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
    /// <summary>An iterative weapon or unarmed swing, a maneuver that replaces one (trip, disarm, sunder, grapple), or a grapple action once grappling.</summary>
    MainHand,
    /// <summary>The next natural attack of an innate natural-attack sequence (with Haste, one extra natural attack, CMB-106), or a maneuver that replaces one (at that natural attack's BAB).</summary>
    NaturalSequence,
    /// <summary>An off-hand attack: counts toward commitment but does not move the main-hand cursor.</summary>
    OffHand
}

/// <summary>
/// Turn-scoped, per-creature attack-sequence state (house-rule progressive attack flow).
/// The first attack, or a maneuver that replaces one, spends only the standard action, so the
/// creature may still take its move action if it stops there. A second attack turns the turn into
/// a full attack and spends the move action; after that only a 5-foot step is allowed, and a
/// creature that has already used its move action cannot take a second attack.
/// This applies PHB p.143 "Deciding between an Attack or a Full Attack". Trip, disarm, sunder and
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

    public bool IsFullAttack => Mode == ProgressiveAttackMode.FullAttackCommitted;
    public bool HasStartedAttacking => Mode != ProgressiveAttackMode.None;
    public bool NextAttackNeedsMoveAction => Mode == ProgressiveAttackMode.StandardAttackCommitted && !PendingStepPaid;

    public void Clear()
    {
        Mode = ProgressiveAttackMode.None;
        AttacksCommitted = 0;
        MainHandStepsUsed = 0;
        MainHandBudget = 0;
        PendingStepPaid = false;
        HasteExtraNaturalAttackUsed = false;
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

        return -1;
    }
}

/// <summary>
/// What a combat maneuver costs in the attack sequence. One switch case per maneuver type, so
/// including or excluding a type is a one-line change on the NPC side (CMB-102). Read by the AI
/// (GameManager.TryNPCSpecialAttackIfBeneficial, ShouldUseManeuver). The PC branches and availability
/// helpers for trip, disarm, sunder and grapple still hard-code them as attack steps: change them with
/// the table.
/// </summary>
public static class ManeuverActionCost
{
    /// <summary>
    /// Maneuvers that may replace one melee attack of an attack or full attack, at that attack's
    /// BAB (PHB p.141 Table 8-2 note 7). They cost one step of the creature's sequence through
    /// <see cref="CharacterController.TryCommitManeuverSubstituteStep"/>: an iterative step, or for a
    /// creature fighting with natural attacks one natural attack at that attack's BAB (MM p.312). Bull rush and overrun are not in this set:
    /// for every creature, PC or NPC, they are a standard action or part of a charge (PHB p.154,
    /// p.157), never one attack of a full attack and never an attack of opportunity.
    /// </summary>
    public static bool ReplacesMeleeAttack(SpecialAttackType type)
    {
        switch (type)
        {
            case SpecialAttackType.Trip:    // PHB p.158
            case SpecialAttackType.Disarm:  // PHB p.155
            case SpecialAttackType.Grapple: // PHB p.156
            case SpecialAttackType.Sunder:  // PHB p.158 (owner decision 2026-10-07)
                return true;
            default: // BullRushAttack p.154 and Overrun p.157: standard action or part of a charge; Feint p.155: standard (move with Improved Feint, p.95); CoupDeGrace: full round
                return false;
        }
    }
}
