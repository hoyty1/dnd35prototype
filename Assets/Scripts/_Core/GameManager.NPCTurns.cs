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
        CurrentPhase = TurnPhase.NPCTurn;
        CombatUI.SetActivePC(0); // No PC active
        CombatUI.SetActiveNPC(NPCs.IndexOf(npc)); // Highlight active NPC
        CombatUI.SetActionButtonsVisible(false);
        CombatUI.HideSummonContextMenu();

        // Update initiative UI to highlight current NPC
        UpdateInitiativeUI();

        ExpireAidBonusesAtTurnStart(npc);
        HandleFlamingSphereTurnStart(npc);

        if (ShouldSkipTurnDueToHPState(npc))
        {
            CombatUI.SetActiveNPC(-1); // Clear NPC highlight
            if (npc != null && npc.Stats != null)
            {
                string reason = GetUnableToActReason(npc);
                CombatUI?.ShowCombatLog(CombatLogHelper.Failure("", $"⏭ {npc.Stats.CharacterName} {reason} and cannot act this turn."));
            }
            NextInitiativeTurn();
            yield break;
        }

        // Determine AI behavior for this NPC
        NPCAIBehavior behavior = GetNPCBehaviorForAI(npc);
        if (_aiService != null)
            yield return StartCoroutine(_aiService.ExecuteNPCTurn(npc, behavior));

        // Check if all PCs are dead after NPC turn
        if (AreAllPCsDead())
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI.SetTurnIndicator("DEFEAT! All heroes have fallen!");
            CombatUI.SetActionButtonsVisible(false);
            yield break;
        }

        // Advance to next in initiative
        NextInitiativeTurn();
    }

    private IEnumerator AI_SummonedCreature(CharacterController summon)
    {
        ActiveSummonInstance data = GetActiveSummon(summon);
        if (data == null)
            yield break;

        // ── Death/disable check at summon turn start ──
        if (summon.Stats != null && summon.Stats.CurrentHP <= 0)
        {
            Debug.Log($"🔥 [AI] {summon.Stats.CharacterName} is dead/disabled (HP={summon.Stats.CurrentHP}) at summon turn start — turn ended");
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
                if (summon.Stats.CurrentHP <= 0)
                {
                    Debug.Log($"🔥 [AI] {summon.Stats.CharacterName} killed/disabled during retreat (HP={summon.Stats.CurrentHP}) — turn ended");
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
                if (summon.Stats.CurrentHP <= 0)
                {
                    Debug.Log($"🔥 [AI] {summon.Stats.CharacterName} killed/disabled during advance (HP={summon.Stats.CurrentHP}) — turn ended");
                    yield break;
                }

                summon.Actions.UseMoveAction();
                CombatUI.ShowCombatLog(CombatLogHelper.SummonRaw($"{GetSummonDisplayName(summon)} closes in on {target.Stats.CharacterName}."));
                yield return new WaitForSeconds(0.4f);
            }
        }

        // ── Death/disable re-check before attack phase ──
        if (summon.Stats.CurrentHP <= 0)
        {
            Debug.Log($"🔥 [AI] {summon.Stats.CharacterName} dead/disabled before summon attack phase (HP={summon.Stats.CurrentHP}) — turn ended");
            yield break;
        }

        target = SelectSummonTargetByCommand(summon, data);
        if (target == null)
            yield break;

        if (!summon.IsTargetInCurrentWeaponRange(target) || target.Stats.IsDead)
            yield break;

        if (TryExecuteSummonSmiteAttack(summon, target, data))
        {
            UpdateAllStatsUI();
            yield return new WaitForSeconds(0.8f);
            yield break;
        }

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

    private bool TryExecuteSummonSmiteAttack(CharacterController summon, CharacterController target, ActiveSummonInstance summonData)
    {
        if (summon == null || target == null || summonData == null)
            return false;
        if (summonData.SmiteUsed || (summon.Stats != null && summon.Stats.TemplateSmiteUsed))
            return false;
        if (!summon.Actions.HasStandardAction)
            return false;

        bool smiteEvil = summon.Stats.HasTemplateSmiteEvil && AlignmentHelper.IsEvil(target.Stats.CharacterAlignment);
        bool smiteGood = summon.Stats.HasTemplateSmiteGood && AlignmentHelper.IsGood(target.Stats.CharacterAlignment);
        if (!smiteEvil && !smiteGood)
            return false;

        // Smite uses Charisma modifier "if any"; clamp to 0 so low CHA never creates a penalty.
        int attackBonus = Mathf.Max(0, summon.Stats.CHAMod + 2);
        int damageBonus = Mathf.Max(1, summon.Stats.Level + 2);

        summon.Stats.MoraleAttackBonus += attackBonus;
        summon.Stats.MoraleDamageBonus += damageBonus;

        CombatResult result;
        try
        {
            CharacterController flankPartner;
            bool isFlanking = CombatUtils.IsAttackerFlanking(summon, target, GetAllCharacters(), out flankPartner);
            int flankBonus = isFlanking ? CombatUtils.FlankingAttackBonus : 0;
            result = summon.Attack(target, isFlanking, flankBonus, flankPartner != null ? flankPartner.Stats.CharacterName : null, null);
        }
        finally
        {
            summon.Stats.MoraleAttackBonus -= attackBonus;
            summon.Stats.MoraleDamageBonus -= damageBonus;
        }

        summon.CommitStandardAction();
        summonData.SmiteUsed = true;
        summon.Stats.TemplateSmiteUsed = true;

        string targetAxis = smiteEvil ? "Evil" : "Good";
        CombatUI.ShowCombatLog(CombatLogHelper.Color($"✦ {GetSummonDisplayName(summon)} uses Smite {targetAxis}! {result.GetDetailedSummary()}", "FFD280"));

        if (result.TargetKilled)
            HandleSummonDeathCleanup(target);

        return true;
    }

    private void TryResolveFreeTripOnHit(CharacterController attacker, CharacterController target, CombatResult attackResult, RangeInfo attackRange)
    {
        if (attacker == null || target == null || attacker.Stats == null || target.Stats == null || attackResult == null)
            return;

        if (!attacker.Stats.HasTripAttack)
            return;

        bool isMeleeHit = attackRange != null
            ? attackRange.IsMelee
            : !attackResult.IsRangedAttack;
        if (!isMeleeHit)
            return;

        if (!attackResult.Hit || target.Stats.IsDead || target.HasCondition(CombatConditionType.Prone))
            return;

        // MM trip (Ex): no touch attack and no AoO (CMB-014).
        SpecialAttackResult tripResult = attacker.ResolveFreeTripAttempt(target);
        string tripContext = tripResult.Success
            ? "free trip follow-up"
            : "free trip attempt failed";

        CombatUI?.ShowCombatLog(CombatLogHelper.Death("☠", $"{attacker.Stats.CharacterName} follows up with Trip ({tripContext}): {tripResult.Log}"));
        Debug.Log($"[NPC Trip Follow-up] {attacker.Stats.CharacterName} triggered free trip after hit. Success={tripResult.Success}");

        // Melee reaction effects (Fire Shield, Thorns, etc.) — free trip follow-up is a melee maneuver
        MeleeReactionService.TriggerReactions(attacker, target, null);
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
        return TryNPCSpecialAttackIfBeneficial(npc, target, null);
    }

    private bool TryNPCSpecialAttackIfBeneficial(CharacterController npc, CharacterController target, SpecialAttackType? forcedChoice)
    {
        if (npc == null || target == null)
            return false;

        bool hasImprovedGrab = npc.Stats != null && npc.Stats.HasImprovedGrab;
        var coupTargets = GetAdjacentHelplessEnemiesForCoupDeGrace(npc);
        bool profileAllowsCoupDeGrace = npc.EnemyUseCoupDeGraceOverride
            ?? (npc.aiProfile != null && npc.aiProfile.ShouldUseCoupDeGrace(npc));
        bool hasCoupOption = profileAllowsCoupDeGrace
            && coupTargets.Count > 0
            && npc.Actions != null
            && npc.Actions.HasFullRoundAction;

        if (npc.IsGrappling() && (!forcedChoice.HasValue || forcedChoice.Value != SpecialAttackType.CoupDeGrace))
            return false;

        // A maneuver that replaces an attack only needs the next attack step (CMB-102); the exact
        // cost per type is checked once the choice is known.
        if (!npc.Actions.HasStandardAction && !hasCoupOption && !npc.CanCommitAttack(AttackStepKind.MainHand, out _))
            return false;

        SpecialAttackType? choice = forcedChoice;

        if (choice.HasValue && !npc.CanPerformSpecialAttack(choice.Value))
        {
            string npcName = npc.Stats != null ? npc.Stats.CharacterName : "<unknown>";
            Debug.Log($"[AI][SpecialAttack] {npcName} cannot perform forced {choice.Value} while in {npc.GetPrimaryWeaponType()} mode.");
            return false;
        }

        if (!choice.HasValue)
        {
            if (hasCoupOption)
                choice = SpecialAttackType.CoupDeGrace;
            else if (!target.Stats.IsProne
                && npc.HasMeleeWeaponEquipped()
                && npc.CanPerformSpecialAttack(SpecialAttackType.Trip))
                choice = SpecialAttackType.Trip;

            if (choice == null
                && target.GetEquippedMainWeapon() != null
                && npc.Stats.STRMod >= 3
                && npc.CanPerformSpecialAttack(SpecialAttackType.Disarm))
                choice = SpecialAttackType.Disarm;

            if (choice == null
                && npc.Stats.STRMod >= 4
                && !hasImprovedGrab
                && npc.CanPerformSpecialAttack(SpecialAttackType.Grapple))
                choice = SpecialAttackType.Grapple;
        }

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

        // Trip, disarm, sunder and grapple replace one melee attack (PHB p.141 Table 8-2 note 7):
        // one step of the NPC's own attack sequence at that step's BAB, paid before the AoOs as the
        // PC wrapper does (CMB-102). Other maneuvers still cost a standard action (or the full round
        // for a coup de grace).
        bool replacesAttack = ManeuverActionCost.ReplacesMeleeAttack(choice.Value);
        int? stepBab = null;
        if (replacesAttack)
        {
            if (!npc.TryCommitAttack(AttackStepKind.MainHand, out int step, out string why))
            {
                Debug.Log($"[AI][SpecialAttack] {npc.Stats.CharacterName} cannot use {choice.Value} as an attack: {why}");
                return false;
            }

            stepBab = npc.GetMainHandAttackStepBAB(step);
        }
        else if (choice.Value != SpecialAttackType.CoupDeGrace && !npc.Actions.HasStandardAction)
        {
            return false;
        }

        // Same initiation AoOs as the PC wrapper (CMB-076). A foiled attempt still spends
        // its action; an NPC dropped by the AoO ends its turn (callers return after this).
        int hpBeforeManeuverAoOs = npc.Stats.CurrentHP;
        var initiationProvokers = new HashSet<CharacterController>();
        ManeuverAoOOutcome maneuverAoOOutcome = ResolveManeuverInitiationAoOs(npc, target, choice.Value, provokersOut: initiationProvokers);
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

        // Melee reaction effects (Fire Shield, Thorns, etc.) — trip/disarm are melee maneuvers
        if (choice.Value == SpecialAttackType.Trip || choice.Value == SpecialAttackType.Disarm)
            MeleeReactionService.TriggerReactions(npc, target, null);

        if (result.Success)
        {
            if (choice.Value == SpecialAttackType.BullRushAttack || choice.Value == SpecialAttackType.BullRushCharge)
                ResolveBullRushPushAndFollow(npc, target, result, isCharge: false, squaresMovedThisCharge: 0, onComplete: null,
                    attackerAlreadyProvoked: initiationProvokers);
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
            if (!ThreatSystem.CanMakeAoO(enemy))
            {
                Debug.Log($"[AOO-DEBUG] {enemy?.Stats?.CharacterName ?? "<unknown>"} cannot make AoO now (used {enemy?.Stats?.AttacksOfOpportunityUsed}/{enemy?.Stats?.MaxAttacksOfOpportunity}).");
                continue;
            }

            CombatResult aooResult = ThreatSystem.ExecuteAoO(enemy, attacker);
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

    private bool CanAttemptImprovedGrabFromAttack(CharacterController attacker, CharacterController target, CombatResult attackResult)
    {
        if (attacker?.Stats == null || target?.Stats == null || attackResult == null)
            return false;

        if (!attacker.Stats.HasImprovedGrab || target.Stats.IsDead || !attackResult.Hit)
            return false;

        return IsImprovedGrabTriggerAttack(attacker, attackResult);
    }

    private IEnumerator ResolveImprovedGrabWithPromptCoroutine(CharacterController attacker, CharacterController target, CombatResult attackResult, Action onResolved)
    {
        bool shouldAttemptGrab = true;
        if (attacker != null && attacker.IsControllable)
        {
            bool playerDecision = false;
            yield return StartCoroutine(PromptImprovedGrabChoice(attacker, target, attackResult != null ? attackResult.WeaponName : null, decision => playerDecision = decision));
            shouldAttemptGrab = playerDecision;
        }

        if (!shouldAttemptGrab)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("↷", $"{attacker?.Stats?.CharacterName ?? "Attacker"} declines to start a grapple."));
            onResolved?.Invoke();
            yield break;
        }

        SpecialAttackResult grabResult = attacker.ResolveImprovedGrabFreeAttempt(target);
        string attackName = !string.IsNullOrWhiteSpace(attackResult?.WeaponName) ? attackResult.WeaponName : "trigger attack";
        CombatUI?.ShowCombatLog(CombatLogHelper.Warning("🪢", $"Improved Grab ({attackName} hit): {grabResult.Log}"));
        onResolved?.Invoke();
    }

    private bool TryResolveImprovedGrabAfterSingleAttack(CharacterController attacker, CharacterController target, CombatResult attackResult, Action onResolved)
    {
        if (!CanAttemptImprovedGrabFromAttack(attacker, target, attackResult))
            return false;

        if (attacker != null && attacker.IsControllable)
        {
            StartCoroutine(ResolveImprovedGrabWithPromptCoroutine(attacker, target, attackResult, onResolved));
            return true;
        }

        SpecialAttackResult grabResult = attacker.ResolveImprovedGrabFreeAttempt(target);
        string attackName = !string.IsNullOrWhiteSpace(attackResult?.WeaponName) ? attackResult.WeaponName : "trigger attack";
        CombatUI?.ShowCombatLog(CombatLogHelper.Warning("🪢", $"Improved Grab ({attackName} hit): {grabResult.Log}"));
        return false;
    }

    private void TryResolveImprovedGrabFromAttackResults(CharacterController attacker, CharacterController target, List<CombatResult> attacks)
    {
        if (attacker?.Stats == null || target?.Stats == null || attacks == null || attacks.Count == 0)
            return;

        if (!attacker.Stats.HasImprovedGrab || target.Stats.IsDead)
            return;

        for (int i = 0; i < attacks.Count; i++)
        {
            CombatResult attackResult = attacks[i];
            if (!CanAttemptImprovedGrabFromAttack(attacker, target, attackResult))
                continue;

            SpecialAttackResult grabResult = attacker.ResolveImprovedGrabFreeAttempt(target);
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("🪢", $"Improved Grab ({attackResult.WeaponName} hit): {grabResult.Log}"));

            if (grabResult.Success || target.Stats.IsDead)
                break;
        }
    }

    /// <summary>
    /// NPC melee attack or full attack, one step at a time through the creature's own attack
    /// sequence (CMB-102, PHB p.143). The first step spends the standard action and a second turns
    /// the turn into a full attack (move action), so an NPC that moved, is slowed or can take only
    /// one action gets one step, and Haste adds its step through the iterative count (weapon and
    /// unarmed sequences only, CMB-106). Before each
    /// weapon step (and before the first natural step) <paramref name="tryStepManeuver"/> may
    /// replace that attack with trip, disarm, sunder or grapple at that step's BAB
    /// (PHB p.141 Table 8-2 note 7; which types is ManeuverActionCost.ReplacesMeleeAttack).
    /// Each attack is resolved by CharacterController.ResolveAttackSequenceStep, the resolver the
    /// PC iterative flow uses (PC_NPC_PARITY plan step 6).
    /// </summary>
    private FullAttackResult PerformNPCMeleeAttackSequence(
        CharacterController npc,
        CharacterController initialTarget,
        DND35.AI.AIProfile profile,
        Func<CharacterController, CharacterController, bool> tryStepManeuver,
        out bool startedGrappleBySubstitute,
        out int maneuversUsed,
        out int targetSwitchCount,
        out string stopReason)
    {
        startedGrappleBySubstitute = false;
        maneuversUsed = 0;
        targetSwitchCount = 0;
        stopReason = null;

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
        int stepsTaken = 0;

        // Safety cap; the attack sequence itself ends the loop (CanCommitAttack). Sized from the
        // budget so that maneuvers and kill-retargets (iterations that commit no attack) cannot
        // crowd out a many-headed creature's last steps.
        int maxSequenceIterations = npc.GetMainHandAttackBudget(AttackStepKind.NaturalSequence)
            + npc.GetIterativeAttackCount() + 8;
        int iteration;
        for (iteration = 0; iteration < maxSequenceIterations; iteration++)
        {
            if (npc.Stats == null || npc.Stats.IsDead || npc.Stats.CurrentHP <= 0 || CurrentPhase == TurnPhase.CombatOver)
                break;

            // Same predicate as CombatFlowService.ShouldUseNaturalAttackStep (PC iterative flow).
            AttackStepKind stepKind = npc.GetEquippedMainWeapon() == null && npc.Stats.HasNaturalAttacks
                ? AttackStepKind.NaturalSequence
                : AttackStepKind.MainHand;

            if (!npc.CanCommitAttack(stepKind, out string cannotCommitReason))
            {
                stopReason = cannotCommitReason;
                break;
            }

            bool needsNewTarget = currentTarget == null
                || currentTarget.Stats == null
                || currentTarget.Stats.IsDead
                || (profile != null && profile.ShouldIgnoreUnconsciousTargets(npc) && currentTarget.IsUnconscious)
                || !npc.IsTargetInCurrentWeaponRange(currentTarget); // melee reach; never the PC thrown mode (CMB-096)

            if (needsNewTarget)
            {
                // Without an adaptive profile the NPC attacks only its chosen target, as before.
                if (!adaptive)
                {
                    stopReason = currentTarget == null || currentTarget.Stats == null || currentTarget.Stats.IsDead
                        ? "target is down"
                        : "target is out of reach";
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

            // A maneuver may replace this attack (CMB-102). Inside a natural sequence only the first
            // step is offered (the maneuver takes an iterative BAB, and a natural creature below
            // BAB +6 has one iterative); the remaining natural attacks then follow it, as in the PC
            // iterative flow (the shared budget is the natural-attack count).
            if (tryStepManeuver != null && (stepKind == AttackStepKind.MainHand || stepsTaken == 0))
            {
                if (tryStepManeuver(npc, currentTarget))
                {
                    maneuversUsed++;
                    stepsTaken++;

                    // A grapple started by a substitute: the remaining steps become grapple actions,
                    // handed to AI_GrappleRestrictedTurn by the caller.
                    if (npc.IsGrappling())
                    {
                        startedGrappleBySubstitute = true;
                        break;
                    }

                    if (npc.Stats.IsDead || npc.Stats.CurrentHP <= 0)
                        break;

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

            stepsTaken++;

            CharacterController flankPartner;
            bool isFlanking = CombatUtils.IsAttackerFlanking(npc, currentTarget, GetAllCharacters(), out flankPartner);
            int flankBonus = isFlanking ? CombatUtils.FlankingAttackBonus : 0;
            string partnerName = flankPartner != null && flankPartner.Stats != null
                ? flankPartner.Stats.CharacterName
                : null;

            RangeInfo rangeInfo = CalculateRangeInfo(npc, currentTarget);
            bool isMeleeFearBreakAttack = IsMeleeAttackForTurnUndeadFearBreak(
                npc,
                npc.GetEquippedMainWeapon(),
                rangeInfo,
                treatAsThrownAttack: false);
            ProcessTurnUndeadMeleeFearBreak(npc, currentTarget, isMeleeFearBreakAttack);

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
                out string label);

            if (attack == null)
                break;

            if (string.IsNullOrEmpty(label))
                label = $"Attack {step + 1}";

            aggregate.Attacks.Add(attack);
            aggregate.AttackLabels.Add(label);

            CombatUI?.ShowCombatLog(attack.GetAttackBreakdown(label));

            // Concentration per damage instance (PHB p.70).
            if (attack.Hit && attack.TotalDamage > 0)
                CheckConcentrationOnDamage(currentTarget, attack.TotalDamage);

            // Melee reaction effects (Fire Shield, Thorns, etc.) — generic service call
            if (attack.Hit && !attack.IsRangedAttack)
                MeleeReactionService.TriggerReactions(npc, currentTarget, attack);

            var stepAttacks = new List<CombatResult> { attack };
            TryResolveFreeTripFromAttackResults(npc, currentTarget, stepAttacks, rangeInfo);
            // Today both NPC paths keep attacking after Improved Grab takes hold.
            TryResolveImprovedGrabFromAttackResults(npc, currentTarget, stepAttacks);

            if (currentTarget.Stats.IsDead)
            {
                HandleSummonDeathCleanup(currentTarget);

                if (AreAllPCsDead())
                {
                    CurrentPhase = TurnPhase.CombatOver;
                    CombatUI.SetTurnIndicator("DEFEAT! All heroes have fallen!");
                    CombatUI.SetActionButtonsVisible(false);
                    break;
                }

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
    /// Coroutine wrapper for <see cref="PerformNPCMeleeAttackSequence"/>: summary log, last-known
    /// position search, and the grapple handoff after a grapple started by a substitute.
    /// </summary>
    private IEnumerator NPCMeleeAttackSequence(
        CharacterController npc,
        CharacterController target,
        Func<CharacterController, CharacterController, bool> tryStepManeuver)
    {
        FullAttackResult aggregate = PerformNPCMeleeAttackSequence(
            npc,
            target,
            npc.aiProfile,
            tryStepManeuver,
            out bool startedGrappleBySubstitute,
            out int maneuversUsed,
            out int targetSwitchCount,
            out string stopReason);

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

        // The remaining steps of a turn whose grapple was started by a substitute become grapple
        // actions at those steps' BAB (PHB p.156); AI_GrappleRestrictedTurn draws them through the
        // same attack sequence.
        if (startedGrappleBySubstitute && npc.IsGrappling() && npc.CanCommitAttack(AttackStepKind.MainHand, out _))
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

    private bool TryNPCPerformSpellCast(CharacterController npc, CharacterController target, SpellData spell)
    {
        if (npc == null || target == null || spell == null || npc.Stats == null || target.Stats == null)
            return false;

        if (!npc.Actions.HasStandardAction)
            return false;

        if (target.Stats.IsDead)
            return false;

        SpellcastingComponent spellComp = npc.Spellcasting;
        if (spellComp == null || !spellComp.CanCastSpells)
            return false;

        if (!spellComp.CanCast(spell))
            return false;

        if (!IsValidTargetForSpell(npc, target, spell))
            return false;

        int rangeSquares = spell.GetRangeSquaresForCasterLevel(npc.Stats.GetCasterLevel());
        if (rangeSquares <= 0)
            rangeSquares = 1;

        int distance = SquareGridUtils.GetDistance(npc.GridPosition, target.GridPosition);
        if (distance > rangeSquares)
            return false;

        if (spell.TargetType == SpellTargetType.Area)
            return false;

        if (!npc.CommitStandardAction())
            return false;

        // Entangled (DC 15 + level) and grappled/pinned (DC 20 + level) casting Concentration,
        // as on the PC path; the helpers spend the slot when the spell is lost (SPL-006).
        if (!ResolveEntangledSomaticCastingConcentration(npc, spellComp, spell, null, false, spell.SpellLevel, false, -1, null)
            || !ResolveGrappledOrPinnedCastingConcentration(npc, spellComp, spell, null, false, spell.SpellLevel, false, -1, null))
        {
            UpdateAllStatsUI();
            return true;
        }

        bool consumed = spellComp.CastSpellFromSlot(spell);
        if (!consumed)
            return false;

        if (TryRollArcaneSpellFailure(npc, spell, false, out int asfRoll, out int asfChance))
        {
            LogArcaneSpellFailure(npc, spell, asfRoll, asfChance);
            UpdateAllStatsUI();
            return true;
        }

        // D&D 3.5e: Blinking NPC caster has 20% spell failure chance
        if (npc.HasActiveBlinkEffect)
        {
            int blinkNpcRoll = DiceService.Percentile("Blink NPC caster spell failure");
            if (blinkNpcRoll <= 20)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Damage("⚡", $"{npc.Stats.CharacterName}'s {spell.Name} fizzles! (Blink spell failure: rolled {blinkNpcRoll} ≤ 20%)"));
                UpdateAllStatsUI();
                return true;
            }
        }

        // PHB p.140: casting provokes AoOs from threatening enemies unless cast defensively;
        // same rolls as the PC prompt, with the AI choosing whether to cast defensively (SPL-006).
        // The slot is already spent, so a failed check or a disrupting hit loses the spell.
        if (!ResolveNPCSpellcastProvocation(npc, spell))
        {
            UpdateAllStatsUI();
            return true;
        }

        BreakInvisibilityOnHostileSpellCast(npc, spell, target, null);

        // ── COUNTERSPELL CHECK (NPC spell cast path) ──
        CounterspellResult npcCounterspellResult = TryResolveCounterspell(npc, spell);
        if (npcCounterspellResult != null && npcCounterspellResult.Success)
        {
            Debug.Log($"[Counterspell] NPC {npc.Stats.CharacterName}'s {spell.Name} was countered! No effect.");
            UpdateAllStatsUI();
            return true; // Spell was cast (slot consumed) but countered
        }

        // Ring of Counterspells (DMG p.230) on the target, as on the PC path (SPL-006).
        if (CounterspellManager.TryRingCounterspell(target, spell))
        {
            UpdateAllStatsUI();
            return true;
        }

        bool skipFriendlyTouchAttackRoll = spell.IsMeleeTouchSpell() && IsFriendlyTarget(npc, target);
        bool forceTargetToFailSave = ShouldForceTargetToAcceptSave(npc, target, spell);

        if (TryHandleMirrorImageSpellTargetAttack(npc, target, spell, out string mirrorSpellLog))
        {
            _lastCombatLog = mirrorSpellLog;
            CombatUI?.ShowCombatLog(_lastCombatLog);
            UpdateAllStatsUI();
            return true;
        }

        // D&D 3.5e: 50% chance targeted spell fails against blinking target (NPC path)
        if (target != null && target != npc && target.HasActiveBlinkEffect
            && spell.TargetType != SpellTargetType.Self
            && spell.TargetType != SpellTargetType.Area)
        {
            int blinkTargetRoll = DiceService.Percentile("Blink NPC target spell failure");
            if (blinkTargetRoll <= 50)
            {
                string targetName = target.Stats != null ? target.Stats.CharacterName : target.name;
                CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("🌀", $"{spell.Name} fails to reach {targetName}! Target is on the Ethereal Plane. (Blink: rolled {blinkTargetRoll} ≤ 50%)"));
                UpdateAllStatsUI();
                return true;
            }
        }

        SpellResult result = SpellCaster.Cast(spell, npc.Stats, target.Stats, null, skipFriendlyTouchAttackRoll, forceTargetToFailSave, npc, target);

        bool appliesTrackedEffect = spell.EffectType == SpellEffectType.Buff || spell.EffectType == SpellEffectType.Debuff ||
                                   spell.EffectType == SpellEffectType.Control || spell.EffectType == SpellEffectType.Illusion ||
                                   spell.EffectType == SpellEffectType.Wall;
        // Same rule as PerformSpellCast (SPL-007): Debuff and Control saves negate, so an NPC's
        // Hold Person no longer paralyzes a PC who saved; Cause Fear and Scare are partial;
        // nonintelligent undead get no save vs Command Undead.
        bool effectNegatedBySave = SpellUtilities.IsEffectNegatedBySave(
            spell, result, target != null && !target.IsIntelligentUndead());

        if (effectNegatedBySave)
            CombatUI?.ShowCombatLog(CombatLogHelper.Defensive("🛡", $"{target.Stats.CharacterName} resists {spell.Name} with a successful {result.SaveType} save."));

        if (result.MindAffectingImmunityBlocked)
            CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("🧠", $"{target.Stats.CharacterName} is immune to mind-affecting effects. {spell.Name} has no effect."));

        // ── Lesser Globe of Invulnerability check ──
        if (target != null && spell != null && result.Success && !effectNegatedBySave)
        {
            if (LesserGlobeOfInvulnerabilityAreaEffect.DoesAnyGlobeBlockSpell(spell, target))
            {
                result.Success = false;
                CombatUI?.ShowCombatLog(CombatLogHelper.Defensive("🛡", $"{spell.Name} (level {spell.SpellLevel}) is blocked by Lesser Globe of Invulnerability! Spell effects of 3rd level or lower cannot affect {target.Stats.CharacterName}."));
            }
        }

        bool handledCauseFear = TryResolveCauseFearSpellEffect(npc, target, spell, result);

        // Scare (Will partial: shaken 1 round on a save) resolves in its own handler, as on the PC path.
        bool handledScare = false;
        if (!handledCauseFear)
            handledScare = TryResolveScareSpellEffect(npc, target, spell, result);

        bool handledRayOfEnfeeblement = false;
        if (!handledCauseFear && !handledScare && result.Success && !effectNegatedBySave)
            handledRayOfEnfeeblement = TryResolveRayOfEnfeeblementSpellEffect(npc, target, spell, result);

        bool handledTouchOfIdiocy = false;
        if (!handledCauseFear && !handledScare && !handledRayOfEnfeeblement && result.Success && !effectNegatedBySave)
            handledTouchOfIdiocy = TryResolveTouchOfIdiocySpellEffect(npc, target, spell, result);

        bool handledMelfsAcidArrow = false;
        if (!handledCauseFear && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && result.Success && !effectNegatedBySave)
            handledMelfsAcidArrow = TryResolveMelfsAcidArrowSpellEffect(npc, target, spell, result);

        bool handledRayOfExhaustion = false;
        if (!handledCauseFear && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && result.Success)
            handledRayOfExhaustion = TryResolveRayOfExhaustionSpellEffect(npc, target, spell, result);

        bool handledVampiricTouch = false;
        if (!handledCauseFear && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && result.Success)
            handledVampiricTouch = TryResolveVampiricTouchSpellEffect(npc, target, spell, result);

        bool handledEnervation = false;
        if (!handledCauseFear && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && result.Success)
            handledEnervation = TryResolveEnervationSpellEffect(npc, target, spell, result);

        bool handledContagion = false;
        if (!handledCauseFear && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && result.Success)
            handledContagion = TryResolveContagionSpellEffect(npc, target, spell, result);

        bool handledBestowCurse = false;
        if (!handledCauseFear && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && result.Success)
            handledBestowCurse = TryResolveBestowCurseSpellEffect(npc, target, spell, result);

        bool handledGreaterInvisibility = false;
        if (!handledCauseFear && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && !handledBestowCurse && result.Success)
            handledGreaterInvisibility = TryResolveGreaterInvisibilitySpellEffect(npc, target, spell, result);

        bool handledPhantasmalKiller = false;
        if (!handledCauseFear && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && !handledBestowCurse && !handledGreaterInvisibility && result.Success)
            handledPhantasmalKiller = TryResolvePhantasmalKillerSpellEffect(npc, target, spell, result);

        bool handledFireShield = false;
        if (!handledCauseFear && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && !handledBestowCurse && !handledGreaterInvisibility && !handledPhantasmalKiller && result.Success)
            handledFireShield = TryResolveFireShieldSpellEffect(npc, target, spell, result);

        bool handledResilientSphere = false;
        if (!handledCauseFear && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && !handledBestowCurse && !handledGreaterInvisibility && !handledPhantasmalKiller && !handledFireShield && result.Success && !effectNegatedBySave)
            handledResilientSphere = TryResolveResilientSphereSpellEffect(npc, target, spell, result);

        bool handledAnimateRope = false;
        if (!handledCauseFear && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && !handledBestowCurse && !handledGreaterInvisibility && !handledPhantasmalKiller && !handledFireShield && !handledResilientSphere)
            handledAnimateRope = TryResolveAnimateRopeSpellEffect(npc, target, spell, result);

        bool handledMirrorImage = false;
        if (!handledCauseFear && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && !handledBestowCurse && !handledGreaterInvisibility && !handledPhantasmalKiller && !handledFireShield && !handledResilientSphere && !handledAnimateRope && result.Success && !effectNegatedBySave)
            handledMirrorImage = TryResolveMirrorImageSpellEffect(npc, target, spell, result);

        bool handledLesserGlobe = false;
        if (!handledCauseFear && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && !handledBestowCurse && !handledGreaterInvisibility && !handledPhantasmalKiller && !handledFireShield && !handledResilientSphere && !handledAnimateRope && !handledMirrorImage && result.Success)
            handledLesserGlobe = TryResolveLesserGlobeSpellEffect(npc, target, spell, result);

        if (!handledCauseFear && !handledScare && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && !handledBestowCurse && !handledGreaterInvisibility && !handledPhantasmalKiller && !handledFireShield && !handledResilientSphere && !handledAnimateRope && !handledMirrorImage && !handledLesserGlobe && result.Success && appliesTrackedEffect && !effectNegatedBySave)
            ApplySpellBuff(npc, target, spell, spellComp);

        if (result.DamageDealt > 0)
            CheckConcentrationOnDamage(target, result.DamageDealt);

        _lastCombatLog = result.GetFormattedLog();
        CombatUI?.ShowCombatLog(_lastCombatLog);

        if (result.TargetKilled)
        {
            target.OnDeath();
            HandleSummonDeathCleanup(target);
        }

        UpdateAllStatsUI();
        return true;
    }

    /// <summary>Delegates to AIService.IsGiantBombardierBeetle for centralized AI utility.</summary>
    private static bool IsGiantBombardierBeetle(CharacterController npc)
        => AIService.IsGiantBombardierBeetle(npc);

    private bool TryNPCUseBombardierAcidSpray(CharacterController npc, CharacterController primaryTarget)
    {
        if (!IsGiantBombardierBeetle(npc) || npc == null || npc.Stats == null || primaryTarget == null || primaryTarget.Stats == null)
            return false;

        if (!npc.HasBombardierAcidSprayReady || !npc.Actions.HasStandardAction)
            return false;

        int distanceSquares = SquareGridUtils.GetDistance(npc.GridPosition, primaryTarget.GridPosition);
        if (distanceSquares > 2)
            return false;

        HashSet<Vector2Int> coneCells = AoESystem.GetConeCells(npc.GridPosition, primaryTarget.GridPosition, 2, Grid);
        if (coneCells == null || coneCells.Count == 0)
            return false;

        List<CharacterController> victims = new List<CharacterController>();
        foreach (Vector2Int pos in coneCells)
        {
            SquareCell cell = Grid != null ? Grid.GetCell(pos) : null;
            if (cell == null || !cell.IsOccupied || cell.Occupant == null)
                continue;

            CharacterController occupant = cell.Occupant;
            if (occupant == npc || occupant.Stats == null || occupant.Stats.IsDead)
                continue;

            if (!victims.Contains(occupant))
                victims.Add(occupant);
        }

        if (victims.Count == 0)
            return false;

        if (!npc.CommitStandardAction())
            return false;

        CombatUI?.ShowCombatLog(CombatLogHelper.SpellEffect("🧪", $"{npc.Stats.CharacterName} unleashes Acid Spray (10-ft cone)!"));

        for (int i = 0; i < victims.Count; i++)
        {
            CharacterController victim = victims[i];
            int rawDamage = 0;
            for (int d = 0; d < 6; d++)
                rawDamage += DiceService.D4("Fireball damage die");

            int saveRoll = DiceService.D20("Fireball Reflex save");
            int saveTotal = saveRoll + victim.Stats.ReflexSave;
            bool saveSuccess = saveTotal >= 12;
            int damageToApply = saveSuccess ? Mathf.FloorToInt(rawDamage * 0.5f) : rawDamage;

            // D&D 3.5e: Blinking creatures take half damage from area attacks
            if (victim.HasActiveBlinkEffect)
                damageToApply = Mathf.Max(damageToApply > 0 ? 1 : 0, damageToApply / 2);

            DamagePacket packet = new DamagePacket
            {
                RawDamage = damageToApply,
                Types = new HashSet<DamageType> { DamageType.Acid },
                AttackTags = DamageBypassTag.None,
                IsRanged = true,
                IsNonlethal = false,
                Source = AttackSource.Other,
                SourceName = "Bombardier Beetle Acid Spray"
            };

            DamageResolutionResult mitigation = victim.Stats.ApplyIncomingDamage(damageToApply, packet);
            int finalDamage = mitigation.FinalDamage;

            string blinkAreaNote = victim.HasActiveBlinkEffect ? " [Blink: halved]" : "";
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"   {victim.Stats.CharacterName}: Reflex d20({saveRoll}) + {victim.Stats.ReflexSave} = {saveTotal} {(saveSuccess ? "SUCCESS" : "FAIL")} | Acid {finalDamage} damage{blinkAreaNote}"));

            if (finalDamage > 0)
                CheckConcentrationOnDamage(victim, finalDamage);

            if (victim.Stats.IsDead)
            {
                victim.OnDeath();
                HandleSummonDeathCleanup(victim);
            }
        }

        int cooldown = DiceService.D4("Acid spray cooldown 1d4");
        npc.ConfigureBombardierAcidSprayCooldown(cooldown);
        CombatUI?.ShowCombatLog(CombatLogHelper.Expired("⏱", $"Acid spray recharges in {cooldown} rounds."));

        UpdateAllStatsUI();
        return true;
    }

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

        RangeInfo npcRangeInfo = CalculateRangeInfo(npc, target);

        CharacterController flankPartner;
        bool isFlanking = CombatUtils.IsAttackerFlanking(npc, target, GetAllCharacters(), out flankPartner);
        int flankBonus = isFlanking ? CombatUtils.FlankingAttackBonus : 0;
        string partnerName = flankPartner != null && flankPartner.Stats != null
            ? flankPartner.Stats.CharacterName
            : null;

        if (TryNPCUseBombardierAcidSpray(npc, target))
        {
            yield return new WaitForSeconds(1.0f);
            yield break;
        }

        // Melee: one step at a time through the shared attack sequence (CMB-102). The split reads
        // the NPC's own weapon, not the PC thrown-mode global (NPCs never throw melee weapons, CMB-096).
        bool npcUsesRangedWeapon = npc.GetEquippedMainWeapon()?.WeaponCat == WeaponCategory.Ranged;
        if (!npcUsesRangedWeapon)
        {
            yield return StartCoroutine(NPCMeleeAttackSequence(npc, target, tryStepManeuver));
            yield break;
        }

        // Ranged (and thrown) attacks are unchanged (CMB-091). The AI's maneuver is tried once
        // first, as before; the ranged kiter's reach gate for it is CMB-104.
        if (tryStepManeuver != null && tryStepManeuver(npc, target))
        {
            yield return new WaitForSeconds(0.8f);
            yield break;
        }

        bool canUseFullAttack = npc.Actions != null
            && npc.Actions.HasFullRoundAction
            && npc.IsTargetInCurrentWeaponRange(target)
            && !npc.HasActiveSlowEffect; // Slow prevents full-round actions (PHB p.280)

        if (canUseFullAttack)
        {
            if (!ResolveRangedAttackAoOForNPCAttackIfProvoked(npc, npcRangeInfo))
            {
                yield return new WaitForSeconds(0.8f);
                yield break;
            }

            npc.Actions.UseFullRoundAction();

            bool isMeleeFearBreakAttack = IsMeleeAttackForTurnUndeadFearBreak(
                npc,
                npc.GetEquippedMainWeapon(),
                npcRangeInfo,
                treatAsThrownAttack: false);
            ProcessTurnUndeadMeleeFearBreak(npc, target, isMeleeFearBreakAttack);

            FullAttackResult fullResult = npc.FullAttack(target, isFlanking, flankBonus, partnerName, npcRangeInfo);
            string flankPrefix = isFlanking
                ? $"⚔ {npc.Stats.CharacterName} gains +2 flanking bonus{(string.IsNullOrEmpty(partnerName) ? "" : $" (with {partnerName})")}.\n"
                : string.Empty;

            _lastCombatLog = flankPrefix + fullResult.GetFullSummary();

            Debug.Log($"[AI][Attack] {npc.Stats.CharacterName} performed full attack: attacks={fullResult.Attacks.Count}, hits={fullResult.HitCount}, totalDamage={fullResult.TotalDamageDealt}");

            if (LogAttacksToConsole)
                LogFullAttackToConsole(fullResult);

            CombatUI.ShowCombatLog(_lastCombatLog);
            UpdateAllStatsUI();

            if (fullResult.TotalDamageDealt > 0)
                CheckConcentrationOnDamage(target, fullResult.TotalDamageDealt);

            // Melee reaction effects (Fire Shield, Thorns, etc.) — generic service call per hit
            if (fullResult.Attacks != null)
            {
                foreach (var atk in fullResult.Attacks)
                {
                    if (atk.Hit && !atk.IsRangedAttack)
                        MeleeReactionService.TriggerReactions(npc, target, atk);
                }
            }

            TryResolveFreeTripFromAttackResults(npc, target, fullResult.Attacks, npcRangeInfo);
            TryResolveImprovedGrabFromAttackResults(npc, target, fullResult.Attacks);

            if (fullResult.TargetKilled)
            {
                HandleSummonDeathCleanup(target);

                if (AreAllPCsDead())
                {
                    CurrentPhase = TurnPhase.CombatOver;
                    CombatUI.SetTurnIndicator("DEFEAT! All heroes have fallen!");
                    CombatUI.SetActionButtonsVisible(false);
                    yield break;
                }

                CombatUI.ShowCombatLog(_lastCombatLog + $"\n{target.Stats.CharacterName} has fallen, but the fight continues!");
            }

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

        bool singleAttackFearBreak = IsMeleeAttackForTurnUndeadFearBreak(
            npc,
            npc.GetEquippedMainWeapon(),
            npcRangeInfo,
            treatAsThrownAttack: false);
        ProcessTurnUndeadMeleeFearBreak(npc, target, singleAttackFearBreak);

        if (!ResolveRangedAttackAoOForNPCAttackIfProvoked(npc, npcRangeInfo))
        {
            yield return new WaitForSeconds(0.8f);
            yield break;
        }

        CombatResult result = npc.Attack(target, isFlanking, flankBonus, partnerName, npcRangeInfo);

        TryResolveFreeTripOnHit(npc, target, result, npcRangeInfo);

        _lastCombatLog = BuildAttackLog(npc, isFlanking, partnerName, result);

        if (LogAttacksToConsole)
            Debug.Log("[Combat] " + _lastCombatLog);

        CombatUI.ShowCombatLog(_lastCombatLog);
        UpdateAllStatsUI();

        if (result.Hit && result.TotalDamage > 0)
            CheckConcentrationOnDamage(target, result.TotalDamage);

        // Melee reaction effects (Fire Shield, Thorns, etc.) — generic service call
        if (result.Hit && !result.IsRangedAttack)
            MeleeReactionService.TriggerReactions(npc, target, result);

        TryResolveImprovedGrabFromAttackResults(npc, target, new List<CombatResult> { result });

        if (result.TargetKilled)
        {
            HandleSummonDeathCleanup(target);

            if (AreAllPCsDead())
            {
                CurrentPhase = TurnPhase.CombatOver;
                CombatUI.SetTurnIndicator("DEFEAT! All heroes have fallen!");
                CombatUI.SetActionButtonsVisible(false);
                yield break;
            }

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
        if (npc.Stats.CurrentHP <= 0)
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

        // Compute affected cells
        HashSet<Vector2Int> cells;
        if (bw.Shape == BreathWeaponShape.Cone)
            cells = AoESystem.GetConeCells(npc.GridPosition, aimTarget.GridPosition, rangeSquares, Grid);
        else
            cells = AoESystem.GetLineCellsToTarget(npc.GridPosition, aimTarget.GridPosition, rangeSquares, Grid);

        if (cells == null || cells.Count == 0)
        {
            Debug.LogWarning($"[AI][BreathWeapon] {npc.Stats.CharacterName} breath weapon produced no cells!");
            yield break;
        }

        // Roll damage: DamageCount × d(DamageDice)
        int totalDamage = 0;
        for (int i = 0; i < bw.DamageCount; i++)
            totalDamage += UnityEngine.Random.Range(1, bw.DamageDice + 1);

        // Gather targets in the area
        List<CharacterController> allChars = GetAllCharacters();
        var targets = new List<CharacterController>();
        for (int i = 0; i < allChars.Count; i++)
        {
            CharacterController c = allChars[i];
            if (c == null || c.Stats == null || c.Stats.IsDead || c == npc)
                continue;
            if (cells.Contains(c.GridPosition))
                targets.Add(c);
        }

        // Build combat log
        var log = new System.Text.StringBuilder();
        log.AppendLine("═══════════════════════════════════");
        log.AppendLine($"🔥 {npc.Stats.CharacterName} unleashes a {bw.RangeFeet}-ft {shapeName} of {bw.DamageType}!");
        log.AppendLine($"  Damage: {bw.DamageCount}d{bw.DamageDice} = {totalDamage}");
        log.AppendLine($"  Save DC {bw.SaveDC} ({(bw.IsReflexSave ? "Reflex" : "Fortitude")}) for half damage");
        log.AppendLine($"  Targets: {targets.Count} creature(s) in {cells.Count} squares");
        log.AppendLine();

        for (int t = 0; t < targets.Count; t++)
        {
            CharacterController target = targets[t];
            log.AppendLine($"  --- {target.Stats.CharacterName} ---");

            // Check damage immunity
            if (target.Stats.DamageImmunities != null && target.Stats.DamageImmunities.Contains(bw.DamageType))
            {
                log.AppendLine($"  IMMUNE to {bw.DamageType}! No damage taken.");
                log.AppendLine();
                continue;
            }

            // Roll saving throw
            int saveBonus = bw.IsReflexSave ? target.Stats.ReflexSave : target.Stats.FortitudeSave;
            int saveRoll = DiceRoller.D20();
            int saveTotal = saveRoll + saveBonus;
            bool saved = saveTotal >= bw.SaveDC;

            int damageTaken = saved ? totalDamage / 2 : totalDamage;

            // Check damage resistance
            if (target.Stats.DamageResistances != null)
            {
                for (int r = 0; r < target.Stats.DamageResistances.Count; r++)
                {
                    var res = target.Stats.DamageResistances[r];
                    if (res != null && res.Type == bw.DamageType && res.Amount > 0)
                    {
                        int reduced = Mathf.Min(damageTaken, res.Amount);
                        damageTaken -= reduced;
                        log.AppendLine($"  Resistance ({bw.DamageType} {res.Amount}): -{reduced} damage");
                        break;
                    }
                }
            }

            string saveResult = saved ? "SAVED" : "FAILED";
            log.AppendLine($"  {(bw.IsReflexSave ? "Reflex" : "Fortitude")} save DC {bw.SaveDC}: d20={saveRoll}+{saveBonus}={saveTotal} - {saveResult}!");

            int hpBefore = target.Stats.CurrentHP;
            if (damageTaken > 0)
                target.Stats.TakeDamage(damageTaken);
            int hpAfter = target.Stats.CurrentHP;

            log.AppendLine($"  Damage: {damageTaken} {bw.DamageType}{(saved ? " (half)" : "")}");
            log.AppendLine($"  {target.Stats.CharacterName}: {hpBefore} → {hpAfter} HP");

            if (target.Stats.IsDead || hpAfter <= -10)
            {
                target.OnDeath();
                HandleSummonDeathCleanup(target);
                log.AppendLine($"  💀 {target.Stats.CharacterName} has been slain!");
            }

            // Check concentration
            if (damageTaken > 0)
                CheckConcentrationOnDamage(target, damageTaken);

            log.AppendLine();
        }

        log.Append("═══════════════════════════════════");

        CombatUI?.ShowCombatLog(log.ToString());
        if (LogAttacksToConsole)
            Debug.Log(log.ToString());

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
            int targetHD = target.Stats.Level > 0 ? target.Stats.Level : 1;

            // Will save
            int roll = DiceRoller.D20();
            int willMod = target.Stats.WillSave;
            int total = roll + willMod;

            if (total >= fp.SaveDC)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"  ✅ {targetName} resists Frightful Presence (Will {roll}+{willMod}={total} vs DC {fp.SaveDC}) — immune for remainder of combat"));
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
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"  😱 {targetName} is <b>Panicked</b> for {durationRounds} rounds! (Will {roll}+{willMod}={total} vs DC {fp.SaveDC}, {targetHD} HD ≤ {fp.HDThresholdForPanic})"));
            }
            else
            {
                // Shaken
                target.ApplyCondition(CombatConditionType.Shaken, durationRounds, dragonName);
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"  😰 {targetName} is <b>Shaken</b> for {durationRounds} rounds! (Will {roll}+{willMod}={total} vs DC {fp.SaveDC})"));
            }
        }

        yield return new WaitForSeconds(0.5f);
    }
}
