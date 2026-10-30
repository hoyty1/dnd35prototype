/// <summary>How a draw (or a shield readied or loosed) is paid (<see cref="EquipmentActionRules.DrawPaymentFor"/>).</summary>
public enum DrawPayment
{
    /// <summary>A move action (PHB p.142, Table 8-2).</summary>
    MoveAction,
    /// <summary>Free: Quick Draw (PHB p.98), weapons only.</summary>
    QuickDraw,
    /// <summary>Free: combined with a regular move at base attack bonus +1 or more (PHB p.142, Table 8-2 note 4).</summary>
    WithMove,
    /// <summary>Free: the second light or one-handed weapon a Two-Weapon Fighting creature draws in the time of one (PHB p.142, note 4).</summary>
    Paired,
}

/// <summary>
/// The rules of the equipment entries (batch 13 step 15, decision D8; docs/designs/action_catalog_batch13.md): drawing,
/// sheathing, readying or loosing a shield, picking up and dropping, one rule for every creature. The entries' cores
/// (GameManager.EquipmentActions.cs) read it for PCs and NPCs alike. Pure, except <see cref="Pay"/>.
///
/// RAW applied (PHB p.141-143 with the Table 8-2 notes; Quick Draw p.98):
/// - drawing a weapon, or a weapon-like object in easy reach such as a wand, is a move action that does not provoke; with a
///   base attack bonus of +1 or more it is a free action combined with a regular move (note 4); with Quick Draw a weapon is
///   drawn as a free action; with Two-Weapon Fighting two light or one-handed weapons are drawn in the time of one;
/// - sheathing a weapon (putting it away for a free hand) is a move action that provokes;
/// - readying (strapping on) or loosing (unstrapping and dropping) a shield is a move action that does not provoke, free
///   with a regular move at base attack bonus +1 (note 4); dropping a carried but not worn shield is free;
/// - picking up an item is a move action that provokes; dropping a held item is a free action;
/// - only Quick Draw lets a creature throw at its full rate of attacks: each next thrown weapon is a draw (p.98).
/// Delegated RAW readings (2026-10-29, docs/systems/RULES_COVERAGE.md 4.1):
/// - the free draw "combined with a regular move" is one draw per move action spent moving
///   (<see cref="ActionEconomy.FreeDrawsWithMove"/>); a 5-foot step is not a move action and a charge or a withdraw is
///   not a regular move, so neither earns one;
/// - every weapon and wand a creature carries is in easy reach (the game has no pack location), so it is drawn, not
///   retrieved; Quick Draw names weapons only, so a wand is never drawn by it;
/// - a shield in the left hand is readied (strapped on): dropping it is loosing it, a move action; a picked-up weapon goes
///   into a free hand when one is free, else into the pack (the hand budget is ITM-089);
/// - the paired Two-Weapon Fighting draw is the second light or one-handed weapon drawn right after the first, before
///   any other action is spent, into the other hand while the first is still held (<see cref="IsPairedDrawOpen"/>).
/// Owner question Q5's default (2026-10-29): in combat the armor and every other worn slot stay locked (no multi-round
/// actions; ITM-095) and only an item in hand can be dropped.
/// </summary>
public static class EquipmentActionRules
{
    /// <summary>The ready-shield entry's variant that looses (unstraps and drops) the readied shield.</summary>
    public const string VariantLoose = "loose";

    /// <summary>The drop entry's variant that drops everything held (a panicked creature, PHB p.311).</summary>
    public const string VariantAllHeld = "all-held";

    /// <summary>The draw entry's variants naming the preferred hand (the inventory slot the PC dropped the weapon on).</summary>
    public const string VariantRightHand = "right-hand";
    public const string VariantLeftHand = "left-hand";

    public static Inventory InventoryOf(CharacterController actor)
        => actor != null && actor.InventoryComp != null ? actor.InventoryComp.CharacterInventory : null;

    public static bool HasQuickDraw(CharacterController actor)
        => actor != null && actor.Stats != null && FeatManager.HasQuickDraw(actor.Stats);

    public static bool HasTwoWeaponFighting(CharacterController actor)
        => actor != null && actor.Stats != null && actor.Stats.HasFeat("Two-Weapon Fighting");

    /// <summary>A weapon, or a weapon-like object carried in easy reach (a wand, PHB p.142).</summary>
    public static bool IsDrawable(ItemData item) => item != null && (item.IsWeapon || item.IsWand);

    /// <summary>A weapon Two-Weapon Fighting draws two of at once: light or one-handed (PHB p.142, note 4).</summary>
    public static bool IsPairable(ItemData item) => item != null && item.IsWeapon && !item.IsTwoHanded;

    /// <summary>
    /// How drawing <paramref name="item"/> is paid now: Quick Draw (a weapon), the paired Two-Weapon Fighting draw, the free
    /// draw of a regular move at base attack bonus +1, else a move action.
    /// </summary>
    public static DrawPayment DrawPaymentFor(CharacterController actor, ItemData item)
    {
        if (actor == null || actor.Stats == null)
            return DrawPayment.MoveAction;
        bool weapon = item != null && item.IsWeapon;
        if (weapon && HasQuickDraw(actor))
            return DrawPayment.QuickDraw;
        ActionEconomy a = actor.Actions;
        if (weapon && IsPairedDrawOpen(actor) && IsPairable(item))
            return DrawPayment.Paired;
        return WithMoveOrMoveAction(actor);
    }

    /// <summary>
    /// The paired Two-Weapon Fighting draw is still open (PHB p.142, note 4: two weapons "in the time" of one): the first
    /// weapon was drawn by the last action this creature took (<see cref="ActionEconomy.PairedDrawAvailable"/>, cleared by
    /// any action spent since) and is still in its hand, so the second goes into the other hand.
    /// </summary>
    public static bool IsPairedDrawOpen(CharacterController actor)
    {
        ActionEconomy a = actor != null ? actor.Actions : null;
        return a != null && a.PairedDrawAvailable && a.PairedDrawFirst != null && HandHolding(actor, a.PairedDrawFirst) != EquipSlot.None;
    }

    /// <summary>How readying or loosing a shield is paid now: free with a regular move at base attack bonus +1 (note 4), else a move action.</summary>
    public static DrawPayment ShieldPaymentFor(CharacterController actor) => WithMoveOrMoveAction(actor);

    private static DrawPayment WithMoveOrMoveAction(CharacterController actor)
    {
        ActionEconomy a = actor != null ? actor.Actions : null;
        if (a != null && a.FreeDrawsWithMove > 0 && actor.Stats != null && actor.Stats.BaseAttackBonus >= 1)
            return DrawPayment.WithMove;
        return DrawPayment.MoveAction;
    }

    /// <summary>The action <paramref name="payment"/> costs.</summary>
    public static ActionCostType CostOf(DrawPayment payment) => payment == DrawPayment.MoveAction ? ActionCostType.Move : ActionCostType.Free;

    /// <summary>The draw entry's cost for <paramref name="item"/> (its CostFor).</summary>
    public static ActionCostType DrawCost(CharacterController actor, ItemData item) => CostOf(DrawPaymentFor(actor, item));

    /// <summary>The ready-shield entry's cost (its CostFor).</summary>
    public static ActionCostType ShieldCost(CharacterController actor) => CostOf(ShieldPaymentFor(actor));

    public static bool CanPay(CharacterController actor, DrawPayment payment)
        => payment != DrawPayment.MoveAction || ActionCosts.CanAfford(actor, ActionCostType.Move);

    /// <summary>
    /// Pays <paramref name="payment"/> for drawing <paramref name="item"/> (or readying a shield, item null): the move action
    /// (or the standard action traded for one), or the free source it uses. A Two-Weapon Fighting draw of a light or
    /// one-handed weapon then leaves the paired second draw available. False when it cannot be paid (nothing spent).
    /// </summary>
    public static bool Pay(CharacterController actor, DrawPayment payment, ItemData item)
    {
        if (actor == null || actor.Actions == null || !CanPay(actor, payment))
            return false;
        ActionEconomy a = actor.Actions;
        switch (payment)
        {
            case DrawPayment.MoveAction:
                if (!ActionCosts.TryPay(actor, ActionCostType.Move))
                    return false;
                break;
            case DrawPayment.WithMove:
                a.FreeDrawsWithMove--;
                break;
            case DrawPayment.Paired:
                a.ClearPairedDraw();
                return true;
        }
        if (item != null && IsPairable(item) && HasTwoWeaponFighting(actor))
        {
            a.PairedDrawAvailable = true;
            a.PairedDrawFirst = item;
        }
        return true;
    }

    /// <summary>The log's words for <paramref name="payment"/>.</summary>
    public static string Describe(DrawPayment payment)
    {
        switch (payment)
        {
            case DrawPayment.QuickDraw: return "free action, Quick Draw";
            case DrawPayment.WithMove: return "free action with its move";
            case DrawPayment.Paired: return "free, drawn with the first by Two-Weapon Fighting";
            default: return "move action";
        }
    }

    /// <summary>
    /// The hand a drawn weapon goes into: both hands free for a two-handed weapon (the right hand). A named hand
    /// (<paramref name="preferred"/> RightHand or LeftHand: the inventory slot the PC clicked) is strict: that hand, or
    /// <see cref="EquipSlot.None"/> with the refusal naming what it holds (no silent move to the other hand). With no hand
    /// named (the AI, the harness): the right hand, else the left. <see cref="EquipSlot.None"/> with
    /// <paramref name="refusal"/> when no hand is free.
    /// </summary>
    public static EquipSlot HandForDraw(CharacterController actor, ItemData item, EquipSlot preferred, out string refusal)
    {
        refusal = null;
        Inventory inv = InventoryOf(actor);
        if (inv == null || item == null)
        {
            refusal = "no inventory";
            return EquipSlot.None;
        }
        bool rightFree = inv.RightHandSlot == null;
        bool leftFree = inv.LeftHandSlot == null;
        if (item.IsTwoHanded)
        {
            if (rightFree && leftFree && item.CanEquipIn(EquipSlot.RightHand))
                return EquipSlot.RightHand;
            refusal = item.Name + " needs both hands free (sheathe or drop what is held first)";
            return EquipSlot.None;
        }
        // A two-handed weapon in one hand fills both (Inventory.RecalculateStats' rule).
        bool twoHanderHeld = (inv.RightHandSlot != null && inv.RightHandSlot.IsTwoHanded) || (inv.LeftHandSlot != null && inv.LeftHandSlot.IsTwoHanded);
        if (twoHanderHeld)
        {
            refusal = "both hands hold a two-handed weapon (sheathe or drop it first)";
            return EquipSlot.None;
        }
        preferred = ItemData.ResolveHandAlias(preferred);
        if (preferred == EquipSlot.RightHand || preferred == EquipSlot.LeftHand)
        {
            ItemData inHand = preferred == EquipSlot.RightHand ? inv.RightHandSlot : inv.LeftHandSlot;
            if (inHand != null)
            {
                refusal = inHand.Name + " is in that hand: sheathe it (move action, provokes) or drop it (free) first";
                return EquipSlot.None;
            }
            if (!item.CanEquipIn(preferred))
            {
                refusal = item.Name + " cannot go in that hand";
                return EquipSlot.None;
            }
            return preferred;
        }
        if (rightFree && item.CanEquipIn(EquipSlot.RightHand))
            return EquipSlot.RightHand;
        if (leftFree && item.CanEquipIn(EquipSlot.LeftHand))
            return EquipSlot.LeftHand;
        refusal = "no free hand for " + item.Name + " (sheathe or drop what is held first)";
        return EquipSlot.None;
    }

    /// <summary>The hand that holds <paramref name="item"/>, or None.</summary>
    public static EquipSlot HandHolding(CharacterController actor, ItemData item)
    {
        Inventory inv = InventoryOf(actor);
        if (inv == null || item == null)
            return EquipSlot.None;
        if (ReferenceEquals(inv.RightHandSlot, item))
            return EquipSlot.RightHand;
        if (ReferenceEquals(inv.LeftHandSlot, item))
            return EquipSlot.LeftHand;
        return EquipSlot.None;
    }

    /// <summary>A readied shield: a shield held in a hand slot is strapped on (dropping it is loosing it, PHB p.143).</summary>
    public static bool IsReadiedShield(ItemData item) => item != null && item.IsShield;

    /// <summary>
    /// The reason an equipment slot other than a hand cannot change in combat (ITM-095): armor takes rounds to don or
    /// remove (PHB Table 7-7, p.123) and no action spans rounds here; putting on or taking off another worn item (a ring, a
    /// cloak) is not built either (DMG p.213: a standard action after retrieving it).
    /// </summary>
    public static string WornSlotLockedReason(EquipSlot slot, ItemData item)
    {
        bool armor = slot == EquipSlot.Armor || slot == EquipSlot.ArmorRobe || (item != null && item.IsArmor);
        return armor
            ? "armor cannot be donned or removed in combat: it takes rounds (PHB Table 7-7, p.123) and no action spans rounds yet (ITM-095)"
            : "worn items cannot be put on or taken off in combat yet (DMG p.213; ITM-095)";
    }

    /// <summary>True for the slots a creature holds things in (the entries handle these; every other slot is worn).</summary>
    public static bool IsHandSlot(EquipSlot slot)
    {
        EquipSlot s = ItemData.ResolveHandAlias(slot);
        return s == EquipSlot.RightHand || s == EquipSlot.LeftHand;
    }
}
