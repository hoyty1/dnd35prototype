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

        // Sunder needs a weapon or a natural attack that deals slashing or bludgeoning damage (PHB p.158;
        // owner ruling 2026-10-08, CMB-102). The active PC fighting with natural attacks sunders with the
        // natural attack its maneuvers give up next, when that one can (GetPcSunderNaturalAttackIndex); any
        // other creature with the shared check the AI and the NPC executor use (CanSunderWithAttack at its
        // current step). Both sides give up the next natural attack in order (CMB-102 open item 8).
        if (!attacker.HasMeleeWeaponEquipped())
            return false;

        if (IsPcNaturalManeuverAttacker(attacker))
        {
            if (GetPcSunderNaturalAttackIndex(attacker, out _) < 0)
                return false;
        }
        else if (!attacker.CanSunderWithAttack(-1, out _))
        {
            return false;
        }

        return CanUseMainHandManeuverAttackOption(attacker, "Sunder");
    }

    public int GetRemainingMainHandSunderAttackActions(CharacterController attacker)
    {
        if (!CanUseMainHandSunderAttackOption(attacker))
            return 0;

        int remaining = GetRemainingMainHandManeuverAttackActions(attacker);
        if (IsPcNaturalManeuverAttacker(attacker))
            remaining = Mathf.Min(remaining, GetPcSunderCapableNaturalAttackCount(attacker));
        return remaining;
    }

    public int GetCurrentMainHandSunderAttackBonus(CharacterController attacker)
    {
        if (!CanUseMainHandSunderAttackOption(attacker))
            return 0;

        if (IsPcNaturalManeuverAttacker(attacker))
            return attacker.GetNaturalAttackStepBAB(GetPcSunderNaturalAttackIndex(attacker, out _));

        return GetCurrentMainHandManeuverAttackBonusForUI(attacker);
    }

    /// <summary>The active PC fighting with its natural attacks: its maneuvers give up natural attacks it picks (CMB-102).</summary>
    private bool IsPcNaturalManeuverAttacker(CharacterController attacker)
        => attacker != null && attacker == ActivePC && attacker.GetManeuverSubstituteStepKind() == AttackStepKind.NaturalSequence;

    /// <summary>
    /// The natural attack a PC sunder gives up: the one every PC maneuver gives up (the first natural attack
    /// not used this turn, <see cref="GetPcManeuverNaturalAttackIndex(CharacterController, out bool)"/>), when it
    /// deals slashing or bludgeoning damage (PHB p.158, CMB-102); -1 when it does not. This is the NPC rule too:
    /// an NPC sunders with the natural attack at its current step (<see cref="CharacterController.CanSunderWithAttack"/>),
    /// and neither side can skip ahead to a later natural attack (CMB-102 open item 8). At Haste's extra natural
    /// attack (<paramref name="givesUpHasteExtraAttack"/>), the attacker's choice, it is made with a natural attack
    /// that can sunder (<see cref="CharacterController.GetSunderNaturalAttackIndexForStep"/>, as for NPCs; CMB-106).
    /// </summary>
    private int GetPcSunderNaturalAttackIndex(CharacterController attacker, out bool givesUpHasteExtraAttack)
    {
        givesUpHasteExtraAttack = false;
        if (!IsPcNaturalManeuverAttacker(attacker))
            return -1;

        int index = GetPcManeuverNaturalAttackIndex(attacker, out bool hasteStep);
        if (index < 0)
            return -1;

        if (hasteStep)
        {
            index = attacker.GetSunderNaturalAttackIndexForStep(attacker.GetHasteExtraNaturalStepIndex());
            givesUpHasteExtraAttack = index >= 0;
            return index;
        }

        return attacker.CanNaturalAttackSunder(index) ? index : -1;
    }

    /// <summary>Unused natural attacks of the active PC that can sunder, plus Haste's extra natural attack while it is unused and one can: an upper bound, since each sunder needs the next unused natural attack to be one of them.</summary>
    private int GetPcSunderCapableNaturalAttackCount(CharacterController attacker)
    {
        if (!IsPcNaturalManeuverAttacker(attacker))
            return 0;

        int count = attacker.Stats.GetTotalNaturalAttackCount();
        int capable = 0;
        for (int i = 0; i < count; i++)
        {
            if (!_usedNaturalAttackSequenceIndices.Contains(i) && attacker.CanNaturalAttackSunder(i))
                capable++;
        }

        if (attacker.CanUseHasteExtraNaturalAttack() && attacker.GetFirstSunderCapableNaturalAttackIndex() >= 0)
            capable++;
        return capable;
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
    // natural attack is marked used; a sunder is refused when that natural attack deals no slashing or
    // bludgeoning damage (PHB p.158, owner ruling 2026-10-08). Grapple actions while already grappling take the
    // place of an attack too (PHB p.156): an iterative step for a weapon or unarmed fighter (callers pass
    // iterativeOnly), one natural attack for a creature fighting with its natural attacks (CMB-127; a grapple
    // natural attack gives up the natural attack it makes, passed as naturalAttackIndexOverride).
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
    private bool TryCommitMainHandManeuverStep(CharacterController attacker, string maneuverLabel, out int attackBonusUsed, out string reason, bool iterativeOnly = false, bool forSunder = false,
        int naturalAttackIndexOverride = -1, bool hasteOverride = false)
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
        int naturalAttackIndex = -1;
        if (kind == AttackStepKind.NaturalSequence && naturalAttackIndexOverride >= 0)
        {
            // A grapple natural attack gives up the natural attack it makes, PC or NPC (CMB-127). The shared commit
            // refuses one the attack pool has as used; the active PC's natural-attack buttons keep their own record too.
            naturalAttackIndex = naturalAttackIndexOverride;
            givesUpHasteExtraAttack = hasteOverride;
            if (!givesUpHasteExtraAttack && IsNaturalAttackSequenceIndexUsed(attacker, naturalAttackIndex))
            {
                reason = $"Natural attack #{naturalAttackIndex + 1} was already used this turn.";
                return false;
            }
        }
        else if (kind == AttackStepKind.NaturalSequence)
        {
            // A sunder gives up a natural attack that can sunder (PHB p.158, CMB-102); other maneuvers the next unused one.
            naturalAttackIndex = forSunder && IsPcNaturalManeuverAttacker(attacker)
                ? GetPcSunderNaturalAttackIndex(attacker, out givesUpHasteExtraAttack)
                : GetPcManeuverNaturalAttackIndex(attacker, out givesUpHasteExtraAttack);
            if (forSunder && IsPcNaturalManeuverAttacker(attacker) && naturalAttackIndex < 0)
            {
                reason = "The next natural attack deals no slashing or bludgeoning damage, so it cannot sunder (PHB p.158).";
                return false;
            }
        }
        bool committed;
        if (iterativeOnly)
        {
            committed = attacker.TryCommitAttack(AttackStepKind.MainHand, out int step, out reason);
            if (committed)
            {
                attackBonusUsed = attacker.GetMainHandAttackStepBAB(step);
                attacker.ProgressiveAttackPool.RecordSubstituteNaturalAttack(-1); // an iterative step gives up no natural attack
            }
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
            else if (naturalAttackIndex >= 0 && attacker == ActivePC)
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

    private bool TryConsumeMainHandManeuverAttackAction(CharacterController attacker, string maneuverLabel, out int attackBonusUsed, out int attacksRemaining, out string reason, bool iterativeOnly = false,
        int naturalAttackIndexOverride = -1, bool hasteOverride = false)
    {
        attacksRemaining = 0;

        if (!TryCommitMainHandManeuverStep(attacker, maneuverLabel, out attackBonusUsed, out reason, iterativeOnly,
                naturalAttackIndexOverride: naturalAttackIndexOverride, hasteOverride: hasteOverride))
            return false;

        attacksRemaining = GetRemainingMainHandManeuverAttackActions(attacker, iterativeOnly);
        Debug.Log($"[{maneuverLabel}][Flow] Consumed main-hand maneuver attack at BAB {CharacterStats.FormatMod(attackBonusUsed)}; remaining={attacksRemaining}");
        return true;
    }

    // Once grappling, each attack can be a grapple action (PHB p.156: one in place of each of your attacks, at
    // successively lower BAB). Every grapple action that takes an attack (damage, pin, escape check, light weapon,
    // unarmed strike, opponent's weapon) is an iterative step at its iterative BAB, for every creature, so its grapple
    // check rolls at that BAB. The one exception is the grapple attack of a creature fighting with its natural attacks:
    // it is ONE natural attack (MM p.314) and takes the place of that natural attack in the natural sequence, at that
    // attack's bonus (CMB-127). Both kinds share the step counter, so a natural-weapon creature gets a grapple check
    // only while its step count is still inside its iterative ladder. How many grapple natural attacks it gets (one
    // per natural attack, as coded, or the iterative ladder) is open with the owner (CMB-146).

    /// <summary>
    /// True when <paramref name="actionType"/>, made by a grappling <paramref name="attacker"/>, is a grapple natural
    /// attack: Attack Unarmed by a creature fighting with its natural attacks (CMB-127). It takes a natural step; every
    /// other grapple action takes an iterative step.
    /// </summary>
    public static bool IsGrappleNaturalAttackStep(CharacterController attacker, GrappleActionType? actionType)
        => attacker != null
            && actionType == GrappleActionType.AttackUnarmed
            && attacker.IsGrappling()
            && attacker.UsesNaturalAttacksForGrappleAttack();

    /// <summary>Grapple natural attacks still available this turn: natural steps left, capped by the natural attacks left to choose from.</summary>
    private int GetRemainingGrappleNaturalAttackSteps(CharacterController attacker)
    {
        if (attacker == null || !attacker.IsGrappling() || !attacker.UsesNaturalAttacksForGrappleAttack())
            return 0;

        int steps = GetRemainingMainHandManeuverAttackActions(attacker, iterativeOnly: false);
        return Mathf.Min(steps, GetGrappleNaturalAttackOptionsFor(attacker).Count);
    }

    /// <summary>
    /// Whether <paramref name="attacker"/> can take a grapple action that uses an attack: start a grapple when not
    /// grappling; once grappling, <paramref name="actionType"/> (an iterative step, or a natural step for a grapple
    /// natural attack). With no action type, whether any such grapple action is available.
    /// </summary>
    public bool CanUseGrappleAttackOption(CharacterController attacker, GrappleActionType? actionType = null)
        => GetRemainingGrappleAttackActions(attacker, actionType) > 0;

    /// <summary>
    /// Grapple actions of this kind still available this turn (see <see cref="CanUseGrappleAttackOption"/>). With no
    /// action type, the larger of the iterative and the grapple natural attack counts.
    /// </summary>
    public int GetRemainingGrappleAttackActions(CharacterController attacker, GrappleActionType? actionType = null)
    {
        if (attacker == null || attacker.Actions == null)
            return 0;

        if (!attacker.IsGrappling())
            return attacker.CanUseStandardGrapple() ? GetRemainingMainHandManeuverAttackActions(attacker, iterativeOnly: false) : 0;

        if (IsGrappleNaturalAttackStep(attacker, actionType))
            return GetRemainingGrappleNaturalAttackSteps(attacker);

        int iterative = GetRemainingMainHandManeuverAttackActions(attacker, iterativeOnly: true);
        if (actionType.HasValue)
            return iterative;

        return Mathf.Max(iterative, GetRemainingGrappleNaturalAttackSteps(attacker));
    }

    /// <summary>
    /// The BAB the next grapple action of this kind uses: the iterative step's BAB (its grapple check), or for a
    /// grapple natural attack the BAB of the natural attack it makes by default. With no action type, the iterative
    /// step's while one is left, else the grapple natural attack's.
    /// </summary>
    public int GetCurrentGrappleAttackBonus(CharacterController attacker, GrappleActionType? actionType = null)
    {
        if (!CanUseGrappleAttackOption(attacker, actionType))
            return 0;

        if (!attacker.IsGrappling())
            return GetCurrentMainHandManeuverAttackBonusForUI(attacker, iterativeOnly: false);

        bool naturalStep = IsGrappleNaturalAttackStep(attacker, actionType)
            || (!actionType.HasValue && GetRemainingMainHandManeuverAttackActions(attacker, iterativeOnly: true) <= 0);
        if (!naturalStep)
            return GetCurrentMainHandManeuverAttackBonusForUI(attacker, iterativeOnly: true);

        CharacterController.GrappleNaturalAttackOption option = attacker.GetDefaultGrappleNaturalAttackOption(GetGrappleNaturalAttackOptionsFor(attacker));
        return option.NaturalAttackIndex >= 0 ? attacker.GetNaturalAttackStepBAB(option.NaturalAttackIndex) : 0;
    }

    /// <summary>
    /// Commits the attack step a grapple action takes (PHB p.156): starting a grapple replaces an attack (CMB-102);
    /// once grappling, an iterative step, or for a grapple natural attack (<see cref="IsGrappleNaturalAttackStep"/>,
    /// CMB-127) the natural attack <paramref name="naturalAttackIndex"/> and <paramref name="naturalAttackIsHasteExtra"/>
    /// name, which is given up; with -1 the attacker's default option (highest bonus). A grapple natural attack with no
    /// natural attack left, or one already used, is refused before the step is spent. <paramref name="attacksRemaining"/>
    /// counts every grapple action still available.
    /// </summary>
    private bool TryConsumeGrappleAttackAction(CharacterController attacker, out int attackBonusUsed, out int attacksRemaining, out string reason,
        GrappleActionType? actionType = null, int naturalAttackIndex = -1, bool naturalAttackIsHasteExtra = false)
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

        if (!isAlreadyGrappling)
            return TryConsumeMainHandManeuverAttackAction(attacker, "Grapple", out attackBonusUsed, out attacksRemaining, out reason);

        bool naturalStep = IsGrappleNaturalAttackStep(attacker, actionType);
        if (naturalStep)
        {
            if (naturalAttackIndex < 0)
            {
                CharacterController.GrappleNaturalAttackOption option = attacker.GetDefaultGrappleNaturalAttackOption(GetGrappleNaturalAttackOptionsFor(attacker));
                naturalAttackIndex = option.NaturalAttackIndex;
                naturalAttackIsHasteExtra = option.IsHasteExtraAttack;
            }

            if (naturalAttackIndex < 0)
            {
                reason = "no natural attack is left this turn";
                return false;
            }
        }
        else
        {
            naturalAttackIndex = -1;
            naturalAttackIsHasteExtra = false;
        }

        if (!TryConsumeMainHandManeuverAttackAction(attacker, "Grapple", out attackBonusUsed, out _, out reason,
                iterativeOnly: !naturalStep,
                naturalAttackIndexOverride: naturalAttackIndex, hasteOverride: naturalAttackIsHasteExtra))
            return false;

        attacksRemaining = GetRemainingGrappleAttackActions(attacker);
        return true;
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
            if (!TryCommitMainHandManeuverStep(attacker, "Sunder", out attackBonusUsed, out reason, forSunder: true))
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
    /// path. Opponents that already had their movement opportunity against the attacker this round (a
    /// charge path, a move action before a standard bull rush) get no further AoO as it follows: the
    /// follow reads and writes the attacker's round record (ThreatSystem.HasHadMovementOpportunity,
    /// PHB p.138, owner ruling 2026-10-08, CMB-128). An AoO at the bull rush's start is not a
    /// movement opportunity and is not recorded (follows from the owner decision 2026-10-07 that the
    /// entry is the bull rush's own provocation, not movement).
    /// </summary>
    private void ResolveBullRushPushAndFollow(
        CharacterController attacker,
        CharacterController target,
        SpecialAttackResult bullRushResult,
        bool isCharge,
        int squaresMovedThisCharge,
        System.Action<bool> onComplete,
        int diagonalsMovedThisCharge = 0)
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
            BullRushMovementOutcome movement = ExecuteBullRushMovement(attacker, target, direction, pushSquares, follow);
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
            diagonalsMovedThisCharge);

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
    /// from every opponent threatening a square it leaves, except the other participant (PHB p.154):
    /// the follower at most once per opponent per round, shared with its other movement this round
    /// (PHB p.138, owner ruling 2026-10-08, CMB-128), the pushed defender at most once per opponent
    /// per push (forced movement; CMB-150 asks the owner whether it shares the round record). Each
    /// such AoO may strike the other participant instead (<see cref="ResolveBullRushAoO"/>). Owner decision 2026-10-07: a push interrupted by an
    /// AoO goes on unless the defender dies (or leaves the combat), so a defender knocked prone,
    /// dying or unconscious keeps being pushed; a push into an occupied square or a wall stops
    /// there. The squares beyond the first still need the attacker to move with the defender (PHB
    /// p.154), so an AoO that stops or drops the follower ends them. The follower moves with
    /// MoveToCell (markAsMoved true: no 5-foot step afterwards, PHB p.144) and nothing records the
    /// squares followed: owner ruling 2026-10-08 (CMB-129), after a standard bull rush the follow and
    /// the move action are independent in both directions (BullRushRules.GetMovementLimitSquares).
    /// Logs only what happened.
    /// </summary>
    private BullRushMovementOutcome ExecuteBullRushMovement(
        CharacterController attacker,
        CharacterController target,
        Vector2Int direction,
        int pushSquares,
        bool follow)
    {
        string attackerName = attacker.Stats.CharacterName;
        string targetName = target.Stats.CharacterName;
        var outcome = new BullRushMovementOutcome();

        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", follow
            ? $"{attackerName} moves with {targetName}, pushing up to {pushSquares} square{(pushSquares == 1 ? string.Empty : "s")} ({pushSquares * 5} feet)."
            : $"{attackerName} pushes {targetName} 5 feet and stays."));

        var pushProvoked = new HashSet<CharacterController>();
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
                    outcome.SomeoneDropped |= ResolveBullRushStepAoOs(attacker, target, attackerNext, null, "following");
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
    /// other bull rush participant) gets none. <paramref name="pushProvoked"/> is null for the follower:
    /// its step is ordinary movement, so an opponent whose movement opportunity against it already came
    /// this round gets none, and each opponent listed now is recorded for the round, whether or not it
    /// can make the AoO (ThreatSystem.RecordMovementOpportunity, PHB p.138, CMB-128). For the pushed
    /// defender it is the push's own set, as before: an opponent in it gets none, and the round record
    /// is neither read nor written (forced movement, CMB-150). Returns true when an AoO left either
    /// participant at 0 HP or below.
    /// </summary>
    private bool ResolveBullRushStepAoOs(CharacterController mover, CharacterController partner, Vector2Int next, HashSet<CharacterController> pushProvoked, string context)
    {
        bool forced = pushProvoked != null;
        List<AoOThreatInfo> threats = ThreatSystem.AnalyzePathForAoOs(mover, new List<Vector2Int> { next }, GetAllCharacters(), forcedMovement: forced);
        bool dropped = false;
        for (int i = 0; i < threats.Count; i++)
        {
            CharacterController provoker = threats[i] != null ? threats[i].Threatener : null;
            if (provoker == null || provoker == partner)
                continue;
            if (!forced)
                ThreatSystem.RecordMovementOpportunity(provoker, mover);
            if ((forced && pushProvoked.Contains(provoker)) || !ThreatSystem.CanMakeAoO(provoker))
                continue;

            if (forced)
                pushProvoked.Add(provoker);
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

    // ── Trip aftermath: the Improved Trip attack and the counter-trip (PHB p.96, p.158; CMB-079) ──
    // One path for every trip the shared resolver (CharacterController.ResolveTrip) settles: the PC
    // wrapper (ExecuteSpecialAttack), the NPC executor (TryNPCSpecialAttackIfBeneficial, which also runs
    // the trip that replaces a step of an NPC attack or full attack), the free trip after a hit
    // (TryResolveFreeTripOnHit) and the free trip after an AoO hit (ThreatSystem.ExecuteAoO). The rules
    // live in CharacterController (CanTrip, CanCounterTrip, ResolveCounterTrip, the Improved Trip
    // attack); this part makes and logs the follow-up attack after the trip's own log and melee
    // reactions, and asks the defender whether it trips back: a controllable defender through a
    // prompt, an AI-run one through AIService.ShouldCounterTrip. Every caller runs it after the trip's
    // MeleeReactionService.TriggerReactions (ExecuteAoO after the AoO's own reactions).

    private bool _counterTripPromptOpen;
    private CharacterController _counterTripPromptDefender;
    private CharacterController _counterTripPromptTripper;
    private Action _counterTripPromptDone;

    /// <summary>
    /// True while a controllable defender's counter-trip prompt waits for its answer (PHB p.158). An NPC
    /// coroutine whose trip failed waits on it before its next step (NPCMeleeAttackSequence, the ranged
    /// maneuver paths), so the counter-trip lands before the tripper acts again.
    /// </summary>
    public bool IsAwaitingCounterTripChoice => _counterTripPromptOpen;

    /// <summary>
    /// Settles what follows a trip resolved by the shared resolver; callers run it after the trip's own log
    /// and melee reactions. A landed trip with Improved Trip gets its attack here (PHB p.96,
    /// <see cref="CharacterController.ResolveImprovedTripFollowUp"/>, which re-checks that both sides are up,
    /// the tripper can attack and the target is in reach), logged with its Concentration check, melee
    /// reactions, death line and the victory or defeat check. A trip lost at the opposed check
    /// (<see cref="SpecialAttackResult.CounterTripAllowed"/>) lets the defender try to trip back (PHB p.158)
    /// when it is an enemy of the tripper and still able (<see cref="CharacterController.CanCounterTrip"/>):
    /// a controllable defender is asked through a prompt, an AI-run defender decides through
    /// <see cref="AIService.ShouldCounterTrip"/>. A trip by a creature that is not the defender's enemy (an
    /// ally, a neutral) gets no counter-trip offer, a limit awaiting the owner (CMB-136). <paramref name="onDone"/>
    /// runs once everything is settled: at once, or when the prompt is answered (until then
    /// <see cref="IsAwaitingCounterTripChoice"/> is true).
    /// </summary>
    internal void HandleTripAftermath(CharacterController attacker, CharacterController target, SpecialAttackResult result, Action onDone)
    {
        // Made and reported once: ResolveImprovedTripFollowUp clears the pending flag.
        bool followUpPending = result != null && result.ImprovedTripFollowUpPending && attacker != null
            && CurrentPhase != TurnPhase.CombatOver;
        if (followUpPending)
        {
            attacker.ResolveImprovedTripFollowUp(target, result);
            if (result.FollowUpAttack != null)
                ReportImprovedTripFollowUp(attacker, target, result);
            else if (!string.IsNullOrEmpty(result.FollowUpNote))
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("", result.FollowUpNote));
        }

        if (result == null || !result.CounterTripAllowed || attacker == null || target == null
            || CurrentPhase == TurnPhase.CombatOver
            || !TeamUtility.IsEnemy(target, attacker)
            || !target.CanCounterTrip(attacker, out _))
        {
            onDone?.Invoke();
            return;
        }

        if (target.IsControllable && CombatUI != null && !_counterTripPromptOpen)
        {
            OpenCounterTripPrompt(target, attacker, onDone);
            return;
        }

        bool tripBack = _aiService != null
            ? _aiService.ShouldCounterTrip(target, attacker)
            : DND35.AI.AIProfile.DefaultShouldCounterTrip(target, attacker);
        ResolveCounterTripChoice(target, attacker, tripBack);
        onDone?.Invoke();
    }

    private void ReportImprovedTripFollowUp(CharacterController attacker, CharacterController target, SpecialAttackResult result)
    {
        CombatResult attack = result.FollowUpAttack;
        CombatUI?.ShowCombatLog(attack.GetAttackBreakdown(result.FollowUpLabel));

        // Concentration per damage instance (PHB p.70) and melee reactions, as for any melee attack.
        if (attack.Hit && attack.TotalDamage > 0)
            CheckConcentrationOnDamage(target, attack.TotalDamage);
        if (attack.Hit && !attack.IsRangedAttack)
            MeleeReactionService.TriggerReactions(attacker, target, attack);

        if (target != null && target.Stats != null && target.Stats.IsDead)
            CombatUI?.ShowCombatLog(CombatLogHelper.Death("💀", $"{target.Stats.CharacterName} is slain by {attacker.Stats.CharacterName}'s Improved Trip attack!"));

        SettleTripAftermathCasualties("ImprovedTrip.FollowUp", target, attacker);
        UpdateAllStatsUI();
    }

    /// <summary>
    /// Death cleanup and the end-of-combat checks after an Improved Trip attack or a counter-trip, which run
    /// outside the attack loops that normally make them: each dead creature gets its summon cleanup; a dead
    /// enemy-team creature runs the victory check (with its XP registration); when a hero went down and every
    /// hero is down, the defeat state is set as the NPC attack loop sets it. The callers' own checks still run afterwards.
    /// </summary>
    private void SettleTripAftermathCasualties(string sourceContext, params CharacterController[] creatures)
    {
        CharacterController deadEnemy = null;
        bool heroDown = false;
        foreach (CharacterController creature in creatures)
        {
            if (creature == null || creature.Stats == null)
                continue;

            if (creature.Team == CharacterTeam.Player && creature.Stats.CurrentHP <= 0)
                heroDown = true;

            if (!creature.Stats.IsDead)
                continue;

            HandleSummonDeathCleanup(creature);
            if (creature.Team == CharacterTeam.Enemy && deadEnemy == null)
                deadEnemy = creature;
        }

        if (CurrentPhase == TurnPhase.CombatOver)
            return;

        if (deadEnemy != null && CheckCombatVictory(sourceContext, deadEnemy))
            return;

        if (heroDown && AreAllPCsDead())
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI?.SetTurnIndicator("DEFEAT! All heroes have fallen!");
            CombatUI?.SetActionButtonsVisible(false);
        }
    }

    private void OpenCounterTripPrompt(CharacterController defender, CharacterController tripper, Action onDone)
    {
        _counterTripPromptOpen = true;
        _counterTripPromptDefender = defender;
        _counterTripPromptTripper = tripper;
        _counterTripPromptDone = onDone;

        string defenderName = defender.Stats.CharacterName;
        string tripperName = tripper.Stats.CharacterName;
        int checkModifier = defender.GetTripAttackerCheckModifier();
        int resistModifier = tripper.GetTripOrOverrunDefenderCheckModifier();
        int chance = Mathf.RoundToInt(CharacterController.EstimateOpposedCheckWinChance(checkModifier, resistModifier) * 100f);
        string wardNote = defender.HasActiveInvisibilityEffect || defender.Stats.SanctuaryActive
            ? "\n\nTripping back is an attack: it ends your invisibility or Sanctuary."
            : string.Empty;

        CombatUI.ShowCombatLog(CombatLogHelper.Warning("", $"Waiting for {defenderName}'s decision: trip {tripperName} back?"));
        CombatUI.ShowConfirmationDialog(
            title: "Trip Back?",
            message: $"{tripperName} failed to trip {defenderName}.\n\n"
                + $"{defenderName} may react at once and try to trip {tripperName}: a Strength check {CharacterStats.FormatMod(checkModifier)} "
                + $"against {tripperName}'s Strength or Dexterity check {CharacterStats.FormatMod(resistModifier)}. "
                + $"No touch attack and no attack of opportunity (PHB p.158).\n\nChance to trip: about {chance}%."
                + wardNote,
            confirmLabel: "Trip Back",
            cancelLabel: "Decline",
            onConfirm: () => AnswerCounterTripPrompt(true),
            onCancel: () => AnswerCounterTripPrompt(false));
    }

    /// <summary>The prompt's answer: resolve or decline the counter-trip, then run the caller's continuation.</summary>
    private void AnswerCounterTripPrompt(bool tripBack)
    {
        if (!_counterTripPromptOpen)
            return;

        CharacterController defender = _counterTripPromptDefender;
        CharacterController tripper = _counterTripPromptTripper;
        Action done = _counterTripPromptDone;
        ClearCounterTripPrompt(hideDialog: false);

        if (defender != null && tripper != null && CurrentPhase != TurnPhase.CombatOver)
            ResolveCounterTripChoice(defender, tripper, tripBack);
        done?.Invoke();
    }

    /// <summary>Drops an unanswered counter-trip prompt without resolving it (a combat reset or a halted fight).</summary>
    private void ClearCounterTripPrompt(bool hideDialog)
    {
        bool wasOpen = _counterTripPromptOpen;
        _counterTripPromptOpen = false;
        _counterTripPromptDefender = null;
        _counterTripPromptTripper = null;
        _counterTripPromptDone = null;
        if (hideDialog && wasOpen)
            CombatUI?.HideConfirmationDialog();
    }

    private void ResolveCounterTripChoice(CharacterController defender, CharacterController tripper, bool tripBack)
    {
        if (!tripBack)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{defender.Stats.CharacterName} does not try to trip {tripper.Stats.CharacterName} back."));
            return;
        }

        // The counter-trip is a melee attack on the tripper (PHB p.158): the same hostile-action breaks the
        // trip paths run first (turned undead, fascination, charm).
        bool canTripBack = defender.CanCounterTrip(tripper, out _);
        if (canTripBack)
        {
            ProcessTurnUndeadMeleeFearBreak(defender, tripper, isMeleeAttack: true);
            BreakFascinationOnHostileAction(defender, tripper, "hostile action");
            BreakCharmOnHostileAction(defender, tripper);
        }

        SpecialAttackResult counter = defender.ResolveCounterTrip(tripper);
        CombatUI?.ShowCombatLog(CombatLogHelper.Buff("↩", $"COUNTER-TRIP: {counter.Log}"));

        // Melee contact with the tripper, as after every other trip (Fire Shield, Thorns), then the
        // cleanup and end-of-combat checks for a defender those reactions dropped.
        if (canTripBack && counter.CheckRoll > 0)
        {
            MeleeReactionService.TriggerReactions(defender, tripper, null);
            SettleTripAftermathCasualties("CounterTrip", defender, tripper);
        }

        UpdateAllStatsUI();
    }
}
