/// <summary>
/// The AI's reads of the legal set (batch 14 step 10, AI-080; docs/designs/personalities_batch14.md, "AI-063, AI-047,
/// AI-080"): an AI routine asks these before it picks an action, so an AI-run creature, NPC or party member, never picks
/// one its core would refuse (a nauseated or slowed creature's attack, a nauseated mouther's spittle or engulf). Each
/// reads <see cref="ActorActionSet.AllowsNow"/> (the entry's executor CanStart, then the turn's compulsion filter), the
/// same legality the PC action panel and the shadow record read, so both sides follow one rule; the AI only chooses
/// among what it allows. Pure: no dice, no log, no state change.
/// </summary>
public static class AiLegal
{
    /// <summary>
    /// Whether <paramref name="actor"/> may declare a weapon attack now (the attack entry: an attack action left, or an
    /// attack sequence already started, PHB p.139, p.143; a nauseated creature is unable to attack, PHB p.310). Whether a given
    /// target may be attacked is the attack guard's (AiAttackGuard).
    /// </summary>
    public static bool CanAttackNow(CharacterController actor, GameManager gm = null)
        => ActorActionSet.AllowsNow(gm ?? GameManager.Instance, actor, ActionIds.Attack);

    /// <summary>
    /// Whether <paramref name="actor"/> may start a move action's movement now, as the AI's move takes it: the move entry
    /// (MovementService.CanStartMovement(Move): prone, pinned, grappling, blocked, a failed Balance check in grease, a
    /// 5-foot step taken, no speed; ActionCosts.CanAfford(Move)) and the move action itself. The AI's move request
    /// (MoveRequest.ForAI) spends the move action only and never converts the standard action (PHB p.141), so a turn
    /// whose move action is spent reads false here even when the standard action is left: converting it is a choice the
    /// AI does not make yet (the plan's "CanAfford(Move)" as built, recorded in the plan's step 10).
    /// </summary>
    public static bool CanMoveNow(CharacterController actor, GameManager gm = null)
        => actor != null && actor.Actions != null && actor.Actions.HasMoveAction
           && ActorActionSet.AllowsNow(gm ?? GameManager.Instance, actor, ActionIds.Move);

    /// <summary>Whether <paramref name="actor"/> has the catalog action <paramref name="id"/> and may take it now (no target yet).</summary>
    public static bool CanUse(CharacterController actor, string id, GameManager gm = null)
        => ActorActionSet.AllowsNow(gm ?? GameManager.Instance, actor, id);
}
