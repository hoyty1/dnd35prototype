using System.Collections;
using UnityEngine;

/// <summary>
/// GameManager partial class: the item entries' cores (batch 13 step 14, D7; docs/architecture/combat-actions.md), moved
/// from the PC-bound consumable code (the old TryUseConsumableFromInventory combat path, ResolveConsumableUseProvocation and
/// ResolveConsumableAoOsAndApply), at their RAW costs (ItemUseRules): retrieving a stored item is a move action that
/// provokes and leaves the item in hand; a potion is a standard action that provokes (a full round to administer it to an
/// unconscious creature); a scroll takes its spell's casting time and provokes as casting; a wand a standard action (or
/// the casting time) and does not provoke. No UI follow-up here: the PC runner's UiAfter does it.
/// </summary>
public partial class GameManager
{
    /// <summary>
    /// True while the use-item core hands a scroll or wand to the cast core for an AI-run actor (TryUseScroll and
    /// TryUseWand then run the cast through the AI decider, RunItemCastForAI, instead of opening the PC's targeting).
    /// </summary>
    private bool _itemCastForAI;

    /// <summary>The target of that AI item cast (the context's target; null: the user itself).</summary>
    private CharacterController _itemCastAiTarget;

    /// <summary>
    /// Set by TryUseScroll and TryUseWand when the activation itself was attempted (a Use Magic Device check or a
    /// scroll's caster level check was rolled, DMG p.213, p.238): the use's action is then spent whether or not a spell
    /// came of it (PHB p.86, Use Magic Device: the check is part of the activation action).
    /// </summary>
    private bool _itemActivationRolled;

    /// <summary>Set by RunItemCastForAI when the cast core went past its Legality stage, so its Cost stage paid the activation.</summary>
    private bool _itemCastPaid;

    /// <summary>
    /// Why <paramref name="actor"/> cannot use an item now at all, or null: no condition bars it
    /// (ItemManipulationConditionRefusal: pinned, wild shape), a usable item in its pack, and an action to take: a move or
    /// standard action (CanUseItemManipulationAction), or, with neither left, a held item whose use is a free action (a
    /// scroll of Feather Fall, PHB p.174; D7). The PC's Use Item button and the use-item and retrieve-item entries'
    /// CanStart read it.
    /// </summary>
    internal string GetUseItemDisabledReason(CharacterController actor)
    {
        string condition = ItemManipulationConditionRefusal(actor);
        if (condition != null)
            return condition;
        if (!HasUsableItem(actor))
            return "No usable item.";
        if (!CanUseItemManipulationAction(actor, out string reason) && !HoldsFreeUseItem(actor))
            return reason;
        return null;
    }

    /// <summary>True when the item <paramref name="actor"/> holds (CharacterController.RetrievedItem) is in its pack and its use is a free action it can still take this round.</summary>
    private bool HoldsFreeUseItem(CharacterController actor)
    {
        ItemData held = actor != null ? actor.RetrievedItem : null;
        if (held == null || !actor.IsHoldingRetrievedItem(held))
            return false;
        return GetItemUseCost(actor, held, administer: false) == ActionCostType.Free && CanPayItemUse(actor, ActionCostType.Free);
    }

    /// <summary>True when <paramref name="actor"/>'s pack holds a consumable (a potion, an oil, a scroll, a wand).</summary>
    internal static bool HasUsableItem(CharacterController actor)
    {
        Inventory inv = actor != null && actor.InventoryComp != null ? actor.InventoryComp.CharacterInventory : null;
        if (inv == null || inv.GeneralSlots == null)
            return false;
        for (int i = 0; i < inv.GeneralSlots.Length; i++)
            if (inv.GeneralSlots[i] != null && inv.GeneralSlots[i].IsConsumable)
                return true;
        return false;
    }

    /// <summary>
    /// Why the pack item at <paramref name="inventoryIndex"/> cannot be used, or null: no inventory, no such slot, an
    /// empty slot, a slot holding another item than <paramref name="item"/> (when given), or not a consumable.
    /// </summary>
    internal string GetUseItemRefusal(CharacterController actor, int inventoryIndex, ItemData item)
    {
        Inventory inv = actor != null && actor.InventoryComp != null ? actor.InventoryComp.CharacterInventory : null;
        if (inv == null || inv.GeneralSlots == null)
            return "no inventory";
        if (inventoryIndex < 0 || inventoryIndex >= inv.GeneralSlots.Length)
            return "invalid inventory slot";
        ItemData inSlot = inv.GeneralSlots[inventoryIndex];
        if (inSlot == null)
            return "the inventory slot is empty";
        if (item != null && !ReferenceEquals(item, inSlot))
            return item.Name + " is no longer in that slot";
        if (!inSlot.IsConsumable)
            return inSlot.Name + " is not a consumable item";
        return null;
    }

    /// <summary>
    /// The action-bar refusal of using the pack item at <paramref name="inventoryIndex"/> now (null: it can be started):
    /// a stored item needs the move action that retrieves it (a standard action traded for one included); an item in
    /// hand needs its use's own action (<see cref="GetItemUseCost"/>). An administered potion's target must be an
    /// unconscious creature beside the actor (DMG p.229). <paramref name="aiCast"/>: the AI's scroll and wand casts have
    /// no area, summon or wall aim yet (batch 15).
    /// </summary>
    internal Legality CheckItemUse(CharacterController actor, int inventoryIndex, ItemData item, CharacterController target,
        string variant, bool aiCast)
    {
        string refusal = GetUseItemRefusal(actor, inventoryIndex, item);
        if (refusal != null)
            return Legality.Refused(LegalityBar.Equipment, refusal);
        item = actor.InventoryComp.CharacterInventory.GeneralSlots[inventoryIndex];
        ItemUseKind kind = ItemUseRules.KindOf(item);
        bool administer = variant == ItemUseRules.VariantAdminister;

        if (administer)
        {
            if (kind != ItemUseKind.Potion)
                return Legality.Refused(LegalityBar.Equipment, "only a potion or oil can be administered");
            if (target == null || target == actor || target.Stats == null || target.IsDead || !target.IsUnconscious)
                return Legality.Refused(LegalityBar.Target, "a potion is administered to an unconscious creature (DMG p.229)");
            if (actor.GetMinimumDistanceToTarget(target, chebyshev: true) > 1)
                return Legality.Refused(LegalityBar.Target, "out of reach: administering a potion needs the creature beside the actor");
        }

        // A scroll or wand the actor cannot use at all (not on its class list and no Use Magic Device; DMG p.237-238,
        // p.245) is refused before anything is retrieved or paid; the Use Magic Device roll itself comes with the use.
        if (kind == ItemUseKind.Scroll)
        {
            ScrollValidator.ScrollValidationResult scrollCheck = ScrollValidator.Validate(actor, item);
            if (scrollCheck != null && !scrollCheck.CanUse)
                return Legality.Refused(LegalityBar.Equipment, scrollCheck.FailureReason ?? (item.Name + " cannot be used"));
        }
        else if (kind == ItemUseKind.Wand)
        {
            WandValidator.WandValidationResult wandCheck = WandValidator.Validate(actor, item);
            if (wandCheck != null && !wandCheck.CanUse)
                return Legality.Refused(LegalityBar.Equipment, wandCheck.FailureReason ?? (item.Name + " cannot be used"));
        }

        if (kind == ItemUseKind.Scroll || kind == ItemUseKind.Wand)
        {
            SpellData spell = kind == ItemUseKind.Scroll ? ResolveScrollSpell(item) : ResolveWandSpell(item);
            CastSourceKind sourceKind = kind == ItemUseKind.Scroll ? CastSourceKind.Scroll : CastSourceKind.Wand;
            // Silence bars the use before anything is retrieved, paid or rolled (PHB p.279; DMG p.213, p.238): a silenced
            // reader cannot attempt it at all (SPL-092), as the cast core's Legality stage refuses it.
            string silenced = ItemUseSilenceRefusal(actor, item, spell, sourceKind, log: false);
            if (silenced != null)
                return Legality.Refused(LegalityBar.Condition, silenced);

            if (aiCast && spell != null)
            {
                if (AoESystem.RoutesToArea(spell) || spell.EffectType == SpellEffectType.Summon || spell.EffectType == SpellEffectType.Wall)
                    return Legality.Refused(LegalityBar.Other, "the AI has no area, summon or wall aim for an item cast yet (batch 15)");
                // A hostile single-target spell needs a target other than the user (the AI cast aims at the user when none).
                bool hostile = spell.TargetType == SpellTargetType.SingleEnemy || spell.EffectType == SpellEffectType.Damage;
                if (hostile && ItemSpellNeedsTargeting(spell) && (target == null || target == actor))
                    return Legality.Refused(LegalityBar.Target, spell.Name + " from " + item.Name + " needs a target other than the user");
            }
        }

        // A wand in easy reach is drawn, not retrieved (PHB p.142; batch 13 step 15, D8, ITM-092): a move action, free with
        // a regular move at base attack bonus +1 (EquipmentActionRules.DrawPaymentFor).
        if (kind == ItemUseKind.Wand && !actor.IsHoldingRetrievedItem(item))
        {
            return EquipmentActionRules.CanPay(actor, EquipmentActionRules.DrawPaymentFor(actor, item))
                ? Legality.Allowed()
                : Legality.Refused(LegalityBar.Action, "No move action left to draw " + item.Name + " (PHB p.142).");
        }

        if (ItemUseRules.MustBeInHand(kind) && !actor.IsHoldingRetrievedItem(item))
        {
            ActionEconomy a = actor.Actions;
            return a != null && (a.HasMoveAction || a.CanConvertStandardToMove)
                ? Legality.Allowed()
                : Legality.Refused(LegalityBar.Action, "No move action left to retrieve " + item.Name + " (PHB p.141).");
        }

        ActionCostType cost = GetItemUseCost(actor, item, administer);
        return CanPayItemUse(actor, cost)
            ? Legality.Allowed()
            : Legality.Refused(LegalityBar.Action, "No action left to use " + item.Name + ": it takes " + ItemUseRules.Describe(cost) + ".");
    }

    /// <summary>
    /// The action using <paramref name="item"/> (in hand) costs (ItemUseRules; D7): administering a potion a full round;
    /// a potion, staff or activated item a standard action; a scroll's or wand's spell its cast cost
    /// (CastActionRules.CastingAction: the casting time, at least a standard action, a free-action spell's item free;
    /// DMG p.213).
    /// </summary>
    internal ActionCostType GetItemUseCost(CharacterController actor, ItemData item, bool administer)
    {
        if (administer)
            return ActionCostType.FullRound;
        ItemUseKind kind = ItemUseRules.KindOf(item);
        if (kind != ItemUseKind.Scroll && kind != ItemUseKind.Wand)
            return ActionCostType.Standard;
        SpellData baseSpell = kind == ItemUseKind.Scroll ? ResolveScrollSpell(item) : ResolveWandSpell(item);
        if (baseSpell == null)
            return ActionCostType.Standard;
        MetamagicData metamagic;
        SpellData spell = kind == ItemUseKind.Scroll
            ? BuildScrollPipelineSpell(item, baseSpell, out metamagic, out _)
            : BuildWandPipelineSpell(item, baseSpell, out metamagic, out _);
        CastSourceKind sourceKind = kind == ItemUseKind.Scroll ? CastSourceKind.Scroll : CastSourceKind.Wand;
        return ItemUseRules.FromCastingAction(CastActionRules.CastingAction(spell, metamagic, new CastSource { Kind = sourceKind, Item = item }));
    }

    /// <summary>True when <paramref name="actor"/> has <paramref name="cost"/> left (a free-action scroll: the round's free-action spell, PHB p.174).</summary>
    private static bool CanPayItemUse(CharacterController actor, ActionCostType cost)
    {
        ActionEconomy a = actor != null ? actor.Actions : null;
        if (a == null)
            return false;
        switch (cost)
        {
            case ActionCostType.Free:
                return actor.Spellcasting == null || !actor.Spellcasting.HasCastQuickenedSpellThisRound;
            case ActionCostType.FullRound:
                return a.HasFullRoundAction;
            default:
                return a.HasStandardAction;
        }
    }

    /// <summary>Pays <paramref name="cost"/> for an item used outside the cast core (a potion, a self-target scroll or wand, a staff).</summary>
    private static bool PayItemUse(CharacterController actor, ActionCostType cost)
    {
        if (!CanPayItemUse(actor, cost))
            return false;
        switch (cost)
        {
            case ActionCostType.Free:
                actor.Spellcasting?.MarkQuickenedSpellCast();
                return true;
            case ActionCostType.FullRound:
                actor.Actions.UseFullRoundAction();
                return true;
            default:
                return actor.CommitStandardAction(); // a disabled actor takes its strenuous 1 hp (PHB p.145)
        }
    }

    /// <summary>
    /// A PC's item action is under way (its attacks of opportunity and their pauses): the PC is busy until the runner's
    /// follow-up returns the action panel, as the old consumable path marked it (the harness's Ui steps wait on it).
    /// </summary>
    private void MarkItemUseAnimating(ActorSource source)
    {
        if (source == ActorSource.Ui)
            CurrentSubPhase = PlayerSubPhase.Animating;
    }

    /// <summary>Pays the move action of retrieving a stored item: the move action, else a standard action traded for one (PHB p.141).</summary>
    private static bool PayRetrieve(CharacterController actor)
    {
        ActionEconomy a = actor != null ? actor.Actions : null;
        if (a == null)
            return false;
        if (a.HasMoveAction)
            return a.UseMoveAction();
        if (a.CanConvertStandardToMove)
        {
            a.ConvertStandardToMove();
            return true;
        }
        return false;
    }

    /// <summary>
    /// The spell types whose scroll or wand needs a target or an aim, so its use enters the cast core in an encounter
    /// (InitiateScrollCastThroughPipeline, InitiateWandCastThroughPipeline, or RunItemCastForAI); the rest apply to the
    /// user at once (TryApplySpellConsumableEffect).
    /// </summary>
    internal static bool ItemSpellNeedsTargeting(SpellData spell)
    {
        return spell != null && (
            spell.EffectType == SpellEffectType.Damage ||
            spell.EffectType == SpellEffectType.Summon ||
            spell.EffectType == SpellEffectType.Escape ||
            spell.EffectType == SpellEffectType.Dispel ||
            spell.EffectType == SpellEffectType.Wall ||
            spell.EffectType == SpellEffectType.Divination ||
            spell.EffectType == SpellEffectType.Utility ||
            spell.TargetType == SpellTargetType.SingleEnemy ||
            spell.TargetType == SpellTargetType.SingleAlly ||
            spell.TargetType == SpellTargetType.Touch ||
            AoESystem.RoutesToArea(spell)); // an Area target type or an area shape (Prayer), batch 11 step 23
    }

    /// <summary>True when using <paramref name="item"/> now enters the cast core, which pays and provokes for it.</summary>
    private bool ItemUseOpensCast(ItemData item)
    {
        if (item == null || !IsCombatEncounterRunning())
            return false;
        if (item.IsScroll)
            return ItemSpellNeedsTargeting(ResolveScrollSpell(item));
        if (item.IsWand)
            return ItemSpellNeedsTargeting(ResolveWandSpell(item));
        return false;
    }

    /// <summary>
    /// An AI-run scroll or wand cast through the cast core (batch 13 step 14): the item's spell (its caster level,
    /// metamagic and DC) at <see cref="_itemCastAiTarget"/> (the user when none), with the AI decider, run synchronously
    /// (RunCastSync; SpellCastRequest.ForAIPrebuilt): the Cost stage pays the casting action, a scroll provokes as
    /// casting, the Spend stage uses up the scroll or the charge. True when it was cast; <see cref="_itemCastPaid"/> says
    /// whether the core paid the activation (it went past its Legality stage and was not cancelled).
    /// </summary>
    private bool RunItemCastForAI(CharacterController actor, ItemData item, int inventoryIndex, SpellData baseSpell, CastSourceKind kind,
        out string resultMessage)
    {
        MetamagicData metamagic;
        int casterLevel;
        SpellData spell = kind == CastSourceKind.Scroll
            ? BuildScrollPipelineSpell(item, baseSpell, out metamagic, out casterLevel)
            : BuildWandPipelineSpell(item, baseSpell, out metamagic, out casterLevel);
        CharacterController target = _itemCastAiTarget != null ? _itemCastAiTarget : actor;
        SpellCastRequest request = SpellCastRequest.ForAIPrebuilt(actor, target, spell, metamagic, new CastSource
        {
            Kind = kind,
            Item = item,
            ItemIndex = inventoryIndex,
            ItemCasterLevel = casterLevel,
            SaveDCOverride = spell.SaveDC > 0 ? spell.SaveDC : (int?)null
        });

        bool cast = RunCastSync(request, out CastOutcome outcome);
        _itemCastPaid = cast || (outcome != null && outcome.Kind != CastOutcomeKind.Refused && outcome.Kind != CastOutcomeKind.Cancelled);
        string symbol = kind == CastSourceKind.Scroll ? "📜" : "🪄";
        string verb = kind == CastSourceKind.Scroll ? "reads" : "activates";
        resultMessage = cast
            ? $"{symbol} {actor.Stats.CharacterName} {verb} {item.Name} at {target.Stats.CharacterName}."
            : $"{symbol} {actor.Stats.CharacterName} cannot use {item.Name} now ({(outcome != null ? outcome.ToString() : "no cast")}).";
        return cast;
    }

    /// <summary>
    /// Retrieves the stored pack item at <paramref name="inventoryIndex"/> (the retrieve-item entry; also the first part
    /// of using a stored item): a move action, or a standard action traded for one (PHB p.141), that provokes
    /// (AoOProvokingAction.RetrieveItem): <paramref name="decider"/> confirms it, the action is paid, then each
    /// threatening foe takes its attack of opportunity. The item is then in hand (CharacterController.RetrievedItem,
    /// delegated RAW reading 2026-10-29). Performed when it was retrieved.
    /// </summary>
    internal IEnumerator ActionCore_RetrieveItem(CharacterController actor, int inventoryIndex, ItemData item, ActorSource source,
        IActionDecider decider, ActionOutcome o)
    {
        string refusal = GetUseItemRefusal(actor, inventoryIndex, item);
        if (actor == null || actor.Stats == null || refusal != null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Equipment, refusal ?? "no actor"));
            yield break;
        }
        item = actor.InventoryComp.CharacterInventory.GeneralSlots[inventoryIndex];
        if (actor.IsHoldingRetrievedItem(item))
        {
            o.Refuse(Legality.Refused(LegalityBar.Equipment, item.Name + " is already in hand"));
            yield break;
        }

        // Inlined (InlineAction), so a retrieval nothing threatens takes no frame: the use that follows runs at once, as the
        // old consumable path did (a PC's wand targeting opens in the click's own frame).
        IEnumerator provoke = InlineAction(ResolveActionProvocation(actor, AoOProvokingAction.RetrieveItem, decider, o, "retrieve " + item.Name,
            payOnProceed: () => { MarkItemUseAnimating(source); return PayRetrieve(actor); }), 0);
        while (provoke.MoveNext())
            yield return provoke.Current;
        if (o.Cancelled || o.CombatEnded || !o.Refusal.Ok)
            yield break;
        if (CombatEndRules.IsOutOfFight(actor))
        {
            o.ActorDown = true;
            o.Performed = true;
            yield break;
        }

        actor.SetRetrievedItem(item);
        o.Performed = true;
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{actor.Stats.CharacterName} retrieves {item.Name} (move action)."));
        UpdateAllStatsUI();
    }

    /// <summary>
    /// Uses the pack item at <paramref name="inventoryIndex"/> (batch 13 step 14, D7), for any creature: a stored item is
    /// retrieved first (<see cref="ActionCore_RetrieveItem"/>); when the action its use needs is then gone (a 1-round
    /// scroll, an administered potion), the action ends with the item in hand. A scroll's or wand's spell that needs an aim
    /// enters the cast core, which pays its casting action and provokes for a scroll (the PC's targeting, or for the AI
    /// RunItemCastForAI at <paramref name="target"/>). Any other use pays its action here (GetItemUseCost) and provokes as
    /// ItemUseRules says: <paramref name="decider"/> confirms, the action is paid, each threatening foe takes its attack of
    /// opportunity, and a hit forces a Concentration check as for casting a spell (DMG p.229, the potion's spell level;
    /// for a self-target scroll the damaged caster's check, explicit RAW, PHB p.69-70, DMG p.213; ITM-093), a failure leaving the potion undrunk (the
    /// action spent) or the scroll's spell lost (the scroll spent). <paramref name="variant"/> "administer" gives a
    /// potion to an unconscious <paramref name="target"/> beside the actor (a full round, DMG p.229).
    /// </summary>
    internal IEnumerator ActionCore_UseItem(CharacterController actor, int inventoryIndex, ItemData item, CharacterController target,
        string variant, ActorSource source, IActionDecider decider, ActionOutcome o)
    {
        Legality legal = CheckItemUse(actor, inventoryIndex, item, target, variant, source != ActorSource.Ui);
        if (actor == null || actor.Stats == null || !legal.Ok)
        {
            o.Refuse(actor == null ? Legality.Refused(LegalityBar.Target, "no actor") : legal);
            yield break;
        }
        item = actor.InventoryComp.CharacterInventory.GeneralSlots[inventoryIndex];
        ItemUseKind kind = ItemUseRules.KindOf(item);
        bool administer = variant == ItemUseRules.VariantAdminister;
        string name = actor.Stats.CharacterName;

        // 1. A wand in easy reach is drawn first: a move action that does not provoke, free with a regular move at base
        // attack bonus +1 (PHB p.142; batch 13 step 15, D8, ITM-092). Any other stored item is retrieved first: a move
        // action that provokes (PHB p.141).
        bool retrieved = false;
        if (kind == ItemUseKind.Wand && !actor.IsHoldingRetrievedItem(item))
        {
            if (!DrawWeaponNow(actor, inventoryIndex, item, null, out string drawRefusal))
            {
                o.Refuse(Legality.Refused(LegalityBar.Action, drawRefusal ?? ("could not draw " + item.Name)));
                yield break;
            }
            o.Performed = true;
            retrieved = true;
        }
        else if (ItemUseRules.MustBeInHand(kind) && !actor.IsHoldingRetrievedItem(item))
        {
            IEnumerator retrieve = InlineAction(ActionCore_RetrieveItem(actor, inventoryIndex, item, source, decider, o), 0);
            while (retrieve.MoveNext())
                yield return retrieve.Current;
            if (o.Cancelled || o.CombatEnded || o.ActorDown || !o.Refusal.Ok || !o.Performed)
                yield break;
            retrieved = true;
        }

        // 2. The use's own action.
        ActionCostType cost = GetItemUseCost(actor, item, administer);
        if (!CanPayItemUse(actor, cost))
        {
            if (retrieved)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{name} holds {item.Name}; using it takes {ItemUseRules.Describe(cost)}."));
                yield break;
            }
            o.Refuse(Legality.Refused(LegalityBar.Action, "no action left to use " + item.Name));
            yield break;
        }

        // 3a. A scroll's or wand's aimed spell: the cast core pays and provokes.
        if (ItemUseOpensCast(item))
        {
            UseItemNow(actor, inventoryIndex, item, kind, null, source, target, cost, paid: false, o);
            yield break;
        }

        // 3b. Any other use: paid here, with its own provocation.
        AoOProvokingAction? provocation = ItemUseRules.UseProvocation(kind, cost);
        if (provocation == null)
        {
            if (!PayItemUse(actor, cost))
            {
                o.Refuse(Legality.Refused(LegalityBar.Action, "the use of " + item.Name + " could not be paid"));
                yield break;
            }
            UseItemNow(actor, inventoryIndex, item, kind, administer ? target : null, source, target, cost, paid: true, o);
            yield break;
        }

        SpellData useSpell = kind == ItemUseKind.Scroll ? ResolveScrollSpell(item) : ResolveConsumableSpell(item);
        int spellLevel = kind == ItemUseKind.Potion && item.PotionSpellLevel > 0 ? item.PotionSpellLevel : (useSpell != null ? useSpell.SpellLevel : 0);
        bool interrupted = false;
        string verb = kind == ItemUseKind.Scroll ? "read " : administer ? "administer " : "use ";
        IEnumerator useProvoke = InlineAction(ResolveActionProvocation(actor, provocation.Value, decider, o, verb + item.Name,
            payOnProceed: () => { MarkItemUseAnimating(source); return PayItemUse(actor, cost); },
            afterEachAttack: aoo =>
            {
                // Delegated RAW reading 2026-10-29 (RULES_COVERAGE 4.1, D7): DMG p.229's "successful attack" forces the
                // check when it deals damage, as for a caster (PHB p.70: damage while casting), so a hit that damage
                // reduction or resistance stops entirely forces none. A grappling attack of opportunity is not modelled
                // (ITM-090).
                if (interrupted || aoo == null || !aoo.Hit || aoo.TotalDamage <= 0)
                    return;
                interrupted = !ItemUseConcentrationHolds(actor, item, useSpell, spellLevel, aoo.TotalDamage);
            }), 0);
        while (useProvoke.MoveNext())
            yield return useProvoke.Current;
        if (retrieved && !o.CombatEnded && (o.Cancelled || !o.Refusal.Ok))
        {
            // The retrieval was taken (its move action spent, the item in hand); only the use was called off.
            o.Cancelled = false;
            o.Refusal = Legality.Allowed();
            o.Performed = true;
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{name} holds {item.Name} but does not use it."));
            UpdateAllStatsUI();
            yield break;
        }
        if (o.Cancelled || o.CombatEnded || !o.Refusal.Ok)
            yield break;
        if (CombatEndRules.IsOutOfFight(actor))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Death("💀", $"{name} is struck down before using {item.Name}!"));
            UpdateAllStatsUI();
            o.ActorDown = true;
            o.Performed = true;
            yield break;
        }

        if (interrupted)
        {
            o.Performed = true;
            if (kind == ItemUseKind.Scroll)
            {
                // The spell is lost and the scroll with it, as for a reading lost to its casting checks (DMG p.238).
                ConsumeOneFromStack(actor.InventoryComp.CharacterInventory, inventoryIndex, item);
                actor.ClearRetrievedItem();
                CombatUI?.ShowCombatLog(CombatLogHelper.Interrupted("💥", $"{name}'s reading of {item.Name} is interrupted by damage: the spell is lost and the scroll used up."));
            }
            else
            {
                string what = administer
                    ? $"cannot give {item.Name} to {(target != null && target.Stats != null ? target.Stats.CharacterName : "the creature")}"
                    : $"cannot drink {item.Name}";
                CombatUI?.ShowCombatLog(CombatLogHelper.Interrupted("💥", $"{name} is interrupted by damage and {what} (DMG p.229); it stays in hand."));
            }
            UpdateAllStatsUI();
            yield break;
        }

        UseItemNow(actor, inventoryIndex, item, kind, administer ? target : null, source, target, cost, paid: true, o);
    }

    /// <summary>
    /// The Concentration check a hit forces on a creature using a potion or reading a scroll (DMG p.229: as for casting a
    /// spell; DC 10 + damage + the spell's level, PHB p.69-70). True when it holds. Its bonus is the casting check's
    /// (ConcentrationManager: the caster-level proxy + Con + Combat Casting), not the Concentration skill: a non-caster's
    /// ranks do not count (CHR-085) and Combat Casting adds +4 it should not (SPL-169); both fixes cover this call.
    /// </summary>
    private bool ItemUseConcentrationHolds(CharacterController actor, ItemData item, SpellData spell, int spellLevel, int damage)
    {
        int dc = ConcentrationService.GetDamageDC(damage, Mathf.Max(0, spellLevel));
        ConcentrationCheckResult check = ConcentrationManager.MakeSpellcastingConcentrationCheck(
            actor, dc, ConcentrationCheckType.DamagedWhileCasting, spell, damage);
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("🧠",
            $"{actor.Stats.CharacterName} Concentration to keep using {item.Name}: d20 {check.D20Roll} + {check.TotalBonus} = {check.TotalRoll} vs DC {dc} " +
            (check.Success ? "(success)" : "(failed)")));
        return check.Success;
    }

    /// <summary>The spell of a potion or oil (its consumable spell), or null for a legacy healing item.</summary>
    private static SpellData ResolveConsumableSpell(ItemData item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.ConsumableSpellName))
            return null;
        SpellDatabase.Init();
        return SpellDatabase.GetSpell(item.ConsumableSpellName) ?? SpellDatabase.GetSpellByName(item.ConsumableSpellName);
    }

    /// <summary>
    /// The use step: apply the item (to <paramref name="recipient"/> when a potion is administered) and log the result.
    /// A potion or scroll used up leaves the hand (a wand stays in it). A scroll or wand whose spell opened the PC's
    /// targeting leaves the UI to its cast (CoreOwnsUi); an AI-run one ran through the cast core already
    /// (RunItemCastForAI, at <paramref name="aiTarget"/>). <paramref name="paid"/>: the caller paid <paramref name="cost"/>
    /// already (3b); else (3a) the cast core pays it, and when no cast paid it but the activation was attempted (a Use
    /// Magic Device or caster level check rolled, a scroll mishap) it is paid here: the action is spent (DMG p.213,
    /// p.238; PHB p.86). A use whose activation was attempted counts as performed even when it failed.
    /// </summary>
    private void UseItemNow(CharacterController actor, int inventoryIndex, ItemData item, ItemUseKind kind, CharacterController recipient,
        ActorSource source, CharacterController aiTarget, ActionCostType cost, bool paid, ActionOutcome o)
    {
        bool ok;
        string resultMessage;
        _itemCastForAI = source != ActorSource.Ui;
        _itemCastAiTarget = aiTarget;
        _itemActivationRolled = false;
        _itemCastPaid = false;
        bool activationAttempted;
        bool castPaid;
        try
        {
            ok = ApplyConsumableEffectAndConsume(actor, inventoryIndex, out resultMessage, recipient);
        }
        finally
        {
            activationAttempted = _itemActivationRolled;
            castPaid = _itemCastPaid;
            _itemCastForAI = false;
            _itemCastAiTarget = null;
            _itemActivationRolled = false;
            _itemCastPaid = false;
        }

        // A scroll or wand whose spell entered the PC's targeting: the cast spends the item and pays its action, and the
        // cast's own UI (the targeting) continues the PC's turn.
        if (ok && ConsumableCastAwaitingSpend)
        {
            o.Performed = true;
            CombatUI?.ShowCombatLog(resultMessage);
            o.CoreOwnsUi = true;
            return;
        }

        // 3a with no cast that paid: an attempted activation (a failed check, a mishap) still spends its action.
        bool spent = paid || castPaid;
        if (!spent && (ok || activationAttempted))
        {
            PayItemUse(actor, cost);
            spent = true;
        }

        if (ok)
        {
            o.Performed = true;

            if ((kind == ItemUseKind.Potion || kind == ItemUseKind.Scroll) && ReferenceEquals(actor.RetrievedItem, item))
                actor.ClearRetrievedItem();
            CombatUI?.ShowCombatLog(resultMessage);
            UpdateAllStatsUI();
            return;
        }

        CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{resultMessage}"));
        if (spent && (paid || activationAttempted || castPaid))
        {
            // The action was taken and failed (a failed Use Magic Device or caster level check, a spell lost): performed.
            o.Performed = true;
            UpdateAllStatsUI();
            return;
        }
        o.Refuse(Legality.Refused(LegalityBar.Equipment, resultMessage));
        o.RefusalLogged = true;
    }
}
