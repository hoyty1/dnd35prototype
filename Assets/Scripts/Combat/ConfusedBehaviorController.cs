using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// D&D 3.5e Confused turn controller.
/// Rolls d% once at turn start and executes the corresponding forced behavior.
/// </summary>
public sealed class ConfusedBehaviorController
{
    public enum ConfusedTurnMode
    {
        ActNormally,
        AttackCasterOrSelf,
        Babble,
        FleeFromCaster,
        AttackNearestCreature
    }

    public sealed class ConfusedTurnDecision
    {
        public int Roll;
        public ConfusedTurnMode Mode;
        public CharacterController CasterSource;
        public CharacterController PreferredTarget;
    }

    public bool TryRollDecision(GameManager gameManager, CharacterController actor, out ConfusedTurnDecision decision)
    {
        decision = null;
        if (gameManager == null || actor == null || actor.Stats == null)
            return false;

        if (!actor.HasCondition(CombatConditionType.Confused))
            return false;

        int roll = Random.Range(1, 101); // d%
        CharacterController caster = ResolveConfusionSource(gameManager, actor);

        var resolved = new ConfusedTurnDecision
        {
            Roll = roll,
            CasterSource = caster,
            Mode = ResolveMode(roll)
        };

        switch (resolved.Mode)
        {
            case ConfusedTurnMode.AttackCasterOrSelf:
                resolved.PreferredTarget = caster;
                break;
            case ConfusedTurnMode.AttackNearestCreature:
                resolved.PreferredTarget = FindNearestCreature(gameManager, actor, excludeSelf: true);
                break;
        }

        decision = resolved;
        return true;
    }

    public IEnumerator ExecuteDecision(GameManager gameManager, CharacterController actor, ConfusedTurnDecision decision)
    {
        if (gameManager == null || actor == null || actor.Stats == null || decision == null)
            yield break;

        string actorName = actor.Stats.CharacterName;
        string casterName = decision.CasterSource != null && decision.CasterSource.Stats != null
            ? decision.CasterSource.Stats.CharacterName
            : "the confusion source";

        gameManager.CombatUI?.ShowCombatLog($"🌀 {actorName} is confused (d% {decision.Roll:00}).");

        switch (decision.Mode)
        {
            case ConfusedTurnMode.ActNormally:
                gameManager.CombatUI?.ShowCombatLog($"🌀 {actorName} fights through confusion and acts normally.");
                yield break;

            case ConfusedTurnMode.Babble:
                gameManager.CombatUI?.ShowCombatLog($"🌀 {actorName} babbles incoherently and does nothing.");
                yield return new WaitForSeconds(0.35f);
                yield break;

            case ConfusedTurnMode.AttackCasterOrSelf:
                if (decision.PreferredTarget != null
                    && decision.PreferredTarget.Stats != null
                    && !decision.PreferredTarget.Stats.IsDead
                    && actor.IsTargetInCurrentWeaponRange(decision.PreferredTarget))
                {
                    gameManager.CombatUI?.ShowCombatLog($"🌀 {actorName} lashes out at the confusion source ({casterName})!");
                    yield return gameManager.StartCoroutine(gameManager.NPCPerformAttackForAI(actor, decision.PreferredTarget));
                    yield break;
                }

                yield return ResolveSelfAttack(actor, gameManager, $"🌀 {actorName} cannot reach {casterName} and attacks itself in confusion!");
                yield break;

            case ConfusedTurnMode.FleeFromCaster:
                {
                    Vector2Int fleeAnchor = decision.CasterSource != null ? decision.CasterSource.GridPosition : actor.GridPosition;
                    bool moved = false;
                    if (HasAnyMoveAction(actor))
                    {
                        SquareCell fleeCell = FindBestMovementCell(gameManager, actor, fleeAnchor, maximizeDistance: true);
                        if (fleeCell != null && fleeCell.Coords != actor.GridPosition)
                        {
                            gameManager.CombatUI?.ShowCombatLog($"🌀 {actorName} panics and flees from {casterName}!");
                            yield return gameManager.StartCoroutine(gameManager.MoveCharacterAlongComputedPathForAI(actor, fleeCell.Coords, gameManager.GetPlayerMoveSecondsPerStepForAI()));
                            ConsumeMoveAction(actor);
                            moved = true;
                        }
                    }

                    if (!moved)
                        gameManager.CombatUI?.ShowCombatLog($"🌀 {actorName} tries to flee from {casterName} but cannot move.");

                    yield return new WaitForSeconds(0.25f);
                    yield break;
                }

            case ConfusedTurnMode.AttackNearestCreature:
                {
                    CharacterController target = decision.PreferredTarget;
                    if (target == null || target.Stats == null || target.Stats.IsDead)
                    {
                        gameManager.CombatUI?.ShowCombatLog($"🌀 {actorName} is too disoriented to find a target.");
                        yield break;
                    }

                    if (!actor.IsTargetInCurrentWeaponRange(target) && HasAnyMoveAction(actor))
                    {
                        SquareCell approach = FindBestMovementCell(gameManager, actor, target.GridPosition, maximizeDistance: false);
                        if (approach != null && approach.Coords != actor.GridPosition)
                        {
                            yield return gameManager.StartCoroutine(gameManager.MoveCharacterAlongComputedPathForAI(actor, approach.Coords, gameManager.GetPlayerMoveSecondsPerStepForAI()));
                            ConsumeMoveAction(actor);
                        }
                    }

                    if (actor.IsTargetInCurrentWeaponRange(target))
                    {
                        gameManager.CombatUI?.ShowCombatLog($"🌀 {actorName} attacks the nearest creature ({target.Stats.CharacterName})!");
                        yield return gameManager.StartCoroutine(gameManager.NPCPerformAttackForAI(actor, target));
                    }
                    else
                    {
                        gameManager.CombatUI?.ShowCombatLog($"🌀 {actorName} stumbles toward {target.Stats.CharacterName} but cannot attack this turn.");
                    }

                    yield break;
                }
        }
    }

    private static ConfusedTurnMode ResolveMode(int roll)
    {
        if (roll <= 10) return ConfusedTurnMode.AttackCasterOrSelf;
        if (roll <= 20) return ConfusedTurnMode.ActNormally;
        if (roll <= 50) return ConfusedTurnMode.Babble;
        if (roll <= 70) return ConfusedTurnMode.FleeFromCaster;
        return ConfusedTurnMode.AttackNearestCreature;
    }

    private static CharacterController ResolveConfusionSource(GameManager gameManager, CharacterController actor)
    {
        if (gameManager == null || actor == null)
            return null;

        List<ConditionService.ActiveCondition> active = gameManager.GetActiveConditions(actor);
        if (active != null)
        {
            for (int i = 0; i < active.Count; i++)
            {
                ConditionService.ActiveCondition condition = active[i];
                if (condition == null)
                    continue;
                if (ConditionRules.Normalize(condition.Type) != CombatConditionType.Confused)
                    continue;

                if (condition.Source != null && condition.Source.Stats != null && !condition.Source.Stats.IsDead)
                    return condition.Source;

                if (!string.IsNullOrWhiteSpace(condition.SourceName))
                {
                    CharacterController sourceByName = FindCharacterByName(gameManager, condition.SourceName);
                    if (sourceByName != null)
                        return sourceByName;
                }
            }
        }

        return FindNearestCreature(gameManager, actor, excludeSelf: true);
    }

    private static CharacterController FindCharacterByName(GameManager gameManager, string sourceName)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
            return null;

        List<CharacterController> all = gameManager.GetAllCharactersForAI();
        for (int i = 0; i < all.Count; i++)
        {
            CharacterController c = all[i];
            if (c == null || c.Stats == null || c.Stats.IsDead)
                continue;

            if (string.Equals(c.Stats.CharacterName, sourceName, System.StringComparison.Ordinal))
                return c;
        }

        return null;
    }

    private static CharacterController FindNearestCreature(GameManager gameManager, CharacterController actor, bool excludeSelf)
    {
        List<CharacterController> all = gameManager.GetAllCharactersForAI();
        CharacterController best = null;
        int bestDistance = int.MaxValue;

        for (int i = 0; i < all.Count; i++)
        {
            CharacterController candidate = all[i];
            if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (excludeSelf && candidate == actor)
                continue;

            int dist = SquareGridUtils.GetDistance(actor.GridPosition, candidate.GridPosition);
            if (dist < bestDistance)
            {
                bestDistance = dist;
                best = candidate;
            }
        }

        return best;
    }

    private static SquareCell FindBestMovementCell(GameManager gameManager, CharacterController actor, Vector2Int anchor, bool maximizeDistance)
    {
        if (gameManager == null || gameManager.Grid == null || actor == null || actor.Stats == null)
            return null;

        int moveRange = gameManager.GetCurrentMoveRangeSquares(actor);
        List<SquareCell> candidates = gameManager.Grid.GetCellsInRange(actor.GridPosition, Mathf.Max(0, moveRange));
        SquareCell best = null;
        int bestDistance = maximizeDistance ? int.MinValue : int.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            SquareCell cell = candidates[i];
            if (cell == null)
                continue;

            if (!gameManager.Grid.CanPlaceCreature(cell.Coords, actor.GetVisualSquaresOccupied(), actor))
                continue;

            int dist = SquareGridUtils.GetDistance(cell.Coords, anchor);
            if (maximizeDistance)
            {
                if (dist > bestDistance)
                {
                    bestDistance = dist;
                    best = cell;
                }
            }
            else
            {
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    best = cell;
                }
            }
        }

        return best;
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

    private static IEnumerator ResolveSelfAttack(CharacterController actor, GameManager gameManager, string log)
    {
        if (actor == null || actor.Stats == null)
            yield break;

        gameManager.CombatUI?.ShowCombatLog(log);

        int raw = Mathf.Max(1, actor.Stats.RollDamage());
        var packet = new DamagePacket
        {
            RawDamage = raw,
            Types = new HashSet<DamageType> { DamageType.Bludgeoning },
            AttackTags = DamageBypassTag.Bludgeoning,
            IsRanged = false,
            IsNonlethal = false,
            Source = AttackSource.Other,
            SourceName = "Confusion"
        };

        int hpBefore = actor.Stats.CurrentHP;
        DamageResolutionResult result = actor.Stats.ApplyIncomingDamage(raw, packet);
        int hpAfter = actor.Stats.CurrentHP;

        gameManager.CombatUI?.ShowCombatLog($"🩸 {actor.Stats.CharacterName} hits itself for {result.FinalDamage} damage ({hpBefore} → {hpAfter} HP).");

        yield return new WaitForSeconds(0.35f);
    }
}
