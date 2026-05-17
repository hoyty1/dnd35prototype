using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DND35.Magic;
using UnityEngine;
using DND35e.Identifiers;
using Random = UnityEngine.Random;

/// <summary>
/// GameManager partial class: Player Combat Actions &amp; Target Resolution
/// 
/// Contains all player-facing combat action handlers:
/// - Attack target selection and highlighting
/// - Melee/ranged/thrown attack execution
/// - Full attack with retargeting and 5-foot step
/// - Dual-wield and flurry of blows
/// - Special attack targeting and execution (grapple, trip, disarm, etc.)
/// - Movement click handling and AoO resolution
/// - Off-hand attack handling
/// - Cell click routing
/// - Turn ending logic
/// 
/// Extracted from main GameManager.cs to reduce file size.
/// </summary>
public partial class GameManager
{
    // ═══════════════════════════════════════════════════════════════════
    //  PLAYER COMBAT ACTIONS &amp; TARGET RESOLUTION
    // ═══════════════════════════════════════════════════════════════════

    private void ShowAttackTargets(CharacterController pc)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        // All combatants are considered for flanking checks (team and threat filtering happens in CombatUtils).
        List<CharacterController> allCombatants = GetAllCharacters();

        // Determine the equipped weapon's range semantics based on selected attack type.
        ItemData weapon = pc.GetEquippedMainWeapon();
        bool usingThrownAttack = IsUsingThrownAttackMode(pc, weapon);
        bool isRangedWeapon = IsAttackModeRanged(pc, weapon);
        bool isThrownWeapon = usingThrownAttack || (weapon != null && weapon.WeaponCat == WeaponCategory.Ranged && weapon.IsThrown);
        int rangeIncrement = (weapon != null && isRangedWeapon) ? weapon.RangeIncrement : 0;

        if (usingThrownAttack && weapon != null)
        {
            Debug.Log($"[Attack][Thrown] Showing thrown target selection for {pc.Stats.CharacterName} using {weapon.Name} (increment {weapon.RangeIncrement} ft)");
        }

        int meleeMinDistance = 1;
        int meleeMaxDistance = 1;

        int maxRangeSquares;
        if (isRangedWeapon && rangeIncrement > 0)
        {
            maxRangeSquares = RangeCalculator.GetMaxRangeSquares(rangeIncrement, isThrownWeapon);
            ShowRangeZoneHighlights(pc, rangeIncrement, maxRangeSquares, isThrownWeapon);
        }
        else if (!isRangedWeapon)
        {
            // IMPORTANT: use the same min/max ring logic as actual melee validation.
            meleeMinDistance = pc.GetMeleeMinAttackDistance(weapon);
            meleeMaxDistance = pc.GetMeleeMaxAttackDistance(weapon);
            maxRangeSquares = Mathf.Max(1, meleeMaxDistance);
            ShowMeleeRangeZoneHighlights(pc, meleeMinDistance, meleeMaxDistance);
        }
        else
        {
            maxRangeSquares = pc.Stats.AttackRange;
        }

        int sizePadding = Mathf.Max(0, pc.GetVisualSquaresOccupied() - 1);
        List<SquareCell> allCells = isRangedWeapon
            ? Grid.GetCellsInRange(pc.GridPosition, maxRangeSquares + sizePadding)
            : GetCellsInChebyshevRange(pc.GridPosition, maxRangeSquares + sizePadding);
        bool hasTarget = false;
        bool anyFlanking = false;

        foreach (var cell in allCells)
        {
            if (cell.IsOccupied && cell.Occupant != pc && !cell.Occupant.Stats.IsDead)
            {
                if (!IsEnemyTeam(pc, cell.Occupant))
                    continue;

                if (!pc.IsTargetInCurrentWeaponRange(cell.Occupant))
                    continue;

                if (isRangedWeapon && !pc.CanSee(cell.Occupant, incomingIsRangedAttack: true))
                    continue;

                // Check whether attacker can flank this target with any ally who actually threatens.
                CharacterController flankPartner;
                bool flanking = CombatUtils.IsAttackerFlanking(pc, cell.Occupant, allCombatants, out flankPartner);

                if (flanking)
                {
                    cell.SetHighlight(HighlightType.Flanking);
                    anyFlanking = true;
                }
                else
                {
                    // For melee targeting we keep enemy cells in the same "valid ring" color language.
                    cell.SetHighlight(isRangedWeapon ? HighlightType.Attack : HighlightType.AttackRange);
                }
                _highlightedCells.Add(cell);
                hasTarget = true;
            }
        }

        // ── WALL OF ICE TARGETING ──
        // Also highlight Wall of Ice cells within attack range as valid targets.
        // Walls can be attacked with melee or ranged weapons (Hardness 0).
        bool hasWallTarget = false;
        if (_pendingAttackMode != PendingAttackMode.CastSpell && _pendingAttackMode != PendingAttackMode.TemplateSmite)
        {
            HashSet<Vector2Int> wallCells = WallOfIceAreaEffect.GetAllIntactWallOfIceCells();
            if (wallCells.Count > 0)
            {
                foreach (Vector2Int wallCoord in wallCells)
                {
                    // Check if wall cell is within weapon range
                    int dist = SquareGridUtils.ChebyshevDistance(pc.GridPosition, wallCoord);

                    bool inRange;
                    if (isRangedWeapon)
                    {
                        int maxRangeSq = rangeIncrement > 0
                            ? RangeCalculator.GetMaxRangeSquares(rangeIncrement, isThrownWeapon)
                            : pc.Stats.AttackRange;
                        inRange = dist <= maxRangeSq;
                    }
                    else
                    {
                        inRange = dist >= meleeMinDistance && dist <= meleeMaxDistance;
                    }

                    if (!inRange)
                        continue;

                    SquareCell wallCell = Grid.GetCell(wallCoord);
                    if (wallCell == null)
                        continue;

                    // Don't re-highlight if already highlighted as an enemy target
                    if (_highlightedCells.Contains(wallCell))
                        continue;

                    wallCell.SetHighlight(HighlightType.AttackRange);
                    _highlightedCells.Add(wallCell);
                    hasWallTarget = true;
                    hasTarget = true;
                }
            }
        }

        if (hasTarget)
        {
            string flankMsg = anyFlanking ? " (FLANKING available! +2 to hit)" : "";
            string wallMsg = hasWallTarget ? " | Wall of Ice can be targeted" : "";
            string modeStr = "";
            switch (_pendingAttackMode)
            {
                case PendingAttackMode.Single: modeStr = "ATTACK"; break;
                case PendingAttackMode.FullAttack: modeStr = "FULL ATTACK"; break;
                case PendingAttackMode.DualWield: modeStr = "DUAL WIELD"; break;
                case PendingAttackMode.FlurryOfBlows: modeStr = "FLURRY OF BLOWS"; break;
                case PendingAttackMode.CastSpell: modeStr = "CAST SPELL"; break;
                case PendingAttackMode.TemplateSmite: modeStr = "SMITE"; break;
            }

            if (_pendingAttackMode == PendingAttackMode.Single && _currentAttackType == AttackType.Thrown)
                modeStr = "THROWN ATTACK";

            string rangeMsg = "";
            if (isRangedWeapon && rangeIncrement > 0)
            {
                int incSquares = RangeCalculator.GetRangeIncrementSquares(rangeIncrement);
                int maxRange = RangeCalculator.GetMaxRangeFeet(rangeIncrement, isThrownWeapon);
                rangeMsg = $"\n{weapon.Name}: {rangeIncrement} ft increment ({incSquares} sq), max {maxRange} ft";
            }
            else if (weapon == null)
            {
                var unarmed = pc.GetUnarmedDamage();
                rangeMsg = $"\nUnarmed strike: {unarmed.damageCount}d{unarmed.damageDice}";
            }

            if (CombatUI.TurnIndicatorText != null && !CombatUI.TurnIndicatorText.text.Contains("DUAL WIELD"))
                CombatUI.SetTurnIndicator($"{modeStr}: Click an enemy to attack!{flankMsg}{wallMsg}{rangeMsg}");
        }
        else
        {
            string noRangeMsg = isRangedWeapon ? "No enemies within maximum range!" : "No enemies in range!";
            CombatUI.SetTurnIndicator(noRangeMsg);
            StartCoroutine(ReturnToActionChoicesAfterDelay(1.5f));
        }
    }

    private List<CharacterController> GetValidRangedTargets(CharacterController attacker)
    {
        var valid = new List<CharacterController>();
        if (attacker == null || !IsAttackModeRanged(attacker))
            return valid;

        ItemData weapon = attacker.GetEquippedMainWeapon();
        int rangeIncrement = weapon != null ? weapon.RangeIncrement : 0;
        bool isThrownWeapon = IsUsingThrownAttackMode(attacker, weapon) || (weapon != null && weapon.WeaponCat == WeaponCategory.Ranged && weapon.IsThrown);

        int maxRangeSquares = (rangeIncrement > 0)
            ? RangeCalculator.GetMaxRangeSquares(rangeIncrement, isThrownWeapon)
            : attacker.Stats.AttackRange;

        int sizePadding = Mathf.Max(0, attacker.GetVisualSquaresOccupied() - 1);
        List<SquareCell> allCells = Grid.GetCellsInRange(attacker.GridPosition, maxRangeSquares + sizePadding);
        foreach (SquareCell cell in allCells)
        {
            if (cell == null || !cell.IsOccupied || cell.Occupant == null || cell.Occupant == attacker)
                continue;

            CharacterController candidate = cell.Occupant;
            if (candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!IsEnemyTeam(attacker, candidate))
                continue;

            if (!attacker.IsTargetInCurrentWeaponRange(candidate))
                continue;

            if (!attacker.CanSee(candidate, incomingIsRangedAttack: true))
                continue;

            valid.Add(candidate);
        }

        return valid;
    }

    private List<CharacterController> GetValidMeleeTargets(CharacterController attacker)
    {
        var valid = new List<CharacterController>();
        if (attacker == null)
            return valid;

        foreach (CharacterController candidate in GetAllCharacters())
        {
            if (candidate == null || candidate == attacker || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!IsEnemyTeam(attacker, candidate))
                continue;

            if (attacker.IsTargetInCurrentWeaponRange(candidate))
                valid.Add(candidate);
        }

        return valid;
    }

    private List<CharacterController> GetValidTargetsForCurrentWeapon(CharacterController attacker)
    {
        if (attacker == null)
            return new List<CharacterController>();

        return IsAttackModeRanged(attacker)
            ? GetValidRangedTargets(attacker)
            : GetValidMeleeTargets(attacker);
    }

    private bool IsTargetInCurrentWeaponRange(CharacterController attacker, CharacterController target)
    {
        if (attacker == null || target == null || target.Stats == null || target.Stats.IsDead)
            return false;

        if (IsUsingThrownAttackMode(attacker))
            return attacker.IsTargetInThrownWeaponRange(target);

        return attacker.IsTargetInCurrentWeaponRange(target);
    }

    private bool HasAnyValidTargetFromPosition(CharacterController attacker, Vector2Int attackerPosition, bool rangedMode)
    {
        if (attacker == null || attacker.Stats == null)
            return false;

        ItemData weapon = attacker.GetEquippedMainWeapon();
        int rangeIncrement = weapon != null ? weapon.RangeIncrement : 0;
        bool isThrownWeapon = IsUsingThrownAttackMode(attacker, weapon) || (weapon != null && weapon.WeaponCat == WeaponCategory.Ranged && weapon.IsThrown);
        List<Vector2Int> attackerSquares = attacker.GetOccupiedSquaresAt(attackerPosition);

        foreach (CharacterController candidate in GetAllCharacters())
        {
            if (candidate == null || candidate == attacker || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!IsEnemyTeam(attacker, candidate))
                continue;

            if (rangedMode)
            {
                int sqDist = int.MaxValue;
                List<Vector2Int> candidateSquares = candidate.GetOccupiedSquares();
                for (int i = 0; i < attackerSquares.Count; i++)
                {
                    for (int j = 0; j < candidateSquares.Count; j++)
                    {
                        int d = SquareGridUtils.GetDistance(attackerSquares[i], candidateSquares[j]);
                        if (d < sqDist)
                            sqDist = d;
                    }
                }

                if (rangeIncrement > 0)
                {
                    int distFeet = RangeCalculator.SquaresToFeet(sqDist);
                    if (RangeCalculator.IsWithinMaxRange(distFeet, rangeIncrement, isThrownWeapon))
                        return true;
                }
                else if (sqDist <= attacker.Stats.AttackRange)
                {
                    return true;
                }
            }
            else
            {
                if (CombatUtils.CanThreatenTargetFromPosition(attacker, attackerPosition, candidate))
                    return true;
            }
        }

        return false;
    }

    private IEnumerator WaitForFullAttackRetargetSelection(CharacterController attacker, int remainingAttacks)
    {
        bool rangedMode = attacker != null && attacker.IsEquippedWeaponRanged();
        string modeLabel = rangedMode ? "ranged" : "melee";

        _isAwaitingRangedRetargetSelection = true;
        _rangedRetargetSelectionCancelled = false;
        _selectedRangedRetarget = null;

        CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
        ShowAttackTargets(attacker);
        CombatUI?.ShowCombatLog($"🎯 Select a new {modeLabel} target for {remainingAttacks} remaining attack(s), or right-click/ESC to cancel.");
        CombatUI?.SetTurnIndicator($"TARGET SWITCH: Select {modeLabel} target ({remainingAttacks} attack(s) remain) | Right-click/ESC to cancel");

        while (_isAwaitingRangedRetargetSelection)
            yield return null;

        CurrentSubPhase = PlayerSubPhase.Animating;
    }

    private void ShowFullAttackFiveFootStepOptions(CharacterController pc)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        foreach (Vector2Int neighbor in SquareGridUtils.GetNeighbors(pc.GridPosition))
        {
            if (!IsValidFiveFootStepDestination(pc, neighbor))
                continue;

            if (_fullAttackFiveFootStepRequireReachableTarget
                && !HasAnyValidTargetFromPosition(pc, neighbor, _fullAttackFiveFootStepRangedMode))
            {
                continue;
            }

            SquareCell cell = Grid.GetCell(neighbor);
            if (cell == null) continue;

            cell.SetHighlight(HighlightType.FiveFootStep);
            _highlightedCells.Add(cell);
        }

        HighlightCharacterFootprint(pc, HighlightType.Selected);
    }

    private IEnumerator WaitForOptionalFiveFootStepDuringFullAttack(
        CharacterController attacker,
        string prompt,
        bool requireReachableTargetAfterStep,
        bool rangedMode)
    {
        if (attacker == null || !CanTakeFiveFootStep(attacker))
            yield break;

        _isAwaitingFullAttackFiveFootStepSelection = true;
        _fullAttackFiveFootStepSelectionCancelled = false;
        _fullAttackFiveFootStepWasTaken = false;
        _fullAttackFiveFootStepRequireReachableTarget = requireReachableTargetAfterStep;
        _fullAttackFiveFootStepRangedMode = rangedMode;

        CurrentSubPhase = PlayerSubPhase.TakingFiveFootStep;
        ShowFullAttackFiveFootStepOptions(attacker);

        if (_highlightedCells.Count == 0)
        {
            _isAwaitingFullAttackFiveFootStepSelection = false;
            _fullAttackFiveFootStepSelectionCancelled = true;
            Grid.ClearAllHighlights();
            _highlightedCells.Clear();
            CurrentSubPhase = PlayerSubPhase.Animating;
            yield break;
        }

        CombatUI?.ShowCombatLog($"↔ {prompt} Select a highlighted square for a 5-foot step, or right-click/ESC to skip.");
        CombatUI?.SetTurnIndicator($"5-FOOT STEP: {prompt} Click destination or right-click/ESC to skip");

        while (_isAwaitingFullAttackFiveFootStepSelection)
            yield return null;

        CurrentSubPhase = PlayerSubPhase.Animating;
    }

    /// <summary>
    /// Returns all valid grid cells in a Chebyshev square radius (diagonals count as 1).
    /// This is used for melee/reach previews so corner cells are never dropped by
    /// D&D 3.5 movement-distance filtering.
    /// </summary>
    private List<SquareCell> GetCellsInChebyshevRange(Vector2Int center, int range, bool includeCenter = false)
    {
        var cells = new List<SquareCell>();
        if (Grid == null || range < 0)
            return cells;

        for (int x = center.x - range; x <= center.x + range; x++)
        {
            for (int y = center.y - range; y <= center.y + range; y++)
            {
                var coords = new Vector2Int(x, y);
                if (!includeCenter && coords == center)
                    continue;

                if (SquareGridUtils.GetChebyshevDistance(center, coords) > range)
                    continue;

                SquareCell cell = Grid.GetCell(coords);
                if (cell != null)
                    cells.Add(cell);
            }
        }

        return cells;
    }

    private void ShowMeleeRangeZoneHighlights(CharacterController attacker, int minDistance, int maxDistance)
    {
        if (attacker == null || Grid == null)
            return;

        int min = Mathf.Max(1, minDistance);
        int max = Mathf.Max(min, maxDistance);

        int sizePadding = Mathf.Max(0, attacker.GetVisualSquaresOccupied() - 1);
        List<Vector2Int> attackerOccupiedSquares = attacker.GetOccupiedSquares();
        List<SquareCell> allCells = GetCellsInChebyshevRange(attacker.GridPosition, max + sizePadding);
        foreach (SquareCell cell in allCells)
        {
            if (cell == null)
                continue;
            if (cell.IsOccupied && cell.Occupant == attacker)
                continue;

            int sqDist = int.MaxValue;
            foreach (Vector2Int occupied in attackerOccupiedSquares)
            {
                int d = SquareGridUtils.GetChebyshevDistance(occupied, cell.Coords);
                if (d < sqDist) sqDist = d;
            }
            if (sqDist <= 0 || sqDist > max)
                continue;

            // Dead zone ring(s): inside max reach but below legal min distance.
            if (sqDist < min)
            {
                cell.SetHighlight(HighlightType.AttackDeadZone);
                continue;
            }

            // Legal melee ring(s): exactly what CanMeleeAttackDistance uses.
            if (sqDist >= min && sqDist <= max)
                cell.SetHighlight(HighlightType.AttackRange);
        }
    }

    private void ShowRangeZoneHighlights(CharacterController pc, int rangeIncrement, int maxRangeSquares, bool isThrownWeapon = false)
    {
        int sizePadding = Mathf.Max(0, pc.GetVisualSquaresOccupied() - 1);
        List<Vector2Int> occupiedSquares = pc.GetOccupiedSquares();
        List<SquareCell> allCells = Grid.GetCellsInRange(pc.GridPosition, maxRangeSquares + sizePadding);
        foreach (var cell in allCells)
        {
            if (cell.IsOccupied && cell.Occupant == pc) continue;

            int sqDist = int.MaxValue;
            foreach (Vector2Int occupied in occupiedSquares)
            {
                int d = SquareGridUtils.GetDistance(occupied, cell.Coords);
                if (d < sqDist) sqDist = d;
            }

            int zone = RangeCalculator.GetRangeZone(sqDist, rangeIncrement, isThrownWeapon);

            switch (zone)
            {
                case 1: cell.SetHighlight(HighlightType.RangeClose); break;
                case 2: cell.SetHighlight(HighlightType.RangeMedium); break;
                case 3: cell.SetHighlight(HighlightType.RangeFar); break;
            }
        }
    }

    private IEnumerator ReturnToActionChoicesAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!IsPlayerTurn) yield break;

        CharacterController pc = ActivePC;
        if (pc != null && CurrentSubPhase == PlayerSubPhase.SelectingAttackTarget && _pendingDefensiveAttackSelection)
        {
            pc.SetFightingDefensively(false);
            _pendingDefensiveAttackSelection = false;
            CombatUI?.ShowCombatLog($"↩ {pc.Stats.CharacterName} cancels defensive attack declaration.");
            UpdateAllStatsUI();
        }

        ShowActionChoices();
    }

    // ========== CELL CLICK HANDLING ==========

    public void OnCellClicked(SquareCell cell)
    {
        // ── Test-panel bypass: allow targeting even outside a real PC turn ──
        CharacterController testCaster = GetTestPanelCaster();
        if (testCaster != null)
        {
            Debug.Log($"[TestPanel] OnCellClicked routed via test-panel caster={testCaster.Stats?.CharacterName}  SubPhase={CurrentSubPhase}  cell={cell.Coords}");
        }

        if (CurrentPhase == TurnPhase.CombatOver && testCaster == null) return;

        CharacterController pc = ActivePC ?? testCaster;
        if (pc == null) return;

        switch (CurrentSubPhase)
        {
            case PlayerSubPhase.Moving:
                HandleMovementClick(pc, cell);
                break;

            case PlayerSubPhase.TakingFiveFootStep:
                HandleFiveFootStepClick(pc, cell);
                break;

            case PlayerSubPhase.Crawling:
                HandleCrawlClick(pc, cell);
                break;

            case PlayerSubPhase.SelectingAttackTarget:
                HandleAttackTargetClick(pc, cell);
                break;

            case PlayerSubPhase.SelectingSpecialTarget:
                if (_isSelectingMirrorImageSwap)
                    HandleMirrorImageSwapCellClick(pc, cell);
                else
                    HandleSpecialAttackTargetClick(pc, cell);
                break;

            case PlayerSubPhase.ConfirmingTurnUndead:
                ConfirmTurnUndeadTargeting();
                break;

            case PlayerSubPhase.SelectingChargeTarget:
                HandleChargeTargetClick(pc, cell);
                break;

            case PlayerSubPhase.ConfirmingChargePath:
                HandleChargeConfirmationClick(pc, cell);
                break;

            case PlayerSubPhase.SelectingAoETarget:
                HandleAoETargetClick(pc, cell);
                break;

            case PlayerSubPhase.SelectingFlamingSphereTarget:
                HandleFlamingSphereControlClick(pc, cell);
                break;

            case PlayerSubPhase.ChoosingAction:
                break;
        }
    }

    private const float PlayerMoveSecondsPerStep = 0.08f;
    private const float NpcChargeMoveSecondsPerStep = 0.06f;

    private void HandleMovementClick(CharacterController pc, SquareCell cell)
    {
        if (_waitingForAoOConfirmation) return;

        if (_isSelectingOverrunDestination)
        {
            HandleOverrunDestinationClick(pc, cell);
            return;
        }

        if (_isFreeAdjacentGrappleMoveSelection)
        {
            HandleFreeAdjacentGrappleMovementClick(pc, cell);
            return;
        }

        if (_isGrappleMoveSelection)
        {
            HandleGrappleMovementClick(pc, cell);
            return;
        }

        if (_isOverrunContinuationSelection)
        {
            HandleOverrunContinuationMovementClick(pc, cell);
            return;
        }

        if (cell.Coords == pc.GridPosition)
        {
            CancelMovementSelection();
            return;
        }

        if (!_highlightedCells.Contains(cell) || !Grid.CanPlaceCreature(cell.Coords, pc.GetVisualSquaresOccupied(), pc))
            return;

        int movementRangeOverride = _isSelectingWithdraw ? GetWithdrawMoveRangeSquares(pc) : -1;
        bool suppressFirstSquareAoO = _isSelectingWithdraw;
        var pathResult = _movementService != null
            ? _movementService.FindPath(
                pc,
                cell.Coords,
                avoidThreats: true,
                maxRangeOverride: movementRangeOverride > 0 ? movementRangeOverride : (int?)null,
                allowThroughAllies: true,
                allowThroughEnemies: false,
                suppressFirstSquareAoO: suppressFirstSquareAoO)
            : Grid.FindSafePath(pc.GridPosition, cell.Coords, pc, GetAllCharacters());

        if (pathResult == null || pathResult.Path == null || pathResult.Path.Count == 0)
        {
            CombatUI?.ShowCombatLog("⚠ No valid movement path to that tile.");
            return;
        }

        if (!pathResult.ProvokesAoOs)
        {
            StartCoroutine(ExecuteMovement(pc, new List<Vector2Int>(pathResult.Path), isWithdraw: _isSelectingWithdraw));
            return;
        }

        Debug.Log($"[GameManager] Movement to ({cell.Coords.x},{cell.Coords.y}) would provoke {pathResult.ProvokedAoOs.Count} AoO(s)!");

        var uniqueThreateners = new List<CharacterController>();
        var seen = new HashSet<CharacterController>();
        foreach (var aooInfo in pathResult.ProvokedAoOs)
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
            ActionDescription = $"Move to ({cell.Coords.x},{cell.Coords.y})",
            Actor = pc,
            ThreateningEnemies = uniqueThreateners,
            OnProceed = () => StartCoroutine(ResolveAoOsAndMove(pc, pathResult, isWithdraw: _isSelectingWithdraw)),
            OnCancel = () =>
            {
                CurrentSubPhase = PlayerSubPhase.Moving;
                if (_isGrappleMoveSelection)
                    ShowGrappleMoveRange(pc);
                else
                    ShowMovementRange(pc, _isSelectingWithdraw ? GetWithdrawMoveRangeSquares(pc) : -1);
                CombatUI.SetActionButtonsVisible(false);
                CombatUI.SetTurnIndicator(_isGrappleMoveSelection
                    ? $"{pc.Stats.CharacterName} - Move while grappling: choose destination within half speed ({_grappleMoveMaxRangeSquares} sq)"
                    : (_isSelectingWithdraw
                        ? $"{pc.Stats.CharacterName} - Withdraw: select destination (double move, first square avoids AoO)"
                        : $"{pc.Stats.CharacterName} - Click a tile to move (right-click/ESC or own tile to cancel)"));
            }
        });
    }

    private IEnumerator ResolveAoOsAndMove(CharacterController pc, AoOPathResult pathResult, bool isWithdraw = false)
    {
        if (pc == null || pc.Stats == null)
            yield break;

        CurrentSubPhase = PlayerSubPhase.Animating;

        List<Vector2Int> path = (pathResult != null && pathResult.Path != null && pathResult.Path.Count > 0)
            ? pathResult.Path
            : new List<Vector2Int>();

        List<AoOThreatInfo> provokedAoOs = (pathResult != null && pathResult.ProvokedAoOs != null)
            ? pathResult.ProvokedAoOs
            : null;

        yield return StartCoroutine(ExecuteMovement(pc, path, isWithdraw, provokedAoOs));
    }

    private IEnumerator ExecuteMovement(CharacterController pc, List<Vector2Int> path, bool isWithdraw = false, List<AoOThreatInfo> provokedAoOs = null)
    {
        if (pc == null || pc.Stats == null || path == null || path.Count == 0)
            yield break;

        CurrentSubPhase = PlayerSubPhase.Animating;

        // Hide path preview and hover marker immediately when movement begins
        if (_pathPreview != null) _pathPreview.HidePath();
        if (_hoverMarker != null) _hoverMarker.Hide();

        bool consumedAction = false;
        if (isWithdraw)
        {
            if (pc.Actions.HasFullRoundAction)
            {
                pc.Actions.UseFullRoundAction();
                consumedAction = true;
            }
            else
            {
                CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot complete Withdraw without a full-round action.");
                ShowActionChoices();
                yield break;
            }

            pc.IsWithdrawing = true;
            pc.WithdrawFirstStepProtected = true;
        }
        else
        {
            if (pc.Actions.HasMoveAction)
            {
                pc.Actions.UseMoveAction();
                consumedAction = true;
            }
            else if (pc.Actions.CanConvertStandardToMove)
            {
                pc.Actions.ConvertStandardToMove();
                consumedAction = true;
            }
        }

        if (!consumedAction)
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} has no action available for movement.");
            ShowActionChoices();
            yield break;
        }

        bool interruptedByIncapacitation = false;
        bool interruptedByGreaseSlip = false;
        int movementBudgetSquares = isWithdraw ? GetWithdrawMoveRangeSquares(pc) : GetCurrentMoveRangeSquares(pc);
        int movementCostConsumed = 0;
        Vector2Int previousCell = pc.GridPosition;

        for (int pathIndex = 0; pathIndex < path.Count; pathIndex++)
        {
            Vector2Int step = path[pathIndex];
            int stepCost = 1 + GetGreaseAreaExtraMovementCost(pc, step);
            if (movementCostConsumed + stepCost > movementBudgetSquares)
            {
                CombatUI?.ShowCombatLog($"🛢 {pc.Stats.CharacterName} cannot move farther this action (grease slows movement).");
                break;
            }

            var stepPath = new List<Vector2Int> { step };

            if (_movementService != null)
                yield return StartCoroutine(_movementService.ExecuteMovement(pc, stepPath, PlayerMoveSecondsPerStep, markAsMoved: false));
            else
                yield return StartCoroutine(pc.MoveAlongPath(stepPath, PlayerMoveSecondsPerStep, markAsMoved: false));

            movementCostConsumed += stepCost;

            if (provokedAoOs != null && provokedAoOs.Count > 0)
            {
                for (int aooIndex = 0; aooIndex < provokedAoOs.Count; aooIndex++)
                {
                    AoOThreatInfo aooInfo = provokedAoOs[aooIndex];
                    if (aooInfo == null || aooInfo.PathIndex != pathIndex)
                        continue;

                    CharacterController threatener = aooInfo.Threatener;
                    if (threatener == null || threatener.Stats == null || threatener.Stats.IsDead)
                        continue;

                    CombatResult aooResult = _movementService != null
                        ? _movementService.TriggerAoO(threatener, pc)
                        : ThreatSystem.ExecuteAoO(threatener, pc);
                    if (aooResult == null)
                        continue;

                    string aooLog = $"⚔ AoO: {aooResult.GetDetailedSummary()}";
                    CombatUI?.ShowCombatLog(aooLog);
                    UpdateAllStatsUI();

                    if (LogAttacksToConsole)
                        Debug.Log("[Combat] " + aooLog);

                    if (aooResult.Hit && aooResult.TotalDamage > 0)
                        CheckConcentrationOnDamage(pc, aooResult.TotalDamage);

                    if (pc.IsUnconscious || pc.Stats.IsDead)
                    {
                        interruptedByIncapacitation = true;
                        break;
                    }

                    yield return new WaitForSeconds(1.0f);
                }

                if (interruptedByIncapacitation)
                    break;
            }

            // ── Death/disable check after area effect damage during movement step ──
            // Area effects (Wall of Fire, etc.) may deal damage when a creature enters
            // their cells via UpdateCharacterTracking() in Unity's Update loop.
            // The MoveAlongPath coroutine already breaks on death, but we need to
            // set the interruptedByIncapacitation flag here for proper PC turn handling.
            if (!interruptedByIncapacitation && pc.Stats != null && pc.Stats.CurrentHP <= 0)
            {
                Debug.Log($"🔥 [PCMovement] {pc.Stats.CharacterName} killed/disabled by area damage during movement step {pathIndex + 1} (HP={pc.Stats.CurrentHP})");
                interruptedByIncapacitation = true;
                break;
            }

            if (!HandleGreaseStepAfterMovement(pc, previousCell, step))
            {
                interruptedByGreaseSlip = true;
                break;
            }

            previousCell = step;
        }

        if (movementCostConsumed > 0)
            pc.HasMovedThisTurn = true;

        CheckTurnUndeadProximityBreakingForMover(pc);
        PruneTurnUndeadTrackers();
        UpdateAllStatsUI();

        if (isWithdraw)
        {
            pc.WithdrawFirstStepProtected = false;
            if (!interruptedByIncapacitation)
                CombatUI?.ShowCombatLog($"↩ {pc.Stats.CharacterName} completes Withdraw.");
        }

        InvalidatePreviewThreats();

        if (interruptedByIncapacitation)
        {
            CombatUI?.ShowCombatLog($"⛔ {pc.Stats.CharacterName}'s movement stops immediately due to incapacitation.");

            if (AreAllPCsDead())
            {
                CurrentPhase = TurnPhase.CombatOver;
                CombatUI.SetTurnIndicator("DEFEAT! All heroes have fallen!");
                CombatUI.SetActionButtonsVisible(false);
                yield break;
            }

            EndActivePCTurn();
            yield break;
        }

        if (interruptedByGreaseSlip)
            CombatUI?.ShowCombatLog($"🛢 {pc.Stats.CharacterName}'s movement ends after slipping in grease.");

        ShowActionChoices();
    }

    private IEnumerator MoveCharacterAlongComputedPathWithdraw(CharacterController mover, Vector2Int destination, float secondsPerStep)
    {
        if (mover == null || mover.Stats == null || Grid == null)
            yield break;

        if (!mover.Actions.HasFullRoundAction)
            yield break;

        if (destination == mover.GridPosition)
            yield break;

        int maxRange = GetWithdrawMoveRangeSquares(mover);
        AoOPathResult pathResult = _movementService != null
            ? _movementService.FindPath(
                mover,
                destination,
                avoidThreats: false,
                maxRangeOverride: maxRange,
                allowThroughAllies: true,
                allowThroughEnemies: false,
                suppressFirstSquareAoO: true)
            : Grid.FindPathAoOAware(mover.GridPosition, destination, null, maxRange, mover.GetVisualSquaresOccupied(), mover);

        List<Vector2Int> path = (pathResult != null && pathResult.Path != null && pathResult.Path.Count > 0)
            ? pathResult.Path
            : null;

        if (path == null || path.Count == 0)
            yield break;

        mover.Actions.UseFullRoundAction();
        mover.IsWithdrawing = true;
        mover.WithdrawFirstStepProtected = true;

        if (pathResult != null && pathResult.ProvokedAoOs != null)
        {
            foreach (var aooInfo in pathResult.ProvokedAoOs)
            {
                if (mover.Stats.IsDead)
                    break;

                CharacterController threatener = aooInfo != null ? aooInfo.Threatener : null;
                if (threatener == null || threatener.Stats == null || threatener.Stats.IsDead)
                    continue;

                CombatResult aooResult = _movementService != null
                    ? _movementService.TriggerAoO(threatener, mover)
                    : ThreatSystem.ExecuteAoO(threatener, mover);

                if (aooResult != null)
                    CombatUI?.ShowCombatLog($"⚔ AoO (Withdraw): {aooResult.GetDetailedSummary()}");

                yield return new WaitForSeconds(0.35f);
            }
        }

        if (!mover.Stats.IsDead)
        {
            if (_movementService != null)
                yield return StartCoroutine(_movementService.ExecuteMovement(mover, path, secondsPerStep, markAsMoved: true));
            else
                yield return StartCoroutine(mover.MoveAlongPath(path, secondsPerStep, markAsMoved: true));

            CheckTurnUndeadProximityBreakingForMover(mover);
            PruneTurnUndeadTrackers();
        }

        mover.WithdrawFirstStepProtected = false;
    }

    private IEnumerator MoveCharacterAlongComputedPath(CharacterController mover, Vector2Int destination, float secondsPerStep)
    {
        if (mover == null || mover.Stats == null || Grid == null)
            yield break;

        if (destination == mover.GridPosition)
            yield break;

        int maxRange = GetCurrentMoveRangeSquares(mover);
        if (maxRange <= 0)
            yield break;

        AoOPathResult pathResult = _movementService != null
            ? _movementService.FindPath(mover, destination, avoidThreats: false, maxRangeOverride: maxRange)
            : Grid.FindPathAoOAware(mover.GridPosition, destination, null, maxRange, mover.GetVisualSquaresOccupied(), mover);

        List<Vector2Int> path = (pathResult != null && pathResult.Path != null && pathResult.Path.Count > 0)
            ? pathResult.Path
            : null;

        // If no valid path found (e.g. blocked by Wall of Ice or other obstacle), abort movement.
        if (path == null || path.Count == 0)
        {
            Debug.Log($"[CombatActions] MoveCharacterAlongComputedPath: No valid path to {destination} for {mover?.Stats?.CharacterName}. Movement blocked.");
            yield break;
        }

        if (_movementService != null)
            yield return StartCoroutine(_movementService.ExecuteMovement(mover, path, secondsPerStep, markAsMoved: true));
        else
            yield return StartCoroutine(mover.MoveAlongPath(path, secondsPerStep, markAsMoved: true));
        CheckTurnUndeadProximityBreakingForMover(mover);
        PruneTurnUndeadTrackers();
    }

    private void HandleAttackTargetClick(CharacterController pc, SquareCell cell)
    {
        Debug.Log($"[CombatActions] HandleAttackTargetClick  pc={pc?.Stats?.CharacterName}  cell={cell?.Coords}  mode={_pendingAttackMode}  spell={_pendingSpell?.Name}  testPanel={_testPanelCastActive}");

        // ===== SPELL CASTING MODE =====
        if (_pendingAttackMode == PendingAttackMode.CastSpell && _pendingSpell != null)
        {
            // Summon Monster spells: creature was selected first, now place it on an empty highlighted tile.
            if (IsSummonMonsterSpell(_pendingSpell))
            {
                if (_pendingSummonSelection == null)
                {
                    ShowSummonCreatureSelectionMenu(pc, _pendingSpell);
                    return;
                }

                if (!_highlightedCells.Contains(cell))
                {
                    CombatUI.ShowCombatLog("Choose a highlighted empty tile in range for the summon.");
                    return;
                }

                if (cell.IsOccupied)
                {
                    CombatUI.ShowCombatLog("Choose an empty tile to place your summon.");
                    return;
                }

                PerformSummonMonsterCast(pc, cell, _pendingSummonSelection);
                return;
            }

            if (IsSummonSwarmSpell(_pendingSpell))
            {
                if (string.IsNullOrWhiteSpace(_pendingSummonSwarmNpcId))
                {
                    ShowSummonSwarmSelectionMenu(pc, _pendingSpell);
                    return;
                }

                if (!_highlightedCells.Contains(cell))
                {
                    CombatUI.ShowCombatLog("Choose a highlighted tile in range to place the swarm.");
                    return;
                }

                PerformSummonSwarmCast(pc, cell, _pendingSummonSwarmNpcId);
                return;
            }

            // For ally/touch spells, clicking own tile = self-target.
            if (cell.Coords == pc.GridPosition
                && (_pendingSpell.TargetType == SpellTargetType.SingleAlly || _pendingSpell.TargetType == SpellTargetType.Touch))
            {
                if (IsValidTargetForSpell(pc, pc, _pendingSpell))
                {
                    PerformSpellCast(pc, pc);
                }
                else
                {
                    CombatUI.ShowCombatLog($"{_pendingSpell.Name} cannot target self right now.");
                }
                return;
            }

            // Cancel if clicking non-highlighted cell.
            if (!_highlightedCells.Contains(cell))
            {
                Debug.Log($"[CombatActions] Spell targeting CANCELLED – cell {cell?.Coords} not in highlighted set (count={_highlightedCells.Count})");
                _pendingSpell = null;
                _pendingMetamagic = null;
                _pendingSpellFromHeldCharge = false;
                _pendingAnimateRopeItem = null;
                _pendingResistEnergyType = null;
                _pendingProtectionFromEnergyType = null;
                _pendingSummonSelection = null;
                _pendingSummonListLevel = 0;
                _pendingSummonCountInfo = null;
                _pendingSummonSwarmNpcId = null;
                ResetPendingGreaseCastMode();
                CleanupTestPanelCast();
                ShowActionChoices();
                return;
            }

            // Valid target click
            if (cell.IsOccupied && !cell.Occupant.Stats.IsDead)
            {
                if (IsPendingGreaseObjectCast())
                {
                    PerformGreaseObjectCast(pc, cell.Occupant);
                    return;
                }

                if (IsPendingGreaseArmorCast())
                {
                    PerformGreaseArmorCast(pc, cell.Occupant);
                    return;
                }

                PerformSpellCast(pc, cell.Occupant);
                return;
            }

            // Fallback cancel
            _pendingSpell = null;
            _pendingMetamagic = null;
            _pendingSpellFromHeldCharge = false;
            ResetPendingGreaseCastMode();
            ShowActionChoices();
            return;
        }

        if (_pendingAttackMode == PendingAttackMode.TemplateSmite)
        {
            if (!cell.IsOccupied || cell.Occupant == null || cell.Occupant == pc || cell.Occupant.Stats == null || cell.Occupant.Stats.IsDead || !_highlightedCells.Contains(cell))
            {
                CombatUI?.ShowCombatLog("Select a highlighted valid smite target.");
                return;
            }

            ExecuteTemplateSmiteAttack(pc, cell.Occupant);
            return;
        }

        // ===== NORMAL ATTACK MODE =====
        if (_isAwaitingRangedRetargetSelection)
        {
            if (cell.IsOccupied && cell.Occupant != null && cell.Occupant != pc && !cell.Occupant.Stats.IsDead
                && _highlightedCells.Contains(cell) && IsEnemyTeam(pc, cell.Occupant))
            {
                _selectedRangedRetarget = cell.Occupant;
                _isAwaitingRangedRetargetSelection = false;
                return;
            }

            CombatUI?.ShowCombatLog("Select a highlighted valid target, or right-click/ESC to cancel remaining attacks.");
            return;
        }

        if (_isSelectingOffHandTarget)
        {
            HandleOffHandTargetClick(pc, cell);
            return;
        }

        if (!cell.IsOccupied || cell.Occupant == pc || cell.Occupant.Stats.IsDead)
        {
            // ── WALL OF ICE ATTACK ──
            // Check if the clicked cell contains a destructible Wall of Ice
            if (_highlightedCells.Contains(cell))
            {
                WallOfIceAreaEffect wall = WallOfIceAreaEffect.GetWallAtCell(cell.Coords);
                if (wall != null)
                {
                    PerformPlayerAttackOnWall(pc, wall, cell.Coords);
                    return;
                }
            }

            if (cell.Coords == pc.GridPosition || !_highlightedCells.Contains(cell))
            {
                ShowActionChoices();
                return;
            }
        }


        if (cell.IsOccupied && cell.Occupant != pc && !cell.Occupant.Stats.IsDead && _highlightedCells.Contains(cell)
            && IsEnemyTeam(pc, cell.Occupant))
        {
            PerformPlayerAttack(pc, cell.Occupant);
        }
    }

    private void HandleOffHandTargetClick(CharacterController attacker, SquareCell cell)
    {
        Debug.Log($"[OffHand] Target clicked at cell ({cell?.X},{cell?.Y}) selecting={_isSelectingOffHandTarget} highlightedCount={_highlightedCells.Count}");

        if (!_isSelectingOffHandTarget)
        {
            Debug.Log("[OffHand] Ignoring click because off-hand target selection is not active.");
            return;
        }

        if (!cell.IsOccupied || cell.Occupant == null || cell.Occupant == attacker || cell.Occupant.Stats == null || cell.Occupant.Stats.IsDead || !_highlightedCells.Contains(cell) || !IsEnemyTeam(attacker, cell.Occupant))
        {
            Debug.Log($"[OffHand] Invalid target click. occupied={cell.IsOccupied} occupant={(cell.Occupant != null ? cell.Occupant.Stats.CharacterName : "none")} highlighted={_highlightedCells.Contains(cell)} enemy={(cell.Occupant != null ? IsEnemyTeam(attacker, cell.Occupant) : false)}");
            _isSelectingOffHandTarget = false;
            _isSelectingOffHandThrownTarget = false;
            _currentOffHandBAB = 0;
            _currentOffHandWeapon = null;
            ShowActionChoices();
            return;
        }

        CharacterController target = cell.Occupant;
        ItemData offHandWeapon = _currentOffHandWeapon;
        if (offHandWeapon == null)
        {
            Debug.Log("[OffHand] Early return: current off-hand weapon is null.");
            CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} has no off-hand weapon available.");
            _isSelectingOffHandTarget = false;
            _isSelectingOffHandThrownTarget = false;
            _currentOffHandBAB = 0;
            _currentOffHandWeapon = null;
            ShowActionChoices();
            return;
        }

        bool useThrownRange = _isSelectingOffHandThrownTarget
            || (offHandWeapon.IsThrown && _currentAttackType == AttackType.Thrown);

        Debug.Log($"[OffHand] HandleOffHandTargetClick attacker={attacker.Stats.CharacterName} target={target.Stats.CharacterName} mode={(useThrownRange ? "Thrown" : "Melee")} weapon={offHandWeapon.Name} BAB={_currentOffHandBAB}");

        if (_weaponAttacksCommittedThisTurn >= 1 && !_attackSequenceConsumesFullRound)
        {
            if (!TryEnterProgressiveFullAttackStage(attacker, useThrownRange ? "an off-hand thrown attack" : "an off-hand attack"))
            {
                _isSelectingOffHandTarget = false;
                _isSelectingOffHandThrownTarget = false;
                _currentOffHandBAB = 0;
                _currentOffHandWeapon = null;
                ShowActionChoices();
                return;
            }
        }
        else if (!_isInAttackSequence)
        {
            bool shouldConsumeStandardAction = attacker.Actions.HasStandardAction && !attacker.Actions.FullRoundActionUsed;
            if (shouldConsumeStandardAction)
            {
                if (!attacker.CommitStandardAction())
                {
                    Debug.Log("[OffHand] Early return: failed to consume standard action at confirm-time.");
                    string modeLabel = useThrownRange ? "off-hand thrown attack" : "off-hand attack";
                    CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} could not commit a standard action for an {modeLabel}.");
                    _isSelectingOffHandTarget = false;
                    _isSelectingOffHandThrownTarget = false;
                    _currentOffHandBAB = 0;
                    _currentOffHandWeapon = null;
                    ShowActionChoices();
                    return;
                }

                Debug.Log($"[Attack][OffHand] Consumed standard action on confirm for {(useThrownRange ? "thrown" : "melee")} off-hand attack.");
            }
            else
            {
                Debug.Log($"[Attack][OffHand] Skipping standard action consumption (hasStandard={attacker.Actions.HasStandardAction}, fullRoundUsed={attacker.Actions.FullRoundActionUsed}, offHandAvailable={_offHandAttackAvailableThisTurn}, offHandUsed={_offHandAttackUsedThisTurn}).");
            }
        }

        CurrentSubPhase = PlayerSubPhase.Animating;

        Debug.Log("[OffHand] Calling ExecuteOffHandAttack...");
        CombatResult result = ExecuteOffHandAttack(attacker, target, _currentOffHandBAB, offHandWeapon, useThrownRange);
        Debug.Log("[OffHand] ExecuteOffHandAttack returned.");

        if (result != null)
            RegisterWeaponAttackCommitted(attacker);

        if (result != null && result.Hit && result.TotalDamage > 0)
            CheckConcentrationOnDamage(target, result.TotalDamage);

        if (result != null && result.TargetKilled)
        {
            HandleSummonDeathCleanup(target);

            if (target.Team == CharacterTeam.Enemy)
            {
                UpdateAllStatsUI();
                if (AreAllNPCsDead())
                {
                    _offHandAttackUsedThisTurn = true;
                    _isSelectingOffHandTarget = false;
                    _isSelectingOffHandThrownTarget = false;
                    Debug.Log("[CombatEnd] Victory condition met after off-hand attack kill.");
                    HandleCombatVictoryDetected("ResolveOffHandAttack");
                    return;
                }
            }
        }

        if (useThrownRange)
            ResolveOffHandThrownWeaponAfterAttack(attacker, target, offHandWeapon);

        _offHandAttackUsedThisTurn = true;
        _offHandAttackAvailableThisTurn = attacker.HasOffHandWeaponEquipped();
        _isSelectingOffHandTarget = false;
        _isSelectingOffHandThrownTarget = false;

        Debug.Log("[OffHand] Off-hand attack used this turn");
        Debug.Log($"[OffHand] _offHandAttackAvailableThisTurn: {_offHandAttackAvailableThisTurn}");
        Debug.Log($"[OffHand] _offHandAttackUsedThisTurn: {_offHandAttackUsedThisTurn}");
        Debug.Log($"[Attack][OffHand] Off-hand attack resolved. inSequence={_isInAttackSequence} mainAttacksUsed={_totalAttacksUsed}/{_totalAttackBudget} thrown={useThrownRange}");

        StartCoroutine(AfterAttackDelay(attacker, 1.2f));
    }

    private CombatResult ExecuteOffHandAttack(CharacterController attacker, CharacterController target, int attackBab, ItemData offHandWeapon, bool useThrownRange)
    {
        if (_combatFlowService != null)
            return _combatFlowService.ExecuteOffHandAttack(attacker, target, attackBab, offHandWeapon, useThrownRange);

        return null;
    }

    private void ResolveOffHandThrownWeaponAfterAttack(CharacterController thrower, CharacterController target, ItemData thrownWeapon)
    {
        if (!IsThrowableMeleeWeapon(thrownWeapon))
            return;

        if (thrower == null || thrower.Stats == null)
            return;

        Vector2Int landingPosition = target != null ? target.GridPosition : thrower.GridPosition;
        if (!TryDropThrownWeaponToGround(thrower, thrownWeapon, landingPosition, EquipSlot.LeftHand, out string dropFeedback))
        {
            Debug.LogWarning($"[Attack][OffHand][Thrown] {dropFeedback}");
            CombatUI?.ShowCombatLog($"⚠ {dropFeedback}");
            return;
        }

        CombatUI?.ShowCombatLog($"→ {thrownWeapon.Name} lands on ground at ({landingPosition.x},{landingPosition.y}).");

        if (TryEquipNextThrowableOffHandWeapon(thrower, out ItemData nextWeapon, out string equipFeedback))
        {
            Debug.Log($"[Attack][OffHand][Thrown] {equipFeedback}");
            CombatUI?.ShowCombatLog($"↻ {thrower.Stats.CharacterName} auto-equips {nextWeapon.Name} to off-hand.");
            _currentOffHandWeapon = nextWeapon;
            return;
        }

        Debug.Log($"[Attack][OffHand][Thrown] {equipFeedback}");
        _currentOffHandWeapon = thrower.GetOffHandAttackWeapon();

        if (!thrower.HasThrowableOffHandWeaponEquipped())
        {
            Debug.Log($"[Attack][OffHand][Thrown] {thrower.Stats.CharacterName} has no throwable off-hand weapon equipped after the throw.");
            CombatUI?.ShowCombatLog($"⚠ {thrower.Stats.CharacterName} has no more throwable off-hand weapons equipped.");
        }
    }

    private void ShowSpecialAttackTargets(CharacterController attacker, SpecialAttackType type)
    {
        if (type == SpecialAttackType.Overrun)
        {
            Debug.LogWarning("[Overrun][LegacyGuard] ShowSpecialAttackTargets(Overrun) was invoked; redirecting to destination selection.");
            StartOverrunDestinationSelection(attacker);
            return;
        }

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        int maxRange = (type == SpecialAttackType.Feint || type == SpecialAttackType.CoupDeGrace)
            ? 1
            : attacker.GetMeleeMaxAttackDistance();
        if (maxRange < 1) maxRange = 1;

        int sizePadding = Mathf.Max(0, attacker.GetVisualSquaresOccupied() - 1);
        List<SquareCell> allCells = GetCellsInChebyshevRange(attacker.GridPosition, maxRange + sizePadding);
        bool hasTarget = false;

        foreach (var c in allCells)
        {
            if (!c.IsOccupied || c.Occupant == attacker || c.Occupant.Stats.IsDead) continue;
            if (!IsEnemyTeam(attacker, c.Occupant)) continue;

            int distance = attacker.GetMinimumDistanceToTarget(c.Occupant, chebyshev: true);
            bool inRange = (type == SpecialAttackType.Feint || type == SpecialAttackType.CoupDeGrace)
                ? distance == 1
                : attacker.CanMeleeAttackDistance(distance);

            if (type == SpecialAttackType.Overrun)
            {
                if (!IsValidOverrunTarget(attacker, c.Occupant, out _, requireAdjacency: true))
                    continue;

                inRange = distance == 1;
            }

            if (!inRange)
                continue;

            if (type == SpecialAttackType.Disarm)
            {
                bool hasDisarmableWeapon = c.Occupant.HasDisarmableWeaponEquipped();
                c.SetHighlight(hasDisarmableWeapon ? HighlightType.Attack : HighlightType.AttackDeadZone);
                _highlightedCells.Add(c);
                hasTarget = true;
                continue;
            }

            if (type == SpecialAttackType.Sunder)
            {
                bool hasSunderableItem = c.Occupant.HasSunderableItemEquipped();
                c.SetHighlight(hasSunderableItem ? HighlightType.Attack : HighlightType.AttackDeadZone);
                _highlightedCells.Add(c);
                hasTarget = true;
                continue;
            }

            if (type == SpecialAttackType.CoupDeGrace)
            {
                bool helplessTarget = c.Occupant.IsHelplessForCoupDeGrace() && !c.Occupant.IsImmuneToCriticalHits();
                Debug.Log($"[Targeting][CoupDeGrace] candidate={c.Occupant.Stats.CharacterName} hp={c.Occupant.Stats.CurrentHP} dead={c.Occupant.Stats.IsDead} unconscious={c.Occupant.Stats.IsUnconscious} helpless={helplessTarget}");
                c.SetHighlight(helplessTarget ? HighlightType.Attack : HighlightType.AttackDeadZone);
                _highlightedCells.Add(c);
                hasTarget = true;
                continue;
            }

            c.SetHighlight(HighlightType.Attack);
            _highlightedCells.Add(c);
            hasTarget = true;
        }

        HighlightCharacterFootprint(attacker, HighlightType.Selected);

        if (hasTarget)
        {
            if (type == SpecialAttackType.Disarm)
                CombatUI.SetTurnIndicator("SPECIAL: Disarm - red targets are valid, gray targets have no disarmable weapon (Right-click/Esc to cancel)");
            else if (type == SpecialAttackType.Sunder)
                CombatUI.SetTurnIndicator("SPECIAL: Sunder - red targets are valid, gray targets have no sunderable item (Right-click/Esc to cancel)");
            else if (type == SpecialAttackType.CoupDeGrace)
                CombatUI.SetTurnIndicator("SPECIAL: Coup de Grace - red targets are helpless and vulnerable to critical hits (Right-click/Esc to cancel)");
            else
                CombatUI.SetTurnIndicator($"SPECIAL: {type} - select target (Right-click/Esc to cancel)");
        }
        else
        {
            CombatUI.SetTurnIndicator($"No targets in range for {type}.");
            StartCoroutine(ReturnToActionChoicesAfterDelay(1.0f));
        }
    }

    private void HandleSpecialAttackTargetClick(CharacterController attacker, SquareCell cell)
    {
        if (!_highlightedCells.Contains(cell) || !cell.IsOccupied || cell.Occupant == attacker)
        {
            ShowActionChoices();
            return;
        }

        CharacterController target = cell.Occupant;
        if (_pendingSpecialAttackType == SpecialAttackType.Disarm)
        {
            HandleDisarmTargetClick(attacker, target);
            return;
        }

        if (_pendingSpecialAttackType == SpecialAttackType.Sunder)
        {
            HandleSunderTargetClick(attacker, target);
            return;
        }

        if (_pendingSpecialAttackType == SpecialAttackType.Overrun)
        {
            Debug.LogWarning("[Overrun][LegacyGuard] Received special-target click while pending overrun; restarting destination selection flow.");
            StartOverrunDestinationSelection(attacker);
            return;
        }

        ExecuteSpecialAttack(attacker, target, _pendingSpecialAttackType);
    }



    private void ExecuteSpecialAttack(CharacterController attacker, CharacterController target, SpecialAttackType type, EquipSlot? disarmTargetSlot = null, EquipSlot? sunderTargetSlot = null)
    {
        if (attacker == null || target == null) { ShowActionChoices(); return; }

        CurrentSubPhase = PlayerSubPhase.Animating;

        bool specialAttackCountsAsMeleeFearBreak = type == SpecialAttackType.Trip
            || type == SpecialAttackType.Disarm
            || type == SpecialAttackType.Grapple
            || type == SpecialAttackType.Sunder
            || type == SpecialAttackType.BullRushAttack
            || type == SpecialAttackType.BullRushCharge
            || type == SpecialAttackType.Overrun
            || type == SpecialAttackType.CoupDeGrace;
        ProcessTurnUndeadMeleeFearBreak(attacker, target, specialAttackCountsAsMeleeFearBreak);

        string actionLabel = "standard action";
        int? disarmAttackBonusOverride = null;
        int disarmAttackBonusUsed = 0;
        bool disarmUsedOffHand = false;
        int disarmDualWieldPenaltyForLog = 0;
        ItemData disarmAttackerWeaponOverride = null;
        int? grappleAttackBonusOverride = null;
        int? bullRushAttackBonusOverride = null;
        int? tripAttackBonusOverride = null;
        int? sunderAttackBonusOverride = null;
        int sunderAttackBonusUsed = 0;
        bool sunderUsedOffHand = false;
        int sunderDualWieldPenaltyForLog = 0;
        ItemData sunderAttackerWeaponOverride = null;

        if (type == SpecialAttackType.Feint)
        {
            if (!TryConsumeFeintAction(attacker, out actionLabel))
            {
                CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} cannot feint: no eligible action remaining.");
                ShowActionChoices();
                return;
            }
        }
        else if (type == SpecialAttackType.CoupDeGrace)
        {
            if (!target.IsHelplessForCoupDeGrace())
            {
                CombatUI?.ShowCombatLog($"⚠ {target.Stats.CharacterName} is not helpless; Coup de Grace cannot be performed.");
                ShowActionChoices();
                return;
            }

            if (target.IsImmuneToCriticalHits())
            {
                CombatUI?.ShowCombatLog($"⚠ {target.Stats.CharacterName} is immune to critical hits and cannot be coup de graced.");
                ShowActionChoices();
                return;
            }

            if (!attacker.Actions.HasFullRoundAction)
            {
                CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} cannot perform Coup de Grace: full-round action already spent.");
                ShowActionChoices();
                return;
            }

            attacker.Actions.UseFullRoundAction();
            actionLabel = "full-round action";
        }
        else if (type == SpecialAttackType.Disarm)
        {
            if (!_isDisarmSequenceActive || _disarmInitiator != attacker || _disarmTarget != target)
                BeginDisarmSequence(attacker, target, disarmTargetSlot);

            int disarmAttemptsBefore = GetRemainingDisarmAttackActions(attacker);
            Debug.Log(
                $"[Disarm][Flow] Starting attempt {_disarmAttemptNumber + 1} attacker={attacker.Stats.CharacterName} " +
                $"target={target.Stats.CharacterName} stdAction={attacker.Actions.HasStandardAction} " +
                $"moveAction={attacker.Actions.HasMoveAction} fullRound={attacker.Actions.HasFullRoundAction} " +
                $"sharedSequenceActive={_isInAttackSequence} sequenceOwner={(_attackingCharacter != null && _attackingCharacter.Stats != null ? _attackingCharacter.Stats.CharacterName : "<null>")} " +
                $"disarmAttemptsBefore={disarmAttemptsBefore} offHandAvailable={CanUseOffHandAttackOption(attacker)} requestedOffHand={_pendingDisarmUseOffHandSelection}");

            bool useOffHandDisarm = _pendingDisarmUseOffHandSelection;
            if (!TryConsumeDisarmAttackAction(attacker, useOffHandDisarm, out disarmAttackBonusUsed, out int disarmAttacksRemaining, out string disarmConsumeReason, out disarmUsedOffHand, out disarmAttackerWeaponOverride))
            {
                string reason = string.IsNullOrWhiteSpace(disarmConsumeReason)
                    ? "no eligible disarm attack remaining"
                    : disarmConsumeReason;
                Debug.LogWarning($"[Disarm][Flow] Consume failed for {attacker.Stats.CharacterName}: {reason}");
                CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} cannot perform Disarm: {reason}.");
                _pendingDisarmUseOffHandSelection = false;
                ClearDisarmSequenceState();
                ShowActionChoices();
                return;
            }

            disarmAttackBonusOverride = disarmAttackBonusUsed;
            if (_isDualWielding)
                disarmDualWieldPenaltyForLog = disarmUsedOffHand ? _offHandPenalty : _mainHandPenalty;

            _disarmAttemptNumber++;
            string handLabel = disarmUsedOffHand ? "off-hand" : "main-hand";
            actionLabel = $"disarm attempt #{_disarmAttemptNumber} ({handLabel}), BAB {CharacterStats.FormatMod(disarmAttackBonusUsed)} ({disarmAttacksRemaining} remaining)";
            Debug.Log(
                $"[Disarm][Flow] Consume success actor={attacker.Stats.CharacterName} attempt={_disarmAttemptNumber} " +
                $"hand={handLabel} usedBAB={CharacterStats.FormatMod(disarmAttackBonusUsed)} remaining={disarmAttacksRemaining} " +
                $"stdActionNow={attacker.Actions.HasStandardAction} moveActionNow={attacker.Actions.HasMoveAction} fullRoundNow={attacker.Actions.HasFullRoundAction}");
        }
        else if (type == SpecialAttackType.Sunder)
        {
            if (!_isSunderSequenceActive || _sunderInitiator != attacker || _sunderTarget != target)
                BeginSunderSequence(attacker, target, sunderTargetSlot);

            int sunderAttemptsBefore = GetRemainingSunderAttackActions(attacker);
            Debug.Log(
                $"[Sunder][Flow] Starting attempt {_sunderAttemptNumber + 1} attacker={attacker.Stats.CharacterName} " +
                $"target={target.Stats.CharacterName} stdAction={attacker.Actions.HasStandardAction} " +
                $"moveAction={attacker.Actions.HasMoveAction} fullRound={attacker.Actions.HasFullRoundAction} " +
                $"sharedSequenceActive={_isInAttackSequence} sequenceOwner={(_attackingCharacter != null && _attackingCharacter.Stats != null ? _attackingCharacter.Stats.CharacterName : "<null>")} " +
                $"sunderAttemptsBefore={sunderAttemptsBefore} offHandAvailable={CanUseOffHandAttackOption(attacker)} requestedOffHand={_pendingSunderUseOffHandSelection}");

            bool useOffHandSunder = _pendingSunderUseOffHandSelection;
            if (!TryConsumeSunderAttackAction(attacker, useOffHandSunder, out sunderAttackBonusUsed, out int sunderAttacksRemaining, out string sunderConsumeReason, out sunderUsedOffHand, out sunderAttackerWeaponOverride))
            {
                string reason = string.IsNullOrWhiteSpace(sunderConsumeReason)
                    ? "no eligible sunder attack remaining"
                    : sunderConsumeReason;
                Debug.LogWarning($"[Sunder][Flow] Consume failed for {attacker.Stats.CharacterName}: {reason}");
                CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} cannot perform Sunder: {reason}.");
                _pendingSunderUseOffHandSelection = false;
                ClearSunderSequenceState();
                ShowActionChoices();
                return;
            }

            sunderAttackBonusOverride = sunderAttackBonusUsed;
            if (_isDualWielding)
                sunderDualWieldPenaltyForLog = sunderUsedOffHand ? _offHandPenalty : _mainHandPenalty;

            _sunderAttemptNumber++;
            string handLabel = sunderUsedOffHand ? "off-hand" : "main-hand";
            actionLabel = $"sunder attempt #{_sunderAttemptNumber} ({handLabel}), BAB {CharacterStats.FormatMod(sunderAttackBonusUsed)} ({sunderAttacksRemaining} remaining)";
            Debug.Log(
                $"[Sunder][Flow] Consume success actor={attacker.Stats.CharacterName} attempt={_sunderAttemptNumber} " +
                $"hand={handLabel} usedBAB={CharacterStats.FormatMod(sunderAttackBonusUsed)} remaining={sunderAttacksRemaining} " +
                $"stdActionNow={attacker.Actions.HasStandardAction} moveActionNow={attacker.Actions.HasMoveAction} fullRoundNow={attacker.Actions.HasFullRoundAction}");
        }
        else if (type == SpecialAttackType.Grapple)
        {
            Debug.Log($"[GameManager][Grapple] Attempting shared-pool consume actor={attacker.Stats.CharacterName} phase={CurrentPhase} subPhase={CurrentSubPhase} std={attacker.Actions.HasStandardAction} full={attacker.Actions.HasFullRoundAction} remaining={GetRemainingGrappleAttackActions(attacker)}");
            if (!TryConsumeGrappleAttackAction(attacker, out int grappleAttackBonusUsed, out int grappleAttacksRemaining, out string grappleConsumeReason))
            {
                string reason = string.IsNullOrWhiteSpace(grappleConsumeReason)
                    ? "no eligible attack remaining"
                    : grappleConsumeReason;
                Debug.LogWarning($"[GameManager][Grapple] Shared-pool consume failed actor={attacker.Stats.CharacterName} reason={reason}");
                CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} cannot initiate grapple: {reason}.");
                ShowActionChoices();
                return;
            }

            grappleAttackBonusOverride = grappleAttackBonusUsed;
            actionLabel = $"attack BAB {CharacterStats.FormatMod(grappleAttackBonusUsed)} ({grappleAttacksRemaining} remaining)";
            Debug.Log($"[GameManager][Grapple] Shared-pool consume success actor={attacker.Stats.CharacterName} usedBAB={CharacterStats.FormatMod(grappleAttackBonusUsed)} remaining={grappleAttacksRemaining}");
        }
        else if (type == SpecialAttackType.BullRushAttack)
        {
            Debug.Log($"[GameManager][BullRushAttack] Attempting shared-pool consume actor={attacker.Stats.CharacterName} phase={CurrentPhase} subPhase={CurrentSubPhase} std={attacker.Actions.HasStandardAction} full={attacker.Actions.HasFullRoundAction} remaining={GetRemainingBullRushAttackActions(attacker)}");
            if (!TryConsumeBullRushAttackAction(attacker, out int bullRushBabUsed, out int bullRushAttacksRemaining, out string bullRushConsumeReason))
            {
                string reason = string.IsNullOrWhiteSpace(bullRushConsumeReason)
                    ? "no eligible attack remaining"
                    : bullRushConsumeReason;
                Debug.LogWarning($"[GameManager][BullRushAttack] Shared-pool consume failed actor={attacker.Stats.CharacterName} reason={reason}");
                CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} cannot perform Bull Rush (Attack): {reason}.");
                ShowActionChoices();
                return;
            }

            bullRushAttackBonusOverride = bullRushBabUsed;
            actionLabel = $"attack BAB {CharacterStats.FormatMod(bullRushBabUsed)} ({bullRushAttacksRemaining} remaining)";
            Debug.Log($"[GameManager][BullRushAttack] Shared-pool consume success actor={attacker.Stats.CharacterName} usedBAB={CharacterStats.FormatMod(bullRushBabUsed)} remaining={bullRushAttacksRemaining}");
        }
        else if (type == SpecialAttackType.Trip)
        {
            Debug.Log($"[GameManager][Trip] Attempting shared-pool consume actor={attacker.Stats.CharacterName} phase={CurrentPhase} subPhase={CurrentSubPhase} std={attacker.Actions.HasStandardAction} full={attacker.Actions.HasFullRoundAction} remaining={GetRemainingTripAttackActions(attacker)}");
            if (!TryConsumeTripAttackAction(attacker, out int tripBabUsed, out int tripAttacksRemaining, out string tripConsumeReason))
            {
                string reason = string.IsNullOrWhiteSpace(tripConsumeReason)
                    ? "no eligible attack remaining"
                    : tripConsumeReason;
                Debug.LogWarning($"[GameManager][Trip] Shared-pool consume failed actor={attacker.Stats.CharacterName} reason={reason}");
                CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} cannot perform Trip: {reason}.");
                ShowActionChoices();
                return;
            }

            tripAttackBonusOverride = tripBabUsed;
            actionLabel = $"attack BAB {CharacterStats.FormatMod(tripBabUsed)} ({tripAttacksRemaining} remaining)";
            Debug.Log($"[GameManager][Trip] Shared-pool consume success actor={attacker.Stats.CharacterName} usedBAB={CharacterStats.FormatMod(tripBabUsed)} remaining={tripAttacksRemaining}");
        }
        else
        {
            if (!attacker.CommitStandardAction())
            {
                CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} cannot use {type}: standard action already spent.");
                ShowActionChoices();
                return;
            }
            actionLabel = "standard action";
        }

        bool maneuverProvokesAoO = type == SpecialAttackType.Grapple || type == SpecialAttackType.Sunder || type == SpecialAttackType.CoupDeGrace;
        if (maneuverProvokesAoO)
        {
            bool attackerIgnoresAoO = false;
            string maneuverLabel = type == SpecialAttackType.Grapple
                ? "Grapple"
                : (type == SpecialAttackType.Sunder ? "Sunder" : "Coup de Grace");

            if (type == SpecialAttackType.Grapple)
                attackerIgnoresAoO = attacker.Stats != null && attacker.Stats.HasFeat("Improved Grapple");
            else if (type == SpecialAttackType.Sunder)
                attackerIgnoresAoO = attacker.Stats != null && attacker.Stats.HasFeat("Improved Sunder");

            if (!attackerIgnoresAoO)
            {
                var provokingEnemies = new List<CharacterController>();

                if (type == SpecialAttackType.Grapple || type == SpecialAttackType.Sunder)
                {
                    if (target != null && target.Stats != null && !target.Stats.IsDead)
                        provokingEnemies.Add(target);
                }
                else
                {
                    provokingEnemies = ThreatSystem.GetThreateningEnemies(attacker.GridPosition, attacker, GetAllCharacters());
                }

                provokingEnemies.RemoveAll(enemy => enemy == null || enemy.Stats == null || enemy.Stats.IsDead || !ThreatSystem.CanMakeAoO(enemy));

                for (int i = 0; i < provokingEnemies.Count; i++)
                {
                    CharacterController enemy = provokingEnemies[i];
                    CombatResult maneuverAoO = ThreatSystem.ExecuteAoO(enemy, attacker);
                    if (maneuverAoO == null)
                        continue;

                    CombatUI.ShowCombatLog($"⚔ {maneuverLabel} initiation AoO: {maneuverAoO.GetDetailedSummary()}");
                    UpdateAllStatsUI();

                    if (type != SpecialAttackType.CoupDeGrace && maneuverAoO.Hit)
                    {
                        CombatUI.ShowCombatLog($"{maneuverLabel} attempt disrupted by attack of opportunity");
                        Grid.ClearAllHighlights();
                        _highlightedCells.Clear();
                        _isSelectingSpecialAttack = false;
                        if (type == SpecialAttackType.Sunder)
                        {
                            _pendingSunderUseOffHandSelection = false;
                            ClearSunderSequenceState();
                        }
                        StartCoroutine(AfterAttackDelay(attacker, 0.8f));
                        return;
                    }

                    if (attacker.Stats.IsDead || attacker.IsUnconscious)
                    {
                        CombatUI.ShowCombatLog($"💀 {attacker.Stats.CharacterName} is incapacitated while attempting to start {maneuverLabel.ToLowerInvariant()}.");
                        Grid.ClearAllHighlights();
                        _highlightedCells.Clear();
                        _isSelectingSpecialAttack = false;
                        if (type == SpecialAttackType.Sunder)
                        {
                            _pendingSunderUseOffHandSelection = false;
                            ClearSunderSequenceState();
                        }
                        StartCoroutine(AfterAttackDelay(attacker, 0.8f));
                        return;
                    }
                }
            }
        }

        BreakFascinationOnHostileAction(attacker, target, "threatening movement");

        SpecialAttackResult result = attacker.ExecuteSpecialAttack(
            type,
            target,
            disarmTargetSlot,
            disarmAttackBonusOverride,
            grappleAttackBonusOverride,
            bullRushAttackBonusOverride,
            bullRushChargeBonusOverride: type == SpecialAttackType.BullRushCharge ? 2 : 0,
            disarmAttackerWeaponOverride: disarmAttackerWeaponOverride,
            tripAttackBonusOverride: tripAttackBonusOverride,
            disarmUsedOffHand: disarmUsedOffHand,
            disarmDualWieldPenaltyForLog: disarmDualWieldPenaltyForLog,
            sunderTargetSlot: sunderTargetSlot,
            sunderAttackBonusOverride: sunderAttackBonusOverride,
            sunderAttackerWeaponOverride: sunderAttackerWeaponOverride,
            sunderUsedOffHand: sunderUsedOffHand,
            sunderDualWieldPenaltyForLog: sunderDualWieldPenaltyForLog);
        if (type == SpecialAttackType.Disarm)
            CombatUI.ShowCombatLog(result.Log);
        else
            CombatUI.ShowCombatLog($"⚔ SPECIAL [{type}] ({actionLabel}): {result.Log}");
        if (type == SpecialAttackType.Grapple)
        {
            int attacksRemaining = GetRemainingGrappleAttackActions(attacker);
            int nextBab = GetCurrentGrappleAttackBonus(attacker);
            Debug.Log($"[GameManager][Grapple] Result success={result.Success} actor={attacker.Stats.CharacterName} remainingSharedPool={attacksRemaining} nextBAB={CharacterStats.FormatMod(nextBab)} phase={CurrentPhase} subPhase={CurrentSubPhase}");

            if (attacksRemaining > 0)
                CombatUI?.ShowCombatLog($"↻ {attacker.Stats.CharacterName} has {attacksRemaining} grapple attack(s) remaining (next BAB {CharacterStats.FormatMod(nextBab)}).");
            else
                CombatUI?.ShowCombatLog($"↻ {attacker.Stats.CharacterName} has no grapple attacks remaining this turn.");
        }
        else if (type == SpecialAttackType.BullRushAttack)
        {
            int attacksRemaining = GetRemainingBullRushAttackActions(attacker);
            int nextBab = GetCurrentBullRushAttackBonus(attacker);
            Debug.Log($"[GameManager][BullRushAttack] Result success={result.Success} actor={attacker.Stats.CharacterName} remainingSharedPool={attacksRemaining} nextBAB={CharacterStats.FormatMod(nextBab)} phase={CurrentPhase} subPhase={CurrentSubPhase}");

            if (attacksRemaining > 0)
                CombatUI?.ShowCombatLog($"↻ {attacker.Stats.CharacterName} has {attacksRemaining} Bull Rush (Attack) attempt(s) remaining (next BAB {CharacterStats.FormatMod(nextBab)}).");
            else
                CombatUI?.ShowCombatLog($"↻ {attacker.Stats.CharacterName} has no Bull Rush (Attack) attempts remaining this turn.");
        }
        else if (type == SpecialAttackType.Trip)
        {
            int attacksRemaining = GetRemainingTripAttackActions(attacker);
            int nextBab = GetCurrentTripAttackBonus(attacker);
            Debug.Log($"[GameManager][Trip] Result success={result.Success} actor={attacker.Stats.CharacterName} remainingSharedPool={attacksRemaining} nextBAB={CharacterStats.FormatMod(nextBab)} phase={CurrentPhase} subPhase={CurrentSubPhase}");

            if (attacksRemaining > 0)
                CombatUI?.ShowCombatLog($"↻ {attacker.Stats.CharacterName} has {attacksRemaining} trip attempt(s) remaining (next BAB {CharacterStats.FormatMod(nextBab)}).");
            else
                CombatUI?.ShowCombatLog($"↻ {attacker.Stats.CharacterName} has no trip attempts remaining this turn.");
        }
        else if (type == SpecialAttackType.Disarm)
        {
            int attacksRemaining = GetRemainingDisarmAttackActions(attacker);
            int nextBab = GetCurrentDisarmAttackBonus(attacker);
            int targetDisarmableItems = target != null ? target.GetDisarmableHeldItemOptions().Count : 0;
            string handLabel = disarmUsedOffHand ? "off-hand" : "main-hand";

            Debug.Log(
                $"[Disarm][Flow] Completed attempt {_disarmAttemptNumber} attacker={attacker.Stats.CharacterName} " +
                $"target={(target != null && target.Stats != null ? target.Stats.CharacterName : "<null>")} " +
                $"success={result.Success} hand={handLabel} usedBAB={CharacterStats.FormatMod(disarmAttackBonusUsed)} " +
                $"attacksRemaining={attacksRemaining} nextBAB={CharacterStats.FormatMod(nextBab)} targetDisarmableItems={targetDisarmableItems}");

            CombatUI?.ShowCombatLog($"[Disarm] Attempt #{_disarmAttemptNumber} ({handLabel}) used BAB {CharacterStats.FormatMod(disarmAttackBonusUsed)}.");

            if (attacksRemaining > 0)
                CombatUI?.ShowCombatLog($"↻ {attacker.Stats.CharacterName} has {attacksRemaining} disarm-capable attack(s) remaining (next BAB {CharacterStats.FormatMod(nextBab)}).");
            else
                CombatUI?.ShowCombatLog($"↻ {attacker.Stats.CharacterName} has no disarm-capable attacks remaining this turn.");

            _pendingDisarmUseOffHandSelection = false;
            ClearDisarmSequenceState();
        }
        else if (type == SpecialAttackType.Sunder)
        {
            int attacksRemaining = GetRemainingSunderAttackActions(attacker);
            int nextBab = GetCurrentSunderAttackBonus(attacker);
            int targetSunderableItems = target != null ? target.GetSunderableItemOptions().Count : 0;
            string handLabel = sunderUsedOffHand ? "off-hand" : "main-hand";

            Debug.Log(
                $"[Sunder][Flow] Completed attempt {_sunderAttemptNumber} attacker={attacker.Stats.CharacterName} " +
                $"target={(target != null && target.Stats != null ? target.Stats.CharacterName : "<null>")} " +
                $"success={result.Success} hand={handLabel} usedBAB={CharacterStats.FormatMod(sunderAttackBonusUsed)} " +
                $"attacksRemaining={attacksRemaining} nextBAB={CharacterStats.FormatMod(nextBab)} targetSunderableItems={targetSunderableItems}");

            CombatUI?.ShowCombatLog($"[Sunder] Attempt #{_sunderAttemptNumber} ({handLabel}) used BAB {CharacterStats.FormatMod(sunderAttackBonusUsed)}.");

            if (attacksRemaining > 0)
                CombatUI?.ShowCombatLog($"↻ {attacker.Stats.CharacterName} has {attacksRemaining} sunder-capable attack(s) remaining (next BAB {CharacterStats.FormatMod(nextBab)}).");
            else
                CombatUI?.ShowCombatLog($"↻ {attacker.Stats.CharacterName} has no sunder-capable attacks remaining this turn.");

            _pendingSunderUseOffHandSelection = false;
            ClearSunderSequenceState();
        }

        // Fire Shield retribution: trip and disarm are melee maneuvers that involve physical contact
        if ((type == SpecialAttackType.Trip || type == SpecialAttackType.Disarm) &&
            target != null && target.Stats != null && target.Stats.FireShieldActive)
        {
            ResolveFireShieldRetribution(target, attacker);
        }

        if (result.Success)
        {
            if (type == SpecialAttackType.BullRushAttack || type == SpecialAttackType.BullRushCharge)
            {
                ResolveBullRushPushAndFollow(attacker, target, result, () => FinalizeSpecialAttackResolution(attacker, target));
                return;
            }

            if (type == SpecialAttackType.Overrun)
                TryPushTargetAway(attacker, target, 1, allowAttackerFollow: true);
        }

        FinalizeSpecialAttackResolution(attacker, target);
    }

    private void FinalizeSpecialAttackResolution(CharacterController attacker, CharacterController target)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        _isSelectingSpecialAttack = false;

        UpdateAllStatsUI();

        if (target != null && target.Stats != null && target.Stats.IsDead && target.Team == CharacterTeam.Enemy && AreAllNPCsDead())
        {
            Debug.Log("[CombatEnd] Victory condition met after special attack resolution.");
            HandleCombatVictoryDetected("FinalizeSpecialAttackResolution");
            return;
        }

        if (attacker != null)
            StartCoroutine(AfterAttackDelay(attacker, 1.0f));
    }

    private struct BullRushPushResolution
    {
        public Vector2Int Direction;
        public Vector2Int OriginalTargetPosition;
        public Vector2Int FinalTargetPosition;
        public int RequestedSquares;
        public int ActualSquares;
        public bool Obstructed;

        public bool TargetMoved => ActualSquares > 0;
    }


    private void TryPushTargetAway(CharacterController attacker, CharacterController target, int squares, bool allowAttackerFollow)
    {
        BullRushPushResolution pushResolution = ExecuteBullRushPush(attacker, target, squares);
        if (allowAttackerFollow)
            ExecuteBullRushFollow(attacker, pushResolution);
    }

    private void CancelSpecialAttackTargeting()
    {
        _isSelectingSpecialAttack = false;
        _pendingDisarmUseOffHandSelection = false;
        _pendingSunderUseOffHandSelection = false;
        ClearDisarmSequenceState();
        ClearSunderSequenceState();
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        ShowActionChoices();
    }

    // ========== CHARGE ACTION (D&D 3.5e PHB p.154) ==========


    // ========== ATTACK EXECUTION ==========

    private void PerformPlayerAttack(CharacterController attacker, CharacterController target)
    {
        if (_combatFlowService != null)
        {
            _combatFlowService.PerformPlayerAttack(attacker, target);
            return;
        }

        CurrentSubPhase = PlayerSubPhase.Animating;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  WALL OF ICE ATTACK — Player attacks a destructible wall
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Performs a weapon attack against a Wall of Ice cell.
    /// Wall of Ice: Hardness 0, so all damage applies. No attack roll needed
    /// (wall is stationary and has no AC — auto-hit, PHB p.166 "Attacking Objects").
    /// D&D 3.5e: Stationary objects have AC 5 effectively, but walls are large
    /// stationary objects — attacks auto-hit in practice.
    /// </summary>
    private void PerformPlayerAttackOnWall(CharacterController attacker, WallOfIceAreaEffect wall, Vector2Int wallCell)
    {
        if (attacker == null || wall == null)
            return;

        CurrentSubPhase = PlayerSubPhase.Animating;

        // Commit standard action for single attack
        if (_pendingAttackMode == PendingAttackMode.Single ||
            _pendingAttackMode == PendingAttackMode.FullAttack ||
            _pendingAttackMode == PendingAttackMode.DualWield ||
            _pendingAttackMode == PendingAttackMode.FlurryOfBlows)
        {
            if (!attacker.CommitStandardAction())
            {
                CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} has no standard action available.");
                ShowActionChoices();
                return;
            }
        }

        // Get weapon and calculate damage
        ItemData weapon = attacker.GetEquippedMainWeapon();
        string weaponName = weapon != null ? weapon.Name : "Unarmed strike";

        // Roll weapon damage (auto-hit against stationary wall)
        int damageCount, damageDice;
        if (weapon != null)
        {
            weapon.GetScaledDamageDice(attacker.Stats.CurrentSizeCategory, out damageCount, out damageDice);
        }
        else
        {
            var unarmed = attacker.GetUnarmedDamage();
            damageCount = unarmed.damageCount;
            damageDice = unarmed.damageDice;
        }

        int damage = 0;
        for (int i = 0; i < damageCount; i++)
            damage += UnityEngine.Random.Range(1, damageDice + 1);

        // Add STR modifier to damage (melee and thrown)
        bool isRanged = weapon != null && weapon.WeaponCat == WeaponCategory.Ranged && !weapon.IsThrown;
        if (!isRanged)
        {
            int strMod = CharacterStats.GetModifier(attacker.Stats.STR);
            // Two-handed weapons get 1.5x STR
            bool isTwoHanded = weapon != null && weapon.IsTwoHanded;
            if (isTwoHanded)
                damage += Mathf.FloorToInt(strMod * 1.5f);
            else
                damage += strMod;
        }

        // Add enhancement bonus damage
        if (weapon != null)
        {
            damage += weapon.GetEnhancementDamageBonus();
        }

        // Minimum 1 damage on a hit
        damage = Mathf.Max(1, damage);

        // Check if this is fire damage (fire weapons are especially effective)
        bool isFire = false;
        if (weapon != null && weapon.ActiveSpellEffects != null)
        {
            foreach (var eff in weapon.ActiveSpellEffects)
            {
                if (eff != null && !string.IsNullOrEmpty(eff.BonusDamageType) &&
                    eff.BonusDamageType.IndexOf("fire", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isFire = true;
                    // Add bonus fire damage dice
                    if (!string.IsNullOrEmpty(eff.BonusDamageDice))
                    {
                        // Parse "1d6" style bonus damage
                        string[] parts = eff.BonusDamageDice.Split('d');
                        if (parts.Length == 2 && int.TryParse(parts[0], out int bCount) && int.TryParse(parts[1], out int bDice))
                        {
                            for (int i = 0; i < bCount; i++)
                                damage += UnityEngine.Random.Range(1, bDice + 1);
                        }
                    }
                    break;
                }
            }
        }

        // Build combat log — per-cell damage
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"🧊 {attacker.Stats.CharacterName} attacks Wall of Ice at ({wallCell.x},{wallCell.y}) with {weaponName}!");
        sb.AppendLine($"  Auto-hit (stationary object, Hardness 0)");
        sb.AppendLine($"  Damage: {damage}{(isFire ? " (includes fire)" : "")}");

        // Apply damage to specific cell
        wall.OnCellAttacked(attacker, wallCell, damage, isFire);

        int remainingHP = wall.GetCellHP(wallCell);
        if (wall.IsBreached(wallCell))
        {
            sb.AppendLine($"  💥 Wall cell ({wallCell.x},{wallCell.y}) breached!");
        }
        else
        {
            int maxHP = wall.CasterLevel * 3;
            sb.AppendLine($"  Cell HP: {remainingHP}/{maxHP}");
        }
        sb.Append("═══════════════════════════════════");

        CombatUI?.ShowCombatLog(sb.ToString());
        UpdateAllStatsUI();
        Grid.ClearAllHighlights();

        StartCoroutine(AfterAttackDelay(attacker, 1.0f));
    }

    /// <summary>
    /// Attempts a Strength check to breach an intact Wall of Ice cell.
    /// D&D 3.5e: DC 15 + caster level. Consumes a standard action.
    /// Can be called from UI when a player selects "Break Wall" on an adjacent intact wall cell.
    /// </summary>
    public void PerformStrengthCheckOnWall(CharacterController attacker, WallOfIceAreaEffect wall, Vector2Int wallCell)
    {
        if (attacker == null || wall == null)
            return;

        // Must be adjacent (Chebyshev distance 1)
        int dist = SquareGridUtils.ChebyshevDistance(attacker.GridPosition, wallCell);
        if (dist > 1)
        {
            CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} must be adjacent to attempt a Strength check.");
            return;
        }

        // Cell must be intact
        if (wall.IsBreached(wallCell))
        {
            CombatUI?.ShowCombatLog($"⚠ That wall cell is already breached.");
            return;
        }

        // Consume standard action
        if (!attacker.CommitStandardAction())
        {
            CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} has no standard action available.");
            ShowActionChoices();
            return;
        }

        CurrentSubPhase = PlayerSubPhase.Animating;

        bool success = wall.AttemptStrengthCheck(attacker, wallCell);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        int strMod = CharacterStats.GetModifier(attacker.Stats.STR);
        int dc = 15 + wall.CasterLevel;
        sb.AppendLine($"💪 {attacker.Stats.CharacterName} attempts to break through Wall of Ice at ({wallCell.x},{wallCell.y})!");
        sb.AppendLine($"  Strength Check DC {dc} (15 + CL {wall.CasterLevel})");
        sb.AppendLine($"  STR modifier: {(strMod >= 0 ? "+" : "")}{strMod}");

        if (success)
        {
            sb.AppendLine($"  ✅ Success! The ice cracks open — cell breached!");
        }
        else
        {
            sb.AppendLine($"  ❌ Failed! The wall holds firm.");
        }
        sb.Append("═══════════════════════════════════");

        CombatUI?.ShowCombatLog(sb.ToString());
        UpdateAllStatsUI();
        Grid.ClearAllHighlights();

        StartCoroutine(AfterAttackDelay(attacker, 1.0f));
    }

    private RangeInfo CalculateRangeInfo(CharacterController attacker, CharacterController target)
    {
        if (_combatFlowService != null)
            return _combatFlowService.CalculateRangeInfo(attacker, target);

        return RangeCalculator.GetRangeInfo(0, 0, false);
    }

    private string BuildAttackLog(CharacterController attacker, bool isFlanking, string partnerName, CombatResult result)
    {
        if (_combatFlowService != null)
            return _combatFlowService.BuildAttackLog(attacker, isFlanking, partnerName, result);

        return result != null ? result.GetDetailedSummary() : string.Empty;
    }

    private void PerformIterativeSequenceAttack(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo = null)
    {
        string attackerName = attacker != null && attacker.Stats != null ? attacker.Stats.CharacterName : "<null>";
        string targetName = target != null && target.Stats != null ? target.Stats.CharacterName : "<null>";
        Debug.Log($"[AttackFlow] PerformIterativeSequenceAttack ENTER | attacker={attackerName} | target={targetName} | phase={CurrentPhase} | subPhase={CurrentSubPhase} | inSequence={_isInAttackSequence} | attacksUsed={_totalAttacksUsed}/{_totalAttackBudget}");

        if (_combatFlowService != null)
        {
            _combatFlowService.PerformIterativeSequenceAttack(attacker, target, isFlanking, flankBonus, partnerName, rangeInfo);
            Debug.Log($"[AttackFlow] PerformIterativeSequenceAttack EXIT via service | attacker={attackerName} | target={targetName} | targetDead={(target != null && target.Stats != null && target.Stats.IsDead)} | phase={CurrentPhase} | waitingLoot={WaitingForLootCollection}");
            return;
        }

        Debug.LogWarning("[AttackFlow] PerformIterativeSequenceAttack skipped because _combatFlowService is null.");
    }

    private void PerformSingleAttack(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo = null)
    {
        if (_combatFlowService != null)
        {
            _combatFlowService.PerformSingleAttack(attacker, target, isFlanking, flankBonus, partnerName, rangeInfo);
            return;
        }
    }

    private IEnumerator PerformFullAttackWithRetargetingAndFiveFootStep(CharacterController attacker, CharacterController initialTarget)
    {
        if (attacker == null || initialTarget == null)
        {
            ShowActionChoices();
            yield break;
        }

        attacker.Actions.UseFullRoundAction();

        bool rangedMode = IsAttackModeRanged(attacker);
        string modeLabel = rangedMode ? "ranged" : "melee";

        RangeInfo initialRangeInfo = CalculateRangeInfo(attacker, initialTarget);
        int plannedAttackCount = attacker.GetPlannedFullAttackCount(initialRangeInfo);
        if (plannedAttackCount <= 0)
        {
            CombatUI?.ShowCombatLog($"⚠ {attacker.Stats.CharacterName} has no available attacks.");
            StartCoroutine(DelayedEndActivePCTurn(0.8f));
            yield break;
        }

        CharacterController currentTarget = initialTarget;
        int attacksMade = 0;

        // D&D 3.5: You can take a 5-foot step before a full attack.
        if (CanTakeFiveFootStep(attacker))
        {
            yield return StartCoroutine(WaitForOptionalFiveFootStepDuringFullAttack(
                attacker,
                "Before attacks:",
                requireReachableTargetAfterStep: false,
                rangedMode: rangedMode));
        }

        for (int attackIndex = 0; attackIndex < plannedAttackCount; attackIndex++)
        {
            if (attacker == null || attacker.Stats == null || attacker.Stats.IsDead)
                break;

            int remainingAttacks = plannedAttackCount - attackIndex;
            bool needsRetarget = currentTarget == null
                || currentTarget.Stats == null
                || currentTarget.Stats.IsDead
                || !IsTargetInCurrentWeaponRange(attacker, currentTarget);

            if (needsRetarget)
            {
                if (currentTarget != null && currentTarget.Stats != null && !currentTarget.Stats.IsDead)
                {
                    CombatUI?.ShowCombatLog($"⚠ {currentTarget.Stats.CharacterName} is no longer in {modeLabel} reach.");
                }

                List<CharacterController> validTargets = GetValidTargetsForCurrentWeapon(attacker);

                if (validTargets.Count == 0 && CanTakeFiveFootStep(attacker))
                {
                    CombatUI?.ShowCombatLog($"No valid {modeLabel} targets right now. You may take a 5-foot step to continue.");

                    yield return StartCoroutine(WaitForOptionalFiveFootStepDuringFullAttack(
                        attacker,
                        "Step to reach another target:",
                        requireReachableTargetAfterStep: true,
                        rangedMode: rangedMode));

                    if (_fullAttackFiveFootStepSelectionCancelled || !_fullAttackFiveFootStepWasTaken)
                    {
                        CombatUI?.ShowCombatLog($"↩ {attacker.Stats.CharacterName} ends full attack early. {remainingAttacks} attack(s) unused.");
                        break;
                    }

                    validTargets = GetValidTargetsForCurrentWeapon(attacker);
                }

                if (validTargets.Count == 0)
                {
                    CombatUI?.ShowCombatLog($"⚠ No valid {modeLabel} targets for {remainingAttacks} remaining attack(s).");
                    break;
                }

                yield return StartCoroutine(WaitForFullAttackRetargetSelection(attacker, remainingAttacks));

                if (_rangedRetargetSelectionCancelled || _selectedRangedRetarget == null)
                {
                    CombatUI?.ShowCombatLog($"↩ {attacker.Stats.CharacterName} ends full attack early. {remainingAttacks} attack(s) unused.");
                    break;
                }

                currentTarget = _selectedRangedRetarget;
                _selectedRangedRetarget = null;
                _rangedRetargetSelectionCancelled = false;

                CombatUI?.ShowCombatLog($"🎯 {attacker.Stats.CharacterName} switches to {currentTarget.Stats.CharacterName}.");
            }

            // Recompute flanking/range context each attack in case target/position changed.
            var allCombatants = GetAllCharacters();
            CharacterController flankPartner;
            bool isFlanking = CombatUtils.IsAttackerFlanking(attacker, currentTarget, allCombatants, out flankPartner);
            int flankBonus = isFlanking ? CombatUtils.FlankingAttackBonus : 0;
            string partnerName = flankPartner != null ? flankPartner.Stats.CharacterName : "";
            RangeInfo rangeInfo = CalculateRangeInfo(attacker, currentTarget);

            bool isMeleeFearBreakAttack = IsMeleeAttackForTurnUndeadFearBreak(
                attacker,
                attacker.GetEquippedMainWeapon(),
                rangeInfo,
                treatAsThrownAttack: false);
            ProcessTurnUndeadMeleeFearBreak(attacker, currentTarget, isMeleeFearBreakAttack);

            FullAttackResult stepResult = attacker.FullAttack(
                currentTarget,
                isFlanking,
                flankBonus,
                partnerName,
                rangeInfo,
                startAttackIndex: attackIndex,
                maxAttacks: 1);

            if (stepResult == null || stepResult.Attacks.Count == 0)
                break;

            attacksMade++;
            CombatResult attack = stepResult.Attacks[0];
            string label = (stepResult.AttackLabels != null && stepResult.AttackLabels.Count > 0)
                ? stepResult.AttackLabels[0]
                : $"Attack {attackIndex + 1}";

            CombatUI?.ShowCombatLog(attack.GetAttackBreakdown(label));
            UpdateAllStatsUI();
            Grid.ClearAllHighlights();
            _highlightedCells.Clear();

            if (attack.Hit && attack.TotalDamage > 0)
                CheckConcentrationOnDamage(currentTarget, attack.TotalDamage);

            // Fire Shield retribution: defender's Fire Shield deals damage back to melee attacker
            if (attack.Hit && !rangedMode && currentTarget != null && currentTarget.Stats.FireShieldActive)
                ResolveFireShieldRetribution(currentTarget, attacker);

            TryResolveFreeTripFromAttackResults(attacker, currentTarget, stepResult.Attacks, rangeInfo);

            if (attack.TargetKilled)
            {
                HandleSummonDeathCleanup(currentTarget);

                if (currentTarget.Team == CharacterTeam.Enemy && AreAllNPCsDead())
                {
                    Debug.Log("[CombatEnd] Victory condition met during full attack sequence.");
                    HandleCombatVictoryDetected("ExecuteFullAttackSequence");
                    yield break;
                }

                int attacksRemainingAfterKill = plannedAttackCount - (attackIndex + 1);
                if (attacksRemainingAfterKill > 0)
                {
                    CombatUI?.ShowCombatLog($"💀 {currentTarget.Stats.CharacterName} is defeated! {attacksRemainingAfterKill} attack(s) remaining.");
                    currentTarget = null;
                }
            }

            // D&D 3.5: You can 5-foot step between attacks during a full attack.
            if (attackIndex < plannedAttackCount - 1 && CanTakeFiveFootStep(attacker))
            {
                yield return StartCoroutine(WaitForOptionalFiveFootStepDuringFullAttack(
                    attacker,
                    "Between attacks:",
                    requireReachableTargetAfterStep: false,
                    rangedMode: rangedMode));
            }

            yield return new WaitForSeconds(0.35f);
        }

        // D&D 3.5: You can also 5-foot step after attacks.
        if (CanTakeFiveFootStep(attacker) && CurrentPhase != TurnPhase.CombatOver)
        {
            yield return StartCoroutine(WaitForOptionalFiveFootStepDuringFullAttack(
                attacker,
                "After attacks:",
                requireReachableTargetAfterStep: false,
                rangedMode: rangedMode));
        }

        _isAwaitingRangedRetargetSelection = false;
        _selectedRangedRetarget = null;
        _rangedRetargetSelectionCancelled = false;
        _isAwaitingFullAttackFiveFootStepSelection = false;
        _fullAttackFiveFootStepSelectionCancelled = false;
        _fullAttackFiveFootStepWasTaken = false;

        CombatUI?.ShowCombatLog($"✅ {attacker.Stats.CharacterName} completes {modeLabel} full attack ({attacksMade}/{plannedAttackCount} attacks used).");
        UpdateAllStatsUI();
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        StartCoroutine(DelayedEndActivePCTurn(1.0f));
    }
    private void PerformFullAttack(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo = null)
    {
        if (_combatFlowService != null)
        {
            _combatFlowService.PerformFullAttack(attacker, target, isFlanking, flankBonus, partnerName, rangeInfo);
            return;
        }
    }

    private void PerformDualWieldAttack(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo = null)
    {
        if (_combatFlowService != null)
        {
            _combatFlowService.PerformDualWieldAttack(attacker, target, isFlanking, flankBonus, partnerName, rangeInfo);
            return;
        }
    }

    private void PerformFlurryOfBlows(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo = null)
    {
        if (_combatFlowService != null)
        {
            _combatFlowService.PerformFlurryOfBlows(attacker, target, isFlanking, flankBonus, partnerName, rangeInfo);
            return;
        }
    }

    private bool IsHoldingTouchCharge(CharacterController character)
    {
        if (character == null || !character.Stats.IsSpellcaster)
            return false;

        var spellComp = character.GetComponent<SpellcastingComponent>();
        return spellComp != null && spellComp.HasHeldTouchCharge && spellComp.HeldTouchSpell != null;
    }

    private string GetHeldTouchSpellName(CharacterController character)
    {
        var spellComp = character != null ? character.GetComponent<SpellcastingComponent>() : null;
        if (spellComp != null && spellComp.HeldTouchSpell != null)
            return spellComp.HeldTouchSpell.Name;
        return "held touch spell";
    }

    private bool ShouldAutoEndTurn(CharacterController character)
    {
        if (character == null)
            return true;

        if (character.IsControllable)
        {
            bool offHandAvailable = CanUseOffHandAttackOption(character);
            bool offHandThrownAvailable = CanUseOffHandThrownAttackOption(character);
            Debug.Log($"[TurnFlow] ShouldAutoEndTurn=false for controllable unit {character.Stats.CharacterName}. " +
                      $"Manual End Turn required. offHandAvailable={offHandAvailable} offHandThrownAvailable={offHandThrownAvailable} " +
                      $"offHandGate={_offHandAttackAvailableThisTurn} offHandUsed={_offHandAttackUsedThisTurn} attacksUsed={_totalAttacksUsed}/{_totalAttackBudget}");
            return false;
        }

        bool hasRemainingGrappleAttempts = CanUseGrappleAttackOption(character);
        bool hasRemainingBullRushAttempts = CanUseBullRushAttackOption(character);
        bool hasRemainingTripAttempts = CanUseTripAttackOption(character);
        bool hasRemainingDisarmAttempts = CanUseDisarmAttackOption(character);
        bool hasRemainingCoupDeGraceAttempt = CanUseCoupDeGraceAttackOption(character);

        bool hasIterativeWeaponAttackSequence = _isInAttackSequence && _attackingCharacter == character;

        if (hasRemainingGrappleAttempts || hasRemainingBullRushAttempts || hasRemainingTripAttempts || hasRemainingDisarmAttempts || hasRemainingCoupDeGraceAttempt || hasIterativeWeaponAttackSequence)
        {
            Debug.Log(
                $"[TurnFlow] ShouldAutoEndTurn=false for {character.Stats.CharacterName}: " +
                $"iterativeRemaining(g={hasRemainingGrappleAttempts}, br={hasRemainingBullRushAttempts}, trip={hasRemainingTripAttempts}, d={hasRemainingDisarmAttempts}, cdg={hasRemainingCoupDeGraceAttempt}, atk={hasIterativeWeaponAttackSequence})");
            return false;
        }

        bool shouldAutoEnd = !character.Actions.HasAnyActionLeft && !IsHoldingTouchCharge(character);
        Debug.Log($"[TurnFlow] ShouldAutoEndTurn character={character.Stats.CharacterName} hasAnyActionLeft={character.Actions.HasAnyActionLeft} holdingTouchCharge={IsHoldingTouchCharge(character)} => {shouldAutoEnd}");
        return shouldAutoEnd;
    }

    private IEnumerator AfterAttackDelay(CharacterController pc, float delay)
    {
        LogMenuFlow("AfterAttackDelay:START", pc, $"delay={delay:0.00}");
        yield return new WaitForSeconds(delay);

        LogMenuFlow("AfterAttackDelay:AFTER_WAIT", pc, $"delay={delay:0.00}");

        if (CurrentPhase == TurnPhase.CombatOver)
        {
            LogMenuFlow("AfterAttackDelay:ABORT_COMBAT_OVER", pc);
            yield break;
        }

        bool shouldEndTurn = ShouldAutoEndTurn(pc);
        LogMenuFlow("AfterAttackDelay:DECISION", pc, $"shouldAutoEndTurn={shouldEndTurn}");

        if (shouldEndTurn)
        {
            EndActivePCTurn();
        }
        else
        {
            Debug.Log("=== ATTACK SEQUENCE COMPLETE ===");
            Debug.Log($"[OffHand] _offHandAttackAvailableThisTurn: {_offHandAttackAvailableThisTurn}");
            Debug.Log($"[OffHand] _offHandAttackUsedThisTurn: {_offHandAttackUsedThisTurn}");
            Debug.Log("[Actions] Calling ShowActionChoices()");
            ShowActionChoices();
        }
    }

    // ========== TURN ENDING ==========

    /// <summary>
    /// End the current PC's turn and advance to the next combatant in initiative order.
    /// </summary>
    private void EndActivePCTurn()
    {
        CharacterController pc = ActivePC;
        if (TryBeginMirrorImageSwapSelection(pc))
            return;

        EndAttackSequence();
        EndThrownAttackSequence();
        ResetOffHandTurnState();
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        if (CurrentPhase == TurnPhase.CombatOver) return;

        // Advance to next in initiative order
        NextInitiativeTurn();
    }

    private IEnumerator DelayedEndActivePCTurn(float delay)
    {
        LogMenuFlow("DelayedEndActivePCTurn:START", ActivePC, $"delay={delay}");

        yield return new WaitForSeconds(delay);

        LogMenuFlow("DelayedEndActivePCTurn:AFTER_DELAY", ActivePC, $"delay={delay}");

        if (CurrentPhase == TurnPhase.CombatOver)
            yield break;

        // SAFETY CHECK: if a submenu opened during the delay window, do not force-close it.
        if (CombatUI != null && CombatUI.IsSpecialStyleSelectionMenuOpen())
        {
            LogMenuFlow("DelayedEndActivePCTurn:ABORT_SUBMENU_OPEN", ActivePC, "Submenu open after delay");
            Debug.Log("[GameManager][MenuFlow] DelayedEndActivePCTurn: Submenu open, aborting");
            yield break;
        }

        CharacterController pc = ActivePC;
        if (ShouldAutoEndTurn(pc))
            EndActivePCTurn();
        else
            ShowActionChoices();
    }

}
