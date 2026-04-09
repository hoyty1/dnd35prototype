using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central game manager handling turn flow, input routing, and game state.
/// Supports two PC characters and one NPC with intelligent targeting.
/// Turn order: PC1 → PC2 → NPC → repeat
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Grid")]
    public HexGrid Grid;

    [Header("Characters")]
    public CharacterController PC1;
    public CharacterController PC2;
    public CharacterController NPC;

    // Legacy alias so existing code referencing GM.PC still compiles if needed
    public CharacterController PC { get => PC1; set => PC1 = value; }

    [Header("UI")]
    public CombatUI CombatUI;

    // Game state
    public enum TurnPhase { PC1Move, PC1Action, PC2Move, PC2Action, NPCTurn, CombatOver }
    public TurnPhase CurrentPhase { get; private set; }

    /// <summary>Returns the PC whose turn it currently is, or null if not a player phase.</summary>
    public CharacterController ActivePC
    {
        get
        {
            if (CurrentPhase == TurnPhase.PC1Move || CurrentPhase == TurnPhase.PC1Action) return PC1;
            if (CurrentPhase == TurnPhase.PC2Move || CurrentPhase == TurnPhase.PC2Action) return PC2;
            return null;
        }
    }

    public bool IsPlayerTurn => ActivePC != null;

    private List<HexCell> _highlightedCells = new List<HexCell>();
    private bool _waitingForAttackTarget;
    private string _lastCombatLog = "";
    private Camera _mainCam;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Generate grid
        Grid.GenerateGrid();

        // Create and place characters
        SetupCharacters();

        // Center camera
        CenterCamera();

        // Cache camera reference for raycasting
        _mainCam = Camera.main;

        // Start with PC1 turn
        StartPCTurn(PC1);

        Debug.Log("[GameManager] Initialization complete. Phase: " + CurrentPhase);
    }

    /// <summary>
    /// Detect hex clicks via Physics2D raycasting every frame.
    /// </summary>
    private void Update()
    {
        // Only process clicks during player phases
        if (!IsPlayerTurn) return;

        bool clicked = false;
        Vector3 mouseScreenPos = Vector3.zero;

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0))
        {
            clicked = true;
            mouseScreenPos = Input.mousePosition;
        }
#endif

#if ENABLE_INPUT_SYSTEM
        if (!clicked)
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                clicked = true;
                mouseScreenPos = mouse.position.ReadValue();
            }
        }
#endif

        if (!clicked || _mainCam == null) return;

        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Vector2 worldPoint = _mainCam.ScreenToWorldPoint(mouseScreenPos);
        RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero);

        if (hit.collider != null)
        {
            HexCell cell = hit.collider.GetComponent<HexCell>();
            if (cell != null)
            {
                Debug.Log($"[GameManager] Hex clicked: ({cell.Q}, {cell.R}) Phase={CurrentPhase}");
                OnHexClicked(cell);
            }
        }
    }

    private void SetupCharacters()
    {
        // --- PC1 Setup ---
        CharacterStats pc1Stats = new CharacterStats(
            name: "Hero 1",
            maxHP: 30,
            ac: 14,
            atkBonus: 5,
            minDmg: 3,
            maxDmg: 8,
            moveRange: 4,
            atkRange: 1
        );

        Sprite pcAlive = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        Vector2Int pc1Start = new Vector2Int(3, 8);
        PC1.Init(pc1Stats, pc1Start, pcAlive, pcDead);

        // --- PC2 Setup ---
        CharacterStats pc2Stats = new CharacterStats(
            name: "Hero 2",
            maxHP: 30,
            ac: 14,
            atkBonus: 5,
            minDmg: 3,
            maxDmg: 8,
            moveRange: 4,
            atkRange: 1
        );

        Vector2Int pc2Start = new Vector2Int(3, 12);
        PC2.Init(pc2Stats, pc2Start, pcAlive, pcDead);

        // Apply a blue tint to PC2 so the two heroes are visually distinct
        SpriteRenderer pc2SR = PC2.GetComponent<SpriteRenderer>();
        if (pc2SR != null)
            pc2SR.color = new Color(0.6f, 0.7f, 1f, 1f); // light blue tint

        // --- NPC Setup ---
        CharacterStats npcStats = new CharacterStats(
            name: "Goblin",
            maxHP: 20,
            ac: 12,
            atkBonus: 3,
            minDmg: 2,
            maxDmg: 6,
            moveRange: 3,
            atkRange: 1
        );

        Sprite npcAlive = LoadSprite("Sprites/npc_enemy_alive");
        Sprite npcDead = LoadSprite("Sprites/npc_enemy_dead");

        Vector2Int npcStart = new Vector2Int(16, 10);
        NPC.Init(npcStats, npcStart, npcAlive, npcDead);

        // Update UI
        CombatUI.UpdateAllStats(PC1, PC2, NPC);
    }

    private Sprite LoadSprite(string path)
    {
        Sprite s = Resources.Load<Sprite>(path);
        if (s == null)
        {
            Texture2D tex = Resources.Load<Texture2D>(path);
            if (tex != null)
            {
                s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), 64f);
            }
        }
        return s;
    }

    private void CenterCamera()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 center = Grid.GetGridCenter();
            cam.transform.position = new Vector3(center.x, center.y, -10f);
            cam.orthographicSize = 10f;
        }
    }

    // ========== TURN MANAGEMENT ==========

    /// <summary>
    /// Begin a PC's turn. Handles skipping dead PCs.
    /// </summary>
    public void StartPCTurn(CharacterController pc)
    {
        if (CurrentPhase == TurnPhase.CombatOver) return;

        // If this PC is dead, advance to the next phase
        if (pc.Stats.IsDead)
        {
            if (pc == PC1) { StartPCTurn(PC2); return; }
            if (pc == PC2) { StartCoroutine(NPCTurnCoroutine()); return; }
        }

        pc.StartNewTurn();

        if (pc == PC1)
            CurrentPhase = TurnPhase.PC1Move;
        else
            CurrentPhase = TurnPhase.PC2Move;

        string pcLabel = (pc == PC1) ? "Hero 1" : "Hero 2";
        CombatUI.SetTurnIndicator($"{pcLabel}'s Turn - Click a highlighted tile to move");
        CombatUI.SetActionButtonsVisible(false);
        CombatUI.SetActivePC(pc == PC1 ? 1 : 2);
        _waitingForAttackTarget = false;

        ShowMovementRange(pc);
    }

    // Legacy helper — starts PC1 turn (used nowhere now, kept for safety)
    public void StartPlayerTurn() => StartPCTurn(PC1);

    private void ShowMovementRange(CharacterController pc)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        List<HexCell> moveCells = Grid.GetCellsInRange(pc.GridPosition, pc.Stats.MoveRange);
        foreach (var cell in moveCells)
        {
            if (!cell.IsOccupied)
            {
                cell.SetHighlight(HighlightType.Move);
                _highlightedCells.Add(cell);
            }
        }

        HexCell current = Grid.GetCell(pc.GridPosition);
        if (current != null)
            current.SetHighlight(HighlightType.Selected);
    }

    private void EnterActionPhase()
    {
        CharacterController pc = ActivePC;
        if (pc == PC1)
            CurrentPhase = TurnPhase.PC1Action;
        else
            CurrentPhase = TurnPhase.PC2Action;

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        CombatUI.SetTurnIndicator($"{pc.Stats.CharacterName} - Choose an action");
        CombatUI.SetActionButtonsVisible(true);
    }

    // Called from UI Attack button
    public void OnAttackButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;
        if (CurrentPhase != TurnPhase.PC1Action && CurrentPhase != TurnPhase.PC2Action) return;

        _waitingForAttackTarget = true;
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        List<HexCell> rangeCells = Grid.GetCellsInRange(pc.GridPosition, pc.Stats.AttackRange);
        bool hasTarget = false;
        foreach (var cell in rangeCells)
        {
            if (cell.IsOccupied && cell.Occupant != pc && !cell.Occupant.Stats.IsDead)
            {
                cell.SetHighlight(HighlightType.Attack);
                _highlightedCells.Add(cell);
                hasTarget = true;
            }
        }

        if (hasTarget)
        {
            CombatUI.SetTurnIndicator("Click an enemy to attack!");
        }
        else
        {
            CombatUI.SetTurnIndicator("No enemies in range! Ending turn...");
            _waitingForAttackTarget = false;
            StartCoroutine(DelayedEndActivePCTurn(1.5f));
        }
    }

    // Called from UI End Turn button
    public void OnEndTurnButtonPressed()
    {
        if (!IsPlayerTurn) return;
        EndActivePCTurn();
    }

    /// <summary>
    /// End the current active PC's turn and advance to the next phase.
    /// </summary>
    private void EndActivePCTurn()
    {
        CharacterController pc = ActivePC;
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        _waitingForAttackTarget = false;
        CombatUI.SetActionButtonsVisible(false);

        if (CurrentPhase == TurnPhase.CombatOver) return;

        if (pc == PC1)
        {
            // Advance to PC2's turn
            StartPCTurn(PC2);
        }
        else
        {
            // PC2 done → NPC turn
            StartCoroutine(NPCTurnCoroutine());
        }
    }

    private IEnumerator DelayedEndActivePCTurn(float delay)
    {
        yield return new WaitForSeconds(delay);
        EndActivePCTurn();
    }

    // ========== HEX CLICK HANDLING ==========

    public void OnHexClicked(HexCell cell)
    {
        if (CurrentPhase == TurnPhase.CombatOver) return;

        CharacterController pc = ActivePC;
        if (pc == null) return;

        // Move phase
        if (CurrentPhase == TurnPhase.PC1Move || CurrentPhase == TurnPhase.PC2Move)
        {
            if (_highlightedCells.Contains(cell) && !cell.IsOccupied)
            {
                pc.MoveToCell(cell);
                CombatUI.UpdateAllStats(PC1, PC2, NPC);
                EnterActionPhase();
            }
            else if (cell.Coords == pc.GridPosition)
            {
                // Click own tile to skip movement
                EnterActionPhase();
            }
        }
        // Action phase: attack target selection
        else if ((CurrentPhase == TurnPhase.PC1Action || CurrentPhase == TurnPhase.PC2Action) && _waitingForAttackTarget)
        {
            if (cell.IsOccupied && cell.Occupant != pc && !cell.Occupant.Stats.IsDead)
            {
                if (_highlightedCells.Contains(cell))
                {
                    PerformPlayerAttack(pc, cell.Occupant);
                }
            }
        }
    }

    private void PerformPlayerAttack(CharacterController attacker, CharacterController target)
    {
        CombatResult result = attacker.Attack(target);
        _lastCombatLog = result.GetSummary();
        CombatUI.ShowCombatLog(_lastCombatLog);
        CombatUI.UpdateAllStats(PC1, PC2, NPC);

        _waitingForAttackTarget = false;
        Grid.ClearAllHighlights();

        if (result.TargetKilled)
        {
            // NPC killed → check victory
            if (target == NPC)
            {
                CurrentPhase = TurnPhase.CombatOver;
                CombatUI.SetTurnIndicator("VICTORY! Enemy defeated!");
                CombatUI.SetActionButtonsVisible(false);
                return;
            }
        }

        // End this PC's turn after attack
        StartCoroutine(DelayedEndActivePCTurn(1.5f));
    }

    // ========== NPC AI TURN ==========

    private IEnumerator NPCTurnCoroutine()
    {
        CurrentPhase = TurnPhase.NPCTurn;
        CombatUI.SetTurnIndicator("Enemy Turn...");
        CombatUI.SetActivePC(0); // no PC active

        if (NPC.Stats.IsDead)
        {
            yield return new WaitForSeconds(0.5f);
            StartPCTurn(PC1);
            yield break;
        }

        NPC.StartNewTurn();
        yield return new WaitForSeconds(0.8f);

        // Determine closest alive PC
        CharacterController closestPC = GetClosestAlivePC();

        if (closestPC == null)
        {
            // Both PCs dead — should not normally reach here but handle gracefully
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI.SetTurnIndicator("DEFEAT! All heroes have fallen!");
            CombatUI.SetActionButtonsVisible(false);
            yield break;
        }

        int distToTarget = HexUtils.HexDistance(NPC.GridPosition, closestPC.GridPosition);

        // Move toward closest PC if not in attack range
        if (distToTarget > NPC.Stats.AttackRange)
        {
            HexCell bestCell = FindBestMoveToward(NPC, closestPC.GridPosition);
            if (bestCell != null)
            {
                NPC.MoveToCell(bestCell);
                CombatUI.ShowCombatLog($"{NPC.Stats.CharacterName} moves toward {closestPC.Stats.CharacterName}!");
                yield return new WaitForSeconds(0.6f);
            }
        }

        // Re-evaluate closest target after moving (could have changed)
        closestPC = GetClosestAlivePC();
        if (closestPC == null)
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI.SetTurnIndicator("DEFEAT! All heroes have fallen!");
            yield break;
        }

        distToTarget = HexUtils.HexDistance(NPC.GridPosition, closestPC.GridPosition);

        if (distToTarget <= NPC.Stats.AttackRange && !closestPC.Stats.IsDead)
        {
            CombatResult result = NPC.Attack(closestPC);
            _lastCombatLog = result.GetSummary();
            CombatUI.ShowCombatLog(_lastCombatLog);
            CombatUI.UpdateAllStats(PC1, PC2, NPC);

            if (result.TargetKilled)
            {
                // Check if BOTH PCs are dead
                if (PC1.Stats.IsDead && PC2.Stats.IsDead)
                {
                    CurrentPhase = TurnPhase.CombatOver;
                    CombatUI.SetTurnIndicator("DEFEAT! All heroes have fallen!");
                    CombatUI.SetActionButtonsVisible(false);
                    yield break;
                }
                else
                {
                    CombatUI.ShowCombatLog(_lastCombatLog + $"\n{closestPC.Stats.CharacterName} has fallen, but the fight continues!");
                }
            }

            yield return new WaitForSeconds(1.2f);
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
        }

        StartPCTurn(PC1);
    }

    /// <summary>
    /// Returns the alive PC closest to the NPC, or null if all PCs are dead.
    /// </summary>
    private CharacterController GetClosestAlivePC()
    {
        bool pc1Alive = !PC1.Stats.IsDead;
        bool pc2Alive = !PC2.Stats.IsDead;

        if (!pc1Alive && !pc2Alive) return null;
        if (pc1Alive && !pc2Alive) return PC1;
        if (!pc1Alive && pc2Alive) return PC2;

        // Both alive — pick the closer one
        int dist1 = HexUtils.HexDistance(NPC.GridPosition, PC1.GridPosition);
        int dist2 = HexUtils.HexDistance(NPC.GridPosition, PC2.GridPosition);
        return dist1 <= dist2 ? PC1 : PC2;
    }

    /// <summary>
    /// Find the best cell for NPC to move toward a target position.
    /// </summary>
    private HexCell FindBestMoveToward(CharacterController mover, Vector2Int targetPos)
    {
        List<HexCell> moveCells = Grid.GetCellsInRange(mover.GridPosition, mover.Stats.MoveRange);
        HexCell bestCell = null;
        int bestDist = int.MaxValue;

        foreach (var cell in moveCells)
        {
            if (cell.IsOccupied) continue;

            int dist = HexUtils.HexDistance(cell.Coords, targetPos);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestCell = cell;
            }
        }

        return bestCell;
    }
}
