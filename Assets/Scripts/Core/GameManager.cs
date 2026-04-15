using System;
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

    /// <summary>Encounter preset selection overlay shown before combat starts.</summary>
    public EncounterSelectionUI EncounterSelectionUI;

    /// <summary>Whether combat setup is waiting on encounter selection.</summary>
    public bool WaitingForEncounterSelection { get; private set; }

    private string _selectedEncounterPresetId = "goblin_raiders";
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
        Animating
    }

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

    // Pending charge state
    private CharacterController _chargeTarget;
    private List<Vector2Int> _pendingChargePath = new List<Vector2Int>();

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

        // Keep the pre-combat picker compact: show exactly three curated options.
        if (presets.Count > 3)
            presets = presets.GetRange(0, 3);

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
    /// Handle input every frame - inventory toggle and cell clicks.
    /// </summary>
    private void Update()
    {
        // Skip all game input during character creation / encounter selection.
        if (WaitingForCharacterCreation || WaitingForEncounterSelection) return;

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

        if (CurrentSubPhase == PlayerSubPhase.SelectingChargeTarget || CurrentSubPhase == PlayerSubPhase.ConfirmingChargePath)
            UpdateChargeHoverPreview();

        // Right-click / Escape to cancel targeting in various states
        if (CurrentSubPhase == PlayerSubPhase.Moving
            || CurrentSubPhase == PlayerSubPhase.TakingFiveFootStep
            || CurrentSubPhase == PlayerSubPhase.Crawling
            || CurrentSubPhase == PlayerSubPhase.SelectingAoETarget
            || CurrentSubPhase == PlayerSubPhase.ConfirmingSelfAoE
            || CurrentSubPhase == PlayerSubPhase.SelectingSpecialTarget
            || CurrentSubPhase == PlayerSubPhase.SelectingAttackTarget
            || CurrentSubPhase == PlayerSubPhase.SelectingChargeTarget
            || CurrentSubPhase == PlayerSubPhase.ConfirmingChargePath)
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
                if (CurrentSubPhase == PlayerSubPhase.Moving)
                {
                    CancelMovementSelection();
                }
                else if (CurrentSubPhase == PlayerSubPhase.TakingFiveFootStep)
                {
                    CancelFiveFootStepSelection();
                }
                else if (CurrentSubPhase == PlayerSubPhase.Crawling)
                {
                    CancelCrawlSelection();
                }
                else if (CurrentSubPhase == PlayerSubPhase.ConfirmingSelfAoE)
                {
                    OnSelfAoECancelled();
                }
                else if (CurrentSubPhase == PlayerSubPhase.SelectingAoETarget && _isAoETargeting)
                {
                    CancelAoETargeting();
                }
                else if (CurrentSubPhase == PlayerSubPhase.SelectingSpecialTarget)
                {
                    CancelSpecialAttackTargeting();
                }
                else if (CurrentSubPhase == PlayerSubPhase.SelectingChargeTarget || CurrentSubPhase == PlayerSubPhase.ConfirmingChargePath)
                {
                    CancelChargeTargeting();
                }
                else if (CurrentSubPhase == PlayerSubPhase.SelectingAttackTarget)
                {
                    if (_pendingAttackMode == PendingAttackMode.CastSpell)
                        CancelSpellTargeting();
                    else
                        CancelPendingAttackTargeting();
                }
                return;
            }
        }

        // Right-click summon command menu (player-owned summons only, during player action phase).
        if (CurrentSubPhase == PlayerSubPhase.ChoosingAction)
        {
            if (TryHandleSummonRightClick())
                return;
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

    private bool TryHandleSummonRightClick()
    {
        bool rightClicked = false;
        Vector3 mouseScreenPos = Vector3.zero;

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(1))
        {
            rightClicked = true;
            mouseScreenPos = Input.mousePosition;
        }
#endif

#if ENABLE_INPUT_SYSTEM
        if (!rightClicked)
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                rightClicked = true;
                mouseScreenPos = mouse.position.ReadValue();
            }
        }
#endif

        if (!rightClicked || _mainCam == null)
            return false;

        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return false;
        }

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

    private void SetupEnemyEncounter(List<string> enemyIds)
    {
        EnemyDatabase.Init();
        ItemDatabase.Init();

        _npcAIBehaviors.Clear();

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

            Vector2Int pos = (i < EncounterSpawnPositions.Length)
                ? EncounterSpawnPositions[i]
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

        Debug.Log($"[GameManager] {def.Name}: HP {stats.MaxHP} AC {stats.ArmorClass} " +
                  $"Atk {CharacterStats.FormatMod(stats.AttackBonus)} Speed {stats.MoveRange}sq");
    }

    /// <summary>Helper to update all stat UI panels using 4-PC multi-NPC system.</summary>
    private void UpdateAllStatsUI()
    {
        RefreshFlankedConditions();

        if (CombatUI == null) return;
        CombatUI.UpdateAllStats4PC(PCs, NPCs);
    }

    /// <summary>
    /// Keep Flanked condition badges in sync with current battlefield positions.
    /// </summary>
    private void RefreshFlankedConditions()
    {
        var allCharacters = GetAllCharacters();

        foreach (var character in allCharacters)
        {
            if (character == null || character.Stats == null)
                continue;

            bool hasFlanked = character.HasCondition(CombatConditionType.Flanked);

            // Dead units should not retain tactical flanking condition badges.
            if (character.Stats.IsDead)
            {
                if (hasFlanked)
                    character.Stats.RemoveCondition(CombatConditionType.Flanked);
                continue;
            }

            bool shouldBeFlanked = false;
            foreach (var enemy in allCharacters)
            {
                if (enemy == null || enemy == character || enemy.Stats == null) continue;
                if (enemy.Stats.IsDead) continue;
                if (enemy.IsPlayerControlled == character.IsPlayerControlled) continue;

                CharacterController partner;
                if (CombatUtils.IsAttackerFlanking(enemy, character, allCharacters, out partner))
                {
                    shouldBeFlanked = true;
                    break;
                }
            }

            if (shouldBeFlanked && !hasFlanked)
                character.Stats.ApplyCondition(CombatConditionType.Flanked, -1, "Flanking");
            else if (!shouldBeFlanked && hasFlanked)
                character.Stats.RemoveCondition(CombatConditionType.Flanked);
        }
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

    /// <summary>Check if all PCs in the party are dead.</summary>
    private bool AreAllPCsDead()
    {
        foreach (var pc in PCs)
        {
            if (pc != null && !pc.Stats.IsDead) return false;
        }
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

        _initiativeOrder = InitiativeSystem.RollInitiative(activePCs, activeNPCs);
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

            // Tick summon durations (Summon Monster: 1 round/level)
            TickSummonDurations();
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
        _loggedHeldChargeNoActionsReminder = false;

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
        CombatUI.HideSummonContextMenu();

        _waitingForAoOConfirmation = false;
        _pendingAoOAction = null;
        _spellcastProvocationCancelled = false;
        ClearSpellcastResourceSnapshot();
        CombatUI.HideAoOConfirmationPrompt();
        // Hide movement path preview and hover marker when leaving movement phase
        if (_pathPreview != null) _pathPreview.HidePath();
        if (_hoverMarker != null) _hoverMarker.Hide();

        // Reset transient charge state whenever we return to action menu
        _chargeTarget = null;
        _pendingChargePath.Clear();

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        SquareCell current = Grid.GetCell(pc.GridPosition);
        if (current != null)
            current.SetHighlight(HighlightType.Selected);

        CombatUI.SetActionButtonsVisible(true);
        CombatUI.HideSpecialAttackMenu();
        CombatUI.UpdateActionButtons(pc);
        CombatUI.UpdateFeatControls(pc);

        string pcName = pc.Stats.CharacterName;
        string actionInfo = pc.Actions.GetStatusString();

        string weaponStateInfo = string.Empty;
        ItemData currentWeapon = pc.GetEquippedMainWeapon();
        if (currentWeapon != null && currentWeapon.RequiresReload)
        {
            weaponStateInfo = $"\n{pc.GetWeaponLoadStateLabel(currentWeapon)}";
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
            if (IsHoldingTouchCharge(pc))
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
                CombatUI.SetTurnIndicator($"{pcName}'s Turn - No actions remaining");
                StartCoroutine(DelayedEndActivePCTurn(1.0f));
            }
        }
    }

    // ========== ACTION BUTTON HANDLERS ==========

    public void OnMoveButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;

        if (pc.Stats.MovementBlockedByCondition)
        {
            CombatUI.ShowCombatLog($"⚠ {pc.Stats.CharacterName} is grappled and cannot move.");
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
        if (character == null || character.Stats == null)
            return "No active character";

        if (character.HasMovedThisTurn)
            return "Already moved this turn";

        if (character.HasTakenFiveFootStep)
            return "Already used 5-foot step this turn";

        if (character.HasCondition(CombatConditionType.Prone))
            return "Cannot 5-foot step while prone";

        if (character.HasCondition(CombatConditionType.Grappled))
            return "Cannot 5-foot step while grappled";

        // Must have at least one legal adjacent destination.
        foreach (var neighbor in SquareGridUtils.GetNeighbors(character.GridPosition))
        {
            if (IsValidFiveFootStepDestination(character, neighbor))
                return string.Empty;
        }

        return "No valid adjacent square";
    }

    public void OnFiveFootStepButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;

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

        SquareCell current = Grid.GetCell(pc.GridPosition);
        if (current != null)
            current.SetHighlight(HighlightType.Selected);
    }

    private bool IsValidFiveFootStepDestination(CharacterController pc, Vector2Int destination)
    {
        if (!SquareGridUtils.IsAdjacent(pc.GridPosition, destination))
            return false;

        SquareCell cell = Grid.GetCell(destination);
        if (cell == null)
            return false;

        if (cell.IsOccupied)
            return false;

        if (IsDifficultTerrain(destination))
            return false;

        return true;
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
        if (!pc.FiveFootStep(destination))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} failed to take a 5-foot step.");
            return false;
        }

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

        string reason = GetDropProneDisabledReason(pc);
        if (!string.IsNullOrEmpty(reason))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot drop prone: {reason}.");
            return;
        }

        pc.Stats.ApplyCondition(CombatConditionType.Prone, -1, pc.Stats.CharacterName);
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

                CombatResult aooResult = ThreatSystem.ExecuteAoO(enemy, pc);
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

        bool removed = pc.Stats.RemoveCondition(CombatConditionType.Prone);
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

        SquareCell current = Grid.GetCell(pc.GridPosition);
        if (current != null)
            current.SetHighlight(HighlightType.Selected);
    }

    private bool IsValidCrawlDestination(CharacterController pc, Vector2Int destination)
    {
        if (pc == null) return false;
        if (!SquareGridUtils.IsAdjacent(pc.GridPosition, destination))
            return false;

        SquareCell cell = Grid.GetCell(destination);
        if (cell == null || cell.IsOccupied)
            return false;

        if (IsDifficultTerrain(destination))
            return false;

        return true;
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
        var provokedAoOs = ThreatSystem.AnalyzePathForAoOs(pc, crawlPath, GetAllCharacters());

        if (provokedAoOs.Count > 0)
        {
            CombatUI?.ShowCombatLog("Crawling provokes attacks of opportunity!");
            foreach (var aooInfo in provokedAoOs)
            {
                if (pc.Stats.IsDead) break;

                CharacterController threatener = aooInfo.Threatener;
                if (threatener == null || threatener.Stats == null || threatener.Stats.IsDead) continue;

                CombatResult aooResult = ThreatSystem.ExecuteAoO(threatener, pc);
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
        yield return StartCoroutine(pc.MoveAlongPath(new List<Vector2Int> { destination.Coords }, PlayerMoveSecondsPerStep, markAsMoved: true));

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
        if (pc == null || !pc.Actions.HasStandardAction) return;

        if (!pc.CanAttackWithEquippedWeapon(out string cannotAttackReason))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot attack: {cannotAttackReason}");
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        _pendingDefensiveAttackSelection = false;
        pc.SetFightingDefensively(false);

        _pendingAttackMode = PendingAttackMode.Single;
        CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
        ShowAttackTargets(pc);
    }

    public void OnSpecialAttackButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || !pc.Actions.HasStandardAction) return;

        _isSelectingSpecialAttack = true;
        CombatUI.ShowSpecialAttackMenu(pc, OnSpecialAttackSelected, ShowActionChoices);
    }

    private void OnSpecialAttackSelected(SpecialAttackType type)
    {
        CharacterController pc = ActivePC;
        if (pc == null) { ShowActionChoices(); return; }

        _pendingSpecialAttackType = type;
        _isSelectingSpecialAttack = true;
        CombatUI.HideSpecialAttackMenu();
        CurrentSubPhase = PlayerSubPhase.SelectingSpecialTarget;
        ShowSpecialAttackTargets(pc, type);
    }

    public void OnChargeButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;
        EnterChargeMode(pc);
    }

    public void OnFullAttackButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || !pc.Actions.HasFullRoundAction) return;

        if (!pc.CanAttackWithEquippedWeapon(out string cannotAttackReason))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot full attack: {cannotAttackReason}");
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        _pendingDefensiveAttackSelection = false;
        pc.SetFightingDefensively(false);

        _pendingAttackMode = PendingAttackMode.FullAttack;
        CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
        ShowAttackTargets(pc);
    }

    public void OnAttackDefensivelyButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || pc.Stats == null || !pc.Actions.HasStandardAction) return;

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
        if (pc == null || !pc.Actions.HasFullRoundAction || !pc.CanDualWield()) return;

        var inv = pc.GetComponent<InventoryComponent>();
        ItemData main = inv != null ? inv.CharacterInventory.RightHandSlot : null;
        ItemData off = inv != null ? inv.CharacterInventory.LeftHandSlot : null;
        bool canMain = pc.CanAttackWithWeapon(main, out string mainReason);
        bool canOff = pc.CanAttackWithWeapon(off, out string offReason);
        if (!canMain && !canOff)
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot dual-wield attack: {mainReason}");
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

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

        CharacterController pc = ActivePC;
        if (IsHoldingTouchCharge(pc))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} ends turn while holding {GetHeldTouchSpellName(pc)}. The charge persists.");
        }

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

        // Casting can only begin if there is a castable prepared spell.
        // Held charges are delivered via the dedicated Discharge button.
        if (!spellComp.HasAnyCastablePreparedSpell())
        {
            Debug.Log($"[GameManager] {pc.Stats.CharacterName}: No prepared spells with available slots to cast.");
            return;
        }

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

    public string GetSummonTooltip(CharacterController character)
    {
        var summon = GetActiveSummon(character);
        if (summon == null || character == null || character.Stats == null)
            return null;

        CharacterStats s = character.Stats;
        string typeLine = summon.Template != null && !string.IsNullOrEmpty(summon.Template.CreatureTypeLine)
            ? summon.Template.CreatureTypeLine
            : "Summoned Creature";

        string attackName = summon.Template != null && !string.IsNullOrEmpty(summon.Template.AttackLabel)
            ? summon.Template.AttackLabel
            : "Attack";

        string special = "None";
        if (summon.Template != null && summon.Template.SpecialTraits != null && summon.Template.SpecialTraits.Count > 0)
            special = string.Join("\n• ", summon.Template.SpecialTraits);

        string casterName = summon.Caster != null && summon.Caster.Stats != null
            ? summon.Caster.Stats.CharacterName
            : "Unknown";

        string roundsWord = summon.RemainingRounds == 1 ? "round" : "rounds";
        string commandText = summon.CurrentCommand != null
            ? summon.CurrentCommand.Description
            : SummonCommand.AttackNearest().Description;

        return $"{GetSummonDisplayName(character)}\n{typeLine}\n\nHP: {s.CurrentHP}/{Mathf.Max(1, s.TotalMaxHP)}   AC: {s.ArmorClass}   Speed: {s.SpeedInFeet} ft\n" +
               $"STR {s.STR}  DEX {s.DEX}  CON {s.CON}\nINT {s.INT}  WIS {s.WIS}  CHA {s.CHA}\n\n" +
               $"Attack: {attackName} {CharacterStats.FormatMod(s.AttackBonus)} ({s.BaseDamageCount}d{s.BaseDamageDice}{CharacterStats.FormatMod(s.BonusDamage)})\n\n" +
               $"Special:\n• {special}\n\nCommand: {commandText}\nDuration: {summon.RemainingRounds} {roundsWord} remaining\nSummoned by: {casterName}";
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

        SquareCell casterCell = Grid.GetCell(caster.GridPosition);
        if (casterCell != null)
            casterCell.SetHighlight(HighlightType.Selected);

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
            caster.Actions.UseStandardAction();
        }
        else
        {
            spellComp.MarkQuickenedSpellCast();
        }

        int slotLevelToConsume = _pendingSpell.SpellLevel;
        bool hasMetamagicApplied = _pendingMetamagic != null && _pendingMetamagic.HasAnyMetamagic;
        if (hasMetamagicApplied)
            slotLevelToConsume = _pendingMetamagic.GetEffectiveSpellLevel(_pendingSpell.SpellLevel);

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
        if (combatant == null || combatant.Stats == null) return;

        bool isPCTeam = IsPC(combatant);
        var entry = new InitiativeSystem.InitiativeEntry(combatant, isPCTeam);

        int insertIdx = -1;
        if (summoner != null)
        {
            int summonerIdx = _initiativeOrder.FindIndex(e => e.Character == summoner);
            if (summonerIdx >= 0)
            {
                entry.Roll = _initiativeOrder[summonerIdx].Roll;
                entry.Modifier = _initiativeOrder[summonerIdx].Modifier;
                entry.Total = _initiativeOrder[summonerIdx].Total;

                insertIdx = summonerIdx + 1;
                while (insertIdx < _initiativeOrder.Count)
                {
                    var e = _initiativeOrder[insertIdx];
                    if (e.Total != entry.Total || e.Character == summoner)
                        break;

                    if (!IsSummonedCreature(e.Character))
                        break;

                    insertIdx++;
                }
            }
        }

        if (insertIdx < 0)
        {
            insertIdx = 0;
            while (insertIdx < _initiativeOrder.Count)
            {
                var cur = _initiativeOrder[insertIdx];
                if (entry.Total > cur.Total) break;
                if (entry.Total == cur.Total && entry.Modifier > cur.Modifier) break;
                insertIdx++;
            }
        }

        _initiativeOrder.Insert(insertIdx, entry);

        if (insertIdx <= _currentInitiativeIndex)
            _currentInitiativeIndex++;

        Debug.Log($"[Initiative] Added {combatant.Stats.CharacterName} to initiative at position {insertIdx + 1}: {entry.Total}");
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

        SquareCell currentCell = Grid.GetCell(cc.GridPosition);
        if (currentCell != null && currentCell.Occupant == cc)
        {
            currentCell.IsOccupied = false;
            currentCell.Occupant = null;
        }

        int npcIdx = NPCs.IndexOf(cc);
        if (npcIdx >= 0)
        {
            NPCs.RemoveAt(npcIdx);
            if (npcIdx < _npcAIBehaviors.Count)
                _npcAIBehaviors.RemoveAt(npcIdx);
        }

        _summonedAllies.Remove(cc);
        _summonedEnemies.Remove(cc);

        int initIdx = _initiativeOrder.FindIndex(e => e.Character == cc);
        if (initIdx >= 0)
        {
            _initiativeOrder.RemoveAt(initIdx);
            if (initIdx < _currentInitiativeIndex)
                _currentInitiativeIndex = Mathf.Max(0, _currentInitiativeIndex - 1);
            else if (_currentInitiativeIndex >= _initiativeOrder.Count)
                _currentInitiativeIndex = 0;
        }

        if (cc == _activeTurnCharacter)
            _activeTurnCharacter = null;

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

        if (targetCell.IsOccupied)
        {
            CombatUI.ShowCombatLog("Cannot summon there: tile is occupied.");
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
            caster.Actions.UseStandardAction();
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

        var expiredConditions = character.Stats.TickConditions();
        foreach (var cond in expiredConditions)
        {
            string msg = $"⏱ {character.Stats.CharacterName} is no longer {cond.Type}.";
            Debug.Log($"[Condition] {msg}");
            CombatUI?.ShowCombatLog($"<color=#99CCFF>{msg}</color>");
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

    private void CancelMovementSelection()
    {
        CharacterController pc = ActivePC;

        if (_pathPreview != null) _pathPreview.HidePath();
        if (_hoverMarker != null) _hoverMarker.Hide();

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        _waitingForAoOConfirmation = false;
        _pendingAoOAction = null;
        CombatUI?.HideAoOConfirmationPrompt();
        if (pc != null)
            CombatUI?.ShowCombatLog($"↩ {pc.Stats.CharacterName} cancels movement.");

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

    private void ShowAttackTargets(CharacterController pc)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        // All combatants are considered for flanking checks (team and threat filtering happens in CombatUtils).
        List<CharacterController> allCombatants = GetAllCharacters();

        // Determine the equipped weapon's range semantics.
        ItemData weapon = pc.GetEquippedMainWeapon();
        int rangeIncrement = (weapon != null) ? weapon.RangeIncrement : 0;
        bool isThrownWeapon = (weapon != null) && weapon.IsThrown;
        bool isRangedWeapon = (weapon != null && weapon.WeaponCat == WeaponCategory.Ranged) || rangeIncrement > 0;

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

        List<SquareCell> allCells = isRangedWeapon
            ? Grid.GetCellsInRange(pc.GridPosition, maxRangeSquares)
            : GetCellsInChebyshevRange(pc.GridPosition, maxRangeSquares);
        bool hasTarget = false;
        bool anyFlanking = false;

        foreach (var cell in allCells)
        {
            if (cell.IsOccupied && cell.Occupant != pc && !cell.Occupant.Stats.IsDead)
            {
                if (!IsEnemyTeam(pc, cell.Occupant))
                    continue;

                int sqDist = isRangedWeapon
                    ? SquareGridUtils.GetDistance(pc.GridPosition, cell.Coords)
                    : SquareGridUtils.GetChebyshevDistance(pc.GridPosition, cell.Coords);

                if (isRangedWeapon && rangeIncrement > 0)
                {
                    int distFeet = RangeCalculator.SquaresToFeet(sqDist);
                    if (!RangeCalculator.IsWithinMaxRange(distFeet, rangeIncrement, isThrownWeapon))
                        continue;
                }
                else
                {
                    if (!pc.CanMeleeAttackDistance(sqDist))
                        continue;
                }

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

    private List<CharacterController> GetValidRangedTargets(CharacterController attacker)
    {
        var valid = new List<CharacterController>();
        if (attacker == null || !attacker.IsEquippedWeaponRanged())
            return valid;

        ItemData weapon = attacker.GetEquippedMainWeapon();
        int rangeIncrement = weapon != null ? weapon.RangeIncrement : 0;
        bool isThrownWeapon = weapon != null && weapon.IsThrown;

        int maxRangeSquares = (rangeIncrement > 0)
            ? RangeCalculator.GetMaxRangeSquares(rangeIncrement, isThrownWeapon)
            : attacker.Stats.AttackRange;

        List<SquareCell> allCells = Grid.GetCellsInRange(attacker.GridPosition, maxRangeSquares);
        foreach (SquareCell cell in allCells)
        {
            if (cell == null || !cell.IsOccupied || cell.Occupant == null || cell.Occupant == attacker)
                continue;

            CharacterController candidate = cell.Occupant;
            if (candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!IsEnemyTeam(attacker, candidate))
                continue;

            int sqDist = SquareGridUtils.GetDistance(attacker.GridPosition, candidate.GridPosition);
            bool inRange;
            if (rangeIncrement > 0)
            {
                int distFeet = RangeCalculator.SquaresToFeet(sqDist);
                inRange = RangeCalculator.IsWithinMaxRange(distFeet, rangeIncrement, isThrownWeapon);
            }
            else
            {
                inRange = sqDist <= attacker.Stats.AttackRange;
            }

            if (inRange)
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

            int distance = SquareGridUtils.GetChebyshevDistance(attacker.GridPosition, candidate.GridPosition);
            if (attacker.CanMeleeAttackDistance(distance))
                valid.Add(candidate);
        }

        return valid;
    }

    private List<CharacterController> GetValidTargetsForCurrentWeapon(CharacterController attacker)
    {
        if (attacker == null)
            return new List<CharacterController>();

        return attacker.IsEquippedWeaponRanged()
            ? GetValidRangedTargets(attacker)
            : GetValidMeleeTargets(attacker);
    }

    private bool IsTargetInCurrentWeaponRange(CharacterController attacker, CharacterController target)
    {
        if (attacker == null || target == null || attacker.Stats == null || target.Stats == null || target.Stats.IsDead)
            return false;

        bool isRanged = attacker.IsEquippedWeaponRanged();

        if (isRanged)
        {
            int sqDist = SquareGridUtils.GetDistance(attacker.GridPosition, target.GridPosition);
            ItemData weapon = attacker.GetEquippedMainWeapon();
            int rangeIncrement = weapon != null ? weapon.RangeIncrement : 0;
            bool isThrownWeapon = weapon != null && weapon.IsThrown;

            if (rangeIncrement > 0)
            {
                int distFeet = RangeCalculator.SquaresToFeet(sqDist);
                return RangeCalculator.IsWithinMaxRange(distFeet, rangeIncrement, isThrownWeapon);
            }

            return sqDist <= attacker.Stats.AttackRange;
        }

        int meleeDistance = SquareGridUtils.GetChebyshevDistance(attacker.GridPosition, target.GridPosition);
        return attacker.CanMeleeAttackDistance(meleeDistance);
    }

    private bool HasAnyValidTargetFromPosition(CharacterController attacker, Vector2Int attackerPosition, bool rangedMode)
    {
        if (attacker == null || attacker.Stats == null)
            return false;

        ItemData weapon = attacker.GetEquippedMainWeapon();
        int rangeIncrement = weapon != null ? weapon.RangeIncrement : 0;
        bool isThrownWeapon = weapon != null && weapon.IsThrown;
        foreach (CharacterController candidate in GetAllCharacters())
        {
            if (candidate == null || candidate == attacker || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!IsEnemyTeam(attacker, candidate))
                continue;

            int sqDist = rangedMode
                ? SquareGridUtils.GetDistance(attackerPosition, candidate.GridPosition)
                : SquareGridUtils.GetChebyshevDistance(attackerPosition, candidate.GridPosition);
            if (rangedMode)
            {
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
            else if (attacker.CanMeleeAttackDistance(sqDist))
            {
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

        SquareCell current = Grid.GetCell(pc.GridPosition);
        if (current != null)
            current.SetHighlight(HighlightType.Selected);
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

        List<SquareCell> allCells = GetCellsInChebyshevRange(attacker.GridPosition, max);
        foreach (SquareCell cell in allCells)
        {
            if (cell == null || cell.Coords == attacker.GridPosition)
                continue;

            int sqDist = SquareGridUtils.GetChebyshevDistance(attacker.GridPosition, cell.Coords);
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

        if (cell.Coords == pc.GridPosition)
        {
            CancelMovementSelection();
            return;
        }

        if (!_highlightedCells.Contains(cell) || cell.IsOccupied)
            return;

        var allCharacters = GetAllCharacters();
        var pathResult = Grid.FindSafePath(pc.GridPosition, cell.Coords, pc, allCharacters);

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
                ShowMovementRange(pc);
                CombatUI.SetActionButtonsVisible(false);
                CombatUI.SetTurnIndicator($"{pc.Stats.CharacterName} - Click a tile to move (right-click/ESC or own tile to cancel)");
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

        yield return StartCoroutine(pc.MoveAlongPath(path, PlayerMoveSecondsPerStep, markAsMoved: true));
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
        AoOPathResult pathResult = Grid.FindPathAoOAware(mover.GridPosition, destination, null, maxRange);
        List<Vector2Int> path = (pathResult != null && pathResult.Path != null && pathResult.Path.Count > 0)
            ? pathResult.Path
            : new List<Vector2Int> { destination };

        yield return StartCoroutine(mover.MoveAlongPath(path, secondsPerStep, markAsMoved: true));
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

    private void ShowSpecialAttackTargets(CharacterController attacker, SpecialAttackType type)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        int maxRange = (type == SpecialAttackType.Feint)
            ? 1
            : attacker.GetMeleeMaxAttackDistance();
        if (maxRange < 1) maxRange = 1;

        List<SquareCell> allCells = GetCellsInChebyshevRange(attacker.GridPosition, maxRange);
        bool hasTarget = false;

        foreach (var c in allCells)
        {
            if (!c.IsOccupied || c.Occupant == attacker || c.Occupant.Stats.IsDead) continue;
            if (!IsEnemyTeam(attacker, c.Occupant)) continue;

            int distance = SquareGridUtils.GetChebyshevDistance(attacker.GridPosition, c.Coords);
            bool inRange = (type == SpecialAttackType.Feint)
                ? distance == 1
                : attacker.CanMeleeAttackDistance(distance);

            if (inRange)
            {
                c.SetHighlight(HighlightType.Attack);
                _highlightedCells.Add(c);
                hasTarget = true;
            }
        }

        SquareCell selfCell = Grid.GetCell(attacker.GridPosition);
        if (selfCell != null) selfCell.SetHighlight(HighlightType.Selected);

        if (hasTarget)
            CombatUI.SetTurnIndicator($"SPECIAL: {type} - select target (Right-click/Esc to cancel)");
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

        ExecuteSpecialAttack(attacker, cell.Occupant, _pendingSpecialAttackType);
    }

    private void ExecuteSpecialAttack(CharacterController attacker, CharacterController target, SpecialAttackType type)
    {
        if (attacker == null || target == null) { ShowActionChoices(); return; }

        CurrentSubPhase = PlayerSubPhase.Animating;
        attacker.Actions.UseStandardAction();

        SpecialAttackResult result = attacker.ExecuteSpecialAttack(type, target);
        CombatUI.ShowCombatLog($"⚔ SPECIAL [{type}]: {result.Log}");

        if (result.Success)
        {
            if (type == SpecialAttackType.BullRush)
                TryPushTargetAway(attacker, target, 1, allowAttackerFollow: true);
            else if (type == SpecialAttackType.Overrun)
                TryPushTargetAway(attacker, target, 1, allowAttackerFollow: false);
        }

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        _isSelectingSpecialAttack = false;

        UpdateAllStatsUI();

        if (target.Stats.IsDead && !target.IsPlayerControlled && AreAllNPCsDead())
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI.SetTurnIndicator("VICTORY! All enemies defeated!");
            CombatUI.SetActionButtonsVisible(false);
            return;
        }

        StartCoroutine(AfterAttackDelay(attacker, 1.0f));
    }

    private void TryPushTargetAway(CharacterController attacker, CharacterController target, int squares, bool allowAttackerFollow)
    {
        Vector2Int dir = target.GridPosition - attacker.GridPosition;
        dir.x = Mathf.Clamp(dir.x, -1, 1);
        dir.y = Mathf.Clamp(dir.y, -1, 1);
        if (dir == Vector2Int.zero) dir = Vector2Int.right;

        Vector2Int destination = target.GridPosition;
        for (int i = 0; i < squares; i++)
        {
            Vector2Int next = destination + dir;
            SquareCell nextCell = Grid.GetCell(next);
            if (nextCell == null || nextCell.IsOccupied) break;
            destination = next;
        }

        if (destination != target.GridPosition)
        {
            SquareCell destCell = Grid.GetCell(destination);
            if (destCell != null)
            {
                Vector2Int oldTargetPos = target.GridPosition;
                target.MoveToCell(destCell);
                CombatUI.ShowCombatLog($"↗ {target.Stats.CharacterName} is pushed to ({destination.x},{destination.y}).");

                if (allowAttackerFollow)
                {
                    SquareCell followCell = Grid.GetCell(oldTargetPos);
                    if (followCell != null && !followCell.IsOccupied)
                    {
                        attacker.MoveToCell(followCell);
                        CombatUI.ShowCombatLog($"{attacker.Stats.CharacterName} follows through into ({oldTargetPos.x},{oldTargetPos.y}).");
                    }
                }
            }
        }
    }

    private void CancelSpecialAttackTargeting()
    {
        _isSelectingSpecialAttack = false;
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        ShowActionChoices();
    }

    // ========== CHARGE ACTION (D&D 3.5e PHB p.154) ==========

    private void UpdateChargeHoverPreview()
    {
        if (_mainCam == null || ActivePC == null) return;
        if (CurrentSubPhase != PlayerSubPhase.SelectingChargeTarget) return;

        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        Vector3 mouseScreenPos = Vector3.zero;
#if ENABLE_LEGACY_INPUT_MANAGER
        mouseScreenPos = Input.mousePosition;
#endif
#if ENABLE_INPUT_SYSTEM
        var mouseDev = UnityEngine.InputSystem.Mouse.current;
        if (mouseDev != null)
            mouseScreenPos = mouseDev.position.ReadValue();
#endif

        Vector2 worldPoint = _mainCam.ScreenToWorldPoint(mouseScreenPos);
        Vector2Int gridCoord = SquareGridUtils.WorldToGrid(worldPoint);
        SquareCell hovered = Grid.GetCell(gridCoord);
        if (hovered == null || !hovered.IsOccupied || hovered.Occupant == null || hovered.Occupant == ActivePC)
            return;

        CharacterController target = hovered.Occupant;
        if (!CanChargeTarget(ActivePC, target, logFailures: false))
            return;

        var previewPath = GetChargePath(ActivePC.GridPosition, target.GridPosition, Mathf.Max(1, ActivePC.Stats.AttackRange));
        int pathCost = SquareGridUtils.CalculatePathCost(ActivePC.GridPosition, previewPath);
        CombatUI.SetTurnIndicator($"CHARGE ready: {target.Stats.CharacterName} ({pathCost * 5} ft). Click target to preview and confirm.");
    }

    public bool HasAnyValidChargeTarget(CharacterController charger)
    {
        if (charger == null) return false;
        var validTargets = GetValidChargeTargets(charger, false);
        return validTargets.Count > 0;
    }

    public void EnterChargeMode(CharacterController charger)
    {
        if (charger == null) return;

        if (charger.HasTakenFiveFootStep)
        {
            CombatUI?.ShowCombatLog($"⚠ {charger.Stats.CharacterName} cannot charge after taking a 5-foot step.");
            return;
        }

        if (!charger.Actions.HasFullRoundAction)
        {
            CombatUI?.ShowCombatLog($"⚠ {charger.Stats.CharacterName} needs a full-round action to charge.");
            return;
        }

        if (!charger.HasMeleeWeaponEquipped())
        {
            CombatUI?.ShowCombatLog($"⚠ {charger.Stats.CharacterName} needs a melee weapon (or natural/unarmed attack) to charge.");
            return;
        }

        var validTargets = GetValidChargeTargets(charger, true);
        if (validTargets.Count == 0)
        {
            CombatUI?.ShowCombatLog($"⚠ No valid charge targets for {charger.Stats.CharacterName}.");
            return;
        }

        _chargeTarget = null;
        _pendingChargePath.Clear();

        CurrentSubPhase = PlayerSubPhase.SelectingChargeTarget;
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        foreach (var t in validTargets)
        {
            SquareCell c = Grid.GetCell(t.GridPosition);
            if (c != null)
            {
                c.SetHighlight(HighlightType.Attack);
                _highlightedCells.Add(c);
            }
        }

        SquareCell self = Grid.GetCell(charger.GridPosition);
        if (self != null) self.SetHighlight(HighlightType.Selected);

        CombatUI.SetTurnIndicator("CHARGE: Select a target. Right-click/Esc to cancel.");
    }

    private List<CharacterController> GetValidChargeTargets(CharacterController charger, bool logFailures)
    {
        var list = new List<CharacterController>();
        if (charger == null) return list;

        foreach (var candidate in GetAllCharacters())
        {
            if (candidate == null || candidate == charger || candidate.Stats == null || candidate.Stats.IsDead)
                continue;

            if (!IsEnemyTeam(charger, candidate)) continue;

            if (CanChargeTarget(charger, candidate, logFailures: false))
                list.Add(candidate);
        }

        if (logFailures && list.Count == 0)
            CombatUI?.ShowCombatLog("⚠ No enemies meet charge requirements (distance, straight path, clear lane, line of sight).");

        return list;
    }

    public bool CanChargeTarget(CharacterController charger, CharacterController target, bool logFailures = true)
    {
        if (charger == null || target == null || charger == target) return false;
        if (charger.Stats == null || target.Stats == null || target.Stats.IsDead) return false;

        if (!IsEnemyTeam(charger, target))
        {
            if (logFailures) CombatUI?.ShowCombatLog("⚠ You can only charge an enemy target.");
            return false;
        }

        if (!charger.Actions.HasFullRoundAction)
        {
            if (logFailures) CombatUI?.ShowCombatLog("⚠ Need full-round action to charge.");
            return false;
        }

        if (charger.HasTakenFiveFootStep)
        {
            if (logFailures) CombatUI?.ShowCombatLog("⚠ Cannot charge after taking a 5-foot step.");
            return false;
        }

        if (!charger.HasMeleeWeaponEquipped())
        {
            if (logFailures) CombatUI?.ShowCombatLog("⚠ Need a melee weapon (or natural/unarmed attack) to charge.");
            return false;
        }

        if (charger.Stats.IsFatigued)
        {
            if (logFailures) CombatUI?.ShowCombatLog("⚠ Fatigued creatures cannot charge.");
            return false;
        }

        if (!IsStraightLinePath(charger.GridPosition, target.GridPosition))
        {
            if (logFailures) CombatUI?.ShowCombatLog("⚠ Must charge in a straight cardinal or diagonal line.");
            return false;
        }

        List<Vector2Int> path = GetChargePath(charger.GridPosition, target.GridPosition, Mathf.Max(1, charger.Stats.AttackRange));
        if (path == null || path.Count == 0)
        {
            if (logFailures) CombatUI?.ShowCombatLog("⚠ Cannot find a legal endpoint for this charge.");
            return false;
        }

        int minDistance = 2; // 10 ft
        int maxDistance = Mathf.Max(minDistance, charger.Stats.MoveRange * 2);
        int pathCost = SquareGridUtils.CalculatePathCost(charger.GridPosition, path);

        if (pathCost < minDistance)
        {
            if (logFailures) CombatUI?.ShowCombatLog("⚠ Target is too close to charge (minimum 10 ft / 2 squares). ");
            return false;
        }

        if (pathCost > maxDistance)
        {
            if (logFailures) CombatUI?.ShowCombatLog($"⚠ Target is too far to charge (max {maxDistance * 5} ft).");
            return false;
        }

        if (!HasChargeLineOfSight(charger, target, path))
        {
            if (logFailures) CombatUI?.ShowCombatLog("⚠ No line of sight to charge target.");
            return false;
        }

        if (!IsPathClear(path, charger, target))
        {
            if (logFailures) CombatUI?.ShowCombatLog("⚠ Charge path is blocked.");
            return false;
        }

        foreach (Vector2Int p in path)
        {
            if (IsDifficultTerrain(p))
            {
                if (logFailures) CombatUI?.ShowCombatLog("⚠ Cannot charge through difficult terrain.");
                return false;
            }
        }

        Vector2Int finalPos = path[path.Count - 1];
        int distToTarget = SquareGridUtils.GetChebyshevDistance(finalPos, target.GridPosition);
        if (!charger.CanMeleeAttackDistance(distToTarget))
        {
            if (logFailures) CombatUI?.ShowCombatLog("⚠ Charge must end in legal melee reach ring of the target.");
            return false;
        }

        return true;
    }

    public List<Vector2Int> GetChargePath(Vector2Int start, Vector2Int targetPos, int attackRange = 1)
    {
        var path = new List<Vector2Int>();
        if (!IsStraightLinePath(start, targetPos)) return path;

        Vector2Int delta = targetPos - start;
        Vector2Int direction = new Vector2Int(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
        if (direction == Vector2Int.zero) return path;

        Vector2Int current = start;
        int safety = 0;
        while (safety++ < 100)
        {
            current += direction;
            SquareCell cell = Grid.GetCell(current);
            if (cell == null) break;

            path.Add(current);

            int distToTarget = SquareGridUtils.GetChebyshevDistance(current, targetPos);
            if (distToTarget <= Mathf.Max(1, attackRange) && distToTarget > 0)
                break;

            if (current == targetPos)
                break;
        }

        if (path.Count == 0) return path;

        Vector2Int end = path[path.Count - 1];
        int endDist = SquareGridUtils.GetChebyshevDistance(end, targetPos);
        if (endDist < 1 || endDist > Mathf.Max(1, attackRange))
            path.Clear();

        return path;
    }

    public bool IsStraightLinePath(Vector2Int start, Vector2Int end)
    {
        Vector2Int delta = end - start;
        if (delta == Vector2Int.zero) return false;

        if (delta.x == 0 || delta.y == 0) return true; // cardinal
        return Mathf.Abs(delta.x) == Mathf.Abs(delta.y); // diagonal
    }

    public bool IsPathClear(List<Vector2Int> path, CharacterController charger, CharacterController target)
    {
        if (path == null || charger == null) return false;

        foreach (Vector2Int pos in path)
        {
            SquareCell cell = Grid.GetCell(pos);
            if (cell == null) return false;

            CharacterController occupant = GetCharacterAtPosition(pos);
            if (occupant != null && occupant != charger)
            {
                if (target == null || occupant != target)
                    return false;
            }
        }

        return true;
    }

    private bool HasChargeLineOfSight(CharacterController charger, CharacterController target, List<Vector2Int> path)
    {
        if (charger == null || target == null) return false;
        if (Grid.GetCell(target.GridPosition) == null) return false;
        if (path == null || path.Count == 0) return false;

        // Prototype LOS: no wall/cover system yet, so straight unobstructed charge lane is treated as LOS.
        return IsStraightLinePath(charger.GridPosition, target.GridPosition);
    }

    private bool IsDifficultTerrain(Vector2Int pos)
    {
        // Difficult terrain is not data-driven in this prototype yet.
        // Keep hook for future implementation.
        return false;
    }

    private CharacterController GetCharacterAtPosition(Vector2Int pos)
    {
        foreach (var c in GetAllCharacters())
        {
            if (c != null && c.GridPosition == pos && c.Stats != null && !c.Stats.IsDead)
                return c;
        }
        return null;
    }

    private void HandleChargeTargetClick(CharacterController charger, SquareCell cell)
    {
        if (charger == null || cell == null) return;

        if (!cell.IsOccupied || cell.Occupant == charger || cell.Occupant.Stats.IsDead)
            return;

        CharacterController target = cell.Occupant;
        if (!CanChargeTarget(charger, target, logFailures: true))
            return;

        _chargeTarget = target;
        _pendingChargePath = GetChargePath(charger.GridPosition, target.GridPosition, Mathf.Max(1, charger.Stats.AttackRange));

        CurrentSubPhase = PlayerSubPhase.ConfirmingChargePath;
        ShowChargePathPreview(charger, target);
    }

    private void HandleChargeConfirmationClick(CharacterController charger, SquareCell cell)
    {
        if (charger == null || _chargeTarget == null || _pendingChargePath == null || _pendingChargePath.Count == 0)
        {
            CancelChargeTargeting();
            return;
        }

        bool clickedTarget = cell != null && cell.IsOccupied && cell.Occupant == _chargeTarget;
        bool clickedFinal = cell != null && cell.Coords == _pendingChargePath[_pendingChargePath.Count - 1];

        if (!clickedTarget && !clickedFinal)
        {
            // Allow switching target during confirm step.
            if (cell != null && cell.IsOccupied && cell.Occupant != charger)
            {
                HandleChargeTargetClick(charger, cell);
            }
            return;
        }

        StartCoroutine(ExecuteCharge(charger, _chargeTarget));
    }

    private void ShowChargePathPreview(CharacterController charger, CharacterController target)
    {
        if (charger == null || target == null) return;
        if (!CanChargeTarget(charger, target, logFailures: false)) return;

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        foreach (Vector2Int pos in _pendingChargePath)
        {
            SquareCell c = Grid.GetCell(pos);
            if (c != null)
            {
                c.SetHighlight(HighlightType.Move);
                _highlightedCells.Add(c);
            }
        }

        SquareCell self = Grid.GetCell(charger.GridPosition);
        if (self != null) self.SetHighlight(HighlightType.Selected);
        SquareCell tcell = Grid.GetCell(target.GridPosition);
        if (tcell != null) tcell.SetHighlight(HighlightType.Attack);

        CombatUI.SetTurnIndicator($"CHARGE: {target.Stats.CharacterName} | +2 attack, -2 AC until next turn. Click target/endpoint to confirm.");
    }

    private IEnumerator ExecuteCharge(CharacterController charger, CharacterController target)
    {
        if (!CanChargeTarget(charger, target, logFailures: true))
        {
            ShowActionChoices();
            yield break;
        }

        CurrentSubPhase = PlayerSubPhase.Animating;

        List<Vector2Int> path = GetChargePath(charger.GridPosition, target.GridPosition, Mathf.Max(1, charger.Stats.AttackRange));
        if (path == null || path.Count == 0)
        {
            CombatUI?.ShowCombatLog("⚠ Charge aborted: invalid path.");
            ShowActionChoices();
            yield break;
        }

        CombatUI?.ShowCombatLog($"🏇 {charger.Stats.CharacterName} charges {target.Stats.CharacterName}!");

        // Resolve provoked AoOs during charge movement.
        var provokedAoOs = ThreatSystem.AnalyzePathForAoOs(charger, path, GetAllCharacters());
        foreach (var aooInfo in provokedAoOs)
        {
            if (charger.Stats.IsDead) break;
            if (aooInfo == null || aooInfo.Threatener == null || aooInfo.Threatener.Stats.IsDead) continue;

            CombatResult aooResult = ThreatSystem.ExecuteAoO(aooInfo.Threatener, charger);
            if (aooResult == null) continue;

            CombatUI.ShowCombatLog($"⚔ AoO during charge: {aooResult.GetDetailedSummary()}");
            UpdateAllStatsUI();

            if (aooResult.Hit && aooResult.TotalDamage > 0)
                CheckConcentrationOnDamage(charger, aooResult.TotalDamage);

            yield return new WaitForSeconds(0.8f);
        }

        if (charger.Stats.IsDead)
        {
            CombatUI?.ShowCombatLog($"{charger.Stats.CharacterName} falls before completing the charge.");
            UpdateAllStatsUI();
            if (IsPlayerTurn) EndActivePCTurn();
            yield break;
        }

        yield return StartCoroutine(charger.MoveAlongPath(path, PlayerMoveSecondsPerStep, markAsMoved: true));

        InvalidatePreviewThreats();

        // Apply +2 charge attack bonus to this attack only.
        charger.Stats.MoraleAttackBonus += 2;
        CombatResult result;
        try
        {
            result = charger.Attack(target, false, 0, null, null);
        }
        finally
        {
            charger.Stats.MoraleAttackBonus -= 2;
        }

        if (result != null)
        {
            CombatUI.ShowCombatLog($"⚡ Charge Attack (+2): {result.GetDetailedSummary()}");
            if (result.Hit && result.TotalDamage > 0)
                CheckConcentrationOnDamage(target, result.TotalDamage);
        }

        // Apply AC penalty until next turn.
        charger.Stats.ApplyCondition(CombatConditionType.ChargePenalty, 1, charger.Stats.CharacterName);
        CombatUI.ShowCombatLog($"🛡 {charger.Stats.CharacterName} is charging: -2 AC until next turn.");

        charger.Actions.UseFullRoundAction();

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        _chargeTarget = null;
        _pendingChargePath.Clear();

        UpdateAllStatsUI();

        if (target.Stats.IsDead && !target.IsPlayerControlled && AreAllNPCsDead())
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI.SetTurnIndicator("VICTORY! All enemies defeated!");
            CombatUI.SetActionButtonsVisible(false);
            yield break;
        }

        StartCoroutine(AfterAttackDelay(charger, 1.0f));
    }

    private void CancelChargeTargeting()
    {
        _chargeTarget = null;
        _pendingChargePath.Clear();
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        ShowActionChoices();
    }

    // ========== ATTACK EXECUTION ==========

    private void PerformPlayerAttack(CharacterController attacker, CharacterController target)
    {
        CurrentSubPhase = PlayerSubPhase.Animating;

        // Check flanking against all combatants (team/threat rules applied by CombatUtils).
        var allCombatants = GetAllCharacters();

        CharacterController flankPartner;
        bool isFlanking = CombatUtils.IsAttackerFlanking(attacker, target, allCombatants, out flankPartner);
        int flankBonus = isFlanking ? CombatUtils.FlankingAttackBonus : 0;
        string partnerName = flankPartner != null ? flankPartner.Stats.CharacterName : "";

        if (CombatUI != null)
        {
            string flankIndicator = CombatUI.BuildFlankingIndicator(isFlanking, flankPartner);
            CombatUI.SetTurnIndicator($"{attacker.Stats.CharacterName} attacks {target.Stats.CharacterName}{flankIndicator}");
        }
        RangeInfo rangeInfo = CalculateRangeInfo(attacker, target);
        // Targeting is resolved; clear pending declaration marker.
        _pendingDefensiveAttackSelection = false;

        switch (_pendingAttackMode)
        {
            case PendingAttackMode.Single:
                PerformSingleAttack(attacker, target, isFlanking, flankBonus, partnerName, rangeInfo);
                break;
            case PendingAttackMode.FullAttack:
                StartCoroutine(PerformFullAttackWithRetargetingAndFiveFootStep(attacker, target));
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
        string flankLogPrefix = isFlanking
            ? $"⚔ {attacker.Stats.CharacterName} gains +2 flanking bonus{(string.IsNullOrEmpty(partnerName) ? "" : $" (with {partnerName})")}.\n"
            : string.Empty;
        _lastCombatLog = flankLogPrefix + result.GetDetailedSummary();

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

        if (result.TargetKilled)
        {
            HandleSummonDeathCleanup(target);

            if (!target.IsPlayerControlled)
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
        }

        StartCoroutine(AfterAttackDelay(attacker, 1.5f));
    }

    private IEnumerator PerformFullAttackWithRetargetingAndFiveFootStep(CharacterController attacker, CharacterController initialTarget)
    {
        if (attacker == null || initialTarget == null)
        {
            ShowActionChoices();
            yield break;
        }

        attacker.Actions.UseFullRoundAction();

        bool rangedMode = attacker.IsEquippedWeaponRanged();
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
        attacker.Actions.UseFullRoundAction();

        FullAttackResult result = attacker.FullAttack(target, isFlanking, flankBonus, partnerName, rangeInfo);
        string flankLogPrefix = isFlanking
            ? $"⚔ {attacker.Stats.CharacterName} gains +2 flanking bonus{(string.IsNullOrEmpty(partnerName) ? "" : $" (with {partnerName})")}.\n"
            : string.Empty;
        _lastCombatLog = flankLogPrefix + result.GetFullSummary();

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

        if (result.TargetKilled)
        {
            HandleSummonDeathCleanup(target);

            if (!target.IsPlayerControlled)
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
        }

        StartCoroutine(DelayedEndActivePCTurn(2.0f));
    }

    private void PerformDualWieldAttack(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo = null)
    {
        attacker.Actions.UseFullRoundAction();

        FullAttackResult result = attacker.DualWieldAttack(target, isFlanking, flankBonus, partnerName, rangeInfo);
        string flankLogPrefix = isFlanking
            ? $"⚔ {attacker.Stats.CharacterName} gains +2 flanking bonus{(string.IsNullOrEmpty(partnerName) ? "" : $" (with {partnerName})")}.\n"
            : string.Empty;
        _lastCombatLog = flankLogPrefix + result.GetFullSummary();

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

        if (result.TargetKilled)
        {
            HandleSummonDeathCleanup(target);

            if (!target.IsPlayerControlled)
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
        }

        StartCoroutine(DelayedEndActivePCTurn(2.0f));
    }

    private void PerformFlurryOfBlows(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo = null)
    {
        attacker.Actions.UseFullRoundAction();

        FullAttackResult result = attacker.FlurryOfBlows(target, isFlanking, flankBonus, partnerName, rangeInfo);
        string flankLogPrefix = isFlanking
            ? $"⚔ {attacker.Stats.CharacterName} gains +2 flanking bonus{(string.IsNullOrEmpty(partnerName) ? "" : $" (with {partnerName})")}.\n"
            : string.Empty;
        _lastCombatLog = flankLogPrefix + result.GetFullSummary();

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

        if (result.TargetKilled)
        {
            HandleSummonDeathCleanup(target);

            if (!target.IsPlayerControlled)
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
        }

        StartCoroutine(DelayedEndActivePCTurn(2.0f));
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
        if (character == null) return true;
        return !character.Actions.HasAnyActionLeft && !IsHoldingTouchCharge(character);
    }

    private IEnumerator AfterAttackDelay(CharacterController pc, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (CurrentPhase == TurnPhase.CombatOver) yield break;

        if (ShouldAutoEndTurn(pc))
        {
            EndActivePCTurn();
        }
        else
        {
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

        if (CurrentPhase == TurnPhase.CombatOver)
            yield break;

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
        _activeTurnCharacter = npc;
        CombatUI.SetActivePC(0); // No PC active
        CombatUI.SetActiveNPC(NPCs.IndexOf(npc)); // Highlight active NPC
        CombatUI.SetActionButtonsVisible(false);
        CombatUI.HideSummonContextMenu();

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

        bool isSummon = IsSummonedCreature(npc);
        string turnColor = isSummon ? "#66E8FF" : "#FF6666";
        string turnIcon = isSummon ? "✶" : "💀";

        CombatUI.SetTurnIndicator($"{GetSummonDisplayName(npc)}'s turn...");
        CombatUI.ShowCombatLog($"<color={turnColor}>{turnIcon} {GetSummonDisplayName(npc)}'s turn begins</color>");
        yield return new WaitForSeconds(0.6f);

        CharacterController targetPC = GetClosestAlivePCTo(npc);
        if (targetPC == null) yield break;

        if (isSummon)
        {
            yield return StartCoroutine(AI_SummonedCreature(npc));
            yield break;
        }

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
        if (npc == null || targetPC == null || targetPC.Stats == null || targetPC.Stats.IsDead)
            yield break;

        if (ShouldNPCUseCharge(npc, targetPC))
        {
            yield return StartCoroutine(NPCExecuteCharge(npc, targetPC));
            yield break;
        }

        if (!npc.IsTargetInCurrentWeaponRange(targetPC))
        {
            SquareCell bestCell = FindBestMoveToward(npc, targetPC);
            if (bestCell != null)
            {
                yield return StartCoroutine(MoveCharacterAlongComputedPath(npc, bestCell.Coords, PlayerMoveSecondsPerStep));
                npc.Actions.UseMoveAction();
                CombatUI.ShowCombatLog($"{npc.Stats.CharacterName} advances toward {targetPC.Stats.CharacterName}!");
                yield return new WaitForSeconds(0.5f);
            }
        }

        targetPC = GetClosestAlivePCTo(npc);
        if (targetPC == null) yield break;

        if (npc.IsTargetInCurrentWeaponRange(targetPC) && !targetPC.Stats.IsDead)
        {
            if (!TryNPCSpecialAttackIfBeneficial(npc, targetPC))
                yield return StartCoroutine(NPCPerformAttack(npc, targetPC));
            else
                yield return new WaitForSeconds(0.8f);
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
                yield return StartCoroutine(MoveCharacterAlongComputedPath(npc, retreatCell.Coords, PlayerMoveSecondsPerStep));
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
            if (!TryNPCSpecialAttackIfBeneficial(npc, rangedTarget))
                yield return StartCoroutine(NPCPerformAttack(npc, rangedTarget));
            else
                yield return new WaitForSeconds(0.8f);
        }
        else if (distToRangedTarget > maxRange && npc.Actions.HasMoveAction)
        {
            SquareCell approachCell = FindBestMoveToward(npc, rangedTarget.GridPosition);
            if (approachCell != null)
            {
                yield return StartCoroutine(MoveCharacterAlongComputedPath(npc, approachCell.Coords, PlayerMoveSecondsPerStep));
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
        CharacterController weakerPC = GetWeakerAlivePC(npc);
        if (weakerPC != null) targetPC = weakerPC;
        if (npc == null || targetPC == null || targetPC.Stats == null || targetPC.Stats.IsDead)
            yield break;

        if (ShouldNPCUseCharge(npc, targetPC))
        {
            yield return StartCoroutine(NPCExecuteCharge(npc, targetPC));
            yield break;
        }

        if (!npc.IsTargetInCurrentWeaponRange(targetPC))
        {
            SquareCell bestCell = FindBestMoveToward(npc, targetPC);
            if (bestCell != null)
            {
                yield return StartCoroutine(MoveCharacterAlongComputedPath(npc, bestCell.Coords, PlayerMoveSecondsPerStep));
                npc.Actions.UseMoveAction();
                CombatUI.ShowCombatLog($"{npc.Stats.CharacterName} advances methodically toward {targetPC.Stats.CharacterName}.");
                yield return new WaitForSeconds(0.5f);
            }
        }

        targetPC = GetClosestAlivePCTo(npc);
        if (targetPC == null) yield break;

        if (npc.IsTargetInCurrentWeaponRange(targetPC) && !targetPC.Stats.IsDead)
        {
            if (!TryNPCSpecialAttackIfBeneficial(npc, targetPC))
                yield return StartCoroutine(NPCPerformAttack(npc, targetPC));
            else
                yield return new WaitForSeconds(0.8f);
        }
        else
        {
            yield return new WaitForSeconds(0.3f);
        }
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

        if (lowHP)
        {
            SquareCell retreat = FindBestMoveAwayFrom(summon, target.GridPosition);
            if (retreat != null && retreat.Coords != summon.GridPosition)
            {
                yield return StartCoroutine(MoveCharacterAlongComputedPath(summon, retreat.Coords, PlayerMoveSecondsPerStep));
                if (summon.Actions.HasMoveAction)
                    summon.Actions.UseMoveAction();
                CombatUI.ShowCombatLog($"<color=#FFCC66>{GetSummonDisplayName(summon)} withdraws to survive.</color>");
                yield return new WaitForSeconds(0.45f);
            }
        }

        if (!summon.IsTargetInCurrentWeaponRange(target) && summon.Actions.HasMoveAction)
        {
            SquareCell bestCell = FindBestMoveToward(summon, target);
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
            summon.Actions.UseStandardAction();
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

        summon.Actions.UseStandardAction();
        summonData.SmiteUsed = true;

        string targetAxis = smiteEvil ? "Evil" : "Good";
        CombatUI.ShowCombatLog($"<color=#FFD280>✦ {GetSummonDisplayName(summon)} uses Smite {targetAxis}! {result.GetDetailedSummary()}</color>");

        if (result.TargetKilled)
            HandleSummonDeathCleanup(target);

        return true;
    }
    private bool ShouldNPCUseCharge(CharacterController npc, CharacterController target)
    {
        if (npc == null || target == null) return false;
        if (!npc.Actions.HasFullRoundAction) return false;
        if (!npc.HasMeleeWeaponEquipped()) return false;
        if (target.Stats == null || target.Stats.IsDead) return false;

        int dist = SquareGridUtils.GetChebyshevDistance(npc.GridPosition, target.GridPosition);
        // Prefer charge when out of melee reach but still reachable in a charge lane.
        if (npc.CanMeleeAttackDistance(dist)) return false;

        return CanChargeTarget(npc, target, logFailures: false);
    }

    private IEnumerator NPCExecuteCharge(CharacterController npc, CharacterController target)
    {
        if (!CanChargeTarget(npc, target, logFailures: false))
            yield break;

        List<Vector2Int> path = GetChargePath(npc.GridPosition, target.GridPosition, Mathf.Max(1, npc.Stats.AttackRange));
        if (path == null || path.Count == 0)
            yield break;

        CombatUI.ShowCombatLog($"🏇 {npc.Stats.CharacterName} charges {target.Stats.CharacterName}!");

        var provokedAoOs = ThreatSystem.AnalyzePathForAoOs(npc, path, GetAllCharacters());
        foreach (var aooInfo in provokedAoOs)
        {
            if (npc.Stats.IsDead) break;
            if (aooInfo == null || aooInfo.Threatener == null || aooInfo.Threatener.Stats.IsDead) continue;

            CombatResult aooResult = ThreatSystem.ExecuteAoO(aooInfo.Threatener, npc);
            if (aooResult == null) continue;

            CombatUI.ShowCombatLog($"⚔ AoO vs {npc.Stats.CharacterName}: {aooResult.GetDetailedSummary()}");
            UpdateAllStatsUI();

            if (aooResult.Hit && aooResult.TotalDamage > 0)
                CheckConcentrationOnDamage(npc, aooResult.TotalDamage);

            yield return new WaitForSeconds(0.5f);
        }

        if (npc.Stats.IsDead)
            yield break;

        yield return StartCoroutine(npc.MoveAlongPath(path, NpcChargeMoveSecondsPerStep, markAsMoved: true));

        CharacterController flankPartner;
        bool isFlankingCharge = CombatUtils.IsAttackerFlanking(npc, target, GetAllCharacters(), out flankPartner);
        int flankingBonus = isFlankingCharge ? CombatUtils.FlankingAttackBonus : 0;

        npc.Stats.MoraleAttackBonus += 2;
        CombatResult result;
        try
        {
            result = npc.Attack(target, isFlankingCharge, flankingBonus,
                flankPartner != null ? flankPartner.Stats.CharacterName : null, null);
        }
        finally
        {
            npc.Stats.MoraleAttackBonus -= 2;
        }

        if (result != null)
        {
            string flankText = isFlankingCharge ? " + Flanking" : "";
            CombatUI.ShowCombatLog($"☠ Charge Attack (+2{flankText}): {result.GetDetailedSummary()}");
            if (result.Hit && result.TotalDamage > 0)
                CheckConcentrationOnDamage(target, result.TotalDamage);
        }

        npc.Stats.ApplyCondition(CombatConditionType.ChargePenalty, 1, npc.Stats.CharacterName);
        npc.Actions.UseFullRoundAction();
        UpdateAllStatsUI();

        yield return new WaitForSeconds(0.8f);
    }

    private bool TryNPCSpecialAttackIfBeneficial(CharacterController npc, CharacterController target)
    {
        if (npc == null || target == null) return false;
        if (!npc.Actions.HasStandardAction) return false;

        SpecialAttackType? choice = null;

        if (!target.Stats.IsProne && npc.HasMeleeWeaponEquipped())
            choice = SpecialAttackType.Trip;

        if (choice == null && target.GetEquippedMainWeapon() != null && npc.Stats.STRMod >= 3)
            choice = SpecialAttackType.Disarm;

        if (choice == null && npc.Stats.STRMod >= 4)
            choice = SpecialAttackType.Grapple;

        if (choice == null) return false;

        var result = npc.ExecuteSpecialAttack(choice.Value, target);
        CombatUI.ShowCombatLog($"☠ {npc.Stats.CharacterName} uses SPECIAL [{choice.Value}]! {result.Log}");

        if (result.Success)
        {
            if (choice.Value == SpecialAttackType.BullRush)
                TryPushTargetAway(npc, target, 1, allowAttackerFollow: true);
            else if (choice.Value == SpecialAttackType.Overrun)
                TryPushTargetAway(npc, target, 1, allowAttackerFollow: false);
        }

        npc.Actions.UseStandardAction();
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

        npc.Actions.UseStandardAction();
        RangeInfo npcRangeInfo = CalculateRangeInfo(npc, target);

        CharacterController flankPartner;
        bool isFlanking = CombatUtils.IsAttackerFlanking(npc, target, GetAllCharacters(), out flankPartner);
        int flankBonus = isFlanking ? CombatUtils.FlankingAttackBonus : 0;

        CombatResult result = npc.Attack(target, isFlanking, flankBonus,
            flankPartner != null ? flankPartner.Stats.CharacterName : null, npcRangeInfo);

        string flankLogPrefix = isFlanking
            ? $"⚔ {npc.Stats.CharacterName} gains +2 flanking bonus{(flankPartner != null ? $" (with {flankPartner.Stats.CharacterName})" : "")}.\n"
            : string.Empty;
        _lastCombatLog = flankLogPrefix + result.GetDetailedSummary();

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

    /// <summary>Find closest alive enemy to a specific combatant.</summary>
    private CharacterController GetClosestAlivePCTo(CharacterController npc)
    {
        return GetClosestAliveEnemyTo(npc);
    }

    /// <summary>Legacy wrapper for backward compat.</summary>
    private CharacterController GetClosestAlivePC()
    {
        return (NPC != null) ? GetClosestAlivePCTo(NPC) : null;
    }

    /// <summary>Get the alive enemy with the lowest current HP (for defensive AI targeting).</summary>
    private CharacterController GetWeakerAlivePC(CharacterController npc)
    {
        CharacterController weakest = null;
        int lowestHP = int.MaxValue;

        foreach (var candidate in GetAllCharacters())
        {
            if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!IsEnemyTeam(npc, candidate))
                continue;

            if (candidate.Stats.CurrentHP < lowestHP)
            {
                lowestHP = candidate.Stats.CurrentHP;
                weakest = candidate;
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

    private SquareCell FindBestMoveToward(CharacterController mover, CharacterController target)
    {
        if (target == null)
            return null;

        return FindBestMoveToward(mover, target.GridPosition, target);
    }

    private SquareCell FindBestMoveToward(CharacterController mover, Vector2Int targetPos)
    {
        return FindBestMoveToward(mover, targetPos, null);
    }

    private SquareCell FindBestMoveToward(CharacterController mover, Vector2Int targetPos, CharacterController targetCharacter)
    {
        List<SquareCell> moveCells = Grid.GetCellsInRange(mover.GridPosition, mover.Stats.MoveRange);
        SquareCell bestCell = null;
        int bestDist = int.MaxValue;
        bool bestCanThreaten = false;
        bool bestWouldFlank = false;

        List<CharacterController> allCombatants = null;
        if (targetCharacter != null)
            allCombatants = GetAllCharacters();

        foreach (var cell in moveCells)
        {
            if (cell.IsOccupied) continue;

            int dist = SquareGridUtils.GetDistance(cell.Coords, targetPos);

            bool canThreatenFromCell = false;
            bool wouldFlankFromCell = false;

            if (targetCharacter != null)
            {
                canThreatenFromCell = CombatUtils.CanThreatenTargetFromPosition(mover, cell.Coords, targetCharacter);
                if (canThreatenFromCell)
                {
                    CharacterController flankPartner;
                    wouldFlankFromCell = CombatUtils.IsAttackerFlankingFromPosition(
                        mover,
                        cell.Coords,
                        targetCharacter,
                        allCombatants,
                        out flankPartner);
                }
            }

            bool better = false;

            // Tactical priority: prefer valid flanking setups first, then threatening squares,
            // then shortest approach distance.
            if (wouldFlankFromCell != bestWouldFlank)
                better = wouldFlankFromCell;
            else if (canThreatenFromCell != bestCanThreaten)
                better = canThreatenFromCell;
            else if (dist < bestDist)
                better = true;

            if (better)
            {
                bestDist = dist;
                bestCell = cell;
                bestCanThreaten = canThreatenFromCell;
                bestWouldFlank = wouldFlankFromCell;
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
            if (first.FightingDefensivelyAttackPenalty != 0) feats.Add("Fighting Defensively");
            if (first.ShootingIntoMeleePenalty != 0) feats.Add("Shooting into melee");
            if (first.PreciseShotNegated) feats.Add("Precise Shot");
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

            if (atk.FightingDefensivelyAttackPenalty != 0)
                Debug.Log($"[Combat]     {FormatConsoleModLine(atk.FightingDefensivelyAttackPenalty, "Fighting Defensively")}");

            if (atk.ShootingIntoMeleePenalty != 0)
                Debug.Log($"[Combat]     {FormatConsoleModLine(atk.ShootingIntoMeleePenalty, "shooting into melee")}");
            else if (atk.PreciseShotNegated)
                Debug.Log("[Combat]     + 0 (Precise Shot negates shooting into melee penalty)");

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