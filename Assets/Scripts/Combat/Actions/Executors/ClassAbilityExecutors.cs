using System.Collections;

/// <summary>
/// The request of a turning (batch 13 step 16, D9): whether the turner lets the turning take undead that are already
/// turned (PHB p.159: the turner may skip over them). The PC is asked once per use (GameManager.ConfirmTurnUndeadTargeting);
/// with no request, as for the AI, already turned undead are skipped.
/// </summary>
public sealed class TurnUndeadRequest
{
    public bool IncludeTurned;
}

/// <summary>
/// Turn undead (status Extracted; D9, CMB-201, CMB-026's line of effect, AI-054): GameManager.ActionCore_TurnUndead, the
/// PC's old ExecuteTurnUndead made actor-agnostic. A standard action that provokes nothing (PHB p.159), paid first (the
/// action and the day's attempt), then the turning check (d20 + Cha, Table 8-9) and, when it can affect an undead in
/// range, the turning damage (2d6 + turning level + Cha), a pool of Hit Dice spent on the undead within 60 ft with line
/// of effect, closest first (ties to the lower HD), stopping at the first that does not fit; an undead of half the
/// turning level or less is destroyed. CanStart is GameManager.CanUseTurnUndead (the class, the standard action, the
/// grapple bar, the daily uses). The PC's Turn Undead button (its range preview, then any click; RunPlayerAction, UiAfter
/// AttackDelay 0.9 s) and the AI (GameManager.TryNPCTurnUndeadTurnForAI through RunAIAction, design choice C6) run it.
/// <see cref="ActionContext.CoreRequest"/> may carry a <see cref="TurnUndeadRequest"/>.
/// </summary>
public sealed class TurnUndeadActionExecutor : IActionExecutor
{
    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null)
            return Legality.Refused(LegalityBar.Other, "no game");
        return gm.CanUseTurnUndead(actor, out string reason)
            ? Legality.Allowed()
            : Legality.Refused(GameManager.TurnUndeadRefusalBar(reason), reason);
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        return CanStart(ctx.Actor);
    }

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null || ctx == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Other, "no game"));
            yield break;
        }
        bool include = ctx.CoreRequest is TurnUndeadRequest r && r.IncludeTurned;
        gm.ActionCore_TurnUndead(ctx.Actor, include, o);
    }
}

/// <summary>
/// Rage (status Extracted; D10, AI-079, AI-054): GameManager.ActionCore_Rage over CharacterController.ActivateRage. Entering
/// a rage takes no time, but only on the barbarian's own turn, not in response to another's action (PHB p.25): no action
/// is spent, and it is refused when it is not the actor's turn or the barbarian has already raged this encounter ("only
/// once per encounter"; fatigue is no bar, CharacterStats.RagedThisEncounter) (<see cref="RefusalOf"/>). The PC's Rage button
/// (RunPlayerAction, UiAfter ShowChoices) and the AI's turn start (GameManager.TryNPCRageForAI through RunAIAction, design
/// choice C6) run it.
/// </summary>
public sealed class RageActionExecutor : IActionExecutor
{
    /// <summary>Why <paramref name="actor"/> cannot enter a rage now (PHB p.25-26), or null when it can.</summary>
    public static string RefusalOf(CharacterController actor, out LegalityBar bar)
    {
        bar = LegalityBar.Resource;
        if (actor == null || actor.Stats == null)
        {
            bar = LegalityBar.Other;
            return "no actor";
        }
        CharacterStats s = actor.Stats;
        if (!s.IsBarbarian || s.MaxRagesPerDay <= 0)
        {
            bar = LegalityBar.Other;
            return "not a barbarian";
        }
        if (s.IsRaging)
            return "already raging";
        // Once per encounter (PHB p.25); fatigue is no bar (a fatigued barbarian that has not raged yet may rage).
        if (s.RagedThisEncounter)
            return "already raged this encounter (PHB p.25)";
        if (s.RagesUsedToday >= s.MaxRagesPerDay)
            return "no rages left today";
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null || gm.CurrentCharacter != actor)
        {
            bar = LegalityBar.Action;
            return "only on its own turn (PHB p.25)";
        }
        return null;
    }

    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        string reason = RefusalOf(actor, out LegalityBar bar);
        return reason == null ? Legality.Allowed() : Legality.Refused(bar, reason);
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        return CanStart(ctx.Actor);
    }

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null || ctx == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Other, "no game"));
            yield break;
        }
        gm.ActionCore_Rage(ctx.Actor, o);
    }
}

/// <summary>
/// The activated domain powers (status Extracted; AI-054, CHR-070): GameManager.ActionCore_DomainPower, the PC's Domain
/// Power button's dispatch made actor-agnostic. <see cref="ActionContext.Variant"/> is the domain (PHB p.185-189): Strength
/// (Feat of Strength, free), Destruction (the smite, declared free before the attack), Death (the death touch on
/// <see cref="ActionContext.Target"/>, a standard action), Sun (a greater turning in place of a turning attempt: the
/// turn-undead core with every affected undead destroyed; a <see cref="TurnUndeadRequest"/> as for turn undead), Travel,
/// Luck (arms the reroll), Plant and the elemental domains (their turning, a standard action). CanStart: some power is
/// left (GameManager.GetAvailableDomainPowers); CanExecute adds the domain's own use (CanActivateDomainPower), the
/// standard action where the power takes one, and the Death touch's target (living, within natural reach: a melee touch
/// attack, AttackLegality.IsInReachOrRange). The PC's button and the AI (RunAIAction; the
/// AI's own choice is batch 14's) run it alike.
/// </summary>
public sealed class DomainPowerActionExecutor : IActionExecutor
{
    public const string SunVariant = "Sun";
    public const string DeathVariant = "Death";

    /// <summary>The domain power's action: a standard action for the touch and the turnings, else free (as the powers are built).</summary>
    public static ActionCostType CostOf(string domain)
    {
        switch (domain)
        {
            case "Death":
            case "Sun":
            case "Plant":
            case "Air":
            case "Earth":
            case "Fire":
            case "Water":
                return ActionCostType.Standard;
            default:
                return ActionCostType.Free;
        }
    }

    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null)
            return Legality.Refused(LegalityBar.Other, "no game");
        return ActionLegality.FromCheck(gm.GetAvailableDomainPowers(actor).Count > 0, "no domain power left", LegalityBar.Resource);
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
        if (string.IsNullOrEmpty(ctx.Variant))
            return Legality.Refused(LegalityBar.Target, "no domain chosen");
        if (!gm.CanActivateDomainPower(ctx.Actor, ctx.Variant, out string reason))
            return Legality.Refused(LegalityBar.Resource, reason);
        if (CostOf(ctx.Variant) == ActionCostType.Standard && (ctx.Actor.Actions == null || !ctx.Actor.Actions.HasStandardAction))
            return Legality.Refused(LegalityBar.Action, "no standard action left");
        if (ctx.Variant == SunVariant && !gm.CanUseTurnUndead(ctx.Actor, out string turnReason))
            return Legality.Refused(GameManager.TurnUndeadRefusalBar(turnReason), turnReason);
        if (ctx.Variant == DeathVariant)
        {
            Legality live = ExecutorUtil.LiveTarget(ctx.Target);
            if (!live.Ok)
                return live;
            // The death touch is a melee touch attack (PHB p.187): the target must be within natural reach.
            if (!AttackLegality.IsInReachOrRange(ctx.Actor, ctx.Target, DirectAttackKind.Touch, null, out string reachReason))
                return Legality.Refused(LegalityBar.Target, reachReason);
        }
        return Legality.Allowed();
    }

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null || ctx == null || string.IsNullOrEmpty(ctx.Variant))
        {
            o.Refuse(Legality.Refused(LegalityBar.Target, gm == null ? "no game" : "no domain chosen"));
            yield break;
        }
        bool include = ctx.CoreRequest is TurnUndeadRequest r && r.IncludeTurned;
        gm.ActionCore_DomainPower(ctx.Actor, ctx.Variant, ctx.Target, include, o);
    }
}
