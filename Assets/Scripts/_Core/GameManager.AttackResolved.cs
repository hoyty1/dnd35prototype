using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The one post-attack hook (batch 12 step 13, docs/designs/pc_npc_parity_batch12.md): every attack path (A-J,
/// <see cref="AttackOrigin"/>) hands its resolved attack step to <see cref="OnAttackResolved"/>, which runs the work after
/// the roll for PCs and NPCs alike. Its stages, in order:
/// 1. the free trip, for the origins that make it first (<see cref="PostAttackQuirk.FreeTripBeforeLog"/>);
/// 2. the thrown drop and the ammunition, for the origins that spend them before their log;
/// 3. the caller's bookkeeping and log lines (<see cref="AttackResolution.ReportStep"/>);
/// 4. the thrown drop and the ammunition (by intent: GameManager.ResolveThrownWeaponAfterAttack, CombatFlowService.ConsumeAmmoForAttack);
/// 5. Concentration per hit (PHB p.70), or on the summed damage (D, G);
/// 6. the melee reactions (Fire Shield and the like, MeleeReactionService), then the caller's work after them (the pounce's rakes)
///    and a deferred free-trip aftermath (the AoO);
/// 7. the free trip (Trip (Ex), MM; TryResolveFreeTripOnHit);
/// 8. Improved Grab (MM p.310), the choice by the resolution's decider (<see cref="IAttackDecider"/>);
/// 9. Cleave (no origin yet: path A's comes after the combat-end check, stage 12);
/// 10. the summon cleanup of a slain target, at the end so that nothing earlier sees a despawned creature;
/// 11. the combat-end check (CORE-011), for the origins whose check is not their caller's (A, B, D);
/// 12. path A's tail: its slain line, Cleave and Great Cleave (PHB p.92), the end of its sequence, then its Improved Grab.
/// The charm, fascination and command breaks run inside CharacterController.Attack and the turned-undead fear break
/// before the roll; target-side triggers (D1) and AttackResolvedEvent come in steps 14 and 15. Each origin's differences
/// from this order are its <see cref="PostAttackQuirk"/> bits (a pure move: every trace hash unchanged).
/// Loops that resolve attacks iterate copies of the creature lists (GetAllCharacters, threatener lists), so a summon
/// despawned by stage 10 never changes a list under a caller (AI-015).
/// The hook and its runners are internal on purpose: only the attack paths, the static suites and the scenario harness
/// (all in Assembly-CSharp) run it; a Unity_RunCommand script (its own assembly) reaches it through an attack path, not
/// directly, so that no caller skips its path's bookkeeping.
/// </summary>
public partial class GameManager
{
    /// <summary>
    /// The post-attack work of one resolved attack step (see the class summary). Yields only while a decider waits on the
    /// player (the Improved Grab prompt). A coroutine caller drives it inline
    /// (<c>var post = OnAttackResolved(res, o); while (post.MoveNext()) yield return post.Current;</c>); a synchronous one
    /// calls <see cref="RunAttackResolved"/> (callback form) or <see cref="RunAttackResolvedSync"/>.
    /// </summary>
    internal IEnumerator OnAttackResolved(AttackResolution res, PostAttackOutcome outcome)
    {
        if (outcome == null)
            outcome = new PostAttackOutcome();
        if (res == null || res.Attacker == null)
        {
            outcome.Finished = true;
            yield break;
        }

        CharacterController attacker = res.Attacker;
        CharacterController target = res.Target;
        List<SpecialAttackResult> deferredTripAftermaths = null;

        // 1-2. What some paths do before their log.
        if (!res.Has(PostAttackQuirk.NoFreeTrip) && res.Has(PostAttackQuirk.FreeTripBeforeLog))
            deferredTripAftermaths = RunPostAttackFreeTrip(res);
        if (res.Has(PostAttackQuirk.ThrownAndAmmoBeforeLog))
            RunPostAttackThrownAndAmmo(res);

        // 3. The caller's bookkeeping and log lines.
        res.ReportStep?.Invoke();

        // 4. Thrown drop and ammunition.
        if (!res.Has(PostAttackQuirk.ThrownAndAmmoBeforeLog))
            RunPostAttackThrownAndAmmo(res);

        // 5. Concentration.
        if (!res.Has(PostAttackQuirk.NoConcentration) && !res.Has(PostAttackQuirk.ConcentrationWithGrab))
            RunPostAttackConcentration(res);

        // 6. Melee reactions, the caller's work after them, a deferred trip aftermath.
        if (!res.Has(PostAttackQuirk.NoReactions))
            RunPostAttackReactions(res);
        res.AfterReactions?.Invoke();
        if (deferredTripAftermaths != null)
            for (int i = 0; i < deferredTripAftermaths.Count; i++)
                HandleTripAftermath(attacker, target, deferredTripAftermaths[i], null);

        // 7. Free trip.
        if (!res.Has(PostAttackQuirk.NoFreeTrip) && !res.Has(PostAttackQuirk.FreeTripBeforeLog))
            RunPostAttackFreeTrip(res);

        // 8. Improved Grab.
        if (!res.Has(PostAttackQuirk.NoImprovedGrab) && !res.Has(PostAttackQuirk.GrabAfterCombatEnd))
        {
            IEnumerator grab = RunPostAttackImprovedGrab(res, outcome);
            while (grab.MoveNext())
                yield return grab.Current;
        }

        // 9. Cleave.
        if (!res.Has(PostAttackQuirk.NoCleave) && !res.Has(PostAttackQuirk.CleaveAfterCombatEnd) && RunPostAttackCleave(res))
        {
            outcome.CombatEnded = true;
            outcome.Finished = true;
            yield break;
        }

        // 10. Summon cleanup.
        if (!res.Has(PostAttackQuirk.NoSummonCleanup))
        {
            bool slain = res.Has(PostAttackQuirk.SummonCleanupWhenDead)
                ? target != null && target.Stats != null && target.Stats.IsDead
                : res.TargetKilled;
            if (slain)
                HandleSummonDeathCleanup(target);
        }

        // 11. Combat end: either side, dead or only out of the fight (dying, unconscious), the shared check (CORE-011, CORE-037).
        outcome.TargetOutOfFight = CombatEndRules.IsOutOfFight(target);
        if (!res.Has(PostAttackQuirk.CombatEndByCaller) && outcome.TargetOutOfFight)
        {
            UpdateAllStatsUI();
            if (CheckCombatVictory(res.CombatEndContext, target))
            {
                outcome.CombatEnded = true;
                outcome.Finished = true;
                yield break;
            }
        }

        // 12. Path A's tail: its slain line, Cleave, the end of its sequence, then Improved Grab.
        res.OnFightContinues?.Invoke();

        if (!res.Has(PostAttackQuirk.NoCleave) && res.Has(PostAttackQuirk.CleaveAfterCombatEnd) && RunPostAttackCleave(res))
        {
            outcome.CombatEnded = true;
            outcome.Finished = true;
            yield break;
        }

        res.BeforeLateGrab?.Invoke();

        if (!res.Has(PostAttackQuirk.NoImprovedGrab) && res.Has(PostAttackQuirk.GrabAfterCombatEnd))
        {
            IEnumerator grab = RunPostAttackImprovedGrab(res, outcome);
            while (grab.MoveNext())
                yield return grab.Current;
        }

        outcome.Finished = true;
    }

    /// <summary>
    /// The callback form of <see cref="OnAttackResolved"/> for a synchronous caller: runs it inline and returns false when
    /// it finished; when a decider waits (the PC's Improved Grab prompt) it returns true, the rest runs as a coroutine and
    /// <paramref name="onResumedDone"/> is called when it ends.
    /// </summary>
    internal bool RunAttackResolved(AttackResolution res, PostAttackOutcome outcome, Action onResumedDone = null)
    {
        IEnumerator run = OnAttackResolved(res, outcome);
        while (run.MoveNext())
        {
            object wait = run.Current;
            if (wait == null)
                continue;
            if (outcome != null)
                outcome.Suspended = true;
            StartCoroutine(ContinueAttackResolved(run, wait, onResumedDone));
            return true;
        }
        return false;
    }

    /// <summary>
    /// <see cref="OnAttackResolved"/> for a caller that cannot wait (its decider is synchronous or its origin makes no
    /// choice). A run that waits anyway is a bug: it is reported and finishes as a coroutine.
    /// </summary>
    internal void RunAttackResolvedSync(AttackResolution res, PostAttackOutcome outcome)
    {
        if (RunAttackResolved(res, outcome))
            Debug.LogError($"[AttackResolved] {res?.Origin} for {res?.Attacker?.Stats?.CharacterName ?? "<null>"} waited on its decider in a synchronous caller; it finishes later.");
    }

    private IEnumerator ContinueAttackResolved(IEnumerator run, object first, Action onDone)
    {
        yield return first;
        while (run.MoveNext())
            yield return run.Current;
        onDone?.Invoke();
    }

    /// <summary>
    /// The Trip (Ex) free trip after each hit (TryResolveFreeTripOnHit, stopping once the target is dead or prone). With
    /// <see cref="PostAttackQuirk.TripAftermathAfterReactions"/> only the trip is made and its aftermath is returned for
    /// after the reactions.
    /// </summary>
    private List<SpecialAttackResult> RunPostAttackFreeTrip(AttackResolution res)
    {
        if (!res.Has(PostAttackQuirk.TripAftermathAfterReactions))
        {
            TryResolveFreeTripFromAttackResults(res.Attacker, res.Target, res.Results, res.RangeInfo);
            return null;
        }

        var aftermaths = new List<SpecialAttackResult>();
        for (int i = 0; i < res.Results.Count; i++)
        {
            SpecialAttackResult trip = ResolveFreeTripAfterHit(res.Attacker, res.Target, res.Results[i], res.RangeInfo);
            if (trip != null)
                aftermaths.Add(trip);
            if (res.Target == null || res.Target.Stats == null || res.Target.Stats.IsDead || res.Target.HasCondition(CombatConditionType.Prone))
                break;
        }
        return aftermaths;
    }

    /// <summary>The thrown weapon's drop (by the intent's mode) and one piece of ammunition for a projectile weapon.</summary>
    private void RunPostAttackThrownAndAmmo(AttackResolution res)
    {
        ItemData weapon = res.Intent != null ? res.Intent.Weapon : null;
        if (!res.Has(PostAttackQuirk.NoThrownDrop) && !res.Has(PostAttackQuirk.ThrownDropByCaller))
            ResolveThrownWeaponAfterAttack(res.Attacker, res.Target, weapon, res.Intent);
        if (!res.Has(PostAttackQuirk.NoAmmoSpend) && weapon != null && weapon.IsProjectileWeapon && _combatFlowService != null)
            _combatFlowService.ConsumeAmmoForAttack(res.Attacker, weapon);
    }

    /// <summary>Concentration for damage (PHB p.70): one check per damaging hit, or one on the summed damage.</summary>
    private void RunPostAttackConcentration(AttackResolution res)
    {
        if (res.Has(PostAttackQuirk.ConcentrationSummed))
        {
            int total = 0;
            if (res.FullResult != null)
                total = res.FullResult.TotalDamageDealt;
            else
                for (int i = 0; i < res.Results.Count; i++)
                    if (res.Results[i] != null && res.Results[i].Hit)
                        total += res.Results[i].TotalDamage;
            if (total > 0)
                CheckConcentrationOnDamage(res.Target, total);
            return;
        }

        for (int i = 0; i < res.Results.Count; i++)
        {
            CombatResult r = res.Results[i];
            if (r != null && r.Hit && r.TotalDamage > 0)
                CheckConcentrationOnDamage(res.Target, r.TotalDamage);
        }
    }

    /// <summary>Melee reaction effects (Fire Shield, Thorns, etc.) for each melee hit.</summary>
    private void RunPostAttackReactions(AttackResolution res)
    {
        for (int i = 0; i < res.Results.Count; i++)
        {
            CombatResult r = res.Results[i];
            if (r == null || !r.Hit)
                continue;
            bool ranged = res.Has(PostAttackQuirk.ReactionsBySequenceMode) ? res.SequenceIsRanged
                : !res.Has(PostAttackQuirk.ReactionsOnAnyHit) && r.IsRangedAttack;
            if (!ranged)
                MeleeReactionService.TriggerReactions(res.Attacker, res.Target, r);
        }
    }

    /// <summary>
    /// Improved Grab after a trigger hit (MM p.310): the shared trigger test (CanAttemptImprovedGrabFromAttack), the
    /// decider's choice (grapple normally, hold with the body part at -20, or skip; CMB-148) and the one resolver
    /// (ResolveImprovedGrabChoice). It tries each hit until a grab takes hold or the target is dead; the charge's grab
    /// (<see cref="PostAttackQuirk.ChargeGrab"/>) tries once, on the first eligible hit, and with
    /// <see cref="PostAttackQuirk.ConcentrationWithGrab"/> each hit's Concentration check runs in the same loop.
    /// </summary>
    private IEnumerator RunPostAttackImprovedGrab(AttackResolution res, PostAttackOutcome outcome)
    {
        CharacterController attacker = res.Attacker;
        CharacterController target = res.Target;
        bool chargeGrab = res.Has(PostAttackQuirk.ChargeGrab);
        bool concentrationInLoop = res.Has(PostAttackQuirk.ConcentrationWithGrab);

        if (attacker == null || attacker.Stats == null || target == null || target.Stats == null || res.Results.Count == 0)
            yield break;
        if (!chargeGrab && (!attacker.Stats.HasImprovedGrab || target.Stats.IsDead))
            yield break;

        bool attempted = false;
        bool succeeded = false;
        for (int i = 0; i < res.Results.Count; i++)
        {
            CombatResult hit = res.Results[i];
            if (hit == null)
                continue;

            if (concentrationInLoop && hit.Hit && hit.TotalDamage > 0)
                CheckConcentrationOnDamage(target, hit.TotalDamage);

            if (chargeGrab && (attempted || succeeded || target.Stats.IsDead))
                continue;

            if (!CanAttemptImprovedGrabFromAttack(attacker, target, hit))
                continue;

            ImprovedGrabChoice choice = ImprovedGrabChoice.Skip;
            IAttackDecider decider = res.Decider ?? AttackDeciders.For(attacker);
            Coroutine wait = decider.ChooseImprovedGrab(this, attacker, target, hit, c => choice = c);
            if (wait != null)
                yield return wait;

            attempted = true;
            outcome.GrabAttempted = true;
            if (choice == ImprovedGrabChoice.Skip)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("↷", $"{attacker.Stats.CharacterName} declines to start a grapple."));
                continue;
            }

            SpecialAttackResult grabResult = ResolveImprovedGrabChoice(attacker, target, hit, choice);
            if (chargeGrab)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("🪢", $"Improved Grab: {grabResult.Log}"));
            }
            else
            {
                string attackName = !string.IsNullOrWhiteSpace(hit.WeaponName) ? hit.WeaponName : "trigger attack";
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("🪢", $"Improved Grab ({attackName} hit): {grabResult.Log}"));
            }

            succeeded = grabResult.Success;
            outcome.GrabSucceeded |= grabResult.Success;

            // The pounce's rakes came with its full attack; grapple actions begin next turn.
            if (chargeGrab && grabResult.Success && res.Intent != null && res.Intent.IsPounce)
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("⏹", $"{attacker.Stats.CharacterName} established the grapple, but pounce already consumed the full-round action. Grapple attacks begin next turn."));

            if (!chargeGrab && (grabResult.Success || target.Stats.IsDead))
                break;
        }
    }

    /// <summary>Cleave and Great Cleave after an attack that killed its target (path A's code, CombatFlowService.ResolveCleaveAfterDrop); true when the combat ended.</summary>
    private bool RunPostAttackCleave(AttackResolution res)
    {
        if (!res.TargetKilled || _combatFlowService == null || res.Results.Count == 0)
            return false;
        return _combatFlowService.ResolveCleaveAfterDrop(res.Attacker, res.Target, res.Results[0], res.Intent != null ? res.Intent.Weapon : null);
    }
}
