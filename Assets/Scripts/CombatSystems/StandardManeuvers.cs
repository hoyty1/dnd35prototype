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
            CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} cannot perform Disarm: no main-hand disarm attacks remaining.");
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
            CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} cannot perform Sunder: no main-hand sunder attacks remaining.");
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

    private bool TryStartMainHandSpecialManeuverSequence(CharacterController attacker, string maneuverLabel, out string reason)
    {
        reason = string.Empty;

        if (attacker == null || attacker.Actions == null)
        {
            reason = "No action economy available.";
            return false;
        }

        if (_isInAttackSequence)
        {
            if (_attackingCharacter == attacker)
                return true;

            reason = "Another attack sequence is currently active.";
            return false;
        }

        int fullAttackBudget = Mathf.Max(1, attacker.GetIterativeAttackCount());
        _attackingCharacter = attacker;
        _equippedWeapon = attacker.GetEquippedMainWeapon();
        _totalAttacksUsed = 0;
        _totalAttackBudget = 0;
        _attackSequenceConsumesFullRound = false;
        _isInAttackSequence = true;
        _currentAttackType = AttackType.Melee;

        if (attacker.Actions.HasFullRoundAction)
        {
            attacker.Actions.UseFullRoundAction();
            _totalAttackBudget = fullAttackBudget;
            _attackSequenceConsumesFullRound = true;
        }
        else if (attacker.CommitStandardAction())
        {
            _totalAttackBudget = Mathf.Min(1, fullAttackBudget);
            _attackSequenceConsumesFullRound = false;
        }
        else
        {
            EndAttackSequence();
            reason = "No standard or full-round action remaining.";
            return false;
        }

        int firstBaseBab = attacker.GetIterativeAttackBAB(0);
        _currentAttackBAB = _isDualWielding ? firstBaseBab + _mainHandPenalty : firstBaseBab;
        Debug.Log($"[{maneuverLabel}][Flow] Started shared attack sequence: actor={attacker.Stats.CharacterName}, budget={_totalAttackBudget}, firstBAB={CharacterStats.FormatMod(_currentAttackBAB)}");
        return true;
    }

    private void AdvanceMainHandSequenceAfterSpecialManeuverUse(CharacterController attacker, string maneuverLabel)
    {
        if (!_isInAttackSequence || _attackingCharacter != attacker)
            return;

        _totalAttacksUsed++;
        Debug.Log($"[{maneuverLabel}][Flow] Main-hand maneuver consumed iterative attack {_totalAttacksUsed}/{_totalAttackBudget}.");

        if (_totalAttacksUsed == 1 && !_attackSequenceConsumesFullRound && _totalAttackBudget > 1)
        {
            if (attacker.Actions != null && attacker.Actions.HasMoveAction)
            {
                attacker.Actions.UseMoveAction();
                _attackSequenceConsumesFullRound = true;
                Debug.Log($"[{maneuverLabel}][Flow] Converted shared sequence to full-round after first maneuver attack.");
            }
            else
            {
                _totalAttackBudget = _totalAttacksUsed;
                Debug.LogWarning($"[{maneuverLabel}][Flow] Could not convert to full-round; trimming maneuver attack budget.");
            }
        }

        if (HasMoreAttacksAvailable())
        {
            int nextBaseBab = attacker.GetIterativeAttackBAB(_totalAttacksUsed);
            _currentAttackBAB = _isDualWielding ? nextBaseBab + _mainHandPenalty : nextBaseBab;
            Debug.Log($"[{maneuverLabel}][Flow] Prepared next main-hand maneuver BAB {CharacterStats.FormatMod(_currentAttackBAB)}.");
        }
        else
        {
            Debug.Log($"[{maneuverLabel}][Flow] Main-hand maneuver iterative attacks exhausted; ending shared sequence.");
            EndAttackSequence();
        }
    }

    private bool CanUseMainHandManeuverAttackOption(CharacterController attacker, string maneuverLabel)
    {
        if (attacker == null || attacker.Actions == null)
            return false;

        if (_isInAttackSequence && _attackingCharacter != null && _attackingCharacter != attacker)
        {
            Debug.Log($"[{maneuverLabel}][Flow] Cannot use maneuver: another actor owns the current attack sequence.");
            return false;
        }

        return GetRemainingMainHandManeuverAttackActions(attacker) > 0;
    }

    private int GetRemainingMainHandManeuverAttackActions(CharacterController attacker)
    {
        if (attacker == null || attacker.Actions == null)
            return 0;

        if (_isInAttackSequence && _attackingCharacter == attacker)
            return Mathf.Max(0, _totalAttackBudget - _totalAttacksUsed);

        if (attacker.Actions.HasFullRoundAction)
            return Mathf.Max(1, attacker.GetIterativeAttackCount());

        if (attacker.Actions.HasStandardAction)
            return 1;

        return 0;
    }

    private int GetCurrentMainHandManeuverAttackBonusForUI(CharacterController attacker)
    {
        if (attacker == null || attacker.Stats == null)
            return 0;

        if (_isInAttackSequence && _attackingCharacter == attacker && HasMoreAttacksAvailable())
            return _currentAttackBAB;

        if (attacker.Actions == null)
            return attacker.Stats.BaseAttackBonus;

        if (attacker.Actions.HasFullRoundAction)
        {
            int firstBaseBab = attacker.GetIterativeAttackBAB(0);
            if (_isDualWielding)
                firstBaseBab += _mainHandPenalty;
            return firstBaseBab;
        }

        if (attacker.Actions.HasStandardAction)
            return attacker.Stats.BaseAttackBonus;

        return 0;
    }

    private bool TryConsumeMainHandManeuverAttackAction(CharacterController attacker, string maneuverLabel, out int attackBonusUsed, out int attacksRemaining, out string reason)
    {
        attackBonusUsed = 0;
        attacksRemaining = 0;
        reason = string.Empty;

        if (attacker == null || attacker.Actions == null)
        {
            reason = "No action economy available.";
            return false;
        }

        bool hasActiveOwnedSequence = _isInAttackSequence
            && _attackingCharacter == attacker
            && HasMoreAttacksAvailable();

        bool canStartSequence = !_isInAttackSequence
            && TryStartMainHandSpecialManeuverSequence(attacker, maneuverLabel, out reason)
            && HasMoreAttacksAvailable();

        if (!hasActiveOwnedSequence && !canStartSequence)
        {
            if (string.IsNullOrWhiteSpace(reason))
                reason = $"No {maneuverLabel.ToLowerInvariant()} attacks remaining this turn.";
            return false;
        }

        attackBonusUsed = _currentAttackBAB;
        AdvanceMainHandSequenceAfterSpecialManeuverUse(attacker, maneuverLabel);
        attacksRemaining = GetRemainingMainHandManeuverAttackActions(attacker);
        Debug.Log($"[{maneuverLabel}][Flow] Consumed main-hand maneuver attack at BAB {CharacterStats.FormatMod(attackBonusUsed)}; remaining={attacksRemaining}");
        return true;
    }

    public bool CanUseGrappleAttackOption(CharacterController attacker)
        => CanUseMainHandManeuverAttackOption(attacker, "Grapple");

    public int GetRemainingGrappleAttackActions(CharacterController attacker)
        => GetRemainingMainHandManeuverAttackActions(attacker);

    public int GetCurrentGrappleAttackBonus(CharacterController attacker)
        => GetCurrentMainHandManeuverAttackBonusForUI(attacker);

    private bool TryConsumeGrappleAttackAction(CharacterController attacker, out int attackBonusUsed, out int attacksRemaining, out string reason)
        => TryConsumeMainHandManeuverAttackAction(attacker, "Grapple", out attackBonusUsed, out attacksRemaining, out reason);

    public bool CanUseBullRushAttackOption(CharacterController attacker)
        => CanUseMainHandManeuverAttackOption(attacker, "BullRushAttack");

    public int GetRemainingBullRushAttackActions(CharacterController attacker)
        => GetRemainingMainHandManeuverAttackActions(attacker);

    public int GetCurrentBullRushAttackBonus(CharacterController attacker)
        => GetCurrentMainHandManeuverAttackBonusForUI(attacker);

    private bool TryConsumeBullRushAttackAction(CharacterController attacker, out int attackBonusUsed, out int attacksRemaining, out string reason)
        => TryConsumeMainHandManeuverAttackAction(attacker, "BullRushAttack", out attackBonusUsed, out attacksRemaining, out reason);

    public bool CanUseTripAttackOption(CharacterController attacker)
        => CanUseMainHandManeuverAttackOption(attacker, "Trip");

    public int GetRemainingTripAttackActions(CharacterController attacker)
        => GetRemainingMainHandManeuverAttackActions(attacker);

    public int GetCurrentTripAttackBonus(CharacterController attacker)
        => GetCurrentMainHandManeuverAttackBonusForUI(attacker);

    private bool TryConsumeTripAttackAction(CharacterController attacker, out int attackBonusUsed, out int attacksRemaining, out string reason)
        => TryConsumeMainHandManeuverAttackAction(attacker, "Trip", out attackBonusUsed, out attacksRemaining, out reason);

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
            bool hasActiveMainHandSequence = _isInAttackSequence
                && _attackingCharacter == attacker
                && HasMoreAttacksAvailable();

            bool canStartMainHandSequence = !_isInAttackSequence
                && TryStartMainHandSpecialManeuverSequence(attacker, "Disarm", out reason)
                && HasMoreAttacksAvailable();

            if (!hasActiveMainHandSequence && !canStartMainHandSequence)
            {
                if (string.IsNullOrWhiteSpace(reason))
                    reason = "No main-hand disarm attacks remaining this turn.";
                return false;
            }

            attackBonusUsed = _currentAttackBAB;
            usedOffHand = false;
            disarmWeapon = attacker.GetEquippedMainWeapon();
            AdvanceMainHandSequenceAfterSpecialManeuverUse(attacker, "Disarm");
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
            bool hasActiveMainHandSequence = _isInAttackSequence
                && _attackingCharacter == attacker
                && HasMoreAttacksAvailable();

            bool canStartMainHandSequence = !_isInAttackSequence
                && TryStartMainHandSpecialManeuverSequence(attacker, "Sunder", out reason)
                && HasMoreAttacksAvailable();

            if (!hasActiveMainHandSequence && !canStartMainHandSequence)
            {
                if (string.IsNullOrWhiteSpace(reason))
                    reason = "No main-hand sunder attacks remaining this turn.";
                return false;
            }

            attackBonusUsed = _currentAttackBAB;
            usedOffHand = false;
            sunderWeapon = attacker.GetEquippedMainWeapon();
            AdvanceMainHandSequenceAfterSpecialManeuverUse(attacker, "Sunder");
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
            CombatUI?.ShowCombatLog($"{targetName} has no weapon to disarm!");

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
                    CombatUI.ShowCombatLog($"⚠ {target.Stats.CharacterName}'s selected held item is no longer equipped.");
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
            CombatUI?.ShowCombatLog($"{targetName} has no item to sunder!");

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
                    CombatUI.ShowCombatLog($"⚠ {target.Stats.CharacterName}'s selected item is no longer equipped.");
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

        CombatUI?.ShowCombatLog($"Result: {attacker.Stats.CharacterName} wins ({bullRushResult.CheckTotal} vs {bullRushResult.OpposedTotal})");
        CombatUI?.ShowCombatLog($"Difference: {difference}");

        void ExecuteSelectedExtraDistance(int chosenExtraSquares)
        {
            int clampedExtra = Mathf.Clamp(chosenExtraSquares, 0, maxExtraSquares);
            int totalSquares = 1 + clampedExtra;
            Debug.Log($"[GameManager][BullRushExtraPush] ExecuteSelectedExtraDistance chosen={chosenExtraSquares}, clamped={clampedExtra}, totalSquares={totalSquares}, maxExtraSquares={maxExtraSquares}, frame={Time.frameCount}");

            if (clampedExtra <= 0)
                CombatUI?.ShowCombatLog($"{attacker.Stats.CharacterName} chooses to push 1 square (base only)");
            else
                CombatUI?.ShowCombatLog($"{attacker.Stats.CharacterName} chooses to push {clampedExtra} extra square{(clampedExtra == 1 ? string.Empty : "s")} ({totalSquares} total)");

            BullRushPushResolution pushResolution = ExecuteBullRushPush(attacker, target, totalSquares);
            UpdateAllStatsUI();

            if (!pushResolution.TargetMoved)
            {
                onComplete?.Invoke();
                return;
            }

            if (attacker.IsPlayerControlled && CombatUI != null)
            {
                CombatUI.ShowBullRushFollowChoice(attacker, target, pushResolution.ActualSquares, shouldFollow =>
                {
                    if (shouldFollow)
                        ExecuteBullRushFollow(attacker, pushResolution);
                    else
                        CombatUI?.ShowCombatLog($"{attacker.Stats.CharacterName} chooses not to follow.");

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
            CombatUI?.ShowCombatLog($"Can push 0 to {maxExtraSquares} extra squares (base 1 + extra)");

            if (attacker.IsPlayerControlled && CombatUI != null)
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
            CombatUI?.ShowCombatLog("Push 1 square (5 feet) - no extra available");
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

    private BullRushPushResolution ExecuteBullRushPush(CharacterController attacker, CharacterController target, int squares)
    {
        var resolution = new BullRushPushResolution
        {
            RequestedSquares = Mathf.Max(1, squares),
            OriginalTargetPosition = target.GridPosition,
            FinalTargetPosition = target.GridPosition,
            Direction = target.GridPosition - attacker.GridPosition
        };

        resolution.Direction.x = Mathf.Clamp(resolution.Direction.x, -1, 1);
        resolution.Direction.y = Mathf.Clamp(resolution.Direction.y, -1, 1);
        if (resolution.Direction == Vector2Int.zero)
            resolution.Direction = Vector2Int.right;

        Vector2Int destination = target.GridPosition;
        for (int i = 0; i < resolution.RequestedSquares; i++)
        {
            Vector2Int next = destination + resolution.Direction;
            SquareCell nextCell = Grid.GetCell(next);
            if (nextCell == null || nextCell.IsOccupied)
            {
                resolution.Obstructed = true;
                break;
            }

            destination = next;
            resolution.ActualSquares++;
        }

        resolution.FinalTargetPosition = destination;

        if (resolution.ActualSquares <= 0)
        {
            CombatUI?.ShowCombatLog($"{target.Stats.CharacterName} cannot be pushed; path is blocked.");
            return resolution;
        }

        SquareCell destinationCell = Grid.GetCell(destination);
        if (destinationCell == null)
        {
            CombatUI?.ShowCombatLog($"{target.Stats.CharacterName} cannot be pushed; no valid destination.");
            resolution.ActualSquares = 0;
            resolution.FinalTargetPosition = resolution.OriginalTargetPosition;
            return resolution;
        }

        target.MoveToCell(destinationCell);
        int feet = resolution.ActualSquares * 5;
        CombatUI?.ShowCombatLog($"↗ {target.Stats.CharacterName} is pushed back {resolution.ActualSquares} square{(resolution.ActualSquares == 1 ? string.Empty : "s")} ({feet} feet).");

        if (resolution.Obstructed && resolution.ActualSquares < resolution.RequestedSquares)
            CombatUI?.ShowCombatLog($"⚠ Obstacle reached: push stops after {resolution.ActualSquares} square{(resolution.ActualSquares == 1 ? string.Empty : "s")}.");

        return resolution;
    }

    private void ExecuteBullRushFollow(CharacterController attacker, BullRushPushResolution pushResolution)
    {
        if (attacker == null || pushResolution.ActualSquares <= 0)
            return;

        Vector2Int current = attacker.GridPosition;
        int movedSquares = 0;

        for (int i = 0; i < pushResolution.ActualSquares; i++)
        {
            Vector2Int next = current + pushResolution.Direction;
            SquareCell nextCell = Grid.GetCell(next);
            if (nextCell == null || nextCell.IsOccupied)
                break;

            current = next;
            movedSquares++;
        }

        if (movedSquares <= 0)
        {
            CombatUI?.ShowCombatLog($"{attacker.Stats.CharacterName} cannot follow due to blocked path.");
            return;
        }

        SquareCell followDestination = Grid.GetCell(current);
        if (followDestination == null)
            return;

        attacker.MoveToCell(followDestination);
        CombatUI?.ShowCombatLog($"{attacker.Stats.CharacterName} follows {movedSquares} square{(movedSquares == 1 ? string.Empty : "s")}.");
    }
}
