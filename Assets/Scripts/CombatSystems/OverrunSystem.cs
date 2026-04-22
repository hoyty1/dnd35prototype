using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OverrunSystem : BaseCombatManeuver
{
}

public partial class GameManager
{
    private bool _isSelectingOverrunDestination;
    private CharacterController _overrunAttacker;
    private Vector2Int _overrunDestination;

    // Legacy continuation fields (unused in destination-based overrun flow, retained for backward compatibility).
    private bool _isOverrunContinuationSelection;
    private CharacterController _overrunContinuationAttacker;
    private readonly Dictionary<Vector2Int, List<Vector2Int>> _overrunContinuationPathsByDestination = new Dictionary<Vector2Int, List<Vector2Int>>();

    public bool CanUseOverrun(CharacterController actor, out string reason)
    {
        reason = "Unavailable";
        if (actor == null || actor.Stats == null)
            return false;

        if (!actor.Actions.HasMoveAction)
        {
            reason = "No move action";
            return false;
        }

        if (!actor.Actions.HasStandardAction)
        {
            reason = "No standard action";
            return false;
        }

        if (actor.HasTakenFiveFootStep)
        {
            reason = "After 5-ft step";
            return false;
        }

        if (actor.HasCondition(CombatConditionType.Prone))
        {
            reason = "Prone";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private void ClearOverrunDestinationSelectionState()
    {
        _isSelectingOverrunDestination = false;
        _overrunAttacker = null;
        _overrunDestination = Vector2Int.zero;
    }

    private void StartOverrunDestinationSelection(CharacterController attacker)
    {
        if (attacker == null || attacker.Stats == null || Grid == null)
        {
            ShowActionChoices();
            return;
        }

        Debug.Log($"[Overrun][Flow] Entering destination selection for {attacker.Stats.CharacterName}.");

        ClearOverrunContinuationState();
        _isSelectingOverrunDestination = true;
        _overrunAttacker = attacker;
        CurrentSubPhase = PlayerSubPhase.Moving;

        ShowOverrunDestinationOptions(attacker);
        CombatUI?.SetActionButtonsVisible(false);
        CombatUI?.SetTurnIndicator($"{attacker.Stats.CharacterName} - Select destination for overrun");
        CombatUI?.ShowCombatLog("Select destination for overrun.");
    }

    private void ShowOverrunDestinationOptions(CharacterController attacker)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        if (attacker == null || attacker.Stats == null)
            return;

        int movementSpeed = Mathf.Max(0, attacker.Stats.MoveRange);
        List<SquareCell> candidateCells = Grid.GetCellsInRange(attacker.GridPosition, movementSpeed);

        for (int i = 0; i < candidateCells.Count; i++)
        {
            SquareCell cell = candidateCells[i];
            if (cell == null)
                continue;

            if (cell.Coords == attacker.GridPosition)
                continue;

            AoOPathResult pathResult = FindPath(
                attacker,
                cell.Coords,
                avoidThreats: false,
                maxRangeOverride: movementSpeed,
                allowThroughAllies: true,
                allowThroughEnemies: true);

            if (pathResult == null || pathResult.Path == null || pathResult.Path.Count == 0)
                continue;

            if (pathResult.Path[pathResult.Path.Count - 1] != cell.Coords)
                continue;

            cell.SetHighlight(HighlightType.Move);
            _highlightedCells.Add(cell);
        }

        HighlightCharacterFootprint(attacker, HighlightType.Selected);

        if (_highlightedCells.Count <= 0)
        {
            CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} has no reachable overrun destination.");
            ShowActionChoices();
        }
    }

    private void HandleOverrunDestinationClick(CharacterController pc, SquareCell cell)
    {
        if (!_isSelectingOverrunDestination || pc == null || cell == null)
            return;

        if (_overrunAttacker != pc)
        {
            ShowActionChoices();
            return;
        }

        if (cell.Coords == pc.GridPosition)
        {
            CancelMovementSelection();
            return;
        }

        if (!_highlightedCells.Contains(cell))
            return;

        _overrunDestination = cell.Coords;
        AnalyzeOverrunPath(pc, _overrunDestination);
    }

    private void AnalyzeOverrunPath(CharacterController attacker, Vector2Int destination)
    {
        if (attacker == null || attacker.Stats == null || Grid == null)
        {
            ShowActionChoices();
            return;
        }

        int movementSpeed = Mathf.Max(0, attacker.Stats.MoveRange);

        AoOPathResult overrunPathResult = FindPath(
            attacker,
            destination,
            avoidThreats: false,
            maxRangeOverride: movementSpeed,
            allowThroughAllies: true,
            allowThroughEnemies: true);

        List<Vector2Int> overrunPath = overrunPathResult != null && overrunPathResult.Path != null
            ? new List<Vector2Int>(overrunPathResult.Path)
            : new List<Vector2Int>();

        if (overrunPath.Count == 0 || overrunPath[overrunPath.Count - 1] != destination)
        {
            CombatUI?.ShowCombatLog("⚠ No valid overrun path to that destination.");
            ShowOverrunDestinationOptions(attacker);
            return;
        }

        AoOPathResult normalPathResult = FindPath(
            attacker,
            destination,
            avoidThreats: false,
            maxRangeOverride: movementSpeed,
            allowThroughAllies: true,
            allowThroughEnemies: false);

        List<Vector2Int> normalPath = normalPathResult != null && normalPathResult.Path != null
            ? new List<Vector2Int>(normalPathResult.Path)
            : new List<Vector2Int>();

        bool canAvoidEnemies = normalPath.Count > 0 && normalPath[normalPath.Count - 1] == destination;
        if (canAvoidEnemies)
        {
            CombatUI?.ShowConfirmationDialog(
                "OVERRUN CHOICE",
                "You can reach this destination without overrunning. Still overrun?",
                "Overrun",
                "Normal Move",
                onConfirm: () => ExecuteOverrunMovementPath(attacker, overrunPath),
                onCancel: () => ExecuteNormalMovementPath(attacker, normalPath));
            return;
        }

        ExecuteOverrunMovementPath(attacker, overrunPath);
    }

    private void ExecuteNormalMovementPath(CharacterController attacker, List<Vector2Int> path)
    {
        ClearOverrunDestinationSelectionState();
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        if (path == null || path.Count == 0)
        {
            ShowActionChoices();
            return;
        }

        StartCoroutine(ExecuteMovement(attacker, path));
    }

    private void ExecuteOverrunMovementPath(CharacterController attacker, List<Vector2Int> path)
    {
        if (attacker == null || attacker.Stats == null || path == null || path.Count == 0)
        {
            ShowActionChoices();
            return;
        }

        if (!attacker.Actions.HasMoveAction)
        {
            CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} no longer has a move action for overrun.");
            ShowActionChoices();
            return;
        }

        ClearOverrunDestinationSelectionState();
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CurrentSubPhase = PlayerSubPhase.Animating;

        attacker.Actions.UseMoveAction();
        attacker.HasMovedThisTurn = true;

        bool standardActionConsumed = false;
        Vector2Int lastSafePosition = attacker.GridPosition;

        void ProcessStep(int stepIndex)
        {
            if (stepIndex >= path.Count)
            {
                CombatUI?.ShowCombatLog($"{attacker.Stats.CharacterName} completes the overrun!");
                UpdateAllStatsUI();
                ShowActionChoices();
                return;
            }

            Vector2Int stepPosition = path[stepIndex];
            CharacterController enemy = GetEnemyAtPositionForOverrun(attacker, stepPosition);
            if (enemy != null)
            {
                if (ThreatSystem.CanMakeAoO(enemy) && !enemy.Stats.IsDead)
                {
                    CombatUI?.ShowCombatLog($"{enemy.Stats.CharacterName} gets an attack of opportunity!");
                    CombatResult aooResult = TriggerAoO(enemy, attacker);
                    if (aooResult != null)
                    {
                        CombatUI?.ShowCombatLog($"⚔ Overrun AoO: {aooResult.GetDetailedSummary()}");
                        UpdateAllStatsUI();
                    }

                    if (attacker.Stats.IsDead)
                    {
                        CombatUI?.ShowCombatLog($"{attacker.Stats.CharacterName} is dropped before completing the overrun.");
                        ShowActionChoices();
                        return;
                    }
                }

                ResolveEnemyOverrunResponse(enemy, enemyBlocks =>
                {
                    if (!enemyBlocks)
                    {
                        CombatUI?.ShowCombatLog($"{enemy.Stats.CharacterName} avoids the overrun!");
                        ProcessStep(stepIndex + 1);
                        return;
                    }

                    CombatUI?.ShowCombatLog($"{enemy.Stats.CharacterName} attempts to block!");
                    if (!standardActionConsumed)
                    {
                        if (!attacker.CommitStandardAction())
                        {
                            CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} cannot spend a standard action to resolve overrun block.");
                            ShowActionChoices();
                            return;
                        }
                        standardActionConsumed = true;
                    }

                    bool attackerWon = ResolveOverrunOpposedCheck(attacker, enemy);
                    if (!attackerWon)
                    {
                        CombatUI?.ShowCombatLog($"{enemy.Stats.CharacterName} stops the overrun!");
                        SquareCell lastSafeCell = Grid.GetCell(lastSafePosition);
                        if (lastSafeCell != null)
                            attacker.MoveToCell(lastSafeCell, markAsMoved: true);

                        UpdateAllStatsUI();
                        ShowActionChoices();
                        return;
                    }

                    CombatUI?.ShowCombatLog($"{attacker.Stats.CharacterName} pushes through!");
                    ProcessStep(stepIndex + 1);
                });

                return;
            }

            SquareCell destinationCell = Grid.GetCell(stepPosition);
            if (destinationCell == null)
            {
                ShowActionChoices();
                return;
            }

            // For occupied intermediate squares (ally overlap), continue logical traversal.
            bool occupiedByOther = false;
            if (destinationCell.IsOccupied)
            {
                IReadOnlyList<CharacterController> occupants = destinationCell.Occupants;
                for (int occIndex = 0; occIndex < occupants.Count; occIndex++)
                {
                    CharacterController occupant = occupants[occIndex];
                    if (occupant == null || occupant == attacker)
                        continue;

                    occupiedByOther = true;
                    break;
                }
            }

            if (!occupiedByOther)
            {
                attacker.MoveToCell(destinationCell, markAsMoved: true);
                lastSafePosition = attacker.GridPosition;
            }

            ProcessStep(stepIndex + 1);
        }

        ProcessStep(0);
    }

    private CharacterController GetEnemyAtPositionForOverrun(CharacterController attacker, Vector2Int position)
    {
        if (attacker == null || Grid == null)
            return null;

        SquareCell cell = Grid.GetCell(position);
        if (cell == null || !cell.IsOccupied)
            return null;

        IReadOnlyList<CharacterController> occupants = cell.Occupants;
        for (int i = 0; i < occupants.Count; i++)
        {
            CharacterController occupant = occupants[i];
            if (occupant == null || occupant == attacker || occupant.Stats == null || occupant.Stats.IsDead)
                continue;

            if (IsEnemyTeam(attacker, occupant))
                return occupant;
        }

        return null;
    }

    private void ResolveEnemyOverrunResponse(CharacterController enemy, Action<bool> onResolved)
    {
        if (enemy == null)
        {
            onResolved?.Invoke(true);
            return;
        }

        if (!enemy.IsPlayerControlled)
        {
            onResolved?.Invoke(true);
            return;
        }

        CombatUI?.ShowConfirmationDialog(
            $"OVERRUN RESPONSE - {enemy.Stats.CharacterName}",
            $"{enemy.Stats.CharacterName}: choose response.",
            "Block",
            "Avoid",
            onConfirm: () => onResolved?.Invoke(true),
            onCancel: () => onResolved?.Invoke(false));
    }

    private bool ResolveOverrunOpposedCheck(CharacterController attacker, CharacterController defender)
    {
        int attackerRoll = UnityEngine.Random.Range(1, 21);
        int attackerTotal = attackerRoll + attacker.Stats.STRMod + GetOverrunSizeModifier(attacker.Stats.CurrentSizeCategory);

        int defenderRoll = UnityEngine.Random.Range(1, 21);
        int defenderAbility = Mathf.Max(defender.Stats.STRMod, defender.Stats.DEXMod);
        int defenderTotal = defenderRoll + defenderAbility + GetOverrunSizeModifier(defender.Stats.CurrentSizeCategory);

        CombatUI?.ShowCombatLog($"Overrun: {attacker.Stats.CharacterName} ({attackerTotal}) vs {defender.Stats.CharacterName} ({defenderTotal})");
        return attackerTotal > defenderTotal;
    }

    private int GetOverrunSizeModifier(SizeCategory size)
    {
        switch (size)
        {
            case SizeCategory.Fine: return -16;
            case SizeCategory.Diminutive: return -12;
            case SizeCategory.Tiny: return -8;
            case SizeCategory.Small: return -4;
            case SizeCategory.Medium: return 0;
            case SizeCategory.Large: return 4;
            case SizeCategory.Huge: return 8;
            case SizeCategory.Gargantuan: return 12;
            case SizeCategory.Colossal: return 16;
            default: return 0;
        }
    }

    private void ClearOverrunContinuationState()
    {
        _isOverrunContinuationSelection = false;
        _overrunContinuationAttacker = null;
        _overrunContinuationPathsByDestination.Clear();
    }

    private bool ShowOverrunContinuationPromptIfAvailable(CharacterController attacker, Vector2Int direction, int remainingMovement)
    {
        if (attacker == null || attacker.Stats == null || CombatUI == null)
            return false;

        if (direction == Vector2Int.zero || remainingMovement <= 0)
            return false;

        Dictionary<Vector2Int, List<Vector2Int>> destinationPaths = BuildOverrunContinuationDestinationPaths(attacker, direction, remainingMovement);
        if (destinationPaths.Count == 0)
            return false;

        CombatUI.ShowPickUpItemSelection(
            actorName: attacker.Stats.CharacterName,
            itemOptions: new List<string> { "Yes", "No" },
            onSelect: selectedIndex =>
            {
                bool continueMoving = selectedIndex == 0;
                if (!continueMoving)
                {
                    CombatUI.ShowCombatLog($"↔ {attacker.Stats.CharacterName} holds position after overrun.");
                    StartCoroutine(AfterAttackDelay(attacker, 1.0f));
                    return;
                }

                BeginOverrunContinuationSelection(attacker, direction, remainingMovement, destinationPaths);
            },
            onCancel: () => StartCoroutine(AfterAttackDelay(attacker, 1.0f)),
            titleOverride: "OVERRUN CONTINUATION",
            bodyOverride: "Defender avoided. Continue moving?",
            optionButtonColorOverride: new Color(0.42f, 0.27f, 0.16f, 1f));

        return true;
    }

    private void BeginOverrunContinuationSelection(
        CharacterController attacker,
        Vector2Int direction,
        int remainingMovement,
        Dictionary<Vector2Int, List<Vector2Int>> destinationPaths)
    {
        if (attacker == null || attacker.Stats == null)
        {
            ShowActionChoices();
            return;
        }

        _isOverrunContinuationSelection = true;
        _overrunContinuationAttacker = attacker;
        _overrunContinuationPathsByDestination.Clear();

        foreach (KeyValuePair<Vector2Int, List<Vector2Int>> kvp in destinationPaths)
            _overrunContinuationPathsByDestination[kvp.Key] = new List<Vector2Int>(kvp.Value);

        CurrentSubPhase = PlayerSubPhase.Moving;
        ShowOverrunContinuationOptions(attacker);
        CombatUI.SetActionButtonsVisible(false);
        CombatUI.SetTurnIndicator($"{attacker.Stats.CharacterName} - Overrun continuation: choose a square along the path");
        CombatUI.ShowCombatLog($"↪ Overrun continuation: choose how far to move straight ahead ({remainingMovement} square(s) remaining).");
    }

    private void ShowOverrunContinuationOptions(CharacterController attacker)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        if (attacker == null)
            return;

        foreach (Vector2Int destination in _overrunContinuationPathsByDestination.Keys)
        {
            SquareCell cell = Grid.GetCell(destination);
            if (cell == null)
                continue;

            cell.SetHighlight(HighlightType.Move);
            _highlightedCells.Add(cell);
        }

        HighlightCharacterFootprint(attacker, HighlightType.Selected);
    }

    private Dictionary<Vector2Int, List<Vector2Int>> BuildOverrunContinuationDestinationPaths(
        CharacterController attacker,
        Vector2Int direction,
        int remainingMovement)
    {
        var destinations = new Dictionary<Vector2Int, List<Vector2Int>>();

        if (attacker == null || remainingMovement <= 0 || direction == Vector2Int.zero)
            return destinations;

        int moverSizeSquares = attacker.GetVisualSquaresOccupied();
        Vector2Int current = attacker.GridPosition;
        var traversalPath = new List<Vector2Int>();

        for (int stepIndex = 0; stepIndex < remainingMovement + 8; stepIndex++)
        {
            Vector2Int next = current + direction;
            SquareCell nextCell = Grid.GetCell(next);
            if (nextCell == null)
                break;

            if (!Grid.CanTraversePathNode(next, moverSizeSquares, attacker, isDestinationNode: false))
                break;

            traversalPath.Add(next);
            int moveCost = SquareGridUtils.CalculatePathCost(attacker.GridPosition, traversalPath);
            if (moveCost > remainingMovement)
                break;

            if (Grid.CanTraversePathNode(next, moverSizeSquares, attacker, isDestinationNode: true))
                destinations[next] = new List<Vector2Int>(traversalPath);

            current = next;
        }

        return destinations;
    }

    private void HandleOverrunContinuationMovementClick(CharacterController pc, SquareCell cell)
    {
        if (!_isOverrunContinuationSelection || pc == null || cell == null)
            return;

        if (_overrunContinuationAttacker != pc)
        {
            ClearOverrunContinuationState();
            ShowActionChoices();
            return;
        }

        if (cell.Coords == pc.GridPosition)
        {
            ClearOverrunContinuationState();
            ShowActionChoices();
            return;
        }

        if (!_overrunContinuationPathsByDestination.TryGetValue(cell.Coords, out List<Vector2Int> selectedPath)
            || selectedPath == null
            || selectedPath.Count == 0)
        {
            return;
        }

        AoOPathResult pathResult = new AoOPathResult
        {
            Path = new List<Vector2Int>(selectedPath),
            ProvokedAoOs = CheckForAoO(pc, selectedPath)
        };

        if (!pathResult.ProvokesAoOs)
        {
            ClearOverrunContinuationState();
            StartCoroutine(ExecuteMovement(pc, pathResult.Path));
            return;
        }

        var uniqueThreateners = new List<CharacterController>();
        var seen = new HashSet<CharacterController>();
        foreach (AoOThreatInfo aooInfo in pathResult.ProvokedAoOs)
        {
            CharacterController threatener = aooInfo != null ? aooInfo.Threatener : null;
            if (threatener == null || !seen.Add(threatener))
                continue;
            uniqueThreateners.Add(threatener);
        }

        ShowAoOActionConfirmation(new AoOProvokingActionInfo
        {
            ActionType = AoOProvokingAction.Movement,
            ActionName = "MOVE",
            ActionDescription = $"Continue overrun movement to ({cell.Coords.x},{cell.Coords.y})",
            Actor = pc,
            ThreateningEnemies = uniqueThreateners,
            OnProceed = () =>
            {
                ClearOverrunContinuationState();
                StartCoroutine(ResolveAoOsAndMove(pc, pathResult));
            },
            OnCancel = () =>
            {
                CurrentSubPhase = PlayerSubPhase.Moving;
                ShowOverrunContinuationOptions(pc);
                CombatUI.SetActionButtonsVisible(false);
                CombatUI.SetTurnIndicator($"{pc.Stats.CharacterName} - Overrun continuation: choose a square along the path");
            }
        });
    }

    private bool IsOverrunTargetSizeLegal(CharacterController attacker, CharacterController target, out string reason)
    {
        reason = string.Empty;
        if (attacker == null || target == null || attacker.Stats == null || target.Stats == null)
        {
            reason = "Invalid";
            return false;
        }

        int sizeDelta = (int)target.GetCurrentSizeCategory() - (int)attacker.GetCurrentSizeCategory();
        if (sizeDelta > 1)
        {
            reason = "Target too large";
            return false;
        }

        return true;
    }

    private bool CanAttackerOccupyDefenderSquareIfVacated(CharacterController attacker, CharacterController target)
    {
        if (attacker == null || target == null || Grid == null)
            return false;

        Vector2Int destination = target.GridPosition;
        int attackerSizeSquares = attacker.GetVisualSquaresOccupied();

        // Treat target's occupied footprint as temporarily vacated for legality checks.
        return Grid.CanPlaceCreature(
            destination,
            attackerSizeSquares,
            ignoreOccupant: attacker,
            ignoreOtherOccupants: false,
            additionalIgnoredOccupants: new List<CharacterController> { target });
    }

    private bool IsValidOverrunTarget(CharacterController attacker, CharacterController target, out string reason, bool requireAdjacency)
    {
        reason = string.Empty;
        if (attacker == null || target == null || attacker.Stats == null || target.Stats == null)
        {
            reason = "Invalid";
            return false;
        }

        if (attacker == target || target.Stats.IsDead)
        {
            reason = "Invalid target";
            return false;
        }

        if (!IsEnemyTeam(attacker, target))
        {
            reason = "Not enemy";
            return false;
        }

        if (!IsOverrunTargetSizeLegal(attacker, target, out reason))
            return false;

        if (requireAdjacency)
        {
            int distance = attacker.GetMinimumDistanceToTarget(target, chebyshev: true);
            if (distance != 1)
            {
                reason = "Not adjacent";
                return false;
            }
        }

        if (!CanAttackerOccupyDefenderSquareIfVacated(attacker, target))
        {
            reason = "No room to pass";
            return false;
        }

        return true;
    }

    private void HandleOverrunTargetClick(CharacterController attacker, CharacterController target)
    {
        Debug.LogWarning("[Overrun][LegacyGuard] HandleOverrunTargetClick invoked. Target-based overrun is deprecated; redirecting to destination selection.");

        if (attacker == null || attacker.Stats == null)
        {
            ShowActionChoices();
            return;
        }

        StartOverrunDestinationSelection(attacker);
    }

    private bool ShouldShowOverrunResponsePrompt(CharacterController target)
    {
        bool shouldPrompt = target != null && target.IsPlayerControlled;
        Debug.Log($"[Overrun] Target response mode: {(shouldPrompt ? "PlayerPrompt" : "NPC-AutoBlock")}");
        return shouldPrompt;
    }

    private void ShowOverrunResponsePrompt(CharacterController attacker, CharacterController target)
    {
        CombatUI.ShowPickUpItemSelection(
            actorName: target.Stats.CharacterName,
            itemOptions: new List<string>
            {
                "Avoid — Yield space and let the attacker pass.",
                "Block — Oppose with a Strength check."
            },
            onSelect: selectedIndex =>
            {
                bool defenderBlocks = selectedIndex == 1;
                Debug.Log($"[Overrun] Player selected {(defenderBlocks ? "Block" : "Avoid")} for {target.Stats.CharacterName}.");
                ResolveOverrunSpecialAttack(attacker, target, defenderBlocks);
            },
            onCancel: () =>
            {
                Debug.Log($"[Overrun] Response prompt cancelled for {target.Stats.CharacterName}; returning to selection UI.");
                if (CurrentPhase == TurnPhase.PCTurn && ActivePC == attacker && attacker.Actions.HasStandardAction)
                    ShowSpecialAttackTargets(attacker, SpecialAttackType.Overrun);
                else
                    ShowActionChoices();
            },
            titleOverride: $"OVERRUN — {target.Stats.CharacterName} RESPONSE",
            bodyOverride: $"{target.Stats.CharacterName}: avoid or block {attacker.Stats.CharacterName}'s overrun?",
            optionButtonColorOverride: new Color(0.42f, 0.27f, 0.16f, 1f));
    }

    private void ResolveOverrunSpecialAttack(CharacterController attacker, CharacterController target, bool defenderBlocks)
    {
        if (attacker == null || target == null || attacker.Stats == null || target.Stats == null)
        {
            ShowActionChoices();
            return;
        }

        if (!IsValidOverrunTarget(attacker, target, out string invalidReason, requireAdjacency: true))
        {
            CombatUI?.ShowCombatLog($"⚠ Overrun aborted: {invalidReason}.");
            ShowActionChoices();
            return;
        }

        CurrentSubPhase = PlayerSubPhase.Animating;

        bool attackerHasImprovedOverrun = attacker.Stats.HasFeat("Improved Overrun");
        bool provokedAoO = false;

        if (!attackerHasImprovedOverrun && ThreatSystem.CanMakeAoO(target) && !target.Stats.IsDead)
        {
            provokedAoO = true;
            CombatResult aooResult = TriggerAoO(target, attacker);
            if (aooResult != null)
            {
                CombatUI.ShowCombatLog($"⚔ Overrun AoO: {aooResult.GetDetailedSummary()}");
                UpdateAllStatsUI();

                if (aooResult.Hit && aooResult.TotalDamage > 0)
                    CheckConcentrationOnDamage(attacker, aooResult.TotalDamage);
            }
        }

        if (attacker.Stats.IsDead)
        {
            CombatUI.ShowCombatLog($"{attacker.Stats.CharacterName} is dropped before completing the overrun.");
            Grid.ClearAllHighlights();
            _highlightedCells.Clear();
            _isSelectingSpecialAttack = false;
            UpdateAllStatsUI();
            ShowActionChoices();
            return;
        }

        bool resolvedAsBlock = defenderBlocks;
        Vector2Int overrunDirection = Vector2Int.zero;
        int movementSpentByOverrun = 0;

        if (!defenderBlocks)
        {
            if (!TryResolveOverrunAvoidMovement(attacker, target, out overrunDirection, out movementSpentByOverrun))
            {
                CombatUI.ShowCombatLog($"⚠ {target.Stats.CharacterName} has no room to avoid and must block the overrun.");
                resolvedAsBlock = true;
            }
        }

        SpecialAttackResult result = attacker.ResolveOverrunAttempt(target, resolvedAsBlock, provokedAoO);
        CombatUI.ShowCombatLog($"⚔ SPECIAL [Overrun]: {result.Log}");

        if (result.Success && !result.DefenderAvoided)
            TryPushTargetAway(attacker, target, 1, allowAttackerFollow: true);

        if (result.AttackerActionConsumed)
            attacker.CommitStandardAction();

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        _isSelectingSpecialAttack = false;

        UpdateAllStatsUI();

        if (target.Stats.IsDead && !target.IsPlayerControlled && AreAllNPCsDead())
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI.SetTurnIndicator("VICTORY! All enemies defeated!");
            CombatUI.SetActionButtonsVisible(false);
            return;
        }

        if (result.DefenderAvoided
            && !result.AttackerActionConsumed
            && CurrentPhase == TurnPhase.PCTurn
            && ActivePC == attacker
            && attacker.IsPlayerControlled
            && attacker.Actions.HasMoveAction)
        {
            int remainingMovement = Mathf.Max(0, attacker.Stats.MoveRange - movementSpentByOverrun);
            if (ShowOverrunContinuationPromptIfAvailable(attacker, overrunDirection, remainingMovement))
                return;
        }

        StartCoroutine(AfterAttackDelay(attacker, 1.0f));
    }

    private bool TryResolveOverrunAvoidMovement(CharacterController attacker, CharacterController target, out Vector2Int overrunDirection, out int movementCost)
    {
        overrunDirection = Vector2Int.zero;
        movementCost = 0;

        if (attacker == null || target == null)
            return false;

        Vector2Int attackerStart = attacker.GridPosition;
        Vector2Int originalTargetPos = target.GridPosition;
        SquareCell sidestepCell = FindOverrunSidestepCell(target, attackerStart);
        if (sidestepCell == null)
            return false;

        target.MoveToCell(sidestepCell);

        SquareCell attackerDestination = Grid.GetCell(originalTargetPos);
        if (attackerDestination == null || attackerDestination.IsOccupied ||
            !Grid.CanPlaceCreature(originalTargetPos, attacker.GetVisualSquaresOccupied(), attacker))
        {
            // Roll back sidestep if attacker cannot complete the move-through.
            SquareCell rollbackCell = Grid.GetCell(originalTargetPos);
            if (rollbackCell != null && !rollbackCell.IsOccupied)
                target.MoveToCell(rollbackCell);
            return false;
        }

        CombatUI.ShowCombatLog($"↔ {target.Stats.CharacterName} avoids and sidesteps to ({sidestepCell.Coords.x},{sidestepCell.Coords.y}).");
        attacker.MoveToCell(attackerDestination);
        CombatUI.ShowCombatLog($"➤ {attacker.Stats.CharacterName} moves through into ({originalTargetPos.x},{originalTargetPos.y}).");

        overrunDirection = originalTargetPos - attackerStart;
        overrunDirection.x = Mathf.Clamp(overrunDirection.x, -1, 1);
        overrunDirection.y = Mathf.Clamp(overrunDirection.y, -1, 1);

        movementCost = SquareGridUtils.CalculatePathCost(attackerStart, new List<Vector2Int> { originalTargetPos });
        return true;
    }
    private SquareCell FindOverrunSidestepCell(CharacterController defender, Vector2Int attackerPosition)
    {
        if (defender == null)
            return null;

        Vector2Int defenderPos = defender.GridPosition;
        Vector2Int approachDir = defenderPos - attackerPosition;
        approachDir.x = Mathf.Clamp(approachDir.x, -1, 1);
        approachDir.y = Mathf.Clamp(approachDir.y, -1, 1);
        if (approachDir == Vector2Int.zero)
            approachDir = Vector2Int.right;

        var candidates = new List<Vector2Int>
        {
            defenderPos + new Vector2Int(-approachDir.y, approachDir.x),
            defenderPos + new Vector2Int(approachDir.y, -approachDir.x),
            defenderPos + new Vector2Int(-approachDir.y + approachDir.x, approachDir.x + approachDir.y),
            defenderPos + new Vector2Int(approachDir.y + approachDir.x, -approachDir.x + approachDir.y)
        };

        // Fall back to any adjacent square if preferred sidestep cells are blocked.
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;
                Vector2Int candidate = defenderPos + new Vector2Int(dx, dy);
                if (!candidates.Contains(candidate))
                    candidates.Add(candidate);
            }
        }

        foreach (Vector2Int candidate in candidates)
        {
            if (candidate == attackerPosition)
                continue;

            SquareCell candidateCell = Grid.GetCell(candidate);
            if (candidateCell == null || candidateCell.IsOccupied)
                continue;

            if (!Grid.CanPlaceCreature(candidate, defender.GetVisualSquaresOccupied(), defender))
                continue;

            return candidateCell;
        }

        return null;
    }
}
