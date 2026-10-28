using UnityEngine;
using UnityEngine.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DND35.AI;
using DND35.Magic;
using Random = UnityEngine.Random;  // Resolve ambiguity with System.Random
using DND35e.Identifiers;

public enum SpecialAttackType
{
    Trip,
    Disarm,
    Grapple,
    Sunder,
    BullRushAttack,
    BullRushCharge,
    Overrun,
    Feint,
    AidAnother,
    WakeSleepingAlly,
    CoupDeGrace,
    TurnUndead
}

public enum GrappleActionType
{
    EscapeArtist,
    OpposedGrappleEscape,
    DamageOpponent,
    AttackWithLightWeapon,
    AttackUnarmed,
    PinOpponent,
    MoveHalfSpeed,
    UseOpponentWeapon,
    DisarmSmallObject,
    DrawLightWeapon,
    RetrieveSpellComponent,
    ReleasePinnedOpponent,
    /// <summary>The rake's extra claw attacks against the grappled foe (MM p.314): needs a full attack and a turn begun grappling (CMB-144).</summary>
    Rake
}

public enum AttackDamageMode
{
    Lethal,
    Nonlethal
}

public struct DisarmableHeldItemOption
{
    public EquipSlot HandSlot;
    public ItemData HeldItem;

    public DisarmableHeldItemOption(EquipSlot handSlot, ItemData heldItem)
    {
        HandSlot = handSlot;
        HeldItem = heldItem;
    }
}

public enum SunderTargetKind
{
    MainHand,
    OffHand,
    Shield,
    Armor
}

public struct SunderableItemOption
{
    public EquipSlot Slot;
    public ItemData Item;
    public SunderTargetKind Kind;

    public SunderableItemOption(EquipSlot slot, ItemData item, SunderTargetKind kind)
    {
        Slot = slot;
        Item = item;
        Kind = kind;
    }

    public string GetLabel()
    {
        string itemName = Item != null ? Item.Name : "Item";
        switch (Kind)
        {
            case SunderTargetKind.MainHand: return $"Main Hand Weapon: {itemName}";
            case SunderTargetKind.OffHand: return $"Off-Hand Item: {itemName}";
            case SunderTargetKind.Shield: return $"Shield: {itemName}";
            case SunderTargetKind.Armor: return $"Armor: {itemName}";
            default: return itemName;
        }
    }
}

public class GrappleCheckResult
{
    public int BaseRoll;
    public int BaseAttackBonus;
    public int StrengthModifier;
    public int SizeModifier;
    public int MiscModifier;
    public int Total;

    public string CharacterName;
    public readonly List<string> MiscBreakdown = new List<string>();
    private readonly List<int> _miscValues = new List<int>();

    public void AddMiscModifier(int value, string source)
    {
        if (value == 0)
            return;

        MiscModifier += value;
        if (!string.IsNullOrEmpty(source))
        {
            MiscBreakdown.Add(source);
            _miscValues.Add(value);
        }
    }

    private static string FormatSigned(int value)
    {
        return value >= 0 ? $"+{value}" : value.ToString();
    }

    public string GetBreakdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{CharacterName}'s grapple check:");
        sb.AppendLine($"  Base roll: 1d20 = {BaseRoll}");
        sb.AppendLine($"  BAB: {FormatSigned(BaseAttackBonus)}");
        sb.AppendLine($"  STR modifier: {FormatSigned(StrengthModifier)}");
        sb.AppendLine($"  Size modifier: {FormatSigned(SizeModifier)}");

        // Listed whenever a modifier is named, also when they cancel out (Improved Grapple +4 and the -4 for nonlethal
        // Improved Grab hold damage).
        if (MiscModifier != 0 || MiscBreakdown.Count > 0)
        {
            if (MiscBreakdown.Count == 1)
                sb.AppendLine($"  Misc modifiers: {FormatSigned(MiscModifier)} ({MiscBreakdown[0]})");
            else
            {
                sb.AppendLine($"  Misc modifiers: {FormatSigned(MiscModifier)}");
                for (int i = 0; i < MiscBreakdown.Count; i++)
                    sb.AppendLine($"    - {MiscBreakdown[i]} ({FormatSigned(_miscValues[i])})");
            }
        }

        sb.AppendLine($"  Total: {Total}");
        return sb.ToString().TrimEnd();
    }
}

public class BullRushCheckResult
{
    public int BaseRoll;
    public int BaseAttackBonus;
    public int StrengthModifier;
    public int StrengthOrDexterityModifier;
    public int SizeModifier;
    public int ChargeBonus;
    public int StabilityBonus;
    public int MiscModifier;
    public int Total;
    public string CharacterName;
    public bool UsesBestStrengthOrDexterity;
    public readonly List<string> MiscBreakdown = new List<string>();

    public void AddMiscModifier(int value, string source)
    {
        if (value == 0)
            return;

        MiscModifier += value;
        if (!string.IsNullOrEmpty(source))
            MiscBreakdown.Add(source);
    }

    private static string FormatSigned(int value)
    {
        return value >= 0 ? $"+{value}" : value.ToString();
    }

    public string GetBreakdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{CharacterName}'s bull rush check:");
        sb.AppendLine($"  Base roll: 1d20 = {BaseRoll}");
        if (BaseAttackBonus != 0)
            sb.AppendLine($"  BAB: {FormatSigned(BaseAttackBonus)}");

        if (UsesBestStrengthOrDexterity)
            sb.AppendLine($"  STR/DEX modifier: {FormatSigned(StrengthOrDexterityModifier)}");
        else
            sb.AppendLine($"  STR modifier: {FormatSigned(StrengthModifier)}");

        sb.AppendLine($"  Size modifier: {FormatSigned(SizeModifier)}");

        if (ChargeBonus != 0)
            sb.AppendLine($"  Charge bonus: {FormatSigned(ChargeBonus)}");

        if (StabilityBonus != 0)
            sb.AppendLine($"  Stability bonus: {FormatSigned(StabilityBonus)}");

        if (MiscModifier != 0)
        {
            if (MiscBreakdown.Count == 1)
                sb.AppendLine($"  Misc modifiers: {FormatSigned(MiscModifier)} ({MiscBreakdown[0]})");
            else
            {
                sb.AppendLine($"  Misc modifiers: {FormatSigned(MiscModifier)}");
                for (int i = 0; i < MiscBreakdown.Count; i++)
                    sb.AppendLine($"    - {MiscBreakdown[i]}");
            }
        }

        sb.AppendLine($"  Total: {Total}");
        return sb.ToString().TrimEnd();
    }
}

public enum CharacterTeam
{
    Player,
    Enemy,
    Neutral
}

/// <summary>
/// Controls a character on the square grid (both PC and NPC).
/// Supports D&D 3.5 action economy, full attacks, dual wielding, and critical hits.
/// Enhanced with detailed combat log breakdown fields.
/// </summary>
public class CharacterController : MonoBehaviour
{
    [Header("Character Setup")]
    [FormerlySerializedAs("IsPlayerControlled")]
    [SerializeField] private bool _isPlayerControlled;
    [SerializeField] private CharacterTeam _team = CharacterTeam.Enemy;
    [SerializeField] private bool _isControllable;
    [SerializeField] private bool _controllableExplicitlySet;

    /// <summary>
    /// Team-side compatibility flag used throughout legacy combat checks.
    /// True => Player team, False => Enemy/neutral team.
    /// </summary>
    public bool IsPlayerControlled
    {
        get => _isPlayerControlled;
        set
        {
            _isPlayerControlled = value;
            _team = value ? CharacterTeam.Player : CharacterTeam.Enemy;

            // Backward compatibility: if explicit controllability was never configured,
            // preserve historical behavior where player-side actors were directly controlled.
            if (!_controllableExplicitlySet)
                _isControllable = value;
        }
    }

    /// <summary>Current combat team for ally/enemy evaluation.</summary>
    public CharacterTeam Team => _team;

    /// <summary>True when this character receives player input instead of AI automation.</summary>
    public bool IsControllable
    {
        get => _isControllable;
        set
        {
            _isControllable = value;
            _controllableExplicitlySet = true;
        }
    }

    /// <summary>Convenience flag for AI execution checks.</summary>
    public bool UsesAI => !IsControllable;

    public void SetTeam(CharacterTeam team)
    {
        _team = team;
        _isPlayerControlled = team == CharacterTeam.Player;

        if (!_controllableExplicitlySet)
            _isControllable = _isPlayerControlled;
    }

    private void NormalizeTeamControlState()
    {
        if (_team == CharacterTeam.Player)
            _isPlayerControlled = true;
        else if (_isPlayerControlled)
            _team = CharacterTeam.Player;

        if (!_controllableExplicitlySet)
            _isControllable = _isPlayerControlled;
    }

    public void SetControllable(bool controllable)
    {
        IsControllable = controllable;
    }

    public void ConfigureTeamControl(CharacterTeam team, bool controllable)
    {
        _team = team;
        _isPlayerControlled = team == CharacterTeam.Player;
        _isControllable = controllable;
        _controllableExplicitlySet = true;
    }

    /// <summary>
    /// True for a controller of the encounter enemy pool (<c>GameManager.CreateNPCPoolSlot</c>: the slots SceneBootstrap
    /// builds and those SetupEnemyEncounter adds for a larger encounter, ENC-001); false for party slots, summons and
    /// other controllers added to <c>GameManager.NPCs</c>. Set once at creation; a slot reset (CRE-046) keeps it.
    /// </summary>
    public bool IsEncounterPoolSlot { get; internal set; }

    [Header("AI Configuration")]
    [Tooltip("Optional AI profile used by AIService for NPC decision making")]
    public AIProfile aiProfile;

    [HideInInspector] public bool? EnemyUseCoupDeGraceOverride;

    /// <summary>
    /// Optional explicit enemy name this NPC should prioritize when selecting targets.
    /// </summary>
    public string PriorityTargetName { get; set; }

    [Header("Sprites")]
    public Sprite AliveSprite;
    public Sprite DeadSprite;

    [HideInInspector] public CharacterStats Stats;
    [HideInInspector] public Vector2Int GridPosition;

    private string _displayedRace;
    public DisguiseSelfEffectData ActiveDisguiseSelfEffect { get; private set; }
    public ExpeditiousRetreatEffectData ActiveExpeditiousRetreatEffect { get; private set; }
    public InvisibilityEffectData ActiveInvisibilityEffect { get; private set; }
    public SeeInvisibilityEffectData ActiveSeeInvisibilityEffect { get; private set; }
    public GlitterdustEffectData ActiveGlitterdustEffect { get; private set; }
    public MelfsAcidArrowEffectData ActiveMelfsAcidArrowEffect { get; private set; }
    public BlindnessDeafnessEffectData ActiveBlindnessDeafnessEffect { get; private set; }
    public HasteEffectData ActiveHasteEffect { get; private set; }
    public SlowEffectData ActiveSlowEffect { get; private set; }
    public CommandUndeadEffectData ActiveCommandUndeadEffect { get; private set; }
    public FalseLifeEffectData ActiveFalseLifeEffect { get; private set; }
    public GhoulTouchEffectData ActiveGhoulTouchEffect { get; private set; }
    public ScareEffectData ActiveScareEffect { get; private set; }
    public SpectralHandEffectData ActiveSpectralHandEffect { get; private set; }

    /// <summary>Active alignment/undead detection effect (Detect Chaos/Evil/Good/Law/Undead).</summary>
    public AlignmentDetectionEffectData ActiveAlignmentDetectionEffect { get; private set; }

    /// <summary>Whether this character has an active alignment/undead detection effect.</summary>
    public bool HasActiveAlignmentDetection => ActiveAlignmentDetectionEffect != null;

    /// <summary>Apply an alignment detection effect to this character (the caster).</summary>
    public void ApplyAlignmentDetectionEffect(AlignmentDetectionEffectData data)
    {
        ActiveAlignmentDetectionEffect = data;
        Debug.Log($"[AlignmentDetection] {Stats?.CharacterName} now detecting {data.Type} (label={data.StatusLabel})");
    }

    /// <summary>Update the detection scan (call each round while concentrating).</summary>
    public void UpdateAlignmentDetectionScan(System.Collections.Generic.List<CharacterController> allCharacters)
    {
        if (ActiveAlignmentDetectionEffect == null) return;
        ActiveAlignmentDetectionEffect.ConcentrationRounds++;
        ActiveAlignmentDetectionEffect.ScanForCreatures(allCharacters);
    }

    /// <summary>Tick down detection duration. Returns true if expired.</summary>
    public bool TickAlignmentDetectionDuration()
    {
        if (ActiveAlignmentDetectionEffect == null) return true;
        ActiveAlignmentDetectionEffect.DurationRemainingRounds = Mathf.Max(0, ActiveAlignmentDetectionEffect.DurationRemainingRounds - 1);
        if (ActiveAlignmentDetectionEffect.DurationRemainingRounds <= 0)
        {
            RemoveAlignmentDetectionEffect();
            return true;
        }
        return false;
    }

    /// <summary>Remove the active alignment detection effect.</summary>
    public AlignmentDetectionEffectData RemoveAlignmentDetectionEffect()
    {
        var removed = ActiveAlignmentDetectionEffect;
        ActiveAlignmentDetectionEffect = null;
        if (removed != null)
            Debug.Log($"[AlignmentDetection] {Stats?.CharacterName} detection of {removed.Type} ended.");
        return removed;
    }

    /// <summary>Active attribute enhancement effects (one per ability score). Keyed by AbilityType.</summary>
    private readonly System.Collections.Generic.Dictionary<AbilityType, AttributeEnhancementEffectData> _activeAttributeEnhancements
        = new System.Collections.Generic.Dictionary<AbilityType, AttributeEnhancementEffectData>();

    private readonly System.Collections.Generic.List<CommandUndeadEffectData> _commandedUndeadList = new System.Collections.Generic.List<CommandUndeadEffectData>();
    private EnfeebledConditionData _activeEnfeeblementEffect;
    private TouchOfIdiocyConditionData _activeTouchOfIdiocyEffect;
    public EnfeebledConditionData ActiveEnfeeblementEffect => _activeEnfeeblementEffect;
    public TouchOfIdiocyConditionData ActiveTouchOfIdiocyEffect => _activeTouchOfIdiocyEffect;
    public int TotalEnfeeblementStrengthPenalty => Stats != null ? Stats.EnfeeblementStrengthPenalty : 0;
    public string ActualRace => Stats != null && !string.IsNullOrWhiteSpace(Stats.RaceName) ? Stats.RaceName : "Unknown";
    public string DisplayedRace => !string.IsNullOrWhiteSpace(_displayedRace) ? _displayedRace : ActualRace;
    [HideInInspector] public bool HasMovedThisTurn;
    [HideInInspector] public bool HasTakenFiveFootStep;
    [HideInInspector] public bool HasAttackedThisTurn;
    [HideInInspector] public bool IsWithdrawing;

    /// <summary>Action economy tracker for the current turn.</summary>
    public ActionEconomy Actions = new ActionEconomy();

    /// <summary>
    /// The creature's attack sequence for this turn: the first attack spends the standard action and a second one turns
    /// the turn into a full attack, as PHB p.143 "Deciding between an Attack or a Full Attack" allows (see AttackPool).
    /// The pool is rebuilt at turn start and consumed as attack actions are committed.
    /// </summary>
    public AttackPool ProgressiveAttackPool { get; } = new AttackPool();

    /// <summary>
    /// STOPGAP AI memory of this creature's own maneuvers this turn (owner decision 2026-10-07, AI-060):
    /// read and written only by AIService's maneuver evaluation, cleared by <see cref="StartNewTurn"/>.
    /// It limits AI choices only; no rule reads it. See <see cref="DND35.AI.AIManeuverTurnMemory"/>.
    /// </summary>
    public DND35.AI.AIManeuverTurnMemory AIManeuverMemory { get; } = new DND35.AI.AIManeuverTurnMemory();

    /// <summary>
    /// The opponents whose movement opportunity against this creature already came this round (PHB p.138,
    /// owner ruling 2026-10-08, CMB-128). Read and written only through ThreatSystem.HasHadMovementOpportunity,
    /// RecordMovementOpportunity and ClearMovementOpportunities; cleared by <see cref="StartNewTurn"/>.
    /// </summary>
    public HashSet<CharacterController> MovementOpportunityThreateners { get; } = new HashSet<CharacterController>();

    // ========== FEAT PROPERTIES ==========

    /// <summary>
    /// Power Attack value: subtract from melee attack rolls, add to melee damage.
    /// Valid range: 0 to BAB. Two-handed weapons get 2× damage bonus.
    /// </summary>
    public int PowerAttackValue { get; private set; }

    /// <summary>
    /// Whether Rapid Shot is enabled. When active during a full attack with a ranged weapon,
    /// grants one extra attack at highest BAB but all attacks take -2 penalty.
    /// </summary>
    public bool RapidShotEnabled { get; private set; }

    /// <summary>
    /// D&D 3.5: Fighting Defensively stance.
    /// While active: -4 attack rolls, +2 dodge AC until start of next turn.
    /// </summary>
    public bool IsFightingDefensively { get; private set; }

    /// <summary>
    /// Current selected attack damage mode for this character.
    /// This value can be manually toggled by the combat UI, or auto-resolved to a rules default on reset.
    /// </summary>
    public AttackDamageMode CurrentAttackDamageMode { get; private set; } = AttackDamageMode.Lethal;
    private bool _attackDamageModeManuallySetThisRound;
    private struct DamageModeAttackProfile
    {
        public bool DealNonlethalDamage;
        public int AttackPenalty;
        public string PenaltySource;
    }

    // Feint windows keyed by this attacker.
    // A successful feint lets this attacker deny DEX-to-AC against that target on the next melee attack,
    // usable before the end of this attacker's next turn.
    private sealed class FeintWindow
    {
        public CharacterController Target;
        public int ExpiresAfterTurnStartCount;
    }

    private readonly List<FeintWindow> _activeFeintWindows = new List<FeintWindow>();
    private int _turnsStartedCount;

    // Tracks whether ability-score-zero logic applied these specific conditions,
    // so recovery can safely remove only what this system added.
    private bool _abilityZeroAppliedHelpless;
    private bool _abilityZeroAppliedUnconscious;

    [Header("Disease & Poison")]
    [SerializeField] private List<ActiveDisease> _activeDiseases = new List<ActiveDisease>();
    [SerializeField] private List<ActivePoison> _activePoisons = new List<ActivePoison>();

    public List<ActiveDisease> ActiveDiseases => _activeDiseases;
    public List<ActivePoison> ActivePoisons => _activePoisons;

    [Header("Innate Monster Abilities")]
    /// <summary>
    /// True once the bombardier beetle's acid spray was used this round: MM p.285 allows it once per round, read as
    /// between the start of one of its turns and the start of the next (decision D1 of batch 12). Cleared by
    /// <see cref="ClearOncePerRoundFlags"/> at its turn start and on slot reuse (CRE-046).
    /// </summary>
    [SerializeField] private bool _acidSprayUsedThisRound;
    [SerializeField] private int _regenerationAmountPerRound;
    [SerializeField] private DamageBypassTag _regenerationSuppressedBy = DamageBypassTag.None;
    [SerializeField] private int _regenerationSuppressedRoundsRemaining;
    [SerializeField] private int _swarmBleedingPerRound;

    // Incorporeal creatures (Shadow, Wraith, Allip)
    [SerializeField] private bool _isIncorporeal;
    public bool IsIncorporeal => _isIncorporeal;

    // Breath weapon tracking
    [SerializeField] private BreathWeaponDefinition _breathWeapon;
    [SerializeField] private int _breathWeaponCooldownRemaining;
    /// <summary>Breath weapon uses spent by this creature (the gorgon's five per day, MM p.138); cleared with the breath.</summary>
    [SerializeField] private int _breathWeaponUsesToday;
    public bool HasBreathWeapon => _breathWeapon != null;
    /// <summary>True when the generic breath path (NPCExecuteBreathWeaponForAI, DragonAIProfile) may use the breath now. A
    /// once-per-round use (the bombardier beetle's acid spray, MM p.285) is never ready here: its own routine
    /// (GameManager.TryNPCUseBombardierAcidSpray) reads <see cref="AcidSprayUsedThisRound"/> instead (CRE-040).</summary>
    public bool IsBreathWeaponReady => _breathWeapon != null && !_breathWeapon.OncePerRound && _breathWeaponCooldownRemaining <= 0
        && (_breathWeapon.UsesPerDay <= 0 || _breathWeaponUsesToday < _breathWeapon.UsesPerDay);
    /// <summary>Rounds until the breath weapon recharges (0: ready, if a daily limit leaves a use).</summary>
    public int BreathWeaponCooldownRemaining => _breathWeaponCooldownRemaining;
    /// <summary>Breath weapon uses spent so far (against <see cref="BreathWeaponDefinition.UsesPerDay"/>).</summary>
    public int BreathWeaponUsesToday => _breathWeaponUsesToday;
    /// <summary>Read-only access to breath weapon definition (does NOT consume/trigger cooldown).</summary>
    public BreathWeaponDefinition GetBreathWeaponDefinition() => _breathWeapon;

    // Secondary breath weapon (metallic dragons)
    [SerializeField] private SecondaryBreathWeaponDefinition _secondaryBreathWeapon;
    public bool HasSecondaryBreathWeapon => _secondaryBreathWeapon != null;
    /// <summary>
    /// A dragon with more than one breath weapon still breathes only once every 1d4 rounds (MM p.69): the secondary breath
    /// shares the primary breath's recharge and has no daily count (CRE-073).
    /// </summary>
    public bool IsSecondaryBreathWeaponReady => _secondaryBreathWeapon != null && _breathWeaponCooldownRemaining <= 0;
    public SecondaryBreathWeaponDefinition GetSecondaryBreathWeaponDefinition() => _secondaryBreathWeapon;

    // Frightful Presence (Young Adult+ dragons)
    [SerializeField] private FrightfulPresenceDefinition _frightfulPresence;
    [SerializeField] private bool _hasFrightfulPresenceTriggered; // Triggers once per combat
    public bool HasFrightfulPresence => _frightfulPresence != null;
    public FrightfulPresenceDefinition GetFrightfulPresenceDefinition() => _frightfulPresence;
    public bool HasFrightfulPresenceTriggered => _hasFrightfulPresenceTriggered;
    public void MarkFrightfulPresenceTriggered() { _hasFrightfulPresenceTriggered = true; }

    // Engulf tracking
    [SerializeField] private EngulfDefinition _engulf;
    public bool HasEngulf => _engulf != null;
    public EngulfDefinition GetEngulfDefinition() => _engulf;

    // Stench aura
    [SerializeField] private int _stenchAuraDC;
    [SerializeField] private int _stenchAuraRange;
    public bool HasStenchAura => _stenchAuraDC > 0;

    // Supernatural aura (babble, moan)
    [SerializeField] private AuraAbilityDefinition _auraAbility;
    public bool HasAuraAbility => _auraAbility != null;
    public AuraAbilityDefinition GetAuraAbilityDefinition() => _auraAbility;

    // Ranged special attack (Spittle, Web, Acid Spray)
    [SerializeField] private RangedSpecialAttackDefinition _rangedSpecialAttack;
    [SerializeField] private int _rangedSpecialAttackCooldownRounds;
    public bool HasRangedSpecialAttack => _rangedSpecialAttack != null;
    public bool IsRangedSpecialAttackReady => _rangedSpecialAttack != null && _rangedSpecialAttackCooldownRounds <= 0;
    public RangedSpecialAttackDefinition GetRangedSpecialAttackDefinition() => _rangedSpecialAttack;

    // Blood drain (CON drain while grappling)
    [SerializeField] private BloodDrainDefinition _bloodDrain;
    public bool HasBloodDrain => _bloodDrain != null;
    public BloodDrainDefinition GetBloodDrainDefinition() => _bloodDrain;

    // Terrain manipulation (Ground Manipulation, Caltrops)
    [SerializeField] private TerrainManipulationDefinition _terrainManipulation;
    [SerializeField] private int _terrainManipulationDurationRemaining;
    public bool HasTerrainManipulation => _terrainManipulation != null;
    public TerrainManipulationDefinition GetTerrainManipulationDefinition() => _terrainManipulation;

    /// <summary>True when the once-per-round acid spray was already used this round (MM p.285, D1).</summary>
    public bool AcidSprayUsedThisRound => _acidSprayUsedThisRound;
    public bool HasBombardierAcidSprayReady => !_acidSprayUsedThisRound;
    public bool HasRegeneration => _regenerationAmountPerRound > 0;

    // Tracks attackers that currently have an active feint window against this defender.
    // Used only for visual/status indication on the defender token.
    private readonly HashSet<CharacterController> _incomingFeintSources = new HashSet<CharacterController>();

    // Tracks each enemy's last known grid square when this character had line of sight.
    // Used for total-concealment targeting behavior.
    private readonly Dictionary<CharacterController, Vector2Int> _lastKnownTargetPositions = new Dictionary<CharacterController, Vector2Int>();

    // Pin state tracking (D&D 3.5e):
    // - A character can be pinning one opponent.
    // - A character can be pinned by one opponent.
    private bool _isPinningOpponent;
    private CharacterController _pinnedOpponent;
    private CharacterController _pinnedBy;

    private sealed class GrappleLink
    {
        public CharacterController Controller;
        public CharacterController Defender;
        public CharacterController PinnedCharacter;
        public CharacterController PinMaintainer;
        // PHB p.156: a pin holds the opponent for 1 round. The round is up when the maintainer's turn-start
        // count reaches this value (its next turn); 0 = no maintainer. See IsPinRenewalDue (CMB-120).
        public int PinRoundEndsAtMaintainerTurnStart;

        // Improved Grab (MM p.310). The natural attack that established the hold, when Improved Grab started this
        // grapple (null otherwise), and the controller's turn-start count at that moment: in later rounds ("successive
        // rounds") each successful Damage grapple check of the controller deals that attack's damage (CMB-148).
        public NaturalAttackDefinition EstablishingAttack;
        public int EstablishedAtControllerTurnStart;
        // The controller chose to hold with the body part that made the attack: -20 on its grapple checks, and it is
        // not considered grappled (no Grappled condition, keeps its Dex bonus, threatens, may attack others; MM p.310,
        // owner ruling 2026-10-11, CMB-148, CMB-149). The defender is grappled as usual.
        public bool ControllerHoldsWithBodyPart;

        public CharacterController GetOpponent(CharacterController actor)
        {
            if (actor == Controller) return Defender;
            if (actor == Defender) return Controller;
            return null;
        }

        public bool Contains(CharacterController actor)
        {
            return actor == Controller || actor == Defender;
        }
    }

    private static readonly Dictionary<CharacterController, GrappleLink> _grappleLinksByCharacter = new Dictionary<CharacterController, GrappleLink>();

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only, read-only (scenario harness clean check): one name per entry of the static grapple
    /// link table ("&lt;destroyed&gt;" for a key whose object is gone). Unlike IsGrappling it never ends a link.
    /// </summary>
    internal static List<string> Harness_GrappleLinkHolders()
    {
        var names = new List<string>();
        foreach (KeyValuePair<CharacterController, GrappleLink> kv in _grappleLinksByCharacter)
            names.Add(kv.Key != null ? kv.Key.name : "<destroyed>");
        return names;
    }
#endif

    /// <summary>Set Power Attack value, clamped to 0..BAB.</summary>
    public void SetPowerAttack(int value)
    {
        if (Stats == null) { PowerAttackValue = 0; return; }
        PowerAttackValue = Mathf.Clamp(value, 0, Mathf.Max(1, Stats.BaseAttackBonus));
    }

    /// <summary>Toggle Rapid Shot on/off.</summary>
    public void SetRapidShot(bool enabled)
    {
        RapidShotEnabled = enabled;
        Debug.Log($"[RapidShot] {(Stats != null ? Stats.CharacterName : "unknown")}: SetRapidShot({enabled}) → RapidShotEnabled = {RapidShotEnabled}");
    }

    /// <summary>Toggle Fighting Defensively stance for this turn.</summary>
    public void SetFightingDefensively(bool enabled)
    {
        IsFightingDefensively = enabled;
        if (Stats != null)
        {
            Debug.Log($"[Defensive] {Stats.CharacterName}: Fighting Defensively {(enabled ? "ON" : "OFF")}");
        }
    }

    public void SetAttackDamageMode(AttackDamageMode mode)
    {
        CurrentAttackDamageMode = mode;
        _attackDamageModeManuallySetThisRound = true;
    }

    public void ToggleAttackDamageMode()
    {
        CurrentAttackDamageMode = CurrentAttackDamageMode == AttackDamageMode.Lethal
            ? AttackDamageMode.Nonlethal
            : AttackDamageMode.Lethal;
        _attackDamageModeManuallySetThisRound = true;
    }

    public void ResetAttackDamageMode()
    {
        _attackDamageModeManuallySetThisRound = false;
        CurrentAttackDamageMode = GetDefaultAttackDamageModeForWeapon(GetEquippedMainWeapon());
    }

    public static bool IsIterativeGrappleAttackAction(GrappleActionType actionType)
    {
        switch (actionType)
        {
            case GrappleActionType.DamageOpponent:
            case GrappleActionType.AttackWithLightWeapon:
            case GrappleActionType.AttackUnarmed:
            case GrappleActionType.PinOpponent:
            case GrappleActionType.UseOpponentWeapon:
            case GrappleActionType.OpposedGrappleEscape:
                return true;
            default:
                return false;
        }
    }

    public int GetNumberOfAttacks()
    {
        return EnsureCombatStats().GetNumberOfAttacks();
    }

    public List<int> GetAttackBonuses()
    {
        return EnsureCombatStats().GetAttackBonuses();
    }

    public int GetIterativeAttackCount()
    {
        return EnsureCombatStats().GetIterativeAttackCount();
    }

    public int GetIterativeAttackBAB(int attackIndex)
    {
        return EnsureCombatStats().GetIterativeAttackBAB(attackIndex);
    }

    public int GetOffHandAttackCount()
    {
        return EnsureCombatStats().GetOffHandAttackCount();
    }

    public int GetOffHandAttackBAB(int attackIndex)
    {
        return EnsureCombatStats().GetOffHandAttackBAB(attackIndex);
    }

    // ========== ATTACK-SEQUENCE EXECUTOR (PHB p.143, Table 8-2 note 7) ==========
    // One per-creature attack sequence shared by the PC weapon flow, PC maneuvers, grapple
    // sub-actions and the AI. State lives in ProgressiveAttackPool; see AttackPool for the rules.

    /// <summary>
    /// Attack steps this turn. A creature fighting with its innate natural attacks (no main weapon)
    /// has one step per natural attack, plus Haste's extra natural attack (CMB-106), whichever kind
    /// of step comes first, so natural attacks and maneuvers that replace one share a single cap.
    /// Otherwise the Haste-aware iterative count. Either way Haste adds exactly one step.
    /// </summary>
    public int GetMainHandAttackBudget(AttackStepKind kind)
    {
        if (kind == AttackStepKind.NaturalSequence || ShouldUseInnateNaturalAttackProfile(GetEquippedMainWeapon()))
            return Mathf.Max(1, GetNaturalAttackStepBudget());

        return Mathf.Max(1, GetIterativeAttackCount());
    }

    // ----- Flurry of blows (PHB p.40-41; CHR-003) -----
    // One rule for PC and NPC monks. The flurry is declared before the first attack and makes the turn a full attack
    // (TryBeginFlurryOfBlows spends the full-round action). Its steps then run through this attack sequence like any
    // full attack: CharacterCombatStats.GetAttackBonuses / GetIterativeAttackBAB and FullAttack read the flurry steps
    // (CharacterStats.GetFlurryOfBlowsBaseAttackBonuses: the iterative steps plus the extra attack at the highest
    // bonus, two from 11th level; Haste adds its attack after them), BuildAttackBonus adds the flurry penalty to every
    // attack until the monk's next turn (her attacks of opportunity included), and BuildWeaponDamageBonus adds the full
    // Strength bonus to a flurry attack's damage. The PC Flurry button declares it and starts the PC progressive attack
    // sequence (one ResolveAttackSequenceStep per target click); the AI declares it before its melee sequence when
    // AIProfile.ShouldUseFlurryOfBlows says so. On both sides a maneuver may replace any flurry step, the extra attack
    // included, at that step's bonus (TryCommitManeuverSubstituteStep; PHB p.40 lets the flurry's attacks be any mix of
    // its attack forms and says nothing against a trip or disarm in place of one, PHB p.143 lets a maneuver replace a
    // melee attack in a full attack: delegated RAW reading 2026-10-14). Once she is grappling, the flurry's extra attack
    // ends (CharacterCombatStats.GetAttackBonuses) but its penalty stays on her attack rolls.

    /// <summary>True while this creature's flurry of blows lasts (from its declaration to the start of its next turn).</summary>
    public bool IsUsingFlurryOfBlows => ProgressiveAttackPool.IsFlurryOfBlows;

    /// <summary>
    /// The flurry penalty on an attack roll this creature makes while its flurry lasts (PHB p.40: every attack that round
    /// and its attacks of opportunity before its next turn; CHR-003), for the attack rolls not built by
    /// <see cref="BuildAttackBonus"/>: a trip or grapple touch attack and the disarm and sunder opposed attack rolls.
    /// </summary>
    public int FlurryAttackRollPenalty => IsUsingFlurryOfBlows && Stats != null ? Stats.FlurryOfBlowsAttackPenalty : 0;

    // Set while a flurry attack (not an attack of opportunity) resolves, for its Strength rule (PHB p.41).
    private bool _resolvingFlurryOfBlowsAttack;

    /// <summary>
    /// A weapon a flurry may use (PHB p.40): an unarmed strike (null) or a special monk weapon
    /// (<see cref="CharacterStats.IsSpecialMonkWeapon"/>), in melee. A thrown shuriken flurry is not modelled.
    /// </summary>
    public bool IsFlurryOfBlowsWeapon(ItemData weapon)
    {
        if (weapon == null)
            return !ShouldUseInnateNaturalAttackProfile(null);

        return CharacterStats.IsSpecialMonkWeapon(weapon) && weapon.WeaponCat != WeaponCategory.Ranged;
    }

    /// <summary>
    /// Whether this creature may declare a flurry of blows now (PHB p.40): a monk who is not wearing armor, carrying a
    /// shield or a medium or heavy load, attacking with an unarmed strike or a special monk weapon, before any attack
    /// this turn, with its full-round action available (a full attack, PHB p.143; not while slowed, PHB p.280, or
    /// grappling, PHB p.156). Declared before the first attack, and never while grappling: delegated RAW readings
    /// 2026-10-14 (RULES_COVERAGE 4.1).
    /// </summary>
    public bool CanUseFlurryOfBlows(out string reason)
    {
        reason = string.Empty;
        if (Stats == null || !Stats.IsMonk)
        {
            reason = "only a monk can strike with a flurry of blows";
            return false;
        }

        string fetter = Stats.GetMonkFetterReason();
        if (fetter != null)
        {
            reason = $"a monk {fetter} loses her flurry of blows (PHB p.40)";
            return false;
        }

        ItemData weapon = GetEquippedMainWeapon();
        if (!IsFlurryOfBlowsWeapon(weapon))
        {
            reason = weapon != null
                ? $"a flurry uses only unarmed strikes or special monk weapons, not {weapon.Name} (PHB p.40)"
                : "a flurry uses unarmed strikes or special monk weapons, not natural attacks";
            return false;
        }

        if (IsGrappling())
        {
            reason = "a grappling creature attacks only through grapple actions (PHB p.156)";
            return false;
        }

        if (ProgressiveAttackPool.HasStartedAttacking)
        {
            reason = "a flurry is declared before the first attack of the turn (PHB p.40)";
            return false;
        }

        if (Actions == null || !Actions.HasFullRoundAction)
        {
            reason = "a flurry of blows is a full attack and needs the full-round action (PHB p.40, p.143)";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Declare a flurry of blows (PHB p.40): spends the full-round action and makes this turn's attack sequence a flurry
    /// (<see cref="AttackPool.MarkFlurryOfBlows"/>). True when the flurry is (already) declared.
    /// </summary>
    public bool TryBeginFlurryOfBlows(out string reason)
    {
        reason = string.Empty;
        if (IsUsingFlurryOfBlows)
            return true;

        if (!CanUseFlurryOfBlows(out reason))
            return false;

        Actions.UseFullRoundAction();
        ProgressiveAttackPool.MarkFlurryOfBlows();
        Debug.Log($"[Monk] {Stats.CharacterName} declares a flurry of blows: steps {string.Join("/", Stats.GetFlurryOfBlowsModifiedBaseAttackBonuses())} (penalty {Stats.FlurryOfBlowsAttackPenalty})");
        return true;
    }

    /// <summary>
    /// The attack bonus of each attack a flurry would make now (each flurry step with the shared per-attack terms and the
    /// flurry penalty), for button labels and the AI. Haste's extra attack is included at the end.
    /// </summary>
    public int[] GetFlurryOfBlowsAttackBonusPreview(CharacterController target = null)
    {
        if (Stats == null || !Stats.IsMonk)
            return new int[0];

        ItemData weapon = GetEquippedMainWeapon();
        AttackBonusBreakdown terms = BuildAttackBonus(target, weapon, false, null, false, 0, 0, IsWeaponTwoHanded(weapon));
        terms.FlurryPenalty = Stats.FlurryOfBlowsAttackPenalty;
        int[] steps = Stats.GetFlurryOfBlowsBaseAttackBonuses();
        bool haste = HasActiveHasteEffect && ActiveHasteEffect.GrantsExtraAttack && steps.Length > 0;
        var bonuses = new int[steps.Length + (haste ? 1 : 0)];
        for (int i = 0; i < steps.Length; i++)
            bonuses[i] = steps[i] + terms.Total;
        if (haste)
            bonuses[steps.Length] = steps[0] + terms.Total;
        return bonuses;
    }

    // ----- Haste's extra attack with a natural weapon (PHB p.239; owner decision 2026-10-07, CMB-106) -----
    // PHB p.239: on a full attack a hasted creature makes one extra attack at its full bonus. The owner
    // ruled that this covers natural weapons: a hasted creature fighting with its innate natural attacks
    // makes ONE extra attack with ONE of its natural weapons, at that natural attack's normal bonus
    // (primary full, secondary -5 or -2 with Multiattack, MM p.312), and the attacker picks which one.
    // It is one more step of the natural sequence, after the natural attacks (step index = the
    // natural-attack count), so it needs the full attack like any second step (PHB p.143). A creature
    // with a main weapon takes the iterative Haste step instead (CharacterCombatStats.GetIterativeAttackCount),
    // never both. PCs pick the natural attack through the natural-attack buttons (a used attack is offered
    // again while the Haste attack is unused) or, on the Full Attack button and a pounce, through
    // GameManager.PromptHasteNaturalAttackChoice (CMB-124); the AI picks with AIProfile.ChooseHasteNaturalAttackIndex.

    /// <summary>True while Haste grants its extra attack (PHB p.239).</summary>
    public bool HasHasteExtraAttack => HasActiveHasteEffect && ActiveHasteEffect.GrantsExtraAttack;

    /// <summary>True when Haste's extra attack would be a natural attack: hasted, no main weapon, natural attacks (CMB-106).</summary>
    public bool HasHasteExtraNaturalAttack()
        => HasHasteExtraAttack && UsesInnateNaturalAttackSequence() && Stats != null && Stats.GetTotalNaturalAttackCount() > 0;

    /// <summary>Natural-attack steps this turn: one per natural attack, plus one for Haste (CMB-106).</summary>
    public int GetNaturalAttackStepBudget()
    {
        int naturalAttacks = Stats != null ? Stats.GetTotalNaturalAttackCount() : 0;
        return naturalAttacks + (HasHasteExtraNaturalAttack() ? 1 : 0);
    }

    /// <summary>True when natural-sequence step <paramref name="stepIndex"/> is Haste's extra natural attack.</summary>
    public bool IsHasteExtraNaturalStep(int stepIndex)
        => HasHasteExtraNaturalAttack() && stepIndex >= Stats.GetTotalNaturalAttackCount();

    /// <summary>Step index of Haste's extra natural attack: after the natural attacks.</summary>
    public int GetHasteExtraNaturalStepIndex() => Stats != null ? Stats.GetTotalNaturalAttackCount() : 0;

    /// <summary>Haste's extra natural attack is granted and not yet made (or given up for a maneuver) this turn.</summary>
    public bool CanUseHasteExtraNaturalAttack()
        => HasHasteExtraNaturalAttack() && !ProgressiveAttackPool.HasteExtraNaturalAttackUsed;

    /// <summary>Record that Haste's extra natural attack was made or given up this turn.</summary>
    public void MarkHasteExtraNaturalAttackUsed() => ProgressiveAttackPool.MarkHasteExtraNaturalAttackUsed();

    /// <summary>
    /// The natural attack Haste's extra attack uses when nobody chose one (the PC iterative Attack
    /// button, a cancelled PC Haste chooser on the Full Attack button or a pounce (CMB-124), a
    /// maneuver given up in its place): the highest attack bonus, then the
    /// higher average damage, then the first in the sequence. The AI's own choice (with riders) is
    /// AIProfile.ChooseHasteNaturalAttackIndex. Returns a natural-sequence index, or -1 with none.
    /// </summary>
    public int GetDefaultHasteNaturalAttackIndex()
    {
        if (Stats == null)
            return -1;

        int count = Stats.GetTotalNaturalAttackCount();
        int best = -1;
        int bestBonus = int.MinValue;
        float bestDamage = float.MinValue;
        for (int i = 0; i < count; i++)
        {
            NaturalAttackDefinition natural = Stats.GetNaturalAttackAtSequenceIndex(i);
            if (natural == null)
                continue;

            int bonus = Stats.GetNaturalAttackBonus(natural);
            float damage = GetNaturalAttackAverageDamage(natural);
            if (bonus > bestBonus || (bonus == bestBonus && damage > bestDamage))
            {
                best = i;
                bestBonus = bonus;
                bestDamage = damage;
            }
        }

        return best;
    }

    /// <summary>Average damage of one hit with this natural attack (scaled dice, its Strength share and flat bonus).</summary>
    public float GetNaturalAttackAverageDamage(NaturalAttackDefinition natural)
    {
        if (Stats == null || natural == null)
            return 0f;

        Stats.GetScaledNaturalAttackDamage(natural, out int damageCount, out int damageDice);
        return damageCount * (damageDice + 1) * 0.5f + Stats.GetNaturalAttackDamageBonus(natural) + natural.BonusDamage;
    }

    /// <summary>
    /// The natural-sequence index a natural step resolves: the step itself for steps before the Haste
    /// step; for the Haste step <paramref name="hasteNaturalAttackIndex"/> when it names a natural
    /// attack, else <see cref="GetDefaultHasteNaturalAttackIndex"/>.
    /// </summary>
    public int ResolveNaturalAttackIndexForStep(int stepIndex, int hasteNaturalAttackIndex = -1)
    {
        if (!IsHasteExtraNaturalStep(stepIndex))
            return stepIndex;

        int count = Stats.GetTotalNaturalAttackCount();
        return hasteNaturalAttackIndex >= 0 && hasteNaturalAttackIndex < count
            ? hasteNaturalAttackIndex
            : GetDefaultHasteNaturalAttackIndex();
    }

    /// <summary>
    /// Highest step count usable by this kind of step. A MainHand step (a weapon or unarmed swing, a
    /// weapon user's maneuver, or any creature's grapple action once grappling other than a natural-weapon
    /// creature's grapple natural attack) uses an iterative BAB, so it can never go past the iterative ladder.
    /// A natural-attack creature's maneuver or grapple natural attack is a NaturalSequence step (CMB-102,
    /// CMB-127) and is capped by the natural-attack count instead; a grapple natural attack is also capped by the
    /// iterative ladder (TryCommitGrappleNaturalAttackStep; PHB p.156, owner interpretation 2026-10-11, CMB-146).
    /// </summary>
    private int GetAttackStepLimit(AttackStepKind kind)
    {
        AttackPool pool = ProgressiveAttackPool;
        int limit = pool.MainHandBudget > 0 ? pool.MainHandBudget : GetMainHandAttackBudget(kind);
        if (kind == AttackStepKind.MainHand)
            limit = Mathf.Min(limit, Mathf.Max(1, GetIterativeAttackCount()));
        return limit;
    }

    /// <summary>
    /// True while this turn can still become (or already is) a full attack. A 5-foot step does not
    /// use the move action, so it does not block a full attack (PHB p.143-144).
    /// </summary>
    public bool CanStillReachFullAttack()
    {
        if (ProgressiveAttackPool.IsFullAttack)
            return true;

        return Actions != null
            && Actions.HasMoveAction
            && !Actions.SingleActionOnly;
    }

    /// <summary>Can the action for the next attack be paid (or is it already paid)?</summary>
    public bool CanPayForNextAttack(out string reason)
    {
        reason = string.Empty;
        AttackPool pool = ProgressiveAttackPool;

        if (pool.PendingStepPaid)
            return true;

        if (Actions == null)
        {
            reason = "No action economy available.";
            return false;
        }

        switch (pool.Mode)
        {
            case ProgressiveAttackMode.None:
                if (Actions.HasStandardAction)
                    return true;
                reason = "no standard action";
                return false;

            case ProgressiveAttackMode.StandardAttackCommitted:
                if (Actions.SingleActionOnly)
                {
                    reason = "limited to a single action this turn (disabled, staggered or slowed), so a second attack is not allowed (PHB p.145, p.280)";
                    return false;
                }

                if (!Actions.HasMoveAction)
                {
                    reason = "a second attack makes this a full attack and needs the unspent move action (PHB p.143)";
                    return false;
                }

                return true;

            default:
                return true;
        }
    }

    /// <summary>Can one more attack step of this kind be committed this turn?</summary>
    public bool CanCommitAttack(AttackStepKind kind, out string reason)
    {
        if (!CanPayForNextAttack(out reason))
        {
            if (string.IsNullOrEmpty(reason))
                reason = "No attacks remaining this turn.";
            return false;
        }

        if (kind == AttackStepKind.MainHand || kind == AttackStepKind.NaturalSequence)
        {
            if (ProgressiveAttackPool.MainHandStepsUsed >= GetAttackStepLimit(kind))
            {
                reason = "No attacks remaining this turn.";
                return false;
            }
        }
        else if (kind == AttackStepKind.OffHand && IsUsingFlurryOfBlows)
        {
            // A flurry's attacks are its own steps, with unarmed strikes and monk weapons interchangeably; there is no
            // off-hand attack in it: every flurry attack adds the full Strength bonus, never x1/2 (PHB p.40-41;
            // delegated RAW reading 2026-10-14).
            reason = "a flurry of blows has no off-hand attacks (PHB p.40)";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool TryPayForNextAttack(out string reason)
    {
        return TryPayForNextAttack(out reason, out _);
    }

    /// <summary>
    /// Spend the action for the next attack: the standard action for the first, the move action
    /// for the second (the turn becomes a full attack), nothing afterwards. Paying twice before the
    /// attack is registered spends nothing more.
    /// </summary>
    public bool TryPayForNextAttack(out string reason, out bool enteredFullAttack)
    {
        enteredFullAttack = false;
        reason = string.Empty;
        AttackPool pool = ProgressiveAttackPool;

        if (pool.PendingStepPaid)
            return true;

        if (Actions == null)
        {
            reason = "No action economy available.";
            return false;
        }

        switch (pool.Mode)
        {
            case ProgressiveAttackMode.None:
                if (!CommitStandardAction())
                {
                    reason = "no standard action";
                    return false;
                }

                pool.MarkPaid(ProgressiveAttackMode.StandardAttackCommitted);
                return true;

            case ProgressiveAttackMode.StandardAttackCommitted:
                if (!CanPayForNextAttack(out reason))
                    return false;

                Actions.UseMoveAction();
                pool.MarkPaid(ProgressiveAttackMode.FullAttackCommitted);
                enteredFullAttack = true;
                return true;

            default:
                pool.MarkPaid(ProgressiveAttackMode.FullAttackCommitted);
                return true;
        }
    }

    /// <summary>Record an attack that was paid for. Returns the main-hand step index, or -1 for an off-hand attack.</summary>
    public int RegisterAttackMade(AttackStepKind kind)
    {
        if (kind == AttackStepKind.MainHand || kind == AttackStepKind.NaturalSequence)
            ProgressiveAttackPool.EnsureMainHandBudget(GetMainHandAttackBudget(kind));

        return ProgressiveAttackPool.RegisterAttack(kind);
    }

    /// <summary>Check, pay for and record one attack step. Maneuvers and the AI use this one call.</summary>
    public bool TryCommitAttack(AttackStepKind kind, out int mainHandStepIndex, out string reason)
    {
        mainHandStepIndex = -1;

        if (!CanCommitAttack(kind, out reason))
            return false;

        if (!TryPayForNextAttack(out reason))
            return false;

        mainHandStepIndex = RegisterAttackMade(kind);
        return true;
    }

    /// <summary>Main-hand steps still available this turn, given the actions left.</summary>
    public int GetRemainingMainHandAttackSteps(AttackStepKind kind = AttackStepKind.MainHand)
    {
        if (!CanPayForNextAttack(out _))
            return 0;

        AttackPool pool = ProgressiveAttackPool;
        int left = Mathf.Max(0, GetAttackStepLimit(kind) - pool.MainHandStepsUsed);

        if (!CanStillReachFullAttack())
        {
            bool nextIsPaidOrFirst = pool.Mode == ProgressiveAttackMode.None || pool.PendingStepPaid;
            left = Mathf.Min(left, nextIsPaidOrFirst ? 1 : 0);
        }

        return left;
    }

    public int GetMainHandAttackStepBAB(int stepIndex) => GetIterativeAttackBAB(stepIndex);

    // ----- Maneuvers that replace an attack (trip, disarm, sunder, grapple; CMB-102) -----
    // One rule for PCs and NPCs: the maneuver takes the place of one step of this creature's own
    // sequence and rolls at that step's bonus. A weapon or unarmed fighter gives up an iterative
    // attack (its iterative BAB, PHB p.141 Table 8-2 note 7, p.143). A creature fighting with its
    // innate natural attacks gives up one natural attack, any one of them, and rolls at that natural
    // attack's BAB: full BAB for a primary attack, -5 for a secondary one, -2 with Multiattack
    // (MM p.312, p.304; owner decision 2026-10-07). Its other natural attacks stay available.
    // Which natural attack is given up follows the sequence order (see TryCommitManeuverSubstituteStep).

    /// <summary>
    /// True when this creature fights with its innate natural attacks (no main weapon). The one
    /// predicate for "natural-attack creature": the PC and NPC melee flows, the maneuver substitute
    /// and the disarm rules (a natural-weapon attacker is armed, MM p.312) all read it.
    /// </summary>
    public bool UsesInnateNaturalAttackSequence() => ShouldUseInnateNaturalAttackProfile(GetEquippedMainWeapon());

    /// <summary>The step kind of this creature's melee attacks: a natural attack of its innate sequence, or an iterative step.</summary>
    public AttackStepKind GetMeleeAttackStepKind()
        => UsesInnateNaturalAttackSequence() ? AttackStepKind.NaturalSequence : AttackStepKind.MainHand;

    /// <summary>The step kind a maneuver that replaces an attack commits (see the rule above).</summary>
    public AttackStepKind GetManeuverSubstituteStepKind() => GetMeleeAttackStepKind();

    /// <summary>
    /// Sunder is a melee attack with a slashing or bludgeoning weapon (PHB p.158). The one legality check
    /// for PCs and NPCs (the PC Sunder button, <c>AIService.ShouldUseManeuver</c>, the NPC executor and
    /// <see cref="ResolveSunder"/>), made before any attack step is spent:
    /// - a manufactured weapon in hand may sunder (its damage type is not checked yet, CMB-139);
    /// - a creature fighting with its natural attacks sunders with the natural attack the sunder replaces,
    ///   which must deal slashing or bludgeoning damage (owner ruling 2026-10-08, CMB-102; MM p.312: a bite,
    ///   claw, talon, slam or tentacle can, a gore or sting cannot; <see cref="NaturalAttackDefinition.CanSunder"/>);
    /// - a creature with neither sunders with an unarmed strike, a bludgeoning light weapon (PHB p.116 Table 7-5),
    ///   and so does an NPC that lists "Unarmed Strike" as a natural attack (no shipped entry since CRE-042): owner interpretation 2026-10-11
    ///   (CMB-141); <see cref="ResolveSunder"/> gives it the light-weapon -4.
    /// <paramref name="naturalAttackIndex"/> names the natural attack given up; a negative value means the
    /// one at the current step, the order the NPC sequence resolves them in
    /// (<see cref="GetSunderNaturalAttackIndexForStep"/>).
    /// </summary>
    public bool CanSunderWithAttack(int naturalAttackIndex, out string reason)
    {
        string actorName = Stats != null ? Stats.CharacterName : name;
        if (GetEquippedMainWeapon() != null)
        {
            reason = string.Empty;
            return true;
        }

        if (!UsesInnateNaturalAttackSequence())
        {
            // An unarmed strike (owner interpretation 2026-10-11, CMB-141).
            reason = string.Empty;
            return true;
        }

        int index = naturalAttackIndex >= 0
            ? naturalAttackIndex
            : GetSunderNaturalAttackIndexForStep(ProgressiveAttackPool.MainHandStepsUsed);
        NaturalAttackDefinition natural = Stats.GetNaturalAttackAtSequenceIndex(index);
        if (natural == null)
        {
            reason = $"{actorName} has no natural attack left that can sunder.";
            return false;
        }

        if (!natural.CanSunder)
        {
            reason = $"{actorName}'s {natural.Name} deals no slashing or bludgeoning damage, so it cannot sunder (PHB p.158).";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// True when this creature fights with natural weapons (MM p.312: armed): it uses its innate natural
    /// sequence and at least one of its natural attacks is not an unarmed strike. An NPC that lists only
    /// unarmed strikes as natural attacks (no shipped entry since CRE-042) is unarmed, as a PC monk is: its disarm takes the
    /// light-weapon -4 and catches the weapon (PHB p.155), and its sunder takes the light-weapon -4 (PHB p.158;
    /// owner interpretation 2026-10-11, CMB-141).
    /// </summary>
    public bool FightsWithNaturalWeapons()
    {
        if (Stats == null || Stats.NaturalAttacks == null || !UsesInnateNaturalAttackSequence())
            return false;

        foreach (NaturalAttackDefinition natural in Stats.NaturalAttacks)
        {
            if (natural != null && !natural.IsUnarmedStrike)
                return true;
        }

        return false;
    }

    /// <summary>True when the natural attack at this place of the innate sequence deals slashing or bludgeoning damage (PHB p.158, CMB-102).</summary>
    public bool CanNaturalAttackSunder(int naturalAttackIndex)
    {
        NaturalAttackDefinition natural = Stats != null ? Stats.GetNaturalAttackAtSequenceIndex(naturalAttackIndex) : null;
        return natural != null && natural.CanSunder;
    }

    /// <summary>The first natural attack of the innate sequence that can sunder, or -1.</summary>
    public int GetFirstSunderCapableNaturalAttackIndex()
    {
        int count = Stats != null ? Stats.GetTotalNaturalAttackCount() : 0;
        for (int i = 0; i < count; i++)
        {
            if (CanNaturalAttackSunder(i))
                return i;
        }

        return -1;
    }

    /// <summary>
    /// The natural attack a sunder made at natural-sequence step <paramref name="stepIndex"/> is made with:
    /// the natural attack at that step; at Haste's extra natural attack (an attack of the attacker's choice,
    /// CMB-106) the default Haste natural attack when it can sunder, else the first one that can (-1 if none).
    /// </summary>
    public int GetSunderNaturalAttackIndexForStep(int stepIndex)
    {
        if (!IsHasteExtraNaturalStep(stepIndex))
            return stepIndex;

        int hasteIndex = GetDefaultHasteNaturalAttackIndex();
        return CanNaturalAttackSunder(hasteIndex) ? hasteIndex : GetFirstSunderCapableNaturalAttackIndex();
    }

    /// <summary>
    /// BAB of the natural attack at this place of the innate sequence, with its secondary-attack
    /// penalty (MM p.312; -2 with Multiattack): what a maneuver replacing that natural attack rolls at.
    /// An index past the natural attacks is Haste's extra natural attack (CMB-106), at the bonus of the
    /// natural attack it would use (<see cref="ResolveNaturalAttackIndexForStep"/>).
    /// </summary>
    public int GetNaturalAttackStepBAB(int naturalAttackIndex)
    {
        if (Stats == null)
            return 0;

        NaturalAttackDefinition natural = Stats.GetNaturalAttackAtSequenceIndex(ResolveNaturalAttackIndexForStep(naturalAttackIndex));
        return Stats.BaseAttackBonus + Stats.GetNaturalAttackSequencePenalty(natural);
    }

    /// <summary>
    /// BAB a maneuver rolls at when it replaces sequence step <paramref name="stepIndex"/>. For a
    /// natural-attack creature, <paramref name="naturalAttackIndex"/> names the natural attack given up;
    /// a negative value means the natural attack at the step itself (the NPC sequence order), and at
    /// the Haste step the default Haste natural attack (CMB-106).
    /// </summary>
    public int GetManeuverSubstituteBAB(int stepIndex, int naturalAttackIndex = -1)
    {
        if (GetManeuverSubstituteStepKind() == AttackStepKind.NaturalSequence)
            return GetNaturalAttackStepBAB(naturalAttackIndex >= 0 ? naturalAttackIndex : ResolveSubstituteNaturalAttackIndex(stepIndex, out _));

        return GetMainHandAttackStepBAB(stepIndex);
    }

    /// <summary>
    /// The natural attack a maneuver or grapple action gives up when the caller names none (the NPC paths), at
    /// natural-sequence step <paramref name="stepIndex"/>. Normally the natural attack at the step itself, the order
    /// the NPC sequence resolves them in; at the Haste step Haste's extra natural attack while it is unused
    /// (<paramref name="isHasteExtraAttack"/>, at the default Haste natural attack's bonus, CMB-106). When that natural
    /// attack is already used this turn, because a grapple attack action picked natural attacks out of sequence order
    /// (PHB p.156, CMB-127), the first unused natural attack in sequence order, else Haste's extra attack while unused.
    /// Returns a natural-sequence index (the step's own when nothing is left).
    /// </summary>
    public int ResolveSubstituteNaturalAttackIndex(int stepIndex, out bool isHasteExtraAttack)
    {
        isHasteExtraAttack = false;
        int count = Stats != null ? Stats.GetTotalNaturalAttackCount() : 0;
        AttackPool pool = ProgressiveAttackPool;

        if (IsHasteExtraNaturalStep(stepIndex))
        {
            if (CanUseHasteExtraNaturalAttack())
            {
                isHasteExtraAttack = true;
                return GetDefaultHasteNaturalAttackIndex();
            }
        }
        else if (stepIndex >= 0 && stepIndex < count && !pool.IsNaturalAttackUsed(stepIndex))
        {
            return stepIndex;
        }

        for (int i = 0; i < count; i++)
        {
            if (!pool.IsNaturalAttackUsed(i))
                return i;
        }

        if (CanUseHasteExtraNaturalAttack())
        {
            isHasteExtraAttack = true;
            return GetDefaultHasteNaturalAttackIndex();
        }

        return ResolveNaturalAttackIndexForStep(stepIndex);
    }

    /// <summary>
    /// Check, pay for and record the step a maneuver (or a grapple natural attack, CMB-127) replaces, and return
    /// the BAB it rolls at. Used by the PC maneuver wrapper, the grapple actions and the NPC maneuver executor alike.
    /// For a weapon or unarmed fighter the step is an iterative one at its iterative BAB. For a creature fighting
    /// with its natural attacks the step gives up one natural attack and returns that attack's BAB:
    /// <paramref name="naturalAttackIndex"/> when the caller names one (the PC wrapper's next unused natural attack,
    /// or the natural attack a grapple attack makes, the attacker's pick), with <paramref name="givesUpHasteExtraAttack"/>
    /// when it is Haste's extra attack with that natural weapon (CMB-106); a named natural attack that is out of range
    /// or already made or given up this turn (<see cref="AttackPool.IsNaturalAttackUsed"/>), or a Haste attack that
    /// is not available, is refused before the step is spent. With -1 the step gives up
    /// <see cref="ResolveSubstituteNaturalAttackIndex"/>: the natural attack at the step (the NPC sequence order), or
    /// the first unused one, else Haste's extra attack. The natural attack given up is marked used in the attack
    /// pool (or Haste's extra attack is), and recorded with the Haste flag as the last substitute
    /// (<see cref="AttackPool.LastSubstituteNaturalAttackIndex"/>, <see cref="AttackPool.LastSubstituteWasHasteExtraAttack"/>).
    /// </summary>
    public bool TryCommitManeuverSubstituteStep(int naturalAttackIndex, out int maneuverBab, out int stepIndex, out string reason,
        bool givesUpHasteExtraAttack = false)
    {
        maneuverBab = 0;
        stepIndex = -1;
        AttackStepKind kind = GetManeuverSubstituteStepKind();
        if (kind == AttackStepKind.NaturalSequence && naturalAttackIndex >= 0
            && !CanGiveUpNaturalAttack(naturalAttackIndex, givesUpHasteExtraAttack, out reason))
            return false;

        if (!TryCommitAttack(kind, out stepIndex, out reason))
            return false;

        if (kind != AttackStepKind.NaturalSequence)
        {
            maneuverBab = GetMainHandAttackStepBAB(stepIndex);
            ProgressiveAttackPool.RecordSubstituteNaturalAttack(-1);
            return true;
        }

        // The natural attack given up: the caller's, else the step's own (or the first unused one, CMB-127).
        bool givesUpHaste = givesUpHasteExtraAttack;
        int givenUpIndex = naturalAttackIndex;
        if (givenUpIndex < 0)
            givenUpIndex = ResolveSubstituteNaturalAttackIndex(stepIndex, out givesUpHaste);

        maneuverBab = GetNaturalAttackStepBAB(givenUpIndex);
        if (givesUpHaste)
            MarkHasteExtraNaturalAttackUsed();
        else
            ProgressiveAttackPool.MarkNaturalAttackUsed(givenUpIndex);
        // Which natural attack was given up, for the Improved Trip follow-up attack (PHB p.96, CMB-079) and a
        // grapple natural attack committed without a named attack (CMB-127).
        ProgressiveAttackPool.RecordSubstituteNaturalAttack(givenUpIndex, givesUpHaste);
        return true;
    }

    /// <summary>
    /// Whether a caller-named natural attack can still be given up (or made) this turn: in range, and not made or
    /// given up already (<see cref="AttackPool.IsNaturalAttackUsed"/>); for Haste's extra attack with it, while that
    /// attack is unused (<see cref="CanUseHasteExtraNaturalAttack"/>). Checked before any step is spent (CMB-127).
    /// </summary>
    public bool CanGiveUpNaturalAttack(int naturalAttackIndex, bool asHasteExtraAttack, out string reason)
    {
        reason = string.Empty;
        int count = Stats != null ? Stats.GetTotalNaturalAttackCount() : 0;
        if (naturalAttackIndex < 0 || naturalAttackIndex >= count)
        {
            reason = $"no natural attack #{naturalAttackIndex + 1}";
            return false;
        }

        if (asHasteExtraAttack)
        {
            if (CanUseHasteExtraNaturalAttack())
                return true;
            reason = "Haste's extra natural attack is not available this turn";
            return false;
        }

        if (ProgressiveAttackPool.IsNaturalAttackUsed(naturalAttackIndex))
        {
            NaturalAttackDefinition natural = Stats.GetNaturalAttackAtSequenceIndex(naturalAttackIndex);
            reason = $"the {(natural != null && !string.IsNullOrWhiteSpace(natural.Name) ? natural.Name : "natural attack")} (#{naturalAttackIndex + 1}) was already used this turn";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Resolve one already-committed attack step of this creature's sequence (PHB p.143). Shared by
    /// the PC iterative flow and the NPC melee sequence; the attack modifier comes from
    /// BuildAttackBonus inside Attack/FullAttack. A natural step is the stepIndex-th natural attack
    /// of the innate sequence; the step after the natural attacks is Haste's extra natural attack
    /// (CMB-106), made with natural attack <paramref name="hasteNaturalAttackIndex"/> (the attacker's
    /// choice; -1 for <see cref="GetDefaultHasteNaturalAttackIndex"/>) and marked used. Any other step
    /// is one Attack at that step's iterative BAB plus <paramref name="babAdjustment"/> (the PC
    /// dual-wield main-hand penalty). Returns null when nothing was resolved.
    /// </summary>
    public CombatResult ResolveAttackSequenceStep(
        CharacterController target,
        AttackStepKind kind,
        int stepIndex,
        bool isFlanking,
        int flankBonus,
        string partnerName,
        RangeInfo rangeInfo,
        ItemData weapon,
        int babAdjustment,
        out string stepLabel,
        int hasteNaturalAttackIndex = -1)
    {
        stepLabel = null;
        if (target == null || target.Stats == null)
            return null;

        if (kind == AttackStepKind.NaturalSequence)
        {
            bool hasteStep = IsHasteExtraNaturalStep(stepIndex);
            FullAttackResult naturalStep = FullAttack(target, isFlanking, flankBonus, partnerName, rangeInfo,
                startAttackIndex: stepIndex, maxAttacks: 1, hasteNaturalAttackIndex: hasteNaturalAttackIndex);
            if (naturalStep == null || naturalStep.Attacks == null || naturalStep.Attacks.Count == 0)
                return null;

            if (hasteStep)
                MarkHasteExtraNaturalAttackUsed();

            stepLabel = naturalStep.AttackLabels != null && naturalStep.AttackLabels.Count > 0
                ? naturalStep.AttackLabels[0]
                : $"Natural attack {stepIndex + 1}";
            return naturalStep.Attacks[0];
        }

        int stepBab = GetMainHandAttackStepBAB(stepIndex) + babAdjustment;
        if (!IsUsingFlurryOfBlows)
        {
            stepLabel = $"Attack {stepIndex + 1} (BAB {CharacterStats.FormatMod(stepBab)})";
            return Attack(target, isFlanking, flankBonus, partnerName, rangeInfo, stepBab, weapon);
        }

        // A flurry step (PHB p.40-41, CHR-003): the flurry's own BAB step (GetIterativeAttackBAB reads the flurry steps),
        // the flurry penalty from BuildAttackBonus, the full Strength bonus on damage, and only flurry weapons.
        if (!IsFlurryOfBlowsWeapon(weapon ?? GetEquippedMainWeapon()))
        {
            Debug.LogWarning($"[Monk] {Stats.CharacterName}: {(weapon ?? GetEquippedMainWeapon())?.Name ?? "this attack"} cannot be part of a flurry of blows (PHB p.40)");
            return null;
        }

        stepLabel = $"Flurry {stepIndex + 1} (BAB {CharacterStats.FormatMod(stepBab)})";
        _resolvingFlurryOfBlowsAttack = true;
        try
        {
            return Attack(target, isFlanking, flankBonus, partnerName, rangeInfo, stepBab, weapon);
        }
        finally
        {
            _resolvingFlurryOfBlowsAttack = false;
        }
    }

    public bool TryConsumeIterativeGrappleAttackAction(out int attackBonusUsed, out int attacksRemaining, out string reason)
        => TryConsumeIterativeMainHandStep(out attackBonusUsed, out attacksRemaining, out reason);

    public bool TryConsumeIterativeDisarmAttackAction(out int attackBonusUsed, out int attacksRemaining, out string reason)
        => TryConsumeIterativeMainHandStep(out attackBonusUsed, out attacksRemaining, out reason);

    private bool TryConsumeIterativeMainHandStep(out int attackBonusUsed, out int attacksRemaining, out string reason)
    {
        attackBonusUsed = 0;
        attacksRemaining = 0;

        if (!TryCommitAttack(AttackStepKind.MainHand, out int step, out reason))
            return false;

        attackBonusUsed = GetMainHandAttackStepBAB(step);
        attacksRemaining = GetRemainingMainHandAttackSteps();
        return true;
    }

    private bool ShouldUseInnateNaturalAttackProfile(ItemData weapon)
    {
        if (weapon != null || Stats == null)
            return false;

        return Stats.HasNaturalAttacks;
    }

    private AttackDamageMode GetDefaultAttackDamageModeForWeapon(ItemData weapon)
    {
        if (weapon != null)
            return AttackDamageMode.Lethal;

        if (ShouldUseInnateNaturalAttackProfile(weapon))
            return AttackDamageMode.Lethal;

        if (HasImprovedUnarmedStrikeForDamageMode() || HasGauntletEquipped())
            return AttackDamageMode.Lethal;

        return AttackDamageMode.Nonlethal;
    }

    private AttackDamageMode ResolveSelectedAttackDamageMode(ItemData weapon)
    {
        if (_attackDamageModeManuallySetThisRound)
            return CurrentAttackDamageMode;

        AttackDamageMode defaultMode = GetDefaultAttackDamageModeForWeapon(weapon);
        CurrentAttackDamageMode = defaultMode;
        return defaultMode;
    }

    /// <summary>
    /// Damage mode and its attack penalty for an attack with <paramref name="weapon"/> (null: unarmed, or the innate
    /// natural attacks of a natural-attack creature). One parameter only: UnarmedDamageModeTests reflects on it by
    /// name (TST-006); <see cref="ResolveDamageModeAttackProfileCore"/> has the forced unarmed strike.
    /// </summary>
    private DamageModeAttackProfile ResolveDamageModeAttackProfile(ItemData weapon)
        => ResolveDamageModeAttackProfileCore(weapon, unarmedStrike: false);

    /// <summary>
    /// <see cref="ResolveDamageModeAttackProfile"/> with <paramref name="unarmedStrike"/>, which forces an unarmed
    /// strike, also for a creature with natural attacks, and reads the selected mode without changing it: the
    /// creature's chosen mode when it set one this round, else lethal with Improved Unarmed Strike or a gauntlet,
    /// else nonlethal.
    /// </summary>
    private DamageModeAttackProfile ResolveDamageModeAttackProfileCore(ItemData weapon, bool unarmedStrike)
    {
        AttackDamageMode selectedMode = !unarmedStrike
            ? ResolveSelectedAttackDamageMode(weapon)
            : _attackDamageModeManuallySetThisRound
                ? CurrentAttackDamageMode
                : HasImprovedUnarmedStrikeForDamageMode() || HasGauntletEquipped() ? AttackDamageMode.Lethal : AttackDamageMode.Nonlethal;
        bool selectedNonlethal = selectedMode == AttackDamageMode.Nonlethal;
        bool isInnateNaturalAttack = !unarmedStrike && ShouldUseInnateNaturalAttackProfile(weapon);
        bool isUnarmedStrike = unarmedStrike || (weapon == null && !isInnateNaturalAttack);
        bool weaponIsInherentlyNonlethal = weapon != null && weapon.DealsNonlethalDamage;

        var profile = new DamageModeAttackProfile
        {
            DealNonlethalDamage = false,
            AttackPenalty = 0,
            PenaltySource = string.Empty
        };

        // A natural weapon deals lethal damage; like any melee weapon that deals lethal damage it may deal nonlethal
        // damage at -4 on the attack roll (PHB p.146; MM p.312 treats natural weapons as weapons; delegated RAW reading
        // 2026-10-14). Default lethal (GetDefaultAttackDamageModeForWeapon); the creature's chosen mode applies.
        if (isInnateNaturalAttack)
            return GetNaturalWeaponDamageModeProfile(selectedNonlethal);

        if (isUnarmedStrike)
        {
            profile.DealNonlethalDamage = selectedNonlethal;
            bool hasLethalUnarmedDefault = HasImprovedUnarmedStrikeForDamageMode() || HasGauntletEquipped();
            if (!selectedNonlethal && !hasLethalUnarmedDefault)
            {
                profile.AttackPenalty = -4;
                profile.PenaltySource = "Using lethal damage with unarmed strike";
            }

            return profile;
        }

        if (weaponIsInherentlyNonlethal)
        {
            profile.DealNonlethalDamage = selectedNonlethal;
            if (!selectedNonlethal)
            {
                profile.AttackPenalty = -4;
                profile.PenaltySource = $"Using lethal damage with {weapon.Name}";
            }

            return profile;
        }

        // Default weapon behavior is lethal.
        profile.DealNonlethalDamage = selectedNonlethal;
        if (selectedNonlethal)
        {
            profile.AttackPenalty = -4;
            profile.PenaltySource = $"Using nonlethal damage with {weapon.Name}";
        }

        return profile;
    }

    /// <summary>
    /// The damage mode of an attack roll with a natural weapon (PHB p.146: a lethal melee weapon deals nonlethal damage
    /// at -4 on the attack roll; delegated RAW reading 2026-10-14): lethal with no penalty, or nonlethal at -4.
    /// </summary>
    private static DamageModeAttackProfile GetNaturalWeaponDamageModeProfile(bool nonlethal)
    {
        return nonlethal
            ? new DamageModeAttackProfile { DealNonlethalDamage = true, AttackPenalty = -4, PenaltySource = "Using nonlethal damage with a natural weapon" }
            : new DamageModeAttackProfile { DealNonlethalDamage = false, AttackPenalty = 0, PenaltySource = string.Empty };
    }

    /// <summary>
    /// The damage mode of the rake's claw attacks (MM p.314), natural weapons like any other: the creature's chosen
    /// mode this round, lethal by default (<see cref="GetNaturalWeaponDamageModeProfile"/>).
    /// </summary>
    private DamageModeAttackProfile GetRakeDamageModeProfile()
        => GetNaturalWeaponDamageModeProfile(_attackDamageModeManuallySetThisRound && CurrentAttackDamageMode == AttackDamageMode.Nonlethal);

    /// <summary>
    /// Resolve base unarmed strike damage for this character.
    /// D&D 3.5 baseline is 1d3 at Medium and scales by size; a monk's Medium baseline is her Table 3-10 damage by monk
    /// level (CharacterStats.TryGetMonkUnarmedMediumDamage, 1d6 to 2d10), resized the same way (Table 3-11; CHR-003).
    /// </summary>
    public (int damageCount, int damageDice, int bonusDamage) GetUnarmedDamage()
    {
        int mediumBaseCount = 1;
        int mediumBaseDice = 3;

        if (Stats != null && Stats.TryGetMonkUnarmedMediumDamage(out int monkCount, out int monkDice))
        {
            mediumBaseCount = monkCount;
            mediumBaseDice = monkDice;
        }

        SizeCategory currentSize = Stats != null ? Stats.CurrentSizeCategory : SizeCategory.Medium;
        if (!WeaponDamageScaler.TryScaleDamageDice(mediumBaseCount, mediumBaseDice, SizeCategory.Medium, currentSize, out int scaledCount, out int scaledDice))
        {
            scaledCount = mediumBaseCount;
            scaledDice = mediumBaseDice;
        }

        return (scaledCount, scaledDice, 0);
    }

    /// <summary>
    /// The weapon's damage dice in this creature's hands (DMG Tables 2-2 and 2-3, p.28). Weapons are sized for their
    /// current wielder, so a size change (Enlarge or Reduce Person) resizes them, except a thrown weapon: an item that
    /// leaves an enlarged or reduced creature returns to its normal size, so a thrown weapon deals its normal damage
    /// (PHB p.227 and p.269); it is scaled to the creature's normal size (<see cref="CharacterStats.BaseSizeCategory"/>).
    /// A projectile deals damage by the size of its launcher, which stays resized, so pass false for it.
    /// </summary>
    public void GetScaledWeaponDamageDice(ItemData weapon, out int damageCount, out int damageDice, bool thrownAttack = false)
    {
        if (weapon == null)
        {
            damageCount = 1;
            damageDice = 3;
            return;
        }

        SizeCategory size = Stats == null ? SizeCategory.Medium
            : thrownAttack ? Stats.BaseSizeCategory
            : Stats.CurrentSizeCategory;
        weapon.GetScaledDamageDice(size, out damageCount, out damageDice);
    }

    /// <summary>True when the attack throws the weapon (it leaves the wielder's hand), not a melee or launcher attack.</summary>
    public static bool IsThrownWeaponAttack(ItemData weapon, RangeInfo rangeInfo)
    {
        return weapon != null && weapon.IsThrown && IsRangedWeaponAttack(weapon, rangeInfo);
    }

    /// <summary>
    /// Resolve base damage inputs and display label for the current attack source.
    /// </summary>
    private void ResolveBaseAttackDamageProfile(ItemData weapon, out int damageDice, out int damageCount, out int bonusDamage, out string attackLabel, bool thrownAttack = false)
    {
        if (weapon != null)
        {
            GetScaledWeaponDamageDice(weapon, out damageCount, out damageDice, thrownAttack);
            bonusDamage = weapon.BonusDamage;
            attackLabel = weapon.Name;
            return;
        }

        if (ShouldUseInnateNaturalAttackProfile(weapon))
        {
            NaturalAttackDefinition naturalAttack = Stats.GetPrimaryNaturalAttack();
            if (naturalAttack != null)
            {
                Stats.GetScaledNaturalAttackDamage(naturalAttack, out damageCount, out damageDice);
                // Its Strength share comes from BuildWeaponDamageBonus with the natural attack (MM p.312, CMB-003).
                bonusDamage = 0;
                // Same fallback as FullAttack: only a data name can be a Trip (Ex) trigger (CMB-125).
                attackLabel = string.IsNullOrWhiteSpace(naturalAttack.Name)
                    ? "Natural attack"
                    : naturalAttack.Name;
                return;
            }
        }

        var unarmed = GetUnarmedDamage();
        damageDice = unarmed.damageDice;
        damageCount = unarmed.damageCount;
        bonusDamage = unarmed.bonusDamage;
        attackLabel = "Unarmed strike";
    }

    /// <summary>Check if the given weapon is two-handed.</summary>
    public static bool IsWeaponTwoHanded(ItemData weapon)
    {
        if (weapon == null) return false;
        if (weapon.IsTwoHanded) return true;
        // Also check by name for common two-handed weapons
        string name = weapon.Name.ToLower();
        return name.Contains(ItemIDs.GREATSWORD) || name.Contains(ItemIDs.GREATAXE) || name.Contains(ItemIDs.GREATCLUB)
            || name.Contains(ItemIDs.LONGBOW) || name.Contains("heavy crossbow") || name.Contains(ItemIDs.QUARTERSTAFF)
            || name.Contains(ItemIDs.LONGSPEAR) || name.Contains(ItemIDs.GLAIVE) || name.Contains(ItemIDs.HALBERD)
            || name.Contains(ItemIDs.RANSEUR) || name.Contains("scythe") || name.Contains(ItemIDs.FALCHION);
    }

    private static bool WeaponDisablesStrengthDamageBonuses(ItemData weapon)
    {
        if (weapon == null)
            return false;

        return weapon.NoStrengthToDamage || weapon.HasSpecialProperty("no_str_damage");
    }

    private static bool IsTorchWeapon(ItemData weapon)
    {
        if (weapon == null)
            return false;

        string id = (weapon.Id ?? string.Empty).Trim();
        return string.Equals(id, ItemIDs.TORCH, StringComparison.OrdinalIgnoreCase)
            || weapon.IdEnum == ItemID.WeaponTorch;
    }

    private SpriteRenderer _sr;
    private ConditionManager _conditionManager;
    private CharacterCombatStats _combatStats;
    private CharacterEquipment _equipment;
    private CharacterInventory _inventory;
    private CharacterConditions _conditions;
    private CharacterTags _tags;
    private StatusTagManager _statusTagManager;
    private Coroutine _currentScaleAnimation;
    private Coroutine _grappleAlternateVisibilityCoroutine;
    private int _grappleDisplayPauseLocks;
    private bool _wasBlurVisualActive;
    private float _blurVisualTimeOffset;

    private const float GrappleAlternateVisibilitySeconds = 1f;

    /// <summary>
    /// Whether this creature was brought into the fight by a summoning effect (Summon Monster, Summon Nature's Ally, the
    /// greater lion's shield). Set by every summon registration path and kept after the summon trackers drop it (death
    /// cleanup, despawn), so combat XP can leave it out: DMG p.37, a creature an enemy summons is part of that enemy's CR
    /// and earns no XP of its own. Cleared by <see cref="ResetForNewCreature"/>.
    /// </summary>
    public bool WasSummoned { get; private set; }

    /// <summary>Marks this creature as summoned (<see cref="WasSummoned"/>).</summary>
    public void MarkAsSummoned() => WasSummoned = true;


    // ========== D&D 3.5e HP STATE ==========
    private HPState _currentHPState = HPState.Healthy;
    private bool _hasProcessedDeath;

    public HPState CurrentHPState => _currentHPState;
    public bool IsDead => _currentHPState == HPState.Dead;
    public bool IsAlive => !IsDead;

    /// <summary>Cached accessor for the StatusEffectManager component.</summary>
    private StatusEffectManager _statusEffectManager;
    public StatusEffectManager StatusEffectManager
    {
        get
        {
            if (_statusEffectManager == null)
                _statusEffectManager = GetComponent<StatusEffectManager>();
            return _statusEffectManager;
        }
    }

    /// <summary>Cached accessor for the SpellcastingComponent.</summary>
    private SpellcastingComponent _spellcastingComponent;
    public SpellcastingComponent Spellcasting
    {
        get
        {
            if (_spellcastingComponent == null)
                _spellcastingComponent = GetComponent<SpellcastingComponent>();
            return _spellcastingComponent;
        }
    }

    /// <summary>Cached accessor for the InventoryComponent.</summary>
    private InventoryComponent _inventoryComponent;
    public InventoryComponent InventoryComp
    {
        get
        {
            if (_inventoryComponent == null)
                _inventoryComponent = GetComponent<InventoryComponent>();
            return _inventoryComponent;
        }
    }

    /// <summary>Cached accessor for the ConcentrationManager.</summary>
    private ConcentrationManager _concentrationManager;
    public ConcentrationManager Concentration
    {
        get
        {
            if (_concentrationManager == null)
                _concentrationManager = GetComponent<ConcentrationManager>();
            return _concentrationManager;
        }
    }

    public bool IsUnconscious => _currentHPState == HPState.Unconscious
                                 || _currentHPState == HPState.Dying
                                 || _currentHPState == HPState.Stable
                                 || _currentHPState == HPState.Dead
                                 || HasConditionDirect(CombatConditionType.Unconscious);

    public bool CanTakeTurnActions()
    {
        return _currentHPState == HPState.Healthy || _currentHPState == HPState.Disabled || _currentHPState == HPState.Staggered;
    }
    [Header("Visual Animation Settings")]
    [SerializeField, Min(0f)] private float _sizeChangeDuration = 0.4f;
    [SerializeField] private AnimationEasing _sizeChangeEasing = AnimationEasing.EaseOutCubic;

    public enum AnimationEasing
    {
        Linear,
        EaseOutCubic,
        EaseInOutCubic,
        EaseOutBack,
        EaseOutElastic
    }

    private CharacterCombatStats EnsureCombatStats()
    {
        if (_combatStats == null)
        {
            _combatStats = GetComponent<CharacterCombatStats>();
            if (_combatStats == null)
                _combatStats = gameObject.AddComponent<CharacterCombatStats>();

            _combatStats.Initialize(this);
        }

        return _combatStats;
    }

    private CharacterEquipment EnsureEquipment()
    {
        if (_equipment == null)
        {
            _equipment = GetComponent<CharacterEquipment>();
            if (_equipment == null)
                _equipment = gameObject.AddComponent<CharacterEquipment>();

            _equipment.Initialize(this);
        }

        return _equipment;
    }

    private CharacterInventory EnsureInventory()
    {
        if (_inventory == null)
        {
            _inventory = GetComponent<CharacterInventory>();
            if (_inventory == null)
                _inventory = gameObject.AddComponent<CharacterInventory>();

            _inventory.Initialize(this);
        }

        return _inventory;
    }

    private CharacterConditions EnsureConditions()
    {
        if (_conditions == null)
        {
            _conditions = GetComponent<CharacterConditions>();
            if (_conditions == null)
                _conditions = gameObject.AddComponent<CharacterConditions>();

            ConditionService conditionService = GameManager.Instance != null
                ? GameManager.Instance.GetComponent<ConditionService>()
                : null;

            _conditions.Initialize(this, conditionService);
        }
        else if (GameManager.Instance != null)
        {
            // Keep the reference fresh if services were created after this character.
            ConditionService conditionService = GameManager.Instance.GetComponent<ConditionService>();
            _conditions.SetConditionService(conditionService);
        }

        return _conditions;
    }

    private CharacterTags EnsureTags()
    {
        if (_tags == null)
            _tags = new CharacterTags(this);

        return _tags;
    }

    private StatusTagManager EnsureStatusTagManager()
    {
        if (_statusTagManager == null)
            _statusTagManager = new StatusTagManager(this);

        return _statusTagManager;
    }

    public CharacterInventory Inventory => EnsureInventory();
    public CharacterConditions Conditions => EnsureConditions();
    public CharacterTags Tags => EnsureTags();

    private void Awake()
    {
        NormalizeTeamControlState();

        _sr = GetComponent<SpriteRenderer>();
        if (_sr == null)
            _sr = gameObject.AddComponent<SpriteRenderer>();

        // Battlefield status indicators (condition badges above token).
        if (GetComponent<StatusEffectIndicator>() == null)
            gameObject.AddComponent<StatusEffectIndicator>();

        _conditionManager = GetComponent<ConditionManager>();
        if (_conditionManager == null)
            _conditionManager = gameObject.AddComponent<ConditionManager>();

        EnsureCombatStats();
        EnsureEquipment();
        EnsureInventory();
        EnsureConditions();
        EnsureTags();
        EnsureStatusTagManager();
        _blurVisualTimeOffset = Random.Range(0f, Mathf.PI * 2f);
        _wasBlurVisualActive = HasActiveBlurEffect;
        RefreshInvisibilityVisual();
        RefreshAllTags();
    }

    /// <summary>
    /// Refreshes equipment-derived tags (armor and wielding state).
    /// Called automatically after inventory stat recalculation.
    /// </summary>
    public void RefreshEquipmentTags()
    {
        EnsureStatusTagManager().RefreshEquipmentTags();
    }

    /// <summary>
    /// Refreshes all dynamic tags (identity, HP state, conditions, and equipment).
    /// </summary>
    public void RefreshAllTags()
    {
        EnsureStatusTagManager().RefreshAllTags();
    }

    public void SetDisplayedRace(string raceName)
    {
        if (string.IsNullOrWhiteSpace(raceName))
            raceName = ActualRace;

        _displayedRace = raceName;
        RefreshAllTags();
    }

    public void ResetDisplayedRaceToActual()
    {
        _displayedRace = ActualRace;
        RefreshAllTags();
    }

    public void ApplyDisguiseSelfEffect(string disguisedRace, int durationRemainingRounds, CharacterController caster)
    {
        ActiveDisguiseSelfEffect = new DisguiseSelfEffectData
        {
            OriginalRace = ActualRace,
            DisguisedRace = string.IsNullOrWhiteSpace(disguisedRace) ? ActualRace : disguisedRace,
            DurationRemainingRounds = Mathf.Max(0, durationRemainingRounds)
        };

        ActiveDisguiseSelfEffect.SetCaster(caster);
        SetDisplayedRace(ActiveDisguiseSelfEffect.DisguisedRace);
    }

    public void UpdateDisguiseSelfDuration(int durationRemainingRounds)
    {
        if (ActiveDisguiseSelfEffect == null)
            return;

        ActiveDisguiseSelfEffect.DurationRemainingRounds = Mathf.Max(0, durationRemainingRounds);
    }

    public void ClearDisguiseSelfEffect()
    {
        ActiveDisguiseSelfEffect = null;
        // In a wild shape the sheet keeps showing the form (WildShapeRules; CHR-020).
        if (WildShapeRules.IsWildShaped(Stats))
            SetDisplayedRace(Stats.ActiveWildShape.FormName);
        else
            ResetDisplayedRaceToActual();
    }

    public void ApplyExpeditiousRetreatEffect(int speedBonusFeet, int durationRemainingRounds, CharacterController caster)
    {
        int bonus = Mathf.Max(0, speedBonusFeet);
        int rounds = Mathf.Max(0, durationRemainingRounds);
        if (bonus <= 0 || rounds <= 0)
            return;

        ActiveExpeditiousRetreatEffect = new ExpeditiousRetreatEffectData
        {
            SpeedBonusFeet = bonus,
            DurationRemainingRounds = rounds
        };
        ActiveExpeditiousRetreatEffect.SetCaster(caster);
    }

    public void UpdateExpeditiousRetreatDuration(int durationRemainingRounds)
    {
        if (ActiveExpeditiousRetreatEffect == null)
            return;

        ActiveExpeditiousRetreatEffect.DurationRemainingRounds = Mathf.Max(0, durationRemainingRounds);
    }

    public ExpeditiousRetreatEffectData TickExpeditiousRetreatEffect()
    {
        if (ActiveExpeditiousRetreatEffect == null)
            return null;

        ActiveExpeditiousRetreatEffect.DurationRemainingRounds = Mathf.Max(0, ActiveExpeditiousRetreatEffect.DurationRemainingRounds - 1);
        if (ActiveExpeditiousRetreatEffect.DurationRemainingRounds > 0)
            return null;

        ExpeditiousRetreatEffectData expired = ActiveExpeditiousRetreatEffect;
        ActiveExpeditiousRetreatEffect = null;
        return expired;
    }

    public ExpeditiousRetreatEffectData RemoveExpeditiousRetreatEffect()
    {
        if (ActiveExpeditiousRetreatEffect == null)
            return null;

        ExpeditiousRetreatEffectData removed = ActiveExpeditiousRetreatEffect;
        ActiveExpeditiousRetreatEffect = null;
        return removed;
    }

    public void ClearExpeditiousRetreatEffect()
    {
        ActiveExpeditiousRetreatEffect = null;
    }

    // ========== HASTE EFFECT (PHB p.239) ==========

    /// <summary>True if the character is currently affected by Haste.</summary>
    public bool HasActiveHasteEffect => ActiveHasteEffect != null && ActiveHasteEffect.DurationRemainingRounds > 0;

    /// <summary>Returns the number of rounds remaining for the Haste effect, or 0 if not active.</summary>
    public int GetHasteRemainingRounds()
    {
        return ActiveHasteEffect != null ? Mathf.Max(0, ActiveHasteEffect.DurationRemainingRounds) : 0;
    }

    public void ApplyHasteEffect(int durationRemainingRounds, CharacterController caster)
    {
        int rounds = Mathf.Max(0, durationRemainingRounds);
        if (rounds <= 0)
            return;

        // Haste dispels Slow
        if (HasActiveSlowEffect)
            ClearSlowEffect();

        ActiveHasteEffect = new HasteEffectData
        {
            AttackBonus = 1,
            ACBonus = 1,
            ReflexSaveBonus = 1,
            SpeedBonusFeet = 30,
            GrantsExtraAttack = true,
            DurationRemainingRounds = rounds
        };
        ActiveHasteEffect.SetCaster(caster);
    }

    public void UpdateHasteDuration(int durationRemainingRounds)
    {
        if (ActiveHasteEffect == null)
            return;
        ActiveHasteEffect.DurationRemainingRounds = Mathf.Max(0, durationRemainingRounds);
    }

    public void ClearHasteEffect()
    {
        ActiveHasteEffect = null;
    }

    // ========== SLOW EFFECT (PHB p.280) ==========

    /// <summary>True if the character is currently affected by Slow.</summary>
    public bool HasActiveSlowEffect => ActiveSlowEffect != null && ActiveSlowEffect.DurationRemainingRounds > 0;

    /// <summary>Returns the number of rounds remaining for the Slow effect, or 0 if not active.</summary>
    public int GetSlowRemainingRounds()
    {
        return ActiveSlowEffect != null ? Mathf.Max(0, ActiveSlowEffect.DurationRemainingRounds) : 0;
    }

    public void ApplySlowEffect(int durationRemainingRounds, CharacterController caster)
    {
        int rounds = Mathf.Max(0, durationRemainingRounds);
        if (rounds <= 0)
            return;

        // Slow dispels Haste
        if (HasActiveHasteEffect)
            ClearHasteEffect();

        ActiveSlowEffect = new SlowEffectData
        {
            AttackPenalty = -1,
            ACPenalty = -1,
            ReflexSavePenalty = -1,
            SpeedMultiplier = 0.5f,
            BlocksFullRoundActions = true,
            DurationRemainingRounds = rounds
        };
        ActiveSlowEffect.SetCaster(caster);
        RefreshActionLimits(); // Slow: one move or standard action, no full-round action (PHB p.280, CMB-095)
    }

    public void UpdateSlowDuration(int durationRemainingRounds)
    {
        if (ActiveSlowEffect == null)
            return;
        ActiveSlowEffect.DurationRemainingRounds = Mathf.Max(0, durationRemainingRounds);
        RefreshActionLimits();
    }

    public void ClearSlowEffect()
    {
        ActiveSlowEffect = null;
        RefreshActionLimits(); // never gives back an action already spent
    }

    /// <summary>
    /// This creature's Mirror Image figments (PHB p.254; MirrorImageRules, batch 11 step 26): the images left and the
    /// creatures that know the real figure. Null when it has none. Cleared by <see cref="ResetForNewCreature"/> (CRE-046).
    /// </summary>
    public MirrorImageFigures MirrorImageFigures;

    public bool HasActiveInvisibilityEffect => ActiveInvisibilityEffect != null && ActiveInvisibilityEffect.IsInvisible;
    public bool HasActiveSeeInvisibilityEffect => ActiveSeeInvisibilityEffect != null && ActiveSeeInvisibilityEffect.CanSeeInvisible;
    public bool HasActiveGlitterdustEffect => ActiveGlitterdustEffect != null && ActiveGlitterdustEffect.OutlinedByDust && ActiveGlitterdustEffect.DurationRemainingRounds > 0;
    public bool HasActiveMelfsAcidArrowEffect => ActiveMelfsAcidArrowEffect != null && ActiveMelfsAcidArrowEffect.IsActive && ActiveMelfsAcidArrowEffect.RemainingDamageRounds > 0;
    public bool HasActiveBlurEffect
    {
        get
        {
            StatusEffectManager statusMgr = StatusEffectManager;
            return statusMgr != null && statusMgr.HasEffect(SpellNames.BLUR) && statusMgr.GetRemainingRounds(SpellNames.BLUR) > 0;
        }
    }
    public bool IsOutlinedByGlitterdust => HasActiveGlitterdustEffect;

    public int GetBlurRemainingRounds()
    {
        StatusEffectManager statusMgr = StatusEffectManager;
        return statusMgr != null ? Mathf.Max(0, statusMgr.GetRemainingRounds(SpellNames.BLUR)) : 0;
    }

    /// <summary>
    /// True if the character is currently affected by Displacement (PHB p.222),
    /// which grants a 50% miss chance as if it had total concealment.
    /// True Seeing negates this.
    /// </summary>
    public bool HasActiveDisplacementEffect
    {
        get
        {
            StatusEffectManager statusMgr = StatusEffectManager;
            return statusMgr != null && statusMgr.HasEffect(SpellNames.DISPLACEMENT) && statusMgr.GetRemainingRounds(SpellNames.DISPLACEMENT) > 0;
        }
    }

    /// <summary>
    /// Returns the number of rounds remaining for the Displacement effect, or 0
    /// if no Displacement effect is active.
    /// </summary>
    public int GetDisplacementRemainingRounds()
    {
        StatusEffectManager statusMgr = StatusEffectManager;
        return statusMgr != null ? Mathf.Max(0, statusMgr.GetRemainingRounds(SpellNames.DISPLACEMENT)) : 0;
    }

    /// <summary>
    /// True if the character is currently affected by Blink (PHB p.206),
    /// rapidly shifting between Material and Ethereal Planes.
    /// </summary>
    public bool HasActiveBlinkEffect
    {
        get
        {
            StatusEffectManager statusMgr = StatusEffectManager;
            return statusMgr != null && statusMgr.HasEffect(SpellNames.BLINK) && statusMgr.GetRemainingRounds(SpellNames.BLINK) > 0;
        }
    }

    /// <summary>
    /// Returns the number of rounds remaining for the Blink effect, or 0 if not active.
    /// </summary>
    public int GetBlinkRemainingRounds()
    {
        StatusEffectManager statusMgr = StatusEffectManager;
        return statusMgr != null ? Mathf.Max(0, statusMgr.GetRemainingRounds(SpellNames.BLINK)) : 0;
    }

    /// <summary>
    /// Calculates the Blink miss chance an attacker faces when attacking this blinking target.
    /// Per PHB p.206:
    ///   - Base 50% miss chance
    ///   - 20% if attacker can see invisible OR strike ethereal (not both)
    ///   - 0% if attacker can do BOTH (see invisible AND strike ethereal)
    ///   - Blind-Fight feat does NOT help (target is ethereal, not merely invisible)
    /// </summary>
    public int GetBlinkMissChanceAgainst(CharacterController attacker)
    {
        if (!HasActiveBlinkEffect)
            return 0;

        bool canSeeInvisible = attacker != null && attacker.CanSeeInvisible(this);
        bool canStrikeEthereal = attacker != null && attacker.HasGhostTouchWeapon();

        if (canSeeInvisible && canStrikeEthereal)
            return 0;   // Both: no miss chance
        if (canSeeInvisible || canStrikeEthereal)
            return 20;  // One of the two: reduced miss chance
        return 50;       // Neither: full miss chance
    }

    /// <summary>
    /// Returns true if this character has a Ghost Touch weapon equipped (can strike ethereal creatures).
    /// Used for Blink miss chance reduction.
    /// </summary>
    public bool HasGhostTouchWeapon()
    {
        // Check equipped weapon for the Ghost Touch enchantment.
        // Ghost Touch allows striking incorporeal/ethereal creatures.
        // Uses VisualTags on ItemData for extensible enchantment tagging.
        if (Stats == null) return false;
        var mainWeapon = GetEquippedWeapon();
        if (mainWeapon != null && mainWeapon.VisualTags != null && mainWeapon.VisualTags.Contains("ghost_touch"))
            return true;
        return false;
    }

    /// <summary>
    /// Returns the Blink attacker's own miss chance (20%) when attacking while blinking.
    /// PHB p.206: The blinking creature's own attacks have a 20% miss chance.
    /// </summary>
    public int GetBlinkAttackerMissChance()
    {
        return HasActiveBlinkEffect ? 20 : 0;
    }

    /// <summary>
    /// Returns +2 attack bonus for a blinking attacker (strikes as if invisible).
    /// PHB p.206: Since the blinking creature is etheral, it appears invisible (briefly).
    /// </summary>
    public int GetBlinkAttackerBonus(CharacterController target)
    {
        if (!HasActiveBlinkEffect)
            return 0;
        // If the target can see invisible, no bonus
        if (target != null && target.CanSeeInvisible(this))
            return 0;
        return 2;
    }

    /// <summary>
    /// Returns true if a blinking attacker should deny the target's Dex bonus to AC.
    /// PHB p.206: Blinking creature strikes as invisible.
    /// </summary>
    public bool BlinkDeniesDexToAC(CharacterController target)
    {
        if (!HasActiveBlinkEffect)
            return false;
        // If the target can see invisible creatures, they keep Dex
        if (target != null && target.CanSeeInvisible(this))
            return false;
        return true;
    }

    public void ApplyMelfsAcidArrowEffect(int remainingDamageRounds, CharacterController caster, MetamagicData metamagic = null)
    {
        int rounds = Mathf.Max(0, remainingDamageRounds);
        if (rounds <= 0)
        {
            ClearMelfsAcidArrowEffect();
            return;
        }

        if (ActiveMelfsAcidArrowEffect == null)
            ActiveMelfsAcidArrowEffect = new MelfsAcidArrowEffectData();

        ActiveMelfsAcidArrowEffect.IsActive = true;
        ActiveMelfsAcidArrowEffect.RemainingDamageRounds = rounds;
        ActiveMelfsAcidArrowEffect.SetCaster(caster);
        ActiveMelfsAcidArrowEffect.Metamagic = metamagic;
    }

    public void UpdateMelfsAcidArrowDuration(int remainingDamageRounds)
    {
        if (ActiveMelfsAcidArrowEffect == null)
            return;

        ActiveMelfsAcidArrowEffect.RemainingDamageRounds = Mathf.Max(0, remainingDamageRounds);
        ActiveMelfsAcidArrowEffect.IsActive = ActiveMelfsAcidArrowEffect.RemainingDamageRounds > 0;
    }

    public void ClearMelfsAcidArrowEffect()
    {
        ActiveMelfsAcidArrowEffect = null;
    }

    public void ApplyInvisibilityEffect(int durationRemainingRounds, CharacterController caster, bool isMoving = false)
    {
        int rounds = Mathf.Max(0, durationRemainingRounds);
        if (rounds <= 0)
            return;

        if (ActiveInvisibilityEffect == null)
            ActiveInvisibilityEffect = InvisibilityEffectData.CreateStandardInvisibility(rounds, caster);
        else
        {
            ActiveInvisibilityEffect.IsInvisible = true;
            ActiveInvisibilityEffect.DurationRemainingRounds = rounds;
            ActiveInvisibilityEffect.IsMoving = isMoving;
            ActiveInvisibilityEffect.SetCaster(caster);
            // Preserve default standard invisibility settings
            if (string.IsNullOrEmpty(ActiveInvisibilityEffect.SourceSpellId))
            {
                ActiveInvisibilityEffect.SourceSpellId = SpellNames.INVISIBILITY;
                ActiveInvisibilityEffect.SourceName = "Invisibility";
                ActiveInvisibilityEffect.SourceType = InvisibilitySourceType.Spell;
                ActiveInvisibilityEffect.BreaksOnAttack = true;
            }
        }
        ActiveInvisibilityEffect.IsMoving = isMoving;
        RefreshInvisibilityVisual();
    }

    /// <summary>
    /// Applies an invisibility effect using a pre-configured InvisibilityEffectData.
    /// Used by Greater Invisibility, magic items, and special abilities.
    /// </summary>
    public void ApplyInvisibilityEffectData(InvisibilityEffectData effectData)
    {
        if (effectData == null || !effectData.IsInvisible)
            return;

        ActiveInvisibilityEffect = effectData;
        RefreshInvisibilityVisual();
    }

    public void ApplySeeInvisibilityEffect(int durationRemainingRounds, CharacterController caster)
    {
        int rounds = Mathf.Max(0, durationRemainingRounds);
        if (rounds <= 0)
            return;

        if (ActiveSeeInvisibilityEffect == null)
            ActiveSeeInvisibilityEffect = new SeeInvisibilityEffectData();

        ActiveSeeInvisibilityEffect.CanSeeInvisible = true;
        ActiveSeeInvisibilityEffect.DurationRemainingRounds = rounds;
        ActiveSeeInvisibilityEffect.SetCaster(caster != null ? caster : this);
    }

    public void ApplyGlitterdustEffect(int durationRemainingRounds, CharacterController caster, bool blindedByFailedSave)
    {
        int rounds = Mathf.Max(0, durationRemainingRounds);
        if (rounds <= 0)
            return;

        if (ActiveGlitterdustEffect == null)
            ActiveGlitterdustEffect = new GlitterdustEffectData();

        ActiveGlitterdustEffect.OutlinedByDust = true;
        ActiveGlitterdustEffect.DurationRemainingRounds = Mathf.Max(rounds, ActiveGlitterdustEffect.DurationRemainingRounds);
        ActiveGlitterdustEffect.IsBlinded = ActiveGlitterdustEffect.IsBlinded || blindedByFailedSave;
        ActiveGlitterdustEffect.SetCaster(caster);

        RefreshInvisibilityVisual();
        UpdateGlitterdustVisual();
    }

    public void UpdateSeeInvisibilityDuration(int durationRemainingRounds)
    {
        if (ActiveSeeInvisibilityEffect == null)
            return;

        ActiveSeeInvisibilityEffect.DurationRemainingRounds = Mathf.Max(0, durationRemainingRounds);
    }

    public void UpdateGlitterdustDuration(int durationRemainingRounds)
    {
        if (ActiveGlitterdustEffect == null)
            return;

        ActiveGlitterdustEffect.DurationRemainingRounds = Mathf.Max(0, durationRemainingRounds);
        if (ActiveGlitterdustEffect.DurationRemainingRounds <= 0)
            ClearGlitterdustEffect();
        else
            UpdateGlitterdustVisual();
    }

    public void SetGlitterdustBlindedState(bool isBlinded)
    {
        if (ActiveGlitterdustEffect == null)
            return;

        ActiveGlitterdustEffect.IsBlinded = isBlinded;
    }

    public void ClearSeeInvisibilityEffect()
    {
        ActiveSeeInvisibilityEffect = null;
    }

    public void ClearGlitterdustEffect()
    {
        ActiveGlitterdustEffect = null;
        UpdateGlitterdustVisual(forceDisable: true);
        RefreshInvisibilityVisual();
    }

    public bool CanSeeInvisible(CharacterController target = null)
    {
        if (!HasActiveSeeInvisibilityEffect)
            return false;

        // See Invisible only reveals invisible creatures/objects and does not pierce walls.
        // LOS/cover constraints are handled by existing targeting and concealment systems.
        return true;
    }

    public void UpdateInvisibilityDuration(int durationRemainingRounds)
    {
        if (ActiveInvisibilityEffect == null)
            return;

        ActiveInvisibilityEffect.DurationRemainingRounds = Mathf.Max(0, durationRemainingRounds);
    }

    public void UpdateInvisibilityMovementState(bool isMoving)
    {
        if (ActiveInvisibilityEffect == null || !ActiveInvisibilityEffect.IsInvisible)
            return;

        ActiveInvisibilityEffect.IsMoving = isMoving;
    }

    public int GetInvisibilityHideBonus()
    {
        if (!HasActiveInvisibilityEffect || IsOutlinedByGlitterdust)
            return 0;

        return ActiveInvisibilityEffect.GetCurrentHideBonus();
    }

    public int GetInvisibilityHideBonusAgainst(CharacterController observer)
    {
        if (!HasActiveInvisibilityEffect || IsOutlinedByGlitterdust)
            return 0;

        if (observer != null && observer != this && observer.CanSeeInvisible(this))
            return 0;

        return GetInvisibilityHideBonus();
    }

    public int GetInvisibilityArmorClassBonusAgainst(CharacterController attacker)
    {
        if (!HasActiveInvisibilityEffect || IsOutlinedByGlitterdust)
            return 0;

        if (attacker != null && attacker.CanSeeInvisible(this))
            return 0;

        return 2;
    }

    public int GetGlitterdustHidePenalty()
    {
        return IsOutlinedByGlitterdust ? -40 : 0;
    }

    /// <summary>
    /// Attempts to break invisibility due to a hostile action.
    /// For standard Invisibility (BreaksOnAttack=true), the effect ends.
    /// For Greater Invisibility (BreaksOnAttack=false), the effect persists.
    /// Returns true if invisibility was actually broken.
    /// </summary>
    public bool BreakInvisibility(string reason, CharacterController hostileTarget = null)
    {
        if (!HasActiveInvisibilityEffect)
            return false;

        // INVISIBILITY SPHERE (PHB p.245): Per-creature invisibility from a
        // sphere has BreaksOnAttack=false, but attack handling is still required.
        //   - If the recipient (sphere center) attacks → entire sphere ends.
        //   - If any other affected creature attacks → only that creature visible.
        if (ActiveInvisibilityEffect.MatchesSpellId(SpellNames.INVISIBILITY_SPHERE))
        {
            if (GameManager.Instance != null &&
                GameManager.Instance.TryHandleInvisibilitySphereAttack(this, reason ?? "hostile action"))
            {
                return true;
            }
            // If sphere lookup failed, fall through to standard handling.
        }

        // Greater Invisibility and similar effects do NOT break on attack/hostile action
        if (!ActiveInvisibilityEffect.BreaksOnAttack)
            return false;

        string sourceSpellId = ActiveInvisibilityEffect.SourceSpellId;
        // Read before the spell effect is removed: removing the Invisibility effect clears ActiveInvisibilityEffect
        // (StatusEffectManager -> ClearInvisibilityEffect), and reading it afterwards threw a NullReferenceException.
        string sourceName = ActiveInvisibilityEffect.SourceName ?? "invisibility";

        StatusEffectManager statusMgr = StatusEffectManager;
        if (statusMgr != null)
        {
            // Remove the specific spell effect that created this invisibility
            if (!string.IsNullOrEmpty(sourceSpellId))
                statusMgr.RemoveEffectsBySpellId(sourceSpellId);
            else
                statusMgr.RemoveEffectsBySpellId(SpellNames.INVISIBILITY);
        }

        string actorName = Stats != null ? Stats.CharacterName : name;
        string reasonLabel = string.IsNullOrWhiteSpace(reason) ? "hostile action" : reason;
        string targetLabel = hostileTarget != null && hostileTarget.Stats != null
            ? $" against {hostileTarget.Stats.CharacterName}"
            : string.Empty;
        GameManager.Instance?.CombatUI?.ShowCombatLog(CombatLogHelper.Defensive("👁", $"{actorName}'s {sourceName} ends ({reasonLabel}{targetLabel})."));

        if (HasCondition(CombatConditionType.Invisible))
            RemoveCondition(CombatConditionType.Invisible);

        ClearInvisibilityEffect();
        return true;
    }

    /// <summary>
    /// Forces invisibility to end regardless of BreaksOnAttack setting.
    /// Used for dispel, antimagic, or when the duration expires.
    /// </summary>
    public void ForceEndInvisibility(string reason = null)
    {
        if (!HasActiveInvisibilityEffect)
            return;

        string sourceSpellId = ActiveInvisibilityEffect.SourceSpellId;

        StatusEffectManager statusMgr = StatusEffectManager;
        if (statusMgr != null && !string.IsNullOrEmpty(sourceSpellId))
            statusMgr.RemoveEffectsBySpellId(sourceSpellId);

        if (HasCondition(CombatConditionType.Invisible))
            RemoveCondition(CombatConditionType.Invisible);

        string actorName = Stats != null ? Stats.CharacterName : name;
        string reasonLabel = string.IsNullOrWhiteSpace(reason) ? "effect ended" : reason;
        GameManager.Instance?.CombatUI?.ShowCombatLog(CombatLogHelper.Defensive("👁", $"{actorName}'s invisibility ends ({reasonLabel})."));

        ClearInvisibilityEffect();
    }

    /// <summary>
    /// Returns the invisible attacker's attack roll bonus (+2 per PHB p.141).
    /// This should be captured BEFORE BreakInvisibility is called during an attack.
    /// </summary>
    public int GetInvisibleAttackerBonus(CharacterController target)
    {
        if (!HasActiveInvisibilityEffect || IsOutlinedByGlitterdust)
            return 0;

        // If the target can see invisible creatures, no bonus
        if (target != null && target.CanSeeInvisible(this))
            return 0;

        return ActiveInvisibilityEffect.GetAttackBonus();
    }

    /// <summary>
    /// Returns true if this invisible attacker should deny the target's Dex bonus to AC.
    /// PHB p.141: A defender who can't see the attacker loses Dex bonus to AC.
    /// This should be checked BEFORE BreakInvisibility is called during an attack.
    /// </summary>
    public bool ShouldDenyTargetDexToAC(CharacterController target)
    {
        if (!HasActiveInvisibilityEffect || IsOutlinedByGlitterdust)
            return false;

        // If the target can see invisible creatures, they keep Dex
        if (target != null && target.CanSeeInvisible(this))
            return false;

        return true;
    }

    public void ClearInvisibilityEffect()
    {
        ActiveInvisibilityEffect = null;
        RefreshInvisibilityVisual();
    }

    // ======================== BLINDNESS / DEAFNESS EFFECT ========================

    /// <summary>Whether this character has an active blindness/deafness effect from the spell system.</summary>
    public bool HasActiveBlindnessDeafnessEffect => ActiveBlindnessDeafnessEffect != null && ActiveBlindnessDeafnessEffect.IsActive;

    /// <summary>
    /// Returns true if this character is blinded (from any source: spell effect data OR condition system).
    /// Checks both the dedicated BlindnessDeafnessEffectData and the condition system for redundancy.
    /// </summary>
    public bool IsBlind()
    {
        if (ActiveBlindnessDeafnessEffect != null && ActiveBlindnessDeafnessEffect.IsBlindness)
            return true;
        return HasCondition(CombatConditionType.Blinded);
    }

    /// <summary>
    /// Returns true if this character is deafened (from any source: spell effect data OR condition system).
    /// </summary>
    public bool IsDeaf()
    {
        if (ActiveBlindnessDeafnessEffect != null && ActiveBlindnessDeafnessEffect.IsDeafness)
            return true;
        return HasCondition(CombatConditionType.Deafened);
    }

    /// <summary>
    /// Applies a blindness effect from the Blindness/Deafness spell.
    /// Also applies the Blinded condition to the condition system for combat integration.
    /// PHB p.206, p.305: -2 AC, lose Dex to AC, 50% miss chance, half speed.
    /// </summary>
    public void ApplyBlindnessEffect(BlindnessDeafnessEffectData effectData)
    {
        if (effectData == null) return;
        ActiveBlindnessDeafnessEffect = effectData;

        // Also apply the Blinded condition for combat system integration
        // The condition system already handles -2 AC, DeniedDexToAC, 0.5x movement, etc.
        int rounds = effectData.DurationRemainingRounds; // -1 = permanent
        string sourceName = effectData.SourceName ?? "Blindness";
        ApplyCondition(CombatConditionType.Blinded, rounds, sourceName);

        Debug.Log($"[BlindnessDeafness] {Stats?.CharacterName}: Blinded by {sourceName} (permanent={effectData.IsPermanent})");
    }

    /// <summary>
    /// Applies a deafness effect from the Blindness/Deafness spell.
    /// Also applies the Deafened condition to the condition system for combat integration.
    /// PHB p.206, p.307: -4 initiative, 20% verbal spell failure.
    /// </summary>
    public void ApplyDeafnessEffect(BlindnessDeafnessEffectData effectData)
    {
        if (effectData == null) return;
        ActiveBlindnessDeafnessEffect = effectData;

        // Also apply the Deafened condition for combat system integration
        // The condition system already handles -4 initiative modifier.
        int rounds = effectData.DurationRemainingRounds; // -1 = permanent
        string sourceName = effectData.SourceName ?? "Deafness";
        ApplyCondition(CombatConditionType.Deafened, rounds, sourceName);

        Debug.Log($"[BlindnessDeafness] {Stats?.CharacterName}: Deafened by {sourceName} (permanent={effectData.IsPermanent})");
    }

    /// <summary>
    /// Removes the active blindness/deafness spell effect and the associated condition.
    /// Called by Remove Blindness/Deafness spell, Dispel Magic, or caster dismissal.
    /// </summary>
    public void RemoveBlindnessDeafnessEffect()
    {
        if (ActiveBlindnessDeafnessEffect == null) return;

        string effectName = ActiveBlindnessDeafnessEffect.IsBlindness ? "Blindness" : "Deafness";
        CombatConditionType conditionToRemove = ActiveBlindnessDeafnessEffect.IsBlindness
            ? CombatConditionType.Blinded
            : CombatConditionType.Deafened;

        Debug.Log($"[BlindnessDeafness] {Stats?.CharacterName}: {effectName} removed");

        ActiveBlindnessDeafnessEffect = null;
        RemoveCondition(conditionToRemove);
    }

    /// <summary>
    /// Returns the verbal component spell failure chance from deafness.
    /// PHB p.307: 20% chance of spell failure for spells with verbal components.
    /// </summary>
    public int GetDeafnessSpellFailureChance()
    {
        if (ActiveBlindnessDeafnessEffect != null && ActiveBlindnessDeafnessEffect.IsDeafness)
            return ActiveBlindnessDeafnessEffect.GetVerbalSpellFailureChance();
        if (HasCondition(CombatConditionType.Deafened))
            return 20;
        return 0;
    }

    // ======================== COMMAND UNDEAD METHODS ========================

    /// <summary>
    /// Returns true if this character is currently under a Command Undead spell effect.
    /// </summary>
    public bool IsCommandedUndead => ActiveCommandUndeadEffect != null && ActiveCommandUndeadEffect.IsActive;

    /// <summary>
    /// Returns the caster who controls this undead via Command Undead, or null.
    /// </summary>
    public CharacterController CommandUndeadController => IsCommandedUndead ? ActiveCommandUndeadEffect.Caster : null;

    /// <summary>
    /// Returns the list of undead this character has under Command Undead control.
    /// </summary>
    public System.Collections.Generic.List<CommandUndeadEffectData> CommandedUndeadList => _commandedUndeadList;

    /// <summary>
    /// Returns true if this character can be targeted by Command Undead (must be undead creature type).
    /// </summary>
    public bool CanBeCommandedAsUndead()
    {
        if (Stats == null) return false;
        string creatureType = string.IsNullOrWhiteSpace(Stats.CreatureType)
            ? string.Empty
            : Stats.CreatureType.Trim().ToLowerInvariant();
        return creatureType == "undead";
    }

    /// <summary>
    /// Returns true if this undead is intelligent (Int ≥ 1).
    /// PHB p.211: Intelligent undead get a Will save; nonintelligent do not.
    /// </summary>
    public bool IsIntelligentUndead()
    {
        if (Stats == null) return false;
        if (Stats.IsMindless) return false;
        // INT of NO_SCORE (represented as -999 or similar) means no Intelligence score
        return Stats.INT >= 1;
    }

    /// <summary>
    /// Applies a Command Undead effect to this character (the target undead).
    /// Called after SR and save checks pass.
    /// </summary>
    public void ApplyCommandUndeadEffect(CommandUndeadEffectData effectData)
    {
        if (effectData == null) return;

        // Remove any existing Command Undead effect first
        RemoveCommandUndeadEffect();

        ActiveCommandUndeadEffect = effectData;

        // Register this effect on the caster's controlled list
        if (effectData.Caster != null)
        {
            effectData.Caster._commandedUndeadList.Add(effectData);
        }

        Debug.Log($"[CommandUndead] {Stats?.CharacterName} is now commanded by {effectData.CasterName} " +
                  $"(intelligent={effectData.IsIntelligent}, duration={effectData.DurationRemainingRounds} rounds)");
    }

    /// <summary>
    /// Removes the active Command Undead effect from this character.
    /// Also removes the effect from the caster's controlled list.
    /// </summary>
    public void RemoveCommandUndeadEffect()
    {
        if (ActiveCommandUndeadEffect == null) return;

        var oldEffect = ActiveCommandUndeadEffect;
        oldEffect.BreakControl("Effect removed");

        // Remove from caster's tracked list
        if (oldEffect.Caster != null)
        {
            oldEffect.Caster._commandedUndeadList.Remove(oldEffect);
        }

        ActiveCommandUndeadEffect = null;

        Debug.Log($"[CommandUndead] {Stats?.CharacterName}: Command Undead effect removed");
    }

    /// <summary>
    /// Breaks Command Undead control due to a threatening act by the caster or their allies.
    /// PHB p.211: Any threatening act by caster or apparent allies breaks the spell.
    /// </summary>
    public void BreakCommandUndeadControl(string reason = "Threatening act")
    {
        if (ActiveCommandUndeadEffect == null || !ActiveCommandUndeadEffect.IsActive) return;

        string casterName = ActiveCommandUndeadEffect.CasterName ?? "Unknown";
        Debug.Log($"[CommandUndead] {Stats?.CharacterName}: Control by {casterName} broken — {reason}");

        RemoveCommandUndeadEffect();
    }

    // ===================== FALSE LIFE EFFECT =====================

    /// <summary>Whether this character has an active False Life effect with remaining temp HP.</summary>
    public bool HasActiveFalseLifeEffect => ActiveFalseLifeEffect != null && ActiveFalseLifeEffect.IsActive && ActiveFalseLifeEffect.HasTempHP;

    /// <summary>
    /// Apply a False Life effect to this character.
    /// If an existing False Life is active, follows D&D 3.5e non-stacking rules:
    /// use the higher temp HP value (do not add them together).
    /// </summary>
    public void ApplyFalseLifeEffect(FalseLifeEffectData effectData)
    {
        if (effectData == null || Stats == null) return;

        // Non-stacking: if existing False Life is active, keep higher value
        if (ActiveFalseLifeEffect != null && ActiveFalseLifeEffect.IsActive && ActiveFalseLifeEffect.HasTempHP)
        {
            if (effectData.CurrentTempHP <= ActiveFalseLifeEffect.CurrentTempHP)
            {
                Debug.Log($"[FalseLife] {Stats.CharacterName}: New False Life ({effectData.CurrentTempHP} temp HP) " +
                          $"not applied — existing ({ActiveFalseLifeEffect.CurrentTempHP} temp HP) is higher or equal");
                return;
            }

            // New is higher — remove old temp HP from stats before applying new
            Debug.Log($"[FalseLife] {Stats.CharacterName}: Replacing False Life ({ActiveFalseLifeEffect.CurrentTempHP} temp HP) " +
                      $"with higher value ({effectData.CurrentTempHP} temp HP)");
            Stats.TempHP = Mathf.Max(0, Stats.TempHP - ActiveFalseLifeEffect.CurrentTempHP);
            ActiveFalseLifeEffect.Discharge("replaced by higher False Life");
        }

        ActiveFalseLifeEffect = effectData;

        // Apply temp HP to stats
        Stats.TempHP += effectData.CurrentTempHP;

        Debug.Log($"[FalseLife] {Stats.CharacterName}: False Life applied — {effectData.CurrentTempHP} temp HP " +
                  $"(CL {effectData.CasterLevel}, duration {effectData.DurationRemainingRounds} rounds)");
    }

    /// <summary>
    /// Remove the active False Life effect, clearing remaining temp HP from stats.
    /// Called when the spell is dispelled, expires, or is otherwise removed.
    /// </summary>
    public void RemoveFalseLifeEffect()
    {
        if (ActiveFalseLifeEffect == null) return;

        if (ActiveFalseLifeEffect.IsActive && Stats != null)
        {
            // Remove remaining temp HP from stats
            Stats.TempHP = Mathf.Max(0, Stats.TempHP - ActiveFalseLifeEffect.CurrentTempHP);
        }

        ActiveFalseLifeEffect.Discharge("effect removed");
        ActiveFalseLifeEffect = null;

        Debug.Log($"[FalseLife] {Stats?.CharacterName}: False Life effect removed");
    }

    // ===================== ATTRIBUTE ENHANCEMENT EFFECTS =====================

    /// <summary>Check if this character has an active attribute enhancement on a specific ability.</summary>
    public bool HasActiveAttributeEnhancement(AbilityType ability)
    {
        return _activeAttributeEnhancements.ContainsKey(ability) && _activeAttributeEnhancements[ability].IsActive;
    }

    /// <summary>Get the active attribute enhancement effect for a specific ability, or null.</summary>
    public AttributeEnhancementEffectData GetActiveAttributeEnhancement(AbilityType ability)
    {
        _activeAttributeEnhancements.TryGetValue(ability, out var data);
        return data != null && data.IsActive ? data : null;
    }

    /// <summary>Get all active attribute enhancement effects.</summary>
    public System.Collections.Generic.List<AttributeEnhancementEffectData> GetAllActiveAttributeEnhancements()
    {
        var list = new System.Collections.Generic.List<AttributeEnhancementEffectData>();
        foreach (var kvp in _activeAttributeEnhancements)
        {
            if (kvp.Value != null && kvp.Value.IsActive)
                list.Add(kvp.Value);
        }
        return list;
    }

    /// <summary>
    /// Apply an attribute enhancement effect to this character.
    /// Handles D&D 3.5e non-stacking: if an enhancement bonus to the same ability
    /// already exists, only the highest applies (duration refreshes if same spell).
    /// For Bear's Endurance: grants bonus HP (2 per HD) added to current and max HP.
    /// </summary>
    public void ApplyAttributeEnhancement(AttributeEnhancementEffectData effectData)
    {
        if (effectData == null || Stats == null) return;

        AbilityType ability = effectData.EnhancedAbility;

        // Check for existing enhancement on the same ability
        if (_activeAttributeEnhancements.TryGetValue(ability, out var existing) && existing != null && existing.IsActive)
        {
            if (effectData.BonusAmount <= existing.BonusAmount)
            {
                // Same or lower bonus — don't stack, just refresh duration if same spell
                if (effectData.SourceSpellId == existing.SourceSpellId)
                {
                    existing.DurationRemainingRounds = Mathf.Max(existing.DurationRemainingRounds, effectData.DurationRemainingRounds);
                    Debug.Log($"[AttributeEnhancement] {Stats.CharacterName}: {effectData.SourceName} duration refreshed");
                }
                else
                {
                    Debug.Log($"[AttributeEnhancement] {Stats.CharacterName}: {effectData.SourceName} (+{effectData.BonusAmount}) " +
                              $"not applied — existing enhancement (+{existing.BonusAmount}) is equal or higher");
                }
                return;
            }

            // New is higher — remove old first
            Debug.Log($"[AttributeEnhancement] {Stats.CharacterName}: Replacing {existing.SourceName} (+{existing.BonusAmount}) " +
                      $"with {effectData.SourceName} (+{effectData.BonusAmount})");
            RemoveAttributeEnhancementInternal(existing);
        }

        _activeAttributeEnhancements[ability] = effectData;

        // For Bear's Endurance: add bonus HP
        if (effectData.IsBearsEndurance && effectData.GrantedBonusHP > 0)
        {
            Stats.BonusMaxHP += effectData.GrantedBonusHP;
            Stats.CurrentHP += effectData.GrantedBonusHP;
            Debug.Log($"[AttributeEnhancement] {Stats.CharacterName}: Bear's Endurance grants +{effectData.GrantedBonusHP} HP " +
                      $"({effectData.GrantedBonusHP / 2} HD × 2 HP/HD)");
        }

        Debug.Log($"[AttributeEnhancement] {Stats.CharacterName}: {effectData.SourceName} applied — " +
                  $"+{effectData.BonusAmount} enhancement bonus to {effectData.AbilityName}");
    }

    /// <summary>
    /// Remove an attribute enhancement effect. For Bear's Endurance, removes the bonus HP
    /// from both current and max HP — this can kill the character if current HP drops to 0 or below.
    /// Returns true if the removal caused the character to drop to 0 or fewer HP.
    /// </summary>
    public bool RemoveAttributeEnhancement(AbilityType ability)
    {
        if (!_activeAttributeEnhancements.TryGetValue(ability, out var existing) || existing == null)
            return false;

        bool causedDeath = RemoveAttributeEnhancementInternal(existing);
        _activeAttributeEnhancements.Remove(ability);
        return causedDeath;
    }

    /// <summary>
    /// Remove an attribute enhancement by spell ID. Used when a specific spell expires/is dispelled.
    /// Returns true if the removal caused the character to drop to 0 or fewer HP.
    /// </summary>
    public bool RemoveAttributeEnhancementBySpellId(string spellId)
    {
        AbilityType ability = AttributeEnhancementEffectData.GetAbilityForSpell(spellId);
        if (!_activeAttributeEnhancements.TryGetValue(ability, out var existing) || existing == null)
            return false;
        if (existing.SourceSpellId != spellId)
            return false;

        bool causedDeath = RemoveAttributeEnhancementInternal(existing);
        _activeAttributeEnhancements.Remove(ability);
        return causedDeath;
    }

    /// <summary>Internal helper to reverse an attribute enhancement effect.</summary>
    private bool RemoveAttributeEnhancementInternal(AttributeEnhancementEffectData effectData)
    {
        if (effectData == null || Stats == null) return false;

        bool causedDeath = false;

        // For Bear's Endurance: remove bonus HP — can kill!
        if (effectData.IsBearsEndurance && effectData.GrantedBonusHP > 0)
        {
            Stats.BonusMaxHP = Mathf.Max(0, Stats.BonusMaxHP - effectData.GrantedBonusHP);
            Stats.CurrentHP -= effectData.GrantedBonusHP;

            Debug.Log($"[AttributeEnhancement] {Stats.CharacterName}: Bear's Endurance HP removed — " +
                      $"lost {effectData.GrantedBonusHP} HP (now {Stats.CurrentHP}/{Stats.TotalMaxHP})");

            if (Stats.CurrentHP <= 0)
            {
                causedDeath = true;
                Debug.Log($"[AttributeEnhancement] ☠ {Stats.CharacterName}: Dropped to {Stats.CurrentHP} HP " +
                          $"after Bear's Endurance ended — dying/dead!");
            }
        }

        effectData.Expire("effect removed");

        Debug.Log($"[AttributeEnhancement] {Stats.CharacterName}: {effectData.SourceName} removed — " +
                  $"+{effectData.BonusAmount} {effectData.AbilityName} enhancement bonus removed");

        return causedDeath;
    }

    // ===================== GHOUL TOUCH EFFECT =====================

    /// <summary>Whether this character has an active Ghoul Touch paralysis effect.</summary>
    public bool HasActiveGhoulTouchEffect => ActiveGhoulTouchEffect != null && ActiveGhoulTouchEffect.IsParalyzed;

    /// <summary>
    /// Apply a Ghoul Touch effect to this character.
    /// Sets paralysis condition and activates stench aura.
    /// </summary>
    public void ApplyGhoulTouchEffect(GhoulTouchEffectData effectData)
    {
        if (effectData == null || Stats == null) return;

        ActiveGhoulTouchEffect = effectData;

        // Apply Paralyzed condition via the condition system
        ApplyCondition(CombatConditionType.Paralyzed, effectData.ParalysisDurationRounds,
            effectData.CasterName ?? "Ghoul Touch");

        // Also apply Helpless condition (paralysis implies helpless in D&D 3.5e)
        ApplyCondition(CombatConditionType.Helpless, effectData.ParalysisDurationRounds,
            effectData.CasterName ?? "Ghoul Touch");

        Debug.Log($"[GhoulTouch] {Stats.CharacterName}: Paralyzed for {effectData.ParalysisDurationRounds} rounds (stench aura active)");
    }

    /// <summary>
    /// Remove the active Ghoul Touch effect from this character.
    /// Clears paralysis and stench aura.
    /// </summary>
    public void RemoveGhoulTouchEffect()
    {
        if (ActiveGhoulTouchEffect == null) return;

        ActiveGhoulTouchEffect.Expire("effect removed");
        ActiveGhoulTouchEffect = null;

        // Remove conditions
        RemoveCondition(CombatConditionType.Paralyzed);
        RemoveCondition(CombatConditionType.Helpless);

        Debug.Log($"[GhoulTouch] {Stats?.CharacterName}: Ghoul Touch effect removed");
    }

    // ===================== SCARE EFFECT =====================

    /// <summary>Whether this character has an active Scare fear effect.</summary>
    public bool HasActiveScareEffect => ActiveScareEffect != null && ActiveScareEffect.IsActive;

    /// <summary>
    /// Apply a Scare effect to this character.
    /// Applies Frightened or Shaken condition based on the effect data.
    /// </summary>
    public void ApplyScareEffect(ScareEffectData effectData)
    {
        if (effectData == null || Stats == null) return;

        ActiveScareEffect = effectData;

        if (effectData.IsFrightened)
        {
            ApplyCondition(CombatConditionType.Frightened, effectData.DurationRemainingRounds,
                effectData.CasterName ?? "Scare");
            Debug.Log($"[Scare] {Stats.CharacterName}: Frightened for {effectData.DurationRemainingRounds} rounds");
        }
        else if (effectData.IsShaken)
        {
            ApplyCondition(CombatConditionType.Shaken, effectData.DurationRemainingRounds,
                effectData.CasterName ?? "Scare");
            Debug.Log($"[Scare] {Stats.CharacterName}: Shaken for {effectData.DurationRemainingRounds} round(s)");
        }
    }

    /// <summary>
    /// Remove the active Scare effect from this character.
    /// </summary>
    public void RemoveScareEffect()
    {
        if (ActiveScareEffect == null) return;

        FearLevel level = ActiveScareEffect.CurrentFearLevel;
        ActiveScareEffect.Expire("effect removed");
        ActiveScareEffect = null;

        if (level == FearLevel.Frightened)
            RemoveCondition(CombatConditionType.Frightened);
        else if (level == FearLevel.Shaken)
            RemoveCondition(CombatConditionType.Shaken);

        Debug.Log($"[Scare] {Stats?.CharacterName}: Scare effect removed");
    }

    // ===================== SPECTRAL HAND EFFECT =====================

    /// <summary>Whether this character has an active Spectral Hand.</summary>
    public bool HasActiveSpectralHandEffect => ActiveSpectralHandEffect != null && ActiveSpectralHandEffect.IsHandAvailable;

    /// <summary>
    /// Apply a Spectral Hand effect to this character.
    /// The caster loses HP equal to the hand's HP on creation.
    /// </summary>
    public void ApplySpectralHandEffect(SpectralHandEffectData effectData)
    {
        if (effectData == null || Stats == null) return;

        // Remove existing spectral hand if any
        if (ActiveSpectralHandEffect != null && ActiveSpectralHandEffect.IsActive)
        {
            RemoveSpectralHandEffect();
        }

        ActiveSpectralHandEffect = effectData;

        // Apply HP loss to caster
        Stats.CurrentHP -= effectData.CasterHPLost;
        if (Stats.CurrentHP < 0) Stats.CurrentHP = 0;

        Debug.Log($"[SpectralHand] {Stats.CharacterName}: Spectral Hand created. Lost {effectData.CasterHPLost} HP. " +
                  $"Hand HP: {effectData.CurrentHandHP}, AC: {effectData.HandAC}");
    }

    /// <summary>
    /// Remove the active Spectral Hand effect. Restores HP if hand was not destroyed.
    /// </summary>
    public void RemoveSpectralHandEffect()
    {
        if (ActiveSpectralHandEffect == null) return;

        int hpRestored = ActiveSpectralHandEffect.EndSpell("effect removed");

        if (hpRestored > 0 && Stats != null)
        {
            Stats.CurrentHP = Mathf.Min(Stats.CurrentHP + hpRestored, Stats.TotalMaxHP);
            Debug.Log($"[SpectralHand] {Stats.CharacterName}: Regained {hpRestored} HP (spell ended normally)");
        }

        ActiveSpectralHandEffect = null;
        Debug.Log($"[SpectralHand] {Stats?.CharacterName}: Spectral Hand effect removed");
    }

    /// <summary>
    /// Handle destruction of the spectral hand. Caster does NOT regain HP.
    /// </summary>
    public void DestroySpectralHand()
    {
        if (ActiveSpectralHandEffect == null) return;

        ActiveSpectralHandEffect.DestroyHand();
        ActiveSpectralHandEffect = null;
        Debug.Log($"[SpectralHand] {Stats?.CharacterName}: Spectral Hand destroyed — HP loss is permanent");
    }

    /// <summary>
    /// Get the touch attack bonus from Spectral Hand (+2) if delivering through hand.
    /// </summary>
    public int GetSpectralHandTouchAttackBonus()
    {
        return HasActiveSpectralHandEffect ? SpectralHandEffectData.TOUCH_ATTACK_BONUS : 0;
    }

    /// <summary>
    /// Check if a spell can be delivered through the active spectral hand.
    /// </summary>
    public bool CanDeliverSpellThroughSpectralHand(SpellData spell)
    {
        return HasActiveSpectralHandEffect && ActiveSpectralHandEffect.CanDeliverSpell(spell);
    }

    // ===================== CONDITION QUERY HELPERS =====================

    /// <summary>Returns true if this character is currently paralyzed (from any source).</summary>
    public bool IsParalyzed()
    {
        if (ActiveGhoulTouchEffect != null && ActiveGhoulTouchEffect.IsParalyzed)
            return true;
        return HasCondition(CombatConditionType.Paralyzed);
    }

    /// <summary>Returns true if this character is currently frightened (from any source).</summary>
    public bool IsFrightened()
    {
        if (ActiveScareEffect != null && ActiveScareEffect.IsFrightened)
            return true;
        return HasCondition(CombatConditionType.Frightened);
    }

    /// <summary>Returns true if this character is currently shaken (from any source).</summary>
    public bool IsShaken()
    {
        if (ActiveScareEffect != null && ActiveScareEffect.IsShaken)
            return true;
        return HasCondition(CombatConditionType.Shaken);
    }

    /// <summary>Returns true if this character is currently sickened (from any source).</summary>
    public bool IsSickened()
    {
        return HasCondition(CombatConditionType.Sickened);
    }

    // ═══════════════════════════════════════════════════════════════
    // NAUSEATED CONDITION (Stinking Cloud, etc.)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Whether this creature is currently nauseated.
    /// Nauseated creatures can only take a single move action per turn.
    /// They cannot attack, cast spells, concentrate, or make AoO.
    /// </summary>
    public bool IsNauseated => HasCondition(CombatConditionType.Nauseated);

    /// <summary>
    /// Apply the nauseated condition for a specified number of rounds.
    /// Use rounds = -1 for indefinite duration (e.g., while in Stinking Cloud).
    /// </summary>
    public void ApplyNauseatedCondition(int rounds, string sourceName = "Nauseated")
    {
        ApplyCondition(CombatConditionType.Nauseated, rounds, sourceName);
    }

    /// <summary>
    /// Remove the nauseated condition.
    /// </summary>
    public void RemoveNauseatedCondition()
    {
        RemoveCondition(CombatConditionType.Nauseated);
    }

    /// <summary>
    /// Apply the blinded condition for a specified number of rounds.
    /// </summary>
    public void ApplyBlindedCondition(int rounds, string sourceName = "Blinded")
    {
        ApplyCondition(CombatConditionType.Blinded, rounds, sourceName);
    }

    /// <summary>
    /// Apply the sickened condition for a specified number of rounds.
    /// </summary>
    public void ApplySickenedCondition(int rounds, string sourceName = "Sickened")
    {
        ApplyCondition(CombatConditionType.Sickened, rounds, sourceName);
    }

    /// <summary>
    /// Apply the engulfed condition (target is inside a creature and takes automatic damage).
    /// </summary>
    public void ApplyEngulfedCondition(CharacterController engulfingCreature, EngulfDefinition engulfDef)
    {
        if (engulfingCreature == null || engulfDef == null)
            return;

        // Track the engulfing creature
        // The actual damage/duration will be handled during monster turns
        // For now, apply a status condition marker
        ApplyCondition(CombatConditionType.Helpless, -1, $"Engulfed by {engulfingCreature.Stats?.CharacterName ?? "creature"}");
        
        Debug.Log($"[Status] {Stats?.CharacterName ?? name} is engulfed by {engulfingCreature.Stats?.CharacterName ?? "creature"}");
        // TODO: Implement ongoing engulf damage tracking
    }

    /// <summary>
    /// Check if creature is in any active Sleet Storm area (for movement/attack modifiers).
    /// </summary>
    public bool IsInSleetStorm => SleetStormAreaEffect.IsCreatureInAnySleetStorm(this);

    /// <summary>
    /// Get the movement speed modifier for Sleet Storm (half speed = 0.5).
    /// Returns 1.0 if not in any sleet storm.
    /// </summary>
    public float GetSleetStormMovementModifier()
    {
        return IsInSleetStorm ? 0.5f : 1.0f;
    }

    /// <summary>
    /// Get the ranged attack penalty from Sleet Storm concealment.
    /// Returns 0 if not in any sleet storm; attacks into/from sleet use concealment miss chance instead.
    /// </summary>
    public int GetSleetStormRangedAttackPenalty()
    {
        // Sleet Storm doesn't impose a flat penalty to ranged attacks per RAW;
        // instead it uses concealment miss chance (20% at 5 ft, 50% beyond).
        // This method returns 0 — the penalty is handled by concealment system.
        return 0;
    }

    /// <summary>
    /// Get the Concentration DC modifier for casting in a Sleet Storm.
    /// Returns 0 if not in any sleet storm.
    /// DC = 5 + level of spell being cast (weather distraction).
    /// </summary>
    public int GetSleetStormConcentrationDC(int spellLevelBeingCast)
    {
        if (!IsInSleetStorm) return 0;
        return SleetStormAreaEffect.GetConcentrationDCModifier(spellLevelBeingCast);
    }

    private void RefreshInvisibilityVisual()
    {
        RefreshInvisibilityVisualForObserver(null);
    }

    public void RefreshInvisibilityVisualForObserver(CharacterController observer)
    {
        if (_sr == null)
            _sr = GetComponent<SpriteRenderer>();

        if (_sr == null)
            return;

        float alpha = 1f;
        if (HasActiveInvisibilityEffect)
        {
            bool observerCanSeeInvisible = observer != null && observer != this && observer.CanSeeInvisible(this);
            alpha = (observerCanSeeInvisible || IsOutlinedByGlitterdust) ? 0.92f : 0.45f;
        }

        Color c = _sr.color;
        c.a = alpha;
        _sr.color = c;

        UpdateGlitterdustVisual();
    }

    private void UpdateGlitterdustVisual(bool forceDisable = false)
    {
        if (_sr == null)
            _sr = GetComponent<SpriteRenderer>();

        if (_sr == null)
            return;

        bool shouldShow = !forceDisable && IsOutlinedByGlitterdust;
        Color c = _sr.color;

        // Keep Glitterdust visuals lightweight and package-independent:
        // we use a subtle golden tint instead of ParticleSystem effects.
        if (shouldShow)
        {
            c.r = 1f;
            c.g = 0.92f;
            c.b = 0.55f;
        }
        else
        {
            c.r = 1f;
            c.g = 1f;
            c.b = 1f;
        }

        _sr.color = c;
    }

    private void LateUpdate()
    {
        bool blurActive = HasActiveBlurEffect;
        if (blurActive != _wasBlurVisualActive)
        {
            _wasBlurVisualActive = blurActive;
            RefreshInvisibilityVisual();
        }

        if (blurActive)
            ApplyBlurVisualPulse();
    }

    private void ApplyBlurVisualPulse()
    {
        if (_sr == null)
            _sr = GetComponent<SpriteRenderer>();

        if (_sr == null)
            return;

        // Lightweight blur indicator: subtle oscillating cool tint to imply wavering outline.
        float pulse = 0.12f + 0.08f * (0.5f + 0.5f * Mathf.Sin((Time.time * 9f) + _blurVisualTimeOffset));
        Color c = _sr.color;
        float targetRed = IsOutlinedByGlitterdust ? 1f : 0.86f;
        float targetGreen = IsOutlinedByGlitterdust ? 0.95f : 0.92f;
        float targetBlue = IsOutlinedByGlitterdust ? 0.7f : 1f;

        c.r = Mathf.Lerp(c.r, targetRed, pulse);
        c.g = Mathf.Lerp(c.g, targetGreen, pulse);
        c.b = Mathf.Lerp(c.b, targetBlue, pulse);
        _sr.color = c;
    }

    public EnfeebledConditionData ApplyEnfeeblementEffect(int strengthPenaltyAmount, int durationRemainingRounds, CharacterController caster)
    {
        int penalty = Mathf.Max(0, strengthPenaltyAmount);
        int rounds = Mathf.Max(0, durationRemainingRounds);
        if (penalty <= 0 || rounds <= 0)
            return null;

        _activeEnfeeblementEffect = new EnfeebledConditionData
        {
            Caster = caster,
            CasterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : string.Empty,
            StrengthPenaltyAmount = penalty,
            RemainingRounds = rounds,
            SourceSpellId = SpellNames.RAY_OF_ENFEEBLEMENT,
            SourceEffectName = "Ray of Enfeeblement"
        };

        RefreshEnfeeblementDerivedState();
        return _activeEnfeeblementEffect;
    }

    public EnfeebledConditionData TickEnfeeblementEffect()
    {
        if (_activeEnfeeblementEffect == null)
            return null;

        _activeEnfeeblementEffect.RemainingRounds = Mathf.Max(0, _activeEnfeeblementEffect.RemainingRounds - 1);
        if (_activeEnfeeblementEffect.RemainingRounds > 0)
            return null;

        EnfeebledConditionData expired = _activeEnfeeblementEffect;
        _activeEnfeeblementEffect = null;
        RefreshEnfeeblementDerivedState();
        return expired;
    }

    public EnfeebledConditionData RemoveEnfeeblementEffect()
    {
        if (_activeEnfeeblementEffect == null)
            return null;

        EnfeebledConditionData removed = _activeEnfeeblementEffect;
        _activeEnfeeblementEffect = null;
        RefreshEnfeeblementDerivedState();
        return removed;
    }

    public void ClearEnfeeblementEffects()
    {
        if (_activeEnfeeblementEffect == null)
            return;

        _activeEnfeeblementEffect = null;
        RefreshEnfeeblementDerivedState();
    }

    public TouchOfIdiocyConditionData ApplyTouchOfIdiocyEffect(
        int intelligenceDamage,
        int wisdomDamage,
        int charismaDamage,
        int durationRemainingRounds,
        CharacterController caster)
    {
        if (Stats == null)
            return null;

        int rounds = Mathf.Max(0, durationRemainingRounds);
        if (rounds <= 0)
            return null;

        int intDamage = Mathf.Max(0, intelligenceDamage);
        int wisDamage = Mathf.Max(0, wisdomDamage);
        int chaDamage = Mathf.Max(0, charismaDamage);
        if (intDamage <= 0 && wisDamage <= 0 && chaDamage <= 0)
            return null;

        // Per D&D 3.5e this spell doesn't stack with itself; refresh by replacing.
        RemoveTouchOfIdiocyEffect();

        _activeTouchOfIdiocyEffect = new TouchOfIdiocyConditionData
        {
            Caster = caster,
            CasterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : string.Empty,
            RemainingRounds = rounds,
            IntelligenceDamage = intDamage,
            WisdomDamage = wisDamage,
            CharismaDamage = chaDamage,
            SourceSpellId = SpellNames.TOUCH_OF_IDIOCY,
            SourceEffectName = "Touch of Idiocy"
        };

        if (intDamage > 0)
            ApplyAbilityDamage(AbilityType.INT, intDamage, "Touch of Idiocy");
        if (wisDamage > 0)
            ApplyAbilityDamage(AbilityType.WIS, wisDamage, "Touch of Idiocy");
        if (chaDamage > 0)
            ApplyAbilityDamage(AbilityType.CHA, chaDamage, "Touch of Idiocy");

        return _activeTouchOfIdiocyEffect;
    }

    public TouchOfIdiocyConditionData TickTouchOfIdiocyEffect()
    {
        if (_activeTouchOfIdiocyEffect == null)
            return null;

        _activeTouchOfIdiocyEffect.RemainingRounds = Mathf.Max(0, _activeTouchOfIdiocyEffect.RemainingRounds - 1);
        if (_activeTouchOfIdiocyEffect.RemainingRounds > 0)
            return null;

        return RemoveTouchOfIdiocyEffect();
    }

    public TouchOfIdiocyConditionData RemoveTouchOfIdiocyEffect()
    {
        if (_activeTouchOfIdiocyEffect == null)
            return null;

        TouchOfIdiocyConditionData removed = _activeTouchOfIdiocyEffect;
        _activeTouchOfIdiocyEffect = null;

        if (removed.IntelligenceDamage > 0)
            HealAbilityDamage(AbilityType.INT, removed.IntelligenceDamage, "Touch of Idiocy expiration");
        if (removed.WisdomDamage > 0)
            HealAbilityDamage(AbilityType.WIS, removed.WisdomDamage, "Touch of Idiocy expiration");
        if (removed.CharismaDamage > 0)
            HealAbilityDamage(AbilityType.CHA, removed.CharismaDamage, "Touch of Idiocy expiration");

        return removed;
    }

    public void ClearTouchOfIdiocyEffect()
    {
        RemoveTouchOfIdiocyEffect();
    }

    public bool IsComatoseOnlyFromTouchOfIdiocyEffect()
    {
        return IsComatoseOnlyFromTouchOfIdiocy();
    }

    private void RefreshEnfeeblementDerivedState()
    {
        int totalPenalty = _activeEnfeeblementEffect != null
            ? Mathf.Max(0, _activeEnfeeblementEffect.StrengthPenaltyAmount)
            : 0;

        Stats?.SetEnfeeblementStrengthPenalty(totalPenalty);

        Inventory inventoryData = GetInventoryData();
        if (inventoryData != null)
            inventoryData.RecalculateStats();
    }

    private void OnValidate()
    {
        NormalizeTeamControlState();
    }

    public string GetArmorTag()
    {
        if (Tags.HasTag("Light Armor")) return "Light Armor";
        if (Tags.HasTag("Medium Armor")) return "Medium Armor";
        if (Tags.HasTag("Heavy Armor")) return "Heavy Armor";
        if (Tags.HasTag("Unarmored")) return "Unarmored";
        return "Unknown";
    }

    public void DebugPrintTags()
    {
        string charName = Stats != null ? Stats.CharacterName : name;
        Debug.Log($"[Tags] {charName}: {Tags.GetTagsDebugString()}");
    }

    private void OnDisable()
    {
        if (_currentScaleAnimation != null)
        {
            StopCoroutine(_currentScaleAnimation);
            _currentScaleAnimation = null;
        }

        StopGrappleAlternatingDisplay(ensureVisible: true);
    }


    private void OnDestroy()
    {
        if (Stats != null)
        {
            Stats.CurrentHPChanged -= OnCurrentHPChanged;
            Stats.NonlethalDamageChanged -= OnNonlethalDamageChanged;
        }

        ClearOwnedFeintWindowsAndIndicators();
        ClearIncomingFeintIndicators();
        ReleaseGrappleState("character removed");
    }
    /// <summary>
    /// Initialize the character with stats and place on grid.
    /// </summary>
    public void Init(CharacterStats stats, Vector2Int startPos, Sprite alive, Sprite dead)
    {
        if (Stats != null)
        {
            Stats.CurrentHPChanged -= OnCurrentHPChanged;
            Stats.NonlethalDamageChanged -= OnNonlethalDamageChanged;
        }

        Stats = stats;
        AliveSprite = alive;
        DeadSprite = dead;
        GridPosition = startPos;

        _activeDiseases.Clear();
        _activePoisons.Clear();

        if (Stats != null)
        {
            Stats.OwnerCharacter = this;
            Stats.CurrentHPChanged += OnCurrentHPChanged;
            Stats.NonlethalDamageChanged += OnNonlethalDamageChanged;
        }

        if (_conditionManager == null)
            _conditionManager = GetComponent<ConditionManager>() ?? gameObject.AddComponent<ConditionManager>();
        _conditionManager.Init(Stats);

        // Every combatant, PC or NPC, tracks the squares of unseen enemies and pinpoints them by Listen at turn start
        // (PerceptionRules.ProcessTurnStart, DMG p.295; CMB-094, batch 12 step 8).
        LastKnownPositionTracker.Ensure(this);

        EnsureConditions();

        SyncHPStateFromCurrentHP(emitLog: false);
        _sr.sprite = (_currentHPState == HPState.Dead && DeadSprite != null) ? DeadSprite : AliveSprite;
        _sr.sortingOrder = 10;

        RefreshGridOccupancy();
        UpdateVisualSize(false);

        ActiveDisguiseSelfEffect = null;
        ActiveExpeditiousRetreatEffect = null;
        ActiveInvisibilityEffect = null;
        ActiveSeeInvisibilityEffect = null;
        ActiveGlitterdustEffect = null;
        ActiveMelfsAcidArrowEffect = null;
        ActiveHasteEffect = null;
        ActiveSlowEffect = null;
        _activeEnfeeblementEffect = null;
        _activeTouchOfIdiocyEffect = null;
        UpdateGlitterdustVisual(forceDisable: true);
        if (Stats != null)
        {
            Stats.SetEnfeeblementStrengthPenalty(0);
            // Land speed and Jump enhancement bonuses are Stats.Bonuses entries owned by their spell effects (removed with them).
            Stats.HasteAttackBonus = 0;
            Stats.HasteACBonus = 0;
            Stats.HasteReflexBonus = 0;
            Stats.SlowAttackPenalty = 0;
            Stats.SlowACPenalty = 0;
            Stats.SlowReflexPenalty = 0;
            Stats.SlowSpeedMultiplier = 1f;
            Stats.FireShieldActive = false;
            Stats.FireShieldIsWarm = false;
            Stats.FireShieldCasterLevel = 0;
            Stats.FireShieldDurationRounds = 0;
            // NOTE: Resilient Sphere is now a stationary area effect (ResilientSphereAreaEffect),
            // not tracked on CharacterStats. No state to clear here.
            Stats.ResetCurrentSizeToBase(); // Ensure size resets on combat end/death
            Stats.ActiveProtectionFromArrowsEffect = null;
            Stats.ActiveStoneskinEffect = null;
            Stats.ActiveDimensionalAnchorEffect = null;
            ActiveAlignmentDetectionEffect = null; // Clear detection effects on combat reset
            if (Stats.ActiveResistEnergyEffects != null)
                Stats.ActiveResistEnergyEffects.Clear();
            if (Stats.ActiveProtectionFromEnergyEffects != null)
                Stats.ActiveProtectionFromEnergyEffects.Clear();
        }
        _displayedRace = ActualRace;
        RefreshInvisibilityVisual();
        RefreshAllTags();
        CheckAbilityScoreZeroEffects();
    }

    private SquareGrid CurrentGrid => GameManager.Instance != null ? GameManager.Instance.Grid : SquareGrid.Instance;

    public int GetVisualSquaresOccupied()
    {
        if (Stats == null) return 1;
        return Stats.CurrentSizeCategory.GetSpaceWidthSquares();
    }

    public List<Vector2Int> GetOccupiedSquaresAt(Vector2Int basePosition)
    {
        SquareGrid grid = CurrentGrid;
        if (grid != null)
            return grid.GetOccupiedSquares(basePosition, GetVisualSquaresOccupied());

        var occupied = new List<Vector2Int>();
        int size = Mathf.Max(1, GetVisualSquaresOccupied());
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                occupied.Add(new Vector2Int(basePosition.x + x, basePosition.y + y));
            }
        }

        return occupied;
    }

    public List<Vector2Int> GetOccupiedSquares()
    {
        return GetOccupiedSquaresAt(GridPosition);
    }

    private static int GetGrappleMovementPriority(Vector2Int moverPos, Vector2Int destination)
    {
        int dx = Mathf.Abs(destination.x - moverPos.x);
        int dy = Mathf.Abs(destination.y - moverPos.y);

        if (dx == 0 && dy == 0) return 0;      // Already on square
        if (dy == 0 && dx > 0) return 1;       // Horizontal preferred
        if (dx == 0 && dy > 0) return 2;       // Vertical next
        return 3;                              // Diagonal / mixed movement fallback
    }

    private Vector2Int FindBestGrapplePosition(CharacterController mover, CharacterController stayer)
    {
        if (mover == null || stayer == null)
            return GridPosition;

        List<Vector2Int> stayerSquares = stayer.GetOccupiedSquares();
        if (stayerSquares == null || stayerSquares.Count == 0)
            return stayer.GridPosition;

        if (mover.GetVisualSquaresOccupied() == stayer.GetVisualSquaresOccupied())
            return stayer.GridPosition;

        Vector2Int moverPos = mover.GridPosition;
        Vector2Int bestSquare = stayerSquares[0];
        int bestPriority = GetGrappleMovementPriority(moverPos, bestSquare);
        int bestDistance = Mathf.Abs(bestSquare.x - moverPos.x) + Mathf.Abs(bestSquare.y - moverPos.y);

        for (int i = 1; i < stayerSquares.Count; i++)
        {
            Vector2Int candidate = stayerSquares[i];
            int candidatePriority = GetGrappleMovementPriority(moverPos, candidate);
            int candidateDistance = Mathf.Abs(candidate.x - moverPos.x) + Mathf.Abs(candidate.y - moverPos.y);

            if (candidatePriority < bestPriority || (candidatePriority == bestPriority && candidateDistance < bestDistance))
            {
                bestSquare = candidate;
                bestPriority = candidatePriority;
                bestDistance = candidateDistance;
            }
        }

        return bestSquare;
    }

    private bool TrySetGrapplePosition(Vector2Int destination, CharacterController overlapAllowedWith)
    {
        Vector2Int oldPosition = GridPosition;

        SquareGrid grid = CurrentGrid;
        if (grid != null)
        {
            List<CharacterController> allowedOverlap = null;
            if (overlapAllowedWith != null)
                allowedOverlap = new List<CharacterController> { overlapAllowedWith };

            bool canPlace = grid.CanPlaceCreature(
                destination,
                GetVisualSquaresOccupied(),
                this,
                ignoreOtherOccupants: false,
                additionalIgnoredOccupants: allowedOverlap);

            if (!canPlace)
                return false;

            grid.ClearCreatureOccupancy(this);
        }

        GridPosition = destination;

        if (grid != null)
            grid.SetCreatureOccupancy(this, GridPosition, GetVisualSquaresOccupied());

        UpdatePositionForSize();

        if (oldPosition != GridPosition)
            GameManager.Instance?.NotifyCharacterMovement(this, oldPosition, GridPosition, "grapple-reposition");

        return true;
    }

    private string PositionGrapplingCharacters(CharacterController initiator, CharacterController target)
    {
        if (initiator == null || target == null)
            return string.Empty;

        // Visual clarity rule for grapples: attempt to move the initiator token into the target square first.
        // If footprint constraints prevent that placement, fall back to size-aware mover/stayer resolution.
        Vector2Int initiatorDestination = FindBestGrapplePosition(initiator, target);
        if (initiator.TrySetGrapplePosition(initiatorDestination, target))
            return $"{(initiator.Stats != null ? initiator.Stats.CharacterName : initiator.name)} moves into {(target.Stats != null ? target.Stats.CharacterName : target.name)}'s space at ({initiatorDestination.x}, {initiatorDestination.y}).";

        CharacterController mover;
        CharacterController stayer;

        int initiatorFootprint = initiator.GetVisualSquaresOccupied();
        int targetFootprint = target.GetVisualSquaresOccupied();

        if (initiatorFootprint > targetFootprint)
        {
            stayer = initiator;
            mover = target;
        }
        else if (targetFootprint > initiatorFootprint)
        {
            stayer = target;
            mover = initiator;
        }
        else
        {
            stayer = initiator;
            mover = target;
        }

        Vector2Int fallbackDestination = FindBestGrapplePosition(mover, stayer);
        bool moved = mover.TrySetGrapplePosition(fallbackDestination, stayer);
        if (!moved)
            return string.Empty;

        string moverName = mover.Stats != null ? mover.Stats.CharacterName : mover.name;
        string stayerName = stayer.Stats != null ? stayer.Stats.CharacterName : stayer.name;
        return $"{moverName} moves into {stayerName}'s space at ({fallbackDestination.x}, {fallbackDestination.y}).";
    }

    private bool CanOccupyAtCurrentSize(Vector2Int basePosition)
    {
        SquareGrid grid = CurrentGrid;
        if (grid == null) return true;
        return grid.CanPlaceCreature(basePosition, GetVisualSquaresOccupied(), this);
    }

    private void RefreshGridOccupancy()
    {
        SquareGrid grid = CurrentGrid;
        if (grid == null) return;

        grid.ClearCreatureOccupancy(this);
        grid.SetCreatureOccupancy(this, GridPosition, GetVisualSquaresOccupied());
    }

    private const float DefaultMoveSecondsPerStep = 0.08f;

    /// <summary>
    /// Move the character to a new square cell instantly.
    /// </summary>
    /// <param name="targetCell">Destination grid cell.</param>
    /// <param name="markAsMoved">Whether this movement should count as normal movement for turn tracking.</param>
    /// <param name="forced">The creature is pushed or dragged by something else (bull rush, overrun, Gust of Wind, a
    /// grappler's drag): GameManager.NotifyCharacterMovement is told it is not the creature's own movement (Mirror Image's
    /// re-confounding, PHB p.254). The movement type the trace sees is unchanged.</param>
    public void MoveToCell(SquareCell targetCell, bool markAsMoved = true, bool forced = false)
    {
        if (targetCell == null || GameManager.Instance == null || GameManager.Instance.Grid == null)
            return;

        Vector2Int oldPosition = GridPosition;
        SquareGrid grid = CurrentGrid;
        Vector2Int targetBasePosition = targetCell.Coords;
        if (grid != null && !grid.CanPlaceCreature(targetBasePosition, GetVisualSquaresOccupied(), this))
            return;

        // Update position and occupancy
        GridPosition = targetBasePosition;
        RefreshGridOccupancy();
        UpdatePositionForSize();

        UpdateInvisibilityMovementState(true);
        if (markAsMoved)
            HasMovedThisTurn = true;

        if (oldPosition != GridPosition)
            GameManager.Instance?.NotifyCharacterMovement(this, oldPosition, GridPosition, markAsMoved ? "move" : "reposition", forced);
    }

    /// <summary>
    /// Smoothly animate movement along a path of grid coordinates.
    /// The path must be ordered and should exclude the current starting square.
    /// Pass <paramref name="lastStepIsDestination"/> false when the path is one segment of a
    /// longer move (the movement AoO helper pauses mid-path), so the segment may end in an
    /// ally's square that the full move only passes through.
    /// </summary>
    public IEnumerator MoveAlongPath(List<Vector2Int> path, float secondsPerStep = DefaultMoveSecondsPerStep, bool markAsMoved = true, bool lastStepIsDestination = true)
    {
        if (path == null || path.Count == 0)
            yield break;

        if (GameManager.Instance == null || GameManager.Instance.Grid == null)
            yield break;

        Vector2Int oldPosition = GridPosition;
        float clampedStepDuration = Mathf.Max(0.01f, secondsPerStep);
        SquareGrid grid = CurrentGrid;

        // Clear the mover's footprint once before animating.
        // During animation the token can pass through ally-occupied squares,
        // so we only re-apply occupancy after movement completes.
        if (grid != null)
            grid.ClearCreatureOccupancy(this);

        for (int i = 0; i < path.Count; i++)
        {
            // ── Incapacitation check BEFORE each movement step ──
            // If damage taken during movement (Wall of Fire pass-through, an AoO) left the creature dead, dying or
            // unconscious, stop immediately. A disabled creature (exactly 0 hp, or below 0 with Diehard) keeps
            // moving: it may still take its move action (PHB p.145; batch 12 D5, AI-053).
            if (Stats != null && ThreatSystem.IsMoverIncapacitated(this))
            {
                Debug.Log($"🔥 [MoveAlongPath] {Stats.CharacterName} cannot act (HP={Stats.CurrentHP}) — movement interrupted at step {i}/{path.Count}");
                break;
            }

            SquareCell nextCell = GameManager.Instance.Grid.GetCell(path[i]);
            if (nextCell == null)
                continue;

            bool isDestinationStep = lastStepIsDestination && (i == path.Count - 1);
            bool allowEnemyOverlap = Stats != null && Stats.IsSwarm;
            if (grid != null && !grid.CanTraversePathNode(
                    nextCell.Coords,
                    GetVisualSquaresOccupied(),
                    this,
                    isDestinationStep,
                    allowThroughAllies: true,
                    allowThroughEnemies: allowEnemyOverlap,
                    allowDestinationEnemyOverlap: allowEnemyOverlap))
                break;

            Vector3 startPos = transform.position;
            Vector3 endPos = grid != null
                ? grid.GetCenteredWorldPosition(nextCell.Coords, GetVisualSquaresOccupied())
                : SquareGridUtils.GridToWorld(nextCell.X, nextCell.Y);

            // Update logical position for the current animation segment.
            // Occupancy is restored once at the end to avoid clobbering allies while passing through.
            GridPosition = nextCell.Coords;

            float elapsed = 0f;
            while (elapsed < clampedStepDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / clampedStepDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                transform.position = Vector3.Lerp(startPos, endPos, smoothT);
                yield return null;
            }

            transform.position = endPos;

            // ── Wall of Ice breach pass-through damage ──
            // Moving through a breached Wall of Ice cell (Line mode only) deals 1d6+CL cold damage.
            // D&D 3.5e: creatures passing through a breached section take cold damage.
            if (WallOfIceAreaEffect.IsCellBreachedWallOfIce(nextCell.Coords))
            {
                WallOfIceAreaEffect.ApplyBreachDamageAtCell(this, nextCell.Coords);
            }

            // ── Death/disable check AFTER each movement step ──
            // Area effects (Wall of Fire, etc.) detect entry via UpdateCharacterTracking()
            // in their Update() method. The yield return null above gives Unity a frame to
            // process those Updates. Check again after the step completes.
            if (Stats != null && ThreatSystem.IsMoverIncapacitated(this))
            {
                Debug.Log($"🔥 [MoveAlongPath] {Stats.CharacterName} dropped after step {i + 1}/{path.Count} at ({GridPosition.x},{GridPosition.y}) (HP={Stats.CurrentHP}) — movement interrupted");
                break;
            }
        }

        RefreshGridOccupancy();
        UpdatePositionForSize();
        UpdateInvisibilityMovementState(true);
        if (markAsMoved)
            HasMovedThisTurn = true;

        if (oldPosition != GridPosition)
            GameManager.Instance?.NotifyCharacterMovement(this, oldPosition, GridPosition, markAsMoved ? "path-move" : "path-reposition");

    }
    /// <summary>
    /// Returns a snapshot list of currently active combat conditions on this character.
    /// </summary>
    public List<StatusEffect> GetActiveConditions()
    {
        return EnsureConditions().GetActiveConditions();
    }

    public List<StatusEffect> GetActiveConditionsDirect()
    {
        if (Stats == null || Stats.ActiveConditions == null)
            return new List<StatusEffect>();

        return new List<StatusEffect>(Stats.ActiveConditions);
    }

    /// <summary>
    /// True if this character currently has the specified combat condition.
    /// </summary>
    public bool HasCondition(CombatConditionType type)
    {
        return EnsureConditions().HasCondition(type);
    }

    public bool HasConditionDirect(CombatConditionType type)
    {
        if (Stats == null) return false;

        if (_conditionManager != null)
            return _conditionManager.HasCondition(type);

        CombatConditionType normalized = ConditionRules.Normalize(type);
        return Stats.ActiveConditions != null
            && Stats.ActiveConditions.Exists(c => ConditionRules.Normalize(c.Type) == normalized);
    }

    public void ApplyCondition(CombatConditionType type, int rounds, string sourceName)
    {
        EnsureConditions().ApplyCondition(type, rounds, sourceName);
    }

    public void ApplyConditionDirect(CombatConditionType type, int rounds, string sourceName)
    {
        if (Stats == null) return;

        if (_conditionManager != null)
            _conditionManager.ApplyCondition(type, rounds, sourceName);
        else
            Stats.ApplyCondition(type, rounds, sourceName);

        EnsureStatusTagManager().UpdateStatusEffectTags(GetActiveConditionsDirect());

        SpellcastingComponent spellComp = Spellcasting;
        spellComp?.ApplyNegativeLevelSlotLoss();
    }

    public bool RemoveCondition(CombatConditionType type)
    {
        return EnsureConditions().RemoveCondition(type);
    }

    public bool RemoveConditionDirect(CombatConditionType type)
    {
        if (Stats == null) return false;

        bool removed = _conditionManager != null
            ? _conditionManager.RemoveCondition(type)
            : Stats.RemoveCondition(type);

        if (removed)
        {
            EnsureStatusTagManager().UpdateStatusEffectTags(GetActiveConditionsDirect());
            SpellcastingComponent spellComp = Spellcasting;
            spellComp?.ApplyNegativeLevelSlotLoss();
        }

        return removed;
    }

    public List<StatusEffect> TickConditions()
    {
        return EnsureConditions().TickConditions();
    }

    public List<StatusEffect> TickConditionsDirect() => TickConditionsDirect(null);

    /// <summary>
    /// Ticks the conditions whose duration anchor passes <paramref name="ticksForAnchor"/> (null: all) and returns the
    /// expired ones (CMB-006).
    /// </summary>
    public List<StatusEffect> TickConditionsDirect(Func<CharacterController, bool> ticksForAnchor)
    {
        if (Stats == null) return new List<StatusEffect>();

        List<StatusEffect> expired = _conditionManager != null
            ? _conditionManager.TickConditions(ticksForAnchor)
            : Stats.TickConditions(ticksForAnchor);

        EnsureStatusTagManager().UpdateStatusEffectTags(GetActiveConditionsDirect());
        SpellcastingComponent spellComp = Spellcasting;
        spellComp?.ApplyNegativeLevelSlotLoss();
        return expired;
    }

    public bool IsProneCondition => EnsureConditions().IsProne;
    public bool IsGrappledCondition => EnsureConditions().IsGrappled;
    public bool IsPinnedCondition => EnsureConditions().IsPinned;
    public bool IsPinningCondition => EnsureConditions().IsPinning;
    public bool IsStunnedCondition => EnsureConditions().IsStunned;
    public bool IsInvisibleCondition => EnsureConditions().IsInvisible;
    public bool IsTurnedCondition => EnsureConditions().IsTurned;
    public bool IsFeintedCondition => EnsureConditions().IsFeinted;
    public bool IsFlatFootedCondition => EnsureConditions().IsFlatFooted;
    public bool IsDexDenied => EnsureConditions().IsDexDenied;
    public int NegativeLevelCount => Stats != null ? Stats.NegativeLevelCount : 0;

    public int ApplyNegativeLevels(int count, string sourceName)
    {
        if (Stats == null || count <= 0)
            return 0;

        string source = string.IsNullOrWhiteSpace(sourceName) ? "Negative Energy" : sourceName;
        for (int i = 0; i < count; i++)
            ApplyCondition(CombatConditionType.EnergyDrained, -1, source);

        Stats.RefreshNegativeLevelState();
        return Stats.NegativeLevelCount;
    }

    public int RemoveNegativeLevels(int count)
    {
        if (Stats == null || count <= 0)
            return 0;

        int removed = 0;
        for (int i = Stats.ActiveConditions.Count - 1; i >= 0 && removed < count; i--)
        {
            StatusEffect effect = Stats.ActiveConditions[i];
            if (effect == null || ConditionRules.Normalize(effect.Type) != CombatConditionType.EnergyDrained)
                continue;

            Stats.ActiveConditions.RemoveAt(i);
            removed++;
        }

        if (removed > 0)
        {
            Stats.RefreshNegativeLevelState();
            EnsureStatusTagManager().UpdateStatusEffectTags(GetActiveConditionsDirect());
            SpellcastingComponent spellComp = Spellcasting;
            spellComp?.ApplyNegativeLevelSlotLoss();
        }

        return removed;
    }

    public void SetProne(bool prone)
    {
        if (prone)
            ApplyCondition(CombatConditionType.Prone, -1, Stats != null ? Stats.CharacterName : "Prone");
        else
            RemoveCondition(CombatConditionType.Prone);
    }

    public void StandUp()
    {
        SetProne(false);
    }

    public void ApplyAbilityDamage(AbilityType ability, int amount, string source = "")
    {
        if (Stats == null || amount <= 0)
            return;

        if (!Stats.HasAbilityScore(ability))
        {
            LogAbilityScoreMessage($"🛡 {Stats.CharacterName} has no {GetAbilityName(ability)} score to damage!");
            return;
        }

        int applied = Stats.ApplyAbilityDamage(ability, amount);
        if (applied <= 0)
            return;

        LogAbilityScoreMessage($"💢 {Stats.CharacterName} takes {applied} {GetAbilityName(ability)} damage{FormatSource(source)}.");
        RecalculateStatsFromAbilityScoreChange();
        CheckAbilityScoreZeroEffects();
    }

    public void ApplyAbilityDrain(AbilityType ability, int amount, string source = "")
    {
        if (Stats == null || amount <= 0)
            return;

        if (!Stats.HasAbilityScore(ability))
        {
            LogAbilityScoreMessage($"🛡 {Stats.CharacterName} has no {GetAbilityName(ability)} score to drain!");
            return;
        }

        int applied = Stats.ApplyAbilityDrain(ability, amount);
        if (applied <= 0)
            return;

        LogAbilityScoreMessage($"🕸 {Stats.CharacterName} suffers {applied} {GetAbilityName(ability)} drain{FormatSource(source)}.");
        RecalculateStatsFromAbilityScoreChange();
        CheckAbilityScoreZeroEffects();
    }

    public int HealAbilityDamage(AbilityType ability, int amount, string source = "Natural healing")
    {
        if (Stats == null || amount <= 0)
            return 0;

        int healed = Stats.HealAbilityDamage(ability, amount);
        if (healed > 0)
        {
            LogAbilityScoreMessage($"💚 {Stats.CharacterName} heals {healed} {GetAbilityName(ability)} damage{FormatSource(source)}.");
            RecalculateStatsFromAbilityScoreChange();
            CheckAbilityScoreZeroEffects();
        }

        return healed;
    }

    public int RemoveAbilityDrain(AbilityType ability, int amount, string source = "Restoration")
    {
        if (Stats == null || amount <= 0)
            return 0;

        int removed = Stats.RemoveAbilityDrain(ability, amount);
        if (removed > 0)
        {
            LogAbilityScoreMessage($"✨ {Stats.CharacterName} recovers {removed} {GetAbilityName(ability)} drain{FormatSource(source)}.");
            RecalculateStatsFromAbilityScoreChange();
            CheckAbilityScoreZeroEffects();
        }

        return removed;
    }

    public void HealAbilityDamageDaily(int amountPerAbility = 1, string source = "Daily recovery")
    {
        if (Stats == null || amountPerAbility <= 0)
            return;

        int totalHealed = Stats.HealAllAbilityDamage(amountPerAbility);
        if (totalHealed <= 0)
            return;

        LogAbilityScoreMessage($"🌅 {Stats.CharacterName} naturally recovers {totalHealed} total ability damage{FormatSource(source)}.");
        RecalculateStatsFromAbilityScoreChange();
        CheckAbilityScoreZeroEffects();
    }

    /// <summary>
    /// Apply disease exposure and resolve the initial infection Fortitude save.
    /// </summary>
    public void ExposeToDisease(DiseaseType diseaseType, int dcModifier = 0)
    {
        DiseaseData disease = DiseaseDatabase.GetDisease(diseaseType);
        if (disease == null || Stats == null)
            return;

        if (Stats.IsImmuneToDisease())
        {
            LogAbilityScoreMessage($"🦠 {Stats.CharacterName} is immune to disease and ignores {disease.Name}.");
            return;
        }

        int dc = Mathf.Max(0, disease.FortitudeDC + dcModifier);
        // The one save roll (SaveRollRules, D13; SPL-018).
        SaveRoll save = SaveRollRules.Roll(Stats, SavingThrowType.Fortitude, dc, SaveContext.Disease(disease.Name), "Disease exposure Fort save");

        LogAbilityScoreMessage($"🦠 {Stats.CharacterName} is exposed to {disease.Name} (Fort DC {dc}).");
        LogAbilityScoreMessage($"   Fortitude: d20({save.Roll}) + {save.Modifier} = {save.Total} {SaveRollRules.Outcome(save)}");

        if (save.Succeeded)
            return;

        _activeDiseases.Add(new ActiveDisease(disease));
        ActiveDisease added = _activeDiseases[_activeDiseases.Count - 1];
        LogAbilityScoreMessage($"⚠ {Stats.CharacterName} contracts {disease.Name}! Incubation: {added.DaysUntilActive} day(s).");
    }

    /// <summary>Marks the once-per-round acid spray as used (true) or available (false) this round.</summary>
    public void SetAcidSprayUsedThisRound(bool used)
    {
        _acidSprayUsedThisRound = used;
    }

    /// <summary>
    /// The creature's turn start (GameManager.BeginCombatantTurn, every creature, acting or not): a new round of its own
    /// begins, so its once-per-round acid spray is available again (MM p.285; D1, CRE-040). The Cleave flag joins it
    /// later in batch 12.
    /// </summary>
    public void ClearOncePerRoundFlags()
    {
        _acidSprayUsedThisRound = false;
    }

    /// <summary>
    /// The creature's turn start (GameManager.BeginCombatantTurn, every creature, acting or not; decision D3): a round of
    /// each recharge passes, the breath weapon's, the ranged special's and the terrain manipulation's (AI-032).
    /// </summary>
    public void TickTurnCooldowns()
    {
        TickBreathWeaponCooldown();
        TickRangedSpecialAttackCooldown();
        TickTerrainManipulationDuration();
    }

    /// <summary>
    /// The rounds before a recharging ability is usable again: <paramref name="rechargeDice"/> (the MM's "1d4" rounds) rolled
    /// when it is set, else the fixed <paramref name="fixedRounds"/> ("once every 10 rounds"). Each die passes the scenario
    /// roll filter with the context <paramref name="context"/> (CRE-073, D14).
    /// </summary>
    public static int RollRechargeRounds(string rechargeDice, int fixedRounds, string context)
    {
        if (!string.IsNullOrEmpty(rechargeDice) && DiceExpression.TryParse(rechargeDice, out DiceExpression dice))
        {
            int rolled = dice.IsFixed
                ? dice.Modifier
                : DiceService.RollMultiple(dice.NumDice, dice.DiceSides, context) + dice.Modifier;
            return Mathf.Max(0, rolled);
        }
        return Mathf.Max(0, fixedRounds);
    }

    public void ConfigureRegeneration(int amountPerRound, DamageBypassTag suppressedBy)
    {
        _regenerationAmountPerRound = Mathf.Max(0, amountPerRound);
        _regenerationSuppressedBy = suppressedBy;
        _regenerationSuppressedRoundsRemaining = 0;
    }

    /// <summary>Configure incorporeal trait (Shadow, Wraith, Allip).</summary>
    public void ConfigureIncorporeal(bool isIncorporeal)
    {
        _isIncorporeal = isIncorporeal;
    }

    /// <summary>Configure breath weapon (Hell Hound, dragons).</summary>
    public void ConfigureBreathWeapon(BreathWeaponDefinition bw)
    {
        _breathWeapon = bw?.Clone();
        _breathWeaponCooldownRemaining = 0;
        _breathWeaponUsesToday = 0;
    }

    /// <summary>Configure secondary breath weapon (metallic dragons: paralysis gas, sleep gas, etc.).</summary>
    public void ConfigureSecondaryBreathWeapon(SecondaryBreathWeaponDefinition sbw)
    {
        _secondaryBreathWeapon = sbw?.Clone();
    }

    /// <summary>Configure frightful presence (Young Adult+ dragons).</summary>
    public void ConfigureFrightfulPresence(FrightfulPresenceDefinition fp)
    {
        _frightfulPresence = fp?.Clone();
        _hasFrightfulPresenceTriggered = false;
    }

    /// <summary>
    /// Use the secondary breath weapon: it starts the one recharge both breaths share (MM p.69: 1d4 rounds; CRE-073).
    /// Returns the definition for resolution.
    /// </summary>
    public SecondaryBreathWeaponDefinition UseSecondaryBreathWeapon()
    {
        if (!IsSecondaryBreathWeaponReady)
            return null;
        _breathWeaponCooldownRemaining = RollRechargeRounds(_breathWeapon != null ? _breathWeapon.RechargeDice : "1d4",
            _breathWeapon != null ? _breathWeapon.RechargeRounds : 0, $"{Stats?.CharacterName} breath weapon recharge");
        return _secondaryBreathWeapon;
    }

    /// <summary>Configure engulf ability (Gelatinous Cube, Cloaker).</summary>
    public void ConfigureEngulf(EngulfDefinition engulf)
    {
        _engulf = engulf?.Clone();
    }

    /// <summary>Configure stench aura (Troglodyte).</summary>
    public void ConfigureStenchAura(int dc, int rangeFeet)
    {
        _stenchAuraDC = dc;
        _stenchAuraRange = rangeFeet;
    }

    /// <summary>Configure supernatural aura (Allip babble, Cloaker moan).</summary>
    public void ConfigureAuraAbility(AuraAbilityDefinition aura)
    {
        _auraAbility = aura?.Clone();
    }

    /// <summary>Configure ranged special attack (Spittle, Web, Acid Spray).</summary>
    public void ConfigureRangedSpecialAttack(RangedSpecialAttackDefinition rangedAttack)
    {
        _rangedSpecialAttack = rangedAttack?.Clone();
        _rangedSpecialAttackCooldownRounds = 0;
    }

    /// <summary>Configure blood drain ability (triggered while grappling).</summary>
    public void ConfigureBloodDrain(BloodDrainDefinition bloodDrain)
    {
        _bloodDrain = bloodDrain?.Clone();
    }

    /// <summary>Configure terrain manipulation ability (Ground Manipulation bog, Caltrops).</summary>
    public void ConfigureTerrainManipulation(TerrainManipulationDefinition terrainManip)
    {
        _terrainManipulation = terrainManip?.Clone();
        _terrainManipulationDurationRemaining = terrainManip?.DurationRounds ?? 0;
    }

    // ========== SLOT REUSE (CRE-046) ==========

    /// <summary>
    /// Components a freshly built character GameObject carries (SceneBootstrap and Awake add them) and that
    /// <see cref="ResetForNewCreature"/> keeps, re-initialised by the spawn. Every other component (spellcasting,
    /// spell effects, concentration, True Strike, ring regeneration, summon visuals and any behaviour a later system
    /// attaches) belongs to the creature that held the controller and is destroyed. InventoryComponent is kept because
    /// every spawn path re-initialises it with a new Inventory (and reuses the existing component rather than adding a
    /// second one); LastKnownPositionTracker is kept and cleared (every combatant has one from Init, CMB-094).
    /// </summary>
    private static readonly HashSet<Type> SlotReuseKeptComponentTypes = new HashSet<Type>
    {
        typeof(Transform),
        typeof(SpriteRenderer),
        typeof(CharacterController),
        typeof(CharacterCombatStats),
        typeof(CharacterEquipment),
        typeof(CharacterInventory),
        typeof(CharacterConditions),
        typeof(ConditionManager),
        typeof(StatusEffectIndicator),
        typeof(InventoryComponent),
        // Every combatant has one from Init (CMB-094); ResetForNewCreature clears it instead of destroying it.
        typeof(LastKnownPositionTracker),
    };

    /// <summary>
    /// Returns this controller to the state of a freshly created one before a new creature is initialised in it.
    /// The enemy pool that SceneBootstrap builds is reused by every encounter, and before CRE-046 a slot kept the
    /// previous creature's traits (an allip's incorporeality and Babble aura, a breath weapon, a stench aura, ...).
    /// Called by GameManager.ResetCharacterSlotForSpawn, which every spawn path goes through: InitializeNPCFromDefinition
    /// (encounters, test presets, the scenario harness and summons) and, for a party slot that gets a different
    /// character, GameManager.ResetPCSlotForNewCharacter (character creation, the Configure*TestParty presets and the
    /// scenario harness). Clears: the grapple link and pin state (the
    /// opponent is released too), a mount, Command Undead links both ways, owned feint windows, every spell-effect
    /// record on the controller, readied counterspell, turn and action-economy state, the attack pool and AI
    /// maneuver memory, feat toggles, the attack damage mode, ability-zero bookkeeping, diseases and poisons, every
    /// innate monster ability (regeneration, incorporeal, breath weapons, frightful presence, engulf, stench, aura,
    /// ranged special attack, blood drain, terrain manipulation, acid spray cooldown), last-known
    /// positions, the summoned flag, HP state, AI profile and target priority, displayed race, tags, shield-bash AC suppression, sprite
    /// tint and visibility, running coroutines, and every component outside <see cref="SlotReuseKeptComponentTypes"/>
    /// (concentration is ended first, so an effect it holds on another creature ends). Stats stay until the spawn's
    /// Init replaces them; links other characters and services keep to this controller are cleared by the GameManager
    /// caller. A no-op in effect on a new controller.
    /// </summary>
    public void ResetForNewCreature(string reason = "slot reused")
    {
        StopAllCoroutines();
        StrenuousDamagePending = false;
        _currentScaleAnimation = null;
        _grappleAlternateVisibilityCoroutine = null;

        // Links to other creatures (resolved while the old stats are still bound).
        ReleaseGrappleState(reason);
        _isPinningOpponent = false;
        _pinnedOpponent = null;
        _pinnedBy = null;
        _grappleDisplayPauseLocks = 0;

        if (MountSystem.IsMounted(this))
            MountSystem.ForceDismount(this, allowSoftFall: false);

        RemoveCommandUndeadEffect();
        if (_commandedUndeadList.Count > 0)
        {
            var commanded = new List<CommandUndeadEffectData>(_commandedUndeadList);
            for (int i = 0; i < commanded.Count; i++)
            {
                CharacterController undead = commanded[i] != null ? commanded[i].ControlledUndead : null;
                if (undead != null && undead != this && undead.ActiveCommandUndeadEffect == commanded[i])
                    undead.RemoveCommandUndeadEffect();
            }
            _commandedUndeadList.Clear();
        }

        ClearOwnedFeintWindowsAndIndicators();
        _activeFeintWindows.Clear();
        _incomingFeintSources.Clear();
        _turnsStartedCount = 0;
        CurrentTurnStart.Clear();
        _lastKnownTargetPositions.Clear();
        WasSummoned = false;

        // Spell-effect records kept on the controller (their stat changes went to the old stats).
        MirrorImageFigures = null;
        ActiveDisguiseSelfEffect = null;
        ActiveExpeditiousRetreatEffect = null;
        ActiveInvisibilityEffect = null;
        ActiveSeeInvisibilityEffect = null;
        ActiveGlitterdustEffect = null;
        ActiveMelfsAcidArrowEffect = null;
        ActiveBlindnessDeafnessEffect = null;
        ActiveHasteEffect = null;
        ActiveSlowEffect = null;
        ActiveCommandUndeadEffect = null;
        ActiveFalseLifeEffect = null;
        ActiveGhoulTouchEffect = null;
        ActiveScareEffect = null;
        ActiveSpectralHandEffect = null;
        ActiveAlignmentDetectionEffect = null;
        _activeAttributeEnhancements.Clear();
        _activeEnfeeblementEffect = null;
        _activeTouchOfIdiocyEffect = null;
        if (ReadiedCounterspell != null)
        {
            ReadiedCounterspell.Clear();
            ReadiedCounterspell = null;
        }

        // Turn state, action economy, attack sequence and AI memory.
        HasMovedThisTurn = false;
        HasTakenFiveFootStep = false;
        HasAttackedThisTurn = false;
        IsWithdrawing = false;
        Actions.Reset();
        ProgressiveAttackPool.Clear();
        AIManeuverMemory.Clear();
        ThreatSystem.ClearMovementOpportunities(this);

        // Feat toggles and the attack damage mode.
        PowerAttackValue = 0;
        RapidShotEnabled = false;
        IsFightingDefensively = false;
        CurrentAttackDamageMode = AttackDamageMode.Lethal;
        _attackDamageModeManuallySetThisRound = false;

        _abilityZeroAppliedHelpless = false;
        _abilityZeroAppliedUnconscious = false;
        _activeDiseases.Clear();
        _activePoisons.Clear();

        // Innate monster abilities: every one back to "none" (the spawn configures the new creature's own).
        SetAcidSprayUsedThisRound(false);
        ConfigureRegeneration(0, DamageBypassTag.None);
        ConfigureIncorporeal(false);
        ConfigureBreathWeapon(null);
        ConfigureSecondaryBreathWeapon(null);
        ConfigureFrightfulPresence(null);
        ConfigureEngulf(null);
        ConfigureStenchAura(0, 0);
        ConfigureAuraAbility(null);
        ConfigureRangedSpecialAttack(null);
        ConfigureBloodDrain(null);
        ConfigureTerrainManipulation(null);

        _hasProcessedDeath = false;
        _currentHPState = HPState.Healthy;

        aiProfile = null;
        EnemyUseCoupDeGraceOverride = null;
        PriorityTargetName = null;
        _displayedRace = null;
        _wasBlurVisualActive = false;

        EnsureTags().ClearAllTags();
        _statusTagManager = new StatusTagManager(this);
        EnsureEquipment().ResetShieldBashState();

        if (_sr == null)
            _sr = GetComponent<SpriteRenderer>();
        if (_sr != null)
        {
            _sr.enabled = true;
            _sr.color = Color.white;
        }

        // Components that belong to the old creature. Concentration is ended first so that an effect it keeps on
        // another creature ends with it; the destroyed spell-effect manager takes the old creature's effects along.
        ConcentrationManager concentration = GetComponent<ConcentrationManager>();
        if (concentration != null && concentration.IsConcentrating)
            concentration.EndConcentration(silent: true);

        Component[] components = GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];
            if (c == null || SlotReuseKeptComponentTypes.Contains(c.GetType()))
                continue;
            DestroyImmediate(c);
        }

        _statusEffectManager = null;
        _spellcastingComponent = null;
        _concentrationManager = null;

        // The old creature's last-known squares, pinpoints and auto-miss counts (the component stays, CMB-094).
        LastKnownPositionTracker tracker = GetComponent<LastKnownPositionTracker>();
        if (tracker != null)
            tracker.ClearAllPositions();
    }

    /// <summary>
    /// Drops what this character remembers about <paramref name="other"/>: its feint window against it, the feint
    /// marker it set here, a counterspell readied against it, and its last-known square (also in its
    /// LastKnownPositionTracker). Used when
    /// <paramref name="other"/>'s controller is about to hold a different creature (CRE-046).
    /// </summary>
    public void ForgetCreature(CharacterController other)
    {
        if (other == null || other == this)
            return;

        for (int i = _activeFeintWindows.Count - 1; i >= 0; i--)
        {
            FeintWindow window = _activeFeintWindows[i];
            if (window == null || window.Target == other)
                _activeFeintWindows.RemoveAt(i);
        }

        if (_incomingFeintSources.Remove(other) && _incomingFeintSources.Count == 0 && Stats != null)
            RemoveCondition(CombatConditionType.Feinted);

        _lastKnownTargetPositions.Remove(other);

        // The old creature's knowledge of which of this creature's Mirror Image figures is real (PHB p.254).
        MirrorImageRules.Forget(this, other);

        // A counterspell readied against the old creature must not fire against the new one in the same controller.
        if (ReadiedCounterspell != null && ReadiedCounterspell.WatchedCaster == other)
            ClearReadiedCounterspell();

        LastKnownPositionTracker tracker = GetComponent<LastKnownPositionTracker>();
        if (tracker != null)
            tracker.ForgetTarget(other);
    }

    /// <summary>Tick ranged special attack cooldown at start of turn.</summary>
    public void TickRangedSpecialAttackCooldown()
    {
        if (_rangedSpecialAttackCooldownRounds > 0)
            _rangedSpecialAttackCooldownRounds--;
    }

    /// <summary>Apply the ranged special attack's recharge after use: its recharge dice when set, else its fixed rounds (CRE-073).</summary>
    public void ApplyRangedSpecialAttackCooldown()
    {
        if (_rangedSpecialAttack == null)
            return;
        int rounds = RollRechargeRounds(_rangedSpecialAttack.RechargeDice, _rangedSpecialAttack.CooldownRounds,
            $"{Stats?.CharacterName} {_rangedSpecialAttack.Name} recharge");
        if (rounds > 0)
            _rangedSpecialAttackCooldownRounds = rounds;
    }

    /// <summary>Tick terrain manipulation duration (for non-caster-following effects).</summary>
    public void TickTerrainManipulationDuration()
    {
        if (_terrainManipulation != null && _terrainManipulationDurationRemaining > 0)
            _terrainManipulationDurationRemaining--;
    }

    /// <summary>Check if terrain manipulation is still active.</summary>
    public bool IsTerrainManipulationActive()
    {
        return _terrainManipulation != null && (_terrainManipulation.FollowsCaster || _terrainManipulationDurationRemaining > 0);
    }

    /// <summary>Tick breath weapon cooldown at start of turn.</summary>
    public void TickBreathWeaponCooldown()
    {
        if (_breathWeaponCooldownRemaining > 0)
            _breathWeaponCooldownRemaining--;
    }

    /// <summary>
    /// Use the breath weapon: it recharges after its MM recharge, rolled (<see cref="BreathWeaponDefinition.RechargeDice"/>,
    /// "1d4" rounds) or fixed (<see cref="BreathWeaponDefinition.RechargeRounds"/>), and spends one use of a daily limit
    /// (D14, CRE-073). Its rounds pass at this creature's turn start (<see cref="TickTurnCooldowns"/>), so a breath that
    /// recharges in 1 round is ready on the next turn. Returns the definition for resolution, or null when not ready.
    /// </summary>
    public BreathWeaponDefinition UseBreathWeapon()
    {
        if (_breathWeapon == null || _breathWeaponCooldownRemaining > 0)
            return null;
        if (_breathWeapon.UsesPerDay > 0 && _breathWeaponUsesToday >= _breathWeapon.UsesPerDay)
            return null;
        _breathWeaponCooldownRemaining = RollRechargeRounds(_breathWeapon.RechargeDice, _breathWeapon.RechargeRounds,
            $"{Stats?.CharacterName} breath weapon recharge");
        _breathWeaponUsesToday++;
        return _breathWeapon;
    }

    public void ApplyRegenerationAtTurnStart()
    {
        if (_regenerationAmountPerRound <= 0 || Stats == null || Stats.CurrentHP <= -10)
            return;

        if (_regenerationSuppressedRoundsRemaining > 0)
        {
            _regenerationSuppressedRoundsRemaining--;
            return;
        }

        int hpBefore = Stats.CurrentHP;
        int nonlethalBefore = Stats.NonlethalDamage;
        int healedHp = Stats.HealDamage(_regenerationAmountPerRound, out int nonlethalHealed);

        if (healedHp > 0 || nonlethalHealed > 0)
        {
            LogAbilityScoreMessage($"♻ {Stats.CharacterName} regenerates {healedHp} HP and removes {nonlethalHealed} nonlethal.");
            if (GameManager.Instance != null && GameManager.Instance.CombatUI != null)
                GameManager.Instance.CombatUI.ShowCombatLog(CombatLogHelper.Success("♻", $"{Stats.CharacterName} regenerates ({hpBefore}→{Stats.CurrentHP} HP, nonlethal {nonlethalBefore}→{Stats.NonlethalDamage})."));
        }
    }

    public void NotifyIncomingDamage(DamagePacket packet, int finalDamage)
    {
        if (packet == null)
            return;

        if (finalDamage > 0 && packet.Types != null && packet.Types.Contains(DamageType.Fire) && GameManager.Instance != null)
            GameManager.Instance.NotifyFireDamageAtPosition(GridPosition, packet.SourceName);

        if (finalDamage <= 0 || _regenerationAmountPerRound <= 0)
            return;

        if (_regenerationSuppressedBy != DamageBypassTag.None
            && (packet.AttackTags & _regenerationSuppressedBy) != 0)
        {
            _regenerationSuppressedRoundsRemaining = Mathf.Max(_regenerationSuppressedRoundsRemaining, 1);
        }
    }

    private bool IsImmuneToPoison()
    {
        return Stats != null && Stats.IsImmuneToPoison();
    }

    /// <summary>
    /// Apply poison exposure and resolve initial poison save/damage.
    /// Secondary damage is handled after a delay by GameManager poison ticking.
    /// </summary>
    public void ApplyPoison(string poisonId, int dcModifier = 0)
    {
        PoisonData poison = PoisonDatabase.GetPoison(poisonId);
        if (poison == null || Stats == null)
            return;

        if (IsImmuneToPoison())
        {
            LogAbilityScoreMessage($"☠ {Stats.CharacterName} is immune to poison and ignores {poison.Name}.");
            return;
        }

        int dc = Mathf.Max(0, poison.FortitudeDC + dcModifier);
        // The one save roll (SaveRollRules, D13), with the poison context (CHR-018).
        SaveRoll save = SaveRollRules.Roll(Stats, SavingThrowType.Fortitude, dc, SaveContext.Poison(poison.Id ?? poison.Name), "Poison initial Fort save");

        LogAbilityScoreMessage($"☠ {Stats.CharacterName} is exposed to {poison.Name} ({poison.Type}, Fort DC {dc}).");
        LogAbilityScoreMessage($"   Initial save: d20({save.Roll}) + {save.Modifier} = {save.Total} {SaveRollRules.Outcome(save)}");

        ActivePoison activePoison = new ActivePoison(poison)
        {
            InitialSaveSucceeded = save.Succeeded
        };

        if (!save.Succeeded)
        {
            ApplyAbilityEffectList(poison.InitialDamage, $"{poison.Name} (initial)");
            ApplySpecialEffectList(poison.InitialSpecialEffects, poison.Name, applyForInitial: true, applyForSecondary: false);
        }

        _activePoisons.Add(activePoison);
    }

    /// <summary>
    /// Called by GameManager when the poison's secondary timer expires.
    /// </summary>
    public void ProcessPoisonSecondaryDamage(ActivePoison poison)
    {
        if (poison == null || poison.PoisonData == null || poison.SecondaryResolved || Stats == null)
            return;

        if (Stats.IsImmuneToPoison())
        {
            LogAbilityScoreMessage($"☣ {Stats.CharacterName} is immune to poison; secondary effect from {poison.PoisonData.Name} is negated.");
            poison.SecondaryResolved = true;
            return;
        }

        int dc = poison.PoisonData.FortitudeDC;
        // The one save roll (SaveRollRules, D13), with the poison context (CHR-018).
        SaveRoll save = SaveRollRules.Roll(Stats, SavingThrowType.Fortitude, dc,
            SaveContext.Poison(poison.PoisonData.Id ?? poison.PoisonData.Name), "Poison secondary Fort save");

        LogAbilityScoreMessage($"☣ {Stats.CharacterName} makes secondary save vs {poison.PoisonData.Name} (DC {dc}).");
        LogAbilityScoreMessage($"   Secondary save: d20({save.Roll}) + {save.Modifier} = {save.Total} {SaveRollRules.Outcome(save)}");

        poison.SecondarySaveSucceeded = save.Succeeded;
        if (!poison.SecondarySaveSucceeded)
        {
            ApplyAbilityEffectList(poison.PoisonData.SecondaryDamage, $"{poison.PoisonData.Name} (secondary)");
            ApplySpecialEffectList(poison.PoisonData.SecondarySpecialEffects, poison.PoisonData.Name, applyForInitial: false, applyForSecondary: true);
        }

        poison.SecondaryResolved = true;
    }

    /// <summary>
    /// Called once per in-game day to progress incubation and active disease damage.
    /// </summary>
    public void ProcessDiseaseEffectsDaily()
    {
        if (Stats == null || _activeDiseases.Count == 0)
            return;

        for (int i = _activeDiseases.Count - 1; i >= 0; i--)
        {
            ActiveDisease active = _activeDiseases[i];
            if (active == null || active.DiseaseData == null)
            {
                _activeDiseases.RemoveAt(i);
                continue;
            }

            if (active.IsIncubating)
            {
                active.DaysUntilActive--;
                if (active.DaysUntilActive <= 0)
                {
                    active.IsIncubating = false;
                    active.DaysUntilActive = 0;
                    LogAbilityScoreMessage($"🧫 {Stats.CharacterName}'s {active.DiseaseData.Name} incubation ends.");
                }
                else
                {
                    LogAbilityScoreMessage($"🧫 {active.DiseaseData.Name} incubating ({active.DaysUntilActive} day(s) remaining).");
                }

                continue;
            }

            int dc = active.DiseaseData.FortitudeDC;
            // The one save roll (SaveRollRules, D13; SPL-018).
            SaveRoll save = SaveRollRules.Roll(Stats, SavingThrowType.Fortitude, dc, SaveContext.Disease(active.DiseaseData.Name), "Disease daily Fort save");
            bool success = save.Succeeded;

            LogAbilityScoreMessage($"🦠 Daily save vs {active.DiseaseData.Name}: d20({save.Roll}) + {save.Modifier} = {save.Total} vs DC {dc} {SaveRollRules.Outcome(save)}");

            if (success)
            {
                active.ConsecutiveSuccessfulSaves++;
                if (active.ConsecutiveSuccessfulSaves >= 2)
                {
                    LogAbilityScoreMessage($"✅ {Stats.CharacterName} recovers from {active.DiseaseData.Name}.");
                    _activeDiseases.RemoveAt(i);
                }
            }
            else
            {
                active.ConsecutiveSuccessfulSaves = 0;
                ApplyAbilityEffectList(active.DiseaseData.DamageEffects, active.DiseaseData.Name);
                ApplySpecialEffectList(active.DiseaseData.SpecialEffects, active.DiseaseData.Name, applyForInitial: true, applyForSecondary: true);
            }
        }
    }

    public string GetActiveDiseaseSummary()
    {
        if (_activeDiseases == null || _activeDiseases.Count == 0)
            return string.Empty;

        List<string> parts = new List<string>();
        for (int i = 0; i < _activeDiseases.Count; i++)
        {
            ActiveDisease disease = _activeDiseases[i];
            if (disease != null)
                parts.Add(disease.GetStatusSummary());
        }

        return string.Join("; ", parts);
    }

    public string GetActivePoisonSummary()
    {
        if (_activePoisons == null || _activePoisons.Count == 0)
            return string.Empty;

        List<string> parts = new List<string>();
        for (int i = 0; i < _activePoisons.Count; i++)
        {
            ActivePoison poison = _activePoisons[i];
            if (poison != null && !poison.SecondaryResolved)
                parts.Add(poison.GetStatusSummary());
        }

        return string.Join("; ", parts);
    }

    private void ApplySpecialEffectList(
        List<PoisonSpecialEffect> effects,
        string source,
        bool applyForInitial,
        bool applyForSecondary)
    {
        if (effects == null || effects.Count == 0 || Stats == null)
            return;

        for (int i = 0; i < effects.Count; i++)
        {
            PoisonSpecialEffect effect = effects[i];
            if (effect == null || effect.EffectType == PoisonEffectType.None)
                continue;

            bool shouldApply = true;
            if (applyForInitial && !applyForSecondary)
                shouldApply = effect.AppliesToInitial || !effect.AppliesToSecondary;
            else if (applyForSecondary && !applyForInitial)
                shouldApply = effect.AppliesToSecondary || !effect.AppliesToInitial;

            if (!shouldApply)
                continue;

            ApplySpecialEffect(effect, source);
        }
    }

    private void ApplySpecialEffect(PoisonSpecialEffect effect, string source)
    {
        if (effect == null || Stats == null)
            return;

        string sourceName = string.IsNullOrWhiteSpace(source) ? "Poison/Disease" : source;

        if (effect.EffectType == PoisonEffectType.Death)
        {
            LogAbilityScoreMessage($"☠ {Stats.CharacterName} dies from {sourceName}.");
            Stats.CurrentHP = Mathf.Min(Stats.CurrentHP, -10);
            SyncHPStateFromCurrentHP(emitLog: true);
            return;
        }

        int rounds = effect.RollDurationInRounds();
        CombatConditionType conditionType = effect.ToConditionType();
        if (conditionType != CombatConditionType.None)
        {
            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                gm.ApplyCondition(
                    this,
                    conditionType,
                    rounds,
                    source: null,
                    data: null,
                    sourceNameOverride: sourceName,
                    sourceCategory: "Poison");

                if (conditionType == CombatConditionType.Paralyzed || conditionType == CombatConditionType.Unconscious)
                {
                    gm.ApplyCondition(
                        this,
                        CombatConditionType.Helpless,
                        rounds,
                        source: null,
                        data: null,
                        sourceNameOverride: sourceName,
                        sourceCategory: "Poison");
                }
            }
            else
            {
                ApplyCondition(conditionType, rounds, sourceName);

                if (conditionType == CombatConditionType.Paralyzed || conditionType == CombatConditionType.Unconscious)
                    ApplyCondition(CombatConditionType.Helpless, rounds, sourceName);
            }

            string durationLabel = rounds < 0 ? "permanent" : $"{rounds} rounds";
            LogAbilityScoreMessage($"⚠ {Stats.CharacterName} is {ConditionRules.GetDefinition(conditionType).DisplayName.ToLowerInvariant()} from {sourceName} ({durationLabel}).");
        }
    }

    private void ApplyAbilityEffectList(List<AbilityDamageEffect> effects, string source)
    {
        if (effects == null || effects.Count == 0)
            return;

        for (int i = 0; i < effects.Count; i++)
        {
            AbilityDamageEffect effect = effects[i];
            if (effect == null)
                continue;

            int amount = effect.RollDamage();
            if (amount <= 0)
                continue;

            if (effect.IsDrain)
                ApplyAbilityDrain(effect.Ability, amount, source);
            else
                ApplyAbilityDamage(effect.Ability, amount, source);
        }
    }

    public void CheckAbilityScoreZeroEffects()
    {
        if (Stats == null)
            return;

        bool constitutionZero = Stats.IsAbilityReducedToZero(AbilityType.CON);
        bool shouldBeHelpless = Stats.IsHelplessFromAbilityScore();
        bool shouldBeUnconscious = Stats.IsComatoseFromAbilityScore();

        // Touch of Idiocy special-case: mental scores reduced to 0 by this spell do NOT cause helplessness/unconsciousness.
        if (shouldBeHelpless && IsHelplessOnlyFromTouchOfIdiocyMentalZero())
            shouldBeHelpless = false;

        if (shouldBeUnconscious && IsComatoseOnlyFromTouchOfIdiocy())
            shouldBeUnconscious = false;

        if (shouldBeHelpless && !_abilityZeroAppliedHelpless)
        {
            _abilityZeroAppliedHelpless = true;
            ApplyCondition(CombatConditionType.Helpless, -1, "Ability Score 0");
            LogAbilityScoreMessage($"⚠ {Stats.CharacterName} becomes helpless (reduced ability score reached 0).");
        }
        else if (!shouldBeHelpless && _abilityZeroAppliedHelpless)
        {
            _abilityZeroAppliedHelpless = false;
            RemoveCondition(CombatConditionType.Helpless);
            LogAbilityScoreMessage($"✅ {Stats.CharacterName} is no longer helpless from ability score loss.");
        }

        if (shouldBeUnconscious && !_abilityZeroAppliedUnconscious)
        {
            _abilityZeroAppliedUnconscious = true;
            ApplyCondition(CombatConditionType.Unconscious, -1, "Ability Score 0");
            LogAbilityScoreMessage($"💤 {Stats.CharacterName} becomes comatose (mental ability reduced to 0).");
        }
        else if (!shouldBeUnconscious && _abilityZeroAppliedUnconscious)
        {
            _abilityZeroAppliedUnconscious = false;
            if (_currentHPState != HPState.Unconscious && _currentHPState != HPState.Dying && _currentHPState != HPState.Stable && _currentHPState != HPState.Dead)
                RemoveCondition(CombatConditionType.Unconscious);
            LogAbilityScoreMessage($"✅ {Stats.CharacterName} recovers from comatose ability-score loss.");
        }

        if (constitutionZero && _currentHPState != HPState.Dead)
        {
            LogAbilityScoreMessage($"☠ {Stats.CharacterName} dies (Constitution reduced to 0).");
            Stats.CurrentHP = Mathf.Min(Stats.CurrentHP, -10);
            SyncHPStateFromCurrentHP(emitLog: true);
        }
    }

    private bool IsHelplessOnlyFromTouchOfIdiocyMentalZero()
    {
        if (Stats == null || _activeTouchOfIdiocyEffect == null)
            return false;

        // STR/DEX at 0 still causes helplessness normally.
        if (Stats.IsAbilityReducedToZero(AbilityType.STR) || Stats.IsAbilityReducedToZero(AbilityType.DEX))
            return false;

        bool mentalZero = Stats.IsAbilityReducedToZero(AbilityType.INT)
            || Stats.IsAbilityReducedToZero(AbilityType.WIS)
            || Stats.IsAbilityReducedToZero(AbilityType.CHA);
        if (!mentalZero)
            return false;

        return IsComatoseOnlyFromTouchOfIdiocy();
    }

    private bool IsComatoseOnlyFromTouchOfIdiocy()
    {
        if (Stats == null || _activeTouchOfIdiocyEffect == null)
            return false;

        return !WouldStillBeComatoseWithoutTouchOfIdiocyDamage(AbilityType.INT)
            && !WouldStillBeComatoseWithoutTouchOfIdiocyDamage(AbilityType.WIS)
            && !WouldStillBeComatoseWithoutTouchOfIdiocyDamage(AbilityType.CHA);
    }

    private bool WouldStillBeComatoseWithoutTouchOfIdiocyDamage(AbilityType ability)
    {
        if (Stats == null || !Stats.HasAbilityScore(ability))
            return false;

        if (!Stats.IsAbilityReducedToZero(ability))
            return false;

        int touchDamage = GetTouchOfIdiocyDamageForAbility(ability);
        if (touchDamage <= 0)
            return true;

        int baseScore = Stats.GetBaseAbilityScore(ability);
        if (baseScore == CharacterStats.NO_SCORE)
            return false;

        int totalPenalty = Stats.AbilityScoreDamage.GetTotalPenalty(ability);
        int nonIdiocyPenalty = Mathf.Max(0, totalPenalty - touchDamage);
        int scoreWithoutTouchOfIdiocy = Mathf.Max(0, baseScore - nonIdiocyPenalty);
        return scoreWithoutTouchOfIdiocy <= 0;
    }

    private int GetTouchOfIdiocyDamageForAbility(AbilityType ability)
    {
        if (_activeTouchOfIdiocyEffect == null)
            return 0;

        switch (ability)
        {
            case AbilityType.INT:
                return Mathf.Max(0, _activeTouchOfIdiocyEffect.IntelligenceDamage);
            case AbilityType.WIS:
                return Mathf.Max(0, _activeTouchOfIdiocyEffect.WisdomDamage);
            case AbilityType.CHA:
                return Mathf.Max(0, _activeTouchOfIdiocyEffect.CharismaDamage);
            default:
                return 0;
        }
    }

    private void RecalculateStatsFromAbilityScoreChange()
    {
        Inventory inventoryData = GetInventoryData();
        if (inventoryData != null)
            inventoryData.RecalculateStats();
    }

    private void LogAbilityScoreMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (GameManager.Instance != null && GameManager.Instance.CombatUI != null)
            GameManager.Instance.CombatUI.ShowCombatLog(message);
        else
            Debug.Log(message);
    }

    private static string GetAbilityName(AbilityType ability)
    {
        switch (ability)
        {
            case AbilityType.STR: return "Strength";
            case AbilityType.DEX: return "Dexterity";
            case AbilityType.CON: return "Constitution";
            case AbilityType.INT: return "Intelligence";
            case AbilityType.WIS: return "Wisdom";
            case AbilityType.CHA: return "Charisma";
            default: return ability.ToString();
        }
    }

    private static string FormatSource(string source)
    {
        return string.IsNullOrWhiteSpace(source) ? string.Empty : $" from {source}";
    }

    public int GetConditionACModifier()
    {
        return EnsureConditions().GetConditionACModifier();
    }

    public int GetConditionAttackModifier()
    {
        return EnsureConditions().GetConditionAttackModifier();
    }

    public bool CanTakeActions()
    {
        return EnsureConditions().CanTakeActions();
    }

    public bool CanMove()
    {
        return EnsureConditions().CanMove();
    }

    public bool CanAttack()
    {
        return EnsureConditions().CanAttack();
    }

    public bool CanCastSpells()
    {
        return EnsureConditions().CanCastSpells();
    }

    public bool IsHelplessLikeConditionState()
    {
        return EnsureConditions().IsHelplessLike();
    }

    public string GetConditionSummary()
    {
        return EnsureConditions().GetConditionSummary();
    }

    public int ClearAllConditions()
    {
        return EnsureConditions().ClearAllConditions();
    }

    private void OnCurrentHPChanged(int oldHP, int newHP)
    {
        string name = Stats != null ? Stats.CharacterName : gameObject.name;
        Debug.Log($"[DeathFlow][HPChanged] {name} HP changed | old={oldHP} -> new={newHP} | stateBefore={_currentHPState} | deadNow={newHP <= -10}");

        HPState next = DetermineStateFromHPTransition(oldHP, newHP);
        Debug.Log($"[DeathFlow][HPChanged] {name} resolved next HP state={next}");
        SetHPState(next, emitLog: true);

        if (newHP < oldHP && GameManager.Instance != null && GameManager.Instance.IsCharacterAsleep(this))
        {
            if (GameManager.Instance.TryWakeSleepingCharacter(this, "takes damage", suppressLog: true))
                GameManager.Instance.CombatUI?.ShowCombatLog(CombatLogHelper.Damage("💥", $"{Stats.CharacterName} wakes from taking damage."));
        }
    }

    private void OnNonlethalDamageChanged(int oldValue, int newValue)
    {
        if (Stats == null)
            return;

        HPState next = DetermineStateFromHPTransition(Stats.CurrentHP, Stats.CurrentHP);
        SetHPState(next, emitLog: true);

        if (newValue > oldValue && GameManager.Instance != null && GameManager.Instance.IsCharacterAsleep(this))
        {
            if (GameManager.Instance.TryWakeSleepingCharacter(this, "takes nonlethal damage", suppressLog: true))
                GameManager.Instance.CombatUI?.ShowCombatLog(CombatLogHelper.Damage("💥", $"{Stats.CharacterName} wakes from taking damage."));
        }
    }

    /// <summary>
    /// Sync state from current HP value (used at initialization or forced refresh).
    /// </summary>
    public void SyncHPStateFromCurrentHP(bool emitLog = false)
    {
        if (Stats == null)
            return;

        HPState next = DetermineStateFromHPTransition(Stats.CurrentHP, Stats.CurrentHP);
        SetHPState(next, emitLog);
    }

    private HPState DetermineStateFromHPTransition(int oldHP, int newHP)
    {
        if (Stats == null)
            return HPState.Healthy;

        if (newHP <= -10)
            return HPState.Dead;

        if (newHP <= -1)
        {
            // Diehard (PHB p.93): stable automatically and conscious, acting as disabled, at -1 to -9 HP. Checked before
            // the healing-stabilizes rule, so a Diehard character healed or regenerating at negative HP stays disabled
            // (and stays in the fight, CombatEndRules) instead of becoming Stable, which counts as unconscious.
            if (Stats != null && FeatManager.HasDiehard(Stats))
            {
                if (_currentHPState != HPState.Disabled)
                    Debug.Log($"[Diehard] {Stats.CharacterName} remains conscious at {newHP} HP (Diehard feat)");
                return HPState.Disabled;
            }

            if (newHP > oldHP)
                return HPState.Stable; // Healing while still negative stabilizes.

            if (_currentHPState == HPState.Stable && newHP == oldHP)
                return HPState.Stable;

            return HPState.Dying;
        }

        // 0 or higher HP: evaluate disabled/nonlethal states.
        if (Stats.NonlethalDamage > newHP)
            return HPState.Unconscious;

        if (newHP > 0 && Stats.NonlethalDamage == newHP)
            return HPState.Staggered;

        if (newHP == 0)
            return HPState.Disabled;

        return HPState.Healthy;
    }

    private void SetHPState(HPState newState, bool emitLog)
    {
        if (_currentHPState == newState)
            return;

        HPState oldState = _currentHPState;
        _currentHPState = newState;
        OnHPStateChanged(oldState, newState, emitLog);
    }

    private void OnHPStateChanged(HPState oldState, HPState newState, bool emitLog)
    {
        Debug.Log($"[HPState] {(Stats != null ? Stats.CharacterName : name)}: {oldState} -> {newState} (HP {(Stats != null ? Stats.CurrentHP : 0)})");

        UpdateConditionsForHPState(newState);
        EnsureStatusTagManager().UpdateHPStateTags(newState);

        RefreshActionLimits();

        if (newState == HPState.Unconscious || newState == HPState.Dying || newState == HPState.Stable)
        {
            ReleaseGrappleState("incapacitated");

            var concMgr = Concentration;
            if (concMgr != null && concMgr.IsConcentrating)
                concMgr.OnCharacterIncapacitated();

            var spellComp = Spellcasting;
            if (spellComp != null && spellComp.HasHeldTouchCharge)
                spellComp.ClearHeldTouchCharge("caster incapacitated");
        }

        if (newState != HPState.Dead)
            _hasProcessedDeath = false;

        if (newState == HPState.Dead)
            OnDeath();

        if (emitLog && GameManager.Instance != null && GameManager.Instance.CombatUI != null && Stats != null)
        {
            string msg = BuildHPStateLogMessage(oldState, newState);
            if (!string.IsNullOrEmpty(msg))
                GameManager.Instance.CombatUI.ShowCombatLog(msg);
        }
    }

    private string BuildHPStateLogMessage(HPState oldState, HPState newState)
    {
        string who = Stats != null ? Stats.CharacterName : "Character";

        switch (newState)
        {
            case HPState.Healthy:
                if (oldState == HPState.Disabled || oldState == HPState.Staggered || oldState == HPState.Unconscious || oldState == HPState.Dying || oldState == HPState.Stable)
                    return $"✅ {who} is back in the fight ({Stats.CurrentHP} HP).";
                return string.Empty;

            case HPState.Disabled:
                return $"⚠ {who} is DISABLED at 0 HP (one move OR one standard action).";

            case HPState.Staggered:
                return $"⚠ {who} is STAGGERED (nonlethal damage equals current HP: one move OR one standard action).";

            case HPState.Unconscious:
                return $"💤 {who} falls UNCONSCIOUS from nonlethal damage ({Stats.NonlethalDamage} nonlethal vs {Stats.CurrentHP} HP).";

            case HPState.Dying:
                return $"💀 {who} is DYING at {Stats.CurrentHP} HP and falls unconscious.";

            case HPState.Stable:
                return $"🛡 {who} is STABLE at {Stats.CurrentHP} HP (unconscious, no HP loss).";

            case HPState.Dead:
                return $"☠ {who} has DIED.";

            default:
                return string.Empty;
        }
    }

    private void UpdateConditionsForHPState(HPState state)
    {
        // Remove existing HP-state conditions first.
        RemoveCondition(CombatConditionType.Disabled);
        RemoveCondition(CombatConditionType.Staggered);
        RemoveCondition(CombatConditionType.Dying);
        RemoveCondition(CombatConditionType.Stable);
        RemoveCondition(CombatConditionType.Unconscious);

        switch (state)
        {
            case HPState.Disabled:
                ApplyCondition(CombatConditionType.Disabled, -1, "HP State");
                break;
            case HPState.Staggered:
                ApplyCondition(CombatConditionType.Staggered, -1, "HP State");
                break;
            case HPState.Unconscious:
                ApplyCondition(CombatConditionType.Unconscious, -1, "HP State");
                break;
            case HPState.Dying:
                ApplyCondition(CombatConditionType.Dying, -1, "HP State");
                ApplyCondition(CombatConditionType.Unconscious, -1, "HP State");
                break;
            case HPState.Stable:
                ApplyCondition(CombatConditionType.Stable, -1, "HP State");
                ApplyCondition(CombatConditionType.Unconscious, -1, "HP State");
                break;
        }
    }

    /// <summary>DiceService context of the dying character's stabilization roll (a scenario can force it).</summary>
    public const string StabilizationCheckContext = "Stabilization check";

    /// <summary>
    /// D&D 3.5 end-of-turn dying progression (check first, then lose HP on failure): the dying character has a 10%
    /// chance on d% to become stable, else it loses 1 hit point (PHB p.145; CHR-011). Also used for the PCs' dying rolls
    /// when both sides are out at once (GameManager.EvaluateCombatEnd, CORE-038).
    /// </summary>
    public void ProcessEndOfTurnHPState()
    {
        if (Stats == null || _currentHPState != HPState.Dying)
            return;

        // Diehard characters auto-stabilize and don't lose HP each round
        if (FeatManager.HasDiehard(Stats))
        {
            SetHPState(HPState.Disabled, emitLog: true);
            if (GameManager.Instance != null && GameManager.Instance.CombatUI != null)
                GameManager.Instance.CombatUI.ShowCombatLog(CombatLogHelper.Success("💪", $"{Stats.CharacterName} stabilizes automatically (Diehard feat) and remains conscious at {Stats.CurrentHP} HP."));
            return;
        }

        int roll = DiceService.Percentile(StabilizationCheckContext);
        const int stableChance = 10;

        if (GameManager.Instance != null && GameManager.Instance.CombatUI != null)
        {
            GameManager.Instance.CombatUI.ShowCombatLog(CombatLogHelper.Info("🎲", $"{Stats.CharacterName} stabilization check: d%({roll}) vs {stableChance} or less (PHB p.145)"));
        }

        if (roll <= stableChance)
        {
            SetHPState(HPState.Stable, emitLog: true);
            return;
        }

        int before = Stats.CurrentHP;
        Stats.TakeDamage(1);
        int after = Stats.CurrentHP;

        if (GameManager.Instance != null && GameManager.Instance.CombatUI != null)
        {
            GameManager.Instance.CombatUI.ShowCombatLog(CombatLogHelper.Failure("💉", $"{Stats.CharacterName} fails to stabilize and loses 1 HP ({before} → {after})."));
        }
    }

    public void Stabilize(string sourceName = SpellNames.AID)
    {
        if (Stats == null)
            return;

        if (Stats.CurrentHP >= -9 && Stats.CurrentHP <= -1 && _currentHPState != HPState.Dead)
        {
            SetHPState(HPState.Stable, emitLog: true);
            if (GameManager.Instance != null && GameManager.Instance.CombatUI != null)
                GameManager.Instance.CombatUI.ShowCombatLog(CombatLogHelper.Success("🩹", $"{Stats.CharacterName} is stabilized by {sourceName}."));
        }
    }

    /// <summary>
    /// The one action-limit formula (PHB p.145 disabled, p.146 and glossary p.313 staggered, p.280 Slow, p.310
    /// nauseated; MM p.266 single actions; CMB-095): one move or one standard action and no full-round action while
    /// disabled, staggered, slowed or limited to single actions; a single move action while nauseated. It only sets the
    /// flags, so it never gives back an action already spent (<see cref="ActionEconomy.UseStandardAction"/> and
    /// <see cref="ActionEconomy.UseMoveAction"/> spend both halves while a limit is on). Called at turn start, on an HP
    /// state change and wherever Slow or Nauseated is applied or removed.
    /// </summary>
    public void RefreshActionLimits()
    {
        if (Actions == null)
            return;

        Actions.SingleActionOnly = _currentHPState == HPState.Disabled
            || _currentHPState == HPState.Staggered
            || (Stats != null && Stats.IsSingleActionsOnly)
            || HasActiveSlowEffect;
        Actions.MoveActionOnly = Stats != null && HasConditionDirect(CombatConditionType.Nauseated);
    }

    /// <summary>
    /// A disabled creature took a strenuous act this turn (a standard action, or a quickened spell) and owes the 1
    /// damage for it (<see cref="ResolvePendingStrenuousDamage"/>). Cleared at turn start and by
    /// <see cref="ResetForNewCreature"/>; a refunded cast puts back its value from before the cast.
    /// </summary>
    public bool StrenuousDamagePending { get; private set; }

    /// <summary>
    /// What this creature's current turn start decided, PC or NPC: the compulsions it is under this turn
    /// (<see cref="TurnStartState.Compulsion"/>, set by GameManager.TryRunCompulsionTurn; batch 12 step 7). Cleared at
    /// every turn start and by <see cref="ResetForNewCreature"/>.
    /// </summary>
    public TurnStartState CurrentTurnStart { get; } = new TurnStartState();

    /// <summary>
    /// Spend the standard action. A disabled creature's standard action is strenuous (<see cref="MarkStrenuousAction"/>).
    /// Every standard action of either side pays here.
    /// </summary>
    public bool CommitStandardAction()
    {
        if (!Actions.HasStandardAction)
            return false;

        Actions.UseStandardAction();
        MarkStrenuousAction();
        return true;
    }

    /// <summary>
    /// A strenuous act by a disabled creature: a standard action (<see cref="CommitStandardAction"/>) or another
    /// strenuous action such as casting a quickened spell (PHB p.145; PHB p.93 Diehard; the cast core's free-action cost,
    /// PCs and NPCs alike). It takes 1 damage after completing the act (at 0 hp it is then at -1 and dying; with
    /// Diehard below 0 it loses 1 more and stays disabled). The damage is owed here and dealt by
    /// <see cref="ResolvePendingStrenuousDamage"/> once the act is done: at the turn end (GameManager.NextInitiativeTurn)
    /// or, when the act decides the fight, by the combat-end check (GameManager.EvaluateCombatEnd). A creature the
    /// damage will leave dying takes nothing further this turn (<see cref="ActionEconomy.NoFurtherActions"/>: no 5-ft
    /// step, swift action or quickened spell; delegated RAW reading 2026-10-28, RULES_COVERAGE 4.1). No-op for a
    /// creature that is not disabled.
    /// </summary>
    public void MarkStrenuousAction()
    {
        if (_currentHPState != HPState.Disabled || Stats == null || Stats.CurrentHP > 0)
            return;

        StrenuousDamagePending = true;
        bool staysDisabled = Stats.TempHP > 0 || FeatManager.HasDiehard(Stats);
        if (!staysDisabled && Actions != null)
            Actions.NoFurtherActions = true;
    }

    /// <summary>
    /// A cast refunded after its cost was paid (the AoO prompt cancelled, a failed spend) never completed its act, so it
    /// owes nothing: put back the owed-damage flag captured before the cast (GameManager.RestoreSpellcastResourceSnapshot).
    /// </summary>
    public void RestoreStrenuousDamagePending(bool pending)
    {
        StrenuousDamagePending = pending;
    }

    /// <summary>
    /// Deal the 1 damage a disabled creature owes for its strenuous act (PHB p.145, "after completing the act";
    /// <see cref="MarkStrenuousAction"/>). Temporary hit points absorb it first (<see cref="CharacterStats.TakeDamage"/>).
    /// The game calls it at every turn end, before the combat-end check (GameManager.NextInitiativeTurn), and in the
    /// combat-end check once a side is out (GameManager.EvaluateCombatEnd), for PCs and NPCs alike. The damage is due
    /// whatever happened since (healing included). True when damage was dealt.
    /// </summary>
    public bool ResolvePendingStrenuousDamage()
    {
        if (!StrenuousDamagePending)
            return false;

        StrenuousDamagePending = false;
        if (Stats == null || IsDead || Stats.IsDead)
            return false;

        int before = Stats.CurrentHP;
        Stats.TakeDamage(1);
        int after = Stats.CurrentHP;
        if (GameManager.Instance != null && GameManager.Instance.CombatUI != null)
            GameManager.Instance.CombatUI.ShowCombatLog(CombatLogHelper.Warning("⚠", after < before
                ? $"{Stats.CharacterName} takes 1 damage for acting while disabled ({before} → {after} HP)."
                : $"{Stats.CharacterName} takes 1 damage for acting while disabled (absorbed by temporary hit points)."));
        return true;
    }

    private void TryApplyNaturalAttackOnHitEffects(CharacterController target, CombatResult attackResult, NaturalAttackDefinition naturalAttack)
    {
        if (target == null || attackResult == null || !attackResult.Hit || naturalAttack == null)
            return;

        // --- Poison on hit ---
        // A wild-shaped druid's DC is figured for her, not copied from the form (WildShapeRules.OnHitSaveDcModifier,
        // MM p.313, p.6; delegated RAW reading 2026-10-14, CHR-020); 0 for every creature in its own form.
        string poisonId = naturalAttack.PoisonOnHitId;
        if (!string.IsNullOrWhiteSpace(poisonId))
        {
            PoisonData poison = PoisonDatabase.GetPoison(poisonId);
            target.ApplyPoison(poisonId, poison != null ? WildShapeRules.OnHitSaveDcModifier(Stats, poison.FortitudeDC) : 0);
        }

        // --- Disease on hit (Dire Rat filth fever, etc.) ---
        if (naturalAttack.HasDiseaseOnHit)
        {
            DiseaseData disease = DiseaseDatabase.GetDisease(naturalAttack.DiseaseOnHitType);
            target.ExposeToDisease(naturalAttack.DiseaseOnHitType,
                disease != null ? WildShapeRules.OnHitSaveDcModifier(Stats, disease.FortitudeDC) : 0);
            Debug.Log($"[OnHit] {Stats?.CharacterName ?? "?"} exposes {target.Stats?.CharacterName ?? "?"} to {naturalAttack.DiseaseOnHitType}");
        }

        // --- Paralysis on hit (Gelatinous Cube tentacle, Ghoul, etc.) ---
        if (naturalAttack.ParalysisOnHitDC > 0 && target.Stats != null)
        {
            // The one save roll (SaveRollRules, D13), against this creature's ability (SPL-018).
            SaveRoll save = SaveRollRules.Roll(target.Stats, SavingThrowType.Fortitude, naturalAttack.ParalysisOnHitDC,
                SaveContext.CreatureAbility("paralysis", this), null);
            if (!save.Succeeded)
            {
                int duration = Mathf.Max(1, naturalAttack.ParalysisOnHitDurationRounds);
                target.Conditions.ApplyCondition(CombatConditionType.Paralyzed, duration, Stats?.CharacterName ?? "Paralysis");
                Debug.Log($"[OnHit] {target.Stats.CharacterName} paralyzed for {duration} rounds (Fort d20({save.Roll})+{save.Modifier}={save.Total} vs DC {naturalAttack.ParalysisOnHitDC}: failed{save.NaturalSuffix})");
            }
            else
            {
                Debug.Log($"[OnHit] {target.Stats.CharacterName} resists paralysis (Fort d20({save.Roll})+{save.Modifier}={save.Total} vs DC {naturalAttack.ParalysisOnHitDC}{save.NaturalSuffix})");
            }
        }

        // --- Energy drain on hit (Wight, Wraith) ---
        if (naturalAttack.EnergyDrainOnHit > 0)
        {
            target.ApplyNegativeLevels(naturalAttack.EnergyDrainOnHit, Stats?.CharacterName ?? "Energy Drain");
            Debug.Log($"[OnHit] {target.Stats?.CharacterName ?? "?"} drains {naturalAttack.EnergyDrainOnHit} level(s) from {target.Stats?.CharacterName ?? "?"}");
        }

        // --- Ability drain on hit (Shadow STR drain, Wraith CON drain, Allip WIS drain) ---
        if (naturalAttack.AbilityDrainAmount > 0)
        {
            target.ApplyAbilityDrain(naturalAttack.AbilityDrainType, naturalAttack.AbilityDrainAmount, Stats?.CharacterName ?? "Ability Drain");
            Debug.Log($"[OnHit] {target.Stats?.CharacterName ?? "?"} drains {naturalAttack.AbilityDrainAmount} {naturalAttack.AbilityDrainType} from {target.Stats?.CharacterName ?? "?"}");
        }

        // --- Petrification on hit (Cockatrice) ---
        if (naturalAttack.PetrificationOnHitDC > 0 && target.Stats != null)
        {
            // The one save roll (SaveRollRules, D13), against this creature's ability (SPL-018).
            SaveRoll save = SaveRollRules.Roll(target.Stats, SavingThrowType.Fortitude, naturalAttack.PetrificationOnHitDC,
                SaveContext.CreatureAbility("petrification", this), null);
            if (!save.Succeeded)
            {
                target.Conditions.ApplyCondition(CombatConditionType.Petrified, 999, Stats?.CharacterName ?? "Petrification");
                Debug.Log($"[OnHit] {target.Stats.CharacterName} PETRIFIED (Fort d20({save.Roll})+{save.Modifier}={save.Total} vs DC {naturalAttack.PetrificationOnHitDC}: failed{save.NaturalSuffix})");
            }
            else
            {
                Debug.Log($"[OnHit] {target.Stats.CharacterName} resists petrification (Fort d20({save.Roll})+{save.Modifier}={save.Total} vs DC {naturalAttack.PetrificationOnHitDC}{save.NaturalSuffix})");
            }
        }

        // --- Blood drain (Stirge: Con damage while attached) ---
        if (naturalAttack.HasBloodDrain && naturalAttack.BloodDrainConDamagePerRound > 0)
        {
            target.ApplyAbilityDrain(AbilityType.CON, naturalAttack.BloodDrainConDamagePerRound, Stats?.CharacterName ?? "Blood Drain");
            Debug.Log($"[OnHit] {Stats?.CharacterName ?? "?"} blood drains {naturalAttack.BloodDrainConDamagePerRound} CON from {target.Stats?.CharacterName ?? "?"}");
        }
    }

    // ========== SINGLE ATTACK (Standard Action) ==========

    /// <summary>
    /// Perform a single attack against another character (standard action).
    /// Returns a CombatResult with details including critical hit info.
    /// </summary>
    public CombatResult Attack(CharacterController target)
    {
        return Attack(target, false, 0, null, null, null, null, 0, false);
    }

    private int ConsumeAidAnotherAttackBonus(CharacterController target)
    {
        if (GameManager.Instance == null) return 0;

        string attackerName = Stats != null ? Stats.CharacterName : "Unknown";
        string targetName = target != null && target.Stats != null ? target.Stats.CharacterName : "Unknown";
        Debug.Log($"[AidBonus][Attack] {attackerName} requesting Aid Another offense bonus vs {targetName}");

        int bonus = GameManager.Instance.ConsumeAidAnotherAttackBonus(this, target);
        Debug.Log($"[AidBonus][Attack] {attackerName} received Aid Another offense bonus: +{bonus}");
        return bonus;
    }

    private int ConsumeAidAnotherAcBonus(CharacterController target)
    {
        if (GameManager.Instance == null) return 0;

        string attackerName = Stats != null ? Stats.CharacterName : "Unknown";
        string targetName = target != null && target.Stats != null ? target.Stats.CharacterName : "Unknown";
        Debug.Log($"[AidBonus][Defense] {attackerName} requesting defender Aid Another AC adjustment vs {targetName}");

        int bonus = GameManager.Instance.ConsumeAidAnotherAcBonus(this, target);
        Debug.Log($"[AidBonus][Defense] {attackerName} received defender Aid Another AC adjustment: +{bonus}");
        return bonus;
    }

    // ========== SHARED ATTACK MODIFIER (CMB-043) ==========

    /// <summary>
    /// True when an attack with this weapon at this range is a ranged attack: a ranged weapon,
    /// or a thrown weapon used at range. Shared by every weapon attack path.
    /// </summary>
    private static bool IsRangedWeaponAttack(ItemData weapon, RangeInfo rangeInfo)
    {
        if (weapon == null || rangeInfo == null || rangeInfo.IsMelee)
            return false;

        bool thrownAtRange = weapon.IsThrown && weapon.RangeIncrement > 0;
        return weapon.WeaponCat == WeaponCategory.Ranged || thrownAtRange;
    }

    /// <summary>
    /// Threat range and multiplier of the weapon actually used (PHB p.140), before Improved Critical.
    /// Reads the weapon itself (plus Keen Edge style spell effects, as Inventory.ApplyWeaponStats does)
    /// so off-hand and override weapons use their own crit. Unarmed and natural attacks are 20/x2.
    /// </summary>
    private static void ResolveWeaponCritProfile(ItemData weapon, out int baseThreatMin, out int critMultiplier)
    {
        if (weapon == null)
        {
            baseThreatMin = 20;
            critMultiplier = 2;
            return;
        }

        int threatMin = weapon.CritThreatMin > 0 ? weapon.CritThreatMin : 20;
        if (weapon.ActiveSpellEffects != null)
        {
            foreach (var eff in weapon.ActiveSpellEffects)
            {
                if (eff != null && eff.CritThreatRangeModifier != 0)
                    threatMin += eff.CritThreatRangeModifier;
            }
        }

        baseThreatMin = Mathf.Clamp(threatMin, 2, 20);
        critMultiplier = weapon.CritMultiplier > 0 ? weapon.CritMultiplier : 2;
    }

    /// <summary>Magic Stone: +1 enhancement on a sling attack while stones remain (PHB p.251).</summary>
    private int GetMagicStoneAttackBonus(ItemData weapon, bool isRanged)
    {
        bool active = isRanged && weapon != null && weapon.Id == ItemIDs.SLING
            && Stats.MagicStoneActive && Stats.MagicStoneCharges > 0;
        return active ? 1 : 0;
    }

    /// <summary>
    /// The one place a weapon attack roll's modifier is assembled (CMB-043). Attack, FullAttack
    /// (iterative, natural and flurry) and DualWieldAttack all call this, then set the
    /// per-attack terms (BaseAttackBonus step, SequenceModifier, AidAnotherBonus, MagicStoneBonus). The flurry of blows
    /// penalty (FlurryPenalty) is set here for every attack while the creature flurries (PHB p.40; CHR-003).
    /// Rules applied: ability is DEX for ranged attacks and Weapon Finesse, else STR (PHB p.134, p.102);
    /// size (p.134); shooting into melee (p.140); prone (p.151); flanking (p.153); range increments (p.134);
    /// mounted ranged (applied whenever mounted; RAW p.157 only while the mount double-moves or runs, CMB-031);
    /// feats; morale and condition modifiers; non-proficiency.
    /// </summary>
    /// <param name="baseAttackBonus">BAB step for the first attack (callers overwrite per attack).</param>
    /// <param name="isTwoHanded">Wielded two-handed (Power Attack doubles damage).</param>
    /// <param name="rapidShotEnabled">Full attack with the Rapid Shot toggle on.</param>
    public AttackBonusBreakdown BuildAttackBonus(CharacterController target, ItemData weapon, bool isRanged,
        RangeInfo rangeInfo, bool isFlanking, int flankingBonus, int baseAttackBonus,
        bool isTwoHanded, bool rapidShotEnabled = false)
    {
        bool isMelee = !isRanged;
        bool hasRangeInfo = rangeInfo != null && !rangeInfo.IsMelee;

        ResolveWeaponCritProfile(weapon, out int baseThreatMin, out int critMultiplier);
        AttackCalculator.FeatModifiers feats = AttackCalculator.CalculateAllFeatModifiers(
            Stats, weapon, isRanged, isMelee, isTwoHanded, PowerAttackValue,
            rangeInfo != null ? rangeInfo.DistanceFeet : 0, hasRangeInfo,
            WeaponDisablesStrengthDamageBonuses(weapon), baseThreatMin, rapidShotEnabled);

        var b = new AttackBonusBreakdown
        {
            BaseAttackBonus = baseAttackBonus,
            SequenceModifier = 0,
            SequenceLabel = string.Empty,
            // Every attack of the round a monk flurries, and her attacks of opportunity until her next turn (PHB p.40; CHR-003).
            FlurryPenalty = IsUsingFlurryOfBlows ? Stats.FlurryOfBlowsAttackPenalty : 0,
            AbilityMod = feats.AbilityMod,
            AbilityName = feats.AbilityName,
            SizeModifier = Stats.SizeModifier,
            FlankingBonus = isFlanking ? flankingBonus : 0,
            // Racial: +1 against a kind (dwarf vs orcs and goblinoids, gnome vs kobolds and goblinoids) and the halfling's
            // +1 with thrown weapons and slings (PHB p.15, p.17, p.20; CHR-019).
            RacialBonus = RacialTraitRules.AttackBonus(Stats, target != null ? target.Stats : null, weapon, isRanged),
            RangePenalty = hasRangeInfo && rangeInfo.IsInRange ? rangeInfo.Penalty : 0,
            MountedRangedPenalty = hasRangeInfo && MountSystem.IsMounted(this) ? MountedCombatSystem.GetMountedRangedPenalty(this) : 0,
            Feats = feats,
            PronePenalty = GetProneAttackModifier(isMelee),
            FightingDefensivelyPenalty = IsFightingDefensively ? CombatCalculationService.FightingDefensivelyAttackPenalty : 0,
            WeaponNonProficiencyPenalty = Stats.GetWeaponNonProficiencyPenalty(weapon),
            ArmorNonProficiencyPenalty = Stats.GetArmorNonProficiencyAttackPenalty(),
            MoraleBonus = Stats.EffectAttackBonus,
            ConditionModifier = Stats.ConditionAttackPenalty,
            AidAnotherBonus = 0,
            DamageModePenalty = ResolveDamageModeAttackProfile(weapon).AttackPenalty,
            SolidFogPenalty = isMelee ? Stats.SolidFogMeleeAttackPenalty : 0,
            MagicStoneBonus = GetMagicStoneAttackBonus(weapon, isRanged),
            CritThreatMin = feats.CritThreatMin,
            CritMultiplier = critMultiplier
        };

        b.ShootingIntoMeleePenalty = GetShootingIntoMeleePenalty(this, target, isRanged, out bool preciseShotNegated);
        b.PreciseShotNegated = preciseShotNegated;

        // Bracers of Archery: competence bonus with bows only (arrows; DMG p.250).
        bool isBow = isRanged && weapon != null && weapon.RequiresAmmoType == AmmunitionType.Arrow;
        b.WondrousBowAttackBonus = isBow ? Stats.WondrousBowAttackBonus : 0;

        return b;
    }

    /// <summary>
    /// The one place a weapon damage roll's static modifier is assembled (CMB-003; the attack roll's is
    /// <see cref="BuildAttackBonus"/>). Every weapon attack path passes the result to PerformSingleAttackWithCrit, and
    /// coup de grace, sunder and grapple damage add its <see cref="WeaponDamageBreakdown.Total"/>, so all of them add
    /// the same terms: the Strength share (PHB p.134: 1/2 in the off hand, 1-1/2 two-handed, a penalty never
    /// multiplied, a composite bow's rating; a natural attack's own share, MM p.312), the profile's flat damage,
    /// weapon enhancement and material, the feat terms in <paramref name="feats"/> (Power Attack, Point Blank Shot,
    /// Weapon Specialization; pass default when the path has no feat terms), CharacterStats.EffectWeaponDamageBonus
    /// (Inspire Courage, Prayer, Divine Favor, Magic Fang and other effects, stacked by type), condition modifiers
    /// (Sickened -2, DMG p.301), Solid Fog's melee penalty, Bracers of Archery with a bow, and an optional
    /// per-attack situational term (a charge). The Destruction smite, a declared paladin or template smite (melee only;
    /// SmiteRules) and a bane weapon's +2 against its foe (DMG p.224) are added by PerformSingleAttackWithCrit, which
    /// also uses up the smite. Weapon damage dice already follow the wielder's size (GetScaledWeaponDamageDice, CMB-119),
    /// so size adds no flat term.
    /// </summary>
    /// <param name="weapon">The weapon (null: an unarmed strike or a natural attack).</param>
    /// <param name="isOffHand">An off-hand attack: half the Strength bonus (PHB p.134).</param>
    /// <param name="weaponBonusDamage">The attack profile's flat damage (ItemData.BonusDamage, Magic Stone's +1).</param>
    /// <param name="naturalAttack">A natural attack: its Strength share replaces the weapon rule (MM p.312).</param>
    public WeaponDamageBreakdown BuildWeaponDamageBonus(ItemData weapon, bool isRanged, bool isOffHand,
        AttackCalculator.FeatModifiers feats, int weaponBonusDamage = 0, string weaponBonusLabel = null,
        NaturalAttackDefinition naturalAttack = null, int situationalBonus = 0, string situationalLabel = null)
    {
        var d = new WeaponDamageBreakdown
        {
            WeaponBonus = weaponBonusDamage,
            WeaponBonusLabel = weaponBonusLabel,
            PowerAttackBonus = feats.PowerAttackDamageBonus,
            PointBlankShotBonus = feats.PointBlankShotDamageBonus,
            WeaponSpecializationBonus = feats.WeaponSpecDamageBonus,
            SituationalBonus = situationalBonus,
            SituationalLabel = situationalLabel,
            IsRangedAttack = isRanged
        };
        if (Stats == null)
            return d;

        if (naturalAttack != null)
        {
            d.StrengthBonus = Stats.GetNaturalAttackDamageBonus(naturalAttack);
            d.StrengthLabel = DescribeNaturalAttackStrength(naturalAttack.BonusDamageSource, Stats.STRMod);
        }
        else if (WeaponDisablesStrengthDamageBonuses(weapon))
        {
            d.StrengthSuppressed = true;
            d.StrengthLabel = "no STR modifier";
        }
        else if (_resolvingFlurryOfBlowsAttack && !isOffHand)
        {
            // A flurry attack adds the full Strength bonus, not 1-1/2 or 1/2 times it, in one hand or both (PHB p.41; CHR-003).
            d.StrengthBonus = Stats.STRMod;
            d.StrengthLabel = "STR (flurry)";
        }
        else
        {
            d.StrengthBonus = Stats.GetWeaponDamageModifier(weapon, isOffHand);
            d.StrengthLabel = Stats.GetDamageModifierDescription(weapon, isOffHand);
        }

        if (weapon != null)
        {
            d.EnhancementBonus = weapon.GetEnhancementDamageBonus();
            d.MaterialModifier = weapon.MaterialDamageModifier;
        }

        d.MoraleBonus = Stats.EffectWeaponDamageBonus;
        d.MoraleLabel = d.MoraleBonus != 0 ? DescribeMoraleDamageSources() : null;
        d.ConditionModifier = Stats.ConditionWeaponDamageModifier;
        d.ConditionLabel = d.ConditionModifier != 0 ? DescribeConditionDamageSources() : null;
        d.SolidFogPenalty = isRanged ? 0 : Stats.SolidFogMeleeDamagePenalty;
        bool isBow = isRanged && weapon != null && weapon.RequiresAmmoType == AmmunitionType.Arrow;
        d.BracersOfArcheryBonus = isBow ? Stats.WondrousBowDamageBonus : 0;
        return d;
    }

    // A Strength penalty applies in full, never halved or multiplied (owner interpretation 2026-10-11, CMB-161), so with
    // a penalty a sole or secondary natural attack's share reads plain "STR".
    private static string DescribeNaturalAttackStrength(DamageBonusSource source, int strMod)
    {
        switch (source)
        {
            case DamageBonusSource.StrengthOneAndHalf: return strMod < 0 ? "STR" : "1.5× STR";
            case DamageBonusSource.StrengthHalf: return strMod < 0 ? "STR" : "0.5× STR";
            case DamageBonusSource.None: return "no STR modifier";
            default: return "STR";
        }
    }

    /// <summary>
    /// Feat terms for a damage roll whose attack side has no Power Attack penalty (no attack roll, or a hand-built one):
    /// only Weapon Specialization with <paramref name="weapon"/> (null: unarmed strike), which adds to every damage roll
    /// with the chosen weapon (PHB p.102). Power Attack and Point Blank Shot stay off.
    /// </summary>
    private AttackCalculator.FeatModifiers DamageOnlyFeatModifiers(ItemData weapon)
    {
        return new AttackCalculator.FeatModifiers
        {
            WeaponSpecDamageBonus = Stats != null ? AttackCalculator.GetWeaponSpecBonus(Stats, weapon) : 0
        };
    }

    /// <summary>
    /// True when <paramref name="weaponOverride"/> is the weapon this creature holds in its off hand rather than its main
    /// weapon (CharacterEquipment.GetOffHandAttackWeapon), so an attack with it adds half the Strength bonus (PHB p.134; CMB-008).
    /// </summary>
    private bool IsOffHandWeaponOverride(ItemData weaponOverride, bool unarmedStrike)
    {
        if (unarmedStrike || weaponOverride == null)
            return false;
        return weaponOverride != GetEquippedMainWeapon() && weaponOverride == GetOffHandAttackWeapon();
    }

    /// <summary>
    /// "Divine Favor, Inspire Courage": the effects whose weapon damage modifier counts after stacking
    /// (CharacterStats.EffectWeaponDamageBonus, BonusLedger.DescribeSources), for the combat log.
    /// </summary>
    private string DescribeMoraleDamageSources()
    {
        string names = Stats.Bonuses.DescribeSources(BonusTarget.WeaponDamage);
        return string.IsNullOrEmpty(names) ? "spell and morale bonuses" : names;
    }

    /// <summary>"conditions (Sickened)": the conditions behind CharacterStats.ConditionWeaponDamageModifier.</summary>
    private string DescribeConditionDamageSources()
    {
        var names = new List<string>();
        if (Stats.ActiveConditions != null)
        {
            for (int i = 0; i < Stats.ActiveConditions.Count; i++)
            {
                ConditionDefinition def = ConditionRules.GetDefinition(Stats.ActiveConditions[i].Type);
                if (def == null || def.WeaponDamageModifier == 0)
                    continue;
                string label = !string.IsNullOrWhiteSpace(def.DisplayName) ? def.DisplayName : Stats.ActiveConditions[i].Type.ToString();
                if (!names.Contains(label))
                    names.Add(label);
            }
        }
        return names.Count == 0 ? "conditions" : string.Join(", ", names);
    }

    /// <summary>Copies a hit's damage terms onto its result for the combat log and the scenario trace (CMB-003).</summary>
    private static void RecordWeaponDamageBreakdown(CombatResult result, WeaponDamageBreakdown damage)
    {
        if (result == null)
            return;
        result.WeaponDamageBonus = damage;
        result.HasWeaponDamageBonus = true;
        result.DamageModifier = damage.StrengthBonus;
        result.DamageModifierDesc = damage.StrengthLabel;
        result.WeaponEnhancementDamageBonus = damage.EnhancementBonus;
        result.PowerAttackDamageBonus = damage.PowerAttackBonus;
        result.WeaponSpecBonus = damage.WeaponSpecializationBonus;
        result.FeatDamageBonus = damage.FeatBonus;
    }

    /// <summary>
    /// Perform a single attack with flanking context and optional range info.
    /// Includes full D&D 3.5 critical hit mechanics, racial attack bonuses, and feat effects.
    /// Uses weapon's DamageModifierType for correct STR bonus to damage.
    /// Integrates: Power Attack, Point Blank Shot, Weapon Focus, Weapon Specialization,
    /// Weapon Finesse, Combat Expertise, Improved Critical, Dodge.
    /// With <paramref name="unarmedStrike"/> the attack is an unarmed strike whatever this creature holds (a punch,
    /// kick or head butt, PHB p.139): unarmed damage and damage mode, natural reach; <paramref name="attackWeaponOverride"/>
    /// is ignored. Used by the Improved Trip attack when no held weapon can attack (CMB-136).
    /// <paramref name="isOffHandAttack"/> adds half the Strength bonus to damage (PHB p.134; CMB-008).
    /// <paramref name="situationalAttackBonus"/> and <paramref name="situationalDamageBonus"/> apply to this attack only
    /// and are listed under <paramref name="situationalLabel"/> (CMB-003). A declared smite is not passed here: the
    /// attack core applies it (SmiteRules, CHR-020).
    /// </summary>
    public CombatResult Attack(
        CharacterController target,
        bool isFlanking,
        int flankingBonus,
        string flankingPartnerName,
        RangeInfo rangeInfo = null,
        int? baseAttackBonusOverride = null,
        ItemData attackWeaponOverride = null,
        int additionalAttackModifier = 0,
        bool isOffHandAttack = false,
        bool unarmedStrike = false,
        int situationalAttackBonus = 0,
        int situationalDamageBonus = 0,
        string situationalLabel = null)
    {
        if (target == null || target.Stats == null || target.Stats.IsDead)
        {
            Debug.LogWarning($"[Combat] {Stats?.CharacterName ?? name} attempted to attack an invalid target.");
            return new CombatResult
            {
                Attacker = this,
                Defender = target,
                WeaponName = !unarmedStrike && (attackWeaponOverride ?? GetEquippedMainWeapon()) != null
                    ? (attackWeaponOverride ?? GetEquippedMainWeapon()).Name
                    : "Unarmed strike",
                Hit = false,
                DieRoll = 1,
                TotalRoll = 1,
                TargetAC = 0,
                DefenderHPBefore = 0,
                DefenderHPAfter = 0
            };
        }

        // Get equipped weapon for damage modifier and feat calculations (null: an unarmed strike)
        ItemData equippedWeapon = unarmedStrike ? null : attackWeaponOverride ?? GetEquippedMainWeapon();
        if (!CanAttackWithWeapon(equippedWeapon, out string cannotAttackReason))
        {
            Debug.LogWarning($"[Combat] {Stats.CharacterName} cannot attack: {cannotAttackReason}");
            return new CombatResult
            {
                Attacker = this,
                Defender = target,
                WeaponName = equippedWeapon != null ? equippedWeapon.Name : "Unarmed strike",
                Hit = false,
                // No actual d20 is rolled in this early-return path, but keep roll fields within valid d20 bounds.
                DieRoll = 1,
                TotalRoll = 1,
                TargetAC = target != null && target.Stats != null ? target.Stats.ArmorClass : 0,
                DefenderHPBefore = target != null && target.Stats != null ? target.Stats.CurrentHP : 0,
                DefenderHPAfter = target != null && target.Stats != null ? target.Stats.CurrentHP : 0
            };
        }

        bool useThrownRange = equippedWeapon != null
            && equippedWeapon.IsThrown
            && equippedWeapon.RangeIncrement > 0
            && rangeInfo != null
            && !rangeInfo.IsMelee;

        // A null weapon means the main weapon to the reach helpers, so an unarmed strike checks its own reach.
        bool targetInRange = unarmedStrike
            ? IsTargetInUnarmedReach(target)
            : IsTargetInWeaponRange(target, equippedWeapon, useThrownRange);

        if (!targetInRange)
        {
            string rangeMode = useThrownRange ? "thrown" : "default";
            Debug.LogWarning($"[Combat] {Stats.CharacterName} cannot attack {target?.Stats?.CharacterName}: target out of {rangeMode} weapon range.");
            return new CombatResult
            {
                Attacker = this,
                Defender = target,
                WeaponName = equippedWeapon != null ? equippedWeapon.Name : "Unarmed strike",
                Hit = false,
                // No actual d20 is rolled in this early-return path, but keep roll fields within valid d20 bounds.
                DieRoll = 1,
                TotalRoll = 1,
                TargetAC = target != null && target.Stats != null ? target.Stats.ArmorClass : 0,
                DefenderHPBefore = target != null && target.Stats != null ? target.Stats.CurrentHP : 0,
                DefenderHPAfter = target != null && target.Stats != null ? target.Stats.CurrentHP : 0
            };
        }

        bool isRanged = IsRangedWeaponAttack(equippedWeapon, rangeInfo);

        // Last-known-position empty-square checks are resolved in PerformSingleAttackWithCrit()
        // so all attack entry points (single, full-attack, flurry, grapple strike, etc.) follow
        // the same rules and never roll against an empty square.

        // Shared per-attack modifier (CMB-043): the same terms FullAttack, DualWieldAttack and FlurryOfBlows add.
        AttackBonusBreakdown atkBonus = BuildAttackBonus(target, equippedWeapon, isRanged, rangeInfo,
            isFlanking, flankingBonus, baseAttackBonusOverride ?? Stats.BaseAttackBonus,
            IsWeaponTwoHanded(equippedWeapon));
        // Caller-supplied penalty (two-weapon off-hand, Manyshot, Mobility AoO); shown as the dual-wield entry below.
        atkBonus.SequenceModifier = additionalAttackModifier;
        atkBonus.SituationalBonus = situationalAttackBonus;
        atkBonus.SituationalLabel = situationalLabel;
        atkBonus.AidAnotherBonus = ConsumeAidAnotherAttackBonus(target);
        int aidAnotherTargetAcBonus = ConsumeAidAnotherAcBonus(target);
        DamageModeAttackProfile damageModeProfile = ResolveDamageModeAttackProfileCore(equippedWeapon, unarmedStrike);
        if (unarmedStrike)
            atkBonus.DamageModePenalty = damageModeProfile.AttackPenalty;

        if (atkBonus.AbilityName == "DEX(Finesse)")
            Debug.Log($"[Feats] {Stats.CharacterName}: Weapon Finesse active, using DEX {Stats.DEXMod} for attack");
        if (atkBonus.Feats.CombatExpertisePenalty != 0)
            Debug.Log($"[Feats] {Stats.CharacterName}: Combat Expertise {atkBonus.Feats.CombatExpertisePenalty} attack, +{-atkBonus.Feats.CombatExpertisePenalty} AC");
        if (atkBonus.MountedRangedPenalty != 0)
            Debug.Log($"[Combat] {Stats.CharacterName} mounted ranged penalty: {atkBonus.MountedRangedPenalty}");

        int totalAtkMod = atkBonus.Total;
        int damageDice, damageCount, bonusDamage;
        string attackLabel;
        if (unarmedStrike)
        {
            (damageCount, damageDice, bonusDamage) = GetUnarmedDamage();
            attackLabel = "Unarmed strike";
        }
        else
        {
            ResolveBaseAttackDamageProfile(equippedWeapon, out damageDice, out damageCount, out bonusDamage, out attackLabel,
                IsThrownWeaponAttack(equippedWeapon, rangeInfo));
        }

        // D&D 3.5e Magic Stone: when firing a sling with active Magic Stone charges,
        // override damage to 1d6+1; the +1 enhancement to attack is atkBonus.MagicStoneBonus (PHB p.251)
        bool magicStoneUsed = atkBonus.MagicStoneBonus > 0;
        if (magicStoneUsed)
        {
            damageDice = 6;
            damageCount = 1;
            bonusDamage = 1;
        }

        NaturalAttackDefinition naturalAttackForOnHit = null;
        if (!unarmedStrike && ShouldUseInnateNaturalAttackProfile(equippedWeapon))
            naturalAttackForOnHit = Stats.GetPrimaryNaturalAttack();

        // Shared damage modifier (CMB-003): half Strength in the off hand (CMB-008), the natural attack's own share.
        // A weapon override that is the off-hand weapon (an AoO with the off-hand melee weapon while the main hand holds
        // a ranged one, a weapon thrown from the off hand) deals off-hand damage even when the caller does not say so:
        // PHB p.134 halves the Strength bonus of any weapon in the off hand.
        bool offHandForDamage = isOffHandAttack || IsOffHandWeaponOverride(attackWeaponOverride, unarmedStrike);
        WeaponDamageBreakdown damageBonus = BuildWeaponDamageBonus(equippedWeapon, isRanged, offHandForDamage, atkBonus.Feats,
            bonusDamage, magicStoneUsed ? "Magic Stone" : null, naturalAttackForOnHit, situationalDamageBonus, situationalLabel);

        // Record HP before attack
        int hpBefore = target.Stats.CurrentHP;

        var result = PerformSingleAttackWithCrit(target, totalAtkMod, isFlanking, flankingBonus, flankingPartnerName,
            damageDice, damageCount, damageBonus, atkBonus.CritThreatMin, atkBonus.CritMultiplier,
            equippedWeapon, aidAnotherTargetAcBonus,
            damageModeProfile.DealNonlethalDamage, damageModeProfile.AttackPenalty, damageModeProfile.PenaltySource);

        int baseAttackWithoutAid = totalAtkMod - atkBonus.AidAnotherBonus;
        string attackerNameForLog = Stats != null ? Stats.CharacterName : "Unknown";
        string targetNameForLog = target != null && target.Stats != null ? target.Stats.CharacterName : "Unknown";
        Debug.Log($"[Attack] {attackerNameForLog} attacks {targetNameForLog}: d20={result.DieRoll} + base={baseAttackWithoutAid} + aid={atkBonus.AidAnotherBonus} => total={result.TotalRoll} vs AC {result.TargetAC}");

        atkBonus.ApplyToResult(result, rangeInfo);
        result.AidAnotherTargetAcBonus = aidAnotherTargetAcBonus;
        result.FightingDefensivelyACBonus = target != null && target.IsFightingDefensively ? 2 : 0;
        result.IsDualWieldAttack = isOffHandAttack || additionalAttackModifier != 0;
        result.IsOffHandAttack = isOffHandAttack;
        result.BreakdownDualWieldPenalty = additionalAttackModifier;

        result.WeaponName = attackLabel;
        result.BaseDamageDiceStr = $"{damageCount}d{damageDice}";

        TryApplyNaturalAttackOnHitEffects(target, result, naturalAttackForOnHit);

        // D&D 3.5e Magic Stone: decrement charges after attack (magic DR bypass set in damage packet)
        if (magicStoneUsed)
        {
            result.WeaponName = "Magic Stone (Sling)";
            Stats.MagicStoneCharges--;
            if (Stats.MagicStoneCharges <= 0)
            {
                Stats.MagicStoneActive = false;
                Stats.MagicStoneCharges = 0;
                Debug.Log($"[MagicStone] {Stats.CharacterName}: All magic stones discharged, spell ends.");
                // Remove the spell effect since all charges are used
                StatusEffectManager selfStatusMgr = StatusEffectManager;
                if (selfStatusMgr != null)
                    selfStatusMgr.RemoveEffectsBySpellId(SpellNames.MAGIC_STONE);
            }
            else
            {
                Debug.Log($"[MagicStone] {Stats.CharacterName}: {Stats.MagicStoneCharges} magic stone(s) remaining.");
            }
        }

        // HP tracking
        result.DefenderHPBefore = hpBefore;
        result.DefenderHPAfter = target.Stats.CurrentHP;

        // Off-hand shield bash in single-attack/off-hand flow must suppress shield AC
        // when Improved Shield Bash is not present.
        if (isOffHandAttack && IsShieldBashWeapon(equippedWeapon))
            SuppressShieldBonusForShieldBash(equippedWeapon);

        if (equippedWeapon != null && equippedWeapon.RequiresReload)
        {
            string reloadStateMessage = OnWeaponFired(equippedWeapon);
            if (!string.IsNullOrEmpty(reloadStateMessage))
                Debug.Log($"[Reload] {Stats.CharacterName}: {reloadStateMessage}");
        }

        // The hostile-action notices (charm, command undead, fascination, combat noise) are sent per swing by
        // PerformSingleAttackWithCrit, the core every weapon attack path shares (GameManager.NotifyWeaponAttackAgainst).

        HasAttackedThisTurn = true;
        return result;
    }

    /// <summary>
    /// Returns how many attacks this character can make during a full attack right now,
    /// including Rapid Shot when active with a ranged weapon.
    /// </summary>
    public int GetPlannedFullAttackCount(RangeInfo rangeInfo = null)
    {
        ItemData equippedWeapon = GetEquippedMainWeapon();
        if (ShouldUseInnateNaturalAttackProfile(equippedWeapon))
            return Mathf.Max(0, GetNaturalAttackStepBudget()); // with Haste's extra natural attack (CMB-106)

        // A flurry's steps, with Haste's extra attack: the same list FullAttack resolves (CHR-003).
        if (IsUsingFlurryOfBlows)
            return Mathf.Max(0, GetIterativeAttackCount());

        int[] attackBonuses = Stats.GetIterativeAttackBonuses();
        int count = attackBonuses != null ? attackBonuses.Length : 0;

        bool useThrownRange = equippedWeapon != null
            && equippedWeapon.IsThrown
            && equippedWeapon.RangeIncrement > 0
            && rangeInfo != null
            && !rangeInfo.IsMelee;
        bool isRanged = (equippedWeapon != null && (equippedWeapon.WeaponCat == WeaponCategory.Ranged || useThrownRange))
                        && rangeInfo != null && !rangeInfo.IsMelee;

        bool hasRapidShotFeat = Stats.HasFeat("Rapid Shot");
        bool rapidShotActive = isRanged && hasRapidShotFeat && RapidShotEnabled;
        if (rapidShotActive)
            count += 1;

        return Mathf.Max(0, count);
    }
    // ========== FULL ATTACK (Full-Round Action) ==========

    /// <summary>
    /// Perform a Full Attack action - all iterative attacks based on BAB.
    /// Each attack can independently threaten and confirm a critical hit.
    /// Includes racial attack bonuses, range penalties, and feat effects.
    /// Rapid Shot: extra attack at highest BAB, -2 to all ranged attacks.
    /// Power Attack: penalty to melee attack, bonus to melee damage.
    /// Point Blank Shot: +1 atk/dmg for ranged within 30 ft.
    /// Haste: one extra attack. With a weapon (or unarmed) at the highest BAB; for a creature fighting
    /// with its natural attacks one extra natural attack after the others, with natural attack
    /// <paramref name="hasteNaturalAttackIndex"/> (-1: <see cref="GetDefaultHasteNaturalAttackIndex"/>)
    /// at that attack's normal bonus (PHB p.239; owner decision 2026-10-07, CMB-106).
    /// </summary>
    public FullAttackResult FullAttack(CharacterController target, bool isFlanking, int flankingBonus, string flankingPartnerName, RangeInfo rangeInfo = null, int startAttackIndex = 0, int maxAttacks = int.MaxValue,
        int hasteNaturalAttackIndex = -1)
    {
        var result = new FullAttackResult();
        result.Type = FullAttackResult.AttackType.FullAttack;
        result.Attacker = this;
        result.Defender = target;

        if (target == null || target.Stats == null || target.Stats.IsDead)
        {
            Debug.LogWarning($"[FullAttack] {Stats?.CharacterName ?? name} attempted a full attack on an invalid target.");
            result.DefenderHPBefore = 0;
            result.DefenderHPAfter = 0;
            result.TargetKilled = false;
            return result;
        }

        result.DefenderHPBefore = target.Stats.CurrentHP;

        // Get equipped weapon for damage modifier and feat calculations
        ItemData equippedWeapon = GetEquippedMainWeapon();
        if (!CanAttackWithWeapon(equippedWeapon, out string cannotAttackReason))
        {
            Debug.LogWarning($"[FullAttack] {Stats.CharacterName}: {cannotAttackReason}");
            result.DefenderHPAfter = target.Stats.CurrentHP;
            result.TargetKilled = target.Stats.IsDead;
            return result;
        }

        bool isRanged = IsRangedWeaponAttack(equippedWeapon, rangeInfo);
        bool isMelee = !isRanged;

        // Shared per-attack modifier (CMB-043): the same terms as a single Attack; each attack
        // below only swaps in its BAB step, sequence penalty, Aid Another and Magic Stone.
        AttackBonusBreakdown sequenceBonus = BuildAttackBonus(target, equippedWeapon, isRanged, rangeInfo,
            isFlanking, flankingBonus, Stats.BaseAttackBonus, IsWeaponTwoHanded(equippedWeapon), RapidShotEnabled);
        bool rapidShotActive = sequenceBonus.Feats.RapidShotActive;
        DamageModeAttackProfile damageModeProfile = ResolveDamageModeAttackProfile(equippedWeapon);
        ResolveBaseAttackDamageProfile(equippedWeapon, out int damageDice, out int damageCount, out int bonusDamage, out string attackLabel,
            IsThrownWeaponAttack(equippedWeapon, rangeInfo));

        bool useNaturalAttackSequence = isMelee && ShouldUseInnateNaturalAttackProfile(equippedWeapon);
        if (useNaturalAttackSequence)
        {
            List<NaturalAttackDefinition> naturalAttacks = Stats.GetValidNaturalAttacks();
            if (startAttackIndex < 0)
                startAttackIndex = 0;

            // The natural sequence in order (each attack repeated by its Count), then Haste's extra
            // natural attack with the chosen natural attack (CMB-106).
            var naturalSteps = new List<(NaturalAttackDefinition attack, int repeat, bool haste)>();
            for (int naturalIndex = 0; naturalIndex < naturalAttacks.Count; naturalIndex++)
            {
                int attackCount = Mathf.Max(1, naturalAttacks[naturalIndex].Count);
                for (int repeat = 0; repeat < attackCount; repeat++)
                    naturalSteps.Add((naturalAttacks[naturalIndex], repeat, false));
            }

            // Only while it is unused this turn: it is one attack per full attack. A grapple attack action
            // that takes the Haste step (ResolveGrappleNaturalAttack, CMB-127) has marked it used already
            // and resolves its natural attack by index instead.
            if (CanUseHasteExtraNaturalAttack() && naturalSteps.Count > 0)
            {
                int hasteIndex = ResolveNaturalAttackIndexForStep(naturalSteps.Count, hasteNaturalAttackIndex);
                if (hasteIndex >= 0 && hasteIndex < naturalSteps.Count)
                    naturalSteps.Add((naturalSteps[hasteIndex].attack, naturalSteps[hasteIndex].repeat, true));
            }

            int naturalAttacksExecuted = 0;
            for (int stepIndex = startAttackIndex; stepIndex < naturalSteps.Count; stepIndex++)
            {
                if (naturalAttacksExecuted >= maxAttacks)
                    break;

                if (target.Stats.IsDead)
                    break;

                NaturalAttackDefinition naturalAttack = naturalSteps[stepIndex].attack;
                int repeat = naturalSteps[stepIndex].repeat;
                bool isHasteExtraAttack = naturalSteps[stepIndex].haste;

                // Every natural attack is at full BAB; secondary attacks take -5, or -2 with Multiattack (MM p.312, p.304).
                AttackBonusBreakdown atkBonus = sequenceBonus;
                atkBonus.BaseAttackBonus = Stats.BaseAttackBonus;
                atkBonus.SequenceModifier = Stats.GetNaturalAttackSequencePenalty(naturalAttack);
                atkBonus.SequenceLabel = "secondary natural attack";
                atkBonus.AidAnotherBonus = ConsumeAidAnotherAttackBonus(target);
                int aidAnotherTargetAcBonus = ConsumeAidAnotherAcBonus(target);

                int hpBeforeAtk = target.Stats.CurrentHP;
                // The natural attack's own Strength share (MM p.312: half for a secondary attack) and the shared terms (CMB-003).
                WeaponDamageBreakdown naturalDamage = BuildWeaponDamageBonus(equippedWeapon, isRanged: false, isOffHand: false,
                    atkBonus.Feats, naturalAttack: naturalAttack);

                Stats.GetScaledNaturalAttackDamage(naturalAttack, out int naturalDamageCount, out int naturalDamageDice);

                CombatResult atk = PerformSingleAttackWithCrit(target, atkBonus.Total, isFlanking, flankingBonus, flankingPartnerName,
                    naturalDamageDice, naturalDamageCount, naturalDamage,
                    atkBonus.CritThreatMin, atkBonus.CritMultiplier,
                    equippedWeapon, aidAnotherTargetAcBonus,
                    damageModeProfile.DealNonlethalDamage, damageModeProfile.AttackPenalty, damageModeProfile.PenaltySource);

                atkBonus.ApplyToResult(atk, rangeInfo);
                atk.AidAnotherTargetAcBonus = aidAnotherTargetAcBonus;
                atk.FightingDefensivelyACBonus = target != null && target.IsFightingDefensively ? 2 : 0;
                atk.WeaponName = string.IsNullOrWhiteSpace(naturalAttack.Name) ? "Natural attack" : naturalAttack.Name;
                atk.BaseDamageDiceStr = $"{naturalDamageCount}d{naturalDamageDice}";
                atk.DefenderHPBefore = hpBeforeAtk;
                atk.DefenderHPAfter = target.Stats.CurrentHP;

                TryApplyNaturalAttackOnHitEffects(target, atk, naturalAttack);
                if (isHasteExtraAttack)
                    MarkHasteExtraNaturalAttackUsed();
                else
                    ProgressiveAttackPool.MarkNaturalAttackUsed(stepIndex); // its natural-sequence index (CMB-127)

                result.Attacks.Add(atk);
                string naturalLabel = string.IsNullOrWhiteSpace(naturalAttack.Name) ? "Natural" : naturalAttack.Name;
                string roleLabel = naturalAttack.IsPrimary ? "Primary" : "Secondary";
                result.AttackLabels.Add(isHasteExtraAttack
                    ? $"{naturalLabel} (Haste, {roleLabel} {CharacterStats.FormatMod(atkBonus.LabelBonus)})"
                    : $"{naturalLabel} {repeat + 1} ({roleLabel} {CharacterStats.FormatMod(atkBonus.LabelBonus)})");
                naturalAttacksExecuted++;
            }

            result.DefenderHPAfter = target.Stats.CurrentHP;
            result.TargetKilled = target.Stats.IsDead;
            HasAttackedThisTurn = result.Attacks.Count > 0;
            return result;
        }

        // === Debug Logging ===
        Debug.Log($"[FullAttack] {Stats.CharacterName}: FullAttack() called");
        Debug.Log($"[FullAttack] Weapon: {(equippedWeapon != null ? equippedWeapon.Name : "(unarmed)")}, Ranged: {isRanged}, ability: {sequenceBonus.AbilityName} {CharacterStats.FormatMod(sequenceBonus.AbilityMod)}, morale: {CharacterStats.FormatMod(sequenceBonus.MoraleBonus)}");
        Debug.Log($"[FullAttack] Feats: WF={sequenceBonus.Feats.WeaponFocusBonus}, WS={sequenceBonus.Feats.WeaponSpecDamageBonus}, PA={sequenceBonus.Feats.PowerAttackDamageBonus}, CE={sequenceBonus.Feats.CombatExpertisePenalty}, effect damage={CharacterStats.FormatMod(Stats.EffectWeaponDamageBonus)}");
        if (rapidShotActive) Debug.Log($"[FullAttack] Rapid Shot active: -2 penalty, +1 extra attack");

        // A flurry of blows (PHB p.40-41, CHR-003): its own BAB steps (the iterative steps plus the extra attack, two from
        // 11th level, at the highest), with unarmed strikes or special monk weapons in melee only. The penalty is in
        // sequenceBonus (BuildAttackBonus) and the full Strength bonus in the step damage below.
        bool flurry = IsUsingFlurryOfBlows;
        if (flurry && (isRanged || !IsFlurryOfBlowsWeapon(equippedWeapon)))
        {
            Debug.LogWarning($"[FullAttack] {Stats.CharacterName}: {(equippedWeapon != null ? equippedWeapon.Name : "this attack")} cannot be part of a flurry of blows (PHB p.40)");
            result.DefenderHPAfter = target.Stats.CurrentHP;
            result.TargetKilled = target.Stats.IsDead;
            return result;
        }

        // Build the list of BAB steps, inserting the Rapid Shot extra attack
        int[] babSteps = flurry ? Stats.GetFlurryOfBlowsBaseAttackBonuses() : Stats.GetIterativeBaseAttackBonuses();
        var allBabSteps = new List<int>(babSteps);
        int baseAttackCount = allBabSteps.Count;

        if (rapidShotActive)
        {
            allBabSteps.Insert(0, babSteps[0]);
            Debug.Log($"[FullAttack] Rapid Shot: attack count {baseAttackCount} → {allBabSteps.Count}");
        }
        else if (RapidShotEnabled && Stats.HasFeat("Rapid Shot") && !isRanged)
        {
            Debug.LogWarning($"[FullAttack] {Stats.CharacterName}: Rapid Shot ON but weapon is not ranged");
        }

        // Haste grants one extra attack at highest BAB (PHB p.239)
        if (HasActiveHasteEffect && ActiveHasteEffect.GrantsExtraAttack)
        {
            allBabSteps.Add(babSteps[0]);
            Debug.Log($"[FullAttack] Haste: extra attack at highest BAB, attack count → {allBabSteps.Count}");
        }

        if (startAttackIndex < 0)
            startAttackIndex = 0;

        int attacksExecuted = 0;
        for (int i = startAttackIndex; i < allBabSteps.Count; i++)
        {
            if (attacksExecuted >= maxAttacks)
                break;

            if (target.Stats.IsDead)
            {
                Debug.Log($"[FullAttack] Target is dead, stopping at attack {i + 1}");
                break;
            }

            AttackBonusBreakdown atkBonus = sequenceBonus;
            atkBonus.BaseAttackBonus = allBabSteps[i];
            atkBonus.AidAnotherBonus = ConsumeAidAnotherAttackBonus(target);
            int aidAnotherTargetAcBonus = ConsumeAidAnotherAcBonus(target);
            // Magic Stone charges run out mid-sequence, so re-check per attack (PHB p.251).
            atkBonus.MagicStoneBonus = GetMagicStoneAttackBonus(equippedWeapon, isRanged);

            string label;
            if (rapidShotActive && i == 0)
                label = $"Attack 1 (Rapid Shot, {CharacterStats.FormatMod(atkBonus.LabelBonus)})";
            else if (flurry)
                label = $"Flurry {i + 1} ({CharacterStats.FormatMod(atkBonus.LabelBonus)})";
            else
                label = $"Attack {i + 1} ({CharacterStats.FormatMod(atkBonus.LabelBonus)})";

            int hpBeforeAtk = target.Stats.CurrentHP;

            // D&D 3.5e Magic Stone: per-attack damage override for sling full attack (PHB p.251)
            int atkDamageDice = damageDice;
            int atkDamageCount = damageCount;
            int atkBonusDamage = bonusDamage;
            bool fullAtkMagicStoneUsed = atkBonus.MagicStoneBonus > 0;
            if (fullAtkMagicStoneUsed)
            {
                atkDamageDice = 6;
                atkDamageCount = 1;
                atkBonusDamage = 1;
            }

            WeaponDamageBreakdown stepDamage;
            _resolvingFlurryOfBlowsAttack = flurry;
            try
            {
                stepDamage = BuildWeaponDamageBonus(equippedWeapon, isRanged, isOffHand: false, atkBonus.Feats,
                    atkBonusDamage, fullAtkMagicStoneUsed ? "Magic Stone" : null);
            }
            finally
            {
                _resolvingFlurryOfBlowsAttack = false;
            }
            CombatResult atk = PerformSingleAttackWithCrit(target, atkBonus.Total, isFlanking, flankingBonus, flankingPartnerName,
                atkDamageDice, atkDamageCount, stepDamage, atkBonus.CritThreatMin, atkBonus.CritMultiplier,
                equippedWeapon, aidAnotherTargetAcBonus,
                damageModeProfile.DealNonlethalDamage, damageModeProfile.AttackPenalty, damageModeProfile.PenaltySource);

            // D&D 3.5e Magic Stone: decrement charges after each full attack hit
            if (fullAtkMagicStoneUsed)
            {
                atk.WeaponName = "Magic Stone (Sling)";
                Stats.MagicStoneCharges--;
                if (Stats.MagicStoneCharges <= 0)
                {
                    Stats.MagicStoneActive = false;
                    Stats.MagicStoneCharges = 0;
                    Debug.Log($"[MagicStone] {Stats.CharacterName}: All magic stones discharged during full attack, spell ends.");
                    StatusEffectManager selfStatusMgr = StatusEffectManager;
                    if (selfStatusMgr != null)
                        selfStatusMgr.RemoveEffectsBySpellId(SpellNames.MAGIC_STONE);
                }
            }

            atkBonus.ApplyToResult(atk, rangeInfo);
            atk.AidAnotherTargetAcBonus = aidAnotherTargetAcBonus;
            atk.FightingDefensivelyACBonus = target != null && target.IsFightingDefensively ? 2 : 0;

            if (!fullAtkMagicStoneUsed)
                atk.WeaponName = attackLabel;
            atk.BaseDamageDiceStr = $"{atkDamageCount}d{atkDamageDice}";

            atk.DefenderHPBefore = hpBeforeAtk;
            atk.DefenderHPAfter = target.Stats.CurrentHP;

            result.Attacks.Add(atk);
            result.AttackLabels.Add(label);
            attacksExecuted++;
        }

        result.DefenderHPAfter = target.Stats.CurrentHP;
        result.TargetKilled = target.Stats.IsDead;

        if (equippedWeapon != null && equippedWeapon.RequiresReload && result.Attacks.Count > 0)
        {
            string reloadStateMessage = OnWeaponFired(equippedWeapon);
            if (!string.IsNullOrEmpty(reloadStateMessage))
                Debug.Log($"[Reload] {Stats.CharacterName}: {reloadStateMessage}");
        }

        HasAttackedThisTurn = result.Attacks.Count > 0;
        return result;
    }

    // ========== DUAL WIELD ATTACK (Full-Round Action) ==========

    /// <summary>
    /// Check if this character can make a dual-wield/off-hand attack sequence.
    /// Supports: left-hand weapon, shield bash off-hand, and spiked gauntlet off-hand.
    /// </summary>
    public bool CanDualWield()
    {
        return EnsureEquipment().CanDualWield();
    }

    /// <summary>
    /// Returns true when the currently resolved main weapon is being used two-handed.
    /// </summary>
    public bool IsTwoHanding()
    {
        return EnsureEquipment().IsTwoHanding();
    }

    /// <summary>
    /// Returns the resolved primary weapon for dual-wield style attacks, if any.
    /// </summary>
    public ItemData GetDualWieldMainWeapon()
    {
        return EnsureEquipment().GetDualWieldMainWeapon();
    }

    /// <summary>
    /// Returns the resolved off-hand weapon for dual-wield style attacks, if any.
    /// This can be a left-hand weapon, a shield bash profile from the left-hand shield,
    /// or a spiked gauntlet from the Hands slot.
    /// </summary>
    public ItemData GetDualWieldOffHandWeapon()
    {
        return EnsureEquipment().GetDualWieldOffHandWeapon();
    }

    /// <summary>
    /// Returns true if this character currently has a valid off-hand attack option.
    /// Valid options: off-hand weapon, shield bash profile, or hands-slot spiked gauntlet.
    /// </summary>
    public bool HasOffHandWeaponEquipped()
    {
        return EnsureEquipment().HasOffHandWeaponEquipped();
    }

    public bool HasThrowableOffHandWeaponEquipped()
    {
        return EnsureEquipment().HasThrowableOffHandWeaponEquipped();
    }

    /// <summary>
    /// Returns the concrete weapon profile used for a separate off-hand attack button.
    /// </summary>
    public ItemData GetOffHandAttackWeapon()
    {
        return EnsureEquipment().GetOffHandAttackWeapon();
    }

    /// <summary>
    /// True if the current dual-wield off-hand attack option comes from a spiked gauntlet in the Hands slot.
    /// </summary>
    public bool IsDualWieldOffHandSpikedGauntlet()
    {
        return EnsureEquipment().IsDualWieldOffHandSpikedGauntlet();
    }

    /// <summary>
    /// True if the current dual-wield off-hand attack option is a shield bash.
    /// </summary>
    public bool IsDualWieldOffHandShieldBash()
    {
        return EnsureEquipment().IsDualWieldOffHandShieldBash();
    }

    /// <summary>
    /// Returns true when the currently equipped off-hand weapon counts as a light weapon.
    /// D&D 3.5e rule support: a light-category off-hand weapon reduces TWF penalties.
    /// </summary>
    public bool IsOffHandWeaponLight()
    {
        return EnsureEquipment().IsOffHandWeaponLight();
    }

    /// <summary>
    /// Get the dual wield penalty information.
    /// Returns (mainHandPenalty, offHandPenalty, isLightOffHand).
    /// Without TWF feat: -6/-10 (normal) or -4/-8 (light off-hand).
    /// With TWF feat: -4/-4 (normal) or -2/-2 (light off-hand).
    /// </summary>
    public (int mainPenalty, int offPenalty, bool lightOffHand) GetDualWieldPenalties()
    {
        return EnsureEquipment().GetDualWieldPenalties();
    }

    // ========== INVENTORY WRAPPERS ==========

    public Inventory GetInventoryData()
    {
        return EnsureInventory().GetInventory();
    }

    public bool AddItem(ItemData item)
    {
        return EnsureInventory().AddItem(item);
    }

    public bool RemoveItem(ItemData item)
    {
        return EnsureInventory().RemoveItem(item);
    }

    public List<ItemData> GetAllInventoryItems()
    {
        return EnsureInventory().GetAllItems();
    }

    public int GetGeneralInventoryItemCount()
    {
        return EnsureInventory().GetGeneralInventoryItemCount();
    }

    public float GetTotalCarriedWeightLbs()
    {
        return EnsureInventory().GetTotalCarriedWeightLbs();
    }

    public int GetConsumableInventoryCount()
    {
        return EnsureInventory().GetConsumableCount();
    }

    /// <summary>The attack modifier this creature's Grappled (or Grappling) condition rows add to ConditionAttackPenalty (-4 each).</summary>
    private int GetGrappledConditionAttackModifier()
    {
        if (Stats == null || Stats.ActiveConditions == null)
            return 0;

        int total = 0;
        for (int i = 0; i < Stats.ActiveConditions.Count; i++)
        {
            StatusEffect condition = Stats.ActiveConditions[i];
            if (condition != null && ConditionRules.Normalize(condition.Type) == CombatConditionType.Grappled)
                total += ConditionRules.GetDefinition(condition.Type).AttackModifier;
        }

        return total;
    }

    /// <summary>
    /// The rake's extra claw attacks (MM p.314) against <paramref name="target"/>, at the full BAB with no -4 for
    /// grappling. Callers: the pounce (a charge's full attack) and the grapple rake (<see cref="ResolveGrappleRake"/>,
    /// which checks the turn-start and full-attack rules, CMB-144).
    /// </summary>
    public FullAttackResult PerformRakeAttacks(CharacterController target, bool isFlanking, int flankingBonus, string flankingPartnerName)
    {
        var result = new FullAttackResult
        {
            Type = FullAttackResult.AttackType.FullAttack,
            Attacker = this,
            Defender = target,
            DefenderHPBefore = target != null && target.Stats != null ? target.Stats.CurrentHP : 0
        };

        if (target == null || target.Stats == null || Stats == null || target.Stats.IsDead)
        {
            result.DefenderHPAfter = result.DefenderHPBefore;
            return result;
        }

        NaturalAttackDefinition rakeAttack = Stats.GetRakeAttackDefinition();
        if (rakeAttack == null)
        {
            result.DefenderHPAfter = target.Stats.CurrentHP;
            return result;
        }

        int critThreatMin = 20;
        int critMult = 2;
        int attackCount = Mathf.Max(1, rakeAttack.Count);
        int armorNonProfPenalty = Stats.GetArmorNonProficiencyAttackPenalty();
        // Rake attacks are not subject to the usual -4 for attacking with a natural weapon in a grapple (MM p.314,
        // CMB-144): leave the Grappled row's attack modifier out; every other condition still applies.
        int conditionAttackPenalty = Stats.ConditionAttackPenalty - GetGrappledConditionAttackModifier();
        int solidFogAtkPenalty = Stats.SolidFogMeleeAttackPenalty; // rake is always melee
        // A natural weapon may deal nonlethal damage at -4 like any lethal melee weapon (PHB p.146; delegated RAW reading 2026-10-14).
        DamageModeAttackProfile rakeDamageMode = GetRakeDamageModeProfile();

        for (int i = 0; i < attackCount; i++)
        {
            if (target.Stats.IsDead)
                break;

            // D&D 3.5e: rake attacks use PRIMARY attack bonuses (no -5 secondary penalty)
            int baseBonus = Stats.BaseAttackBonus + Stats.STRMod + Stats.SizeModifier;
            int atkMod = baseBonus + (isFlanking ? flankingBonus : 0) + armorNonProfPenalty + conditionAttackPenalty + solidFogAtkPenalty
                + rakeDamageMode.AttackPenalty;
            int hpBeforeAtk = target.Stats.CurrentHP;

            Stats.GetScaledNaturalAttackDamage(rakeAttack, out int damageCount, out int damageDice);

            // The rake's own Strength share (its data, MM p.314) plus the shared damage terms (CMB-003). No feat terms:
            // the rake's attack modifier is still built by hand (CMB-087).
            WeaponDamageBreakdown rakeDamage = BuildWeaponDamageBonus(null, isRanged: false, isOffHand: false,
                default(AttackCalculator.FeatModifiers), naturalAttack: rakeAttack);

            CombatResult atk = PerformSingleAttackWithCrit(
                target,
                atkMod,
                isFlanking,
                flankingBonus,
                flankingPartnerName,
                damageDice,
                damageCount,
                rakeDamage,
                critThreatMin,
                critMult,
                null,
                situationalTargetAcBonus: 0,
                dealNonlethalDamage: rakeDamageMode.DealNonlethalDamage,
                damageModeAttackPenalty: rakeDamageMode.AttackPenalty,
                damageModePenaltySource: rakeDamageMode.PenaltySource);

            atk.WeaponName = string.IsNullOrWhiteSpace(rakeAttack.Name) ? "Rake" : rakeAttack.Name;
            atk.BreakdownBAB = baseBonus;
            atk.BreakdownAbilityMod = Stats.STRMod;
            atk.BreakdownAbilityName = "STR";
            atk.WeaponNonProficiencyPenalty = 0;
            atk.ArmorNonProficiencyPenalty = armorNonProfPenalty;
            atk.DefenderHPBefore = hpBeforeAtk;
            atk.DefenderHPAfter = target.Stats.CurrentHP;
            atk.BaseDamageDiceStr = $"{damageCount}d{damageDice}";

            result.Attacks.Add(atk);
            result.AttackLabels.Add($"Rake {i + 1} ({CharacterStats.FormatMod(baseBonus)})");
        }

        // The rake's extra claw attacks come once a turn (MM p.314), whichever path made them (CMB-127).
        if (result.Attacks.Count > 0)
            ProgressiveAttackPool.MarkRakeUsed();

        result.DefenderHPAfter = target.Stats.CurrentHP;
        result.TargetKilled = target.Stats.IsDead;
        return result;
    }

    public FullAttackResult DualWieldAttack(CharacterController target, bool isFlanking, int flankingBonus, string flankingPartnerName, RangeInfo rangeInfo = null)
    {
        var result = new FullAttackResult();
        result.Type = FullAttackResult.AttackType.DualWield;
        result.Attacker = this;
        result.Defender = target;
        result.DefenderHPBefore = target.Stats.CurrentHP;

        if (HasCondition(CombatConditionType.Grappled))
        {
            Debug.LogWarning($"[DualWield] {Stats.CharacterName} cannot dual-wield while grappled.");
            return result;
        }

        if (!CanDualWield())
            return result;

        ItemData mainWeapon = GetDualWieldMainWeapon();
        ItemData offWeapon = GetDualWieldOffHandWeapon();
        bool offHandFromSpikedGauntlet = IsDualWieldOffHandSpikedGauntlet();
        if (mainWeapon == null || offWeapon == null)
            return result;

        bool canMainAttack = CanAttackWithWeapon(mainWeapon, out string mainBlockedReason);
        bool canOffAttack = CanAttackWithWeapon(offWeapon, out string offBlockedReason);
        DamageModeAttackProfile mainDamageModeProfile = ResolveDamageModeAttackProfile(mainWeapon);
        DamageModeAttackProfile offDamageModeProfile = ResolveDamageModeAttackProfile(offWeapon);

        result.MainWeaponName = mainWeapon.Name;
        result.OffWeaponName = offWeapon.Name;

        var (mainPenalty, offPenalty, lightOff) = GetDualWieldPenalties();

        // Main-hand attack
        if (canMainAttack)
        {
            // Shared per-attack modifier (CMB-043), built for this hand's weapon. Each weapon is
            // held in one hand, so Power Attack is never doubled while dual-wielding.
            AttackBonusBreakdown mainBonus = BuildAttackBonus(target, mainWeapon, IsRangedWeaponAttack(mainWeapon, rangeInfo),
                rangeInfo, isFlanking, flankingBonus, Stats.BaseAttackBonus, isTwoHanded: false);
            mainBonus.SequenceModifier = mainPenalty; // shown as the "dual wield" entry
            mainBonus.MagicStoneBonus = 0; // Magic Stone is resolved only by Attack and FullAttack
            mainBonus.AidAnotherBonus = ConsumeAidAnotherAttackBonus(target);
            int mainAidAnotherTargetAcBonus = ConsumeAidAnotherAcBonus(target);
            string mainLabel = $"Attack 1 - Main Hand ({mainWeapon.Name})";

            WeaponDamageBreakdown mainDamage = BuildWeaponDamageBonus(mainWeapon, IsRangedWeaponAttack(mainWeapon, rangeInfo),
                isOffHand: false, mainBonus.Feats, mainWeapon.BonusDamage);

            GetScaledWeaponDamageDice(mainWeapon, out int mainDamageCount, out int mainDamageDice, IsThrownWeaponAttack(mainWeapon, rangeInfo));

            int hpBeforeMain = target.Stats.CurrentHP;
            CombatResult mainAtk = PerformSingleAttackWithCrit(target, mainBonus.Total, isFlanking, flankingBonus, flankingPartnerName,
                mainDamageDice, mainDamageCount, mainDamage, mainBonus.CritThreatMin, mainBonus.CritMultiplier,
                mainWeapon, mainAidAnotherTargetAcBonus,
                mainDamageModeProfile.DealNonlethalDamage, mainDamageModeProfile.AttackPenalty, mainDamageModeProfile.PenaltySource);

            mainBonus.ApplyToResult(mainAtk, rangeInfo);
            mainAtk.AidAnotherTargetAcBonus = mainAidAnotherTargetAcBonus;
            mainAtk.FightingDefensivelyACBonus = target != null && target.IsFightingDefensively ? 2 : 0;
            mainAtk.WeaponName = mainWeapon.Name;
            mainAtk.BaseDamageDiceStr = $"{mainDamageCount}d{mainDamageDice}";
            mainAtk.IsDualWieldAttack = true;
            mainAtk.IsOffHandAttack = false;
            mainAtk.BreakdownDualWieldPenalty = mainPenalty;
            mainAtk.DefenderHPBefore = hpBeforeMain;
            mainAtk.DefenderHPAfter = target.Stats.CurrentHP;

            result.Attacks.Add(mainAtk);
            result.AttackLabels.Add(mainLabel);

            if (mainWeapon.RequiresReload)
            {
                string reloadStateMessage = OnWeaponFired(mainWeapon);
                if (!string.IsNullOrEmpty(reloadStateMessage))
                    Debug.Log($"[Reload] {Stats.CharacterName}: {reloadStateMessage}");
            }
        }
        else
        {
            Debug.LogWarning($"[DualWield] {Stats.CharacterName} main-hand attack skipped: {mainBlockedReason}");
        }
        // Off-hand attack
        if (!target.Stats.IsDead && canOffAttack)
        {
            // Same shared modifier for the off-hand weapon: its own Weapon Finesse, Weapon Focus and crit.
            AttackBonusBreakdown offBonus = BuildAttackBonus(target, offWeapon, IsRangedWeaponAttack(offWeapon, rangeInfo),
                rangeInfo, isFlanking, flankingBonus, Stats.BaseAttackBonus, isTwoHanded: false);
            offBonus.SequenceModifier = offPenalty; // shown as the "off-hand" entry
            offBonus.MagicStoneBonus = 0;
            offBonus.AidAnotherBonus = ConsumeAidAnotherAttackBonus(target);
            int offAidAnotherTargetAcBonus = ConsumeAidAnotherAcBonus(target);
            bool offHandShieldBash = IsShieldBashWeapon(offWeapon);
            string offLabel = offHandFromSpikedGauntlet
                ? $"Attack 2 - Off Hand ({offWeapon.Name}, Hands Slot)"
                : offHandShieldBash
                    ? $"Attack 2 - Off Hand (Shield Bash: {offWeapon.Name})"
                    : $"Attack 2 - Off Hand ({offWeapon.Name})";

            WeaponDamageBreakdown offDamage = BuildWeaponDamageBonus(offWeapon, IsRangedWeaponAttack(offWeapon, rangeInfo),
                isOffHand: true, offBonus.Feats, offWeapon.BonusDamage);

            GetScaledWeaponDamageDice(offWeapon, out int offDamageCount, out int offDamageDice, IsThrownWeaponAttack(offWeapon, rangeInfo));

            int hpBeforeOff = target.Stats.CurrentHP;
            CombatResult offAtk = PerformSingleAttackWithCrit(target, offBonus.Total, isFlanking, flankingBonus, flankingPartnerName,
                offDamageDice, offDamageCount, offDamage, offBonus.CritThreatMin, offBonus.CritMultiplier,
                offWeapon, offAidAnotherTargetAcBonus,
                offDamageModeProfile.DealNonlethalDamage, offDamageModeProfile.AttackPenalty, offDamageModeProfile.PenaltySource);

            offBonus.ApplyToResult(offAtk, rangeInfo);
            offAtk.AidAnotherTargetAcBonus = offAidAnotherTargetAcBonus;
            offAtk.FightingDefensivelyACBonus = target != null && target.IsFightingDefensively ? 2 : 0;
            offAtk.WeaponName = offWeapon.Name;
            offAtk.BaseDamageDiceStr = $"{offDamageCount}d{offDamageDice}";
            offAtk.IsDualWieldAttack = true;
            offAtk.IsOffHandAttack = true;
            offAtk.BreakdownDualWieldPenalty = offPenalty;
            offAtk.DefenderHPBefore = hpBeforeOff;
            offAtk.DefenderHPAfter = target.Stats.CurrentHP;

            result.Attacks.Add(offAtk);
            result.AttackLabels.Add(offLabel);

            if (IsShieldBashWeapon(offWeapon))
            {
                SuppressShieldBonusForShieldBash(offWeapon);
            }

            if (offWeapon.RequiresReload)
            {
                string reloadStateMessage = OnWeaponFired(offWeapon);
                if (!string.IsNullOrEmpty(reloadStateMessage))
                    Debug.Log($"[Reload] {Stats.CharacterName}: {reloadStateMessage}");
            }
        }
        else if (!canOffAttack)
        {
            Debug.LogWarning($"[DualWield] {Stats.CharacterName} off-hand attack skipped: {offBlockedReason}");
        }

        result.DefenderHPAfter = target.Stats.CurrentHP;
        result.TargetKilled = target.Stats.IsDead;
        HasAttackedThisTurn = result.Attacks.Count > 0;
        return result;
    }

    private int GetProneAttackModifier(bool isMeleeAttack)
    {
        if (!HasCondition(CombatConditionType.Prone)) return 0;
        return isMeleeAttack ? -4 : 0;
    }

    private static List<CharacterController> GetAllCombatCharactersSnapshot()
    {
        var all = new List<CharacterController>();
        var gm = GameManager.Instance;
        if (gm == null) return all;

        if (gm.PCs != null)
        {
            for (int i = 0; i < gm.PCs.Count; i++)
            {
                var pc = gm.PCs[i];
                if (pc == null || pc.Stats == null || pc.gameObject == null || !pc.gameObject.activeInHierarchy) continue;
                all.Add(pc);
            }
        }

        if (gm.NPCs != null)
        {
            for (int i = 0; i < gm.NPCs.Count; i++)
            {
                var npc = gm.NPCs[i];
                if (npc == null || npc.Stats == null || npc.gameObject == null || !npc.gameObject.activeInHierarchy) continue;
                all.Add(npc);
            }
        }

        return all;
    }

    private static bool IsTargetEngagedInMeleeWithAttackerAllies(CharacterController target, CharacterController attacker)
    {
        if (target == null || attacker == null || target.Stats == null || attacker.Stats == null) return false;

        // Use threat map: target is engaged in melee if a threatening enemy of target
        // (excluding attacker) is on the attacker's team.
        List<CharacterController> all = GetAllCombatCharactersSnapshot();
        if (all.Count == 0) return false;

        List<CharacterController> threateningEnemies = ThreatSystem.GetThreateningEnemies(target.GridPosition, target, all);
        if (threateningEnemies == null || threateningEnemies.Count == 0)
            return false;

        for (int i = 0; i < threateningEnemies.Count; i++)
        {
            CharacterController threatener = threateningEnemies[i];
            if (threatener == null || threatener == attacker) continue;
            if (threatener.Stats == null || threatener.Stats.IsDead) continue;

            if (threatener.Team == attacker.Team)
                return true;
        }

        return false;
    }

    private static int GetShootingIntoMeleePenalty(CharacterController attacker, CharacterController target, bool isRangedAttack, out bool preciseShotNegated)
    {
        preciseShotNegated = false;

        if (!isRangedAttack || attacker == null || target == null)
            return 0;

        bool engaged = IsTargetEngagedInMeleeWithAttackerAllies(target, attacker);
        if (!engaged)
            return 0;

        if (attacker.Stats != null && attacker.Stats.HasFeat("Precise Shot"))
        {
            preciseShotNegated = true;
            return 0;
        }

        return -4;
    }

    private static int GetSituationalTargetArmorClass(CharacterController target, CharacterController attacker, bool isRangedAttack)
    {
        if (target == null || target.Stats == null)
            return 10;

        int targetAC = target.Stats.ArmorClass;

        // D&D 3.5e: invisible defenders gain +2 AC only against attackers who cannot see them.
        targetAC += target.GetInvisibilityArmorClassBonusAgainst(attacker);

        if (target.HasCondition(CombatConditionType.Prone))
            targetAC += CombatCalculationService.ProneACModifier(isRangedAttack);

        // D&D 3.5 Pinned AC rule:
        // Pinned creatures take a -4 AC penalty against opponents other than the creature pinning them.
        if (target.HasCondition(CombatConditionType.Pinned))
        {
            bool attackerIsGrappleOpponent = target.TryGetGrappleState(out CharacterController grappleOpponent, out _, out _, out _)
                && grappleOpponent == attacker;
            if (!attackerIsGrappleOpponent)
                targetAC += CombatCalculationService.PinnedACPenalty;
        }

        if (target.IsFightingDefensively)
            targetAC += CombatCalculationService.FightingDefensivelyACBonus; // Dodge bonus

        // Racial dodge bonus against this attacker's kind: dwarf and gnome +4 against giants (PHB p.15, p.17; CHR-019).
        targetAC += RacialTraitRules.KindDodgeACBonusAgainst(target.Stats, attacker != null ? attacker.Stats : null);

        // Mounted AC bonus: +1 vs opponents on foot (higher ground, PHB p.157)
        int mountedACBonus = MountSystem.GetMountedACBonus(target, attacker);
        if (mountedACBonus > 0)
            targetAC += mountedACBonus;

        return targetAC;
    }

    private static int GetDexBonusAppliedToArmorClass(CharacterController target)
    {
        if (target == null || target.Stats == null)
            return 0;

        if (target.Stats.DeniedDexToAcByCondition)
            return 0;

        int dexToAc = target.Stats.DEXMod;
        if (target.Stats.MaxDexBonus >= 0 && dexToAc > target.Stats.MaxDexBonus)
            dexToAc = target.Stats.MaxDexBonus;

        return Mathf.Max(0, dexToAc);
    }

    /// <summary>
    /// D&D 3.5 Grapple AC rule:
    /// A grappled defender loses their DEX bonus to AC against attackers they are NOT grappling,
    /// but keeps DEX bonus to AC against the grapple opponent.
    /// </summary>
    private static int GetGrappleDexDeniedAgainstAttacker(CharacterController defender, CharacterController attacker, out string note)
    {
        note = string.Empty;

        if (defender == null || defender.Stats == null)
            return 0;

        if (!defender.HasCondition(CombatConditionType.Grappled))
            return 0;

        if (defender.TryGetGrappleState(out CharacterController grappleOpponent, out _, out _, out _)
            && grappleOpponent == attacker)
        {
            note = "Grapple: defender keeps DEX bonus to AC against current grapple opponent.";
            return 0;
        }

        int deniedDexBonus = GetDexBonusAppliedToArmorClass(defender);
        if (deniedDexBonus > 0)
        {
            note = "Grapple: defender loses DEX bonus to AC vs non-grappled attacker.";
        }
        else
        {
            note = "Grapple: defender has no positive DEX bonus to lose vs non-grappled attacker.";
        }

        return deniedDexBonus;
    }

    /// <summary>
    /// D&D 3.5 Pinned AC rule:
    /// Pinned defenders are immobile and lose their DEX bonus to AC against all attackers.
    /// They are NOT helpless.
    /// </summary>
    private static int GetPinnedDexDeniedAgainstAttacker(CharacterController defender, CharacterController attacker, out string note)
    {
        note = string.Empty;

        if (defender == null || defender.Stats == null || !defender.HasCondition(CombatConditionType.Pinned))
            return 0;

        int deniedDexBonus = GetDexBonusAppliedToArmorClass(defender);
        bool attackerIsGrappleOpponent = defender.TryGetGrappleState(out CharacterController grappleOpponent, out _, out _, out _)
            && grappleOpponent == attacker;

        if (attackerIsGrappleOpponent)
            note = deniedDexBonus > 0
                ? "Pinned: defender is immobile and loses DEX bonus to AC while pinned."
                : "Pinned: defender is immobile and has no positive DEX bonus to lose.";
        else
            note = deniedDexBonus > 0
                ? "Pinned: defender is immobile, loses DEX bonus to AC, and takes -4 AC vs non-grappling attackers."
                : "Pinned: defender is immobile and takes -4 AC vs non-grappling attackers.";

        return deniedDexBonus;
    }

    private void SetIncomingFeintIndicator(CharacterController attacker, bool active)
    {
        if (attacker == null || attacker == this)
            return;

        if (active)
        {
            _incomingFeintSources.Add(attacker);
            if (!HasCondition(CombatConditionType.Feinted))
                ApplyCondition(CombatConditionType.Feinted, -1, attacker.Stats != null ? attacker.Stats.CharacterName : "Feint");
            return;
        }

        _incomingFeintSources.Remove(attacker);

        // Trim dead/null references to avoid stale marker state.
        _incomingFeintSources.RemoveWhere(src => src == null || src.Stats == null || src.Stats.IsDead);

        if (_incomingFeintSources.Count == 0)
            RemoveCondition(CombatConditionType.Feinted);
    }

    private void ClearIncomingFeintIndicators()
    {
        if (_incomingFeintSources.Count > 0)
            _incomingFeintSources.Clear();

        RemoveCondition(CombatConditionType.Feinted);
    }

    private void ClearOwnedFeintWindowsAndIndicators()
    {
        if (_activeFeintWindows.Count == 0)
            return;

        for (int i = _activeFeintWindows.Count - 1; i >= 0; i--)
        {
            FeintWindow window = _activeFeintWindows[i];
            if (window != null && window.Target != null)
                window.Target.SetIncomingFeintIndicator(this, active: false);
        }

        _activeFeintWindows.Clear();
    }

    private void PruneExpiredFeintWindows()
    {
        if (_activeFeintWindows.Count == 0)
            return;

        for (int i = _activeFeintWindows.Count - 1; i >= 0; i--)
        {
            FeintWindow window = _activeFeintWindows[i];
            bool expired = window == null
                || window.Target == null
                || window.Target.Stats == null
                || window.Target.Stats.IsDead
                || _turnsStartedCount > window.ExpiresAfterTurnStartCount;

            if (!expired)
                continue;

            if (window != null && window.Target != null)
                window.Target.SetIncomingFeintIndicator(this, active: false);

            _activeFeintWindows.RemoveAt(i);
        }
    }

    private void RegisterSuccessfulFeint(CharacterController target)
    {
        if (target == null)
            return;

        PruneExpiredFeintWindows();

        FeintWindow existing = _activeFeintWindows.Find(w => w != null && w.Target == target);
        if (existing == null)
        {
            existing = new FeintWindow
            {
                Target = target,
                ExpiresAfterTurnStartCount = _turnsStartedCount + 1
            };
            _activeFeintWindows.Add(existing);
        }
        else
        {
            existing.ExpiresAfterTurnStartCount = _turnsStartedCount + 1;
        }

        target.SetIncomingFeintIndicator(this, active: true);
    }

    private bool TryConsumeFeintDexDenial(CharacterController target, bool isMeleeAttack, out int deniedDexBonus, out string note, out bool feintWindowConsumed)
    {
        deniedDexBonus = 0;
        note = string.Empty;
        feintWindowConsumed = false;

        if (!isMeleeAttack || target == null || target.Stats == null)
            return false;

        PruneExpiredFeintWindows();
        int idx = _activeFeintWindows.FindIndex(w => w != null && w.Target == target);
        if (idx < 0)
            return false;

        // D&D 3.5: the effect is for your next melee attack against the feinted target.
        // Consume the window regardless of whether it yields a numerical AC reduction.
        FeintWindow consumed = _activeFeintWindows[idx];
        _activeFeintWindows.RemoveAt(idx);
        if (consumed != null && consumed.Target != null)
            consumed.Target.SetIncomingFeintIndicator(this, active: false);

        feintWindowConsumed = true;

        // A flat-footed target is already denied its Dexterity bonus, unless it has uncanny dodge, which does not stop a
        // feint (PHB p.137, p.155; CMB-028).
        if (target.Stats.IsDexDeniedByFlatFooted)
        {
            note = "Feint window consumed: target already flat-footed (no extra DEX denial).";
            return false;
        }

        deniedDexBonus = GetDexBonusAppliedToArmorClass(target);
        if (deniedDexBonus <= 0)
        {
            note = "Feint window consumed: target has no positive DEX bonus to AC.";
            return false;
        }

        note = $"Feint: denied +{deniedDexBonus} DEX bonus to AC on this melee attack.";
        return true;
    }

    private bool IsTargetImmuneToSneakAttackDamage(CharacterController target)
    {
        if (target == null || target.Stats == null)
            return false;

        if (target.Stats.Immunities != null && target.Stats.Immunities.immuneToSneakAttack)
            return true;

        // PHB p.50: any creature immune to critical hits is not vulnerable to sneak attacks.
        if (target.IsImmuneToCriticalHits())
            return true;

        string creatureType = string.IsNullOrEmpty(target.Stats.CreatureType)
            ? string.Empty
            : target.Stats.CreatureType.Trim().ToLowerInvariant();

        // D&D 3.5 precision-damage immunity (minimum implemented set requested by design task).
        return creatureType == "undead"
            || creatureType == "construct"
            || creatureType == "ooze";
    }

    /// <summary>
    /// The one coup de grace distance test for the PC button and highlight, the AI and the resolver: the helpless
    /// target is adjacent or in one of this creature's own squares (Chebyshev distance 0 or 1). PHB p.153 asks for a
    /// melee weapon against a helpless opponent; a square-mate is in melee reach (PHB p.149, owner ruling CMB-131).
    /// Reach beyond adjacent squares is not accepted (unchanged).
    /// </summary>
    public bool IsInCoupDeGraceReach(CharacterController target)
    {
        if (target == null || target == this)
            return false;
        return GetMinimumDistanceToTarget(target, chebyshev: true) <= 1;
    }

    public bool IsHelplessForCoupDeGrace()
    {
        if (Stats == null || Stats.IsDead)
            return false;

        return IsUnconscious || IsHelplessLikeConditionState();
    }

    public bool IsImmuneToCriticalHits()
    {
        if (Stats == null)
            return false;

        if (Stats.Immunities != null && Stats.Immunities.immuneToCriticalHits)
            return true;

        string creatureType = string.IsNullOrEmpty(Stats.CreatureType)
            ? string.Empty
            : Stats.CreatureType.Trim().ToLowerInvariant();

        // A swarm has no discernible anatomy and is not subject to critical hits (MM p.316, swarm subtype).
        if (Stats.IsSwarm)
            return true;

        // Types not subject to critical hits (MM type traits: construct p.307, elemental p.308, ooze and plant p.313,
        // undead p.317); other creatures carry Immunities.immuneToCriticalHits (CMB-004).
        return creatureType == "undead"
            || creatureType == "construct"
            || creatureType == "ooze"
            || creatureType == "elemental"
            || creatureType == "plant";
    }

    private bool IsTargetDeniedDexForSneakAttack(CharacterController target, bool isMeleeAttack, bool feintWindowConsumed, out string reason)
    {
        reason = string.Empty;

        if (target == null)
            return false;

        if (isMeleeAttack && feintWindowConsumed)
        {
            reason = "feinted target (DEX denied)";
            return true;
        }

        // Flat-footed at the start of a battle (PHB p.137; CMB-028); uncanny dodge keeps the Dexterity bonus, so no sneak
        // attack for it (PHB p.26, p.50).
        if (target.Stats != null && target.Stats.IsDexDeniedByFlatFooted)
        {
            reason = "target is flat-footed";
            return true;
        }

        if (target.HasCondition(CombatConditionType.Stunned))
        {
            reason = "target is stunned";
            return true;
        }

        if (target.HasCondition(CombatConditionType.Paralyzed) || target.HasCondition(CombatConditionType.Helpless))
        {
            reason = "target is paralyzed/helpless";
            return true;
        }

        // Every other condition that denies the Dexterity bonus to AC, the same test as the AC formulas (blinded, DMG
        // p.300; cowering; pinned; asleep...), whether or not the target has a Dexterity bonus (PHB p.50). The global
        // Feinted mark is skipped: a feint counts only for the feinting attacker (feintWindowConsumed above).
        if (target.Stats != null && target.Stats.TryGetDexDenyingCondition(ignoreFeinted: true, out CombatConditionType dexDenyingCondition))
        {
            reason = "target is " + dexDenyingCondition.ToString().ToLowerInvariant() + " (DEX denied)";
            return true;
        }

        // A grappling creature loses its Dexterity bonus to AC against opponents it is not grappling (PHB p.156), as in
        // GetGrappleDexDeniedAgainstAttacker, whether or not it has a bonus to lose.
        if (target.HasCondition(CombatConditionType.Grappled)
            && !(target.TryGetGrappleState(out CharacterController sneakGrappleOpponent, out _, out _, out _) && sneakGrappleOpponent == this))
        {
            reason = "target is grappling someone else (DEX denied)";
            return true;
        }

        return false;
    }

    /// <summary>
    /// True when this creature's last perception check (its turn-start Listen check, or Spot against Hide with See
    /// Invisibility; <see cref="PerceptionRules.ProcessTurnStart"/>) pinpointed <paramref name="target"/>'s square, PC or
    /// NPC (DMG p.295; CMB-094). The target keeps its concealment: the attack still rolls the miss chance.
    /// </summary>
    public bool HasPinpointed(CharacterController target)
    {
        LastKnownPositionTracker tracker = GetComponent<LastKnownPositionTracker>();
        return tracker != null && target != null && tracker.IsPinpointedThisRound(target);
    }

    /// <summary>
    /// True when this creature can aim a ranged attack at <paramref name="target"/>: it sees it, or it has pinpointed its
    /// square (<see cref="HasPinpointed"/>; DMG p.295, PHB p.152; CMB-094). The PC target list and target click use it;
    /// a ranged attack into a square merely believed to hold the target needs a believed-square UI (CMB-094).
    /// </summary>
    public bool CanLocateForRangedAttack(CharacterController target)
        => target != null && (CanSee(target, incomingIsRangedAttack: true) || HasPinpointed(target));

    // ========== INTERNAL: Single attack with critical hit support ==========

    private bool TryResolveLastKnownPositionAutoMiss(CharacterController target, bool isRangedAttack, ItemData weapon, out CombatResult autoMiss)
    {
        autoMiss = null;

        if (target == null || target.Stats == null || target.Stats.IsDead)
            return false;

        // Maintain attacker-side memory whenever the target is currently visible.
        UpdateLastKnownPosition(target, incomingIsRangedAttack: isRangedAttack);

        if (!target.HasTotalConcealment(this, incomingIsRangedAttack: isRangedAttack))
            return false;

        LastKnownPositionTracker tracker = GetComponent<LastKnownPositionTracker>();
        if (tracker != null && tracker.IsPinpointedThisRound(target))
            return false;

        Vector2Int? trackerLastKnown = tracker != null ? tracker.GetLastKnownPosition(target) : null;
        Vector2Int? fallbackLastKnown = GetLastKnownPosition(target);
        Vector2Int? resolvedLastKnown = trackerLastKnown ?? fallbackLastKnown;

        if (!resolvedLastKnown.HasValue || target.GridPosition == resolvedLastKnown.Value)
            return false;

        string attackerName = Stats != null ? Stats.CharacterName : name;
        string targetName = target.Stats != null ? target.Stats.CharacterName : target.name;
        string lastKnownText = $"({resolvedLastKnown.Value.x}, {resolvedLastKnown.Value.y})";
        string currentText = $"({target.GridPosition.x}, {target.GridPosition.y})";

        Debug.Log($"[Concealment] {attackerName} attacks {targetName}'s last known position.");
        Debug.Log($"[Concealment]   Last known: {lastKnownText}");
        Debug.Log($"[Concealment]   Current: {currentText}");
        Debug.Log("[Concealment]   ATTACKING EMPTY SQUARE - automatic miss (no attack roll).");

        GameManager gm = GameManager.Instance;
        if (gm != null && gm.CombatUI != null)
        {
            gm.CombatUI.ShowCombatLog(CombatLogHelper.Info("", $"{attackerName} attacks {targetName}'s last known position."));
            gm.CombatUI.ShowCombatLog(CombatLogHelper.Info("", $"  Last known: {lastKnownText}"));
            gm.CombatUI.ShowCombatLog(CombatLogHelper.Info("", $"  Current: {currentText}"));
            gm.CombatUI.ShowCombatLog(CombatLogHelper.Info("", "  ATTACKING EMPTY SQUARE: automatic miss (no attack roll)"));
        }

        autoMiss = new CombatResult
        {
            Attacker = this,
            Defender = target,
            WeaponName = weapon != null ? weapon.Name : "Unarmed strike",
            Hit = false,
            DieRoll = 0,
            TotalRoll = 0,
            TargetAC = 0,
            MissedDueToConcealment = true,
            ConcealmentMissChance = 100,
            ConcealmentRoll = 100,
            ConcealmentDescription = $"Last known position miss: last seen at {lastKnownText}, current position {currentText}, target moved",
            DefenderHPBefore = target.Stats.CurrentHP,
            DefenderHPAfter = target.Stats.CurrentHP,
            IsRangedAttack = isRangedAttack
        };

        return true;
    }

    /// <summary>
    /// Perform a single attack with full D&D 3.5 critical hit mechanics (see
    /// <see cref="PerformSingleAttackWithCritCore"/>), then report the result to the inert
    /// <see cref="ScenarioHooks.AttackResolved"/> test hook. Every weapon attack path calls this (single attacks, AoOs,
    /// full attacks, two-weapon fighting, flurries, natural, rake and grapple attacks), so each swing also sends the
    /// hostile-action notices against the target here, for PCs and NPCs alike: a charm or command undead by the attacker
    /// ends, a bard's fascination breaks (an obvious threat, PHB p.29), and the combat noise disturbs fascinated creatures
    /// nearby (<see cref="GameManager.NotifyWeaponAttackAgainst"/>; CHR-026).
    /// </summary>
    private CombatResult PerformSingleAttackWithCrit(CharacterController target, int totalAtkMod,
        bool isFlanking, int flankingBonus, string flankingPartnerName,
        int damageDice, int damageCount, WeaponDamageBreakdown damageBonus,
        int critThreatMin, int critMultiplier,
        ItemData weapon, int situationalTargetAcBonus = 0,
        bool dealNonlethalDamage = false, int damageModeAttackPenalty = 0, string damageModePenaltySource = "")
    {
        CombatResult result = PerformSingleAttackWithCritCore(target, totalAtkMod,
            isFlanking, flankingBonus, flankingPartnerName,
            damageDice, damageCount, damageBonus,
            critThreatMin, critMultiplier,
            weapon, situationalTargetAcBonus,
            dealNonlethalDamage, damageModeAttackPenalty, damageModePenaltySource);
        if (GameManager.Instance != null && target != null)
            GameManager.Instance.NotifyWeaponAttackAgainst(this, target);
        // A hit on the real figure of a Mirror Image caster shows who is real to everyone watching (decision D7 (6)).
        if (result != null && result.Hit && !result.MirrorImageFigure && MirrorImageRules.HasImages(target))
            MirrorImageRules.RevealRealFigure(target, this);
        ScenarioHooks.AttackResolved?.Invoke(this, result);
        return result;
    }

    // ========================================================================
    // Damage after the weapon roll, shared by every weapon hit and the coup de grace (CMB-004)
    // ========================================================================

    /// <summary>
    /// The special-ability damage of <paramref name="weapon"/> against <paramref name="target"/>: energy dice (flaming,
    /// frost, shock), burst dice on a confirmed critical, alignment dice (holy, unholy, axiomatic, anarchic), bane dice,
    /// vicious (with its backlash to the wielder), vorpal (a confirmed critical on a natural 20) and wounding. These are
    /// extra damage over the weapon's normal damage, so they are added once and never multiplied on a critical hit
    /// (PHB p.140; burst dice scale with the multiplier themselves, DMG p.224). <paramref name="critEffectsOnly"/> is a
    /// confirmed critical against a creature immune to critical hits: burst dice and Vorpal still apply (DMG p.222, "Magic
    /// Weapons and Critical Hits"), without multiplied damage. <paramref name="critConfirmed"/> and
    /// <paramref name="critEffectsOnly"/> are the critical as it stands after the defender's Fortification: a negated
    /// critical is a normal hit, so no crit-only dice and no Vorpal (DMG p.219; delegated RAW reading 2026-10-14,
    /// ITM-067). Vorpal needs a natural 20 (a coup de grace passes 0, PHB p.153) and then follows its own text
    /// (DMG p.226), not critical-hit immunity: <see cref="CharacterStats.GetVorpalBeheadingResult"/>. A severed head
    /// that slays the creature is reported through <paramref name="beheaded"/>: the caller kills it outright after the
    /// hit's damage is applied, so nothing that reacts to hit point loss (regeneration, death prevention) keeps it up.
    /// Log notes go to <paramref name="notes"/>. Shared by <see cref="PerformSingleAttackWithCritCore"/> and
    /// <see cref="ResolveCoupDeGrace"/> (an automatic critical hit, PHB p.153), for PCs and NPCs alike.
    /// </summary>
    private int RollWeaponEnchantmentRiderDamage(ItemData weapon, CharacterController target, bool critConfirmed,
        bool critEffectsOnly, int critMultiplier, int naturalRoll, List<string> notes, out bool beheaded)
    {
        beheaded = false;
        if (weapon == null || !weapon.IsEnchanted)
            return 0;

        int enchantmentBonusDamage = 0;
        string enchantmentDamageLog = "";

        // --- Elemental damage (Flaming, Frost, Shock, etc.) ---
        var elemDmg = EnchantmentEffects.RollElementalDamage(weapon);
        for (int ed = 0; ed < elemDmg.Count; ed++)
        {
            enchantmentBonusDamage += elemDmg[ed].Amount;
            enchantmentDamageLog += $" {elemDmg[ed]}";
        }

        // --- Crit bonus damage (Flaming Burst, Icy Burst, Shocking Burst, Thundering) ---
        if (critConfirmed || critEffectsOnly)
        {
            var critDmg = EnchantmentEffects.RollCritBonusDamage(weapon, critMultiplier);
            for (int cd = 0; cd < critDmg.Count; cd++)
            {
                enchantmentBonusDamage += critDmg[cd].Amount;
                enchantmentDamageLog += $" {critDmg[cd]}";
            }
        }

        // --- Alignment damage (Holy, Unholy, Axiomatic, Anarchic) ---
        if (target != null && target.Stats != null)
        {
            var alignDmg = EnchantmentEffects.RollAlignmentDamage(weapon, target.Stats.CharacterAlignment);
            for (int ad = 0; ad < alignDmg.Count; ad++)
            {
                enchantmentBonusDamage += alignDmg[ad].Amount;
                enchantmentDamageLog += $" {alignDmg[ad]}";
            }

            // --- Bane damage ---
            string targetCreatureType = target.Stats.CreatureType ?? "";
            var baneDmg = EnchantmentEffects.RollBaneDamage(weapon, targetCreatureType);
            for (int bd = 0; bd < baneDmg.Count; bd++)
            {
                enchantmentBonusDamage += baneDmg[bd].Amount;
                enchantmentDamageLog += $" {baneDmg[bd]}";
            }
        }

        // --- Vicious damage (to target and backlash to wielder) ---
        if (EnchantmentEffects.RollViciousDamage(weapon, out int viciousTarget, out int viciousBacklash))
        {
            enchantmentBonusDamage += viciousTarget;
            enchantmentDamageLog += $" +{viciousTarget} (Vicious)";
            if (viciousBacklash > 0 && Stats != null)
            {
                Stats.TakeDamage(viciousBacklash);
                Debug.Log($"[Enchantment] Vicious backlash: {Stats.CharacterName} takes {viciousBacklash} damage from Vicious weapon.");
            }
        }

        // --- Vorpal (DMG p.226: a natural 20 followed by a confirmed critical severs the head, if it has one) ---
        // Works against creatures immune to critical hits too (DMG p.222); what losing the head does follows Vorpal's own
        // text (delegated RAW reading 2026-10-14, ITM-067). Beheading slays outright: it is not damage, so the caller
        // sets the creature dead after the hit's damage, where nothing that reacts to hit point loss can keep it up.
        if (EnchantmentEffects.CheckVorpalEffect(weapon, naturalRoll, critConfirmed || critEffectsOnly)
            && target != null && target.Stats != null)
        {
            BeheadingResult beheading = target.Stats.GetVorpalBeheadingResult();
            if (beheading == BeheadingResult.Dies)
            {
                Debug.Log($"[Enchantment] VORPAL! {Stats.CharacterName}'s {weapon.Name} decapitates {target.Stats.CharacterName}!");
                notes?.Add("⚔ VORPAL DECAPITATION!");
                beheaded = true;
            }
            else
            {
                string why = beheading == BeheadingResult.NoHead
                    ? "has no head to sever"
                    : "is not affected by the loss of its head";
                Debug.Log($"[Enchantment] Vorpal: {target.Stats.CharacterName} {why} (DMG p.226).");
                notes?.Add($"Vorpal: {target.Stats.CharacterName} {why}.");
            }
        }

        // --- Wounding (1 CON damage per hit) ---
        if (EnchantmentEffects.HasWoundingEffect(weapon) && target != null && target.Stats != null)
        {
            Debug.Log($"[Enchantment] Wounding: {target.Stats.CharacterName} takes 1 CON damage from Wounding weapon.");
            notes?.Add("Wounding: -1 CON");
            // Note: actual CON damage tracking would need ability damage system
        }

        if (enchantmentBonusDamage > 0)
        {
            Debug.Log($"[Enchantment] {Stats.CharacterName}'s enchanted {weapon.Name} deals +{enchantmentBonusDamage} bonus damage:{enchantmentDamageLog}");
        }

        return enchantmentBonusDamage;
    }

    /// <summary>
    /// A specific weapon's OnDamageRoll (e.g. Sword of Subtlety +4, Sylvan Scimitar +1d6) and, on a confirmed critical
    /// hit, its OnCriticalHit hook, also against a creature immune to critical hits (<paramref name="critEffectsOnly"/>;
    /// DMG p.222: a mace of smiting destroys an iron golem on a confirmed critical). Both flags are the critical after the
    /// defender's Fortification: a negated critical runs no OnCriticalHit (DMG p.219, ITM-067). Adjusts
    /// <paramref name="rawTotalDamage"/> and returns how much OnDamageRoll changed it. Specific-item damage that RAW
    /// multiplies on a critical is still added once (ITM-077).
    /// </summary>
    private static int ApplySpecificItemDamageHooks(ItemData weapon, CharacterController target, bool critConfirmed,
        bool critEffectsOnly, ref int rawTotalDamage, List<string> notes)
    {
        if (weapon == null || weapon.SpecificItemBehavior == null || target == null)
            return 0;

        var behaviorDmgNotes = new List<string>();

        // OnDamageRoll: modify damage
        int before = rawTotalDamage;
        weapon.SpecificItemBehavior.OnDamageRoll(target, ref rawTotalDamage, critConfirmed, behaviorDmgNotes);
        int delta = rawTotalDamage - before;

        // OnCriticalHit: crit-triggered effects
        if (critConfirmed || critEffectsOnly)
            weapon.SpecificItemBehavior.OnCriticalHit(target, rawTotalDamage, behaviorDmgNotes);

        notes?.AddRange(behaviorDmgNotes);
        return delta;
    }

    /// <summary>
    /// Fortification on <paramref name="target"/>'s armor and shield (DMG p.219): when a critical hit or sneak attack is
    /// scored on the wearer, a percentile roll may negate it, and the damage is rolled normally. True when it does.
    /// </summary>
    private static bool RollDefenderFortification(CharacterController target, out int fortPercent)
    {
        fortPercent = 0;
        Inventory targetInv = target?.GetInventoryData();
        ItemData defenderArmor = targetInv?.ArmorRobeSlot;
        ItemData defenderShield = (targetInv?.LeftHandSlot != null && targetInv.LeftHandSlot.IsShield) ? targetInv.LeftHandSlot : null;
        if (!EnchantmentEffects.CheckFortification(defenderArmor, defenderShield, out int fortRoll))
            return false;

        fortPercent = EnchantmentEffects.GetFortificationPercent(defenderArmor)
                    + EnchantmentEffects.GetFortificationPercent(defenderShield);
        fortPercent = Mathf.Min(fortPercent, 100);
        Debug.Log($"[Enchantment] Fortification! Roll {fortRoll} ≤ {fortPercent}%: crit/sneak negated for {target.Stats.CharacterName}.");
        return true;
    }

    /// <summary>
    /// What a hit with <paramref name="weapon"/> counts as against damage reduction: the weapon's own tags, ranged, the
    /// enchantment tags (holy counts as good, unholy as evil, ...) and Magic Stone on a sling (PHB p.251).
    /// </summary>
    private DamageBypassTag GetWeaponAttackBypassTags(ItemData weapon, bool isRangedAttack)
    {
        DamageBypassTag attackTags = weapon != null ? weapon.GetBypassTags() : DamageBypassTag.Bludgeoning;
        if (isRangedAttack)
            attackTags |= DamageBypassTag.Ranged;

        // Add enchantment bypass tags (Holy → Good, Unholy → Evil, etc.)
        if (weapon != null && weapon.IsEnchanted)
            attackTags |= EnchantmentEffects.GetEnchantmentBypassTags(weapon);

        // D&D 3.5e Magic Stone: sling attacks with active Magic Stone count as magic weapons (PHB p.251)
        if (Stats != null && Stats.MagicStoneActive && Stats.MagicStoneCharges >= 0
            && weapon != null && weapon.Id == ItemIDs.SLING)
            attackTags |= DamageBypassTag.Magic;

        return attackTags;
    }

    private static void AppendSpecialAttackNotes(CombatResult result, List<string> notes)
    {
        if (result == null || notes == null)
            return;
        for (int i = 0; i < notes.Count; i++)
            result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote) ? notes[i] : $"{result.SpecialAttackNote} {notes[i]}";
    }

    /// <summary>
    /// Perform a single attack with full D&D 3.5 critical hit mechanics.
    /// Step 1: Roll d20. Check if in threat range.
    /// Step 2: If threat, roll confirmation vs same AC with same bonus.
    /// Step 3: Roll the weapon dice plus <paramref name="damageBonus"/>'s total with <see cref="WeaponDamageRoll"/>:
    /// once on a normal hit, the critical multiplier number of times on a confirmed critical (PHB p.134, p.140; CMB-004),
    /// then add the extra dice that are never multiplied (sneak attack, enchantment riders). A target immune to critical
    /// hits makes no confirmation roll and takes normal damage.
    /// </summary>
    /// <param name="weapon">The weapon being used (null = unarmed or natural)</param>
    /// <param name="damageBonus">Every static damage term, from <see cref="BuildWeaponDamageBonus"/> (CMB-003).</param>
    private CombatResult PerformSingleAttackWithCritCore(CharacterController target, int totalAtkMod,
        bool isFlanking, int flankingBonus, string flankingPartnerName,
        int damageDice, int damageCount, WeaponDamageBreakdown damageBonus,
        int critThreatMin, int critMultiplier,
        ItemData weapon, int situationalTargetAcBonus = 0,
        bool dealNonlethalDamage = false, int damageModeAttackPenalty = 0, string damageModePenaltySource = "")
    {
        var result = new CombatResult();
        result.Attacker = this;
        result.Defender = target;
        result.IsFlanking = isFlanking;
        result.FlankingBonus = isFlanking ? flankingBonus : 0;
        result.FlankingPartnerName = flankingPartnerName ?? "";
        result.AttackDamageMode = dealNonlethalDamage ? AttackDamageMode.Nonlethal : AttackDamageMode.Lethal;
        result.DamageModeAttackPenalty = damageModeAttackPenalty;
        result.DamageModePenaltySource = damageModePenaltySource ?? string.Empty;

        // Store weapon crit properties on result for display
        result.CritThreatMin = critThreatMin;
        result.CritMultiplier = critMultiplier;

        // Store base damage dice string
        result.BaseDamageDiceStr = $"{damageCount}d{damageDice}";
        result.WeaponName = weapon != null ? weapon.Name : "Unarmed strike";

        bool suppressStrengthToDamage = damageBonus.StrengthSuppressed;
        if (suppressStrengthToDamage)
            result.SpecialAttackNote = "Weapon rule: Strength modifier is not added to damage.";

        bool isRangedAttack = weapon != null && (weapon.WeaponCat == WeaponCategory.Ranged || weapon.RangeIncrement > 0);

        TrueStrikeEffect trueStrike = GetComponent<TrueStrikeEffect>();
        bool trueStrikeActive = trueStrike != null && trueStrike.IsActive();
        bool ignoreConcealmentForThisAttack = trueStrikeActive;
        int trueStrikeBonus = trueStrikeActive ? trueStrike.GetAttackBonus() : 0;

        bool targetIsHelplessLike = !isRangedAttack && target != null && target.IsHelplessForCoupDeGrace();
        int helplessMeleeAttackBonus = targetIsHelplessLike ? 4 : 0;

        int weaponEnhancementAttackBonus = weapon != null ? weapon.GetEnhancementAttackBonus() : 0;
        // D&D 3.5e PHB p.126: Masterwork weapons grant +1 attack (does not stack with magic enhancement).
        int masterworkAttackBonus = weapon != null ? weapon.MasterworkAttackBonus : 0;
        result.WeaponEnhancementAttackBonus = weaponEnhancementAttackBonus + masterworkAttackBonus;

        int blindedTargetAttackBonus = target != null && target.HasCondition(CombatConditionType.Blinded) ? 2 : 0;

        // D&D 3.5e PHB p.141: Invisible attacker gains +2 on attack rolls against sighted opponents.
        // IMPORTANT: Capture BEFORE breaking invisibility so the attack that breaks it still benefits.
        int invisibleAttackerBonus = GetInvisibleAttackerBonus(target);
        bool attackerWasInvisible = invisibleAttackerBonus > 0;

        // D&D 3.5e PHB p.257: Blink grants +2 attack bonus (as if invisible) against targets
        // that cannot see invisible creatures. Does NOT break on attack (unlike standard invisibility).
        int blinkAttackerBonus = GetBlinkAttackerBonus(target);
        bool blinkDenyDex = BlinkDeniesDexToAC(target);

        // D&D 3.5e PHB p.141: Defender who can't see attacker loses Dex bonus to AC.
        // Capture BEFORE breaking invisibility.
        bool denyTargetDexFromInvisibility = ShouldDenyTargetDexToAC(target);
        int deniedDexFromInvisibility = 0;
        if (denyTargetDexFromInvisibility && target != null && target.Stats != null)
        {
            deniedDexFromInvisibility = GetDexBonusAppliedToArmorClass(target);
        }

        // Blink deny Dex to AC (stacks with invisibility deny — take the larger denied amount)
        int deniedDexFromBlink = 0;
        if (blinkDenyDex && target != null && target.Stats != null)
        {
            deniedDexFromBlink = GetDexBonusAppliedToArmorClass(target);
        }

        // Destruction Domain Smite: +4 attack and +cleric level damage on the smiting attack (PHB p.186). Both are read
        // here, before the attack roll consumes the smite (CHR-005, CMB-003).
        // The smite is a melee attack, so a ranged attack neither gains it nor uses it up.
        bool destructionSmiteEligible = !damageBonus.IsRangedAttack;
        int destructionSmiteAttackBonus = destructionSmiteEligible ? GameManager.GetDestructionSmiteAttackBonus(this) : 0;
        damageBonus.DestructionSmiteBonus = destructionSmiteEligible ? GameManager.GetDestructionSmiteDamageBonus(this) : 0;

        // Paladin Smite Evil and the template smites (PHB p.44, MM p.31, p.107; SmiteRules, CHR-020): a declared smite is
        // used up by this melee attack, hit or miss (delegated RAW reading 2026-10-14), and adds its bonuses only against
        // a creature of the smitten alignment. A ranged attack leaves the declaration in place.
        SmiteApplication smite = SmiteRules.ConsumeForMeleeAttack(this, target, damageBonus.IsRangedAttack);
        int smiteAttackBonus = smite.Used ? smite.AttackBonus : 0;
        if (smite.Used)
        {
            damageBonus.SmiteBonus = smite.DamageBonus;
            damageBonus.SmiteLabel = smite.Label;
        }

        // Favored enemy (PHB p.47; RangerRules, CHR-020): the ranger's bonus on every weapon damage roll against a creature
        // of a favored type, melee or ranged, for PCs and NPCs alike.
        damageBonus.FavoredEnemyBonus = target != null ? RangerRules.FavoredEnemyDamageBonus(Stats, target.Stats) : 0;

        // Enchantment attack bonus (e.g., Bane +2 vs matching creature type)
        int enchantmentAttackBonus = 0;
        if (weapon != null && weapon.IsEnchanted && target != null && target.Stats != null)
        {
            string targetCreatureType = target.Stats.CreatureType ?? "";
            enchantmentAttackBonus = EnchantmentEffects.GetEnchantmentAttackBonus(weapon, targetCreatureType);
            // Bane: against its foe the weapon's effective enhancement bonus is +2 better, on damage as well as on the
            // attack roll (DMG p.224); the extra 2d6 is rolled with the other enchantment riders.
            damageBonus.BaneBonus = EnchantmentEffects.GetBaneEnhancementBonus(weapon, targetCreatureType);
        }
        RecordWeaponDamageBreakdown(result, damageBonus);

        // D&D 3.5 DMG: Brilliant Energy weapons cannot harm undead, constructs, or objects.
        // The weapon passes harmlessly through them. Return an automatic miss.
        if (weapon != null && weapon.IsEnchanted && target != null && target.Stats != null)
        {
            string targetCT = target.Stats.CreatureType ?? "";
            if (AdvancedEnchantmentEffects.BrilliantEnergyCannotHarm(weapon, targetCT))
            {
                result.Hit = false;
                result.DieRoll = 0;
                result.TotalRoll = 0;
                result.TargetAC = 0;
                result.DefenderHPBefore = target.Stats.CurrentHP;
                result.DefenderHPAfter = target.Stats.CurrentHP;
                string attackerName = Stats != null ? Stats.CharacterName : name;
                string targetName = target.Stats != null ? target.Stats.CharacterName : target.name;
                result.SpecialAttackNote = $"Brilliant Energy weapon passes harmlessly through {targetName} ({targetCT}) — cannot harm undead or constructs.";
                if (GameManager.Instance?.CombatUI != null)
                    GameManager.Instance.CombatUI.ShowCombatLog(CombatLogHelper.Color($"✦ {attackerName}'s Brilliant Energy weapon passes harmlessly through {targetName} ({targetCT})!", CombatLogHelper.ColorSoftYellow));
                return result;
            }
        }

        // --- Specific Item Behavior: OnPreAttackRoll ---
        int specificItemAttackBonus = 0;
        if (weapon != null && weapon.SpecificItemBehavior != null && target != null)
        {
            var behaviorNotes = new System.Collections.Generic.List<string>();
            weapon.SpecificItemBehavior.OnPreAttackRoll(target, ref specificItemAttackBonus, behaviorNotes);
            foreach (var note in behaviorNotes)
            {
                result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote) ? note : $"{result.SpecialAttackNote} {note}";
            }
        }

        int totalAtkModWithTrueStrike = totalAtkMod + weaponEnhancementAttackBonus + masterworkAttackBonus + trueStrikeBonus + helplessMeleeAttackBonus + blindedTargetAttackBonus + invisibleAttackerBonus + blinkAttackerBonus + destructionSmiteAttackBonus + smiteAttackBonus + enchantmentAttackBonus + specificItemAttackBonus;

        // D&D 3.5e: making an attack roll (or attempting to attack) breaks standard invisibility.
        // Greater Invisibility does NOT break on attack (BreaksOnAttack=false).
        BreakInvisibility("attack roll", target);

        if (TryResolveLastKnownPositionAutoMiss(target, isRangedAttack, weapon, out CombatResult emptySquareMiss))
            return emptySquareMiss;

        // Mirror Image (PHB p.254; decision D7, batch 11 step 26): an attack on a caster with images picks a figure at
        // random; an image is rolled against 10 + size + Dex, and a hit destroys it with no damage and no on-hit effect.
        // The same for every attacker and caster (MirrorImageRules).
        // The attack roll against an image is still this attack's roll: the same bookkeeping follows it as a roll against
        // the caster (a declared Destruction smite and True Strike are used up, a Luck reroll is logged, the modifiers
        // are listed), so a daily smite spent on an image is spent.
        if (MirrorImageRules.TryStrikeImageWithAttack(this, target, totalAtkModWithTrueStrike, result))
        {
            result.IsRangedAttack = isRangedAttack;
            FinishAttackRollBookkeeping();
            result.RebuildBreakdownsFromComputedValues();
            return result;
        }

        int targetAC = GetSituationalTargetArmorClass(target, this, isRangedAttack) + Mathf.Max(0, situationalTargetAcBonus);

        // D&D 3.5 DMG: Brilliant Energy weapons ignore armor, shield, and natural armor bonuses.
        // Only deflection, dodge, luck, sacred, profane, and force bonuses still apply.
        if (weapon != null && weapon.IsEnchanted && target != null && target.Stats != null)
        {
            Inventory targetInvBE = target.GetInventoryData();
            ItemData beArmor = targetInvBE?.ArmorRobeSlot;
            ItemData beShield = (targetInvBE?.LeftHandSlot != null && targetInvBE.LeftHandSlot.IsShield) ? targetInvBE.LeftHandSlot : null;
            int natArmor = target.Stats.NaturalArmorBonus;
            int brilliantACReduction = AdvancedEnchantmentEffects.GetBrilliantEnergyACReduction(weapon, beArmor, beShield, natArmor);
            if (brilliantACReduction > 0)
            {
                targetAC -= brilliantACReduction;
                string beNote = $"Brilliant Energy: ignores armor/shield/natural armor (-{brilliantACReduction} AC).";
                result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote)
                    ? beNote
                    : $"{result.SpecialAttackNote} {beNote}";
            }
        }

        // D&D 3.5e PHB p.141: Defender who can't see the attacker loses Dex bonus to AC.
        // Applied from the pre-break invisible attacker state captured above.
        if (deniedDexFromInvisibility > 0)
        {
            targetAC -= deniedDexFromInvisibility;
            string note = $"Invisible attacker: target denied DEX bonus to AC (-{deniedDexFromInvisibility}).";
            result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote)
                ? note
                : $"{result.SpecialAttackNote} {note}";
        }

        // Blink deny Dex to AC — only apply if it exceeds what invisibility already denied.
        if (deniedDexFromBlink > 0 && deniedDexFromBlink > deniedDexFromInvisibility)
        {
            int additionalDexDenied = deniedDexFromBlink - deniedDexFromInvisibility;
            targetAC -= additionalDexDenied;
            string note = $"Blinking attacker: target denied DEX bonus to AC (-{deniedDexFromBlink}).";
            result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote)
                ? note
                : $"{result.SpecialAttackNote} {note}";
        }

        // Log invisible attacker bonus in the attack note
        if (attackerWasInvisible)
        {
            string note = $"Invisible attacker: +{invisibleAttackerBonus} attack bonus.";
            result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote)
                ? note
                : $"{result.SpecialAttackNote} {note}";
        }

        // Log Blink attacker bonus
        if (blinkAttackerBonus > 0)
        {
            string note = $"Blinking attacker: +{blinkAttackerBonus} attack bonus (target cannot see invisible).";
            result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote)
                ? note
                : $"{result.SpecialAttackNote} {note}";
        }

        bool targetIsInvisibleToRules = target != null && target.HasActiveInvisibilityEffect && !target.IsOutlinedByGlitterdust;
        int invisibilityAcBonus = targetIsInvisibleToRules ? target.GetInvisibilityArmorClassBonusAgainst(this) : 0;
        if (targetIsInvisibleToRules)
        {
            if (invisibilityAcBonus > 0)
            {
                string note = "Invisible target: +2 AC applied (attacker cannot see invisible).";
                result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote)
                    ? note
                    : $"{result.SpecialAttackNote} {note}";
            }
            else if (CanSeeInvisible(target))
            {
                string note = "No invisibility penalties: attacker can see invisible target.";
                result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote)
                    ? note
                    : $"{result.SpecialAttackNote} {note}";
            }
        }

        AlignmentProtectionBenefits protection = AlignmentProtectionRules.GetBenefitsAgainst(
            target,
            Stats != null ? Stats.CharacterAlignment : Alignment.None);

        // The ward's deflection does not stack with the target's own deflection (ring or spell): only the excess applies.
        int protectionDeflection = AlignmentProtectionRules.DeflectionAcIncrease(protection, target != null ? target.Stats : null);
        if (protectionDeflection > 0)
        {
            targetAC += protectionDeflection;
            result.ProtectionDeflectionBonusToAc = protectionDeflection;
            result.ProtectionSourceName = protection.SourceSpellName;
        }

        bool attackerIsSummoned = GameManager.Instance != null && GameManager.Instance.IsSummonedCreature(this);
        bool isMeleeContactAttack = !isRangedAttack;
        if (isMeleeContactAttack && attackerIsSummoned && protection.HasMatch && protection.BlocksSummonedContact)
        {
            result.TargetAC = targetAC;
            result.Hit = false;
            result.DieRoll = 1;
            result.TotalRoll = 1;
            result.DefenderHPBefore = target != null && target.Stats != null ? target.Stats.CurrentHP : 0;
            result.DefenderHPAfter = result.DefenderHPBefore;
            result.ProtectionSummonedBarrierBlocked = true;
            result.ProtectionBarrierNote = "Protection from alignment barrier blocks bodily contact from summoned creatures.";
            return result;
        }

        int grappleDexDenied = GetGrappleDexDeniedAgainstAttacker(target, this, out string grappleDexNote);
        int pinnedDexDenied = GetPinnedDexDeniedAgainstAttacker(target, this, out string pinnedDexNote);
        int totalDexDenied = Mathf.Max(grappleDexDenied, pinnedDexDenied);
        if (totalDexDenied > 0)
        {
            targetAC -= totalDexDenied;
            result.GrappleDexDeniedToAc = totalDexDenied;
        }

        if (!string.IsNullOrEmpty(pinnedDexNote))
            result.GrappleDexRuleNote = pinnedDexNote;
        else if (!string.IsNullOrEmpty(grappleDexNote))
            result.GrappleDexRuleNote = grappleDexNote;

        int feintDexDenied = 0;
        string feintNote;
        bool feintWindowConsumed;
        if (TryConsumeFeintDexDenial(target, !isRangedAttack, out feintDexDenied, out feintNote, out feintWindowConsumed))
        {
            targetAC -= feintDexDenied;
            result.FeintDexDeniedToAc = feintDexDenied;
            result.FeintWindowNote = feintNote;
        }
        else if (!string.IsNullOrEmpty(feintNote))
        {
            result.FeintWindowNote = feintNote;
        }

        // Dodge bonuses: the racial ones (CHR-019: the +4 against giants is in GetSituationalTargetArmorClass and the
        // svirfneblin's +4 against all creatures in ArmorClass) and the Dodge feat and Haste (CharacterStats.DodgeACBonus,
        // in ArmorClass). A dodge bonus goes with the Dexterity bonus to AC, for any reason it is denied (PHB p.136, p.93,
        // p.239): when this attack alone denies the target its Dexterity bonus (an attacker it cannot see, a feint, a
        // grapple or pin), they go too. A condition that denies Dexterity (flat-footed, stunned...) already left them out.
        if (target != null && target.Stats != null)
        {
            int racialDodge = RacialTraitRules.DodgeACBonusAgainst(target.Stats, Stats);
            int otherDodge = target.Stats.DodgeACBonus;
            if (racialDodge > 0 || otherDodge > 0)
            {
                bool grappledByAnother = target.HasCondition(CombatConditionType.Grappled)
                    && !(target.TryGetGrappleState(out CharacterController dodgeGrappleOpponent, out _, out _, out _) && dodgeGrappleOpponent == this);
                bool attackDeniesDex = denyTargetDexFromInvisibility || blinkDenyDex || feintWindowConsumed
                    || target.HasCondition(CombatConditionType.Pinned) || grappledByAnother;
                string dodgeNote = null;
                if (attackDeniesDex)
                {
                    targetAC -= racialDodge + otherDodge;
                    dodgeNote = $"Dodge +{racialDodge + otherDodge} AC lost with the DEX bonus.";
                }
                else if (racialDodge > 0)
                {
                    dodgeNote = $"Racial dodge: +{racialDodge} AC against {(Stats != null ? Stats.CharacterName : name)}.";
                }
                if (!string.IsNullOrEmpty(dodgeNote))
                    result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote)
                        ? dodgeNote
                        : $"{result.SpecialAttackNote} {dodgeNote}";
            }
        }

        // Step 1: Roll to hit
        var (hit, roll, total) = Stats.RollToHitWithMod(totalAtkModWithTrueStrike, targetAC);

        result.DieRoll = roll;
        result.TotalRoll = total;
        result.TargetAC = targetAC;
        result.Hit = hit;
        result.IsRangedAttack = isRangedAttack;
        result.NaturalTwenty = (roll == 20);
        result.NaturalOne = (roll == 1);

        FinishAttackRollBookkeeping();

        // What follows every attack roll of this attack, against the caster or one of its Mirror Image figures: the Luck
        // reroll's log, the declared Destruction smite used up (declared before the attack, PHB p.186), the smite note,
        // the attack modifiers listed, and True Strike used up (hit or miss).
        void FinishAttackRollBookkeeping()
        {
            // Log Luck domain reroll if it was triggered during the attack roll
            GameManager.Instance?.LogLuckRerollIfTriggered(Stats);

            // Consume Destruction Domain Smite immediately after the attack roll (D&D 3.5e: declared before attack)
            if (destructionSmiteAttackBonus > 0)
            {
                result.AddAttackBuffDebuffModifier("Destruction Smite", destructionSmiteAttackBonus);
                GameManager.ConsumeDestructionSmite(this);
            }

            if (smite.Used)
            {
                if (smiteAttackBonus != 0)
                    result.AddAttackBuffDebuffModifier(smite.Label, smiteAttackBonus);
                string smiteTargetName = target != null && target.Stats != null ? target.Stats.CharacterName : "the target";
                string smiteNote = smite.TargetIsSmiteFoe
                    ? $"{smite.Label}: {CharacterStats.FormatMod(smite.AttackBonus)} attack, {CharacterStats.FormatMod(smite.DamageBonus)} damage."
                    : $"{smite.Label} used up with no effect: {smiteTargetName} is not {(smite.Source == SmiteSource.TemplateSmiteGood ? "good" : "evil")} (PHB p.44).";
                result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote) ? smiteNote : $"{result.SpecialAttackNote} {smiteNote}";
            }

            AttachAttackBuffDebuffBreakdown(result);

            // NOTE: Weapon enhancement bonus is NOT added to AttackBuffDebuffModifiers here
            // because it is already included in CombatResult.WeaponEnhancementAttackBonus,
            // which BuildBreakdowns() adds to AttackRollBreakdown.Modifiers as "enhancement".
            // Adding it here as well would create a duplicate entry in the breakdown display.

            if (trueStrikeActive && trueStrikeBonus != 0)
                result.AddAttackBuffDebuffModifier("True Strike (insight)", trueStrikeBonus);

            if (helplessMeleeAttackBonus > 0)
            {
                result.AddAttackBuffDebuffModifier("Melee vs helpless target", helplessMeleeAttackBonus);
                string helplessNote = "Target is helpless: +4 melee attack bonus applied.";
                result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote)
                    ? helplessNote
                    : $"{result.SpecialAttackNote} {helplessNote}";
            }

            // D&D 3.5e True Strike is consumed on the next attack roll (hit or miss).
            if (trueStrikeActive)
                trueStrike.ConsumeOnAttackRoll();
        }

        // Step 2: Concealment miss chance check (rolled after a successful attack roll)
        bool isThreat = false;
        bool critConfirmed = false;
        int confirmRoll = 0;
        int confirmTotal = 0;

        if (hit)
        {
            int targetMissChance = target.GetMissChance(this, isRangedAttack);
            int blindedAttackerMissChance = HasCondition(CombatConditionType.Blinded) ? CombatCalculationService.BlindedAttackerMissChance : 0;
            int missChance = Mathf.Max(targetMissChance, blindedAttackerMissChance);

            if (target.HasActiveInvisibilityEffect && CanSeeInvisible(target) && targetMissChance <= 0)
            {
                string attackerName = Stats != null ? Stats.CharacterName : name;
                string targetName = target.Stats != null ? target.Stats.CharacterName : target.name;
                GameManager.Instance?.CombatUI?.ShowCombatLog(CombatLogHelper.IceBlue("👁", $"{attackerName} sees invisible {targetName} clearly (no concealment)."));
            }

            if (targetMissChance > 0 && CanSeeInvisible(target) && target.HasActiveBlurEffect)
            {
                string note = "Blur effect remains despite magical sight.";
                result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote)
                    ? note
                    : $"{result.SpecialAttackNote} {note}";
            }

            // ── Improved Precise Shot: ignore less-than-total concealment on ranged attacks ──
            // D&D 3.5e PHB p.96: Improved Precise Shot lets you ignore the miss chance
            // from concealment (but not total concealment, i.e. 50%+) on ranged attacks.
            if (isRangedAttack && targetMissChance > 0 && targetMissChance < 50
                && FeatManager.HasImprovedPreciseShot(Stats))
            {
                string ipsNote = $"Improved Precise Shot: ignoring {targetMissChance}% concealment miss chance.";
                result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote)
                    ? ipsNote : $"{result.SpecialAttackNote} {ipsNote}";
                missChance = blindedAttackerMissChance; // only keep attacker-side miss chance if any
            }

            if (missChance > 0)
            {
                result.ConcealmentMissChance = missChance;

                string concealmentDescription = targetMissChance > 0
                    ? target.GetConcealmentDescription(this, isRangedAttack)
                    : string.Empty;

                if (blindedAttackerMissChance > 0)
                {
                    string blindDescription = "Attacker is blinded (50% miss chance)";
                    concealmentDescription = string.IsNullOrEmpty(concealmentDescription)
                        ? blindDescription
                        : $"{concealmentDescription}; {blindDescription}";
                }

                result.ConcealmentDescription = string.IsNullOrEmpty(concealmentDescription)
                    ? "Concealment"
                    : concealmentDescription;

                if (ignoreConcealmentForThisAttack && targetMissChance > 0)
                {
                    // True Strike negates miss chance from target concealment for this attack,
                    // but does not negate misses from the attacker being blinded.
                    if (blindedAttackerMissChance <= 0)
                    {
                        result.ConcealmentRoll = 100;
                        result.ConcealmentDescription = $"{result.ConcealmentDescription} (ignored by True Strike)";
                    }
                    else
                    {
                        missChance = blindedAttackerMissChance;
                        result.ConcealmentMissChance = missChance;
                        int concealmentRoll = DiceService.Percentile("Concealment miss chance");
                        result.ConcealmentRoll = concealmentRoll;

                        if (concealmentRoll <= missChance)
                        {
                            result.Hit = false;
                            result.MissedDueToConcealment = true;
                            result.Damage = 0;
                            result.BaseDamageRoll = 0;
                            result.RawTotalDamage = 0;
                            result.FinalDamageDealt = 0;
                            return result;
                        }
                    }
                }
                else
                {
                    int concealmentRoll = DiceService.Percentile("Blind-Fight concealment reroll");
                    result.ConcealmentRoll = concealmentRoll;

                    if (concealmentRoll <= missChance)
                    {
                        result.Hit = false;
                        result.MissedDueToConcealment = true;
                        result.Damage = 0;
                        result.BaseDamageRoll = 0;
                        result.RawTotalDamage = 0;
                        result.FinalDamageDealt = 0;
                        return result;
                    }
                }
            }

            // ── Blink attacker 20% miss chance ──
            // D&D 3.5e PHB p.206: While blinking, the attacker has a 20% chance of
            // striking while on the Ethereal Plane, causing the attack to miss.
            // This is separate from target concealment and applies even if concealment
            // was bypassed. It represents the attacker's own planar instability.
            int blinkAttackerMissChance = GetBlinkAttackerMissChance();
            if (blinkAttackerMissChance > 0)
            {
                int blinkRoll = DiceService.Percentile("Blink attacker miss chance");
                if (blinkRoll <= blinkAttackerMissChance)
                {
                    result.Hit = false;
                    result.MissedDueToConcealment = true;
                    result.Damage = 0;
                    result.BaseDamageRoll = 0;
                    result.RawTotalDamage = 0;
                    result.FinalDamageDealt = 0;
                    string blinkNote = $"Blink miss: attacker was ethereal when striking ({blinkAttackerMissChance}% chance, rolled {blinkRoll}).";
                    result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote)
                        ? blinkNote
                        : $"{result.SpecialAttackNote} {blinkNote}";
                    return result;
                }
                else
                {
                    string blinkNote = $"Blink attacker roll: {blinkRoll} vs {blinkAttackerMissChance}% (attack proceeds).";
                    result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote)
                        ? blinkNote
                        : $"{result.SpecialAttackNote} {blinkNote}";
                }
            }

            // ── Deflect Arrows: target may negate one ranged hit per round ──
            // D&D 3.5e PHB p.93: Once per round, a character with Deflect Arrows who
            // is aware of the attack and has a free hand can deflect one ranged weapon
            // attack that would otherwise hit them.
            if (isRangedAttack && target != null && FeatManager.TryDeflectArrow(target, this))
            {
                result.Hit = false;
                result.Damage = 0;
                result.BaseDamageRoll = 0;
                result.RawTotalDamage = 0;
                result.FinalDamageDealt = 0;

                // Snatch Arrows: if target has Snatch Arrows feat AND deflected, they catch the projectile
                // and may throw it back as an immediate ranged attack (PHB p.101).
                bool snatched = FeatManager.ShouldSnatchAfterDeflect(target);
                if (snatched)
                {
                    string snatchNote = "Snatch Arrows: caught deflected projectile and throws it back!";
                    result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote)
                        ? snatchNote : $"{result.SpecialAttackNote} {snatchNote}";

                    // Immediate counter-attack: target throws the caught projectile back at the attacker.
                    // Uses target's BAB + DEX mod as the attack roll (improvised thrown weapon, no penalty per RAW).
                    if (target.Stats != null && this.Stats != null && !this.Stats.IsDead)
                    {
                        int counterAttackRoll = DiceRoller.D20();
                        int counterAttackBonus = target.Stats.BaseAttackBonus + target.Stats.DEXMod;
                        int counterTotal = counterAttackRoll + counterAttackBonus;
                        int snatchTargetAC = this.Stats.ArmorClass;

                        if (CombatCalculationService.IsHit(counterAttackRoll, counterTotal, snatchTargetAC))
                        {
                            // Hit: deal 1d4 + STR damage (thrown projectile)
                            int snatchDamage = DiceRoller.D4() + target.Stats.STRMod;
                            snatchDamage = Mathf.Max(1, snatchDamage);
                            this.Stats.TakeDamage(snatchDamage);
                            result.SpecialAttackNote += $" Counter-attack hits for {snatchDamage} damage! (roll {counterAttackRoll}+{counterAttackBonus}={counterTotal} vs AC {targetAC})";
                        }
                        else
                        {
                            result.SpecialAttackNote += $" Counter-attack misses. (roll {counterAttackRoll}+{counterAttackBonus}={counterTotal} vs AC {targetAC})";
                        }
                    }
                }
                else
                {
                    string deflectNote = "Deflect Arrows: ranged attack deflected!";
                    result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote)
                        ? deflectNote : $"{result.SpecialAttackNote} {deflectNote}";
                }

                return result;
            }

            bool whipArmorBlocked = IsTargetImmuneToWhipDamage(target, weapon);
            if (whipArmorBlocked)
            {
                result.IsCritThreat = false;
                result.CritConfirmed = false;
                result.Damage = 0;
                result.BaseDamageRoll = 0;
                result.RawTotalDamage = 0;
                result.FinalDamageDealt = 0;
                result.ImmunityPrevented = true;
                result.MitigationSummary = "Whip cannot harm targets with armor/natural armor bonus +1 or higher.";
                result.DamageTypeSummary = "nonlethal slashing";
                return result;
            }

            // A confirmed threat against a creature immune to critical hits: no multiplied damage, but the weapon's
            // critical-hit effects (burst dice, specific-item OnCriticalHit) still apply (DMG p.222).
            bool critEffectsOnly = false;

            bool autoCritOnParalyzed = !isRangedAttack
                && target != null
                && target.HasCondition(CombatConditionType.Paralyzed)
                && GetMinimumDistanceToTarget(target, chebyshev: true) == 1
                && !target.IsImmuneToCriticalHits();

            if (autoCritOnParalyzed)
            {
                isThreat = true;
                critConfirmed = true;
                confirmRoll = 20;
                confirmTotal = 20 + totalAtkModWithTrueStrike;

                result.IsCritThreat = true;
                result.CritConfirmed = true;
                result.ConfirmationRoll = confirmRoll;
                result.ConfirmationTotal = confirmTotal;
                result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote)
                    ? "Adjacent melee hit vs paralyzed target: automatic critical hit."
                    : $"{result.SpecialAttackNote} Adjacent melee hit vs paralyzed target: automatic critical hit.";
            }
            else
            {
                isThreat = CharacterStats.IsCritThreat(roll, critThreatMin);
                result.IsCritThreat = isThreat;

                if (isThreat)
                {
                    // Roll confirmation with the same attack modifier
                    var (confirmed, confRoll, confTotal) = Stats.RollCritConfirmation(totalAtkModWithTrueStrike, targetAC);
                    confirmRoll = confRoll;
                    confirmTotal = confTotal;
                    result.ConfirmationRoll = confirmRoll;
                    result.ConfirmationTotal = confirmTotal;

                    if (target != null && target.IsImmuneToCriticalHits())
                    {
                        // A creature not subject to critical hits (MM p.307-317 type traits: construct, elemental, ooze,
                        // plant, undead; swarms) takes normal damage: the weapon damage is never multiplied (CMB-004,
                        // CMB-064). The threat is still confirmed as usual, and a confirmed critical still applies the
                        // weapon's critical-hit effects such as burst dice (DMG p.222, "Magic Weapons and Critical Hits").
                        critEffectsOnly = confirmed;
                        result.CritImmunityPrevented = true;
                        result.CritEffectsOnly = confirmed;
                        string immuneNote = confirmed
                            ? $"{target.Stats.CharacterName} is immune to critical hits: normal damage, critical-hit weapon effects only."
                            : $"{target.Stats.CharacterName} is immune to critical hits: normal damage.";
                        result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote)
                            ? immuneNote
                            : $"{result.SpecialAttackNote} {immuneNote}";
                    }
                    else
                    {
                        critConfirmed = confirmed;
                        result.CritConfirmed = critConfirmed;
                    }
                }
            }

            // Sneak attack: applies if attacker is Rogue and target is either flanked
            // or denied DEX to AC (feint, flat-footed, stunned, etc.).
            // Sneak attack is NOT multiplied on critical hits (D&D 3.5 rule). Eligibility is settled here, before
            // Fortification; the dice are rolled after the weapon damage.
            bool deniedDexForSneak = IsTargetDeniedDexForSneakAttack(target, !isRangedAttack, feintWindowConsumed, out string dexDeniedReason);
            bool sneakAttackEligible = Stats.IsRogue && (isFlanking || deniedDexForSneak);

            if (sneakAttackEligible && target.HasConcealment(isRangedAttack))
            {
                result.SneakAttackTriggerReason = "target has concealment";
                Debug.Log($"[Sneak Attack] {Stats.CharacterName} cannot sneak attack {target.Stats.CharacterName}: target has concealment.");
                sneakAttackEligible = false;
            }

            if (sneakAttackEligible && IsTargetImmuneToSneakAttackDamage(target))
            {
                string immunityReason = target.IsImmuneToCriticalHits()
                    ? $"{target.Stats.CharacterName} is immune to critical hits, so to sneak attack ({target.Stats.CreatureType})"
                    : $"target creature type '{target.Stats.CreatureType}' is immune to sneak attack precision damage";
                result.SneakAttackTriggerReason = immunityReason;
                Debug.Log($"[Sneak Attack] {Stats.CharacterName} cannot sneak attack {target.Stats.CharacterName}: {immunityReason}.");
                sneakAttackEligible = false;
            }

            // Fortification (defender's armor or shield, DMG p.219) is rolled as soon as a critical hit (or a sneak
            // attack) is scored, before any damage: a negated critical is no critical hit, so the damage is rolled
            // normally and nothing that needs a critical hit happens (burst and Thundering dice, a specific weapon's
            // OnCriticalHit, Vorpal). One roll decides both the critical and the sneak attack. A confirmed critical's
            // effects against a creature immune to critical hits (critEffectsOnly, DMG p.222) are a critical scored on
            // the wearer too. Delegated RAW reading 2026-10-14 (ITM-067).
            if ((critConfirmed || critEffectsOnly || sneakAttackEligible)
                && RollDefenderFortification(target, out int fortPercent))
            {
                if (critConfirmed || critEffectsOnly)
                {
                    critConfirmed = false;
                    critEffectsOnly = false;
                    result.CritConfirmed = false;
                    result.CritEffectsOnly = false;
                    result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote)
                        ? $"Fortification ({fortPercent}%): crit negated"
                        : $"{result.SpecialAttackNote} Fortification ({fortPercent}%): crit negated";
                }

                if (sneakAttackEligible)
                {
                    sneakAttackEligible = false;
                    result.SneakAttackTriggerReason = $"negated by Fortification ({fortPercent}%)";
                    result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote)
                        ? $"Fortification ({fortPercent}%): sneak attack negated"
                        : $"{result.SpecialAttackNote} Fortification ({fortPercent}%): sneak attack negated";
                }
            }

            // Step 3: Roll weapon damage. The static modifier is built once for every attack path (CMB-003,
            // BuildWeaponDamageBonus). A normal hit rolls the weapon dice plus that modifier once; a confirmed critical
            // rolls both the multiplier number of times and adds the rolls (PHB p.134 and p.140; CMB-004, owner ruling
            // 2026-10-08: RAW). Extra dice (sneak attack, enchantment riders) are added once below, never multiplied.
            int staticDamage = damageBonus.Total;
            WeaponDamageRoll weaponRoll = WeaponDamageRoll.Roll(damageCount, damageDice, staticDamage, critConfirmed ? critMultiplier : 1);
            int baseDmgRoll = weaponRoll.DiceTotal;
            int rawWeaponDamage = weaponRoll.Total; // a weapon hit always deals at least 1 before mitigation (PHB p.134)
            if (critConfirmed)
                result.CritDamageDice = weaponRoll.DiceLabel;
            result.WeaponDamageRoll = weaponRoll;
            result.HasWeaponDamageRoll = true;
            result.Damage = rawWeaponDamage;
            result.BaseDamageRoll = baseDmgRoll;
            Debug.Log($"[Damage] {Stats.CharacterName} -> {(target != null && target.Stats != null ? target.Stats.CharacterName : "?")}: "
                + $"{(critConfirmed ? "critical " : string.Empty)}{weaponRoll.Describe()} weapon damage; modifier per roll [{damageBonus.Describe()}]");

            int rawSneakDamage = 0;
            if (sneakAttackEligible)
            {
                int rogueLevel = Stats.GetClassLevel("Rogue");
                int sneakDice = CombatUtils.GetSneakAttackDice(rogueLevel);
                rawSneakDamage = CombatUtils.RollSneakAttackDamage(rogueLevel);
                result.SneakAttackApplied = true;
                result.SneakAttackDice = sneakDice;
                result.SneakAttackDamage = rawSneakDamage;
                result.SneakAttackByFlanking = isFlanking;
                result.SneakAttackByDexDenied = deniedDexForSneak;
                result.SneakAttackTriggerReason = isFlanking
                    ? "target is flanked"
                    : dexDeniedReason;

                string triggerReason = string.IsNullOrEmpty(result.SneakAttackTriggerReason)
                    ? "qualifying condition met"
                    : result.SneakAttackTriggerReason;
                Debug.Log($"[Sneak Attack] {Stats.CharacterName} triggered sneak attack vs {target.Stats.CharacterName}: {triggerReason}. +{rawSneakDamage} ({sneakDice}d6)");
            }

            int rawTotalDamage = rawWeaponDamage + rawSneakDamage;

            bool targetIsSwarm = target != null && target.Stats != null && target.Stats.IsSwarm;
            if (targetIsSwarm && IsTorchWeapon(weapon))
            {
                int torchDamage = DiceService.Roll(1, 3, "Torch damage vs swarm"); // 1d3 fire vs swarms
                rawWeaponDamage = torchDamage;
                rawSneakDamage = 0;
                rawTotalDamage = torchDamage;
                result.SneakAttackApplied = false;
                result.SneakAttackDice = 0;
                result.SneakAttackDamage = 0;
                result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote)
                    ? $"Torch vs swarm: 1d3({torchDamage}) fire damage."
                    : $"{result.SpecialAttackNote} Torch vs swarm: 1d3({torchDamage}) fire damage.";
            }

            // ================================================================
            // EXTRA DAMAGE, SPECIFIC ITEMS (shared with the coup de grace, CMB-004)
            // ================================================================
            // Special-ability dice are extra damage over the weapon's normal damage: added once, never multiplied on a
            // critical (PHB p.140). The same helpers run for a coup de grace (ResolveCoupDeGrace), an automatic critical.
            // They see the critical as it stands after Fortification (ITM-067).
            var extraDamageNotes = new List<string>();
            int enchantmentBonusDamage = RollWeaponEnchantmentRiderDamage(weapon, target, critConfirmed, critEffectsOnly,
                critMultiplier, result.DieRoll, extraDamageNotes, out bool vorpalBeheaded);
            rawTotalDamage += enchantmentBonusDamage;

            int specificDelta = ApplySpecificItemDamageHooks(weapon, target, critConfirmed, critEffectsOnly, ref rawTotalDamage,
                extraDamageNotes);
            rawWeaponDamage += specificDelta; // Track in weapon damage too
            AppendSpecialAttackNotes(result, extraDamageNotes);

            result.Damage = rawWeaponDamage;
            result.RawTotalDamage = rawTotalDamage;

            // Build damage packet for DR/resistance/immunity resolution
            var damageTypes = weapon != null
                ? weapon.GetDamageTypes()
                : new System.Collections.Generic.HashSet<DamageType> { DamageType.Bludgeoning };

            DamageBypassTag attackTags = GetWeaponAttackBypassTags(weapon,
                weapon != null && (weapon.WeaponCat == WeaponCategory.Ranged || weapon.RangeIncrement > 0));

            var packet = new DamagePacket
            {
                RawDamage = rawTotalDamage,
                Types = damageTypes,
                AttackTags = attackTags,
                IsRanged = result.IsRangedAttack,
                IsNonlethal = dealNonlethalDamage,
                Source = AttackSource.Weapon,
                SourceName = string.IsNullOrEmpty(result.WeaponName) ? "attack" : result.WeaponName,
            };

            DamageResolutionResult mitigation = target.Stats.ApplyIncomingDamage(rawTotalDamage, packet);
            result.FinalDamageDealt = mitigation.FinalDamage;
            result.ResistancePrevented = mitigation.ResistanceApplied;
            result.DRPrevented = mitigation.DamageReductionApplied;
            result.ImmunityPrevented = mitigation.ImmunityTriggered;
            result.MitigationSummary = mitigation.GetMitigationSummary();
            result.DamageTypeSummary = DamageTextUtils.FormatDamageTypes(damageTypes);

            if (dealNonlethalDamage)
            {
                result.DamageTypeSummary = string.IsNullOrEmpty(result.DamageTypeSummary)
                    ? "nonlethal"
                    : $"{result.DamageTypeSummary}, nonlethal";
            }

            // A Vorpal beheading slays outright (DMG p.226): set after the damage, so regeneration's death prevention in
            // TakeDamage cannot keep the creature at -9 (ITM-067).
            if (vorpalBeheaded && target.Stats.CurrentHP > -10)
            {
                target.Stats.CurrentHP = -10;
                target.SyncHPStateFromCurrentHP(emitLog: false);
            }

            if (target.Stats.IsDead)
            {
                target.OnDeath();
                result.TargetKilled = true;
            }

            // ================================================================
            // STUNNING FIST (D&D 3.5 PHB p.101)
            // ================================================================
            // Stunning Fist must be declared before the attack roll (toggle), and
            // applies on a successful unarmed hit. The target must make a Fort save
            // or be stunned for 1 round. Uses are consumed even on miss (handled
            // by the toggle being reset after each attack attempt).
            if (!result.TargetKilled && Stats != null && Stats.StunningFistActive
                && FeatManager.HasStunningFist(Stats)
                && !isRangedAttack)
            {
                // Check if this is an unarmed attack (no weapon, or IUS)
                bool isUnarmedAttack = weapon == null || (weapon.Name ?? "").ToLowerInvariant().Contains("unarmed");
                if (isUnarmedAttack || FeatManager.HasImprovedUnarmedStrike(Stats))
                {
                    bool stunned = FeatManager.TryApplyStunningFist(Stats, target, this);
                    int dc = FeatManager.GetStunningFistDC(Stats);
                    if (stunned)
                    {
                        string stunNote = $"💫 STUNNING FIST: {target.Stats.CharacterName} is STUNNED for 1 round! (DC {dc})";
                        result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote)
                            ? stunNote : $"{result.SpecialAttackNote} {stunNote}";
                    }
                    else
                    {
                        string resistNote = $"Stunning Fist: {target.Stats.CharacterName} resists (DC {dc})";
                        result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote)
                            ? resistNote : $"{result.SpecialAttackNote} {resistNote}";
                    }
                }
                else
                {
                    // Not an unarmed attack — Stunning Fist wasted
                    Stats.StunningFistActive = false;
                }
            }

            // ================================================================
            // SPECIFIC ITEM BEHAVIOR: OnHitApplied + OnKill
            // ================================================================
            if (weapon != null && weapon.SpecificItemBehavior != null && target != null)
            {
                var behaviorHitNotes = new System.Collections.Generic.List<string>();

                // OnHitApplied: post-damage effects (sleep, ability damage, slaying, etc.)
                if (!target.Stats.IsDead)
                {
                    weapon.SpecificItemBehavior.OnHitApplied(target, result.FinalDamageDealt, behaviorHitNotes);

                    // Check if the behavior caused death (e.g., Slaying Arrow)
                    if (target.Stats.IsDead && !result.TargetKilled)
                    {
                        target.OnDeath();
                        result.TargetKilled = true;
                    }
                }

                // OnKill: kill-triggered effects (Cleave from Sylvan Scimitar, etc.)
                if (result.TargetKilled)
                {
                    weapon.SpecificItemBehavior.OnKill(target, behaviorHitNotes);
                }

                foreach (var note in behaviorHitNotes)
                {
                    result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote) ? note : $"{result.SpecialAttackNote} {note}";
                }
            }

            // ================================================================
            // DEFENSIVE ITEM BEHAVIORS: OnAttackedBy (on defender's equipped items)
            // ================================================================
            if (target != null)
            {
                Inventory defInv = target.GetInventoryData();
                if (defInv != null)
                {
                    var defNotes = new System.Collections.Generic.List<string>();
                    bool forceReroll = false;

                    // Check armor behavior
                    ItemData defArmor = defInv.ArmorRobeSlot;
                    if (defArmor?.SpecificItemBehavior != null)
                        defArmor.SpecificItemBehavior.OnAttackedBy(result, ref forceReroll, defNotes);

                    // Check shield behavior
                    ItemData defShield = (defInv.LeftHandSlot != null && defInv.LeftHandSlot.IsShield) ? defInv.LeftHandSlot : null;
                    if (defShield?.SpecificItemBehavior != null)
                        defShield.SpecificItemBehavior.OnAttackedBy(result, ref forceReroll, defNotes);

                    foreach (var note in defNotes)
                    {
                        result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote) ? note : $"{result.SpecialAttackNote} {note}";
                    }

                    // If a defensive behavior forced a reroll (Banded Mail of Luck), 
                    // note it in the result. Full reroll logic would re-invoke the attack.
                    if (forceReroll)
                    {
                        result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote)
                            ? "Forced reroll!"
                            : $"{result.SpecialAttackNote} Forced reroll!";
                        Debug.Log($"[SpecificItem] Defensive behavior forced attack reroll against {target.Stats?.CharacterName}");
                    }
                }
            }
        }

        result.RebuildBreakdownsFromComputedValues();
        return result;
    }

    // ========== WEAPON HELPERS ==========

    /// <summary>
    /// True if a spiked gauntlet is equipped in the Hands slot.
    /// </summary>
    public bool HasSpikedGauntletEquipped()
    {
        return EnsureEquipment().HasSpikedGauntletEquipped();
    }

    /// <summary>
    /// True if a standard gauntlet is equipped in the Hands slot.
    /// </summary>
    public bool HasGauntletEquipped()
    {
        return EnsureEquipment().HasGauntletEquipped();
    }

    /// <summary>
    /// True if the character has Improved Unarmed Strike for damage-mode defaults.
    /// </summary>
    private bool HasImprovedUnarmedStrikeForDamageMode()
    {
        return FeatManager.HasImprovedUnarmedStrike(Stats);
    }

    /// <summary>
    /// Returns true if the provided weapon (or current main weapon) is a reload-based crossbow and currently unloaded.
    /// </summary>
    public bool IsCrossbowUnloaded(ItemData weapon = null)
    {
        return EnsureEquipment().IsCrossbowUnloaded(weapon);
    }

    /// <summary>
    /// Check whether this character can currently attack with the equipped main weapon.
    /// </summary>
    public bool CanAttackWithEquippedWeapon(out string reason)
    {
        return CanAttackWithWeapon(GetEquippedMainWeapon(), out reason);
    }

    /// <summary>
    /// Check whether this character can currently attack with the specified weapon.
    /// </summary>
    public bool CanAttackWithWeapon(ItemData weapon, out string reason)
    {
        reason = string.Empty;

        if (!CanAttack())
        {
            reason = "Current condition prevents attacking.";
            return false;
        }

        if (HasCondition(CombatConditionType.Pinned))
        {
            reason = "Pinned creatures cannot attack.";
            return false;
        }

        if (HasCondition(CombatConditionType.Grappled))
        {
            bool usingUnarmed = weapon == null;
            bool usingLightWeapon = weapon != null && EnsureEquipment().IsWeaponLightForWielder(weapon);
            if (!usingUnarmed && !usingLightWeapon)
            {
                reason = "While grappled, only unarmed strikes or light weapons can be used.";
                return false;
            }
        }

        if (weapon == null) return true;

        if (weapon.RequiresReload && !weapon.IsLoaded)
        {
            reason = $"{weapon.Name} is unloaded and must be reloaded.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns true if the character has the matching Rapid Reload feat for the given crossbow.
    /// </summary>
    public bool HasRapidReloadForWeapon(ItemData weapon)
    {
        return EnsureEquipment().HasRapidReloadForWeapon(weapon);
    }

    /// <summary>
    /// Get effective reload action for a weapon after applying Rapid Reload, if present.
    /// </summary>
    public ReloadActionType GetEffectiveReloadAction(ItemData weapon)
    {
        return EnsureEquipment().GetEffectiveReloadAction(weapon);
    }

    /// <summary>
    /// Mark weapon as fired and apply automatic free-action reload if available.
    /// Returns a short status string for combat log purposes.
    /// </summary>
    public string OnWeaponFired(ItemData weapon)
    {
        return EnsureEquipment().OnWeaponFired(weapon);
    }

    /// <summary>
    /// Reload weapon state immediately (action-economy checks are handled by GameManager/UI).
    /// </summary>
    public bool ReloadWeapon(ItemData weapon)
    {
        return EnsureEquipment().ReloadWeapon(weapon);
    }

    /// <summary>
    /// Human-readable label for reload action type.
    /// </summary>
    public static string GetReloadActionLabel(ReloadActionType action)
    {
        switch (action)
        {
            case ReloadActionType.FreeAction: return "Free";
            case ReloadActionType.MoveAction: return "Move";
            case ReloadActionType.FullRound: return "Full-round";
            default: return "None";
        }
    }

    /// <summary>
    /// Build a short weapon load-state label for UI.
    /// </summary>
    public string GetWeaponLoadStateLabel(ItemData weapon = null)
    {
        return EnsureEquipment().GetWeaponLoadStateLabel(weapon);
    }

    /// <summary>
    /// Check if the equipped main weapon is an inherent ranged weapon.
    /// Thrown-capable melee weapons are still treated as melee by default
    /// unless a thrown attack mode is explicitly selected.
    /// </summary>
    public bool IsEquippedWeaponRanged()
    {
        return EnsureEquipment().IsEquippedWeaponRanged();
    }

    /// <summary>
    /// Returns true when the currently equipped primary weapon can be used in both melee and thrown modes.
    /// This is used for dual-action button presentation (Attack (Melee) + Attack (Thrown)).
    /// </summary>
    public bool HasThrowableWeaponEquipped()
    {
        return EnsureEquipment().HasThrowableWeaponEquipped();
    }

    /// <summary>
    /// Check if the equipped primary weapon is ranged.
    /// </summary>
    public bool HasRangedWeaponEquipped()
    {
        return EnsureEquipment().IsEquippedWeaponRanged();
    }

    /// <summary>
    /// Check if the character has a melee weapon equipped (or is unarmed, which counts as melee).
    /// D&D 3.5 Rule: Only characters with melee weapons (including natural/unarmed) threaten squares.
    /// Ranged-only characters do NOT threaten any squares and cannot make Attacks of Opportunity.
    /// </summary>
    public bool HasMeleeWeaponEquipped()
    {
        return EnsureEquipment().HasMeleeWeaponEquipped();
    }

    /// <summary>
    /// Returns true when this character is currently in a ranged-only combat loadout.
    /// </summary>
    public bool IsRangedOnlyCombatLoadout()
    {
        return HasRangedWeaponEquipped() && !HasMeleeWeaponEquipped();
    }

    /// <summary>
    /// Get a compact label for currently usable weapon mode.
    /// </summary>
    public string GetPrimaryWeaponType()
    {
        if (IsRangedOnlyCombatLoadout())
            return "Ranged";
        if (HasMeleeWeaponEquipped())
            return "Melee";
        return "Unarmed";
    }

    /// <summary>
    /// True when this actor can attempt melee-only special maneuvers.
    /// </summary>
    public bool CanPerformSpecialMeleeAttacks()
    {
        return !IsRangedOnlyCombatLoadout();
    }

    /// <summary>
    /// Validate whether current loadout can perform the requested special attack type.
    /// </summary>
    public bool CanPerformSpecialAttack(SpecialAttackType attackType)
    {
        switch (attackType)
        {
            case SpecialAttackType.Trip:
            case SpecialAttackType.Disarm:
            case SpecialAttackType.Grapple:
            case SpecialAttackType.Sunder:
            case SpecialAttackType.Overrun:
            case SpecialAttackType.CoupDeGrace:
                return CanPerformSpecialMeleeAttacks();
            // Bull rush needs no weapon: it is a body push resolved by Strength (PHB p.154).
            // Target legality is CanBullRush.
            case SpecialAttackType.BullRushAttack:
            case SpecialAttackType.BullRushCharge:
                return true;
            default:
                return true;
        }
    }

    /// <summary>
    /// Get all currently threatened squares for this character based on equipped melee weapon reach.
    /// Convenience wrapper over ThreatSystem for flanking/AI/UI queries.
    /// </summary>
    public List<Vector2Int> GetThreatenedSquares()
    {
        return new List<Vector2Int>(ThreatSystem.GetThreatenedSquares(this));
    }


    /// <summary>
    /// Current effective creature size.
    /// </summary>
    public SizeCategory GetCurrentSizeCategory()
    {
        return Stats != null ? Stats.CurrentSizeCategory : SizeCategory.Medium;
    }

    /// <summary>
    /// Current occupied footprint edge length in grid squares (1 => 1x1, 2 => 2x2, etc.).
    /// </summary>
    public int GetCurrentSpaceSquares()
    {
        return GetVisualSquaresOccupied();
    }

    /// <summary>
    /// Current natural reach in squares from size (before weapon-specific adjustments).
    /// </summary>
    public int GetCurrentNaturalReachSquares()
    {
        return Stats != null ? Stats.NaturalReachSquares : 1;
    }

    /// <summary>
    /// Apply a temporary size shift (e.g., Enlarge/Reduce) and refresh visuals/occupancy.
    /// </summary>
    public bool ChangeSize(int categoryDelta)
    {
        if (Stats == null) return false;

        SizeCategory oldSize = Stats.CurrentSizeCategory;
        bool changed = Stats.ChangeSize(categoryDelta);
        if (!changed)
            return false;

        Debug.Log($"[Size] {Stats.CharacterName}: ChangeSize {oldSize} -> {Stats.CurrentSizeCategory} (delta {categoryDelta:+#;-#;0})");

        if (!CanOccupyAtCurrentSize(GridPosition))
        {
            Stats.CurrentSizeCategory = oldSize;
            RecalculateInventoryStatsAfterSizeChange();
            Debug.LogWarning($"[Size] {Stats.CharacterName}: size change blocked - insufficient room at ({GridPosition.x},{GridPosition.y}).");
            UpdateVisualSize(false);
            RefreshGridOccupancy();
            return false;
        }

        RecalculateInventoryStatsAfterSizeChange();
        RefreshGridOccupancy();
        UpdateVisualSize();
        return true;
    }

    /// <summary>
    /// The current size with <paramref name="newBase"/> as the natural size and any temporary shift (Enlarge or Reduce
    /// Person) kept on top.
    /// </summary>
    private SizeCategory CurrentSizeForNaturalSize(SizeCategory newBase)
    {
        // Delegated RAW reading 2026-10-14 (CHR-020): wild shape sets the natural size and is not a size-increasing
        // effect, so an Enlarge or Reduce Person in effect keeps its one-step change on top of the form's size.
        int shift = (int)Stats.CurrentSizeCategory - (int)Stats.BaseSizeCategory;
        int shifted = Mathf.Clamp((int)newBase + shift, (int)SizeCategory.Fine, (int)SizeCategory.Colossal);
        return (SizeCategory)shifted;
    }

    /// <summary>
    /// Whether this creature's footprint at natural size <paramref name="newBase"/> fits where it stands (wild shape).
    /// A creature that is not on the grid has no footprint to check.
    /// </summary>
    public bool CanTakeNaturalSize(SizeCategory newBase)
    {
        if (Stats == null)
            return false;
        SquareGrid grid = CurrentGrid;
        if (grid == null || !grid.IsValidPosition(GridPosition))
            return true;
        SizeCategory oldCurrent = Stats.CurrentSizeCategory;
        Stats.CurrentSizeCategory = CurrentSizeForNaturalSize(newBase);
        bool fits = CanOccupyAtCurrentSize(GridPosition);
        Stats.CurrentSizeCategory = oldCurrent;
        return fits;
    }

    /// <summary>
    /// Sets the creature's natural size (a wild shape's form, or its own size when it changes back; WildShapeRules, PHB
    /// p.197), keeping a temporary size shift on top, and refreshes its footprint, weapon dice and token. The caller checks
    /// the room first (<see cref="CanTakeNaturalSize"/>); a change back always happens.
    /// </summary>
    public void SetNaturalSize(SizeCategory newBase)
    {
        if (Stats == null)
            return;
        SizeCategory current = CurrentSizeForNaturalSize(newBase);
        Stats.BaseSizeCategory = newBase;
        Stats.CurrentSizeCategory = current;
        RecalculateInventoryStatsAfterSizeChange();
        RefreshGridOccupancy();
        UpdateVisualSize(false);
    }

    private void RecalculateInventoryStatsAfterSizeChange()
    {
        Inventory inventoryData = GetInventoryData();
        if (inventoryData != null)
            inventoryData.RecalculateStats();
    }

    /// <summary>
    /// Updates visual token size to match current size category.
    /// </summary>
    public void UpdateVisualSize()
    {
        UpdateVisualSize(true);
    }

    /// <summary>
    /// Updates visual token size to match current size category.
    /// </summary>
    public void UpdateVisualSize(bool animate)
    {
        SizeCategory currentSize = GetCurrentSizeCategory();
        int squaresOccupied = GetVisualSquaresOccupied();
        float targetScale = currentSize.GetVisualTokenScale();

        if (!animate)
        {
            UpdateVisualSizeInstant();
        }
        else
        {
            if (_currentScaleAnimation != null)
            {
                StopCoroutine(_currentScaleAnimation);
                _currentScaleAnimation = null;
            }

            _currentScaleAnimation = StartCoroutine(SmoothScaleCoroutine(targetScale));
            UpdatePositionForSize();
        }

        Debug.Log($"[Size] {Stats?.CharacterName ?? gameObject.name}: UpdateVisualSize -> {currentSize}, footprint {squaresOccupied}x{squaresOccupied}, scale {targetScale:0.##}, animate={animate}");
    }

    /// <summary>
    /// Instantly updates token scale and position to match current size category.
    /// </summary>
    public void UpdateVisualSizeInstant()
    {
        if (_currentScaleAnimation != null)
        {
            StopCoroutine(_currentScaleAnimation);
            _currentScaleAnimation = null;
        }

        float targetScale = GetCurrentSizeCategory().GetVisualTokenScale();
        transform.localScale = new Vector3(targetScale, targetScale, 1f);
        UpdatePositionForSize();
    }

    private IEnumerator SmoothScaleCoroutine(float targetScale)
    {
        float duration = Mathf.Max(0f, _sizeChangeDuration);
        if (duration <= 0f)
        {
            transform.localScale = new Vector3(targetScale, targetScale, 1f);
            _currentScaleAnimation = null;
            yield break;
        }

        float startScale = transform.localScale.x;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = ApplyEasing(t, _sizeChangeEasing);
            float currentScale = Mathf.Lerp(startScale, targetScale, easedT);
            transform.localScale = new Vector3(currentScale, currentScale, 1f);
            yield return null;
        }

        transform.localScale = new Vector3(targetScale, targetScale, 1f);
        _currentScaleAnimation = null;
    }

    private float ApplyEasing(float t, AnimationEasing easing)
    {
        switch (easing)
        {
            case AnimationEasing.Linear:
                return t;
            case AnimationEasing.EaseOutCubic:
                return EaseOutCubic(t);
            case AnimationEasing.EaseInOutCubic:
                return EaseInOutCubic(t);
            case AnimationEasing.EaseOutBack:
                return EaseOutBack(t);
            case AnimationEasing.EaseOutElastic:
                return EaseOutElastic(t);
            default:
                return t;
        }
    }

    private float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private float EaseInOutCubic(float t)
    {
        return t < 0.5f
            ? 4f * t * t * t
            : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }

    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private float EaseOutElastic(float t)
    {
        const float c4 = (2f * Mathf.PI) / 3f;
        return t == 0f ? 0f
            : t == 1f ? 1f
            : Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
    }

    /// <summary>
    /// Backward-compatible wrapper.
    /// </summary>
    public void UpdateSizeVisuals()
    {
        UpdateVisualSize();
    }

    [ContextMenu("Test Visual Scaling")]
    public void TestVisualScaling()
    {
        Debug.Log($"[SizeTest] {Stats?.CharacterName ?? gameObject.name}: Tiny={SizeCategory.Tiny.GetVisualTokenScale()}, Small={SizeCategory.Small.GetVisualTokenScale()}, Medium={SizeCategory.Medium.GetVisualTokenScale()}, Large={SizeCategory.Large.GetVisualTokenScale()}");
    }

    private void UpdatePositionForSize()
    {
        SquareGrid grid = CurrentGrid;
        if (grid != null)
        {
            transform.position = grid.GetCenteredWorldPosition(GridPosition, GetVisualSquaresOccupied());
            return;
        }

        Vector3 basePosition = SquareGridUtils.GridToWorld(GridPosition);
        int squares = GetVisualSquaresOccupied();
        if (squares <= 1)
        {
            transform.position = basePosition;
            return;
        }

        float offset = (squares - 1) * SquareGridUtils.CellSize * 0.5f;
        transform.position = basePosition + new Vector3(offset, offset, 0f);
    }

    private void SetTokenVisible(bool visible)
    {
        if (_sr == null)
            _sr = GetComponent<SpriteRenderer>();

        if (_sr != null)
            _sr.enabled = visible;
    }

    private void StopGrappleAlternatingDisplay(bool ensureVisible)
    {
        if (_grappleAlternateVisibilityCoroutine != null)
        {
            StopCoroutine(_grappleAlternateVisibilityCoroutine);
            _grappleAlternateVisibilityCoroutine = null;
        }

        if (ensureVisible)
            SetTokenVisible(true);
    }

    public void SetGrappleContextMenuDisplayLocked(bool isLocked)
    {
        if (!TryGetGrappleLink(this, out GrappleLink link) || link == null)
            return;

        CharacterController controller = link.Controller;
        CharacterController defender = link.Defender;
        if (controller == null || defender == null)
            return;

        if (isLocked)
        {
            controller._grappleDisplayPauseLocks++;
            defender._grappleDisplayPauseLocks++;

            controller.StopGrappleAlternatingDisplay(ensureVisible: true);
            defender.StopGrappleAlternatingDisplay(ensureVisible: true);
            controller.SetTokenVisible(true);
            defender.SetTokenVisible(true);
            return;
        }

        controller._grappleDisplayPauseLocks = Mathf.Max(0, controller._grappleDisplayPauseLocks - 1);
        defender._grappleDisplayPauseLocks = Mathf.Max(0, defender._grappleDisplayPauseLocks - 1);

        bool shouldResumeAlternatingDisplay = controller._grappleDisplayPauseLocks == 0
            && defender._grappleDisplayPauseLocks == 0
            && controller.TryGetGrappleState(out CharacterController controllerOpponent, out _, out _, out _)
            && controllerOpponent == defender;

        if (shouldResumeAlternatingDisplay)
            StartGrappleAlternatingDisplay(controller, defender);
    }

    private static void StartGrappleAlternatingDisplay(CharacterController initiator, CharacterController target)
    {
        if (initiator == null || target == null)
            return;

        initiator.StopGrappleAlternatingDisplay(ensureVisible: true);
        target.StopGrappleAlternatingDisplay(ensureVisible: true);

        initiator._grappleAlternateVisibilityCoroutine = initiator.StartCoroutine(
            initiator.RunGrappleAlternatingDisplay(target));
    }

    private IEnumerator RunGrappleAlternatingDisplay(CharacterController target)
    {
        bool showInitiator = true;

        while (target != null
            && TryGetGrappleState(out CharacterController myOpponent, out _, out _, out _)
            && myOpponent == target
            && target.TryGetGrappleState(out CharacterController theirOpponent, out _, out _, out _)
            && theirOpponent == this)
        {
            if (_grappleDisplayPauseLocks > 0 || target._grappleDisplayPauseLocks > 0)
            {
                SetTokenVisible(true);
                target.SetTokenVisible(true);
                yield return new WaitForSeconds(0.1f);
                continue;
            }

            SetTokenVisible(showInitiator);
            target.SetTokenVisible(!showInitiator);

            yield return new WaitForSeconds(GrappleAlternateVisibilitySeconds);
            showInitiator = !showInitiator;
        }

        SetTokenVisible(true);
        if (target != null)
            target.SetTokenVisible(true);

        _grappleAlternateVisibilityCoroutine = null;
    }
    /// <summary>
    /// Get minimum melee distance (in squares) this character can attack with current weapon.
    /// Most melee weapons: 1. Reach-only weapons: 2. Whip: 2 (with max 3).
    /// </summary>
    public int GetMeleeMinAttackDistance(ItemData weapon = null)
    {
        weapon ??= GetEquippedMainWeapon();

        // Unarmed attack is always adjacent.
        if (weapon == null || weapon.WeaponCat != WeaponCategory.Melee)
            return 1;

        bool canAttackAdjacent = weapon.CanAttackAdjacent;
        int maxReach = GetMeleeMaxAttackDistance(weapon);

        if (canAttackAdjacent) return 1;
        return maxReach >= 2 ? 2 : 1;
    }

    /// <summary>
    /// Get maximum melee distance (in squares) this character can attack with current weapon.
    /// </summary>
    public int GetMeleeMaxAttackDistance(ItemData weapon = null)
    {
        weapon ??= GetEquippedMainWeapon();

        int naturalReach = Stats != null ? Mathf.Max(1, Stats.NaturalReachSquares) : 1;

        if (weapon == null || weapon.WeaponCat != WeaponCategory.Melee)
            return naturalReach;

        int weaponReach = weapon.ReachSquares > 0 ? weapon.ReachSquares : weapon.AttackRange;
        weaponReach = Mathf.Max(1, weaponReach);

        // Reach weapons stack with larger creature natural reach in this prototype.
        // Example: Large creature (natural 2) + longspear (reach 2) => 3 squares.
        if (weapon.IsReachWeapon && naturalReach > 1)
            return weaponReach + (naturalReach - 1);

        return Mathf.Max(weaponReach, naturalReach);
    }

    /// <summary>
    /// Returns true if the specified square distance is legal for this character's current melee weapon.
    /// Distance 0 means the target is in one of this creature's own squares (a square shared after a pin
    /// release, CMB-089; a swarm or a Tiny creature). You can attack into your own square (PHB p.149), and you
    /// threaten every square you can attack into (PHB p.137), so distance 0 is legal for every creature and
    /// weapon that can attack an adjacent foe; a reach weapon cannot (PHB p.113). Owner ruling 2026-10-08 (CMB-131).
    /// </summary>
    public bool CanMeleeAttackDistance(int squareDistance, ItemData weapon = null)
    {
        if (squareDistance < 0) return false;
        int minDist = GetMeleeMinAttackDistance(weapon);
        if (squareDistance == 0) return minDist <= 1;
        int maxDist = GetMeleeMaxAttackDistance(weapon);
        return squareDistance >= minDist && squareDistance <= maxDist;
    }

    /// <summary>
    /// Get the currently equipped weapon used for primary attacks.
    /// Alias retained for gameplay systems that expect a "GetEquippedWeapon" helper.
    /// </summary>
    public ItemData GetEquippedWeapon()
    {
        return EnsureEquipment().GetEquippedWeapon();
    }

    /// <summary>
    /// Check whether this character threatens a target with the provided (or currently equipped) weapon.
    /// Uses melee min/max reach constraints (Chebyshev distance) to match D&D 3.5e adjacency/reach behavior.
    /// </summary>
    public bool ThreatensWith(CharacterController target, ItemData weapon = null)
    {
        if (target == null || Stats == null || target.Stats == null)
            return false;

        weapon ??= GetEquippedWeapon();

        int distance = CalculateDistance(target);
        int minReach = GetMeleeMinAttackDistance(weapon);
        int maxReach = GetWeaponReach(weapon);
        bool threatens = CanMeleeAttackDistance(distance, weapon);

        string weaponName = weapon != null ? weapon.Name : "Unarmed Strike";
        string selfName = Stats != null ? Stats.CharacterName : "Unknown";
        string targetName = target.Stats != null ? target.Stats.CharacterName : "Unknown";
        Debug.Log($"[AidAnother] {selfName} threatens {targetName} with {weaponName}? {threatens} (distance={distance}, minReach={minReach}, maxReach={maxReach})");

        return threatens;
    }

    /// <summary>
    /// Get the maximum melee threat distance in grid squares for the specified weapon.
    /// </summary>
    private int GetWeaponReach(ItemData weapon)
    {
        return GetMeleeMaxAttackDistance(weapon);
    }

    /// <summary>
    /// Calculate Chebyshev grid distance to another character.
    /// </summary>
    private int CalculateDistance(CharacterController target)
    {
        return GetMinimumDistanceToTargetSquares(target, chebyshev: true);
    }

    public int GetMinimumDistanceToTarget(CharacterController target, bool chebyshev = true)
    {
        return GetMinimumDistanceToTargetSquares(target, chebyshev);
    }

    private int GetMinimumDistanceToTargetSquares(CharacterController target, bool chebyshev)
    {
        if (target == null) return int.MaxValue;

        List<Vector2Int> mySquares = GetOccupiedSquares();
        List<Vector2Int> targetSquares = target.GetOccupiedSquares();
        int minDist = int.MaxValue;

        for (int i = 0; i < mySquares.Count; i++)
        {
            for (int j = 0; j < targetSquares.Count; j++)
            {
                int distance = chebyshev
                    ? SquareGridUtils.GetChebyshevDistance(mySquares[i], targetSquares[j])
                    : SquareGridUtils.GetDistance(mySquares[i], targetSquares[j]);

                if (distance < minDist)
                    minDist = distance;
            }
        }

        return minDist == int.MaxValue ? 0 : minDist;
    }

    private bool IsTargetInWeaponRange(CharacterController target, ItemData weapon, bool useThrownRange)
    {
        if (target == null || target.Stats == null || Stats == null)
            return false;

        if (useThrownRange)
            return IsTargetInThrownWeaponRange(target, weapon);

        bool isRanged = weapon != null && weapon.WeaponCat == WeaponCategory.Ranged;
        if (isRanged)
        {
            int distance = GetMinimumDistanceToTargetSquares(target, chebyshev: false);
            int rangeIncrement = weapon.RangeIncrement;
            bool isThrownWeapon = weapon.IsThrown;

            if (rangeIncrement > 0)
            {
                int distFeet = RangeCalculator.SquaresToFeet(distance);
                return RangeCalculator.IsWithinMaxRange(distFeet, rangeIncrement, isThrownWeapon);
            }

            return distance <= Mathf.Max(1, Stats.AttackRange);
        }

        int meleeDistance = GetMinimumDistanceToTargetSquares(target, chebyshev: true);
        return CanMeleeAttackDistance(meleeDistance, weapon);
    }

    /// <summary>
    /// Check if this target is in range of the currently equipped weapon in default mode
    /// (melee for melee weapons, ranged for ranged weapons).
    /// </summary>
    public bool IsTargetInCurrentWeaponRange(CharacterController target)
    {
        if (target == null || target.Stats == null || Stats == null) return false;

        if (Stats.IsSwarm)
        {
            int swarmDistance = GetMinimumDistanceToTargetSquares(target, chebyshev: true);
            return swarmDistance == 0;
        }

        ItemData weapon = GetEquippedMainWeapon();

        bool isRanged = weapon != null && weapon.WeaponCat == WeaponCategory.Ranged;
        if (isRanged)
        {
            // For multi-square creatures, use nearest occupied-square pair.
            int distance = GetMinimumDistanceToTargetSquares(target, chebyshev: false);
            int rangeIncrement = weapon.RangeIncrement;
            bool isThrownWeapon = weapon.IsThrown;

            if (rangeIncrement > 0)
            {
                int distFeet = RangeCalculator.SquaresToFeet(distance);
                return RangeCalculator.IsWithinMaxRange(distFeet, rangeIncrement, isThrownWeapon);
            }

            return distance <= Mathf.Max(1, Stats.AttackRange);
        }

        // Reach/threat uses Chebyshev distance so diagonals count the same as orthogonal squares.
        int meleeDistance = GetMinimumDistanceToTargetSquares(target, chebyshev: true);
        return CanMeleeAttackDistance(meleeDistance, weapon);
    }

    /// <summary>
    /// Check if this target is in thrown range for a thrown-capable weapon.
    /// </summary>
    public bool IsTargetInThrownWeaponRange(CharacterController target, ItemData thrownWeapon = null)
    {
        if (target == null || target.Stats == null || Stats == null)
            return false;

        ItemData weapon = thrownWeapon ?? GetEquippedMainWeapon();
        if (weapon == null || !weapon.IsThrown || weapon.RangeIncrement <= 0)
            return false;

        int distance = GetMinimumDistanceToTargetSquares(target, chebyshev: false);
        int distFeet = RangeCalculator.SquaresToFeet(distance);
        return RangeCalculator.IsWithinMaxRange(distFeet, weapon.RangeIncrement, true);
    }

    /// <summary>
    /// D&D 3.5 whip rule: standard whip cannot harm creatures with armor bonus +1 or natural armor +1.
    /// </summary>
    public bool IsTargetImmuneToWhipDamage(CharacterController target, ItemData weapon)
    {
        if (target == null || target.Stats == null || weapon == null) return false;
        if (!weapon.WhipLikeArmorRestriction) return false;

        int armorLikeBonus = Mathf.Max(0, target.Stats.ArmorBonus + target.Stats.NaturalArmorBonus);
        return armorLikeBonus >= 1;
    }
    /// <summary>
    /// Get the equipped primary attack weapon.
    /// Priority: right-hand weapon, left-hand weapon, then spiked gauntlet in Hands slot.
    /// Returns null if no weapon-equivalent item is available (unarmed).
    /// </summary>
    public ItemData GetEquippedMainWeapon()
    {
        return EnsureEquipment().GetEquippedMainWeapon();
    }

    /// <summary>
    /// True when any weapon-equivalent item is equipped (including weapons/shields in hand slots and hands-slot spiked gauntlet).
    /// </summary>
    public bool HasWeaponEquipped()
    {
        return EnsureEquipment().HasWeaponEquipped();
    }

    /// <summary>
    /// True when this character currently has at least one disarmable held item in right/left hand.
    /// </summary>
    public bool HasDisarmableWeaponEquipped()
    {
        return EnsureEquipment().HasDisarmableWeaponEquipped();
    }

    /// <summary>
    /// Returns equipped held items in the hand slots that are valid disarm targets (right, then left).
    /// </summary>
    public List<DisarmableHeldItemOption> GetDisarmableHeldItemOptions()
    {
        return EnsureEquipment().GetDisarmableHeldItemOptions();
    }

    public bool HasSunderableItemEquipped()
    {
        return EnsureEquipment().HasSunderableItemEquipped();
    }

    public List<SunderableItemOption> GetSunderableItemOptions()
    {
        return EnsureEquipment().GetSunderableItemOptions();
    }

    /// <summary>
    /// Returns equipped hand-slot light weapons (right hand first, then left hand).
    /// Used by grapple "Use Opponent's Weapon" availability and resolution.
    /// </summary>
    public List<DisarmableHeldItemOption> GetEquippedLightHandWeaponOptions()
    {
        return EnsureEquipment().GetEquippedLightHandWeaponOptions();
    }

    /// <summary>
    /// Returns the equipped main-hand (right-hand) weapon if present.
    /// This is used for grapple light-weapon attacks that are intentionally constrained to main hand.
    /// </summary>
    public ItemData GetEquippedMainHandWeapon()
    {
        return EnsureEquipment().GetEquippedMainHandWeapon();
    }

    /// <summary>
    /// D&D 3.5 grapple option: attack with a light weapon at -4 attack penalty.
    /// Availability is constrained to the equipped main-hand weapon.
    /// </summary>
    public bool CanAttackWithLightWeaponWhileGrappling(out ItemData mainHandLightWeapon, out string reason)
    {
        return EnsureEquipment().CanAttackWithLightWeaponWhileGrappling(out mainHandLightWeapon, out reason);
    }

    /// <summary>
    /// D&D 3.5 grapple option: attack unarmed at -4 attack penalty.
    /// </summary>
    public bool CanAttackUnarmedWhileGrappling(out string reason)
    {
        reason = string.Empty;
        return true;
    }

    // ========== DUAL WIELD INFO ==========

    /// <summary>
    /// Get a description of dual wield status for UI display.
    /// </summary>
    public string GetDualWieldDescription()
    {
        return EnsureEquipment().GetDualWieldDescription();
    }

    // ========== GRAPPLE STATE ==========

    private static bool TryGetGrappleLink(CharacterController actor, out GrappleLink link)
    {
        link = null;
        if (actor == null)
            return false;

        if (!_grappleLinksByCharacter.TryGetValue(actor, out link) || link == null)
            return false;

        if (link.Controller == null || link.Defender == null || link.Controller == link.Defender)
        {
            _grappleLinksByCharacter.Remove(actor);
            link = null;
            return false;
        }

        return true;
    }

    private static void RegisterGrappleLink(GrappleLink link)
    {
        if (link == null || link.Controller == null || link.Defender == null)
            return;

        _grappleLinksByCharacter[link.Controller] = link;
        _grappleLinksByCharacter[link.Defender] = link;
    }

    private static void UnregisterGrappleLink(GrappleLink link)
    {
        if (link == null)
            return;

        if (link.Controller != null)
            _grappleLinksByCharacter.Remove(link.Controller);
        if (link.Defender != null)
            _grappleLinksByCharacter.Remove(link.Defender);
    }

    private static void RemoveConditionIfPresent(CharacterController actor, CombatConditionType condition)
    {
        if (actor == null || actor.Stats == null)
            return;

        if (actor.HasCondition(condition))
            actor.RemoveCondition(condition);
    }

    private static void ApplyConditionIfMissing(CharacterController actor, CombatConditionType condition, string sourceName)
    {
        if (actor == null || actor.Stats == null)
            return;

        if (!actor.HasCondition(condition))
            actor.ApplyCondition(condition, -1, sourceName);
    }

    private static void SetPinnedState(GrappleLink link, CharacterController pinned, CharacterController maintainer)
    {
        if (link == null || pinned == null || pinned.Stats == null)
            return;

        CharacterController previousPinned = link.PinnedCharacter;
        CharacterController previousMaintainer = link.PinMaintainer;

        if (previousPinned != null && previousPinned != pinned)
        {
            RemoveConditionIfPresent(previousPinned, CombatConditionType.Pinned);
            previousPinned.ClearPinnedBy();
        }

        if (previousMaintainer != null && previousMaintainer != maintainer)
            previousMaintainer.ReleasePinningState();

        link.PinnedCharacter = pinned;
        link.PinMaintainer = maintainer;
        // PHB p.156: the pin lasts 1 round, until the maintainer's next turn starts (a renewal restarts it).
        link.PinRoundEndsAtMaintainerTurnStart = maintainer != null ? maintainer._turnsStartedCount + 1 : 0;

        if (maintainer != null)
            maintainer.SetPinningOpponent(pinned);
        pinned.SetPinnedBy(maintainer);

        string sourceName = maintainer != null && maintainer.Stats != null
            ? maintainer.Stats.CharacterName
            : "Grapple";
        pinned.ApplyCondition(CombatConditionType.Pinned, -1, sourceName);
    }

    private static void ClearPinnedState(GrappleLink link)
    {
        if (link == null)
            return;

        CharacterController pinned = link.PinnedCharacter;
        CharacterController maintainer = link.PinMaintainer;

        if (pinned != null)
        {
            RemoveConditionIfPresent(pinned, CombatConditionType.Pinned);
            pinned.ClearPinnedBy();
        }

        if (maintainer != null)
            maintainer.ReleasePinningState();

        link.PinnedCharacter = null;
        link.PinMaintainer = null;
        link.PinRoundEndsAtMaintainerTurnStart = 0;
    }

    private static void EndGrappleLink(GrappleLink link, string reason)
    {
        if (link == null)
            return;

        CharacterController controller = link.Controller;
        CharacterController defender = link.Defender;

        ClearPinnedState(link);
        UnregisterGrappleLink(link);

        if (controller != null)
        {
            RemoveConditionIfPresent(controller, CombatConditionType.Grappled);
            controller.ReleasePinningState();
            controller.ClearPinnedBy();
        }

        if (defender != null)
        {
            RemoveConditionIfPresent(defender, CombatConditionType.Grappled);
            defender.ReleasePinningState();
            defender.ClearPinnedBy();
        }

        if (controller != null)
        {
            controller._grappleDisplayPauseLocks = 0;
            controller.StopGrappleAlternatingDisplay(ensureVisible: true);
        }

        if (defender != null)
        {
            defender._grappleDisplayPauseLocks = 0;
            defender.StopGrappleAlternatingDisplay(ensureVisible: true);
        }

        if (!string.IsNullOrEmpty(reason))
            Debug.Log($"[Grapple] Link ended ({reason}).");
    }

    /// <summary>Starts a grapple link with <paramref name="target"/> (a grapple, not an Improved Grab).</summary>
    private string EstablishGrappleWith(CharacterController target) => EstablishGrappleLink(target, null, false);

    /// <summary>
    /// Starts the grapple link with <paramref name="target"/>. <paramref name="establishingAttack"/> is the natural attack
    /// whose Improved Grab started it (MM p.310: its damage on later successful Damage checks, CMB-148);
    /// <paramref name="holdWithBodyPart"/> is the Improved Grab hold with that body part, in which this creature is not
    /// grappled (MM p.310; CMB-148, CMB-149).
    /// </summary>
    private string EstablishGrappleLink(CharacterController target, NaturalAttackDefinition establishingAttack, bool holdWithBodyPart)
    {
        if (target == null || target == this)
            return string.Empty;

        ReleaseGrappleState("new grapple established");
        target.ReleaseGrappleState("new grapple established");

        var link = new GrappleLink
        {
            Controller = this,
            Defender = target,
            PinnedCharacter = null,
            PinMaintainer = null,
            PinRoundEndsAtMaintainerTurnStart = 0,
            EstablishingAttack = establishingAttack,
            EstablishedAtControllerTurnStart = _turnsStartedCount,
            ControllerHoldsWithBodyPart = holdWithBodyPart && establishingAttack != null
        };

        RegisterGrappleLink(link);
        if (!link.ControllerHoldsWithBodyPart)
            ApplyConditionIfMissing(this, CombatConditionType.Grappled, target.Stats != null ? target.Stats.CharacterName : "Grapple");
        ApplyConditionIfMissing(target, CombatConditionType.Grappled, Stats != null ? Stats.CharacterName : "Grapple");

        RemoveConditionIfPresent(this, CombatConditionType.Pinned);
        RemoveConditionIfPresent(target, CombatConditionType.Pinned);
        ReleasePinningState();
        target.ReleasePinningState();
        ClearPinnedBy();
        target.ClearPinnedBy();

        string positioningLog = PositionGrapplingCharacters(this, target);
        StartGrappleAlternatingDisplay(this, target);
        return positioningLog;
    }

    public void ReleaseGrappleState(string reason = "")
    {
        if (!TryGetGrappleLink(this, out GrappleLink link))
            return;

        EndGrappleLink(link, reason);
    }

    public bool TryGetGrappleState(out CharacterController opponent, out bool isController, out bool isPinned, out bool opponentPinned)
    {
        opponent = null;
        isController = false;
        isPinned = false;
        opponentPinned = false;

        if (!TryGetGrappleLink(this, out GrappleLink link))
            return false;

        opponent = link.GetOpponent(this);
        if (opponent == null || opponent.Stats == null || opponent.Stats.IsDead)
        {
            EndGrappleLink(link, "opponent unavailable");
            opponent = null;
            return false;
        }

        // MM p.310: a creature that holds with one body part after Improved Grab is not considered grappled; it acts
        // normally (TryGetImprovedGrabHold describes its hold). The held creature is grappled by it as usual.
        if (link.ControllerHoldsWithBodyPart && link.Controller == this)
        {
            opponent = null;
            return false;
        }

        isController = link.Controller == this;
        isPinned = link.PinnedCharacter == this;
        opponentPinned = link.PinnedCharacter == opponent;
        return true;
    }

    public bool IsInActiveGrapple()
    {
        return TryGetGrappleState(out _, out _, out _, out _);
    }

    /// <summary>
    /// True when this creature is part of any grapple link: grappling, grappled, or in an Improved Grab hold with one
    /// body part on either side (the holder is not <see cref="IsGrappling"/>, MM p.310). <paramref name="other"/> is the
    /// other creature of the link. Every "already in a grapple" gate that starts a grapple reads this, because a
    /// creature has one grapple link at a time (a second one would break the first; CMB-163).
    /// </summary>
    public bool IsInAnyGrappleLink(out CharacterController other)
    {
        other = null;
        if (!TryGetGrappleLink(this, out GrappleLink link) || link == null)
            return false;

        other = link.GetOpponent(this);
        return true;
    }

    /// <summary>
    /// Convenience alias for UI/gameplay checks that need to know whether this character
    /// is currently in an active grapple.
    /// </summary>
    public bool IsGrappling()
    {
        return IsInActiveGrapple();
    }

    // ----- Improved Grab hold with one body part (MM p.310; owner ruling 2026-10-11, CMB-148, CMB-149) -----
    // A creature with Improved Grab may conduct the grapple normally or simply hold the opponent with the body part
    // that made the attack. Holding: -20 on its grapple checks, and it is not considered grappled: no Grappled
    // condition, so it keeps its Dex bonus to AC, threatens, makes attacks of opportunity and takes no -4 on attacks,
    // and its remaining attacks go on against other opponents. The held creature is grappled by it. The controller
    // chooses (PC prompt, AI AIProfile.ShouldHoldWithImprovedGrab) and both reach ResolveImprovedGrabFreeAttempt.
    // In later rounds the holder may make the Damage grapple check against the held creature in place of an attack
    // (at -20; a success deals the establishing attack's damage, MM p.310; IsGrappleActionAvailableToImprovedGrabHolder),
    // and when it moves it carries the held creature along while it can drag its weight (MM p.310, PHB p.162;
    // CheckImprovedGrabHoldReach). The hold's details are delegated RAW readings of 2026-10-14 (RULES_COVERAGE 4.1,
    // CMB-163): the -20 also applies to the free check that starts the hold (the choice comes first); the holder may let
    // go as a free action on its turn (ReleaseImprovedGrabHold); it may not attack the held creature in any form,
    // attacks of opportunity included (IsBarredFromOrdinaryAttackOn, IsBarredFromSpecialAttackOn,
    // ThreatSystem.CanMakeAoOAgainst); its only grapple action is the Damage check; the held creature cannot pin or
    // move its holder; the drag limit uses the holder's body plan (PHB p.162). Known deviation (CMB-163): a creature has
    // one grapple link at a time, so a holder starts no second hold or grapple and cannot be grabbed (IsInAnyGrappleLink),
    // which RAW allows. Data gap: no creature weights are stored (GetTypicalCreatureWeightUpperBoundLbs).

    /// <summary>The grapple check penalty of an Improved Grab hold with one body part (MM p.310).</summary>
    public const int ImprovedGrabHoldGrappleCheckPenalty = -20;

    /// <summary>True when this creature holds another with one body part after Improved Grab (MM p.310; see above).</summary>
    public bool IsHoldingWithImprovedGrab() => TryGetImprovedGrabHold(out _, out _);

    /// <summary>
    /// The creature this one holds with one body part after Improved Grab, and the natural attack (body part) that holds it.
    /// False when it holds none; a hold whose held creature is gone ends here.
    /// </summary>
    public bool TryGetImprovedGrabHold(out CharacterController held, out NaturalAttackDefinition bodyPart)
    {
        held = null;
        bodyPart = null;
        if (!TryGetGrappleLink(this, out GrappleLink link) || link == null || !link.ControllerHoldsWithBodyPart || link.Controller != this)
            return false;

        if (link.Defender == null || link.Defender.Stats == null || link.Defender.Stats.IsDead)
        {
            EndGrappleLink(link, "held creature unavailable");
            return false;
        }

        held = link.Defender;
        bodyPart = link.EstablishingAttack;
        return true;
    }

    /// <summary>True when this creature is held by <paramref name="holder"/>'s Improved Grab hold with one body part (MM p.310).</summary>
    public bool IsHeldByImprovedGrabHold(out CharacterController holder)
    {
        holder = null;
        if (!TryGetGrappleLink(this, out GrappleLink link) || link == null || !link.ControllerHoldsWithBodyPart || link.Defender != this)
            return false;

        holder = link.Controller;
        return holder != null;
    }

    /// <summary>
    /// MM p.310: a creature holding with one body part "can use its remaining attacks against other opponents", so it
    /// may not attack the creature it holds in any form: melee, iterative, off-hand, natural, ranged (ranged touch
    /// special attacks such as Spittle included), charge, smite, trip, disarm, sunder, bull rush, coup de grace
    /// (<see cref="IsBarredFromSpecialAttackOn"/>) or an attack of opportunity
    /// (ThreatSystem.CanMakeAoOAgainst, ExecuteAoO). It harms the held creature only through the hold's Damage check
    /// (delegated RAW reading 2026-10-14, CMB-163). Spells and other effects that are not attacks are not barred. A
    /// creature entry that grants an attack on the held opponent (the giant praying mantis's hold bite, MM p.285; the
    /// giant soldier ant's sting, MM p.284) would override this; no database creature has one yet (CRE-050).
    /// Read by the PC attack, off-hand, ranged-retarget and smite clicks (and the template smite target test), the PC
    /// full-attack retarget, Cleave and Whirlwind, both charge paths (CanChargeTarget), the NPC melee sequence and ranged
    /// branch, the summon smite and target choice, the AI ranged special attack and its free-Spittle target, the coup de
    /// grace candidate list, the AI target filter and every AoO path.
    /// </summary>
    public bool IsBarredFromOrdinaryAttackOn(CharacterController target, out string reason)
    {
        reason = string.Empty;
        if (target == null || !TryGetImprovedGrabHold(out CharacterController held, out NaturalAttackDefinition bodyPart) || held != target)
            return false;

        string partName = bodyPart != null && !string.IsNullOrWhiteSpace(bodyPart.Name) ? bodyPart.Name.Trim().ToLowerInvariant() : "body part";
        reason = $"{Stats.CharacterName} holds {target.Stats.CharacterName} with its {partName} and can use its other attacks only against other opponents (MM p.310).";
        return true;
    }

    /// <summary>
    /// True for the special attacks that are attacks, so the Improved Grab hold bar applies to them (delegated RAW reading
    /// 2026-10-14, CMB-163): trip, disarm and sunder (made in place of a melee attack, PHB p.155-158), the coup de grace
    /// (a melee attack that hits automatically, PHB p.153) and the bull rush (PHB p.154: a standard action "(an attack)" or
    /// part of a charge, which is barred through CanChargeTarget as well). Not attacks: overrun (a standard action taken
    /// during a move, PHB p.157), feint (a Bluff check, PHB p.155), aid another (an attack roll against AC 10 that helps
    /// an ally and does nothing to the opponent, PHB p.154), waking an ally and turning undead. Grapple is governed by the
    /// one-grapple-link rule instead.
    /// </summary>
    public static bool IsSpecialAttackAnAttack(SpecialAttackType type)
        => type == SpecialAttackType.Trip
            || type == SpecialAttackType.Disarm
            || type == SpecialAttackType.Sunder
            || type == SpecialAttackType.CoupDeGrace
            || type == SpecialAttackType.BullRushAttack
            || type == SpecialAttackType.BullRushCharge;

    /// <summary>
    /// The Improved Grab hold bar (<see cref="IsBarredFromOrdinaryAttackOn"/>) for a special attack: a holder may not trip,
    /// disarm, sunder, bull rush or coup de grace the creature it holds (delegated RAW reading 2026-10-14, CMB-163). Shared by the
    /// PC special-attack targeting and click, the NPC special-attack executor and the resolver.
    /// </summary>
    public bool IsBarredFromSpecialAttackOn(SpecialAttackType type, CharacterController target, out string reason)
    {
        reason = string.Empty;
        return IsSpecialAttackAnAttack(type) && IsBarredFromOrdinaryAttackOn(target, out reason);
    }

    /// <summary>
    /// The natural attack whose damage this creature's successful grapple checks deal (MM p.310, CMB-148): the attack that
    /// established the grapple through its Improved Grab, from the round after it was established ("successive rounds");
    /// null otherwise (the Damage action then deals unarmed strike damage). The Damage action deals it in place of the
    /// unarmed strike; a won pin, move, use-opponent's-weapon or pin-escape check adds it (AppendImprovedGrabCheckDamage).
    /// Shared by the resolver, the PC damage menu and the AI.
    /// </summary>
    public NaturalAttackDefinition GetImprovedGrabDamageAttackForGrappleCheck()
    {
        if (!TryGetGrappleLink(this, out GrappleLink link) || link == null || link.Controller != this || link.EstablishingAttack == null)
            return null;

        return _turnsStartedCount > link.EstablishedAtControllerTurnStart ? link.EstablishingAttack : null;
    }

    /// <summary>The damage-mode rule of this creature's Damage grapple action (<see cref="GetGrappleDamageModeRule"/>).</summary>
    public struct GrappleDamageModeRule
    {
        /// <summary>The Improved Grab establishing attack whose damage the check deals, or null (unarmed strike damage).</summary>
        public NaturalAttackDefinition HoldAttack;
        public AttackDamageMode DefaultMode;
        /// <summary>The grapple check modifier for lethal damage (0 or -4).</summary>
        public int LethalPenalty;
        /// <summary>The grapple check modifier for nonlethal damage (0 or -4).</summary>
        public int NonlethalPenalty;
        /// <summary>Why the default is lethal with no penalty, for the log and the menu; null for the plain unarmed strike.</summary>
        public string Reason;
    }

    /// <summary>
    /// The damage mode of the Damage grapple action, shared by the resolver (the DamageOpponent case of
    /// ResolveGrappleActionCore), the PC damage-mode menu (GameManager.ShowGrappleDamageModeMenu) and the AI:
    /// after an Improved Grab, the establishing natural attack's damage, lethal by default and nonlethal at -4 (MM p.310;
    /// PHB p.146; delegated RAW reading 2026-10-14). Otherwise unarmed strike damage (PHB p.156): nonlethal by default,
    /// lethal at -4; a monk's lethal by default and nonlethal with no penalty (PHB p.156 exception); with Improved
    /// Unarmed Strike either mode with no penalty, lethal by default (PHB p.96: lethal or nonlethal at its option;
    /// delegated RAW reading 2026-10-14); with a gauntlet as the unarmed-strike attack code treats it (PHB p.118: a
    /// gauntlet strike is an unarmed attack that deals lethal damage; ResolveDamageModeAttackProfileCore): lethal by
    /// default, either mode with no penalty (delegated RAW reading 2026-10-14).
    /// </summary>
    public GrappleDamageModeRule GetGrappleDamageModeRule()
    {
        NaturalAttackDefinition holdAttack = GetImprovedGrabDamageAttackForGrappleCheck();
        if (holdAttack != null)
        {
            return new GrappleDamageModeRule
            {
                HoldAttack = holdAttack,
                DefaultMode = AttackDamageMode.Lethal,
                LethalPenalty = 0,
                NonlethalPenalty = -4,
                Reason = $"Improved Grab: the {holdAttack.Name} that established the hold, MM p.310"
            };
        }

        string reason = Stats != null && Stats.IsMonk
            ? "Monk exception"
            : FeatManager.HasImprovedUnarmedStrike(Stats)
                ? "Improved Unarmed Strike feat"
                : HasGauntletEquipped()
                    ? "gauntlet: a lethal unarmed strike, PHB p.118"
                    : null;
        if (reason != null)
        {
            return new GrappleDamageModeRule
            {
                DefaultMode = AttackDamageMode.Lethal,
                LethalPenalty = 0,
                NonlethalPenalty = 0,
                Reason = reason
            };
        }

        return new GrappleDamageModeRule
        {
            DefaultMode = AttackDamageMode.Nonlethal,
            LethalPenalty = -4,
            NonlethalPenalty = 0,
            Reason = null
        };
    }

    /// <summary>
    /// A creature held by an Improved Grab hold with one body part cannot pin or drag its holder, which is not grappled
    /// (MM p.310; PHB p.156-157: a pin or a grapple Move would make the holder grappled). It keeps its other grapple
    /// actions against the holder (escape, attack, Damage, Escape Artist, casting, drawing a light weapon, using the
    /// holder's weapon) (delegated RAW reading 2026-10-14, CMB-163). Shared by the resolver, the PC menu and the AI.
    /// </summary>
    public bool IsGrappleActionBlockedByImprovedGrabHold(GrappleActionType actionType, out string reason)
    {
        reason = string.Empty;
        if (actionType != GrappleActionType.PinOpponent && actionType != GrappleActionType.MoveHalfSpeed)
            return false;

        if (!IsHeldByImprovedGrabHold(out CharacterController holder))
            return false;

        string holderName = holder.Stats != null ? holder.Stats.CharacterName : holder.name;
        reason = $"{holderName} holds {Stats.CharacterName} with one body part and is not grappled itself (MM p.310), so it cannot be pinned or moved by the grapple.";
        return true;
    }

    /// <summary>-20 while this creature holds with one body part (MM p.310), else 0. Read by every grapple check formula.</summary>
    public int GetImprovedGrabHoldGrappleCheckPenalty()
        => IsHoldingWithImprovedGrab() ? ImprovedGrabHoldGrappleCheckPenalty : 0;

    /// <summary>
    /// Lets go of an Improved Grab hold with one body part, which ends the grapple for the held creature too; neither
    /// creature moves. A free action on the holder's own turn (delegated RAW reading 2026-10-14, CMB-163: MM p.310 is
    /// silent; PHB p.157 lets a pinner release as a free action, PHB p.144 puts free actions on your turn). The hold
    /// also ends when the held creature escapes or when the holder moves away and cannot drag it
    /// (<see cref="CheckImprovedGrabHoldReach"/>). When the AI lets go is an AI design item (AI-064). Returns the log
    /// line, or empty when this creature holds nobody.
    /// </summary>
    public string ReleaseImprovedGrabHold(string reason = null)
    {
        if (!TryGetImprovedGrabHold(out CharacterController held, out NaturalAttackDefinition bodyPart))
            return string.Empty;

        string heldName = held.Stats != null ? held.Stats.CharacterName : held.name;
        string partName = bodyPart != null && !string.IsNullOrWhiteSpace(bodyPart.Name) ? bodyPart.Name.Trim().ToLowerInvariant() : "body part";
        ReleaseGrappleState(string.IsNullOrEmpty(reason) ? "Improved Grab hold released" : reason);
        return $"{Stats.CharacterName} lets go of {heldName} ({partName} hold){(string.IsNullOrEmpty(reason) ? string.Empty : ": " + reason)}. {heldName} is no longer grappled.";
    }

    /// <summary>
    /// The upper end of the typical weight of a creature of <paramref name="size"/>, in pounds (MM p.314 size table; a
    /// Colossal creature's open range uses its lower end). RAW the carry test uses the held creature's own weight (its
    /// MM entry, or PHB p.109 Table 6-6 for a PC) plus its gear; the game stores no creature weights, so the Improved
    /// Grab carry test reads this stand-in, plus the creature's carried gear: a tracked data gap, not a ruling (CMB-163).
    /// </summary>
    public static float GetTypicalCreatureWeightUpperBoundLbs(SizeCategory size)
    {
        switch (size)
        {
            case SizeCategory.Fine: return 0.125f;
            case SizeCategory.Diminutive: return 1f;
            case SizeCategory.Tiny: return 8f;
            case SizeCategory.Small: return 60f;
            case SizeCategory.Medium: return 500f;
            case SizeCategory.Large: return 4000f;
            case SizeCategory.Huge: return 32000f;
            case SizeCategory.Gargantuan: return 250000f;
            default: return 250000f;
        }
    }

    /// <summary>
    /// The carrying capacity multiplier of a bipedal creature of <paramref name="size"/> (PHB p.162: Table 9-1 is for
    /// Medium bipeds; Small x3/4 ... Colossal x16).
    /// </summary>
    public static float GetBipedCarryingCapacityMultiplier(SizeCategory size)
    {
        switch (size)
        {
            case SizeCategory.Fine: return 0.125f;
            case SizeCategory.Diminutive: return 0.25f;
            case SizeCategory.Tiny: return 0.5f;
            case SizeCategory.Small: return 0.75f;
            case SizeCategory.Large: return 2f;
            case SizeCategory.Huge: return 4f;
            case SizeCategory.Gargantuan: return 8f;
            case SizeCategory.Colossal: return 16f;
            default: return 1f;
        }
    }

    /// <summary>
    /// The carrying capacity multiplier of a quadruped of <paramref name="size"/> (PHB p.162: Fine x1/4, Diminutive x1/2,
    /// Tiny x3/4, Small x1, Medium x1-1/2, Large x3, Huge x6, Gargantuan x12, Colossal x24).
    /// </summary>
    public static float GetQuadrupedCarryingCapacityMultiplier(SizeCategory size)
    {
        switch (size)
        {
            case SizeCategory.Fine: return 0.25f;
            case SizeCategory.Diminutive: return 0.5f;
            case SizeCategory.Tiny: return 0.75f;
            case SizeCategory.Small: return 1f;
            case SizeCategory.Large: return 3f;
            case SizeCategory.Huge: return 6f;
            case SizeCategory.Gargantuan: return 12f;
            case SizeCategory.Colossal: return 24f;
            default: return 1.5f;
        }
    }

    /// <summary>
    /// The PHB p.162 carrying capacity multiplier for <paramref name="size"/> and the creature's body plan: the quadruped
    /// multipliers for a four-legged creature (<see cref="CharacterStats.IsQuadruped"/>), the biped ones for every other
    /// creature (delegated RAW reading 2026-10-14, CMB-163).
    /// </summary>
    public static float GetCarryingCapacityMultiplier(SizeCategory size, bool isQuadruped)
        => isQuadruped ? GetQuadrupedCarryingCapacityMultiplier(size) : GetBipedCarryingCapacityMultiplier(size);

    /// <summary>
    /// MM p.310: a creature holding with one body part "can even move (possibly carrying away the opponent), provided it
    /// can drag the opponent's weight". PHB p.162: a creature can drag five times its maximum load. True when this
    /// creature can drag <paramref name="held"/>: 5 x its heavy load (Strength, with the size multiplier of its body
    /// plan, <see cref="GetCarryingCapacityMultiplier"/>) against the held creature's weight plus its carried gear
    /// (delegated RAW reading 2026-10-14, CMB-163). The weight is the size-table stand-in (data gap, CMB-163); an MM entry's
    /// own Carrying Capacity line and the ground modifiers (PHB p.162: doubled or halved) are not modelled, and no
    /// Improved Grab creature in the database has such a line.
    /// </summary>
    public bool CanDragImprovedGrabHeldCreature(CharacterController held, out string reason)
    {
        reason = string.Empty;
        if (held == null || held.Stats == null || Stats == null)
            return false;

        float dragLimit = 5f * CharacterStats.GetHeavyLoadForStrength(Stats.EffectiveStrengthScore)
            * GetCarryingCapacityMultiplier(Stats.CurrentSizeCategory, Stats.IsQuadruped);
        float weight = GetTypicalCreatureWeightUpperBoundLbs(held.Stats.CurrentSizeCategory) + Mathf.Max(0f, held.Stats.TotalCarriedWeightLbs);
        if (weight <= dragLimit)
            return true;

        reason = $"{Stats.CharacterName} cannot drag {held.Stats.CharacterName}'s weight ({weight:0} lb. against a drag limit of {dragLimit:0} lb.; MM p.310, PHB p.162)";
        return false;
    }

    /// <summary>
    /// Keeps an Improved Grab hold with one body part together when one of the two moves, called for every movement
    /// (GameManager.NotifyCharacterMovement) with the mover's step. When the holder moves out of its natural reach of
    /// the held creature, it carries the creature along (MM p.310: it can move carrying the opponent if it can drag the
    /// opponent's weight, <see cref="CanDragImprovedGrabHeldCreature"/>): the held creature moves by the same step, or
    /// into the holder's space, without provoking: a creature moved by another creature's action provokes from no one
    /// (delegated RAW reading 2026-10-14, CMB-152, CMB-163; RULES_COVERAGE 4.1; the holder's own move provokes as normal).
    /// The hold ends when the holder cannot drag the weight, when
    /// there is no room, or when the held creature is moved out of reach by something else. Returns the log line of an
    /// ended hold, or empty.
    /// </summary>
    public static string CheckImprovedGrabHoldReach(CharacterController mover, Vector2Int previousPosition, Vector2Int currentPosition)
    {
        if (mover == null)
            return string.Empty;

        CharacterController holder = null;
        if (mover.IsHoldingWithImprovedGrab())
            holder = mover;
        else if (mover.IsHeldByImprovedGrabHold(out CharacterController h))
            holder = h;

        if (holder == null || !holder.TryGetImprovedGrabHold(out CharacterController held, out _))
            return string.Empty;

        if (IsWithinImprovedGrabHoldReach(holder, held))
            return string.Empty;

        if (mover != holder)
            return holder.ReleaseImprovedGrabHold("the held creature was moved out of reach");

        if (!holder.CanDragImprovedGrabHeldCreature(held, out string dragReason))
            return holder.ReleaseImprovedGrabHold(dragReason);

        Vector2Int heldFrom = held.GridPosition;
        Vector2Int step = currentPosition - previousPosition;
        if ((held.TrySetGrapplePosition(heldFrom + step, holder) && IsWithinImprovedGrabHoldReach(holder, held))
            || (held.TrySetGrapplePosition(holder.FindBestGrapplePosition(held, holder), holder) && IsWithinImprovedGrabHoldReach(holder, held)))
        {
            Debug.Log($"[Grapple] {holder.Stats.CharacterName} carries {held.Stats.CharacterName} from ({heldFrom.x},{heldFrom.y}) to ({held.GridPosition.x},{held.GridPosition.y}) (Improved Grab hold, MM p.310).");
            return string.Empty;
        }

        return holder.ReleaseImprovedGrabHold("no room to carry the held creature along");
    }

    private static bool IsWithinImprovedGrabHoldReach(CharacterController holder, CharacterController held)
    {
        int reach = holder.Stats != null ? Mathf.Max(1, holder.Stats.NaturalReachSquares) : 1;
        return holder.GetMinimumDistanceToTarget(held, chebyshev: true) <= reach;
    }

    public bool IsPinned()
    {
        return HasCondition(CombatConditionType.Pinned) || _pinnedBy != null;
    }

    public CharacterController GetPinnedBy()
    {
        if (_pinnedBy == null)
            return null;

        if (_pinnedBy.Stats == null || _pinnedBy.Stats.IsDead)
        {
            _pinnedBy = null;
            return null;
        }

        return _pinnedBy;
    }

    public void SetPinnedBy(CharacterController pinner)
    {
        _pinnedBy = pinner;
    }

    public void ClearPinnedBy()
    {
        _pinnedBy = null;
    }

    public bool IsPinningOpponent()
    {
        if (!_isPinningOpponent || _pinnedOpponent == null)
            return false;

        if (_pinnedOpponent.Stats == null || _pinnedOpponent.Stats.IsDead)
        {
            ReleasePinningState();
            return false;
        }

        if (!TryGetGrappleLink(this, out GrappleLink link) || link == null)
        {
            ReleasePinningState();
            return false;
        }

        if (link.PinMaintainer != this || link.PinnedCharacter != _pinnedOpponent)
        {
            ReleasePinningState();
            return false;
        }

        return true;
    }

    public CharacterController GetPinnedOpponent()
    {
        return IsPinningOpponent() ? _pinnedOpponent : null;
    }

    public void SetPinningOpponent(CharacterController opponent)
    {
        _isPinningOpponent = opponent != null;
        _pinnedOpponent = opponent;
    }

    public void ReleasePinningState()
    {
        _isPinningOpponent = false;
        _pinnedOpponent = null;
    }

    /// <summary>
    /// PHB p.156 (owner decision 2026-10-07, CMB-120): a pin holds the opponent for 1 round, so its round is up
    /// when the pinner's next turn starts. The pin is still in place on that turn only so the pinner can pin
    /// again: Pin Opponent then renews it for another round (failure ends the pin, not the grapple), Release
    /// Pinned Opponent still releases it, any other grapple action lets it lapse first
    /// (<see cref="ResolveGrappleAction"/>), and a pin still due at the end of that turn ends
    /// (<see cref="ProcessPinnedDurationAtTurnEnd"/>). Shared by the PC grapple menu, the action buttons and the AI.
    /// </summary>
    public bool IsPinRenewalDue()
    {
        if (!IsPinningOpponent())
            return false;

        if (!TryGetGrappleLink(this, out GrappleLink link) || link == null)
            return false;

        return link.PinRoundEndsAtMaintainerTurnStart > 0 && _turnsStartedCount >= link.PinRoundEndsAtMaintainerTurnStart;
    }

    /// <summary>
    /// True while this character holds a pin whose round is not yet up: the pinner restrictions of PHB p.156
    /// apply (only damage, use the opponent's weapon, move, disarm a small object or release the pin).
    /// False on the pinner's renewal turn (<see cref="IsPinRenewalDue"/>), when the pin is about to end.
    /// </summary>
    public bool IsHoldingPinThisRound()
    {
        return IsPinningOpponent() && !IsPinRenewalDue();
    }

    public bool IsGrappleActionBlockedWhilePinning(GrappleActionType actionType, out string blockedReason)
    {
        blockedReason = string.Empty;

        // On the renewal turn the pinner may pin again or do anything else (the pin then lapses first; CMB-120).
        if (!IsHoldingPinThisRound())
            return false;

        switch (actionType)
        {
            case GrappleActionType.DamageOpponent:
            case GrappleActionType.UseOpponentWeapon:
            case GrappleActionType.MoveHalfSpeed:
            case GrappleActionType.DisarmSmallObject:
            case GrappleActionType.ReleasePinnedOpponent:
                return false;

            default:
                blockedReason = "While holding a pin, only damage, use opponent weapon, move, disarm small object, or release pin are allowed.";
                return true;
        }
    }

    public bool TryGetActiveGrappleOpponents(out List<CharacterController> opponents)
    {
        opponents = new List<CharacterController>();

        if (!TryGetGrappleLink(this, out GrappleLink link))
            return false;

        CharacterController controller = link.Controller;
        CharacterController defender = link.Defender;

        if (controller != null
            && controller != this
            && controller.Stats != null
            && !controller.Stats.IsDead)
        {
            opponents.Add(controller);
        }

        if (defender != null
            && defender != this
            && defender.Stats != null
            && !defender.Stats.IsDead
            && !opponents.Contains(defender))
        {
            opponents.Add(defender);
        }

        return opponents.Count > 0;
    }

    public int GetGrappleSizeModifier()
    {
        return EnsureCombatStats().GetGrappleSizeModifier();
    }

    private enum GrappleCheckContext
    {
        Standard,
        ResistGrapple,
        EscapeGrapple,
        /// <summary>A pinned creature's opposed check to escape the pin (PHB p.156-157; Grease's +10 to escape a pin, PHB p.237).</summary>
        EscapePin,
        ResistPin
    }

    private int GetGreasedArmorGrappleBonus(GrappleCheckContext context)
    {
        StatusEffectManager statusEffectManager = StatusEffectManager;
        if (statusEffectManager == null || statusEffectManager.ActiveEffects == null || statusEffectManager.ActiveEffects.Count == 0)
            return 0;

        int bestBonus = 0;
        for (int i = 0; i < statusEffectManager.ActiveEffects.Count; i++)
        {
            ActiveSpellEffect effect = statusEffectManager.ActiveEffects[i];
            if (effect == null)
                continue;

            int candidate = 0;
            switch (context)
            {
                case GrappleCheckContext.ResistGrapple:
                    candidate = effect.GreasedArmorGrappleResistBonus;
                    break;
                case GrappleCheckContext.EscapeGrapple:
                    candidate = effect.GreasedArmorGrappleEscapeBonus;
                    break;
                case GrappleCheckContext.EscapePin:
                    candidate = effect.GreasedArmorBreakPinBonus;
                    break;
                case GrappleCheckContext.ResistPin:
                    candidate = effect.GreasedArmorResistPinBonus;
                    break;
            }

            if (candidate > bestBonus)
                bestBonus = candidate;
        }

        return bestBonus;
    }

    /// <summary>
    /// Evaluate concealment miss chance for this target against a specific attacker.
    /// Handles dynamic effects (e.g., Obscuring Mist distance bands).
    /// </summary>
    private int EvaluateEffectMissChanceAgainstAttacker(ActiveSpellEffect effect, CharacterController attacker, bool incomingIsRangedAttack)
    {
        if (effect == null)
            return 0;

        // ── Blink: ethereal concealment, NOT visual ──
        // Blinded attackers still face Blink miss chance (it's planar, not sight-based).
        // Blind-Fight feat does not help against Blink either.
        // Dynamic miss chance: 50% base, reduced to 20% if attacker can see invisible
        // OR has ghost touch weapon, reduced to 0% if attacker has BOTH.
        if (IsBlinkConcealmentEffect(effect))
        {
            if (attacker != null)
                return GetBlinkMissChanceAgainst(attacker);
            return Mathf.Clamp(effect.MissChance, 0, 100); // fallback to base 50%
        }

        // Opponents who cannot see the subject ignore visual concealment effects such as Blur/Invisibility.
        // They still suffer their own blinded miss chance elsewhere in attack resolution.
        if (attacker != null && attacker.HasCondition(CombatConditionType.Blinded))
            return 0;

        if (effect.MissChanceAgainstRangedOnly && !incomingIsRangedAttack)
            return 0;

        if (effect.MissChanceAgainstMeleeOnly && incomingIsRangedAttack)
            return 0;

        if (attacker != null)
        {
            if (effect.SourceAreaEffect is ObscuringMistAreaEffect mist)
                return Mathf.Clamp(mist.GetConcealmentMissChance(attacker, this), 0, 100);

            if (effect.SourceAreaEffect is FogCloudAreaEffect fog)
                return Mathf.Clamp(fog.GetConcealmentMissChance(attacker, this), 0, 100);

            if (effect.SourceAreaEffect is DarknessAreaEffect darkness)
                return Mathf.Clamp(darkness.GetConcealmentMissChance(attacker, this), 0, 100);
        }

        return Mathf.Clamp(effect.MissChance, 0, 100);
    }

        /// <summary>
    /// Returns true if this ActiveSpellEffect is from any variant of invisibility.
    /// Used to determine if See Invisibility / Glitterdust can negate the concealment.
    /// Supports standard Invisibility, Greater Invisibility, and future variants.
    /// </summary>
    private static bool IsInvisibilityConcealmentEffect(ActiveSpellEffect effect)
    {
        if (effect == null || effect.Spell == null)
            return false;

        string spellId = effect.Spell.SpellId;
        return string.Equals(spellId, SpellNames.INVISIBILITY, StringComparison.Ordinal)
               || string.Equals(spellId, "greater_invisibility", StringComparison.Ordinal)
               || string.Equals(spellId, "improved_invisibility", StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns true if this ActiveSpellEffect is from the Blink spell.
    /// Blink concealment is ethereal, not visual — it is NOT negated by blinded attackers
    /// and its miss chance is dynamic based on attacker capabilities.
    /// </summary>
    private static bool IsBlinkConcealmentEffect(ActiveSpellEffect effect)
    {
        if (effect == null || effect.Spell == null)
            return false;
        return string.Equals(effect.Spell.SpellId, SpellNames.BLINK, StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns the highest concealment miss chance currently protecting this character.
    /// D&D 3.5e concealment miss chances do not stack; use the highest applicable source.
    /// </summary>
    public int GetMissChance(bool incomingIsRangedAttack = false)
    {
        return GetMissChance(null, incomingIsRangedAttack);
    }

    public int GetMissChance(CharacterController attacker, bool incomingIsRangedAttack = false)
    {
        int bestMissChance = 0;

        StatusEffectManager statusEffectManager = StatusEffectManager;
        if (statusEffectManager != null && statusEffectManager.ActiveEffects != null)
        {
            for (int i = 0; i < statusEffectManager.ActiveEffects.Count; i++)
            {
                ActiveSpellEffect effect = statusEffectManager.ActiveEffects[i];
                if (effect == null || effect.MissChance <= 0)
                    continue;

                if (IsInvisibilityConcealmentEffect(effect)
                    && ((attacker != null && attacker.CanSeeInvisible(this)) || IsOutlinedByGlitterdust))
                {
                    // See Invisible and Glitterdust both ignore concealment granted by Invisibility only.
                    // Other concealment sources (fog, darkness, blur, etc.) still apply normally.
                    continue;
                }

                int normalized = EvaluateEffectMissChanceAgainstAttacker(effect, attacker, incomingIsRangedAttack);
                if (normalized > bestMissChance)
                    bestMissChance = normalized;
            }
        }

        // D&D 3.5e Entropic Shield: 20% miss chance against ranged attacks only (PHB p.227)
        if (incomingIsRangedAttack && Stats != null && Stats.EntropicShieldActive)
            bestMissChance = Mathf.Max(bestMissChance, 20);

        // Darkness does not block LOS, but attacks involving darkness squares still have 20% miss chance.
        if (attacker != null)
            bestMissChance = Mathf.Max(bestMissChance, DarknessAreaEffect.GetAttackConcealmentMissChance(attacker, this));

        // Wall of Fire is opaque (PHB p.298) — attacks through active wall cells have 20% miss chance.
        if (attacker != null)
            bestMissChance = Mathf.Max(bestMissChance, WallOfFireAreaEffect.GetAttackConcealmentMissChance(attacker, this));

        // Incorporeal creatures: 50% miss chance from non-magical, non-ghost-touch, non-force attacks
        if (_isIncorporeal)
        {
            // TODO: Skip miss chance if attacker uses ghost touch weapon or force effects
            bestMissChance = Mathf.Max(bestMissChance, 50);
        }

        // Cloak of Displacement: wondrous item-based miss chance (DMG p.253).
        // Minor = 20%, Major = 50%. Does not stack with concealment; uses highest.
        if (Stats != null && Stats.DisplacementMissChance > 0)
            bestMissChance = Mathf.Max(bestMissChance, Stats.DisplacementMissChance);

        return bestMissChance;
    }

    /// <summary>
    /// True when this character currently has total concealment against the incoming attack type.
    /// </summary>
    public bool HasTotalConcealment(bool incomingIsRangedAttack = false)
    {
        return HasTotalConcealment(null, incomingIsRangedAttack);
    }

    public bool HasTotalConcealment(CharacterController attacker, bool incomingIsRangedAttack = false)
    {
        return GetMissChance(attacker, incomingIsRangedAttack) >= 50;
    }

    public bool HasConcealment(bool incomingIsRangedAttack = false)
    {
        return HasConcealment(null, incomingIsRangedAttack);
    }

    public bool HasConcealment(CharacterController attacker, bool incomingIsRangedAttack = false)
    {
        return GetMissChance(attacker, incomingIsRangedAttack) > 0;
    }

    public bool CanSee(CharacterController target, bool incomingIsRangedAttack = false)
    {
        if (target == null || target.Stats == null || target.Stats.IsDead)
            return false;

        if (!target.HasTotalConcealment(this, incomingIsRangedAttack))
            return true;

        return false;
    }

    /// <summary>
    /// Whether this creature can see <paramref name="target"/> in the sense of rules worded "an opponent you can
    /// see", such as Withdraw's start square (PHB p.143). Unlike <see cref="CanSee"/>, which tests the 50% miss
    /// chance (CMB-078), this is a sight test only: false when this creature is Blinded (DMG p.300) or the target
    /// has total visual concealment from it (invisibility it cannot pierce with See Invisibility or a Glitterdust
    /// outline, or a fog or mist that hides it, PHB p.152); true against an incorporeal creature, Displacement
    /// (PHB p.223: not real total concealment), Blink (ethereal, not visual) and a cloak of displacement, whose
    /// miss chances do not hide the creature from view. Actor-agnostic: PCs and NPCs use it alike.
    /// </summary>
    public bool CanVisuallyPerceive(CharacterController target)
    {
        if (target == null || target.Stats == null || target.Stats.IsDead)
            return false;
        if (target == this)
            return true;
        if (HasCondition(CombatConditionType.Blinded))
            return false;

        bool invisibilityPierced = CanSeeInvisible(target) || target.IsOutlinedByGlitterdust;
        if (target.HasActiveInvisibilityEffect && !invisibilityPierced)
            return false;

        StatusEffectManager statusEffectManager = target.StatusEffectManager;
        if (statusEffectManager == null || statusEffectManager.ActiveEffects == null)
            return true;

        for (int i = 0; i < statusEffectManager.ActiveEffects.Count; i++)
        {
            ActiveSpellEffect effect = statusEffectManager.ActiveEffects[i];
            if (effect == null || effect.MissChance <= 0)
                continue;

            // Miss chances that do not hide the creature from view.
            if (IsBlinkConcealmentEffect(effect) || effect.MissChanceAgainstRangedOnly)
                continue;
            if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.DISPLACEMENT, StringComparison.Ordinal))
                continue;

            bool isInvisibility = IsInvisibilityConcealmentEffect(effect)
                || (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.INVISIBILITY_SPHERE, StringComparison.Ordinal));
            if (isInvisibility)
            {
                if (!invisibilityPierced)
                    return false;
                continue;
            }

            if (effect.IsTotalConcealment)
                return false;

            // Fog, mist and darkness: total concealment (50%) from this observer hides the target (PHB p.152).
            if (target.EvaluateEffectMissChanceAgainstAttacker(effect, this, incomingIsRangedAttack: false) >= 50)
                return false;
        }

        return true;
    }

    public void UpdateLastKnownPosition(CharacterController target, bool incomingIsRangedAttack = false)
    {
        if (target == null || target.Stats == null || target.Stats.IsDead)
            return;

        if (CanSee(target, incomingIsRangedAttack))
            _lastKnownTargetPositions[target] = target.GridPosition;
    }

    public Vector2Int? GetLastKnownPosition(CharacterController target)
    {
        if (target == null)
            return null;

        if (_lastKnownTargetPositions.TryGetValue(target, out Vector2Int pos))
            return pos;

        return null;
    }

    public void ClearLastKnownPosition(CharacterController target)
    {
        if (target == null)
            return;

        _lastKnownTargetPositions.Remove(target);
    }

    /// <summary>
    /// Human-readable concealment summary for combat log output.
    /// </summary>
    public string GetConcealmentDescription(bool incomingIsRangedAttack = false)
    {
        return GetConcealmentDescription(null, incomingIsRangedAttack);
    }

    public string GetConcealmentDescription(CharacterController attacker, bool incomingIsRangedAttack = false)
    {
        int missChance = GetMissChance(attacker, incomingIsRangedAttack);
        if (missChance <= 0)
            return "No concealment";

        int darknessMissChance = attacker != null ? DarknessAreaEffect.GetAttackConcealmentMissChance(attacker, this) : 0;
        if (attacker != null && darknessMissChance > 0 && darknessMissChance >= missChance)
        {
            bool attackerInDarkness = DarknessAreaEffect.IsPositionInMagicalDarkness(attacker.GridPosition);
            bool targetInDarkness = DarknessAreaEffect.IsPositionInMagicalDarkness(GridPosition);
            bool pathCrossesDarkness = DarknessAreaEffect.IsLineBlockedByMagicalDarkness(attacker.GridPosition, GridPosition, includeEndpoints: false);

            var reasons = new List<string>();
            if (targetInDarkness)
                reasons.Add("Target is in magical darkness (20% miss chance)");
            if (pathCrossesDarkness)
                reasons.Add("Attack crosses darkness area (20% miss chance)");
            if (attackerInDarkness)
                reasons.Add("Attacker is in magical darkness (20% miss chance)");

            if (reasons.Count > 0)
                return string.Join("; ", reasons);
        }

        StatusEffectManager statusEffectManager = StatusEffectManager;
        ActiveSpellEffect sourceEffect = null;
        if (statusEffectManager != null && statusEffectManager.ActiveEffects != null)
        {
            for (int i = 0; i < statusEffectManager.ActiveEffects.Count; i++)
            {
                ActiveSpellEffect effect = statusEffectManager.ActiveEffects[i];
                if (effect == null || effect.MissChance <= 0)
                    continue;

                if (IsInvisibilityConcealmentEffect(effect)
                    && ((attacker != null && attacker.CanSeeInvisible(this)) || IsOutlinedByGlitterdust))
                {
                    continue;
                }

                int normalized = EvaluateEffectMissChanceAgainstAttacker(effect, attacker, incomingIsRangedAttack);
                if (normalized == missChance)
                {
                    sourceEffect = effect;
                    break;
                }
            }
        }

        // D&D 3.5e Entropic Shield: identify as concealment source for ranged attacks
        if (incomingIsRangedAttack && Stats != null && Stats.EntropicShieldActive && sourceEffect == null && missChance == 20)
            return "Entropic Shield (20% miss chance vs ranged attacks)";

        // Cloak of Displacement: identify as miss chance source (DMG p.253)
        if (Stats != null && Stats.DisplacementMissChance > 0 && sourceEffect == null && missChance == Stats.DisplacementMissChance)
        {
            string tier = Stats.DisplacementMissChance >= 50 ? "Major" : "Minor";
            return $"Cloak of Displacement, {tier} ({Stats.DisplacementMissChance}% miss chance)";
        }

        string sourceName = sourceEffect != null && !string.IsNullOrWhiteSpace(sourceEffect.ConcealmentSource)
            ? sourceEffect.ConcealmentSource
            : "Unknown source";

        if (sourceEffect != null && attacker != null &&
            (sourceEffect.SourceAreaEffect is ObscuringMistAreaEffect || sourceEffect.SourceAreaEffect is FogCloudAreaEffect))
        {
            int distance = attacker.GetMinimumDistanceToTarget(this, chebyshev: true);
            string distanceLabel = distance <= 1 ? "within 5 ft" : "beyond 5 ft";
            bool dynamicTotal = missChance >= 50;
            return dynamicTotal
                ? $"Total concealment ({missChance}% miss chance) from {sourceName} ({distanceLabel})"
                : $"Concealment ({missChance}% miss chance) from {sourceName} ({distanceLabel})";
        }

        bool isTotal = sourceEffect != null ? (sourceEffect.IsTotalConcealment || missChance >= 50) : missChance >= 50;
        return isTotal
            ? $"Total concealment ({missChance}% miss chance) from {sourceName}"
            : $"Concealment ({missChance}% miss chance) from {sourceName}";
    }

    private GrappleCheckResult RollGrappleCheck(
        int? baseAttackBonusOverride = null,
        int additionalModifier = 0,
        string additionalModifierLabel = null,
        GrappleCheckContext context = GrappleCheckContext.Standard)
    {
        var result = new GrappleCheckResult
        {
            CharacterName = Stats != null ? Stats.CharacterName : name,
            BaseRoll = DiceService.D20("Grapple check"),
            BaseAttackBonus = baseAttackBonusOverride ?? (Stats != null ? Stats.BaseAttackBonus : 0),
            StrengthModifier = Stats != null ? Stats.STRMod : 0,
            SizeModifier = GetGrappleSizeModifier()
        };

        // A grapple check is BAB + Str + the special size modifier + effects that name grapple checks (PHB p.155-156;
        // owner interpretation 2026-10-11, CMB-130). It is not an attack roll, so condition attack modifiers (Grappled, Pinned,
        // Shaken, Prone...) and Haste's attack bonus do not apply.
        if (Stats != null && Stats.HasFeat("Improved Grapple"))
            result.AddMiscModifier(4, "Improved Grapple feat");

        int holdPenalty = GetImprovedGrabHoldGrappleCheckPenalty();
        if (holdPenalty != 0)
            result.AddMiscModifier(holdPenalty, "Improved Grab hold with one body part (MM p.310)");

        // Nonlethal Improved Grab hold damage on this check (ResolveGrappleAction; delegated RAW reading 2026-10-14).
        if (_improvedGrabCheckModifier != 0)
            result.AddMiscModifier(_improvedGrabCheckModifier, _improvedGrabCheckModifierLabel ?? "Nonlethal Improved Grab damage");

        int greasedArmorBonus = GetGreasedArmorGrappleBonus(context);
        if (greasedArmorBonus > 0)
        {
            string label = "Greased armor";
            switch (context)
            {
                case GrappleCheckContext.ResistGrapple:
                    label = "Greased armor (resist grapple)";
                    break;
                case GrappleCheckContext.EscapeGrapple:
                    label = "Greased armor (escape grapple)";
                    break;
                case GrappleCheckContext.EscapePin:
                    label = "Greased armor (escape pin)";
                    break;
                case GrappleCheckContext.ResistPin:
                    label = "Greased armor (resist pin)";
                    break;
            }

            result.AddMiscModifier(greasedArmorBonus, label);
            Debug.Log($"[GrappleSystem][GreasedArmor] {result.CharacterName} gains {greasedArmorBonus:+#;-#;0} on grapple check ({context}).");
        }

        if (additionalModifier != 0)
            result.AddMiscModifier(additionalModifier, string.IsNullOrEmpty(additionalModifierLabel) ? "Additional modifiers" : additionalModifierLabel);

        result.Total = result.BaseRoll + result.BaseAttackBonus + result.StrengthModifier + result.SizeModifier + result.MiscModifier;
        return result;
    }

    private void AttachAttackBuffDebuffBreakdown(CombatResult result)
    {
        if (result == null || Stats == null)
            return;

        if (result.AttackBuffDebuffModifiers == null)
            result.AttackBuffDebuffModifiers = new List<AttackModifierBreakdownEntry>();
        else
            result.AttackBuffDebuffModifiers.Clear();

        // The effect modifiers that count after stacking (CharacterStats.EffectAttackBonus): each one by its source
        // (Bless, Divine Favor, Inspire Courage, Charge, ...), so two luck or morale bonuses list only the better one.
        List<TypedBonus> effectAttackModifiers = Stats.Bonuses.AppliedModifiers(BonusTarget.AttackRoll);
        for (int i = 0; i < effectAttackModifiers.Count; i++)
        {
            TypedBonus modifier = effectAttackModifiers[i];
            string label = !string.IsNullOrWhiteSpace(modifier.Label) ? modifier.Label : (modifier.Source ?? "Spell effect");
            result.AddAttackBuffDebuffModifier(label, modifier.Value);
        }

        int conditionAttackBonusTotal = 0;
        if (Stats.ActiveConditions != null)
        {
            for (int i = 0; i < Stats.ActiveConditions.Count; i++)
            {
                StatusEffect activeCondition = Stats.ActiveConditions[i];
                ConditionDefinition conditionDefinition = ConditionRules.GetDefinition(activeCondition.Type);
                int attackModifier = conditionDefinition.AttackModifier;
                if (attackModifier == 0)
                    continue;

                string conditionLabel = !string.IsNullOrWhiteSpace(conditionDefinition.DisplayName)
                    ? conditionDefinition.DisplayName
                    : activeCondition.Type.ToString();
                if (!string.IsNullOrWhiteSpace(activeCondition.SourceName)
                    && !string.Equals(activeCondition.SourceName, conditionLabel, StringComparison.OrdinalIgnoreCase))
                {
                    conditionLabel = $"{conditionLabel} ({activeCondition.SourceName})";
                }

                result.AddAttackBuffDebuffModifier(conditionLabel, attackModifier);
                conditionAttackBonusTotal += attackModifier;
            }
        }

        int enfeeblementPenalty = Stats.EnfeeblementStrengthPenalty;
        if (enfeeblementPenalty > 0)
        {
            int strengthWithoutEnfeeblement = Mathf.Max(1, Stats.EffectiveSTRScore - Stats.StrengthConditionPenalty);
            int modWithoutEnfeeblement = CharacterStats.GetModifier(strengthWithoutEnfeeblement);
            int attackModifierDelta = Stats.STRMod - modWithoutEnfeeblement;
            if (attackModifierDelta != 0)
                result.AddAttackBuffDebuffModifier($"Ray of Enfeeblement (Str -{enfeeblementPenalty})", attackModifierDelta);
        }

        int remainingConditionAttackBonus = Stats.ConditionAttackPenalty - conditionAttackBonusTotal;
        if (remainingConditionAttackBonus != 0)
            result.AddAttackBuffDebuffModifier("Other condition modifiers", remainingConditionAttackBonus);
    }

    private static string BuildOpposedResultLine(string actorName, int actorTotal, string opponentName, int opponentTotal, bool actorWins)
    {
        string tie = actorTotal == opponentTotal ? "Tie (higher modifier, then reroll) - " : string.Empty;
        return actorWins
            ? $"Result: {tie}{actorName} wins ({actorTotal} vs {opponentTotal})"
            : $"Result: {tie}{opponentName} wins ({opponentTotal} vs {actorTotal})";
    }

    /// <summary>Opposed grapple check with the PHB p.156 tie rule (higher modifier, then reroll).</summary>
    private static bool DoesAttackerWinGrappleCheck(GrappleCheckResult attackerCheck, GrappleCheckResult defenderCheck)
    {
        if (attackerCheck == null || defenderCheck == null)
            return false;

        return DoesAttackerWinOpposedCheck(
            attackerCheck.Total, attackerCheck.Total - attackerCheck.BaseRoll,
            defenderCheck.Total, defenderCheck.Total - defenderCheck.BaseRoll);
    }

    private static string BuildD20Formula(string label, int dieRoll, int modifier, int total)
    {
        string mod = modifier >= 0 ? $"+{modifier}" : modifier.ToString();
        return $"{label}: 1d20{mod} = {dieRoll}{mod} = {total}";
    }

    private static string BuildDamageFormula(string label, string dice, int diceRoll, int staticModifier, int rawDamage, int finalDamage)
    {
        string staticPart = staticModifier >= 0 ? $"+{staticModifier}" : staticModifier.ToString();
        string finalClause = rawDamage != finalDamage ? $" => {finalDamage} final" : string.Empty;
        return $"{label}: {dice}{staticPart} = {diceRoll}{staticPart} = {rawDamage} raw{finalClause}";
    }

    private bool TryResolveOpposedGrappleCheck(CharacterController opponent, out int myRoll, out int myTotal, out int oppRoll, out int oppTotal, int myCheckModifier = 0, int? myBaseAttackBonusOverride = null)
    {
        myRoll = 0;
        myTotal = 0;
        oppRoll = 0;
        oppTotal = 0;

        if (opponent == null || opponent.Stats == null || Stats == null)
            return false;

        GrappleCheckResult myCheck = RollGrappleCheck(myBaseAttackBonusOverride, myCheckModifier);
        GrappleCheckResult oppCheck = opponent.RollGrappleCheck();

        myRoll = myCheck.BaseRoll;
        myTotal = myCheck.Total;
        oppRoll = oppCheck.BaseRoll;
        oppTotal = oppCheck.Total;
        return true;
    }

    /// <summary>
    /// Resolves one grapple action for a PC or an NPC. On the pinner's renewal turn (<see cref="IsPinRenewalDue"/>)
    /// every action except Pin Opponent (the renewal) and Release Pinned Opponent first lets the pin lapse, because
    /// its 1 round is up (PHB p.156; CMB-120); the lapse is the first line of the result's log. An action that would
    /// resolve to nothing for this pinner (<see cref="IsGrappleActionNoOpForPinner"/>) leaves the pin in place.
    /// For Attack Unarmed by a creature fighting with its natural attacks, <paramref name="naturalAttackIndex"/> and
    /// <paramref name="naturalAttackIsHasteExtra"/> name the one natural attack it makes (<see cref="ResolveGrappleNaturalAttack"/>,
    /// CMB-127); the caller commits that step first. With -1, the natural attack the committed step gave up
    /// (<see cref="AttackPool.LastSubstituteNaturalAttackIndex"/>, when <paramref name="iterativeAttackBonusOverride"/> says a
    /// step was committed), else <see cref="GetDefaultGrappleNaturalAttackOption"/>; with none left the attack is refused.
    /// </summary>
    public SpecialAttackResult ResolveGrappleAction(
        GrappleActionType actionType,
        AttackDamageMode? grappleDamageModeOverride = null,
        EquipSlot? opponentWeaponHandSlotOverride = null,
        int? iterativeAttackBonusOverride = null,
        int naturalAttackIndex = -1,
        bool naturalAttackIsHasteExtra = false)
    {
        string lapseLog = string.Empty;
        if (actionType != GrappleActionType.PinOpponent
            && actionType != GrappleActionType.ReleasePinnedOpponent
            && IsPinRenewalDue()
            && !IsGrappleActionNoOpForPinner(actionType))
        {
            lapseLog = LetDuePinLapse();
        }

        // MM p.310 (CMB-148; owner ruling 2026-10-14): in the rounds after an Improved Grab, the grabber's other won grapple
        // checks on its own turn also deal the establishing attack's damage (the Damage action deals it itself): pin, move,
        // use opponent's weapon, and an escape from a pin (the grapple goes on). Lethal by default; nonlethal at -4 on the
        // check (delegated RAW reading 2026-10-14, PHB p.146 and p.156), chosen with grappleDamageModeOverride.
        NaturalAttackDefinition grabDamageAttack = WouldGrappleActionDealImprovedGrabDamage(actionType) ? GetImprovedGrabDamageAttackForGrappleCheck() : null;
        CharacterController grabDamageTarget = null;
        bool grabDamageNonlethal = grabDamageAttack != null && grappleDamageModeOverride == AttackDamageMode.Nonlethal;
        if (grabDamageAttack != null)
            IsInAnyGrappleLink(out grabDamageTarget);

        SpecialAttackResult result;
        _improvedGrabCheckModifier = grabDamageNonlethal ? -4 : 0;
        _improvedGrabCheckModifierLabel = grabDamageNonlethal ? $"Nonlethal damage with the {grabDamageAttack.Name} (Improved Grab)" : null;
        try
        {
            result = ResolveGrappleActionCore(actionType, grappleDamageModeOverride, opponentWeaponHandSlotOverride, iterativeAttackBonusOverride,
                naturalAttackIndex, naturalAttackIsHasteExtra);
        }
        finally
        {
            _improvedGrabCheckModifier = 0;
            _improvedGrabCheckModifierLabel = null;
        }
        if (grabDamageAttack != null)
            AppendImprovedGrabCheckDamage(result, grabDamageTarget, grabDamageAttack, grabDamageNonlethal);
        if (result != null && !string.IsNullOrEmpty(lapseLog))
            result.Log = string.IsNullOrEmpty(result.Log) ? lapseLog : lapseLog + "\n\n" + result.Log;
        return result;
    }

    /// <summary>
    /// True for a grapple action that resolves to nothing for a pinner (no check, no attack): the Draw a Light Weapon,
    /// Retrieve a Spell Component and Disarm Small Object stubs (CMB-033), and the weapon attacks it cannot make. Such
    /// an action does not cost a due pin (CMB-120).
    /// </summary>
    private bool IsGrappleActionNoOpForPinner(GrappleActionType actionType)
    {
        switch (actionType)
        {
            case GrappleActionType.DrawLightWeapon:
            case GrappleActionType.RetrieveSpellComponent:
            case GrappleActionType.DisarmSmallObject:
                return true;
            case GrappleActionType.AttackWithLightWeapon:
                return !CanAttackWithLightWeaponWhileGrappling(out _, out _);
            case GrappleActionType.AttackUnarmed:
                return !CanAttackUnarmedWhileGrappling(out _);
            case GrappleActionType.Rake:
                return !CanRakeInGrapple(out _);
            case GrappleActionType.UseOpponentWeapon:
            {
                CharacterController opponent = GetPinnedOpponent();
                List<DisarmableHeldItemOption> options = opponent != null ? opponent.GetEquippedLightHandWeaponOptions() : null;
                return options == null || options.Count == 0;
            }
            default:
                return false;
        }
    }

    /// <summary>
    /// Lets this character's pin lapse when its 1 round is up (<see cref="IsPinRenewalDue"/>; PHB p.156, CMB-120)
    /// and returns the log line, or an empty string when no pin was due. Called by every action the pinner takes
    /// on its renewal turn other than pinning again or releasing: grapple actions (<see cref="ResolveGrappleAction"/>)
    /// and spellcasting (GameManager.RollGrappledOrPinnedCastingConcentration, shared by the PC and NPC cast paths).
    /// </summary>
    public string LetDuePinLapseIfDue()
    {
        return IsPinRenewalDue() ? LetDuePinLapse() : string.Empty;
    }

    /// <summary>
    /// Ends the pin on this pinned character and keeps the grapple going: escaping a pin leaves the escaper grappled
    /// (PHB p.156-157). Used when the pinner lets the escape happen without opposing it. False when this character is
    /// not the pinned creature of a grapple link (nothing changes then).
    /// </summary>
    public bool EscapePinKeepingGrapple()
    {
        if (!IsPinned() || !TryGetGrappleLink(this, out GrappleLink link) || link == null || link.PinnedCharacter != this)
            return false;

        ClearPinnedState(link);
        return true;
    }

    /// <summary>Ends a pin whose round is up (CMB-120) and returns the log line; empty when nothing was pinned.</summary>
    private string LetDuePinLapse()
    {
        if (!TryGetGrappleLink(this, out GrappleLink link) || link == null || link.PinnedCharacter == null)
            return string.Empty;

        CharacterController pinned = link.PinnedCharacter;
        ClearPinnedState(link);
        string pinnedName = pinned != null && pinned.Stats != null ? pinned.Stats.CharacterName : "the opponent";
        return $"{Stats.CharacterName} does not pin {pinnedName} again: the pin ends after its 1 round (PHB p.156). The grapple continues.";
    }

    /// <summary>The damage a successful grapple check dealt (<see cref="RollAndApplyGrappleCheckDamage"/>).</summary>
    private struct GrappleCheckDamageRoll
    {
        public int DiceCount;
        public int DiceSides;
        public int Raw;
        public int Final;
        public List<int> Rolls;
        public WeaponDamageBreakdown Bonus;
    }

    /// <summary>
    /// Rolls and applies the damage of a won grapple check to <paramref name="opponent"/>: the natural attack
    /// <paramref name="naturalAttack"/> (an Improved Grab's establishing attack, MM p.310, CMB-148: its scaled dice and
    /// its own Strength share) or, when null, an unarmed strike (PHB p.156), with the shared weapon damage terms
    /// (CMB-003; no attack roll, so no Power Attack), through the damage pipeline (mitigation, death). The one damage
    /// roll of the Damage grapple action and of the Improved Grab damage on the grabber's other won checks.
    /// </summary>
    private GrappleCheckDamageRoll RollAndApplyGrappleCheckDamage(CharacterController opponent, NaturalAttackDefinition naturalAttack, bool nonlethal)
    {
        var roll = new GrappleCheckDamageRoll { Rolls = new List<int>() };
        if (opponent == null || opponent.Stats == null || Stats == null)
            return roll;

        DamageBypassTag damageTags;
        string damageSourceName;
        if (naturalAttack != null)
        {
            Stats.GetScaledNaturalAttackDamage(naturalAttack, out roll.DiceCount, out roll.DiceSides);
            roll.DiceCount = Mathf.Max(1, roll.DiceCount);
            roll.DiceSides = Mathf.Max(1, roll.DiceSides);
            roll.Bonus = BuildWeaponDamageBonus(null, isRanged: false, isOffHand: false,
                default(AttackCalculator.FeatModifiers), naturalAttack: naturalAttack);
            damageTags = naturalAttack.GetPhysicalDamageTypes();
            if (damageTags == DamageBypassTag.None)
                damageTags = DamageBypassTag.Bludgeoning;
            damageSourceName = $"Grapple damage ({naturalAttack.Name}, Improved Grab)";
        }
        else
        {
            var unarmed = GetUnarmedDamage();
            roll.DiceCount = Mathf.Max(1, unarmed.damageCount);
            roll.DiceSides = Mathf.Max(2, unarmed.damageDice);
            // Unarmed strike damage counts as weapon damage for bonuses on weapon damage rolls (PHB p.121), so morale,
            // Sickened and Weapon Specialization in unarmed strike (PHB p.102) apply.
            roll.Bonus = BuildWeaponDamageBonus(null, isRanged: false, isOffHand: false,
                DamageOnlyFeatModifiers(null), unarmed.bonusDamage);
            damageTags = DamageBypassTag.Bludgeoning;
            damageSourceName = "Grapple damage (unarmed strike)";
        }
        // A ranger's favored enemy bonus applies to this weapon damage roll too (PHB p.47; CHR-020).
        roll.Bonus.FavoredEnemyBonus = RangerRules.FavoredEnemyDamageBonus(Stats, opponent.Stats);

        for (int i = 0; i < roll.DiceCount; i++)
        {
            int die = DiceService.RollDie(roll.DiceSides, "Grapple damage");
            roll.Rolls.Add(die);
            roll.Raw += die;
        }

        roll.Raw = Mathf.Max(1, roll.Raw + roll.Bonus.Total);

        var damageTypes = new HashSet<DamageType>();
        if ((damageTags & DamageBypassTag.Bludgeoning) != 0) damageTypes.Add(DamageType.Bludgeoning);
        if ((damageTags & DamageBypassTag.Piercing) != 0) damageTypes.Add(DamageType.Piercing);
        if ((damageTags & DamageBypassTag.Slashing) != 0) damageTypes.Add(DamageType.Slashing);
        if (damageTypes.Count == 0) damageTypes.Add(DamageType.Bludgeoning);

        var packet = new DamagePacket
        {
            RawDamage = roll.Raw,
            Types = damageTypes,
            AttackTags = damageTags,
            IsRanged = false,
            IsNonlethal = nonlethal,
            Source = AttackSource.Weapon,
            SourceName = damageSourceName
        };

        DamageResolutionResult mitigation = opponent.Stats.ApplyIncomingDamage(roll.Raw, packet);
        roll.Final = mitigation.FinalDamage;

        if (opponent.Stats.IsDead)
        {
            opponent.OnDeath();
            ReleaseGrappleState("opponent killed in grapple");
        }

        return roll;
    }

    /// <summary>
    /// The grapple actions whose won check is "a successful grapple check" of the grabber other than the Damage action,
    /// which deals the establishing attack's damage itself (MM p.310, CMB-148): pin, move, use opponent's weapon, and an
    /// opposed escape made while this creature is pinned, which ends only the pin (PHB p.156-157) and is a grapple check
    /// it makes on its own turn. An escape from the grapple itself is left out: it ends the hold (and an Escape Artist
    /// check is no grapple check). Owner ruling 2026-10-14 (RULES_COVERAGE 4.1): only the grapple checks the grabber makes
    /// on its own turn in the rounds after the grab count, not the opposed checks it rolls against the held creature's actions.
    /// </summary>
    private static bool IsImprovedGrabDamagingGrappleCheck(GrappleActionType actionType, bool actorPinned)
        => actionType == GrappleActionType.PinOpponent
            || actionType == GrappleActionType.MoveHalfSpeed
            || actionType == GrappleActionType.UseOpponentWeapon
            || (actionType == GrappleActionType.OpposedGrappleEscape && actorPinned);

    /// <summary>
    /// True when a won <paramref name="actionType"/> check by this creature now would also deal its Improved Grab hold
    /// damage (<see cref="IsImprovedGrabDamagingGrappleCheck"/> and <see cref="GetImprovedGrabDamageAttackForGrappleCheck"/>):
    /// the PC grapple buttons then ask for the damage mode (lethal, or nonlethal at -4 on the check), as the Damage action does.
    /// </summary>
    public bool WouldGrappleActionDealImprovedGrabDamage(GrappleActionType actionType)
        => IsImprovedGrabDamagingGrappleCheck(actionType, IsPinned()) && GetImprovedGrabDamageAttackForGrappleCheck() != null;

    /// <summary>
    /// The extra modifier on this creature's own grapple check while <see cref="ResolveGrappleAction"/> resolves a check
    /// that deals nonlethal Improved Grab hold damage: -4 (delegated RAW reading 2026-10-14: PHB p.146 puts the -4 for
    /// nonlethal damage with a lethal weapon on the roll that deals it, PHB p.156 on the grapple check), else 0.
    /// </summary>
    private int _improvedGrabCheckModifier;
    private string _improvedGrabCheckModifierLabel;

    /// <summary>
    /// MM p.310 (CMB-148): each successful grapple check the grabber makes in the rounds after its Improved Grab
    /// automatically deals the damage of the attack that established the hold, lethal unless the grabber chose nonlethal
    /// (at -4 on the check). Appends that damage to the result of a won pin, move, use-opponent's-weapon or pin-escape
    /// check (the Damage action deals it itself).
    /// </summary>
    private void AppendImprovedGrabCheckDamage(SpecialAttackResult result, CharacterController opponent, NaturalAttackDefinition establishingAttack, bool nonlethal)
    {
        if (result == null || !result.Success || establishingAttack == null || opponent == null || opponent.Stats == null || opponent.Stats.IsDead)
            return;

        int hpBefore = opponent.Stats.CurrentHP;
        GrappleCheckDamageRoll roll = RollAndApplyGrappleCheckDamage(opponent, establishingAttack, nonlethal);
        result.DamageDealt += roll.Final;
        result.TargetKilled = opponent.Stats.IsDead;
        string mode = nonlethal ? "nonlethal" : "lethal";
        string line = $"Improved Grab: the successful grapple check also deals the {establishingAttack.Name}'s damage (MM p.310): "
            + $"{roll.DiceCount}d{roll.DiceSides} + [{roll.Bonus.Describe()}] = {roll.Raw} {mode} ({opponent.Stats.CharacterName} takes {roll.Final}) [Rolls: {string.Join(", ", roll.Rolls)}]. "
            + $"HP: {hpBefore} -> {opponent.Stats.CurrentHP}";
        result.Log = string.IsNullOrEmpty(result.Log) ? line : result.Log + "\n\n" + line;
    }

    /// <summary>
    /// The grapple actions a creature holding with one body part after Improved Grab may take against the creature it
    /// holds: the Damage grapple check, from the round after the grab (MM p.310: each successful grapple check in
    /// successive rounds deals the establishing attack's damage; at -20, GetImprovedGrabHoldGrappleCheckPenalty), in
    /// place of an attack. Its other grapple actions (pin, Move, use opponent's weapon) are not offered: a holder with one
    /// body part is not grappling (MM p.310), so it does not get the PHB p.156-157 list; it moves with its own normal
    /// movement, carrying the held creature (delegated RAW reading 2026-10-14, CMB-163). Shared by the resolver, the PC
    /// buttons and the AI.
    /// </summary>
    public bool IsGrappleActionAvailableToImprovedGrabHolder(GrappleActionType actionType, out string reason)
    {
        reason = string.Empty;
        if (!TryGetImprovedGrabHold(out CharacterController held, out NaturalAttackDefinition bodyPart))
        {
            reason = $"{(Stats != null ? Stats.CharacterName : name)} holds nobody with Improved Grab.";
            return false;
        }

        string heldName = held.Stats != null ? held.Stats.CharacterName : held.name;
        if (actionType != GrappleActionType.DamageOpponent)
        {
            reason = $"{Stats.CharacterName} holds {heldName} with one body part and is not grappling: its only grapple action is the Damage check (MM p.310; CMB-163).";
            return false;
        }

        if (GetImprovedGrabDamageAttackForGrappleCheck() == null)
        {
            string partName = bodyPart != null && !string.IsNullOrWhiteSpace(bodyPart.Name) ? bodyPart.Name.Trim().ToLowerInvariant() : "body part";
            reason = $"{Stats.CharacterName}'s {partName} hold deals its damage with grapple checks in the rounds after the grab (MM p.310).";
            return false;
        }

        return true;
    }

    private SpecialAttackResult ResolveGrappleActionCore(
        GrappleActionType actionType,
        AttackDamageMode? grappleDamageModeOverride,
        EquipSlot? opponentWeaponHandSlotOverride,
        int? iterativeAttackBonusOverride,
        int naturalAttackIndex,
        bool naturalAttackIsHasteExtra)
    {
        if (!TryGetGrappleState(out CharacterController opponent, out _, out bool isPinned, out bool opponentPinned))
        {
            // A holder with one body part (MM p.310) is not grappling, but its Damage check against the creature it holds
            // goes through here, at its -20 (CMB-163).
            if (!TryGetImprovedGrabHold(out CharacterController held, out _))
            {
                return new SpecialAttackResult
                {
                    ManeuverName = "Grapple Action",
                    Success = false,
                    Log = $"{Stats.CharacterName} is not currently in a grapple."
                };
            }

            if (!IsGrappleActionAvailableToImprovedGrabHolder(actionType, out string holderReason))
            {
                return new SpecialAttackResult
                {
                    ManeuverName = "Grapple Action",
                    Success = false,
                    Log = holderReason
                };
            }

            opponent = held;
            isPinned = false;
            opponentPinned = false;
        }

        bool isPinning = IsPinningOpponent();
        if (IsGrappleActionBlockedWhilePinning(actionType, out string blockedReason)
            || IsGrappleActionBlockedByImprovedGrabHold(actionType, out blockedReason))
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Grapple Action",
                Success = false,
                Log = blockedReason
            };
        }

        switch (actionType)
        {
            case GrappleActionType.EscapeArtist:
            {
                int roll = DiceService.D20("Escape Artist check");
                int bonus = Stats.GetSkillBonus("Escape Artist");
                int total = roll + bonus;
                int dc = 20 + opponent.GetGrappleModifier();
                bool success = total >= dc;

                bool escapedPinOnly = false;
                if (success)
                {
                    if (isPinned)
                    {
                        if (TryGetGrappleLink(this, out GrappleLink link))
                        {
                            ClearPinnedState(link);
                            escapedPinOnly = true;
                        }
                    }
                    else
                    {
                        ReleaseGrappleState("escaped with Escape Artist");
                    }
                }

                string outcome = success
                    ? (escapedPinOnly
                        ? $"{Stats.CharacterName} slips out of the pin by {opponent.Stats.CharacterName}. The grapple continues."
                        : $"{Stats.CharacterName} slips free of {opponent.Stats.CharacterName}'s hold!")
                    : $"{Stats.CharacterName} fails to slip free.";

                string log = string.Join("\n\n", new[]
                {
                    $"{Stats.CharacterName} attempts Escape Artist against {opponent.Stats.CharacterName}",
                    BuildD20Formula($"{Stats.CharacterName} Escape Artist", roll, bonus, total),
                    $"Escape DC: 20 + {opponent.Stats.CharacterName} grapple mod ({opponent.GetGrappleModifier()}) = {dc}",
                    $"Result: {(success ? "SUCCESS" : "FAILURE")} ({total} vs DC {dc})\n{outcome}"
                });

                return new SpecialAttackResult
                {
                    ManeuverName = "Grapple Escape (Escape Artist)",
                    Success = success,
                    CheckRoll = roll,
                    CheckTotal = total,
                    OpposedTotal = dc,
                    Log = log
                };
            }
            case GrappleActionType.OpposedGrappleEscape:
            {
                if (opponent == null || opponent.Stats == null || Stats == null)
                {
                    return new SpecialAttackResult
                    {
                        ManeuverName = "Grapple Escape",
                        Success = false,
                        Log = "No valid grapple opponent."
                    };
                }

                GrappleCheckResult myCheck = RollGrappleCheck(iterativeAttackBonusOverride,
                    context: isPinned ? GrappleCheckContext.EscapePin : GrappleCheckContext.EscapeGrapple);
                GrappleCheckResult oppCheck = opponent.RollGrappleCheck(context: GrappleCheckContext.ResistGrapple);

                bool success = DoesAttackerWinGrappleCheck(myCheck, oppCheck);
                bool escapedPinOnly = false;
                if (success)
                {
                    if (isPinned)
                    {
                        if (TryGetGrappleLink(this, out GrappleLink link))
                        {
                            ClearPinnedState(link);
                            escapedPinOnly = true;
                        }
                    }
                    else
                    {
                        ReleaseGrappleState("escaped with grapple check");
                    }
                }

                string outcome = success
                    ? (escapedPinOnly
                        ? $"{Stats.CharacterName} escapes the pin, but the grapple continues!"
                        : $"{Stats.CharacterName} escapes from grapple!")
                    : $"{Stats.CharacterName} fails to escape.";

                string log = string.Join("\n\n", new[]
                {
                    $"{Stats.CharacterName} attempts to escape from grapple",
                    myCheck.GetBreakdown(),
                    oppCheck.GetBreakdown(),
                    BuildOpposedResultLine(Stats.CharacterName, myCheck.Total, opponent.Stats.CharacterName, oppCheck.Total, success) + "\n" + outcome
                });

                return new SpecialAttackResult
                {
                    ManeuverName = "Grapple Escape (Opposed)",
                    Success = success,
                    CheckRoll = myCheck.BaseRoll,
                    CheckTotal = myCheck.Total,
                    OpposedRoll = oppCheck.BaseRoll,
                    OpposedTotal = oppCheck.Total,
                    Log = log
                };
            }
            case GrappleActionType.DamageOpponent:
            {
                if (isPinned)
                {
                    return new SpecialAttackResult
                    {
                        ManeuverName = "Grapple Damage",
                        Success = false,
                        Log = $"{Stats.CharacterName} is pinned and cannot deal grapple damage until they escape the pin."
                    };
                }

                if (opponent == null || opponent.Stats == null || Stats == null)
                {
                    return new SpecialAttackResult
                    {
                        ManeuverName = "Grapple Damage",
                        Success = false,
                        Log = "No valid grapple opponent."
                    };
                }

                // The damage-mode rule of the Damage action (GetGrappleDamageModeRule, shared with the PC menu): after an
                // Improved Grab the establishing natural attack's damage (MM p.310, CMB-148; no constrict here), lethal by
                // default, nonlethal at -4; otherwise unarmed strike damage (PHB p.156).
                GrappleDamageModeRule damageRule = GetGrappleDamageModeRule();
                NaturalAttackDefinition holdAttack = damageRule.HoldAttack;
                string grappleDamageRuleReason = damageRule.Reason;

                AttackDamageMode selectedMode = grappleDamageModeOverride ?? damageRule.DefaultMode;
                bool dealNonlethalDamage = selectedMode == AttackDamageMode.Nonlethal;
                int grappleCheckPenalty = dealNonlethalDamage ? damageRule.NonlethalPenalty : damageRule.LethalPenalty;
                string grappleCheckPenaltyLabel = grappleCheckPenalty == 0
                    ? null
                    : (holdAttack != null ? $"Nonlethal damage with the {holdAttack.Name}" : "Lethal damage with an unarmed strike (PHB p.156)");

                GrappleCheckResult myCheck = RollGrappleCheck(iterativeAttackBonusOverride, grappleCheckPenalty, grappleCheckPenaltyLabel);
                GrappleCheckResult oppCheck = opponent.RollGrappleCheck();

                bool success = DoesAttackerWinGrappleCheck(myCheck, oppCheck);
                GrappleCheckDamageRoll damageRoll = success
                    ? RollAndApplyGrappleCheckDamage(opponent, holdAttack, dealNonlethalDamage)
                    : default(GrappleCheckDamageRoll);
                int finalDamageDealt = damageRoll.Final;
                int rawDamage = damageRoll.Raw;
                int damageDiceCount = damageRoll.DiceCount;
                int damageDiceSides = damageRoll.DiceSides;
                WeaponDamageBreakdown grappleDamageBonus = damageRoll.Bonus;
                List<int> damageRolls = damageRoll.Rolls ?? new List<int>();

                string damageTypeLabel = dealNonlethalDamage ? "nonlethal" : "lethal";
                string defaultDamageRuleSummary = holdAttack != null
                    ? $"Deals the {holdAttack.Name}'s damage, lethal by default ({grappleDamageRuleReason})."
                    : (grappleDamageRuleReason != null
                        ? $"Deals lethal damage by default ({grappleDamageRuleReason})."
                        : "Deals nonlethal damage by default (unarmed strike, PHB p.156).");
                string penaltyRuleSummary = grappleCheckPenalty != 0
                    ? (holdAttack != null
                        ? $"Applies -4 penalty for choosing nonlethal damage with the {holdAttack.Name} (PHB p.146)."
                        : "Applies -4 penalty for choosing lethal damage with an unarmed strike (PHB p.156).")
                    : (grappleDamageRuleReason != null
                        ? $"No penalty ({grappleDamageRuleReason})."
                        : "No penalty (nonlethal default).");

                string resultLine = BuildOpposedResultLine(Stats.CharacterName, myCheck.Total, opponent.Stats.CharacterName, oppCheck.Total, success);
                string outcomeLine = success
                    ? $"Damage: {damageDiceCount}d{damageDiceSides} + [{grappleDamageBonus.Describe()}] = {rawDamage} {damageTypeLabel} ({opponent.Stats.CharacterName} takes {finalDamageDealt})."
                    : $"{Stats.CharacterName} fails to damage {opponent.Stats.CharacterName}.";

                if (success && damageRolls.Count > 0)
                    outcomeLine += $" [Rolls: {string.Join(", ", damageRolls)}]";

                string log = string.Join("\n\n", new[]
                {
                    $"{Stats.CharacterName} attempts to damage {opponent.Stats.CharacterName}",
                    myCheck.GetBreakdown(),
                    oppCheck.GetBreakdown(),
                    resultLine + "\n" + outcomeLine,
                    defaultDamageRuleSummary + " " + penaltyRuleSummary
                });

                return new SpecialAttackResult
                {
                    ManeuverName = "Grapple Damage",
                    Success = success,
                    CheckRoll = myCheck.BaseRoll,
                    CheckTotal = myCheck.Total,
                    OpposedRoll = oppCheck.BaseRoll,
                    OpposedTotal = oppCheck.Total,
                    DamageDealt = finalDamageDealt,
                    TargetKilled = opponent.Stats.IsDead,
                    Log = log
                };
            }
            case GrappleActionType.AttackWithLightWeapon:
            {
                return ResolveLightWeaponAttackWhileGrappling(opponent, isPinned, iterativeAttackBonusOverride);
            }
            case GrappleActionType.AttackUnarmed:
            {
                return ResolveUnarmedAttackWhileGrappling(opponent, isPinned, iterativeAttackBonusOverride, naturalAttackIndex, naturalAttackIsHasteExtra);
            }
            case GrappleActionType.PinOpponent:
            {
                if (isPinned)
                {
                    return new SpecialAttackResult
                    {
                        ManeuverName = "Pin Opponent",
                        Success = false,
                        Log = $"{Stats.CharacterName} is pinned and cannot pin {opponent.Stats.CharacterName}."
                    };
                }

                // PHB p.156 (CMB-120): the pin lasts 1 round; on the pinner's next turn it may pin again
                // (renewal). Before then the opponent is already held for this round.
                bool isRenewal = isPinning && opponentPinned && IsPinRenewalDue();
                if (isPinning && !isRenewal)
                {
                    return new SpecialAttackResult
                    {
                        ManeuverName = "Pin Opponent",
                        Success = false,
                        Log = $"{Stats.CharacterName} already holds {opponent.Stats.CharacterName} pinned this round."
                    };
                }

                if (opponent == null || opponent.Stats == null || Stats == null)
                {
                    return new SpecialAttackResult
                    {
                        ManeuverName = "Pin Opponent",
                        Success = false,
                        Log = "No valid grapple opponent."
                    };
                }

                // A renewal is a new pin attempt: the 1-round pin is over, so it ends before the checks and the
                // defender does not resist with the Pinned condition's modifiers (PHB p.156-157). A failed renewal
                // therefore leaves the pin ended and the grapple in place (owner decision 2026-10-07, CMB-120).
                bool hasLink = TryGetGrappleLink(this, out GrappleLink link);
                if (isRenewal && hasLink)
                    ClearPinnedState(link);

                GrappleCheckResult myCheck = RollGrappleCheck(iterativeAttackBonusOverride);
                GrappleCheckResult oppCheck = opponent.RollGrappleCheck(context: GrappleCheckContext.ResistPin);

                bool success = DoesAttackerWinGrappleCheck(myCheck, oppCheck);
                if (success && hasLink)
                {
                    // A renewal restarts the 1-round duration (SetPinnedState).
                    SetPinnedState(link, opponent, this);
                    RemoveConditionIfPresent(this, CombatConditionType.Pinned);
                }

                string outcomeLine;
                if (isRenewal)
                    outcomeLine = success
                        ? $"{opponent.Stats.CharacterName} stays pinned for another round!"
                        : $"{Stats.CharacterName} fails to keep {opponent.Stats.CharacterName} pinned. The pin ends; the grapple continues.";
                else
                    outcomeLine = success
                        ? $"{opponent.Stats.CharacterName} is pinned!"
                        : $"{Stats.CharacterName} fails to pin {opponent.Stats.CharacterName}.";

                string log = string.Join("\n\n", new[]
                {
                    isRenewal
                        ? $"{Stats.CharacterName} attempts to pin {opponent.Stats.CharacterName} again (the pin's 1 round is up)"
                        : $"{Stats.CharacterName} attempts to pin {opponent.Stats.CharacterName}",
                    myCheck.GetBreakdown(),
                    oppCheck.GetBreakdown(),
                    BuildOpposedResultLine(Stats.CharacterName, myCheck.Total, opponent.Stats.CharacterName, oppCheck.Total, success) + "\n" + outcomeLine
                });

                return new SpecialAttackResult
                {
                    ManeuverName = "Pin Opponent",
                    Success = success,
                    CheckRoll = myCheck.BaseRoll,
                    CheckTotal = myCheck.Total,
                    OpposedRoll = oppCheck.BaseRoll,
                    OpposedTotal = oppCheck.Total,
                    Log = log
                };
            }
            case GrappleActionType.MoveHalfSpeed:
            {
                if (isPinned)
                {
                    return new SpecialAttackResult
                    {
                        ManeuverName = "Move While Grappling",
                        Success = false,
                        Log = $"{Stats.CharacterName} is pinned and cannot move the grapple."
                    };
                }

                if (!TryGetActiveGrappleOpponents(out List<CharacterController> grappleOpponents))
                {
                    return new SpecialAttackResult
                    {
                        ManeuverName = "Move While Grappling",
                        Success = false,
                        Log = "No valid grapple opponents."
                    };
                }

                int myRoll = DiceService.D20("Grapple opposed check (attacker)");
                // GetGrappleModifier plus the -4 of nonlethal Improved Grab hold damage when chosen (ResolveGrappleAction).
                int myBaseModifier = GetGrappleModifier() + _improvedGrabCheckModifier;

                bool isOneVsOne = grappleOpponents.Count == 1;
                bool movingPinnedOpponent = isOneVsOne && grappleOpponents[0] != null && grappleOpponents[0].HasCondition(CombatConditionType.Pinned);
                int pinnedMoveBonus = (isOneVsOne && movingPinnedOpponent) ? 4 : 0;

                int myTotal = myRoll + myBaseModifier + pinnedMoveBonus;
                bool beatAllOpponents = true;
                int highestOpposedTotal = int.MinValue;
                int highestOpposedRoll = 0;

                int myFormulaModifier = myBaseModifier + pinnedMoveBonus;
                var opposedCheckLogLines = new List<string>
                {
                    pinnedMoveBonus > 0
                        ? $"{Stats.CharacterName} gains +4 on grapple check for moving a pinned opponent in a 1v1 grapple."
                        : string.Empty,
                    _improvedGrabCheckModifier != 0
                        ? $"{Stats.CharacterName} takes {_improvedGrabCheckModifier} on the grapple check: {_improvedGrabCheckModifierLabel}."
                        : string.Empty,
                    BuildD20Formula($"{Stats.CharacterName} grapple check", myRoll, myFormulaModifier, myTotal)
                };

                for (int i = 0; i < grappleOpponents.Count; i++)
                {
                    CharacterController currentOpponent = grappleOpponents[i];
                    if (currentOpponent == null || currentOpponent.Stats == null || currentOpponent.Stats.IsDead)
                        continue;

                    int oppRoll = DiceService.D20("Grapple opposed check (defender)");
                    int oppMod = currentOpponent.GetGrappleModifier();
                    int oppTotal = oppRoll + oppMod;
                    bool beatThisOpponent = DoesAttackerWinOpposedCheck(myTotal, myFormulaModifier, oppTotal, oppMod);
                    beatAllOpponents &= beatThisOpponent;

                    if (oppTotal > highestOpposedTotal)
                    {
                        highestOpposedTotal = oppTotal;
                        highestOpposedRoll = oppRoll;
                    }

                    opposedCheckLogLines.Add(BuildD20Formula($"vs {currentOpponent.Stats.CharacterName}", oppRoll, oppMod, oppTotal)
                        + $" → {(beatThisOpponent ? "beaten" : "not beaten")}");
                }

                if (highestOpposedTotal == int.MinValue)
                    highestOpposedTotal = 0;

                opposedCheckLogLines.RemoveAll(string.IsNullOrEmpty);

                string resultSummary = beatAllOpponents
                    ? $"{Stats.CharacterName} beats all opposed grapple checks and can move the grapple up to half speed (standard action)."
                    : $"{Stats.CharacterName} fails to beat all opposed grapple checks. No grapple movement occurs.";

                opposedCheckLogLines.Add(resultSummary);

                return new SpecialAttackResult
                {
                    ManeuverName = "Move While Grappling",
                    Success = beatAllOpponents,
                    CheckRoll = myRoll,
                    CheckTotal = myTotal,
                    OpposedRoll = highestOpposedRoll,
                    OpposedTotal = highestOpposedTotal,
                    Log = string.Join("\n", opposedCheckLogLines)
                };
            }
            case GrappleActionType.UseOpponentWeapon:
                return ResolveUseOpponentWeapon(opponent, isPinned, opponentWeaponHandSlotOverride, iterativeAttackBonusOverride);

            case GrappleActionType.DisarmSmallObject:
                return ResolveDisarmSmallObjectStub(opponent, isPinned);

            case GrappleActionType.DrawLightWeapon:
                return ResolveDrawLightWeaponDuringGrappleStub();

            case GrappleActionType.RetrieveSpellComponent:
                return ResolveRetrieveSpellComponentDuringGrappleStub();

            case GrappleActionType.Rake:
                return ResolveGrappleRake();

            case GrappleActionType.ReleasePinnedOpponent:
            {
                if (!isPinning || !opponentPinned)
                {
                    return new SpecialAttackResult
                    {
                        ManeuverName = "Release Pinned Opponent",
                        Success = false,
                        Log = $"{Stats.CharacterName} is not actively pinning {opponent.Stats.CharacterName}."
                    };
                }

                string opponentName = opponent != null && opponent.Stats != null
                    ? opponent.Stats.CharacterName
                    : "opponent";

                // PHB p.157: releasing a pin (a free action) ends the grapple for both creatures (CMB-089).
                ReleaseGrappleState("pinned opponent released");

                return new SpecialAttackResult
                {
                    ManeuverName = "Release Pinned Opponent",
                    Success = true,
                    Log = $"{Stats.CharacterName} releases {opponentName} from the pin, ending the grapple."
                };
            }
            default:
                return new SpecialAttackResult
                {
                    ManeuverName = "Grapple Action",
                    Success = false,
                    Log = "Unknown grapple action."
                };
        }
    }

    /// <summary>
    /// The grapple check modifier of the Use Opponent's Weapon check: the one grapple check formula (BAB, Str, special
    /// size modifier and effects that name grapple checks such as Improved Grapple; PHB p.155-156, CMB-130).
    /// </summary>
    private int GetStrictOpposedGrappleModifierForUseOpponentWeapon(int? baseAttackBonusOverride = null)
        => GetGrappleModifier(baseAttackBonusOverride);

    private bool TryResolveStrictOpposedGrappleCheckForUseOpponentWeapon(
        CharacterController opponent,
        out int myRoll,
        out int myTotal,
        out int oppRoll,
        out int oppTotal,
        out int myModifier,
        out int oppModifier,
        int? myBaseAttackBonusOverride)
    {
        myRoll = 0;
        myTotal = 0;
        oppRoll = 0;
        oppTotal = 0;
        myModifier = 0;
        oppModifier = 0;

        if (opponent == null || opponent.Stats == null || Stats == null)
            return false;

        // Plus the -4 of nonlethal Improved Grab hold damage when chosen (ResolveGrappleAction).
        myModifier = GetStrictOpposedGrappleModifierForUseOpponentWeapon(myBaseAttackBonusOverride) + _improvedGrabCheckModifier;
        oppModifier = opponent.GetStrictOpposedGrappleModifierForUseOpponentWeapon();

        myRoll = DiceService.D20("Grapple move check (self)");
        oppRoll = DiceService.D20("Grapple move check (opponent)");
        myTotal = myRoll + myModifier;
        oppTotal = oppRoll + oppModifier;
        return true;
    }

    private ItemData ResolveOpponentLightWeaponForUseOpponentWeapon(
        CharacterController opponent,
        EquipSlot? handSlotOverride,
        out EquipSlot selectedSlot,
        out string selectionNote)
    {
        selectedSlot = EquipSlot.RightHand;
        selectionNote = string.Empty;

        if (opponent == null)
            return null;

        List<DisarmableHeldItemOption> options = opponent.GetEquippedLightHandWeaponOptions();
        if (options == null || options.Count == 0)
            return null;

        if (handSlotOverride.HasValue)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].HandSlot == handSlotOverride.Value)
                {
                    selectedSlot = options[i].HandSlot;
                    return options[i].HeldItem;
                }
            }
        }

        selectedSlot = options[0].HandSlot;
        ItemData fallbackWeapon = options[0].HeldItem;
        if (handSlotOverride.HasValue)
            selectionNote = $"Requested {handSlotOverride.Value} light weapon was unavailable; defaulted to {selectedSlot}.";

        return fallbackWeapon;
    }

    private SpecialAttackResult ResolveUseOpponentWeapon(CharacterController opponent, bool isPinned, EquipSlot? opponentWeaponHandSlotOverride, int? iterativeAttackBonusOverride)
    {
        const int useOpponentWeaponAttackPenalty = -4;

        if (isPinned)
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Use Opponent's Weapon",
                Success = false,
                Log = $"{Stats.CharacterName} is pinned and cannot use {opponent?.Stats?.CharacterName ?? "the opponent"}'s weapon."
            };
        }

        ItemData opponentWeapon = ResolveOpponentLightWeaponForUseOpponentWeapon(
            opponent,
            opponentWeaponHandSlotOverride,
            out EquipSlot selectedHandSlot,
            out string selectionNote);

        if (opponentWeapon == null)
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Use Opponent's Weapon",
                Success = false,
                Log = $"{Stats.CharacterName} cannot use opponent's weapon because {opponent.Stats.CharacterName} has no equipped light weapon in either hand."
            };
        }

        if (!TryResolveStrictOpposedGrappleCheckForUseOpponentWeapon(
                opponent,
                out int myRoll,
                out int myTotal,
                out int oppRoll,
                out int oppTotal,
                out int myModifier,
                out int oppModifier,
                iterativeAttackBonusOverride))
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Use Opponent's Weapon",
                Success = false,
                Log = "No valid grapple opponent."
            };
        }

        bool grappleSuccess = DoesAttackerWinOpposedCheck(myTotal, myModifier, oppTotal, oppModifier);
        var logLines = new List<string>
        {
            $"{Stats.CharacterName} attempts to use opponent's {opponentWeapon.Name}.",
            string.IsNullOrEmpty(selectionNote) ? string.Empty : selectionNote,
            _improvedGrabCheckModifier != 0 ? $"{Stats.CharacterName} takes {_improvedGrabCheckModifier} on the grapple check: {_improvedGrabCheckModifierLabel}." : string.Empty,
            BuildD20Formula($"{Stats.CharacterName} grapple check", myRoll, myModifier, myTotal),
            BuildD20Formula($"{opponent.Stats.CharacterName} grapple check", oppRoll, oppModifier, oppTotal),
            BuildOpposedResultLine(Stats.CharacterName, myTotal, opponent.Stats.CharacterName, oppTotal, grappleSuccess)
        };

        int finalDamageDealt = 0;
        bool targetKilled = false;

        if (grappleSuccess)
        {
            int weaponNonProfPenalty = Stats.GetWeaponNonProficiencyPenalty(opponentWeapon);
            int armorNonProfPenalty = Stats.GetArmorNonProficiencyAttackPenalty();
            int attackBaseBonus = iterativeAttackBonusOverride ?? Stats.BaseAttackBonus;
            int attackMod = attackBaseBonus
                            + Stats.STRMod
                            + Stats.SizeModifier
                            + Stats.ConditionAttackPenalty
                            + GetProneAttackModifier(isMeleeAttack: true)
                            + weaponNonProfPenalty
                            + armorNonProfPenalty
                            + useOpponentWeaponAttackPenalty
                            // An attack roll in a flurry round takes the flurry penalty (PHB p.40; delegated RAW reading 2026-10-14).
                            + FlurryAttackRollPenalty;

            GetScaledWeaponDamageDice(opponentWeapon, out int scaledOpponentDamageCount, out int scaledOpponentDamageDice);
            int damageDice = Mathf.Max(1, scaledOpponentDamageDice); // a 1-point step (DMG p.28) rolls 1d1, CMB-133
            int damageCount = Mathf.Max(1, scaledOpponentDamageCount);
            // Shared damage terms (CMB-003) with Weapon Specialization (a damage-only feat, PHB p.102); no Power Attack
            // while the attack modifier is built by hand and takes no Power Attack penalty (CMB-087).
            WeaponDamageBreakdown weaponDamage = BuildWeaponDamageBonus(opponentWeapon, isRanged: false, isOffHand: false,
                DamageOnlyFeatModifiers(opponentWeapon), opponentWeapon.BonusDamage);
            int critThreatMin = opponentWeapon.CritThreatMin > 0 ? opponentWeapon.CritThreatMin : 20;
            int critMultiplier = opponentWeapon.CritMultiplier > 0 ? opponentWeapon.CritMultiplier : 2;

            int hpBefore = opponent.Stats.CurrentHP;
            CombatResult attackResult = PerformSingleAttackWithCrit(
                opponent,
                attackMod,
                isFlanking: false,
                flankingBonus: 0,
                flankingPartnerName: null,
                damageDice,
                damageCount,
                weaponDamage,
                critThreatMin,
                critMultiplier,
                opponentWeapon,
                situationalTargetAcBonus: 0,
                dealNonlethalDamage: false,
                damageModeAttackPenalty: 0,
                damageModePenaltySource: string.Empty);

            attackResult.BreakdownBAB = attackBaseBonus;
            attackResult.BreakdownAbilityMod = Stats.STRMod;
            attackResult.BreakdownAbilityName = "STR";
            attackResult.SizeAttackBonus = Stats.SizeModifier;
            attackResult.WeaponNonProficiencyPenalty = weaponNonProfPenalty;
            attackResult.ArmorNonProficiencyPenalty = armorNonProfPenalty;
            attackResult.DefenderHPBefore = hpBefore;
            attackResult.DefenderHPAfter = opponent.Stats.CurrentHP;

            finalDamageDealt = attackResult.FinalDamageDealt;
            targetKilled = opponent.Stats.IsDead;

            int attackDamageRaw = attackResult.RawTotalDamage > 0 ? attackResult.RawTotalDamage : (attackResult.Damage + attackResult.SneakAttackDamage);
            int attackDamageStaticModifier = attackResult.Damage - attackResult.BaseDamageRoll;

            logLines.Add($"{Stats.CharacterName} uses {opponent.Stats.CharacterName}'s {opponentWeapon.Name} against them ({selectedHandSlot}).");
            logLines.Add("-4 penalty for using opponent's weapon.");
            if (FlurryAttackRollPenalty != 0)
                logLines.Add($"Flurry of blows penalty applied: {CharacterStats.FormatMod(FlurryAttackRollPenalty)}.");
            logLines.Add(BuildD20Formula("Attack roll", attackResult.DieRoll, attackMod, attackResult.TotalRoll)
                + $" vs AC {attackResult.TargetAC} => {(attackResult.Hit ? "HIT" : "MISS")}");
            if (attackResult.Hit)
            {
                logLines.Add(BuildDamageFormula(
                    "Damage roll",
                    string.IsNullOrEmpty(attackResult.DamageDiceShown) ? $"{damageCount}d{damageDice}" : attackResult.DamageDiceShown,
                    attackResult.BaseDamageRoll,
                    attackDamageStaticModifier,
                    attackDamageRaw,
                    finalDamageDealt));
                logLines.Add($"Target HP: {attackResult.DefenderHPBefore} -> {attackResult.DefenderHPAfter}");
            }
            else
            {
                logLines.Add("Damage roll: not rolled (attack missed).");
            }
        }
        else
        {
            logLines.Add($"{Stats.CharacterName} fails to control {opponent.Stats.CharacterName}'s weapon and cannot make the attack.");
        }

        logLines.RemoveAll(string.IsNullOrEmpty);

        return new SpecialAttackResult
        {
            ManeuverName = "Use Opponent's Weapon",
            Success = grappleSuccess,
            CheckRoll = myRoll,
            CheckTotal = myTotal,
            OpposedRoll = oppRoll,
            OpposedTotal = oppTotal,
            DamageDealt = finalDamageDealt,
            TargetKilled = targetKilled,
            Log = string.Join("\n", logLines)
        };
    }

    private SpecialAttackResult ResolveLightWeaponAttackWhileGrappling(CharacterController opponent, bool isPinned, int? iterativeAttackBonusOverride)
    {
        if (!CanAttackWithLightWeaponWhileGrappling(out ItemData mainHandLightWeapon, out string reason))
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Grapple Light Weapon Attack",
                Success = false,
                Log = $"{Stats.CharacterName} cannot attack with a light weapon while grappling: {reason}"
            };
        }

        return ResolveAttackWhileGrappling(
            opponent,
            mainHandLightWeapon,
            maneuverName: "Grapple Light Weapon Attack",
            isPinned: isPinned,
            enforceMainHandLightWeaponOnly: true,
            iterativeAttackBonusOverride: iterativeAttackBonusOverride);
    }

    private SpecialAttackResult ResolveUnarmedAttackWhileGrappling(CharacterController opponent, bool isPinned, int? iterativeAttackBonusOverride,
        int naturalAttackIndex, bool naturalAttackIsHasteExtra)
    {
        if (!CanAttackUnarmedWhileGrappling(out string reason))
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Grapple Unarmed Attack",
                Success = false,
                Log = $"{Stats.CharacterName} cannot make an unarmed grapple attack: {reason}"
            };
        }

        return ResolveAttackWhileGrappling(
            opponent,
            null,
            maneuverName: "Grapple Unarmed Attack",
            isPinned: isPinned,
            enforceMainHandLightWeaponOnly: false,
            iterativeAttackBonusOverride: iterativeAttackBonusOverride,
            naturalAttackIndex: naturalAttackIndex,
            naturalAttackIsHasteExtra: naturalAttackIsHasteExtra);
    }

    private SpecialAttackResult ResolveAttackWhileGrappling(
        CharacterController opponent,
        ItemData weapon,
        string maneuverName,
        bool isPinned,
        bool enforceMainHandLightWeaponOnly,
        int? iterativeAttackBonusOverride,
        int naturalAttackIndex = -1,
        bool naturalAttackIsHasteExtra = false)
    {
        const int grappleAttackPenalty = -4;

        if (isPinned)
        {
            return new SpecialAttackResult
            {
                ManeuverName = maneuverName,
                Success = false,
                Log = $"{Stats.CharacterName} is pinned and cannot make this grapple attack."
            };
        }

        if (opponent == null || opponent.Stats == null)
        {
            return new SpecialAttackResult
            {
                ManeuverName = maneuverName,
                Success = false,
                Log = "No valid grapple opponent."
            };
        }

        if (enforceMainHandLightWeaponOnly)
        {
            if (!CanAttackWithLightWeaponWhileGrappling(out ItemData mainHandWeapon, out string reason))
            {
                return new SpecialAttackResult
                {
                    ManeuverName = maneuverName,
                    Success = false,
                    Log = $"{Stats.CharacterName} cannot attack with a light weapon while grappling: {reason}"
                };
            }

            weapon = mainHandWeapon;
        }
        else if (!CanAttackWithWeapon(weapon, out string cannotAttackReason))
        {
            return new SpecialAttackResult
            {
                ManeuverName = maneuverName,
                Success = false,
                Log = $"{Stats.CharacterName} cannot make grapple attack: {cannotAttackReason}"
            };
        }

        bool isUnarmed = weapon == null;
        // A creature fighting with its natural attacks attacks with one of them (PHB p.156; MM p.314: one natural
        // weapon per attack while grappling; CMB-127): the attacker's pick. When the caller named none, the natural
        // attack the committed step gave up (an override BAB means the caller committed one), else the default
        // option. It never falls back to an unarmed strike.
        if (isUnarmed && UsesNaturalAttacksForGrappleAttack())
        {
            if (naturalAttackIndex < 0)
            {
                if (iterativeAttackBonusOverride.HasValue && ProgressiveAttackPool.LastSubstituteNaturalAttackIndex >= 0)
                {
                    naturalAttackIndex = ProgressiveAttackPool.LastSubstituteNaturalAttackIndex;
                    naturalAttackIsHasteExtra = ProgressiveAttackPool.LastSubstituteWasHasteExtraAttack;
                }
                else
                {
                    GrappleNaturalAttackOption fallback = GetDefaultGrappleNaturalAttackOption(GetGrappleNaturalAttackOptions());
                    naturalAttackIndex = fallback.NaturalAttackIndex;
                    naturalAttackIsHasteExtra = fallback.IsHasteExtraAttack;
                }
            }

            if (naturalAttackIndex < 0)
            {
                return new SpecialAttackResult
                {
                    ManeuverName = maneuverName,
                    Success = false,
                    Log = $"{Stats.CharacterName} has no natural attack left to make while grappling this turn."
                };
            }

            return ResolveGrappleNaturalAttack(opponent, naturalAttackIndex, naturalAttackIsHasteExtra, maneuverName);
        }

        DamageModeAttackProfile damageMode = ResolveDamageModeAttackProfile(weapon);

        int weaponNonProfPenalty = isUnarmed ? 0 : Stats.GetWeaponNonProficiencyPenalty(weapon);
        int armorNonProfPenalty = Stats.GetArmorNonProficiencyAttackPenalty();
        int attackBaseBonus = iterativeAttackBonusOverride ?? Stats.BaseAttackBonus;
        int attackMod = attackBaseBonus
                        + Stats.STRMod
                        + Stats.SizeModifier
                        + Stats.ConditionAttackPenalty
                        + GetProneAttackModifier(isMeleeAttack: true)
                        + weaponNonProfPenalty
                        + armorNonProfPenalty
                        + grappleAttackPenalty
                        + damageMode.AttackPenalty
                        // An attack roll in a flurry round takes the flurry penalty (PHB p.40; delegated RAW reading 2026-10-14).
                        + FlurryAttackRollPenalty;

        int damageDice;
        int damageCount;
        int bonusDamage;
        int critThreatMin;
        int critMultiplier;

        string grappleAttackLabel = string.Empty;

        if (isUnarmed)
        {
            var unarmed = GetUnarmedDamage();
            damageDice = Mathf.Max(2, unarmed.damageDice);
            damageCount = Mathf.Max(1, unarmed.damageCount);
            // Strength comes from the shared damage modifier once (it was added here and again by the attack core, CMB-003).
            bonusDamage = unarmed.bonusDamage;
            critThreatMin = 20;
            critMultiplier = 2;
        }
        else
        {
            GetScaledWeaponDamageDice(weapon, out int scaledWeaponDamageCount, out int scaledWeaponDamageDice);
            damageDice = Mathf.Max(1, scaledWeaponDamageDice); // a 1-point step (DMG p.28) rolls 1d1, CMB-133
            damageCount = Mathf.Max(1, scaledWeaponDamageCount);
            bonusDamage = weapon.BonusDamage;
            critThreatMin = weapon.CritThreatMin > 0 ? weapon.CritThreatMin : 20;
            critMultiplier = weapon.CritMultiplier > 0 ? weapon.CritMultiplier : 2;
        }

        // Shared damage terms (CMB-003) with Weapon Specialization (a damage-only feat, PHB p.102); no Power Attack
        // while the attack modifier is built by hand and takes no Power Attack penalty (CMB-087).
        WeaponDamageBreakdown grappleDamage = BuildWeaponDamageBonus(weapon, isRanged: false, isOffHand: false,
            DamageOnlyFeatModifiers(weapon), bonusDamage);

        int hpBefore = opponent.Stats.CurrentHP;
        CombatResult attackResult = PerformSingleAttackWithCrit(
            opponent,
            attackMod,
            isFlanking: false,
            flankingBonus: 0,
            flankingPartnerName: null,
            damageDice,
            damageCount,
            grappleDamage,
            critThreatMin,
            critMultiplier,
            weapon,
            situationalTargetAcBonus: 0,
            dealNonlethalDamage: damageMode.DealNonlethalDamage,
            damageModeAttackPenalty: damageMode.AttackPenalty,
            damageModePenaltySource: damageMode.PenaltySource);

        attackResult.BreakdownBAB = Stats.BaseAttackBonus;
        attackResult.BreakdownAbilityMod = Stats.STRMod;
        attackResult.BreakdownAbilityName = "STR";
        attackResult.SizeAttackBonus = Stats.SizeModifier;
        attackResult.WeaponNonProficiencyPenalty = weaponNonProfPenalty;
        attackResult.ArmorNonProficiencyPenalty = armorNonProfPenalty;
        attackResult.DefenderHPBefore = hpBefore;
        attackResult.DefenderHPAfter = opponent.Stats.CurrentHP;

        string weaponLabel = isUnarmed
            ? (string.IsNullOrWhiteSpace(grappleAttackLabel) ? "unarmed strike" : grappleAttackLabel)
            : weapon.Name;
        string damageTypeLabel = damageMode.DealNonlethalDamage ? "nonlethal" : "lethal";
        int grappleAttackRawDamage = attackResult.RawTotalDamage > 0 ? attackResult.RawTotalDamage : (attackResult.Damage + attackResult.SneakAttackDamage);
        int grappleAttackStaticModifier = attackResult.Damage - attackResult.BaseDamageRoll;

        var logLines = new List<string>
        {
            $"{Stats.CharacterName} attacks {opponent.Stats.CharacterName} with {weaponLabel} while grappling.",
            BuildD20Formula("Attack roll", attackResult.DieRoll, attackMod, attackResult.TotalRoll)
                + $" vs AC {attackResult.TargetAC} => {(attackResult.Hit ? "HIT" : "MISS")}",
            "Includes -4 grapple attack penalty.",
            FlurryAttackRollPenalty != 0
                ? $"Flurry of blows penalty applied: {CharacterStats.FormatMod(FlurryAttackRollPenalty)}."
                : string.Empty,
            damageMode.AttackPenalty != 0
                ? $"Damage-mode penalty applied: {damageMode.AttackPenalty:+#;-#;0} ({damageMode.PenaltySource})."
                : string.Empty,
            attackResult.Hit
                ? BuildDamageFormula(
                    $"Damage roll ({damageTypeLabel})",
                    string.IsNullOrEmpty(attackResult.DamageDiceShown) ? $"{damageCount}d{damageDice}" : attackResult.DamageDiceShown,
                    attackResult.BaseDamageRoll,
                    grappleAttackStaticModifier,
                    grappleAttackRawDamage,
                    attackResult.FinalDamageDealt)
                : "Damage roll: not rolled (attack missed).",
            attackResult.Hit
                ? $"Target HP: {attackResult.DefenderHPBefore} -> {attackResult.DefenderHPAfter}."
                : "Damage dealt: 0 (attack missed)."
        };
        logLines.RemoveAll(string.IsNullOrEmpty);

        return new SpecialAttackResult
        {
            ManeuverName = maneuverName,
            Success = attackResult.Hit,
            CheckRoll = attackResult.DieRoll,
            CheckTotal = attackResult.TotalRoll,
            OpposedTotal = attackResult.TargetAC,
            DamageDealt = attackResult.FinalDamageDealt,
            TargetKilled = opponent.Stats.IsDead,
            Log = string.Join("\n", logLines)
        };
    }

    // ----- Grapple attack with a natural weapon (PHB p.156; MM p.314; CMB-127, CMB-146) -----
    // PHB p.156 (Attack Your Opponent): while grappling, an attack with an unarmed strike, a natural weapon or a light
    // weapon takes the place of one of your attacks. MM p.314 (Rake): normally a monster attacks with only one of its
    // natural weapons while grappling. So each grapple attack action of a creature fighting with its natural attacks
    // is ONE natural attack of its choice, at that attack's normal bonus (primary full BAB, secondary -5 or -2 with
    // Multiattack, MM p.312, p.304), with the -4 for attacking in a grapple: the attacker's Grappled condition row
    // (AttackModifier -4, read by BuildAttackBonus), so it is not added a second time here. How many: one per step of
    // the BAB ladder (PHB p.156: one grapple action in place of each of your attacks; owner interpretation 2026-10-11,
    // CMB-146), Haste's extra attack included, so a lion (BAB +3) makes one a turn. The step is a NaturalSequence step
    // (it gives up the natural attack it makes, like a maneuver that replaces a natural attack, CMB-102), capped by the
    // iterative ladder (HasGrappleNaturalAttackLadderStep); every other grapple action (damage, pin, escape, light
    // weapon, opponent's weapon) is an iterative step, and all share the step counter. The step is committed by the
    // caller with the chosen natural attack (GameManager.TryConsumeIterativeGrappleAttack), PC and AI alike; the PC
    // picks in the grapple natural-attack menu, the AI with DND35.AI.NaturalAttackChoice. Rake is its own grapple
    // action (GrappleActionType.Rake, ResolveGrappleRake; CMB-144).

    /// <summary>
    /// Grapple natural attacks this creature can still make this turn: the natural attacks left to choose from
    /// (<paramref name="alsoUsed"/> as in <see cref="GetGrappleNaturalAttackOptions"/>), capped by the steps left on its
    /// BAB ladder (PHB p.156; owner interpretation 2026-10-11, CMB-146) and in its natural sequence.
    /// </summary>
    public int GetRemainingGrappleNaturalAttackCount(Func<int, bool> alsoUsed = null)
    {
        if (!IsGrappling() || !UsesNaturalAttacksForGrappleAttack())
            return 0;

        int ladder = GetRemainingMainHandAttackSteps(AttackStepKind.MainHand);
        int natural = GetRemainingMainHandAttackSteps(AttackStepKind.NaturalSequence);
        return Mathf.Min(Mathf.Min(ladder, natural), GetGrappleNaturalAttackOptions(alsoUsed).Count);
    }

    /// <summary>
    /// Check, pay for and record the step of one grapple natural attack (PHB p.156; MM p.314): refused when no step
    /// is left on the BAB ladder (owner interpretation 2026-10-11, CMB-146), else <see cref="TryCommitManeuverSubstituteStep"/>
    /// with the natural attack it makes (<paramref name="naturalAttackIndex"/>, -1 for the step's default).
    /// </summary>
    public bool TryCommitGrappleNaturalAttackStep(int naturalAttackIndex, bool isHasteExtraAttack, out int attackBab, out int stepIndex, out string reason)
    {
        attackBab = 0;
        stepIndex = -1;
        if (!HasGrappleNaturalAttackLadderStep(out reason))
            return false;

        return TryCommitManeuverSubstituteStep(naturalAttackIndex, out attackBab, out stepIndex, out reason, isHasteExtraAttack);
    }

    /// <summary>
    /// The one ladder rule of a grapple natural attack (PHB p.156; owner interpretation 2026-10-11, CMB-146): it takes
    /// the place of one of the creature's iterative attacks, so it needs a step left on the BAB ladder. Read before any
    /// step is spent by GameManager.TryConsumeGrappleAttackAction (the PC and AI path) and <see cref="TryCommitGrappleNaturalAttackStep"/>.
    /// </summary>
    public bool HasGrappleNaturalAttackLadderStep(out string reason)
    {
        if (GetRemainingMainHandAttackSteps(AttackStepKind.MainHand) > 0)
        {
            reason = string.Empty;
            return true;
        }

        reason = "no attack is left on the BAB ladder this turn (a grapple attack takes the place of one of the creature's iterative attacks, PHB p.156)";
        return false;
    }

    /// <summary>One natural attack a grapple attack action can make: a natural-sequence index, and whether it is Haste's extra attack with that weapon.</summary>
    public struct GrappleNaturalAttackOption
    {
        public int NaturalAttackIndex;
        public bool IsHasteExtraAttack;

        public static GrappleNaturalAttackOption None => new GrappleNaturalAttackOption { NaturalAttackIndex = -1 };
    }

    /// <summary>
    /// True when this creature's grapple attack (the "Attack Unarmed" grapple action) is a natural attack: it fights
    /// with its innate natural attacks (<see cref="UsesInnateNaturalAttackSequence"/>, no main weapon). A creature
    /// holding a weapon makes an unarmed strike (or uses its light weapon) instead.
    /// </summary>
    public bool UsesNaturalAttacksForGrappleAttack() => UsesInnateNaturalAttackSequence() && Stats != null && Stats.GetTotalNaturalAttackCount() > 0;

    /// <summary>
    /// The natural attacks a grapple attack action can make now, in sequence order: every natural attack not made or
    /// given up this turn (<see cref="AttackPool.IsNaturalAttackUsed"/>, or <paramref name="alsoUsed"/> for a caller
    /// that keeps its own record, the PC natural-attack buttons), then, while Haste's extra natural attack is unused,
    /// each used one again as the Haste attack (CMB-106). Empty for a creature that does not use natural attacks.
    /// </summary>
    public List<GrappleNaturalAttackOption> GetGrappleNaturalAttackOptions(Func<int, bool> alsoUsed = null)
    {
        var options = new List<GrappleNaturalAttackOption>();
        if (!UsesNaturalAttacksForGrappleAttack())
            return options;

        int count = Stats.GetTotalNaturalAttackCount();
        var used = new List<int>();
        for (int i = 0; i < count; i++)
        {
            if (ProgressiveAttackPool.IsNaturalAttackUsed(i) || (alsoUsed != null && alsoUsed(i)))
                used.Add(i);
            else
                options.Add(new GrappleNaturalAttackOption { NaturalAttackIndex = i, IsHasteExtraAttack = false });
        }

        if (CanUseHasteExtraNaturalAttack())
        {
            for (int i = 0; i < used.Count; i++)
                options.Add(new GrappleNaturalAttackOption { NaturalAttackIndex = used[i], IsHasteExtraAttack = true });
        }

        return options;
    }

    /// <summary>
    /// The option a grapple attack uses when nobody chose: the highest attack bonus, then the higher average damage,
    /// then the first (an unused attack before a Haste one). <see cref="GrappleNaturalAttackOption.None"/> when empty.
    /// </summary>
    public GrappleNaturalAttackOption GetDefaultGrappleNaturalAttackOption(List<GrappleNaturalAttackOption> options)
    {
        GrappleNaturalAttackOption best = GrappleNaturalAttackOption.None;
        if (options == null || Stats == null)
            return best;

        int bestBonus = int.MinValue;
        float bestDamage = float.MinValue;
        for (int i = 0; i < options.Count; i++)
        {
            NaturalAttackDefinition natural = Stats.GetNaturalAttackAtSequenceIndex(options[i].NaturalAttackIndex);
            if (natural == null)
                continue;

            int bonus = Stats.GetNaturalAttackBonus(natural);
            float damage = GetNaturalAttackAverageDamage(natural);
            if (bonus > bestBonus || (bonus == bestBonus && damage > bestDamage))
            {
                best = options[i];
                bestBonus = bonus;
                bestDamage = damage;
            }
        }

        return best;
    }

    /// <summary>
    /// One natural attack against <paramref name="opponent"/> by a grapple attack action (see the rule above): the
    /// natural attack at <paramref name="naturalAttackIndex"/>, rolled through <see cref="FullAttack"/> at its normal
    /// bonus, so every BuildAttackBonus term and its on-hit riders apply. <paramref name="isHasteExtraAttack"/> marks it
    /// as Haste's extra attack in the log; the step commit has already marked the Haste attack (or this natural
    /// attack) used. The rake is a separate grapple action (<see cref="ResolveGrappleRake"/>, CMB-144).
    /// </summary>
    public SpecialAttackResult ResolveGrappleNaturalAttack(CharacterController opponent, int naturalAttackIndex, bool isHasteExtraAttack,
        string maneuverName = "Grapple Natural Attack")
    {
        if (opponent == null || opponent.Stats == null || Stats == null)
        {
            return new SpecialAttackResult
            {
                ManeuverName = maneuverName,
                Success = false,
                Log = "No valid grapple opponent."
            };
        }

        NaturalAttackDefinition natural = Stats.GetNaturalAttackAtSequenceIndex(naturalAttackIndex);
        if (natural == null || !UsesNaturalAttacksForGrappleAttack())
        {
            return new SpecialAttackResult
            {
                ManeuverName = maneuverName,
                Success = false,
                Log = $"{Stats.CharacterName} has no natural attack #{naturalAttackIndex + 1} to make while grappling."
            };
        }

        int targetHpBefore = opponent.Stats.CurrentHP;
        FullAttackResult single = FullAttack(opponent, isFlanking: false, flankingBonus: 0, flankingPartnerName: null,
            rangeInfo: null, startAttackIndex: naturalAttackIndex, maxAttacks: 1);
        CombatResult attack = single != null && single.Attacks != null && single.Attacks.Count > 0 ? single.Attacks[0] : null;
        string naturalName = string.IsNullOrWhiteSpace(natural.Name) ? "natural weapon" : natural.Name;
        if (attack == null)
        {
            return new SpecialAttackResult
            {
                ManeuverName = maneuverName,
                Success = false,
                Log = $"{Stats.CharacterName} cannot attack {opponent.Stats.CharacterName} with its {naturalName} while grappling."
            };
        }

        string label = single.AttackLabels != null && single.AttackLabels.Count > 0 ? single.AttackLabels[0] : naturalName;
        if (isHasteExtraAttack)
            label += " [Haste's extra attack]";

        var logLines = new List<string>
        {
            $"{Stats.CharacterName} attacks {opponent.Stats.CharacterName} with its {naturalName} while grappling: one natural attack in place of an attack (PHB p.156; MM p.314).",
            attack.GetAttackBreakdown(label),
            $"Target HP: {targetHpBefore} -> {opponent.Stats.CurrentHP}"
        };

        return new SpecialAttackResult
        {
            ManeuverName = maneuverName,
            Success = attack.Hit,
            CheckRoll = attack.DieRoll,
            CheckTotal = attack.TotalRoll,
            OpposedTotal = attack.TargetAC,
            DamageDealt = attack.Hit ? attack.FinalDamageDealt : 0,
            TargetKilled = opponent.Stats.IsDead,
            Log = string.Join("\n", logLines)
        };
    }

    // ----- Rake in a grapple (MM p.314; RAW by reading 2026-10-14, CMB-144: "extra" attacks need a full attack, PHB p.143) -----
    // A monster with rake gains (usually two) extra claw attacks it can use only against a grappled foe, not subject to
    // the -4 for attacking in a grapple. It must begin its turn grappling to rake: it cannot begin a grapple and rake
    // in the same turn (a pounce at the end of a charge is the exception, resolved by the charge itself, and its rakes need
    // no grapple: MM p.313 Pounce; delegated RAW reading 2026-10-14). The rake
    // comes with a full attack, so it makes the turn a full attack: the move action is spent if the turn was not one
    // already. Once a turn. The one rake rule for PCs (the grapple action menu) and the AI (AI_GrappleRestrictedTurn).

    /// <summary>Whether this creature can rake its grapple opponent now (see the rule above); <paramref name="reason"/> says why not.</summary>
    public bool CanRakeInGrapple(out string reason)
    {
        reason = string.Empty;
        string actorName = Stats != null ? Stats.CharacterName : name;
        if (Stats == null || !Stats.HasRake || Stats.GetRakeAttackDefinition() == null)
        {
            reason = $"{actorName} has no rake.";
            return false;
        }

        if (!TryGetGrappleState(out CharacterController opponent, out _, out bool isPinned, out _) || opponent == null)
        {
            reason = $"{actorName} is not grappling a foe to rake.";
            return false;
        }

        // Rake claws are used only against a grappled foe (MM p.314); a holder with one body part is not grappled (MM p.310).
        if (IsHeldByImprovedGrabHold(out CharacterController holder) && holder == opponent)
        {
            reason = $"{actorName} cannot rake {opponent.Stats.CharacterName}: a creature holding with one body part is not grappled, and a rake needs a grappled foe (MM p.310, p.314).";
            return false;
        }

        if (isPinned)
        {
            reason = $"{actorName} is pinned and cannot rake.";
            return false;
        }

        // A pinner's actions are limited while the pin's round lasts (PHB p.156, CMB-120).
        if (IsGrappleActionBlockedWhilePinning(GrappleActionType.Rake, out string pinnerReason))
        {
            reason = pinnerReason;
            return false;
        }

        if (!ProgressiveAttackPool.BeganTurnGrappling)
        {
            reason = $"{actorName} did not begin its turn grappling, so it cannot rake this turn (MM p.314).";
            return false;
        }

        if (ProgressiveAttackPool.RakeUsedThisTurn)
        {
            reason = $"{actorName} already raked this turn.";
            return false;
        }

        if (!ProgressiveAttackPool.IsFullAttack && !CanStillReachFullAttack())
        {
            reason = $"{actorName} cannot make a full attack this turn, and a rake needs one (MM p.314).";
            return false;
        }

        if (!ProgressiveAttackPool.HasStartedAttacking && (Actions == null || !Actions.HasStandardAction))
        {
            reason = $"{actorName} has no standard action left for a full attack.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Rakes the grappled foe (see the rule above): makes the turn a full attack (spending the standard action when no
    /// attack was made yet, and the move action), then the rake's claw attacks through <see cref="PerformRakeAttacks"/>.
    /// Refused, with nothing spent, when <see cref="CanRakeInGrapple"/> says no.
    /// </summary>
    public SpecialAttackResult ResolveGrappleRake()
    {
        if (!CanRakeInGrapple(out string reason))
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Rake",
                Success = false,
                Log = reason
            };
        }

        TryGetGrappleState(out CharacterController opponent, out _, out _, out _);
        if (!ProgressiveAttackPool.IsFullAttack)
        {
            if (!ProgressiveAttackPool.HasStartedAttacking)
                CommitStandardAction();
            Actions?.UseMoveAction();
            ProgressiveAttackPool.MarkFullAttackCommitted();
        }

        int targetHpBefore = opponent.Stats.CurrentHP;
        FullAttackResult rake = PerformRakeAttacks(opponent, isFlanking: false, flankingBonus: 0, flankingPartnerName: null);
        int rakeCount = rake != null && rake.Attacks != null ? rake.Attacks.Count : 0;
        var logLines = new List<string>
        {
            $"{Stats.CharacterName} rakes {opponent.Stats.CharacterName}: {rakeCount} extra claw attack(s) against the grappled foe, without the -4 for grappling; this turn is a full attack (MM p.314)."
        };

        int totalDamage = 0;
        bool anyHit = false;
        for (int i = 0; i < rakeCount; i++)
        {
            CombatResult rakeAttack = rake.Attacks[i];
            if (rakeAttack == null)
                continue;

            if (rakeAttack.Hit)
            {
                anyHit = true;
                totalDamage += rakeAttack.FinalDamageDealt;
            }

            string rakeLabel = rake.AttackLabels != null && i < rake.AttackLabels.Count ? rake.AttackLabels[i] : $"Rake {i + 1}";
            logLines.Add(rakeAttack.GetAttackBreakdown(rakeLabel));
        }

        logLines.Add($"Target HP: {targetHpBefore} -> {opponent.Stats.CurrentHP}");

        return new SpecialAttackResult
        {
            ManeuverName = "Rake",
            Success = anyHit,
            DamageDealt = totalDamage,
            TargetKilled = opponent.Stats.IsDead,
            Log = string.Join("\n", logLines)
        };
    }

    private SpecialAttackResult ResolveDisarmSmallObjectStub(CharacterController opponent, bool isPinned)
    {
        if (opponent == null || opponent.Stats == null)
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Disarm Small Object",
                Success = false,
                Log = "No valid grapple opponent."
            };
        }

        if (isPinned)
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Disarm Small Object",
                Success = false,
                Log = $"{Stats.CharacterName} is pinned and cannot disarm a small object."
            };
        }

        CharacterController pinnedTarget = GetPinnedOpponent();
        if (pinnedTarget == null || pinnedTarget != opponent)
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Disarm Small Object",
                Success = false,
                Log = "Disarm Small Object requires actively pinning the target."
            };
        }

        return new SpecialAttackResult
        {
            ManeuverName = "Disarm Small Object",
            Success = false,
            Log = $"{Stats.CharacterName} attempts to disarm a small object from {opponent.Stats.CharacterName}. Disarm Small Object action - Not yet implemented."
        };
    }

    private SpecialAttackResult ResolveDrawLightWeaponDuringGrappleStub()
    {
        // TODO: Implement Draw Light Weapon during grapple
        // - Check inventory for light weapons
        // - Draw weapon (move-equivalent action)
        // - Update equipped weapon slot
        // - Allow attacking with drawn weapon on subsequent turns
        // - Add detailed combat log message for the draw action result
        Debug.Log("Draw a Light Weapon - Not yet implemented");

        return new SpecialAttackResult
        {
            ManeuverName = "Draw a Light Weapon",
            Success = false,
            Log = "Draw a Light Weapon - Not yet implemented"
        };
    }

    private SpecialAttackResult ResolveRetrieveSpellComponentDuringGrappleStub()
    {
        // TODO: Implement Retrieve Spell Component during grapple
        // - Check if character is spellcaster
        // - Check if character has spell components in inventory
        // - Retrieve component (move-equivalent action)
        // - Make component available for next spell
        // - May require Concentration check or Escape Artist check
        // - Add detailed combat log message for the retrieve action result
        Debug.Log("Retrieve a Spell Component - Not yet implemented");

        return new SpecialAttackResult
        {
            ManeuverName = "Retrieve a Spell Component",
            Success = false,
            Log = "Retrieve a Spell Component - Not yet implemented"
        };
    }

    // ========== LIFECYCLE ==========

    /// <summary>
    /// Called when this character reaches -10 HP or lower.
    /// </summary>
    public void OnDeath()
    {
        string who = Stats != null ? Stats.CharacterName : gameObject.name;
        Debug.Log($"[DeathFlow][OnDeath] ENTER | who={who} | hp={(Stats != null ? Stats.CurrentHP : 0)} | alreadyProcessed={_hasProcessedDeath}");

        if (_hasProcessedDeath)
        {
            Debug.Log($"[DeathFlow][OnDeath] SKIP duplicate death processing for {who}.");
            return;
        }

        _hasProcessedDeath = true;

        // A slain druid in a wild shape reverts to her own form and stays dead (PHB p.263, p.37; CHR-020).
        if (WildShapeRules.EndWithoutAction(this, slain: true))
            GameManager.Instance?.CombatUI?.ShowCombatLog(CombatLogHelper.Info("🐾", $"{who} reverts to its own form as it dies."));

        if (_sr != null && DeadSprite != null)
            _sr.sprite = DeadSprite;

        // Break concentration on death (D&D 3.5e: concentration ends if killed/unconscious)
        var concMgr = Concentration;
        if (concMgr != null && concMgr.IsConcentrating)
        {
            concMgr.OnCharacterIncapacitated();
        }

        // Held touch charges are lost if the caster dies/unconscious.
        var spellComp = Spellcasting;
        if (spellComp != null && spellComp.HasHeldTouchCharge)
        {
            spellComp.ClearHeldTouchCharge("caster incapacitated");
        }

        ClearOwnedFeintWindowsAndIndicators();
        ClearIncomingFeintIndicators();
        ReleaseGrappleState("death");

        // Clean up Imbue with Spell Ability on death
        ImbueWithSpellAbilityManager.HandleDeath(this);

        Debug.Log($"[DeathFlow][OnDeath] EXIT | who={who} | hp={(Stats != null ? Stats.CurrentHP : 0)}");
    }

    public void ProcessPinnedDurationAtTurnEnd()
    {
        if (!TryGetGrappleLink(this, out GrappleLink link) || link == null || link.PinnedCharacter == null)
            return;

        if (link.PinMaintainer == null || link.PinMaintainer.Stats == null || link.PinMaintainer.Stats.IsDead)
        {
            CharacterController pinnedNoMaintainer = link.PinnedCharacter;
            ClearPinnedState(link);
            if (GameManager.Instance != null && GameManager.Instance.CombatUI != null && pinnedNoMaintainer != null && pinnedNoMaintainer.Stats != null)
                GameManager.Instance.CombatUI.ShowCombatLog(CombatLogHelper.Expired("⏱", $"Pin on {pinnedNoMaintainer.Stats.CharacterName} ends because the controlling grappler can no longer maintain it."));
            return;
        }

        // PHB p.156 (CMB-120): the pin held for 1 round. It ends at the end of the pinner's next turn unless the
        // pinner pinned again that turn; it also ends when the pinner's turn passed while it could not act.
        // Since batch 12 step 6 a skipped turn also runs StartNewTurn (BeginCombatantTurn), so IsPinRenewalDue
        // already turns true then; the could-not-act branch still covers a pinner that lost its ability to act
        // during the turn, or a turn end that runs without a new turn count.
        if (link.PinMaintainer != this)
            return;

        bool couldNotAct = !CanTakeTurnActions() || !CanTakeActions();
        if (!IsPinRenewalDue() && !couldNotAct)
            return;

        CharacterController pinned = link.PinnedCharacter;
        ClearPinnedState(link);
        if (GameManager.Instance != null && GameManager.Instance.CombatUI != null && pinned != null && pinned.Stats != null)
        {
            string why = couldNotAct
                ? $"{Stats.CharacterName} cannot act to hold it"
                : $"{Stats.CharacterName} did not pin again this turn";
            GameManager.Instance.CombatUI.ShowCombatLog(CombatLogHelper.Expired("⏱", $"Pin on {pinned.Stats.CharacterName} ends after 1 round ({why}). The grapple continues."));
        }
    }

    /// <summary>
    /// Reset turn flags and action economy.
    /// Power Attack and Rapid Shot settings persist between turns (player choice).
    /// Also resets Attacks of Opportunity counters for the new round.
    /// </summary>
    public void StartNewTurn()
    {
        SyncHPStateFromCurrentHP(emitLog: false);
        RestoreShieldBonusAfterShieldBash();

        _turnsStartedCount++;
        PruneExpiredFeintWindows();

        // D&D 3.5e PHB: Readied actions expire at the start of your next turn
        ClearReadiedCounterspell();

        HasMovedThisTurn = false;
        UpdateInvisibilityMovementState(false);
        HasTakenFiveFootStep = false;
        HasAttackedThisTurn = false;
        IsWithdrawing = false;
        IsFightingDefensively = false; // lasts until start of this character's next turn
        Actions.Reset();
        RefreshActionLimits();
        StrenuousDamagePending = false;
        ProgressiveAttackPool.Clear();
        // A rake needs the creature to begin its turn grappling (MM p.314, CMB-144).
        ProgressiveAttackPool.MarkTurnStartedGrappling(IsGrappling());
        AIManeuverMemory.Clear(); // AI stopgap (AI-060): per-turn maneuver memory
        ThreatSystem.ClearMovementOpportunities(this); // a new round of movement opportunities (PHB p.138, CMB-128)
        // Note: PowerAttackValue and RapidShotEnabled persist between turns
        // They are player-controlled and reset only when the player changes them

        // Reset AoO counters for the new round
        ThreatSystem.ResetAoOForTurn(this);

        // Reset per-round Phase 1 combat feat trackers
        if (Stats != null)
        {
            Stats.DeflectArrowsUsedThisRound = false;
            Stats.HasUsedMountedCombatThisRound = false;
            Stats.SpringAttackTarget = null;
            Stats.IsUsingSpringAttackMovement = false;
        }
    }

    // ========== SPECIAL ATTACK MANEUVERS ==========

    /// <summary>
    /// Resolves a special attack (see <see cref="ExecuteSpecialAttackCore"/>), then reports it to the
    /// inert <see cref="ScenarioHooks.ManeuverResolved"/> test hook. A trip that lands records the Improved
    /// Trip attack (<see cref="PrepareImprovedTripFollowUp"/>, PHB p.96) at the trip's bonus,
    /// <paramref name="tripAttackBonusOverride"/> (the bonus of the step the trip replaced; for a PC in a
    /// two-weapon round it already carries the main-hand penalty, GameManager.TryCommitMainHandManeuverStep);
    /// GameManager.HandleTripAftermath makes it after the trip's log and reactions.
    /// </summary>
    public SpecialAttackResult ExecuteSpecialAttack(
        SpecialAttackType type,
        CharacterController target,
        EquipSlot? disarmTargetSlot = null,
        int? disarmAttackBonusOverride = null,
        int? grappleAttackBonusOverride = null,
        int bullRushChargeBonusOverride = 0,
        ItemData disarmAttackerWeaponOverride = null,
        int? tripAttackBonusOverride = null,
        bool disarmUsedOffHand = false,
        int disarmDualWieldPenaltyForLog = 0,
        EquipSlot? sunderTargetSlot = null,
        int? sunderAttackBonusOverride = null,
        ItemData sunderAttackerWeaponOverride = null,
        bool sunderUsedOffHand = false,
        int sunderDualWieldPenaltyForLog = 0)
    {
        // A combat maneuver against a creature is an obvious threat to it: it breaks a bard's fascination at once (PHB
        // p.29) and is a hostile action against it. Here, past the initiation AoOs, the PC wrapper and the NPC executor
        // send it identically (delegated RAW reading 2026-10-14, CHR-026). Waking an ally and turning undead are not
        // maneuvers against the target.
        if (GameManager.Instance != null && target != null
            && type != SpecialAttackType.WakeSleepingAlly && type != SpecialAttackType.TurnUndead)
            GameManager.Instance.BreakFascinationOnHostileAction(this, target, GameManager.FascinationThreatManeuver);

        SpecialAttackResult result = ExecuteSpecialAttackCore(type, target,
            disarmTargetSlot, disarmAttackBonusOverride, grappleAttackBonusOverride,
            bullRushChargeBonusOverride, disarmAttackerWeaponOverride, tripAttackBonusOverride,
            disarmUsedOffHand, disarmDualWieldPenaltyForLog,
            sunderTargetSlot, sunderAttackBonusOverride, sunderAttackerWeaponOverride,
            sunderUsedOffHand, sunderDualWieldPenaltyForLog);
        ScenarioHooks.ManeuverResolved?.Invoke(this, target, type, result);
        if (type == SpecialAttackType.Trip)
            PrepareImprovedTripFollowUp(result, tripAttackBonusOverride, freeTripTrigger: null);
        return result;
    }

    private SpecialAttackResult ExecuteSpecialAttackCore(
        SpecialAttackType type,
        CharacterController target,
        EquipSlot? disarmTargetSlot,
        int? disarmAttackBonusOverride,
        int? grappleAttackBonusOverride,
        int bullRushChargeBonusOverride,
        ItemData disarmAttackerWeaponOverride,
        int? tripAttackBonusOverride,
        bool disarmUsedOffHand,
        int disarmDualWieldPenaltyForLog,
        EquipSlot? sunderTargetSlot,
        int? sunderAttackBonusOverride,
        ItemData sunderAttackerWeaponOverride,
        bool sunderUsedOffHand,
        int sunderDualWieldPenaltyForLog)
    {
        if (target == null || target.Stats == null)
        {
            return new SpecialAttackResult
            {
                ManeuverName = type.ToString(),
                Success = false,
                Log = $"{Stats.CharacterName} cannot perform {type}: no valid target."
            };
        }

        // The initiation AoO step can kill the target (e.g. Fire Shield retribution on its AoO
        // hit); the shared resolver then does nothing, for PC and NPC callers alike.
        if (target.IsDead || target.Stats.IsDead)
        {
            return new SpecialAttackResult
            {
                ManeuverName = type.ToString(),
                Success = false,
                Log = $"{Stats.CharacterName} cannot perform {type}: {target.Stats.CharacterName} is dead."
            };
        }

        // A holder with one body part makes no attack on the creature it holds (MM p.310; delegated RAW reading
        // 2026-10-14, CMB-163). The PC and NPC callers refuse it before any cost; this keeps the resolver on the rule.
        if (IsBarredFromSpecialAttackOn(type, target, out string holdReason))
        {
            return new SpecialAttackResult
            {
                ManeuverName = type.ToString(),
                Success = false,
                Log = holdReason
            };
        }

        if (!CanPerformSpecialAttack(type))
        {
            string attackerName = Stats != null ? Stats.CharacterName : name;
            string maneuverName = type.ToString();
            string weaponType = GetPrimaryWeaponType();
            Debug.LogWarning($"[SpecialAttack][Validation] {attackerName} blocked from using {maneuverName} while weapon mode is {weaponType}.");
            return new SpecialAttackResult
            {
                ManeuverName = maneuverName,
                Success = false,
                Log = $"{attackerName} cannot perform {maneuverName} while wielding a ranged-only loadout."
            };
        }

        bool breaksInvisibility = type == SpecialAttackType.Trip
                                 || type == SpecialAttackType.Disarm
                                 || type == SpecialAttackType.Grapple
                                 || type == SpecialAttackType.Sunder
                                 || type == SpecialAttackType.BullRushAttack
                                 || type == SpecialAttackType.BullRushCharge
                                 || type == SpecialAttackType.Overrun
                                 || type == SpecialAttackType.CoupDeGrace;

        if (breaksInvisibility && target != null && target.Team != Team)
            BreakInvisibility("attack action", target);

        switch (type)
        {
            case SpecialAttackType.Trip: return ResolveTrip(target, tripAttackBonusOverride);
            case SpecialAttackType.Disarm: return ResolveDisarm(target, disarmTargetSlot, disarmAttackBonusOverride, disarmAttackerWeaponOverride, disarmUsedOffHand, disarmDualWieldPenaltyForLog);
            case SpecialAttackType.Grapple: return ResolveGrapple(target, grappleAttackBonusOverride);
            case SpecialAttackType.Sunder: return ResolveSunder(target, sunderTargetSlot, sunderAttackBonusOverride, sunderAttackerWeaponOverride, sunderUsedOffHand, sunderDualWieldPenaltyForLog);
            // Bull rush is an opposed Strength check; BAB does not enter it (PHB p.154, CMB-014).
            case SpecialAttackType.BullRushAttack:
                return ResolveBullRush(target, chargeBonus: 0);
            case SpecialAttackType.BullRushCharge:
                return ResolveBullRush(target, chargeBonus: bullRushChargeBonusOverride == 0 ? 2 : bullRushChargeBonusOverride);
            case SpecialAttackType.Overrun: return ResolveOverrun(target, defenderBlocks: true);
            case SpecialAttackType.Feint: return ResolveFeint(target);
            case SpecialAttackType.CoupDeGrace: return ResolveCoupDeGrace(target);
            case SpecialAttackType.TurnUndead:
                return new SpecialAttackResult
                {
                    ManeuverName = "Turn Undead",
                    Success = false,
                    Log = "Turn Undead is resolved by GameManager and does not target a single creature."
                };
            default:
                return new SpecialAttackResult
                {
                    ManeuverName = type.ToString(),
                    Success = false,
                    Log = $"{Stats.CharacterName} tries an unknown maneuver."
                };
        }
    }

    private bool IsBlockedBySummonedContactBarrier(CharacterController target, out AlignmentProtectionBenefits protection)
    {
        protection = default(AlignmentProtectionBenefits);

        if (target == null || target.Stats == null || Stats == null)
            return false;

        if (GameManager.Instance == null || !GameManager.Instance.IsSummonedCreature(this))
            return false;

        protection = AlignmentProtectionRules.GetBenefitsAgainst(target, Stats.CharacterAlignment);
        return protection.HasMatch && protection.BlocksSummonedContact;
    }

    private SpecialAttackResult BuildSummonedContactBarrierResult(CharacterController target, string maneuverName)
    {
        string attackerName = Stats != null ? Stats.CharacterName : name;
        string defenderName = target != null && target.Stats != null ? target.Stats.CharacterName : "target";

        return new SpecialAttackResult
        {
            ManeuverName = maneuverName,
            Success = false,
            Log = $"Protection barrier prevents {attackerName} from making bodily contact with {defenderName}. {maneuverName} automatically fails!"
        };
    }

    // ========== SHARED OPPOSED-MANEUVER MATH (CMB-014) ==========
    // PHB p.154-158. Trip, bull rush and overrun are opposed Strength checks with the special
    // size modifier (+4 per category above Medium, -4 per category below; p.156) and a +4
    // stability bonus for the defender. Grapple checks use BAB + STR + special size. Disarm and
    // sunder are opposed attack rolls. Ties follow the opposed-check rule (PHB p.64, and p.156
    // for grapple): the higher modifier wins, then both roll again.

    /// <summary>PHB p.156 special size modifier, used by grapple, bull rush, overrun and trip.</summary>
    public int GetSpecialSizeModifier()
    {
        return GetGrappleSizeModifier();
    }

    /// <summary>
    /// +4 when resisting a bull rush, overrun or trip for a creature that has more than two legs or
    /// is otherwise exceptionally stable (PHB p.154, p.157, p.158). Stable comes from creature data
    /// (<see cref="CharacterStats.IsExceptionallyStable"/>, set from the MM for NPCs, summons and
    /// NPC dwarves) or racial stability (<see cref="RaceData.StabilityBonus"/>, PC dwarves). The
    /// bonus is a single +4, never doubled. No bonus for a rider: dwarf stability needs the creature
    /// on the ground (PHB p.15), and a rider is not standing on its own feet, so no creature gets
    /// stability while riding (the mount keeps its own). Mounting is unreachable in play (CMB-031).
    /// Flying and climbing are not tracked (CMB-111).
    /// </summary>
    public int GetManeuverStabilityBonus()
    {
        if (Stats == null)
            return 0;

        bool stable = Stats.IsExceptionallyStable
            || (Stats.Race != null && Stats.Race.StabilityBonus > 0);
        if (!stable)
            return 0;

        if (MountSystem.IsMounted(this))
            return 0;

        return 4;
    }

    /// <summary>Strength-check modifier for the creature attempting a trip (PHB p.158).</summary>
    public int GetTripAttackerCheckModifier()
    {
        if (Stats == null)
            return 0;

        return Stats.STRMod + GetSpecialSizeModifier() + Stats.ConditionAbilityCheckModifier
            + (Stats.HasFeat("Improved Trip") ? 4 : 0);
    }

    /// <summary>Check modifier for resisting a trip or overrun: the better of STR and DEX (PHB p.157-158).</summary>
    public int GetTripOrOverrunDefenderCheckModifier()
    {
        if (Stats == null)
            return 0;

        return Mathf.Max(Stats.STRMod, Stats.DEXMod) + GetSpecialSizeModifier() + GetManeuverStabilityBonus()
            + Stats.ConditionAbilityCheckModifier;
    }

    /// <summary>Strength-check modifier for the creature attempting an overrun (PHB p.157).</summary>
    public int GetOverrunAttackerCheckModifier(int chargeBonus = 0)
    {
        if (Stats == null)
            return 0;

        return Stats.STRMod + GetSpecialSizeModifier() + Stats.ConditionAbilityCheckModifier + chargeBonus
            + (Stats.HasFeat("Improved Overrun") ? 4 : 0);
    }

    /// <summary>Melee touch attack modifier used to start a trip or a grapple.</summary>
    public int GetManeuverMeleeTouchAttackModifier(int? baseAttackBonusOverride = null)
    {
        if (Stats == null)
            return 0;

        int bab = baseAttackBonusOverride ?? Stats.BaseAttackBonus;
        return bab + Stats.STRMod + Stats.SizeModifier + Stats.ConditionAttackPenalty + FlurryAttackRollPenalty;
    }

    /// <summary>A natural 20 always hits and a natural 1 always misses (PHB p.134).</summary>
    public static bool IsManeuverTouchAttackHit(int naturalRoll, int total, int touchArmorClass)
    {
        if (naturalRoll >= 20)
            return true;
        if (naturalRoll <= 1)
            return false;
        return total >= touchArmorClass;
    }

    /// <summary>
    /// Opposed check winner (PHB p.64, p.156): the higher result wins; on a tie the higher
    /// modifier wins; if the modifiers are equal both sides roll again.
    /// </summary>
    public static bool DoesAttackerWinOpposedCheck(int attackerTotal, int attackerModifier, int defenderTotal, int defenderModifier)
    {
        return DoesAttackerWinOpposedCheck(attackerTotal, attackerModifier, defenderTotal, defenderModifier,
            () => DiceService.D20("Opposed check tie reroll"));
    }

    /// <summary>Testable overload: <paramref name="rollD20"/> supplies the tie-break rerolls.</summary>
    public static bool DoesAttackerWinOpposedCheck(int attackerTotal, int attackerModifier, int defenderTotal, int defenderModifier, Func<int> rollD20)
    {
        if (attackerTotal != defenderTotal)
            return attackerTotal > defenderTotal;

        if (attackerModifier != defenderModifier)
            return attackerModifier > defenderModifier;

        // Equal modifiers: roll again until the tie breaks (bounded as a safeguard).
        for (int i = 0; i < 100 && rollD20 != null; i++)
        {
            int attackerReroll = rollD20();
            int defenderReroll = rollD20();
            if (attackerReroll != defenderReroll)
                return attackerReroll > defenderReroll;
        }

        return false;
    }

    /// <summary>
    /// Free trip after a hit (MM trip, e.g. a wolf's bite): no touch attack and no attack of
    /// opportunity. The opposed Strength check is the normal one, the trip size limit applies (PHB p.158),
    /// and the opponent cannot react to trip back (the MM trip entries, for example the wolf, MM p.283).
    /// With Improved Trip a trip that lands records the feat's attack (PHB p.96): the attack that hit,
    /// <paramref name="triggeringHit"/>, made again (<see cref="PrepareImprovedTripFollowUp"/>). The caller runs
    /// GameManager.HandleTripAftermath after the trip's log and reactions to make it.
    /// </summary>
    public SpecialAttackResult ResolveFreeTripAttempt(CharacterController target, CombatResult triggeringHit = null)
    {
        SpecialAttackResult result = ResolveTrip(target, attackBonusOverride: null, freeTripAfterHit: true);
        PrepareImprovedTripFollowUp(result, null,
            triggeringHit ?? new CombatResult { BreakdownBAB = Stats != null ? Stats.BaseAttackBonus : 0 });
        return result;
    }

    // ========== TRIP LEGALITY, COUNTER-TRIP AND IMPROVED TRIP (PHB p.158, p.96; CMB-079) ==========

    /// <summary>
    /// Shared trip legality for PCs and NPCs (PHB p.158, CMB-079). Spends nothing. Refuses when the target
    /// is missing, dead or this creature; this creature is a swarm (swarms make no trip attempts here) or
    /// the target is a swarm (MM p.316); either side is incorporeal (MM p.311: an incorporeal creature
    /// cannot trip or be tripped); or the target is more than one size category larger than this creature
    /// (PHB p.158). Checked by the PC menu and wrapper and the NPC executor before any cost, by the AI's trip
    /// choices, by <see cref="ResolveTrip"/> itself (free trips included), and from the defender's side for
    /// a counter-trip (<see cref="CanCounterTrip"/>).
    /// </summary>
    public bool CanTrip(CharacterController target, out string reason)
    {
        reason = null;
        if (target == null || target.Stats == null || target == this || Stats == null)
        {
            reason = "no valid target";
            return false;
        }

        if (target.IsDead || target.Stats.IsDead)
        {
            reason = $"{target.Stats.CharacterName} is dead";
            return false;
        }

        if (Stats.IsSwarm)
        {
            reason = $"{Stats.CharacterName} cannot make trip attempts while in swarm form";
            return false;
        }

        if (target.Stats.IsSwarm)
        {
            reason = $"{target.Stats.CharacterName} is a swarm and cannot be tripped";
            return false;
        }

        if (IsIncorporeal)
        {
            reason = $"{Stats.CharacterName} is incorporeal and cannot trip";
            return false;
        }

        if (target.IsIncorporeal)
        {
            reason = $"{target.Stats.CharacterName} is incorporeal and cannot be tripped";
            return false;
        }

        // SizeCategory runs Fine..Colossal: the target may be at most one category larger (PHB p.158).
        if ((int)target.GetCurrentSizeCategory() - (int)GetCurrentSizeCategory() > 1)
        {
            reason = $"{target.Stats.CharacterName} is more than one size category larger";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Whether this creature, the defender of a trip that just failed at the opposed check, may try to trip
    /// the tripper back (PHB p.158). The rule part only; whether it wants to is the defender's choice (the PC
    /// prompt or the AI, GameManager.HandleTripAftermath). Any defender may react, whatever its side: the
    /// trip rule names no side, so an ally of the tripper (one that a confused or charmed creature tripped) is
    /// asked too, and the decision layer chooses (owner ruling 2026-10-08, CMB-136). Refused when either side
    /// is down; this creature is unconscious, dying, stable or otherwise unable to act (an HP state that cannot
    /// act, a helpless condition, or a condition that prevents attacks such as stunned or nauseated); it is
    /// grappling (PHB p.156 limits a grappler to grapple actions); the tripper is already prone; the trip would
    /// be illegal from this side (<see cref="CanTrip"/>: size, swarm, incorporeal); or a protection barrier
    /// keeps this summoned creature from touching the tripper. A conscious creature at 0 HP or below (disabled,
    /// PHB p.145, or below 0 with Diehard) may trip back: the counter-trip is a reaction, not an action, so the
    /// disabled limit on actions does not apply and it costs no hit point (owner ruling 2026-10-08, CMB-136).
    /// </summary>
    public bool CanCounterTrip(CharacterController tripper, out string reason)
    {
        reason = null;
        if (Stats == null || IsDead || Stats.IsDead)
        {
            reason = "the defender is down";
            return false;
        }

        if (tripper == null || tripper.Stats == null || tripper.IsDead || tripper.Stats.IsDead)
        {
            reason = "no tripper to trip back";
            return false;
        }

        // Unconscious, dying and stable creatures are out (IsUnconscious, CanTakeTurnActions); a disabled one
        // (HPState.Disabled: 0 HP, or below 0 with Diehard) is conscious and may react.
        if (IsUnconscious || !CanTakeTurnActions() || !CanAttack() || IsHelplessLikeConditionState())
        {
            reason = $"{Stats.CharacterName} cannot act";
            return false;
        }

        if (IsGrappling())
        {
            reason = $"{Stats.CharacterName} is grappling";
            return false;
        }

        if (tripper.HasCondition(CombatConditionType.Prone))
        {
            reason = $"{tripper.Stats.CharacterName} is already prone";
            return false;
        }

        if (!CanTrip(tripper, out reason))
            return false;

        if (IsBlockedBySummonedContactBarrier(tripper, out _))
        {
            reason = $"a protection barrier keeps {Stats.CharacterName} from touching {tripper.Stats.CharacterName}";
            return false;
        }

        return true;
    }

    /// <summary>
    /// The counter-trip (PHB p.158): after <paramref name="tripper"/>'s trip against this creature failed at the
    /// opposed check, this creature makes a Strength check opposed by the tripper's Dexterity or Strength check
    /// (the better modifier): the same opposed check as a trip (<see cref="GetTripAttackerCheckModifier"/>, so
    /// Improved Trip's +4 counts, against <see cref="GetTripOrOverrunDefenderCheckModifier"/>, with the special
    /// size modifiers and the tripper's stability), with the roles swapped. No touch attack and no attack of
    /// opportunity; it is an attack for invisibility and Sanctuary. On a win the tripper is prone. Raises
    /// <see cref="ScenarioHooks.ManeuverResolved"/> as a trip by this creature with
    /// <see cref="SpecialAttackResult.IsCounterTrip"/> set. The caller asks the defender first
    /// (GameManager.HandleTripAftermath, which also runs the contact side effects). A counter-trip that lands
    /// records Improved Trip's attack (PHB p.96) at this creature's best melee bonus
    /// (<see cref="PrepareImprovedTripFollowUp"/>; owner interpretation 2026-10-11, CMB-134), made by
    /// GameManager.ResolveCounterTripChoice after the counter-trip's contact side effects.
    /// </summary>
    public SpecialAttackResult ResolveCounterTrip(CharacterController tripper)
    {
        var result = new SpecialAttackResult
        {
            ManeuverName = "Counter-trip",
            IsCounterTrip = true,
            AttackerActionConsumed = false
        };

        if (!CanCounterTrip(tripper, out string reason))
        {
            result.Log = $"{(Stats != null ? Stats.CharacterName : name)} cannot trip back: {reason}.";
            return result;
        }

        // Invisibility ends when the subject attacks any creature (PHB p.245), an ally included: since the owner
        // ruling of 2026-10-08 (CMB-136) the tripper may be on this creature's side.
        BreakInvisibility("attack action", tripper);
        CombatFlowService.BreakProtectiveWardsOnAttack(this);

        int atkRoll = DiceService.D20("Counter-trip check");
        int defRoll = DiceService.D20("Counter-trip resist check");
        int atkModifier = GetTripAttackerCheckModifier();
        int defModifier = tripper.GetTripOrOverrunDefenderCheckModifier();
        int atkTotal = atkRoll + atkModifier;
        int defTotal = defRoll + defModifier;

        bool success = DoesAttackerWinOpposedCheck(atkTotal, atkModifier, defTotal, defModifier);
        if (success)
            tripper.ApplyCondition(CombatConditionType.Prone, -1, Stats.CharacterName);

        string checkLine = $"Str check {atkTotal} (d20 {atkRoll} {CharacterStats.FormatMod(atkModifier)}) vs "
            + $"{tripper.Stats.CharacterName}'s Str/Dex check {defTotal} (d20 {defRoll} {CharacterStats.FormatMod(defModifier)})"
            + (atkTotal == defTotal ? ", tie broken by modifier or reroll" : string.Empty);

        result.Success = success;
        result.CheckRoll = atkRoll;
        result.CheckTotal = atkTotal;
        result.OpposedRoll = defRoll;
        result.OpposedTotal = defTotal;
        result.Log = success
            ? $"{Stats.CharacterName} trips {tripper.Stats.CharacterName} back! {checkLine} → PRONE (until standing)."
            : $"{Stats.CharacterName} tries to trip {tripper.Stats.CharacterName} back and fails. {checkLine}.";

        PrepareImprovedTripFollowUp(result, null, freeTripTrigger: null);
        ScenarioHooks.ManeuverResolved?.Invoke(this, tripper, SpecialAttackType.Trip, result);
        return result;
    }

    /// <summary>
    /// Chance that the side with <paramref name="attackerModifier"/> wins an opposed d20 check against
    /// <paramref name="defenderModifier"/> (PHB p.64: the higher result wins, a tie goes to the higher
    /// modifier, and with equal modifiers both roll again, counted as half). Exact over the 400 roll pairs.
    /// Used by the counter-trip prompt and the AI's counter-trip choice.
    /// </summary>
    public static float EstimateOpposedCheckWinChance(int attackerModifier, int defenderModifier)
    {
        int halfWins = 0;
        for (int a = 1; a <= 20; a++)
        {
            for (int d = 1; d <= 20; d++)
            {
                int attackerTotal = a + attackerModifier;
                int defenderTotal = d + defenderModifier;
                if (attackerTotal > defenderTotal)
                    halfWins += 2;
                else if (attackerTotal == defenderTotal)
                    halfWins += attackerModifier > defenderModifier ? 2 : (attackerModifier == defenderModifier ? 1 : 0);
            }
        }

        return halfWins / 800f;
    }

    /// <summary>
    /// Improved Trip (PHB p.96), first half: after a trip by this creature lands, records on
    /// <paramref name="result"/> the attack the feat grants, without making it. The attack is the one the trip
    /// replaced, "as if you hadn't used your attack for the trip attempt": <paramref name="tripAttackBonus"/> is
    /// that step's bonus (for a PC in a two-weapon round it already includes the main-hand penalty, added by
    /// GameManager.TryCommitMainHandManeuverStep); a natural step is the natural attack the trip gave up
    /// (<see cref="AttackPool.LastSubstituteNaturalAttackIndex"/>). After a free trip (MM trip on a hit,
    /// <paramref name="freeTripTrigger"/> set) the attack that hit is made again. A counter-trip (PHB p.158) used no
    /// attack, so the feat's "as if you hadn't used your attack" has no attack to point at; owner interpretation
    /// 2026-10-11 (CMB-134): the attack is made at this creature's best melee bonus, full BAB with a weapon or an
    /// unarmed strike, or its best natural attack (<see cref="GetDefaultHasteNaturalAttackIndex"/>: the highest
    /// bonus). GameManager.HandleTripAftermath (GameManager.ResolveCounterTripChoice for a counter-trip) makes the
    /// attack (<see cref="ResolveImprovedTripFollowUp"/>) after the trip's own log and melee reactions.
    /// </summary>
    private void PrepareImprovedTripFollowUp(SpecialAttackResult result, int? tripAttackBonus, CombatResult freeTripTrigger)
    {
        if (result == null || !result.Success || Stats == null || !Stats.HasFeat("Improved Trip"))
            return;

        result.ImprovedTripFollowUpPending = true;
        if (result.IsCounterTrip)
        {
            result.FollowUpAttackBonus = Stats.BaseAttackBonus;
            result.FollowUpTrigger = null;
            result.FollowUpNaturalAttackIndex = UsesInnateNaturalAttackSequence() ? GetDefaultHasteNaturalAttackIndex() : -1;
            return;
        }

        result.FollowUpAttackBonus = tripAttackBonus;
        result.FollowUpTrigger = freeTripTrigger;
        result.FollowUpNaturalAttackIndex = !UsesInnateNaturalAttackSequence()
            ? -1
            : freeTripTrigger != null
                ? FindNaturalAttackSequenceIndex(freeTripTrigger.WeaponName)
                : ProgressiveAttackPool.LastSubstituteNaturalAttackIndex;
    }

    /// <summary>What the Improved Trip attack is made with (PHB p.96; owner ruling 2026-10-08, CMB-136).</summary>
    public enum ImprovedTripAttackSource
    {
        None,
        /// <summary>A creature fighting with its innate natural attacks: the natural attack the trip gave up.</summary>
        NaturalSequence,
        /// <summary>The main weapon.</summary>
        MainWeapon,
        /// <summary>The off-hand weapon (a left-hand weapon, a shield bash or a spiked gauntlet), when the main weapon cannot attack.</summary>
        OffHandWeapon,
        /// <summary>An unarmed strike: the attacker holds no weapon, or none it holds can attack the opponent.</summary>
        UnarmedStrike
    }

    /// <summary>
    /// Which attack Improved Trip's "melee attack against that opponent" (PHB p.96) is made with, for PCs and NPCs
    /// alike (owner ruling 2026-10-08, CMB-136). Nothing while the opponent is down or this creature is down or cannot
    /// attack (<paramref name="note"/> says why when it is this creature). A creature fighting with its innate natural
    /// attacks uses the natural attack the trip gave up, in its natural reach. Otherwise the main weapon when it can
    /// attack the opponent (a melee weapon that reaches it and can be used now); else another weapon this creature has
    /// that reaches it: the off-hand weapon (<see cref="GetOffHandAttackWeapon"/>), then an unarmed strike in natural
    /// reach. A natural attack beside a held weapon is not modelled (CMB-077) and the game has no armor spikes, so
    /// those are never chosen. A worn spiked gauntlet is chosen only as the off-hand weapon, so not while this creature
    /// two-hands its main weapon (<see cref="GetOffHandAttackWeapon"/> offers no off-hand weapon then); that case falls
    /// to the unarmed strike. When nothing reaches, no attack is made. A conscious tripper at 0 HP or below (disabled,
    /// or Diehard) still makes the attack: the feat grants it at once as part of the trip, as the counter-trip is
    /// allowed to a disabled defender (<see cref="CanCounterTrip"/>). Read by
    /// <see cref="ResolveImprovedTripFollowUp"/> and <see cref="DoesImprovedTripAttackProvoke"/>; spends nothing.
    /// </summary>
    public ImprovedTripAttackSource SelectImprovedTripAttack(CharacterController target, out ItemData weapon, out string note)
    {
        weapon = null;
        note = null;
        if (Stats == null || target == null || target.Stats == null || target.IsDead || target.Stats.IsDead)
            return ImprovedTripAttackSource.None;

        string selfName = Stats.CharacterName;
        if (IsDead || Stats.IsDead || IsUnconscious || !CanTakeTurnActions() || !CanAttack())
        {
            note = $"Improved Trip: {selfName} cannot make the follow-up attack.";
            return ImprovedTripAttackSource.None;
        }

        if (UsesInnateNaturalAttackSequence())
        {
            if (IsTargetInCurrentWeaponRange(target))
                return ImprovedTripAttackSource.NaturalSequence;

            note = $"Improved Trip: {target.Stats.CharacterName} is out of {selfName}'s melee reach, so no follow-up attack.";
            return ImprovedTripAttackSource.None;
        }

        ItemData main = GetEquippedMainWeapon();
        if (main != null)
        {
            if (CanMakeImprovedTripAttackWith(main, target))
            {
                weapon = main;
                return ImprovedTripAttackSource.MainWeapon;
            }

            ItemData offHand = GetOffHandAttackWeapon();
            if (offHand != null && offHand != main && CanMakeImprovedTripAttackWith(offHand, target))
            {
                weapon = offHand;
                return ImprovedTripAttackSource.OffHandWeapon;
            }
        }

        if (IsTargetInUnarmedReach(target) && CanAttackWithWeapon(null, out _))
            return ImprovedTripAttackSource.UnarmedStrike;

        note = $"Improved Trip: {selfName} has no weapon that can attack {target.Stats.CharacterName}, so no follow-up attack.";
        return ImprovedTripAttackSource.None;
    }

    /// <summary>A held melee weapon that reaches <paramref name="target"/> and can be used now (loaded, legal while grappled).</summary>
    private bool CanMakeImprovedTripAttackWith(ItemData weapon, CharacterController target)
    {
        return weapon != null
            && weapon.WeaponCat == WeaponCategory.Melee
            && IsTargetInWeaponRange(target, weapon, useThrownRange: false)
            && CanAttackWithWeapon(weapon, out _);
    }

    /// <summary>
    /// True when <paramref name="target"/> is within this creature's unarmed reach: its natural reach (PHB p.137;
    /// at least 5 feet), its own square included. A held reach weapon does not change it.
    /// </summary>
    public bool IsTargetInUnarmedReach(CharacterController target)
    {
        if (target == null || target.Stats == null || Stats == null)
            return false;

        int distance = GetMinimumDistanceToTargetSquares(target, chebyshev: true);
        return distance <= Mathf.Max(1, Stats.NaturalReachSquares);
    }

    /// <summary>
    /// PHB p.139 (Unarmed Attacks): an unarmed attack by this creature provokes an attack of opportunity from
    /// <paramref name="target"/>, the creature attacked, when this creature's unarmed attack is not "armed" and the
    /// target is armed (<see cref="IsArmedAgainstUnarmedAttacks"/>). The attacker's unarmed attack is armed when it has
    /// Improved Unarmed Strike (a monk has it) or claws, fangs or similar natural weapons
    /// (<see cref="HasNaturalPhysicalWeapons"/>), whatever it holds. Only the rule part; whether the target can make
    /// the AoO now (one left, it threatens this creature) is ThreatSystem's. Read for the Improved Trip attack
    /// (<see cref="DoesImprovedTripAttackProvoke"/>); the other unarmed attack paths do not check it yet (CMB-135).
    /// </summary>
    public bool DoesUnarmedAttackProvoke(CharacterController target)
    {
        if (Stats == null || target == null || target.Stats == null)
            return false;

        if (FeatManager.HasImprovedUnarmedStrike(Stats) || HasNaturalPhysicalWeapons())
            return false;

        return target.IsArmedAgainstUnarmedAttacks();
    }

    /// <summary>
    /// "Armed" in the PHB p.139 sense, for defense: this creature holds a melee weapon (a spiked gauntlet or a shield
    /// bash counts), has Improved Unarmed Strike, has claws, fangs or similar natural weapons
    /// (<see cref="HasNaturalPhysicalWeapons"/>), or holds the charge of a touch spell. An unarmed creature with none of
    /// these, or one holding only ranged weapons, is not armed. Whether it can actually make an AoO (it threatens, has
    /// one left) is ThreatSystem's.
    /// </summary>
    public bool IsArmedAgainstUnarmedAttacks()
    {
        if (Stats == null)
            return false;

        ItemData main = GetEquippedMainWeapon();
        if (main != null && main.WeaponCat == WeaponCategory.Melee)
            return true;

        ItemData offHand = GetOffHandAttackWeapon();
        if (offHand != null && offHand.WeaponCat == WeaponCategory.Melee)
            return true;

        if (FeatManager.HasImprovedUnarmedStrike(Stats) || HasNaturalPhysicalWeapons())
            return true;

        SpellcastingComponent spellcasting = Spellcasting;
        return spellcasting != null && spellcasting.HasHeldTouchCharge;
    }

    /// <summary>
    /// True when this creature has claws, fangs or similar natural physical weapons (PHB p.139): a natural attack that
    /// is not an unarmed strike, whether or not it holds a weapon now.
    /// </summary>
    public bool HasNaturalPhysicalWeapons()
    {
        if (Stats == null || Stats.NaturalAttacks == null)
            return false;

        foreach (NaturalAttackDefinition natural in Stats.NaturalAttacks)
        {
            if (natural != null && !natural.IsUnarmedStrike)
                return true;
        }

        return false;
    }

    /// <summary>
    /// True when the Improved Trip attack <paramref name="result"/> still holds would be an unarmed strike that
    /// provokes an attack of opportunity from <paramref name="target"/> (PHB p.139: the AoO comes before the
    /// attack). GameManager.HandleTripAftermath asks it before <see cref="ResolveImprovedTripFollowUp"/> and runs
    /// the target's AoO when ThreatSystem allows one.
    /// </summary>
    public bool DoesImprovedTripAttackProvoke(CharacterController target, SpecialAttackResult result)
    {
        if (result == null || !result.ImprovedTripFollowUpPending)
            return false;

        return SelectImprovedTripAttack(target, out _, out _) == ImprovedTripAttackSource.UnarmedStrike
            && DoesUnarmedAttackProvoke(target);
    }

    /// <summary>
    /// Improved Trip (PHB p.96), second half: makes the attack <see cref="PrepareImprovedTripFollowUp"/> recorded,
    /// once (the pending flag is cleared). Called by GameManager.HandleTripAftermath after the trip's own log and
    /// melee reactions (and after the target's AoO when the attack is an unarmed strike that provokes, PHB p.139), so
    /// a tripper those dropped makes no attack. The weapon is <see cref="SelectImprovedTripAttack"/>'s choice (owner
    /// ruling 2026-10-08, CMB-136): the main weapon when it can attack the tripped opponent, else the off-hand weapon
    /// (made as an off-hand attack, so a shield bash drops the shield's AC bonus, PHB p.125), else an unarmed strike,
    /// else none. A weapon or unarmed attack is one Attack at the recorded bonus (the bonus of
    /// the attack the trip replaced) with that weapon's own modifiers; a natural step is the natural attack the trip
    /// gave up, at its own bonus. After a free trip the attack that hit is made again: the same natural attack, or a
    /// weapon attack at that hit's BAB. The attack spends no step of the attack sequence. The attack goes in
    /// <see cref="SpecialAttackResult.FollowUpAttack"/>, a reason it was not made in
    /// <see cref="SpecialAttackResult.FollowUpNote"/>.
    /// </summary>
    public void ResolveImprovedTripFollowUp(CharacterController target, SpecialAttackResult result)
    {
        if (result == null || !result.ImprovedTripFollowUpPending)
            return;

        result.ImprovedTripFollowUpPending = false;
        ImprovedTripAttackSource source = SelectImprovedTripAttack(target, out ItemData weapon, out string note);
        if (source == ImprovedTripAttackSource.None)
        {
            result.FollowUpNote = note;
            return;
        }

        GameManager gm = GameManager.Instance;
        List<CharacterController> combatants = gm != null ? gm.GetAllCharactersForAI() : null;
        CharacterController flankPartner = null;
        bool isFlanking = combatants != null && CombatUtils.IsAttackerFlanking(this, target, combatants, out flankPartner);
        int flankBonus = isFlanking ? CombatUtils.FlankingAttackBonus : 0;
        string partnerName = flankPartner != null && flankPartner.Stats != null ? flankPartner.Stats.CharacterName : null;

        CombatResult attack = null;
        string label = null;
        if (source == ImprovedTripAttackSource.NaturalSequence)
        {
            int naturalIndex = result.FollowUpNaturalAttackIndex;
            if (naturalIndex < 0)
                naturalIndex = GetDefaultHasteNaturalAttackIndex();
            if (naturalIndex < 0)
                return;

            FullAttackResult naturalAttack = FullAttack(target, isFlanking, flankBonus, partnerName, null,
                startAttackIndex: naturalIndex, maxAttacks: 1);
            if (naturalAttack != null && naturalAttack.Attacks != null && naturalAttack.Attacks.Count > 0)
            {
                attack = naturalAttack.Attacks[0];
                label = naturalAttack.AttackLabels != null && naturalAttack.AttackLabels.Count > 0
                    ? naturalAttack.AttackLabels[0]
                    : "Natural attack";
            }
        }
        else
        {
            CombatResult trigger = result.FollowUpTrigger;
            int bab = result.FollowUpAttackBonus ?? (trigger != null ? trigger.BreakdownBAB : Stats.BaseAttackBonus);
            bool unarmed = source == ImprovedTripAttackSource.UnarmedStrike;
            // An off-hand weapon attacks as an off-hand attack: a shield bash loses the shield's AC bonus (PHB p.125)
            // and the off hand's half Strength to damage (PHB p.113) follows the Attack path (CMB-008).
            attack = Attack(target, isFlanking, flankBonus, partnerName, null, bab, unarmed ? null : weapon,
                isOffHandAttack: source == ImprovedTripAttackSource.OffHandWeapon,
                unarmedStrike: unarmed);
            ItemData main = GetEquippedMainWeapon();
            string why = source == ImprovedTripAttackSource.MainWeapon || main == null
                ? string.Empty
                : $"; {main.Name} cannot attack {target.Stats.CharacterName}";
            string hand = source == ImprovedTripAttackSource.OffHandWeapon ? ", off hand" : string.Empty;
            label = $"{(unarmed || weapon == null ? "Unarmed strike" : weapon.Name)} (BAB {CharacterStats.FormatMod(bab)}{hand}{why})";
        }

        if (attack == null)
            return;

        result.FollowUpAttack = attack;
        result.FollowUpLabel = $"Improved Trip attack: {label}";
    }

    /// <summary>Sequence index of the first natural attack named <paramref name="naturalAttackName"/>, or -1.</summary>
    private int FindNaturalAttackSequenceIndex(string naturalAttackName)
    {
        if (Stats == null || string.IsNullOrEmpty(naturalAttackName))
            return -1;

        int count = Stats.GetTotalNaturalAttackCount();
        for (int i = 0; i < count; i++)
        {
            NaturalAttackDefinition natural = Stats.GetNaturalAttackAtSequenceIndex(i);
            if (natural != null && natural.Name == naturalAttackName)
                return i;
        }

        return -1;
    }

    private SpecialAttackResult ResolveTrip(CharacterController target, int? attackBonusOverride = null, bool freeTripAfterHit = false)
    {
        // Shared legality (size, swarm, incorporeal; PHB p.158). Callers check it before any cost;
        // this keeps a free trip and any direct call inside the rule too.
        if (!CanTrip(target, out string tripReason))
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Trip",
                Success = false,
                Log = $"{(Stats != null ? Stats.CharacterName : name)} cannot trip: {tripReason}."
            };
        }

        if (IsBlockedBySummonedContactBarrier(target, out _))
            return BuildSummonedContactBarrierResult(target, "Trip");

        // PHB p.158 step 1: an unarmed melee touch attack (skipped for a free trip after a hit).
        // BAB enters only this touch attack; the trip itself is an opposed Strength check.
        string touchLine = string.Empty;
        if (!freeTripAfterHit)
        {
            int touchRoll = DiceService.D20("Trip touch attack");
            // Racial attack bonus against the target's kind and its racial dodge against giants apply (CHR-019).
            int touchModifier = GetManeuverMeleeTouchAttackModifier(attackBonusOverride) + RacialTraitRules.AttackBonusAgainst(Stats, target.Stats);
            int touchTotal = touchRoll + touchModifier;
            int touchAC = target.Stats.TouchArmorClass + RacialTraitRules.KindDodgeACBonusAgainst(target.Stats, Stats);
            bool touchHit = IsManeuverTouchAttackHit(touchRoll, touchTotal, touchAC);
            touchLine = $"Touch attack: d20 {touchRoll} {CharacterStats.FormatMod(touchModifier)} = {touchTotal} vs touch AC {touchAC}"
                + (touchRoll >= 20 ? " (natural 20)" : touchRoll <= 1 ? " (natural 1)" : string.Empty)
                + (touchHit ? " → hit." : " → miss.");

            if (!touchHit)
            {
                return new SpecialAttackResult
                {
                    ManeuverName = "Trip",
                    Success = false,
                    CheckRoll = touchRoll,
                    CheckTotal = touchTotal,
                    OpposedTotal = touchAC,
                    Log = $"{Stats.CharacterName} fails to trip {target.Stats.CharacterName}. {touchLine}"
                };
            }
        }

        int atkRoll = DiceService.D20("Trip Strength check");
        int defRoll = DiceService.D20("Trip defense check");
        int atkModifier = GetTripAttackerCheckModifier();
        int defModifier = target.GetTripOrOverrunDefenderCheckModifier();
        int atkTotal = atkRoll + atkModifier;
        int defTotal = defRoll + defModifier;

        bool success = DoesAttackerWinOpposedCheck(atkTotal, atkModifier, defTotal, defModifier);
        if (success)
            target.ApplyCondition(CombatConditionType.Prone, -1, Stats.CharacterName);

        string checkLine = $"Str check {atkTotal} (d20 {atkRoll} {CharacterStats.FormatMod(atkModifier)}) vs "
            + $"{target.Stats.CharacterName}'s Str/Dex check {defTotal} (d20 {defRoll} {CharacterStats.FormatMod(defModifier)})"
            + (atkTotal == defTotal ? ", tie broken by modifier or reroll" : string.Empty);
        string prefix = string.IsNullOrEmpty(touchLine) ? string.Empty : touchLine + " ";

        return new SpecialAttackResult
        {
            ManeuverName = "Trip",
            Success = success,
            CheckRoll = atkRoll,
            CheckTotal = atkTotal,
            OpposedRoll = defRoll,
            OpposedTotal = defTotal,
            // PHB p.158: a trip lost at the opposed check lets the defender try to trip back; a free
            // trip after a hit does not (MM trip: the opponent cannot react to trip). Whether the
            // defender wants to is its own choice (GameManager.HandleTripAftermath).
            CounterTripAllowed = !success && !freeTripAfterHit && target.CanCounterTrip(this, out _),
            Log = success
                ? $"{Stats.CharacterName} trips {target.Stats.CharacterName}! {prefix}{checkLine} → PRONE (until standing)."
                : $"{Stats.CharacterName} fails to trip {target.Stats.CharacterName}. {prefix}{checkLine}."
        };
    }

    private SpecialAttackResult ResolveDisarm(CharacterController target, EquipSlot? preferredTargetSlot, int? iterativeAttackBonusOverride = null, ItemData attackerWeaponOverride = null, bool usedOffHand = false, int attackerDualWieldPenaltyForLog = 0)
    {
        if (IsBlockedBySummonedContactBarrier(target, out _))
            return BuildSummonedContactBarrierResult(target, "Disarm");

        if (!TryGetDisarmTargetHeldItem(target, preferredTargetSlot, out ItemData targetHeldItem, out EquipSlot targetHeldItemSlot))
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Disarm",
                Success = false,
                Log = $"{target.Stats.CharacterName} has no held item to disarm."
            };
        }

        ItemData attackerHeldWeapon = attackerWeaponOverride ?? GetEquippedMainWeapon();

        bool defenderHasLockedGauntlet = HasLockedGauntletEquipped(target);
        int defenderLockedGauntletBonus = defenderHasLockedGauntlet ? 10 : 0;

        DisarmCheckResult primaryCheck = RollDisarmCheck(
            this,
            target,
            attackerHeldWeapon,
            targetHeldItem,
            targetHeldItemSlot,
            defenderLockedGauntletBonus,
            lockedGauntletReason: defenderHasLockedGauntlet ? "Locked Gauntlet" : string.Empty,
            attackerBaseAttackBonusOverride: iterativeAttackBonusOverride,
            attackerDualWieldPenaltyForLog: attackerDualWieldPenaltyForLog);

        bool success = primaryCheck.Success;
        string handLabel = usedOffHand ? "Off-Hand" : "Main Hand";
        string resultLabel = success ? "SUCCESS" : "FAILURE";

        var logLines = new List<string>
        {
            $"{Stats.CharacterName} attempts to disarm {target.Stats.CharacterName} ({handLabel})",
            primaryCheck.BreakdownLog,
            $"Result: {primaryCheck.AttackerTotal} vs {primaryCheck.DefenderTotal} - {resultLabel}!"
        };

        if (success)
        {
            ItemData disarmedItem = RemoveEquippedHeldItem(target, targetHeldItemSlot);
            target.ApplyCondition(CombatConditionType.Disarmed, 2, Stats.CharacterName);

            if (disarmedItem == null)
            {
                logLines.Add($"⚠ {target.Stats.CharacterName}'s held item could not be removed.");
            }
            else if (DisarmerCatchesWeapon(this, attackerHeldWeapon))
            {
                if (TryEquipDisarmedItem(this, disarmedItem, out EquipSlot equippedSlot))
                {
                    logLines.Add($"🤲 {Stats.CharacterName} was unarmed and catches {disarmedItem.Name}, equipping it in {equippedSlot}.");
                }
                else
                {
                    DropItemToGround(target, disarmedItem);
                    logLines.Add($"{disarmedItem.Name} drops to the ground in {target.Stats.CharacterName}'s square ({target.GridPosition.x},{target.GridPosition.y}) because {Stats.CharacterName} had no free hand.");
                }
            }
            else
            {
                DropItemToGround(target, disarmedItem);
                logLines.Add($"{disarmedItem.Name} drops to the ground in {target.Stats.CharacterName}'s square ({target.GridPosition.x},{target.GridPosition.y}).");
            }
        }
        else
        {
            // D&D 3.5e: failed disarm grants exactly one immediate counter-disarm attempt,
            // unless the attacker has Improved Disarm (PHB p.95: no chance to disarm you).
            if (Stats.HasFeat("Improved Disarm"))
            {
                logLines.Add($"Improved Disarm: {target.Stats.CharacterName} gets no counter-disarm attempt.");
            }
            else if (TryGetDisarmTargetHeldItem(this, null, out ItemData counterTargetHeldItem, out EquipSlot counterTargetSlot))
            {
                ItemData counterAttackerHeldWeapon = target.GetEquippedMainWeapon();
                bool counterDefenderHasLockedGauntlet = HasLockedGauntletEquipped(this);
                int counterDefenderLockedBonus = counterDefenderHasLockedGauntlet ? 10 : 0;

                DisarmCheckResult counterCheck = RollDisarmCheck(
                    target,
                    this,
                    counterAttackerHeldWeapon,
                    counterTargetHeldItem,
                    counterTargetSlot,
                    counterDefenderLockedBonus,
                    lockedGauntletReason: counterDefenderHasLockedGauntlet ? "Locked Gauntlet" : string.Empty);

                logLines.Add($"↩ Immediate counter-disarm by {target.Stats.CharacterName} (does not provoke AoO). {(counterCheck.Success ? "Success" : "Failed")} ({counterCheck.AttackerTotal} vs {counterCheck.DefenderTotal}).");
                logLines.Add(counterCheck.BreakdownLog);

                if (counterCheck.Success)
                {
                    ItemData counterDisarmedItem = RemoveEquippedHeldItem(this, counterTargetSlot);
                    ApplyCondition(CombatConditionType.Disarmed, 2, target.Stats.CharacterName);

                    if (counterDisarmedItem == null)
                    {
                        logLines.Add($"⚠ {Stats.CharacterName}'s held item could not be removed by the counter-disarm.");
                    }
                    else if (DisarmerCatchesWeapon(target, counterAttackerHeldWeapon))
                    {
                        if (TryEquipDisarmedItem(target, counterDisarmedItem, out EquipSlot counterEquipSlot))
                        {
                            logLines.Add($"🤲 {target.Stats.CharacterName} was unarmed and catches {counterDisarmedItem.Name}, equipping it in {counterEquipSlot}.");
                        }
                        else
                        {
                            DropItemToGround(this, counterDisarmedItem);
                            logLines.Add($"{counterDisarmedItem.Name} drops to the ground in {Stats.CharacterName}'s square ({GridPosition.x},{GridPosition.y}) because {target.Stats.CharacterName} had no free hand.");
                        }
                    }
                    else
                    {
                        DropItemToGround(this, counterDisarmedItem);
                        logLines.Add($"{counterDisarmedItem.Name} drops to the ground in {Stats.CharacterName}'s square ({GridPosition.x},{GridPosition.y}).");
                    }
                }
                else
                {
                    logLines.Add("Counter-disarm failed; no further free disarm attempts are granted.");
                }
            }
            else
            {
                logLines.Add($"{target.Stats.CharacterName} has no held item to use for a counter-disarm.");
            }
        }

        return new SpecialAttackResult
        {
            ManeuverName = "Disarm",
            Success = success,
            CheckRoll = primaryCheck.AttackerRoll,
            CheckTotal = primaryCheck.AttackerTotal,
            OpposedRoll = primaryCheck.DefenderRoll,
            OpposedTotal = primaryCheck.DefenderTotal,
            ProvokedAoO = false,
            Log = string.Join("\n", logLines)
        };
    }

    /// <summary>
    /// Checks whether this character can initiate a standard Grapple maneuver. Improved Grab
    /// (MM p.310) adds a free, non-provoking grapple after a qualifying hit; it does not take
    /// away the normal grapple attack, so Improved Grab creatures can use both (CMB-014).
    /// </summary>
    public bool CanUseStandardGrapple()
    {
        return Stats != null;
    }

    /// <summary>
    /// BAB of the opposed grapple check that follows a grapple's touch attack made at
    /// <paramref name="touchAttackBab"/>. An iterative step keeps its BAB (PHB p.156). When the grapple
    /// replaced a natural attack (<see cref="TryCommitManeuverSubstituteStep"/> records which), that natural
    /// attack's secondary penalty (MM p.312; -2 with Multiattack, MM p.304) is taken off again: it applies to
    /// the touch attack only (owner ruling 2026-10-08, CMB-102). Never above the creature's full BAB.
    /// </summary>
    public int GetGrappleCheckBabAfterTouchAttack(int touchAttackBab, bool touchAttackBabFromStep)
    {
        if (!touchAttackBabFromStep || Stats == null || GetManeuverSubstituteStepKind() != AttackStepKind.NaturalSequence)
            return touchAttackBab;

        NaturalAttackDefinition natural = Stats.GetNaturalAttackAtSequenceIndex(ProgressiveAttackPool.LastSubstituteNaturalAttackIndex);
        int secondaryPenalty = natural != null ? Stats.GetNaturalAttackSequencePenalty(natural) : 0;
        if (secondaryPenalty >= 0)
            return touchAttackBab;

        return Mathf.Max(touchAttackBab, Mathf.Min(touchAttackBab - secondaryPenalty, Stats.BaseAttackBonus));
    }

    private SpecialAttackResult ResolveGrapple(CharacterController target, int? iterativeAttackBonusOverride = null)
    {
        if (target == null || target.Stats == null || Stats == null)
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Grapple",
                Success = false,
                Log = "No valid grapple target."
            };
        }

        if (Stats.IsSwarm)
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Grapple",
                Success = false,
                Log = $"{Stats.CharacterName} cannot initiate grapples while in swarm form."
            };
        }

        if (target.Stats.IsSwarm)
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Grapple",
                Success = false,
                Log = $"{target.Stats.CharacterName} is a swarm and cannot be grappled."
            };
        }

        if (IsBlockedBySummonedContactBarrier(target, out _))
            return BuildSummonedContactBarrierResult(target, "Grapple");

        if (!CanUseStandardGrapple())
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Grapple",
                Success = false,
                Log = $"{Stats.CharacterName} cannot initiate a standard grapple right now."
            };
        }

        // One grapple link per creature: a holder with one body part (not IsGrappling, MM p.310) counts too (CMB-163).
        if (IsInAnyGrappleLink(out CharacterController currentOpponent))
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Grapple",
                Success = false,
                Log = $"{Stats.CharacterName} is already in a grapple with {currentOpponent.Stats.CharacterName}."
            };
        }

        if (target.IsInAnyGrappleLink(out CharacterController targetOpponent))
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Grapple",
                Success = false,
                Log = $"{target.Stats.CharacterName} is already in a grapple with {targetOpponent.Stats.CharacterName}."
            };
        }

        int touchRoll = DiceService.D20("Touch attack");
        int attackBab = iterativeAttackBonusOverride ?? Stats.BaseAttackBonus;
        int touchStr = Stats.STRMod;
        int touchSize = Stats.SizeModifier;
        int touchCondition = Stats.ConditionAttackPenalty;
        // The touch attack is an attack roll: racial bonuses against the target's kind apply, and so does the target's
        // racial dodge against giants (PHB p.15, p.17; CHR-019).
        int touchRacial = RacialTraitRules.AttackBonusAgainst(Stats, target.Stats);
        int touchTotal = touchRoll + GetManeuverMeleeTouchAttackModifier(attackBab) + touchRacial;
        // Full touch AC (deflection, dodge, conditions...) and natural 20/1 (CMB-014).
        int touchAC = target.Stats.TouchArmorClass + RacialTraitRules.KindDodgeACBonusAgainst(target.Stats, Stats);
        bool touchHit = IsManeuverTouchAttackHit(touchRoll, touchTotal, touchAC);

        var touchBuilder = new StringBuilder();
        touchBuilder.AppendLine("Touch attack:");
        touchBuilder.AppendLine($"  Base roll: 1d20 = {touchRoll}");
        touchBuilder.AppendLine($"  BAB: {attackBab:+0;-#;+0}");
        touchBuilder.AppendLine($"  STR modifier: {touchStr:+0;-#;+0}");
        touchBuilder.AppendLine($"  Size modifier: {touchSize:+0;-#;+0}");
        if (touchCondition != 0)
            touchBuilder.AppendLine($"  Condition modifiers: {touchCondition:+0;-#;+0}");
        if (touchRacial != 0)
            touchBuilder.AppendLine($"  Racial: {touchRacial:+0;-#;+0}");
        touchBuilder.AppendLine($"  Total: {touchTotal}");
        touchBuilder.AppendLine($"  Target touch AC: {touchAC}");
        if (touchRoll >= 20)
            touchBuilder.AppendLine("  Natural 20: automatic hit");
        else if (touchRoll <= 1)
            touchBuilder.AppendLine("  Natural 1: automatic miss");

        if (!touchHit)
        {
            string missLog = string.Join("\n\n", new[]
            {
                $"{Stats.CharacterName} attempts to grapple {target.Stats.CharacterName}",
                touchBuilder.ToString().TrimEnd(),
                "Result: Miss!\nGrapple attempt failed"
            });

            return new SpecialAttackResult
            {
                ManeuverName = "Grapple",
                Success = false,
                CheckRoll = touchRoll,
                CheckTotal = touchTotal,
                OpposedTotal = touchAC,
                Log = missLog
            };
        }

        // The step's BAB enters the opposed grapple check too: for an iterative step that is RAW (PHB
        // p.156, multiple grapples at successively lower BAB). A grapple that replaces a secondary natural
        // attack takes the -5 (or -2 with Multiattack) on the touch attack only: it is a penalty on the
        // attack roll (MM p.312), and natural attacks use the full BAB, so the grapple check does not take
        // it (owner ruling 2026-10-08, CMB-102).
        int grappleCheckBab = GetGrappleCheckBabAfterTouchAttack(attackBab, iterativeAttackBonusOverride.HasValue);
        GrappleCheckResult attackerCheck = RollGrappleCheck(grappleCheckBab);
        GrappleCheckResult defenderCheck = target.RollGrappleCheck(context: GrappleCheckContext.ResistGrapple);

        // PHB p.156 step 3: the hold automatically fails against a target two or more size
        // categories larger.
        bool targetTooLarge = (int)target.Stats.CurrentSizeCategory - (int)Stats.CurrentSizeCategory >= 2;
        bool success = !targetTooLarge && DoesAttackerWinGrappleCheck(attackerCheck, defenderCheck);
        string grapplePositioningLog = string.Empty;
        if (success)
            grapplePositioningLog = EstablishGrappleWith(target);

        string resultLine = targetTooLarge
            ? $"Result: {target.Stats.CharacterName} is two or more size categories larger; the hold automatically fails."
            : BuildOpposedResultLine(Stats.CharacterName, attackerCheck.Total, target.Stats.CharacterName, defenderCheck.Total, success);
        string outcomeLine = success
            ? $"{Stats.CharacterName} successfully grapples {target.Stats.CharacterName}!"
            : "Grapple attempt failed";

        if (success)
            outcomeLine += " Both are GRAPPLED. While grappled: no threatened squares, no attacks of opportunity, no normal movement (only Move grapple action at half speed after winning opposed check).";

        if (success && !string.IsNullOrEmpty(grapplePositioningLog))
            outcomeLine += $" {grapplePositioningLog}";

        string successLog = string.Join("\n\n", new[]
        {
            $"{Stats.CharacterName} attempts to grapple {target.Stats.CharacterName}",
            touchBuilder.ToString().TrimEnd() + "\nResult: Hit!",
            attackerCheck.GetBreakdown(),
            defenderCheck.GetBreakdown(),
            resultLine + "\n" + outcomeLine
        });

        return new SpecialAttackResult
        {
            ManeuverName = "Grapple",
            Success = success,
            CheckRoll = attackerCheck.BaseRoll,
            CheckTotal = attackerCheck.Total,
            OpposedRoll = defenderCheck.BaseRoll,
            OpposedTotal = defenderCheck.Total,
            Log = successLog
        };
    }

    /// <summary>
    /// The largest size this creature's Improved Grab can seize: the MM entry's own maximum when it names one
    /// (<see cref="CharacterStats.ImprovedGrabMaxTargetSize"/>), else one size category smaller than the creature's
    /// current size (MM p.310). Below Fine (a Fine grabber without an override) nothing qualifies.
    /// </summary>
    public int GetImprovedGrabMaxTargetSizeIndex()
    {
        if (Stats != null && Stats.ImprovedGrabMaxTargetSize.HasValue)
            return (int)Stats.ImprovedGrabMaxTargetSize.Value;
        return (int)GetCurrentSizeCategory() - 1;
    }

    /// <summary>
    /// The size limit of Improved Grab (MM p.310): unless the creature's entry says otherwise, the free grapple works
    /// only against an opponent at least one size category smaller. The one size test for every Improved Grab path,
    /// PC and NPC alike: <c>GameManager.CanAttemptImprovedGrabFromAttack</c> (single attacks, full attacks, attack
    /// sequences), the charge and pounce grabs, <see cref="ResolveImprovedGrabFreeAttempt"/> and the AI's Haste pick
    /// (DND35.AI.NaturalAttackChoice). Only the size is tested here; trigger attack, hit and grapple state are the
    /// callers' checks.
    /// </summary>
    public bool CanImprovedGrabTargetBySize(CharacterController target, out string reason)
    {
        reason = null;
        if (target == null || target.Stats == null || Stats == null)
        {
            reason = "no valid target";
            return false;
        }

        int maxSize = GetImprovedGrabMaxTargetSizeIndex();
        if ((int)target.GetCurrentSizeCategory() <= maxSize)
            return true;

        string limit = maxSize < (int)SizeCategory.Fine
            ? "no size"
            : ((SizeCategory)maxSize).ToString() + " or smaller";
        reason = $"{target.Stats.CharacterName} ({target.GetCurrentSizeCategory()}) is too large for {Stats.CharacterName}'s Improved Grab ({limit}, MM p.310)";
        return false;
    }

    /// <summary>
    /// The natural attack of this creature that <paramref name="attackResult"/> was made with (by its name), or null: the
    /// attack that establishes an Improved Grab hold (MM p.310, CMB-148).
    /// </summary>
    public NaturalAttackDefinition GetNaturalAttackForAttackResult(CombatResult attackResult)
        => attackResult != null ? GetNaturalAttackByWeaponName(attackResult.WeaponName) : null;

    /// <summary>This creature's natural attack named <paramref name="weaponName"/> (exact name first, then a name it contains), or null.</summary>
    public NaturalAttackDefinition GetNaturalAttackByWeaponName(string weaponName)
    {
        if (Stats == null || Stats.NaturalAttacks == null || string.IsNullOrWhiteSpace(weaponName))
            return null;

        weaponName = weaponName.Trim();
        NaturalAttackDefinition partial = null;
        foreach (NaturalAttackDefinition natural in Stats.NaturalAttacks)
        {
            if (natural == null || string.IsNullOrWhiteSpace(natural.Name))
                continue;
            if (string.Equals(natural.Name.Trim(), weaponName, StringComparison.OrdinalIgnoreCase))
                return natural;
            if (partial == null && weaponName.IndexOf(natural.Name.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                partial = natural;
        }

        return partial;
    }

    /// <summary>
    /// The free grapple attempt of Improved Grab (MM p.310), the one resolver for PCs and NPCs. <paramref name="establishingAttack"/>
    /// is the natural attack whose hit triggered it (recorded on the grapple link: its damage on later successful Damage
    /// checks, CMB-148). With <paramref name="holdWithBodyPart"/> the controller chose to hold with that body part instead
    /// of grappling normally: -20 on this check and on its later grapple checks, and it is not grappled (MM p.310; owner
    /// ruling 2026-10-11, CMB-148, CMB-149). The choice is made before the check, so the -20 applies to it (MM p.310: "it
    /// takes a -20 penalty on grapple checks").
    /// </summary>
    public SpecialAttackResult ResolveImprovedGrabFreeAttempt(CharacterController target, NaturalAttackDefinition establishingAttack = null,
        bool holdWithBodyPart = false)
    {
        if (target == null || target.Stats == null || Stats == null)
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Improved Grab",
                Success = false,
                Log = "No valid target for Improved Grab."
            };
        }

        if (IsBlockedBySummonedContactBarrier(target, out _))
            return BuildSummonedContactBarrierResult(target, "Improved Grab");

        if (!Stats.HasImprovedGrab)
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Improved Grab",
                Success = false,
                Log = $"{Stats.CharacterName} does not have Improved Grab."
            };
        }

        if (!CanImprovedGrabTargetBySize(target, out string sizeReason))
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Improved Grab",
                Success = false,
                Log = sizeReason + "."
            };
        }

        // One grapple link per creature: a holder with one body part (not IsGrappling, MM p.310) counts too (CMB-163).
        if (IsInAnyGrappleLink(out CharacterController currentOpponent))
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Improved Grab",
                Success = false,
                Log = $"{Stats.CharacterName} is already in a grapple with {currentOpponent.Stats.CharacterName}."
            };
        }

        if (target.IsInAnyGrappleLink(out CharacterController targetOpponent))
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Improved Grab",
                Success = false,
                Log = $"{target.Stats.CharacterName} is already in a grapple with {targetOpponent.Stats.CharacterName}."
            };
        }

        // A hold needs the body part that made the attack (MM p.310); without one the grapple is conducted normally. The
        // choice to hold comes before the free check, so the -20 applies to that establishing check too (MM p.310; PHB
        // p.155-156 names it the "Hold" step; delegated RAW reading 2026-10-14, CMB-163).
        bool hold = holdWithBodyPart && establishingAttack != null;
        string partName = establishingAttack != null && !string.IsNullOrWhiteSpace(establishingAttack.Name)
            ? establishingAttack.Name.Trim().ToLowerInvariant()
            : "trigger attack";
        GrappleCheckResult attackerCheck = hold
            ? RollGrappleCheck(additionalModifier: ImprovedGrabHoldGrappleCheckPenalty, additionalModifierLabel: $"Improved Grab hold with its {partName} (MM p.310)")
            : RollGrappleCheck();
        GrappleCheckResult defenderCheck = target.RollGrappleCheck(context: GrappleCheckContext.ResistGrapple);
        bool success = DoesAttackerWinGrappleCheck(attackerCheck, defenderCheck);

        string grapplePositioningLog = string.Empty;
        if (success)
            grapplePositioningLog = EstablishGrappleLink(target, establishingAttack, hold);

        string resultLine = BuildOpposedResultLine(Stats.CharacterName, attackerCheck.Total, target.Stats.CharacterName, defenderCheck.Total, success);
        string outcomeLine = success
            ? (hold
                ? $"{Stats.CharacterName} holds {target.Stats.CharacterName} with its {partName}!"
                : $"{Stats.CharacterName} seizes {target.Stats.CharacterName} with Improved Grab!")
            : $"{Stats.CharacterName} fails to secure the grapple.";

        if (success)
            outcomeLine += hold
                ? $" {target.Stats.CharacterName} is grappled; {Stats.CharacterName} is not (it takes -20 on grapple checks, keeps its Dex bonus, threatens and may attack other opponents; MM p.310)."
                : " Both combatants gain the grappled condition.";

        if (success && !string.IsNullOrEmpty(grapplePositioningLog))
            outcomeLine += $" {grapplePositioningLog}";

        string log = string.Join("\n\n", new[]
        {
            hold
                ? $"{Stats.CharacterName} attempts Improved Grab on {target.Stats.CharacterName} (free action), holding with its {partName} only (-20)."
                : $"{Stats.CharacterName} attempts Improved Grab on {target.Stats.CharacterName} (free action).",
            attackerCheck.GetBreakdown(),
            defenderCheck.GetBreakdown(),
            resultLine + "\n" + outcomeLine
        });

        return new SpecialAttackResult
        {
            ManeuverName = "Improved Grab",
            Success = success,
            CheckRoll = attackerCheck.BaseRoll,
            CheckTotal = attackerCheck.Total,
            OpposedRoll = defenderCheck.BaseRoll,
            OpposedTotal = defenderCheck.Total,
            Log = log,
            ProvokedAoO = false
        };
    }

    private SpecialAttackResult ResolveSunder(
        CharacterController target,
        EquipSlot? targetSlot,
        int? iterativeAttackBonusOverride = null,
        ItemData attackerWeaponOverride = null,
        bool usedOffHand = false,
        int attackerDualWieldPenaltyForLog = 0)
    {
        if (IsBlockedBySummonedContactBarrier(target, out _))
            return BuildSummonedContactBarrierResult(target, "Sunder");

        if (!TryGetSunderTargetItem(target, targetSlot, out ItemData targetItem, out EquipSlot resolvedTargetSlot, out SunderTargetKind targetKind))
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Sunder",
                Success = false,
                Log = $"{target.Stats.CharacterName} has no sunderable weapon, shield, or armor equipped."
            };
        }

        // The weapon or natural attack the sunder is made with (CanSunderWithAttack). A natural-attack
        // creature uses the natural attack the sunder replaced (recorded by TryCommitManeuverSubstituteStep),
        // or, when no substitute step was committed, the one at its current step (CMB-102).
        ItemData attackerWeapon = attackerWeaponOverride;
        NaturalAttackDefinition sunderNatural = null;
        if (attackerWeapon == null)
        {
            int naturalIndex = -1;
            if (GetEquippedMainWeapon() == null && UsesInnateNaturalAttackSequence())
            {
                naturalIndex = ProgressiveAttackPool.LastSubstituteNaturalAttackIndex >= 0
                    ? ProgressiveAttackPool.LastSubstituteNaturalAttackIndex
                    : GetSunderNaturalAttackIndexForStep(ProgressiveAttackPool.MainHandStepsUsed);
                if (naturalIndex < 0)
                    naturalIndex = GetFirstSunderCapableNaturalAttackIndex();
            }

            if (!CanSunderWithAttack(naturalIndex, out string noSunderWeaponReason))
            {
                return new SpecialAttackResult
                {
                    ManeuverName = "Sunder",
                    Success = false,
                    Log = noSunderWeaponReason
                };
            }

            attackerWeapon = GetEquippedMainWeapon();
            if (attackerWeapon == null && UsesInnateNaturalAttackSequence())
                sunderNatural = Stats.GetNaturalAttackAtSequenceIndex(naturalIndex);
            if (attackerWeapon == null && sunderNatural == null && UsesInnateNaturalAttackSequence())
            {
                return new SpecialAttackResult
                {
                    ManeuverName = "Sunder",
                    Success = false,
                    Log = $"{Stats.CharacterName} has no weapon or natural attack that can sunder."
                };
            }
        }

        // An unarmed strike (owner interpretation 2026-10-11, CMB-141): no weapon in hand and no natural sequence, or
        // an "Unarmed Strike" the creature lists as a natural attack (no shipped entry since CRE-042). A light weapon on the opposed
        // roll (PHB p.158; PHB p.116 Table 7-5).
        bool unarmedSunder = attackerWeapon == null && (sunderNatural == null || sunderNatural.IsUnarmedStrike);
        // The attack's damage mode, the same one an attack with this weapon or unarmed strike would use: a plain
        // unarmed strike deals nonlethal damage unless the creature has Improved Unarmed Strike (or a gauntlet) or
        // takes -4 for lethal damage (PHB p.139); a nonlethal weapon (sap, whip) takes -4 for lethal damage and a
        // lethal weapon -4 for nonlethal (PHB p.146). The opposed roll is an attack roll (PHB p.158), so that -4
        // applies to it, and objects are immune to nonlethal damage (PHB p.165), so a nonlethal sunder that wins
        // deals the item nothing. A natural attack, a natural "Unarmed Strike" included, is always lethal.
        DamageModeAttackProfile sunderDamageMode = GetSunderDamageModeProfile(attackerWeapon, sunderNatural);

        targetItem.EnsureDurabilityInitialized();

        int attackRoll = DiceService.D20("Sunder attack roll");
        int defenseRoll = DiceService.D20("Sunder defense roll");
        int attackBab = iterativeAttackBonusOverride ?? Stats.BaseAttackBonus;
        int improvedSunderBonus = Stats.HasFeat("Improved Sunder") ? 4 : 0;
        // PHB p.158 Step 2: each side rolls with its own weapon, +4 for a two-handed one and -4 for a light
        // one, and the larger combatant gets +4 per size category of difference: the size term compares the
        // combatants (PHB p.158; recorded 2026-10-11, CMB-141). The defender rolls with the targeted weapon, or, when a
        // shield or armor is targeted (worn armor should not be a target at all, CMB-166), with the weapon it holds:
        // the main-hand weapon first, else a weapon held in the off hand (GetSunderDefenderWeapon). The targeted shield
        // is never its weapon. With no weapon in hand it rolls with a true natural weapon (0) or its unarmed strike, a
        // light weapon (-4), an "Unarmed Strike" listed as a natural attack included (delegated RAW reading 2026-10-14,
        // CMB-165). A natural weapon gets no handedness modifier, as on a disarm roll (owner ruling 2026-10-08 for
        // disarm and owner interpretation 2026-10-11 for sunder, CMB-141); the attacker's unarmed strike is a light
        // weapon, -4 (owner interpretation 2026-10-11, CMB-141).
        int handednessBonus = unarmedSunder
            ? UnarmedStrikeSunderHandednessModifier
            : sunderNatural != null ? NaturalWeaponDisarmHandednessModifier : GetSunderHandednessModifier(attackerWeapon);
        int damageModePenalty = sunderDamageMode.AttackPenalty;
        int sizeBonus = GetLargerCombatantSizeBonus(this, target);
        ItemData defenderWeapon = GetSunderDefenderWeapon(target, targetItem);
        int defenderHandednessBonus = GetSunderDefenderHandednessModifier(target, defenderWeapon, out string defenderHandednessLabel);
        int defenderSizeBonus = GetLargerCombatantSizeBonus(target, this);
        // Both rolls are opposed attack rolls (PHB p.158), so each side's racial attack bonus against the other's kind
        // applies (PHB p.15, p.17; CHR-019).
        int racialBonus = RacialTraitRules.AttackBonusAgainst(Stats, target.Stats);
        int defenderRacialBonus = RacialTraitRules.AttackBonusAgainst(target.Stats, Stats);
        // The flurry penalty on either side's opposed attack roll while its flurry lasts (PHB p.40; CHR-003).
        int flurryPenalty = FlurryAttackRollPenalty;
        int defenderFlurryPenalty = target.FlurryAttackRollPenalty;

        int attackTotal = attackRoll + flurryPenalty
            + attackBab
            + Stats.STRMod
            + Stats.SizeModifier
            + Stats.ConditionAttackPenalty
            + improvedSunderBonus
            + handednessBonus
            + damageModePenalty
            + sizeBonus
            + racialBonus;

        int defenseTotal = defenseRoll + defenderFlurryPenalty
            + target.Stats.BaseAttackBonus
            + target.Stats.STRMod
            + target.Stats.SizeModifier
            + target.Stats.ConditionAttackPenalty
            + defenderHandednessBonus
            + defenderSizeBonus
            + defenderRacialBonus;

        var logLines = new List<string>();
        string handLabel = usedOffHand ? "Off-Hand" : "Main Hand";
        string targetLabel = GetSunderTargetDisplayLabel(targetKind, targetItem);

        logLines.Add(sunderNatural != null
            ? $"{Stats.CharacterName} attempts to sunder {target.Stats.CharacterName}'s {targetLabel} ({sunderNatural.Name})"
            : unarmedSunder
                ? $"{Stats.CharacterName} attempts to sunder {target.Stats.CharacterName}'s {targetLabel} (Unarmed strike, {(sunderDamageMode.DealNonlethalDamage ? "nonlethal" : "lethal")})"
                : $"{Stats.CharacterName} attempts to sunder {target.Stats.CharacterName}'s {targetLabel} ({handLabel})");
        logLines.Add(
            $"Attacker check: d20 {attackRoll} + BAB {CharacterStats.FormatMod(attackBab)} + STR {CharacterStats.FormatMod(Stats.STRMod)} + size {CharacterStats.FormatMod(Stats.SizeModifier)}"
            + (Stats.ConditionAttackPenalty != 0 ? $" + condition {CharacterStats.FormatMod(Stats.ConditionAttackPenalty)}" : string.Empty)
            + (improvedSunderBonus != 0 ? $" + Improved Sunder {CharacterStats.FormatMod(improvedSunderBonus)}" : string.Empty)
            + (handednessBonus != 0 ? $" + {(unarmedSunder ? "unarmed strike (light)" : "handedness")} {CharacterStats.FormatMod(handednessBonus)}" : string.Empty)
            + (damageModePenalty != 0
                ? $" + {(unarmedSunder ? "lethal unarmed strike" : (sunderDamageMode.DealNonlethalDamage ? "nonlethal" : "lethal") + " with " + attackerWeapon.Name)} {CharacterStats.FormatMod(damageModePenalty)}"
                : string.Empty)
            + (sizeBonus != 0 ? $" + larger size {CharacterStats.FormatMod(sizeBonus)}" : string.Empty)
            + (racialBonus != 0 ? $" + racial {CharacterStats.FormatMod(racialBonus)}" : string.Empty)
            + (flurryPenalty != 0 ? $" + flurry of blows {CharacterStats.FormatMod(flurryPenalty)}" : string.Empty)
            + (attackerDualWieldPenaltyForLog != 0 ? $" [includes dual-wield penalty {CharacterStats.FormatMod(attackerDualWieldPenaltyForLog)} in BAB]" : string.Empty)
            + $" = {attackTotal}");
        logLines.Add($"Defender check: d20 {defenseRoll} + BAB {CharacterStats.FormatMod(target.Stats.BaseAttackBonus)} + STR {CharacterStats.FormatMod(target.Stats.STRMod)} + size {CharacterStats.FormatMod(target.Stats.SizeModifier)}"
            + (target.Stats.ConditionAttackPenalty != 0 ? $" + condition {CharacterStats.FormatMod(target.Stats.ConditionAttackPenalty)}" : string.Empty)
            + (defenderHandednessBonus != 0 ? $" + {defenderHandednessLabel} {CharacterStats.FormatMod(defenderHandednessBonus)}" : string.Empty)
            + (defenderSizeBonus != 0 ? $" + larger size {CharacterStats.FormatMod(defenderSizeBonus)}" : string.Empty)
            + (defenderRacialBonus != 0 ? $" + racial {CharacterStats.FormatMod(defenderRacialBonus)}" : string.Empty)
            + (defenderFlurryPenalty != 0 ? $" + flurry of blows {CharacterStats.FormatMod(defenderFlurryPenalty)}" : string.Empty)
            + $" = {defenseTotal}");

        if (attackTotal < defenseTotal)
        {
            logLines.Add($"Result: FAILURE ({attackTotal} vs {defenseTotal}).");
            return new SpecialAttackResult
            {
                ManeuverName = "Sunder",
                Success = false,
                CheckRoll = attackRoll,
                CheckTotal = attackTotal,
                OpposedRoll = defenseRoll,
                OpposedTotal = defenseTotal,
                DamageDealt = 0,
                Log = string.Join("\n", logLines)
            };
        }

        int damageDiceSides;
        int damageDiceCount;
        WeaponDamageBreakdown sunderDamage;
        // The shared damage modifier (CMB-003): the sunder is a melee attack with this weapon or natural attack, so its
        // damage roll adds the same terms (Strength share, enhancement, Weapon Specialization, morale, conditions). No
        // Power Attack: the sunder's opposed roll above does not take its penalty or the other attack-side feat terms
        // (CMB-159).
        if (sunderDamageMode.DealNonlethalDamage)
        {
            // Objects are immune to nonlethal damage (PHB p.165).
            string nonlethalSource = unarmedSunder ? "unarmed strike" : attackerWeapon.Name;
            logLines.Add($"Damage: none. {Stats.CharacterName}'s {nonlethalSource} deals nonlethal damage, and objects are immune to nonlethal damage (PHB p.165).");
            return new SpecialAttackResult
            {
                ManeuverName = "Sunder",
                Success = true,
                CheckRoll = attackRoll,
                CheckTotal = attackTotal,
                OpposedRoll = defenseRoll,
                OpposedTotal = defenseTotal,
                DamageDealt = 0,
                Log = string.Join("\n", logLines)
            };
        }

        if (unarmedSunder && sunderNatural == null)
        {
            // A plain unarmed strike: 1d3 at Medium, scaled for size, or the monk's unarmed damage (PHB p.40).
            (damageDiceCount, damageDiceSides, _) = GetUnarmedDamage();
            damageDiceSides = Mathf.Max(1, damageDiceSides);
            damageDiceCount = Mathf.Max(1, damageDiceCount);
            sunderDamage = BuildWeaponDamageBonus(null, isRanged: false, isOffHand: false, DamageOnlyFeatModifiers(null));
        }
        else if (sunderNatural != null)
        {
            // The natural attack's own damage: its dice scaled for size and its Strength share (half for a
            // secondary attack, MM p.312).
            Stats.GetScaledNaturalAttackDamage(sunderNatural, out damageDiceCount, out damageDiceSides);
            damageDiceSides = Mathf.Max(1, damageDiceSides);
            damageDiceCount = Mathf.Max(1, damageDiceCount);
            // NaturalAttackDefinition.BonusDamage is not a damage term on any attack path (CRE-061).
            sunderDamage = BuildWeaponDamageBonus(null, isRanged: false, isOffHand: false,
                default(AttackCalculator.FeatModifiers), naturalAttack: sunderNatural);
        }
        else
        {
            GetScaledWeaponDamageDice(attackerWeapon, out damageDiceCount, out damageDiceSides);
            damageDiceSides = Mathf.Max(1, damageDiceSides);
            damageDiceCount = Mathf.Max(1, damageDiceCount);
            sunderDamage = BuildWeaponDamageBonus(attackerWeapon, isRanged: false, usedOffHand, DamageOnlyFeatModifiers(attackerWeapon),
                attackerWeapon.BonusDamage);
        }

        int damageRoll = Stats.RollBaseDamage(damageDiceSides, damageDiceCount);
        int rawDamage = Mathf.Max(1, damageRoll + sunderDamage.Total);

        targetItem.ApplySunderDamage(rawDamage, out int effectiveDamage, out int hpBefore, out int hpAfter);

        logLines.Add($"Damage: {damageDiceCount}d{damageDiceSides} ({damageRoll}) + [{sunderDamage.Describe()}] = {rawDamage}");
        logLines.Add($"Object durability: hardness {targetItem.Hardness} reduces damage to {effectiveDamage}. HP {hpBefore} -> {hpAfter}/{targetItem.MaxHitPoints}");

        if (targetItem.IsDestroyed)
        {
            DestroyEquippedItem(target, resolvedTargetSlot);
            if (targetKind == SunderTargetKind.MainHand || targetKind == SunderTargetKind.OffHand || targetKind == SunderTargetKind.Shield)
                target.ApplyCondition(CombatConditionType.Disarmed, 2, Stats.CharacterName);

            logLines.Add($"💥 {target.Stats.CharacterName}'s {targetItem.Name} is destroyed!");
            logLines.Add($"{targetItem.Name} is removed from inventory.");
            return new SpecialAttackResult
            {
                ManeuverName = "Sunder",
                Success = true,
                CheckRoll = attackRoll,
                CheckTotal = attackTotal,
                OpposedRoll = defenseRoll,
                OpposedTotal = defenseTotal,
                DamageDealt = effectiveDamage,
                Log = string.Join("\n", logLines)
            };
        }

        if (targetItem.IsBroken)
            logLines.Add($"⚠ {target.Stats.CharacterName}'s {targetItem.Name} is now BROKEN.");
        else
            logLines.Add($"Result: Hit item but it remains intact.");

        return new SpecialAttackResult
        {
            ManeuverName = "Sunder",
            Success = true,
            CheckRoll = attackRoll,
            CheckTotal = attackTotal,
            OpposedRoll = defenseRoll,
            OpposedTotal = defenseTotal,
            DamageDealt = effectiveDamage,
            Log = string.Join("\n", logLines)
        };
    }

    private static string GetSunderTargetDisplayLabel(SunderTargetKind kind, ItemData item)
    {
        string itemName = item != null ? item.Name : "item";
        switch (kind)
        {
            case SunderTargetKind.MainHand: return $"main-hand weapon ({itemName})";
            case SunderTargetKind.OffHand: return $"off-hand item ({itemName})";
            case SunderTargetKind.Shield: return $"shield ({itemName})";
            case SunderTargetKind.Armor: return $"armor ({itemName})";
            default: return itemName;
        }
    }

    private static bool TryGetSunderTargetItem(CharacterController target, EquipSlot? preferredSlot, out ItemData item, out EquipSlot resolvedSlot, out SunderTargetKind kind)
    {
        item = null;
        resolvedSlot = EquipSlot.None;
        kind = SunderTargetKind.MainHand;

        if (target == null)
            return false;

        List<SunderableItemOption> options = target.GetSunderableItemOptions();
        if (options.Count == 0)
            return false;

        if (preferredSlot.HasValue)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Slot == preferredSlot.Value)
                {
                    item = options[i].Item;
                    resolvedSlot = options[i].Slot;
                    kind = options[i].Kind;
                    return item != null;
                }
            }
        }

        item = options[0].Item;
        resolvedSlot = options[0].Slot;
        kind = options[0].Kind;
        return item != null;
    }

    /// <summary>
    /// Bull rush attacker check (PHB p.154): an opposed Strength check, so no BAB. Special size
    /// modifier, +2 when charging, +4 with Improved Bull Rush (CMB-014).
    /// </summary>
    public BullRushCheckResult RollBullRushAttackerCheck(int chargeBonus = 0, int? fixedRoll = null)
    {
        var result = new BullRushCheckResult
        {
            CharacterName = Stats != null ? Stats.CharacterName : name,
            BaseRoll = fixedRoll ?? DiceService.D20("Bull rush check"),
            StrengthModifier = Stats != null ? Stats.STRMod : 0,
            SizeModifier = GetSpecialSizeModifier(),
            ChargeBonus = chargeBonus,
            UsesBestStrengthOrDexterity = false
        };

        if (Stats != null && Stats.HasFeat("Improved Bull Rush"))
            result.AddMiscModifier(4, "Improved Bull Rush feat");
        if (Stats != null && Stats.ConditionAbilityCheckModifier != 0)
            result.AddMiscModifier(Stats.ConditionAbilityCheckModifier, "Condition modifiers");

        result.Total = result.BaseRoll
            + result.StrengthModifier
            + result.SizeModifier
            + result.ChargeBonus
            + result.MiscModifier;

        return result;
    }

    /// <summary>
    /// Bull rush defender check (PHB p.154): Strength (not Dexterity), special size modifier and
    /// +4 stability (CMB-014).
    /// </summary>
    public BullRushCheckResult RollBullRushDefenderCheck(int? fixedRoll = null)
    {
        var result = new BullRushCheckResult
        {
            CharacterName = Stats != null ? Stats.CharacterName : name,
            BaseRoll = fixedRoll ?? DiceService.D20("Bull rush defense"),
            StrengthModifier = Stats != null ? Stats.STRMod : 0,
            SizeModifier = GetSpecialSizeModifier(),
            StabilityBonus = GetManeuverStabilityBonus(),
            UsesBestStrengthOrDexterity = false
        };

        if (Stats != null && Stats.ConditionAbilityCheckModifier != 0)
            result.AddMiscModifier(Stats.ConditionAbilityCheckModifier, "Condition modifiers");

        result.Total = result.BaseRoll
            + result.StrengthModifier
            + result.SizeModifier
            + result.StabilityBonus
            + result.MiscModifier;

        return result;
    }

    /// <summary>
    /// Shared bull rush legality for PCs and NPCs (PHB p.154, CMB-102). Spends nothing. Refuses when
    /// the target is missing, dead or this creature; the target is a swarm (MM p.316); this creature
    /// is a swarm (owner interpretation of a RAW silence, 2026-10-07, see below); either side is
    /// incorporeal (MM p.311); this creature is grappling or pinned (PHB p.156); the target is more
    /// than one size category larger; or the target is not adjacent (the bull rusher must enter its
    /// space).
    /// <paramref name="atEndOfCharge"/> true is for the charge planner only, which calls it before
    /// the move: it skips the adjacency rule, which the charge endpoints satisfy. The bull rush itself
    /// (ResolveBullRush, ResolveChargeBullRush) always checks with false, after the move.
    /// </summary>
    public bool CanBullRush(CharacterController target, bool atEndOfCharge, out string reason)
    {
        reason = null;
        if (target == null || target.Stats == null || target == this)
        {
            reason = "no valid target";
            return false;
        }

        if (target.IsDead || target.Stats.IsDead)
        {
            reason = $"{target.Stats.CharacterName} is dead";
            return false;
        }

        // Owner interpretation of a RAW silence (2026-10-07; not a house rule): a swarm cannot bull rush. MM p.316
        // bars bull rushing a swarm and gives swarms no standard melee attacks, but does not say whether a swarm can
        // bull rush; the owner ruled that it cannot (docs/systems/RULES_COVERAGE.md section 4.1).
        if (Stats != null && Stats.IsSwarm)
        {
            reason = "a swarm cannot bull rush";
            return false;
        }

        if (target.Stats.IsSwarm)
        {
            reason = $"{target.Stats.CharacterName} is a swarm and cannot be bull rushed";
            return false;
        }

        if (IsIncorporeal)
        {
            reason = "an incorporeal creature cannot physically push another";
            return false;
        }

        if (target.IsIncorporeal)
        {
            reason = $"{target.Stats.CharacterName} is incorporeal and cannot be pushed";
            return false;
        }

        if (IsGrappling() || IsPinned())
        {
            reason = "cannot bull rush while grappling";
            return false;
        }

        // SizeCategory runs Fine..Colossal: the target may be at most one category larger.
        if ((int)target.GetCurrentSizeCategory() - (int)GetCurrentSizeCategory() > 1)
        {
            reason = $"{target.Stats.CharacterName} is more than one size category larger";
            return false;
        }

        // Adjacent only: the bull rusher moves into the defender's space (PHB p.154), so a creature sharing this
        // creature's square cannot be bull rushed (owner interpretation 2026-10-11, CMB-154).
        if (!atEndOfCharge && GetMinimumDistanceToTarget(target, chebyshev: true) != 1)
        {
            reason = $"{target.Stats.CharacterName} is not adjacent";
            return false;
        }

        return true;
    }

    private SpecialAttackResult ResolveBullRush(CharacterController target, int chargeBonus)
    {
        // Checked after any charge move, so adjacency applies here too (PHB p.154).
        if (!CanBullRush(target, false, out string bullRushReason))
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Bull Rush",
                Success = false,
                Log = $"{(Stats != null ? Stats.CharacterName : name)} cannot bull rush: {bullRushReason}."
            };
        }

        if (IsBlockedBySummonedContactBarrier(target, out _))
            return BuildSummonedContactBarrierResult(target, "Bull Rush");

        BullRushCheckResult attackerCheck = RollBullRushAttackerCheck(chargeBonus);
        BullRushCheckResult defenderCheck = target.RollBullRushDefenderCheck();
        bool success = DoesAttackerWinOpposedCheck(
            attackerCheck.Total, attackerCheck.Total - attackerCheck.BaseRoll,
            defenderCheck.Total, defenderCheck.Total - defenderCheck.BaseRoll);

        string resultLine = BuildOpposedResultLine(Stats.CharacterName, attackerCheck.Total, target.Stats.CharacterName, defenderCheck.Total, success);
        int margin = attackerCheck.Total - defenderCheck.Total;

        string header = chargeBonus > 0
            ? $"{Stats.CharacterName} charges and attempts to bull rush {target.Stats.CharacterName}"
            : $"{Stats.CharacterName} attempts to bull rush {target.Stats.CharacterName}";

        string outcome = success
            ? $"{Stats.CharacterName} successfully bull rushes {target.Stats.CharacterName} (wins by {margin}): push 5 ft, farther only by moving with the target (PHB p.154)."
            : $"{Stats.CharacterName} fails to bull rush {target.Stats.CharacterName}";

        string log = string.Join("\n\n", new[]
        {
            header,
            attackerCheck.GetBreakdown(),
            defenderCheck.GetBreakdown(),
            resultLine + "\n" + outcome
        });

        return new SpecialAttackResult
        {
            ManeuverName = "Bull Rush",
            Success = success,
            CheckRoll = attackerCheck.BaseRoll,
            CheckTotal = attackerCheck.Total,
            OpposedRoll = defenderCheck.BaseRoll,
            OpposedTotal = defenderCheck.Total,
            DamageDealt = 0,
            Log = log
        };
    }

    public SpecialAttackResult ResolveOverrunAttempt(CharacterController target, bool defenderBlocks, bool provokedAoO = false)
    {
        return ResolveOverrun(target, defenderBlocks, provokedAoO);
    }

    private SpecialAttackResult ResolveOverrun(CharacterController target, bool defenderBlocks, bool provokedAoO = false)
    {
        if (!defenderBlocks)
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Overrun",
                Success = true,
                DefenderAvoided = true,
                ProvokedAoO = provokedAoO,
                AttackerActionConsumed = false,
                Log = $"{target.Stats.CharacterName} avoids {Stats.CharacterName}'s overrun attempt and yields space."
            };
        }

        if (IsBlockedBySummonedContactBarrier(target, out _))
            return BuildSummonedContactBarrierResult(target, "Overrun");

        int atkRoll = DiceService.D20("Overrun attack roll");
        int defRoll = DiceService.D20("Overrun defense roll");

        // PHB p.157 overrun block: the attacker's Strength check (special size modifier,
        // +4 Improved Overrun) against the defender's Strength or Dexterity check, whichever is
        // better, with special size modifier and +4 stability (CMB-014).
        int atkModifier = GetOverrunAttackerCheckModifier();
        int defModifier = target.GetTripOrOverrunDefenderCheckModifier();
        int atkTotal = atkRoll + atkModifier;
        int defTotal = defRoll + defModifier;
        bool success = DoesAttackerWinOpposedCheck(atkTotal, atkModifier, defTotal, defModifier);

        if (success)
            target.ApplyCondition(CombatConditionType.Prone, 1, Stats.CharacterName);

        return new SpecialAttackResult
        {
            ManeuverName = "Overrun",
            Success = success,
            DefenderAvoided = false,
            ProvokedAoO = provokedAoO,
            AttackerActionConsumed = true,
            CheckRoll = atkRoll,
            CheckTotal = atkTotal,
            OpposedRoll = defRoll,
            OpposedTotal = defTotal,
            Log = success
                ? $"{Stats.CharacterName} overruns {target.Stats.CharacterName}! ({atkTotal} vs {defTotal}) Target knocked PRONE."
                : $"{Stats.CharacterName} fails to overrun {target.Stats.CharacterName}. ({atkTotal} vs {defTotal})"
        };
    }

    private SpecialAttackResult ResolveFeint(CharacterController target)
    {
        if (Stats != null && Stats.IsSkillBlockedByMindless("Bluff"))
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Feint",
                Success = false,
                Log = Stats.GetMindlessSkillRestrictionMessage("Bluff")
            };
        }

        int bluffRoll = DiceService.D20("Feint Bluff check");
        int opposedRoll = DiceService.D20("Feint opposed check");

        // A ranger's favored enemy bonus applies to his Bluff against a favored foe, and to the defender's Sense Motive
        // (or its stand-in below) against a feinting favored foe (PHB p.47; CHR-020).
        int bluffBonus = Stats.GetSkillBonusAgainst("Bluff", target.Stats);

        bool targetIsHumanoid = false;
        if (target.Stats != null && !string.IsNullOrEmpty(target.Stats.CreatureType))
            targetIsHumanoid = target.Stats.CreatureType.Trim().ToLowerInvariant().Contains("humanoid");

        bool useBabWisDefense = !targetIsHumanoid || (target.Stats != null && target.Stats.IsMindless);
        int opposedBonus = useBabWisDefense
            ? (target.Stats.BaseAttackBonus + target.Stats.WISMod + RangerRules.FavoredEnemySkillBonus(target.Stats, Stats, "Sense Motive"))
            : target.Stats.GetSkillBonusAgainst("Sense Motive", Stats);

        int bluffTotal = bluffRoll + bluffBonus;
        int opposedTotal = opposedRoll + opposedBonus;
        bool success = bluffTotal >= opposedTotal;

        string opposedLabel = useBabWisDefense
            ? (target.Stats != null && target.Stats.IsMindless ? "BAB + WIS (mindless)" : "BAB + WIS (non-humanoid)")
            : "Sense Motive";

        if (success)
            RegisterSuccessfulFeint(target);

        return new SpecialAttackResult
        {
            ManeuverName = "Feint",
            Success = success,
            CheckRoll = bluffRoll,
            CheckTotal = bluffTotal,
            OpposedRoll = opposedRoll,
            OpposedTotal = opposedTotal,
            Log = success
                ? $"{Stats.CharacterName} feints {target.Stats.CharacterName}! Bluff ({bluffRoll}+{bluffBonus}={bluffTotal}) vs {opposedLabel} ({opposedRoll}+{opposedBonus}={opposedTotal}). Next melee attack by {Stats.CharacterName} before end of their next turn denies DEX-to-AC (unless target is already flat-footed)."
                : $"{Stats.CharacterName}'s feint fails: Bluff ({bluffRoll}+{bluffBonus}={bluffTotal}) vs {target.Stats.CharacterName}'s {opposedLabel} ({opposedRoll}+{opposedBonus}={opposedTotal})."
        };
    }

    private SpecialAttackResult ResolveCoupDeGrace(CharacterController target)
    {
        if (target == null || target.Stats == null)
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Coup de Grace",
                Success = false,
                Log = "Coup de Grace failed: invalid target."
            };
        }

        if (IsBlockedBySummonedContactBarrier(target, out _))
            return BuildSummonedContactBarrierResult(target, "Coup de Grace");

        if (!IsInCoupDeGraceReach(target))
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Coup de Grace",
                Success = false,
                Log = $"Coup de Grace failed: {target.Stats.CharacterName} is not adjacent."
            };
        }

        if (!target.IsHelplessForCoupDeGrace())
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Coup de Grace",
                Success = false,
                Log = $"Coup de Grace failed: {target.Stats.CharacterName} is not helpless."
            };
        }

        if (target.IsImmuneToCriticalHits())
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Coup de Grace",
                Success = false,
                Log = $"Coup de Grace failed: {target.Stats.CharacterName} is immune to critical hits ({target.Stats.CreatureType})."
            };
        }

        ItemData weapon = GetEquippedMainWeapon();
        ResolveBaseAttackDamageProfile(weapon, out int damageDice, out int damageCount, out int bonusDamage, out string attackLabel);

        // The shared damage modifier (CMB-003): the same terms as a melee attack with this weapon or natural attack
        // (Strength share, enhancement, Weapon Specialization, morale, conditions). Power Attack trades an attack roll
        // penalty for damage (PHB p.98) and a coup de grace makes no attack roll (PHB p.153), so Power Attack adds
        // nothing to it (owner interpretation 2026-10-11, CMB-160).
        NaturalAttackDefinition cdgNatural = ShouldUseInnateNaturalAttackProfile(weapon) ? Stats.GetPrimaryNaturalAttack() : null;
        AttackCalculator.FeatModifiers cdgFeats = cdgNatural != null ? default(AttackCalculator.FeatModifiers) : DamageOnlyFeatModifiers(weapon);
        WeaponDamageBreakdown cdgDamage = BuildWeaponDamageBonus(weapon, isRanged: false, isOffHand: false, cdgFeats,
            bonusDamage, naturalAttack: cdgNatural);
        // Bane: against its foe the weapon's enhancement bonus is +2 better on damage too (DMG p.224), as on any hit.
        if (weapon != null && weapon.IsEnchanted)
            cdgDamage.BaneBonus = EnchantmentEffects.GetBaneEnhancementBonus(weapon, target.Stats.CreatureType ?? "");
        // A ranger's favored enemy bonus, as on any weapon damage roll against the creature (PHB p.47; CHR-020).
        cdgDamage.FavoredEnemyBonus = RangerRules.FavoredEnemyDamageBonus(Stats, target.Stats);
        // A coup de grace is an automatic critical hit (PHB p.153): the weapon damage, static modifier included, is
        // rolled the weapon's multiplier number of times (PHB p.134, p.140), the same roll as a confirmed critical on
        // any attack path (WeaponDamageRoll, CMB-004). Unarmed and natural attacks are x2 (ResolveWeaponCritProfile).
        ResolveWeaponCritProfile(weapon, out _, out int critMultiplier);

        // Fortification on the target's armor or shield is rolled first, as on any attack path: a negated critical is a
        // normal hit, so the damage is rolled once, with no sneak attack and nothing that needs a critical hit (burst
        // dice, a specific weapon's OnCriticalHit) (DMG p.219; delegated RAW reading 2026-10-14, ITM-067).
        var cdgNotes = new List<string>();
        bool sneakEligible = Stats.IsRogue && !IsTargetImmuneToSneakAttackDamage(target);
        bool critNegated = RollDefenderFortification(target, out int fortPercent);
        if (critNegated)
            cdgNotes.Add($"Fortification ({fortPercent}%): critical hit{(sneakEligible ? " and sneak attack" : string.Empty)} negated, damage rolled normally.");

        WeaponDamageRoll cdgRoll = WeaponDamageRoll.Roll(damageCount, damageDice, cdgDamage.Total, critNegated ? 1 : critMultiplier);
        int weaponDamage = cdgRoll.Total;

        // Sneak attack dice are extra damage over the weapon's normal damage: added once, never multiplied (PHB p.134,
        // p.140), and a rogue gets them against a helpless target on a coup de grace (PHB p.153).
        int sneakDamage = 0;
        int sneakDice = 0;
        bool sneakApplied = false;
        if (sneakEligible && !critNegated)
        {
            int rogueLevel = Stats.GetClassLevel("Rogue");
            sneakDice = CombatUtils.GetSneakAttackDice(rogueLevel);
            sneakDamage = CombatUtils.RollSneakAttackDamage(rogueLevel);
            sneakApplied = sneakDamage > 0;
        }

        // Everything after the weapon roll is the same as on any attack path (CMB-004): the special-ability dice added
        // once (burst dice only on a critical that stands), then the specific-item hooks. No natural 20, so no Vorpal.
        int riderDamage = RollWeaponEnchantmentRiderDamage(weapon, target, critConfirmed: !critNegated, critEffectsOnly: false,
            critMultiplier, naturalRoll: 0, cdgNotes, out _);
        int rawCriticalDamage = weaponDamage + sneakDamage + riderDamage;
        int specificDelta = ApplySpecificItemDamageHooks(weapon, target, critConfirmed: !critNegated, critEffectsOnly: false,
            ref rawCriticalDamage, cdgNotes);
        weaponDamage += specificDelta;

        var damageTypes = weapon != null
            ? weapon.GetDamageTypes()
            : new HashSet<DamageType> { DamageType.Bludgeoning };

        DamageBypassTag attackTags = GetWeaponAttackBypassTags(weapon, isRangedAttack: false);

        var packet = new DamagePacket
        {
            RawDamage = rawCriticalDamage,
            Types = damageTypes,
            AttackTags = attackTags,
            IsRanged = false,
            IsNonlethal = false,
            Source = AttackSource.Weapon,
            SourceName = weapon != null ? weapon.Name : "unarmed strike"
        };

        int hpBefore = target.Stats.CurrentHP;
        DamageResolutionResult mitigation = target.Stats.ApplyIncomingDamage(rawCriticalDamage, packet);
        int finalDamage = mitigation.FinalDamage;
        int hpAfterDamage = target.Stats.CurrentHP;

        int fortRoll = 0;
        int fortTotal = 0;
        int fortMod = 0;
        string fortNatural = string.Empty;
        int saveDC = 10 + Mathf.Max(0, finalDamage);
        bool fortSucceeded = false;
        bool diedFromDamage = hpAfterDamage <= -10;

        if (!diedFromDamage)
        {
            // The one save roll (SaveRollRules, D13; SPL-018): a natural 20 survives, a natural 1 dies (PHB p.153, p.177).
            SaveRoll fortSave = SaveRollRules.Roll(target.Stats, SavingThrowType.Fortitude, saveDC,
                SaveContext.Effect("Coup de Grace", this), "Coup de Grace Fort save");
            fortRoll = fortSave.Roll;
            fortMod = fortSave.Modifier;
            fortTotal = fortSave.Total;
            fortSucceeded = fortSave.Succeeded;
            fortNatural = fortSave.NaturalSuffix;

            if (!fortSucceeded)
            {
                target.Stats.CurrentHP = -10;
                target.SyncHPStateFromCurrentHP(emitLog: false);
            }
        }

        bool targetKilled = target.Stats.IsDead;
        if (targetKilled)
            target.OnDeath();

        string mitigationSummary = mitigation.GetMitigationSummary();
        string sneakSegment = sneakApplied
            ? $" + sneak attack {sneakDice}d6({sneakDamage}), not multiplied"
            : string.Empty;
        string damageSource = weapon != null ? weapon.Name : attackLabel;
        string riderSegment = riderDamage != 0 ? $" + special abilities {riderDamage}, not multiplied" : string.Empty;
        string specificSegment = specificDelta != 0 ? $" {(specificDelta > 0 ? "+" : "-")} item {Mathf.Abs(specificDelta)}" : string.Empty;
        string rollSegment = critNegated
            ? $"critical negated, normal damage {cdgRoll.Describe()}"
            : $"automatic critical {cdgRoll.Describe()}";
        string notesSegment = cdgNotes.Count > 0 ? string.Join(" ", cdgNotes) + " " : string.Empty;

        string saveLine = diedFromDamage
            ? $"{target.Stats.CharacterName} is slain by damage before the Fortitude save can matter."
            : $"Fortitude save: d20({fortRoll}) + {fortMod} = {fortTotal} vs DC {saveDC} {(fortSucceeded ? "SUCCESS" : "FAILURE")}{fortNatural}.";

        string deathLine = diedFromDamage
            ? $"{target.Stats.CharacterName} is dead."
            : (!fortSucceeded
                ? $"{target.Stats.CharacterName} dies from the failed save."
                : (targetKilled ? $"{target.Stats.CharacterName} is dead." : $"{target.Stats.CharacterName} survives."));

        return new SpecialAttackResult
        {
            ManeuverName = "Coup de Grace",
            Success = true,
            DamageDealt = finalDamage,
            TargetKilled = targetKilled,
            WeaponDamageRoll = cdgRoll,
            SneakAttackDamage = sneakDamage,
            ExtraDamage = riderDamage + specificDelta,
            CritNegated = critNegated,
            RawDamage = rawCriticalDamage,
            Log = $"{Stats.CharacterName} performs Coup de Grace on helpless {target.Stats.CharacterName} with {damageSource}: "
                + $"{rollSegment} (modifier per roll: {cdgDamage.Describe()}){specificSegment}{sneakSegment}{riderSegment} = {rawCriticalDamage}; "
                + notesSegment
                + $"mitigated to {finalDamage} ({hpBefore} → {target.Stats.CurrentHP}). "
                + (string.IsNullOrEmpty(mitigationSummary) ? string.Empty : mitigationSummary + " ")
                + saveLine + " " + deathLine
        };
    }

    public int GetGrappleModifier(int? baseAttackBonusOverride = null)
    {
        return EnsureCombatStats().GetGrappleModifier(baseAttackBonusOverride);
    }

    private struct DisarmCheckResult
    {
        public bool Success;
        public int AttackerRoll;
        public int AttackerTotal;
        public int DefenderRoll;
        public int DefenderTotal;
        public string BreakdownLog;

        public static DisarmCheckResult CreateAutomaticFailure(
            ItemData attackerHeldItem,
            ItemData defenderHeldItem,
            EquipSlot defenderHeldSlot,
            string reason)
        {
            string attackerHeldLabel = attackerHeldItem != null ? attackerHeldItem.Name : "Unarmed Strike";
            string defenderHeldLabel = defenderHeldItem != null ? defenderHeldItem.Name : $"Held Item ({defenderHeldSlot})";

            return new DisarmCheckResult
            {
                Success = false,
                AttackerRoll = 0,
                AttackerTotal = 0,
                DefenderRoll = 0,
                DefenderTotal = 99,
                BreakdownLog = $"Attacker Roll:\n  Held Item: {attackerHeldLabel}\n  Total: automatic failure\n\nDefender Roll:\n  Held Item: {defenderHeldLabel}\n  Total: automatic success\n  Reason: {reason}"
            };
        }
    }

    /// <summary>
    /// Handedness modifier of a natural weapon on a disarm roll: a one-handed weapon of the creature's own
    /// size, so 0 (owner ruling 2026-10-08, CMB-102; PHB p.155). <see cref="ResolveSunder"/> uses the same
    /// value for a natural weapon on the sunder roll (PHB p.158; owner interpretation 2026-10-11, CMB-141).
    /// </summary>
    public const int NaturalWeaponDisarmHandednessModifier = 0;

    /// <summary>
    /// Handedness modifier of an unarmed strike on a sunder roll: a light weapon (PHB p.116 Table 7-5), so -4 (PHB
    /// p.158; owner interpretation 2026-10-11, CMB-141), for a PC's unarmed strike and an NPC's "Unarmed Strike" natural attack.
    /// </summary>
    public const int UnarmedStrikeSunderHandednessModifier = -4;

    /// <summary>
    /// True when a sunder by this creature now would deal nonlethal damage (PHB p.139, p.146): a plain unarmed strike
    /// without Improved Unarmed Strike or a gauntlet, a nonlethal weapon such as a sap, or a weapon used in nonlethal
    /// mode. Objects are immune to nonlethal damage (PHB p.165). The rules allow the attempt (<see cref="ResolveSunder"/>
    /// deals the item nothing); the AI reads it to skip a pointless sunder. A natural attack is always lethal.
    /// </summary>
    public bool WouldSunderDealNonlethalDamage()
    {
        if (Stats == null)
            return false;

        ItemData weapon = GetEquippedMainWeapon();
        if (weapon == null && UsesInnateNaturalAttackSequence())
            return false;

        return GetSunderDamageModeProfile(weapon, null).DealNonlethalDamage;
    }

    /// <summary>
    /// The damage mode of a sunder made with <paramref name="weapon"/> (null: an unarmed strike) or the natural
    /// attack <paramref name="natural"/> (always lethal, no penalty), as the same attack would have it (PHB p.139,
    /// p.146; <see cref="ResolveDamageModeAttackProfileCore"/>).
    /// </summary>
    private DamageModeAttackProfile GetSunderDamageModeProfile(ItemData weapon, NaturalAttackDefinition natural)
    {
        if (weapon == null && natural != null)
            return default(DamageModeAttackProfile);

        return ResolveDamageModeAttackProfileCore(weapon, unarmedStrike: weapon == null);
    }

    /// <summary>
    /// PHB p.155: a disarmer that attempted the disarm unarmed now has the weapon; an armed one knocks
    /// it to the ground in the defender's square. A creature attacking with its natural weapons is
    /// armed (MM p.312), so it does not catch the weapon, and its natural sequence goes on (CMB-102).
    /// An unarmed strike listed as a natural attack (no shipped entry since CRE-042) is unarmed (<see cref="FightsWithNaturalWeapons"/>).
    /// </summary>
    private static bool DisarmerCatchesWeapon(CharacterController disarmer, ItemData disarmerHeldWeapon)
        => disarmerHeldWeapon == null && (disarmer == null || !disarmer.FightsWithNaturalWeapons());

    private static DisarmCheckResult RollDisarmCheck(
        CharacterController attacker,
        CharacterController defender,
        ItemData attackerHeldItem,
        ItemData defenderHeldItem,
        EquipSlot defenderHeldSlot,
        int defenderSpecialResistBonus,
        string lockedGauntletReason,
        int? attackerBaseAttackBonusOverride = null,
        int attackerDualWieldPenaltyForLog = 0)
    {
        // An unarmed strike counts as a light weapon (-4, PHB p.155), also when an NPC lists it as a natural
        // attack (the monks; FightsWithNaturalWeapons). A natural weapon is not an unarmed strike (MM p.312:
        // armed); it counts as a one-handed weapon of the creature's own size, so it gets neither the
        // two-handed +4 nor the light -4 (owner ruling 2026-10-08, CMB-102). The larger combatant gets +4 per
        // size category of difference (PHB p.155); the smaller one takes no penalty. The size term compares the
        // combatants, as PHB p.155 and p.158 do (rules as written, recorded 2026-10-11, CMB-141; the 2026-10-08 disarm ruling
        // worded it as the two items, which is the same while each weapon is its wielder's size, ITM-071).
        bool attackerUsesNaturalWeapon = attackerHeldItem == null && attacker.FightsWithNaturalWeapons();
        int atkHeldItemMod = attackerUsesNaturalWeapon
            ? NaturalWeaponDisarmHandednessModifier
            : GetDisarmHeldItemModifier(attackerHeldItem, treatUnarmedAsLight: true);
        int atkSizeDiffMod = GetLargerCombatantSizeBonus(attacker, defender);
        int atkImprovedDisarmMod = attacker.Stats.HasFeat("Improved Disarm") ? 4 : 0;

        int defHeldItemMod = GetDisarmHeldItemModifier(defenderHeldItem, treatUnarmedAsLight: false);
        int defNonMeleeHeldItemPenalty = GetDisarmNonMeleeHeldItemPenalty(defenderHeldItem);
        int defSizeDiffMod = GetLargerCombatantSizeBonus(defender, attacker);
        // Improved Disarm helps only the creature making the disarm attempt (PHB p.95), so the
        // defender never adds it here; a counter-disarm swaps the roles (CMB-014).
        const int defImprovedDisarmMod = 0;

        int atkRoll = DiceService.D20("Disarm attack roll");
        int defRoll = DiceService.D20("Disarm defense roll");

        int attackerBaseAttackBonusUsed = attackerBaseAttackBonusOverride ?? attacker.Stats.BaseAttackBonus;
        int attackerBaseAttackBonusRaw = attacker.Stats.BaseAttackBonus;
        int attackerIterativeAdjustment = attackerBaseAttackBonusUsed - attackerBaseAttackBonusRaw - attackerDualWieldPenaltyForLog;

        // Both rolls are opposed attack rolls (PHB p.155), so each side's racial attack bonus against the other's kind
        // applies (dwarf +1 against orcs and goblinoids, gnome +1 against kobolds and goblinoids; PHB p.15, p.17; CHR-019).
        int atkRacialMod = RacialTraitRules.AttackBonusAgainst(attacker.Stats, defender.Stats);
        int defRacialMod = RacialTraitRules.AttackBonusAgainst(defender.Stats, attacker.Stats);

        // The flurry penalty on either side's opposed attack roll while its flurry lasts (PHB p.40; CHR-003).
        int atkFlurryMod = attacker.FlurryAttackRollPenalty;
        int defFlurryMod = defender.FlurryAttackRollPenalty;

        int atkTotal = atkRoll + attackerBaseAttackBonusUsed + attacker.Stats.STRMod + attacker.Stats.SizeModifier + attacker.Stats.ConditionAttackPenalty
                       + atkHeldItemMod + atkSizeDiffMod + atkImprovedDisarmMod + atkRacialMod + atkFlurryMod;
        int defTotal = defRoll + defender.Stats.BaseAttackBonus + defender.Stats.STRMod + defender.Stats.SizeModifier + defender.Stats.ConditionAttackPenalty
                       + defHeldItemMod + defNonMeleeHeldItemPenalty + defSizeDiffMod + defImprovedDisarmMod + defenderSpecialResistBonus + defRacialMod
                       + defFlurryMod;

        string attackerHeldLabel = attackerHeldItem != null ? attackerHeldItem.Name : attackerUsesNaturalWeapon ? "Natural weapon" : "Unarmed Strike";
        string defenderHeldLabel = defenderHeldItem != null ? defenderHeldItem.Name : $"Held Item ({defenderHeldSlot})";

        string atkBreakdown = BuildDisarmDetailedRollSection(
            title: "Attacker Roll:",
            d20Roll: atkRoll,
            baseAttackBonus: attackerBaseAttackBonusRaw,
            iterativeAdjustment: attackerIterativeAdjustment,
            dualWieldPenalty: attackerDualWieldPenaltyForLog,
            strengthModifier: attacker.Stats.STRMod,
            sizeModifier: attacker.Stats.SizeModifier,
            conditionModifier: attacker.Stats.ConditionAttackPenalty,
            weaponModifier: atkHeldItemMod,
            sizeDifferenceModifier: atkSizeDiffMod,
            nonMeleePenalty: 0,
            improvedDisarmModifier: atkImprovedDisarmMod,
            racialModifier: atkRacialMod,
            specialModifier: atkFlurryMod,
            specialModifierLabel: atkFlurryMod != 0 ? "Flurry of Blows" : string.Empty,
            heldItemLabel: attackerHeldLabel,
            total: atkTotal);

        string defBreakdown = BuildDisarmDetailedRollSection(
            title: "Defender Roll:",
            d20Roll: defRoll,
            baseAttackBonus: defender.Stats.BaseAttackBonus,
            iterativeAdjustment: 0,
            dualWieldPenalty: 0,
            strengthModifier: defender.Stats.STRMod,
            sizeModifier: defender.Stats.SizeModifier,
            conditionModifier: defender.Stats.ConditionAttackPenalty,
            weaponModifier: defHeldItemMod,
            sizeDifferenceModifier: defSizeDiffMod,
            nonMeleePenalty: defNonMeleeHeldItemPenalty,
            improvedDisarmModifier: defImprovedDisarmMod,
            racialModifier: defRacialMod,
            specialModifier: defenderSpecialResistBonus + defFlurryMod,
            specialModifierLabel: defFlurryMod == 0 ? lockedGauntletReason
                : string.IsNullOrEmpty(lockedGauntletReason) ? "Flurry of Blows" : lockedGauntletReason + ", Flurry of Blows",
            heldItemLabel: defenderHeldLabel,
            total: defTotal);

        return new DisarmCheckResult
        {
            Success = DoesAttackerWinOpposedCheck(atkTotal, atkTotal - atkRoll, defTotal, defTotal - defRoll),
            AttackerRoll = atkRoll,
            AttackerTotal = atkTotal,
            DefenderRoll = defRoll,
            DefenderTotal = defTotal,
            BreakdownLog = $"{atkBreakdown}\n\n{defBreakdown}"
        };
    }

    private static string BuildDisarmDetailedRollSection(
        string title,
        int d20Roll,
        int baseAttackBonus,
        int iterativeAdjustment,
        int dualWieldPenalty,
        int strengthModifier,
        int sizeModifier,
        int conditionModifier,
        int weaponModifier,
        int sizeDifferenceModifier,
        int nonMeleePenalty,
        int improvedDisarmModifier,
        int racialModifier,
        int specialModifier,
        string specialModifierLabel,
        string heldItemLabel,
        int total)
    {
        var sb = new StringBuilder();
        sb.AppendLine(title);
        if (!string.IsNullOrEmpty(heldItemLabel))
            sb.AppendLine($"  Held Item: {heldItemLabel}");

        sb.AppendLine($"  d20: {d20Roll}");
        sb.AppendLine($"  BAB: {FormatSignedDisarmModifier(baseAttackBonus)}");
        if (iterativeAdjustment != 0)
            sb.AppendLine($"  Iterative: {FormatSignedDisarmModifier(iterativeAdjustment)}");
        if (dualWieldPenalty != 0)
            sb.AppendLine($"  Dual Wield: {FormatSignedDisarmModifier(dualWieldPenalty)}");
        sb.AppendLine($"  STR: {FormatSignedDisarmModifier(strengthModifier)}");
        sb.AppendLine($"  Size: {FormatSignedDisarmModifier(sizeModifier)}");
        if (conditionModifier != 0)
            sb.AppendLine($"  Condition: {FormatSignedDisarmModifier(conditionModifier)}");
        sb.AppendLine($"  Weapon: {FormatSignedDisarmModifier(weaponModifier)}");
        if (nonMeleePenalty != 0)
            sb.AppendLine($"  Non-Melee Item: {FormatSignedDisarmModifier(nonMeleePenalty)}");
        if (sizeDifferenceModifier != 0)
            sb.AppendLine($"  Size Difference: {FormatSignedDisarmModifier(sizeDifferenceModifier)}");
        if (improvedDisarmModifier != 0)
            sb.AppendLine($"  Improved Disarm: {FormatSignedDisarmModifier(improvedDisarmModifier)}");
        if (racialModifier != 0)
            sb.AppendLine($"  Racial: {FormatSignedDisarmModifier(racialModifier)}");
        if (specialModifier != 0)
        {
            string specialLabel = string.IsNullOrWhiteSpace(specialModifierLabel) ? "Special" : specialModifierLabel;
            sb.AppendLine($"  {specialLabel}: {FormatSignedDisarmModifier(specialModifier)}");
        }

        sb.AppendLine($"  Total: {BuildDisarmEquation(d20Roll, baseAttackBonus, iterativeAdjustment, dualWieldPenalty, strengthModifier, sizeModifier, conditionModifier, weaponModifier, nonMeleePenalty, sizeDifferenceModifier, improvedDisarmModifier, racialModifier, specialModifier)} = {total}");
        return sb.ToString().TrimEnd();
    }

    private static string BuildDisarmEquation(int d20Roll, params int[] modifiers)
    {
        var sb = new StringBuilder();
        sb.Append(d20Roll);
        for (int i = 0; i < modifiers.Length; i++)
        {
            int value = modifiers[i];
            if (value >= 0)
                sb.Append($" + {value}");
            else
                sb.Append($" - {Mathf.Abs(value)}");
        }

        return sb.ToString();
    }

    private static string FormatSignedDisarmModifier(int value)
    {
        return value >= 0 ? $"+{value}" : value.ToString();
    }

    private static bool IsSpikedGauntletItem(ItemData item)
    {
        return CharacterEquipment.IsSpikedGauntletItem(item);
    }

    private static bool IsShieldBashWeapon(ItemData item)
    {
        return CharacterEquipment.IsShieldBashWeapon(item);
    }

    private static bool IsGauntletItem(ItemData item)
    {
        return CharacterEquipment.IsGauntletItem(item);
    }

    private void SuppressShieldBonusForShieldBash(ItemData shield)
    {
        EnsureEquipment().SuppressShieldBonusForShieldBash(shield);
    }

    private void RestoreShieldBonusAfterShieldBash()
    {
        EnsureEquipment().RestoreShieldBonusAfterShieldBash();
    }

    private static bool HasLockedGauntletEquipped(CharacterController character)
    {
        ItemData handsItem = GetEquippedHandsItem(character);
        if (handsItem == null)
            return false;

        string id = (handsItem.Id ?? string.Empty).ToLowerInvariant();
        string name = (handsItem.Name ?? string.Empty).ToLowerInvariant();
        return id == ItemIDs.LOCKED_GAUNTLET || name.Contains("locked gauntlet");
    }

    private static ItemData GetEquippedHandsItem(CharacterController character)
    {
        return character != null ? character.EnsureInventory().GetEquippedHandsItem() : null;
    }

    private static bool TryEquipDisarmedItem(CharacterController receiver, ItemData disarmedItem, out EquipSlot equippedSlot)
    {
        equippedSlot = EquipSlot.None;
        return receiver != null && receiver.EnsureInventory().TryEquipDisarmedItem(disarmedItem, out equippedSlot);
    }

    private static void DropItemToGround(CharacterController owner, ItemData item)
    {
        if (owner == null || item == null)
            return;

        SquareGrid grid = GameManager.Instance != null ? GameManager.Instance.Grid : SquareGrid.Instance;
        SquareCell cell = grid != null ? grid.GetCell(owner.GridPosition) : null;
        if (cell != null)
            cell.AddGroundItem(item);
    }

    public int DropHeldItemsDueToCondition(string reason)
    {
        CharacterInventory inventory = EnsureInventory();
        if (inventory == null)
            return 0;

        int droppedCount = 0;
        string ownerName = Stats != null ? Stats.CharacterName : name;

        ItemData rightHand = inventory.RemoveEquippedHeldItem(EquipSlot.RightHand);
        if (rightHand != null)
        {
            DropItemToGround(this, rightHand);
            droppedCount++;
            GameManager.Instance?.CombatUI?.ShowCombatLog(CombatLogHelper.Info("💨", $"{ownerName} drops {rightHand.Name} ({reason})."));
        }

        ItemData leftHand = inventory.RemoveEquippedHeldItem(EquipSlot.LeftHand);
        if (leftHand != null)
        {
            DropItemToGround(this, leftHand);
            droppedCount++;
            GameManager.Instance?.CombatUI?.ShowCombatLog(CombatLogHelper.Info("💨", $"{ownerName} drops {leftHand.Name} ({reason})."));
        }

        return droppedCount;
    }

    private static int GetDisarmHeldItemModifier(CharacterController character)
    {
        ItemData heldItem = character != null ? character.GetEquippedMainWeapon() : null;
        return GetDisarmHeldItemModifier(heldItem, treatUnarmedAsLight: true);
    }

    private static int GetDisarmHeldItemModifier(ItemData heldItem, bool treatUnarmedAsLight)
    {
        if (heldItem == null)
            return treatUnarmedAsLight ? -4 : 0;

        if (!heldItem.IsWeapon)
            return 0;

        if (heldItem.WeaponSize == WeaponSizeCategory.TwoHanded || heldItem.IsTwoHanded)
            return 4;

        if (heldItem.WeaponSize == WeaponSizeCategory.Light || heldItem.IsLightWeapon)
            return -4;

        return 0;
    }

    /// <summary>
    /// The size term of the disarm and sunder opposed rolls (PHB p.155, p.158): the larger combatant gets +4
    /// per size category of difference; the smaller one gets nothing (no penalty), so the swing is 4 per category.
    /// It compares the combatants, not the items (PHB p.155, p.158; recorded 2026-10-11, CMB-141).
    /// </summary>
    private static int GetLargerCombatantSizeBonus(CharacterController actor, CharacterController opponent)
    {
        if (actor == null || opponent == null || actor.Stats == null || opponent.Stats == null)
            return 0;

        int sizeStepDifference = (int)actor.Stats.CurrentSizeCategory - (int)opponent.Stats.CurrentSizeCategory;
        return Mathf.Max(0, sizeStepDifference) * 4;
    }

    /// <summary>
    /// The weapon the defender of a sunder rolls with (PHB p.158 Step 2, "your respective weapons"): the targeted weapon
    /// when a weapon is the target; otherwise the melee weapon it holds, the main hand first, else one held in the off
    /// hand, else a worn spiked gauntlet. A shield is never its weapon, and neither is a ranged weapon (handedness is a
    /// melee weapon trait, PHB p.113). Null when it holds no melee weapon, so it rolls with a natural weapon or its unarmed
    /// strike (delegated RAW reading 2026-10-14, CMB-165).
    /// </summary>
    public static ItemData GetSunderDefenderWeapon(CharacterController defender, ItemData targetItem)
    {
        if (targetItem != null && targetItem.IsWeapon && !targetItem.IsShield)
            return targetItem;

        if (defender == null)
            return null;

        Inventory inv = defender.GetInventoryData();
        if (inv != null)
        {
            if (IsSunderDefenderMeleeWeapon(inv.RightHandSlot))
                return inv.RightHandSlot;
            if (IsSunderDefenderMeleeWeapon(inv.LeftHandSlot))
                return inv.LeftHandSlot;
        }

        ItemData hands = GetEquippedHandsItem(defender);
        return IsSpikedGauntletItem(hands) ? hands : null;
    }

    private static bool IsSunderDefenderMeleeWeapon(ItemData item)
        => item != null && item.IsWeapon && !item.IsShield && !item.IsRangedWeapon;

    /// <summary>
    /// The defender's handedness term on the sunder opposed roll (PHB p.158 Step 2): its weapon's term when it rolls with
    /// a held weapon; with none, a true natural weapon (claw, bite, slam) is a one-handed weapon of its size (0,
    /// <see cref="NaturalWeaponDisarmHandednessModifier"/>), and otherwise it rolls with its unarmed strike, always a
    /// light weapon (-4, <see cref="UnarmedStrikeSunderHandednessModifier"/>; PHB p.113, p.139), an "Unarmed Strike"
    /// listed as a natural attack included (no shipped entry since CRE-042). No damage-mode -4: only the attacker deals damage (PHB p.158
    /// Step 3) (delegated RAW reading 2026-10-14, CMB-165). The same for PCs and NPCs.
    /// </summary>
    public static int GetSunderDefenderHandednessModifier(CharacterController defender, ItemData defenderWeapon, out string label)
    {
        if (defenderWeapon != null)
        {
            label = "handedness";
            return GetSunderHandednessModifier(defenderWeapon);
        }

        if (defender != null && defender.FightsWithNaturalWeapons())
        {
            label = "natural weapon";
            return NaturalWeaponDisarmHandednessModifier;
        }

        label = "unarmed strike (light)";
        return UnarmedStrikeSunderHandednessModifier;
    }

    /// <summary>
    /// The handedness term of a sunder opposed roll for the weapon a combatant rolls with (PHB p.158): +4 for a
    /// two-handed weapon, -4 for a light one, 0 for a one-handed weapon, a non-weapon or no weapon.
    /// </summary>
    private static int GetSunderHandednessModifier(ItemData weapon)
    {
        if (weapon == null || !weapon.IsWeapon)
            return 0;

        if (IsWeaponTwoHanded(weapon) || weapon.WeaponSize == WeaponSizeCategory.TwoHanded)
            return 4;

        if (weapon.IsLightWeapon || weapon.WeaponSize == WeaponSizeCategory.Light)
            return -4;

        return 0;
    }

    private static int GetDisarmNonMeleeHeldItemPenalty(ItemData heldItem)
    {
        if (heldItem == null)
            return 0;

        if (heldItem.IsShield)
            return -4;

        if (heldItem.IsWeapon)
            return heldItem.WeaponCat == WeaponCategory.Melee ? 0 : -4;

        // Non-weapon held items (wands, rods, etc.) are easier to disarm.
        return -4;
    }

    private static string BuildDisarmModifierBreakdown(
        string sideLabel,
        string heldItemLabel,
        int heldItemModifier,
        int sizeDifferenceModifier,
        int nonMeleePenalty,
        int improvedFeatModifier,
        int specialGearModifier = 0,
        string specialGearLabel = "")
    {
        string heldItemPart = heldItemModifier != 0 ? $"weapon {heldItemModifier:+#;-#;0}" : "weapon +0";
        string sizePart = sizeDifferenceModifier != 0 ? $"size {sizeDifferenceModifier:+#;-#;0}" : "size +0";
        string nonMeleePart = nonMeleePenalty != 0 ? $", non-melee {nonMeleePenalty:+#;-#;0}" : string.Empty;
        string featPart = improvedFeatModifier != 0 ? $", Improved Disarm {improvedFeatModifier:+#;-#;0}" : string.Empty;
        string gearPart = specialGearModifier != 0
            ? $", {specialGearLabel} {specialGearModifier:+#;-#;0}"
            : (!string.IsNullOrEmpty(specialGearLabel) ? $", {specialGearLabel}" : string.Empty);

        return $"{sideLabel}[{heldItemLabel}: {heldItemPart}, {sizePart}{nonMeleePart}{featPart}{gearPart}]";
    }

    private static bool TryGetDisarmTargetHeldItem(CharacterController target, EquipSlot? preferredTargetSlot, out ItemData heldItem, out EquipSlot handSlot)
    {
        heldItem = null;
        handSlot = EquipSlot.None;
        if (target == null) return false;

        var options = target.GetDisarmableHeldItemOptions();
        if (options.Count == 0)
            return false;

        if (preferredTargetSlot.HasValue)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].HandSlot == preferredTargetSlot.Value)
                {
                    handSlot = options[i].HandSlot;
                    heldItem = options[i].HeldItem;
                    return heldItem != null;
                }
            }
        }

        handSlot = options[0].HandSlot;
        heldItem = options[0].HeldItem;
        return heldItem != null;
    }

    private static void DestroyEquippedMainWeapon(CharacterController target)
    {
        DestroyEquippedHeldItem(target, null);
    }

    private static void DestroyEquippedHeldItem(CharacterController target, EquipSlot? handSlot)
    {
        RemoveEquippedHeldItem(target, handSlot);
    }

    private static void DestroyEquippedItem(CharacterController target, EquipSlot slot)
    {
        if (target == null)
            return;

        bool removed = target.EnsureInventory().DestroyEquippedItem(slot, out ItemData destroyedItem);
        if (destroyedItem == null)
            return;

        if (removed)
        {
            string ownerName = target.Stats != null ? target.Stats.CharacterName : "Unknown";
            Debug.Log($"[Sunder] Destroyed item removed from inventory: {destroyedItem.Name} (owner: {ownerName})");
        }
        else
        {
            Debug.LogWarning($"[Sunder] Failed to remove destroyed item reference: {destroyedItem.Name}");
        }
    }

    private static ItemData RemoveEquippedHeldItem(CharacterController target, EquipSlot? handSlot)
    {
        return target != null ? target.EnsureInventory().RemoveEquippedHeldItem(handSlot) : null;
    }

    // ========== 5-FOOT STEP ==========

    /// <summary>
    /// Perform a 5-foot step (1 square move that does NOT provoke AoOs).
    /// D&D 3.5: 5-foot step can be taken if no other movement this turn.
    /// </summary>
    /// <param name="targetCell">The adjacent cell to step to.</param>
    /// <returns>True if the 5-foot step was successful.</returns>
    public bool FiveFootStep(SquareCell targetCell)
    {
        if (targetCell == null) return false;

        // Must be adjacent (1 square away)
        if (!SquareGridUtils.IsAdjacent(GridPosition, targetCell.Coords))
        {
            Debug.Log($"[5ftStep] {Stats.CharacterName}: Target not adjacent, cannot 5-foot step");
            return false;
        }

        // Must not have moved this turn
        if (HasMovedThisTurn)
        {
            Debug.Log($"[5ftStep] {Stats.CharacterName}: Already moved this turn, cannot 5-foot step");
            return false;
        }

        // Can only take one 5-foot step per turn
        if (HasTakenFiveFootStep)
        {
            Debug.Log($"[5ftStep] {Stats.CharacterName}: Already used 5-foot step this turn");
            return false;
        }

        if (HasCondition(CombatConditionType.Prone) || HasCondition(CombatConditionType.Grappled))
        {
            Debug.Log($"[5ftStep] {Stats.CharacterName}: Invalid condition for 5-foot step");
            return false;
        }

        if (targetCell.IsOccupied)
        {
            Debug.Log($"[5ftStep] {Stats.CharacterName}: Target cell occupied, cannot 5-foot step");
            return false;
        }

        Debug.Log($"[5ftStep] {Stats.CharacterName} takes a 5-foot step to ({targetCell.Coords.x},{targetCell.Coords.y}) - NO AoO provoked");

        // Move without consuming normal movement/action economy.
        MoveToCell(targetCell, markAsMoved: false);
        HasTakenFiveFootStep = true;
        return true;
    }

    // ========== MONK: FLURRY OF BLOWS (Full-Round Action) ==========

    /// <summary>
    /// Declare a flurry of blows (<see cref="TryBeginFlurryOfBlows"/>, unless already declared this turn) and resolve all
    /// its remaining attacks against <paramref name="target"/> through <see cref="FullAttack"/>, the shared full attack
    /// (PHB p.40-41; CHR-003). The PC Flurry button and the AI step the same sequence one attack at a time instead
    /// (GameManager full attack sequence, PerformNPCMeleeAttackSequence); this one-call form is for tests and scripts.
    /// Returns an empty result when the flurry cannot be declared.
    /// </summary>
    public FullAttackResult FlurryOfBlows(CharacterController target, bool isFlanking, int flankingBonus, string flankingPartnerName, RangeInfo rangeInfo = null)
    {
        if (!TryBeginFlurryOfBlows(out string reason))
        {
            Debug.LogWarning($"[Monk] {Stats?.CharacterName ?? name}: cannot use Flurry of Blows: {reason}");
            return new FullAttackResult
            {
                Type = FullAttackResult.AttackType.FullAttack,
                Attacker = this,
                Defender = target,
                DefenderHPBefore = target != null && target.Stats != null ? target.Stats.CurrentHP : 0,
                DefenderHPAfter = target != null && target.Stats != null ? target.Stats.CurrentHP : 0
            };
        }

        return FullAttack(target, isFlanking, flankingBonus, flankingPartnerName, rangeInfo);
    }

    // ========== BARBARIAN: RAGE (Free Action) ==========

    /// <summary>
    /// Activate Barbarian Rage. This is a free action that can be done at the start of the turn.
    /// </summary>
    public bool ActivateRage()
    {
        if (Stats == null || !Stats.IsBarbarian)
        {
            Debug.LogWarning($"[Barbarian] Cannot rage - not a Barbarian!");
            return false;
        }

        bool success = Stats.ActivateRage();
        if (success)
        {
            Debug.Log($"[Barbarian] {Stats.CharacterName}: Rage activated via CharacterController! " +
                      $"AC now {Stats.ArmorClass} (rage -2 penalty applied)");
        }
        return success;
    }

    // ==================== COUNTERSPELL SYSTEM ====================

    /// <summary>
    /// Active readied counterspell data. Null when no counterspell is readied.
    /// D&D 3.5e PHB: Counterspelling requires a readied action (standard action on your turn).
    /// </summary>
    public CounterspellData ReadiedCounterspell { get; private set; }

    /// <summary>
    /// Whether this character currently has an active, untriggered counterspell readied.
    /// </summary>
    public bool HasReadiedCounterspell => ReadiedCounterspell != null && ReadiedCounterspell.IsActive;

    /// <summary>
    /// Ready a counterspell action targeting a specific enemy caster.
    /// Consumes the character's standard action for this turn.
    /// PHB: "On your turn, you declare a Ready action (standard action consumed)."
    /// </summary>
    /// <param name="watchedCaster">Specific enemy caster to watch. Null to watch any enemy.</param>
    /// <param name="currentRound">Current combat round number.</param>
    /// <returns>True if counterspell was successfully readied.</returns>
    public bool ReadyCounterspell(CharacterController watchedCaster, int currentRound)
    {
        if (!Actions.HasStandardAction)
        {
            Debug.Log($"[Counterspell] {Stats.CharacterName}: Cannot ready counterspell — no standard action available.");
            return false;
        }

        // Must be a spellcaster with available spell slots
        var spellComp = Spellcasting;
        if (spellComp == null || !spellComp.CanCastSpells)
        {
            Debug.Log($"[Counterspell] {Stats.CharacterName}: Cannot ready counterspell — not a spellcaster.");
            return false;
        }

        if (!spellComp.HasAnyCastablePreparedSpell())
        {
            Debug.Log($"[Counterspell] {Stats.CharacterName}: Cannot ready counterspell — no spell slots available.");
            return false;
        }

        // Consume standard action
        CommitStandardAction();

        // Create counterspell data
        ReadiedCounterspell = new CounterspellData
        {
            Counterspeller = this,
            WatchedCaster = watchedCaster,
            WatchAnyEnemy = (watchedCaster == null),
            SpellcraftBonus = Stats.GetSkillBonus("Spellcraft"),
            PreferDispelMagic = false,
            HasTriggered = false,
            ReadiedOnRound = currentRound
        };

        string targetDesc = watchedCaster != null
            ? watchedCaster.Stats.CharacterName
            : "any enemy";
        Debug.Log($"[Counterspell] {Stats.CharacterName}: Readied counterspell against {targetDesc} (Spellcraft +{ReadiedCounterspell.SpellcraftBonus})");

        return true;
    }

    /// <summary>
    /// Clear the readied counterspell (e.g., at start of next turn, or after triggering).
    /// </summary>
    public void ClearReadiedCounterspell()
    {
        if (ReadiedCounterspell != null)
        {
            Debug.Log($"[Counterspell] {Stats.CharacterName}: Readied counterspell cleared.");
            ReadiedCounterspell.Clear();
            ReadiedCounterspell = null;
        }
    }

    /// <summary>
    /// Roll a Spellcraft check to identify a spell being cast.
    /// DC = 15 + spell level (PHB p.82). Spellcraft is trained only: a creature without ranks cannot attempt the check at
    /// all and fails without a roll (PHB p.64, whose example is exactly this; <see cref="CharacterStats.CanAttemptSkill"/>).
    /// Identification is a step of a specific-spell counter only; Dispel Magic counters without it (PHB p.170).
    /// </summary>
    /// <param name="spellLevel">Level of the spell being cast.</param>
    /// <param name="roll">The d20 roll result.</param>
    /// <param name="total">Total check result (d20 + Spellcraft bonus).</param>
    /// <param name="dc">The DC to beat (15 + spell level).</param>
    /// <returns>True if the spell was successfully identified.</returns>
    public bool RollSpellcraftIdentification(int spellLevel, out int roll, out int total, out int dc)
    {
        dc = 15 + spellLevel;
        if (!Stats.CanAttemptSkill("Spellcraft"))
        {
            roll = 0;
            total = 0;
            Debug.Log($"[Counterspell] {Stats.CharacterName}: cannot attempt Spellcraft untrained (trained only, PHB p.64) → FAILED");
            return false;
        }
        int bonus = Stats.GetSkillBonus("Spellcraft");
        roll = DiceService.D20("Counterspell Spellcraft check");
        total = roll + bonus;
        bool success = total >= dc;
        Debug.Log($"[Counterspell] {Stats.CharacterName}: Spellcraft check d20({roll}) + {bonus} = {total} vs DC {dc} → {(success ? "IDENTIFIED" : "FAILED")}");
        return success;
    }

    /// <summary>
    /// Check if this character has a specific spell prepared and available to cast.
    /// Used for same-spell counterspell checking.
    /// Metamagic is ignored for matching (PHB).
    /// A counterspell is a casting of the counter (PHB p.170), so it needs the spell's components as any cast does
    /// (SpellComponentRules, PHB p.174; batch 11 step 17).
    /// </summary>
    public bool HasSpellAvailableForCounter(string spellId)
    {
        if (string.IsNullOrEmpty(spellId)) return false;
        var spellComp = Spellcasting;
        if (spellComp == null) return false;

        SpellData spell = SpellDatabase.GetSpell(spellId);
        if (spell == null) return false;

        return spellComp.CanCast(spell) && SpellComponentRules.Refusal(this, spell, null, out _) == null;
    }

    /// <summary>
    /// Check if this character has Dispel Magic available to cast.
    /// </summary>
    public bool HasDispelMagicAvailable()
    {
        return HasSpellAvailableForCounter(SpellNames.DISPEL_MAGIC);
    }

    /// <summary>
    /// Consume a spell slot for the counterspell (same spell or Dispel Magic).
    /// Both caster and counterspeller lose their spell slots per PHB.
    /// </summary>
    /// <param name="spellId">The spell ID to consume.</param>
    /// <returns>True if the spell slot was successfully consumed.</returns>
    public bool ConsumeSpellSlotForCounter(string spellId)
    {
        if (string.IsNullOrEmpty(spellId)) return false;
        var spellComp = Spellcasting;
        if (spellComp == null) return false;

        SpellData spell = SpellDatabase.GetSpell(spellId);
        if (spell == null) return false;

        bool consumed = spellComp.CastSpellFromSlot(spell);
        if (consumed)
        {
            Debug.Log($"[Counterspell] {Stats.CharacterName}: Consumed spell slot for {spell.Name}.");
            // The counter is a casting (PHB p.170): its priced material components go with the slot (PHB p.174).
            string componentLog = SpellComponentRules.SpendCostlyComponents(this, spell, null, null, out _);
            if (componentLog != null)
                GameManager.Instance?.CombatUI?.ShowCombatLog(CombatLogHelper.Info("💎", componentLog));
        }
        else
            Debug.LogWarning($"[Counterspell] {Stats.CharacterName}: Failed to consume spell slot for {spell.Name}!");
        return consumed;
    }

    /// <summary>
    /// Grapple actions that cost no action (PHB p.157: releasing a pinned opponent is a free action).
    /// Shared by the PC grapple flow, the AI grapple turn and the AI legal-action list.
    /// </summary>
    public static bool IsFreeGrappleAction(GrappleActionType actionType)
    {
        return actionType == GrappleActionType.ReleasePinnedOpponent;
    }
}