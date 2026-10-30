using System.Collections;
using System.Collections.Generic;
using DND35.AI;
using UnityEngine;

/// <summary>What <see cref="AiOutOfReach.Run"/> did.</summary>
public sealed class AiOutOfReachOutcome
{
    /// <summary>The alternative that was performed; null when none was.</summary>
    public OutOfReachAlternative? Performed;

    /// <summary>An attack was made (another target, or the attack after a move, AI-045).</summary>
    public bool Attacked;

    /// <summary>The weapon was reloaded first (the catalog's reload entry, ReloadActionExecutor; D4).</summary>
    public bool Reloaded;
}

/// <summary>
/// The AI's out-of-reach alternatives (batch 13 step 5; AI-075, AI-045; design choice C1, decision D1,
/// docs/systems/RULES_COVERAGE.md 4.1): when no target passes the creature's attack guard (AiAttackGuard), the ordered
/// list of its profile (AIProfile.GetOutOfReachAlternatives) is tried through the action catalog
/// (GameManager.RunAIAction with the attack, full attack, move, withdraw and total defense entries), and the first one
/// performed ends the list:
/// - AttackOtherTarget: the best target the guard allows (AIService.SelectGuardPassingTarget), with the full attack when
///   the full-round action is still free, else the attack action;
/// - PreferredRange: the move to the profile's preferred range of the focus target (the kiter's old approach move,
///   AIService.EvaluateMovementOptions with the profile), then, AI-045, the attack with the standard action left when
///   a target now passes the guard; skipped when the creature has no ranged means at all (AIService.HasRangedMeans: no
///   ranged weapon that can shoot, no throwable weapon, no castable spell beyond touch), so a bow without arrows never
///   backs off from an adjacent foe or walks to a range it cannot use (AI-039, AI-073);
/// - Approach: the move to the reach or range of the weapon in hand (<see cref="ApproachRangeSquares"/>), then the
///   attack as above; skipped when the weapon cannot shoot at all (no ammunition), since closing in cannot help;
/// - Withdraw: the withdraw away from the focus target (AIService.EvaluateWithdrawRetreatDestination; PHB p.143);
/// - TotalDefense: total defense (PHB p.142, D2).
/// A move to the creature's own square is no move. Before the list, a crossbow that is not loaded and can be reloaded
/// at all this turn (with a bolt to load: ReloadRules, PHB p.113-114) is reloaded through the catalog's reload entry
/// (batch 13 step 12, D4: the PC Reload button's executor, provoking as loading does, PHB p.115, p.99); the list then
/// runs with what the load left (AI-057: a free Rapid Reload load leaves the whole turn, a move-action load the
/// standard action), and ends at once when the load left no standard action. <paramref name="allowThrow"/> is the calling routine's guard: false for the melee routines, whose "another
/// target" and attack after a move count a throwable weapon's melee reach only (AiAttackGuard.IntentFor). An AI choice,
/// not a rule: batch 14 re-weights the list, batch 22 replaces it with the evaluator.
/// </summary>
public static class AiOutOfReach
{
    public static IEnumerator Run(GameManager gm, AIService ai, CharacterController npc, CharacterController focus,
        CombatStyle styleWithoutProfile, bool allowThrow, AiOutOfReachOutcome result = null)
    {
        if (result == null)
            result = new AiOutOfReachOutcome();
        if (gm == null || ai == null || npc == null || npc.Stats == null || Over(gm, npc))
            yield break;

        string name = npc.Stats.CharacterName;
        var reload = new ActionOutcome();
        yield return gm.TryReloadEquippedWeaponForAI(npc, reload);
        if (reload.Performed)
        {
            result.Reloaded = true;
            yield return new WaitForSeconds(0.8f);
            if (Over(gm, npc) || !ActionCosts.CanAfford(npc, ActionCostType.Standard))
                yield break;
        }
        else if (reload.Spent != ActionCostType.NotAnAction && reload.Spent != ActionCostType.Free)
        {
            yield break; // the load was paid and an attack of opportunity stopped it
        }

        AIProfile profile = npc.aiProfile;
        IReadOnlyList<OutOfReachAlternative> alternatives = profile != null
            ? profile.GetOutOfReachAlternatives(npc)
            : AIProfile.DefaultOutOfReachAlternatives(npc, styleWithoutProfile, castingProfile: false);
        if (alternatives == null || alternatives.Count == 0)
            yield break;
        if (focus == null || focus.Stats == null || focus.Stats.IsDead)
            focus = ai.SelectBestTarget(npc, gm.GetAllCharactersForAI());
        bool weaponCanShoot = AiAttackGuard.CheckWeapon(npc, null).Ok;
        bool hasRangedMeans = ai.HasRangedMeans(npc, focus);
        Debug.Log($"[AI][OutOfReach] {name}: no target passes the attack guard; alternatives {string.Join(",", alternatives)} (focus {(focus != null && focus.Stats != null ? focus.Stats.CharacterName : "none")})");

        for (int i = 0; i < alternatives.Count; i++)
        {
            if (Over(gm, npc))
                yield break;

            OutOfReachAlternative alternative = alternatives[i];
            switch (alternative)
            {
                case OutOfReachAlternative.AttackOtherTarget:
                {
                    CharacterController other = CanAttackNow(npc) ? ai.SelectGuardPassingTarget(npc, null, allowThrow) : null;
                    if (other == null)
                        break;
                    var attack = new ActionOutcome();
                    yield return Attack(gm, npc, other, attack);
                    if (!attack.Performed)
                        break;
                    result.Performed = alternative;
                    result.Attacked = true;
                    yield break;
                }

                case OutOfReachAlternative.PreferredRange:
                case OutOfReachAlternative.Approach:
                {
                    if (focus == null || focus.Stats == null || !CanMove(npc))
                        break;
                    if (alternative == OutOfReachAlternative.Approach && !weaponCanShoot)
                        break;
                    if (alternative == OutOfReachAlternative.PreferredRange && !hasRangedMeans)
                        break;
                    SquareCell cell = alternative == OutOfReachAlternative.PreferredRange
                        ? ai.EvaluateMovementOptions(npc, focus.GridPosition, retreat: false, profile: profile)
                        : ai.EvaluateMovementOptions(npc, focus.GridPosition, retreat: false, focus, profile, ApproachRangeSquares(npc, allowThrow));
                    if (cell == null || cell.Coords == npc.GridPosition)
                        break;

                    ActionContext moveCtx = ActionContext.For(ActionIds.Move, npc, ActorSource.AI);
                    moveCtx.Cell = cell.Coords;
                    var move = new ActionOutcome();
                    yield return gm.RunAIAction(moveCtx, move);
                    if (Over(gm, npc))
                        yield break;
                    if (!move.Performed)
                        break;

                    result.Performed = alternative;
                    gm.CombatUI?.ShowCombatLog(alternative == OutOfReachAlternative.PreferredRange
                        ? CombatLogHelper.Info("", $"{name} moves to get a better shot.")
                        : CombatLogHelper.Info("", $"{name} closes in on {focus.Stats.CharacterName}."));
                    yield return new WaitForSeconds(0.5f);

                    // AI-045: the move may have brought a target into reach or range; the standard action left attacks it.
                    if (Over(gm, npc) || !CanAttackNow(npc))
                        yield break;
                    CharacterController reached = ai.SelectGuardPassingTarget(npc, null, allowThrow);
                    if (reached != null)
                    {
                        var attack = new ActionOutcome();
                        yield return Attack(gm, npc, reached, attack);
                        result.Attacked = attack.Performed;
                    }
                    yield break;
                }

                case OutOfReachAlternative.Withdraw:
                {
                    if (focus == null || focus.Stats == null || npc.Actions == null
                        || !MovementService.CanStartMovement(npc, MovementService.WithdrawKindFor(npc)).Ok)
                        break;
                    SquareCell cell = ai.EvaluateWithdrawRetreatDestination(npc, focus.GridPosition);
                    if (cell == null || cell.Coords == npc.GridPosition)
                        break;
                    ActionContext withdrawCtx = ActionContext.For(ActionIds.Withdraw, npc, ActorSource.AI);
                    withdrawCtx.Cell = cell.Coords;
                    var withdraw = new ActionOutcome();
                    yield return gm.RunAIAction(withdrawCtx, withdraw);
                    if (!withdraw.Performed)
                        break;
                    result.Performed = alternative;
                    if (!Over(gm, npc))
                        gm.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{name} withdraws from {focus.Stats.CharacterName}."));
                    yield break;
                }

                case OutOfReachAlternative.TotalDefense:
                {
                    var defense = new ActionOutcome();
                    yield return gm.RunAIAction(ActionContext.For(ActionIds.TotalDefense, npc, ActorSource.AI), defense);
                    if (!defense.Performed)
                        break;
                    result.Performed = alternative;
                    yield break;
                }
            }
        }
    }

    /// <summary>
    /// The distance an approach aims for (squares from the focus target): with a ranged weapon its maximum range (ten
    /// increments, five thrown, PHB p.134), capped by the profile's preferred range when it has one; with a melee weapon,
    /// unarmed or natural attacks the farthest distance the creature can attack at, so a reach weapon stops at its reach,
    /// outside its dead zone (PHB p.112, p.137). A throwable melee weapon (a dagger) counts as a ranged one, five
    /// increments (PHB p.134), when the routine allows throws (<paramref name="allowThrow"/>: the kiter) and the creature
    /// fights from range (a Ranged profile or a casting one, C1), so a caster with a dagger stays at throwing range.
    /// </summary>
    public static int ApproachRangeSquares(CharacterController npc, bool allowThrow = false)
    {
        if (npc == null || npc.Stats == null)
            return 1;
        ItemData weapon = npc.GetEquippedMainWeapon();
        int preferred = npc.aiProfile != null && npc.aiProfile.Movement != null ? npc.aiProfile.Movement.PreferredRangeSquares : 0;
        if (weapon != null && weapon.WeaponCat == WeaponCategory.Ranged && weapon.RangeIncrement > 0)
        {
            int max = RangeCalculator.GetMaxRangeSquares(weapon.RangeIncrement, weapon.IsThrown);
            return preferred > 0 ? Mathf.Min(preferred, max) : max;
        }
        bool fightsFromRange = npc.aiProfile != null
            && (npc.aiProfile.CombatStyle == CombatStyle.Ranged || npc.aiProfile.IsCastingProfile);
        if (allowThrow && fightsFromRange && weapon != null && weapon.RangeIncrement > 0
            && GameManager.IsThrowableWeapon(weapon, meleeOnly: true))
        {
            int max = RangeCalculator.GetMaxRangeSquares(weapon.RangeIncrement, true);
            return preferred > 0 ? Mathf.Min(preferred, max) : max;
        }

        ItemData melee = weapon != null && weapon.WeaponCat == WeaponCategory.Melee ? weapon : null;
        int best = 1;
        for (int d = 1; d <= 6; d++)
            if (npc.CanMeleeAttackDistance(d, melee))
                best = d;
        return best;
    }

    private static IEnumerator Attack(GameManager gm, CharacterController npc, CharacterController target, ActionOutcome outcome)
    {
        string id = npc.Actions != null && npc.Actions.HasFullRoundAction ? ActionIds.FullAttack : ActionIds.Attack;
        yield return gm.RunAIAction(ActionContext.For(id, npc, ActorSource.AI, target), outcome);
    }

    private static bool CanAttackNow(CharacterController npc)
        => npc.CanTakeTurnActions() && npc.Actions != null
           && (ActionCosts.CanAfford(npc, ActionCostType.Standard) || npc.ProgressiveAttackPool.HasStartedAttacking);

    private static bool CanMove(CharacterController npc)
        => npc.Actions != null && npc.Actions.HasMoveAction && MovementService.CanStartMovement(npc, MoveKind.Move).Ok;

    private static bool Over(GameManager gm, CharacterController npc)
        => gm.CurrentPhase == GameManager.TurnPhase.CombatOver || npc.Stats == null || !npc.CanTakeTurnActions();
}
