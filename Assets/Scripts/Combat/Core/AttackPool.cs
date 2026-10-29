using System;
using UnityEngine;

public enum ProgressiveAttackMode
{
    None,
    StandardAttackCommitted,
    FullAttackCommitted
}

/// <summary>
/// What one attack step of a turn is, for the shared attack-sequence executor
/// (<see cref="CharacterController.TryCommitAttack"/>).
/// </summary>
public enum AttackStepKind
{
    /// <summary>An iterative weapon or unarmed swing, a maneuver that replaces one (trip, disarm, sunder, grapple), or a grapple action once grappling (every one except a natural-weapon creature's grapple natural attack).</summary>
    MainHand,
    /// <summary>The next natural attack of an innate natural-attack sequence (with Haste, one extra natural attack, CMB-106), or a maneuver or grapple natural attack that replaces one (at that natural attack's BAB; CMB-102, CMB-127).</summary>
    NaturalSequence,
    /// <summary>
    /// An off-hand attack (two-weapon fighting, PHB p.160): counts toward commitment but does not move the main-hand cursor;
    /// it moves the off-hand one (AttackPool.OffHandStepsUsed). A step of the shared executor since batch 12 step 18.
    /// </summary>
    OffHand
}

/// <summary>
/// Turn-scoped, per-creature attack-sequence state: the full attack decided attack by attack, as PHB p.143
/// "Deciding between an Attack or a Full Attack" allows (a rule of the book, not a house rule).
/// The first attack, or a maneuver that replaces one, spends only the standard action, so the
/// creature may still take its move action if it stops there (after a 5-foot step that move action
/// cannot move it any distance, PHB p.143-144). A second attack turns the turn into a full attack
/// (a full-round action, PHB p.143) and spends the move action; after that only a 5-foot step is
/// allowed, and a creature that has already used its move action cannot take a second attack. Trip, disarm, sunder and
/// grapple replace a melee attack (PHB p.141 Table 8-2 note 7) and use the same steps, at that
/// step's bonus (an iterative BAB, or for a natural-attack creature the BAB of the natural attack
/// replaced, MM p.312). Cleared by <see cref="CharacterController.StartNewTurn"/>.
/// </summary>
[Serializable]
public sealed class AttackPool
{
    public ProgressiveAttackMode Mode { get; private set; } = ProgressiveAttackMode.None;

    /// <summary>Attacks resolved (or committed) this turn, of any <see cref="AttackStepKind"/>.</summary>
    public int AttacksCommitted { get; private set; }

    /// <summary>Main-hand or natural-sequence steps used this turn; the index of the next step.</summary>
    public int MainHandStepsUsed { get; private set; }

    /// <summary>
    /// Off-hand steps made (or given up for an off-hand maneuver or a ward) this turn: the index of the next off-hand
    /// attack (CharacterCombatStats.GetOffHandAttackBAB) and its limit (CharacterController.GetOffHandAttackCount; PHB p.160:
    /// one off-hand attack, a second with Improved Two-Weapon Fighting, a third with Greater). Batch 12 step 18.
    /// </summary>
    public int OffHandStepsUsed { get; private set; }

    /// <summary>Main-hand steps available this turn; 0 until the first main-hand or natural step fixes it.</summary>
    public int MainHandBudget { get; private set; }

    /// <summary>The action for the next attack is spent but that attack is not resolved yet.</summary>
    public bool PendingStepPaid { get; private set; }

    /// <summary>
    /// Haste's extra attack with a natural weapon (PHB p.239; owner decision 2026-10-07, CMB-106) was
    /// made, or given up for a maneuver, this turn. Steps are counted in <see cref="MainHandStepsUsed"/>;
    /// this flag says which natural step was the Haste one, for flows that pick natural attacks out of
    /// order (the PC natural-attack buttons).
    /// </summary>
    public bool HasteExtraNaturalAttackUsed { get; private set; }

    /// <summary>
    /// Natural-sequence index of the natural attack the last maneuver substitute gave up this turn, or -1 (none
    /// yet, or the last substitute replaced an iterative step). Set by
    /// <see cref="CharacterController.TryCommitManeuverSubstituteStep"/>; read by the Improved Trip follow-up
    /// attack (PHB p.96, CMB-079), which is that natural attack made as if the trip had not used it.
    /// </summary>
    public int LastSubstituteNaturalAttackIndex { get; private set; } = -1;

    /// <summary>
    /// True when the last maneuver or grapple substitute gave up Haste's extra natural attack (made with the natural
    /// attack at <see cref="LastSubstituteNaturalAttackIndex"/>, CMB-106) rather than that natural attack itself. Read by
    /// a grapple natural attack whose caller committed the step without naming the attack (CMB-127).
    /// </summary>
    public bool LastSubstituteWasHasteExtraAttack { get; private set; }

    // Natural attacks made or given up this turn, by natural-sequence index (not serialized; turn-scoped).
    private readonly System.Collections.Generic.HashSet<int> _usedNaturalAttackIndices = new System.Collections.Generic.HashSet<int>();

    /// <summary>
    /// True when the natural attack at this natural-sequence index was made, or given up for a maneuver, this turn
    /// (Haste's extra attack with it is tracked by <see cref="HasteExtraNaturalAttackUsed"/> instead). Recorded for PCs
    /// and NPCs alike by <see cref="CharacterController.FullAttack"/> (every natural attack path resolves through it) and
    /// <see cref="CharacterController.TryCommitManeuverSubstituteStep"/>. Read where natural attacks are chosen out of
    /// sequence order: the grapple attack action (PHB p.156, CMB-127) and a maneuver whose step's own natural attack
    /// is already used (<see cref="CharacterController.ResolveSubstituteNaturalAttackIndex"/>).
    /// </summary>
    public bool IsNaturalAttackUsed(int naturalAttackIndex) => _usedNaturalAttackIndices.Contains(naturalAttackIndex);

    /// <summary>
    /// A rake (MM p.314) was made this turn: its extra claw attacks come once a turn (CMB-127, CMB-144). Set by
    /// <see cref="CharacterController.PerformRakeAttacks"/>.
    /// </summary>
    public bool RakeUsedThisTurn { get; private set; }

    /// <summary>
    /// The creature was grappling when this turn began: a rake needs it (MM p.314: it cannot begin a grapple and rake
    /// in the same turn; CMB-144). Set by <see cref="CharacterController.StartNewTurn"/>.
    /// </summary>
    public bool BeganTurnGrappling { get; private set; }

    /// <summary>
    /// The monk declared a flurry of blows this turn (PHB p.40; CHR-003): a full attack from the first attack, whose
    /// main-hand steps follow CharacterStats.GetFlurryOfBlowsBaseAttackBonuses and whose attacks, with every attack of
    /// opportunity the monk makes before her next turn, take CharacterStats.FlurryOfBlowsAttackPenalty
    /// (CharacterController.BuildAttackBonus). It lasts until <see cref="Clear"/> at the start of her next turn.
    /// Set by <see cref="CharacterController.TryBeginFlurryOfBlows"/>.
    /// </summary>
    public bool IsFlurryOfBlows { get; private set; }

    /// <summary>
    /// Rapid Shot was declared this turn (PHB p.99; batch 12 decision D9): before the first attack, committing the turn to
    /// the full attack. The sequence gets one extra ranged attack at the highest bonus (CharacterCombatStats.GetIterativeAttackCount
    /// and GetIterativeAttackBAB put it first) and every attack of the round takes -2 (CharacterController.BuildAttackBonus
    /// through AttackCalculator's Rapid Shot term, ranged attacks only: the attacks of that full attack, never a melee AoO
    /// before the next turn, delegated RAW reading 2026-10-28, RULES_COVERAGE 4.1). It lasts until <see cref="Clear"/> at the start of the
    /// creature's next turn, so a reused slot never carries it. Set by <see cref="CharacterController.TryDeclareRapidShot"/>.
    /// </summary>
    public bool IsRapidShot { get; private set; }

    public bool IsFullAttack => Mode == ProgressiveAttackMode.FullAttackCommitted;
    public bool HasStartedAttacking => Mode != ProgressiveAttackMode.None;
    public bool NextAttackNeedsMoveAction => Mode == ProgressiveAttackMode.StandardAttackCommitted && !PendingStepPaid;

    public void Clear()
    {
        Mode = ProgressiveAttackMode.None;
        AttacksCommitted = 0;
        MainHandStepsUsed = 0;
        OffHandStepsUsed = 0;
        MainHandBudget = 0;
        PendingStepPaid = false;
        HasteExtraNaturalAttackUsed = false;
        LastSubstituteNaturalAttackIndex = -1;
        LastSubstituteWasHasteExtraAttack = false;
        _usedNaturalAttackIndices.Clear();
        RakeUsedThisTurn = false;
        BeganTurnGrappling = false;
        IsFlurryOfBlows = false;
        IsRapidShot = false;
    }

    /// <summary>Declares Rapid Shot: the turn is a full attack (its actions are spent by the caller) with one extra ranged attack.</summary>
    internal void MarkRapidShot()
    {
        IsRapidShot = true;
        Mode = ProgressiveAttackMode.FullAttackCommitted;
    }

    /// <summary>Starts a flurry of blows: the turn is a full attack (its actions are spent by the caller) with the flurry steps.</summary>
    internal void MarkFlurryOfBlows()
    {
        IsFlurryOfBlows = true;
        Mode = ProgressiveAttackMode.FullAttackCommitted;
    }

    internal void MarkTurnStartedGrappling(bool grappling)
    {
        BeganTurnGrappling = grappling;
    }

    /// <summary>
    /// Makes this turn a full attack without recording an attack step: a rake in a grapple (MM p.314, CMB-144), whose
    /// caller has spent the standard and move actions. Later steps pay nothing more (PHB p.143).
    /// </summary>
    internal void MarkFullAttackCommitted()
    {
        Mode = ProgressiveAttackMode.FullAttackCommitted;
    }

    internal void MarkNaturalAttackUsed(int naturalAttackIndex)
    {
        if (naturalAttackIndex >= 0)
            _usedNaturalAttackIndices.Add(naturalAttackIndex);
    }

    internal void MarkRakeUsed()
    {
        RakeUsedThisTurn = true;
    }

    internal void RecordSubstituteNaturalAttack(int naturalAttackIndex, bool wasHasteExtraAttack = false)
    {
        LastSubstituteNaturalAttackIndex = naturalAttackIndex;
        LastSubstituteWasHasteExtraAttack = naturalAttackIndex >= 0 && wasHasteExtraAttack;
    }

    internal void MarkHasteExtraNaturalAttackUsed()
    {
        HasteExtraNaturalAttackUsed = true;
    }

    /// <summary>Fix this turn's main-hand budget; ignored once it is set. Minimum 1.</summary>
    public void EnsureMainHandBudget(int budget)
    {
        if (MainHandBudget > 0)
            return;

        MainHandBudget = Mathf.Max(1, budget);
    }

    internal void MarkPaid(ProgressiveAttackMode mode)
    {
        Mode = mode;
        PendingStepPaid = true;
    }

    /// <summary>Record one attack. Returns the main-hand step index, or -1 for an off-hand attack.</summary>
    internal int RegisterAttack(AttackStepKind kind)
    {
        AttacksCommitted++;
        PendingStepPaid = false;

        // An attack recorded without a paid step still starts the sequence.
        if (Mode == ProgressiveAttackMode.None)
            Mode = ProgressiveAttackMode.StandardAttackCommitted;

        if (kind == AttackStepKind.MainHand || kind == AttackStepKind.NaturalSequence)
            return MainHandStepsUsed++;

        OffHandStepsUsed++;
        return -1;
    }
}

/// <summary>
/// What one maneuver costs (batch 12 step 19, CMB-102): the action it takes and whether it takes the place of one melee
/// attack of an attack or full attack instead (PHB p.141 Table 8-2 note 7). <see cref="MoveOrChargeOnly"/> marks a
/// maneuver made only during a move or a charge (overrun, PHB p.157), <see cref="ChargeEnd"/> one that ends a charge (the
/// charge bull rush, PHB p.154).
/// </summary>
public readonly struct ManeuverCost
{
    public readonly ActionCostType Action;
    public readonly bool ReplacesMeleeAttack;
    public readonly bool MoveOrChargeOnly;
    public readonly bool ChargeEnd;

    public ManeuverCost(ActionCostType action, bool replacesMeleeAttack = false, bool moveOrChargeOnly = false, bool chargeEnd = false)
    {
        Action = action;
        ReplacesMeleeAttack = replacesMeleeAttack;
        MoveOrChargeOnly = moveOrChargeOnly;
        ChargeEnd = chargeEnd;
    }

    public override string ToString()
        => ReplacesMeleeAttack ? "one melee attack" : Action + (MoveOrChargeOnly ? " (in a move or charge)" : "") + (ChargeEnd ? " (end of a charge)" : "");
}

/// <summary>
/// The one cost table of the combat maneuvers (batch 12 step 19, CMB-102): one row per <see cref="SpecialAttackType"/>,
/// read by both sides. The PC wrapper (GameManager.ExecuteSpecialAttack, the Special Attack menu's availability
/// GameManager.CanAffordManeuverForUi and CombatUI's buttons) and the NPC executor (GameManager.TryNPCSpecialAttackIfBeneficial,
/// AIService.ShouldUseManeuver) build a ManeuverRequest whose cost comes from here, and the maneuver core
/// (GameManager.ExecuteManeuver) pays it before the initiation AoOs.
/// </summary>
public static class ManeuverActionCost
{
    /// <summary>
    /// The cost of <paramref name="type"/> for <paramref name="actor"/> (null: a creature without feats):
    /// - trip, disarm, grapple and sunder take the place of one melee attack (PHB p.141 Table 8-2 note 7, p.155, p.156,
    ///   p.158; sunder by owner decision 2026-10-07): one step of the creature's attack sequence through
    ///   <see cref="CharacterController.TryCommitManeuverSubstituteStep"/>, an iterative step or, for a creature fighting
    ///   with natural attacks, one natural attack at that attack's BAB (MM p.312);
    /// - feint is a standard action (PHB p.155), a move action with Improved Feint (PHB p.95);
    /// - coup de grace is a full-round action (PHB p.153);
    /// - a standing bull rush is a standard action, the charge bull rush the end of a charge (PHB p.154), never one attack
    ///   of a full attack;
    /// - overrun is a standard action made only during a move or a charge (PHB p.157; GameManager.RunOverrunMove, never this table's request);
    /// - aid another (PHB p.154), turning undead (PHB p.159) and waking a sleeping ally (shaking it awake, PHB p.280) are
    ///   standard actions.
    /// </summary>
    public static ManeuverCost For(SpecialAttackType type, CharacterController actor)
    {
        switch (type)
        {
            case SpecialAttackType.Trip:    // PHB p.158
            case SpecialAttackType.Disarm:  // PHB p.155
            case SpecialAttackType.Grapple: // PHB p.156
            case SpecialAttackType.Sunder:  // PHB p.158 (owner decision 2026-10-07)
                return new ManeuverCost(ActionCostType.Standard, replacesMeleeAttack: true);
            case SpecialAttackType.Feint:   // PHB p.155; Improved Feint PHB p.95
                return actor != null && actor.Stats != null && actor.Stats.HasFeat("Improved Feint")
                    ? new ManeuverCost(ActionCostType.Move)
                    : new ManeuverCost(ActionCostType.Standard);
            case SpecialAttackType.CoupDeGrace: // PHB p.153
                return new ManeuverCost(ActionCostType.FullRound);
            case SpecialAttackType.BullRushCharge: // PHB p.154: the end of a charge
                return new ManeuverCost(ActionCostType.FullRound, chargeEnd: true);
            case SpecialAttackType.Overrun: // PHB p.157: during a move or a charge
                return new ManeuverCost(ActionCostType.Standard, moveOrChargeOnly: true);
            default: // BullRushAttack p.154, AidAnother p.154, TurnUndead p.159, WakeSleepingAlly p.280
                return new ManeuverCost(ActionCostType.Standard);
        }
    }

    /// <summary>True when <paramref name="type"/> takes the place of one melee attack (<see cref="For"/>).</summary>
    public static bool ReplacesMeleeAttack(SpecialAttackType type) => For(type, null).ReplacesMeleeAttack;
}
