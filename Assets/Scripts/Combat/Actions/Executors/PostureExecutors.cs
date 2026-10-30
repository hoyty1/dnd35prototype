using System.Collections;

/// <summary>
/// The reload rule (batch 13 step 12, decision D4, docs/systems/RULES_COVERAGE.md 4.1; CMB-032, AI-057), one rule for
/// every creature (explicit RAW):
/// - loading a hand or light crossbow is a move action, a heavy crossbow a full-round action (PHB p.115);
/// - Rapid Reload makes the hand or light load a free action and the heavy load a move action (PHB p.99);
/// - every load provokes attacks of opportunity, Rapid Reload's included (PHB p.99, p.115; Table 8-2 p.141, its
///   move-action column realigned 2026-10-29);
/// - loading puts a bolt in the crossbow (PHB p.113-114): no bolt, no reload; a pinned or grappling creature cannot reload
///   (PHB p.156: loading is not among a grappler's actions; PHB p.115-116: it takes two hands).
/// The repeating crossbow (PHB p.116) is not modelled (no such item; CRE-052). The cost is
/// <see cref="ItemData.GetEffectiveReloadAction"/> through <see cref="CharacterController.GetEffectiveReloadAction"/> (the
/// weapon's base load and the matching Rapid Reload feat). GameManager.CanReloadEquippedWeapon (the PC panel, the attack
/// guard's tests) forwards here. Pure: no dice, no log, no state change.
/// </summary>
public static class ReloadRules
{
    /// <summary>
    /// Whether <paramref name="actor"/> may reload its main weapon now, with the load's action type
    /// (<paramref name="reloadAction"/>, set whenever the weapon needs loading) and why not (<paramref name="reason"/>).
    /// </summary>
    public static bool Check(CharacterController actor, out string reason, out ReloadActionType reloadAction)
        => Check(actor, out reason, out reloadAction, out _);

    /// <summary><see cref="Check(CharacterController, out string, out ReloadActionType)"/> with the kind of bar of a refusal.</summary>
    public static bool Check(CharacterController actor, out string reason, out ReloadActionType reloadAction, out LegalityBar bar)
    {
        reason = string.Empty;
        reloadAction = ReloadActionType.None;
        bar = LegalityBar.None;

        if (actor == null)
        {
            reason = "No active character";
            bar = LegalityBar.Target;
            return false;
        }

        ItemData weapon = actor.GetEquippedMainWeapon();
        if (weapon == null || !weapon.RequiresReload)
        {
            reason = "No reloadable weapon equipped";
            bar = LegalityBar.Equipment;
            return false;
        }

        if (weapon.IsLoaded)
        {
            reason = "Weapon already loaded";
            bar = LegalityBar.Ammunition;
            return false;
        }

        if (actor.HasCondition(CombatConditionType.Pinned))
        {
            reason = "Pinned creatures cannot reload";
            bar = LegalityBar.Condition;
            return false;
        }

        // A grappler takes only the actions PHB p.156 lists, and loading is not one of them; loading a crossbow also
        // takes two hands (PHB p.115-116: one hand may fire a crossbow, not load it).
        if (actor.IsGrappling())
        {
            reason = "Grappling creatures cannot reload";
            bar = LegalityBar.Condition;
            return false;
        }

        reloadAction = actor.GetEffectiveReloadAction(weapon);

        // Loading a crossbow places a bolt in it (PHB p.113-114): no bolt, no reload.
        global::Inventory ammoInventory = actor.InventoryComp != null ? actor.InventoryComp.CharacterInventory : null;
        if (weapon.RequiresAmmoType != AmmunitionType.None && ammoInventory != null && !ammoInventory.HasAmmo(weapon.RequiresAmmoType))
        {
            reason = $"No {weapon.RequiresAmmoType} ammunition to load";
            bar = LegalityBar.Ammunition;
            return false;
        }

        if (actor.Actions == null)
        {
            reason = "Cannot reload";
            bar = LegalityBar.Action;
            return false;
        }

        switch (reloadAction)
        {
            case ReloadActionType.FreeAction:
                return true;

            case ReloadActionType.MoveAction:
                if (actor.Actions.HasMoveAction || actor.Actions.CanConvertStandardToMove)
                    return true;
                reason = "Need move action";
                bar = LegalityBar.Action;
                return false;

            case ReloadActionType.FullRound:
                if (actor.Actions.HasFullRoundAction)
                    return true;
                reason = "Need full-round action";
                bar = LegalityBar.Action;
                return false;

            default:
                reason = "Cannot reload";
                bar = LegalityBar.Other;
                return false;
        }
    }

    /// <summary>The catalog's action type of a load (D4): free, a move action or a full-round action.</summary>
    public static ActionCostType CostOf(ReloadActionType reloadAction)
    {
        switch (reloadAction)
        {
            case ReloadActionType.FreeAction: return ActionCostType.Free;
            case ReloadActionType.FullRound: return ActionCostType.FullRound;
            default: return ActionCostType.Move;
        }
    }

    /// <summary>The load's action type for <paramref name="actor"/>'s main weapon now (a move action when it needs none).</summary>
    public static ActionCostType CostFor(CharacterController actor)
    {
        ItemData weapon = actor != null ? actor.GetEquippedMainWeapon() : null;
        if (weapon == null || !weapon.RequiresReload)
            return ActionCostType.Move;
        return CostOf(actor.GetEffectiveReloadAction(weapon));
    }
}

/// <summary>
/// Reload (status Extracted; D4, CMB-032, AI-057): GameManager.ActionCore_Reload, the PC Reload button's old executor
/// made actor-agnostic. It pays the load's action (<see cref="ReloadRules"/>, the entry's CostFor) once the actor goes
/// on with an action that provokes (the UI's attack-of-opportunity prompt; the AI always goes on), takes the attacks of
/// opportunity (GameManager.ResolveActionProvocation, AoOProvokingAction.LoadCrossbow), then loads the crossbow unless an
/// attack of opportunity dropped the actor. The PC button (GameManager.RunPlayerAction, UiAfter ShowChoices), the NPC
/// attack prelude (NPCPerformAttack: reload, then the attack with the standard action left) and the AI's out-of-reach
/// reload (AiOutOfReach) run it alike.
/// </summary>
public sealed class ReloadActionExecutor : IActionExecutor
{
    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        return ReloadRules.Check(actor, out string reason, out _, out LegalityBar bar)
            ? Legality.Allowed()
            : Legality.Refused(bar, reason);
    }

    public Legality CanExecute(ActionContext ctx) => ctx == null ? Legality.Refused(LegalityBar.Other, "no context") : CanStart(ctx.Actor);

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Other, "no game"));
            yield break;
        }
        yield return gm.ActionCore_Reload(ctx.Actor, ctx.Source, ctx.Decider ?? ActionDeciders.For(ctx.Actor, ctx.Source), o);
    }
}

/// <summary>
/// Crawl 5 feet while prone (status Extracted; D5, AI-054): GameManager.ActionCore_Crawl, the PC's old ExecuteCrawl made
/// actor-agnostic. A move action (spent when the crawl starts, D3) that provokes from every foe that threatens the creature
/// at any point of the crawl (PHB p.143): a foe that threatens the square left attacks before the creature leaves it
/// (PHB p.137), one that threatens only the square entered attacks there; each foe once (PHB p.138). CanStart is the PC
/// panel's gate (GameManager.GetCrawlDisabledReason: prone, not pinned or grappled, no 5-foot step taken, a move action
/// left, a square to crawl to); CanExecute adds the square (an adjacent square a step could enter, not difficult terrain).
/// The PC's crawl click and the AI (the harness's Step.Crawl; the AI's own choice to crawl is batch 14's) run it alike.
/// </summary>
public sealed class CrawlActionExecutor : IActionExecutor
{
    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null)
            return Legality.Refused(LegalityBar.Other, "no game");
        return ActionLegality.FromDisabledReason(gm.GetCrawlDisabledReason(actor), LegalityBar.Condition);
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        Legality start = CanStart(ctx.Actor);
        if (!start.Ok)
            return start;
        if (!ctx.Cell.HasValue)
            return Legality.Refused(LegalityBar.Terrain, "no destination");
        return ExecutorUtil.Gm.IsValidCrawlDestinationForActions(ctx.Actor, ctx.Cell.Value)
            ? Legality.Allowed()
            : Legality.Refused(LegalityBar.Terrain, "not a square the creature can crawl to");
    }

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null || !ctx.Cell.HasValue)
        {
            o.Refuse(Legality.Refused(LegalityBar.Terrain, "no destination"));
            yield break;
        }
        yield return gm.ActionCore_Crawl(ctx.Actor, ctx.Cell.Value, ctx.Source, o);
    }
}

/// <summary>
/// Drop prone (status Extracted; D5, AI-054): a free action that provokes nothing (PHB p.141 Table 8-2, p.311).
/// GameManager.ActionCore_DropProne, the PC button's old body. CanStart is GameManager.GetDropProneDisabledReason (not
/// already prone, not pinned or grappled). The PC button and the AI (the harness's Step.DropProne) run it alike.
/// </summary>
public sealed class DropProneActionExecutor : IActionExecutor
{
    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null)
            return Legality.Refused(LegalityBar.Other, "no game");
        return ActionLegality.FromDisabledReason(gm.GetDropProneDisabledReason(actor), LegalityBar.Condition);
    }

    public Legality CanExecute(ActionContext ctx) => ctx == null ? Legality.Refused(LegalityBar.Other, "no context") : CanStart(ctx.Actor);

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Other, "no game"));
            yield break;
        }
        o.Performed = gm.ActionCore_DropProne(ctx.Actor, o);
        yield break;
    }
}
