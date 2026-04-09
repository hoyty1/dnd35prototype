using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central game manager handling turn flow with D&D 3.5 action economy.
/// Supports two PC characters and one NPC with intelligent targeting.
/// Turn order: PC1 → PC2 → NPC → repeat
///
/// Action Economy per turn:
/// - 1 Move Action + 1 Standard Action (in any order)
/// - OR 1 Full-Round Action (uses both - e.g., Full Attack, Dual Wield)
/// - Standard can be converted to a second Move Action
/// - Plus 1 Swift Action per turn (simplified for now)
/// - Plus unlimited Free Actions
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

    // Legacy alias
    public CharacterController PC { get => PC1; set => PC1 = value; }

    [Header("UI")]
    public CombatUI CombatUI;
    public InventoryUI InventoryUI;

    // Game state - simplified phases
    public enum TurnPhase { PC1Turn, PC2Turn, NPCTurn, CombatOver }

    // Sub-states for player turns
    public enum PlayerSubPhase { ChoosingAction, Moving, SelectingAttackTarget, Animating }

    public TurnPhase CurrentPhase { get; private set; }
    public PlayerSubPhase CurrentSubPhase { get; private set; }

    /// <summary>Returns the PC whose turn it currently is.</summary>
    public CharacterController ActivePC
    {
        get
        {
            if (CurrentPhase == TurnPhase.PC1Turn) return PC1;
            if (CurrentPhase == TurnPhase.PC2Turn) return PC2;
            return null;
        }
    }

    public bool IsPlayerTurn => ActivePC != null;

    // Current attack mode being selected for
    private enum PendingAttackMode { Single, FullAttack, DualWield }
    private PendingAttackMode _pendingAttackMode;

    private List<HexCell> _highlightedCells = new List<HexCell>();
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
        Grid.GenerateGrid();
        SetupCharacters();
        CenterCamera();
        _mainCam = Camera.main;
        StartPCTurn(PC1);
        Debug.Log("[GameManager] Initialization complete. Phase: " + CurrentPhase);
    }

    /// <summary>
    /// Handle input every frame - inventory toggle and hex clicks.
    /// </summary>
    private void Update()
    {
        HandleInventoryInput();

        if (!IsPlayerTurn) return;
        if (CurrentSubPhase == PlayerSubPhase.Animating) return;
        if (InventoryUI != null && InventoryUI.IsOpen) return;

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
                Debug.Log($"[GameManager] Hex clicked: ({cell.Q}, {cell.R}) Phase={CurrentPhase} Sub={CurrentSubPhase}");
                OnHexClicked(cell);
            }
        }
    }

    private void HandleInventoryInput()
    {
        bool iPressed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.I))
            iPressed = true;
#endif

#if ENABLE_INPUT_SYSTEM
        if (!iPressed)
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.iKey.wasPressedThisFrame)
                iPressed = true;
        }
#endif

        if (iPressed && InventoryUI != null)
        {
            if (InventoryUI.IsOpen)
            {
                InventoryUI.Close();
                // Refresh action buttons after inventory changes (might affect dual wield)
                if (IsPlayerTurn && ActivePC != null && CurrentSubPhase == PlayerSubPhase.ChoosingAction)
                    ShowActionChoices();
            }
            else if (IsPlayerTurn && ActivePC != null)
            {
                InventoryUI.Toggle(ActivePC);
            }
        }
    }

    private void CloseInventoryIfOpen()
    {
        if (InventoryUI != null && InventoryUI.IsOpen)
            InventoryUI.Close();
    }

    private void SetupCharacters()
    {
        // Initialize race database
        RaceDatabase.Init();

        // ==========================================
        // PC1: "Aldric" - Dwarf Fighter (Level 3)
        // Base scores: STR 16, DEX 12, CON 14, WIS 10, INT 10, CHA 13
        // Dwarf racial: CON +2 = 16, CHA -2 = 11
        // Dwarf speed: 20 ft (4 hexes), NOT reduced by armor
        // ==========================================
        CharacterStats pc1Stats = new CharacterStats(
            name: "Aldric",
            level: 3,
            characterClass: "Fighter",
            str: 16, dex: 12, con: 14, wis: 10, intelligence: 10, cha: 13,
            bab: 3,
            armorBonus: 4,
            shieldBonus: 2,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 4,  // overridden by race
            atkRange: 1,
            baseHitDieHP: 22,
            raceName: "Dwarf"
        );

        Debug.Log($"[GameManager] Aldric (Dwarf Fighter): STR {pc1Stats.STR} DEX {pc1Stats.DEX} CON {pc1Stats.CON} " +
                  $"WIS {pc1Stats.WIS} INT {pc1Stats.INT} CHA {pc1Stats.CHA} | " +
                  $"HP {pc1Stats.MaxHP} | Speed {pc1Stats.MoveRange} hexes ({pc1Stats.SpeedInFeet} ft)");

        Sprite pcAlive = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        Vector2Int pc1Start = new Vector2Int(3, 8);
        PC1.Init(pc1Stats, pc1Start, pcAlive, pcDead);

        var pc1Inv = PC1.gameObject.AddComponent<InventoryComponent>();
        pc1Inv.Init(pc1Stats);
        pc1Inv.SetupAldric();

        // ==========================================
        // PC2: "Lyra" - Elf Rogue (Level 3)
        // Base scores: STR 12, DEX 17, CON 12, WIS 13, INT 14, CHA 10
        // Elf racial: DEX +2 = 19, CON -2 = 10
        // Elf speed: 30 ft (6 hexes)
        // Elf weapon proficiencies: longsword, rapier, longbow, shortbow
        // ==========================================
        CharacterStats pc2Stats = new CharacterStats(
            name: "Lyra",
            level: 3,
            characterClass: "Rogue",
            str: 12, dex: 17, con: 12, wis: 13, intelligence: 14, cha: 10,
            bab: 2,
            armorBonus: 2,
            shieldBonus: 0,
            damageDice: 6,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 5,  // overridden by race
            atkRange: 1,
            baseHitDieHP: 15,
            raceName: "Elf"
        );

        Debug.Log($"[GameManager] Lyra (Elf Rogue): STR {pc2Stats.STR} DEX {pc2Stats.DEX} CON {pc2Stats.CON} " +
                  $"WIS {pc2Stats.WIS} INT {pc2Stats.INT} CHA {pc2Stats.CHA} | " +
                  $"HP {pc2Stats.MaxHP} | Speed {pc2Stats.MoveRange} hexes ({pc2Stats.SpeedInFeet} ft)");

        Vector2Int pc2Start = new Vector2Int(3, 12);
        PC2.Init(pc2Stats, pc2Start, pcAlive, pcDead);

        var pc2Inv = PC2.gameObject.AddComponent<InventoryComponent>();
        pc2Inv.Init(pc2Stats);
        pc2Inv.SetupLyra();

        SpriteRenderer pc2SR = PC2.GetComponent<SpriteRenderer>();
        if (pc2SR != null)
            pc2SR.color = new Color(0.6f, 0.7f, 1f, 1f);

        // ==========================================
        // NPC: "Goblin Warchief" - Goblinoid creature
        // Goblinoid tag: triggers Dwarf's +1 racial attack bonus
        // ==========================================
        CharacterStats npcStats = new CharacterStats(
            name: "Goblin Warchief",
            level: 2,
            characterClass: "Warrior",
            str: 14, dex: 15, con: 13, wis: 10, intelligence: 10, cha: 8,
            bab: 2,
            armorBonus: 3,
            shieldBonus: 1,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 3,
            atkRange: 1,
            baseHitDieHP: 12
        );
        // Tag the goblin as a Goblinoid for racial attack bonus purposes
        npcStats.CreatureTags.Add("Goblinoid");

        Sprite npcAlive = LoadSprite("Sprites/npc_enemy_alive");
        Sprite npcDead = LoadSprite("Sprites/npc_enemy_dead");

        Vector2Int npcStart = new Vector2Int(16, 10);
        NPC.Init(npcStats, npcStart, npcAlive, npcDead);

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

    // ========== TURN MANAGEMENT WITH ACTION ECONOMY ==========

    /// <summary>
    /// Begin a PC's turn with full action economy.
    /// </summary>
    public void StartPCTurn(CharacterController pc)
    {
        if (CurrentPhase == TurnPhase.CombatOver) return;

        CloseInventoryIfOpen();

        // If this PC is dead, advance
        if (pc.Stats.IsDead)
        {
            if (pc == PC1) { StartPCTurn(PC2); return; }
            if (pc == PC2) { StartCoroutine(NPCTurnCoroutine()); return; }
        }

        pc.StartNewTurn();

        CurrentPhase = (pc == PC1) ? TurnPhase.PC1Turn : TurnPhase.PC2Turn;
        CurrentSubPhase = PlayerSubPhase.ChoosingAction;

        string pcLabel = (pc == PC1) ? "Hero 1" : "Hero 2";
        CombatUI.SetActivePC(pc == PC1 ? 1 : 2);

        ShowActionChoices();
    }

    // Legacy helper
    public void StartPlayerTurn() => StartPCTurn(PC1);

    /// <summary>
    /// Show the action choice UI for the current PC.
    /// </summary>
    private void ShowActionChoices()
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;

        CurrentSubPhase = PlayerSubPhase.ChoosingAction;

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        // Highlight current position
        HexCell current = Grid.GetCell(pc.GridPosition);
        if (current != null)
            current.SetHighlight(HighlightType.Selected);

        // Update action buttons based on action economy
        CombatUI.SetActionButtonsVisible(true);
        CombatUI.UpdateActionButtons(pc);

        // Build status message
        string pcName = pc.Stats.CharacterName;
        string actionInfo = pc.Actions.GetStatusString();

        // Check if dual wield is possible and add info
        string dwInfo = "";
        if (pc.CanDualWield())
            dwInfo = "\n" + pc.GetDualWieldDescription();

        CombatUI.SetTurnIndicator($"{pcName}'s Turn - Choose an action  [I] Inventory\n{actionInfo}{dwInfo}");

        // Auto-end turn if no actions left
        if (!pc.Actions.HasAnyActionLeft)
        {
            CombatUI.SetTurnIndicator($"{pcName}'s Turn - No actions remaining");
            StartCoroutine(DelayedEndActivePCTurn(1.0f));
        }
    }

    // ========== ACTION BUTTON HANDLERS ==========

    /// <summary>Called when Move button is pressed.</summary>
    public void OnMoveButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;

        // Determine if this uses move action or converted standard
        if (pc.Actions.HasMoveAction)
        {
            // Normal move action
        }
        else if (pc.Actions.CanConvertStandardToMove)
        {
            // Will convert standard to move when actually moving
        }
        else
        {
            return; // Can't move
        }

        CurrentSubPhase = PlayerSubPhase.Moving;
        ShowMovementRange(pc);
        CombatUI.SetActionButtonsVisible(false);
        CombatUI.SetTurnIndicator($"{pc.Stats.CharacterName} - Click a tile to move (or own tile to cancel)");
    }

    /// <summary>Called when Attack (Standard Action) button is pressed.</summary>
    public void OnAttackButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || !pc.Actions.HasStandardAction) return;

        _pendingAttackMode = PendingAttackMode.Single;
        CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
        ShowAttackTargets(pc);
    }

    /// <summary>Called when Full Attack button is pressed.</summary>
    public void OnFullAttackButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || !pc.Actions.HasFullRoundAction) return;

        _pendingAttackMode = PendingAttackMode.FullAttack;
        CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
        ShowAttackTargets(pc);
    }

    /// <summary>Called when Dual Wield button is pressed.</summary>
    public void OnDualWieldButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || !pc.Actions.HasFullRoundAction || !pc.CanDualWield()) return;

        _pendingAttackMode = PendingAttackMode.DualWield;
        CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;

        var (mainPen, offPen, lightOff) = pc.GetDualWieldPenalties();
        string penaltyInfo = lightOff ? $"(light off-hand: {mainPen}/{offPen})" : $"(penalties: {mainPen}/{offPen})";

        ShowAttackTargets(pc);
        CombatUI.SetTurnIndicator($"DUAL WIELD: Select target {penaltyInfo}");
    }

    /// <summary>Called when End Turn button is pressed.</summary>
    public void OnEndTurnButtonPressed()
    {
        if (!IsPlayerTurn) return;
        EndActivePCTurn();
    }

    // ========== MOVEMENT ==========

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

    // ========== ATTACK TARGET SELECTION ==========

    private void ShowAttackTargets(CharacterController pc)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        CharacterController otherPC = (pc == PC1) ? PC2 : PC1;

        List<HexCell> rangeCells = Grid.GetCellsInRange(pc.GridPosition, pc.Stats.AttackRange);
        bool hasTarget = false;
        bool anyFlanking = false;

        foreach (var cell in rangeCells)
        {
            if (cell.IsOccupied && cell.Occupant != pc && !cell.Occupant.Stats.IsDead)
            {
                bool flanking = !otherPC.Stats.IsDead &&
                    CombatUtils.IsFlanking(pc.GridPosition, otherPC.GridPosition, cell.Occupant.GridPosition);

                if (flanking)
                {
                    cell.SetHighlight(HighlightType.Flanking);
                    anyFlanking = true;
                }
                else
                {
                    cell.SetHighlight(HighlightType.Attack);
                }
                _highlightedCells.Add(cell);
                hasTarget = true;
            }
        }

        if (hasTarget)
        {
            string flankMsg = anyFlanking ? " (FLANKING available! +2 to hit)" : "";
            string modeStr = "";
            switch (_pendingAttackMode)
            {
                case PendingAttackMode.Single: modeStr = "ATTACK"; break;
                case PendingAttackMode.FullAttack: modeStr = "FULL ATTACK"; break;
                case PendingAttackMode.DualWield: modeStr = "DUAL WIELD"; break;
            }
            if (CombatUI.TurnIndicatorText != null && !CombatUI.TurnIndicatorText.text.Contains("DUAL WIELD"))
                CombatUI.SetTurnIndicator($"{modeStr}: Click an enemy to attack!{flankMsg}");
        }
        else
        {
            CombatUI.SetTurnIndicator("No enemies in range!");
            StartCoroutine(ReturnToActionChoicesAfterDelay(1.5f));
        }
    }

    private IEnumerator ReturnToActionChoicesAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (IsPlayerTurn)
            ShowActionChoices();
    }

    // ========== HEX CLICK HANDLING ==========

    public void OnHexClicked(HexCell cell)
    {
        if (CurrentPhase == TurnPhase.CombatOver) return;

        CharacterController pc = ActivePC;
        if (pc == null) return;

        switch (CurrentSubPhase)
        {
            case PlayerSubPhase.Moving:
                HandleMovementClick(pc, cell);
                break;

            case PlayerSubPhase.SelectingAttackTarget:
                HandleAttackTargetClick(pc, cell);
                break;

            case PlayerSubPhase.ChoosingAction:
                // Clicking on own tile when choosing does nothing special
                break;
        }
    }

    private void HandleMovementClick(CharacterController pc, HexCell cell)
    {
        if (cell.Coords == pc.GridPosition)
        {
            // Click own tile to cancel movement and return to action choices
            ShowActionChoices();
            return;
        }

        if (_highlightedCells.Contains(cell) && !cell.IsOccupied)
        {
            // Consume the appropriate action
            if (pc.Actions.HasMoveAction)
            {
                pc.Actions.UseMoveAction();
            }
            else if (pc.Actions.CanConvertStandardToMove)
            {
                pc.Actions.ConvertStandardToMove();
            }

            pc.MoveToCell(cell);
            CombatUI.UpdateAllStats(PC1, PC2, NPC);

            // Return to action choices (player can still use standard action if available)
            ShowActionChoices();
        }
    }

    private void HandleAttackTargetClick(CharacterController pc, HexCell cell)
    {
        // Allow clicking own tile or empty tile to cancel
        if (!cell.IsOccupied || cell.Occupant == pc || cell.Occupant.Stats.IsDead)
        {
            if (cell.Coords == pc.GridPosition || !_highlightedCells.Contains(cell))
            {
                // Cancel attack selection
                ShowActionChoices();
                return;
            }
        }

        if (cell.IsOccupied && cell.Occupant != pc && !cell.Occupant.Stats.IsDead && _highlightedCells.Contains(cell))
        {
            PerformPlayerAttack(pc, cell.Occupant);
        }
    }

    // ========== ATTACK EXECUTION ==========

    private void PerformPlayerAttack(CharacterController attacker, CharacterController target)
    {
        CurrentSubPhase = PlayerSubPhase.Animating;

        // Check for flanking
        var allies = new List<CharacterController>();
        if (attacker == PC1) allies.Add(PC2);
        else if (attacker == PC2) allies.Add(PC1);

        CharacterController flankPartner;
        bool isFlanking = CombatUtils.IsAttackerFlanking(attacker, target, allies, out flankPartner);
        int flankBonus = isFlanking ? CombatUtils.FlankingAttackBonus : 0;
        string partnerName = flankPartner != null ? flankPartner.Stats.CharacterName : "";

        switch (_pendingAttackMode)
        {
            case PendingAttackMode.Single:
                PerformSingleAttack(attacker, target, isFlanking, flankBonus, partnerName);
                break;

            case PendingAttackMode.FullAttack:
                PerformFullAttack(attacker, target, isFlanking, flankBonus, partnerName);
                break;

            case PendingAttackMode.DualWield:
                PerformDualWieldAttack(attacker, target, isFlanking, flankBonus, partnerName);
                break;
        }
    }

    private void PerformSingleAttack(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName)
    {
        // Standard Action
        attacker.Actions.UseStandardAction();

        CombatResult result = attacker.Attack(target, isFlanking, flankBonus, partnerName);
        _lastCombatLog = result.GetSummary();
        CombatUI.ShowCombatLog(_lastCombatLog);
        CombatUI.UpdateAllStats(PC1, PC2, NPC);

        Grid.ClearAllHighlights();

        if (result.TargetKilled && target == NPC)
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI.SetTurnIndicator("VICTORY! Enemy defeated!");
            CombatUI.SetActionButtonsVisible(false);
            return;
        }

        // After standard action, check if there are more actions available
        StartCoroutine(AfterAttackDelay(attacker, 1.5f));
    }

    private void PerformFullAttack(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName)
    {
        // Full-Round Action
        attacker.Actions.UseFullRoundAction();

        FullAttackResult result = attacker.FullAttack(target, isFlanking, flankBonus, partnerName);
        _lastCombatLog = result.GetFullSummary();
        CombatUI.ShowCombatLog(_lastCombatLog);
        CombatUI.UpdateAllStats(PC1, PC2, NPC);

        Grid.ClearAllHighlights();

        if (result.TargetKilled && target == NPC)
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI.SetTurnIndicator("VICTORY! Enemy defeated!");
            CombatUI.SetActionButtonsVisible(false);
            return;
        }

        // Full-round action ends the turn
        StartCoroutine(DelayedEndActivePCTurn(2.0f));
    }

    private void PerformDualWieldAttack(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName)
    {
        // Full-Round Action
        attacker.Actions.UseFullRoundAction();

        FullAttackResult result = attacker.DualWieldAttack(target, isFlanking, flankBonus, partnerName);
        _lastCombatLog = result.GetFullSummary();
        CombatUI.ShowCombatLog(_lastCombatLog);
        CombatUI.UpdateAllStats(PC1, PC2, NPC);

        Grid.ClearAllHighlights();

        if (result.TargetKilled && target == NPC)
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI.SetTurnIndicator("VICTORY! Enemy defeated!");
            CombatUI.SetActionButtonsVisible(false);
            return;
        }

        // Full-round action ends the turn
        StartCoroutine(DelayedEndActivePCTurn(2.0f));
    }

    /// <summary>
    /// After a standard action attack, return to action choices if more actions available,
    /// otherwise end turn.
    /// </summary>
    private IEnumerator AfterAttackDelay(CharacterController pc, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (CurrentPhase == TurnPhase.CombatOver) yield break;

        // Check if the PC still has actions (e.g., unused move action)
        if (pc.Actions.HasAnyActionLeft)
        {
            ShowActionChoices();
        }
        else
        {
            EndActivePCTurn();
        }
    }

    // ========== TURN ENDING ==========

    private void EndActivePCTurn()
    {
        CharacterController pc = ActivePC;
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        if (CurrentPhase == TurnPhase.CombatOver) return;

        if (pc == PC1)
        {
            StartPCTurn(PC2);
        }
        else
        {
            StartCoroutine(NPCTurnCoroutine());
        }
    }

    private IEnumerator DelayedEndActivePCTurn(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (CurrentPhase != TurnPhase.CombatOver)
            EndActivePCTurn();
    }

    // ========== NPC AI TURN ==========

    private IEnumerator NPCTurnCoroutine()
    {
        CurrentPhase = TurnPhase.NPCTurn;
        CombatUI.SetTurnIndicator("Enemy Turn...");
        CombatUI.SetActivePC(0);
        CombatUI.SetActionButtonsVisible(false);

        if (NPC.Stats.IsDead)
        {
            yield return new WaitForSeconds(0.5f);
            StartPCTurn(PC1);
            yield break;
        }

        NPC.StartNewTurn();
        yield return new WaitForSeconds(0.8f);

        CharacterController closestPC = GetClosestAlivePC();

        if (closestPC == null)
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI.SetTurnIndicator("DEFEAT! All heroes have fallen!");
            CombatUI.SetActionButtonsVisible(false);
            yield break;
        }

        int distToTarget = HexUtils.HexDistance(NPC.GridPosition, closestPC.GridPosition);

        // NPC uses simple action economy: move then attack
        if (distToTarget > NPC.Stats.AttackRange)
        {
            HexCell bestCell = FindBestMoveToward(NPC, closestPC.GridPosition);
            if (bestCell != null)
            {
                NPC.MoveToCell(bestCell);
                NPC.Actions.UseMoveAction();
                CombatUI.ShowCombatLog($"{NPC.Stats.CharacterName} moves toward {closestPC.Stats.CharacterName}!");
                yield return new WaitForSeconds(0.6f);
            }
        }

        // Re-evaluate
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
            NPC.Actions.UseStandardAction();
            CombatResult result = NPC.Attack(closestPC);
            _lastCombatLog = result.GetSummary();
            CombatUI.ShowCombatLog(_lastCombatLog);
            CombatUI.UpdateAllStats(PC1, PC2, NPC);

            if (result.TargetKilled)
            {
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

    private CharacterController GetClosestAlivePC()
    {
        bool pc1Alive = !PC1.Stats.IsDead;
        bool pc2Alive = !PC2.Stats.IsDead;

        if (!pc1Alive && !pc2Alive) return null;
        if (pc1Alive && !pc2Alive) return PC1;
        if (!pc1Alive && pc2Alive) return PC2;

        int dist1 = HexUtils.HexDistance(NPC.GridPosition, PC1.GridPosition);
        int dist2 = HexUtils.HexDistance(NPC.GridPosition, PC2.GridPosition);
        return dist1 <= dist2 ? PC1 : PC2;
    }

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
