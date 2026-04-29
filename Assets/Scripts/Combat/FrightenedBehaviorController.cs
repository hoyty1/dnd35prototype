using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// D&D 3.5e Frightened behavior controller.
/// Frightened creatures must flee from their fear source when possible.
/// If they cannot flee, they fight defensively while fear penalties remain active.
/// </summary>
public sealed class FrightenedBehaviorController
{
    public sealed class FrightenedTurnDecision
    {
        public CharacterController CasterSource;
        public ConditionService.ActiveCondition Condition;
        public FrightenedConditionData FearData;
    }

    public bool TryBuildDecision(GameManager gameManager, CharacterController actor, out FrightenedTurnDecision decision)
    {
        decision = null;
        if (gameManager == null || actor == null || actor.Stats == null || actor.Stats.IsDead)
            return false;

        if (!actor.HasCondition(CombatConditionType.Frightened))
            return false;

        List<ConditionService.ActiveCondition> active = gameManager.GetActiveConditions(actor);
        if (active == null || active.Count == 0)
            return false;

        for (int i = 0; i < active.Count; i++)
        {
            ConditionService.ActiveCondition condition = active[i];
            if (condition == null || ConditionRules.Normalize(condition.Type) != CombatConditionType.Frightened)
                continue;

            CharacterController source = condition.Source;
            FrightenedConditionData data = condition.Data as FrightenedConditionData;
            if (data != null)
            {
                data.RefreshRemainingRounds(condition.RemainingRounds);
                if (source == null)
                    source = data.Caster;
            }

            if (source == null && !string.IsNullOrWhiteSpace(condition.SourceName))
                source = FindCharacterByName(gameManager, condition.SourceName);

            decision = new FrightenedTurnDecision
            {
                CasterSource = source,
                Condition = condition,
                FearData = data
            };
            return true;
        }

        return false;
    }

    public IEnumerator ExecuteDecision(GameManager gameManager, CharacterController actor, FrightenedTurnDecision decision)
    {
        if (gameManager == null || actor == null || actor.Stats == null || decision == null)
            yield break;

        CharacterController source = decision.CasterSource;
        if (source == null || source.Stats == null || source.Stats.IsDead)
        {
            gameManager.RemoveCondition(actor, CombatConditionType.Frightened);
            gameManager.CombatUI?.ShowCombatLog($"😌 {actor.Stats.CharacterName} is no longer frightened.");
            yield break;
        }

        string actorName = actor.Stats.CharacterName;
        string sourceName = source.Stats.CharacterName;

        int currentDistance = SquareGridUtils.GetDistance(actor.GridPosition, source.GridPosition);

        if (actor.Actions != null && actor.Actions.HasFullRoundAction && !actor.Stats.MovementBlockedByCondition)
        {
            SquareCell withdrawCell = FindBestFleeCell(gameManager, actor, source.GridPosition, withdraw: true, running: false, currentDistance);
            if (withdrawCell != null && withdrawCell.Coords != actor.GridPosition)
            {
                yield return gameManager.StartCoroutine(
                    gameManager.ExecuteWithdrawMovementForAI(actor, withdrawCell.Coords, gameManager.GetPlayerMoveSecondsPerStepForAI()));

                gameManager.CombatUI?.ShowCombatLog($"😱 {actorName} flees in terror from {sourceName}.");
                yield return new WaitForSeconds(0.35f);
                yield break;
            }

            SquareCell runCell = FindBestFleeCell(gameManager, actor, source.GridPosition, withdraw: false, running: true, currentDistance);
            if (runCell != null && runCell.Coords != actor.GridPosition)
            {
                yield return gameManager.StartCoroutine(
                    gameManager.MoveCharacterAlongComputedPathForAI(actor, runCell.Coords, gameManager.GetPlayerMoveSecondsPerStepForAI()));

                actor.Actions.UseFullRoundAction();
                gameManager.CombatUI?.ShowCombatLog($"🏃 {actorName} runs from {sourceName}, provoking attacks of opportunity!");
                yield return new WaitForSeconds(0.35f);
                yield break;
            }
        }

        if (HasAnyMoveAction(actor) && !actor.Stats.MovementBlockedByCondition)
        {
            SquareCell fleeCell = FindBestFleeCell(gameManager, actor, source.GridPosition, withdraw: false, running: false, currentDistance);
            if (fleeCell != null && fleeCell.Coords != actor.GridPosition)
            {
                yield return gameManager.StartCoroutine(
                    gameManager.MoveCharacterAlongComputedPathForAI(actor, fleeCell.Coords, gameManager.GetPlayerMoveSecondsPerStepForAI()));

                ConsumeMoveAction(actor);
                gameManager.CombatUI?.ShowCombatLog($"😱 {actorName} runs from {sourceName} in terror!");
                yield return new WaitForSeconds(0.35f);
                yield break;
            }
        }

        actor.SetFightingDefensively(true);
        gameManager.CombatUI?.ShowCombatLog($"🛡 {actorName} is cornered by fear and fights defensively.");
        yield return new WaitForSeconds(0.25f);
    }

    private static SquareCell FindBestFleeCell(
        GameManager gameManager,
        CharacterController actor,
        Vector2Int sourcePos,
        bool withdraw,
        bool running,
        int currentDistance)
    {
        if (gameManager == null || gameManager.Grid == null || actor == null || actor.Stats == null)
            return null;

        int maxRange = withdraw
            ? Mathf.Max(1, actor.Stats.MoveRange * 2)
            : Mathf.Max(1, actor.Stats.MoveRange);

        List<SquareCell> candidates = gameManager.Grid.GetCellsInRange(actor.GridPosition, maxRange);
        SquareCell best = null;
        int bestDistance = currentDistance;
        int bestProvokes = int.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            SquareCell cell = candidates[i];
            if (cell == null || cell.Coords == actor.GridPosition)
                continue;

            int candidateDistance = SquareGridUtils.GetDistance(cell.Coords, sourcePos);
            if (candidateDistance <= currentDistance)
                continue;

            if (!gameManager.Grid.CanPlaceCreature(cell.Coords, actor.GetVisualSquaresOccupied(), actor))
                continue;

            AoOPathResult pathResult = gameManager.FindPath(
                actor,
                cell.Coords,
                avoidThreats: false,
                maxRangeOverride: maxRange,
                allowThroughAllies: true,
                allowThroughEnemies: false,
                suppressFirstSquareAoO: withdraw);

            if (pathResult == null || pathResult.Path == null || pathResult.Path.Count == 0)
                continue;

            int provokes = pathResult.ProvokedAoOs != null ? pathResult.ProvokedAoOs.Count : 0;
            if (candidateDistance > bestDistance || (candidateDistance == bestDistance && provokes < bestProvokes))
            {
                best = cell;
                bestDistance = candidateDistance;
                bestProvokes = provokes;
            }
        }

        return best;
    }

    private static CharacterController FindCharacterByName(GameManager gameManager, string characterName)
    {
        if (gameManager == null || string.IsNullOrWhiteSpace(characterName))
            return null;

        List<CharacterController> all = gameManager.GetAllCharactersForAI();
        for (int i = 0; i < all.Count; i++)
        {
            CharacterController candidate = all[i];
            if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                continue;

            if (string.Equals(candidate.Stats.CharacterName, characterName, System.StringComparison.Ordinal))
                return candidate;
        }

        return null;
    }

    private static bool HasAnyMoveAction(CharacterController actor)
    {
        if (actor == null || actor.Actions == null)
            return false;

        return actor.Actions.HasMoveAction || actor.Actions.CanConvertStandardToMove;
    }

    private static void ConsumeMoveAction(CharacterController actor)
    {
        if (actor == null || actor.Actions == null)
            return;

        if (actor.Actions.HasMoveAction)
            actor.Actions.UseMoveAction();
        else if (actor.Actions.CanConvertStandardToMove)
            actor.Actions.ConvertStandardToMove();
    }
}
