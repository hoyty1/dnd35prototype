using System;
using System.Collections.Generic;

/// <summary>
/// Which attack path resolved an attack (batch 12 step 13; the letters of the attack map in
/// docs/designs/pc_npc_parity_batch12.md). Each path still runs its own pre-attack steps and continuation; the
/// post-attack work of all of them is GameManager.OnAttackResolved, with the path's differences named by
/// <see cref="PostAttackQuirkSets.For"/>.
/// </summary>
public enum AttackOrigin
{
    /// <summary>A: the PC's single attack (CombatFlowService.PerformSingleAttack).</summary>
    PcSingle,
    /// <summary>B: a step of the PC's iterative attack sequence (CombatFlowService.PerformIterativeSequenceAttack).</summary>
    PcSequence,
    /// <summary>C: a step of the PC's Full Attack button (GameManager.PerformFullAttackWithRetargetingAndFiveFootStep).</summary>
    PcFullAttackButton,
    /// <summary>D: the PC's legacy one-target two-weapon full attack (CombatFlowService.PerformDualWieldAttack).</summary>
    PcDualWield,
    /// <summary>E: the PC's off-hand attack (GameManager.HandleOffHandTargetClick).</summary>
    PcOffHand,
    /// <summary>F: a step of the NPC melee sequence (GameManager.PerformNPCMeleeAttackSequence).</summary>
    NpcMelee,
    /// <summary>G: the NPC ranged full attack (GameManager.NPCPerformAttack).</summary>
    NpcRangedFull,
    /// <summary>H: the NPC ranged single attack (GameManager.NPCPerformAttack).</summary>
    NpcRangedSingle,
    /// <summary>I: an attack of opportunity (ThreatSystem.ExecuteAoO).</summary>
    AoO,
    /// <summary>J: the attack or pounce at the end of a charge, PC and NPC (GameManager.ExecuteCharge, NPCExecuteCharge).</summary>
    Charge
}

/// <summary>
/// Today's differences between the attack paths' post-attack work, as named bits (batch 12 step 13, the batch 11
/// LegacyCastQuirk pattern): a pure move kept every path's behaviour, and each difference from the hook's order
/// (OnAttackResolved: thrown drop and ammunition, Concentration per hit, melee reactions, free trip, Improved Grab, Cleave,
/// summon cleanup, combat end) is one bit set by <see cref="PostAttackQuirkSets"/>. Step 14 removes the grab, ammunition,
/// thrown-drop, Concentration, reaction and order bits; step 15 the Cleave bits.
/// </summary>
[Flags]
public enum PostAttackQuirk
{
    None = 0,

    // ── Thrown weapons and ammunition ──
    /// <summary>No thrown-weapon drop after the attack.</summary>
    NoThrownDrop = 1 << 0,
    /// <summary>The caller drops the thrown weapon itself, after its combat-end check (the off-hand throw, E).</summary>
    ThrownDropByCaller = 1 << 1,
    /// <summary>No ammunition spent after the attack.</summary>
    NoAmmoSpend = 1 << 2,
    /// <summary>The thrown drop and the ammunition come before the caller's attack log (A, B).</summary>
    ThrownAndAmmoBeforeLog = 1 << 3,

    // ── Concentration ──
    /// <summary>No Concentration check for damage here (the AoO; a caller that needs one makes it).</summary>
    NoConcentration = 1 << 4,
    /// <summary>One Concentration check on the full attack's summed damage, not one per hit (D, G).</summary>
    ConcentrationSummed = 1 << 5,
    /// <summary>Each hit's Concentration check runs inside the Improved Grab loop, after the free trip (the pounce).</summary>
    ConcentrationWithGrab = 1 << 6,

    // ── Melee reactions ──
    /// <summary>No melee reactions (Fire Shield and the like) to the attack (E).</summary>
    NoReactions = 1 << 7,
    /// <summary>A hit triggers reactions unless the whole sequence is ranged, not by the hit's own ranged flag (C).</summary>
    ReactionsBySequenceMode = 1 << 8,
    /// <summary>Every hit triggers reactions; the ranged flag is not read (the AoO, I).</summary>
    ReactionsOnAnyHit = 1 << 9,

    // ── Free trip ──
    /// <summary>No Trip (Ex) free trip after a hit (D, E).</summary>
    NoFreeTrip = 1 << 10,
    /// <summary>The free trip comes first, before the caller's attack log, Concentration and reactions (A, B, H, I, the charge's single attack).</summary>
    FreeTripBeforeLog = 1 << 11,
    /// <summary>The free trip's aftermath (the Improved Trip attack) waits until after the reactions (the AoO, I).</summary>
    TripAftermathAfterReactions = 1 << 12,

    // ── Improved Grab ──
    /// <summary>No Improved Grab after a trigger hit (B, C, D, E, I).</summary>
    NoImprovedGrab = 1 << 13,
    /// <summary>Improved Grab comes last: after the combat-end check, Cleave and the caller's end of the sequence (A).</summary>
    GrabAfterCombatEnd = 1 << 14,
    /// <summary>The charge's grab: one attempt, on the first eligible hit, logged without the attack's name (J).</summary>
    ChargeGrab = 1 << 15,

    // ── Cleave ──
    /// <summary>No Cleave or Great Cleave after the attack drops a foe (every origin but A).</summary>
    NoCleave = 1 << 16,
    /// <summary>Cleave comes after the combat-end check (A).</summary>
    CleaveAfterCombatEnd = 1 << 17,

    // ── Summon cleanup and combat end ──
    /// <summary>No summon cleanup for a slain target here (I, J).</summary>
    NoSummonCleanup = 1 << 18,
    /// <summary>The summon cleanup runs when the target is dead after the step, not only when this attack killed it (F).</summary>
    SummonCleanupWhenDead = 1 << 19,
    /// <summary>
    /// The caller checks combat end itself (C, E, F, G, H, I, J): its own frame names the end in the scenario traces
    /// (the phase event's "by" frames), so the check cannot move into the hook in a pure move.
    /// </summary>
    CombatEndByCaller = 1 << 20,

    // ── Pre-attack steps the path skips (read by no stage; named for step 14) ──
    /// <summary>No AoO for a ranged attack made while threatened (C, D).</summary>
    NoRangedAttackAoO = 1 << 21,
    /// <summary>No turned-undead fear break before the attack (the AoO, I).</summary>
    NoFearBreak = 1 << 22,
}

/// <summary>The <see cref="PostAttackQuirk"/> bits each origin sets: today's behaviour of that path, verified in code (2026-10-28).</summary>
public static class PostAttackQuirkSets
{
    private const PostAttackQuirk NoThrownOrAmmo = PostAttackQuirk.NoThrownDrop | PostAttackQuirk.NoAmmoSpend;

    /// <summary>A: trip, thrown drop and ammunition before the log; Cleave and the grab after the combat-end check.</summary>
    public const PostAttackQuirk PcSingle = PostAttackQuirk.FreeTripBeforeLog | PostAttackQuirk.ThrownAndAmmoBeforeLog
        | PostAttackQuirk.CleaveAfterCombatEnd | PostAttackQuirk.GrabAfterCombatEnd;

    /// <summary>B: no grab; trip, thrown drop and ammunition before the log.</summary>
    public const PostAttackQuirk PcSequence = PostAttackQuirk.NoImprovedGrab | PostAttackQuirk.FreeTripBeforeLog
        | PostAttackQuirk.ThrownAndAmmoBeforeLog | PostAttackQuirk.NoCleave;

    /// <summary>C: no grab, ranged AoO, ammunition or thrown drop; reactions by the sequence's mode; the caller ends the combat.</summary>
    public const PostAttackQuirk PcFullAttackButton = PostAttackQuirk.NoImprovedGrab | PostAttackQuirk.NoRangedAttackAoO | NoThrownOrAmmo
        | PostAttackQuirk.ReactionsBySequenceMode | PostAttackQuirk.CombatEndByCaller | PostAttackQuirk.NoCleave;

    /// <summary>D: no trip, thrown drop, ammunition, grab or ranged AoO; Concentration on the summed damage.</summary>
    public const PostAttackQuirk PcDualWield = PostAttackQuirk.NoFreeTrip | NoThrownOrAmmo | PostAttackQuirk.NoImprovedGrab
        | PostAttackQuirk.NoRangedAttackAoO | PostAttackQuirk.ConcentrationSummed | PostAttackQuirk.NoCleave;

    /// <summary>E: no trip, reactions, grab or ammunition; the caller drops the thrown weapon after it checks combat end.</summary>
    public const PostAttackQuirk PcOffHand = PostAttackQuirk.NoFreeTrip | PostAttackQuirk.NoReactions | PostAttackQuirk.NoImprovedGrab
        | PostAttackQuirk.NoAmmoSpend | PostAttackQuirk.ThrownDropByCaller | PostAttackQuirk.CombatEndByCaller | PostAttackQuirk.NoCleave;

    /// <summary>F: no thrown drop or ammunition; the summon cleanup when the target is dead; the caller ends the combat.</summary>
    public const PostAttackQuirk NpcMelee = NoThrownOrAmmo | PostAttackQuirk.SummonCleanupWhenDead | PostAttackQuirk.CombatEndByCaller
        | PostAttackQuirk.NoCleave;

    /// <summary>G: no ammunition or thrown drop; Concentration summed; the grab after all attacks (one batch of rolls); the caller ends the combat.</summary>
    public const PostAttackQuirk NpcRangedFull = NoThrownOrAmmo | PostAttackQuirk.ConcentrationSummed | PostAttackQuirk.CombatEndByCaller
        | PostAttackQuirk.NoCleave;

    /// <summary>H: no ammunition or thrown drop; the trip before the log; the caller ends the combat.</summary>
    public const PostAttackQuirk NpcRangedSingle = NoThrownOrAmmo | PostAttackQuirk.FreeTripBeforeLog | PostAttackQuirk.CombatEndByCaller
        | PostAttackQuirk.NoCleave;

    /// <summary>
    /// I: no grab, Cleave or fear break, no thrown drop or ammunition; the trip first, its aftermath after the reactions;
    /// every hit reacts; Concentration, summon cleanup and combat end are left to the callers.
    /// </summary>
    public const PostAttackQuirk AoO = PostAttackQuirk.NoImprovedGrab | PostAttackQuirk.NoCleave | PostAttackQuirk.NoFearBreak | NoThrownOrAmmo
        | PostAttackQuirk.FreeTripBeforeLog | PostAttackQuirk.TripAftermathAfterReactions | PostAttackQuirk.ReactionsOnAnyHit
        | PostAttackQuirk.NoConcentration | PostAttackQuirk.NoSummonCleanup | PostAttackQuirk.CombatEndByCaller;

    /// <summary>J (the single charge attack): the trip before the log; the charge's grab; no summon cleanup; the caller ends the combat at the charge's end.</summary>
    public const PostAttackQuirk Charge = NoThrownOrAmmo | PostAttackQuirk.FreeTripBeforeLog | PostAttackQuirk.ChargeGrab
        | PostAttackQuirk.NoSummonCleanup | PostAttackQuirk.CombatEndByCaller | PostAttackQuirk.NoCleave;

    /// <summary>J (the pounce): the grab after all attacks with each hit's Concentration inside its loop; the charge's grab; no summon cleanup.</summary>
    public const PostAttackQuirk ChargePounce = NoThrownOrAmmo | PostAttackQuirk.ConcentrationWithGrab | PostAttackQuirk.ChargeGrab
        | PostAttackQuirk.NoSummonCleanup | PostAttackQuirk.CombatEndByCaller | PostAttackQuirk.NoCleave;

    /// <summary>The bits of <paramref name="origin"/>; <paramref name="pounce"/> picks the charge's pounce set.</summary>
    public static PostAttackQuirk For(AttackOrigin origin, bool pounce = false)
    {
        switch (origin)
        {
            case AttackOrigin.PcSingle: return PcSingle;
            case AttackOrigin.PcSequence: return PcSequence;
            case AttackOrigin.PcFullAttackButton: return PcFullAttackButton;
            case AttackOrigin.PcDualWield: return PcDualWield;
            case AttackOrigin.PcOffHand: return PcOffHand;
            case AttackOrigin.NpcMelee: return NpcMelee;
            case AttackOrigin.NpcRangedFull: return NpcRangedFull;
            case AttackOrigin.NpcRangedSingle: return NpcRangedSingle;
            case AttackOrigin.AoO: return AoO;
            case AttackOrigin.Charge: return pounce ? ChargePounce : Charge;
            default: return PostAttackQuirk.None;
        }
    }

    public static bool Has(PostAttackQuirk set, PostAttackQuirk bit) => (set & bit) == bit;
}

/// <summary>
/// One resolved attack step (or one batch of rolls, for the paths that roll a full attack at once) handed to
/// GameManager.OnAttackResolved: who attacked whom, the attack results, the intent, the origin and its quirk bits, the
/// decider for the attacker's choices, and the caller's own pieces the hook runs at their place in today's order.
/// </summary>
public sealed class AttackResolution
{
    public CharacterController Attacker;
    public CharacterController Target;

    /// <summary>The step's attack results, in roll order (one for a single step).</summary>
    public List<CombatResult> Results = new List<CombatResult>();

    /// <summary>The summed full attack, for the paths that roll one at once (D, G); null otherwise.</summary>
    public FullAttackResult FullResult;

    public AttackIntent Intent;
    public AttackOrigin Origin;

    /// <summary>The origin's bits (<see cref="PostAttackQuirkSets.For"/>).</summary>
    public PostAttackQuirk Quirks;

    /// <summary>The attack's range (the free trip's melee test); null means "from the result's ranged flag".</summary>
    public RangeInfo RangeInfo;

    /// <summary>Who makes the attacker's choices (Improved Grab).</summary>
    public IAttackDecider Decider;

    /// <summary>The combat-end context string, for the origins whose end check is the hook's (A, B, D).</summary>
    public string CombatEndContext;

    /// <summary>Whether the whole sequence is ranged (<see cref="PostAttackQuirk.ReactionsBySequenceMode"/>, C).</summary>
    public bool SequenceIsRanged;

    /// <summary>The caller's bookkeeping and log lines for the step, run where its old block wrote them (after the stages that came first).</summary>
    public Action ReportStep;

    /// <summary>The caller's work right after the reactions (the pounce's rakes).</summary>
    public Action AfterReactions;

    /// <summary>The caller's line when the attack dropped the target and the fight goes on (A), before Cleave.</summary>
    public Action OnFightContinues;

    /// <summary>The caller's end of its sequence before a late grab (<see cref="PostAttackQuirk.GrabAfterCombatEnd"/>, A).</summary>
    public Action BeforeLateGrab;

    /// <summary>True when one of the results killed the target (the full attack's flag for D and G).</summary>
    public bool TargetKilled
    {
        get
        {
            if (FullResult != null)
                return FullResult.TargetKilled;
            for (int i = 0; i < Results.Count; i++)
                if (Results[i] != null && Results[i].TargetKilled)
                    return true;
            return false;
        }
    }

    public bool Has(PostAttackQuirk bit) => PostAttackQuirkSets.Has(Quirks, bit);

    /// <summary>
    /// A resolution of <paramref name="origin"/> with its quirk set. The decider is the attacker's own
    /// (<see cref="AttackDeciders.For"/>); an NPC path passes <see cref="ActorSource.AI"/>, since the NPC turn path is
    /// AI-run even for a controllable creature on it (a confused PC), as before.
    /// </summary>
    public static AttackResolution Create(AttackOrigin origin, CharacterController attacker, CharacterController target,
        AttackIntent intent, ActorSource? deciderSource = null, bool pounce = false)
    {
        return new AttackResolution
        {
            Origin = origin,
            Quirks = PostAttackQuirkSets.For(origin, pounce),
            Attacker = attacker,
            Target = target,
            Intent = intent,
            Decider = AttackDeciders.For(attacker, deciderSource),
        };
    }

    public AttackResolution With(CombatResult result)
    {
        if (result != null)
            Results.Add(result);
        return this;
    }

    public AttackResolution With(IEnumerable<CombatResult> results)
    {
        if (results != null)
            foreach (CombatResult r in results)
                Results.Add(r);
        return this;
    }
}

/// <summary>What the post-attack hook did that the caller's continuation needs.</summary>
public sealed class PostAttackOutcome
{
    /// <summary>The hook's combat-end check ended the combat (the caller stops).</summary>
    public bool CombatEnded;

    /// <summary>The target was out of the fight at the combat-end stage.</summary>
    public bool TargetOutOfFight;

    /// <summary>An Improved Grab was attempted (a skip included) and whether it took hold.</summary>
    public bool GrabAttempted;
    public bool GrabSucceeded;

    /// <summary>The hook stopped to wait on a decider (the PC's Improved Grab prompt) and finishes later.</summary>
    public bool Suspended;

    /// <summary>The hook ran to its end.</summary>
    public bool Finished;
}
