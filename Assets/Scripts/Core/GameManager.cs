using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DND35.AI.Profiles;
using DND35.Magic;
using UnityEngine;
using UnityEngine.UI;
using DND35e.Identifiers;

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

    /// <summary>Combat state machine for tracking combat flow phases.</summary>
    public CombatStateMachine CombatState { get; private set; } = new CombatStateMachine();

    /// <summary>Command processor for executing game commands through a unified pipeline.</summary>
    private CommandProcessor _commandProcessor;

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
    private List<NPCAIBehavior> _npcAIBehaviors = new List<NPCAIBehavior>();

    // Legacy alias
    public CharacterController PC { get => PC1; set => PC1 = value; }

    [Header("UI")]
    public CombatUI CombatUI;
    public InventoryUI InventoryUI;
    public CharacterSheetUI CharacterSheetUI;
    public CharacterCreationUI CharacterCreationUI;
    public SkillsUIPanel SkillsUI;
    public SpellPreparationUI SpellPreparationUI;
    public PreCombatHubUI PreCombatHubUI;
    public StoreUI StoreUI;

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

    /// <summary>Pre-combat inventory UI shown after encounter selection and before initiative.</summary>
    public PreCombatInventoryUI PreCombatInventoryUI;

    /// <summary>Quick item use panel for combat — search/filter/sort consumable items.</summary>
    public QuickItemUsePanel QuickItemUsePanel;

    /// <summary>Shared party stash (session-only for now).</summary>
    public PartyStash PartyStash = new PartyStash();

    /// <summary>Whether combat setup is waiting on encounter selection.</summary>
    public bool WaitingForEncounterSelection { get; private set; }

    /// <summary>Whether the pre-combat inventory phase is currently active.</summary>
    public bool WaitingForPreCombatInventory { get; private set; }

    private const string GrappleTestPresetId = "grapple_test";
    private const string GreaseTestPresetId = "grease_test";
    private const string FeintSneakTestPresetId = "feint_sneak_test";
    private const string TurnUndeadTestPresetId = "turn_undead_test";
    private const string ArmorTargetingTestPresetId = "armor_targeting_test";
    private const string TigerHuntTestPresetId = "tiger_hunt_test";
    private const string OgreBattleTestPresetId = "ogre_battle_test";
    private const string ShieldBashTestPresetId = "shield_bash_test";
    private const string CelestialTemplateTestPresetId = "celestial_template_test";
    private const string FiendishTemplateTestPresetId = "fiendish_template_test";
    private const string SummonMonsterTestPresetId = "summon_monster_test";
    private const string NPCMagicMissileTestPresetId = "npc_magic_missile_test";
    private const string ProtectionFromEvilTestPresetId = "protection_from_evil_test";
    private const string WindDispersionTestPresetId = "wind_dispersion_test";
    private const string ObscuringMistRangedOnlyTestPresetId = "obscuring_mist_ranged_only";
    private const string DisruptUndeadTestPresetId = "disrupt_undead_test";
    private const string TrueStrikeTestPresetId = "true_strike_test";
    private const string WizardSpellTestPresetId = "wizard_spell_test";
    private const string ClericSpellTestPresetId = "cleric_spell_test";
    private const string CharmPersonTestPresetId = "charm_person_test";
    private const string SleepSpellTestPresetId = "sleep_spell_test";
    private const string MirrorImageTestPresetId = "mirror_image_test";
    private string _selectedEncounterPresetId = "goblin_raiders";
    private bool _isGrappleTestEncounter;
    private bool _isGreaseTestEncounter;
    private bool _isFeintSneakTestEncounter;
    private bool _isTurnUndeadTestEncounter;
    private bool _isArmorTargetingTestEncounter;
    private bool _isTigerHuntTestEncounter;
    private bool _isOgreBattleTestEncounter;
    private bool _isShieldBashTestEncounter;
    private bool _isCelestialTemplateTestEncounter;
    private bool _isFiendishTemplateTestEncounter;
    private bool _isSummonMonsterTestEncounter;
    private bool _isNpcMagicMissileTestEncounter;
    private bool _isProtectionFromEvilTestEncounter;
    private bool _isWindDispersionTestEncounter;
    private bool _isObscuringMistRangedOnlyTestEncounter;
    private bool _isDisruptUndeadTestEncounter;
    private bool _isTrueStrikeTestEncounter;
    private bool _isWizardSpellTestEncounter;
    private bool _isClericSpellTestEncounter;
    private bool _isCharmPersonTestEncounter;
    private bool _isSleepSpellTestEncounter;
    private bool _isMirrorImageTestEncounter;
    private readonly List<string> _activeEncounterEnemyIds = new List<string>();
    private bool _partyStashSeeded;

    // Party resources — delegated to EconomyService (legacy accessors kept for backward compatibility).
    private int partyGold = 1000;
    public event Action<int> OnGoldChanged;

    /// <summary>Public accessor to the EconomyService for systems that need direct access.</summary>
    public EconomyService Economy => _economyService;

    public int PartyGold
    {
        get => _economyService != null ? _economyService.PartyGold : partyGold;
        set
        {
            if (_economyService != null)
                _economyService.PartyGold = value;
            else
            {
                int clamped = Mathf.Max(0, value);
                if (partyGold == clamped)
                    return;
                partyGold = clamped;
                Debug.Log($"[Gold] Party gold is now {partyGold} gp");
                OnGoldChanged?.Invoke(partyGold);
            }
        }
    }

    public bool SpendGold(int amount)
    {
        if (_economyService != null)
            return _economyService.SpendGold(amount);

        // Fallback (before service init)
        if (amount <= 0) return true;
        if (partyGold >= amount) { PartyGold -= amount; return true; }
        return false;
    }

    public void AddGold(int amount)
    {
        if (_economyService != null)
            _economyService.AddGold(amount);
        else if (amount > 0)
            PartyGold += amount;
    }

    // Endless combat-loop session stats (persist while the session is running).
    public int CompletedCombatCount { get; private set; }
    public int TotalLootItemsCollected { get; private set; }
    public int TotalEncounterXPDefeated { get; private set; }

    // Defeated enemy tracking for end-of-combat XP award display.
    private readonly List<CharacterController> _defeatedEnemiesThisCombat = new List<CharacterController>();

    // D&D timing: 1 in-game day = 14,400 rounds.
    private const int RoundsPerDay = 14400;

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
        SelectingFlamingSphereTarget,
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
    [SerializeField] private EconomyService _economyService;
    [SerializeField] private SummoningService _summoningService;
    [SerializeField] private EncounterService _encounterService;
    [SerializeField] private SpellApplicationService _spellApplicationService;

    /// <summary>Centralized summoning service. Manages summoned creature lifecycle.</summary>
    public SummoningService Summoning => _summoningService;
    /// <summary>Centralized encounter service. Manages combat encounter lifecycle.</summary>
    public EncounterService Encounters => _encounterService;
    /// <summary>Centralized spell application service. Manages spell effect application and queries.</summary>
    public SpellApplicationService SpellApplication => _spellApplicationService;

    private ConfusedBehaviorController _confusedBehaviorController;
    private CharmedBehaviorController _charmedBehaviorController;
    private FascinatedBehaviorController _fascinatedBehaviorController;
    private FrightenedBehaviorController _frightenedBehaviorController;

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
    public enum PendingAttackMode { Single, FullAttack, DualWield, FlurryOfBlows, CastSpell, TemplateSmite }

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
    private ItemData _pendingAnimateRopeItem; // Selected rope material component reserved for the pending Animate Rope cast
    private ItemData _pendingMagicWeaponItem; // Selected weapon to receive the pending Magic Weapon spell
    private ResistEnergyType? _pendingResistEnergyType;
    private ResistEnergyType? _pendingProtectionFromEnergyType;
    private bool? _pendingFireShieldIsWarm; // Chosen Fire Shield type: true=Warm, false=Chill, null=not yet chosen
    private string _pendingDisguiseSelfRace;
    private SummonMonsterOption _pendingSummonSelection; // Selected summon option waiting for placement
    private int _pendingSummonListLevel; // Selected Summon Monster list level (I/II/III...)
    private SummonCreatureCountInfo _pendingSummonCountInfo; // Count formula/range for selected list level
    private string _pendingSummonSwarmNpcId; // Selected swarm type for Summon Swarm placement
    private int _pendingNaturalAttackSequenceIndex = -1; // Sequence index for selected natural-weapon single attack
    private string _pendingNaturalAttackLabel; // Display label for selected natural-weapon single attack

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

    // Withdraw selection state.
    private bool _isSelectingWithdraw;

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

    // Progressive house-rule attack tracking.
    private int _weaponAttacksCommittedThisTurn;
    private readonly HashSet<int> _usedNaturalAttackSequenceIndices = new HashSet<int>();

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
    private Vector2Int _lastLineHoverKey = new Vector2Int(int.MinValue, int.MinValue); // Line endpoint hover key
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

    // ── Emanation tracking (Magic Circles, future: Prayer, Auras, etc.) ──
    /// <summary>All active emanation effects (Magic Circles, future: Prayer, Auras, etc.).</summary>
    private readonly List<EmanationEffectData> _activeEmanations = new List<EmanationEffectData>();

    private class ActiveSummonInstance
    {
        public CharacterController Controller;
        public CharacterController Caster;
        public int RemainingRounds;
        public int TotalDurationRounds;
        public string SourceSpellId;
        public bool IsAlliedToPCs;
        public bool SmiteUsed;
        public SummonCommand CurrentCommand;
        public bool IsConcentrationSummon;
        public bool HasEnteredPostConcentrationDuration;
    }

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

        InitializeDiseaseAndPoisonDatabases();

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

        _economyService ??= gameObject.GetComponent<EconomyService>() ?? gameObject.AddComponent<EconomyService>();
        _economyService.Initialize(this, () => CombatUI, partyGold);
        // Sync: EconomyService now owns the gold state; wire the event back to GameManager's legacy event.
        _economyService.OnGoldChanged += gold => OnGoldChanged?.Invoke(gold);
        // Transfer stash ownership to EconomyService.
        if (PartyStash != null)
            _economyService.PartyStash = PartyStash;
        else
            PartyStash = _economyService.PartyStash;

        // ── New extracted services ──
        _summoningService ??= gameObject.GetComponent<SummoningService>() ?? gameObject.AddComponent<SummoningService>();
        _summoningService.Initialize(this, () => CombatUI);

        _encounterService ??= gameObject.GetComponent<EncounterService>() ?? gameObject.AddComponent<EncounterService>();
        _encounterService.Initialize(this, () => CombatUI);

        _spellApplicationService ??= gameObject.GetComponent<SpellApplicationService>() ?? gameObject.AddComponent<SpellApplicationService>();
        _spellApplicationService.Initialize(this, () => CombatUI, _conditionService);

        _confusedBehaviorController ??= new ConfusedBehaviorController();
        _charmedBehaviorController ??= new CharmedBehaviorController();
        _fascinatedBehaviorController ??= new FascinatedBehaviorController();
        _frightenedBehaviorController ??= new FrightenedBehaviorController();

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

        // Command processor for unified action execution pipeline
        _commandProcessor = gameObject.GetComponent<CommandProcessor>() ?? gameObject.AddComponent<CommandProcessor>();
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
        return c.IsControllable;
    }

    private bool IsEnemyTeam(CharacterController source, CharacterController target)
    {
        if (source == null || target == null) return false;

        return (source.Team == CharacterTeam.Player && target.Team == CharacterTeam.Enemy)
            || (source.Team == CharacterTeam.Enemy && target.Team == CharacterTeam.Player);
    }

    private bool IsAllyTeam(CharacterController source, CharacterController target)
    {
        if (source == null || target == null) return false;
        if (source.Team == CharacterTeam.Neutral || target.Team == CharacterTeam.Neutral) return false;
        return source.Team == target.Team;
    }

    private List<CharacterController> GetTeamMembers(CharacterTeam teamFilter)
    {
        var team = new List<CharacterController>();
        foreach (var c in GetAllCharacters())
        {
            if (c == null || c.Stats == null || c.Stats.IsDead) continue;
            if (c.Team == teamFilter)
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
        ProcessCreationLevelUpsThenPromptEncounterSelection();
    }

    /// <summary>
    /// Called when all 4 characters have been created through the character creation UI.
    /// </summary>
    public void OnCharacterCreationComplete4(CharacterCreationData[] pcDataArray)
    {
        int partyCount = pcDataArray != null ? pcDataArray.Length : 0;
        Debug.Log($"[PlayNow] GameManager received creation complete callback. partyCount={partyCount}");

        if (pcDataArray == null || pcDataArray.Length == 0)
        {
            Debug.LogError("[PlayNow] Character creation completed with no character data. Cannot continue to encounter selection.");
            return;
        }

        WaitingForCharacterCreation = false;
        Debug.Log($"[GameManager] Character creation complete ({pcDataArray.Length} PCs)");
        SetupCreatedCharacters(pcDataArray);
        ProcessCreationLevelUpsThenPromptEncounterSelection();
    }


    private void ProcessCreationLevelUpsThenPromptEncounterSelection()
    {
        UpdateAllStatsUI();

        List<CharacterController> charactersNeedingLevelUp = new List<CharacterController>();
        if (PCs != null)
        {
            for (int i = 0; i < PCs.Count; i++)
            {
                CharacterController pc = PCs[i];
                if (pc == null || pc.Stats == null)
                    continue;

                if (pc.Stats.PendingLevelUps > 0)
                    charactersNeedingLevelUp.Add(pc);
            }
        }

        if (charactersNeedingLevelUp.Count == 0)
        {
            PromptEncounterSelection();
            return;
        }

        Debug.Log($"[GameManager] Processing creation level-up sequence for {charactersNeedingLevelUp.Count} character(s).");
        ShowLevelUpUISequence(charactersNeedingLevelUp, 0, () =>
        {
            UpdateAllStatsUI();
            PromptEncounterSelection();
        });
    }

    private void PromptEncounterSelection()
    {
        Debug.Log("[PlayNow] PromptEncounterSelection invoked.");
        NPCDatabase.Init();
        EnsurePartyStashInitialized();

        if (EncounterSelectionUI == null)
            EncounterSelectionUI = FindObjectOfType<EncounterSelectionUI>();
        if (EncounterSelectionUI == null)
            EncounterSelectionUI = gameObject.AddComponent<EncounterSelectionUI>();

        if (EncounterSelectionUI == null)
        {
            Debug.LogError("[PlayNow] EncounterSelectionUI could not be resolved. Encounter selection cannot be shown.");
            return;
        }

        PreCombatHubUI?.Close();
        StoreUI?.Close();
        SpellPreparationUI?.Close();
        PreCombatInventoryUI?.Close(suppressCallback: true);
        LootCollectionUI?.Close(invokeClosedCallback: false);
        Debug.Log($"[CombatReset] PromptEncounterSelection pre-open cleanup | preCombatUiAssigned={PreCombatInventoryUI != null} | preCombatUiOpen={(PreCombatInventoryUI != null && PreCombatInventoryUI.IsOpen)} | lootUiAssigned={LootCollectionUI != null} | lootUiOpen={(LootCollectionUI != null && LootCollectionUI.IsOpen)} | waitingLoot={WaitingForLootCollection}");
        WaitingForPreCombatInventory = false;
        ResetPostCombatLootCollectionState("PromptEncounterSelection");
        WaitingForEncounterSelection = true;

        var presets = NPCDatabase.ListEncounterPresets();
        int presetCount = presets != null ? presets.Count : 0;
        Debug.Log($"[PlayNow] Opening encounter selection UI. presets={presetCount}, partyCount={(PCs != null ? PCs.Count : 0)}");

        // Show all available encounters so the player can scroll and select any scenario.
        EncounterSelectionUI.Open(presets,
            onSelect: presetId =>
            {
                WaitingForEncounterSelection = false;
                _selectedEncounterPresetId = string.IsNullOrEmpty(presetId) ? "goblin_raiders" : presetId;
                Debug.Log($"[PlayNow] Encounter preset selected: {_selectedEncounterPresetId}");
                ApplyEncounterPreset(_selectedEncounterPresetId);
                OpenPreCombatHubPhase();
            },
            onStartRandomEncounter: (enemyIds, generated) =>
            {
                WaitingForEncounterSelection = false;
                int enemyCount = enemyIds != null ? enemyIds.Count : 0;
                Debug.Log($"[PlayNow] Random encounter selected. enemies={enemyCount}, generated={(generated != null)}");
                ApplyRandomEncounter(enemyIds, generated);
                OpenPreCombatHubPhase();
            },
            onCancel: () =>
            {
                WaitingForEncounterSelection = false;
                _selectedEncounterPresetId = "goblin_raiders";
                Debug.Log("[PlayNow] Encounter selection canceled. Falling back to goblin_raiders preset.");
                ApplyEncounterPreset(_selectedEncounterPresetId);
                OpenPreCombatHubPhase();
            },
            partyAverageLevel: GetCurrentPartyAverageLevel(),
            partyLevels: GetCurrentPartyLevels(),
            partySize: PCs != null ? PCs.Count : 4);
    }

    private void EnsurePartyStashInitialized()
    {
        if (_economyService != null)
        {
            _economyService.EnsurePartyStashInitialized();
            PartyStash = _economyService.PartyStash;
        }
        else
        {
            PartyStash ??= new PartyStash();
            if (!_partyStashSeeded)
            {
                PartyStash.SeedDefaultItemsIfEmpty();
                _partyStashSeeded = true;
            }
        }

        if (CurrentPhase != TurnPhase.PCTurn && CurrentPhase != TurnPhase.NPCTurn)
            PartyStash.Unlock();
    }

    public void RestorePartyAfterCombat()
    {
        EnsurePartyStashInitialized();

        if (AreaEffectManager.HasInstance)
            AreaEffectManager.Instance.ClearAllEffects();
        if (WindEffectManager.HasInstance)
            WindEffectManager.Instance.ClearAllWindEffects();

        // Despawn any lingering summoned creatures from the finished combat.
        for (int i = _activeSummons.Count - 1; i >= 0; i--)
        {
            ActiveSummonInstance activeSummon = _activeSummons[i];
            if (activeSummon?.Controller == null)
                continue;

            Grid?.ClearCreatureOccupancy(activeSummon.Controller);
            Destroy(activeSummon.Controller.gameObject);
        }
        _activeSummons.Clear();
        _summonedAllies.Clear();
        _summonedEnemies.Clear();
        ClearAllMirrorImageEffects("combat reset");
        MeleeReactionService.ClearAll();
        CurseTracker.ClearAll();

        if (PCs == null || PCs.Count == 0)
            return;

        Vector2Int[] defaultRestPositions =
        {
            new Vector2Int(3, 6),
            new Vector2Int(3, 9),
            new Vector2Int(3, 12),
            new Vector2Int(3, 15)
        };

        if (Grid != null)
        {
            for (int i = 0; i < PCs.Count; i++)
            {
                CharacterController pc = PCs[i];
                if (pc != null)
                    Grid.ClearCreatureOccupancy(pc);
            }
        }

        for (int i = 0; i < PCs.Count; i++)
        {
            CharacterController pc = PCs[i];
            if (pc == null || pc.Stats == null || pc.gameObject == null || !pc.gameObject.activeInHierarchy)
                continue;

            CharacterStats stats = pc.Stats;

            if (stats.IsRaging)
                stats.DeactivateRage();

            StatusEffectManager statusMgr = pc.GetComponent<StatusEffectManager>();
            statusMgr?.RemoveAllEffects();

            SpellcastingComponent spellComp = pc.GetComponent<SpellcastingComponent>();
            if (spellComp != null)
            {
                spellComp.ClearHeldTouchCharge("full rest");
                spellComp.RestoreAllSlots();
                spellComp.ActiveBuffs?.Clear();
                spellComp.MageArmorActive = false;
                spellComp.MageArmorACBonus = 0;
            }

            RemoveAllConditions(pc);
            pc.ClearAllConditions();

            pc.ClearDisguiseSelfEffect();
            pc.ClearExpeditiousRetreatEffect();
            pc.ClearInvisibilityEffect();
            pc.ClearSeeInvisibilityEffect();
            pc.ClearGlitterdustEffect();
            pc.ClearMelfsAcidArrowEffect();
            pc.ClearEnfeeblementEffects();
            pc.ClearTouchOfIdiocyEffect();
            stats.ResetCurrentSizeToBase();
            pc.UpdateVisualSize(false);

            stats.ActiveResistEnergyEffects?.Clear();
            stats.ActiveProtectionFromEnergyEffects?.Clear();
            stats.ActiveProtectionFromArrowsEffect = null;
            stats.ActiveStoneskinEffect = null;
            stats.ActiveDimensionalAnchorEffect = null;
            stats.TemplateSmiteUsed = false;
            stats.RagesUsedToday = 0;
            stats.TurnUndeadAttemptsUsedToday = 0;
            // Reset domain power daily uses
            stats.StrengthDomainUsesToday = 0;
            stats.DestructionDomainUsesToday = 0;
            stats.DeathDomainUsesToday = 0;
            stats.SunDomainUsesToday = 0;
            stats.TravelDomainUsesToday = 0;
            stats.DestructionSmiteActive = false;
            stats.StrengthDomainBonusRounds = 0;
            stats.TravelDomainFreedomRounds = 0;
            stats.GreaterTurningActive = false;
            stats.TemporarySTRBonus = 0;
            stats.IsFatigued = false;

            if (pc.ActivePoisons != null)
                pc.ActivePoisons.Clear();

            Inventory inventory = pc.GetComponent<InventoryComponent>()?.CharacterInventory;
            if (inventory != null)
            {
                ClearTemporaryItemSpellEffects(inventory.RightHandSlot);
                ClearTemporaryItemSpellEffects(inventory.LeftHandSlot);
                ClearTemporaryItemSpellEffects(inventory.HandsSlot);
                ClearTemporaryItemSpellEffects(inventory.HeadSlot);
                ClearTemporaryItemSpellEffects(inventory.FaceEyesSlot);
                ClearTemporaryItemSpellEffects(inventory.NeckSlot);
                ClearTemporaryItemSpellEffects(inventory.TorsoSlot);
                ClearTemporaryItemSpellEffects(inventory.ArmorRobeSlot);
                ClearTemporaryItemSpellEffects(inventory.WaistSlot);
                ClearTemporaryItemSpellEffects(inventory.BackSlot);
                ClearTemporaryItemSpellEffects(inventory.WristsSlot);
                ClearTemporaryItemSpellEffects(inventory.LeftRingSlot);
                ClearTemporaryItemSpellEffects(inventory.RightRingSlot);
                ClearTemporaryItemSpellEffects(inventory.FeetSlot);

                if (inventory.GeneralSlots != null)
                {
                    for (int slotIndex = 0; slotIndex < inventory.GeneralSlots.Length; slotIndex++)
                        ClearTemporaryItemSpellEffects(inventory.GeneralSlots[slotIndex]);
                }

                inventory.RecalculateStats();
            }

            stats.ClearNonlethalDamage();
            stats.TempHP = 0;
            stats.BonusMaxHP = 0;
            stats.CurrentHP = stats.TotalMaxHP;
            pc.SyncHPStateFromCurrentHP(emitLog: false);

            if (Grid != null && i < defaultRestPositions.Length)
            {
                SquareCell restCell = Grid.GetCell(defaultRestPositions[i]);
                if (restCell != null)
                    pc.MoveToCell(restCell, markAsMoved: false);
            }

            pc.StartNewTurn();
        }

        CombatUI?.SetTurnIndicator("Party Rested and Restored!");
        CombatUI?.ShowCombatLog("✅ Party Rested and Restored!");
        CombatUI?.ShowCombatLog("💖 HP and abilities fully recovered.");
        CombatUI?.ShowCombatLog("⚔ Ready for next encounter.");

        UpdateAllStatsUI();
    }

    private void ClearBattlefieldForEncounterLoopReset(string context)
    {
        string safeContext = string.IsNullOrWhiteSpace(context) ? "unspecified" : context;
        Debug.Log($"[BattlefieldReset] Clearing battlefield | context={safeContext} | phase={CurrentPhase}");

        if (_pathPreview != null)
            _pathPreview.HidePath();
        if (_hoverMarker != null)
            _hoverMarker.Hide();

        ClearAoEPreviewHighlights();
        _isAoETargeting = false;
        _currentAoECells = null;
        _lastAoEHoverPos = new Vector2Int(-1, -1);
        _lastLineHoverKey = new Vector2Int(int.MinValue, int.MinValue);
        _lastConeHoverKey = new Vector2Int(int.MinValue, int.MinValue);

        if (Grid != null)
            Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        ClearAllActiveGreaseEffects();

        if (AreaEffectManager.HasInstance)
            AreaEffectManager.Instance.ClearAllEffects();
        if (WindEffectManager.HasInstance)
            WindEffectManager.Instance.ClearAllWindEffects();

        Debug.Log($"[BattlefieldReset] Battlefield cleared | context={safeContext}");
    }

    private void ResetCombatStateForNextEncounter(string context)
    {
        string safeContext = string.IsNullOrWhiteSpace(context) ? "unspecified" : context;
        int turnOrderCount = _turnService != null && _turnService.InitiativeOrder != null ? _turnService.InitiativeOrder.Count : 0;
        int activeNpcs = 0;
        if (NPCs != null)
        {
            for (int i = 0; i < NPCs.Count; i++)
            {
                CharacterController npc = NPCs[i];
                if (npc != null && npc.gameObject != null && npc.gameObject.activeSelf)
                    activeNpcs++;
            }
        }

        string currentCharacterName = CurrentCharacter != null && CurrentCharacter.Stats != null
            ? CurrentCharacter.Stats.CharacterName
            : "None";

        Debug.Log($"[CombatReset] ENTER | context={safeContext} | phase={CurrentPhase} | subPhase={CurrentSubPhase} | turnOrder={turnOrderCount} | currentCharacter={currentCharacterName} | activeNPCs={activeNpcs}");

        StopAllCoroutines();
        _turnService?.StopAllCoroutines();

        EndAttackSequence();
        EndThrownAttackSequence();
        ResetOffHandTurnState();
        ClearDisarmSequenceState();
        ClearSunderSequenceState();

        _waitingForAoOConfirmation = false;
        _pendingAoOAction = null;
        _spellcastProvocationCancelled = false;
        ClearSpellcastResourceSnapshot();

        _isSelectingWithdraw = false;
        _isSelectingBreakWallTarget = false;
        _isSelectingSpecialAttack = false;
        _pendingDisarmUseOffHandSelection = false;
        _pendingSunderUseOffHandSelection = false;

        _pendingSpell = null;
        _pendingMetamagic = null;
        _pendingSpellFromHeldCharge = false;
        _pendingAnimateRopeItem = null;
        _pendingMagicWeaponItem = null;
        _pendingKeenEdgeItem = null;
        _pendingKeenEdgeIsAmmo = false;
        _pendingGreaterMagicWeaponItem = null;
        _pendingResistEnergyType = null;
        _pendingProtectionFromEnergyType = null;
        _pendingFireShieldIsWarm = null;
        _pendingDisguiseSelfRace = null;
        _pendingSummonSelection = null;
        _pendingSummonListLevel = 0;
        _pendingSummonCountInfo = null;
        _pendingSummonSwarmNpcId = null;
        _pendingNaturalAttackSequenceIndex = -1;
        _pendingNaturalAttackLabel = null;
        ResetPendingGreaseCastMode();

        _isConfirmingSelfAoE = false;
        _pendingSelfAoECells = null;
        _pendingSelfAoETargets = null;

        _chargeTarget = null;
        _pendingChargePath.Clear();
        _pendingChargeBullRush = false;

        _isAwaitingRangedRetargetSelection = false;
        _rangedRetargetSelectionCancelled = false;
        _selectedRangedRetarget = null;
        _isAwaitingFullAttackFiveFootStepSelection = false;
        _fullAttackFiveFootStepSelectionCancelled = false;
        _fullAttackFiveFootStepWasTaken = false;
        _fullAttackFiveFootStepRequireReachableTarget = false;
        _fullAttackFiveFootStepRangedMode = false;

        _isSelectingTurnUndead = false;
        _turnUndeadPendingInvoker = null;
        CloseTurnUndeadSelectionPanel(clearHighlights: true);
        _activeTurnUndeadSelectionContext = null;

        ClearOverrunDestinationSelectionState();
        ClearOverrunContinuationState();
        ClearFreeAdjacentGrappleMoveSelectionState();
        ClearGrappleMoveSelectionState();

        ClearBattlefieldForEncounterLoopReset(safeContext);

        if (Grid != null)
        {
            if (PCs != null)
            {
                for (int i = 0; i < PCs.Count; i++)
                {
                    CharacterController pc = PCs[i];
                    if (pc == null)
                        continue;

                    Grid.ClearCreatureOccupancy(pc);
                    if (pc.gameObject != null && pc.gameObject.activeInHierarchy)
                        Grid.SetCreatureOccupancy(pc, pc.GridPosition, pc.GetVisualSquaresOccupied());
                }
            }

            if (NPCs != null)
            {
                for (int i = 0; i < NPCs.Count; i++)
                {
                    CharacterController npc = NPCs[i];
                    if (npc == null)
                        continue;

                    Grid.ClearCreatureOccupancy(npc);
                    npc.GridPosition = new Vector2Int(-1000, -1000);
                    if (npc.transform != null)
                        npc.transform.position = new Vector3(-1000f, -1000f, 0f);
                    if (npc.gameObject != null)
                        npc.gameObject.SetActive(false);
                }
            }
        }

        _previewThreatenedSquares?.Clear();
        InvalidatePreviewThreats();

        _turnService?.ForceResetWithoutCallbacks($"ResetCombatStateForNextEncounter:{safeContext}");

        WaitingForPreCombatInventory = false;
        WaitingForLootCollection = false;
        WaitingForEncounterSelection = false;
        _postCombatLootCollectionTriggered = false;
        CurrentPhase = TurnPhase.CombatOver;
        CurrentSubPhase = PlayerSubPhase.ChoosingAction;

        CombatUI?.ResetAllUI(clearCombatLog: true);

        int turnOrderAfter = _turnService != null && _turnService.InitiativeOrder != null ? _turnService.InitiativeOrder.Count : 0;
        Debug.Log($"[CombatReset] EXIT | context={safeContext} | phase={CurrentPhase} | subPhase={CurrentSubPhase} | turnOrder={turnOrderAfter}");
    }

    private static void ClearTemporaryItemSpellEffects(ItemData item)
    {
        if (item == null || item.ActiveSpellEffects == null || item.ActiveSpellEffects.Count == 0)
            return;

        item.ActiveSpellEffects.Clear();
    }

    public void RegisterCombatLoopCompletion(int lootedCount)
    {
        CompletedCombatCount++;
        TotalLootItemsCollected += Mathf.Max(0, lootedCount);

        int encounterXp = 0;
        if (NPCs != null)
        {
            for (int i = 0; i < NPCs.Count; i++)
            {
                CharacterController npc = NPCs[i];
                if (npc == null || npc.Stats == null)
                    continue;

                if (!npc.Stats.IsDead)
                    continue;

                if (ChallengeRatingUtils.TryParse(npc.Stats.ChallengeRating, out float cr))
                    encounterXp += ChallengeRatingUtils.GetXpForCr(cr);
            }
        }

        TotalEncounterXPDefeated += Mathf.Max(0, encounterXp);
    }

    public void ReturnToEncounterSelection()
    {
        EnsurePartyStashInitialized();
        PartyStash?.Unlock();

        Debug.Log($"[CombatReset] ReturnToEncounterSelection PRE reset | waitingLoot={WaitingForLootCollection} | lootTriggered={_postCombatLootCollectionTriggered} | lootUiAssigned={LootCollectionUI != null} | lootUiOpen={(LootCollectionUI != null && LootCollectionUI.IsOpen)}");
        ResetCombatStateForNextEncounter("ReturnToEncounterSelection");
        Debug.Log($"[CombatReset] ReturnToEncounterSelection POST reset | waitingLoot={WaitingForLootCollection} | lootTriggered={_postCombatLootCollectionTriggered} | phase={CurrentPhase} | subPhase={CurrentSubPhase}");

        WaitingForLootCollection = false;
        WaitingForPreCombatInventory = false;
        WaitingForEncounterSelection = false;
        CurrentPhase = TurnPhase.CombatOver;

        CombatUI?.ShowCombatLog($"📊 Combat Loop Stats — Fights: {CompletedCombatCount} | Loot Items: {TotalLootItemsCollected} | XP Defeated: {TotalEncounterXPDefeated}");
        PromptEncounterSelection();
    }

    public void ExitCombatLoopToMenu()
    {
        WaitingForLootCollection = false;
        WaitingForPreCombatInventory = false;
        WaitingForEncounterSelection = false;

        EnsurePartyStashInitialized();
        PartyStash?.Unlock();
        CombatUI?.ShowCombatLog("🛑 Combat loop exited.");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private List<CharacterController> GetActivePartyMembersForPreCombat()
    {
        List<CharacterController> partyMembers = new List<CharacterController>();
        if (PCs == null)
            return partyMembers;

        for (int i = 0; i < PCs.Count; i++)
        {
            CharacterController pc = PCs[i];
            if (pc != null && pc.gameObject != null && pc.gameObject.activeInHierarchy && pc.Stats != null && !pc.Stats.IsDead)
                partyMembers.Add(pc);
        }

        return partyMembers;
    }

    private bool IsPreparedCaster(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return false;

        CharacterStats stats = character.Stats;
        bool isPreparedCasterClass =
            stats.IsWizard ||
            stats.IsCleric ||
            stats.HasClass("Druid") ||
            stats.HasClass("Bard") ||
            stats.HasClass("Paladin") ||
            stats.HasClass("Ranger");

        if (!isPreparedCasterClass)
            return false;

        SpellcastingComponent spellComp = character.GetComponent<SpellcastingComponent>();
        return spellComp != null && spellComp.SpellSlots != null && spellComp.SpellSlots.Count > 0;
    }

    private bool HasAnyPreparedSpell(CharacterController character)
    {
        if (character == null)
            return false;

        SpellcastingComponent spellComp = character.GetComponent<SpellcastingComponent>();
        if (spellComp == null || spellComp.SpellSlots == null)
            return false;

        return spellComp.GetPreparedCasterClassNames().Any(className => spellComp.HasPreparedSpellForClass(className));
    }

    private List<CharacterController> GetUnpreparedPreparedCasters(List<CharacterController> partyMembers)
    {
        List<CharacterController> unprepared = new List<CharacterController>();
        if (partyMembers == null)
            return unprepared;

        for (int i = 0; i < partyMembers.Count; i++)
        {
            CharacterController member = partyMembers[i];
            if (!IsPreparedCaster(member))
                continue;

            SpellcastingComponent spellComp = member.GetComponent<SpellcastingComponent>();
            if (spellComp == null)
                continue;

            if (spellComp.GetUnpreparedCasterClassNames().Count > 0)
                unprepared.Add(member);
        }

        return unprepared;
    }

    private List<string> BuildSpellPreparationStatusLines(List<CharacterController> partyMembers)
    {
        List<string> lines = new List<string>();
        if (partyMembers == null)
            return lines;

        for (int i = 0; i < partyMembers.Count; i++)
        {
            CharacterController member = partyMembers[i];
            if (!IsPreparedCaster(member))
                continue;

            SpellcastingComponent spellComp = member.GetComponent<SpellcastingComponent>();
            if (spellComp == null)
                continue;

            string name = member != null && member.Stats != null ? member.Stats.CharacterName : "Unknown";
            List<string> missingClasses = spellComp.GetUnpreparedCasterClassNames();
            if (missingClasses.Count == 0)
            {
                lines.Add($"✓ {name}: Prepared");
            }
            else
            {
                lines.Add($"⚠️ {name}: Missing {string.Join(", ", missingClasses)} prep");
            }
        }

        return lines;
    }

    private StoreInventory EnsureStoreInventoryInitialized()
    {
        if (_economyService != null)
            return _economyService.EnsureStoreInventoryInitialized();

        StoreInventory storeInventory = StoreInventory.Instance;
        if (storeInventory == null)
            storeInventory = gameObject.GetComponent<StoreInventory>() ?? gameObject.AddComponent<StoreInventory>();

        return storeInventory;
    }

    private void OpenPreCombatHubPhase()
    {
        EnsurePartyStashInitialized();
        EnsureStoreInventoryInitialized();

        if (PreCombatHubUI == null)
            PreCombatHubUI = FindObjectOfType<PreCombatHubUI>();
        if (PreCombatHubUI == null)
            PreCombatHubUI = gameObject.AddComponent<PreCombatHubUI>();

        List<CharacterController> partyMembers = GetActivePartyMembersForPreCombat();
        PartyStash.Unlock();
        WaitingForPreCombatInventory = true;

        Debug.Log($"[PreCombatHub] Opening pre-combat hub for encounter '{_selectedEncounterPresetId}'. partyMembers={partyMembers.Count}");
        Debug.Log($"[Store] Store opened with {StoreInventory.Instance.GetItemsByCategory("All").Count} items");
        Debug.Log($"[Store] Party has {PartyGold} gp");

        PreCombatHubUI.Open(
            onOpenStore: () => OpenStoreFromPreCombat(partyMembers),
            onOpenInventory: () => OpenInventoryFromPreCombat(partyMembers),
            onOpenSpellPreparation: () => OpenSpellPreparationFromPreCombat(partyMembers),
            onStartEncounter: () => StartEncounterFromPreCombat("Hub.StartEncounter"),
            onBackToEncounterSelection: () =>
            {
                WaitingForPreCombatInventory = false;
                PartyStash.Unlock();
                PreCombatHubUI?.Close();
                PromptEncounterSelection();
            },
            spellcasterStatusLines: BuildSpellPreparationStatusLines(partyMembers));
    }

    private void OpenInventoryFromPreCombat(List<CharacterController> partyMembers)
    {
        EnsurePartyStashInitialized();

        if (PreCombatInventoryUI == null)
            PreCombatInventoryUI = FindObjectOfType<PreCombatInventoryUI>();
        if (PreCombatInventoryUI == null)
            PreCombatInventoryUI = gameObject.AddComponent<PreCombatInventoryUI>();

        PreCombatHubUI?.HideMenu();
        PartyStash.Unlock();

        PreCombatInventoryUI.Open(
            PartyStash,
            partyMembers,
            onBeginCombat: () => StartEncounterFromPreCombat("Stash.BeginCombat"),
            onBack: () => ReturnToPreCombatHubFromSubWindow("Stash.Back"));
    }

    private void OpenStoreFromPreCombat(List<CharacterController> partyMembers)
    {
        EnsurePartyStashInitialized();
        EnsureStoreInventoryInitialized();

        StoreUI storeUI = StoreUI;
        if (storeUI == null)
            storeUI = FindObjectOfType<StoreUI>();
        if (storeUI == null)
            storeUI = gameObject.AddComponent<StoreUI>();

        StoreUI = storeUI;
        PreCombatHubUI?.HideMenu();

        storeUI.ShowStore(
            PartyStash,
            partyMembers,
            onBackToMenu: () => ReturnToPreCombatHubFromSubWindow("Store.Back"),
            onStartEncounter: () => StartEncounterFromPreCombat("Store.StartEncounter"));
    }

    private void OpenSpellPreparationFromPreCombat(List<CharacterController> partyMembers)
    {
        PreCombatHubUI?.HideMenu();

        ShowSpellPreparation(
            partyMembers,
            onComplete: () =>
            {
                CombatUI?.ShowCombatLog("🔮 Spell preparation complete. Returning to pre-combat menu.");
                ReturnToPreCombatHubFromSubWindow("SpellPrep.Done");
            },
            onBackToMenu: () => ReturnToPreCombatHubFromSubWindow("SpellPrep.Back"),
            onStartEncounter: () => StartEncounterFromPreCombat("SpellPrep.StartEncounter"));
    }

    private void ReturnToPreCombatHubFromSubWindow(string source)
    {
        Debug.Log($"[PreCombatHub] Returning from sub-window: {source}");
        EnsurePartyStashInitialized();
        PartyStash.Unlock();
        WaitingForPreCombatInventory = true;

        List<CharacterController> partyMembers = GetActivePartyMembersForPreCombat();
        PreCombatHubUI?.UpdateSpellPreparationStatus(BuildSpellPreparationStatusLines(partyMembers));
        PreCombatHubUI?.ShowMenu();
    }

    private void StartEncounterFromPreCombat(string source)
    {
        Debug.Log($"[PreCombatHub] Start encounter requested from {source}");

        List<CharacterController> partyMembers = GetActivePartyMembersForPreCombat();
        List<CharacterController> unpreparedCasters = GetUnpreparedPreparedCasters(partyMembers);

        if (unpreparedCasters.Count > 0)
        {
            List<string> warnings = new List<string>();
            for (int i = 0; i < unpreparedCasters.Count; i++)
            {
                CharacterController caster = unpreparedCasters[i];
                string name = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Unknown";
                SpellcastingComponent spellComp = caster != null ? caster.GetComponent<SpellcastingComponent>() : null;
                List<string> missingClasses = spellComp != null ? spellComp.GetUnpreparedCasterClassNames() : new List<string>();

                if (missingClasses.Count == 0)
                    warnings.Add($"{name}: spells not prepared");
                else
                    warnings.Add($"{name}: {string.Join(", ", missingClasses)} spells not prepared");
            }

            string message = "Warning:\n" + string.Join("\n", warnings) + "\n\nUnprepared classes cannot cast spells in combat. Prepare spells now?";

            if (CombatUI != null)
            {
                CombatUI.ShowConfirmationDialog(
                    title: "Unprepared Spellcasters",
                    message: message,
                    confirmLabel: "Prepare Spells",
                    cancelLabel: "Fight Anyway",
                    onConfirm: () =>
                    {
                        if (StoreUI != null && StoreUI.IsOpen)
                            StoreUI.Close();
                        if (PreCombatInventoryUI != null && PreCombatInventoryUI.IsOpen)
                            PreCombatInventoryUI.Close(suppressCallback: true);

                        OpenSpellPreparationFromPreCombat(unpreparedCasters);
                    },
                    onCancel: () => ForceStartEncounterFromPreCombat(source + ".FightAnyway"));
                return;
            }

            Debug.LogWarning("[PreCombatHub] CombatUI unavailable for unprepared warning. Proceeding to combat.");
        }

        ForceStartEncounterFromPreCombat(source);
    }

    private void ForceStartEncounterFromPreCombat(string source)
    {
        Debug.Log($"[PreCombatHub] Forcing encounter start from {source}");
        WaitingForPreCombatInventory = false;

        PreCombatHubUI?.Close();
        if (StoreUI != null && StoreUI.IsOpen)
            StoreUI.Close();
        if (PreCombatInventoryUI != null && PreCombatInventoryUI.IsOpen)
            PreCombatInventoryUI.Close(suppressCallback: true);
        if (SpellPreparationUI != null && SpellPreparationUI.IsOpen)
            SpellPreparationUI.Close();

        PartyStash.Lock();
        StartCombat();
    }

    private void ShowSpellPreparation(
        List<CharacterController> party,
        System.Action onComplete,
        System.Action onBackToMenu = null,
        System.Action onStartEncounter = null)
    {
        SpellPreparationUI spellPrepUI = SpellPreparationUI;
        if (spellPrepUI == null)
            spellPrepUI = FindObjectOfType<SpellPreparationUI>();

        if (spellPrepUI == null)
        {
            Debug.Log("[SpellPrep] SpellPreparationUI not found in scene. Creating runtime instance.");
            GameObject uiObj = new GameObject("SpellPreparationUI");
            spellPrepUI = uiObj.AddComponent<SpellPreparationUI>();

            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                uiObj.transform.SetParent(canvas.transform, false);
                spellPrepUI.BuildUI(canvas);
            }
            else
            {
                Debug.LogWarning("[SpellPrep] No Canvas found for spell prep UI. Skipping preparation step.");
                onComplete?.Invoke();
                return;
            }
        }

        SpellPreparationUI = spellPrepUI;
        spellPrepUI.Show(party, onComplete, onBackToMenu, onStartEncounter);
    }

    private int GetCurrentPartyAverageLevel()
    {
        if (PCs == null || PCs.Count == 0)
            return 3;

        int total = 0;
        int count = 0;
        for (int i = 0; i < PCs.Count; i++)
        {
            CharacterController pc = PCs[i];
            if (pc == null || pc.Stats == null)
                continue;

            total += Mathf.Max(1, pc.Stats.Level);
            count++;
        }

        if (count == 0)
            return 3;

        return Mathf.Max(1, Mathf.RoundToInt((float)total / count));
    }

    private List<int> GetCurrentPartyLevels()
    {
        List<int> levels = new List<int>();
        if (PCs == null)
            return levels;

        for (int i = 0; i < PCs.Count; i++)
        {
            CharacterController pc = PCs[i];
            if (pc == null || pc.Stats == null)
                continue;

            levels.Add(Mathf.Max(1, pc.Stats.Level));
        }

        return levels;
    }

    private void ApplyRandomEncounter(List<string> enemyIds, GeneratedRandomEncounter generated)
    {
        _selectedEncounterPresetId = "random_encounter";
        _activeEncounterEnemyIds.Clear();

        if (enemyIds != null)
        {
            for (int i = 0; i < enemyIds.Count; i++)
            {
                string id = enemyIds[i];
                if (!string.IsNullOrWhiteSpace(id))
                    _activeEncounterEnemyIds.Add(id);
            }
        }

        if (_activeEncounterEnemyIds.Count == 0)
        {
            _activeEncounterEnemyIds.Add("goblin_warchief");
            _activeEncounterEnemyIds.Add("hobgoblin_sergeant");
            _activeEncounterEnemyIds.Add("skeleton_archer");
        }

        _isGrappleTestEncounter = false;
        _isGreaseTestEncounter = false;
        _isFeintSneakTestEncounter = false;
        _isTurnUndeadTestEncounter = false;
        _isArmorTargetingTestEncounter = false;
        _isTigerHuntTestEncounter = false;
        _isOgreBattleTestEncounter = false;
        _isShieldBashTestEncounter = false;
        _isCelestialTemplateTestEncounter = false;
        _isFiendishTemplateTestEncounter = false;
        _isSummonMonsterTestEncounter = false;
        _isNpcMagicMissileTestEncounter = false;
        _isProtectionFromEvilTestEncounter = false;
        _isWindDispersionTestEncounter = false;
        _isObscuringMistRangedOnlyTestEncounter = false;
        _isDisruptUndeadTestEncounter = false;
        _isTrueStrikeTestEncounter = false;
        _isWizardSpellTestEncounter = false;
        _isClericSpellTestEncounter = false;
        _isCharmPersonTestEncounter = false;
        _isSleepSpellTestEncounter = false;
        _isMirrorImageTestEncounter = false;

        RestoreStandardPartyLayout();
        SetupEnemyEncounter(_activeEncounterEnemyIds);
        SetupNPCIcons();
        UpdateAllStatsUI();

        if (generated != null)
            CombatUI?.ShowCombatLog($"🎲 Random encounter loaded: {generated.BuildHeaderLine()} • XP {generated.TotalXP}");
        else
            CombatUI?.ShowCombatLog("🎲 Random encounter loaded.");
    }

    private void ApplyEncounterPreset(string presetId)
    {
        EncounterPreset preset = NPCDatabase.GetEncounterPreset(presetId);
        _activeEncounterEnemyIds.Clear();
        _isGrappleTestEncounter = string.Equals(presetId, GrappleTestPresetId, StringComparison.Ordinal);
        _isGreaseTestEncounter = string.Equals(presetId, GreaseTestPresetId, StringComparison.Ordinal);
        _isFeintSneakTestEncounter = string.Equals(presetId, FeintSneakTestPresetId, StringComparison.Ordinal);
        _isTurnUndeadTestEncounter = string.Equals(presetId, TurnUndeadTestPresetId, StringComparison.Ordinal);
        _isArmorTargetingTestEncounter = string.Equals(presetId, ArmorTargetingTestPresetId, StringComparison.Ordinal);
        _isTigerHuntTestEncounter = string.Equals(presetId, TigerHuntTestPresetId, StringComparison.Ordinal);
        _isOgreBattleTestEncounter = string.Equals(presetId, OgreBattleTestPresetId, StringComparison.Ordinal);
        _isShieldBashTestEncounter = string.Equals(presetId, ShieldBashTestPresetId, StringComparison.Ordinal);
        _isCelestialTemplateTestEncounter = string.Equals(presetId, CelestialTemplateTestPresetId, StringComparison.Ordinal);
        _isFiendishTemplateTestEncounter = string.Equals(presetId, FiendishTemplateTestPresetId, StringComparison.Ordinal);
        _isSummonMonsterTestEncounter = string.Equals(presetId, SummonMonsterTestPresetId, StringComparison.Ordinal);
        _isNpcMagicMissileTestEncounter = string.Equals(presetId, NPCMagicMissileTestPresetId, StringComparison.Ordinal);
        _isProtectionFromEvilTestEncounter = string.Equals(presetId, ProtectionFromEvilTestPresetId, StringComparison.Ordinal);
        _isWindDispersionTestEncounter = string.Equals(presetId, WindDispersionTestPresetId, StringComparison.Ordinal);
        _isObscuringMistRangedOnlyTestEncounter = string.Equals(presetId, ObscuringMistRangedOnlyTestPresetId, StringComparison.Ordinal);
        _isDisruptUndeadTestEncounter = string.Equals(presetId, DisruptUndeadTestPresetId, StringComparison.Ordinal);
        _isTrueStrikeTestEncounter = string.Equals(presetId, TrueStrikeTestPresetId, StringComparison.Ordinal);
        _isWizardSpellTestEncounter = string.Equals(presetId, WizardSpellTestPresetId, StringComparison.Ordinal);
        _isClericSpellTestEncounter = string.Equals(presetId, ClericSpellTestPresetId, StringComparison.Ordinal);
        _isCharmPersonTestEncounter = string.Equals(presetId, CharmPersonTestPresetId, StringComparison.Ordinal);
        _isSleepSpellTestEncounter = string.Equals(presetId, SleepSpellTestPresetId, StringComparison.Ordinal);
        _isMirrorImageTestEncounter = string.Equals(presetId, MirrorImageTestPresetId, StringComparison.Ordinal);

        if (preset != null && preset.NPCIds != null && preset.NPCIds.Count > 0)
        {
            _activeEncounterEnemyIds.AddRange(preset.NPCIds);
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
        else if (_isGreaseTestEncounter)
            ConfigureGreaseTestParty();
        else if (_isFeintSneakTestEncounter)
            ConfigureFeintSneakTestParty();
        else if (_isTurnUndeadTestEncounter)
            ConfigureTurnUndeadTestParty();
        else if (_isArmorTargetingTestEncounter)
            ConfigureArmorTargetingTestParty();
        else if (_isTigerHuntTestEncounter)
            ConfigureTigerHuntTestParty();
        else if (_isOgreBattleTestEncounter)
            ConfigureOgreBattleTestParty();
        else if (_isShieldBashTestEncounter)
            ConfigureShieldBashTestParty();
        else if (_isCelestialTemplateTestEncounter)
            ConfigureCelestialTemplateTestParty();
        else if (_isFiendishTemplateTestEncounter)
            ConfigureFiendishTemplateTestParty();
        else if (_isSummonMonsterTestEncounter)
            ConfigureSummonMonsterTestParty();
        else if (_isNpcMagicMissileTestEncounter)
            ConfigureNpcMagicMissileTestParty();
        else if (_isProtectionFromEvilTestEncounter)
            ConfigureProtectionFromEvilTestParty();
        else if (_isWindDispersionTestEncounter)
            ConfigureWindDispersionTestParty();
        else if (_isObscuringMistRangedOnlyTestEncounter)
            ConfigureObscuringMistRangedOnlyTestParty();
        else if (_isDisruptUndeadTestEncounter)
            ConfigureDisruptUndeadTestParty();
        else if (_isTrueStrikeTestEncounter)
            ConfigureTrueStrikeTestParty();
        else if (_isWizardSpellTestEncounter)
            ConfigureWizardSpellTestParty();
        else if (_isClericSpellTestEncounter)
            ConfigureClericSpellTestParty();
        else if (_isCharmPersonTestEncounter)
            ConfigureCharmPersonTestParty();
        else if (_isSleepSpellTestEncounter)
            ConfigureSleepSpellTestParty();
        else if (_isMirrorImageTestEncounter)
            ConfigureMirrorImageTestParty();
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

            bool hasCustomRolledStats = data != null
                && data.RolledStats != null
                && Array.Exists(data.RolledStats, rolled => rolled > 0);

            int baseCreationLevel = Mathf.Max(1, data.CharacterLevel);
            int targetLevel = Mathf.Max(baseCreationLevel, data.TargetLevel);

            if (hasCustomRolledStats)
            {
                // Custom creation should always build at level 1 first, then use pending level-ups.
                baseCreationLevel = 1;
                targetLevel = Mathf.Max(baseCreationLevel, data.TargetLevel);
                data.CharacterLevel = baseCreationLevel;
                data.TargetLevel = targetLevel;
            }

            data.ComputeFinalStats();

            Debug.Log($"[GameManager][CreationFlow] {data.CharacterName}: customRolled={hasCustomRolledStats}, baseLevel={baseCreationLevel}, targetLevel={targetLevel}, pendingToQueue={Mathf.Max(0, targetLevel - baseCreationLevel)}");

            int armorBonus, shieldBonus, damageDice;
            GetClassDefaults(data.ClassName, out armorBonus, out shieldBonus, out damageDice);

            CharacterStats stats = new CharacterStats(
                name: data.CharacterName,
                level: baseCreationLevel,
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

            // Set deity and domains from character creation data.
            // D&D 3.5e: all characters can only worship a deity within one step of their alignment.
            DeityDatabase.Init();
            string chosenDeityId = data.ChosenDeityId ?? "";
            if (!string.IsNullOrEmpty(chosenDeityId))
            {
                DeityData chosenDeity = DeityDatabase.GetDeity(chosenDeityId);
                bool deityCompatible = chosenDeity != null && chosenDeity.IsAlignmentCompatible(stats.CharacterAlignment);
                if (!deityCompatible)
                {
                    Debug.LogWarning($"[GameManager][CreationFlow] Removing incompatible deity '{chosenDeityId}' for {data.CharacterName} ({stats.CharacterAlignment}).");
                    chosenDeityId = "";
                }
            }

            stats.DeityId = chosenDeityId;
            stats.ChosenDomains = !string.IsNullOrEmpty(chosenDeityId) && data.ChosenDomains != null
                ? new System.Collections.Generic.List<string>(data.ChosenDomains)
                : new System.Collections.Generic.List<string>();

            // Set spontaneous casting type for clerics
            stats.SpontaneousCasting = data.SpontaneousCasting;

            // Wizard specialization/familiar choices (wizard level 1 feature) from creation.
            if (data.WizardSpecialization != null)
            {
                stats.WizardSpecialization = data.WizardSpecialization;
                stats.WizardSpecialization.Normalize();
            }
            else
            {
                stats.WizardSpecialization = WizardSpecialization.CreateGeneralist();
            }

            stats.ApplyWizardFamiliar(data.WizardFamiliar ?? WizardFamiliar.CreateNone());

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
            stats.InitializeSkills(data.ClassName, baseCreationLevel);
            if (data.SkillRanks != null)
            {
                foreach (var kvp in data.SkillRanks)
                {
                    for (int r = 0; r < kvp.Value; r++)
                        stats.AddSkillRank(kvp.Key);
                }
            }

            // Preserve any unspent skill points from character creation into the class pool
            // so they carry over and are available during the first level-up.
            // Use the explicit UnspentSkillPoints from creation data if available, otherwise
            // fall back to the remaining AvailableSkillPoints after rank allocation.
            int unspentFromCreation = data.UnspentSkillPoints > 0 ? data.UnspentSkillPoints : Mathf.Max(0, stats.AvailableSkillPoints);
            if (unspentFromCreation > 0)
            {
                stats.EnsureClassSkillPointPoolsInitialized();
                int existingPool = stats.GetClassSkillPointPool(data.ClassName);
                stats.SetClassSkillPointPool(data.ClassName, existingPool + unspentFromCreation);
                Debug.Log($"[GameManager] {data.CharacterName}: saved {unspentFromCreation} unspent skill points to {data.ClassName} pool (total pool: {existingPool + unspentFromCreation}).");
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
            // Apply weapon focus weapons from creation data (list-based)
            if (data.WeaponFocusWeapons != null && data.WeaponFocusWeapons.Count > 0)
            {
                foreach (string w in data.WeaponFocusWeapons)
                {
                    if (!string.IsNullOrWhiteSpace(w) && !stats.WeaponFocusWeapons.Contains(w))
                        stats.WeaponFocusWeapons.Add(w);
                }
            }
            else if (!string.IsNullOrEmpty(data.WeaponFocusChoice))
            {
                // Legacy single-weapon path (quickstart presets)
                stats.WeaponFocusChoice = data.WeaponFocusChoice;
            }
            if (!string.IsNullOrEmpty(data.SkillFocusChoice))
                stats.SkillFocusChoice = data.SkillFocusChoice;

            // War domain: grant free Weapon Focus with deity's favored weapon (D&D 3.5e PHB p.191)
            if (stats.ChosenDomains != null && stats.ChosenDomains.Contains("War"))
            {
                GrantWarDomainFeats(stats);
            }

            FeatManager.ApplyPassiveFeats(stats);

            if (targetLevel > baseCreationLevel)
            {
                stats.PendingLevelUps = Mathf.Max(0, stats.PendingLevelUps + (targetLevel - baseCreationLevel));
                Debug.Log($"[GameManager] {data.CharacterName}: queued {stats.PendingLevelUps} pending level-up(s) for creation progression ({baseCreationLevel} -> {targetLevel}).");
            }

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
                // Pass prepared spell slot IDs from character creation.
                // Important: an explicitly empty list means "start with no prepared spells".
                if (data.PreparedSpellSlotIds != null)
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
    /// War domain granted power: free Weapon Focus feat with the deity's favored weapon,
    /// plus martial weapon proficiency if the character doesn't already have it.
    /// Per D&D 3.5e PHB p.191: "The character gets Weapon Focus as a bonus feat...
    /// and proficiency... with the deity's favored weapon."
    /// </summary>
    private static void GrantWarDomainFeats(CharacterStats stats)
    {
        if (stats == null) return;

        DeityDatabase.Init();
        DeityData deity = DeityDatabase.GetDeity(stats.DeityId);
        if (deity == null || string.IsNullOrWhiteSpace(deity.FavoredWeapon))
        {
            Debug.LogWarning($"[GameManager] War domain: no deity or favored weapon for {stats.CharacterName}");
            return;
        }

        string favoredWeapon = deity.FavoredWeapon;

        // Grant Weapon Focus if not already owned
        if (!stats.HasFeat("Weapon Focus"))
        {
            stats.Feats.Add("Weapon Focus");
            Debug.Log($"[GameManager] War domain: granted Weapon Focus to {stats.CharacterName}");
        }

        // Add deity's favored weapon to Weapon Focus weapons list
        if (!stats.WeaponFocusWeapons.Contains(favoredWeapon))
            stats.WeaponFocusWeapons.Add(favoredWeapon);
        Debug.Log($"[GameManager] War domain: {stats.CharacterName} Weapon Focus added {favoredWeapon} ({deity.Name}'s favored weapon)");

        // Grant martial weapon proficiency for the favored weapon if needed.
        // Clerics already have simple weapon proficiency; if the favored weapon is martial,
        // they get proficiency as part of the War domain power.
        // (Proficiency is handled by IsProficientWithWeapon checking ExtraWeaponProficiencies.)
        if (!stats.IsProficientWithWeaponByName(favoredWeapon))
        {
            stats.ExtraWeaponProficiencies.Add(favoredWeapon);
            Debug.Log($"[GameManager] War domain: granted {favoredWeapon} proficiency to {stats.CharacterName}");
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
        // Poison secondary timers continue regardless of input/turn state.
        UpdatePoisonTimers();

        // Skip all game input during character creation / encounter selection / pre-combat inventory.
        if (WaitingForCharacterCreation || WaitingForEncounterSelection || WaitingForPreCombatInventory || WaitingForLootCollection)
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

    private void InitializeDiseaseAndPoisonDatabases()
    {
        DiseaseDatabase.Initialize();
        PoisonDatabase.Initialize();
    }

    private void UpdatePoisonTimers()
    {
        List<CharacterController> characters = GetAllCharacters();
        if (characters == null || characters.Count == 0)
            return;

        for (int c = 0; c < characters.Count; c++)
        {
            CharacterController character = characters[c];
            if (character == null || character.ActivePoisons == null || character.ActivePoisons.Count == 0)
                continue;

            for (int i = character.ActivePoisons.Count - 1; i >= 0; i--)
            {
                ActivePoison poison = character.ActivePoisons[i];
                if (poison == null || poison.PoisonData == null)
                {
                    character.ActivePoisons.RemoveAt(i);
                    continue;
                }

                if (poison.SecondaryResolved)
                {
                    character.ActivePoisons.RemoveAt(i);
                    continue;
                }

                poison.TimeUntilSecondary -= Time.deltaTime;
                if (poison.TimeUntilSecondary <= 0f)
                {
                    character.ProcessPoisonSecondaryDamage(poison);

                    if (poison.SecondaryResolved)
                        character.ActivePoisons.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>
    /// Apply once-per-day disease progression and natural ability damage recovery.
    /// Can be called by future rest/day systems; also auto-called every 14,400 rounds.
    /// </summary>
    public void ProcessDailyEffects()
    {
        List<CharacterController> characters = GetAllCharacters();
        if (characters == null || characters.Count == 0)
            return;

        foreach (CharacterController character in characters)
        {
            if (character == null || character.Stats == null || character.Stats.IsDead)
                continue;

            character.ProcessDiseaseEffectsDaily();
            character.HealAbilityDamageDaily(1, "Daily recovery");
        }

        UpdateAllStatsUI();
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
            case PlayerSubPhase.SelectingFlamingSphereTarget:
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

        if (CombatUI != null && CombatUI.IsDisguiseSelfRaceSelectorOpen())
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
            if (_isSelectingMirrorImageSwap)
                CancelMirrorImageSwapSelectionAndSkip();
            else
                CancelSpecialAttackTargeting();
            return true;
        }

        if (CurrentSubPhase == PlayerSubPhase.SelectingChargeTarget || CurrentSubPhase == PlayerSubPhase.ConfirmingChargePath)
        {
            CancelChargeTargeting();
            return true;
        }

        if (CurrentSubPhase == PlayerSubPhase.SelectingFlamingSphereTarget)
        {
            CancelFlamingSphereControlSelection(showCancelLog: true);
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
        if (summon.Team != CharacterTeam.Player)
            return false;

        ActiveSummonInstance active = GetActiveSummon(summon);
        if (active == null)
            return false;

        if (string.Equals(active.SourceSpellId, SpellNames.SUMMON_SWARM, StringComparison.Ordinal))
        {
            CombatUI?.ShowCombatLog("⚠ Summon Swarm is uncontrolled. You cannot issue commands to it.");
            return true;
        }

        if (!summon.IsControllable)
        {
            CombatUI?.ShowCombatLog("⚠ This summoned ally is AI-controlled. Direct command menus are unavailable.");
            return true;
        }

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
        // NPCs: Multiple enemies from NPCDatabase
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

    private NPCDefinition BuildEncounterDefinitionForSpawn(string enemyId, NPCDefinition sourceDef, int spawnIndex)
    {
        if (sourceDef == null)
            return null;

        // Apply scenario-local template directives at spawn-time to avoid creating dedicated
        // celestial NPC records (wolf/dire bear stay generic base entries in NPCDatabase).
        NPCDefinition scenarioDef = sourceDef;
        if (_isCelestialTemplateTestEncounter
            && (spawnIndex == 0 || spawnIndex == 1)
            && (string.Equals(enemyId, "wolf_pack_hunter", StringComparison.Ordinal)
                || string.Equals(enemyId, "dire_bear", StringComparison.Ordinal)))
        {
            scenarioDef = sourceDef.Clone();
            if (scenarioDef.AppliedTemplateIds == null)
                scenarioDef.AppliedTemplateIds = new List<string>();

            bool alreadyTagged = false;
            for (int i = 0; i < scenarioDef.AppliedTemplateIds.Count; i++)
            {
                if (string.Equals(scenarioDef.AppliedTemplateIds[i], "celestial", StringComparison.OrdinalIgnoreCase))
                {
                    alreadyTagged = true;
                    break;
                }
            }

            if (!alreadyTagged)
                scenarioDef.AppliedTemplateIds.Add("celestial");
        }

        return CreatureTemplateRegistry.ApplyTemplatesClone(scenarioDef);
    }

    private static readonly Vector2Int[] EncounterSpawnPositions = {
        new Vector2Int(16, 6),
        new Vector2Int(14, 10),
        new Vector2Int(16, 14),
        new Vector2Int(13, 8),
        new Vector2Int(13, 12),
    };

    private static readonly Vector2Int[] GreaseTestSpawnPositions = {
        new Vector2Int(12, 5),
        new Vector2Int(13, 6),
        new Vector2Int(12, 6),
        new Vector2Int(13, 5),
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

    private static readonly Vector2Int[] TigerHuntTestSpawnPositions = {
        new Vector2Int(14, 10),
    };

    private static readonly Vector2Int[] OgreBattleTestSpawnPositions = {
        new Vector2Int(8, 10),  // Player ally dire tiger
        new Vector2Int(14, 8),  // Ogre #1
        new Vector2Int(14, 12), // Ogre #2
    };

    private static readonly Vector2Int[] ShieldBashTestSpawnPositions = {
        new Vector2Int(7, 9),   // Orc adjacent to Shielder
        new Vector2Int(11, 9),  // Orc adjacent to Basher
    };

    private static readonly Vector2Int[] CelestialTemplateTestSpawnPositions = {
        new Vector2Int(2, 7),   // Celestial wolf ally
        new Vector2Int(4, 7),   // Celestial dire bear ally
        new Vector2Int(10, 7),  // Skeleton warrior
        new Vector2Int(11, 6),  // Skeleton archer
        new Vector2Int(11, 8),  // Zombie
    };

    private static readonly Vector2Int[] FiendishTemplateTestSpawnPositions = {
        new Vector2Int(2, 7),   // Fiendish wolf ally
        new Vector2Int(4, 7),   // Fiendish dire bear ally
        new Vector2Int(10, 7),  // Human paladin (good)
        new Vector2Int(11, 7),  // Human cleric (good)
    };

    private static readonly Vector2Int[] SummonMonsterTestSpawnPositions = {
        new Vector2Int(13, 7),
        new Vector2Int(15, 9),
        new Vector2Int(13, 12),
    };

    private static readonly Vector2Int[] ProtectionFromEvilTestSpawnPositions = {
        new Vector2Int(12, 9),  // Evil enchanter with line of sight to protected wizard.
        new Vector2Int(10, 9),  // Fiendish wolf starts close enough to test summoned contact barrier.
        new Vector2Int(12, 11), // Evil goblin melee pressure from a flank lane.
        new Vector2Int(8, 5),   // Neutral bandit control (no AC bonus expected).
        new Vector2Int(13, 7),  // Neutral mage control (no save bonus expected).
        new Vector2Int(13, 3),  // Evil acolyte control (+2 save bonus expected).
    };

    private static readonly Vector2Int[] WindDispersionTestSpawnPositions = {
        new Vector2Int(15, 3),  // Small target in lane (knockback + prone case)
        new Vector2Int(15, 5),  // Medium target (prone case)
        new Vector2Int(15, 7),  // Medium high-Fort target (save resistance case)
        new Vector2Int(15, 9),  // Large target (checked case)
        new Vector2Int(12, 11), // Archer off line for concealment-only interaction
    };

    private static readonly Vector2Int[] ObscuringMistRangedOnlySpawnPositions = {
        new Vector2Int(8, 14),  // North longbow
        new Vector2Int(13, 12), // Northeast longbow
        new Vector2Int(15, 8),  // East composite longbow
        new Vector2Int(12, 4),  // Southeast shortbow
        new Vector2Int(8, 2),   // South heavy crossbow
        new Vector2Int(2, 8),   // West shortbow
    };

    private static readonly Vector2Int[] WizardSpellTestSpawnPositions = {
        new Vector2Int(12, 9),
    };

    private static readonly Vector2Int[] ClericSpellTestSpawnPositions = {
        new Vector2Int(12, 9),
    };

    private static readonly Vector2Int[] MirrorImageTestSpawnPositions = {
        new Vector2Int(9, 13),
        new Vector2Int(14, 8),
        new Vector2Int(9, 3),
        new Vector2Int(4, 8),
    };

    /// <summary>Helper to update all stat UI panels using 4-PC multi-NPC system.</summary>
    private void UpdateAllStatsUI()
    {
        RefreshFlankedConditions();

        CharacterController observer = ActivePC != null ? ActivePC : CurrentCharacter;
        List<CharacterController> allCharacters = GetAllCharacters();
        for (int i = 0; i < allCharacters.Count; i++)
        {
            CharacterController character = allCharacters[i];
            if (character == null)
                continue;

            character.RefreshInvisibilityVisualForObserver(observer);
        }

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

        if (normalizedType == CombatConditionType.Asleep)
        {
            RemoveCondition(character, CombatConditionType.Unconscious);
            character.SyncHPStateFromCurrentHP(emitLog: false);

            string wakeMsg = $"⏱ {character.Stats.CharacterName} wakes as sleep duration expires.";
            Debug.Log($"[Condition] {wakeMsg}");
            CombatUI?.ShowCombatLog($"<color=#99CCFF>{wakeMsg}</color>");
            return;
        }

        if (TryHandleColorSprayConditionExpiry(character, condition))
            return;

        if (TryHandleAnimateRopeConditionExpiry(character, condition))
            return;

        if (TryHandleWebConditionExpiry(character, condition))
            return;

        bool fromColorSpray = string.Equals(condition.SourceId, SpellNames.COLOR_SPRAY, StringComparison.Ordinal)
            || string.Equals(condition.SourceName, "Color Spray", StringComparison.Ordinal);
        if (fromColorSpray && (normalizedType == CombatConditionType.Unconscious || normalizedType == CombatConditionType.Blinded))
            return;

        if (normalizedType == CombatConditionType.Frightened)
        {
            string fearEnd = $"⏱ {character.Stats.CharacterName} is no longer frightened.";
            Debug.Log($"[Condition] {fearEnd}");
            CombatUI?.ShowCombatLog($"<color=#99CCFF>{fearEnd}</color>");
            return;
        }

        string conditionLabel = condition.Type.ToString();
        string msg = $"⏱ {character.Stats.CharacterName} is no longer {conditionLabel}.";
        Debug.Log($"[Condition] {msg}");
        CombatUI?.ShowCombatLog($"<color=#99CCFF>{msg}</color>");
    }

    public void BreakCharmOnHostileAction(CharacterController attacker, CharacterController target)
    {
        if (attacker == null || target == null || target.Stats == null)
            return;

        if (!HasCondition(target, CombatConditionType.Charmed))
            return;

        List<ConditionService.ActiveCondition> active = GetActiveConditions(target);
        if (active == null || active.Count == 0)
            return;

        for (int i = 0; i < active.Count; i++)
        {
            ConditionService.ActiveCondition condition = active[i];
            if (condition == null || ConditionRules.Normalize(condition.Type) != CombatConditionType.Charmed)
                continue;

            CharacterController source = condition.Source;
            CharmedConditionData charmData = condition.Data as CharmedConditionData;
            if (source == null && charmData != null)
                source = charmData.Caster;

            bool casterMatch = source == attacker
                || (!string.IsNullOrWhiteSpace(condition.SourceName)
                    && attacker.Stats != null
                    && string.Equals(condition.SourceName, attacker.Stats.CharacterName, StringComparison.Ordinal));

            if (!casterMatch)
                continue;

            if (RemoveCondition(target, CombatConditionType.Charmed))
            {
                CombatUI?.ShowCombatLog($"💔 {target.Stats.CharacterName} is no longer charmed after being attacked by {attacker.Stats.CharacterName}.");
            }

            return;
        }
    }

    /// <summary>
    /// Breaks Command Undead control when the caster (or their allies) threatens the commanded undead.
    /// PHB p.211: Any act by the caster or the caster's apparent allies that threatens the
    /// commanded undead breaks the spell immediately.
    /// </summary>
    public void BreakCommandUndeadOnHostileAction(CharacterController attacker, CharacterController target)
    {
        if (attacker == null || target == null || target.Stats == null)
            return;

        if (!target.IsCommandedUndead)
            return;

        CharacterController controller = target.CommandUndeadController;
        if (controller == null)
            return;

        // Check if attacker is the caster or an ally of the caster
        bool isCasterOrAlly = (attacker == controller) || !IsEnemyTeam(attacker, controller);

        if (isCasterOrAlly)
        {
            string attackerName = attacker.Stats != null ? attacker.Stats.CharacterName : "Unknown";
            CombatUI?.ShowCombatLog($"<color=#FF6666>💔 {target.Stats.CharacterName} is no longer commanded! " +
                $"Threatening act by {attackerName} broke the Command Undead spell.</color>");
            target.BreakCommandUndeadControl($"Threatening act by {attackerName}");
        }
    }

    private bool IsHostileSpellCast(CharacterController caster, SpellData spell, CharacterController primaryTarget, List<CharacterController> areaTargets, out CharacterController hostileTarget)
    {
        hostileTarget = null;

        if (caster == null || spell == null)
            return false;

        if (primaryTarget != null && primaryTarget.Stats != null && IsEnemyTeam(caster, primaryTarget))
        {
            hostileTarget = primaryTarget;
            return true;
        }

        if (areaTargets == null || areaTargets.Count == 0)
            return false;

        for (int i = 0; i < areaTargets.Count; i++)
        {
            CharacterController candidate = areaTargets[i];
            if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                continue;

            if (!IsEnemyTeam(caster, candidate))
                continue;

            hostileTarget = candidate;
            return true;
        }

        return false;
    }

    private void BreakInvisibilityOnHostileSpellCast(CharacterController caster, SpellData spell, CharacterController primaryTarget = null, List<CharacterController> areaTargets = null)
    {
        if (caster == null || spell == null)
            return;

        if (!caster.HasActiveInvisibilityEffect)
            return;

        if (!IsHostileSpellCast(caster, spell, primaryTarget, areaTargets, out CharacterController hostileTarget))
            return;

        caster.BreakInvisibility("hostile spell", hostileTarget);
    }

    private void UpdateEnemyLastKnownPositionForInvisibility(CharacterController invisibleCharacter)
    {
        if (invisibleCharacter == null || invisibleCharacter.Stats == null)
            return;

        List<CharacterController> allCharacters = GetAllCharacters();
        for (int i = 0; i < allCharacters.Count; i++)
        {
            CharacterController observer = allCharacters[i];
            if (observer == null || observer == invisibleCharacter || observer.Stats == null || observer.Stats.IsDead)
                continue;

            if (!IsEnemyTeam(observer, invisibleCharacter))
                continue;

            observer.UpdateLastKnownPosition(invisibleCharacter, incomingIsRangedAttack: observer.IsEquippedWeaponRanged());

            LastKnownPositionTracker tracker = observer.GetComponent<LastKnownPositionTracker>();
            tracker?.UpdateLastKnownPosition(invisibleCharacter);
        }
    }

    public void BreakFascinationOnHostileAction(CharacterController attacker, CharacterController target, string disturbanceReason = "hostile action")
    {
        if (attacker == null || attacker.Stats == null || target == null || target.Stats == null)
            return;

        if (!HasCondition(target, CombatConditionType.Fascinated))
            return;

        List<ConditionService.ActiveCondition> active = GetActiveConditions(target);
        if (active == null || active.Count == 0)
            return;

        for (int i = 0; i < active.Count; i++)
        {
            ConditionService.ActiveCondition condition = active[i];
            if (condition == null || ConditionRules.Normalize(condition.Type) != CombatConditionType.Fascinated)
                continue;

            CharacterController source = ResolveFascinationSource(condition);
            if (source == null || source.Stats == null)
                continue;

            bool hostileBySourceSide = attacker == source || IsAllyTeam(attacker, source);
            if (!hostileBySourceSide)
                continue;

            TryDisturbFascinatedTarget(target, condition, disturbanceReason, attacker);
            return;
        }
    }

    private CharacterController ResolveFascinationSource(ConditionService.ActiveCondition condition)
    {
        if (condition == null)
            return null;

        if (condition.Source != null)
            return condition.Source;

        if (condition.Data is FascinatedConditionData fascinatedData && fascinatedData.Caster != null)
            return fascinatedData.Caster;

        if (string.IsNullOrWhiteSpace(condition.SourceName))
            return null;

        List<CharacterController> all = GetAllCharacters();
        for (int i = 0; i < all.Count; i++)
        {
            CharacterController candidate = all[i];
            if (candidate == null || candidate.Stats == null)
                continue;

            if (string.Equals(candidate.Stats.CharacterName, condition.SourceName, StringComparison.Ordinal))
                return candidate;
        }

        return null;
    }

    private void TryDisturbFascinatedTarget(
        CharacterController target,
        ConditionService.ActiveCondition fascinatedCondition,
        string reason,
        CharacterController disturber)
    {
        if (target == null || target.Stats == null || fascinatedCondition == null)
            return;

        CharacterController source = ResolveFascinationSource(fascinatedCondition);
        FascinatedConditionData data = fascinatedCondition.Data as FascinatedConditionData;

        string reasonText = string.IsNullOrWhiteSpace(reason) ? "disturbance" : reason;
        int disturbanceDc = 10;
        if (data != null)
            disturbanceDc = Mathf.Max(1, data.DisturbanceSaveDC);

        int saveRoll = DiceService.D20("Disturbance Will save");
        int saveTotal = saveRoll + target.Stats.WillSave;
        bool saveSucceeded = saveTotal >= disturbanceDc;

        if (saveSucceeded)
        {
            if (RemoveCondition(target, CombatConditionType.Fascinated))
            {
                CombatUI?.ShowCombatLog($"🔔 {target.Stats.CharacterName} is disturbed by {reasonText} and breaks free of fascination (Will {saveTotal} vs DC {disturbanceDc}).");
            }
            return;
        }

        string sourceName = source != null && source.Stats != null ? source.Stats.CharacterName : fascinatedCondition.SourceName;
        string disturberName = disturber != null && disturber.Stats != null ? disturber.Stats.CharacterName : "disturbance";
        if (!string.IsNullOrWhiteSpace(sourceName))
            CombatUI?.ShowCombatLog($"👁 {target.Stats.CharacterName} remains fascinated by {sourceName} despite {disturberName}'s {reasonText} (Will {saveTotal} vs DC {disturbanceDc}).");
    }

    public void BreakFascinationFromLoudNoise(CharacterController noiseSource, Vector2Int noiseOrigin, int radiusSquares = 6)
    {
        if (noiseSource == null || noiseSource.Stats == null)
            return;

        List<CharacterController> all = GetAllCharacters();
        for (int i = 0; i < all.Count; i++)
        {
            CharacterController candidate = all[i];
            if (candidate == null || candidate == noiseSource || candidate.Stats == null || candidate.Stats.IsDead)
                continue;

            int dist = SquareGridUtils.GetDistance(noiseOrigin, candidate.GridPosition);
            if (dist > Mathf.Max(1, radiusSquares))
                continue;

            List<ConditionService.ActiveCondition> active = GetActiveConditions(candidate);
            if (active == null || active.Count == 0)
                continue;

            for (int condIndex = 0; condIndex < active.Count; condIndex++)
            {
                ConditionService.ActiveCondition condition = active[condIndex];
                if (condition == null || ConditionRules.Normalize(condition.Type) != CombatConditionType.Fascinated)
                    continue;

                CharacterController fascinationSource = ResolveFascinationSource(condition);
                if (fascinationSource == null || fascinationSource.Stats == null)
                    continue;

                bool fromSourceSide = noiseSource == fascinationSource || IsAllyTeam(noiseSource, fascinationSource);
                if (!fromSourceSide)
                    continue;

                TryDisturbFascinatedTarget(candidate, condition, "loud noise", noiseSource);
                break;
            }
        }
    }

    private bool IsActiveCombatant(CharacterController c)
    {
        return c != null && c.gameObject != null && c.gameObject.activeInHierarchy && c.Stats != null;
    }

    private bool HasRegenerationOrFastHealing(CharacterController npc, bool logMatches = true)
    {
        if (npc == null || npc.Stats == null)
            return false;

        CharacterStats stats = npc.Stats;
        string npcName = string.IsNullOrWhiteSpace(stats.CharacterName) ? npc.name : stats.CharacterName;

        if (npc.HasRegeneration)
        {
            if (logMatches)
                Debug.Log($"[VictoryCheck] {npcName} can recover (innate regeneration configured).");
            return true;
        }

        if (stats.SpecialAbilities != null)
        {
            for (int i = 0; i < stats.SpecialAbilities.Count; i++)
            {
                string ability = stats.SpecialAbilities[i];
                if (string.IsNullOrWhiteSpace(ability))
                    continue;

                string normalized = ability.Trim().ToLowerInvariant();
                if (normalized.Contains("regeneration") || normalized.Contains("fast healing"))
                {
                    if (logMatches)
                        Debug.Log($"[VictoryCheck] {npcName} can recover via trait '{ability}'.");
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Check if all hostile (enemy-team) combatants are defeated for combat-resolution purposes.
    /// D&D 3.5e handling: HP <= 0 counts as down/defeated unless the target has regeneration/fast healing and can recover.
    /// </summary>
    private bool AreAllNPCsDead()
    {
        Debug.Log("[VictoryCheck] Checking if all enemies are defeated (HP <= 0 unless recoverable).");

        if (NPCs == null)
        {
            Debug.Log("[VictoryCheck] AreAllNPCsDead called with null NPC list. Treating as victory-safe true.");
            return true;
        }

        int aliveEnemies = 0;
        for (int i = 0; i < NPCs.Count; i++)
        {
            CharacterController npc = NPCs[i];
            bool active = IsActiveCombatant(npc);
            bool isEnemy = active && npc.Team == CharacterTeam.Enemy;
            string npcName = npc != null && npc.Stats != null ? npc.Stats.CharacterName : $"<npc:{i}>";

            if (!active || !isEnemy)
            {
                Debug.Log($"[VictoryCheck] Skipping NPC in victory scan | idx={i} | name={npcName} | active={active} | isEnemy={isEnemy}");
                continue;
            }

            int hp = npc.Stats.CurrentHP;
            bool atZeroOrBelow = hp <= 0;
            bool canRecover = HasRegenerationOrFastHealing(npc);
            bool isDefeated = atZeroOrBelow && !canRecover;

            Debug.Log($"[VictoryCheck] Enemy #{i}: {npcName} | hp={hp} | atOrBelowZero={atZeroOrBelow} | canRecover={canRecover} | countsAsAlive={!isDefeated}");

            if (!isDefeated)
            {
                aliveEnemies++;

                if (atZeroOrBelow && canRecover)
                    Debug.Log($"[VictoryCheck] {npcName} is down but can recover; still counted as alive.");
                else
                    Debug.Log($"[VictoryCheck] {npcName} is still fighting.");
            }
            else
            {
                Debug.Log($"[VictoryCheck] {npcName} is defeated for victory checks.");
            }
        }

        bool allDead = aliveEnemies == 0;
        Debug.Log($"[VictoryCheck] AreAllNPCsDead result={allDead} | aliveEnemies={aliveEnemies} | snapshot={BuildEnemyStatusSnapshot()}");
        return allDead;
    }

    /// <summary>
    /// Check if all active PCs in the party are defeated (HP <= 0).
    /// For combat resolution, unconscious/disabled/dying PCs count as unable to continue.
    /// </summary>
    private bool AreAllPCsDead()
    {
        foreach (var pc in PCs)
        {
            if (!IsActiveCombatant(pc))
                continue;

            if (pc.Stats.CurrentHP > 0)
                return false;
        }

        // If no active PCs remain, treat the party as defeated.
        return true;
    }

    /// <summary>Count remaining alive hostile (enemy-team) NPCs.</summary>
    private int GetAliveNPCCount()
    {
        if (NPCs == null)
            return 0;

        int count = 0;
        foreach (var npc in NPCs)
        {
            if (!IsActiveCombatant(npc))
                continue;

            if (npc.Team != CharacterTeam.Enemy)
                continue;

            bool atZeroOrBelow = npc.Stats.CurrentHP <= 0;
            bool canRecover = HasRegenerationOrFastHealing(npc, logMatches: false);
            bool isDefeated = atZeroOrBelow && !canRecover;

            if (!isDefeated)
                count++;
        }

        Debug.Log($"[VictoryCheck] GetAliveNPCCount -> {count} | snapshot={BuildEnemyStatusSnapshot()}");
        return count;
    }

    private string BuildEnemyStatusSnapshot()
    {
        if (NPCs == null)
            return "NPCs=<null>";

        List<string> entries = new List<string>();
        for (int i = 0; i < NPCs.Count; i++)
        {
            CharacterController npc = NPCs[i];
            if (npc == null)
            {
                entries.Add($"#{i}:<null>");
                continue;
            }

            string name = npc.Stats != null ? npc.Stats.CharacterName : npc.name;
            bool active = IsActiveCombatant(npc);
            bool enemy = npc.Team == CharacterTeam.Enemy;
            int hp = npc.Stats != null ? npc.Stats.CurrentHP : 0;
            bool dead = npc.Stats != null && npc.Stats.IsDead;
            bool canRecover = HasRegenerationOrFastHealing(npc, logMatches: false);
            bool defeatedForVictory = hp <= 0 && !canRecover;
            entries.Add($"#{i}:{name}[active={active},enemy={enemy},dead={dead},hp={hp},canRecover={canRecover},defeatedForVictory={defeatedForVictory}]");
        }

        return string.Join("; ", entries);
    }

    private void RegisterDefeatedEnemyForXP(CharacterController character, string sourceContext)
    {
        if (character == null || character.Stats == null)
            return;

        if (character.Team != CharacterTeam.Enemy)
            return;

        bool countsAsDefeated = character.Stats.CurrentHP <= 0 && !HasRegenerationOrFastHealing(character, logMatches: false);
        if (!countsAsDefeated)
            return;

        if (_defeatedEnemiesThisCombat.Contains(character))
            return;

        _defeatedEnemiesThisCombat.Add(character);
        string enemyName = string.IsNullOrWhiteSpace(character.Stats.CharacterName) ? "Unknown Enemy" : character.Stats.CharacterName;
        string cr = string.IsNullOrWhiteSpace(character.Stats.ChallengeRating) ? "—" : character.Stats.ChallengeRatingDisplay;
        Debug.Log($"[Combat] Enemy defeated tracked: {enemyName} (CR {cr}) | source={sourceContext}");
    }

    private void CaptureDefeatedEnemiesSnapshotForXP(string sourceContext)
    {
        if (NPCs == null)
            return;

        for (int i = 0; i < NPCs.Count; i++)
            RegisterDefeatedEnemyForXP(NPCs[i], sourceContext);

        Debug.Log($"[XP] Defeated enemy snapshot captured | source={sourceContext} | tracked={_defeatedEnemiesThisCombat.Count}");
    }

    public List<CharacterController> GetDefeatedEnemiesForXP()
    {
        return new List<CharacterController>(_defeatedEnemiesThisCombat);
    }

    private bool CheckCombatVictory(string sourceContext, CharacterController defeatedTarget = null)
    {
        RegisterDefeatedEnemyForXP(defeatedTarget, sourceContext);

        string targetName = defeatedTarget != null && defeatedTarget.Stats != null ? defeatedTarget.Stats.CharacterName : "<none>";
        Debug.Log($"[VictoryCheck] ENTER | source={sourceContext} | frame={Time.frameCount} | phase={CurrentPhase} | target={targetName} | targetDead={(defeatedTarget != null && defeatedTarget.Stats != null && defeatedTarget.Stats.IsDead)}");

        int aliveBefore = GetAliveNPCCount();

        if (CurrentPhase == TurnPhase.CombatOver)
        {
            Debug.Log($"[VictoryCheck] EARLY RETURN | source={sourceContext} | reason=CurrentPhase already CombatOver | aliveBefore={aliveBefore}");
            return false;
        }

        bool allEnemiesDead = AreAllNPCsDead();
        int aliveAfter = GetAliveNPCCount();
        Debug.Log($"[VictoryCheck] EVALUATED | source={sourceContext} | aliveBefore={aliveBefore} | aliveAfter={aliveAfter} | allEnemiesDead={allEnemiesDead}");

        if (!allEnemiesDead)
            return false;

        Debug.Log($"[VictoryCheck] All enemies dead. Calling HandleCombatVictoryDetected | source={sourceContext}");
        HandleCombatVictoryDetected(sourceContext);
        Debug.Log($"[VictoryCheck] EXIT after HandleCombatVictoryDetected | source={sourceContext} | waitingLoot={WaitingForLootCollection} | phaseNow={CurrentPhase}");
        return true;
    }

    /// <summary>Get first alive hostile NPC (for backward compat in single-target scenarios).</summary>
    private CharacterController GetFirstAliveNPC()
    {
        foreach (var npc in NPCs)
        {
            if (!IsActiveCombatant(npc) || npc.Stats.IsDead) continue;
            if (npc.Team != CharacterTeam.Enemy) continue;
            return npc;
        }
        return null;
    }

    private void HandleCombatVictoryDetected(string sourceContext)
    {
        Debug.Log($"[CombatEnd] Victory detected | source={sourceContext} | frame={Time.frameCount} | phaseBefore={CurrentPhase} | waitingLootBefore={WaitingForLootCollection}");

        CurrentPhase = TurnPhase.CombatOver;
        CombatUI?.SetTurnIndicator("VICTORY! All enemies defeated!");
        CombatUI?.SetActionButtonsVisible(false);

        Debug.Log($"[CombatEnd] Loot safeguards | source={sourceContext} | lootUiAssigned={LootCollectionUI != null} | partyStashAssigned={PartyStash != null}");
        if (LootCollectionUI == null)
            Debug.LogWarning("[LootUI] LootCollectionUI reference is null before BeginPostCombatLootCollection. Initialization will be attempted.");
        if (PartyStash == null)
            Debug.LogWarning("[LootFlow] PartyStash is null before BeginPostCombatLootCollection. Initialization will be attempted.");

        CaptureDefeatedEnemiesSnapshotForXP($"{sourceContext}.Victory");

        Debug.Log($"[CombatEnd] Triggering post-combat loot collection | source={sourceContext} | waitingBefore={WaitingForLootCollection}");
        BeginPostCombatLootCollection();
        Debug.Log($"[CombatEnd] Post-combat loot collection invoked | source={sourceContext} | waitingAfter={WaitingForLootCollection} | phaseAfter={CurrentPhase}");
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
        int turnOrderCountBefore = _turnService != null && _turnService.InitiativeOrder != null ? _turnService.InitiativeOrder.Count : 0;
        string currentCharacterName = CurrentCharacter != null && CurrentCharacter.Stats != null
            ? CurrentCharacter.Stats.CharacterName
            : "None";
        int activeNpcCountBefore = 0;
        if (NPCs != null)
        {
            for (int i = 0; i < NPCs.Count; i++)
            {
                CharacterController npc = NPCs[i];
                if (npc != null && npc.gameObject != null && npc.gameObject.activeSelf)
                    activeNpcCountBefore++;
            }
        }

        Debug.Log($"[CombatStart] Pre-start state | phase={CurrentPhase} | subPhase={CurrentSubPhase} | turnOrder={turnOrderCountBefore} | selectedCharacter={currentCharacterName} | activeNPCs={activeNpcCountBefore}");

        bool hasLingeringTurnState = _turnService != null && _turnService.HasInitiativeEntries();
        bool hasLingeringCombatSelections = _isInAttackSequence
            || _isSelectingSpecialAttack
            || _isSelectingWithdraw
            || _isAoETargeting
            || (_highlightedCells != null && _highlightedCells.Count > 0)
            || _waitingForAoOConfirmation;

        if (hasLingeringTurnState || hasLingeringCombatSelections)
        {
            Debug.LogWarning($"[CombatStart] Detected lingering combat state before StartCombat (turnState={hasLingeringTurnState}, selections={hasLingeringCombatSelections}). Forcing reset.");
            ResetCombatStateForNextEncounter("StartCombat.DefensivePreInit");
        }

        Debug.Log("[CombatStart] Starting combat");

        WaitingForPreCombatInventory = false;
        ResetPostCombatLootCollectionState("StartCombat");
        _defeatedEnemiesThisCombat.Clear();
        Debug.Log("[XP] Cleared defeated enemy tracker for new combat.");

        PreCombatHubUI?.Close();
        if (StoreUI != null && StoreUI.IsOpen)
            StoreUI.Close();
        if (SpellPreparationUI != null && SpellPreparationUI.IsOpen)
            SpellPreparationUI.Close();

        if (PreCombatInventoryUI != null && PreCombatInventoryUI.IsOpen)
        {
            Debug.Log("[CombatStart] Closing pre-combat UI");
            PreCombatInventoryUI.Close(suppressCallback: true);
        }

        LootCollectionUI?.Close(invokeClosedCallback: false);
        Debug.Log($"[CombatStart] Post-loot reset checkpoint | waitingLoot={WaitingForLootCollection} | lootTriggered={_postCombatLootCollectionTriggered} | preCombatUiOpen={(PreCombatInventoryUI != null && PreCombatInventoryUI.IsOpen)} | lootUiAssigned={LootCollectionUI != null} | lootUiOpen={(LootCollectionUI != null && LootCollectionUI.IsOpen)}");
        PartyStash?.Lock();

        if (CombatUI == null)
        {
            Debug.LogError("[CombatStart] CombatUI is null. Cannot start combat.");
            return;
        }

        if (!CombatUI.gameObject.activeSelf)
        {
            Debug.LogWarning("[CombatStart] CombatUI inactive, activating");
            CombatUI.gameObject.SetActive(true);
        }

        TurnPhase previousPhase = CurrentPhase;
        CurrentPhase = TurnPhase.PCTurn;
        CurrentSubPhase = PlayerSubPhase.ChoosingAction;
        Debug.Log($"[CombatPhase] Transitioning: {previousPhase} → {CurrentPhase} (combat bootstrap)");

        CombatUI.InitializeForCombat();

        ClearAllActiveGreaseEffects();

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

        Debug.Log($"[Initiative] Beginning initiative phase | activePCs={activePCs.Count} | activeNPCs={activeNPCs.Count}");

        List<CharacterController> forcedFirst = GetForcedFirstInitiativeActors();
        _turnService?.StartCombat(activePCs, activeNPCs, IsPC, forcedFirst);

        string orderStr = _turnService != null ? _turnService.GetInitiativeOrderString() : "No combatants";
        Debug.Log($"[Initiative] Combat begins! Initiative order:\n{orderStr}");

        // Publish combat started event
        GameEventSystem.Instance.Publish(new CombatStartedEvent
        {
            TotalCombatants = activePCs.Count + activeNPCs.Count,
            Round = 1
        });

        UpdateInitiativeUI();
    }

    private List<CharacterController> GetForcedFirstInitiativeActors()
    {
        if ((_isWizardSpellTestEncounter || _isClericSpellTestEncounter || _isMirrorImageTestEncounter) && IsActiveCombatant(PC1) && PC1 != null)
        {
            return new List<CharacterController> { PC1 };
        }

        return null;
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
        string characterName = character != null && character.Stats != null ? character.Stats.CharacterName : "<null>";
        Debug.Log($"[CombatPhase] OnTurnStarted | phase={CurrentPhase} | subPhase={CurrentSubPhase} | character={characterName}");

        if (character == null)
        {
            Debug.LogWarning("[CombatPhase] OnTurnStarted received null character.");
            return;
        }

        if (CurrentPhase == TurnPhase.CombatOver)
        {
            Debug.LogWarning($"[CombatPhase] Ignoring turn start for {characterName} because phase is CombatOver.");
            return;
        }

        // Publish turn started event
        GameEventSystem.Instance.Publish(new TurnStartedEvent
        {
            Character = character,
            IsPC = IsPC(character),
            RoundNumber = CurrentRoundNumber
        });

        // Tick domain power durations at start of turn
        TickDomainPowerDurations(character);

        if (IsPC(character))
        {
            Debug.Log($"[CombatPhase] Entering player turn for {characterName}");
            StartPCTurn(character);
        }
        else
        {
            Debug.Log($"[CombatPhase] Entering NPC turn for {characterName}");
            StartCoroutine(SingleNPCTurnFromInitiative(character));
        }
    }

    private void OnNewRound(int round)
    {
        Debug.Log($"[GameManager] ═══ ROUND {round} BEGINS ═══");

        // Publish new round event
        GameEventSystem.Instance.Publish(new NewRoundEvent { RoundNumber = round });

        CombatUI.AddTurnSeparator(round);
        ResetQuickenedSpellTrackingForAllCharacters();
        ResetAttackDamageModesForAllCharacters();

        // Tick all spell + condition effect durations at the start of each new round.
        TickAllSpellDurations();
        _conditionService?.OnRoundEnd();

        // Tick summon durations (Summon Monster: 1 round/level)
        TickSummonDurations();

        // Tick all emanation durations (Magic Circles, etc.)
        TickEmanations();

        // Tick persistent Grease zones/objects.
        TickActiveGreaseEffects();

        // Keep Turn Undead tracker table aligned with condition expiration.
        PruneTurnUndeadTrackers();
        LogOngoingTurnUndeadStatusAtRoundStart();

        if (round > 0 && round % RoundsPerDay == 0)
            ProcessDailyEffects();
    }

    private void OnCombatEnded()
    {
        int aliveEnemiesBefore = GetAliveNPCCount();
        bool allNpcsDead = AreAllNPCsDead();
        bool allPcsDead = AreAllPCsDead();
        bool isVictory = allNpcsDead && !allPcsDead;

        Debug.Log($"[LootFlow] OnCombatEnded triggered | frame={Time.frameCount} | activeNPCs={(NPCs != null ? NPCs.Count : 0)} | activePCs={(PCs != null ? PCs.Count : 0)} | aliveEnemiesBefore={aliveEnemiesBefore} | allNpcsDead={allNpcsDead} | allPcsDead={allPcsDead} | victory={isVictory}");

        // Publish combat ended event
        GameEventSystem.Instance.Publish(new CombatEndedEvent
        {
            PlayerVictory = isVictory,
            Context = "OnCombatEnded"
        });

        CurrentPhase = TurnPhase.CombatOver;
        _activeEmanations.Clear();
        ClearAllActiveGreaseEffects();
        ClearAllMirrorImageEffects("combat ended");
        MeleeReactionService.ClearAll();
        CurseTracker.ClearAll();
        _conditionService?.CleanupOnCombatEnd(GetAllCharacters());

        CombatUI.SetTurnIndicator(isVictory ? "VICTORY! All enemies defeated!" : "Combat has ended.");
        CombatUI.SetActionButtonsVisible(false);
        UpdateInitiativeUI();

        if (isVictory)
        {
            Debug.Log("[CombatEnd] OnCombatEnded detected victory; invoking loot collection.");
            BeginPostCombatLootCollection();
            Debug.Log($"[LootFlow] OnCombatEnded post-invoke state | waitingLoot={WaitingForLootCollection} | lootUiAssigned={LootCollectionUI != null}");
        }
        else
        {
            Debug.Log("[CombatEnd] OnCombatEnded without victory; skipping loot collection.");
        }
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
        if (character == null || character.Stats == null)
            return true;

        if (!character.CanTakeTurnActions())
            return true;

        return !character.CanTakeActions();
    }

    private string GetUnableToActReason(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return "cannot act";

        if (!character.CanTakeTurnActions())
        {
            if (character.CurrentHPState == HPState.Dead)
                return "is dead";

            return "is unconscious";
        }

        List<StatusEffect> active = character.GetActiveConditions();
        for (int i = 0; i < active.Count; i++)
        {
            CombatConditionType normalized = ConditionRules.Normalize(active[i].Type);
            if (normalized == CombatConditionType.HideousLaughter)
                return "is laughing uncontrollably";

            ConditionDefinition def = ConditionRules.GetDefinition(active[i].Type);
            if (def.PreventsStandardActions && def.PreventsFullRoundActions)
                return $"is {def.DisplayName.ToLowerInvariant()}";
        }

        return "cannot act";
    }
    /// <summary>Move to the next initiative slot and start that turn.</summary>
    private void NextInitiativeTurn()
    {
        CharacterController endingCharacter = CurrentCharacter;

        // Spell-specific end-of-turn expiration (D&D 3.5e timing):
        // True Strike lasts until end of caster's next turn if unused.
        var trueStrike = endingCharacter != null ? endingCharacter.GetComponent<TrueStrikeEffect>() : null;
        trueStrike?.CheckExpirationAtTurnEnd(endingCharacter, CurrentRound);

        endingCharacter?.ProcessPinnedDurationAtTurnEnd();
        WarnFlamingSphereNotMovedAtTurnEnd(endingCharacter);
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
        HandleFlamingSphereTurnStart(pc);
        ApplyMelfsAcidArrowTurnStartDamage(pc);
        pc.TickBombardierAcidSprayCooldown();
        pc.ApplyRegenerationAtTurnStart();
        CloseInventoryIfOpen();

        // Tick Aid Another expiry counters before actions; this keeps bonuses available for one full beneficiary turn.
        ExpireAidBonusesAtTurnStart(pc);

        // If this PC is unconscious/dead, skip their actions.
        if (ShouldSkipTurnDueToHPState(pc))
        {
            if (pc != null && pc.Stats != null)
            {
                string reason = GetUnableToActReason(pc);
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
        _weaponAttacksCommittedThisTurn = 0;
        _usedNaturalAttackSequenceIndices.Clear();

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
        if (pcIdx > 0)
        {
            CombatUI.SetActivePC(pcIdx);
            CombatUI.SetActiveNPC(-1); // Clear NPC highlights when a core party member is active
        }
        else
        {
            // Player-controlled non-party combatants (hirelings, dominated foes, etc.)
            // still use player input but are represented in NPC panels.
            CombatUI.SetActivePC(0);
            CombatUI.SetActiveNPC(NPCs.IndexOf(pc));
        }

        // Update initiative UI to highlight current character
        UpdateInitiativeUI();

        if (TryBeginConfusedPCTurn(pc))
            return;

        Debug.Log($"[Turn] Beginning turn for {pc.Stats.CharacterName}");
        Debug.Log("[Turn] Showing action buttons");
        ShowActionChoices();
        Debug.Log("[Turn] Player turn ready");
    }

    private bool TryBeginConfusedPCTurn(CharacterController pc)
    {
        _confusedBehaviorController ??= new ConfusedBehaviorController();
        if (!_confusedBehaviorController.TryRollDecision(this, pc, out ConfusedBehaviorController.ConfusedTurnDecision decision))
            return false;

        if (decision.Mode == ConfusedBehaviorController.ConfusedTurnMode.ActNormally)
        {
            CombatUI?.ShowCombatLog($"🌀 {pc.Stats.CharacterName} is confused but acts normally this turn.");
            return false;
        }

        CurrentSubPhase = PlayerSubPhase.Animating;
        CombatUI?.SetActionButtonsVisible(false);
        StartCoroutine(ExecuteConfusedForcedPCTurn(pc, decision));
        return true;
    }

    private IEnumerator ExecuteConfusedForcedPCTurn(CharacterController pc, ConfusedBehaviorController.ConfusedTurnDecision decision)
    {
        if (_confusedBehaviorController == null)
            yield break;

        yield return StartCoroutine(_confusedBehaviorController.ExecuteDecision(this, pc, decision));

        if (CurrentPhase == TurnPhase.PCTurn && CurrentCharacter == pc)
            EndCurrentTurn();
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
        ClearPendingNaturalAttackSelection();
        EndGrappleContextMenuDisplayLock();
        CombatUI.HideSummonContextMenu();
        CombatUI.HideDisguiseSelfRaceSelector();

        _waitingForAoOConfirmation = false;
        _pendingAoOAction = null;
        _isSelectingWithdraw = false;
        _isSelectingBreakWallTarget = false;
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

        CombatUI.ShowActionButtonsForCharacter(pc);
        CombatUI.HideSpecialAttackMenu();

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

    /// <summary>
    /// Returns the action type required to use a stowed item from inventory.
    /// Currently always FullRound (retrieve + use = full-round action).
    /// TODO: Future — bags of holding, quick-draw pouches, and feats (e.g. Quick Draw for
    /// alchemical items) can reduce this to standard or even move action.
    /// </summary>
    private string GetItemUseActionType(CharacterController actor)
    {
        // Future: check actor for bags, feats, or item-specific overrides here.
        // e.g. if (actor.HasQuickDrawPouch()) return "Move";
        return "FullRound";
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

        string actionType = GetItemUseActionType(actor);
        if (actionType == "FullRound")
        {
            // Retrieving a stowed item and using it requires a full-round action (PHB p.142).
            if (actor.Actions.HasFullRoundAction)
                return true;

            reason = "No full-round action available (retrieving and using a stowed item is a full-round action).";
            return false;
        }

        // Fallback for future reduced-action types
        if (actor.Actions.HasMoveAction || actor.Actions.CanConvertStandardToMove || actor.Actions.HasStandardAction)
            return true;

        reason = "No action available to manipulate an item.";
        return false;
    }

    private void ConsumeItemManipulationAction(CharacterController actor)
    {
        if (actor == null) return;

        string actionType = GetItemUseActionType(actor);
        if (actionType == "FullRound")
        {
            actor.Actions.UseFullRoundAction();
            return;
        }

        // Fallback for future reduced-action types
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
            ActionDescription = $"Use {item.Name} (full-round action: retrieve + use)",
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
        CombatUI?.ShowCombatLog($"{actor.Stats.CharacterName} uses a full-round action to retrieve and use {item.Name} (provokes AoO).");

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

    // ==================== QUICK ITEM USE ====================

    /// <summary>
    /// Called when the "Use Item" button is pressed in combat.
    /// Opens the QuickItemUsePanel showing the active character's consumable items.
    /// </summary>
    public void OnUseItemButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;

        if (RedirectPinnedCharacterToGrappleMenu(pc, "using items"))
            return;

        if (!CanUseItemManipulationAction(pc, out string reason))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot use items: {reason} (retrieving and using a stowed item is a full-round action)");
            return;
        }

        if (QuickItemUsePanel == null)
        {
            CombatUI?.ShowCombatLog("⚠ Quick Item Use panel is not available.");
            return;
        }

        // Set up callbacks
        QuickItemUsePanel.OnItemSelected = (inventoryIndex) =>
        {
            // Delegate to existing consumable use system which handles AoO, action economy, etc.
            if (TryUseConsumableFromInventory(pc, inventoryIndex, out string feedback))
            {
                CombatUI?.ShowCombatLog(feedback);
            }
            else
            {
                CombatUI?.ShowCombatLog($"⚠ {feedback}");
            }
            ShowActionChoices();
        };
        QuickItemUsePanel.OnCancelled = () =>
        {
            ShowActionChoices();
        };

        QuickItemUsePanel.Open(pc);
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

        if (!TryResolveGreasedItemPickup(actor, item, out string greasePickupFailure))
        {
            feedback = greasePickupFailure;
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
                total += DiceService.RollDie(spell.HealDice, "Spell healing die");
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
                total += DiceService.RollDie(item.HealDiceSides, "Item healing die");
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

        if (pc.Stats.MovementBlockedByCondition || GetCurrentMoveRangeSquares(pc) <= 0)
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
        _isSelectingWithdraw = false;
        CurrentSubPhase = PlayerSubPhase.Moving;
        ShowMovementRange(pc);
        CombatUI.SetActionButtonsVisible(false);
        CombatUI.SetTurnIndicator($"{pc.Stats.CharacterName} - Click a tile to move (right-click/ESC or own tile to cancel)");
    }

    private int GetWithdrawMoveRangeSquares(CharacterController character)
    {
        int baseRange = GetCurrentMoveRangeSquares(character);
        if (baseRange <= 0)
            return 0;

        return baseRange * 2;
    }

    public string GetWithdrawDisabledReason(CharacterController character)
    {
        if (_combatFlowService != null)
        {
            if (_combatFlowService.CanPerformWithdraw(character, out string reason))
                return string.Empty;

            return reason;
        }

        return "Combat flow unavailable";
    }

    public void OnWithdrawButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null)
            return;

        if (RedirectPinnedCharacterToGrappleMenu(pc, "withdrawing"))
            return;

        string reason = GetWithdrawDisabledReason(pc);
        if (!string.IsNullOrEmpty(reason))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot withdraw: {reason}.");
            return;
        }

        EndAttackSequence();
        _isSelectingWithdraw = true;
        CurrentSubPhase = PlayerSubPhase.Moving;
        ShowMovementRange(pc, maxRangeOverride: GetWithdrawMoveRangeSquares(pc));
        CombatUI.SetActionButtonsVisible(false);
        CombatUI.SetTurnIndicator($"{pc.Stats.CharacterName} - Withdraw: select destination (double move, first square avoids AoO)");
        CombatUI?.ShowCombatLog($"↩ {pc.Stats.CharacterName} begins Withdraw (full-round, up to {GetWithdrawMoveRangeSquares(pc) * 5} ft). First square is protected from attacks of opportunity.");
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

        if (character.HasCondition(CombatConditionType.HideousLaughter))
            return "Cannot stand while laughing uncontrollably";

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

        Vector2Int oldPos = pc.GridPosition;
        ConsumeMoveAction(pc);

        if (_movementService != null)
            yield return StartCoroutine(_movementService.ExecuteMovement(pc, crawlPath, PlayerMoveSecondsPerStep, markAsMoved: true));
        else
            yield return StartCoroutine(pc.MoveAlongPath(crawlPath, PlayerMoveSecondsPerStep, markAsMoved: true));

        bool interruptedByIncapacitation = false;
        if (provokedAoOs.Count > 0)
        {
            CombatUI?.ShowCombatLog("Crawling provokes attacks of opportunity!");
            foreach (var aooInfo in provokedAoOs)
            {
                CharacterController threatener = aooInfo != null ? aooInfo.Threatener : null;
                if (threatener == null || threatener.Stats == null || threatener.Stats.IsDead)
                    continue;

                CombatResult aooResult = _movementService != null
                    ? _movementService.TriggerAoO(threatener, pc)
                    : ThreatSystem.ExecuteAoO(threatener, pc);
                if (aooResult == null)
                    continue;

                CombatUI?.ShowCombatLog($"⚔ AoO (crawling): {aooResult.GetDetailedSummary()}");
                UpdateAllStatsUI();

                if (aooResult.Hit && aooResult.TotalDamage > 0)
                    CheckConcentrationOnDamage(pc, aooResult.TotalDamage);

                if (pc.IsUnconscious || pc.Stats.IsDead)
                {
                    interruptedByIncapacitation = true;
                    break;
                }

                yield return new WaitForSeconds(0.8f);
            }
        }

        RefreshFlankedConditions();
        UpdateAllStatsUI();
        InvalidatePreviewThreats();

        if (interruptedByIncapacitation)
        {
            CombatUI?.ShowCombatLog($"⛔ {pc.Stats.CharacterName}'s crawl is interrupted by incapacitation.");
            EndActivePCTurn();
            yield break;
        }

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

        ClearPendingNaturalAttackSelection();
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

    private void SetPendingNaturalAttackSelection(int naturalAttackSequenceIndex, string naturalAttackLabel)
    {
        _pendingNaturalAttackSequenceIndex = Mathf.Max(0, naturalAttackSequenceIndex);
        _pendingNaturalAttackLabel = naturalAttackLabel;
    }

    private void ClearPendingNaturalAttackSelection()
    {
        _pendingNaturalAttackSequenceIndex = -1;
        _pendingNaturalAttackLabel = null;
    }

    private bool HasPendingNaturalAttackSelection()
    {
        return _pendingNaturalAttackSequenceIndex >= 0;
    }

    public void OnNaturalAttackButtonPressed(int naturalAttackSequenceIndex, string naturalAttackLabel)
    {
        CharacterController pc = ActivePC;
        if (pc == null)
            return;

        if (RedirectPinnedCharacterToGrappleMenu(pc, "attacks"))
            return;

        if (IsActionBlockedByTurnedCondition(pc, "attacks"))
        {
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        if (pc.Stats == null || !pc.Stats.HasNaturalAttacks || pc.GetEquippedMainWeapon() != null)
        {
            string pcName = pc.Stats != null ? pc.Stats.CharacterName : "Character";
            CombatUI?.ShowCombatLog($"⚠ {pcName} cannot use a natural-weapon attack option right now.");
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        if (_weaponAttacksCommittedThisTurn <= 0)
        {
            if (pc.Actions == null || !pc.Actions.HasStandardAction)
            {
                CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} has no standard action available for a natural attack.");
                CombatUI?.UpdateActionButtons(pc);
                return;
            }
        }
        else if (!_attackSequenceConsumesFullRound)
        {
            if (pc.Actions == null || !pc.Actions.HasMoveAction)
            {
                CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot continue natural attacks after moving.");
                CombatUI?.UpdateActionButtons(pc);
                return;
            }
        }

        if (!HasRemainingNaturalAttacks(pc))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} has no natural attacks remaining this turn.");
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        string resolvedLabel = string.IsNullOrWhiteSpace(naturalAttackLabel) ? "Natural attack" : naturalAttackLabel;
        int resolvedSequenceIndex = ResolveNextAvailableNaturalAttackSequenceIndex(pc, naturalAttackSequenceIndex, resolvedLabel);
        if (resolvedSequenceIndex < 0)
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} has no {resolvedLabel} attack remaining this turn.");
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        _pendingDefensiveAttackSelection = false;
        pc.SetFightingDefensively(false);
        EndAttackSequence();
        SetPendingNaturalAttackSelection(resolvedSequenceIndex, resolvedLabel);

        _pendingAttackMode = PendingAttackMode.Single;
        _currentAttackType = AttackType.Melee;
        CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
        ShowAttackTargets(pc);
        CombatUI?.SetTurnIndicator($"ATTACK ({resolvedLabel}): Click an enemy to attack!");
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

        ClearPendingNaturalAttackSelection();
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

        string mainType = (mainWeapon != null && (mainWeapon.IsLightWeapon || mainWeapon.WeaponSize == WeaponSizeCategory.Light)) ? SpellNames.LIGHT : "normal";
        string offType = lightOffHand ? SpellNames.LIGHT : "normal";

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

        ClearPendingNaturalAttackSelection();
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

        ClearPendingNaturalAttackSelection();
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
        if (actor == null
            || !_isInAttackSequence
            || _attackingCharacter != actor
            || !HasMoreAttacksAvailable())
            return false;

        if (_weaponAttacksCommittedThisTurn >= 1 && !_attackSequenceConsumesFullRound)
            return actor.Actions != null && actor.Actions.HasMoveAction;

        return true;
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

    private bool TryEnterProgressiveFullAttackStage(CharacterController attacker, string attemptedActionLabel)
    {
        if (attacker == null || attacker.Actions == null)
            return false;

        // First committed weapon attack only spends Standard action.
        if (_weaponAttacksCommittedThisTurn <= 0)
            return true;

        // Already in full-attack stage this turn.
        if (_attackSequenceConsumesFullRound)
            return true;

        if (!attacker.Actions.HasMoveAction)
        {
            string actionLabel = string.IsNullOrWhiteSpace(attemptedActionLabel) ? "another attack" : attemptedActionLabel;
            CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} cannot continue attacking: {actionLabel} would require consuming the remaining move action.");
            return false;
        }

        attacker.Actions.UseMoveAction();
        _attackSequenceConsumesFullRound = true;
        CombatUI?.ShowCombatLog($"↻ {attacker.Stats.CharacterName} commits to a full attack and spends their move action.");
        return true;
    }

    private void RegisterWeaponAttackCommitted(CharacterController attacker)
    {
        if (attacker == null)
            return;

        _weaponAttacksCommittedThisTurn = Mathf.Max(0, _weaponAttacksCommittedThisTurn) + 1;

        if (_weaponAttacksCommittedThisTurn >= 2)
            _attackSequenceConsumesFullRound = true;
    }

    private int GetTotalNaturalAttackCount(CharacterController attacker)
    {
        if (attacker == null || attacker.Stats == null)
            return 0;

        List<NaturalAttackDefinition> naturalAttacks = attacker.Stats.GetValidNaturalAttacks();
        int total = 0;
        for (int i = 0; i < naturalAttacks.Count; i++)
            total += Mathf.Max(1, naturalAttacks[i].Count);

        return total;
    }

    private bool HasRemainingNaturalAttacks(CharacterController attacker)
    {
        int totalNaturalAttacks = GetTotalNaturalAttackCount(attacker);
        return totalNaturalAttacks > 0 && _usedNaturalAttackSequenceIndices.Count < totalNaturalAttacks;
    }

    private static bool AreSameNaturalAttackName(string a, string b)
    {
        string lhs = string.IsNullOrWhiteSpace(a) ? string.Empty : a.Trim();
        string rhs = string.IsNullOrWhiteSpace(b) ? string.Empty : b.Trim();
        return string.Equals(lhs, rhs, StringComparison.OrdinalIgnoreCase);
    }

    private int ResolveNextAvailableNaturalAttackSequenceIndex(CharacterController attacker, int preferredSequenceIndex, string preferredLabel)
    {
        if (attacker == null || attacker.Stats == null)
            return -1;

        List<NaturalAttackDefinition> naturalAttacks = attacker.Stats.GetValidNaturalAttacks();
        if (naturalAttacks.Count <= 0)
            return -1;

        int globalIndex = 0;
        int firstUnused = -1;
        int preferredByLabel = -1;

        for (int i = 0; i < naturalAttacks.Count; i++)
        {
            NaturalAttackDefinition natural = naturalAttacks[i];
            int count = Mathf.Max(1, natural.Count);
            string naturalName = string.IsNullOrWhiteSpace(natural.Name) ? "Natural attack" : natural.Name;

            for (int repeat = 0; repeat < count; repeat++)
            {
                int index = globalIndex++;
                if (_usedNaturalAttackSequenceIndices.Contains(index))
                    continue;

                if (firstUnused < 0)
                    firstUnused = index;

                if (preferredByLabel < 0 && AreSameNaturalAttackName(naturalName, preferredLabel))
                    preferredByLabel = index;

                if (index == preferredSequenceIndex)
                    return index;
            }
        }

        if (preferredByLabel >= 0)
            return preferredByLabel;

        return firstUnused;
    }

    private bool CanAttack(CharacterController actor)
    {
        if (actor == null)
            return false;

        if (!actor.CanAttack())
            return false;

        if (_isInAttackSequence && _attackingCharacter == actor)
        {
            if (!HasMoreAttacksAvailable())
                return false;

            if (_weaponAttacksCommittedThisTurn >= 1 && !_attackSequenceConsumesFullRound)
                return actor.Actions != null && actor.Actions.HasMoveAction;

            return true;
        }

        if (actor.Actions == null)
            return false;

        // Off-hand-first flow: if off-hand already consumed the standard action,
        // allow starting the main-hand iterative sequence by consuming move as full-round conversion.
        if (_offHandAttackUsedThisTurn && _offHandAttackAvailableThisTurn && actor == ActivePC && actor.Actions.HasMoveAction)
            return true;

        return actor.Actions.HasStandardAction;
    }

    public bool CanUsePrimaryAttackOption(CharacterController actor)
    {
        return CanAttack(actor);
    }

    private bool CanThrowWeapon(CharacterController actor)
    {
        if (actor == null)
            return false;

        if (!actor.CanAttack())
            return false;

        ItemData weapon = actor.GetEquippedWeapon();
        if (weapon == null || !weapon.IsThrown || weapon.RangeIncrement <= 0)
            return false;

        if (_isInAttackSequence)
        {
            if (_attackingCharacter != actor || !HasMoreAttacksAvailable())
                return false;

            if (_weaponAttacksCommittedThisTurn >= 1 && !_attackSequenceConsumesFullRound)
                return actor.Actions != null && actor.Actions.HasMoveAction;

            return true;
        }

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

    public bool IsNaturalAttackSequenceIndexUsed(CharacterController actor, int sequenceIndex)
    {
        return actor != null
            && actor == ActivePC
            && sequenceIndex >= 0
            && _usedNaturalAttackSequenceIndices.Contains(sequenceIndex);
    }

    public bool CanUseNaturalAttackOption(CharacterController actor)
    {
        if (actor == null || actor != ActivePC || actor.Stats == null || !actor.Stats.HasNaturalAttacks || actor.GetEquippedMainWeapon() != null)
            return false;

        if (!HasRemainingNaturalAttacks(actor))
            return false;

        if (_weaponAttacksCommittedThisTurn <= 0)
            return actor.Actions != null && actor.Actions.HasStandardAction;

        if (_attackSequenceConsumesFullRound)
            return true;

        return actor.Actions != null && actor.Actions.HasMoveAction;
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

        if (_isInAttackSequence)
        {
            if (_attackingCharacter != actor)
            {
                Debug.Log($"[OffHand][CanUse] Denied: attack sequence belongs to {( _attackingCharacter != null && _attackingCharacter.Stats != null ? _attackingCharacter.Stats.CharacterName : "<none>")}, not {actor.Stats?.CharacterName ?? "<null>"}.");
                return false;
            }

            if (_weaponAttacksCommittedThisTurn >= 1 && !_attackSequenceConsumesFullRound && !actor.Actions.HasMoveAction)
            {
                Debug.Log($"[OffHand][CanUse] Denied: second attack would require move action but {actor.Stats?.CharacterName ?? "<null>"} has no move action.");
                return false;
            }

            Debug.Log($"[OffHand][CanUse] Allowed in active sequence for {actor.Stats?.CharacterName ?? "<null>"}.");
            return true;
        }

        if (_weaponAttacksCommittedThisTurn <= 0)
        {
            bool canUseStandard = actor.Actions.HasStandardAction;
            Debug.Log($"[OffHand][CanUse] Outside sequence, first attack requires standard. allowed={canUseStandard}");
            return canUseStandard;
        }

        if (_attackSequenceConsumesFullRound)
            return true;

        bool canUseMoveForSecondAttack = actor.Actions.HasMoveAction;
        Debug.Log($"[OffHand][CanUse] Outside sequence, additional attack requires move. allowed={canUseMoveForSecondAttack}");
        return canUseMoveForSecondAttack;
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

    private static bool UsesInnateNaturalAttackSequence(CharacterController attacker, AttackType attackType, ItemData equippedWeapon)
    {
        return attacker != null
            && attackType == AttackType.Melee
            && equippedWeapon == null
            && attacker.Stats != null
            && attacker.Stats.HasNaturalAttacks;
    }

    private static bool TryGetNaturalAttackAtSequenceIndex(CharacterController attacker, int attackIndex, out NaturalAttackDefinition attack)
    {
        attack = null;
        if (attacker == null || attacker.Stats == null || attackIndex < 0)
            return false;

        List<NaturalAttackDefinition> naturalAttacks = attacker.Stats.GetValidNaturalAttacks();
        int currentIndex = 0;
        for (int naturalIndex = 0; naturalIndex < naturalAttacks.Count; naturalIndex++)
        {
            NaturalAttackDefinition naturalAttack = naturalAttacks[naturalIndex];
            int count = Mathf.Max(1, naturalAttack.Count);
            for (int i = 0; i < count; i++)
            {
                if (currentIndex == attackIndex)
                {
                    attack = naturalAttack;
                    return true;
                }

                currentIndex++;
            }
        }

        return false;
    }

    private int GetAttackSequenceBaseAttackBonus(CharacterController attacker, AttackType attackType, int attackIndex)
    {
        if (UsesInnateNaturalAttackSequence(attacker, attackType, attacker != null ? attacker.GetEquippedMainWeapon() : null)
            && TryGetNaturalAttackAtSequenceIndex(attacker, attackIndex, out NaturalAttackDefinition naturalAttack))
        {
            return attacker.Stats.GetNaturalAttackBonus(naturalAttack);
        }

        return attacker != null ? attacker.GetIterativeAttackBAB(attackIndex) : 0;
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

        bool usingInnateNaturalAttacks = UsesInnateNaturalAttackSequence(attacker, attackType, _equippedWeapon);
        _totalAttackBudget = usingInnateNaturalAttacks
            ? Mathf.Max(1, attacker.Stats.GetTotalNaturalAttackCount())
            : Mathf.Max(1, attacker.GetIterativeAttackCount());
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

        if (_weaponAttacksCommittedThisTurn >= 1 && !_attackSequenceConsumesFullRound)
        {
            if (!TryEnterProgressiveFullAttackStage(attacker, "a second attack"))
            {
                EndAttackSequence();
                ShowActionChoices();
                return;
            }
        }

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
        int baseBab = GetAttackSequenceBaseAttackBonus(attacker, attackType, _totalAttacksUsed);
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
        Debug.Log($"[Attack][Sequence] Final state before teardown: attacksUsed={_totalAttacksUsed}/{_totalAttackBudget}, offHandUsed={_offHandAttackUsedThisTurn}, offHandAvailable={_offHandAttackAvailableThisTurn}, phase={CurrentPhase}");

        // Nuclear safety net: if attack flow ended and every enemy is dead, force victory handling.
        if (CurrentPhase == TurnPhase.PCTurn || CurrentPhase == TurnPhase.NPCTurn)
        {
            int aliveEnemies = GetAliveNPCCount();
            int totalEnemyCombatants = 0;
            if (NPCs != null)
            {
                for (int i = 0; i < NPCs.Count; i++)
                {
                    CharacterController npc = NPCs[i];
                    if (!IsActiveCombatant(npc))
                        continue;
                    if (npc.Team != CharacterTeam.Enemy)
                        continue;
                    totalEnemyCombatants++;
                }
            }

            Debug.Log($"[Attack][ForceCheck] EndAttackSequence enemy status | aliveEnemies={aliveEnemies} | totalEnemyCombatants={totalEnemyCombatants} | snapshot={BuildEnemyStatusSnapshot()}");
            if (aliveEnemies == 0 && totalEnemyCombatants > 0)
            {
                Debug.Log("[Attack][ForceCheck] FORCING victory detection from EndAttackSequence");
                CheckCombatVictory("EndAttackSequence.ForceCheck");
            }
            else
            {
                Debug.Log("[Attack][ForceCheck] No force trigger needed.");
            }
        }
        else
        {
            Debug.Log($"[Attack][ForceCheck] Skipped force check due to phase={CurrentPhase}");
        }

        _totalAttacksUsed = 0;
        _totalAttackBudget = 0;
        _isInAttackSequence = false;
        _attackingCharacter = null;
        _equippedWeapon = null;

        // Keep progressive full-attack commitment across single-attack UI refreshes.
        // This must persist after the second committed attack so remaining natural attacks
        // can still be selected even though no move action remains.
        if (_weaponAttacksCommittedThisTurn <= 0)
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
        _weaponAttacksCommittedThisTurn = 0;
        _usedNaturalAttackSequenceIndices.Clear();
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
        bool hasCoupDeGraceAvailable = CanUseCoupDeGraceAttackOption(actor);
        return actor.Actions.HasStandardAction
            || actor.Actions.HasFullRoundAction
            || CanUseImprovedFeintAsMove(actor)
            || hasGrappleAttackAvailable
            || hasBullRushAttackAvailable
            || hasTripAttackAvailable
            || hasDisarmAttackAvailable
            || hasSunderAttackAvailable
            || hasCoupDeGraceAvailable;
    }



    public void OnSpecialAttackButtonPressed()
    {
        CharacterController pc = ActivePC;
        bool canOpen = pc != null && CanOpenSpecialAttackMenu(pc);
        Debug.Log($"[GameManager][SpecialAttack] ButtonPressed actor={(pc != null && pc.Stats != null ? pc.Stats.CharacterName : "<null>")} canOpen={canOpen} phase={CurrentPhase} subPhase={CurrentSubPhase} std={(pc != null ? pc.Actions.HasStandardAction : false)} full={(pc != null ? pc.Actions.HasFullRoundAction : false)} grappleAvailable={(pc != null ? CanUseGrappleAttackOption(pc) : false)} bullRushAvailable={(pc != null ? CanUseBullRushAttackOption(pc) : false)} tripAvailable={(pc != null ? CanUseTripAttackOption(pc) : false)} disarmAvailable={(pc != null ? CanUseDisarmAttackOption(pc) : false)} sunderAvailable={(pc != null ? CanUseSunderAttackOption(pc) : false)} coupDeGraceAvailable={(pc != null ? CanUseCoupDeGraceAttackOption(pc) : false)}");
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
        bool hasCoupDeGraceAttackAvailable = CanUseCoupDeGraceAttackOption(pc);

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
                                : (type == SpecialAttackType.CoupDeGrace
                                    ? hasCoupDeGraceAttackAvailable
                                    : (type == SpecialAttackType.BullRushCharge
                                        ? pc.Actions.HasFullRoundAction
                                        : pc.Actions.HasStandardAction)))))));

        Debug.Log($"[GameManager][SpecialAttack] Selected type={type} actor={pc.Stats.CharacterName} allowed={hasAction} phase={CurrentPhase} subPhase={CurrentSubPhase} std={pc.Actions.HasStandardAction} full={pc.Actions.HasFullRoundAction} grappleAvailable={hasGrappleAttackAvailable} bullRushAvailable={hasBullRushAttackAvailable} tripAvailable={hasTripAttackAvailable} mainDisarmAvailable={hasMainHandDisarmAttackAvailable} offHandDisarmAvailable={hasOffHandDisarmAttackAvailable} mainSunderAvailable={hasMainHandSunderAttackAvailable} offHandSunderAvailable={hasOffHandSunderAttackAvailable} coupDeGraceAvailable={hasCoupDeGraceAttackAvailable} requestedOffHand={useOffHandDisarm}");

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
                                    : (type == SpecialAttackType.CoupDeGrace
                                        ? "Need a full-round action, an adjacent helpless enemy, and a melee attack option"
                                        : (type == SpecialAttackType.BullRushCharge
                                            ? "Need a full-round action and valid charge movement"
                                            : "Need a standard action")))))));
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

        // Slow prevents full-round actions (PHB p.280)
        if (pc.HasActiveSlowEffect)
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} is Slowed and cannot take full-round actions!");
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

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

        ClearPendingNaturalAttackSelection();
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

        ClearPendingNaturalAttackSelection();
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

        ClearPendingNaturalAttackSelection();
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
        {
            Debug.Log("[CombatEnd] EndCurrentTurn early return because phase is already CombatOver.");
            return;
        }

        Debug.Log($"[TurnFlow] EndCurrentTurn | isPlayerTurn={IsPlayerTurn} | phase={CurrentPhase} | subPhase={CurrentSubPhase}");
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
                           (pc.Stats.IsExhaustedState ? "exhausted" :
                               (pc.Stats.IsFatiguedState ? "fatigued" : "no rages left today"));
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

    public bool HasActiveDisguiseSelf(CharacterController character)
    {
        if (character == null)
            return false;

        StatusEffectManager statusMgr = character.GetComponent<StatusEffectManager>();
        return statusMgr != null && statusMgr.HasEffect(SpellNames.DISGUISE_SELF);
    }

    public bool HasActiveExpeditiousRetreat(CharacterController character)
    {
        if (character == null)
            return false;

        StatusEffectManager statusMgr = character.GetComponent<StatusEffectManager>();
        return statusMgr != null && statusMgr.HasEffect(SpellNames.EXPEDITIOUS_RETREAT);
    }

    public bool HasActiveJump(CharacterController character)
    {
        if (character == null)
            return false;

        StatusEffectManager statusMgr = character.GetComponent<StatusEffectManager>();
        return statusMgr != null && statusMgr.HasEffect(SpellNames.JUMP);
    }

    public bool HasActiveInvisibility(CharacterController character)
    {
        if (character == null)
            return false;

        // Check the runtime invisibility effect first (supports all sources)
        if (character.HasActiveInvisibilityEffect)
            return true;

        // Fallback: check StatusEffectManager for spell-based invisibility
        StatusEffectManager statusMgr = character.GetComponent<StatusEffectManager>();
        return statusMgr != null && (statusMgr.HasEffect(SpellNames.INVISIBILITY)
            || statusMgr.HasEffect("greater_invisibility"));
    }

    public bool HasActiveSeeInvisibility(CharacterController character)
    {
        if (character == null)
            return false;

        StatusEffectManager statusMgr = character.GetComponent<StatusEffectManager>();
        return statusMgr != null && statusMgr.HasEffect(SpellNames.SEE_INVISIBLE);
    }

    public void OnDismissExpeditiousRetreatButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null)
            return;

        StatusEffectManager statusMgr = pc.GetComponent<StatusEffectManager>();
        if (statusMgr == null || !statusMgr.HasEffect(SpellNames.EXPEDITIOUS_RETREAT))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} has no active Expeditious Retreat to dismiss.");
            return;
        }

        statusMgr.RemoveEffectsBySpellId(SpellNames.EXPEDITIOUS_RETREAT);
        ExpeditiousRetreatEffectData removed = pc.RemoveExpeditiousRetreatEffect();
        int removedBonus = removed != null ? Mathf.Max(0, removed.SpeedBonusFeet) : 30;
        CombatUI?.ShowCombatLog($"<color=#88CCFF>💨 {pc.Stats.CharacterName} dismisses Expeditious Retreat (speed -{removedBonus} ft).</color>");
        UpdateAllStatsUI();
        ShowActionChoices();
    }

    public void OnDismissJumpButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || !pc.Actions.HasStandardAction)
            return;

        StatusEffectManager statusMgr = pc.GetComponent<StatusEffectManager>();
        if (statusMgr == null || !statusMgr.HasEffect(SpellNames.JUMP))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} has no active Jump spell to dismiss.");
            return;
        }

        pc.CommitStandardAction();
        statusMgr.RemoveEffectsBySpellId(SpellNames.JUMP);

        CombatUI?.ShowCombatLog($"<color=#88CCFF>🦘 {pc.Stats.CharacterName} dismisses Jump.</color>");
        UpdateAllStatsUI();
        ShowActionChoices();
    }

    public void OnDismissInvisibilityButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || !pc.Actions.HasStandardAction)
            return;

        if (!pc.HasActiveInvisibilityEffect)
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} has no active invisibility effect to dismiss.");
            return;
        }

        // Check if the effect is dismissible
        if (pc.ActiveInvisibilityEffect != null && !pc.ActiveInvisibilityEffect.IsDismissible)
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName}'s invisibility cannot be dismissed.");
            return;
        }

        string sourceSpellId = pc.ActiveInvisibilityEffect?.SourceSpellId;
        string sourceName = pc.ActiveInvisibilityEffect?.SourceName ?? "Invisibility";

        StatusEffectManager statusMgr = pc.GetComponent<StatusEffectManager>();
        if (statusMgr != null && !string.IsNullOrEmpty(sourceSpellId))
            statusMgr.RemoveEffectsBySpellId(sourceSpellId);
        else if (statusMgr != null)
            statusMgr.RemoveEffectsBySpellId(SpellNames.INVISIBILITY);

        pc.CommitStandardAction();
        pc.ClearInvisibilityEffect();

        CombatUI?.ShowCombatLog($"<color=#88CCFF>👁 {pc.Stats.CharacterName} dismisses {sourceName}.</color>");
        UpdateAllStatsUI();
        ShowActionChoices();
    }

    public void OnDismissSeeInvisibilityButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null)
            return;

        StatusEffectManager statusMgr = pc.GetComponent<StatusEffectManager>();
        if (statusMgr == null || !statusMgr.HasEffect(SpellNames.SEE_INVISIBLE))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} has no active See Invisible spell to dismiss.");
            return;
        }

        statusMgr.RemoveEffectsBySpellId(SpellNames.SEE_INVISIBLE);
        pc.ClearSeeInvisibilityEffect();

        CombatUI?.ShowCombatLog($"<color=#88CCFF>👁 {pc.Stats.CharacterName} dismisses See Invisible.</color>");
        UpdateAllStatsUI();
        ShowActionChoices();
    }

    public void OnDismissDisguiseSelfButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || !pc.Actions.HasStandardAction)
            return;

        StatusEffectManager statusMgr = pc.GetComponent<StatusEffectManager>();
        if (statusMgr == null || !statusMgr.HasEffect(SpellNames.DISGUISE_SELF))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} has no active Disguise Self to dismiss.");
            return;
        }

        pc.CommitStandardAction();
        statusMgr.RemoveEffectsBySpellId(SpellNames.DISGUISE_SELF);

        CombatUI?.ShowCombatLog($"<color=#88CCFF>🎭 {pc.Stats.CharacterName} dismisses Disguise Self and appears as {pc.DisplayedRace}.</color>");
        UpdateAllStatsUI();
        ShowActionChoices();
    }

    /// <summary>Called when a spell is chosen from the spell selection panel (with optional metamagic).</summary>
    private void OnSpellSelectedWithMetamagic(SpellData spell, MetamagicData metamagic)
    {
        CharacterController pc = ActivePC;
        if (pc == null) { ShowActionChoices(); return; }

        _pendingSpell = spell;
        _pendingMetamagic = metamagic;
        _pendingSpellFromHeldCharge = false;
        _pendingAnimateRopeItem = null;
        _pendingResistEnergyType = null;
        _pendingProtectionFromEnergyType = null;
        _pendingFireShieldIsWarm = null;
        _pendingMagicWeaponItem = null;
        _pendingKeenEdgeItem = null;
        _pendingKeenEdgeIsAmmo = false;
        _pendingGreaterMagicWeaponItem = null;
        _pendingDisguiseSelfRace = null;
        _pendingSummonSelection = null;
        _pendingSummonListLevel = 0;
        _pendingSummonCountInfo = null;
        _pendingSummonSwarmNpcId = null;

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

        if (TryShowGreaseCastModePrompt(pc))
            return;

        if (TryHandleAnimateRopeComponentSelection(pc))
            return;

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
                    ResetPendingGreaseCastMode();
                    ShowActionChoices();
                });
            return;
        }

        BeginPendingSpellTargeting(pc);
    }

    private static bool IsAnimateRopeSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.ANIMATE_ROPE, StringComparison.Ordinal);
    }

    private bool TryHandleAnimateRopeComponentSelection(CharacterController caster)
    {
        if (!IsAnimateRopeSpell(_pendingSpell))
        {
            _pendingAnimateRopeItem = null;
            _pendingResistEnergyType = null;
            _pendingProtectionFromEnergyType = null;
            return false;
        }

        if (caster == null || caster.Stats == null)
            return false;

        if (!TryGetAnimateRopeInventoryOptions(caster, out List<ItemData> ropeOptions))
        {
            CombatUI?.ShowCombatLog("⚠ You need rope to cast this spell.");
            _pendingSpell = null;
            _pendingMetamagic = null;
            _pendingSpellFromHeldCharge = false;
            _pendingAnimateRopeItem = null;
            _pendingResistEnergyType = null;
            _pendingProtectionFromEnergyType = null;
            ShowActionChoices();
            return true;
        }

        if (ropeOptions.Count == 1)
        {
            _pendingAnimateRopeItem = ropeOptions[0];
            return false;
        }

        List<string> labels = new List<string>(ropeOptions.Count);
        for (int i = 0; i < ropeOptions.Count; i++)
        {
            ItemData rope = ropeOptions[i];
            int breakDc = GetRopeBreakDC(rope);
            labels.Add($"{rope.Name} (Break DC {breakDc})");
        }

        if (CombatUI != null)
        {
            CombatUI.ShowPickUpItemSelection(
                actorName: caster.Stats.CharacterName,
                itemOptions: labels,
                onSelect: selectedIndex =>
                {
                    if (selectedIndex < 0 || selectedIndex >= ropeOptions.Count)
                    {
                        _pendingSpell = null;
                        _pendingMetamagic = null;
                        _pendingSpellFromHeldCharge = false;
                        _pendingAnimateRopeItem = null;
                        _pendingResistEnergyType = null;
                        _pendingProtectionFromEnergyType = null;
                        ShowActionChoices();
                        return;
                    }

                    _pendingAnimateRopeItem = ropeOptions[selectedIndex];
                    BeginPendingSpellTargeting(caster);
                },
                onCancel: () =>
                {
                    _pendingSpell = null;
                    _pendingMetamagic = null;
                    _pendingSpellFromHeldCharge = false;
                    _pendingAnimateRopeItem = null;
                    _pendingResistEnergyType = null;
                    _pendingProtectionFromEnergyType = null;
                    ShowActionChoices();
                },
                titleOverride: "Animate Rope - Select Rope",
                bodyOverride: "Choose which rope to animate.",
                optionButtonColorOverride: new Color(0.24f, 0.34f, 0.56f, 1f));
        }

        return true;
    }

    private bool TryGetAnimateRopeInventoryOptions(CharacterController caster, out List<ItemData> ropeItems)
    {
        ropeItems = new List<ItemData>();
        if (caster == null)
            return false;

        InventoryComponent inventoryComponent = caster.GetComponent<InventoryComponent>();
        Inventory inventory = inventoryComponent != null ? inventoryComponent.CharacterInventory : null;
        if (inventory == null || inventory.GeneralSlots == null)
            return false;

        for (int i = 0; i < inventory.GeneralSlots.Length; i++)
        {
            ItemData item = inventory.GeneralSlots[i];
            if (!IsRopeItem(item))
                continue;

            ropeItems.Add(item);
        }

        return ropeItems.Count > 0;
    }

    private static bool IsRopeItem(ItemData item)
    {
        if (item == null)
            return false;

        if (item is RopeItemData)
            return true;

        string id = item.Id ?? string.Empty;
        return string.Equals(id, ItemIDs.ROPE, StringComparison.OrdinalIgnoreCase)
               || string.Equals(id, ItemIDs.ROPE_HEMP, StringComparison.OrdinalIgnoreCase)
               || string.Equals(id, ItemIDs.ROPE_SILK, StringComparison.OrdinalIgnoreCase);
    }

    private static int GetRopeBreakDC(ItemData item)
    {
        if (item is RopeItemData rope && rope.BreakDC > 0)
            return rope.BreakDC;

        string id = item != null ? (item.Id ?? string.Empty) : string.Empty;
        if (string.Equals(id, ItemIDs.ROPE_SILK, StringComparison.OrdinalIgnoreCase))
            return 23;

        return 24;
    }

    private ItemData ConsumePendingAnimateRopeItem(CharacterController caster)
    {
        if (caster == null)
            return null;

        InventoryComponent inventoryComponent = caster.GetComponent<InventoryComponent>();
        Inventory inventory = inventoryComponent != null ? inventoryComponent.CharacterInventory : null;
        if (inventory == null)
            return null;

        ItemData selected = _pendingAnimateRopeItem;
        if (selected == null)
        {
            if (!TryGetAnimateRopeInventoryOptions(caster, out List<ItemData> options) || options.Count == 0)
                return null;
            selected = options[0];
        }

        if (!inventory.RemoveItem(selected))
            return null;

        _pendingAnimateRopeItem = null;
        _pendingResistEnergyType = null;
        _pendingProtectionFromEnergyType = null;
        return selected;
    }

    private static bool IsMagicWeaponSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.MAGIC_WEAPON, StringComparison.Ordinal);
    }

    private bool TryHandleMagicWeaponWeaponSelection(CharacterController caster, CharacterController target)
    {
        if (!IsMagicWeaponSpell(_pendingSpell))
        {
            _pendingMagicWeaponItem = null;
            return false;
        }

        if (target == null || target.Stats == null)
            return false;

        if (_pendingMagicWeaponItem != null)
            return false;

        if (!TryGetMagicWeaponInventoryOptions(target, out List<ItemData> weaponOptions, out List<string> weaponLabels))
        {
            CombatUI?.ShowCombatLog($"⚠ {target.Stats.CharacterName} has no weapon in inventory to enchant with Magic Weapon.");
            _pendingSpell = null;
            _pendingMetamagic = null;
            _pendingSpellFromHeldCharge = false;
            _pendingAnimateRopeItem = null;
            _pendingResistEnergyType = null;
            _pendingProtectionFromEnergyType = null;
            _pendingMagicWeaponItem = null;
            ShowActionChoices();
            return true;
        }

        if (weaponOptions.Count == 1)
        {
            _pendingMagicWeaponItem = weaponOptions[0];
            return false;
        }

        CombatUI?.ShowPickUpItemSelection(
            actorName: caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Caster",
            itemOptions: weaponLabels,
            onSelect: selectedIndex =>
            {
                if (selectedIndex < 0 || selectedIndex >= weaponOptions.Count)
                {
                    _pendingSpell = null;
                    _pendingMetamagic = null;
                    _pendingSpellFromHeldCharge = false;
                    _pendingAnimateRopeItem = null;
                    _pendingResistEnergyType = null;
                    _pendingProtectionFromEnergyType = null;
                    _pendingMagicWeaponItem = null;
                    ShowActionChoices();
                    return;
                }

                _pendingMagicWeaponItem = weaponOptions[selectedIndex];
                PerformSpellCast(caster, target);
            },
            onCancel: () =>
            {
                _pendingSpell = null;
                _pendingMetamagic = null;
                _pendingSpellFromHeldCharge = false;
                _pendingAnimateRopeItem = null;
                _pendingResistEnergyType = null;
                _pendingProtectionFromEnergyType = null;
                _pendingMagicWeaponItem = null;
                ShowActionChoices();
            },
            titleOverride: "Magic Weapon - Select Weapon",
            bodyOverride: $"Choose which weapon from {target.Stats.CharacterName}'s inventory to enchant.",
            optionButtonColorOverride: new Color(0.24f, 0.34f, 0.56f, 1f));

        return true;
    }

    private static bool TryGetMagicWeaponInventoryOptions(CharacterController target, out List<ItemData> weapons, out List<string> labels)
    {
        weapons = new List<ItemData>();
        labels = new List<string>();

        if (target == null)
            return false;

        InventoryComponent inventoryComponent = target.GetComponent<InventoryComponent>();
        Inventory inventory = inventoryComponent != null ? inventoryComponent.CharacterInventory : null;
        if (inventory == null)
            return false;

        // Equipped locations.
        TryAddMagicWeaponOption(inventory.RightHandSlot, "Right Hand", weapons, labels);
        TryAddMagicWeaponOption(inventory.LeftHandSlot, "Left Hand", weapons, labels);
        TryAddMagicWeaponOption(inventory.HandsSlot, "Hands", weapons, labels);

        // Full backpack/general inventory.
        if (inventory.GeneralSlots != null)
        {
            for (int i = 0; i < inventory.GeneralSlots.Length; i++)
            {
                ItemData item = inventory.GeneralSlots[i];
                if (item == null)
                    continue;

                TryAddMagicWeaponOption(item, $"Backpack Slot {i + 1}", weapons, labels);
            }
        }

        return weapons.Count > 0;
    }

    private static void TryAddMagicWeaponOption(ItemData item, string locationLabel, List<ItemData> weapons, List<string> labels)
    {
        if (item == null || !item.IsWeapon || weapons == null || labels == null)
            return;

        for (int i = 0; i < weapons.Count; i++)
        {
            if (ReferenceEquals(weapons[i], item))
                return;
        }

        int currentEnhancement = item.GetHighestWeaponEnhancementBonus();
        string enhancementText = currentEnhancement > 0 ? $"+{currentEnhancement}" : "+0";
        weapons.Add(item);
        labels.Add($"{item.Name} ({locationLabel}, current enhancement {enhancementText})");
    }

    private bool TryApplyMagicWeaponToPendingItem(CharacterController caster, CharacterController target, SpellData spell)
    {
        if (!IsMagicWeaponSpell(spell))
            return false;

        ItemData weapon = _pendingMagicWeaponItem;
        _pendingMagicWeaponItem = null;

        if (weapon == null)
        {
            CombatUI?.ShowCombatLog("⚠ Magic Weapon failed: no weapon selected.");
            return true;
        }

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
        int rounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name;

        var effect = new ItemSpellEffect(spell.SpellId, spell.Name, casterName, casterLevel, rounds)
        {
            BonusType = BonusType.Enhancement,
            EnhancementBonusAttack = 1,
            EnhancementBonusDamage = 1,
            CountsAsMagicForBypass = true
        };

        weapon.AddOrReplaceItemSpellEffect(effect);

        int effectiveAttackBonus = weapon.GetEnhancementAttackBonus();
        int effectiveDamageBonus = weapon.GetEnhancementDamageBonus();
        bool magicBypass = weapon.IsMagicForBypass;
        string recipientName = target != null && target.Stats != null ? target.Stats.CharacterName : "target";

        CombatUI?.ShowCombatLog($"<color=#88FFEE>✨ {spell.Name} enchants {recipientName}'s {weapon.Name}: +1 enhancement for {effect.GetDurationDisplayString()}.</color>");
        CombatUI?.ShowCombatLog($"<color=#88FFEE>🗡 {weapon.Name} effective enhancement now +{Mathf.Max(effectiveAttackBonus, effectiveDamageBonus)} (attack +{effectiveAttackBonus}, damage +{effectiveDamageBonus}); counts as magic: {(magicBypass ? "yes" : "no")}.</color>");
        return true;
    }

    private bool TryResolveAnimateRopeSpellEffect(CharacterController caster, CharacterController target, SpellData spell, SpellResult result)
    {
        if (!IsAnimateRopeSpell(spell) || caster == null || target == null || result == null)
            return false;

        ItemData ropeItem = ConsumePendingAnimateRopeItem(caster);
        if (ropeItem == null)
        {
            CombatUI?.ShowCombatLog($"⚠ {caster.Stats.CharacterName} has no rope available to animate.");
            return true;
        }

        int breakDc = GetRopeBreakDC(ropeItem);

        if (result.RequiredAttackRoll && !result.AttackHit)
        {
            DropAnimateRopeItemAt(target.GridPosition, ropeItem);
            CombatUI?.ShowCombatLog($"🪢 Animate Rope misses. {ropeItem.Name} lands on the ground.");
            return true;
        }

        if (result.RequiredSave && result.SaveSucceeded)
        {
            DropAnimateRopeItemAt(target.GridPosition, ropeItem);
            CombatUI?.ShowCombatLog($"🪢 {target.Stats.CharacterName} dodges the rope with a successful Reflex save. {ropeItem.Name} falls to the ground.");
            return true;
        }

        int casterLevel = caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));

        var conditionData = new AnimateRopeEntangledConditionData
        {
            Caster = caster,
            Target = target,
            RopeItem = ropeItem,
            RopeBreakDC = breakDc,
            LastKnownTargetPosition = target.GridPosition,
            RopeDestroyed = false,
            RopeDroppedToGround = false,
            SourceSpellId = spell.SpellId,
            SourceSpellName = spell.Name
        };

        if (_conditionService != null)
        {
            _conditionService.ApplyCondition(
                target,
                CombatConditionType.Entangled,
                durationRounds,
                source: caster,
                data: conditionData,
                sourceNameOverride: spell.Name,
                sourceCategory: "Spell",
                sourceId: spell.SpellId);
        }
        else
        {
            target.ApplyCondition(CombatConditionType.Entangled, durationRounds, spell.Name);
        }

        CombatUI?.ShowCombatLog($"🪢 {target.Stats.CharacterName} is entangled by {ropeItem.Name}! Movement is reduced to half speed, -2 attack, -4 DEX. Escape with STR vs DC {breakDc} or Escape Artist DC 20.");
        return true;
    }

    private bool TryGetAnimateRopeEntangledCondition(CharacterController actor, out ConditionService.ActiveCondition condition, out AnimateRopeEntangledConditionData data)
    {
        condition = null;
        data = null;

        if (actor == null)
            return false;

        List<ConditionService.ActiveCondition> active = GetActiveConditions(actor);
        for (int i = 0; i < active.Count; i++)
        {
            ConditionService.ActiveCondition candidate = active[i];
            if (candidate == null)
                continue;
            if (ConditionRules.Normalize(candidate.Type) != CombatConditionType.Entangled)
                continue;

            AnimateRopeEntangledConditionData ropeData = candidate.Data as AnimateRopeEntangledConditionData;
            bool sourceMatches = string.Equals(candidate.SourceId, SpellNames.ANIMATE_ROPE, StringComparison.Ordinal)
                || (ropeData != null && string.Equals(ropeData.SourceSpellId, SpellNames.ANIMATE_ROPE, StringComparison.Ordinal));
            if (!sourceMatches)
                continue;

            condition = candidate;
            data = ropeData;
            return true;
        }

        return false;
    }

    private bool TryGetWebEntangledCondition(CharacterController actor, out ConditionService.ActiveCondition condition, out WebEntangledConditionData data)
    {
        condition = null;
        data = null;

        if (actor == null)
            return false;

        List<ConditionService.ActiveCondition> active = GetActiveConditions(actor);
        for (int i = 0; i < active.Count; i++)
        {
            ConditionService.ActiveCondition candidate = active[i];
            if (candidate == null)
                continue;
            if (ConditionRules.Normalize(candidate.Type) != CombatConditionType.Entangled)
                continue;

            WebEntangledConditionData webData = candidate.Data as WebEntangledConditionData;
            bool sourceMatches = string.Equals(candidate.SourceId, SpellNames.WEB, StringComparison.Ordinal)
                || (webData != null && string.Equals(webData.SourceSpellId, SpellNames.WEB, StringComparison.Ordinal));
            if (!sourceMatches)
                continue;

            condition = candidate;
            data = webData;
            return true;
        }

        return false;
    }

    public bool IsEntangledByWeb(CharacterController actor)
    {
        return TryGetWebEntangledCondition(actor, out _, out _);
    }

    public void ApplyWebEntangledCondition(CharacterController caster, CharacterController target, int durationRounds)
    {
        if (target == null || target.Stats == null || target.Stats.IsDead)
            return;

        var data = new WebEntangledConditionData
        {
            Caster = caster,
            Target = target,
            EscapeDC = WebAreaEffect.EscapeDc,
            SourceSpellId = SpellNames.WEB,
            SourceSpellName = "Web"
        };

        int rounds = Mathf.Max(1, durationRounds);
        if (_conditionService != null)
        {
            _conditionService.ApplyCondition(
                target,
                CombatConditionType.Entangled,
                rounds,
                source: caster,
                data: data,
                sourceNameOverride: "Web",
                sourceCategory: "Spell",
                sourceId: SpellNames.WEB);
        }
        else
        {
            target.ApplyCondition(CombatConditionType.Entangled, rounds, "Web");
        }
    }

    public void RemoveWebEntangledConditionsFromArea(WebAreaEffect sourceArea)
    {
        if (sourceArea == null)
            return;

        List<CharacterController> all = GetAllCharacters();
        List<WebAreaEffect> activeWebs = AreaEffectManager.Instance.GetEffectsOfType<WebAreaEffect>();
        for (int i = 0; i < all.Count; i++)
        {
            CharacterController character = all[i];
            if (character == null || character.Stats == null)
                continue;

            if (!TryGetWebEntangledCondition(character, out _, out _))
                continue;

            // Only clear this entangled state if no remaining web area still covers the creature.
            bool stillCoveredByAnyWeb = false;
            for (int j = 0; j < activeWebs.Count; j++)
            {
                WebAreaEffect web = activeWebs[j];
                if (web == null || web == sourceArea)
                    continue;

                if (web.IsCellInArea(character.GridPosition))
                {
                    stillCoveredByAnyWeb = true;
                    break;
                }
            }

            if (stillCoveredByAnyWeb)
                continue;

            character.RemoveCondition(CombatConditionType.Entangled);
            CombatUI?.ShowCombatLog($"🕸 {character.Stats.CharacterName} is freed as the web dissipates.");
        }
    }

    private void DropAnimateRopeItemAt(Vector2Int position, ItemData ropeItem)
    {
        if (ropeItem == null || Grid == null)
            return;

        SquareCell cell = Grid.GetCell(position);
        if (cell == null)
            return;

        cell.AddGroundItem(ropeItem);
    }

    public bool CanUseAnimateRopeEscapeAction(CharacterController actor, out string reason)
    {
        reason = string.Empty;
        if (actor == null || actor.Stats == null)
        {
            reason = "No active actor.";
            return false;
        }

        bool hasAnimateRope = TryGetAnimateRopeEntangledCondition(actor, out _, out _);
        bool hasWeb = TryGetWebEntangledCondition(actor, out _, out _);
        if (!hasAnimateRope && !hasWeb)
        {
            reason = string.Empty;
            return false;
        }

        if (actor.Actions == null || !actor.Actions.HasStandardAction)
        {
            reason = "Standard action already used.";
            return false;
        }

        return true;
    }

    public string GetEntangleEscapeActionLabel(CharacterController actor)
    {
        if (TryGetWebEntangledCondition(actor, out _, out _))
            return "Web: Escape";
        if (TryGetAnimateRopeEntangledCondition(actor, out _, out _))
            return "Animate Rope: Escape";
        return "Entangle: Escape";
    }

    private bool TryHandleAnimateRopeEscapeAction(CharacterController actor, bool consumeStandardAction)
    {
        if (actor == null || actor.Stats == null)
            return false;

        bool fromAnimateRope = TryGetAnimateRopeEntangledCondition(actor, out _, out AnimateRopeEntangledConditionData ropeData);
        bool fromWeb = TryGetWebEntangledCondition(actor, out _, out WebEntangledConditionData webData);
        if (!fromAnimateRope && !fromWeb)
            return false;

        if (consumeStandardAction && (actor.Actions == null || !actor.Actions.HasStandardAction || !actor.CommitStandardAction()))
        {
            CombatUI?.ShowCombatLog($"⚠ {actor.Stats.CharacterName} has no standard action available to attempt escape.");
            return true;
        }

        int strBonus = actor.Stats != null ? actor.Stats.STRMod : 0;
        int escapeArtistBonus = actor.Stats != null ? actor.Stats.GetSkillBonus("Escape Artist") : 0;
        int breakDc = fromAnimateRope
            ? (ropeData != null && ropeData.RopeBreakDC > 0 ? ropeData.RopeBreakDC : 24)
            : Mathf.Max(1, webData != null && webData.EscapeDC > 0 ? webData.EscapeDC : WebAreaEffect.EscapeDc);

        bool useStrength = (strBonus - breakDc) >= (escapeArtistBonus - 20);
        int dc = useStrength ? breakDc : 20;
        int bonus = useStrength ? strBonus : escapeArtistBonus;
        string checkLabel = useStrength ? "Strength" : "Escape Artist";
        string sourceLabel = fromAnimateRope ? "Animate Rope" : "Web";

        int roll = DiceService.D20("Escape check");
        int total = roll + bonus;
        bool success = total >= dc;

        string icon = fromAnimateRope ? "🪢" : "🕸";
        CombatUI?.ShowCombatLog($"{icon} {actor.Stats.CharacterName} attempts to escape {sourceLabel} ({checkLabel}): d20 {roll} + {bonus} = {total} vs DC {dc}.");

        if (success)
        {
            actor.RemoveCondition(CombatConditionType.Entangled);
            if (fromAnimateRope && ropeData != null)
            {
                DropAnimateRopeItemAt(actor.GridPosition, ropeData.RopeItem);
                ropeData.RopeDroppedToGround = true;
                ropeData.LastKnownTargetPosition = actor.GridPosition;
            }

            CombatUI?.ShowCombatLog($"✅ {actor.Stats.CharacterName} escapes {sourceLabel}.");
        }
        else
        {
            CombatUI?.ShowCombatLog($"❌ {actor.Stats.CharacterName} fails to escape {sourceLabel}.");
        }

        UpdateAllStatsUI();
        return true;
    }

    public bool TryExecuteAnimateRopeEscapeForNpc(CharacterController npc)
    {
        if (npc == null || npc.Stats == null)
            return false;

        return TryHandleAnimateRopeEscapeAction(npc, consumeStandardAction: true);
    }

    private bool TryHandleAnimateRopeConditionExpiry(CharacterController character, ConditionService.ActiveCondition condition)
    {
        if (character == null || condition == null)
            return false;

        if (ConditionRules.Normalize(condition.Type) != CombatConditionType.Entangled)
            return false;

        AnimateRopeEntangledConditionData data = condition.Data as AnimateRopeEntangledConditionData;
        bool isAnimateRope = string.Equals(condition.SourceId, SpellNames.ANIMATE_ROPE, StringComparison.Ordinal)
            || (data != null && string.Equals(data.SourceSpellId, SpellNames.ANIMATE_ROPE, StringComparison.Ordinal));
        if (!isAnimateRope)
            return false;

        Vector2Int dropPos = character.GridPosition;
        if (data != null)
        {
            if (data.RopeDroppedToGround)
                return true;

            data.LastKnownTargetPosition = character.GridPosition;
            dropPos = data.LastKnownTargetPosition;
            DropAnimateRopeItemAt(dropPos, data.RopeItem);
            data.RopeDroppedToGround = true;
        }

        CombatUI?.ShowCombatLog($"⏱ Animate Rope ends on {character.Stats.CharacterName}. The rope falls to the ground.");
        return true;
    }

    private bool TryHandleWebConditionExpiry(CharacterController character, ConditionService.ActiveCondition condition)
    {
        if (character == null || condition == null)
            return false;

        if (ConditionRules.Normalize(condition.Type) != CombatConditionType.Entangled)
            return false;

        WebEntangledConditionData data = condition.Data as WebEntangledConditionData;
        bool isWeb = string.Equals(condition.SourceId, SpellNames.WEB, StringComparison.Ordinal)
            || (data != null && string.Equals(data.SourceSpellId, SpellNames.WEB, StringComparison.Ordinal));
        if (!isWeb)
            return false;

        CombatUI?.ShowCombatLog($"⏱ {character.Stats.CharacterName} is no longer entangled by web.");
        return true;
    }

    public void NotifyFireDamageAtPosition(Vector2Int position, string sourceName)
    {
        if (string.Equals(sourceName, "Web Flames", StringComparison.OrdinalIgnoreCase))
            return;

        List<WebAreaEffect> webEffects = AreaEffectManager.Instance.GetEffectsOfType<WebAreaEffect>();
        for (int i = 0; i < webEffects.Count; i++)
        {
            WebAreaEffect web = webEffects[i];
            if (web == null || web.IsBurning || !web.IsCellInArea(position))
                continue;

            web.Ignite(string.IsNullOrWhiteSpace(sourceName) ? "fire" : sourceName);
        }
    }

    private bool IsSummonMonsterSpell(SpellData spell)
    {
        if (spell == null || string.IsNullOrWhiteSpace(spell.SpellId))
            return false;

        return SummonMonsterLists.GetSummonMonsterSpellLevel(spell.SpellId) > 0;
    }

    private bool IsSummonSwarmSpell(SpellData spell)
    {
        return spell != null
               && string.Equals(spell.SpellId, SpellNames.SUMMON_SWARM, StringComparison.Ordinal);
    }

    private List<SummonMonsterOption> GetSummonOptionsForSpell(SpellData spell, CharacterController caster, int listLevel)
    {
        if (spell == null || caster == null || caster.Stats == null || listLevel <= 0)
            return new List<SummonMonsterOption>();

        return SummonMonsterLists.GetFilteredOptionsForListLevel(listLevel, caster.Stats);
    }

    private string BuildSummonCountRangeText(SpellData spell, int selectedListLevel)
    {
        int spellLevel = spell != null ? SummonMonsterLists.GetSummonMonsterSpellLevel(spell.SpellId) : 0;
        SummonCreatureCountInfo info = SummonMonsterLists.GetCreatureCountInfo(spellLevel, selectedListLevel);
        return info != null ? info.RangeText : "1 creature";
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

    public CharacterController GetSummonCasterForAI(CharacterController summon)
    {
        ActiveSummonInstance data = GetActiveSummon(summon);
        return data != null ? data.Caster : null;
    }

    public void SetSummonCommand(CharacterController summon, SummonCommand command)
    {
        if (summon == null || command == null)
            return;

        var active = GetActiveSummon(summon);
        if (active == null)
            return;

        if (string.Equals(active.SourceSpellId, SpellNames.SUMMON_SWARM, StringComparison.Ordinal))
        {
            CombatUI?.ShowCombatLog("⚠ Summon Swarm cannot be controlled.");
            return;
        }

        if (!summon.IsControllable)
        {
            CombatUI?.ShowCombatLog("⚠ This summoned ally is AI-controlled and cannot receive direct commands.");
            return;
        }

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

    private void RegisterScenarioSummonedCreature(CharacterController summon, CharacterController caster, int durationRounds, string sourceSpellId)
    {
        if (summon == null)
            return;

        ActiveSummonInstance existing = GetActiveSummon(summon);
        if (existing != null)
            _activeSummons.Remove(existing);

        CharacterController resolvedCaster = caster ?? summon;
        int clampedDuration = Mathf.Max(1, durationRounds);

        var scenarioSummon = new ActiveSummonInstance
        {
            Controller = summon,
            Caster = resolvedCaster,
            RemainingRounds = clampedDuration,
            TotalDurationRounds = clampedDuration,
            SourceSpellId = string.IsNullOrWhiteSpace(sourceSpellId) ? "scenario_setup" : sourceSpellId,
            IsAlliedToPCs = summon.Team == CharacterTeam.Player,
            SmiteUsed = false,
            CurrentCommand = SummonCommand.AttackNearest()
        };

        _activeSummons.Add(scenarioSummon);

        if (summon.Team == CharacterTeam.Player)
            _summonedAllies.Add(summon);
        else
            _summonedEnemies.Add(summon);
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
        if (!IsPlayerTurn || summon == null || summon.Team != CharacterTeam.Player)
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

    // ═══════════════════════════════════════════════════════════════════
    //  EMANATION AREA EFFECT MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Register any emanation effect. Replaces an existing emanation of the same type
    /// centered on the same creature (one emanation per type per creature).
    /// </summary>
    /// <param name="emanation">The emanation to register.</param>
    public void RegisterEmanation(EmanationEffectData emanation)
    {
        if (emanation == null)
            return;

        // For mobile emanations, require a valid center creature
        if (!emanation.CenterPosition.HasValue && emanation.CenterCreature == null)
            return;

        // Remove any existing emanation of the same concrete type on the same center
        var emanationType = emanation.GetType();
        for (int i = _activeEmanations.Count - 1; i >= 0; i--)
        {
            var existing = _activeEmanations[i];
            if (existing.GetType() == emanationType && existing.CenterCreature == emanation.CenterCreature)
            {
                _activeEmanations.RemoveAt(i);
            }
        }

        _activeEmanations.Add(emanation);
        Debug.Log($"[Emanation] {emanation.GetEffectName()} registered on {emanation.CenterCreature?.Stats?.CharacterName ?? "fixed position"}, CL {emanation.CasterLevel}, {emanation.RemainingRounds} rounds");
    }

    /// <summary>
    /// Unregister all emanations centered on a specific creature.
    /// Called on death, dispel, etc.
    /// </summary>
    /// <param name="centerCreature">The creature whose emanations should be removed.</param>
    public void UnregisterEmanation(CharacterController centerCreature)
    {
        if (centerCreature == null) return;
        for (int i = _activeEmanations.Count - 1; i >= 0; i--)
        {
            if (_activeEmanations[i].CenterCreature == centerCreature)
            {
                Debug.Log($"[Emanation] Removed {_activeEmanations[i].GetEffectName()} from {centerCreature.Stats?.CharacterName}");
                _activeEmanations.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Tick all active emanations (call each round). Removes expired or invalid ones.
    /// Also refreshes Invisibility Sphere membership: creatures who have moved
    /// outside the emanation lose invisibility immediately at round boundaries.
    /// </summary>
    public void TickEmanations()
    {
        // Refresh Invisibility Sphere membership BEFORE ticking so that
        // creatures who have left the emanation become visible during the
        // round end / round start visual update pass.
        RefreshInvisibilitySpheres();

        for (int i = _activeEmanations.Count - 1; i >= 0; i--)
        {
            var em = _activeEmanations[i];
            if (em.ShouldRemove() || !em.Tick())
            {
                // For Invisibility Sphere, ensure all initially-affected creatures
                // are made visible before we drop the emanation.
                if (em is InvisibilitySphereEffect sphere)
                    sphere.EndForAll("duration expired");

                Debug.Log($"[Emanation] Expired: {em.GetEffectName()}");
                _activeEmanations.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Get all active emanations (read-only copy for tests/queries).
    /// </summary>
    public List<EmanationEffectData> GetActiveEmanations()
    {
        return new List<EmanationEffectData>(_activeEmanations);
    }

    /// <summary>
    /// Get all active emanations of a specific type.
    /// </summary>
    /// <typeparam name="T">The emanation subclass type to filter by.</typeparam>
    /// <returns>List of active emanations of the requested type.</returns>
    public List<T> GetActiveEmanationsOfType<T>() where T : EmanationEffectData
    {
        var result = new List<T>();
        for (int i = 0; i < _activeEmanations.Count; i++)
        {
            if (_activeEmanations[i] is T typed)
                result.Add(typed);
        }
        return result;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  MAGIC CIRCLE-SPECIFIC CONVENIENCE METHODS
    //  (Delegate to generic emanation system, filter by MagicCircleEffectData)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Register a Magic Circle emanation. Convenience wrapper around RegisterEmanation.
    /// </summary>
    public void RegisterMagicCircle(MagicCircleEffectData data)
    {
        RegisterEmanation(data);
    }

    /// <summary>
    /// Remove a Magic Circle effect (on death, dispel, etc.).
    /// Removes only MagicCircleEffectData emanations centered on the creature.
    /// </summary>
    public void RemoveMagicCircle(CharacterController centerCreature)
    {
        if (centerCreature == null) return;
        for (int i = _activeEmanations.Count - 1; i >= 0; i--)
        {
            if (_activeEmanations[i] is MagicCircleEffectData mc && mc.CenterCreature == centerCreature)
            {
                Debug.Log($"[MagicCircle] Removed {mc.GetSpellName()} from {centerCreature.Stats?.CharacterName}");
                _activeEmanations.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Get Magic Circle protection benefits for a creature against an attacker's alignment.
    /// Checks if the creature is within any active Magic Circle emanation that wards against the given alignment.
    /// Returns the best (highest) benefits found.
    /// </summary>
    public AlignmentProtectionBenefits GetMagicCircleBenefitsAgainst(CharacterController creature, Alignment sourceAlignment)
    {
        var benefits = new AlignmentProtectionBenefits();
        if (creature == null || _activeEmanations.Count == 0)
            return benefits;

        for (int i = 0; i < _activeEmanations.Count; i++)
        {
            if (!(_activeEmanations[i] is MagicCircleEffectData mc))
                continue;

            if (mc.CenterCreature == null || mc.CenterCreature.IsDead)
                continue;

            // Check if creature is in the area
            if (!mc.IsCreatureInArea(creature))
                continue;

            // Check if attacker matches the warded alignment
            if (!mc.IsAttackerOfWardedAlignment(sourceAlignment))
                continue;

            benefits.HasMatch = true;
            benefits.DeflectionAcBonus = Mathf.Max(benefits.DeflectionAcBonus, 2);
            benefits.ResistanceSaveBonus = Mathf.Max(benefits.ResistanceSaveBonus, 2);
            benefits.BlocksMentalControl = true;
            benefits.BlocksSummonedContact = true;

            if (string.IsNullOrEmpty(benefits.SourceSpellName))
                benefits.SourceSpellName = mc.GetSpellName();
        }

        return benefits;
    }

    /// <summary>
    /// Check if a creature is within any active Magic Circle that protects against the given alignment.
    /// Used for mental control suppression checks.
    /// </summary>
    public bool IsProtectedByMagicCircle(CharacterController creature, AlignmentProtectionType wardedAlignment)
    {
        if (creature == null || _activeEmanations.Count == 0)
            return false;

        for (int i = 0; i < _activeEmanations.Count; i++)
        {
            if (!(_activeEmanations[i] is MagicCircleEffectData mc))
                continue;

            if (mc.CenterCreature == null || mc.CenterCreature.IsDead)
                continue;

            if (mc.WardedAlignment != wardedAlignment)
                continue;

            if (mc.IsCreatureInArea(creature))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Check if a creature is within any active Magic Circle.
    /// </summary>
    public bool IsInAnyMagicCircle(CharacterController creature)
    {
        if (creature == null || _activeEmanations.Count == 0)
            return false;

        for (int i = 0; i < _activeEmanations.Count; i++)
        {
            if (!(_activeEmanations[i] is MagicCircleEffectData mc))
                continue;

            if (mc.CenterCreature != null && !mc.CenterCreature.IsDead && mc.IsCreatureInArea(creature))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Get all active Magic Circle effects (read-only access for tests/queries).
    /// </summary>
    public List<MagicCircleEffectData> GetActiveMagicCircles()
    {
        return GetActiveEmanationsOfType<MagicCircleEffectData>();
    }

    /// <summary>
    /// Get the Magic Circle effect centered on a specific creature, or null.
    /// </summary>
    public MagicCircleEffectData GetMagicCircleOnCreature(CharacterController creature)
    {
        if (creature == null) return null;
        for (int i = 0; i < _activeEmanations.Count; i++)
        {
            if (_activeEmanations[i] is MagicCircleEffectData mc && mc.CenterCreature == creature)
                return mc;
        }
        return null;
    }

    private void ShowSummonCreatureSelectionMenu(CharacterController caster, SpellData spell)
    {
        if (caster == null || spell == null)
        {
            ShowActionChoices();
            return;
        }

        int spellLevel = SummonMonsterLists.GetSummonMonsterSpellLevel(spell.SpellId);
        if (spellLevel <= 0)
        {
            CombatUI?.ShowCombatLog($"{spell.Name} is not recognized as a Summon Monster spell.");
            ShowActionChoices();
            return;
        }

        List<int> availableListLevels = SummonMonsterLists.GetAvailableListLevelsForSpell(spell.SpellId);
        if (availableListLevels == null || availableListLevels.Count == 0)
        {
            CombatUI?.ShowCombatLog($"No summon list levels available for {spell.Name}.");
            _pendingSpell = null;
            _pendingMetamagic = null;
            _pendingSpellFromHeldCharge = false;
            _pendingSummonSelection = null;
            _pendingSummonListLevel = 0;
            _pendingSummonCountInfo = null;
            _pendingSummonSwarmNpcId = null;
            ShowActionChoices();
            return;
        }

        Dictionary<int, List<SummonMonsterOption>> optionsByLevel = new Dictionary<int, List<SummonMonsterOption>>();
        for (int i = 0; i < availableListLevels.Count; i++)
        {
            int listLevel = availableListLevels[i];
            optionsByLevel[listLevel] = GetSummonOptionsForSpell(spell, caster, listLevel);
        }

        bool hasAnySummonOption = optionsByLevel.Values.Any(v => v != null && v.Count > 0);
        if (!hasAnySummonOption)
        {
            CombatUI?.ShowCombatLog($"No valid summon options for {spell.Name} ({caster.Stats.CharacterAlignment}).");
            _pendingSpell = null;
            _pendingMetamagic = null;
            _pendingSpellFromHeldCharge = false;
            _pendingSummonSelection = null;
            _pendingSummonListLevel = 0;
            _pendingSummonCountInfo = null;
            _pendingSummonSwarmNpcId = null;
            ShowActionChoices();
            return;
        }

        int defaultListLevel = Mathf.Clamp(spellLevel, 1, availableListLevels[availableListLevels.Count - 1]);

        CombatUI?.ShowSummonCreatureSelection(
            spellName: spell.Name,
            spellLevel: spellLevel,
            availableListLevels: availableListLevels,
            defaultListLevel: defaultListLevel,
            getCreatureOptionsForLevel: selectedLevel =>
            {
                if (!optionsByLevel.TryGetValue(selectedLevel, out List<SummonMonsterOption> levelOptions) || levelOptions == null)
                    return new List<string>();

                return levelOptions.ConvertAll(o => o.BuildUiLabel());
            },
            getCountRangeTextForLevel: selectedLevel => BuildSummonCountRangeText(spell, selectedLevel),
            restrictionHint: SummonMonsterLists.GetSummonRestrictionHint(caster.Stats),
            onSelect: (selectedLevel, selectedIndex) =>
            {
                if (!optionsByLevel.TryGetValue(selectedLevel, out List<SummonMonsterOption> levelOptions)
                    || levelOptions == null
                    || selectedIndex < 0
                    || selectedIndex >= levelOptions.Count)
                {
                    ShowActionChoices();
                    return;
                }

                _pendingSummonListLevel = selectedLevel;
                _pendingSummonSelection = levelOptions[selectedIndex];
                _pendingSummonCountInfo = SummonMonsterLists.GetCreatureCountInfo(spellLevel, selectedLevel);
                _pendingAttackMode = PendingAttackMode.CastSpell;
                CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
                ShowSummonPlacementTargets(caster, spell);
            },
            onCancel: () =>
            {
                _pendingSpell = null;
                _pendingMetamagic = null;
                _pendingSpellFromHeldCharge = false;
                _pendingSummonSelection = null;
                _pendingSummonListLevel = 0;
                _pendingSummonCountInfo = null;
                _pendingSummonSwarmNpcId = null;
                ShowActionChoices();
            });
    }

    private int CalculateSummonSwarmRangeFeet(int casterLevel)
    {
        return 25 + (5 * Mathf.Max(0, casterLevel / 2));
    }

    private List<string> BuildSummonSwarmChoiceNpcIds()
    {
        string[] preferred = { "bat_swarm", "rat_swarm", "spider_swarm" };
        var validIds = new List<string>();

        for (int i = 0; i < preferred.Length; i++)
        {
            if (NPCDatabase.Get(preferred[i]) != null)
                validIds.Add(preferred[i]);
        }

        return validIds;
    }

    private List<string> BuildSummonSwarmChoiceLabels()
    {
        List<string> npcIds = BuildSummonSwarmChoiceNpcIds();
        var labels = new List<string>();

        for (int i = 0; i < npcIds.Count; i++)
        {
            NPCDefinition def = NPCDatabase.Get(npcIds[i]);
            if (def == null)
                continue;

            string special = "Swarm";
            if (def.SwarmTraits != null)
            {
                if (def.SwarmTraits.HasWounding)
                    special = "Wounding";
                else if (def.SwarmTraits.HasDisease)
                    special = "Disease";
                else if (def.SwarmTraits.HasPoison)
                    special = "Poison";
            }

            int approxAc = 10 + def.NaturalArmorBonus + CharacterStats.GetModifier(def.DEX) + def.SizeCategory.GetAttackAndAcModifier();
            labels.Add($"{def.Name}\n  {def.HitDice} HD, {Mathf.Max(1, def.BaseHitDieHP)} HP, AC {approxAc}\n  Damage: {def.SwarmTraits?.SwarmDamageDice ?? "1d6"}\n  Special: {special}");
        }

        return labels;
    }

    private void ShowSummonSwarmSelectionMenu(CharacterController caster, SpellData spell)
    {
        if (caster == null || spell == null || CombatUI == null)
        {
            ShowActionChoices();
            return;
        }

        List<string> ids = BuildSummonSwarmChoiceNpcIds();
        List<string> labels = BuildSummonSwarmChoiceLabels();
        if (ids.Count == 0 || labels.Count == 0)
        {
            CombatUI?.ShowCombatLog("No swarm creatures are available in the NPC database.");
            ShowActionChoices();
            return;
        }

        CombatUI.ShowPickUpItemSelection(
            actorName: caster.Stats != null ? caster.Stats.CharacterName : "Caster",
            itemOptions: labels,
            onSelect: idx =>
            {
                if (idx < 0 || idx >= ids.Count)
                {
                    ShowActionChoices();
                    return;
                }

                _pendingSummonSwarmNpcId = ids[idx];
                _pendingAttackMode = PendingAttackMode.CastSpell;
                CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
                ShowSummonSwarmPlacementTargets(caster, spell);
            },
            onCancel: () =>
            {
                _pendingSpell = null;
                _pendingMetamagic = null;
                _pendingSpellFromHeldCharge = false;
                _pendingSummonSwarmNpcId = null;
                ShowActionChoices();
            },
            titleOverride: "SUMMON SWARM - Select Swarm Type",
            bodyOverride: "⚠ WARNING: Swarm is UNCONTROLLED!\nWill attack nearest creature (friend or foe).\nRequires concentration; after concentration ends, it lasts 2 more rounds.");
    }

    private void ShowSummonSwarmPlacementTargets(CharacterController caster, SpellData spell)
    {
        if (caster == null || spell == null || string.IsNullOrWhiteSpace(_pendingSummonSwarmNpcId))
        {
            ShowActionChoices();
            return;
        }

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        int rangeSquares = Mathf.Max(1, spell.GetRangeSquaresForCasterLevel(caster?.Stats?.Level ?? 1));
        List<SquareCell> cells = Grid.GetCellsInRange(caster.GridPosition, rangeSquares);

        foreach (SquareCell cell in cells)
        {
            if (cell == null)
                continue;

            int dist = SquareGridUtils.GetDistance(caster.GridPosition, cell.Coords);
            if (dist > rangeSquares)
                continue;

            cell.SetHighlight(HighlightType.SpellTarget);
            _highlightedCells.Add(cell);
        }

        HighlightCharacterFootprint(caster, HighlightType.Selected);

        int feet = CalculateSummonSwarmRangeFeet(caster != null && caster.Stats != null ? caster.Stats.Level : 1);
        CombatUI.SetTurnIndicator($"✦ {spell.Name}: place swarm ({_pendingSummonSwarmNpcId}) | Range: {feet} ft | Occupied squares allowed | Right-click to cancel");
    }

    private void ShowSummonPlacementTargets(CharacterController caster, SpellData spell)
    {
        if (caster == null || spell == null || _pendingSummonSelection == null)
        {
            ShowActionChoices();
            return;
        }

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        int range = spell.GetRangeSquaresForCasterLevel(caster?.Stats?.Level ?? 0);
        if (range <= 0) range = 1;
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

        string listLevelLabel = _pendingSummonListLevel > 0
            ? SummonMonsterLists.ToRomanLevel(_pendingSummonListLevel)
            : "?";
        string countPreview = _pendingSummonCountInfo != null
            ? _pendingSummonCountInfo.RangeText
            : BuildSummonCountRangeText(spell, Mathf.Max(1, _pendingSummonListLevel));

        CombatUI.SetTurnIndicator($"✦ {spell.Name} (List {listLevelLabel}): Place {_pendingSummonSelection.BuildUiLabel()} | {countPreview} | Range: {range * 5} ft | Right-click to cancel");
    }


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

    private void ShowMovementRange(CharacterController pc, int maxRangeOverride = -1)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        if (_movementService == null || pc == null)
            return;

        int resolvedMaxRange = maxRangeOverride >= 0 ? maxRangeOverride : GetCurrentMoveRangeSquares(pc);
        List<SquareCell> moveCells = _movementService.CalculateMovementRange(pc, resolvedMaxRange);
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
    public AoOPathResult FindPath(CharacterController mover, Vector2Int destination, bool avoidThreats = true, int? maxRangeOverride = null, bool allowThroughAllies = true, bool allowThroughEnemies = false, bool suppressFirstSquareAoO = false)
        => _movementService != null
            ? _movementService.FindPath(mover, destination, avoidThreats, maxRangeOverride, allowThroughAllies, allowThroughEnemies, suppressFirstSquareAoO)
            : new AoOPathResult();
    public AoOPathResult FindPath(CharacterController mover, Vector2Int destination, HashSet<Vector2Int> threatenedSquares, int maxRangeOverride, bool allowThroughAllies = true, bool allowThroughEnemies = false, bool suppressFirstSquareAoO = false)
        => _movementService != null
            ? _movementService.FindPath(mover, destination, threatenedSquares, maxRangeOverride, allowThroughAllies, allowThroughEnemies, suppressFirstSquareAoO)
            : new AoOPathResult();
    public List<AoOThreatInfo> CheckForAoO(CharacterController mover, List<Vector2Int> path, bool suppressFirstSquareAoO = false)
        => _movementService != null ? _movementService.CheckForAoO(mover, path, suppressFirstSquareAoO) : new List<AoOThreatInfo>();
    public CombatResult TriggerAoO(CharacterController threatener, CharacterController target)
        => _movementService != null ? _movementService.TriggerAoO(threatener, target) : ThreatSystem.ExecuteAoO(threatener, target);
    public bool CanTake5FootStep(CharacterController character, Vector2Int destination)
        => _movementService != null && _movementService.CanTake5FootStep(character, destination);

    // Public delegation helpers for condition-aware systems.
    public void ApplyCondition(
        CharacterController target,
        CombatConditionType type,
        int rounds,
        CharacterController source = null,
        object data = null,
        bool expiresAtEndOfTurn = false,
        bool expiresAtStartOfTurn = false,
        string sourceNameOverride = null,
        string sourceCategory = "Unknown",
        string sourceId = null)
        => _conditionService?.ApplyCondition(target, type, rounds, source, data, expiresAtEndOfTurn, expiresAtStartOfTurn, sourceNameOverride, sourceCategory, sourceId);
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

    public int GetCurrentMoveRangeSquares(CharacterController target)
    {
        if (target == null || target.Stats == null)
            return 0;

        if (IsEntangledByWeb(target))
            return 0;

        return Mathf.Max(0, target.Stats.MoveRange);
    }

    public bool TryGetConfusedTurnDecisionForAI(CharacterController actor, out ConfusedBehaviorController.ConfusedTurnDecision decision)
    {
        _confusedBehaviorController ??= new ConfusedBehaviorController();
        return _confusedBehaviorController.TryRollDecision(this, actor, out decision);
    }

    public IEnumerator ExecuteConfusedTurnDecisionForAI(CharacterController actor, ConfusedBehaviorController.ConfusedTurnDecision decision)
    {
        _confusedBehaviorController ??= new ConfusedBehaviorController();
        yield return StartCoroutine(_confusedBehaviorController.ExecuteDecision(this, actor, decision));
    }

    public bool TryGetCharmedTurnDecisionForAI(CharacterController actor, out CharmedBehaviorController.CharmedTurnDecision decision)
    {
        _charmedBehaviorController ??= new CharmedBehaviorController();
        return _charmedBehaviorController.TryBuildDecision(this, actor, out decision);
    }

    public IEnumerator ExecuteCharmedTurnDecisionForAI(CharacterController actor, CharmedBehaviorController.CharmedTurnDecision decision)
    {
        _charmedBehaviorController ??= new CharmedBehaviorController();
        yield return StartCoroutine(_charmedBehaviorController.ExecuteDecision(this, actor, decision));
    }

    public bool TryGetFascinatedTurnDecisionForAI(CharacterController actor, out FascinatedBehaviorController.FascinatedTurnDecision decision)
    {
        _fascinatedBehaviorController ??= new FascinatedBehaviorController();
        return _fascinatedBehaviorController.TryBuildDecision(this, actor, out decision);
    }

    public bool TryGetFrightenedTurnDecisionForAI(CharacterController actor, out FrightenedBehaviorController.FrightenedTurnDecision decision)
    {
        _frightenedBehaviorController ??= new FrightenedBehaviorController();
        return _frightenedBehaviorController.TryBuildDecision(this, actor, out decision);
    }

    public IEnumerator ExecuteFrightenedTurnDecisionForAI(CharacterController actor, FrightenedBehaviorController.FrightenedTurnDecision decision)
    {
        _frightenedBehaviorController ??= new FrightenedBehaviorController();
        yield return StartCoroutine(_frightenedBehaviorController.ExecuteDecision(this, actor, decision));
    }

    // Public delegation helpers for AIService.
    public NPCAIBehavior GetNPCBehaviorForAI(CharacterController npc)
    {
        int npcIdx = NPCs.IndexOf(npc);
        return (npcIdx >= 0 && npcIdx < _npcAIBehaviors.Count)
            ? _npcAIBehaviors[npcIdx]
            : NPCAIBehavior.AggressiveMelee;
    }

    public void BeginNPCTurnForAI(CharacterController npc)
    {
        if (npc == null)
            return;

        _conditionService?.OnTurnStart(npc);
        ApplyMelfsAcidArrowTurnStartDamage(npc);
        npc.TickBombardierAcidSprayCooldown();
        npc.ApplyRegenerationAtTurnStart();
        npc.StartNewTurn();
        ProcessNPCRoundStartPerception(npc);
        PruneTurnUndeadTrackers();
        CheckTurnUndeadProximityBreakingForMover(npc);
    }

    private void ProcessNPCRoundStartPerception(CharacterController npc)
    {
        if (npc == null || npc.Stats == null || npc.Stats.IsDead)
            return;

        LastKnownPositionTracker tracker = npc.GetComponent<LastKnownPositionTracker>();
        if (tracker == null)
            tracker = npc.gameObject.AddComponent<LastKnownPositionTracker>();

        var visibleEnemies = new List<CharacterController>();
        var concealedTrackedEnemies = new List<CharacterController>();

        List<CharacterController> allCharacters = GetAllCharacters();
        bool incomingIsRangedAttack = npc.IsEquippedWeaponRanged();

        for (int i = 0; i < allCharacters.Count; i++)
        {
            CharacterController enemy = allCharacters[i];
            if (enemy == null || enemy == npc || enemy.Stats == null || enemy.Stats.IsDead)
                continue;
            if (!IsEnemyTeam(npc, enemy))
                continue;

            if (npc.CanSee(enemy, incomingIsRangedAttack))
                visibleEnemies.Add(enemy);
            else if (tracker.HasLastKnownPosition(enemy))
                concealedTrackedEnemies.Add(enemy);
        }

        tracker.UpdateVisibleCharacters(visibleEnemies);

        if (concealedTrackedEnemies.Count > 0)
        {
            string npcName = npc.Stats != null ? npc.Stats.CharacterName : npc.name;
            CombatUI?.ShowCombatLog($"{npcName} attempts to locate concealed targets:");
            tracker.AttemptListenChecks(concealedTrackedEnemies, this);
        }
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

    public IEnumerator ExecuteWithdrawMovementForAI(CharacterController mover, Vector2Int destination, float secondsPerStep)
        => MoveCharacterAlongComputedPathWithdraw(mover, destination, secondsPerStep);

    public bool TryNPCSpecialAttackIfBeneficialForAI(CharacterController npc, CharacterController target)
        => TryNPCSpecialAttackIfBeneficial(npc, target);

    public bool TryNPCSpecialAttackByTypeForAI(CharacterController npc, CharacterController target, SpecialAttackType attackType)
        => TryNPCSpecialAttackIfBeneficial(npc, target, attackType);

    public IEnumerator NPCPerformAttackForAI(CharacterController npc, CharacterController target)
        => NPCPerformAttack(npc, target);

    public bool TryNPCPerformSpellCastForAI(CharacterController npc, CharacterController target, SpellData spell)
        => TryNPCPerformSpellCast(npc, target, spell);

    public bool HasActiveShieldSpellForAI(CharacterController target)
        => HasActiveShieldSpell(target);

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

    public bool CanTakeFiveFootStepForAI(CharacterController npc)
        => CanTakeFiveFootStep(npc);

    public bool CanTakeFiveFootStepToForAI(CharacterController npc, Vector2Int destination)
    {
        if (npc == null || _movementService == null)
            return false;

        return _movementService.CanTake5FootStep(npc, destination);
    }

    public bool TryTakeFiveFootStepForAI(CharacterController npc, Vector2Int destination)
    {
        if (npc == null || Grid == null)
            return false;

        SquareCell destinationCell = Grid.GetCell(destination);
        if (destinationCell == null)
            return false;

        return ExecuteFiveFootStep(npc, destinationCell, returnToActionChoices: false);
    }

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
                        : (_isSelectingWithdraw
                            ? $"↩ {pc.Stats.CharacterName} cancels Withdraw."
                            : $"↩ {pc.Stats.CharacterName} cancels movement."));
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
            if (character.Team == pc.Team) continue;

            var threats = ThreatSystem.GetThreatenedSquares(character);
            _previewThreatenedSquares.UnionWith(threats);
        }

        _previewThreatsDirty = false;
        return _previewThreatenedSquares;
    }

    private void UpdatePathPreview()
    {
        if (_pathPreview == null)
            return;

        bool isMovementPreview = CurrentSubPhase == PlayerSubPhase.Moving && ActivePC != null;
        bool isFlamingSpherePreview = CurrentSubPhase == PlayerSubPhase.SelectingFlamingSphereTarget && _selectedFlamingSphereForControl != null;

        if (!isMovementPreview && !isFlamingSpherePreview)
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

        if (_inputService != null && _inputService.IsPointerOverUI())
        {
            if (_pathPreview.IsVisible) _pathPreview.HidePath();
            return;
        }

        Vector2 worldPoint = _inputService != null
            ? _inputService.GetMouseWorldPosition()
            : (Vector2)_mainCam.ScreenToWorldPoint(Input.mousePosition);
        Vector2Int gridCoord = SquareGridUtils.WorldToGrid(worldPoint);

        if (!_pathPreview.HasCoordChanged(gridCoord))
            return;

        SquareCell hoveredCell = Grid.GetCell(gridCoord);
        if (hoveredCell == null || !_highlightedCells.Contains(hoveredCell))
        {
            _pathPreview.HidePath();
            return;
        }

        if (isFlamingSpherePreview)
        {
            FlamingSphereEntity sphere = _selectedFlamingSphereForControl;
            if (sphere == null || gridCoord == sphere.GridPosition)
            {
                _pathPreview.HidePath();
                return;
            }

            if (!TryBuildFlamingSphereTravelPath(sphere, gridCoord, out List<Vector2Int> spherePath, out _)
                || spherePath == null
                || spherePath.Count <= 0)
            {
                _pathPreview.HidePath();
                return;
            }

            _pathPreview.ShowPath(sphere.GridPosition, spherePath, null);
            return;
        }

        CharacterController pc = ActivePC;
        if (pc == null || gridCoord == pc.GridPosition)
        {
            _pathPreview.HidePath();
            return;
        }

        bool previewAllowThroughEnemies = _isSelectingOverrunDestination;
        HashSet<Vector2Int> threatenedSquares = previewAllowThroughEnemies
            ? null
            : GetPreviewThreatenedSquares(pc);

        int previewMaxRange = _isGrappleMoveSelection
            ? Mathf.Max(1, _grappleMoveMaxRangeSquares)
            : (_isFreeAdjacentGrappleMoveSelection ? 1 : (_isSelectingWithdraw ? GetWithdrawMoveRangeSquares(pc) : GetCurrentMoveRangeSquares(pc)));
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
            var segmentThreatened = new List<bool>();
            bool hasThreatData = threatenedSquares != null;
            Vector2Int prev = pc.GridPosition;
            int segmentIndex = 0;
            foreach (var step in pathResult.Path)
            {
                bool leaving = hasThreatData && threatenedSquares.Contains(prev);
                if (_isSelectingWithdraw && segmentIndex == 0)
                    leaving = false;

                segmentThreatened.Add(leaving);
                prev = step;
                segmentIndex++;
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
        if (_hoverMarker == null)
            return;

        bool isMovementMarker = CurrentSubPhase == PlayerSubPhase.Moving && ActivePC != null;
        bool isFlamingSphereMarker = CurrentSubPhase == PlayerSubPhase.SelectingFlamingSphereTarget && _selectedFlamingSphereForControl != null;

        if (!isMovementMarker && !isFlamingSphereMarker)
        {
            if (_hoverMarker.IsVisible)
            {
                _hoverMarker.Hide();
                _lastHoverMarkerCoord = new Vector2Int(-999, -999);
            }
            return;
        }

        if (_mainCam == null)
            return;

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

        if (gridCoord == _lastHoverMarkerCoord)
            return;

        _lastHoverMarkerCoord = gridCoord;

        SquareCell hoveredCell = Grid.GetCell(gridCoord);
        if (hoveredCell == null)
        {
            _hoverMarker.Hide();
            return;
        }

        bool isValidDestination;
        if (isFlamingSphereMarker)
        {
            FlamingSphereEntity sphere = _selectedFlamingSphereForControl;
            isValidDestination = sphere != null
                && _highlightedCells.Contains(hoveredCell)
                && gridCoord != sphere.GridPosition
                && TryBuildFlamingSphereTravelPath(sphere, gridCoord, out List<Vector2Int> spherePath, out _)
                && spherePath != null
                && spherePath.Count > 0;
        }
        else
        {
            isValidDestination = _highlightedCells.Contains(hoveredCell)
                                 && gridCoord != ActivePC.GridPosition;
        }

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

}
