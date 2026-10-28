using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DND35.Magic;
using DND35e.Identifiers;
using UnityEngine;

/// <summary>
/// GameManager partial class: NPC Turn Execution
/// 
/// Contains all NPC/AI turn execution logic:
/// - SingleNPCTurnFromInitiative: Main NPC turn coroutine
/// - AI_SummonedCreature: Summoned creature turn execution
/// - NPC attack execution and full attack with adaptive retargeting
/// - NPC spell casting
/// - Special attack evaluation for AI
/// - Improved Grab and free trip resolution
/// - Bombardier beetle acid spray
/// - Last-known-position auto-miss handling
/// - Attack logging and mode reset utilities
/// 
/// Extracted from main GameManager.cs to reduce file size.
/// </summary>
public partial class GameManager
{
    // ═══════════════════════════════════════════════════════════════════
    //  NPC TURN EXECUTION
    // ═══════════════════════════════════════════════════════════════════


    /// <summary>
    /// Execute a single NPC turn triggered by the initiative system.
    /// </summary>
    private IEnumerator SingleNPCTurnFromInitiative(CharacterController npc)
    {
        // A combat that ended before this turn began (for example by a tick at the round boundary) stays ended.
        if (CurrentPhase == TurnPhase.CombatOver)
            yield break;

        CurrentPhase = TurnPhase.NPCTurn;
        CombatUI.SetActivePC(0); // No PC active
        CombatUI.SetActiveNPC(NPCs.IndexOf(npc)); // Highlight active NPC
        CombatUI.SetActionButtonsVisible(false);
        CombatUI.HideSummonContextMenu();

        // Update initiative UI to highlight current NPC
        UpdateInitiativeUI();

        // The turn-start effects, resets and combat-end check (BeginCombatantTurn, GameManager.TurnStart.cs) already ran in
        // OnTurnStarted, for this NPC as for a PC (D3, CMB-075, CORE-003).

        if (ShouldSkipTurnDueToHPState(npc))
        {
            CombatUI.SetActiveNPC(-1); // Clear NPC highlight
            if (npc != null && npc.Stats != null)
            {
                string reason = GetUnableToActReason(npc);
                CombatUI?.ShowCombatLog(CombatLogHelper.Failure("", $"⏭ {npc.Stats.CharacterName} {reason} and cannot act this turn."));
            }
            MaintainConcentrationOnSkippedTurn(npc); // a pinned or paralyzed caster still concentrates (ConcentrationRules)
            NextInitiativeTurn();
            yield break;
        }

        // The perception check, the Turn Undead proximity check and the compulsion pass of a creature that acts
        // (GameManager.TurnStart.cs), as for a PC (CMB-094, batch 12 step 8): an NPC's compulsions are stored on
        // CurrentTurnStart and run in its AI turn (batch 12 step 7).
        BeginActingTurn(npc);

        // Determine AI behavior for this NPC
        NPCAIBehavior behavior = GetNPCBehaviorForAI(npc);
        if (_aiService != null)
            yield return StartCoroutine(_aiService.ExecuteNPCTurn(npc, behavior));

        // Victory or defeat after the NPC turn, whoever this NPC is and whichever side it dropped (CORE-011).
        if (EvaluateCombatEnd("SingleNPCTurnFromInitiative"))
            yield break;

        // Advance to next in initiative
        NextInitiativeTurn();
    }

    private IEnumerator AI_SummonedCreature(CharacterController summon)
    {
        ActiveSummonInstance data = GetActiveSummon(summon);
        if (data == null)
            yield break;

        // ── Death/disable check at summon turn start ──
        if (summon.Stats != null && !summon.CanTakeTurnActions())
        {
            Debug.Log($"🔥 [AI] {summon.Stats.CharacterName} cannot act (HP={summon.Stats.CurrentHP}) at summon turn start — turn ended");
            yield break;
        }

        if (data.CurrentCommand != null && data.CurrentCommand.Type == SummonCommandType.ProtectCaster && data.Caster != null && data.Caster.Stats != null)
        {
            CombatUI.ShowCombatLog(CombatLogHelper.SummonRaw($"{GetSummonDisplayName(summon)} protects {data.Caster.Stats.CharacterName}."));
        }

        CharacterController target = SelectSummonTargetByCommand(summon, data);
        if (target == null)
            yield break;

        bool lowHP = summon.Stats != null && summon.Stats.TotalMaxHP > 0 && summon.Stats.CurrentHP <= Mathf.CeilToInt(summon.Stats.TotalMaxHP * 0.30f);

        if (lowHP && _aiService != null && summon.Actions.HasMoveAction)
        {
            SquareCell retreat = _aiService.EvaluateMovementOptions(summon, target.GridPosition, retreat: true);
            if (retreat != null && retreat.Coords != summon.GridPosition)
            {
                yield return StartCoroutine(MoveCharacterAlongComputedPath(summon, retreat.Coords, PlayerMoveSecondsPerStep));

                // ── Death/disable check after retreat movement ──
                if (!summon.CanTakeTurnActions())
                {
                    Debug.Log($"🔥 [AI] {summon.Stats.CharacterName} dropped during retreat (HP={summon.Stats.CurrentHP}) — turn ended");
                    yield break;
                }

                if (summon.Actions.HasMoveAction)
                    summon.Actions.UseMoveAction();
                CombatUI.ShowCombatLog(CombatLogHelper.Buff("", $"{GetSummonDisplayName(summon)} withdraws to survive."));
                yield return new WaitForSeconds(0.45f);
            }
        }

        if (!summon.IsTargetInCurrentWeaponRange(target) && summon.Actions.HasMoveAction && _aiService != null)
        {
            SquareCell bestCell = _aiService.EvaluateMovementOptions(summon, target.GridPosition, retreat: false, target);
            if (bestCell != null)
            {
                yield return StartCoroutine(MoveCharacterAlongComputedPath(summon, bestCell.Coords, PlayerMoveSecondsPerStep));

                // ── Death/disable check after advance movement ──
                if (!summon.CanTakeTurnActions())
                {
                    Debug.Log($"🔥 [AI] {summon.Stats.CharacterName} dropped during advance (HP={summon.Stats.CurrentHP}) — turn ended");
                    yield break;
                }

                summon.Actions.UseMoveAction();
                CombatUI.ShowCombatLog(CombatLogHelper.SummonRaw($"{GetSummonDisplayName(summon)} closes in on {target.Stats.CharacterName}."));
                yield return new WaitForSeconds(0.4f);
            }
        }

        // ── Death/disable re-check before attack phase ──
        if (!summon.CanTakeTurnActions())
        {
            Debug.Log($"🔥 [AI] {summon.Stats.CharacterName} cannot act before summon attack phase (HP={summon.Stats.CurrentHP}) — turn ended");
            yield break;
        }

        target = SelectSummonTargetByCommand(summon, data);
        if (target == null)
            yield break;

        if (!summon.IsTargetInCurrentWeaponRange(target) || target.Stats.IsDead)
            yield break;

        // A templated summon's smite is declared by the shared NPC attack sequence below (NPCPerformAttack ->
        // TryNPCDeclareSmite; SmiteRules, CHR-020, AI-056), as for every other creature with a smite.

        // A trip taken as an action (not the free trip after a bite) replaces one melee attack
        // (PHB p.141 Table 8-2 note 7, p.158). It runs as a step of the shared NPC melee sequence
        // (CMB-102): the same step BAB and initiation AoOs (CMB-014, CMB-076) as any NPC
        // substitute, and the summon's remaining steps attack afterwards.
        Func<CharacterController, CharacterController, bool> summonTripStep =
            (actor, stepTarget) => actor.Stats != null && actor.Stats.HasTripAttack
                && stepTarget != null && stepTarget.Stats != null && !stepTarget.Stats.IsProne
                && TryNPCSpecialAttackByTypeForAI(actor, stepTarget, SpecialAttackType.Trip);

        yield return StartCoroutine(NPCPerformAttack(summon, target, summonTripStep));
    }

    /// <summary>Delegates to AIService.SelectSummonTarget for centralized summon targeting.</summary>
    private CharacterController SelectSummonTargetByCommand(CharacterController summon, ActiveSummonInstance summonData)
    {
        SummonCommandType cmd = summonData != null && summonData.CurrentCommand != null
            ? summonData.CurrentCommand.Type
            : SummonCommandType.AttackNearest;
        CharacterController caster = summonData != null ? summonData.Caster : null;
        return AIService.SelectSummonTarget(summon, caster, cmd, GetAllCharacters());
    }

    /// <summary>
    /// The Trip (Ex) free trip after a hit, with its aftermath (the Improved Trip attack; no counter-trip). Used by
    /// the PC attack buttons, the NPC attack sequence, charges, pounces and smites; an AoO calls
    /// <see cref="ResolveFreeTripAfterHit"/> and runs the aftermath after its own log (ThreatSystem.ExecuteAoO).
    /// </summary>
    private void TryResolveFreeTripOnHit(CharacterController attacker, CharacterController target, CombatResult attackResult, RangeInfo attackRange)
    {
        SpecialAttackResult tripResult = ResolveFreeTripAfterHit(attacker, target, attackResult, attackRange);

        // The Improved Trip attack after a free trip that landed (PHB p.96, CMB-079); no counter-trip.
        if (tripResult != null)
            HandleTripAftermath(attacker, target, tripResult, null);
    }

    /// <summary>
    /// The one rule for the Trip (Ex) free trip after a hit, for PCs and NPCs on any attack path (turns, charges,
    /// pounces, smites, AoOs): only a melee hit with the trigger attack the creature's MM entry names (CMB-125), on a
    /// living, standing target the trip rules allow (CanTrip); no touch attack, no AoO and no counter-trip (MM Trip
    /// (Ex), CMB-014). Writes the combat log line and runs the trip's melee reactions. Returns the trip's result, or
    /// null when no trip was attempted; the caller runs <see cref="HandleTripAftermath"/>.
    /// </summary>
    internal SpecialAttackResult ResolveFreeTripAfterHit(CharacterController attacker, CharacterController target, CombatResult attackResult, RangeInfo attackRange)
    {
        if (attacker == null || target == null || attacker.Stats == null || target.Stats == null || attackResult == null)
            return null;

        // MM Trip (Ex): only a hit with the attack the creature's entry names (its bite; the cheetah's claw or bite)
        // allows the free trip, not a weapon, unarmed or other natural attack hit (CMB-125).
        if (!attacker.Stats.IsTripTriggerAttack(attackResult.WeaponName))
            return null;

        bool isMeleeHit = attackRange != null
            ? attackRange.IsMelee
            : !attackResult.IsRangedAttack;
        if (!isMeleeHit)
            return null;

        if (!attackResult.Hit || attacker.IsDead || attacker.Stats.IsDead
            || target.Stats.IsDead || target.HasCondition(CombatConditionType.Prone))
            return null;

        // The trip size limit and the other trip rules apply to a free trip too (PHB p.158, CMB-079).
        if (!attacker.CanTrip(target, out string cannotTripReason))
        {
            Debug.Log($"[NPC Trip Follow-up] {attacker.Stats.CharacterName} makes no free trip: {cannotTripReason}.");
            return null;
        }

        // MM trip (Ex): no touch attack and no AoO (CMB-014); the opponent cannot trip back.
        SpecialAttackResult tripResult = attacker.ResolveFreeTripAttempt(target, attackResult);
        string tripContext = tripResult.Success
            ? "free trip follow-up"
            : "free trip attempt failed";

        CombatUI?.ShowCombatLog(CombatLogHelper.Death("☠", $"{attacker.Stats.CharacterName} follows up with Trip ({tripContext}): {tripResult.Log}"));
        Debug.Log($"[NPC Trip Follow-up] {attacker.Stats.CharacterName} triggered free trip after hit{(attackResult.IsAttackOfOpportunity ? " (AoO)" : "")}. Success={tripResult.Success}");

        // Melee reaction effects (Fire Shield, Thorns, etc.) — free trip follow-up is a melee maneuver
        MeleeReactionService.TriggerReactions(attacker, target, null);

        return tripResult;
    }

    private void TryResolveFreeTripFromAttackResults(CharacterController attacker, CharacterController target, List<CombatResult> attacks, RangeInfo attackRange)
    {
        if (attacker == null || target == null || attacks == null || attacks.Count == 0)
            return;

        for (int i = 0; i < attacks.Count; i++)
        {
            CombatResult attackResult = attacks[i];
            TryResolveFreeTripOnHit(attacker, target, attackResult, attackRange);

            if (target.Stats == null || target.Stats.IsDead || target.HasCondition(CombatConditionType.Prone))
                break;
        }
    }

    private bool TryNPCSpecialAttackIfBeneficial(CharacterController npc, CharacterController target)
    {
        return TryNPCSpecialAttackIfBeneficial(npc, target, null, out _, out _);
    }

    private bool TryNPCSpecialAttackIfBeneficial(CharacterController npc, CharacterController target, SpecialAttackType? forcedChoice)
    {
        return TryNPCSpecialAttackIfBeneficial(npc, target, forcedChoice, out _, out _);
    }

    /// <summary>
    /// True when the NPC may coup de grace now: its profile (or override) allows it, a helpless enemy
    /// is adjacent and the full-round action is free.
    /// </summary>
    private bool HasNPCCoupDeGraceOption(CharacterController npc, List<CharacterController> coupTargets)
    {
        bool profileAllowsCoupDeGrace = npc.EnemyUseCoupDeGraceOverride
            ?? (npc.aiProfile != null && npc.aiProfile.ShouldUseCoupDeGrace(npc));
        return profileAllowsCoupDeGrace
            && coupTargets != null
            && coupTargets.Count > 0
            && npc.Actions != null
            && npc.Actions.HasFullRoundAction;
    }

    /// <summary>
    /// The maneuver the legacy chooser (an NPC with no AI profile) picks against
    /// <paramref name="target"/>: coup de grace, else trip, else disarm (STR mod 3+), else grapple
    /// (STR mod 4+, not with Improved Grab); null when none applies. A choice only: whether it can be
    /// paid for now is checked by the executor (<c>TryNPCSpecialAttackIfBeneficial</c>).
    /// </summary>
    private SpecialAttackType? ChooseNPCFallbackManeuver(CharacterController npc, CharacterController target, bool hasCoupOption)
    {
        if (npc == null || npc.Stats == null || target == null || target.Stats == null)
            return null;

        if (hasCoupOption)
            return SpecialAttackType.CoupDeGrace;

        if (!target.Stats.IsProne
            && npc.HasMeleeWeaponEquipped()
            && npc.CanPerformSpecialAttack(SpecialAttackType.Trip)
            && npc.CanTrip(target, out _))
            return SpecialAttackType.Trip;

        if (target.GetEquippedMainWeapon() != null
            && npc.Stats.STRMod >= 3
            && npc.CanPerformSpecialAttack(SpecialAttackType.Disarm))
            return SpecialAttackType.Disarm;

        if (npc.Stats.STRMod >= 4
            && !npc.Stats.HasImprovedGrab
            && npc.CanPerformSpecialAttack(SpecialAttackType.Grapple))
            return SpecialAttackType.Grapple;

        return null;
    }

    /// <summary>The legacy chooser's pick (<see cref="ChooseNPCFallbackManeuver"/>) without acting, for the AI's stopgap check (AI-060).</summary>
    private SpecialAttackType? PeekNPCFallbackManeuver(CharacterController npc, CharacterController target)
    {
        if (npc == null || target == null)
            return null;

        return ChooseNPCFallbackManeuver(npc, target, HasNPCCoupDeGraceOption(npc, GetAdjacentHelplessEnemiesForCoupDeGrace(npc)));
    }

    /// <summary>
    /// The NPC maneuver executor. Returns true when it acted (the maneuver's step or action was spent);
    /// then <paramref name="attempted"/> is the maneuver type and <paramref name="succeeded"/> its
    /// result (false also when an initiation AoO foiled the attempt). Both outputs only report: the AI's
    /// stopgap maneuver memory (AIService, AI-060) reads them; no rule does.
    /// </summary>
    private bool TryNPCSpecialAttackIfBeneficial(
        CharacterController npc,
        CharacterController target,
        SpecialAttackType? forcedChoice,
        out SpecialAttackType? attempted,
        out bool succeeded)
    {
        attempted = null;
        succeeded = false;

        if (npc == null || target == null)
            return false;

        var coupTargets = GetAdjacentHelplessEnemiesForCoupDeGrace(npc);
        bool hasCoupOption = HasNPCCoupDeGraceOption(npc, coupTargets);

        if (npc.IsGrappling() && (!forcedChoice.HasValue || forcedChoice.Value != SpecialAttackType.CoupDeGrace))
            return false;

        // A maneuver that replaces an attack only needs the next attack step, iterative or natural
        // (CMB-102); the exact cost per type is checked once the choice is known.
        if (!npc.Actions.HasStandardAction && !hasCoupOption && !npc.CanCommitAttack(npc.GetManeuverSubstituteStepKind(), out _))
            return false;

        SpecialAttackType? choice = forcedChoice;

        if (choice.HasValue && !npc.CanPerformSpecialAttack(choice.Value))
        {
            string npcName = npc.Stats != null ? npc.Stats.CharacterName : "<unknown>";
            Debug.Log($"[AI][SpecialAttack] {npcName} cannot perform forced {choice.Value} while in {npc.GetPrimaryWeaponType()} mode.");
            return false;
        }

        if (!choice.HasValue)
            choice = ChooseNPCFallbackManeuver(npc, target, hasCoupOption);

        if (choice == null)
            return false;

        if (choice.Value == SpecialAttackType.CoupDeGrace)
        {
            if (!hasCoupOption)
                return false;

            CharacterController coupTarget = (target != null && coupTargets.Contains(target))
                ? target
                : coupTargets[0];
            target = coupTarget;
        }

        // A charge bull rush is a charge, not an action here: it goes through
        // NPCExecuteChargeForAI(npc, target, bullRush: true), as the PC's goes through the charge mode.
        if (choice.Value == SpecialAttackType.BullRushCharge)
        {
            Debug.Log($"[AI][SpecialAttack] {npc.Stats.CharacterName}: BullRushCharge is resolved by the charge executor, not as a standing maneuver.");
            return false;
        }

        // Same bull rush legality as the PC wrapper (size, swarm, incorporeal, grappling,
        // adjacency; PHB p.154), checked before any cost or AoO (CMB-102).
        if (choice.Value == SpecialAttackType.BullRushAttack
            && !npc.CanBullRush(target, false, out string bullRushReason))
        {
            Debug.Log($"[AI][SpecialAttack] {npc.Stats.CharacterName} cannot bull rush {target?.Stats?.CharacterName ?? "<null>"}: {bullRushReason}");
            return false;
        }

        // The Improved Grab hold bar, as on the PC wrapper (MM p.310; delegated RAW reading 2026-10-14, CMB-163): no
        // trip, disarm, sunder or coup de grace on the creature this NPC holds with one body part, before any cost.
        if (npc.IsBarredFromSpecialAttackOn(choice.Value, target, out string holdReason))
        {
            Debug.Log($"[AI][SpecialAttack] {holdReason}");
            return false;
        }

        // The one direct-attack gate the PC wrapper runs too (AttackLegality, batch 12 step 12), before any cost or AoO:
        // the Resilient Sphere (CMB-092), the maneuver's own reach (CMB-104: trip and grapple at natural reach, disarm and
        // sunder at the weapon's, bull rush adjacent, so a ranged kiter no longer trips from thrown range) and a known
        // failed ward save (D6).
        bool gatedManeuver = IsDirectAttackManeuverForGate(choice.Value);
        if (gatedManeuver)
        {
            Legality maneuverLegality = AttackLegality.CanDirectlyAttack(npc, target, DirectAttackKind.Maneuver, AttackIntent.ForManeuver(choice.Value));
            if (!maneuverLegality.Ok)
            {
                Debug.Log($"[AI][SpecialAttack] {npc.Stats.CharacterName} cannot use {choice.Value} on {target?.Stats?.CharacterName ?? "<null>"}: {maneuverLegality.Reason}");
                return false;
            }
        }

        // Same trip legality as the PC wrapper (size, swarm, incorporeal; PHB p.158, CMB-079), checked
        // before any cost or AoO.
        if (choice.Value == SpecialAttackType.Trip && !npc.CanTrip(target, out string tripReason))
        {
            Debug.Log($"[AI][SpecialAttack] {npc.Stats.CharacterName} cannot trip {target?.Stats?.CharacterName ?? "<null>"}: {tripReason}");
            return false;
        }

        // Sunder needs a weapon, a natural attack that deals slashing or bludgeoning damage or an unarmed strike
        // (CharacterController.CanSunderWithAttack, the check the PC buttons and the AI use; PHB p.158,
        // owner ruling 2026-10-08, CMB-102, and owner interpretation 2026-10-11, CMB-141), refused before any attack step or AoO is spent. A natural-attack
        // NPC sunders with the natural attack at its current step; at Haste's extra natural attack it picks
        // one that can sunder (GetSunderNaturalAttackIndexForStep).
        if (choice.Value == SpecialAttackType.Sunder && !npc.CanSunderWithAttack(-1, out string sunderReason))
        {
            Debug.Log($"[AI][SpecialAttack] {sunderReason}");
            return false;
        }

        int substituteNaturalIndex = -1;
        bool substituteGivesUpHaste = false;
        if (choice.Value == SpecialAttackType.Sunder && npc.GetManeuverSubstituteStepKind() == AttackStepKind.NaturalSequence)
        {
            int currentStep = npc.ProgressiveAttackPool.MainHandStepsUsed;
            if (npc.IsHasteExtraNaturalStep(currentStep))
            {
                substituteNaturalIndex = npc.GetSunderNaturalAttackIndexForStep(currentStep);
                substituteGivesUpHaste = substituteNaturalIndex >= 0;
            }
        }

        // Trip, disarm, sunder and grapple replace one melee attack (PHB p.141 Table 8-2 note 7):
        // one step of the NPC's own attack sequence at that step's bonus (an iterative BAB, or the
        // BAB of the natural attack it replaces, MM p.312), paid before the AoOs as the PC wrapper
        // does (CMB-102). In a natural sequence the NPC gives up the natural attack at the current
        // step, the order PerformNPCMeleeAttackSequence resolves them in. Other maneuvers still cost
        // a standard action (or the full round for a coup de grace).
        bool replacesAttack = ManeuverActionCost.ReplacesMeleeAttack(choice.Value);
        int? stepBab = null;
        if (replacesAttack)
        {
            if (!npc.TryCommitManeuverSubstituteStep(substituteNaturalIndex, out int maneuverBab, out _, out string why, substituteGivesUpHaste))
            {
                Debug.Log($"[AI][SpecialAttack] {npc.Stats.CharacterName} cannot use {choice.Value} as an attack: {why}");
                return false;
            }

            stepBab = maneuverBab;
        }
        else if (choice.Value != SpecialAttackType.CoupDeGrace && !npc.Actions.HasStandardAction)
        {
            return false;
        }

        // Sanctuary and Hide from Undead (PHB p.274, p.241; D6), as on the PC wrapper: the attempt rolls the NPC's one save
        // after the cost and before the initiation AoOs; a failure loses the step (a substitute paid it above) or the
        // action, and the maneuver is never made.
        if (gatedManeuver && AttackLegality.CommitWard(npc, target, DirectAttackKind.Maneuver, AttackIntent.ForManeuver(choice.Value)) == WardVerdict.Failed)
        {
            if (!replacesAttack)
            {
                if (choice.Value == SpecialAttackType.CoupDeGrace)
                    npc.Actions.UseFullRoundAction();
                else
                    npc.CommitStandardAction();
            }

            UpdateAllStatsUI();
            attempted = choice.Value;
            succeeded = false;
            return true;
        }

        // Same initiation AoOs as the PC wrapper (CMB-076). A foiled attempt still spends
        // its action; an NPC dropped by the AoO ends its turn (callers return after this).
        int hpBeforeManeuverAoOs = npc.Stats.CurrentHP;
        ManeuverAoOOutcome maneuverAoOOutcome = ResolveManeuverInitiationAoOs(npc, target, choice.Value);
        if (maneuverAoOOutcome != ManeuverAoOOutcome.Proceed)
        {
            // The PC wrapper spends its action before the AoOs, so an AoO that drops the PC to
            // 0 HP costs no extra strenuous-action hit point; match that for a dropped NPC.
            bool droppedByAoO = maneuverAoOOutcome == ManeuverAoOOutcome.AttackerIncapacitated
                || (hpBeforeManeuverAoOs > 0 && npc.Stats.CurrentHP <= 0);
            // A substitute already paid its attack step above.
            if (!replacesAttack)
            {
                if (choice.Value == SpecialAttackType.CoupDeGrace)
                    npc.Actions.UseFullRoundAction();
                else if (droppedByAoO)
                    npc.Actions.UseStandardAction();
                else
                    npc.CommitStandardAction();
            }

            UpdateAllStatsUI();
            attempted = choice.Value;
            succeeded = false; // foiled before the check
            return true;
        }

        var result = npc.ExecuteSpecialAttack(
            choice.Value,
            target,
            disarmAttackBonusOverride: choice.Value == SpecialAttackType.Disarm ? stepBab : null,
            grappleAttackBonusOverride: choice.Value == SpecialAttackType.Grapple ? stepBab : null,
            tripAttackBonusOverride: choice.Value == SpecialAttackType.Trip ? stepBab : null,
            sunderAttackBonusOverride: choice.Value == SpecialAttackType.Sunder ? stepBab : null);
        CombatUI.ShowCombatLog(CombatLogHelper.Death("☠", $"{npc.Stats.CharacterName} uses SPECIAL [{choice.Value}]! {result.Log}"));

        // The target's reaction (the acid spray, D1) resolves as ExecuteSpecialAttack returns and can end the combat or
        // drop the NPC: no aftermath then (the caller's loop stops on CombatOver).
        if (CurrentPhase == TurnPhase.CombatOver || CombatEndRules.IsOutOfFight(npc))
        {
            UpdateAllStatsUI();
            attempted = choice.Value;
            succeeded = result != null && result.Success;
            return true;
        }

        // Melee reaction effects (Fire Shield, Thorns, etc.) — trip/disarm are melee maneuvers
        if (choice.Value == SpecialAttackType.Trip || choice.Value == SpecialAttackType.Disarm)
            MeleeReactionService.TriggerReactions(npc, target, null);

        // The Improved Trip attack and the defender's counter-trip (PHB p.96, p.158; CMB-079), shared with
        // the PC wrapper. A controllable defender's prompt may still be open when this returns
        // (IsAwaitingCounterTripChoice); the NPC coroutines wait on it before the next step.
        if (choice.Value == SpecialAttackType.Trip)
            HandleTripAftermath(npc, target, result, null);

        if (result.Success)
        {
            if (choice.Value == SpecialAttackType.BullRushAttack || choice.Value == SpecialAttackType.BullRushCharge)
                ResolveBullRushPushAndFollow(npc, target, result, isCharge: false, squaresMovedThisCharge: 0, onComplete: null);
            else if (choice.Value == SpecialAttackType.Overrun)
                TryPushTargetAway(npc, target, 1, allowAttackerFollow: true);
        }

        // A substitute already paid its attack step before the AoOs. An AoO during a bull rush push
        // or follow that dropped the NPC costs no extra strenuous-action hit point either (the PC
        // wrapper commits before any AoO).
        if (!replacesAttack)
        {
            if (choice.Value == SpecialAttackType.CoupDeGrace)
                npc.Actions.UseFullRoundAction();
            else if (hpBeforeManeuverAoOs > 0 && npc.Stats.CurrentHP <= 0)
                npc.Actions.UseStandardAction();
            else
                npc.CommitStandardAction();
        }

        UpdateAllStatsUI();
        attempted = choice.Value;
        succeeded = result != null && result.Success;
        return true;
    }

    private bool ResolveRangedAttackAoOForNPCAttackIfProvoked(CharacterController attacker, RangeInfo rangeInfo)
    {
        if (attacker == null || attacker.Stats == null || attacker.Stats.IsDead)
            return true;

        bool isRangedOrThrownAttack = rangeInfo != null
            ? !rangeInfo.IsMelee
            : (attacker.IsEquippedWeaponRanged() || (attacker.GetEquippedMainWeapon()?.IsThrown ?? false));

        if (!isRangedOrThrownAttack)
            return true;

        List<CharacterController> threateningEnemies = ThreatSystem.GetThreateningEnemies(
            attacker.GridPosition,
            attacker,
            GetAllCharacters());

        threateningEnemies.RemoveAll(enemy => enemy == null || enemy.Stats == null || enemy.Stats.IsDead);

        if (threateningEnemies.Count == 0)
            return true;

        CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attacker.Stats.CharacterName} makes a ranged attack while threatened and provokes up to {threateningEnemies.Count} attack(s) of opportunity."));

        foreach (CharacterController enemy in threateningEnemies)
        {
            if (!ThreatSystem.CanMakeAoOAgainst(enemy, attacker))
            {
                Debug.Log($"[AOO-DEBUG] {enemy?.Stats?.CharacterName ?? "<unknown>"} cannot make AoO now (used {enemy?.Stats?.AttacksOfOpportunityUsed}/{enemy?.Stats?.MaxAttacksOfOpportunity}).");
                continue;
            }

            CombatResult aooResult = ThreatSystem.ExecuteAoO(enemy, attacker, trigger: "ranged");
            if (aooResult == null)
            {
                Debug.Log($"[AOO-DEBUG] ExecuteAoO returned null for {enemy?.Stats?.CharacterName ?? "<unknown>"} vs {attacker.Stats.CharacterName}.");
                continue;
            }

            CombatUI?.ShowCombatLog(CombatLogHelper.Buff("⚔", $"AoO vs ranged attack: {aooResult.GetDetailedSummary()}"));
        }

        if (attacker.Stats.IsDead)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Interrupted("💀", $"{attacker.Stats.CharacterName} is slain before completing the ranged attack."));
            return false;
        }

        return true;
    }

    /// <summary>
    /// Whether a hit lets <paramref name="attacker"/> try Improved Grab on <paramref name="target"/>: the attacker has
    /// it, the attack hit with its trigger attack, the target is alive, neither is already in a grapple link (a holder
    /// with one body part included, CMB-163), and the target is small enough
    /// (CharacterController.CanImprovedGrabTargetBySize, MM p.310). Every Improved Grab path checks this before it
    /// prompts a controllable attacker or rolls, PC and NPC alike (CMB-126). A trigger hit refused only for size logs
    /// one combat log line, so the rule is visible in play; each caller asks once per attack.
    /// </summary>
    private bool CanAttemptImprovedGrabFromAttack(CharacterController attacker, CharacterController target, CombatResult attackResult)
    {
        if (attacker?.Stats == null || target?.Stats == null || attackResult == null)
            return false;

        if (!attacker.Stats.HasImprovedGrab || target.Stats.IsDead || !attackResult.Hit)
            return false;

        // Only a melee hit triggers it (MM p.310: "hits with a melee weapon"), whatever the trigger attack's name.
        if (attackResult.IsRangedAttack)
            return false;

        if (!IsImprovedGrabTriggerAttack(attacker, attackResult))
            return false;

        // One grapple link per creature: a creature that holds another with one body part (not grappling, MM p.310)
        // starts no second grapple with its remaining attacks, and a creature already in a grapple is not grabbed again
        // (the engine's one-link-per-creature limit, a known RAW deviation, CMB-163: RAW allows a second hold or grapple).
        // Checked before the PC prompt and the AI choice.
        if (attacker.IsInAnyGrappleLink(out CharacterController attackerLinked) || target.IsInAnyGrappleLink(out _))
        {
            string attackName = !string.IsNullOrWhiteSpace(attackResult.WeaponName) ? attackResult.WeaponName : "trigger attack";
            string why = attackerLinked != null
                ? $"{attacker.Stats.CharacterName} is already in a grapple with {attackerLinked.Stats.CharacterName}"
                : $"{target.Stats.CharacterName} is already in a grapple";
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("🪢", $"No Improved Grab after the {attackName} hit: {why}."));
            return false;
        }

        if (!attacker.CanImprovedGrabTargetBySize(target, out string sizeReason))
        {
            string attackName = !string.IsNullOrWhiteSpace(attackResult.WeaponName) ? attackResult.WeaponName : "trigger attack";
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("🪢", $"No Improved Grab after the {attackName} hit: {sizeReason}."));
            return false;
        }

        return true;
    }

    /// <summary>The controller's choice after an Improved Grab trigger hit (MM p.310; CMB-148).</summary>
    public enum ImprovedGrabChoice
    {
        Skip,
        GrappleNormally,
        HoldWithBodyPart
    }

    /// <summary>
    /// The AI's Improved Grab choice (MM p.310; owner ruling 2026-10-11, CMB-148): its profile's
    /// (AIProfile.ShouldHoldWithImprovedGrab), else the default, between grappling normally and the -20 hold.
    /// </summary>
    internal ImprovedGrabChoice ChooseImprovedGrabForAI(CharacterController attacker, CharacterController target)
    {
        List<CharacterController> all = GetAllCharacters();
        bool hold = attacker != null && attacker.aiProfile != null
            ? attacker.aiProfile.ShouldHoldWithImprovedGrab(attacker, target, all)
            : DND35.AI.AIProfile.DefaultShouldHoldWithImprovedGrab(attacker, target, all);
        return hold ? ImprovedGrabChoice.HoldWithBodyPart : ImprovedGrabChoice.GrappleNormally;
    }

    /// <summary>
    /// Resolves the controller's Improved Grab choice through the one resolver, PC and NPC alike
    /// (CharacterController.ResolveImprovedGrabFreeAttempt): the natural attack that hit establishes the hold (its damage in
    /// later rounds, CMB-148), and the hold choice decides whether the attacker is grappled (MM p.310, CMB-149).
    /// </summary>
    private SpecialAttackResult ResolveImprovedGrabChoice(CharacterController attacker, CharacterController target, CombatResult attackResult, ImprovedGrabChoice choice)
    {
        NaturalAttackDefinition establishingAttack = attacker != null ? attacker.GetNaturalAttackForAttackResult(attackResult) : null;
        return attacker.ResolveImprovedGrabFreeAttempt(target, establishingAttack, choice == ImprovedGrabChoice.HoldWithBodyPart);
    }

    /// <summary>
    /// The natural attack an AI-run creature uses for Haste's extra attack (PHB p.239; owner decision
    /// 2026-10-07, CMB-106): its profile's choice (AIProfile.ChooseHasteNaturalAttackIndex), or the
    /// shared AI scoring (DND35.AI.NaturalAttackChoice) when it has no profile. A caller that runs the
    /// turn with another profile passes it in <paramref name="profile"/>, so one profile makes every choice.
    /// </summary>
    internal int ChooseHasteNaturalAttackIndexForAI(CharacterController npc, CharacterController target, DND35.AI.AIProfile profile = null)
    {
        if (npc == null || !npc.HasHasteExtraNaturalAttack())
            return -1;

        DND35.AI.AIProfile chooser = profile != null ? profile : npc.aiProfile;
        return chooser != null
            ? chooser.ChooseHasteNaturalAttackIndex(npc, target)
            : DND35.AI.NaturalAttackChoice.ChooseHasteExtraAttackIndex(npc, target);
    }

    /// <summary>
    /// The natural attack an AI-run creature makes with a grapple attack action (PHB p.156, CMB-127): among the
    /// options left this turn (CharacterController.GetGrappleNaturalAttackOptions), the best by its profile's natural
    /// attack scoring (AIProfile.ScoreHasteNaturalAttack, by default DND35.AI.NaturalAttackChoice.Score: the highest
    /// bonus unless another natural attack carries a rider that matters against the target).
    /// </summary>
    internal CharacterController.GrappleNaturalAttackOption ChooseGrappleNaturalAttackForAI(CharacterController npc, CharacterController target)
    {
        if (npc == null)
            return CharacterController.GrappleNaturalAttackOption.None;

        List<CharacterController.GrappleNaturalAttackOption> options = npc.GetGrappleNaturalAttackOptions();
        DND35.AI.AIProfile profile = npc.aiProfile;
        System.Func<int, float> score = profile != null
            ? (System.Func<int, float>)(index => profile.ScoreHasteNaturalAttack(npc, target, index))
            : null;
        int pick = DND35.AI.NaturalAttackChoice.ChooseGrappleAttackOption(npc, target, options, score);
        return pick >= 0 ? options[pick] : CharacterController.GrappleNaturalAttackOption.None;
    }

    /// <summary>
    /// NPC melee attack or full attack, one step at a time through the creature's own attack
    /// sequence (CMB-102, PHB p.143). The first step spends the standard action and a second turns
    /// the turn into a full attack (move action), so an NPC that moved, is slowed or can take only
    /// one action gets one step, and Haste adds one step: an iterative step for a weapon or unarmed
    /// sequence, or one extra natural attack after the natural attacks for a natural sequence, with
    /// the natural attack the AI picks (ChooseHasteNaturalAttackIndexForAI; PHB p.239, owner decision
    /// 2026-10-07, CMB-106). Before each
    /// step, weapon or natural, <paramref name="tryStepManeuver"/> may replace that attack with trip,
    /// disarm, sunder or grapple at that step's bonus: the iterative BAB, or the BAB of the natural
    /// attack it replaces (PHB p.141 Table 8-2 note 7, MM p.312; which types is
    /// ManeuverActionCost.ReplacesMeleeAttack; the BAB is CharacterController.TryCommitManeuverSubstituteStep).
    /// In a natural sequence the NPC is offered at most one substitute (an AI limit pending AI-035).
    /// Each attack is resolved by CharacterController.ResolveAttackSequenceStep, the resolver the
    /// PC iterative flow uses (PC_NPC_PARITY plan step 6).
    /// A trip that failed against a controllable defender may leave the counter-trip prompt open
    /// (PHB p.158, CMB-079): the loop then stops with <paramref name="resume"/>.Suspended set, and the
    /// coroutine wrapper waits for the answer and calls again with the same <paramref name="resume"/>
    /// to continue the sequence (null: a fresh sequence that cannot be resumed).
    /// </summary>
    private FullAttackResult PerformNPCMeleeAttackSequence(
        CharacterController npc,
        CharacterController initialTarget,
        DND35.AI.AIProfile profile,
        Func<CharacterController, CharacterController, bool> tryStepManeuver,
        out bool startedGrappleInSequence,
        out int maneuversUsed,
        out int targetSwitchCount,
        out string stopReason,
        NpcMeleeSequenceResume resume)
    {
        startedGrappleInSequence = false;
        maneuversUsed = 0;
        targetSwitchCount = 0;
        stopReason = null;
        if (resume != null)
            resume.Suspended = false;

        var aggregate = new FullAttackResult
        {
            Type = FullAttackResult.AttackType.FullAttack,
            Attacker = npc,
            Defender = initialTarget,
            DefenderHPBefore = initialTarget != null && initialTarget.Stats != null ? initialTarget.Stats.CurrentHP : 0
        };

        if (npc == null || npc.Stats == null || initialTarget == null || initialTarget.Stats == null)
            return aggregate;

        bool adaptive = profile != null && profile.ShouldSwitchTargetsMidFullAttack(npc);
        CharacterController currentTarget = initialTarget;

        // AI limit, not a rule (AI-035, CMB-102 open item 7): in a natural sequence the evaluation
        // re-runs before every natural attack and the shipped profiles would retry a failed trip (or
        // chain trip, disarm, grapple) with each one, never rolling the bites and claws an Improved
        // Grab creature needs. Until AI-035 adds an odds check, a natural-attack NPC makes at most
        // one maneuver substitute per sequence; its other natural attacks are rolled. PCs may replace
        // as many natural attacks as they like. Weapon sequences keep the per-step evaluation.
        bool naturalSubstituteUsed = resume != null && resume.NaturalSubstituteUsed;

        // Safety cap; the attack sequence itself ends the loop (CanCommitAttack). Sized from the
        // budget so that maneuvers and kill-retargets (iterations that commit no attack) cannot
        // crowd out a many-headed creature's last steps.
        int maxSequenceIterations = npc.GetMainHandAttackBudget(AttackStepKind.NaturalSequence)
            + npc.GetIterativeAttackCount() + 8;
        int iteration;
        for (iteration = 0; iteration < maxSequenceIterations; iteration++)
        {
            if (npc.Stats == null || npc.Stats.IsDead || !npc.CanTakeTurnActions() || CurrentPhase == TurnPhase.CombatOver)
                break;

            // Same predicate as the PC iterative flow (CharacterController.UsesInnateNaturalAttackSequence).
            AttackStepKind stepKind = npc.GetMeleeAttackStepKind();

            if (!npc.CanCommitAttack(stepKind, out string cannotCommitReason))
            {
                stopReason = cannotCommitReason;
                break;
            }

            bool needsNewTarget = currentTarget == null
                || currentTarget.Stats == null
                || currentTarget.Stats.IsDead
                || (profile != null && profile.ShouldIgnoreUnconsciousTargets(npc) && currentTarget.IsUnconscious)
                || !npc.IsTargetInCurrentWeaponRange(currentTarget) // melee reach; never the PC thrown mode (CMB-096)
                // The direct-attack gate, read only (AttackLegality): a creature holding with one body part attacks only
                // other opponents (MM p.310, CMB-149), nothing passes a Resilient Sphere (PHB p.258, CMB-092), and a failed
                // Sanctuary or Hide from Undead save bars the target (D6).
                || !AttackLegality.Check(npc, currentTarget, DirectAttackKind.Weapon, null, AttackLegalityChecks.Sphere | AttackLegalityChecks.HoldBar | AttackLegalityChecks.Ward).Ok;

            if (needsNewTarget)
            {
                // Without an adaptive profile the NPC attacks only its chosen target, as before.
                if (!adaptive)
                {
                    Legality stopLegality = currentTarget != null && currentTarget.Stats != null
                        ? AttackLegality.Check(npc, currentTarget, DirectAttackKind.Weapon, null, AttackLegalityChecks.Sphere | AttackLegalityChecks.HoldBar | AttackLegalityChecks.Ward)
                        : Legality.Allowed();
                    stopReason = currentTarget == null || currentTarget.Stats == null || currentTarget.Stats.IsDead
                        ? "target is down"
                        : (!stopLegality.Ok ? stopLegality.Reason : "target is out of reach");
                    break;
                }

                CharacterController inReachTarget = SelectBestAdaptiveFullAttackTarget(npc, profile, requireInRange: true);
                if (inReachTarget != null)
                {
                    currentTarget = inReachTarget;
                    targetSwitchCount++;
                    CombatUI?.ShowCombatLog(CombatLogHelper.Info("🎯", $"{npc.Stats.CharacterName} shifts focus to {currentTarget.Stats.CharacterName}."));
                }
                else
                {
                    CharacterController steppedTarget = null;
                    bool stepped = profile.ShouldTakeFiveFootStepToContinueFullAttack(npc)
                        && TryTakeFiveFootStepForAdaptiveFullAttack(npc, profile, out steppedTarget);

                    if (stepped)
                    {
                        currentTarget = steppedTarget;
                        targetSwitchCount++;
                        CombatUI?.ShowCombatLog(CombatLogHelper.Info("🎯", $"{npc.Stats.CharacterName} re-engages {currentTarget.Stats.CharacterName} after a 5-foot step."));
                    }
                    else
                    {
                        int remainingAttacks = npc.GetRemainingMainHandAttackSteps(stepKind);
                        CombatUI?.ShowCombatLog(CombatLogHelper.Info("↩", $"{npc.Stats.CharacterName} has no valid active targets for {remainingAttacks} remaining attack(s)."));
                        break;
                    }
                }
            }

            if (currentTarget == null || currentTarget.Stats == null)
                break;

            // A maneuver may replace this attack (CMB-102), weapon or natural. In a natural sequence it
            // replaces the natural attack of this step and rolls at that attack's BAB (primary full,
            // secondary -5 or -2 with Multiattack, MM p.312; owner decision 2026-10-07); the other
            // natural attacks still follow, as in the PC flow (the shared budget is the natural-attack count).
            // A declared smite keeps this step an attack: the smite rides on the next melee attack
            // (SmiteRules, CHR-020), so a maneuver substitute would waste it; a hit can still start a
            // free trip (ResolveFreeTripAfterHit).
            if (tryStepManeuver != null && !(naturalSubstituteUsed && stepKind == AttackStepKind.NaturalSequence)
                && npc.Stats.DeclaredSmite == SmiteSource.None)
            {
                if (tryStepManeuver(npc, currentTarget))
                {
                    maneuversUsed++;
                    if (stepKind == AttackStepKind.NaturalSequence)
                        naturalSubstituteUsed = true;

                    // A grapple started by a substitute: the remaining steps become grapple actions,
                    // handed to AI_GrappleRestrictedTurn by the caller.
                    if (npc.IsGrappling())
                    {
                        startedGrappleInSequence = true;
                        break;
                    }

                    if (npc.Stats.IsDead || !npc.CanTakeTurnActions() || CurrentPhase == TurnPhase.CombatOver)
                        break;

                    // A failed trip waits on a controllable defender's counter-trip choice (PHB p.158,
                    // CMB-079): stop here; NPCMeleeAttackSequence resumes after the answer.
                    if (IsAwaitingCounterTripChoice && resume != null)
                    {
                        resume.Suspended = true;
                        resume.NaturalSubstituteUsed = naturalSubstituteUsed;
                        resume.Target = currentTarget;
                        stopReason = "waiting for the defender's counter-trip choice";
                        break;
                    }

                    // A non-substitute (coup de grace, bull rush) spent the standard or full-round
                    // action, so the next CanCommitAttack fails and the loop ends.
                    continue;
                }
            }

            if (!npc.TryCommitAttack(stepKind, out int step, out string commitReason))
            {
                stopReason = commitReason;
                break;
            }

            // Sanctuary and Hide from Undead (PHB p.274, p.241; D6): this step's attempt rolls the NPC's one save against the
            // target's casting; a failure loses this step (paid above), and the next step looks for another target.
            if (AttackLegality.CommitWard(npc, currentTarget, DirectAttackKind.Weapon) == WardVerdict.Failed)
            {
                UpdateAllStatsUI();
                if (!adaptive)
                {
                    stopReason = "lost the attack to the target's ward";
                    break;
                }
                currentTarget = null;
                continue;
            }

            CharacterController flankPartner;
            bool isFlanking = CombatUtils.IsAttackerFlanking(npc, currentTarget, GetAllCharacters(), out flankPartner);
            int flankBonus = isFlanking ? CombatUtils.FlankingAttackBonus : 0;
            string partnerName = flankPartner != null && flankPartner.Stats != null
                ? flankPartner.Stats.CharacterName
                : null;

            AttackIntent stepIntent = AttackIntent.ForAI(this, npc, currentTarget);
            stepIntent.Hand = stepKind;
            RangeInfo rangeInfo = CalculateRangeInfo(npc, currentTarget, stepIntent);

            // Haste's extra natural attack (CMB-106): the AI picks which natural attack it uses (the NPC turn path is AI-run).
            int hasteNaturalAttackIndex = -1;
            if (stepKind == AttackStepKind.NaturalSequence && npc.IsHasteExtraNaturalStep(step))
                AttackDeciders.For(npc, ActorSource.AI).ChooseHasteNaturalAttack(this, npc, currentTarget, profile, 0, choice => hasteNaturalAttackIndex = choice);

            // Weapon steps run through CharacterController.Attack, so its charm, fascination and
            // command-undead breaks apply to NPC full attacks too (CMB-090).
            CombatResult attack = npc.ResolveAttackSequenceStep(
                currentTarget,
                stepKind,
                step,
                isFlanking,
                flankBonus,
                partnerName,
                rangeInfo,
                npc.GetEquippedMainWeapon(),
                0,
                out string label,
                hasteNaturalAttackIndex);

            if (attack == null)
                break;

            if (string.IsNullOrEmpty(label))
                label = $"Attack {step + 1}";

            aggregate.Attacks.Add(attack);
            aggregate.AttackLabels.Add(label);

            CombatUI?.ShowCombatLog(attack.GetAttackBreakdown(label));

            // The one post-attack hook (origin F): the thrown drop and ammunition, Concentration per hit (PHB p.70), melee
            // reactions, the free trip, Improved Grab (the AI's choice), the summon cleanup and the combat-end check for the
            // target and the attacker: a target or attacker dead, dying or unconscious may be the last of its side (CORE-011).
            AttackResolution stepResolution = AttackResolution.Create(AttackOrigin.NpcMelee, npc, currentTarget, stepIntent, ActorSource.AI).With(attack);
            stepResolution.RangeInfo = rangeInfo;
            stepResolution.CombatEndContext = "NPCPerformAttack.Sequence";
            var stepOutcome = new PostAttackOutcome();
            RunAttackResolvedSync(stepResolution, stepOutcome);
            if (stepOutcome.CombatEnded)
                break;
            if (stepOutcome.AttackerOutOfFight)
            {
                stopReason = "the attacker is out of the fight";
                break;
            }

            // A creature grappling acts only through grapple actions (PHB p.156; CMB-149): once its Improved Grab takes
            // hold, the remaining steps become grapple actions against the grappled creature, through the same step
            // counter (the caller hands them to AI_GrappleRestrictedTurn). A hold with one body part (MM p.310) leaves
            // it ungrappled: its remaining attacks go on against other opponents (the retarget above).
            if (npc.IsGrappling())
            {
                startedGrappleInSequence = true;
                stopReason = "grappling: the remaining attacks become grapple actions (PHB p.156)";
                break;
            }

            if (currentTarget.Stats.IsDead)
            {
                int attacksRemainingAfterKill = adaptive ? npc.GetRemainingMainHandAttackSteps(stepKind) : 0;
                CombatUI?.ShowCombatLog(CombatLogHelper.Death("💀", attacksRemainingAfterKill > 0
                    ? $"{currentTarget.Stats.CharacterName} is defeated! {attacksRemainingAfterKill} attack(s) remaining."
                    : $"{currentTarget.Stats.CharacterName} has fallen, but the fight continues!"));

                currentTarget = null;
                continue;
            }

            if (profile != null && profile.ShouldIgnoreUnconsciousTargets(npc) && currentTarget.IsUnconscious)
            {
                if (adaptive && npc.GetRemainingMainHandAttackSteps(stepKind) > 0)
                    CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("💤", $"{currentTarget.Stats.CharacterName} drops unconscious! {npc.Stats.CharacterName} looks for another active target."));

                currentTarget = null;
            }
        }

        // A break leaves iteration below the cap; reaching it means the cap cut the sequence short.
        if (iteration >= maxSequenceIterations)
            Debug.LogWarning($"[AI][Attack] {npc.Stats.CharacterName} melee sequence hit its safety cap of {maxSequenceIterations} iterations; remaining steps={npc.GetRemainingMainHandAttackSteps()}.");

        aggregate.DefenderHPAfter = aggregate.Defender != null && aggregate.Defender.Stats != null
            ? aggregate.Defender.Stats.CurrentHP
            : aggregate.DefenderHPBefore;
        aggregate.TargetKilled = aggregate.Defender != null && aggregate.Defender.Stats != null && aggregate.Defender.Stats.IsDead;

        return aggregate;
    }

    /// <summary>
    /// Where a suspended <see cref="PerformNPCMeleeAttackSequence"/> stopped: set when a counter-trip prompt
    /// opened after a step (CMB-079), read when the sequence continues after the answer.
    /// </summary>
    private sealed class NpcMeleeSequenceResume
    {
        public bool Suspended;
        public bool NaturalSubstituteUsed;
        public CharacterController Target;
    }

    /// <summary>
    /// A monk NPC declares a flurry of blows before its melee sequence when its profile chooses it
    /// (AIProfile.ShouldUseFlurryOfBlows) and the shared rules allow it (CharacterController.TryBeginFlurryOfBlows, the
    /// core the PC Flurry button calls; PHB p.40-41, CHR-003). The sequence then steps the flurry's attacks, each through
    /// ResolveAttackSequenceStep, so maneuvers may replace them as in any full attack. Only with the target in reach.
    /// </summary>
    private bool TryNPCDeclareFlurryOfBlows(CharacterController npc, CharacterController target)
    {
        if (npc == null || npc.Stats == null || !npc.Stats.IsMonk || target == null || npc.IsUsingFlurryOfBlows)
            return false;
        if (!npc.IsTargetInCurrentWeaponRange(target) || !npc.CanUseFlurryOfBlows(out _))
            return false;

        bool choose = npc.aiProfile != null
            ? npc.aiProfile.ShouldUseFlurryOfBlows(npc, target)
            : DND35.AI.AIProfile.DefaultShouldUseFlurryOfBlows(npc, target);
        if (!choose || !npc.TryBeginFlurryOfBlows(out _))
            return false;

        int[] bonuses = npc.GetFlurryOfBlowsAttackBonusPreview(target);
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("🥋",
            $"{npc.Stats.CharacterName} strikes with a flurry of blows ({string.Join("/", System.Array.ConvertAll(bonuses, CharacterStats.FormatMod))}; {CharacterStats.FormatMod(npc.Stats.FlurryOfBlowsAttackPenalty)} on every attack until its next turn)."));
        return true;
    }

    /// <summary>
    /// Coroutine wrapper for <see cref="PerformNPCMeleeAttackSequence"/>: summary log, last-known
    /// position search, and the grapple handoff after a grapple started by a substitute. When the
    /// sequence stops for a controllable defender's counter-trip prompt (PHB p.158, CMB-079) it waits
    /// for the answer and continues the same sequence.
    /// </summary>
    private IEnumerator NPCMeleeAttackSequence(
        CharacterController npc,
        CharacterController target,
        Func<CharacterController, CharacterController, bool> tryStepManeuver)
    {
        var resume = new NpcMeleeSequenceResume();
        FullAttackResult aggregate = PerformNPCMeleeAttackSequence(
            npc,
            target,
            npc.aiProfile,
            tryStepManeuver,
            out bool startedGrappleInSequence,
            out int maneuversUsed,
            out int targetSwitchCount,
            out string stopReason,
            resume);

        while (resume.Suspended)
        {
            while (IsAwaitingCounterTripChoice)
                yield return null;

            if (CurrentPhase == TurnPhase.CombatOver || npc.Stats == null || npc.Stats.IsDead)
                break;

            FullAttackResult rest = PerformNPCMeleeAttackSequence(
                npc,
                resume.Target != null ? resume.Target : target,
                npc.aiProfile,
                tryStepManeuver,
                out bool restStartedGrapple,
                out int restManeuvers,
                out int restSwitches,
                out stopReason,
                resume);
            aggregate.Attacks.AddRange(rest.Attacks);
            aggregate.AttackLabels.AddRange(rest.AttackLabels);
            startedGrappleInSequence |= restStartedGrapple;
            maneuversUsed += restManeuvers;
            targetSwitchCount += restSwitches;
        }

        if (aggregate.Defender != null && aggregate.Defender.Stats != null)
        {
            aggregate.DefenderHPAfter = aggregate.Defender.Stats.CurrentHP;
            aggregate.TargetKilled = aggregate.Defender.Stats.IsDead;
        }

        UpdateAllStatsUI();

        int attacksMade = aggregate.Attacks.Count;
        if (attacksMade == 0 && maneuversUsed == 0)
        {
            string why = string.IsNullOrEmpty(stopReason) ? "no attack step available" : stopReason;
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{npc.Stats.CharacterName} makes no attack: {why}."));
        }
        else
        {
            string sequenceName = npc.ProgressiveAttackPool.IsFullAttack ? "full attack" : "attack";
            string maneuverPart = maneuversUsed > 0 ? $", {maneuversUsed} maneuver(s)" : string.Empty;
            string switchPart = targetSwitchCount > 0 ? $", {targetSwitchCount} target switch(es)" : string.Empty;
            _lastCombatLog = $"✅ {npc.Stats.CharacterName} completes {sequenceName} ({attacksMade} attack(s){maneuverPart}, {aggregate.TotalDamageDealt} total damage{switchPart}).";
            CombatUI?.ShowCombatLog(_lastCombatLog);
        }

        Debug.Log($"[AI][Attack] {npc.Stats.CharacterName} melee sequence: attacks={attacksMade}, maneuvers={maneuversUsed}, hits={aggregate.HitCount}, totalDamage={aggregate.TotalDamageDealt}, fullAttack={npc.ProgressiveAttackPool.IsFullAttack}, stop={stopReason ?? "-"}");

        if (LogAttacksToConsole && attacksMade > 0)
            LogFullAttackToConsole(aggregate);

        if (CurrentPhase == TurnPhase.CombatOver)
            yield break;

        if (FullAttackHadLastKnownPositionMiss(aggregate) && HandleConsecutiveLastKnownAutoMiss(npc, target))
            yield return StartCoroutine(TryImmediateSearchAfterLastKnownMiss(npc, target));

        // The remaining steps of a turn whose grapple was started in the sequence (a substitute grapple, or an
        // Improved Grab that took hold, CMB-149) become grapple actions (PHB p.156); AI_GrappleRestrictedTurn draws them
        // through the same attack sequence: grapple checks and grapple natural attacks, each on a step of the BAB
        // ladder (CMB-127, CMB-146).
        if (startedGrappleInSequence && npc.IsGrappling() && CanUseGrappleAttackOption(npc))
            yield return StartCoroutine(AI_GrappleRestrictedTurn(npc));

        yield return new WaitForSeconds(1.0f);
    }

    /// <summary>Delegates to AIService.SelectAdaptiveFullAttackTarget for centralized target selection.</summary>
    private CharacterController SelectBestAdaptiveFullAttackTarget(
        CharacterController attacker,
        DND35.AI.AIProfile profile,
        bool requireInRange)
        => _aiService != null
            ? _aiService.SelectAdaptiveFullAttackTarget(attacker, profile, GetAllCharacters(), requireInRange)
            : null;

    private bool TryTakeFiveFootStepForAdaptiveFullAttack(
        CharacterController attacker,
        DND35.AI.AIProfile profile,
        out CharacterController nextTarget)
    {
        nextTarget = null;

        if (attacker == null || !CanTakeFiveFootStep(attacker) || _movementService == null)
            return false;

        var enemies = new List<CharacterController>();
        bool hasConsciousEnemy = false;

        foreach (CharacterController candidate in GetAllCharacters())
        {
            if (candidate == null || candidate == attacker || candidate.Stats == null || candidate.Stats.IsDead)
                continue;

            if (!TeamUtility.IsEnemy(attacker, candidate))
                continue;

            enemies.Add(candidate);
            if (!candidate.IsUnconscious)
                hasConsciousEnemy = true;
        }

        bool ignoreUnconscious = profile != null
            && profile.ShouldIgnoreUnconsciousTargets(attacker)
            && hasConsciousEnemy;

        Vector2Int[] neighbors = SquareGridUtils.GetNeighbors(attacker.GridPosition);
        Vector2Int bestStep = attacker.GridPosition;
        CharacterController bestTarget = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < neighbors.Length; i++)
        {
            Vector2Int stepCell = neighbors[i];
            if (!_movementService.CanTake5FootStep(attacker, stepCell))
                continue;

            for (int t = 0; t < enemies.Count; t++)
            {
                CharacterController candidate = enemies[t];
                if (ignoreUnconscious && candidate.IsUnconscious)
                    continue;

                int distance = SquareGridUtils.GetDistance(stepCell, candidate.GridPosition);
                if (!attacker.CanMeleeAttackDistance(distance, attacker.GetEquippedMainWeapon()))
                    continue;

                float score = profile != null ? profile.ScoreTarget(candidate, attacker) : 0f;
                score -= distance * 0.25f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestStep = stepCell;
                    bestTarget = candidate;
                }
            }
        }

        if (bestTarget == null)
            return false;

        SquareCell destination = Grid != null ? Grid.GetCell(bestStep) : null;
        if (destination == null)
            return false;

        if (!ExecuteFiveFootStep(attacker, destination, returnToActionChoices: false))
            return false;

        nextTarget = bestTarget;
        return true;
    }

    /// <summary>
    /// The NPC cast executor (batch 11 step 8): an AI request (SpellCastRequest.ForAI, with the AI decider and the NPC
    /// path's legacy quirks, LegacyCastQuirkSets.AiPath) run through the shared cast core at once (RunCastSync), in its
    /// own context on the cast stack, so it nests cleanly over a Ui cast the player left open. Returns whether the cast
    /// counts as cast (CastOutcome.WasCast): false when it was refused (no standard action, a dead or invalid target, no
    /// slot, out of range, an area spell) or its slot could not be spent; true once the slot is spent, whatever came of
    /// the spell. The cast core runs the combat-end check (batch 12 step 9).
    /// </summary>
    private bool TryNPCPerformSpellCast(CharacterController npc, CharacterController target, SpellData spell, MetamagicData metamagic = null)
    {
        if (npc == null || target == null || spell == null || npc.Stats == null || target.Stats == null)
            return false;

        SpellCastRequest request = SpellCastRequest.ForAI(npc, target, spell, metamagic);
        CastStageTrace.Mark(npc, spell, CastStageTrace.Begin, nameof(TryNPCPerformSpellCast));
        return RunCastSync(request, out _);
    }

    /// <summary>
    /// The NPC area cast executor (batch 11 step 25, AI-001 area half): an area the AI aimed (<paramref name="placement"/>,
    /// AreaAimSearch) built into an AI area request (BuildAiAreaRequest: SpellCastRequest.ForAIArea, or ForAIGrease for
    /// Grease's 10-ft. square) and run through the shared cast core at once (RunCastSync), the same stages, area handler
    /// registry and Grease resolve as a PC's area cast: the Legality stage filters the area by line of effect from the
    /// placement's origin and tests the origin's range and line of effect and the decider's capability. Returns whether the
    /// cast counts as cast (CastOutcome.WasCast). The cast core's area exit runs the combat-end check (batch 12 step 9).
    /// </summary>
    private bool TryNPCPerformAreaSpellCast(CharacterController npc, SpellData spell, AreaPlacement placement, MetamagicData metamagic = null)
    {
        if (npc == null || spell == null || placement == null || npc.Stats == null)
            return false;

        SpellCastRequest request = BuildAiAreaRequest(npc, spell, placement, metamagic);
        CastStageTrace.Mark(npc, spell, CastStageTrace.Begin, nameof(TryNPCPerformAreaSpellCast));
        return RunCastSync(request, out _);
    }

    /// <summary>
    /// The NPC summon cast executor (batch 11 step 27, SPL-091): a Summon Monster or Summon Nature's Ally cast as an AI
    /// summon request (SpellCastRequest.ForAISummon, the AI decider choosing the creature and its square at the Legality
    /// stage, AiFillSummonChoices) run through the shared cast core at once (RunCastSync): the same stages and the same
    /// Summon resolve as a PC's summon, which inserts each creature into the initiative after the caster and registers it.
    /// <paramref name="destination"/>: a square the caller chose (a scenario), else the AI's. Returns whether the cast
    /// counts as cast (CastOutcome.WasCast). The cast core runs the combat-end check (batch 12 step 9).
    /// </summary>
    private bool TryNPCPerformSummonSpellCast(CharacterController npc, SpellData spell, MetamagicData metamagic = null, Vector2Int? destination = null)
    {
        if (npc == null || spell == null || npc.Stats == null)
            return false;

        SpellCastRequest request = SpellCastRequest.ForAISummon(npc, spell, destination, metamagic);
        CastStageTrace.Mark(npc, spell, CastStageTrace.Begin, nameof(TryNPCPerformSummonSpellCast));
        return RunCastSync(request, out _);
    }

    /// <summary>
    /// The NPC hold of a melee touch spell's charge (batch 11 step 27, SPL-115; PHB p.141): an AI hold request
    /// (SpellCastRequest.ForAIHoldCharge) run through the shared cast core at once, the same stages and HoldCharge resolve
    /// as the PC's Discharge Later. The AI then moves into reach and delivers the charge in the same turn
    /// (AIService.TryDeliverHeldChargeTurn). Returns whether the cast counts as cast.
    /// </summary>
    private bool TryNPCHoldTouchCharge(CharacterController npc, SpellData spell, MetamagicData metamagic = null)
    {
        if (npc == null || spell == null || npc.Stats == null)
            return false;

        SpellCastRequest request = SpellCastRequest.ForAIHoldCharge(npc, spell, metamagic);
        CastStageTrace.Mark(npc, spell, CastStageTrace.Begin, nameof(TryNPCHoldTouchCharge));
        return RunCastSync(request, out _);
    }

    /// <summary>
    /// The NPC delivery of its held charge to <paramref name="target"/> (batch 11 step 27, SPL-115): the request the PC's
    /// Discharge button builds, with the AI decider (SpellCastRequest.ForAIHeldChargeDelivery), run through the shared
    /// cast core at once. A missed touch keeps the charge (PHB p.141; the PostCast stage). Returns whether the delivery
    /// was made (CastOutcome.WasCast: it passed the Legality stage; a delivery spends nothing).
    /// </summary>
    private bool TryNPCDeliverHeldCharge(CharacterController npc, CharacterController target)
    {
        if (npc == null || target == null || npc.Stats == null || target.Stats == null)
            return false;

        SpellCastRequest request = SpellCastRequest.ForAIHeldChargeDelivery(npc, target);
        if (request == null)
            return false;
        CastStageTrace.Mark(npc, request.Spell, CastStageTrace.Begin, nameof(TryNPCDeliverHeldCharge));
        return RunCastSync(request, out _);
    }

    /// <summary>Delegates to AIService.IsGiantBombardierBeetle for centralized AI utility.</summary>
    private static bool IsGiantBombardierBeetle(CharacterController npc)
        => AIService.IsGiantBombardierBeetle(npc);

    /// <summary>
    /// NPC attack action. Melee runs step by step through the creature's attack sequence
    /// (<see cref="NPCMeleeAttackSequence"/>); before each step <paramref name="tryStepManeuver"/>
    /// (the AI's maneuver evaluation) may replace that attack (CMB-102). Summons pass a trip-only
    /// evaluation; callers that pass nothing (charmed, confused, the ranged kiter) get plain attacks. Ranged attacks still use
    /// one FullAttack call or one Attack (CMB-091), with the maneuver tried once beforehand.
    /// </summary>
    private IEnumerator NPCPerformAttack(
        CharacterController npc,
        CharacterController target,
        Func<CharacterController, CharacterController, bool> tryStepManeuver = null)
    {
        if (npc == null || npc.Stats == null)
            yield break;

        if (target == null || target.Stats == null || target.Stats.IsDead)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{npc.Stats.CharacterName} has no valid target and stops attacking."));
            yield break;
        }

        // A grappling creature attacks only through grapple actions against the creature it grapples (PHB p.156; CMB-149).
        // The normal AI turn runs its grapple turn before it gets here; this gate covers the callers that attack directly
        // (a confused creature attacking the nearest creature, a charmed one attacking its charmer's foe).
        if (npc.IsGrappling())
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("🪢", $"{npc.Stats.CharacterName} is grappling, so its attack is a grapple action (PHB p.156)."));
            if (CanUseGrappleAttackOption(npc) || (npc.Actions != null && npc.Actions.HasStandardAction))
                yield return StartCoroutine(AI_GrappleRestrictedTurn(npc));
            yield break;
        }

        // A creature holding the target with one body part makes no ranged attack on it either (MM p.310; delegated RAW
        // reading 2026-10-14, CMB-163). Checked before anything that assumes an attack happens (wards, frightful
        // presence); the melee sequence below retargets on its own.
        if (npc.GetEquippedMainWeapon()?.WeaponCat == WeaponCategory.Ranged
            && npc.IsBarredFromOrdinaryAttackOn(target, out string rangedHoldReason))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("🪢", rangedHoldReason));
            yield return new WaitForSeconds(0.4f);
            yield break;
        }

        // Nothing passes a Resilient Sphere's boundary (PHB p.258, CMB-092), and a target whose ward this NPC failed to
        // overcome cannot be attacked (D6): the shared gate, read only, before anything that assumes an attack happens.
        Legality attackLegality = AttackLegality.Check(npc, target, DirectAttackKind.Weapon, null,
            AttackLegalityChecks.Sphere | AttackLegalityChecks.Ward);
        if (!attackLegality.Ok)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("🛡", attackLegality.Reason));
            yield return new WaitForSeconds(0.4f);
            yield break;
        }

        // D&D 3.5e: Sanctuary/Hide from Undead end if the warded creature attacks
        CombatFlowService.BreakProtectiveWardsOnAttack(npc);

        // --- Frightful Presence (dragons, Young Adult+) ---
        // Triggers once per combat when the dragon first attacks or uses breath weapon.
        if (npc.HasFrightfulPresence && !npc.HasFrightfulPresenceTriggered)
        {
            yield return StartCoroutine(ResolveFrightfulPresence(npc));
        }

        if (npc.aiProfile != null)
            npc.aiProfile.TryEnsureWeaponFallback(npc);

        if (!npc.CanAttackWithEquippedWeapon(out string cannotAttackReason))
        {
            if (ExecuteReload(npc, out string reloadLog))
            {
                CombatUI.ShowCombatLog(reloadLog);
                UpdateAllStatsUI();
                yield return new WaitForSeconds(0.8f);
                yield break;
            }

            CombatUI.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{npc.Stats.CharacterName} cannot attack: {cannotAttackReason}"));
            yield return new WaitForSeconds(0.6f);
            yield break;
        }

        AttackIntent npcIntent = AttackIntent.ForAI(this, npc, target);
        RangeInfo npcRangeInfo = CalculateRangeInfo(npc, target, npcIntent);

        CharacterController flankPartner;
        bool isFlanking = CombatUtils.IsAttackerFlanking(npc, target, GetAllCharacters(), out flankPartner);
        int flankBonus = isFlanking ? CombatUtils.FlankingAttackBonus : 0;
        string partnerName = flankPartner != null && flankPartner.Stats != null
            ? flankPartner.Stats.CharacterName
            : null;

        // The bombardier beetle's acid spray is a reaction to being attacked or disturbed, not part of its own attack
        // (MM p.285, D1; GameManager.AttackedReactions.cs).

        // Melee: one step at a time through the shared attack sequence (CMB-102). The split reads
        // the NPC's own weapon, not the PC thrown-mode global (NPCs never throw melee weapons, CMB-096).
        bool npcUsesRangedWeapon = npc.GetEquippedMainWeapon()?.WeaponCat == WeaponCategory.Ranged;
        if (!npcUsesRangedWeapon)
        {
            // A smite rides on the sequence's first melee attack (SmiteRules, CHR-020); declared only with the target in reach.
            if (npc.IsTargetInCurrentWeaponRange(target))
                TryNPCDeclareSmite(npc, target);
            TryNPCDeclareFlurryOfBlows(npc, target);
            yield return StartCoroutine(NPCMeleeAttackSequence(npc, target, tryStepManeuver));
            yield break;
        }

        // Ranged (and thrown) attacks are unchanged (CMB-091). The AI's maneuver is tried once
        // first, as before; the maneuver executor's reach gate refuses one out of reach (CMB-104).
        if (tryStepManeuver != null && tryStepManeuver(npc, target))
        {
            // A failed trip may be waiting on a controllable defender's counter-trip choice (CMB-079).
            while (IsAwaitingCounterTripChoice)
                yield return null;
            yield return new WaitForSeconds(0.8f);
            yield break;
        }

        // A projectile weapon needs a piece of ammunition to shoot (PHB p.113; CMB-019). With none, no attack is attempted:
        // no ward save, no ranged-attack AoO (PHB p.141: the attack provokes, and none is made) and no action spent. The AI
        // does not yet route an archer with an empty quiver to melee (AI-073).
        ItemData rangedWeapon = npc.GetEquippedMainWeapon();
        if (rangedWeapon != null && rangedWeapon.IsProjectileWeapon && _combatFlowService != null && !_combatFlowService.CheckAmmoAvailable(npc, rangedWeapon))
        {
            yield return new WaitForSeconds(0.6f);
            yield break;
        }

        // Sanctuary and Hide from Undead (PHB p.274, p.241; D6): the ranged attack's attempt rolls the NPC's one save. This
        // one-target ranged branch cannot retarget, so a failure loses its first attack step, the standard action (as a
        // single attack would); the turn may go on with its move action.
        if (AttackLegality.CommitWard(npc, target, DirectAttackKind.Weapon) == WardVerdict.Failed)
        {
            npc.TryCommitAttack(AttackStepKind.MainHand, out _, out _);
            UpdateAllStatsUI();
            yield return new WaitForSeconds(0.6f);
            yield break;
        }

        bool canUseFullAttack = npc.Actions != null
            && npc.Actions.HasFullRoundAction
            && npc.IsTargetInCurrentWeaponRange(target); // no full-round action while slowed (PHB p.280, CMB-095)

        if (canUseFullAttack)
        {
            if (!ResolveRangedAttackAoOForNPCAttackIfProvoked(npc, npcRangeInfo))
            {
                yield return new WaitForSeconds(0.8f);
                yield break;
            }

            npc.Actions.UseFullRoundAction();

            // One step at a time (batch 12 step 14; CMB-019, CMB-091): each shot checks its ammunition (PHB p.113) and a
            // thrown full attack the weapon in hand, and each goes through the post-attack hook (origin G): the thrown drop
            // and the next draw, one piece of ammunition, Concentration per hit, melee reactions, the free trip, Improved
            // Grab, the summon cleanup and the combat-end check. A crossbow's reload is checked by each step (FullAttack's
            // CanAttackWithWeapon), so an unloaded crossbow ends the sequence. The AoOs the shots provoke are resolved once
            // above, before the full-round action (CMB-091; step 17 makes them per shot).
            ItemData firstWeapon = npc.GetEquippedMainWeapon();
            bool thrownSequence = npcIntent.IsThrowOf(firstWeapon);
            var fullResult = new FullAttackResult { Type = FullAttackResult.AttackType.FullAttack, Attacker = npc, Defender = target, DefenderHPBefore = target.Stats.CurrentHP };
            const int MaxRangedSteps = 16;
            for (int stepIndex = 0; stepIndex < MaxRangedSteps; stepIndex++)
            {
                if (npc.Stats.IsDead || CombatEndRules.IsOutOfFight(npc) || target.Stats.IsDead)
                    break;

                ItemData stepWeapon = npc.GetEquippedMainWeapon();
                if (thrownSequence && !npcIntent.IsThrowOf(stepWeapon))
                {
                    CombatUI?.ShowCombatLog(CombatLogHelper.Info("↩", $"{npc.Stats.CharacterName} has no more weapons to throw."));
                    break;
                }
                if (stepWeapon != null && stepWeapon.IsProjectileWeapon && _combatFlowService != null && !_combatFlowService.CheckAmmoAvailable(npc, stepWeapon))
                    break;

                npcIntent.Weapon = stepWeapon;
                RangeInfo stepRangeInfo = CalculateRangeInfo(npc, target, npcIntent);
                FullAttackResult stepResult = npc.FullAttack(target, isFlanking, flankBonus, partnerName, stepRangeInfo,
                    startAttackIndex: stepIndex, maxAttacks: 1);
                if (stepResult == null || stepResult.Attacks.Count == 0)
                    break;

                CombatResult shot = stepResult.Attacks[0];
                string shotLabel = stepResult.AttackLabels != null && stepResult.AttackLabels.Count > 0 ? stepResult.AttackLabels[0] : $"Attack {stepIndex + 1}";
                fullResult.Attacks.Add(shot);
                fullResult.AttackLabels.Add(shotLabel);

                AttackResolution stepResolution = AttackResolution.Create(AttackOrigin.NpcRangedFull, npc, target, npcIntent, ActorSource.AI).With(shot);
                stepResolution.RangeInfo = stepRangeInfo;
                stepResolution.CombatEndContext = "NPCPerformAttack.FullAttack";
                stepResolution.ReportStep = () =>
                {
                    CombatUI?.ShowCombatLog(shot.GetAttackBreakdown(shotLabel));
                    UpdateAllStatsUI();
                };
                var stepOutcome = new PostAttackOutcome();
                IEnumerator stepPost = OnAttackResolved(stepResolution, stepOutcome);
                while (stepPost.MoveNext())
                    yield return stepPost.Current;

                if (stepOutcome.CombatEnded || CurrentPhase == TurnPhase.CombatOver)
                    yield break;
                // Grappling after a grab, or holding the target with one body part (MM p.310; CMB-163): no more shots at it.
                if (npc.IsGrappling() || npc.IsBarredFromOrdinaryAttackOn(target, out _))
                    break;
            }

            fullResult.DefenderHPAfter = target.Stats.CurrentHP;
            fullResult.TargetKilled = target.Stats.IsDead;
            string flankPrefix = isFlanking
                ? $"⚔ {npc.Stats.CharacterName} gains +2 flanking bonus{(string.IsNullOrEmpty(partnerName) ? "" : $" (with {partnerName})")}.\n"
                : string.Empty;

            _lastCombatLog = flankPrefix + fullResult.GetFullSummary();

            Debug.Log($"[AI][Attack] {npc.Stats.CharacterName} performed full attack: attacks={fullResult.Attacks.Count}, hits={fullResult.HitCount}, totalDamage={fullResult.TotalDamageDealt}");

            if (LogAttacksToConsole)
                LogFullAttackToConsole(fullResult);

            UpdateAllStatsUI();

            if (fullResult.TargetKilled)
                CombatUI.ShowCombatLog(CombatLogHelper.Death("💀", $"{target.Stats.CharacterName} has fallen, but the fight continues!"));

            if (FullAttackHadLastKnownPositionMiss(fullResult) && HandleConsecutiveLastKnownAutoMiss(npc, target))
                yield return StartCoroutine(TryImmediateSearchAfterLastKnownMiss(npc, target));

            yield return new WaitForSeconds(1.0f);
            yield break;
        }

        if (!npc.CommitStandardAction())
        {
            CombatUI.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{npc.Stats.CharacterName} has no standard action available."));
            yield return new WaitForSeconds(0.6f);
            yield break;
        }

        if (!ResolveRangedAttackAoOForNPCAttackIfProvoked(npc, npcRangeInfo))
        {
            yield return new WaitForSeconds(0.8f);
            yield break;
        }

        // The ammunition was checked before the action and the AoOs (above; PHB p.113, CMB-019).
        ItemData singleWeapon = npc.GetEquippedMainWeapon();

        CombatResult result = npc.Attack(target, isFlanking, flankBonus, partnerName, npcRangeInfo);

        // The one post-attack hook (origin H): the log, the thrown drop and the ammunition, Concentration, melee reactions,
        // the free trip, Improved Grab (the AI's choice), the summon cleanup and the combat-end check for the target and the
        // attacker (CORE-011).
        npcIntent.Weapon = singleWeapon;
        AttackResolution singleResolution = AttackResolution.Create(AttackOrigin.NpcRangedSingle, npc, target, npcIntent, ActorSource.AI).With(result);
        singleResolution.RangeInfo = npcRangeInfo;
        singleResolution.CombatEndContext = "NPCPerformAttack.Single";
        singleResolution.ReportStep = () =>
        {
            _lastCombatLog = BuildAttackLog(npc, isFlanking, partnerName, result);

            if (LogAttacksToConsole)
                Debug.Log("[Combat] " + _lastCombatLog);

            CombatUI.ShowCombatLog(_lastCombatLog);
            UpdateAllStatsUI();
        };
        var singleOutcome = new PostAttackOutcome();
        IEnumerator singlePost = OnAttackResolved(singleResolution, singleOutcome);
        while (singlePost.MoveNext())
            yield return singlePost.Current;

        if (singleOutcome.CombatEnded || CurrentPhase == TurnPhase.CombatOver)
            yield break;

        if (result.TargetKilled)
        {
            CombatUI.ShowCombatLog(_lastCombatLog + $"\n{target.Stats.CharacterName} has fallen, but the fight continues!");
        }

        if (IsLastKnownPositionAutoMiss(result) && HandleConsecutiveLastKnownAutoMiss(npc, target))
            yield return StartCoroutine(TryImmediateSearchAfterLastKnownMiss(npc, target));

        yield return new WaitForSeconds(1.0f);
    }

    private bool HandleConsecutiveLastKnownAutoMiss(CharacterController npc, CharacterController target)
    {
        if (npc == null || target == null)
            return false;

        LastKnownPositionTracker tracker = npc.GetComponent<LastKnownPositionTracker>();
        if (tracker == null)
            return true;

        int missCount = tracker.RegisterLastKnownAutoMiss(target);
        if (!tracker.ShouldForgetTargetAfterAutoMisses(target))
            return true;

        tracker.ForgetTarget(target);

        string npcName = npc.Stats != null ? npc.Stats.CharacterName : npc.name;
        string targetName = target.Stats != null ? target.Stats.CharacterName : target.name;
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{npcName} loses track of {targetName} after {missCount} failed attacks on the last known square and stops blind-firing."));
        Debug.Log($"[AI][Concealment] {npcName} forgetting stale last-known position for {targetName} after {missCount} consecutive auto-misses.");

        return false;
    }

    private static bool IsLastKnownPositionAutoMiss(CombatResult result)
    {
        if (result == null)
            return false;

        if (!result.MissedDueToConcealment || result.ConcealmentMissChance < 100)
            return false;

        string description = result.ConcealmentDescription ?? string.Empty;
        return description.IndexOf("last known", StringComparison.OrdinalIgnoreCase) >= 0
            || description.IndexOf("target moved", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool FullAttackHadLastKnownPositionMiss(FullAttackResult fullResult)
    {
        if (fullResult == null || fullResult.Attacks == null || fullResult.Attacks.Count == 0)
            return false;

        for (int i = 0; i < fullResult.Attacks.Count; i++)
        {
            if (IsLastKnownPositionAutoMiss(fullResult.Attacks[i]))
                return true;
        }

        return false;
    }

    private IEnumerator TryImmediateSearchAfterLastKnownMiss(CharacterController npc, CharacterController target)
    {
        if (npc == null || target == null || npc.Actions == null)
            yield break;

        if (!npc.Actions.HasMoveAction)
            yield break;

        LastKnownPositionTracker tracker = npc.GetComponent<LastKnownPositionTracker>();
        Vector2Int destinationHint = target.GridPosition;
        if (tracker != null)
        {
            Vector2Int? known = tracker.GetLastKnownPosition(target);
            if (known.HasValue)
                destinationHint = known.Value;
        }

        DND35.AI.AIProfile profile = npc.aiProfile;
        SquareCell searchCell = _aiService != null
            ? _aiService.EvaluateMovementOptions(npc, destinationHint, retreat: false, target, profile)
            : null;

        if (searchCell == null || searchCell.Coords == npc.GridPosition)
            yield break;

        string npcName = npc.Stats != null ? npc.Stats.CharacterName : npc.name;
        string targetName = target.Stats != null ? target.Stats.CharacterName : target.name;

        CombatUI?.ShowCombatLog(CombatLogHelper.Failure("", $"{npcName} rushes to search after missing {targetName}'s last known position."));

        yield return StartCoroutine(MoveCharacterAlongComputedPath(npc, searchCell.Coords, PlayerMoveSecondsPerStep));
        if (!npc.CanTakeTurnActions())
            yield break; // dropped by an AoO while moving (CMB-073)

        if (npc.Actions.HasMoveAction)
            npc.Actions.UseMoveAction();

        bool canSeeAfterMove = npc.CanSee(target, npc.IsEquippedWeaponRanged());
        CombatUI?.ShowCombatLog(canSeeAfterMove
            ? $"{npcName} reacquires visual contact on {targetName}."
            : $"{npcName} continues searching for {targetName} in concealment.");

        yield return new WaitForSeconds(0.35f);
    }

    // ========== DETAILED CONSOLE LOGGING ==========

    private void LogFullAttackToConsole(FullAttackResult result)
    {
        if (_combatFlowService != null)
        {
            _combatFlowService.LogFullAttackToConsole(result);
            return;
        }
    }

    private void ResetAttackDamageModesForAllCharacters()
    {
        foreach (var character in GetAllCharacters())
        {
            if (character == null)
                continue;

            character.ResetAttackDamageMode();
        }

        CombatUI?.ResetDamageModeToggleVisual();
        Debug.Log("[GameManager] Attack damage modes reset to class/equipment defaults for new round");
    }

    private static string FormatConsoleModLine(int value, string label)
    {
        if (value >= 0)
            return $"+ {value} ({label})";
        else
            return $"- {-value} ({label})";
    }

    // ========== QUICKENED SPELL TRACKING (D&D 3.5e: ONE PER ROUND) ==========

    /// <summary>
    /// Reset quickened spell tracking for all characters at the start of a new round.
    /// D&D 3.5e: Each character can cast only one quickened spell per round.
    /// </summary>
    private void ResetQuickenedSpellTrackingForAllCharacters()
    {
        foreach (var character in GetAllCharacters())
        {
            var spellComp = character.Spellcasting;
            if (spellComp != null)
            {
                spellComp.ResetQuickenedSpellTracking();
            }
        }
        Debug.Log("[GameManager] Quickened spell tracking reset for new round");
    }

    // ══════════════════════════════════════════════════════════════════
    //  Breath Weapon Execution (called from AIService for dragon-type AI)
    // ══════════════════════════════════════════════════════════════════

    /// <summary>The dice context of each creature's save against a breath weapon (SaveRollRules; the scenario harness forces it by this label).</summary>
    public const string BreathWeaponSaveContext = "Breath weapon save";

    /// <summary>The dice context of each creature's Will save against a frightful presence (SaveRollRules; a test can force it by this label).</summary>
    public const string FrightfulPresenceSaveContext = "Frightful presence save";

    /// <summary>
    /// Resolves a breath weapon attack aimed at <paramref name="aimTarget"/>.
    /// Computes the affected cells (cone or line), rolls damage, and applies
    /// Reflex/Fort saves per D&amp;D 3.5e rules. Returns true if the breath
    /// weapon was successfully used.
    /// </summary>
    public IEnumerator NPCExecuteBreathWeaponForAI(CharacterController npc, CharacterController aimTarget)
    {
        if (npc == null || aimTarget == null || !npc.HasBreathWeapon || !npc.IsBreathWeaponReady)
            yield break;

        // A once-per-round use (the bombardier beetle's acid spray, MM p.285) is a reaction (GameManager.AttackedReactions.cs);
        // IsBreathWeaponReady is already false for it, and this guard runs before Frightful Presence so nothing is spent.
        if (npc.GetBreathWeaponDefinition().OncePerRound)
            yield break;

        // --- Frightful Presence (dragons) triggers on first attack/breath ---
        if (npc.HasFrightfulPresence && !npc.HasFrightfulPresenceTriggered)
        {
            yield return StartCoroutine(ResolveFrightfulPresence(npc));
        }

        BreathWeaponDefinition bw = npc.UseBreathWeapon(); // consumes use, starts cooldown
        if (bw == null)
            yield break;

        int rangeSquares = Mathf.Max(1, bw.RangeFeet / 5);
        string shapeName = bw.Shape == BreathWeaponShape.Cone ? "cone" : "line";

        // Compute affected cells: the cone or line starts at an intersection on the edge of the creature's space (MM p.69,
        // PHB p.175) and covers the aim target, the best such aim for its side, the one DragonAIProfile scored
        // (AoESystem.BestDirectedAimAt; batch 11 step 24, decision D5).
        AreaAim breathAim = AoESystem.BestDirectedAimAt(npc, npc.GridPosition,
            bw.Shape == BreathWeaponShape.Cone ? AoEShape.Cone : AoEShape.Line, rangeSquares, aimTarget, Grid);
        HashSet<Vector2Int> cells = breathAim != null ? breathAim.Cells : null;

        if (cells == null || cells.Count == 0)
        {
            Debug.LogWarning($"[AI][BreathWeapon] {npc.Stats.CharacterName} breath weapon produced no cells!");
            yield break;
        }

        // Roll damage: DamageCount × d(DamageDice), once for the whole breath; every creature in the area takes this one
        // total before its own save (decision D6 of the batch 11 design, batch 11 step 20, SPL-180).
        // A breath with no damage dice (the gorgon's petrifying breath) deals none.
        int totalDamage = (bw.DamageCount > 0 && bw.DamageDice > 0)
            ? DiceService.RollMultiple(bw.DamageCount, bw.DamageDice, "Breath weapon damage") + bw.DamageBonus
            : 0;

        // Gather targets in the area
        List<CharacterController> allChars = GetAllCharacters();
        var targets = new List<CharacterController>();
        for (int i = 0; i < allChars.Count; i++)
        {
            CharacterController c = allChars[i];
            if (c == null || c.Stats == null || c.Stats.IsDead || c == npc)
                continue;
            // Any square of its space, as BestDirectedAimAt counted it (batch 11 step 24 review).
            if (AoESystem.IsCreatureInCells(cells, c))
                targets.Add(c);
        }

        // Build combat log
        var log = new System.Text.StringBuilder();
        log.AppendLine("═══════════════════════════════════");
        log.AppendLine($"🔥 {npc.Stats.CharacterName} unleashes a {bw.RangeFeet}-ft {shapeName} of {bw.DamageType}!");
        log.AppendLine($"  Damage: {bw.DamageCount}d{bw.DamageDice}{(bw.DamageBonus != 0 ? (bw.DamageBonus > 0 ? "+" : "") + bw.DamageBonus : "")} = {totalDamage}");
        log.AppendLine($"  Save DC {bw.SaveDC} ({(bw.IsReflexSave ? "Reflex" : "Fortitude")}) {(bw.SaveNegates ? "negates" : "for half damage")}");
        log.AppendLine($"  Targets: {targets.Count} creature(s) in {cells.Count} squares");
        log.AppendLine();

        // The breath resolves for every creature in the area first: a reaction to its damage (the acid spray, D1) comes
        // once the whole breath has resolved.
        DeferAttackedReactions();
        try
        {
            for (int t = 0; t < targets.Count; t++)
            {
                CharacterController target = targets[t];
                log.AppendLine($"  --- {target.Stats.CharacterName} ---");

                // The one save roll (SaveRollRules, D13): the Luck reroll, natural 1 and 20 (PHB p.177), and the bonuses that
                // depend on the breathing creature (SaveContext.CreatureAbility: an alignment ward; SPL-018).
                SaveRoll breathSave = SaveRollRules.Roll(target.Stats,
                    bw.IsReflexSave ? SavingThrowType.Reflex : SavingThrowType.Fortitude, bw.SaveDC,
                    SaveContext.CreatureAbility(npc.Stats.CharacterName + "'s breath weapon", npc), BreathWeaponSaveContext);
                int saveBonus = breathSave.Modifier;
                int saveRoll = breathSave.Roll;
                int saveTotal = breathSave.Total;
                bool saved = breathSave.Succeeded;

                int damageRolled = saved ? (bw.SaveNegates ? 0 : totalDamage / 2) : totalDamage;

                // Evasion and Improved Evasion on a Reflex-half breath (PHB p.50, p.41-42; SpellSaveResolver.ApplyEvasion, SPL-090).
                string evasionNote = null;
                if (bw.IsReflexSave && !bw.SaveNegates)
                    damageRolled = SpellSaveResolver.ApplyEvasion(damageRolled, target.Stats, saved, out evasionNote);

                string saveResult = saved ? "SAVED" : "FAILED";
                log.AppendLine($"  {(bw.IsReflexSave ? "Reflex" : "Fortitude")} save DC {bw.SaveDC}: d20={saveRoll}+{saveBonus}={saveTotal} - {saveResult}{breathSave.NaturalSuffix}!");
                if (evasionNote != null)
                    log.AppendLine($"  {evasionNote}");

                // A breath weapon is a supernatural ability: immunity, Protection from Energy, resistance and Fire Shield
                // apply by its energy type, damage reduction does not (MM p.307); then the concentration and death checks
                // (SPL-004). The combat-end check runs once after every target.
                if (damageRolled > 0)
                {
                    int hpBefore = target.Stats.CurrentHP;
                    DamagePacket breathPacket = DamagePackets.Supernatural(npc.Stats.CharacterName + "'s breath weapon", bw.DamageType, saved && bw.IsReflexSave);
                    breathPacket.SourceCreature = npc;
                    DamageResolutionResult dealt = DealDamage(target, damageRolled, breathPacket);
                    int hpAfter = target.Stats.CurrentHP;

                    log.AppendLine($"  Damage: {dealt.FinalDamage} {bw.DamageType}{(saved && !bw.SaveNegates ? " (half)" : "")}{DescribeMitigation(dealt)}");
                    log.AppendLine($"  {target.Stats.CharacterName}: {hpBefore} → {hpAfter} HP");
                }

                if (target.Stats.IsDead)
                    log.AppendLine($"  💀 {target.Stats.CharacterName} has been slain!");

                log.AppendLine();
            }
        }
        finally
        {
            ResumeAttackedReactions(flush: false);
        }

        log.Append("═══════════════════════════════════");

        CombatUI?.ShowCombatLog(log.ToString());
        if (LogAttacksToConsole)
            Debug.Log(log.ToString());

        // The reactions the breath triggered, after its log.
        FlushAttackedReactions();

        UpdateAllStatsUI();
        EvaluateCombatEnd("NPCBreathWeapon");

        yield return new WaitForSeconds(1.0f);
    }

    // ================================================================
    // Frightful Presence — D&D 3.5e Dragon Special Attack
    // ================================================================

    /// <summary>
    /// Resolves Frightful Presence for a dragon (Young Adult+).
    /// All enemies within range must make a Will save or become:
    ///   - Panicked (if HD &lt;= threshold, typically 4)
    ///   - Shaken (if HD &gt; threshold)
    /// Duration: 4d6 rounds. On success: immune for 24 hours (rest of combat).
    /// Dragons are immune to other dragons' Frightful Presence.
    /// </summary>
    private IEnumerator ResolveFrightfulPresence(CharacterController dragon)
    {
        if (dragon == null || !dragon.HasFrightfulPresence)
            yield break;

        dragon.MarkFrightfulPresenceTriggered();

        FrightfulPresenceDefinition fp = dragon.GetFrightfulPresenceDefinition();
        if (fp == null) yield break;

        int rangeSquares = Mathf.Max(1, fp.RangeFeet / 5);
        string dragonName = dragon.Stats != null ? dragon.Stats.CharacterName : "Dragon";

        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"🐉 <b>{dragonName}</b> radiates Frightful Presence! (DC {fp.SaveDC}, {fp.RangeFeet} ft.)"));
        yield return new WaitForSeconds(0.6f);

        // Gather all player characters in range
        List<CharacterController> allChars = GetAllCharacters();
        for (int i = 0; i < allChars.Count; i++)
        {
            CharacterController target = allChars[i];
            if (target == null || target.Stats == null || target.Stats.IsDead)
                continue;
            if (target == dragon)
                continue;
            // Dragons are immune to frightful presence
            if (target.Stats.CreatureType == "Dragon")
                continue;
            // Only affects enemies
            if (target.Team == dragon.Team)
                continue;

            int distance = Mathf.Abs(target.GridPosition.x - dragon.GridPosition.x)
                         + Mathf.Abs(target.GridPosition.y - dragon.GridPosition.y);
            if (distance > rangeSquares)
                continue;

            string targetName = target.Stats.CharacterName;
            int targetHD = target.Stats.GetHitDice();

            // Will save against a fear effect (SaveRules: morale against fear, CHR-018), through the one save roll
            // (SaveRollRules, D13).
            // The context names the dragon, so the bonuses that depend on the creature causing the fear apply (an
            // alignment ward, PHB p.266).
            SaveRoll fearSave = SaveRollRules.Roll(target.Stats, SavingThrowType.Will, fp.SaveDC,
                SaveContext.FearFrom(dragonName + "'s frightful presence", dragon), FrightfulPresenceSaveContext);
            int roll = fearSave.Roll;
            int willMod = fearSave.Modifier;
            int total = fearSave.Total;

            if (fearSave.Succeeded)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"  ✅ {targetName} resists Frightful Presence (Will {roll}+{willMod}={total} vs DC {fp.SaveDC}{fearSave.NaturalSuffix}) — immune for remainder of combat"));
                continue;
            }

            // Failed save — determine Panicked vs Shaken
            int durationRounds = 0;
            for (int d = 0; d < fp.DurationDice; d++)
                durationRounds += UnityEngine.Random.Range(1, fp.DurationDieSides + 1);

            if (targetHD <= fp.HDThresholdForPanic)
            {
                // Panicked
                target.ApplyCondition(CombatConditionType.Panicked, durationRounds, dragonName);
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"  😱 {targetName} is <b>Panicked</b> for {durationRounds} rounds! (Will {roll}+{willMod}={total} vs DC {fp.SaveDC}{fearSave.NaturalSuffix}, {targetHD} HD ≤ {fp.HDThresholdForPanic})"));
            }
            else
            {
                // Shaken
                target.ApplyCondition(CombatConditionType.Shaken, durationRounds, dragonName);
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"  😰 {targetName} is <b>Shaken</b> for {durationRounds} rounds! (Will {roll}+{willMod}={total} vs DC {fp.SaveDC}{fearSave.NaturalSuffix})"));
            }
        }

        yield return new WaitForSeconds(0.5f);
    }
}
