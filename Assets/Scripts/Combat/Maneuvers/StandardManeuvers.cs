using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StandardManeuvers : BaseCombatManeuver
{
}

public partial class GameManager
{
    private void ShowDualWieldingPromptForDisarm(CharacterController attacker)
    {
        if (attacker == null)
            return;

        string message = "You have weapons in both hands.\nUse dual wielding for this disarm?\n\n"
            + "Yes: Apply dual-wield penalties, off-hand disarm available\n"
            + "No: No penalties, off-hand disarm unavailable this round";

        CombatUI?.ShowConfirmationDialog(
            title: "Dual wield disarm?",
            message: message,
            confirmLabel: "Yes",
            cancelLabel: "No",
            onConfirm: () => OnDisarmDualWieldingChoiceSelected(attacker, true),
            onCancel: () => OnDisarmDualWieldingChoiceSelected(attacker, false));
    }

    private void OnDisarmDualWieldingChoiceSelected(CharacterController attacker, bool dualWield)
    {
        if (attacker == null)
            return;

        ApplyDualWieldingChoiceState(attacker, dualWield, "Disarm");

        if (!CanUseMainHandDisarmAttackOption(attacker))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attacker.Stats.CharacterName} cannot perform Disarm: no main-hand disarm attacks remaining."));
            ShowActionChoices();
            return;
        }

        _pendingSpecialAttackType = SpecialAttackType.Disarm;
        _pendingDisarmUseOffHandSelection = false;
        _pendingSunderUseOffHandSelection = false;
        _isSelectingSpecialAttack = true;
        CurrentSubPhase = PlayerSubPhase.SelectingSpecialTarget;
        ShowSpecialAttackTargets(attacker, SpecialAttackType.Disarm);
    }

    private void ShowDualWieldingPromptForSunder(CharacterController attacker)
    {
        if (attacker == null)
            return;

        string message = "You have weapons in both hands.\nUse dual wielding for this sunder?\n\n"
            + "Yes: Apply dual-wield penalties, off-hand sunder available\n"
            + "No: No penalties, off-hand sunder unavailable this round";

        CombatUI?.ShowConfirmationDialog(
            title: "Dual wield sunder?",
            message: message,
            confirmLabel: "Yes",
            cancelLabel: "No",
            onConfirm: () => OnSunderDualWieldingChoiceSelected(attacker, true),
            onCancel: () => OnSunderDualWieldingChoiceSelected(attacker, false));
    }

    private void OnSunderDualWieldingChoiceSelected(CharacterController attacker, bool dualWield)
    {
        if (attacker == null)
            return;

        ApplyDualWieldingChoiceState(attacker, dualWield, "Sunder");

        if (!CanUseMainHandSunderAttackOption(attacker))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attacker.Stats.CharacterName} cannot perform Sunder: no main-hand sunder attacks remaining."));
            ShowActionChoices();
            return;
        }

        _pendingSpecialAttackType = SpecialAttackType.Sunder;
        _pendingDisarmUseOffHandSelection = false;
        _pendingSunderUseOffHandSelection = false;
        _isSelectingSpecialAttack = true;
        CurrentSubPhase = PlayerSubPhase.SelectingSpecialTarget;
        ShowSpecialAttackTargets(attacker, SpecialAttackType.Sunder);
    }

    private int GetRemainingDisarmAttempts(CharacterController attacker)
    {
        return GetRemainingMainHandDisarmAttackActions(attacker) + GetRemainingOffHandDisarmAttackActions(attacker);
    }

    public bool CanUseMainHandDisarmAttackOption(CharacterController attacker)
    {
        if (attacker == null || attacker.Actions == null)
            return false;

        if (!attacker.HasMeleeWeaponEquipped())
            return false;

        return CanUseMainHandManeuverAttackOption(attacker, "Disarm");
    }

    public int GetRemainingMainHandDisarmAttackActions(CharacterController attacker)
    {
        if (!CanUseMainHandDisarmAttackOption(attacker))
            return 0;

        return GetRemainingMainHandManeuverAttackActions(attacker);
    }

    public int GetCurrentMainHandDisarmAttackBonus(CharacterController attacker)
    {
        if (!CanUseMainHandDisarmAttackOption(attacker))
            return 0;

        return GetCurrentMainHandManeuverAttackBonusForUI(attacker);
    }

    public bool ShouldShowOffHandDisarmButton(CharacterController attacker)
    {
        return attacker != null
            && _dualWieldingChoiceMade
            && _isDualWielding
            && attacker.HasOffHandWeaponEquipped();
    }

    public bool CanUseOffHandDisarmAttackOption(CharacterController attacker)
    {
        if (!ShouldShowOffHandDisarmButton(attacker))
            return false;

        if (attacker == null || attacker.Actions == null)
            return false;

        if (!CanUseOffHandAttackOption(attacker))
            return false;

        return attacker.GetOffHandAttackWeapon() != null;
    }

    public int GetRemainingOffHandDisarmAttackActions(CharacterController attacker)
    {
        return CanUseOffHandDisarmAttackOption(attacker) ? 1 : 0;
    }

    public int GetCurrentOffHandDisarmAttackBonus(CharacterController attacker)
    {
        if (!CanUseOffHandDisarmAttackOption(attacker) || attacker == null || attacker.Stats == null)
            return 0;

        int offHandBab = attacker.Stats.BaseAttackBonus;
        if (_isDualWielding)
            offHandBab += _offHandPenalty;
        return offHandBab;
    }

    public bool CanUseDisarmAttackOption(CharacterController attacker)
    {
        return CanUseMainHandDisarmAttackOption(attacker) || CanUseOffHandDisarmAttackOption(attacker);
    }

    public int GetRemainingDisarmAttackActions(CharacterController attacker)
    {
        return GetRemainingDisarmAttempts(attacker);
    }

    public int GetCurrentDisarmAttackBonus(CharacterController attacker)
    {
        if (CanUseMainHandDisarmAttackOption(attacker))
            return GetCurrentMainHandDisarmAttackBonus(attacker);

        return GetCurrentOffHandDisarmAttackBonus(attacker);
    }

    private int GetRemainingSunderAttempts(CharacterController attacker)
    {
        return GetRemainingMainHandSunderAttackActions(attacker) + GetRemainingOffHandSunderAttackActions(attacker);
    }

    public bool CanUseMainHandSunderAttackOption(CharacterController attacker)
    {
        if (attacker == null || attacker.Actions == null)
            return false;

        // Sunder needs a manufactured weapon (the check the AI and the NPC executor use too), so a
        // creature fighting with natural attacks or unarmed is not offered it (CMB-102).
        if (!attacker.HasMeleeWeaponEquipped() || !attacker.CanSunderWithMainWeapon(out _))
            return false;

        return CanUseMainHandManeuverAttackOption(attacker, "Sunder");
    }

    public int GetRemainingMainHandSunderAttackActions(CharacterController attacker)
    {
        if (!CanUseMainHandSunderAttackOption(attacker))
            return 0;

        return GetRemainingMainHandManeuverAttackActions(attacker);
    }

    public int GetCurrentMainHandSunderAttackBonus(CharacterController attacker)
    {
        if (!CanUseMainHandSunderAttackOption(attacker))
            return 0;

        return GetCurrentMainHandManeuverAttackBonusForUI(attacker);
    }

    public bool ShouldShowOffHandSunderButton(CharacterController attacker)
    {
        return attacker != null
            && _dualWieldingChoiceMade
            && _isDualWielding
            && attacker.HasOffHandWeaponEquipped();
    }

    public bool CanUseOffHandSunderAttackOption(CharacterController attacker)
    {
        if (!ShouldShowOffHandSunderButton(attacker))
            return false;

        if (attacker == null || attacker.Actions == null)
            return false;

        if (!CanUseOffHandAttackOption(attacker))
            return false;

        return attacker.GetOffHandAttackWeapon() != null;
    }

    public int GetRemainingOffHandSunderAttackActions(CharacterController attacker)
    {
        return CanUseOffHandSunderAttackOption(attacker) ? 1 : 0;
    }

    public int GetCurrentOffHandSunderAttackBonus(CharacterController attacker)
    {
        if (!CanUseOffHandSunderAttackOption(attacker) || attacker == null || attacker.Stats == null)
            return 0;

        int offHandBab = attacker.Stats.BaseAttackBonus;
        if (_isDualWielding)
            offHandBab += _offHandPenalty;
        return offHandBab;
    }

    public bool CanUseSunderAttackOption(CharacterController attacker)
    {
        return CanUseMainHandSunderAttackOption(attacker) || CanUseOffHandSunderAttackOption(attacker);
    }

    public int GetRemainingSunderAttackActions(CharacterController attacker)
    {
        return GetRemainingSunderAttempts(attacker);
    }

    public int GetCurrentSunderAttackBonus(CharacterController attacker)
    {
        if (CanUseMainHandSunderAttackOption(attacker))
            return GetCurrentMainHandSunderAttackBonus(attacker);

        return GetCurrentOffHandSunderAttackBonus(attacker);
    }

    // Trip, disarm, sunder and grapple replace one melee attack of an attack or full attack, at that
    // attack's bonus (PHB p.141 Table 8-2 note 7, p.143). They are steps of the creature's own attack
    // sequence (CharacterController.TryCommitManeuverSubstituteStep, shared with the NPC executor), so
    // the first one spends only the standard action and a second attack or maneuver turns the turn
    // into a full attack (CMB-102). A weapon or unarmed fighter gives up an iterative attack (its
    // iterative BAB); a creature fighting with its natural attacks gives up one natural attack, at that
    // attack's BAB (primary full, secondary -5 or -2 with Multiattack, MM p.312). The PC gives up the
    // natural attack the Attack button would use next (the first one not used this turn), and that
    // natural attack is marked used. Grapple actions while already grappling stay iterative steps
    // (PHB p.156), so callers pass iterativeOnly for them.
    // The list lives in ManeuverActionCost.ReplacesMeleeAttack. Bull rush and overrun are not on it:
    // they are standard actions or part of a charge (PHB p.154, p.157).

    private AttackStepKind GetManeuverStepKind(CharacterController attacker, bool iterativeOnly)
        => iterativeOnly ? AttackStepKind.MainHand : attacker.GetManeuverSubstituteStepKind();

    /// <summary>
    /// The natural attack a PC maneuver gives up: the first one not used this turn, from the sequence
    /// cursor. When every natural attack is used, Haste's extra natural attack if it is unused
    /// (<paramref name="givesUpHasteExtraAttack"/>), at the bonus of the default Haste natural attack
    /// (the highest; CMB-106).
    /// </summary>
    private int GetPcManeuverNaturalAttackIndex(CharacterController attacker, out bool givesUpHasteExtraAttack)
    {
        givesUpHasteExtraAttack = false;
        if (attacker == null || attacker != ActivePC || attacker.GetManeuverSubstituteStepKind() != AttackStepKind.NaturalSequence)
            return -1;

        int index = ResolveNextAvailableNaturalAttackSequenceIndex(attacker, attacker.ProgressiveAttackPool.MainHandStepsUsed, null);
        if (index < 0 && attacker.CanUseHasteExtraNaturalAttack())
        {
            givesUpHasteExtraAttack = true;
            index = attacker.GetDefaultHasteNaturalAttackIndex();
        }

        return index;
    }

    private int GetPcManeuverNaturalAttackIndex(CharacterController attacker) => GetPcManeuverNaturalAttackIndex(attacker, out _);

    private bool CanUseMainHandManeuverAttackOption(CharacterController attacker, string maneuverLabel, bool iterativeOnly = false)
    {
        if (attacker == null || attacker.Actions == null)
            return false;

        return GetRemainingMainHandManeuverAttackActions(attacker, iterativeOnly) > 0;
    }

    private int GetRemainingMainHandManeuverAttackActions(CharacterController attacker, bool iterativeOnly = false)
    {
        if (attacker == null || attacker.Actions == null)
            return 0;

        int remaining = attacker.GetRemainingMainHandAttackSteps(GetManeuverStepKind(attacker, iterativeOnly));

        // A PC's natural-attack buttons can use natural attacks out of order; never offer more
        // substitutes than unused natural attacks (plus Haste's extra natural attack while unused, CMB-106).
        if (!iterativeOnly && attacker == ActivePC && attacker.GetManeuverSubstituteStepKind() == AttackStepKind.NaturalSequence)
            remaining = Mathf.Min(remaining, GetRemainingNaturalAttackCount(attacker));

        return remaining;
    }

    private int GetCurrentMainHandManeuverAttackBonusForUI(CharacterController attacker, bool iterativeOnly = false)
    {
        if (attacker == null || attacker.Stats == null)
            return 0;

        if (GetRemainingMainHandManeuverAttackActions(attacker, iterativeOnly) <= 0)
            return 0;

        int step = attacker.ProgressiveAttackPool.MainHandStepsUsed;
        int bab = iterativeOnly
            ? attacker.GetMainHandAttackStepBAB(step)
            : attacker.GetManeuverSubstituteBAB(step, GetPcManeuverNaturalAttackIndex(attacker));
        if (_isDualWielding && attacker == ActivePC && GetManeuverStepKind(attacker, iterativeOnly) == AttackStepKind.MainHand)
            bab += _mainHandPenalty;
        return bab;
    }

    /// <summary>
    /// Commit the attack step a maneuver replaces. Returns the BAB the maneuver rolls at (with the PC
    /// dual-wield main-hand penalty on a weapon step) and ends the PC Attack-button flow when no
    /// main-hand step remains.
    /// </summary>
    private bool TryCommitMainHandManeuverStep(CharacterController attacker, string maneuverLabel, out int attackBonusUsed, out string reason, bool iterativeOnly = false)
    {
        attackBonusUsed = 0;
        reason = string.Empty;

        if (attacker == null || attacker.Actions == null)
        {
            reason = "No action economy available.";
            return false;
        }

        AttackStepKind kind = GetManeuverStepKind(attacker, iterativeOnly);
        if (kind == AttackStepKind.NaturalSequence && GetRemainingMainHandManeuverAttackActions(attacker) <= 0)
        {
            reason = $"No {maneuverLabel.ToLowerInvariant()} attacks remaining this turn.";
            return false;
        }

        bool givesUpHasteExtraAttack = false;
        int naturalAttackIndex = kind == AttackStepKind.NaturalSequence ? GetPcManeuverNaturalAttackIndex(attacker, out givesUpHasteExtraAttack) : -1;
        bool committed;
        if (iterativeOnly)
        {
            committed = attacker.TryCommitAttack(AttackStepKind.MainHand, out int step, out reason);
            if (committed)
                attackBonusUsed = attacker.GetMainHandAttackStepBAB(step);
        }
        else
        {
            committed = attacker.TryCommitManeuverSubstituteStep(naturalAttackIndex, out attackBonusUsed, out _, out reason, givesUpHasteExtraAttack);
        }

        if (!committed)
        {
            if (string.IsNullOrWhiteSpace(reason))
                reason = $"No {maneuverLabel.ToLowerInvariant()} attacks remaining this turn.";
            return false;
        }

        if (kind == AttackStepKind.NaturalSequence)
        {
            // The natural attack given up is spent, so the Attack and natural-attack buttons skip it.
            // Haste's extra attack given up is marked by TryCommitManeuverSubstituteStep instead.
            if (givesUpHasteExtraAttack)
            {
                Debug.Log($"[{maneuverLabel}][Flow] Replaces Haste's extra natural attack (at the {attacker.Stats.GetNaturalAttackAtSequenceIndex(naturalAttackIndex)?.Name ?? "?"} bonus).");
            }
            else if (naturalAttackIndex >= 0)
            {
                _usedNaturalAttackSequenceIndices.Add(naturalAttackIndex);
                Debug.Log($"[{maneuverLabel}][Flow] Replaces natural attack #{naturalAttackIndex + 1} ({attacker.Stats.GetNaturalAttackAtSequenceIndex(naturalAttackIndex)?.Name ?? "?"}).");
            }
        }
        else if (_isDualWielding && attacker == ActivePC)
        {
            attackBonusUsed += _mainHandPenalty;
        }

        if (_isInAttackSequence && _attackingCharacter == attacker && !HasMoreAttacksAvailable())
            EndAttackSequence();

        return true;
    }

    private bool TryConsumeMainHandManeuverAttackAction(CharacterController attacker, string maneuverLabel, out int attackBonusUsed, out int attacksRemaining, out string reason, bool iterativeOnly = false)
    {
        attacksRemaining = 0;

        if (!TryCommitMainHandManeuverStep(attacker, maneuverLabel, out attackBonusUsed, out reason, iterativeOnly))
            return false;

        attacksRemaining = GetRemainingMainHandManeuverAttackActions(attacker, iterativeOnly);
        Debug.Log($"[{maneuverLabel}][Flow] Consumed main-hand maneuver attack at BAB {CharacterStats.FormatMod(attackBonusUsed)}; remaining={attacksRemaining}");
        return true;
    }

    public bool CanUseGrappleAttackOption(CharacterController attacker)
    {
        if (attacker == null)
            return false;

        // Once grappling, each attack can be a grapple action at its iterative BAB (PHB p.156).
        bool isAlreadyGrappling = attacker.IsGrappling();
        if (!isAlreadyGrappling && !attacker.CanUseStandardGrapple())
            return false;

        return CanUseMainHandManeuverAttackOption(attacker, "Grapple", iterativeOnly: isAlreadyGrappling);
    }

    public int GetRemainingGrappleAttackActions(CharacterController attacker)
    {
        if (!CanUseGrappleAttackOption(attacker))
            return 0;

        return GetRemainingMainHandManeuverAttackActions(attacker, iterativeOnly: attacker.IsGrappling());
    }

    public int GetCurrentGrappleAttackBonus(CharacterController attacker)
    {
        if (!CanUseGrappleAttackOption(attacker))
            return 0;

        return GetCurrentMainHandManeuverAttackBonusForUI(attacker, iterativeOnly: attacker.IsGrappling());
    }

    private bool TryConsumeGrappleAttackAction(CharacterController attacker, out int attackBonusUsed, out int attacksRemaining, out string reason)
    {
        attackBonusUsed = 0;
        attacksRemaining = 0;
        reason = string.Empty;

        if (attacker == null)
        {
            reason = "No attacker available.";
            return false;
        }

        bool isAlreadyGrappling = attacker.IsGrappling();
        if (!isAlreadyGrappling && !attacker.CanUseStandardGrapple())
        {
            reason = "Standard grapple is not available";
            return false;
        }

        return TryConsumeMainHandManeuverAttackAction(attacker, "Grapple", out attackBonusUsed, out attacksRemaining, out reason, iterativeOnly: isAlreadyGrappling);
    }

    // Bull rush is a standard action (or the end of a charge) for every creature, never one attack
    // of a full attack (PHB p.141 Table 8-2, p.154; CMB-102). Target legality is
    // CharacterController.CanBullRush.
    public bool CanUseBullRushAttackOption(CharacterController attacker)
        => attacker?.Actions != null && attacker.Actions.HasStandardAction && !attacker.IsGrappling();

    public bool CanUseTripAttackOption(CharacterController attacker)
        => CanUseMainHandManeuverAttackOption(attacker, "Trip");

    public int GetRemainingTripAttackActions(CharacterController attacker)
        => GetRemainingMainHandManeuverAttackActions(attacker);

    public int GetCurrentTripAttackBonus(CharacterController attacker)
        => GetCurrentMainHandManeuverAttackBonusForUI(attacker);

    private bool TryConsumeTripAttackAction(CharacterController attacker, out int attackBonusUsed, out int attacksRemaining, out string reason)
        => TryConsumeMainHandManeuverAttackAction(attacker, "Trip", out attackBonusUsed, out attacksRemaining, out reason);

    public List<CharacterController> GetAdjacentHelplessEnemiesForCoupDeGrace(CharacterController attacker)
    {
        var adjacentHelpless = new List<CharacterController>();
        if (attacker == null || attacker.Stats == null)
            return adjacentHelpless;

        List<CharacterController> all = GetAllCharacters();
        for (int i = 0; i < all.Count; i++)
        {
            CharacterController candidate = all[i];
            if (candidate == null || candidate == attacker || candidate.Stats == null || candidate.Stats.IsDead)
                continue;

            if (!TeamUtility.IsEnemy(attacker, candidate))
                continue;

            int distance = attacker.GetMinimumDistanceToTarget(candidate, chebyshev: true);
            if (distance != 1)
                continue;

            if (!candidate.IsHelplessForCoupDeGrace())
                continue;

            adjacentHelpless.Add(candidate);
        }

        return adjacentHelpless;
    }

    public bool CanUseCoupDeGraceAttackOption(CharacterController attacker)
    {
        if (attacker == null || attacker.Actions == null || attacker.Stats == null)
            return false;

        if (!attacker.Actions.HasFullRoundAction)
            return false;

        if (attacker.HasCondition(CombatConditionType.Turned)
            || attacker.HasCondition(CombatConditionType.Prone)
            || attacker.HasCondition(CombatConditionType.Pinned)
            || attacker.HasCondition(CombatConditionType.Grappled))
            return false;

        if (!attacker.CanAttackWithEquippedWeapon(out _))
            return false;

        if (attacker.IsEquippedWeaponRanged())
            return false;

        return GetAdjacentHelplessEnemiesForCoupDeGrace(attacker).Count > 0;
    }

    private bool TryConsumeDisarmAttackAction(CharacterController attacker, bool useOffHand, out int attackBonusUsed, out int attacksRemaining, out string reason, out bool usedOffHand, out ItemData disarmWeapon)
    {
        attackBonusUsed = 0;
        attacksRemaining = 0;
        reason = string.Empty;
        usedOffHand = false;
        disarmWeapon = null;

        if (attacker == null || attacker.Actions == null)
        {
            reason = "No action economy available.";
            return false;
        }

        if (!useOffHand)
        {
            if (!TryCommitMainHandManeuverStep(attacker, "Disarm", out attackBonusUsed, out reason))
                return false;

            usedOffHand = false;
            disarmWeapon = attacker.GetEquippedMainWeapon();
            attacksRemaining = GetRemainingDisarmAttempts(attacker);
            Debug.Log($"[Disarm][Flow] Consumed main-hand disarm attack at BAB {CharacterStats.FormatMod(attackBonusUsed)}.");
            return true;
        }

        if (!CanUseOffHandDisarmAttackOption(attacker))
        {
            reason = "No off-hand disarm attacks remaining this turn.";
            return false;
        }

        ItemData offHandWeapon = attacker.GetOffHandAttackWeapon();
        if (offHandWeapon == null)
        {
            reason = "No valid off-hand weapon equipped.";
            return false;
        }

        int offHandBab = attacker.Stats != null ? attacker.Stats.BaseAttackBonus : 0;
        if (_isDualWielding)
            offHandBab += _offHandPenalty;

        // The off-hand maneuver is one attack of the creature's sequence (PHB p.143).
        if (!attacker.TryCommitAttack(AttackStepKind.OffHand, out _, out reason))
            return false;

        attackBonusUsed = offHandBab;
        usedOffHand = true;
        disarmWeapon = offHandWeapon;
        _offHandAttackUsedThisTurn = true;
        _offHandAttackAvailableThisTurn = attacker.HasOffHandWeaponEquipped();
        attacksRemaining = GetRemainingDisarmAttempts(attacker);
        Debug.Log($"[Disarm][Flow] Consumed off-hand disarm attack at BAB {CharacterStats.FormatMod(attackBonusUsed)}.");
        return true;
    }

    private bool TryConsumeSunderAttackAction(CharacterController attacker, bool useOffHand, out int attackBonusUsed, out int attacksRemaining, out string reason, out bool usedOffHand, out ItemData sunderWeapon)
    {
        attackBonusUsed = 0;
        attacksRemaining = 0;
        reason = string.Empty;
        usedOffHand = false;
        sunderWeapon = null;

        if (attacker == null || attacker.Actions == null)
        {
            reason = "No action economy available.";
            return false;
        }

        if (!useOffHand)
        {
            if (!TryCommitMainHandManeuverStep(attacker, "Sunder", out attackBonusUsed, out reason))
                return false;

            usedOffHand = false;
            sunderWeapon = attacker.GetEquippedMainWeapon();
            attacksRemaining = GetRemainingSunderAttempts(attacker);
            Debug.Log($"[Sunder][Flow] Consumed main-hand sunder attack at BAB {CharacterStats.FormatMod(attackBonusUsed)}.");
            return true;
        }

        if (!CanUseOffHandSunderAttackOption(attacker))
        {
            reason = "No off-hand sunder attacks remaining this turn.";
            return false;
        }

        ItemData offHandWeapon = attacker.GetOffHandAttackWeapon();
        if (offHandWeapon == null)
        {
            reason = "No valid off-hand weapon equipped.";
            return false;
        }

        int offHandBab = attacker.Stats != null ? attacker.Stats.BaseAttackBonus : 0;
        if (_isDualWielding)
            offHandBab += _offHandPenalty;

        // The off-hand maneuver is one attack of the creature's sequence (PHB p.143).
        if (!attacker.TryCommitAttack(AttackStepKind.OffHand, out _, out reason))
            return false;

        attackBonusUsed = offHandBab;
        usedOffHand = true;
        sunderWeapon = offHandWeapon;
        _offHandAttackUsedThisTurn = true;
        _offHandAttackAvailableThisTurn = attacker.HasOffHandWeaponEquipped();
        attacksRemaining = GetRemainingSunderAttempts(attacker);
        Debug.Log($"[Sunder][Flow] Consumed off-hand sunder attack at BAB {CharacterStats.FormatMod(attackBonusUsed)}.");
        return true;
    }

    private bool CanUseImprovedFeintAsMove(CharacterController actor)
    {
        if (actor == null || actor.Stats == null || !actor.Stats.HasFeat("Improved Feint"))
            return false;

        return actor.Actions.HasMoveAction || actor.Actions.CanConvertStandardToMove;
    }

    private bool TryConsumeFeintAction(CharacterController attacker, out string actionLabel)
    {
        actionLabel = "";
        if (attacker == null)
            return false;

        if (CanUseImprovedFeintAsMove(attacker))
        {
            if (attacker.Actions.HasMoveAction)
                attacker.Actions.UseMoveAction();
            else
                attacker.Actions.ConvertStandardToMove();

            actionLabel = "move action (Improved Feint)";
            return true;
        }

        if (attacker.CommitStandardAction())
        {
            actionLabel = "standard action";
            return true;
        }

        return false;
    }

    private void HandleDisarmTargetClick(CharacterController attacker, CharacterController target)
    {
        if (attacker == null || target == null)
        {
            ShowActionChoices();
            return;
        }

        if (!target.HasDisarmableWeaponEquipped())
        {
            string targetName = target.Stats != null ? target.Stats.CharacterName : "Target";
            Debug.Log($"[Disarm][Flow] Invalid target selected: {targetName} has no disarmable weapon equipped.");
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("", $"{targetName} has no weapon to disarm!"));

            // Do not consume any attack action; allow selecting another target.
            ShowSpecialAttackTargets(attacker, SpecialAttackType.Disarm);
            return;
        }

        List<DisarmableHeldItemOption> options = target.GetDisarmableHeldItemOptions();
        if (options.Count <= 1)
        {
            EquipSlot? selectedSlot = options.Count == 1 ? options[0].HandSlot : null;
            BeginDisarmSequence(attacker, target, selectedSlot);
            ExecuteSpecialAttack(attacker, target, SpecialAttackType.Disarm, selectedSlot);
            return;
        }

        List<string> optionLabels = new List<string>(options.Count);
        for (int i = 0; i < options.Count; i++)
        {
            string handLabel = options[i].HandSlot == EquipSlot.RightHand ? "Main Hand" : "Off-Hand";
            string heldItemName = options[i].HeldItem != null ? options[i].HeldItem.Name : "Held Item";
            optionLabels.Add($"{handLabel}: {heldItemName}");
        }

        CombatUI.ShowDisarmWeaponSelection(
            target.Stats.CharacterName,
            optionLabels,
            onSelect: selectedIndex =>
            {
                if (selectedIndex < 0 || selectedIndex >= options.Count)
                {
                    ShowSpecialAttackTargets(attacker, SpecialAttackType.Disarm);
                    return;
                }

                // Re-validate in case gear changed while prompt was open.
                List<DisarmableHeldItemOption> latestOptions = target.GetDisarmableHeldItemOptions();
                EquipSlot selectedSlot = options[selectedIndex].HandSlot;
                bool slotStillValid = latestOptions.Exists(o => o.HandSlot == selectedSlot);
                if (!slotStillValid)
                {
                    CombatUI.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{target.Stats.CharacterName}'s selected held item is no longer equipped."));
                    ShowSpecialAttackTargets(attacker, SpecialAttackType.Disarm);
                    return;
                }

                BeginDisarmSequence(attacker, target, selectedSlot);
                ExecuteSpecialAttack(attacker, target, SpecialAttackType.Disarm, selectedSlot);
            },
            onCancel: () =>
            {
                if (CurrentPhase == TurnPhase.PCTurn && ActivePC == attacker && attacker.Actions.HasStandardAction)
                    ShowSpecialAttackTargets(attacker, SpecialAttackType.Disarm);
                else
                    ShowActionChoices();
            });
    }

    private void HandleSunderTargetClick(CharacterController attacker, CharacterController target)
    {
        if (attacker == null || target == null)
        {
            ShowActionChoices();
            return;
        }

        if (!target.HasSunderableItemEquipped())
        {
            string targetName = target.Stats != null ? target.Stats.CharacterName : "Target";
            Debug.Log($"[Sunder][Flow] Invalid target selected: {targetName} has no sunderable item equipped.");
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("", $"{targetName} has no item to sunder!"));

            // Do not consume any attack action; allow selecting another target.
            ShowSpecialAttackTargets(attacker, SpecialAttackType.Sunder);
            return;
        }

        List<SunderableItemOption> options = target.GetSunderableItemOptions();
        if (options.Count <= 1)
        {
            EquipSlot? selectedSlot = options.Count == 1 ? options[0].Slot : null;
            BeginSunderSequence(attacker, target, selectedSlot);
            ExecuteSpecialAttack(attacker, target, SpecialAttackType.Sunder, sunderTargetSlot: selectedSlot);
            return;
        }

        List<string> optionLabels = new List<string>(options.Count);
        for (int i = 0; i < options.Count; i++)
            optionLabels.Add(options[i].GetLabel());

        CombatUI.ShowSunderItemSelection(
            target.Stats.CharacterName,
            optionLabels,
            onSelect: selectedIndex =>
            {
                if (selectedIndex < 0 || selectedIndex >= options.Count)
                {
                    ShowSpecialAttackTargets(attacker, SpecialAttackType.Sunder);
                    return;
                }

                // Re-validate in case gear changed while prompt was open.
                List<SunderableItemOption> latestOptions = target.GetSunderableItemOptions();
                EquipSlot selectedSlot = options[selectedIndex].Slot;
                bool slotStillValid = latestOptions.Exists(o => o.Slot == selectedSlot);
                if (!slotStillValid)
                {
                    CombatUI.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{target.Stats.CharacterName}'s selected item is no longer equipped."));
                    ShowSpecialAttackTargets(attacker, SpecialAttackType.Sunder);
                    return;
                }

                BeginSunderSequence(attacker, target, selectedSlot);
                ExecuteSpecialAttack(attacker, target, SpecialAttackType.Sunder, sunderTargetSlot: selectedSlot);
            },
            onCancel: () =>
            {
                if (CurrentPhase == TurnPhase.PCTurn && ActivePC == attacker && attacker.Actions.HasStandardAction)
                    ShowSpecialAttackTargets(attacker, SpecialAttackType.Sunder);
                else
                    ShowActionChoices();
            });
    }

    private void BeginDisarmSequence(CharacterController attacker, CharacterController target, EquipSlot? targetSlot)
    {
        _isDisarmSequenceActive = attacker != null && target != null;
        _disarmInitiator = attacker;
        _disarmTarget = target;
        _disarmTargetSlot = targetSlot;
        _disarmAttemptNumber = 0;

        Debug.Log($"[Disarm][Flow] BeginDisarmSequence attacker={(attacker != null && attacker.Stats != null ? attacker.Stats.CharacterName : "<null>")} target={(target != null && target.Stats != null ? target.Stats.CharacterName : "<null>")} slot={(targetSlot.HasValue ? targetSlot.Value.ToString() : "Auto")}");
    }

    private void ClearDisarmSequenceState()
    {
        Debug.Log($"[Disarm][Flow] ClearDisarmSequenceState previousState active={_isDisarmSequenceActive} attempt={_disarmAttemptNumber} attacker={(_disarmInitiator != null && _disarmInitiator.Stats != null ? _disarmInitiator.Stats.CharacterName : "<null>")} target={(_disarmTarget != null && _disarmTarget.Stats != null ? _disarmTarget.Stats.CharacterName : "<null>")}");

        _isDisarmSequenceActive = false;
        _disarmInitiator = null;
        _disarmTarget = null;
        _disarmTargetSlot = null;
        _disarmAttemptNumber = 0;
    }

    private void BeginSunderSequence(CharacterController attacker, CharacterController target, EquipSlot? targetSlot)
    {
        _isSunderSequenceActive = attacker != null && target != null;
        _sunderInitiator = attacker;
        _sunderTarget = target;
        _sunderTargetSlot = targetSlot;
        _sunderAttemptNumber = 0;

        Debug.Log($"[Sunder][Flow] BeginSunderSequence attacker={(attacker != null && attacker.Stats != null ? attacker.Stats.CharacterName : "<null>")} target={(target != null && target.Stats != null ? target.Stats.CharacterName : "<null>")} slot={(targetSlot.HasValue ? targetSlot.Value.ToString() : "Auto")}");
    }

    private void ClearSunderSequenceState()
    {
        Debug.Log($"[Sunder][Flow] ClearSunderSequenceState previousState active={_isSunderSequenceActive} attempt={_sunderAttemptNumber} attacker={(_sunderInitiator != null && _sunderInitiator.Stats != null ? _sunderInitiator.Stats.CharacterName : "<null>")} target={(_sunderTarget != null && _sunderTarget.Stats != null ? _sunderTarget.Stats.CharacterName : "<null>")}");

        _isSunderSequenceActive = false;
        _sunderInitiator = null;
        _sunderTarget = null;
        _sunderTargetSlot = null;
        _sunderAttemptNumber = 0;
    }

    /// <summary>
    /// The push and follow after a successful bull rush (PHB p.154), shared by the PC standard bull
    /// rush (ExecuteSpecialAttack), the NPC executor (TryNPCSpecialAttackIfBeneficial) and the charge
    /// bull rush (ResolveChargeBullRush). The attacker decides first: push the defender 5 ft and stay,
    /// or move with it and push 1 to maxIfFollowing squares (1 + margin / 5, capped by the attacker's
    /// movement limit; <see cref="BullRushRules"/>). An attacker that cannot move (limit 0: prone,
    /// entangled, after a 5-foot step, a charge that used all its movement) is not asked and pushes
    /// 5 ft. A controllable attacker picks in the CombatUI prompt, any other attacker through the AI
    /// hook (AIService.ChooseBullRushPush, CMB-098). Then <see cref="ExecuteBullRushMovement"/> moves
    /// both square by square with their AoOs. When an AoO drops either participant this checks
    /// victory and the party's defeat (CORE-011). Synchronous apart from the PC prompt, so the NPC
    /// path (onComplete null) finishes before this returns. <paramref name="onComplete"/> receives
    /// true when the attacker ends the bull rush dead, dying or unconscious.
    /// <paramref name="diagonalsMovedThisCharge"/> continues the 5-10-5 diagonal count of a charge
    /// path; <paramref name="attackerAlreadyProvoked"/> lists opponents that already had a movement
    /// opportunity against the attacker this action (the charge path and the bull rush initiation,
    /// CMB-113), which get no further AoO as it follows.
    /// </summary>
    private void ResolveBullRushPushAndFollow(
        CharacterController attacker,
        CharacterController target,
        SpecialAttackResult bullRushResult,
        bool isCharge,
        int squaresMovedThisCharge,
        System.Action<bool> onComplete,
        int diagonalsMovedThisCharge = 0,
        ICollection<CharacterController> attackerAlreadyProvoked = null)
    {
        if (attacker == null || attacker.Stats == null || target == null || target.Stats == null
            || bullRushResult == null || !bullRushResult.Success)
        {
            onComplete?.Invoke(false);
            return;
        }

        int margin = bullRushResult.CheckTotal - bullRushResult.OpposedTotal;
        Vector2Int direction = BullRushRules.GetPushDirection(attacker, target);
        bool diagonal = direction.x != 0 && direction.y != 0;
        int movementLimit = BullRushRules.GetMovementLimitSquares(attacker, isCharge, squaresMovedThisCharge);
        int maxIfFollowing = BullRushRules.GetMaxPushSquares(margin, true, movementLimit, diagonal, isCharge ? diagonalsMovedThisCharge : 0);

        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", maxIfFollowing > 0
            ? $"Result: {attacker.Stats.CharacterName} wins ({bullRushResult.CheckTotal} vs {bullRushResult.OpposedTotal}, margin {margin}). "
              + $"Push 5 ft and stay, or move with {target.Stats.CharacterName} up to {maxIfFollowing * 5} ft (movement limit {movementLimit * 5} ft)."
            : $"Result: {attacker.Stats.CharacterName} wins ({bullRushResult.CheckTotal} vs {bullRushResult.OpposedTotal}, margin {margin}). "
              + $"{attacker.Stats.CharacterName} cannot move with {target.Stats.CharacterName} now, so the push is 5 ft."));

        // Nothing to decide when the first square is already blocked.
        if (Grid == null || !Grid.CanPlaceCreature(target.GridPosition + direction, target.GetVisualSquaresOccupied(), target))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Failure("", $"{target.Stats.CharacterName} cannot be pushed; path is blocked."));
            onComplete?.Invoke(false);
            return;
        }

        void Execute(bool follow, int squares)
        {
            follow &= maxIfFollowing > 0;
            int pushSquares = follow ? Mathf.Clamp(squares, 1, maxIfFollowing) : 1;
            BullRushMovementOutcome movement = ExecuteBullRushMovement(attacker, target, direction, pushSquares, follow, attackerAlreadyProvoked);
            bool attackerDown = ThreatSystem.IsMoverIncapacitated(attacker);

            // CORE-011: an AoO during the push or follow may end the combat, on every path.
            if (movement.SomeoneDropped && CurrentPhase != TurnPhase.CombatOver)
            {
                RegisterDefeatedEnemyForXP(target, "BullRush.MovementAoO");
                RegisterDefeatedEnemyForXP(attacker, "BullRush.MovementAoO");
                CheckCombatVictory("BullRush.MovementAoO", target);

                if (CurrentPhase != TurnPhase.CombatOver && AreAllPCsDead())
                {
                    CurrentPhase = TurnPhase.CombatOver;
                    CombatUI?.SetTurnIndicator("DEFEAT! All heroes have fallen!");
                    CombatUI?.SetActionButtonsVisible(false);
                }
            }

            onComplete?.Invoke(attackerDown);
        }

        if (maxIfFollowing <= 0)
        {
            Execute(false, 1);
            return;
        }

        if (attacker.IsControllable && CombatUI != null)
        {
            CombatUI.ShowBullRushExtraPushChoice(attacker, target, maxIfFollowing,
                onSelect: followSquares => Execute(followSquares > 0, followSquares),
                onCancel: () => Execute(false, 1));
            return;
        }

        int chosen = ChooseBullRushPushForAI(attacker, target, maxIfFollowing, out bool aiFollows);
        Execute(aiFollows, chosen);
    }

    /// <summary>
    /// Bull rush push decision for a non-controllable attacker (CMB-098): AIService.ChooseBullRushPush,
    /// which asks the AI profile. Returns the squares to push (1 when not following).
    /// </summary>
    private int ChooseBullRushPushForAI(CharacterController attacker, CharacterController target, int maxIfFollowing, out bool follow)
    {
        if (_aiService != null)
            return _aiService.ChooseBullRushPush(attacker, target, maxIfFollowing, out follow);

        follow = true;
        return Mathf.Max(1, maxIfFollowing);
    }

    /// <summary>Set by <see cref="ResolveBullRushPushAndFollowCoroutine"/> when the attacker ends the push dead, dying or unconscious.</summary>
    private sealed class BullRushPushCoroutineOutcome
    {
        public bool AttackerIncapacitated;
    }

    private IEnumerator ResolveBullRushPushAndFollowCoroutine(
        CharacterController attacker,
        CharacterController target,
        SpecialAttackResult bullRushResult,
        bool isCharge,
        int squaresMovedThisCharge,
        int diagonalsMovedThisCharge = 0,
        ICollection<CharacterController> attackerAlreadyProvoked = null,
        BullRushPushCoroutineOutcome outcome = null)
    {
        bool finished = false;
        ResolveBullRushPushAndFollow(attacker, target, bullRushResult, isCharge, squaresMovedThisCharge,
            attackerDown =>
            {
                if (outcome != null)
                    outcome.AttackerIncapacitated = attackerDown;
                finished = true;
            },
            diagonalsMovedThisCharge, attackerAlreadyProvoked);

        while (!finished)
            yield return null;
    }

    /// <summary>What <see cref="ExecuteBullRushMovement"/> did.</summary>
    private struct BullRushMovementOutcome
    {
        public int Pushed;
        public int Followed;
        public bool SomeoneDropped;
    }

    /// <summary>
    /// Moves the defender up to <paramref name="pushSquares"/> squares along <paramref name="direction"/>,
    /// one square at a time (forced movement, markAsMoved false). The first square is the base 5-ft
    /// push and needs no following. When the attacker follows, each step is one joint move: the
    /// attacker moves into the squares the defender vacates, and every square beyond the first is
    /// pushed only if the attacker moves with the defender on that step (PHB p.154). So the push
    /// ends as soon as the attacker cannot follow: its footprint is blocked, an AoO stops it
    /// (ThreatSystem.ShouldStopMovementAfterAoO) or it is incapacitated. Each step must fit the
    /// mover's whole footprint (SquareGrid.CanPlaceCreature). Before each step each mover provokes
    /// from every opponent threatening a square it leaves, except the other participant, at most
    /// once per opponent per push and per follow (PHB p.154, p.138); the follow set starts from
    /// <paramref name="attackerAlreadyProvoked"/>. Each such AoO may strike the other participant
    /// instead (<see cref="ResolveBullRushAoO"/>). The push also stops when the defender is dead or
    /// off the grid; pending the owner (CMB-115) a prone, dying or unconscious defender keeps being
    /// pushed. Logs only what happened.
    /// </summary>
    private BullRushMovementOutcome ExecuteBullRushMovement(
        CharacterController attacker,
        CharacterController target,
        Vector2Int direction,
        int pushSquares,
        bool follow,
        ICollection<CharacterController> attackerAlreadyProvoked = null)
    {
        string attackerName = attacker.Stats.CharacterName;
        string targetName = target.Stats.CharacterName;
        var outcome = new BullRushMovementOutcome();

        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", follow
            ? $"{attackerName} moves with {targetName}, pushing up to {pushSquares} square{(pushSquares == 1 ? string.Empty : "s")} ({pushSquares * 5} feet)."
            : $"{attackerName} pushes {targetName} 5 feet and stays."));

        var pushProvoked = new HashSet<CharacterController>();
        var followProvoked = attackerAlreadyProvoked != null
            ? new HashSet<CharacterController>(attackerAlreadyProvoked)
            : new HashSet<CharacterController>();
        bool obstructed = false;
        string pushStopReason = null;
        string followStopReason = null;

        for (int step = 1; step <= pushSquares; step++)
        {
            bool baseStep = step == 1;
            if (IsBullRushDefenderGone(target))
            {
                pushStopReason = $"{targetName} is dead";
                break;
            }

            Vector2Int targetNext = target.GridPosition + direction;
            if (!CanBullRushMoverEnter(target, targetNext))
            {
                obstructed = true;
                break;
            }

            // The attacker's half of the joint step, resolved first: past the base square the
            // defender moves only if the attacker can move with it.
            bool attackerMoves = follow;
            Vector2Int attackerNext = attacker.GridPosition + direction;
            if (follow)
            {
                string why = null;
                if (ThreatSystem.IsMoverIncapacitated(attacker))
                {
                    why = "incapacitated";
                }
                else if (!CanBullRushFollowerEnter(attacker, target, attackerNext))
                {
                    why = "blocked path";
                }
                else
                {
                    ThreatSystem.MoverAoOSnapshot before = ThreatSystem.CaptureMoverState(attacker);
                    outcome.SomeoneDropped |= ResolveBullRushStepAoOs(attacker, target, attackerNext, followProvoked, "following");
                    if (ThreatSystem.ShouldStopMovementAfterAoO(attacker, before, out string stopReason))
                        why = stopReason;
                }

                if (why != null)
                {
                    followStopReason = why;
                    if (!baseStep)
                        break;
                    attackerMoves = false;
                }
            }

            outcome.SomeoneDropped |= ResolveBullRushStepAoOs(target, attacker, targetNext, pushProvoked, "pushed");
            if (IsBullRushDefenderGone(target))
            {
                pushStopReason = $"{targetName} is dead";
                break;
            }

            // A push AoO that strays into the follower can drop it; past the base square that ends the push.
            if (attackerMoves && ThreatSystem.IsMoverIncapacitated(attacker))
            {
                followStopReason = "incapacitated";
                attackerMoves = false;
                if (!baseStep)
                    break;
            }

            SquareCell targetCell = Grid.GetCell(targetNext);
            if (targetCell != null)
                target.MoveToCell(targetCell, markAsMoved: false);
            if (target.GridPosition != targetNext)
            {
                obstructed = true;
                break;
            }

            outcome.Pushed++;

            if (attackerMoves)
            {
                SquareCell attackerCell = Grid.GetCell(attackerNext);
                if (attackerCell != null)
                    attacker.MoveToCell(attackerCell);
                if (attacker.GridPosition != attackerNext)
                {
                    followStopReason = "blocked path";
                    break;
                }

                outcome.Followed++;
            }

            if (!follow || followStopReason != null)
                break;
        }

        int pushed = outcome.Pushed;
        if (pushed > 0)
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("↗", $"{targetName} is pushed back {pushed} square{(pushed == 1 ? string.Empty : "s")} ({pushed * 5} feet)."));
        else if (obstructed)
            CombatUI?.ShowCombatLog(CombatLogHelper.Failure("", $"{targetName} cannot be pushed; path is blocked."));

        if (follow)
        {
            int followed = outcome.Followed;
            if (followed > 0)
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{attackerName} moves with {targetName} {followed} square{(followed == 1 ? string.Empty : "s")} ({followed * 5} feet)."));
            if (followStopReason != null)
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", (followed > 0
                    ? $"{attackerName} stops moving with {targetName} ({followStopReason})"
                    : $"{attackerName} cannot move with {targetName} ({followStopReason})")
                    + (pushed > 0 && pushed < pushSquares ? $", so the push ends after {pushed * 5} feet." : ".")));
        }

        if (pushed > 0 && pushed < pushSquares && followStopReason == null)
        {
            string reason = pushStopReason ?? "obstacle reached";
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"Push stops after {pushed} square{(pushed == 1 ? string.Empty : "s")} ({reason})."));
        }

        UpdateAllStatsUI();
        return outcome;
    }

    /// <summary>
    /// Resolves the AoOs one bull rush step provokes from <paramref name="mover"/> leaving its current
    /// squares (ThreatSystem.AnalyzePathForAoOs for a one-square path). <paramref name="partner"/> (the
    /// other bull rush participant) gets none, nor does an opponent already in
    /// <paramref name="alreadyProvoked"/>. Returns true when an AoO left either participant at 0 HP or below.
    /// </summary>
    private bool ResolveBullRushStepAoOs(CharacterController mover, CharacterController partner, Vector2Int next, HashSet<CharacterController> alreadyProvoked, string context)
    {
        List<AoOThreatInfo> threats = ThreatSystem.AnalyzePathForAoOs(mover, new List<Vector2Int> { next }, GetAllCharacters());
        bool dropped = false;
        for (int i = 0; i < threats.Count; i++)
        {
            CharacterController provoker = threats[i] != null ? threats[i].Threatener : null;
            if (provoker == null || provoker == partner || alreadyProvoked.Contains(provoker) || !ThreatSystem.CanMakeAoO(provoker))
                continue;

            alreadyProvoked.Add(provoker);
            int moverHpBefore = mover.Stats.CurrentHP;
            int partnerHpBefore = partner != null && partner.Stats != null ? partner.Stats.CurrentHP : 0;
            ResolveBullRushAoO(provoker, mover, partner, isFromMovement: true, context: $"Bull Rush ({mover.Stats.CharacterName} {context})");
            UpdateAllStatsUI();

            if ((moverHpBefore > 0 && mover.Stats.CurrentHP <= 0)
                || (partner != null && partner.Stats != null && partnerHpBefore > 0 && partner.Stats.CurrentHP <= 0))
                dropped = true;

            if (ThreatSystem.IsMoverIncapacitated(mover))
                break;
        }

        return dropped;
    }

    private bool CanBullRushMoverEnter(CharacterController mover, Vector2Int baseSquare)
        => Grid != null && Grid.CanPlaceCreature(baseSquare, mover.GetVisualSquaresOccupied(), mover);

    /// <summary>
    /// Whether the follower fits at <paramref name="baseSquare"/> once the defender has moved one square
    /// on (the defender's current squares count as free: both move the same way, so they never overlap).
    /// </summary>
    private bool CanBullRushFollowerEnter(CharacterController follower, CharacterController defender, Vector2Int baseSquare)
        => Grid != null && Grid.CanPlaceCreature(baseSquare, follower.GetVisualSquaresOccupied(), follower,
            additionalIgnoredOccupants: new List<CharacterController> { defender });

    /// <summary>The push stops for a dead defender or one no longer in the combat.</summary>
    private bool IsBullRushDefenderGone(CharacterController target)
        => target == null || target.Stats == null || target.IsDead || target.Stats.IsDead || !IsActiveCombatant(target);

    /// <summary>
    /// Overrun's push after a successful targeted overrun: the defender 1 square away from the attacker,
    /// then the attacker into the vacated square. No AoOs. Unchanged behaviour (CMB-116: RAW lets the
    /// attacker move through instead).
    /// </summary>
    private void TryPushTargetAway(CharacterController attacker, CharacterController target, int squares, bool allowAttackerFollow)
    {
        if (attacker == null || target == null || target.Stats == null || Grid == null)
            return;

        Vector2Int direction = BullRushRules.GetPushDirection(attacker, target);
        int pushed = 0;
        for (int i = 0; i < Mathf.Max(1, squares); i++)
        {
            Vector2Int next = target.GridPosition + direction;
            if (!CanBullRushMoverEnter(target, next))
                break;
            SquareCell cell = Grid.GetCell(next);
            if (cell != null)
                target.MoveToCell(cell);
            if (target.GridPosition != next)
                break;
            pushed++;
        }

        if (pushed <= 0)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Failure("", $"{target.Stats.CharacterName} cannot be pushed; path is blocked."));
            return;
        }

        CombatUI?.ShowCombatLog(CombatLogHelper.Info("↗", $"{target.Stats.CharacterName} is pushed back {pushed} square{(pushed == 1 ? string.Empty : "s")} ({pushed * 5} feet)."));
        if (!allowAttackerFollow || attacker.Stats == null)
            return;

        int followed = 0;
        for (int i = 0; i < pushed; i++)
        {
            Vector2Int next = attacker.GridPosition + direction;
            if (!CanBullRushMoverEnter(attacker, next))
                break;
            SquareCell cell = Grid.GetCell(next);
            if (cell != null)
                attacker.MoveToCell(cell);
            if (attacker.GridPosition != next)
                break;
            followed++;
        }

        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", followed > 0
            ? $"{attacker.Stats.CharacterName} follows {followed} square{(followed == 1 ? string.Empty : "s")}."
            : $"{attacker.Stats.CharacterName} cannot follow due to blocked path."));
    }
}
