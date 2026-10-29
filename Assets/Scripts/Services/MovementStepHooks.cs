using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A rule that runs on every square of a creature's own movement, PC or NPC (batch 12 step 23, GRID-018): registered in
/// <see cref="MovementService"/> (<see cref="MovementService.RegisterStepHook"/>) and run by the one step executor,
/// <c>CharacterController.MoveAlongPath</c>, so every mover inherits it: the PC Move and Withdraw
/// (GameManager.ExecuteMovement), the AI movers and withdraws (ExecutePathWithMovementAoOs), the charge core, the
/// overrun's move and the crawl. The 5-foot step (<c>CharacterController.FiveFootStep</c>) runs the same list with
/// <see cref="MoveKind.FiveFootStep"/>. Forced movement (a bull rush push, a grapple drag, a teleport) moves through
/// <c>MoveToCell</c> and runs no step hook.
/// </summary>
public interface IMovementStepHook
{
    /// <summary>
    /// Before <paramref name="actor"/> steps from <paramref name="from"/> into <paramref name="to"/>. Setting
    /// <paramref name="stop"/> refuses the step: the mover stays on <paramref name="from"/> and its movement ends.
    /// </summary>
    void BeforeStep(CharacterController actor, Vector2Int from, Vector2Int to, MoveKind kind, ref bool stop);

    /// <summary>
    /// After <paramref name="actor"/> has entered <paramref name="to"/>. Setting <paramref name="stop"/> ends its movement
    /// there.
    /// </summary>
    void OnStep(CharacterController actor, Vector2Int from, Vector2Int to, MoveKind kind, ref bool stop);
}

/// <summary>
/// A mover that may no longer walk stops (batch 12 step 23, GRID-018): before each step, and after it, the mover is
/// refused when it is held in place (a condition that prevents movement, a web's hold, the tentacles' grapple, a pin) or
/// down (prone; only a crawl moves a prone creature, PHB p.142-143) - <see cref="MovementService.StepHaltReason"/>. The
/// test reads the mover's state, not what changed, so it holds whoever caused it (an area's entry reaction on this step,
/// an area's own per-frame tracking during the step's animation) and in the movers that walk one square per call (the PC
/// Move, the charge, the overrun): their next step is refused, and each stops where it stands.
/// </summary>
public sealed class MovementHaltStepHook : IMovementStepHook
{
    public void BeforeStep(CharacterController actor, Vector2Int from, Vector2Int to, MoveKind kind, ref bool stop)
    {
        if (!stop && MovementService.StepHaltReason(actor, kind) != null)
            stop = true;
    }

    public void OnStep(CharacterController actor, Vector2Int from, Vector2Int to, MoveKind kind, ref bool stop)
    {
        if (!stop && MovementService.StepHaltReason(actor, kind) != null)
            stop = true;
    }
}

/// <summary>
/// Grease on the ground (PHB p.237; decision D12 of docs/designs/pc_npc_parity_batch12.md): a creature moving within or
/// into a greased area makes one DC 10 Balance check a round, on its first step of greased movement
/// (<see cref="GreaseAreaEffect.CheckMovement"/>). A creature that failed one this round cannot move at all this round.
/// The half speed of a success is the greased squares' difficult-terrain cost (SquareGridUtils.StepCost).
/// </summary>
public sealed class GreaseStepHook : IMovementStepHook
{
    public void BeforeStep(CharacterController actor, Vector2Int from, Vector2Int to, MoveKind kind, ref bool stop)
    {
        if (actor == null || actor.Stats == null || actor.Stats.IsDead || !AreaEffectManager.HasInstance)
            return;

        List<GreaseAreaEffect> greases = AreaEffectManager.Instance.GetEffectsOfType<GreaseAreaEffect>();
        if (greases.Count == 0)
            return;

        // A mover the grease does not hinder makes no Balance check: an incorporeal creature (PHB p.148: difficult terrain
        // does not hamper it; it does not walk on the greased surface) or one under Freedom of Movement (PHB p.233: it moves
        // normally despite magic that impedes movement). Delegated RAW reading 2026-10-29 under the owner interpretation of
        // that date, docs/systems/RULES_COVERAGE.md 4.1; MovementTerrain.ZoneHindersMovement, the one exemption, which the
        // charge planner asks too. The answer depends only on the mover and the zone's kind.
        for (int i = 0; i < greases.Count; i++)
        {
            if (greases[i] == null)
                continue;
            if (!MovementTerrain.ZoneHindersMovement(actor, greases[i]))
                return;
            break;
        }

        int round = GameManager.Instance != null ? GameManager.Instance.CurrentRound : 0;

        // Failure "means it can't move that round" (PHB p.237): every later step this round is refused.
        for (int i = 0; i < greases.Count; i++)
        {
            GreaseAreaEffect g = greases[i];
            if (g != null && g.FailedBalanceThisRound(actor, round))
            {
                g.LogCannotMove(actor, round);
                stop = true;
                return;
            }
        }

        // The area the step moves into, else the one it moves within or out of.
        GreaseAreaEffect area = null;
        for (int i = 0; i < greases.Count && area == null; i++)
            if (greases[i] != null && greases[i].IsCellInArea(to))
                area = greases[i];
        for (int i = 0; i < greases.Count && area == null; i++)
            if (greases[i] != null && greases[i].IsCellInArea(from))
                area = greases[i];

        if (area != null)
            area.CheckMovement(actor, kind, round, ref stop);
    }

    public void OnStep(CharacterController actor, Vector2Int from, Vector2Int to, MoveKind kind, ref bool stop) { }
}

/// <summary>
/// Zone entry and exit on every step: each persistent area effect registers a mover that steps into it and runs its
/// entry reaction then (<see cref="PersistentAreaEffect.NotifyCreatureStep"/>), so a creature that crosses an area is
/// caught (Spike Stones, Entangle) and one that stops inside is not met again as a new entrant at the next round start
/// (the double entry of GRID-018). Areas with their own per-frame tracking (Black Tentacles, Wall of Fire, the fogs, the
/// walls) share the same registry, so each entry fires once. An entry that holds the mover or knocks it down ends its
/// movement there through <see cref="MovementHaltStepHook"/>, which runs after this hook too.
/// </summary>
public sealed class AreaZoneEntryStepHook : IMovementStepHook
{
    public void BeforeStep(CharacterController actor, Vector2Int from, Vector2Int to, MoveKind kind, ref bool stop) { }

    public void OnStep(CharacterController actor, Vector2Int from, Vector2Int to, MoveKind kind, ref bool stop)
    {
        if (actor == null || actor.Stats == null || actor.Stats.IsDead || !AreaEffectManager.HasInstance)
            return;

        List<PersistentAreaEffect> areas = AreaEffectManager.Instance.GetAllAreaEffects();
        for (int i = 0; i < areas.Count; i++)
        {
            if (areas[i] != null)
                areas[i].NotifyCreatureStep(actor);
        }
    }
}

/// <summary>
/// A breached Wall of Ice section deals its cold damage to a creature that moves through it (PHB p.299; formerly inline in
/// CharacterController.MoveAlongPath).
/// </summary>
public sealed class WallOfIceBreachStepHook : IMovementStepHook
{
    public void BeforeStep(CharacterController actor, Vector2Int from, Vector2Int to, MoveKind kind, ref bool stop) { }

    public void OnStep(CharacterController actor, Vector2Int from, Vector2Int to, MoveKind kind, ref bool stop)
    {
        if (actor != null && WallOfIceAreaEffect.IsCellBreachedWallOfIce(to))
            WallOfIceAreaEffect.ApplyBreachDamageAtCell(actor, to);
    }
}
