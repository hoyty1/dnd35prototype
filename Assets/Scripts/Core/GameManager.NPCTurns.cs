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
                CombatUI?.ShowCombatLog($"⏭ {npc.Stats.CharacterName} {reason} and cannot act this turn.");
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
            CombatUI.ShowCombatLog($"<color=#66E8FF>{GetSummonDisplayName(summon)} protects {data.Caster.Stats.CharacterName}.</color>");
        }

        CharacterController target = SelectSummonTargetByCommand(summon, data);
        if (target == null)
            yield break;

        bool lowHP = summon.Stats != null && summon.Stats.TotalMaxHP > 0 && summon.Stats.CurrentHP <= Mathf.CeilToInt(summon.Stats.TotalMaxHP * 0.30f);

        if (lowHP && _aiService != null)
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
                CombatUI.ShowCombatLog($"<color=#FFCC66>{GetSummonDisplayName(summon)} withdraws to survive.</color>");
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
                CombatUI.ShowCombatLog($"<color=#66E8FF>{GetSummonDisplayName(summon)} closes in on {target.Stats.CharacterName}.</color>");
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

        if (summon.Stats != null && summon.Stats.HasTripAttack && !target.Stats.IsProne && summon.Actions.HasStandardAction)
        {
            var trip = summon.ExecuteSpecialAttack(SpecialAttackType.Trip, target);
            CombatUI.ShowCombatLog($"<color=#66E8FF>✦ {GetSummonDisplayName(summon)} attempts Trip: {trip.Log}</color>");

            // Fire Shield retribution: trip is a melee maneuver
            if (target != null && target.Stats != null && target.Stats.FireShieldActive)
                ResolveFireShieldRetribution(target, summon);

            summon.CommitStandardAction();
            UpdateAllStatsUI();
            yield return new WaitForSeconds(0.65f);
            yield break;
        }

        if (TryExecuteSummonSmiteAttack(summon, target, data))
        {
            UpdateAllStatsUI();
            yield return new WaitForSeconds(0.8f);
            yield break;
        }

        yield return StartCoroutine(NPCPerformAttack(summon, target));
    }

    private CharacterController SelectSummonTargetByCommand(CharacterController summon, ActiveSummonInstance summonData)
    {
        if (summon == null)
            return null;

        List<CharacterController> enemies = new List<CharacterController>();
        foreach (var candidate in GetAllCharacters())
        {
            if (candidate == null || candidate == summon || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!IsEnemyTeam(summon, candidate))
                continue;
            enemies.Add(candidate);
        }

        if (enemies.Count == 0)
            return null;

        SummonCommandType cmd = summonData != null && summonData.CurrentCommand != null
            ? summonData.CurrentCommand.Type
            : SummonCommandType.AttackNearest;

        switch (cmd)
        {
            case SummonCommandType.ProtectCaster:
                return FindEnemyNearestToSummoner(enemies, summonData != null ? summonData.Caster : null, summon);
            case SummonCommandType.AttackNearest:
            default:
                return FindNearestEnemyToSummon(enemies, summon);
        }
    }

    private CharacterController FindNearestEnemyToSummon(List<CharacterController> enemies, CharacterController summon)
    {
        CharacterController nearest = null;
        int nearestDist = int.MaxValue;

        for (int i = 0; i < enemies.Count; i++)
        {
            CharacterController enemy = enemies[i];
            int dist = SquareGridUtils.GetDistance(summon.GridPosition, enemy.GridPosition);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = enemy;
            }
        }

        return nearest;
    }

    private CharacterController FindEnemyNearestToSummoner(List<CharacterController> enemies, CharacterController summoner, CharacterController summon)
    {
        if (summoner == null)
            return FindNearestEnemyToSummon(enemies, summon);

        CharacterController nearest = null;
        int nearestDist = int.MaxValue;

        for (int i = 0; i < enemies.Count; i++)
        {
            CharacterController enemy = enemies[i];
            int dist = SquareGridUtils.GetDistance(summoner.GridPosition, enemy.GridPosition);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = enemy;
            }
        }

        return nearest;
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
        CombatUI.ShowCombatLog($"<color=#FFD280>✦ {GetSummonDisplayName(summon)} uses Smite {targetAxis}! {result.GetDetailedSummary()}</color>");

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

        SpecialAttackResult tripResult = attacker.ExecuteSpecialAttack(SpecialAttackType.Trip, target);
        string tripContext = tripResult.Success
            ? "free trip follow-up"
            : "free trip attempt failed";

        CombatUI?.ShowCombatLog($"☠ {attacker.Stats.CharacterName} follows up with Trip ({tripContext}): {tripResult.Log}");
        Debug.Log($"[NPC Trip Follow-up] {attacker.Stats.CharacterName} triggered free trip after hit. Success={tripResult.Success}");

        // Fire Shield retribution: free trip follow-up is a melee maneuver
        if (target != null && target.Stats != null && target.Stats.FireShieldActive)
            ResolveFireShieldRetribution(target, attacker);
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

        if (!npc.Actions.HasStandardAction && !hasCoupOption)
            return false;

        SpecialAttackType? choice = forcedChoice;

        if (choice.HasValue && !npc.CanPerformSpecialAttack(choice.Value))
        {
            string npcName = npc.Stats != null ? npc.Stats.CharacterName : "<unknown>";
            Debug.Log($"[AI][SpecialAttack] {npcName} cannot perform forced {choice.Value} while in {npc.GetPrimaryWeaponType()} mode.");
            return false;
        }

        if (choice == SpecialAttackType.Grapple && hasImprovedGrab)
        {
            string npcName = npc.Stats != null ? npc.Stats.CharacterName : "<unknown>";
            Debug.Log($"[AI][SpecialAttack] {npcName} has Improved Grab; refusing forced standard Grapple action.");
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

        var result = npc.ExecuteSpecialAttack(choice.Value, target);
        CombatUI.ShowCombatLog($"☠ {npc.Stats.CharacterName} uses SPECIAL [{choice.Value}]! {result.Log}");

        // Fire Shield retribution: trip and disarm are melee maneuvers that involve physical contact
        if ((choice.Value == SpecialAttackType.Trip || choice.Value == SpecialAttackType.Disarm) &&
            target != null && target.Stats != null && target.Stats.FireShieldActive)
        {
            ResolveFireShieldRetribution(target, npc);
        }

        if (result.Success)
        {
            if (choice.Value == SpecialAttackType.BullRushAttack || choice.Value == SpecialAttackType.BullRushCharge)
                ResolveBullRushPushAndFollow(npc, target, result, onComplete: null);
            else if (choice.Value == SpecialAttackType.Overrun)
                TryPushTargetAway(npc, target, 1, allowAttackerFollow: true);
        }

        if (choice.Value == SpecialAttackType.CoupDeGrace)
            npc.Actions.UseFullRoundAction();
        else
            npc.CommitStandardAction();

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

        CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} makes a ranged attack while threatened and provokes up to {threateningEnemies.Count} attack(s) of opportunity.");

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

            CombatUI?.ShowCombatLog($"⚔ AoO vs ranged attack: {aooResult.GetDetailedSummary()}");
        }

        if (attacker.Stats.IsDead)
        {
            CombatUI?.ShowCombatLog($"<color=#FF6644>💀 {attacker.Stats.CharacterName} is slain before completing the ranged attack.</color>");
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
            CombatUI?.ShowCombatLog($"↷ {attacker?.Stats?.CharacterName ?? "Attacker"} declines to start a grapple.");
            onResolved?.Invoke();
            yield break;
        }

        SpecialAttackResult grabResult = attacker.ResolveImprovedGrabFreeAttempt(target);
        string attackName = !string.IsNullOrWhiteSpace(attackResult?.WeaponName) ? attackResult.WeaponName : "trigger attack";
        CombatUI?.ShowCombatLog($"🪢 Improved Grab ({attackName} hit): {grabResult.Log}");
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
        CombatUI?.ShowCombatLog($"🪢 Improved Grab ({attackName} hit): {grabResult.Log}");
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
            CombatUI?.ShowCombatLog($"🪢 Improved Grab ({attackResult.WeaponName} hit): {grabResult.Log}");

            if (grabResult.Success || target.Stats.IsDead)
                break;
        }
    }

    private FullAttackResult PerformNPCFullAttackWithAdaptiveRetargeting(
        CharacterController npc,
        CharacterController initialTarget,
        DND35.AI.AIProfile profile)
    {
        var aggregate = new FullAttackResult
        {
            Type = FullAttackResult.AttackType.FullAttack,
            Attacker = npc,
            Defender = initialTarget,
            DefenderHPBefore = initialTarget != null && initialTarget.Stats != null ? initialTarget.Stats.CurrentHP : 0
        };

        if (npc == null || npc.Stats == null || initialTarget == null || initialTarget.Stats == null)
            return aggregate;

        RangeInfo initialRangeInfo = CalculateRangeInfo(npc, initialTarget);
        int plannedAttackCount = npc.GetPlannedFullAttackCount(initialRangeInfo);
        if (plannedAttackCount <= 0)
        {
            CombatUI?.ShowCombatLog($"⚠ {npc.Stats.CharacterName} has no available full-attack steps.");
            aggregate.DefenderHPAfter = initialTarget.Stats.CurrentHP;
            aggregate.TargetKilled = initialTarget.Stats.IsDead;
            return aggregate;
        }

        CharacterController currentTarget = initialTarget;
        int attacksMade = 0;
        int targetSwitchCount = 0;

        for (int attackIndex = 0; attackIndex < plannedAttackCount; attackIndex++)
        {
            if (npc == null || npc.Stats == null || npc.Stats.IsDead || CurrentPhase == TurnPhase.CombatOver)
                break;

            bool needsNewTarget = currentTarget == null
                || currentTarget.Stats == null
                || currentTarget.Stats.IsDead
                || (profile != null && profile.ShouldIgnoreUnconsciousTargets(npc) && currentTarget.IsUnconscious)
                || !IsTargetInCurrentWeaponRange(npc, currentTarget);

            if (needsNewTarget)
            {
                CharacterController inReachTarget = SelectBestAdaptiveFullAttackTarget(npc, profile, requireInRange: true);
                if (inReachTarget != null)
                {
                    currentTarget = inReachTarget;
                    targetSwitchCount++;
                    CombatUI?.ShowCombatLog($"🎯 {npc.Stats.CharacterName} shifts focus to {currentTarget.Stats.CharacterName}.");
                }
                else
                {
                    CharacterController steppedTarget = null;
                    bool stepped = profile != null
                        && profile.ShouldTakeFiveFootStepToContinueFullAttack(npc)
                        && TryTakeFiveFootStepForAdaptiveFullAttack(npc, profile, out steppedTarget);

                    if (stepped)
                    {
                        currentTarget = steppedTarget;
                        targetSwitchCount++;
                        CombatUI?.ShowCombatLog($"🎯 {npc.Stats.CharacterName} re-engages {currentTarget.Stats.CharacterName} after a 5-foot step.");
                    }
                    else
                    {
                        int remainingAttacks = plannedAttackCount - attackIndex;
                        CombatUI?.ShowCombatLog($"↩ {npc.Stats.CharacterName} has no valid active targets for {remainingAttacks} remaining attack(s).");
                        break;
                    }
                }
            }

            if (currentTarget == null || currentTarget.Stats == null)
                break;

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

            FullAttackResult stepResult = npc.FullAttack(
                currentTarget,
                isFlanking,
                flankBonus,
                partnerName,
                rangeInfo,
                startAttackIndex: attackIndex,
                maxAttacks: 1);

            if (stepResult == null || stepResult.Attacks == null || stepResult.Attacks.Count == 0)
                break;

            CombatResult attack = stepResult.Attacks[0];
            string label = (stepResult.AttackLabels != null && stepResult.AttackLabels.Count > 0)
                ? stepResult.AttackLabels[0]
                : $"Attack {attackIndex + 1}";

            aggregate.Attacks.Add(attack);
            aggregate.AttackLabels.Add(label);
            attacksMade++;

            CombatUI?.ShowCombatLog(attack.GetAttackBreakdown(label));

            if (attack.Hit && attack.TotalDamage > 0)
                CheckConcentrationOnDamage(currentTarget, attack.TotalDamage);

            // Fire Shield retribution: defender's Fire Shield damages melee attacker
            if (attack.Hit && !attack.IsRangedAttack && currentTarget != null && currentTarget.Stats.FireShieldActive)
                ResolveFireShieldRetribution(currentTarget, npc);

            TryResolveFreeTripFromAttackResults(npc, currentTarget, stepResult.Attacks, rangeInfo);
            TryResolveImprovedGrabFromAttackResults(npc, currentTarget, stepResult.Attacks);

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

                int attacksRemainingAfterKill = plannedAttackCount - (attackIndex + 1);
                if (attacksRemainingAfterKill > 0)
                    CombatUI?.ShowCombatLog($"💀 {currentTarget.Stats.CharacterName} is defeated! {attacksRemainingAfterKill} attack(s) remaining.");

                currentTarget = null;
                continue;
            }

            if (profile != null && profile.ShouldIgnoreUnconsciousTargets(npc) && currentTarget.IsUnconscious)
            {
                int attacksRemainingAfterDrop = plannedAttackCount - (attackIndex + 1);
                if (attacksRemainingAfterDrop > 0)
                    CombatUI?.ShowCombatLog($"💤 {currentTarget.Stats.CharacterName} drops unconscious! {npc.Stats.CharacterName} looks for another active target.");

                currentTarget = null;
            }
        }

        aggregate.DefenderHPAfter = aggregate.Defender != null && aggregate.Defender.Stats != null
            ? aggregate.Defender.Stats.CurrentHP
            : aggregate.DefenderHPBefore;
        aggregate.TargetKilled = aggregate.Defender != null && aggregate.Defender.Stats != null && aggregate.Defender.Stats.IsDead;

        _lastCombatLog = $"✅ {npc.Stats.CharacterName} completes adaptive full attack ({attacksMade}/{plannedAttackCount} attacks, {aggregate.TotalDamageDealt} total damage, {targetSwitchCount} target switch(es)).";
        CombatUI?.ShowCombatLog(_lastCombatLog);

        return aggregate;
    }

    private CharacterController SelectBestAdaptiveFullAttackTarget(
        CharacterController attacker,
        DND35.AI.AIProfile profile,
        bool requireInRange)
    {
        if (attacker == null || attacker.Stats == null)
            return null;

        var enemies = new List<CharacterController>();
        bool hasConsciousEnemy = false;

        foreach (CharacterController candidate in GetAllCharacters())
        {
            if (candidate == null || candidate == attacker || candidate.Stats == null || candidate.Stats.IsDead)
                continue;

            if (!IsEnemyTeam(attacker, candidate))
                continue;

            enemies.Add(candidate);
            if (!candidate.IsUnconscious)
                hasConsciousEnemy = true;
        }

        bool ignoreUnconscious = profile != null
            && profile.ShouldIgnoreUnconsciousTargets(attacker)
            && hasConsciousEnemy;

        var candidates = new List<CharacterController>();
        for (int i = 0; i < enemies.Count; i++)
        {
            CharacterController candidate = enemies[i];
            if (ignoreUnconscious && candidate.IsUnconscious)
                continue;

            if (requireInRange && !IsTargetInCurrentWeaponRange(attacker, candidate))
                continue;

            candidates.Add(candidate);
        }

        if (candidates.Count == 0)
            return null;

        if (_aiService != null)
        {
            CharacterController profiled = _aiService.SelectBestTarget(attacker, candidates);
            if (profiled != null)
                return profiled;
        }

        CharacterController best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < candidates.Count; i++)
        {
            CharacterController candidate = candidates[i];
            float score = profile != null ? profile.ScoreTarget(candidate, attacker) : 0f;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

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

            if (!IsEnemyTeam(attacker, candidate))
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

        SpellcastingComponent spellComp = npc.GetComponent<SpellcastingComponent>();
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
                CombatUI?.ShowCombatLog($"⚡ {npc.Stats.CharacterName}'s {spell.Name} fizzles! (Blink spell failure: rolled {blinkNpcRoll} ≤ 20%)");
                UpdateAllStatsUI();
                return true;
            }
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
                CombatUI?.ShowCombatLog($"🌀 {spell.Name} fails to reach {targetName}! Target is on the Ethereal Plane. (Blink: rolled {blinkTargetRoll} ≤ 50%)");
                UpdateAllStatsUI();
                return true;
            }
        }

        SpellResult result = SpellCaster.Cast(spell, npc.Stats, target.Stats, null, skipFriendlyTouchAttackRoll, forceTargetToFailSave, npc, target);

        bool appliesTrackedEffect = spell.EffectType == SpellEffectType.Buff || spell.EffectType == SpellEffectType.Debuff;
        bool causeFearSaveReduced = IsCauseFearSpell(spell) && result.RequiredSave && result.SaveSucceeded;
        bool blurSaveNegated = spell != null
                               && string.Equals(spell.SpellId, SpellNames.BLUR, StringComparison.Ordinal)
                               && result.RequiredSave
                               && result.SaveSucceeded;

        // D&D 3.5e PHB p.211: Command Undead — nonintelligent undead get no saving throw.
        bool commandUndeadNoSaveOverrideNPC = spell != null
            && spell.SpellId == SpellNames.COMMAND_UNDEAD
            && target != null && !target.IsIntelligentUndead();

        bool effectNegatedBySave = (spell.EffectType == SpellEffectType.Debuff || blurSaveNegated)
                                   && result.RequiredSave
                                   && result.SaveSucceeded
                                   && !causeFearSaveReduced
                                   && !commandUndeadNoSaveOverrideNPC;

        if (effectNegatedBySave)
            CombatUI?.ShowCombatLog($"🛡 {target.Stats.CharacterName} resists {spell.Name} with a successful {result.SaveType} save.");

        if (result.MindAffectingImmunityBlocked)
            CombatUI?.ShowCombatLog($"🧠 {target.Stats.CharacterName} is immune to mind-affecting effects. {spell.Name} has no effect.");

        bool handledCauseFear = TryResolveCauseFearSpellEffect(npc, target, spell, result);
        bool handledRayOfEnfeeblement = false;
        if (!handledCauseFear && result.Success && !effectNegatedBySave)
            handledRayOfEnfeeblement = TryResolveRayOfEnfeeblementSpellEffect(npc, target, spell, result);

        bool handledTouchOfIdiocy = false;
        if (!handledCauseFear && !handledRayOfEnfeeblement && result.Success && !effectNegatedBySave)
            handledTouchOfIdiocy = TryResolveTouchOfIdiocySpellEffect(npc, target, spell, result);

        bool handledMelfsAcidArrow = false;
        if (!handledCauseFear && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && result.Success && !effectNegatedBySave)
            handledMelfsAcidArrow = TryResolveMelfsAcidArrowSpellEffect(npc, target, spell, result);

        bool handledRayOfExhaustion = false;
        if (!handledCauseFear && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && result.Success)
            handledRayOfExhaustion = TryResolveRayOfExhaustionSpellEffect(npc, target, spell, result);

        bool handledVampiricTouch = false;
        if (!handledCauseFear && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && result.Success)
            handledVampiricTouch = TryResolveVampiricTouchSpellEffect(npc, target, spell, result);

        bool handledEnervation = false;
        if (!handledCauseFear && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && result.Success)
            handledEnervation = TryResolveEnervationSpellEffect(npc, target, spell, result);

        bool handledContagion = false;
        if (!handledCauseFear && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && result.Success)
            handledContagion = TryResolveContagionSpellEffect(npc, target, spell, result);

        bool handledBestowCurse = false;
        if (!handledCauseFear && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && result.Success)
            handledBestowCurse = TryResolveBestowCurseSpellEffect(npc, target, spell, result);

        bool handledGreaterInvisibility = false;
        if (!handledCauseFear && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && !handledBestowCurse && result.Success)
            handledGreaterInvisibility = TryResolveGreaterInvisibilitySpellEffect(npc, target, spell, result);

        bool handledPhantasmalKiller = false;
        if (!handledCauseFear && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && !handledBestowCurse && !handledGreaterInvisibility && result.Success)
            handledPhantasmalKiller = TryResolvePhantasmalKillerSpellEffect(npc, target, spell, result);

        bool handledFireShield = false;
        if (!handledCauseFear && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && !handledBestowCurse && !handledGreaterInvisibility && !handledPhantasmalKiller && result.Success)
            handledFireShield = TryResolveFireShieldSpellEffect(npc, target, spell, result);

        bool handledResilientSphere = false;
        if (!handledCauseFear && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && !handledBestowCurse && !handledGreaterInvisibility && !handledPhantasmalKiller && !handledFireShield && result.Success && !effectNegatedBySave)
            handledResilientSphere = TryResolveResilientSphereSpellEffect(npc, target, spell, result);

        bool handledAnimateRope = false;
        if (!handledCauseFear && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && !handledBestowCurse && !handledGreaterInvisibility && !handledPhantasmalKiller && !handledFireShield && !handledResilientSphere)
            handledAnimateRope = TryResolveAnimateRopeSpellEffect(npc, target, spell, result);

        bool handledMirrorImage = false;
        if (!handledCauseFear && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && !handledBestowCurse && !handledGreaterInvisibility && !handledPhantasmalKiller && !handledFireShield && !handledResilientSphere && !handledAnimateRope && result.Success && !effectNegatedBySave)
            handledMirrorImage = TryResolveMirrorImageSpellEffect(npc, target, spell, result);

        if (!handledCauseFear && !handledRayOfEnfeeblement && !handledTouchOfIdiocy && !handledMelfsAcidArrow && !handledRayOfExhaustion && !handledVampiricTouch && !handledEnervation && !handledContagion && !handledBestowCurse && !handledGreaterInvisibility && !handledPhantasmalKiller && !handledFireShield && !handledResilientSphere && !handledAnimateRope && !handledMirrorImage && result.Success && appliesTrackedEffect && !effectNegatedBySave)
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

    private static bool IsGiantBombardierBeetle(CharacterController npc)
    {
        if (npc?.Stats == null)
            return false;

        return string.Equals(npc.Stats.SourceNpcDefinitionId, "giant_bombardier_beetle", StringComparison.OrdinalIgnoreCase);
    }

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

        CombatUI?.ShowCombatLog($"🧪 {npc.Stats.CharacterName} unleashes Acid Spray (10-ft cone)!");

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
            CombatUI?.ShowCombatLog($"   {victim.Stats.CharacterName}: Reflex d20({saveRoll}) + {victim.Stats.ReflexSave} = {saveTotal} {(saveSuccess ? "SUCCESS" : "FAIL")} | Acid {finalDamage} damage{blinkAreaNote}");

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
        CombatUI?.ShowCombatLog($"⏱ Acid spray recharges in {cooldown} rounds.");

        UpdateAllStatsUI();
        return true;
    }

    private IEnumerator NPCPerformAttack(CharacterController npc, CharacterController target)
    {
        if (npc == null || npc.Stats == null)
            yield break;

        if (target == null || target.Stats == null || target.Stats.IsDead)
        {
            CombatUI?.ShowCombatLog($"⚠ {npc.Stats.CharacterName} has no valid target and stops attacking.");
            yield break;
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

            CombatUI.ShowCombatLog($"⚠ {npc.Stats.CharacterName} cannot attack: {cannotAttackReason}");
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

            DND35.AI.AIProfile activeProfile = npc.aiProfile;
            bool canSwitchMidAttack = activeProfile != null
                && activeProfile.ShouldSwitchTargetsMidFullAttack(npc)
                && !IsAttackModeRanged(npc);

            if (canSwitchMidAttack)
            {
                FullAttackResult switchedResult = PerformNPCFullAttackWithAdaptiveRetargeting(npc, target, activeProfile);

                Debug.Log($"[AI][Attack] {npc.Stats.CharacterName} performed adaptive full attack: attacks={switchedResult.Attacks.Count}, hits={switchedResult.HitCount}, totalDamage={switchedResult.TotalDamageDealt}");

                if (LogAttacksToConsole)
                    LogFullAttackToConsole(switchedResult);

                UpdateAllStatsUI();
                yield return new WaitForSeconds(1.0f);
                yield break;
            }

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

            // Fire Shield retribution: trigger for each melee hit in full attack
            if (target != null && target.Stats.FireShieldActive)
            {
                foreach (var atk in fullResult.Attacks)
                {
                    if (atk.Hit && !atk.IsRangedAttack)
                        ResolveFireShieldRetribution(target, npc);
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
            CombatUI.ShowCombatLog($"⚠ {npc.Stats.CharacterName} has no standard action available.");
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

        // Fire Shield retribution: defender's Fire Shield damages melee attacker
        if (result.Hit && !result.IsRangedAttack && target != null && target.Stats.FireShieldActive)
            ResolveFireShieldRetribution(target, npc);

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
        CombatUI?.ShowCombatLog($"{npcName} loses track of {targetName} after {missCount} failed attacks on the last known square and stops blind-firing.");
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

        CombatUI?.ShowCombatLog($"{npcName} rushes to search after missing {targetName}'s last known position.");

        yield return StartCoroutine(MoveCharacterAlongComputedPath(npc, searchCell.Coords, PlayerMoveSecondsPerStep));

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
            var spellComp = character.GetComponent<SpellcastingComponent>();
            if (spellComp != null)
            {
                spellComp.ResetQuickenedSpellTracking();
            }
        }
        Debug.Log("[GameManager] Quickened spell tracking reset for new round");
    }

}
