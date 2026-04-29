using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Extracted action-button state manager for CombatUI.
/// Handles visibility/enabled state/labels for combat action buttons.
/// </summary>
public class ActionButtonPanel : MonoBehaviour
{
    private CombatUI _combatUI;

    public void Initialize(CombatUI combatUI)
    {
        _combatUI = combatUI;
    }

    private GameObject ActionPanel => _combatUI != null ? _combatUI.ActionPanel : null;
    private Button MoveButton => _combatUI != null ? _combatUI.MoveButton : null;
    private Button WithdrawButton => _combatUI != null ? _combatUI.WithdrawButton : null;
    private Button FiveFootStepButton => _combatUI != null ? _combatUI.FiveFootStepButton : null;
    private Button DropProneButton => _combatUI != null ? _combatUI.DropProneButton : null;
    private Button StandUpButton => _combatUI != null ? _combatUI.StandUpButton : null;
    private Button CrawlButton => _combatUI != null ? _combatUI.CrawlButton : null;
    private Button AttackButton => _combatUI != null ? _combatUI.AttackButton : null;
    private Button AttackThrownButton => _combatUI != null ? _combatUI.AttackThrownButton : null;
    private Button AttackOffHandButton => _combatUI != null ? _combatUI.AttackOffHandButton : null;
    private Button AttackOffHandThrownButton => _combatUI != null ? _combatUI.AttackOffHandThrownButton : null;
    private Button FullAttackButton => _combatUI != null ? _combatUI.FullAttackButton : null;
    private Button SpecialAttackButton => _combatUI != null ? _combatUI.SpecialAttackButton : null;
    private Button TurnUndeadButton => _combatUI != null ? _combatUI.TurnUndeadButton : null;
    private Button SmiteButton => _combatUI != null ? _combatUI.SmiteButton : null;
    private Button GrappleActionsButton => _combatUI != null ? _combatUI.GrappleActionsButton : null;
    private Button GrappleDamageButton => _combatUI != null ? _combatUI.GrappleDamageButton : null;
    private Button GrappleLightWeaponAttackButton => _combatUI != null ? _combatUI.GrappleLightWeaponAttackButton : null;
    private Button GrappleUnarmedAttackButton => _combatUI != null ? _combatUI.GrappleUnarmedAttackButton : null;
    private Button GrapplePinButton => _combatUI != null ? _combatUI.GrapplePinButton : null;
    private Button GrappleBreakPinButton => _combatUI != null ? _combatUI.GrappleBreakPinButton : null;
    private Button GrappleEscapeArtistButton => _combatUI != null ? _combatUI.GrappleEscapeArtistButton : null;
    private Button GrappleEscapeCheckButton => _combatUI != null ? _combatUI.GrappleEscapeCheckButton : null;
    private Button GrappleMoveButton => _combatUI != null ? _combatUI.GrappleMoveButton : null;
    private Button GrappleUseOpponentWeaponButton => _combatUI != null ? _combatUI.GrappleUseOpponentWeaponButton : null;
    private Button GrappleDisarmSmallObjectButton => _combatUI != null ? _combatUI.GrappleDisarmSmallObjectButton : null;
    private Button GrappleReleasePinnedButton => _combatUI != null ? _combatUI.GrappleReleasePinnedButton : null;
    private Button AidAnotherButton => _combatUI != null ? _combatUI.AidAnotherButton : null;
    private Button OverrunButton => _combatUI != null ? _combatUI.OverrunButton : null;
    private Button ChargeButton => _combatUI != null ? _combatUI.ChargeButton : null;
    private Button DualWieldButton => _combatUI != null ? _combatUI.DualWieldButton : null;
    private Button EndTurnButton => _combatUI != null ? _combatUI.EndTurnButton : null;
    private Button ReloadButton => _combatUI != null ? _combatUI.ReloadButton : null;
    private Button DropEquippedItemButton => _combatUI != null ? _combatUI.DropEquippedItemButton : null;
    private Button PickUpItemButton => _combatUI != null ? _combatUI.PickUpItemButton : null;
    private Button DamageModeToggleButton => _combatUI != null ? _combatUI.DamageModeToggleButton : null;
    private Text ActionStatusText => _combatUI != null ? _combatUI.ActionStatusText : null;
    private Button FlurryOfBlowsButton => _combatUI != null ? _combatUI.FlurryOfBlowsButton : null;
    private Button RageButton => _combatUI != null ? _combatUI.RageButton : null;
    private Text RageStatusText => _combatUI != null ? _combatUI.RageStatusText : null;
    private Button CastSpellButton => _combatUI != null ? _combatUI.CastSpellButton : null;
    private Button DischargeTouchButton => _combatUI != null ? _combatUI.DischargeTouchButton : null;
    private Button DismissDisguiseSelfButton => _combatUI != null ? _combatUI.DismissDisguiseSelfButton : null;
    private Button DismissExpeditiousRetreatButton => _combatUI != null ? _combatUI.DismissExpeditiousRetreatButton : null;
    private Button AttackDefensivelyButton => _combatUI != null ? _combatUI.AttackDefensivelyButton : null;
    private Button FullAttackDefensivelyButton => _combatUI != null ? _combatUI.FullAttackDefensivelyButton : null;

    private sealed class ActionButtonState
    {
        public bool Visible;
        public bool Enabled;
        public string Label;

        public ActionButtonState(bool visible, bool enabled, string label = null)
        {
            Visible = visible;
            Enabled = enabled;
            Label = label;
        }
    }

    private sealed class ActionButtonStates
    {
        private readonly Dictionary<Button, ActionButtonState> _states = new Dictionary<Button, ActionButtonState>();

        public void Set(Button button, ActionButtonState state)
        {
            if (button == null || state == null)
                return;

            _states[button] = state;
        }

        public IEnumerable<KeyValuePair<Button, ActionButtonState>> Enumerate()
        {
            return _states;
        }
    }

    private sealed class ActionButtonContext
    {
        public GameManager Gm;
        public ActionEconomy Actions;
        public bool HasIterativeAttacks;
        public bool HasRapidShot;
        public bool IsGrappling;
        public bool IsProne;
        public bool IsPinned;
        public bool IsTurned;
        public bool CanAttackWithWeapon;
        public ItemData EquippedWeapon;
        public bool UsingUnarmedStrike;
        public string AttackSourceLabel;
        public bool UsingInnateNaturalAttacks;
        public List<NaturalAttackButtonOption> NaturalAttackOptions;
        public bool HasMultipleNaturalAttackTypes;
        public bool IterativeWeaponSequenceActive;
        public bool IterativeWeaponFullRoundStage;
        public bool HasGrappleState;
        public CharacterController GrappleOpponent;
        public bool ActorPinnedInGrappleState;
        public bool OpponentPinnedByActor;
        public bool IsPinningOpponent;
        public bool HasGrappleAttackAvailable;
        public bool HasBullRushAttackAvailable;
        public bool HasTripAttackAvailable;
        public bool HasDisarmAttackAvailable;
        public bool HasCoupDeGraceAttackAvailable;
        public bool HasThrowableMeleeWeapon;
        public bool IterativeThrownSequenceActive;
        public bool IterativeThrownFullRoundStage;
        public bool CanFightDefensively;
        public bool CanStartMainAfterOffHand;
        public bool HasStandardAttack;
        public bool CanContinueIterativeAttack;
        public bool HasOffHandWeapon;
        public bool HasThrowableOffHandWeapon;
        public bool OffHandUsed;
        public bool OffHandAvailableThisTurn;
        public bool ShowOnlyPinnedEscapeActions;
        public bool ShowOnlyPinnerActions;
        public bool HasIterativeGrappleAttack;
        public int GrappleAttacksRemaining;
        public int CurrentGrappleAttackBonus;
        public string IterativeTag;
        public string GrappleOpponentName;
        public bool HasAnimateRopeEscapeAction;
        public string AnimateRopeEscapeDisabledReason;
    }

    private sealed class NaturalAttackButtonOption
    {
        public int SequenceIndex;
        public string AttackName;
        public bool IsPrimary;
    }

    public void UpdateActionButtons(CharacterController pc)
    {
        if (_combatUI == null)
            return;

        if (pc == null || ActionPanel == null)
        {
            HideAllActionButtons();
            return;
        }

        ActionButtonContext context = BuildActionButtonContext(pc);
        ConfigureAttackButtonListeners(context);

        ActionButtonStates states = ComputeActionButtonStates(pc, context);
        ApplyButtonStates(states);

        UpdateRageStatus(pc);
        _combatUI.UpdateDamageModeToggle(pc);

        if (context.IsGrappling)
            HideNonGrappleActionButtons();

        if (ActionStatusText != null)
            ActionStatusText.text = context.Actions.GetStatusString();
    }

    public void RefreshActionButtons()
    {
        if (_combatUI == null)
            return;

        GameManager gm = GameManager.Instance;
        if (gm != null && gm.CurrentCharacter != null)
            UpdateActionButtons(gm.CurrentCharacter);
    }

    private ActionButtonContext BuildActionButtonContext(CharacterController pc)
    {
        ActionButtonContext context = new ActionButtonContext();

        context.Gm = GameManager.Instance;
        context.Actions = pc.Actions;
        context.HasIterativeAttacks = pc.Stats.IterativeAttackCount > 1;
        context.HasRapidShot = pc.Stats.HasFeat("Rapid Shot");
        context.IsGrappling = pc.IsGrappling();
        context.IsProne = pc.HasCondition(CombatConditionType.Prone);
        context.IsPinned = pc.HasCondition(CombatConditionType.Pinned);
        context.IsTurned = pc.HasCondition(CombatConditionType.Turned);
        context.CanAttackWithWeapon = pc.CanAttackWithEquippedWeapon(out _);
        context.EquippedWeapon = pc.GetEquippedMainWeapon();
        NaturalAttackDefinition primaryNaturalAttack = pc.Stats != null ? pc.Stats.GetPrimaryNaturalAttack() : null;
        bool usingNaturalAttack = context.EquippedWeapon == null && primaryNaturalAttack != null;
        context.UsingInnateNaturalAttacks = usingNaturalAttack;
        context.NaturalAttackOptions = BuildNaturalAttackButtonOptions(pc);
        context.HasMultipleNaturalAttackTypes = context.NaturalAttackOptions.Count > 1;
        context.UsingUnarmedStrike = context.EquippedWeapon == null && !usingNaturalAttack;
        context.AttackSourceLabel = context.EquippedWeapon != null
            ? context.EquippedWeapon.Name
            : (usingNaturalAttack ? (string.IsNullOrWhiteSpace(primaryNaturalAttack.Name) ? "Natural attack" : primaryNaturalAttack.Name) : "Unarmed strike");

        context.IterativeWeaponSequenceActive = context.Gm != null && context.Gm.IsIterativeAttackSequenceActiveFor(pc);
        context.IterativeWeaponFullRoundStage = context.Gm != null && context.Gm.IsIterativeAttackInFullRoundStage(pc);

        context.HasGrappleState = pc.TryGetGrappleState(out CharacterController grappleOpponent, out _, out bool actorPinnedInGrappleState, out bool opponentPinnedByActor);
        context.GrappleOpponent = grappleOpponent;
        context.ActorPinnedInGrappleState = actorPinnedInGrappleState;
        context.OpponentPinnedByActor = opponentPinnedByActor;
        context.IsPinningOpponent = pc.IsPinningOpponent();

        context.HasGrappleAttackAvailable = context.Gm != null && context.Gm.CanUseGrappleAttackOption(pc);
        context.HasBullRushAttackAvailable = context.Gm != null && context.Gm.CanUseBullRushAttackOption(pc);
        context.HasTripAttackAvailable = context.Gm != null && context.Gm.CanUseTripAttackOption(pc);
        context.HasDisarmAttackAvailable = context.Gm != null && context.Gm.CanUseDisarmAttackOption(pc);
        context.HasCoupDeGraceAttackAvailable = context.Gm != null && context.Gm.CanUseCoupDeGraceAttackOption(pc);

        context.HasThrowableMeleeWeapon = context.Gm != null && context.Gm.HasThrowableMeleeWeaponEquipped(pc);
        context.IterativeThrownSequenceActive = context.Gm != null && context.Gm.IsIterativeThrownAttackSequenceActiveFor(pc);
        context.IterativeThrownFullRoundStage = context.Gm != null && context.Gm.IsIterativeThrownAttackInFullRoundStage(pc);

        context.CanFightDefensively = pc.Stats.BaseAttackBonus >= 1;
        context.CanStartMainAfterOffHand = context.Gm != null && context.Gm.IsOffHandAttackUsedThisTurn(pc) && context.Actions.HasMoveAction && !context.IterativeWeaponSequenceActive;
        context.HasStandardAttack = context.Actions.HasStandardAction || context.CanStartMainAfterOffHand;
        context.CanContinueIterativeAttack = context.IterativeWeaponSequenceActive;

        context.HasOffHandWeapon = pc.HasOffHandWeaponEquipped();
        context.HasThrowableOffHandWeapon = pc.HasThrowableOffHandWeaponEquipped();
        context.OffHandUsed = context.Gm != null && context.Gm.IsOffHandAttackUsedThisTurn(pc);
        context.OffHandAvailableThisTurn = context.Gm == null || context.Gm.IsOffHandAttackAvailableThisTurn(pc);

        context.ShowOnlyPinnedEscapeActions = context.IsGrappling && (context.IsPinned || context.ActorPinnedInGrappleState);
        context.ShowOnlyPinnerActions = context.IsGrappling && context.IsPinningOpponent && !context.ShowOnlyPinnedEscapeActions;
        context.HasIterativeGrappleAttack = context.Gm != null && context.Gm.CanUseGrappleAttackOption(pc);
        context.GrappleAttacksRemaining = context.Gm != null ? context.Gm.GetRemainingGrappleAttackActions(pc) : 0;
        context.CurrentGrappleAttackBonus = context.Gm != null ? context.Gm.GetCurrentGrappleAttackBonus(pc) : 0;
        context.IterativeTag = $"BAB {CharacterStats.FormatMod(context.CurrentGrappleAttackBonus)} | {context.GrappleAttacksRemaining} left";
        context.GrappleOpponentName = context.GrappleOpponent != null && context.GrappleOpponent.Stats != null
            ? context.GrappleOpponent.Stats.CharacterName
            : "Opponent";

        context.AnimateRopeEscapeDisabledReason = string.Empty;
        context.HasAnimateRopeEscapeAction = context.Gm != null && context.Gm.CanUseAnimateRopeEscapeAction(pc, out context.AnimateRopeEscapeDisabledReason);

        return context;
    }

    private List<NaturalAttackButtonOption> BuildNaturalAttackButtonOptions(CharacterController pc)
    {
        var options = new List<NaturalAttackButtonOption>();
        if (pc == null || pc.Stats == null)
            return options;

        List<NaturalAttackDefinition> validAttacks = pc.Stats.GetValidNaturalAttacks();
        if (validAttacks.Count == 0)
            return options;

        GameManager gm = GameManager.Instance;
        bool shouldGroupByAttackTypeAtTurnStart = gm != null && gm.Combat_GetWeaponAttacksCommittedThisTurn() <= 0;
        HashSet<string> seenAttackTypes = shouldGroupByAttackTypeAtTurnStart
            ? new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            : null;

        int sequenceIndex = 0;
        for (int i = 0; i < validAttacks.Count; i++)
        {
            NaturalAttackDefinition attack = validAttacks[i];
            int count = Mathf.Max(1, attack.Count);
            string attackName = string.IsNullOrWhiteSpace(attack.Name) ? "Natural attack" : attack.Name.Trim();

            for (int repeat = 0; repeat < count; repeat++)
            {
                int currentSequenceIndex = sequenceIndex++;
                bool isUsed = gm != null && gm.IsNaturalAttackSequenceIndexUsed(pc, currentSequenceIndex);
                if (isUsed)
                    continue;

                if (shouldGroupByAttackTypeAtTurnStart)
                {
                    if (seenAttackTypes == null || !seenAttackTypes.Add(attackName))
                        continue;
                }

                options.Add(new NaturalAttackButtonOption
                {
                    SequenceIndex = currentSequenceIndex,
                    AttackName = attackName,
                    IsPrimary = attack.IsPrimary
                });
            }
        }

        return options;
    }

    private static string BuildNaturalAttackButtonLabel(NaturalAttackButtonOption option)
    {
        if (option == null)
            return "Attack: Natural attack";

        string role = option.IsPrimary ? "Primary" : "Secondary";
        return $"Attack: {option.AttackName} ({role})";
    }

    private void ConfigureAttackButtonListeners(ActionButtonContext context)
    {
        if (AttackButton == null || context == null)
            return;

        GameManager gm = context.Gm;
        if (gm == null)
            return;

        if (context.UsingInnateNaturalAttacks && context.NaturalAttackOptions != null && context.NaturalAttackOptions.Count > 0)
        {
            NaturalAttackButtonOption primaryOption = context.NaturalAttackOptions[0];
            AttackButton.onClick.RemoveAllListeners();
            AttackButton.onClick.AddListener(() => gm.OnNaturalAttackButtonPressed(primaryOption.SequenceIndex, primaryOption.AttackName));

            if (AttackThrownButton != null)
            {
                AttackThrownButton.onClick.RemoveAllListeners();
                if (context.NaturalAttackOptions.Count > 1)
                {
                    NaturalAttackButtonOption secondaryOption = context.NaturalAttackOptions[1];
                    AttackThrownButton.onClick.AddListener(() => gm.OnNaturalAttackButtonPressed(secondaryOption.SequenceIndex, secondaryOption.AttackName));
                }
                else
                {
                    AttackThrownButton.onClick.AddListener(() => gm.OnThrownAttackButtonPressed());
                }
            }

            return;
        }

        AttackButton.onClick.RemoveAllListeners();
        AttackButton.onClick.AddListener(() => gm.OnAttackButtonPressed());

        if (AttackThrownButton != null)
        {
            AttackThrownButton.onClick.RemoveAllListeners();
            AttackThrownButton.onClick.AddListener(() => gm.OnThrownAttackButtonPressed());
        }
    }

    private ActionButtonStates ComputeActionButtonStates(CharacterController pc, ActionButtonContext context)
    {
        ActionButtonStates states = new ActionButtonStates();

        ComputeMovementActionStates(pc, context, states);
        ComputeAttackActionStates(pc, context, states);
        ComputeSpecialActionStates(pc, context, states);
        ComputeClassAndSpellActionStates(pc, context, states);
        ComputeEquipmentActionStates(pc, context, states);
        ComputeGrappleActionStates(pc, context, states);
        ComputeAlwaysAvailableStates(states);

        return states;
    }

    private void ComputeMovementActionStates(CharacterController pc, ActionButtonContext context, ActionButtonStates states)
    {
        bool canMoveByActions = context.Actions.HasMoveAction || context.Actions.CanConvertStandardToMove;
        bool blockedByFiveFootStep = pc.HasTakenFiveFootStep;
        bool blockedByIterativeAttackSequence = context.IterativeWeaponFullRoundStage || (context.Gm != null && context.Gm.IsIterativeThrownAttackInFullRoundStage(pc));
        bool canMove = canMoveByActions && !blockedByFiveFootStep && !blockedByIterativeAttackSequence && !context.IsProne && !context.IsPinned;

        string moveLabel = "Move (Used)";
        if (context.IsPinned) moveLabel = "Move (Pinned: grapple escape only)";
        else if (context.IsProne) moveLabel = "Move (Stand up first)";
        else if (blockedByFiveFootStep) moveLabel = "Move (After 5-ft step: no)";
        else if (blockedByIterativeAttackSequence) moveLabel = "Move (Consumed by Full Round Attack)";
        else if (context.Actions.HasMoveAction) moveLabel = "Move (Move Action)";
        else if (context.Actions.CanConvertStandardToMove) moveLabel = "Move (Std→Move)";

        states.Set(MoveButton, new ActionButtonState(true, canMove, moveLabel));

        string withdrawDisabledReason = context.Gm != null ? context.Gm.GetWithdrawDisabledReason(pc) : "Unavailable";
        bool canWithdraw = string.IsNullOrEmpty(withdrawDisabledReason);
        string withdrawLabel = canWithdraw ? "Withdraw (Full-Round, 2x move)" : $"Withdraw ({withdrawDisabledReason})";
        states.Set(WithdrawButton, new ActionButtonState(true, canWithdraw, withdrawLabel));

        string fiveFootDisabledReason = context.Gm != null ? context.Gm.GetFiveFootStepDisabledReason(pc) : "Unavailable";
        bool canFiveFootStep = string.IsNullOrEmpty(fiveFootDisabledReason);
        states.Set(FiveFootStepButton, new ActionButtonState(true, canFiveFootStep, canFiveFootStep ? "5-Foot Step (Free)" : $"5-Foot Step ({fiveFootDisabledReason})"));

        string dropProneDisabledReason = context.Gm != null ? context.Gm.GetDropProneDisabledReason(pc) : "Unavailable";
        bool canDropProne = string.IsNullOrEmpty(dropProneDisabledReason);
        states.Set(DropProneButton, new ActionButtonState(!context.IsProne, canDropProne, canDropProne ? "Drop Prone (Free)" : $"Drop Prone ({dropProneDisabledReason})"));

        string standUpDisabledReason = context.Gm != null ? context.Gm.GetStandUpDisabledReason(pc) : "Unavailable";
        bool canStandUp = string.IsNullOrEmpty(standUpDisabledReason);
        states.Set(StandUpButton, new ActionButtonState(context.IsProne, canStandUp, canStandUp ? "Stand Up (Move, AoO)" : $"Stand Up ({standUpDisabledReason})"));

        string crawlDisabledReason = context.Gm != null ? context.Gm.GetCrawlDisabledReason(pc) : "Unavailable";
        bool canCrawl = string.IsNullOrEmpty(crawlDisabledReason);
        states.Set(CrawlButton, new ActionButtonState(context.IsProne, canCrawl, canCrawl ? "Crawl 5 ft (Move, AoO)" : $"Crawl ({crawlDisabledReason})"));
    }

    private void ComputeAttackActionStates(CharacterController pc, ActionButtonContext context, ActionButtonStates states)
    {
        ComputePrimaryAttackStates(pc, context, states);
        ComputeOffHandAttackStates(pc, context, states);
        ComputeDefensiveAndChargeStates(pc, context, states);
        ComputeDualWieldAndFullAttackStates(pc, context, states);
    }

    private void ComputePrimaryAttackStates(CharacterController pc, ActionButtonContext context, ActionButtonStates states)
    {
        bool usingNaturalOptions = context.UsingInnateNaturalAttacks && context.NaturalAttackOptions != null && context.NaturalAttackOptions.Count > 0;

        bool canSingleAttack = usingNaturalOptions
            ? (context.Gm != null && context.Gm.CanUseNaturalAttackOption(pc) && !context.IsPinned && !context.IsTurned)
            : ((context.Gm != null && context.Gm.CanUsePrimaryAttackOption(pc)) && context.CanAttackWithWeapon && !context.IsPinned && !context.IsTurned);

        bool showAttackButton = usingNaturalOptions
            ? (context.IsPinned || context.NaturalAttackOptions.Count > 0)
            : (context.IsPinned || context.HasStandardAttack || context.CanContinueIterativeAttack);

        string attackLabel;
        if (context.IsPinned)
            attackLabel = "Attack (Pinned: grapple escape only)";
        else if (context.IsTurned)
            attackLabel = "Attack (Turned: must flee)";
        else if (context.UsingInnateNaturalAttacks && context.NaturalAttackOptions != null && context.NaturalAttackOptions.Count > 0)
            attackLabel = BuildNaturalAttackButtonLabel(context.NaturalAttackOptions[0]);
        else if (!context.CanAttackWithWeapon)
            attackLabel = "Attack (Reload first)";
        else if (context.CanStartMainAfterOffHand)
            attackLabel = context.HasThrowableMeleeWeapon ? "Attack (Melee - Full Round)" : "Attack (Full Round)";
        else if (context.Gm != null)
            attackLabel = context.HasThrowableMeleeWeapon
                ? (context.IterativeWeaponFullRoundStage ? "Attack (Melee - Full Round)" : "Attack (Melee)")
                : context.Gm.GetIterativeAttackButtonLabel(pc, context.UsingUnarmedStrike, context.AttackSourceLabel);
        else
            attackLabel = context.UsingUnarmedStrike ? $"Attack (Standard, {context.AttackSourceLabel})" : "Attack (Standard)";

        states.Set(AttackButton, new ActionButtonState(showAttackButton, canSingleAttack, attackLabel));

        bool showingSecondaryNaturalAttack = context.UsingInnateNaturalAttacks
            && context.NaturalAttackOptions != null
            && context.NaturalAttackOptions.Count > 1;

        bool showThrownButton = showingSecondaryNaturalAttack
            ? (context.NaturalAttackOptions.Count > 1 && !context.IsPinned)
            : context.HasThrowableMeleeWeapon && (context.HasStandardAttack || context.IterativeThrownSequenceActive) && !context.IsPinned;

        bool canThrowAttack = showingSecondaryNaturalAttack
            ? (context.Gm != null && context.Gm.CanUseNaturalAttackOption(pc) && !context.IsTurned)
            : showThrownButton && context.Gm != null && context.Gm.CanUseThrownAttackOption(pc) && context.CanAttackWithWeapon && !context.IsTurned;

        string thrownLabel;
        if (context.IsTurned)
            thrownLabel = showingSecondaryNaturalAttack ? "Attack (Turned: must flee)" : "Attack (Thrown - Turned)";
        else if (context.UsingInnateNaturalAttacks && context.NaturalAttackOptions != null && context.NaturalAttackOptions.Count > 1)
            thrownLabel = BuildNaturalAttackButtonLabel(context.NaturalAttackOptions[1]);
        else
            thrownLabel = !context.CanAttackWithWeapon ? "Attack (Thrown - Reload first)" : (context.IterativeThrownFullRoundStage ? "Attack (Thrown - Full Round)" : "Attack (Thrown)");

        states.Set(AttackThrownButton, new ActionButtonState(showThrownButton, canThrowAttack, thrownLabel));
    }

    private void ComputeOffHandAttackStates(CharacterController pc, ActionButtonContext context, ActionButtonStates states)
    {
        Debug.Log("=== UPDATE ACTION BUTTONS ===");
        Debug.Log($"[UI] Character: {pc.Stats.CharacterName}");
        Debug.Log($"[UI] Updating off-hand buttons for {pc.Stats.CharacterName}: hasOffHandWeapon={context.HasOffHandWeapon}, hasThrowableOffHandWeapon={context.HasThrowableOffHandWeapon}, offHandUsed={context.OffHandUsed}, offHandAvailableThisTurn={context.OffHandAvailableThisTurn}");

        bool showOffHandButton = context.HasOffHandWeapon && !context.OffHandUsed && context.OffHandAvailableThisTurn;
        bool canOffHandAttack = showOffHandButton && context.Gm != null && context.Gm.CanUseOffHandAttackOption(pc) && !context.IsPinned && !context.IsTurned;
        string offHandLabel = context.IsPinned
            ? "Attack (Off-Hand - Pinned)"
            : (context.IsTurned
                ? "Attack (Off-Hand - Turned)"
                : (context.OffHandUsed
                    ? "Attack (Off-Hand - Used)"
                    : (!canOffHandAttack ? "Attack (Off-Hand - Unavailable)" : "Attack (Off-Hand)")));
        Debug.Log($"[UI] Off-hand available: {context.OffHandAvailableThisTurn}");
        Debug.Log($"[UI] Off-hand melee button: show={showOffHandButton}, enabled={canOffHandAttack}, pinned={context.IsPinned}");
        Debug.Log($"[UI] Off-hand melee button label: {offHandLabel}");
        states.Set(AttackOffHandButton, new ActionButtonState(showOffHandButton, canOffHandAttack, offHandLabel));

        bool showOffHandThrownButton = context.HasThrowableOffHandWeapon && !context.OffHandUsed && context.OffHandAvailableThisTurn;
        bool canOffHandThrownAttack = showOffHandThrownButton && context.Gm != null && context.Gm.CanUseOffHandThrownAttackOption(pc) && !context.IsPinned && !context.IsTurned;
        string offHandThrownLabel = context.IsPinned
            ? "Attack (Off-Hand Thrown - Pinned)"
            : (context.IsTurned
                ? "Attack (Off-Hand Thrown - Turned)"
                : (context.OffHandUsed
                    ? "Attack (Off-Hand Thrown - Used)"
                    : (!canOffHandThrownAttack ? "Attack (Off-Hand Thrown - Unavailable)" : "Attack (Off-Hand Thrown)")));
        Debug.Log($"[UI] Off-hand thrown button: show={showOffHandThrownButton}, enabled={canOffHandThrownAttack}, pinned={context.IsPinned}");
        Debug.Log($"[UI] Off-hand thrown button label: {offHandThrownLabel}");
        states.Set(AttackOffHandThrownButton, new ActionButtonState(showOffHandThrownButton, canOffHandThrownAttack, offHandThrownLabel));
    }

    private void ComputeDefensiveAndChargeStates(CharacterController pc, ActionButtonContext context, ActionButtonStates states)
    {
        bool canAttackDefensively = context.Actions.HasStandardAction && context.CanFightDefensively && context.CanAttackWithWeapon && !context.IsPinned && !context.IsTurned;
        string attackDefensivelyLabel;
        if (context.IsPinned) attackDefensivelyLabel = "Fighting Defensively (Pinned: no)";
        else if (context.IsTurned) attackDefensivelyLabel = "Fighting Defensively (Turned: no)";
        else if (!context.CanFightDefensively) attackDefensivelyLabel = "Fighting Defensively (Std) [BAB +1]";
        else if (!context.Actions.HasStandardAction) attackDefensivelyLabel = "Fighting Defensively (Std) [Used]";
        else if (!context.CanAttackWithWeapon) attackDefensivelyLabel = "Fighting Defensively (Std) [Reload first]";
        else attackDefensivelyLabel = context.UsingUnarmedStrike ? $"Fighting Defensively (Std, {context.AttackSourceLabel})" : "Fighting Defensively (Std)";
        states.Set(AttackDefensivelyButton, new ActionButtonState(true, canAttackDefensively, attackDefensivelyLabel));

        bool hasFullRound = context.Actions.HasFullRoundAction;
        bool hasMeleeThreat = pc.HasMeleeWeaponEquipped();
        bool fatigueRestricted = pc.Stats != null && pc.Stats.IsFatiguedOrExhausted;
        bool entangledRestricted = pc.Stats != null && pc.Stats.IsEntangledState;
        bool isExhausted = pc.Stats != null && pc.Stats.IsExhaustedState;
        bool blockedByFiveFootStep = pc.HasTakenFiveFootStep;
        bool blockedByProne = context.IsProne;
        bool hasAnyChargeTarget = context.Gm != null && context.Gm.HasAnyValidChargeTarget(pc);
        bool canChargeTarget = hasFullRound && hasMeleeThreat && !fatigueRestricted && !entangledRestricted && !blockedByFiveFootStep && !blockedByProne && !context.IsPinned && !context.IsTurned && hasAnyChargeTarget;

        string chargeLabel;
        if (context.IsPinned) chargeLabel = "Charge (Pinned: no)";
        else if (context.IsTurned) chargeLabel = "Charge (Turned: no)";
        else if (!hasMeleeThreat) chargeLabel = "Charge (Need melee)";
        else if (fatigueRestricted) chargeLabel = isExhausted ? "Charge (Exhausted)" : "Charge (Fatigued)";
        else if (entangledRestricted) chargeLabel = "Charge (Entangled)";
        else if (blockedByProne) chargeLabel = "Charge (Prone)";
        else if (blockedByFiveFootStep) chargeLabel = "Charge (After 5-ft step: no)";
        else if (!hasFullRound) chargeLabel = "Charge (Used)";
        else if (!hasAnyChargeTarget) chargeLabel = "Charge (No valid target)";
        else chargeLabel = "Charge (Full-Round)";
        states.Set(ChargeButton, new ActionButtonState(hasMeleeThreat || fatigueRestricted || entangledRestricted || blockedByFiveFootStep || blockedByProne, canChargeTarget, chargeLabel));
    }

    private void ComputeDualWieldAndFullAttackStates(CharacterController pc, ActionButtonContext context, ActionButtonStates states)
    {
        // Dual Wield entry point is intentionally removed from the main Actions window.
        states.Set(DualWieldButton, new ActionButtonState(false, false));

        bool showNaturalFullAttack = context.UsingInnateNaturalAttacks && context.HasMultipleNaturalAttackTypes;
        bool canNaturalFullAttack = showNaturalFullAttack
            && context.Actions.HasFullRoundAction
            && !context.IsPinned
            && !context.IsTurned
            && context.CanAttackWithWeapon;

        string fullAttackLabel;
        if (!showNaturalFullAttack)
            fullAttackLabel = "Full Attack";
        else if (context.IsPinned)
            fullAttackLabel = "Full Attack (Pinned: grapple escape only)";
        else if (context.IsTurned)
            fullAttackLabel = "Full Attack (Turned: must flee)";
        else if (!context.Actions.HasFullRoundAction)
            fullAttackLabel = "Full Attack (Used)";
        else
            fullAttackLabel = "Full Attack (All Natural Weapons)";

        states.Set(FullAttackButton, new ActionButtonState(showNaturalFullAttack, canNaturalFullAttack, fullAttackLabel));
        states.Set(FullAttackDefensivelyButton, new ActionButtonState(false, false));
    }

    private void ComputeSpecialActionStates(CharacterController pc, ActionButtonContext context, ActionButtonStates states)
    {
        bool canImprovedFeintMove = pc != null
            && pc.Stats != null
            && pc.Stats.HasFeat("Improved Feint")
            && (context.Actions.HasMoveAction || context.Actions.CanConvertStandardToMove);

        bool canSpecialAttack = !context.IsTurned
            && (context.IsPinned
                ? context.Actions.HasStandardAction
                : (context.Actions.HasStandardAction || context.Actions.HasFullRoundAction || canImprovedFeintMove || context.HasGrappleAttackAvailable || context.HasBullRushAttackAvailable || context.HasTripAttackAvailable || context.HasDisarmAttackAvailable || context.HasCoupDeGraceAttackAvailable));

        string specialAttackLabel;
        if (context.IsPinned)
            specialAttackLabel = context.Actions.HasStandardAction ? "Grapple Escape Actions (Standard)" : "Grapple Escape Actions (Used)";
        else if (context.IsTurned)
            specialAttackLabel = "Special Attack (Turned: no actions)";
        else if (context.Actions.HasStandardAction)
            specialAttackLabel = canImprovedFeintMove ? "Special Attack (Std / Feint Move)" : "Special Attack (Standard)";
        else if (context.HasGrappleAttackAvailable && context.Gm != null)
            specialAttackLabel = $"Special Attack (Grapple BAB {CharacterStats.FormatMod(context.Gm.GetCurrentGrappleAttackBonus(pc))}, {context.Gm.GetRemainingGrappleAttackActions(pc)} left)";
        else if (context.HasBullRushAttackAvailable && context.Gm != null)
            specialAttackLabel = $"Special Attack (Bull Rush BAB {CharacterStats.FormatMod(context.Gm.GetCurrentBullRushAttackBonus(pc))}, {context.Gm.GetRemainingBullRushAttackActions(pc)} left)";
        else if (context.HasTripAttackAvailable && context.Gm != null)
            specialAttackLabel = $"Special Attack (Trip BAB {CharacterStats.FormatMod(context.Gm.GetCurrentTripAttackBonus(pc))}, {context.Gm.GetRemainingTripAttackActions(pc)} left)";
        else if (context.HasDisarmAttackAvailable && context.Gm != null)
            specialAttackLabel = $"Special Attack (Disarm BAB {CharacterStats.FormatMod(context.Gm.GetCurrentDisarmAttackBonus(pc))}, {context.Gm.GetRemainingDisarmAttackActions(pc)} left)";
        else if (context.HasCoupDeGraceAttackAvailable)
            specialAttackLabel = "Special Attack (Coup de Grace ready)";
        else if (canImprovedFeintMove)
            specialAttackLabel = "Special Attack (Feint Move)";
        else
            specialAttackLabel = "Special Attack (Used)";

        states.Set(SpecialAttackButton, new ActionButtonState(true, canSpecialAttack, specialAttackLabel));

        string turnUndeadReason = string.Empty;
        bool canUseTurnUndead = context.Gm != null && context.Gm.CanUseTurnUndead(pc, out turnUndeadReason);
        bool canEverUseTurnUndead = pc != null && pc.Stats != null && (pc.Stats.IsCleric || (pc.Stats.IsPaladin && pc.Stats.Level >= 4));
        int remaining = context.Gm != null ? context.Gm.GetRemainingTurnUndeadAttempts(pc) : 0;
        int maxAttempts = pc != null && pc.Stats != null ? pc.Stats.MaxTurnUndeadAttemptsPerDay : 0;

        string turnUndeadLabel;
        if (!canEverUseTurnUndead)
            turnUndeadLabel = "Turn Undead (Class feature locked)";
        else if (canUseTurnUndead)
            turnUndeadLabel = $"Turn Undead ({remaining}/{maxAttempts} left)";
        else
            turnUndeadLabel = $"Turn Undead ({turnUndeadReason}; {remaining}/{maxAttempts})";

        states.Set(TurnUndeadButton, new ActionButtonState(canEverUseTurnUndead, canUseTurnUndead, turnUndeadLabel));

        string smiteReason = string.Empty;
        bool canUseSmite = context.Gm != null && context.Gm.CanUseTemplateSmite(pc, out smiteReason);
        bool hasTemplateSmite = pc != null && pc.Stats != null && (pc.Stats.HasTemplateSmiteEvil || pc.Stats.HasTemplateSmiteGood);
        string smiteAxis = pc != null && pc.Stats != null && pc.Stats.HasTemplateSmiteGood ? "Good" : "Evil";
        string smiteLabel = !hasTemplateSmite
            ? "Smite (N/A)"
            : (canUseSmite ? $"Smite {smiteAxis} (Standard, 1/day)" : $"Smite {smiteAxis} ({smiteReason})");
        states.Set(SmiteButton, new ActionButtonState(hasTemplateSmite, canUseSmite, smiteLabel));

        bool standaloneDisarmWasVisible = _combatUI.HideStandaloneDisarmButtonsInMainActionsForActionPanel();
        Debug.Log($"[CombatUI][Actions] actor={pc.Stats.CharacterName} specialAttackVisible=true specialAttackInteractable={canSpecialAttack} grappleAvailable={context.HasGrappleAttackAvailable} bullRushAvailable={context.HasBullRushAttackAvailable} tripAvailable={context.HasTripAttackAvailable} disarmViaSpecialAvailable={context.HasDisarmAttackAvailable} coupDeGraceAvailable={context.HasCoupDeGraceAttackAvailable} standaloneDisarmSuppressed={standaloneDisarmWasVisible}");

        bool canUseLegacyGrappleButton = context.IsGrappling && context.Actions.HasStandardAction;
        states.Set(GrappleActionsButton, new ActionButtonState(context.IsGrappling, canUseLegacyGrappleButton, canUseLegacyGrappleButton ? "Grapple Actions" : "Grapple Actions (Used)"));

        // Aid Another and Overrun are surfaced via the Special Attack submenu.
        states.Set(AidAnotherButton, new ActionButtonState(false, false));
        states.Set(OverrunButton, new ActionButtonState(false, false));
        Debug.Log($"[CombatUI][Actions] Hiding main-menu Overrun button for {pc.Stats.CharacterName}; use Special Attack submenu instead.");
    }

    private void ComputeClassAndSpellActionStates(CharacterController pc, ActionButtonContext context, ActionButtonStates states)
    {
        bool isMonk = pc.Stats.IsMonk;
        bool canFlurry = isMonk && context.Actions.HasFullRoundAction && !context.IsPinned && !context.IsTurned;
        string flurryLabel = "Flurry of Blows (N/A)";
        if (context.IsPinned)
            flurryLabel = "Flurry of Blows (Pinned: no)";
        else if (context.IsTurned)
            flurryLabel = "Flurry of Blows (Turned: no)";
        else if (canFlurry)
        {
            int[] bonuses = pc.Stats.GetFlurryOfBlowsBonuses();
            string bonusStr = string.Join("/", System.Array.ConvertAll(bonuses, b => CharacterStats.FormatMod(b)));
            flurryLabel = $"Flurry of Blows x{bonuses.Length} ({bonusStr})";
        }
        states.Set(FlurryOfBlowsButton, new ActionButtonState(isMonk, canFlurry, flurryLabel));

        bool isBarbarian = pc.Stats.IsBarbarian;
        bool canRage = isBarbarian && !pc.Stats.IsRaging && !pc.Stats.IsFatiguedOrExhausted && pc.Stats.RagesUsedToday < pc.Stats.MaxRagesPerDay;
        string rageLabel;
        if (pc.Stats.IsRaging) rageLabel = $"RAGING ({pc.Stats.RageRoundsRemaining} rds)";
        else if (pc.Stats.IsExhaustedState) rageLabel = "Rage (Exhausted)";
        else if (pc.Stats.IsFatiguedState) rageLabel = "Rage (Fatigued)";
        else if (canRage) rageLabel = $"Rage ({pc.Stats.MaxRagesPerDay - pc.Stats.RagesUsedToday}/day)";
        else rageLabel = "Rage (Used)";
        states.Set(RageButton, new ActionButtonState(isBarbarian, canRage, rageLabel));

        bool isSpellcaster = pc.Stats.IsSpellcaster;
        SpellcastingComponent spellComp = pc.GetComponent<SpellcastingComponent>();
        bool hasCastableSpells = isSpellcaster && spellComp != null && spellComp.HasAnyCastablePreparedSpell();
        bool canCast = context.Actions.HasStandardAction && hasCastableSpells && !context.IsTurned;
        string castBaseLabel;
        if (!canCast)
            castBaseLabel = context.IsTurned ? "Cast Spell (Turned: no)" : "Cast Spell (N/A)";
        else if (context.IsPinned)
            castBaseLabel = "Cast Spell (Std, pinned: concentration/components apply)";
        else if (context.IsGrappling)
            castBaseLabel = "Cast Spell (Std, grappled: concentration/components apply)";
        else
            castBaseLabel = "Cast Spell (Standard)";

        int asfChance = (pc.Stats != null && pc.Stats.IsAffectedByArcaneSpellFailure)
            ? Mathf.Clamp(pc.Stats.ArcaneSpellFailure, 0, 100)
            : 0;
        string castSpellLabel = asfChance > 0 ? $"{castBaseLabel}\n⚠ ASF {asfChance}%" : castBaseLabel;

        states.Set(CastSpellButton, new ActionButtonState(hasCastableSpells, canCast, castSpellLabel));

        _combatUI.EnsureDischargeTouchButtonExistsForActionPanel();
        SpellcastingComponent touchSpellComp = pc.GetComponent<SpellcastingComponent>();
        bool hasHeldTouchCharge = pc.Stats.IsSpellcaster && touchSpellComp != null && touchSpellComp.HasHeldTouchCharge && touchSpellComp.HeldTouchSpell != null;
        string heldName = hasHeldTouchCharge ? touchSpellComp.HeldTouchSpell.Name : "Touch";
        states.Set(DischargeTouchButton, new ActionButtonState(hasHeldTouchCharge, hasHeldTouchCharge, $"Discharge {heldName}"));

        bool hasActiveDisguiseSelf = context.Gm != null && context.Gm.HasActiveDisguiseSelf(pc);
        bool canDismissDisguiseSelf = hasActiveDisguiseSelf && context.Actions.HasStandardAction && !context.IsTurned;
        string dismissLabel = hasActiveDisguiseSelf
            ? (canDismissDisguiseSelf ? "Dismiss Disguise Self (Standard)" : "Dismiss Disguise Self (Used)")
            : "Dismiss Disguise Self";
        states.Set(DismissDisguiseSelfButton, new ActionButtonState(hasActiveDisguiseSelf, canDismissDisguiseSelf, dismissLabel));

        bool hasActiveExpeditiousRetreat = context.Gm != null && context.Gm.HasActiveExpeditiousRetreat(pc);
        bool canDismissExpeditiousRetreat = hasActiveExpeditiousRetreat && !context.IsTurned;
        string dismissExpeditiousLabel = hasActiveExpeditiousRetreat
            ? "Dismiss Expeditious Retreat (Free)"
            : "Dismiss Expeditious Retreat";
        states.Set(DismissExpeditiousRetreatButton, new ActionButtonState(hasActiveExpeditiousRetreat, canDismissExpeditiousRetreat, dismissExpeditiousLabel));
    }

    private void ComputeEquipmentActionStates(CharacterController pc, ActionButtonContext context, ActionButtonStates states)
    {
        ItemData weapon = pc.GetEquippedMainWeapon();
        bool hasReloadableWeaponEquipped = weapon != null && weapon.RequiresReload;
        bool isWeaponLoaded = !hasReloadableWeaponEquipped || weapon.IsLoaded;
        string reloadDisabledReason = "Unavailable";
        ReloadActionType reloadAction = ReloadActionType.None;
        bool canReload = hasReloadableWeaponEquipped && context.Gm != null
                         && context.Gm.CanReloadEquippedWeapon(pc, out reloadDisabledReason, out reloadAction);

        string actionLabel = hasReloadableWeaponEquipped ? CharacterController.GetReloadActionLabel(pc.GetEffectiveReloadAction(weapon)) : "Move";
        string reloadLabel;
        if (isWeaponLoaded) reloadLabel = $"Reload ({actionLabel}) [Loaded]";
        else if (canReload) reloadLabel = $"Reload ({actionLabel})";
        else reloadLabel = $"Reload ({actionLabel}) [{reloadDisabledReason}]";

        states.Set(ReloadButton, new ActionButtonState(hasReloadableWeaponEquipped, canReload, reloadLabel));

        string dropDisabledReason = context.Gm != null ? context.Gm.GetDropEquippedItemDisabledReason(pc) : "Unavailable";
        bool canDropEquipped = string.IsNullOrEmpty(dropDisabledReason);
        states.Set(DropEquippedItemButton, new ActionButtonState(true, canDropEquipped, canDropEquipped ? "Drop Equipped (Free)" : $"Drop Equipped ({dropDisabledReason})"));

        bool hasNearbyGroundItem = context.Gm != null && context.Gm.HasGroundItemInPickupRange(pc);
        string pickupDisabledReason = context.Gm != null ? context.Gm.GetPickUpItemDisabledReason(pc) : "Unavailable";
        bool canPickUp = string.IsNullOrEmpty(pickupDisabledReason);
        states.Set(PickUpItemButton, new ActionButtonState(hasNearbyGroundItem, canPickUp, canPickUp ? "Pick Up Item (Move, AoO)" : $"Pick Up Item ({pickupDisabledReason})"));
    }

    private void ComputeGrappleActionStates(CharacterController pc, ActionButtonContext context, ActionButtonStates states)
    {
        ComputeGrappleOffenseStates(pc, context, states);
        ComputeGrappleEscapeStates(pc, context, states);
        ComputeGrappleControlStates(pc, context, states);
    }

    private void ComputeGrappleOffenseStates(CharacterController pc, ActionButtonContext context, ActionButtonStates states)
    {
        bool canGrappleDamage = context.IsGrappling && !context.ShowOnlyPinnedEscapeActions && context.HasIterativeGrappleAttack && CanUseGrappleActionWhilePinning(pc, GrappleActionType.DamageOpponent);
        string grappleDamageBaseLabel = context.ShowOnlyPinnerActions ? $"Damage {context.GrappleOpponentName}" : "Grapple: Damage";
        states.Set(GrappleDamageButton, new ActionButtonState(
            context.IsGrappling && !context.ShowOnlyPinnedEscapeActions,
            canGrappleDamage,
            canGrappleDamage ? $"{grappleDamageBaseLabel} ({context.IterativeTag})" : $"{grappleDamageBaseLabel} (No attacks left)"));

        bool hasMainHandLightWeapon = pc.CanAttackWithLightWeaponWhileGrappling(out ItemData mainHandLightWeapon, out _);
        bool canLightWeaponAttack = context.IsGrappling
            && !context.ShowOnlyPinnedEscapeActions
            && context.HasIterativeGrappleAttack
            && hasMainHandLightWeapon
            && CanUseGrappleActionWhilePinning(pc, GrappleActionType.AttackWithLightWeapon);
        string lightWeaponLabel = hasMainHandLightWeapon ? mainHandLightWeapon.Name : "Main-hand light weapon";
        string grappleLightLabel = canLightWeaponAttack
            ? $"Grapple: Attack {lightWeaponLabel} (-4, {context.IterativeTag})"
            : (hasMainHandLightWeapon && !context.HasIterativeGrappleAttack
                ? $"Grapple: Attack {lightWeaponLabel} (-4, No attacks left)"
                : $"Grapple: Attack {lightWeaponLabel} (-4, N/A)");
        states.Set(GrappleLightWeaponAttackButton, new ActionButtonState(context.IsGrappling && !context.ShowOnlyPinnedEscapeActions && !context.ShowOnlyPinnerActions, canLightWeaponAttack, grappleLightLabel));

        bool canUnarmedByRule = pc.CanAttackUnarmedWhileGrappling(out _);
        bool canUnarmedAttack = context.IsGrappling
            && !context.ShowOnlyPinnedEscapeActions
            && context.HasIterativeGrappleAttack
            && canUnarmedByRule
            && CanUseGrappleActionWhilePinning(pc, GrappleActionType.AttackUnarmed);
        string grappleUnarmedLabel = canUnarmedAttack
            ? $"Grapple: Attack Unarmed (-4, {context.IterativeTag})"
            : (canUnarmedByRule && !context.HasIterativeGrappleAttack
                ? "Grapple: Attack Unarmed (-4, No attacks left)"
                : "Grapple: Attack Unarmed (-4, N/A)");
        states.Set(GrappleUnarmedAttackButton, new ActionButtonState(context.IsGrappling && !context.ShowOnlyPinnedEscapeActions && !context.ShowOnlyPinnerActions, canUnarmedAttack, grappleUnarmedLabel));

        bool canPin = context.IsGrappling && !context.ShowOnlyPinnedEscapeActions && context.HasIterativeGrappleAttack && CanUseGrappleActionWhilePinning(pc, GrappleActionType.PinOpponent);
        states.Set(GrapplePinButton, new ActionButtonState(
            context.IsGrappling && !context.ShowOnlyPinnedEscapeActions && !context.ShowOnlyPinnerActions,
            canPin,
            canPin ? $"Grapple: Pin Opponent ({context.IterativeTag})" : "Grapple: Pin Opponent (No attacks left)"));

        // Deprecated for current pin model: pinned creatures now use escape actions only.
        states.Set(GrappleBreakPinButton, new ActionButtonState(false, false));
    }

    private void ComputeGrappleEscapeStates(CharacterController pc, ActionButtonContext context, ActionButtonStates states)
    {
        bool hasAnimateRopeEscape = !context.IsGrappling && (context.HasAnimateRopeEscapeAction || !string.IsNullOrEmpty(context.AnimateRopeEscapeDisabledReason));
        bool canEscapeArtist = context.IsGrappling && context.Actions.HasStandardAction;
        bool escapeArtistVisible = (context.IsGrappling && !context.ShowOnlyPinnerActions) || hasAnimateRopeEscape;
        bool escapeArtistEnabled = context.IsGrappling ? canEscapeArtist : context.HasAnimateRopeEscapeAction;
        string escapeArtistLabel;
        if (context.IsGrappling)
        {
            escapeArtistLabel = canEscapeArtist ? "Grapple: Escape Artist (Std)" : "Grapple: Escape Artist (Used)";
        }
        else
        {
            escapeArtistLabel = context.HasAnimateRopeEscapeAction
                ? "Animate Rope: Escape (Std)"
                : $"Animate Rope: Escape ({context.AnimateRopeEscapeDisabledReason})";
        }

        states.Set(GrappleEscapeArtistButton, new ActionButtonState(
            escapeArtistVisible,
            escapeArtistEnabled,
            escapeArtistLabel));

        bool canEscapeCheck = context.IsGrappling
            && context.HasIterativeGrappleAttack
            && CanUseGrappleActionWhilePinning(pc, GrappleActionType.OpposedGrappleEscape);
        states.Set(GrappleEscapeCheckButton, new ActionButtonState(
            context.IsGrappling && !context.ShowOnlyPinnerActions,
            canEscapeCheck,
            canEscapeCheck ? $"Grapple: Escape Check ({context.IterativeTag})" : "Grapple: Escape Check (No attacks left)"));

        bool canGrappleMove = context.IsGrappling && !context.ShowOnlyPinnedEscapeActions && context.Actions.HasStandardAction && CanUseGrappleActionWhilePinning(pc, GrappleActionType.MoveHalfSpeed);
        string grappleMoveLabel = canGrappleMove
            ? (context.ShowOnlyPinnerActions ? $"Move (dragging {context.GrappleOpponentName})" : "Grapple: Move (Std)")
            : "Grapple: Move (Used/Blocked)";
        states.Set(GrappleMoveButton, new ActionButtonState(context.IsGrappling && !context.ShowOnlyPinnedEscapeActions, canGrappleMove, grappleMoveLabel));
    }

    private void ComputeGrappleControlStates(CharacterController pc, ActionButtonContext context, ActionButtonStates states)
    {
        bool opponentHasLightWeapon = context.HasGrappleState && context.GrappleOpponent != null
            && context.GrappleOpponent.GetEquippedLightHandWeaponOptions() != null
            && context.GrappleOpponent.GetEquippedLightHandWeaponOptions().Count > 0;
        bool canUseOpponentWeapon = context.IsGrappling
            && !context.ShowOnlyPinnedEscapeActions
            && context.HasIterativeGrappleAttack
            && opponentHasLightWeapon
            && CanUseGrappleActionWhilePinning(pc, GrappleActionType.UseOpponentWeapon);
        string useOpponentBaseLabel = context.ShowOnlyPinnerActions
            ? $"Use {context.GrappleOpponentName}'s Weapon"
            : "Grapple: Use Opponent Weapon";
        string useOpponentLabel = canUseOpponentWeapon
            ? $"{useOpponentBaseLabel} ({context.IterativeTag})"
            : (opponentHasLightWeapon && !context.HasIterativeGrappleAttack
                ? $"{useOpponentBaseLabel} (No attacks left)"
                : $"{useOpponentBaseLabel} (Unavailable)");
        states.Set(GrappleUseOpponentWeaponButton, new ActionButtonState(context.IsGrappling && !context.ShowOnlyPinnedEscapeActions, canUseOpponentWeapon, useOpponentLabel));

        bool canDisarmSmallObject = context.IsGrappling && context.ShowOnlyPinnerActions && context.Actions.HasStandardAction && CanUseGrappleActionWhilePinning(pc, GrappleActionType.DisarmSmallObject);
        states.Set(GrappleDisarmSmallObjectButton, new ActionButtonState(
            context.IsGrappling && context.ShowOnlyPinnerActions,
            canDisarmSmallObject,
            canDisarmSmallObject ? $"Disarm Small Object ({context.GrappleOpponentName})" : "Disarm Small Object (Used/Blocked)"));

        bool canReleasePin = context.IsGrappling && context.ShowOnlyPinnerActions && context.OpponentPinnedByActor;
        states.Set(GrappleReleasePinnedButton, new ActionButtonState(
            context.IsGrappling && context.ShowOnlyPinnerActions,
            canReleasePin,
            canReleasePin ? $"Release {context.GrappleOpponentName} from Pin" : "Release Pin (N/A)"));
    }

    private bool CanUseGrappleActionWhilePinning(CharacterController pc, GrappleActionType actionType)
    {
        return !pc.IsGrappleActionBlockedWhilePinning(actionType, out _);
    }

    private void ComputeAlwaysAvailableStates(ActionButtonStates states)
    {
        states.Set(EndTurnButton, new ActionButtonState(true, true));
    }

    private void ApplyButtonStates(ActionButtonStates states)
    {
        foreach (KeyValuePair<Button, ActionButtonState> kv in states.Enumerate())
        {
            ApplyButtonState(kv.Key, kv.Value);
        }
    }

    private void ApplyButtonState(Button button, ActionButtonState state)
    {
        if (button == null || state == null)
            return;

        button.gameObject.SetActive(state.Visible);
        button.interactable = state.Enabled;

        if (!string.IsNullOrEmpty(state.Label))
        {
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
                label.text = state.Label;
        }
    }

    private void UpdateRageStatus(CharacterController pc)
    {
        if (RageStatusText == null)
            return;

        bool isBarbarian = pc.Stats.IsBarbarian;
        RageStatusText.gameObject.SetActive(isBarbarian && (pc.Stats.IsRaging || pc.Stats.IsFatiguedOrExhausted));
        if (pc.Stats.IsRaging)
            RageStatusText.text = $"⚡ RAGING! {pc.Stats.RageRoundsRemaining} rounds left | -2 AC | +4 STR/CON | +2 Will";
        else if (pc.Stats.IsExhaustedState)
            RageStatusText.text = "🥵 EXHAUSTED: -6 STR, -6 DEX, half speed";
        else if (pc.Stats.IsFatiguedState)
            RageStatusText.text = "😫 FATIGUED: -2 STR, -2 DEX";
    }

    private void HideNonGrappleActionButtons()
    {
        if (MoveButton != null) MoveButton.gameObject.SetActive(false);
        if (WithdrawButton != null) WithdrawButton.gameObject.SetActive(false);
        if (FiveFootStepButton != null) FiveFootStepButton.gameObject.SetActive(false);
        if (DropProneButton != null) DropProneButton.gameObject.SetActive(false);
        if (StandUpButton != null) StandUpButton.gameObject.SetActive(false);
        if (CrawlButton != null) CrawlButton.gameObject.SetActive(false);
        if (AttackButton != null) AttackButton.gameObject.SetActive(false);
        if (AttackThrownButton != null) AttackThrownButton.gameObject.SetActive(false);
        if (AttackOffHandButton != null) AttackOffHandButton.gameObject.SetActive(false);
        if (AttackOffHandThrownButton != null) AttackOffHandThrownButton.gameObject.SetActive(false);
        if (AttackDefensivelyButton != null) AttackDefensivelyButton.gameObject.SetActive(false);
        if (SpecialAttackButton != null) SpecialAttackButton.gameObject.SetActive(false);
        if (SmiteButton != null) SmiteButton.gameObject.SetActive(false);
        if (GrappleActionsButton != null) GrappleActionsButton.gameObject.SetActive(false);
        if (AidAnotherButton != null) AidAnotherButton.gameObject.SetActive(false);
        if (OverrunButton != null) OverrunButton.gameObject.SetActive(false);
        if (ChargeButton != null) ChargeButton.gameObject.SetActive(false);
        if (FullAttackButton != null) FullAttackButton.gameObject.SetActive(false);
        if (FullAttackDefensivelyButton != null) FullAttackDefensivelyButton.gameObject.SetActive(false);
        if (DualWieldButton != null) DualWieldButton.gameObject.SetActive(false);
        if (FlurryOfBlowsButton != null) FlurryOfBlowsButton.gameObject.SetActive(false);
        if (RageButton != null) RageButton.gameObject.SetActive(false);
        if (ReloadButton != null) ReloadButton.gameObject.SetActive(false);
        if (DropEquippedItemButton != null) DropEquippedItemButton.gameObject.SetActive(false);
        if (PickUpItemButton != null) PickUpItemButton.gameObject.SetActive(false);
        if (DamageModeToggleButton != null) DamageModeToggleButton.gameObject.SetActive(false);
        if (DischargeTouchButton != null) DischargeTouchButton.gameObject.SetActive(false);
        if (DismissDisguiseSelfButton != null) DismissDisguiseSelfButton.gameObject.SetActive(false);
        if (DismissExpeditiousRetreatButton != null) DismissExpeditiousRetreatButton.gameObject.SetActive(false);
    }

    public void HideAllActionButtons()
    {
        if (MoveButton != null) MoveButton.gameObject.SetActive(false);
        if (WithdrawButton != null) WithdrawButton.gameObject.SetActive(false);
        if (FiveFootStepButton != null) FiveFootStepButton.gameObject.SetActive(false);
        if (DropProneButton != null) DropProneButton.gameObject.SetActive(false);
        if (StandUpButton != null) StandUpButton.gameObject.SetActive(false);
        if (CrawlButton != null) CrawlButton.gameObject.SetActive(false);
        if (AttackButton != null) AttackButton.gameObject.SetActive(false);
        if (AttackThrownButton != null) AttackThrownButton.gameObject.SetActive(false);
        if (AttackOffHandButton != null) AttackOffHandButton.gameObject.SetActive(false);
        if (AttackOffHandThrownButton != null) AttackOffHandThrownButton.gameObject.SetActive(false);
        if (FullAttackButton != null) FullAttackButton.gameObject.SetActive(false);
        if (SpecialAttackButton != null) SpecialAttackButton.gameObject.SetActive(false);
        if (TurnUndeadButton != null) TurnUndeadButton.gameObject.SetActive(false);
        if (SmiteButton != null) SmiteButton.gameObject.SetActive(false);
        if (GrappleActionsButton != null) GrappleActionsButton.gameObject.SetActive(false);
        if (GrappleDamageButton != null) GrappleDamageButton.gameObject.SetActive(false);
        if (GrappleLightWeaponAttackButton != null) GrappleLightWeaponAttackButton.gameObject.SetActive(false);
        if (GrappleUnarmedAttackButton != null) GrappleUnarmedAttackButton.gameObject.SetActive(false);
        if (GrapplePinButton != null) GrapplePinButton.gameObject.SetActive(false);
        if (GrappleBreakPinButton != null) GrappleBreakPinButton.gameObject.SetActive(false);
        if (GrappleEscapeArtistButton != null) GrappleEscapeArtistButton.gameObject.SetActive(false);
        if (GrappleEscapeCheckButton != null) GrappleEscapeCheckButton.gameObject.SetActive(false);
        if (GrappleMoveButton != null) GrappleMoveButton.gameObject.SetActive(false);
        if (GrappleUseOpponentWeaponButton != null) GrappleUseOpponentWeaponButton.gameObject.SetActive(false);
        if (GrappleDisarmSmallObjectButton != null) GrappleDisarmSmallObjectButton.gameObject.SetActive(false);
        if (GrappleReleasePinnedButton != null) GrappleReleasePinnedButton.gameObject.SetActive(false);
        if (AidAnotherButton != null) AidAnotherButton.gameObject.SetActive(false);
        if (OverrunButton != null) OverrunButton.gameObject.SetActive(false);
        if (ChargeButton != null) ChargeButton.gameObject.SetActive(false);
        if (DualWieldButton != null) DualWieldButton.gameObject.SetActive(false);
        if (EndTurnButton != null) EndTurnButton.gameObject.SetActive(false);
        if (ReloadButton != null) ReloadButton.gameObject.SetActive(false);
        if (DropEquippedItemButton != null) DropEquippedItemButton.gameObject.SetActive(false);
        if (PickUpItemButton != null) PickUpItemButton.gameObject.SetActive(false);
        if (DamageModeToggleButton != null) DamageModeToggleButton.gameObject.SetActive(false);
        if (FlurryOfBlowsButton != null) FlurryOfBlowsButton.gameObject.SetActive(false);
        if (RageButton != null) RageButton.gameObject.SetActive(false);
        if (CastSpellButton != null) CastSpellButton.gameObject.SetActive(false);
        if (DischargeTouchButton != null) DischargeTouchButton.gameObject.SetActive(false);
        if (DismissDisguiseSelfButton != null) DismissDisguiseSelfButton.gameObject.SetActive(false);
        if (DismissExpeditiousRetreatButton != null) DismissExpeditiousRetreatButton.gameObject.SetActive(false);
        if (AttackDefensivelyButton != null) AttackDefensivelyButton.gameObject.SetActive(false);
        if (FullAttackDefensivelyButton != null) FullAttackDefensivelyButton.gameObject.SetActive(false);
    }
}
