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
/// The differences left between the attack paths' post-attack work, as named bits (batch 12 steps 13 and 14, the batch 11
/// LegacyCastQuirk pattern). Step 14 removed the grab, break, ammunition, thrown-drop, Concentration, reaction, order and
/// combat-end bits: every origin now runs the same stages (GameManager.OnAttackResolved). Only the Cleave bits are left,
/// removed by step 15 (CMB-017).
/// </summary>
[Flags]
public enum PostAttackQuirk
{
    None = 0,

    /// <summary>No Cleave or Great Cleave after the attack drops a foe (every origin but A; CMB-017).</summary>
    NoCleave = 1 << 0,
    /// <summary>Cleave comes after the combat-end check, at the end of path A's step.</summary>
    CleaveAfterCombatEnd = 1 << 1,
}

/// <summary>The <see cref="PostAttackQuirk"/> bits each origin sets (verified in code 2026-10-28, batch 12 step 14).</summary>
public static class PostAttackQuirkSets
{
    /// <summary>A: Cleave after the combat-end check (path A's code, CombatFlowService.ResolveCleaveAfterDrop).</summary>
    public const PostAttackQuirk PcSingle = PostAttackQuirk.CleaveAfterCombatEnd;

    /// <summary>Every other origin (B-J): no Cleave yet (step 15).</summary>
    public const PostAttackQuirk Others = PostAttackQuirk.NoCleave;

    /// <summary>The bits of <paramref name="origin"/>. A pounce step has the charge's bits (it is origin J, one step at a time).</summary>
    public static PostAttackQuirk For(AttackOrigin origin)
    {
        switch (origin)
        {
            case AttackOrigin.PcSingle: return PcSingle;
            case AttackOrigin.PcSequence:
            case AttackOrigin.PcFullAttackButton:
            case AttackOrigin.PcDualWield:
            case AttackOrigin.PcOffHand:
            case AttackOrigin.NpcMelee:
            case AttackOrigin.NpcRangedFull:
            case AttackOrigin.NpcRangedSingle:
            case AttackOrigin.AoO:
            case AttackOrigin.Charge:
                return Others;
            default: return PostAttackQuirk.None;
        }
    }

    public static bool Has(PostAttackQuirk set, PostAttackQuirk bit) => (set & bit) == bit;
}

/// <summary>
/// One resolved attack step handed to GameManager.OnAttackResolved (the legacy two-weapon full attack, D, hands its one
/// batch of rolls): who attacked whom, the attack results, the intent, the origin and its quirk bits, the decider for the
/// attacker's choices, and the caller's own pieces the hook runs at their place.
/// </summary>
public sealed class AttackResolution
{
    public CharacterController Attacker;
    public CharacterController Target;

    /// <summary>The step's attack results, in roll order (one for a single step).</summary>
    public List<CombatResult> Results = new List<CombatResult>();

    /// <summary>The full attack, for the path that rolls one at once (D); null otherwise.</summary>
    public FullAttackResult FullResult;

    public AttackIntent Intent;
    public AttackOrigin Origin;

    /// <summary>The origin's bits (<see cref="PostAttackQuirkSets.For"/>).</summary>
    public PostAttackQuirk Quirks;

    /// <summary>The attack's range (the free trip's melee test); null means "from the result's ranged flag".</summary>
    public RangeInfo RangeInfo;

    /// <summary>
    /// Who makes the attacker's choices (Improved Grab). An AoO and the NPC turn path pass the AI decider (the AoO runs
    /// inside another creature's action and cannot wait on a prompt).
    /// </summary>
    public IAttackDecider Decider;

    /// <summary>The combat-end context string (the CombatEnd log's source); null names the origin.</summary>
    public string CombatEndContext;

    /// <summary>The caller's bookkeeping and log lines for the step, run first.</summary>
    public Action ReportStep;

    /// <summary>The caller's line when the attack dropped the target and the fight goes on (A), before Cleave.</summary>
    public Action OnFightContinues;

    /// <summary>The caller's end of its step after Cleave (A: the end of its sequence).</summary>
    public Action OnStepEnd;

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
        AttackIntent intent, ActorSource? deciderSource = null)
    {
        return new AttackResolution
        {
            Origin = origin,
            Quirks = PostAttackQuirkSets.For(origin),
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

    /// <summary>The attacker was out of the fight (dead, dying or unconscious) after the reactions.</summary>
    public bool AttackerOutOfFight;

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
