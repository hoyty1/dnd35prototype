using System.Collections;
using UnityEngine;

/// <summary>
/// GameManager partial class: the equipment entries' cores (batch 13 step 15, D8; docs/architecture/combat-actions.md),
/// for every creature: drawing a weapon or a wand, sheathing, readying or loosing a shield, picking up and dropping, at
/// their RAW costs (EquipmentActionRules). Before this step only the PC's character-sheet inventory changed equipment, at
/// no cost (ITM-069), the thrown redraw was free for everyone (CHR-021) and no NPC could draw (AI-073). No UI follow-up
/// here: the PC runner's UiAfter does it.
/// </summary>
public partial class GameManager
{
    // ── Draw ──────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The pack index of <paramref name="item"/> (the given index when it holds it), or -1.</summary>
    private static int ResolvePackIndex(Inventory inv, int index, ItemData item)
    {
        if (inv == null || inv.GeneralSlots == null)
            return -1;
        if (index >= 0 && index < inv.GeneralSlots.Length && inv.GeneralSlots[index] != null
            && (item == null || ReferenceEquals(inv.GeneralSlots[index], item)))
            return index;
        if (item == null)
            return -1;
        for (int i = 0; i < inv.GeneralSlots.Length; i++)
            if (ReferenceEquals(inv.GeneralSlots[i], item))
                return i;
        return -1;
    }

    private static EquipSlot PreferredHand(string variant)
        => variant == EquipmentActionRules.VariantLeftHand ? EquipSlot.LeftHand
            : variant == EquipmentActionRules.VariantRightHand ? EquipSlot.RightHand : EquipSlot.None;

    /// <summary>
    /// Why <paramref name="actor"/> cannot draw the pack weapon or wand <paramref name="item"/> (at
    /// <paramref name="index"/>) now, or Allowed: no condition bars handling items (pinned, a wild shape), it is a
    /// weapon or a wand in the pack, a hand is free for a weapon (EquipmentActionRules.HandForDraw), and its draw can be
    /// paid (EquipmentActionRules.DrawPaymentFor: Quick Draw, the free draw of a regular move, the paired Two-Weapon
    /// Fighting draw, else a move action). Pure.
    /// </summary>
    internal Legality CheckDrawWeapon(CharacterController actor, int index, ItemData item, string variant)
    {
        string condition = ItemManipulationConditionRefusal(actor);
        if (condition != null)
            return Legality.Refused(LegalityBar.Condition, condition);
        Inventory inv = EquipmentActionRules.InventoryOf(actor);
        int i = ResolvePackIndex(inv, index, item);
        if (i < 0)
            return Legality.Refused(LegalityBar.Equipment, item != null ? item.Name + " is not in the pack" : "no weapon named to draw");
        item = inv.GeneralSlots[i];
        if (!EquipmentActionRules.IsDrawable(item))
            return Legality.Refused(LegalityBar.Equipment, item.Name + " is not a weapon or a wand");
        if (item.IsWand)
        {
            if (actor.IsHoldingRetrievedItem(item))
                return Legality.Refused(LegalityBar.Equipment, item.Name + " is already in hand");
        }
        else if (EquipmentActionRules.HandForDraw(actor, item, PreferredHand(variant), out string handRefusal) == EquipSlot.None)
        {
            return Legality.Refused(LegalityBar.Equipment, handRefusal);
        }
        DrawPayment payment = EquipmentActionRules.DrawPaymentFor(actor, item);
        return EquipmentActionRules.CanPay(actor, payment)
            ? Legality.Allowed()
            : Legality.Refused(LegalityBar.Action, "no move action left to draw " + item.Name + " (PHB p.142)");
    }

    /// <summary>
    /// Draws <paramref name="item"/> now (D8), for any creature, with no frame and no attack of opportunity (drawing does
    /// not provoke, PHB p.142, Table 8-2): pays its draw, puts a weapon in a free hand, or marks a wand held
    /// (CharacterController.RetrievedItem, as a retrieved item; D7), and logs the action spent. False, with
    /// <paramref name="refusal"/>, when it cannot (nothing spent).
    /// </summary>
    internal bool DrawWeaponNow(CharacterController actor, int index, ItemData item, string variant, out string refusal)
    {
        Legality legal = CheckDrawWeapon(actor, index, item, variant);
        if (!legal.Ok)
        {
            refusal = legal.Reason;
            return false;
        }
        refusal = null;
        Inventory inv = EquipmentActionRules.InventoryOf(actor);
        int i = ResolvePackIndex(inv, index, item);
        item = inv.GeneralSlots[i];
        EquipSlot hand = item.IsWand ? EquipSlot.None : EquipmentActionRules.HandForDraw(actor, item, PreferredHand(variant), out _);
        DrawPayment payment = EquipmentActionRules.DrawPaymentFor(actor, item);
        if (!EquipmentActionRules.Pay(actor, payment, item))
        {
            refusal = "the draw could not be paid";
            return false;
        }
        if (item.IsWand)
            actor.SetRetrievedItem(item);
        else if (!inv.EquipFromInventory(i, hand))
        {
            refusal = item.Name + " could not be drawn into " + hand;
            Debug.LogWarning($"[Equipment] {actor.Stats.CharacterName}: {refusal} after its draw was paid");
            return false;
        }
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("🗡", $"{actor.Stats.CharacterName} draws {item.Name} ({EquipmentActionRules.Describe(payment)})."));
        UpdateAllStatsUI();
        InvalidatePreviewThreats();
        return true;
    }

    /// <summary>The draw entry's core: <see cref="DrawWeaponNow"/>, refused with nothing spent when it cannot draw.</summary>
    internal IEnumerator ActionCore_DrawWeapon(CharacterController actor, int index, ItemData item, string variant, ActionOutcome o)
    {
        if (!DrawWeaponNow(actor, index, item, variant, out string refusal))
        {
            Legality legal = CheckDrawWeapon(actor, index, item, variant);
            o.Refuse(legal.Ok ? Legality.Refused(LegalityBar.Equipment, refusal) : legal);
            yield break;
        }
        o.Performed = true;
    }

    // ── Sheathe ───────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The weapon or retrieved item a sheathe with no item named puts away: the main-hand weapon, else the off hand's, else the item in hand.</summary>
    private static ItemData DefaultHeldItem(CharacterController actor, bool includeShield)
    {
        Inventory inv = EquipmentActionRules.InventoryOf(actor);
        if (inv == null)
            return null;
        if (inv.RightHandSlot != null && (includeShield || !EquipmentActionRules.IsReadiedShield(inv.RightHandSlot)))
            return inv.RightHandSlot;
        if (inv.LeftHandSlot != null && (includeShield || !EquipmentActionRules.IsReadiedShield(inv.LeftHandSlot)))
            return inv.LeftHandSlot;
        return actor.RetrievedItem != null && actor.IsHoldingRetrievedItem(actor.RetrievedItem) ? actor.RetrievedItem : null;
    }

    /// <summary>
    /// Why <paramref name="actor"/> cannot sheathe <paramref name="item"/> (null: what it holds) now, or Allowed: no
    /// condition bars it, the item is in a hand (a readied shield is loosed instead) or is the item it retrieved, and a
    /// move action is left (PHB p.142). Pure.
    /// </summary>
    internal Legality CheckSheatheWeapon(CharacterController actor, ItemData item)
    {
        string condition = ItemManipulationConditionRefusal(actor);
        if (condition != null)
            return Legality.Refused(LegalityBar.Condition, condition);
        item = item ?? DefaultHeldItem(actor, includeShield: false);
        if (item == null)
            return Legality.Refused(LegalityBar.Equipment, "nothing held to sheathe");
        EquipSlot hand = EquipmentActionRules.HandHolding(actor, item);
        if (hand == EquipSlot.None && !actor.IsHoldingRetrievedItem(item))
            return Legality.Refused(LegalityBar.Equipment, item.Name + " is not held");
        if (hand != EquipSlot.None && EquipmentActionRules.IsReadiedShield(item))
            return Legality.Refused(LegalityBar.Equipment, item.Name + " is a readied shield: loosing it is its own move action (PHB p.143)");
        return ActionCosts.CanAfford(actor, ActionCostType.Move)
            ? Legality.Allowed()
            : Legality.Refused(LegalityBar.Action, "no move action left to sheathe " + item.Name + " (PHB p.142)");
    }

    /// <summary>
    /// Sheathes <paramref name="item"/> (D8): a move action that provokes (PHB p.142, Table 8-2). <paramref name="decider"/>
    /// confirms it (the UI's prompt; the AI goes on), the move action is paid, each threatening foe takes its attack of
    /// opportunity (ResolveActionProvocation, SheatheWeapon), then the weapon goes to the pack, or the retrieved item is put
    /// away, unless an attack dropped the actor. Performed once the action is paid.
    /// </summary>
    internal IEnumerator ActionCore_SheatheWeapon(CharacterController actor, ItemData item, ActorSource source, IActionDecider decider, ActionOutcome o)
    {
        Legality legal = CheckSheatheWeapon(actor, item);
        if (!legal.Ok)
        {
            o.Refuse(legal);
            yield break;
        }
        item = item ?? DefaultHeldItem(actor, includeShield: false);
        string name = actor.Stats.CharacterName;
        IEnumerator provoke = InlineAction(ResolveActionProvocation(actor, AoOProvokingAction.SheatheWeapon, decider, o, "sheathe " + item.Name,
            payOnProceed: () => { MarkItemUseAnimating(source); return ActionCosts.TryPay(actor, ActionCostType.Move); }), 0);
        while (provoke.MoveNext())
            yield return provoke.Current;
        if (o.Cancelled || o.CombatEnded || !o.Refusal.Ok)
            yield break;
        o.Performed = true;
        if (CombatEndRules.IsOutOfFight(actor))
        {
            o.ActorDown = true;
            yield break;
        }

        EquipSlot hand = EquipmentActionRules.HandHolding(actor, item);
        if (hand != EquipSlot.None)
        {
            Inventory inv = EquipmentActionRules.InventoryOf(actor);
            if (inv == null || !inv.Unequip(hand))
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{name} could not sheathe {item.Name}."));
                yield break;
            }
        }
        else if (actor.IsHoldingRetrievedItem(item))
        {
            actor.ClearRetrievedItem();
        }
        else
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{name} no longer holds {item.Name}."));
            yield break;
        }
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{name} sheathes {item.Name} (move action)."));
        UpdateAllStatsUI();
        InvalidatePreviewThreats();
    }

    // ── Ready or loose a shield ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>The readied shield <paramref name="actor"/> holds, or null.</summary>
    private static ItemData ReadiedShield(CharacterController actor)
    {
        Inventory inv = EquipmentActionRules.InventoryOf(actor);
        if (inv == null)
            return null;
        if (EquipmentActionRules.IsReadiedShield(inv.LeftHandSlot))
            return inv.LeftHandSlot;
        return EquipmentActionRules.IsReadiedShield(inv.RightHandSlot) ? inv.RightHandSlot : null;
    }

    /// <summary>
    /// Why <paramref name="actor"/> cannot ready (strap on) the pack shield <paramref name="item"/>, or loose (unstrap and
    /// drop) its readied shield (<paramref name="variant"/> "loose", or a shield already readied), or Allowed: no condition
    /// bars it, the shield hand is free to ready one, and its move action, or the free one of a regular move at base attack
    /// bonus +1, can be paid (PHB p.142-143, Table 8-2 note 4). Pure.
    /// </summary>
    internal Legality CheckReadyShield(CharacterController actor, int index, ItemData item, string variant)
    {
        string condition = ItemManipulationConditionRefusal(actor);
        if (condition != null)
            return Legality.Refused(LegalityBar.Condition, condition);
        Inventory inv = EquipmentActionRules.InventoryOf(actor);
        if (inv == null)
            return Legality.Refused(LegalityBar.Equipment, "no inventory");
        ItemData readied = ReadiedShield(actor);
        bool loose = variant == EquipmentActionRules.VariantLoose || (item != null && ReferenceEquals(item, readied));
        if (loose)
        {
            if (readied == null)
                return Legality.Refused(LegalityBar.Equipment, "no readied shield to loose");
        }
        else
        {
            int i = ResolvePackIndex(inv, index, item);
            if (i < 0 || !inv.GeneralSlots[i].IsShield)
                return Legality.Refused(LegalityBar.Equipment, item != null ? item.Name + " is not a shield in the pack" : "no shield named to ready");
            if (readied != null)
                return Legality.Refused(LegalityBar.Equipment, readied.Name + " is already readied");
            if (inv.LeftHandSlot != null || (inv.RightHandSlot != null && inv.RightHandSlot.IsTwoHanded))
                return Legality.Refused(LegalityBar.Equipment, "the shield hand is not free (sheathe or drop what it holds first)");
            if (!inv.GeneralSlots[i].CanEquipIn(EquipSlot.LeftHand))
                return Legality.Refused(LegalityBar.Equipment, inv.GeneralSlots[i].Name + " cannot be readied on the arm");
        }
        DrawPayment payment = EquipmentActionRules.ShieldPaymentFor(actor);
        return EquipmentActionRules.CanPay(actor, payment)
            ? Legality.Allowed()
            : Legality.Refused(LegalityBar.Action, "no move action left to " + (loose ? "loose" : "ready") + " a shield (PHB p.142)");
    }

    /// <summary>
    /// Readies the pack shield <paramref name="item"/> or looses the readied one (D8): a move action that does not provoke,
    /// free with a regular move at base attack bonus +1 (PHB p.142-143, Table 8-2 note 4). A loosed shield is dropped in the
    /// creature's square ("unstrapping and dropping", PHB p.143). Performed when done.
    /// </summary>
    internal IEnumerator ActionCore_ReadyShield(CharacterController actor, int index, ItemData item, string variant, ActionOutcome o)
    {
        Legality legal = CheckReadyShield(actor, index, item, variant);
        if (!legal.Ok)
        {
            o.Refuse(legal);
            yield break;
        }
        Inventory inv = EquipmentActionRules.InventoryOf(actor);
        ItemData readied = ReadiedShield(actor);
        bool loose = variant == EquipmentActionRules.VariantLoose || (item != null && ReferenceEquals(item, readied));
        DrawPayment payment = EquipmentActionRules.ShieldPaymentFor(actor);
        if (!EquipmentActionRules.Pay(actor, payment, null))
        {
            o.Refuse(Legality.Refused(LegalityBar.Action, "the shield's action could not be paid"));
            yield break;
        }
        string name = actor.Stats.CharacterName;
        if (loose)
        {
            EquipSlot hand = EquipmentActionRules.HandHolding(actor, readied);
            if (!TryDropEquippedHeldItemToGround(actor, hand, out ItemData dropped, out _, out string feedback))
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", feedback));
                o.Performed = true;
                yield break;
            }
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("🛡", $"{name} looses {dropped.Name} and drops it ({EquipmentActionRules.Describe(payment)})."));
        }
        else
        {
            int i = ResolvePackIndex(inv, index, item);
            ItemData shield = inv.GeneralSlots[i];
            if (!inv.EquipFromInventory(i, EquipSlot.LeftHand))
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{name} could not ready {shield.Name}."));
                o.Performed = true;
                yield break;
            }
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("🛡", $"{name} readies {shield.Name} ({EquipmentActionRules.Describe(payment)})."));
        }
        o.Performed = true;
        UpdateAllStatsUI();
        InvalidatePreviewThreats();
    }

    // ── Pick up ───────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The ground item a pick-up aims at: <paramref name="item"/> in <paramref name="cell"/> (or anywhere in reach when no
    /// cell is named), else the first in reach whose id is <paramref name="itemId"/>, else the first in reach (the actor's
    /// square, then the adjacent ones). False when none is.
    /// </summary>
    private bool TryResolvePickUp(CharacterController actor, Vector2Int? cell, ItemData item, string itemId, out SquareCell foundCell, out ItemData found)
    {
        foundCell = null;
        found = null;
        if (!TryGetAvailablePickUpItems(actor, out System.Collections.Generic.List<PickUpGroundItemOption> options))
            return false;
        foreach (PickUpGroundItemOption option in options)
        {
            if (option.Cell == null || option.Item == null)
                continue;
            if (cell.HasValue && option.Cell.Coords != cell.Value)
                continue;
            bool match = item != null ? ReferenceEquals(option.Item, item)
                : string.IsNullOrEmpty(itemId) || option.Item.Id == itemId;
            if (!match)
                continue;
            foundCell = option.Cell;
            found = option.Item;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Why <paramref name="actor"/> cannot pick the item up now, or Allowed: the shared gate (GetPickUpItemDisabledReason:
    /// not pinned, an item in reach, a move action or a standard action to trade) and the item named in reach. Pure.
    /// </summary>
    internal Legality CheckPickUpItem(CharacterController actor, Vector2Int? cell, ItemData item, string itemId)
    {
        string reason = GetPickUpItemDisabledReason(actor);
        if (!string.IsNullOrEmpty(reason))
            return Legality.Refused(reason.StartsWith("No move") ? LegalityBar.Action : LegalityBar.Equipment, reason);
        string condition = ItemManipulationConditionRefusal(actor);
        if (condition != null)
            return Legality.Refused(LegalityBar.Condition, condition);
        if (!TryResolvePickUp(actor, cell, item, itemId, out _, out _))
            return Legality.Refused(LegalityBar.Equipment, (item != null ? item.Name : itemId ?? "that item") + " is not on or beside the creature's square");
        return ActionCosts.CanAfford(actor, ActionCostType.Move)
            ? Legality.Allowed()
            : Legality.Refused(LegalityBar.Action, "no move action left to pick up an item (PHB p.142)");
    }

    /// <summary>
    /// Picks up a ground item in reach (D8): a move action that provokes (PHB p.142, Table 8-2). <paramref name="decider"/>
    /// confirms it, the move action is paid, each threatening foe takes its attack of opportunity
    /// (ResolveActionProvocation, PickUpItem), then the item is taken (TryPickUpGroundItem: a greased item may slip, PHB
    /// p.237): a weapon into a free hand when one is free, anything else into the pack (delegated RAW reading 2026-10-29;
    /// the hand budget is ITM-089). Performed once the action is paid.
    /// </summary>
    internal IEnumerator ActionCore_PickUpItem(CharacterController actor, Vector2Int? cell, ItemData item, string itemId, ActorSource source,
        IActionDecider decider, ActionOutcome o)
    {
        Legality legal = CheckPickUpItem(actor, cell, item, itemId);
        if (!legal.Ok)
        {
            o.Refuse(legal);
            yield break;
        }
        TryResolvePickUp(actor, cell, item, itemId, out SquareCell groundCell, out ItemData groundItem);
        string name = actor.Stats.CharacterName;
        IEnumerator provoke = InlineAction(ResolveActionProvocation(actor, AoOProvokingAction.PickUpItem, decider, o, "pick up " + groundItem.Name,
            payOnProceed: () => { MarkItemUseAnimating(source); return ActionCosts.TryPay(actor, ActionCostType.Move); }), 0);
        while (provoke.MoveNext())
            yield return provoke.Current;
        if (o.Cancelled || o.CombatEnded || !o.Refusal.Ok)
            yield break;
        o.Performed = true;
        if (CombatEndRules.IsOutOfFight(actor))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Death("💀", $"{name} is struck down before picking up {groundItem.Name}!"));
            UpdateAllStatsUI();
            o.ActorDown = true;
            yield break;
        }

        if (!TryPickUpGroundItem(actor, groundCell, groundItem, out string pickupMsg))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", pickupMsg));
            UpdateAllStatsUI();
            yield break;
        }
        CombatUI?.ShowCombatLog(pickupMsg.TrimEnd('.') + " (move action).");
        // A weapon goes into a free hand (held, as it was picked up); anything else, or a weapon with no hand free, to the pack.
        if (groundItem.IsWeapon)
        {
            Inventory inv = EquipmentActionRules.InventoryOf(actor);
            EquipSlot hand = EquipmentActionRules.HandForDraw(actor, groundItem, EquipSlot.None, out _);
            int i = ResolvePackIndex(inv, -1, groundItem);
            if (hand != EquipSlot.None && i >= 0 && inv.EquipFromInventory(i, hand))
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{name} holds {groundItem.Name}."));
        }
        UpdateAllStatsUI();
        InvalidatePreviewThreats();
    }

    // ── Drop ──────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Why <paramref name="actor"/> cannot drop <paramref name="item"/> (null: what it holds; <paramref name="variant"/>
    /// "all-held": everything it holds) now, or Allowed: the shared gate (GetDropEquippedItemDisabledReason: not pinned,
    /// something held that can be dropped) and the item held. A readied shield is loosed, not dropped (PHB p.143). Pure.
    /// </summary>
    internal Legality CheckDropItem(CharacterController actor, ItemData item, string variant)
    {
        string reason = GetDropEquippedItemDisabledReason(actor);
        if (!string.IsNullOrEmpty(reason))
            return Legality.Refused(LegalityBar.Equipment, reason);
        if (variant == EquipmentActionRules.VariantAllHeld || item == null)
            return Legality.Allowed();
        EquipSlot hand = EquipmentActionRules.HandHolding(actor, item);
        if (hand == EquipSlot.None && !actor.IsHoldingRetrievedItem(item))
            return Legality.Refused(LegalityBar.Equipment, item.Name + " is not held");
        if (hand != EquipSlot.None && EquipmentActionRules.IsReadiedShield(item))
            return Legality.Refused(LegalityBar.Equipment, item.Name + " is a readied shield: loosing it is a move action (PHB p.143)");
        return Legality.Allowed();
    }

    /// <summary>
    /// Drops <paramref name="item"/> (null: what it holds; variant "all-held": every weapon and item it holds, its readied
    /// shield kept) in the creature's square (D8): a free action that provokes nothing (PHB p.141, Table 8-2).
    /// <paramref name="why"/> goes in the log line (a panicked creature's drop, PHB p.311). Returns the number dropped.
    /// </summary>
    internal int DropItemNow(CharacterController actor, ItemData item, string variant, string why = null)
    {
        if (!CheckDropItem(actor, item, variant).Ok)
            return 0;
        var drops = new System.Collections.Generic.List<ItemData>();
        if (variant == EquipmentActionRules.VariantAllHeld)
        {
            Inventory inv = EquipmentActionRules.InventoryOf(actor);
            if (inv != null && inv.RightHandSlot != null && !EquipmentActionRules.IsReadiedShield(inv.RightHandSlot))
                drops.Add(inv.RightHandSlot);
            if (inv != null && inv.LeftHandSlot != null && !EquipmentActionRules.IsReadiedShield(inv.LeftHandSlot))
                drops.Add(inv.LeftHandSlot);
            if (actor.RetrievedItem != null && actor.IsHoldingRetrievedItem(actor.RetrievedItem))
                drops.Add(actor.RetrievedItem);
        }
        else
        {
            ItemData one = item ?? DefaultHeldItem(actor, includeShield: false);
            if (one != null)
                drops.Add(one);
        }

        string name = actor.Stats.CharacterName;
        string suffix = string.IsNullOrEmpty(why) ? "free action" : why + "; a free action";
        int count = 0;
        foreach (ItemData d in drops)
        {
            EquipSlot hand = EquipmentActionRules.HandHolding(actor, d);
            if (hand != EquipSlot.None)
            {
                if (!TryDropEquippedHeldItemToGround(actor, hand, out _, out _, out string feedback))
                {
                    CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", feedback));
                    continue;
                }
            }
            else
            {
                Inventory inv = EquipmentActionRules.InventoryOf(actor);
                SquareCell cell = GetCharacterCurrentCell(actor);
                if (inv == null || cell == null || !inv.RemoveItem(d))
                    continue;
                actor.ClearRetrievedItem();
                cell.AddGroundItem(d);
            }
            count++;
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("⬇", $"{name} drops {d.Name} ({suffix}, no attack of opportunity)."));
        }
        if (count > 0)
        {
            UpdateAllStatsUI();
            InvalidatePreviewThreats();
        }
        return count;
    }

    /// <summary>The drop entry's core: <see cref="DropItemNow"/>, refused with nothing dropped when it cannot.</summary>
    internal IEnumerator ActionCore_DropItem(CharacterController actor, ItemData item, string variant, ActionOutcome o)
    {
        Legality legal = CheckDropItem(actor, item, variant);
        if (!legal.Ok)
        {
            o.Refuse(legal);
            yield break;
        }
        o.Performed = DropItemNow(actor, item, variant, variant == EquipmentActionRules.VariantAllHeld ? "everything it holds" : null) > 0;
        if (!o.Performed)
            o.Refuse(Legality.Refused(LegalityBar.Equipment, "nothing was dropped"));
    }
}
