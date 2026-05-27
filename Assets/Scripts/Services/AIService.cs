using System;
using System.Collections;
using System.Collections.Generic;
using DND35.AI;
using DND35.AI.Profiles;
using DND35.Magic;
using UnityEngine;
using DND35e.Identifiers;
using Random = UnityEngine.Random;

/// <summary>
/// Centralized NPC AI orchestration and tactical decision-making.
/// GameManager delegates hostile turn decisions and target/movement evaluation to this service.
/// </summary>
public class AIService : MonoBehaviour
{
    public enum AIDifficultyLevel
    {
        Easy,
        Normal,
        Hard
    }

    public enum AIActionType
    {
        Wait,
        Move,
        Attack,
        SpecialManeuver,
        Charge,
        Retreat
    }

    private GameManager _gameManager;

    [SerializeField] private AIDifficultyLevel _difficulty = AIDifficultyLevel.Normal;

    public void Initialize(GameManager gameManager)
    {
        _gameManager = gameManager;
    }

    public void Cleanup()
    {
        _gameManager = null;
    }

    public IEnumerator ExecuteNPCTurn(CharacterController npc, NPCAIBehavior behavior)
    {
        if (_gameManager == null || npc == null || npc.Stats == null)
            yield break;

        _gameManager.BeginNPCTurnForAI(npc);

        bool isSummon = _gameManager.IsSummonedCreature(npc);
        string turnColor = isSummon ? "#66E8FF" : "#FF6666";
        string turnIcon = isSummon ? "✶" : "💀";

        _gameManager.CombatUI.SetTurnIndicator($"{_gameManager.GetSummonDisplayName(npc)}'s turn...");
        _gameManager.CombatUI.ShowCombatLog($"<color={turnColor}>{turnIcon} {_gameManager.GetSummonDisplayName(npc)}'s turn begins</color>");
        yield return new WaitForSeconds(0.6f);

        // ── Death/disable check after turn-start effects ──
        // The NPC may have been killed/disabled by start-of-turn area damage
        // (e.g., standing in Wall of Fire, heat wave at caster's turn start, etc.)
        if (npc.Stats.CurrentHP <= 0)
        {
            Debug.Log($"🔥 [AI] {npc.Stats.CharacterName} is dead/disabled (HP={npc.Stats.CurrentHP}) after turn-start effects — turn ended immediately");
            _gameManager.CombatUI?.ShowCombatLog($"💀 {npc.Stats.CharacterName} is dead/disabled and cannot act.");
            yield break;
        }

        if (_gameManager.TryGetConfusedTurnDecisionForAI(npc, out ConfusedBehaviorController.ConfusedTurnDecision confusedDecision))
        {
            if (confusedDecision.Mode != ConfusedBehaviorController.ConfusedTurnMode.ActNormally)
            {
                yield return _gameManager.StartCoroutine(_gameManager.ExecuteConfusedTurnDecisionForAI(npc, confusedDecision));
                yield break;
            }

            _gameManager.CombatUI?.ShowCombatLog($"🌀 {npc.Stats.CharacterName} is confused but acts normally.");
        }

        if (_gameManager.TryGetCharmedTurnDecisionForAI(npc, out CharmedBehaviorController.CharmedTurnDecision charmedDecision))
        {
            yield return _gameManager.StartCoroutine(_gameManager.ExecuteCharmedTurnDecisionForAI(npc, charmedDecision));
            yield break;
        }

        if (_gameManager.TryGetFascinatedTurnDecisionForAI(npc, out FascinatedBehaviorController.FascinatedTurnDecision fascinatedDecision))
        {
            CharacterController source = fascinatedDecision != null ? fascinatedDecision.CasterSource : null;
            if (source == null || source.Stats == null || source.Stats.IsDead)
            {
                _gameManager.RemoveCondition(npc, CombatConditionType.Fascinated);
                _gameManager.CombatUI?.ShowCombatLog($"👁 {npc.Stats.CharacterName} is no longer fascinated.");
            }
            else
            {
                _gameManager.CombatUI?.ShowCombatLog($"👁 {npc.Stats.CharacterName} stares blankly at {source.Stats.CharacterName} and takes no actions.");
            }

            yield return new WaitForSeconds(0.25f);
            yield break;
        }

        if (_gameManager.TryGetFrightenedTurnDecisionForAI(npc, out FrightenedBehaviorController.FrightenedTurnDecision frightenedDecision))
        {
            yield return _gameManager.StartCoroutine(_gameManager.ExecuteFrightenedTurnDecisionForAI(npc, frightenedDecision));
            yield break;
        }

        if (_gameManager.TryExecuteAnimateRopeEscapeForNpc(npc))
        {
            yield return new WaitForSeconds(0.35f);
        }

        CharacterController targetPC = SelectBestTarget(npc, _gameManager.GetAllCharactersForAI());
        if (targetPC == null)
        {
            yield return _gameManager.StartCoroutine(ExecuteSearchTurnWhenNoTargets(npc));
            targetPC = SelectBestTarget(npc, _gameManager.GetAllCharactersForAI());
            if (targetPC == null)
            {
                string npcName = npc.Stats != null ? npc.Stats.CharacterName : npc.name;
                _gameManager.CombatUI?.ShowCombatLog($"{npcName} cannot find a target and keeps searching the battlefield.");
                yield break;
            }

            _gameManager.CombatUI?.ShowCombatLog($"{npc.Stats.CharacterName} spots {targetPC.Stats.CharacterName} after searching.");
        }

        if (npc.HasCondition(CombatConditionType.Turned) && _gameManager.IsUndeadCharacterForAI(npc))
        {
            yield return _gameManager.StartCoroutine(ExecuteTurnedUndeadTurn(npc));
            yield break;
        }

        if (npc.IsGrappling())
        {
            yield return _gameManager.StartCoroutine(_gameManager.ExecuteGrappleRestrictedTurnForAI(npc));
            yield break;
        }

        // ── Resilient Sphere: NPC inside sphere can move within it but attacks/spells
        // cannot pass the boundary (PHB p.263). The sphere is now a stationary area effect.
        if (ResilientSphereAreaEffect.IsCharacterInAnySphere(npc))
        {
            _gameManager.CombatUI?.ShowCombatLog(
                $"<color=#44CCFF>🔮 {npc.Stats.CharacterName} is enclosed in a Resilient Sphere — attacks and spells cannot pass through the boundary!</color>");
            // NPC skips offensive actions but could still move within sphere
            yield return new WaitForSeconds(0.5f);
            yield break;
        }

        AIProfile profile = GetProfile(npc);
        if (profile is SwarmAI swarmProfile)
        {
            yield return _gameManager.StartCoroutine(ExecuteSwarmTurn(npc, swarmProfile, profile is IndiscriminateSwarmAI));
            yield break;
        }

        if (isSummon)
        {
            yield return _gameManager.StartCoroutine(_gameManager.ExecuteSummonedCreatureTurnForAI(npc));
            yield break;
        }

        if (profile != null)
        {
            if (profile is HealerAIProfile healerProfile)
            {
                List<CharacterController> allCombatants = _gameManager.GetAllCharactersForAI();
                bool hasCastableSpells = HasCastablePreparedSpells(npc);
                HealerActionType actionType = healerProfile.DetermineActionPriority(npc, allCombatants, hasCastableSpells);

                Debug.Log($"[AI][Healer] {npc.Stats.CharacterName} action priority: {actionType}");

                if (actionType == HealerActionType.PhysicalAttack)
                {
                    CombatStyle physicalStyle = healerProfile.DetermineCombatMode(npc);
                    if (physicalStyle == CombatStyle.Ranged)
                    {
                        yield return _gameManager.StartCoroutine(ExecuteRangedKiterTurn(npc));
                    }
                    else if (behavior == NPCAIBehavior.DefensiveMelee)
                    {
                        yield return _gameManager.StartCoroutine(ExecuteDefensiveMeleeTurn(npc, targetPC));
                    }
                    else
                    {
                        yield return _gameManager.StartCoroutine(ExecuteAggressiveMeleeTurn(npc, targetPC));
                    }

                    yield break;
                }

                if (actionType == HealerActionType.CriticalHealing || actionType == HealerActionType.Healing)
                {
                    CharacterController healTarget = healerProfile.GetPriorityHealTarget(npc, allCombatants);
                    if (healTarget != null && healTarget.Stats != null)
                    {
                        Debug.Log($"[AI][Healer] {npc.Stats.CharacterName} targeting {healTarget.Stats.CharacterName} for healing.");
                        // T1.1 FIX: Actually attempt to cast heal spell on the ally target
                        bool healCasted = TryExecuteSpellcastAction(npc, healTarget);
                        if (healCasted)
                        {
                            Debug.Log($"[AI][Healer] {npc.Stats.CharacterName} successfully healed {healTarget.Stats.CharacterName}.");
                            yield return new WaitForSeconds(0.5f);
                            yield break;
                        }
                        Debug.Log($"[AI][Healer] {npc.Stats.CharacterName} failed to heal — falling back to ranged kiter.");
                    }
                }

                if (actionType == HealerActionType.Buffing)
                {
                    // T1.1 FIX: Actually attempt to cast buff spell (ally targeting now works)
                    bool buffCasted = TryExecuteSpellcastAction(npc, null);
                    if (buffCasted)
                    {
                        Debug.Log($"[AI][Healer] {npc.Stats.CharacterName} successfully cast buff spell.");
                        yield return new WaitForSeconds(0.5f);
                        yield break;
                    }
                }

                // Spell execution handled by ranged kiter turn (includes offensive spells)
                yield return _gameManager.StartCoroutine(ExecuteRangedKiterTurn(npc));
                yield break;
            }

            // ── Dragon / breath-weapon tactical AI ──
            if (profile is DragonAIProfile dragonProfile)
            {
                yield return _gameManager.StartCoroutine(ExecuteDragonTurn(npc, targetPC, dragonProfile));
                yield break;
            }

            // Profile drives targeting/maneuvers, while NPCAIBehavior still selects tactical shell.
            if (behavior == NPCAIBehavior.DefensiveMelee)
            {
                yield return _gameManager.StartCoroutine(ExecuteDefensiveMeleeTurn(npc, targetPC));
            }
            else if (behavior == NPCAIBehavior.RangedKiter || profile.CombatStyle == CombatStyle.Ranged)
            {
                yield return _gameManager.StartCoroutine(ExecuteRangedKiterTurn(npc));
            }
            else
            {
                yield return _gameManager.StartCoroutine(ExecuteAggressiveMeleeTurn(npc, targetPC));
            }

            yield break;
        }

        switch (behavior)
        {
            case NPCAIBehavior.AggressiveMelee:
                yield return _gameManager.StartCoroutine(ExecuteAggressiveMeleeTurn(npc, targetPC));
                break;
            case NPCAIBehavior.RangedKiter:
                yield return _gameManager.StartCoroutine(ExecuteRangedKiterTurn(npc));
                break;
            case NPCAIBehavior.DefensiveMelee:
                yield return _gameManager.StartCoroutine(ExecuteDefensiveMeleeTurn(npc, targetPC));
                break;
            default:
                yield return _gameManager.StartCoroutine(ExecuteAggressiveMeleeTurn(npc, targetPC));
                break;
        }
    }

    private IEnumerator ExecuteSearchTurnWhenNoTargets(CharacterController npc)
    {
        if (npc == null || npc.Stats == null || npc.Actions == null)
            yield break;

        string npcName = npc.Stats.CharacterName;
        _gameManager.CombatUI?.ShowCombatLog($"{npcName} has no valid targets and starts searching.");

        if (!npc.Actions.HasMoveAction || npc.Stats.MovementBlockedByCondition)
        {
            _gameManager.CombatUI?.ShowCombatLog($"{npcName} cannot move to search this turn.");
            yield break;
        }

        Vector2Int searchDestination;
        if (!TryFindSearchDestination(npc, out searchDestination) || searchDestination == npc.GridPosition)
        {
            _gameManager.CombatUI?.ShowCombatLog($"{npcName} scans the area but finds no better position.");
            yield break;
        }

        Debug.Log($"[AI][Search] {npcName} moving from {npc.GridPosition} to {searchDestination}");

        yield return _gameManager.StartCoroutine(
            _gameManager.MoveCharacterAlongComputedPathForAI(npc, searchDestination, _gameManager.GetPlayerMoveSecondsPerStepForAI()));

        if (npc.Actions.HasMoveAction)
            npc.Actions.UseMoveAction();

        _gameManager.CombatUI?.ShowCombatLog($"{npcName} moves to search for enemies.");
        yield return new WaitForSeconds(0.35f);
    }

    private bool TryFindSearchDestination(CharacterController npc, out Vector2Int destination)
    {
        destination = npc != null ? npc.GridPosition : Vector2Int.zero;

        if (npc == null || npc.Stats == null || _gameManager == null || _gameManager.Grid == null)
            return false;

        LastKnownPositionTracker tracker = npc.GetComponent<LastKnownPositionTracker>();
        AIProfile profile = GetProfile(npc);
        List<CharacterController> allCombatants = _gameManager.GetAllCharactersForAI();

        SquareCell bestTrackedCell = null;
        int bestTrackedDistance = int.MaxValue;

        if (tracker != null && allCombatants != null)
        {
            for (int i = 0; i < allCombatants.Count; i++)
            {
                CharacterController candidate = allCombatants[i];
                if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                    continue;
                if (!_gameManager.IsEnemyTeamForAI(npc, candidate))
                    continue;
                if (!tracker.HasLastKnownPosition(candidate))
                    continue;

                Vector2Int? knownPosition = tracker.GetLastKnownPosition(candidate);
                if (!knownPosition.HasValue)
                    continue;

                SquareCell candidateCell = EvaluateMovementOptions(npc, knownPosition.Value, retreat: false, candidate, profile);
                if (candidateCell == null || candidateCell.Coords == npc.GridPosition)
                    continue;

                int distToKnown = SquareGridUtils.GetDistance(candidateCell.Coords, knownPosition.Value);
                if (distToKnown < bestTrackedDistance)
                {
                    bestTrackedDistance = distToKnown;
                    bestTrackedCell = candidateCell;
                }
            }
        }

        if (bestTrackedCell != null)
        {
            destination = bestTrackedCell.Coords;
            return true;
        }

        Vector2Int mapCenter = new Vector2Int(_gameManager.Grid.Width / 2, _gameManager.Grid.Height / 2);
        int npcMoveRange = _gameManager.GetCurrentMoveRangeSquares(npc);
        List<SquareCell> moveCells = _gameManager.Grid.GetCellsInRange(npc.GridPosition, npcMoveRange);

        SquareCell bestExplorationCell = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < moveCells.Count; i++)
        {
            SquareCell cell = moveCells[i];
            if (cell == null || cell.Coords == npc.GridPosition)
                continue;

            if (!_gameManager.Grid.CanPlaceCreature(cell.Coords, npc.GetVisualSquaresOccupied(), npc))
                continue;

            AoOPathResult pathResult = _gameManager.FindPath(npc, cell.Coords, avoidThreats: false, maxRangeOverride: npcMoveRange);
            if (pathResult == null || pathResult.Path == null || pathResult.Path.Count == 0)
                continue;

            int distToCenter = SquareGridUtils.GetDistance(cell.Coords, mapCenter);
            float score = -distToCenter + UnityEngine.Random.Range(0f, 4f);

            if (score > bestScore)
            {
                bestScore = score;
                bestExplorationCell = cell;
            }
        }

        if (bestExplorationCell == null)
            return false;

        destination = bestExplorationCell.Coords;
        return true;
    }

    private IEnumerator ExecuteTurnedUndeadTurn(CharacterController npc)
    {
        if (npc == null || npc.Stats == null)
            yield break;

        CharacterController source = _gameManager.GetTurnUndeadTurnerForAI(npc);

        if (source == null)
        {
            List<StatusEffect> activeConditions = npc.GetActiveConditions();
            for (int i = 0; i < activeConditions.Count; i++)
            {
                StatusEffect condition = activeConditions[i];
                if (condition == null || ConditionRules.Normalize(condition.Type) != CombatConditionType.Turned)
                    continue;

                if (string.IsNullOrWhiteSpace(condition.SourceName))
                    break;

                List<CharacterController> all = _gameManager.GetAllCharactersForAI();
                for (int c = 0; c < all.Count; c++)
                {
                    CharacterController candidate = all[c];
                    if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                        continue;

                    if (string.Equals(candidate.Stats.CharacterName, condition.SourceName, StringComparison.Ordinal))
                    {
                        source = candidate;
                        _gameManager.RegisterTurnUndeadTrackerForAI(npc, source);
                        break;
                    }
                }
                break;
            }
        }

        if (source == null)
            source = _gameManager.GetClosestAliveEnemyToForAI(npc);

        if (source != null && npc.Actions.HasMoveAction && !npc.Stats.MovementBlockedByCondition)
        {
            SquareCell retreatCell = EvaluateMovementOptions(npc, source.GridPosition, retreat: true);
            if (retreatCell != null && retreatCell.Coords != npc.GridPosition)
            {
                yield return _gameManager.StartCoroutine(
                    _gameManager.MoveCharacterAlongComputedPathForAI(npc, retreatCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI()));
                npc.Actions.UseMoveAction();
                _gameManager.CombatUI?.ShowCombatLog($"↩ {npc.Stats.CharacterName} flees from divine turning!");
                yield return new WaitForSeconds(0.45f);
                yield break;
            }
        }

        _gameManager.CombatUI?.ShowCombatLog($"↩ {npc.Stats.CharacterName} is turned and cowers, unable to attack.");
        yield return new WaitForSeconds(0.35f);
    }

    private IEnumerator ExecuteAggressiveMeleeTurn(CharacterController npc, CharacterController target)
    {
        if (npc == null || target == null || target.Stats == null || target.Stats.IsDead)
            yield break;

        // Death/disable check: NPC may have been killed by damage before this method runs
        if (npc.Stats.CurrentHP <= 0)
        {
            Debug.Log($"🔥 [AI] {npc.Stats.CharacterName} is dead/disabled (HP={npc.Stats.CurrentHP}) at start of aggressive melee turn — turn ended");
            yield break;
        }

        AIActionType action = SelectBestAction(npc, target, preferAggression: true);
        if (action == AIActionType.Charge)
        {
            yield return _gameManager.StartCoroutine(_gameManager.NPCExecuteChargeForAI(npc, target));
            yield break;
        }

        AIProfile profile = GetProfile(npc);

        if (!npc.IsTargetInCurrentWeaponRange(target))
        {
            SquareCell bestCell = EvaluateMovementOptions(npc, target.GridPosition, retreat: false, target, profile);
            if (bestCell != null)
            {
                yield return _gameManager.StartCoroutine(
                    _gameManager.MoveCharacterAlongComputedPathForAI(npc, bestCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI()));

                // ── Death/disable check after movement ──
                // Creature may have been killed by area damage (Wall of Fire, etc.) during movement.
                if (npc.Stats.CurrentHP <= 0)
                {
                    Debug.Log($"🔥 [AI] {npc.Stats.CharacterName} killed/disabled during movement (HP={npc.Stats.CurrentHP}) — turn ended");
                    yield break;
                }

                npc.Actions.UseMoveAction();
                _gameManager.CombatUI.ShowCombatLog($"{npc.Stats.CharacterName} advances toward {target.Stats.CharacterName}!");
                yield return new WaitForSeconds(0.5f);
            }
        }

        // ── Death/disable re-check before attack phase ──
        if (npc.Stats.CurrentHP <= 0)
        {
            Debug.Log($"🔥 [AI] {npc.Stats.CharacterName} dead/disabled before attack phase (HP={npc.Stats.CurrentHP}) — turn ended");
            yield break;
        }

        target = SelectBestTarget(npc, _gameManager.GetAllCharactersForAI());
        if (target == null)
            yield break;

        if (npc.IsTargetInCurrentWeaponRange(target) && !target.Stats.IsDead)
        {
            bool usedSpecial = ShouldUseManeuver(npc, target) && TryExecutePreferredManeuver(npc, target, profile);
            if (!usedSpecial)
                yield return _gameManager.StartCoroutine(_gameManager.NPCPerformAttackForAI(npc, target));
            else
                yield return new WaitForSeconds(0.8f);
        }
        else
        {
            // ── AI: Wall of Ice interaction ──
            // If the NPC cannot reach the target but is adjacent to a Wall of Ice,
            // try to attack or break through the wall to clear the path.
            yield return _gameManager.StartCoroutine(TryAIWallInteraction(npc, target));
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  Dragon Tactical Turn
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Executes a full dragon tactical turn following the priority:
    /// 1. Cast buff spells (if not in melee)
    /// 2. Cast attack spells (if not in melee)
    /// 3. Avoid AoOs
    /// 4. Prioritize healers/mages (unless closer target)
    /// 5. Use breath weapon when available to hit most targets
    /// 6. Full melee attack as last resort
    /// </summary>
    private IEnumerator ExecuteDragonTurn(CharacterController npc, CharacterController target, DragonAIProfile dragonProfile)
    {
        if (npc == null || target == null || target.Stats == null || target.Stats.IsDead)
            yield break;

        if (npc.Stats.CurrentHP <= 0)
        {
            Debug.Log($"🔥 [AI][Dragon] {npc.Stats.CharacterName} is dead/disabled (HP={npc.Stats.CurrentHP}) — turn ended");
            yield break;
        }

        List<CharacterController> allCombatants = _gameManager.GetAllCharactersForAI();

        // Reset breath weapon decision state
        dragonProfile.WantsToUseBreathWeapon = false;
        dragonProfile.BreathWeaponAimTarget = null;

        // ── Priority 1 & 2: Cast buff/attack spells if not in melee ──
        if (!dragonProfile.IsTooCloseForCasting(npc, allCombatants, _gameManager))
        {
            if (npc.Stats.IsSpellcaster && TryExecuteSpellcastAction(npc, target))
            {
                Debug.Log($"[AI][Dragon] {npc.Stats.CharacterName} cast a spell (priority 1/2).");
                yield return new WaitForSeconds(0.8f);
                yield break;
            }
        }

        // ── Priority 5: Evaluate breath weapon ──
        bool breathReady = dragonProfile.EvaluateBreathWeapon(npc, allCombatants, _gameManager);

        if (breathReady && dragonProfile.BreathWeaponAimTarget != null)
        {
            // Use breath weapon — this is a standard action
            Debug.Log($"[AI][Dragon] {npc.Stats.CharacterName} uses breath weapon (hits {dragonProfile.BreathWeaponExpectedHits} enemies)!");
            yield return _gameManager.StartCoroutine(
                _gameManager.NPCExecuteBreathWeaponForAI(npc, dragonProfile.BreathWeaponAimTarget));

            // After breath weapon, dragon may still have move action for positioning
            if (npc.Stats.CurrentHP > 0 && npc.Actions.HasMoveAction)
            {
                // Try to step away from melee threats (AoO avoidance — priority 3)
                CharacterController closestEnemy = SelectBestTarget(npc, allCombatants);
                if (closestEnemy != null)
                {
                    int dist = SquareGridUtils.GetDistance(npc.GridPosition, closestEnemy.GridPosition);
                    if (dist <= 1)
                    {
                        SquareCell retreatCell = EvaluateMovementOptions(npc, closestEnemy.GridPosition, retreat: true, profile: dragonProfile);
                        if (retreatCell != null && retreatCell.Coords != npc.GridPosition)
                        {
                            yield return _gameManager.StartCoroutine(
                                _gameManager.MoveCharacterAlongComputedPathForAI(npc, retreatCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI()));
                            npc.Actions.UseMoveAction();
                            _gameManager.CombatUI?.ShowCombatLog($"{npc.Stats.CharacterName} repositions after breath weapon.");
                            yield return new WaitForSeconds(0.3f);
                        }
                    }
                }
            }

            yield break;
        }

        // ── Priority 3 & 4 & 6: Movement + melee attack ──
        // Check for charge opportunity first
        AIActionType action = SelectBestAction(npc, target, preferAggression: true);
        if (action == AIActionType.Charge)
        {
            yield return _gameManager.StartCoroutine(_gameManager.NPCExecuteChargeForAI(npc, target));
            yield break;
        }

        // Move toward target if not in range (priority 3: movement evaluator respects AoO avoidance)
        if (!npc.IsTargetInCurrentWeaponRange(target))
        {
            SquareCell bestCell = EvaluateMovementOptions(npc, target.GridPosition, retreat: false, target, dragonProfile);
            if (bestCell != null)
            {
                yield return _gameManager.StartCoroutine(
                    _gameManager.MoveCharacterAlongComputedPathForAI(npc, bestCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI()));

                if (npc.Stats.CurrentHP <= 0)
                {
                    Debug.Log($"🔥 [AI][Dragon] {npc.Stats.CharacterName} killed during movement — turn ended");
                    yield break;
                }

                npc.Actions.UseMoveAction();
                _gameManager.CombatUI?.ShowCombatLog($"{npc.Stats.CharacterName} advances toward {target.Stats.CharacterName}!");
                yield return new WaitForSeconds(0.5f);
            }
        }

        if (npc.Stats.CurrentHP <= 0)
            yield break;

        // Re-evaluate target after movement (priority 4: profile ScoreTarget already weights healers/mages)
        target = SelectBestTarget(npc, allCombatants);
        if (target == null)
            yield break;

        // ── Priority 6: Full melee attack ──
        if (npc.IsTargetInCurrentWeaponRange(target) && !target.Stats.IsDead)
        {
            bool usedSpecial = ShouldUseManeuver(npc, target) && TryExecutePreferredManeuver(npc, target, dragonProfile);
            if (!usedSpecial)
                yield return _gameManager.StartCoroutine(_gameManager.NPCPerformAttackForAI(npc, target));
            else
                yield return new WaitForSeconds(0.8f);
        }
        else
        {
            yield return _gameManager.StartCoroutine(TryAIWallInteraction(npc, target));
        }
    }

    private IEnumerator ExecuteRangedKiterTurn(CharacterController npc)
    {
        // Death/disable check: NPC may have been killed by damage before this method runs
        if (npc == null || npc.Stats == null || npc.Stats.CurrentHP <= 0)
        {
            if (npc != null && npc.Stats != null)
                Debug.Log($"🔥 [AI] {npc.Stats.CharacterName} is dead/disabled (HP={npc.Stats.CurrentHP}) at start of ranged kiter turn — turn ended");
            yield break;
        }

        AIProfile profile = GetProfile(npc);
        List<CharacterController> allCombatants = _gameManager.GetAllCharactersForAI();

        CharacterController closestPC = SelectBestTarget(npc, allCombatants);
        if (closestPC == null)
            yield break;

        bool preferRangedVisibility = npc.IsEquippedWeaponRanged();
        LastKnownPositionTracker tracker = npc.GetComponent<LastKnownPositionTracker>();
        if (tracker == null)
            tracker = npc.gameObject.AddComponent<LastKnownPositionTracker>();

        if (npc.CanSee(closestPC, incomingIsRangedAttack: preferRangedVisibility))
        {
            tracker.UpdateLastKnownPosition(closestPC);
        }

        if (!npc.CanSee(closestPC, incomingIsRangedAttack: preferRangedVisibility))
        {
            var concealedSingleTarget = new List<CharacterController> { closestPC };
            tracker.AttemptListenChecks(concealedSingleTarget, _gameManager);

            Vector2Int? lastKnown = tracker.GetLastKnownPosition(closestPC);
            if (tracker.IsPinpointedThisRound(closestPC))
                _gameManager.CombatUI?.ShowCombatLog($"{npc.Stats.CharacterName} pinpoints {closestPC.Stats.CharacterName} by sound and attacks their current position.");
            else if (lastKnown.HasValue)
                _gameManager.CombatUI?.ShowCombatLog($"{npc.Stats.CharacterName} cannot see {closestPC.Stats.CharacterName} clearly and fires at the last known position.");

            if (tracker.IsPinpointedThisRound(closestPC) || lastKnown.HasValue)
            {
                yield return _gameManager.StartCoroutine(_gameManager.NPCPerformAttackForAI(npc, closestPC));
                yield return new WaitForSeconds(0.45f);
                yield break;
            }

            if (npc.Actions.HasMoveAction)
            {
                SquareCell blindSearchCell = EvaluateMovementOptions(npc, closestPC.GridPosition, retreat: false, closestPC, profile);
                if (blindSearchCell != null && blindSearchCell.Coords != npc.GridPosition)
                {
                    yield return _gameManager.StartCoroutine(
                        _gameManager.MoveCharacterAlongComputedPathForAI(npc, blindSearchCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI()));
                    npc.Actions.UseMoveAction();
                    _gameManager.CombatUI?.ShowCombatLog($"{npc.Stats.CharacterName} advances, trying to reacquire line of sight through concealment.");
                    yield return new WaitForSeconds(0.4f);
                }
            }

            yield break;
        }

        if (TryExecuteSpellcastAction(npc, closestPC))
        {
            yield return new WaitForSeconds(0.8f);
            yield break;
        }

        bool avoidAoORisk = npc.IsEquippedWeaponRanged();
        bool riskIsTooHigh = false;
        bool tookTacticalStep = false;

        if (avoidAoORisk)
        {
            RangedAoORiskAssessment riskAssessment = AssessRangedAoORisk(npc, closestPC, profile, allCombatants);
            riskIsTooHigh = riskAssessment.IsThreatened && riskAssessment.ExpectedDamage > riskAssessment.RiskTolerance;

            if (riskAssessment.IsThreatened)
            {
                if (riskIsTooHigh && TryTakeTacticalFiveFootStep(npc, closestPC, profile, allCombatants, out Vector2Int stepDestination))
                {
                    tookTacticalStep = true;
                    riskIsTooHigh = false;
                    _gameManager.CombatUI?.ShowCombatLog(
                        $"{npc.Stats.CharacterName} takes a tactical 5-foot step to avoid incoming attacks before firing.");
                    Debug.Log($"[AI][RangedAoO] {npc.Stats.CharacterName} 5-foot steps to {stepDestination} (expected={riskAssessment.ExpectedDamage:F1}, tolerance={riskAssessment.RiskTolerance:F1})");
                    yield return new WaitForSeconds(0.35f);
                }
                else
                {
                    string riskLabel = riskIsTooHigh ? "high" : "acceptable";
                    Debug.Log($"[AI][RangedAoO] {npc.Stats.CharacterName} threat risk is {riskLabel} (expected={riskAssessment.ExpectedDamage:F1}, tolerance={riskAssessment.RiskTolerance:F1}, threats={riskAssessment.ThreatCount})");
                }
            }
        }

        int distToClosestPC = SquareGridUtils.GetDistance(npc.GridPosition, closestPC.GridPosition);
        bool shouldRetreatForDistance = distToClosestPC <= 2 && npc.Actions.HasMoveAction && !tookTacticalStep;
        bool shouldRetreatForRisk = avoidAoORisk && riskIsTooHigh && npc.Actions.HasMoveAction && !tookTacticalStep;

        if (shouldRetreatForDistance || shouldRetreatForRisk)
        {
            SquareCell retreatCell = EvaluateMovementOptions(npc, closestPC.GridPosition, retreat: true, profile: profile);
            if (retreatCell != null)
            {
                yield return _gameManager.StartCoroutine(
                    _gameManager.MoveCharacterAlongComputedPathForAI(npc, retreatCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI()));

                // ── Death/disable check after retreat movement ──
                if (npc.Stats.CurrentHP <= 0)
                {
                    Debug.Log($"🔥 [AI] {npc.Stats.CharacterName} killed/disabled during retreat movement (HP={npc.Stats.CurrentHP}) — turn ended");
                    yield break;
                }

                npc.Actions.UseMoveAction();

                if (shouldRetreatForRisk)
                    _gameManager.CombatUI.ShowCombatLog($"{npc.Stats.CharacterName} repositions to avoid provoking attacks of opportunity.");
                else
                    _gameManager.CombatUI.ShowCombatLog($"{npc.Stats.CharacterName} retreats to maintain distance!");

                yield return new WaitForSeconds(0.5f);
            }
        }

        // ── Death/disable re-check before attack phase ──
        if (npc.Stats.CurrentHP <= 0)
        {
            Debug.Log($"🔥 [AI] {npc.Stats.CharacterName} dead/disabled before ranged attack phase (HP={npc.Stats.CurrentHP}) — turn ended");
            yield break;
        }

        CharacterController rangedTarget = SelectBestTarget(npc, _gameManager.GetAllCharactersForAI());
        if (rangedTarget == null)
            yield break;

        if (TryExecuteSpellcastAction(npc, rangedTarget))
        {
            yield return new WaitForSeconds(0.8f);
            yield break;
        }

        int maxRange = GetMaximumAttackRangeInSquares(npc);
        int preferredSpellRange = GetBestAvailableSpellRangeInSquares(npc, rangedTarget);
        if (preferredSpellRange > maxRange)
            maxRange = preferredSpellRange;

        int distToRangedTarget = SquareGridUtils.GetDistance(npc.GridPosition, rangedTarget.GridPosition);

        if (distToRangedTarget <= maxRange && !rangedTarget.Stats.IsDead)
        {
            bool usedSpecial = ShouldUseManeuver(npc, rangedTarget) && TryExecutePreferredManeuver(npc, rangedTarget, profile);
            if (!usedSpecial)
                yield return _gameManager.StartCoroutine(_gameManager.NPCPerformAttackForAI(npc, rangedTarget));
            else
                yield return new WaitForSeconds(0.8f);
        }
        else if (distToRangedTarget > maxRange && npc.Actions.HasMoveAction)
        {
            SquareCell approachCell = EvaluateMovementOptions(npc, rangedTarget.GridPosition, retreat: false, profile: profile);
            if (approachCell != null)
            {
                yield return _gameManager.StartCoroutine(
                    _gameManager.MoveCharacterAlongComputedPathForAI(npc, approachCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI()));
                npc.Actions.UseMoveAction();
                _gameManager.CombatUI.ShowCombatLog($"{npc.Stats.CharacterName} moves to get a better shot.");
                yield return new WaitForSeconds(0.5f);
            }
        }
        else
        {
            yield return new WaitForSeconds(0.3f);
        }
    }

    private struct RangedAoORiskAssessment
    {
        public bool IsThreatened;
        public int ThreatCount;
        public float ExpectedDamage;
        public float RiskTolerance;
    }

    private RangedAoORiskAssessment AssessRangedAoORisk(
        CharacterController npc,
        CharacterController target,
        AIProfile profile,
        List<CharacterController> allCombatants)
    {
        var assessment = new RangedAoORiskAssessment();
        if (npc == null || npc.Stats == null)
            return assessment;

        List<CharacterController> threateningEnemies = ThreatSystem.GetThreateningEnemies(npc.GridPosition, npc, allCombatants);
        threateningEnemies.RemoveAll(enemy => !ThreatSystem.CanMakeAoO(enemy));

        assessment.ThreatCount = threateningEnemies.Count;
        assessment.IsThreatened = assessment.ThreatCount > 0;

        if (!assessment.IsThreatened)
            return assessment;

        assessment.ExpectedDamage = ThreatSystem.CalculateExpectedAoODamageForRangedAttack(npc, threateningEnemies);
        assessment.RiskTolerance = CalculateRangedRiskTolerance(npc, target, profile);
        return assessment;
    }

    private float CalculateRangedRiskTolerance(CharacterController npc, CharacterController target, AIProfile profile)
    {
        if (npc == null || npc.Stats == null)
            return 0f;

        float maxHP = Mathf.Max(1f, npc.Stats.TotalMaxHP);
        float hpPercent = Mathf.Clamp01((float)npc.Stats.CurrentHP / maxHP);

        float tolerancePercent;
        if (hpPercent > 0.75f)
            tolerancePercent = 0.25f;
        else if (hpPercent > 0.5f)
            tolerancePercent = 0.10f;
        else
            tolerancePercent = 0.05f;

        if (profile != null)
            tolerancePercent *= Mathf.Clamp(profile.GetRangedAoORiskToleranceMultiplier(), 0.25f, 2f);

        // Accept slightly higher risk for kill opportunities/high-value enemy casters.
        if (target != null && target.Stats != null)
        {
            bool targetNearDefeat = target.Stats.TotalMaxHP > 0
                && ((float)target.Stats.CurrentHP / target.Stats.TotalMaxHP) <= 0.25f;
            bool highValueTarget = target.Stats.IsWizard || target.Stats.IsCleric;

            if (targetNearDefeat || highValueTarget)
                tolerancePercent += 0.05f;
        }

        return maxHP * Mathf.Clamp(tolerancePercent, 0.02f, 0.35f);
    }

    private bool TryTakeTacticalFiveFootStep(
        CharacterController npc,
        CharacterController target,
        AIProfile profile,
        List<CharacterController> allCombatants,
        out Vector2Int destination)
    {
        destination = npc != null ? npc.GridPosition : Vector2Int.zero;

        if (npc == null || target == null || _gameManager == null || _gameManager.Grid == null)
            return false;

        if (!_gameManager.CanTakeFiveFootStepForAI(npc))
            return false;

        int preferredRange = profile != null && profile.Movement != null
            ? Mathf.Max(1, profile.Movement.PreferredRangeSquares)
            : 4;
        int maxRange = GetMaximumAttackRangeInSquares(npc);

        Vector2Int bestCell = npc.GridPosition;
        float bestScore = float.NegativeInfinity;
        bool found = false;

        Vector2Int[] neighbors = SquareGridUtils.GetNeighbors(npc.GridPosition);
        for (int i = 0; i < neighbors.Length; i++)
        {
            Vector2Int candidate = neighbors[i];
            if (!_gameManager.CanTakeFiveFootStepToForAI(npc, candidate))
                continue;

            int distToTarget = SquareGridUtils.GetDistance(candidate, target.GridPosition);
            if (distToTarget > maxRange)
                continue;

            List<CharacterController> threatsAfterStep = ThreatSystem.GetThreateningEnemies(candidate, npc, allCombatants);
            threatsAfterStep.RemoveAll(enemy => !ThreatSystem.CanMakeAoO(enemy));

            float expectedAfterStep = ThreatSystem.CalculateExpectedAoODamageForRangedAttack(npc, threatsAfterStep);
            float rangeScore = -Mathf.Abs(distToTarget - preferredRange);
            float threatScore = -expectedAfterStep * 3f;
            float totalScore = threatScore + rangeScore;

            if (threatsAfterStep.Count == 0)
                totalScore += 6f;

            if (totalScore > bestScore)
            {
                bestScore = totalScore;
                bestCell = candidate;
                found = true;
            }
        }

        if (!found)
            return false;

        if (_gameManager.TryTakeFiveFootStepForAI(npc, bestCell))
        {
            destination = bestCell;
            return true;
        }

        return false;
    }

    private IEnumerator ExecuteDefensiveMeleeTurn(CharacterController npc, CharacterController preferredTarget)
    {
        CharacterController weakerPC = SelectLowestHPEnemy(npc);
        CharacterController target = weakerPC != null ? weakerPC : preferredTarget;

        if (npc == null || target == null || target.Stats == null || target.Stats.IsDead)
            yield break;

        // Death/disable check: NPC may have been killed by damage before this method runs
        if (npc.Stats.CurrentHP <= 0)
        {
            Debug.Log($"🔥 [AI] {npc.Stats.CharacterName} is dead/disabled (HP={npc.Stats.CurrentHP}) at start of defensive melee turn — turn ended");
            yield break;
        }

        AIActionType action = SelectBestAction(npc, target, preferAggression: false);
        if (action == AIActionType.Charge)
        {
            yield return _gameManager.StartCoroutine(_gameManager.NPCExecuteChargeForAI(npc, target));
            yield break;
        }

        AIProfile profile = GetProfile(npc);

        float hpPercent = npc.Stats.TotalMaxHP > 0 ? (float)npc.Stats.CurrentHP / npc.Stats.TotalMaxHP : 1f;
        if (hpPercent < 0.30f && npc.Actions.HasFullRoundAction)
        {
            SquareCell withdrawCell = EvaluateWithdrawRetreatDestination(npc, target.GridPosition);
            if (withdrawCell != null && withdrawCell.Coords != npc.GridPosition)
            {
                yield return _gameManager.StartCoroutine(
                    _gameManager.ExecuteWithdrawMovementForAI(npc, withdrawCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI()));

                // ── Death/disable check after withdraw movement ──
                if (npc.Stats.CurrentHP <= 0)
                {
                    Debug.Log($"🔥 [AI] {npc.Stats.CharacterName} killed/disabled during withdraw (HP={npc.Stats.CurrentHP}) — turn ended");
                    yield break;
                }

                _gameManager.CombatUI?.ShowCombatLog($"{npc.Stats.CharacterName} withdraws from {target.Stats.CharacterName}.");
                yield return new WaitForSeconds(0.45f);
                yield break;
            }
        }

        if (!npc.IsTargetInCurrentWeaponRange(target))
        {
            SquareCell bestCell = EvaluateMovementOptions(npc, target.GridPosition, retreat: false, target, profile);
            if (bestCell != null)
            {
                yield return _gameManager.StartCoroutine(
                    _gameManager.MoveCharacterAlongComputedPathForAI(npc, bestCell.Coords, _gameManager.GetPlayerMoveSecondsPerStepForAI()));

                // ── Death/disable check after movement ──
                if (npc.Stats.CurrentHP <= 0)
                {
                    Debug.Log($"🔥 [AI] {npc.Stats.CharacterName} killed/disabled during movement (HP={npc.Stats.CurrentHP}) — turn ended");
                    yield break;
                }

                npc.Actions.UseMoveAction();
                _gameManager.CombatUI.ShowCombatLog($"{npc.Stats.CharacterName} advances methodically toward {target.Stats.CharacterName}.");
                yield return new WaitForSeconds(0.5f);
            }
        }

        // ── Death/disable re-check before attack phase ──
        if (npc.Stats.CurrentHP <= 0)
        {
            Debug.Log($"🔥 [AI] {npc.Stats.CharacterName} dead/disabled before attack phase (HP={npc.Stats.CurrentHP}) — turn ended");
            yield break;
        }

        target = SelectBestTarget(npc, _gameManager.GetAllCharactersForAI());
        if (target == null)
            yield break;

        if (npc.IsTargetInCurrentWeaponRange(target) && !target.Stats.IsDead)
        {
            bool usedSpecial = ShouldUseManeuver(npc, target) && TryExecutePreferredManeuver(npc, target, profile);
            if (!usedSpecial)
                yield return _gameManager.StartCoroutine(_gameManager.NPCPerformAttackForAI(npc, target));
            else
                yield return new WaitForSeconds(0.8f);
        }
        else
        {
            // ── AI: Wall of Ice interaction (defensive variant) ──
            yield return _gameManager.StartCoroutine(TryAIWallInteraction(npc, target));
        }
    }

    private IEnumerator ExecuteSwarmTurn(CharacterController swarm, SwarmAI profile, bool indiscriminate)
    {
        if (_gameManager == null || swarm == null || swarm.Stats == null || profile == null)
            yield break;

        List<CharacterController> candidates = BuildSwarmTargetCandidates(swarm, indiscriminate);
        CharacterController target = profile.ResolveTarget(swarm, candidates);

        if (target == null)
        {
            _gameManager.CombatUI?.ShowCombatLog($"{swarm.Stats.CharacterName} finds no living creatures to swarm.");
            yield return new WaitForSeconds(0.3f);
            yield break;
        }

        _gameManager.CombatUI?.ShowCombatLog($"{swarm.Stats.CharacterName} scans for nearest creature: {target.Stats.CharacterName}.");

        // Indiscriminate swarms (Summon Swarm) attack nearest creature regardless of team
        if (indiscriminate && target.Team == swarm.Team)
        {
            CharacterController summonCaster = _gameManager.GetSummonCasterForAI(swarm);
            if (summonCaster != null && target == summonCaster)
                _gameManager.CombatUI?.ShowCombatLog($"<color=#FF8866>⚠ {swarm.Stats.CharacterName} is uncontrolled and attacks its summoner {target.Stats.CharacterName}!</color>");
            else
                _gameManager.CombatUI?.ShowCombatLog($"<color=#FF8866>⚠ {swarm.Stats.CharacterName} is uncontrolled and attacks ally {target.Stats.CharacterName}!</color>");
        }

        if (!swarm.IsTargetInCurrentWeaponRange(target) && swarm.Actions.HasMoveAction)
        {
            yield return _gameManager.StartCoroutine(
                _gameManager.MoveCharacterAlongComputedPathForAI(swarm, target.GridPosition, _gameManager.GetPlayerMoveSecondsPerStepForAI()));

            // ── Death/disable check after swarm movement ──
            if (swarm.Stats.CurrentHP <= 0)
            {
                Debug.Log($"🔥 [AI] {swarm.Stats.CharacterName} killed/disabled during swarm movement (HP={swarm.Stats.CurrentHP}) — turn ended");
                yield break;
            }

            swarm.Actions.UseMoveAction();
            yield return new WaitForSeconds(0.35f);
        }

        // ── Death/disable re-check before swarm attack phase ──
        if (swarm.Stats.CurrentHP <= 0)
        {
            Debug.Log($"🔥 [AI] {swarm.Stats.CharacterName} dead/disabled before swarm attack phase (HP={swarm.Stats.CurrentHP}) — turn ended");
            yield break;
        }

        target = profile.ResolveTarget(swarm, candidates);
        if (target == null)
            yield break;

        if (swarm.IsTargetInCurrentWeaponRange(target))
        {
            _gameManager.CombatUI?.ShowCombatLog($"{swarm.Stats.CharacterName} occupies {target.Stats.CharacterName}'s space and continues swarming.");
        }
        else
        {
            _gameManager.CombatUI?.ShowCombatLog($"{swarm.Stats.CharacterName} shuffles toward {target.Stats.CharacterName}.");
        }

        yield return new WaitForSeconds(0.3f);

        // ── Swarm automatic damage (MM p.239): At the end of the swarm's turn,
        // any creature whose space the swarm occupies takes swarm damage automatically
        // (no attack roll). Then each damaged creature must make a Fortitude save
        // vs the distraction DC or become nauseated for 1 round.
        yield return _gameManager.StartCoroutine(ApplySwarmDamageToOccupants(swarm, indiscriminate));
    }

    /// <summary>
    /// D&D 3.5e swarm damage: automatically deals damage to all creatures occupying the
    /// swarm's space at the end of its turn. No attack roll required.
    /// Also triggers distraction (Fort save or nauseated 1 round).
    /// </summary>
    private IEnumerator ApplySwarmDamageToOccupants(CharacterController swarm, bool indiscriminate)
    {
        if (swarm == null || swarm.Stats == null || !swarm.Stats.IsSwarm)
            yield break;

        SwarmTraits traits = swarm.Stats.SwarmTraits;
        if (traits == null || !traits.IsSwarm)
            yield break;

        Vector2Int swarmPos = swarm.GridPosition;
        List<CharacterController> allChars = _gameManager.GetAllCharactersForAI();
        if (allChars == null)
            yield break;

        for (int i = 0; i < allChars.Count; i++)
        {
            CharacterController victim = allChars[i];
            if (victim == null || victim == swarm || victim.Stats == null || victim.Stats.IsDead)
                continue;

            // Swarms only damage creatures sharing their space
            if (victim.GridPosition != swarmPos)
                continue;

            // Skip friendly creatures unless this is an indiscriminate swarm
            if (!indiscriminate && !_gameManager.IsEnemyTeamForAI(swarm, victim))
                continue;

            // ── Roll swarm damage ──
            int damage = RollSwarmDamage(traits);
            if (damage <= 0)
                continue;

            string dmgTypeStr = traits.SwarmDamageType.ToString();
            _gameManager.CombatUI?.ShowCombatLog(
                $"<color=#FF6644>🐝 {swarm.Stats.CharacterName} swarm damage: {damage} {dmgTypeStr} damage to {victim.Stats.CharacterName}! (no attack roll)</color>");

            victim.Stats.TakeDamage(damage);
            yield return new WaitForSeconds(0.25f);

            // Check if victim died from swarm damage
            if (victim.Stats.IsDead)
            {
                _gameManager.CombatUI?.ShowCombatLog(
                    $"💀 {victim.Stats.CharacterName} is killed by the swarm!");
                victim.OnDeath();
                continue;
            }

            // ── Distraction: Fort save or nauseated 1 round (MM p.239) ──
            if (traits.DistractionDC > 0)
            {
                var saveResult = SavingThrowResolver.ResolveFortitudeSave(
                    victim.Stats, traits.DistractionDC, $"{swarm.Stats.CharacterName} distraction");

                if (saveResult.Succeeded)
                {
                    _gameManager.CombatUI?.ShowCombatLog(
                        $"💪 {victim.Stats.CharacterName} resists distraction (Fort {saveResult.Total} vs DC {traits.DistractionDC}).");
                }
                else
                {
                    _gameManager.CombatUI?.ShowCombatLog(
                        $"<color=#FFAA00>🤢 {victim.Stats.CharacterName} fails distraction save (Fort {saveResult.Total} vs DC {traits.DistractionDC}) — nauseated for 1 round!</color>");
                    victim.ApplyNauseatedCondition(1, $"{swarm.Stats.CharacterName} Distraction");
                }
                yield return new WaitForSeconds(0.2f);
            }

            // ── Poison rider (if applicable) ──
            if (traits.HasPoison && !string.IsNullOrEmpty(traits.PoisonId))
            {
                _gameManager.CombatUI?.ShowCombatLog(
                    $"☠ {victim.Stats.CharacterName} is exposed to {swarm.Stats.CharacterName}'s poison!");
                // Poison application handled by existing poison system if available
            }
        }
    }

    /// <summary>
    /// Rolls swarm damage from SwarmTraits. Uses SwarmDamage as flat if > 0,
    /// otherwise parses and rolls SwarmDamageDice (e.g. "1d6").
    /// </summary>
    private static int RollSwarmDamage(SwarmTraits traits)
    {
        // If a flat SwarmDamage value is set, use it directly (some swarms have fixed damage)
        if (traits.SwarmDamage > 0)
            return traits.SwarmDamage;

        // Parse dice notation (e.g., "1d6", "2d6")
        string dice = traits.SwarmDamageDice;
        if (string.IsNullOrWhiteSpace(dice))
            return 0;

        string trimmed = dice.Trim().ToLowerInvariant();
        int dIdx = trimmed.IndexOf('d');
        if (dIdx <= 0)
        {
            // Flat number
            if (int.TryParse(trimmed, out int flat))
                return flat;
            return 0;
        }

        string leftStr = trimmed.Substring(0, dIdx);
        string rightStr = dIdx + 1 < trimmed.Length ? trimmed.Substring(dIdx + 1) : "";

        // Handle bonus (e.g., "1d6+2")
        int bonus = 0;
        int plusIdx = rightStr.IndexOf('+');
        int minusIdx = rightStr.IndexOf('-');
        if (plusIdx >= 0)
        {
            string bonusStr = rightStr.Substring(plusIdx + 1);
            int.TryParse(bonusStr, out bonus);
            rightStr = rightStr.Substring(0, plusIdx);
        }
        else if (minusIdx >= 0)
        {
            string bonusStr = rightStr.Substring(minusIdx); // includes the minus
            int.TryParse(bonusStr, out bonus);
            rightStr = rightStr.Substring(0, minusIdx);
        }

        if (!int.TryParse(leftStr, out int count) || count <= 0)
            return 0;
        if (!int.TryParse(rightStr, out int sides) || sides <= 0)
            return 0;

        return DiceService.RollMultiple(count, sides, "swarm damage") + bonus;
    }

    private List<CharacterController> BuildSwarmTargetCandidates(CharacterController swarm, bool indiscriminate)
    {
        List<CharacterController> allCombatants = _gameManager != null
            ? _gameManager.GetAllCharactersForAI()
            : null;

        var candidates = new List<CharacterController>();
        if (allCombatants == null)
            return candidates;

        for (int i = 0; i < allCombatants.Count; i++)
        {
            CharacterController candidate = allCombatants[i];
            if (candidate == null || candidate == swarm || candidate.Stats == null || candidate.Stats.IsDead)
                continue;

            // Indiscriminate swarms (Summon Swarm) attack ALL creatures including caster
            // No friend/foe distinction - purely distance-based targeting
            if (!indiscriminate && !_gameManager.IsEnemyTeamForAI(swarm, candidate))
                continue;

            candidates.Add(candidate);
        }

        return candidates;
    }

    private const string ArmorPriorityBehaviorTag = "Uses Armor-Based Targeting";

    private static AIProfile GetProfile(CharacterController npc)
    {
        return npc != null ? npc.aiProfile : null;
    }

    public CharacterController SelectBestTarget(CharacterController npc, List<CharacterController> allCombatants)
    {
        if (npc == null || allCombatants == null)
            return null;

        CharacterController mirrorPriorityTarget = _gameManager != null
            ? _gameManager.GetMirrorImagePriorityTargetForAI(npc)
            : null;
        if (mirrorPriorityTarget != null)
        {
            Debug.Log($"[AI][MirrorImage] {npc.Stats.CharacterName} prioritizes nearest mirror-image entity: {mirrorPriorityTarget.Stats?.CharacterName}");
            return mirrorPriorityTarget;
        }

        LastKnownPositionTracker tracker = npc.GetComponent<LastKnownPositionTracker>();
        if (tracker == null)
            tracker = npc.gameObject.AddComponent<LastKnownPositionTracker>();

        var visibleTargets = new List<CharacterController>();
        var concealedTrackedTargets = new List<CharacterController>();

        for (int i = 0; i < allCombatants.Count; i++)
        {
            CharacterController candidate = allCombatants[i];
            if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!_gameManager.IsEnemyTeamForAI(npc, candidate))
                continue;

            if (ShouldExcludeTargetBecauseOfCharm(npc, candidate))
                continue;

            if (ShouldExcludeTargetBecauseOfFrightenedSource(npc, candidate))
                continue;

            // D&D 3.5e Sanctuary (PHB p.274): attacker must make Will save vs DC to target this creature.
            // On failure, the attacker must choose a different target. Checked per-target in AI selection.
            if (ShouldExcludeTargetBecauseOfSanctuary(npc, candidate))
                continue;

            // D&D 3.5e Hide from Undead (PHB p.241): undead cannot perceive the hidden creature.
            // Mindless undead are automatically hidden from; intelligent undead get a Will save.
            if (ShouldExcludeTargetBecauseOfHideFromUndead(npc, candidate))
                continue;

            if (CanSeeTarget(npc, candidate))
            {
                visibleTargets.Add(candidate);
                tracker.UpdateLastKnownPosition(candidate);
            }
            else if (tracker.HasLastKnownPosition(candidate))
            {
                concealedTrackedTargets.Add(candidate);
            }
        }

        // D&D 3.5e priority: visible enemies first, concealed enemies second (only if tracked).
        // Note: forgotten targets are not permanently excluded. If they are visible now,
        // they are added to visibleTargets and immediately tracked again above.
        CharacterController visibleTarget = SelectBestTargetFromCandidates(npc, visibleTargets);
        if (visibleTarget != null)
            return visibleTarget;

        if (concealedTrackedTargets.Count > 0)
        {
            tracker.AttemptListenChecks(concealedTrackedTargets, _gameManager);

            var pinpointedTargets = new List<CharacterController>();
            for (int i = 0; i < concealedTrackedTargets.Count; i++)
            {
                CharacterController candidate = concealedTrackedTargets[i];
                if (tracker.IsPinpointedThisRound(candidate))
                    pinpointedTargets.Add(candidate);
            }

            CharacterController pinpointedTarget = SelectBestTargetFromCandidates(npc, pinpointedTargets);
            if (pinpointedTarget != null)
                return pinpointedTarget;

            CharacterController trackedTarget = SelectBestTargetFromCandidates(npc, concealedTrackedTargets);
            if (trackedTarget != null)
                return trackedTarget;
        }

        return null;
    }

    private CharacterController SelectBestTargetFromCandidates(CharacterController npc, List<CharacterController> candidateTargets)
    {
        if (npc == null || candidateTargets == null || candidateTargets.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(npc.PriorityTargetName))
        {
            for (int i = 0; i < candidateTargets.Count; i++)
            {
                CharacterController candidate = candidateTargets[i];
                if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                    continue;
                if (candidate.Stats.CharacterName != npc.PriorityTargetName)
                    continue;

                Debug.Log($"[AI][PriorityTarget] {npc.Stats.CharacterName} prioritizes {candidate.Stats.CharacterName}");
                return candidate;
            }
        }

        AIProfile profile = GetProfile(npc);
        if (profile != null)
        {
            CharacterController profiled = SelectBestTargetFromProfile(npc, candidateTargets, profile);
            if (profiled != null)
                return profiled;
        }

        if (UsesArmorPriorityTargeting(npc))
        {
            CharacterController prioritized = SelectBestArmorPriorityTarget(npc, candidateTargets);
            if (prioritized != null)
                return prioritized;
        }

        return SelectBestTargetDefault(npc, candidateTargets);
    }

    private bool CanSeeTarget(CharacterController npc, CharacterController target)
    {
        if (npc == null || target == null || target.Stats == null || target.Stats.IsDead)
            return false;

        bool incomingIsRangedAttack = npc.IsEquippedWeaponRanged();
        return npc.CanSee(target, incomingIsRangedAttack);
    }

    private bool ShouldExcludeTargetBecauseOfCharm(CharacterController npc, CharacterController candidate)
    {
        if (_gameManager == null || npc == null || candidate == null)
            return false;

        if (!npc.HasCondition(CombatConditionType.Charmed))
            return false;

        List<ConditionService.ActiveCondition> active = _gameManager.GetActiveConditions(npc);
        if (active == null || active.Count == 0)
            return false;

        for (int i = 0; i < active.Count; i++)
        {
            ConditionService.ActiveCondition condition = active[i];
            if (condition == null || ConditionRules.Normalize(condition.Type) != CombatConditionType.Charmed)
                continue;

            CharacterController source = condition.Source;
            if (source == null && condition.Data is CharmedConditionData charmData)
                source = charmData.Caster;

            if (source != null && source == candidate)
                return true;

            if (source == null
                && !string.IsNullOrWhiteSpace(condition.SourceName)
                && candidate.Stats != null
                && string.Equals(condition.SourceName, candidate.Stats.CharacterName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private bool ShouldExcludeTargetBecauseOfFrightenedSource(CharacterController npc, CharacterController candidate)
    {
        if (_gameManager == null || npc == null || candidate == null)
            return false;

        if (!npc.HasCondition(CombatConditionType.Frightened))
            return false;

        List<ConditionService.ActiveCondition> active = _gameManager.GetActiveConditions(npc);
        if (active == null || active.Count == 0)
            return false;

        for (int i = 0; i < active.Count; i++)
        {
            ConditionService.ActiveCondition condition = active[i];
            if (condition == null || ConditionRules.Normalize(condition.Type) != CombatConditionType.Frightened)
                continue;

            CharacterController source = condition.Source;
            if (source == null && condition.Data is FrightenedConditionData fearData)
                source = fearData.Caster;

            if (source != null && source == candidate)
                return true;

            if (source == null
                && !string.IsNullOrWhiteSpace(condition.SourceName)
                && candidate.Stats != null
                && string.Equals(condition.SourceName, candidate.Stats.CharacterName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// D&D 3.5e Sanctuary (PHB p.274): An attacker must succeed on a Will save (DC set
    /// when the spell was cast) or be unable to attack/target the warded creature.
    /// Each potential attacker rolls once per targeting attempt.
    /// </summary>
    private bool ShouldExcludeTargetBecauseOfSanctuary(CharacterController npc, CharacterController candidate)
    {
        if (_gameManager == null || npc == null || candidate == null)
            return false;
        if (candidate.Stats == null || !candidate.Stats.SanctuaryActive)
            return false;

        int dc = candidate.Stats.SanctuaryDC;
        var saveResult = SavingThrowResolver.ResolveWillSave(npc.Stats, dc, "Sanctuary");
        string npcName = npc.Stats != null ? npc.Stats.CharacterName : "NPC";
        string candName = candidate.Stats.CharacterName;

        if (saveResult.Succeeded)
        {
            _gameManager.CombatUI?.ShowCombatLog(
                $"🛡️ {npcName} overcomes {candName}'s Sanctuary (Will {saveResult.Total} vs DC {dc}).");
            Debug.Log($"[AI][Sanctuary] {npcName} passed Will save {saveResult.Total} vs DC {dc} — can target {candName}");
            return false; // can target
        }
        else
        {
            _gameManager.CombatUI?.ShowCombatLog(
                $"🛡️ {npcName} is unable to attack {candName} — Sanctuary! (Will {saveResult.Total} vs DC {dc})");
            Debug.Log($"[AI][Sanctuary] {npcName} failed Will save {saveResult.Total} vs DC {dc} — cannot target {candName}");
            return true; // excluded
        }
    }

    /// <summary>
    /// D&D 3.5e Hide from Undead (PHB p.241): Undead cannot perceive the warded creature.
    /// Mindless undead are automatically affected (no save). Intelligent undead (INT ≥ 1)
    /// get a Will save to see through the ward.
    /// Non-undead NPCs are unaffected by this spell.
    /// </summary>
    private bool ShouldExcludeTargetBecauseOfHideFromUndead(CharacterController npc, CharacterController candidate)
    {
        if (_gameManager == null || npc == null || candidate == null)
            return false;
        if (candidate.Stats == null || !candidate.Stats.HideFromUndeadActive)
            return false;

        // Only affects undead attackers
        if (!_gameManager.IsUndeadCharacterForAI(npc))
            return false;

        string npcName = npc.Stats != null ? npc.Stats.CharacterName : "NPC";
        string candName = candidate.Stats.CharacterName;

        // Mindless undead: automatically hidden from, no save
        if (npc.Stats != null && npc.Stats.IsMindless)
        {
            _gameManager.CombatUI?.ShowCombatLog(
                $"👻 {npcName} cannot perceive {candName} — Hidden from Undead! (mindless, no save)");
            Debug.Log($"[AI][HideFromUndead] Mindless undead {npcName} auto-excluded from targeting {candName}");
            return true;
        }

        // Intelligent undead: Will save to see through
        int dc = candidate.Stats.HideFromUndeadDC;
        var saveResult = SavingThrowResolver.ResolveWillSave(npc.Stats, dc, "Hide from Undead");

        if (saveResult.Succeeded)
        {
            _gameManager.CombatUI?.ShowCombatLog(
                $"👻 {npcName} sees through {candName}'s ward! (Will {saveResult.Total} vs DC {dc})");
            Debug.Log($"[AI][HideFromUndead] Intelligent undead {npcName} passed Will {saveResult.Total} vs DC {dc}");
            // Spell breaks for this target when the undead sees through
            candidate.Stats.HideFromUndeadActive = false;
            var statusMgr = candidate.StatusEffectManager;
            statusMgr?.RemoveEffectsBySpellId(SpellNames.HIDE_FROM_UNDEAD);
            _gameManager.CombatUI?.ShowCombatLog(
                $"👻 Hide from Undead on {candName} is broken — {npcName} perceived them!");
            return false;
        }
        else
        {
            _gameManager.CombatUI?.ShowCombatLog(
                $"👻 {npcName} cannot perceive {candName} — Hidden from Undead! (Will {saveResult.Total} vs DC {dc})");
            Debug.Log($"[AI][HideFromUndead] Intelligent undead {npcName} failed Will {saveResult.Total} vs DC {dc}");
            return true;
        }
    }

    private CharacterController SelectBestTargetFromProfile(CharacterController npc, List<CharacterController> allCombatants, AIProfile profile)
    {
        CharacterController best = null;
        float bestScore = float.NegativeInfinity;

        var enemyCandidates = new List<CharacterController>();
        bool hasConsciousEnemy = false;

        for (int i = 0; i < allCombatants.Count; i++)
        {
            CharacterController candidate = allCombatants[i];
            if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!_gameManager.IsEnemyTeamForAI(npc, candidate))
                continue;

            enemyCandidates.Add(candidate);
            if (!candidate.IsUnconscious)
                hasConsciousEnemy = true;
        }

        bool ignoreUnconscious = profile.ShouldIgnoreUnconsciousTargets(npc) && hasConsciousEnemy;

        for (int i = 0; i < enemyCandidates.Count; i++)
        {
            CharacterController candidate = enemyCandidates[i];
            if (ignoreUnconscious && candidate.IsUnconscious)
                continue;

            float score = profile.ScoreTarget(candidate, npc);
            score += GetPerceptionTargetingAdjustment(npc, candidate, profile);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (best != null)
        {
            bool incomingIsRanged = npc.IsEquippedWeaponRanged();
            int missChance = best.GetMissChance(npc, incomingIsRanged);
            bool canSee = npc.CanSee(best, incomingIsRanged);
            Debug.Log($"[AI][Profile:{profile.ProfileName}] {npc.Stats.CharacterName} targets {best.Stats.CharacterName} score={bestScore:F1} concealment={missChance}% ({GetConcealmentPriorityLabel(missChance, canSee)})");
        }

        return best;
    }

    private CharacterController SelectBestTargetDefault(CharacterController npc, List<CharacterController> allCombatants)
    {
        CharacterController best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < allCombatants.Count; i++)
        {
            CharacterController candidate = allCombatants[i];
            if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!_gameManager.IsEnemyTeamForAI(npc, candidate))
                continue;

            float score = GetTargetPriority(npc, candidate);
            score += GetPerceptionTargetingAdjustment(npc, candidate);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (best != null)
        {
            bool incomingIsRanged = npc.IsEquippedWeaponRanged();
            int missChance = best.GetMissChance(npc, incomingIsRanged);
            bool canSee = npc.CanSee(best, incomingIsRanged);
            Debug.Log($"[AI][Default] {npc.Stats.CharacterName} targets {best.Stats.CharacterName} score={bestScore:F1} concealment={missChance}% ({GetConcealmentPriorityLabel(missChance, canSee)})");
        }

        return best;
    }

    private CharacterController SelectBestArmorPriorityTarget(CharacterController npc, List<CharacterController> allCombatants)
    {
        CharacterController best = null;
        float bestScore = float.NegativeInfinity;

        int maxRange = GetMaximumAttackRangeInSquares(npc);

        for (int i = 0; i < allCombatants.Count; i++)
        {
            CharacterController candidate = allCombatants[i];
            if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!_gameManager.IsEnemyTeamForAI(npc, candidate))
                continue;

            int distance = SquareGridUtils.GetDistance(npc.GridPosition, candidate.GridPosition);
            if (distance > maxRange)
                continue;

            float armorScore = GetArmorPriorityScore(candidate);
            float distanceBonus = Mathf.Max(0f, maxRange - distance) * 2f;
            float woundedBonus = Mathf.Clamp01(1f - ((float)candidate.Stats.CurrentHP / Mathf.Max(1f, candidate.Stats.TotalMaxHP))) * 1.5f;
            float totalScore = armorScore + distanceBonus + woundedBonus;
            totalScore += GetPerceptionTargetingAdjustment(npc, candidate);

            if (totalScore > bestScore)
            {
                bestScore = totalScore;
                best = candidate;
            }
        }

        if (best != null)
        {
            Debug.Log($"[AI][ArmorPriority] {npc.Stats.CharacterName} targets {best.Stats.CharacterName} ({best.GetArmorTag()}) score={bestScore:F1}");
        }

        return best;
    }

    private bool UsesArmorPriorityTargeting(CharacterController npc)
    {
        if (npc == null)
            return false;

        if (npc.Tags != null && npc.Tags.HasTag(ArmorPriorityBehaviorTag))
            return true;

        if (npc.Stats != null && npc.Stats.CreatureTags != null)
        {
            for (int i = 0; i < npc.Stats.CreatureTags.Count; i++)
            {
                if (string.Equals(npc.Stats.CreatureTags[i], ArmorPriorityBehaviorTag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private float GetArmorPriorityScore(CharacterController target)
    {
        if (target == null)
            return 0f;

        if (target.Tags != null)
        {
            if (target.Tags.HasTag("Unarmored")) return 100f;
            if (target.Tags.HasTag("Light Armor")) return 75f;
            if (target.Tags.HasTag("Medium Armor")) return 50f;
            if (target.Tags.HasTag("Heavy Armor")) return 25f;
        }

        string armorTag = target.GetArmorTag();
        if (string.Equals(armorTag, "Unarmored", StringComparison.OrdinalIgnoreCase)) return 100f;
        if (string.Equals(armorTag, "Light Armor", StringComparison.OrdinalIgnoreCase)) return 75f;
        if (string.Equals(armorTag, "Medium Armor", StringComparison.OrdinalIgnoreCase)) return 50f;
        if (string.Equals(armorTag, "Heavy Armor", StringComparison.OrdinalIgnoreCase)) return 25f;

        return 10f;
    }

    private float GetPerceptionTargetingAdjustment(CharacterController npc, CharacterController target, AIProfile profile = null)
    {
        if (npc == null || target == null || target.Stats == null)
            return 0f;

        float adjustment = 0f;

        if (target.IsInvisibleCondition)
        {
            int distanceSquares = SquareGridUtils.GetDistance(npc.GridPosition, target.GridPosition);
            bool hasScent = npc.Stats != null && npc.Stats.HasScent;

            if (hasScent)
            {
                // Scent gives a strong close-range lock and partial tracking at longer range.
                adjustment += distanceSquares <= 6 ? 4f : -6f;
            }
            else
            {
                // Without scent, invisible targets are much harder to prioritize reliably.
                adjustment += -18f;
            }
        }

        bool incomingIsRanged = npc.IsEquippedWeaponRanged();
        npc.UpdateLastKnownPosition(target, incomingIsRanged);

        int missChance = target.GetMissChance(npc, incomingIsRanged);
        bool canSeeTarget = npc.CanSee(target, incomingIsRanged);
        bool hasLastKnownPosition = npc.GetLastKnownPosition(target).HasValue;

        if (!canSeeTarget)
        {
            // Keep baseline visibility awareness so non-concealment blind spots still receive a penalty.
            adjustment += -12f;
            if (hasLastKnownPosition)
                adjustment += 4f;
        }

        if (profile == null || profile.PrioritizeVisibleTargets)
        {
            float concealmentMultiplier = profile != null ? Mathf.Max(0f, profile.ConcealmentPenaltyMultiplier) : 1f;
            adjustment += GetConcealmentTargetingAdjustment(missChance, canSeeTarget, hasLastKnownPosition, concealmentMultiplier);
        }

        return adjustment;
    }

    internal static float GetConcealmentTargetingAdjustment(int missChance, bool canSeeTarget, bool hasLastKnownPosition, float penaltyMultiplier = 1f)
    {
        float multiplier = Mathf.Max(0f, penaltyMultiplier);
        if (multiplier <= 0f)
            return 0f;

        int normalizedMissChance = Mathf.Clamp(missChance, 0, 100);

        if (normalizedMissChance <= 0)
            return 50f * multiplier; // Highest priority: clean line of attack.

        if (normalizedMissChance < 50)
            return -30f * multiplier; // Medium priority: partial concealment.

        float totalConcealmentPenalty = -80f * multiplier; // Low priority by default.
        if (!canSeeTarget)
            totalConcealmentPenalty -= 40f * multiplier; // Lowest priority when target cannot be seen.

        if (hasLastKnownPosition)
            totalConcealmentPenalty += 20f * multiplier; // Slight recovery when a trackable position exists.

        return totalConcealmentPenalty;
    }

    private static string GetConcealmentPriorityLabel(int missChance, bool canSeeTarget)
    {
        int normalizedMissChance = Mathf.Clamp(missChance, 0, 100);
        if (normalizedMissChance <= 0)
            return "visible";
        if (normalizedMissChance < 50)
            return "partially concealed";
        if (canSeeTarget)
            return "totally concealed";

        return "unknown position";
    }

    public float GetTargetPriority(CharacterController npc, CharacterController target)
    {
        if (npc == null || target == null || target.Stats == null)
            return float.MinValue;

        int distance = SquareGridUtils.GetDistance(npc.GridPosition, target.GridPosition);
        float distanceScore = Mathf.Max(0f, 12f - distance) * 1.8f; // proximity

        float threatScore = CalculateThreat(npc, target) * 2.0f; // healers/casters/high offense

        float hpRatio = target.Stats.TotalMaxHP > 0
            ? Mathf.Clamp01((float)target.Stats.CurrentHP / target.Stats.TotalMaxHP)
            : 1f;
        float woundedBonus = (1f - hpRatio) * 8f; // finish wounded enemies

        float armorEase = Mathf.Clamp(26f - target.Stats.ArmorClass, -6f, 8f) * 0.7f; // easier AC is better

        bool flankingOpportunity = CombatUtils.CanThreatenTargetFromPosition(npc, npc.GridPosition, target);
        float flankingBonus = flankingOpportunity ? 2.5f : 0f;

        string creatureType = target.Stats.CreatureType ?? string.Empty;
        bool vulnerableType = creatureType.IndexOf("undead", StringComparison.OrdinalIgnoreCase) >= 0
            || creatureType.IndexOf("outsider", StringComparison.OrdinalIgnoreCase) >= 0;
        float vulnerabilityBonus = vulnerableType ? 1.5f : 0f;

        return distanceScore + threatScore + woundedBonus + armorEase + flankingBonus + vulnerabilityBonus;
    }

    public float CalculateThreat(CharacterController observer, CharacterController target)
    {
        if (target == null || target.Stats == null)
            return 0f;

        float score = 0f;

        if (target.Stats.IsCleric || target.Stats.IsWizard)
            score += 3f;
        if (target.Stats.IsRogue)
            score += 2f;

        score += Mathf.Clamp(target.Stats.Level * 0.35f, 0f, 4f);
        score += Mathf.Clamp(target.Stats.STRMod + target.Stats.DEXMod, -2f, 6f) * 0.35f;

        if (target.HasCondition(CombatConditionType.Prone))
            score -= 0.75f;

        return score;
    }

    public SquareCell EvaluateMovementOptions(CharacterController mover, Vector2Int targetPos, bool retreat, CharacterController targetCharacter = null, AIProfile profile = null)
    {
        if (mover == null || mover.Stats == null || _gameManager.Grid == null)
            return null;

        if (profile == null)
            profile = GetProfile(mover);

        int moverMoveRange = _gameManager.GetCurrentMoveRangeSquares(mover);
        List<SquareCell> moveCells = _gameManager.Grid.GetCellsInRange(mover.GridPosition, moverMoveRange);
        List<CharacterController> allCombatants = targetCharacter != null ? _gameManager.GetAllCharactersForAI() : null;

        SquareCell bestCell = null;
        float bestScore = float.NegativeInfinity;

        int preferredRange = profile != null && profile.Movement != null
            ? Mathf.Max(0, profile.Movement.PreferredRangeSquares)
            : 1;

        bool avoidAoOs = profile != null
            && profile.Movement != null
            && profile.Movement.AvoidAoOs
            && !profile.ShouldIgnoreAoO(mover);
        bool seekFlanking = profile == null || profile.Movement == null || profile.Movement.SeekFlanking;

        for (int i = 0; i < moveCells.Count; i++)
        {
            SquareCell cell = moveCells[i];
            if (cell == null)
                continue;

            if (!_gameManager.Grid.CanPlaceCreature(cell.Coords, mover.GetVisualSquaresOccupied(), mover))
                continue;

            AoOPathResult pathResult = _gameManager.FindPath(mover, cell.Coords, avoidThreats: false, maxRangeOverride: moverMoveRange);
            if (pathResult == null || pathResult.Path == null)
                continue;

            int dist = SquareGridUtils.GetDistance(cell.Coords, targetPos);
            bool canThreatenFromCell = false;
            bool wouldFlankFromCell = false;
            if (targetCharacter != null)
            {
                canThreatenFromCell = CombatUtils.CanThreatenTargetFromPosition(mover, cell.Coords, targetCharacter);
                if (canThreatenFromCell)
                {
                    CharacterController flankPartner;
                    wouldFlankFromCell = CombatUtils.IsAttackerFlankingFromPosition(
                        mover,
                        cell.Coords,
                        targetCharacter,
                        allCombatants,
                        out flankPartner);
                }
            }

            float score;
            if (retreat)
            {
                score = dist * 2f;
            }
            else
            {
                int distanceToPreferred = Mathf.Abs(dist - preferredRange);
                score = -distanceToPreferred * 2f;
                if (canThreatenFromCell)
                    score += 2f;
                if (seekFlanking && wouldFlankFromCell)
                    score += 3f;
            }

            if (pathResult.ProvokesAoOs)
                score += avoidAoOs ? -1000f : -2f * pathResult.ProvokedAoOs.Count;

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
            }
        }

        return bestCell;
    }

    private SquareCell EvaluateWithdrawRetreatDestination(CharacterController mover, Vector2Int dangerSource)
    {
        if (mover == null || mover.Stats == null || _gameManager.Grid == null)
            return null;

        int withdrawRange = Mathf.Max(0, _gameManager.GetCurrentMoveRangeSquares(mover) * 2);
        List<SquareCell> moveCells = _gameManager.Grid.GetCellsInRange(mover.GridPosition, withdrawRange);

        SquareCell bestCell = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < moveCells.Count; i++)
        {
            SquareCell cell = moveCells[i];
            if (cell == null)
                continue;

            if (!_gameManager.Grid.CanPlaceCreature(cell.Coords, mover.GetVisualSquaresOccupied(), mover))
                continue;

            AoOPathResult pathResult = _gameManager.FindPath(
                mover,
                cell.Coords,
                avoidThreats: false,
                maxRangeOverride: withdrawRange,
                allowThroughAllies: true,
                allowThroughEnemies: false,
                suppressFirstSquareAoO: true);

            if (pathResult == null || pathResult.Path == null || pathResult.Path.Count == 0)
                continue;

            int distance = SquareGridUtils.GetDistance(cell.Coords, dangerSource);
            int provokes = pathResult.ProvokedAoOs != null ? pathResult.ProvokedAoOs.Count : 0;

            float score = distance * 3f;
            score -= provokes * 4f;
            score -= Mathf.Max(0, pathResult.Path.Count - withdrawRange) * 2f;

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
            }
        }

        return bestCell;
    }

    public float EvaluateAttackOptions(CharacterController npc, CharacterController target)
    {
        if (npc == null || target == null || target.Stats == null)
            return float.MinValue;

        bool inRange = npc.IsTargetInCurrentWeaponRange(target);
        float score = inRange ? 8f : 2f;

        score += ShouldUseManeuver(npc, target) ? 2.5f : 0.75f;

        SpellData spell = SelectSpell(npc, target);
        if (spell != null)
            score += 1.5f;

        float hpPercent = npc.Stats.TotalMaxHP > 0 ? (float)npc.Stats.CurrentHP / npc.Stats.TotalMaxHP : 1f;
        if (hpPercent < 0.35f)
            score -= 2f;

        return score;
    }

    public bool ShouldUseManeuver(CharacterController npc, CharacterController target)
    {
        if (npc == null || target == null)
            return false;

        AIProfile profile = GetProfile(npc);
        if (_gameManager != null
            && _gameManager.CanUseCoupDeGraceAttackOption(npc)
            && ShouldNPCUseCoupDeGrace(npc, profile))
        {
            return true;
        }

        if (npc.IsGrappling())
            return false;
        if (!npc.Actions.HasStandardAction)
            return false;

        if (profile != null)
        {
            SpecialAttackType? preferred = profile.GetPreferredManeuver(npc, target);
            if (preferred.HasValue)
            {
                if (!npc.CanPerformSpecialAttack(preferred.Value))
                {
                    Debug.Log($"[AI][Maneuver] {npc.Stats.CharacterName} skips {preferred.Value} due to ranged-only loadout ({npc.GetPrimaryWeaponType()}).");
                    return false;
                }

                if (preferred.Value == SpecialAttackType.Trip)
                    return !target.HasCondition(CombatConditionType.Prone) && npc.HasMeleeWeaponEquipped();

                if (preferred.Value == SpecialAttackType.Disarm)
                    return target.HasDisarmableWeaponEquipped();

                if (preferred.Value == SpecialAttackType.Grapple)
                    return profile.ShouldInitiateGrapple(npc, target);

                if (preferred.Value == SpecialAttackType.Sunder)
                    return target.HasSunderableItemEquipped();

                return true;
            }

            // Profile present but no preferred maneuver => obey profile and skip legacy fallback.
            return false;
        }

        if (target.GetEquippedMainWeapon() != null
            && npc.Stats.STRMod >= 3
            && npc.CanPerformSpecialAttack(SpecialAttackType.Disarm))
            return true; // disarm preference
        if (!target.Stats.IsProne
            && npc.HasMeleeWeaponEquipped()
            && npc.CanPerformSpecialAttack(SpecialAttackType.Trip))
            return true; // trip preference

        return npc.Stats.STRMod >= 4
            && npc.CanPerformSpecialAttack(SpecialAttackType.Grapple);
    }

    private bool TryExecutePreferredManeuver(CharacterController npc, CharacterController target, AIProfile profile)
    {
        if (npc == null || target == null)
            return false;

        if (_gameManager != null
            && _gameManager.CanUseCoupDeGraceAttackOption(npc)
            && ShouldNPCUseCoupDeGrace(npc, profile))
        {
            return _gameManager.TryNPCSpecialAttackByTypeForAI(npc, target, SpecialAttackType.CoupDeGrace);
        }

        if (profile != null)
        {
            SpecialAttackType? preferred = profile.GetPreferredManeuver(npc, target);
            if (preferred.HasValue)
            {
                if (!npc.CanPerformSpecialAttack(preferred.Value))
                {
                    Debug.Log($"[AI][Maneuver] {npc.Stats.CharacterName} blocked from executing {preferred.Value} due to ranged-only loadout ({npc.GetPrimaryWeaponType()}).");
                    return false;
                }

                return _gameManager.TryNPCSpecialAttackByTypeForAI(npc, target, preferred.Value);
            }

            return false;
        }

        return _gameManager.TryNPCSpecialAttackIfBeneficialForAI(npc, target);
    }

    private static bool ShouldNPCUseCoupDeGrace(CharacterController npc, AIProfile profile)
    {
        if (npc == null)
            return false;

        if (npc.EnemyUseCoupDeGraceOverride.HasValue)
            return npc.EnemyUseCoupDeGraceOverride.Value;

        AIProfile resolvedProfile = profile ?? GetProfile(npc);
        return resolvedProfile != null && resolvedProfile.ShouldUseCoupDeGrace(npc);
    }

    private static bool HasCastablePreparedSpells(CharacterController caster)
    {
        if (caster == null)
            return false;

        SpellcastingComponent spellcasting = caster.Spellcasting;
        return spellcasting != null && spellcasting.CanCastSpells && spellcasting.HasAnyCastablePreparedSpell();
    }

    private int GetBestAvailableSpellRangeInSquares(CharacterController caster, CharacterController target)
    {
        if (caster == null || caster.Stats == null)
            return 0;

        SpellData bestSpell = SelectSpell(caster, target);
        if (bestSpell == null)
            return 0;

        int rangeSquares = bestSpell.GetRangeSquaresForCasterLevel(caster.Stats.GetCasterLevel());
        return Mathf.Max(1, rangeSquares);
    }

    private bool TryExecuteSpellcastAction(CharacterController caster, CharacterController fallbackTarget)
    {
        if (_gameManager == null || caster == null || caster.Stats == null)
            return false;

        if (!caster.Actions.HasStandardAction)
            return false;

        if (!caster.Stats.IsSpellcaster)
            return false;

        SpellcastingComponent spellcasting = caster.Spellcasting;
        List<SpellData> castableSpells = spellcasting != null ? spellcasting.GetCastablePreparedSpells() : null;
        int castableCount = castableSpells != null ? castableSpells.Count : 0;

        SpellData spell = SelectSpell(caster, fallbackTarget);
        if (spell == null)
        {
            Debug.Log($"[AI][Spell] {caster.Stats.CharacterName} has no castable spell choice. castablePrepared={castableCount}");
            return false;
        }

        // ── T1.3: Defensive casting awareness ──
        List<CharacterController> allCombatants = _gameManager.GetAllCharactersForAI();
        int defensiveResult = AISpellcastingStrategist.EvaluateDefensiveCasting(caster, spell, allCombatants);
        if (defensiveResult == 0)
        {
            // Can't safely cast in melee — try a lower-level spell
            SpellData altSpell = SelectLowerLevelAlternative(caster, fallbackTarget, spell.SpellLevel);
            if (altSpell != null)
            {
                int altResult = AISpellcastingStrategist.EvaluateDefensiveCasting(caster, altSpell, allCombatants);
                if (altResult > 0)
                {
                    Debug.Log($"[AI][Spell] {caster.Stats.CharacterName} switches from {spell.Name} (too risky in melee) to {altSpell.Name}");
                    spell = altSpell;
                }
                else
                {
                    Debug.Log($"[AI][Spell] {caster.Stats.CharacterName} can't safely cast any spell while threatened — aborting spellcasting.");
                    return false;
                }
            }
            else
            {
                Debug.Log($"[AI][Spell] {caster.Stats.CharacterName} can't safely cast {spell.Name} while threatened and has no alternatives.");
                return false;
            }
        }
        else if (defensiveResult == 1)
        {
            Debug.Log($"[AI][Spell] {caster.Stats.CharacterName} will cast {spell.Name} defensively (threatened in melee).");
        }

        CharacterController spellTarget = SelectBestSpellTarget(caster, spell, fallbackTarget);
        if (spellTarget == null)
        {
            int rangeSquares = Mathf.Max(1, spell.GetRangeSquaresForCasterLevel(caster.Stats.GetCasterLevel()));
            Debug.Log($"[AI][Spell] {caster.Stats.CharacterName} selected {spell.Name} but found no valid targets in range ({rangeSquares} squares).");
            return false;
        }

        int targetDistance = SquareGridUtils.GetDistance(caster.GridPosition, spellTarget.GridPosition);
        int spellRange = Mathf.Max(1, spell.GetRangeSquaresForCasterLevel(caster.Stats.GetCasterLevel()));
        bool isAllyTargeted = AISpellcastingStrategist.IsAllyTargetedSpell(spell);
        string targetRelation = isAllyTargeted ? "ally" : "enemy";
        Debug.Log($"[AI][Spell] {caster.Stats.CharacterName} evaluating {spell.Name}: target={spellTarget.Stats.CharacterName} ({targetRelation}), distance={targetDistance}, range={spellRange}, castablePrepared={castableCount}");

        bool casted = _gameManager.TryNPCPerformSpellCastForAI(caster, spellTarget, spell);
        if (!casted)
        {
            Debug.Log($"[AI][Spell] {caster.Stats.CharacterName} failed to cast {spell.Name} on {spellTarget.Stats.CharacterName}. (Likely invalid target constraints, LOS, or range race)");
            return false;
        }

        Debug.Log($"[AI][Spell] {caster.Stats.CharacterName} casts {spell.Name} on {spellTarget.Stats.CharacterName}");
        return true;
    }

    /// <summary>Find a lower-level spell alternative when the primary choice is too risky in melee.</summary>
    private SpellData SelectLowerLevelAlternative(CharacterController caster, CharacterController target, int maxLevel)
    {
        if (caster == null || caster.Stats == null) return null;

        SpellcastingComponent spellcasting = caster.Spellcasting;
        if (spellcasting == null) return null;

        List<SpellData> castable = spellcasting.GetCastablePreparedSpells();
        if (castable == null) return null;

        SpellData best = null;
        float bestScore = float.NegativeInfinity;

        List<CharacterController> allCombatants = _gameManager?.GetAllCharactersForAI() ?? new List<CharacterController>();

        for (int i = 0; i < castable.Count; i++)
        {
            SpellData spell = castable[i];
            if (spell == null || spell.SpellLevel >= maxLevel) continue;

            float score = AISpellcastingStrategist.ScoreSpellComprehensive(
                spell, caster, target, allCombatants, _gameManager);

            if (score > bestScore)
            {
                bestScore = score;
                best = spell;
            }
        }

        return best;
    }

    private CharacterController SelectBestSpellTarget(CharacterController caster, SpellData spell, CharacterController fallbackTarget)
    {
        if (_gameManager == null || caster == null || spell == null)
            return fallbackTarget;

        List<CharacterController> allCombatants = _gameManager.GetAllCharactersForAI();
        if (allCombatants == null || allCombatants.Count == 0)
            return fallbackTarget;

        // ── T1.1: Use comprehensive target selection (supports allies AND enemies) ──
        return AISpellcastingStrategist.SelectBestSpellTarget(
            caster, spell, fallbackTarget, allCombatants, _gameManager);
    }

    private static bool HasCreatureTag(CharacterController character, string tag)
    {
        if (character?.Stats?.CreatureTags == null || string.IsNullOrWhiteSpace(tag))
            return false;

        for (int i = 0; i < character.Stats.CreatureTags.Count; i++)
        {
            if (string.Equals(character.Stats.CreatureTags[i], tag, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsMagicMissileOnlyCaster(CharacterController caster)
    {
        if (caster == null)
            return false;

        if (caster.Tags != null && caster.Tags.HasTag("AI:MagicMissileOnly"))
            return true;

        return HasCreatureTag(caster, "AI:MagicMissileOnly");
    }

    public SpellData SelectSpell(CharacterController caster, CharacterController target)
    {
        if (caster == null || caster.Stats == null)
            return null;

        SpellcastingComponent spellcasting = caster.Spellcasting;
        if (spellcasting == null || !spellcasting.CanCastSpells || !spellcasting.HasAnyCastablePreparedSpell())
            return null;

        List<SpellData> castable = spellcasting.GetCastablePreparedSpells();
        if (castable == null || castable.Count == 0)
            return null;

        if (IsMagicMissileOnlyCaster(caster))
        {
            for (int i = 0; i < castable.Count; i++)
            {
                SpellData spell = castable[i];
                if (spell != null && string.Equals(spell.SpellId, SpellNames.MAGIC_MISSILE, StringComparison.OrdinalIgnoreCase))
                    return spell;
            }

            return null;
        }

        AIProfile profile = GetProfile(caster);
        SpellcasterAIProfile spellcasterProfile = profile as SpellcasterAIProfile;

        SpellData best = null;
        float bestScore = float.NegativeInfinity;

        List<CharacterController> allCombatants = _gameManager != null
            ? _gameManager.GetAllCharactersForAI()
            : new List<CharacterController>();

        // Cache StatusEffectManager for buff-already-active checks (prevents wasting spell slots).
        StatusEffectManager statusMgr = caster.StatusEffectManager;

        for (int i = 0; i < castable.Count; i++)
        {
            SpellData spell = castable[i];
            if (spell == null)
                continue;

            // ── Buff-already-active check ──
            // D&D 3.5e: Same spell doesn't stack — skip buff/illusion spells already active on the caster.
            // This prevents AI from wasting actions and spell slots recasting Mage Armor, Shield,
            // Mirror Image, etc. when they're already providing their benefit.
            if ((spell.EffectType == SpellEffectType.Buff || spell.EffectType == SpellEffectType.Illusion) && statusMgr != null)
            {
                bool alreadyActive = statusMgr.HasEffect(spell.SpellId);
                if (alreadyActive)
                {
                    int remaining = statusMgr.GetRemainingRounds(spell.SpellId);
                    // Allow recasting if about to expire (≤ 1 round remaining) — useful for
                    // long fights where the dragon wants to maintain its buffs.
                    if (remaining > 1 || remaining == -1) // -1 = indefinite duration
                    {
                        Debug.Log($"[AI][Spell] {caster.Stats.CharacterName}: Skipping {spell.Name} — already active ({remaining} rounds remaining)");
                        continue;
                    }
                }
            }

            float score;

            if (spellcasterProfile != null)
            {
                score = spellcasterProfile.ScoreSpell(spell, caster, target, allCombatants, _gameManager);
            }
            else
            {
                // ── Comprehensive scoring for non-profile casters (Tiers 1–4) ──
                score = AISpellcastingStrategist.ScoreSpellComprehensive(
                    spell, caster, target, allCombatants, _gameManager);
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = spell;
            }
        }

        return best;
    }

    public AIActionType SelectBestAction(CharacterController npc, CharacterController target, bool preferAggression)
    {
        if (npc == null || target == null)
            return AIActionType.Wait;

        float hpPercent = npc.Stats.TotalMaxHP > 0 ? (float)npc.Stats.CurrentHP / npc.Stats.TotalMaxHP : 1f;
        int distance = SquareGridUtils.GetDistance(npc.GridPosition, target.GridPosition);
        bool canCharge = _gameManager.ShouldNPCUseChargeForAI(npc, target);

        AIProfile profile = GetProfile(npc);
        bool profilePrefersCharge = profile == null || profile.ShouldPreferCharge(npc, target, distance, preferAggression);

        if (!preferAggression && hpPercent < 0.30f && npc.Actions.HasMoveAction)
            return AIActionType.Retreat;

        if (canCharge && profilePrefersCharge)
            return AIActionType.Charge;

        if (npc.IsTargetInCurrentWeaponRange(target))
        {
            if (ShouldUseManeuver(npc, target))
                return AIActionType.SpecialManeuver;
            return AIActionType.Attack;
        }

        if (distance > 1 && npc.Actions.HasMoveAction)
            return AIActionType.Move;

        return AIActionType.Wait;
    }

    private CharacterController SelectLowestHPEnemy(CharacterController npc)
    {
        List<CharacterController> all = _gameManager.GetAllCharactersForAI();
        CharacterController weakest = null;
        int lowestHP = int.MaxValue;

        for (int i = 0; i < all.Count; i++)
        {
            CharacterController candidate = all[i];
            if (candidate == null || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!_gameManager.IsEnemyTeamForAI(npc, candidate))
                continue;

            if (candidate.Stats.CurrentHP < lowestHP)
            {
                lowestHP = candidate.Stats.CurrentHP;
                weakest = candidate;
            }
        }

        return weakest;
    }

    private int GetMaximumAttackRangeInSquares(CharacterController npc)
    {
        ItemData weapon = npc.GetEquippedMainWeapon();
        if (weapon == null)
            return 1;

        if (weapon.WeaponCat == WeaponCategory.Ranged || weapon.RangeIncrement > 0)
            return RangeCalculator.GetMaxRangeSquares(weapon.RangeIncrement, weapon.IsThrown);

        return 1;
    }

    // ==================== AI COUNTERSPELL SUPPORT ====================

    /// <summary>
    /// Evaluate whether an NPC spellcaster should ready a counterspell this turn.
    /// AI readies counterspell when:
    /// - The NPC is a spellcaster with available spell slots
    /// - There are enemy spellcasters that pose a threat
    /// - The NPC has Spellcraft ranks (for spell identification)
    /// - The NPC has Dispel Magic or likely-to-be-cast enemy spells prepared
    /// </summary>
    /// <param name="npc">The NPC to evaluate.</param>
    /// <returns>True if the AI decided to ready a counterspell.</returns>
    public bool TryAIReadyCounterspell(CharacterController npc)
    {
        if (npc == null || npc.Stats == null || npc.Stats.IsDead) return false;
        if (!npc.Actions.HasStandardAction) return false;

        // Must be a spellcaster
        var spellComp = npc.Spellcasting;
        if (spellComp == null || !spellComp.CanCastSpells) return false;

        // Need at least some Spellcraft or Dispel Magic to be useful
        int spellcraftBonus = npc.Stats.GetSkillBonus("Spellcraft");
        bool hasDispelMagic = npc.HasDispelMagicAvailable();
        if (spellcraftBonus <= 0 && !hasDispelMagic) return false;

        // Find enemy spellcasters that are alive and nearby
        CharacterController bestTarget = null;
        int bestThreat = 0;

        foreach (var c in _gameManager.GetAllCharactersForAI())
        {
            if (c == null || c.Stats == null || c.Stats.IsDead) continue;
            if (c.Team == npc.Team) continue; // Skip allies

            // Check if they're a spellcaster
            if (!c.Stats.IsSpellcaster) continue;

            var enemySpellComp = c.Spellcasting;
            if (enemySpellComp == null || !enemySpellComp.HasAnyCastablePreparedSpell()) continue;

            int distance = SquareGridUtils.GetDistance(npc.GridPosition, c.GridPosition);
            int casterLevel = Mathf.Max(1, npc.Stats.GetCasterLevel());
            int dispelRange = (100 + 10 * casterLevel) / 5;

            if (distance > dispelRange) continue;

            // Threat based on caster level and proximity
            int threat = c.Stats.GetCasterLevel() * 10 - distance;
            if (threat > bestThreat)
            {
                bestThreat = threat;
                bestTarget = c;
            }
        }

        if (bestTarget == null) return false;

        // AI decision: ready counterspell if there's a significant threat
        // Only do this ~30% of the time to keep AI varied
        if (UnityEngine.Random.Range(0, 100) > 30) return false;

        int currentRound = _gameManager.CurrentRoundNumber;
        bool readied = npc.ReadyCounterspell(bestTarget, currentRound);

        if (readied)
        {
            // AI prefers same-spell counter over Dispel Magic (auto-success vs check)
            // But will use Dispel Magic as fallback
            if (npc.ReadiedCounterspell != null)
                npc.ReadiedCounterspell.PreferDispelMagic = !hasDispelMagic ? false : (spellcraftBonus < 5);

            _gameManager.CombatUI?.ShowCombatLog(
                $"<color=#FFD700>⚡ {npc.Stats.CharacterName} readies a counterspell against {bestTarget.Stats.CharacterName}!</color>");
        }

        return readied;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AI WALL OF ICE INTERACTION
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// If the NPC is adjacent to an intact Wall of Ice, decides whether to attack it
    /// with a weapon (auto-hit, Hardness 0) or attempt a Strength check (DC 15 + CL).
    /// Prefers weapon attack if equipped; falls back to STR check for unarmed NPCs.
    /// If not adjacent to any wall, yields a short delay and returns.
    /// </summary>
    private IEnumerator TryAIWallInteraction(CharacterController npc, CharacterController target)
    {
        if (npc == null || npc.Stats.CurrentHP <= 0 || !npc.Actions.HasStandardAction)
        {
            yield return new WaitForSeconds(0.3f);
            yield break;
        }

        Vector2Int targetPos = target != null ? target.GridPosition : npc.GridPosition;
        Vector2Int? bestWallCell = _gameManager.FindBestAdjacentWallCellForAI(npc, targetPos);

        if (!bestWallCell.HasValue)
        {
            yield return new WaitForSeconds(0.3f);
            yield break;
        }

        WallOfIceAreaEffect wall = WallOfIceAreaEffect.GetWallAtCell(bestWallCell.Value);
        if (wall == null || wall.IsBreached(bestWallCell.Value))
        {
            yield return new WaitForSeconds(0.3f);
            yield break;
        }

        // Decide: weapon attack vs Strength check
        // Weapon attacks auto-hit and deal full damage (Hardness 0), so prefer weapon.
        // STR check is a backup if STR bonus is high enough for reasonable success.
        bool hasWeapon = npc.GetEquippedMainWeapon() != null || npc.Stats.GetPrimaryNaturalAttack() != null;
        int strMod = CharacterStats.GetModifier(npc.Stats.STR);
        int dc = wall.GetStrengthCheckDC();
        bool strCheckLikely = (strMod + 10) >= dc; // Rough: ~50%+ chance on d20

        if (hasWeapon)
        {
            Debug.Log($"[AI] {npc.Stats.CharacterName} attacks Wall of Ice at ({bestWallCell.Value.x},{bestWallCell.Value.y}) with weapon.");
            yield return _gameManager.StartCoroutine(_gameManager.NPCAttackWallForAI(npc, wall, bestWallCell.Value));
        }
        else if (strCheckLikely)
        {
            Debug.Log($"[AI] {npc.Stats.CharacterName} attempts STR check (DC {dc}) on Wall of Ice at ({bestWallCell.Value.x},{bestWallCell.Value.y}).");
            yield return _gameManager.StartCoroutine(_gameManager.NPCBreakWallForAI(npc, wall, bestWallCell.Value));
        }
        else
        {
            Debug.Log($"[AI] {npc.Stats.CharacterName} is adjacent to Wall of Ice but STR check DC {dc} too high (STR mod {strMod}), skipping.");
            yield return new WaitForSeconds(0.3f);
        }
    }
}
