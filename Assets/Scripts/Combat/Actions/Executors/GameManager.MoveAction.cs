using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameManager partial class: the one move executor of both sides (batch 13 step 9, AI-076's preparation). The PC's move
/// and withdraw (GameManager.ExecuteMovement, through the catalog's move and withdraw entries and RunPlayerAction) and the
/// AI's (the *ForAI move wrappers, through RunAIAction) all run <see cref="ExecuteMoveAction"/>. No UI follow-up here: the
/// entries' UiAfter (ShowChoices) and the runner continue the PC's turn; ActionCatalogTests scans this file.
/// </summary>
public partial class GameManager
{
    /// <summary>
    /// Runs one movement request, in order:
    /// <list type="number">
    /// <item>the shared movement legality (<see cref="MovementService.CanStartMovement"/>; a refusal is logged, sets
    /// <see cref="MoveAttempt.Refused"/> and spends nothing);</item>
    /// <item>the path: the request's own (the PC's click), or one found to its destination within the move's budget (none:
    /// nothing moves, and no refusal);</item>
    /// <item>the spend before the walk (the withdraw's own action; a move action unless
    /// <see cref="MoveQuirk.AiSpendsAfterWalk"/>);</item>
    /// <item>the walk: the PC's per-step loop, or the AI's segment walker under <see cref="MoveQuirk.AiSegmentLoop"/>; both
    /// resolve each attack of opportunity before the step that provokes it (PHB p.137);</item>
    /// <item>the after-walk spend under <see cref="MoveQuirk.AiSpendsAfterWalk"/>, also with no path under
    /// <see cref="MoveQuirk.AiSpendsWithoutPath"/>, unless the mover was dropped or the combat ended where the request's
    /// bits say so.</item>
    /// </list>
    /// It never continues the UI and reads no controller flag: the same request walks the same way for any mover. The
    /// attempt record is <see cref="MoveRequest.Attempt"/> (made here when null).
    /// </summary>
    internal IEnumerator ExecuteMoveAction(MoveRequest req)
    {
        if (req == null)
            yield break;
        // The request's own record is the one attempt (made here when the caller gave none); callers read req.Attempt.
        if (req.Attempt == null)
            req.Attempt = new MoveAttempt();
        MoveAttempt attempt = req.Attempt;
        CharacterController mover = req.Mover;
        if (mover == null || mover.Stats == null)
            yield break;

        bool withdraw = req.IsWithdraw;
        string what = withdraw ? "withdraw" : "move";
        bool searchPath = req.Path == null;

        // An AI move to nowhere (no grid, its own square) moves nothing and is not refused, as the AI cores did.
        bool go = !searchPath || (Grid != null && req.Destination.HasValue && req.Destination.Value != mover.GridPosition);
        if (go)
        {
            // 1. The shared movement legality (batch 12 step 22, GRID-019, GRID-022): no ordinary movement after a 5-foot
            // step (PHB p.144), while prone or grappling, or with no action left; a withdraw is a full-round action or a
            // Restricted Withdraw, never blinded (PHB p.143-144).
            MoveKind legalityKind = withdraw ? MovementService.WithdrawKindFor(mover) : MoveKind.Move;
            Legality legal = MovementService.CanStartMovement(mover, legalityKind);
            if (!legal.Ok)
            {
                RecordRefusedMovement(mover, what, legal.Reason, attempt);
                yield break;
            }

            // 2. The path. The budget is read before anything is spent: a Restricted Withdraw spends the single action.
            int budget = withdraw ? GetWithdrawMoveRangeSquares(mover) : GetCurrentMoveRangeSquares(mover);
            List<Vector2Int> path = req.Path;
            List<AoOThreatInfo> provokedAoOs = req.ProvokedAoOs;
            if (searchPath)
            {
                path = null;
                provokedAoOs = null;
                // Prone, entangled in a web or otherwise unable to move (CMB-074): nowhere to go.
                if (budget > 0)
                {
                    // A withdraw's start square provokes from no enemy the mover can see (suppressFirstSquareAoO, the startSpace
                    // rule in ThreatSystem.AnalyzePathForAoOs, CharacterController.CanVisuallyPerceive, PHB p.143, DMG p.29,
                    // GRID-021); the rest provoke as any movement does (PHB p.137, CMB-073).
                    AoOPathResult pathResult = _movementService != null
                        ? (withdraw
                            ? _movementService.FindPath(mover, req.Destination.Value, avoidThreats: false, maxRangeOverride: budget,
                                allowThroughAllies: true, allowThroughEnemies: false, suppressFirstSquareAoO: true)
                            : _movementService.FindPath(mover, req.Destination.Value, avoidThreats: false, maxRangeOverride: budget))
                        : Grid.FindPathAoOAware(mover.GridPosition, req.Destination.Value, null, budget, mover.GetVisualSquaresOccupied(), mover);
                    if (pathResult != null && pathResult.Path != null && pathResult.Path.Count > 0)
                    {
                        path = pathResult.Path;
                        provokedAoOs = pathResult.ProvokedAoOs;
                    }
                    else if (!withdraw)
                    {
                        // No valid path (a Wall of Ice or another obstacle): nothing moves.
                        Debug.Log($"[Movement] ExecuteMoveAction: no valid path to {req.Destination.Value} for {mover.Stats.CharacterName}. Movement blocked.");
                    }
                }
            }

            if (path != null && path.Count > 0)
            {
                // 3. The spend before the walk.
                if (req.Spend == MoveSpend.ExecutorOwned)
                {
                    // The full-round action, or a Restricted Withdraw's standard action (the shared legality, GRID-022).
                    if (!TrySpendWithdrawAction(mover, out string withdrawRefusal))
                    {
                        RecordRefusedMovement(mover, what, withdrawRefusal, attempt,
                            req.PcRefusalText ? $"{mover.Stats.CharacterName} cannot complete Withdraw: {withdrawRefusal}." : null);
                        yield break;
                    }
                }
                else if (req.Spend == MoveSpend.MoveAction && !req.Has(MoveQuirk.AiSpendsAfterWalk))
                {
                    if (!SpendMoveForMovement(mover, req.AllowConvertStandard))
                    {
                        RecordRefusedMovement(mover, what, "no action available for movement", attempt,
                            req.PcRefusalText ? $"{mover.Stats.CharacterName} has no action available for movement." : null);
                        yield break;
                    }
                }

                attempt.Started = true;
                if (withdraw)
                    mover.IsWithdrawing = true;

                // 4. The walk.
                if (req.Has(MoveQuirk.AiSegmentLoop))
                {
                    var moveOutcome = new MovementAoOStepOutcome();
                    yield return StartCoroutine(ExecutePathWithMovementAoOs(mover, path, provokedAoOs, req.SecondsPerStep, req.Label, moveOutcome,
                        withdraw ? MovementService.WithdrawKindFor(mover) : MoveKind.Move));
                    attempt.Incapacitated = moveOutcome.Incapacitated;

                    CheckTurnUndeadProximityBreakingForMover(mover);
                    PruneTurnUndeadTrackers();
                }
                else
                {
                    IEnumerator walk = ExecuteMovement(req, path, provokedAoOs, budget, attempt);
                    while (walk.MoveNext())
                        yield return walk.Current;
                }
            }
        }

        // 5. The after-walk spend (AI-076's quirk until step 10).
        if (req.Spend == MoveSpend.MoveAction && req.Has(MoveQuirk.AiSpendsAfterWalk) && ShouldSpendAfterWalk(req, attempt))
            SpendMoveForMovement(mover, req.AllowConvertStandard);
    }

    /// <summary>Whether the after-walk spend of an AI move is made (the quirk bits of <paramref name="req"/>; never after a refusal).</summary>
    internal bool ShouldSpendAfterWalk(MoveRequest req, MoveAttempt attempt)
    {
        if (req == null || attempt == null || attempt.Refused)
            return false;
        if (!attempt.Started && !req.Has(MoveQuirk.AiSpendsWithoutPath))
            return false;
        if (req.Has(MoveQuirk.AiNoSpendIfCombatOver) && CurrentPhase == TurnPhase.CombatOver)
            return false;
        if (req.Has(MoveQuirk.AiNoSpendIfDropped) && req.Mover != null && !req.Mover.CanTakeTurnActions())
            return false;
        return true;
    }

    /// <summary>Spends one move action, or converts the standard action when <paramref name="allowConvertStandard"/>
    /// (PHB p.141). False when neither is left (nothing spent).</summary>
    private static bool SpendMoveForMovement(CharacterController mover, bool allowConvertStandard)
    {
        if (mover == null || mover.Actions == null)
            return false;
        if (mover.Actions.HasMoveAction)
        {
            mover.Actions.UseMoveAction();
            return true;
        }
        if (allowConvertStandard && mover.Actions.CanConvertStandardToMove)
        {
            mover.Actions.ConvertStandardToMove();
            return true;
        }
        return false;
    }

    /// <summary>
    /// The per-step walk (the PC's): each step's cost against the budget (diagonals 1-2-1, difficult terrain;
    /// SquareGridUtils.StepCost, GRID-013), the step refusals, the attacks of opportunity before the step (PHB p.137,
    /// CMB-005), the step, and the drop checks; then the walk's log lines and, for a dropped mover, the combat-end check
    /// (<see cref="MoveRequest.CombatEndContext"/>). Its yields are its caller's (ExecuteMoveAction inlines it). It is named
    /// ExecuteMovement, as the PC's move was before step 9, so the scenario trace's caller frames of a combat ended by a PC
    /// dropped on its own move stay [..., EvaluateCombatEnd, ExecuteMovement] (ScenarioTrace.CallerFrames).
    /// </summary>
    private IEnumerator ExecuteMovement(MoveRequest req, List<Vector2Int> path, List<AoOThreatInfo> provokedAoOs, int movementBudgetSquares, MoveAttempt attempt)
    {
        CharacterController pc = req.Mover;
        bool isWithdraw = req.IsWithdraw;
        bool interruptedByIncapacitation = false;
        bool stoppedOnTheWay = false;
        string stoppedByAoOReason = null;
        int movementCostConsumed = 0;
        int diagonalCount = 0;
        Vector2Int previousCell = pc.GridPosition;
        var aooOutcome = new MovementAoOStepOutcome();

        for (int pathIndex = 0; pathIndex < path.Count; pathIndex++)
        {
            Vector2Int step = path[pathIndex];
            // The one step cost (diagonals 1-2-1, difficult terrain such as Grease 2 or 3; SquareGridUtils.StepCost, GRID-013),
            // the cost the path search trimmed the path by. The diagonal count is committed only once the step is taken.
            int nextDiagonalCount = diagonalCount;
            int stepCost = SquareGridUtils.StepCost(previousCell, step, ref nextDiagonalCount, SquareGridUtils.IsDifficultTerrainForMovement(pc, step));
            if (movementCostConsumed + stepCost > movementBudgetSquares)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("🛢", $"{pc.Stats.CharacterName} cannot move farther this action (difficult terrain slows movement)."));
                break;
            }

            // A step the mover cannot take (a barrier, a hold, Grease's failed Balance check, PHB p.237) provokes nothing:
            // it never leaves its square (PHB p.137).
            MoveKind stepKind = isWithdraw ? MovementService.WithdrawKindFor(pc) : MoveKind.Move;
            if (MovementService.IsStepRefused(pc, pc.GridPosition, step, stepKind))
            {
                stoppedOnTheWay = true;
                break;
            }

            // AoOs for leaving the current square resolve before the step (PHB p.137, CMB-005).
            yield return StartCoroutine(ResolveMovementAoOsBeforeStep(pc, provokedAoOs, pathIndex, req.Label, 1.0f, aooOutcome));
            if (aooOutcome.StopMovement)
            {
                if (aooOutcome.Incapacitated)
                    interruptedByIncapacitation = true;
                else
                    stoppedByAoOReason = aooOutcome.Reason;
                break;
            }

            var stepPath = new List<Vector2Int> { step };

            // Only the path's last square is the destination: an ally's square the path passes through is
            // a legal intermediate step (PHB p.148), as in ExecutePathWithMovementAoOs for the segment walker.
            bool stepIsDestination = pathIndex == path.Count - 1;
            if (_movementService != null)
                yield return StartCoroutine(_movementService.ExecuteMovement(pc, stepPath, req.SecondsPerStep, markAsMoved: false, lastStepIsDestination: stepIsDestination, kind: stepKind));
            else
                yield return StartCoroutine(pc.MoveAlongPath(stepPath, req.SecondsPerStep, markAsMoved: false, lastStepIsDestination: stepIsDestination, kind: stepKind));

            // Blocked (MoveAlongPath refused the square: a barrier, an occupant, a movement step rule such as Grease's
            // Balance check, PHB p.237, D12): stop here rather than jump to the next square.
            if (pc.GridPosition != step)
            {
                if (!interruptedByIncapacitation && pc.Stats != null && ThreatSystem.IsMoverIncapacitated(pc))
                    interruptedByIncapacitation = true;
                else
                    stoppedOnTheWay = true;
                break;
            }

            movementCostConsumed += stepCost;
            diagonalCount = nextDiagonalCount;

            // Area effects (Wall of Fire, etc.) may deal damage when a creature enters their cells (their Update loop).
            // MoveAlongPath already stops on a drop; the flag here ends the walk. A disabled mover (0 hp) keeps moving
            // (PHB p.145, batch 12 D5); dead, dying or unconscious stops.
            if (!interruptedByIncapacitation && pc.Stats != null && ThreatSystem.IsMoverIncapacitated(pc))
            {
                Debug.Log($"🔥 [PCMovement] {pc.Stats.CharacterName} dropped by area damage during movement step {pathIndex + 1} (HP={pc.Stats.CurrentHP})");
                interruptedByIncapacitation = true;
                break;
            }

            previousCell = step;
        }

        if (movementCostConsumed > 0)
            pc.HasMovedThisTurn = true;

        CheckTurnUndeadProximityBreakingForMover(pc);
        PruneTurnUndeadTrackers();
        UpdateAllStatsUI();

        if (isWithdraw && !interruptedByIncapacitation)
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("↩", $"{pc.Stats.CharacterName} completes Withdraw."));

        InvalidatePreviewThreats();

        if (interruptedByIncapacitation)
        {
            attempt.Incapacitated = true;
            CombatUI?.ShowCombatLog(CombatLogHelper.CriticalFailure("⛔", $"{pc.Stats.CharacterName}'s movement stops immediately due to incapacitation."));
            if (!string.IsNullOrEmpty(req.CombatEndContext))
                EvaluateCombatEnd(req.CombatEndContext);
            yield break;
        }

        if (stoppedOnTheWay)
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⛔", $"{pc.Stats.CharacterName}'s movement stops short of the destination."));

        if (!string.IsNullOrEmpty(stoppedByAoOReason))
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⛔", $"{pc.Stats.CharacterName}'s movement stops ({stoppedByAoOReason}) after an attack of opportunity."));
    }

    /// <summary>
    /// A refused movement (the shared legality, or the withdraw's spend): recorded on <paramref name="attempt"/> and logged
    /// (Debug.Log and the combat log: "X cannot move: reason.", or <paramref name="combatLogLine"/>: the PC's own wording
    /// of a spend refusal before step 9, <see cref="MoveRequest.PcRefusalText"/>). Nothing was spent.
    /// </summary>
    private void RecordRefusedMovement(CharacterController mover, string what, string reason, MoveAttempt attempt, string combatLogLine = null)
    {
        if (attempt != null)
        {
            attempt.Refused = true;
            attempt.Reason = reason ?? string.Empty;
        }

        string name = mover != null && mover.Stats != null ? mover.Stats.CharacterName : "?";
        Debug.Log($"[Movement] {name} cannot {what}: {reason}");
        CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", combatLogLine ?? $"{name} cannot {what}: {reason}."));
    }
}
