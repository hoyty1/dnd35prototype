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

    // ── Wall of Ice Strength-check selection state ──
    private bool _isSelectingBreakWallTarget;

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
        // The scan includes the attacker's own square, melee and ranged alike: a creature sharing it is in melee reach
        // (PHB p.149, CMB-131) and in range of a ranged or thrown weapon, as IsTargetInCurrentWeaponRange already
        // accepts for the AI. The Chebyshev square is a superset of the D&D-distance range; the range test filters.
        List<SquareCell> allCells = GetCellsInChebyshevRange(pc.GridPosition, maxRangeSquares + sizePadding, includeCenter: true);
        bool hasTarget = false;
        bool anyFlanking = false;

        foreach (var cell in allCells)
        {
            // Allow targeting ANY character (enemies, friendlies, neutrals) so players can attack walls, allies
            // (mind control, coup de grace), etc.; an enemy is preferred when the square holds several (CMB-131).
            CharacterController occupant = GetAttackTargetInCell(pc, cell);
            if (occupant != null)
            {
                bool isEnemy = TeamUtility.IsEnemy(pc, occupant);

                // Check whether attacker can flank this target with any ally who actually threatens.
                // Flanking only applies against enemies.
                CharacterController flankPartner;
                bool flanking = isEnemy && CombatUtils.IsAttackerFlanking(pc, occupant, allCombatants, out flankPartner);

                if (flanking)
                {
                    cell.SetHighlight(HighlightType.Flanking);
                    anyFlanking = true;
                }
                else if (!isEnemy)
                {
                    // Friendly/neutral targets get a distinct highlight
                    cell.SetHighlight(HighlightType.AttackRange);
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
        if (_pendingAttackMode != PendingAttackMode.CastSpell && _pendingAttackMode != PendingAttackMode.LayOnHands)
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
            bool hasAnyFriendlyTarget = false;
            foreach (var hc in _highlightedCells)
            {
                if (hc.IsOccupied && hc.Occupant != pc && !hc.Occupant.Stats.IsDead && !TeamUtility.IsEnemy(pc, hc.Occupant))
                {
                    hasAnyFriendlyTarget = true;
                    break;
                }
            }
            string friendlyMsg = hasAnyFriendlyTarget ? " | ⚠ Friendly targets available" : "";
            string modeStr = "";
            switch (_pendingAttackMode)
            {
                case PendingAttackMode.Single: modeStr = "ATTACK"; break;
                case PendingAttackMode.FullAttack: modeStr = "FULL ATTACK"; break;
                case PendingAttackMode.DualWield: modeStr = "DUAL WIELD"; break;
                case PendingAttackMode.CastSpell: modeStr = "CAST SPELL"; break;
                case PendingAttackMode.LayOnHands: modeStr = "LAY ON HANDS"; break;
            }

            if (_pendingAttackMode == PendingAttackMode.Single && AttackIntent.ForUi(this, pc).ThrownSelected)
                modeStr = "THROWN ATTACK";
            else if (_pendingAttackMode == PendingAttackMode.Single && pc.IsUsingFlurryOfBlows)
                modeStr = "FLURRY OF BLOWS";

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
                CombatUI.SetTurnIndicator($"{modeStr}: Click a target to attack!{flankMsg}{wallMsg}{friendlyMsg}{rangeMsg}");
        }
        else
        {
            string noRangeMsg = isRangedWeapon ? "No targets within maximum range!" : "No targets in range!";
            CombatUI.SetTurnIndicator(noRangeMsg);
            StartCoroutine(ReturnToActionChoicesAfterDelay(1.5f));
        }
    }

    /// <summary>
    /// The creature a normal attack by <paramref name="pc"/> on <paramref name="cell"/> targets: a living occupant other
    /// than the attacker that is in range of the current weapon and, for a ranged attack, visible or pinpointed by the
    /// attacker's turn-start Listen check (CharacterController.CanLocateForRangedAttack, DMG p.295; CMB-094); an enemy first
    /// (SquareCell.GetOccupantOtherThan). The attack highlight and the click use this one choice, so a square shared
    /// by an ally and an enemy (a pin release, CMB-089/CMB-131) highlights and attacks the enemy. Null when none.
    /// </summary>
    private CharacterController GetAttackTargetInCell(CharacterController pc, SquareCell cell)
    {
        if (pc == null || cell == null)
            return null;

        bool isRanged = IsAttackModeRanged(pc, pc.GetEquippedMainWeapon());
        return cell.GetOccupantOtherThan(pc, o => o.Stats != null && !o.Stats.IsDead
            && pc.IsTargetInCurrentWeaponRange(o)
            && (!isRanged || pc.CanLocateForRangedAttack(o)));
    }

    private List<CharacterController> GetValidRangedTargets(CharacterController attacker, AttackIntent intent = null)
    {
        var valid = new List<CharacterController>();
        if (attacker == null || !IsAttackModeRanged(attacker, null, intent))
            return valid;

        // Every enemy, not one occupant per square, so a creature sharing a square with another (or with the
        // attacker, CMB-131) is a candidate; the weapon range test below bounds the distance.
        foreach (CharacterController candidate in GetAllCharacters())
        {
            if (candidate == null || candidate == attacker || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!TeamUtility.IsEnemy(attacker, candidate))
                continue;

            if (!attacker.IsTargetInCurrentWeaponRange(candidate))
                continue;

            // Seen, or pinpointed by the attacker's turn-start Listen check (DMG p.295; CMB-094): the attack keeps the
            // target's miss chance.
            if (!attacker.CanLocateForRangedAttack(candidate))
                continue;

            // The direct-attack gate, read only (AttackLegality): the Improved Grab hold bar (MM p.310, CMB-163), the
            // Resilient Sphere (CMB-092) and a known failed Sanctuary or Hide from Undead save (D6). An unrolled ward stays
            // a target: the click rolls the save.
            if (!IsDirectlyAttackableNoReach(attacker, candidate))
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
            if (!TeamUtility.IsEnemy(attacker, candidate))
                continue;

            // No visibility filter: a pinpointed creature is a target, and so (until the believed-square UI, CMB-094) is an
            // unseen one the PC has not pinpointed; the attack's last-known-square auto-miss and miss chance apply.
            if (attacker.IsTargetInCurrentWeaponRange(candidate) && IsDirectlyAttackableNoReach(attacker, candidate))
                valid.Add(candidate);
        }

        return valid;
    }

    /// <summary>
    /// The direct-attack gate without its reach test (the caller runs its own for the attack mode): the hold bar, the
    /// Resilient Sphere and a known failed ward save (AttackLegality.Check; batch 12 step 12). Never rolls.
    /// </summary>
    private static bool IsDirectlyAttackableNoReach(CharacterController attacker, CharacterController target)
        => AttackLegality.Check(attacker, target, DirectAttackKind.Weapon, null, AttackLegalityChecks.Sphere | AttackLegalityChecks.HoldBar | AttackLegalityChecks.Ward).Ok;

    /// <summary>The targets of the attack mode of <paramref name="intent"/> (null: the PC UI's, AttackIntent.ForUi).</summary>
    private List<CharacterController> GetValidTargetsForCurrentWeapon(CharacterController attacker, AttackIntent intent = null)
    {
        if (attacker == null)
            return new List<CharacterController>();

        return IsAttackModeRanged(attacker, null, intent)
            ? GetValidRangedTargets(attacker, intent)
            : GetValidMeleeTargets(attacker);
    }

    /// <summary>Reach or range for the attack mode of <paramref name="intent"/> (null: the PC UI's, AttackIntent.ForUi).</summary>
    private bool IsTargetInCurrentWeaponRange(CharacterController attacker, CharacterController target, AttackIntent intent = null)
    {
        if (attacker == null || target == null || target.Stats == null || target.Stats.IsDead)
            return false;

        if (IsUsingThrownAttackMode(attacker, null, intent))
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
            if (!TeamUtility.IsEnemy(attacker, candidate))
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
        CombatUI?.ShowCombatLog(CombatLogHelper.Info("🎯", $"Select a new {modeLabel} target for {remainingAttacks} remaining attack(s), or right-click/ESC to cancel."));
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

        CombatUI?.ShowCombatLog(CombatLogHelper.Info("↔", $"{prompt} Select a highlighted square for a 5-foot step, or right-click/ESC to skip."));
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
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("↩", $"{pc.Stats.CharacterName} cancels defensive attack declaration."));
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

        if (!_highlightedCells.Contains(cell) || _movementService == null || !_movementService.IsValidMoveDestination(pc, cell.Coords))
            return;

        // The same path rules as the AI's moves (MoveCharacterAlongComputedPath[Withdraw]): the move or
        // withdraw budget the highlight and hover preview use, the swarm pass-through flags, and for a
        // withdraw the start-square exemption (PHB p.143). Only the route preference (avoidThreats) is the
        // PC UI's own (GRID-001).
        int movementRange = _isSelectingWithdraw ? GetWithdrawMoveRangeSquares(pc) : GetCurrentMoveRangeSquares(pc);
        if (movementRange <= 0)
            return;

        var pathResult = _movementService.FindPath(
            pc,
            cell.Coords,
            avoidThreats: true,
            maxRangeOverride: movementRange,
            allowThroughAllies: true,
            allowThroughEnemies: false,
            suppressFirstSquareAoO: _isSelectingWithdraw);

        if (pathResult == null || pathResult.Path == null || pathResult.Path.Count == 0)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", "No valid movement path to that tile."));
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
                        ? $"{pc.Stats.CharacterName} - Withdraw: select destination (double move, leaving your starting space provokes no AoO from enemies you can see)"
                        : $"{pc.Stats.CharacterName} - Click a tile to move (right-click/ESC or own tile to cancel)"));
            }
        });
    }

    // ========================================================================
    // SHARED ATTACK-OF-OPPORTUNITY RESOLUTION (CMB-005, CMB-073, CMB-076)
    // One path for PCs, NPCs, summons and condition-forced movers. The rules
    // themselves (what provokes, what stops movement) live in ThreatSystem.
    // ========================================================================

    /// <summary>What happened while the AoOs for one path step resolved.</summary>
    private sealed class MovementAoOStepOutcome
    {
        public bool StopMovement;
        public bool Incapacitated;
        public string Reason = string.Empty;

        public void Reset()
        {
            StopMovement = false;
            Incapacitated = false;
            Reason = string.Empty;
        }
    }

    private static bool HasAoOAtPathIndex(List<AoOThreatInfo> provokedAoOs, int pathIndex)
    {
        if (provokedAoOs == null)
            return false;

        for (int i = 0; i < provokedAoOs.Count; i++)
        {
            if (provokedAoOs[i] != null && provokedAoOs[i].PathIndex == pathIndex)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves the AoOs the mover provokes by leaving its current square toward
    /// path[pathIndex]. Call it BEFORE the step, while the mover is still in the square
    /// it is leaving (PHB p.137), so the threatener's reach is checked from there.
    /// Sets <paramref name="outcome"/>.StopMovement when an AoO kills, drops, trips or
    /// otherwise stops the mover; the caller must then not take the step. Records each
    /// opponent listed at this step as having had its movement opportunity this round
    /// (ThreatSystem.RecordMovementOpportunity, PHB p.138, CMB-128). Callers: PC moves and
    /// withdraw, NPC, summon and compulsion moves, both charge paths, crawl, the overrun
    /// continuation, the grappler's drag (GrappleSystem.ExecuteGrappleMovement, through
    /// ExecutePathWithMovementAoOs) and the escaper's adjacent move
    /// (GrappleSystem.ExecuteFreeAdjacentGrappleMovement; both CMB-152). The bull rush follow
    /// resolves and records its own steps (ResolveBullRushStepAoOs). Not yet through here, so they
    /// resolve no movement AoOs and record nothing: the PC move-through overrun, its Normal Move
    /// branch and the attacker's follow after a targeted overrun push (CMB-151, CMB-116).
    /// </summary>
    private IEnumerator ResolveMovementAoOsBeforeStep(
        CharacterController mover,
        List<AoOThreatInfo> provokedAoOs,
        int pathIndex,
        string logLabel,
        float secondsAfterEachAoO,
        MovementAoOStepOutcome outcome)
    {
        outcome.Reset();
        if (mover == null || mover.Stats == null || !HasAoOAtPathIndex(provokedAoOs, pathIndex))
            yield break;

        ThreatSystem.MoverAoOSnapshot before = ThreatSystem.CaptureMoverState(mover);

        // The mover leaves these opponents' squares now: each one's movement opportunity against it this
        // round has come, whether or not the AoO is made (PHB p.138, CMB-128). Recorded for the whole step
        // first, so a later step or move this round lists none of them again even if this step stops early.
        for (int i = 0; i < provokedAoOs.Count; i++)
        {
            AoOThreatInfo aooInfo = provokedAoOs[i];
            if (aooInfo != null && aooInfo.PathIndex == pathIndex)
                ThreatSystem.RecordMovementOpportunity(aooInfo.Threatener, mover);
        }

        for (int i = 0; i < provokedAoOs.Count; i++)
        {
            AoOThreatInfo aooInfo = provokedAoOs[i];
            if (aooInfo == null || aooInfo.PathIndex != pathIndex)
                continue;

            CharacterController threatener = aooInfo.Threatener;
            if (threatener == null || threatener.Stats == null || threatener.Stats.IsDead)
                continue;

            CombatResult aooResult = _movementService != null
                ? _movementService.TriggerAoO(threatener, mover)
                : ThreatSystem.ExecuteAoO(threatener, mover, isFromMovement: true);
            if (aooResult == null)
                continue;

            string aooLog = $"{logLabel}: {aooResult.GetDetailedSummary()}";
            CombatUI?.ShowCombatLog(CombatLogHelper.Buff("⚔", aooLog));
            UpdateAllStatsUI();

            if (LogAttacksToConsole)
                Debug.Log("[Combat] " + aooLog);

            if (ThreatSystem.ShouldStopMovementAfterAoO(mover, before, out string reason))
            {
                outcome.StopMovement = true;
                outcome.Reason = reason;
                if (ThreatSystem.IsMoverIncapacitated(mover))
                {
                    // Further AoOs against a dropped mover are moot; stop now.
                    outcome.Incapacitated = true;
                    yield break;
                }
            }

            // A tripped mover still takes the other AoOs it already provoked at this step.
            yield return new WaitForSeconds(secondsAfterEachAoO);
        }
    }

    /// <summary>
    /// Moves a non-interactive mover (NPC, summon, charmed/confused/frightened creature,
    /// AI withdraw) along a precomputed path, resolving each provoked AoO before the step
    /// that provokes it. Path segments without AoOs move in one call, exactly as before.
    /// </summary>
    private IEnumerator ExecutePathWithMovementAoOs(
        CharacterController mover,
        List<Vector2Int> path,
        List<AoOThreatInfo> provokedAoOs,
        float secondsPerStep,
        string aooLogLabel,
        MovementAoOStepOutcome outcome)
    {
        outcome.Reset();
        if (mover == null || mover.Stats == null || path == null || path.Count == 0)
            yield break;

        var stepOutcome = new MovementAoOStepOutcome();
        int segmentStart = 0;
        for (int pathIndex = 0; pathIndex <= path.Count; pathIndex++)
        {
            bool atEnd = pathIndex == path.Count;
            if (!atEnd && !HasAoOAtPathIndex(provokedAoOs, pathIndex))
                continue;

            // Walk the AoO-free segment that ends where the next AoO is provoked. A segment
            // that pauses mid-path may end in an ally's square the move only passes through,
            // so only the true end of the path is treated as the destination.
            if (pathIndex > segmentStart)
            {
                List<Vector2Int> segment = path.GetRange(segmentStart, pathIndex - segmentStart);
                if (_movementService != null)
                    yield return StartCoroutine(_movementService.ExecuteMovement(mover, segment, secondsPerStep, markAsMoved: true, lastStepIsDestination: atEnd));
                else
                    yield return StartCoroutine(mover.MoveAlongPath(segment, secondsPerStep, markAsMoved: true, lastStepIsDestination: atEnd));

                if (ThreatSystem.IsMoverIncapacitated(mover))
                {
                    outcome.StopMovement = true;
                    outcome.Incapacitated = true;
                    outcome.Reason = "incapacitated";
                    yield break;
                }

                // Blocked part-way (MoveAlongPath stops at an untraversable node): the
                // squares that would provoke later were never reached.
                if (mover.GridPosition != segment[segment.Count - 1])
                {
                    outcome.StopMovement = true;
                    outcome.Reason = "blocked";
                    yield break;
                }
            }

            if (atEnd)
                yield break;

            segmentStart = pathIndex;
            yield return StartCoroutine(ResolveMovementAoOsBeforeStep(mover, provokedAoOs, pathIndex, aooLogLabel, 0.5f, stepOutcome));
            if (stepOutcome.StopMovement)
            {
                outcome.StopMovement = true;
                outcome.Incapacitated = stepOutcome.Incapacitated;
                outcome.Reason = stepOutcome.Reason;
                CombatUI?.ShowCombatLog(CombatLogHelper.CriticalFailure("⛔",
                    $"{mover.Stats.CharacterName}'s movement stops ({stepOutcome.Reason}) after an attack of opportunity."));
                yield break;
            }
        }
    }

    private enum ManeuverAoOOutcome
    {
        Proceed,
        Disrupted,
        AttackerIncapacitated
    }

    /// <summary>
    /// Resolves the AoOs a maneuver provokes when it starts, for PCs and NPCs alike
    /// (rules in <see cref="ThreatSystem.GetManeuverAoOProvokers"/>). The caller has already
    /// spent the action; on anything but Proceed the attempt is lost. A bull rush's initiation AoO
    /// is its own provocation (PHB p.154, owner decision 2026-10-07), so an enemy that had a movement
    /// AoO earlier this round (a charge bull rush) may still take it. Following from that decision
    /// (PHB p.138), it also does not count as the enemy's movement opportunity when the attacker
    /// follows the pushed defender.
    /// </summary>
    private ManeuverAoOOutcome ResolveManeuverInitiationAoOs(
        CharacterController attacker,
        CharacterController target,
        SpecialAttackType type)
    {
        if (attacker == null || attacker.Stats == null)
            return ManeuverAoOOutcome.AttackerIncapacitated;

        List<CharacterController> provokers = ThreatSystem.GetManeuverAoOProvokers(attacker, target, type, GetAllCharacters());
        string maneuverLabel = ThreatSystem.GetManeuverAoOLabel(type);
        bool isBullRush = type == SpecialAttackType.BullRushAttack || type == SpecialAttackType.BullRushCharge;

        for (int i = 0; i < provokers.Count; i++)
        {
            // PHB p.154: during a bull rush an AoO against the attacker by anyone but the defender
            // may strike the defender instead.
            int targetHpBefore = target != null && target.Stats != null ? target.Stats.CurrentHP : 0;
            CombatResult maneuverAoO = isBullRush && provokers[i] != target
                ? ResolveBullRushAoO(provokers[i], attacker, target, isFromMovement: false, context: $"{maneuverLabel} initiation")
                : ThreatSystem.ExecuteAoO(provokers[i], attacker, trigger: "maneuver");
            if (maneuverAoO == null)
                continue;

            if (!isBullRush || provokers[i] == target)
                CombatUI?.ShowCombatLog(CombatLogHelper.Buff("⚔", $"{maneuverLabel} initiation AoO: {maneuverAoO.GetDetailedSummary()}"));
            UpdateAllStatsUI();

            if (attacker.IsDead || attacker.Stats.IsDead || attacker.IsUnconscious)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Death("💀", $"{attacker.Stats.CharacterName} is incapacitated while attempting to start {maneuverLabel.ToLowerInvariant()}."));
                return ManeuverAoOOutcome.AttackerIncapacitated;
            }

            // A strayed AoO can kill the defender; there is then nothing to push (CORE-011: check victory).
            if (isBullRush && target != null && target.Stats != null && (target.IsDead || target.Stats.IsDead))
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{target.Stats.CharacterName} falls before the bull rush lands."));
                CheckCombatVictory("BullRush.InitiationAoO", target);
                return ManeuverAoOOutcome.Disrupted;
            }

            // A strayed AoO that leaves the defender dying or unconscious may end the combat (CORE-011);
            // otherwise the bull rush goes on against it: only the defender's death stops a bull rush, as
            // during the push (owner decision 2026-10-07).
            if (isBullRush && target != null && target.Stats != null && targetHpBefore > 0 && target.Stats.CurrentHP <= 0
                && CheckCombatVictory("BullRush.InitiationAoO", target))
                return ManeuverAoOOutcome.Disrupted;

            if (ThreatSystem.DoesManeuverAoODisruptAttempt(type, maneuverAoO))
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"{maneuverLabel} attempt disrupted by attack of opportunity"));
                return ManeuverAoOOutcome.Disrupted;
            }
        }

        return ManeuverAoOOutcome.Proceed;
    }

    /// <summary>
    /// Test hook, null in play: when set, returns the d100 for <see cref="ResolveBullRushAoO"/>
    /// instead of rolling, so tests can force or prevent misdirection. Behaviour-neutral.
    /// </summary>
    internal static Func<int> BullRushMisdirectionRollOverride;

    /// <summary>The d100 context of the bull rush's 25% chance that an attack of opportunity strikes the other creature (DiceService; CORE-030).</summary>
    public const string BullRushMisdirectionContext = "Bull rush misdirection d100";

    /// <summary>
    /// One AoO during a bull rush (PHB p.154): <paramref name="provoker"/> attacks
    /// <paramref name="intended"/>, but when the provoker is not <paramref name="other"/> (the other
    /// participant), a d100 roll of 1-25 makes the attack strike <paramref name="other"/> instead.
    /// Shared by the initiation AoOs (intended = attacker, other = defender) and the push and follow
    /// steps (ExecuteBullRushMovement). Mobility applies only to an AoO that hits the mover itself.
    /// Returns the AoO result (its Defender is whoever was attacked), or null when none was made.
    /// </summary>
    private CombatResult ResolveBullRushAoO(
        CharacterController provoker,
        CharacterController intended,
        CharacterController other,
        bool isFromMovement = false,
        string context = "Bull Rush")
    {
        if (provoker == null || intended == null || intended.Stats == null)
            return null;

        CharacterController victim = intended;
        int roll = 0;
        bool canStray = other != null && other != provoker && other != intended
            && other.Stats != null && !other.IsDead && !other.Stats.IsDead;
        if (canStray)
        {
            roll = BullRushMisdirectionRollOverride != null ? BullRushMisdirectionRollOverride() : DiceService.Percentile(BullRushMisdirectionContext); // CORE-030
            if (roll <= 25)
                victim = other;
        }

        bool strays = victim != intended;
        CombatResult result = ThreatSystem.ExecuteAoO(provoker, victim, isFromMovement && !strays,
            trigger: isFromMovement ? "bullrush-move" : "maneuver");
        if (result == null)
            return null;

        if (strays)
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠",
                $"{provoker.Stats.CharacterName}'s attack of opportunity at {intended.Stats.CharacterName} strays into {other.Stats.CharacterName} (d100 {roll}, 1-25 misdirects during a bull rush)."));
        CombatUI?.ShowCombatLog(CombatLogHelper.Buff("⚔", $"{context} AoO: {result.GetDetailedSummary()}"));
        return result;
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
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{pc.Stats.CharacterName} cannot complete Withdraw without a full-round action."));
                ShowActionChoices();
                yield break;
            }

            pc.IsWithdrawing = true;
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
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{pc.Stats.CharacterName} has no action available for movement."));
            ShowActionChoices();
            yield break;
        }

        bool interruptedByIncapacitation = false;
        bool interruptedByGreaseSlip = false;
        string stoppedByAoOReason = null;
        int movementBudgetSquares = isWithdraw ? GetWithdrawMoveRangeSquares(pc) : GetCurrentMoveRangeSquares(pc);
        int movementCostConsumed = 0;
        Vector2Int previousCell = pc.GridPosition;
        var aooOutcome = new MovementAoOStepOutcome();

        for (int pathIndex = 0; pathIndex < path.Count; pathIndex++)
        {
            Vector2Int step = path[pathIndex];
            int stepCost = 1 + GetGreaseAreaExtraMovementCost(pc, step);
            if (movementCostConsumed + stepCost > movementBudgetSquares)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("🛢", $"{pc.Stats.CharacterName} cannot move farther this action (grease slows movement)."));
                break;
            }

            // AoOs for leaving the current square resolve before the step (PHB p.137, CMB-005).
            yield return StartCoroutine(ResolveMovementAoOsBeforeStep(pc, provokedAoOs, pathIndex, "AoO", 1.0f, aooOutcome));
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
            // a legal intermediate step (PHB p.148), as in ExecutePathWithMovementAoOs for the AI movers.
            bool stepIsDestination = pathIndex == path.Count - 1;
            if (_movementService != null)
                yield return StartCoroutine(_movementService.ExecuteMovement(pc, stepPath, PlayerMoveSecondsPerStep, markAsMoved: false, lastStepIsDestination: stepIsDestination));
            else
                yield return StartCoroutine(pc.MoveAlongPath(stepPath, PlayerMoveSecondsPerStep, markAsMoved: false, lastStepIsDestination: stepIsDestination));

            // Blocked (MoveAlongPath refused the square): stop here rather than jump to the next square.
            if (pc.GridPosition != step)
            {
                if (!interruptedByIncapacitation && pc.Stats != null && ThreatSystem.IsMoverIncapacitated(pc))
                    interruptedByIncapacitation = true;
                break;
            }

            movementCostConsumed += stepCost;

            // ── Death/disable check after area effect damage during movement step ──
            // Area effects (Wall of Fire, etc.) may deal damage when a creature enters
            // their cells via UpdateCharacterTracking() in Unity's Update loop.
            // The MoveAlongPath coroutine already breaks on death, but we need to
            // set the interruptedByIncapacitation flag here for proper PC turn handling.
            // A disabled PC (0 hp) keeps moving (PHB p.145, batch 12 D5); dead, dying or unconscious stops.
            if (!interruptedByIncapacitation && pc.Stats != null && ThreatSystem.IsMoverIncapacitated(pc))
            {
                Debug.Log($"🔥 [PCMovement] {pc.Stats.CharacterName} dropped by area damage during movement step {pathIndex + 1} (HP={pc.Stats.CurrentHP})");
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

        if (isWithdraw && !interruptedByIncapacitation)
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("↩", $"{pc.Stats.CharacterName} completes Withdraw."));

        InvalidatePreviewThreats();

        if (interruptedByIncapacitation)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.CriticalFailure("⛔", $"{pc.Stats.CharacterName}'s movement stops immediately due to incapacitation."));

            if (EvaluateCombatEnd("PCMovement.Incapacitated"))
                yield break;

            EndActivePCTurn();
            yield break;
        }

        if (interruptedByGreaseSlip)
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("🛢", $"{pc.Stats.CharacterName}'s movement ends after slipping in grease."));

        if (!string.IsNullOrEmpty(stoppedByAoOReason))
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⛔", $"{pc.Stats.CharacterName}'s movement stops ({stoppedByAoOReason}) after an attack of opportunity."));

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
        if (maxRange <= 0)
            yield break; // prone, entangled in a web or otherwise unable to move (CMB-074)

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

        // Leaving the starting space (every square the mover starts in, for the whole withdraw) is already exempt in
        // ProvokedAoOs from the enemies the mover can see (the startSpace rule in ThreatSystem.AnalyzePathForAoOs,
        // suppressFirstSquareAoO, CharacterController.CanVisuallyPerceive, PHB p.143, DMG p.29, GRID-021); the rest resolve
        // before the step that provokes them, like any other movement.
        List<AoOThreatInfo> provokedAoOs = pathResult != null ? pathResult.ProvokedAoOs : null;
        var moveOutcome = new MovementAoOStepOutcome();
        yield return StartCoroutine(ExecutePathWithMovementAoOs(mover, path, provokedAoOs, secondsPerStep, "AoO (Withdraw)", moveOutcome));

        CheckTurnUndeadProximityBreakingForMover(mover);
        PruneTurnUndeadTrackers();
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

        // Ordinary movement provokes for every mover (PHB p.137, CMB-073). Callers re-check
        // the mover's HP after this coroutine, because an AoO can drop it mid-path.
        var moveOutcome = new MovementAoOStepOutcome();
        yield return StartCoroutine(ExecutePathWithMovementAoOs(mover, path, pathResult != null ? pathResult.ProvokedAoOs : null, secondsPerStep, "AoO", moveOutcome));

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
                    CombatUI.ShowCombatLog(CombatLogHelper.Summon("", "Choose a highlighted empty tile in range for the summon."));
                    return;
                }

                if (cell.IsOccupied)
                {
                    CombatUI.ShowCombatLog(CombatLogHelper.Summon("", "Choose an empty tile to place your summon."));
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
                    CombatUI.ShowCombatLog(CombatLogHelper.Info("", "Choose a highlighted tile in range to place the swarm."));
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
                    CombatUI.ShowCombatLog(CombatLogHelper.Failure("", $"{_pendingSpell.Name} cannot target self right now."));
                }
                return;
            }

            // Cancel if clicking non-highlighted cell.
            if (!_highlightedCells.Contains(cell))
            {
                Debug.Log($"[CombatActions] Spell targeting CANCELLED – cell {cell?.Coords} not in highlighted set (count={_highlightedCells.Count})");
                EndPendingCast();
                CleanupTestPanelCast();
                ShowActionChoices();
                return;
            }

            // Valid target click
            if (cell.IsOccupied && !cell.Occupant.Stats.IsDead)
            {
                if (IsPendingGreaseObjectCast())
                {
                    PerformGreaseCast(pc, cell.Occupant, null, null);
                    return;
                }

                if (IsPendingGreaseArmorCast())
                {
                    PerformGreaseCast(pc, cell.Occupant, null, null);
                    return;
                }

                PerformSpellCast(pc, cell.Occupant);
                return;
            }

            // Fallback cancel
            EndPendingCast();
            ShowActionChoices();
            return;
        }

        if (_pendingAttackMode == PendingAttackMode.LayOnHands)
        {
            HandleLayOnHandsTargetClick(pc, cell);
            return;
        }

        // ===== NORMAL ATTACK MODE =====
        if (_isAwaitingRangedRetargetSelection)
        {
            CharacterController retarget = GetAttackTargetInCell(pc, cell); // as highlighted (CMB-131)
            Legality retargetLegality = retarget != null
                ? AttackLegality.Check(pc, retarget, DirectAttackKind.Weapon, null, AttackLegalityChecks.Sphere | AttackLegalityChecks.HoldBar | AttackLegalityChecks.Ward)
                : Legality.Allowed();
            if (!retargetLegality.Ok)
            {
                // The direct-attack gate: the Improved Grab hold bar (MM p.310; delegated RAW reading 2026-10-14, CMB-163),
                // the Resilient Sphere (CMB-092), a known failed ward save (D6).
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", retargetLegality.Reason));
                return;
            }

            if (retarget != null && retarget.Stats != null && !retarget.Stats.IsDead
                && _highlightedCells.Contains(cell))
            {
                _selectedRangedRetarget = retarget;
                _isAwaitingRangedRetargetSelection = false;
                return;
            }

            CombatUI?.ShowCombatLog(CombatLogHelper.Info("", "Select a highlighted valid target, or right-click/ESC to cancel remaining attacks."));
            return;
        }

        if (_isSelectingOffHandTarget)
        {
            HandleOffHandTargetClick(pc, cell);
            return;
        }

        // ── WALL OF ICE STRENGTH CHECK ──
        // When _isSelectingBreakWallTarget, the player clicked a cell to attempt a STR break.
        if (_isSelectingBreakWallTarget)
        {
            if (_highlightedCells.Contains(cell))
            {
                WallOfIceAreaEffect wall = WallOfIceAreaEffect.GetWallAtCell(cell.Coords);
                if (wall != null)
                {
                    _isSelectingBreakWallTarget = false;
                    PerformStrengthCheckOnWall(pc, wall, cell.Coords);
                    return;
                }
            }
            // Clicked a non-wall cell — cancel and return to action choices
            _isSelectingBreakWallTarget = false;
            Grid.ClearAllHighlights();
            ShowActionChoices();
            return;
        }

        // The target ShowAttackTargets highlighted in this square: a click on the attacker's own square picks a creature
        // sharing it, and a square shared by an ally and an enemy gives the enemy (PHB p.149, CMB-131).
        CharacterController clicked = GetAttackTargetInCell(pc, cell) ?? cell.GetOccupantOtherThan(pc);
        if (clicked == null || clicked.Stats == null || clicked.Stats.IsDead)
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


        if (clicked != null && clicked.Stats != null && !clicked.Stats.IsDead && _highlightedCells.Contains(cell))
        {
            // A creature holding with one body part attacks only other opponents (MM p.310, CMB-149), nothing passes a
            // Resilient Sphere (CMB-092), and a known failed ward save bars the target (D6): the shared gate.
            Legality clickLegality = AttackLegality.Check(pc, clicked, DirectAttackKind.Weapon, null, AttackLegalityChecks.Sphere | AttackLegalityChecks.HoldBar | AttackLegalityChecks.Ward);
            if (!clickLegality.Ok)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", clickLegality.Reason));
                return;
            }

            // Allow attacking any highlighted target (enemies, friendlies, neutrals).
            // This supports attacking mind-controlled allies, neutral creatures, etc.
            PerformPlayerAttack(pc, clicked);
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

        // As ShowOffHandAttackTargets highlights: an enemy (own square: the square-mate, CMB-131).
        CharacterController clicked = cell.GetOccupantOtherThan(attacker, o => TeamUtility.IsEnemy(attacker, o)) ?? cell.GetOccupantOtherThan(attacker);
        if (clicked == null || clicked.Stats == null || clicked.Stats.IsDead || !_highlightedCells.Contains(cell))
        {
            Debug.Log($"[OffHand] Invalid target click. occupied={cell.IsOccupied} occupant={(clicked != null && clicked.Stats != null ? clicked.Stats.CharacterName : "none")} highlighted={_highlightedCells.Contains(cell)}");
            _isSelectingOffHandTarget = false;
            _isSelectingOffHandThrownTarget = false;
            _currentOffHandBAB = 0;
            _currentOffHandWeapon = null;
            ShowActionChoices();
            return;
        }

        // A creature holding with one body part attacks only other opponents, with its off hand too (MM p.310; delegated
        // RAW reading 2026-10-14, CMB-163); the selection stays open for another target.
        // The same gate also refuses a target across a Resilient Sphere (CMB-092) or behind a ward this attacker failed
        // to overcome (D6), before any cost.
        Legality offHandLegality = AttackLegality.Check(attacker, clicked, DirectAttackKind.Weapon, null, AttackLegalityChecks.Sphere | AttackLegalityChecks.HoldBar | AttackLegalityChecks.Ward);
        if (!offHandLegality.Ok)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", offHandLegality.Reason));
            return;
        }

        CharacterController target = clicked;
        ItemData offHandWeapon = _currentOffHandWeapon;
        if (offHandWeapon == null)
        {
            Debug.Log("[OffHand] Early return: current off-hand weapon is null.");
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attacker.Stats.CharacterName} has no off-hand weapon available."));
            _isSelectingOffHandTarget = false;
            _isSelectingOffHandThrownTarget = false;
            _currentOffHandBAB = 0;
            _currentOffHandWeapon = null;
            ShowActionChoices();
            return;
        }

        bool useThrownRange = _isSelectingOffHandThrownTarget
            || (offHandWeapon.IsThrown && AttackIntent.ForUi(this, attacker).ThrownSelected);

        Debug.Log($"[OffHand] HandleOffHandTargetClick attacker={attacker.Stats.CharacterName} target={target.Stats.CharacterName} mode={(useThrownRange ? "Thrown" : "Melee")} weapon={offHandWeapon.Name} BAB={_currentOffHandBAB}");

        // The off-hand attack is one attack of the creature's sequence: the first attack of the turn
        // pays the standard action, a later one the move action (full attack, PHB p.143).
        if (!attacker.TryPayForNextAttack(out string offHandPayReason, out bool offHandEnteredFullAttack))
        {
            string modeLabel = useThrownRange ? "an off-hand thrown attack" : "an off-hand attack";
            Debug.Log($"[OffHand] Early return: cannot pay for {modeLabel}: {offHandPayReason}");
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attacker.Stats.CharacterName} cannot make {modeLabel}: {offHandPayReason}."));
            _isSelectingOffHandTarget = false;
            _isSelectingOffHandThrownTarget = false;
            _currentOffHandBAB = 0;
            _currentOffHandWeapon = null;
            ShowActionChoices();
            return;
        }

        if (offHandEnteredFullAttack)
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("↻", $"{attacker.Stats.CharacterName} commits to a full attack and spends their move action."));

        CurrentSubPhase = PlayerSubPhase.Animating;

        // Sanctuary and Hide from Undead (PHB p.274, p.241; D6): the attempt rolls the attacker's one save; a failure loses
        // this off-hand attack (paid above).
        if (AttackLegality.CommitWard(attacker, target, DirectAttackKind.Weapon) == WardVerdict.Failed)
        {
            attacker.RegisterAttackMade(AttackStepKind.OffHand);
            _offHandAttackUsedThisTurn = true;
            _offHandAttackAvailableThisTurn = attacker.HasOffHandWeaponEquipped();
            _isSelectingOffHandTarget = false;
            _isSelectingOffHandThrownTarget = false;
            UpdateAllStatsUI();
            StartCoroutine(AfterAttackDelay(attacker, 1.0f));
            return;
        }

        Debug.Log("[OffHand] Calling ExecuteOffHandAttack...");
        CombatResult result = ExecuteOffHandAttack(attacker, target, _currentOffHandBAB, offHandWeapon, useThrownRange);
        Debug.Log("[OffHand] ExecuteOffHandAttack returned.");

        if (result == null)
        {
            FinishOffHandAttack(attacker, useThrownRange);
            return;
        }

        attacker.RegisterAttackMade(AttackStepKind.OffHand);

        // The one post-attack hook (origin E): the thrown drop and the next draw into the off hand, Concentration, melee
        // reactions, the free trip, Improved Grab (the attacker's decider), the summon cleanup and the combat-end check.
        AttackIntent offHandIntent = AttackIntent.ForUi(this, attacker);
        offHandIntent.Hand = AttackStepKind.OffHand;
        offHandIntent.Weapon = offHandWeapon;
        offHandIntent.Mode = useThrownRange ? AttackMode.Thrown : AttackMode.Melee;
        AttackResolution offHandResolution = AttackResolution.Create(AttackOrigin.PcOffHand, attacker, target, offHandIntent).With(result);
        offHandResolution.CombatEndContext = "ResolveOffHandAttack";
        var offHandOutcome = new PostAttackOutcome();
        bool waiting = RunAttackResolved(offHandResolution, offHandOutcome, () =>
        {
            if (!offHandOutcome.CombatEnded)
                FinishOffHandAttack(attacker, useThrownRange);
        });
        if (waiting)
            return;
        if (offHandOutcome.CombatEnded)
        {
            _offHandAttackUsedThisTurn = true;
            _isSelectingOffHandTarget = false;
            _isSelectingOffHandThrownTarget = false;
            return;
        }

        FinishOffHandAttack(attacker, useThrownRange);
    }

    /// <summary>The off-hand attack's end: the UI's off-hand fields (read back from the inventory after a throw) and the after-attack delay.</summary>
    private void FinishOffHandAttack(CharacterController attacker, bool useThrownRange)
    {
        if (useThrownRange)
            _currentOffHandWeapon = attacker.GetOffHandAttackWeapon();

        _offHandAttackUsedThisTurn = true;
        _offHandAttackAvailableThisTurn = attacker.HasOffHandWeaponEquipped();
        _isSelectingOffHandTarget = false;
        _isSelectingOffHandThrownTarget = false;

        Debug.Log($"[Attack][OffHand] Off-hand attack resolved. inSequence={_isInAttackSequence} mainAttacksUsed={attacker.ProgressiveAttackPool.MainHandStepsUsed}/{attacker.ProgressiveAttackPool.MainHandBudget} thrown={useThrownRange} offHandAvailable={_offHandAttackAvailableThisTurn}");

        StartCoroutine(AfterAttackDelay(attacker, 1.2f));
    }

    private CombatResult ExecuteOffHandAttack(CharacterController attacker, CharacterController target, int attackBab, ItemData offHandWeapon, bool useThrownRange)
    {
        if (_combatFlowService != null)
            return _combatFlowService.ExecuteOffHandAttack(attacker, target, attackBab, offHandWeapon, useThrownRange);

        return null;
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

        int maxRange = (type == SpecialAttackType.Feint || type == SpecialAttackType.CoupDeGrace || type == SpecialAttackType.BullRushAttack)
            ? 1
            : attacker.GetMeleeMaxAttackDistance();
        if (maxRange < 1) maxRange = 1;

        int sizePadding = Mathf.Max(0, attacker.GetVisualSquaresOccupied() - 1);
        // The attacker's own square is included: a creature sharing it is in reach (PHB p.149, CMB-131).
        List<SquareCell> allCells = GetCellsInChebyshevRange(attacker.GridPosition, maxRange + sizePadding, includeCenter: true);
        bool hasTarget = false;
        bool hasRefusedBullRushTarget = false;
        bool hasRefusedTripTarget = false;
        bool hasHoldBarredTarget = false;

        // Drawn first so that a target sharing the attacker's square keeps its target highlight.
        HighlightCharacterFootprint(attacker, HighlightType.Selected);

        foreach (var c in allCells)
        {
            // An enemy first, so a square shared by an ally and an enemy offers the enemy (CMB-131).
            CharacterController occupant = c.GetOccupantOtherThan(attacker);
            if (occupant == null || occupant.Stats == null || occupant.Stats.IsDead) continue;
            if (!TeamUtility.IsEnemy(attacker, occupant)) continue;

            int distance = attacker.GetMinimumDistanceToTarget(occupant, chebyshev: true);
            // Feint and coup de grace need a foe adjacent or in the attacker's own square (PHB p.149, CMB-131); coup de
            // grace through the one test the button, the AI and the resolver share. Bull rush enters the defender's
            // space, so it needs an adjacent target (PHB p.154; a square-mate is refused, owner interpretation 2026-10-11, CMB-154).
            bool inRange = type == SpecialAttackType.CoupDeGrace
                ? attacker.IsInCoupDeGraceReach(occupant)
                : type == SpecialAttackType.Feint
                ? distance <= 1
                : type == SpecialAttackType.BullRushAttack
                    ? distance == 1
                    // Trip and grapple are unarmed melee touch attacks, so natural reach whatever the weapon (a longspear
                    // trips an adjacent foe), and a trip weapon trips at its own reach (PHB p.115-122); disarm and sunder
                    // the reach of the weapon in the hand chosen: the shared maneuver reach rule (AttackLegality, CMB-104).
                    // Trip legality itself grays the target below.
                    : type == SpecialAttackType.Trip
                        ? AttackLegality.IsInTripReach(attacker, distance)
                        : type == SpecialAttackType.Grapple
                            ? distance <= AttackLegality.UnarmedReachSquares(attacker)
                            : AttackLegality.IsInManeuverReach(attacker, occupant, type, PcManeuverIntent(attacker, type), out _);

            if (type == SpecialAttackType.Overrun)
            {
                if (!IsValidOverrunTarget(attacker, occupant, out _, requireAdjacency: true))
                    continue;

                inRange = distance == 1;
            }

            if (!inRange)
                continue;

            // A creature holding this target with one body part may not trip, disarm, sunder or coup de grace it (MM
            // p.310; delegated RAW reading 2026-10-14, CMB-163): shown gray, and the click is refused with the reason. It
            // does not count as a target, so a holder whose only foe in reach is the held creature gets "No targets".
            if (attacker.IsBarredFromSpecialAttackOn(type, occupant, out _))
            {
                c.SetHighlight(HighlightType.AttackDeadZone);
                _highlightedCells.Add(c);
                hasHoldBarredTarget = true;
                continue;
            }

            // Nothing passes a Resilient Sphere (CMB-092), and a foe whose ward this attacker failed to overcome cannot be
            // attacked (D6): not a target (read only; an unrolled ward stays a target and the click rolls its save).
            if (IsDirectAttackManeuverForGate(type)
                && !AttackLegality.Check(attacker, occupant, DirectAttackKind.Maneuver, AttackIntent.ForManeuver(type),
                    AttackLegalityChecks.Sphere | AttackLegalityChecks.Ward).Ok)
                continue;

            if (type == SpecialAttackType.Disarm)
            {
                bool hasDisarmableWeapon = occupant.HasDisarmableWeaponEquipped();
                c.SetHighlight(hasDisarmableWeapon ? HighlightType.Attack : HighlightType.AttackDeadZone);
                _highlightedCells.Add(c);
                hasTarget = true;
                continue;
            }

            if (type == SpecialAttackType.BullRushAttack)
            {
                // Shared legality (size, swarm, incorporeal, grappling): gray targets are refused.
                bool canBullRush = attacker.CanBullRush(occupant, false, out _);
                if (!canBullRush)
                    hasRefusedBullRushTarget = true;
                c.SetHighlight(canBullRush ? HighlightType.Attack : HighlightType.AttackDeadZone);
                _highlightedCells.Add(c);
                hasTarget = true;
                continue;
            }

            if (type == SpecialAttackType.Trip)
            {
                // Shared trip legality (size, swarm, incorporeal; PHB p.158): gray targets are refused.
                bool canTrip = attacker.CanTrip(occupant, out _);
                if (!canTrip)
                    hasRefusedTripTarget = true;
                c.SetHighlight(canTrip ? HighlightType.Attack : HighlightType.AttackDeadZone);
                _highlightedCells.Add(c);
                hasTarget = true;
                continue;
            }

            if (type == SpecialAttackType.Sunder)
            {
                bool hasSunderableItem = occupant.HasSunderableItemEquipped();
                c.SetHighlight(hasSunderableItem ? HighlightType.Attack : HighlightType.AttackDeadZone);
                _highlightedCells.Add(c);
                hasTarget = true;
                continue;
            }

            if (type == SpecialAttackType.CoupDeGrace)
            {
                bool helplessTarget = occupant.IsHelplessForCoupDeGrace() && !occupant.IsImmuneToCriticalHits();
                Debug.Log($"[Targeting][CoupDeGrace] candidate={occupant.Stats.CharacterName} hp={occupant.Stats.CurrentHP} dead={occupant.Stats.IsDead} unconscious={occupant.Stats.IsUnconscious} helpless={helplessTarget}");
                c.SetHighlight(helplessTarget ? HighlightType.Attack : HighlightType.AttackDeadZone);
                _highlightedCells.Add(c);
                hasTarget = true;
                continue;
            }

            c.SetHighlight(HighlightType.Attack);
            _highlightedCells.Add(c);
            hasTarget = true;
        }

        if (hasTarget)
        {
            string indicator;
            if (type == SpecialAttackType.Disarm)
                indicator = "SPECIAL: Disarm - red targets are valid, gray targets have no disarmable weapon (Right-click/Esc to cancel)";
            else if (type == SpecialAttackType.Sunder)
                indicator = "SPECIAL: Sunder - red targets are valid, gray targets have no sunderable item (Right-click/Esc to cancel)";
            else if (type == SpecialAttackType.BullRushAttack)
                indicator = hasRefusedBullRushTarget
                    ? "SPECIAL: Bull Rush (standard action) - red targets are valid; gray targets are too large, incorporeal or swarms (Right-click/Esc to cancel)"
                    : "SPECIAL: Bull Rush (standard action) - select an adjacent target (Right-click/Esc to cancel)";
            else if (type == SpecialAttackType.Trip && hasRefusedTripTarget)
                indicator = "SPECIAL: Trip - red targets are valid; gray targets are too large, incorporeal or swarms (Right-click/Esc to cancel)";
            else if (type == SpecialAttackType.CoupDeGrace)
                indicator = "SPECIAL: Coup de Grace - red targets are helpless and vulnerable to critical hits (Right-click/Esc to cancel)";
            else
                indicator = $"SPECIAL: {type} - select target (Right-click/Esc to cancel)";

            if (hasHoldBarredTarget)
                indicator += " Gray also marks the creature you hold, which you may not attack (MM p.310).";
            CombatUI.SetTurnIndicator(indicator);
        }
        else
        {
            CombatUI.SetTurnIndicator(hasHoldBarredTarget
                ? $"No targets in range for {type}: the only one is the creature you hold (MM p.310)."
                : $"No targets in range for {type}.");
            StartCoroutine(ReturnToActionChoicesAfterDelay(1.0f));
        }
    }

    private void HandleSpecialAttackTargetClick(CharacterController attacker, SquareCell cell)
    {
        // A click on the attacker's own square picks a creature sharing it, an enemy first, as highlighted (PHB p.149, CMB-131).
        CharacterController target = cell.GetOccupantOtherThan(attacker);
        if (!_highlightedCells.Contains(cell) || target == null)
        {
            ShowActionChoices();
            return;
        }

        // The Improved Grab hold bar (MM p.310; delegated RAW reading 2026-10-14, CMB-163), before any menu or cost;
        // the selection stays open for another target.
        if (attacker.IsBarredFromSpecialAttackOn(_pendingSpecialAttackType, target, out string specialHoldReason))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", specialHoldReason));
            return;
        }

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

        // The Improved Grab hold bar (MM p.310; delegated RAW reading 2026-10-14, CMB-163), before any cost: this entry
        // is also reached after the disarm and sunder item menus.
        if (attacker.IsBarredFromSpecialAttackOn(type, target, out string holdReason))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", holdReason));
            Grid.ClearAllHighlights();
            _highlightedCells.Clear();
            _isSelectingSpecialAttack = false;
            ShowActionChoices();
            return;
        }

        // Shared bull rush legality (CMB-102) before any side effect, so a refused (gray) target
        // costs nothing and does not break turning, as on the NPC executor.
        if (type == SpecialAttackType.BullRushAttack && !attacker.CanBullRush(target, false, out string bullRushReason))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attacker.Stats.CharacterName} cannot bull rush {target.Stats.CharacterName}: {bullRushReason}."));
            Grid.ClearAllHighlights();
            _highlightedCells.Clear();
            _isSelectingSpecialAttack = false;
            ShowActionChoices();
            return;
        }

        // Shared trip legality (size, swarm, incorporeal; PHB p.158, CMB-079), also before any cost.
        if (type == SpecialAttackType.Trip && !attacker.CanTrip(target, out string tripReason))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attacker.Stats.CharacterName} cannot trip {target.Stats.CharacterName}: {tripReason}."));
            Grid.ClearAllHighlights();
            _highlightedCells.Clear();
            _isSelectingSpecialAttack = false;
            ShowActionChoices();
            return;
        }

        // The one direct-attack gate for maneuvers, shared with the NPC executor (AttackLegality, batch 12 step 12): the
        // Resilient Sphere (CMB-092), the maneuver's reach (CMB-104: trip and grapple at natural reach, disarm and sunder
        // at the weapon's) and a known failed ward save (D6), before any cost.
        bool gatedManeuver = IsDirectAttackManeuverForGate(type);
        if (gatedManeuver)
        {
            Legality maneuverLegality = AttackLegality.CanDirectlyAttack(attacker, target, DirectAttackKind.Maneuver, PcManeuverIntent(attacker, type));
            if (!maneuverLegality.Ok)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attacker.Stats.CharacterName} cannot use {type} on {target.Stats.CharacterName}: {maneuverLegality.Reason}."));
                Grid.ClearAllHighlights();
                _highlightedCells.Clear();
                _isSelectingSpecialAttack = false;
                if (type == SpecialAttackType.Sunder) { _pendingSunderUseOffHandSelection = false; ClearSunderSequenceState(); }
                else if (type == SpecialAttackType.Disarm) { _pendingDisarmUseOffHandSelection = false; ClearDisarmSequenceState(); }
                ShowActionChoices();
                return;
            }
        }

        CurrentSubPhase = PlayerSubPhase.Animating;

        string actionLabel = "standard action";
        int? disarmAttackBonusOverride = null;
        int disarmAttackBonusUsed = 0;
        bool disarmUsedOffHand = false;
        int disarmDualWieldPenaltyForLog = 0;
        ItemData disarmAttackerWeaponOverride = null;
        int? grappleAttackBonusOverride = null;
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
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attacker.Stats.CharacterName} cannot feint: no eligible action remaining."));
                ShowActionChoices();
                return;
            }
        }
        else if (type == SpecialAttackType.CoupDeGrace)
        {
            if (!target.IsHelplessForCoupDeGrace())
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{target.Stats.CharacterName} is not helpless; Coup de Grace cannot be performed."));
                ShowActionChoices();
                return;
            }

            if (target.IsImmuneToCriticalHits())
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{target.Stats.CharacterName} is immune to critical hits and cannot be coup de graced."));
                ShowActionChoices();
                return;
            }

            if (!attacker.Actions.HasFullRoundAction)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attacker.Stats.CharacterName} cannot perform Coup de Grace: full-round action already spent."));
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
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attacker.Stats.CharacterName} cannot perform Disarm: {reason}."));
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
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attacker.Stats.CharacterName} cannot perform Sunder: {reason}."));
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
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attacker.Stats.CharacterName} cannot initiate grapple: {reason}."));
                ShowActionChoices();
                return;
            }

            grappleAttackBonusOverride = grappleAttackBonusUsed;
            actionLabel = $"attack BAB {CharacterStats.FormatMod(grappleAttackBonusUsed)} ({grappleAttacksRemaining} remaining)";
            Debug.Log($"[GameManager][Grapple] Shared-pool consume success actor={attacker.Stats.CharacterName} usedBAB={CharacterStats.FormatMod(grappleAttackBonusUsed)} remaining={grappleAttacksRemaining}");
        }
        else if (type == SpecialAttackType.BullRushAttack)
        {
            // Bull rush is a standard action for every creature (PHB p.154, CMB-102), never one
            // attack of a full attack. Its legality was checked at the top of this method.
            if (!attacker.CommitStandardAction())
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attacker.Stats.CharacterName} cannot bull rush: standard action already spent."));
                ShowActionChoices();
                return;
            }
            actionLabel = "standard action";
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
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attacker.Stats.CharacterName} cannot perform Trip: {reason}."));
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
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attacker.Stats.CharacterName} cannot use {type}: standard action already spent."));
                ShowActionChoices();
                return;
            }
            actionLabel = "standard action";
        }

        // Sanctuary and Hide from Undead (PHB p.274, p.241; D6), as on the NPC executor: the attempt rolls the attacker's
        // one save, after the cost and before the initiation AoOs; a failure loses the paid step or action and the maneuver
        // is never made (so it provokes nothing).
        if (gatedManeuver && AttackLegality.CommitWard(attacker, target, DirectAttackKind.Maneuver, AttackIntent.ForManeuver(type)) == WardVerdict.Failed)
        {
            Grid.ClearAllHighlights();
            _highlightedCells.Clear();
            _isSelectingSpecialAttack = false;
            if (type == SpecialAttackType.Sunder) { _pendingSunderUseOffHandSelection = false; ClearSunderSequenceState(); }
            else if (type == SpecialAttackType.Disarm) { _pendingDisarmUseOffHandSelection = false; ClearDisarmSequenceState(); }
            UpdateAllStatsUI();
            StartCoroutine(AfterAttackDelay(attacker, 0.8f));
            return;
        }

        // Shared with the NPC executor (TryNPCSpecialAttackIfBeneficial, CMB-076).
        if (ResolveManeuverInitiationAoOs(attacker, target, type) != ManeuverAoOOutcome.Proceed)
        {
            Grid.ClearAllHighlights();
            _highlightedCells.Clear();
            _isSelectingSpecialAttack = false;
            if (type == SpecialAttackType.Sunder)
            {
                _pendingSunderUseOffHandSelection = false;
                ClearSunderSequenceState();
            }
            else if (type == SpecialAttackType.Disarm)
            {
                _pendingDisarmUseOffHandSelection = false;
                ClearDisarmSequenceState();
            }
            StartCoroutine(AfterAttackDelay(attacker, 0.8f));
            return;
        }

        // The maneuver's hostile-action notice (a bard's fascination breaks) is sent by CharacterController.ExecuteSpecialAttack,
        // shared with the NPC executor (CHR-026).

        SpecialAttackResult result = attacker.ExecuteSpecialAttack(
            type,
            target,
            disarmTargetSlot,
            disarmAttackBonusOverride,
            grappleAttackBonusOverride,
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
            CombatUI.ShowCombatLog(CombatLogHelper.Buff("⚔", $"SPECIAL [{type}] ({actionLabel}): {result.Log}"));
        if (type == SpecialAttackType.Grapple)
        {
            int attacksRemaining = GetRemainingGrappleAttackActions(attacker);
            int nextBab = GetCurrentGrappleAttackBonus(attacker);
            Debug.Log($"[GameManager][Grapple] Result success={result.Success} actor={attacker.Stats.CharacterName} remainingSharedPool={attacksRemaining} nextBAB={CharacterStats.FormatMod(nextBab)} phase={CurrentPhase} subPhase={CurrentSubPhase}");

            if (attacksRemaining > 0)
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("↻", $"{attacker.Stats.CharacterName} has {attacksRemaining} grapple attack(s) remaining (next BAB {CharacterStats.FormatMod(nextBab)})."));
            else
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("↻", $"{attacker.Stats.CharacterName} has no grapple attacks remaining this turn."));
        }
        else if (type == SpecialAttackType.Trip)
        {
            int attacksRemaining = GetRemainingTripAttackActions(attacker);
            int nextBab = GetCurrentTripAttackBonus(attacker);
            Debug.Log($"[GameManager][Trip] Result success={result.Success} actor={attacker.Stats.CharacterName} remainingSharedPool={attacksRemaining} nextBAB={CharacterStats.FormatMod(nextBab)} phase={CurrentPhase} subPhase={CurrentSubPhase}");

            if (attacksRemaining > 0)
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("↻", $"{attacker.Stats.CharacterName} has {attacksRemaining} trip attempt(s) remaining (next BAB {CharacterStats.FormatMod(nextBab)})."));
            else
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("↻", $"{attacker.Stats.CharacterName} has no trip attempts remaining this turn."));
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

            CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"[Disarm] Attempt #{_disarmAttemptNumber} ({handLabel}) used BAB {CharacterStats.FormatMod(disarmAttackBonusUsed)}."));

            if (attacksRemaining > 0)
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("↻", $"{attacker.Stats.CharacterName} has {attacksRemaining} disarm-capable attack(s) remaining (next BAB {CharacterStats.FormatMod(nextBab)})."));
            else
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("↻", $"{attacker.Stats.CharacterName} has no disarm-capable attacks remaining this turn."));

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

            CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"[Sunder] Attempt #{_sunderAttemptNumber} ({handLabel}) used BAB {CharacterStats.FormatMod(sunderAttackBonusUsed)}."));

            if (attacksRemaining > 0)
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("↻", $"{attacker.Stats.CharacterName} has {attacksRemaining} sunder-capable attack(s) remaining (next BAB {CharacterStats.FormatMod(nextBab)})."));
            else
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("↻", $"{attacker.Stats.CharacterName} has no sunder-capable attacks remaining this turn."));

            _pendingSunderUseOffHandSelection = false;
            ClearSunderSequenceState();
        }

        // The target's reaction to the maneuver (the bombardier beetle's acid spray, D1) resolves as ExecuteSpecialAttack
        // returns and can end the combat: nothing of the maneuver's aftermath follows a victory or defeat.
        if (CurrentPhase == TurnPhase.CombatOver)
        {
            FinalizeSpecialAttackResolution(attacker, target);
            return;
        }

        // Melee reaction effects (Fire Shield, Thorns, etc.) — trip/disarm are melee maneuvers
        if (type == SpecialAttackType.Trip || type == SpecialAttackType.Disarm)
            MeleeReactionService.TriggerReactions(attacker, target, null);

        // The Improved Trip attack and the defender's counter-trip (PHB p.96, p.158; CMB-079), shared
        // with the NPC executor. A controllable defender's prompt holds the finish until it is answered.
        if (type == SpecialAttackType.Trip)
        {
            // The Improved Trip attack's reactions, or the target's AoO before an unarmed one (PHB p.139), can drop
            // the tripper; its turn then ends instead of offering more actions.
            HandleTripAftermath(attacker, target, result, () =>
            {
                if (IsSpecialAttackerDropped(attacker))
                    FinalizeSpecialAttackAttackerDropped(attacker, "during the trip");
                else
                    FinalizeSpecialAttackResolution(attacker, target);
            });
            return;
        }

        if (result.Success)
        {
            if (type == SpecialAttackType.BullRushAttack || type == SpecialAttackType.BullRushCharge)
            {
                ResolveBullRushPushAndFollow(attacker, target, result, isCharge: type == SpecialAttackType.BullRushCharge, squaresMovedThisCharge: 0,
                    onComplete: attackerDown =>
                    {
                        if (attackerDown)
                            FinalizeSpecialAttackAttackerDropped(attacker, "during the bull rush");
                        else
                            FinalizeSpecialAttackResolution(attacker, target);
                    });
                return;
            }

            if (type == SpecialAttackType.Overrun)
                TryPushTargetAway(attacker, target, 1, allowAttackerFollow: true);
        }

        FinalizeSpecialAttackResolution(attacker, target);
    }

    /// <summary>
    /// The PC wrapper's maneuver intent for the shared gate (AttackLegality, CMB-104): a disarm or sunder names the hand
    /// chosen from the menu (the off-hand weapon, or the main weapon; none for an unarmed attempt), as
    /// TryConsumeDisarmAttackAction and TryConsumeSunderAttackAction then use it; any other maneuver names none.
    /// </summary>
    private AttackIntent PcManeuverIntent(CharacterController attacker, SpecialAttackType type)
    {
        if (attacker != null && (type == SpecialAttackType.Disarm || type == SpecialAttackType.Sunder))
        {
            bool offHand = type == SpecialAttackType.Disarm ? _pendingDisarmUseOffHandSelection : _pendingSunderUseOffHandSelection;
            return AttackIntent.ForManeuverWith(type, offHand ? attacker.GetOffHandAttackWeapon() : attacker.GetEquippedMainWeapon());
        }
        return AttackIntent.ForManeuver(type);
    }

    /// <summary>
    /// The special attacks the PC wrapper and the NPC executor put through the direct-attack gate (AttackLegality): those
    /// made against an opponent in reach (trip, disarm, sunder, grapple, bull rush, coup de grace, feint). Aid another,
    /// waking an ally, overrun (a move-through, OverrunSystem) and turning undead have their own tests.
    /// </summary>
    internal static bool IsDirectAttackManeuverForGate(SpecialAttackType type)
        => type == SpecialAttackType.Trip || type == SpecialAttackType.Disarm || type == SpecialAttackType.Sunder
           || type == SpecialAttackType.Grapple || type == SpecialAttackType.BullRushAttack
           || type == SpecialAttackType.CoupDeGrace || type == SpecialAttackType.Feint;

    private void FinalizeSpecialAttackResolution(CharacterController attacker, CharacterController target)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        _isSelectingSpecialAttack = false;

        UpdateAllStatsUI();

        // Already decided (for example by an AoO during a bull rush push, CORE-011).
        if (CurrentPhase == TurnPhase.CombatOver)
            return;

        // Victory or defeat when the maneuver (or an AoO it provoked) dropped the last creature of a side (CORE-011).
        if (EvaluateCombatEnd("FinalizeSpecialAttackResolution"))
            return;

        if (attacker != null)
            StartCoroutine(AfterAttackDelay(attacker, 1.0f));
    }

    /// <summary>True when <paramref name="attacker"/> can no longer act this turn: dead, unconscious or dying.</summary>
    private static bool IsSpecialAttackerDropped(CharacterController attacker)
    {
        return attacker != null && attacker.Stats != null
            && (attacker.IsDead || attacker.Stats.IsDead || attacker.IsUnconscious || !attacker.CanTakeTurnActions());
    }

    /// <summary>
    /// End of a PC special attack whose attacker an AoO or a reaction dropped during the resolution (a bull
    /// rush push or follow, a trip's Improved Trip attack): same cleanup as
    /// <see cref="FinalizeSpecialAttackResolution"/>, then the turn ends, as when movement AoOs drop a PC
    /// (ResolveAoOsAndMove). <paramref name="when"/> completes the log line ("during the bull rush"). The
    /// resolver has already run the victory and defeat checks.
    /// </summary>
    private void FinalizeSpecialAttackAttackerDropped(CharacterController attacker, string when)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        _isSelectingSpecialAttack = false;
        UpdateAllStatsUI();

        if (CurrentPhase == TurnPhase.CombatOver)
            return;

        string name = attacker != null && attacker.Stats != null ? attacker.Stats.CharacterName : "The attacker";
        CombatUI?.ShowCombatLog(CombatLogHelper.CriticalFailure("⛔", $"{name} is incapacitated {when}."));
        if (IsPlayerTurn)
            EndActivePCTurn();
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

        // NOTE: Do NOT check or commit standard action here.
        // The Attack button already committed the standard action before
        // the player clicks the wall cell. Same pattern as regular attacks.

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
            // Two-handed weapons get 1-1/2 times a STR bonus; a penalty applies in full (PHB p.113-114; CMB-161)
            bool isTwoHanded = weapon != null && weapon.IsTwoHanded;
            if (isTwoHanded)
                damage += CombatCalculationService.TwoHandedStrDamage(strMod);
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
        _highlightedCells.Clear();

        // End the attack sequence so the character can't keep attacking the wall.
        // Wall attacks are a single standard action — no iterative attacks apply.
        EndAttackSequence();

        StartCoroutine(AfterAttackDelay(attacker, 1.0f));
    }

    // ═══════════════════════════════════════════════════════════════════
    //  BREAK WALL (STR CHECK) — BUTTON HANDLER & TARGET SELECTION
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Called when the player presses the "Break Wall" action button.
    /// Enters a target-selection sub-phase that highlights adjacent intact wall cells.
    /// </summary>
    public void OnBreakWallButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;

        // Collect adjacent intact Wall of Ice cells (Chebyshev distance 1)
        HashSet<Vector2Int> allIntactWalls = WallOfIceAreaEffect.GetAllIntactWallOfIceCells();
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        bool hasAdjacentWall = false;
        foreach (Vector2Int wallCoord in allIntactWalls)
        {
            int dist = SquareGridUtils.ChebyshevDistance(pc.GridPosition, wallCoord);
            if (dist > 1) continue;

            SquareCell wallCell = Grid.GetCell(wallCoord);
            if (wallCell == null) continue;

            wallCell.SetHighlight(HighlightType.AttackRange);
            _highlightedCells.Add(wallCell);
            hasAdjacentWall = true;
        }

        if (!hasAdjacentWall)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{pc.Stats.CharacterName} is not adjacent to any intact Wall of Ice."));
            ShowActionChoices();
            return;
        }

        _isSelectingBreakWallTarget = true;
        // HandleAttackTargetClick tests the pending mode before the break-wall flag, so a mode left over from an
        // earlier target selection (Lay on Hands) must not claim the wall click.
        _pendingAttackMode = PendingAttackMode.Single;
        CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
        CombatUI?.ShowCombatLog(CombatLogHelper.Success("💪", $"Select an adjacent Wall of Ice cell to attempt a Strength check, or right-click/ESC to cancel."));
        Debug.Log($"[BreakWall] {pc.Stats.CharacterName} entering break-wall target selection with {_highlightedCells.Count} adjacent wall cells.");
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
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attacker.Stats.CharacterName} must be adjacent to attempt a Strength check."));
            return;
        }

        // Cell must be intact
        if (wall.IsBreached(wallCell))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"That wall cell is already breached."));
            return;
        }

        // Consume standard action
        if (!attacker.CommitStandardAction())
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attacker.Stats.CharacterName} has no standard action available."));
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
        _highlightedCells.Clear();

        // Clear break-wall targeting state
        _isSelectingBreakWallTarget = false;

        // End any attack sequence that may be active
        EndAttackSequence();

        StartCoroutine(AfterAttackDelay(attacker, 1.0f));
    }

    /// <summary>The attack's range for the mode of <paramref name="intent"/> (AttackIntent.ForUi for a PC flow, ForAI for an NPC).</summary>
    private RangeInfo CalculateRangeInfo(CharacterController attacker, CharacterController target, AttackIntent intent)
    {
        if (_combatFlowService != null)
            return _combatFlowService.CalculateRangeInfo(attacker, target, intent);

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
        Debug.Log($"[AttackFlow] PerformIterativeSequenceAttack ENTER | attacker={attackerName} | target={targetName} | phase={CurrentPhase} | subPhase={CurrentSubPhase} | inSequence={_isInAttackSequence} | attacksUsed={(attacker != null ? attacker.ProgressiveAttackPool.MainHandStepsUsed : 0)}/{(attacker != null ? attacker.ProgressiveAttackPool.MainHandBudget : 0)}");

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

        // The PC UI's attack selection, read once (AttackIntent.ForUi); every step of this full attack uses it.
        AttackIntent sequenceIntent = AttackIntent.ForUi(this, attacker);
        bool rangedMode = IsAttackModeRanged(attacker, null, sequenceIntent);
        string modeLabel = rangedMode ? "ranged" : "melee";
        // A thrown full attack (a javelin, a thrown dagger): each step needs a throwable weapon in hand (PHB p.113).
        bool thrownSequence = sequenceIntent.IsThrowOf(sequenceIntent.Weapon);

        RangeInfo initialRangeInfo = CalculateRangeInfo(attacker, initialTarget, sequenceIntent);
        int plannedAttackCount = attacker.GetPlannedFullAttackCount(initialRangeInfo);
        if (plannedAttackCount <= 0)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attacker.Stats.CharacterName} has no available attacks."));
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
                || !IsTargetInCurrentWeaponRange(attacker, currentTarget, sequenceIntent)
                // A creature holding with one body part attacks only other opponents (MM p.310, CMB-163); nothing passes a
                // Resilient Sphere (CMB-092); a failed ward save bars the target (D6): the shared gate.
                || !IsDirectlyAttackableNoReach(attacker, currentTarget);

            if (needsRetarget)
            {
                if (currentTarget != null && currentTarget.Stats != null && !currentTarget.Stats.IsDead)
                {
                    Legality sequenceLegality = AttackLegality.Check(attacker, currentTarget, DirectAttackKind.Weapon, null,
                        AttackLegalityChecks.Sphere | AttackLegalityChecks.HoldBar | AttackLegalityChecks.Ward);
                    CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", !sequenceLegality.Ok
                        ? sequenceLegality.Reason
                        : $"{currentTarget.Stats.CharacterName} is no longer in {modeLabel} reach."));
                }

                List<CharacterController> validTargets = GetValidTargetsForCurrentWeapon(attacker, sequenceIntent);

                if (validTargets.Count == 0 && CanTakeFiveFootStep(attacker))
                {
                    CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"No valid {modeLabel} targets right now. You may take a 5-foot step to continue."));

                    yield return StartCoroutine(WaitForOptionalFiveFootStepDuringFullAttack(
                        attacker,
                        "Step to reach another target:",
                        requireReachableTargetAfterStep: true,
                        rangedMode: rangedMode));

                    if (_fullAttackFiveFootStepSelectionCancelled || !_fullAttackFiveFootStepWasTaken)
                    {
                        CombatUI?.ShowCombatLog(CombatLogHelper.Info("↩", $"{attacker.Stats.CharacterName} ends full attack early. {remainingAttacks} attack(s) unused."));
                        break;
                    }

                    validTargets = GetValidTargetsForCurrentWeapon(attacker, sequenceIntent);
                }

                if (validTargets.Count == 0)
                {
                    CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"No valid {modeLabel} targets for {remainingAttacks} remaining attack(s)."));
                    break;
                }

                yield return StartCoroutine(WaitForFullAttackRetargetSelection(attacker, remainingAttacks));

                if (_rangedRetargetSelectionCancelled || _selectedRangedRetarget == null)
                {
                    CombatUI?.ShowCombatLog(CombatLogHelper.Info("↩", $"{attacker.Stats.CharacterName} ends full attack early. {remainingAttacks} attack(s) unused."));
                    break;
                }

                currentTarget = _selectedRangedRetarget;
                _selectedRangedRetarget = null;
                _rangedRetargetSelectionCancelled = false;

                CombatUI?.ShowCombatLog(CombatLogHelper.Info("🎯", $"{attacker.Stats.CharacterName} switches to {currentTarget.Stats.CharacterName}."));
            }

            // Haste's extra natural attack: the player picks the natural weapon when the sequence reaches it
            // (PHB p.239; owner decision 2026-10-07, CMB-106; CMB-124). Cancel uses the default.
            int hasteNaturalAttackIndex = -1;
            if (!rangedMode && attacker.IsHasteExtraNaturalStep(attackIndex) && attacker.CanUseHasteExtraNaturalAttack())
            {
                Coroutine hasteChoice = AttackDeciders.For(attacker).ChooseHasteNaturalAttack(this, attacker, currentTarget, null, 0, choice => hasteNaturalAttackIndex = choice);
                if (hasteChoice != null)
                    yield return hasteChoice;
                if (attacker == null || attacker.Stats == null || attacker.Stats.IsDead
                    || currentTarget == null || currentTarget.Stats == null || currentTarget.Stats.IsDead)
                    break;
            }

            // Sanctuary and Hide from Undead (PHB p.274, p.241; D6): this step's attempt rolls the attacker's one save; a
            // failure loses this attack, and the next one goes to another target (the warded one now fails the gate).
            if (AttackLegality.CommitWard(attacker, currentTarget, DirectAttackKind.Weapon) == WardVerdict.Failed)
            {
                attacksMade++;
                UpdateAllStatsUI();
                yield return new WaitForSeconds(0.35f);
                continue;
            }

            // Recompute flanking/range context each attack in case target/position changed.
            var allCombatants = GetAllCharacters();
            CharacterController flankPartner;
            bool isFlanking = CombatUtils.IsAttackerFlanking(attacker, currentTarget, allCombatants, out flankPartner);
            int flankBonus = isFlanking ? CombatUtils.FlankingAttackBonus : 0;
            string partnerName = flankPartner != null ? flankPartner.Stats.CharacterName : "";
            // The weapon in hand for this step: after a throw the hook drew the next one (or none is left).
            ItemData stepWeapon = attacker.GetEquippedMainWeapon();
            sequenceIntent.Weapon = stepWeapon;
            RangeInfo rangeInfo = CalculateRangeInfo(attacker, currentTarget, sequenceIntent);

            // A ranged or thrown attack provokes from each threatening foe, step by step (PHB p.141 Table 8-2), and a
            // projectile weapon needs a piece of ammunition for each shot (PHB p.113): the same helpers as the Attack
            // button's sequence (CombatFlowService; step 17 merges the copies).
            if (rangedMode && _combatFlowService != null)
            {
                if (stepWeapon != null && stepWeapon.IsProjectileWeapon && !_combatFlowService.CheckAmmoAvailable(attacker, stepWeapon))
                {
                    CombatUI?.ShowCombatLog(CombatLogHelper.Info("↩", $"{attacker.Stats.CharacterName} ends full attack early: no ammunition. {remainingAttacks} attack(s) unused."));
                    break;
                }
                if (thrownSequence && !sequenceIntent.IsThrowOf(stepWeapon))
                {
                    CombatUI?.ShowCombatLog(CombatLogHelper.Info("↩", $"{attacker.Stats.CharacterName} has no throwable weapon in hand. {remainingAttacks} attack(s) unused."));
                    break;
                }
                if (!_combatFlowService.ResolveRangedAttackAoOIfProvoked(attacker))
                    break;
                if (CurrentPhase == TurnPhase.CombatOver)
                    yield break;
            }

            FullAttackResult stepResult = attacker.FullAttack(
                currentTarget,
                isFlanking,
                flankBonus,
                partnerName,
                rangeInfo,
                startAttackIndex: attackIndex,
                maxAttacks: 1,
                hasteNaturalAttackIndex: hasteNaturalAttackIndex);

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

            // The one post-attack hook (origin C): the thrown drop and the ammunition, Concentration per hit, melee
            // reactions, the free trip, Improved Grab (the player's prompt, MM p.310), the summon cleanup and the
            // combat-end check for the target and the attacker (CORE-011, CORE-037).
            AttackResolution stepResolution = AttackResolution.Create(AttackOrigin.PcFullAttackButton, attacker, currentTarget, sequenceIntent).With(stepResult.Attacks);
            stepResolution.RangeInfo = rangeInfo;
            stepResolution.CombatEndContext = "ExecuteFullAttackSequence";
            var stepOutcome = new PostAttackOutcome();
            IEnumerator stepPost = OnAttackResolved(stepResolution, stepOutcome);
            while (stepPost.MoveNext())
                yield return stepPost.Current;

            if (stepOutcome.CombatEnded || CurrentPhase == TurnPhase.CombatOver)
                yield break;
            if (stepOutcome.AttackerOutOfFight)
                break;

            // A creature grappling acts only through grapple actions (PHB p.156; CMB-149): once its Improved Grab takes
            // hold, this button's remaining attacks are not made as ordinary attacks. A hold with one body part (MM p.310)
            // leaves it ungrappled, so its remaining attacks go on against other opponents.
            if (attacker.IsGrappling())
            {
                int attacksLeft = plannedAttackCount - (attackIndex + 1);
                if (attacksLeft > 0)
                    CombatUI?.ShowCombatLog(CombatLogHelper.Info("🪢", $"{attacker.Stats.CharacterName} is grappling: its {attacksLeft} remaining attack(s) are not made as ordinary attacks (PHB p.156)."));
                break;
            }

            if (attack.TargetKilled)
            {
                int attacksRemainingAfterKill = plannedAttackCount - (attackIndex + 1);
                if (attacksRemainingAfterKill > 0)
                {
                    CombatUI?.ShowCombatLog(CombatLogHelper.Death("💀", $"{currentTarget.Stats.CharacterName} is defeated! {attacksRemainingAfterKill} attack(s) remaining."));
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

        // D&D 3.5: You can also 5-foot step after attacks (not an attacker a reaction or an AoO dropped).
        if (CanTakeFiveFootStep(attacker) && CurrentPhase != TurnPhase.CombatOver && !CombatEndRules.IsOutOfFight(attacker))
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

        CombatUI?.ShowCombatLog(CombatLogHelper.Success("✅", $"{attacker.Stats.CharacterName} completes {modeLabel} full attack ({attacksMade}/{plannedAttackCount} attacks used)."));
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

    private bool IsHoldingTouchCharge(CharacterController character)
    {
        if (character == null || !character.Stats.IsSpellcaster)
            return false;

        var spellComp = character.Spellcasting;
        return spellComp != null && spellComp.HasHeldTouchCharge && spellComp.HeldTouchSpell != null;
    }

    private string GetHeldTouchSpellName(CharacterController character)
    {
        var spellComp = character != null ? character.Spellcasting : null;
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
                      $"offHandGate={_offHandAttackAvailableThisTurn} offHandUsed={_offHandAttackUsedThisTurn} attacksUsed={character.ProgressiveAttackPool.MainHandStepsUsed}/{character.ProgressiveAttackPool.MainHandBudget}");
            return false;
        }

        bool hasRemainingGrappleAttempts = CanUseGrappleAttackOption(character);
        bool hasRemainingTripAttempts = CanUseTripAttackOption(character);
        bool hasRemainingDisarmAttempts = CanUseDisarmAttackOption(character);
        bool hasRemainingCoupDeGraceAttempt = CanUseCoupDeGraceAttackOption(character);

        bool hasIterativeWeaponAttackSequence = _isInAttackSequence && _attackingCharacter == character;

        if (hasRemainingGrappleAttempts || hasRemainingTripAttempts || hasRemainingDisarmAttempts || hasRemainingCoupDeGraceAttempt || hasIterativeWeaponAttackSequence)
        {
            Debug.Log(
                $"[TurnFlow] ShouldAutoEndTurn=false for {character.Stats.CharacterName}: " +
                $"iterativeRemaining(g={hasRemainingGrappleAttempts}, trip={hasRemainingTripAttempts}, d={hasRemainingDisarmAttempts}, cdg={hasRemainingCoupDeGraceAttempt}, atk={hasIterativeWeaponAttackSequence})");
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

        // An attacker a reaction or an AoO dropped (dead, dying, unconscious) has no more choices this turn.
        if (pc != null && pc == ActivePC && CombatEndRules.IsOutOfFight(pc))
        {
            LogMenuFlow("AfterAttackDelay:ATTACKER_OUT_OF_FIGHT", pc);
            EndActivePCTurn();
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

    // ═══════════════════════════════════════════════════════════════════
    //  AI WALL OF ICE INTERACTION HELPERS
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// AI helper: Attack an adjacent intact Wall of Ice cell with equipped weapon.
    /// Returns an IEnumerator so the AI can yield on it.
    /// </summary>
    public IEnumerator NPCAttackWallForAI(CharacterController npc, WallOfIceAreaEffect wall, Vector2Int wallCell)
    {
        if (npc == null || wall == null || wall.IsBreached(wallCell))
            yield break;

        // AI must commit its own standard action (player path commits via Attack button)
        if (!npc.CommitStandardAction())
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{npc.Stats.CharacterName} has no standard action available."));
            yield break;
        }

        PerformPlayerAttackOnWall(npc, wall, wallCell);
        yield return new WaitForSeconds(1.0f);
    }

    /// <summary>
    /// AI helper: Attempt a Strength check to break an adjacent intact Wall of Ice cell.
    /// Returns an IEnumerator so the AI can yield on it.
    /// </summary>
    public IEnumerator NPCBreakWallForAI(CharacterController npc, WallOfIceAreaEffect wall, Vector2Int wallCell)
    {
        if (npc == null || wall == null || wall.IsBreached(wallCell))
            yield break;

        PerformStrengthCheckOnWall(npc, wall, wallCell);
        yield return new WaitForSeconds(1.0f);
    }

    /// <summary>
    /// Returns the best adjacent intact Wall of Ice cell for an NPC to target,
    /// prioritizing cells that lie between the NPC and a given target position.
    /// Returns null if no adjacent wall cells exist.
    /// </summary>
    public Vector2Int? FindBestAdjacentWallCellForAI(CharacterController npc, Vector2Int targetPos)
    {
        HashSet<Vector2Int> intactWalls = WallOfIceAreaEffect.GetAllIntactWallOfIceCells();
        if (intactWalls.Count == 0) return null;

        Vector2Int? bestCell = null;
        float bestScore = float.MinValue;

        foreach (Vector2Int wc in intactWalls)
        {
            int dist = SquareGridUtils.ChebyshevDistance(npc.GridPosition, wc);
            if (dist > 1) continue;

            // Score: prefer wall cells closer to the target (blocks path)
            float distToTarget = SquareGridUtils.ChebyshevDistance(wc, targetPos);
            float score = -distToTarget; // Lower distance to target = higher priority

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = wc;
            }
        }

        return bestCell;
    }

}
