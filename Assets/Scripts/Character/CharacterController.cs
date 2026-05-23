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
    BreakPin,
    MoveHalfSpeed,
    UseOpponentWeapon,
    DisarmSmallObject,
    DrawLightWeapon,
    RetrieveSpellComponent,
    ReleasePinnedOpponent
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
        sb.AppendLine($"{CharacterName}'s grapple check:");
        sb.AppendLine($"  Base roll: 1d20 = {BaseRoll}");
        sb.AppendLine($"  BAB: {FormatSigned(BaseAttackBonus)}");
        sb.AppendLine($"  STR modifier: {FormatSigned(StrengthModifier)}");
        sb.AppendLine($"  Size modifier: {FormatSigned(SizeModifier)}");

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
    [HideInInspector] public bool WithdrawFirstStepProtected;

    /// <summary>Action economy tracker for the current turn.</summary>
    public ActionEconomy Actions = new ActionEconomy();

    /// <summary>
    /// Progressive attack pool used by the house-rule iterative attack flow.
    /// The pool is rebuilt at turn start and consumed as attack actions are committed.
    /// </summary>
    public AttackPool ProgressiveAttackPool { get; } = new AttackPool();

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
    [SerializeField] private int _bombardierAcidSprayCooldownRounds;
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
    public bool HasBreathWeapon => _breathWeapon != null;
    public bool IsBreathWeaponReady => _breathWeapon != null && _breathWeaponCooldownRemaining <= 0;
    /// <summary>Read-only access to breath weapon definition (does NOT consume/trigger cooldown).</summary>
    public BreathWeaponDefinition GetBreathWeaponDefinition() => _breathWeapon;

    // Secondary breath weapon (metallic dragons)
    [SerializeField] private SecondaryBreathWeaponDefinition _secondaryBreathWeapon;
    public bool HasSecondaryBreathWeapon => _secondaryBreathWeapon != null;
    public bool IsSecondaryBreathWeaponReady => _secondaryBreathWeapon != null && _secondaryBreathWeapon.UsesRemaining > 0;
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

    // Stench aura
    [SerializeField] private int _stenchAuraDC;
    [SerializeField] private int _stenchAuraRange;
    public bool HasStenchAura => _stenchAuraDC > 0;

    // Supernatural aura (babble, moan)
    [SerializeField] private AuraAbilityDefinition _auraAbility;
    public bool HasAuraAbility => _auraAbility != null;

    public int BombardierAcidSprayCooldownRounds => Mathf.Max(0, _bombardierAcidSprayCooldownRounds);
    public bool HasBombardierAcidSprayReady => _bombardierAcidSprayCooldownRounds <= 0;
    public bool HasRegeneration => _regenerationAmountPerRound > 0;

    // Tracks attackers that currently have an active feint window against this defender.
    // Used only for visual/status indication on the defender token.
    private readonly HashSet<CharacterController> _incomingFeintSources = new HashSet<CharacterController>();

    // Tracks each enemy's last known grid square when this character had line of sight.
    // Used for total-concealment targeting behavior.
    private readonly Dictionary<CharacterController, Vector2Int> _lastKnownTargetPositions = new Dictionary<CharacterController, Vector2Int>();

    // Iterative grapple attack tracking (D&D 3.5):
    // Some grapple actions can be used multiple times as attacks during a full attack sequence.
    private readonly List<int> _grappleAttackBonusesThisTurn = new List<int>();
    private int _grappleAttacksUsedThisTurn;
    private int _grappleAttackBudgetThisTurn;
    private bool _grappleAttackSequenceStarted;

    // Iterative bull rush attack tracking (bull rush as attack action).
    private readonly List<int> _bullRushAttackBonusesThisTurn = new List<int>();
    private int _bullRushAttacksUsedThisTurn;
    private int _bullRushAttackBudgetThisTurn;
    private bool _bullRushAttackSequenceStarted;

    // Iterative disarm attack tracking (disarm as attack action during a full attack sequence).
    private readonly List<int> _disarmAttackBonusesThisTurn = new List<int>();
    private int _disarmAttacksUsedThisTurn;
    private int _disarmAttackBudgetThisTurn;
    private bool _disarmAttackSequenceStarted;

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
        public int PinExpiresAfterMaintainerTurnStartCount;

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

    public bool CanUseIterativeGrappleAttackAction()
    {
        if (_grappleAttackSequenceStarted)
            return _grappleAttacksUsedThisTurn < _grappleAttackBudgetThisTurn;

        return Actions != null && (Actions.HasFullRoundAction || Actions.HasStandardAction);
    }

    public int GetRemainingGrappleAttackActions()
    {
        if (_grappleAttackSequenceStarted)
            return Mathf.Max(0, _grappleAttackBudgetThisTurn - _grappleAttacksUsedThisTurn);

        if (Actions == null)
            return 0;

        if (Actions.HasFullRoundAction)
            return GetAttackBonuses().Count;

        if (Actions.HasStandardAction)
            return 1;

        return 0;
    }

    public int GetCurrentGrappleAttackBonus()
    {
        if (_grappleAttackSequenceStarted)
        {
            if (_grappleAttacksUsedThisTurn >= _grappleAttackBudgetThisTurn || _grappleAttacksUsedThisTurn >= _grappleAttackBonusesThisTurn.Count)
                return 0;
            return _grappleAttackBonusesThisTurn[_grappleAttacksUsedThisTurn];
        }

        if (Actions != null && !Actions.HasFullRoundAction && Actions.HasStandardAction)
            return Stats != null ? Stats.BaseAttackBonus : 0;

        List<int> bonuses = GetAttackBonuses();
        return bonuses.Count > 0 ? bonuses[0] : 0;
    }

    public bool HasActiveIterativeGrappleAttackSequence()
    {
        return _grappleAttackSequenceStarted;
    }

    public bool HasRemainingIterativeGrappleAttacksInSequence()
    {
        return _grappleAttackSequenceStarted && _grappleAttacksUsedThisTurn < _grappleAttackBudgetThisTurn;
    }

    public bool TryConsumeIterativeGrappleAttackAction(out int attackBonusUsed, out int attacksRemaining, out string reason)
    {
        attackBonusUsed = 0;
        attacksRemaining = 0;
        reason = string.Empty;

        if (Actions == null)
        {
            reason = "No action economy available.";
            return false;
        }

        if (!_grappleAttackSequenceStarted)
        {
            _grappleAttackBonusesThisTurn.Clear();
            _grappleAttackBonusesThisTurn.AddRange(GetAttackBonuses());

            if (Actions.HasFullRoundAction)
            {
                Actions.UseFullRoundAction();
                _grappleAttackBudgetThisTurn = _grappleAttackBonusesThisTurn.Count;
            }
            else if (CommitStandardAction())
            {
                _grappleAttackBudgetThisTurn = Mathf.Min(1, _grappleAttackBonusesThisTurn.Count);
            }
            else
            {
                reason = "No standard or full-round action remaining.";
                return false;
            }

            _grappleAttackBudgetThisTurn = Mathf.Max(0, _grappleAttackBudgetThisTurn);
            _grappleAttacksUsedThisTurn = 0;
            _grappleAttackSequenceStarted = true;
        }

        if (_grappleAttacksUsedThisTurn >= _grappleAttackBudgetThisTurn)
        {
            reason = "No grapple attacks remaining this turn.";
            return false;
        }

        if (_grappleAttacksUsedThisTurn >= _grappleAttackBonusesThisTurn.Count)
        {
            reason = "No iterative attack bonus available for this grapple attack.";
            return false;
        }

        attackBonusUsed = _grappleAttackBonusesThisTurn[_grappleAttacksUsedThisTurn];
        _grappleAttacksUsedThisTurn++;
        attacksRemaining = Mathf.Max(0, _grappleAttackBudgetThisTurn - _grappleAttacksUsedThisTurn);
        return true;
    }

    public bool CanUseIterativeBullRushAttackAction()
    {
        if (_bullRushAttackSequenceStarted)
            return _bullRushAttacksUsedThisTurn < _bullRushAttackBudgetThisTurn;

        return Actions != null && (Actions.HasFullRoundAction || Actions.HasStandardAction);
    }

    public int GetRemainingBullRushAttackActions()
    {
        if (_bullRushAttackSequenceStarted)
            return Mathf.Max(0, _bullRushAttackBudgetThisTurn - _bullRushAttacksUsedThisTurn);

        if (Actions == null)
            return 0;

        if (Actions.HasFullRoundAction)
            return GetAttackBonuses().Count;

        if (Actions.HasStandardAction)
            return 1;

        return 0;
    }

    public int GetCurrentBullRushAttackBonus()
    {
        if (_bullRushAttackSequenceStarted)
        {
            if (_bullRushAttacksUsedThisTurn >= _bullRushAttackBudgetThisTurn || _bullRushAttacksUsedThisTurn >= _bullRushAttackBonusesThisTurn.Count)
                return 0;
            return _bullRushAttackBonusesThisTurn[_bullRushAttacksUsedThisTurn];
        }

        if (Actions != null && !Actions.HasFullRoundAction && Actions.HasStandardAction)
            return Stats != null ? Stats.BaseAttackBonus : 0;

        List<int> bonuses = GetAttackBonuses();
        return bonuses.Count > 0 ? bonuses[0] : 0;
    }

    public bool HasRemainingIterativeBullRushAttacksInSequence()
    {
        return _bullRushAttackSequenceStarted && _bullRushAttacksUsedThisTurn < _bullRushAttackBudgetThisTurn;
    }

    public bool TryConsumeIterativeBullRushAttackAction(out int attackBonusUsed, out int attacksRemaining, out string reason)
    {
        attackBonusUsed = 0;
        attacksRemaining = 0;
        reason = string.Empty;

        if (Actions == null)
        {
            reason = "No action economy available.";
            return false;
        }

        if (!_bullRushAttackSequenceStarted)
        {
            _bullRushAttackBonusesThisTurn.Clear();
            _bullRushAttackBonusesThisTurn.AddRange(GetAttackBonuses());

            if (Actions.HasFullRoundAction)
            {
                Actions.UseFullRoundAction();
                _bullRushAttackBudgetThisTurn = _bullRushAttackBonusesThisTurn.Count;
            }
            else if (CommitStandardAction())
            {
                _bullRushAttackBudgetThisTurn = Mathf.Min(1, _bullRushAttackBonusesThisTurn.Count);
            }
            else
            {
                reason = "No standard or full-round action remaining.";
                return false;
            }

            _bullRushAttackBudgetThisTurn = Mathf.Max(0, _bullRushAttackBudgetThisTurn);
            _bullRushAttacksUsedThisTurn = 0;
            _bullRushAttackSequenceStarted = true;
        }

        if (_bullRushAttacksUsedThisTurn >= _bullRushAttackBudgetThisTurn)
        {
            reason = "No bull rush attacks remaining this turn.";
            return false;
        }

        if (_bullRushAttacksUsedThisTurn >= _bullRushAttackBonusesThisTurn.Count)
        {
            reason = "No iterative attack bonus available for this bull rush attack.";
            return false;
        }

        attackBonusUsed = _bullRushAttackBonusesThisTurn[_bullRushAttacksUsedThisTurn];
        _bullRushAttacksUsedThisTurn++;
        attacksRemaining = Mathf.Max(0, _bullRushAttackBudgetThisTurn - _bullRushAttacksUsedThisTurn);
        return true;
    }

    public bool CanUseIterativeDisarmAttackAction()
    {
        if (_disarmAttackSequenceStarted)
            return _disarmAttacksUsedThisTurn < _disarmAttackBudgetThisTurn;

        return Actions != null && (Actions.HasFullRoundAction || Actions.HasStandardAction);
    }

    public int GetRemainingDisarmAttackActions()
    {
        if (_disarmAttackSequenceStarted)
            return Mathf.Max(0, _disarmAttackBudgetThisTurn - _disarmAttacksUsedThisTurn);

        if (Actions == null)
            return 0;

        if (Actions.HasFullRoundAction)
            return GetAttackBonuses().Count;

        if (Actions.HasStandardAction)
            return 1;

        return 0;
    }

    public int GetCurrentDisarmAttackBonus()
    {
        if (_disarmAttackSequenceStarted)
        {
            if (_disarmAttacksUsedThisTurn >= _disarmAttackBudgetThisTurn || _disarmAttacksUsedThisTurn >= _disarmAttackBonusesThisTurn.Count)
                return 0;
            return _disarmAttackBonusesThisTurn[_disarmAttacksUsedThisTurn];
        }

        if (Actions != null && !Actions.HasFullRoundAction && Actions.HasStandardAction)
            return Stats != null ? Stats.BaseAttackBonus : 0;

        List<int> bonuses = GetAttackBonuses();
        return bonuses.Count > 0 ? bonuses[0] : 0;
    }

    public bool HasRemainingIterativeDisarmAttacksInSequence()
    {
        return _disarmAttackSequenceStarted && _disarmAttacksUsedThisTurn < _disarmAttackBudgetThisTurn;
    }

    public bool TryConsumeIterativeDisarmAttackAction(out int attackBonusUsed, out int attacksRemaining, out string reason)
    {
        attackBonusUsed = 0;
        attacksRemaining = 0;
        reason = string.Empty;

        if (Actions == null)
        {
            reason = "No action economy available.";
            return false;
        }

        if (!_disarmAttackSequenceStarted)
        {
            _disarmAttackBonusesThisTurn.Clear();
            _disarmAttackBonusesThisTurn.AddRange(GetAttackBonuses());

            if (Actions.HasFullRoundAction)
            {
                Actions.UseFullRoundAction();
                _disarmAttackBudgetThisTurn = _disarmAttackBonusesThisTurn.Count;
            }
            else if (CommitStandardAction())
            {
                _disarmAttackBudgetThisTurn = Mathf.Min(1, _disarmAttackBonusesThisTurn.Count);
            }
            else
            {
                reason = "No standard or full-round action remaining.";
                return false;
            }

            _disarmAttackBudgetThisTurn = Mathf.Max(0, _disarmAttackBudgetThisTurn);
            _disarmAttacksUsedThisTurn = 0;
            _disarmAttackSequenceStarted = true;
        }

        if (_disarmAttacksUsedThisTurn >= _disarmAttackBudgetThisTurn)
        {
            reason = "No disarm attacks remaining this turn.";
            return false;
        }

        if (_disarmAttacksUsedThisTurn >= _disarmAttackBonusesThisTurn.Count)
        {
            reason = "No iterative attack bonus available for this disarm attack.";
            return false;
        }

        attackBonusUsed = _disarmAttackBonusesThisTurn[_disarmAttacksUsedThisTurn];
        _disarmAttacksUsedThisTurn++;
        attacksRemaining = Mathf.Max(0, _disarmAttackBudgetThisTurn - _disarmAttacksUsedThisTurn);
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

    private DamageModeAttackProfile ResolveDamageModeAttackProfile(ItemData weapon)
    {
        bool selectedNonlethal = ResolveSelectedAttackDamageMode(weapon) == AttackDamageMode.Nonlethal;
        bool isInnateNaturalAttack = ShouldUseInnateNaturalAttackProfile(weapon);
        bool isUnarmedStrike = weapon == null && !isInnateNaturalAttack;
        bool weaponIsInherentlyNonlethal = weapon != null && weapon.DealsNonlethalDamage;

        var profile = new DamageModeAttackProfile
        {
            DealNonlethalDamage = false,
            AttackPenalty = 0,
            PenaltySource = string.Empty
        };

        if (isInnateNaturalAttack)
            return profile;

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
    /// Resolve base unarmed strike damage for this character.
    /// D&D 3.5 baseline is 1d3 at Medium and scales by size; monks use their class progression as Medium baseline.
    /// </summary>
    public (int damageCount, int damageDice, int bonusDamage) GetUnarmedDamage()
    {
        int mediumBaseCount = 1;
        int mediumBaseDice = 3;

        if (Stats != null && Stats.MonkUnarmedDamageDie > 0)
            mediumBaseDice = Stats.MonkUnarmedDamageDie;

        SizeCategory currentSize = Stats != null ? Stats.CurrentSizeCategory : SizeCategory.Medium;
        if (!WeaponDamageScaler.TryScaleDamageDice(mediumBaseCount, mediumBaseDice, SizeCategory.Medium, currentSize, out int scaledCount, out int scaledDice))
        {
            scaledCount = mediumBaseCount;
            scaledDice = mediumBaseDice;
        }

        return (scaledCount, scaledDice, 0);
    }

    private void GetScaledWeaponDamageDice(ItemData weapon, out int damageCount, out int damageDice)
    {
        if (weapon == null)
        {
            damageCount = 1;
            damageDice = 3;
            return;
        }

        SizeCategory currentSize = Stats != null ? Stats.CurrentSizeCategory : SizeCategory.Medium;
        weapon.GetScaledDamageDice(currentSize, out damageCount, out damageDice);
    }

    /// <summary>
    /// Resolve base damage inputs and display label for the current attack source.
    /// </summary>
    private void ResolveBaseAttackDamageProfile(ItemData weapon, out int damageDice, out int damageCount, out int bonusDamage, out string attackLabel)
    {
        if (weapon != null)
        {
            GetScaledWeaponDamageDice(weapon, out damageCount, out damageDice);
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
                bonusDamage = Stats.GetNaturalAttackDamageBonus(naturalAttack) - Stats.STRMod;
                attackLabel = string.IsNullOrWhiteSpace(naturalAttack.Name)
                    ? (Stats.HasTripAttack ? "Bite" : "Natural attack")
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
    }

    public void UpdateSlowDuration(int durationRemainingRounds)
    {
        if (ActiveSlowEffect == null)
            return;
        ActiveSlowEffect.DurationRemainingRounds = Mathf.Max(0, durationRemainingRounds);
    }

    public void ClearSlowEffect()
    {
        ActiveSlowEffect = null;
    }

    public bool HasActiveInvisibilityEffect => ActiveInvisibilityEffect != null && ActiveInvisibilityEffect.IsInvisible;
    public bool HasActiveSeeInvisibilityEffect => ActiveSeeInvisibilityEffect != null && ActiveSeeInvisibilityEffect.CanSeeInvisible;
    public bool HasActiveGlitterdustEffect => ActiveGlitterdustEffect != null && ActiveGlitterdustEffect.OutlinedByDust && ActiveGlitterdustEffect.DurationRemainingRounds > 0;
    public bool HasActiveMelfsAcidArrowEffect => ActiveMelfsAcidArrowEffect != null && ActiveMelfsAcidArrowEffect.IsActive && ActiveMelfsAcidArrowEffect.RemainingDamageRounds > 0;
    public bool HasActiveBlurEffect
    {
        get
        {
            StatusEffectManager statusMgr = GetComponent<StatusEffectManager>();
            return statusMgr != null && statusMgr.HasEffect(SpellNames.BLUR) && statusMgr.GetRemainingRounds(SpellNames.BLUR) > 0;
        }
    }
    public bool IsOutlinedByGlitterdust => HasActiveGlitterdustEffect;

    public int GetBlurRemainingRounds()
    {
        StatusEffectManager statusMgr = GetComponent<StatusEffectManager>();
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
            StatusEffectManager statusMgr = GetComponent<StatusEffectManager>();
            return statusMgr != null && statusMgr.HasEffect(SpellNames.DISPLACEMENT) && statusMgr.GetRemainingRounds(SpellNames.DISPLACEMENT) > 0;
        }
    }

    /// <summary>
    /// Returns the number of rounds remaining for the Displacement effect, or 0
    /// if no Displacement effect is active.
    /// </summary>
    public int GetDisplacementRemainingRounds()
    {
        StatusEffectManager statusMgr = GetComponent<StatusEffectManager>();
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
            StatusEffectManager statusMgr = GetComponent<StatusEffectManager>();
            return statusMgr != null && statusMgr.HasEffect(SpellNames.BLINK) && statusMgr.GetRemainingRounds(SpellNames.BLINK) > 0;
        }
    }

    /// <summary>
    /// Returns the number of rounds remaining for the Blink effect, or 0 if not active.
    /// </summary>
    public int GetBlinkRemainingRounds()
    {
        StatusEffectManager statusMgr = GetComponent<StatusEffectManager>();
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

    public void ApplyMelfsAcidArrowEffect(int remainingDamageRounds, CharacterController caster)
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

        StatusEffectManager statusMgr = GetComponent<StatusEffectManager>();
        if (statusMgr != null)
        {
            // Remove the specific spell effect that created this invisibility
            if (!string.IsNullOrEmpty(sourceSpellId))
                statusMgr.RemoveEffectsBySpellId(sourceSpellId);
            else
                statusMgr.RemoveEffectsBySpellId(SpellNames.INVISIBILITY);
        }

        string actorName = Stats != null ? Stats.CharacterName : name;
        string sourceName = ActiveInvisibilityEffect.SourceName ?? "invisibility";
        string reasonLabel = string.IsNullOrWhiteSpace(reason) ? "hostile action" : reason;
        string targetLabel = hostileTarget != null && hostileTarget.Stats != null
            ? $" against {hostileTarget.Stats.CharacterName}"
            : string.Empty;
        GameManager.Instance?.CombatUI?.ShowCombatLog($"<color=#88CCFF>👁 {actorName}'s {sourceName} ends ({reasonLabel}{targetLabel}).</color>");

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

        StatusEffectManager statusMgr = GetComponent<StatusEffectManager>();
        if (statusMgr != null && !string.IsNullOrEmpty(sourceSpellId))
            statusMgr.RemoveEffectsBySpellId(sourceSpellId);

        if (HasCondition(CombatConditionType.Invisible))
            RemoveCondition(CombatConditionType.Invisible);

        string actorName = Stats != null ? Stats.CharacterName : name;
        string reasonLabel = string.IsNullOrWhiteSpace(reason) ? "effect ended" : reason;
        GameManager.Instance?.CombatUI?.ShowCombatLog($"<color=#88CCFF>👁 {actorName}'s invisibility ends ({reasonLabel}).</color>");

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
            Stats.LandSpeedEnhancementBonusFeet = 0;
            Stats.JumpEnhancementBonus = 0;
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
    public void MoveToCell(SquareCell targetCell, bool markAsMoved = true)
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
            GameManager.Instance?.NotifyCharacterMovement(this, oldPosition, GridPosition, markAsMoved ? "move" : "reposition");
    }

    /// <summary>
    /// Smoothly animate movement along a path of grid coordinates.
    /// The path must be ordered and should exclude the current starting square.
    /// </summary>
    public IEnumerator MoveAlongPath(List<Vector2Int> path, float secondsPerStep = DefaultMoveSecondsPerStep, bool markAsMoved = true)
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
            // ── Death/disable check BEFORE each movement step ──
            // If the creature was killed or disabled (HP <= 0) by damage taken during
            // movement (e.g., Wall of Fire pass-through, AoO, etc.), stop immediately.
            // D&D 3.5e: a dead/dying/disabled creature cannot continue moving.
            if (Stats != null && Stats.CurrentHP <= 0)
            {
                Debug.Log($"🔥 [MoveAlongPath] {Stats.CharacterName} is dead/disabled (HP={Stats.CurrentHP}) — movement interrupted at step {i}/{path.Count}");
                break;
            }

            SquareCell nextCell = GameManager.Instance.Grid.GetCell(path[i]);
            if (nextCell == null)
                continue;

            bool isDestinationStep = (i == path.Count - 1);
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
            if (Stats != null && Stats.CurrentHP <= 0)
            {
                Debug.Log($"🔥 [MoveAlongPath] {Stats.CharacterName} killed/disabled after step {i + 1}/{path.Count} at ({GridPosition.x},{GridPosition.y}) (HP={Stats.CurrentHP}) — movement interrupted");
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

        SpellcastingComponent spellComp = GetComponent<SpellcastingComponent>();
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
            SpellcastingComponent spellComp = GetComponent<SpellcastingComponent>();
            spellComp?.ApplyNegativeLevelSlotLoss();
        }

        return removed;
    }

    public List<StatusEffect> TickConditions()
    {
        return EnsureConditions().TickConditions();
    }

    public List<StatusEffect> TickConditionsDirect()
    {
        if (Stats == null) return new List<StatusEffect>();

        List<StatusEffect> expired = _conditionManager != null
            ? _conditionManager.TickConditions()
            : Stats.TickConditions();

        EnsureStatusTagManager().UpdateStatusEffectTags(GetActiveConditionsDirect());
        SpellcastingComponent spellComp = GetComponent<SpellcastingComponent>();
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
            SpellcastingComponent spellComp = GetComponent<SpellcastingComponent>();
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
        int roll = DiceService.D20("Disease exposure Fort save");
        int total = roll + Stats.FortitudeSave;

        LogAbilityScoreMessage($"🦠 {Stats.CharacterName} is exposed to {disease.Name} (Fort DC {dc}).");
        LogAbilityScoreMessage($"   Fortitude: d20({roll}) + {Stats.FortitudeSave} = {total} {(total >= dc ? "SUCCESS" : "FAIL")}");

        if (total >= dc)
            return;

        _activeDiseases.Add(new ActiveDisease(disease));
        ActiveDisease added = _activeDiseases[_activeDiseases.Count - 1];
        LogAbilityScoreMessage($"⚠ {Stats.CharacterName} contracts {disease.Name}! Incubation: {added.DaysUntilActive} day(s).");
    }

    public void ConfigureBombardierAcidSprayCooldown(int rounds)
    {
        _bombardierAcidSprayCooldownRounds = Mathf.Max(0, rounds);
    }

    public void TickBombardierAcidSprayCooldown()
    {
        if (_bombardierAcidSprayCooldownRounds > 0)
            _bombardierAcidSprayCooldownRounds--;
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

    /// <summary>Use secondary breath weapon (decrements uses remaining). Returns the definition for resolution.</summary>
    public SecondaryBreathWeaponDefinition UseSecondaryBreathWeapon()
    {
        if (_secondaryBreathWeapon == null || _secondaryBreathWeapon.UsesRemaining <= 0)
            return null;
        _secondaryBreathWeapon.UsesRemaining--;
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

    /// <summary>Tick breath weapon cooldown at start of turn.</summary>
    public void TickBreathWeaponCooldown()
    {
        if (_breathWeaponCooldownRemaining > 0)
            _breathWeaponCooldownRemaining--;
    }

    /// <summary>Use breath weapon (sets cooldown). Returns the definition for resolution.</summary>
    public BreathWeaponDefinition UseBreathWeapon()
    {
        if (_breathWeapon == null || _breathWeaponCooldownRemaining > 0)
            return null;
        _breathWeaponCooldownRemaining = _breathWeapon.RechargeRounds;
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
                GameManager.Instance.CombatUI.ShowCombatLog($"♻ {Stats.CharacterName} regenerates ({hpBefore}→{Stats.CurrentHP} HP, nonlethal {nonlethalBefore}→{Stats.NonlethalDamage}).");
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
        int roll = DiceService.D20("Poison initial Fort save");
        int total = roll + Stats.FortitudeSave;

        LogAbilityScoreMessage($"☠ {Stats.CharacterName} is exposed to {poison.Name} ({poison.Type}, Fort DC {dc}).");
        LogAbilityScoreMessage($"   Initial save: d20({roll}) + {Stats.FortitudeSave} = {total} {(total >= dc ? "SUCCESS" : "FAIL")}");

        ActivePoison activePoison = new ActivePoison(poison)
        {
            InitialSaveSucceeded = total >= dc
        };

        if (total < dc)
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
        int roll = DiceService.D20("Poison secondary Fort save");
        int total = roll + Stats.FortitudeSave;

        LogAbilityScoreMessage($"☣ {Stats.CharacterName} makes secondary save vs {poison.PoisonData.Name} (DC {dc}).");
        LogAbilityScoreMessage($"   Secondary save: d20({roll}) + {Stats.FortitudeSave} = {total} {(total >= dc ? "SUCCESS" : "FAIL")}");

        poison.SecondarySaveSucceeded = total >= dc;
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
            int roll = DiceService.D20("Disease daily Fort save");
            int total = roll + Stats.FortitudeSave;
            bool success = total >= dc;

            LogAbilityScoreMessage($"🦠 Daily save vs {active.DiseaseData.Name}: d20({roll}) + {Stats.FortitudeSave} = {total} vs DC {dc} {(success ? "SUCCESS" : "FAIL")}");

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
                GameManager.Instance.CombatUI?.ShowCombatLog($"💥 {Stats.CharacterName} wakes from taking damage.");
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
                GameManager.Instance.CombatUI?.ShowCombatLog($"💥 {Stats.CharacterName} wakes from taking damage.");
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
            if (newHP > oldHP)
                return HPState.Stable; // Healing while still negative stabilizes.

            if (_currentHPState == HPState.Stable && newHP == oldHP)
                return HPState.Stable;

            // Diehard: remain conscious (Disabled) at negative HP, auto-stabilize
            if (Stats != null && FeatManager.HasDiehard(Stats))
            {
                Debug.Log($"[Diehard] {Stats.CharacterName} remains conscious at {newHP} HP (Diehard feat)");
                return HPState.Disabled;
            }

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

        Actions.SingleActionOnly = (newState == HPState.Disabled || newState == HPState.Staggered);

        if (newState == HPState.Unconscious || newState == HPState.Dying || newState == HPState.Stable)
        {
            ReleaseGrappleState("incapacitated");

            var concMgr = GetComponent<ConcentrationManager>();
            if (concMgr != null && concMgr.IsConcentrating)
                concMgr.OnCharacterIncapacitated();

            var spellComp = GetComponent<SpellcastingComponent>();
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

    /// <summary>
    /// D&D 3.5 end-of-turn dying progression (check first, then lose HP on failure).
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
                GameManager.Instance.CombatUI.ShowCombatLog($"💪 {Stats.CharacterName} stabilizes automatically (Diehard feat) and remains conscious at {Stats.CurrentHP} HP.");
            return;
        }

        int roll = DiceService.D20("Stabilization check");
        int conMod = Stats.CONMod;
        int total = roll + conMod;
        const int dc = 10;

        if (GameManager.Instance != null && GameManager.Instance.CombatUI != null)
        {
            GameManager.Instance.CombatUI.ShowCombatLog(
                $"🎲 {Stats.CharacterName} stabilization check: d20({roll}) + CON({CharacterStats.FormatMod(conMod)}) = {total} vs DC {dc}");
        }

        if (total >= dc)
        {
            SetHPState(HPState.Stable, emitLog: true);
            return;
        }

        int before = Stats.CurrentHP;
        Stats.TakeDamage(1);
        int after = Stats.CurrentHP;

        if (GameManager.Instance != null && GameManager.Instance.CombatUI != null)
        {
            GameManager.Instance.CombatUI.ShowCombatLog($"💉 {Stats.CharacterName} fails to stabilize and loses 1 HP ({before} → {after}).");
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
                GameManager.Instance.CombatUI.ShowCombatLog($"🩹 {Stats.CharacterName} is stabilized by {sourceName}.");
        }
    }

    public bool CommitStandardAction()
    {
        if (!Actions.HasStandardAction)
            return false;

        Actions.UseStandardAction();

        if (_currentHPState == HPState.Disabled && Stats != null && Stats.CurrentHP == 0)
        {
            if (GameManager.Instance != null && GameManager.Instance.CombatUI != null)
                GameManager.Instance.CombatUI.ShowCombatLog($"⚠ {Stats.CharacterName} takes a standard action while disabled and drops to -1 HP!");

            Stats.CurrentHP = -1;
        }

        return true;
    }

    private void TryApplyNaturalAttackOnHitEffects(CharacterController target, CombatResult attackResult, NaturalAttackDefinition naturalAttack)
    {
        if (target == null || attackResult == null || !attackResult.Hit || naturalAttack == null)
            return;

        // --- Poison on hit ---
        string poisonId = naturalAttack.PoisonOnHitId;
        if (!string.IsNullOrWhiteSpace(poisonId))
            target.ApplyPoison(poisonId);

        // --- Disease on hit (Dire Rat filth fever, etc.) ---
        if (naturalAttack.HasDiseaseOnHit)
        {
            target.ExposeToDisease(naturalAttack.DiseaseOnHitType);
            Debug.Log($"[OnHit] {Stats?.CharacterName ?? "?"} exposes {target.Stats?.CharacterName ?? "?"} to {naturalAttack.DiseaseOnHitType}");
        }

        // --- Paralysis on hit (Gelatinous Cube tentacle, Ghoul, etc.) ---
        if (naturalAttack.ParalysisOnHitDC > 0 && target.Stats != null)
        {
            int roll = UnityEngine.Random.Range(1, 21);
            int total = roll + target.Stats.FortitudeSave;
            if (total < naturalAttack.ParalysisOnHitDC)
            {
                int duration = Mathf.Max(1, naturalAttack.ParalysisOnHitDurationRounds);
                target.Conditions.ApplyCondition(CombatConditionType.Paralyzed, duration, Stats?.CharacterName ?? "Paralysis");
                Debug.Log($"[OnHit] {target.Stats.CharacterName} paralyzed for {duration} rounds (Fort d20({roll})+{target.Stats.FortitudeSave}={total} < DC {naturalAttack.ParalysisOnHitDC})");
            }
            else
            {
                Debug.Log($"[OnHit] {target.Stats.CharacterName} resists paralysis (Fort d20({roll})+{target.Stats.FortitudeSave}={total} >= DC {naturalAttack.ParalysisOnHitDC})");
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
            int roll = UnityEngine.Random.Range(1, 21);
            int total = roll + target.Stats.FortitudeSave;
            if (total < naturalAttack.PetrificationOnHitDC)
            {
                target.Conditions.ApplyCondition(CombatConditionType.Petrified, 999, Stats?.CharacterName ?? "Petrification");
                Debug.Log($"[OnHit] {target.Stats.CharacterName} PETRIFIED (Fort d20({roll})+{target.Stats.FortitudeSave}={total} < DC {naturalAttack.PetrificationOnHitDC})");
            }
            else
            {
                Debug.Log($"[OnHit] {target.Stats.CharacterName} resists petrification (Fort d20({roll})+{target.Stats.FortitudeSave}={total} >= DC {naturalAttack.PetrificationOnHitDC})");
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

    /// <summary>
    /// Perform a single attack with flanking context and optional range info.
    /// Includes full D&D 3.5 critical hit mechanics, racial attack bonuses, and feat effects.
    /// Uses weapon's DamageModifierType for correct STR bonus to damage.
    /// Integrates: Power Attack, Point Blank Shot, Weapon Focus, Weapon Specialization,
    /// Weapon Finesse, Combat Expertise, Improved Critical, Dodge.
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
        bool isOffHandAttack = false)
    {
        if (target == null || target.Stats == null || target.Stats.IsDead)
        {
            Debug.LogWarning($"[Combat] {Stats?.CharacterName ?? name} attempted to attack an invalid target.");
            return new CombatResult
            {
                Attacker = this,
                Defender = target,
                WeaponName = (attackWeaponOverride ?? GetEquippedMainWeapon()) != null
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

        // Calculate racial attack bonus against target
        int racialAtkBonus = Stats.GetRacialAttackBonus(target.Stats);
        int rangePenalty = (rangeInfo != null && !rangeInfo.IsMelee && rangeInfo.IsInRange) ? rangeInfo.Penalty : 0;

        // Get equipped weapon for damage modifier and feat calculations
        ItemData equippedWeapon = attackWeaponOverride ?? GetEquippedMainWeapon();
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

        bool targetInRange = IsTargetInWeaponRange(target, equippedWeapon, useThrownRange);

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

        bool isRanged = equippedWeapon != null
                        && (equippedWeapon.WeaponCat == WeaponCategory.Ranged || useThrownRange)
                        && rangeInfo != null && !rangeInfo.IsMelee;
        bool isMelee = !isRanged;

        // Last-known-position empty-square checks are resolved in PerformSingleAttackWithCrit()
        // so all attack entry points (single, full-attack, flurry, grapple strike, etc.) follow
        // the same rules and never roll against an empty square.

        // === FEAT MODIFIERS (via AttackCalculator) ===
        int distFeet = (rangeInfo != null) ? rangeInfo.DistanceFeet : 0;
        bool hasValidRange = rangeInfo != null && !rangeInfo.IsMelee;
        int baseCritMin = Stats.CritThreatMin > 0 ? Stats.CritThreatMin : 20;
        var featMods = AttackCalculator.CalculateAllFeatModifiers(
            Stats, equippedWeapon, isRanged, isMelee,
            IsWeaponTwoHanded(equippedWeapon), PowerAttackValue,
            distFeet, hasValidRange,
            WeaponDisablesStrengthDamageBonuses(equippedWeapon),
            baseCritMin);

        int powerAtkPenalty = featMods.PowerAttackPenalty;
        int powerAtkDmgBonus = featMods.PowerAttackDamageBonus;
        bool pointBlankActive = featMods.PointBlankShotActive;
        int pbsAtkBonus = featMods.PointBlankShotAttackBonus;
        int pbsDmgBonus = featMods.PointBlankShotDamageBonus;
        int weaponFocusBonus = featMods.WeaponFocusBonus;
        int weaponSpecBonus = featMods.WeaponSpecDamageBonus;
        int abilityMod = featMods.AbilityMod;
        string abilityName = featMods.AbilityName;
        int combatExpertisePenalty = featMods.CombatExpertisePenalty;

        if (abilityName == "DEX(Finesse)")
            Debug.Log($"[Feats] {Stats.CharacterName}: Weapon Finesse active, using DEX {Stats.DEXMod} for attack");
        if (combatExpertisePenalty != 0)
            Debug.Log($"[Feats] {Stats.CharacterName}: Combat Expertise {combatExpertisePenalty} attack, +{-combatExpertisePenalty} AC");

        // Prone: melee attacks take -4.
        int proneAttackPenalty = GetProneAttackModifier(isMelee);

        // Fighting Defensively: -4 attack rolls while stance is active.
        int fightingDefensivelyPenalty = IsFightingDefensively ? -4 : 0;

        // Shooting into melee: -4 for ranged attacks against targets engaged with attacker allies.
        bool preciseShotNegated = false;
        int shootingIntoMeleePenalty = GetShootingIntoMeleePenalty(this, target, isRanged, out preciseShotNegated);

        int weaponNonProfPenalty = Stats.GetWeaponNonProficiencyPenalty(equippedWeapon);
        int armorNonProfPenalty = Stats.GetArmorNonProficiencyAttackPenalty();
        int moraleAttackBonus = Stats.MoraleAttackBonus;
        int conditionAttackPenalty = Stats.ConditionAttackPenalty;
        int solidFogAtkPenalty = isMelee ? Stats.SolidFogMeleeAttackPenalty : 0;
        int solidFogDmgPenalty = isMelee ? Stats.SolidFogMeleeDamagePenalty : 0;
        int aidAnotherAttackBonus = ConsumeAidAnotherAttackBonus(target);
        int aidAnotherTargetAcBonus = ConsumeAidAnotherAcBonus(target);
        DamageModeAttackProfile damageModeProfile = ResolveDamageModeAttackProfile(equippedWeapon);

        int baseAttackBonusUsed = baseAttackBonusOverride ?? Stats.BaseAttackBonus;

        int totalAtkMod = baseAttackBonusUsed + abilityMod + Stats.SizeModifier
                          + (isFlanking ? flankingBonus : 0) + racialAtkBonus + rangePenalty
                          + powerAtkPenalty + pbsAtkBonus + weaponFocusBonus + combatExpertisePenalty
                          + proneAttackPenalty + fightingDefensivelyPenalty + shootingIntoMeleePenalty
                          + weaponNonProfPenalty + armorNonProfPenalty + moraleAttackBonus + conditionAttackPenalty
                          + aidAnotherAttackBonus + damageModeProfile.AttackPenalty + additionalAttackModifier
                          + solidFogAtkPenalty;

        int critThreatMin = featMods.CritThreatMin;
        int critMult = Stats.CritMultiplier > 0 ? Stats.CritMultiplier : 2;

        int totalFeatDmgBonus = featMods.TotalFeatDamageBonus + solidFogDmgPenalty;
        ResolveBaseAttackDamageProfile(equippedWeapon, out int damageDice, out int damageCount, out int bonusDamage, out string attackLabel);

        // D&D 3.5e Magic Stone: when firing a sling with active Magic Stone charges,
        // override damage to 1d6+1, add +1 enhancement to attack, and mark as magical (PHB p.251)
        bool magicStoneUsed = false;
        if (isRanged && equippedWeapon != null && equippedWeapon.Id == ItemIDs.SLING
            && Stats.MagicStoneActive && Stats.MagicStoneCharges > 0)
        {
            damageDice = 6;
            damageCount = 1;
            bonusDamage = 1;
            totalAtkMod += 1; // +1 enhancement bonus to attack
            magicStoneUsed = true;
        }

        NaturalAttackDefinition naturalAttackForOnHit = null;
        if (ShouldUseInnateNaturalAttackProfile(equippedWeapon))
            naturalAttackForOnHit = Stats.GetPrimaryNaturalAttack();

        // Record HP before attack
        int hpBefore = target.Stats.CurrentHP;

        var result = PerformSingleAttackWithCrit(target, totalAtkMod, isFlanking, flankingBonus, flankingPartnerName,
            damageDice, damageCount, bonusDamage, critThreatMin, critMult,
            equippedWeapon, false, totalFeatDmgBonus, aidAnotherTargetAcBonus,
            damageModeProfile.DealNonlethalDamage, damageModeProfile.AttackPenalty, damageModeProfile.PenaltySource);

        result.RacialAttackBonus = racialAtkBonus;
        result.SizeAttackBonus = Stats.SizeModifier;
        result.PowerAttackValue = (powerAtkPenalty != 0) ? PowerAttackValue : 0;

        int baseAttackWithoutAid = totalAtkMod - aidAnotherAttackBonus;
        string attackerNameForLog = Stats != null ? Stats.CharacterName : "Unknown";
        string targetNameForLog = target != null && target.Stats != null ? target.Stats.CharacterName : "Unknown";
        Debug.Log($"[Attack] {attackerNameForLog} attacks {targetNameForLog}: d20={result.DieRoll} + base={baseAttackWithoutAid} + aid={aidAnotherAttackBonus} => total={result.TotalRoll} vs AC {result.TargetAC}");
        result.PowerAttackDamageBonus = powerAtkDmgBonus;
        result.PointBlankShotActive = pointBlankActive;
        result.FeatDamageBonus = totalFeatDmgBonus;
        result.WeaponFocusBonus = weaponFocusBonus;
        result.WeaponSpecBonus = weaponSpecBonus;
        result.CombatExpertisePenalty = combatExpertisePenalty;
        result.FightingDefensivelyAttackPenalty = fightingDefensivelyPenalty;
        result.ShootingIntoMeleePenalty = shootingIntoMeleePenalty;
        result.PreciseShotNegated = preciseShotNegated;
        result.AidAnotherAttackBonus = aidAnotherAttackBonus;
        result.AidAnotherTargetAcBonus = aidAnotherTargetAcBonus;
        result.FightingDefensivelyACBonus = target != null && target.IsFightingDefensively ? 2 : 0;

        // Breakdown fields for detailed logging
        result.BreakdownBAB = baseAttackBonusUsed;
        result.BreakdownAbilityMod = abilityMod;
        result.BreakdownAbilityName = abilityName;
        result.WeaponNonProficiencyPenalty = weaponNonProfPenalty;
        result.ArmorNonProficiencyPenalty = armorNonProfPenalty;
        result.IsDualWieldAttack = isOffHandAttack || additionalAttackModifier != 0;
        result.IsOffHandAttack = isOffHandAttack;
        result.BreakdownDualWieldPenalty = additionalAttackModifier;

        // Store range info on result
        if (rangeInfo != null && !rangeInfo.IsMelee && rangeInfo.IsInRange)
        {
            result.IsRangedAttack = true;
            result.RangeDistanceFeet = rangeInfo.DistanceFeet;
            result.RangeDistanceSquares = rangeInfo.SquareDistance;
            result.RangeIncrementNumber = rangeInfo.IncrementNumber;
            result.RangePenalty = rangeInfo.Penalty;
        }
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
                StatusEffectManager selfStatusMgr = GetComponent<StatusEffectManager>();
                if (selfStatusMgr != null)
                    selfStatusMgr.RemoveEffectsBySpellId(SpellNames.DOMAIN_MAGIC_STONE);
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

        if (GameManager.Instance != null)
        {
            GameManager.Instance.BreakCharmOnHostileAction(this, target);
            GameManager.Instance.BreakCommandUndeadOnHostileAction(this, target);
            GameManager.Instance.BreakFascinationOnHostileAction(this, target, "attack");
            GameManager.Instance.BreakFascinationFromLoudNoise(this, target != null ? target.GridPosition : GridPosition, radiusSquares: 4);
        }

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
            return Mathf.Max(0, Stats.GetTotalNaturalAttackCount());

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
    /// </summary>
    public FullAttackResult FullAttack(CharacterController target, bool isFlanking, int flankingBonus, string flankingPartnerName, RangeInfo rangeInfo = null, int startAttackIndex = 0, int maxAttacks = int.MaxValue)
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

        int[] attackBonuses = Stats.GetIterativeAttackBonuses();
        int critThreatMin = Stats.CritThreatMin > 0 ? Stats.CritThreatMin : 20;
        int critMult = Stats.CritMultiplier > 0 ? Stats.CritMultiplier : 2;
        int racialAtkBonus = Stats.GetRacialAttackBonus(target.Stats);
        int rangePenalty = (rangeInfo != null && !rangeInfo.IsMelee && rangeInfo.IsInRange) ? rangeInfo.Penalty : 0;

        // Get equipped weapon for damage modifier and feat calculations
        ItemData equippedWeapon = GetEquippedMainWeapon();
        if (!CanAttackWithWeapon(equippedWeapon, out string cannotAttackReason))
        {
            Debug.LogWarning($"[FullAttack] {Stats.CharacterName}: {cannotAttackReason}");
            result.DefenderHPAfter = target.Stats.CurrentHP;
            result.TargetKilled = target.Stats.IsDead;
            return result;
        }

        bool useThrownRange = equippedWeapon != null
            && equippedWeapon.IsThrown
            && equippedWeapon.RangeIncrement > 0
            && rangeInfo != null
            && !rangeInfo.IsMelee;
        bool isRanged = (equippedWeapon != null && (equippedWeapon.WeaponCat == WeaponCategory.Ranged || useThrownRange))
                        && rangeInfo != null && !rangeInfo.IsMelee;
        bool isMelee = !isRanged;
        int weaponNonProfPenalty = Stats.GetWeaponNonProficiencyPenalty(equippedWeapon);
        int armorNonProfPenalty = Stats.GetArmorNonProficiencyAttackPenalty();
        int conditionAttackPenalty = Stats.ConditionAttackPenalty;
        int solidFogAtkPenalty = isMelee ? Stats.SolidFogMeleeAttackPenalty : 0;
        int solidFogDmgPenalty = isMelee ? Stats.SolidFogMeleeDamagePenalty : 0;

        // === FEAT MODIFIERS (via AttackCalculator) - Full Attack ===
        int fullAtkDistFeet = (rangeInfo != null) ? rangeInfo.DistanceFeet : 0;
        var fullAtkFeatMods = AttackCalculator.CalculateAllFeatModifiers(
            Stats, equippedWeapon, isRanged, isMelee,
            IsWeaponTwoHanded(equippedWeapon), PowerAttackValue,
            fullAtkDistFeet, rangeInfo != null && !rangeInfo.IsMelee,
            WeaponDisablesStrengthDamageBonuses(equippedWeapon),
            critThreatMin, RapidShotEnabled);

        int powerAtkPenalty = fullAtkFeatMods.PowerAttackPenalty;
        int powerAtkDmgBonus = fullAtkFeatMods.PowerAttackDamageBonus;
        bool pointBlankActive = fullAtkFeatMods.PointBlankShotActive;
        int pbsAtkBonus = fullAtkFeatMods.PointBlankShotAttackBonus;
        int pbsDmgBonus = fullAtkFeatMods.PointBlankShotDamageBonus;
        int weaponFocusBonus = fullAtkFeatMods.WeaponFocusBonus;
        int weaponSpecBonus = fullAtkFeatMods.WeaponSpecDamageBonus;
        int baseAbilityMod = fullAtkFeatMods.AbilityMod;
        string baseAbilityName = fullAtkFeatMods.AbilityName;
        int combatExpertisePenalty = fullAtkFeatMods.CombatExpertisePenalty;
        critThreatMin = fullAtkFeatMods.CritThreatMin;
        bool rapidShotActive = fullAtkFeatMods.RapidShotActive;
        int rapidShotPenalty = fullAtkFeatMods.RapidShotPenalty;

        // Prone: melee attacks take -4.
        int proneAttackPenalty = GetProneAttackModifier(isMelee);

        // Fighting Defensively: -4 attack while active.
        int fightingDefensivelyPenalty = IsFightingDefensively ? -4 : 0;

        // Shooting into melee: -4 unless Precise Shot negates it.
        bool preciseShotNegated = false;
        int shootingIntoMeleePenalty = GetShootingIntoMeleePenalty(this, target, isRanged, out preciseShotNegated);

        int totalFeatDmgBonus = fullAtkFeatMods.TotalFeatDamageBonus + solidFogDmgPenalty;
        DamageModeAttackProfile damageModeProfile = ResolveDamageModeAttackProfile(equippedWeapon);
        ResolveBaseAttackDamageProfile(equippedWeapon, out int damageDice, out int damageCount, out int bonusDamage, out string attackLabel);

        bool useNaturalAttackSequence = isMelee && ShouldUseInnateNaturalAttackProfile(equippedWeapon);
        if (useNaturalAttackSequence)
        {
            List<NaturalAttackDefinition> naturalAttacks = Stats.GetValidNaturalAttacks();
            if (startAttackIndex < 0)
                startAttackIndex = 0;

            int naturalAttackGlobalIndex = 0;
            int naturalAttacksExecuted = 0;
            for (int naturalIndex = 0; naturalIndex < naturalAttacks.Count; naturalIndex++)
            {
                NaturalAttackDefinition naturalAttack = naturalAttacks[naturalIndex];
                int attackCount = Mathf.Max(1, naturalAttack.Count);
                for (int repeat = 0; repeat < attackCount; repeat++)
                {
                    if (naturalAttacksExecuted >= maxAttacks)
                        break;

                    if (naturalAttackGlobalIndex++ < startAttackIndex)
                        continue;

                    if (target.Stats.IsDead)
                        break;

                    int baseBonus = Stats.GetNaturalAttackBonus(naturalAttack);
                    int aidAnotherAttackBonus = ConsumeAidAnotherAttackBonus(target);
                    int aidAnotherTargetAcBonus = ConsumeAidAnotherAcBonus(target);
                    int atkMod = baseBonus + (isFlanking ? flankingBonus : 0) + racialAtkBonus
                                 + powerAtkPenalty + weaponFocusBonus + combatExpertisePenalty
                                 + proneAttackPenalty + fightingDefensivelyPenalty
                                 + shootingIntoMeleePenalty + armorNonProfPenalty + conditionAttackPenalty
                                 + aidAnotherAttackBonus + damageModeProfile.AttackPenalty
                                 + solidFogAtkPenalty;

                    int hpBeforeAtk = target.Stats.CurrentHP;
                    bool useHalfStrength = !naturalAttack.IsPrimary;
                    int baseStrengthFromDamageResolver = useHalfStrength ? Mathf.FloorToInt(Stats.STRMod * 0.5f) : Stats.STRMod;
                    int naturalDamageBonus = Stats.GetNaturalAttackDamageBonus(naturalAttack) - baseStrengthFromDamageResolver;

                    Stats.GetScaledNaturalAttackDamage(naturalAttack, out int naturalDamageCount, out int naturalDamageDice);

                    CombatResult atk = PerformSingleAttackWithCrit(target, atkMod, isFlanking, flankingBonus, flankingPartnerName,
                        naturalDamageDice, naturalDamageCount, naturalDamageBonus,
                        critThreatMin, critMult,
                        equippedWeapon, useHalfStrength, totalFeatDmgBonus, aidAnotherTargetAcBonus,
                        damageModeProfile.DealNonlethalDamage, damageModeProfile.AttackPenalty, damageModeProfile.PenaltySource);

                    atk.RacialAttackBonus = racialAtkBonus;
                    atk.SizeAttackBonus = Stats.SizeModifier;
                    atk.PowerAttackValue = (powerAtkPenalty != 0) ? PowerAttackValue : 0;
                    atk.PowerAttackDamageBonus = powerAtkDmgBonus;
                    atk.RapidShotActive = false;
                    atk.PointBlankShotActive = false;
                    atk.FeatDamageBonus = totalFeatDmgBonus;
                    atk.WeaponFocusBonus = weaponFocusBonus;
                    atk.WeaponSpecBonus = weaponSpecBonus;
                    atk.CombatExpertisePenalty = combatExpertisePenalty;
                    atk.FightingDefensivelyAttackPenalty = fightingDefensivelyPenalty;
                    atk.ShootingIntoMeleePenalty = shootingIntoMeleePenalty;
                    atk.PreciseShotNegated = preciseShotNegated;
                    atk.AidAnotherAttackBonus = aidAnotherAttackBonus;
                    atk.AidAnotherTargetAcBonus = aidAnotherTargetAcBonus;
                    atk.FightingDefensivelyACBonus = target != null && target.IsFightingDefensively ? 2 : 0;
                    atk.BreakdownBAB = baseBonus;
                    atk.BreakdownAbilityMod = Stats.STRMod;
                    atk.BreakdownAbilityName = naturalAttack.IsPrimary ? "STR" : "STR (Secondary)";
                    atk.WeaponNonProficiencyPenalty = 0;
                    atk.ArmorNonProficiencyPenalty = armorNonProfPenalty;
                    atk.WeaponName = string.IsNullOrWhiteSpace(naturalAttack.Name) ? "Natural attack" : naturalAttack.Name;
                    atk.BaseDamageDiceStr = $"{naturalDamageCount}d{naturalDamageDice}";
                    atk.DefenderHPBefore = hpBeforeAtk;
                    atk.DefenderHPAfter = target.Stats.CurrentHP;

                    TryApplyNaturalAttackOnHitEffects(target, atk, naturalAttack);

                    result.Attacks.Add(atk);
                    string naturalLabel = string.IsNullOrWhiteSpace(naturalAttack.Name) ? "Natural" : naturalAttack.Name;
                    string roleLabel = naturalAttack.IsPrimary ? "Primary" : "Secondary";
                    result.AttackLabels.Add($"{naturalLabel} {repeat + 1} ({roleLabel} {CharacterStats.FormatMod(baseBonus)})");
                    naturalAttacksExecuted++;
                }

                if (naturalAttacksExecuted >= maxAttacks || target.Stats.IsDead)
                    break;
            }

            result.DefenderHPAfter = target.Stats.CurrentHP;
            result.TargetKilled = target.Stats.IsDead;
            HasAttackedThisTurn = result.Attacks.Count > 0;
            return result;
        }

        // === Debug Logging ===
        Debug.Log($"[FullAttack] {Stats.CharacterName}: FullAttack() called");
        Debug.Log($"[FullAttack] Weapon: {(equippedWeapon != null ? equippedWeapon.Name : "(unarmed)")}, Ranged: {isRanged}");
        Debug.Log($"[FullAttack] Feats: WF={weaponFocusBonus}, WS={weaponSpecBonus}, PA={powerAtkDmgBonus}, CE={combatExpertisePenalty}");
        if (rapidShotActive) Debug.Log($"[FullAttack] Rapid Shot active: -2 penalty, +1 extra attack");

        // Build the list of attack bonuses, inserting Rapid Shot extra attack
        var allAttackBonuses = new List<int>(attackBonuses);
        int baseAttackCount = allAttackBonuses.Count;

        if (rapidShotActive)
        {
            allAttackBonuses.Insert(0, attackBonuses[0]);
            Debug.Log($"[FullAttack] Rapid Shot: attack count {baseAttackCount} → {allAttackBonuses.Count}");
        }
        else if (RapidShotEnabled && Stats.HasFeat("Rapid Shot") && !isRanged)
        {
            Debug.LogWarning($"[FullAttack] {Stats.CharacterName}: Rapid Shot ON but weapon is not ranged");
        }

        // Haste grants one extra attack at highest BAB (PHB p.239)
        if (HasActiveHasteEffect && ActiveHasteEffect.GrantsExtraAttack)
        {
            allAttackBonuses.Add(attackBonuses[0]);
            Debug.Log($"[FullAttack] Haste: extra attack at highest BAB, attack count → {allAttackBonuses.Count}");
        }

        if (startAttackIndex < 0)
            startAttackIndex = 0;

        int attacksExecuted = 0;
        for (int i = startAttackIndex; i < allAttackBonuses.Count; i++)
        {
            if (attacksExecuted >= maxAttacks)
                break;

            if (target.Stats.IsDead)
            {
                Debug.Log($"[FullAttack] Target is dead, stopping at attack {i + 1}");
                break;
            }

            int baseBonus = allAttackBonuses[i];
            int aidAnotherAttackBonus = ConsumeAidAnotherAttackBonus(target);
            int aidAnotherTargetAcBonus = ConsumeAidAnotherAcBonus(target);
            int atkMod = baseBonus + (isFlanking ? flankingBonus : 0) + racialAtkBonus + rangePenalty
                         + powerAtkPenalty + pbsAtkBonus + weaponFocusBonus + combatExpertisePenalty
                         + rapidShotPenalty + proneAttackPenalty + fightingDefensivelyPenalty + shootingIntoMeleePenalty
                         + weaponNonProfPenalty + armorNonProfPenalty + conditionAttackPenalty
                         + aidAnotherAttackBonus + damageModeProfile.AttackPenalty
                         + solidFogAtkPenalty;

            // The base bonus from GetIterativeAttackBonuses already includes STRMod + SizeModifier.
            if (!isRanged && FeatManager.ShouldUseWeaponFinesse(Stats, equippedWeapon))
            {
                // Remove STR, add DEX (base bonus already has STRMod from GetIterativeAttackBonuses).
                atkMod += (Stats.DEXMod - Stats.STRMod);
            }

            string label;
            if (rapidShotActive && i == 0)
                label = $"Attack 1 (Rapid Shot, {CharacterStats.FormatMod(baseBonus)})";
            else
                label = $"Attack {i + 1} ({CharacterStats.FormatMod(baseBonus)})";

            int hpBeforeAtk = target.Stats.CurrentHP;

            // D&D 3.5e Magic Stone: per-attack override for sling full attack (PHB p.251)
            int atkDamageDice = damageDice;
            int atkDamageCount = damageCount;
            int atkBonusDamage = bonusDamage;
            int magicStoneAtkBonus = 0;
            bool fullAtkMagicStoneUsed = false;
            if (isRanged && equippedWeapon != null && equippedWeapon.Id == ItemIDs.SLING
                && Stats.MagicStoneActive && Stats.MagicStoneCharges > 0)
            {
                atkDamageDice = 6;
                atkDamageCount = 1;
                atkBonusDamage = 1;
                magicStoneAtkBonus = 1;
                fullAtkMagicStoneUsed = true;
            }

            CombatResult atk = PerformSingleAttackWithCrit(target, atkMod + magicStoneAtkBonus, isFlanking, flankingBonus, flankingPartnerName,
                atkDamageDice, atkDamageCount, atkBonusDamage, critThreatMin, critMult,
                equippedWeapon, false, totalFeatDmgBonus, aidAnotherTargetAcBonus,
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
                    StatusEffectManager selfStatusMgr = GetComponent<StatusEffectManager>();
                    if (selfStatusMgr != null)
                        selfStatusMgr.RemoveEffectsBySpellId(SpellNames.DOMAIN_MAGIC_STONE);
                }
            }

            atk.RacialAttackBonus = racialAtkBonus;
            atk.SizeAttackBonus = Stats.SizeModifier;
            atk.PowerAttackValue = (powerAtkPenalty != 0) ? PowerAttackValue : 0;
            atk.PowerAttackDamageBonus = powerAtkDmgBonus;
            atk.RapidShotActive = rapidShotActive;
            atk.PointBlankShotActive = pointBlankActive;
            atk.FeatDamageBonus = totalFeatDmgBonus;
            atk.WeaponFocusBonus = weaponFocusBonus;
            atk.WeaponSpecBonus = weaponSpecBonus;
            atk.CombatExpertisePenalty = combatExpertisePenalty;
            atk.FightingDefensivelyAttackPenalty = fightingDefensivelyPenalty;
            atk.ShootingIntoMeleePenalty = shootingIntoMeleePenalty;
            atk.PreciseShotNegated = preciseShotNegated;
            atk.AidAnotherAttackBonus = aidAnotherAttackBonus;
            atk.AidAnotherTargetAcBonus = aidAnotherTargetAcBonus;
            atk.FightingDefensivelyACBonus = target != null && target.IsFightingDefensively ? 2 : 0;

            // Breakdown fields
            atk.BreakdownBAB = baseBonus;
            atk.BreakdownAbilityMod = baseAbilityMod;
            atk.BreakdownAbilityName = baseAbilityName;
            atk.WeaponNonProficiencyPenalty = weaponNonProfPenalty;
            atk.ArmorNonProficiencyPenalty = armorNonProfPenalty;

            // Store range info on each attack result
            if (rangeInfo != null && !rangeInfo.IsMelee && rangeInfo.IsInRange)
            {
                atk.IsRangedAttack = true;
                atk.RangeDistanceFeet = rangeInfo.DistanceFeet;
                atk.RangeDistanceSquares = rangeInfo.SquareDistance;
                atk.RangeIncrementNumber = rangeInfo.IncrementNumber;
                atk.RangePenalty = rangeInfo.Penalty;
            }
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
        int conditionAttackPenalty = Stats.ConditionAttackPenalty;
        int solidFogAtkPenalty = Stats.SolidFogMeleeAttackPenalty; // rake is always melee
        int solidFogDmgPenalty = Stats.SolidFogMeleeDamagePenalty;

        for (int i = 0; i < attackCount; i++)
        {
            if (target.Stats.IsDead)
                break;

            // D&D 3.5e: rake attacks use PRIMARY attack bonuses (no -5 secondary penalty)
            int baseBonus = Stats.BaseAttackBonus + Stats.STRMod + Stats.SizeModifier;
            int atkMod = baseBonus + (isFlanking ? flankingBonus : 0) + armorNonProfPenalty + conditionAttackPenalty + solidFogAtkPenalty;
            int hpBeforeAtk = target.Stats.CurrentHP;

            Stats.GetScaledNaturalAttackDamage(rakeAttack, out int damageCount, out int damageDice);

            // Rake damage still uses 0.5× STR by rule, so keep off-hand strength handling for damage resolution.
            const bool useHalfStrengthForRakeDamage = true;
            int baseStrengthFromDamageResolver = Mathf.FloorToInt(Stats.STRMod * 0.5f);
            int naturalDamageBonus = Stats.GetNaturalAttackDamageBonus(rakeAttack) - baseStrengthFromDamageResolver + solidFogDmgPenalty;

            CombatResult atk = PerformSingleAttackWithCrit(
                target,
                atkMod,
                isFlanking,
                flankingBonus,
                flankingPartnerName,
                damageDice,
                damageCount,
                naturalDamageBonus,
                critThreatMin,
                critMult,
                null,
                isOffHand: useHalfStrengthForRakeDamage,
                featDamageBonus: 0,
                situationalTargetAcBonus: 0,
                dealNonlethalDamage: false,
                damageModeAttackPenalty: 0,
                damageModePenaltySource: string.Empty);

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
        int armorNonProfPenalty = Stats.GetArmorNonProficiencyAttackPenalty();
        int mainWeaponNonProfPenalty = Stats.GetWeaponNonProficiencyPenalty(mainWeapon);
        int offWeaponNonProfPenalty = Stats.GetWeaponNonProficiencyPenalty(offWeapon);

        int racialAtkBonus = Stats.GetRacialAttackBonus(target.Stats);
        int rangePenalty = (rangeInfo != null && !rangeInfo.IsMelee && rangeInfo.IsInRange) ? rangeInfo.Penalty : 0;

        bool isRanged = rangeInfo != null && !rangeInfo.IsMelee;
        bool isMelee = !isRanged;

        // Power Attack (via AttackCalculator) - always one-handed while dual-wielding
        AttackCalculator.CalculatePowerAttack(Stats, PowerAttackValue, isMelee,
            false /* never two-handed in dual-wield */, WeaponDisablesStrengthDamageBonuses(mainWeapon),
            out int powerAtkPenalty, out int powerAtkDmgBonus);

        // Point Blank Shot (via AttackCalculator)
        int dualDistFeet = (rangeInfo != null) ? rangeInfo.DistanceFeet : 0;
        AttackCalculator.CalculatePointBlankShot(Stats, isRanged, dualDistFeet,
            out bool pointBlankActive, out int pbsAtkBonus, out int pbsDmgBonus);

        int mainWFBonus = FeatManager.GetWeaponFocusBonus(Stats, mainWeapon?.Name ?? "Unarmed");
        int offWFBonus = FeatManager.GetWeaponFocusBonus(Stats, offWeapon?.Name ?? "Unarmed");
        int mainWSBonus = FeatManager.GetWeaponSpecializationBonus(Stats, mainWeapon?.Name ?? "Unarmed");
        int offWSBonus = FeatManager.GetWeaponSpecializationBonus(Stats, offWeapon?.Name ?? "Unarmed");

        // Weapon Finesse (via AttackCalculator)
        int finesseAtkAdjust = 0;
        AttackCalculator.GetAttackAbilityModifier(Stats, mainWeapon, isRanged, out int abilityMod, out string abilityName);
        if (isMelee && abilityName == "DEX(Finesse)")
        {
            finesseAtkAdjust = Stats.DEXMod - Stats.STRMod;
            abilityName = "DEX"; // dual-wield labels use simpler name
        }

        // Combat Expertise (via AttackCalculator)
        int combatExpertisePenalty = AttackCalculator.CalculateCombatExpertisePenalty(Stats, isMelee);

        int proneAttackPenalty = GetProneAttackModifier(isMelee);
        int fightingDefensivelyPenalty = IsFightingDefensively ? -4 : 0;
        bool preciseShotNegated = false;
        int shootingIntoMeleePenalty = GetShootingIntoMeleePenalty(this, target, isRanged, out preciseShotNegated);
        int solidFogAtkPenalty = isMelee ? Stats.SolidFogMeleeAttackPenalty : 0;
        int solidFogDmgPenalty = isMelee ? Stats.SolidFogMeleeDamagePenalty : 0;

        // Main-hand attack
        if (canMainAttack)
        {
            int mainAidAnotherAttackBonus = ConsumeAidAnotherAttackBonus(target);
            int mainAidAnotherTargetAcBonus = ConsumeAidAnotherAcBonus(target);
            int mainAtkMod = Stats.AttackBonus + mainPenalty + (isFlanking ? flankingBonus : 0) + racialAtkBonus + rangePenalty
                             + powerAtkPenalty + pbsAtkBonus + mainWFBonus + finesseAtkAdjust + combatExpertisePenalty
                             + proneAttackPenalty + fightingDefensivelyPenalty + shootingIntoMeleePenalty
                             + mainWeaponNonProfPenalty + armorNonProfPenalty + mainAidAnotherAttackBonus
                             + mainDamageModeProfile.AttackPenalty + solidFogAtkPenalty;
            string mainLabel = $"Attack 1 - Main Hand ({mainWeapon.Name})";

            int mainCritMin = FeatManager.GetAdjustedCritThreatMin(Stats, mainWeapon.CritThreatMin > 0 ? mainWeapon.CritThreatMin : 20);
            int mainCritMult = mainWeapon.CritMultiplier > 0 ? mainWeapon.CritMultiplier : 2;
            int totalMainFeatDmg = powerAtkDmgBonus + pbsDmgBonus + mainWSBonus + solidFogDmgPenalty;

            GetScaledWeaponDamageDice(mainWeapon, out int mainDamageCount, out int mainDamageDice);

            int hpBeforeMain = target.Stats.CurrentHP;
            CombatResult mainAtk = PerformSingleAttackWithCrit(target, mainAtkMod, isFlanking, flankingBonus, flankingPartnerName,
                mainDamageDice, mainDamageCount, mainWeapon.BonusDamage, mainCritMin, mainCritMult,
                mainWeapon, false, totalMainFeatDmg, mainAidAnotherTargetAcBonus,
                mainDamageModeProfile.DealNonlethalDamage, mainDamageModeProfile.AttackPenalty, mainDamageModeProfile.PenaltySource);

            mainAtk.RacialAttackBonus = racialAtkBonus;
            mainAtk.SizeAttackBonus = Stats.SizeModifier;
            mainAtk.PowerAttackValue = (powerAtkPenalty != 0) ? PowerAttackValue : 0;
            mainAtk.PowerAttackDamageBonus = powerAtkDmgBonus;
            mainAtk.PointBlankShotActive = pointBlankActive;
            mainAtk.FeatDamageBonus = totalMainFeatDmg;
            mainAtk.WeaponFocusBonus = mainWFBonus;
            mainAtk.WeaponSpecBonus = mainWSBonus;
            mainAtk.CombatExpertisePenalty = combatExpertisePenalty;
            mainAtk.FightingDefensivelyAttackPenalty = fightingDefensivelyPenalty;
            mainAtk.ShootingIntoMeleePenalty = shootingIntoMeleePenalty;
            mainAtk.AidAnotherAttackBonus = mainAidAnotherAttackBonus;
            mainAtk.AidAnotherTargetAcBonus = mainAidAnotherTargetAcBonus;
            mainAtk.PreciseShotNegated = preciseShotNegated;
            mainAtk.FightingDefensivelyACBonus = target != null && target.IsFightingDefensively ? 2 : 0;
            mainAtk.WeaponName = mainWeapon.Name;
            mainAtk.BaseDamageDiceStr = $"{mainDamageCount}d{mainDamageDice}";
            mainAtk.IsDualWieldAttack = true;
            mainAtk.IsOffHandAttack = false;
            mainAtk.BreakdownBAB = Stats.BaseAttackBonus;
            mainAtk.BreakdownAbilityMod = abilityMod;
            mainAtk.BreakdownAbilityName = abilityName;
            mainAtk.BreakdownDualWieldPenalty = mainPenalty;
            mainAtk.WeaponNonProficiencyPenalty = mainWeaponNonProfPenalty;
            mainAtk.ArmorNonProficiencyPenalty = armorNonProfPenalty;
            mainAtk.DefenderHPBefore = hpBeforeMain;
            mainAtk.DefenderHPAfter = target.Stats.CurrentHP;

            if (rangeInfo != null && !rangeInfo.IsMelee && rangeInfo.IsInRange)
            {
                mainAtk.IsRangedAttack = true;
                mainAtk.RangeDistanceFeet = rangeInfo.DistanceFeet;
                mainAtk.RangeDistanceSquares = rangeInfo.SquareDistance;
                mainAtk.RangeIncrementNumber = rangeInfo.IncrementNumber;
                mainAtk.RangePenalty = rangeInfo.Penalty;
            }

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
            int offAidAnotherAttackBonus = ConsumeAidAnotherAttackBonus(target);
            int offAidAnotherTargetAcBonus = ConsumeAidAnotherAcBonus(target);
            int offAtkMod = Stats.AttackBonus + offPenalty + (isFlanking ? flankingBonus : 0) + racialAtkBonus + rangePenalty
                            + powerAtkPenalty + pbsAtkBonus + offWFBonus + finesseAtkAdjust + combatExpertisePenalty
                            + proneAttackPenalty + fightingDefensivelyPenalty + shootingIntoMeleePenalty
                            + offWeaponNonProfPenalty + armorNonProfPenalty + offAidAnotherAttackBonus
                            + offDamageModeProfile.AttackPenalty + solidFogAtkPenalty;
            bool offHandShieldBash = IsShieldBashWeapon(offWeapon);
            string offLabel = offHandFromSpikedGauntlet
                ? $"Attack 2 - Off Hand ({offWeapon.Name}, Hands Slot)"
                : offHandShieldBash
                    ? $"Attack 2 - Off Hand (Shield Bash: {offWeapon.Name})"
                    : $"Attack 2 - Off Hand ({offWeapon.Name})";

            int offCritMin = FeatManager.GetAdjustedCritThreatMin(Stats, offWeapon.CritThreatMin > 0 ? offWeapon.CritThreatMin : 20);
            int offCritMult = offWeapon.CritMultiplier > 0 ? offWeapon.CritMultiplier : 2;
            int totalOffFeatDmg = powerAtkDmgBonus + pbsDmgBonus + offWSBonus + solidFogDmgPenalty;

            GetScaledWeaponDamageDice(offWeapon, out int offDamageCount, out int offDamageDice);

            int hpBeforeOff = target.Stats.CurrentHP;
            CombatResult offAtk = PerformSingleAttackWithCrit(target, offAtkMod, isFlanking, flankingBonus, flankingPartnerName,
                offDamageDice, offDamageCount, offWeapon.BonusDamage, offCritMin, offCritMult,
                offWeapon, true, totalOffFeatDmg, offAidAnotherTargetAcBonus,
                offDamageModeProfile.DealNonlethalDamage, offDamageModeProfile.AttackPenalty, offDamageModeProfile.PenaltySource);

            offAtk.RacialAttackBonus = racialAtkBonus;
            offAtk.SizeAttackBonus = Stats.SizeModifier;
            offAtk.PowerAttackValue = (powerAtkPenalty != 0) ? PowerAttackValue : 0;
            offAtk.PowerAttackDamageBonus = powerAtkDmgBonus;
            offAtk.PointBlankShotActive = pointBlankActive;
            offAtk.FeatDamageBonus = totalOffFeatDmg;
            offAtk.AidAnotherAttackBonus = offAidAnotherAttackBonus;
            offAtk.AidAnotherTargetAcBonus = offAidAnotherTargetAcBonus;
            offAtk.WeaponFocusBonus = offWFBonus;
            offAtk.WeaponSpecBonus = offWSBonus;
            offAtk.CombatExpertisePenalty = combatExpertisePenalty;
            offAtk.FightingDefensivelyAttackPenalty = fightingDefensivelyPenalty;
            offAtk.ShootingIntoMeleePenalty = shootingIntoMeleePenalty;
            offAtk.PreciseShotNegated = preciseShotNegated;
            offAtk.FightingDefensivelyACBonus = target != null && target.IsFightingDefensively ? 2 : 0;
            offAtk.WeaponName = offWeapon.Name;
            offAtk.BaseDamageDiceStr = $"{offDamageCount}d{offDamageDice}";
            offAtk.IsDualWieldAttack = true;
            offAtk.IsOffHandAttack = true;
            offAtk.BreakdownBAB = Stats.BaseAttackBonus;
            offAtk.BreakdownAbilityMod = abilityMod;
            offAtk.BreakdownAbilityName = abilityName;
            offAtk.BreakdownDualWieldPenalty = offPenalty;
            offAtk.WeaponNonProficiencyPenalty = offWeaponNonProfPenalty;
            offAtk.ArmorNonProficiencyPenalty = armorNonProfPenalty;
            offAtk.DefenderHPBefore = hpBeforeOff;
            offAtk.DefenderHPAfter = target.Stats.CurrentHP;

            if (rangeInfo != null && !rangeInfo.IsMelee && rangeInfo.IsInRange)
            {
                offAtk.IsRangedAttack = true;
                offAtk.RangeDistanceFeet = rangeInfo.DistanceFeet;
                offAtk.RangeDistanceSquares = rangeInfo.SquareDistance;
                offAtk.RangeIncrementNumber = rangeInfo.IncrementNumber;
                offAtk.RangePenalty = rangeInfo.Penalty;
            }

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
            targetAC += isRangedAttack ? 4 : -4;

        // D&D 3.5 Pinned AC rule:
        // Pinned creatures take a -4 AC penalty against opponents other than the creature pinning them.
        if (target.HasCondition(CombatConditionType.Pinned))
        {
            bool attackerIsGrappleOpponent = target.TryGetGrappleState(out CharacterController grappleOpponent, out _, out _, out _)
                && grappleOpponent == attacker;
            if (!attackerIsGrappleOpponent)
                targetAC -= 4;
        }

        if (target.IsFightingDefensively)
            targetAC += 2; // Dodge bonus

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

        if (target.HasCondition(CombatConditionType.FlatFooted))
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

        string creatureType = string.IsNullOrEmpty(target.Stats.CreatureType)
            ? string.Empty
            : target.Stats.CreatureType.Trim().ToLowerInvariant();

        // D&D 3.5 precision-damage immunity (minimum implemented set requested by design task).
        return creatureType == "undead"
            || creatureType == "construct"
            || creatureType == "ooze";
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

        // D&D 3.5 baseline critical-hit immunity used in this prototype.
        return creatureType == "undead"
            || creatureType == "construct"
            || creatureType == "ooze";
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

        if (target.HasCondition(CombatConditionType.FlatFooted))
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

        return false;
    }

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
            gm.CombatUI.ShowCombatLog($"{attackerName} attacks {targetName}'s last known position.");
            gm.CombatUI.ShowCombatLog($"  Last known: {lastKnownText}");
            gm.CombatUI.ShowCombatLog($"  Current: {currentText}");
            gm.CombatUI.ShowCombatLog("  ATTACKING EMPTY SQUARE: automatic miss (no attack roll)");
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
    /// Perform a single attack with full D&D 3.5 critical hit mechanics.
    /// Uses the weapon's DamageModifierType to determine STR bonus to damage.
    /// Step 1: Roll d20. Check if in threat range.
    /// Step 2: If threat, roll confirmation vs same AC with same bonus.
    /// Step 3: If confirmed, multiply weapon dice (not static bonuses or sneak attack).
    /// </summary>
    /// <param name="weapon">The weapon being used (null = unarmed)</param>
    /// <param name="isOffHand">True if this is an off-hand attack (overrides to 0.5× STR)</param>
    /// <param name="featDamageBonus">Extra flat damage from feats (Power Attack, Point Blank Shot)</param>
    private CombatResult PerformSingleAttackWithCrit(CharacterController target, int totalAtkMod,
        bool isFlanking, int flankingBonus, string flankingPartnerName,
        int damageDice, int damageCount, int bonusDamage,
        int critThreatMin, int critMultiplier,
        ItemData weapon, bool isOffHand, int featDamageBonus = 0, int situationalTargetAcBonus = 0,
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

        bool suppressStrengthToDamage = WeaponDisablesStrengthDamageBonuses(weapon);

        // Calculate the damage modifier based on weapon's DamageModifierType
        int damageModifier = Stats.GetWeaponDamageModifier(weapon, isOffHand);
        string damageModDesc = Stats.GetDamageModifierDescription(weapon, isOffHand);
        if (suppressStrengthToDamage)
        {
            damageModifier = 0;
            damageModDesc = "no STR modifier";
            result.SpecialAttackNote = "Weapon rule: Strength modifier is not added to damage.";
        }

        result.DamageModifier = damageModifier;
        result.DamageModifierDesc = damageModDesc;

        bool isRangedAttack = weapon != null && (weapon.WeaponCat == WeaponCategory.Ranged || weapon.RangeIncrement > 0);

        TrueStrikeEffect trueStrike = GetComponent<TrueStrikeEffect>();
        bool trueStrikeActive = trueStrike != null && trueStrike.IsActive();
        bool ignoreConcealmentForThisAttack = trueStrikeActive;
        int trueStrikeBonus = trueStrikeActive ? trueStrike.GetAttackBonus() : 0;

        bool targetIsHelplessLike = !isRangedAttack && target != null && target.IsHelplessForCoupDeGrace();
        int helplessMeleeAttackBonus = targetIsHelplessLike ? 4 : 0;

        int weaponEnhancementAttackBonus = weapon != null ? weapon.GetEnhancementAttackBonus() : 0;
        int weaponEnhancementDamageBonus = weapon != null ? weapon.GetEnhancementDamageBonus() : 0;
        result.WeaponEnhancementAttackBonus = weaponEnhancementAttackBonus;
        result.WeaponEnhancementDamageBonus = weaponEnhancementDamageBonus;

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

        // Destruction Domain Smite: +4 attack on next melee attack
        int destructionSmiteAttackBonus = GameManager.GetDestructionSmiteAttackBonus(this);

        int totalAtkModWithTrueStrike = totalAtkMod + weaponEnhancementAttackBonus + trueStrikeBonus + helplessMeleeAttackBonus + blindedTargetAttackBonus + invisibleAttackerBonus + blinkAttackerBonus + destructionSmiteAttackBonus;

        // D&D 3.5e: making an attack roll (or attempting to attack) breaks standard invisibility.
        // Greater Invisibility does NOT break on attack (BreaksOnAttack=false).
        BreakInvisibility("attack roll", target);

        if (TryResolveLastKnownPositionAutoMiss(target, isRangedAttack, weapon, out CombatResult emptySquareMiss))
            return emptySquareMiss;

        if (GameManager.Instance != null && GameManager.Instance.TryHandleMirrorImageCloneAttacked(this, target, result, totalAtkModWithTrueStrike, out CombatResult mirrorCloneResult))
        {
            mirrorCloneResult.IsRangedAttack = isRangedAttack;
            if (trueStrikeActive)
                trueStrike.ConsumeOnAttackRoll();

            mirrorCloneResult.RebuildBreakdownsFromComputedValues();
            return mirrorCloneResult;
        }

        int targetAC = GetSituationalTargetArmorClass(target, this, isRangedAttack) + Mathf.Max(0, situationalTargetAcBonus);

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

        if (protection.DeflectionAcBonus > 0)
        {
            targetAC += protection.DeflectionAcBonus;
            result.ProtectionDeflectionBonusToAc = protection.DeflectionAcBonus;
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

        // Step 1: Roll to hit
        var (hit, roll, total) = Stats.RollToHitWithMod(totalAtkModWithTrueStrike, targetAC);

        // Log Luck domain reroll if it was triggered during the attack roll
        GameManager.Instance?.LogLuckRerollIfTriggered(Stats);

        result.DieRoll = roll;
        result.TotalRoll = total;
        result.TargetAC = targetAC;
        result.Hit = hit;
        result.IsRangedAttack = isRangedAttack;
        result.NaturalTwenty = (roll == 20);
        result.NaturalOne = (roll == 1);

        // Consume Destruction Domain Smite immediately after the attack roll (D&D 3.5e: declared before attack)
        if (destructionSmiteAttackBonus > 0)
        {
            result.AddAttackBuffDebuffModifier("Destruction Smite", destructionSmiteAttackBonus);
            GameManager.ConsumeDestructionSmite(this);
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

        // Step 2: Concealment miss chance check (rolled after a successful attack roll)
        bool isThreat = false;
        bool critConfirmed = false;
        int confirmRoll = 0;
        int confirmTotal = 0;

        if (hit)
        {
            int targetMissChance = target.GetMissChance(this, isRangedAttack);
            int blindedAttackerMissChance = HasCondition(CombatConditionType.Blinded) ? 50 : 0;
            int missChance = Mathf.Max(targetMissChance, blindedAttackerMissChance);

            if (target.HasActiveInvisibilityEffect && CanSeeInvisible(target) && targetMissChance <= 0)
            {
                string attackerName = Stats != null ? Stats.CharacterName : name;
                string targetName = target.Stats != null ? target.Stats.CharacterName : target.name;
                GameManager.Instance?.CombatUI?.ShowCombatLog($"<color=#A6F3FF>👁 {attackerName} sees invisible {targetName} clearly (no concealment).</color>");
            }

            if (targetMissChance > 0 && CanSeeInvisible(target) && target.HasActiveBlurEffect)
            {
                string note = "Blur effect remains despite magical sight.";
                result.SpecialAttackNote = string.IsNullOrEmpty(result.SpecialAttackNote)
                    ? note
                    : $"{result.SpecialAttackNote} {note}";
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
                    critConfirmed = confirmed;
                    confirmRoll = confRoll;
                    confirmTotal = confTotal;
                    result.CritConfirmed = critConfirmed;
                    result.ConfirmationRoll = confirmRoll;
                    result.ConfirmationTotal = confirmTotal;
                }
            }

            // Step 3: Roll weapon damage (feat bonus added as flat bonus, not multiplied on crit)
            int rawWeaponDamage;
            int baseDmgRoll;
            if (critConfirmed)
            {
                // Critical damage: multiply weapon dice, add static bonuses (STR + bonus) once
                int totalCritDice = damageCount * critMultiplier;
                baseDmgRoll = Stats.RollBaseDamage(damageDice, totalCritDice);
                rawWeaponDamage = baseDmgRoll + damageModifier + bonusDamage;
                rawWeaponDamage += featDamageBonus; // Feat bonus added after crit multiplication
                rawWeaponDamage += weaponEnhancementDamageBonus;
                if (suppressStrengthToDamage && weaponEnhancementDamageBonus > 0 && critMultiplier > 1)
                {
                    // Torch-style weapons: critical hit doubles fixed weapon damage package,
                    // including enhancement contribution for the weapon's base damage.
                    rawWeaponDamage += weaponEnhancementDamageBonus * (critMultiplier - 1);
                }

                int critStatic = damageModifier + bonusDamage + weaponEnhancementDamageBonus
                    + ((suppressStrengthToDamage && weaponEnhancementDamageBonus > 0 && critMultiplier > 1)
                        ? weaponEnhancementDamageBonus * (critMultiplier - 1)
                        : 0);
                result.CritDamageDice = $"{totalCritDice}d{damageDice}+{critStatic}";
            }
            else
            {
                // Normal damage - roll weapon dice separately for breakdown
                baseDmgRoll = Stats.RollBaseDamage(damageDice, damageCount);
                int destructionSmiteDmgBonus = GameManager.GetDestructionSmiteDamageBonus(this);
                rawWeaponDamage = baseDmgRoll + damageModifier + bonusDamage + featDamageBonus + weaponEnhancementDamageBonus + destructionSmiteDmgBonus;
            }
            rawWeaponDamage = Mathf.Max(1, rawWeaponDamage); // Weapon hit always deals at least 1 before mitigation
            result.Damage = rawWeaponDamage;
            result.BaseDamageRoll = baseDmgRoll;

            // Sneak attack: applies if attacker is Rogue and target is either flanked
            // or denied DEX to AC (feint, flat-footed, stunned, etc.).
            // Sneak attack is NOT multiplied on critical hits (D&D 3.5 rule)
            int rawSneakDamage = 0;
            bool deniedDexForSneak = IsTargetDeniedDexForSneakAttack(target, !isRangedAttack, feintWindowConsumed, out string dexDeniedReason);
            bool sneakAttackEligible = Stats.IsRogue && (isFlanking || deniedDexForSneak);

            if (sneakAttackEligible && target.HasConcealment(isRangedAttack))
            {
                result.SneakAttackTriggerReason = "target has concealment";
                Debug.Log($"[Sneak Attack] {Stats.CharacterName} cannot sneak attack {target.Stats.CharacterName}: target has concealment.");
                sneakAttackEligible = false;
            }

            if (sneakAttackEligible)
            {
                if (IsTargetImmuneToSneakAttackDamage(target))
                {
                    string immunityReason = $"target creature type '{target.Stats.CreatureType}' is immune to sneak attack precision damage";
                    result.SneakAttackTriggerReason = immunityReason;
                    Debug.Log($"[Sneak Attack] {Stats.CharacterName} cannot sneak attack {target.Stats.CharacterName}: {immunityReason}.");
                }
                else
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

            result.Damage = rawWeaponDamage;
            result.RawTotalDamage = rawTotalDamage;

            // Build damage packet for DR/resistance/immunity resolution
            var damageTypes = weapon != null
                ? weapon.GetDamageTypes()
                : new System.Collections.Generic.HashSet<DamageType> { DamageType.Bludgeoning };

            DamageBypassTag attackTags = weapon != null ? weapon.GetBypassTags() : DamageBypassTag.Bludgeoning;
            if (weapon != null && (weapon.WeaponCat == WeaponCategory.Ranged || weapon.RangeIncrement > 0))
                attackTags |= DamageBypassTag.Ranged;

            // D&D 3.5e Magic Stone: sling attacks with active Magic Stone count as magic weapons (PHB p.251)
            if (Stats != null && Stats.MagicStoneActive && Stats.MagicStoneCharges >= 0
                && weapon != null && weapon.Id == ItemIDs.SLING)
                attackTags |= DamageBypassTag.Magic;

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

            if (target.Stats.IsDead)
            {
                target.OnDeath();
                result.TargetKilled = true;
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
            case SpecialAttackType.BullRushAttack:
            case SpecialAttackType.BullRushCharge:
            case SpecialAttackType.Overrun:
            case SpecialAttackType.CoupDeGrace:
                return CanPerformSpecialMeleeAttacks();
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
    /// </summary>
    public bool CanMeleeAttackDistance(int squareDistance, ItemData weapon = null)
    {
        if (squareDistance <= 0) return false;
        int minDist = GetMeleeMinAttackDistance(weapon);
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
        link.PinExpiresAfterMaintainerTurnStartCount = 0;

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
        link.PinExpiresAfterMaintainerTurnStartCount = 0;
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

    private string EstablishGrappleWith(CharacterController target)
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
            PinExpiresAfterMaintainerTurnStartCount = 0
        };

        RegisterGrappleLink(link);
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
    /// Convenience alias for UI/gameplay checks that need to know whether this character
    /// is currently in an active grapple.
    /// </summary>
    public bool IsGrappling()
    {
        return IsInActiveGrapple();
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

    public bool IsGrappleActionBlockedWhilePinning(GrappleActionType actionType, out string blockedReason)
    {
        blockedReason = string.Empty;

        if (!IsPinningOpponent())
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
        BreakPin,
        ResistPin
    }

    private int GetGreasedArmorGrappleBonus(GrappleCheckContext context)
    {
        StatusEffectManager statusEffectManager = GetComponent<StatusEffectManager>();
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
                case GrappleCheckContext.BreakPin:
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

        StatusEffectManager statusEffectManager = GetComponent<StatusEffectManager>();
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

        StatusEffectManager statusEffectManager = GetComponent<StatusEffectManager>();
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

        if (Stats != null)
        {
            if (Stats.ConditionAttackPenalty != 0)
                result.AddMiscModifier(Stats.ConditionAttackPenalty, "Condition modifiers");

            if (Stats.HasFeat("Improved Grapple"))
                result.AddMiscModifier(4, "Improved Grapple feat");
        }

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
                case GrappleCheckContext.BreakPin:
                    label = "Greased armor (break pin)";
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

        int spellAttackBonusTotal = 0;
        StatusEffectManager statusEffectManager = GetComponent<StatusEffectManager>();
        if (statusEffectManager != null && statusEffectManager.ActiveEffects != null)
        {
            for (int i = 0; i < statusEffectManager.ActiveEffects.Count; i++)
            {
                ActiveSpellEffect effect = statusEffectManager.ActiveEffects[i];
                if (effect == null)
                    continue;

                int attackBonus = effect.AppliedAttackBonus;
                if (attackBonus == 0)
                    continue;

                string spellLabel = effect.Spell != null && !string.IsNullOrWhiteSpace(effect.Spell.Name)
                    ? effect.Spell.Name
                    : "Spell effect";
                result.AddAttackBuffDebuffModifier(spellLabel, attackBonus);
                spellAttackBonusTotal += attackBonus;
            }
        }

        int remainingMoraleAttackBonus = Stats.MoraleAttackBonus - spellAttackBonusTotal;
        if (remainingMoraleAttackBonus != 0)
        {
            string moraleLabel = "Other morale effects";
            if (spellAttackBonusTotal == 0 && remainingMoraleAttackBonus == 2)
                moraleLabel = "Charge";

            result.AddAttackBuffDebuffModifier(moraleLabel, remainingMoraleAttackBonus);
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

    private static string BuildOpposedResultLine(string actorName, int actorTotal, string opponentName, int opponentTotal)
    {
        if (actorTotal > opponentTotal)
            return $"Result: {actorName} wins ({actorTotal} vs {opponentTotal})";

        if (actorTotal == opponentTotal)
            return $"Result: Tie - {opponentName} wins ({opponentTotal} vs {actorTotal})";

        return $"Result: {opponentName} wins ({opponentTotal} vs {actorTotal})";
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

    public SpecialAttackResult ResolveGrappleAction(
        GrappleActionType actionType,
        AttackDamageMode? grappleDamageModeOverride = null,
        EquipSlot? opponentWeaponHandSlotOverride = null,
        int? iterativeAttackBonusOverride = null)
    {
        if (!TryGetGrappleState(out CharacterController opponent, out _, out bool isPinned, out bool opponentPinned))
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Grapple Action",
                Success = false,
                Log = $"{Stats.CharacterName} is not currently in a grapple."
            };
        }

        bool isPinning = IsPinningOpponent();
        if (IsGrappleActionBlockedWhilePinning(actionType, out string blockedReason))
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

                GrappleCheckResult myCheck = RollGrappleCheck(iterativeAttackBonusOverride, context: GrappleCheckContext.EscapeGrapple);
                GrappleCheckResult oppCheck = opponent.RollGrappleCheck(context: GrappleCheckContext.ResistGrapple);

                bool success = myCheck.Total > oppCheck.Total;
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
                    BuildOpposedResultLine(Stats.CharacterName, myCheck.Total, opponent.Stats.CharacterName, oppCheck.Total) + "\n" + outcome
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
                        Log = $"{Stats.CharacterName} is pinned and cannot deal grapple damage until they break the pin."
                    };
                }

                bool isMonk = Stats != null && Stats.IsMonk;
                bool hasImprovedUnarmedStrike = FeatManager.HasImprovedUnarmedStrike(Stats);
                bool usesMonkOrIusException = isMonk || hasImprovedUnarmedStrike;
                string grappleDamageRuleReason = isMonk
                    ? "Monk exception"
                    : (hasImprovedUnarmedStrike ? "Improved Unarmed Strike feat" : null);

                AttackDamageMode selectedMode = grappleDamageModeOverride
                    ?? (usesMonkOrIusException ? AttackDamageMode.Lethal : AttackDamageMode.Nonlethal);
                bool dealNonlethalDamage = selectedMode == AttackDamageMode.Nonlethal;
                int grappleCheckPenalty = (!usesMonkOrIusException && !dealNonlethalDamage) ? -4 : 0;

                if (opponent == null || opponent.Stats == null || Stats == null)
                {
                    return new SpecialAttackResult
                    {
                        ManeuverName = "Grapple Damage",
                        Success = false,
                        Log = "No valid grapple opponent."
                    };
                }

                GrappleCheckResult myCheck = RollGrappleCheck(iterativeAttackBonusOverride, grappleCheckPenalty, grappleCheckPenalty != 0 ? "Lethal damage without Monk/Improved Unarmed Strike" : null);
                GrappleCheckResult oppCheck = opponent.RollGrappleCheck();

                bool success = myCheck.Total > oppCheck.Total;
                int finalDamageDealt = 0;
                int rawDamage = 0;
                var unarmed = GetUnarmedDamage();
                int damageDiceCount = Mathf.Max(1, unarmed.damageCount);
                int damageDiceSides = Mathf.Max(2, unarmed.damageDice);
                int bonusDamage = Stats.STRMod + unarmed.bonusDamage;
                var damageRolls = new List<int>();

                if (success)
                {
                    for (int i = 0; i < damageDiceCount; i++)
                    {
                        int die = DiceService.RollDie(damageDiceSides, "Grapple damage");
                        damageRolls.Add(die);
                        rawDamage += die;
                    }

                    rawDamage += bonusDamage;
                    rawDamage = Mathf.Max(1, rawDamage);

                    var packet = new DamagePacket
                    {
                        RawDamage = rawDamage,
                        Types = new HashSet<DamageType> { DamageType.Bludgeoning },
                        AttackTags = DamageBypassTag.Bludgeoning,
                        IsRanged = false,
                        IsNonlethal = dealNonlethalDamage,
                        Source = AttackSource.Weapon,
                        SourceName = "Grapple damage (unarmed strike)"
                    };

                    DamageResolutionResult mitigation = opponent.Stats.ApplyIncomingDamage(rawDamage, packet);
                    finalDamageDealt = mitigation.FinalDamage;

                    if (opponent.Stats.IsDead)
                    {
                        opponent.OnDeath();
                        ReleaseGrappleState("opponent killed in grapple");
                    }
                }

                string damageTypeLabel = dealNonlethalDamage ? "nonlethal" : "lethal";
                string defaultDamageRuleSummary = grappleDamageRuleReason != null
                    ? $"Deals lethal damage by default ({grappleDamageRuleReason})."
                    : "Deals nonlethal damage by default (no Monk/Improved Unarmed Strike exception).";
                string penaltyRuleSummary = grappleCheckPenalty != 0
                    ? "Applies -4 penalty for choosing lethal damage without Monk/Improved Unarmed Strike exception."
                    : (grappleDamageRuleReason != null
                        ? $"No penalty ({grappleDamageRuleReason})."
                        : "No penalty (nonlethal default).");

                string resultLine = BuildOpposedResultLine(Stats.CharacterName, myCheck.Total, opponent.Stats.CharacterName, oppCheck.Total);
                string outcomeLine = success
                    ? $"Damage: {damageDiceCount}d{damageDiceSides}{bonusDamage:+#;-#;0} = {rawDamage} {damageTypeLabel} ({opponent.Stats.CharacterName} takes {finalDamageDealt})."
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
                return ResolveUnarmedAttackWhileGrappling(opponent, isPinned, iterativeAttackBonusOverride);
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

                if (isPinning)
                {
                    return new SpecialAttackResult
                    {
                        ManeuverName = "Pin Opponent",
                        Success = false,
                        Log = $"{Stats.CharacterName} is already pinning {opponent.Stats.CharacterName}."
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

                GrappleCheckResult myCheck = RollGrappleCheck(iterativeAttackBonusOverride);
                GrappleCheckResult oppCheck = opponent.RollGrappleCheck(context: GrappleCheckContext.ResistPin);

                bool success = myCheck.Total > oppCheck.Total;
                if (success && TryGetGrappleLink(this, out GrappleLink link))
                {
                    SetPinnedState(link, opponent, this);
                    RemoveConditionIfPresent(this, CombatConditionType.Pinned);
                }

                string outcomeLine = success
                    ? $"{opponent.Stats.CharacterName} is pinned!"
                    : $"{Stats.CharacterName} fails to pin {opponent.Stats.CharacterName}.";

                string log = string.Join("\n\n", new[]
                {
                    $"{Stats.CharacterName} attempts to pin {opponent.Stats.CharacterName}",
                    myCheck.GetBreakdown(),
                    oppCheck.GetBreakdown(),
                    BuildOpposedResultLine(Stats.CharacterName, myCheck.Total, opponent.Stats.CharacterName, oppCheck.Total) + "\n" + outcomeLine
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
            case GrappleActionType.BreakPin:
            {
                if (!isPinned)
                {
                    return new SpecialAttackResult
                    {
                        ManeuverName = "Break Pin",
                        Success = false,
                        Log = $"{Stats.CharacterName} is not pinned."
                    };
                }

                if (opponent == null || opponent.Stats == null || Stats == null)
                {
                    return new SpecialAttackResult
                    {
                        ManeuverName = "Break Pin",
                        Success = false,
                        Log = "No valid grapple opponent."
                    };
                }

                GrappleCheckResult myCheck = RollGrappleCheck(iterativeAttackBonusOverride, context: GrappleCheckContext.BreakPin);
                GrappleCheckResult oppCheck = opponent.RollGrappleCheck();

                bool success = myCheck.Total > oppCheck.Total;
                if (success && TryGetGrappleLink(this, out GrappleLink link))
                {
                    ClearPinnedState(link);
                }

                string outcomeLine = success
                    ? $"{Stats.CharacterName} breaks the pin from {opponent.Stats.CharacterName}!"
                    : $"{Stats.CharacterName} fails to break the pin.";

                string log = string.Join("\n\n", new[]
                {
                    $"{Stats.CharacterName} attempts to break pin from {opponent.Stats.CharacterName}",
                    myCheck.GetBreakdown(),
                    oppCheck.GetBreakdown(),
                    BuildOpposedResultLine(Stats.CharacterName, myCheck.Total, opponent.Stats.CharacterName, oppCheck.Total) + "\n" + outcomeLine
                });

                return new SpecialAttackResult
                {
                    ManeuverName = "Break Pin",
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
                int myBaseModifier = GetGrappleModifier();

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
                    bool beatThisOpponent = myTotal > oppTotal;
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

                if (TryGetGrappleLink(this, out GrappleLink link))
                    ClearPinnedState(link);

                string opponentName = opponent != null && opponent.Stats != null
                    ? opponent.Stats.CharacterName
                    : "opponent";

                return new SpecialAttackResult
                {
                    ManeuverName = "Release Pinned Opponent",
                    Success = true,
                    Log = $"{Stats.CharacterName} releases {opponentName} from pin. Both remain in the grapple."
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

    private int GetStrictOpposedGrappleModifierForUseOpponentWeapon(int? baseAttackBonusOverride = null)
    {
        int sizeMod = Stats != null ? Stats.CurrentSizeCategory.GetGrappleModifier() : 0;
        int bab = baseAttackBonusOverride ?? (Stats != null ? Stats.BaseAttackBonus : 0);
        return bab + (Stats != null ? Stats.STRMod : 0) + sizeMod;
    }

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

        myModifier = GetStrictOpposedGrappleModifierForUseOpponentWeapon(myBaseAttackBonusOverride);
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

        bool grappleSuccess = myTotal >= oppTotal;
        var logLines = new List<string>
        {
            $"{Stats.CharacterName} attempts to use opponent's {opponentWeapon.Name}.",
            string.IsNullOrEmpty(selectionNote) ? string.Empty : selectionNote,
            BuildD20Formula($"{Stats.CharacterName} grapple check", myRoll, myModifier, myTotal),
            BuildD20Formula($"{opponent.Stats.CharacterName} grapple check", oppRoll, oppModifier, oppTotal),
            BuildOpposedResultLine(Stats.CharacterName, myTotal, opponent.Stats.CharacterName, oppTotal)
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
                            + useOpponentWeaponAttackPenalty;

            GetScaledWeaponDamageDice(opponentWeapon, out int scaledOpponentDamageCount, out int scaledOpponentDamageDice);
            int damageDice = Mathf.Max(2, scaledOpponentDamageDice);
            int damageCount = Mathf.Max(1, scaledOpponentDamageCount);
            int bonusDamage = opponentWeapon.BonusDamage;
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
                bonusDamage,
                critThreatMin,
                critMultiplier,
                opponentWeapon,
                isOffHand: false,
                featDamageBonus: 0,
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
            logLines.Add(BuildD20Formula("Attack roll", attackResult.DieRoll, attackMod, attackResult.TotalRoll)
                + $" vs AC {attackResult.TargetAC} => {(attackResult.Hit ? "HIT" : "MISS")}");
            if (attackResult.Hit)
            {
                logLines.Add(BuildDamageFormula(
                    "Damage roll",
                    string.IsNullOrEmpty(attackResult.BaseDamageDiceStr) ? $"{damageCount}d{damageDice}" : attackResult.BaseDamageDiceStr,
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

    private SpecialAttackResult ResolveUnarmedAttackWhileGrappling(CharacterController opponent, bool isPinned, int? iterativeAttackBonusOverride)
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
            iterativeAttackBonusOverride: iterativeAttackBonusOverride);
    }

    private SpecialAttackResult ResolveAttackWhileGrappling(
        CharacterController opponent,
        ItemData weapon,
        string maneuverName,
        bool isPinned,
        bool enforceMainHandLightWeaponOnly,
        int? iterativeAttackBonusOverride)
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
        if (isUnarmed && ShouldUseInnateNaturalAttackProfile(null))
            return ResolveNaturalAttackRoutineWhileGrappling(opponent, maneuverName);

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
                        + damageMode.AttackPenalty;

        int damageDice;
        int damageCount;
        int bonusDamage;
        int critThreatMin;
        int critMultiplier;

        string grappleAttackLabel = string.Empty;

        if (isUnarmed)
        {
            if (ShouldUseInnateNaturalAttackProfile(null))
            {
                ResolveBaseAttackDamageProfile(null, out damageDice, out damageCount, out bonusDamage, out grappleAttackLabel);
                damageDice = Mathf.Max(2, damageDice);
                damageCount = Mathf.Max(1, damageCount);
                critThreatMin = 20;
                critMultiplier = 2;
            }
            else
            {
                var unarmed = GetUnarmedDamage();
                damageDice = Mathf.Max(2, unarmed.damageDice);
                damageCount = Mathf.Max(1, unarmed.damageCount);
                bonusDamage = Stats.STRMod + unarmed.bonusDamage;
                critThreatMin = 20;
                critMultiplier = 2;
            }
        }
        else
        {
            GetScaledWeaponDamageDice(weapon, out int scaledWeaponDamageCount, out int scaledWeaponDamageDice);
            damageDice = Mathf.Max(2, scaledWeaponDamageDice);
            damageCount = Mathf.Max(1, scaledWeaponDamageCount);
            bonusDamage = weapon.BonusDamage;
            critThreatMin = weapon.CritThreatMin > 0 ? weapon.CritThreatMin : 20;
            critMultiplier = weapon.CritMultiplier > 0 ? weapon.CritMultiplier : 2;
        }

        int hpBefore = opponent.Stats.CurrentHP;
        CombatResult attackResult = PerformSingleAttackWithCrit(
            opponent,
            attackMod,
            isFlanking: false,
            flankingBonus: 0,
            flankingPartnerName: null,
            damageDice,
            damageCount,
            bonusDamage,
            critThreatMin,
            critMultiplier,
            weapon,
            isOffHand: false,
            featDamageBonus: 0,
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
            damageMode.AttackPenalty != 0
                ? $"Damage-mode penalty applied: {damageMode.AttackPenalty:+#;-#;0} ({damageMode.PenaltySource})."
                : string.Empty,
            attackResult.Hit
                ? BuildDamageFormula(
                    $"Damage roll ({damageTypeLabel})",
                    string.IsNullOrEmpty(attackResult.BaseDamageDiceStr) ? $"{damageCount}d{damageDice}" : attackResult.BaseDamageDiceStr,
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

    /// <summary>
    /// Grapple natural attack routine for creatures with innate natural attacks.
    /// D&D 3.5e: Use full natural attack routine while grappling, plus rake when available.
    /// Tiger example: 2 claws + bite + 2 rakes.
    /// </summary>
    private SpecialAttackResult ResolveNaturalAttackRoutineWhileGrappling(CharacterController opponent, string maneuverName)
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

        int targetHpBefore = opponent.Stats.CurrentHP;
        FullAttackResult naturalRoutine = FullAttack(
            opponent,
            isFlanking: false,
            flankingBonus: 0,
            flankingPartnerName: null,
            rangeInfo: null,
            startAttackIndex: 0,
            maxAttacks: int.MaxValue);

        int naturalAttackCount = naturalRoutine != null && naturalRoutine.Attacks != null ? naturalRoutine.Attacks.Count : 0;
        int naturalHitCount = 0;
        int naturalDamage = 0;

        if (naturalRoutine != null && naturalRoutine.Attacks != null)
        {
            for (int i = 0; i < naturalRoutine.Attacks.Count; i++)
            {
                CombatResult attack = naturalRoutine.Attacks[i];
                if (attack != null && attack.Hit)
                {
                    naturalHitCount++;
                    naturalDamage += attack.FinalDamageDealt;
                }
            }
        }

        int rakeAttackCount = 0;
        int rakeHitCount = 0;
        int rakeDamage = 0;
        bool rakeExecuted = false;
        FullAttackResult rakeRoutine = null;

        if (Stats.HasRake
            && TryGetGrappleState(out CharacterController grappledOpponent, out _, out _, out _)
            && grappledOpponent == opponent
            && !opponent.Stats.IsDead)
        {
            rakeRoutine = PerformRakeAttacks(opponent, isFlanking: false, flankingBonus: 0, flankingPartnerName: null);
            if (rakeRoutine != null && rakeRoutine.Attacks != null && rakeRoutine.Attacks.Count > 0)
            {
                rakeExecuted = true;
                rakeAttackCount = rakeRoutine.Attacks.Count;
                for (int i = 0; i < rakeRoutine.Attacks.Count; i++)
                {
                    CombatResult attack = rakeRoutine.Attacks[i];
                    if (attack != null && attack.Hit)
                    {
                        rakeHitCount++;
                        rakeDamage += attack.FinalDamageDealt;
                    }
                }
            }
        }

        int totalAttackCount = naturalAttackCount + rakeAttackCount;
        int totalHitCount = naturalHitCount + rakeHitCount;
        int totalDamage = naturalDamage + rakeDamage;

        var logLines = new List<string>
        {
            $"{Stats.CharacterName} mauls {opponent.Stats.CharacterName} while grappling.",
            $"Natural attacks: {naturalAttackCount} (full routine while grappling).",
            rakeExecuted
                ? $"Rake attacks: {rakeAttackCount} (grapple bonus attacks)."
                : "Rake attacks: 0.",
            $"Total attacks: {totalAttackCount} | Hits: {totalHitCount} | Damage: {totalDamage}",
            $"Target HP: {targetHpBefore} -> {opponent.Stats.CurrentHP}"
        };

        if (naturalRoutine != null && naturalRoutine.Attacks != null && naturalRoutine.Attacks.Count > 0)
        {
            logLines.Add("── Natural attack roll breakdowns ──");
            for (int i = 0; i < naturalRoutine.Attacks.Count; i++)
            {
                CombatResult attack = naturalRoutine.Attacks[i];
                if (attack == null)
                    continue;

                string label = (naturalRoutine.AttackLabels != null && i < naturalRoutine.AttackLabels.Count)
                    ? naturalRoutine.AttackLabels[i]
                    : $"Natural Attack {i + 1}";
                logLines.Add(attack.GetAttackBreakdown(label));
            }
        }

        if (rakeRoutine != null && rakeRoutine.Attacks != null && rakeRoutine.Attacks.Count > 0)
        {
            logLines.Add("── Rake attack roll breakdowns ──");
            for (int i = 0; i < rakeRoutine.Attacks.Count; i++)
            {
                CombatResult attack = rakeRoutine.Attacks[i];
                if (attack == null)
                    continue;

                string label = (rakeRoutine.AttackLabels != null && i < rakeRoutine.AttackLabels.Count)
                    ? rakeRoutine.AttackLabels[i]
                    : $"Rake {i + 1}";
                logLines.Add(attack.GetAttackBreakdown(label));
            }
        }

        return new SpecialAttackResult
        {
            ManeuverName = maneuverName,
            Success = totalHitCount > 0,
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

        if (_sr != null && DeadSprite != null)
            _sr.sprite = DeadSprite;

        // Break concentration on death (D&D 3.5e: concentration ends if killed/unconscious)
        var concMgr = GetComponent<ConcentrationManager>();
        if (concMgr != null && concMgr.IsConcentrating)
        {
            concMgr.OnCharacterIncapacitated();
        }

        // Held touch charges are lost if the caster dies/unconscious.
        var spellComp = GetComponent<SpellcastingComponent>();
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
                GameManager.Instance.CombatUI.ShowCombatLog($"⏱ Pin on {pinnedNoMaintainer.Stats.CharacterName} ends because the controlling grappler can no longer maintain it.");
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
        WithdrawFirstStepProtected = false;
        IsFightingDefensively = false; // lasts until start of this character's next turn
        Actions.Reset();
        Actions.SingleActionOnly = (_currentHPState == HPState.Disabled || _currentHPState == HPState.Staggered);
        ProgressiveAttackPool.Clear();
        _grappleAttackBonusesThisTurn.Clear();
        _grappleAttacksUsedThisTurn = 0;
        _grappleAttackBudgetThisTurn = 0;
        _grappleAttackSequenceStarted = false;
        _bullRushAttackBonusesThisTurn.Clear();
        _bullRushAttacksUsedThisTurn = 0;
        _bullRushAttackBudgetThisTurn = 0;
        _bullRushAttackSequenceStarted = false;
        _disarmAttackBonusesThisTurn.Clear();
        _disarmAttacksUsedThisTurn = 0;
        _disarmAttackBudgetThisTurn = 0;
        _disarmAttackSequenceStarted = false;
        // Note: PowerAttackValue and RapidShotEnabled persist between turns
        // They are player-controlled and reset only when the player changes them

        // Reset AoO counters for the new round
        ThreatSystem.ResetAoOForTurn(this);
    }

    // ========== SPECIAL ATTACK MANEUVERS ==========

    public SpecialAttackResult ExecuteSpecialAttack(
        SpecialAttackType type,
        CharacterController target,
        EquipSlot? disarmTargetSlot = null,
        int? disarmAttackBonusOverride = null,
        int? grappleAttackBonusOverride = null,
        int? bullRushAttackBonusOverride = null,
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
        if (target == null || target.Stats == null)
        {
            return new SpecialAttackResult
            {
                ManeuverName = type.ToString(),
                Success = false,
                Log = $"{Stats.CharacterName} cannot perform {type}: no valid target."
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
            case SpecialAttackType.BullRushAttack:
                return ResolveBullRush(target, bullRushAttackBonusOverride ?? (Stats != null ? Stats.BaseAttackBonus : 0), chargeBonus: 0);
            case SpecialAttackType.BullRushCharge:
                return ResolveBullRush(target, Stats != null ? Stats.BaseAttackBonus : 0, chargeBonus: bullRushChargeBonusOverride == 0 ? 2 : bullRushChargeBonusOverride);
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

    private SpecialAttackResult ResolveTrip(CharacterController target, int? attackBonusOverride = null)
    {
        if (Stats != null && Stats.IsSwarm)
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Trip",
                Success = false,
                Log = $"{Stats.CharacterName} cannot make trip attempts while in swarm form."
            };
        }

        if (target != null && target.Stats != null && target.Stats.IsSwarm)
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Trip",
                Success = false,
                Log = $"{target.Stats.CharacterName} is a swarm and cannot be tripped."
            };
        }

        if (IsBlockedBySummonedContactBarrier(target, out _))
            return BuildSummonedContactBarrierResult(target, "Trip");

        int atkRoll = DiceService.D20("Trip attack roll");
        int defRoll = DiceService.D20("Trip defense roll");
        int attackBonus = attackBonusOverride ?? Stats.BaseAttackBonus;
        int atkTotal = atkRoll + attackBonus + Stats.STRMod + Stats.SizeModifier + Stats.TripAttackCheckBonus + Stats.ConditionAttackPenalty + (Stats.HasFeat("Improved Trip") ? 4 : 0);
        int defAbility = Mathf.Max(target.Stats.STRMod, target.Stats.DEXMod);
        int defTotal = defRoll + target.Stats.BaseAttackBonus + defAbility + target.Stats.SizeModifier + target.Stats.ConditionAttackPenalty + (target.Stats.HasFeat("Improved Trip") ? 4 : 0);

        bool success = atkTotal >= defTotal;
        if (success)
            target.ApplyCondition(CombatConditionType.Prone, -1, Stats.CharacterName);

        return new SpecialAttackResult
        {
            ManeuverName = "Trip",
            Success = success,
            CheckRoll = atkRoll,
            CheckTotal = atkTotal,
            OpposedRoll = defRoll,
            OpposedTotal = defTotal,
            Log = success
                ? $"{Stats.CharacterName} trips {target.Stats.CharacterName}! ({atkTotal} vs {defTotal}) → PRONE (until standing). [BAB {CharacterStats.FormatMod(attackBonus)}]"
                : $"{Stats.CharacterName} fails to trip {target.Stats.CharacterName}. ({atkTotal} vs {defTotal}) [BAB {CharacterStats.FormatMod(attackBonus)}]"
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
            else if (attackerHeldWeapon == null)
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
            // D&D 3.5e: failed disarm grants exactly one immediate counter-disarm attempt.
            if (TryGetDisarmTargetHeldItem(this, null, out ItemData counterTargetHeldItem, out EquipSlot counterTargetSlot))
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
                    else if (counterAttackerHeldWeapon == null)
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
    /// Checks whether this character can initiate a standard Grapple maneuver.
    /// Creatures with Improved Grab must initiate grapples through that ability only.
    /// </summary>
    public bool CanUseStandardGrapple()
    {
        if (Stats == null)
            return false;

        if (Stats.HasImprovedGrab)
        {
            Debug.Log($"[Grapple] {Stats.CharacterName} has Improved Grab and cannot use standard Grapple action.");
            return false;
        }

        return true;
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
                Log = Stats.HasImprovedGrab
                    ? $"{Stats.CharacterName} has Improved Grab and cannot initiate a standard grapple. Grapples must be triggered by Improved Grab after a qualifying hit."
                    : $"{Stats.CharacterName} cannot initiate a standard grapple right now."
            };
        }

        if (TryGetGrappleState(out CharacterController currentOpponent, out _, out _, out _))
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Grapple",
                Success = false,
                Log = $"{Stats.CharacterName} is already grappling {currentOpponent.Stats.CharacterName}."
            };
        }

        if (target.TryGetGrappleState(out CharacterController targetOpponent, out _, out _, out _))
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Grapple",
                Success = false,
                Log = $"{target.Stats.CharacterName} is already grappling {targetOpponent.Stats.CharacterName}."
            };
        }

        int touchRoll = DiceService.D20("Touch attack");
        int attackBab = iterativeAttackBonusOverride ?? Stats.BaseAttackBonus;
        int touchStr = Stats.STRMod;
        int touchSize = Stats.SizeModifier;
        int touchCondition = Stats.ConditionAttackPenalty;
        int touchTotal = touchRoll + attackBab + touchStr + touchSize + touchCondition;
        int touchAC = 10 + target.Stats.DEXMod + target.Stats.SizeModifier;

        var touchBuilder = new StringBuilder();
        touchBuilder.AppendLine("Touch attack:");
        touchBuilder.AppendLine($"  Base roll: 1d20 = {touchRoll}");
        touchBuilder.AppendLine($"  BAB: {attackBab:+0;-#;+0}");
        touchBuilder.AppendLine($"  STR modifier: {touchStr:+0;-#;+0}");
        touchBuilder.AppendLine($"  Size modifier: {touchSize:+0;-#;+0}");
        if (touchCondition != 0)
            touchBuilder.AppendLine($"  Condition modifiers: {touchCondition:+0;-#;+0}");
        touchBuilder.AppendLine($"  Total: {touchTotal}");
        touchBuilder.AppendLine($"  Target touch AC: {touchAC}");

        if (touchTotal < touchAC)
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

        GrappleCheckResult attackerCheck = RollGrappleCheck(attackBab);
        GrappleCheckResult defenderCheck = target.RollGrappleCheck(context: GrappleCheckContext.ResistGrapple);

        bool success = attackerCheck.Total > defenderCheck.Total;
        string grapplePositioningLog = string.Empty;
        if (success)
            grapplePositioningLog = EstablishGrappleWith(target);

        string resultLine = BuildOpposedResultLine(Stats.CharacterName, attackerCheck.Total, target.Stats.CharacterName, defenderCheck.Total);
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

    public SpecialAttackResult ResolveImprovedGrabFreeAttempt(CharacterController target)
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

        if (TryGetGrappleState(out CharacterController currentOpponent, out _, out _, out _))
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Improved Grab",
                Success = false,
                Log = $"{Stats.CharacterName} is already grappling {currentOpponent.Stats.CharacterName}."
            };
        }

        if (target.TryGetGrappleState(out CharacterController targetOpponent, out _, out _, out _))
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Improved Grab",
                Success = false,
                Log = $"{target.Stats.CharacterName} is already grappling {targetOpponent.Stats.CharacterName}."
            };
        }

        GrappleCheckResult attackerCheck = RollGrappleCheck();
        GrappleCheckResult defenderCheck = target.RollGrappleCheck(context: GrappleCheckContext.ResistGrapple);
        bool success = attackerCheck.Total > defenderCheck.Total;

        string grapplePositioningLog = string.Empty;
        if (success)
            grapplePositioningLog = EstablishGrappleWith(target);

        string resultLine = BuildOpposedResultLine(Stats.CharacterName, attackerCheck.Total, target.Stats.CharacterName, defenderCheck.Total);
        string outcomeLine = success
            ? $"{Stats.CharacterName} seizes {target.Stats.CharacterName} with Improved Grab!"
            : $"{Stats.CharacterName} fails to secure the grapple.";

        if (success)
            outcomeLine += " Both combatants gain the grappled condition.";

        if (success && !string.IsNullOrEmpty(grapplePositioningLog))
            outcomeLine += $" {grapplePositioningLog}";

        string log = string.Join("\n\n", new[]
        {
            $"{Stats.CharacterName} attempts Improved Grab on {target.Stats.CharacterName} (free action).",
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

        ItemData attackerWeapon = attackerWeaponOverride ?? GetEquippedMainWeapon();
        if (attackerWeapon == null)
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Sunder",
                Success = false,
                Log = $"{Stats.CharacterName} cannot sunder without a weapon."
            };
        }

        targetItem.EnsureDurabilityInitialized();

        int attackRoll = DiceService.D20("Sunder attack roll");
        int defenseRoll = DiceService.D20("Sunder defense roll");
        int attackBab = iterativeAttackBonusOverride ?? Stats.BaseAttackBonus;
        int improvedSunderBonus = Stats.HasFeat("Improved Sunder") ? 4 : 0;
        int handednessBonus = GetSunderHandednessModifier(attackerWeapon, targetItem);

        int attackTotal = attackRoll
            + attackBab
            + Stats.STRMod
            + Stats.SizeModifier
            + Stats.ConditionAttackPenalty
            + improvedSunderBonus
            + handednessBonus;

        int defenseTotal = defenseRoll
            + target.Stats.BaseAttackBonus
            + target.Stats.STRMod
            + target.Stats.SizeModifier
            + target.Stats.ConditionAttackPenalty;

        var logLines = new List<string>();
        string handLabel = usedOffHand ? "Off-Hand" : "Main Hand";
        string targetLabel = GetSunderTargetDisplayLabel(targetKind, targetItem);

        logLines.Add($"{Stats.CharacterName} attempts to sunder {target.Stats.CharacterName}'s {targetLabel} ({handLabel})");
        logLines.Add(
            $"Attacker check: d20 {attackRoll} + BAB {CharacterStats.FormatMod(attackBab)} + STR {CharacterStats.FormatMod(Stats.STRMod)} + size {CharacterStats.FormatMod(Stats.SizeModifier)}"
            + (Stats.ConditionAttackPenalty != 0 ? $" + condition {CharacterStats.FormatMod(Stats.ConditionAttackPenalty)}" : string.Empty)
            + (improvedSunderBonus != 0 ? $" + Improved Sunder {CharacterStats.FormatMod(improvedSunderBonus)}" : string.Empty)
            + (handednessBonus != 0 ? $" + leverage {CharacterStats.FormatMod(handednessBonus)}" : string.Empty)
            + (attackerDualWieldPenaltyForLog != 0 ? $" [includes dual-wield penalty {CharacterStats.FormatMod(attackerDualWieldPenaltyForLog)} in BAB]" : string.Empty)
            + $" = {attackTotal}");
        logLines.Add($"Defender check: d20 {defenseRoll} + BAB {CharacterStats.FormatMod(target.Stats.BaseAttackBonus)} + STR {CharacterStats.FormatMod(target.Stats.STRMod)} + size {CharacterStats.FormatMod(target.Stats.SizeModifier)}"
            + (target.Stats.ConditionAttackPenalty != 0 ? $" + condition {CharacterStats.FormatMod(target.Stats.ConditionAttackPenalty)}" : string.Empty)
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
        if (attackerWeapon != null)
        {
            GetScaledWeaponDamageDice(attackerWeapon, out damageDiceCount, out damageDiceSides);
            damageDiceSides = Mathf.Max(1, damageDiceSides);
            damageDiceCount = Mathf.Max(1, damageDiceCount);
        }
        else
        {
            damageDiceSides = Mathf.Max(1, Stats.BaseDamageDice);
            damageDiceCount = Mathf.Max(1, Stats.BaseDamageCount);
        }

        int damageRoll = Stats.RollBaseDamage(damageDiceSides, damageDiceCount);
        int damageAbility = Stats.GetWeaponDamageModifier(attackerWeapon, usedOffHand);
        int damageBonus = attackerWeapon.BonusDamage;
        int rawDamage = Mathf.Max(1, damageRoll + damageAbility + damageBonus);

        targetItem.ApplySunderDamage(rawDamage, out int effectiveDamage, out int hpBefore, out int hpAfter);

        logLines.Add($"Damage: {damageDiceCount}d{damageDiceSides} ({damageRoll}) + ability {CharacterStats.FormatMod(damageAbility)} + weapon {CharacterStats.FormatMod(damageBonus)} = {rawDamage}");
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

    private static int GetSunderHandednessModifier(ItemData attackerWeapon, ItemData targetItem)
    {
        if (attackerWeapon == null || targetItem == null)
            return 0;

        bool attackerTwoHanded = IsWeaponTwoHanded(attackerWeapon);
        bool attackerLight = attackerWeapon.IsLightWeapon || attackerWeapon.WeaponSize == WeaponSizeCategory.Light;

        bool targetTwoHanded = IsWeaponTwoHanded(targetItem);
        bool targetOneHandedOrLight = targetItem.IsShield || !targetTwoHanded;

        if (attackerTwoHanded && targetOneHandedOrLight)
            return 4;

        if (attackerLight && targetTwoHanded)
            return -4;

        return 0;
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

    public BullRushCheckResult RollBullRushAttackerCheck(int bab, int chargeBonus = 0, int? fixedRoll = null)
    {
        var result = new BullRushCheckResult
        {
            CharacterName = Stats != null ? Stats.CharacterName : name,
            BaseRoll = fixedRoll ?? DiceService.D20("Bull rush check"),
            BaseAttackBonus = bab,
            StrengthModifier = Stats != null ? Stats.STRMod : 0,
            SizeModifier = GetGrappleSizeModifier(),
            ChargeBonus = chargeBonus,
            UsesBestStrengthOrDexterity = false
        };

        if (Stats != null && Stats.HasFeat("Improved Bull Rush"))
            result.AddMiscModifier(4, "Improved Bull Rush feat");

        result.Total = result.BaseRoll
            + result.BaseAttackBonus
            + result.StrengthModifier
            + result.SizeModifier
            + result.ChargeBonus
            + result.MiscModifier;

        return result;
    }

    public BullRushCheckResult RollBullRushDefenderCheck(int? fixedRoll = null)
    {
        int strengthMod = Stats != null ? Stats.STRMod : 0;
        int dexterityMod = Stats != null ? Stats.DEXMod : 0;
        int bestAbility = Mathf.Max(strengthMod, dexterityMod);
        int stabilityBonus = (Stats != null && Stats.Race != null) ? Stats.Race.StabilityBonus : 0;

        var result = new BullRushCheckResult
        {
            CharacterName = Stats != null ? Stats.CharacterName : name,
            BaseRoll = fixedRoll ?? DiceService.D20("Bull rush defense"),
            BaseAttackBonus = Stats != null ? Stats.BaseAttackBonus : 0,
            StrengthOrDexterityModifier = bestAbility,
            SizeModifier = GetGrappleSizeModifier(),
            StabilityBonus = stabilityBonus,
            UsesBestStrengthOrDexterity = true
        };

        result.Total = result.BaseRoll
            + result.BaseAttackBonus
            + result.StrengthOrDexterityModifier
            + result.SizeModifier
            + result.StabilityBonus;

        return result;
    }

    private SpecialAttackResult ResolveBullRush(CharacterController target, int attackBab, int chargeBonus)
    {
        if (Stats != null && Stats.IsSwarm)
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Bull Rush",
                Success = false,
                Log = $"{Stats.CharacterName} cannot bull rush while in swarm form."
            };
        }

        if (target != null && target.Stats != null && target.Stats.IsSwarm)
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Bull Rush",
                Success = false,
                Log = $"{target.Stats.CharacterName} is a swarm and cannot be bull rushed."
            };
        }

        if (IsBlockedBySummonedContactBarrier(target, out _))
            return BuildSummonedContactBarrierResult(target, "Bull Rush");

        BullRushCheckResult attackerCheck = RollBullRushAttackerCheck(attackBab, chargeBonus);
        BullRushCheckResult defenderCheck = target.RollBullRushDefenderCheck();
        bool success = attackerCheck.Total > defenderCheck.Total;

        string resultLine = BuildOpposedResultLine(Stats.CharacterName, attackerCheck.Total, target.Stats.CharacterName, defenderCheck.Total);
        int margin = Mathf.Max(0, attackerCheck.Total - defenderCheck.Total);
        int maxPushSquares = success ? 1 + (margin / 5) : 0;
        int pushFeet = success ? maxPushSquares * 5 : 0;

        string header = chargeBonus > 0
            ? $"{Stats.CharacterName} charges and attempts to bull rush {target.Stats.CharacterName}"
            : $"{Stats.CharacterName} attempts to bull rush {target.Stats.CharacterName}";

        string outcome = success
            ? $"{Stats.CharacterName} successfully bull rushes {target.Stats.CharacterName}. Can push 1 to {maxPushSquares} square{(maxPushSquares == 1 ? string.Empty : "s")} (5 to {pushFeet} ft)."
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
            DamageDealt = pushFeet,
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

        // D&D 3.5 overrun blocking check: opposed STR checks with size modifiers.
        // Use grapple-size scale (+/-4 per size category step), not attack/AC size modifier.
        int atkSizeMod = GetGrappleSizeModifier();
        int defSizeMod = target.GetGrappleSizeModifier();
        int atkTotal = atkRoll + Stats.STRMod + atkSizeMod + Stats.ConditionAttackPenalty + (Stats.HasFeat("Improved Overrun") ? 4 : 0);
        int defTotal = defRoll + target.Stats.STRMod + defSizeMod + target.Stats.ConditionAttackPenalty;
        bool success = atkTotal >= defTotal;

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

        int bluffBonus = Stats.GetSkillBonus("Bluff");

        bool targetIsHumanoid = false;
        if (target.Stats != null && !string.IsNullOrEmpty(target.Stats.CreatureType))
            targetIsHumanoid = target.Stats.CreatureType.Trim().ToLowerInvariant().Contains("humanoid");

        bool useBabWisDefense = !targetIsHumanoid || (target.Stats != null && target.Stats.IsMindless);
        int opposedBonus = useBabWisDefense
            ? (target.Stats.BaseAttackBonus + target.Stats.WISMod)
            : target.Stats.GetSkillBonus("Sense Motive");

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

        int distance = GetMinimumDistanceToTarget(target, chebyshev: true);
        if (distance != 1)
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

        int damageModifier = Stats.GetWeaponDamageModifier(weapon, isOffHand: false);
        int baseDamageRoll = Stats.RollBaseDamage(damageDice, damageCount);
        int baseDamage = Mathf.Max(1, baseDamageRoll + damageModifier + bonusDamage);

        int sneakDamage = 0;
        bool sneakApplied = false;
        if (Stats.IsRogue)
        {
            int rogueLevel = Stats.GetClassLevel("Rogue");
            sneakDamage = CombatUtils.RollSneakAttackDamage(rogueLevel);
            sneakApplied = sneakDamage > 0;
        }

        int preCritDamage = baseDamage + sneakDamage;
        int critMultiplier = weapon != null && weapon.CritMultiplier > 0
            ? weapon.CritMultiplier
            : (Stats.CritMultiplier > 0 ? Stats.CritMultiplier : 2);
        int rawCriticalDamage = Mathf.Max(1, preCritDamage * critMultiplier);

        var damageTypes = weapon != null
            ? weapon.GetDamageTypes()
            : new HashSet<DamageType> { DamageType.Bludgeoning };

        DamageBypassTag attackTags = weapon != null ? weapon.GetBypassTags() : DamageBypassTag.Bludgeoning;

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
        int saveDC = 10 + Mathf.Max(0, finalDamage);
        bool fortSucceeded = false;
        bool diedFromDamage = hpAfterDamage <= -10;

        if (!diedFromDamage)
        {
            fortRoll = DiceService.D20("Coup de Grace Fort save");
            fortTotal = fortRoll + target.Stats.FortitudeSave;
            fortSucceeded = fortTotal >= saveDC;

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
            ? $" + Sneak {sneakDamage}"
            : string.Empty;
        string damageSource = weapon != null ? weapon.Name : attackLabel;

        string saveLine = diedFromDamage
            ? $"{target.Stats.CharacterName} is slain by damage before the Fortitude save can matter."
            : $"Fortitude save: d20({fortRoll}) + {target.Stats.FortitudeSave} = {fortTotal} vs DC {saveDC} {(fortSucceeded ? "SUCCESS" : "FAILURE")}.";

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
            Log = $"{Stats.CharacterName} performs Coup de Grace on helpless {target.Stats.CharacterName} with {damageSource}: "
                + $"({damageCount}d{damageDice}={baseDamageRoll} + mod {CharacterStats.FormatMod(damageModifier + bonusDamage)}{sneakSegment}) ×{critMultiplier} = {rawCriticalDamage}; "
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
        int atkHeldItemMod = GetDisarmHeldItemModifier(attackerHeldItem, treatUnarmedAsLight: true);
        int atkSizeDiffMod = GetDisarmSizeDifferenceModifier(attacker, defender);
        int atkImprovedDisarmMod = attacker.Stats.HasFeat("Improved Disarm") ? 4 : 0;

        int defHeldItemMod = GetDisarmHeldItemModifier(defenderHeldItem, treatUnarmedAsLight: false);
        int defNonMeleeHeldItemPenalty = GetDisarmNonMeleeHeldItemPenalty(defenderHeldItem);
        int defSizeDiffMod = GetDisarmSizeDifferenceModifier(defender, attacker);
        int defImprovedDisarmMod = defender.Stats.HasFeat("Improved Disarm") ? 4 : 0;

        int atkRoll = DiceService.D20("Disarm attack roll");
        int defRoll = DiceService.D20("Disarm defense roll");

        int attackerBaseAttackBonusUsed = attackerBaseAttackBonusOverride ?? attacker.Stats.BaseAttackBonus;
        int attackerBaseAttackBonusRaw = attacker.Stats.BaseAttackBonus;
        int attackerIterativeAdjustment = attackerBaseAttackBonusUsed - attackerBaseAttackBonusRaw - attackerDualWieldPenaltyForLog;

        int atkTotal = atkRoll + attackerBaseAttackBonusUsed + attacker.Stats.STRMod + attacker.Stats.SizeModifier + attacker.Stats.ConditionAttackPenalty
                       + atkHeldItemMod + atkSizeDiffMod + atkImprovedDisarmMod;
        int defTotal = defRoll + defender.Stats.BaseAttackBonus + defender.Stats.STRMod + defender.Stats.SizeModifier + defender.Stats.ConditionAttackPenalty
                       + defHeldItemMod + defNonMeleeHeldItemPenalty + defSizeDiffMod + defImprovedDisarmMod + defenderSpecialResistBonus;

        string attackerHeldLabel = attackerHeldItem != null ? attackerHeldItem.Name : "Unarmed Strike";
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
            specialModifier: 0,
            specialModifierLabel: string.Empty,
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
            specialModifier: defenderSpecialResistBonus,
            specialModifierLabel: lockedGauntletReason,
            heldItemLabel: defenderHeldLabel,
            total: defTotal);

        return new DisarmCheckResult
        {
            Success = atkTotal >= defTotal,
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
        if (specialModifier != 0)
        {
            string specialLabel = string.IsNullOrWhiteSpace(specialModifierLabel) ? "Special" : specialModifierLabel;
            sb.AppendLine($"  {specialLabel}: {FormatSignedDisarmModifier(specialModifier)}");
        }

        sb.AppendLine($"  Total: {BuildDisarmEquation(d20Roll, baseAttackBonus, iterativeAdjustment, dualWieldPenalty, strengthModifier, sizeModifier, conditionModifier, weaponModifier, nonMeleePenalty, sizeDifferenceModifier, improvedDisarmModifier, specialModifier)} = {total}");
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
            GameManager.Instance?.CombatUI?.ShowCombatLog($"💨 {ownerName} drops {rightHand.Name} ({reason}).");
        }

        ItemData leftHand = inventory.RemoveEquippedHeldItem(EquipSlot.LeftHand);
        if (leftHand != null)
        {
            DropItemToGround(this, leftHand);
            droppedCount++;
            GameManager.Instance?.CombatUI?.ShowCombatLog($"💨 {ownerName} drops {leftHand.Name} ({reason}).");
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

    private static int GetDisarmSizeDifferenceModifier(CharacterController actor, CharacterController opponent)
    {
        if (actor == null || opponent == null || actor.Stats == null || opponent.Stats == null)
            return 0;

        int sizeStepDifference = (int)actor.Stats.CurrentSizeCategory - (int)opponent.Stats.CurrentSizeCategory;
        return sizeStepDifference * 4;
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
    /// Perform a Flurry of Blows attack (Monk only).
    /// D&D 3.5: Two attacks at reduced BAB. At level 3: +0/+0.
    /// Must use unarmed strike or special monk weapon.
    /// </summary>
    public FullAttackResult FlurryOfBlows(CharacterController target, bool isFlanking, int flankingBonus, string flankingPartnerName, RangeInfo rangeInfo = null)
    {
        var result = new FullAttackResult();
        result.Type = FullAttackResult.AttackType.FullAttack;
        result.Attacker = this;
        result.Defender = target;
        result.DefenderHPBefore = target.Stats.CurrentHP;

        if (!Stats.IsMonk)
        {
            Debug.LogWarning($"[Monk] {Stats.CharacterName}: Cannot use Flurry of Blows - not a Monk!");
            return result;
        }

        // Get flurry attack bonuses
        int[] flurryBonuses = Stats.GetFlurryOfBlowsBonuses();
        Debug.Log($"[Monk] {Stats.CharacterName}: Flurry of Blows! {flurryBonuses.Length} attacks at " +
                  $"{string.Join("/", System.Array.ConvertAll(flurryBonuses, b => CharacterStats.FormatMod(b)))}");

        // Use monk/unarmed fallback profile unless a monk weapon is equipped.
        var unarmedProfile = GetUnarmedDamage();
        int damageDice = unarmedProfile.damageDice;
        int damageCount = unarmedProfile.damageCount;
        int bonusDamage = unarmedProfile.bonusDamage;

        // Check for equipped weapon (quarterstaff is a monk weapon)
        ItemData equippedWeapon = EnsureInventory().GetRightHandEquippedWeapon();
        int critThreatMin = 20;
        int critMult = 2;

        if (equippedWeapon != null)
        {
            // Use weapon stats if equipped
            GetScaledWeaponDamageDice(equippedWeapon, out damageCount, out damageDice);
            bonusDamage = equippedWeapon.BonusDamage;
            critThreatMin = equippedWeapon.CritThreatMin;
            critMult = equippedWeapon.CritMultiplier;
            Debug.Log($"[Monk] Using weapon: {equippedWeapon.Name} ({damageCount}d{damageDice})");
        }
        else
        {
            Debug.Log($"[Monk] Using unarmed strike: {damageCount}d{damageDice}");
        }

        int racialAtkBonus = Stats.GetRacialAttackBonus(target.Stats);
        int proneAttackPenalty = GetProneAttackModifier(isMeleeAttack: true);
        int weaponNonProfPenalty = Stats.GetWeaponNonProficiencyPenalty(equippedWeapon);
        int conditionAttackPenalty = Stats.ConditionAttackPenalty;
        int armorNonProfPenalty = Stats.GetArmorNonProficiencyAttackPenalty();
        int solidFogAtkPenalty = Stats.SolidFogMeleeAttackPenalty; // flurry is always melee
        int solidFogDmgPenalty = Stats.SolidFogMeleeDamagePenalty;
        DamageModeAttackProfile damageModeProfile = ResolveDamageModeAttackProfile(equippedWeapon);

        for (int i = 0; i < flurryBonuses.Length; i++)
        {
            if (target.Stats.IsDead)
            {
                Debug.Log($"[Monk] Target is dead, stopping at attack {i + 1}");
                break;
            }

            int aidAnotherAttackBonus = ConsumeAidAnotherAttackBonus(target);
            int aidAnotherTargetAcBonus = ConsumeAidAnotherAcBonus(target);
            int atkMod = flurryBonuses[i] + (isFlanking ? flankingBonus : 0) + racialAtkBonus + proneAttackPenalty
                       + weaponNonProfPenalty + armorNonProfPenalty + conditionAttackPenalty + aidAnotherAttackBonus
                       + damageModeProfile.AttackPenalty + solidFogAtkPenalty;

            string label = $"Flurry {i + 1} ({CharacterStats.FormatMod(flurryBonuses[i])})";
            int hpBefore = target.Stats.CurrentHP;

            CombatResult atk = PerformSingleAttackWithCrit(target, atkMod, isFlanking, flankingBonus, flankingPartnerName,
                damageDice, damageCount, bonusDamage, critThreatMin, critMult,
                equippedWeapon, false, solidFogDmgPenalty, aidAnotherTargetAcBonus,
                damageModeProfile.DealNonlethalDamage, damageModeProfile.AttackPenalty, damageModeProfile.PenaltySource);

            atk.RacialAttackBonus = racialAtkBonus;
            atk.SizeAttackBonus = Stats.SizeModifier;
            atk.AidAnotherAttackBonus = aidAnotherAttackBonus;
            atk.AidAnotherTargetAcBonus = aidAnotherTargetAcBonus;
            if (equippedWeapon != null)
            {
                atk.WeaponName = equippedWeapon.Name;
                atk.BaseDamageDiceStr = $"{damageCount}d{damageDice}";
            }
            else
            {
                atk.WeaponName = "Unarmed Strike";
                atk.BaseDamageDiceStr = $"1d{damageDice}";
            }
            atk.WeaponNonProficiencyPenalty = weaponNonProfPenalty;
            atk.ArmorNonProficiencyPenalty = armorNonProfPenalty;
            atk.DefenderHPBefore = hpBefore;
            atk.DefenderHPAfter = target.Stats.CurrentHP;

            result.Attacks.Add(atk);
            result.AttackLabels.Add(label);
        }

        result.DefenderHPAfter = target.Stats.CurrentHP;
        result.TargetKilled = target.Stats.IsDead;
        HasAttackedThisTurn = true;

        Debug.Log($"[Monk] {Stats.CharacterName}: Flurry of Blows complete - " +
                  $"{result.Attacks.Count} attacks, target HP: {result.DefenderHPAfter}");
        return result;
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
        var spellComp = GetComponent<SpellcastingComponent>();
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
    /// DC = 15 + spell level (PHB).
    /// </summary>
    /// <param name="spellLevel">Level of the spell being cast.</param>
    /// <param name="roll">The d20 roll result.</param>
    /// <param name="total">Total check result (d20 + Spellcraft bonus).</param>
    /// <param name="dc">The DC to beat (15 + spell level).</param>
    /// <returns>True if the spell was successfully identified.</returns>
    public bool RollSpellcraftIdentification(int spellLevel, out int roll, out int total, out int dc)
    {
        dc = 15 + spellLevel;
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
    /// </summary>
    public bool HasSpellAvailableForCounter(string spellId)
    {
        if (string.IsNullOrEmpty(spellId)) return false;
        var spellComp = GetComponent<SpellcastingComponent>();
        if (spellComp == null) return false;

        SpellData spell = SpellDatabase.GetSpell(spellId);
        if (spell == null) return false;

        return spellComp.CanCast(spell);
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
        var spellComp = GetComponent<SpellcastingComponent>();
        if (spellComp == null) return false;

        SpellData spell = SpellDatabase.GetSpell(spellId);
        if (spell == null) return false;

        bool consumed = spellComp.CastSpellFromSlot(spell);
        if (consumed)
            Debug.Log($"[Counterspell] {Stats.CharacterName}: Consumed spell slot for {spell.Name}.");
        else
            Debug.LogWarning($"[Counterspell] {Stats.CharacterName}: Failed to consume spell slot for {spell.Name}!");
        return consumed;
    }
}