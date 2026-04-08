using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central game manager handling turn flow, input routing, and game state.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Grid")]
    public HexGrid Grid;

    [Header("Characters")]
    public CharacterController PC;
    public CharacterController NPC;

    [Header("UI")]
    public CombatUI CombatUI;

    // Game state
    public enum TurnPhase { PlayerMove, PlayerAction, NPCTurn, CombatOver }
    public TurnPhase CurrentPhase { get; private set; }
    public bool IsPlayerTurn => CurrentPhase == TurnPhase.PlayerMove || CurrentPhase == TurnPhase.PlayerAction;

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

        // Start with player turn
        StartPlayerTurn();

        Debug.Log("[GameManager] Initialization complete. Phase: " + CurrentPhase);
    }

    /// <summary>
    /// Detect hex clicks via Physics2D raycasting every frame.
    /// This replaces OnMouseDown on HexCell for reliable input across
    /// both the legacy and new Input System configurations in Unity 6+.
    /// </summary>
    private void Update()
    {
        // Only process clicks during player phases
        if (!IsPlayerTurn) return;

        // Detect left mouse button press (works with both Input Systems when activeInputHandler=2)
        bool clicked = false;
        Vector3 mouseScreenPos = Vector3.zero;

        // Try legacy Input first
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0))
        {
            clicked = true;
            mouseScreenPos = Input.mousePosition;
        }
#endif

        // Fallback / alternative: New Input System
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

        // Check if click is over a UI element — if so, ignore (let UI handle it)
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        // Raycast into 2D world
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
        // --- PC Setup ---
        CharacterStats pcStats = new CharacterStats(
            name: "Hero",
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

        Vector2Int pcStart = new Vector2Int(3, 10);
        PC.Init(pcStats, pcStart, pcAlive, pcDead);

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
        CombatUI.UpdateStats(PC, NPC);
    }

    private Sprite LoadSprite(string path)
    {
        Sprite s = Resources.Load<Sprite>(path);
        if (s == null)
        {
            // Fallback: try loading texture and create sprite
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

    public void StartPlayerTurn()
    {
        if (CurrentPhase == TurnPhase.CombatOver) return;

        PC.StartNewTurn();
        CurrentPhase = TurnPhase.PlayerMove;
        CombatUI.SetTurnIndicator("Your Turn - Click a highlighted tile to move");
        CombatUI.SetActionButtonsVisible(false);
        _waitingForAttackTarget = false;

        ShowMovementRange();
    }

    private void ShowMovementRange()
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        List<HexCell> moveCells = Grid.GetCellsInRange(PC.GridPosition, PC.Stats.MoveRange);
        foreach (var cell in moveCells)
        {
            if (!cell.IsOccupied)
            {
                cell.SetHighlight(HighlightType.Move);
                _highlightedCells.Add(cell);
            }
        }

        // Highlight current position
        HexCell current = Grid.GetCell(PC.GridPosition);
        if (current != null)
            current.SetHighlight(HighlightType.Selected);
    }

    private void EnterActionPhase()
    {
        CurrentPhase = TurnPhase.PlayerAction;
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        CombatUI.SetTurnIndicator("Choose an action");
        CombatUI.SetActionButtonsVisible(true);
    }

    // Called from UI Attack button
    public void OnAttackButtonPressed()
    {
        if (CurrentPhase != TurnPhase.PlayerAction) return;

        _waitingForAttackTarget = true;
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        // Highlight enemies in attack range
        List<HexCell> rangeCells = Grid.GetCellsInRange(PC.GridPosition, PC.Stats.AttackRange);
        bool hasTarget = false;
        foreach (var cell in rangeCells)
        {
            if (cell.IsOccupied && cell.Occupant != PC && !cell.Occupant.Stats.IsDead)
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
            StartCoroutine(DelayedEndPlayerTurn(1.5f));
        }
    }

    // Called from UI End Turn button
    public void OnEndTurnButtonPressed()
    {
        if (!IsPlayerTurn) return;
        EndPlayerTurn();
    }

    private void EndPlayerTurn()
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        _waitingForAttackTarget = false;
        CombatUI.SetActionButtonsVisible(false);

        if (CurrentPhase != TurnPhase.CombatOver)
            StartCoroutine(NPCTurnCoroutine());
    }

    private IEnumerator DelayedEndPlayerTurn(float delay)
    {
        yield return new WaitForSeconds(delay);
        EndPlayerTurn();
    }

    // ========== HEX CLICK HANDLING ==========

    public void OnHexClicked(HexCell cell)
    {
        if (CurrentPhase == TurnPhase.CombatOver) return;

        // Player move phase: click to move
        if (CurrentPhase == TurnPhase.PlayerMove)
        {
            if (_highlightedCells.Contains(cell) && !cell.IsOccupied)
            {
                PC.MoveToCell(cell);
                CombatUI.UpdateStats(PC, NPC);
                EnterActionPhase();
            }
            else if (cell.Coords == PC.GridPosition)
            {
                // Click own tile to skip movement
                EnterActionPhase();
            }
        }
        // Player action phase: attack target selection
        else if (CurrentPhase == TurnPhase.PlayerAction && _waitingForAttackTarget)
        {
            if (cell.IsOccupied && cell.Occupant != PC && !cell.Occupant.Stats.IsDead)
            {
                if (_highlightedCells.Contains(cell))
                {
                    PerformPlayerAttack(cell.Occupant);
                }
            }
        }
    }

    private void PerformPlayerAttack(CharacterController target)
    {
        CombatResult result = PC.Attack(target);
        _lastCombatLog = result.GetSummary();
        CombatUI.ShowCombatLog(_lastCombatLog);
        CombatUI.UpdateStats(PC, NPC);

        _waitingForAttackTarget = false;
        Grid.ClearAllHighlights();

        if (result.TargetKilled)
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI.SetTurnIndicator("VICTORY! Enemy defeated!");
            CombatUI.SetActionButtonsVisible(false);
            return;
        }

        // End player turn after attack
        StartCoroutine(DelayedEndPlayerTurn(1.5f));
    }

    // ========== NPC AI TURN ==========

    private IEnumerator NPCTurnCoroutine()
    {
        CurrentPhase = TurnPhase.NPCTurn;
        CombatUI.SetTurnIndicator("Enemy Turn...");

        if (NPC.Stats.IsDead)
        {
            yield return new WaitForSeconds(0.5f);
            StartPlayerTurn();
            yield break;
        }

        NPC.StartNewTurn();
        yield return new WaitForSeconds(0.8f);

        // Simple AI: move toward player then attack if in range
        int distToPlayer = HexUtils.HexDistance(NPC.GridPosition, PC.GridPosition);

        // Move toward player if not in attack range
        if (distToPlayer > NPC.Stats.AttackRange)
        {
            HexCell bestCell = FindBestMoveToward(NPC, PC.GridPosition);
            if (bestCell != null)
            {
                NPC.MoveToCell(bestCell);
                CombatUI.ShowCombatLog($"{NPC.Stats.CharacterName} moves closer!");
                yield return new WaitForSeconds(0.6f);
            }
        }

        // Check attack range after moving
        distToPlayer = HexUtils.HexDistance(NPC.GridPosition, PC.GridPosition);
        if (distToPlayer <= NPC.Stats.AttackRange && !PC.Stats.IsDead)
        {
            CombatResult result = NPC.Attack(PC);
            _lastCombatLog = result.GetSummary();
            CombatUI.ShowCombatLog(_lastCombatLog);
            CombatUI.UpdateStats(PC, NPC);

            if (result.TargetKilled)
            {
                CurrentPhase = TurnPhase.CombatOver;
                CombatUI.SetTurnIndicator("DEFEAT! You have been slain!");
                CombatUI.SetActionButtonsVisible(false);
                yield break;
            }

            yield return new WaitForSeconds(1.2f);
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
        }

        StartPlayerTurn();
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
