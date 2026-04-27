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
    private const string DisruptUndeadTestPresetId = "disrupt_undead_test";
    private const string WizardSpellTestPresetId = "wizard_spell_test";
    private const string ClericSpellTestPresetId = "cleric_spell_test";
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
    private bool _isDisruptUndeadTestEncounter;
    private bool _isWizardSpellTestEncounter;
    private bool _isClericSpellTestEncounter;
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
    private SummonMonsterOption _pendingSummonSelection; // Selected summon option waiting for placement
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
        public int RemainingRounds;
        public int TotalDurationRounds;
        public string SourceSpellId;
        public bool IsAlliedToPCs;
        public bool SmiteUsed;
        public SummonCommand CurrentCommand;
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
        NPCDatabase.Init();

        if (EncounterSelectionUI == null)
            EncounterSelectionUI = FindObjectOfType<EncounterSelectionUI>();
        if (EncounterSelectionUI == null)
            EncounterSelectionUI = gameObject.AddComponent<EncounterSelectionUI>();

        WaitingForEncounterSelection = true;
        var presets = NPCDatabase.ListEncounterPresets();

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
        _isDisruptUndeadTestEncounter = string.Equals(presetId, DisruptUndeadTestPresetId, StringComparison.Ordinal);
        _isWizardSpellTestEncounter = string.Equals(presetId, WizardSpellTestPresetId, StringComparison.Ordinal);
        _isClericSpellTestEncounter = string.Equals(presetId, ClericSpellTestPresetId, StringComparison.Ordinal);

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
        else if (_isDisruptUndeadTestEncounter)
            ConfigureDisruptUndeadTestParty();
        else if (_isWizardSpellTestEncounter)
            ConfigureWizardSpellTestParty();
        else if (_isClericSpellTestEncounter)
            ConfigureClericSpellTestParty();
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
        if (summon.Team != CharacterTeam.Player)
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

    private void ConfigureGreaseTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats wizardStats = new CharacterStats(
            name: "Greasy Greg",
            level: 5,
            characterClass: "Wizard",
            str: 10, dex: 14, con: 12, wis: 13, intelligence: 18, cha: 10,
            bab: 2,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 4,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 22,
            raceName: "Human"
        );
        wizardStats.CharacterAlignment = Alignment.TrueNeutral;

        CharacterStats fighterStats = new CharacterStats(
            name: "Slippery Sam",
            level: 5,
            characterClass: "Fighter",
            str: 18, dex: 14, con: 14, wis: 12, intelligence: 10, cha: 10,
            bab: 5,
            armorBonus: 4,
            shieldBonus: 0,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 47,
            raceName: "Human"
        );
        fighterStats.CharacterAlignment = Alignment.LawfulNeutral;

        Vector2Int wizardStart = new Vector2Int(5, 5);
        Vector2Int fighterStart = new Vector2Int(7, 5);

        Sprite wizardAlive = IconLoader.GetToken("Wizard") ?? pcAliveFallback;
        Sprite fighterAlive = IconLoader.GetToken("Fighter") ?? pcAliveFallback;

        PC1.Init(wizardStats, wizardStart, wizardAlive, pcDead);
        PC2.Init(fighterStats, fighterStart, fighterAlive, pcDead);

        InventoryComponent wizardInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        wizardInventory.Init(wizardStats);
        wizardInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("quarterstaff"), EquipSlot.RightHand);
        wizardInventory.CharacterInventory.RecalculateStats();

        InventoryComponent fighterInventory = PC2.gameObject.GetComponent<InventoryComponent>() ?? PC2.gameObject.AddComponent<InventoryComponent>();
        fighterInventory.Init(fighterStats);
        fighterInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("longsword"), EquipSlot.RightHand);
        fighterInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("chainmail"), EquipSlot.Armor);
        fighterInventory.CharacterInventory.RecalculateStats();

        SpellcastingComponent wizardSpellComp = PC1.gameObject.GetComponent<SpellcastingComponent>() ?? PC1.gameObject.AddComponent<SpellcastingComponent>();
        wizardSpellComp.KnownSpells.Clear();
        wizardSpellComp.SelectedSpellIds = new List<string>
        {
            "detect_magic_wiz", "read_magic", "grease", "mage_armor"
        };
        wizardSpellComp.PreparedSpellSlotIds = new List<string>
        {
            "grease", "grease", "grease", "grease"
        };
        wizardSpellComp.Init(wizardStats);

        StatusEffectManager wizardStatusMgr = PC1.gameObject.GetComponent<StatusEffectManager>() ?? PC1.gameObject.AddComponent<StatusEffectManager>();
        wizardStatusMgr.Init(wizardStats);

        StatusEffectManager fighterStatusMgr = PC2.gameObject.GetComponent<StatusEffectManager>() ?? PC2.gameObject.AddComponent<StatusEffectManager>();
        fighterStatusMgr.Init(fighterStats);

        ConcentrationManager wizardConcentrationMgr = PC1.gameObject.GetComponent<ConcentrationManager>() ?? PC1.gameObject.AddComponent<ConcentrationManager>();
        wizardConcentrationMgr.Init(wizardStats, PC1);

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, true, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("╔═══════════════════════════════════════════════════════╗");
        CombatUI?.ShowCombatLog("║          🧪 GREASE MECHANICS TEST SCENARIO           ║");
        CombatUI?.ShowCombatLog("╚═══════════════════════════════════════════════════════╝");
        CombatUI?.ShowCombatLog("This scenario tests all three Grease modes and grapple defense timing.");
        CombatUI?.ShowCombatLog("  • Greasy Greg (Wizard 5): Grease prepared x4 (DC 15). ");
        CombatUI?.ShowCombatLog("  • Slippery Sam (Fighter 5): NO pre-applied grease; must be buffed by spell.");
        CombatUI?.ShowCombatLog("  • Enemies: 4 low-Reflex grapplers clustered for 10-ft area testing.");
        CombatUI?.ShowCombatLog("");
        CombatUI?.ShowCombatLog("WIZARD ACTIONS:");
        CombatUI?.ShowCombatLog("  1. Cast Grease (Armor) on Slippery Sam (+10 grapple defense, 5 rounds).");
        CombatUI?.ShowCombatLog("  2. Cast Grease (Area) on enemy cluster to force Reflex saves/prone.");
        CombatUI?.ShowCombatLog("  3. Cast Grease (Object) on enemy weapon to force drops.");
        CombatUI?.ShowCombatLog("FIGHTER ACTIONS:");
        CombatUI?.ShowCombatLog("  1. Wait for Grease (Armor), then absorb enemy grapple attempts.");
        CombatUI?.ShowCombatLog("  2. If grappled, test escape checks with the +10 circumstance bonus.");
        CombatUI?.ShowCombatLog("ENEMY BEHAVIOR:");
        CombatUI?.ShowCombatLog("  • All grapplers prioritize Slippery Sam first.");

        Debug.Log($"[GreaseTest] Party ready. Wizard at {wizardStart}, Fighter at {fighterStart}. Grease prepared: 4.");
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

    private void ConfigureTigerHuntTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats fighterStats = new CharacterStats(
            name: "Test Fighter",
            level: 5,
            characterClass: "Fighter",
            str: 18, dex: 12, con: 16, wis: 10, intelligence: 10, cha: 8,
            bab: 5,
            armorBonus: 8,
            shieldBonus: 2,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 4,
            baseSpeed: 4,
            atkRange: 1,
            baseHitDieHP: 38,
            raceName: "Human"
        );

        CharacterStats rogueStats = new CharacterStats(
            name: "Test Rogue",
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
            baseHitDieHP: 28,
            raceName: "Human"
        );

        CharacterStats wizardStats = new CharacterStats(
            name: "Test Wizard",
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
            baseHitDieHP: 18,
            raceName: "Human"
        );

        fighterStats.CharacterAlignment = Alignment.LawfulNeutral;
        rogueStats.CharacterAlignment = Alignment.ChaoticNeutral;
        wizardStats.CharacterAlignment = Alignment.NeutralGood;

        PC1.Init(fighterStats, new Vector2Int(6, 12), IconLoader.GetToken("Fighter") ?? pcAliveFallback, pcDead);
        PC2.Init(rogueStats, new Vector2Int(8, 10), IconLoader.GetToken("Rogue") ?? pcAliveFallback, pcDead);
        PC3.Init(wizardStats, new Vector2Int(9, 7), IconLoader.GetToken("Wizard") ?? pcAliveFallback, pcDead);

        InventoryComponent fighterInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        fighterInventory.Init(fighterStats);
        fighterInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("full_plate"), EquipSlot.Armor);
        fighterInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("longsword"), EquipSlot.RightHand);
        fighterInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("shield_heavy_steel"), EquipSlot.LeftHand);
        fighterInventory.CharacterInventory.RecalculateStats();

        InventoryComponent rogueInventory = PC2.gameObject.GetComponent<InventoryComponent>() ?? PC2.gameObject.AddComponent<InventoryComponent>();
        rogueInventory.Init(rogueStats);
        rogueInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("leather_armor"), EquipSlot.Armor);
        rogueInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("short_sword"), EquipSlot.RightHand);
        rogueInventory.CharacterInventory.RecalculateStats();

        InventoryComponent wizardInventory = PC3.gameObject.GetComponent<InventoryComponent>() ?? PC3.gameObject.AddComponent<InventoryComponent>();
        wizardInventory.Init(wizardStats);
        wizardInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("quarterstaff"), EquipSlot.RightHand);
        wizardInventory.CharacterInventory.RecalculateStats();

        // Predator-priority target setup: start rogue wounded and wizard invisible.
        rogueStats.CurrentHP = Mathf.Clamp(12, 1, rogueStats.TotalMaxHP);
        PC3.ApplyCondition(CombatConditionType.Invisible, -1, "Tiger Hunt Test");

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, true, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, true, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("🐅 Tiger Hunt Test: Fighter, wounded rogue, and invisible wizard face a tiger in open terrain.");
        CombatUI?.ShowCombatLog("   Verify: Pounce charge + rake sequence, Improved Grab follow-up, scent targeting on invisible wizard, and predator target choice.");
        CombatUI?.ShowCombatLog("   Optional: focus fire tiger below 30% HP to verify animal withdraw/flee behavior.");
    }

    private void ConfigureOgreBattleTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats wizardStats = new CharacterStats(
            name: "Aria",
            level: 6,
            characterClass: "Wizard",
            str: 8, dex: 14, con: 12, wis: 13, intelligence: 18, cha: 10,
            bab: 3,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 4,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 30,
            raceName: "Human"
        );

        wizardStats.CharacterAlignment = Alignment.NeutralGood;

        PC1.Init(wizardStats, new Vector2Int(6, 10), IconLoader.GetToken("Wizard") ?? pcAliveFallback, pcDead);

        InventoryComponent wizardInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        wizardInventory.Init(wizardStats);
        wizardInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("quarterstaff"), EquipSlot.RightHand);
        wizardInventory.CharacterInventory.RecalculateStats();

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, false, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("🧙 Ogre Battle: Aria (Wizard 6) fights alongside a controllable dire tiger against two ogres.");
        CombatUI?.ShowCombatLog("   Validate: multi-character player turns, ally tiger control, and berserk ogre pressure.");
    }

    private void ConfigureShieldBashTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats shielderStats = new CharacterStats(
            name: "Shielder",
            level: 5,
            characterClass: "Fighter",
            str: 16, dex: 14, con: 14, wis: 10, intelligence: 10, cha: 10,
            bab: 5,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 3,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 44,
            raceName: "Human"
        );

        shielderStats.InitFeats();
        shielderStats.AddFeats(new List<string> { "Improved Shield Bash" });

        CharacterStats basherStats = new CharacterStats(
            name: "Basher",
            level: 5,
            characterClass: "Fighter",
            str: 16, dex: 14, con: 14, wis: 10, intelligence: 10, cha: 10,
            bab: 5,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 3,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 44,
            raceName: "Human"
        );

        basherStats.InitFeats();

        Vector2Int shielderStart = new Vector2Int(6, 9);
        Vector2Int basherStart = new Vector2Int(12, 9);

        PC1.Init(shielderStats, shielderStart, IconLoader.GetToken("Fighter") ?? pcAliveFallback, pcDead);
        PC2.Init(basherStats, basherStart, IconLoader.GetToken("Fighter") ?? pcAliveFallback, pcDead);

        InventoryComponent shielderInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        shielderInventory.Init(shielderStats);
        shielderInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("longsword"), EquipSlot.RightHand);
        shielderInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("shield_heavy_steel"), EquipSlot.LeftHand);
        shielderInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("chain_shirt"), EquipSlot.Armor);
        shielderInventory.CharacterInventory.RecalculateStats();

        InventoryComponent basherInventory = PC2.gameObject.GetComponent<InventoryComponent>() ?? PC2.gameObject.AddComponent<InventoryComponent>();
        basherInventory.Init(basherStats);
        basherInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("longsword"), EquipSlot.RightHand);
        basherInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("shield_heavy_steel"), EquipSlot.LeftHand);
        basherInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("chain_shirt"), EquipSlot.Armor);
        basherInventory.CharacterInventory.RecalculateStats();

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, true, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("🛡️ Shield Bash Test: Shielder (Improved Shield Bash) vs Basher (no feat).");
        CombatUI?.ShowCombatLog("   Both use longsword + heavy shield + chain shirt. Expected base AC: 18 each.");
        CombatUI?.ShowCombatLog("   After shield bash: Shielder keeps AC 18; Basher drops to AC 16 until next turn.");
    }

    private void ConfigureCelestialTemplateTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats clericStats = new CharacterStats(
            name: "Lysara",
            level: 5,
            characterClass: "Cleric",
            str: 12, dex: 10, con: 14, wis: 18, intelligence: 10, cha: 14,
            bab: 3,
            armorBonus: 4,
            shieldBonus: 2,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 1,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 36,
            raceName: "Human"
        );

        clericStats.CharacterAlignment = Alignment.LawfulGood;

        Vector2Int clericStart = new Vector2Int(3, 7);
        Sprite clericAlive = IconLoader.GetToken("Cleric") ?? pcAliveFallback;
        PC1.Init(clericStats, clericStart, clericAlive, pcDead);

        InventoryComponent clericInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        clericInventory.Init(clericStats);
        clericInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("mace_heavy"), EquipSlot.RightHand);
        clericInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("shield_heavy_steel"), EquipSlot.LeftHand);
        clericInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("chainmail"), EquipSlot.Armor);
        clericInventory.CharacterInventory.RecalculateStats();

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, false, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("✨ Celestial Template Test: Lysara (Cleric 5) commands celestial wolf + celestial dire bear allies.");
        CombatUI?.ShowCombatLog("   Verify: templates are applied at spawn time (Magical Beast type, resistances, DR/SR scaling, Smite Evil). ");
        CombatUI?.ShowCombatLog("   Opposing undead should remain evil-aligned to validate celestial smite targeting.");
    }

    private void ConfigureFiendishTemplateTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats necromancerStats = new CharacterStats(
            name: "Malakai",
            level: 5,
            characterClass: "Wizard",
            str: 8, dex: 14, con: 12, wis: 12, intelligence: 18, cha: 14,
            bab: 2,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 4,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 28,
            raceName: "Human"
        );

        necromancerStats.CharacterAlignment = Alignment.NeutralEvil;
        necromancerStats.AddSpecialAbility("Necromancy Focus");

        Vector2Int necromancerStart = new Vector2Int(3, 7);
        Sprite necromancerAlive = IconLoader.GetToken("Wizard") ?? pcAliveFallback;
        PC1.Init(necromancerStats, necromancerStart, necromancerAlive, pcDead);

        InventoryComponent necromancerInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        necromancerInventory.Init(necromancerStats);
        necromancerInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("quarterstaff"), EquipSlot.RightHand);
        necromancerInventory.CharacterInventory.RecalculateStats();

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, false, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("🔥 Fiendish Template Test: Malakai (NE Wizard 5) commands fiendish wolf + fiendish dire bear allies.");
        CombatUI?.ShowCombatLog("   Verify Fiendish scaling: darkvision, Resist Cold/Fire, Smite Good, DR 10/magic at 12 HD, and SR 22 on the dire bear.");
        CombatUI?.ShowCombatLog("   Targets are good-aligned human paladin + cleric to validate Smite Good selection and damage bonuses.");
    }

    private void ConfigureSummonMonsterTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats clericStats = new CharacterStats(
            name: "Ilyra",
            level: 5,
            characterClass: "Cleric",
            str: 10, dex: 12, con: 14, wis: 18, intelligence: 10, cha: 14,
            bab: 3,
            armorBonus: 4,
            shieldBonus: 2,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 34,
            raceName: "Human"
        );
        clericStats.CharacterAlignment = Alignment.NeutralGood;

        CharacterStats wizardStats = new CharacterStats(
            name: "Theron",
            level: 5,
            characterClass: "Wizard",
            str: 8, dex: 14, con: 12, wis: 12, intelligence: 18, cha: 10,
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
        wizardStats.CharacterAlignment = Alignment.TrueNeutral;

        Vector2Int clericStart = new Vector2Int(4, 9);
        Vector2Int wizardStart = new Vector2Int(3, 10);

        Sprite clericAlive = IconLoader.GetToken("Cleric") ?? pcAliveFallback;
        Sprite wizardAlive = IconLoader.GetToken("Wizard") ?? pcAliveFallback;

        PC1.Init(clericStats, clericStart, clericAlive, pcDead);
        PC2.Init(wizardStats, wizardStart, wizardAlive, pcDead);

        InventoryComponent clericInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        clericInventory.Init(clericStats);
        clericInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("mace_heavy"), EquipSlot.RightHand);
        clericInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("shield_heavy_steel"), EquipSlot.LeftHand);
        clericInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("chainmail"), EquipSlot.Armor);
        clericInventory.CharacterInventory.RecalculateStats();

        InventoryComponent wizardInventory = PC2.gameObject.GetComponent<InventoryComponent>() ?? PC2.gameObject.AddComponent<InventoryComponent>();
        wizardInventory.Init(wizardStats);
        wizardInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("quarterstaff"), EquipSlot.RightHand);
        wizardInventory.CharacterInventory.RecalculateStats();

        SpellcastingComponent clericSpellComp = PC1.gameObject.GetComponent<SpellcastingComponent>() ?? PC1.gameObject.AddComponent<SpellcastingComponent>();
        clericSpellComp.KnownSpells.Clear();
        clericSpellComp.SelectedSpellIds = new List<string> { "detect_magic", "guidance", "light", "resistance" };
        clericSpellComp.PreparedSpellSlotIds = null;
        clericSpellComp.Init(clericStats);
        PrepareSummonMonsterTestSpellSlots(
            clericSpellComp,
            summonOneSpellId: "summon_monster_1_clr",
            summonTwoSpellId: "summon_monster_2_clr",
            levelOneFallbackAId: "bless",
            levelOneFallbackBId: "shield_of_faith",
            levelTwoFallbackAId: "hold_person",
            levelTwoFallbackBId: "cure_moderate_wounds");

        SpellcastingComponent wizardSpellComp = PC2.gameObject.GetComponent<SpellcastingComponent>() ?? PC2.gameObject.AddComponent<SpellcastingComponent>();
        wizardSpellComp.KnownSpells.Clear();
        wizardSpellComp.SelectedSpellIds = new List<string> { "detect_magic", "ray_of_frost", "acid_splash", "read_magic" };
        wizardSpellComp.PreparedSpellSlotIds = null;
        wizardSpellComp.Init(wizardStats);
        PrepareSummonMonsterTestSpellSlots(
            wizardSpellComp,
            summonOneSpellId: "summon_monster_1",
            summonTwoSpellId: "summon_monster_2",
            levelOneFallbackAId: "magic_missile",
            levelOneFallbackBId: "mage_armor",
            levelTwoFallbackAId: "mirror_image",
            levelTwoFallbackBId: "invisibility");

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, true, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("🌀 Summon Monster Test: Ilyra (Cleric 5) and Theron (Wizard 5) both have Summon Monster I/II prepared.");
        CombatUI?.ShowCombatLog("   Flow validation: choose creature first, then pick a legal placement tile.");
        CombatUI?.ShowCombatLog("   Alignment validation: cleric sees celestial/fiendish cleric-locked options based on alignment; wizard sees class-agnostic options.");
    }

    private void ConfigureNpcMagicMissileTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats wizardStats = new CharacterStats(
            name: "Theron",
            level: 5,
            characterClass: "Wizard",
            str: 8, dex: 14, con: 12, wis: 12, intelligence: 18, cha: 10,
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

        Vector2Int wizardStart = new Vector2Int(3, 9);

        Sprite wizardAlive = IconLoader.GetToken("Wizard") ?? pcAliveFallback;

        PC1.Init(wizardStats, wizardStart, wizardAlive, pcDead);

        InventoryComponent wizardInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        wizardInventory.Init(wizardStats);
        wizardInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("quarterstaff"), EquipSlot.RightHand);
        wizardInventory.CharacterInventory.RecalculateStats();

        SpellcastingComponent wizardSpellComp = PC1.gameObject.GetComponent<SpellcastingComponent>() ?? PC1.gameObject.AddComponent<SpellcastingComponent>();
        wizardSpellComp.KnownSpells.Clear();
        wizardSpellComp.SelectedSpellIds = new List<string>
        {
            "detect_magic_wiz", "ray_of_frost", "acid_splash", "read_magic",
            "shield", "magic_missile", "mage_armor", "mirror_image"
        };
        wizardSpellComp.PreparedSpellSlotIds = new List<string>
        {
            "detect_magic_wiz", "ray_of_frost", "acid_splash", "read_magic",
            "shield", "magic_missile", "mage_armor",
            "mirror_image", "invisibility"
        };
        wizardSpellComp.Init(wizardStats);

        StatusEffectManager wizardStatusMgr = PC1.gameObject.GetComponent<StatusEffectManager>() ?? PC1.gameObject.AddComponent<StatusEffectManager>();
        wizardStatusMgr.Init(wizardStats);

        ConcentrationManager wizardConcentrationMgr = PC1.gameObject.GetComponent<ConcentrationManager>() ?? PC1.gameObject.AddComponent<ConcentrationManager>();
        wizardConcentrationMgr.Init(wizardStats, PC1);

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, false, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("🧪 NPC Magic Missile Test: Theron (Wizard 5) has Shield prepared for direct counter-testing.");
        CombatUI?.ShowCombatLog("   Cast Shield on Theron, then end turn to verify Arcane Missile Adept cannot damage him with Magic Missile.");
        CombatUI?.ShowCombatLog("   Scenario is now focused to two combatants only: player wizard vs enemy Arcane Missile Adept.");
    }

    private void ConfigureProtectionFromEvilTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats wizardStats = new CharacterStats(
            name: "Warded Theron",
            level: 10,
            characterClass: "Wizard",
            str: 8, dex: 14, con: 14, wis: 14, intelligence: 20, cha: 10,
            bab: 5,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 4,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 58,
            raceName: "Human"
        );
        wizardStats.CharacterAlignment = Alignment.TrueNeutral;

        Vector2Int wizardStart = new Vector2Int(3, 9);
        Sprite wizardAlive = IconLoader.GetToken("Wizard") ?? pcAliveFallback;
        PC1.Init(wizardStats, wizardStart, wizardAlive, pcDead);

        InventoryComponent wizardInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        wizardInventory.Init(wizardStats);
        wizardInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("quarterstaff"), EquipSlot.RightHand);
        wizardInventory.CharacterInventory.RecalculateStats();

        SpellcastingComponent wizardSpellComp = PC1.gameObject.GetComponent<SpellcastingComponent>() ?? PC1.gameObject.AddComponent<SpellcastingComponent>();
        wizardSpellComp.KnownSpells.Clear();
        wizardSpellComp.SelectedSpellIds = new List<string>
        {
            "detect_magic_wiz", "read_magic", "protection_from_evil", "shield", "magic_missile"
        };
        wizardSpellComp.PreparedSpellSlotIds = new List<string>
        {
            "protection_from_evil", "protection_from_evil", "shield", "magic_missile", "magic_missile"
        };
        wizardSpellComp.Init(wizardStats);

        StatusEffectManager wizardStatusMgr = PC1.gameObject.GetComponent<StatusEffectManager>() ?? PC1.gameObject.AddComponent<StatusEffectManager>();
        wizardStatusMgr.Init(wizardStats);

        SpellData protectionFromEvil = SpellDatabase.GetSpell("protection_from_evil");
        if (protectionFromEvil != null)
        {
            wizardStatusMgr.AddEffect(protectionFromEvil, "Scenario Setup", casterLevel: wizardStats.Level);
            CombatUI?.ShowCombatLog("🛡️ Warded Theron starts with Protection from Evil active.");
        }
        else
        {
            Debug.LogError("[ProtectionFromEvilTest] Missing spell definition: protection_from_evil");
        }

        ConcentrationManager wizardConcentrationMgr = PC1.gameObject.GetComponent<ConcentrationManager>() ?? PC1.gameObject.AddComponent<ConcentrationManager>();
        wizardConcentrationMgr.Init(wizardStats, PC1);

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, false, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("🧪 Protection from Evil Test: Warded Theron (Wizard 10) vs evil + non-evil controls.");
        CombatUI?.ShowCombatLog("This scenario tests SIX mechanics:");
        CombatUI?.ShowCombatLog("  1. Mental Control Immunity (Charm Person blocked)");
        CombatUI?.ShowCombatLog("  2. Summoned Barrier (Fiendish Wolf can't touch)");
        CombatUI?.ShowCombatLog("  3. AC Bonus vs Evil (+2 vs Evil Goblin)");
        CombatUI?.ShowCombatLog("  4. NO AC Bonus vs Non-Evil (normal AC vs Neutral Bandit)");
        CombatUI?.ShowCombatLog("  5. Save Bonus vs Evil (+2 vs Evil Acolyte's Daze)");
        CombatUI?.ShowCombatLog("  6. NO Save Bonus vs Non-Evil (normal save vs Neutral Mage's Daze)");
        CombatUI?.ShowCombatLog("");
    }

    private void ConfigureDisruptUndeadTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats wizardStats = new CharacterStats(
            name: "Necromancer Theron",
            level: 3,
            characterClass: "Wizard",
            str: 8, dex: 14, con: 12, wis: 12, intelligence: 16, cha: 10,
            bab: 1,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 4,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 18,
            raceName: "Human"
        );

        Vector2Int wizardStart = new Vector2Int(3, 9);
        Sprite wizardAlive = IconLoader.GetToken("Wizard") ?? pcAliveFallback;
        PC1.Init(wizardStats, wizardStart, wizardAlive, pcDead);

        InventoryComponent wizardInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        wizardInventory.Init(wizardStats);
        wizardInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("quarterstaff"), EquipSlot.RightHand);
        wizardInventory.CharacterInventory.RecalculateStats();

        SpellcastingComponent wizardSpellComp = PC1.gameObject.GetComponent<SpellcastingComponent>() ?? PC1.gameObject.AddComponent<SpellcastingComponent>();
        wizardSpellComp.KnownSpells.Clear();
        wizardSpellComp.SelectedSpellIds = new List<string>
        {
            "detect_magic_wiz", "ray_of_frost", "acid_splash", "disrupt_undead", "read_magic"
        };
        wizardSpellComp.PreparedSpellSlotIds = new List<string>
        {
            "disrupt_undead", "disrupt_undead", "disrupt_undead", "disrupt_undead", "disrupt_undead"
        };
        wizardSpellComp.Init(wizardStats);

        StatusEffectManager wizardStatusMgr = PC1.gameObject.GetComponent<StatusEffectManager>() ?? PC1.gameObject.AddComponent<StatusEffectManager>();
        wizardStatusMgr.Init(wizardStats);

        ConcentrationManager wizardConcentrationMgr = PC1.gameObject.GetComponent<ConcentrationManager>() ?? PC1.gameObject.AddComponent<ConcentrationManager>();
        wizardConcentrationMgr.Init(wizardStats, PC1);

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, false, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("☀️ Disrupt Undead Test: Necromancer Theron (Wizard 3) prepared 5× Disrupt Undead.");
        CombatUI?.ShowCombatLog("   Test Mode - Easy to Hit is active for all enemies (very low AC / Touch AC).");
        CombatUI?.ShowCombatLog("   Procedure: use ranged touch attacks vs skeletons/zombie and verify 1d6 positive damage.");
        CombatUI?.ShowCombatLog("   Validation: cast at the living orc as a control target — Disrupt Undead should report no effect.");
    }

    private void PrepareSummonMonsterTestSpellSlots(
        SpellcastingComponent spellComp,
        string summonOneSpellId,
        string summonTwoSpellId,
        string levelOneFallbackAId,
        string levelOneFallbackBId,
        string levelTwoFallbackAId,
        string levelTwoFallbackBId)
    {
        if (spellComp == null || spellComp.SpellSlots == null || spellComp.SpellSlots.Count == 0)
            return;

        SpellData summonOne = SpellDatabase.GetSpell(summonOneSpellId);
        SpellData summonTwo = SpellDatabase.GetSpell(summonTwoSpellId);
        SpellData levelOneFallbackA = SpellDatabase.GetSpell(levelOneFallbackAId);
        SpellData levelOneFallbackB = SpellDatabase.GetSpell(levelOneFallbackBId);
        SpellData levelTwoFallbackA = SpellDatabase.GetSpell(levelTwoFallbackAId);
        SpellData levelTwoFallbackB = SpellDatabase.GetSpell(levelTwoFallbackBId);

        int levelOneSummonCount = 0;
        int levelTwoSummonCount = 0;

        for (int i = 0; i < spellComp.SpellSlots.Count; i++)
        {
            SpellSlot slot = spellComp.SpellSlots[i];
            if (slot == null)
                continue;

            SpellData toPrepare = null;

            if (slot.Level == 1)
            {
                if (summonOne != null && levelOneSummonCount < 2)
                {
                    toPrepare = summonOne;
                    levelOneSummonCount++;
                }
                else
                {
                    toPrepare = levelOneFallbackA ?? levelOneFallbackB;
                }
            }
            else if (slot.Level == 2)
            {
                if (summonTwo != null && levelTwoSummonCount < 2)
                {
                    toPrepare = summonTwo;
                    levelTwoSummonCount++;
                }
                else
                {
                    toPrepare = levelTwoFallbackA ?? levelTwoFallbackB;
                }
            }

            if (toPrepare != null)
                spellComp.PrepareSpellInSlot(i, toPrepare);
        }

        spellComp.SyncPreparedSpellsFromSlots();
        Debug.Log($"[SummonTest] Prepared summon loadout for {spellComp.Stats?.CharacterName}: {spellComp.GetSlotDetails()}");
    }

    private void ConfigureWizardSpellTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats wizardStats = new CharacterStats(
            name: "Archmage Theron",
            level: 20,
            characterClass: "Wizard",
            str: 8, dex: 16, con: 14, wis: 12, intelligence: 24, cha: 10,
            bab: 10,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 4,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 110,
            raceName: "Human"
        );
        wizardStats.CharacterAlignment = Alignment.TrueNeutral;

        Vector2Int wizardStart = new Vector2Int(3, 9);
        Sprite wizardAlive = IconLoader.GetToken("Wizard") ?? pcAliveFallback;
        PC1.Init(wizardStats, wizardStart, wizardAlive, pcDead);

        InventoryComponent wizardInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        wizardInventory.Init(wizardStats);
        wizardInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("quarterstaff"), EquipSlot.RightHand);
        wizardInventory.CharacterInventory.RecalculateStats();

        SpellcastingComponent wizardSpellComp = PC1.gameObject.GetComponent<SpellcastingComponent>() ?? PC1.gameObject.AddComponent<SpellcastingComponent>();
        wizardSpellComp.KnownSpells.Clear();
        wizardSpellComp.SelectedSpellIds = null;
        wizardSpellComp.PreparedSpellSlotIds = null;
        wizardSpellComp.Init(wizardStats);
        AutoPopulateAndPrepareAllImplementedClassSpells(wizardSpellComp, "Wizard");

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, false, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("📘 Wizard Spell Test: Archmage Theron auto-prepared all implemented wizard spells.");
        CombatUI?.ShowCombatLog("   Target Dummy has AC 1, HP 50, and severe save penalties for deterministic spell validation.");
    }

    private void ConfigureClericSpellTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats clericStats = new CharacterStats(
            name: "High Priestess Ilyra",
            level: 20,
            characterClass: "Cleric",
            str: 14, dex: 12, con: 16, wis: 24, intelligence: 12, cha: 16,
            bab: 15,
            armorBonus: 4,
            shieldBonus: 2,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 150,
            raceName: "Human"
        );
        clericStats.CharacterAlignment = Alignment.NeutralGood;

        Vector2Int clericStart = new Vector2Int(3, 9);
        Sprite clericAlive = IconLoader.GetToken("Cleric") ?? pcAliveFallback;
        PC1.Init(clericStats, clericStart, clericAlive, pcDead);

        InventoryComponent clericInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        clericInventory.Init(clericStats);
        clericInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("mace_heavy"), EquipSlot.RightHand);
        clericInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("shield_heavy_steel"), EquipSlot.LeftHand);
        clericInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("chainmail"), EquipSlot.Armor);
        clericInventory.CharacterInventory.RecalculateStats();

        SpellcastingComponent clericSpellComp = PC1.gameObject.GetComponent<SpellcastingComponent>() ?? PC1.gameObject.AddComponent<SpellcastingComponent>();
        clericSpellComp.KnownSpells.Clear();
        clericSpellComp.SelectedSpellIds = null;
        clericSpellComp.PreparedSpellSlotIds = null;
        clericSpellComp.Init(clericStats);
        AutoPopulateAndPrepareAllImplementedClassSpells(clericSpellComp, "Cleric");

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, false, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("📖 Cleric Spell Test: High Priestess Ilyra auto-prepared all implemented cleric spells.");
        CombatUI?.ShowCombatLog("   Target Dummy has AC 1, HP 50, and severe save penalties for deterministic spell validation.");
    }

    private void AutoPopulateAndPrepareAllImplementedClassSpells(SpellcastingComponent spellComp, string className)
    {
        if (spellComp == null || spellComp.Stats == null)
            return;

        List<SpellData> implementedSpells = SpellDatabase.GetImplementedSpellsForClass(className);
        if (implementedSpells == null || implementedSpells.Count == 0)
            implementedSpells = SpellDatabase.GetSpellsForClass(className);

        spellComp.KnownSpells.Clear();
        if (implementedSpells != null)
            spellComp.KnownSpells.AddRange(implementedSpells);

        for (int i = 0; i < spellComp.SpellSlots.Count; i++)
        {
            SpellSlot existingSlot = spellComp.SpellSlots[i];
            if (existingSlot != null)
                existingSlot.Clear();
        }

        int maxSpellLevel = 0;
        for (int i = 0; i < spellComp.KnownSpells.Count; i++)
        {
            SpellData spell = spellComp.KnownSpells[i];
            if (spell != null && spell.SpellLevel > maxSpellLevel)
                maxSpellLevel = spell.SpellLevel;
        }

        EnsureSpellSlotArrayCapacity(spellComp, maxSpellLevel + 1);

        for (int level = 0; level <= maxSpellLevel; level++)
        {
            List<SpellData> spellsAtLevel = new List<SpellData>();
            for (int i = 0; i < spellComp.KnownSpells.Count; i++)
            {
                SpellData spell = spellComp.KnownSpells[i];
                if (spell != null && spell.SpellLevel == level)
                    spellsAtLevel.Add(spell);
            }

            int existingSlotCount = 0;
            for (int i = 0; i < spellComp.SpellSlots.Count; i++)
            {
                SpellSlot slot = spellComp.SpellSlots[i];
                if (slot != null && slot.Level == level)
                    existingSlotCount++;
            }

            int requiredSlots = Mathf.Max(existingSlotCount, spellsAtLevel.Count);
            while (existingSlotCount < requiredSlots)
            {
                spellComp.SpellSlots.Add(new SpellSlot(level));
                existingSlotCount++;
            }

            spellComp.SlotsMax[level] = requiredSlots;
            spellComp.SlotsRemaining[level] = requiredSlots;

            if (spellsAtLevel.Count == 0)
                continue;

            int cursor = 0;
            for (int slotIndex = 0; slotIndex < spellComp.SpellSlots.Count; slotIndex++)
            {
                SpellSlot slot = spellComp.SpellSlots[slotIndex];
                if (slot == null || slot.Level != level)
                    continue;

                SpellData toPrepare = spellsAtLevel[cursor % spellsAtLevel.Count];
                spellComp.PrepareSpellInSlot(slotIndex, toPrepare);
                cursor++;
            }
        }

        spellComp.SyncPreparedSpellsFromSlots();
        Debug.Log($"[SpellTest] Auto-populated {spellComp.KnownSpells.Count} implemented {className} spells for {spellComp.Stats.CharacterName}. Slots: {spellComp.GetSlotSummary()}");
    }

    private static void EnsureSpellSlotArrayCapacity(SpellcastingComponent spellComp, int requiredLength)
    {
        int targetLength = Mathf.Max(1, requiredLength);

        if (spellComp.SlotsMax == null || spellComp.SlotsMax.Length < targetLength)
        {
            int[] resizedMax = new int[targetLength];
            if (spellComp.SlotsMax != null)
            {
                for (int i = 0; i < spellComp.SlotsMax.Length; i++)
                    resizedMax[i] = spellComp.SlotsMax[i];
            }
            spellComp.SlotsMax = resizedMax;
        }

        if (spellComp.SlotsRemaining == null || spellComp.SlotsRemaining.Length < targetLength)
        {
            int[] resizedRemaining = new int[targetLength];
            if (spellComp.SlotsRemaining != null)
            {
                for (int i = 0; i < spellComp.SlotsRemaining.Length; i++)
                    resizedRemaining[i] = spellComp.SlotsRemaining[i];
            }
            spellComp.SlotsRemaining = resizedRemaining;
        }
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

    private static readonly Vector2Int[] WizardSpellTestSpawnPositions = {
        new Vector2Int(12, 9),
    };

    private static readonly Vector2Int[] ClericSpellTestSpawnPositions = {
        new Vector2Int(12, 9),
    };

    private void SetupEnemyEncounter(List<string> enemyIds)
    {
        NPCDatabase.Init();
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
            NPCDefinition sourceDef = NPCDatabase.Get(enemyId);
            NPCDefinition def = BuildEncounterDefinitionForSpawn(enemyId, sourceDef, i);
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
            else if (_isGreaseTestEncounter && i < GreaseTestSpawnPositions.Length)
            {
                // Cluster all enemies into a tight 2x2 for 10-ft grease area and repeated grapple attempts.
                pos = GreaseTestSpawnPositions[i];
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
            else if (_isTigerHuntTestEncounter && i < TigerHuntTestSpawnPositions.Length)
            {
                // Place tiger with enough lane length to charge wounded prey and trigger pounce behavior.
                pos = TigerHuntTestSpawnPositions[i];
            }
            else if (_isOgreBattleTestEncounter && i < OgreBattleTestSpawnPositions.Length)
            {
                // Spawn controllable dire tiger near the wizard with both ogres advancing from the far side.
                pos = OgreBattleTestSpawnPositions[i];
            }
            else if (_isShieldBashTestEncounter && i < ShieldBashTestSpawnPositions.Length)
            {
                // Keep one melee enemy adjacent to each test fighter so shield-bash AC differences are obvious.
                pos = ShieldBashTestSpawnPositions[i];
            }
            else if (_isCelestialTemplateTestEncounter && i < CelestialTemplateTestSpawnPositions.Length)
            {
                // Keep celestial allies close to the cleric and undead on the opposite side.
                pos = CelestialTemplateTestSpawnPositions[i];
            }
            else if (_isFiendishTemplateTestEncounter && i < FiendishTemplateTestSpawnPositions.Length)
            {
                // Keep fiendish allies near the necromancer with good enemies opposite for Smite Good demonstrations.
                pos = FiendishTemplateTestSpawnPositions[i];
            }
            else if (_isSummonMonsterTestEncounter && i < SummonMonsterTestSpawnPositions.Length)
            {
                // Keep targets spread so summon placement and command behavior can be observed.
                pos = SummonMonsterTestSpawnPositions[i];
            }
            else if (_isProtectionFromEvilTestEncounter && i < ProtectionFromEvilTestSpawnPositions.Length)
            {
                // Place enemies so all three protection clauses are exercised quickly (charm spell, summoned contact, regular melee).
                pos = ProtectionFromEvilTestSpawnPositions[i];
            }
            else if (_isWizardSpellTestEncounter && i < WizardSpellTestSpawnPositions.Length)
            {
                // Keep the dummy in a clean line with the wizard for single-target + AoE validation.
                pos = WizardSpellTestSpawnPositions[i];
            }
            else if (_isClericSpellTestEncounter && i < ClericSpellTestSpawnPositions.Length)
            {
                // Mirror wizard test spacing so cleric spell coverage can be compared directly.
                pos = ClericSpellTestSpawnPositions[i];
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

            if (npc.Stats != null && string.Equals(enemyId, "target_dummy", StringComparison.Ordinal))
            {
                // Force extremely low saves for deterministic save-or-suck / save-for-half spell validation.
                npc.Stats.MoraleSaveBonus = -10;
                Debug.Log($"[SpellTest] Applied target dummy save penalty: F={npc.Stats.FortitudeSave}, R={npc.Stats.ReflexSave}, W={npc.Stats.WillSave}");
            }

            _npcAIBehaviors.Add(def.AIBehavior);

            ApplyScenarioSpawnOverrides(enemyId, npc, i);
            ApplyDisruptUndeadTestEasyHitOverrides(enemyId, npc);

            if (_isArmorTargetingTestEncounter && string.Equals(enemyId, "skeleton_archer", StringComparison.Ordinal))
            {
                npc.aiProfile = ScriptableObject.CreateInstance<RangedAIProfile>();
                npc.Tags.AddTag("Uses Armor-Based Targeting");
                Debug.Log($"[ArmorTargetingTest] Overriding {npc.Stats.CharacterName} to Ranged profile for armor-priority targeting validation.");
            }

            if (_isShieldBashTestEncounter)
            {
                // Keep shield bash validation deterministic: basic melee pressure only, no trip/disarm/grapple maneuver selection.
                npc.aiProfile = ScriptableObject.CreateInstance<UndeadMindlessAIProfile>();
                npc.Tags.AddTag("ShieldBashTestSimpleMeleeAI");
                Debug.Log($"[ShieldBashTest] Overriding {npc.Stats.CharacterName} to simple melee-only AI profile.");
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

            string templateLog = (def.AppliedTemplateIds != null && def.AppliedTemplateIds.Count > 0)
                ? $" | Templates: {string.Join(",", def.AppliedTemplateIds)}"
                : string.Empty;
            Debug.Log($"[GameManager] Spawned NPC {i}: {def.Name} (Lv {def.Level} {def.CharacterClass}) at ({pos.x},{pos.y}) — AI: {def.AIBehavior}{templateLog}");
            if (!string.IsNullOrWhiteSpace(def.AITargetPriority))
                CombatUI?.ShowCombatLog($"  {npc.Stats.CharacterName} priority target: {def.AITargetPriority}");

            if (_isGreaseTestEncounter && npc.Stats != null)
            {
                int grappleMod = npc.GetGrappleModifier();
                string weaponLabel = "unarmed";
                if (def.EquipmentIds != null)
                {
                    for (int eqIndex = 0; eqIndex < def.EquipmentIds.Count; eqIndex++)
                    {
                        EquipmentSlotPair eq = def.EquipmentIds[eqIndex];
                        if (eq != null && eq.Slot == EquipSlot.RightHand && !string.IsNullOrWhiteSpace(eq.ItemId))
                        {
                            weaponLabel = eq.ItemId;
                            break;
                        }
                    }
                }

                CombatUI?.ShowCombatLog($"✓ {npc.Stats.CharacterName}: Grapple {CharacterStats.FormatMod(grappleMod)}, Reflex {CharacterStats.FormatMod(npc.Stats.ReflexSave)}, Weapon {weaponLabel}");
            }
        }

        if (_isGreaseTestEncounter)
        {
            CombatUI?.ShowCombatLog("🧪 Grease scenario loaded: enemies are clustered in a 2x2 square (12,5) to (13,6).");
            CombatUI?.ShowCombatLog("   Use Grease (Armor) on Slippery Sam first, then validate Area and Object modes.");
            CombatUI?.ShowCombatLog("   Enemies are scripted to prioritize Slippery Sam for grapple pressure.");
        }

        if (_isProtectionFromEvilTestEncounter)
        {
            CombatUI?.ShowCombatLog("╔═══════════════════════════════════════════════════════╗");
            CombatUI?.ShowCombatLog("║   CONTROL TESTS (Non-Evil + Evil Save Comparison)    ║");
            CombatUI?.ShowCombatLog("╚═══════════════════════════════════════════════════════╝");
            CombatUI?.ShowCombatLog("");
            CombatUI?.ShowCombatLog("✓ Neutral Bandit (TRUE NEUTRAL): NO AC bonus from ward expected.");
            CombatUI?.ShowCombatLog("✓ Neutral Mage (TRUE NEUTRAL): Daze allows normal save (no +2 bonus).");
            CombatUI?.ShowCombatLog("✓ Evil Acolyte (NEUTRAL EVIL): Daze allows save with +2 protection bonus.");
            CombatUI?.ShowCombatLog("");
            CombatUI?.ShowCombatLog("AC BONUS TEST:");
            CombatUI?.ShowCombatLog("  Evil Goblin      → Player AC includes +2 deflection");
            CombatUI?.ShowCombatLog("  Neutral Bandit   → Player AC has no protection deflection bonus");
            CombatUI?.ShowCombatLog("");
            CombatUI?.ShowCombatLog("SAVE BONUS TEST:");
            CombatUI?.ShowCombatLog("  Evil Acolyte Daze   → Will save gains +2 resistance bonus");
            CombatUI?.ShowCombatLog("  Neutral Mage Daze   → Will save remains base value (no +2)");
            CombatUI?.ShowCombatLog("");
            CombatUI?.ShowCombatLog("MENTAL CONTROL TEST:");
            CombatUI?.ShowCombatLog("  Evil Enchanter Charm Person → BLOCKED completely by protection");
            CombatUI?.ShowCombatLog("  Daze (both evil/neutral casters) → NOT blocked, only save mechanics apply");
            CombatUI?.ShowCombatLog("");
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

    private void ApplyDisruptUndeadTestEasyHitOverrides(string enemyId, CharacterController npc)
    {
        if (!_isDisruptUndeadTestEncounter || npc == null || npc.Stats == null)
            return;

        CharacterStats stats = npc.Stats;

        // Test-only override: lower all defenses so Disrupt Undead ranged touch attacks land consistently.
        stats.BaseDEX = 1;
        stats.DEX = 1;
        stats.ArmorBonus = 0;
        stats.ShieldBonus = 0;
        stats.SpellACBonus = 0;
        stats.DeflectionBonus = 0;
        stats.NaturalArmorBonus = 1;

        int loweredAC = stats.ArmorClass;
        int loweredTouchAC = SpellcastingComponent.GetTouchAC(stats);

        string enemyLabel = string.IsNullOrEmpty(stats.CharacterName) ? enemyId : stats.CharacterName;
        CombatUI?.ShowCombatLog($"🧪 Test Mode - Easy to Hit: {enemyLabel} defenses lowered (AC {loweredAC}, Touch AC {loweredTouchAC}).");
        Debug.Log($"[DisruptUndeadTest] Easy-hit override applied to {enemyLabel}: AC={loweredAC}, TouchAC={loweredTouchAC}");
    }

    private void ApplyScenarioSpawnOverrides(string enemyId, CharacterController npc, int spawnIndex)
    {
        if (npc == null)
            return;

        if (_isOgreBattleTestEncounter
            && spawnIndex == 0
            && string.Equals(enemyId, "dire_tiger", StringComparison.Ordinal))
        {
            // IMPORTANT: Keep NPCDatabase definitions scenario-agnostic.
            // Ogre Battle needs an allied/controllable tiger, so we override allegiance/control at spawn time
            // instead of baking scenario flags (IsAlly/IsControllable) into the shared NPC record.
            npc.ConfigureTeamControl(CharacterTeam.Player, controllable: true);
            npc.Tags.AddTag("ScenarioOverride:OgreBattleAlly");
            Debug.Log("[OgreBattleTest] Applied spawn-time override for dire_tiger: Team=Player, IsControllable=true.");
        }

        if (_isCelestialTemplateTestEncounter)
        {
            if (spawnIndex == 0 || spawnIndex == 1)
            {
                npc.ConfigureTeamControl(CharacterTeam.Player, controllable: true);
                npc.Stats.CharacterAlignment = Alignment.NeutralGood;
                npc.Tags.AddTag("ScenarioOverride:CelestialTemplateAlly");
                Debug.Log($"[CelestialTemplateTest] Ally override applied to {enemyId}: Team=Player, IsControllable=true, Alignment=NeutralGood.");
            }
            else
            {
                npc.ConfigureTeamControl(CharacterTeam.Enemy, controllable: false);
                npc.Stats.CharacterAlignment = Alignment.NeutralEvil;
                npc.Tags.AddTag("ScenarioOverride:CelestialTemplateUndeadEnemy");
                Debug.Log($"[CelestialTemplateTest] Enemy override applied to {enemyId}: Team=Enemy, Alignment=NeutralEvil.");
            }
        }

        if (_isFiendishTemplateTestEncounter)
        {
            if (spawnIndex == 0 || spawnIndex == 1)
            {
                npc.ConfigureTeamControl(CharacterTeam.Player, controllable: true);
                npc.Stats.CharacterAlignment = Alignment.NeutralEvil;
                npc.Tags.AddTag("ScenarioOverride:FiendishTemplateAlly");
                Debug.Log($"[FiendishTemplateTest] Ally override applied to {enemyId}: Team=Player, IsControllable=true, Alignment=NeutralEvil.");
            }
            else
            {
                npc.ConfigureTeamControl(CharacterTeam.Enemy, controllable: false);
                npc.Stats.CharacterAlignment = spawnIndex == 2 ? Alignment.LawfulGood : Alignment.NeutralGood;
                npc.Tags.AddTag("ScenarioOverride:FiendishTemplateGoodEnemy");
                Debug.Log($"[FiendishTemplateTest] Enemy override applied to {enemyId}: Team=Enemy, Alignment={npc.Stats.CharacterAlignment}.");
            }
        }

        if (_isProtectionFromEvilTestEncounter)
        {
            npc.ConfigureTeamControl(CharacterTeam.Enemy, controllable: false);

            if (string.Equals(enemyId, "evil_enchanter_test", StringComparison.Ordinal))
            {
                npc.Stats.CharacterAlignment = Alignment.NeutralEvil;
                npc.Tags.AddTag("ScenarioOverride:ProtectionFromEvilEnchanter");
                Debug.Log("[ProtectionFromEvilTest] Enchanter override applied: Team=Enemy, Alignment=NeutralEvil, Charm Person loadout active.");
            }
            else if (string.Equals(enemyId, "fiendish_wolf", StringComparison.Ordinal))
            {
                npc.Stats.CharacterAlignment = Alignment.NeutralEvil;
                npc.Tags.AddTag("ScenarioOverride:ProtectionFromEvilSummonedFiend");

                CharacterController summonCaster = null;
                if (NPCs != null && NPCs.Count > 0)
                    summonCaster = NPCs[0];
                if (summonCaster == null)
                    summonCaster = npc;

                RegisterScenarioSummonedCreature(npc, summonCaster, durationRounds: 50, sourceSpellId: "scenario_setup_protection_from_evil_test");
                Debug.Log("[ProtectionFromEvilTest] Fiendish wolf registered as summoned creature for barrier validation.");
            }
            else if (string.Equals(enemyId, "evil_goblin_test", StringComparison.Ordinal))
            {
                npc.Stats.CharacterAlignment = Alignment.NeutralEvil;
                npc.Tags.AddTag("ScenarioOverride:ProtectionFromEvilMeleeEnemy");
            }
            else if (string.Equals(enemyId, "neutral_bandit_test", StringComparison.Ordinal))
            {
                npc.Stats.CharacterAlignment = Alignment.TrueNeutral;
                npc.Tags.AddTag("ScenarioOverride:ProtectionFromEvilNeutralMeleeControl");
                Debug.Log("[ProtectionFromEvilTest] Neutral bandit control applied: no deflection bonus should trigger.");
            }
            else if (string.Equals(enemyId, "neutral_mage_test", StringComparison.Ordinal))
            {
                npc.Stats.CharacterAlignment = Alignment.TrueNeutral;
                npc.Tags.AddTag("ScenarioOverride:ProtectionFromEvilNeutralCasterControl");
                Debug.Log("[ProtectionFromEvilTest] Neutral mage control applied: Daze should not grant +2 protection save bonus.");
            }
            else if (string.Equals(enemyId, "evil_acolyte_test", StringComparison.Ordinal))
            {
                npc.Stats.CharacterAlignment = Alignment.NeutralEvil;
                npc.Tags.AddTag("ScenarioOverride:ProtectionFromEvilEvilCasterControl");
                Debug.Log("[ProtectionFromEvilTest] Evil acolyte control applied: Daze should grant +2 protection save bonus.");
            }
            else
            {
                npc.Stats.CharacterAlignment = Alignment.NeutralEvil;
                npc.Tags.AddTag("ScenarioOverride:ProtectionFromEvilFallbackEnemy");
            }
        }
    }

    private void InitializeNPCFromDefinition(CharacterController npc, NPCDefinition def,
        Vector2Int pos, Sprite alive, Sprite dead)
    {
        int hitDice = Mathf.Max(1, def.HitDice > 0 ? def.HitDice : def.Level);
        CreatureTypeProgression creatureProgression = CreatureTypeProgressionDatabase.GetFromString(def.CreatureType);

        BABProgression babProgression = def.BABOverride ?? creatureProgression.BAB;
        SaveProgression fortitudeProgression = def.FortitudeSaveOverride ?? creatureProgression.Fortitude;
        SaveProgression reflexProgression = def.ReflexSaveOverride ?? creatureProgression.Reflex;
        SaveProgression willProgression = def.WillSaveOverride ?? creatureProgression.Will;

        int computedBab = ProgressionCalculator.CalculateBAB(babProgression, hitDice);
        int resolvedBab = def.BaseAttackBonusOverride ?? computedBab;
        int computedBaseHitDieHp = def.BaseHitDieHP > 0
            ? def.BaseHitDieHP
            : ProgressionCalculator.CalculateAverageHpFromHitDice(creatureProgression.HitDie, hitDice);

        CharacterStats stats = new CharacterStats(
            name: def.Name,
            level: def.Level,
            characterClass: def.CharacterClass,
            str: def.STR, dex: def.DEX, con: def.CON,
            wis: def.WIS, intelligence: def.INT, cha: def.CHA,
            bab: resolvedBab,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 0,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: def.BaseSpeed,
            atkRange: 1,
            baseHitDieHP: computedBaseHitDieHp
        );

        stats.SetNaturalAttacks(def.NaturalAttacks);

        stats.HitDice = hitDice;
        stats.UseCreatureTypeProgression = true;
        stats.CreatureBABProgression = babProgression;
        stats.BaseAttackBonusOverride = def.BaseAttackBonusOverride;
        stats.CreatureFortitudeProgression = fortitudeProgression;
        stats.CreatureReflexProgression = reflexProgression;
        stats.CreatureWillProgression = willProgression;

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
        stats.HasImprovedGrab = def.HasImprovedGrab;
        stats.ImprovedGrabTriggerAttackName = def.ImprovedGrabTriggerAttackName;
        stats.HasPounce = def.HasPounce;
        stats.HasRake = def.HasRake;
        stats.HasScent = def.HasScent;
        stats.SetRakeAttack(def.RakeAttack);

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

        stats.SpellResistance = Mathf.Max(0, def.SpellResistance);

        bool hasCelestialTemplate = false;
        bool hasFiendishTemplate = false;
        if (def.AppliedTemplateIds != null)
        {
            for (int i = 0; i < def.AppliedTemplateIds.Count; i++)
            {
                string templateId = def.AppliedTemplateIds[i];
                if (string.IsNullOrWhiteSpace(templateId))
                    continue;

                if (string.Equals(templateId, "celestial", StringComparison.OrdinalIgnoreCase))
                    hasCelestialTemplate = true;
                else if (string.Equals(templateId, "fiendish", StringComparison.OrdinalIgnoreCase))
                    hasFiendishTemplate = true;
            }
        }

        stats.IsCelestialTemplate = hasCelestialTemplate;
        stats.IsFiendishTemplate = hasFiendishTemplate;
        stats.HasTemplateSmiteEvil = def.GainsSmiteEvil;
        stats.HasTemplateSmiteGood = def.GainsSmiteGood;
        stats.TemplateSmiteUsed = false;

        if (def.SpecialAbilities != null)
        {
            for (int i = 0; i < def.SpecialAbilities.Count; i++)
                stats.AddSpecialAbility(def.SpecialAbilities[i]);
        }

        npc.Init(stats, pos, alive, dead);

        CharacterTeam npcTeam = def.IsAlly ? CharacterTeam.Player : CharacterTeam.Enemy;
        npc.ConfigureTeamControl(npcTeam, def.IsControllable);

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

        bool shouldInitSpellcasting = stats.IsSpellcaster
            && ((def.KnownSpellIds != null && def.KnownSpellIds.Count > 0)
                || (def.PreparedSpellSlotIds != null && def.PreparedSpellSlotIds.Count > 0));

        if (shouldInitSpellcasting)
        {
            SpellcastingComponent spellComp = npc.gameObject.GetComponent<SpellcastingComponent>()
                ?? npc.gameObject.AddComponent<SpellcastingComponent>();
            spellComp.KnownSpells.Clear();
            spellComp.SelectedSpellIds = def.KnownSpellIds != null && def.KnownSpellIds.Count > 0
                ? new List<string>(def.KnownSpellIds)
                : null;
            spellComp.PreparedSpellSlotIds = def.PreparedSpellSlotIds != null && def.PreparedSpellSlotIds.Count > 0
                ? new List<string>(def.PreparedSpellSlotIds)
                : null;
            spellComp.Init(stats);

            Debug.Log($"[GameManager] Initialized NPC spellcasting for {def.Name}: {spellComp.GetSlotSummary()}");
        }

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
        npc.EnemyUseCoupDeGraceOverride = def.UseCoupDeGrace;
        npc.PriorityTargetName = string.IsNullOrWhiteSpace(def.AITargetPriority) ? null : def.AITargetPriority;

        Debug.Log($"[GameManager] {def.Name}: HP {stats.MaxHP} AC {stats.ArmorClass} " +
                  $"Atk {CharacterStats.FormatMod(stats.AttackBonus)} Speed {stats.MoveRange}sq " +
                  $"Type={stats.CreatureType} HD={stats.HitDice} BABProg={stats.CreatureBABProgression} " +
                  $"Saves(F/R/W)={stats.ClassFortSave}/{stats.ClassRefSave}/{stats.ClassWillSave}");
    }

    private DND35.AI.AIProfile BuildRuntimeAIProfile(NPCDefinition def)
    {
        if (def == null)
            return null;

        NPCAIProfileArchetype archetype = def.AIProfileArchetype;

        // Legacy fallback for old definitions that don't explicitly set an archetype.
        if (archetype == NPCAIProfileArchetype.None
            && string.Equals(def.CreatureType, "Animal", StringComparison.OrdinalIgnoreCase))
        {
            archetype = NPCAIProfileArchetype.Animal;
        }

        switch (archetype)
        {
            case NPCAIProfileArchetype.Animal:
                return ScriptableObject.CreateInstance<AnimalAIProfile>();
            case NPCAIProfileArchetype.Humanoid:
                return ScriptableObject.CreateInstance<HumanoidAIProfile>();
            case NPCAIProfileArchetype.Berserk:
                return ScriptableObject.CreateInstance<BerserkAIProfile>();
            case NPCAIProfileArchetype.Grappler:
                return ScriptableObject.CreateInstance<GrapplerAIProfile>();
            case NPCAIProfileArchetype.Ranged:
                return ScriptableObject.CreateInstance<RangedAIProfile>();
            case NPCAIProfileArchetype.Healer:
                return ScriptableObject.CreateInstance<HealerAIProfile>();
            case NPCAIProfileArchetype.Spellcaster:
                return ScriptableObject.CreateInstance<SpellcasterAIProfile>();
            case NPCAIProfileArchetype.Evoker:
                return ScriptableObject.CreateInstance<EvokerAIProfile>();
            case NPCAIProfileArchetype.Abjurer:
                return ScriptableObject.CreateInstance<AbjurerAIProfile>();
            case NPCAIProfileArchetype.Necromancer:
                return ScriptableObject.CreateInstance<NecromancerAIProfile>();
            case NPCAIProfileArchetype.UndeadMindless:
                return ScriptableObject.CreateInstance<UndeadMindlessAIProfile>();
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
            if (npc.Team != CharacterTeam.Enemy) continue; // allied/neutral units should not block victory
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
            if (npc.Team != CharacterTeam.Enemy) continue;
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
            if (npc.Team != CharacterTeam.Enemy) continue;
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

        List<CharacterController> forcedFirst = GetForcedFirstInitiativeActors();
        _turnService?.StartCombat(activePCs, activeNPCs, IsPC, forcedFirst);

        string orderStr = _turnService != null ? _turnService.GetInitiativeOrderString() : "No combatants";
        Debug.Log($"[Initiative] Combat begins! Initiative order:\n{orderStr}");

        UpdateInitiativeUI();
    }

    private List<CharacterController> GetForcedFirstInitiativeActors()
    {
        if ((_isWizardSpellTestEncounter || _isClericSpellTestEncounter) && IsActiveCombatant(PC1) && PC1 != null)
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

        // Tick persistent Grease zones/objects.
        TickActiveGreaseEffects();

        // Keep Turn Undead tracker table aligned with condition expiration.
        PruneTurnUndeadTrackers();
        LogOngoingTurnUndeadStatusAtRoundStart();
    }

    private void OnCombatEnded()
    {
        CurrentPhase = TurnPhase.CombatOver;
        ClearAllActiveGreaseEffects();
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

        if (character.HasCondition(CombatConditionType.Dazed))
            return "is dazed";

        if (character.HasCondition(CombatConditionType.Stunned))
            return "is stunned";

        if (character.HasCondition(CombatConditionType.Turned))
            return "is turned";

        if (character.HasCondition(CombatConditionType.Pinned))
            return "is pinned";

        return "cannot act";
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
        ClearPendingNaturalAttackSelection();
        EndGrappleContextMenuDisplayLock();
        CombatUI.HideSummonContextMenu();

        _waitingForAoOConfirmation = false;
        _pendingAoOAction = null;
        _isSelectingWithdraw = false;
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
        _isSelectingWithdraw = false;
        CurrentSubPhase = PlayerSubPhase.Moving;
        ShowMovementRange(pc);
        CombatUI.SetActionButtonsVisible(false);
        CombatUI.SetTurnIndicator($"{pc.Stats.CharacterName} - Click a tile to move (right-click/ESC or own tile to cancel)");
    }

    private static int GetWithdrawMoveRangeSquares(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return 0;

        return Mathf.Max(1, character.Stats.MoveRange * 2);
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

        if (actor.HasCondition(CombatConditionType.Turned))
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

        if (actor.HasCondition(CombatConditionType.Turned))
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
        Debug.Log($"[Attack][Sequence] Final state before teardown: attacksUsed={_totalAttacksUsed}/{_totalAttackBudget}, offHandUsed={_offHandAttackUsedThisTurn}, offHandAvailable={_offHandAttackAvailableThisTurn}");

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

    /// <summary>Called when a spell is chosen from the spell selection panel (with optional metamagic).</summary>
    private void OnSpellSelectedWithMetamagic(SpellData spell, MetamagicData metamagic)
    {
        CharacterController pc = ActivePC;
        if (pc == null) { ShowActionChoices(); return; }

        _pendingSpell = spell;
        _pendingMetamagic = metamagic;
        _pendingSpellFromHeldCharge = false;
        _pendingSummonSelection = null;

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

    private bool IsSummonMonsterSpell(SpellData spell)
    {
        if (spell == null || string.IsNullOrWhiteSpace(spell.SpellId))
            return false;

        return SummonMonsterLists.GetSummonMonsterSpellLevel(spell.SpellId) > 0;
    }

    private List<SummonMonsterOption> GetSummonOptionsForSpell(SpellData spell, CharacterController caster)
    {
        if (spell == null || caster == null || caster.Stats == null)
            return new List<SummonMonsterOption>();

        return SummonMonsterLists.GetFilteredOptions(spell.SpellId, caster.Stats);
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

    private void ShowSummonCreatureSelectionMenu(CharacterController caster, SpellData spell)
    {
        if (caster == null || spell == null)
        {
            ShowActionChoices();
            return;
        }

        var options = GetSummonOptionsForSpell(spell, caster);
        if (options == null || options.Count == 0)
        {
            CombatUI?.ShowCombatLog($"No valid summon options for {spell.Name} ({caster.Stats.CharacterAlignment}).");
            _pendingSpell = null;
            _pendingMetamagic = null;
            _pendingSpellFromHeldCharge = false;
            _pendingSummonSelection = null;
            ShowActionChoices();
            return;
        }

        CombatUI?.ShowSummonCreatureSelection(
            spell.Name,
            options.ConvertAll(o => o.BuildUiLabel()),
            onSelect: idx =>
            {
                if (idx < 0 || idx >= options.Count)
                {
                    ShowActionChoices();
                    return;
                }

                _pendingSummonSelection = options[idx];
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
                ShowActionChoices();
            });
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

        CombatUI.SetTurnIndicator($"✦ {spell.Name}: Place {_pendingSummonSelection.BuildUiLabel()} | Range: {range * 5} ft | Right-click to cancel");
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

    private CharacterController SpawnSummonedCreature(CharacterController caster, Vector2Int cell, SummonMonsterOption option)
    {
        if (caster == null || option == null)
            return null;

        NPCDefinition baseDef = NPCDatabase.Get(option.NpcDefinitionId);
        if (baseDef == null)
        {
            Debug.LogError($"[Summon] Missing NPC definition '{option.NpcDefinitionId}' for summon option '{option.DisplayName}'.");
            return null;
        }

        NPCDefinition summonDef = baseDef.Clone();

        if (summonDef.AppliedTemplateIds == null)
            summonDef.AppliedTemplateIds = new List<string>();
        else
            summonDef.AppliedTemplateIds.Clear();

        if (!string.IsNullOrWhiteSpace(option.TemplateId))
            summonDef.AppliedTemplateIds.Add(option.TemplateId);

        // Apply template mutations (DR/resistances/special abilities/etc.) through the centralized registry.
        summonDef = CreatureTemplateRegistry.ApplyTemplatesClone(summonDef) ?? summonDef;
        summonDef.Id = $"summon_runtime_{option.NpcDefinitionId}";
        summonDef.Name = option.BuildUiLabel();

        bool isCelestial = string.Equals(option.TemplateId, "celestial", StringComparison.OrdinalIgnoreCase);
        bool isFiendish = string.Equals(option.TemplateId, "fiendish", StringComparison.OrdinalIgnoreCase);

        if (summonDef.CreatureTags == null)
            summonDef.CreatureTags = new List<string>();

        if (!summonDef.CreatureTags.Contains("Summoned"))
            summonDef.CreatureTags.Add("Summoned");

        if (isCelestial && !summonDef.CreatureTags.Contains("Good"))
            summonDef.CreatureTags.Add("Good");
        if (isFiendish && !summonDef.CreatureTags.Contains("Evil"))
            summonDef.CreatureTags.Add("Evil");

        GameObject summonGO = new GameObject($"Summon_{option.NpcDefinitionId}_{UnityEngine.Random.Range(1000, 9999)}");
        if (summonGO.GetComponent<SpriteRenderer>() == null)
            summonGO.AddComponent<SpriteRenderer>();

        CharacterController summon = summonGO.AddComponent<CharacterController>();

        string iconKey = IconLoader.DetermineMonsterType(summonDef.Name);
        Sprite alive = !string.IsNullOrEmpty(iconKey) ? IconLoader.GetToken(iconKey) : null;
        if (alive == null)
            alive = LoadSprite("Sprites/npc_enemy_alive");
        Sprite dead = LoadSprite("Sprites/npc_enemy_dead");

        InitializeNPCFromDefinition(summon, summonDef, cell, alive, dead);

        bool alliedToPlayer = caster.Team == CharacterTeam.Player;
        summon.ConfigureTeamControl(alliedToPlayer ? CharacterTeam.Player : CharacterTeam.Enemy, controllable: alliedToPlayer);

        if (summon.Stats != null)
        {
            if (isCelestial)
                summon.Stats.CharacterAlignment = Alignment.NeutralGood;
            else if (isFiendish)
                summon.Stats.CharacterAlignment = Alignment.NeutralEvil;
            else
                summon.Stats.CharacterAlignment = Alignment.TrueNeutral;
        }

        NPCs.Add(summon);
        _npcAIBehaviors.Add(summonDef.AIBehavior);

        if (summon.Team == CharacterTeam.Player)
            _summonedAllies.Add(summon);
        else
            _summonedEnemies.Add(summon);

        var summonVisual = summon.gameObject.GetComponent<SummonedCreatureVisual>();
        if (summonVisual == null)
            summonVisual = summon.gameObject.AddComponent<SummonedCreatureVisual>();
        summonVisual.Init(summon, isCelestial, isFiendish);

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

    private void PerformSummonMonsterCast(CharacterController caster, SquareCell targetCell, SummonMonsterOption option)
    {
        if (caster == null || targetCell == null || option == null || _pendingSpell == null)
        {
            ShowActionChoices();
            return;
        }

        NPCDefinition baseDef = NPCDatabase.Get(option.NpcDefinitionId);
        if (baseDef == null)
        {
            CombatUI?.ShowCombatLog($"Cannot summon {option.DisplayName}: missing creature definition.");
            ShowActionChoices();
            return;
        }

        int summonSizeSquares = baseDef.SizeCategory.GetSpaceWidthSquares();
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

            CharacterController summonCC = SpawnSummonedCreature(caster, targetCell.Coords, option);
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
                RemainingRounds = durationRounds,
                TotalDurationRounds = durationRounds,
                SourceSpellId = _pendingSpell.SpellId,
                IsAlliedToPCs = summonCC.Team == CharacterTeam.Player,
                SmiteUsed = false,
                CurrentCommand = SummonCommand.AttackNearest()
            };
            _activeSummons.Add(activeSummon);

            var visual = summonCC.GetComponent<SummonedCreatureVisual>();
            if (visual != null)
                visual.SetDuration(durationRounds, durationRounds);

            CombatUI.ShowCombatLog($"<color=#66E8FF>✨ {caster.Stats.CharacterName} casts {_pendingSpell.Name} and summons {option.BuildUiLabel()} for {durationRounds} rounds!</color>");

            _pendingSpell = null;
            _pendingMetamagic = null;
            _pendingSpellFromHeldCharge = false;
            _pendingSummonSelection = null;

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

        if (IsPendingGreaseAreaCast())
        {
            EnterGreaseAreaTargetingMode(caster, _pendingSpell);
            return;
        }

        if (IsPendingGreaseArmorCast())
        {
            EnterGreaseArmorTargetingMode(caster, _pendingSpell);
            return;
        }

        // ===== AoE SPELLS: Enter AoE targeting mode =====
        if (_pendingSpell.AoEShapeType != AoEShape.None)
        {
            EnterAoETargetingMode(caster, _pendingSpell);
            return;
        }

        // Summon Monster spells: choose creature first, then select destination tile.
        if (IsSummonMonsterSpell(_pendingSpell))
        {
            ShowSummonCreatureSelectionMenu(caster, _pendingSpell);
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

    private int GetTargetHitDice(CharacterController target)
    {
        if (target?.Stats == null) return 0;
        return Mathf.Max(1, target.Stats.HitDice > 0 ? target.Stats.HitDice : target.Stats.Level);
    }

    private bool IsImmuneToMindAffecting(CharacterController target)
    {
        if (target?.Stats == null) return false;

        string creatureType = string.IsNullOrWhiteSpace(target.Stats.CreatureType)
            ? string.Empty
            : target.Stats.CreatureType.Trim().ToLowerInvariant();

        if (creatureType == "undead" || creatureType == "construct" || creatureType == "ooze" || creatureType == "plant" || creatureType == "vermin")
            return true;

        if (target.Stats.SpecialAbilities != null)
        {
            for (int i = 0; i < target.Stats.SpecialAbilities.Count; i++)
            {
                string trait = target.Stats.SpecialAbilities[i];
                if (string.IsNullOrWhiteSpace(trait))
                    continue;

                string normalized = trait.ToLowerInvariant();
                if (normalized.Contains("mind-affect") || normalized.Contains("mind affecting") || normalized.Contains("mindless"))
                    return true;
            }
        }

        return false;
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

        if (spell.SpellId == "daze")
        {
            // D&D 3.5e Daze: one humanoid creature of 4 HD or less.
            // Protection-from-Evil benchmark scenario intentionally allows Daze against the level 10 test wizard
            // so we can validate save bonus behavior (+2 vs evil, no bonus vs neutral).
            if (!IsEnemyTeam(caster, target)) return false;
            if (!IsHumanoid(target)) return false;
            if (!_isProtectionFromEvilTestEncounter && GetTargetHitDice(target) > 4) return false;
            if (IsImmuneToMindAffecting(target)) return false;
            return true;
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

    private bool HasActiveShieldSpell(CharacterController target)
    {
        if (target == null)
            return false;

        StatusEffectManager statusMgr = target.GetComponent<StatusEffectManager>();
        if (statusMgr != null)
        {
            foreach (ActiveSpellEffect effect in statusMgr.ActiveEffects)
            {
                if (effect?.Spell == null)
                    continue;

                if (string.Equals(effect.Spell.SpellId, "shield", StringComparison.OrdinalIgnoreCase)
                    && effect.RemainingRounds > 0)
                {
                    return true;
                }
            }
        }

        SpellcastingComponent spellComp = target.GetComponent<SpellcastingComponent>();
        if (spellComp != null
            && spellComp.ActiveBuffs != null
            && spellComp.ActiveBuffs.TryGetValue("shield", out int rounds)
            && rounds > 0)
        {
            return true;
        }

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

        int range = spell.GetRangeSquaresForCasterLevel(caster?.Stats?.Level ?? 0);
        if (range <= 0) range = 1; // Touch/self spells = adjacent (1 square for targeting)


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
            string rangeStr = spell.RangeSquares <= 0 ? "Touch" : $"{range} sq ({range * 5} ft)";
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

            bool effectNegatedBySave = _pendingSpell.EffectType == SpellEffectType.Debuff && result.RequiredSave && result.SaveSucceeded;
            if (effectNegatedBySave)
            {
                CombatUI?.ShowCombatLog($"🛡 {target.Stats.CharacterName} resists {_pendingSpell.Name} with a successful {result.SaveType} save.");
            }

            if (result.MindAffectingImmunityBlocked)
            {
                CombatUI?.ShowCombatLog($"🧠 {target.Stats.CharacterName} is immune to mind-affecting effects. {_pendingSpell.Name} has no effect.");
            }

            if (result.Success && appliesTrackedEffect && !effectNegatedBySave)
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
                    ResetPendingGreaseCastMode();
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
                    ResetPendingGreaseCastMode();
                    return;
                }
            }

            _pendingSpell = null;
            _pendingSpellFromHeldCharge = false;
            _pendingMetamagic = null;
            ResetPendingGreaseCastMode();

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
            bool casterIsPC = caster.Team == CharacterTeam.Player;
            CharacterTeam enemyTeamType = caster.Team == CharacterTeam.Player ? CharacterTeam.Enemy : CharacterTeam.Player;
            List<CharacterController> allyTeam = GetTeamMembers(caster.Team);
            List<CharacterController> enemyTeam = GetTeamMembers(enemyTeamType);
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
            int range = spell.AoERangeSquares > 0
                ? spell.AoERangeSquares
                : spell.GetRangeSquaresForCasterLevel(caster?.Stats?.Level ?? 0);
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

        if (TryUpdateGreaseAreaPreview(pc, worldPoint))
            return;

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

                int range = _pendingSpell.AoERangeSquares > 0
                    ? _pendingSpell.AoERangeSquares
                    : _pendingSpell.GetRangeSquaresForCasterLevel(pc?.Stats?.Level ?? 0);
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
                int range = _pendingSpell.AoERangeSquares > 0
                    ? _pendingSpell.AoERangeSquares
                    : _pendingSpell.GetRangeSquaresForCasterLevel(pc?.Stats?.Level ?? 0);
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

        if (TryHandleGreaseAreaTargetClick(caster, clickedCell))
            return;

        Vector2Int targetPos = clickedCell.Coords;

        // Validate range for burst spells
        if (_pendingSpell.AoEShapeType == AoEShape.Burst)
        {
            int range = _pendingSpell.AoERangeSquares > 0
                ? _pendingSpell.AoERangeSquares
                : _pendingSpell.GetRangeSquaresForCasterLevel(caster?.Stats?.Level ?? 0);
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
        bool casterIsPC = caster.Team == CharacterTeam.Player;
        CharacterTeam enemyTeamType = caster.Team == CharacterTeam.Player ? CharacterTeam.Enemy : CharacterTeam.Player;
        List<CharacterController> allyTeam = GetTeamMembers(caster.Team);
        List<CharacterController> enemyTeam = GetTeamMembers(enemyTeamType);
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
        ResetPendingGreaseCastMode();

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
        _pendingSummonSelection = null;
        ResetPendingGreaseCastMode();
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
        ClearPendingNaturalAttackSelection();
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

            if (TryHandleConcealmentAreaSpellCast(caster, _pendingSpell, aoeCells, targets, out string concealmentAreaLog))
            {
                _lastCombatLog = concealmentAreaLog;

                if (isSpontaneous)
                {
                    string sacrificeInfo = !string.IsNullOrEmpty(spontaneousSacrificedSpellId)
                        ? $"Sacrificed: {spontaneousSacrificedSpellId}"
                        : "Converted prepared spell";
                    _lastCombatLog = $"⟳ {caster.Stats.CharacterName} spontaneously casts {_pendingSpell.Name}! ({sacrificeInfo})\n" + _lastCombatLog;
                }

                if (isQuickened)
                    _lastCombatLog = $"⚡ {caster.Stats.CharacterName} casts QUICKENED {_pendingSpell.Name}! (Free Action)\n" + _lastCombatLog;

                CombatUI.ShowCombatLog(_lastCombatLog);
                UpdateAllStatsUI();
                Grid.ClearAllHighlights();

                _pendingSpell = null;
                _pendingMetamagic = null;
                StartCoroutine(AfterAttackDelay(caster, 1.5f));
                return;
            }

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
        if (spell != null && spell.SpellId == "daze")
        {
            int dazeRounds = Mathf.Max(1, spell.BuffDurationRounds > 0 ? spell.BuffDurationRounds : 1);
            string sourceName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name;
            target.ApplyCondition(CombatConditionType.Dazed, dazeRounds, sourceName);
            CombatUI?.ShowCombatLog($"<color=#FFCC66>💫 {target.Stats.CharacterName} is dazed for {dazeRounds} round(s)!</color>");
            Debug.Log($"[GameManager] Daze applied to {target.Stats.CharacterName} for {dazeRounds} round(s)");
            return null;
        }

        if (spell != null && spell.SpellId == "flare")
        {
            int dazzledRounds = Mathf.Max(1, spell.BuffDurationRounds > 0 ? spell.BuffDurationRounds : 10);
            string sourceName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name;
            target.ApplyCondition(CombatConditionType.Dazzled, dazzledRounds, sourceName);
            CombatUI?.ShowCombatLog($"<color=#FFCC66>✨ {target.Stats.CharacterName} is dazzled (-1 attack, Spot, and Search) for {dazzledRounds} round(s)!</color>");
            Debug.Log($"[GameManager] Flare applied Dazzled to {target.Stats.CharacterName} for {dazzledRounds} round(s)");
            return null;
        }

        if (spell != null && spell.SpellId == "touch_of_fatigue")
        {
            string sourceName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name;
            int casterLevel = caster != null && caster.Stats != null
                ? Mathf.Max(1, caster.Stats.GetCasterLevel())
                : 1;

            bool wasExhausted = target.HasCondition(CombatConditionType.Exhausted);
            if (wasExhausted)
            {
                CombatUI?.ShowCombatLog($"<color=#FFCC66>💤 {target.Stats.CharacterName} is already exhausted. Touch of Fatigue has no effect.</color>");
                Debug.Log($"[GameManager] Touch of Fatigue had no effect on {target.Stats.CharacterName} (already exhausted)");
                return null;
            }

            bool wasFatigued = target.HasCondition(CombatConditionType.Fatigued);
            target.ApplyCondition(CombatConditionType.Fatigued, casterLevel, sourceName);

            bool isNowExhausted = target.HasCondition(CombatConditionType.Exhausted);
            if (wasFatigued && isNowExhausted)
            {
                CombatUI?.ShowCombatLog($"<color=#FF9966>🥵 {target.Stats.CharacterName} becomes exhausted for {casterLevel} round(s)!</color>");
                Debug.Log($"[GameManager] Touch of Fatigue escalated {target.Stats.CharacterName} to Exhausted for {casterLevel} rounds");
            }
            else
            {
                CombatUI?.ShowCombatLog($"<color=#FFCC66>😫 {target.Stats.CharacterName} is fatigued for {casterLevel} round(s)!</color>");
                Debug.Log($"[GameManager] Touch of Fatigue applied Fatigued to {target.Stats.CharacterName} for {casterLevel} rounds");
            }

            return null;
        }

        // Use StatusEffectManager for tracked buff application
        var statusMgr = target.GetComponent<StatusEffectManager>();
        if (statusMgr != null)
        {
            // Defensive rebind: some encounter presets reinitialize CharacterStats objects on existing
            // character GameObjects, so the manager must always point at the current stats instance.
            statusMgr.Init(target.Stats);

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
        ResetPendingGreaseCastMode();

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
        ResetPendingGreaseCastMode();

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

    private void ShowMovementRange(CharacterController pc, int maxRangeOverride = -1)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        if (_movementService == null || pc == null)
            return;

        List<SquareCell> moveCells = _movementService.CalculateMovementRange(pc, maxRangeOverride);
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
            : (_isFreeAdjacentGrappleMoveSelection ? 1 : (_isSelectingWithdraw ? GetWithdrawMoveRangeSquares(pc) : pc.Stats.MoveRange));
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
            int segmentIndex = 0;
            foreach (var step in pathResult.Path)
            {
                // A segment is "dangerous" if we're leaving a threatened square.
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
                case PendingAttackMode.TemplateSmite: modeStr = "SMITE"; break;
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

        int movementRangeOverride = _isSelectingWithdraw ? GetWithdrawMoveRangeSquares(pc) : -1;
        bool suppressFirstSquareAoO = _isSelectingWithdraw;
        var pathResult = _movementService != null
            ? _movementService.FindPath(
                pc,
                cell.Coords,
                avoidThreats: true,
                maxRangeOverride: movementRangeOverride > 0 ? movementRangeOverride : (int?)null,
                allowThroughAllies: true,
                allowThroughEnemies: false,
                suppressFirstSquareAoO: suppressFirstSquareAoO)
            : Grid.FindSafePath(pc.GridPosition, cell.Coords, pc, GetAllCharacters());

        if (pathResult == null || pathResult.Path == null || pathResult.Path.Count == 0)
        {
            CombatUI?.ShowCombatLog("⚠ No valid movement path to that tile.");
            return;
        }

        if (!pathResult.ProvokesAoOs)
        {
            StartCoroutine(ExecuteMovement(pc, new List<Vector2Int>(pathResult.Path), isWithdraw: _isSelectingWithdraw));
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
            OnProceed = () => StartCoroutine(ResolveAoOsAndMove(pc, pathResult, isWithdraw: _isSelectingWithdraw)),
            OnCancel = () =>
            {
                CurrentSubPhase = PlayerSubPhase.Moving;
                if (_isGrappleMoveSelection)
                    ShowGrappleMoveRange(pc);
                else
                    ShowMovementRange(pc, _isSelectingWithdraw ? GetWithdrawMoveRangeSquares(pc) : -1);
                CombatUI.SetActionButtonsVisible(false);
                CombatUI.SetTurnIndicator(_isGrappleMoveSelection
                    ? $"{pc.Stats.CharacterName} - Move while grappling: choose destination within half speed ({_grappleMoveMaxRangeSquares} sq)"
                    : (_isSelectingWithdraw
                        ? $"{pc.Stats.CharacterName} - Withdraw: select destination (double move, first square avoids AoO)"
                        : $"{pc.Stats.CharacterName} - Click a tile to move (right-click/ESC or own tile to cancel)"));
            }
        });
    }

    private IEnumerator ResolveAoOsAndMove(CharacterController pc, AoOPathResult pathResult, bool isWithdraw = false)
    {
        if (pc == null || pc.Stats == null)
            yield break;

        CurrentSubPhase = PlayerSubPhase.Animating;

        List<Vector2Int> path = (pathResult != null && pathResult.Path != null && pathResult.Path.Count > 0)
            ? pathResult.Path
            : new List<Vector2Int>();

        List<AoOThreatInfo> provokedAoOs = (pathResult != null && pathResult.ProvokedAoOs != null)
            ? pathResult.ProvokedAoOs
            : null;

        yield return StartCoroutine(ExecuteMovement(pc, path, isWithdraw, provokedAoOs));
    }

    private IEnumerator ExecuteMovement(CharacterController pc, List<Vector2Int> path, bool isWithdraw = false, List<AoOThreatInfo> provokedAoOs = null)
    {
        if (pc == null || pc.Stats == null || path == null || path.Count == 0)
            yield break;

        CurrentSubPhase = PlayerSubPhase.Animating;

        // Hide path preview and hover marker immediately when movement begins
        if (_pathPreview != null) _pathPreview.HidePath();
        if (_hoverMarker != null) _hoverMarker.Hide();

        bool consumedAction = false;
        if (isWithdraw)
        {
            if (pc.Actions.HasFullRoundAction)
            {
                pc.Actions.UseFullRoundAction();
                consumedAction = true;
            }
            else
            {
                CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot complete Withdraw without a full-round action.");
                ShowActionChoices();
                yield break;
            }

            pc.IsWithdrawing = true;
            pc.WithdrawFirstStepProtected = true;
        }
        else
        {
            if (pc.Actions.HasMoveAction)
            {
                pc.Actions.UseMoveAction();
                consumedAction = true;
            }
            else if (pc.Actions.CanConvertStandardToMove)
            {
                pc.Actions.ConvertStandardToMove();
                consumedAction = true;
            }
        }

        if (!consumedAction)
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} has no action available for movement.");
            ShowActionChoices();
            yield break;
        }

        bool interruptedByIncapacitation = false;
        bool interruptedByGreaseSlip = false;
        int movementBudgetSquares = isWithdraw ? GetWithdrawMoveRangeSquares(pc) : Mathf.Max(1, pc.Stats.MoveRange);
        int movementCostConsumed = 0;
        Vector2Int previousCell = pc.GridPosition;

        for (int pathIndex = 0; pathIndex < path.Count; pathIndex++)
        {
            Vector2Int step = path[pathIndex];
            int stepCost = 1 + GetGreaseAreaExtraMovementCost(pc, step);
            if (movementCostConsumed + stepCost > movementBudgetSquares)
            {
                CombatUI?.ShowCombatLog($"🛢 {pc.Stats.CharacterName} cannot move farther this action (grease slows movement).");
                break;
            }

            var stepPath = new List<Vector2Int> { step };

            if (_movementService != null)
                yield return StartCoroutine(_movementService.ExecuteMovement(pc, stepPath, PlayerMoveSecondsPerStep, markAsMoved: false));
            else
                yield return StartCoroutine(pc.MoveAlongPath(stepPath, PlayerMoveSecondsPerStep, markAsMoved: false));

            movementCostConsumed += stepCost;

            if (provokedAoOs != null && provokedAoOs.Count > 0)
            {
                for (int aooIndex = 0; aooIndex < provokedAoOs.Count; aooIndex++)
                {
                    AoOThreatInfo aooInfo = provokedAoOs[aooIndex];
                    if (aooInfo == null || aooInfo.PathIndex != pathIndex)
                        continue;

                    CharacterController threatener = aooInfo.Threatener;
                    if (threatener == null || threatener.Stats == null || threatener.Stats.IsDead)
                        continue;

                    CombatResult aooResult = _movementService != null
                        ? _movementService.TriggerAoO(threatener, pc)
                        : ThreatSystem.ExecuteAoO(threatener, pc);
                    if (aooResult == null)
                        continue;

                    string aooLog = $"⚔ AoO: {aooResult.GetDetailedSummary()}";
                    CombatUI?.ShowCombatLog(aooLog);
                    UpdateAllStatsUI();

                    if (LogAttacksToConsole)
                        Debug.Log("[Combat] " + aooLog);

                    if (aooResult.Hit && aooResult.TotalDamage > 0)
                        CheckConcentrationOnDamage(pc, aooResult.TotalDamage);

                    if (pc.IsUnconscious || pc.Stats.IsDead)
                    {
                        interruptedByIncapacitation = true;
                        break;
                    }

                    yield return new WaitForSeconds(1.0f);
                }

                if (interruptedByIncapacitation)
                    break;
            }

            if (!HandleGreaseStepAfterMovement(pc, previousCell, step))
            {
                interruptedByGreaseSlip = true;
                break;
            }

            previousCell = step;
        }

        if (movementCostConsumed > 0)
            pc.HasMovedThisTurn = true;

        CheckTurnUndeadProximityBreakingForMover(pc);
        PruneTurnUndeadTrackers();
        UpdateAllStatsUI();

        if (isWithdraw)
        {
            pc.WithdrawFirstStepProtected = false;
            if (!interruptedByIncapacitation)
                CombatUI?.ShowCombatLog($"↩ {pc.Stats.CharacterName} completes Withdraw.");
        }

        InvalidatePreviewThreats();

        if (interruptedByIncapacitation)
        {
            CombatUI?.ShowCombatLog($"⛔ {pc.Stats.CharacterName}'s movement stops immediately due to incapacitation.");

            if (AreAllPCsDead())
            {
                CurrentPhase = TurnPhase.CombatOver;
                CombatUI.SetTurnIndicator("DEFEAT! All heroes have fallen!");
                CombatUI.SetActionButtonsVisible(false);
                yield break;
            }

            EndActivePCTurn();
            yield break;
        }

        if (interruptedByGreaseSlip)
            CombatUI?.ShowCombatLog($"🛢 {pc.Stats.CharacterName}'s movement ends after slipping in grease.");

        ShowActionChoices();
    }

    private IEnumerator MoveCharacterAlongComputedPathWithdraw(CharacterController mover, Vector2Int destination, float secondsPerStep)
    {
        if (mover == null || mover.Stats == null || Grid == null)
            yield break;

        if (!mover.Actions.HasFullRoundAction)
            yield break;

        if (destination == mover.GridPosition)
            yield break;

        int maxRange = GetWithdrawMoveRangeSquares(mover);
        AoOPathResult pathResult = _movementService != null
            ? _movementService.FindPath(
                mover,
                destination,
                avoidThreats: false,
                maxRangeOverride: maxRange,
                allowThroughAllies: true,
                allowThroughEnemies: false,
                suppressFirstSquareAoO: true)
            : Grid.FindPathAoOAware(mover.GridPosition, destination, null, maxRange, mover.GetVisualSquaresOccupied(), mover);

        List<Vector2Int> path = (pathResult != null && pathResult.Path != null && pathResult.Path.Count > 0)
            ? pathResult.Path
            : null;

        if (path == null || path.Count == 0)
            yield break;

        mover.Actions.UseFullRoundAction();
        mover.IsWithdrawing = true;
        mover.WithdrawFirstStepProtected = true;

        if (pathResult != null && pathResult.ProvokedAoOs != null)
        {
            foreach (var aooInfo in pathResult.ProvokedAoOs)
            {
                if (mover.Stats.IsDead)
                    break;

                CharacterController threatener = aooInfo != null ? aooInfo.Threatener : null;
                if (threatener == null || threatener.Stats == null || threatener.Stats.IsDead)
                    continue;

                CombatResult aooResult = _movementService != null
                    ? _movementService.TriggerAoO(threatener, mover)
                    : ThreatSystem.ExecuteAoO(threatener, mover);

                if (aooResult != null)
                    CombatUI?.ShowCombatLog($"⚔ AoO (Withdraw): {aooResult.GetDetailedSummary()}");

                yield return new WaitForSeconds(0.35f);
            }
        }

        if (!mover.Stats.IsDead)
        {
            if (_movementService != null)
                yield return StartCoroutine(_movementService.ExecuteMovement(mover, path, secondsPerStep, markAsMoved: true));
            else
                yield return StartCoroutine(mover.MoveAlongPath(path, secondsPerStep, markAsMoved: true));

            CheckTurnUndeadProximityBreakingForMover(mover);
            PruneTurnUndeadTrackers();
        }

        mover.WithdrawFirstStepProtected = false;
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
            // Summon Monster spells: creature was selected first, now place it on an empty highlighted tile.
            if (IsSummonMonsterSpell(_pendingSpell))
            {
                if (_pendingSummonSelection == null)
                {
                    ShowSummonCreatureSelectionMenu(pc, _pendingSpell);
                    return;
                }

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

                PerformSummonMonsterCast(pc, cell, _pendingSummonSelection);
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
                ResetPendingGreaseCastMode();
                ShowActionChoices();
                return;
            }

            // Valid target click
            if (cell.IsOccupied && !cell.Occupant.Stats.IsDead)
            {
                if (IsPendingGreaseObjectCast())
                {
                    PerformGreaseObjectCast(pc, cell.Occupant);
                    return;
                }

                if (IsPendingGreaseArmorCast())
                {
                    PerformGreaseArmorCast(pc, cell.Occupant);
                    return;
                }

                PerformSpellCast(pc, cell.Occupant);
                return;
            }

            // Fallback cancel
            _pendingSpell = null;
            _pendingMetamagic = null;
            _pendingSpellFromHeldCharge = false;
            ResetPendingGreaseCastMode();
            ShowActionChoices();
            return;
        }

        if (_pendingAttackMode == PendingAttackMode.TemplateSmite)
        {
            if (!cell.IsOccupied || cell.Occupant == null || cell.Occupant == pc || cell.Occupant.Stats == null || cell.Occupant.Stats.IsDead || !_highlightedCells.Contains(cell))
            {
                CombatUI?.ShowCombatLog("Select a highlighted valid smite target.");
                return;
            }

            ExecuteTemplateSmiteAttack(pc, cell.Occupant);
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

        if (_weaponAttacksCommittedThisTurn >= 1 && !_attackSequenceConsumesFullRound)
        {
            if (!TryEnterProgressiveFullAttackStage(attacker, useThrownRange ? "an off-hand thrown attack" : "an off-hand attack"))
            {
                _isSelectingOffHandTarget = false;
                _isSelectingOffHandThrownTarget = false;
                _currentOffHandBAB = 0;
                _currentOffHandWeapon = null;
                ShowActionChoices();
                return;
            }
        }
        else if (!_isInAttackSequence)
        {
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

        if (result != null)
            RegisterWeaponAttackCommitted(attacker);

        if (result != null && result.Hit && result.TotalDamage > 0)
            CheckConcentrationOnDamage(target, result.TotalDamage);

        if (result != null && result.TargetKilled)
        {
            HandleSummonDeathCleanup(target);

            if (target.Team == CharacterTeam.Enemy)
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

        int maxRange = (type == SpecialAttackType.Feint || type == SpecialAttackType.CoupDeGrace)
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
            bool inRange = (type == SpecialAttackType.Feint || type == SpecialAttackType.CoupDeGrace)
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

            if (type == SpecialAttackType.CoupDeGrace)
            {
                bool helplessTarget = c.Occupant.IsHelplessForCoupDeGrace() && !c.Occupant.IsImmuneToCriticalHits();
                c.SetHighlight(helplessTarget ? HighlightType.Attack : HighlightType.AttackDeadZone);
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
            else if (type == SpecialAttackType.CoupDeGrace)
                CombatUI.SetTurnIndicator("SPECIAL: Coup de Grace - red targets are helpless and vulnerable to critical hits (Right-click/Esc to cancel)");
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
            || type == SpecialAttackType.Overrun
            || type == SpecialAttackType.CoupDeGrace;
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
        else if (type == SpecialAttackType.CoupDeGrace)
        {
            if (!target.IsHelplessForCoupDeGrace())
            {
                CombatUI?.ShowCombatLog($"⚠ {target.Stats.CharacterName} is not helpless; Coup de Grace cannot be performed.");
                ShowActionChoices();
                return;
            }

            if (target.IsImmuneToCriticalHits())
            {
                CombatUI?.ShowCombatLog($"⚠ {target.Stats.CharacterName} is immune to critical hits and cannot be coup de graced.");
                ShowActionChoices();
                return;
            }

            if (!attacker.Actions.HasFullRoundAction)
            {
                CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} cannot perform Coup de Grace: full-round action already spent.");
                ShowActionChoices();
                return;
            }

            attacker.Actions.UseFullRoundAction();
            actionLabel = "full-round action";
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

        bool maneuverProvokesAoO = type == SpecialAttackType.Grapple || type == SpecialAttackType.Sunder || type == SpecialAttackType.CoupDeGrace;
        if (maneuverProvokesAoO)
        {
            bool attackerIgnoresAoO = false;
            string maneuverLabel = type == SpecialAttackType.Grapple
                ? "Grapple"
                : (type == SpecialAttackType.Sunder ? "Sunder" : "Coup de Grace");

            if (type == SpecialAttackType.Grapple)
                attackerIgnoresAoO = attacker.Stats != null && attacker.Stats.HasFeat("Improved Grapple");
            else if (type == SpecialAttackType.Sunder)
                attackerIgnoresAoO = attacker.Stats != null && attacker.Stats.HasFeat("Improved Sunder");

            if (!attackerIgnoresAoO)
            {
                var provokingEnemies = new List<CharacterController>();

                if (type == SpecialAttackType.Grapple || type == SpecialAttackType.Sunder)
                {
                    if (target != null && target.Stats != null && !target.Stats.IsDead)
                        provokingEnemies.Add(target);
                }
                else
                {
                    provokingEnemies = ThreatSystem.GetThreateningEnemies(attacker.GridPosition, attacker, GetAllCharacters());
                }

                provokingEnemies.RemoveAll(enemy => enemy == null || enemy.Stats == null || enemy.Stats.IsDead || !ThreatSystem.CanMakeAoO(enemy));

                for (int i = 0; i < provokingEnemies.Count; i++)
                {
                    CharacterController enemy = provokingEnemies[i];
                    CombatResult maneuverAoO = ThreatSystem.ExecuteAoO(enemy, attacker);
                    if (maneuverAoO == null)
                        continue;

                    CombatUI.ShowCombatLog($"⚔ {maneuverLabel} initiation AoO: {maneuverAoO.GetDetailedSummary()}");
                    UpdateAllStatsUI();

                    if (type != SpecialAttackType.CoupDeGrace && maneuverAoO.Hit)
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

                    if (attacker.Stats.IsDead || attacker.IsUnconscious)
                    {
                        CombatUI.ShowCombatLog($"💀 {attacker.Stats.CharacterName} is incapacitated while attempting to start {maneuverLabel.ToLowerInvariant()}.");
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

        if (target != null && target.Stats != null && target.Stats.IsDead && target.Team == CharacterTeam.Enemy && AreAllNPCsDead())
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

            TryResolveFreeTripFromAttackResults(attacker, currentTarget, stepResult.Attacks, rangeInfo);

            if (attack.TargetKilled)
            {
                HandleSummonDeathCleanup(currentTarget);

                if (currentTarget.Team == CharacterTeam.Enemy && AreAllNPCsDead())
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

        if (character.IsControllable)
        {
            bool offHandAvailable = CanUseOffHandAttackOption(character);
            bool offHandThrownAvailable = CanUseOffHandThrownAttackOption(character);
            Debug.Log($"[TurnFlow] ShouldAutoEndTurn=false for controllable unit {character.Stats.CharacterName}. " +
                      $"Manual End Turn required. offHandAvailable={offHandAvailable} offHandThrownAvailable={offHandThrownAvailable} " +
                      $"offHandGate={_offHandAttackAvailableThisTurn} offHandUsed={_offHandAttackUsedThisTurn} attacksUsed={_totalAttacksUsed}/{_totalAttackBudget}");
            return false;
        }

        bool hasRemainingGrappleAttempts = CanUseGrappleAttackOption(character);
        bool hasRemainingBullRushAttempts = CanUseBullRushAttackOption(character);
        bool hasRemainingTripAttempts = CanUseTripAttackOption(character);
        bool hasRemainingDisarmAttempts = CanUseDisarmAttackOption(character);
        bool hasRemainingCoupDeGraceAttempt = CanUseCoupDeGraceAttackOption(character);

        bool hasIterativeWeaponAttackSequence = _isInAttackSequence && _attackingCharacter == character;

        if (hasRemainingGrappleAttempts || hasRemainingBullRushAttempts || hasRemainingTripAttempts || hasRemainingDisarmAttempts || hasRemainingCoupDeGraceAttempt || hasIterativeWeaponAttackSequence)
        {
            Debug.Log(
                $"[TurnFlow] ShouldAutoEndTurn=false for {character.Stats.CharacterName}: " +
                $"iterativeRemaining(g={hasRemainingGrappleAttempts}, br={hasRemainingBullRushAttempts}, trip={hasRemainingTripAttempts}, d={hasRemainingDisarmAttempts}, cdg={hasRemainingCoupDeGraceAttempt}, atk={hasIterativeWeaponAttackSequence})");
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
                string reason = GetUnableToActReason(npc);
                CombatUI?.ShowCombatLog($"⏭ {npc.Stats.CharacterName} {reason} and cannot act this turn.");
            }
            NextInitiativeTurn();
            yield break;
        }

        // Determine AI behavior for this NPC
        NPCAIBehavior behavior = GetNPCBehaviorForAI(npc);
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

        if (summon.Stats != null && summon.Stats.HasTripAttack && !target.Stats.IsProne && summon.Actions.HasStandardAction)
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
        if (summon == null || target == null || summonData == null)
            return false;
        if (summonData.SmiteUsed || (summon.Stats != null && summon.Stats.TemplateSmiteUsed))
            return false;
        if (!summon.Actions.HasStandardAction)
            return false;

        bool smiteEvil = summon.Stats.HasTemplateSmiteEvil && AlignmentHelper.IsEvil(target.Stats.CharacterAlignment);
        bool smiteGood = summon.Stats.HasTemplateSmiteGood && AlignmentHelper.IsGood(target.Stats.CharacterAlignment);
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
        summon.Stats.TemplateSmiteUsed = true;

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

        bool isMeleeHit = attackRange != null
            ? attackRange.IsMelee
            : !attackResult.IsRangedAttack;
        if (!isMeleeHit)
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

    private void TryResolveFreeTripFromAttackResults(CharacterController attacker, CharacterController target, List<CombatResult> attacks, RangeInfo attackRange)
    {
        if (attacker == null || target == null || attacks == null || attacks.Count == 0)
            return;

        for (int i = 0; i < attacks.Count; i++)
        {
            CombatResult attackResult = attacks[i];
            TryResolveFreeTripOnHit(attacker, target, attackResult, attackRange);

            if (target.Stats == null || target.Stats.IsDead || target.HasCondition(CombatConditionType.Prone))
                break;
        }
    }

    private bool TryNPCSpecialAttackIfBeneficial(CharacterController npc, CharacterController target)
    {
        return TryNPCSpecialAttackIfBeneficial(npc, target, null);
    }

    private bool TryNPCSpecialAttackIfBeneficial(CharacterController npc, CharacterController target, SpecialAttackType? forcedChoice)
    {
        if (npc == null || target == null)
            return false;

        bool hasImprovedGrab = npc.Stats != null && npc.Stats.HasImprovedGrab;
        var coupTargets = GetAdjacentHelplessEnemiesForCoupDeGrace(npc);
        bool profileAllowsCoupDeGrace = npc.EnemyUseCoupDeGraceOverride
            ?? (npc.aiProfile != null && npc.aiProfile.ShouldUseCoupDeGrace(npc));
        bool hasCoupOption = profileAllowsCoupDeGrace
            && coupTargets.Count > 0
            && npc.Actions != null
            && npc.Actions.HasFullRoundAction;

        if (npc.IsGrappling() && (!forcedChoice.HasValue || forcedChoice.Value != SpecialAttackType.CoupDeGrace))
            return false;

        if (!npc.Actions.HasStandardAction && !hasCoupOption)
            return false;

        SpecialAttackType? choice = forcedChoice;

        if (choice == SpecialAttackType.Grapple && hasImprovedGrab)
        {
            string npcName = npc.Stats != null ? npc.Stats.CharacterName : "<unknown>";
            Debug.Log($"[AI][SpecialAttack] {npcName} has Improved Grab; refusing forced standard Grapple action.");
            return false;
        }

        if (!choice.HasValue)
        {
            if (hasCoupOption)
                choice = SpecialAttackType.CoupDeGrace;
            else if (!target.Stats.IsProne && npc.HasMeleeWeaponEquipped())
                choice = SpecialAttackType.Trip;

            if (choice == null && target.GetEquippedMainWeapon() != null && npc.Stats.STRMod >= 3)
                choice = SpecialAttackType.Disarm;

            if (choice == null && npc.Stats.STRMod >= 4 && !hasImprovedGrab)
                choice = SpecialAttackType.Grapple;
        }

        if (choice == null)
            return false;

        if (choice.Value == SpecialAttackType.CoupDeGrace)
        {
            if (!hasCoupOption)
                return false;

            CharacterController coupTarget = (target != null && coupTargets.Contains(target))
                ? target
                : coupTargets[0];
            target = coupTarget;
        }

        var result = npc.ExecuteSpecialAttack(choice.Value, target);
        CombatUI.ShowCombatLog($"☠ {npc.Stats.CharacterName} uses SPECIAL [{choice.Value}]! {result.Log}");

        if (result.Success)
        {
            if (choice.Value == SpecialAttackType.BullRushAttack || choice.Value == SpecialAttackType.BullRushCharge)
                ResolveBullRushPushAndFollow(npc, target, result, onComplete: null);
            else if (choice.Value == SpecialAttackType.Overrun)
                TryPushTargetAway(npc, target, 1, allowAttackerFollow: true);
        }

        if (choice.Value == SpecialAttackType.CoupDeGrace)
            npc.Actions.UseFullRoundAction();
        else
            npc.CommitStandardAction();

        UpdateAllStatsUI();
        return true;
    }

    private bool ResolveRangedAttackAoOForNPCAttackIfProvoked(CharacterController attacker, RangeInfo rangeInfo)
    {
        if (attacker == null || attacker.Stats == null || attacker.Stats.IsDead)
            return true;

        bool isRangedOrThrownAttack = rangeInfo != null
            ? !rangeInfo.IsMelee
            : (attacker.IsEquippedWeaponRanged() || (attacker.GetEquippedMainWeapon()?.IsThrown ?? false));

        if (!isRangedOrThrownAttack)
            return true;

        List<CharacterController> threateningEnemies = ThreatSystem.GetThreateningEnemies(
            attacker.GridPosition,
            attacker,
            GetAllCharacters());

        threateningEnemies.RemoveAll(enemy => enemy == null || enemy.Stats == null || enemy.Stats.IsDead);

        if (threateningEnemies.Count == 0)
            return true;

        CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} makes a ranged attack while threatened and provokes up to {threateningEnemies.Count} attack(s) of opportunity.");

        foreach (CharacterController enemy in threateningEnemies)
        {
            if (!ThreatSystem.CanMakeAoO(enemy))
            {
                Debug.Log($"[AOO-DEBUG] {enemy?.Stats?.CharacterName ?? "<unknown>"} cannot make AoO now (used {enemy?.Stats?.AttacksOfOpportunityUsed}/{enemy?.Stats?.MaxAttacksOfOpportunity}).");
                continue;
            }

            CombatResult aooResult = ThreatSystem.ExecuteAoO(enemy, attacker);
            if (aooResult == null)
            {
                Debug.Log($"[AOO-DEBUG] ExecuteAoO returned null for {enemy?.Stats?.CharacterName ?? "<unknown>"} vs {attacker.Stats.CharacterName}.");
                continue;
            }

            CombatUI?.ShowCombatLog($"⚔ AoO vs ranged attack: {aooResult.GetDetailedSummary()}");
        }

        if (attacker.Stats.IsDead)
        {
            CombatUI?.ShowCombatLog($"<color=#FF6644>💀 {attacker.Stats.CharacterName} is slain before completing the ranged attack.</color>");
            return false;
        }

        return true;
    }

    private bool CanAttemptImprovedGrabFromAttack(CharacterController attacker, CharacterController target, CombatResult attackResult)
    {
        if (attacker?.Stats == null || target?.Stats == null || attackResult == null)
            return false;

        if (!attacker.Stats.HasImprovedGrab || target.Stats.IsDead || !attackResult.Hit)
            return false;

        return IsImprovedGrabTriggerAttack(attacker, attackResult);
    }

    private IEnumerator ResolveImprovedGrabWithPromptCoroutine(CharacterController attacker, CharacterController target, CombatResult attackResult, Action onResolved)
    {
        bool shouldAttemptGrab = true;
        if (attacker != null && attacker.IsControllable)
        {
            bool playerDecision = false;
            yield return StartCoroutine(PromptImprovedGrabChoice(attacker, target, attackResult != null ? attackResult.WeaponName : null, decision => playerDecision = decision));
            shouldAttemptGrab = playerDecision;
        }

        if (!shouldAttemptGrab)
        {
            CombatUI?.ShowCombatLog($"↷ {attacker?.Stats?.CharacterName ?? "Attacker"} declines to start a grapple.");
            onResolved?.Invoke();
            yield break;
        }

        SpecialAttackResult grabResult = attacker.ResolveImprovedGrabFreeAttempt(target);
        string attackName = !string.IsNullOrWhiteSpace(attackResult?.WeaponName) ? attackResult.WeaponName : "trigger attack";
        CombatUI?.ShowCombatLog($"🪢 Improved Grab ({attackName} hit): {grabResult.Log}");
        onResolved?.Invoke();
    }

    private bool TryResolveImprovedGrabAfterSingleAttack(CharacterController attacker, CharacterController target, CombatResult attackResult, Action onResolved)
    {
        if (!CanAttemptImprovedGrabFromAttack(attacker, target, attackResult))
            return false;

        if (attacker != null && attacker.IsControllable)
        {
            StartCoroutine(ResolveImprovedGrabWithPromptCoroutine(attacker, target, attackResult, onResolved));
            return true;
        }

        SpecialAttackResult grabResult = attacker.ResolveImprovedGrabFreeAttempt(target);
        string attackName = !string.IsNullOrWhiteSpace(attackResult?.WeaponName) ? attackResult.WeaponName : "trigger attack";
        CombatUI?.ShowCombatLog($"🪢 Improved Grab ({attackName} hit): {grabResult.Log}");
        return false;
    }

    private void TryResolveImprovedGrabFromAttackResults(CharacterController attacker, CharacterController target, List<CombatResult> attacks)
    {
        if (attacker?.Stats == null || target?.Stats == null || attacks == null || attacks.Count == 0)
            return;

        if (!attacker.Stats.HasImprovedGrab || target.Stats.IsDead)
            return;

        for (int i = 0; i < attacks.Count; i++)
        {
            CombatResult attackResult = attacks[i];
            if (!CanAttemptImprovedGrabFromAttack(attacker, target, attackResult))
                continue;

            SpecialAttackResult grabResult = attacker.ResolveImprovedGrabFreeAttempt(target);
            CombatUI?.ShowCombatLog($"🪢 Improved Grab ({attackResult.WeaponName} hit): {grabResult.Log}");

            if (grabResult.Success || target.Stats.IsDead)
                break;
        }
    }

    private FullAttackResult PerformNPCFullAttackWithAdaptiveRetargeting(
        CharacterController npc,
        CharacterController initialTarget,
        DND35.AI.AIProfile profile)
    {
        var aggregate = new FullAttackResult
        {
            Type = FullAttackResult.AttackType.FullAttack,
            Attacker = npc,
            Defender = initialTarget,
            DefenderHPBefore = initialTarget != null && initialTarget.Stats != null ? initialTarget.Stats.CurrentHP : 0
        };

        if (npc == null || npc.Stats == null || initialTarget == null || initialTarget.Stats == null)
            return aggregate;

        RangeInfo initialRangeInfo = CalculateRangeInfo(npc, initialTarget);
        int plannedAttackCount = npc.GetPlannedFullAttackCount(initialRangeInfo);
        if (plannedAttackCount <= 0)
        {
            CombatUI?.ShowCombatLog($"⚠ {npc.Stats.CharacterName} has no available full-attack steps.");
            aggregate.DefenderHPAfter = initialTarget.Stats.CurrentHP;
            aggregate.TargetKilled = initialTarget.Stats.IsDead;
            return aggregate;
        }

        CharacterController currentTarget = initialTarget;
        int attacksMade = 0;
        int targetSwitchCount = 0;

        for (int attackIndex = 0; attackIndex < plannedAttackCount; attackIndex++)
        {
            if (npc == null || npc.Stats == null || npc.Stats.IsDead || CurrentPhase == TurnPhase.CombatOver)
                break;

            bool needsNewTarget = currentTarget == null
                || currentTarget.Stats == null
                || currentTarget.Stats.IsDead
                || (profile != null && profile.ShouldIgnoreUnconsciousTargets(npc) && currentTarget.IsUnconscious)
                || !IsTargetInCurrentWeaponRange(npc, currentTarget);

            if (needsNewTarget)
            {
                CharacterController inReachTarget = SelectBestAdaptiveFullAttackTarget(npc, profile, requireInRange: true);
                if (inReachTarget != null)
                {
                    currentTarget = inReachTarget;
                    targetSwitchCount++;
                    CombatUI?.ShowCombatLog($"🎯 {npc.Stats.CharacterName} shifts focus to {currentTarget.Stats.CharacterName}.");
                }
                else
                {
                    CharacterController steppedTarget = null;
                    bool stepped = profile != null
                        && profile.ShouldTakeFiveFootStepToContinueFullAttack(npc)
                        && TryTakeFiveFootStepForAdaptiveFullAttack(npc, profile, out steppedTarget);

                    if (stepped)
                    {
                        currentTarget = steppedTarget;
                        targetSwitchCount++;
                        CombatUI?.ShowCombatLog($"🎯 {npc.Stats.CharacterName} re-engages {currentTarget.Stats.CharacterName} after a 5-foot step.");
                    }
                    else
                    {
                        int remainingAttacks = plannedAttackCount - attackIndex;
                        CombatUI?.ShowCombatLog($"↩ {npc.Stats.CharacterName} has no valid active targets for {remainingAttacks} remaining attack(s).");
                        break;
                    }
                }
            }

            if (currentTarget == null || currentTarget.Stats == null)
                break;

            CharacterController flankPartner;
            bool isFlanking = CombatUtils.IsAttackerFlanking(npc, currentTarget, GetAllCharacters(), out flankPartner);
            int flankBonus = isFlanking ? CombatUtils.FlankingAttackBonus : 0;
            string partnerName = flankPartner != null && flankPartner.Stats != null
                ? flankPartner.Stats.CharacterName
                : null;

            RangeInfo rangeInfo = CalculateRangeInfo(npc, currentTarget);
            bool isMeleeFearBreakAttack = IsMeleeAttackForTurnUndeadFearBreak(
                npc,
                npc.GetEquippedMainWeapon(),
                rangeInfo,
                treatAsThrownAttack: false);
            ProcessTurnUndeadMeleeFearBreak(npc, currentTarget, isMeleeFearBreakAttack);

            FullAttackResult stepResult = npc.FullAttack(
                currentTarget,
                isFlanking,
                flankBonus,
                partnerName,
                rangeInfo,
                startAttackIndex: attackIndex,
                maxAttacks: 1);

            if (stepResult == null || stepResult.Attacks == null || stepResult.Attacks.Count == 0)
                break;

            CombatResult attack = stepResult.Attacks[0];
            string label = (stepResult.AttackLabels != null && stepResult.AttackLabels.Count > 0)
                ? stepResult.AttackLabels[0]
                : $"Attack {attackIndex + 1}";

            aggregate.Attacks.Add(attack);
            aggregate.AttackLabels.Add(label);
            attacksMade++;

            CombatUI?.ShowCombatLog(attack.GetAttackBreakdown(label));

            if (attack.Hit && attack.TotalDamage > 0)
                CheckConcentrationOnDamage(currentTarget, attack.TotalDamage);

            TryResolveFreeTripFromAttackResults(npc, currentTarget, stepResult.Attacks, rangeInfo);
            TryResolveImprovedGrabFromAttackResults(npc, currentTarget, stepResult.Attacks);

            if (currentTarget.Stats.IsDead)
            {
                HandleSummonDeathCleanup(currentTarget);

                if (AreAllPCsDead())
                {
                    CurrentPhase = TurnPhase.CombatOver;
                    CombatUI.SetTurnIndicator("DEFEAT! All heroes have fallen!");
                    CombatUI.SetActionButtonsVisible(false);
                    break;
                }

                int attacksRemainingAfterKill = plannedAttackCount - (attackIndex + 1);
                if (attacksRemainingAfterKill > 0)
                    CombatUI?.ShowCombatLog($"💀 {currentTarget.Stats.CharacterName} is defeated! {attacksRemainingAfterKill} attack(s) remaining.");

                currentTarget = null;
                continue;
            }

            if (profile != null && profile.ShouldIgnoreUnconsciousTargets(npc) && currentTarget.IsUnconscious)
            {
                int attacksRemainingAfterDrop = plannedAttackCount - (attackIndex + 1);
                if (attacksRemainingAfterDrop > 0)
                    CombatUI?.ShowCombatLog($"💤 {currentTarget.Stats.CharacterName} drops unconscious! {npc.Stats.CharacterName} looks for another active target.");

                currentTarget = null;
            }
        }

        aggregate.DefenderHPAfter = aggregate.Defender != null && aggregate.Defender.Stats != null
            ? aggregate.Defender.Stats.CurrentHP
            : aggregate.DefenderHPBefore;
        aggregate.TargetKilled = aggregate.Defender != null && aggregate.Defender.Stats != null && aggregate.Defender.Stats.IsDead;

        _lastCombatLog = $"✅ {npc.Stats.CharacterName} completes adaptive full attack ({attacksMade}/{plannedAttackCount} attacks, {aggregate.TotalDamageDealt} total damage, {targetSwitchCount} target switch(es)).";
        CombatUI?.ShowCombatLog(_lastCombatLog);

        return aggregate;
    }

    private CharacterController SelectBestAdaptiveFullAttackTarget(
        CharacterController attacker,
        DND35.AI.AIProfile profile,
        bool requireInRange)
    {
        if (attacker == null || attacker.Stats == null)
            return null;

        var enemies = new List<CharacterController>();
        bool hasConsciousEnemy = false;

        foreach (CharacterController candidate in GetAllCharacters())
        {
            if (candidate == null || candidate == attacker || candidate.Stats == null || candidate.Stats.IsDead)
                continue;

            if (!IsEnemyTeam(attacker, candidate))
                continue;

            enemies.Add(candidate);
            if (!candidate.IsUnconscious)
                hasConsciousEnemy = true;
        }

        bool ignoreUnconscious = profile != null
            && profile.ShouldIgnoreUnconsciousTargets(attacker)
            && hasConsciousEnemy;

        var candidates = new List<CharacterController>();
        for (int i = 0; i < enemies.Count; i++)
        {
            CharacterController candidate = enemies[i];
            if (ignoreUnconscious && candidate.IsUnconscious)
                continue;

            if (requireInRange && !IsTargetInCurrentWeaponRange(attacker, candidate))
                continue;

            candidates.Add(candidate);
        }

        if (candidates.Count == 0)
            return null;

        if (_aiService != null)
        {
            CharacterController profiled = _aiService.SelectBestTarget(attacker, candidates);
            if (profiled != null)
                return profiled;
        }

        CharacterController best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < candidates.Count; i++)
        {
            CharacterController candidate = candidates[i];
            float score = profile != null ? profile.ScoreTarget(candidate, attacker) : 0f;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private bool TryTakeFiveFootStepForAdaptiveFullAttack(
        CharacterController attacker,
        DND35.AI.AIProfile profile,
        out CharacterController nextTarget)
    {
        nextTarget = null;

        if (attacker == null || !CanTakeFiveFootStep(attacker) || _movementService == null)
            return false;

        var enemies = new List<CharacterController>();
        bool hasConsciousEnemy = false;

        foreach (CharacterController candidate in GetAllCharacters())
        {
            if (candidate == null || candidate == attacker || candidate.Stats == null || candidate.Stats.IsDead)
                continue;

            if (!IsEnemyTeam(attacker, candidate))
                continue;

            enemies.Add(candidate);
            if (!candidate.IsUnconscious)
                hasConsciousEnemy = true;
        }

        bool ignoreUnconscious = profile != null
            && profile.ShouldIgnoreUnconsciousTargets(attacker)
            && hasConsciousEnemy;

        Vector2Int[] neighbors = SquareGridUtils.GetNeighbors(attacker.GridPosition);
        Vector2Int bestStep = attacker.GridPosition;
        CharacterController bestTarget = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < neighbors.Length; i++)
        {
            Vector2Int stepCell = neighbors[i];
            if (!_movementService.CanTake5FootStep(attacker, stepCell))
                continue;

            for (int t = 0; t < enemies.Count; t++)
            {
                CharacterController candidate = enemies[t];
                if (ignoreUnconscious && candidate.IsUnconscious)
                    continue;

                int distance = SquareGridUtils.GetDistance(stepCell, candidate.GridPosition);
                if (!attacker.CanMeleeAttackDistance(distance, attacker.GetEquippedMainWeapon()))
                    continue;

                float score = profile != null ? profile.ScoreTarget(candidate, attacker) : 0f;
                score -= distance * 0.25f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestStep = stepCell;
                    bestTarget = candidate;
                }
            }
        }

        if (bestTarget == null)
            return false;

        SquareCell destination = Grid != null ? Grid.GetCell(bestStep) : null;
        if (destination == null)
            return false;

        if (!ExecuteFiveFootStep(attacker, destination, returnToActionChoices: false))
            return false;

        nextTarget = bestTarget;
        return true;
    }

    private bool TryNPCPerformSpellCast(CharacterController npc, CharacterController target, SpellData spell)
    {
        if (npc == null || target == null || spell == null || npc.Stats == null || target.Stats == null)
            return false;

        if (!npc.Actions.HasStandardAction)
            return false;

        if (target.Stats.IsDead)
            return false;

        SpellcastingComponent spellComp = npc.GetComponent<SpellcastingComponent>();
        if (spellComp == null || !spellComp.CanCastSpells)
            return false;

        if (!spellComp.CanCast(spell))
            return false;

        if (!IsValidTargetForSpell(npc, target, spell))
            return false;

        int rangeSquares = spell.GetRangeSquaresForCasterLevel(npc.Stats.GetCasterLevel());
        if (rangeSquares <= 0)
            rangeSquares = 1;

        int distance = SquareGridUtils.GetDistance(npc.GridPosition, target.GridPosition);
        if (distance > rangeSquares)
            return false;

        if (spell.TargetType == SpellTargetType.Area)
            return false;

        if (!npc.CommitStandardAction())
            return false;

        bool consumed = spellComp.CastSpellFromSlot(spell);
        if (!consumed)
            return false;

        if (TryRollArcaneSpellFailure(npc, spell, false, out int asfRoll, out int asfChance))
        {
            LogArcaneSpellFailure(npc, spell, asfRoll, asfChance);
            UpdateAllStatsUI();
            return true;
        }

        bool skipFriendlyTouchAttackRoll = spell.IsMeleeTouchSpell() && IsFriendlyTarget(npc, target);
        bool forceTargetToFailSave = ShouldForceTargetToAcceptSave(npc, target, spell);
        SpellResult result = SpellCaster.Cast(spell, npc.Stats, target.Stats, null, skipFriendlyTouchAttackRoll, forceTargetToFailSave, npc, target);

        bool appliesTrackedEffect = spell.EffectType == SpellEffectType.Buff || spell.EffectType == SpellEffectType.Debuff;
        bool effectNegatedBySave = spell.EffectType == SpellEffectType.Debuff && result.RequiredSave && result.SaveSucceeded;

        if (effectNegatedBySave)
            CombatUI?.ShowCombatLog($"🛡 {target.Stats.CharacterName} resists {spell.Name} with a successful {result.SaveType} save.");

        if (result.MindAffectingImmunityBlocked)
            CombatUI?.ShowCombatLog($"🧠 {target.Stats.CharacterName} is immune to mind-affecting effects. {spell.Name} has no effect.");

        if (result.Success && appliesTrackedEffect && !effectNegatedBySave)
            ApplySpellBuff(npc, target, spell, spellComp);

        if (result.DamageDealt > 0)
            CheckConcentrationOnDamage(target, result.DamageDealt);

        _lastCombatLog = result.GetFormattedLog();
        CombatUI?.ShowCombatLog(_lastCombatLog);

        if (result.TargetKilled)
        {
            target.OnDeath();
            HandleSummonDeathCleanup(target);
        }

        UpdateAllStatsUI();
        return true;
    }

    private IEnumerator NPCPerformAttack(CharacterController npc, CharacterController target)
    {
        if (npc != null && npc.aiProfile != null)
            npc.aiProfile.TryEnsureWeaponFallback(npc);

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

        RangeInfo npcRangeInfo = CalculateRangeInfo(npc, target);

        CharacterController flankPartner;
        bool isFlanking = CombatUtils.IsAttackerFlanking(npc, target, GetAllCharacters(), out flankPartner);
        int flankBonus = isFlanking ? CombatUtils.FlankingAttackBonus : 0;
        string partnerName = flankPartner != null && flankPartner.Stats != null
            ? flankPartner.Stats.CharacterName
            : null;

        bool canUseFullAttack = npc.Actions != null
            && npc.Actions.HasFullRoundAction
            && npc.IsTargetInCurrentWeaponRange(target);

        if (canUseFullAttack)
        {
            if (!ResolveRangedAttackAoOForNPCAttackIfProvoked(npc, npcRangeInfo))
            {
                yield return new WaitForSeconds(0.8f);
                yield break;
            }

            npc.Actions.UseFullRoundAction();

            DND35.AI.AIProfile activeProfile = npc.aiProfile;
            bool canSwitchMidAttack = activeProfile != null
                && activeProfile.ShouldSwitchTargetsMidFullAttack(npc)
                && !IsAttackModeRanged(npc);

            if (canSwitchMidAttack)
            {
                FullAttackResult switchedResult = PerformNPCFullAttackWithAdaptiveRetargeting(npc, target, activeProfile);

                Debug.Log($"[AI][Attack] {npc.Stats.CharacterName} performed adaptive full attack: attacks={switchedResult.Attacks.Count}, hits={switchedResult.HitCount}, totalDamage={switchedResult.TotalDamageDealt}");

                if (LogAttacksToConsole)
                    LogFullAttackToConsole(switchedResult);

                UpdateAllStatsUI();
                yield return new WaitForSeconds(1.0f);
                yield break;
            }

            bool isMeleeFearBreakAttack = IsMeleeAttackForTurnUndeadFearBreak(
                npc,
                npc.GetEquippedMainWeapon(),
                npcRangeInfo,
                treatAsThrownAttack: false);
            ProcessTurnUndeadMeleeFearBreak(npc, target, isMeleeFearBreakAttack);

            FullAttackResult fullResult = npc.FullAttack(target, isFlanking, flankBonus, partnerName, npcRangeInfo);
            string flankPrefix = isFlanking
                ? $"⚔ {npc.Stats.CharacterName} gains +2 flanking bonus{(string.IsNullOrEmpty(partnerName) ? "" : $" (with {partnerName})")}.\n"
                : string.Empty;

            _lastCombatLog = flankPrefix + fullResult.GetFullSummary();

            Debug.Log($"[AI][Attack] {npc.Stats.CharacterName} performed full attack: attacks={fullResult.Attacks.Count}, hits={fullResult.HitCount}, totalDamage={fullResult.TotalDamageDealt}");

            if (LogAttacksToConsole)
                LogFullAttackToConsole(fullResult);

            CombatUI.ShowCombatLog(_lastCombatLog);
            UpdateAllStatsUI();

            if (fullResult.TotalDamageDealt > 0)
                CheckConcentrationOnDamage(target, fullResult.TotalDamageDealt);

            TryResolveFreeTripFromAttackResults(npc, target, fullResult.Attacks, npcRangeInfo);
            TryResolveImprovedGrabFromAttackResults(npc, target, fullResult.Attacks);

            if (fullResult.TargetKilled)
            {
                HandleSummonDeathCleanup(target);

                if (AreAllPCsDead())
                {
                    CurrentPhase = TurnPhase.CombatOver;
                    CombatUI.SetTurnIndicator("DEFEAT! All heroes have fallen!");
                    CombatUI.SetActionButtonsVisible(false);
                    yield break;
                }

                CombatUI.ShowCombatLog(_lastCombatLog + $"\n{target.Stats.CharacterName} has fallen, but the fight continues!");
            }

            yield return new WaitForSeconds(1.0f);
            yield break;
        }

        if (!npc.CommitStandardAction())
        {
            CombatUI.ShowCombatLog($"⚠ {npc.Stats.CharacterName} has no standard action available.");
            yield return new WaitForSeconds(0.6f);
            yield break;
        }

        bool singleAttackFearBreak = IsMeleeAttackForTurnUndeadFearBreak(
            npc,
            npc.GetEquippedMainWeapon(),
            npcRangeInfo,
            treatAsThrownAttack: false);
        ProcessTurnUndeadMeleeFearBreak(npc, target, singleAttackFearBreak);

        if (!ResolveRangedAttackAoOForNPCAttackIfProvoked(npc, npcRangeInfo))
        {
            yield return new WaitForSeconds(0.8f);
            yield break;
        }

        CombatResult result = npc.Attack(target, isFlanking, flankBonus, partnerName, npcRangeInfo);

        TryResolveFreeTripOnHit(npc, target, result, npcRangeInfo);

        _lastCombatLog = BuildAttackLog(npc, isFlanking, partnerName, result);

        if (LogAttacksToConsole)
            Debug.Log("[Combat] " + _lastCombatLog);

        CombatUI.ShowCombatLog(_lastCombatLog);
        UpdateAllStatsUI();

        if (result.Hit && result.TotalDamage > 0)
            CheckConcentrationOnDamage(target, result.TotalDamage);

        TryResolveImprovedGrabFromAttackResults(npc, target, new List<CombatResult> { result });

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

            CombatUI.ShowCombatLog(_lastCombatLog + $"\n{target.Stats.CharacterName} has fallen, but the fight continues!");
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
