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

        if (!attacker.HasMeleeWeaponEquipped())
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
    // attack's BAB (PHB p.141 Table 8-2 note 7, p.143). They are steps of the creature's own attack
    // sequence (CharacterController.TryCommitAttack), so the first one spends only the standard action
    // and a second attack or maneuver turns the turn into a full attack (CMB-102).
    // The list lives in ManeuverActionCost.ReplacesMeleeAttack. Bull rush and overrun are not on it:
    // they are standard actions or part of a charge (PHB p.154, p.157).

    private bool CanUseMainHandManeuverAttackOption(CharacterController attacker, string maneuverLabel)
    {
        if (attacker == null || attacker.Actions == null)
            return false;

        return attacker.GetRemainingMainHandAttackSteps() > 0;
    }

    private int GetRemainingMainHandManeuverAttackActions(CharacterController attacker)
    {
        if (attacker == null || attacker.Actions == null)
            return 0;

        return attacker.GetRemainingMainHandAttackSteps();
    }

    private int GetCurrentMainHandManeuverAttackBonusForUI(CharacterController attacker)
    {
        if (attacker == null || attacker.Stats == null)
            return 0;

        if (attacker.GetRemainingMainHandAttackSteps() <= 0)
            return 0;

        int bab = attacker.GetMainHandAttackStepBAB(attacker.ProgressiveAttackPool.MainHandStepsUsed);
        if (_isDualWielding && attacker == ActivePC)
            bab += _mainHandPenalty;
        return bab;
    }

    /// <summary>
    /// Commit one main-hand attack step for a maneuver that replaces an attack. Returns the BAB of
    /// that step (with the PC dual-wield main-hand penalty) and ends the PC Attack-button flow when
    /// no main-hand step remains.
    /// </summary>
    private bool TryCommitMainHandManeuverStep(CharacterController attacker, string maneuverLabel, out int attackBonusUsed, out string reason)
    {
        attackBonusUsed = 0;
        reason = string.Empty;

        if (attacker == null || attacker.Actions == null)
        {
            reason = "No action economy available.";
            return false;
        }

        if (!attacker.TryCommitAttack(AttackStepKind.MainHand, out int step, out reason))
        {
            if (string.IsNullOrWhiteSpace(reason))
                reason = $"No {maneuverLabel.ToLowerInvariant()} attacks remaining this turn.";
            return false;
        }

        attackBonusUsed = attacker.GetMainHandAttackStepBAB(step);
        if (_isDualWielding && attacker == ActivePC)
            attackBonusUsed += _mainHandPenalty;

        if (_isInAttackSequence && _attackingCharacter == attacker && !HasMoreAttacksAvailable())
            EndAttackSequence();

        return true;
    }

    private bool TryConsumeMainHandManeuverAttackAction(CharacterController attacker, string maneuverLabel, out int attackBonusUsed, out int attacksRemaining, out string reason)
    {
        attacksRemaining = 0;

        if (!TryCommitMainHandManeuverStep(attacker, maneuverLabel, out attackBonusUsed, out reason))
            return false;

        attacksRemaining = GetRemainingMainHandManeuverAttackActions(attacker);
        Debug.Log($"[{maneuverLabel}][Flow] Consumed main-hand maneuver attack at BAB {CharacterStats.FormatMod(attackBonusUsed)}; remaining={attacksRemaining}");
        return true;
    }

    public bool CanUseGrappleAttackOption(CharacterController attacker)
    {
        if (attacker == null)
            return false;

        bool isAlreadyGrappling = attacker.IsGrappling();
        if (!isAlreadyGrappling && !attacker.CanUseStandardGrapple())
            return false;

        return CanUseMainHandManeuverAttackOption(attacker, "Grapple");
    }

    public int GetRemainingGrappleAttackActions(CharacterController attacker)
    {
        if (!CanUseGrappleAttackOption(attacker))
            return 0;

        return GetRemainingMainHandManeuverAttackActions(attacker);
    }

    public int GetCurrentGrappleAttackBonus(CharacterController attacker)
    {
        if (!CanUseGrappleAttackOption(attacker))
            return 0;

        return GetCurrentMainHandManeuverAttackBonusForUI(attacker);
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

        return TryConsumeMainHandManeuverAttackAction(attacker, "Grapple", out attackBonusUsed, out attacksRemaining, out reason);
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

    private int GetBullRushMaxPushSquares(SpecialAttackResult bullRushResult)
    {
        if (bullRushResult == null || !bullRushResult.Success)
            return 0;

        int difference = Mathf.Max(0, bullRushResult.CheckTotal - bullRushResult.OpposedTotal);
        int additionalSquares = difference / 5;
        return 1 + additionalSquares;
    }

    private void ResolveBullRushPushAndFollow(CharacterController attacker, CharacterController target, SpecialAttackResult bullRushResult, System.Action onComplete)
    {
        if (attacker == null || target == null || bullRushResult == null || !bullRushResult.Success)
        {
            onComplete?.Invoke();
            return;
        }

        int difference = Mathf.Max(0, bullRushResult.CheckTotal - bullRushResult.OpposedTotal);
        int maxExtraSquares = difference / 5;

        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"Result: {attacker.Stats.CharacterName} wins ({bullRushResult.CheckTotal} vs {bullRushResult.OpposedTotal})"));
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"Difference: {difference}"));

        void ExecuteSelectedExtraDistance(int chosenExtraSquares)
        {
            int clampedExtra = Mathf.Clamp(chosenExtraSquares, 0, maxExtraSquares);
            int totalSquares = 1 + clampedExtra;
            Debug.Log($"[GameManager][BullRushExtraPush] ExecuteSelectedExtraDistance chosen={chosenExtraSquares}, clamped={clampedExtra}, totalSquares={totalSquares}, maxExtraSquares={maxExtraSquares}, frame={Time.frameCount}");

            if (clampedExtra <= 0)
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{attacker.Stats.CharacterName} chooses to push 1 square (base only)"));
            else
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{attacker.Stats.CharacterName} chooses to push {clampedExtra} extra square{(clampedExtra == 1 ? string.Empty : "s")} ({totalSquares} total)"));

            BullRushPushResolution pushResolution = ExecuteBullRushPush(attacker, target, totalSquares);
            UpdateAllStatsUI();

            if (!pushResolution.TargetMoved)
            {
                onComplete?.Invoke();
                return;
            }

            if (attacker.IsControllable && CombatUI != null)
            {
                CombatUI.ShowBullRushFollowChoice(attacker, target, pushResolution.ActualSquares, shouldFollow =>
                {
                    if (shouldFollow)
                        ExecuteBullRushFollow(attacker, pushResolution);
                    else
                        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{attacker.Stats.CharacterName} chooses not to follow."));

                    UpdateAllStatsUI();
                    onComplete?.Invoke();
                });
            }
            else
            {
                ExecuteBullRushFollow(attacker, pushResolution);
                UpdateAllStatsUI();
                onComplete?.Invoke();
            }
        }

        if (maxExtraSquares > 0)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"Can push 0 to {maxExtraSquares} extra squares (base 1 + extra)"));

            if (attacker.IsControllable && CombatUI != null)
            {
                Debug.Log($"[GameManager][BullRushExtraPush] Showing player choice UI. attacker={attacker.Stats.CharacterName}, target={target.Stats.CharacterName}, maxExtraSquares={maxExtraSquares}, actionPanelExists={CombatUI.ActionPanel != null}, actionPanelActiveSelf={(CombatUI.ActionPanel != null && CombatUI.ActionPanel.activeSelf)}, actionPanelActiveInHierarchy={(CombatUI.ActionPanel != null && CombatUI.ActionPanel.activeInHierarchy)}, frame={Time.frameCount}");

                CombatUI.ShowBullRushExtraPushChoice(attacker, target, maxExtraSquares,
                    onSelect: selectedExtraSquares =>
                    {
                        Debug.Log($"[GameManager][BullRushExtraPush] Player selected extra={selectedExtraSquares}, frame={Time.frameCount}");
                        ExecuteSelectedExtraDistance(selectedExtraSquares);
                    },
                    onCancel: () =>
                    {
                        Debug.Log($"[GameManager][BullRushExtraPush] Player cancelled selection. Defaulting to 0 extra squares, frame={Time.frameCount}");
                        ExecuteSelectedExtraDistance(0);
                    });
            }
            else
            {
                Debug.Log($"[GameManager][BullRushExtraPush] Auto-selecting max extra for non-player attacker. attacker={attacker.Stats.CharacterName}, maxExtraSquares={maxExtraSquares}, frame={Time.frameCount}");
                ExecuteSelectedExtraDistance(maxExtraSquares);
            }
        }
        else
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("", "Push 1 square (5 feet) - no extra available"));
            ExecuteSelectedExtraDistance(0);
        }
    }

    private IEnumerator ResolveBullRushPushAndFollowCoroutine(CharacterController attacker, CharacterController target, SpecialAttackResult bullRushResult)
    {
        bool finished = false;
        ResolveBullRushPushAndFollow(attacker, target, bullRushResult, () => finished = true);

        while (!finished)
            yield return null;
    }

    /// <summary>
    /// Direction "straight back" for a push (PHB p.154), from the side where the two footprints
    /// touch rather than from the anchor squares, so multi-square creatures push along the axis
    /// they face. Diagonal only when the footprints touch at a corner.
    /// </summary>
    private static Vector2Int GetPushDirection(CharacterController attacker, CharacterController target)
    {
        int attackerSize = Mathf.Max(1, attacker.GetVisualSquaresOccupied());
        int targetSize = Mathf.Max(1, target.GetVisualSquaresOccupied());
        Vector2Int a = attacker.GridPosition;
        Vector2Int t = target.GridPosition;

        int AxisSign(int attackerMin, int targetMin)
        {
            int attackerMax = attackerMin + attackerSize - 1;
            int targetMax = targetMin + targetSize - 1;
            if (targetMin > attackerMax) return 1;
            if (targetMax < attackerMin) return -1;
            return 0;
        }

        var direction = new Vector2Int(AxisSign(a.x, t.x), AxisSign(a.y, t.y));
        if (direction == Vector2Int.zero)
        {
            // Overlapping footprints: fall back to the difference of the footprint centres.
            float dx = (t.x + targetSize * 0.5f) - (a.x + attackerSize * 0.5f);
            float dy = (t.y + targetSize * 0.5f) - (a.y + attackerSize * 0.5f);
            direction = new Vector2Int(dx > 0f ? 1 : (dx < 0f ? -1 : 0), dy > 0f ? 1 : (dy < 0f ? -1 : 0));
        }

        return direction == Vector2Int.zero ? Vector2Int.right : direction;
    }

    private BullRushPushResolution ExecuteBullRushPush(CharacterController attacker, CharacterController target, int squares)
    {
        var resolution = new BullRushPushResolution
        {
            RequestedSquares = Mathf.Max(1, squares),
            OriginalTargetPosition = target.GridPosition,
            FinalTargetPosition = target.GridPosition,
            Direction = GetPushDirection(attacker, target)
        };

        // Each step must fit the target's whole footprint; its own squares do not block it.
        int targetSize = target.GetVisualSquaresOccupied();
        Vector2Int destination = target.GridPosition;
        for (int i = 0; i < resolution.RequestedSquares; i++)
        {
            Vector2Int next = destination + resolution.Direction;
            if (Grid == null || !Grid.CanPlaceCreature(next, targetSize, target))
            {
                resolution.Obstructed = true;
                break;
            }

            destination = next;
            resolution.ActualSquares++;
        }

        if (resolution.ActualSquares <= 0)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Failure("", $"{target.Stats.CharacterName} cannot be pushed; path is blocked."));
            return resolution;
        }

        SquareCell destinationCell = Grid.GetCell(destination);
        if (destinationCell != null)
            target.MoveToCell(destinationCell);

        if (destinationCell == null || target.GridPosition != destination)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Failure("", $"{target.Stats.CharacterName} cannot be pushed; no valid destination."));
            resolution.ActualSquares = 0;
            resolution.FinalTargetPosition = resolution.OriginalTargetPosition;
            return resolution;
        }

        resolution.FinalTargetPosition = destination;
        int feet = resolution.ActualSquares * 5;
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("↗", $"{target.Stats.CharacterName} is pushed back {resolution.ActualSquares} square{(resolution.ActualSquares == 1 ? string.Empty : "s")} ({feet} feet)."));

        if (resolution.Obstructed && resolution.ActualSquares < resolution.RequestedSquares)
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"Obstacle reached: push stops after {resolution.ActualSquares} square{(resolution.ActualSquares == 1 ? string.Empty : "s")}."));

        return resolution;
    }

    private void ExecuteBullRushFollow(CharacterController attacker, BullRushPushResolution pushResolution)
    {
        if (attacker == null || pushResolution.ActualSquares <= 0)
            return;

        // The follower's whole footprint must fit at each step.
        int attackerSize = attacker.GetVisualSquaresOccupied();
        Vector2Int start = attacker.GridPosition;
        Vector2Int current = start;
        int movedSquares = 0;

        for (int i = 0; i < pushResolution.ActualSquares; i++)
        {
            Vector2Int next = current + pushResolution.Direction;
            if (Grid == null || !Grid.CanPlaceCreature(next, attackerSize, attacker))
                break;

            current = next;
            movedSquares++;
        }

        if (movedSquares <= 0)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{attacker.Stats.CharacterName} cannot follow due to blocked path."));
            return;
        }

        SquareCell followDestination = Grid.GetCell(current);
        if (followDestination == null)
            return;

        attacker.MoveToCell(followDestination);
        if (attacker.GridPosition == start)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{attacker.Stats.CharacterName} cannot follow due to blocked path."));
            return;
        }

        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{attacker.Stats.CharacterName} follows {movedSquares} square{(movedSquares == 1 ? string.Empty : "s")}."));
    }
}
