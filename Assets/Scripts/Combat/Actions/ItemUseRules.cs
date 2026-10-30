/// <summary>What kind of use a pack item has (<see cref="ItemUseRules.KindOf"/>).</summary>
public enum ItemUseKind
{
    /// <summary>A potion or oil, or another drink-like consumable (a legacy healing item): drunk or applied (DMG p.229).</summary>
    Potion,
    /// <summary>A scroll: spell completion (DMG p.213, p.237).</summary>
    Scroll,
    /// <summary>A wand: spell trigger (DMG p.213, p.245).</summary>
    Wand,
    /// <summary>A staff: spell trigger (DMG p.213, p.243).</summary>
    Staff,
    /// <summary>A ring or wondrous item with an activated power: command word or use-activated (DMG p.213).</summary>
    Activated
}

/// <summary>
/// The action rules of using an item (batch 13 step 14, decision D7; docs/designs/action_catalog_batch13.md), one rule
/// for every creature: the use-item and retrieve-item entries' cores (GameManager.ActionCore_UseItem,
/// ActionCore_RetrieveItem) read it for PCs and NPCs alike. Pure: no side effects.
///
/// RAW applied:
/// - PHB p.141-142, Table 8-2: retrieving a stored item is a move action that provokes; drinking a potion or applying an
///   oil is a standard action that provokes. DMG p.229 (Potions, Activation) says the same and adds that a hit by an
///   attack of opportunity forces a Concentration check as for casting a spell, a failure meaning the potion is not drunk
///   (GameManager.ActionCore_UseItem), and that administering a potion to an unconscious creature is a full-round action.
/// - DMG p.213 (Activation): a scroll (spell completion) takes the spell's casting time and provokes as casting does; a
///   wand or staff (spell trigger) does not provoke; activating a magic item is otherwise a standard action, and the
///   casting time of a spell is the time to activate the same power in an item (CastActionRules.CastingAction holds the
///   scroll's and wand's cast cost). A wand or staff must be held to be used (DMG p.245, p.243).
/// Delegated RAW readings (2026-10-29, docs/systems/RULES_COVERAGE.md 4.1): a retrieved item stays in hand
/// (CharacterController.RetrievedItem) until it is used up, stowed, dropped or the encounter ends; a scroll of a
/// free-action spell is free, provokes nothing and counts as the round's free-action spell (Table 8-2, as a quickened
/// spell); administering a potion provokes as using one does (DMG p.229 says using a potion provokes and names no
/// exception for administering it); a wand in easy reach is drawn, not retrieved (batch 13 step 15, D8, ITM-092: a move
/// action that does not provoke, free with a regular move at base attack bonus +1; EquipmentActionRules).
/// A ring or wondrous item's activated power is worn, not stored: no retrieval, a standard action, no provocation.
/// </summary>
public static class ItemUseRules
{
    /// <summary>The use-item entry's variant that administers a potion to the context's (unconscious) target (DMG p.229).</summary>
    public const string VariantAdminister = "administer";

    /// <summary>The kind of use <paramref name="item"/> has.</summary>
    public static ItemUseKind KindOf(ItemData item)
    {
        if (item == null)
            return ItemUseKind.Activated;
        if (item.IsScroll)
            return ItemUseKind.Scroll;
        if (item.IsWand)
            return ItemUseKind.Wand;
        if (item.IsStaff)
            return ItemUseKind.Staff;
        if ((item.IsRing && item.HasActiveRingAbility) || (item.IsWondrous && item.HasActiveWondrousAbility))
            return ItemUseKind.Activated;
        return ItemUseKind.Potion;
    }

    /// <summary>True when an item of <paramref name="kind"/> must be in hand to be used, so a stored one is retrieved first.</summary>
    public static bool MustBeInHand(ItemUseKind kind) => kind != ItemUseKind.Activated;

    /// <summary>The action of a scroll's or wand's cast (CastActionRules.CastingAction) as an action-economy cost.</summary>
    public static ActionCostType FromCastingAction(CastActionCost cost)
    {
        switch (cost)
        {
            case CastActionCost.Free: return ActionCostType.Free;
            case CastActionCost.FullRound: return ActionCostType.FullRound;
            default: return ActionCostType.Standard;
        }
    }

    /// <summary>
    /// The attack-of-opportunity kind using an item of <paramref name="kind"/> at <paramref name="cost"/> provokes, or
    /// null: a potion or oil (drunk, applied or administered) provokes; a scroll provokes as casting does, unless it is a
    /// free action; a wand, staff or activated item does not (DMG p.213, p.229).
    /// </summary>
    public static AoOProvokingAction? UseProvocation(ItemUseKind kind, ActionCostType cost)
    {
        switch (kind)
        {
            case ItemUseKind.Potion:
                return AoOProvokingAction.DrinkPotion;
            case ItemUseKind.Scroll:
                return cost == ActionCostType.Free ? (AoOProvokingAction?)null : AoOProvokingAction.UseScroll;
            default:
                return null;
        }
    }

    /// <summary>A short label of <paramref name="cost"/> for the log ("a standard action", "a full-round action", "a free action").</summary>
    public static string Describe(ActionCostType cost)
    {
        switch (cost)
        {
            case ActionCostType.Free: return "a free action";
            case ActionCostType.FullRound: return "a full-round action";
            case ActionCostType.Move: return "a move action";
            default: return "a standard action";
        }
    }
}
