using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// D&D 3.5e Confused turn controller.
/// Rolls d% once at turn start and executes the corresponding forced behavior. A prone creature stands up first when
/// its roll lets it act or move (D5, delegated RAW reading, batch 13 step 12, revised 2026-10-29 in its review; CMB-074):
/// on "attack the nearest creature" when it cannot already attack that creature as it lies, and on "flee" (its top speed
/// starts with standing up, as a frightened creature's flight does, D15), here for NPCs and the PC's forced turn alike;
/// and on "act normally" in the turn it then takes (the AI turn's own stand-up; a PC chooses for itself). It stays prone
/// on "babble". On "attack the caster" it does not stand yet: a caster out of reach still makes it attack itself (AI-043,
/// not RAW); when AI-043's close-with-the-caster fix lands, a prone creature stands first there too.
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

        int roll = DiceRoller.D100(); // d%
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

        gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("🌀", $"{actorName} is confused (d% {decision.Roll:00})."));

        switch (decision.Mode)
        {
            case ConfusedTurnMode.ActNormally:
                gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("🌀", $"{actorName} fights through confusion and acts normally."));
                yield break;

            case ConfusedTurnMode.Babble:
                gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("🌀", $"{actorName} babbles incoherently and does nothing."));
                yield return new WaitForSeconds(0.35f);
                yield break;

            case ConfusedTurnMode.AttackCasterOrSelf:
            {
                bool casterThere = decision.PreferredTarget != null
                    && decision.PreferredTarget.Stats != null
                    && !decision.PreferredTarget.Stats.IsDead;
                // The AI attack guard (batch 13 step 5, D1), melee reach for a throwable weapon (C1).
                Legality onCaster = casterThere
                    ? AiAttackGuard.Check(actor, decision.PreferredTarget, allowThrow: false)
                    : Legality.Refused(LegalityBar.Reach, "no confusion source");
                if (onCaster.Ok)
                {
                    gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("🌀", $"{actorName} lashes out at the confusion source ({casterName})!"));
                    yield return gameManager.StartCoroutine(gameManager.NPCPerformAttackForAI(actor, decision.PreferredTarget));
                    yield break;
                }

                // The self-attack (AI-043, not RAW: PHB p.212 has the creature close with the caster) keeps its old trigger,
                // the caster out of reach or range; another refusal (a Resilient Sphere, a known failed ward save, no
                // ammunition) is no attack at all.
                if (onCaster.Bar == LegalityBar.Reach || onCaster.Bar == LegalityBar.Range)
                    yield return ResolveSelfAttack(actor, gameManager, $"🌀 {actorName} cannot reach {casterName} and attacks itself in confusion!");
                else
                    gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("🌀", $"{actorName} lashes out at {casterName} but cannot attack: {onCaster.Reason}."));
                yield break;
            }

            case ConfusedTurnMode.FleeFromCaster:
                {
                    Vector2Int fleeAnchor = decision.CasterSource != null ? decision.CasterSource.GridPosition : actor.GridPosition;
                    bool moved = false;

                    // A prone creature flees at its top possible speed (PHB p.212): it stands up first (a move action that
                    // provokes, PHB p.143), then flees with the action left (D5 as revised 2026-10-29; D15 reads a
                    // frightened creature's flight the same way). The catalog's stand-up entry, the PC button's rule.
                    if (actor.HasCondition(CombatConditionType.Prone) && !actor.IsGrappling() && HasAnyMoveAction(actor))
                    {
                        yield return gameManager.StartCoroutine(gameManager.TryStandUpFromProneForAI(actor));
                        if (gameManager.CurrentPhase == GameManager.TurnPhase.CombatOver || !actor.CanTakeTurnActions())
                            yield break; // dropped by an attack of opportunity while standing up
                    }
                    if (HasAnyMoveAction(actor))
                    {
                        SquareCell fleeCell = FindBestMovementCell(gameManager, actor, fleeAnchor, maximizeDistance: true);
                        if (fleeCell != null && fleeCell.Coords != actor.GridPosition)
                        {
                            // The line shows once the move starts (spent before the first step, D3), never for a refused one.
                            var fleeMove = new MoveAttempt();
                            yield return gameManager.StartCoroutine(gameManager.MoveCharacterAlongComputedPathForAI(
                                ConfusedMove(gameManager, actor, fleeCell.Coords, fleeMove)
                                    .LoggingOnStart(CombatLogHelper.Debuff("🌀", $"{actorName} panics and flees from {casterName}!"))));
                            if (!fleeMove.Refused) // a refused move spends nothing (D3); the executor spent this one first
                                moved = true;
                        }
                    }

                    if (!moved)
                        gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("🌀", $"{actorName} tries to flee from {casterName} but cannot move."));

                    yield return new WaitForSeconds(0.25f);
                    yield break;
                }

            case ConfusedTurnMode.AttackNearestCreature:
                {
                    CharacterController target = decision.PreferredTarget;
                    if (target == null || target.Stats == null || target.Stats.IsDead)
                    {
                        gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("🌀", $"{actorName} is too disoriented to find a target."));
                        yield break;
                    }

                    // A prone creature that must attack the nearest creature and cannot already attack it as it lies stands up
                    // first, with its move action and the attacks of opportunity standing provokes (CMB-074; D5, a delegated RAW
                    // reading of PHB p.212, which is silent on prone: the roll lets it act, and a prone creature takes no
                    // ordinary movement, PHB p.142-143). "Cannot attack as it lies": the AI attack guard refuses the target, or
                    // the weapon is a ranged one other than a crossbow, which a prone attacker cannot use (PHB p.311; not yet
                    // enforced on the attack itself, CMB-196). The catalog's stand-up entry, the PC button's rule.
                    if (actor.HasCondition(CombatConditionType.Prone) && !actor.IsGrappling()
                        && (!AiAttackGuard.Passes(actor, target, allowThrow: false) || ProneBarsMainWeapon(actor)))
                    {
                        yield return gameManager.StartCoroutine(gameManager.TryStandUpFromProneForAI(actor));
                        if (gameManager.CurrentPhase == GameManager.TurnPhase.CombatOver || !actor.CanTakeTurnActions())
                            yield break; // dropped by an attack of opportunity while standing up
                    }

                    bool approachRefused = false;
                    if (!actor.IsTargetInCurrentWeaponRange(target) && HasAnyMoveAction(actor))
                    {
                        SquareCell approach = FindBestMovementCell(gameManager, actor, target.GridPosition, maximizeDistance: false);
                        if (approach != null && approach.Coords != actor.GridPosition)
                        {
                            var approachMove = new MoveAttempt();
                            yield return gameManager.StartCoroutine(gameManager.MoveCharacterAlongComputedPathForAI(ConfusedMove(gameManager, actor, approach.Coords, approachMove)));
                            if (!actor.CanTakeTurnActions())
                                yield break; // dropped by an AoO while moving (CMB-073)
                            approachRefused = approachMove.Refused; // refused before its first step: nothing moved or spent (D3)
                        }
                    }

                    // The AI attack guard (batch 13 step 5, D1; C1: melee reach), and an attack action left: a creature that
                    // stood up and then closed in with its converted standard action declares no attack (PHB p.139, p.143).
                    if (AiAttackGuard.Passes(actor, target, allowThrow: false)
                        && (ActionCosts.CanAfford(actor, ActionCostType.Standard) || actor.ProgressiveAttackPool.HasStartedAttacking))
                    {
                        gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("🌀", $"{actorName} attacks the nearest creature ({target.Stats.CharacterName})!"));
                        yield return gameManager.StartCoroutine(gameManager.NPCPerformAttackForAI(actor, target));
                    }
                    else if (approachRefused)
                    {
                        // No move line for a move that never started (batch 13 step 10).
                        gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("🌀", $"{actorName} cannot reach {target.Stats.CharacterName} this turn."));
                    }
                    else
                    {
                        gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("🌀", $"{actorName} stumbles toward {target.Stats.CharacterName} but cannot attack this turn."));
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

    /// <summary>Whether <paramref name="actor"/>'s main weapon is a ranged one a prone attacker cannot use (PHB p.311: every ranged weapon but a crossbow).</summary>
    private static bool ProneBarsMainWeapon(CharacterController actor)
    {
        ItemData weapon = actor != null ? actor.GetEquippedMainWeapon() : null;
        return weapon != null && weapon.WeaponCat == WeaponCategory.Ranged && !weapon.IsCrossbowWeapon;
    }

    private static bool HasAnyMoveAction(CharacterController actor)
    {
        if (actor == null || actor.Actions == null)
            return false;

        return actor.Actions.HasMoveAction || actor.Actions.CanConvertStandardToMove;
    }

    /// <summary>
    /// A confused creature's move (batch 13 step 9): the move executor spends the move action, or converts the standard
    /// action, before the first step (D3, batch 13 step 10). A refused move (no path, or the shared legality) spends
    /// nothing, and the controller goes on to its next choice.
    /// </summary>
    private static MoveRequest ConfusedMove(GameManager gameManager, CharacterController actor, Vector2Int destination, MoveAttempt attempt)
        => MoveRequest.ForAI(actor, destination, gameManager.GetPlayerMoveSecondsPerStepForAI(), MoveSpend.MoveAction, attempt)
            .ConvertingStandard();

    private static IEnumerator ResolveSelfAttack(CharacterController actor, GameManager gameManager, string log)
    {
        if (actor == null || actor.Stats == null)
            yield break;

        // Hitting itself is an attack, a standard action: a nauseated creature (a single move action) or a pinned one
        // (escape only, PHB p.156) cannot make it (batch 12 step 7: both now reach the confusion roll, decision D4).
        if (actor.IsPinned() || actor.Actions == null || !actor.CommitStandardAction())
        {
            gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Debuff("🌀", $"{actor.Stats.CharacterName} flails in confusion but cannot attack this turn."));
            yield return new WaitForSeconds(0.25f);
            yield break;
        }

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

        gameManager.CombatUI?.ShowCombatLog(CombatLogHelper.Damage("🩸", $"{actor.Stats.CharacterName} hits itself for {result.FinalDamage} damage ({hpBefore} → {hpAfter} HP)."));

        yield return new WaitForSeconds(0.35f);
    }
}
