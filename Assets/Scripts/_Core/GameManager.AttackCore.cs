using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The one attack-sequence executor for PCs and NPCs (batch 12 step 17, docs/designs/pc_npc_parity_batch12.md; CMB-091,
/// CMB-045, CMB-096, CMB-173; decision D9). <see cref="RunAttackSequence"/> runs an <see cref="AttackSequenceRequest"/>
/// (the Attack button's click, the Full Attack button, an NPC's attack or full attack, an AoO) step by step, the same
/// stages for every attacker (<see cref="AttackSequenceStage"/>):
/// 1. the decider names the step's target or stops (IAttackDecider.ChooseNextStep; PHB p.143: each attack may go to a
///    new target);
/// 2. one more step of this kind may be committed (CharacterController.CanCommitAttack: the attack ladder, the off-hand
///    count, the actions); the AI's maneuver may then take this melee step's place (CMB-102). A step is a main-hand,
///    natural or off-hand one (batch 12 step 18: an Off-Hand button's step, or the hand a two-weapon request picks, PHB
///    p.160, TwoWeaponStepKind), or a held charge's touch after its casting round (SPL-156: a melee touch in place of a
///    regular attack, whose ward, roll and effect are its delivery cast's; it skips stages 4, 5, 7 and 9);
/// 3. the attacker may attack that target (AttackLegality: the Resilient Sphere, the one-body-part hold bar, a known
///    failed ward save) and it is in reach or range for the step's mode;
/// 4. a ranged or thrown step: a weapon left to throw, the weapon usable (a crossbow loaded, CanAttackWithWeapon), a piece
///    of ammunition for a projectile (PHB p.113), and Rapid Shot declared before the first attack (PHB p.99, D9);
/// 5. the attempt rolls the attacker's one save against the target's Sanctuary or Hide from Undead (WardSaveLedger, D6):
///    a failure loses the step;
/// 6. the step is paid and recorded (CharacterController.TryCommitAttack: the standard action, then the move action for
///    the second step, PHB p.143);
/// 7. a ranged step provokes from each threatening foe (PHB p.141 Table 8-2), before every shot;
/// 8. the roll (CharacterController.ResolveAttackSequenceStep; Haste's extra natural attack by the decider's choice, or
///    the natural attack a PC's natural-attack button picked, AttackSequenceRequest.ChosenNaturalAttackIndex, CMB-200);
/// 9. the post-attack hook (OnAttackResolved).
/// The ranged weapon checks come before the ward's commit, so a shot that cannot be made rolls no ward save and spends
/// nothing (batch 12 step 14's rule for an empty quiver). It stops when the combat ends, the attacker drops or starts
/// grappling (PHB p.156), the decider stops or the steps run out. It commits costs only through the attack sequence; the
/// callers (CombatFlowService.PerformIterativeSequenceAttack, PerformFullAttackWithRetargetingAndFiveFootStep,
/// NPCPerformAttack, ThreatSystem.ExecuteAoO) do the UI continuation.
/// </summary>
public partial class GameManager
{
    /// <summary>The attack-sequence executor (see the class summary). A coroutine caller drives it inline; a synchronous one calls <see cref="RunAttackSequenceSync"/>.</summary>
    internal IEnumerator RunAttackSequence(AttackSequenceRequest req, AttackSequenceOutcome o)
    {
        if (o == null)
            o = new AttackSequenceOutcome();
        CharacterController attacker = req != null ? req.Attacker : null;
        CharacterController firstTarget = req != null ? req.InitialTarget : null;
        o.Aggregate = new FullAttackResult
        {
            Type = FullAttackResult.AttackType.FullAttack,
            Attacker = attacker,
            Defender = firstTarget,
            DefenderHPBefore = firstTarget != null && firstTarget.Stats != null ? firstTarget.Stats.CurrentHP : 0
        };
        if (req == null || attacker == null || attacker.Stats == null || req.Decider == null)
        {
            o.StopReason = "no attacker";
            o.Finished = true;
            yield break;
        }

        bool aoo = req.Kind == AttackSequenceKind.AoO;
        bool heldTouch = req.Kind == AttackSequenceKind.HeldChargeTouch;
        // The attack or pounce at the end of a charge (batch 12 step 21): prepaid steps, no ward commit (the charge's covers them).
        bool charge = req.Kind == AttackSequenceKind.Charge;
        bool forcedOffHand = !aoo && !heldTouch && !charge && req.Intent != null && req.Intent.Hand == AttackStepKind.OffHand;
        var state = new AttackSequenceState { Request = req, Outcome = o, CurrentTarget = firstTarget };
        bool naturalSubstituteUsed = false;

        // Safety cap; the attack sequence itself ends the loop (CanCommitAttack). Sized so maneuvers and retargets (iterations
        // that commit no attack) cannot crowd out a many-headed creature's last steps.
        int cap = aoo || heldTouch ? 1
            : attacker.GetMainHandAttackBudget(AttackStepKind.NaturalSequence) + attacker.GetIterativeAttackCount() + attacker.GetOffHandAttackCount() + 8;
        int iteration;
        for (iteration = 0; iteration < cap; iteration++)
        {
            if (CurrentPhase == TurnPhase.CombatOver)
            {
                o.CombatEnded = true;
                break;
            }
            if (attacker.Stats == null || CombatEndRules.IsOutOfFight(attacker))
            {
                o.AttackerOutOfFight = true;
                o.StopReason = o.StopReason ?? "the attacker is out of the fight";
                break;
            }
            if (!aoo && !attacker.CanTakeTurnActions())
            {
                o.StopReason = o.StopReason ?? "the attacker cannot act";
                break;
            }
            if (state.StepsTaken >= req.MaxSteps)
                break;

            // 1. The step's target, or stop.
            Stage(req, o, AttackSequenceStage.ChooseNextStep);
            AttackStepChoice choice = null;
            Coroutine choosing = req.Decider.ChooseNextStep(this, state, c => choice = c);
            if (choosing != null)
                yield return choosing;
            if (CurrentPhase == TurnPhase.CombatOver)
            {
                o.CombatEnded = true;
                break;
            }
            if (choice == null || choice.Stop || choice.Target == null || choice.Target.Stats == null)
            {
                o.StopReason = o.StopReason ?? (choice != null && !string.IsNullOrEmpty(choice.Reason) ? choice.Reason : "no target");
                break;
            }
            CharacterController target = choice.Target;
            state.CurrentTarget = target;
            // A charge's steps all go to the charge target; once it is dead the charge has nothing left to attack.
            if (charge && target.Stats.IsDead)
            {
                o.StopReason = o.StopReason ?? "the charge's target is dead";
                break;
            }

            // The step's intent: the request's, with the weapon in hand now (a thrown weapon is dropped after each throw and
            // the next one drawn by the post-attack hook) and the step's hand: an off-hand step (an Off-Hand button, or the
            // hand the two-weapon request picks, PHB p.160) uses the off-hand weapon; a held-charge touch is a melee touch
            // made in place of a regular attack (SPL-156), a main-hand step.
            AttackIntent intent = req.Intent != null ? req.Intent.CloneForStep() : AttackIntent.ForMode(AttackMode.Melee);
            AttackStepKind kind;
            bool ranged;
            if (aoo || heldTouch)
            {
                intent.Weapon = aoo ? req.AoOWeapon : null;
                ranged = false;
                kind = AttackStepKind.MainHand;
            }
            else
            {
                ItemData mainWeapon = StepWeapon(req, attacker);
                intent.Weapon = mainWeapon;
                bool offHandStep = forcedOffHand
                    || (req.InterleaveOffHand && !IsAttackModeRanged(attacker, mainWeapon, intent)
                        && TwoWeaponStepKind(attacker) == AttackStepKind.OffHand);
                if (offHandStep)
                {
                    intent.Weapon = forcedOffHand && req.Intent.Weapon != null ? req.Intent.Weapon : attacker.GetOffHandAttackWeapon();
                    if (!forcedOffHand)
                        intent.Mode = AttackMode.Melee;
                }
                ranged = IsAttackModeRanged(attacker, intent.Weapon, intent);
                kind = offHandStep ? AttackStepKind.OffHand : ranged ? AttackStepKind.MainHand : attacker.GetMeleeAttackStepKind();
            }
            if (state.ThrownSequence && kind != AttackStepKind.OffHand && !intent.IsThrowOf(intent.Weapon))
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("↩", $"{attacker.Stats.CharacterName} has no weapon in hand to throw again."));
                o.StopReason = "no weapon left to throw";
                break;
            }
            intent.Hand = kind;

            // 2. One more step of this kind (an AoO is not a step of the attack sequence: ThreatSystem spent the AoO).
            Stage(req, o, AttackSequenceStage.CanCommitAttack);
            if (!aoo && !charge && !attacker.CanCommitAttack(kind, out string cannotCommitReason))
            {
                o.StopReason = cannotCommitReason;
                break;
            }
            // The natural attack the attacker picked for this step (a natural-attack button, batch 13 step 17, CMB-200): it must
            // still be unused, or, picked as Haste's extra attack, Haste's attack must still be unused (PHB p.239; CMB-106).
            // Refused before the step is rolled, paid or recorded (stage 6 pays; a natural-attack button's click is paid there
            // too, CMB-200), so a used natural attack is never made twice.
            bool chosenNatural = !aoo && !heldTouch && !charge && kind == AttackStepKind.NaturalSequence
                && req.ChosenNaturalAttackIndex >= 0 && state.StepsTaken == 0;
            if (chosenNatural && (req.ChosenNaturalAttackIsHasteExtra
                    ? !attacker.CanUseHasteExtraNaturalAttack()
                    : attacker.ProgressiveAttackPool.IsNaturalAttackUsed(req.ChosenNaturalAttackIndex)))
            {
                o.StopReason = req.ChosenNaturalAttackIsHasteExtra ? "no Haste attack left this turn" : "that natural attack is already used this turn";
                break;
            }

            // The AI's maneuver may replace this melee attack (CMB-102), at this step's bonus: the iterative BAB, or the BAB
            // of the natural attack it replaces (PHB p.141 Table 8-2 note 7, MM p.312). In a natural sequence at most one
            // substitute (an AI limit pending AI-035). A declared smite keeps the step an attack (SmiteRules, CHR-020).
            // The step's intent goes with the maneuver (its two-weapon state: the main-hand penalty, PHB p.160; batch 12 step 19).
            bool stepManeuvered = false;
            if (req.TryStepManeuver != null && !ranged && !aoo && !heldTouch && !charge && kind != AttackStepKind.OffHand
                && !(naturalSubstituteUsed && kind == AttackStepKind.NaturalSequence)
                && attacker.Stats.DeclaredSmite == SmiteSource.None)
            {
                _maneuverStepIntent = intent;
                try { stepManeuvered = req.TryStepManeuver(attacker, target); }
                finally { _maneuverStepIntent = null; }
            }
            if (stepManeuvered)
            {
                o.ManeuversUsed++;
                if (kind == AttackStepKind.NaturalSequence)
                    naturalSubstituteUsed = true;
                // A grapple started by a substitute: the remaining steps become grapple actions (the caller hands them on).
                if (attacker.IsGrappling())
                {
                    o.StartedGrapple = true;
                    o.StopReason = "grappling: the remaining attacks become grapple actions (PHB p.156)";
                    break;
                }
                // A failed trip may wait on a controllable defender's counter-trip choice (PHB p.158, CMB-079).
                if (IsAwaitingCounterTripChoice)
                    yield return new WaitWhile(() => IsAwaitingCounterTripChoice);
                // A non-substitute (coup de grace, bull rush) spent the action: the next CanCommitAttack stops the loop.
                continue;
            }

            // 3. May this attacker attack that target, in reach or range for the step's mode?
            Stage(req, o, AttackSequenceStage.CanDirectlyAttack);
            // A held-charge touch: the sphere, the hold bar and the touch's reach (the unarmed reach, PHB p.141); the ward is
            // the delivery's (its cast's own ward commit). Any other step: its weapon's reach or range below.
            Legality legality = heldTouch
                ? AttackLegality.Check(attacker, target, DirectAttackKind.Touch, intent,
                    AttackLegalityChecks.Sphere | AttackLegalityChecks.HoldBar | AttackLegalityChecks.Reach)
                : AttackLegality.Check(attacker, target, aoo ? DirectAttackKind.AoO : DirectAttackKind.Weapon, intent,
                    AttackLegalityChecks.Sphere | AttackLegalityChecks.HoldBar | AttackLegalityChecks.Ward);
            if (legality.Ok && !aoo && !heldTouch && !IsTargetInStepRange(attacker, target, intent))
                legality = Legality.Refused($"{target.Stats.CharacterName} is out of {attacker.Stats.CharacterName}'s reach or range");
            if (!legality.Ok)
            {
                o.StopReason = legality.Reason;
                o.StoppedByLegality = true;
                break;
            }

            // 4. A ranged or thrown step: a weapon to throw, a usable weapon (a loaded crossbow), ammunition; Rapid Shot.
            if (ranged)
            {
                Stage(req, o, AttackSequenceStage.RangedWeaponChecks);
                if (!CanShootWithWeapon(attacker, intent.Weapon, out string cannotShootReason))
                {
                    o.StopReason = cannotShootReason;
                    break;
                }
                if (!state.RangedChecked && kind != AttackStepKind.OffHand)
                {
                    state.RangedChecked = true;
                    state.ThrownSequence = intent.IsThrowOf(intent.Weapon);
                    // The intent's Rapid Shot: the PC toggle, or the AI's choice (AttackIntent.ForAI; AI-055).
                    if (intent.RapidShot)
                        TryDeclareRapidShotForAttack(attacker, intent.Weapon, true, wanted: true);
                }
            }

            // 5. The ward's save (D6): a failure loses the step (it is still spent). A held-charge touch's ward is its cast's;
            // a charge's is the charge's own commit, made once for its attack or pounce (ChargeCore).
            if (!heldTouch && !charge)
                Stage(req, o, AttackSequenceStage.WardCommit);
            if (!heldTouch && !charge && AttackLegality.CommitWard(attacker, target, aoo ? DirectAttackKind.AoO : DirectAttackKind.Weapon, intent) == WardVerdict.Failed)
            {
                if (!aoo && attacker.TryCommitAttack(kind, out _, out _) && kind == AttackStepKind.NaturalSequence)
                {
                    // The picked natural attack is the one lost (CMB-200); else the next one in the executor's order.
                    if (chosenNatural && req.ChosenNaturalAttackIsHasteExtra)
                        attacker.MarkHasteExtraNaturalAttackUsed();
                    else if (chosenNatural)
                        attacker.ProgressiveAttackPool.MarkNaturalAttackUsed(req.ChosenNaturalAttackIndex);
                    else
                        LoseNextNaturalAttack(attacker);
                }
                state.StepsTaken++;
                o.StepsLostToWard++;
                UpdateAllStatsUI();
                continue;
            }

            // 6. Pay and record the step.
            Stage(req, o, AttackSequenceStage.TryCommitAttack);
            int stepIndex = -1;
            int naturalIndex = -1;
            int offHandIndex = attacker.ProgressiveAttackPool.OffHandStepsUsed;
            // A lone attack with the off-hand weapon (the attack action, on a turn that can no longer become a full attack)
            // is no two-weapon fighting: no extra attack, so no Table 8-10 penalty; it keeps the half Strength on damage
            // (PHB p.160, p.134; delegated RAW reading 2026-10-28). Read before the step is recorded.
            bool loneOffHandAttack = kind == AttackStepKind.OffHand && !aoo
                && attacker.ProgressiveAttackPool.Mode == ProgressiveAttackMode.None
                && attacker.ProgressiveAttackPool.OffHandStepsUsed == 0
                && !attacker.CanStillReachFullAttack();
            bool hasteNaturalStep = false;
            if (charge)
            {
                // Prepaid: the charge core spent the charge's action; the step index is the pounce's natural-attack index.
                stepIndex = state.StepsTaken;
            }
            else if (!aoo)
            {
                // The natural attack this step makes: the attacker's pick (checked at stage 2), else the step's own when
                // unused, else the first unused one, else Haste's extra natural attack (CMB-106); read before the step is
                // recorded.
                if (chosenNatural)
                {
                    hasteNaturalStep = req.ChosenNaturalAttackIsHasteExtra;
                    naturalIndex = hasteNaturalStep ? -1 : req.ChosenNaturalAttackIndex;
                }
                else if (kind == AttackStepKind.NaturalSequence && !ResolveNextNaturalAttack(attacker, out naturalIndex, out hasteNaturalStep))
                {
                    o.StopReason = "no natural attack left";
                    break;
                }
                if (!attacker.TryCommitAttack(kind, out stepIndex, out string payReason))
                {
                    o.StopReason = payReason;
                    break;
                }
            }
            state.StepsTaken++;

            // 8 (a held-charge touch). The step is paid; the delivery's cast rolls the touch at this step's BAB (the full BAB
            // for the attack action or a full attack's first step, the iterative BAB of a later one: the touch is made in
            // place of that attack, PHB p.141, p.143), resolves the spell, its PostCast stage keeps the charge on a miss and
            // its CombatEnd stage checks the end of combat (SPL-156).
            if (heldTouch)
            {
                Stage(req, o, AttackSequenceStage.ResolveStep);
                int stepBab = stepIndex > 0 ? attacker.GetMainHandAttackStepBAB(stepIndex) : attacker.Stats.BaseAttackBonus;
                bool delivered = req.DeliverHeldCharge != null && req.DeliverHeldCharge(attacker, target, stepBab);
                if (delivered)
                    o.AttacksMade++;
                else
                    o.StopReason = o.StopReason ?? "the delivery was not made";
                if (CurrentPhase == TurnPhase.CombatOver)
                    o.CombatEnded = true;
                break;
            }

            RangeInfo rangeInfo = aoo ? null : CalculateRangeInfo(attacker, target, intent);
            bool isFlanking = CombatUtils.IsAttackerFlanking(attacker, target, GetAllCharacters(), out CharacterController flankPartner);
            int flankBonus = isFlanking ? CombatUtils.FlankingAttackBonus : 0;
            string partnerName = flankPartner != null && flankPartner.Stats != null ? flankPartner.Stats.CharacterName : null;

            // 7. A ranged step provokes from each threatening foe (PHB p.141 Table 8-2), every shot (CMB-091, CMB-045).
            if (ranged)
            {
                Stage(req, o, AttackSequenceStage.RangedAoO);
                bool survived = _combatFlowService == null || _combatFlowService.ResolveRangedAttackAoOIfProvoked(attacker, rangeInfo, intent);
                if (CurrentPhase == TurnPhase.CombatOver)
                {
                    o.CombatEnded = true;
                    break;
                }
                if (!survived || CombatEndRules.IsOutOfFight(attacker))
                {
                    o.AttackerOutOfFight = true;
                    o.StopReason = "the attacker was dropped by an attack of opportunity";
                    break;
                }
                // An AoO can grab the shooter (Improved Grab, D8): a creature grappling acts only through grapple actions
                // (PHB p.156), so the shot is lost (the step stays spent). An AoO that left the weapon unusable (a pin, a
                // condition) stops it the same way.
                if (attacker.IsGrappling())
                {
                    o.StartedGrapple = true;
                    o.StopReason = "grappled by an attack of opportunity: the shot is lost (PHB p.156)";
                    CombatUI?.ShowCombatLog(CombatLogHelper.Info("🪢", $"{attacker.Stats.CharacterName} is grappled by the attack of opportunity and loses the shot (PHB p.156)."));
                    break;
                }
                if (!attacker.CanAttackWithWeapon(intent.Weapon, out string afterAoOReason))
                {
                    o.StopReason = afterAoOReason;
                    CombatUI?.ShowCombatLog(CombatLogHelper.Info("↩", $"{attacker.Stats.CharacterName} cannot shoot after the attack of opportunity: {afterAoOReason}"));
                    break;
                }
            }

            // 8. The roll.
            Stage(req, o, AttackSequenceStage.ResolveStep);
            var step = new AttackSequenceStep
            {
                Target = target,
                Intent = intent,
                Kind = kind,
                StepIndex = stepIndex,
                NaturalAttackIndex = naturalIndex,
                HasteNaturalStep = hasteNaturalStep,
                IsRanged = ranged,
                RangeInfo = rangeInfo,
                IsFlanking = isFlanking,
                FlankBonus = flankBonus,
                PartnerName = partnerName,
            };
            CombatResult result;
            string label;
            if (aoo)
            {
                result = attacker.Attack(target, isFlanking, flankBonus, partnerName, null, null, req.AoOWeapon, req.AoOAttackModifier);
                label = "Attack of opportunity";
            }
            else if (charge)
            {
                result = ResolveChargeStep(attacker, target, req, stepIndex, isFlanking, flankBonus, partnerName, out label);
                if (result == null)
                {
                    o.StopReason = req.ChargePounce ? "no natural attack left" : "the step made no attack";
                    break;
                }
            }
            else
            {
                int resolveIndex = stepIndex;
                int hasteChoice = -1;
                if (kind == AttackStepKind.NaturalSequence)
                {
                    resolveIndex = hasteNaturalStep ? attacker.GetHasteExtraNaturalStepIndex() : naturalIndex;
                    if (hasteNaturalStep)
                    {
                        // Haste's extra natural attack: the attacker picks the natural weapon (PHB p.239; CMB-106, CMB-124),
                        // already picked by a natural-attack button (CMB-200).
                        int picked = chosenNatural ? req.ChosenNaturalAttackIndex : -1;
                        if (!chosenNatural)
                        {
                            Coroutine hastePrompt = req.Decider.ChooseHasteNaturalAttack(this, attacker, target, req.Profile, 0, c => picked = c);
                            if (hastePrompt != null)
                                yield return hastePrompt;
                        }
                        hasteChoice = picked >= 0 ? picked : attacker.GetDefaultHasteNaturalAttackIndex();
                        step.NaturalAttackIndex = hasteChoice;
                        if (target.Stats == null || target.Stats.IsDead || CombatEndRules.IsOutOfFight(attacker))
                        {
                            o.StopReason = "the Haste attack's target or attacker is gone";
                            break;
                        }
                    }
                }
                result = kind == AttackStepKind.OffHand
                    ? attacker.ResolveOffHandAttackStep(target, offHandIndex, isFlanking, flankBonus, partnerName, rangeInfo, intent.Weapon, out label,
                        twoWeaponPenalty: !loneOffHandAttack)
                    : attacker.ResolveAttackSequenceStep(target, kind, resolveIndex, isFlanking, flankBonus, partnerName, rangeInfo,
                        intent.Weapon, kind == AttackStepKind.MainHand ? req.MainHandBabAdjustment : 0, out label, hasteChoice);
            }

            if (result == null)
            {
                o.StopReason = "the step made no attack";
                break;
            }

            if (string.IsNullOrEmpty(label))
                label = kind == AttackStepKind.OffHand ? $"Off hand {offHandIndex + 1}" : $"Attack {stepIndex + 1}";
            step.Result = result;
            step.Label = label;
            o.AttacksMade++;
            o.LastResult = result;
            o.LastStep = step;
            o.Aggregate.Attacks.Add(result);
            o.Aggregate.AttackLabels.Add(label);
            if (aoo)
                req.OnAoOResolved?.Invoke(result);

            // 9. The post-attack hook: the caller's log lines, the thrown drop and ammunition, Concentration, reactions, the
            // free trip, Improved Grab, Cleave, the summon cleanup and the combat-end check.
            Stage(req, o, AttackSequenceStage.OnAttackResolved);
            AttackOrigin origin = req.Origin;
            if (req.Kind == AttackSequenceKind.AI)
                origin = !ranged ? AttackOrigin.NpcMelee
                    : attacker.ProgressiveAttackPool.IsFullAttack ? AttackOrigin.NpcRangedFull : AttackOrigin.NpcRangedSingle;
            AttackResolution resolution = AttackResolution.Create(origin, attacker, target, intent).With(result);
            resolution.Decider = req.Decider;
            resolution.RangeInfo = rangeInfo;
            resolution.CombatEndContext = req.CombatEndContext;
            resolution.CallerCanWait = req.CallerCanWait;
            AttackSequenceStep reported = step;
            resolution.ReportStep = () =>
            {
                if (req.ReportStep != null)
                {
                    req.ReportStep(reported);
                }
                else
                {
                    CombatUI?.ShowCombatLog(reported.Result.GetAttackBreakdown(reported.Label));
                    UpdateAllStatsUI();
                }
            };
            var post = new PostAttackOutcome();
            IEnumerator hook = OnAttackResolved(resolution, post);
            while (hook.MoveNext())
                yield return hook.Current;
            state.PreviousStep = step;

            if (post.CombatEnded || CurrentPhase == TurnPhase.CombatOver)
            {
                o.CombatEnded = true;
                break;
            }
            if (post.AttackerOutOfFight)
            {
                o.AttackerOutOfFight = true;
                o.StopReason = "the attacker is out of the fight";
                break;
            }
            // A creature grappling acts only through grapple actions (PHB p.156; CMB-149): once its Improved Grab takes hold the
            // remaining steps become grapple actions. A hold with one body part (MM p.310) leaves it ungrappled.
            if (!aoo && attacker.IsGrappling())
            {
                o.StartedGrapple = true;
                o.StopReason = "grappling: the remaining attacks become grapple actions (PHB p.156)";
                break;
            }
        }

        if (iteration >= cap && !aoo)
            Debug.LogWarning($"[AttackSequence] {attacker.Stats?.CharacterName} hit the safety cap of {cap} iterations.");

        // A pounce's rakes (MM p.313: no grapple needed; delegated RAW reading 2026-10-14, CMB-144), after its natural attacks.
        if (charge && req.ChargePounce && !o.CombatEnded && CurrentPhase != TurnPhase.CombatOver)
            ResolvePounceRakes(attacker, firstTarget, o);

        CharacterController defender = o.Aggregate.Defender;
        o.Aggregate.DefenderHPAfter = defender != null && defender.Stats != null ? defender.Stats.CurrentHP : o.Aggregate.DefenderHPBefore;
        o.Aggregate.TargetKilled = defender != null && defender.Stats != null && defender.Stats.IsDead;
        o.RapidShotDeclared = attacker.IsRapidShotDeclared;
        o.Finished = true;
    }

    /// <summary>
    /// <see cref="RunAttackSequence"/> for a synchronous caller: runs it inline and returns false when it finished. When it
    /// waits (a player's prompt, a counter-trip answer) it returns true and the rest runs as a coroutine.
    /// <paramref name="onDone"/> is called when it ends, at once or later.
    /// </summary>
    internal bool RunAttackSequenceSync(AttackSequenceRequest req, AttackSequenceOutcome o, Action onDone = null)
    {
        IEnumerator run = RunAttackSequence(req, o);
        while (run.MoveNext())
        {
            object wait = run.Current;
            if (wait == null)
                continue;
            if (o != null)
                o.Suspended = true;
            StartCoroutine(ContinueAttackResolved(run, wait, onDone));
            return true;
        }
        onDone?.Invoke();
        return false;
    }

    private static void Stage(AttackSequenceRequest req, AttackSequenceOutcome o, AttackSequenceStage stage)
    {
        o.Stages.Add(stage);
        req.StageObserver?.Invoke(stage);
    }

    /// <summary>
    /// The weapon a step uses: the main weapon in hand now; for a PC throw, the weapon the UI holds ready to throw
    /// (GameManager._equippedWeapon, refreshed from the inventory after each throw), else the main weapon.
    /// </summary>
    private ItemData StepWeapon(AttackSequenceRequest req, CharacterController attacker)
    {
        ItemData main = attacker.GetEquippedMainWeapon();
        bool uiThrow = (req.Kind == AttackSequenceKind.UiAttack || req.Kind == AttackSequenceKind.UiFullAttack)
            && req.Intent != null && req.Intent.Mode == AttackMode.Thrown;
        return uiThrow ? (_equippedWeapon ?? main) : main;
    }

    /// <summary>
    /// The hand of the next step of a two-weapon attack (batch 12 step 18, PHB p.160: the extra attacks with the off-hand
    /// weapon come in any order with the main hand's): the hand whose next attack has the higher BAB (the ladders' BAB,
    /// before the two-weapon penalties), the main hand on a tie, while that hand has an attack left this turn (CanCommitAttack: the main-hand ladder, the off-hand count). The
    /// main hand when neither has one (the executor then stops at CanCommitAttack).
    /// </summary>
    internal static AttackStepKind TwoWeaponStepKind(CharacterController attacker)
    {
        if (attacker == null || attacker.Stats == null)
            return AttackStepKind.MainHand;
        bool offOk = attacker.CanCommitAttack(AttackStepKind.OffHand, out _);
        if (!offOk)
            return AttackStepKind.MainHand;
        bool mainOk = attacker.CanCommitAttack(AttackStepKind.MainHand, out _);
        if (!mainOk)
            return AttackStepKind.OffHand;
        int mainBab = attacker.GetMainHandAttackStepBAB(attacker.ProgressiveAttackPool.MainHandStepsUsed);
        return attacker.GetNextOffHandAttackBAB() > mainBab ? AttackStepKind.OffHand : AttackStepKind.MainHand;
    }

    /// <summary>
    /// The kind of the next step <paramref name="req"/> would make (the deciders' count of what is left): an off-hand step for
    /// an Off-Hand button, the two-weapon request's pick (<see cref="TwoWeaponStepKind"/>), a main-hand step for a ranged
    /// attack or a held-charge touch, else the creature's melee kind (an iterative or natural step).
    /// </summary>
    internal static AttackStepKind NextStepKind(AttackSequenceRequest req, CharacterController attacker, bool ranged)
    {
        if (req != null && req.Kind == AttackSequenceKind.HeldChargeTouch)
            return AttackStepKind.MainHand;
        if (req != null && req.Intent != null && req.Intent.Hand == AttackStepKind.OffHand)
            return AttackStepKind.OffHand;
        if (ranged)
            return AttackStepKind.MainHand;
        if (req != null && req.InterleaveOffHand)
            return TwoWeaponStepKind(attacker);
        return attacker != null ? attacker.GetMeleeAttackStepKind() : AttackStepKind.MainHand;
    }

    /// <summary>
    /// The attack steps <paramref name="req"/> has left this turn: the main-hand (or natural) steps, plus the off-hand ones
    /// for a two-weapon request (PHB p.160), or the off-hand ones alone for an Off-Hand button.
    /// </summary>
    internal static int RemainingSteps(AttackSequenceRequest req, CharacterController attacker, bool ranged)
    {
        if (attacker == null)
            return 0;
        if (req != null && req.Intent != null && req.Intent.Hand == AttackStepKind.OffHand)
            return attacker.GetRemainingOffHandAttackSteps();
        AttackStepKind kind = ranged ? AttackStepKind.MainHand : attacker.GetMeleeAttackStepKind();
        int left = attacker.GetRemainingMainHandAttackSteps(kind);
        if (req != null && req.InterleaveOffHand && !ranged)
        {
            left += attacker.GetRemainingOffHandAttackSteps();
            // A turn that cannot become a full attack has one attack, whichever hand makes it (PHB p.143).
            if (!attacker.CanStillReachFullAttack())
                left = Mathf.Min(left, 1);
        }
        return left;
    }

    /// <summary>Reach or range for the step's mode: a throw's thrown range, else the weapon's reach or range (melee reach, a projectile's increments).</summary>
    internal static bool IsTargetInStepRange(CharacterController attacker, CharacterController target, AttackIntent intent)
    {
        if (attacker == null || target == null || target.Stats == null || target.Stats.IsDead)
            return false;
        if (IsUsingThrownAttackMode(attacker, intent != null ? intent.Weapon : null, intent))
            return attacker.IsTargetInThrownWeaponRange(target, intent.Weapon);
        return attacker.IsTargetInCurrentWeaponRange(target);
    }

    /// <summary>
    /// The natural attack the next natural step makes (CMB-106, CMB-127): the one at the step's place in the sequence when it
    /// is unused this turn, else the first unused one (a natural-attack button or a grapple attack used some out of order),
    /// else Haste's extra natural attack while it is unused. False when none is left.
    /// </summary>
    private static bool ResolveNextNaturalAttack(CharacterController attacker, out int naturalIndex, out bool hasteStep)
    {
        naturalIndex = -1;
        hasteStep = false;
        if (attacker == null || attacker.Stats == null)
            return false;

        AttackPool pool = attacker.ProgressiveAttackPool;
        int count = attacker.Stats.GetTotalNaturalAttackCount();
        int preferred = pool.MainHandStepsUsed;
        if (preferred >= 0 && preferred < count && !pool.IsNaturalAttackUsed(preferred))
        {
            naturalIndex = preferred;
            return true;
        }
        for (int i = 0; i < count; i++)
        {
            if (!pool.IsNaturalAttackUsed(i))
            {
                naturalIndex = i;
                return true;
            }
        }
        if (attacker.CanUseHasteExtraNaturalAttack())
        {
            hasteStep = true;
            return true;
        }
        return false;
    }

    /// <summary>A natural step lost to a ward (D6) uses up the natural attack it would have made, so a later step does not repeat it.</summary>
    internal static void LoseNextNaturalAttack(CharacterController attacker)
    {
        AttackPool pool = attacker.ProgressiveAttackPool;
        int count = attacker.Stats != null ? attacker.Stats.GetTotalNaturalAttackCount() : 0;
        for (int i = 0; i < count; i++)
        {
            if (!pool.IsNaturalAttackUsed(i))
            {
                pool.MarkNaturalAttackUsed(i);
                return;
            }
        }
        if (attacker.CanUseHasteExtraNaturalAttack())
            attacker.MarkHasteExtraNaturalAttackUsed();
    }

    /// <summary>
    /// The ranged weapon checks of stage 4, one rule for every caller (the executor, the Attack button's
    /// StartAttackSequence, the Full Attack button and <see cref="TryDeclareRapidShotForAttack"/>): the weapon is usable now
    /// (CharacterController.CanAttackWithWeapon: a loaded crossbow, the grapple's light-weapon limit) and a projectile
    /// weapon has a piece of ammunition (PHB p.113, CombatFlowService.CheckAmmoAvailable). A refusal is logged once here,
    /// so a shot that cannot be made spends nothing (batch 12 step 14's rule).
    /// </summary>
    internal bool CanShootWithWeapon(CharacterController attacker, ItemData weapon, out string reason)
    {
        reason = string.Empty;
        if (attacker == null || attacker.Stats == null)
        {
            reason = "no attacker";
            return false;
        }
        if (!attacker.CanAttackWithWeapon(weapon, out reason))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("↩", $"{attacker.Stats.CharacterName} cannot attack: {reason}"));
            return false;
        }
        if (weapon != null && weapon.IsProjectileWeapon && _combatFlowService != null
            && !_combatFlowService.CheckAmmoAvailable(attacker, weapon))
        {
            reason = "no ammunition";
            return false;
        }
        return true;
    }

    /// <summary>
    /// Rapid Shot declared before the first ranged attack of the turn (PHB p.99; decision D9) when the attacker wants it
    /// (<paramref name="wanted"/>: the attack intent's choice, the AI's by its profile since batch 12 step 18, AI-055; null
    /// reads the attacker's toggle, CharacterController.RapidShotEnabled): it commits the turn to the full attack and adds the extra attack, and every attack of the round takes -2. When it
    /// cannot be declared any more (an attack was made, the move action is spent, the creature is slowed) the attack is made
    /// without it, and the log says why. One rule for the Attack button (GameManager.StartAttackSequence, which pays the
    /// first step before the target click), the Full Attack button and the NPC sequence (RunAttackSequence stage 4). The
    /// Fighting Defensively (Std) and natural-attack buttons are the attack action and never declare it (their click's
    /// request clears intent.RapidShot; GameManager.BeginAttackSequenceAtClick; batch 13 step 17).
    /// </summary>
    internal bool TryDeclareRapidShotForAttack(CharacterController attacker, ItemData weapon, bool ranged, bool? wanted = null)
    {
        bool want = wanted ?? (attacker != null && attacker.RapidShotEnabled);
        if (attacker == null || attacker.Stats == null || !want || attacker.IsRapidShotDeclared)
            return attacker != null && attacker.IsRapidShotDeclared;
        if (!attacker.Stats.HasFeat("Rapid Shot") || !ranged)
            return false;
        // After the turn's first attack the choice is gone (PHB p.143): nothing to declare, nothing to log.
        if (attacker.ProgressiveAttackPool.AttacksCommitted > 0 || attacker.ProgressiveAttackPool.Mode == ProgressiveAttackMode.StandardAttackCommitted)
            return false;
        // A shot that cannot be made declares nothing and spends nothing (stage 4's order, for every caller).
        if (!CanShootWithWeapon(attacker, weapon, out _))
            return false;

        if (attacker.TryDeclareRapidShot(weapon, ranged, out string reason))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("🏹",
                $"{attacker.Stats.CharacterName} uses Rapid Shot: a full attack with one extra shot, -2 on every attack this round (PHB p.99)."));
            return true;
        }

        CombatUI?.ShowCombatLog(CombatLogHelper.Info("🏹", $"{attacker.Stats.CharacterName} attacks without Rapid Shot: {reason}."));
        return false;
    }
}
