using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// D&D 3.5e Frightened behavior controller.
/// Frightened and panicked creatures must flee from their fear source when possible, PC or NPC: the AI turn calls it
/// for an NPC, GameManager.TryRunCompulsionTurn for a PC's forced turn (batch 12 step 7, owner decision 2026-10-23, Q1).
/// If they cannot flee, a frightened creature fights defensively (AI-044) and a panicked one cowers without attacking.
/// <see cref="Flee"/> is the one flight search, also used by turned creatures (AIService.ExecuteTurnedTurn).
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

        if (!actor.HasCondition(CombatConditionType.Frightened) && !actor.HasCondition(CombatConditionType.Panicked))
            return false;

        List<ConditionService.ActiveCondition> active = gameManager.GetActiveConditions(actor);
        if (active == null || active.Count == 0)
            return false;

        for (int i = 0; i < active.Count; i++)
        {
            ConditionService.ActiveCondition condition = active[i];
            if (condition == null)
                continue;
            CombatConditionType normalized = ConditionRules.Normalize(condition.Type);
            if (normalized != CombatConditionType.Frightened && normalized != CombatConditionType.Panicked)
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

    /// <summary>What a flight did this turn (<see cref="Flee"/>).</summary>
    public enum FlightOutcome
    {
        /// <summary>It moved away, or spent its action on a grapple escape attempt: the turn is used.</summary>
        Fled,
        /// <summary>No flight was possible: no cell farther from the source and no grapple to escape.</summary>
        Cornered,
        /// <summary>The fear's source is gone or dead; the fear ended and nothing was done.</summary>
        SourceGone
    }

    /// <summary>The outcome holder a caller passes to <see cref="ExecuteDecision"/> or <see cref="Flee"/>.</summary>
    public sealed class FlightResult
    {
        public FlightOutcome Outcome = FlightOutcome.Cornered;
    }

    /// <summary>
    /// The frightened or panicked creature's turn, PC or NPC: flee from the source (<see cref="Flee"/>); when cornered a
    /// frightened creature fights defensively (AI-044) and a panicked one cowers and does not attack (PHB glossary p.311;
    /// the total defense it would typically take is not modelled, CMB-179). <paramref name="result"/> reports the outcome,
    /// so a PC's forced turn can give a cornered frightened PC its menu back (PHB p.310: it may fight).
    /// </summary>
    public IEnumerator ExecuteDecision(GameManager gameManager, CharacterController actor, FrightenedTurnDecision decision, FlightResult result = null)
    {
        result ??= new FlightResult();
        if (gameManager == null || actor == null || actor.Stats == null || decision == null)
            yield break;

        CharacterController source = decision.CasterSource;
        if (source == null || source.Stats == null || source.Stats.IsDead)
        {
            // GameManager.EvaluateTurnCompulsion already ends such a fear before the turn is decided (batch 12 step 7);
            // this stays as a safety net (CMB-178: not RAW, a fear outlasts its source).
            EndFearWithoutSource(gameManager, actor);
            result.Outcome = FlightOutcome.SourceGone;
            yield break;
        }

        bool isPanicked = actor.HasCondition(CombatConditionType.Panicked);
        string actorName = actor.Stats.CharacterName;

        yield return gameManager.StartCoroutine(Flee(gameManager, actor, source, isPanicked ? " (Panicked!)" : string.Empty, result));
        if (result.Outcome != FlightOutcome.Cornered)
            yield break;

        if (isPanicked)
        {
            gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("😰", $"{actorName} is cornered and cowers in panic, unable to attack."));
        }
        else
        {
            actor.SetFightingDefensively(true);
            gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Defensive("🛡", $"{actorName} is cornered by fear and fights defensively."));
        }
        yield return new WaitForSeconds(0.25f);
    }

    /// <summary>
    /// Ends a fear whose source is gone or dead (the existing shortcut, CMB-178), with its log line. Shared by the turn
    /// start (GameManager.EvaluateTurnCompulsion) and <see cref="ExecuteDecision"/>.
    /// </summary>
    public static void EndFearWithoutSource(GameManager gameManager, CharacterController actor)
    {
        if (gameManager == null || actor == null || actor.Stats == null)
            return;
        gameManager.RemoveCondition(actor, CombatConditionType.Frightened);
        gameManager.RemoveCondition(actor, CombatConditionType.Panicked);
        gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Info("😌", $"{actor.Stats.CharacterName} is no longer frightened."));
    }

    /// <summary>
    /// The one flight of a creature that must flee from <paramref name="source"/>, PC or NPC: frightened, panicked
    /// (PHB glossary p.310-311) or turned (PHB p.159, "by the best and fastest means available"; AIService.ExecuteTurnedTurn).
    /// A grappling creature first tries to escape the grapple (the shared AI escape, GameManager.RunGrappleEscapeForFlight;
    /// PHB p.156: escaping is its only way to flee), then flees with what is left. Otherwise: withdraw (a full-round
    /// action, the first square does not provoke), else run, else one move action (a standard action converted to a move
    /// counts, so a creature that stood up still flees). <paramref name="result"/> is Fled when it moved or tried to
    /// escape, Cornered when it could do neither.
    /// </summary>
    public static IEnumerator Flee(GameManager gameManager, CharacterController actor, CharacterController source, string logSuffix, FlightResult result)
    {
        result ??= new FlightResult();
        result.Outcome = FlightOutcome.Cornered;
        if (gameManager == null || actor == null || actor.Stats == null || source == null || source.Stats == null)
            yield break;

        logSuffix ??= string.Empty;
        string actorName = actor.Stats.CharacterName;
        string sourceName = source.Stats.CharacterName;

        if (actor.IsGrappling())
        {
            result.Outcome = FlightOutcome.Fled;
            yield return gameManager.StartCoroutine(gameManager.RunGrappleEscapeForFlight(actor));
            if (gameManager.CurrentPhase == GameManager.TurnPhase.CombatOver || actor.IsGrappling() || !actor.CanTakeTurnActions())
                yield break;
            // Free of the grapple: flee on with the actions left.
        }

        int currentDistance = SquareGridUtils.GetDistance(actor.GridPosition, source.GridPosition);

        if (actor.Actions != null && actor.Actions.HasFullRoundAction && !actor.Stats.MovementBlockedByCondition)
        {
            SquareCell withdrawCell = FindBestFleeCell(gameManager, actor, source.GridPosition, withdraw: true, running: false, currentDistance);
            if (withdrawCell != null && withdrawCell.Coords != actor.GridPosition)
            {
                result.Outcome = FlightOutcome.Fled;
                yield return gameManager.StartCoroutine(
                    gameManager.ExecuteWithdrawMovementForAI(actor, withdrawCell.Coords, gameManager.GetPlayerMoveSecondsPerStepForAI()));

                gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("", $"😱 {actorName} flees in terror from {sourceName}.{logSuffix}"));
                yield return new WaitForSeconds(0.35f);
                yield break;
            }

            SquareCell runCell = FindBestFleeCell(gameManager, actor, source.GridPosition, withdraw: false, running: true, currentDistance);
            if (runCell != null && runCell.Coords != actor.GridPosition)
            {
                result.Outcome = FlightOutcome.Fled;
                yield return gameManager.StartCoroutine(
                    gameManager.MoveCharacterAlongComputedPathForAI(actor, runCell.Coords, gameManager.GetPlayerMoveSecondsPerStepForAI()));
                if (gameManager.CurrentPhase == GameManager.TurnPhase.CombatOver || !actor.CanTakeTurnActions())
                    yield break; // dropped by an attack of opportunity on the way (CMB-073)

                actor.Actions.UseFullRoundAction();
                gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Warning("🏃", $"{actorName} runs from {sourceName}, provoking attacks of opportunity!{logSuffix}"));
                yield return new WaitForSeconds(0.35f);
                yield break;
            }
        }

        if (HasAnyMoveAction(actor) && !actor.Stats.MovementBlockedByCondition)
        {
            SquareCell fleeCell = FindBestFleeCell(gameManager, actor, source.GridPosition, withdraw: false, running: false, currentDistance);
            if (fleeCell != null && fleeCell.Coords != actor.GridPosition)
            {
                result.Outcome = FlightOutcome.Fled;
                yield return gameManager.StartCoroutine(
                    gameManager.MoveCharacterAlongComputedPathForAI(actor, fleeCell.Coords, gameManager.GetPlayerMoveSecondsPerStepForAI()));
                if (gameManager.CurrentPhase == GameManager.TurnPhase.CombatOver || !actor.CanTakeTurnActions())
                    yield break; // dropped by an attack of opportunity on the way (CMB-073)

                ConsumeMoveAction(actor);
                gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("😱", $"{actorName} runs from {sourceName} in terror!{logSuffix}"));
                yield return new WaitForSeconds(0.35f);
                yield break;
            }
        }
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

        int moveRange = gameManager.GetCurrentMoveRangeSquares(actor);
        int maxRange = withdraw
            ? Mathf.Max(0, moveRange * 2)
            : Mathf.Max(0, moveRange);

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
