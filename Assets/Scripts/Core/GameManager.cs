using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Central game manager handling turn flow with D&D 3.5 action economy.
/// Supports four PC characters and multiple NPC enemies with varied AI behaviors.
/// Turn order is determined by D&D 3.5 Initiative rolls at combat start.
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
    /// <summary>Enable/disable Debug.Log output of all attack rolls to the Unity Console.</summary>
    public static bool LogAttacksToConsole = true;
    public static GameManager Instance { get; private set; }

    [Header("Grid")]
    public SquareGrid Grid;

    [Header("Characters")]
    public CharacterController PC1;
    public CharacterController PC2;
    public CharacterController PC3;
    public CharacterController PC4;
    public CharacterController NPC;  // Legacy field — first NPC for backward compat

    /// <summary>All player characters in the party (supports 4 PCs).</summary>
    public List<CharacterController> PCs = new List<CharacterController>();

    /// <summary>All NPC enemies in the encounter (supports multiple enemies).</summary>
    public List<CharacterController> NPCs = new List<CharacterController>();

    /// <summary>AI behavior assigned to each NPC (indexed same as NPCs list).</summary>
    private List<EnemyAIBehavior> _npcAIBehaviors = new List<EnemyAIBehavior>();

    // Legacy alias
    public CharacterController PC { get => PC1; set => PC1 = value; }

    [Header("UI")]
    public CombatUI CombatUI;
    public InventoryUI InventoryUI;
    public CharacterSheetUI CharacterSheetUI;
    public CharacterCreationUI CharacterCreationUI;
    public SkillsUIPanel SkillsUI;
    public SpellPreparationUI SpellPreparationUI;

    /// <summary>Whether the game is waiting for character creation to complete.</summary>
    public bool WaitingForCharacterCreation { get; private set; }

    // Game state
    public enum TurnPhase { PCTurn, NPCTurn, CombatOver }

    // Sub-states for player turns
    public enum PlayerSubPhase { ChoosingAction, Moving, SelectingAttackTarget, SelectingAoETarget, ConfirmingSelfAoE, Animating }

    public TurnPhase CurrentPhase { get; private set; }
    public PlayerSubPhase CurrentSubPhase { get; private set; }

    // ========== INITIATIVE SYSTEM ==========
    /// <summary>Sorted initiative order for all combatants.</summary>
    private List<InitiativeSystem.InitiativeEntry> _initiativeOrder = new List<InitiativeSystem.InitiativeEntry>();
    /// <summary>Current index in initiative order (whose turn it is).</summary>
    private int _currentInitiativeIndex;

    /// <summary>The character currently taking their turn (PC or NPC).</summary>
    private CharacterController _activeTurnCharacter;

    /// <summary>Returns the PC whose turn it currently is (null during NPC turns).</summary>
    public CharacterController ActivePC
    {
        get
        {
            if (CurrentPhase == TurnPhase.PCTurn && _activeTurnCharacter != null && IsPC(_activeTurnCharacter))
                return _activeTurnCharacter;
            return null;
        }
    }

    public bool IsPlayerTurn => ActivePC != null;

    // Current attack mode being selected for
    private enum PendingAttackMode { Single, FullAttack, DualWield, FlurryOfBlows, CastSpell }
    private PendingAttackMode _pendingAttackMode;
    private SpellData _pendingSpell; // Spell selected for casting
    private MetamagicData _pendingMetamagic; // Metamagic applied to pending spell

    // ========== AOE TARGETING STATE ==========
    private bool _isAoETargeting;                          // Currently in AoE targeting mode
    private HashSet<Vector2Int> _currentAoECells;          // Cells currently highlighted for AoE preview
    private Vector2Int _lastAoEHoverPos = new Vector2Int(-1, -1); // Last hovered grid pos for AoE preview
    private Vector2Int _lastLineHoverKey = new Vector2Int(int.MinValue, int.MinValue); // Line direction hover key
    private Vector2Int _lastConeHoverKey = new Vector2Int(int.MinValue, int.MinValue); // Cone mouse tilt hover key

    // ========== SELF-CENTERED AOE CONFIRMATION STATE ==========
    private bool _isConfirmingSelfAoE;                     // Waiting for user to confirm self-centered AoE
    private HashSet<Vector2Int> _pendingSelfAoECells;      // AoE cells for preview
    private List<CharacterController> _pendingSelfAoETargets; // Targets that will be affected

    private List<SquareCell> _highlightedCells = new List<SquareCell>();
    private string _lastCombatLog = "";
    private Camera _mainCam;

    // ========== PATH PREVIEW ==========
    private PathPreview _pathPreview;

    // ========== HOVER MARKER ==========
    private HoverMarker _hoverMarker;
    private Vector2Int _lastHoverMarkerCoord = new Vector2Int(-999, -999);

    /// <summary>Current combat round number (starts at 1).</summary>
    private int _currentRound = 0;

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
        CenterCamera();
        _mainCam = Camera.main;

        // Initialize path preview for movement hover
        var previewGO = new GameObject("PathPreview");
        _pathPreview = previewGO.AddComponent<PathPreview>();

        // Initialize hover marker (X indicator on hovered square)
        var markerGO = new GameObject("HoverMarker");
        _hoverMarker = markerGO.AddComponent<HoverMarker>();

        // Initialize icon system
        IconManager.Init();

        // Check if character creation UI exists
        if (CharacterCreationUI != null)
        {
            WaitingForCharacterCreation = true;
            CharacterCreationUI.OnCreationComplete = OnCharacterCreationComplete;
            CharacterCreationUI.OnCreationComplete4 = OnCharacterCreationComplete4;
            Debug.Log("[GameManager] Waiting for character creation...");
        }
        else
        {
            // No creation UI - use default characters
            SetupCharacters();
            StartCombat();
            Debug.Log("[GameManager] Initialization complete (default characters).");
        }
    }

    // ========== HELPER: Check if a character is a PC ==========
    private bool IsPC(CharacterController c)
    {
        return PCs.Contains(c);
    }

    /// <summary>Get the 1-based PC index (1-4) for a given character, or 0 if not a PC.</summary>
    private int GetPCIndex(CharacterController c)
    {
        int idx = PCs.IndexOf(c);
        return idx >= 0 ? idx + 1 : 0;
    }

    /// <summary>
    /// Called when all characters have been created through the character creation UI.
    /// Supports both legacy 2-param callback and new 4-param array callback.
    /// </summary>
    private void OnCharacterCreationComplete(CharacterCreationData pc1Data, CharacterCreationData pc2Data)
    {
        // Legacy 2-PC callback — wrap into array with 2 entries
        WaitingForCharacterCreation = false;
        Debug.Log($"[GameManager] Character creation complete (2 PCs): {pc1Data.CharacterName}, {pc2Data.CharacterName}");
        SetupCreatedCharacters(new CharacterCreationData[] { pc1Data, pc2Data });
        UpdateAllStatsUI();
        StartCombat();
    }

    /// <summary>
    /// Called when all 4 characters have been created through the character creation UI.
    /// </summary>
    public void OnCharacterCreationComplete4(CharacterCreationData[] pcDataArray)
    {
        WaitingForCharacterCreation = false;
        Debug.Log($"[GameManager] Character creation complete ({pcDataArray.Length} PCs)");
        SetupCreatedCharacters(pcDataArray);
        UpdateAllStatsUI();
        StartCombat();
    }

    /// <summary>
    /// Set up characters from character creation data (supports 2-4 PCs).
    /// </summary>
    private void SetupCreatedCharacters(CharacterCreationData[] pcDataArray)
    {
        RaceDatabase.Init();
        ItemDatabase.Init();
        FeatDefinitions.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        // PC starting positions
        Vector2Int[] pcPositions = new Vector2Int[]
        {
            new Vector2Int(3, 6),
            new Vector2Int(3, 9),
            new Vector2Int(3, 12),
            new Vector2Int(3, 15)
        };

        // Tint colors for PCs (fallback only if no class token)
        Color[] pcColors = new Color[]
        {
            Color.white,
            new Color(0.6f, 0.7f, 1f, 1f),
            new Color(0.7f, 1f, 0.7f, 1f),
            new Color(1f, 0.8f, 0.6f, 1f)
        };

        CharacterController[] pcSlots = new CharacterController[] { PC1, PC2, PC3, PC4 };

        for (int i = 0; i < pcDataArray.Length && i < pcSlots.Length; i++)
        {
            if (pcSlots[i] == null) continue;
            CharacterCreationData data = pcDataArray[i];
            data.ComputeFinalStats();

            int armorBonus, shieldBonus, damageDice;
            GetClassDefaults(data.ClassName, out armorBonus, out shieldBonus, out damageDice);

            CharacterStats stats = new CharacterStats(
                name: data.CharacterName,
                level: 3,
                characterClass: data.ClassName,
                str: data.STR, dex: data.DEX, con: data.CON,
                wis: data.WIS, intelligence: data.INT, cha: data.CHA,
                bab: data.BAB,
                armorBonus: armorBonus,
                shieldBonus: shieldBonus,
                damageDice: damageDice,
                damageCount: 1,
                bonusDamage: 0,
                baseSpeed: data.BaseSpeed,
                atkRange: 1,
                baseHitDieHP: data.HP,
                raceName: data.RaceName
            );

            // Set alignment from character creation data
            stats.CharacterAlignment = data.ChosenAlignment;

            // Set deity and domains from character creation data
            stats.DeityId = data.ChosenDeityId ?? "";
            stats.ChosenDomains = data.ChosenDomains != null
                ? new System.Collections.Generic.List<string>(data.ChosenDomains)
                : new System.Collections.Generic.List<string>();

            // Set spontaneous casting type for clerics
            stats.SpontaneousCasting = data.SpontaneousCasting;

            // Use class-specific token sprite for grid display; fallback to generic
            Sprite pcAlive = IconLoader.GetToken(data.ClassName) ?? pcAliveFallback;
            Vector2Int startPos = (i < pcPositions.Length) ? pcPositions[i] : new Vector2Int(3, 6 + i * 3);
            pcSlots[i].Init(stats, startPos, pcAlive, pcDead);

            // Only apply tint if using the generic fallback sprite (class tokens are already colored)
            if (pcAlive == pcAliveFallback && i > 0)
            {
                SpriteRenderer sr = pcSlots[i].GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = pcColors[i];
            }

            // Inventory
            var inv = pcSlots[i].gameObject.AddComponent<InventoryComponent>();
            inv.Init(stats);
            SetupStartingEquipment(inv, data.ClassName);

            // Skills
            stats.InitializeSkills(data.ClassName, 3);
            if (data.SkillRanks != null)
            {
                foreach (var kvp in data.SkillRanks)
                {
                    for (int r = 0; r < kvp.Value; r++)
                        stats.AddSkillRank(kvp.Key);
                }
            }

            // Feats
            if (data.SelectedFeats != null && data.SelectedFeats.Count > 0)
            {
                stats.AddFeats(data.SelectedFeats);
                Debug.Log($"[GameManager] {data.CharacterName} general feats: {string.Join(", ", data.SelectedFeats)}");
            }
            if (data.BonusFeats != null && data.BonusFeats.Count > 0)
            {
                stats.AddFeats(data.BonusFeats);
                Debug.Log($"[GameManager] {data.CharacterName} bonus feats: {string.Join(", ", data.BonusFeats)}");
            }
            if (!string.IsNullOrEmpty(data.WeaponFocusChoice))
                stats.WeaponFocusChoice = data.WeaponFocusChoice;
            if (!string.IsNullOrEmpty(data.SkillFocusChoice))
                stats.SkillFocusChoice = data.SkillFocusChoice;
            FeatManager.ApplyPassiveFeats(stats);

            Debug.Log($"[GameManager] {data.CharacterName} ({data.RaceName} {data.ClassName}): " +
                      $"STR {stats.STR} DEX {stats.DEX} CON {stats.CON} " +
                      $"HP {stats.MaxHP} AC {stats.ArmorClass} Atk {CharacterStats.FormatMod(stats.AttackBonus)} " +
                      $"Feats: {stats.Feats.Count}");

            // Initialize spellcasting if applicable
            if (stats.IsSpellcaster)
            {
                SpellDatabase.Init();
                var spellComp = pcSlots[i].gameObject.AddComponent<SpellcastingComponent>();
                // Pass selected spell IDs from character creation (Wizard spellbook choices)
                if (data.SelectedSpellIds != null && data.SelectedSpellIds.Count > 0)
                    spellComp.SelectedSpellIds = new System.Collections.Generic.List<string>(data.SelectedSpellIds);
                // Pass prepared spell slot IDs from character creation (Wizard spell preparation choices)
                if (data.PreparedSpellSlotIds != null && data.PreparedSpellSlotIds.Count > 0)
                    spellComp.PreparedSpellSlotIds = new System.Collections.Generic.List<string>(data.PreparedSpellSlotIds);
                spellComp.Init(stats);
                Debug.Log($"[GameManager] {data.CharacterName}: Spellcasting initialized - {spellComp.GetSlotSummary()}");
            }

            // Initialize StatusEffectManager for duration tracking
            var statusMgr = pcSlots[i].gameObject.GetComponent<StatusEffectManager>();
            if (statusMgr == null)
                statusMgr = pcSlots[i].gameObject.AddComponent<StatusEffectManager>();
            statusMgr.Init(stats);

            // Initialize ConcentrationManager for spell concentration tracking
            var concMgr = pcSlots[i].gameObject.GetComponent<ConcentrationManager>();
            if (concMgr == null)
                concMgr = pcSlots[i].gameObject.AddComponent<ConcentrationManager>();
            concMgr.Init(stats, PCs[i]);

            // Set PC icon
            Sprite classIcon = IconManager.GetClassIcon(data.ClassName);
            if (classIcon != null && CombatUI != null)
                CombatUI.SetPCIcon(i + 1, classIcon);
        }

        // ===== NPCs (Multiple Enemies) =====
        SetupEnemyEncounter();
    }

    /// <summary>
    /// Get default armor bonus, shield bonus, and damage dice for a class.
    /// Delegates to ClassRegistry for class-specific values.
    /// </summary>
    private void GetClassDefaults(string className, out int armorBonus, out int shieldBonus, out int damageDice)
    {
        ClassRegistry.Init();
        ICharacterClass classDef = ClassRegistry.GetClass(className);
        if (classDef != null)
        {
            armorBonus = classDef.DefaultArmorBonus;
            shieldBonus = classDef.DefaultShieldBonus;
            damageDice = classDef.DefaultDamageDice;
        }
        else
        {
            armorBonus = 0; shieldBonus = 0; damageDice = 6;
        }
    }

    /// <summary>
    /// Set up starting equipment based on class (PHB starting packages).
    /// Delegates to the class definition from ClassRegistry.
    /// </summary>
    private void SetupStartingEquipment(InventoryComponent inv, string className)
    {
        ItemDatabase.Init();
        ClassRegistry.Init();
        ICharacterClass classDef = ClassRegistry.GetClass(className);
        if (classDef != null)
        {
            classDef.SetupStartingEquipment(inv);
        }
        else
        {
            Debug.LogWarning($"[GameManager] No class definition found for '{className}', skipping equipment setup.");
        }
        inv.CharacterInventory.RecalculateStats();
    }

    /// <summary>
    /// Handle input every frame - inventory toggle and cell clicks.
    /// </summary>
    private void Update()
    {
        // Skip all game input during character creation
        if (WaitingForCharacterCreation) return;

        HandleInventoryInput();
        HandleSkillsInput();
        HandleCharacterSheetInput();

        if (!IsPlayerTurn) return;
        if (CurrentSubPhase == PlayerSubPhase.Animating) return;
        if (_waitingForAoOConfirmation) return;
        if (InventoryUI != null && InventoryUI.IsOpen && !InventoryUI.IsEmbedded) return;
        if (SkillsUI != null && SkillsUI.IsOpen) return;
        if (CharacterSheetUI != null && CharacterSheetUI.IsOpen) return;

        // Update path preview during movement phase (runs every frame, not just on click)
        UpdatePathPreview();

        // Update hover X marker during movement phase
        UpdateHoverMarker();

        // Update AoE preview during AoE targeting phase (runs every frame)
        if (CurrentSubPhase == PlayerSubPhase.SelectingAoETarget)
            UpdateAoEPreview();

        // Right-click / Escape to cancel targeting in various states
        if (CurrentSubPhase == PlayerSubPhase.SelectingAoETarget
            || CurrentSubPhase == PlayerSubPhase.ConfirmingSelfAoE
            || (CurrentSubPhase == PlayerSubPhase.SelectingAttackTarget && _pendingAttackMode == PendingAttackMode.CastSpell))
        {
            bool rightClicked = false;
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(1))
                rightClicked = true;
#endif
#if ENABLE_INPUT_SYSTEM
            if (!rightClicked)
            {
                var rmouse = UnityEngine.InputSystem.Mouse.current;
                if (rmouse != null && rmouse.rightButton.wasPressedThisFrame)
                    rightClicked = true;
            }
#endif
            // Also cancel with Escape key
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape))
                rightClicked = true;
#endif
#if ENABLE_INPUT_SYSTEM
            if (!rightClicked)
            {
                var kbd = UnityEngine.InputSystem.Keyboard.current;
                if (kbd != null && kbd.escapeKey.wasPressedThisFrame)
                    rightClicked = true;
            }
#endif
            if (rightClicked)
            {
                if (CurrentSubPhase == PlayerSubPhase.ConfirmingSelfAoE)
                {
                    OnSelfAoECancelled();
                }
                else if (CurrentSubPhase == PlayerSubPhase.SelectingAoETarget && _isAoETargeting)
                {
                    CancelAoETargeting();
                }
                else if (CurrentSubPhase == PlayerSubPhase.SelectingAttackTarget && _pendingAttackMode == PendingAttackMode.CastSpell)
                {
                    CancelSpellTargeting();
                }
                return;
            }
        }

        // Left-click to confirm self-centered AoE spell
        if (CurrentSubPhase == PlayerSubPhase.ConfirmingSelfAoE)
        {
            bool leftClicked = false;
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0))
                leftClicked = true;
#endif
#if ENABLE_INPUT_SYSTEM
            if (!leftClicked)
            {
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                    leftClicked = true;
            }
#endif
            if (leftClicked)
            {
                OnSelfAoEConfirmed();
                return;
            }
            return; // Block all other input while confirming
        }

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

        // Check if pointer is over a UI element
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            Debug.Log("[Grid] Click blocked by UI element (IsPointerOverGameObject)");
            return;
        }

        Vector2 worldPoint = _mainCam.ScreenToWorldPoint(mouseScreenPos);
        RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero);

        if (hit.collider != null)
        {
            SquareCell cell = hit.collider.GetComponent<SquareCell>();
            if (cell != null)
            {
                Debug.Log($"[Grid] Raycast hit cell at ({cell.X}, {cell.Y}) Phase={CurrentPhase} Sub={CurrentSubPhase}");
                OnCellClicked(cell);
            }
        }
        else
        {
            Debug.Log("[Grid] Click detected but no cell hit by raycast");
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

        if (iPressed && InventoryUI != null && !InventoryUI.IsEmbedded)
        {
            if (InventoryUI.IsOpen)
            {
                InventoryUI.Close();
                if (IsPlayerTurn && ActivePC != null && CurrentSubPhase == PlayerSubPhase.ChoosingAction)
                    ShowActionChoices();
            }
            else if (IsPlayerTurn && ActivePC != null)
            {
                InventoryUI.Toggle(ActivePC);
            }
        }
    }

    private void HandleSkillsInput()
    {
        bool kPressed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.K))
            kPressed = true;
#endif

#if ENABLE_INPUT_SYSTEM
        if (!kPressed)
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.kKey.wasPressedThisFrame)
                kPressed = true;
        }
#endif

        if (kPressed && SkillsUI != null)
        {
            if (SkillsUI.IsOpen)
            {
                Debug.Log("[UI] K pressed - closing Skills panel");
                SkillsUI.Close();
            }
            else if (IsPlayerTurn && ActivePC != null)
            {
                Debug.Log("[UI] K pressed - opening Skills panel");
                SkillsUI.OpenForDisplay(ActivePC.Stats);
            }
        }
    }

    private void HandleCharacterSheetInput()
    {
        bool cPressed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.C))
            cPressed = true;
#endif

#if ENABLE_INPUT_SYSTEM
        if (!cPressed)
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.cKey.wasPressedThisFrame)
                cPressed = true;
        }
#endif

        if (cPressed && CharacterSheetUI != null)
        {
            if (CharacterSheetUI.IsOpen)
            {
                Debug.Log("[UI] C pressed - closing Character Sheet");
                CharacterSheetUI.Close();
                if (IsPlayerTurn && ActivePC != null && CurrentSubPhase == PlayerSubPhase.ChoosingAction)
                    ShowActionChoices();
            }
            else if (IsPlayerTurn && ActivePC != null)
            {
                Debug.Log("[UI] C pressed - opening Character Sheet");
                CharacterSheetUI.Toggle(ActivePC);
            }
        }
    }

    private void CloseInventoryIfOpen()
    {
        if (InventoryUI != null && InventoryUI.IsOpen && !InventoryUI.IsEmbedded)
            InventoryUI.Close();
        if (CharacterSheetUI != null && CharacterSheetUI.IsOpen)
            CharacterSheetUI.Close();
    }

    // ========== DEFAULT CHARACTER SETUP (Quick Start / No Creation UI) ==========

    private void SetupCharacters()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        // ==========================================
        // PC1: "Aldric" - Dwarf Fighter (Level 3)
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
            baseSpeed: 4,
            atkRange: 1,
            baseHitDieHP: 22,
            raceName: "Dwarf"
        );

        pc1Stats.CharacterAlignment = Alignment.LawfulGood;

        Debug.Log($"[GameManager] Aldric (Dwarf Fighter): STR {pc1Stats.STR} DEX {pc1Stats.DEX} CON {pc1Stats.CON} " +
                  $"WIS {pc1Stats.WIS} INT {pc1Stats.INT} CHA {pc1Stats.CHA} | " +
                  $"HP {pc1Stats.MaxHP} | Speed {pc1Stats.MoveRange} squares ({pc1Stats.SpeedInFeet} ft)");

        Vector2Int pc1Start = new Vector2Int(3, 6);
        Sprite pc1Alive = IconLoader.GetToken("Fighter") ?? pcAliveFallback;
        PC1.Init(pc1Stats, pc1Start, pc1Alive, pcDead);

        var pc1Inv = PC1.gameObject.AddComponent<InventoryComponent>();
        pc1Inv.Init(pc1Stats);
        pc1Inv.SetupAldric();

        pc1Stats.InitializeSkills("Fighter", 3);
        for (int i = 0; i < 4; i++) pc1Stats.AddSkillRank("Climb");
        for (int i = 0; i < 4; i++) pc1Stats.AddSkillRank("Intimidate");
        for (int i = 0; i < 3; i++) pc1Stats.AddSkillRank("Jump");
        for (int i = 0; i < 3; i++) pc1Stats.AddSkillRank("Swim");

        // ==========================================
        // PC2: "Lyra" - Elf Rogue (Level 3)
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
            baseSpeed: 5,
            atkRange: 1,
            baseHitDieHP: 15,
            raceName: "Elf"
        );

        pc2Stats.CharacterAlignment = Alignment.ChaoticGood;

        Debug.Log($"[GameManager] Lyra (Elf Rogue): STR {pc2Stats.STR} DEX {pc2Stats.DEX} CON {pc2Stats.CON} " +
                  $"WIS {pc2Stats.WIS} INT {pc2Stats.INT} CHA {pc2Stats.CHA} | " +
                  $"HP {pc2Stats.MaxHP} | Speed {pc2Stats.MoveRange} squares ({pc2Stats.SpeedInFeet} ft)");

        Vector2Int pc2Start = new Vector2Int(3, 9);
        Sprite pc2Alive = IconLoader.GetToken("Rogue") ?? pcAliveFallback;
        PC2.Init(pc2Stats, pc2Start, pc2Alive, pcDead);

        var pc2Inv = PC2.gameObject.AddComponent<InventoryComponent>();
        pc2Inv.Init(pc2Stats);
        pc2Inv.SetupLyra();

        pc2Stats.InitializeSkills("Rogue", 3);
        for (int i = 0; i < 6; i++) pc2Stats.AddSkillRank("Hide");
        for (int i = 0; i < 6; i++) pc2Stats.AddSkillRank("Move Silently");
        for (int i = 0; i < 6; i++) pc2Stats.AddSkillRank("Spot");
        for (int i = 0; i < 6; i++) pc2Stats.AddSkillRank("Listen");
        for (int i = 0; i < 5; i++) pc2Stats.AddSkillRank("Disable Device");
        for (int i = 0; i < 5; i++) pc2Stats.AddSkillRank("Open Lock");
        for (int i = 0; i < 5; i++) pc2Stats.AddSkillRank("Search");
        for (int i = 0; i < 4; i++) pc2Stats.AddSkillRank("Tumble");
        for (int i = 0; i < 4; i++) pc2Stats.AddSkillRank("Bluff");
        for (int i = 0; i < 4; i++) pc2Stats.AddSkillRank("Diplomacy");
        for (int i = 0; i < 4; i++) pc2Stats.AddSkillRank("Climb");
        for (int i = 0; i < 3; i++) pc2Stats.AddSkillRank("Balance");
        for (int i = 0; i < 2; i++) pc2Stats.AddSkillRank("Sleight of Hand");

        // Only tint if using generic fallback sprite
        if (pc2Alive == pcAliveFallback)
        {
            SpriteRenderer pc2SR = PC2.GetComponent<SpriteRenderer>();
            if (pc2SR != null)
                pc2SR.color = new Color(0.6f, 0.7f, 1f, 1f);
        }

        // ==========================================
        // PC3: "Kael" - Human Monk (Level 3)
        // ==========================================
        CharacterStats pc3Stats = new CharacterStats(
            name: "Kael",
            level: 3,
            characterClass: "Monk",
            str: 14, dex: 16, con: 12, wis: 15, intelligence: 10, cha: 8,
            bab: 2,
            armorBonus: 0,  // Monk: unarmored (WIS to AC)
            shieldBonus: 0,
            damageDice: 6,  // Monk unarmed 1d6 at level 3
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,   // Monk: 30 ft base + fast movement = 40 ft (8 sq) at level 3
            atkRange: 1,
            baseHitDieHP: 18,
            raceName: "Human"
        );

        pc3Stats.CharacterAlignment = Alignment.LawfulNeutral;

        Debug.Log($"[GameManager] Kael (Human Monk): STR {pc3Stats.STR} DEX {pc3Stats.DEX} CON {pc3Stats.CON} " +
                  $"WIS {pc3Stats.WIS} INT {pc3Stats.INT} CHA {pc3Stats.CHA} | " +
                  $"HP {pc3Stats.MaxHP} | Speed {pc3Stats.MoveRange} squares ({pc3Stats.SpeedInFeet} ft)");

        Vector2Int pc3Start = new Vector2Int(3, 12);
        if (PC3 != null)
        {
            Sprite pc3Alive = IconLoader.GetToken("Monk") ?? pcAliveFallback;
            PC3.Init(pc3Stats, pc3Start, pc3Alive, pcDead);

            var pc3Inv = PC3.gameObject.AddComponent<InventoryComponent>();
            pc3Inv.Init(pc3Stats);
            SetupStartingEquipment(pc3Inv, "Monk");

            pc3Stats.InitializeSkills("Monk", 3);
            for (int i = 0; i < 6; i++) pc3Stats.AddSkillRank("Tumble");
            for (int i = 0; i < 6; i++) pc3Stats.AddSkillRank("Balance");
            for (int i = 0; i < 6; i++) pc3Stats.AddSkillRank("Listen");
            for (int i = 0; i < 6; i++) pc3Stats.AddSkillRank("Spot");

            // Only tint if using generic fallback sprite
            if (pc3Alive == pcAliveFallback)
            {
                SpriteRenderer pc3SR = PC3.GetComponent<SpriteRenderer>();
                if (pc3SR != null)
                    pc3SR.color = new Color(0.7f, 1f, 0.7f, 1f);
            }
        }

        // ==========================================
        // PC4: "Grunk" - Half-Orc Barbarian (Level 3)
        // ==========================================
        CharacterStats pc4Stats = new CharacterStats(
            name: "Grunk",
            level: 3,
            characterClass: "Barbarian",
            str: 18, dex: 13, con: 16, wis: 10, intelligence: 8, cha: 6,
            bab: 3,
            armorBonus: 3,  // Hide armor
            shieldBonus: 0,
            damageDice: 12, // Greataxe 1d12
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 8,   // Barbarian fast movement: 40 ft (8 squares)
            atkRange: 1,
            baseHitDieHP: 28,
            raceName: "Half-Orc"
        );

        pc4Stats.CharacterAlignment = Alignment.ChaoticNeutral;

        Debug.Log($"[GameManager] Grunk (Half-Orc Barbarian): STR {pc4Stats.STR} DEX {pc4Stats.DEX} CON {pc4Stats.CON} " +
                  $"WIS {pc4Stats.WIS} INT {pc4Stats.INT} CHA {pc4Stats.CHA} | " +
                  $"HP {pc4Stats.MaxHP} | Speed {pc4Stats.MoveRange} squares ({pc4Stats.SpeedInFeet} ft)");

        Vector2Int pc4Start = new Vector2Int(3, 15);
        if (PC4 != null)
        {
            Sprite pc4Alive = IconLoader.GetToken("Barbarian") ?? pcAliveFallback;
            PC4.Init(pc4Stats, pc4Start, pc4Alive, pcDead);

            var pc4Inv = PC4.gameObject.AddComponent<InventoryComponent>();
            pc4Inv.Init(pc4Stats);
            SetupStartingEquipment(pc4Inv, "Barbarian");

            pc4Stats.InitializeSkills("Barbarian", 3);
            for (int i = 0; i < 6; i++) pc4Stats.AddSkillRank("Climb");
            for (int i = 0; i < 6; i++) pc4Stats.AddSkillRank("Intimidate");
            for (int i = 0; i < 6; i++) pc4Stats.AddSkillRank("Jump");
            for (int i = 0; i < 6; i++) pc4Stats.AddSkillRank("Swim");

            // Only tint if using generic fallback sprite
            if (pc4Alive == pcAliveFallback)
            {
                SpriteRenderer pc4SR = PC4.GetComponent<SpriteRenderer>();
                if (pc4SR != null)
                    pc4SR.color = new Color(1f, 0.8f, 0.6f, 1f);
            }
        }

        // ==========================================
        // NPCs: Multiple enemies from EnemyDatabase
        // ==========================================
        SetupEnemyEncounter();

        // Set PC icons
        SetupPCIcons();

        UpdateAllStatsUI();
    }

    /// <summary>Set class icons for all PCs based on their character class.</summary>
    private void SetupPCIcons()
    {
        if (CombatUI == null) return;
        for (int i = 0; i < PCs.Count; i++)
        {
            if (PCs[i] != null && PCs[i].Stats != null)
            {
                Sprite icon = IconManager.GetClassIcon(PCs[i].Stats.CharacterClass);
                if (icon != null)
                    CombatUI.SetPCIcon(i + 1, icon);
            }
        }
    }

    /// <summary>Set enemy icons for all NPCs based on their enemy type.</summary>
    private void SetupNPCIcons()
    {
        if (CombatUI == null) return;
        for (int i = 0; i < NPCs.Count && i < DefaultEncounterEnemyIds.Length; i++)
        {
            Sprite icon = IconManager.GetEnemyIcon(DefaultEncounterEnemyIds[i]);
            if (icon != null)
                CombatUI.SetNPCIcon(i, icon);
        }
    }

    // ========== ENEMY ENCOUNTER SETUP ==========

    private static readonly string[] DefaultEncounterEnemyIds = {
        "skeleton_archer",
        "orc_berserker",
        "hobgoblin_sergeant"
    };

    private static readonly Vector2Int[] DefaultEncounterPositions = {
        new Vector2Int(16, 6),
        new Vector2Int(14, 10),
        new Vector2Int(16, 14),
    };

    private void SetupEnemyEncounter()
    {
        EnemyDatabase.Init();
        ItemDatabase.Init();

        _npcAIBehaviors.Clear();

        Sprite npcAliveFallback = LoadSprite("Sprites/npc_enemy_alive");
        Sprite npcDead = LoadSprite("Sprites/npc_enemy_dead");

        for (int i = 0; i < NPCs.Count && i < DefaultEncounterEnemyIds.Length; i++)
        {
            string enemyId = DefaultEncounterEnemyIds[i];
            EnemyDefinition def = EnemyDatabase.Get(enemyId);
            if (def == null)
            {
                Debug.LogError($"[GameManager] Unknown enemy ID: {enemyId}");
                continue;
            }

            CharacterController npc = NPCs[i];
            Vector2Int pos = (i < DefaultEncounterPositions.Length)
                ? DefaultEncounterPositions[i]
                : new Vector2Int(15 + i, 10);

            // Try class-specific monster token; fallback to generic NPC sprite
            string monsterType = IconLoader.DetermineMonsterType(def.Name);
            Sprite npcAlive = null;
            if (!string.IsNullOrEmpty(monsterType))
                npcAlive = IconLoader.GetToken(monsterType);
            if (npcAlive == null)
                npcAlive = npcAliveFallback;

            InitializeNPCFromDefinition(npc, def, pos, npcAlive, npcDead);
            _npcAIBehaviors.Add(def.AIBehavior);

            // Only apply color tint if using the generic fallback sprite
            SpriteRenderer sr = npc.GetComponent<SpriteRenderer>();
            if (sr != null && npcAlive == npcAliveFallback) sr.color = def.SpriteColor;

            if (i < CombatUI.NPCPanels.Count)
            {
                var panelUI = CombatUI.NPCPanels[i];
                if (panelUI.Panel != null)
                {
                    Image panelImg = panelUI.Panel.GetComponent<UnityEngine.UI.Image>();
                    if (panelImg != null) panelImg.color = def.PanelColor;
                }
                if (panelUI.NameText != null) panelUI.NameText.color = def.NameColor;
            }

            Debug.Log($"[GameManager] Spawned NPC {i}: {def.Name} (Lv {def.Level} {def.CharacterClass}) " +
                      $"at ({pos.x},{pos.y}) — AI: {def.AIBehavior}");
        }

        // Legacy NPC field points to first enemy
        if (NPCs.Count > 0)
            NPC = NPCs[0];

        // Hide legacy single-NPC panel since we're using multi-panels
        if (CombatUI.NPCNameText != null)
            CombatUI.NPCNameText.transform.parent.gameObject.SetActive(false);

        // Set NPC icons
        SetupNPCIcons();
    }

    private void InitializeNPCFromDefinition(CharacterController npc, EnemyDefinition def,
        Vector2Int pos, Sprite alive, Sprite dead)
    {
        CharacterStats stats = new CharacterStats(
            name: def.Name,
            level: def.Level,
            characterClass: def.CharacterClass,
            str: def.STR, dex: def.DEX, con: def.CON,
            wis: def.WIS, intelligence: def.INT, cha: def.CHA,
            bab: def.BAB,
            armorBonus: def.ArmorBonus,
            shieldBonus: def.ShieldBonus,
            damageDice: def.DamageDice,
            damageCount: def.DamageCount,
            bonusDamage: def.BonusDamage,
            baseSpeed: def.BaseSpeed,
            atkRange: def.AttackRange,
            baseHitDieHP: def.BaseHitDieHP
        );

        foreach (string tag in def.CreatureTags)
            stats.CreatureTags.Add(tag);

        foreach (string featName in def.Feats)
        {
            if (!stats.HasFeat(featName))
                stats.Feats.Add(featName);
        }
        if (!string.IsNullOrEmpty(def.WeaponFocusChoice))
            stats.WeaponFocusChoice = def.WeaponFocusChoice;

        FeatManager.ApplyPassiveFeats(stats);

        npc.Init(stats, pos, alive, dead);

        InventoryComponent inv = npc.gameObject.GetComponent<InventoryComponent>();
        if (inv == null) inv = npc.gameObject.AddComponent<InventoryComponent>();
        inv.Init(stats);

        foreach (var eq in def.EquipmentIds)
        {
            ItemData item = ItemDatabase.CloneItem(eq.ItemId);
            if (item != null)
                inv.CharacterInventory.DirectEquip(item, eq.Slot);
            else
                Debug.LogWarning($"[GameManager] Item not found: {eq.ItemId} for {def.Name}");
        }

        foreach (string itemId in def.BackpackItemIds)
        {
            ItemData item = ItemDatabase.CloneItem(itemId);
            if (item != null)
                inv.CharacterInventory.AddItem(item);
        }

        inv.CharacterInventory.RecalculateStats();

        // Initialize StatusEffectManager for NPC duration tracking
        var statusMgr = npc.gameObject.GetComponent<StatusEffectManager>();
        if (statusMgr == null)
            statusMgr = npc.gameObject.AddComponent<StatusEffectManager>();
        statusMgr.Init(stats);

        // Initialize ConcentrationManager for NPC concentration tracking
        var concMgr = npc.gameObject.GetComponent<ConcentrationManager>();
        if (concMgr == null)
            concMgr = npc.gameObject.AddComponent<ConcentrationManager>();
        concMgr.Init(stats, npc);

        Debug.Log($"[GameManager] {def.Name}: HP {stats.MaxHP} AC {stats.ArmorClass} " +
                  $"Atk {CharacterStats.FormatMod(stats.AttackBonus)} Speed {stats.MoveRange}sq");
    }

    /// <summary>Helper to update all stat UI panels using 4-PC multi-NPC system.</summary>
    private void UpdateAllStatsUI()
    {
        if (CombatUI == null) return;
        CombatUI.UpdateAllStats4PC(PCs, NPCs);
    }

    /// <summary>Check if all NPCs in the encounter are dead.</summary>
    private bool AreAllNPCsDead()
    {
        foreach (var npc in NPCs)
        {
            if (npc != null && !npc.Stats.IsDead) return false;
        }
        return true;
    }

    /// <summary>Check if all PCs in the party are dead.</summary>
    private bool AreAllPCsDead()
    {
        foreach (var pc in PCs)
        {
            if (pc != null && !pc.Stats.IsDead) return false;
        }
        return true;
    }

    /// <summary>Count remaining alive NPCs.</summary>
    private int GetAliveNPCCount()
    {
        int count = 0;
        foreach (var npc in NPCs)
        {
            if (npc != null && !npc.Stats.IsDead) count++;
        }
        return count;
    }

    /// <summary>Get first alive NPC (for backward compat in single-target scenarios).</summary>
    private CharacterController GetFirstAliveNPC()
    {
        foreach (var npc in NPCs)
        {
            if (npc != null && !npc.Stats.IsDead) return npc;
        }
        return null;
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

    // ========== INITIATIVE & COMBAT START ==========

    /// <summary>
    /// Roll initiative for all combatants and start the first turn.
    /// D&D 3.5: 1d20 + initiative modifier (DEX mod + Improved Initiative feat bonus).
    /// </summary>
    public void StartCombat()
    {
        _initiativeOrder = InitiativeSystem.RollInitiative(PCs, NPCs);
        _currentInitiativeIndex = 0;

        // Log initiative order
        string orderStr = InitiativeSystem.GetInitiativeOrderString(_initiativeOrder);
        Debug.Log($"[Initiative] Combat begins! Initiative order:\n{orderStr}");

        // Update initiative display in UI
        UpdateInitiativeUI();

        // Start the first turn
        AdvanceToNextTurn();
    }

    /// <summary>Update the initiative panel in the UI.</summary>
    private void UpdateInitiativeUI()
    {
        if (CombatUI == null) return;
        string display = InitiativeSystem.GetInitiativeDisplayString(_initiativeOrder, _currentInitiativeIndex);
        CombatUI.UpdateInitiativeDisplay(display);
    }

    /// <summary>
    /// Advance to the next combatant in initiative order.
    /// Skips dead characters. Wraps around to the beginning for a new round.
    /// </summary>
    private void AdvanceToNextTurn()
    {
        if (CurrentPhase == TurnPhase.CombatOver) return;

        // Find next alive character
        int attempts = 0;
        while (attempts < _initiativeOrder.Count)
        {
            if (_currentInitiativeIndex >= _initiativeOrder.Count)
                _currentInitiativeIndex = 0; // New round

            var entry = _initiativeOrder[_currentInitiativeIndex];

            if (entry.Character != null && !entry.Character.Stats.IsDead)
            {
                _activeTurnCharacter = entry.Character;

                if (entry.IsPC)
                {
                    StartPCTurn(entry.Character);
                }
                else
                {
                    StartCoroutine(SingleNPCTurnFromInitiative(entry.Character));
                }
                return;
            }

            // This character is dead, skip
            _currentInitiativeIndex++;
            attempts++;
        }

        // All characters are dead somehow
        CurrentPhase = TurnPhase.CombatOver;
        CombatUI.SetTurnIndicator("Combat has ended.");
        CombatUI.SetActionButtonsVisible(false);
    }

    /// <summary>Move to the next initiative slot and start that turn.</summary>
    private void NextInitiativeTurn()
    {
        _currentInitiativeIndex++;
        UpdateInitiativeUI();
        // Threat map may have changed (NPC moved, character died, etc.)
        InvalidatePreviewThreats();
        AdvanceToNextTurn();
    }

    // ========== TURN MANAGEMENT WITH ACTION ECONOMY ==========

    /// <summary>
    /// Begin a PC's turn with full action economy.
    /// </summary>
    public void StartPCTurn(CharacterController pc)
    {
        if (CurrentPhase == TurnPhase.CombatOver) return;

        CloseInventoryIfOpen();

        // If this PC is dead, advance to next in initiative
        if (pc.Stats.IsDead)
        {
            NextInitiativeTurn();
            return;
        }

        // ===== NEW ROUND DETECTION =====
        // A new round begins when PC1's turn starts (turn order: PC1 → PC2 → NPC → repeat)
        if (pc == PC1)
        {
            _currentRound++;
            Debug.Log($"[GameManager] ═══ ROUND {_currentRound} BEGINS ═══");
            CombatUI.AddTurnSeparator(_currentRound);
            ResetQuickenedSpellTrackingForAllCharacters();

            // Tick all spell effect durations at the start of each new round
            TickAllSpellDurations();
        }

        // Log turn start in combat log
        CombatUI.ShowCombatLog($"<color=#FFD700>⚔ {pc.Stats.CharacterName}'s turn begins</color>");

        // Tick Barbarian Rage at start of turn
        if (pc.Stats.IsBarbarian && pc.Stats.IsRaging)
        {
            pc.Stats.TickRage();
            if (!pc.Stats.IsRaging)
            {
                CombatUI.ShowCombatLog($"😫 {pc.Stats.CharacterName}'s rage has ended! Now fatigued.");
                UpdateAllStatsUI();
            }
            else
            {
                CombatUI.ShowCombatLog($"⚡ {pc.Stats.CharacterName}: Rage - {pc.Stats.RageRoundsRemaining} rounds remaining");
            }
        }

        pc.StartNewTurn();

        CurrentPhase = TurnPhase.PCTurn;
        _activeTurnCharacter = pc;
        CurrentSubPhase = PlayerSubPhase.ChoosingAction;

        int pcIdx = GetPCIndex(pc);
        CombatUI.SetActivePC(pcIdx);
        CombatUI.SetActiveNPC(-1); // Clear NPC highlights when PC is active

        // Update initiative UI to highlight current character
        UpdateInitiativeUI();

        ShowActionChoices();
    }

    // Legacy helper
    public void StartPlayerTurn()
    {
        // Start combat from beginning if no initiative order
        if (_initiativeOrder.Count == 0)
            StartCombat();
        else
            AdvanceToNextTurn();
    }

    /// <summary>
    /// Show the action choice UI for the current PC.
    /// </summary>
    private void ShowActionChoices()
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;

        CurrentSubPhase = PlayerSubPhase.ChoosingAction;

        // Hide movement path preview and hover marker when leaving movement phase
        if (_pathPreview != null) _pathPreview.HidePath();
        if (_hoverMarker != null) _hoverMarker.Hide();

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        SquareCell current = Grid.GetCell(pc.GridPosition);
        if (current != null)
            current.SetHighlight(HighlightType.Selected);

        CombatUI.SetActionButtonsVisible(true);
        CombatUI.UpdateActionButtons(pc);
        CombatUI.UpdateFeatControls(pc);

        string pcName = pc.Stats.CharacterName;
        string actionInfo = pc.Actions.GetStatusString();

        string dwInfo = "";
        if (pc.CanDualWield())
            dwInfo = "\n" + pc.GetDualWieldDescription();

        string featInfo = "";
        if (pc.Stats.Feats.Count > 0)
            featInfo = $"\nFeats: {string.Join(", ", pc.Stats.Feats)}";

        // Show spell info for spellcasters
        string spellInfo = "";
        if (pc.Stats.IsSpellcaster)
        {
            var spellComp = pc.GetComponent<SpellcastingComponent>();
            if (spellComp != null)
                spellInfo = $"\n✦ Spells: {spellComp.GetSlotSummary()}";
        }

        CombatUI.SetTurnIndicator($"{pcName}'s Turn - Choose an action  [I] Inventory  [K] Skills\n{actionInfo}{dwInfo}{featInfo}{spellInfo}");

        if (!pc.Actions.HasAnyActionLeft)
        {
            CombatUI.SetTurnIndicator($"{pcName}'s Turn - No actions remaining");
            StartCoroutine(DelayedEndActivePCTurn(1.0f));
        }
    }

    // ========== ACTION BUTTON HANDLERS ==========

    public void OnMoveButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;

        if (pc.Actions.HasMoveAction) { /* Normal move */ }
        else if (pc.Actions.CanConvertStandardToMove) { /* Will convert */ }
        else return;

        CurrentSubPhase = PlayerSubPhase.Moving;
        ShowMovementRange(pc);
        CombatUI.SetActionButtonsVisible(false);
        CombatUI.SetTurnIndicator($"{pc.Stats.CharacterName} - Click a tile to move (or own tile to cancel)");
    }

    public void OnAttackButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || !pc.Actions.HasStandardAction) return;

        _pendingAttackMode = PendingAttackMode.Single;
        CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
        ShowAttackTargets(pc);
    }

    public void OnFullAttackButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || !pc.Actions.HasFullRoundAction) return;

        _pendingAttackMode = PendingAttackMode.FullAttack;
        CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
        ShowAttackTargets(pc);
    }

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

    public void OnEndTurnButtonPressed()
    {
        if (!IsPlayerTurn) return;
        EndActivePCTurn();
    }

    public void OnPowerAttackSliderChanged(float value)
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;
        pc.SetPowerAttack((int)value);
        CombatUI.UpdatePowerAttackLabel(pc);
    }

    public void OnRapidShotTogglePressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;
        bool oldValue = pc.RapidShotEnabled;
        pc.SetRapidShot(!pc.RapidShotEnabled);
        Debug.Log($"[RapidShot] Rapid Shot toggle clicked, new value: {pc.RapidShotEnabled} (was {oldValue}) for {pc.Stats.CharacterName}");
        CombatUI.UpdateRapidShotLabel(pc);
        CombatUI.UpdateActionButtons(pc);
    }

    public void OnFlurryOfBlowsButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || !pc.Stats.IsMonk || !pc.Actions.HasFullRoundAction) return;

        _pendingAttackMode = PendingAttackMode.FlurryOfBlows;
        CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;

        int[] bonuses = pc.Stats.GetFlurryOfBlowsBonuses();
        string bonusStr = string.Join("/", System.Array.ConvertAll(bonuses, b => CharacterStats.FormatMod(b)));
        Debug.Log($"[Monk] {pc.Stats.CharacterName}: Flurry of Blows selected - {bonuses.Length} attacks at {bonusStr}");

        ShowAttackTargets(pc);
        CombatUI.SetTurnIndicator($"FLURRY OF BLOWS: Select target ({bonusStr})");
    }

    public void OnRageButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || !pc.Stats.IsBarbarian) return;

        bool success = pc.ActivateRage();
        if (success)
        {
            CombatUI.ShowCombatLog($"⚡ {pc.Stats.CharacterName} enters a BARBARIAN RAGE! " +
                                  $"+4 STR, +4 CON, +2 Will, -2 AC for {pc.Stats.RageRoundsRemaining} rounds!");
            UpdateAllStatsUI();
            CombatUI.UpdateActionButtons(pc);
            Debug.Log($"[GameManager] {pc.Stats.CharacterName} activated Rage via button");
        }
        else
        {
            string reason = pc.Stats.IsRaging ? "already raging" :
                           pc.Stats.IsFatigued ? "fatigued" : "no rages left today";
            CombatUI.ShowCombatLog($"{pc.Stats.CharacterName} cannot rage: {reason}");
            Debug.Log($"[GameManager] {pc.Stats.CharacterName} failed to activate Rage: {reason}");
        }
    }

    // ========== SPELLCASTING ==========

    /// <summary>Called when Cast Spell button is pressed (Standard Action, spellcasters only).</summary>
    public void OnCastSpellButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || !pc.Stats.IsSpellcaster || !pc.Actions.HasStandardAction) return;

        var spellComp = pc.GetComponent<SpellcastingComponent>();
        if (spellComp == null) return;

        // Only allow casting if there are prepared spells with available slots
        if (!spellComp.HasAnyCastablePreparedSpell())
        {
            Debug.Log($"[GameManager] {pc.Stats.CharacterName}: No prepared spells with available slots to cast.");
            return;
        }

        // Show spell selection panel with metamagic support (only prepared spells shown)
        CombatUI.SetActionButtonsVisible(false);
        CombatUI.ShowSpellSelection(spellComp, OnSpellSelectedWithMetamagic, OnSpellSelectionCancelled);
    }

    /// <summary>Called when a spell is chosen from the spell selection panel (with optional metamagic).</summary>
    private void OnSpellSelectedWithMetamagic(SpellData spell, MetamagicData metamagic)
    {
        CharacterController pc = ActivePC;
        if (pc == null) { ShowActionChoices(); return; }

        _pendingSpell = spell;
        _pendingMetamagic = metamagic;

        // If metamagic modifies the spell data (range, action type), clone and apply
        if (metamagic != null && metamagic.HasAnyMetamagic)
        {
            _pendingSpell = spell.Clone();
            SpellCaster.ApplyMetamagicToSpellData(_pendingSpell, metamagic);
            Debug.Log($"[GameManager] Metamagic applied: {metamagic.GetSummary(spell.SpellLevel)}");
        }

        // ===== AoE SPELLS: Enter AoE targeting mode =====
        if (_pendingSpell.AoEShapeType != AoEShape.None)
        {
            EnterAoETargetingMode(pc, _pendingSpell);
            return;
        }

        // Determine targeting based on spell type
        if (_pendingSpell.TargetType == SpellTargetType.Self)
        {
            // Self-targeting spells (Mage Armor) cast immediately
            PerformSpellCast(pc, pc);
        }
        else if (_pendingSpell.TargetType == SpellTargetType.SingleAlly)
        {
            // Ally targeting (Cure Light Wounds) - show ally targets
            _pendingAttackMode = PendingAttackMode.CastSpell;
            CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
            ShowSpellTargets(pc, _pendingSpell);
        }
        else if (_pendingSpell.TargetType == SpellTargetType.SingleEnemy)
        {
            // Enemy targeting (damage spells) - show enemy targets in range
            _pendingAttackMode = PendingAttackMode.CastSpell;
            CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
            ShowSpellTargets(pc, _pendingSpell);
        }
        else
        {
            // Default: show targets
            _pendingAttackMode = PendingAttackMode.CastSpell;
            CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
            ShowSpellTargets(pc, _pendingSpell);
        }
    }

    /// <summary>Legacy callback for backward compat (no metamagic).</summary>
    private void OnSpellSelected(SpellData spell)
    {
        OnSpellSelectedWithMetamagic(spell, null);
    }

    /// <summary>Called when spell selection is cancelled.</summary>
    private void OnSpellSelectionCancelled()
    {
        ShowActionChoices();
    }

    /// <summary>
    /// Highlight valid targets for a spell based on its range and target type.
    /// Shows the full spell range area (purple) with valid targets highlighted (magenta).
    /// </summary>
    private void ShowSpellTargets(CharacterController caster, SpellData spell)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        int range = spell.RangeSquares;
        if (range <= 0) range = 1; // Touch spells = adjacent (1 square)

        bool casterIsPC = IsPC(caster);

        List<SquareCell> allCells = Grid.GetCellsInRange(caster.GridPosition, range);
        bool hasTarget = false;

        // First pass: highlight all cells in range with spell range color (purple)
        foreach (var cell in allCells)
        {
            int sqDist = SquareGridUtils.GetDistance(caster.GridPosition, cell.Coords);
            if (sqDist > range) continue;
            if (cell.Coords == caster.GridPosition) continue;
            cell.SetHighlight(HighlightType.SpellRange);
        }

        // Highlight caster's own position
        SquareCell casterCell = Grid.GetCell(caster.GridPosition);
        if (casterCell != null)
            casterCell.SetHighlight(HighlightType.Selected);

        // Second pass: highlight valid targets (magenta) on top of range area
        foreach (var cell in allCells)
        {
            if (!cell.IsOccupied || cell.Occupant.Stats.IsDead) continue;
            if (cell.Occupant == caster) continue;

            int sqDist = SquareGridUtils.GetDistance(caster.GridPosition, cell.Coords);
            if (sqDist > range) continue;

            bool validTarget = false;
            if (spell.TargetType == SpellTargetType.SingleEnemy)
            {
                // Enemies: NPCs are enemies to PCs, PCs are enemies to NPCs
                bool isEnemy = casterIsPC ? NPCs.Contains(cell.Occupant) : IsPC(cell.Occupant);
                validTarget = isEnemy;
            }
            else if (spell.TargetType == SpellTargetType.SingleAlly)
            {
                // Allies: other PCs are allies to PCs, other NPCs are allies to NPCs
                bool isAlly = casterIsPC ? (IsPC(cell.Occupant) && cell.Occupant != caster) :
                                           (NPCs.Contains(cell.Occupant) && cell.Occupant != caster);
                validTarget = isAlly;
            }
            else if (spell.TargetType == SpellTargetType.Touch)
            {
                // Touch can target anyone adjacent
                validTarget = true;
            }
            else if (spell.TargetType == SpellTargetType.Area)
            {
                // Area spells can target any cell in range (including empty ones)
                validTarget = true;
            }

            if (validTarget)
            {
                cell.SetHighlight(HighlightType.SpellTarget);
                _highlightedCells.Add(cell);
                hasTarget = true;
            }
        }

        // For SingleAlly spells, also allow self-targeting by clicking own tile
        if (spell.TargetType == SpellTargetType.SingleAlly)
        {
            if (casterCell != null)
            {
                casterCell.SetHighlight(HighlightType.SpellTarget);
                _highlightedCells.Add(casterCell);
                hasTarget = true;
            }
        }

        if (hasTarget)
        {
            string rangeStr = spell.RangeSquares <= 0 ? "Touch" : $"{spell.RangeSquares} sq ({spell.RangeSquares * 5} ft)";
            string targetMsg = spell.TargetType == SpellTargetType.SingleAlly
                ? "Click an ally (or self) to cast"
                : spell.TargetType == SpellTargetType.Area
                    ? "Click a target area to cast"
                    : "Click an enemy to cast";
            CombatUI.SetTurnIndicator($"✦ {spell.Name}: {targetMsg} | Range: {rangeStr} | Right-click to cancel");
        }
        else
        {
            CombatUI.SetTurnIndicator($"No valid targets for {spell.Name}! | Right-click to cancel");
            StartCoroutine(ReturnToActionChoicesAfterDelay(1.5f));
        }
    }

    /// <summary>
    /// Execute a spell cast from caster to target.
    /// </summary>
    private void PerformSpellCast(CharacterController caster, CharacterController target)
    {
        CurrentSubPhase = PlayerSubPhase.Animating;

        // Quickened spells don't consume standard action (they're free actions)
        bool isQuickened = _pendingMetamagic != null && _pendingMetamagic.Has(MetamagicFeatId.QuickenSpell);
        if (!isQuickened)
        {
            caster.Actions.UseStandardAction();
        }
        else
        {
            // Mark that this character has used their one quickened spell for this round
            var casterSpellComp = caster.GetComponent<SpellcastingComponent>();
            if (casterSpellComp != null)
            {
                casterSpellComp.MarkQuickenedSpellCast();
            }
        }

        // Get spellcasting component
        var spellComp = caster.GetComponent<SpellcastingComponent>();
        if (spellComp == null)
        {
            Debug.LogError("[GameManager] PerformSpellCast: No SpellcastingComponent!");
            ShowActionChoices();
            return;
        }

        // Check if this is a spontaneous cast (cleric converting a specific prepared spell)
        bool isSpontaneous = CombatUI != null && CombatUI.IsSpontaneousCast;
        int spontaneousLevel = isSpontaneous ? CombatUI.SpontaneousCastLevel : -1;
        string spontaneousSacrificedSpellId = isSpontaneous ? CombatUI.SpontaneousSacrificedSpellId : null;

        // Clear spontaneous casting state
        if (CombatUI != null)
            CombatUI.ClearSpontaneousCastState();

        // Consume spell slot using D&D 3.5e slot-based system
        // Cantrips (level 0) are UNLIMITED — no slot consumed
        int slotLevelToConsume = _pendingSpell.SpellLevel;
        bool hasMetamagicApplied = _pendingMetamagic != null && _pendingMetamagic.HasAnyMetamagic;

        if (hasMetamagicApplied)
        {
            slotLevelToConsume = _pendingMetamagic.GetEffectiveSpellLevel(_pendingSpell.SpellLevel);
            Debug.Log($"[GameManager] Metamagic: consuming level {slotLevelToConsume} slot " +
                      $"(base {_pendingSpell.SpellLevel} + {slotLevelToConsume - _pendingSpell.SpellLevel} metamagic)");
        }

        // Consume spell slot
        // Cantrips are unlimited — CastSpellFromSlot handles this (no slot consumed)
        // Both Wizards and Clerics use slot-based system
        {
            bool consumed;
            if (isSpontaneous)
            {
                // Spontaneous casting: consume the specific prepared spell slot
                if (!string.IsNullOrEmpty(spontaneousSacrificedSpellId))
                {
                    consumed = spellComp.SpontaneousCastFromSpecificSpell(spontaneousSacrificedSpellId);
                    if (consumed)
                    {
                        Debug.Log($"[GameManager] Spontaneous cast: {caster.Stats.CharacterName} sacrificed '{spontaneousSacrificedSpellId}' → {_pendingSpell.Name}");
                    }
                }
                else
                {
                    // Fallback: consume any slot at the spontaneous level (legacy path)
                    consumed = spellComp.SpontaneousCastFromSlot(spontaneousLevel);
                    if (consumed)
                    {
                        Debug.Log($"[GameManager] Spontaneous cast (level-based): {caster.Stats.CharacterName} converted a level {spontaneousLevel} slot → {_pendingSpell.Name}");
                    }
                }
            }
            else if (hasMetamagicApplied && slotLevelToConsume > 0)
            {
                consumed = spellComp.CastWizardSpellWithMetamagic(_pendingSpell, _pendingMetamagic);
            }
            else
            {
                consumed = spellComp.CastSpellFromSlot(_pendingSpell);
            }

            if (!consumed)
            {
                Debug.LogError($"[GameManager] Failed to consume level {slotLevelToConsume} spell slot!");
                ShowActionChoices();
                return;
            }
        }

        // Check if caster is concentrating on another spell — casting requires a concentration check
        HandleConcentrationOnCasting(caster, _pendingSpell);

        // Resolve the spell with metamagic
        SpellResult result = SpellCaster.Cast(_pendingSpell, caster.Stats, target.Stats, _pendingMetamagic);

        // Apply buff effects based on spell type
        if (result.Success && _pendingSpell.EffectType == SpellEffectType.Buff)
        {
            var appliedEffect = ApplySpellBuff(caster, target, _pendingSpell, spellComp);

            // If this is a concentration spell, begin tracking concentration on the caster
            if (appliedEffect != null && _pendingSpell.DurationType == DurationType.Concentration)
            {
                BeginConcentrationTracking(caster, appliedEffect, _pendingSpell);
            }
        }

        // Check concentration for spell damage on the target
        if (result.DamageDealt > 0 && target != null)
        {
            CheckConcentrationOnDamage(target, result.DamageDealt);
        }

        // Handle death if target was killed
        if (result.TargetKilled && target != null)
        {
            target.OnDeath();
        }

        // Build combat log with quickened spell / spontaneous cast indicators
        _lastCombatLog = result.GetFormattedLog();

        if (isSpontaneous)
        {
            string sacrificeInfo = !string.IsNullOrEmpty(spontaneousSacrificedSpellId)
                ? $"Sacrificed: {spontaneousSacrificedSpellId}"
                : "Converted prepared spell";
            string spontPrefix = $"⟳ {caster.Stats.CharacterName} spontaneously casts {_pendingSpell.Name}! ({sacrificeInfo})\n";
            _lastCombatLog = spontPrefix + _lastCombatLog;
        }

        if (isQuickened)
        {
            string quickenedPrefix = $"⚡ {caster.Stats.CharacterName} casts QUICKENED {_pendingSpell.Name}! (Free Action)\n";
            _lastCombatLog = quickenedPrefix + _lastCombatLog;
        }

        if (GameManager.LogAttacksToConsole)
            Debug.Log("[Spell] " + _lastCombatLog);

        CombatUI.ShowCombatLog(_lastCombatLog);
        UpdateAllStatsUI();

        Grid.ClearAllHighlights();

        // Check for victory (all NPCs dead) or defeat (all PCs dead)
        if (result.TargetKilled)
        {
            if (AreAllNPCsDead())
            {
                CurrentPhase = TurnPhase.CombatOver;
                CombatUI.SetTurnIndicator("VICTORY! All enemies defeated!");
                CombatUI.SetActionButtonsVisible(false);
                _pendingSpell = null;
                _pendingMetamagic = null;
                return;
            }
            else if (AreAllPCsDead())
            {
                CurrentPhase = TurnPhase.CombatOver;
                CombatUI.SetTurnIndicator("DEFEAT! All party members have fallen!");
                CombatUI.SetActionButtonsVisible(false);
                _pendingSpell = null;
                _pendingMetamagic = null;
                return;
            }
        }

        _pendingSpell = null;
        _pendingMetamagic = null;

        // After standard action, check for remaining actions
        StartCoroutine(AfterAttackDelay(caster, 1.5f));
    }

    // ========================================================================
    // AREA OF EFFECT (AoE) TARGETING AND RESOLUTION
    // ========================================================================

    /// <summary>
    /// Enter AoE targeting mode for the given spell.
    /// Shows the spell's placement range and lets the player aim the AoE.
    /// </summary>
    private void EnterAoETargetingMode(CharacterController caster, SpellData spell)
    {
        // ===== SELF-CENTERED BURST: Show preview with confirmation =====
        if (spell.AoEShapeType == AoEShape.Burst && spell.AoERangeSquares <= 0)
        {
            Debug.Log($"[AoE] Self-centered burst: {spell.Name} — showing preview at ({caster.GridPosition.x},{caster.GridPosition.y})");

            // Calculate AoE cells centered on caster
            HashSet<Vector2Int> aoeCells = AoESystem.GetBurstCells(caster.GridPosition, spell.AoESizeSquares, Grid);

            // Visual preview — highlight affected area
            Grid.ClearAllHighlights();
            foreach (Vector2Int cellPos in aoeCells)
            {
                SquareCell cell = Grid.GetCell(cellPos);
                if (cell == null) continue;

                if (cell.IsOccupied && cell.Occupant != null && !cell.Occupant.Stats.IsDead)
                {
                    bool isAlly = IsPC(caster) ? PCs.Contains(cell.Occupant) : NPCs.Contains(cell.Occupant);
                    cell.SetHighlight(isAlly ? HighlightType.AoEAlly : HighlightType.AoETarget);
                }
                else
                {
                    cell.SetHighlight(HighlightType.AoEPreview);
                }
            }

            // Get all valid targets
            bool casterIsPC = IsPC(caster);
            List<CharacterController> targets = AoESystem.GetTargetsInArea(
                aoeCells, caster, PCs, NPCs,
                spell.AoEFilter, casterIsPC, Grid);

            Debug.Log($"[AoE] Self-centered {spell.Name}: {aoeCells.Count} cells, {targets.Count} targets — awaiting confirmation");

            // Store state for confirmation
            _isConfirmingSelfAoE = true;
            _pendingSelfAoECells = aoeCells;
            _pendingSelfAoETargets = targets;
            CurrentSubPhase = PlayerSubPhase.ConfirmingSelfAoE;
            CombatUI.SetActionButtonsVisible(false);

            // Show turn indicator with confirm/cancel instructions
            CombatUI.SetTurnIndicator($"Casting {spell.Name} — Left-click to confirm, Right-click to cancel");
            return;
        }

        _isAoETargeting = true;
        _currentAoECells = null;
        _lastAoEHoverPos = new Vector2Int(-1, -1);
        _lastLineHoverKey = new Vector2Int(int.MinValue, int.MinValue);
        _lastConeHoverKey = new Vector2Int(int.MinValue, int.MinValue);
        _pendingAttackMode = PendingAttackMode.CastSpell;
        CurrentSubPhase = PlayerSubPhase.SelectingAoETarget;

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        // For burst spells with range > 0, show the valid placement range
        if (spell.AoEShapeType == AoEShape.Burst)
        {
            int range = spell.AoERangeSquares > 0 ? spell.AoERangeSquares : spell.RangeSquares;
            if (range <= 0) range = 1;

            List<SquareCell> rangeCells = Grid.GetCellsInRange(caster.GridPosition, range);
            foreach (var cell in rangeCells)
            {
                cell.SetHighlight(HighlightType.SpellRange);
            }
            // Also highlight caster's position as valid
            SquareCell casterCell = Grid.GetCell(caster.GridPosition);
            if (casterCell != null)
                casterCell.SetHighlight(HighlightType.SpellRange);

            string rangeStr = $"{range * 5} ft";
            string sizeStr = $"{spell.AoESizeSquares * 5}-ft radius burst";
            CombatUI.SetTurnIndicator($"✦ {spell.Name}: Aim {sizeStr} | Range: {rangeStr} | Move mouse to preview, click to cast | Right-click to cancel");
        }
        // For cone spells, just show the caster's position - direction is determined by mouse
        else if (spell.AoEShapeType == AoEShape.Cone)
        {
            SquareCell casterCell = Grid.GetCell(caster.GridPosition);
            if (casterCell != null)
                casterCell.SetHighlight(HighlightType.Selected);

            string sizeStr = $"{spell.AoESizeSquares * 5}-ft cone";
            CombatUI.SetTurnIndicator($"✦ {spell.Name}: Aim {sizeStr} from caster | Move mouse to aim, click to cast | Right-click to cancel");
        }
        else if (spell.AoEShapeType == AoEShape.Line)
        {
            SquareCell casterCell = Grid.GetCell(caster.GridPosition);
            if (casterCell != null)
                casterCell.SetHighlight(HighlightType.Selected);

            string sizeStr = $"{spell.AoESizeSquares * 5}-ft line";
            CombatUI.SetTurnIndicator($"✦ {spell.Name}: Aim {sizeStr} | Move mouse to aim, click to cast | Right-click to cancel");
        }

        Debug.Log($"[AoE] Entered AoE targeting mode: {spell.Name} ({spell.AoEShapeType}, {spell.AoESizeSquares} sq)");
    }

    /// <summary>
    /// Get the current mouse position in world coordinates.
    /// Used by AoE targeting for mouse-direction line spell targeting.
    /// </summary>
    private Vector2 GetMouseWorldPosition()
    {
        Vector3 mouseScreenPos = Vector3.zero;
#if ENABLE_LEGACY_INPUT_MANAGER
        mouseScreenPos = Input.mousePosition;
#endif
#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
            mouseScreenPos = (Vector3)(Vector2)mouse.position.ReadValue();
#endif
        if (_mainCam == null) return Vector2.zero;
        return _mainCam.ScreenToWorldPoint(mouseScreenPos);
    }

    /// <summary>
    /// Called every frame during AoE targeting to update the preview overlay
    /// based on the current mouse position.
    /// </summary>
    private void UpdateAoEPreview()
    {
        if (!_isAoETargeting || _pendingSpell == null) return;

        CharacterController pc = ActivePC;
        if (pc == null) return;

        // Get mouse position in world coordinates
        Vector2 worldPoint = GetMouseWorldPosition();
        if (worldPoint == Vector2.zero && _mainCam == null) return;

        HashSet<Vector2Int> aoeCells = null;

        // ===== LINE SPELLS: Mouse-direction targeting =====
        // Line extends from caster center in the direction of the mouse,
        // always to maximum spell range. Smooth, continuous aiming.
        if (_pendingSpell.AoEShapeType == AoEShape.Line)
        {
            // Quantize mouse direction to avoid excessive recalculation.
            // Encode the mouse world position at ~quarter-cell resolution.
            Vector2Int hoverKey = new Vector2Int(
                Mathf.RoundToInt(worldPoint.x * 4f),
                Mathf.RoundToInt(worldPoint.y * 4f));
            if (hoverKey == _lastLineHoverKey) return;
            _lastLineHoverKey = hoverKey;

            ClearAoEPreviewHighlights();
            aoeCells = AoESystem.GetLineCellsFromDirection(
                pc.GridPosition, worldPoint, _pendingSpell.AoESizeSquares, Grid);
        }
        else
        {
            Vector2Int gridPos = SquareGridUtils.WorldToGrid(worldPoint);

            if (_pendingSpell.AoEShapeType == AoEShape.Burst)
            {
                // Burst preview only depends on hovered grid cell center.
                if (gridPos == _lastAoEHoverPos) return;
                _lastAoEHoverPos = gridPos;

                ClearAoEPreviewHighlights();

                int range = _pendingSpell.AoERangeSquares > 0 ? _pendingSpell.AoERangeSquares : _pendingSpell.RangeSquares;
                if (!AoESystem.IsWithinCastingRange(pc.GridPosition, gridPos, range))
                    return;
                aoeCells = AoESystem.GetBurstCells(gridPos, _pendingSpell.AoESizeSquares, Grid);
            }
            else if (_pendingSpell.AoEShapeType == AoEShape.Cone)
            {
                // Cone preview depends on both target cell (direction snap) and
                // precise mouse position (cardinal first-row tilt), so track both.
                Vector2Int coneHoverKey = new Vector2Int(
                    Mathf.RoundToInt(worldPoint.x * 4f),
                    Mathf.RoundToInt(worldPoint.y * 4f));

                if (gridPos == _lastAoEHoverPos && coneHoverKey == _lastConeHoverKey) return;

                _lastAoEHoverPos = gridPos;
                _lastConeHoverKey = coneHoverKey;

                ClearAoEPreviewHighlights();
                aoeCells = AoESystem.GetConeCells(
                    pc.GridPosition,
                    gridPos,
                    _pendingSpell.AoESizeSquares,
                    Grid,
                    worldPoint);
            }
        }

        if (aoeCells == null || aoeCells.Count == 0) return;

        _currentAoECells = aoeCells;

        bool casterIsPC = IsPC(pc);

        // Highlight the AoE cells with color-coded feedback
        foreach (Vector2Int cellPos in aoeCells)
        {
            SquareCell cell = Grid.GetCell(cellPos);
            if (cell == null) continue;

            if (cell.IsOccupied && cell.Occupant != null && !cell.Occupant.Stats.IsDead)
            {
                CharacterController occupant = cell.Occupant;
                bool isAlly = casterIsPC ? PCs.Contains(occupant) : NPCs.Contains(occupant);
                bool isEnemy = casterIsPC ? NPCs.Contains(occupant) : PCs.Contains(occupant);

                if (_pendingSpell.AoEFilter == AoETargetFilter.AlliesOnly && isAlly)
                    cell.SetHighlight(HighlightType.AoEAlly);
                else if (_pendingSpell.AoEFilter == AoETargetFilter.EnemiesOnly && isEnemy)
                    cell.SetHighlight(HighlightType.AoETarget);
                else if (_pendingSpell.AoEFilter == AoETargetFilter.All)
                {
                    cell.SetHighlight(isEnemy ? HighlightType.AoETarget : HighlightType.AoEAlly);
                }
                else
                    cell.SetHighlight(HighlightType.AoEPreview);
            }
            else
            {
                cell.SetHighlight(HighlightType.AoEPreview);
            }
        }
    }

    /// <summary>
    /// Clear only the AoE preview highlights, keeping the spell range highlights intact.
    /// </summary>
    private void ClearAoEPreviewHighlights()
    {
        if (_currentAoECells == null) return;

        CharacterController pc = ActivePC;
        Vector2Int casterPos = pc != null ? pc.GridPosition : Vector2Int.zero;

        foreach (Vector2Int cellPos in _currentAoECells)
        {
            SquareCell cell = Grid.GetCell(cellPos);
            if (cell == null) continue;

            // Restore to range highlight if within burst placement range, otherwise clear
            if (_pendingSpell != null && _pendingSpell.AoEShapeType == AoEShape.Burst)
            {
                int range = _pendingSpell.AoERangeSquares > 0 ? _pendingSpell.AoERangeSquares : _pendingSpell.RangeSquares;
                int dist = SquareGridUtils.GetDistance(casterPos, cellPos);
                if (dist <= range)
                    cell.SetHighlight(HighlightType.SpellRange);
                else
                    cell.SetHighlight(HighlightType.None);
            }
            else
            {
                cell.SetHighlight(HighlightType.None);
            }
        }

        _currentAoECells = null;
    }

    /// <summary>
    /// Handle a click during AoE targeting mode.
    /// Confirms the AoE placement and resolves the spell.
    /// </summary>
    private void HandleAoETargetClick(CharacterController caster, SquareCell clickedCell)
    {
        if (_pendingSpell == null || !_isAoETargeting) return;

        Vector2Int targetPos = clickedCell.Coords;

        // Validate range for burst spells
        if (_pendingSpell.AoEShapeType == AoEShape.Burst)
        {
            int range = _pendingSpell.AoERangeSquares > 0 ? _pendingSpell.AoERangeSquares : _pendingSpell.RangeSquares;
            if (!AoESystem.IsWithinCastingRange(caster.GridPosition, targetPos, range))
            {
                Debug.Log($"[AoE] Target position ({targetPos.x},{targetPos.y}) is out of range for burst");
                return; // Don't cancel, just ignore out-of-range clicks
            }
        }

        // Calculate the final AoE cells
        HashSet<Vector2Int> aoeCells = null;
        Vector2 worldPoint = GetMouseWorldPosition();

        if (_pendingSpell.AoEShapeType == AoEShape.Burst)
        {
            aoeCells = AoESystem.GetBurstCells(targetPos, _pendingSpell.AoESizeSquares, Grid);
        }
        else if (_pendingSpell.AoEShapeType == AoEShape.Cone)
        {
            aoeCells = AoESystem.GetConeCells(
                caster.GridPosition,
                targetPos,
                _pendingSpell.AoESizeSquares,
                Grid,
                worldPoint);
        }
        else if (_pendingSpell.AoEShapeType == AoEShape.Line)
        {
            // Line spells: mouse-direction targeting — line extends from caster
            // center in direction of mouse, always to full spell range
            aoeCells = AoESystem.GetLineCellsFromDirection(
                caster.GridPosition, worldPoint, _pendingSpell.AoESizeSquares, Grid);
            Debug.Log($"[AoE] Line direction → {aoeCells.Count} cells");
        }

        if (aoeCells == null || aoeCells.Count == 0)
        {
            Debug.Log("[AoE] No cells in AoE area");
            return;
        }

        // Get all valid targets in the AoE
        bool casterIsPC = IsPC(caster);
        List<CharacterController> targets = AoESystem.GetTargetsInArea(
            aoeCells, caster, PCs, NPCs,
            _pendingSpell.AoEFilter, casterIsPC, Grid);

        Debug.Log($"[AoE] {_pendingSpell.Name}: {aoeCells.Count} cells, {targets.Count} targets");

        // Exit AoE targeting mode
        _isAoETargeting = false;
        _currentAoECells = null;
        _lastAoEHoverPos = new Vector2Int(-1, -1);
        _lastConeHoverKey = new Vector2Int(int.MinValue, int.MinValue);
        _lastLineHoverKey = new Vector2Int(int.MinValue, int.MinValue);

        // Execute the AoE spell
        PerformAoESpellCast(caster, targets, aoeCells);
    }

    /// <summary>
    /// Cancel AoE targeting and return to action choices.
    /// </summary>
    private void CancelAoETargeting()
    {
        _isAoETargeting = false;
        _currentAoECells = null;
        _lastAoEHoverPos = new Vector2Int(-1, -1);
        _lastLineHoverKey = new Vector2Int(int.MinValue, int.MinValue);
        _lastConeHoverKey = new Vector2Int(int.MinValue, int.MinValue);
        _pendingSpell = null;
        _pendingMetamagic = null;

        Grid.ClearAllHighlights();
        ShowActionChoices();
        Debug.Log("[AoE] AoE targeting cancelled");
    }

    /// <summary>
    /// Cancel single-target spell targeting and return to action choices.
    /// Called when right-click or Escape is pressed during SelectingAttackTarget
    /// while a spell is pending.
    /// </summary>
    private void CancelSpellTargeting()
    {
        _pendingSpell = null;
        _pendingMetamagic = null;
        _pendingAttackMode = PendingAttackMode.Single;

        Grid.ClearAllHighlights();
        ShowActionChoices();
        Debug.Log("[Spell] Spell targeting cancelled via right-click/Escape");
    }

    // ========== SELF-CENTERED AOE CONFIRMATION CALLBACKS ==========

    /// <summary>
    /// Called when the player confirms a self-centered AoE spell via left-click.
    /// Proceeds with the actual spell cast.
    /// </summary>
    private void OnSelfAoEConfirmed()
    {
        if (!_isConfirmingSelfAoE || _pendingSpell == null)
        {
            Debug.LogWarning("[AoE] OnSelfAoEConfirmed called but no pending self-AoE!");
            ShowActionChoices();
            return;
        }

        CharacterController caster = ActivePC;
        if (caster == null)
        {
            ClearSelfAoEState();
            ShowActionChoices();
            return;
        }

        Debug.Log($"[AoE] Self-centered {_pendingSpell.Name} CONFIRMED — casting on {_pendingSelfAoETargets.Count} targets");

        // Cache before clearing state
        var targets = _pendingSelfAoETargets;
        var cells = _pendingSelfAoECells;

        ClearSelfAoEState();

        // Now execute the spell
        PerformAoESpellCast(caster, targets, cells);
    }

    /// <summary>
    /// Called when the player cancels a self-centered AoE spell via right-click/Escape.
    /// Returns to action choices without consuming the spell slot.
    /// </summary>
    private void OnSelfAoECancelled()
    {
        Debug.Log($"[AoE] Self-centered AoE spell CANCELLED — no spell slot consumed");

        ClearSelfAoEState();

        _pendingSpell = null;
        _pendingMetamagic = null;

        Grid.ClearAllHighlights();
        ShowActionChoices();
    }

    /// <summary>Clear the self-centered AoE confirmation state.</summary>
    private void ClearSelfAoEState()
    {
        _isConfirmingSelfAoE = false;
        _pendingSelfAoECells = null;
        _pendingSelfAoETargets = null;
    }

    /// <summary>
    /// Execute an AoE spell against all valid targets in the area.
    /// Handles spell slot consumption, then resolves the spell for each target.
    /// </summary>
    private void PerformAoESpellCast(CharacterController caster, List<CharacterController> targets, HashSet<Vector2Int> aoeCells)
    {
        CurrentSubPhase = PlayerSubPhase.Animating;

        // Quickened spells don't consume standard action
        bool isQuickened = _pendingMetamagic != null && _pendingMetamagic.Has(MetamagicFeatId.QuickenSpell);
        if (!isQuickened)
        {
            caster.Actions.UseStandardAction();
        }
        else
        {
            var casterSpellComp = caster.GetComponent<SpellcastingComponent>();
            if (casterSpellComp != null)
                casterSpellComp.MarkQuickenedSpellCast();
        }

        // Get spellcasting component
        var spellComp = caster.GetComponent<SpellcastingComponent>();
        if (spellComp == null)
        {
            Debug.LogError("[GameManager] PerformAoESpellCast: No SpellcastingComponent!");
            ShowActionChoices();
            return;
        }

        // Check spontaneous casting state
        bool isSpontaneous = CombatUI != null && CombatUI.IsSpontaneousCast;
        int spontaneousLevel = isSpontaneous ? CombatUI.SpontaneousCastLevel : -1;
        string spontaneousSacrificedSpellId = isSpontaneous ? CombatUI.SpontaneousSacrificedSpellId : null;

        if (CombatUI != null)
            CombatUI.ClearSpontaneousCastState();

        // Consume spell slot (same logic as PerformSpellCast)
        int slotLevelToConsume = _pendingSpell.SpellLevel;
        bool hasMetamagicApplied = _pendingMetamagic != null && _pendingMetamagic.HasAnyMetamagic;

        if (hasMetamagicApplied)
        {
            slotLevelToConsume = _pendingMetamagic.GetEffectiveSpellLevel(_pendingSpell.SpellLevel);
        }

        {
            bool consumed;
            if (isSpontaneous)
            {
                if (!string.IsNullOrEmpty(spontaneousSacrificedSpellId))
                    consumed = spellComp.SpontaneousCastFromSpecificSpell(spontaneousSacrificedSpellId);
                else
                    consumed = spellComp.SpontaneousCastFromSlot(spontaneousLevel);
            }
            else if (hasMetamagicApplied && slotLevelToConsume > 0)
            {
                consumed = spellComp.CastWizardSpellWithMetamagic(_pendingSpell, _pendingMetamagic);
            }
            else
            {
                consumed = spellComp.CastSpellFromSlot(_pendingSpell);
            }

            if (!consumed)
            {
                Debug.LogError($"[GameManager] AoE: Failed to consume level {slotLevelToConsume} spell slot!");
                ShowActionChoices();
                return;
            }
        }

        // Check if caster is concentrating on another spell — casting requires a concentration check
        HandleConcentrationOnCasting(caster, _pendingSpell);

        // Build the combat log header
        var logBuilder = new System.Text.StringBuilder();
        string shapeStr = _pendingSpell.AoEShapeType == AoEShape.Cone ? "cone" :
                          _pendingSpell.AoEShapeType == AoEShape.Burst ? "burst" : "line";
        logBuilder.AppendLine($"═══════════════════════════════════");
        logBuilder.AppendLine($"✨ {caster.Stats.CharacterName} casts {_pendingSpell.Name}! ({_pendingSpell.AoESizeSquares * 5}-ft {shapeStr})");
        logBuilder.AppendLine($"  [{(_pendingSpell.SpellLevel == 0 ? "Cantrip" : $"Level {_pendingSpell.SpellLevel}")}] {_pendingSpell.School}");
        logBuilder.AppendLine($"  Targets: {targets.Count} creature(s) in {aoeCells.Count} squares");
        logBuilder.AppendLine();

        if (targets.Count == 0)
        {
            logBuilder.AppendLine($"  No valid targets in area!");
            logBuilder.Append($"═══════════════════════════════════");
        }
        else
        {
            // Resolve spell for each target
            int targetIndex = 0;
            foreach (CharacterController target in targets)
            {
                targetIndex++;
                logBuilder.AppendLine($"  --- Target {targetIndex}: {target.Stats.CharacterName} ---");

                // For buff spells, apply the buff
                if (_pendingSpell.EffectType == SpellEffectType.Buff)
                {
                    // Create a simple result for buff
                    var buffResult = new SpellResult();
                    buffResult.Spell = _pendingSpell;
                    buffResult.CasterName = caster.Stats.CharacterName;
                    buffResult.TargetName = target.Stats.CharacterName;
                    buffResult.Success = true;
                    buffResult.BuffApplied = true;
                    buffResult.BuffDescription = _pendingSpell.Description;

                    var appliedEffect = ApplySpellBuff(caster, target, _pendingSpell, spellComp);

                    // Track concentration for the first target of a concentration AoE buff
                    if (appliedEffect != null && _pendingSpell.DurationType == DurationType.Concentration && targetIndex == 1)
                    {
                        BeginConcentrationTracking(caster, appliedEffect, _pendingSpell);
                    }

                    logBuilder.AppendLine($"  BUFF APPLIED! {_pendingSpell.Description}");
                    Debug.Log($"[AoE] Buff applied to {target.Stats.CharacterName}");
                }
                // For damage spells, resolve with save and damage
                else if (_pendingSpell.EffectType == SpellEffectType.Damage)
                {
                    SpellResult result = SpellCaster.Cast(_pendingSpell, caster.Stats, target.Stats, _pendingMetamagic);

                    if (result.RequiredSave)
                    {
                        string saveResult = result.SaveSucceeded ? "SAVED" : "FAILED";
                        logBuilder.AppendLine($"  {result.SaveType} save DC {result.SaveDC}: d20={result.SaveRoll}+{result.SaveMod}={result.SaveTotal} - {saveResult}!");
                    }

                    if (result.DamageDealt > 0)
                    {
                        logBuilder.AppendLine($"  Damage: {result.DamageDealt} {result.DamageType}");
                        logBuilder.AppendLine($"  {target.Stats.CharacterName}: {result.TargetHPBefore} → {result.TargetHPAfter} HP");

                        // Check concentration for AoE spell damage
                        CheckConcentrationOnDamage(target, result.DamageDealt);
                    }

                    if (result.TargetKilled)
                    {
                        target.OnDeath();
                        logBuilder.AppendLine($"  💀 {target.Stats.CharacterName} has been slain!");
                    }
                }
                // For healing spells
                else if (_pendingSpell.EffectType == SpellEffectType.Healing)
                {
                    SpellResult result = SpellCaster.Cast(_pendingSpell, caster.Stats, target.Stats, _pendingMetamagic);

                    logBuilder.AppendLine($"  Healed: {result.HealingDone} HP");
                    logBuilder.AppendLine($"  {target.Stats.CharacterName}: {result.TargetHPBefore} → {result.TargetHPAfter} HP");
                }

                logBuilder.AppendLine();
            }

            logBuilder.Append($"═══════════════════════════════════");
        }

        _lastCombatLog = logBuilder.ToString();

        if (isSpontaneous)
        {
            string sacrificeInfo = !string.IsNullOrEmpty(spontaneousSacrificedSpellId)
                ? $"Sacrificed: {spontaneousSacrificedSpellId}"
                : "Converted prepared spell";
            _lastCombatLog = $"⟳ {caster.Stats.CharacterName} spontaneously casts {_pendingSpell.Name}! ({sacrificeInfo})\n" + _lastCombatLog;
        }

        if (isQuickened)
        {
            _lastCombatLog = $"⚡ {caster.Stats.CharacterName} casts QUICKENED {_pendingSpell.Name}! (Free Action)\n" + _lastCombatLog;
        }

        if (GameManager.LogAttacksToConsole)
            Debug.Log("[AoE Spell] " + _lastCombatLog);

        CombatUI.ShowCombatLog(_lastCombatLog);
        UpdateAllStatsUI();

        Grid.ClearAllHighlights();

        // Check for victory/defeat
        if (AreAllNPCsDead())
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI.SetTurnIndicator("VICTORY! All enemies defeated!");
            CombatUI.SetActionButtonsVisible(false);
            _pendingSpell = null;
            _pendingMetamagic = null;
            return;
        }
        else if (AreAllPCsDead())
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI.SetTurnIndicator("DEFEAT! All party members have fallen!");
            CombatUI.SetActionButtonsVisible(false);
            _pendingSpell = null;
            _pendingMetamagic = null;
            return;
        }

        _pendingSpell = null;
        _pendingMetamagic = null;

        StartCoroutine(AfterAttackDelay(caster, 1.5f));
    }

    /// <summary>
    /// Apply buff effects from a spell to the target character.
    /// Uses StatusEffectManager for proper duration tracking and stat modification reversal.
    /// Falls back to legacy system if StatusEffectManager is not available.
    /// </summary>
    private ActiveSpellEffect ApplySpellBuff(CharacterController caster, CharacterController target, SpellData spell, SpellcastingComponent spellComp)
    {
        // Use StatusEffectManager for tracked buff application
        var statusMgr = target.GetComponent<StatusEffectManager>();
        if (statusMgr != null)
        {
            int casterLevel = caster.Stats != null ? caster.Stats.Level : 1;
            var effect = statusMgr.AddEffect(spell, caster.Stats.CharacterName, casterLevel);

            if (effect != null)
            {
                // Also track in SpellcastingComponent's ActiveBuffs for backward compat
                var targetSpellComp = target.GetComponent<SpellcastingComponent>();
                if (targetSpellComp != null)
                {
                    targetSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;
                }

                string durStr = effect.GetDurationDisplayString();
                CombatUI?.ShowCombatLog($"<color=#88FF88>✨ {spell.Name} applied to {target.Stats.CharacterName} [{durStr}]</color>");
                Debug.Log($"[GameManager] {spell.Name} buff applied to {target.Stats.CharacterName} via StatusEffectManager: {effect.GetDetailedString()}");
            }
            else
            {
                Debug.Log($"[GameManager] {spell.Name} buff NOT applied to {target.Stats.CharacterName} (stacking rules prevented it)");
            }

            UpdateAllStatsUI();
            return effect;
        }

        // ===== LEGACY FALLBACK (no StatusEffectManager) =====
        var legacySpellComp = target.GetComponent<SpellcastingComponent>();

        if (spell.SpellId == "mage_armor")
        {
            target.Stats.SpellACBonus = spell.BuffACBonus;
            if (legacySpellComp != null)
            {
                legacySpellComp.MageArmorActive = true;
                legacySpellComp.MageArmorACBonus = spell.BuffACBonus;
            }
            else
            {
                SpellcastingComponent.ApplyMageArmor(target, spell);
            }
        }
        else if (spell.BuffAttackBonus != 0 || spell.BuffDamageBonus != 0 || spell.BuffSaveBonus != 0)
        {
            if (spell.BuffAttackBonus != 0) target.Stats.MoraleAttackBonus += spell.BuffAttackBonus;
            if (spell.BuffDamageBonus != 0) target.Stats.MoraleDamageBonus += spell.BuffDamageBonus;
            if (spell.BuffSaveBonus != 0) target.Stats.MoraleSaveBonus += spell.BuffSaveBonus;
            if (legacySpellComp != null) legacySpellComp.ApplyBuff(spell);
        }
        else if (spell.BuffDeflectionBonus > 0)
        {
            target.Stats.DeflectionBonus += spell.BuffDeflectionBonus;
            if (legacySpellComp != null) legacySpellComp.ApplyBuff(spell);
        }
        else if (spell.BuffShieldBonus > 0)
        {
            target.Stats.ShieldBonus += spell.BuffShieldBonus;
            if (legacySpellComp != null) legacySpellComp.ApplyBuff(spell);
        }
        else if (!string.IsNullOrEmpty(spell.BuffStatName) && spell.BuffStatBonus != 0)
        {
            ApplyStatBuff(target, spell.BuffStatName, spell.BuffStatBonus);
            if (legacySpellComp != null) legacySpellComp.ApplyBuff(spell);
        }
        else if (spell.BuffTempHP > 0)
        {
            target.Stats.TempHP += spell.BuffTempHP;
            if (legacySpellComp != null) legacySpellComp.ApplyBuff(spell);
        }
        else
        {
            if (legacySpellComp != null) legacySpellComp.ApplyBuff(spell);
            else if (spellComp != null) spellComp.ApplyBuff(spell);
        }

        Debug.Log($"[GameManager] {spell.Name} buff applied to {target.Stats.CharacterName} (legacy path)");
        return null; // Legacy path doesn't return tracked effects
    }

    /// <summary>
    /// Apply a stat buff to a target character (e.g., +4 STR from Bull's Strength).
    /// </summary>
    private void ApplyStatBuff(CharacterController target, string statName, int bonus)
    {
        switch (statName.ToUpper())
        {
            case "STR":
                target.Stats.STR += bonus;
                break;
            case "DEX":
                target.Stats.DEX += bonus;
                break;
            case "CON":
                target.Stats.CON += bonus;
                int hpBonus = (bonus / 2) * target.Stats.Level;
                target.Stats.CurrentHP += hpBonus;
                target.Stats.BonusMaxHP += hpBonus;
                break;
            case "INT":
                target.Stats.INT += bonus;
                break;
            case "WIS":
                target.Stats.WIS += bonus;
                break;
            case "CHA":
                target.Stats.CHA += bonus;
                break;
            default:
                Debug.Log($"[GameManager] Unknown stat buff target: {statName}");
                break;
        }
    }

    /// <summary>
    /// Tick all spell effect durations for all characters (PCs and NPCs).
    /// Called at the start of each new combat round.
    /// Removes expired effects and reverses their stat modifications.
    /// </summary>
    private void TickAllSpellDurations()
    {
        Debug.Log($"[SpellDuration] Ticking spell durations for round {_currentRound}...");

        // Tick PCs
        foreach (var pc in PCs)
        {
            if (pc == null || pc.Stats.IsDead) continue;
            TickCharacterSpellDurations(pc);
        }

        // Tick NPCs
        foreach (var npc in NPCs)
        {
            if (npc == null || npc.Stats.IsDead) continue;
            TickCharacterSpellDurations(npc);
        }

        UpdateAllStatsUI();
    }

    /// <summary>
    /// Tick spell durations for a single character.
    /// </summary>
    private void TickCharacterSpellDurations(CharacterController character)
    {
        var statusMgr = character.GetComponent<StatusEffectManager>();
        if (statusMgr == null || statusMgr.ActiveEffectCount == 0) return;

        var expired = statusMgr.TickAllEffects();

        foreach (var effect in expired)
        {
            string msg = $"⏱ {effect.Spell?.Name ?? "Unknown"} has expired on {character.Stats.CharacterName}!";
            Debug.Log($"[SpellDuration] {msg}");
            CombatUI?.ShowCombatLog($"<color=#FFAA44>{msg}</color>");
        }

        // Log remaining active effects
        if (statusMgr.ActiveEffectCount > 0)
        {
            foreach (var effect in statusMgr.ActiveEffects)
            {
                Debug.Log($"[SpellDuration] {character.Stats.CharacterName}: {effect.GetDisplayString()}");
            }
        }
    }

    // ========== CONCENTRATION MECHANICS (D&D 3.5e PHB) ==========

    /// <summary>
    /// Check if a character needs to make a concentration check after taking damage.
    /// Called after any damage is applied to a character (melee, ranged, spell, AoO).
    /// DC = 10 + damage dealt + spell level of concentration spell.
    /// </summary>
    /// <param name="character">The character who took damage.</param>
    /// <param name="damageTaken">Amount of damage dealt.</param>
    private void CheckConcentrationOnDamage(CharacterController character, int damageTaken)
    {
        if (character == null || damageTaken <= 0) return;

        var concMgr = character.GetComponent<ConcentrationManager>();
        if (concMgr == null || !concMgr.IsConcentrating) return;

        // If the character is dead, break concentration automatically
        if (character.Stats.IsDead)
        {
            string breakLog = concMgr.ForceBreakConcentration("killed");
            if (!string.IsNullOrEmpty(breakLog))
            {
                CombatUI?.ShowCombatLog($"<color=#FF6644>{breakLog}</color>");
            }
            UpdateAllStatsUI();
            return;
        }

        // Perform the concentration check
        var result = concMgr.CheckConcentrationOnDamage(damageTaken);
        if (!string.IsNullOrEmpty(result.LogMessage))
        {
            string color = result.Success ? "#88CCFF" : "#FF6644";
            CombatUI?.ShowCombatLog($"<color={color}>{result.LogMessage}</color>");
        }

        if (!result.Success)
        {
            UpdateAllStatsUI();
        }
    }

    /// <summary>
    /// Check concentration when a character casts a spell while already concentrating.
    /// If the new spell is also a concentration spell, the old one ends automatically.
    /// If the new spell is NOT a concentration spell, requires a check (DC 15 + new spell level).
    /// </summary>
    /// <param name="caster">The caster.</param>
    /// <param name="newSpell">The spell being cast.</param>
    /// <returns>True if casting can proceed, false if concentration check failed and casting should be aborted.</returns>
    private bool HandleConcentrationOnCasting(CharacterController caster, SpellData newSpell)
    {
        if (caster == null || newSpell == null) return true;

        var concMgr = caster.GetComponent<ConcentrationManager>();
        if (concMgr == null || !concMgr.IsConcentrating) return true;

        // If the new spell is a concentration spell, the old one ends automatically
        // (handled in BeginConcentration). No check needed, casting proceeds.
        if (newSpell.DurationType == DurationType.Concentration)
        {
            return true;
        }

        // Casting a non-concentration spell while concentrating requires a check
        // DC = 15 + spell level of the NEW spell
        var result = concMgr.CheckConcentrationOnCasting(newSpell.SpellLevel);
        if (!string.IsNullOrEmpty(result.LogMessage))
        {
            string color = result.Success ? "#88CCFF" : "#FF6644";
            CombatUI?.ShowCombatLog($"<color={color}>{result.LogMessage}</color>");
        }

        if (!result.Success)
        {
            UpdateAllStatsUI();
        }

        // Casting always proceeds — the check only affects the existing concentration spell
        return true;
    }

    /// <summary>
    /// After a concentration spell is successfully cast and its effect applied,
    /// begin tracking concentration for the caster.
    /// </summary>
    /// <param name="caster">The caster of the concentration spell.</param>
    /// <param name="effect">The ActiveSpellEffect that was created.</param>
    /// <param name="spell">The concentration spell that was cast.</param>
    private void BeginConcentrationTracking(CharacterController caster, ActiveSpellEffect effect, SpellData spell)
    {
        if (caster == null || effect == null || spell == null) return;
        if (spell.DurationType != DurationType.Concentration) return;

        var concMgr = caster.GetComponent<ConcentrationManager>();
        if (concMgr == null) return;

        string log = concMgr.BeginConcentration(effect);
        if (!string.IsNullOrEmpty(log))
        {
            CombatUI?.ShowCombatLog($"<color=#44AAFF>{log}</color>");
        }
    }

    /// <summary>
    /// Voluntarily end concentration for a character (free action).
    /// Called from UI "End Concentration" button.
    /// </summary>
    public void EndConcentrationVoluntarily(CharacterController character)
    {
        if (character == null) return;

        var concMgr = character.GetComponent<ConcentrationManager>();
        if (concMgr == null || !concMgr.IsConcentrating) return;

        string log = concMgr.EndConcentration();
        if (!string.IsNullOrEmpty(log))
        {
            CombatUI?.ShowCombatLog($"<color=#FFAA44>{log}</color>");
        }
        UpdateAllStatsUI();
    }

    // ========== MOVEMENT ==========

    // AoO confirmation state
    private bool _waitingForAoOConfirmation;
    private AoOPathResult _pendingAoOPath;
    private SquareCell _pendingMoveTarget;

    private void ShowMovementRange(CharacterController pc)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        List<SquareCell> moveCells = Grid.GetCellsInRange(pc.GridPosition, pc.Stats.MoveRange);
        foreach (var cell in moveCells)
        {
            if (!cell.IsOccupied)
            {
                cell.SetHighlight(HighlightType.Move);
                _highlightedCells.Add(cell);
            }
        }

        SquareCell current = Grid.GetCell(pc.GridPosition);
        if (current != null)
            current.SetHighlight(HighlightType.Selected);
    }

    // ========== PATH PREVIEW ==========

    /// <summary>
    /// Update the dotted-line path preview during the movement phase.
    /// Called every frame from Update() — detects the cell under the mouse
    /// and shows the A* path from the active PC to that cell.
    /// </summary>
    /// <summary>
    /// Cached threatened squares for path preview (rebuilt when hovered cell changes).
    /// </summary>
    private HashSet<Vector2Int> _previewThreatenedSquares;

    /// <summary>
    /// Whether the threatened squares cache needs rebuilding (e.g., after turn change).
    /// </summary>
    private bool _previewThreatsDirty = true;

    /// <summary>
    /// Mark the preview threat cache as dirty so it gets rebuilt on next hover.
    /// Call this when turn changes, characters move, or combat state changes.
    /// </summary>
    public void InvalidatePreviewThreats()
    {
        _previewThreatsDirty = true;
    }

    /// <summary>
    /// Build the set of all enemy-threatened squares for the given PC.
    /// Cached until invalidated to avoid per-frame recalculation.
    /// </summary>
    private HashSet<Vector2Int> GetPreviewThreatenedSquares(CharacterController pc)
    {
        if (!_previewThreatsDirty && _previewThreatenedSquares != null)
            return _previewThreatenedSquares;

        _previewThreatenedSquares = new HashSet<Vector2Int>();
        var allChars = GetAllCharacters();
        foreach (var character in allChars)
        {
            if (character == pc) continue;
            if (character.Stats.IsDead) continue;
            if (character.IsPlayerControlled == pc.IsPlayerControlled) continue;

            var threats = ThreatSystem.GetThreatenedSquares(character);
            _previewThreatenedSquares.UnionWith(threats);
        }

        _previewThreatsDirty = false;
        return _previewThreatenedSquares;
    }

    private void UpdatePathPreview()
    {
        // Only show preview during movement sub-phase
        if (_pathPreview == null) return;

        if (CurrentSubPhase != PlayerSubPhase.Moving || ActivePC == null)
        {
            if (_pathPreview.IsVisible) _pathPreview.HidePath();
            return;
        }

        if (_mainCam == null) return;

        // Get mouse position in world space
        Vector3 mouseScreenPos = Vector3.zero;
#if ENABLE_LEGACY_INPUT_MANAGER
        mouseScreenPos = Input.mousePosition;
#endif
#if ENABLE_INPUT_SYSTEM
        var mouseDev = UnityEngine.InputSystem.Mouse.current;
        if (mouseDev != null)
            mouseScreenPos = mouseDev.position.ReadValue();
#endif

        // Don't show preview if pointer is over UI
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            if (_pathPreview.IsVisible) _pathPreview.HidePath();
            return;
        }

        Vector2 worldPoint = _mainCam.ScreenToWorldPoint(mouseScreenPos);
        Vector2Int gridCoord = SquareGridUtils.WorldToGrid(worldPoint);

        // Skip recalculation if hovering over the same cell
        if (!_pathPreview.HasCoordChanged(gridCoord)) return;

        // Check if the hovered cell is a valid movement destination
        SquareCell hoveredCell = Grid.GetCell(gridCoord);
        if (hoveredCell == null || !_highlightedCells.Contains(hoveredCell))
        {
            _pathPreview.HidePath();
            return;
        }

        // Don't show path to the character's own cell
        CharacterController pc = ActivePC;
        if (gridCoord == pc.GridPosition)
        {
            _pathPreview.HidePath();
            return;
        }

        // Build threatened squares for AoO-aware pathfinding
        var threatenedSquares = GetPreviewThreatenedSquares(pc);

        // Use AoO-aware A* pathfinder — routes around threatened squares when possible
        var pathResult = Grid.FindPathAoOAware(pc.GridPosition, gridCoord, threatenedSquares, pc.Stats.MoveRange);

        if (pathResult.Path != null && pathResult.Path.Count > 0)
        {
            // Build per-segment threat flags for visual feedback
            var segmentThreatened = new List<bool>();
            Vector2Int prev = pc.GridPosition;
            foreach (var step in pathResult.Path)
            {
                // A segment is "dangerous" if we're leaving a threatened square
                bool leaving = threatenedSquares.Contains(prev);
                segmentThreatened.Add(leaving);
                prev = step;
            }

            _pathPreview.ShowPath(pc.GridPosition, pathResult.Path, segmentThreatened);
        }
        else
        {
            _pathPreview.HidePath();
        }
    }

    // ========== HOVER MARKER ==========

    /// <summary>
    /// Updates the X hover marker to show which grid square the mouse is over
    /// during the movement phase. Only updates when the hovered cell changes.
    /// </summary>
    private void UpdateHoverMarker()
    {
        if (_hoverMarker == null) return;

        // Only show during movement sub-phase
        if (CurrentSubPhase != PlayerSubPhase.Moving || ActivePC == null)
        {
            if (_hoverMarker.IsVisible)
            {
                _hoverMarker.Hide();
                _lastHoverMarkerCoord = new Vector2Int(-999, -999);
            }
            return;
        }

        if (_mainCam == null) return;

        // Get mouse position in world space
        Vector3 mouseScreenPos = Vector3.zero;
#if ENABLE_LEGACY_INPUT_MANAGER
        mouseScreenPos = Input.mousePosition;
#endif
#if ENABLE_INPUT_SYSTEM
        var mouseDev = UnityEngine.InputSystem.Mouse.current;
        if (mouseDev != null)
            mouseScreenPos = mouseDev.position.ReadValue();
#endif

        // Hide if pointer is over UI
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            if (_hoverMarker.IsVisible)
            {
                _hoverMarker.Hide();
                _lastHoverMarkerCoord = new Vector2Int(-999, -999);
            }
            return;
        }

        Vector2 worldPoint = _mainCam.ScreenToWorldPoint(mouseScreenPos);
        Vector2Int gridCoord = SquareGridUtils.WorldToGrid(worldPoint);

        // Skip if same cell as last frame
        if (gridCoord == _lastHoverMarkerCoord) return;
        _lastHoverMarkerCoord = gridCoord;

        // Check if the hovered cell is a valid grid cell
        SquareCell hoveredCell = Grid.GetCell(gridCoord);
        if (hoveredCell == null)
        {
            _hoverMarker.Hide();
            return;
        }

        // Determine color: white for valid movement destinations, red-ish for invalid
        bool isValidDestination = _highlightedCells.Contains(hoveredCell)
                                  && gridCoord != ActivePC.GridPosition;
        Color markerColor = isValidDestination
            ? Color.white
            : new Color(1f, 0.3f, 0.3f, 0.6f);

        _hoverMarker.ShowAt(hoveredCell.transform.position, markerColor);
    }

    /// <summary>
    /// Get all characters in combat for AoO threat calculations.
    /// </summary>
    private List<CharacterController> GetAllCharacters()
    {
        var all = new List<CharacterController>();
        foreach (var pc in PCs)
        {
            if (pc != null) all.Add(pc);
        }
        foreach (var npc in NPCs)
        {
            if (npc != null) all.Add(npc);
        }
        return all;
    }

    // ========== ATTACK TARGET SELECTION ==========

    private void ShowAttackTargets(CharacterController pc)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        // Get all ally PCs for flanking calculation (all alive PCs except the attacker)
        List<CharacterController> allyPCs = new List<CharacterController>();
        foreach (var ally in PCs)
        {
            if (ally != null && ally != pc && !ally.Stats.IsDead)
                allyPCs.Add(ally);
        }

        // Determine the equipped weapon's range increment
        ItemData weapon = pc.GetEquippedMainWeapon();
        int rangeIncrement = (weapon != null) ? weapon.RangeIncrement : 0;
        bool isThrownWeapon = (weapon != null) && weapon.IsThrown;
        bool isRangedWeapon = (weapon != null && weapon.WeaponCat == WeaponCategory.Ranged) || rangeIncrement > 0;

        int maxRangeSquares;
        if (isRangedWeapon && rangeIncrement > 0)
            maxRangeSquares = RangeCalculator.GetMaxRangeSquares(rangeIncrement, isThrownWeapon);
        else
            maxRangeSquares = pc.Stats.AttackRange;

        if (isRangedWeapon && rangeIncrement > 0)
            ShowRangeZoneHighlights(pc, rangeIncrement, maxRangeSquares, isThrownWeapon);

        List<SquareCell> allCells = Grid.GetCellsInRange(pc.GridPosition, maxRangeSquares);
        bool hasTarget = false;
        bool anyFlanking = false;

        foreach (var cell in allCells)
        {
            if (cell.IsOccupied && cell.Occupant != pc && !cell.Occupant.Stats.IsDead)
            {
                int sqDist = SquareGridUtils.GetDistance(pc.GridPosition, cell.Coords);

                if (isRangedWeapon && rangeIncrement > 0)
                {
                    int distFeet = RangeCalculator.SquaresToFeet(sqDist);
                    if (!RangeCalculator.IsWithinMaxRange(distFeet, rangeIncrement, isThrownWeapon))
                        continue;
                }
                else
                {
                    if (sqDist > pc.Stats.AttackRange)
                        continue;
                }

                // Check flanking with any ally
                bool flanking = false;
                foreach (var ally in allyPCs)
                {
                    if (CombatUtils.IsFlanking(pc.GridPosition, ally.GridPosition, cell.Occupant.GridPosition))
                    {
                        flanking = true;
                        break;
                    }
                }

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
                case PendingAttackMode.FlurryOfBlows: modeStr = "FLURRY OF BLOWS"; break;
                case PendingAttackMode.CastSpell: modeStr = "CAST SPELL"; break;
            }

            string rangeMsg = "";
            if (isRangedWeapon && rangeIncrement > 0)
            {
                int incSquares = RangeCalculator.GetRangeIncrementSquares(rangeIncrement);
                int maxRange = RangeCalculator.GetMaxRangeFeet(rangeIncrement, isThrownWeapon);
                rangeMsg = $"\n{weapon.Name}: {rangeIncrement} ft increment ({incSquares} sq), max {maxRange} ft";
            }

            if (CombatUI.TurnIndicatorText != null && !CombatUI.TurnIndicatorText.text.Contains("DUAL WIELD"))
                CombatUI.SetTurnIndicator($"{modeStr}: Click an enemy to attack!{flankMsg}{rangeMsg}");
        }
        else
        {
            string noRangeMsg = isRangedWeapon ? "No enemies within maximum range!" : "No enemies in range!";
            CombatUI.SetTurnIndicator(noRangeMsg);
            StartCoroutine(ReturnToActionChoicesAfterDelay(1.5f));
        }
    }

    private void ShowRangeZoneHighlights(CharacterController pc, int rangeIncrement, int maxRangeSquares, bool isThrownWeapon = false)
    {
        List<SquareCell> allCells = Grid.GetCellsInRange(pc.GridPosition, maxRangeSquares);
        foreach (var cell in allCells)
        {
            if (cell.Coords == pc.GridPosition) continue;

            int sqDist = SquareGridUtils.GetDistance(pc.GridPosition, cell.Coords);
            int zone = RangeCalculator.GetRangeZone(sqDist, rangeIncrement, isThrownWeapon);

            switch (zone)
            {
                case 1: cell.SetHighlight(HighlightType.RangeClose); break;
                case 2: cell.SetHighlight(HighlightType.RangeMedium); break;
                case 3: cell.SetHighlight(HighlightType.RangeFar); break;
            }
        }
    }

    private IEnumerator ReturnToActionChoicesAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (IsPlayerTurn)
            ShowActionChoices();
    }

    // ========== CELL CLICK HANDLING ==========

    public void OnCellClicked(SquareCell cell)
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

            case PlayerSubPhase.SelectingAoETarget:
                HandleAoETargetClick(pc, cell);
                break;

            case PlayerSubPhase.ChoosingAction:
                break;
        }
    }

    private void HandleMovementClick(CharacterController pc, SquareCell cell)
    {
        if (_waitingForAoOConfirmation) return;

        if (cell.Coords == pc.GridPosition)
        {
            ShowActionChoices();
            return;
        }

        if (_highlightedCells.Contains(cell) && !cell.IsOccupied)
        {
            var allCharacters = GetAllCharacters();
            var pathResult = Grid.FindSafePath(pc.GridPosition, cell.Coords, pc, allCharacters);

            if (pathResult.ProvokesAoOs)
            {
                Debug.Log($"[GameManager] Movement to ({cell.Coords.x},{cell.Coords.y}) would provoke {pathResult.ProvokedAoOs.Count} AoO(s)!");
                _pendingAoOPath = pathResult;
                _pendingMoveTarget = cell;
                _waitingForAoOConfirmation = true;

                var enemyNames = pathResult.GetThreateningEnemyNames();
                CombatUI.ShowAoOWarning(enemyNames, OnAoOConfirmed, OnAoOCancelled);
            }
            else
            {
                ExecuteMovement(pc, cell);
            }
        }
    }

    private void OnAoOConfirmed()
    {
        _waitingForAoOConfirmation = false;

        CharacterController pc = ActivePC;
        if (pc == null || _pendingMoveTarget == null)
        {
            ShowActionChoices();
            return;
        }

        Debug.Log($"[GameManager] Player confirmed movement - resolving AoOs");

        if (_pendingAoOPath != null && _pendingAoOPath.ProvokesAoOs)
        {
            StartCoroutine(ResolveAoOsAndMove(pc, _pendingMoveTarget, _pendingAoOPath));
        }
        else
        {
            ExecuteMovement(pc, _pendingMoveTarget);
        }

        _pendingAoOPath = null;
        _pendingMoveTarget = null;
    }

    private void OnAoOCancelled()
    {
        _waitingForAoOConfirmation = false;
        _pendingAoOPath = null;
        _pendingMoveTarget = null;

        Debug.Log("[GameManager] Player cancelled movement to avoid AoOs");

        CharacterController pc = ActivePC;
        if (pc != null)
        {
            CurrentSubPhase = PlayerSubPhase.Moving;
            ShowMovementRange(pc);
            CombatUI.SetActionButtonsVisible(false);
            CombatUI.SetTurnIndicator($"{pc.Stats.CharacterName} - Click a tile to move (or own tile to cancel)");
        }
    }

    private IEnumerator ResolveAoOsAndMove(CharacterController pc, SquareCell targetCell, AoOPathResult pathResult)
    {
        CurrentSubPhase = PlayerSubPhase.Animating;

        foreach (var aooInfo in pathResult.ProvokedAoOs)
        {
            if (pc.Stats.IsDead) break;

            var threatener = aooInfo.Threatener;
            if (threatener.Stats.IsDead) continue;

            CombatResult aooResult = ThreatSystem.ExecuteAoO(threatener, pc);
            if (aooResult != null)
            {
                string aooLog = $"⚔ AoO: {aooResult.GetDetailedSummary()}";
                CombatUI.ShowCombatLog(aooLog);
                UpdateAllStatsUI();

                if (LogAttacksToConsole)
                    Debug.Log("[Combat] " + aooLog);

                // Check concentration for AoO damage
                if (aooResult.Hit && aooResult.TotalDamage > 0)
                {
                    CheckConcentrationOnDamage(pc, aooResult.TotalDamage);
                }

                yield return new WaitForSeconds(1.0f);
            }
        }

        if (!pc.Stats.IsDead)
        {
            ExecuteMovement(pc, targetCell);
        }
        else
        {
            Debug.Log($"[GameManager] {pc.Stats.CharacterName} was slain by AoO during movement!");
            CombatUI.ShowCombatLog($"{pc.Stats.CharacterName} was slain during movement!");
            UpdateAllStatsUI();

            if (AreAllPCsDead())
            {
                CurrentPhase = TurnPhase.CombatOver;
                CombatUI.SetTurnIndicator("DEFEAT! All heroes have fallen!");
                CombatUI.SetActionButtonsVisible(false);
                yield break;
            }

            yield return new WaitForSeconds(1.0f);
            EndActivePCTurn();
        }
    }

    private void ExecuteMovement(CharacterController pc, SquareCell cell)
    {
        // Hide path preview and hover marker immediately when movement begins
        if (_pathPreview != null) _pathPreview.HidePath();
        if (_hoverMarker != null) _hoverMarker.Hide();

        if (pc.Actions.HasMoveAction)
        {
            pc.Actions.UseMoveAction();
        }
        else if (pc.Actions.CanConvertStandardToMove)
        {
            pc.Actions.ConvertStandardToMove();
        }

        pc.MoveToCell(cell);
        UpdateAllStatsUI();

        // Invalidate threat cache since positions changed
        InvalidatePreviewThreats();

        ShowActionChoices();
    }

    private void HandleAttackTargetClick(CharacterController pc, SquareCell cell)
    {
        // ===== SPELL CASTING MODE =====
        if (_pendingAttackMode == PendingAttackMode.CastSpell && _pendingSpell != null)
        {
            // For ally spells, clicking own tile = self-target
            if (cell.Coords == pc.GridPosition && _pendingSpell.TargetType == SpellTargetType.SingleAlly)
            {
                PerformSpellCast(pc, pc);
                return;
            }

            // Cancel if clicking non-highlighted cell
            if (!_highlightedCells.Contains(cell))
            {
                _pendingSpell = null;
                _pendingMetamagic = null;
                ShowActionChoices();
                return;
            }

            // Valid target click
            if (cell.IsOccupied && !cell.Occupant.Stats.IsDead)
            {
                PerformSpellCast(pc, cell.Occupant);
                return;
            }

            // Fallback cancel
            _pendingSpell = null;
            _pendingMetamagic = null;
            ShowActionChoices();
            return;
        }

        // ===== NORMAL ATTACK MODE =====
        if (!cell.IsOccupied || cell.Occupant == pc || cell.Occupant.Stats.IsDead)
        {
            if (cell.Coords == pc.GridPosition || !_highlightedCells.Contains(cell))
            {
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

        // Check for flanking with ALL allies (not just one specific PC)
        var allies = new List<CharacterController>();
        foreach (var pc in PCs)
        {
            if (pc != null && pc != attacker && !pc.Stats.IsDead)
                allies.Add(pc);
        }

        CharacterController flankPartner;
        bool isFlanking = CombatUtils.IsAttackerFlanking(attacker, target, allies, out flankPartner);
        int flankBonus = isFlanking ? CombatUtils.FlankingAttackBonus : 0;
        string partnerName = flankPartner != null ? flankPartner.Stats.CharacterName : "";

        RangeInfo rangeInfo = CalculateRangeInfo(attacker, target);

        switch (_pendingAttackMode)
        {
            case PendingAttackMode.Single:
                PerformSingleAttack(attacker, target, isFlanking, flankBonus, partnerName, rangeInfo);
                break;
            case PendingAttackMode.FullAttack:
                PerformFullAttack(attacker, target, isFlanking, flankBonus, partnerName, rangeInfo);
                break;
            case PendingAttackMode.DualWield:
                PerformDualWieldAttack(attacker, target, isFlanking, flankBonus, partnerName, rangeInfo);
                break;
            case PendingAttackMode.FlurryOfBlows:
                PerformFlurryOfBlows(attacker, target, isFlanking, flankBonus, partnerName, rangeInfo);
                break;
        }
    }

    private RangeInfo CalculateRangeInfo(CharacterController attacker, CharacterController target)
    {
        ItemData weapon = attacker.GetEquippedMainWeapon();
        int rangeIncrement = (weapon != null) ? weapon.RangeIncrement : 0;
        bool isThrownWeapon = (weapon != null) && weapon.IsThrown;
        int sqDist = SquareGridUtils.GetDistance(attacker.GridPosition, target.GridPosition);
        return RangeCalculator.GetRangeInfo(sqDist, rangeIncrement, isThrownWeapon);
    }

    private void PerformSingleAttack(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo = null)
    {
        attacker.Actions.UseStandardAction();

        CombatResult result = attacker.Attack(target, isFlanking, flankBonus, partnerName, rangeInfo);
        _lastCombatLog = result.GetDetailedSummary();

        if (LogAttacksToConsole)
            Debug.Log("[Combat] " + _lastCombatLog);

        CombatUI.ShowCombatLog(_lastCombatLog);
        UpdateAllStatsUI();
        Grid.ClearAllHighlights();

        // Check concentration for the target if they took damage
        if (result.Hit && result.TotalDamage > 0)
        {
            CheckConcentrationOnDamage(target, result.TotalDamage);
        }

        if (result.TargetKilled && !target.IsPlayerControlled)
        {
            UpdateAllStatsUI();
            if (AreAllNPCsDead())
            {
                CurrentPhase = TurnPhase.CombatOver;
                CombatUI.SetTurnIndicator("VICTORY! All enemies defeated!");
                CombatUI.SetActionButtonsVisible(false);
                return;
            }
            else
            {
                CombatUI.ShowCombatLog(_lastCombatLog + $"\n⚔️ {target.Stats.CharacterName} is slain! {GetAliveNPCCount()} enemies remain.");
            }
        }

        StartCoroutine(AfterAttackDelay(attacker, 1.5f));
    }

    private void PerformFullAttack(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo = null)
    {
        attacker.Actions.UseFullRoundAction();

        FullAttackResult result = attacker.FullAttack(target, isFlanking, flankBonus, partnerName, rangeInfo);
        _lastCombatLog = result.GetFullSummary();

        if (LogAttacksToConsole)
            LogFullAttackToConsole(result);

        CombatUI.ShowCombatLog(_lastCombatLog);
        UpdateAllStatsUI();
        Grid.ClearAllHighlights();

        // Check concentration for total damage from full attack
        if (result.TotalDamageDealt > 0)
        {
            CheckConcentrationOnDamage(target, result.TotalDamageDealt);
        }

        if (result.TargetKilled && !target.IsPlayerControlled)
        {
            UpdateAllStatsUI();
            if (AreAllNPCsDead())
            {
                CurrentPhase = TurnPhase.CombatOver;
                CombatUI.SetTurnIndicator("VICTORY! All enemies defeated!");
                CombatUI.SetActionButtonsVisible(false);
                return;
            }
            else
            {
                CombatUI.ShowCombatLog(_lastCombatLog + $"\n⚔️ {target.Stats.CharacterName} is slain! {GetAliveNPCCount()} enemies remain.");
            }
        }

        StartCoroutine(DelayedEndActivePCTurn(2.0f));
    }

    private void PerformDualWieldAttack(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo = null)
    {
        attacker.Actions.UseFullRoundAction();

        FullAttackResult result = attacker.DualWieldAttack(target, isFlanking, flankBonus, partnerName, rangeInfo);
        _lastCombatLog = result.GetFullSummary();

        if (LogAttacksToConsole)
            LogFullAttackToConsole(result);

        CombatUI.ShowCombatLog(_lastCombatLog);
        UpdateAllStatsUI();
        Grid.ClearAllHighlights();

        // Check concentration for total damage from dual-wield attack
        if (result.TotalDamageDealt > 0)
        {
            CheckConcentrationOnDamage(target, result.TotalDamageDealt);
        }

        if (result.TargetKilled && !target.IsPlayerControlled)
        {
            UpdateAllStatsUI();
            if (AreAllNPCsDead())
            {
                CurrentPhase = TurnPhase.CombatOver;
                CombatUI.SetTurnIndicator("VICTORY! All enemies defeated!");
                CombatUI.SetActionButtonsVisible(false);
                return;
            }
            else
            {
                CombatUI.ShowCombatLog(_lastCombatLog + $"\n⚔️ {target.Stats.CharacterName} is slain! {GetAliveNPCCount()} enemies remain.");
            }
        }

        StartCoroutine(DelayedEndActivePCTurn(2.0f));
    }

    private void PerformFlurryOfBlows(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo = null)
    {
        attacker.Actions.UseFullRoundAction();

        FullAttackResult result = attacker.FlurryOfBlows(target, isFlanking, flankBonus, partnerName, rangeInfo);
        _lastCombatLog = result.GetFullSummary();

        if (LogAttacksToConsole)
            LogFullAttackToConsole(result);

        CombatUI.ShowCombatLog(_lastCombatLog);
        UpdateAllStatsUI();
        Grid.ClearAllHighlights();

        // Check concentration for total damage from flurry of blows
        if (result.TotalDamageDealt > 0)
        {
            CheckConcentrationOnDamage(target, result.TotalDamageDealt);
        }

        if (result.TargetKilled && !target.IsPlayerControlled)
        {
            UpdateAllStatsUI();
            if (AreAllNPCsDead())
            {
                CurrentPhase = TurnPhase.CombatOver;
                CombatUI.SetTurnIndicator("VICTORY! All enemies defeated!");
                CombatUI.SetActionButtonsVisible(false);
                return;
            }
            else
            {
                CombatUI.ShowCombatLog(_lastCombatLog + $"\n⚔️ {target.Stats.CharacterName} is slain! {GetAliveNPCCount()} enemies remain.");
            }
        }

        StartCoroutine(DelayedEndActivePCTurn(2.0f));
    }

    private IEnumerator AfterAttackDelay(CharacterController pc, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (CurrentPhase == TurnPhase.CombatOver) yield break;

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

    /// <summary>
    /// End the current PC's turn and advance to the next combatant in initiative order.
    /// </summary>
    private void EndActivePCTurn()
    {
        CharacterController pc = ActivePC;
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        if (CurrentPhase == TurnPhase.CombatOver) return;

        // Advance to next in initiative order
        NextInitiativeTurn();
    }

    private IEnumerator DelayedEndActivePCTurn(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (CurrentPhase != TurnPhase.CombatOver)
            EndActivePCTurn();
    }

    // ========== NPC AI TURN (Initiative-Based) ==========

    /// <summary>
    /// Execute a single NPC turn triggered by the initiative system.
    /// </summary>
    private IEnumerator SingleNPCTurnFromInitiative(CharacterController npc)
    {
        CurrentPhase = TurnPhase.NPCTurn;
        _activeTurnCharacter = npc;
        CombatUI.SetActivePC(0); // No PC active
        CombatUI.SetActiveNPC(NPCs.IndexOf(npc)); // Highlight active NPC
        CombatUI.SetActionButtonsVisible(false);

        // Update initiative UI to highlight current NPC
        UpdateInitiativeUI();

        if (npc.Stats.IsDead)
        {
            CombatUI.SetActiveNPC(-1); // Clear NPC highlight
            NextInitiativeTurn();
            yield break;
        }

        // Determine AI behavior for this NPC
        int npcIdx = NPCs.IndexOf(npc);
        EnemyAIBehavior behavior = (npcIdx >= 0 && npcIdx < _npcAIBehaviors.Count)
            ? _npcAIBehaviors[npcIdx] : EnemyAIBehavior.AggressiveMelee;

        yield return StartCoroutine(SingleNPCTurn(npc, behavior));

        // Check if all PCs are dead after NPC turn
        if (AreAllPCsDead())
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI.SetTurnIndicator("DEFEAT! All heroes have fallen!");
            CombatUI.SetActionButtonsVisible(false);
            yield break;
        }

        // Advance to next in initiative
        NextInitiativeTurn();
    }

    /// <summary>
    /// Execute a single NPC's turn with behavior-specific AI logic.
    /// </summary>
    private IEnumerator SingleNPCTurn(CharacterController npc, EnemyAIBehavior behavior)
    {
        npc.StartNewTurn();
        CombatUI.SetTurnIndicator($"{npc.Stats.CharacterName}'s turn...");
        CombatUI.ShowCombatLog($"<color=#FF6666>💀 {npc.Stats.CharacterName}'s turn begins</color>");
        yield return new WaitForSeconds(0.6f);

        CharacterController targetPC = GetClosestAlivePCTo(npc);
        if (targetPC == null) yield break;

        switch (behavior)
        {
            case EnemyAIBehavior.AggressiveMelee:
                yield return StartCoroutine(AI_AggressiveMelee(npc, targetPC));
                break;
            case EnemyAIBehavior.RangedKiter:
                yield return StartCoroutine(AI_RangedKiter(npc));
                break;
            case EnemyAIBehavior.DefensiveMelee:
                yield return StartCoroutine(AI_DefensiveMelee(npc, targetPC));
                break;
            default:
                yield return StartCoroutine(AI_AggressiveMelee(npc, targetPC));
                break;
        }
    }

    private IEnumerator AI_AggressiveMelee(CharacterController npc, CharacterController targetPC)
    {
        int distToTarget = SquareGridUtils.GetDistance(npc.GridPosition, targetPC.GridPosition);

        if (distToTarget > npc.Stats.AttackRange)
        {
            SquareCell bestCell = FindBestMoveToward(npc, targetPC.GridPosition);
            if (bestCell != null)
            {
                npc.MoveToCell(bestCell);
                npc.Actions.UseMoveAction();
                CombatUI.ShowCombatLog($"{npc.Stats.CharacterName} charges toward {targetPC.Stats.CharacterName}!");
                yield return new WaitForSeconds(0.5f);
            }
        }

        targetPC = GetClosestAlivePCTo(npc);
        if (targetPC == null) yield break;

        distToTarget = SquareGridUtils.GetDistance(npc.GridPosition, targetPC.GridPosition);

        if (distToTarget <= npc.Stats.AttackRange && !targetPC.Stats.IsDead)
        {
            yield return StartCoroutine(NPCPerformAttack(npc, targetPC));
        }
        else
        {
            yield return new WaitForSeconds(0.3f);
        }
    }

    private IEnumerator AI_RangedKiter(CharacterController npc)
    {
        CharacterController closestPC = GetClosestAlivePCTo(npc);
        if (closestPC == null) yield break;

        int distToClosestPC = SquareGridUtils.GetDistance(npc.GridPosition, closestPC.GridPosition);

        if (distToClosestPC <= 2)
        {
            SquareCell retreatCell = FindBestMoveAwayFrom(npc, closestPC.GridPosition);
            if (retreatCell != null)
            {
                npc.MoveToCell(retreatCell);
                npc.Actions.UseMoveAction();
                CombatUI.ShowCombatLog($"{npc.Stats.CharacterName} retreats to maintain distance!");
                yield return new WaitForSeconds(0.5f);
            }
        }

        CharacterController rangedTarget = GetClosestAlivePCTo(npc);
        if (rangedTarget == null) yield break;

        ItemData weapon = npc.GetEquippedMainWeapon();
        int maxRange = 1;
        if (weapon != null && (weapon.WeaponCat == WeaponCategory.Ranged || weapon.RangeIncrement > 0))
        {
            bool isThrown = weapon.IsThrown;
            maxRange = RangeCalculator.GetMaxRangeSquares(weapon.RangeIncrement, isThrown);
        }

        int distToRangedTarget = SquareGridUtils.GetDistance(npc.GridPosition, rangedTarget.GridPosition);

        if (distToRangedTarget <= maxRange && !rangedTarget.Stats.IsDead)
        {
            yield return StartCoroutine(NPCPerformAttack(npc, rangedTarget));
        }
        else if (distToRangedTarget > maxRange && npc.Actions.HasMoveAction)
        {
            SquareCell approachCell = FindBestMoveToward(npc, rangedTarget.GridPosition);
            if (approachCell != null)
            {
                npc.MoveToCell(approachCell);
                npc.Actions.UseMoveAction();
                CombatUI.ShowCombatLog($"{npc.Stats.CharacterName} moves to get a better shot.");
                yield return new WaitForSeconds(0.5f);
            }
        }
        else
        {
            yield return new WaitForSeconds(0.3f);
        }
    }

    private IEnumerator AI_DefensiveMelee(CharacterController npc, CharacterController targetPC)
    {
        CharacterController weakerPC = GetWeakerAlivePC();
        if (weakerPC != null) targetPC = weakerPC;

        int distToTarget = SquareGridUtils.GetDistance(npc.GridPosition, targetPC.GridPosition);

        if (distToTarget > npc.Stats.AttackRange)
        {
            SquareCell bestCell = FindBestMoveToward(npc, targetPC.GridPosition);
            if (bestCell != null)
            {
                npc.MoveToCell(bestCell);
                npc.Actions.UseMoveAction();
                CombatUI.ShowCombatLog($"{npc.Stats.CharacterName} advances methodically toward {targetPC.Stats.CharacterName}.");
                yield return new WaitForSeconds(0.5f);
            }
        }

        targetPC = GetClosestAlivePCTo(npc);
        if (targetPC == null) yield break;

        distToTarget = SquareGridUtils.GetDistance(npc.GridPosition, targetPC.GridPosition);

        if (distToTarget <= npc.Stats.AttackRange && !targetPC.Stats.IsDead)
        {
            yield return StartCoroutine(NPCPerformAttack(npc, targetPC));
        }
        else
        {
            yield return new WaitForSeconds(0.3f);
        }
    }

    private IEnumerator NPCPerformAttack(CharacterController npc, CharacterController target)
    {
        npc.Actions.UseStandardAction();
        RangeInfo npcRangeInfo = CalculateRangeInfo(npc, target);
        CombatResult result = npc.Attack(target, false, 0, null, npcRangeInfo);
        _lastCombatLog = result.GetDetailedSummary();

        if (LogAttacksToConsole)
            Debug.Log("[Combat] " + _lastCombatLog);

        CombatUI.ShowCombatLog(_lastCombatLog);
        UpdateAllStatsUI();

        // Check concentration for NPC attack damage on target (usually a PC)
        if (result.Hit && result.TotalDamage > 0)
        {
            CheckConcentrationOnDamage(target, result.TotalDamage);
        }

        if (result.TargetKilled)
        {
            if (AreAllPCsDead())
            {
                CurrentPhase = TurnPhase.CombatOver;
                CombatUI.SetTurnIndicator("DEFEAT! All heroes have fallen!");
                CombatUI.SetActionButtonsVisible(false);
                yield break;
            }
            else
            {
                CombatUI.ShowCombatLog(_lastCombatLog + $"\n{target.Stats.CharacterName} has fallen, but the fight continues!");
            }
        }

        yield return new WaitForSeconds(1.0f);
    }

    /// <summary>Find closest alive PC relative to a specific NPC (checks all 4 PCs).</summary>
    private CharacterController GetClosestAlivePCTo(CharacterController npc)
    {
        CharacterController closest = null;
        int closestDist = int.MaxValue;

        foreach (var pc in PCs)
        {
            if (pc == null || pc.Stats.IsDead) continue;
            int dist = SquareGridUtils.GetDistance(npc.GridPosition, pc.GridPosition);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = pc;
            }
        }
        return closest;
    }

    /// <summary>Legacy wrapper for backward compat.</summary>
    private CharacterController GetClosestAlivePC()
    {
        return (NPC != null) ? GetClosestAlivePCTo(NPC) : null;
    }

    /// <summary>Get the alive PC with the lowest current HP (for defensive AI targeting).</summary>
    private CharacterController GetWeakerAlivePC()
    {
        CharacterController weakest = null;
        int lowestHP = int.MaxValue;

        foreach (var pc in PCs)
        {
            if (pc == null || pc.Stats.IsDead) continue;
            if (pc.Stats.CurrentHP < lowestHP)
            {
                lowestHP = pc.Stats.CurrentHP;
                weakest = pc;
            }
        }
        return weakest;
    }

    private SquareCell FindBestMoveAwayFrom(CharacterController mover, Vector2Int threatPos)
    {
        List<SquareCell> moveCells = Grid.GetCellsInRange(mover.GridPosition, mover.Stats.MoveRange);
        SquareCell bestCell = null;
        int bestDist = 0;

        foreach (var cell in moveCells)
        {
            if (cell.IsOccupied) continue;

            int dist = SquareGridUtils.GetDistance(cell.Coords, threatPos);
            if (dist > bestDist)
            {
                bestDist = dist;
                bestCell = cell;
            }
        }

        return bestCell;
    }

    private SquareCell FindBestMoveToward(CharacterController mover, Vector2Int targetPos)
    {
        List<SquareCell> moveCells = Grid.GetCellsInRange(mover.GridPosition, mover.Stats.MoveRange);
        SquareCell bestCell = null;
        int bestDist = int.MaxValue;

        foreach (var cell in moveCells)
        {
            if (cell.IsOccupied) continue;

            int dist = SquareGridUtils.GetDistance(cell.Coords, targetPos);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestCell = cell;
            }
        }

        return bestCell;
    }

    // ========== DETAILED CONSOLE LOGGING ==========

    private void LogFullAttackToConsole(FullAttackResult result)
    {
        string attackerName = result.Attacker.Stats.CharacterName;
        string defenderName = result.Defender.Stats.CharacterName;

        Debug.Log("[Combat] ═══════════════════════════════════════");

        string actionLabel = result.Type == FullAttackResult.AttackType.FullAttack
            ? "full attacks"
            : result.Type == FullAttackResult.AttackType.DualWield
                ? "dual wields against"
                : "attacks";
        Debug.Log($"[Combat] {attackerName} {actionLabel} {defenderName}");

        if (result.Type == FullAttackResult.AttackType.DualWield
            && !string.IsNullOrEmpty(result.MainWeaponName)
            && !string.IsNullOrEmpty(result.OffWeaponName))
        {
            Debug.Log($"[Combat] Main Hand: {result.MainWeaponName}");
            Debug.Log($"[Combat] Off Hand: {result.OffWeaponName}");
        }
        else if (!string.IsNullOrEmpty(result.MainWeaponName))
        {
            bool isRanged = result.Attacks.Count > 0 && result.Attacks[0].IsRangedAttack;
            string wpnType = isRanged ? "ranged" : "melee";
            Debug.Log($"[Combat] Weapon: {result.MainWeaponName} ({wpnType})");
        }

        if (result.Attacks.Count > 0)
        {
            var first = result.Attacks[0];
            var feats = new List<string>();
            if (first.PowerAttackValue > 0) feats.Add($"Power Attack (-{first.PowerAttackValue} atk/+{first.PowerAttackDamageBonus} dmg)");
            if (first.RapidShotActive) feats.Add("Rapid Shot");
            if (first.PointBlankShotActive) feats.Add("Point Blank Shot");
            if (feats.Count > 0)
                Debug.Log($"[Combat] Active Feats: {string.Join(", ", feats)}");

            if (first.IsFlanking)
                Debug.Log($"[Combat] Flanking: Yes (with {first.FlankingPartnerName}, +{first.FlankingBonus})");

            if (first.IsRangedAttack)
            {
                string penaltyStr = first.RangePenalty == 0 ? "no penalty" : $"{first.RangePenalty} penalty";
                Debug.Log($"[Combat] Range: {first.RangeDistanceFeet} ft ({first.RangeDistanceSquares} sq) - Increment {first.RangeIncrementNumber}, {penaltyStr}");
            }
        }

        Debug.Log("[Combat]");

        for (int i = 0; i < result.Attacks.Count; i++)
        {
            Debug.Log("[Combat] ─────────────────────────────────────");

            CombatResult atk = result.Attacks[i];

            string label = (i < result.AttackLabels.Count) ? result.AttackLabels[i] : $"Attack {i + 1}";
            Debug.Log($"[Combat] {label}:");

            Debug.Log("[Combat]   ATTACK ROLL:");
            Debug.Log($"[Combat]     d20 roll: {atk.DieRoll}");

            if (atk.BreakdownBAB != 0)
                Debug.Log($"[Combat]     {FormatConsoleModLine(atk.BreakdownBAB, "BAB")}");

            string abilName = !string.IsNullOrEmpty(atk.BreakdownAbilityName) ? atk.BreakdownAbilityName : "STR";
            if (atk.BreakdownAbilityMod != 0)
                Debug.Log($"[Combat]     {FormatConsoleModLine(atk.BreakdownAbilityMod, $"{abilName} modifier")}");

            if (atk.SizeAttackBonus != 0)
                Debug.Log($"[Combat]     {FormatConsoleModLine(atk.SizeAttackBonus, "size")}");

            if (atk.IsFlanking && atk.FlankingBonus != 0)
                Debug.Log($"[Combat]     {FormatConsoleModLine(atk.FlankingBonus, "flanking")}");

            if (atk.RacialAttackBonus != 0)
                Debug.Log($"[Combat]     {FormatConsoleModLine(atk.RacialAttackBonus, "racial")}");

            if (atk.PowerAttackValue > 0)
                Debug.Log($"[Combat]     {FormatConsoleModLine(-atk.PowerAttackValue, "Power Attack")}");

            if (atk.RapidShotActive)
                Debug.Log($"[Combat]     {FormatConsoleModLine(-2, "Rapid Shot")}");

            if (atk.PointBlankShotActive)
                Debug.Log($"[Combat]     {FormatConsoleModLine(1, "Point Blank Shot")}");

            if (atk.IsRangedAttack && atk.RangePenalty != 0)
                Debug.Log($"[Combat]     {FormatConsoleModLine(atk.RangePenalty, "range")}");

            if (atk.IsDualWieldAttack && atk.BreakdownDualWieldPenalty != 0)
            {
                string dwLabel = atk.IsOffHandAttack ? "off-hand penalty" : "dual wield penalty";
                Debug.Log($"[Combat]     {FormatConsoleModLine(atk.BreakdownDualWieldPenalty, dwLabel)}");
            }

            string critNote = "";
            if (atk.NaturalTwenty) critNote = " (NATURAL 20!)";
            else if (atk.NaturalOne) critNote = " (NATURAL 1!)";
            string hitMiss = atk.Hit ? "HIT!" : "MISS!";
            Debug.Log($"[Combat]     = {atk.TotalRoll} vs AC {atk.TargetAC} - {hitMiss}{critNote}");

            if (atk.IsCritThreat)
            {
                string threatRange = atk.CritThreatMin < 20 ? $"{atk.CritThreatMin}-20" : "20";
                string confModStr = CharacterStats.FormatMod(atk.ConfirmationTotal - atk.ConfirmationRoll);
                if (atk.CritConfirmed)
                    Debug.Log($"[Combat]   *** CRITICAL THREAT ({threatRange})! Confirm: {atk.ConfirmationRoll} {confModStr} = {atk.ConfirmationTotal} vs AC {atk.TargetAC} - CONFIRMED! (×{atk.CritMultiplier}) ***");
                else
                    Debug.Log($"[Combat]   *** Critical Threat ({threatRange})! Confirm: {atk.ConfirmationRoll} {confModStr} = {atk.ConfirmationTotal} vs AC {atk.TargetAC} - Not confirmed ***");
            }

            if (atk.Hit)
            {
                Debug.Log("[Combat]   DAMAGE ROLL:");
                string diceStr = !string.IsNullOrEmpty(atk.BaseDamageDiceStr) ? atk.BaseDamageDiceStr : "?";

                if (atk.CritConfirmed)
                {
                    Debug.Log($"[Combat]     CRITICAL HIT! (×{atk.CritMultiplier})");
                    Debug.Log($"[Combat]     {atk.CritDamageDice} = {atk.Damage - atk.FeatDamageBonus} (crit weapon + mods)");
                }
                else
                {
                    Debug.Log($"[Combat]     {diceStr} roll: {atk.BaseDamageRoll}");

                    if (atk.DamageModifier != 0)
                    {
                        string dmgModLabel = !string.IsNullOrEmpty(atk.DamageModifierDesc) ? atk.DamageModifierDesc : abilName;
                        Debug.Log($"[Combat]     {FormatConsoleModLine(atk.DamageModifier, dmgModLabel)}");
                    }
                }

                if (atk.PowerAttackDamageBonus > 0)
                    Debug.Log($"[Combat]     {FormatConsoleModLine(atk.PowerAttackDamageBonus, "Power Attack")}");

                if (atk.PointBlankShotActive)
                    Debug.Log($"[Combat]     {FormatConsoleModLine(1, "Point Blank Shot")}");

                Debug.Log($"[Combat]     = {atk.Damage} damage");

                if (atk.SneakAttackApplied)
                {
                    Debug.Log($"[Combat]     + {atk.SneakAttackDamage} sneak attack ({atk.SneakAttackDice}d6)");
                    Debug.Log($"[Combat]     = {atk.TotalDamage} total damage");
                }
            }

            Debug.Log("[Combat]");
        }

        Debug.Log("[Combat] ─────────────────────────────────────");
        string critSummary = result.CritCount > 0 ? $", {result.CritCount} critical(s)!" : "";
        Debug.Log($"[Combat] SUMMARY: {result.HitCount}/{result.Attacks.Count} hits{critSummary}, {result.TotalDamageDealt} total damage");
        Debug.Log($"[Combat] {defenderName}: {result.DefenderHPBefore} → {result.DefenderHPAfter} HP");

        if (result.TargetKilled)
            Debug.Log($"[Combat] {defenderName} has been slain!");

        Debug.Log("[Combat] ═══════════════════════════════════════");
    }

    private static string FormatConsoleModLine(int value, string label)
    {
        if (value >= 0)
            return $"+ {value} ({label})";
        else
            return $"- {-value} ({label})";
    }

    // ========== QUICKENED SPELL TRACKING (D&D 3.5e: ONE PER ROUND) ==========

    /// <summary>
    /// Reset quickened spell tracking for all characters at the start of a new round.
    /// D&D 3.5e: Each character can cast only one quickened spell per round.
    /// </summary>
    private void ResetQuickenedSpellTrackingForAllCharacters()
    {
        foreach (var character in GetAllCharacters())
        {
            var spellComp = character.GetComponent<SpellcastingComponent>();
            if (spellComp != null)
            {
                spellComp.ResetQuickenedSpellTracking();
            }
        }
        Debug.Log("[GameManager] Quickened spell tracking reset for new round");
    }
}