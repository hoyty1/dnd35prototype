using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The maneuver core's executor and legality (batch 12 step 19, docs/designs/pc_npc_parity_batch12.md; CMB-102, CMB-098,
/// D15). One path for every maneuver a creature starts against an opponent, PC or NPC: the PC wrapper
/// (<see cref="ExecuteSpecialAttack"/>, after the Special Attack menu and the target click) and the NPC executor
/// (TryNPCSpecialAttackIfBeneficial) build a <see cref="ManeuverRequest"/>; <see cref="ExecuteManeuver"/> checks it
/// (<see cref="CanStartManeuver"/>), pays the row of the cost table (ManeuverActionCost.For) before anything is rolled,
/// commits the ward save (D6), makes the initiation AoOs (ResolveManeuverInitiationAoOs, CMB-076), resolves the maneuver
/// (CharacterController.ExecuteSpecialAttack), runs the melee reactions and the aftermath (the trip's Improved Trip attack
/// and counter-trip, the bull rush push, each asked of the right decider), then the post-attack hook for the maneuvers that
/// are attacks (OnAttackResolved, AttackOrigin.Maneuver). The executor is synchronous with a completion callback, not a
/// coroutine as the design sketched: the NPC step hook of the attack-sequence executor (AttackSequenceRequest.TryStepManeuver)
/// is synchronous, and the only wait (a controllable defender's counter-trip prompt) is already waited on by the NPC
/// coroutines (IsAwaitingCounterTripChoice). No UI continuation runs here: the PC wrapper's callback does it.
/// </summary>
public partial class GameManager
{
    /// <summary>
    /// The intent of the attack step a maneuver replaces in an NPC attack sequence (RunAttackSequence sets it around its step
    /// hook): its two-weapon state, whose main-hand penalty the maneuver takes (PHB p.160). Null outside that hook.
    /// </summary>
    private AttackIntent _maneuverStepIntent;

    /// <summary>
    /// May <paramref name="req"/>'s attacker start its maneuver now? Pure: no dice, nothing spent. The Improved Grab hold bar
    /// (MM p.310, CMB-163), the bull rush and trip limits (CanBullRush, CanTrip; PHB p.154, p.158), the direct-attack gate
    /// for the maneuvers made against an opponent in reach (AttackLegality.CanDirectlyAttack with DirectAttackKind.Maneuver:
    /// the Resilient Sphere, the maneuver's reach, a known failed ward save), a sunder's weapon or natural attack (PHB p.158,
    /// CMB-102), a coup de grace's helpless, critable target (PHB p.153), and the cost the table asks (CanAffordManeuver).
    /// </summary>
    internal Legality CanStartManeuver(ManeuverRequest req)
    {
        if (req == null || req.Attacker == null || req.Target == null || req.Attacker.Stats == null || req.Target.Stats == null)
            return Legality.Refused("no attacker or target");

        CharacterController attacker = req.Attacker;
        CharacterController target = req.Target;
        SpecialAttackType type = req.Type;

        if (attacker.IsBarredFromSpecialAttackOn(type, target, out string holdReason))
            return Legality.Refused(holdReason);

        // The rows the table marks as part of a move or a charge are never standing actions: an overrun is made only
        // during a move or a charge (PHB p.157), and the charge bull rush ends a charge (PHB p.154). Their own flows make
        // them (the overrun move-through, OverrunSystem; the charge executors), never this executor, so nothing is paid.
        if (req.Cost.MoveOrChargeOnly)
            return Legality.Refused("an overrun is made only during a move or a charge (PHB p.157)");
        if (req.Cost.ChargeEnd)
            return Legality.Refused("the charge bull rush is made through the charge (PHB p.154)");

        if (type == SpecialAttackType.BullRushAttack && !attacker.CanBullRush(target, false, out string bullRushReason))
            return Legality.Refused(bullRushReason);

        if (type == SpecialAttackType.Trip && !attacker.CanTrip(target, out string tripReason))
            return Legality.Refused(tripReason);

        if (IsDirectAttackManeuverForGate(type))
        {
            Legality gate = AttackLegality.CanDirectlyAttack(attacker, target, DirectAttackKind.Maneuver, req.Intent);
            if (!gate.Ok)
                return gate;
        }

        if (type == SpecialAttackType.CoupDeGrace)
        {
            if (!target.IsHelplessForCoupDeGrace())
                return Legality.Refused($"{target.Stats.CharacterName} is not helpless");
            if (target.IsImmuneToCriticalHits())
                return Legality.Refused($"{target.Stats.CharacterName} is immune to critical hits");
        }

        if (!CanAffordManeuver(req, out string costReason))
            return Legality.Refused(costReason);

        if (type == SpecialAttackType.Sunder && !req.UseOffHand && !CanSunderWithManeuverStep(attacker, out string sunderReason))
            return Legality.Refused(sunderReason);

        return Legality.Allowed();
    }

    /// <summary>
    /// Whether the attacker has what the cost table asks of <paramref name="req"/> (ManeuverActionCost.For): one more attack
    /// step of its sequence for trip, disarm, sunder and grapple (an off-hand step for an off-hand disarm or sunder); a
    /// standard action, a move action for a feint with Improved Feint (PHB p.95), or the full round for a coup de grace.
    /// </summary>
    private bool CanAffordManeuver(ManeuverRequest req, out string reason)
    {
        reason = string.Empty;
        CharacterController attacker = req.Attacker;
        if (attacker.Actions == null)
        {
            reason = "no action economy";
            return false;
        }

        ManeuverCost cost = req.Cost;
        if (cost.ReplacesMeleeAttack)
        {
            if (req.Type == SpecialAttackType.Grapple)
            {
                if (CanUseGrappleAttackOption(attacker))
                    return true;
                reason = "no attack left to grapple with";
                return false;
            }

            if (req.UseOffHand && (req.Type == SpecialAttackType.Disarm || req.Type == SpecialAttackType.Sunder))
            {
                if (attacker.GetOffHandAttackWeapon() != null && attacker.CanCommitAttack(AttackStepKind.OffHand, out reason))
                    return true;
                if (string.IsNullOrEmpty(reason))
                    reason = "no off-hand weapon";
                return false;
            }

            if (GetRemainingMainHandManeuverAttackActions(attacker) > 0)
                return true;
            attacker.CanCommitAttack(attacker.GetManeuverSubstituteStepKind(), out reason);
            if (string.IsNullOrEmpty(reason))
                reason = "no attack left this turn";
            return false;
        }

        switch (cost.Action)
        {
            case ActionCostType.Move: // a feint with Improved Feint (PHB p.95), or the standard action it can still take
                if (CanUseImprovedFeintAsMove(attacker) || attacker.Actions.HasStandardAction)
                    return true;
                reason = "no move or standard action left";
                return false;
            case ActionCostType.FullRound:
                if (attacker.Actions.HasFullRoundAction)
                    return true;
                reason = "the full-round action is already spent";
                return false;
            default:
                if (attacker.Actions.HasStandardAction)
                    return true;
                reason = "the standard action is already spent";
                return false;
        }
    }

    /// <summary>
    /// A sunder needs a weapon, a natural attack that deals slashing or bludgeoning damage or an unarmed strike (PHB p.158;
    /// CMB-102, CMB-141): a creature fighting with its natural attacks sunders with the natural attack its maneuvers give
    /// up next (<see cref="GetManeuverSunderNaturalAttackIndex"/>), any other one by CharacterController.CanSunderWithAttack.
    /// </summary>
    private bool CanSunderWithManeuverStep(CharacterController attacker, out string reason)
    {
        reason = string.Empty;
        if (IsNaturalManeuverAttacker(attacker))
        {
            if (GetManeuverSunderNaturalAttackIndex(attacker, out _) >= 0)
                return true;
            reason = "the next natural attack deals no slashing or bludgeoning damage, so it cannot sunder (PHB p.158)";
            return false;
        }
        return attacker.CanSunderWithAttack(-1, out reason);
    }

    /// <summary>
    /// Pays the row of the cost table for <paramref name="req"/> (ManeuverActionCost.For), for PCs and NPCs alike, before any
    /// roll: the attack step a trip, disarm, sunder or grapple replaces (TryConsume*AttackAction: an iterative step at its
    /// BAB, with the two-weapon main-hand penalty when the intent fights with two weapons (PHB p.160), or one natural attack
    /// at its BAB, MM p.312; an off-hand step for an off-hand disarm or sunder), the feint's standard or move action
    /// (TryConsumeFeintAction), the coup de grace's full round, or the standard action.
    /// </summary>
    internal bool TryPayManeuver(ManeuverRequest req, ManeuverPayment pay, out string reason)
    {
        reason = string.Empty;
        CharacterController attacker = req.Attacker;
        switch (req.Type)
        {
            case SpecialAttackType.Feint:
            {
                if (!TryConsumeFeintAction(attacker, out string label))
                {
                    reason = "no eligible action remaining";
                    return false;
                }
                pay.ActionLabel = label;
                return true;
            }
            case SpecialAttackType.CoupDeGrace:
                if (!attacker.Actions.HasFullRoundAction)
                {
                    reason = "full-round action already spent";
                    return false;
                }
                attacker.Actions.UseFullRoundAction();
                pay.ActionLabel = "full-round action";
                return true;
            case SpecialAttackType.Disarm:
            {
                if (!TryConsumeDisarmAttackAction(attacker, req.UseOffHand, out int bab, out int remaining, out reason, out bool offHand, out ItemData weapon, req.Intent))
                    return false;
                FillStepPayment(pay, attacker, req.Intent, bab, remaining, offHand, weapon);
                return true;
            }
            case SpecialAttackType.Sunder:
            {
                if (!TryConsumeSunderAttackAction(attacker, req.UseOffHand, out int bab, out int remaining, out reason, out bool offHand, out ItemData weapon, req.Intent))
                    return false;
                FillStepPayment(pay, attacker, req.Intent, bab, remaining, offHand, weapon);
                return true;
            }
            case SpecialAttackType.Grapple:
            {
                if (!TryConsumeGrappleAttackAction(attacker, out int bab, out int remaining, out reason, null, -1, false, req.Intent))
                    return false;
                pay.StepBab = bab;
                pay.AttacksRemaining = remaining;
                pay.ActionLabel = $"attack BAB {CharacterStats.FormatMod(bab)} ({remaining} remaining)";
                return true;
            }
            case SpecialAttackType.Trip:
            {
                if (!TryConsumeTripAttackAction(attacker, out int bab, out int remaining, out reason, req.Intent))
                    return false;
                pay.StepBab = bab;
                pay.AttacksRemaining = remaining;
                pay.ActionLabel = $"attack BAB {CharacterStats.FormatMod(bab)} ({remaining} remaining)";
                return true;
            }
            default:
                // Bull rush (PHB p.154), overrun, aid another and the rest: a standard action (the table's default row).
                if (!attacker.CommitStandardAction())
                {
                    reason = "standard action already spent";
                    return false;
                }
                pay.ActionLabel = "standard action";
                return true;
        }
    }

    private static void FillStepPayment(ManeuverPayment pay, CharacterController attacker, AttackIntent intent, int bab, int remaining, bool offHand, ItemData weapon)
    {
        pay.StepBab = bab;
        pay.AttacksRemaining = remaining;
        pay.UsedOffHand = offHand;
        pay.Weapon = weapon;
        if (intent != null && intent.TwoWeapon)
        {
            var (mainPenalty, offPenalty, _) = attacker.GetDualWieldPenalties();
            pay.DualWieldPenalty = offHand ? offPenalty : mainPenalty;
        }
        pay.ActionLabel = $"{(offHand ? "off-hand" : "main-hand")}, BAB {CharacterStats.FormatMod(bab)} ({remaining} remaining)";
    }

    /// <summary>
    /// The one maneuver executor (see the class summary). Returns the outcome at once; <paramref name="onSettled"/> runs
    /// exactly once when everything that follows is settled: at once, or after a controllable defender answers its
    /// counter-trip prompt or a controllable bull rusher its push prompt (until then <see cref="ManeuverOutcome.Settled"/> is
    /// false). A refused request spends nothing; every other outcome has paid its cost.
    /// </summary>
    internal ManeuverOutcome ExecuteManeuver(ManeuverRequest req, Action<ManeuverOutcome> onSettled)
    {
        var outcome = new ManeuverOutcome { Request = req };
        void Settle()
        {
            if (outcome.Settled)
                return;
            outcome.Settled = true;
            onSettled?.Invoke(outcome);
        }

        // 1. Legality (pure).
        Legality legality = CanStartManeuver(req);
        if (!legality.Ok)
        {
            outcome.Kind = ManeuverOutcomeKind.Refused;
            outcome.Reason = legality.Reason;
            Settle();
            return outcome;
        }

        CharacterController attacker = req.Attacker;
        CharacterController target = req.Target;
        SpecialAttackType type = req.Type;

        // 2. The cost, before the ward and the AoOs, on both sides (CMB-102).
        var pay = new ManeuverPayment();
        if (!TryPayManeuver(req, pay, out string payReason))
        {
            outcome.Kind = ManeuverOutcomeKind.Refused;
            outcome.Reason = string.IsNullOrWhiteSpace(payReason) ? "no eligible action remaining" : payReason;
            Settle();
            return outcome;
        }
        outcome.Payment = pay;
        req.OnPaid?.Invoke(pay);

        // 3. Sanctuary and Hide from Undead (PHB p.274, p.241; D6): one save, after the cost; a failure loses the paid step
        //    or action and the maneuver is never made (so it provokes nothing).
        if (IsDirectAttackManeuverForGate(type)
            && AttackLegality.CommitWard(attacker, target, DirectAttackKind.Maneuver, req.Intent) == WardVerdict.Failed)
        {
            outcome.Kind = ManeuverOutcomeKind.WardFailed;
            UpdateAllStatsUI();
            Settle();
            return outcome;
        }

        // 4. The initiation AoOs (CMB-076). A foiled attempt has spent its cost.
        if (ResolveManeuverInitiationAoOs(attacker, target, type) != ManeuverAoOOutcome.Proceed)
        {
            outcome.Kind = ManeuverOutcomeKind.Foiled;
            outcome.AttackerDown = CombatEndRules.IsOutOfFight(attacker);
            UpdateAllStatsUI();
            Settle();
            return outcome;
        }

        // 5. Resolve. The hostile-action notice (charm, fascination) is sent by CharacterController.ExecuteSpecialAttack, and
        //    the target's reaction (the acid spray, D1) resolves as it returns.
        bool disarm = type == SpecialAttackType.Disarm;
        bool sunder = type == SpecialAttackType.Sunder;
        SpecialAttackResult result = attacker.ExecuteSpecialAttack(
            type,
            target,
            disarm ? req.ItemSlot : null,
            disarm ? pay.StepBab : null,
            type == SpecialAttackType.Grapple ? pay.StepBab : null,
            bullRushChargeBonusOverride: type == SpecialAttackType.BullRushCharge ? 2 : 0,
            disarmAttackerWeaponOverride: disarm ? pay.Weapon : null,
            tripAttackBonusOverride: type == SpecialAttackType.Trip ? pay.StepBab : null,
            disarmUsedOffHand: disarm && pay.UsedOffHand,
            disarmDualWieldPenaltyForLog: disarm ? pay.DualWieldPenalty : 0,
            sunderTargetSlot: sunder ? req.ItemSlot : null,
            sunderAttackBonusOverride: sunder ? pay.StepBab : null,
            sunderAttackerWeaponOverride: sunder ? pay.Weapon : null,
            sunderUsedOffHand: sunder && pay.UsedOffHand,
            sunderDualWieldPenaltyForLog: sunder ? pay.DualWieldPenalty : 0);
        outcome.Kind = ManeuverOutcomeKind.Resolved;
        outcome.Result = result;

        if (req.Source == ActorSource.Ui)
        {
            if (disarm)
                CombatUI?.ShowCombatLog(result.Log);
            else
                CombatUI?.ShowCombatLog(CombatLogHelper.Buff("⚔", $"SPECIAL [{type}] ({pay.ActionLabel}): {result.Log}"));
        }
        else
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Death("☠", $"{attacker.Stats.CharacterName} uses SPECIAL [{type}]! {result.Log}"));
        }
        req.OnResolvedReport?.Invoke(result, pay);

        // 6. A reaction that ended the combat or dropped the attacker: nothing of the aftermath follows.
        if (CurrentPhase == TurnPhase.CombatOver || CombatEndRules.IsOutOfFight(attacker))
        {
            outcome.AttackerDown = CombatEndRules.IsOutOfFight(attacker);
            UpdateAllStatsUI();
            Settle();
            return outcome;
        }

        // 7. Melee reactions (Fire Shield and the like): trip and disarm are melee contacts.
        if (type == SpecialAttackType.Trip || disarm)
            MeleeReactionService.TriggerReactions(attacker, target, null);

        void Finish()
        {
            if (CurrentPhase != TurnPhase.CombatOver && IsAttackManeuver(type))
            {
                AttackResolution res = AttackResolution.Create(AttackOrigin.Maneuver, attacker, target, req.Intent,
                    req.Source == ActorSource.AI ? ActorSource.AI : (ActorSource?)null);
                res.CombatEndContext = "Maneuver." + type;
                RunAttackResolvedSync(res, new PostAttackOutcome());
            }
            outcome.AttackerDown |= CombatEndRules.IsOutOfFight(attacker);
            UpdateAllStatsUI();
            Settle();
        }

        // 8. The aftermath: the Improved Trip attack and the defender's counter-trip (PHB p.96, p.158; CMB-079), the bull
        //    rush push and follow (PHB p.154), each choice asked of its decider.
        if (type == SpecialAttackType.Trip)
        {
            HandleTripAftermath(attacker, target, result, Finish);
            return outcome;
        }

        if (result.Success && (type == SpecialAttackType.BullRushAttack || type == SpecialAttackType.BullRushCharge))
        {
            ResolveBullRushPushAndFollow(attacker, target, result, isCharge: type == SpecialAttackType.BullRushCharge, squaresMovedThisCharge: 0,
                onComplete: attackerDown =>
                {
                    outcome.AttackerDown = attackerDown;
                    Finish();
                },
                decider: req.Decider);
            return outcome;
        }

        Finish();
        return outcome;
    }

    /// <summary>The maneuvers that are attacks (PHB p.155-158, p.153): the post-attack hook runs for them (AttackOrigin.Maneuver).</summary>
    private static bool IsAttackManeuver(SpecialAttackType type)
        => type == SpecialAttackType.Trip || type == SpecialAttackType.Disarm || type == SpecialAttackType.Sunder
           || type == SpecialAttackType.Grapple || type == SpecialAttackType.CoupDeGrace;

    /// <summary>
    /// The Special Attack menu's availability of <paramref name="type"/> for the PC, from the cost table (ManeuverActionCost.For):
    /// an attack step left for the maneuvers that replace one (the PC's remaining-attempt counts), the move or standard
    /// action of a feint, the full round of a coup de grace (with an adjacent helpless foe) or a charge bull rush, else the
    /// standard action (a standing bull rush also not while grappling). Read by OnSpecialAttackSelected and CombatUI's buttons.
    /// </summary>
    public bool CanAffordManeuverForUi(CharacterController pc, SpecialAttackType type, bool useOffHand)
    {
        if (pc == null || pc.Actions == null)
            return false;

        ManeuverCost cost = ManeuverActionCost.For(type, pc);
        if (cost.ReplacesMeleeAttack)
        {
            switch (type)
            {
                case SpecialAttackType.Grapple: return CanUseGrappleAttackOption(pc);
                case SpecialAttackType.Trip: return CanUseTripAttackOption(pc);
                case SpecialAttackType.Disarm: return useOffHand ? CanUseOffHandDisarmAttackOption(pc) : CanUseMainHandDisarmAttackOption(pc);
                case SpecialAttackType.Sunder: return useOffHand ? CanUseOffHandSunderAttackOption(pc) : CanUseMainHandSunderAttackOption(pc);
                default: return GetRemainingMainHandManeuverAttackActions(pc) > 0;
            }
        }

        if (cost.ChargeEnd)
            return pc.Actions.HasFullRoundAction;

        switch (cost.Action)
        {
            case ActionCostType.Move:
                return CanUseImprovedFeintAsMove(pc) || pc.Actions.HasStandardAction;
            case ActionCostType.FullRound:
                return type == SpecialAttackType.CoupDeGrace ? CanUseCoupDeGraceAttackOption(pc) : pc.Actions.HasFullRoundAction;
            default:
                return type == SpecialAttackType.BullRushAttack ? CanUseBullRushAttackOption(pc) : pc.Actions.HasStandardAction;
        }
    }

    /// <summary>The menu's refusal line when <see cref="CanAffordManeuverForUi"/> is false, by the same table.</summary>
    private static string DescribeUnaffordableManeuver(SpecialAttackType type, bool useOffHand)
    {
        switch (type)
        {
            case SpecialAttackType.Feint: return "Need a standard action, or a move action with Improved Feint";
            case SpecialAttackType.Grapple: return "Need at least one remaining grapple attack";
            case SpecialAttackType.BullRushAttack: return "Need a standard action (bull rush cannot be used while grappling)";
            case SpecialAttackType.Trip: return "Need at least one remaining trip attack";
            case SpecialAttackType.Disarm: return useOffHand ? "Need an available off-hand disarm attack" : "Need at least one remaining main-hand disarm attack";
            case SpecialAttackType.Sunder: return useOffHand ? "Need an available off-hand sunder attack" : "Need at least one remaining main-hand sunder attack";
            case SpecialAttackType.CoupDeGrace: return "Need a full-round action, an adjacent helpless enemy, and a melee attack option";
            case SpecialAttackType.BullRushCharge: return "Need a full-round action and valid charge movement";
            default: return "Need a standard action";
        }
    }

    // ── The UI decider's prompts (UiManeuverDecider). Each returns false when it cannot ask now (no UI, or not the
    //    creature's own turn for a selection), and the AI decider then answers. ──

    /// <summary>
    /// The disarm or sunder item menu (D15). With one item it answers at once; with several it opens the menu and answers
    /// with the slot chosen (re-validated against the target's gear), or null for Cancel.
    /// </summary>
    internal bool PromptManeuverItem(CharacterController attacker, CharacterController target, SpecialAttackType type, Action<EquipSlot?> answer)
    {
        if (target == null || target.Stats == null || CombatUI == null)
            return false;

        if (type == SpecialAttackType.Sunder)
        {
            List<SunderableItemOption> options = target.GetSunderableItemOptions();
            if (options.Count <= 1)
            {
                answer?.Invoke(options.Count == 1 ? options[0].Slot : (EquipSlot?)null);
                return true;
            }

            var labels = new List<string>(options.Count);
            for (int i = 0; i < options.Count; i++)
                labels.Add(options[i].GetLabel());
            CombatUI.ShowSunderItemSelection(target.Stats.CharacterName, labels,
                onSelect: index =>
                {
                    if (index < 0 || index >= options.Count) { answer?.Invoke(null); return; }
                    EquipSlot slot = options[index].Slot;
                    if (!target.GetSunderableItemOptions().Exists(o => o.Slot == slot))
                    {
                        CombatUI.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{target.Stats.CharacterName}'s selected item is no longer equipped."));
                        answer?.Invoke(null);
                        return;
                    }
                    answer?.Invoke(slot);
                },
                onCancel: () => answer?.Invoke(null));
            return true;
        }

        List<DisarmableHeldItemOption> held = target.GetDisarmableHeldItemOptions();
        if (held.Count <= 1)
        {
            answer?.Invoke(held.Count == 1 ? held[0].HandSlot : (EquipSlot?)null);
            return true;
        }

        var heldLabels = new List<string>(held.Count);
        for (int i = 0; i < held.Count; i++)
        {
            string handLabel = held[i].HandSlot == EquipSlot.RightHand ? "Main Hand" : "Off-Hand";
            string heldItemName = held[i].HeldItem != null ? held[i].HeldItem.Name : "Held Item";
            heldLabels.Add($"{handLabel}: {heldItemName}");
        }
        CombatUI.ShowDisarmWeaponSelection(target.Stats.CharacterName, heldLabels,
            onSelect: index =>
            {
                if (index < 0 || index >= held.Count) { answer?.Invoke(null); return; }
                EquipSlot slot = held[index].HandSlot;
                if (!target.GetDisarmableHeldItemOptions().Exists(o => o.HandSlot == slot))
                {
                    CombatUI.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{target.Stats.CharacterName}'s selected held item is no longer equipped."));
                    answer?.Invoke(null);
                    return;
                }
                answer?.Invoke(slot);
            },
            onCancel: () => answer?.Invoke(null));
        return true;
    }

    /// <summary>The bull rush push prompt (PHB p.154): stay and push 5 ft, or follow up to <paramref name="maxIfFollowing"/> squares.</summary>
    internal bool PromptBullRushPush(CharacterController attacker, CharacterController target, int maxIfFollowing, Action<bool, int> answer)
    {
        if (CombatUI == null)
            return false;
        CombatUI.ShowBullRushExtraPushChoice(attacker, target, maxIfFollowing,
            onSelect: followSquares => answer?.Invoke(followSquares > 0, followSquares),
            onCancel: () => answer?.Invoke(false, 1));
        return true;
    }

    /// <summary>The AI decider's bull rush push (AIService.ChooseBullRushPush, the attacker's profile).</summary>
    internal int ChooseBullRushPushForAIDecider(CharacterController attacker, CharacterController target, int maxIfFollowing, out bool follow)
        => ChooseBullRushPushForAI(attacker, target, maxIfFollowing, out follow);

    /// <summary>The counter-trip prompt (PHB p.158, CMB-079); false while another one is open or without a UI.</summary>
    internal bool PromptCounterTrip(CharacterController defender, CharacterController tripper, Action<bool> answer)
    {
        if (CombatUI == null || _counterTripPromptOpen)
            return false;
        OpenCounterTripPrompt(defender, tripper, answer);
        return true;
    }

    /// <summary>The AI decider's counter-trip (AIService.ShouldCounterTrip, the defender's profile).</summary>
    internal bool ShouldCounterTripForAIDecider(CharacterController defender, CharacterController tripper)
        => _aiService != null
            ? _aiService.ShouldCounterTrip(defender, tripper)
            : DND35.AI.AIProfile.DefaultShouldCounterTrip(defender, tripper);

    /// <summary>True when <paramref name="actor"/> can click a square now: the controllable creature whose PC turn it is.</summary>
    private bool CanSelectSquareNow(CharacterController actor)
        => actor != null && actor.IsControllable && CurrentPhase == TurnPhase.PCTurn && ActivePC == actor && CombatUI != null && Grid != null;
}
