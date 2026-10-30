using System.Collections;

/// <summary>
/// Aid another (status Extracted; D6, AI-054): GameManager.ActionCore_AidAnother, the PC's old ExecuteAidAnother made
/// actor-agnostic. A standard action, paid first, then an attack roll against AC 10 (PHB p.154) with the aider's melee
/// attack modifier; a hit gives the ally +2 on its next attack roll, or +2 AC, against the opponent, until the start of
/// the aider's next turn. It provokes only when the aided action would (Table 8-2 "Maybe", note 2), and neither aid built
/// here (an attack roll, AC) aids an action that provokes, so it provokes nothing. A grappling or pinned aider cannot aid
/// (PHB p.156). The context: <see cref="ActionContext.Target"/> is the ally aided, <see cref="ActionContext.Opponent"/>
/// the opponent, <see cref="ActionContext.Variant"/> <see cref="OffenseVariant"/> or <see cref="DefenseVariant"/>.
/// CanStart is GameManager.GetAidAnotherDisabledReason; CanExecute adds the targets (GetAidAnotherTargetRefusal). The PC's
/// aid menu (GameManager.ExecuteAidAnother, through RunPlayerAction, UiAfter AttackDelay 0.9 s) and the AI (RunAIAction;
/// the AI's own choice to aid is batch 14's) run it alike.
/// </summary>
public sealed class AidAnotherActionExecutor : IActionExecutor
{
    /// <summary>Aid the ally's next attack roll against the opponent (+2).</summary>
    public const string OffenseVariant = "offense";

    /// <summary>Aid the ally's AC against the opponent's next attack (+2).</summary>
    public const string DefenseVariant = "defense";

    /// <summary>The aid of <paramref name="variant"/>: Offense for <see cref="OffenseVariant"/>, Defense for <see cref="DefenseVariant"/>, else null.</summary>
    public static GameManager.AidType? AidTypeOf(string variant)
    {
        if (variant == OffenseVariant)
            return GameManager.AidType.Offense;
        if (variant == DefenseVariant)
            return GameManager.AidType.Defense;
        return null;
    }

    /// <summary>The context variant of <paramref name="type"/>.</summary>
    public static string VariantOf(GameManager.AidType type) => type == GameManager.AidType.Defense ? DefenseVariant : OffenseVariant;

    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null)
            return Legality.Refused(LegalityBar.Other, "no game");
        return SupportLegality.FromReason(gm.GetAidAnotherDisabledReason(actor));
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        Legality start = CanStart(ctx.Actor);
        if (!start.Ok)
            return start;
        if (!AidTypeOf(ctx.Variant).HasValue)
            return Legality.Refused(LegalityBar.Other, "no aid chosen (offense or defense)");
        string refusal = ExecutorUtil.Gm.GetAidAnotherTargetRefusal(ctx.Actor, ctx.Target, ctx.Opponent);
        return refusal == null ? Legality.Allowed() : Legality.Refused(LegalityBar.Target, refusal);
    }

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        GameManager.AidType? type = ctx != null ? AidTypeOf(ctx.Variant) : null;
        if (gm == null || !type.HasValue)
        {
            o.Refuse(Legality.Refused(LegalityBar.Other, gm == null ? "no game" : "no aid chosen (offense or defense)"));
            yield break;
        }
        gm.ActionCore_AidAnother(ctx.Actor, ctx.Target, ctx.Opponent, type.Value, ctx.Source, o);
    }
}

/// <summary>
/// Wake an ally (status Extracted; D6, AI-054): GameManager.ActionCore_WakeAlly, the PC's old
/// ExecuteAidAnotherWakeSleepingAlly made actor-agnostic. Shaking a sleeper awake is a standard action (PHB p.280), an
/// application of aid another (PHB p.154), paid first; it provokes nothing. A grappling or pinned creature cannot (PHB
/// p.156). <see cref="ActionContext.Target"/> is the sleeping ally, adjacent. CanStart is
/// GameManager.GetWakeAllyDisabledReason; CanExecute adds the ally (GetWakeAllyTargetRefusal). The PC's wake menu (through
/// RunPlayerAction, UiAfter AttackDelay 0.9 s) and the AI (RunAIAction; the AI's own choice is batch 14's) run it alike.
/// </summary>
public sealed class WakeAllyActionExecutor : IActionExecutor
{
    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null)
            return Legality.Refused(LegalityBar.Other, "no game");
        return SupportLegality.FromReason(gm.GetWakeAllyDisabledReason(actor));
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        Legality start = CanStart(ctx.Actor);
        if (!start.Ok)
            return start;
        string refusal = ExecutorUtil.Gm.GetWakeAllyTargetRefusal(ctx.Actor, ctx.Target);
        return refusal == null ? Legality.Allowed() : Legality.Refused(LegalityBar.Target, refusal);
    }

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Other, "no game"));
            yield break;
        }
        gm.ActionCore_WakeAlly(ctx.Actor, ctx.Target, ctx.Source, o);
    }
}

/// <summary>
/// The request of an attack on a wall (batch 13 step 13): set by the PC's Attack-button path, whose attack step was paid
/// before the wall square was clicked (the attack sequence pays it), so the executor pays nothing more.
/// </summary>
public sealed class AttackWallRequest
{
    public bool ActionPaid;
}

/// <summary>
/// Attack a wall (status Extracted; CORE-040, AI-054): GameManager.ActionCore_AttackWall, the old PerformPlayerAttackOnWall
/// and PerformStrengthCheckOnWall made actor-agnostic. A standard action against an intact Wall of Ice square
/// (<see cref="ActionContext.Cell"/>), paid first: a weapon attack that hits automatically (<see cref="WeaponVariant"/>,
/// the default; PHB p.165-166), at any square the attack reaches (the weapon's reach band, or its maximum range for a
/// shot or a throw: GameManager.IsWallCellInWeaponAttackReach, the Attack button's highlight test too), or a Strength
/// check to break through an adjacent square (<see cref="StrengthVariant"/>; PHB p.299). A grappling or pinned creature
/// cannot (PHB p.156). A shot provokes from the foes threatening the shooter (Table 8-2: Attack (ranged)), deals half
/// damage to the object (PHB p.165) and spends its ammunition. It continues no UI, so an NPC's wall attack no longer
/// starts the PC's attack follow-up inside the NPC turn (CORE-040, CORE-012). CanStart is
/// GameManager.GetAttackWallDisabledReason; CanExecute adds the square for the variant (GetAttackWallCellRefusal). The
/// PC's wall click after Attack (an <see cref="AttackWallRequest"/> with the action paid) and the Break Wall button run it
/// through RunPlayerAction (UiAfter AttackDelay 1.0 s); the AI's wall branch (NPCAttackWallForAI, NPCBreakWallForAI)
/// through RunAIAction.
/// </summary>
public sealed class AttackWallActionExecutor : IActionExecutor
{
    /// <summary>A weapon (natural or unarmed) attack on the wall square.</summary>
    public const string WeaponVariant = "weapon";

    /// <summary>A Strength check to break through the wall square.</summary>
    public const string StrengthVariant = "strength";

    private static bool Paid(ActionContext ctx) => ctx != null && ctx.CoreRequest is AttackWallRequest r && r.ActionPaid;

    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null)
            return Legality.Refused(LegalityBar.Other, "no game");
        return SupportLegality.FromReason(gm.GetAttackWallDisabledReason(actor));
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
        // The actor's own bars (grapple, the action unless the Attack button paid it); the square is the variant's test.
        Legality start = SupportLegality.FromReason(gm.GetAttackWallDisabledReason(ctx.Actor, Paid(ctx), requireWall: false));
        if (!start.Ok)
            return start;
        if (ctx.Variant != null && ctx.Variant != WeaponVariant && ctx.Variant != StrengthVariant)
            return Legality.Refused(LegalityBar.Other, "unknown wall attack '" + ctx.Variant + "'");
        if (!ctx.Cell.HasValue)
            return Legality.Refused(LegalityBar.Target, "no wall square");
        string refusal = gm.GetAttackWallCellRefusal(ctx.Actor, ctx.Cell.Value, ctx.Variant == StrengthVariant,
            gm.WallAttackIntent(ctx.Actor, ctx.Source));
        return refusal == null ? Legality.Allowed() : Legality.Refused(LegalityBar.Target, refusal);
    }

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null || ctx == null || !ctx.Cell.HasValue)
        {
            o.Refuse(Legality.Refused(LegalityBar.Target, gm == null ? "no game" : "no wall square"));
            yield break;
        }
        gm.ActionCore_AttackWall(ctx.Actor, ctx.Cell.Value, ctx.Variant == StrengthVariant, Paid(ctx), ctx.Source, o);
    }
}

/// <summary>The legality of a support or wall entry's disabled reason (GameManager.Get*DisabledReason): the action bar, the grapple bar (PHB p.156) or the target.</summary>
internal static class SupportLegality
{
    internal static Legality FromReason(string reason)
    {
        if (reason == null)
            return Legality.Allowed();
        if (reason == "no standard action left")
            return Legality.Refused(LegalityBar.Action, reason);
        if (GameManager.IsGrappleBarReason(reason))
            return Legality.Refused(LegalityBar.Condition, reason);
        return Legality.Refused(reason == "no actor" ? LegalityBar.Other : LegalityBar.Target, reason);
    }
}
