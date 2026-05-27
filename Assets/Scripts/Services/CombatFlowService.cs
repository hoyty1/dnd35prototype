using System;
using System.Collections;
using System.Collections.Generic;
using DND35.Magic;
using DND35e.Identifiers;
using UnityEngine;
using Random = UnityEngine.Random;

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
        int dieRoll = DiceRoller.D20();
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

        // ── Resilient Sphere boundary block (PHB p.263) ──
        // Nothing passes through the sphere boundary. Attacks between creatures
        // separated by a sphere boundary are blocked. Creatures inside the SAME
        // sphere can attack each other normally.
        if (ResilientSphereAreaEffect.DoesSphereBlockInteraction(attacker, target))
        {
            bool attackerInSphere = ResilientSphereAreaEffect.IsCharacterInAnySphere(attacker);
            string msg = attackerInSphere
                ? $"<color=#44CCFF>🔮 {attacker.Stats.CharacterName}'s attack cannot pass through the Resilient Sphere!</color>"
                : $"<color=#44CCFF>🔮 Attack on {target.Stats.CharacterName} is blocked by Resilient Sphere! Nothing can pass through.</color>";
            _gameManager.CombatUI?.ShowCombatLog(msg);
            _gameManager.Combat_ShowActionChoices();
            return;
        }

        // D&D 3.5e Sanctuary (PHB p.274): If the warded creature makes an attack, the spell ends.
        BreakProtectiveWardsOnAttack(attacker);

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

            // Far Shot feat: multiply effective range increment (×1.5 projectile, ×2 thrown)
            int effectiveRangeIncrement = weapon.RangeIncrement;
            if (attacker != null && attacker.Stats != null && FeatManager.HasFarShot(attacker.Stats))
            {
                float multiplier = FeatManager.GetFarShotRangeMultiplier(attacker.Stats, isThrownWeapon);
                effectiveRangeIncrement = Mathf.RoundToInt(weapon.RangeIncrement * multiplier);
            }

            return RangeCalculator.GetRangeInfo(sqDist, effectiveRangeIncrement, isThrownWeapon);
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
        string strengthPenaltyLabel = GetEnfeeblementStrengthPenaltyLabel(attacker);
        string damageModePrefix = result.DamageModeAttackPenalty != 0
            ? $"🗡 Attacking with {damageModeLabel} damage ({result.DamageModeAttackPenalty} penalty){strengthPenaltyLabel}.\n"
            : $"🗡 Attacking with {damageModeLabel} damage{strengthPenaltyLabel}.\n";

        return flankLogPrefix + damageModePrefix + result.GetDetailedSummary();
    }

    private static string GetEnfeeblementStrengthPenaltyLabel(CharacterController attacker)
    {
        if (attacker == null || attacker.Stats == null)
            return string.Empty;

        int penalty = Mathf.Max(0, attacker.Stats.EnfeeblementStrengthPenalty);
        return penalty > 0 ? $" (Str -{penalty})" : string.Empty;
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

        _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attacker.Stats.CharacterName} makes a ranged attack while threatened and provokes up to {threateningEnemies.Count} attack(s) of opportunity."));

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

            _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Buff("⚔", $"AoO vs ranged attack: {aooResult.GetDetailedSummary()}"));
        }

        if (attacker.Stats.IsDead)
        {
            _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Interrupted("💀", $"{attacker.Stats.CharacterName} is slain before completing the ranged attack."));
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

    /// <summary>
    /// Find an alive enemy adjacent to the attacker for Cleave/Great Cleave.
    /// Excludes the original target (already dead). Returns null if none found.
    /// </summary>
    private CharacterController FindAdjacentEnemy(CharacterController attacker, CharacterController excludeTarget)
    {
        if (_gameManager == null || attacker == null)
            return null;

        var allChars = _gameManager.Combat_GetAllCharacters();
        if (allChars == null)
            return null;

        foreach (var candidate in allChars)
        {
            if (candidate == null || candidate == attacker || candidate == excludeTarget)
                continue;
            if (candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (candidate.Team == attacker.Team)
                continue; // Skip allies
            if (!SquareGridUtils.IsAdjacent(attacker.GridPosition, candidate.GridPosition))
                continue;

            return candidate;
        }

        return null;
    }

    private void LogDeathPoint(string context, CharacterController attacker, CharacterController target)
    {
        string attackerName = attacker != null && attacker.Stats != null ? attacker.Stats.CharacterName : "<null>";
        string targetName = target != null && target.Stats != null ? target.Stats.CharacterName : "<null>";
        bool targetDead = target != null && target.Stats != null && target.Stats.IsDead;
        int targetHp = target != null && target.Stats != null ? target.Stats.CurrentHP : 0;
        Debug.Log($"[DeathFlow] {context} | attacker={attackerName} | target={targetName} | targetDead={targetDead} | targetHP={targetHp} | phase={(_gameManager != null ? _gameManager.CurrentPhase.ToString() : "<no-gm>")}");
    }

    private bool TryHandleVictoryAfterEnemyDeath(string context, CharacterController attacker, CharacterController target)
    {
        if (_gameManager == null)
            return false;

        LogDeathPoint($"{context}:ENTRY", attacker, target);

        if (target == null || target.Stats == null)
        {
            Debug.Log($"[VictoryCheck] {context} skip: target is null.");
            return false;
        }

        if (target.Team != CharacterTeam.Enemy)
        {
            Debug.Log($"[VictoryCheck] {context} skip: target is not enemy (team={target.Team}).");
            return false;
        }

        int aliveBefore = _gameManager.Combat_GetAliveNPCCount();
        Debug.Log($"[VictoryCheck] {context} before check | aliveEnemies={aliveBefore}");

        bool handled = _gameManager.Combat_CheckCombatVictory(context, target);

        int aliveAfter = _gameManager.Combat_GetAliveNPCCount();
        Debug.Log($"[VictoryCheck] {context} after check | handled={handled} | aliveEnemies={aliveAfter} | phase={_gameManager.CurrentPhase} | waitingLoot={_gameManager.WaitingForLootCollection}");
        return handled;
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
            _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attackerName} cannot make an off-hand attack while wielding a two-handed weapon."));
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
        _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("↻", $"{modeLabel} (BAB {babLabel}{offHandPenaltyInfo}) with {offHandWeapon.Name}"));

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
            Debug.Log("[AttackFlow] Early return in PerformIterativeSequenceAttack: attacker died/aborted from AoO before attack resolution.");
            _gameManager.Combat_EndAttackSequence();
            return;
        }

        // Ammo check for projectile weapons in iterative sequence
        if (attackWeapon != null && attackWeapon.IsProjectileWeapon && !CheckAmmoAvailable(attacker, attackWeapon))
        {
            Debug.Log("[AttackFlow] No ammo available, ending attack sequence.");
            _gameManager.Combat_EndAttackSequence();
            _gameManager.Combat_ShowActionChoices();
            return;
        }

        bool useNaturalFullAttackStep = _gameManager.Combat_GetCurrentAttackType() == GameManager.AttackType.Melee
            && ShouldUseNaturalAttackStep(attacker, attackWeapon);

        CombatResult result;
        string attackModeLog;
        int attackNumber = _gameManager.Combat_GetTotalAttacksUsed() + 1;
        string strengthPenaltySuffix = GetEnfeeblementStrengthPenaltyLabel(attacker);

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
            attackModeLog = $"↻ Attack #{attackNumber}/{_gameManager.Combat_GetTotalAttackBudget()} (Melee{strengthPenaltySuffix}) {naturalLabel}";
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

            // Consume ammunition for projectile weapons (1 per shot in iterative sequence)
            if (attackWeapon != null && attackWeapon.IsProjectileWeapon)
                ConsumeAmmoForAttack(attacker, attackWeapon);

            string modeLabel = _gameManager.Combat_GetCurrentAttackType() == GameManager.AttackType.Thrown ? "Thrown" : "Melee";
            string dwPenaltyInfo = _gameManager.Combat_IsDualWielding()
                && (_gameManager.Combat_GetCurrentAttackType() == GameManager.AttackType.Melee || _gameManager.Combat_GetCurrentAttackType() == GameManager.AttackType.Thrown)
                    ? $", dual-wield penalty {CharacterStats.FormatMod(_gameManager.Combat_GetMainHandPenalty())}"
                    : string.Empty;

            attackModeLog = $"↻ Attack #{attackNumber}/{_gameManager.Combat_GetTotalAttackBudget()} ({modeLabel}{strengthPenaltySuffix}) at BAB {CharacterStats.FormatMod(_gameManager.Combat_GetCurrentAttackBAB())}{dwPenaltyInfo}";
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

        // Melee reaction effects (Fire Shield, Thorns, etc.) — generic service call
        if (result.Hit && !result.IsRangedAttack)
            MeleeReactionService.TriggerReactions(attacker, target, result);

        if (result.TargetKilled)
        {
            LogDeathPoint("PerformIterativeSequenceAttack:TargetKilled", attacker, target);
            _gameManager.Combat_HandleSummonDeathCleanup(target);
            if (target.Team == CharacterTeam.Enemy)
            {
                _gameManager.Combat_UpdateAllStatsUI();
                Debug.Log("[AttackFlow] Attack sequence ended");
                Debug.Log("[AttackFlow] Target died, checking victory");
                if (TryHandleVictoryAfterEnemyDeath("PerformIterativeSequenceAttack", attacker, target))
                {
                    _gameManager.Combat_EndAttackSequence();
                    return;
                }

                _gameManager.CombatUI?.ShowCombatLog(attackLog + $"\n⚔️ {target.Stats.CharacterName} is slain! {_gameManager.Combat_GetAliveNPCCount()} enemies remain.");
            }
        }

        _gameManager.Combat_SetTotalAttacksUsed(_gameManager.Combat_GetTotalAttacksUsed() + 1);
        _gameManager.Combat_RegisterWeaponAttackCommitted(attacker);

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
            Debug.Log("[AttackFlow] No more attacks available. Ending attack sequence.");
            _gameManager.Combat_EndAttackSequence();
        }

        Debug.Log("[AttackFlow] Scheduling AfterAttackDelay from PerformIterativeSequenceAttack.");
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
        bool wasFirstWeaponAttackBeforeThis = _gameManager.Combat_GetWeaponAttacksCommittedThisTurn() <= 0;

        bool skipStandardCommit = _gameManager.Combat_ConsumeSkipNextSingleAttackStandardActionCommitFlag();
        bool isAdditionalProgressiveAttack = _gameManager.Combat_GetWeaponAttacksCommittedThisTurn() >= 1;

        if (isAdditionalProgressiveAttack)
        {
            if (!_gameManager.Combat_TryEnterProgressiveFullAttackStage(attacker, "a follow-up attack"))
            {
                _gameManager.Combat_ShowActionChoices();
                return;
            }
        }
        else if (!skipStandardCommit)
        {
            if (!attacker.CommitStandardAction())
            {
                _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attacker.Stats.CharacterName} has no standard action available."));
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

        // Ammo check for projectile weapons (before attack resolution)
        if (attackWeapon != null && attackWeapon.IsProjectileWeapon && !CheckAmmoAvailable(attacker, attackWeapon))
        {
            _gameManager.Combat_ShowActionChoices();
            return;
        }

        CombatResult result;
        string naturalAttackModeLog = null;
        int selectedNaturalAttackIndex = -1;

        bool useSelectedNaturalAttack = _gameManager.Combat_HasPendingNaturalAttackSelection()
            && _gameManager.Combat_GetCurrentAttackType() == GameManager.AttackType.Melee
            && attackWeapon == null
            && attacker.Stats != null
            && attacker.Stats.HasNaturalAttacks;

        if (useSelectedNaturalAttack)
        {
            int naturalAttackIndex = Mathf.Max(0, _gameManager.Combat_GetPendingNaturalAttackSequenceIndex());
            selectedNaturalAttackIndex = naturalAttackIndex;
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

        // Consume ammunition for projectile weapons (1 per shot)
        if (attackWeapon != null && attackWeapon.IsProjectileWeapon)
            ConsumeAmmoForAttack(attacker, attackWeapon);

        _gameManager.Combat_RegisterWeaponAttackCommitted(attacker);

        if (useSelectedNaturalAttack)
            _gameManager.Combat_MarkNaturalAttackSequenceIndexUsed(selectedNaturalAttackIndex);

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

        // Melee reaction effects (Fire Shield, Thorns, etc.) — generic service call
        if (result.Hit && !result.IsRangedAttack)
            MeleeReactionService.TriggerReactions(attacker, target, result);

        if (result.TargetKilled)
        {
            LogDeathPoint("PerformSingleAttack:TargetKilled", attacker, target);
            _gameManager.Combat_HandleSummonDeathCleanup(target);
            if (target.Team == CharacterTeam.Enemy)
            {
                _gameManager.Combat_UpdateAllStatsUI();
                if (TryHandleVictoryAfterEnemyDeath("PerformSingleAttack", attacker, target))
                    return;

                _gameManager.CombatUI?.ShowCombatLog(log + $"\n⚔️ {target.Stats.CharacterName} is slain! {_gameManager.Combat_GetAliveNPCCount()} enemies remain.");
            }

            // === CLEAVE / GREAT CLEAVE ===
            // After dropping a foe with a melee attack, grant bonus attack(s) against adjacent enemies.
            if (!result.IsRangedAttack && attacker.Stats != null &&
                (FeatManager.HasCleave(attacker.Stats) || FeatManager.HasGreatCleave(attacker.Stats)))
            {
                bool isGreatCleave = FeatManager.HasGreatCleave(attacker.Stats);
                bool cleaving = true;
                int cleaveCount = 0;

                while (cleaving)
                {
                    // Find an alive enemy adjacent to the attacker (within melee reach)
                    CharacterController cleaveTarget = FindAdjacentEnemy(attacker, target);
                    if (cleaveTarget == null)
                    {
                        if (cleaveCount == 0)
                            Debug.Log($"[Cleave] {attacker.Stats.CharacterName} has no adjacent enemies for cleave.");
                        break;
                    }

                    cleaveCount++;
                    string cleaveFeatName = isGreatCleave ? "Great Cleave" : "Cleave";
                    _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Buff("⚔", $"️ {cleaveFeatName}! {attacker.Stats.CharacterName} strikes {cleaveTarget.Stats.CharacterName}!"));

                    // Cleave attack uses the same weapon at full BAB
                    CombatResult cleaveResult = attacker.Attack(cleaveTarget, false, 0, null, null, null, attackWeapon);
                    string cleaveLog = BuildAttackLog(attacker, false, null, cleaveResult);
                    _gameManager.CombatUI?.ShowCombatLog(cleaveLog);

                    if (cleaveResult.Hit && cleaveResult.TotalDamage > 0)
                        _gameManager.Combat_CheckConcentrationOnDamage(cleaveTarget, cleaveResult.TotalDamage);

                    if (cleaveResult.Hit && !cleaveResult.IsRangedAttack)
                        MeleeReactionService.TriggerReactions(attacker, cleaveTarget, cleaveResult);

                    if (cleaveResult.TargetKilled)
                    {
                        LogDeathPoint("Cleave:TargetKilled", attacker, cleaveTarget);
                        _gameManager.Combat_HandleSummonDeathCleanup(cleaveTarget);
                        _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Buff("⚔", $"️ {cleaveTarget.Stats.CharacterName} is slain by {cleaveFeatName}!"));
                        _gameManager.Combat_UpdateAllStatsUI();

                        if (TryHandleVictoryAfterEnemyDeath("Cleave", attacker, cleaveTarget))
                            return;

                        // Great Cleave: continue cleaving. Regular Cleave: stop after first.
                        if (!isGreatCleave)
                            cleaving = false;
                    }
                    else
                    {
                        // Cleave target survived — stop cleaving
                        cleaving = false;
                    }
                }
            }
        }

        if (useSelectedNaturalAttack && wasFirstWeaponAttackBeforeThis)
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
                var attack = result.Attacks[i];
                _gameManager.Combat_TryResolveFreeTripOnHit(attacker, target, attack, rangeInfo);

                // Melee reaction effects (Fire Shield, Thorns, etc.) — generic service call
                if (attack.Hit && !attack.IsRangedAttack)
                    MeleeReactionService.TriggerReactions(attacker, target, attack);

                if (target == null || target.Stats == null || target.Stats.IsDead || target.HasCondition(CombatConditionType.Prone))
                    break;
            }
        }

        if (result.TargetKilled)
        {
            LogDeathPoint("PerformFullAttack:TargetKilled", attacker, target);
            _gameManager.Combat_HandleSummonDeathCleanup(target);
            if (target.Team == CharacterTeam.Enemy)
            {
                _gameManager.Combat_UpdateAllStatsUI();
                if (TryHandleVictoryAfterEnemyDeath("PerformFullAttack", attacker, target))
                    return;
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

        // Melee reaction effects (Fire Shield, Thorns, etc.) — generic service call
        if (result.Attacks != null)
        {
            foreach (var attack in result.Attacks)
            {
                if (attack.Hit && !attack.IsRangedAttack)
                    MeleeReactionService.TriggerReactions(attacker, target, attack);
            }
        }

        if (result.TargetKilled)
        {
            LogDeathPoint("PerformDualWieldAttack:TargetKilled", attacker, target);
            _gameManager.Combat_HandleSummonDeathCleanup(target);
            if (target.Team == CharacterTeam.Enemy)
            {
                _gameManager.Combat_UpdateAllStatsUI();
                if (TryHandleVictoryAfterEnemyDeath("PerformDualWieldAttack", attacker, target))
                    return;
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

        // Melee reaction effects (Fire Shield, Thorns, etc.) — generic service call
        if (result.Attacks != null)
        {
            foreach (var attack in result.Attacks)
            {
                if (attack.Hit && !attack.IsRangedAttack)
                    MeleeReactionService.TriggerReactions(attacker, target, attack);
            }
        }

        if (result.TargetKilled)
        {
            LogDeathPoint("PerformFlurryOfBlows:TargetKilled", attacker, target);
            _gameManager.Combat_HandleSummonDeathCleanup(target);
            if (target.Team == CharacterTeam.Enemy)
            {
                _gameManager.Combat_UpdateAllStatsUI();
                if (TryHandleVictoryAfterEnemyDeath("PerformFlurryOfBlows", attacker, target))
                    return;
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

    // ===== Ammunition Consumption System =====

    /// <summary>
    /// Check whether the attacker has sufficient ammunition for a ranged attack with the given weapon.
    /// If the weapon requires ammo and none is available, shows a warning and returns false.
    /// </summary>
    public bool CheckAmmoAvailable(CharacterController attacker, ItemData weapon)
    {
        if (attacker == null || weapon == null)
            return true; // Not a ranged weapon or no weapon = no ammo needed

        if (weapon.RequiresAmmoType == AmmunitionType.None)
            return true; // Thrown weapons, darts, etc. don't consume separate ammo

        Inventory inv = _gameManager?.Combat_GetCharacterInventory(attacker);
        if (inv == null)
            return true; // Safety fallback

        if (!inv.HasAmmo(weapon.RequiresAmmoType))
        {
            string ammoName = weapon.RequiresAmmoType.ToString();
            string charName = attacker.Stats != null ? attacker.Stats.CharacterName : "Attacker";
            _gameManager?.CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{charName} has no {ammoName} ammunition! Cannot fire {weapon.Name}."));
            Debug.LogWarning($"[Ammo] {charName} attempted ranged attack with {weapon.Name} but has no {ammoName} ammo.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Consume one round of ammunition after a ranged attack is resolved.
    /// Returns the consumed ammo item (may have enchantments), or null if no ammo was consumed.
    /// Logs ammo consumption and remaining count to combat log.
    /// </summary>
    public ItemData ConsumeAmmoForAttack(CharacterController attacker, ItemData weapon)
    {
        if (attacker == null || weapon == null || weapon.RequiresAmmoType == AmmunitionType.None)
            return null;

        Inventory inv = _gameManager?.Combat_GetCharacterInventory(attacker);
        if (inv == null)
            return null;

        ItemData consumedAmmo = inv.ConsumeOneAmmo(weapon.RequiresAmmoType);
        if (consumedAmmo == null)
        {
            Debug.LogWarning($"[Ammo] Failed to consume ammo for {weapon.Name} - none available.");
            return null;
        }

        string charName = attacker.Stats != null ? attacker.Stats.CharacterName : "Attacker";
        int remaining = inv.GetTotalAmmoCount(weapon.RequiresAmmoType);
        string ammoName = weapon.RequiresAmmoType.ToString();

        // Build enchantment info for combat log
        string enchantInfo = "";
        if (consumedAmmo.ActiveSpellEffects != null && consumedAmmo.ActiveSpellEffects.Count > 0)
        {
            foreach (var eff in consumedAmmo.ActiveSpellEffects)
            {
                if (eff == null) continue;
                if (!string.IsNullOrEmpty(eff.BonusDamageDice))
                    enchantInfo += $" [+{eff.BonusDamageDice} {eff.BonusDamageType}]";
                if (eff.CritThreatRangeModifier != 0)
                    enchantInfo += $" [Keen]";
            }
        }

        Debug.Log($"[Ammo] {charName} consumed 1 {ammoName}{enchantInfo}. {remaining} remaining.");
        _gameManager?.CombatUI?.ShowCombatLog(CombatLogHelper.Buff("🏹", $"{charName} uses 1 {ammoName}{enchantInfo}. ({remaining} remaining)"));

        return consumedAmmo;
    }

    // ════════════════════════════════════════════════════════════════════
    //  SANCTUARY / HIDE FROM UNDEAD — Break on attack
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// D&D 3.5e: Sanctuary and Hide from Undead end immediately if the warded creature
    /// makes an attack or takes any offensive action (PHB p.274 / PHB p.241).
    /// Called at the start of any attack pipeline.
    /// </summary>
    public static void BreakProtectiveWardsOnAttack(CharacterController attacker)
    {
        if (attacker == null || attacker.Stats == null)
            return;

        // Sanctuary: ends if the warded creature attacks
        if (attacker.Stats.SanctuaryActive)
        {
            attacker.Stats.SanctuaryActive = false;
            attacker.Stats.SanctuaryDC = 0;
            attacker.Stats.SanctuaryCasterLevel = 0;
            var statusMgr = attacker.StatusEffectManager;
            statusMgr?.RemoveEffectsBySpellId(SpellNames.SANCTUARY);
            string name = attacker.Stats.CharacterName;
            Debug.Log($"[Sanctuary] {name} attacks — Sanctuary spell ends.");
            GameManager.Instance?.CombatUI?.ShowCombatLog(CombatLogHelper.Defensive("🛡", $"️ {name} makes an attack — Sanctuary ends!"));
        }

        // Hide from Undead: ends if the warded creature attacks
        if (attacker.Stats.HideFromUndeadActive)
        {
            attacker.Stats.HideFromUndeadActive = false;
            attacker.Stats.HideFromUndeadDC = 0;
            attacker.Stats.HideFromUndeadCasterLevel = 0;
            var statusMgr = attacker.StatusEffectManager;
            statusMgr?.RemoveEffectsBySpellId(SpellNames.HIDE_FROM_UNDEAD);
            string name = attacker.Stats.CharacterName;
            Debug.Log($"[HideFromUndead] {name} attacks — Hide from Undead spell ends.");
            GameManager.Instance?.CombatUI?.ShowCombatLog(CombatLogHelper.Death("", $"👻 {name} makes an attack — Hide from Undead ends!"));
        }
    }

    // ========================================================================
    // WHIRLWIND ATTACK (D&D 3.5 PHB p.102)
    // Full-round action: one melee attack at full BAB against each adjacent enemy.
    // ========================================================================

    /// <summary>
    /// Find all alive enemies adjacent to the attacker.
    /// </summary>
    private List<CharacterController> FindAllAdjacentEnemies(CharacterController attacker)
    {
        var result = new List<CharacterController>();
        if (_gameManager == null || attacker == null) return result;

        var allChars = _gameManager.Combat_GetAllCharacters();
        if (allChars == null) return result;

        foreach (var candidate in allChars)
        {
            if (candidate == null || candidate == attacker) continue;
            if (candidate.Stats == null || candidate.Stats.IsDead) continue;
            if (candidate.Team == attacker.Team) continue;
            if (!SquareGridUtils.IsAdjacent(attacker.GridPosition, candidate.GridPosition)) continue;
            result.Add(candidate);
        }
        return result;
    }

    /// <summary>
    /// Execute a Whirlwind Attack: full-round action, one melee attack at full BAB
    /// against every adjacent enemy. Requires Spring Attack, Dodge, Mobility, Combat Expertise, BAB +4.
    /// </summary>
    public void PerformWhirlwindAttack(CharacterController attacker)
    {
        if (_gameManager == null || attacker == null || attacker.Stats == null) return;

        if (!FeatManager.CanUseWhirlwindAttack(attacker.Stats))
        {
            _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Failure("❌", $"{attacker.Stats.CharacterName} cannot use Whirlwind Attack."));
            return;
        }

        attacker.Actions.UseFullRoundAction();

        List<CharacterController> adjacentEnemies = FindAllAdjacentEnemies(attacker);
        if (adjacentEnemies.Count == 0)
        {
            _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("🌀", $"{attacker.Stats.CharacterName} uses Whirlwind Attack but has no adjacent enemies!"));
            _gameManager.Combat_StartDelayedEndActivePCTurn(1.5f);
            return;
        }

        var logLines = new System.Text.StringBuilder();
        logLines.AppendLine($"🌀 <b>{attacker.Stats.CharacterName}</b> uses <color=#FFD700>Whirlwind Attack</color>! ({adjacentEnemies.Count} adjacent target{(adjacentEnemies.Count > 1 ? "s" : "")})");

        int totalDamage = 0;
        bool anyKilled = false;

        foreach (var enemy in adjacentEnemies)
        {
            if (enemy == null || enemy.Stats == null || enemy.Stats.IsDead) continue;

            // Single melee attack at full BAB (no flanking for Whirlwind Attack)
            CombatResult result = attacker.Attack(enemy, false, 0, null, null, null, null, 0, false);
            string line = $"  vs {enemy.Stats.CharacterName}: ";

            if (result.Hit)
            {
                string critStr = result.CritConfirmed ? " <color=#FF4444>CRITICAL!</color>" : "";
                line += $"HIT ({result.DieRoll}+{result.TotalRoll - result.DieRoll}={result.TotalRoll} vs AC {result.TargetAC}){critStr} — {result.TotalDamage} damage";
                totalDamage += result.TotalDamage;

                if (result.TotalDamage > 0)
                    _gameManager.Combat_CheckConcentrationOnDamage(enemy, result.TotalDamage);

                if (!result.IsRangedAttack)
                    MeleeReactionService.TriggerReactions(attacker, enemy, result);

                if (result.TargetKilled)
                {
                    line += " ☠️ SLAIN!";
                    anyKilled = true;
                    LogDeathPoint("WhirlwindAttack:TargetKilled", attacker, enemy);
                    _gameManager.Combat_HandleSummonDeathCleanup(enemy);
                }
            }
            else
            {
                line += $"MISS ({result.DieRoll}+{result.TotalRoll - result.DieRoll}={result.TotalRoll} vs AC {result.TargetAC})";
            }

            logLines.AppendLine(line);
        }

        logLines.AppendLine($"  Total damage: {totalDamage}");

        string log = logLines.ToString();
        _gameManager.Combat_SetLastCombatLog(log);
        _gameManager.CombatUI?.ShowCombatLog(log);
        _gameManager.Combat_UpdateAllStatsUI();
        _gameManager.Combat_ClearHighlights();

        if (anyKilled)
        {
            if (TryHandleVictoryAfterEnemyDeath("WhirlwindAttack", attacker, null))
                return;
        }

        _gameManager.Combat_StartDelayedEndActivePCTurn(2.0f);
    }

    // ========================================================================
    // MANYSHOT (D&D 3.5 PHB p.97)
    // Standard action: fire 2 arrows with a single attack roll at -4 penalty.
    // Target must be within 30 feet. Each arrow rolls damage separately.
    // ========================================================================

    /// <summary>
    /// Execute a Manyshot attack: single attack roll at -4, fire 2 arrows.
    /// If it hits, roll damage twice (one per arrow). Standard action.
    /// </summary>
    public void PerformManyshotAttack(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo = null)
    {
        if (_gameManager == null || attacker == null || target == null) return;

        if (!FeatManager.CanUseManyshot(attacker.Stats))
        {
            _gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Failure("❌", $"{attacker.Stats.CharacterName} cannot use Manyshot."));
            return;
        }

        // Manyshot is a standard action
        attacker.Actions.UseStandardAction();
        attacker.Stats.ManyshotActive = false; // consume the toggle

        int manyshotPenalty = FeatManager.GetManyshotAttackPenalty(); // -4

        // First arrow: single attack roll at -4
        CombatResult firstArrow = attacker.Attack(target, isFlanking, flankBonus, partnerName, rangeInfo, null, null, manyshotPenalty, false);

        var logLines = new System.Text.StringBuilder();
        logLines.AppendLine($"🏹 <b>{attacker.Stats.CharacterName}</b> uses <color=#FFD700>Manyshot</color>! (2 arrows, -4 penalty)");

        int totalDamage = 0;

        if (firstArrow.Hit)
        {
            string critStr1 = firstArrow.CritConfirmed ? " CRITICAL!" : "";
            logLines.AppendLine($"  Arrow 1: HIT ({firstArrow.DieRoll}+{firstArrow.TotalRoll - firstArrow.DieRoll}={firstArrow.TotalRoll} vs AC {firstArrow.TargetAC}){critStr1} — {firstArrow.TotalDamage} damage");
            totalDamage += firstArrow.TotalDamage;

            // Second arrow uses the same attack roll (same hit determination).
            // Roll damage separately for the second arrow.
            if (target != null && target.Stats != null && !target.Stats.IsDead)
            {
                CombatResult secondArrow = attacker.Attack(target, isFlanking, flankBonus, partnerName, rangeInfo, null, null, manyshotPenalty, false);
                // The second arrow uses the same attack roll result, so it auto-hits if the first did.
                // We simulate this by just using the damage from a second attack.
                string critStr2 = secondArrow.CritConfirmed ? " CRITICAL!" : "";
                logLines.AppendLine($"  Arrow 2: HIT{critStr2} — {secondArrow.TotalDamage} damage");
                totalDamage += secondArrow.TotalDamage;

                if (secondArrow.TotalDamage > 0)
                    _gameManager.Combat_CheckConcentrationOnDamage(target, secondArrow.TotalDamage);
            }

            if (firstArrow.TotalDamage > 0)
                _gameManager.Combat_CheckConcentrationOnDamage(target, firstArrow.TotalDamage);
        }
        else
        {
            logLines.AppendLine($"  Both arrows: MISS ({firstArrow.DieRoll}+{firstArrow.TotalRoll - firstArrow.DieRoll}={firstArrow.TotalRoll} vs AC {firstArrow.TargetAC})");
        }

        logLines.AppendLine($"  Total damage: {totalDamage}");

        string log = logLines.ToString();
        _gameManager.Combat_SetLastCombatLog(log);
        _gameManager.CombatUI?.ShowCombatLog(log);
        _gameManager.Combat_UpdateAllStatsUI();
        _gameManager.Combat_ClearHighlights();

        if (firstArrow.TargetKilled || (target != null && target.Stats != null && target.Stats.IsDead))
        {
            LogDeathPoint("Manyshot:TargetKilled", attacker, target);
            _gameManager.Combat_HandleSummonDeathCleanup(target);
            if (target.Team == CharacterTeam.Enemy)
            {
                if (TryHandleVictoryAfterEnemyDeath("Manyshot", attacker, target))
                    return;
            }
        }

        _gameManager.Combat_StartDelayedEndActivePCTurn(2.0f);
    }
}
