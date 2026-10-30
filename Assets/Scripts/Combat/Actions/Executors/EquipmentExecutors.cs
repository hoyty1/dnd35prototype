using System.Collections;

/// <summary>
/// Draw a weapon, or a wand in easy reach (status New; batch 13 step 15, D8, ITM-069, ITM-092): GameManager.ActionCore_DrawWeapon,
/// a move action that does not provoke, free with Quick Draw (a weapon), free with a regular move at base attack bonus +1
/// (once per move action spent moving), and the second light or one-handed weapon free with Two-Weapon Fighting
/// (EquipmentActionRules). The context names the pack item (<see cref="ActionContext.Item"/>,
/// <see cref="ActionContext.InventoryIndex"/>) and optionally the preferred hand (<see cref="ActionContext.Variant"/>
/// "right-hand" or "left-hand"). The PC's character-sheet inventory (GameManager.TryInventoryEquip, through
/// RunPlayerAction), the AI's weapon fallback (AiWeaponFallback) and the harness (RunAIAction) run it alike; the use-item
/// core draws a wand through the same rule.
/// </summary>
public sealed class DrawWeaponActionExecutor : IActionExecutor
{
    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        string condition = GameManager.ItemManipulationConditionRefusal(actor);
        if (condition != null)
            return Legality.Refused(LegalityBar.Condition, condition);
        Inventory inv = EquipmentActionRules.InventoryOf(actor);
        ItemData first = null;
        if (inv != null && inv.GeneralSlots != null)
            foreach (ItemData item in inv.GeneralSlots)
                if (EquipmentActionRules.IsDrawable(item))
                {
                    first = item;
                    break;
                }
        if (first == null)
            return Legality.Refused(LegalityBar.Equipment, "no weapon or wand in the pack to draw");
        return EquipmentActionRules.CanPay(actor, EquipmentActionRules.DrawPaymentFor(actor, first))
            ? Legality.Allowed()
            : Legality.Refused(LegalityBar.Action, "no move action left to draw (PHB p.142)");
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        Legality alive = ExecutorUtil.Alive(ctx.Actor);
        if (!alive.Ok)
            return alive;
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null)
            return Legality.Refused(LegalityBar.Other, "no game");
        return gm.CheckDrawWeapon(ctx.Actor, ctx.InventoryIndex, ctx.Item, ctx.Variant);
    }

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null || ctx == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Other, gm == null ? "no game" : "no context"));
            yield break;
        }
        yield return gm.ActionCore_DrawWeapon(ctx.Actor, ctx.InventoryIndex, ctx.Item, ctx.Variant, o);
    }
}

/// <summary>
/// Sheathe a weapon, or put away the item in hand (status New; batch 13 step 15, D8, ITM-069):
/// GameManager.ActionCore_SheatheWeapon, a move action that provokes (PHB p.142, Table 8-2). The context names the held item
/// (<see cref="ActionContext.Item"/>; null: the main-hand weapon). The PC's inventory (TryInventoryUnequip) and the AI and
/// the harness (RunAIAction) run it alike.
/// </summary>
public sealed class SheatheWeaponActionExecutor : IActionExecutor
{
    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        GameManager gm = ExecutorUtil.Gm;
        return gm == null ? Legality.Refused(LegalityBar.Other, "no game") : gm.CheckSheatheWeapon(actor, null);
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        Legality alive = ExecutorUtil.Alive(ctx.Actor);
        if (!alive.Ok)
            return alive;
        GameManager gm = ExecutorUtil.Gm;
        return gm == null ? Legality.Refused(LegalityBar.Other, "no game") : gm.CheckSheatheWeapon(ctx.Actor, ctx.Item);
    }

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null || ctx == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Other, gm == null ? "no game" : "no context"));
            yield break;
        }
        yield return gm.ActionCore_SheatheWeapon(ctx.Actor, ctx.Item, ctx.Source, ctx.Decider ?? ActionDeciders.For(ctx.Actor, ctx.Source), o);
    }
}

/// <summary>
/// Ready or loose a shield (status New; batch 13 step 15, D8, ITM-069): GameManager.ActionCore_ReadyShield, a move action
/// that does not provoke, free with a regular move at base attack bonus +1 (PHB p.142-143, Table 8-2 note 4). The context
/// names the pack shield to ready, or the readied shield (or <see cref="ActionContext.Variant"/> "loose") to unstrap and
/// drop. The PC's inventory and the AI and the harness run it alike.
/// </summary>
public sealed class ReadyShieldActionExecutor : IActionExecutor
{
    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null)
            return Legality.Refused(LegalityBar.Other, "no game");
        Legality loose = gm.CheckReadyShield(actor, -1, null, EquipmentActionRules.VariantLoose);
        if (loose.Ok)
            return loose;
        Inventory inv = EquipmentActionRules.InventoryOf(actor);
        if (inv != null && inv.GeneralSlots != null)
            for (int i = 0; i < inv.GeneralSlots.Length; i++)
                if (inv.GeneralSlots[i] != null && inv.GeneralSlots[i].IsShield)
                    return gm.CheckReadyShield(actor, i, inv.GeneralSlots[i], null);
        return Legality.Refused(LegalityBar.Equipment, "no shield to ready or loose");
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        Legality alive = ExecutorUtil.Alive(ctx.Actor);
        if (!alive.Ok)
            return alive;
        GameManager gm = ExecutorUtil.Gm;
        return gm == null ? Legality.Refused(LegalityBar.Other, "no game") : gm.CheckReadyShield(ctx.Actor, ctx.InventoryIndex, ctx.Item, ctx.Variant);
    }

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null || ctx == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Other, gm == null ? "no game" : "no context"));
            yield break;
        }
        yield return gm.ActionCore_ReadyShield(ctx.Actor, ctx.InventoryIndex, ctx.Item, ctx.Variant, o);
    }
}

/// <summary>
/// Pick up an item (status Extracted; batch 13 step 15, D8, AI-054): GameManager.ActionCore_PickUpItem, the PC Pick Up
/// button's old body (ResolvePickUpItemProvocation) made actor-agnostic: a move action that provokes (PHB p.142, Table
/// 8-2). The context names the ground item (<see cref="ActionContext.Item"/> and <see cref="ActionContext.Cell"/>, or its
/// id as <see cref="ActionContext.Variant"/>; none: the first in reach). CanStart is the PC panel's gate
/// (GameManager.GetPickUpItemDisabledReason). The PC button and the AI and the harness (RunAIAction) run it alike; the AI's
/// own choice to pick something up is batch 14's.
/// </summary>
public sealed class PickUpItemActionExecutor : IActionExecutor
{
    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null)
            return Legality.Refused(LegalityBar.Other, "no game");
        return ActionLegality.FromDisabledReason(gm.GetPickUpItemDisabledReason(actor), LegalityBar.Equipment);
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        Legality alive = ExecutorUtil.Alive(ctx.Actor);
        if (!alive.Ok)
            return alive;
        GameManager gm = ExecutorUtil.Gm;
        return gm == null ? Legality.Refused(LegalityBar.Other, "no game") : gm.CheckPickUpItem(ctx.Actor, ctx.Cell, ctx.Item, ctx.Variant);
    }

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null || ctx == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Other, gm == null ? "no game" : "no context"));
            yield break;
        }
        yield return gm.ActionCore_PickUpItem(ctx.Actor, ctx.Cell, ctx.Item, ctx.Variant, ctx.Source,
            ctx.Decider ?? ActionDeciders.For(ctx.Actor, ctx.Source), o);
    }
}

/// <summary>
/// Drop an item (status Extracted; batch 13 step 15, D8, AI-054, CMB-181): GameManager.ActionCore_DropItem, the PC Drop
/// button's old body made actor-agnostic: a free action that provokes nothing (PHB p.141, Table 8-2). The context names
/// the held item (<see cref="ActionContext.Item"/>; null: what the creature holds), or <see cref="ActionContext.Variant"/>
/// "all-held" drops everything held (a panicked creature at the start of its turn, PHB p.311,
/// FrightenedBehaviorController). A readied shield is loosed instead (the ready-shield entry, PHB p.143). CanStart is the PC
/// panel's gate (GameManager.GetDropEquippedItemDisabledReason).
/// </summary>
public sealed class DropItemActionExecutor : IActionExecutor
{
    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null)
            return Legality.Refused(LegalityBar.Other, "no game");
        return ActionLegality.FromDisabledReason(gm.GetDropEquippedItemDisabledReason(actor), LegalityBar.Equipment);
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        Legality alive = ExecutorUtil.Alive(ctx.Actor);
        if (!alive.Ok)
            return alive;
        GameManager gm = ExecutorUtil.Gm;
        return gm == null ? Legality.Refused(LegalityBar.Other, "no game") : gm.CheckDropItem(ctx.Actor, ctx.Item, ctx.Variant);
    }

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null || ctx == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Other, gm == null ? "no game" : "no context"));
            yield break;
        }
        yield return gm.ActionCore_DropItem(ctx.Actor, ctx.Item, ctx.Variant, o);
    }
}
