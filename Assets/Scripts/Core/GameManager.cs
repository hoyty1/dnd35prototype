using System;
using System.Collections;
using System.Collections.Generic;
using DND35.AI.Profiles;
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
public partial class GameManager : MonoBehaviour
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

    [Header("Combat Systems")]
    public TurnUndeadSystem turnUndeadSystem;
    public GrappleSystem grappleSystem;
    public OverrunSystem overrunSystem;
    public SupportActions supportActions;
    public StandardManeuvers standardManeuvers;

    /// <summary>Whether the game is waiting for character creation to complete.</summary>
    public bool WaitingForCharacterCreation { get; private set; }

    /// <summary>Encounter preset selection overlay shown before combat starts.</summary>
    public EncounterSelectionUI EncounterSelectionUI;

    /// <summary>Whether combat setup is waiting on encounter selection.</summary>
    public bool WaitingForEncounterSelection { get; private set; }

    private const string GrappleTestPresetId = "grapple_test";
    private const string FeintSneakTestPresetId = "feint_sneak_test";
    private const string TurnUndeadTestPresetId = "turn_undead_test";
    private const string ArmorTargetingTestPresetId = "armor_targeting_test";
    private string _selectedEncounterPresetId = "goblin_raiders";
    private bool _isGrappleTestEncounter;
    private bool _isFeintSneakTestEncounter;
    private bool _isTurnUndeadTestEncounter;
    private bool _isArmorTargetingTestEncounter;
    private readonly List<string> _activeEncounterEnemyIds = new List<string>();

    // Game state
    public enum TurnPhase { PCTurn, NPCTurn, CombatOver }

    // Sub-states for player turns
    public enum PlayerSubPhase
    {
        ChoosingAction,
        Moving,
        TakingFiveFootStep,
        Crawling,
        SelectingAttackTarget,
        SelectingSpecialTarget,
        SelectingChargeTarget,
        ConfirmingChargePath,
        SelectingAoETarget,
        ConfirmingSelfAoE,
        ConfirmingTurnUndead,
        Animating
    }

    public TurnPhase CurrentPhase { get; private set; }
    public PlayerSubPhase CurrentSubPhase { get; private set; }

    // ========== INITIATIVE / TURN SERVICE ==========
    [SerializeField] private TurnService _turnService;
    [SerializeField] private MovementService _movementService;
    [SerializeField] private InputService _inputService;
    [SerializeField] private ConditionService _conditionService;
    [SerializeField] private AIService _aiService;
    [SerializeField] private CombatFlowService _combatFlowService;

    /// <summary>Current combatant in initiative order (PC or NPC).</summary>
    public CharacterController CurrentCharacter => _turnService != null ? _turnService.CurrentCharacter : null;

    /// <summary>Current combat round number (starts at 1 once combat begins).</summary>
    public int CurrentRound => _turnService != null ? _turnService.CurrentRound : 0;

    /// <summary>Returns the PC whose turn it currently is (null during NPC turns).</summary>
    public CharacterController ActivePC
    {
        get
        {
            CharacterController current = CurrentCharacter;
            if (CurrentPhase == TurnPhase.PCTurn && current != null && IsPC(current))
                return current;
            return null;
        }
    }

    public bool IsPlayerTurn => ActivePC != null;

    // Current attack mode being selected for
    public enum PendingAttackMode { Single, FullAttack, DualWield, FlurryOfBlows, CastSpell }

    public enum AttackType
    {
        Melee,
        Thrown,
        Ranged
    }

    private PendingAttackMode _pendingAttackMode;
    private AttackType _currentAttackType = AttackType.Melee;
    private bool _pendingDefensiveAttackSelection; // Set when targeting for a defensive attack action
    private SpellData _pendingSpell; // Spell selected for casting
    private MetamagicData _pendingMetamagic; // Metamagic applied to pending spell
    private bool _pendingSpellFromHeldCharge; // True when delivering an already-held touch spell charge

    // Mid-sequence full-attack retargeting state (ranged + melee)
    private bool _isAwaitingRangedRetargetSelection;
    private bool _rangedRetargetSelectionCancelled;
    private CharacterController _selectedRangedRetarget;

    // Mid-sequence full-attack 5-foot-step state
    private bool _isAwaitingFullAttackFiveFootStepSelection;
    private bool _fullAttackFiveFootStepSelectionCancelled;
    private bool _fullAttackFiveFootStepWasTaken;
    private bool _fullAttackFiveFootStepRequireReachableTarget;
    private bool _fullAttackFiveFootStepRangedMode;

    // Pending special attack state
    private SpecialAttackType _pendingSpecialAttackType;
    private bool _isSelectingSpecialAttack;
    private bool _pendingDisarmUseOffHandSelection;
    private bool _pendingSunderUseOffHandSelection;

    // Turn Undead targeted-confirmation state

    // Unified iterative attack flow state (melee + thrown share one sequence)
    private bool _isInAttackSequence;
    private int _totalAttackBudget;
    private int _totalAttacksUsed;
    private CharacterController _attackingCharacter;
    private ItemData _equippedWeapon;
    private bool _attackSequenceConsumesFullRound;
    private int _currentAttackBAB;

    // Flexible off-hand attack flow state
    // Dedicated off-hand flags, intentionally independent from main-hand sequence tracking.
    private bool _offHandAttackAvailableThisTurn;
    private bool _offHandAttackUsedThisTurn;
    private bool _isSelectingOffHandTarget;
    private bool _isSelectingOffHandThrownTarget;
    private int _currentOffHandBAB;
    private ItemData _currentOffHandWeapon;

    // Turn-scoped dual-wield choice state (first main-hand attack prompt)
    private bool _dualWieldingChoiceMade;
    private bool _isDualWielding;
    private int _mainHandPenalty;
    private int _offHandPenalty;
    private AttackType _pendingAttackType = AttackType.Melee;

    private bool _skipNextSingleAttackStandardActionCommit;

    // Iterative disarm flow state
    private bool _isDisarmSequenceActive;
    private CharacterController _disarmInitiator;
    private CharacterController _disarmTarget;
    private EquipSlot? _disarmTargetSlot;
    private int _disarmAttemptNumber;

    // Iterative sunder flow state
    private bool _isSunderSequenceActive;
    private CharacterController _sunderInitiator;
    private CharacterController _sunderTarget;
    private EquipSlot? _sunderTargetSlot;
    private int _sunderAttemptNumber;

    // Destination-based overrun selection/execution state.

    // Player grapple movement selection state (after winning Move While Grappling opposed check).

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

    private void HighlightCharacterFootprint(CharacterController character, HighlightType type, bool addToSelectableCells = false)
    {
        if (character == null || Grid == null)
            return;

        List<Vector2Int> occupiedSquares = character.GetOccupiedSquares();
        if (occupiedSquares == null || occupiedSquares.Count == 0)
            occupiedSquares = new List<Vector2Int> { character.GridPosition };

        for (int i = 0; i < occupiedSquares.Count; i++)
        {
            SquareCell cell = Grid.GetCell(occupiedSquares[i]);
            if (cell == null)
                continue;

            cell.SetHighlight(type);

            if (addToSelectableCells && !_highlightedCells.Contains(cell))
                _highlightedCells.Add(cell);
        }
    }

    // ========== PATH PREVIEW ==========
    private PathPreview _pathPreview;

    // ========== HOVER MARKER ==========
    private HoverMarker _hoverMarker;
    private Vector2Int _lastHoverMarkerCoord = new Vector2Int(-999, -999);

    // ========== CHARACTER HOVER TOOLTIP ==========
    private CharacterController _lastHoveredCharacter;

    // ========== SUMMONING STATE ==========
    private readonly HashSet<CharacterController> _summonedAllies = new HashSet<CharacterController>();
    private readonly HashSet<CharacterController> _summonedEnemies = new HashSet<CharacterController>();
    private readonly List<ActiveSummonInstance> _activeSummons = new List<ActiveSummonInstance>();

    private class ActiveSummonInstance
    {
        public CharacterController Controller;
        public CharacterController Caster;
        public SummonTemplate Template;
        public int RemainingRounds;
        public int TotalDurationRounds;
        public string SourceSpellId;
        public bool IsAlliedToPCs;
        public bool SmiteUsed;
        public SummonCommand CurrentCommand;
    }


    private class SummonTemplate
    {
        public string TemplateId;
        public string DisplayName;
        public string CharacterClass;
        public string TokenType;
        public Color TintColor;
        public int Level;
        public int STR, DEX, CON, WIS, INT, CHA;
        public int BAB;
        public int ArmorBonus;
        public int ShieldBonus;
        public int DamageDice;
        public int DamageCount;
        public int BonusDamage;
        public int BaseSpeed;
        public int AttackRange;
        public global::SizeCategory SizeCategory;
        public int BaseHitDieHP;
        public string CreatureTypeLine;
        public string AttackLabel;
        public bool IsCelestial;
        public bool IsFiendish;
        public bool HasTrip;
        public bool HasDisease;
        public bool HasMultiAttack;
        public List<string> SpecialTraits = new List<string>();
        public List<string> CreatureTags = new List<string>();
    }

    private static readonly Dictionary<string, List<SummonTemplate>> SummonMonsterOptions = new Dictionary<string, List<SummonTemplate>>
    {
        {
            "summon_monster_1", new List<SummonTemplate>
            {
                new SummonTemplate
                {
                    TemplateId = "sm1_celestial_dog",
                    DisplayName = "Celestial Dog",
                    CharacterClass = "Warrior",
                    TokenType = "wolf",
                    TintColor = new Color(0.95f, 0.95f, 0.8f, 1f),
                    CreatureTypeLine = "Small Magical Beast (Good)",
                    AttackLabel = "Bite",
                    IsCelestial = true,
                    HasTrip = false,
                    Level = 1,
                    STR = 13, DEX = 14, CON = 13, WIS = 12, INT = 2, CHA = 6,
                    BAB = 1,
                    ArmorBonus = 1,
                    ShieldBonus = 0,
                    DamageDice = 6,
                    DamageCount = 1,
                    BonusDamage = 1,
                    BaseSpeed = 8,
                    AttackRange = 1,
                    SizeCategory = global::SizeCategory.Small,
                    BaseHitDieHP = 8,
                    SpecialTraits = new List<string> { "DR 5/magic", "Resist 5 (acid, cold, electricity)", "Scent", "Smite Evil 1/day" },
                    CreatureTags = new List<string> { "Animal", "Summoned", "Good" }
                },
                new SummonTemplate
                {
                    TemplateId = "sm1_fiendish_wolf",
                    DisplayName = "Fiendish Wolf",
                    CharacterClass = "Warrior",
                    TokenType = "wolf",
                    TintColor = new Color(0.6f, 0.2f, 0.2f, 1f),
                    CreatureTypeLine = "Medium Magical Beast (Evil)",
                    AttackLabel = "Bite",
                    IsFiendish = true,
                    HasTrip = true,
                    Level = 1,
                    STR = 13, DEX = 15, CON = 15, WIS = 12, INT = 2, CHA = 6,
                    BAB = 1,
                    ArmorBonus = 2,
                    ShieldBonus = 0,
                    DamageDice = 6,
                    DamageCount = 1,
                    BonusDamage = 2,
                    BaseSpeed = 8,
                    AttackRange = 1,
                    SizeCategory = global::SizeCategory.Medium,
                    BaseHitDieHP = 10,
                    SpecialTraits = new List<string> { "DR 5/magic", "Resist 5 (cold, fire)", "Trip", "Smite Good 1/day" },
                    CreatureTags = new List<string> { "Animal", "Summoned", "Evil" }
                },
                new SummonTemplate
                {
                    TemplateId = "sm1_small_air_elemental",
                    DisplayName = "Small Air Elemental",
                    CharacterClass = "Warrior",
                    TokenType = "wizard",
                    TintColor = new Color(0.65f, 0.85f, 1f, 1f),
                    CreatureTypeLine = "Small Elemental (Air)",
                    AttackLabel = "Slam",
                    HasTrip = false,
                    Level = 1,
                    STR = 10, DEX = 17, CON = 12, WIS = 11, INT = 4, CHA = 11,
                    BAB = 1,
                    ArmorBonus = 2,
                    ShieldBonus = 0,
                    DamageDice = 6,
                    DamageCount = 1,
                    BonusDamage = 1,
                    BaseSpeed = 10,
                    AttackRange = 1,
                    SizeCategory = global::SizeCategory.Small,
                    BaseHitDieHP = 8,
                    SpecialTraits = new List<string> { "Darkvision 60 ft", "Elemental traits", "Whirlwind (prototype)" },
                    CreatureTags = new List<string> { "Elemental", "Summoned" }
                }
            }
        },
        {
            "summon_monster_2", new List<SummonTemplate>
            {
                new SummonTemplate
                {
                    TemplateId = "sm2_celestial_wolf",
                    DisplayName = "Celestial Wolf",
                    CharacterClass = "Warrior",
                    TokenType = "wolf",
                    TintColor = new Color(0.92f, 0.92f, 0.75f, 1f),
                    CreatureTypeLine = "Medium Magical Beast (Good)",
                    AttackLabel = "Bite",
                    IsCelestial = true,
                    HasTrip = true,
                    Level = 2,
                    STR = 15, DEX = 15, CON = 15, WIS = 12, INT = 2, CHA = 6,
                    BAB = 2,
                    ArmorBonus = 3,
                    ShieldBonus = 0,
                    DamageDice = 8,
                    DamageCount = 1,
                    BonusDamage = 2,
                    BaseSpeed = 8,
                    AttackRange = 1,
                    SizeCategory = global::SizeCategory.Medium,
                    BaseHitDieHP = 14,
                    SpecialTraits = new List<string> { "DR 5/magic", "Resist 5 (acid, cold, electricity)", "Trip", "Smite Evil 1/day" },
                    CreatureTags = new List<string> { "Animal", "Summoned", "Good" }
                },
                new SummonTemplate
                {
                    TemplateId = "sm2_fiendish_boar",
                    DisplayName = "Fiendish Boar",
                    CharacterClass = "Warrior",
                    TokenType = "orc",
                    TintColor = new Color(0.45f, 0.25f, 0.2f, 1f),
                    CreatureTypeLine = "Medium Magical Beast (Evil)",
                    AttackLabel = "Gore",
                    IsFiendish = true,
                    HasTrip = false,
                    Level = 2,
                    STR = 17, DEX = 10, CON = 17, WIS = 13, INT = 2, CHA = 4,
                    BAB = 2,
                    ArmorBonus = 4,
                    ShieldBonus = 0,
                    DamageDice = 8,
                    DamageCount = 1,
                    BonusDamage = 3,
                    BaseSpeed = 8,
                    AttackRange = 1,
                    SizeCategory = global::SizeCategory.Medium,
                    BaseHitDieHP = 16,
                    SpecialTraits = new List<string> { "DR 5/magic", "Resist 5 (cold, fire)", "Ferocity", "Smite Good 1/day" },
                    CreatureTags = new List<string> { "Animal", "Summoned", "Evil" }
                },
                new SummonTemplate
                {
                    TemplateId = "sm2_small_fire_elemental",
                    DisplayName = "Small Fire Elemental",
                    CharacterClass = "Warrior",
                    TokenType = "wizard",
                    TintColor = new Color(1f, 0.55f, 0.25f, 1f),
                    CreatureTypeLine = "Small Elemental (Fire)",
                    AttackLabel = "Slam",
                    HasTrip = false,
                    Level = 2,
                    STR = 12, DEX = 17, CON = 12, WIS = 11, INT = 4, CHA = 11,
                    BAB = 2,
                    ArmorBonus = 3,
                    ShieldBonus = 0,
                    DamageDice = 8,
                    DamageCount = 1,
                    BonusDamage = 1,
                    BaseSpeed = 10,
                    AttackRange = 1,
                    SizeCategory = global::SizeCategory.Small,
                    BaseHitDieHP = 12,
                    SpecialTraits = new List<string> { "Darkvision 60 ft", "Elemental traits", "Fire aura (prototype)" },
                    CreatureTags = new List<string> { "Elemental", "Summoned", "Fire" }
                }
            }
        }
    };
    /// <summary>
    /// Tracks whether we've already logged the "no actions but holding charge" reminder this turn.
    /// Prevents duplicate log spam while still informing the player.
    /// </summary>
    private bool _loggedHeldChargeNoActionsReminder;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _turnService ??= gameObject.GetComponent<TurnService>() ?? gameObject.AddComponent<TurnService>();
        _turnService.OnTurnStarted += OnTurnStarted;
        _turnService.OnNewRound += OnNewRound;
        _turnService.OnCombatEnded += OnCombatEnded;

        _movementService ??= gameObject.GetComponent<MovementService>() ?? gameObject.AddComponent<MovementService>();
        _movementService.Initialize(Grid, GetAllCharacters);

        _inputService ??= gameObject.GetComponent<InputService>() ?? gameObject.AddComponent<InputService>();
        _inputService.Initialize(
            mainCamera: _mainCam,
            canProcessInput: CanProcessWorldInput,
            shouldAllowGridClickThroughUi: ShouldAllowGridClickThroughUIBlock,
            secondaryClickHandler: HandleInputSecondaryClick,
            cancelActionHandler: HandleInputCancelRequested);
        _inputService.RegisterClickHandler(InputService.InputMode.Normal, HandleInputModeLeftClick);
        _inputService.RegisterClickHandler(InputService.InputMode.SelectingTarget, HandleInputModeLeftClick);
        _inputService.RegisterClickHandler(InputService.InputMode.SelectingMovement, HandleInputModeLeftClick);
        _inputService.RegisterClickHandler(InputService.InputMode.SelectingArea, HandleInputModeLeftClick);
        _inputService.RegisterClickHandler(InputService.InputMode.PlacingSummon, HandleInputModeLeftClick);
        _inputService.OnInventoryToggleRequested += HandleInventoryInput;
        _inputService.OnSkillsToggleRequested += HandleSkillsInput;
        _inputService.OnCharacterSheetToggleRequested += HandleCharacterSheetInput;

        _conditionService ??= gameObject.GetComponent<ConditionService>() ?? gameObject.AddComponent<ConditionService>();
        _conditionService.Initialize(GetAllCharacters);
        _conditionService.BindTurnService(_turnService);
        _conditionService.OnConditionExpired += HandleConditionExpired;

        _aiService ??= gameObject.GetComponent<AIService>() ?? gameObject.AddComponent<AIService>();
        _aiService.Initialize(this);

        _combatFlowService ??= gameObject.GetComponent<CombatFlowService>() ?? gameObject.AddComponent<CombatFlowService>();
        _combatFlowService.Initialize(this);

        turnUndeadSystem ??= gameObject.GetComponent<TurnUndeadSystem>() ?? gameObject.AddComponent<TurnUndeadSystem>();
        grappleSystem ??= gameObject.GetComponent<GrappleSystem>() ?? gameObject.AddComponent<GrappleSystem>();
        overrunSystem ??= gameObject.GetComponent<OverrunSystem>() ?? gameObject.AddComponent<OverrunSystem>();
        supportActions ??= gameObject.GetComponent<SupportActions>() ?? gameObject.AddComponent<SupportActions>();
        standardManeuvers ??= gameObject.GetComponent<StandardManeuvers>() ?? gameObject.AddComponent<StandardManeuvers>();

        turnUndeadSystem.Initialize(this);
        grappleSystem.Initialize(this);
        overrunSystem.Initialize(this);
        supportActions.Initialize(this);
        standardManeuvers.Initialize(this);
    }

    private void OnDestroy()
    {
        if (_turnService != null)
        {
            _turnService.OnTurnStarted -= OnTurnStarted;
            _turnService.OnNewRound -= OnNewRound;
            _turnService.OnCombatEnded -= OnCombatEnded;
        }

        if (_inputService != null)
        {
            _inputService.OnInventoryToggleRequested -= HandleInventoryInput;
            _inputService.OnSkillsToggleRequested -= HandleSkillsInput;
            _inputService.OnCharacterSheetToggleRequested -= HandleCharacterSheetInput;
        }

        if (_conditionService != null)
        {
            _conditionService.OnConditionExpired -= HandleConditionExpired;
            _conditionService.UnbindTurnService();
        }

        _aiService?.Cleanup();
        _combatFlowService?.Cleanup();

        turnUndeadSystem?.Cleanup();
        grappleSystem?.Cleanup();
        overrunSystem?.Cleanup();
        supportActions?.Cleanup();
        standardManeuvers?.Cleanup();
    }

    private void Start()
    {
        _movementService ??= gameObject.GetComponent<MovementService>() ?? gameObject.AddComponent<MovementService>();
        _movementService.SetGrid(Grid);
        _movementService.Initialize(Grid, GetAllCharacters);

        Grid.GenerateGrid();
        CenterCamera();
        _mainCam = Camera.main;
        _inputService ??= gameObject.GetComponent<InputService>() ?? gameObject.AddComponent<InputService>();
        _inputService.SetCamera(_mainCam);

        // Initialize path preview for movement hover
        var previewGO = new GameObject("PathPreview");
        _pathPreview = previewGO.AddComponent<PathPreview>();

        // Initialize hover marker (X indicator on hovered square)
        var markerGO = new GameObject("HoverMarker");
        _hoverMarker = markerGO.AddComponent<HoverMarker>();

        // Initialize icon system
        IconManager.Init();
        CharacterHoverTooltipUI.EnsureInstance();

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
            PromptEncounterSelection();
            Debug.Log("[GameManager] Initialization complete (default characters, waiting for encounter selection).");
        }
    }

    // ========== HELPER: Team/side queries ==========
    private bool IsPC(CharacterController c)
    {
        if (c == null) return false;
        // Summons are always autonomous NPC turns (even if allied to the party).
        return PCs.Contains(c);
    }

    private bool IsEnemyTeam(CharacterController source, CharacterController target)
    {
        if (source == null || target == null) return false;
        return source.IsPlayerControlled != target.IsPlayerControlled;
    }

    private bool IsAllyTeam(CharacterController source, CharacterController target)
    {
        if (source == null || target == null) return false;
        return source.IsPlayerControlled == target.IsPlayerControlled;
    }

    private List<CharacterController> GetTeamMembers(bool isPlayerControlled)
    {
        var team = new List<CharacterController>();
        foreach (var c in GetAllCharacters())
        {
            if (c == null || c.Stats == null || c.Stats.IsDead) continue;
            if (c.IsPlayerControlled == isPlayerControlled)
                team.Add(c);
        }

        return team;
    }

    private bool IsAdjacent(CharacterController a, CharacterController b)
    {
        if (a == null || b == null || a.Stats == null || b.Stats == null || a.Stats.IsDead || b.Stats.IsDead)
            return false;

        int distance = a.GetMinimumDistanceToTarget(b, chebyshev: true);
        return distance == 1;
    }



    private CharacterController GetClosestAliveEnemyTo(CharacterController source)
    {
        if (source == null) return null;

        CharacterController closest = null;
        int closestDist = int.MaxValue;

        foreach (var candidate in GetAllCharacters())
        {
            if (candidate == null || candidate == source || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!IsEnemyTeam(source, candidate))
                continue;

            int dist = SquareGridUtils.GetDistance(source.GridPosition, candidate.GridPosition);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = candidate;
            }
        }

        return closest;
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
        PromptEncounterSelection();
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
        PromptEncounterSelection();
    }


    private void PromptEncounterSelection()
    {
        EnemyDatabase.Init();

        if (EncounterSelectionUI == null)
            EncounterSelectionUI = FindObjectOfType<EncounterSelectionUI>();
        if (EncounterSelectionUI == null)
            EncounterSelectionUI = gameObject.AddComponent<EncounterSelectionUI>();

        WaitingForEncounterSelection = true;
        var presets = EnemyDatabase.ListEncounterPresets();

        // Show all available encounters so the player can scroll and select any scenario.
        EncounterSelectionUI.Open(presets,
            onSelect: presetId =>
            {
                WaitingForEncounterSelection = false;
                _selectedEncounterPresetId = string.IsNullOrEmpty(presetId) ? "goblin_raiders" : presetId;
                ApplyEncounterPreset(_selectedEncounterPresetId);
                StartCombat();
            },
            onCancel: () =>
            {
                WaitingForEncounterSelection = false;
                _selectedEncounterPresetId = "goblin_raiders";
                ApplyEncounterPreset(_selectedEncounterPresetId);
                StartCombat();
            });
    }

    private void ApplyEncounterPreset(string presetId)
    {
        EncounterPreset preset = EnemyDatabase.GetEncounterPreset(presetId);
        _activeEncounterEnemyIds.Clear();
        _isGrappleTestEncounter = string.Equals(presetId, GrappleTestPresetId, StringComparison.Ordinal);
        _isFeintSneakTestEncounter = string.Equals(presetId, FeintSneakTestPresetId, StringComparison.Ordinal);
        _isTurnUndeadTestEncounter = string.Equals(presetId, TurnUndeadTestPresetId, StringComparison.Ordinal);
        _isArmorTargetingTestEncounter = string.Equals(presetId, ArmorTargetingTestPresetId, StringComparison.Ordinal);

        if (preset != null && preset.EnemyIds != null && preset.EnemyIds.Count > 0)
        {
            _activeEncounterEnemyIds.AddRange(preset.EnemyIds);
            CombatUI?.ShowCombatLog($"🧭 Encounter selected: {preset.DisplayName}");
        }
        else
        {
            _activeEncounterEnemyIds.Add("goblin_warchief");
            _activeEncounterEnemyIds.Add("hobgoblin_sergeant");
            _activeEncounterEnemyIds.Add("skeleton_archer");
            CombatUI?.ShowCombatLog("🧭 Encounter fallback selected: Goblin Raiders");
        }

        if (_isGrappleTestEncounter)
            ConfigureGrappleTestParty();
        else if (_isFeintSneakTestEncounter)
            ConfigureFeintSneakTestParty();
        else if (_isTurnUndeadTestEncounter)
            ConfigureTurnUndeadTestParty();
        else if (_isArmorTargetingTestEncounter)
            ConfigureArmorTargetingTestParty();
        else
            RestoreStandardPartyLayout();

        SetupEnemyEncounter(_activeEncounterEnemyIds);
        SetupNPCIcons();
        UpdateAllStatsUI();
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
            concMgr.Init(stats, pcSlots[i]);

            // Set PC icon
            Sprite classIcon = IconManager.GetClassIcon(data.ClassName);
            if (classIcon != null && CombatUI != null)
                CombatUI.SetPCIcon(i + 1, classIcon);
        }

        // ===== NPCs (Multiple Enemies) =====
        // Enemy encounter setup is deferred until the player selects a preset.
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
    /// Handle player and UI input every frame via InputService.
    /// </summary>
    private void Update()
    {
        // Skip all game input during character creation / encounter selection.
        if (WaitingForCharacterCreation || WaitingForEncounterSelection)
        {
            HideCharacterHoverTooltip();
            return;
        }

        _inputService?.SetInputMode(ResolveInputMode());
        _inputService?.ProcessInput();

        if (!CanProcessWorldInput())
        {
            HideCharacterHoverTooltip();
            return;
        }

        UpdateCharacterHoverTooltip();

        // Update path preview during movement phase (runs every frame, not just on click)
        UpdatePathPreview();

        // Update hover X marker during movement phase
        UpdateHoverMarker();

        // Update AoE preview during AoE targeting phase (runs every frame)
        if (CurrentSubPhase == PlayerSubPhase.SelectingAoETarget)
            UpdateAoEPreview();

        if (CurrentSubPhase == PlayerSubPhase.SelectingChargeTarget || CurrentSubPhase == PlayerSubPhase.ConfirmingChargePath)
            UpdateChargeHoverPreview();
    }

    private InputService.InputMode ResolveInputMode()
    {
        if (InventoryUI != null && InventoryUI.IsOpen && !InventoryUI.IsEmbedded)
            return InputService.InputMode.MenuOpen;

        if (SkillsUI != null && SkillsUI.IsOpen)
            return InputService.InputMode.MenuOpen;

        if (CharacterSheetUI != null && CharacterSheetUI.IsOpen)
            return InputService.InputMode.MenuOpen;

        switch (CurrentSubPhase)
        {
            case PlayerSubPhase.Moving:
            case PlayerSubPhase.TakingFiveFootStep:
            case PlayerSubPhase.Crawling:
                return InputService.InputMode.SelectingMovement;

            case PlayerSubPhase.SelectingAttackTarget:
            case PlayerSubPhase.SelectingSpecialTarget:
            case PlayerSubPhase.SelectingChargeTarget:
            case PlayerSubPhase.ConfirmingChargePath:
            case PlayerSubPhase.ConfirmingTurnUndead:
                return InputService.InputMode.SelectingTarget;

            case PlayerSubPhase.SelectingAoETarget:
            case PlayerSubPhase.ConfirmingSelfAoE:
                return InputService.InputMode.SelectingArea;

            case PlayerSubPhase.ChoosingAction:
                return InputService.InputMode.Normal;

            default:
                return InputService.InputMode.Normal;
        }
    }

    private bool CanProcessWorldInput()
    {
        if (!IsPlayerTurn)
            return false;

        if (CurrentSubPhase == PlayerSubPhase.Animating)
            return false;

        if (_waitingForAoOConfirmation)
            return false;

        if (InventoryUI != null && InventoryUI.IsOpen && !InventoryUI.IsEmbedded)
            return false;

        if (SkillsUI != null && SkillsUI.IsOpen)
            return false;

        if (CharacterSheetUI != null && CharacterSheetUI.IsOpen)
            return false;

        return true;
    }

    private bool HandleInputCancelRequested(InputService.InputClickContext context)
    {
        if (CurrentSubPhase == PlayerSubPhase.Moving)
        {
            CancelMovementSelection();
            return true;
        }

        if (CurrentSubPhase == PlayerSubPhase.TakingFiveFootStep)
        {
            CancelFiveFootStepSelection();
            return true;
        }

        if (CurrentSubPhase == PlayerSubPhase.Crawling)
        {
            CancelCrawlSelection();
            return true;
        }

        if (CurrentSubPhase == PlayerSubPhase.ConfirmingSelfAoE)
        {
            OnSelfAoECancelled();
            return true;
        }

        if (CurrentSubPhase == PlayerSubPhase.SelectingAoETarget && _isAoETargeting)
        {
            CancelAoETargeting();
            return true;
        }

        if (CurrentSubPhase == PlayerSubPhase.ConfirmingTurnUndead)
        {
            CancelTurnUndeadTargeting();
            return true;
        }

        if (CurrentSubPhase == PlayerSubPhase.SelectingSpecialTarget)
        {
            CancelSpecialAttackTargeting();
            return true;
        }

        if (CurrentSubPhase == PlayerSubPhase.SelectingChargeTarget || CurrentSubPhase == PlayerSubPhase.ConfirmingChargePath)
        {
            CancelChargeTargeting();
            return true;
        }

        if (CurrentSubPhase == PlayerSubPhase.SelectingAttackTarget)
        {
            if (_pendingAttackMode == PendingAttackMode.CastSpell)
                CancelSpellTargeting();
            else
                CancelPendingAttackTargeting();

            return true;
        }

        return false;
    }

    private bool HandleInputSecondaryClick(InputService.InputClickContext context)
    {
        if (CurrentSubPhase != PlayerSubPhase.ChoosingAction)
            return false;

        return TryHandleSummonRightClick(context.ScreenPosition);
    }

    private bool HandleInputModeLeftClick(InputService.InputClickContext context)
    {
        if (CurrentSubPhase == PlayerSubPhase.ConfirmingSelfAoE)
        {
            OnSelfAoEConfirmed();
            return true;
        }

        if (context.IsPointerOverUI && ShouldAllowGridClickThroughUIBlock())
        {
            Debug.Log("[Grid] Pointer reports UI overlap, but allowing click-through for off-hand target selection.");
        }

        SquareCell cell = context.GetSquareCell();
        if (cell != null)
        {
            Debug.Log($"[Grid] Raycast hit cell at ({cell.X}, {cell.Y}) Phase={CurrentPhase} Sub={CurrentSubPhase}");
            OnCellClicked(cell);
        }
        else
        {
            Debug.Log("[Grid] Click detected but no cell hit by raycast");
        }

        return true;
    }

    private bool ShouldAllowGridClickThroughUIBlock()
    {
        if (CurrentSubPhase == PlayerSubPhase.ConfirmingSelfAoE)
            return true;

        return CurrentSubPhase == PlayerSubPhase.SelectingAttackTarget
            && _isSelectingOffHandTarget;
    }

    private bool TryHandleSummonRightClick(Vector3 mouseScreenPos)
    {
        if (_mainCam == null)
            return false;

        if (_inputService != null && _inputService.IsPointerOverUI())
            return false;

        Vector2 worldPoint = _mainCam.ScreenToWorldPoint(mouseScreenPos);
        RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero);
        if (hit.collider == null)
            return false;

        SquareCell cell = hit.collider.GetComponent<SquareCell>();
        if (cell == null || !cell.IsOccupied || cell.Occupant == null)
            return false;

        CharacterController summon = cell.Occupant;
        if (!IsSummonedCreature(summon))
            return false;

        // Only allow player-owned summon commands during player turns.
        if (!summon.IsPlayerControlled)
            return false;

        ActiveSummonInstance active = GetActiveSummon(summon);
        if (active == null)
            return false;

        CombatUI?.ShowSummonContextMenu(
            summon,
            active.RemainingRounds,
            active.TotalDurationRounds,
            active.CurrentCommand,
            () => SetSummonCommand(summon, SummonCommand.AttackNearest()),
            () => SetSummonCommand(summon, SummonCommand.ProtectCaster()),
            () => RequestDismissSummon(summon));

        return true;
    }

    private void HandleInventoryInput()
    {
        if (InventoryUI == null || InventoryUI.IsEmbedded)
            return;

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

    private void HandleSkillsInput()
    {
        if (SkillsUI == null)
            return;

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

    private void HandleCharacterSheetInput()
    {
        if (CharacterSheetUI == null)
            return;

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

    private void CloseInventoryIfOpen()
    {
        if (InventoryUI != null && InventoryUI.IsOpen && !InventoryUI.IsEmbedded)
            InventoryUI.Close();
        if (CharacterSheetUI != null && CharacterSheetUI.IsOpen)
            CharacterSheetUI.Close();
    }

    private void ConfigureGrappleTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats fighterStats = new CharacterStats(
            name: "Grapple Tester",
            level: 6,
            characterClass: "Fighter",
            str: 17, dex: 12, con: 14, wis: 10, intelligence: 10, cha: 10,
            bab: 6,
            armorBonus: 4,
            shieldBonus: 1,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 45,
            raceName: "Human"
        );

        fighterStats.CharacterAlignment = Alignment.LawfulNeutral;

        Vector2Int fighterStart = new Vector2Int(9, 9);
        Sprite fighterAlive = IconLoader.GetToken("Fighter") ?? pcAliveFallback;
        PC1.Init(fighterStats, fighterStart, fighterAlive, pcDead);

        var fighterInventory = PC1.gameObject.GetComponent<InventoryComponent>();
        if (fighterInventory == null)
            fighterInventory = PC1.gameObject.AddComponent<InventoryComponent>();
        fighterInventory.Init(fighterStats);
        SetupStartingEquipment(fighterInventory, "Fighter");

        // Grapple test loadout: equip a greatsword for two-handed weapon grapple validation.
        fighterInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("greatsword"), EquipSlot.RightHand);

        // Greatsword is two-handed; explicitly clear off-hand to avoid stale setup state.
        fighterInventory.CharacterInventory.LeftHandSlot = null;

        fighterInventory.CharacterInventory.RecalculateStats();

        // Keep only one player combatant active for a focused grapple scenario.
        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, false, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("🧪 Grapple Test: Fighter and target start adjacent. Use Special Attack -> Grapple.");
    }

    private void ConfigureFeintSneakTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats rogueStats = new CharacterStats(
            name: "Shadow",
            level: 6,
            characterClass: "Rogue",
            str: 10, dex: 18, con: 14, wis: 10, intelligence: 12, cha: 14,
            bab: 4,
            armorBonus: 3,
            shieldBonus: 0,
            damageDice: 6,
            damageCount: 1,
            bonusDamage: 1,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 36,
            raceName: "Human"
        );

        rogueStats.CharacterAlignment = Alignment.ChaoticNeutral;
        rogueStats.InitFeats();
        rogueStats.AddFeats(new List<string> { "Weapon Finesse", "Combat Expertise", "Improved Feint", "Dodge" });

        rogueStats.InitializeSkills("Rogue", 6);
        for (int i = 0; i < 9; i++) rogueStats.AddSkillRank("Bluff");
        for (int i = 0; i < 9; i++) rogueStats.AddSkillRank("Hide");
        for (int i = 0; i < 9; i++) rogueStats.AddSkillRank("Move Silently");
        for (int i = 0; i < 9; i++) rogueStats.AddSkillRank("Tumble");
        for (int i = 0; i < 2; i++) rogueStats.AddSkillRank("Sense Motive");

        Vector2Int rogueStart = new Vector2Int(9, 9);
        Sprite rogueAlive = IconLoader.GetToken("Rogue") ?? pcAliveFallback;
        PC1.Init(rogueStats, rogueStart, rogueAlive, pcDead);

        InventoryComponent rogueInventory = PC1.gameObject.GetComponent<InventoryComponent>();
        if (rogueInventory == null)
            rogueInventory = PC1.gameObject.AddComponent<InventoryComponent>();
        rogueInventory.Init(rogueStats);

        rogueInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("rapier"), EquipSlot.RightHand);
        rogueInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("dagger"), EquipSlot.LeftHand);
        rogueInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("studded_leather"), EquipSlot.Armor);

        rogueInventory.CharacterInventory.RecalculateStats();

        // Focus this scenario on one rogue actor.
        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, false, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("🗡️ Feint Test: Shadow (Rogue 6) starts adjacent to a goblin. Use Special Attack -> Feint, then attack for sneak damage.");
    }

    private void ConfigureTurnUndeadTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats clericStats = new CharacterStats(
            name: "Brother Marcus",
            level: 6,
            characterClass: "Cleric",
            str: 12, dex: 10, con: 14, wis: 16, intelligence: 10, cha: 16,
            bab: 4,
            armorBonus: 4,
            shieldBonus: 2,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 1,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 42,
            raceName: "Human"
        );

        clericStats.CharacterAlignment = Alignment.LawfulGood;

        Vector2Int clericStart = new Vector2Int(9, 9);
        Sprite clericAlive = IconLoader.GetToken("Cleric") ?? pcAliveFallback;
        PC1.Init(clericStats, clericStart, clericAlive, pcDead);

        InventoryComponent clericInventory = PC1.gameObject.GetComponent<InventoryComponent>();
        if (clericInventory == null)
            clericInventory = PC1.gameObject.AddComponent<InventoryComponent>();
        clericInventory.Init(clericStats);

        // Turn Undead test loadout:
        // - Light crossbow equipped for ranged validation
        // - Heavy mace in inventory as melee backup
        // - 20 bolts as a placeholder ammo bundle (display/logging)
        ItemData lightCrossbow = ItemDatabase.CloneItem("crossbow_light");
        if (lightCrossbow != null)
            clericInventory.CharacterInventory.DirectEquip(lightCrossbow, EquipSlot.RightHand);

        ItemData heavyMace = ItemDatabase.CloneItem("mace_heavy");
        if (heavyMace != null)
            clericInventory.CharacterInventory.AddItem(heavyMace);

        ItemData crossbowBolts = ItemDatabase.CloneItem("crossbow_bolts_20");
        if (crossbowBolts != null)
            clericInventory.CharacterInventory.AddItem(crossbowBolts);

        clericInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("chainmail"), EquipSlot.Armor);
        clericInventory.CharacterInventory.RecalculateStats();

        CharacterStats fighterStats = new CharacterStats(
            name: "Gareth",
            level: 6,
            characterClass: "Fighter",
            str: 18, dex: 14, con: 16, wis: 12, intelligence: 10, cha: 8,
            bab: 6,
            armorBonus: 5,
            shieldBonus: 2,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 5,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 58,
            raceName: "Human"
        );

        fighterStats.CharacterAlignment = Alignment.LawfulGood;

        Vector2Int fighterStart = new Vector2Int(9, 7); // 10 feet south of Brother Marcus.
        Sprite fighterAlive = IconLoader.GetToken("Fighter") ?? pcAliveFallback;
        PC2.Init(fighterStats, fighterStart, fighterAlive, pcDead);

        InventoryComponent fighterInventory = PC2.gameObject.GetComponent<InventoryComponent>();
        if (fighterInventory == null)
            fighterInventory = PC2.gameObject.AddComponent<InventoryComponent>();
        fighterInventory.Init(fighterStats);

        ItemData longsword = ItemDatabase.CloneItem("longsword");
        if (longsword != null)
            fighterInventory.CharacterInventory.DirectEquip(longsword, EquipSlot.RightHand);

        ItemData chainmail = ItemDatabase.CloneItem("chainmail");
        if (chainmail != null)
            fighterInventory.CharacterInventory.DirectEquip(chainmail, EquipSlot.Armor);

        ItemData heavyShield = ItemDatabase.CloneItem("shield_heavy_steel")
            ?? ItemDatabase.CloneItem("shield_heavy_wooden")
            ?? ItemDatabase.CloneItem("shield_light_wooden");
        if (heavyShield != null)
            fighterInventory.CharacterInventory.DirectEquip(heavyShield, EquipSlot.LeftHand);

        fighterInventory.CharacterInventory.RecalculateStats();

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, true, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("✝️ Turn Undead Test (Expanded): Brother Marcus (Cleric 6) + Gareth (Fighter 6) vs 12 skeletons + 3 wights (24 HD total).");
        CombatUI?.ShowCombatLog("   Turn HD pool at L6 cleric + CHA 16 averages ~15 (range 10-20), so the HD selection menu should appear consistently.");
        CombatUI?.ShowCombatLog("   Goals: validate HD pool target selection, destruction vs turning choices, and that fighter attacks do NOT break Turn Undead.");
    }

    private void ConfigureArmorTargetingTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats wizardStats = new CharacterStats(
            name: "Aria",
            level: 5,
            characterClass: "Wizard",
            str: 8, dex: 14, con: 12, wis: 13, intelligence: 18, cha: 10,
            bab: 2,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 4,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 24,
            raceName: "Human"
        );

        CharacterStats rogueStats = new CharacterStats(
            name: "Shade",
            level: 5,
            characterClass: "Rogue",
            str: 12, dex: 18, con: 14, wis: 10, intelligence: 13, cha: 12,
            bab: 3,
            armorBonus: 2,
            shieldBonus: 0,
            damageDice: 6,
            damageCount: 1,
            bonusDamage: 1,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 34,
            raceName: "Human"
        );

        CharacterStats fighterStats = new CharacterStats(
            name: "Brom",
            level: 5,
            characterClass: "Fighter",
            str: 18, dex: 12, con: 16, wis: 12, intelligence: 10, cha: 8,
            bab: 5,
            armorBonus: 8,
            shieldBonus: 2,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 4,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 52,
            raceName: "Human"
        );

        PC1.Init(wizardStats, new Vector2Int(6, 8), IconLoader.GetToken("Wizard") ?? pcAliveFallback, pcDead);
        PC2.Init(rogueStats, new Vector2Int(8, 8), IconLoader.GetToken("Rogue") ?? pcAliveFallback, pcDead);
        PC3.Init(fighterStats, new Vector2Int(10, 8), IconLoader.GetToken("Fighter") ?? pcAliveFallback, pcDead);

        InventoryComponent wizardInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        wizardInventory.Init(wizardStats);
        wizardInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("quarterstaff"), EquipSlot.RightHand);
        wizardInventory.CharacterInventory.RecalculateStats();

        InventoryComponent rogueInventory = PC2.gameObject.GetComponent<InventoryComponent>() ?? PC2.gameObject.AddComponent<InventoryComponent>();
        rogueInventory.Init(rogueStats);
        rogueInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("leather_armor"), EquipSlot.Armor);
        rogueInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("short_sword"), EquipSlot.RightHand);
        rogueInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("short_sword"), EquipSlot.LeftHand);
        rogueInventory.CharacterInventory.RecalculateStats();

        InventoryComponent fighterInventory = PC3.gameObject.GetComponent<InventoryComponent>() ?? PC3.gameObject.AddComponent<InventoryComponent>();
        fighterInventory.Init(fighterStats);
        fighterInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("full_plate"), EquipSlot.Armor);
        fighterInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("longsword"), EquipSlot.RightHand);
        fighterInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("shield_heavy_steel"), EquipSlot.LeftHand);
        fighterInventory.CharacterInventory.RecalculateStats();

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, true, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, true, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        PC1.DebugPrintTags();
        PC2.DebugPrintTags();
        PC3.DebugPrintTags();

        CombatUI?.ShowCombatLog("🏹 Armor Targeting Test: Skeleton archers prioritize Unarmored > Light > Medium > Heavy when targets are in range.");
        CombatUI?.ShowCombatLog($"   {PC1.Stats.CharacterName}: {PC1.GetArmorTag()} | {PC2.Stats.CharacterName}: {PC2.GetArmorTag()} | {PC3.Stats.CharacterName}: {PC3.GetArmorTag()}");
    }

    private void RestoreStandardPartyLayout()
    {
        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, true, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, true, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, true, CombatUI != null ? CombatUI.PC4Panel : null);
    }

    private static void SetPCActiveState(CharacterController pc, bool active, GameObject panel)
    {
        if (pc != null && pc.gameObject != null)
            pc.gameObject.SetActive(active);

        if (panel != null)
            panel.SetActive(active);
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
        // Enemy encounter setup is deferred until the player selects a preset.

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

    /// <summary>Set enemy icons for all active NPC slots based on encounter definitions.</summary>
    private void SetupNPCIcons()
    {
        if (CombatUI == null) return;

        for (int i = 0; i < NPCs.Count; i++)
        {
            if (i < _activeEncounterEnemyIds.Count)
            {
                Sprite icon = IconManager.GetEnemyIcon(_activeEncounterEnemyIds[i]);
                if (icon != null)
                    CombatUI.SetNPCIcon(i, icon);
            }
            else
            {
                CombatUI.SetNPCIcon(i, null);
            }
        }
    }

    // ========== ENEMY ENCOUNTER SETUP ==========

    private static readonly Vector2Int[] EncounterSpawnPositions = {
        new Vector2Int(16, 6),
        new Vector2Int(14, 10),
        new Vector2Int(16, 14),
        new Vector2Int(13, 8),
        new Vector2Int(13, 12),
    };

    private static readonly Vector2Int[] TurnUndeadTestSpawnPositions = {
        // Front line (6 skeletons) - ~15 ft from cleric start (9,9)
        new Vector2Int(12, 6),
        new Vector2Int(12, 7),
        new Vector2Int(12, 8),
        new Vector2Int(12, 9),
        new Vector2Int(12, 10),
        new Vector2Int(12, 11),

        // Mid line (3 wights) - ~30 ft from cleric start (9,9)
        new Vector2Int(15, 7),
        new Vector2Int(15, 9),
        new Vector2Int(15, 11),

        // Back line (6 skeletons) - ~40 ft from cleric start (9,9)
        new Vector2Int(17, 6),
        new Vector2Int(17, 7),
        new Vector2Int(17, 8),
        new Vector2Int(17, 9),
        new Vector2Int(17, 10),
        new Vector2Int(17, 11),
    };

    private static readonly Vector2Int[] ArmorTargetingTestSpawnPositions = {
        new Vector2Int(7, 15),
        new Vector2Int(9, 15),
    };

    private void SetupEnemyEncounter(List<string> enemyIds)
    {
        EnemyDatabase.Init();
        ItemDatabase.Init();

        _npcAIBehaviors.Clear();
        _activeTurnUndeadTrackers.Clear();

        Sprite npcAliveFallback = LoadSprite("Sprites/npc_enemy_alive");
        Sprite npcDead = LoadSprite("Sprites/npc_enemy_dead");

        int spawnCount = enemyIds != null ? Mathf.Min(enemyIds.Count, NPCs.Count) : 0;

        for (int i = 0; i < NPCs.Count; i++)
        {
            CharacterController npc = NPCs[i];
            if (npc == null) continue;

            if (i >= spawnCount)
            {
                npc.gameObject.SetActive(false);
                if (CombatUI != null && i < CombatUI.NPCPanels.Count && CombatUI.NPCPanels[i].Panel != null)
                    CombatUI.NPCPanels[i].Panel.SetActive(false);
                continue;
            }

            string enemyId = enemyIds[i];
            EnemyDefinition def = EnemyDatabase.Get(enemyId);
            if (def == null)
            {
                Debug.LogError($"[GameManager] Unknown enemy ID: {enemyId}");
                npc.gameObject.SetActive(false);
                continue;
            }

            npc.gameObject.SetActive(true);
            if (CombatUI != null && i < CombatUI.NPCPanels.Count && CombatUI.NPCPanels[i].Panel != null)
                CombatUI.NPCPanels[i].Panel.SetActive(true);

            Vector2Int pos;
            if ((_isGrappleTestEncounter || _isFeintSneakTestEncounter) && i == 0 && PC1 != null)
            {
                // Spawn adjacent in dedicated mechanics test encounters.
                pos = PC1.GridPosition + Vector2Int.right;
            }
            else if (_isTurnUndeadTestEncounter && i < TurnUndeadTestSpawnPositions.Length)
            {
                // Explicit 15-undead test formation (front skeletons, mid wights, back skeletons).
                pos = TurnUndeadTestSpawnPositions[i];
            }
            else if (_isArmorTargetingTestEncounter && i < ArmorTargetingTestSpawnPositions.Length)
            {
                // Position skeleton archers at range so armor-priority targeting is easy to observe.
                pos = ArmorTargetingTestSpawnPositions[i];
            }
            else
            {
                pos = (i < EncounterSpawnPositions.Length)
                    ? EncounterSpawnPositions[i]
                    : new Vector2Int(15 + i, 10);
            }

            // Try class-specific monster token; fallback to generic NPC sprite
            string monsterType = IconLoader.DetermineMonsterType(def.Name);
            Sprite npcAlive = null;
            if (!string.IsNullOrEmpty(monsterType))
                npcAlive = IconLoader.GetToken(monsterType);
            if (npcAlive == null)
                npcAlive = npcAliveFallback;

            InitializeNPCFromDefinition(npc, def, pos, npcAlive, npcDead);
            _npcAIBehaviors.Add(def.AIBehavior);

            if (_isArmorTargetingTestEncounter && string.Equals(enemyId, "skeleton_archer", StringComparison.Ordinal))
            {
                npc.Tags.AddTag("Uses Armor-Based Targeting");
                Debug.Log($"[ArmorTargetingTest] Enabled armor-priority AI for {npc.Stats.CharacterName}");
            }

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

            Debug.Log($"[GameManager] Spawned NPC {i}: {def.Name} (Lv {def.Level} {def.CharacterClass}) at ({pos.x},{pos.y}) — AI: {def.AIBehavior}");
        }

        // Legacy NPC field points to first active enemy
        NPC = null;
        for (int i = 0; i < NPCs.Count; i++)
        {
            if (NPCs[i] != null && NPCs[i].gameObject.activeSelf)
            {
                NPC = NPCs[i];
                break;
            }
        }

        // Hide legacy single-NPC panel since we're using multi-panels
        if (CombatUI.NPCNameText != null)
            CombatUI.NPCNameText.transform.parent.gameObject.SetActive(false);
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
        stats.CreatureType = string.IsNullOrEmpty(def.CreatureType) ? "Humanoid" : def.CreatureType;
        stats.SetBaseSizeCategory(def.SizeCategory);
        stats.IsTallCreature = def.IsTallCreature;
        stats.NaturalArmorBonus = def.NaturalArmorBonus;
        stats.HasTripAttack = def.HasTripAttack;
        stats.TripAttackCheckBonus = def.TripAttackCheckBonus;

        // Ensure size-derived natural reach is respected for creatures larger than Medium.
        if (stats.AttackRange < stats.NaturalReachSquares)
            stats.AttackRange = stats.NaturalReachSquares;

        // Apply innate mitigation profile from enemy definition
        if (def.DamageReductionAmount > 0)
            stats.AddDamageReduction(def.DamageReductionAmount, def.DamageReductionBypass, def.DamageReductionRangedOnly);

        if (def.DamageResistances != null)
        {
            foreach (var res in def.DamageResistances)
            {
                if (res != null && res.Amount > 0)
                    stats.AddDamageResistance(res.Type, res.Amount);
            }
        }

        if (def.DamageImmunities != null)
        {
            foreach (var imm in def.DamageImmunities)
                stats.AddDamageImmunity(imm);
        }

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

        npc.aiProfile = BuildRuntimeAIProfile(def);

        Debug.Log($"[GameManager] {def.Name}: HP {stats.MaxHP} AC {stats.ArmorClass} " +
                  $"Atk {CharacterStats.FormatMod(stats.AttackBonus)} Speed {stats.MoveRange}sq");
    }

    private DND35.AI.AIProfile BuildRuntimeAIProfile(EnemyDefinition def)
    {
        if (def == null)
            return null;

        EnemyAIProfileArchetype archetype = def.AIProfileArchetype;

        // Legacy fallback for old definitions that don't explicitly set an archetype.
        if (archetype == EnemyAIProfileArchetype.None
            && string.Equals(def.CreatureType, "Animal", StringComparison.OrdinalIgnoreCase))
        {
            archetype = EnemyAIProfileArchetype.Animal;
        }

        switch (archetype)
        {
            case EnemyAIProfileArchetype.Animal:
                return ScriptableObject.CreateInstance<AnimalAIProfile>();
            case EnemyAIProfileArchetype.Berserk:
                return ScriptableObject.CreateInstance<BerserkAIProfile>();
            case EnemyAIProfileArchetype.Grappler:
                return ScriptableObject.CreateInstance<GrapplerAIProfile>();
            case EnemyAIProfileArchetype.Ranged:
                return ScriptableObject.CreateInstance<RangedAIProfile>();
            case EnemyAIProfileArchetype.Healer:
                return ScriptableObject.CreateInstance<HealerAIProfile>();
            case EnemyAIProfileArchetype.Spellcaster:
                return ScriptableObject.CreateInstance<SpellcasterAIProfile>();
            case EnemyAIProfileArchetype.Evoker:
                return ScriptableObject.CreateInstance<EvokerAIProfile>();
            case EnemyAIProfileArchetype.Abjurer:
                return ScriptableObject.CreateInstance<AbjurerAIProfile>();
            case EnemyAIProfileArchetype.Necromancer:
                return ScriptableObject.CreateInstance<NecromancerAIProfile>();
            default:
                return null;
        }
    }

    /// <summary>Helper to update all stat UI panels using 4-PC multi-NPC system.</summary>
    private void UpdateAllStatsUI()
    {
        RefreshFlankedConditions();

        if (CombatUI != null)
            CombatUI.UpdateAllStats4PC(PCs, NPCs);

        CharacterSheetUI?.RefreshIfOpen();
    }

    /// <summary>
    /// Keep Flanked condition badges in sync with current battlefield positions.
    /// </summary>
    private void RefreshFlankedConditions()
    {
        _conditionService?.RefreshFlankedConditions(GetAllCharacters());
    }

    private void HandleConditionExpired(CharacterController character, ConditionService.ActiveCondition condition)
    {
        if (character == null || character.Stats == null || condition == null)
            return;

        CombatConditionType normalizedType = ConditionRules.Normalize(condition.Type);
        if (normalizedType == CombatConditionType.Turned)
            _activeTurnUndeadTrackers.Remove(character);

        string conditionLabel = condition.Type.ToString();
        string msg = $"⏱ {character.Stats.CharacterName} is no longer {conditionLabel}.";
        Debug.Log($"[Condition] {msg}");
        CombatUI?.ShowCombatLog($"<color=#99CCFF>{msg}</color>");
    }

    private bool IsActiveCombatant(CharacterController c)
    {
        return c != null && c.gameObject != null && c.gameObject.activeInHierarchy && c.Stats != null;
    }

    /// <summary>Check if all hostile (enemy-team) combatants are dead.</summary>
    private bool AreAllNPCsDead()
    {
        foreach (var npc in NPCs)
        {
            if (!IsActiveCombatant(npc)) continue;
            if (npc.IsPlayerControlled) continue; // allied summons should not block victory
            if (!npc.Stats.IsDead) return false;
        }
        return true;
    }

    /// <summary>Check if all active PCs in the party are dead.</summary>
    private bool AreAllPCsDead()
    {
        foreach (var pc in PCs)
        {
            if (!IsActiveCombatant(pc)) continue;
            if (!pc.Stats.IsDead) return false;
        }

        // If no active PCs remain, treat the party as defeated.
        return true;
    }

    /// <summary>Count remaining alive hostile (enemy-team) NPCs.</summary>
    private int GetAliveNPCCount()
    {
        int count = 0;
        foreach (var npc in NPCs)
        {
            if (!IsActiveCombatant(npc)) continue;
            if (npc.IsPlayerControlled) continue;
            if (!npc.Stats.IsDead) count++;
        }
        return count;
    }

    /// <summary>Get first alive hostile NPC (for backward compat in single-target scenarios).</summary>
    private CharacterController GetFirstAliveNPC()
    {
        foreach (var npc in NPCs)
        {
            if (!IsActiveCombatant(npc) || npc.Stats.IsDead) continue;
            if (npc.IsPlayerControlled) continue;
            return npc;
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
        var activePCs = new List<CharacterController>();
        foreach (var pc in PCs)
        {
            if (IsActiveCombatant(pc) && !pc.Stats.IsDead)
                activePCs.Add(pc);
        }

        var activeNPCs = new List<CharacterController>();
        foreach (var npc in NPCs)
        {
            if (IsActiveCombatant(npc) && !npc.Stats.IsDead)
                activeNPCs.Add(npc);
        }

        _turnService?.StartCombat(activePCs, activeNPCs, IsPC);

        string orderStr = _turnService != null ? _turnService.GetInitiativeOrderString() : "No combatants";
        Debug.Log($"[Initiative] Combat begins! Initiative order:\n{orderStr}");

        UpdateInitiativeUI();
    }

    /// <summary>Update the initiative panel in the UI.</summary>
    private void UpdateInitiativeUI()
    {
        if (CombatUI == null)
            return;

        string display = _turnService != null ? _turnService.GetInitiativeDisplayString() : string.Empty;
        CombatUI.UpdateInitiativeDisplay(display);
    }

    private void OnTurnStarted(CharacterController character)
    {
        if (CurrentPhase == TurnPhase.CombatOver || character == null)
            return;

        if (IsPC(character))
            StartPCTurn(character);
        else
            StartCoroutine(SingleNPCTurnFromInitiative(character));
    }

    private void OnNewRound(int round)
    {
        Debug.Log($"[GameManager] ═══ ROUND {round} BEGINS ═══");
        CombatUI.AddTurnSeparator(round);
        ResetQuickenedSpellTrackingForAllCharacters();
        ResetAttackDamageModesForAllCharacters();

        // Tick all spell + condition effect durations at the start of each new round.
        TickAllSpellDurations();
        _conditionService?.OnRoundEnd();

        // Tick summon durations (Summon Monster: 1 round/level)
        TickSummonDurations();

        // Keep Turn Undead tracker table aligned with condition expiration.
        PruneTurnUndeadTrackers();
        LogOngoingTurnUndeadStatusAtRoundStart();
    }

    private void OnCombatEnded()
    {
        CurrentPhase = TurnPhase.CombatOver;
        _conditionService?.CleanupOnCombatEnd(GetAllCharacters());
        CombatUI.SetTurnIndicator("Combat has ended.");
        CombatUI.SetActionButtonsVisible(false);
        UpdateInitiativeUI();
    }

    private void ProcessEndOfTurnHPState(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return;

        character.ProcessEndOfTurnHPState();

        if (character.CurrentHPState == HPState.Dead)
        {
            _conditionService?.CleanupOnDeath(character);
            HandleSummonDeathCleanup(character);
        }

        UpdateAllStatsUI();
    }

    private bool ShouldSkipTurnDueToHPState(CharacterController character)
    {
        return character == null || character.Stats == null || !character.CanTakeTurnActions();
    }
    /// <summary>Move to the next initiative slot and start that turn.</summary>
    private void NextInitiativeTurn()
    {
        CharacterController endingCharacter = CurrentCharacter;
        endingCharacter?.ProcessPinnedDurationAtTurnEnd();
        _conditionService?.OnTurnEnd(endingCharacter);
        ProcessEndOfTurnHPState(endingCharacter);

        // Threat map may have changed (NPC moved, character died, etc.)
        InvalidatePreviewThreats();

        _turnService?.EndTurn();
        UpdateInitiativeUI();
    }

    // ========== TURN MANAGEMENT WITH ACTION ECONOMY ==========

    /// <summary>
    /// Begin a PC's turn with full action economy.
    /// </summary>
    public void StartPCTurn(CharacterController pc)
    {
        if (CurrentPhase == TurnPhase.CombatOver) return;

        _conditionService?.OnTurnStart(pc);
        CloseInventoryIfOpen();

        // Tick Aid Another expiry counters before actions; this keeps bonuses available for one full beneficiary turn.
        ExpireAidBonusesAtTurnStart(pc);

        // If this PC is unconscious/dead, skip their actions.
        if (ShouldSkipTurnDueToHPState(pc))
        {
            if (pc != null && pc.Stats != null)
            {
                string reason = pc.CurrentHPState == HPState.Dead
                    ? "is dead"
                    : "is unconscious";
                CombatUI?.ShowCombatLog($"⏭ {pc.Stats.CharacterName} {reason} and cannot act this turn.");
            }

            NextInitiativeTurn();
            return;
        }

        // Reset off-hand attack state for this turn.
        _offHandAttackUsedThisTurn = false;
        _offHandAttackAvailableThisTurn = pc.HasOffHandWeaponEquipped();
        _isSelectingOffHandTarget = false;
        _isSelectingOffHandThrownTarget = false;
        _currentOffHandBAB = 0;
        _currentOffHandWeapon = null;

        Debug.Log("=== TURN START ===");
        Debug.Log($"[Turn] Character: {pc.Stats.CharacterName}");
        Debug.Log($"[OffHand] _offHandAttackAvailableThisTurn: {_offHandAttackAvailableThisTurn}");
        Debug.Log($"[OffHand] _offHandAttackUsedThisTurn: {_offHandAttackUsedThisTurn}");

        // Reset dual-wielding prompt state for this turn.
        _dualWieldingChoiceMade = false;
        _isDualWielding = false;
        _mainHandPenalty = 0;
        _offHandPenalty = 0;
        _pendingAttackType = AttackType.Melee;
        _pendingDisarmUseOffHandSelection = false;
        _pendingSunderUseOffHandSelection = false;

        Debug.Log($"[Turn][OffHand] Flags reset for {pc.Stats.CharacterName}: available={_offHandAttackAvailableThisTurn}, used={_offHandAttackUsedThisTurn}");
        Debug.Log($"[Turn][DualWield] Turn start reset: choiceMade={_dualWieldingChoiceMade}, isDualWielding={_isDualWielding}, mainPenalty={_mainHandPenalty}, offPenalty={_offHandPenalty}");

        // Log turn start in combat log
        CombatUI.ShowCombatLog($"<color=#FFD700>⚔ {pc.Stats.CharacterName}'s turn begins</color>");

        if (pc.IsGrappling())
            CombatUI?.ShowCombatLog("🪢 You are grappling — only grapple actions are available (spellcasting allowed with concentration and component restrictions).");
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

        EndAttackSequence();
        EndThrownAttackSequence();
        pc.StartNewTurn();

        PruneTurnUndeadTrackers();
        CheckTurnUndeadProximityBreakingForCleric(pc);

        _loggedHeldChargeNoActionsReminder = false;

        CurrentPhase = TurnPhase.PCTurn;
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
        if (_turnService == null || !_turnService.HasInitiativeEntries())
            StartCombat();
        else
            _turnService.StartTurnAtCurrentIndex();
    }

    private void LogMenuFlow(string marker, CharacterController actor = null, string details = null)
    {
        string actorName = actor != null && actor.Stats != null ? actor.Stats.CharacterName : "<null>";
        bool menuOpen = CombatUI != null && CombatUI.IsSpecialStyleSelectionMenuOpen();
        string suffix = string.IsNullOrEmpty(details) ? string.Empty : $" | {details}";
        Debug.Log($"[GameManager][MenuFlow] {marker} | actor={actorName} | phase={CurrentPhase} | subPhase={CurrentSubPhase} | menuOpen={menuOpen} | frame={Time.frameCount}{suffix}\nStackTrace:\n{System.Environment.StackTrace}");
    }

    /// <summary>
    /// Show the action choice UI for the current PC.
    /// </summary>
    private void ShowActionChoices()
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;

        bool hasThrowableWeapon = pc.HasThrowableWeaponEquipped();
        bool hasOffHandWeapon = pc.HasOffHandWeaponEquipped();
        bool hasThrowableOffHandWeapon = pc.HasThrowableOffHandWeaponEquipped();

        // Keep simple off-hand gate synced with equipment presence.
        if (!hasOffHandWeapon)
            _offHandAttackAvailableThisTurn = false;

        bool hasMoreAttacks = _isInAttackSequence && _attackingCharacter == pc && HasMoreAttacksAvailable();
        bool offHandAvailable = IsOffHandAttackAvailable();

        Debug.Log("=== SHOW ACTION CHOICES ===");
        Debug.Log($"[OffHand] _offHandAttackAvailableThisTurn: {_offHandAttackAvailableThisTurn}");
        Debug.Log($"[OffHand] _offHandAttackUsedThisTurn: {_offHandAttackUsedThisTurn}");
        Debug.Log($"[OffHand] IsOffHandAttackAvailable(): {offHandAvailable}");
        Debug.Log($"[OffHand] HasOffHandWeaponEquipped(): {hasOffHandWeapon}");
        Debug.Log($"[OffHand] HasThrowableOffHandWeaponEquipped(): {hasThrowableOffHandWeapon}");
        Debug.Log($"[Actions] Showing choices for {pc.Stats.CharacterName}: hasThrowableWeapon={hasThrowableWeapon}, hasOffHandWeapon={hasOffHandWeapon}, hasThrowableOffHandWeapon={hasThrowableOffHandWeapon}, offHandAvailable={offHandAvailable}, offHandGate={_offHandAttackAvailableThisTurn}, inSequence={_isInAttackSequence}, hasMoreAttacks={hasMoreAttacks}, offHandUsed={_offHandAttackUsedThisTurn}, dwChoiceMade={_dualWieldingChoiceMade}, isDualWielding={_isDualWielding}, selectingOffHandThrown={_isSelectingOffHandThrownTarget}");

        LogMenuFlow("ShowActionChoices:ENTER", pc, $"isGrappling={pc.IsGrappling()}, isPinned={pc.IsPinned()}");

        // SAFETY CHECK: A delayed coroutine from a previous action can fire while a submenu is open.
        // In that case we must not refresh action choices or hide transient panels.
        bool isSubmenuOpen = CombatUI != null && CombatUI.IsSpecialStyleSelectionMenuOpen();
        if (isSubmenuOpen)
        {
            LogMenuFlow("ShowActionChoices:ABORT_SUBMENU_OPEN", pc, "Submenu is open; skipping action choice refresh");
            Debug.Log("[GameManager][MenuFlow] ABORT: Submenu is open, skipping ShowActionChoices");
            return;
        }

        CurrentSubPhase = PlayerSubPhase.ChoosingAction;
        _currentAttackType = GetDefaultAttackType(pc);
        _skipNextSingleAttackStandardActionCommit = false;
        EndGrappleContextMenuDisplayLock();
        CombatUI.HideSummonContextMenu();

        _waitingForAoOConfirmation = false;
        _pendingAoOAction = null;
        _isSelectingTurnUndead = false;
        _turnUndeadPendingInvoker = null;
        CloseTurnUndeadSelectionPanel(clearHighlights: true);
        _activeTurnUndeadSelectionContext = null;
        _spellcastProvocationCancelled = false;
        ClearSpellcastResourceSnapshot();
        ClearDisarmSequenceState();
        LogMenuFlow("ShowActionChoices:HIDE_TRANSIENT_PANELS", pc);
        ClearOverrunDestinationSelectionState();
        ClearOverrunContinuationState();
        CombatUI.HideAoOConfirmationPrompt();
        CombatUI.HideDisarmWeaponSelection();
        CombatUI.HideSpecialStyleSelectionMenu();
        CombatUI.HidePickUpItemSelection();
        CombatUI.HideDropEquippedItemSelection();
        // Hide movement path preview and hover marker when leaving movement phase
        if (_pathPreview != null) _pathPreview.HidePath();
        if (_hoverMarker != null) _hoverMarker.Hide();

        // Reset transient charge state whenever we return to action menu
        _chargeTarget = null;
        _pendingChargePath.Clear();
        _pendingChargeBullRush = false;

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        HighlightCharacterFootprint(pc, HighlightType.Selected);

        CombatUI.SetActionButtonsVisible(true);
        CombatUI.HideSpecialAttackMenu();
        CombatUI.UpdateActionButtons(pc);
        CombatUI.UpdateFeatControls(pc);

        string pcName = pc.Stats.CharacterName;
        string actionInfo = pc.Actions.GetStatusString();

        string weaponStateInfo = string.Empty;
        ItemData currentWeapon = pc.GetEquippedMainWeapon();
        if (currentWeapon != null)
        {
            weaponStateInfo = $"\nAttack Source: {currentWeapon.Name}";
            if (currentWeapon.RequiresReload)
                weaponStateInfo += $"\n{pc.GetWeaponLoadStateLabel(currentWeapon)}";
        }
        else
        {
            var unarmed = pc.GetUnarmedDamage();
            weaponStateInfo = $"\nAttack Source: Unarmed strike ({unarmed.damageCount}d{unarmed.damageDice})";
        }

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

        CombatUI.SetTurnIndicator($"{pcName}'s Turn - Choose an action  [I] Inventory  [K] Skills\n{actionInfo}{weaponStateInfo}{dwInfo}{featInfo}{spellInfo}");

        if (!pc.Actions.HasAnyActionLeft)
        {
            if (_isInAttackSequence && _attackingCharacter == pc)
            {
                if (HasMoreAttacksAvailable())
                {
                    int attacksRemaining = _totalAttackBudget - _totalAttacksUsed;
                    CombatUI.SetTurnIndicator($"{pcName}'s Turn - Iterative attacks remaining: {attacksRemaining} (next BAB {CharacterStats.FormatMod(_currentAttackBAB)}). Use Attack (Melee - Full Round) or Attack (Thrown - Full Round), or End Turn.");
                }
                else
                {
                    CombatUI.SetTurnIndicator($"{pcName}'s Turn - Iterative attack sequence complete. You may still use free actions/special toggles or End Turn.");
                }
            }
            else if (CanUseGrappleAttackOption(pc))
            {
                int attacksRemaining = GetRemainingGrappleAttackActions(pc);
                int nextBab = GetCurrentGrappleAttackBonus(pc);
                CombatUI.SetTurnIndicator($"{pcName}'s Turn - Grapple attacks remaining: {attacksRemaining} (next BAB {CharacterStats.FormatMod(nextBab)}). Use Special Attack → Grapple, or End Turn.");
            }
            else if (CanUseBullRushAttackOption(pc))
            {
                int attacksRemaining = GetRemainingBullRushAttackActions(pc);
                int nextBab = GetCurrentBullRushAttackBonus(pc);
                CombatUI.SetTurnIndicator($"{pcName}'s Turn - Bull Rush (Attack) attempts remaining: {attacksRemaining} (next BAB {CharacterStats.FormatMod(nextBab)}). Use Special Attack → Bull Rush (Attack), or End Turn.");
            }
            else if (CanUseTripAttackOption(pc))
            {
                int attacksRemaining = GetRemainingTripAttackActions(pc);
                int nextBab = GetCurrentTripAttackBonus(pc);
                CombatUI.SetTurnIndicator($"{pcName}'s Turn - Trip attempts remaining: {attacksRemaining} (next BAB {CharacterStats.FormatMod(nextBab)}). Use Special Attack → Trip, or End Turn.");
            }
            else if (CanUseDisarmAttackOption(pc))
            {
                int attacksRemaining = GetRemainingDisarmAttackActions(pc);
                int nextBab = GetCurrentDisarmAttackBonus(pc);
                CombatUI.SetTurnIndicator($"{pcName}'s Turn - Disarm-capable attacks remaining: {attacksRemaining} (next BAB {CharacterStats.FormatMod(nextBab)}). Use Special Attack → Disarm, or End Turn.");
            }
            else if (CanUseSunderAttackOption(pc))
            {
                int attacksRemaining = GetRemainingSunderAttackActions(pc);
                int nextBab = GetCurrentSunderAttackBonus(pc);
                CombatUI.SetTurnIndicator($"{pcName}'s Turn - Sunder-capable attacks remaining: {attacksRemaining} (next BAB {CharacterStats.FormatMod(nextBab)}). Use Special Attack → Sunder, or End Turn.");
            }
            else if (IsHoldingTouchCharge(pc))
            {
                string heldSpellName = GetHeldTouchSpellName(pc);
                CombatUI.SetTurnIndicator($"{pcName}'s Turn - No main actions left. You may still discharge {heldSpellName} (free action) or End Turn.");

                if (!_loggedHeldChargeNoActionsReminder)
                {
                    CombatUI.ShowCombatLog($"✋ {pcName} has no main actions left but is still holding {heldSpellName}. Discharging is a free action.");
                    _loggedHeldChargeNoActionsReminder = true;
                }
            }
            else
            {
                CombatUI.SetTurnIndicator($"{pcName}'s Turn - No actions remaining. Click End Turn when ready.");
                Debug.Log($"[TurnFlow] {pcName} has no main actions left; waiting for manual End Turn.");
            }
        }
        LogMenuFlow("ShowActionChoices:EXIT", pc, $"hasAnyActionLeft={pc.Actions.HasAnyActionLeft}");
    }

    private bool IsCombatEncounterRunning()
    {
        return _turnService != null && _turnService.HasInitiativeEntries() && CurrentPhase != TurnPhase.CombatOver;
    }

    /// <summary>
    /// Attempt to use a consumable item from inventory.
    /// In combat this uses D&D 3.5 item-manipulation timing (move action, or standard as alternative)
    /// and can provoke attacks of opportunity from adjacent enemies.
    /// </summary>
    public bool TryUseConsumableFromInventory(CharacterController actor, int inventoryIndex, out string feedback)
    {
        feedback = string.Empty;

        if (actor == null || actor.Stats == null)
        {
            feedback = "No active character.";
            return false;
        }

        var invComp = actor.GetComponent<InventoryComponent>();
        var inv = invComp != null ? invComp.CharacterInventory : null;
        if (inv == null)
        {
            feedback = $"{actor.Stats.CharacterName} has no inventory.";
            return false;
        }

        if (inventoryIndex < 0 || inventoryIndex >= inv.GeneralSlots.Length)
        {
            feedback = "Invalid inventory slot.";
            return false;
        }

        ItemData item = inv.GeneralSlots[inventoryIndex];
        if (item == null)
        {
            feedback = "Inventory slot is empty.";
            return false;
        }

        if (!item.IsConsumable)
        {
            feedback = $"{item.Name} is not a consumable item.";
            return false;
        }

        if (IsCombatEncounterRunning())
        {
            if (!IsPlayerTurn || ActivePC != actor)
            {
                feedback = "Only the active character can use consumables during combat.";
                return false;
            }

            if (CurrentSubPhase != PlayerSubPhase.ChoosingAction)
            {
                feedback = "Cannot use items right now.";
                return false;
            }

            if (_waitingForAoOConfirmation)
            {
                feedback = "Resolve the current attack-of-opportunity prompt first.";
                return false;
            }

            if (!CanUseItemManipulationAction(actor, out string actionReason))
            {
                feedback = actionReason;
                return false;
            }

            ResolveConsumableUseProvocation(actor, inventoryIndex, item);
            feedback = $"Using {item.Name}...";
            return true;
        }

        if (!ApplyConsumableEffectAndConsume(actor, inventoryIndex, out string outOfCombatResult))
        {
            feedback = outOfCombatResult;
            return false;
        }

        feedback = outOfCombatResult;
        UpdateAllStatsUI();
        return true;
    }

    private bool CanUseItemManipulationAction(CharacterController actor, out string reason)
    {
        reason = string.Empty;
        if (actor == null)
        {
            reason = "No active character.";
            return false;
        }

        if (actor.HasCondition(CombatConditionType.Pinned))
        {
            reason = "Pinned creatures cannot manipulate items; only grapple escape actions are allowed.";
            return false;
        }

        if (actor.Actions.HasMoveAction || actor.Actions.CanConvertStandardToMove || actor.Actions.HasStandardAction)
            return true;

        reason = "No move or standard action available to manipulate an item.";
        return false;
    }

    private void ConsumeItemManipulationAction(CharacterController actor)
    {
        if (actor == null) return;

        if (actor.Actions.HasMoveAction)
            actor.Actions.UseMoveAction();
        else if (actor.Actions.CanConvertStandardToMove)
            actor.Actions.ConvertStandardToMove();
        else if (actor.Actions.HasStandardAction)
            actor.Actions.UseStandardAction();
    }

    private void ResolveConsumableUseProvocation(CharacterController actor, int inventoryIndex, ItemData item)
    {
        var threateningEnemies = ThreatSystem.GetThreateningEnemies(actor.GridPosition, actor, GetAllCharacters());
        threateningEnemies.RemoveAll(enemy => enemy == null || enemy.Stats == null || enemy.Stats.IsDead || !ThreatSystem.CanMakeAoO(enemy));

        if (threateningEnemies.Count == 0)
        {
            if (ApplyConsumableEffectAndConsume(actor, inventoryIndex, out string noThreatMessage))
            {
                ConsumeItemManipulationAction(actor);
                CombatUI?.ShowCombatLog(noThreatMessage);
                UpdateAllStatsUI();
            }
            else
            {
                CombatUI?.ShowCombatLog($"⚠ {noThreatMessage}");
            }

            ShowActionChoices();
            return;
        }

        ShowAoOActionConfirmation(new AoOProvokingActionInfo
        {
            ActionType = AoOProvokingAction.DrinkPotion,
            ActionName = $"USE {item.Name.ToUpper()}",
            ActionDescription = $"Use {item.Name} (item manipulation)",
            Actor = actor,
            ThreateningEnemies = threateningEnemies,
            OnProceed = () => StartCoroutine(ResolveConsumableAoOsAndApply(actor, inventoryIndex, item, threateningEnemies)),
            OnCancel = ShowActionChoices
        });
    }

    private IEnumerator ResolveConsumableAoOsAndApply(CharacterController actor, int inventoryIndex, ItemData item, List<CharacterController> threateningEnemies)
    {
        if (actor == null || actor.Stats == null)
            yield break;

        CurrentSubPhase = PlayerSubPhase.Animating;
        CombatUI?.ShowCombatLog($"{actor.Stats.CharacterName} manipulates {item.Name} (provokes AoO).");

        foreach (var enemy in threateningEnemies)
        {
            if (actor.Stats.IsDead) break;
            if (enemy == null || enemy.Stats == null || enemy.Stats.IsDead || !ThreatSystem.CanMakeAoO(enemy))
                continue;

            CombatResult aooResult = ThreatSystem.ExecuteAoO(enemy, actor);
            if (aooResult == null) continue;

            CombatUI?.ShowCombatLog($"⚔ AoO vs item use: {aooResult.GetDetailedSummary()}");
            UpdateAllStatsUI();

            if (aooResult.Hit && aooResult.TotalDamage > 0)
                CheckConcentrationOnDamage(actor, aooResult.TotalDamage);

            yield return new WaitForSeconds(0.65f);
        }

        if (actor.Stats.IsDead)
        {
            CombatUI?.ShowCombatLog($"💀 {actor.Stats.CharacterName} is slain before using {item.Name}!");
            UpdateAllStatsUI();
            EndActivePCTurn();
            yield break;
        }

        if (ApplyConsumableEffectAndConsume(actor, inventoryIndex, out string resultMessage))
        {
            ConsumeItemManipulationAction(actor);
            CombatUI?.ShowCombatLog(resultMessage);
            UpdateAllStatsUI();
        }
        else
        {
            CombatUI?.ShowCombatLog($"⚠ {resultMessage}");
        }

        ShowActionChoices();
    }

    /// <summary>
    /// Drop one equipped held item into the actor's current square.
    /// D&D 3.5e: dropping a held item is a free action.
    /// </summary>
    public string GetDropEquippedItemDisabledReason(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return "No active character";

        Inventory inv = character.GetComponent<InventoryComponent>()?.CharacterInventory;
        if (inv == null)
            return "No inventory";

        if (character.HasCondition(CombatConditionType.Pinned))
            return "Pinned: only grapple escape actions allowed";

        if (inv.RightHandSlot == null && inv.LeftHandSlot == null)
            return "No held item";

        return string.Empty;
    }

    public void OnDropEquippedItemButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;

        if (RedirectPinnedCharacterToGrappleMenu(pc, "dropping equipped items"))
            return;

        string disabledReason = GetDropEquippedItemDisabledReason(pc);
        if (!string.IsNullOrEmpty(disabledReason))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot drop an equipped item: {disabledReason}.");
            return;
        }

        if (!TryGetHeldItemDropOptions(pc, out List<DropEquippedHeldItemOption> options))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} has no held item to drop.");
            ShowActionChoices();
            return;
        }

        if (options.Count == 1)
        {
            ResolveDropEquippedHeldItemFreeAction(pc, options[0].HandSlot);
            return;
        }

        if (CombatUI == null)
        {
            ResolveDropEquippedHeldItemFreeAction(pc, options[0].HandSlot);
            return;
        }

        List<string> optionLabels = new List<string>(options.Count);
        for (int i = 0; i < options.Count; i++)
        {
            optionLabels.Add(options[i].GetSelectionLabel());
        }

        CombatUI.ShowDropEquippedItemSelection(
            pc.Stats.CharacterName,
            optionLabels,
            onSelect: selectedIndex =>
            {
                if (selectedIndex < 0 || selectedIndex >= options.Count)
                {
                    ShowActionChoices();
                    return;
                }

                List<DropEquippedHeldItemOption> latestOptions = new List<DropEquippedHeldItemOption>();
                TryGetHeldItemDropOptions(pc, out latestOptions);

                EquipSlot selectedSlot = options[selectedIndex].HandSlot;
                bool slotStillHeld = latestOptions.Exists(o => o.HandSlot == selectedSlot);
                if (!slotStillHeld)
                {
                    CombatUI?.ShowCombatLog("⚠ That held item is no longer equipped.");
                    ShowActionChoices();
                    return;
                }

                ResolveDropEquippedHeldItemFreeAction(pc, selectedSlot);
            },
            onCancel: ShowActionChoices);
    }

    private void ResolveDropEquippedHeldItemFreeAction(CharacterController actor, EquipSlot handSlot)
    {
        if (!TryDropEquippedHeldItemToGround(actor, handSlot, out ItemData droppedItem, out EquipSlot droppedSlot, out string feedback))
        {
            CombatUI?.ShowCombatLog($"⚠ {feedback}");
            ShowActionChoices();
            return;
        }

        CombatUI?.ShowCombatLog($"⬇ {actor.Stats.CharacterName} drops {droppedItem.Name} from {droppedSlot}.");
        CombatUI?.ShowCombatLog("(Free action - no attacks of opportunity provoked)");
        UpdateAllStatsUI();
        InvalidatePreviewThreats();
        ShowActionChoices();
    }

    /// <summary>
    /// Drop an item from inventory (used by inventory context menu).
    /// This is an immediate utility action and does not consume action economy.
    /// </summary>
    public bool TryDropInventoryItemToGround(CharacterController actor, int inventoryIndex, out string feedback)
    {
        feedback = string.Empty;
        if (actor == null || actor.Stats == null)
        {
            feedback = "No active character.";
            return false;
        }

        Inventory inv = actor.GetComponent<InventoryComponent>()?.CharacterInventory;
        if (inv == null)
        {
            feedback = $"{actor.Stats.CharacterName} has no inventory.";
            return false;
        }

        if (inventoryIndex < 0 || inventoryIndex >= inv.GeneralSlots.Length)
        {
            feedback = "Invalid inventory slot.";
            return false;
        }

        ItemData item = inv.GeneralSlots[inventoryIndex];
        if (item == null)
        {
            feedback = "Inventory slot is empty.";
            return false;
        }

        SquareCell cell = GetCharacterCurrentCell(actor);
        if (cell == null)
        {
            feedback = "Current ground square is unavailable.";
            return false;
        }

        inv.RemoveItemAt(inventoryIndex);
        cell.AddGroundItem(item);
        feedback = $"{actor.Stats.CharacterName} drops {item.Name} on the ground at ({cell.Coords.x},{cell.Coords.y}).";
        CombatUI?.ShowCombatLog($"⬇ {feedback}");
        UpdateAllStatsUI();
        InvalidatePreviewThreats();
        return true;
    }

    /// <summary>
    /// Pick up an item from the actor's current square or any adjacent square.
    /// In combat: move action (or standard->move conversion), and this provokes AoO.
    /// </summary>
    public string GetPickUpItemDisabledReason(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return "No active character";

        Inventory inv = character.GetComponent<InventoryComponent>()?.CharacterInventory;
        if (inv == null)
            return "No inventory";

        if (character.HasCondition(CombatConditionType.Pinned))
            return "Pinned: only grapple escape actions allowed";

        if (!TryGetAvailablePickUpItems(character, out _))
            return "No item on or adjacent";

        if (inv.EmptySlots <= 0)
            return "Inventory full";

        if (!(character.Actions.HasMoveAction || character.Actions.CanConvertStandardToMove || character.Actions.HasStandardAction))
            return "No move or standard action available";

        return string.Empty;
    }

    public bool HasGroundItemInPickupRange(CharacterController character)
    {
        return TryGetAvailablePickUpItems(character, out _);
    }

    public void OnPickUpItemButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;

        if (RedirectPinnedCharacterToGrappleMenu(pc, "picking up items"))
            return;

        string disabledReason = GetPickUpItemDisabledReason(pc);
        if (!string.IsNullOrEmpty(disabledReason))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot pick up item: {disabledReason}.");
            return;
        }

        if (!TryGetAvailablePickUpItems(pc, out List<PickUpGroundItemOption> options))
        {
            CombatUI?.ShowCombatLog("⚠ No item to pick up in current or adjacent squares.");
            return;
        }

        if (options.Count == 1)
        {
            PickUpGroundItemOption single = options[0];
            ResolvePickUpItemProvocation(pc, single.Cell, single.Item);
            return;
        }

        List<string> optionLabels = new List<string>(options.Count);
        for (int i = 0; i < options.Count; i++)
        {
            optionLabels.Add(options[i].GetSelectionLabel());
        }

        if (CombatUI == null)
        {
            PickUpGroundItemOption fallbackOption = options[0];
            ResolvePickUpItemProvocation(pc, fallbackOption.Cell, fallbackOption.Item);
            return;
        }

        CombatUI.ShowPickUpItemSelection(
            pc.Stats.CharacterName,
            optionLabels,
            onSelect: selectedIndex =>
            {
                if (selectedIndex < 0 || selectedIndex >= options.Count)
                {
                    ShowActionChoices();
                    return;
                }

                PickUpGroundItemOption selectedOption = options[selectedIndex];
                if (selectedOption.Cell == null || selectedOption.Item == null)
                {
                    CombatUI?.ShowCombatLog("⚠ That item is no longer available.");
                    ShowActionChoices();
                    return;
                }

                ResolvePickUpItemProvocation(pc, selectedOption.Cell, selectedOption.Item);
            },
            onCancel: ShowActionChoices);
    }

    private void ResolvePickUpItemProvocation(CharacterController actor, SquareCell cell, ItemData item)
    {
        if (actor == null || cell == null || item == null)
            return;

        var threateningEnemies = ThreatSystem.GetThreateningEnemies(actor.GridPosition, actor, GetAllCharacters());
        threateningEnemies.RemoveAll(enemy => enemy == null || enemy.Stats == null || enemy.Stats.IsDead || !ThreatSystem.CanMakeAoO(enemy));

        if (threateningEnemies.Count == 0)
        {
            if (TryPickUpGroundItem(actor, cell, item, out string pickupMsg))
            {
                ConsumeItemManipulationAction(actor);
                CombatUI?.ShowCombatLog(pickupMsg);
                UpdateAllStatsUI();
            }
            else
            {
                CombatUI?.ShowCombatLog($"⚠ {pickupMsg}");
            }

            ShowActionChoices();
            return;
        }

        ShowAoOActionConfirmation(new AoOProvokingActionInfo
        {
            ActionType = AoOProvokingAction.RetrieveItem,
            ActionName = $"PICK UP {item.Name.ToUpper()}",
            ActionDescription = $"Pick up {item.Name} from ground",
            Actor = actor,
            ThreateningEnemies = threateningEnemies,
            OnProceed = () => StartCoroutine(ResolvePickUpAoOsAndApply(actor, cell, item, threateningEnemies)),
            OnCancel = ShowActionChoices
        });
    }

    private IEnumerator ResolvePickUpAoOsAndApply(CharacterController actor, SquareCell cell, ItemData item, List<CharacterController> threateningEnemies)
    {
        if (actor == null || actor.Stats == null)
            yield break;

        CurrentSubPhase = PlayerSubPhase.Animating;
        CombatUI?.ShowCombatLog($"{actor.Stats.CharacterName} reaches for {item.Name} on the ground (provokes AoO).");

        foreach (var enemy in threateningEnemies)
        {
            if (actor.Stats.IsDead) break;
            if (enemy == null || enemy.Stats == null || enemy.Stats.IsDead || !ThreatSystem.CanMakeAoO(enemy))
                continue;

            CombatResult aooResult = ThreatSystem.ExecuteAoO(enemy, actor);
            if (aooResult == null) continue;

            CombatUI?.ShowCombatLog($"⚔ AoO vs pick up: {aooResult.GetDetailedSummary()}");
            UpdateAllStatsUI();

            if (aooResult.Hit && aooResult.TotalDamage > 0)
                CheckConcentrationOnDamage(actor, aooResult.TotalDamage);

            yield return new WaitForSeconds(0.65f);
        }

        if (actor.Stats.IsDead)
        {
            CombatUI?.ShowCombatLog($"💀 {actor.Stats.CharacterName} is slain before picking up {item.Name}!");
            UpdateAllStatsUI();
            EndActivePCTurn();
            yield break;
        }

        if (TryPickUpGroundItem(actor, cell, item, out string pickupMsg))
        {
            ConsumeItemManipulationAction(actor);
            CombatUI?.ShowCombatLog(pickupMsg);
            UpdateAllStatsUI();
        }
        else
        {
            CombatUI?.ShowCombatLog($"⚠ {pickupMsg}");
        }

        ShowActionChoices();
    }

    private bool TryGetHeldItemDropOptions(CharacterController actor, out List<DropEquippedHeldItemOption> options)
    {
        options = new List<DropEquippedHeldItemOption>();
        if (actor == null)
            return false;

        Inventory inv = actor.GetComponent<InventoryComponent>()?.CharacterInventory;
        if (inv == null)
            return false;

        if (inv.RightHandSlot != null)
            options.Add(new DropEquippedHeldItemOption(EquipSlot.RightHand, inv.RightHandSlot));

        if (inv.LeftHandSlot != null)
            options.Add(new DropEquippedHeldItemOption(EquipSlot.LeftHand, inv.LeftHandSlot));

        return options.Count > 0;
    }

    private bool TryDropEquippedHeldItemToGround(CharacterController actor, EquipSlot preferredSlot, out ItemData droppedItem, out EquipSlot droppedSlot, out string feedback)
    {
        droppedItem = null;
        droppedSlot = EquipSlot.None;
        feedback = string.Empty;

        if (actor == null || actor.Stats == null)
        {
            feedback = "No active character.";
            return false;
        }

        Inventory inv = actor.GetComponent<InventoryComponent>()?.CharacterInventory;
        if (inv == null)
        {
            feedback = $"{actor.Stats.CharacterName} has no inventory.";
            return false;
        }

        if (preferredSlot == EquipSlot.RightHand)
        {
            if (inv.RightHandSlot == null)
            {
                feedback = "RightHand has no held item to drop.";
                return false;
            }

            droppedItem = inv.RightHandSlot;
            inv.RightHandSlot = null;
            droppedSlot = EquipSlot.RightHand;
        }
        else if (preferredSlot == EquipSlot.LeftHand)
        {
            if (inv.LeftHandSlot == null)
            {
                feedback = "LeftHand has no held item to drop.";
                return false;
            }

            droppedItem = inv.LeftHandSlot;
            inv.LeftHandSlot = null;
            droppedSlot = EquipSlot.LeftHand;
        }
        else
        {
            feedback = "Invalid held slot selection.";
            return false;
        }

        SquareCell cell = GetCharacterCurrentCell(actor);
        if (cell == null)
        {
            if (droppedSlot == EquipSlot.RightHand) inv.RightHandSlot = droppedItem;
            else if (droppedSlot == EquipSlot.LeftHand) inv.LeftHandSlot = droppedItem;
            droppedItem = null;
            droppedSlot = EquipSlot.None;
            feedback = "Current ground square is unavailable.";
            return false;
        }

        cell.AddGroundItem(droppedItem);
        inv.RecalculateStats();
        return true;
    }

    private void ResolveThrownWeaponAfterAttack(CharacterController thrower, CharacterController target, ItemData thrownWeapon)
    {
        if (_currentAttackType != AttackType.Thrown)
            return;

        if (!IsThrowableMeleeWeapon(thrownWeapon))
            return;

        if (thrower == null || thrower.Stats == null)
            return;

        Vector2Int landingPosition = target != null ? target.GridPosition : thrower.GridPosition;
        if (!TryDropThrownWeaponToGround(thrower, thrownWeapon, landingPosition, EquipSlot.RightHand, out string dropFeedback))
        {
            Debug.LogWarning($"[Attack][Thrown] {dropFeedback}");
            CombatUI?.ShowCombatLog($"⚠ {dropFeedback}");
            return;
        }

        CombatUI?.ShowCombatLog($"→ {thrownWeapon.Name} lands on ground at ({landingPosition.x},{landingPosition.y}).");

        if (TryEquipNextThrowableWeapon(thrower, out ItemData nextWeapon, out string equipFeedback))
        {
            Debug.Log($"[Attack][Thrown] {equipFeedback}");
            CombatUI?.ShowCombatLog($"↻ {thrower.Stats.CharacterName} auto-equips {nextWeapon.Name}.");
            _equippedWeapon = nextWeapon;
            return;
        }

        Debug.Log($"[Attack][Thrown] {equipFeedback}");
        _equippedWeapon = thrower.GetEquippedMainWeapon();

        if (!thrower.HasThrowableWeaponEquipped())
        {
            Debug.Log($"[Attack][Thrown] {thrower.Stats.CharacterName} has no throwable weapon equipped after the throw.");
            CombatUI?.ShowCombatLog($"⚠ {thrower.Stats.CharacterName} has no more throwable weapons equipped.");
        }
    }

    private bool TryDropThrownWeaponToGround(CharacterController thrower, ItemData thrownWeapon, Vector2Int targetPosition, EquipSlot preferredSlot, out string feedback)
    {
        feedback = string.Empty;

        if (thrower == null || thrower.Stats == null)
        {
            feedback = "Thrown weapon drop failed: no active thrower.";
            return false;
        }

        if (thrownWeapon == null)
        {
            feedback = $"{thrower.Stats.CharacterName} has no thrown weapon to drop.";
            return false;
        }

        Inventory inv = thrower.GetComponent<InventoryComponent>()?.CharacterInventory;
        if (inv == null)
        {
            feedback = $"Thrown weapon drop failed: {thrower.Stats.CharacterName} has no inventory.";
            return false;
        }

        bool removed = false;
        string removedFrom = string.Empty;
        EquipSlot removedEquipSlot = EquipSlot.None;
        int removedInventorySlot = -1;

        bool preferLeft = preferredSlot == EquipSlot.LeftHand;
        bool preferRight = preferredSlot == EquipSlot.RightHand;

        if (preferLeft && inv.LeftHandSlot == thrownWeapon)
        {
            inv.LeftHandSlot = null;
            removed = true;
            removedFrom = EquipSlot.LeftHand.ToString();
            removedEquipSlot = EquipSlot.LeftHand;
        }
        else if (preferRight && inv.RightHandSlot == thrownWeapon)
        {
            inv.RightHandSlot = null;
            removed = true;
            removedFrom = EquipSlot.RightHand.ToString();
            removedEquipSlot = EquipSlot.RightHand;
        }
        else if (inv.RightHandSlot == thrownWeapon)
        {
            inv.RightHandSlot = null;
            removed = true;
            removedFrom = EquipSlot.RightHand.ToString();
            removedEquipSlot = EquipSlot.RightHand;
        }
        else if (inv.LeftHandSlot == thrownWeapon)
        {
            inv.LeftHandSlot = null;
            removed = true;
            removedFrom = EquipSlot.LeftHand.ToString();
            removedEquipSlot = EquipSlot.LeftHand;
        }
        else if (inv.HandsSlot == thrownWeapon)
        {
            inv.HandsSlot = null;
            removed = true;
            removedFrom = EquipSlot.Hands.ToString();
            removedEquipSlot = EquipSlot.Hands;
        }
        else
        {
            for (int i = 0; i < inv.GeneralSlots.Length; i++)
            {
                if (inv.GeneralSlots[i] != thrownWeapon)
                    continue;

                inv.GeneralSlots[i] = null;
                removed = true;
                removedFrom = $"Inventory slot {i}";
                removedInventorySlot = i;
                break;
            }
        }

        if (!removed)
        {
            feedback = $"Thrown weapon drop failed: {thrownWeapon.Name} is no longer in {thrower.Stats.CharacterName}'s inventory.";
            return false;
        }

        SquareGrid grid = Grid != null ? Grid : SquareGrid.Instance;
        SquareCell targetCell = grid != null ? grid.GetCell(targetPosition) : null;
        if (targetCell == null)
        {
            targetCell = GetCharacterCurrentCell(thrower);
            if (targetCell != null)
                targetPosition = targetCell.Coords;
        }

        if (targetCell == null)
        {
            if (removedEquipSlot == EquipSlot.RightHand)
                inv.RightHandSlot = thrownWeapon;
            else if (removedEquipSlot == EquipSlot.LeftHand)
                inv.LeftHandSlot = thrownWeapon;
            else if (removedEquipSlot == EquipSlot.Hands)
                inv.HandsSlot = thrownWeapon;
            else if (removedInventorySlot >= 0 && removedInventorySlot < inv.GeneralSlots.Length)
                inv.GeneralSlots[removedInventorySlot] = thrownWeapon;

            inv.RecalculateStats();
            feedback = $"Thrown weapon drop failed: no valid ground square for {thrownWeapon.Name}.";
            return false;
        }

        targetCell.AddGroundItem(thrownWeapon);
        inv.RecalculateStats();
        InvalidatePreviewThreats();

        feedback = $"[Attack][Thrown] {thrower.Stats.CharacterName} throws {thrownWeapon.Name}; removed from {removedFrom} and dropped at ({targetPosition.x},{targetPosition.y}).";
        Debug.Log(feedback);
        return true;
    }

    private bool TryEquipNextThrowableWeapon(CharacterController character, out ItemData equippedWeapon, out string feedback)
    {
        equippedWeapon = null;
        feedback = string.Empty;

        if (character == null || character.Stats == null)
        {
            feedback = "No active character for throwable auto-equip.";
            return false;
        }

        Inventory inv = character.GetComponent<InventoryComponent>()?.CharacterInventory;
        if (inv == null)
        {
            feedback = $"{character.Stats.CharacterName} has no inventory for throwable auto-equip.";
            return false;
        }

        bool rightAvailable = inv.RightHandSlot == null;
        bool leftAvailable = inv.LeftHandSlot == null;
        if (!rightAvailable && !leftAvailable)
        {
            feedback = $"{character.Stats.CharacterName} has no free hand for auto-equip after throw.";
            return false;
        }

        for (int i = 0; i < inv.GeneralSlots.Length; i++)
        {
            ItemData candidate = inv.GeneralSlots[i];
            if (!IsThrowableMeleeWeapon(candidate))
                continue;

            EquipSlot slotToUse = EquipSlot.None;
            if (rightAvailable && candidate.CanEquipIn(EquipSlot.RightHand))
                slotToUse = EquipSlot.RightHand;
            else if (leftAvailable && candidate.CanEquipIn(EquipSlot.LeftHand))
                slotToUse = EquipSlot.LeftHand;

            if (slotToUse == EquipSlot.None)
                continue;

            inv.GeneralSlots[i] = null;
            if (slotToUse == EquipSlot.RightHand)
            {
                inv.RightHandSlot = candidate;
                rightAvailable = false;
            }
            else
            {
                inv.LeftHandSlot = candidate;
                leftAvailable = false;
            }

            inv.RecalculateStats();
            equippedWeapon = candidate;
            feedback = $"[Attack][Thrown] Auto-equipped {candidate.Name} into {slotToUse}.";
            return true;
        }

        feedback = $"[Attack][Thrown] No more throwable melee weapons available for {character.Stats.CharacterName}.";
        return false;
    }

    private bool TryEquipNextThrowableOffHandWeapon(CharacterController character, out ItemData equippedWeapon, out string feedback)
    {
        equippedWeapon = null;
        feedback = string.Empty;

        if (character == null || character.Stats == null)
        {
            feedback = "No active character for off-hand throwable auto-equip.";
            return false;
        }

        Inventory inv = character.GetComponent<InventoryComponent>()?.CharacterInventory;
        if (inv == null)
        {
            feedback = $"{character.Stats.CharacterName} has no inventory for off-hand throwable auto-equip.";
            return false;
        }

        if (inv.LeftHandSlot != null)
        {
            feedback = $"{character.Stats.CharacterName} has no free off-hand slot for auto-equip after throw.";
            return false;
        }

        for (int i = 0; i < inv.GeneralSlots.Length; i++)
        {
            ItemData candidate = inv.GeneralSlots[i];
            if (!IsThrowableMeleeWeapon(candidate) || !candidate.CanEquipIn(EquipSlot.LeftHand))
                continue;

            inv.GeneralSlots[i] = null;
            inv.LeftHandSlot = candidate;
            inv.RecalculateStats();
            equippedWeapon = candidate;
            feedback = $"[Attack][OffHand][Thrown] Auto-equipped {candidate.Name} into {EquipSlot.LeftHand}.";
            return true;
        }

        feedback = $"[Attack][OffHand][Thrown] No more throwable melee weapons available for off-hand on {character.Stats.CharacterName}.";
        return false;
    }

    private static bool IsThrowableMeleeWeapon(ItemData item)
    {
        return item != null
            && item.IsWeapon
            && item.WeaponCat == WeaponCategory.Melee
            && item.IsThrown
            && item.RangeIncrement > 0;
    }

    private bool TryPickUpGroundItem(CharacterController actor, SquareCell cell, ItemData item, out string feedback)
    {
        feedback = string.Empty;
        if (actor == null || actor.Stats == null)
        {
            feedback = "No active character.";
            return false;
        }

        if (cell == null)
        {
            feedback = "No ground square available.";
            return false;
        }

        if (item == null)
        {
            feedback = "No item selected.";
            return false;
        }

        Inventory inv = actor.GetComponent<InventoryComponent>()?.CharacterInventory;
        if (inv == null)
        {
            feedback = $"{actor.Stats.CharacterName} has no inventory.";
            return false;
        }

        if (inv.EmptySlots <= 0)
        {
            feedback = $"{actor.Stats.CharacterName}'s inventory is full.";
            return false;
        }

        if (!cell.RemoveGroundItem(item))
        {
            feedback = $"{item.Name} is no longer on the ground.";
            return false;
        }

        if (!inv.AddItem(item))
        {
            cell.AddGroundItem(item);
            feedback = $"{actor.Stats.CharacterName}'s inventory is full.";
            return false;
        }

        feedback = $"📦 {actor.Stats.CharacterName} picks up {item.Name} from ({cell.Coords.x},{cell.Coords.y}).";
        return true;
    }

    private bool TryGetAvailablePickUpItems(CharacterController character, out List<PickUpGroundItemOption> options)
    {
        options = new List<PickUpGroundItemOption>();
        if (character == null)
            return false;

        SquareGrid grid = Grid != null ? Grid : SquareGrid.Instance;
        if (grid == null)
            return false;

        Vector2Int origin = character.GridPosition;

        AddGroundItemsFromCell(grid.GetCell(origin), options);

        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                if (x == 0 && y == 0)
                    continue;

                SquareCell adjacentCell = grid.GetCell(new Vector2Int(origin.x + x, origin.y + y));
                AddGroundItemsFromCell(adjacentCell, options);
            }
        }

        return options.Count > 0;
    }

    private void AddGroundItemsFromCell(SquareCell cell, List<PickUpGroundItemOption> options)
    {
        if (cell == null || options == null || cell.GroundItems == null || cell.GroundItems.Count == 0)
            return;

        for (int i = 0; i < cell.GroundItems.Count; i++)
        {
            ItemData item = cell.GroundItems[i];
            if (item == null)
                continue;

            options.Add(new PickUpGroundItemOption(cell, item));
        }
    }

    private sealed class DropEquippedHeldItemOption
    {
        public readonly EquipSlot HandSlot;
        public readonly ItemData HeldItem;

        public DropEquippedHeldItemOption(EquipSlot handSlot, ItemData heldItem)
        {
            HandSlot = handSlot;
            HeldItem = heldItem;
        }

        public string GetSelectionLabel()
        {
            string itemName = HeldItem != null && !string.IsNullOrEmpty(HeldItem.Name) ? HeldItem.Name : "Unknown Item";
            return $"{itemName} ({HandSlot})";
        }
    }

    private sealed class PickUpGroundItemOption
    {
        public readonly SquareCell Cell;
        public readonly ItemData Item;

        public PickUpGroundItemOption(SquareCell cell, ItemData item)
        {
            Cell = cell;
            Item = item;
        }

        public string GetSelectionLabel()
        {
            string itemName = Item != null && !string.IsNullOrEmpty(Item.Name) ? Item.Name : "Unknown Item";
            string itemDescription = Item != null && !string.IsNullOrEmpty(Item.Description) ? Item.Description : "No description.";

            Vector2Int coords = Cell != null ? Cell.Coords : Vector2Int.zero;
            string locationText = Cell != null
                ? $"Square ({coords.x},{coords.y})"
                : "Square (unknown)";

            return $"{itemName}\n{itemDescription}\n{locationText}";
        }
    }

    private SquareCell GetCharacterCurrentCell(CharacterController character)
    {
        if (character == null)
            return null;

        SquareGrid grid = Grid != null ? Grid : SquareGrid.Instance;
        return grid != null ? grid.GetCell(character.GridPosition) : null;
    }

    private bool ApplyConsumableEffectAndConsume(CharacterController actor, int inventoryIndex, out string resultMessage)
    {
        resultMessage = string.Empty;

        if (actor == null || actor.Stats == null)
        {
            resultMessage = "No active character.";
            return false;
        }

        var inv = actor.GetComponent<InventoryComponent>()?.CharacterInventory;
        if (inv == null)
        {
            resultMessage = $"{actor.Stats.CharacterName} has no inventory.";
            return false;
        }

        if (inventoryIndex < 0 || inventoryIndex >= inv.GeneralSlots.Length)
        {
            resultMessage = "Invalid inventory slot.";
            return false;
        }

        ItemData currentItem = inv.GeneralSlots[inventoryIndex];
        if (currentItem == null)
        {
            resultMessage = "That item is no longer in the selected slot.";
            return false;
        }

        int oldHP = actor.Stats.CurrentHP;
        int oldNonlethal = actor.Stats.NonlethalDamage;
        int healedAmount = 0;
        int nonlethalHealedAmount = 0;
        string spellSummary = string.Empty;

        switch (currentItem.ConsumableEffect)
        {
            case ConsumableEffectType.HealHP:
            {
                int healingRoll = RollHealingFromConsumable(currentItem);
                healedAmount = actor.Stats.HealDamage(healingRoll, out nonlethalHealedAmount);
                break;
            }
            case ConsumableEffectType.SpellEffect:
            {
                if (!TryApplySpellConsumableEffect(actor, currentItem, out spellSummary))
                {
                    resultMessage = spellSummary;
                    return false;
                }
                break;
            }
            case ConsumableEffectType.None:
            default:
            {
                // Legacy fallback for older consumables defined with flat HealAmount only.
                if (currentItem.HealAmount > 0)
                {
                    healedAmount = actor.Stats.HealDamage(currentItem.HealAmount, out nonlethalHealedAmount);
                }
                else
                {
                    resultMessage = $"{currentItem.Name} has no implemented consumable effect yet.";
                    return false;
                }
                break;
            }
        }

        inv.RemoveItemAt(inventoryIndex);

        if (currentItem.ConsumableEffect == ConsumableEffectType.SpellEffect)
        {
            resultMessage = $"🧪 {actor.Stats.CharacterName} uses {currentItem.Name}. {spellSummary} Item consumed.";
            return true;
        }

        int newCurrentHP = actor.Stats.CurrentHP;
        int newNonlethal = actor.Stats.NonlethalDamage;
        int nonlethalHealed = Mathf.Max(nonlethalHealedAmount, Mathf.Max(0, oldNonlethal - newNonlethal));
        resultMessage = $"🧪 {actor.Stats.CharacterName} uses {currentItem.Name}, healing {healedAmount} HP ({oldHP} → {newCurrentHP}) and removing {nonlethalHealed} nonlethal ({oldNonlethal} → {newNonlethal}). Item consumed.";
        return true;
    }

    private bool TryApplySpellConsumableEffect(CharacterController actor, ItemData item, out string summary)
    {
        summary = string.Empty;
        if (actor == null || actor.Stats == null)
        {
            summary = "No active character.";
            return false;
        }

        if (item == null || string.IsNullOrWhiteSpace(item.ConsumableSpellName))
        {
            summary = "Consumable has no linked spell definition.";
            return false;
        }

        SpellDatabase.Init();
        SpellData baseSpell = SpellDatabase.GetSpellByName(item.ConsumableSpellName);
        if (baseSpell == null)
        {
            summary = $"Spell not found for consumable: {item.ConsumableSpellName}.";
            return false;
        }

        int casterLevel = Mathf.Max(1, item.ConsumableMinimumCasterLevel);
        SpellData consumableSpell = BuildConsumableSpellVariant(baseSpell, item);

        if (consumableSpell.EffectType == SpellEffectType.Healing)
        {
            int oldHP = actor.Stats.CurrentHP;
            int oldNonlethal = actor.Stats.NonlethalDamage;
            int healingRoll = RollHealingFromSpell(consumableSpell);
            int nonlethalHealed;
            int hpHealed = actor.Stats.HealDamage(healingRoll, out nonlethalHealed);
            int newHP = actor.Stats.CurrentHP;
            summary = $"{consumableSpell.Name} heals {hpHealed} HP ({oldHP} → {newHP}) and removes {nonlethalHealed} nonlethal ({oldNonlethal} → {actor.Stats.NonlethalDamage}) at caster level {casterLevel}.";
            return true;
        }

        if (consumableSpell.EffectType == SpellEffectType.Buff || consumableSpell.EffectType == SpellEffectType.Debuff)
        {
            var statusMgr = actor.GetComponent<StatusEffectManager>();
            if (statusMgr == null)
            {
                statusMgr = actor.gameObject.AddComponent<StatusEffectManager>();
                statusMgr.Init(actor.Stats);
            }

            var effect = statusMgr.AddEffect(consumableSpell, item.Name, casterLevel);
            if (effect == null)
            {
                summary = $"{consumableSpell.Name} could not be applied (stacking or stronger existing effect).";
                return false;
            }

            summary = $"{consumableSpell.Name} applied [{effect.GetDurationDisplayString()}].";
            return true;
        }

        summary = $"{consumableSpell.Name} is not supported for consumable use yet.";
        return false;
    }

    private static SpellData BuildConsumableSpellVariant(SpellData baseSpell, ItemData item)
    {
        SpellData spell = baseSpell != null ? baseSpell.Clone() : null;
        if (spell == null || item == null)
            return spell;

        int modifier = item.ConsumableModifier;
        if (modifier == 0)
            return spell;

        if (spell.EffectType == SpellEffectType.Healing)
        {
            spell.BonusHealing = modifier;
            return spell;
        }

        if (spell.BuffDeflectionBonus != 0)
            spell.BuffDeflectionBonus = modifier;
        else if (spell.BuffShieldBonus != 0)
            spell.BuffShieldBonus = modifier;
        else if (spell.BuffACBonus != 0)
            spell.BuffACBonus = modifier;
        else if (spell.BuffAttackBonus != 0)
            spell.BuffAttackBonus = modifier;
        else if (spell.BuffDamageBonus != 0)
            spell.BuffDamageBonus = modifier;
        else if (spell.BuffSaveBonus != 0)
            spell.BuffSaveBonus = modifier;
        else if (spell.BuffStatBonus != 0)
            spell.BuffStatBonus = Mathf.Abs(modifier) * (spell.BuffStatBonus >= 0 ? 1 : -1);

        return spell;
    }

    private static int RollHealingFromSpell(SpellData spell)
    {
        if (spell == null) return 0;

        if (spell.HealCount > 0 && spell.HealDice > 0)
        {
            int total = 0;
            for (int i = 0; i < spell.HealCount; i++)
                total += UnityEngine.Random.Range(1, spell.HealDice + 1);
            total += spell.BonusHealing;
            return Mathf.Max(0, total);
        }

        return Mathf.Max(0, spell.BonusHealing);
    }

    private static int RollHealingFromConsumable(ItemData item)
    {
        if (item == null) return 0;

        if (item.HealDiceCount > 0 && item.HealDiceSides > 0)
        {
            int total = 0;
            for (int i = 0; i < item.HealDiceCount; i++)
                total += UnityEngine.Random.Range(1, item.HealDiceSides + 1);
            total += item.HealBonus;
            return Mathf.Max(0, total);
        }

        if (item.HealAmount > 0)
            return item.HealAmount;

        return 0;
    }

    // ========== ACTION BUTTON HANDLERS ==========


    public void OnMoveButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;

        if (RedirectPinnedCharacterToGrappleMenu(pc, "movement"))
            return;

        if (pc.IsGrappling())
        {
            CombatUI.ShowCombatLog($"⚠ {pc.Stats.CharacterName} is grappled and cannot take normal movement. Use a grapple action (Move while grappling) after winning the opposed check.");
            return;
        }

        if (pc.Stats.MovementBlockedByCondition)
        {
            CombatUI.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot move due to an active condition.");
            return;
        }

        if (pc.HasTakenFiveFootStep)
        {
            CombatUI.ShowCombatLog($"⚠ {pc.Stats.CharacterName} already used a 5-foot step this turn and cannot take normal movement.");
            return;
        }

        if (pc.HasCondition(CombatConditionType.Prone))
        {
            CombatUI.ShowCombatLog($"⚠ {pc.Stats.CharacterName} is prone and must stand up or crawl.");
            return;
        }

        if (pc.Actions.HasMoveAction) { /* Normal move */ }
        else if (pc.Actions.CanConvertStandardToMove) { /* Will convert */ }
        else return;

        EndAttackSequence();
        CurrentSubPhase = PlayerSubPhase.Moving;
        ShowMovementRange(pc);
        CombatUI.SetActionButtonsVisible(false);
        CombatUI.SetTurnIndicator($"{pc.Stats.CharacterName} - Click a tile to move (right-click/ESC or own tile to cancel)");
    }

    public bool CanTakeFiveFootStep(CharacterController character)
    {
        return string.IsNullOrEmpty(GetFiveFootStepDisabledReason(character));
    }

    public string GetFiveFootStepDisabledReason(CharacterController character)
    {
        string reason = string.Empty;

        if (_movementService != null && _movementService.CanTake5FootStep(character, out reason))
            return string.Empty;

        if (_movementService != null)
            return reason;

        return "Movement service unavailable";
    }

    public void OnFiveFootStepButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;

        if (RedirectPinnedCharacterToGrappleMenu(pc, "a 5-foot step"))
            return;

        string reason = GetFiveFootStepDisabledReason(pc);
        if (!string.IsNullOrEmpty(reason))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot take a 5-foot step: {reason}.");
            return;
        }

        CurrentSubPhase = PlayerSubPhase.TakingFiveFootStep;
        ShowFiveFootStepOptions(pc);
        CombatUI.SetActionButtonsVisible(false);
        CombatUI.SetTurnIndicator($"{pc.Stats.CharacterName} - Select 5-foot step destination (right-click/ESC to cancel)");
        CombatUI?.ShowCombatLog($"{pc.Stats.CharacterName} prepares a 5-foot step.");
    }

    private void ShowFiveFootStepOptions(CharacterController pc)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        foreach (Vector2Int neighbor in SquareGridUtils.GetNeighbors(pc.GridPosition))
        {
            if (!IsValidFiveFootStepDestination(pc, neighbor))
                continue;

            SquareCell cell = Grid.GetCell(neighbor);
            if (cell == null) continue;

            cell.SetHighlight(HighlightType.FiveFootStep);
            _highlightedCells.Add(cell);
        }

        HighlightCharacterFootprint(pc, HighlightType.Selected);
    }

    private bool IsValidFiveFootStepDestination(CharacterController pc, Vector2Int destination)
    {
        if (_movementService != null)
            return _movementService.CanTake5FootStep(pc, destination);

        return false;
    }

    private void HandleFiveFootStepClick(CharacterController pc, SquareCell cell)
    {
        if (cell == null) return;

        if (cell.Coords == pc.GridPosition)
        {
            CancelFiveFootStepSelection();
            return;
        }

        if (!_highlightedCells.Contains(cell))
            return;

        bool success = ExecuteFiveFootStep(pc, cell, returnToActionChoices: !_isAwaitingFullAttackFiveFootStepSelection);

        if (_isAwaitingFullAttackFiveFootStepSelection)
        {
            if (success)
                _fullAttackFiveFootStepWasTaken = true;

            _fullAttackFiveFootStepSelectionCancelled = !success;
            _isAwaitingFullAttackFiveFootStepSelection = false;
            CurrentSubPhase = PlayerSubPhase.Animating;
        }
    }

    private bool ExecuteFiveFootStep(CharacterController pc, SquareCell destination, bool returnToActionChoices = true)
    {
        if (pc == null || destination == null)
            return false;

        if (!IsValidFiveFootStepDestination(pc, destination.Coords))
        {
            CombatUI?.ShowCombatLog("⚠ Invalid 5-foot step destination.");
            return false;
        }

        Vector2Int oldPos = pc.GridPosition;

        // 5-foot step does NOT consume move/standard/full-round actions and does NOT provoke AoO.
        bool fiveFootStepSucceeded = _movementService != null
            ? _movementService.Execute5FootStep(pc, destination)
            : pc.FiveFootStep(destination);

        if (!fiveFootStepSucceeded)
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} failed to take a 5-foot step.");
            return false;
        }

        Debug.Log($"[Movement] 5 foot step taken - blocking overrun for {pc.Stats.CharacterName}");
        Debug.Log($"[Movement] HasTakenFiveFootStep={pc.HasTakenFiveFootStep}");

        RefreshFlankedConditions();
        UpdateAllStatsUI();
        InvalidatePreviewThreats();

        CombatUI?.ShowCombatLog($"{pc.Stats.CharacterName} takes a 5-foot step ({oldPos.x},{oldPos.y} → {destination.Coords.x},{destination.Coords.y}).");
        CombatUI?.ShowCombatLog("(No attacks of opportunity provoked)");

        if (returnToActionChoices)
            ShowActionChoices();

        return true;
    }

    private void CancelFiveFootStepSelection()
    {
        CharacterController pc = ActivePC;

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        if (_isAwaitingFullAttackFiveFootStepSelection)
        {
            _fullAttackFiveFootStepSelectionCancelled = true;
            _fullAttackFiveFootStepWasTaken = false;
            _isAwaitingFullAttackFiveFootStepSelection = false;
            CurrentSubPhase = PlayerSubPhase.Animating;

            if (pc != null)
                CombatUI?.ShowCombatLog($"↩ {pc.Stats.CharacterName} skips 5-foot step.");
            return;
        }

        if (pc != null)
            CombatUI?.ShowCombatLog($"↩ {pc.Stats.CharacterName} cancels 5-foot step.");

        ShowActionChoices();
    }

    public string GetDropProneDisabledReason(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return "No active character";

        if (character.HasCondition(CombatConditionType.Prone))
            return "Already prone";

        if (character.HasCondition(CombatConditionType.Pinned))
            return "Cannot drop prone while pinned";

        if (character.HasCondition(CombatConditionType.Grappled))
            return "Cannot drop prone while grappled";

        return string.Empty;
    }

    public string GetStandUpDisabledReason(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return "No active character";

        if (!character.HasCondition(CombatConditionType.Prone))
            return "Not prone";

        if (character.HasCondition(CombatConditionType.Pinned))
            return "Cannot stand up while pinned";

        if (character.HasCondition(CombatConditionType.Grappled))
            return "Cannot stand up while grappled";

        if (character.HasTakenFiveFootStep)
            return "Cannot stand after taking a 5-foot step";

        if (!character.Actions.HasMoveAction && !character.Actions.CanConvertStandardToMove)
            return "No move action available";

        return string.Empty;
    }

    public string GetCrawlDisabledReason(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return "No active character";

        if (!character.HasCondition(CombatConditionType.Prone))
            return "Must be prone";

        if (character.HasCondition(CombatConditionType.Pinned))
            return "Cannot crawl while pinned";

        if (character.HasCondition(CombatConditionType.Grappled))
            return "Cannot crawl while grappled";

        if (character.HasTakenFiveFootStep)
            return "Cannot crawl after taking a 5-foot step";

        if (!character.Actions.HasMoveAction && !character.Actions.CanConvertStandardToMove)
            return "No move action available";

        bool hasDestination = false;
        foreach (var neighbor in SquareGridUtils.GetNeighbors(character.GridPosition))
        {
            if (IsValidCrawlDestination(character, neighbor))
            {
                hasDestination = true;
                break;
            }
        }

        if (!hasDestination)
            return "No valid adjacent square";

        return string.Empty;
    }

    public void OnDropProneButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;

        if (RedirectPinnedCharacterToGrappleMenu(pc, "dropping prone"))
            return;

        string reason = GetDropProneDisabledReason(pc);
        if (!string.IsNullOrEmpty(reason))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot drop prone: {reason}.");
            return;
        }

        pc.ApplyCondition(CombatConditionType.Prone, -1, pc.Stats.CharacterName);
        CombatUI?.ShowCombatLog($"{pc.Stats.CharacterName} drops prone.");
        CombatUI?.ShowCombatLog("(Free action - no attacks of opportunity provoked)");

        RefreshFlankedConditions();
        UpdateAllStatsUI();
        InvalidatePreviewThreats();
        ShowActionChoices();
    }

    public void OnStandUpButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;

        if (RedirectPinnedCharacterToGrappleMenu(pc, "standing up"))
            return;

        string reason = GetStandUpDisabledReason(pc);
        if (!string.IsNullOrEmpty(reason))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot stand up: {reason}.");
            return;
        }

        List<CharacterController> threateners = ThreatSystem.GetThreateningEnemies(pc.GridPosition, pc, GetAllCharacters());
        threateners.RemoveAll(enemy => enemy == null || enemy.Stats == null || enemy.Stats.IsDead || !ThreatSystem.CanMakeAoO(enemy));

        if (threateners.Count == 0)
        {
            StartCoroutine(ResolveStandUp(pc, threateners));
            return;
        }

        ShowAoOActionConfirmation(new AoOProvokingActionInfo
        {
            ActionType = AoOProvokingAction.StandFromProne,
            ActionName = "STAND UP",
            ActionDescription = "Stand from prone",
            Actor = pc,
            ThreateningEnemies = threateners,
            OnProceed = () => StartCoroutine(ResolveStandUp(pc, threateners)),
            OnCancel = ShowActionChoices
        });
    }

    public void OnCrawlButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;

        if (RedirectPinnedCharacterToGrappleMenu(pc, "crawling"))
            return;

        string reason = GetCrawlDisabledReason(pc);
        if (!string.IsNullOrEmpty(reason))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot crawl: {reason}.");
            return;
        }

        CurrentSubPhase = PlayerSubPhase.Crawling;
        ShowCrawlOptions(pc);
        CombatUI.SetActionButtonsVisible(false);
        CombatUI.SetTurnIndicator($"{pc.Stats.CharacterName} - Select crawl destination (right-click/ESC to cancel)");
        CombatUI?.ShowCombatLog($"{pc.Stats.CharacterName} prepares to crawl (5 ft, provokes AoO).");
    }

    private IEnumerator ResolveStandUp(CharacterController pc, List<CharacterController> threateners = null)
    {
        if (pc == null || pc.Stats == null)
            yield break;

        CurrentSubPhase = PlayerSubPhase.Animating;
        CombatUI?.ShowCombatLog($"{pc.Stats.CharacterName} attempts to stand up...");

        if (threateners == null)
        {
            threateners = ThreatSystem.GetThreateningEnemies(pc.GridPosition, pc, GetAllCharacters());
            threateners.RemoveAll(enemy => enemy == null || enemy.Stats == null || enemy.Stats.IsDead || !ThreatSystem.CanMakeAoO(enemy));
        }

        if (threateners.Count > 0)
        {
            CombatUI?.ShowCombatLog("Standing up provokes attacks of opportunity!");

            foreach (var enemy in threateners)
            {
                if (pc.Stats.IsDead) break;
                if (enemy == null || enemy.Stats == null || enemy.Stats.IsDead) continue;

                CombatResult aooResult = _movementService != null
                    ? _movementService.TriggerAoO(enemy, pc)
                    : ThreatSystem.ExecuteAoO(enemy, pc);
                if (aooResult != null)
                {
                    CombatUI?.ShowCombatLog($"⚔ AoO (standing up): {aooResult.GetDetailedSummary()}");
                    UpdateAllStatsUI();

                    if (aooResult.Hit && aooResult.TotalDamage > 0)
                        CheckConcentrationOnDamage(pc, aooResult.TotalDamage);

                    yield return new WaitForSeconds(0.8f);
                }
            }
        }
        else
        {
            CombatUI?.ShowCombatLog("(No enemies threaten - no attacks of opportunity)");
        }

        if (pc.Stats.IsDead)
        {
            CombatUI?.ShowCombatLog($"{pc.Stats.CharacterName} was slain while trying to stand up!");
            UpdateAllStatsUI();
            EndActivePCTurn();
            yield break;
        }

        bool removed = pc.RemoveCondition(CombatConditionType.Prone);
        if (removed)
            CombatUI?.ShowCombatLog($"{pc.Stats.CharacterName} stands up.");

        ConsumeMoveAction(pc);

        RefreshFlankedConditions();
        UpdateAllStatsUI();
        InvalidatePreviewThreats();
        ShowActionChoices();
    }

    private void ShowCrawlOptions(CharacterController pc)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        foreach (Vector2Int neighbor in SquareGridUtils.GetNeighbors(pc.GridPosition))
        {
            if (!IsValidCrawlDestination(pc, neighbor))
                continue;

            SquareCell cell = Grid.GetCell(neighbor);
            if (cell == null) continue;

            cell.SetHighlight(HighlightType.Move);
            _highlightedCells.Add(cell);
        }

        HighlightCharacterFootprint(pc, HighlightType.Selected);
    }

    private bool IsValidCrawlDestination(CharacterController pc, Vector2Int destination)
    {
        if (pc == null)
            return false;

        if (_movementService == null)
            return false;

        // Crawl is an adjacent 5-ft movement while prone; destination occupancy/terrain constraints mirror step movement,
        // but crawl itself has separate condition rules validated by GetCrawlDisabledReason.
        return _movementService.IsValidAdjacentStepDestination(pc, destination, disallowDifficultTerrain: true);
    }

    private void HandleCrawlClick(CharacterController pc, SquareCell cell)
    {
        if (pc == null || cell == null) return;

        if (cell.Coords == pc.GridPosition)
        {
            CancelCrawlSelection();
            return;
        }

        if (!_highlightedCells.Contains(cell))
            return;

        StartCoroutine(ExecuteCrawl(pc, cell));
    }

    private IEnumerator ExecuteCrawl(CharacterController pc, SquareCell destination)
    {
        if (pc == null || destination == null)
            yield break;

        if (!IsValidCrawlDestination(pc, destination.Coords))
        {
            CombatUI?.ShowCombatLog("⚠ Invalid crawl destination.");
            yield break;
        }

        CurrentSubPhase = PlayerSubPhase.Animating;

        var crawlPath = new List<Vector2Int> { destination.Coords };
        var provokedAoOs = _movementService != null
            ? _movementService.CheckForAoO(pc, crawlPath)
            : ThreatSystem.AnalyzePathForAoOs(pc, crawlPath, GetAllCharacters());

        if (provokedAoOs.Count > 0)
        {
            CombatUI?.ShowCombatLog("Crawling provokes attacks of opportunity!");
            foreach (var aooInfo in provokedAoOs)
            {
                if (pc.Stats.IsDead) break;

                CharacterController threatener = aooInfo.Threatener;
                if (threatener == null || threatener.Stats == null || threatener.Stats.IsDead) continue;

                CombatResult aooResult = _movementService != null
                    ? _movementService.TriggerAoO(threatener, pc)
                    : ThreatSystem.ExecuteAoO(threatener, pc);
                if (aooResult != null)
                {
                    CombatUI?.ShowCombatLog($"⚔ AoO (crawling): {aooResult.GetDetailedSummary()}");
                    UpdateAllStatsUI();

                    if (aooResult.Hit && aooResult.TotalDamage > 0)
                        CheckConcentrationOnDamage(pc, aooResult.TotalDamage);

                    yield return new WaitForSeconds(0.8f);
                }
            }
        }

        if (pc.Stats.IsDead)
        {
            CombatUI?.ShowCombatLog($"{pc.Stats.CharacterName} was slain while crawling!");
            UpdateAllStatsUI();
            EndActivePCTurn();
            yield break;
        }

        Vector2Int oldPos = pc.GridPosition;
        ConsumeMoveAction(pc);

        List<Vector2Int> crawlMovePath = new List<Vector2Int> { destination.Coords };
        if (_movementService != null)
            yield return StartCoroutine(_movementService.ExecuteMovement(pc, crawlMovePath, PlayerMoveSecondsPerStep, markAsMoved: true));
        else
            yield return StartCoroutine(pc.MoveAlongPath(crawlMovePath, PlayerMoveSecondsPerStep, markAsMoved: true));

        RefreshFlankedConditions();
        UpdateAllStatsUI();
        InvalidatePreviewThreats();

        CombatUI?.ShowCombatLog($"{pc.Stats.CharacterName} crawls ({oldPos.x},{oldPos.y} → {destination.Coords.x},{destination.Coords.y}).");

        ShowActionChoices();
    }

    private void CancelCrawlSelection()
    {
        CharacterController pc = ActivePC;

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        if (pc != null)
            CombatUI?.ShowCombatLog($"↩ {pc.Stats.CharacterName} cancels crawl.");

        ShowActionChoices();
    }

    private static void ConsumeMoveAction(CharacterController character)
    {
        if (character == null) return;

        if (character.Actions.HasMoveAction)
            character.Actions.UseMoveAction();
        else if (character.Actions.CanConvertStandardToMove)
            character.Actions.ConvertStandardToMove();
    }

    public bool CanReloadEquippedWeapon(CharacterController character, out string reason, out ReloadActionType reloadAction)
    {
        reason = string.Empty;
        reloadAction = ReloadActionType.None;

        if (character == null)
        {
            reason = "No active character";
            return false;
        }

        ItemData weapon = character.GetEquippedMainWeapon();
        if (weapon == null || !weapon.RequiresReload)
        {
            reason = "No reloadable weapon equipped";
            return false;
        }

        if (weapon.IsLoaded)
        {
            reason = "Weapon already loaded";
            return false;
        }

        if (character.HasCondition(CombatConditionType.Pinned))
        {
            reason = "Pinned creatures cannot reload";
            return false;
        }

        reloadAction = character.GetEffectiveReloadAction(weapon);
        switch (reloadAction)
        {
            case ReloadActionType.FreeAction:
                return true;

            case ReloadActionType.MoveAction:
                if (character.Actions.HasMoveAction || character.Actions.CanConvertStandardToMove)
                    return true;
                reason = "Need move action";
                return false;

            case ReloadActionType.FullRound:
                if (character.Actions.HasFullRoundAction)
                    return true;
                reason = "Need full-round action";
                return false;

            default:
                reason = "Cannot reload";
                return false;
        }
    }

    private bool ExecuteReload(CharacterController character, out string reloadLog)
    {
        reloadLog = string.Empty;
        if (character == null) return false;

        ItemData weapon = character.GetEquippedMainWeapon();
        if (weapon == null || !weapon.RequiresReload) return false;
        if (weapon.IsLoaded) return false;

        if (!CanReloadEquippedWeapon(character, out string reason, out ReloadActionType reloadAction))
        {
            reloadLog = string.IsNullOrEmpty(reason) ? $"Cannot reload {weapon.Name}." : $"Cannot reload {weapon.Name}: {reason}.";
            return false;
        }

        switch (reloadAction)
        {
            case ReloadActionType.FreeAction:
                break;
            case ReloadActionType.MoveAction:
                ConsumeMoveAction(character);
                break;
            case ReloadActionType.FullRound:
                character.Actions.UseFullRoundAction();
                break;
        }

        bool reloaded = character.ReloadWeapon(weapon);
        if (!reloaded)
        {
            reloadLog = $"{weapon.Name} could not be reloaded.";
            return false;
        }

        string actionLabel = CharacterController.GetReloadActionLabel(reloadAction);
        reloadLog = $"🔄 {character.Stats.CharacterName} reloads {weapon.Name} ({actionLabel} action).";
        return true;
    }

    public void OnReloadButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;

        if (RedirectPinnedCharacterToGrappleMenu(pc, "reloading"))
            return;

        if (ExecuteReload(pc, out string reloadLog))
        {
            CombatUI?.ShowCombatLog(reloadLog);
            UpdateAllStatsUI();
            ShowActionChoices();
            return;
        }

        if (!string.IsNullOrEmpty(reloadLog))
            CombatUI?.ShowCombatLog($"⚠ {reloadLog}");
    }


    public void OnAttackButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null)
            return;

        Debug.Log("[Attack][Melee] Melee attack button pressed");
        Debug.Log($"[Attack][Sequence] isInSequence: {_isInAttackSequence}");
        Debug.Log($"[Attack][Sequence] attacksUsed: {_totalAttacksUsed}");
        Debug.Log($"[Attack][DualWield] choiceMade: {_dualWieldingChoiceMade}");
        Debug.Log($"[Attack][DualWield] isDualWielding: {_isDualWielding}");

        if (RedirectPinnedCharacterToGrappleMenu(pc, "attacks"))
            return;

        if (IsActionBlockedByTurnedCondition(pc, "attacks"))
        {
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        if (!CanAttack(pc))
        {
            Debug.Log($"[Attack][Melee] Attack denied actor={pc.Stats.CharacterName} hasStandard={pc.Actions.HasStandardAction} inSequence={_isInAttackSequence}");
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        if (!pc.CanAttackWithEquippedWeapon(out string cannotAttackReason))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot attack: {cannotAttackReason}");
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        bool isFirstMainHandAttack = !_isInAttackSequence && _totalAttacksUsed == 0;
        if (isFirstMainHandAttack && !_dualWieldingChoiceMade && NeedsDualWieldingPrompt(pc))
        {
            Debug.Log("[Attack][DualWield] Showing dual wielding prompt before first main-hand attack.");
            _pendingAttackType = AttackType.Melee;
            ShowDualWieldingPrompt(pc);
            return;
        }

        _pendingDefensiveAttackSelection = false;
        pc.SetFightingDefensively(false);

        if (!_isInAttackSequence)
        {
            Debug.Log("[Attack][Sequence] Starting new sequence with melee");
            StartAttackSequence(pc, AttackType.Melee);
        }
        else
        {
            Debug.Log("[Attack][Sequence] Continuing sequence with melee");
            ContinueAttackSequence(pc, AttackType.Melee);
        }
    }

    public void OnThrownAttackButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null)
            return;

        Debug.Log("[Attack][Thrown] Thrown attack button pressed");
        Debug.Log($"[Attack][Sequence] isInSequence: {_isInAttackSequence}");
        Debug.Log($"[Attack][Sequence] attacksUsed: {_totalAttacksUsed}");
        Debug.Log($"[Attack][DualWield] choiceMade: {_dualWieldingChoiceMade}");
        Debug.Log($"[Attack][DualWield] isDualWielding: {_isDualWielding}");

        if (RedirectPinnedCharacterToGrappleMenu(pc, "thrown attacks"))
            return;

        if (IsActionBlockedByTurnedCondition(pc, "thrown attacks"))
        {
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        if (!CanThrowWeapon(pc))
        {
            Debug.Log($"[Attack][Thrown] Attack denied actor={pc.Stats.CharacterName} hasStandard={pc.Actions.HasStandardAction} hasMove={pc.Actions.HasMoveAction} inSequence={_isInAttackSequence}");
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        bool isFirstMainHandAttack = !_isInAttackSequence && _totalAttacksUsed == 0;
        if (isFirstMainHandAttack && !_dualWieldingChoiceMade && NeedsDualWieldingPrompt(pc))
        {
            Debug.Log("[Attack][DualWield] Showing dual wielding prompt for thrown attack");
            _pendingAttackType = AttackType.Thrown;
            ShowDualWieldingPrompt(pc);
            return;
        }

        _pendingDefensiveAttackSelection = false;
        pc.SetFightingDefensively(false);

        if (!_isInAttackSequence)
        {
            Debug.Log("[Attack][Sequence] Starting new sequence with thrown");
            StartAttackSequence(pc, AttackType.Thrown);
        }
        else
        {
            Debug.Log("[Attack][Sequence] Continuing sequence with thrown");
            ContinueAttackSequence(pc, AttackType.Thrown);
        }
    }

    private bool NeedsDualWieldingPrompt(CharacterController attacker)
    {
        if (attacker == null)
            return false;

        ItemData mainHandWeapon = attacker.GetEquippedMainWeapon();
        ItemData offHandWeapon = attacker.GetOffHandAttackWeapon();

        bool isTwoHanding = attacker.IsTwoHanding();
        bool hasMainHandWeapon = mainHandWeapon != null;
        bool hasOffHandWeapon = offHandWeapon != null;
        bool needsPrompt = !isTwoHanding && hasMainHandWeapon && hasOffHandWeapon;

        Debug.Log($"[Attack][DualWield] hasMainHandWeapon: {hasMainHandWeapon} ({mainHandWeapon?.Name ?? "none"})");
        Debug.Log($"[Attack][DualWield] hasOffHandWeapon: {hasOffHandWeapon} ({offHandWeapon?.Name ?? "none"})");
        Debug.Log($"[Attack][DualWield] isTwoHanding: {isTwoHanding}");
        Debug.Log($"[Attack][DualWield] needsPrompt: {needsPrompt}");

        return needsPrompt;
    }

    private void ShowDualWieldingPrompt(CharacterController attacker)
    {
        if (attacker == null)
            return;

        string message = "You have weapons in both hands.\nDo you want to dual wield?\n\n"
            + "Yes: Apply dual-wield penalties, off-hand attack available\n"
            + "No: No penalties, off-hand attack unavailable this round";

        CombatUI?.ShowConfirmationDialog(
            title: "Dual wield?",
            message: message,
            confirmLabel: "Yes",
            cancelLabel: "No",
            onConfirm: () => OnDualWieldingChoiceSelected(attacker, true),
            onCancel: () => OnDualWieldingChoiceSelected(attacker, false));
    }

    private void ApplyDualWieldingChoiceState(CharacterController attacker, bool dualWield, string contextTag)
    {
        if (attacker == null)
            return;

        Debug.Log($"=== DUAL WIELD PROMPT [{contextTag}] ===");
        Debug.Log($"[{contextTag}][DualWield] Choice selected: {(dualWield ? "Yes" : "No")}");

        _dualWieldingChoiceMade = true;

        if (dualWield)
        {
            _isDualWielding = true;
            CalculateDualWieldingPenalties(attacker);

            _offHandAttackAvailableThisTurn = attacker.HasOffHandWeaponEquipped();
            _offHandAttackUsedThisTurn = false;

            Debug.Log($"[{contextTag}][DualWield] Dual wielding enabled");
            Debug.Log($"[{contextTag}][DualWield] Off-hand attack available this turn: {_offHandAttackAvailableThisTurn}");
            Debug.Log($"[{contextTag}][DualWield] Main hand penalty: {_mainHandPenalty}");
            Debug.Log($"[{contextTag}][DualWield] Off-hand penalty: {_offHandPenalty}");

            CombatUI?.ShowCombatLog($"⚔ {attacker.Stats.CharacterName} dual wields (Main hand penalty: {_mainHandPenalty}, Off-hand penalty: {_offHandPenalty}).");
        }
        else
        {
            _isDualWielding = false;
            _mainHandPenalty = 0;
            _offHandPenalty = 0;

            _offHandAttackAvailableThisTurn = false;
            _offHandAttackUsedThisTurn = false;

            Debug.Log($"[{contextTag}][DualWield] Dual wielding disabled");
            Debug.Log($"[{contextTag}][DualWield] Off-hand attack available this turn: false");

            CombatUI?.ShowCombatLog($"⚔ {attacker.Stats.CharacterName} fights with main hand only (no dual-wield penalties). Off-hand attack disabled for this round.");
        }

        Debug.Log($"[{contextTag}][DualWield] Choice: {(dualWield ? "Yes" : "No")}");
        Debug.Log($"[{contextTag}][OffHand] _offHandAttackAvailableThisTurn: {_offHandAttackAvailableThisTurn}");
        Debug.Log($"[{contextTag}][OffHand] _offHandAttackUsedThisTurn: {_offHandAttackUsedThisTurn}");
    }

    private void OnDualWieldingChoiceSelected(CharacterController attacker, bool dualWield)
    {
        if (attacker == null)
            return;

        ApplyDualWieldingChoiceState(attacker, dualWield, "Attack");

        _pendingDefensiveAttackSelection = false;
        attacker.SetFightingDefensively(false);
        Debug.Log($"[Attack][DualWield] Continuing with pending attack type: {_pendingAttackType}");
        StartAttackSequence(attacker, _pendingAttackType);
    }


    private void CalculateDualWieldingPenalties(CharacterController attacker)
    {
        Debug.Log("[DualWield] Calculating penalties");

        if (attacker == null)
        {
            _mainHandPenalty = 0;
            _offHandPenalty = 0;
            Debug.Log("[DualWield] No attacker. Penalties reset to 0/0.");
            return;
        }

        ItemData mainWeapon = attacker.GetDualWieldMainWeapon();
        ItemData offWeapon = attacker.GetDualWieldOffHandWeapon();
        bool hasTWF = attacker.Stats != null && attacker.Stats.HasFeat("Two-Weapon Fighting");
        bool lightOffHand = attacker.IsOffHandWeaponLight();

        (int mainPenalty, int offPenalty) = attacker.Stats != null
            ? FeatManager.GetTWFPenalties(attacker.Stats, lightOffHand)
            : (lightOffHand ? (-4, -8) : (-6, -10));
        _mainHandPenalty = mainPenalty;
        _offHandPenalty = offPenalty;

        string mainType = (mainWeapon != null && (mainWeapon.IsLightWeapon || mainWeapon.WeaponSize == WeaponSizeCategory.Light)) ? "light" : "normal";
        string offType = lightOffHand ? "light" : "normal";

        Debug.Log($"[DualWield] Main hand weapon: {mainWeapon?.Name ?? "None"} ({mainType})");
        Debug.Log($"[DualWield] Off-hand weapon: {offWeapon?.Name ?? "None"} ({offType})");
        Debug.Log($"[DualWield] TWF feat: {hasTWF}");
        Debug.Log($"[DualWield] Light off-hand: {lightOffHand}");
        Debug.Log($"[DualWield] Penalties: Main {_mainHandPenalty}, Off-hand {_offHandPenalty}");
    }

    public void OnOffHandAttackButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null)
            return;

        Debug.Log("[Attack][OffHand] Off-hand attack button pressed");
        Debug.Log($"[Attack][OffHand] used={_offHandAttackUsedThisTurn} inSequence={_isInAttackSequence} attacksUsed={_totalAttacksUsed}");

        if (RedirectPinnedCharacterToGrappleMenu(pc, "off-hand attacks"))
            return;

        if (IsActionBlockedByTurnedCondition(pc, "off-hand attacks"))
        {
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        if (!CanUseOffHandAttackOption(pc))
        {
            Debug.Log($"[Attack][OffHand] Attack denied actor={pc.Stats.CharacterName} hasStandard={pc.Actions.HasStandardAction} hasMove={pc.Actions.HasMoveAction} inSequence={_isInAttackSequence} offHandUsed={_offHandAttackUsedThisTurn}");
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        ItemData offHandWeapon = pc.GetOffHandAttackWeapon();
        if (offHandWeapon == null)
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} has no valid off-hand weapon.");
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        if (!pc.CanAttackWithWeapon(offHandWeapon, out string cannotAttackReason))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot off-hand attack: {cannotAttackReason}");
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        _pendingDefensiveAttackSelection = false;
        pc.SetFightingDefensively(false);

        // If off-hand is the first attack this turn, auto-enable dual wielding.
        if (!_dualWieldingChoiceMade)
        {
            Debug.Log("[Attack][OffHand] First attack is off-hand, automatically enabling dual wielding.");
            _dualWieldingChoiceMade = true;
            _isDualWielding = true;
            CalculateDualWieldingPenalties(pc);
            _offHandAttackAvailableThisTurn = true;
            _offHandAttackUsedThisTurn = false;
            Debug.Log("[OffHand] Off-hand attack available this turn: true");
            CombatUI?.ShowCombatLog($"⚔ {pc.Stats.CharacterName} dual wields (Main hand penalty: {_mainHandPenalty}, Off-hand penalty: {_offHandPenalty}).");
        }

        // NOTE: Do not consume standard action yet. We consume only after a valid target is selected
        // so cancelling target selection does not spend the off-hand attack.
        if (_isInAttackSequence)
            Debug.Log("[Attack][OffHand] Executing during iterative sequence; no additional action cost.");

        int baseBab = pc.Stats != null ? pc.Stats.BaseAttackBonus : 0;
        int offHandPenalty = _isDualWielding ? _offHandPenalty : 0;
        _currentOffHandBAB = baseBab + offHandPenalty;
        _currentOffHandWeapon = offHandWeapon;

        Debug.Log($"[Attack][OffHand] weapon={offHandWeapon.Name} baseBAB={baseBab} penalty={offHandPenalty} attackBAB={_currentOffHandBAB}");

        BeginOffHandTargetSelection(pc, AttackType.Melee);
    }

    public void OnOffHandThrownAttackButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null)
            return;

        Debug.Log("[Attack][OffHand][Thrown] Off-hand thrown attack button pressed");
        Debug.Log($"[Attack][OffHand][Thrown] offHandUsed={_offHandAttackUsedThisTurn} inSequence={_isInAttackSequence} mainHandAttacksUsed={_totalAttacksUsed}");

        if (RedirectPinnedCharacterToGrappleMenu(pc, "off-hand thrown attacks"))
            return;

        if (IsActionBlockedByTurnedCondition(pc, "off-hand thrown attacks"))
        {
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        if (!CanUseOffHandThrownAttackOption(pc))
        {
            Debug.Log($"[Attack][OffHand][Thrown] Attack denied actor={pc.Stats.CharacterName} hasStandard={pc.Actions.HasStandardAction} hasMove={pc.Actions.HasMoveAction} inSequence={_isInAttackSequence} offHandUsed={_offHandAttackUsedThisTurn}");
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        ItemData offHandWeapon = pc.GetOffHandAttackWeapon();
        if (offHandWeapon == null || !offHandWeapon.IsThrown || offHandWeapon.RangeIncrement <= 0)
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} has no throwable off-hand weapon.");
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        if (!pc.CanAttackWithWeapon(offHandWeapon, out string cannotAttackReason))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot off-hand throw: {cannotAttackReason}");
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        _pendingDefensiveAttackSelection = false;
        pc.SetFightingDefensively(false);

        if (!_dualWieldingChoiceMade)
        {
            Debug.Log("[Attack][OffHand][Thrown] First attack is off-hand thrown, automatically enabling dual wielding.");
            _dualWieldingChoiceMade = true;
            _isDualWielding = true;
            CalculateDualWieldingPenalties(pc);
            _offHandAttackAvailableThisTurn = true;
            _offHandAttackUsedThisTurn = false;
            Debug.Log("[OffHand][Thrown] Off-hand attack available this turn: true");
            CombatUI?.ShowCombatLog($"⚔ {pc.Stats.CharacterName} dual wields (Main hand penalty: {_mainHandPenalty}, Off-hand penalty: {_offHandPenalty}).");
        }

        // NOTE: Do not consume standard action yet. We consume only after a valid target is selected
        // so cancelling target selection does not spend the off-hand thrown attack.
        if (_isInAttackSequence)
            Debug.Log("[Attack][OffHand][Thrown] Executing during iterative sequence; no additional action cost.");

        int baseBab = pc.Stats != null ? pc.Stats.BaseAttackBonus : 0;
        int offHandPenalty = _isDualWielding ? _offHandPenalty : 0;
        _currentOffHandBAB = baseBab + offHandPenalty;
        _currentOffHandWeapon = offHandWeapon;

        Debug.Log($"[Attack][OffHand][Thrown] weapon={offHandWeapon.Name} baseBAB={baseBab} penalty={offHandPenalty} attackBAB={_currentOffHandBAB}");

        BeginOffHandTargetSelection(pc, AttackType.Thrown);
    }

    private void BeginOffHandTargetSelection(CharacterController attacker, AttackType attackType)
    {
        if (attacker == null)
            return;

        _isSelectingOffHandTarget = true;
        _isSelectingOffHandThrownTarget = attackType == AttackType.Thrown;
        _pendingAttackMode = PendingAttackMode.Single;
        _currentAttackType = attackType;
        CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;

        Debug.Log($"[Attack][OffHand] Begin target selection attacker={attacker.Stats.CharacterName} attackType={attackType} selectingThrown={_isSelectingOffHandThrownTarget} weapon={_currentOffHandWeapon?.Name ?? "none"}");
        ShowOffHandAttackTargets(attacker, _currentOffHandWeapon, _isSelectingOffHandThrownTarget);
    }

    private void ShowOffHandAttackTargets(CharacterController attacker, ItemData offHandWeapon, bool useThrownRange)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        bool hasTarget = false;
        bool anyFlanking = false;
        List<CharacterController> allCombatants = GetAllCharacters();

        bool hasValidThrownWeapon = useThrownRange
            && offHandWeapon != null
            && offHandWeapon.IsThrown
            && offHandWeapon.RangeIncrement > 0;

        if (hasValidThrownWeapon)
        {
            int maxRangeSquares = RangeCalculator.GetMaxRangeSquares(offHandWeapon.RangeIncrement, true);
            ShowRangeZoneHighlights(attacker, offHandWeapon.RangeIncrement, maxRangeSquares, true);
        }

        foreach (CharacterController candidate in allCombatants)
        {
            if (candidate == null || candidate == attacker || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!IsEnemyTeam(attacker, candidate))
                continue;

            bool inRange;
            if (useThrownRange)
            {
                if (!hasValidThrownWeapon)
                    continue;

                int sqDist = attacker.GetMinimumDistanceToTarget(candidate, chebyshev: false);
                RangeInfo rangeInfo = RangeCalculator.GetRangeInfo(sqDist, offHandWeapon.RangeIncrement, true);
                inRange = rangeInfo != null && rangeInfo.IsInRange;
            }
            else
            {
                int distance = attacker.GetMinimumDistanceToTarget(candidate, chebyshev: true);
                inRange = attacker.CanMeleeAttackDistance(distance, offHandWeapon);
            }

            if (!inRange)
                continue;

            SquareCell targetCell = Grid.GetCell(candidate.GridPosition);
            if (targetCell == null)
                continue;

            bool flanking = !useThrownRange && CombatUtils.IsAttackerFlanking(attacker, candidate, allCombatants, out _);
            HighlightType highlightType = useThrownRange
                ? HighlightType.Attack
                : (flanking ? HighlightType.Flanking : HighlightType.AttackRange);
            targetCell.SetHighlight(highlightType);
            _highlightedCells.Add(targetCell);
            hasTarget = true;
            anyFlanking |= flanking;
        }

        Debug.Log($"[Attack][OffHand] Target scan complete attacker={attacker.Stats.CharacterName} mode={(useThrownRange ? "Thrown" : "Melee")} highlightedTargets={_highlightedCells.Count} weapon={offHandWeapon?.Name ?? "none"}");

        if (hasTarget)
        {
            string weaponName = offHandWeapon != null ? offHandWeapon.Name : "Off-hand";
            if (useThrownRange)
            {
                string rangeText = string.Empty;
                if (hasValidThrownWeapon)
                {
                    int maxRangeFeet = RangeCalculator.GetMaxRangeFeet(offHandWeapon.RangeIncrement, true);
                    rangeText = $" ({offHandWeapon.RangeIncrement} ft increment, max {maxRangeFeet} ft)";
                }

                CombatUI.SetTurnIndicator($"OFF-HAND THROWN ATTACK ({weaponName}){rangeText}: Click an enemy to attack!");
            }
            else
            {
                string flankText = anyFlanking ? " (FLANKING available! +2 to hit)" : string.Empty;
                CombatUI.SetTurnIndicator($"OFF-HAND ATTACK ({weaponName}): Click an enemy to attack!{flankText}");
            }
        }
        else
        {
            _isSelectingOffHandTarget = false;
            _isSelectingOffHandThrownTarget = false;
            _currentOffHandBAB = 0;
            _currentOffHandWeapon = null;
            string mode = useThrownRange ? "throw" : "melee";
            CombatUI.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} has no enemies in off-hand {mode} range.");
            StartCoroutine(ReturnToActionChoicesAfterDelay(0.9f));
        }
    }

    public bool IsIterativeAttackSequenceActiveFor(CharacterController actor)
    {
        return actor != null
            && _isInAttackSequence
            && _attackingCharacter == actor
            && HasMoreAttacksAvailable();
    }

    public bool IsIterativeAttackInFullRoundStage(CharacterController actor)
    {
        return IsIterativeAttackSequenceActiveFor(actor) && _attackSequenceConsumesFullRound;
    }

    public string GetIterativeAttackButtonLabel(CharacterController actor, bool usingUnarmedStrike, string attackSourceLabel)
    {
        if (IsIterativeAttackInFullRoundStage(actor))
            return "Attack (Full Round)";

        return usingUnarmedStrike ? $"Attack (Standard, {attackSourceLabel})" : "Attack (Standard)";
    }

    public bool IsIterativeThrownAttackSequenceActiveFor(CharacterController actor)
    {
        return actor != null
            && _isInAttackSequence
            && _attackingCharacter == actor
            && HasMoreAttacksAvailable()
            && HasThrowableMeleeWeaponEquipped(actor);
    }

    public bool IsIterativeThrownAttackInFullRoundStage(CharacterController actor)
    {
        return IsIterativeThrownAttackSequenceActiveFor(actor) && _attackSequenceConsumesFullRound;
    }

    private bool CanAttack(CharacterController actor)
    {
        if (actor == null)
            return false;

        if (actor.HasCondition(CombatConditionType.Turned))
            return false;

        if (_isInAttackSequence && _attackingCharacter == actor)
            return HasMoreAttacksAvailable();

        if (actor.Actions == null)
            return false;

        // Off-hand-first flow: if off-hand already consumed the standard action,
        // allow starting the main-hand iterative sequence by consuming move as full-round conversion.
        if (_offHandAttackUsedThisTurn && _offHandAttackAvailableThisTurn && actor == ActivePC && actor.Actions.HasMoveAction)
            return true;

        return actor.Actions.HasStandardAction;
    }

    private bool CanThrowWeapon(CharacterController actor)
    {
        if (actor == null)
            return false;

        if (actor.HasCondition(CombatConditionType.Turned))
            return false;

        ItemData weapon = actor.GetEquippedWeapon();
        if (weapon == null || !weapon.IsThrown || weapon.RangeIncrement <= 0)
            return false;

        if (_isInAttackSequence)
            return _attackingCharacter == actor && HasMoreAttacksAvailable();

        if (actor.Actions == null)
            return false;

        if (_offHandAttackUsedThisTurn && _offHandAttackAvailableThisTurn && actor == ActivePC && actor.Actions.HasMoveAction)
            return true;

        return actor.Actions.HasStandardAction;
    }

    public bool CanUseThrownAttackOption(CharacterController actor)
    {
        if (actor == null)
            return false;

        return CanThrowWeapon(actor);
    }

    private bool IsActionBlockedByTurnedCondition(CharacterController actor, string attemptedAction)
    {
        if (actor == null || !actor.HasCondition(CombatConditionType.Turned))
            return false;

        string actionLabel = string.IsNullOrWhiteSpace(attemptedAction) ? "that action" : attemptedAction;
        CombatUI?.ShowCombatLog($"⚠ {actor.Stats.CharacterName} is Turned and cannot perform {actionLabel}. They must flee from the source of divine turning.");
        return true;
    }

    public bool HasThrowableMeleeWeaponEquipped(CharacterController actor)
    {
        return actor != null && actor.HasThrowableWeaponEquipped();
    }

    private bool IsOffHandAttackAvailable()
    {
        bool available = _offHandAttackAvailableThisTurn && !_offHandAttackUsedThisTurn;

        Debug.Log("[OffHand] Checking availability");
        Debug.Log($"[OffHand] _offHandAttackAvailableThisTurn: {_offHandAttackAvailableThisTurn}");
        Debug.Log($"[OffHand] _offHandAttackUsedThisTurn: {_offHandAttackUsedThisTurn}");
        Debug.Log($"[OffHand] Available: {available}");

        return available;
    }

    public bool CanUseOffHandAttackOption(CharacterController actor)
    {
        if (actor == null || actor.Actions == null)
        {
            Debug.Log("[OffHand][CanUse] Denied: actor/actions null.");
            return false;
        }

        if (actor != ActivePC)
        {
            Debug.Log($"[OffHand][CanUse] Denied: actor {actor.Stats?.CharacterName ?? "<null>"} is not ActivePC {(ActivePC != null && ActivePC.Stats != null ? ActivePC.Stats.CharacterName : "<none>")}.");
            return false;
        }

        if (actor.HasCondition(CombatConditionType.Pinned))
        {
            Debug.Log($"[OffHand][CanUse] Denied: {actor.Stats?.CharacterName ?? "<null>"} is pinned.");
            return false;
        }

        if (actor.IsTwoHanding())
        {
            Debug.Log($"[OffHand][CanUse] Denied: {actor.Stats?.CharacterName ?? "<null>"} is using a two-handed weapon.");
            return false;
        }

        if (!actor.HasOffHandWeaponEquipped())
        {
            Debug.Log($"[OffHand][CanUse] Denied: {actor.Stats?.CharacterName ?? "<null>"} has no off-hand weapon equipped.");
            return false;
        }

        bool availableByFlag = IsOffHandAttackAvailable();
        if (!availableByFlag)
        {
            Debug.Log($"[OffHand][CanUse] Denied by flags: availableThisTurn={_offHandAttackAvailableThisTurn}, usedThisTurn={_offHandAttackUsedThisTurn}");
            return false;
        }

        if (_isInAttackSequence && _attackingCharacter != actor)
        {
            Debug.Log($"[OffHand][CanUse] Denied: attack sequence belongs to {( _attackingCharacter != null && _attackingCharacter.Stats != null ? _attackingCharacter.Stats.CharacterName : "<none>")}, not {actor.Stats?.CharacterName ?? "<null>"}.");
            return false;
        }

        // Dedicated flag controls availability. Action-economy checks are intentionally excluded.
        Debug.Log($"[OffHand][CanUse] Allowed for {actor.Stats?.CharacterName ?? "<null>"}. hasStandard={actor.Actions.HasStandardAction}, hasMove={actor.Actions.HasMoveAction}, inSequence={_isInAttackSequence}");
        return true;
    }

    public bool CanUseOffHandThrownAttackOption(CharacterController actor)
    {
        if (actor == null)
            return false;

        if (!CanUseOffHandAttackOption(actor))
            return false;

        ItemData offHandWeapon = actor.GetOffHandAttackWeapon();
        return offHandWeapon != null
            && offHandWeapon.IsThrown
            && offHandWeapon.RangeIncrement > 0;
    }

    public bool IsOffHandAttackUsedThisTurn(CharacterController actor)
    {
        return actor != null && actor == ActivePC && _offHandAttackUsedThisTurn;
    }

    public bool IsOffHandAttackAvailableThisTurn(CharacterController actor)
    {
        return actor != null
            && actor == ActivePC
            && actor.HasOffHandWeaponEquipped()
            && IsOffHandAttackAvailable();
    }

    private AttackType GetDefaultAttackType(CharacterController actor)
    {
        if (actor == null)
            return AttackType.Melee;

        ItemData weapon = actor.GetEquippedMainWeapon();
        if (weapon != null && weapon.WeaponCat == WeaponCategory.Ranged)
            return AttackType.Ranged;

        return AttackType.Melee;
    }

    private void StartAttackSequence(CharacterController attacker)
    {
        StartAttackSequence(attacker, GetDefaultAttackType(attacker));
    }

    private void StartAttackSequence(CharacterController attacker, AttackType attackType)
    {
        if (attacker == null)
            return;

        _attackingCharacter = attacker;
        _equippedWeapon = attacker.GetEquippedMainWeapon();

        _totalAttackBudget = Mathf.Max(1, attacker.GetIterativeAttackCount());
        _totalAttacksUsed = 0;
        _attackSequenceConsumesFullRound = false;
        _isInAttackSequence = true;

        Debug.Log($"[Attack][Sequence] {attacker.Stats.CharacterName} starting attack sequence");
        Debug.Log($"[Attack][Sequence] Total attacks available: {_totalAttackBudget}");
        Debug.Log($"[Attack][Sequence] First attack type: {attackType}");
        Debug.Log($"[Attack][Sequence] Off-hand already used this turn: {_offHandAttackUsedThisTurn}");

        bool offHandOpenedTurn = _offHandAttackUsedThisTurn && !attacker.Actions.HasStandardAction;
        if (offHandOpenedTurn)
        {
            if (attacker.Actions.HasMoveAction)
            {
                attacker.Actions.UseMoveAction();
                _attackSequenceConsumesFullRound = true;
                Debug.Log("[Attack][Sequence] Off-hand used first; consuming move action and entering full-round stage for main-hand iteratives.");
            }
            else
            {
                Debug.LogWarning($"[Attack][Sequence] Off-hand used first but {attacker.Stats.CharacterName} has no move action left; aborting sequence.");
                EndAttackSequence();
                CombatUI?.UpdateActionButtons(attacker);
                return;
            }
        }
        else
        {
            if (!attacker.CommitStandardAction())
            {
                Debug.LogWarning($"[Attack][Sequence] Failed to consume standard action for {attacker.Stats.CharacterName}; aborting sequence.");
                EndAttackSequence();
                CombatUI?.UpdateActionButtons(attacker);
                return;
            }
        }

        PerformAttackByType(attacker, attackType);
    }

    private void ContinueAttackSequence(CharacterController attacker)
    {
        ContinueAttackSequence(attacker, _currentAttackType);
    }

    private void ContinueAttackSequence(CharacterController attacker, AttackType attackType)
    {
        if (attacker == null)
            return;

        if (!_isInAttackSequence || _attackingCharacter != attacker)
        {
            Debug.LogWarning($"[Attack][Sequence] Continue requested with stale sequence for {attacker.Stats.CharacterName}; restarting.");
            StartAttackSequence(attacker, attackType);
            return;
        }

        Debug.Log($"[Attack][Sequence] {attacker.Stats.CharacterName} continuing attack sequence");
        Debug.Log($"[Attack][Sequence] Attack type: {attackType}");

        PerformAttackByType(attacker, attackType);
    }

    private void PerformAttackByType(CharacterController attacker, AttackType attackType)
    {
        if (attacker == null)
            return;

        if (!HasMoreAttacksAvailable())
        {
            Debug.Log("[Attack][Sequence] No attacks available while preparing; ending sequence.");
            EndAttackSequence();
            ShowActionChoices();
            return;
        }

        _equippedWeapon = attacker.GetEquippedMainWeapon();

        if (attackType == AttackType.Thrown)
        {
            if (_equippedWeapon == null || !_equippedWeapon.IsThrown || _equippedWeapon.RangeIncrement <= 0)
            {
                CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} has no throwable weapon equipped!");
                EndAttackSequence();
                ShowActionChoices();
                return;
            }

            if (!attacker.CanAttackWithWeapon(_equippedWeapon, out string cannotAttackReason))
            {
                CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} cannot throw: {cannotAttackReason}");
                EndAttackSequence();
                ShowActionChoices();
                return;
            }
        }

        int attackNumber = _totalAttacksUsed + 1;
        int baseBab = attacker.GetIterativeAttackBAB(_totalAttacksUsed);
        int attackBab = baseBab;

        // Apply dual-wield penalty to main-hand iterative attacks.
        if (_isDualWielding && (attackType == AttackType.Melee || attackType == AttackType.Thrown))
        {
            attackBab += _mainHandPenalty;
            Debug.Log($"[Attack][DualWield] Applying main-hand penalty: {_mainHandPenalty}");
        }

        _currentAttackBAB = attackBab;
        _currentAttackType = attackType;

        Debug.Log($"[Attack][Sequence] Performing attack #{attackNumber}/{_totalAttackBudget}");
        Debug.Log($"[Attack][Sequence] Attack type: {attackType}, Base BAB: {baseBab}, Final BAB: {attackBab}");

        _pendingAttackMode = PendingAttackMode.Single;
        CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
        ShowAttackTargets(attacker);
    }

    private bool HasMoreAttacksAvailable()
    {
        if (!_isInAttackSequence || _attackingCharacter == null)
            return false;

        bool hasMore = _totalAttacksUsed < _totalAttackBudget;
        Debug.Log($"[Attack][Sequence] Attacks used: {_totalAttacksUsed}/{_totalAttackBudget}, hasMore: {hasMore}");
        return hasMore;
    }

    private bool HasMoreThrowsAvailable()
    {
        return HasMoreAttacksAvailable();
    }


    private void EndAttackSequence()
    {
        Debug.Log("[Attack][Sequence] Ending attack sequence");
        Debug.Log($"[Attack][Sequence] Final state before teardown: attacksUsed={_totalAttacksUsed}/{_totalAttackBudget}, offHandUsed={_offHandAttackUsedThisTurn}, offHandAvailable={_offHandAttackAvailableThisTurn}");

        _totalAttacksUsed = 0;
        _totalAttackBudget = 0;
        _isInAttackSequence = false;
        _attackingCharacter = null;
        _equippedWeapon = null;
        _attackSequenceConsumesFullRound = false;
        _currentAttackBAB = 0;

        // Keep per-turn off-hand usage flag, but clear transient targeting state.
        _isSelectingOffHandTarget = false;
        _isSelectingOffHandThrownTarget = false;
        _currentOffHandBAB = 0;
        _currentOffHandWeapon = null;
    }

    private void EndThrownAttackSequence()
    {
        EndAttackSequence();
    }

    private void ResetOffHandTurnState()
    {
        _offHandAttackUsedThisTurn = false;
        _offHandAttackAvailableThisTurn = false;
        _isSelectingOffHandTarget = false;
        _isSelectingOffHandThrownTarget = false;
        _currentOffHandBAB = 0;
        _currentOffHandWeapon = null;

        _dualWieldingChoiceMade = false;
        _isDualWielding = false;
        _mainHandPenalty = 0;
        _offHandPenalty = 0;
        _pendingAttackType = AttackType.Melee;
        _pendingDisarmUseOffHandSelection = false;
        _pendingSunderUseOffHandSelection = false;
    }


    private bool CanOpenSpecialAttackMenu(CharacterController actor)
    {
        if (actor == null)
            return false;

        if (actor.HasCondition(CombatConditionType.Turned))
            return false;

        bool hasGrappleAttackAvailable = CanUseGrappleAttackOption(actor);
        bool hasBullRushAttackAvailable = CanUseBullRushAttackOption(actor);
        bool hasTripAttackAvailable = CanUseTripAttackOption(actor);
        bool hasDisarmAttackAvailable = CanUseDisarmAttackOption(actor);
        bool hasSunderAttackAvailable = CanUseSunderAttackOption(actor);
        return actor.Actions.HasStandardAction
            || actor.Actions.HasFullRoundAction
            || CanUseImprovedFeintAsMove(actor)
            || hasGrappleAttackAvailable
            || hasBullRushAttackAvailable
            || hasTripAttackAvailable
            || hasDisarmAttackAvailable
            || hasSunderAttackAvailable;
    }



    public void OnSpecialAttackButtonPressed()
    {
        CharacterController pc = ActivePC;
        bool canOpen = pc != null && CanOpenSpecialAttackMenu(pc);
        Debug.Log($"[GameManager][SpecialAttack] ButtonPressed actor={(pc != null && pc.Stats != null ? pc.Stats.CharacterName : "<null>")} canOpen={canOpen} phase={CurrentPhase} subPhase={CurrentSubPhase} std={(pc != null ? pc.Actions.HasStandardAction : false)} full={(pc != null ? pc.Actions.HasFullRoundAction : false)} grappleAvailable={(pc != null ? CanUseGrappleAttackOption(pc) : false)} bullRushAvailable={(pc != null ? CanUseBullRushAttackOption(pc) : false)} tripAvailable={(pc != null ? CanUseTripAttackOption(pc) : false)} disarmAvailable={(pc != null ? CanUseDisarmAttackOption(pc) : false)} sunderAvailable={(pc != null ? CanUseSunderAttackOption(pc) : false)}");
        if (!canOpen) return;

        if (RedirectPinnedCharacterToGrappleMenu(pc, "special attacks"))
            return;

        _isSelectingSpecialAttack = true;
        CombatUI.ShowSpecialAttackMenu(pc, OnSpecialAttackSelected, ShowActionChoices);
    }


    private void OnSpecialAttackSelected(SpecialAttackType type, bool useOffHandDisarm)
    {
        CharacterController pc = ActivePC;
        if (pc == null) { ShowActionChoices(); return; }

        if (pc.HasCondition(CombatConditionType.Turned) && type != SpecialAttackType.TurnUndead)
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} is Turned and cannot perform offensive special attacks.");
            ShowActionChoices();
            return;
        }

        if (type == SpecialAttackType.AidAnother)
        {
            if (!CanUseAidAnother(pc, out string aidAnotherReason))
            {
                CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot Aid Another: {aidAnotherReason}.");
                ShowActionChoices();
                return;
            }

            Debug.Log($"[GameManager][SpecialAttack] Redirecting {pc.Stats.CharacterName} to Aid Another flow from special attack menu.");
            CombatUI.HideSpecialAttackMenu();
            OnAidAnotherButtonPressed();
            return;
        }

        if (type == SpecialAttackType.Overrun)
        {
            Debug.Log($"[Overrun][UI] Special Attack menu selected Overrun for {pc.Stats.CharacterName}. Using destination selection flow.");

            if (!CanUseOverrun(pc, out string overrunReason))
            {
                CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot use Overrun: {overrunReason}.");
                ShowActionChoices();
                return;
            }

            StartOverrunDestinationSelection(pc);
            return;
        }

        if (type == SpecialAttackType.TurnUndead)
        {
            CombatUI.HideSpecialAttackMenu();
            EnterTurnUndeadTargeting(pc);
            return;
        }

        bool hasGrappleAttackAvailable = CanUseGrappleAttackOption(pc);
        bool hasBullRushAttackAvailable = CanUseBullRushAttackOption(pc);
        bool hasTripAttackAvailable = CanUseTripAttackOption(pc);
        bool hasMainHandDisarmAttackAvailable = CanUseMainHandDisarmAttackOption(pc);
        bool hasOffHandDisarmAttackAvailable = CanUseOffHandDisarmAttackOption(pc);
        bool hasDisarmAttackAvailable = useOffHandDisarm ? hasOffHandDisarmAttackAvailable : hasMainHandDisarmAttackAvailable;
        bool hasMainHandSunderAttackAvailable = CanUseMainHandSunderAttackOption(pc);
        bool hasOffHandSunderAttackAvailable = CanUseOffHandSunderAttackOption(pc);
        bool hasSunderAttackAvailable = useOffHandDisarm ? hasOffHandSunderAttackAvailable : hasMainHandSunderAttackAvailable;

        if (type == SpecialAttackType.Disarm && !useOffHandDisarm && !_dualWieldingChoiceMade && NeedsDualWieldingPrompt(pc))
        {
            Debug.Log($"[Disarm][DualWield] Showing dual wield prompt before main-hand disarm for {pc.Stats.CharacterName}.");
            CombatUI.HideSpecialAttackMenu();
            ShowDualWieldingPromptForDisarm(pc);
            return;
        }

        if (type == SpecialAttackType.Sunder && !useOffHandDisarm && !_dualWieldingChoiceMade && NeedsDualWieldingPrompt(pc))
        {
            Debug.Log($"[Sunder][DualWield] Showing dual wield prompt before main-hand sunder for {pc.Stats.CharacterName}.");
            CombatUI.HideSpecialAttackMenu();
            ShowDualWieldingPromptForSunder(pc);
            return;
        }

        bool hasAction = type == SpecialAttackType.Feint
            ? (pc.Actions.HasStandardAction || CanUseImprovedFeintAsMove(pc))
            : (type == SpecialAttackType.Grapple
                ? hasGrappleAttackAvailable
                : (type == SpecialAttackType.BullRushAttack
                    ? hasBullRushAttackAvailable
                    : (type == SpecialAttackType.Trip
                        ? hasTripAttackAvailable
                        : (type == SpecialAttackType.Disarm
                            ? hasDisarmAttackAvailable
                            : (type == SpecialAttackType.Sunder
                                ? hasSunderAttackAvailable
                                : (type == SpecialAttackType.BullRushCharge
                                    ? pc.Actions.HasFullRoundAction
                                    : pc.Actions.HasStandardAction))))));

        Debug.Log($"[GameManager][SpecialAttack] Selected type={type} actor={pc.Stats.CharacterName} allowed={hasAction} phase={CurrentPhase} subPhase={CurrentSubPhase} std={pc.Actions.HasStandardAction} full={pc.Actions.HasFullRoundAction} grappleAvailable={hasGrappleAttackAvailable} bullRushAvailable={hasBullRushAttackAvailable} tripAvailable={hasTripAttackAvailable} mainDisarmAvailable={hasMainHandDisarmAttackAvailable} offHandDisarmAvailable={hasOffHandDisarmAttackAvailable} mainSunderAvailable={hasMainHandSunderAttackAvailable} offHandSunderAvailable={hasOffHandSunderAttackAvailable} requestedOffHand={useOffHandDisarm}");

        if (!hasAction)
        {
            string reason = type == SpecialAttackType.Feint
                ? "Need a standard action, or a move action with Improved Feint"
                : (type == SpecialAttackType.Grapple
                    ? "Need at least one remaining grapple attack"
                    : (type == SpecialAttackType.BullRushAttack
                        ? "Need at least one remaining bull rush attack"
                        : (type == SpecialAttackType.Trip
                            ? "Need at least one remaining trip attack"
                            : (type == SpecialAttackType.Disarm
                                ? (useOffHandDisarm ? "Need an available off-hand disarm attack" : "Need at least one remaining main-hand disarm attack")
                                : (type == SpecialAttackType.Sunder
                                    ? (useOffHandDisarm ? "Need an available off-hand sunder attack" : "Need at least one remaining main-hand sunder attack")
                                    : (type == SpecialAttackType.BullRushCharge
                                        ? "Need a full-round action and valid charge movement"
                                        : "Need a standard action"))))));
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot use {type}: {reason}.");
            ShowActionChoices();
            return;
        }

        CombatUI.HideSpecialAttackMenu();

        if (type == SpecialAttackType.BullRushCharge)
        {
            EnterBullRushChargeMode(pc);
            return;
        }

        if (type == SpecialAttackType.Grapple
            && pc.TryGetGrappleState(out _, out _, out _, out _))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} is already grappling. Use the grapple action buttons in the action panel.");
            ShowActionChoices();
            return;
        }

        _pendingSpecialAttackType = type;
        _pendingDisarmUseOffHandSelection = type == SpecialAttackType.Disarm && useOffHandDisarm;
        _pendingSunderUseOffHandSelection = type == SpecialAttackType.Sunder && useOffHandDisarm;
        _isSelectingSpecialAttack = true;
        CurrentSubPhase = PlayerSubPhase.SelectingSpecialTarget;
        ShowSpecialAttackTargets(pc, type);
    }


    public void OnFullAttackButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || !pc.Actions.HasFullRoundAction) return;

        if (RedirectPinnedCharacterToGrappleMenu(pc, "full attacks"))
            return;

        if (IsActionBlockedByTurnedCondition(pc, "full attacks"))
        {
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        if (!pc.CanAttackWithEquippedWeapon(out string cannotAttackReason))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot full attack: {cannotAttackReason}");
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        _pendingDefensiveAttackSelection = false;
        pc.SetFightingDefensively(false);
        EndAttackSequence();
        _currentAttackType = GetDefaultAttackType(pc);

        _pendingAttackMode = PendingAttackMode.FullAttack;
        CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
        ShowAttackTargets(pc);
    }

    public void OnAttackDefensivelyButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || pc.Stats == null || !pc.Actions.HasStandardAction) return;

        if (RedirectPinnedCharacterToGrappleMenu(pc, "fighting defensively"))
            return;

        if (IsActionBlockedByTurnedCondition(pc, "fighting defensively"))
        {
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        if (!pc.CanAttackWithEquippedWeapon(out string cannotAttackReason))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot attack: {cannotAttackReason}");
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        if (pc.Stats.BaseAttackBonus < 1)
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} needs BAB +1 to fight defensively.");
            return;
        }

        _pendingDefensiveAttackSelection = true;
        pc.SetFightingDefensively(true);
        EndAttackSequence();
        _currentAttackType = GetDefaultAttackType(pc);

        _pendingAttackMode = PendingAttackMode.Single;
        CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
        ShowAttackTargets(pc);
        CombatUI?.SetTurnIndicator("FIGHTING DEFENSIVELY (STD): Select target");
        CombatUI?.ShowCombatLog($"🛡 {pc.Stats.CharacterName} declares Fighting Defensively (Std): -4 attack, +2 AC.");
        UpdateAllStatsUI();
    }

    public void OnFullAttackDefensivelyButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || pc.Stats == null || !pc.Actions.HasFullRoundAction) return;

        if (RedirectPinnedCharacterToGrappleMenu(pc, "full attacks"))
            return;

        if (IsActionBlockedByTurnedCondition(pc, "full attacks"))
        {
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        if (!pc.CanAttackWithEquippedWeapon(out string cannotAttackReason))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot full attack: {cannotAttackReason}");
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        if (pc.Stats.BaseAttackBonus < 1)
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} needs BAB +1 to fight defensively.");
            return;
        }

        _pendingDefensiveAttackSelection = true;
        pc.SetFightingDefensively(true);
        EndAttackSequence();
        _currentAttackType = GetDefaultAttackType(pc);

        _pendingAttackMode = PendingAttackMode.FullAttack;
        CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
        ShowAttackTargets(pc);
        CombatUI?.SetTurnIndicator("FULL ATTACK (DEF): Select target");
        CombatUI?.ShowCombatLog($"🛡 {pc.Stats.CharacterName} declares Full Attack (Def): -4 attack, +2 AC.");
        UpdateAllStatsUI();
    }

    public void OnDualWieldButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || !pc.Actions.HasFullRoundAction) return;

        if (RedirectPinnedCharacterToGrappleMenu(pc, "dual-wield attacks"))
            return;

        if (IsActionBlockedByTurnedCondition(pc, "dual-wield attacks"))
        {
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        if (pc.HasCondition(CombatConditionType.Grappled))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot dual-wield while grappled (D&D 3.5: no two-weapon attacks in a grapple).");
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        if (!pc.CanDualWield()) return;

        ItemData main = pc.GetDualWieldMainWeapon();
        ItemData off = pc.GetDualWieldOffHandWeapon();
        bool canMain = pc.CanAttackWithWeapon(main, out string mainReason);
        bool canOff = pc.CanAttackWithWeapon(off, out string offReason);
        if (!canMain && !canOff)
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot dual-wield attack: {mainReason}");
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        EndAttackSequence();
        _pendingAttackMode = PendingAttackMode.DualWield;
        CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;

        var (mainPen, offPen, lightOff) = pc.GetDualWieldPenalties();
        string penaltyInfo = lightOff ? $"(light off-hand: {mainPen}/{offPen})" : $"(penalties: {mainPen}/{offPen})";
        string offHandInfo = pc.IsDualWieldOffHandSpikedGauntlet()
            ? " [Off-hand: Spiked Gauntlet]"
            : pc.IsDualWieldOffHandShieldBash()
                ? (FeatManager.HasImprovedShieldBash(pc.Stats)
                    ? " [Off-hand: Shield Bash, Improved Shield Bash keeps shield AC]"
                    : " [Off-hand: Shield Bash, shield AC lost until next turn]")
                : string.Empty;

        ShowAttackTargets(pc);
        CombatUI.SetTurnIndicator($"DUAL WIELD: Select target {penaltyInfo}{offHandInfo}");
    }

    public void OnEndTurnButtonPressed()
    {
        if (!IsPlayerTurn) return;

        CharacterController pc = ActivePC;
        if (IsHoldingTouchCharge(pc))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} ends turn while holding {GetHeldTouchSpellName(pc)}. The charge persists.");
        }

        EndAttackSequence();
        EndThrownAttackSequence();
        ResetOffHandTurnState();
        EndCurrentTurn();
    }

    public void EndCurrentTurn()
    {
        if (CurrentPhase == TurnPhase.CombatOver)
            return;

        if (IsPlayerTurn)
            EndActivePCTurn();
        else
            NextInitiativeTurn();
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


    public void OnDamageModeTogglePressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null)
            return;

        pc.ToggleAttackDamageMode();
        CombatUI?.UpdateDamageModeToggle(pc);
        CombatUI?.UpdateActionButtons(pc);

        string modeLabel = pc.CurrentAttackDamageMode == AttackDamageMode.Nonlethal ? "Nonlethal" : "Lethal";
        CombatUI?.ShowCombatLog($"🗡 {pc.Stats.CharacterName} switches damage mode to {modeLabel}.");
    }
    public void OnFlurryOfBlowsButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || !pc.Stats.IsMonk || !pc.Actions.HasFullRoundAction) return;

        if (RedirectPinnedCharacterToGrappleMenu(pc, "flurry of blows"))
            return;

        if (IsActionBlockedByTurnedCondition(pc, "flurry of blows"))
        {
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        EndAttackSequence();
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

        if (IsActionBlockedByTurnedCondition(pc, "spellcasting"))
        {
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        var spellComp = pc.GetComponent<SpellcastingComponent>();
        if (spellComp == null) return;

        // Casting can only begin if there is a castable prepared spell.
        // Held charges are delivered via the dedicated Discharge button.
        if (!spellComp.HasAnyCastablePreparedSpell())
        {
            Debug.Log($"[GameManager] {pc.Stats.CharacterName}: No prepared spells with available slots to cast.");
            return;
        }

        if (pc.IsGrappling())
            CombatUI?.ShowCombatLog("🪢 Grappled casting: you must satisfy component restrictions and pass a concentration check (DC 20 + spell level).");
        // Show spell selection panel with metamagic support (only prepared spells shown)
        CombatUI.SetActionButtonsVisible(false);
        CombatUI.ShowSpellSelection(spellComp, OnSpellSelectedWithMetamagic, OnSpellSelectionCancelled);
    }

    /// <summary>
    /// Called by the Discharge button to deliver an already-held touch spell.
    /// </summary>
    public void OnDischargeHeldTouchButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;

        var spellComp = pc.GetComponent<SpellcastingComponent>();
        if (spellComp == null || !spellComp.HasHeldTouchCharge || spellComp.HeldTouchSpell == null)
            return;

        _pendingSpell = spellComp.HeldTouchSpell;
        _pendingMetamagic = spellComp.HeldTouchMetamagic;
        _pendingSpellFromHeldCharge = true;

        _pendingAttackMode = PendingAttackMode.CastSpell;
        CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
        ShowSpellTargets(pc, _pendingSpell);
    }

    /// <summary>Called when a spell is chosen from the spell selection panel (with optional metamagic).</summary>
    private void OnSpellSelectedWithMetamagic(SpellData spell, MetamagicData metamagic)
    {
        CharacterController pc = ActivePC;
        if (pc == null) { ShowActionChoices(); return; }

        _pendingSpell = spell;
        _pendingMetamagic = metamagic;
        _pendingSpellFromHeldCharge = false;

        // Casting another spell while holding a touch charge ends the held charge.
        var spellComp = pc.GetComponent<SpellcastingComponent>();
        if (spellComp != null && spellComp.HasHeldTouchCharge)
        {
            spellComp.ClearHeldTouchCharge("cast another spell");
            CombatUI?.ShowCombatLog($"{pc.Stats.CharacterName}'s held touch charge dissipates as they begin another spell.");
        }

        // If metamagic modifies the spell data (range, action type), clone and apply
        if (metamagic != null && metamagic.HasAnyMetamagic)
        {
            _pendingSpell = spell.Clone();
            SpellCaster.ApplyMetamagicToSpellData(_pendingSpell, metamagic);
            Debug.Log($"[GameManager] Metamagic applied: {metamagic.GetSummary(spell.SpellLevel)}");
        }

        if (ShouldShowTouchSpellPrompt(_pendingSpell))
        {
            CombatUI?.ShowTouchSpellPrompt(
                _pendingSpell,
                onCastNow: () => { BeginPendingSpellTargeting(pc); },
                onDischargeLater: () => { HoldPendingMeleeTouchCharge(pc); },
                onCancel: () =>
                {
                    _pendingSpell = null;
                    _pendingMetamagic = null;
                    _pendingSpellFromHeldCharge = false;
                    ShowActionChoices();
                });
            return;
        }

        BeginPendingSpellTargeting(pc);
    }

    private bool IsSummonMonsterSpell(SpellData spell)
    {
        if (spell == null || string.IsNullOrEmpty(spell.SpellId)) return false;
        string normalized = NormalizeSummonSpellId(spell.SpellId);
        return normalized == "summon_monster_1" || normalized == "summon_monster_2";
    }

    private string NormalizeSummonSpellId(string spellId)
    {
        if (string.IsNullOrEmpty(spellId)) return "";
        if (spellId == "summon_monster_1" || spellId == "summon_monster_1_clr") return "summon_monster_1";
        if (spellId == "summon_monster_2" || spellId == "summon_monster_2_clr") return "summon_monster_2";
        return spellId;
    }

    private List<SummonTemplate> GetSummonOptionsForSpell(SpellData spell)
    {
        if (spell == null) return null;
        string normalized = NormalizeSummonSpellId(spell.SpellId);
        if (SummonMonsterOptions.TryGetValue(normalized, out var options)) return options;
        return null;
    }

    public bool TryGetSummonCommand(CharacterController character, out SummonCommand command)
    {
        command = null;
        var summon = GetActiveSummon(character);
        if (summon == null)
            return false;

        command = summon.CurrentCommand ?? SummonCommand.AttackNearest();
        return true;
    }

    public void SetSummonCommand(CharacterController summon, SummonCommand command)
    {
        if (summon == null || command == null)
            return;

        var active = GetActiveSummon(summon);
        if (active == null)
            return;

        active.CurrentCommand = command;

        string summonName = GetSummonDisplayName(summon);
        CombatUI?.ShowCombatLog($"<color=#66E8FF>{summonName}: {command.Description}.</color>");
    }

    private ActiveSummonInstance GetActiveSummon(CharacterController character)
    {
        if (character == null) return null;
        for (int i = 0; i < _activeSummons.Count; i++)
        {
            var summon = _activeSummons[i];
            if (summon != null && summon.Controller == character)
                return summon;
        }
        return null;
    }

    public bool IsSummonedCreature(CharacterController character)
    {
        return GetActiveSummon(character) != null;
    }

    public bool TryGetSummonRemainingRounds(CharacterController character, out int remaining, out int total)
    {
        remaining = 0;
        total = 1;
        var summon = GetActiveSummon(character);
        if (summon == null) return false;

        remaining = Mathf.Max(0, summon.RemainingRounds);
        total = Mathf.Max(1, summon.TotalDurationRounds);
        return true;
    }

    public string GetSummonDisplayName(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return "";

        if (!TryGetSummonRemainingRounds(character, out int remaining, out _))
            return character.Stats.CharacterName;

        string roundsWord = remaining == 1 ? "round" : "rounds";
        return $"{character.Stats.CharacterName} [S] ({remaining} {roundsWord})";
    }

    public void RequestDismissSummon(CharacterController summon)
    {
        if (!IsPlayerTurn || summon == null || !summon.IsPlayerControlled)
            return;

        var active = GetActiveSummon(summon);
        if (active == null || CombatUI == null) return;

        string summonName = active.Controller != null && active.Controller.Stats != null
            ? active.Controller.Stats.CharacterName
            : "this summon";

        CombatUI.ShowConfirmationDialog(
            title: "Dismiss Summon",
            message: $"Dismiss {summonName}?",
            confirmLabel: "Yes",
            cancelLabel: "No",
            onConfirm: () =>
            {
                StartCoroutine(DespawnSummonWithEffect(active, "dismissed"));
                _activeSummons.Remove(active);
                UpdateAllStatsUI();
            },
            onCancel: null);
    }

    private void ShowSummonPlacementTargets(CharacterController caster, SpellData spell)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        int range = spell != null && spell.RangeSquares > 0 ? spell.RangeSquares : 1;
        List<SquareCell> cells = Grid.GetCellsInRange(caster.GridPosition, range);

        foreach (var cell in cells)
        {
            int dist = SquareGridUtils.GetDistance(caster.GridPosition, cell.Coords);
            if (dist > range) continue;

            cell.SetHighlight(HighlightType.SpellRange);

            if (!cell.IsOccupied)
            {
                cell.SetHighlight(HighlightType.SpellTarget);
                _highlightedCells.Add(cell);
            }
        }

        HighlightCharacterFootprint(caster, HighlightType.Selected);

        CombatUI.SetTurnIndicator($"✦ {_pendingSpell.Name}: Click an empty tile to place summon | Range: {range * 5} ft | Right-click to cancel");

    }

    private bool TryConsumePendingSpellCast(CharacterController caster)
    {
        if (caster == null || _pendingSpell == null) return false;

        bool isQuickened = _pendingMetamagic != null && _pendingMetamagic.Has(MetamagicFeatId.QuickenSpell);
        var spellComp = caster.GetComponent<SpellcastingComponent>();
        if (spellComp == null)
        {
            Debug.LogError("[GameManager] TryConsumePendingSpellCast: No SpellcastingComponent!");
            return false;
        }

        if (!isQuickened)
        {
            caster.CommitStandardAction();
        }
        else
        {
            spellComp.MarkQuickenedSpellCast();
        }

        int slotLevelToConsume = _pendingSpell.SpellLevel;
        bool hasMetamagicApplied = _pendingMetamagic != null && _pendingMetamagic.HasAnyMetamagic;
        if (hasMetamagicApplied)
            slotLevelToConsume = _pendingMetamagic.GetEffectiveSpellLevel(_pendingSpell.SpellLevel);


        if (!ResolveGrappledOrPinnedCastingConcentration(
                caster,
                spellComp,
                _pendingSpell,
                _pendingMetamagic,
                hasMetamagicApplied,
                slotLevelToConsume,
                false,
                -1,
                null))
        {
            HandleConcentrationOnCasting(caster, _pendingSpell);
            _pendingSpell = null;
            _pendingMetamagic = null;
            _pendingSpellFromHeldCharge = false;
            return false;
        }
        if (TryRollArcaneSpellFailure(caster, _pendingSpell, false, out int asfRoll, out int asfChance))
        {
            bool consumedOnFailure = ConsumePendingSpellSlot(
                spellComp,
                _pendingSpell,
                _pendingMetamagic,
                hasMetamagicApplied,
                slotLevelToConsume,
                false,
                -1,
                null);

            if (!consumedOnFailure)
            {
                Debug.LogError($"[GameManager] ASF failure path: could not consume level {slotLevelToConsume} slot for {_pendingSpell.Name}");
                return false;
            }

            HandleConcentrationOnCasting(caster, _pendingSpell);
            LogArcaneSpellFailure(caster, _pendingSpell, asfRoll, asfChance);

            _pendingSpell = null;
            _pendingMetamagic = null;
            _pendingSpellFromHeldCharge = false;

            return false;
        }

        bool consumed = ConsumePendingSpellSlot(
            spellComp,
            _pendingSpell,
            _pendingMetamagic,
            hasMetamagicApplied,
            slotLevelToConsume,
            false,
            -1,
            null);

        if (!consumed)
        {
            Debug.LogError($"[GameManager] Failed to consume level {slotLevelToConsume} slot for summon spell {_pendingSpell.Name}");
            return false;
        }

        HandleConcentrationOnCasting(caster, _pendingSpell);
        return true;
    }

    private bool ConsumePendingSpellSlot(
        SpellcastingComponent spellComp,
        SpellData spell,
        MetamagicData metamagic,
        bool hasMetamagicApplied,
        int slotLevelToConsume,
        bool isSpontaneous,
        int spontaneousLevel,
        string spontaneousSacrificedSpellId)
    {
        if (spellComp == null || spell == null) return false;

        if (isSpontaneous)
        {
            if (!string.IsNullOrEmpty(spontaneousSacrificedSpellId))
                return spellComp.SpontaneousCastFromSpecificSpell(spontaneousSacrificedSpellId);

            return spellComp.SpontaneousCastFromSlot(spontaneousLevel);
        }

        if (hasMetamagicApplied && slotLevelToConsume > 0)
            return spellComp.CastWizardSpellWithMetamagic(spell, metamagic);

        return spellComp.CastSpellFromSlot(spell);
    }

    private bool TryRollArcaneSpellFailure(CharacterController caster, SpellData spell, bool isDeliveringHeldCharge, out int roll, out int asfChance)
    {
        roll = 0;
        asfChance = 0;

        if (isDeliveringHeldCharge || caster == null || caster.Stats == null || spell == null)
            return false;

        if (!caster.Stats.IsAffectedByArcaneSpellFailure)
            return false;

        asfChance = Mathf.Clamp(caster.Stats.ArcaneSpellFailure, 0, 100);
        if (asfChance <= 0)
            return false;

        roll = UnityEngine.Random.Range(1, 101);

        CombatUI?.ShowCombatLog($"ASF Check ({caster.Stats.CharacterName}, {spell.Name}): roll {roll}% vs {asfChance}%");
        return roll <= asfChance;
    }

    private void LogArcaneSpellFailure(CharacterController caster, SpellData spell, int roll, int asfChance)
    {
        if (caster == null || caster.Stats == null || spell == null) return;

        CombatUI?.ShowCombatLog("═══════════════════════════════");
        CombatUI?.ShowCombatLog($"{caster.Stats.CharacterName} attempts to cast {spell.Name}");
        CombatUI?.ShowCombatLog($"Arcane Spell Failure: {roll}% ≤ {asfChance}%");
        CombatUI?.ShowCombatLog("⚠️ SPELL FAILS! Spell slot consumed, no effect.");
        CombatUI?.ShowCombatLog("═══════════════════════════════");
    }

    private void InsertIntoInitiative(CharacterController combatant, CharacterController summoner)
    {
        if (combatant == null || combatant.Stats == null)
            return;

        bool isPCTeam = IsPC(combatant);
        _turnService?.AddToInitiative(combatant, isPCTeam, summoner);
        UpdateInitiativeUI();
    }

    private CharacterController SpawnSummonedCreature(CharacterController caster, Vector2Int cell, SummonTemplate template)
    {
        if (caster == null || template == null) return null;

        GameObject summonGO = new GameObject($"Summon_{template.TemplateId}_{UnityEngine.Random.Range(1000, 9999)}");
        summonGO.AddComponent<SpriteRenderer>();
        CharacterController summon = summonGO.AddComponent<CharacterController>();
        summon.IsPlayerControlled = caster.IsPlayerControlled;

        CharacterStats stats = new CharacterStats(
            name: template.DisplayName,
            level: template.Level,
            characterClass: template.CharacterClass,
            str: template.STR, dex: template.DEX, con: template.CON,
            wis: template.WIS, intelligence: template.INT, cha: template.CHA,
            bab: template.BAB,
            armorBonus: template.ArmorBonus,
            shieldBonus: template.ShieldBonus,
            damageDice: template.DamageDice,
            damageCount: template.DamageCount,
            bonusDamage: template.BonusDamage,
            baseSpeed: template.BaseSpeed,
            atkRange: template.AttackRange,
            baseHitDieHP: template.BaseHitDieHP
        );

        foreach (string tag in template.CreatureTags)
        {
            if (!stats.CreatureTags.Contains(tag))
                stats.CreatureTags.Add(tag);
        }

        if (template.IsCelestial)
            stats.CharacterAlignment = Alignment.NeutralGood;
        else if (template.IsFiendish)
            stats.CharacterAlignment = Alignment.NeutralEvil;
        else
            stats.CharacterAlignment = Alignment.TrueNeutral;

        Sprite alive = IconLoader.GetToken(template.TokenType);
        if (alive == null)
            alive = LoadSprite("Sprites/npc_enemy_alive");
        Sprite dead = LoadSprite("Sprites/npc_enemy_dead");

        summon.Init(stats, cell, alive, dead);

        var sr = summon.GetComponent<SpriteRenderer>();
        if (sr != null && alive == null)
            sr.color = template.TintColor;

        var inv = summon.gameObject.AddComponent<InventoryComponent>();
        inv.Init(stats);
        inv.CharacterInventory.RecalculateStats();

        var statusMgr = summon.gameObject.AddComponent<StatusEffectManager>();
        statusMgr.Init(stats);

        var concMgr = summon.gameObject.AddComponent<ConcentrationManager>();
        concMgr.Init(stats, summon);

        NPCs.Add(summon);
        _npcAIBehaviors.Add(EnemyAIBehavior.AggressiveMelee);

        if (summon.IsPlayerControlled)
            _summonedAllies.Add(summon);
        else
            _summonedEnemies.Add(summon);

        var summonVisual = summon.gameObject.GetComponent<SummonedCreatureVisual>();
        if (summonVisual == null)
            summonVisual = summon.gameObject.AddComponent<SummonedCreatureVisual>();
        summonVisual.Init(summon, template.IsCelestial, template.IsFiendish);

        return summon;
    }

    private IEnumerator DespawnSummonWithEffect(ActiveSummonInstance summon, string reason)
    {
        if (summon == null || summon.Controller == null)
            yield break;

        CharacterController cc = summon.Controller;

        var summonVisual = cc.GetComponent<SummonedCreatureVisual>();
        if (summonVisual != null)
            yield return StartCoroutine(summonVisual.PlayDespawnEffect());

        Grid.ClearCreatureOccupancy(cc);

        int npcIdx = NPCs.IndexOf(cc);
        if (npcIdx >= 0)
        {
            NPCs.RemoveAt(npcIdx);
            if (npcIdx < _npcAIBehaviors.Count)
                _npcAIBehaviors.RemoveAt(npcIdx);
        }

        _summonedAllies.Remove(cc);
        _summonedEnemies.Remove(cc);

        _turnService?.RemoveFromInitiative(cc);

        string despawnMessage;
        if (reason == "duration expired")
            despawnMessage = $"<color=#66E8FF>{cc.Stats.CharacterName} disappears as the summoning ends.</color>";
        else if (reason == "dismissed")
            despawnMessage = $"<color=#66E8FF>{cc.Stats.CharacterName} returns to its home plane.</color>";
        else
            despawnMessage = $"<color=#FF8F8F>{cc.Stats.CharacterName} is slain! (Summoning ended early)</color>";

        CombatUI?.ShowCombatLog(despawnMessage);
        Debug.Log($"[Summon] Despawned {cc.Stats.CharacterName}: {reason}");

        Destroy(cc.gameObject);
        UpdateInitiativeUI();
        UpdateAllStatsUI();
    }

    private void HandleSummonDeathCleanup(CharacterController maybeSummon)
    {
        if (maybeSummon == null) return;

        ActiveSummonInstance summon = GetActiveSummon(maybeSummon);
        if (summon == null) return;

        _activeSummons.Remove(summon);
        StartCoroutine(DespawnSummonWithEffect(summon, "destroyed"));
    }

    private void TickSummonDurations()
    {
        if (_activeSummons.Count == 0) return;

        var expired = new List<ActiveSummonInstance>();
        foreach (var summon in _activeSummons)
        {
            if (summon == null || summon.Controller == null || summon.Controller.Stats == null)
            {
                expired.Add(summon);
                continue;
            }

            if (summon.Controller.Stats.IsDead)
            {
                expired.Add(summon);
                continue;
            }

            summon.RemainingRounds--;

            var visual = summon.Controller.GetComponent<SummonedCreatureVisual>();
            if (visual != null)
                visual.SetDuration(summon.RemainingRounds, summon.TotalDurationRounds);

            if (summon.RemainingRounds == 2)
                CombatUI?.ShowCombatLog($"<color=#66E8FF>{summon.Controller.Stats.CharacterName}: 2 rounds remaining.</color>");
            else if (summon.RemainingRounds == 1)
                CombatUI?.ShowCombatLog($"<color=#FFCC66>{summon.Controller.Stats.CharacterName}: 1 round remaining!</color>");

            if (summon.RemainingRounds <= 0)
                expired.Add(summon);
        }

        foreach (var ex in expired)
        {
            _activeSummons.Remove(ex);
            StartCoroutine(DespawnSummonWithEffect(ex, ex != null && ex.RemainingRounds <= 0 ? "duration expired" : "destroyed"));
        }
    }

    private void PerformSummonMonsterCast(CharacterController caster, SquareCell targetCell, SummonTemplate template)
    {
        if (caster == null || targetCell == null || template == null || _pendingSpell == null)
        {
            ShowActionChoices();
            return;
        }

        int summonSizeSquares = template.SizeCategory.GetSpaceWidthSquares();
        if (!Grid.CanPlaceCreature(targetCell.Coords, summonSizeSquares))
        {
            CombatUI.ShowCombatLog("Cannot summon there: not enough open space for that creature size.");
            ShowSummonPlacementTargets(caster, _pendingSpell);
            return;
        }

        CurrentSubPhase = PlayerSubPhase.Animating;

        CaptureSpellcastResourceSnapshot(caster);

        if (!TryConsumePendingSpellCast(caster))
        {
            ClearSpellcastResourceSnapshot();
            ShowActionChoices();
            return;
        }

        ResolveSpellcastProvocation(caster, _pendingSpell, false, canProceed =>
        {
            if (!canProceed)
            {
                if (_spellcastProvocationCancelled)
                {
                    HandleSpellcastCancelledFromAoOPrompt(caster);
                    return;
                }

                ClearSpellcastResourceSnapshot();
                HandleInterruptedSpellCast(caster, 1.0f);
                return;
            }

            ClearSpellcastResourceSnapshot();

            CharacterController summonCC = SpawnSummonedCreature(caster, targetCell.Coords, template);
            if (summonCC == null)
            {
                ShowActionChoices();
                return;
            }

            InsertIntoInitiative(summonCC, caster);

            int durationRounds = Mathf.Max(1, caster.Stats.Level);
            var activeSummon = new ActiveSummonInstance
            {
                Controller = summonCC,
                Caster = caster,
                Template = template,
                RemainingRounds = durationRounds,
                TotalDurationRounds = durationRounds,
                SourceSpellId = _pendingSpell.SpellId,
                IsAlliedToPCs = summonCC.IsPlayerControlled,
                SmiteUsed = false,
                CurrentCommand = SummonCommand.AttackNearest()
            };
            _activeSummons.Add(activeSummon);

            var visual = summonCC.GetComponent<SummonedCreatureVisual>();
            if (visual != null)
                visual.SetDuration(durationRounds, durationRounds);

            CombatUI.ShowCombatLog($"<color=#66E8FF>✨ {caster.Stats.CharacterName} casts {_pendingSpell.Name} and summons {template.DisplayName} for {durationRounds} rounds!</color>");

            _pendingSpell = null;
            _pendingMetamagic = null;
            _pendingSpellFromHeldCharge = false;

            Grid.ClearAllHighlights();
            UpdateAllStatsUI();
            StartCoroutine(AfterAttackDelay(caster, 1.0f));
        });
    }

    private void BeginPendingSpellTargeting(CharacterController caster)
    {
        if (caster == null || _pendingSpell == null)
        {
            ShowActionChoices();
            return;
        }

        if (!CanBeginSpellcastWhileGrappledOrPinned(caster, _pendingSpell))
        {
            _pendingSpell = null;
            _pendingMetamagic = null;
            _pendingSpellFromHeldCharge = false;
            ShowActionChoices();
            return;
        }

        // ===== AoE SPELLS: Enter AoE targeting mode =====
        if (_pendingSpell.AoEShapeType != AoEShape.None)
        {
            EnterAoETargetingMode(caster, _pendingSpell);
            return;
        }

        // Summon Monster spells: select an empty destination tile in range.
        if (IsSummonMonsterSpell(_pendingSpell))
        {
            _pendingAttackMode = PendingAttackMode.CastSpell;
            CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
            ShowSummonPlacementTargets(caster, _pendingSpell);
            return;
        }

        // Determine targeting based on spell type
        if (_pendingSpell.TargetType == SpellTargetType.Self)
        {
            // Self-targeting spells cast immediately
            PerformSpellCast(caster, caster);
        }
        else
        {
            _pendingAttackMode = PendingAttackMode.CastSpell;
            CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
            ShowSpellTargets(caster, _pendingSpell);
        }
    }

    private bool ShouldShowTouchSpellPrompt(SpellData spell)
    {
        if (spell == null) return false;
        if (spell.AoEShapeType != AoEShape.None) return false;
        if (!spell.IsMeleeTouchSpell()) return false;
        return spell.TargetType != SpellTargetType.Self;
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

    private bool IsHoldableMeleeTouchSpell(SpellData spell)
    {
        if (spell == null) return false;
        if (spell.AoEShapeType != AoEShape.None) return false;
        if (!spell.IsMeleeTouchSpell()) return false;
        if (spell.TargetType == SpellTargetType.Self) return false;
        return true;
    }

    private bool IsFriendlyTarget(CharacterController caster, CharacterController target)
    {
        return IsAllyTeam(caster, target);
    }

    private bool IsHumanoid(CharacterController target)
    {
        if (target?.Stats == null) return false;
        return string.Equals(target.Stats.CreatureType, "Humanoid", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsValidTargetForSpell(CharacterController caster, CharacterController target, SpellData spell)
    {
        if (caster == null || target == null || spell == null || target.Stats == null || target.Stats.IsDead)
            return false;

        bool isPersonTransmutation = spell.SpellId == "enlarge_person" || spell.SpellId == "reduce_person";
        if (isPersonTransmutation)
        {
            // Person transmutations can target any humanoid creature (ally or enemy).
            return IsHumanoid(target);
        }

        switch (spell.TargetType)
        {
            case SpellTargetType.SingleEnemy:
                return IsEnemyTeam(caster, target);
            case SpellTargetType.SingleAlly:
                return target == caster || IsAllyTeam(caster, target);
            case SpellTargetType.Touch:
                return true;
            case SpellTargetType.Area:
                return true;
            case SpellTargetType.Self:
                return target == caster;
            default:
                return false;
        }
    }

    private bool ShouldForceTargetToAcceptSave(CharacterController caster, CharacterController target, SpellData spell)
    {
        if (caster == null || target == null || spell == null) return false;

        // Willing creatures can choose to fail saves. Enlarge/Reduce are the current key use-case.
        if (spell.SpellId == "enlarge_person" || spell.SpellId == "reduce_person")
            return IsAllyTeam(caster, target);

        return false;
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

        // Highlight caster's full occupied footprint.
        HighlightCharacterFootprint(caster, HighlightType.Selected);

        // Second pass: highlight valid targets (magenta) on top of range area
        foreach (var cell in allCells)
        {
            if (!cell.IsOccupied || cell.Occupant.Stats.IsDead) continue;
            if (cell.Occupant == caster) continue;

            int sqDist = SquareGridUtils.GetDistance(caster.GridPosition, cell.Coords);
            if (sqDist > range) continue;

            bool validTarget = IsValidTargetForSpell(caster, cell.Occupant, spell);

            if (validTarget)
            {
                cell.SetHighlight(HighlightType.SpellTarget);

                _highlightedCells.Add(cell);
                hasTarget = true;
            }
        }

        // For SingleAlly spells, also allow self-targeting by clicking own tile.
        if (spell.TargetType == SpellTargetType.SingleAlly && IsValidTargetForSpell(caster, caster, spell))
        {
            HighlightCharacterFootprint(caster, HighlightType.SpellTarget, addToSelectableCells: true);
            hasTarget = true;
        }

        if (hasTarget)
        {
            string rangeStr = spell.RangeSquares <= 0 ? "Touch" : $"{spell.RangeSquares} sq ({spell.RangeSquares * 5} ft)";
            string targetMsg;
            if (_pendingSpellFromHeldCharge)
            {
                targetMsg = "Click a target to discharge held touch spell";
            }
            else
            {
                targetMsg = spell.TargetType == SpellTargetType.SingleAlly
                    ? "Click an ally (or self) to cast"
                    : spell.TargetType == SpellTargetType.Area
                        ? "Click a target area to cast"
                        : "Click an enemy to cast";
            }
            CombatUI.SetTurnIndicator($"✦ {spell.Name}: {targetMsg} | Range: {rangeStr} | Right-click to cancel");
        }
        else
        {
            CombatUI.SetTurnIndicator($"No valid targets for {spell.Name}! | Right-click to cancel");
            StartCoroutine(ReturnToActionChoicesAfterDelay(1.5f));
        }
    }

    /// <summary>
    /// Convert the currently selected melee touch spell into a held charge.
    /// The slot is consumed now; delivery can happen on a later action.
    /// </summary>
    private void HoldPendingMeleeTouchCharge(CharacterController caster)
    {
        if (caster == null || _pendingSpell == null) { ShowActionChoices(); return; }

        CurrentSubPhase = PlayerSubPhase.Animating;

        CaptureSpellcastResourceSnapshot(caster);

        var spellComp = caster.GetComponent<SpellcastingComponent>();
        if (spellComp == null)
        {
            ClearSpellcastResourceSnapshot();
            Debug.LogError("[GameManager] HoldPendingMeleeTouchCharge: No SpellcastingComponent!");
            ShowActionChoices();
            return;
        }

        bool isQuickened = _pendingMetamagic != null && _pendingMetamagic.Has(MetamagicFeatId.QuickenSpell);
        if (!isQuickened)
        {
            caster.CommitStandardAction();
        }
        else
        {
            spellComp.MarkQuickenedSpellCast();
        }

        bool consumed = true;
        if (!_pendingSpellFromHeldCharge)
        {
            bool hasMetamagicApplied = _pendingMetamagic != null && _pendingMetamagic.HasAnyMetamagic;
            int slotLevelToConsume = _pendingSpell.SpellLevel;
            if (hasMetamagicApplied)
                slotLevelToConsume = _pendingMetamagic.GetEffectiveSpellLevel(_pendingSpell.SpellLevel);

            if (!ResolveGrappledOrPinnedCastingConcentration(
                    caster,
                    spellComp,
                    _pendingSpell,
                    _pendingMetamagic,
                    hasMetamagicApplied,
                    slotLevelToConsume,
                    false,
                    -1,
                    null))
            {
                HandleConcentrationOnCasting(caster, _pendingSpell);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingMetamagic = null;
                _pendingSpellFromHeldCharge = false;

                ClearSpellcastResourceSnapshot();
                StartCoroutine(AfterAttackDelay(caster, 1.0f));
                return;
            }
            if (TryRollArcaneSpellFailure(caster, _pendingSpell, false, out int asfRoll, out int asfChance))
            {
                consumed = ConsumePendingSpellSlot(
                    spellComp,
                    _pendingSpell,
                    _pendingMetamagic,
                    hasMetamagicApplied,
                    slotLevelToConsume,
                    false,
                    -1,
                    null);

                if (!consumed)
                {
                    ClearSpellcastResourceSnapshot();
                    Debug.LogError($"[GameManager] ASF failure path: could not consume level {slotLevelToConsume} spell slot for held charge!");
                    ShowActionChoices();
                    return;
                }

                HandleConcentrationOnCasting(caster, _pendingSpell);
                LogArcaneSpellFailure(caster, _pendingSpell, asfRoll, asfChance);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingMetamagic = null;
                _pendingSpellFromHeldCharge = false;

                ClearSpellcastResourceSnapshot();
                StartCoroutine(AfterAttackDelay(caster, 1.0f));
                return;
            }

            consumed = ConsumePendingSpellSlot(
                spellComp,
                _pendingSpell,
                _pendingMetamagic,
                hasMetamagicApplied,
                slotLevelToConsume,
                false,
                -1,
                null);

            if (!consumed)
            {
                ClearSpellcastResourceSnapshot();
                Debug.LogError($"[GameManager] Failed to consume level {slotLevelToConsume} spell slot for held charge!");
                ShowActionChoices();
                return;
            }
        }

        HandleConcentrationOnCasting(caster, _pendingSpell);

        ResolveSpellcastProvocation(caster, _pendingSpell, false, canProceed =>
        {
            if (!canProceed)
            {
                if (_spellcastProvocationCancelled)
                {
                    HandleSpellcastCancelledFromAoOPrompt(caster);
                    return;
                }

                ClearSpellcastResourceSnapshot();
                HandleInterruptedSpellCast(caster, 1.0f);
                return;
            }

            ClearSpellcastResourceSnapshot();
            spellComp.SetHeldTouchCharge(_pendingSpell, _pendingMetamagic);

            CombatUI.ShowCombatLog($"✋ {caster.Stats.CharacterName} chooses Discharge Later and holds the charge of {_pendingSpell.Name}.");
            UpdateAllStatsUI();
            Grid.ClearAllHighlights();

            _pendingSpell = null;
            _pendingMetamagic = null;
            _pendingSpellFromHeldCharge = false;

            StartCoroutine(AfterAttackDelay(caster, 1.0f));
        });
    }
    /// <summary>
    /// Execute a spell cast from caster to target.
    /// </summary>
    private void PerformSpellCast(CharacterController caster, CharacterController target)
    {
        CurrentSubPhase = PlayerSubPhase.Animating;

        if (!IsValidTargetForSpell(caster, target, _pendingSpell))
        {
            CombatUI?.ShowCombatLog($"{_pendingSpell.Name} has invalid target (requires humanoid ally/enemy constraints).");
            _pendingSpell = null;
            _pendingMetamagic = null;
            _pendingSpellFromHeldCharge = false;
            ShowActionChoices();
            return;
        }

        CaptureSpellcastResourceSnapshot(caster);

        bool isDeliveringHeldCharge = _pendingSpellFromHeldCharge;

        // Quickened applies when CASTING the spell, not when delivering a previously held charge.
        bool isQuickened = !isDeliveringHeldCharge && _pendingMetamagic != null && _pendingMetamagic.Has(MetamagicFeatId.QuickenSpell);
        if (isDeliveringHeldCharge)
        {
            // D&D 3.5e: discharging a held touch spell is a free action.
            // Do not consume standard/move actions here.
            Debug.Log($"[GameManager] {caster.Stats.CharacterName} discharging held touch spell as a free action.");
        }
        else if (!isQuickened)
        {
            caster.CommitStandardAction();
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
            ClearSpellcastResourceSnapshot();
            Debug.LogError("[GameManager] PerformSpellCast: No SpellcastingComponent!");
            ShowActionChoices();
            return;
        }

        // Check if this is a spontaneous cast (cleric converting a specific prepared spell)
        bool isSpontaneous = !isDeliveringHeldCharge && CombatUI != null && CombatUI.IsSpontaneousCast;
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

        if (!isDeliveringHeldCharge)
        {
            if (!ResolveGrappledOrPinnedCastingConcentration(
                    caster,
                    spellComp,
                    _pendingSpell,
                    _pendingMetamagic,
                    hasMetamagicApplied,
                    slotLevelToConsume,
                    isSpontaneous,
                    spontaneousLevel,
                    spontaneousSacrificedSpellId))
            {
                HandleConcentrationOnCasting(caster, _pendingSpell);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingSpellFromHeldCharge = false;
                _pendingMetamagic = null;

                ClearSpellcastResourceSnapshot();
                StartCoroutine(AfterAttackDelay(caster, 1.0f));
                return;
            }
            if (TryRollArcaneSpellFailure(caster, _pendingSpell, false, out int asfRoll, out int asfChance))
            {
                bool consumedOnFailure = ConsumePendingSpellSlot(
                    spellComp,
                    _pendingSpell,
                    _pendingMetamagic,
                    hasMetamagicApplied,
                    slotLevelToConsume,
                    isSpontaneous,
                    spontaneousLevel,
                    spontaneousSacrificedSpellId);

                if (!consumedOnFailure)
                {
                    ClearSpellcastResourceSnapshot();
                    Debug.LogError($"[GameManager] ASF failure path: failed to consume level {slotLevelToConsume} spell slot!");
                    ShowActionChoices();
                    return;
                }

                HandleConcentrationOnCasting(caster, _pendingSpell);
                LogArcaneSpellFailure(caster, _pendingSpell, asfRoll, asfChance);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingSpellFromHeldCharge = false;
                _pendingMetamagic = null;

                ClearSpellcastResourceSnapshot();
                StartCoroutine(AfterAttackDelay(caster, 1.0f));
                return;
            }

            // Consume spell slot
            // Cantrips are unlimited — CastSpellFromSlot handles this (no slot consumed)
            // Both Wizards and Clerics use slot-based system
            bool consumed = ConsumePendingSpellSlot(
                spellComp,
                _pendingSpell,
                _pendingMetamagic,
                hasMetamagicApplied,
                slotLevelToConsume,
                isSpontaneous,
                spontaneousLevel,
                spontaneousSacrificedSpellId);

            if (!consumed)
            {
                ClearSpellcastResourceSnapshot();
                Debug.LogError($"[GameManager] Failed to consume level {slotLevelToConsume} spell slot!");
                ShowActionChoices();
                return;
            }

            if (isSpontaneous && !string.IsNullOrEmpty(spontaneousSacrificedSpellId))
                Debug.Log($"[GameManager] Spontaneous cast: {caster.Stats.CharacterName} sacrificed '{spontaneousSacrificedSpellId}' → {_pendingSpell.Name}");
            else if (isSpontaneous)
                Debug.Log($"[GameManager] Spontaneous cast (level-based): {caster.Stats.CharacterName} converted a level {spontaneousLevel} slot → {_pendingSpell.Name}");
        }
        // Check if caster is concentrating on another spell — casting requires a concentration check
        HandleConcentrationOnCasting(caster, _pendingSpell);

        ResolveSpellcastProvocation(caster, _pendingSpell, isDeliveringHeldCharge, canProceed =>
        {
            if (!canProceed)
            {
                if (_spellcastProvocationCancelled)
                {
                    HandleSpellcastCancelledFromAoOPrompt(caster);
                    return;
                }

                ClearSpellcastResourceSnapshot();
                HandleInterruptedSpellCast(caster, 1.0f);
                return;
            }

            ClearSpellcastResourceSnapshot();

            // Resolve the spell with metamagic.
            // D&D 3.5e: willing friendly targets for melee touch delivery should auto-succeed.
            bool skipFriendlyTouchAttackRoll = _pendingSpell.IsMeleeTouchSpell() && IsFriendlyTarget(caster, target);
            bool forceTargetToFailSave = ShouldForceTargetToAcceptSave(caster, target, _pendingSpell);
            SpellResult result = SpellCaster.Cast(_pendingSpell, caster.Stats, target.Stats, _pendingMetamagic, skipFriendlyTouchAttackRoll, forceTargetToFailSave, caster, target);

            // Apply tracked buff/debuff effects based on spell type
            bool appliesTrackedEffect = _pendingSpell.EffectType == SpellEffectType.Buff ||
                                        _pendingSpell.EffectType == SpellEffectType.Debuff;
            if (result.Success && appliesTrackedEffect)
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

            bool retainedHeldChargeOnMiss = false;

            // Delivering a held charge clears it only if the touch actually lands.
            // If the touch attack misses, keep the held charge (PHB 3.5e p.141).
            if (isDeliveringHeldCharge)
            {
                bool touchDeliverySucceeded = !result.RequiredAttackRoll || result.AttackHit;
                if (touchDeliverySucceeded)
                {
                    spellComp.ClearHeldTouchCharge("touch delivered");
                }
                else
                {
                    retainedHeldChargeOnMiss = true;
                }
            }

            // Handle death if target was killed
            if (result.TargetKilled && target != null)
            {
                target.OnDeath();
                HandleSummonDeathCleanup(target);
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

            if (retainedHeldChargeOnMiss)
            {
                _lastCombatLog += $"\n✋ Touch attack missed — {caster.Stats.CharacterName} retains {_pendingSpell.Name} charge.";
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
                    _pendingSpellFromHeldCharge = false;
                    return;
                }
                else if (AreAllPCsDead())
                {
                    CurrentPhase = TurnPhase.CombatOver;
                    CombatUI.SetTurnIndicator("DEFEAT! All party members have fallen!");
                    CombatUI.SetActionButtonsVisible(false);
                    _pendingSpell = null;
                    _pendingMetamagic = null;
                    _pendingSpellFromHeldCharge = false;
                    return;
                }
            }

            _pendingSpell = null;
            _pendingSpellFromHeldCharge = false;
            _pendingMetamagic = null;

            // After standard action, check for remaining actions
            StartCoroutine(AfterAttackDelay(caster, 1.5f));
        });
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
                    bool isAlly = IsAllyTeam(caster, cell.Occupant);
                    cell.SetHighlight(isAlly ? HighlightType.AoEAlly : HighlightType.AoETarget);
                }
                else
                {
                    cell.SetHighlight(HighlightType.AoEPreview);
                }
            }

            // Get all valid targets
            bool casterIsPC = caster.IsPlayerControlled;
            List<CharacterController> allyTeam = GetTeamMembers(caster.IsPlayerControlled);
            List<CharacterController> enemyTeam = GetTeamMembers(!caster.IsPlayerControlled);
            List<CharacterController> targets = AoESystem.GetTargetsInArea(
                aoeCells, caster, allyTeam, enemyTeam,
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
            // Also highlight the caster's full occupied footprint as valid.
            HighlightCharacterFootprint(caster, HighlightType.SpellRange);

            string rangeStr = $"{range * 5} ft";
            string sizeStr = $"{spell.AoESizeSquares * 5}-ft radius burst";
            CombatUI.SetTurnIndicator($"✦ {spell.Name}: Aim {sizeStr} | Range: {rangeStr} | Move mouse to preview, click to cast | Right-click to cancel");
        }
        // For cone spells, highlight the caster footprint; direction is determined by mouse.
        else if (spell.AoEShapeType == AoEShape.Cone)
        {
            HighlightCharacterFootprint(caster, HighlightType.Selected);

            string sizeStr = $"{spell.AoESizeSquares * 5}-ft cone";
            CombatUI.SetTurnIndicator($"✦ {spell.Name}: Aim {sizeStr} from caster | Move mouse to aim, click to cast | Right-click to cancel");
        }
        else if (spell.AoEShapeType == AoEShape.Line)
        {
            HighlightCharacterFootprint(caster, HighlightType.Selected);

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
        if (_inputService != null)
            return _inputService.GetMouseWorldPosition();

        if (_mainCam == null)
            return Vector2.zero;

        return _mainCam.ScreenToWorldPoint(Input.mousePosition);
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


        // Highlight the AoE cells with color-coded feedback
        foreach (Vector2Int cellPos in aoeCells)
        {
            SquareCell cell = Grid.GetCell(cellPos);
            if (cell == null) continue;

            if (cell.IsOccupied && cell.Occupant != null && !cell.Occupant.Stats.IsDead)
            {
                CharacterController occupant = cell.Occupant;
                bool isAlly = IsAllyTeam(pc, occupant);
                bool isEnemy = IsEnemyTeam(pc, occupant);

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
        bool casterIsPC = caster.IsPlayerControlled;
        List<CharacterController> allyTeam = GetTeamMembers(caster.IsPlayerControlled);
        List<CharacterController> enemyTeam = GetTeamMembers(!caster.IsPlayerControlled);
        List<CharacterController> targets = AoESystem.GetTargetsInArea(
            aoeCells, caster, allyTeam, enemyTeam,
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
        _pendingSpellFromHeldCharge = false;

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
        _pendingSpellFromHeldCharge = false;
        _pendingMetamagic = null;
        _pendingAttackMode = PendingAttackMode.Single;

        Grid.ClearAllHighlights();
        ShowActionChoices();
        Debug.Log("[Spell] Spell targeting cancelled via right-click/Escape");
    }

    /// <summary>
    /// Cancel weapon attack targeting and clear any pending defensive declaration.
    /// </summary>
    private void CancelPendingAttackTargeting()
    {
        if (_isAwaitingRangedRetargetSelection)
        {
            _rangedRetargetSelectionCancelled = true;
            _selectedRangedRetarget = null;
            _isAwaitingRangedRetargetSelection = false;
            Grid.ClearAllHighlights();
            _highlightedCells.Clear();
            CurrentSubPhase = PlayerSubPhase.Animating;
            CombatUI?.ShowCombatLog("↩ Remaining full-attack swings/shots cancelled.");
            return;
        }

        CharacterController pc = ActivePC;
        if (pc != null && _pendingDefensiveAttackSelection)
        {
            pc.SetFightingDefensively(false);
            CombatUI?.ShowCombatLog($"↩ {pc.Stats.CharacterName} cancels defensive attack declaration.");
            UpdateAllStatsUI();
        }

        _pendingDefensiveAttackSelection = false;
        _pendingAttackMode = PendingAttackMode.Single;
        _skipNextSingleAttackStandardActionCommit = false;
        _isSelectingOffHandTarget = false;
        _isSelectingOffHandThrownTarget = false;
        _currentOffHandBAB = 0;
        _currentOffHandWeapon = null;

        Grid.ClearAllHighlights();
        ShowActionChoices();
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
        _pendingSpellFromHeldCharge = false;

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

        CaptureSpellcastResourceSnapshot(caster);

        // Quickened spells don't consume standard action
        bool isQuickened = _pendingMetamagic != null && _pendingMetamagic.Has(MetamagicFeatId.QuickenSpell);
        if (!isQuickened)
        {
            caster.CommitStandardAction();
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
            ClearSpellcastResourceSnapshot();
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

        if (!ResolveGrappledOrPinnedCastingConcentration(
                caster,
                spellComp,
                _pendingSpell,
                _pendingMetamagic,
                hasMetamagicApplied,
                slotLevelToConsume,
                isSpontaneous,
                spontaneousLevel,
                spontaneousSacrificedSpellId))
        {
            HandleConcentrationOnCasting(caster, _pendingSpell);
            UpdateAllStatsUI();
            Grid.ClearAllHighlights();

            _pendingSpell = null;
            _pendingMetamagic = null;

            ClearSpellcastResourceSnapshot();
            StartCoroutine(AfterAttackDelay(caster, 1.0f));
            return;
        }
        if (TryRollArcaneSpellFailure(caster, _pendingSpell, false, out int asfRoll, out int asfChance))
        {
            bool consumedOnFailure = ConsumePendingSpellSlot(
                spellComp,
                _pendingSpell,
                _pendingMetamagic,
                hasMetamagicApplied,
                slotLevelToConsume,
                isSpontaneous,
                spontaneousLevel,
                spontaneousSacrificedSpellId);

            if (!consumedOnFailure)
            {
                ClearSpellcastResourceSnapshot();
                Debug.LogError($"[GameManager] AoE ASF failure path: failed to consume level {slotLevelToConsume} spell slot!");
                ShowActionChoices();
                return;
            }

            HandleConcentrationOnCasting(caster, _pendingSpell);
            LogArcaneSpellFailure(caster, _pendingSpell, asfRoll, asfChance);
            UpdateAllStatsUI();
            Grid.ClearAllHighlights();

            _pendingSpell = null;
            _pendingMetamagic = null;

            ClearSpellcastResourceSnapshot();
            StartCoroutine(AfterAttackDelay(caster, 1.0f));
            return;
        }

        bool consumed = ConsumePendingSpellSlot(
            spellComp,
            _pendingSpell,
            _pendingMetamagic,
            hasMetamagicApplied,
            slotLevelToConsume,
            isSpontaneous,
            spontaneousLevel,
            spontaneousSacrificedSpellId);

        if (!consumed)
        {
            ClearSpellcastResourceSnapshot();
            Debug.LogError($"[GameManager] AoE: Failed to consume level {slotLevelToConsume} spell slot!");
            ShowActionChoices();
            return;
        }

        // Check if caster is concentrating on another spell — casting requires a concentration check
        HandleConcentrationOnCasting(caster, _pendingSpell);

        ResolveSpellcastProvocation(caster, _pendingSpell, false, canProceed =>
        {
            if (!canProceed)
            {
                if (_spellcastProvocationCancelled)
                {
                    HandleSpellcastCancelledFromAoOPrompt(caster);
                    return;
                }

                ClearSpellcastResourceSnapshot();
                HandleInterruptedSpellCast(caster, 1.0f);
                return;
            }

            ClearSpellcastResourceSnapshot();

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

                    // For buff/debuff spells, apply tracked effects
                    if (_pendingSpell.EffectType == SpellEffectType.Buff || _pendingSpell.EffectType == SpellEffectType.Debuff)
                    {
                        var appliedEffect = ApplySpellBuff(caster, target, _pendingSpell, spellComp);

                        // Track concentration for the first target of a concentration AoE effect
                        if (appliedEffect != null && _pendingSpell.DurationType == DurationType.Concentration && targetIndex == 1)
                        {
                            BeginConcentrationTracking(caster, appliedEffect, _pendingSpell);
                        }

                        string effectLabel = _pendingSpell.EffectType == SpellEffectType.Debuff ? "DEBUFF APPLIED" : "BUFF APPLIED";
                        logBuilder.AppendLine($"  {effectLabel}! {_pendingSpell.Description}");
                        Debug.Log($"[AoE] {_pendingSpell.EffectType} applied to {target.Stats.CharacterName}");
                    }
                    // For damage spells, resolve with save and damage
                    else if (_pendingSpell.EffectType == SpellEffectType.Damage)
                    {
                        SpellResult result = SpellCaster.Cast(_pendingSpell, caster.Stats, target.Stats, _pendingMetamagic, false, false, caster, target);

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
                            HandleSummonDeathCleanup(target);
                            logBuilder.AppendLine($"  💀 {target.Stats.CharacterName} has been slain!");
                        }
                    }
                    // For healing spells
                    else if (_pendingSpell.EffectType == SpellEffectType.Healing)
                    {
                        SpellResult result = SpellCaster.Cast(_pendingSpell, caster.Stats, target.Stats, _pendingMetamagic, false, false, caster, target);

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
        });
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
                bool isDebuff = spell.EffectType == SpellEffectType.Debuff;
                string color = isDebuff ? "#FF8888" : "#88FF88";
                string effectLabel = isDebuff ? "debuff" : "buff";
                CombatUI?.ShowCombatLog($"<color={color}>✨ {spell.Name} {effectLabel} applied to {target.Stats.CharacterName} [{durStr}]</color>");
                Debug.Log($"[GameManager] {spell.Name} {effectLabel} applied to {target.Stats.CharacterName} via StatusEffectManager: {effect.GetDetailedString()}");
            }
            else
            {
                Debug.Log($"[GameManager] {spell.Name} effect NOT applied to {target.Stats.CharacterName} (stacking rules prevented it)");
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
        Debug.Log($"[SpellDuration] Ticking spell durations for round {CurrentRound}...");

        // Tick active, living PCs
        foreach (var pc in PCs)
        {
            if (!IsActiveCombatant(pc) || pc.Stats.IsDead) continue;
            TickCharacterSpellDurations(pc);
        }

        // Tick active, living NPCs
        foreach (var npc in NPCs)
        {
            if (!IsActiveCombatant(npc) || npc.Stats.IsDead) continue;
            TickCharacterSpellDurations(npc);
        }

        UpdateAllStatsUI();
    }

    /// <summary>
    /// Tick spell durations for a single character.
    /// </summary>
    private void TickCharacterSpellDurations(CharacterController character)
    {
        if (!IsActiveCombatant(character) || character.Stats.IsDead)
            return;
        var statusMgr = character.GetComponent<StatusEffectManager>();
        if (statusMgr != null && statusMgr.ActiveEffectCount > 0)
        {
            var expired = statusMgr.TickAllEffects();

            foreach (var effect in expired)
            {
                string msg = $"⏱ {effect.Spell?.Name ?? "Unknown"} has expired on {character.Stats.CharacterName}!";
                Debug.Log($"[SpellDuration] {msg}");
                CombatUI?.ShowCombatLog($"<color=#FFAA44>{msg}</color>");
            }

            if (statusMgr.ActiveEffectCount > 0)
            {
                foreach (var effect in statusMgr.ActiveEffects)
                {
                    Debug.Log($"[SpellDuration] {character.Stats.CharacterName}: {effect.GetDisplayString()}");
                }
            }
        }

    }


    private List<CharacterController> GetThreateningEnemiesForSpellcasting(CharacterController caster)
    {
        var threatening = ThreatSystem.GetThreateningEnemies(caster.GridPosition, caster, GetAllCharacters());
        threatening.RemoveAll(enemy => enemy == null || enemy.Stats == null || enemy.Stats.IsDead || !ThreatSystem.CanMakeAoO(enemy));
        return threatening;
    }

    private bool AttemptCastDefensively(CharacterController caster, SpellData spell)
    {
        if (caster == null || spell == null) return false;

        int dc = 15 + spell.SpellLevel;
        var check = ConcentrationManager.MakeSpellcastingConcentrationCheck(
            caster,
            dc,
            ConcentrationCheckType.CastingDefensively,
            spell);

        LogSpellcastingConcentrationCheck(caster, spell, check);

        if (!check.Success)
        {
            CombatUI?.ShowCombatLog($"<color=#FF6644>💥 {caster.Stats.CharacterName} fails to cast defensively. {spell.Name} is lost!</color>");
            return false;
        }

        CombatUI?.ShowCombatLog($"<color=#88CCFF>🛡 {caster.Stats.CharacterName} casts defensively and avoids attacks of opportunity.</color>");
        return true;
    }

    private void ResolveSpellcastProvocation(CharacterController caster, SpellData spell, bool isDeliveringHeldCharge, System.Action<bool> onResolved)
    {
        _spellcastProvocationCancelled = false;

        if (caster == null || spell == null || isDeliveringHeldCharge)
        {
            onResolved?.Invoke(true);
            return;
        }

        var threateningEnemies = GetThreateningEnemiesForSpellcasting(caster);
        if (threateningEnemies == null || threateningEnemies.Count == 0)
        {
            onResolved?.Invoke(true);
            return;
        }

        CombatUI?.ShowCombatLog($"⚠ {caster.Stats.CharacterName} is casting {spell.Name} while threatened ({threateningEnemies.Count} adjacent).");

        int defensiveDC = 15 + spell.SpellLevel;
        int concentrationBonus = caster.Stats.GetSpellcastingConcentrationBonus(includeCombatCasting: true);
        float successChance = CalculateDefensiveCastSuccessChancePercent(concentrationBonus, defensiveDC);

        ShowAoOActionConfirmation(new AoOProvokingActionInfo
        {
            ActionType = AoOProvokingAction.CastSpell,
            ActionName = $"CAST {spell.Name.ToUpper()}",
            ActionDescription = $"Cast {spell.Name}",
            Actor = caster,
            ThreateningEnemies = threateningEnemies,
            Spell = spell,
            CastDefensivelyDC = defensiveDC,
            ConcentrationBonus = concentrationBonus,
            SuccessChance = successChance,
            OnCastDefensively = () => onResolved?.Invoke(AttemptCastDefensively(caster, spell)),
            OnProceed = () => ResolveSpellcastAoOs(caster, spell, threateningEnemies, onResolved),
            OnCancel = () =>
            {
                _spellcastProvocationCancelled = true;
                onResolved?.Invoke(false);
            }
        });
    }

    private void ResolveSpellcastAoOs(CharacterController caster, SpellData spell, List<CharacterController> threateningEnemies, System.Action<bool> onResolved)
    {
        if (caster == null || spell == null)
        {
            onResolved?.Invoke(false);
            return;
        }

        if (threateningEnemies == null || threateningEnemies.Count == 0)
        {
            onResolved?.Invoke(true);
            return;
        }

        CombatUI?.ShowCombatLog($"⚠ {caster.Stats.CharacterName} casts normally and provokes {threateningEnemies.Count} attack(s) of opportunity.");

        foreach (var enemy in threateningEnemies)
        {
            if (enemy == null || enemy.Stats == null || enemy.Stats.IsDead || !ThreatSystem.CanMakeAoO(enemy))
                continue;

            CombatResult aooResult = ThreatSystem.ExecuteAoO(enemy, caster);
            if (aooResult == null)
                continue;

            CombatUI?.ShowCombatLog($"⚔ AoO vs spellcasting: {aooResult.GetDetailedSummary()}");

            if (aooResult.Hit && aooResult.TotalDamage > 0)
            {
                // Existing concentration effects / held charges can also be disrupted by this damage.
                CheckConcentrationOnDamage(caster, aooResult.TotalDamage);

                int dc = 10 + aooResult.TotalDamage + spell.SpellLevel;
                var check = ConcentrationManager.MakeSpellcastingConcentrationCheck(
                    caster,
                    dc,
                    ConcentrationCheckType.DamagedWhileCasting,
                    spell,
                    aooResult.TotalDamage);

                LogSpellcastingConcentrationCheck(caster, spell, check);

                if (!check.Success)
                {
                    CombatUI?.ShowCombatLog($"<color=#FF6644>💥 {caster.Stats.CharacterName}'s casting is interrupted by damage. {spell.Name} is lost!</color>");
                    onResolved?.Invoke(false);
                    return;
                }
            }

            if (caster.Stats.IsDead)
            {
                CombatUI?.ShowCombatLog($"<color=#FF6644>💀 {caster.Stats.CharacterName} is slain while casting {spell.Name}!</color>");
                onResolved?.Invoke(false);
                return;
            }
        }

        onResolved?.Invoke(true);
    }

    private void LogSpellcastingConcentrationCheck(CharacterController caster, SpellData spell, ConcentrationCheckResult check)
    {
        if (caster == null || caster.Stats == null || spell == null || check == null) return;

        string reason = check.CheckType == ConcentrationCheckType.CastingDefensively
            ? "Cast Defensively"
            : check.CheckType == ConcentrationCheckType.DamagedWhileCasting
                ? $"Damaged While Casting ({check.DamageDealt} dmg)"
                : check.CheckType.ToString();

        string status = check.Success ? "SUCCESS" : "FAIL";
        string color = check.Success ? "#88CCFF" : "#FF6644";

        CombatUI?.ShowCombatLog($"<color={color}>Concentration [{reason}] {caster.Stats.CharacterName}: d20 {check.Roll} + {check.Bonus} = {check.Total} vs DC {check.DC} — {status}</color>");
    }

    private void CaptureSpellcastResourceSnapshot(CharacterController caster)
    {
        _pendingSpellcastSnapshot = null;

        if (caster == null)
            return;

        var snapshot = new SpellcastResourceSnapshot
        {
            Caster = caster,
            MoveActionUsed = caster.Actions.MoveActionUsed,
            StandardActionUsed = caster.Actions.StandardActionUsed,
            FullRoundActionUsed = caster.Actions.FullRoundActionUsed,
            SwiftActionUsed = caster.Actions.SwiftActionUsed,
            StandardConvertedToMove = caster.Actions.StandardConvertedToMove,
            SlotUsedStates = null,
            QuickenedSpellUsed = false
        };

        var spellComp = caster.GetComponent<SpellcastingComponent>();
        if (spellComp != null)
        {
            snapshot.QuickenedSpellUsed = spellComp.HasCastQuickenedSpellThisRound;

            if (spellComp.SpellSlots != null && spellComp.SpellSlots.Count > 0)
            {
                snapshot.SlotUsedStates = new List<bool>(spellComp.SpellSlots.Count);
                foreach (var slot in spellComp.SpellSlots)
                    snapshot.SlotUsedStates.Add(slot != null && slot.IsUsed);
            }
        }

        _pendingSpellcastSnapshot = snapshot;
    }

    private void ClearSpellcastResourceSnapshot()
    {
        _pendingSpellcastSnapshot = null;
    }

    private void RestoreSpellcastResourceSnapshot(CharacterController caster)
    {
        if (_pendingSpellcastSnapshot == null || caster == null || _pendingSpellcastSnapshot.Caster != caster)
            return;

        caster.Actions.MoveActionUsed = _pendingSpellcastSnapshot.MoveActionUsed;
        caster.Actions.StandardActionUsed = _pendingSpellcastSnapshot.StandardActionUsed;
        caster.Actions.FullRoundActionUsed = _pendingSpellcastSnapshot.FullRoundActionUsed;
        caster.Actions.SwiftActionUsed = _pendingSpellcastSnapshot.SwiftActionUsed;
        caster.Actions.StandardConvertedToMove = _pendingSpellcastSnapshot.StandardConvertedToMove;

        var spellComp = caster.GetComponent<SpellcastingComponent>();
        if (spellComp != null)
        {
            spellComp.HasCastQuickenedSpellThisRound = _pendingSpellcastSnapshot.QuickenedSpellUsed;

            if (_pendingSpellcastSnapshot.SlotUsedStates != null && spellComp.SpellSlots != null)
            {
                int count = Mathf.Min(_pendingSpellcastSnapshot.SlotUsedStates.Count, spellComp.SpellSlots.Count);
                for (int i = 0; i < count; i++)
                {
                    if (spellComp.SpellSlots[i] != null)
                        spellComp.SpellSlots[i].IsUsed = _pendingSpellcastSnapshot.SlotUsedStates[i];
                }

                spellComp.SyncPreparedSpellsFromSlots();
            }
        }

        _pendingSpellcastSnapshot = null;
    }

    private void HandleSpellcastCancelledFromAoOPrompt(CharacterController caster)
    {
        RestoreSpellcastResourceSnapshot(caster);
        _spellcastProvocationCancelled = false;

        _pendingSpell = null;
        _pendingMetamagic = null;
        _pendingSpellFromHeldCharge = false;

        Grid.ClearAllHighlights();
        UpdateAllStatsUI();

        if (caster != null && caster.Stats != null)
            CombatUI?.ShowCombatLog($"↩ {caster.Stats.CharacterName} cancels spell cast.");

        ShowActionChoices();
    }

    private void HandleInterruptedSpellCast(CharacterController caster, float delaySeconds = 1.0f)
    {
        _pendingSpell = null;
        _pendingMetamagic = null;
        _pendingSpellFromHeldCharge = false;

        Grid.ClearAllHighlights();
        UpdateAllStatsUI();

        if (caster != null)
            StartCoroutine(AfterAttackDelay(caster, delaySeconds));
        else
            ShowActionChoices();
    }
    // ========== CONCENTRATION MECHANICS (D&D 3.5e PHB) ==========

    /// <summary>
    /// Check if a character needs to make concentration checks after taking damage.
    /// Applies to both ongoing concentration spells and held touch charges.
    /// </summary>
    private void CheckConcentrationOnDamage(CharacterController character, int damageTaken)
    {
        if (character == null || damageTaken <= 0) return;

        var concMgr = character.GetComponent<ConcentrationManager>();
        var spellComp = character.GetComponent<SpellcastingComponent>();

        bool hasConcentrationSpell = concMgr != null && concMgr.IsConcentrating;
        bool hasHeldTouchCharge = spellComp != null && spellComp.HasHeldTouchCharge && spellComp.HeldTouchSpell != null;

        if (!hasConcentrationSpell && !hasHeldTouchCharge) return;

        // If the character is dead, concentration and held charge break automatically.
        if (character.Stats.IsDead)
        {
            if (hasConcentrationSpell)
            {
                string breakLog = concMgr.ForceBreakConcentration("killed");
                if (!string.IsNullOrEmpty(breakLog))
                    CombatUI?.ShowCombatLog($"<color=#FF6644>{breakLog}</color>");
            }

            if (hasHeldTouchCharge)
            {
                string lostSpellName = spellComp.HeldTouchSpell.Name;
                spellComp.ClearHeldTouchCharge("killed");
                CombatUI?.ShowCombatLog($"<color=#FF6644>💥 {character.Stats.CharacterName}'s held {lostSpellName} charge is lost (killed)!</color>");
            }

            UpdateAllStatsUI();
            return;
        }

        // 1) Held touch charge concentration check (injury formula)
        if (hasHeldTouchCharge)
        {
            var heldResult = concMgr != null
                ? concMgr.CheckHeldChargeOnDamage(spellComp.HeldTouchSpell, damageTaken)
                : new ConcentrationCheckResult { Success = true, LogMessage = "" };

            if (!string.IsNullOrEmpty(heldResult.LogMessage))
            {
                string color = heldResult.Success ? "#88CCFF" : "#FF6644";
                CombatUI?.ShowCombatLog($"<color={color}>{heldResult.LogMessage}</color>");
            }

            if (!heldResult.Success)
            {
                string lostSpellName = spellComp.HeldTouchSpell.Name;
                spellComp.ClearHeldTouchCharge("failed concentration after damage");
                CombatUI?.ShowCombatLog($"<color=#FF6644>💥 {character.Stats.CharacterName} loses concentration and the held {lostSpellName} charge dissipates!</color>");
            }
        }

        // 2) Ongoing concentration spell check
        if (hasConcentrationSpell)
        {
            var result = concMgr.CheckConcentrationOnDamage(damageTaken);
            if (!string.IsNullOrEmpty(result.LogMessage))
            {
                string color = result.Success ? "#88CCFF" : "#FF6644";
                CombatUI?.ShowCombatLog($"<color={color}>{result.LogMessage}</color>");
            }
        }

        UpdateAllStatsUI();
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

    // AoO confirmation state (shared by movement, spellcasting, standing from prone, etc.)
    private bool _waitingForAoOConfirmation;
    private AoOProvokingActionInfo _pendingAoOAction;

    // Spellcast cancellation recovery (AoO prompt cancel should not spend actions/slots).
    private bool _spellcastProvocationCancelled;
    private SpellcastResourceSnapshot _pendingSpellcastSnapshot;

    private sealed class SpellcastResourceSnapshot
    {
        public CharacterController Caster;
        public bool MoveActionUsed;
        public bool StandardActionUsed;
        public bool FullRoundActionUsed;
        public bool SwiftActionUsed;
        public bool StandardConvertedToMove;
        public bool QuickenedSpellUsed;
        public List<bool> SlotUsedStates;
    }

    private void ShowMovementRange(CharacterController pc)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        if (_movementService == null || pc == null)
            return;

        List<SquareCell> moveCells = _movementService.CalculateMovementRange(pc);
        for (int i = 0; i < moveCells.Count; i++)
        {
            SquareCell cell = moveCells[i];
            if (cell == null)
                continue;

            cell.SetHighlight(HighlightType.Move);
            _highlightedCells.Add(cell);
        }

        HighlightCharacterFootprint(pc, HighlightType.Selected);
    }


    private List<Vector2Int> GetAdjacentSquares(Vector2Int origin)
    {
        if (_movementService != null)
            return _movementService.GetAdjacentSquares(origin);

        Vector2Int[] neighbors = SquareGridUtils.GetNeighbors(origin);
        return new List<Vector2Int>(neighbors);
    }

    // Public delegation helpers for movement-aware systems.
    public bool ValidateGridPosition(Vector2Int position) => _movementService != null && _movementService.ValidateGridPosition(position);
    public bool IsSquareOccupied(Vector2Int position, CharacterController ignore = null) => _movementService != null && _movementService.IsSquareOccupied(position, ignore);
    public CharacterController GetCharacterAtPosition(Vector2Int position, CharacterController ignore = null) => _movementService != null ? _movementService.GetCharacterAtPosition(position, ignore) : null;
    public bool IsPositionBlocked(Vector2Int position, int moverSizeSquares = 1, CharacterController mover = null) => _movementService == null || _movementService.IsPositionBlocked(position, moverSizeSquares, mover);
    public int CalculateDistance(Vector2Int from, Vector2Int to, bool chebyshev = false) => _movementService != null ? _movementService.CalculateDistance(from, to, chebyshev) : (chebyshev ? SquareGridUtils.GetChebyshevDistance(from, to) : SquareGridUtils.GetDistance(from, to));
    public List<Vector2Int> GetSquaresInRange(Vector2Int origin, int range, bool includeOrigin = false) => _movementService != null ? _movementService.GetSquaresInRange(origin, range, includeOrigin) : new List<Vector2Int>();
    public int GetMovementCost(Vector2Int start, List<Vector2Int> path) => _movementService != null ? _movementService.GetMovementCost(start, path) : SquareGridUtils.CalculatePathCost(start, path ?? new List<Vector2Int>());
    public AoOPathResult FindPath(CharacterController mover, Vector2Int destination, bool avoidThreats = true, int? maxRangeOverride = null, bool allowThroughAllies = true, bool allowThroughEnemies = false)
        => _movementService != null
            ? _movementService.FindPath(mover, destination, avoidThreats, maxRangeOverride, allowThroughAllies, allowThroughEnemies)
            : new AoOPathResult();
    public AoOPathResult FindPath(CharacterController mover, Vector2Int destination, HashSet<Vector2Int> threatenedSquares, int maxRangeOverride, bool allowThroughAllies = true, bool allowThroughEnemies = false)
        => _movementService != null
            ? _movementService.FindPath(mover, destination, threatenedSquares, maxRangeOverride, allowThroughAllies, allowThroughEnemies)
            : new AoOPathResult();
    public List<AoOThreatInfo> CheckForAoO(CharacterController mover, List<Vector2Int> path)
        => _movementService != null ? _movementService.CheckForAoO(mover, path) : new List<AoOThreatInfo>();
    public CombatResult TriggerAoO(CharacterController threatener, CharacterController target)
        => _movementService != null ? _movementService.TriggerAoO(threatener, target) : ThreatSystem.ExecuteAoO(threatener, target);
    public bool CanTake5FootStep(CharacterController character, Vector2Int destination)
        => _movementService != null && _movementService.CanTake5FootStep(character, destination);

    // Public delegation helpers for condition-aware systems.
    public void ApplyCondition(CharacterController target, CombatConditionType type, int rounds, CharacterController source = null, object data = null, bool expiresAtEndOfTurn = false, bool expiresAtStartOfTurn = false)
        => _conditionService?.ApplyCondition(target, type, rounds, source, data, expiresAtEndOfTurn, expiresAtStartOfTurn);
    public bool RemoveCondition(CharacterController target, CombatConditionType type)
        => _conditionService != null && _conditionService.RemoveCondition(target, type);
    public int RemoveAllConditions(CharacterController target)
        => _conditionService != null ? _conditionService.RemoveAllConditions(target) : 0;
    public bool HasCondition(CharacterController target, CombatConditionType type)
        => _conditionService != null && _conditionService.HasCondition(target, type);
    public int GetConditionDuration(CharacterController target, CombatConditionType type)
        => _conditionService != null ? _conditionService.GetConditionDuration(target, type) : 0;
    public List<ConditionService.ActiveCondition> GetActiveConditions(CharacterController target)
        => _conditionService != null ? _conditionService.GetActiveConditions(target) : new List<ConditionService.ActiveCondition>();

    // Public delegation helpers for AIService.
    public EnemyAIBehavior GetNPCBehaviorForAI(CharacterController npc)
    {
        int npcIdx = NPCs.IndexOf(npc);
        return (npcIdx >= 0 && npcIdx < _npcAIBehaviors.Count)
            ? _npcAIBehaviors[npcIdx]
            : EnemyAIBehavior.AggressiveMelee;
    }

    public void BeginNPCTurnForAI(CharacterController npc)
    {
        if (npc == null)
            return;

        _conditionService?.OnTurnStart(npc);
        npc.StartNewTurn();
        PruneTurnUndeadTrackers();
        CheckTurnUndeadProximityBreakingForMover(npc);
    }

    public IEnumerator ExecuteGrappleRestrictedTurnForAI(CharacterController npc)
        => AI_GrappleRestrictedTurn(npc);

    public IEnumerator ExecuteSummonedCreatureTurnForAI(CharacterController npc)
        => AI_SummonedCreature(npc);

    public bool ShouldNPCUseChargeForAI(CharacterController npc, CharacterController target)
        => ShouldNPCUseCharge(npc, target);

    public IEnumerator NPCExecuteChargeForAI(CharacterController npc, CharacterController target)
        => NPCExecuteCharge(npc, target);

    public IEnumerator MoveCharacterAlongComputedPathForAI(CharacterController mover, Vector2Int destination, float secondsPerStep)
        => MoveCharacterAlongComputedPath(mover, destination, secondsPerStep);

    public bool TryNPCSpecialAttackIfBeneficialForAI(CharacterController npc, CharacterController target)
        => TryNPCSpecialAttackIfBeneficial(npc, target);

    public bool TryNPCSpecialAttackByTypeForAI(CharacterController npc, CharacterController target, SpecialAttackType attackType)
        => TryNPCSpecialAttackIfBeneficial(npc, target, attackType);

    public IEnumerator NPCPerformAttackForAI(CharacterController npc, CharacterController target)
        => NPCPerformAttack(npc, target);

    public List<CharacterController> GetAllCharactersForAI()
        => GetAllCharacters();

    public bool IsEnemyTeamForAI(CharacterController source, CharacterController target)
        => IsEnemyTeam(source, target);

    public bool IsUndeadCharacterForAI(CharacterController character)
        => IsUndeadCharacter(character);

    public CharacterController GetTurnUndeadTurnerForAI(CharacterController undead)
        => GetTurnUndeadTurner(undead);

    public void RegisterTurnUndeadTrackerForAI(CharacterController undead, CharacterController turner)
        => RegisterTurnUndeadTracker(undead, turner);

    public CharacterController GetClosestAliveEnemyToForAI(CharacterController source)
        => GetClosestAliveEnemyTo(source);

    public void PruneTurnUndeadTrackersForAI()
        => PruneTurnUndeadTrackers();

    public void CheckTurnUndeadProximityBreakingForMoverForAI(CharacterController mover)
        => CheckTurnUndeadProximityBreakingForMover(mover);

    public float GetPlayerMoveSecondsPerStepForAI()
        => PlayerMoveSecondsPerStep;

    private void CancelMovementSelection()
    {
        CharacterController pc = ActivePC;

        if (_pathPreview != null) _pathPreview.HidePath();
        if (_hoverMarker != null) _hoverMarker.Hide();

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        _waitingForAoOConfirmation = false;
        _pendingAoOAction = null;

        bool wasOverrunDestinationSelection = _isSelectingOverrunDestination;
        ClearOverrunDestinationSelectionState();
        ClearOverrunContinuationState();

        bool wasFreeAdjacentGrappleMoveSelection = _isFreeAdjacentGrappleMoveSelection;
        if (wasFreeAdjacentGrappleMoveSelection)
            ClearFreeAdjacentGrappleMoveSelectionState();

        bool wasGrappleMoveSelection = _isGrappleMoveSelection;
        if (wasGrappleMoveSelection)
            ClearGrappleMoveSelectionState();

        CombatUI?.HideAoOConfirmationPrompt();
        if (pc != null)
        {
            if (wasFreeAdjacentGrappleMoveSelection)
            {
                CombatUI?.ShowCombatLog($"↩ {pc.Stats.CharacterName} remains in place after ending the grapple.");
            }
            else
            {
                if (wasOverrunDestinationSelection)
                    CombatUI?.ShowCombatLog($"↩ {pc.Stats.CharacterName} cancels overrun destination selection.");
                else
                    CombatUI?.ShowCombatLog(wasGrappleMoveSelection
                        ? $"↩ {pc.Stats.CharacterName} chooses not to move after winning grapple control."
                        : $"↩ {pc.Stats.CharacterName} cancels movement.");
            }
        }

        ShowActionChoices();
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

        if (_mainCam == null)
        {
            Debug.LogWarning("[PathPreview] Main camera is null; skipping preview update.");
            return;
        }

        if (Grid == null)
        {
            Debug.LogWarning("[PathPreview] Grid is null; skipping preview update.");
            if (_pathPreview.IsVisible) _pathPreview.HidePath();
            return;
        }

        if (_highlightedCells == null)
        {
            Debug.LogWarning("[PathPreview] Highlighted cell set is null; skipping preview update.");
            if (_pathPreview.IsVisible) _pathPreview.HidePath();
            return;
        }

        // Don't show preview if pointer is over UI
        if (_inputService != null && _inputService.IsPointerOverUI())
        {
            if (_pathPreview.IsVisible) _pathPreview.HidePath();
            return;
        }

        Vector2 worldPoint = _inputService != null
            ? _inputService.GetMouseWorldPosition()
            : (Vector2)_mainCam.ScreenToWorldPoint(Input.mousePosition);
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

        bool previewAllowThroughEnemies = _isSelectingOverrunDestination;

        // Build threatened squares for AoO-aware pathfinding.
        // Overrun destination preview must show the true overrun path (through enemies),
        // so we intentionally disable threat-avoidance weighting in that mode.
        HashSet<Vector2Int> threatenedSquares = previewAllowThroughEnemies
            ? null
            : GetPreviewThreatenedSquares(pc);

        // Use AoO-aware A* pathfinder.
        // - Normal movement preview: avoids threats/enemies when possible.
        // - Overrun destination preview: allows moving through enemies.
        // Grapple move selection is capped at half speed; post-grapple free reposition is adjacent only.
        int previewMaxRange = _isGrappleMoveSelection
            ? Mathf.Max(1, _grappleMoveMaxRangeSquares)
            : (_isFreeAdjacentGrappleMoveSelection ? 1 : pc.Stats.MoveRange);
        var pathResult = Grid.FindPathAoOAware(
            pc.GridPosition,
            gridCoord,
            threatenedSquares,
            previewMaxRange,
            pc.GetVisualSquaresOccupied(),
            pc,
            allowThroughAllies: true,
            allowThroughEnemies: previewAllowThroughEnemies);

        Debug.Log($"[PathPreview] mode={(previewAllowThroughEnemies ? "OverrunThroughEnemies" : "NormalMove")}, from=({pc.GridPosition.x},{pc.GridPosition.y}) to=({gridCoord.x},{gridCoord.y}), threatAware={(threatenedSquares != null)}");

        if (pathResult.Path != null && pathResult.Path.Count > 0)
        {
            // Build per-segment threat flags for visual feedback.
            // Overrun mode intentionally disables threat-avoidance, so threatenedSquares may be null.
            var segmentThreatened = new List<bool>();
            bool hasThreatData = threatenedSquares != null;
            Vector2Int prev = pc.GridPosition;
            foreach (var step in pathResult.Path)
            {
                // A segment is "dangerous" if we're leaving a threatened square.
                bool leaving = hasThreatData && threatenedSquares.Contains(prev);
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
    /// Shows a compact battlefield tooltip when hovering over a character token.
    /// </summary>
    private void UpdateCharacterHoverTooltip()
    {
        if (_mainCam == null || Grid == null)
        {
            HideCharacterHoverTooltip();
            return;
        }

        if (_inputService != null && _inputService.IsPointerOverUI())
        {
            HideCharacterHoverTooltip();
            return;
        }

        Vector2 worldPoint = _inputService != null
            ? _inputService.GetMouseWorldPosition()
            : (Vector2)_mainCam.ScreenToWorldPoint(Input.mousePosition);

        Vector2Int gridCoord = SquareGridUtils.WorldToGrid(worldPoint);
        SquareCell hoveredCell = Grid.GetCell(gridCoord);
        if (hoveredCell == null)
        {
            HideCharacterHoverTooltip();
            return;
        }

        CharacterController hoveredCharacter = hoveredCell.Occupant;
        if (!IsActiveCombatant(hoveredCharacter))
        {
            HideCharacterHoverTooltip();
            return;
        }

        hoveredCharacter.RefreshAllTags();

        CharacterHoverTooltipUI tooltip = CharacterHoverTooltipUI.Instance;
        if (tooltip == null)
        {
            CharacterHoverTooltipUI.EnsureInstance();
            tooltip = CharacterHoverTooltipUI.Instance;
        }

        if (tooltip == null)
            return;

        Vector3 mouseScreenPos;
        if (_inputService != null && _inputService.TryGetMouseScreenPosition(out mouseScreenPos))
            tooltip.ShowTooltip(hoveredCharacter, mouseScreenPos);
        else
            tooltip.ShowTooltip(hoveredCharacter, Input.mousePosition);

        _lastHoveredCharacter = hoveredCharacter;
    }

    private void HideCharacterHoverTooltip()
    {
        if (_lastHoveredCharacter != null)
            _lastHoveredCharacter = null;

        CharacterHoverTooltipUI.Instance?.HideTooltip();
    }

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

        // Hide if pointer is over UI
        if (_inputService != null && _inputService.IsPointerOverUI())
        {
            if (_hoverMarker.IsVisible)
            {
                _hoverMarker.Hide();
                _lastHoverMarkerCoord = new Vector2Int(-999, -999);
            }
            return;
        }

        Vector2 worldPoint = _inputService != null
            ? _inputService.GetMouseWorldPosition()
            : (Vector2)_mainCam.ScreenToWorldPoint(Input.mousePosition);
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
    /// Get all active characters in combat for AoO threat calculations.
    /// </summary>
    private List<CharacterController> GetAllCharacters()
    {
        var all = new List<CharacterController>();
        foreach (var pc in PCs)
        {
            if (IsActiveCombatant(pc)) all.Add(pc);
        }
        foreach (var npc in NPCs)
        {
            if (IsActiveCombatant(npc)) all.Add(npc);
        }
        return all;
    }

    private static float CalculateDefensiveCastSuccessChancePercent(int concentrationBonus, int defensiveDC)
    {
        int requiredRoll = defensiveDC - concentrationBonus;
        float successChance = (21 - requiredRoll) / 20f * 100f;
        return Mathf.Clamp(successChance, 5f, 95f);
    }

    private void ShowAoOActionConfirmation(AoOProvokingActionInfo actionInfo)
    {
        if (actionInfo == null)
            return;

        if (actionInfo.ThreateningEnemies == null)
            actionInfo.ThreateningEnemies = new List<CharacterController>();

        actionInfo.ThreateningEnemies.RemoveAll(enemy => enemy == null || enemy.Stats == null || enemy.Stats.IsDead || !ThreatSystem.CanMakeAoO(enemy));

        if (actionInfo.ThreateningEnemies.Count == 0)
        {
            actionInfo.OnProceed?.Invoke();
            return;
        }

        if (CombatUI == null)
        {
            actionInfo.OnProceed?.Invoke();
            return;
        }

        if (_waitingForAoOConfirmation)
            return;

        System.Action proceed = actionInfo.OnProceed;
        System.Action castDefensively = actionInfo.OnCastDefensively;
        System.Action cancel = actionInfo.OnCancel;

        actionInfo.OnProceed = () =>
        {
            _waitingForAoOConfirmation = false;
            _pendingAoOAction = null;
            proceed?.Invoke();
        };

        actionInfo.OnCastDefensively = () =>
        {
            _waitingForAoOConfirmation = false;
            _pendingAoOAction = null;
            castDefensively?.Invoke();
        };

        actionInfo.OnCancel = () =>
        {
            _waitingForAoOConfirmation = false;
            _pendingAoOAction = null;
            cancel?.Invoke();
        };

        _waitingForAoOConfirmation = true;
        _pendingAoOAction = actionInfo;
        CombatUI.ShowAoOConfirmationPrompt(actionInfo);
    }

    // ========== ATTACK TARGET SELECTION ==========

    private bool IsUsingThrownAttackMode(CharacterController attacker, ItemData weapon = null)
    {
        if (_currentAttackType != AttackType.Thrown)
            return false;

        if (attacker == null)
            return false;

        weapon ??= attacker.GetEquippedMainWeapon();
        return weapon != null
            && weapon.WeaponCat == WeaponCategory.Melee
            && weapon.IsThrown
            && weapon.RangeIncrement > 0;
    }

    private bool IsAttackModeRanged(CharacterController attacker, ItemData weapon = null)
    {
        if (attacker == null)
            return false;

        weapon ??= attacker.GetEquippedMainWeapon();
        if (weapon == null)
            return false;

        if (weapon.WeaponCat == WeaponCategory.Ranged)
            return true;

        return IsUsingThrownAttackMode(attacker, weapon);
    }

    private void ShowAttackTargets(CharacterController pc)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        // All combatants are considered for flanking checks (team and threat filtering happens in CombatUtils).
        List<CharacterController> allCombatants = GetAllCharacters();

        // Determine the equipped weapon's range semantics based on selected attack type.
        ItemData weapon = pc.GetEquippedMainWeapon();
        bool usingThrownAttack = IsUsingThrownAttackMode(pc, weapon);
        bool isRangedWeapon = IsAttackModeRanged(pc, weapon);
        bool isThrownWeapon = usingThrownAttack || (weapon != null && weapon.WeaponCat == WeaponCategory.Ranged && weapon.IsThrown);
        int rangeIncrement = (weapon != null && isRangedWeapon) ? weapon.RangeIncrement : 0;

        if (usingThrownAttack && weapon != null)
        {
            Debug.Log($"[Attack][Thrown] Showing thrown target selection for {pc.Stats.CharacterName} using {weapon.Name} (increment {weapon.RangeIncrement} ft)");
        }

        int meleeMinDistance = 1;
        int meleeMaxDistance = 1;

        int maxRangeSquares;
        if (isRangedWeapon && rangeIncrement > 0)
        {
            maxRangeSquares = RangeCalculator.GetMaxRangeSquares(rangeIncrement, isThrownWeapon);
            ShowRangeZoneHighlights(pc, rangeIncrement, maxRangeSquares, isThrownWeapon);
        }
        else if (!isRangedWeapon)
        {
            // IMPORTANT: use the same min/max ring logic as actual melee validation.
            meleeMinDistance = pc.GetMeleeMinAttackDistance(weapon);
            meleeMaxDistance = pc.GetMeleeMaxAttackDistance(weapon);
            maxRangeSquares = Mathf.Max(1, meleeMaxDistance);
            ShowMeleeRangeZoneHighlights(pc, meleeMinDistance, meleeMaxDistance);
        }
        else
        {
            maxRangeSquares = pc.Stats.AttackRange;
        }

        int sizePadding = Mathf.Max(0, pc.GetVisualSquaresOccupied() - 1);
        List<SquareCell> allCells = isRangedWeapon
            ? Grid.GetCellsInRange(pc.GridPosition, maxRangeSquares + sizePadding)
            : GetCellsInChebyshevRange(pc.GridPosition, maxRangeSquares + sizePadding);
        bool hasTarget = false;
        bool anyFlanking = false;

        foreach (var cell in allCells)
        {
            if (cell.IsOccupied && cell.Occupant != pc && !cell.Occupant.Stats.IsDead)
            {
                if (!IsEnemyTeam(pc, cell.Occupant))
                    continue;

                if (!pc.IsTargetInCurrentWeaponRange(cell.Occupant))
                    continue;

                // Check whether attacker can flank this target with any ally who actually threatens.
                CharacterController flankPartner;
                bool flanking = CombatUtils.IsAttackerFlanking(pc, cell.Occupant, allCombatants, out flankPartner);

                if (flanking)
                {
                    cell.SetHighlight(HighlightType.Flanking);
                    anyFlanking = true;
                }
                else
                {
                    // For melee targeting we keep enemy cells in the same "valid ring" color language.
                    cell.SetHighlight(isRangedWeapon ? HighlightType.Attack : HighlightType.AttackRange);
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

            if (_pendingAttackMode == PendingAttackMode.Single && _currentAttackType == AttackType.Thrown)
                modeStr = "THROWN ATTACK";

            string rangeMsg = "";
            if (isRangedWeapon && rangeIncrement > 0)
            {
                int incSquares = RangeCalculator.GetRangeIncrementSquares(rangeIncrement);
                int maxRange = RangeCalculator.GetMaxRangeFeet(rangeIncrement, isThrownWeapon);
                rangeMsg = $"\n{weapon.Name}: {rangeIncrement} ft increment ({incSquares} sq), max {maxRange} ft";
            }
            else if (weapon == null)
            {
                var unarmed = pc.GetUnarmedDamage();
                rangeMsg = $"\nUnarmed strike: {unarmed.damageCount}d{unarmed.damageDice}";
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

    private List<CharacterController> GetValidRangedTargets(CharacterController attacker)
    {
        var valid = new List<CharacterController>();
        if (attacker == null || !IsAttackModeRanged(attacker))
            return valid;

        ItemData weapon = attacker.GetEquippedMainWeapon();
        int rangeIncrement = weapon != null ? weapon.RangeIncrement : 0;
        bool isThrownWeapon = IsUsingThrownAttackMode(attacker, weapon) || (weapon != null && weapon.WeaponCat == WeaponCategory.Ranged && weapon.IsThrown);

        int maxRangeSquares = (rangeIncrement > 0)
            ? RangeCalculator.GetMaxRangeSquares(rangeIncrement, isThrownWeapon)
            : attacker.Stats.AttackRange;

        int sizePadding = Mathf.Max(0, attacker.GetVisualSquaresOccupied() - 1);
        List<SquareCell> allCells = Grid.GetCellsInRange(attacker.GridPosition, maxRangeSquares + sizePadding);
        foreach (SquareCell cell in allCells)
        {
            if (cell == null || !cell.IsOccupied || cell.Occupant == null || cell.Occupant == attacker)
                continue;

            CharacterController candidate = cell.Occupant;
            if (candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!IsEnemyTeam(attacker, candidate))
                continue;

            if (attacker.IsTargetInCurrentWeaponRange(candidate))
                valid.Add(candidate);
        }

        return valid;
    }

    private List<CharacterController> GetValidMeleeTargets(CharacterController attacker)
    {
        var valid = new List<CharacterController>();
        if (attacker == null)
            return valid;

        foreach (CharacterController candidate in GetAllCharacters())
        {
            if (candidate == null || candidate == attacker || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!IsEnemyTeam(attacker, candidate))
                continue;

            if (attacker.IsTargetInCurrentWeaponRange(candidate))
                valid.Add(candidate);
        }

        return valid;
    }

    private List<CharacterController> GetValidTargetsForCurrentWeapon(CharacterController attacker)
    {
        if (attacker == null)
            return new List<CharacterController>();

        return IsAttackModeRanged(attacker)
            ? GetValidRangedTargets(attacker)
            : GetValidMeleeTargets(attacker);
    }

    private bool IsTargetInCurrentWeaponRange(CharacterController attacker, CharacterController target)
    {
        if (attacker == null || target == null || target.Stats == null || target.Stats.IsDead)
            return false;

        if (IsUsingThrownAttackMode(attacker))
            return attacker.IsTargetInThrownWeaponRange(target);

        return attacker.IsTargetInCurrentWeaponRange(target);
    }

    private bool HasAnyValidTargetFromPosition(CharacterController attacker, Vector2Int attackerPosition, bool rangedMode)
    {
        if (attacker == null || attacker.Stats == null)
            return false;

        ItemData weapon = attacker.GetEquippedMainWeapon();
        int rangeIncrement = weapon != null ? weapon.RangeIncrement : 0;
        bool isThrownWeapon = IsUsingThrownAttackMode(attacker, weapon) || (weapon != null && weapon.WeaponCat == WeaponCategory.Ranged && weapon.IsThrown);
        List<Vector2Int> attackerSquares = attacker.GetOccupiedSquaresAt(attackerPosition);

        foreach (CharacterController candidate in GetAllCharacters())
        {
            if (candidate == null || candidate == attacker || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!IsEnemyTeam(attacker, candidate))
                continue;

            if (rangedMode)
            {
                int sqDist = int.MaxValue;
                List<Vector2Int> candidateSquares = candidate.GetOccupiedSquares();
                for (int i = 0; i < attackerSquares.Count; i++)
                {
                    for (int j = 0; j < candidateSquares.Count; j++)
                    {
                        int d = SquareGridUtils.GetDistance(attackerSquares[i], candidateSquares[j]);
                        if (d < sqDist)
                            sqDist = d;
                    }
                }

                if (rangeIncrement > 0)
                {
                    int distFeet = RangeCalculator.SquaresToFeet(sqDist);
                    if (RangeCalculator.IsWithinMaxRange(distFeet, rangeIncrement, isThrownWeapon))
                        return true;
                }
                else if (sqDist <= attacker.Stats.AttackRange)
                {
                    return true;
                }
            }
            else
            {
                if (CombatUtils.CanThreatenTargetFromPosition(attacker, attackerPosition, candidate))
                    return true;
            }
        }

        return false;
    }

    private IEnumerator WaitForFullAttackRetargetSelection(CharacterController attacker, int remainingAttacks)
    {
        bool rangedMode = attacker != null && attacker.IsEquippedWeaponRanged();
        string modeLabel = rangedMode ? "ranged" : "melee";

        _isAwaitingRangedRetargetSelection = true;
        _rangedRetargetSelectionCancelled = false;
        _selectedRangedRetarget = null;

        CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
        ShowAttackTargets(attacker);
        CombatUI?.ShowCombatLog($"🎯 Select a new {modeLabel} target for {remainingAttacks} remaining attack(s), or right-click/ESC to cancel.");
        CombatUI?.SetTurnIndicator($"TARGET SWITCH: Select {modeLabel} target ({remainingAttacks} attack(s) remain) | Right-click/ESC to cancel");

        while (_isAwaitingRangedRetargetSelection)
            yield return null;

        CurrentSubPhase = PlayerSubPhase.Animating;
    }

    private void ShowFullAttackFiveFootStepOptions(CharacterController pc)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        foreach (Vector2Int neighbor in SquareGridUtils.GetNeighbors(pc.GridPosition))
        {
            if (!IsValidFiveFootStepDestination(pc, neighbor))
                continue;

            if (_fullAttackFiveFootStepRequireReachableTarget
                && !HasAnyValidTargetFromPosition(pc, neighbor, _fullAttackFiveFootStepRangedMode))
            {
                continue;
            }

            SquareCell cell = Grid.GetCell(neighbor);
            if (cell == null) continue;

            cell.SetHighlight(HighlightType.FiveFootStep);
            _highlightedCells.Add(cell);
        }

        HighlightCharacterFootprint(pc, HighlightType.Selected);
    }

    private IEnumerator WaitForOptionalFiveFootStepDuringFullAttack(
        CharacterController attacker,
        string prompt,
        bool requireReachableTargetAfterStep,
        bool rangedMode)
    {
        if (attacker == null || !CanTakeFiveFootStep(attacker))
            yield break;

        _isAwaitingFullAttackFiveFootStepSelection = true;
        _fullAttackFiveFootStepSelectionCancelled = false;
        _fullAttackFiveFootStepWasTaken = false;
        _fullAttackFiveFootStepRequireReachableTarget = requireReachableTargetAfterStep;
        _fullAttackFiveFootStepRangedMode = rangedMode;

        CurrentSubPhase = PlayerSubPhase.TakingFiveFootStep;
        ShowFullAttackFiveFootStepOptions(attacker);

        if (_highlightedCells.Count == 0)
        {
            _isAwaitingFullAttackFiveFootStepSelection = false;
            _fullAttackFiveFootStepSelectionCancelled = true;
            Grid.ClearAllHighlights();
            _highlightedCells.Clear();
            CurrentSubPhase = PlayerSubPhase.Animating;
            yield break;
        }

        CombatUI?.ShowCombatLog($"↔ {prompt} Select a highlighted square for a 5-foot step, or right-click/ESC to skip.");
        CombatUI?.SetTurnIndicator($"5-FOOT STEP: {prompt} Click destination or right-click/ESC to skip");

        while (_isAwaitingFullAttackFiveFootStepSelection)
            yield return null;

        CurrentSubPhase = PlayerSubPhase.Animating;
    }

    /// <summary>
    /// Returns all valid grid cells in a Chebyshev square radius (diagonals count as 1).
    /// This is used for melee/reach previews so corner cells are never dropped by
    /// D&D 3.5 movement-distance filtering.
    /// </summary>
    private List<SquareCell> GetCellsInChebyshevRange(Vector2Int center, int range, bool includeCenter = false)
    {
        var cells = new List<SquareCell>();
        if (Grid == null || range < 0)
            return cells;

        for (int x = center.x - range; x <= center.x + range; x++)
        {
            for (int y = center.y - range; y <= center.y + range; y++)
            {
                var coords = new Vector2Int(x, y);
                if (!includeCenter && coords == center)
                    continue;

                if (SquareGridUtils.GetChebyshevDistance(center, coords) > range)
                    continue;

                SquareCell cell = Grid.GetCell(coords);
                if (cell != null)
                    cells.Add(cell);
            }
        }

        return cells;
    }

    private void ShowMeleeRangeZoneHighlights(CharacterController attacker, int minDistance, int maxDistance)
    {
        if (attacker == null || Grid == null)
            return;

        int min = Mathf.Max(1, minDistance);
        int max = Mathf.Max(min, maxDistance);

        int sizePadding = Mathf.Max(0, attacker.GetVisualSquaresOccupied() - 1);
        List<Vector2Int> attackerOccupiedSquares = attacker.GetOccupiedSquares();
        List<SquareCell> allCells = GetCellsInChebyshevRange(attacker.GridPosition, max + sizePadding);
        foreach (SquareCell cell in allCells)
        {
            if (cell == null)
                continue;
            if (cell.IsOccupied && cell.Occupant == attacker)
                continue;

            int sqDist = int.MaxValue;
            foreach (Vector2Int occupied in attackerOccupiedSquares)
            {
                int d = SquareGridUtils.GetChebyshevDistance(occupied, cell.Coords);
                if (d < sqDist) sqDist = d;
            }
            if (sqDist <= 0 || sqDist > max)
                continue;

            // Dead zone ring(s): inside max reach but below legal min distance.
            if (sqDist < min)
            {
                cell.SetHighlight(HighlightType.AttackDeadZone);
                continue;
            }

            // Legal melee ring(s): exactly what CanMeleeAttackDistance uses.
            if (sqDist >= min && sqDist <= max)
                cell.SetHighlight(HighlightType.AttackRange);
        }
    }

    private void ShowRangeZoneHighlights(CharacterController pc, int rangeIncrement, int maxRangeSquares, bool isThrownWeapon = false)
    {
        int sizePadding = Mathf.Max(0, pc.GetVisualSquaresOccupied() - 1);
        List<Vector2Int> occupiedSquares = pc.GetOccupiedSquares();
        List<SquareCell> allCells = Grid.GetCellsInRange(pc.GridPosition, maxRangeSquares + sizePadding);
        foreach (var cell in allCells)
        {
            if (cell.IsOccupied && cell.Occupant == pc) continue;

            int sqDist = int.MaxValue;
            foreach (Vector2Int occupied in occupiedSquares)
            {
                int d = SquareGridUtils.GetDistance(occupied, cell.Coords);
                if (d < sqDist) sqDist = d;
            }

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
        if (!IsPlayerTurn) yield break;

        CharacterController pc = ActivePC;
        if (pc != null && CurrentSubPhase == PlayerSubPhase.SelectingAttackTarget && _pendingDefensiveAttackSelection)
        {
            pc.SetFightingDefensively(false);
            _pendingDefensiveAttackSelection = false;
            CombatUI?.ShowCombatLog($"↩ {pc.Stats.CharacterName} cancels defensive attack declaration.");
            UpdateAllStatsUI();
        }

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

            case PlayerSubPhase.TakingFiveFootStep:
                HandleFiveFootStepClick(pc, cell);
                break;

            case PlayerSubPhase.Crawling:
                HandleCrawlClick(pc, cell);
                break;

            case PlayerSubPhase.SelectingAttackTarget:
                HandleAttackTargetClick(pc, cell);
                break;

            case PlayerSubPhase.SelectingSpecialTarget:
                HandleSpecialAttackTargetClick(pc, cell);
                break;

            case PlayerSubPhase.ConfirmingTurnUndead:
                ConfirmTurnUndeadTargeting();
                break;

            case PlayerSubPhase.SelectingChargeTarget:
                HandleChargeTargetClick(pc, cell);
                break;

            case PlayerSubPhase.ConfirmingChargePath:
                HandleChargeConfirmationClick(pc, cell);
                break;

            case PlayerSubPhase.SelectingAoETarget:
                HandleAoETargetClick(pc, cell);
                break;

            case PlayerSubPhase.ChoosingAction:
                break;
        }
    }

    private const float PlayerMoveSecondsPerStep = 0.08f;
    private const float NpcChargeMoveSecondsPerStep = 0.06f;

    private void HandleMovementClick(CharacterController pc, SquareCell cell)
    {
        if (_waitingForAoOConfirmation) return;

        if (_isSelectingOverrunDestination)
        {
            HandleOverrunDestinationClick(pc, cell);
            return;
        }

        if (_isFreeAdjacentGrappleMoveSelection)
        {
            HandleFreeAdjacentGrappleMovementClick(pc, cell);
            return;
        }

        if (_isGrappleMoveSelection)
        {
            HandleGrappleMovementClick(pc, cell);
            return;
        }

        if (_isOverrunContinuationSelection)
        {
            HandleOverrunContinuationMovementClick(pc, cell);
            return;
        }

        if (cell.Coords == pc.GridPosition)
        {
            CancelMovementSelection();
            return;
        }

        if (!_highlightedCells.Contains(cell) || !Grid.CanPlaceCreature(cell.Coords, pc.GetVisualSquaresOccupied(), pc))
            return;

        var pathResult = _movementService != null
            ? _movementService.FindPath(pc, cell.Coords, avoidThreats: true)
            : Grid.FindSafePath(pc.GridPosition, cell.Coords, pc, GetAllCharacters());

        if (pathResult == null || pathResult.Path == null || pathResult.Path.Count == 0)
        {
            CombatUI?.ShowCombatLog("⚠ No valid movement path to that tile.");
            return;
        }

        if (!pathResult.ProvokesAoOs)
        {
            StartCoroutine(ExecuteMovement(pc, new List<Vector2Int>(pathResult.Path)));
            return;
        }

        Debug.Log($"[GameManager] Movement to ({cell.Coords.x},{cell.Coords.y}) would provoke {pathResult.ProvokedAoOs.Count} AoO(s)!");

        var uniqueThreateners = new List<CharacterController>();
        var seen = new HashSet<CharacterController>();
        foreach (var aooInfo in pathResult.ProvokedAoOs)
        {
            CharacterController threatener = aooInfo != null ? aooInfo.Threatener : null;
            if (threatener == null || !seen.Add(threatener))
                continue;
            uniqueThreateners.Add(threatener);
        }

        ShowAoOActionConfirmation(new AoOProvokingActionInfo
        {
            ActionType = AoOProvokingAction.Movement,
            ActionName = "MOVE",
            ActionDescription = $"Move to ({cell.Coords.x},{cell.Coords.y})",
            Actor = pc,
            ThreateningEnemies = uniqueThreateners,
            OnProceed = () => StartCoroutine(ResolveAoOsAndMove(pc, pathResult)),
            OnCancel = () =>
            {
                CurrentSubPhase = PlayerSubPhase.Moving;
                if (_isGrappleMoveSelection)
                    ShowGrappleMoveRange(pc);
                else
                    ShowMovementRange(pc);
                CombatUI.SetActionButtonsVisible(false);
                CombatUI.SetTurnIndicator(_isGrappleMoveSelection
                    ? $"{pc.Stats.CharacterName} - Move while grappling: choose destination within half speed ({_grappleMoveMaxRangeSquares} sq)"
                    : $"{pc.Stats.CharacterName} - Click a tile to move (right-click/ESC or own tile to cancel)");
            }
        });
    }

    private IEnumerator ResolveAoOsAndMove(CharacterController pc, AoOPathResult pathResult)
    {
        CurrentSubPhase = PlayerSubPhase.Animating;

        foreach (var aooInfo in pathResult.ProvokedAoOs)
        {
            if (pc.Stats.IsDead) break;

            var threatener = aooInfo.Threatener;
            if (threatener.Stats.IsDead) continue;

            CombatResult aooResult = _movementService != null
                ? _movementService.TriggerAoO(threatener, pc)
                : ThreatSystem.ExecuteAoO(threatener, pc);
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
            List<Vector2Int> path = (pathResult != null && pathResult.Path != null && pathResult.Path.Count > 0)
                ? pathResult.Path
                : new List<Vector2Int>();

            yield return StartCoroutine(ExecuteMovement(pc, path));
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

    private IEnumerator ExecuteMovement(CharacterController pc, List<Vector2Int> path)
    {
        if (pc == null || path == null || path.Count == 0)
            yield break;

        CurrentSubPhase = PlayerSubPhase.Animating;

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

        if (_movementService != null)
            yield return StartCoroutine(_movementService.ExecuteMovement(pc, path, PlayerMoveSecondsPerStep, markAsMoved: true));
        else
            yield return StartCoroutine(pc.MoveAlongPath(path, PlayerMoveSecondsPerStep, markAsMoved: true));
        CheckTurnUndeadProximityBreakingForMover(pc);
        PruneTurnUndeadTrackers();
        UpdateAllStatsUI();

        // Invalidate threat cache since positions changed
        InvalidatePreviewThreats();

        ShowActionChoices();
    }

    private IEnumerator MoveCharacterAlongComputedPath(CharacterController mover, Vector2Int destination, float secondsPerStep)
    {
        if (mover == null || mover.Stats == null || Grid == null)
            yield break;

        if (destination == mover.GridPosition)
            yield break;

        int maxRange = Mathf.Max(1, mover.Stats.MoveRange);
        AoOPathResult pathResult = _movementService != null
            ? _movementService.FindPath(mover, destination, avoidThreats: false, maxRangeOverride: maxRange)
            : Grid.FindPathAoOAware(mover.GridPosition, destination, null, maxRange, mover.GetVisualSquaresOccupied(), mover);

        List<Vector2Int> path = (pathResult != null && pathResult.Path != null && pathResult.Path.Count > 0)
            ? pathResult.Path
            : new List<Vector2Int> { destination };

        if (_movementService != null)
            yield return StartCoroutine(_movementService.ExecuteMovement(mover, path, secondsPerStep, markAsMoved: true));
        else
            yield return StartCoroutine(mover.MoveAlongPath(path, secondsPerStep, markAsMoved: true));
        CheckTurnUndeadProximityBreakingForMover(mover);
        PruneTurnUndeadTrackers();
    }

    private void HandleAttackTargetClick(CharacterController pc, SquareCell cell)
    {
        // ===== SPELL CASTING MODE =====
        if (_pendingAttackMode == PendingAttackMode.CastSpell && _pendingSpell != null)
        {
            // Summon Monster spells: pick an empty highlighted destination, then choose creature.
            if (IsSummonMonsterSpell(_pendingSpell))
            {
                if (!_highlightedCells.Contains(cell))
                {
                    CombatUI.ShowCombatLog("Choose a highlighted empty tile in range for the summon.");
                    return;
                }

                if (cell.IsOccupied)
                {
                    CombatUI.ShowCombatLog("Choose an empty tile to place your summon.");
                    return;
                }

                List<SummonTemplate> options = GetSummonOptionsForSpell(_pendingSpell);
                if (options == null || options.Count == 0)
                {
                    CombatUI.ShowCombatLog($"No summon options configured for {_pendingSpell.Name}.");
                    _pendingSpell = null;
                    _pendingMetamagic = null;
                    _pendingSpellFromHeldCharge = false;
                    ShowActionChoices();
                    return;
                }

                var selectedCell = cell;
                CombatUI.ShowSummonCreatureSelection(_pendingSpell.Name,
                    options.ConvertAll(o => o.DisplayName),
                    onSelect: (idx) =>
                    {
                        if (idx < 0 || idx >= options.Count)
                        {
                            ShowActionChoices();
                            return;
                        }
                        PerformSummonMonsterCast(pc, selectedCell, options[idx]);
                    },
                    onCancel: () =>
                    {
                        _pendingSpell = null;
                        _pendingMetamagic = null;
                        _pendingSpellFromHeldCharge = false;
                        ShowActionChoices();
                    });
                return;
            }

            // For ally spells, clicking own tile = self-target
            if (cell.Coords == pc.GridPosition && _pendingSpell.TargetType == SpellTargetType.SingleAlly)
            {
                if (IsValidTargetForSpell(pc, pc, _pendingSpell))
                {
                    PerformSpellCast(pc, pc);
                }
                else
                {
                    CombatUI.ShowCombatLog($"{_pendingSpell.Name} can only target humanoids.");
                }
                return;
            }

            // Cancel if clicking non-highlighted cell
            if (!_highlightedCells.Contains(cell))
            {
                _pendingSpell = null;
                _pendingMetamagic = null;
                _pendingSpellFromHeldCharge = false;
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
            _pendingSpellFromHeldCharge = false;
            ShowActionChoices();
            return;
        }

        // ===== NORMAL ATTACK MODE =====
        if (_isAwaitingRangedRetargetSelection)
        {
            if (cell.IsOccupied && cell.Occupant != null && cell.Occupant != pc && !cell.Occupant.Stats.IsDead
                && _highlightedCells.Contains(cell) && IsEnemyTeam(pc, cell.Occupant))
            {
                _selectedRangedRetarget = cell.Occupant;
                _isAwaitingRangedRetargetSelection = false;
                return;
            }

            CombatUI?.ShowCombatLog("Select a highlighted valid target, or right-click/ESC to cancel remaining attacks.");
            return;
        }

        if (_isSelectingOffHandTarget)
        {
            HandleOffHandTargetClick(pc, cell);
            return;
        }
        if (!cell.IsOccupied || cell.Occupant == pc || cell.Occupant.Stats.IsDead)
        {
            if (cell.Coords == pc.GridPosition || !_highlightedCells.Contains(cell))
            {
                ShowActionChoices();
                return;
            }
        }


        if (cell.IsOccupied && cell.Occupant != pc && !cell.Occupant.Stats.IsDead && _highlightedCells.Contains(cell)
            && IsEnemyTeam(pc, cell.Occupant))
        {
            PerformPlayerAttack(pc, cell.Occupant);
        }
    }

    private void HandleOffHandTargetClick(CharacterController attacker, SquareCell cell)
    {
        Debug.Log($"[OffHand] Target clicked at cell ({cell?.X},{cell?.Y}) selecting={_isSelectingOffHandTarget} highlightedCount={_highlightedCells.Count}");

        if (!_isSelectingOffHandTarget)
        {
            Debug.Log("[OffHand] Ignoring click because off-hand target selection is not active.");
            return;
        }

        if (!cell.IsOccupied || cell.Occupant == null || cell.Occupant == attacker || cell.Occupant.Stats == null || cell.Occupant.Stats.IsDead || !_highlightedCells.Contains(cell) || !IsEnemyTeam(attacker, cell.Occupant))
        {
            Debug.Log($"[OffHand] Invalid target click. occupied={cell.IsOccupied} occupant={(cell.Occupant != null ? cell.Occupant.Stats.CharacterName : "none")} highlighted={_highlightedCells.Contains(cell)} enemy={(cell.Occupant != null ? IsEnemyTeam(attacker, cell.Occupant) : false)}");
            _isSelectingOffHandTarget = false;
            _isSelectingOffHandThrownTarget = false;
            _currentOffHandBAB = 0;
            _currentOffHandWeapon = null;
            ShowActionChoices();
            return;
        }

        CharacterController target = cell.Occupant;
        ItemData offHandWeapon = _currentOffHandWeapon;
        if (offHandWeapon == null)
        {
            Debug.Log("[OffHand] Early return: current off-hand weapon is null.");
            CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} has no off-hand weapon available.");
            _isSelectingOffHandTarget = false;
            _isSelectingOffHandThrownTarget = false;
            _currentOffHandBAB = 0;
            _currentOffHandWeapon = null;
            ShowActionChoices();
            return;
        }

        bool useThrownRange = _isSelectingOffHandThrownTarget
            || (offHandWeapon.IsThrown && _currentAttackType == AttackType.Thrown);

        Debug.Log($"[OffHand] HandleOffHandTargetClick attacker={attacker.Stats.CharacterName} target={target.Stats.CharacterName} mode={(useThrownRange ? "Thrown" : "Melee")} weapon={offHandWeapon.Name} BAB={_currentOffHandBAB}");

        if (!_isInAttackSequence)
        {
            // Off-hand attacks are controlled by the dedicated off-hand availability gate.
            // Do not hard-block confirmation if no standard action remains after a completed
            // main-hand full-attack sequence.
            bool shouldConsumeStandardAction = attacker.Actions.HasStandardAction && !attacker.Actions.FullRoundActionUsed;
            if (shouldConsumeStandardAction)
            {
                if (!attacker.CommitStandardAction())
                {
                    Debug.Log("[OffHand] Early return: failed to consume standard action at confirm-time.");
                    string modeLabel = useThrownRange ? "off-hand thrown attack" : "off-hand attack";
                    CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} could not commit a standard action for an {modeLabel}.");
                    _isSelectingOffHandTarget = false;
                    _isSelectingOffHandThrownTarget = false;
                    _currentOffHandBAB = 0;
                    _currentOffHandWeapon = null;
                    ShowActionChoices();
                    return;
                }

                Debug.Log($"[Attack][OffHand] Consumed standard action on confirm for {(useThrownRange ? "thrown" : "melee")} off-hand attack.");
            }
            else
            {
                Debug.Log($"[Attack][OffHand] Skipping standard action consumption (hasStandard={attacker.Actions.HasStandardAction}, fullRoundUsed={attacker.Actions.FullRoundActionUsed}, offHandAvailable={_offHandAttackAvailableThisTurn}, offHandUsed={_offHandAttackUsedThisTurn}).");
            }
        }

        CurrentSubPhase = PlayerSubPhase.Animating;

        Debug.Log("[OffHand] Calling ExecuteOffHandAttack...");
        CombatResult result = ExecuteOffHandAttack(attacker, target, _currentOffHandBAB, offHandWeapon, useThrownRange);
        Debug.Log("[OffHand] ExecuteOffHandAttack returned.");

        if (result != null && result.Hit && result.TotalDamage > 0)
            CheckConcentrationOnDamage(target, result.TotalDamage);

        if (result != null && result.TargetKilled)
        {
            HandleSummonDeathCleanup(target);

            if (!target.IsPlayerControlled)
            {
                UpdateAllStatsUI();
                if (AreAllNPCsDead())
                {
                    _offHandAttackUsedThisTurn = true;
                    _isSelectingOffHandTarget = false;
                    _isSelectingOffHandThrownTarget = false;
                    CurrentPhase = TurnPhase.CombatOver;
                    CombatUI.SetTurnIndicator("VICTORY! All enemies defeated!");
                    CombatUI.SetActionButtonsVisible(false);
                    return;
                }
            }
        }

        if (useThrownRange)
            ResolveOffHandThrownWeaponAfterAttack(attacker, target, offHandWeapon);

        _offHandAttackUsedThisTurn = true;
        _offHandAttackAvailableThisTurn = attacker.HasOffHandWeaponEquipped();
        _isSelectingOffHandTarget = false;
        _isSelectingOffHandThrownTarget = false;

        Debug.Log("[OffHand] Off-hand attack used this turn");
        Debug.Log($"[OffHand] _offHandAttackAvailableThisTurn: {_offHandAttackAvailableThisTurn}");
        Debug.Log($"[OffHand] _offHandAttackUsedThisTurn: {_offHandAttackUsedThisTurn}");
        Debug.Log($"[Attack][OffHand] Off-hand attack resolved. inSequence={_isInAttackSequence} mainAttacksUsed={_totalAttacksUsed}/{_totalAttackBudget} thrown={useThrownRange}");

        StartCoroutine(AfterAttackDelay(attacker, 1.2f));
    }

    private CombatResult ExecuteOffHandAttack(CharacterController attacker, CharacterController target, int attackBab, ItemData offHandWeapon, bool useThrownRange)
    {
        if (_combatFlowService != null)
            return _combatFlowService.ExecuteOffHandAttack(attacker, target, attackBab, offHandWeapon, useThrownRange);

        return null;
    }

    private void ResolveOffHandThrownWeaponAfterAttack(CharacterController thrower, CharacterController target, ItemData thrownWeapon)
    {
        if (!IsThrowableMeleeWeapon(thrownWeapon))
            return;

        if (thrower == null || thrower.Stats == null)
            return;

        Vector2Int landingPosition = target != null ? target.GridPosition : thrower.GridPosition;
        if (!TryDropThrownWeaponToGround(thrower, thrownWeapon, landingPosition, EquipSlot.LeftHand, out string dropFeedback))
        {
            Debug.LogWarning($"[Attack][OffHand][Thrown] {dropFeedback}");
            CombatUI?.ShowCombatLog($"⚠ {dropFeedback}");
            return;
        }

        CombatUI?.ShowCombatLog($"→ {thrownWeapon.Name} lands on ground at ({landingPosition.x},{landingPosition.y}).");

        if (TryEquipNextThrowableOffHandWeapon(thrower, out ItemData nextWeapon, out string equipFeedback))
        {
            Debug.Log($"[Attack][OffHand][Thrown] {equipFeedback}");
            CombatUI?.ShowCombatLog($"↻ {thrower.Stats.CharacterName} auto-equips {nextWeapon.Name} to off-hand.");
            _currentOffHandWeapon = nextWeapon;
            return;
        }

        Debug.Log($"[Attack][OffHand][Thrown] {equipFeedback}");
        _currentOffHandWeapon = thrower.GetOffHandAttackWeapon();

        if (!thrower.HasThrowableOffHandWeaponEquipped())
        {
            Debug.Log($"[Attack][OffHand][Thrown] {thrower.Stats.CharacterName} has no throwable off-hand weapon equipped after the throw.");
            CombatUI?.ShowCombatLog($"⚠ {thrower.Stats.CharacterName} has no more throwable off-hand weapons equipped.");
        }
    }

    private void ShowSpecialAttackTargets(CharacterController attacker, SpecialAttackType type)
    {
        if (type == SpecialAttackType.Overrun)
        {
            Debug.LogWarning("[Overrun][LegacyGuard] ShowSpecialAttackTargets(Overrun) was invoked; redirecting to destination selection.");
            StartOverrunDestinationSelection(attacker);
            return;
        }

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        int maxRange = (type == SpecialAttackType.Feint)
            ? 1
            : attacker.GetMeleeMaxAttackDistance();
        if (maxRange < 1) maxRange = 1;

        int sizePadding = Mathf.Max(0, attacker.GetVisualSquaresOccupied() - 1);
        List<SquareCell> allCells = GetCellsInChebyshevRange(attacker.GridPosition, maxRange + sizePadding);
        bool hasTarget = false;

        foreach (var c in allCells)
        {
            if (!c.IsOccupied || c.Occupant == attacker || c.Occupant.Stats.IsDead) continue;
            if (!IsEnemyTeam(attacker, c.Occupant)) continue;

            int distance = attacker.GetMinimumDistanceToTarget(c.Occupant, chebyshev: true);
            bool inRange = (type == SpecialAttackType.Feint)
                ? distance == 1
                : attacker.CanMeleeAttackDistance(distance);

            if (type == SpecialAttackType.Overrun)
            {
                if (!IsValidOverrunTarget(attacker, c.Occupant, out _, requireAdjacency: true))
                    continue;

                inRange = distance == 1;
            }

            if (!inRange)
                continue;

            if (type == SpecialAttackType.Disarm)
            {
                bool hasDisarmableWeapon = c.Occupant.HasDisarmableWeaponEquipped();
                c.SetHighlight(hasDisarmableWeapon ? HighlightType.Attack : HighlightType.AttackDeadZone);
                _highlightedCells.Add(c);
                hasTarget = true;
                continue;
            }

            if (type == SpecialAttackType.Sunder)
            {
                bool hasSunderableItem = c.Occupant.HasSunderableItemEquipped();
                c.SetHighlight(hasSunderableItem ? HighlightType.Attack : HighlightType.AttackDeadZone);
                _highlightedCells.Add(c);
                hasTarget = true;
                continue;
            }

            c.SetHighlight(HighlightType.Attack);
            _highlightedCells.Add(c);
            hasTarget = true;
        }

        HighlightCharacterFootprint(attacker, HighlightType.Selected);

        if (hasTarget)
        {
            if (type == SpecialAttackType.Disarm)
                CombatUI.SetTurnIndicator("SPECIAL: Disarm - red targets are valid, gray targets have no disarmable weapon (Right-click/Esc to cancel)");
            else if (type == SpecialAttackType.Sunder)
                CombatUI.SetTurnIndicator("SPECIAL: Sunder - red targets are valid, gray targets have no sunderable item (Right-click/Esc to cancel)");
            else
                CombatUI.SetTurnIndicator($"SPECIAL: {type} - select target (Right-click/Esc to cancel)");
        }
        else
        {
            CombatUI.SetTurnIndicator($"No targets in range for {type}.");
            StartCoroutine(ReturnToActionChoicesAfterDelay(1.0f));
        }
    }

    private void HandleSpecialAttackTargetClick(CharacterController attacker, SquareCell cell)
    {
        if (!_highlightedCells.Contains(cell) || !cell.IsOccupied || cell.Occupant == attacker)
        {
            ShowActionChoices();
            return;
        }

        CharacterController target = cell.Occupant;
        if (_pendingSpecialAttackType == SpecialAttackType.Disarm)
        {
            HandleDisarmTargetClick(attacker, target);
            return;
        }

        if (_pendingSpecialAttackType == SpecialAttackType.Sunder)
        {
            HandleSunderTargetClick(attacker, target);
            return;
        }

        if (_pendingSpecialAttackType == SpecialAttackType.Overrun)
        {
            Debug.LogWarning("[Overrun][LegacyGuard] Received special-target click while pending overrun; restarting destination selection flow.");
            StartOverrunDestinationSelection(attacker);
            return;
        }

        ExecuteSpecialAttack(attacker, target, _pendingSpecialAttackType);
    }



    private void ExecuteSpecialAttack(CharacterController attacker, CharacterController target, SpecialAttackType type, EquipSlot? disarmTargetSlot = null, EquipSlot? sunderTargetSlot = null)
    {
        if (attacker == null || target == null) { ShowActionChoices(); return; }

        CurrentSubPhase = PlayerSubPhase.Animating;

        bool specialAttackCountsAsMeleeFearBreak = type == SpecialAttackType.Trip
            || type == SpecialAttackType.Disarm
            || type == SpecialAttackType.Grapple
            || type == SpecialAttackType.Sunder
            || type == SpecialAttackType.BullRushAttack
            || type == SpecialAttackType.BullRushCharge
            || type == SpecialAttackType.Overrun;
        ProcessTurnUndeadMeleeFearBreak(attacker, target, specialAttackCountsAsMeleeFearBreak);

        string actionLabel = "standard action";
        int? disarmAttackBonusOverride = null;
        int disarmAttackBonusUsed = 0;
        bool disarmUsedOffHand = false;
        int disarmDualWieldPenaltyForLog = 0;
        ItemData disarmAttackerWeaponOverride = null;
        int? grappleAttackBonusOverride = null;
        int? bullRushAttackBonusOverride = null;
        int? tripAttackBonusOverride = null;
        int? sunderAttackBonusOverride = null;
        int sunderAttackBonusUsed = 0;
        bool sunderUsedOffHand = false;
        int sunderDualWieldPenaltyForLog = 0;
        ItemData sunderAttackerWeaponOverride = null;

        if (type == SpecialAttackType.Feint)
        {
            if (!TryConsumeFeintAction(attacker, out actionLabel))
            {
                CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} cannot feint: no eligible action remaining.");
                ShowActionChoices();
                return;
            }
        }
        else if (type == SpecialAttackType.Disarm)
        {
            if (!_isDisarmSequenceActive || _disarmInitiator != attacker || _disarmTarget != target)
                BeginDisarmSequence(attacker, target, disarmTargetSlot);

            int disarmAttemptsBefore = GetRemainingDisarmAttackActions(attacker);
            Debug.Log(
                $"[Disarm][Flow] Starting attempt {_disarmAttemptNumber + 1} attacker={attacker.Stats.CharacterName} " +
                $"target={target.Stats.CharacterName} stdAction={attacker.Actions.HasStandardAction} " +
                $"moveAction={attacker.Actions.HasMoveAction} fullRound={attacker.Actions.HasFullRoundAction} " +
                $"sharedSequenceActive={_isInAttackSequence} sequenceOwner={(_attackingCharacter != null && _attackingCharacter.Stats != null ? _attackingCharacter.Stats.CharacterName : "<null>")} " +
                $"disarmAttemptsBefore={disarmAttemptsBefore} offHandAvailable={CanUseOffHandAttackOption(attacker)} requestedOffHand={_pendingDisarmUseOffHandSelection}");

            bool useOffHandDisarm = _pendingDisarmUseOffHandSelection;
            if (!TryConsumeDisarmAttackAction(attacker, useOffHandDisarm, out disarmAttackBonusUsed, out int disarmAttacksRemaining, out string disarmConsumeReason, out disarmUsedOffHand, out disarmAttackerWeaponOverride))
            {
                string reason = string.IsNullOrWhiteSpace(disarmConsumeReason)
                    ? "no eligible disarm attack remaining"
                    : disarmConsumeReason;
                Debug.LogWarning($"[Disarm][Flow] Consume failed for {attacker.Stats.CharacterName}: {reason}");
                CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} cannot perform Disarm: {reason}.");
                _pendingDisarmUseOffHandSelection = false;
                ClearDisarmSequenceState();
                ShowActionChoices();
                return;
            }

            disarmAttackBonusOverride = disarmAttackBonusUsed;
            if (_isDualWielding)
                disarmDualWieldPenaltyForLog = disarmUsedOffHand ? _offHandPenalty : _mainHandPenalty;

            _disarmAttemptNumber++;
            string handLabel = disarmUsedOffHand ? "off-hand" : "main-hand";
            actionLabel = $"disarm attempt #{_disarmAttemptNumber} ({handLabel}), BAB {CharacterStats.FormatMod(disarmAttackBonusUsed)} ({disarmAttacksRemaining} remaining)";
            Debug.Log(
                $"[Disarm][Flow] Consume success actor={attacker.Stats.CharacterName} attempt={_disarmAttemptNumber} " +
                $"hand={handLabel} usedBAB={CharacterStats.FormatMod(disarmAttackBonusUsed)} remaining={disarmAttacksRemaining} " +
                $"stdActionNow={attacker.Actions.HasStandardAction} moveActionNow={attacker.Actions.HasMoveAction} fullRoundNow={attacker.Actions.HasFullRoundAction}");
        }
        else if (type == SpecialAttackType.Sunder)
        {
            if (!_isSunderSequenceActive || _sunderInitiator != attacker || _sunderTarget != target)
                BeginSunderSequence(attacker, target, sunderTargetSlot);

            int sunderAttemptsBefore = GetRemainingSunderAttackActions(attacker);
            Debug.Log(
                $"[Sunder][Flow] Starting attempt {_sunderAttemptNumber + 1} attacker={attacker.Stats.CharacterName} " +
                $"target={target.Stats.CharacterName} stdAction={attacker.Actions.HasStandardAction} " +
                $"moveAction={attacker.Actions.HasMoveAction} fullRound={attacker.Actions.HasFullRoundAction} " +
                $"sharedSequenceActive={_isInAttackSequence} sequenceOwner={(_attackingCharacter != null && _attackingCharacter.Stats != null ? _attackingCharacter.Stats.CharacterName : "<null>")} " +
                $"sunderAttemptsBefore={sunderAttemptsBefore} offHandAvailable={CanUseOffHandAttackOption(attacker)} requestedOffHand={_pendingSunderUseOffHandSelection}");

            bool useOffHandSunder = _pendingSunderUseOffHandSelection;
            if (!TryConsumeSunderAttackAction(attacker, useOffHandSunder, out sunderAttackBonusUsed, out int sunderAttacksRemaining, out string sunderConsumeReason, out sunderUsedOffHand, out sunderAttackerWeaponOverride))
            {
                string reason = string.IsNullOrWhiteSpace(sunderConsumeReason)
                    ? "no eligible sunder attack remaining"
                    : sunderConsumeReason;
                Debug.LogWarning($"[Sunder][Flow] Consume failed for {attacker.Stats.CharacterName}: {reason}");
                CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} cannot perform Sunder: {reason}.");
                _pendingSunderUseOffHandSelection = false;
                ClearSunderSequenceState();
                ShowActionChoices();
                return;
            }

            sunderAttackBonusOverride = sunderAttackBonusUsed;
            if (_isDualWielding)
                sunderDualWieldPenaltyForLog = sunderUsedOffHand ? _offHandPenalty : _mainHandPenalty;

            _sunderAttemptNumber++;
            string handLabel = sunderUsedOffHand ? "off-hand" : "main-hand";
            actionLabel = $"sunder attempt #{_sunderAttemptNumber} ({handLabel}), BAB {CharacterStats.FormatMod(sunderAttackBonusUsed)} ({sunderAttacksRemaining} remaining)";
            Debug.Log(
                $"[Sunder][Flow] Consume success actor={attacker.Stats.CharacterName} attempt={_sunderAttemptNumber} " +
                $"hand={handLabel} usedBAB={CharacterStats.FormatMod(sunderAttackBonusUsed)} remaining={sunderAttacksRemaining} " +
                $"stdActionNow={attacker.Actions.HasStandardAction} moveActionNow={attacker.Actions.HasMoveAction} fullRoundNow={attacker.Actions.HasFullRoundAction}");
        }
        else if (type == SpecialAttackType.Grapple)
        {
            Debug.Log($"[GameManager][Grapple] Attempting shared-pool consume actor={attacker.Stats.CharacterName} phase={CurrentPhase} subPhase={CurrentSubPhase} std={attacker.Actions.HasStandardAction} full={attacker.Actions.HasFullRoundAction} remaining={GetRemainingGrappleAttackActions(attacker)}");
            if (!TryConsumeGrappleAttackAction(attacker, out int grappleAttackBonusUsed, out int grappleAttacksRemaining, out string grappleConsumeReason))
            {
                string reason = string.IsNullOrWhiteSpace(grappleConsumeReason)
                    ? "no eligible attack remaining"
                    : grappleConsumeReason;
                Debug.LogWarning($"[GameManager][Grapple] Shared-pool consume failed actor={attacker.Stats.CharacterName} reason={reason}");
                CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} cannot initiate grapple: {reason}.");
                ShowActionChoices();
                return;
            }

            grappleAttackBonusOverride = grappleAttackBonusUsed;
            actionLabel = $"attack BAB {CharacterStats.FormatMod(grappleAttackBonusUsed)} ({grappleAttacksRemaining} remaining)";
            Debug.Log($"[GameManager][Grapple] Shared-pool consume success actor={attacker.Stats.CharacterName} usedBAB={CharacterStats.FormatMod(grappleAttackBonusUsed)} remaining={grappleAttacksRemaining}");
        }
        else if (type == SpecialAttackType.BullRushAttack)
        {
            Debug.Log($"[GameManager][BullRushAttack] Attempting shared-pool consume actor={attacker.Stats.CharacterName} phase={CurrentPhase} subPhase={CurrentSubPhase} std={attacker.Actions.HasStandardAction} full={attacker.Actions.HasFullRoundAction} remaining={GetRemainingBullRushAttackActions(attacker)}");
            if (!TryConsumeBullRushAttackAction(attacker, out int bullRushBabUsed, out int bullRushAttacksRemaining, out string bullRushConsumeReason))
            {
                string reason = string.IsNullOrWhiteSpace(bullRushConsumeReason)
                    ? "no eligible attack remaining"
                    : bullRushConsumeReason;
                Debug.LogWarning($"[GameManager][BullRushAttack] Shared-pool consume failed actor={attacker.Stats.CharacterName} reason={reason}");
                CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} cannot perform Bull Rush (Attack): {reason}.");
                ShowActionChoices();
                return;
            }

            bullRushAttackBonusOverride = bullRushBabUsed;
            actionLabel = $"attack BAB {CharacterStats.FormatMod(bullRushBabUsed)} ({bullRushAttacksRemaining} remaining)";
            Debug.Log($"[GameManager][BullRushAttack] Shared-pool consume success actor={attacker.Stats.CharacterName} usedBAB={CharacterStats.FormatMod(bullRushBabUsed)} remaining={bullRushAttacksRemaining}");
        }
        else if (type == SpecialAttackType.Trip)
        {
            Debug.Log($"[GameManager][Trip] Attempting shared-pool consume actor={attacker.Stats.CharacterName} phase={CurrentPhase} subPhase={CurrentSubPhase} std={attacker.Actions.HasStandardAction} full={attacker.Actions.HasFullRoundAction} remaining={GetRemainingTripAttackActions(attacker)}");
            if (!TryConsumeTripAttackAction(attacker, out int tripBabUsed, out int tripAttacksRemaining, out string tripConsumeReason))
            {
                string reason = string.IsNullOrWhiteSpace(tripConsumeReason)
                    ? "no eligible attack remaining"
                    : tripConsumeReason;
                Debug.LogWarning($"[GameManager][Trip] Shared-pool consume failed actor={attacker.Stats.CharacterName} reason={reason}");
                CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} cannot perform Trip: {reason}.");
                ShowActionChoices();
                return;
            }

            tripAttackBonusOverride = tripBabUsed;
            actionLabel = $"attack BAB {CharacterStats.FormatMod(tripBabUsed)} ({tripAttacksRemaining} remaining)";
            Debug.Log($"[GameManager][Trip] Shared-pool consume success actor={attacker.Stats.CharacterName} usedBAB={CharacterStats.FormatMod(tripBabUsed)} remaining={tripAttacksRemaining}");
        }
        else
        {
            if (!attacker.CommitStandardAction())
            {
                CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} cannot use {type}: standard action already spent.");
                ShowActionChoices();
                return;
            }
            actionLabel = "standard action";
        }

        bool maneuverProvokesAoO = type == SpecialAttackType.Grapple || type == SpecialAttackType.Sunder;
        if (maneuverProvokesAoO)
        {
            bool attackerIgnoresAoO = false;
            string maneuverLabel = type == SpecialAttackType.Grapple ? "Grapple" : "Sunder";

            if (type == SpecialAttackType.Grapple)
                attackerIgnoresAoO = attacker.Stats != null && attacker.Stats.HasFeat("Improved Grapple");
            else if (type == SpecialAttackType.Sunder)
                attackerIgnoresAoO = attacker.Stats != null && attacker.Stats.HasFeat("Improved Sunder");

            bool targetCanAoO = target != null && !target.Stats.IsDead && ThreatSystem.CanMakeAoO(target);
            if (!attackerIgnoresAoO && targetCanAoO)
            {
                CombatResult maneuverAoO = ThreatSystem.ExecuteAoO(target, attacker);
                if (maneuverAoO != null)
                {
                    CombatUI.ShowCombatLog($"⚔ {maneuverLabel} initiation AoO: {maneuverAoO.GetDetailedSummary()}");
                    UpdateAllStatsUI();

                    if (maneuverAoO.Hit)
                    {
                        CombatUI.ShowCombatLog($"{maneuverLabel} attempt disrupted by attack of opportunity");
                        Grid.ClearAllHighlights();
                        _highlightedCells.Clear();
                        _isSelectingSpecialAttack = false;
                        if (type == SpecialAttackType.Sunder)
                        {
                            _pendingSunderUseOffHandSelection = false;
                            ClearSunderSequenceState();
                        }
                        StartCoroutine(AfterAttackDelay(attacker, 0.8f));
                        return;
                    }
                }

                if (attacker.Stats.IsDead)
                {
                    CombatUI.ShowCombatLog($"💀 {attacker.Stats.CharacterName} is dropped while attempting to start {maneuverLabel.ToLowerInvariant()}.");
                    Grid.ClearAllHighlights();
                    _highlightedCells.Clear();
                    _isSelectingSpecialAttack = false;
                    if (type == SpecialAttackType.Sunder)
                    {
                        _pendingSunderUseOffHandSelection = false;
                        ClearSunderSequenceState();
                    }
                    StartCoroutine(AfterAttackDelay(attacker, 0.8f));
                    return;
                }
            }
        }

        SpecialAttackResult result = attacker.ExecuteSpecialAttack(
            type,
            target,
            disarmTargetSlot,
            disarmAttackBonusOverride,
            grappleAttackBonusOverride,
            bullRushAttackBonusOverride,
            bullRushChargeBonusOverride: type == SpecialAttackType.BullRushCharge ? 2 : 0,
            disarmAttackerWeaponOverride: disarmAttackerWeaponOverride,
            tripAttackBonusOverride: tripAttackBonusOverride,
            disarmUsedOffHand: disarmUsedOffHand,
            disarmDualWieldPenaltyForLog: disarmDualWieldPenaltyForLog,
            sunderTargetSlot: sunderTargetSlot,
            sunderAttackBonusOverride: sunderAttackBonusOverride,
            sunderAttackerWeaponOverride: sunderAttackerWeaponOverride,
            sunderUsedOffHand: sunderUsedOffHand,
            sunderDualWieldPenaltyForLog: sunderDualWieldPenaltyForLog);
        if (type == SpecialAttackType.Disarm)
            CombatUI.ShowCombatLog(result.Log);
        else
            CombatUI.ShowCombatLog($"⚔ SPECIAL [{type}] ({actionLabel}): {result.Log}");
        if (type == SpecialAttackType.Grapple)
        {
            int attacksRemaining = GetRemainingGrappleAttackActions(attacker);
            int nextBab = GetCurrentGrappleAttackBonus(attacker);
            Debug.Log($"[GameManager][Grapple] Result success={result.Success} actor={attacker.Stats.CharacterName} remainingSharedPool={attacksRemaining} nextBAB={CharacterStats.FormatMod(nextBab)} phase={CurrentPhase} subPhase={CurrentSubPhase}");

            if (attacksRemaining > 0)
                CombatUI?.ShowCombatLog($"↻ {attacker.Stats.CharacterName} has {attacksRemaining} grapple attack(s) remaining (next BAB {CharacterStats.FormatMod(nextBab)}).");
            else
                CombatUI?.ShowCombatLog($"↻ {attacker.Stats.CharacterName} has no grapple attacks remaining this turn.");
        }
        else if (type == SpecialAttackType.BullRushAttack)
        {
            int attacksRemaining = GetRemainingBullRushAttackActions(attacker);
            int nextBab = GetCurrentBullRushAttackBonus(attacker);
            Debug.Log($"[GameManager][BullRushAttack] Result success={result.Success} actor={attacker.Stats.CharacterName} remainingSharedPool={attacksRemaining} nextBAB={CharacterStats.FormatMod(nextBab)} phase={CurrentPhase} subPhase={CurrentSubPhase}");

            if (attacksRemaining > 0)
                CombatUI?.ShowCombatLog($"↻ {attacker.Stats.CharacterName} has {attacksRemaining} Bull Rush (Attack) attempt(s) remaining (next BAB {CharacterStats.FormatMod(nextBab)}).");
            else
                CombatUI?.ShowCombatLog($"↻ {attacker.Stats.CharacterName} has no Bull Rush (Attack) attempts remaining this turn.");
        }
        else if (type == SpecialAttackType.Trip)
        {
            int attacksRemaining = GetRemainingTripAttackActions(attacker);
            int nextBab = GetCurrentTripAttackBonus(attacker);
            Debug.Log($"[GameManager][Trip] Result success={result.Success} actor={attacker.Stats.CharacterName} remainingSharedPool={attacksRemaining} nextBAB={CharacterStats.FormatMod(nextBab)} phase={CurrentPhase} subPhase={CurrentSubPhase}");

            if (attacksRemaining > 0)
                CombatUI?.ShowCombatLog($"↻ {attacker.Stats.CharacterName} has {attacksRemaining} trip attempt(s) remaining (next BAB {CharacterStats.FormatMod(nextBab)}).");
            else
                CombatUI?.ShowCombatLog($"↻ {attacker.Stats.CharacterName} has no trip attempts remaining this turn.");
        }
        else if (type == SpecialAttackType.Disarm)
        {
            int attacksRemaining = GetRemainingDisarmAttackActions(attacker);
            int nextBab = GetCurrentDisarmAttackBonus(attacker);
            int targetDisarmableItems = target != null ? target.GetDisarmableHeldItemOptions().Count : 0;
            string handLabel = disarmUsedOffHand ? "off-hand" : "main-hand";

            Debug.Log(
                $"[Disarm][Flow] Completed attempt {_disarmAttemptNumber} attacker={attacker.Stats.CharacterName} " +
                $"target={(target != null && target.Stats != null ? target.Stats.CharacterName : "<null>")} " +
                $"success={result.Success} hand={handLabel} usedBAB={CharacterStats.FormatMod(disarmAttackBonusUsed)} " +
                $"attacksRemaining={attacksRemaining} nextBAB={CharacterStats.FormatMod(nextBab)} targetDisarmableItems={targetDisarmableItems}");

            CombatUI?.ShowCombatLog($"[Disarm] Attempt #{_disarmAttemptNumber} ({handLabel}) used BAB {CharacterStats.FormatMod(disarmAttackBonusUsed)}.");

            if (attacksRemaining > 0)
                CombatUI?.ShowCombatLog($"↻ {attacker.Stats.CharacterName} has {attacksRemaining} disarm-capable attack(s) remaining (next BAB {CharacterStats.FormatMod(nextBab)}).");
            else
                CombatUI?.ShowCombatLog($"↻ {attacker.Stats.CharacterName} has no disarm-capable attacks remaining this turn.");

            _pendingDisarmUseOffHandSelection = false;
            ClearDisarmSequenceState();
        }
        else if (type == SpecialAttackType.Sunder)
        {
            int attacksRemaining = GetRemainingSunderAttackActions(attacker);
            int nextBab = GetCurrentSunderAttackBonus(attacker);
            int targetSunderableItems = target != null ? target.GetSunderableItemOptions().Count : 0;
            string handLabel = sunderUsedOffHand ? "off-hand" : "main-hand";

            Debug.Log(
                $"[Sunder][Flow] Completed attempt {_sunderAttemptNumber} attacker={attacker.Stats.CharacterName} " +
                $"target={(target != null && target.Stats != null ? target.Stats.CharacterName : "<null>")} " +
                $"success={result.Success} hand={handLabel} usedBAB={CharacterStats.FormatMod(sunderAttackBonusUsed)} " +
                $"attacksRemaining={attacksRemaining} nextBAB={CharacterStats.FormatMod(nextBab)} targetSunderableItems={targetSunderableItems}");

            CombatUI?.ShowCombatLog($"[Sunder] Attempt #{_sunderAttemptNumber} ({handLabel}) used BAB {CharacterStats.FormatMod(sunderAttackBonusUsed)}.");

            if (attacksRemaining > 0)
                CombatUI?.ShowCombatLog($"↻ {attacker.Stats.CharacterName} has {attacksRemaining} sunder-capable attack(s) remaining (next BAB {CharacterStats.FormatMod(nextBab)}).");
            else
                CombatUI?.ShowCombatLog($"↻ {attacker.Stats.CharacterName} has no sunder-capable attacks remaining this turn.");

            _pendingSunderUseOffHandSelection = false;
            ClearSunderSequenceState();
        }

        if (result.Success)
        {
            if (type == SpecialAttackType.BullRushAttack || type == SpecialAttackType.BullRushCharge)
            {
                ResolveBullRushPushAndFollow(attacker, target, result, () => FinalizeSpecialAttackResolution(attacker, target));
                return;
            }

            if (type == SpecialAttackType.Overrun)
                TryPushTargetAway(attacker, target, 1, allowAttackerFollow: true);
        }

        FinalizeSpecialAttackResolution(attacker, target);
    }

    private void FinalizeSpecialAttackResolution(CharacterController attacker, CharacterController target)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        _isSelectingSpecialAttack = false;

        UpdateAllStatsUI();

        if (target != null && target.Stats != null && target.Stats.IsDead && !target.IsPlayerControlled && AreAllNPCsDead())
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI.SetTurnIndicator("VICTORY! All enemies defeated!");
            CombatUI.SetActionButtonsVisible(false);
            return;
        }

        if (attacker != null)
            StartCoroutine(AfterAttackDelay(attacker, 1.0f));
    }

    private struct BullRushPushResolution
    {
        public Vector2Int Direction;
        public Vector2Int OriginalTargetPosition;
        public Vector2Int FinalTargetPosition;
        public int RequestedSquares;
        public int ActualSquares;
        public bool Obstructed;

        public bool TargetMoved => ActualSquares > 0;
    }


    private void TryPushTargetAway(CharacterController attacker, CharacterController target, int squares, bool allowAttackerFollow)
    {
        BullRushPushResolution pushResolution = ExecuteBullRushPush(attacker, target, squares);
        if (allowAttackerFollow)
            ExecuteBullRushFollow(attacker, pushResolution);
    }

    private void CancelSpecialAttackTargeting()
    {
        _isSelectingSpecialAttack = false;
        _pendingDisarmUseOffHandSelection = false;
        _pendingSunderUseOffHandSelection = false;
        ClearDisarmSequenceState();
        ClearSunderSequenceState();
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        ShowActionChoices();
    }

    // ========== CHARGE ACTION (D&D 3.5e PHB p.154) ==========


    // ========== ATTACK EXECUTION ==========

    private void PerformPlayerAttack(CharacterController attacker, CharacterController target)
    {
        if (_combatFlowService != null)
        {
            _combatFlowService.PerformPlayerAttack(attacker, target);
            return;
        }

        CurrentSubPhase = PlayerSubPhase.Animating;
    }

    private RangeInfo CalculateRangeInfo(CharacterController attacker, CharacterController target)
    {
        if (_combatFlowService != null)
            return _combatFlowService.CalculateRangeInfo(attacker, target);

        return RangeCalculator.GetRangeInfo(0, 0, false);
    }

    private string BuildAttackLog(CharacterController attacker, bool isFlanking, string partnerName, CombatResult result)
    {
        if (_combatFlowService != null)
            return _combatFlowService.BuildAttackLog(attacker, isFlanking, partnerName, result);

        return result != null ? result.GetDetailedSummary() : string.Empty;
    }

    private void PerformIterativeSequenceAttack(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo = null)
    {
        if (_combatFlowService != null)
        {
            _combatFlowService.PerformIterativeSequenceAttack(attacker, target, isFlanking, flankBonus, partnerName, rangeInfo);
            return;
        }
    }

    private void PerformSingleAttack(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo = null)
    {
        if (_combatFlowService != null)
        {
            _combatFlowService.PerformSingleAttack(attacker, target, isFlanking, flankBonus, partnerName, rangeInfo);
            return;
        }
    }

    private IEnumerator PerformFullAttackWithRetargetingAndFiveFootStep(CharacterController attacker, CharacterController initialTarget)
    {
        if (attacker == null || initialTarget == null)
        {
            ShowActionChoices();
            yield break;
        }

        attacker.Actions.UseFullRoundAction();

        bool rangedMode = IsAttackModeRanged(attacker);
        string modeLabel = rangedMode ? "ranged" : "melee";

        RangeInfo initialRangeInfo = CalculateRangeInfo(attacker, initialTarget);
        int plannedAttackCount = attacker.GetPlannedFullAttackCount(initialRangeInfo);
        if (plannedAttackCount <= 0)
        {
            CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} has no available attacks.");
            StartCoroutine(DelayedEndActivePCTurn(0.8f));
            yield break;
        }

        CharacterController currentTarget = initialTarget;
        int attacksMade = 0;

        // D&D 3.5: You can take a 5-foot step before a full attack.
        if (CanTakeFiveFootStep(attacker))
        {
            yield return StartCoroutine(WaitForOptionalFiveFootStepDuringFullAttack(
                attacker,
                "Before attacks:",
                requireReachableTargetAfterStep: false,
                rangedMode: rangedMode));
        }

        for (int attackIndex = 0; attackIndex < plannedAttackCount; attackIndex++)
        {
            if (attacker == null || attacker.Stats == null || attacker.Stats.IsDead)
                break;

            int remainingAttacks = plannedAttackCount - attackIndex;
            bool needsRetarget = currentTarget == null
                || currentTarget.Stats == null
                || currentTarget.Stats.IsDead
                || !IsTargetInCurrentWeaponRange(attacker, currentTarget);

            if (needsRetarget)
            {
                if (currentTarget != null && currentTarget.Stats != null && !currentTarget.Stats.IsDead)
                {
                    CombatUI?.ShowCombatLog($"⚠ {currentTarget.Stats.CharacterName} is no longer in {modeLabel} reach.");
                }

                List<CharacterController> validTargets = GetValidTargetsForCurrentWeapon(attacker);

                if (validTargets.Count == 0 && CanTakeFiveFootStep(attacker))
                {
                    CombatUI?.ShowCombatLog($"No valid {modeLabel} targets right now. You may take a 5-foot step to continue.");

                    yield return StartCoroutine(WaitForOptionalFiveFootStepDuringFullAttack(
                        attacker,
                        "Step to reach another target:",
                        requireReachableTargetAfterStep: true,
                        rangedMode: rangedMode));

                    if (_fullAttackFiveFootStepSelectionCancelled || !_fullAttackFiveFootStepWasTaken)
                    {
                        CombatUI?.ShowCombatLog($"↩ {attacker.Stats.CharacterName} ends full attack early. {remainingAttacks} attack(s) unused.");
                        break;
                    }

                    validTargets = GetValidTargetsForCurrentWeapon(attacker);
                }

                if (validTargets.Count == 0)
                {
                    CombatUI?.ShowCombatLog($"⚠ No valid {modeLabel} targets for {remainingAttacks} remaining attack(s).");
                    break;
                }

                yield return StartCoroutine(WaitForFullAttackRetargetSelection(attacker, remainingAttacks));

                if (_rangedRetargetSelectionCancelled || _selectedRangedRetarget == null)
                {
                    CombatUI?.ShowCombatLog($"↩ {attacker.Stats.CharacterName} ends full attack early. {remainingAttacks} attack(s) unused.");
                    break;
                }

                currentTarget = _selectedRangedRetarget;
                _selectedRangedRetarget = null;
                _rangedRetargetSelectionCancelled = false;

                CombatUI?.ShowCombatLog($"🎯 {attacker.Stats.CharacterName} switches to {currentTarget.Stats.CharacterName}.");
            }

            // Recompute flanking/range context each attack in case target/position changed.
            var allCombatants = GetAllCharacters();
            CharacterController flankPartner;
            bool isFlanking = CombatUtils.IsAttackerFlanking(attacker, currentTarget, allCombatants, out flankPartner);
            int flankBonus = isFlanking ? CombatUtils.FlankingAttackBonus : 0;
            string partnerName = flankPartner != null ? flankPartner.Stats.CharacterName : "";
            RangeInfo rangeInfo = CalculateRangeInfo(attacker, currentTarget);

            bool isMeleeFearBreakAttack = IsMeleeAttackForTurnUndeadFearBreak(
                attacker,
                attacker.GetEquippedMainWeapon(),
                rangeInfo,
                treatAsThrownAttack: false);
            ProcessTurnUndeadMeleeFearBreak(attacker, currentTarget, isMeleeFearBreakAttack);

            FullAttackResult stepResult = attacker.FullAttack(
                currentTarget,
                isFlanking,
                flankBonus,
                partnerName,
                rangeInfo,
                startAttackIndex: attackIndex,
                maxAttacks: 1);

            if (stepResult == null || stepResult.Attacks.Count == 0)
                break;

            attacksMade++;
            CombatResult attack = stepResult.Attacks[0];
            string label = (stepResult.AttackLabels != null && stepResult.AttackLabels.Count > 0)
                ? stepResult.AttackLabels[0]
                : $"Attack {attackIndex + 1}";

            CombatUI?.ShowCombatLog(attack.GetAttackBreakdown(label));
            UpdateAllStatsUI();
            Grid.ClearAllHighlights();
            _highlightedCells.Clear();

            if (attack.Hit && attack.TotalDamage > 0)
                CheckConcentrationOnDamage(currentTarget, attack.TotalDamage);

            if (attack.TargetKilled)
            {
                HandleSummonDeathCleanup(currentTarget);

                if (!currentTarget.IsPlayerControlled && AreAllNPCsDead())
                {
                    CurrentPhase = TurnPhase.CombatOver;
                    CombatUI.SetTurnIndicator("VICTORY! All enemies defeated!");
                    CombatUI.SetActionButtonsVisible(false);
                    yield break;
                }

                int attacksRemainingAfterKill = plannedAttackCount - (attackIndex + 1);
                if (attacksRemainingAfterKill > 0)
                {
                    CombatUI?.ShowCombatLog($"💀 {currentTarget.Stats.CharacterName} is defeated! {attacksRemainingAfterKill} attack(s) remaining.");
                    currentTarget = null;
                }
            }

            // D&D 3.5: You can 5-foot step between attacks during a full attack.
            if (attackIndex < plannedAttackCount - 1 && CanTakeFiveFootStep(attacker))
            {
                yield return StartCoroutine(WaitForOptionalFiveFootStepDuringFullAttack(
                    attacker,
                    "Between attacks:",
                    requireReachableTargetAfterStep: false,
                    rangedMode: rangedMode));
            }

            yield return new WaitForSeconds(0.35f);
        }

        // D&D 3.5: You can also 5-foot step after attacks.
        if (CanTakeFiveFootStep(attacker) && CurrentPhase != TurnPhase.CombatOver)
        {
            yield return StartCoroutine(WaitForOptionalFiveFootStepDuringFullAttack(
                attacker,
                "After attacks:",
                requireReachableTargetAfterStep: false,
                rangedMode: rangedMode));
        }

        _isAwaitingRangedRetargetSelection = false;
        _selectedRangedRetarget = null;
        _rangedRetargetSelectionCancelled = false;
        _isAwaitingFullAttackFiveFootStepSelection = false;
        _fullAttackFiveFootStepSelectionCancelled = false;
        _fullAttackFiveFootStepWasTaken = false;

        CombatUI?.ShowCombatLog($"✅ {attacker.Stats.CharacterName} completes {modeLabel} full attack ({attacksMade}/{plannedAttackCount} attacks used).");
        UpdateAllStatsUI();
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        StartCoroutine(DelayedEndActivePCTurn(1.0f));
    }
    private void PerformFullAttack(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo = null)
    {
        if (_combatFlowService != null)
        {
            _combatFlowService.PerformFullAttack(attacker, target, isFlanking, flankBonus, partnerName, rangeInfo);
            return;
        }
    }

    private void PerformDualWieldAttack(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo = null)
    {
        if (_combatFlowService != null)
        {
            _combatFlowService.PerformDualWieldAttack(attacker, target, isFlanking, flankBonus, partnerName, rangeInfo);
            return;
        }
    }

    private void PerformFlurryOfBlows(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo = null)
    {
        if (_combatFlowService != null)
        {
            _combatFlowService.PerformFlurryOfBlows(attacker, target, isFlanking, flankBonus, partnerName, rangeInfo);
            return;
        }
    }

    private bool IsHoldingTouchCharge(CharacterController character)
    {
        if (character == null || !character.Stats.IsSpellcaster)
            return false;

        var spellComp = character.GetComponent<SpellcastingComponent>();
        return spellComp != null && spellComp.HasHeldTouchCharge && spellComp.HeldTouchSpell != null;
    }

    private string GetHeldTouchSpellName(CharacterController character)
    {
        var spellComp = character != null ? character.GetComponent<SpellcastingComponent>() : null;
        if (spellComp != null && spellComp.HeldTouchSpell != null)
            return spellComp.HeldTouchSpell.Name;
        return "held touch spell";
    }

    private bool ShouldAutoEndTurn(CharacterController character)
    {
        if (character == null)
            return true;

        if (character.IsPlayerControlled)
        {
            bool offHandAvailable = CanUseOffHandAttackOption(character);
            bool offHandThrownAvailable = CanUseOffHandThrownAttackOption(character);
            Debug.Log($"[TurnFlow] ShouldAutoEndTurn=false for player {character.Stats.CharacterName}. " +
                      $"Manual End Turn required. offHandAvailable={offHandAvailable} offHandThrownAvailable={offHandThrownAvailable} " +
                      $"offHandGate={_offHandAttackAvailableThisTurn} offHandUsed={_offHandAttackUsedThisTurn} attacksUsed={_totalAttacksUsed}/{_totalAttackBudget}");
            return false;
        }

        bool hasRemainingGrappleAttempts = CanUseGrappleAttackOption(character);
        bool hasRemainingBullRushAttempts = CanUseBullRushAttackOption(character);
        bool hasRemainingTripAttempts = CanUseTripAttackOption(character);
        bool hasRemainingDisarmAttempts = CanUseDisarmAttackOption(character);

        bool hasIterativeWeaponAttackSequence = _isInAttackSequence && _attackingCharacter == character;

        if (hasRemainingGrappleAttempts || hasRemainingBullRushAttempts || hasRemainingTripAttempts || hasRemainingDisarmAttempts || hasIterativeWeaponAttackSequence)
        {
            Debug.Log(
                $"[TurnFlow] ShouldAutoEndTurn=false for {character.Stats.CharacterName}: " +
                $"iterativeRemaining(g={hasRemainingGrappleAttempts}, br={hasRemainingBullRushAttempts}, trip={hasRemainingTripAttempts}, d={hasRemainingDisarmAttempts}, atk={hasIterativeWeaponAttackSequence})");
            return false;
        }

        bool shouldAutoEnd = !character.Actions.HasAnyActionLeft && !IsHoldingTouchCharge(character);
        Debug.Log($"[TurnFlow] ShouldAutoEndTurn character={character.Stats.CharacterName} hasAnyActionLeft={character.Actions.HasAnyActionLeft} holdingTouchCharge={IsHoldingTouchCharge(character)} => {shouldAutoEnd}");
        return shouldAutoEnd;
    }

    private IEnumerator AfterAttackDelay(CharacterController pc, float delay)
    {
        LogMenuFlow("AfterAttackDelay:START", pc, $"delay={delay:0.00}");
        yield return new WaitForSeconds(delay);

        LogMenuFlow("AfterAttackDelay:AFTER_WAIT", pc, $"delay={delay:0.00}");

        if (CurrentPhase == TurnPhase.CombatOver)
        {
            LogMenuFlow("AfterAttackDelay:ABORT_COMBAT_OVER", pc);
            yield break;
        }

        bool shouldEndTurn = ShouldAutoEndTurn(pc);
        LogMenuFlow("AfterAttackDelay:DECISION", pc, $"shouldAutoEndTurn={shouldEndTurn}");

        if (shouldEndTurn)
        {
            EndActivePCTurn();
        }
        else
        {
            Debug.Log("=== ATTACK SEQUENCE COMPLETE ===");
            Debug.Log($"[OffHand] _offHandAttackAvailableThisTurn: {_offHandAttackAvailableThisTurn}");
            Debug.Log($"[OffHand] _offHandAttackUsedThisTurn: {_offHandAttackUsedThisTurn}");
            Debug.Log("[Actions] Calling ShowActionChoices()");
            ShowActionChoices();
        }
    }

    // ========== TURN ENDING ==========

    /// <summary>
    /// End the current PC's turn and advance to the next combatant in initiative order.
    /// </summary>
    private void EndActivePCTurn()
    {
        CharacterController pc = ActivePC;
        EndAttackSequence();
        EndThrownAttackSequence();
        ResetOffHandTurnState();
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        if (CurrentPhase == TurnPhase.CombatOver) return;

        // Advance to next in initiative order
        NextInitiativeTurn();
    }

    private IEnumerator DelayedEndActivePCTurn(float delay)
    {
        LogMenuFlow("DelayedEndActivePCTurn:START", ActivePC, $"delay={delay}");

        yield return new WaitForSeconds(delay);

        LogMenuFlow("DelayedEndActivePCTurn:AFTER_DELAY", ActivePC, $"delay={delay}");

        if (CurrentPhase == TurnPhase.CombatOver)
            yield break;

        // SAFETY CHECK: if a submenu opened during the delay window, do not force-close it.
        if (CombatUI != null && CombatUI.IsSpecialStyleSelectionMenuOpen())
        {
            LogMenuFlow("DelayedEndActivePCTurn:ABORT_SUBMENU_OPEN", ActivePC, "Submenu open after delay");
            Debug.Log("[GameManager][MenuFlow] DelayedEndActivePCTurn: Submenu open, aborting");
            yield break;
        }

        CharacterController pc = ActivePC;
        if (ShouldAutoEndTurn(pc))
            EndActivePCTurn();
        else
            ShowActionChoices();
    }

    // ========== NPC AI TURN (Initiative-Based) ==========

    /// <summary>
    /// Execute a single NPC turn triggered by the initiative system.
    /// </summary>
    private IEnumerator SingleNPCTurnFromInitiative(CharacterController npc)
    {
        CurrentPhase = TurnPhase.NPCTurn;
        CombatUI.SetActivePC(0); // No PC active
        CombatUI.SetActiveNPC(NPCs.IndexOf(npc)); // Highlight active NPC
        CombatUI.SetActionButtonsVisible(false);
        CombatUI.HideSummonContextMenu();

        // Update initiative UI to highlight current NPC
        UpdateInitiativeUI();

        ExpireAidBonusesAtTurnStart(npc);

        if (ShouldSkipTurnDueToHPState(npc))
        {
            CombatUI.SetActiveNPC(-1); // Clear NPC highlight
            if (npc != null && npc.Stats != null)
            {
                string reason = npc.CurrentHPState == HPState.Dead ? "is dead" : "is unconscious";
                CombatUI?.ShowCombatLog($"⏭ {npc.Stats.CharacterName} {reason} and cannot act this turn.");
            }
            NextInitiativeTurn();
            yield break;
        }

        // Determine AI behavior for this NPC
        EnemyAIBehavior behavior = GetNPCBehaviorForAI(npc);
        if (_aiService != null)
            yield return StartCoroutine(_aiService.ExecuteNPCTurn(npc, behavior));

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

    private IEnumerator AI_SummonedCreature(CharacterController summon)
    {
        ActiveSummonInstance data = GetActiveSummon(summon);
        if (data == null)
            yield break;

        if (data.CurrentCommand != null && data.CurrentCommand.Type == SummonCommandType.ProtectCaster && data.Caster != null && data.Caster.Stats != null)
        {
            CombatUI.ShowCombatLog($"<color=#66E8FF>{GetSummonDisplayName(summon)} protects {data.Caster.Stats.CharacterName}.</color>");
        }

        CharacterController target = SelectSummonTargetByCommand(summon, data);
        if (target == null)
            yield break;

        bool lowHP = summon.Stats != null && summon.Stats.TotalMaxHP > 0 && summon.Stats.CurrentHP <= Mathf.CeilToInt(summon.Stats.TotalMaxHP * 0.30f);

        if (lowHP && _aiService != null)
        {
            SquareCell retreat = _aiService.EvaluateMovementOptions(summon, target.GridPosition, retreat: true);
            if (retreat != null && retreat.Coords != summon.GridPosition)
            {
                yield return StartCoroutine(MoveCharacterAlongComputedPath(summon, retreat.Coords, PlayerMoveSecondsPerStep));
                if (summon.Actions.HasMoveAction)
                    summon.Actions.UseMoveAction();
                CombatUI.ShowCombatLog($"<color=#FFCC66>{GetSummonDisplayName(summon)} withdraws to survive.</color>");
                yield return new WaitForSeconds(0.45f);
            }
        }

        if (!summon.IsTargetInCurrentWeaponRange(target) && summon.Actions.HasMoveAction && _aiService != null)
        {
            SquareCell bestCell = _aiService.EvaluateMovementOptions(summon, target.GridPosition, retreat: false, target);
            if (bestCell != null)
            {
                yield return StartCoroutine(MoveCharacterAlongComputedPath(summon, bestCell.Coords, PlayerMoveSecondsPerStep));
                summon.Actions.UseMoveAction();
                CombatUI.ShowCombatLog($"<color=#66E8FF>{GetSummonDisplayName(summon)} closes in on {target.Stats.CharacterName}.</color>");
                yield return new WaitForSeconds(0.4f);
            }
        }

        target = SelectSummonTargetByCommand(summon, data);
        if (target == null)
            yield break;

        if (!summon.IsTargetInCurrentWeaponRange(target) || target.Stats.IsDead)
            yield break;

        if (data.Template != null && data.Template.HasTrip && !target.Stats.IsProne && summon.Actions.HasStandardAction)
        {
            var trip = summon.ExecuteSpecialAttack(SpecialAttackType.Trip, target);
            CombatUI.ShowCombatLog($"<color=#66E8FF>✦ {GetSummonDisplayName(summon)} attempts Trip: {trip.Log}</color>");
            summon.CommitStandardAction();
            UpdateAllStatsUI();
            yield return new WaitForSeconds(0.65f);
            yield break;
        }

        if (TryExecuteSummonSmiteAttack(summon, target, data))
        {
            UpdateAllStatsUI();
            yield return new WaitForSeconds(0.8f);
            yield break;
        }

        yield return StartCoroutine(NPCPerformAttack(summon, target));
    }

    private CharacterController SelectSummonTargetByCommand(CharacterController summon, ActiveSummonInstance summonData)
    {
        if (summon == null)
            return null;

        List<CharacterController> enemies = new List<CharacterController>();
        foreach (var candidate in GetAllCharacters())
        {
            if (candidate == null || candidate == summon || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!IsEnemyTeam(summon, candidate))
                continue;
            enemies.Add(candidate);
        }

        if (enemies.Count == 0)
            return null;

        SummonCommandType cmd = summonData != null && summonData.CurrentCommand != null
            ? summonData.CurrentCommand.Type
            : SummonCommandType.AttackNearest;

        switch (cmd)
        {
            case SummonCommandType.ProtectCaster:
                return FindEnemyNearestToSummoner(enemies, summonData != null ? summonData.Caster : null, summon);
            case SummonCommandType.AttackNearest:
            default:
                return FindNearestEnemyToSummon(enemies, summon);
        }
    }

    private CharacterController FindNearestEnemyToSummon(List<CharacterController> enemies, CharacterController summon)
    {
        CharacterController nearest = null;
        int nearestDist = int.MaxValue;

        for (int i = 0; i < enemies.Count; i++)
        {
            CharacterController enemy = enemies[i];
            int dist = SquareGridUtils.GetDistance(summon.GridPosition, enemy.GridPosition);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = enemy;
            }
        }

        return nearest;
    }

    private CharacterController FindEnemyNearestToSummoner(List<CharacterController> enemies, CharacterController summoner, CharacterController summon)
    {
        if (summoner == null)
            return FindNearestEnemyToSummon(enemies, summon);

        CharacterController nearest = null;
        int nearestDist = int.MaxValue;

        for (int i = 0; i < enemies.Count; i++)
        {
            CharacterController enemy = enemies[i];
            int dist = SquareGridUtils.GetDistance(summoner.GridPosition, enemy.GridPosition);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = enemy;
            }
        }

        return nearest;
    }

    private bool TryExecuteSummonSmiteAttack(CharacterController summon, CharacterController target, ActiveSummonInstance summonData)
    {
        if (summon == null || target == null || summonData == null || summonData.Template == null)
            return false;
        if (summonData.SmiteUsed)
            return false;
        if (!summon.Actions.HasStandardAction)
            return false;

        bool smiteEvil = summonData.Template.IsCelestial && AlignmentHelper.IsEvil(target.Stats.CharacterAlignment);
        bool smiteGood = summonData.Template.IsFiendish && AlignmentHelper.IsGood(target.Stats.CharacterAlignment);
        if (!smiteEvil && !smiteGood)
            return false;

        int attackBonus = Mathf.Max(1, summon.Stats.CHAMod + 2);
        int damageBonus = Mathf.Max(1, summon.Stats.Level + 2);

        summon.Stats.MoraleAttackBonus += attackBonus;
        summon.Stats.MoraleDamageBonus += damageBonus;

        CombatResult result;
        try
        {
            CharacterController flankPartner;
            bool isFlanking = CombatUtils.IsAttackerFlanking(summon, target, GetAllCharacters(), out flankPartner);
            int flankBonus = isFlanking ? CombatUtils.FlankingAttackBonus : 0;
            result = summon.Attack(target, isFlanking, flankBonus, flankPartner != null ? flankPartner.Stats.CharacterName : null, null);
        }
        finally
        {
            summon.Stats.MoraleAttackBonus -= attackBonus;
            summon.Stats.MoraleDamageBonus -= damageBonus;
        }

        summon.CommitStandardAction();
        summonData.SmiteUsed = true;

        string targetAxis = smiteEvil ? "Evil" : "Good";
        CombatUI.ShowCombatLog($"<color=#FFD280>✦ {GetSummonDisplayName(summon)} uses Smite {targetAxis}! {result.GetDetailedSummary()}</color>");

        if (result.TargetKilled)
            HandleSummonDeathCleanup(target);

        return true;
    }

    private void TryResolveFreeTripOnHit(CharacterController attacker, CharacterController target, CombatResult attackResult, RangeInfo attackRange)
    {
        if (attacker == null || target == null || attacker.Stats == null || target.Stats == null || attackResult == null)
            return;

        if (!attacker.Stats.HasTripAttack)
            return;

        if (attackRange == null || !attackRange.IsMelee)
            return;

        if (!attackResult.Hit || target.Stats.IsDead || target.HasCondition(CombatConditionType.Prone))
            return;

        SpecialAttackResult tripResult = attacker.ExecuteSpecialAttack(SpecialAttackType.Trip, target);
        string tripContext = tripResult.Success
            ? "free trip follow-up"
            : "free trip attempt failed";

        CombatUI?.ShowCombatLog($"☠ {attacker.Stats.CharacterName} follows up with Trip ({tripContext}): {tripResult.Log}");
        Debug.Log($"[NPC Trip Follow-up] {attacker.Stats.CharacterName} triggered free trip after hit. Success={tripResult.Success}");
    }

    private bool TryNPCSpecialAttackIfBeneficial(CharacterController npc, CharacterController target)
    {
        return TryNPCSpecialAttackIfBeneficial(npc, target, null);
    }

    private bool TryNPCSpecialAttackIfBeneficial(CharacterController npc, CharacterController target, SpecialAttackType? forcedChoice)
    {
        if (npc == null || target == null) return false;
        if (npc.IsGrappling()) return false;
        if (!npc.Actions.HasStandardAction) return false;

        SpecialAttackType? choice = forcedChoice;

        if (!choice.HasValue)
        {
            if (!target.Stats.IsProne && npc.HasMeleeWeaponEquipped())
                choice = SpecialAttackType.Trip;

            if (choice == null && target.GetEquippedMainWeapon() != null && npc.Stats.STRMod >= 3)
                choice = SpecialAttackType.Disarm;

            if (choice == null && npc.Stats.STRMod >= 4)
                choice = SpecialAttackType.Grapple;
        }

        if (choice == null) return false;

        var result = npc.ExecuteSpecialAttack(choice.Value, target);
        CombatUI.ShowCombatLog($"☠ {npc.Stats.CharacterName} uses SPECIAL [{choice.Value}]! {result.Log}");

        if (result.Success)
        {
            if (choice.Value == SpecialAttackType.BullRushAttack || choice.Value == SpecialAttackType.BullRushCharge)
                ResolveBullRushPushAndFollow(npc, target, result, onComplete: null);
            else if (choice.Value == SpecialAttackType.Overrun)
                TryPushTargetAway(npc, target, 1, allowAttackerFollow: true);
        }

        npc.CommitStandardAction();
        UpdateAllStatsUI();
        return true;
    }

    private IEnumerator NPCPerformAttack(CharacterController npc, CharacterController target)
    {
        if (!npc.CanAttackWithEquippedWeapon(out string cannotAttackReason))
        {
            if (ExecuteReload(npc, out string reloadLog))
            {
                CombatUI.ShowCombatLog(reloadLog);
                UpdateAllStatsUI();
                yield return new WaitForSeconds(0.8f);
                yield break;
            }

            CombatUI.ShowCombatLog($"⚠ {npc.Stats.CharacterName} cannot attack: {cannotAttackReason}");
            yield return new WaitForSeconds(0.6f);
            yield break;
        }

        npc.CommitStandardAction();
        RangeInfo npcRangeInfo = CalculateRangeInfo(npc, target);

        CharacterController flankPartner;
        bool isFlanking = CombatUtils.IsAttackerFlanking(npc, target, GetAllCharacters(), out flankPartner);
        int flankBonus = isFlanking ? CombatUtils.FlankingAttackBonus : 0;

        bool isMeleeFearBreakAttack = IsMeleeAttackForTurnUndeadFearBreak(
            npc,
            npc.GetEquippedMainWeapon(),
            npcRangeInfo,
            treatAsThrownAttack: false);
        ProcessTurnUndeadMeleeFearBreak(npc, target, isMeleeFearBreakAttack);

        CombatResult result = npc.Attack(target, isFlanking, flankBonus,
            flankPartner != null ? flankPartner.Stats.CharacterName : null, npcRangeInfo);

        TryResolveFreeTripOnHit(npc, target, result, npcRangeInfo);

        string partnerName = flankPartner != null && flankPartner.Stats != null
            ? flankPartner.Stats.CharacterName
            : null;
        _lastCombatLog = BuildAttackLog(npc, isFlanking, partnerName, result);

        if (LogAttacksToConsole)
            Debug.Log("[Combat] " + _lastCombatLog);

        CombatUI.ShowCombatLog(_lastCombatLog);
        UpdateAllStatsUI();

        if (result.Hit && result.TotalDamage > 0)
            CheckConcentrationOnDamage(target, result.TotalDamage);

        if (result.TargetKilled)
        {
            HandleSummonDeathCleanup(target);

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

    // ========== DETAILED CONSOLE LOGGING ==========

    private void LogFullAttackToConsole(FullAttackResult result)
    {
        if (_combatFlowService != null)
        {
            _combatFlowService.LogFullAttackToConsole(result);
            return;
        }
    }

    private void ResetAttackDamageModesForAllCharacters()
    {
        foreach (var character in GetAllCharacters())
        {
            if (character == null)
                continue;

            character.ResetAttackDamageMode();
        }

        CombatUI?.ResetDamageModeToggleVisual();
        Debug.Log("[GameManager] Attack damage modes reset to class/equipment defaults for new round");
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
