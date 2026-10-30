using System.Collections;

/// <summary>
/// Use an item (status Extracted; batch 13 step 14, D7, ITM-005, AI-054): GameManager.ActionCore_UseItem at the RAW costs
/// of ItemUseRules (a stored item retrieved first; a potion a standard action that provokes; a scroll its casting time,
/// provoking as casting; a wand a standard action that does not provoke; variant "administer" a full-round potion for an
/// unconscious creature, DMG p.229). The context names the pack item (<see cref="ActionContext.Item"/> and
/// <see cref="ActionContext.InventoryIndex"/>) and, for an AI scroll or wand cast or an administered potion, the
/// <see cref="ActionContext.Target"/>. The PC's Use Item panel and the character sheet (GameManager.TryUseConsumableFromInventory,
/// through RunPlayerAction, UiAfter ShowChoices) and the AI and the harness (RunAIAction) run it alike.
/// </summary>
public sealed class UseItemActionExecutor : IActionExecutor
{
    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null)
            return Legality.Refused(LegalityBar.Other, "no game");
        return ItemLegality.FromReason(gm.GetUseItemDisabledReason(actor));
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        Legality start = CanStart(ctx.Actor);
        if (!start.Ok)
            return start;
        return ExecutorUtil.Gm.CheckItemUse(ctx.Actor, ctx.InventoryIndex, ctx.Item, ctx.Target, ctx.Variant, ctx.Source != ActorSource.Ui);
    }

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null || ctx == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Other, gm == null ? "no game" : "no context"));
            yield break;
        }
        yield return gm.ActionCore_UseItem(ctx.Actor, ctx.InventoryIndex, ctx.Item, ctx.Target, ctx.Variant, ctx.Source, ctx.Decider, o);
    }
}

/// <summary>
/// Retrieve a stored item (status New; batch 13 step 14, D7): GameManager.ActionCore_RetrieveItem, a move action that
/// provokes (PHB p.141, Table 8-2); the item is then in hand (CharacterController.RetrievedItem). The use-item entry runs
/// the same core first for an item not in hand; the AI and the harness (RunAIAction) can run it on its own (a charmed
/// creature readying a potion for its unconscious charmer). The PC panel has no separate button: its Use Item retrieves
/// first.
/// </summary>
public sealed class RetrieveItemActionExecutor : IActionExecutor
{
    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null)
            return Legality.Refused(LegalityBar.Other, "no game");
        return ItemLegality.FromReason(gm.GetUseItemDisabledReason(actor));
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        Legality start = CanStart(ctx.Actor);
        if (!start.Ok)
            return start;
        GameManager gm = ExecutorUtil.Gm;
        string refusal = gm.GetUseItemRefusal(ctx.Actor, ctx.InventoryIndex, ctx.Item);
        if (refusal != null)
            return Legality.Refused(LegalityBar.Equipment, refusal);
        ItemData item = ctx.Actor.InventoryComp.CharacterInventory.GeneralSlots[ctx.InventoryIndex];
        if (!ItemUseRules.MustBeInHand(ItemUseRules.KindOf(item)))
            return Legality.Refused(LegalityBar.Equipment, item.Name + " is worn, not stored");
        if (ctx.Actor.IsHoldingRetrievedItem(item))
            return Legality.Refused(LegalityBar.Equipment, item.Name + " is already in hand");
        ActionEconomy a = ctx.Actor.Actions;
        return a != null && (a.HasMoveAction || a.CanConvertStandardToMove)
            ? Legality.Allowed()
            : Legality.Refused(LegalityBar.Action, "no move action left (PHB p.141)");
    }

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null || ctx == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Other, gm == null ? "no game" : "no context"));
            yield break;
        }
        yield return gm.ActionCore_RetrieveItem(ctx.Actor, ctx.InventoryIndex, ctx.Item, ctx.Source, ctx.Decider, o);
    }
}

/// <summary>The legality of an item entry's disabled reason (GameManager.GetUseItemDisabledReason): the action bar or a condition.</summary>
internal static class ItemLegality
{
    internal static Legality FromReason(string reason)
    {
        if (reason == null)
            return Legality.Allowed();
        if (reason.StartsWith("No ") && reason.Contains("action"))
            return Legality.Refused(LegalityBar.Action, reason);
        if (reason.Contains("usable item"))
            return Legality.Refused(LegalityBar.Equipment, reason);
        return Legality.Refused(LegalityBar.Condition, reason);
    }
}
