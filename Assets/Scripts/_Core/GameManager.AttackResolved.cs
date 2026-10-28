using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The one post-attack hook (batch 12 steps 13 and 14, docs/designs/pc_npc_parity_batch12.md): every attack path (A-J,
/// <see cref="AttackOrigin"/>) hands each resolved attack step to <see cref="OnAttackResolved"/>, which runs the same work
/// after the roll for PCs and NPCs alike, in this order:
/// 1. the caller's bookkeeping and log lines (<see cref="AttackResolution.ReportStep"/>);
/// 2. the thrown drop (every thrown weapon, by the intent: ResolveThrownWeaponAfterAttack; PHB p.113) and one piece of
///    ammunition per projectile shot (CombatFlowService.ConsumeAmmoForAttack);
/// 3. Concentration once per damaging hit (PHB p.70);
/// 4. the melee reactions to each melee hit (Fire Shield and the like, MeleeReactionService);
/// 5. the free trip (Trip (Ex), MM; TryResolveFreeTripOnHit) with its aftermath;
/// 6. Improved Grab after each trigger hit until one takes hold (MM p.310; decision D8), the choice by the resolution's
///    decider (<see cref="IAttackDecider"/>);
/// 7. Cleave (no origin yet: path A's comes after the combat-end check; step 15);
/// 8. the summon cleanup of a slain target or attacker, late so that nothing earlier sees a despawned creature;
/// 9. the combat-end check for the target and the attacker (a Fire Shield can drop the attacker; CORE-011);
/// 10. path A's tail: its slain line, Cleave and Great Cleave (PHB p.92), the end of its step;
/// 11. AttackResolvedEvent, one per result (tactics README 1.4 rule 6; no subscribers yet).
/// The charm, command undead, fascination and turned-undead fear breaks run on the swing itself (the fear break before
/// its roll, BreakTurningBeforeSwing; the others after it, NotifyWeaponAttackAgainst; both from
/// CharacterController.PerformSingleAttackWithCrit) and on every maneuver
/// (NotifyManeuverAgainst, from CharacterController.ExecuteSpecialAttack). Target-side triggers (D1) come in step 15. The
/// only origin differences left are the Cleave bits (<see cref="PostAttackQuirk"/>).
/// Loops that resolve attacks iterate copies of the creature lists (GetAllCharacters, threatener lists), so a summon
/// despawned by stage 8 never changes a list under a caller (AI-015).
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

        // 1. The caller's bookkeeping and log lines.
        res.ReportStep?.Invoke();

        // 2. Thrown drop and ammunition.
        RunPostAttackThrownAndAmmo(res);

        // 3. Concentration, once per damaging hit.
        RunPostAttackConcentration(res);

        // 4. Melee reactions.
        RunPostAttackReactions(res);

        // 5. Free trip.
        RunPostAttackFreeTrip(res);

        // 6. Improved Grab.
        IEnumerator grab = RunPostAttackImprovedGrab(res, outcome);
        while (grab.MoveNext())
            yield return grab.Current;

        // 7. Cleave.
        if (!res.Has(PostAttackQuirk.NoCleave) && !res.Has(PostAttackQuirk.CleaveAfterCombatEnd) && RunPostAttackCleave(res))
        {
            outcome.CombatEnded = true;
            FinishAttackResolved(res, outcome);
            yield break;
        }

        // 8. Summon cleanup (idempotent: a summon already despawned is no longer tracked).
        if (target != null && target.Stats != null && target.Stats.IsDead)
            HandleSummonDeathCleanup(target);
        if (attacker.Stats != null && attacker.Stats.IsDead)
            HandleSummonDeathCleanup(attacker);

        // 9. Combat end for the target and the attacker: either side, dead or only out of the fight (dying, unconscious),
        // the shared check (CORE-011, CORE-037). The attacker can drop to a reaction (Fire Shield).
        outcome.TargetOutOfFight = CombatEndRules.IsOutOfFight(target);
        outcome.AttackerOutOfFight = CombatEndRules.IsOutOfFight(attacker);
        if (outcome.TargetOutOfFight || outcome.AttackerOutOfFight)
        {
            UpdateAllStatsUI();
            string context = res.CombatEndContext ?? ("OnAttackResolved." + res.Origin);
            if (outcome.TargetOutOfFight)
                RegisterDefeatedEnemyForXP(target, context);
            if (outcome.AttackerOutOfFight)
                RegisterDefeatedEnemyForXP(attacker, context);
            if (EvaluateCombatEnd(context))
            {
                outcome.CombatEnded = true;
                FinishAttackResolved(res, outcome);
                yield break;
            }
        }

        // 10. Path A's tail: its slain line, Cleave, the end of its step.
        res.OnFightContinues?.Invoke();

        if (!res.Has(PostAttackQuirk.NoCleave) && res.Has(PostAttackQuirk.CleaveAfterCombatEnd) && RunPostAttackCleave(res))
        {
            outcome.CombatEnded = true;
            FinishAttackResolved(res, outcome);
            yield break;
        }

        res.OnStepEnd?.Invoke();

        // 11. The event.
        FinishAttackResolved(res, outcome);
    }

    /// <summary>The hook's end: AttackResolvedEvent for each result (tactics README 1.4 rule 6; no subscribers yet), then Finished.</summary>
    private void FinishAttackResolved(AttackResolution res, PostAttackOutcome outcome)
    {
        GameEventSystem events = GameEventSystem.Instance;
        if (events != null)
        {
            for (int i = 0; i < res.Results.Count; i++)
            {
                CombatResult r = res.Results[i];
                if (r == null || r.NotMade)
                    continue;
                events.Publish(new AttackResolvedEvent
                {
                    Attacker = res.Attacker,
                    Target = res.Target,
                    Hit = r.Hit,
                    Damage = r.Hit ? r.TotalDamage : 0,
                    IsCritical = r.CritConfirmed,
                    WeaponName = r.WeaponName,
                });
            }
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
    /// <see cref="OnAttackResolved"/> for a caller that cannot wait (its decider is synchronous: the AoO and the NPC turn
    /// path pass the AI decider). A run that waits anyway is a bug: it is reported and finishes as a coroutine.
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

    /// <summary>The Trip (Ex) free trip after each hit (TryResolveFreeTripOnHit, stopping once the target is dead or prone).</summary>
    private void RunPostAttackFreeTrip(AttackResolution res)
    {
        // An attacker a reaction dropped (Fire Shield: dead, dying or unconscious) makes no free trip.
        if (CombatEndRules.IsOutOfFight(res.Attacker))
            return;
        TryResolveFreeTripFromAttackResults(res.Attacker, res.Target, res.Results, res.RangeInfo);
    }

    /// <summary>
    /// The thrown weapon's drop (every thrown weapon, by the intent: ResolveThrownWeaponAfterAttack; PHB p.113) and one
    /// piece of ammunition for each shot with a projectile weapon (PHB p.113). The PC paths' UI field for the weapon in
    /// hand is refreshed from the thrower's inventory (<see cref="RefreshUiEquippedWeapon"/>).
    /// </summary>
    private void RunPostAttackThrownAndAmmo(AttackResolution res)
    {
        if (res.Results.Count == 0)
            return;
        // The intent's weapon is the weapon used: every builder sets it, and a null one is an unarmed or natural attack
        // (an AoO whose best weapon is not the bow in hand), so it never falls back to the main weapon. Only an intent-less
        // resolution reads the main weapon.
        ItemData weapon = res.Intent != null ? res.Intent.Weapon : res.Attacker.GetEquippedMainWeapon();
        if (weapon == null)
            return;

        // Results that were never made (CombatResult.NotMade: Attack refused before the roll) neither throw nor shoot.
        bool anyMade = false;
        for (int i = 0; i < res.Results.Count && !anyMade; i++)
            anyMade = res.Results[i] != null && !res.Results[i].NotMade;
        if (!anyMade)
            return;

        if (ResolveThrownWeaponAfterAttack(res.Attacker, res.Target, weapon, res.Intent) && IsPcAttackOrigin(res.Origin))
            RefreshUiEquippedWeapon(res.Attacker);

        // One piece per ranged shot made (a melee result, a charge or a natural AoO, spends none).
        if (weapon.IsProjectileWeapon && _combatFlowService != null)
            for (int i = 0; i < res.Results.Count; i++)
            {
                CombatResult r = res.Results[i];
                if (r != null && !r.NotMade && r.IsRangedAttack)
                    _combatFlowService.ConsumeAmmoForAttack(res.Attacker, weapon);
            }
    }

    /// <summary>The PC attack paths (A-E), whose UI keeps its own field for the weapon in hand.</summary>
    private static bool IsPcAttackOrigin(AttackOrigin origin)
        => origin == AttackOrigin.PcSingle || origin == AttackOrigin.PcSequence || origin == AttackOrigin.PcFullAttackButton
        || origin == AttackOrigin.PcDualWield || origin == AttackOrigin.PcOffHand;

    /// <summary>The PC UI's field for the weapon in hand, read back from <paramref name="pc"/>'s inventory after a throw.</summary>
    private void RefreshUiEquippedWeapon(CharacterController pc)
    {
        if (pc != null)
            _equippedWeapon = pc.GetEquippedMainWeapon();
    }

    /// <summary>Concentration for damage (PHB p.70): one check per damaging hit.</summary>
    private void RunPostAttackConcentration(AttackResolution res)
    {
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
            if (r != null && r.Hit && !r.IsRangedAttack)
                MeleeReactionService.TriggerReactions(res.Attacker, res.Target, r);
        }
    }

    /// <summary>
    /// Improved Grab after a trigger hit (MM p.310; decision D8: each hit with the named attack, whatever the action): the
    /// shared trigger test (CanAttemptImprovedGrabFromAttack), the decider's choice (grapple normally, hold with the body
    /// part at -20, or skip; CMB-148) and the one resolver (ResolveImprovedGrabChoice). It tries each hit of the step until
    /// a grab takes hold or the target is dead.
    /// </summary>
    private IEnumerator RunPostAttackImprovedGrab(AttackResolution res, PostAttackOutcome outcome)
    {
        CharacterController attacker = res.Attacker;
        CharacterController target = res.Target;

        if (attacker == null || attacker.Stats == null || target == null || target.Stats == null || res.Results.Count == 0)
            yield break;
        // An attacker a reaction dropped (Fire Shield: dead, dying or unconscious) attempts nothing more.
        if (!attacker.Stats.HasImprovedGrab || target.Stats.IsDead || CombatEndRules.IsOutOfFight(attacker))
            yield break;

        for (int i = 0; i < res.Results.Count; i++)
        {
            CombatResult hit = res.Results[i];
            if (hit == null)
                continue;

            if (!CanAttemptImprovedGrabFromAttack(attacker, target, hit))
                continue;

            ImprovedGrabChoice choice = ImprovedGrabChoice.Skip;
            IAttackDecider decider = res.Decider ?? AttackDeciders.For(attacker);
            Coroutine wait = decider.ChooseImprovedGrab(this, attacker, target, hit, c => choice = c);
            if (wait != null)
                yield return wait;

            outcome.GrabAttempted = true;
            if (choice == ImprovedGrabChoice.Skip)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("↷", $"{attacker.Stats.CharacterName} declines to start a grapple."));
                continue;
            }

            SpecialAttackResult grabResult = ResolveImprovedGrabChoice(attacker, target, hit, choice);
            string attackName = !string.IsNullOrWhiteSpace(hit.WeaponName) ? hit.WeaponName : "trigger attack";
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("🪢", $"Improved Grab ({attackName} hit): {grabResult.Log}"));
            outcome.GrabSucceeded |= grabResult.Success;

            if (grabResult.Success || target.Stats.IsDead)
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
