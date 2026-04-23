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

        CombatResult result = attacker.Attack(
            target,
            isFlanking,
            flankBonus,
            partnerName,
            rangeInfo,
            _gameManager.Combat_GetCurrentAttackBAB(),
            attackWeapon);

        _gameManager.Combat_TryResolveFreeTripOnHit(attacker, target, result, rangeInfo);
        _gameManager.Combat_ResolveThrownWeaponAfterAttack(attacker, target, attackWeapon);

        int attackNumber = _gameManager.Combat_GetTotalAttacksUsed() + 1;
        string modeLabel = _gameManager.Combat_GetCurrentAttackType() == GameManager.AttackType.Thrown ? "Thrown" : "Melee";
        string dwPenaltyInfo = _gameManager.Combat_IsDualWielding()
            && (_gameManager.Combat_GetCurrentAttackType() == GameManager.AttackType.Melee || _gameManager.Combat_GetCurrentAttackType() == GameManager.AttackType.Thrown)
                ? $", dual-wield penalty {CharacterStats.FormatMod(_gameManager.Combat_GetMainHandPenalty())}"
                : string.Empty;

        _gameManager.CombatUI?.ShowCombatLog($"↻ Attack #{attackNumber}/{_gameManager.Combat_GetTotalAttackBudget()} ({modeLabel}) at BAB {CharacterStats.FormatMod(_gameManager.Combat_GetCurrentAttackBAB())}{dwPenaltyInfo}");

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
            if (!target.IsPlayerControlled)
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
            int nextBaseBab = attacker.GetIterativeAttackBAB(_gameManager.Combat_GetTotalAttacksUsed());
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

    public void PerformSingleAttack(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo = null)
    {
        if (_gameManager == null || attacker == null || target == null)
            return;

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

        CombatResult result = attacker.Attack(target, isFlanking, flankBonus, partnerName, rangeInfo, null, attackWeapon);
        _gameManager.Combat_TryResolveFreeTripOnHit(attacker, target, result, rangeInfo);
        _gameManager.Combat_ResolveThrownWeaponAfterAttack(attacker, target, attackWeapon);

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
            if (!target.IsPlayerControlled)
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

        if (result.TargetKilled)
        {
            _gameManager.Combat_HandleSummonDeathCleanup(target);
            if (!target.IsPlayerControlled)
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
            if (!target.IsPlayerControlled)
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
            if (!target.IsPlayerControlled)
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
