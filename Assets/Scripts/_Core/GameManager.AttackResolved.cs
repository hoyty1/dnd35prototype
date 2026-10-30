using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The one post-attack hook (batch 12 steps 13-15, docs/designs/pc_npc_parity_batch12.md): every attack path (B-J,
/// <see cref="AttackOrigin"/>; A was retired in batch 13 step 17) hands each resolved attack step to <see cref="OnAttackResolved"/>, which runs the same work
/// after the roll for PCs and NPCs alike, whatever the origin, in this order:
/// 1. the caller's bookkeeping and log lines (<see cref="AttackResolution.ReportStep"/>);
/// 2. the thrown drop (every thrown weapon, by the intent: ResolveThrownWeaponAfterAttack; PHB p.113) and one piece of
///    ammunition per projectile shot (CombatFlowService.ConsumeAmmoForAttack);
/// 3. Concentration once per damaging hit (PHB p.70);
/// 4. the melee reactions to each melee hit (Fire Shield and the like, MeleeReactionService);
/// 5. the target-side triggers: the target's reaction to being attacked (the bombardier beetle's acid spray, MM p.285;
///    decision D1; GameManager.AttackedReactions.cs), and any reaction a stage before held (a Fire Shield that burned a
///    beetle attacker); a reaction that ended the combat ends the step here (CombatEnded);
/// 6. the free trip (Trip (Ex), MM; TryResolveFreeTripOnHit) with its aftermath;
/// 7. Improved Grab after each trigger hit until one takes hold (MM p.310; decision D8), the choice by the resolution's
///    decider (<see cref="IAttackDecider"/>);
/// 8. Cleave and Great Cleave after a melee step that dropped its target (PHB p.92; decision D7; <see cref="CleaveRules"/>):
///    one more step of the same origin against another creature in reach, run through this hook again, so it can grab,
///    trip and, with Great Cleave, cleave again;
/// 9. the summon cleanup of a slain target or attacker, late so that nothing earlier sees a despawned creature;
/// 10. the combat-end check for the target and the attacker (a Fire Shield can drop the attacker; CORE-011);
/// 11. AttackResolvedEvent, one per result (tactics README 1.4 rule 6; no subscribers yet).
/// (Path A's tail, its slain line and the end of its step, went with path A in batch 13 step 17, CMB-200: every PC single
/// attack is a step of the attack sequence, whose caller does its own continuation.)
/// The charm, command undead, fascination and turned-undead fear breaks run on the swing itself (the fear break before
/// its roll, BreakTurningBeforeSwing; the others after it, NotifyWeaponAttackAgainst; both from
/// CharacterController.PerformSingleAttackWithCrit) and on every maneuver
/// (NotifyManeuverAgainst, from CharacterController.ExecuteSpecialAttack). No origin differs from another any more (the
/// PostAttackQuirk bits are gone since step 15).
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
    /// player (the Improved Grab, Cleave target or acid spray prompt). A coroutine caller drives it inline
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

        // Stages 1-4 hold the target-side reactions (a Fire Shield that burns a beetle attacker) until stage 5.
        DeferAttackedReactions();
        try
        {
            // 1. The caller's bookkeeping and log lines.
            res.ReportStep?.Invoke();

            // 2. Thrown drop and ammunition.
            RunPostAttackThrownAndAmmo(res);

            // 3. Concentration, once per damaging hit.
            RunPostAttackConcentration(res);

            // 4. Melee reactions.
            RunPostAttackReactions(res);
        }
        finally
        {
            ResumeAttackedReactions(flush: false);
        }

        // 5. Target-side triggers: the target was attacked (an attack roll was made against it; D1).
        if (AnyResultMade(res))
            NotifyAttackedOrDisturbed(target, attacker);
        IEnumerator reactions = RunPendingAttackedReactions(res.CallerCanWait);
        while (reactions.MoveNext())
            yield return reactions.Current;
        // A reaction can end the combat (the spray drops the last creature of a side): nothing more of this step runs.
        if (CurrentPhase == TurnPhase.CombatOver)
        {
            string endContext = res.CombatEndContext ?? ("OnAttackResolved." + res.Origin);
            outcome.TargetOutOfFight = CombatEndRules.IsOutOfFight(target);
            outcome.AttackerOutOfFight = CombatEndRules.IsOutOfFight(attacker);
            if (outcome.TargetOutOfFight)
                RegisterDefeatedEnemyForXP(target, endContext);
            if (outcome.AttackerOutOfFight)
                RegisterDefeatedEnemyForXP(attacker, endContext);
            outcome.CombatEnded = true;
            FinishAttackResolved(res, outcome);
            yield break;
        }

        // 6. Free trip.
        RunPostAttackFreeTrip(res);

        // 7. Improved Grab.
        IEnumerator grab = RunPostAttackImprovedGrab(res, outcome);
        while (grab.MoveNext())
            yield return grab.Current;

        // 8. Cleave and Great Cleave (PHB p.92, D7).
        IEnumerator cleave = RunPostAttackCleave(res, outcome);
        while (cleave.MoveNext())
            yield return cleave.Current;
        if (outcome.CombatEnded)
        {
            FinishAttackResolved(res, outcome);
            yield break;
        }

        // 9. Summon cleanup (idempotent: a summon already despawned is no longer tracked).
        if (target != null && target.Stats != null && target.Stats.IsDead)
            HandleSummonDeathCleanup(target);
        if (attacker.Stats != null && attacker.Stats.IsDead)
            HandleSummonDeathCleanup(attacker);

        // 10. Combat end for the target and the attacker: either side, dead or only out of the fight (dying, unconscious),
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
        if (res != null)
            res.CallerCanWait = false;
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

    /// <summary>The PC attack paths (B-E; A was retired in batch 13 step 17), whose UI keeps its own field for the weapon in hand.</summary>
    private static bool IsPcAttackOrigin(AttackOrigin origin)
        => origin == AttackOrigin.PcSequence || origin == AttackOrigin.PcFullAttackButton
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

    /// <summary>True when at least one of the step's results was an attack actually made (not refused before the roll).</summary>
    private static bool AnyResultMade(AttackResolution res)
    {
        for (int i = 0; i < res.Results.Count; i++)
            if (res.Results[i] != null && !res.Results[i].NotMade)
                return true;
        return false;
    }

    /// <summary>
    /// Cleave and Great Cleave (PHB p.92; decision D7, <see cref="CleaveRules"/>), for every melee origin, the AoO and the
    /// charge included: when a melee result of the step dropped the target and the attacker may cleave (Great Cleave, or
    /// Cleave unused this round), the attacker's decider picks another creature within its reach with that weapon
    /// (<see cref="CleaveRules.Candidates"/>; the UI attacks a lone foe still in the fight at once and asks otherwise), and the attacker makes one extra attack
    /// against it with the same weapon at the same bonus (CharacterController.ResolveCleaveAttack), no 5-foot step first.
    /// That attack is one more step of the same origin through this hook (<see cref="AttackResolution.IsCleave"/>), so it
    /// can grab, trip and, with Great Cleave, cleave again. A failed ward save loses it (D6). Sets
    /// <see cref="PostAttackOutcome.CombatEnded"/> when a cleave step ended the combat.
    /// </summary>
    private IEnumerator RunPostAttackCleave(AttackResolution res, PostAttackOutcome outcome)
    {
        CharacterController attacker = res.Attacker;
        CharacterController target = res.Target;
        if (attacker == null || attacker.Stats == null || target == null || CombatEndRules.IsOutOfFight(attacker))
            yield break;

        CombatResult dropping = CleaveRules.FirstDropping(res.Results, target);
        if (dropping == null || !CleaveRules.MayCleave(attacker, out bool great))
            yield break;

        // A grappling creature may attack only its grapple opponent, and only with the grapple's own attack (PHB p.156; as
        // the pounce stops ordinary attacks once the charger grapples): no Cleave attack. An Improved Grab hold with one
        // body part is not a grapple for the holder (MM p.310), so a holder may still cleave into another creature.
        if (attacker.IsGrappling() && !attacker.IsHoldingWithImprovedGrab())
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("🪢", $"{attacker.Stats.CharacterName} is grappling: no Cleave attack against another creature (PHB p.156)."));
            yield break;
        }

        // The weapon of the dropping attack: the intent's (the off hand's for an off-hand result), or its natural attack.
        ItemData weapon = dropping.IsOffHandAttack
            ? attacker.GetOffHandAttackWeapon()
            : (res.Intent != null ? res.Intent.Weapon : attacker.GetEquippedMainWeapon());
        NaturalAttackDefinition natural = null;
        if (weapon == null || (res.Intent != null && res.Intent.Hand == AttackStepKind.NaturalSequence))
            natural = attacker.GetNaturalAttackForAttackResult(dropping);
        if (natural != null)
            weapon = null;

        // A natural attack the cleave cannot resolve (a creature holding a weapon, CMB-195) costs nothing and logs nothing.
        if (natural != null && attacker.CleaveNaturalStepIndex(natural) < 0)
        {
            Debug.Log($"[Cleave] {attacker.Stats.CharacterName} dropped {target.Stats.CharacterName} with {natural.Name}, but a natural Cleave attack while holding a weapon is not supported (CMB-195).");
            yield break;
        }
        var cleaveIntent = new AttackIntent
        {
            Mode = AttackMode.Melee,
            Weapon = weapon,
            Hand = natural != null ? AttackStepKind.NaturalSequence : (dropping.IsOffHandAttack ? AttackStepKind.OffHand : AttackStepKind.MainHand),
            Flurry = res.Intent != null && res.Intent.Flurry,
            FightDefensively = res.Intent != null && res.Intent.FightDefensively,
        };

        List<CharacterController> candidates = CleaveRules.Candidates(attacker, target, GetAllCharacters(), cleaveIntent, natural != null);
        if (candidates.Count == 0)
        {
            Debug.Log($"[Cleave] {attacker.Stats.CharacterName} dropped {target.Stats.CharacterName} but perceives no other creature in reach.");
            yield break;
        }

        CharacterController chosen = null;
        IAttackDecider decider = res.Decider ?? AttackDeciders.For(attacker);
        Coroutine wait = decider.ChooseCleaveTarget(this, attacker, candidates, c => chosen = c);
        if (wait != null)
            yield return wait;
        if (chosen == null || !candidates.Contains(chosen) || chosen.Stats == null || chosen.Stats.IsDead || CombatEndRules.IsOutOfFight(attacker))
            yield break;

        // The dropped foe counts as defeated now, even if the cleave ends the combat before stage 10 runs.
        RegisterDefeatedEnemyForXP(target, res.CombatEndContext ?? ("OnAttackResolved." + res.Origin));

        attacker.SetCleaveUsedThisRound(true);
        string feat = great ? "Great Cleave" : "Cleave";
        CombatUI?.ShowCombatLog(CombatLogHelper.Buff("⚔", $" {feat}! {attacker.Stats.CharacterName} drops {target.Stats.CharacterName} and strikes {chosen.Stats.CharacterName}."));

        // The cleave attack is a direct attack: a failed ward save loses it (D6).
        if (AttackLegality.CommitWard(attacker, chosen, natural != null ? DirectAttackKind.Natural : DirectAttackKind.Weapon, cleaveIntent) == WardVerdict.Failed)
            yield break;

        // It is a melee attack, so flanking the new target counts (PHB p.153, CMB-001).
        bool flanking = CombatUtils.IsAttackerFlanking(attacker, chosen, GetAllCharacters(), out CharacterController partner);
        int flankBonus = flanking ? CombatUtils.FlankingAttackBonus : 0;
        string partnerName = partner != null && partner.Stats != null ? partner.Stats.CharacterName : null;
        CombatResult cleaveResult = attacker.ResolveCleaveAttack(chosen, dropping, weapon, natural, flanking, flankBonus, partnerName, out string label);
        if (cleaveResult == null)
            yield break;

        AttackResolution next = AttackResolution.Create(res.Origin, attacker, chosen, cleaveIntent).With(cleaveResult);
        next.Decider = decider;
        next.IsCleave = true;
        next.CallerCanWait = res.CallerCanWait;
        next.CombatEndContext = "Cleave";
        next.ReportStep = () =>
        {
            CombatUI?.ShowCombatLog(cleaveResult.GetAttackBreakdown(feat + ": " + label));
            UpdateAllStatsUI();
        };
        var nextOutcome = new PostAttackOutcome();
        IEnumerator post = OnAttackResolved(next, nextOutcome);
        while (post.MoveNext())
            yield return post.Current;
        if (nextOutcome.CombatEnded)
            outcome.CombatEnded = true;
    }

    /// <summary>
    /// The AI's Cleave target (D7): among the foes in <paramref name="candidates"/> (TeamUtility.IsEnemy; the rules offer
    /// every creature in reach, and the AI never cleaves into a non-foe), the best by the attacker's profile target score
    /// (AIProfile.ScoreTarget) less the unrolled-ward penalty (AttackLegality.WardTargetScoreAdjustment), creatures still in
    /// the fight first; a foe already down only when it is the only kind left and the creature's profile does not ignore
    /// downed targets (no profile: ignored). Null: no cleave.
    /// </summary>
    internal CharacterController ChooseCleaveTargetForAI(CharacterController attacker, IList<CharacterController> candidates)
    {
        if (attacker == null || candidates == null || candidates.Count == 0)
            return null;
        DND35.AI.AIProfile profile = attacker.aiProfile;
        bool anyInFight = false;
        for (int i = 0; i < candidates.Count; i++)
            if (candidates[i] != null && TeamUtility.IsEnemy(attacker, candidates[i]) && !CombatEndRules.IsOutOfFight(candidates[i]))
                anyInFight = true;
        bool ignoreDowned = anyInFight || profile == null || profile.ShouldIgnoreUnconsciousTargets(attacker);

        CharacterController best = null;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < candidates.Count; i++)
        {
            CharacterController c = candidates[i];
            if (c == null || !TeamUtility.IsEnemy(attacker, c) || (ignoreDowned && CombatEndRules.IsOutOfFight(c)))
                continue;
            float score = (profile != null ? profile.ScoreTarget(c, attacker) : 0f) + AttackLegality.WardTargetScoreAdjustment(attacker, c);
            if (score > bestScore)
            {
                bestScore = score;
                best = c;
            }
        }
        return best;
    }

    /// <summary>The menu name of the Cleave target prompt.</summary>
    public const string CleaveTargetMenuName = "CleaveTargetMenu";

    /// <summary>
    /// The PC's Cleave target prompt (D7), shown unless the only candidate is a foe still in the fight
    /// (UiAttackDecider.ChooseCleaveTarget): one option per candidate ("Cleave: Name", marked when it is down or not a
    /// foe), and "No Cleave attack". A menu closed without an answer makes no Cleave attack.
    /// </summary>
    internal IEnumerator PromptCleaveTarget(CharacterController attacker, IList<CharacterController> candidates, Action<CharacterController> onResolved)
    {
        if (attacker == null || candidates == null || candidates.Count == 0 || CombatUI == null)
        {
            onResolved?.Invoke(candidates != null && candidates.Count > 0 ? candidates[0] : null);
            yield break;
        }

        var labels = new List<string>(candidates.Count + 1);
        for (int i = 0; i < candidates.Count; i++)
        {
            CharacterController c = candidates[i];
            string state = (CombatEndRules.IsOutOfFight(c) ? ", down" : "") + (TeamUtility.IsEnemy(attacker, c) ? "" : ", not a foe");
            labels.Add($"Cleave: {c.Stats.CharacterName} ({c.Stats.CurrentHP} hp{state})");
        }
        labels.Add("No Cleave attack");

        bool resolved = false;
        CharacterController chosen = null;
        CombatUI.ShowSpecialStyleSelectionMenu(
            menuName: CleaveTargetMenuName,
            optionLabels: labels,
            optionEnabledStates: null,
            onSelect: index =>
            {
                chosen = index >= 0 && index < candidates.Count ? candidates[index] : null;
                resolved = true;
            },
            onCancel: () =>
            {
                chosen = null;
                resolved = true;
            },
            showWhileActionPanelHidden: true);

        if (!resolved)
            CombatUI.ShowCombatLog(CombatLogHelper.Info("⚔", $"{attacker.Stats.CharacterName} dropped a foe: choose the target of the Cleave attack."));

        while (!resolved && CombatUI != null && CombatUI.IsSpecialStyleSelectionMenuOpen())
            yield return null;

        onResolved?.Invoke(resolved ? chosen : null);
    }
}
