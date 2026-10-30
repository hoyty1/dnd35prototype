using System.Collections;

/// <summary>
/// The monster abilities' executors (status Extracted since batch 13 step 18, decision D11; AI-040, CRE-039): breath
/// weapon, ranged special attack (spittle included) and engulf. Each wraps the one core of its ability (the breath core,
/// GameManager.NPCExecuteBreathWeaponForAI's core overload; AIService.ExecuteRangedSpecialAttackCore; AIService.ExecuteEngulfCore),
/// now actor-agnostic: any creature with the data takes them, a controllable one through its Monster Abilities button
/// (GameManager.OnMonsterAbilityButtonPressed, RunPlayerAction), an AI-run one through the Dragon profile's turn and the
/// AI routines (RunAIAction). Their CanExecute is the legality, pre-checked by both runners, so a refusal spends nothing;
/// the cores pay. No executor continues the UI or reads who controls the actor (ActionCatalogTests' scan).
/// </summary>
internal static class MonsterAbilityRules
{
    /// <summary>
    /// The actor bars every monster ability shares (batch 13 step 18 review), on <see cref="LegalityBar.Condition"/>: a
    /// creature that cannot attack (nauseated, dazed, cowering, helpless and the other conditions that bar standard
    /// actions: CharacterController.CanAttack; PHB p.311, a nauseated creature is unable to attack and takes only a move
    /// action, ActionEconomy.MoveActionOnly), one with no further action this turn (a disabled creature's strenuous act,
    /// ActionEconomy.NoFurtherActions; PHB p.145), and a grappling or pinned one, which takes only the grapple actions
    /// (PHB p.156; GameManager.GetSupportActionGrappleRefusal). A free ability, the spittle, is barred as well: its cost
    /// is free, but it is a ranged touch attack (MM p.126). Null when none applies.
    /// </summary>
    internal static Legality? ActorRefusal(CharacterController actor, string abilityName)
    {
        if (actor.Actions != null && actor.Actions.MoveActionOnly)
            return Legality.Refused(LegalityBar.Condition, abilityName + ": only a move action this turn (nauseated: unable to attack, PHB p.311)");
        if (!actor.CanAttack())
            return Legality.Refused(LegalityBar.Condition, abilityName + ": a condition bars attacks and standard actions");
        if (actor.Actions != null && actor.Actions.NoFurtherActions)
            return Legality.Refused(LegalityBar.Condition, abilityName + ": no further action this turn (PHB p.145)");
        string grappled = GameManager.GetSupportActionGrappleRefusal(actor);
        if (grappled != null)
            return Legality.Refused(LegalityBar.Condition, abilityName + ": " + grappled + " (a grappler takes only the grapple actions, PHB p.156)");
        return null;
    }
}

public sealed class BreathWeaponActionExecutor : IActionExecutor
{
    /// <summary>
    /// The breath's start gate (MM p.69, p.315; D11): a breath weapon that is ready (recharged, a daily use left;
    /// CharacterController.IsBreathWeaponReady, never a once-per-round reaction such as the acid spray), an actor free
    /// to use it (<see cref="MonsterAbilityRules.ActorRefusal"/>) and a standard action to spend.
    /// </summary>
    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        if (!actor.HasBreathWeapon)
            return Legality.Refused(LegalityBar.Resource, "no breath weapon");
        if (!actor.IsBreathWeaponReady)
            return Legality.Refused(LegalityBar.Resource, "the breath weapon is not ready (MM p.69: a dragon's breath recharges in 1d4 rounds)");
        Legality? barred = MonsterAbilityRules.ActorRefusal(actor, "Breath Weapon");
        if (barred.HasValue)
            return barred.Value;
        return ActionCosts.CanAfford(actor, ActionCostType.Standard)
            ? Legality.Allowed()
            : Legality.Refused(LegalityBar.Action, "a breath weapon is a standard action (MM p.69, p.315)");
    }

    /// <summary>The start gate, a living aim target, and a cone or line of the breath that reaches it from here (GameManager.AimBreathWeapon).</summary>
    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        Legality start = CanStart(ctx.Actor);
        if (!start.Ok)
            return start;
        return TargetRefusal(ctx.Actor, ctx.Target);
    }

    /// <summary>Whether <paramref name="actor"/>'s breath can be aimed at <paramref name="target"/> from where it stands (pure).</summary>
    public static Legality TargetRefusal(CharacterController actor, CharacterController target)
    {
        Legality live = ExecutorUtil.LiveTarget(target);
        if (!live.Ok)
            return live;
        if (target == actor)
            return Legality.Refused(LegalityBar.Target, "a creature does not breathe on itself");
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null || gm.AimBreathWeapon(actor, target) == null)
            return Legality.Refused(LegalityBar.Range, "no cone or line of the breath reaches " + target.Stats.CharacterName + " from here");
        return Legality.Allowed();
    }

    /// <summary>
    /// The breath core aimed at the context's target (ActionCore_BreathWeapon): the standard action first, then Frightful
    /// Presence, the recharge and one damage roll for every creature in the area (batch 11 D6). Performed when a use of
    /// the breath was spent.
    /// </summary>
    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        if (gm == null || ctx.Actor == null || ctx.Target == null)
        {
            o.Refuse(Legality.Refused(LegalityBar.Target, "no breath aim"));
            yield break;
        }
        int usesBefore = ctx.Actor.BreathWeaponUsesToday;
        yield return gm.ActionCore_BreathWeapon(ctx.Actor, ctx.Target);
        o.Performed = ctx.Actor.BreathWeaponUsesToday > usesBefore;
        if (!o.Performed)
            o.Refuse(Legality.Refused(LegalityBar.Other, "the breath weapon was not used"));
    }
}

/// <summary>
/// A ranged special attack (MM p.315: a supernatural one is a standard action unless its entry says otherwise, an
/// extraordinary one costs what its entry says; the entry's cost is Standard) or, with <c>freeAction</c>, the spittle (MM
/// p.126: an extraordinary ability, a free action every round at one opponent within 30 ft; D11):
/// AIService.ExecuteRangedSpecialAttackCore. The spittle is once per round (CharacterController.FreeRangedSpecialUsedThisRound,
/// cleared at the creature's turn start).
/// </summary>
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
        // A ranged touch attack, so a creature that cannot attack does not make it, free or not (PHB p.311; review).
        Legality? barred = MonsterAbilityRules.ActorRefusal(actor, attack.Name);
        if (barred.HasValue)
            return barred.Value;
        if (_freeAction)
            return actor.FreeRangedSpecialUsedThisRound
                ? Legality.Refused(LegalityBar.Resource, attack.Name + " was already used this round (MM p.126: once every round)")
                : Legality.Allowed();
        return ActionCosts.CanAfford(actor, ActionCostType.Standard)
            ? Legality.Allowed()
            : Legality.Refused(LegalityBar.Action, attack.Name + " is a standard action (MM p.315)");
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        Legality start = CanStart(ctx.Actor);
        if (!start.Ok)
            return start;
        return TargetRefusal(ctx.Actor, ctx.Target);
    }

    /// <summary>
    /// Whether <paramref name="actor"/>'s ranged special can be aimed at <paramref name="target"/> (pure): a living
    /// creature within the attack's range, counted space to space by the PHB p.147 count (a Large creature measures from
    /// its nearest square); the spittle only at an opponent (MM p.126); not the creature it holds with one body part (MM
    /// p.310, CMB-163); nothing through a Resilient Sphere (CMB-092) or at a target whose ward the attacker is known to
    /// have failed (D6). The core makes the same checks and then the commits (the ward roll, the action, the cooldown).
    /// </summary>
    public static Legality TargetRefusal(CharacterController actor, CharacterController target)
    {
        Legality live = ExecutorUtil.LiveTarget(target);
        if (!live.Ok)
            return live;
        RangedSpecialAttackDefinition attack = actor != null && actor.HasRangedSpecialAttack ? actor.GetRangedSpecialAttackDefinition() : null;
        if (attack == null)
            return Legality.Refused(LegalityBar.Resource, "no ranged special attack");
        if (target == actor)
            return Legality.Refused(LegalityBar.Target, attack.Name + " is not aimed at its user");
        if (attack.IsFreeAction && !TeamUtility.IsEnemy(actor, target))
            return Legality.Refused(LegalityBar.Target, attack.Name + " is fired at an opponent (MM p.126)");
        if (!IsInRange(actor, target, attack))
            return Legality.Refused(LegalityBar.Range, target.Stats.CharacterName + " is beyond the " + attack.RangeFeet + " ft of " + attack.Name);
        if (actor.IsBarredFromOrdinaryAttackOn(target, out string holdReason))
            return Legality.Refused(LegalityBar.Target, holdReason);
        Legality barrier = AttackLegality.Check(actor, target, DirectAttackKind.Touch, null,
            AttackLegalityChecks.Sphere | AttackLegalityChecks.Ward);
        if (!barrier.Ok)
            return barrier;
        return Legality.Allowed();
    }

    /// <summary>Whether <paramref name="target"/> is within <paramref name="attack"/>'s range of <paramref name="actor"/>: nearest square to nearest square, the PHB p.147 count.</summary>
    internal static bool IsInRange(CharacterController actor, CharacterController target, RangedSpecialAttackDefinition attack)
        => actor != null && target != null && attack != null
           && actor.GetMinimumDistanceToTarget(target, chebyshev: false) <= attack.RangeFeet / 5;

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        o.Performed = gm != null && gm.ActionCore_RangedSpecial(ctx.Actor, ctx.Target);
        if (!o.Performed)
            o.Refuse(Legality.Refused(LegalityBar.Other, "the ranged special attack was not used"));
        yield break;
    }
}

/// <summary>
/// Engulf (MM p.202, the gelatinous cube: a standard action against Large or smaller creatures; AIService.ExecuteEngulfCore).
/// As built, it is aimed at one adjacent creature of Large size or smaller, which makes a Reflex save or is engulfed. The
/// rest of the cube's rule (the move over its opponents, the attack of opportunity in place of the save, the push aside
/// on a save, no slam in a round it engulfs) and the other engulfers' own rules (the cloaker's grapple, the mouther's
/// swallow) are CRE-078 (docs/issues/CRE.md); the engulfed condition's permanence is CMB-024.
/// </summary>
public sealed class EngulfActionExecutor : IActionExecutor
{
    public Legality CanStart(CharacterController actor)
    {
        Legality alive = ExecutorUtil.Alive(actor);
        if (!alive.Ok)
            return alive;
        if (!actor.HasEngulf)
            return Legality.Refused(LegalityBar.Resource, "no engulf");
        Legality? barred = MonsterAbilityRules.ActorRefusal(actor, "Engulf");
        if (barred.HasValue)
            return barred.Value;
        return ActionCosts.CanAfford(actor, ActionCostType.Standard)
            ? Legality.Allowed()
            : Legality.Refused(LegalityBar.Action, "engulf is a standard action (MM p.202)");
    }

    public Legality CanExecute(ActionContext ctx)
    {
        if (ctx == null)
            return Legality.Refused(LegalityBar.Other, "no context");
        Legality start = CanStart(ctx.Actor);
        if (!start.Ok)
            return start;
        return TargetRefusal(ctx.Actor, ctx.Target);
    }

    /// <summary>
    /// Whether <paramref name="actor"/> can engulf <paramref name="target"/> (pure): a living creature of Large size or
    /// smaller (MM p.202) in a square adjacent to any square of the engulfer's space (<see cref="IsAdjacent"/>).
    /// </summary>
    public static Legality TargetRefusal(CharacterController actor, CharacterController target)
    {
        Legality live = ExecutorUtil.LiveTarget(target);
        if (!live.Ok)
            return live;
        if (target == actor)
            return Legality.Refused(LegalityBar.Target, "a creature does not engulf itself");
        if (target.GetCurrentSizeCategory() > SizeCategory.Large)
            return Legality.Refused(LegalityBar.Target, target.Stats.CharacterName + " is larger than Large (MM p.202: Large or smaller creatures)");
        if (!IsAdjacent(actor, target))
            return Legality.Refused(LegalityBar.Reach, target.Stats.CharacterName + " is not adjacent");
        return Legality.Allowed();
    }

    /// <summary>Whether <paramref name="target"/>'s space touches <paramref name="actor"/>'s: one square apart, nearest square to nearest square (a Large engulfer has four).</summary>
    internal static bool IsAdjacent(CharacterController actor, CharacterController target)
        => actor != null && target != null && actor.GetMinimumDistanceToTarget(target, chebyshev: true) == 1;

    public IEnumerator Execute(ActionContext ctx, ActionOutcome o)
    {
        GameManager gm = ExecutorUtil.Gm;
        o.Performed = gm != null && gm.ActionCore_Engulf(ctx.Actor, ctx.Target);
        if (!o.Performed)
            o.Refuse(Legality.Refused(LegalityBar.Other, "engulf was not used"));
        yield break;
    }
}
