using System;
using System.Collections.Generic;

/// <summary>
/// Which attack path resolved an attack (batch 12 step 13; the letters of the attack map in
/// docs/designs/pc_npc_parity_batch12.md). Each path still runs its own pre-attack steps and continuation; the
/// post-attack work of all of them is GameManager.OnAttackResolved, the same for every origin since batch 12 step 15
/// (the PostAttackQuirk bits that named the paths' differences are gone).
/// </summary>
public enum AttackOrigin
{
    /// <summary>A: the PC's single attack (CombatFlowService.PerformSingleAttack).</summary>
    PcSingle,
    /// <summary>B: a step of the PC's iterative attack sequence (CombatFlowService.PerformIterativeSequenceAttack).</summary>
    PcSequence,
    /// <summary>C: a step of the PC's Full Attack button (GameManager.PerformFullAttackWithRetargetingAndFiveFootStep).</summary>
    PcFullAttackButton,
    /// <summary>D: a step of the PC's Dual Wield full attack (AttackSequenceRequest.ForUiTwoWeaponFullAttack, batch 12 step 18).</summary>
    PcDualWield,
    /// <summary>E: the PC's off-hand attack (GameManager.HandleOffHandTargetClick, AttackSequenceRequest.ForUiOffHand).</summary>
    PcOffHand,
    /// <summary>F: a melee step of an NPC attack sequence (GameManager.RunAttackSequence, AttackSequenceRequest.ForAI).</summary>
    NpcMelee,
    /// <summary>G: a ranged or thrown step of an NPC full attack (GameManager.RunAttackSequence).</summary>
    NpcRangedFull,
    /// <summary>H: a ranged or thrown NPC attack that is not (yet) a full attack (GameManager.RunAttackSequence).</summary>
    NpcRangedSingle,
    /// <summary>I: an attack of opportunity (ThreatSystem.ExecuteAoO).</summary>
    AoO,
    /// <summary>J: the attack or pounce at the end of a charge, PC and NPC (GameManager.ExecuteCharge, NPCExecuteCharge).</summary>
    Charge
}

/// <summary>
/// One resolved attack step handed to GameManager.OnAttackResolved: who attacked whom, the attack results, the intent, the origin, the decider for the attacker's choices,
/// and the caller's own pieces the hook runs at their place. A Cleave attack is one more resolution of the same origin
/// (<see cref="IsCleave"/>), run by the hook itself.
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

    /// <summary>The step is a Cleave or Great Cleave attack the hook made after another step dropped a foe (PHB p.92, D7).</summary>
    public bool IsCleave;

    /// <summary>
    /// False when the caller runs the hook synchronously (GameManager.RunAttackResolvedSync: the AoO and the NPC melee
    /// step), so no choice in it may wait on a prompt: the target's reaction (the acid spray) is then the AI's choice.
    /// </summary>
    public bool CallerCanWait = true;

    /// <summary>The attack's range (the free trip's melee test); null means "from the result's ranged flag".</summary>
    public RangeInfo RangeInfo;

    /// <summary>
    /// Who makes the attacker's choices (Improved Grab, the Cleave target). An AoO and the NPC turn path pass the AI decider
    /// (the AoO runs inside another creature's action and cannot wait on a prompt).
    /// </summary>
    public IAttackDecider Decider;

    /// <summary>The combat-end context string (the CombatEnd log's source); null names the origin.</summary>
    public string CombatEndContext;

    /// <summary>The caller's bookkeeping and log lines for the step, run first.</summary>
    public Action ReportStep;

    /// <summary>The caller's line when the attack dropped the target and the fight goes on (A), after the combat-end check.</summary>
    public Action OnFightContinues;

    /// <summary>The caller's end of its step (A: the end of its sequence), after <see cref="OnFightContinues"/>.</summary>
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

    /// <summary>
    /// A resolution of <paramref name="origin"/>. The decider is the attacker's own
    /// (<see cref="AttackDeciders.For"/>); an NPC path passes <see cref="ActorSource.AI"/>, since the NPC turn path is
    /// AI-run even for a controllable creature on it (a confused PC), as before.
    /// </summary>
    public static AttackResolution Create(AttackOrigin origin, CharacterController attacker, CharacterController target,
        AttackIntent intent, ActorSource? deciderSource = null)
    {
        return new AttackResolution
        {
            Origin = origin,
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
