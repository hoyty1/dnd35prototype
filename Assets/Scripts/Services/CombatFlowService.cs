using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralized combat flow orchestration service.
/// Owns attack execution pipelines, hit resolution helpers, and combat log generation.
/// </summary>
public class CombatFlowService : MonoBehaviour
{
    private GameManager _gameManager;

    public void Initialize(GameManager gameManager)
    {
        _gameManager = gameManager;
    }

    public void Cleanup()
    {
        _gameManager = null;
    }

    public bool CanPerformWithdraw(CharacterController actor, out string reason)
    {
        reason = string.Empty;

        if (actor == null || actor.Stats == null)
        {
            reason = "No active character";
            return false;
        }

        if (!actor.Actions.HasFullRoundAction)
        {
            reason = "Requires a full-round action";
            return false;
        }

        if (actor.HasTakenFiveFootStep)
        {
            reason = "Cannot withdraw after a 5-foot step";
            return false;
        }

        if (actor.HasCondition(CombatConditionType.Prone))
        {
            reason = "Stand up first";
            return false;
        }

        if (actor.HasCondition(CombatConditionType.Pinned))
        {
            reason = "Pinned creatures cannot withdraw";
            return false;
        }

        if (actor.IsGrappling())
        {
            reason = "Cannot withdraw while grappled";
            return false;
        }

        if (actor.Stats.MovementBlockedByCondition)
        {
            reason = "Movement blocked by condition";
            return false;
        }

        return true;
    }

    // ===== Required extraction API (phase 2.1.6 contract) =====
    public CombatResult ExecuteAttack(CharacterController attacker, CharacterController target, int? attackBab = null, ItemData weaponOverride = null, RangeInfo rangeInfo = null, bool isOffHand = false)
    {
        if (attacker == null || target == null)
            return null;

        List<CharacterController> allCombatants = _gameManager != null
            ? _gameManager.Combat_GetAllCharacters()
            : new List<CharacterController>();

        bool isFlanking = CombatUtils.IsAttackerFlanking(attacker, target, allCombatants, out CharacterController flankPartner);
        int flankBonus = isFlanking ? CombatUtils.FlankingAttackBonus : 0;
        string partnerName = flankPartner != null && flankPartner.Stats != null ? flankPartner.Stats.CharacterName : string.Empty;

        CombatResult result = attacker.Attack(target, isFlanking, flankBonus, partnerName, rangeInfo, attackBab, weaponOverride, 0, isOffHand);
        return result;
    }

    public int RollAttack(int baseAttackBonus, int miscellaneousModifiers)
    {
        int dieRoll = UnityEngine.Random.Range(1, 21);
        return dieRoll + baseAttackBonus + miscellaneousModifiers;
    }

    public bool CheckHit(int attackTotal, int targetAc)
    {
        return attackTotal >= targetAc;
    }

    public int RollDamage(int diceCount, int diceSize, int flatBonus)
    {
        int total = flatBonus;
        for (int i = 0; i < Mathf.Max(1, diceCount); i++)
            total += UnityEngine.Random.Range(1, Mathf.Max(2, diceSize) + 1);
        return Mathf.Max(0, total);
    }

    public int ApplyDamage(CharacterController target, int damage)
    {
        if (target == null || target.Stats == null)
            return 0;

        int before = target.Stats.CurrentHP;
        target.Stats.TakeDamage(Mathf.Max(0, damage));
        return Mathf.Max(0, before - target.Stats.CurrentHP);
    }

    public bool CheckCritical(int dieRoll, int threatRangeMin = 20)
    {
        return dieRoll >= Mathf.Clamp(threatRangeMin, 2, 20);
    }

    public bool ConfirmCritical(int confirmationTotal, int targetAc)
    {
        return confirmationTotal >= targetAc;
    }

    public int CalculateCriticalDamage(int baseDamage, int critMultiplier)
    {
        return Mathf.Max(0, baseDamage * Mathf.Max(2, critMultiplier));
    }

    public string GenerateCombatResult(CharacterController attacker, CombatResult result)
    {
        if (result == null)
            return string.Empty;

        string attackerName = attacker != null && attacker.Stats != null ? attacker.Stats.CharacterName : "Attacker";
        string mode = result.AttackDamageMode == AttackDamageMode.Nonlethal ? "nonlethal" : "lethal";
        return $"🗡 {attackerName} attacks ({mode})\n{result.GetDetailedSummary()}";
    }

    // Convenience wrappers named by extraction spec.
    public void ExecuteIterativeAttacks(CharacterController attacker, CharacterController target, bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo)
        => PerformIterativeSequenceAttack(attacker, target, isFlanking, flankBonus, partnerName, rangeInfo);

    public void ExecuteDualWieldAttacks(CharacterController attacker, CharacterController target, bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo)
        => PerformDualWieldAttack(attacker, target, isFlanking, flankBonus, partnerName, rangeInfo);

    public void ExecuteFlurryOfBlows(CharacterController attacker, CharacterController target, bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo)
        => PerformFlurryOfBlows(attacker, target, isFlanking, flankBonus, partnerName, rangeInfo);

    // ===== Delegated GameManager combat flow =====
    public void PerformPlayerAttack(CharacterController attacker, CharacterController target)
    {
        if (_gameManager == null || attacker == null || target == null)
            return;

        _gameManager.Combat_SetSubPhase(GameManager.PlayerSubPhase.Animating);

        var allCombatants = _gameManager.Combat_GetAllCharacters();
        CharacterController flankPartner;
        bool isFlanking = CombatUtils.IsAttackerFlanking(attacker, target, allCombatants, out flankPartner);
        int flankBonus = isFlanking ? CombatUtils.FlankingAttackBonus : 0;
        string partnerName = flankPartner != null ? flankPartner.Stats.CharacterName : string.Empty;

        if (_gameManager.CombatUI != null)
        {
            string flankIndicator = _gameManager.Combat_BuildFlankingIndicator(isFlanking, flankPartner);
            _gameManager.CombatUI.SetTurnIndicator($"{attacker.Stats.CharacterName} attacks {target.Stats.CharacterName}{flankIndicator}");
        }

        RangeInfo rangeInfo = CalculateRangeInfo(attacker, target);
        if (_gameManager.Combat_GetCurrentAttackType() == GameManager.AttackType.Thrown && rangeInfo != null)
        {
            Debug.Log($"[Attack][Thrown] {attacker.Stats.CharacterName} -> {target.Stats.CharacterName}: distance={rangeInfo.DistanceFeet} ft, increment={rangeInfo.IncrementNumber}, penalty={rangeInfo.Penalty}, inRange={rangeInfo.IsInRange}");
        }

        // Targeting is resolved; clear pending declaration marker.
        _gameManager.Combat_ClearPendingDefensiveAttackSelectionFlag();

        switch (_gameManager.Combat_GetPendingAttackMode())
        {
            case GameManager.PendingAttackMode.Single:
                if (_gameManager.Combat_IsInAttackSequence() && _gameManager.Combat_GetAttackingCharacter() == attacker)
                    PerformIterativeSequenceAttack(attacker, target, isFlanking, flankBonus, partnerName, rangeInfo);
                else
                    PerformSingleAttack(attacker, target, isFlanking, flankBonus, partnerName, rangeInfo);
                break;

            case GameManager.PendingAttackMode.FullAttack:
                _gameManager.Combat_StartFullAttackRetargeting(attacker, target);
                break;

            case GameManager.PendingAttackMode.DualWield:
                PerformDualWieldAttack(attacker, target, isFlanking, flankBonus, partnerName, rangeInfo);
                break;

            case GameManager.PendingAttackMode.FlurryOfBlows:
                PerformFlurryOfBlows(attacker, target, isFlanking, flankBonus, partnerName, rangeInfo);
                break;
        }
    }

    public RangeInfo CalculateRangeInfo(CharacterController attacker, CharacterController target)
    {
        ItemData weapon = attacker != null ? attacker.GetEquippedMainWeapon() : null;
        bool usingThrownAttack = _gameManager != null && _gameManager.Combat_IsUsingThrownAttackMode(attacker, weapon);
        bool isRangedAttack = _gameManager != null && _gameManager.Combat_IsAttackModeRanged(attacker, weapon);

        int sqDist = attacker != null && target != null
            ? attacker.GetMinimumDistanceToTarget(target, chebyshev: false)
            : 0;

        if (isRangedAttack && weapon != null && weapon.RangeIncrement > 0)
        {
            bool isThrownWeapon = usingThrownAttack || (weapon.WeaponCat == WeaponCategory.Ranged && weapon.IsThrown);
            return RangeCalculator.GetRangeInfo(sqDist, weapon.RangeIncrement, isThrownWeapon);
        }

        return RangeCalculator.GetRangeInfo(sqDist, 0, false);
    }

    public string BuildAttackLog(CharacterController attacker, bool isFlanking, string partnerName, CombatResult result)
    {
        if (result == null)
            return string.Empty;

        string attackerName = attacker != null && attacker.Stats != null
            ? attacker.Stats.CharacterName
            : "Attacker";

        string flankLogPrefix = isFlanking
            ? $"⚔ {attackerName} gains +2 flanking bonus{(string.IsNullOrEmpty(partnerName) ? "" : $" (with {partnerName})")}.\n"
            : string.Empty;

        string damageModeLabel = result.AttackDamageMode == AttackDamageMode.Nonlethal ? "nonlethal" : "lethal";
        string damageModePrefix = result.DamageModeAttackPenalty != 0
            ? $"🗡 Attacking with {damageModeLabel} damage ({result.DamageModeAttackPenalty} penalty).\n"
            : $"🗡 Attacking with {damageModeLabel} damage.\n";

        return flankLogPrefix + damageModePrefix + result.GetDetailedSummary();
    }

    private bool ResolveRangedAttackAoOIfProvoked(CharacterController attacker)
    {
        if (_gameManager == null || attacker == null || attacker.Stats == null || attacker.Stats.IsDead)
            return true;

        List<CharacterController> threateningEnemies = ThreatSystem.GetThreateningEnemies(
            attacker.GridPosition,
            attacker,
            _gameManager.Combat_GetAllCharacters());

        threateningEnemies.RemoveAll(enemy => enemy == null || enemy.Stats == null || enemy.Stats.IsDead);

        if (threateningEnemies.Count == 0)
            return true;

        _gameManager.CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} makes a ranged attack while threatened and provokes up to {threateningEnemies.Count} attack(s) of opportunity.");

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

            _gameManager.CombatUI?.ShowCombatLog($"⚔ AoO vs ranged attack: {aooResult.GetDetailedSummary()}");
        }

        if (attacker.Stats.IsDead)
        {
            _gameManager.CombatUI?.ShowCombatLog($"<color=#FF6644>💀 {attacker.Stats.CharacterName} is slain before completing the ranged attack.</color>");
            return false;
        }

        return true;
    }

    private bool IsRangedOrThrownAttack(RangeInfo rangeInfo)
    {
        if (rangeInfo != null)
            return !rangeInfo.IsMelee;

        if (_gameManager == null)
            return false;

        GameManager.AttackType currentType = _gameManager.Combat_GetCurrentAttackType();
        return currentType == GameManager.AttackType.Ranged || currentType == GameManager.AttackType.Thrown;
    }

    public CombatResult ExecuteOffHandAttack(CharacterController attacker, CharacterController target, int attackBab, ItemData offHandWeapon, bool useThrownRange)
    {
        if (_gameManager == null || attacker == null || target == null || offHandWeapon == null)
        {
            Debug.Log("[OffHand] ExecuteOffHandAttack aborted due to null attacker/target/weapon.");
            return null;
        }

        if (attacker.IsTwoHanding())
        {
            Debug.Log($"[OffHand] ExecuteOffHandAttack blocked: {attacker.Stats?.CharacterName ?? "Unknown"} is using a two-handed weapon.");
            string attackerName = attacker.Stats?.CharacterName ?? "Attacker";
            _gameManager.CombatUI?.ShowCombatLog($"⚠ {attackerName} cannot make an off-hand attack while wielding a two-handed weapon.");
            return null;
        }

        bool isFlanking = false;
        int flankBonus = 0;
        string partnerName = string.Empty;
        if (!useThrownRange)
        {
            List<CharacterController> allCombatants = _gameManager.Combat_GetAllCharacters();
            isFlanking = CombatUtils.IsAttackerFlanking(attacker, target, allCombatants, out CharacterController flankPartner);
            flankBonus = isFlanking ? CombatUtils.FlankingAttackBonus : 0;
            partnerName = flankPartner != null ? flankPartner.Stats.CharacterName : string.Empty;
        }

        int sqDist = attacker.GetMinimumDistanceToTarget(target, chebyshev: false);
        RangeInfo rangeInfo = useThrownRange
            ? RangeCalculator.GetRangeInfo(sqDist, offHandWeapon.RangeIncrement, true)
            : RangeCalculator.GetRangeInfo(sqDist, 0, false);

        bool isMeleeFearBreak = _gameManager.Combat_IsMeleeFearBreakAttack(
            attacker,
            offHandWeapon,
            rangeInfo,
            treatAsThrownAttack: useThrownRange);

        _gameManager.Combat_ProcessTurnUndeadMeleeFearBreak(attacker, target, isMeleeFearBreak);

        if (IsRangedOrThrownAttack(rangeInfo) && !ResolveRangedAttackAoOIfProvoked(attacker))
            return null;

        CombatResult result = attacker.Attack(
            target,
            isFlanking,
            flankBonus,
            partnerName,
            rangeInfo,
            attackBab,
            offHandWeapon,
            0,
            true);

        string babLabel = CharacterStats.FormatMod(attackBab);
        string offHandPenaltyInfo = _gameManager.Combat_IsDualWielding()
            ? $", dual-wield penalty {CharacterStats.FormatMod(_gameManager.Combat_GetOffHandPenalty())}"
            : string.Empty;

        string modeLabel = useThrownRange ? "Off-Hand Thrown Attack" : "Off-Hand Attack";
        _gameManager.CombatUI?.ShowCombatLog($"↻ {modeLabel} (BAB {babLabel}{offHandPenaltyInfo}) with {offHandWeapon.Name}");

        string log = BuildAttackLog(attacker, isFlanking, partnerName, result);
        _gameManager.Combat_SetLastCombatLog(log);
        _gameManager.CombatUI?.ShowCombatLog(log);

        if (GameManager.LogAttacksToConsole)
            Debug.Log("[Combat] " + log);

        _gameManager.Combat_UpdateAllStatsUI();
        _gameManager.Combat_ClearHighlights();
        return result;
    }

    private static bool ShouldUseNaturalAttackStep(CharacterController attacker, ItemData attackWeapon)
    {
        return attacker != null
            && attackWeapon == null
            && attacker.Stats != null
            && attacker.Stats.HasNaturalAttacks;
    }

    private static bool TryGetNaturalAttackAtSequenceIndex(CharacterController attacker, int attackIndex, out NaturalAttackDefinition attack)
    {
        attack = null;
        if (attacker == null || attacker.Stats == null || attackIndex < 0)
            return false;

        List<NaturalAttackDefinition> naturalAttacks = attacker.Stats.GetValidNaturalAttacks();
        int currentIndex = 0;
        for (int naturalIndex = 0; naturalIndex < naturalAttacks.Count; naturalIndex++)
        {
            NaturalAttackDefinition naturalAttack = naturalAttacks[naturalIndex];
            int count = Mathf.Max(1, naturalAttack.Count);
            for (int i = 0; i < count; i++)
            {
                if (currentIndex == attackIndex)
                {
                    attack = naturalAttack;
                    return true;
                }

                currentIndex++;
            }
        }

        return false;
    }

    private static int GetSequenceAttackBaseBonus(CharacterController attacker, GameManager.AttackType attackType, int attackIndex)
    {
        if (attackType == GameManager.AttackType.Melee
            && ShouldUseNaturalAttackStep(attacker, attacker != null ? attacker.GetEquippedMainWeapon() : null)
            && TryGetNaturalAttackAtSequenceIndex(attacker, attackIndex, out NaturalAttackDefinition naturalAttack))
        {
            return attacker.Stats.GetNaturalAttackBonus(naturalAttack);
        }

        return attacker != null ? attacker.GetIterativeAttackBAB(attackIndex) : 0;
    }

    public void PerformIterativeSequenceAttack(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo = null)
    {
        if (_gameManager == null)
            return;

        if (!_gameManager.Combat_IsInAttackSequence() || _gameManager.Combat_GetAttackingCharacter() != attacker)
        {
            Debug.LogWarning("[Attack][Sequence] Iterative attack requested without active sequence; falling back to single attack.");
            PerformSingleAttack(attacker, target, isFlanking, flankBonus, partnerName, rangeInfo);
            return;
        }

        ItemData attackWeapon = _gameManager.Combat_GetCurrentAttackType() == GameManager.AttackType.Thrown
            ? (_gameManager.Combat_GetEquippedWeapon() ?? attacker.GetEquippedMainWeapon())
            : attacker.GetEquippedMainWeapon();

        bool isMeleeFearBreakAttack = _gameManager.Combat_IsMeleeFearBreakAttack(
            attacker,
            attackWeapon,
            rangeInfo,
            treatAsThrownAttack: _gameManager.Combat_GetCurrentAttackType() == GameManager.AttackType.Thrown);

        _gameManager.Combat_ProcessTurnUndeadMeleeFearBreak(attacker, target, isMeleeFearBreakAttack);

        if (IsRangedOrThrownAttack(rangeInfo) && !ResolveRangedAttackAoOIfProvoked(attacker))
        {
            _gameManager.Combat_EndAttackSequence();
            return;
        }

        bool useNaturalFullAttackStep = _gameManager.Combat_GetCurrentAttackType() == GameManager.AttackType.Melee
            && ShouldUseNaturalAttackStep(attacker, attackWeapon);

        CombatResult result;
        string attackModeLog;
        int attackNumber = _gameManager.Combat_GetTotalAttacksUsed() + 1;

        if (useNaturalFullAttackStep)
        {
            int naturalAttackIndex = _gameManager.Combat_GetTotalAttacksUsed();
            FullAttackResult naturalStep = attacker.FullAttack(
                target,
                isFlanking,
                flankBonus,
                partnerName,
                rangeInfo,
                startAttackIndex: naturalAttackIndex,
                maxAttacks: 1);

            if (naturalStep == null || naturalStep.Attacks == null || naturalStep.Attacks.Count == 0)
            {
                Debug.LogWarning($"[Attack][Sequence] Natural attack step produced no attacks for {attacker.Stats.CharacterName} at index {naturalAttackIndex}; ending sequence.");
                _gameManager.Combat_EndAttackSequence();
                _gameManager.Combat_ShowActionChoices();
                return;
            }

            result = naturalStep.Attacks[0];
            _gameManager.Combat_TryResolveFreeTripOnHit(attacker, target, result, rangeInfo);

            string naturalLabel = (naturalStep.AttackLabels != null && naturalStep.AttackLabels.Count > 0)
                ? naturalStep.AttackLabels[0]
                : "Natural attack";
            attackModeLog = $"↻ Attack #{attackNumber}/{_gameManager.Combat_GetTotalAttackBudget()} (Melee) {naturalLabel}";
        }
        else
        {
            result = attacker.Attack(
                target,
                isFlanking,
                flankBonus,
                partnerName,
                rangeInfo,
                _gameManager.Combat_GetCurrentAttackBAB(),
                attackWeapon);

            _gameManager.Combat_TryResolveFreeTripOnHit(attacker, target, result, rangeInfo);
            _gameManager.Combat_ResolveThrownWeaponAfterAttack(attacker, target, attackWeapon);

            string modeLabel = _gameManager.Combat_GetCurrentAttackType() == GameManager.AttackType.Thrown ? "Thrown" : "Melee";
            string dwPenaltyInfo = _gameManager.Combat_IsDualWielding()
                && (_gameManager.Combat_GetCurrentAttackType() == GameManager.AttackType.Melee || _gameManager.Combat_GetCurrentAttackType() == GameManager.AttackType.Thrown)
                    ? $", dual-wield penalty {CharacterStats.FormatMod(_gameManager.Combat_GetMainHandPenalty())}"
                    : string.Empty;

            attackModeLog = $"↻ Attack #{attackNumber}/{_gameManager.Combat_GetTotalAttackBudget()} ({modeLabel}) at BAB {CharacterStats.FormatMod(_gameManager.Combat_GetCurrentAttackBAB())}{dwPenaltyInfo}";
        }

        _gameManager.CombatUI?.ShowCombatLog(attackModeLog);

        string attackLog = BuildAttackLog(attacker, isFlanking, partnerName, result);
        _gameManager.Combat_SetLastCombatLog(attackLog);
        _gameManager.CombatUI?.ShowCombatLog(attackLog);

        if (GameManager.LogAttacksToConsole)
            Debug.Log("[Combat] " + attackLog);

        _gameManager.Combat_UpdateAllStatsUI();
        _gameManager.Combat_ClearHighlights();

        if (result.Hit && result.TotalDamage > 0)
            _gameManager.Combat_CheckConcentrationOnDamage(target, result.TotalDamage);

        if (result.TargetKilled)
        {
            _gameManager.Combat_HandleSummonDeathCleanup(target);
            if (target.Team == CharacterTeam.Enemy)
            {
                _gameManager.Combat_UpdateAllStatsUI();
                if (_gameManager.Combat_AreAllNPCsDead())
                {
                    _gameManager.Combat_EndAttackSequence();
                    _gameManager.Combat_SetPhase(GameManager.TurnPhase.CombatOver);
                    _gameManager.CombatUI?.SetTurnIndicator("VICTORY! All enemies defeated!");
                    _gameManager.CombatUI?.SetActionButtonsVisible(false);
                    return;
                }

                _gameManager.CombatUI?.ShowCombatLog(attackLog + $"\n⚔️ {target.Stats.CharacterName} is slain! {_gameManager.Combat_GetAliveNPCCount()} enemies remain.");
            }
        }

        _gameManager.Combat_SetTotalAttacksUsed(_gameManager.Combat_GetTotalAttacksUsed() + 1);

        if (_gameManager.Combat_GetTotalAttacksUsed() == 1
            && !_gameManager.Combat_GetAttackSequenceConsumesFullRound()
            && _gameManager.Combat_GetTotalAttackBudget() > 1)
        {
            if (attacker.Actions != null && attacker.Actions.HasMoveAction)
            {
                attacker.Actions.UseMoveAction();
                _gameManager.Combat_SetAttackSequenceConsumesFullRound(true);
            }
            else
            {
                _gameManager.Combat_SetTotalAttackBudget(_gameManager.Combat_GetTotalAttacksUsed());
                Debug.LogWarning("[Attack][Sequence] Could not consume move action for full-round conversion; trimming attack budget.");
            }
        }

        if (_gameManager.Combat_HasMoreAttacksAvailable())
        {
            int nextAttackIndex = _gameManager.Combat_GetTotalAttacksUsed();
            int nextBaseBab = GetSequenceAttackBaseBonus(attacker, _gameManager.Combat_GetCurrentAttackType(), nextAttackIndex);
            int nextBab = nextBaseBab;
            if (_gameManager.Combat_IsDualWielding()
                && (_gameManager.Combat_GetCurrentAttackType() == GameManager.AttackType.Melee || _gameManager.Combat_GetCurrentAttackType() == GameManager.AttackType.Thrown))
            {
                nextBab += _gameManager.Combat_GetMainHandPenalty();
            }

            _gameManager.Combat_SetCurrentAttackBAB(nextBab);
        }
        else
        {
            _gameManager.Combat_EndAttackSequence();
        }

        _gameManager.Combat_StartAfterAttackDelay(attacker, 1.5f);
    }

    private static void PreserveSingleNaturalAttackActionEconomy(
        CharacterController attacker,
        bool moveActionWasAvailableBeforeAttack,
        bool moveActionUsedBeforeAttack,
        bool fullRoundActionUsedBeforeAttack,
        bool standardConvertedToMoveBeforeAttack)
    {
        if (attacker == null || attacker.Actions == null)
            return;

        // Guardrail: selecting a single natural-weapon attack (e.g., Bite) should consume only
        // a standard action. If any internal path accidentally toggles full-round/move state,
        // restore the pre-attack movement economy for this turn.
        if (!fullRoundActionUsedBeforeAttack && attacker.Actions.FullRoundActionUsed)
        {
            Debug.LogWarning($"[Attack][NaturalSingle] Restoring action economy for {attacker.Stats?.CharacterName}: clearing unintended FullRoundActionUsed flag.");
            attacker.Actions.FullRoundActionUsed = false;
        }

        if (!attacker.Actions.SingleActionOnly
            && moveActionWasAvailableBeforeAttack
            && !moveActionUsedBeforeAttack
            && attacker.Actions.MoveActionUsed)
        {
            Debug.LogWarning($"[Attack][NaturalSingle] Restoring move action for {attacker.Stats?.CharacterName} after single natural attack.");
            attacker.Actions.MoveActionUsed = false;
        }

        if (!standardConvertedToMoveBeforeAttack && attacker.Actions.StandardConvertedToMove)
        {
            Debug.LogWarning($"[Attack][NaturalSingle] Clearing unintended StandardConvertedToMove flag for {attacker.Stats?.CharacterName}.");
            attacker.Actions.StandardConvertedToMove = false;
        }
    }

    public void PerformSingleAttack(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo = null)
    {
        if (_gameManager == null || attacker == null || target == null)
            return;

        bool moveActionWasAvailableBeforeAttack = attacker.Actions != null && attacker.Actions.HasMoveAction;
        bool moveActionUsedBeforeAttack = attacker.Actions != null && attacker.Actions.MoveActionUsed;
        bool fullRoundActionUsedBeforeAttack = attacker.Actions != null && attacker.Actions.FullRoundActionUsed;
        bool standardConvertedToMoveBeforeAttack = attacker.Actions != null && attacker.Actions.StandardConvertedToMove;

        bool skipStandardCommit = _gameManager.Combat_ConsumeSkipNextSingleAttackStandardActionCommitFlag();
        if (!skipStandardCommit)
        {
            if (!attacker.CommitStandardAction())
            {
                _gameManager.CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} has no standard action available.");
                _gameManager.Combat_ShowActionChoices();
                return;
            }
        }
        else
        {
            Debug.Log("[Attack][Thrown] Skipping standard action consumption for follow-up thrown attack after ending iterative melee sequence.");
        }

        ItemData attackWeapon = _gameManager.Combat_GetCurrentAttackType() == GameManager.AttackType.Thrown
            ? (_gameManager.Combat_GetEquippedWeapon() ?? attacker.GetEquippedMainWeapon())
            : attacker.GetEquippedMainWeapon();

        bool isMeleeFearBreakAttack = _gameManager.Combat_IsMeleeFearBreakAttack(
            attacker,
            attackWeapon,
            rangeInfo,
            treatAsThrownAttack: _gameManager.Combat_GetCurrentAttackType() == GameManager.AttackType.Thrown);

        _gameManager.Combat_ProcessTurnUndeadMeleeFearBreak(attacker, target, isMeleeFearBreakAttack);

        if (IsRangedOrThrownAttack(rangeInfo) && !ResolveRangedAttackAoOIfProvoked(attacker))
            return;

        CombatResult result;
        string naturalAttackModeLog = null;

        bool useSelectedNaturalAttack = _gameManager.Combat_HasPendingNaturalAttackSelection()
            && _gameManager.Combat_GetCurrentAttackType() == GameManager.AttackType.Melee
            && attackWeapon == null
            && attacker.Stats != null
            && attacker.Stats.HasNaturalAttacks;

        if (useSelectedNaturalAttack)
        {
            int naturalAttackIndex = Mathf.Max(0, _gameManager.Combat_GetPendingNaturalAttackSequenceIndex());
            FullAttackResult naturalStep = attacker.FullAttack(
                target,
                isFlanking,
                flankBonus,
                partnerName,
                rangeInfo,
                startAttackIndex: naturalAttackIndex,
                maxAttacks: 1);

            if (naturalStep != null && naturalStep.Attacks != null && naturalStep.Attacks.Count > 0)
            {
                result = naturalStep.Attacks[0];
                string naturalLabel = _gameManager.Combat_GetPendingNaturalAttackLabel();
                if (string.IsNullOrWhiteSpace(naturalLabel))
                {
                    naturalLabel = naturalStep.AttackLabels != null && naturalStep.AttackLabels.Count > 0
                        ? naturalStep.AttackLabels[0]
                        : "Natural attack";
                }

                naturalAttackModeLog = $"↻ Natural Attack ({naturalLabel})";
            }
            else
            {
                result = attacker.Attack(target, isFlanking, flankBonus, partnerName, rangeInfo, null, attackWeapon);
            }

            _gameManager.Combat_ClearPendingNaturalAttackSelection();
        }
        else
        {
            result = attacker.Attack(target, isFlanking, flankBonus, partnerName, rangeInfo, null, attackWeapon);
        }

        _gameManager.Combat_TryResolveFreeTripOnHit(attacker, target, result, rangeInfo);
        _gameManager.Combat_ResolveThrownWeaponAfterAttack(attacker, target, attackWeapon);

        if (!string.IsNullOrEmpty(naturalAttackModeLog))
            _gameManager.CombatUI?.ShowCombatLog(naturalAttackModeLog);

        string log = BuildAttackLog(attacker, isFlanking, partnerName, result);
        _gameManager.Combat_SetLastCombatLog(log);

        if (GameManager.LogAttacksToConsole)
            Debug.Log("[Combat] " + log);

        _gameManager.CombatUI?.ShowCombatLog(log);
        _gameManager.Combat_UpdateAllStatsUI();
        _gameManager.Combat_ClearHighlights();

        if (result.Hit && result.TotalDamage > 0)
            _gameManager.Combat_CheckConcentrationOnDamage(target, result.TotalDamage);

        if (result.TargetKilled)
        {
            _gameManager.Combat_HandleSummonDeathCleanup(target);
            if (target.Team == CharacterTeam.Enemy)
            {
                _gameManager.Combat_UpdateAllStatsUI();
                if (_gameManager.Combat_AreAllNPCsDead())
                {
                    _gameManager.Combat_SetPhase(GameManager.TurnPhase.CombatOver);
                    _gameManager.CombatUI?.SetTurnIndicator("VICTORY! All enemies defeated!");
                    _gameManager.CombatUI?.SetActionButtonsVisible(false);
                    return;
                }

                _gameManager.CombatUI?.ShowCombatLog(log + $"\n⚔️ {target.Stats.CharacterName} is slain! {_gameManager.Combat_GetAliveNPCCount()} enemies remain.");
            }
        }

        if (useSelectedNaturalAttack)
        {
            PreserveSingleNaturalAttackActionEconomy(
                attacker,
                moveActionWasAvailableBeforeAttack,
                moveActionUsedBeforeAttack,
                fullRoundActionUsedBeforeAttack,
                standardConvertedToMoveBeforeAttack);
        }

        // Single-attack natural weapon actions should always return to main action choices
        // (move + 5-foot-step may remain available). Clear any stale attack-sequence state.
        _gameManager.Combat_EndAttackSequence();

        bool waitingOnImprovedGrabPrompt = _gameManager.Combat_TryResolveImprovedGrabAfterSingleAttack(
            attacker,
            target,
            result,
            onResolved: () => _gameManager.Combat_StartAfterAttackDelay(attacker, 1.5f));

        if (waitingOnImprovedGrabPrompt)
            return;

        _gameManager.Combat_StartAfterAttackDelay(attacker, 1.5f);
    }

    public void PerformFullAttack(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo = null)
    {
        if (_gameManager == null || attacker == null || target == null)
            return;

        attacker.Actions.UseFullRoundAction();

        bool isMeleeFearBreak = _gameManager.Combat_IsMeleeFearBreakAttack(attacker, attacker.GetEquippedMainWeapon(), rangeInfo, false);
        _gameManager.Combat_ProcessTurnUndeadMeleeFearBreak(attacker, target, isMeleeFearBreak);

        FullAttackResult result = attacker.FullAttack(target, isFlanking, flankBonus, partnerName, rangeInfo);
        string flankPrefix = isFlanking
            ? $"⚔ {attacker.Stats.CharacterName} gains +2 flanking bonus{(string.IsNullOrEmpty(partnerName) ? "" : $" (with {partnerName})")}.\n"
            : string.Empty;

        string log = flankPrefix + result.GetFullSummary();
        _gameManager.Combat_SetLastCombatLog(log);

        if (GameManager.LogAttacksToConsole)
            LogFullAttackToConsole(result);

        _gameManager.CombatUI?.ShowCombatLog(log);
        _gameManager.Combat_UpdateAllStatsUI();
        _gameManager.Combat_ClearHighlights();

        if (result.TotalDamageDealt > 0)
            _gameManager.Combat_CheckConcentrationOnDamage(target, result.TotalDamageDealt);

        if (result.Attacks != null)
        {
            for (int i = 0; i < result.Attacks.Count; i++)
            {
                _gameManager.Combat_TryResolveFreeTripOnHit(attacker, target, result.Attacks[i], rangeInfo);
                if (target == null || target.Stats == null || target.Stats.IsDead || target.HasCondition(CombatConditionType.Prone))
                    break;
            }
        }

        if (result.TargetKilled)
        {
            _gameManager.Combat_HandleSummonDeathCleanup(target);
            if (target.Team == CharacterTeam.Enemy)
            {
                _gameManager.Combat_UpdateAllStatsUI();
                if (_gameManager.Combat_AreAllNPCsDead())
                {
                    _gameManager.Combat_SetPhase(GameManager.TurnPhase.CombatOver);
                    _gameManager.CombatUI?.SetTurnIndicator("VICTORY! All enemies defeated!");
                    _gameManager.CombatUI?.SetActionButtonsVisible(false);
                    return;
                }
            }
        }

        _gameManager.Combat_StartDelayedEndActivePCTurn(2.0f);
    }

    public void PerformDualWieldAttack(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo = null)
    {
        if (_gameManager == null || attacker == null || target == null)
            return;

        attacker.Actions.UseFullRoundAction();

        bool isMeleeFearBreak = _gameManager.Combat_IsMeleeFearBreakAttack(attacker, attacker.GetEquippedMainWeapon(), rangeInfo, false);
        _gameManager.Combat_ProcessTurnUndeadMeleeFearBreak(attacker, target, isMeleeFearBreak);

        FullAttackResult result = attacker.DualWieldAttack(target, isFlanking, flankBonus, partnerName, rangeInfo);
        string flankPrefix = isFlanking
            ? $"⚔ {attacker.Stats.CharacterName} gains +2 flanking bonus{(string.IsNullOrEmpty(partnerName) ? "" : $" (with {partnerName})")}.\n"
            : string.Empty;

        string log = flankPrefix + result.GetFullSummary();
        _gameManager.Combat_SetLastCombatLog(log);

        if (GameManager.LogAttacksToConsole)
            LogFullAttackToConsole(result);

        _gameManager.CombatUI?.ShowCombatLog(log);
        _gameManager.Combat_UpdateAllStatsUI();
        _gameManager.Combat_ClearHighlights();

        if (result.TotalDamageDealt > 0)
            _gameManager.Combat_CheckConcentrationOnDamage(target, result.TotalDamageDealt);

        if (result.TargetKilled)
        {
            _gameManager.Combat_HandleSummonDeathCleanup(target);
            if (target.Team == CharacterTeam.Enemy)
            {
                _gameManager.Combat_UpdateAllStatsUI();
                if (_gameManager.Combat_AreAllNPCsDead())
                {
                    _gameManager.Combat_SetPhase(GameManager.TurnPhase.CombatOver);
                    _gameManager.CombatUI?.SetTurnIndicator("VICTORY! All enemies defeated!");
                    _gameManager.CombatUI?.SetActionButtonsVisible(false);
                    return;
                }
            }
        }

        _gameManager.Combat_StartDelayedEndActivePCTurn(2.0f);
    }

    public void PerformFlurryOfBlows(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo = null)
    {
        if (_gameManager == null || attacker == null || target == null)
            return;

        attacker.Actions.UseFullRoundAction();

        bool isMeleeFearBreak = _gameManager.Combat_IsMeleeFearBreakAttack(attacker, attacker.GetEquippedMainWeapon(), rangeInfo, false);
        _gameManager.Combat_ProcessTurnUndeadMeleeFearBreak(attacker, target, isMeleeFearBreak);

        FullAttackResult result = attacker.FlurryOfBlows(target, isFlanking, flankBonus, partnerName, rangeInfo);
        string flankPrefix = isFlanking
            ? $"⚔ {attacker.Stats.CharacterName} gains +2 flanking bonus{(string.IsNullOrEmpty(partnerName) ? "" : $" (with {partnerName})")}.\n"
            : string.Empty;

        string log = flankPrefix + result.GetFullSummary();
        _gameManager.Combat_SetLastCombatLog(log);

        if (GameManager.LogAttacksToConsole)
            LogFullAttackToConsole(result);

        _gameManager.CombatUI?.ShowCombatLog(log);
        _gameManager.Combat_UpdateAllStatsUI();
        _gameManager.Combat_ClearHighlights();

        if (result.TotalDamageDealt > 0)
            _gameManager.Combat_CheckConcentrationOnDamage(target, result.TotalDamageDealt);

        if (result.TargetKilled)
        {
            _gameManager.Combat_HandleSummonDeathCleanup(target);
            if (target.Team == CharacterTeam.Enemy)
            {
                _gameManager.Combat_UpdateAllStatsUI();
                if (_gameManager.Combat_AreAllNPCsDead())
                {
                    _gameManager.Combat_SetPhase(GameManager.TurnPhase.CombatOver);
                    _gameManager.CombatUI?.SetTurnIndicator("VICTORY! All enemies defeated!");
                    _gameManager.CombatUI?.SetActionButtonsVisible(false);
                    return;
                }
            }
        }

        _gameManager.Combat_StartDelayedEndActivePCTurn(2.0f);
    }

    public void LogFullAttackToConsole(FullAttackResult result)
    {
        if (result == null || result.Attacker == null || result.Defender == null)
            return;

        string attackerName = result.Attacker.Stats.CharacterName;
        string defenderName = result.Defender.Stats.CharacterName;

        Debug.Log("[Combat] ═══════════════════════════════════════");
        Debug.Log($"[Combat] {attackerName} attacks {defenderName}");
        Debug.Log($"[Combat] SUMMARY: {result.HitCount}/{result.Attacks.Count} hits, {result.TotalDamageDealt} total damage");
        Debug.Log($"[Combat] {defenderName}: {result.DefenderHPBefore} → {result.DefenderHPAfter} HP");
        if (result.TargetKilled)
            Debug.Log($"[Combat] {defenderName} has been slain!");
        Debug.Log("[Combat] ═══════════════════════════════════════");
    }
}
