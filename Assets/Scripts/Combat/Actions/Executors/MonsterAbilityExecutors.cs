using System.Collections;

/// <summary>
/// The monster abilities' executors (batch 13 step 6): breath weapon, ranged special attack (spittle included) and engulf.
/// Each wraps the AI's own core, the only one these abilities have today (CRE-039), so the AI's calls run through the
/// catalog (GameManager.RunAIAction) with nothing changed; the entries stay AiOnly until batch 13 step 18 opens them to any
/// controller. The cores own their refusals, their cost and their log. No executor continues the UI or reads who controls
/// the actor (ActionCatalogTests' scan).
/// </summary>
public sealed class BreathWeaponActionExecutor : IActionExecutor
{
    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        if (!actor.HasBreathWeapon)
            return Legality.Refused(LegalityBar.Resource, "no breath weapon");
        if (!actor.IsBreathWeaponReady)
            return Legality.Refused(LegalityBar.Resource, "the breath weapon is not ready (MM p.306)");
        return ActionCosts.CanAfford(actor, ActionCostType.Standard)
            ? Legality.Allowed()
            : Legality.Refused(LegalityBar.Action, "a breath weapon is a standard action (MM p.69, p.306)");
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        Legality start = CanStart(ctx.Actor);
        return start.Ok ? ExecutorUtil.LiveTarget(ctx.Target) : start;
    }

    /// <summary>The breath core (GameManager.NPCExecuteBreathWeaponForAI's core overload) aimed at the context's target (ActionCore_BreathWeapon).</summary>
    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null || ctx.Actor == null || ctx.Target == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Target, "no breath aim"));
            yield break;
        }
        bool readyBefore = ctx.Actor.IsBreathWeaponReady;
        yield return gm.ActionCore_BreathWeapon(ctx.Actor, ctx.Target);
        o.Performed = readyBefore && !ctx.Actor.IsBreathWeaponReady;
        if (!o.Performed)
            o.Refuse(Legality.Refused(LegalityBar.Other, "the breath weapon was not used"));
    }
}

/// <summary>A ranged special attack (a web, an acid spray) or spittle (a free action): AIService.ExecuteRangedSpecialAttackCore (MM p.315).</summary>
public sealed class RangedSpecialActionExecutor : IActionExecutor
{
    private readonly bool _freeAction;

    public RangedSpecialActionExecutor(bool freeAction) { _freeAction = freeAction; }

    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        RangedSpecialAttackDefinition attack = actor.HasRangedSpecialAttack ? actor.GetRangedSpecialAttackDefinition() : null;
        if (attack == null || attack.IsFreeAction != _freeAction)
            return Legality.Refused(LegalityBar.Resource, _freeAction ? "no spittle" : "no ranged special attack");
        if (!actor.IsRangedSpecialAttackReady)
            return Legality.Refused(LegalityBar.Resource, attack.Name + " is not ready");
        return _freeAction || ActionCosts.CanAfford(actor, ActionCostType.Standard)
            ? Legality.Allowed()
            : Legality.Refused(LegalityBar.Action, attack.Name + " is a standard action (MM p.315)");
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        Legality start = CanStart(ctx.Actor);
        return start.Ok ? ExecutorUtil.LiveTarget(ctx.Target) : start;
    }

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        o.Performed = gm != null && gm.ActionCore_RangedSpecial(ctx.Actor, ctx.Target);
        if (!o.Performed)
            o.Refuse(Legality.Refused(LegalityBar.Other, "the ranged special attack was not used"));
        yield break;
    }
}

/// <summary>Engulf (the gelatinous cube, MM p.201): AIService.ExecuteEngulfCore.</summary>
public sealed class EngulfActionExecutor : IActionExecutor
{
    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        if (!actor.HasEngulf)
            return Legality.Refused(LegalityBar.Resource, "no engulf");
        return ActionCosts.CanAfford(actor, ActionCostType.Standard)
            ? Legality.Allowed()
            : Legality.Refused(LegalityBar.Action, "engulf is a standard action (MM p.201)");
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        Legality start = CanStart(ctx.Actor);
        return start.Ok ? ExecutorUtil.LiveTarget(ctx.Target) : start;
    }

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        o.Performed = gm != null && gm.ActionCore_Engulf(ctx.Actor, ctx.Target);
        if (!o.Performed)
            o.Refuse(Legality.Refused(LegalityBar.Other, "engulf was not used"));
        yield break;
    }
}
