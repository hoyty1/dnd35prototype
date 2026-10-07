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
    /// <summary>An iterative weapon or unarmed swing, or a maneuver that replaces one (trip, disarm, sunder, grapple).</summary>
    MainHand,
    /// <summary>The next natural attack of an innate natural-attack sequence.</summary>
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
/// step's iterative BAB. Cleared by <see cref="CharacterController.StartNewTurn"/>.
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
