using System.Collections.Generic;
using TMPro;
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
    private Button DomainPowerButton => _combatUI != null ? _combatUI.DomainPowerButton : null;
    private Button SmiteButton => _combatUI != null ? _combatUI.SmiteButton : null;
    private Button LayOnHandsButton => _combatUI != null ? _combatUI.LayOnHandsButton : null;
    private Button WildShapeButton => _combatUI != null ? _combatUI.WildShapeButton : null;
    private Button GrappleActionsButton => _combatUI != null ? _combatUI.GrappleActionsButton : null;
    private Button GrappleDamageButton => _combatUI != null ? _combatUI.GrappleDamageButton : null;
    private Button GrappleLightWeaponAttackButton => _combatUI != null ? _combatUI.GrappleLightWeaponAttackButton : null;
    private Button GrappleUnarmedAttackButton => _combatUI != null ? _combatUI.GrappleUnarmedAttackButton : null;
    private Button GrapplePinButton => _combatUI != null ? _combatUI.GrapplePinButton : null;
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
    private Button UseItemButton => _combatUI != null ? _combatUI.UseItemButton : null;
    private Button DamageModeToggleButton => _combatUI != null ? _combatUI.DamageModeToggleButton : null;
    private Text ActionStatusText => _combatUI != null ? _combatUI.ActionStatusText : null;
    private Button FlurryOfBlowsButton => _combatUI != null ? _combatUI.FlurryOfBlowsButton : null;
    private Button RageButton => _combatUI != null ? _combatUI.RageButton : null;
    private Text RageStatusText => _combatUI != null ? _combatUI.RageStatusText : null;
    private Button BardicMusicButton => _combatUI != null ? _combatUI.BardicMusicButton : null;
    private Text BardicMusicStatusText => _combatUI != null ? _combatUI.BardicMusicStatusText : null;
    private Button CastSpellButton => _combatUI != null ? _combatUI.CastSpellButton : null;
    private Button MaintainConcentrationButton => _combatUI != null ? _combatUI.MaintainConcentrationButton : null;
    private Button UseImbuedSpellButton => _combatUI != null ? _combatUI.UseImbuedSpellButton : null;
    private Button BreakWallButton => _combatUI != null ? _combatUI.BreakWallButton : null;
    private Button ControlFlamingSphereButton => _combatUI != null ? _combatUI.ControlFlamingSphereButton : null;
    private Button DischargeTouchButton => _combatUI != null ? _combatUI.DischargeTouchButton : null;
    private Button DismissDisguiseSelfButton => _combatUI != null ? _combatUI.DismissDisguiseSelfButton : null;
    private Button DismissExpeditiousRetreatButton => _combatUI != null ? _combatUI.DismissExpeditiousRetreatButton : null;
    private Button DismissJumpButton => _combatUI != null ? _combatUI.DismissJumpButton : null;
    private Button DismissInvisibilityButton => _combatUI != null ? _combatUI.DismissInvisibilityButton : null;
    private Button DismissSeeInvisibilityButton => _combatUI != null ? _combatUI.DismissSeeInvisibilityButton : null;
    private Button AttackDefensivelyButton => _combatUI != null ? _combatUI.AttackDefensivelyButton : null;
    private Button FullAttackDefensivelyButton => _combatUI != null ? _combatUI.FullAttackDefensivelyButton : null;
    private Button TotalDefenseButton => _combatUI != null ? _combatUI.TotalDefenseButton : null;
    private Button MonsterAbilityButton => _combatUI != null ? _combatUI.MonsterAbilityButton : null;

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
        public bool IsPinningOpponent;   // holding a pin whose round is not up (pinner restrictions apply)
        public bool PinRenewalDue;       // the pinner's next turn: Pin renews the pin, anything else ends it (CMB-120)
        public bool HasGrappleAttackAvailable;
        public bool HasBullRushAttackAvailable;
        public bool HasTripAttackAvailable;
        public bool HasDisarmAttackAvailable;
        public bool HasCoupDeGraceAttackAvailable;
        public bool HasThrowableMeleeWeapon;
        public bool IterativeThrownSequenceActive;
        public bool IterativeThrownFullRoundStage;
        public bool CanStartMainAfterOffHand;
        public bool HasStandardAttack;
        public bool CanContinueIterativeAttack;
        public bool HasOffHandWeapon;
        public bool HasThrowableOffHandWeapon;
        public bool OffHandUsed;
        public bool OffHandAvailableThisTurn;
        public bool HasActiveFlamingSphere;
        public bool CanControlFlamingSphere;
        public string ControlFlamingSphereReason;
        public bool ShowOnlyPinnedEscapeActions;
        public bool ShowOnlyPinnerActions;
        public bool HasIterativeGrappleAttack;
        public int GrappleAttacksRemaining;
        public int CurrentGrappleAttackBonus;
        public string IterativeTag;
        public bool HasGrappleUnarmedAttack;
        public string GrappleUnarmedAttackTag;
        public string GrappleOpponentName;
        public bool HasAnimateRopeEscapeAction;
        public string AnimateRopeEscapeDisabledReason;

        // Wall of Ice — break wall button context
        public bool HasAdjacentIntactWall;
        public int AdjacentWallDC;
    }

    private sealed class NaturalAttackButtonOption
    {
        public int SequenceIndex;
        public string AttackName;
        public bool IsPrimary;
        /// <summary>A natural attack already used this turn, offered again as Haste's extra attack (CMB-106).</summary>
        public bool IsHasteExtra;
        /// <summary>The attack bonus of this natural attack (CharacterStats.GetNaturalAttackBonus), shown in the chooser.</summary>
        public int AttackBonus;
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
        context.HasIterativeAttacks = pc.GetIterativeAttackCount() > 1; // the one ladder, with Haste (CMB-044)
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
        context.IsPinningOpponent = pc.IsHoldingPinThisRound();
        context.PinRenewalDue = pc.IsPinRenewalDue();

        context.HasGrappleAttackAvailable = context.Gm != null && context.Gm.CanUseGrappleAttackOption(pc);
        context.HasBullRushAttackAvailable = context.Gm != null && context.Gm.CanUseBullRushAttackOption(pc);
        context.HasTripAttackAvailable = context.Gm != null && context.Gm.CanUseTripAttackOption(pc);
        context.HasDisarmAttackAvailable = context.Gm != null && context.Gm.CanUseDisarmAttackOption(pc);
        context.HasCoupDeGraceAttackAvailable = context.Gm != null && context.Gm.CanUseCoupDeGraceAttackOption(pc);

        context.HasThrowableMeleeWeapon = context.Gm != null && context.Gm.HasThrowableMeleeWeaponEquipped(pc);
        context.IterativeThrownSequenceActive = context.Gm != null && context.Gm.IsIterativeThrownAttackSequenceActiveFor(pc);
        context.IterativeThrownFullRoundStage = context.Gm != null && context.Gm.IsIterativeThrownAttackInFullRoundStage(pc);

        context.CanStartMainAfterOffHand = context.Gm != null && context.Gm.IsOffHandAttackUsedThisTurn(pc) && context.Actions.HasMoveAction && !context.IterativeWeaponSequenceActive;
        context.HasStandardAttack = context.Actions.HasStandardAction || context.CanStartMainAfterOffHand;
        context.CanContinueIterativeAttack = context.IterativeWeaponSequenceActive;

        context.HasOffHandWeapon = pc.HasOffHandWeaponEquipped();
        context.HasThrowableOffHandWeapon = pc.HasThrowableOffHandWeaponEquipped();
        context.OffHandUsed = context.Gm != null && context.Gm.IsOffHandAttackUsedThisTurn(pc);
        context.OffHandAvailableThisTurn = context.Gm == null || context.Gm.IsOffHandAttackAvailableThisTurn(pc);

        context.HasActiveFlamingSphere = context.Gm != null && context.Gm.HasActiveFlamingSphere(pc);
        context.CanControlFlamingSphere = context.Gm != null && context.Gm.CanControlFlamingSphere(pc, out context.ControlFlamingSphereReason);

        context.ShowOnlyPinnedEscapeActions = context.IsGrappling && (context.IsPinned || context.ActorPinnedInGrappleState);
        context.ShowOnlyPinnerActions = context.IsGrappling && context.IsPinningOpponent && !context.ShowOnlyPinnedEscapeActions;
        // Grapple checks and weapon grapple attacks take an iterative step (Pin stands for all of them); the grapple attack
        // of a creature fighting with its natural attacks takes a natural step instead (PHB p.156, CMB-127).
        context.HasIterativeGrappleAttack = context.Gm != null && context.Gm.CanUseGrappleAttackOption(pc, GrappleActionType.PinOpponent);
        context.GrappleAttacksRemaining = context.Gm != null ? context.Gm.GetRemainingGrappleAttackActions(pc, GrappleActionType.PinOpponent) : 0;
        context.CurrentGrappleAttackBonus = context.Gm != null ? context.Gm.GetCurrentGrappleAttackBonus(pc, GrappleActionType.PinOpponent) : 0;
        context.IterativeTag = $"BAB {CharacterStats.FormatMod(context.CurrentGrappleAttackBonus)} | {context.GrappleAttacksRemaining} left";
        context.HasGrappleUnarmedAttack = context.Gm != null && context.Gm.CanUseGrappleAttackOption(pc, GrappleActionType.AttackUnarmed);
        int grappleUnarmedRemaining = context.Gm != null ? context.Gm.GetRemainingGrappleAttackActions(pc, GrappleActionType.AttackUnarmed) : 0;
        context.GrappleUnarmedAttackTag = pc.UsesNaturalAttacksForGrappleAttack()
            ? $"{grappleUnarmedRemaining} left"
            : $"BAB {CharacterStats.FormatMod(context.Gm != null ? context.Gm.GetCurrentGrappleAttackBonus(pc, GrappleActionType.AttackUnarmed) : 0)} | {grappleUnarmedRemaining} left";
        context.GrappleOpponentName = context.GrappleOpponent != null && context.GrappleOpponent.Stats != null
            ? context.GrappleOpponent.Stats.CharacterName
            : "Opponent";

        context.AnimateRopeEscapeDisabledReason = string.Empty;
        context.HasAnimateRopeEscapeAction = context.Gm != null && context.Gm.CanUseAnimateRopeEscapeAction(pc, out context.AnimateRopeEscapeDisabledReason);

        // Wall of Ice — check if any adjacent cell has an intact wall for Break Wall button
        context.HasAdjacentIntactWall = false;
        context.AdjacentWallDC = 0;
        HashSet<Vector2Int> intactWallCells = WallOfIceAreaEffect.GetAllIntactWallOfIceCells();
        if (intactWallCells.Count > 0)
        {
            foreach (Vector2Int wc in intactWallCells)
            {
                if (SquareGridUtils.ChebyshevDistance(pc.GridPosition, wc) <= 1)
                {
                    context.HasAdjacentIntactWall = true;
                    WallOfIceAreaEffect wall = WallOfIceAreaEffect.GetWallAtCell(wc);
                    if (wall != null)
                        context.AdjacentWallDC = wall.GetStrengthCheckDC();
                    break;
                }
            }
        }

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
        bool shouldGroupByAttackTypeAtTurnStart = gm != null && !gm.Combat_HasStartedAttackingThisTurn(pc);
        HashSet<string> seenAttackTypes = shouldGroupByAttackTypeAtTurnStart
            ? new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            : null;

        // Haste's extra natural attack (PHB p.239; owner decision 2026-10-07, CMB-106): after the unused
        // natural attacks, one option per used attack type, whose button makes the Haste attack with it.
        var hasteOptions = new List<NaturalAttackButtonOption>();
        var hasteAttackTypes = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

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
                {
                    if (gm.IsHasteExtraNaturalAttackOption(pc, currentSequenceIndex) && hasteAttackTypes.Add(attackName))
                    {
                        hasteOptions.Add(new NaturalAttackButtonOption
                        {
                            SequenceIndex = currentSequenceIndex,
                            AttackName = attackName,
                            IsPrimary = attack.IsPrimary,
                            IsHasteExtra = true,
                            AttackBonus = pc.Stats.GetNaturalAttackBonus(attack)
                        });
                    }
                    continue;
                }

                if (shouldGroupByAttackTypeAtTurnStart)
                {
                    if (seenAttackTypes == null || !seenAttackTypes.Add(attackName))
                        continue;
                }

                options.Add(new NaturalAttackButtonOption
                {
                    SequenceIndex = currentSequenceIndex,
                    AttackName = attackName,
                    IsPrimary = attack.IsPrimary,
                    AttackBonus = pc.Stats.GetNaturalAttackBonus(attack)
                });
            }
        }

        options.AddRange(hasteOptions);
        return options;
    }

    /// <summary>
    /// With more natural-attack options than the two attack buttons (three or more attack types, or
    /// unused attacks plus the Haste re-offers, CMB-106), the second button opens a chooser with every
    /// option after the first, so the player can pick any of them, Haste's extra attack included.
    /// </summary>
    private static bool UsesNaturalAttackChooser(ActionButtonContext context)
        => context != null && context.NaturalAttackOptions != null && context.NaturalAttackOptions.Count > 2;

    private static void OpenNaturalAttackChooser(GameManager gm, List<NaturalAttackButtonOption> options)
    {
        var sequenceIndices = new List<int>();
        var attackNames = new List<string>();
        var labels = new List<string>();
        for (int i = 1; i < options.Count; i++)
        {
            NaturalAttackButtonOption option = options[i];
            sequenceIndices.Add(option.SequenceIndex);
            attackNames.Add(option.AttackName);
            labels.Add(BuildNaturalAttackChooserLabel(option.AttackName, option.IsPrimary, option.AttackBonus, option.IsHasteExtra));
        }

        gm.ShowNaturalAttackOptionMenu(sequenceIndices, attackNames, labels);
    }

    /// <summary>
    /// Label of one natural-attack chooser option: "Name (Primary +5)", or "Name (Haste, Secondary +0)" for Haste's
    /// extra natural attack (CMB-106). Shared by the natural-attack chooser and the Haste chooser of the Full Attack
    /// button and pounce (GameManager.PromptHasteNaturalAttackChoice, CMB-124).
    /// </summary>
    public static string BuildNaturalAttackChooserLabel(string attackName, bool isPrimary, int attackBonus, bool isHasteExtra)
    {
        string name = string.IsNullOrWhiteSpace(attackName) ? "Natural attack" : attackName.Trim();
        string role = isPrimary ? "Primary" : "Secondary";
        string bonus = CharacterStats.FormatMod(attackBonus);
        return isHasteExtra
            ? $"{name} (Haste, {role} {bonus})"
            : $"{name} ({role} {bonus})";
    }

    private static string BuildNaturalAttackButtonLabel(NaturalAttackButtonOption option)
    {
        if (option == null)
            return "Attack: Natural attack";

        string role = option.IsPrimary ? "Primary" : "Secondary";
        return option.IsHasteExtra
            ? $"Attack: {option.AttackName} (Haste, {role})"
            : $"Attack: {option.AttackName} ({role})";
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
                if (UsesNaturalAttackChooser(context))
                {
                    List<NaturalAttackButtonOption> chooserOptions = context.NaturalAttackOptions;
                    AttackThrownButton.onClick.AddListener(() => OpenNaturalAttackChooser(gm, chooserOptions));
                }
                else if (context.NaturalAttackOptions.Count > 1)
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

    /// <summary>One button's computed state, for ActionPanelParityTests (batch 13 step 6).</summary>
    internal struct ButtonStateView
    {
        public Button Button;
        public bool Visible;
        public bool Enabled;
        public string Label;
    }

    /// <summary>
    /// The states <see cref="ComputeActionButtonStates"/> gives <paramref name="pc"/>'s buttons, without applying them
    /// (ActionPanelParityTests compares them with the legal set, ActorActionSet; batch 13 step 6, C8). A button the panel
    /// does not set is absent.
    /// </summary>
    internal List<ButtonStateView> ComputeButtonStatesForParity(CharacterController pc)
    {
        var result = new List<ButtonStateView>();
        if (_combatUI == null || pc == null || pc.Stats == null)
            return result;
        ActionButtonStates states = ComputeActionButtonStates(pc, BuildActionButtonContext(pc));
        foreach (KeyValuePair<Button, ActionButtonState> kv in states.Enumerate())
            result.Add(new ButtonStateView { Button = kv.Key, Visible = kv.Value.Visible, Enabled = kv.Value.Enabled, Label = kv.Value.Label });
        return result;
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
        ComputeBreakWallState(pc, context, states);
        ComputeUseImbuedSpellState(pc, context, states);
        ComputeAlwaysAvailableStates(states);

        return states;
    }

    private void ComputeMovementActionStates(CharacterController pc, ActionButtonContext context, ActionButtonStates states)
    {
        bool canMoveByActions = context.Actions.HasMoveAction || context.Actions.CanConvertStandardToMove;
        bool blockedByFiveFootStep = pc.HasTakenFiveFootStep;
        bool blockedByIterativeAttackSequence = context.IterativeWeaponFullRoundStage || (context.Gm != null && context.Gm.IsIterativeThrownAttackInFullRoundStage(pc));
        // The shared movement legality (MovementService.CanStartMovement, batch 12 step 22), as the AI's moves ask it.
        Legality moveLegality = MovementService.CanStartMovement(pc, MoveKind.Move);
        bool canMove = moveLegality.Ok && !blockedByIterativeAttackSequence;

        string moveLabel = "Move (Used)";
        if (context.IsPinned) moveLabel = "Move (Pinned: grapple escape only)";
        else if (context.IsProne) moveLabel = "Move (Stand up first)";
        else if (blockedByFiveFootStep) moveLabel = "Move (After 5-ft step: no)";
        else if (blockedByIterativeAttackSequence) moveLabel = "Move (Consumed by Full Round Attack)";
        else if (!canMoveByActions) moveLabel = "Move (Used)";
        else if (!moveLegality.Ok) moveLabel = $"Move ({moveLegality.Reason})";
        else if (context.Actions.HasMoveAction) moveLabel = "Move (Move Action)";
        else if (context.Actions.CanConvertStandardToMove) moveLabel = "Move (Std→Move)";

        states.Set(MoveButton, new ActionButtonState(true, canMove, moveLabel));

        string withdrawDisabledReason = context.Gm != null ? context.Gm.GetWithdrawDisabledReason(pc) : "Unavailable";
        bool canWithdraw = string.IsNullOrEmpty(withdrawDisabledReason);
        bool restrictedWithdraw = MovementService.WithdrawKindFor(pc) == MoveKind.RestrictedWithdraw;
        string withdrawLabel = canWithdraw
            ? (restrictedWithdraw ? "Withdraw (Standard, 1x move: restricted)" : "Withdraw (Full-Round, 2x move)")
            : $"Withdraw ({withdrawDisabledReason})";
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
                ? (context.IterativeWeaponFullRoundStage || pc.ProgressiveAttackPool.IsFullAttack || pc.ProgressiveAttackPool.NextAttackNeedsMoveAction
                    ? "Attack (Melee - Full Round)"
                    : "Attack (Melee)")
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
        else if (context.UsingInnateNaturalAttacks && UsesNaturalAttackChooser(context))
            thrownLabel = $"Attack: Other natural attack ({context.NaturalAttackOptions.Count - 1} choices)";
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
        // Any creature that attacks may fight defensively: no base attack bonus is needed (PHB p.140, p.143; CMB-203).
        bool canAttackDefensively = context.Actions.HasStandardAction && context.CanAttackWithWeapon && !context.IsPinned && !context.IsTurned;
        string attackDefensivelyLabel;
        if (context.IsPinned) attackDefensivelyLabel = "Fighting Defensively (Pinned: no)";
        else if (context.IsTurned) attackDefensivelyLabel = "Fighting Defensively (Turned: no)";
        else if (!context.Actions.HasStandardAction) attackDefensivelyLabel = "Fighting Defensively (Std) [Used]";
        else if (!context.CanAttackWithWeapon) attackDefensivelyLabel = "Fighting Defensively (Std) [Reload first]";
        else attackDefensivelyLabel = context.UsingUnarmedStrike ? $"Fighting Defensively (Std, {context.AttackSourceLabel})" : "Fighting Defensively (Std)";
        states.Set(AttackDefensivelyButton, new ActionButtonState(true, canAttackDefensively, attackDefensivelyLabel));

        // Total defense (PHB p.142; CMB-179): the catalog executor's CanStart is the button's state, the rule the AI's
        // runner checks too, its turned and other condition refusals included.
        ActionDefinition totalDefense = ActionCatalog.Get(ActionIds.TotalDefense);
        Legality totalDefenseStart = totalDefense != null && totalDefense.Executor != null
            ? totalDefense.Executor.CanStart(pc)
            : Legality.Refused(LegalityBar.Other, "no executor");
        bool canTotalDefense = totalDefenseStart.Ok;
        string totalDefenseLabel;
        if (pc.IsInTotalDefense) totalDefenseLabel = $"Total Defense (active: +{pc.Stats.TotalDefenseACBonus} AC)";
        else if (context.IsTurned) totalDefenseLabel = "Total Defense (Turned: no)";
        else if (canTotalDefense) totalDefenseLabel = $"Total Defense (Std, +{TotalDefenseRules.DodgeBonus(pc.Stats)} AC)";
        else if (totalDefenseStart.Bar == LegalityBar.Action && !context.Actions.HasStandardAction) totalDefenseLabel = "Total Defense (Std) [Used]";
        else totalDefenseLabel = "Total Defense (Std) [Unavailable]";
        states.Set(TotalDefenseButton, new ActionButtonState(true, canTotalDefense, totalDefenseLabel));

        // A full-round action, or a partial charge (a standard action) for a creature limited to one action (PHB p.154, D2; CMB-108).
        bool hasChargeAction = ChargeRules.HasChargeAction(pc, out bool partialCharge, out _);
        bool hasMeleeThreat = pc.HasMeleeWeaponEquipped();
        bool fatigueRestricted = pc.Stats != null && pc.Stats.IsFatiguedOrExhausted;
        bool entangledRestricted = pc.Stats != null && pc.Stats.IsEntangledState;
        bool isExhausted = pc.Stats != null && pc.Stats.IsExhaustedState;
        bool blockedByFiveFootStep = pc.HasTakenFiveFootStep;
        bool blockedByProne = context.IsProne;
        bool hasAnyChargeTarget = context.Gm != null && context.Gm.HasAnyValidChargeTarget(pc);
        bool canChargeTarget = hasChargeAction && hasMeleeThreat && !fatigueRestricted && !entangledRestricted && !blockedByFiveFootStep && !blockedByProne && !context.IsPinned && !context.IsTurned && hasAnyChargeTarget;

        string chargeLabel;
        if (context.IsPinned) chargeLabel = "Charge (Pinned: no)";
        else if (context.IsTurned) chargeLabel = "Charge (Turned: no)";
        else if (!hasMeleeThreat) chargeLabel = "Charge (Need melee)";
        else if (fatigueRestricted) chargeLabel = isExhausted ? "Charge (Exhausted)" : "Charge (Fatigued)";
        else if (entangledRestricted) chargeLabel = "Charge (Entangled)";
        else if (blockedByProne) chargeLabel = "Charge (Prone)";
        else if (blockedByFiveFootStep) chargeLabel = "Charge (After 5-ft step: no)";
        else if (!hasChargeAction) chargeLabel = context.Actions.MoveActionOnly ? "Charge (Move action only: no)" : "Charge (Used)";
        else if (!hasAnyChargeTarget) chargeLabel = "Charge (No valid target)";
        else chargeLabel = partialCharge ? "Charge (Partial, Std)" : "Charge (Full-Round)";
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
        bool canEverUseTurnUndead = pc != null && pc.Stats != null && pc.Stats.CanEverTurnUndead;
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

        // Domain Power button: visible for clerics with activatable domain powers
        bool hasDomainPowers = pc != null && pc.Stats != null && pc.Stats.IsCleric
            && pc.Stats.ChosenDomains != null && pc.Stats.ChosenDomains.Exists(d =>
                d == "Strength" || d == "Destruction" || d == "Death" || d == "Sun" || d == "Travel" || d == "Plant"
                || d == "Air" || d == "Earth" || d == "Fire" || d == "Water" || d == "Luck");
        bool canUseDomainPower = hasDomainPowers && context.Gm != null
            && context.Gm.GetAvailableDomainPowers(pc).Count > 0;
        // Show armed state for Luck reroll toggle
        bool luckArmed = pc != null && pc.Stats != null && pc.Stats.LuckRerollPending;
        string domainPowerLabel;
        if (!hasDomainPowers)
            domainPowerLabel = "Domain Power (N/A)";
        else if (luckArmed)
            domainPowerLabel = "🍀 Luck Reroll (Armed)";
        else if (canUseDomainPower)
            domainPowerLabel = "Domain Power";
        else
            domainPowerLabel = "Domain Power (Used)";
        // When Luck reroll is armed, allow clicking to disarm (toggle off)
        bool domainPowerInteractable = canUseDomainPower || luckArmed;
        states.Set(DomainPowerButton, new ActionButtonState(hasDomainPowers, domainPowerInteractable, domainPowerLabel));

        // Smite (paladin Smite Evil, PHB p.44; celestial or fiendish template smite, MM p.31, p.107): shown only to a
        // creature that has one; it declares the smite for the next melee attack, or withdraws a declaration (CHR-020).
        bool hasSmite = pc != null && pc.Stats != null && SmiteRules.HasAnySmite(pc.Stats);
        string smiteReason = string.Empty;
        bool canUseSmite = hasSmite && context.Gm != null && context.Gm.CanUseSmiteButton(pc, out smiteReason);
        string smiteLabel = string.Empty;
        if (hasSmite)
        {
            string smiteAxis = SmiteRules.AxisLabel(pc.Stats);
            string uses = $"{SmiteRules.UsesLeft(pc.Stats)}/{SmiteRules.UsesPerDay(pc.Stats)}";
            if (pc.Stats.DeclaredSmite != SmiteSource.None)
                smiteLabel = $"Smite {smiteAxis} declared (press to withdraw)";
            else if (canUseSmite)
                smiteLabel = $"Smite {smiteAxis} (next melee attack, {uses} left)";
            else
                smiteLabel = $"Smite {smiteAxis} ({smiteReason}; {uses})";
        }
        states.Set(SmiteButton, new ActionButtonState(hasSmite, canUseSmite, smiteLabel));

        // Lay on Hands (PHB p.44): shown from paladin 2nd level; a standard action on a target within touch (CHR-020).
        bool hasLayOnHands = pc != null && pc.Stats != null && LayOnHandsRules.HasAbility(pc.Stats);
        string layOnHandsReason = string.Empty;
        bool canLayOnHands = hasLayOnHands && context.Gm != null && context.Gm.CanUseLayOnHandsButton(pc, out layOnHandsReason);
        string layOnHandsLabel = string.Empty;
        if (hasLayOnHands)
        {
            string pool = $"{LayOnHandsRules.RemainingPool(pc.Stats)}/{LayOnHandsRules.MaxPool(pc.Stats)}";
            layOnHandsLabel = canLayOnHands
                ? $"Lay on Hands (Standard, {pool} hp)"
                : $"Lay on Hands ({layOnHandsReason}; {pool})";
        }
        states.Set(LayOnHandsButton, new ActionButtonState(hasLayOnHands, canLayOnHands, layOnHandsLabel));

        // Wild Shape (PHB p.37): shown from druid 5th level; a standard action into an animal form, or back (CHR-020).
        bool hasWildShape = pc != null && pc.Stats != null && WildShapeRules.HasAbility(pc.Stats);
        string wildShapeReason = string.Empty;
        bool canWildShape = hasWildShape && context.Gm != null && context.Gm.CanUseWildShapeButton(pc, out wildShapeReason);
        string wildShapeLabel = string.Empty;
        if (hasWildShape)
        {
            string uses = $"{WildShapeRules.UsesLeft(pc.Stats)}/{WildShapeRules.UsesPerDay(pc.Stats)}";
            if (WildShapeRules.IsWildShaped(pc.Stats))
                wildShapeLabel = canWildShape ? "Change Back (Standard)" : $"Change Back ({wildShapeReason})";
            else
                wildShapeLabel = canWildShape ? $"Wild Shape (Standard, {uses} left)" : $"Wild Shape ({wildShapeReason}; {uses})";
        }
        states.Set(WildShapeButton, new ActionButtonState(hasWildShape, canWildShape, wildShapeLabel));

        bool standaloneDisarmWasVisible = _combatUI.HideStandaloneDisarmButtonsInMainActionsForActionPanel();
        Debug.Log($"[CombatUI][Actions] actor={pc.Stats.CharacterName} specialAttackVisible=true specialAttackInteractable={canSpecialAttack} grappleAvailable={context.HasGrappleAttackAvailable} bullRushAvailable={context.HasBullRushAttackAvailable} tripAvailable={context.HasTripAttackAvailable} disarmViaSpecialAvailable={context.HasDisarmAttackAvailable} coupDeGraceAvailable={context.HasCoupDeGraceAttackAvailable} standaloneDisarmSuppressed={standaloneDisarmWasVisible}");

        bool canUseLegacyGrappleButton = context.IsGrappling && context.Actions.HasStandardAction;
        states.Set(GrappleActionsButton, new ActionButtonState(context.IsGrappling, canUseLegacyGrappleButton, canUseLegacyGrappleButton ? "Grapple Actions" : "Grapple Actions (Used)"));

        // Aid Another and Overrun are surfaced via the Special Attack submenu.
        states.Set(AidAnotherButton, new ActionButtonState(false, false));
        states.Set(OverrunButton, new ActionButtonState(false, false));
        Debug.Log($"[CombatUI][Actions] Hiding main-menu Overrun button for {pc.Stats.CharacterName}; use Special Attack submenu instead.");
    }

    private void ComputeMonsterAbilityState(CharacterController pc, ActionButtonStates states)
    {
        List<ActionDefinition> entries = GameManager.MonsterAbilityEntries(pc);
        if (entries.Count == 0)
        {
            states.Set(MonsterAbilityButton, new ActionButtonState(false, false, "Monster Abilities"));
            return;
        }
        var usable = new List<string>();
        foreach (ActionDefinition d in entries)
        {
            Legality start = d.Executor.CanStart(pc);
            if (start.Ok)
                usable.Add(d.Name);
        }
        string label;
        if (usable.Count == 1)
            label = usable[0];
        else if (usable.Count > 1)
            label = "Monster Abilities (" + usable.Count + ")";
        else if (pc.HasBreathWeapon && !pc.IsBreathWeaponReady && pc.BreathWeaponCooldownRemaining > 0)
            label = "Breath Weapon (recharging: " + pc.BreathWeaponCooldownRemaining + " rd" + (pc.BreathWeaponCooldownRemaining == 1 ? "" : "s") + ")";
        else
            label = "Monster Abilities (none now)";
        states.Set(MonsterAbilityButton, new ActionButtonState(true, usable.Count > 0, label));
    }

    /// <summary>A short button-label form of a CharacterController.CanUseFlurryOfBlows refusal.</summary>
    private static string ShortenFlurryReason(string reason)
    {
        if (string.IsNullOrEmpty(reason))
            return "N/A";
        if (reason.IndexOf("special monk weapons", System.StringComparison.Ordinal) >= 0)
            return "not a monk weapon";
        if (reason.IndexOf("grappl", System.StringComparison.Ordinal) >= 0)
            return "grappling";
        if (reason.IndexOf("before the first attack", System.StringComparison.Ordinal) >= 0)
            return "already attacked";
        if (reason.IndexOf("full-round action", System.StringComparison.Ordinal) >= 0)
            return "no full-round action";
        int cite = reason.IndexOf(" (PHB", System.StringComparison.Ordinal);
        return cite > 0 ? reason.Substring(0, cite) : reason;
    }

    private void ComputeClassAndSpellActionStates(CharacterController pc, ActionButtonContext context, ActionButtonStates states)
    {
        bool isMonk = pc.Stats.IsMonk;
        // The shared flurry rules (CharacterController.CanUseFlurryOfBlows: armor, shield, load, weapon, full-round
        // action; PHB p.40; CHR-003), the same check the AI makes.
        string flurryReason = string.Empty;
        bool flurryAllowed = isMonk && pc.CanUseFlurryOfBlows(out flurryReason);
        bool canFlurry = flurryAllowed && context.Actions.HasFullRoundAction && !context.IsPinned && !context.IsTurned;
        string flurryLabel = "Flurry of Blows (N/A)";
        if (context.IsPinned)
            flurryLabel = "Flurry of Blows (Pinned: no)";
        else if (context.IsTurned)
            flurryLabel = "Flurry of Blows (Turned: no)";
        else if (isMonk && pc.IsUsingFlurryOfBlows)
            flurryLabel = "Flurry of Blows (declared: Attack / Special Attack)";
        else if (isMonk && !flurryAllowed && pc.Stats.GetMonkFetterReason() != null)
            flurryLabel = $"Flurry of Blows (no: {pc.Stats.GetMonkFetterReason()})";
        else if (isMonk && !flurryAllowed && !string.IsNullOrEmpty(flurryReason))
            flurryLabel = $"Flurry of Blows (no: {ShortenFlurryReason(flurryReason)})";
        else if (canFlurry)
        {
            int[] bonuses = pc.GetFlurryOfBlowsAttackBonusPreview();
            string bonusStr = string.Join("/", System.Array.ConvertAll(bonuses, b => CharacterStats.FormatMod(b)));
            flurryLabel = $"Flurry of Blows x{bonuses.Length} ({bonusStr})";
        }
        states.Set(FlurryOfBlowsButton, new ActionButtonState(isMonk, canFlurry, flurryLabel));

        bool isBarbarian = pc.Stats.IsBarbarian;
        // The shared rage legality (RageActionExecutor.RefusalOf, D10): once per encounter, fatigue no bar (PHB p.25).
        bool canRage = isBarbarian && RageActionExecutor.RefusalOf(pc, out _) == null;
        string rageLabel;
        if (pc.Stats.IsRaging) rageLabel = $"RAGING ({pc.Stats.RageRoundsRemaining} rds)";
        else if (pc.Stats.RagedThisEncounter) rageLabel = "Rage (Raged this encounter)";
        else if (canRage) rageLabel = $"Rage ({pc.Stats.MaxRagesPerDay - pc.Stats.RagesUsedToday}/day)";
        else rageLabel = "Rage (Used)";
        states.Set(RageButton, new ActionButtonState(isBarbarian, canRage, rageLabel));

        // ── Monster abilities (batch 13 step 18, D11): breath weapon, spittle, engulf and ranged specials, for any
        // controllable creature with the data (ActionCatalog.AvailableTo); enabled when one of them can start now (its
        // executor's CanStart, the gate the AI's runner checks too) ──
        ComputeMonsterAbilityState(pc, states);

        // ── Bardic Music (PHB p.29-30): shown to a bard with the Perform ranks; the list it opens starts a song, keeps one
        // up, makes a suggestion or stops it (BardicMusicRules, CHR-026) ──
        bool isBard = pc != null && pc.Stats != null && BardicMusicRules.HasBardicMusic(pc.Stats);
        string bardicReason = string.Empty;
        bool canPerformBardicMusic = isBard && !context.IsTurned && context.Gm != null && context.Gm.CanUseBardicMusicButton(pc, out bardicReason);
        string bardicMusicLabel = "Bardic Music";
        if (isBard)
        {
            string uses = $"{BardicMusicRules.UsesLeft(pc.Stats)}/{BardicMusicRules.UsesPerDay(pc.Stats)}";
            BardicAbility active = BardicMusicRules.ActiveAbility(pc.Stats);
            if (active != BardicAbility.None)
            {
                bool owesConcentration = BardicMusicRules.CanConcentrate(pc, out _);
                bardicMusicLabel = $"♪ {BardicAbilityInfo.GetDisplayName(active)} ({uses})" + (owesConcentration ? "\n[Concentrate or it ends]" : "\n[Options]");
            }
            else
                bardicMusicLabel = canPerformBardicMusic ? $"Bardic Music ({uses})" : $"Bardic Music ({bardicReason}; {uses})";
        }
        states.Set(BardicMusicButton, new ActionButtonState(isBard, canPerformBardicMusic, bardicMusicLabel));

        bool isSpellcaster = pc.Stats.IsSpellcaster;
        SpellcastingComponent spellComp = pc.Spellcasting;
        bool hasCastableSpells = isSpellcaster && spellComp != null && spellComp.HasAnySpellWithSlotLeft();
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
        // A cast ends the spell the caster concentrates on (PHB p.176; decision D1, batch 11 step 16).
        ConcentrationManager concentration = pc.Concentration;
        bool concentrating = concentration != null && concentration.IsConcentrating;
        if (canCast && concentrating)
            castSpellLabel += $"\n⚠ Ends {concentration.ConcentrationSpellName}";

        states.Set(CastSpellButton, new ActionButtonState(hasCastableSpells, canCast, castSpellLabel));

        // ── Maintain a concentration spell (PHB p.176): the standard action owed on each turn after the casting; a turn
        // that ends without it ends the spell (ConcentrationRules, decision D2, SPL-153) ──
        string maintainReason = null;
        bool canMaintain = concentrating && !context.IsTurned && ConcentrationRules.CanMaintain(pc, out maintainReason);
        string maintainLabel = "Maintain Spell";
        if (concentrating)
        {
            string spellName = concentration.ConcentrationSpellName;
            if (concentration.MaintainedThisTurn)
                maintainLabel = $"Maintain {spellName} (kept this turn)";
            else if (canMaintain)
                maintainLabel = $"Maintain {spellName} (Standard)\n[or it ends at end of turn]";
            else
                maintainLabel = $"Maintain {spellName} ({maintainReason}; it ends at end of turn)";
        }
        states.Set(MaintainConcentrationButton, new ActionButtonState(concentrating, canMaintain, maintainLabel));

        bool hasActiveFlamingSphere = context.HasActiveFlamingSphere;
        bool canControlFlamingSphere = hasActiveFlamingSphere && context.CanControlFlamingSphere && !context.IsTurned;
        string controlFlamingSphereLabel;
        if (!hasActiveFlamingSphere)
            controlFlamingSphereLabel = "Control Flaming Sphere";
        else if (context.IsTurned)
            controlFlamingSphereLabel = "Control Flaming Sphere (Turned: no)";
        else if (canControlFlamingSphere)
            controlFlamingSphereLabel = "Control Flaming Sphere (Move)";
        else
            controlFlamingSphereLabel = string.IsNullOrWhiteSpace(context.ControlFlamingSphereReason)
                ? "Control Flaming Sphere (Unavailable)"
                : $"Control Flaming Sphere ({context.ControlFlamingSphereReason})";
        states.Set(ControlFlamingSphereButton, new ActionButtonState(hasActiveFlamingSphere, canControlFlamingSphere, controlFlamingSphereLabel));

        _combatUI.EnsureDischargeTouchButtonExistsForActionPanel();
        SpellcastingComponent touchSpellComp = pc.Spellcasting;
        bool hasHeldTouchCharge = pc.Stats.IsSpellcaster && touchSpellComp != null && touchSpellComp.HasHeldTouchCharge && touchSpellComp.HeldTouchSpell != null;
        string heldName = hasHeldTouchCharge ? touchSpellComp.HeldTouchSpell.Name : "Touch";
        // Shown while a charge is held; enabled while it can be delivered now (batch 12 step 18, SPL-156): the casting round's
        // own touch, an attack left for a foe, or a standard action for a friend (HeldChargeDeliveryRules.CanDeliverNow).
        bool canDischarge = hasHeldTouchCharge && HeldChargeDeliveryRules.CanDeliverNow(pc);
        states.Set(DischargeTouchButton, new ActionButtonState(hasHeldTouchCharge, canDischarge, $"Discharge {heldName}"));

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

        bool hasActiveJump = context.Gm != null && context.Gm.HasActiveJump(pc);
        bool canDismissJump = hasActiveJump && context.Actions.HasStandardAction && !context.IsTurned;
        string dismissJumpLabel = hasActiveJump
            ? (canDismissJump ? "Dismiss Jump (Standard)" : "Dismiss Jump (Used)")
            : "Dismiss Jump";
        states.Set(DismissJumpButton, new ActionButtonState(hasActiveJump, canDismissJump, dismissJumpLabel));

        bool hasActiveInvisibility = context.Gm != null && context.Gm.HasActiveInvisibility(pc);
        bool canDismissInvisibility = hasActiveInvisibility && context.Actions.HasStandardAction && !context.IsTurned;
        string dismissInvisibilityLabel = hasActiveInvisibility
            ? (canDismissInvisibility ? "Dismiss Invisibility (Standard)" : "Dismiss Invisibility (Used)")
            : "Dismiss Invisibility";
        states.Set(DismissInvisibilityButton, new ActionButtonState(hasActiveInvisibility, canDismissInvisibility, dismissInvisibilityLabel));

        bool hasActiveSeeInvisibility = context.Gm != null && context.Gm.HasActiveSeeInvisibility(pc);
        bool canDismissSeeInvisibility = hasActiveSeeInvisibility && !context.IsTurned;
        string dismissSeeInvisibilityLabel = hasActiveSeeInvisibility
            ? "Dismiss See Invisible (Free)"
            : "Dismiss See Invisible";
        states.Set(DismissSeeInvisibilityButton, new ActionButtonState(hasActiveSeeInvisibility, canDismissSeeInvisibility, dismissSeeInvisibilityLabel));
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

        // Use Item button: visible if character has consumable items, enabled if full-round action available
        // The use-item entry's own gate (UseItemActionExecutor.CanStart, GameManager.GetUseItemDisabledReason; batch 13
        // step 14, D7): the button and the AI read one rule. Each item's cost (retrieve, then its use) is the executor's.
        int useableItemCount = QuickItemUsePanel.GetUseableItemCount(pc);
        bool hasUseableItems = useableItemCount > 0;
        string useItemReason = context.Gm != null ? context.Gm.GetUseItemDisabledReason(pc) : "No game";
        bool canUseItem = hasUseableItems && useItemReason == null;
        string useItemLabel = !hasUseableItems
            ? "Use Item (None)"
            : (canUseItem ? $"Use Item ({useableItemCount})" : $"Use Item ({useItemReason.TrimEnd('.')})");
        states.Set(UseItemButton, new ActionButtonState(hasUseableItems, canUseItem, useItemLabel));
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

        // A holder with one body part after Improved Grab (MM p.310) is not grappling: its one grapple action is the Damage
        // check against the creature it holds, at -20, from the round after the grab (CMB-163).
        if (pc.TryGetImprovedGrabHold(out CharacterController heldCreature, out _))
        {
            string heldName = heldCreature.Stats != null ? heldCreature.Stats.CharacterName : "held creature";
            bool canHoldDamage = context.Gm != null && context.Gm.CanUseGrappleAttackOption(pc, GrappleActionType.DamageOpponent);
            string holdTag = canHoldDamage
                ? $"BAB {CharacterStats.FormatMod(context.Gm.GetCurrentGrappleAttackBonus(pc, GrappleActionType.DamageOpponent))} | {context.Gm.GetRemainingGrappleAttackActions(pc, GrappleActionType.DamageOpponent)} left, -20"
                : (pc.IsGrappleActionAvailableToImprovedGrabHolder(GrappleActionType.DamageOpponent, out _) ? "No attacks left" : "from next round");
            states.Set(GrappleDamageButton, new ActionButtonState(true, canHoldDamage, $"Grapple: Damage {heldName} ({holdTag})"));
        }

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
            && context.HasGrappleUnarmedAttack
            && canUnarmedByRule
            && CanUseGrappleActionWhilePinning(pc, GrappleActionType.AttackUnarmed);
        // The rake takes no step of the BAB ladder, so this button stays open for it (its natural-attack menu lists Rake;
        // MM p.314, CMB-144); the one rake rule is CanRakeInGrapple.
        bool canRake = context.IsGrappling && pc.UsesNaturalAttacksForGrappleAttack() && pc.CanRakeInGrapple(out _);
        // A creature fighting with its natural attacks makes one natural attack per grapple attack (PHB p.156, CMB-127).
        string grappleAttackName = pc.UsesNaturalAttacksForGrappleAttack() ? "Grapple: Natural Attack" : "Grapple: Attack Unarmed";
        string grappleUnarmedLabel = canUnarmedAttack
            ? $"{grappleAttackName} (-4, {context.GrappleUnarmedAttackTag}{(canRake ? ", or Rake" : string.Empty)})"
            : (canRake
                ? $"{grappleAttackName} (Rake only)"
                : (canUnarmedByRule && !context.HasGrappleUnarmedAttack
                    ? $"{grappleAttackName} (-4, No attacks left)"
                    : $"{grappleAttackName} (-4, N/A)"));
        states.Set(GrappleUnarmedAttackButton, new ActionButtonState(context.IsGrappling && !context.ShowOnlyPinnedEscapeActions && !context.ShowOnlyPinnerActions, canUnarmedAttack || canRake, grappleUnarmedLabel));

        // A creature held with one body part cannot pin its holder, which is not grappled (MM p.310; CMB-163).
        bool pinBlockedByHold = pc.IsGrappleActionBlockedByImprovedGrabHold(GrappleActionType.PinOpponent, out _);
        bool canPin = context.IsGrappling && !context.ShowOnlyPinnedEscapeActions && context.HasIterativeGrappleAttack && CanUseGrappleActionWhilePinning(pc, GrappleActionType.PinOpponent)
            && !pinBlockedByHold;
        states.Set(GrapplePinButton, new ActionButtonState(
            context.IsGrappling && !context.ShowOnlyPinnedEscapeActions && !context.ShowOnlyPinnerActions,
            canPin,
            canPin
                ? (context.PinRenewalDue ? $"Grapple: Pin Again ({context.IterativeTag})" : $"Grapple: Pin Opponent ({context.IterativeTag})")
                : (pinBlockedByHold ? "Grapple: Pin (holder not grappled)" : "Grapple: Pin Opponent (No attacks left)")));
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
            string sourceEscapeLabel = context.Gm != null ? context.Gm.GetEntangleEscapeActionLabel(pc) : "Entangle: Escape";
            escapeArtistLabel = context.HasAnimateRopeEscapeAction
                ? $"{sourceEscapeLabel} (Std)"
                : $"{sourceEscapeLabel} ({context.AnimateRopeEscapeDisabledReason})";
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

        bool moveBlockedByHold = pc.IsGrappleActionBlockedByImprovedGrabHold(GrappleActionType.MoveHalfSpeed, out _);
        bool canGrappleMove = context.IsGrappling && !context.ShowOnlyPinnedEscapeActions && context.Actions.HasStandardAction && CanUseGrappleActionWhilePinning(pc, GrappleActionType.MoveHalfSpeed)
            && !moveBlockedByHold;
        string grappleMoveLabel = canGrappleMove
            ? (context.ShowOnlyPinnerActions ? $"Move (dragging {context.GrappleOpponentName})" : "Grapple: Move (Std)")
            : (moveBlockedByHold ? "Grapple: Move (holder not grappled)" : "Grapple: Move (Used/Blocked)");
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

        // A creature holding with one body part after Improved Grab is not grappling; this button lets it go (MM p.310, CMB-163).
        if (pc.TryGetImprovedGrabHold(out CharacterController heldFoe, out _))
        {
            string heldName = heldFoe.Stats != null ? heldFoe.Stats.CharacterName : "held creature";
            states.Set(GrappleReleasePinnedButton, new ActionButtonState(true, true, $"Let go of {heldName} (free)"));
            return;
        }

        bool releaseVisible = context.IsGrappling && (context.ShowOnlyPinnerActions || (context.PinRenewalDue && !context.ShowOnlyPinnedEscapeActions));
        bool canReleasePin = releaseVisible && context.OpponentPinnedByActor;
        states.Set(GrappleReleasePinnedButton, new ActionButtonState(
            releaseVisible,
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
            Text legacyLabel = button.GetComponentInChildren<Text>();
            if (legacyLabel != null)
                legacyLabel.text = state.Label;

            TMP_Text tmpLabel = button.GetComponentInChildren<TMP_Text>();
            if (tmpLabel != null)
            {
                EnsureTMPFontAsset(tmpLabel);
                tmpLabel.text = state.Label;
            }
        }
    }

    private static TMP_FontAsset _cachedTMPFontAsset;

    private static void EnsureTMPFontAsset(TMP_Text text)
    {
        if (text == null || text.font != null)
            return;

        TMP_FontAsset fontAsset = ResolveTMPFontAsset();
        if (fontAsset != null)
            text.font = fontAsset;
    }

    private static TMP_FontAsset ResolveTMPFontAsset()
    {
        if (_cachedTMPFontAsset != null)
            return _cachedTMPFontAsset;

        _cachedTMPFontAsset = TMP_Settings.defaultFontAsset;
        if (_cachedTMPFontAsset != null)
            return _cachedTMPFontAsset;

        _cachedTMPFontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (_cachedTMPFontAsset != null)
            return _cachedTMPFontAsset;

        Font fallbackFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (fallbackFont == null)
            fallbackFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (fallbackFont == null)
            fallbackFont = Font.CreateDynamicFontFromOSFont("Arial", 16);

        if (fallbackFont != null)
            _cachedTMPFontAsset = TMP_FontAsset.CreateFontAsset(fallbackFont);

        return _cachedTMPFontAsset;
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

    private void ComputeBreakWallState(CharacterController pc, ActionButtonContext context, ActionButtonStates states)
    {
        if (!context.HasAdjacentIntactWall)
        {
            states.Set(BreakWallButton, new ActionButtonState(false, false));
            return;
        }

        bool canBreak = context.Actions.HasStandardAction && !context.IsTurned && !context.IsPinned;
        string label;
        if (canBreak)
            label = $"Break Wall (STR DC {context.AdjacentWallDC})";
        else if (context.IsPinned)
            label = "Break Wall (Pinned)";
        else if (context.IsTurned)
            label = "Break Wall (Turned)";
        else
            label = "Break Wall (No Std Action)";

        states.Set(BreakWallButton, new ActionButtonState(true, canBreak, label));
    }

    private void ComputeUseImbuedSpellState(CharacterController pc, ActionButtonContext context, ActionButtonStates states)
    {
        bool hasImbuedSpells = ImbueWithSpellAbilityManager.HasImbuedSpells(pc);
        if (!hasImbuedSpells)
        {
            states.Set(UseImbuedSpellButton, new ActionButtonState(false, false));
            return;
        }

        int count = pc.Stats.ImbuedSpells != null ? pc.Stats.ImbuedSpells.Count : 0;
        bool canCast = context.Actions.HasStandardAction && !context.IsTurned && !context.IsPinned;
        string label;
        if (canCast)
        {
            label = $"Cast Imbued Spell ({count})";
            // Casting an imbued spell is casting a spell, so it ends the one the PC concentrates on (decision D1, PHB p.176).
            if (pc.Concentration != null && pc.Concentration.IsConcentrating)
                label += $"\n⚠ Ends {pc.Concentration.ConcentrationSpellName}";
        }
        else if (context.IsPinned)
            label = "Imbued Spell (Pinned)";
        else if (context.IsTurned)
            label = "Imbued Spell (Turned)";
        else
            label = "Imbued Spell (No Std)";

        states.Set(UseImbuedSpellButton, new ActionButtonState(true, canCast, label));
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
        // A grappling creature takes only the grapple actions (PHB p.156): no total defense.
        if (TotalDefenseButton != null) TotalDefenseButton.gameObject.SetActive(false);
        if (MonsterAbilityButton != null) MonsterAbilityButton.gameObject.SetActive(false);
        if (SpecialAttackButton != null) SpecialAttackButton.gameObject.SetActive(false);
        if (SmiteButton != null) SmiteButton.gameObject.SetActive(false);
        if (LayOnHandsButton != null) LayOnHandsButton.gameObject.SetActive(false);
        if (WildShapeButton != null) WildShapeButton.gameObject.SetActive(false);
        if (GrappleActionsButton != null) GrappleActionsButton.gameObject.SetActive(false);
        if (AidAnotherButton != null) AidAnotherButton.gameObject.SetActive(false);
        if (OverrunButton != null) OverrunButton.gameObject.SetActive(false);
        if (ChargeButton != null) ChargeButton.gameObject.SetActive(false);
        if (FullAttackButton != null) FullAttackButton.gameObject.SetActive(false);
        if (FullAttackDefensivelyButton != null) FullAttackDefensivelyButton.gameObject.SetActive(false);
        if (DualWieldButton != null) DualWieldButton.gameObject.SetActive(false);
        if (FlurryOfBlowsButton != null) FlurryOfBlowsButton.gameObject.SetActive(false);
        if (RageButton != null) RageButton.gameObject.SetActive(false);
        if (BardicMusicButton != null) BardicMusicButton.gameObject.SetActive(false);
        // The Maintain button stays: a grappling or pinned caster may keep its spell up (ConcentrationRules; delegated
        // RAW reading 2026-10-15, PHB p.156, p.176), as it may cast.
        if (ReloadButton != null) ReloadButton.gameObject.SetActive(false);
        if (DropEquippedItemButton != null) DropEquippedItemButton.gameObject.SetActive(false);
        if (PickUpItemButton != null) PickUpItemButton.gameObject.SetActive(false);
        if (UseItemButton != null) UseItemButton.gameObject.SetActive(false);
        // The damage-mode toggle stays: grapple attacks (natural attacks, the rake, unarmed strikes and light weapons)
        // read the chosen mode, and a natural attack or rake may be made nonlethal at -4 (PHB p.146; delegated RAW
        // reading 2026-10-14), so a grappling PC keeps the choice it has for any other attack.
        if (BreakWallButton != null) BreakWallButton.gameObject.SetActive(false);
        if (UseImbuedSpellButton != null) UseImbuedSpellButton.gameObject.SetActive(false);
        if (ControlFlamingSphereButton != null) ControlFlamingSphereButton.gameObject.SetActive(false);
        if (DischargeTouchButton != null) DischargeTouchButton.gameObject.SetActive(false);
        if (DismissDisguiseSelfButton != null) DismissDisguiseSelfButton.gameObject.SetActive(false);
        if (DismissExpeditiousRetreatButton != null) DismissExpeditiousRetreatButton.gameObject.SetActive(false);
        if (DismissJumpButton != null) DismissJumpButton.gameObject.SetActive(false);
        if (DismissInvisibilityButton != null) DismissInvisibilityButton.gameObject.SetActive(false);
        if (DismissSeeInvisibilityButton != null) DismissSeeInvisibilityButton.gameObject.SetActive(false);
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
        if (DomainPowerButton != null) DomainPowerButton.gameObject.SetActive(false);
        if (SmiteButton != null) SmiteButton.gameObject.SetActive(false);
        if (LayOnHandsButton != null) LayOnHandsButton.gameObject.SetActive(false);
        if (WildShapeButton != null) WildShapeButton.gameObject.SetActive(false);
        if (GrappleActionsButton != null) GrappleActionsButton.gameObject.SetActive(false);
        if (GrappleDamageButton != null) GrappleDamageButton.gameObject.SetActive(false);
        if (GrappleLightWeaponAttackButton != null) GrappleLightWeaponAttackButton.gameObject.SetActive(false);
        if (GrappleUnarmedAttackButton != null) GrappleUnarmedAttackButton.gameObject.SetActive(false);
        if (GrapplePinButton != null) GrapplePinButton.gameObject.SetActive(false);
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
        if (UseItemButton != null) UseItemButton.gameObject.SetActive(false);
        if (DamageModeToggleButton != null) DamageModeToggleButton.gameObject.SetActive(false);
        if (FlurryOfBlowsButton != null) FlurryOfBlowsButton.gameObject.SetActive(false);
        if (RageButton != null) RageButton.gameObject.SetActive(false);
        if (BardicMusicButton != null) BardicMusicButton.gameObject.SetActive(false);
        if (CastSpellButton != null) CastSpellButton.gameObject.SetActive(false);
        if (MaintainConcentrationButton != null) MaintainConcentrationButton.gameObject.SetActive(false);
        if (BreakWallButton != null) BreakWallButton.gameObject.SetActive(false);
        if (UseImbuedSpellButton != null) UseImbuedSpellButton.gameObject.SetActive(false);
        if (DischargeTouchButton != null) DischargeTouchButton.gameObject.SetActive(false);
        if (DismissDisguiseSelfButton != null) DismissDisguiseSelfButton.gameObject.SetActive(false);
        if (DismissExpeditiousRetreatButton != null) DismissExpeditiousRetreatButton.gameObject.SetActive(false);
        if (DismissJumpButton != null) DismissJumpButton.gameObject.SetActive(false);
        if (DismissInvisibilityButton != null) DismissInvisibilityButton.gameObject.SetActive(false);
        if (DismissSeeInvisibilityButton != null) DismissSeeInvisibilityButton.gameObject.SetActive(false);
        if (AttackDefensivelyButton != null) AttackDefensivelyButton.gameObject.SetActive(false);
        if (FullAttackDefensivelyButton != null) FullAttackDefensivelyButton.gameObject.SetActive(false);
        if (TotalDefenseButton != null) TotalDefenseButton.gameObject.SetActive(false);
        if (MonsterAbilityButton != null) MonsterAbilityButton.gameObject.SetActive(false);
    }
}
