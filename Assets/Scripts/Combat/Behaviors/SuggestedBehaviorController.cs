using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A suggested creature's turn (CombatConditionType.Suggested; the bard's Suggestion, PHB p.29 and p.285; CHR-026). The
/// one course modelled, leave the fight, makes the creature move away from the suggester as far as it can (a withdraw
/// while the full round is free, else a move) and take no hostile action; when it cannot get farther it does nothing.
/// One behaviour for PCs and NPCs: AIService calls it for an NPC, GameManager.StartPCTurn for a PC (a forced turn, as for
/// confusion). A fascinated creature takes no actions at all, so the fascination is handled first by both callers.
/// </summary>
public sealed class SuggestedBehaviorController
{
    public sealed class SuggestedTurnDecision
    {
        public CharacterController Suggester;
        public ConditionService.ActiveCondition Condition;
        public SuggestedConditionData Data;
    }

    public bool TryBuildDecision(GameManager gameManager, CharacterController actor, out SuggestedTurnDecision decision)
    {
        decision = null;
        if (gameManager == null || actor == null || actor.Stats == null || actor.IsDead)
            return false;
        if (!actor.HasCondition(CombatConditionType.Suggested))
            return false;

        List<ConditionService.ActiveCondition> active = gameManager.GetActiveConditions(actor);
        if (active == null)
            return false;
        for (int i = 0; i < active.Count; i++)
        {
            ConditionService.ActiveCondition condition = active[i];
            if (condition == null || ConditionRules.Normalize(condition.Type) != CombatConditionType.Suggested)
                continue;
            SuggestedConditionData data = condition.Data as SuggestedConditionData;
            decision = new SuggestedTurnDecision
            {
                Suggester = condition.Source != null ? condition.Source : data != null ? data.Caster : null,
                Condition = condition,
                Data = data
            };
            return true;
        }
        return false;
    }

    public IEnumerator ExecuteDecision(GameManager gameManager, CharacterController actor, SuggestedTurnDecision decision)
    {
        if (gameManager == null || actor == null || actor.Stats == null || decision == null)
            yield break;

        string actorName = actor.Stats.CharacterName;
        CharacterController suggester = decision.Suggester;
        if (suggester == null || suggester.Stats == null)
        {
            gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("💬", $"{actorName} follows a suggestion and stays out of the fight."));
            yield return new WaitForSeconds(0.25f);
            yield break;
        }

        // A prone creature cannot move away, so it stands up first (a move action that provokes; PHB p.143), through the
        // same resolution as any prone creature's turn (CMB-074), then leaves with what action it has left.
        if (actor.HasCondition(CombatConditionType.Prone) && !actor.IsGrappling())
        {
            yield return gameManager.StartCoroutine(gameManager.TryStandUpFromProneForAI(actor));
            if (gameManager.CurrentPhase == GameManager.TurnPhase.CombatOver || actor.IsDead || ThreatSystem.IsMoverIncapacitated(actor))
                yield break;
        }

        Vector2Int from = suggester.GridPosition;
        int currentDistance = SquareGridUtils.GetDistance(actor.GridPosition, from);
        if (actor.Actions != null && !actor.Stats.MovementBlockedByCondition && !actor.IsGrappling())
        {
            // The withdraw it may take, full-round or restricted (the shared movement legality, PHB p.143-144, GRID-022).
            if (MovementService.CanStartMovement(actor, MovementService.WithdrawKindFor(actor)).Ok)
            {
                SquareCell withdrawCell = FindFartherCell(gameManager, actor, from, withdraw: true, currentDistance);
                if (withdrawCell != null)
                {
                    var withdrawAttempt = new MoveAttempt();
                    yield return gameManager.StartCoroutine(
                        gameManager.ExecuteWithdrawMovementForAI(actor, withdrawCell.Coords, gameManager.GetPlayerMoveSecondsPerStepForAI(), withdrawAttempt));
                    if (withdrawAttempt.Started)
                    {
                        gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("💬", $"{actorName} follows {suggester.Stats.CharacterName}'s suggestion and withdraws from the fight."));
                        yield return new WaitForSeconds(0.3f);
                        yield break;
                    }
                    if (gameManager.CurrentPhase == GameManager.TurnPhase.CombatOver || !actor.CanTakeTurnActions())
                        yield break;
                }
            }

            if (actor.Actions.HasMoveAction || actor.Actions.CanConvertStandardToMove)
            {
                SquareCell moveCell = FindFartherCell(gameManager, actor, from, withdraw: false, currentDistance);
                if (moveCell != null)
                {
                    var moveAttempt = new MoveAttempt();
                    yield return gameManager.StartCoroutine(
                        gameManager.MoveCharacterAlongComputedPathForAI(
                            MoveRequest.ForAI(actor, moveCell.Coords, gameManager.GetPlayerMoveSecondsPerStepForAI(), MoveSpend.MoveAction, moveAttempt)
                                .ConvertingStandard()));
                    // The executor spent the move action (or converted the standard action) before the first step (D3).
                    if (!moveAttempt.Refused) // a refused move spends nothing and falls through
                    {
                        gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("💬", $"{actorName} follows {suggester.Stats.CharacterName}'s suggestion and moves away from the fight."));
                        yield return new WaitForSeconds(0.3f);
                        yield break;
                    }
                }
            }
        }

        gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("💬", $"{actorName} follows {suggester.Stats.CharacterName}'s suggestion and stays out of the fight."));
        yield return new WaitForSeconds(0.25f);
    }

    /// <summary>The reachable cell farthest from <paramref name="from"/> (fewest attacks of opportunity on a tie), or null.</summary>
    private static SquareCell FindFartherCell(GameManager gameManager, CharacterController actor, Vector2Int from, bool withdraw, int currentDistance)
    {
        if (gameManager == null || gameManager.Grid == null)
            return null;

        int moveRange = gameManager.GetCurrentMoveRangeSquares(actor);
        int maxRange = withdraw ? Mathf.Max(0, gameManager.GetWithdrawMoveRangeSquares(actor)) : Mathf.Max(0, moveRange); // speed for a Restricted Withdraw (PHB p.144)
        if (maxRange <= 0)
            return null;

        List<SquareCell> candidates = gameManager.Grid.GetCellsInRange(actor.GridPosition, maxRange);
        SquareCell best = null;
        int bestDistance = currentDistance;
        int bestProvokes = int.MaxValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            SquareCell cell = candidates[i];
            if (cell == null || cell.Coords == actor.GridPosition)
                continue;
            int distance = SquareGridUtils.GetDistance(cell.Coords, from);
            if (distance <= currentDistance)
                continue;
            if (!gameManager.Grid.CanPlaceCreature(cell.Coords, actor.GetVisualSquaresOccupied(), actor))
                continue;

            AoOPathResult path = gameManager.FindPath(actor, cell.Coords, avoidThreats: false, maxRangeOverride: maxRange,
                allowThroughAllies: true, allowThroughEnemies: false, suppressFirstSquareAoO: withdraw);
            if (path == null || path.Path == null || path.Path.Count == 0)
                continue;

            int provokes = path.ProvokedAoOs != null ? path.ProvokedAoOs.Count : 0;
            if (distance > bestDistance || (distance == bestDistance && provokes < bestProvokes))
            {
                best = cell;
                bestDistance = distance;
                bestProvokes = provokes;
            }
        }
        return best;
    }
}
