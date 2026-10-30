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
        CombatUI?.SetActivePC(0); // No PC active
        CombatUI?.SetActiveNPC(NPCs.IndexOf(npc)); // Highlight active NPC
        CombatUI?.SetActionButtonsVisible(false);
        CombatUI?.HideSummonContextMenu();

        // Update initiative UI to highlight current NPC
        UpdateInitiativeUI();

        // The turn-start effects, resets and combat-end check (BeginCombatantTurn, GameManager.TurnStart.cs) already ran in
        // OnTurnStarted, for this NPC as for a PC (D3, CMB-075, CORE-003).

        if (ShouldSkipTurnDueToHPState(npc))
        {
            CombatUI?.SetActiveNPC(-1); // Clear NPC highlight
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
            CombatUI?.ShowCombatLog(CombatLogHelper.SummonRaw($"{GetSummonDisplayName(summon)} protects {data.Caster.Stats.CharacterName}."));
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
                var retreatMove = new MoveAttempt();
                // The executor spends the move before the first step (D3); a refused move spends nothing.
                yield return StartCoroutine(MoveCharacterAlongComputedPathForAI(
                    MoveRequest.ForAI(summon, retreat.Coords, PlayerMoveSecondsPerStep, MoveSpend.MoveAction, retreatMove)));

                // ── Death/disable check after retreat movement ──
                if (!summon.CanTakeTurnActions())
                {
                    Debug.Log($"🔥 [AI] {summon.Stats.CharacterName} dropped during retreat (HP={summon.Stats.CurrentHP}) — turn ended");
                    yield break;
                }

                if (!retreatMove.Refused) // a refused move spends nothing (D3)
                {
                    CombatUI?.ShowCombatLog(CombatLogHelper.Buff("", $"{GetSummonDisplayName(summon)} withdraws to survive."));
                    yield return new WaitForSeconds(0.45f);
                }
            }
        }

        if (!summon.IsTargetInCurrentWeaponRange(target) && summon.Actions.HasMoveAction && _aiService != null)
        {
            SquareCell bestCell = _aiService.EvaluateMovementOptions(summon, target.GridPosition, retreat: false, target);
            if (bestCell != null)
            {
                var advanceMove = new MoveAttempt();
                yield return StartCoroutine(MoveCharacterAlongComputedPathForAI(
                    MoveRequest.ForAI(summon, bestCell.Coords, PlayerMoveSecondsPerStep, MoveSpend.MoveAction, advanceMove)));

                // ── Death/disable check after advance movement ──
                if (!summon.CanTakeTurnActions())
                {
                    Debug.Log($"🔥 [AI] {summon.Stats.CharacterName} dropped during advance (HP={summon.Stats.CurrentHP}) — turn ended");
                    yield break;
                }

                if (!advanceMove.Refused) // a refused move spends nothing and the summon goes on to its attack (D3)
                {
                    CombatUI?.ShowCombatLog(CombatLogHelper.SummonRaw($"{GetSummonDisplayName(summon)} closes in on {target.Stats.CharacterName}."));
                    yield return new WaitForSeconds(0.4f);
                }
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

        // Only a target the attack guard allows (batch 13 step 5, D1: reach, range, ammunition; C1: melee reach for a
        // throwable weapon, as before step 5).
        if (target.Stats.IsDead || !AiAttackGuard.Passes(summon, target, allowThrow: false))
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

        yield return StartCoroutine(NPCPerformAttackForAI(summon, target, summonTripStep));
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
    /// The NPC maneuver executor, the AI side of the maneuver core (batch 12 step 19, CMB-102): it picks the maneuver (the
    /// forced one, else the legacy chooser) and builds the AI request (ManeuverRequest.ForAI: the AI decider, the item its
    /// profile picks, D15, and the two-weapon state of the attack step it replaces), and the one executor
    /// (ExecuteManeuver) does the rest, as for a PC: the legality, the cost of the table paid before the ward and the AoOs,
    /// the resolution and its aftermath. Returns true when it acted (the cost was spent); then <paramref name="attempted"/>
    /// is the maneuver type and <paramref name="succeeded"/> its result (false also when a ward save or an initiation AoO
    /// foiled the attempt). Both outputs only report: the AI's stopgap maneuver memory (AIService, AI-060) reads them; no
    /// rule does. A failed trip against a controllable defender may leave its counter-trip prompt open
    /// (IsAwaitingCounterTripChoice); the NPC coroutines wait on it before the next step.
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

            target = (target != null && coupTargets.Contains(target)) ? target : coupTargets[0];
        }

        // A charge bull rush is a charge, not an action here: it goes through
        // NPCExecuteChargeForAI(npc, target, bullRush: true), as the PC's goes through the charge mode.
        if (choice.Value == SpecialAttackType.BullRushCharge)
        {
            Debug.Log($"[AI][SpecialAttack] {npc.Stats.CharacterName}: BullRushCharge is resolved by the charge executor, not as a standing maneuver.");
            return false;
        }

        ManeuverRequest req = ManeuverRequest.ForAI(this, npc, target, choice.Value, _maneuverStepIntent);
        // Through the catalog's maneuver entry (batch 13 step 6): its executor runs ExecuteManeuver and, with NoWait, returns
        // at once as before (a controllable defender's prompt settles later; the callers wait on it themselves).
        ActionDefinition entry = ActionCatalog.ForManeuver(choice.Value);
        ManeuverOutcome outcome;
        if (entry != null && entry.Executor is ManeuverActionExecutor)
        {
            ActionContext ctx = ActionContext.For(entry.Id, npc, ActorSource.AI, target);
            ctx.CoreRequest = req;
            ctx.NoWait = true;
            ActionOutcome ran = RunAIActionNow(ctx);
            outcome = ran.CoreOutcome as ManeuverOutcome
                ?? new ManeuverOutcome { Request = req, Kind = ManeuverOutcomeKind.Refused, Reason = ran.Refusal.Reason };
        }
        else
            outcome = ExecuteManeuver(req, null);
        if (outcome.Kind == ManeuverOutcomeKind.Refused)
        {
            Debug.Log($"[AI][SpecialAttack] {npc.Stats.CharacterName} cannot use {choice.Value} on {target?.Stats?.CharacterName ?? "<null>"}: {outcome.Reason}");
            return false;
        }

        UpdateAllStatsUI();
        attempted = choice.Value;
        succeeded = outcome.Succeeded;
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
    /// (AIProfile.ShouldHoldWithImprovedGrab), else the default, between grappling normally and the -20 hold; or skip the
    /// grab (the creature "can attempt" it, MM p.310; CMB-098, batch 12 step 19) when its profile says the grab is not worth
    /// it (AIProfile.ShouldSkipImprovedGrab; by default only a hopeless one).
    /// </summary>
    internal ImprovedGrabChoice ChooseImprovedGrabForAI(CharacterController attacker, CharacterController target)
    {
        List<CharacterController> all = GetAllCharacters();
        bool skip = attacker != null && attacker.aiProfile != null
            ? attacker.aiProfile.ShouldSkipImprovedGrab(attacker, target, all)
            : DND35.AI.AIProfile.DefaultShouldSkipImprovedGrab(attacker, target, all);
        if (skip)
        {
            Debug.Log($"[AI][ImprovedGrab] {attacker.Stats.CharacterName} skips the grab on {target?.Stats?.CharacterName ?? "its target"} (AIProfile.ShouldSkipImprovedGrab).");
            return ImprovedGrabChoice.Skip;
        }
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
    /// The NPC attack or full attack run synchronously (batch 12 step 17): an AI request (AttackSequenceRequest.ForAI with
    /// <paramref name="profile"/> and <paramref name="tryStepManeuver"/>) through the one attack-sequence executor
    /// (RunAttackSequence), the stages every attacker runs. For callers that cannot yield (the static suites); the NPC turn
    /// drives the executor as a coroutine (<see cref="NPCAttackSequence"/>). The post-attack hook runs with the AI's choices
    /// for a target's reaction (CallerCanWait false). When the sequence waits anyway (a controllable defender's counter-trip
    /// prompt, CMB-079) the rest finishes as a coroutine and the returned result holds the attacks made so far.
    /// </summary>
    private FullAttackResult PerformNPCMeleeAttackSequence(
        CharacterController npc,
        CharacterController initialTarget,
        DND35.AI.AIProfile profile,
        Func<CharacterController, CharacterController, bool> tryStepManeuver,
        out bool startedGrappleInSequence,
        out int maneuversUsed,
        out int targetSwitchCount,
        out string stopReason)
    {
        AttackSequenceRequest request = AttackSequenceRequest.ForAI(this, npc, initialTarget, tryStepManeuver, profile);
        request.CallerCanWait = false;
        var outcome = new AttackSequenceOutcome();
        RunAttackSequenceSync(request, outcome);
        startedGrappleInSequence = outcome.StartedGrapple;
        maneuversUsed = outcome.ManeuversUsed;
        targetSwitchCount = outcome.TargetSwitches;
        stopReason = outcome.StopReason;
        return outcome.Aggregate;
    }

    /// <summary>
    /// The AI step chooser's target test (batch 13 step 5, D1): the attack guard's (AiAttackGuard.CheckTarget) and the
    /// attack sequence's stage-3 reach test (IsTargetInStepRange), one predicate with the executor.
    /// </summary>
    private static Legality StepTargetLegality(CharacterController npc, CharacterController target, AttackIntent probe)
    {
        Legality legality = AiAttackGuard.CheckTarget(npc, target, probe);
        if (legality.Ok && !IsTargetInStepRange(npc, target, probe))
            return Legality.Refused(LegalityBar.Reach, "target is out of reach");
        return legality;
    }

    /// <summary>
    /// The AI's answer to IAttackDecider.ChooseNextStep (batch 12 step 17; CMB-102): the NPC keeps its target while it is a
    /// valid one (alive, not an unconscious one its profile ignores, in reach or range for the step's mode, attackable:
    /// AttackLegality's sphere, hold bar and known ward failure). Otherwise, with a profile that switches targets mid full
    /// attack (AIProfile.ShouldSwitchTargetsMidFullAttack), the best target in reach (AIService.SelectAdaptiveFullAttackTarget),
    /// else a 5-foot step to reach one (melee, AIProfile.ShouldTakeFiveFootStepToContinueFullAttack); without one it stops.
    /// It logs the slain and unconscious lines of the step just made. When no step can be committed it stops first, so it
    /// never steps or retargets for an attack it cannot make.
    /// </summary>
    internal AttackStepChoice ChooseNextAttackStepForAI(AttackSequenceState state)
    {
        AttackSequenceRequest req = state.Request;
        CharacterController npc = req.Attacker;
        DND35.AI.AIProfile profile = req.Profile;
        AttackIntent intent = req.Intent;
        bool adaptive = profile != null && profile.ShouldSwitchTargetsMidFullAttack(npc);
        ItemData weapon = npc.GetEquippedMainWeapon();
        bool ranged = IsAttackModeRanged(npc, weapon, intent);
        // The next step's hand: a two-weapon intent's off-hand steps too (batch 12 step 18, AI-055; PHB p.160).
        AttackStepKind kind = NextStepKind(req, npc, ranged);
        CharacterController current = state.CurrentTarget;

        // The lines for the step just made: a slain target, or an unconscious one the profile ignores.
        AttackSequenceStep previous = state.TakePreviousStep();
        if (previous != null && current != null && current.Stats != null)
        {
            if (current.Stats.IsDead)
            {
                int remainingAfterKill = adaptive ? RemainingSteps(req, npc, ranged) : 0;
                CombatUI?.ShowCombatLog(CombatLogHelper.Death("💀", remainingAfterKill > 0
                    ? $"{current.Stats.CharacterName} is defeated! {remainingAfterKill} attack(s) remaining."
                    : $"{current.Stats.CharacterName} has fallen, but the fight continues!"));
                current = null;
            }
            else if (profile != null && profile.ShouldIgnoreUnconsciousTargets(npc) && current.IsUnconscious)
            {
                if (adaptive && RemainingSteps(req, npc, ranged) > 0)
                    CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("💤", $"{current.Stats.CharacterName} drops unconscious! {npc.Stats.CharacterName} looks for another active target."));
                current = null;
            }
        }

        // A thrown sequence with no weapon left to throw stops before any focus shift or 5-foot step (the executor would stop).
        if (state.ThrownSequence && (intent == null || !intent.IsThrowOf(weapon)))
            return AttackStepChoice.StopWith("no weapon left to throw");

        if (!npc.CanCommitAttack(kind, out string cannotCommitReason))
            return AttackStepChoice.StopWith(cannotCommitReason);

        AttackIntent probe = intent != null ? intent.CloneForStep() : null;
        if (probe != null)
            probe.Weapon = weapon;
        // The attack guard's target test (batch 13 step 5, D1; AiAttackGuard.CheckTarget): the shared direct-attack legality
        // (sphere, hold bar, a known failed ward) with the reach or range of this step's mode, and the executor's own stage-3
        // reach test (IsTargetInStepRange), so the chooser never keeps or picks a target the sequence then refuses. The
        // step's ammunition and load are stage 4's (CanShootWithWeapon), which refuses before any cost.
        Legality legality = current != null && current.Stats != null
            ? StepTargetLegality(npc, current, probe)
            : Legality.Refused("target is down");
        bool needsNewTarget = current == null
            || current.Stats == null
            || current.Stats.IsDead
            || (profile != null && profile.ShouldIgnoreUnconsciousTargets(npc) && current.IsUnconscious)
            || !legality.Ok;
        if (!needsNewTarget)
            return AttackStepChoice.At(current);

        // Without an adaptive profile the NPC attacks only its chosen target.
        if (!adaptive)
        {
            string why = current == null || current.Stats == null || current.Stats.IsDead
                ? "target is down"
                : (legality.Bar == LegalityBar.Reach || legality.Bar == LegalityBar.Range ? "target is out of reach" : legality.Reason);
            return AttackStepChoice.StopWith(why);
        }

        // A new target in reach or range for this step's mode: a throw looks within thrown range, never only adjacent foes.
        CharacterController inReachTarget = SelectBestAdaptiveFullAttackTarget(npc, profile, requireInRange: true,
            inRange: candidate => StepTargetLegality(npc, candidate, probe).Ok);
        if (inReachTarget != null && inReachTarget != current)
        {
            state.Outcome.TargetSwitches++;
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("🎯", $"{npc.Stats.CharacterName} shifts focus to {inReachTarget.Stats.CharacterName}."));
            return AttackStepChoice.At(inReachTarget);
        }

        if (!ranged && profile.ShouldTakeFiveFootStepToContinueFullAttack(npc)
            && TryTakeFiveFootStepForAdaptiveFullAttack(npc, profile, out CharacterController steppedTarget))
        {
            state.Outcome.TargetSwitches++;
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("🎯", $"{npc.Stats.CharacterName} re-engages {steppedTarget.Stats.CharacterName} after a 5-foot step."));
            return AttackStepChoice.At(steppedTarget);
        }

        int remainingAttacks = RemainingSteps(req, npc, ranged);
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("↩", $"{npc.Stats.CharacterName} has no valid active targets for {remainingAttacks} remaining attack(s)."));
        return AttackStepChoice.StopWith("no valid target");
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
        if (!choose)
            return false;
        return DeclareFlurryOfBlows(npc, target, out _);
    }

    /// <summary>
    /// Declares <paramref name="actor"/>'s flurry of blows (CharacterController.TryBeginFlurryOfBlows: the full-round
    /// action, PHB p.40) and logs its attack bonuses against <paramref name="target"/>: the AI's declaration and the
    /// catalog's flurry entry. False, with <paramref name="reason"/> and nothing logged, when the rules refuse it.
    /// </summary>
    internal bool DeclareFlurryOfBlows(CharacterController actor, CharacterController target, out string reason)
    {
        if (!actor.TryBeginFlurryOfBlows(out reason))
            return false;

        int[] bonuses = actor.GetFlurryOfBlowsAttackBonusPreview(target);
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("🥋",
            $"{actor.Stats.CharacterName} strikes with a flurry of blows ({string.Join("/", System.Array.ConvertAll(bonuses, CharacterStats.FormatMod))}; {CharacterStats.FormatMod(actor.Stats.FlurryOfBlowsAttackPenalty)} on every attack until its next turn)."));
        return true;
    }

    /// <summary>
    /// The NPC turn's attack or full attack (origins F, G and H, batch 12 step 17): <paramref name="request"/> (an AI
    /// request) run through the one attack-sequence executor as a coroutine, so a prompt it waits on (a controllable
    /// defender's counter-trip choice, CMB-079; a controlled reactor's acid spray choice) is waited on. Then the summary
    /// log, the last-known-position search after an auto-miss, and the grapple handoff after a grapple started in the
    /// sequence (a substitute grapple, or an Improved Grab that took hold, CMB-149).
    /// </summary>
    private IEnumerator NPCAttackSequence(CharacterController npc, CharacterController target, AttackSequenceRequest request)
    {
        var outcome = new AttackSequenceOutcome();
        IEnumerator run = RunAttackSequence(request, outcome);
        while (run.MoveNext())
            yield return run.Current;

        FullAttackResult aggregate = outcome.Aggregate;
        UpdateAllStatsUI();

        int attacksMade = outcome.AttacksMade;
        string stopReason = outcome.StopReason;
        if (attacksMade == 0 && outcome.ManeuversUsed == 0 && outcome.StepsLostToWard == 0)
        {
            string why = string.IsNullOrEmpty(stopReason) ? "no attack step available" : stopReason;
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{npc.Stats.CharacterName} makes no attack: {why}."));
        }
        else if (attacksMade > 0 || outcome.ManeuversUsed > 0)
        {
            string sequenceName = npc.ProgressiveAttackPool.IsFullAttack ? "full attack" : "attack";
            string maneuverPart = outcome.ManeuversUsed > 0 ? $", {outcome.ManeuversUsed} maneuver(s)" : string.Empty;
            string switchPart = outcome.TargetSwitches > 0 ? $", {outcome.TargetSwitches} target switch(es)" : string.Empty;
            _lastCombatLog = $"✅ {npc.Stats.CharacterName} completes {sequenceName} ({attacksMade} attack(s){maneuverPart}, {aggregate.TotalDamageDealt} total damage{switchPart}).";
            CombatUI?.ShowCombatLog(_lastCombatLog);
        }

        Debug.Log($"[AI][Attack] {npc.Stats.CharacterName} attack sequence ({request.Intent?.Mode}): attacks={attacksMade}, maneuvers={outcome.ManeuversUsed}, hits={aggregate.HitCount}, totalDamage={aggregate.TotalDamageDealt}, fullAttack={npc.ProgressiveAttackPool.IsFullAttack}, rapidShot={outcome.RapidShotDeclared}, stop={stopReason ?? "-"}");

        if (LogAttacksToConsole && attacksMade > 0)
            LogFullAttackToConsole(aggregate);

        if (CurrentPhase == TurnPhase.CombatOver)
            yield break;

        if (FullAttackHadLastKnownPositionMiss(aggregate) && HandleConsecutiveLastKnownAutoMiss(npc, target))
            yield return StartCoroutine(TryImmediateSearchAfterLastKnownMiss(npc, target));

        // The remaining steps of a turn whose grapple was started in the sequence become grapple actions (PHB p.156);
        // AI_GrappleRestrictedTurn draws them through the same attack sequence: grapple checks and grapple natural attacks,
        // each on a step of the BAB ladder (CMB-127, CMB-146).
        if (outcome.StartedGrapple && npc.IsGrappling() && CanUseGrappleAttackOption(npc))
            yield return StartCoroutine(AI_GrappleRestrictedTurn(npc));

        yield return new WaitForSeconds(1.0f);
    }

    /// <summary>Delegates to AIService.SelectAdaptiveFullAttackTarget for centralized target selection.</summary>
    private CharacterController SelectBestAdaptiveFullAttackTarget(
        CharacterController attacker,
        DND35.AI.AIProfile profile,
        bool requireInRange,
        Func<CharacterController, bool> inRange = null)
        => _aiService != null
            ? _aiService.SelectAdaptiveFullAttackTarget(attacker, profile, GetAllCharacters(), requireInRange, inRange)
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

        if (!TryTakeFiveFootStepForAI(attacker, destination.Coords))
            return false;

        nextTarget = bestTarget;
        return true;
    }

    /// <summary>
    /// The NPC cast executor (batch 11 step 8): an AI request (SpellCastRequest.ForAI, with the AI decider) run through the shared cast core at once (RunCastSync), in its
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
        return RunAICastAction(npc, request, target);
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
        return RunAICastAction(npc, request);
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
        return RunAICastAction(npc, request);
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
        return RunAICastAction(npc, request);
    }

    /// <summary>
    /// The NPC delivery of its held charge to <paramref name="target"/> (batch 11 step 27, SPL-115): the request the PC's
    /// Discharge button builds, with the AI decider (SpellCastRequest.ForAIHeldChargeDelivery), run through the shared
    /// cast core at once, at the PC's cost (batch 12 step 18, SPL-156; HeldChargeDeliveryRules): a foe's touch after the
    /// casting round is one step of the attack sequence (<see cref="DeliverHeldChargeThroughAttackStep"/>), the casting
    /// round's own touch costs nothing and a friend's touch the standard action. A missed touch keeps the charge (PHB
    /// p.141; the PostCast stage). Returns whether the delivery was made (CastOutcome.WasCast: it passed the Legality stage).
    /// </summary>
    private bool TryNPCDeliverHeldCharge(CharacterController npc, CharacterController target)
    {
        if (npc == null || target == null || npc.Stats == null || target.Stats == null)
            return false;

        SpellCastRequest request = SpellCastRequest.ForAIHeldChargeDelivery(npc, target);
        if (request == null)
            return false;
        CastStageTrace.Mark(npc, request.Spell, CastStageTrace.Begin, nameof(TryNPCDeliverHeldCharge));
        if (HeldChargeDeliveryRules.CostFor(npc, target) == HeldChargeDeliveryCost.AttackStep)
        {
            bool cast = false;
            DeliverHeldChargeThroughAttackStep(npc, target, AttackDeciders.For(npc, ActorSource.AI), request,
                () => cast = RunCastSync(request, out _), out _);
            // The step could not be made (no attack left, the touch's reach): nothing was spent and cast stays false.
            return cast;
        }
        return RunCastSync(request, out _);
    }

    /// <summary>
    /// A held charge's touch on a foe after its casting round as one step of the attack sequence (batch 12 step 18,
    /// SPL-156; PHB p.141: a touch attack made instead of a regular attack), PC and NPC alike: the executor
    /// (<see cref="RunAttackSequence"/>, AttackSequenceRequest.ForHeldChargeTouch) checks the step (CanCommitAttack, the
    /// shared gate with the touch's reach), pays it (the attack action, or one step of a full attack) and then runs
    /// <paramref name="runDelivery"/>, the delivery's cast with <paramref name="delivery"/>.DeliveryStepCommitted set and the
    /// step's BAB (DeliveryStepBab: the touch rolls at the iterative BAB of the attack it replaces, PHB p.143).
    /// <paramref name="runDelivery"/> returns whether the cast passed its Legality stage.
    /// Before any step is paid the delivery's own legality is tested (CheckLegality, no side effect: the target, the
    /// spell's range, line of effect): a delivery the cast core would refuse is run as it is, with no step paid, so its
    /// Legality stage refuses it with its own log line and refusal path, and nothing is spent (true is returned: the cast
    /// core handled it). False (with <paramref name="refusal"/>) when the attack step could not be made; nothing is spent then.
    /// </summary>
    internal bool DeliverHeldChargeThroughAttackStep(CharacterController caster, CharacterController target, IAttackDecider decider,
        SpellCastRequest delivery, Func<bool> runDelivery, out string refusal)
    {
        refusal = null;
        if (delivery != null)
        {
            bool committedBefore = delivery.DeliveryStepCommitted;
            delivery.DeliveryStepCommitted = true;
            CastOutcome legality = CheckLegality(delivery);
            delivery.DeliveryStepCommitted = committedBefore;
            if (legality.Kind == CastOutcomeKind.Refused)
            {
                runDelivery?.Invoke();
                return true;
            }
        }

        AttackSequenceRequest step = AttackSequenceRequest.ForHeldChargeTouch(caster, target, decider, (c, t, stepBab) =>
        {
            if (delivery != null)
            {
                delivery.DeliveryStepCommitted = true;
                delivery.DeliveryStepBab = stepBab;
            }
            return runDelivery == null || runDelivery();
        });
        var outcome = new AttackSequenceOutcome();
        RunAttackSequenceSync(step, outcome);
        if (outcome.AttacksMade > 0)
            return true;
        // The step was paid but the delivery's Legality refused it after all: the cast core logged it.
        if (delivery != null && delivery.DeliveryStepCommitted)
            return true;
        refusal = outcome.StopReason ?? "no attack left";
        return false;
    }

    /// <summary>Delegates to AIService.IsGiantBombardierBeetle for centralized AI utility.</summary>
    private static bool IsGiantBombardierBeetle(CharacterController npc)
        => AIService.IsGiantBombardierBeetle(npc);

    /// <summary>
    /// NPC attack action. Melee, ranged and thrown attacks run step by step through the one attack-sequence executor
    /// (RunAttackSequence, through <see cref="NPCAttackSequence"/>; batch 12 step 17, CMB-091); before each melee step
    /// <paramref name="tryStepManeuver"/> (the AI's maneuver evaluation) may replace that attack (CMB-102). Summons pass a
    /// trip-only evaluation; callers that pass nothing (charmed, confused, the ranged kiter) get plain attacks. Before a
    /// ranged or thrown sequence the maneuver is tried once.
    /// </summary>
    private IEnumerator NPCPerformAttack(
        CharacterController npc,
        CharacterController target,
        Func<CharacterController, CharacterController, bool> tryStepManeuver = null,
        int maxSteps = int.MaxValue)
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

        // The AI attack guard (batch 13 step 5, D1; owner ruling 2026-10-29): no attack is declared on a target out of the
        // reach or range of the weapon in hand, or with a projectile weapon that has no ammunition (PHB p.112-113, p.134,
        // p.137). The AI routines choose their targets among those the guard allows (AIService.SelectGuardPassingTarget);
        // this backstop covers every other caller (the compulsion controllers, the catalog's attack entry, a scripted
        // step) before anything is spent: no ward break, no frightful presence. A melee attack on a creature it holds with
        // one body part is left to the sequence, which retargets (MM p.310). An unloaded crossbow that can be reloaded
        // passes to the reload below. The profile's weapon fallback (a re-equip after a disarm, UndeadMindlessAIProfile) runs
        // first, so the guard judges the weapon the attack would use.
        if (npc.aiProfile != null)
            npc.aiProfile.TryEnsureWeaponFallback(npc);
        AttackIntent guardIntent = AttackIntent.ForAI(this, npc, target);
        Legality inReach = AttackLegality.IsInReachOrRange(npc, target, DirectAttackKind.Weapon, guardIntent, out _)
            ? AiAttackGuard.CheckWeapon(npc, guardIntent, anyReload: true)
            : Legality.Refused(LegalityBar.Reach, "target is out of reach");
        if (!inReach.Ok)
        {
            ItemData guardWeapon = guardIntent.Weapon;
            if (inReach.Bar == LegalityBar.Ammunition && guardWeapon != null && _combatFlowService != null
                && !_combatFlowService.CheckAmmoAvailable(npc, guardWeapon))
            {
                // CheckAmmoAvailable logged the shot refused for want of ammunition (PHB p.113).
            }
            else
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{npc.Stats.CharacterName} makes no attack: {inReach.Reason}."));
            }
            yield return new WaitForSeconds(0.4f);
            yield break;
        }

        if (!npc.CanAttackWithEquippedWeapon(out string cannotAttackReason))
        {
            // An unloaded crossbow is reloaded through the catalog's reload entry (batch 13 step 12, D4: a move action for a
            // hand or light crossbow, full-round for a heavy one, free or a move with Rapid Reload, provoking every time; PHB
            // p.115, p.99), and the standard action left then makes the attack (AI-057: a light crossbow reloads with the move
            // action and fires in the same turn, as a PC does).
            ItemData unloaded = npc.GetEquippedMainWeapon();
            bool reloaded = false;
            if (unloaded != null && unloaded.RequiresReload && !unloaded.IsLoaded)
            {
                var reload = new ActionOutcome();
                yield return RunAIAction(ActionContext.For(ActionIds.Reload, npc, ActorSource.AI), reload);
                reloaded = reload.Performed;
                if (reloaded || (reload.Spent != ActionCostType.NotAnAction && reload.Spent != ActionCostType.Free))
                    yield return new WaitForSeconds(0.8f);
                if (CurrentPhase == TurnPhase.CombatOver || !npc.CanTakeTurnActions())
                    yield break;
                if (reloaded && !ActionCosts.CanAfford(npc, ActionCostType.Standard))
                    yield break; // a full-round load, or a move-action load with the standard action converted: no shot this turn
            }

            if (!reloaded || !npc.CanAttackWithEquippedWeapon(out cannotAttackReason))
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{npc.Stats.CharacterName} cannot attack: {cannotAttackReason}"));
                yield return new WaitForSeconds(0.6f);
                yield break;
            }
        }

        // An attack action must be left (PHB p.139, p.143): a caller that spent it (a compulsion that stood up and closed in, a
        // load that took the standard action) declares no attack, so no ward breaks and no frightful presence fires.
        if (!ActionCosts.CanAfford(npc, ActionCostType.Standard) && !npc.ProgressiveAttackPool.HasStartedAttacking)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{npc.Stats.CharacterName} makes no attack: no attack action left this turn."));
            yield return new WaitForSeconds(0.4f);
            yield break;
        }

        // D&D 3.5e: Sanctuary/Hide from Undead end if the warded creature attacks (PHB p.274, p.241): only once an attack
        // follows, after the reload above (reloading is not an attack).
        CombatFlowService.BreakProtectiveWardsOnAttack(npc);

        // --- Frightful Presence (dragons, Young Adult+) ---
        // Triggers once per combat when the dragon first attacks or uses breath weapon.
        if (npc.HasFrightfulPresence && !npc.HasFrightfulPresenceTriggered)
        {
            yield return StartCoroutine(ResolveFrightfulPresence(npc));
        }

        // The NPC's own intent (AttackIntent.ForAI; CMB-096): melee, ranged, or a throw of a melee throwable at a target beyond
        // its reach; never the PC UI's selection.
        AttackSequenceRequest request = AttackSequenceRequest.ForAI(this, npc, target, tryStepManeuver, npc.aiProfile);
        if (maxSteps != int.MaxValue)
            request.MaxSteps = maxSteps;
        bool melee = request.Intent.Mode == AttackMode.Melee;

        // The bombardier beetle's acid spray is a reaction to being attacked or disturbed, not part of its own attack
        // (MM p.285, D1; GameManager.AttackedReactions.cs).

        if (melee)
        {
            // A smite rides on the sequence's first melee attack (SmiteRules, CHR-020); declared only with the target in reach.
            if (npc.IsTargetInCurrentWeaponRange(target))
                TryNPCDeclareSmite(npc, target);
            TryNPCDeclareFlurryOfBlows(npc, target);
            // A flurry has no off-hand attacks (PHB p.40; CanCommitAttack): a two-weapon intent chosen before the flurry was
            // declared is rebuilt, so no flurry attack takes the two-weapon main-hand penalty.
            if (npc.IsUsingFlurryOfBlows && request.InterleaveOffHand)
            {
                request = AttackSequenceRequest.ForAI(this, npc, target, tryStepManeuver, npc.aiProfile);
                if (maxSteps != int.MaxValue)
                    request.MaxSteps = maxSteps;
            }
        }
        else if (tryStepManeuver != null && tryStepManeuver(npc, target))
        {
            // Ranged and thrown: the AI's maneuver is tried once first, as before (a maneuver takes a melee attack's place, never
            // a shot's); the maneuver executor's reach gate refuses one out of reach (CMB-104). A failed trip may be waiting on
            // a controllable defender's counter-trip choice (CMB-079).
            while (IsAwaitingCounterTripChoice)
                yield return null;
            yield return new WaitForSeconds(0.8f);
            yield break;
        }

        // One step at a time through the one attack-sequence executor (batch 12 step 17; CMB-091, CMB-102), melee, ranged and
        // thrown alike: each step's ammunition, weapon and reload (PHB p.113), the AoOs each shot provokes (PHB p.141), the ward
        // save, the step's action (the standard action, then the move action for a full attack, PHB p.143) and the post-attack hook.
        yield return StartCoroutine(NPCAttackSequence(npc, target, request));
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

        // No search move it may not make (after a 5-foot step, prone, grappling: the shared movement legality, PHB p.144,
        // batch 12 step 22); its move action stays for another kind of move action.
        if (!MovementService.CanStartMovement(npc, MoveKind.Move).Ok)
            yield break;

        string npcName = npc.Stats != null ? npc.Stats.CharacterName : npc.name;
        string targetName = target.Stats != null ? target.Stats.CharacterName : target.name;

        // The line shows once the move starts (spent before the first step, D3); a refused move shows nothing.
        var searchMove = new MoveAttempt();
        yield return StartCoroutine(MoveCharacterAlongComputedPathForAI(
            MoveRequest.ForAI(npc, searchCell.Coords, PlayerMoveSecondsPerStep, MoveSpend.MoveAction, searchMove)
                .LoggingOnStart(CombatLogHelper.Failure("", $"{npcName} rushes to search after missing {targetName}'s last known position."))));
        if (!npc.CanTakeTurnActions())
            yield break; // dropped by an AoO while moving (CMB-073)
        if (searchMove.Refused)
            yield break; // refused before the first step: nothing spent

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
        // Through the catalog's breath entry (batch 13 step 6), which runs the core overload below.
        ActionContext ctx = ActionContext.For(ActionIds.BreathWeapon, npc, ActorSource.AI, aimTarget);
        return RunAIActionInline(ctx, new ActionOutcome());
    }

    /// <summary>
    /// The breath weapon core (the catalog's breath executor, ActionCore_BreathWeapon). It keeps the wrapper's name as an
    /// overload on purpose: the scenario trace records the GameManager methods that end a combat by name
    /// (ScenarioTrace.CallerFrames), so a renamed core would move every trace where a breath ends the fight.
    /// </summary>
    private IEnumerator NPCExecuteBreathWeaponForAI(CharacterController npc, CharacterController aimTarget, bool catalogCore)
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

    /// <summary>
    /// An NPC cast through the catalog's cast entry (batch 13 step 6, a pure move): the request built by the caller as
    /// before, run by the cast executor through the cast core at once (RunCastSync). Returns whether it counts as cast.
    /// </summary>
    private bool RunAICastAction(CharacterController npc, SpellCastRequest request, CharacterController target = null)
    {
        ActionContext ctx = ActionContext.For(ActionIds.CastSpell, npc, ActorSource.AI, target);
        ctx.CoreRequest = request;
        ctx.Spell = request != null ? request.Spell : null;
        ActionOutcome o = RunAIActionNow(ctx);
        return o.Performed;
    }
}
