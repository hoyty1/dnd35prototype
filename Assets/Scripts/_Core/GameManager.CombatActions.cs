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
        AttackIntent uiIntent = AttackIntent.ForUi(this, pc);
        bool usingThrownAttack = IsUsingThrownAttackMode(pc, weapon, uiIntent);
        bool isRangedWeapon = IsAttackModeRanged(pc, weapon, uiIntent);
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
        // Also highlight the Wall of Ice squares the attack reaches: the weapon's reach band, or its maximum range for a shot
        // or a throw (IsWallCellInWeaponAttackReach, the attack-wall executor's own square test). A grappler takes no wall
        // attack (PHB p.156; GetSupportActionGrappleRefusal).
        bool hasWallTarget = false;
        if (_pendingAttackMode != PendingAttackMode.CastSpell && _pendingAttackMode != PendingAttackMode.LayOnHands
            && GetSupportActionGrappleRefusal(pc) == null)
        {
            HashSet<Vector2Int> wallCells = WallOfIceAreaEffect.GetAllIntactWallOfIceCells();
            if (wallCells.Count > 0)
            {
                foreach (Vector2Int wallCoord in wallCells)
                {
                    if (!IsWallCellInWeaponAttackReach(pc, wallCoord, weapon, uiIntent))
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

            if (_pendingAttackMode == PendingAttackMode.Single && uiIntent.ThrownSelected)
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

        bool isRanged = IsAttackModeRanged(pc, pc.GetEquippedMainWeapon(), AttackIntent.ForUi(this, pc));
        return cell.GetOccupantOtherThan(pc, o => o.Stats != null && !o.Stats.IsDead
            && pc.IsTargetInCurrentWeaponRange(o)
            && (!isRanged || pc.CanLocateForRangedAttack(o)));
    }

    private List<CharacterController> GetValidRangedTargets(CharacterController attacker, AttackIntent intent = null)
    {
        var valid = new List<CharacterController>();
        intent ??= AttackIntent.ForUi(this, attacker);
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

        intent ??= AttackIntent.ForUi(this, attacker);
        return IsAttackModeRanged(attacker, null, intent)
            ? GetValidRangedTargets(attacker, intent)
            : GetValidMeleeTargets(attacker);
    }

    /// <summary>Reach or range for the attack mode of <paramref name="intent"/> (null: the PC UI's, AttackIntent.ForUi).</summary>
    private bool IsTargetInCurrentWeaponRange(CharacterController attacker, CharacterController target, AttackIntent intent = null)
    {
        if (attacker == null || target == null || target.Stats == null || target.Stats.IsDead)
            return false;

        intent ??= AttackIntent.ForUi(this, attacker);
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
        bool isThrownWeapon = IsUsingThrownAttackMode(attacker, weapon, AttackIntent.ForUi(this, attacker)) || (weapon != null && weapon.WeaponCat == WeaponCategory.Ranged && weapon.IsThrown);
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

        if (cell.Coords == pc.GridPosition)
        {
            CancelMovementSelection();
            return;
        }

        if (!_highlightedCells.Contains(cell) || _movementService == null || !_movementService.IsValidMoveDestination(pc, cell.Coords))
            return;

        // The same path rules as the AI's moves (ExecuteMoveAction's path search): the move or
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
                        ? $"{pc.Stats.CharacterName} - {WithdrawIndicator(pc)}"
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
    /// move (GameManager.RunOverrunMove, PC and NPC alike, and its Normal Move branch; CMB-151), the grappler's drag
    /// (GrappleSystem.ExecuteGrappleMovement, through ExecutePathWithMovementAoOs) and the escaper's adjacent move
    /// (GrappleSystem.ExecuteFreeAdjacentGrappleMovement; both CMB-152). The bull rush follow resolves and records its own
    /// steps (ResolveBullRushStepAoOs).
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
        MovementAoOStepOutcome outcome,
        MoveKind kind = MoveKind.Move)
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
                    yield return StartCoroutine(_movementService.ExecuteMovement(mover, segment, secondsPerStep, markAsMoved: true, lastStepIsDestination: atEnd, kind: kind));
                else
                    yield return StartCoroutine(mover.MoveAlongPath(segment, secondsPerStep, markAsMoved: true, lastStepIsDestination: atEnd, kind: kind));

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

            // A step the mover cannot take (a barrier, a hold, Grease's failed Balance check) provokes nothing: it never
            // leaves its square (PHB p.137, p.237).
            if (MovementService.IsStepRefused(mover, mover.GridPosition, path[pathIndex], kind))
            {
                outcome.StopMovement = true;
                outcome.Reason = "blocked";
                yield break;
            }

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
    /// follows the pushed defender. An overrun's step 1 AoO (the defender's, when the attacker enters its
    /// space; GameManager.RunOverrunMove) is made here too and is likewise its own provocation (batch 12 step 20).
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

    /// <summary>
    /// The PC's move or withdraw along the clicked <paramref name="path"/> (batch 13 step 9): the one move executor
    /// (ExecuteMoveAction) with <see cref="MoveRequest.ForUi"/>, through the catalog's move or withdraw entry, whose UiAfter
    /// (ShowChoices) and runner continue the turn: the action panel after the move or a refusal, the turn's end when the
    /// PC was dropped on the way (after the combat-end check). The runner, the executor and the walk are inlined, so the
    /// walk's yields are this coroutine's own.
    /// </summary>
    private IEnumerator ExecuteMovement(CharacterController pc, List<Vector2Int> path, bool isWithdraw = false, List<AoOThreatInfo> provokedAoOs = null)
    {
        if (pc == null || pc.Stats == null || path == null || path.Count == 0)
            yield break;

        CurrentSubPhase = PlayerSubPhase.Animating;

        // Hide path preview and hover marker immediately when movement begins
        if (_pathPreview != null) _pathPreview.HidePath();
        if (_hoverMarker != null) _hoverMarker.Hide();

        ActionContext ctx = ActionContext.For(isWithdraw ? ActionIds.Withdraw : ActionIds.Move, pc, ActorSource.Ui);
        ctx.Cell = path[path.Count - 1];
        ctx.CoreRequest = MoveRequest.ForUi(pc, path, provokedAoOs, isWithdraw, PlayerMoveSecondsPerStep);
        IEnumerator run = RunPlayerActionInline(ctx, new ActionOutcome());
        while (run.MoveNext())
            yield return run.Current;
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

        Debug.Log($"[OffHand] HandleOffHandTargetClick attacker={attacker.Stats.CharacterName} target={target.Stats.CharacterName} mode={(useThrownRange ? "Thrown" : "Melee")} weapon={offHandWeapon.Name}");

        // One off-hand step of the creature's attack sequence through the one executor (batch 12 step 18; PHB p.160): the
        // first attack of the turn pays the standard action, a later one the move action (full attack, PHB p.143); the
        // shared gate, the ward's save (D6), a throw's AoOs, the off-hand ladder's BAB and penalty
        // (CharacterController.ResolveOffHandAttackStep) and the post-attack hook (origin E).
        CurrentSubPhase = PlayerSubPhase.Animating;
        AttackSequenceRequest request = AttackSequenceRequest.ForUiOffHand(this, attacker, target, offHandWeapon, useThrownRange);
        request.ReportStep = step =>
        {
            string modeLabel = useThrownRange ? "Off-Hand Thrown Attack" : "Off-Hand Attack";
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("↻", $"{modeLabel} with {offHandWeapon.Name}"));
            CombatUI?.ShowCombatLog(step.Result.GetAttackBreakdown(step.Label));
            _lastCombatLog = step.Result.GetDetailedSummary();
            UpdateAllStatsUI();
            Grid.ClearAllHighlights();
            _highlightedCells.Clear();
        };
        var outcome = new AttackSequenceOutcome();
        int offHandStepsBefore = attacker.ProgressiveAttackPool.OffHandStepsUsed;
        RunAttackSequenceSync(request, outcome, () =>
        {
            if (outcome.CombatEnded || CurrentPhase == TurnPhase.CombatOver)
            {
                _offHandAttackUsedThisTurn = true;
                _isSelectingOffHandTarget = false;
                _isSelectingOffHandThrownTarget = false;
                return;
            }
            // Nothing attempted only when no step was paid: a throw's step is paid before its AoOs (stage 6, then 7), so an
            // AoO that drops or grapples the thrower ends the attack through FinishOffHandAttack (AfterAttackDelay's
            // out-of-fight and grapple checks), not the action menu.
            bool stepPaid = attacker.ProgressiveAttackPool.OffHandStepsUsed > offHandStepsBefore;
            if (!stepPaid && outcome.AttacksMade == 0 && outcome.StepsLostToWard == 0)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attacker.Stats.CharacterName} cannot make {(useThrownRange ? "an off-hand thrown attack" : "an off-hand attack")}: {outcome.StopReason ?? "no attack"}."));
                _isSelectingOffHandTarget = false;
                _isSelectingOffHandThrownTarget = false;
                _currentOffHandBAB = 0;
                _currentOffHandWeapon = null;
                ShowActionChoices();
                return;
            }
            FinishOffHandAttack(attacker, useThrownRange);
        });
    }

    /// <summary>The off-hand attack's end: the UI's off-hand fields (read back from the inventory after a throw) and the after-attack delay.</summary>
    private void FinishOffHandAttack(CharacterController attacker, bool useThrownRange)
    {
        if (useThrownRange)
            _currentOffHandWeapon = attacker.GetOffHandAttackWeapon();

        _offHandAttackUsedThisTurn = attacker.GetRemainingOffHandAttackSteps() <= 0;
        _offHandAttackAvailableThisTurn = attacker.HasOffHandWeaponEquipped();
        _isSelectingOffHandTarget = false;
        _isSelectingOffHandThrownTarget = false;

        Debug.Log($"[Attack][OffHand] Off-hand attack resolved. inSequence={_isInAttackSequence} mainAttacksUsed={attacker.ProgressiveAttackPool.MainHandStepsUsed}/{attacker.ProgressiveAttackPool.MainHandBudget} offHandUsed={attacker.ProgressiveAttackPool.OffHandStepsUsed} thrown={useThrownRange} offHandAvailable={_offHandAttackAvailableThisTurn}");

        StartCoroutine(AfterAttackDelay(attacker, 1.2f));
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



    /// <summary>
    /// The PC wrapper of the maneuver core (batch 12 step 19, CMB-102): after the Special Attack menu, the target click and
    /// the item menu, it builds the UI request (ManeuverRequest.ForUi, with the hand chosen and the dual-wield state read once
    /// as the intent, PcManeuverIntent) and runs the one executor (ExecuteManeuver: legality, the cost of the table before
    /// the ward and the AoOs, the resolution and its aftermath), the NPC executor's. Its own part is the UI: the disarm and
    /// sunder attempt counters and lines, and the continuation (<see cref="FinishPcManeuver"/>).
    /// </summary>
    private void ExecuteSpecialAttack(CharacterController attacker, CharacterController target, SpecialAttackType type, EquipSlot? disarmTargetSlot = null, EquipSlot? sunderTargetSlot = null)
    {
        if (attacker == null || target == null) { ShowActionChoices(); return; }

        bool useOffHand = type == SpecialAttackType.Disarm ? _pendingDisarmUseOffHandSelection
            : type == SpecialAttackType.Sunder && _pendingSunderUseOffHandSelection;
        EquipSlot? itemSlot = type == SpecialAttackType.Disarm ? disarmTargetSlot : type == SpecialAttackType.Sunder ? sunderTargetSlot : null;
        ManeuverRequest req = ManeuverRequest.ForUi(attacker, target, type, itemSlot, useOffHand, PcManeuverIntent(attacker, type));

        // The shared legality first (nothing spent), so a refused (gray) target costs nothing, as on the NPC executor: the
        // Improved Grab hold bar (CMB-163), bull rush and trip limits, the direct-attack gate (CMB-092, CMB-104, D6), a
        // coup de grace's target and the cost of the table.
        Legality legality = CanStartManeuver(req);
        if (!legality.Ok)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attacker.Stats.CharacterName} cannot use {type} on {target.Stats.CharacterName}: {legality.Reason.TrimEnd('.')}."));
            ClearPcManeuverSelection(type);
            ShowActionChoices();
            return;
        }

        CurrentSubPhase = PlayerSubPhase.Animating;

        if (type == SpecialAttackType.Disarm && (!_isDisarmSequenceActive || _disarmInitiator != attacker || _disarmTarget != target))
            BeginDisarmSequence(attacker, target, disarmTargetSlot);
        if (type == SpecialAttackType.Sunder && (!_isSunderSequenceActive || _sunderInitiator != attacker || _sunderTarget != target))
            BeginSunderSequence(attacker, target, sunderTargetSlot);

        req.OnPaid = pay =>
        {
            if (type == SpecialAttackType.Disarm)
            {
                _disarmAttemptNumber++;
                pay.ActionLabel = $"disarm attempt #{_disarmAttemptNumber} ({(pay.UsedOffHand ? "off-hand" : "main-hand")}), BAB {CharacterStats.FormatMod(pay.StepBab ?? 0)} ({pay.AttacksRemaining} remaining)";
            }
            else if (type == SpecialAttackType.Sunder)
            {
                _sunderAttemptNumber++;
                pay.ActionLabel = $"sunder attempt #{_sunderAttemptNumber} ({(pay.UsedOffHand ? "off-hand" : "main-hand")}), BAB {CharacterStats.FormatMod(pay.StepBab ?? 0)} ({pay.AttacksRemaining} remaining)";
            }
        };
        req.OnResolvedReport = (result, pay) => ReportPcManeuverAttemptsLeft(attacker, type, pay);

        ExecuteManeuver(req, outcome => FinishPcManeuver(attacker, target, type, outcome));
    }

    /// <summary>The PC's remaining-attempt lines after a trip, grapple, disarm or sunder (the Special Attack menu's counts).</summary>
    private void ReportPcManeuverAttemptsLeft(CharacterController attacker, SpecialAttackType type, ManeuverPayment pay)
    {
        string name = attacker.Stats.CharacterName;
        switch (type)
        {
            case SpecialAttackType.Grapple:
            {
                int left = GetRemainingGrappleAttackActions(attacker);
                int nextBab = GetCurrentGrappleAttackBonus(attacker);
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("↻", left > 0
                    ? $"{name} has {left} grapple attack(s) remaining (next BAB {CharacterStats.FormatMod(nextBab)})."
                    : $"{name} has no grapple attacks remaining this turn."));
                break;
            }
            case SpecialAttackType.Trip:
            {
                int left = GetRemainingTripAttackActions(attacker);
                int nextBab = GetCurrentTripAttackBonus(attacker);
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("↻", left > 0
                    ? $"{name} has {left} trip attempt(s) remaining (next BAB {CharacterStats.FormatMod(nextBab)})."
                    : $"{name} has no trip attempts remaining this turn."));
                break;
            }
            case SpecialAttackType.Disarm:
            {
                int left = GetRemainingDisarmAttackActions(attacker);
                int nextBab = GetCurrentDisarmAttackBonus(attacker);
                string hand = pay.UsedOffHand ? "off-hand" : "main-hand";
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"[Disarm] Attempt #{_disarmAttemptNumber} ({hand}) used BAB {CharacterStats.FormatMod(pay.StepBab ?? 0)}."));
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("↻", left > 0
                    ? $"{name} has {left} disarm-capable attack(s) remaining (next BAB {CharacterStats.FormatMod(nextBab)})."
                    : $"{name} has no disarm-capable attacks remaining this turn."));
                _pendingDisarmUseOffHandSelection = false;
                ClearDisarmSequenceState();
                break;
            }
            case SpecialAttackType.Sunder:
            {
                int left = GetRemainingSunderAttackActions(attacker);
                int nextBab = GetCurrentSunderAttackBonus(attacker);
                string hand = pay.UsedOffHand ? "off-hand" : "main-hand";
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"[Sunder] Attempt #{_sunderAttemptNumber} ({hand}) used BAB {CharacterStats.FormatMod(pay.StepBab ?? 0)}."));
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("↻", left > 0
                    ? $"{name} has {left} sunder-capable attack(s) remaining (next BAB {CharacterStats.FormatMod(nextBab)})."
                    : $"{name} has no sunder-capable attacks remaining this turn."));
                _pendingSunderUseOffHandSelection = false;
                ClearSunderSequenceState();
                break;
            }
        }
    }

    /// <summary>Clears the PC's special-attack selection (highlights, the disarm or sunder sequence) after a refused or spent maneuver.</summary>
    private void ClearPcManeuverSelection(SpecialAttackType type)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        _isSelectingSpecialAttack = false;
        if (type == SpecialAttackType.Sunder) { _pendingSunderUseOffHandSelection = false; ClearSunderSequenceState(); }
        else if (type == SpecialAttackType.Disarm) { _pendingDisarmUseOffHandSelection = false; ClearDisarmSequenceState(); }
    }

    /// <summary>
    /// The PC wrapper's continuation once the maneuver core settled: a refusal (nothing spent) logs and reopens the action
    /// choices; a failed ward save or an attempt foiled by an AoO ends the action; a resolved one finishes through
    /// <see cref="FinalizeSpecialAttackResolution"/>, or ends the turn when a trip or bull rush dropped the attacker.
    /// </summary>
    private void FinishPcManeuver(CharacterController attacker, CharacterController target, SpecialAttackType type, ManeuverOutcome outcome)
    {
        switch (outcome.Kind)
        {
            case ManeuverOutcomeKind.Refused:
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attacker.Stats.CharacterName} cannot use {type}: {(outcome.Reason ?? "no eligible action remaining").TrimEnd('.')}."));
                ClearPcManeuverSelection(type);
                ShowActionChoices();
                return;
            case ManeuverOutcomeKind.WardFailed:
            case ManeuverOutcomeKind.Foiled:
                ClearPcManeuverSelection(type);
                UpdateAllStatsUI();
                StartCoroutine(AfterAttackDelay(attacker, 0.8f));
                return;
        }

        if (CurrentPhase == TurnPhase.CombatOver)
        {
            FinalizeSpecialAttackResolution(attacker, target);
            return;
        }

        bool bullRush = type == SpecialAttackType.BullRushAttack || type == SpecialAttackType.BullRushCharge;
        if ((type == SpecialAttackType.Trip || bullRush) && (outcome.AttackerDown || IsSpecialAttackerDropped(attacker)))
        {
            FinalizeSpecialAttackAttackerDropped(attacker, type == SpecialAttackType.Trip ? "during the trip" : "during the bull rush");
            return;
        }

        FinalizeSpecialAttackResolution(attacker, target);
    }

    /// <summary>
    /// The PC wrapper's maneuver intent (ManeuverRequest.ForUi): a disarm or sunder names the hand chosen from the menu (the
    /// off-hand weapon, or the main weapon; none for an unarmed attempt) for the shared gate's reach (AttackLegality,
    /// CMB-104), as TryConsumeDisarmAttackAction and TryConsumeSunderAttackAction then use it; any other maneuver names
    /// none. TwoWeapon is the dual-wield state.
    /// </summary>
    private AttackIntent PcManeuverIntent(CharacterController attacker, SpecialAttackType type)
    {
        AttackIntent intent;
        if (attacker != null && (type == SpecialAttackType.Disarm || type == SpecialAttackType.Sunder))
        {
            bool offHand = type == SpecialAttackType.Disarm ? _pendingDisarmUseOffHandSelection : _pendingSunderUseOffHandSelection;
            intent = AttackIntent.ForManeuverWith(type, offHand ? attacker.GetOffHandAttackWeapon() : attacker.GetEquippedMainWeapon());
        }
        else
        {
            intent = AttackIntent.ForManeuver(type);
        }
        // The dual-wield prompt's state, read once (batch 12 step 19): a maneuver step of a two-weapon round takes the
        // main-hand (or off-hand) penalty (PHB p.160).
        intent.TwoWeapon = _isDualWielding;
        return intent;
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
    /// The PC's attack on a Wall of Ice square after the Attack button (which paid the attack step before the click, as it
    /// pays any attack): runs the catalog's attack-wall entry (AttackWallActionExecutor, batch 13 step 13, CORE-040) through
    /// RunPlayerAction with the action marked paid. The executor (GameManager.ActionCore_AttackWall) hits the stationary
    /// wall automatically (Hardness 0; PHB p.165-166 "Attacking Objects", p.299) and ends the PC's attack flow; the entry's
    /// UiAfter does the attack follow-up (1.0 s).
    /// </summary>
    private void PerformPlayerAttackOnWall(CharacterController attacker, WallOfIceAreaEffect wall, Vector2Int wallCell)
    {
        if (attacker == null || wall == null)
            return;

        ActionContext ctx = ActionContext.For(ActionIds.AttackWall, attacker, ActorSource.Ui);
        ctx.Cell = wallCell;
        ctx.Variant = AttackWallActionExecutor.WeaponVariant;
        ctx.CoreRequest = new AttackWallRequest { ActionPaid = true };
        var outcome = new ActionOutcome();
        RunPlayerAction(ctx, outcome);
        // A refusal before the core ran (the runner's pre-check runs inside the call above) leaves open the attack sequence
        // the Attack button started: close it, so no stale sequence state outlives the refused click (the panel is back).
        if (!outcome.Performed && !outcome.Refusal.Ok)
            EndAttackSequence();
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

        // Collect the intact Wall of Ice cells adjacent to the PC's space (the Strength check's square test,
        // GetAttackWallCellRefusal; WallCellDistance measures from a Large PC's whole footprint).
        HashSet<Vector2Int> allIntactWalls = WallOfIceAreaEffect.GetAllIntactWallOfIceCells();
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        bool hasAdjacentWall = false;
        foreach (Vector2Int wallCoord in allIntactWalls)
        {
            if (WallCellDistance(pc, wallCoord) > 1) continue;

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
    /// The PC's Break Wall pick: a Strength check to breach an adjacent intact Wall of Ice square (DC 15 + caster level, PHB
    /// p.299), a standard action. Runs the catalog's attack-wall entry (AttackWallActionExecutor, the Strength variant;
    /// batch 13 step 13, CORE-040) through RunPlayerAction: the standard action is paid, then the check; the entry's
    /// UiAfter does the attack follow-up (1.0 s). A refusal (not adjacent, already breached, no standard action) shows its
    /// reason and the action panel.
    /// </summary>
    public void PerformStrengthCheckOnWall(CharacterController attacker, WallOfIceAreaEffect wall, Vector2Int wallCell)
    {
        if (attacker == null || wall == null)
            return;

        ActionContext ctx = ActionContext.For(ActionIds.AttackWall, attacker, ActorSource.Ui);
        ctx.Cell = wallCell;
        ctx.Variant = AttackWallActionExecutor.StrengthVariant;
        RunPlayerAction(ctx);
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

    /// <summary>
    /// The Full Attack button (origin C, batch 12 step 17; CMB-173): declares the full attack (the full-round action, PHB
    /// p.143; Rapid Shot first when its toggle is on, PHB p.99, D9) and runs the one attack-sequence executor
    /// (RunAttackSequence) with the player's choices (UiAttackDecider: the 5-foot steps and a new target when one is needed,
    /// <see cref="PromptFullAttackStep"/>; Haste's natural-attack chooser). This coroutine keeps only the UI: the optional
    /// 5-foot step after the attacks, the completion line and the end of the turn.
    /// </summary>
    private IEnumerator PerformFullAttackWithRetargetingAndFiveFootStep(CharacterController attacker, CharacterController initialTarget,
        bool twoWeapon = false)
    {
        if (attacker == null || initialTarget == null)
        {
            ShowActionChoices();
            yield break;
        }

        // The Dual Wield button's full attack (origin D, batch 12 step 18): the same flow with both weapons' steps (PHB p.160).
        AttackSequenceRequest request = twoWeapon
            ? AttackSequenceRequest.ForUiTwoWeaponFullAttack(this, attacker, initialTarget)
            : AttackSequenceRequest.ForUiFullAttack(this, attacker, initialTarget);
        bool rangedMode = IsAttackModeRanged(attacker, null, request.Intent);
        string modeLabel = rangedMode ? "ranged" : "melee";

        // A ranged full attack first runs the executor's weapon checks (a loaded crossbow, ammunition): a shot that cannot be
        // made spends nothing (RunAttackSequence stage 4, CanShootWithWeapon). Rapid Shot is declared before the first attack
        // and commits the turn to the full attack (D9); then the full attack.
        ItemData firstWeapon = StepWeapon(request, attacker);
        if (rangedMode && !CanShootWithWeapon(attacker, firstWeapon, out _))
        {
            ShowActionChoices();
            yield break;
        }
        if (rangedMode && request.Intent.RapidShot)
            TryDeclareRapidShotForAttack(attacker, firstWeapon, true, wanted: true);
        if (!attacker.TryCommitFullAttackAction(out string fullAttackRefusal))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{attacker.Stats.CharacterName} cannot full attack: {fullAttackRefusal}."));
            ShowActionChoices();
            yield break;
        }

        request.ReportStep = step =>
        {
            CombatUI?.ShowCombatLog(step.Result.GetAttackBreakdown(step.Label));
            UpdateAllStatsUI();
            Grid.ClearAllHighlights();
            _highlightedCells.Clear();
        };

        var outcome = new AttackSequenceOutcome();
        IEnumerator run = RunAttackSequence(request, outcome);
        while (run.MoveNext())
            yield return run.Current;

        if (outcome.CombatEnded || CurrentPhase == TurnPhase.CombatOver)
            yield break;

        // The budget is fixed by the first commit; a full attack refused at its first step reads it from the ladder.
        int budget = attacker.ProgressiveAttackPool.MainHandBudget > 0
            ? attacker.ProgressiveAttackPool.MainHandBudget
            : attacker.GetMainHandAttackBudget(rangedMode ? AttackStepKind.MainHand : attacker.GetMeleeAttackStepKind());
        int left = Mathf.Max(0, budget - attacker.ProgressiveAttackPool.MainHandStepsUsed);
        // A Dual Wield full attack counts its off-hand attacks too (PHB p.160).
        if (request.InterleaveOffHand)
        {
            budget += attacker.GetOffHandAttackCount();
            left += Mathf.Max(0, attacker.GetOffHandAttackCount() - attacker.ProgressiveAttackPool.OffHandStepsUsed);
        }
        if (outcome.AttacksMade == 0 && !string.IsNullOrEmpty(outcome.StopReason) && !outcome.AttackerOutOfFight && !outcome.StartedGrapple)
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("↩", $"{attacker.Stats.CharacterName} makes no attack ({outcome.StopReason})."));
        else if (outcome.StartedGrapple && left > 0)
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("🪢", $"{attacker.Stats.CharacterName} is grappling: its {left} remaining attack(s) are not made as ordinary attacks (PHB p.156)."));
        else if (left > 0 && !string.IsNullOrEmpty(outcome.StopReason) && !outcome.AttackerOutOfFight)
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("↩", $"{attacker.Stats.CharacterName} ends full attack early ({outcome.StopReason}). {left} attack(s) unused."));

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

        CombatUI?.ShowCombatLog(CombatLogHelper.Success("✅", $"{attacker.Stats.CharacterName} completes {modeLabel} full attack ({outcome.AttacksMade}/{budget} attacks used)."));
        UpdateAllStatsUI();
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        StartCoroutine(DelayedEndActivePCTurn(1.0f));
    }

    /// <summary>
    /// The player's answer to IAttackDecider.ChooseNextStep on the Full Attack button (origin C, batch 12 step 17): the
    /// optional 5-foot step before the first attack and between attacks (PHB p.144), then the current target while it is
    /// alive, in reach or range and attackable (the hold bar, the Resilient Sphere, a known failed ward save: the shared
    /// gate), else a new target the player clicks, with a 5-foot step offered first when none is in reach; cancelling ends
    /// the full attack. The slain line of the step just made is logged here.
    /// </summary>
    internal IEnumerator PromptFullAttackStep(AttackSequenceState state, Action<AttackStepChoice> answer)
    {
        AttackSequenceRequest req = state.Request;
        CharacterController attacker = req.Attacker;
        AttackIntent intent = req.Intent;
        bool rangedMode = IsAttackModeRanged(attacker, null, intent);
        string modeLabel = rangedMode ? "ranged" : "melee";
        CharacterController currentTarget = state.CurrentTarget;

        AttackSequenceStep previous = state.TakePreviousStep();
        // The main-hand steps left, with the off-hand ones of the Dual Wield full attack (batch 12 step 18; PHB p.160).
        int remaining = RemainingSteps(req, attacker, rangedMode);
        if (previous != null && previous.Result != null && previous.Result.TargetKilled && currentTarget != null && currentTarget.Stats != null)
        {
            if (remaining > 0)
                CombatUI?.ShowCombatLog(CombatLogHelper.Death("💀", $"{currentTarget.Stats.CharacterName} is defeated! {remaining} attack(s) remaining."));
            currentTarget = null;
        }

        if (remaining <= 0)
        {
            answer?.Invoke(AttackStepChoice.StopWith("no attacks left"));
            yield break;
        }

        // D&D 3.5: a 5-foot step before a full attack, or between its attacks (PHB p.144).
        if (CanTakeFiveFootStep(attacker))
        {
            yield return StartCoroutine(WaitForOptionalFiveFootStepDuringFullAttack(
                attacker,
                state.StepsTaken == 0 ? "Before attacks:" : "Between attacks:",
                requireReachableTargetAfterStep: false,
                rangedMode: rangedMode));
        }
        if (state.StepsTaken > 0)
            yield return new WaitForSeconds(0.35f);

        if (attacker.Stats == null || CombatEndRules.IsOutOfFight(attacker))
        {
            answer?.Invoke(AttackStepChoice.StopWith("the attacker is out of the fight"));
            yield break;
        }

        bool needsRetarget = currentTarget == null
            || currentTarget.Stats == null
            || currentTarget.Stats.IsDead
            || !IsTargetInCurrentWeaponRange(attacker, currentTarget, intent)
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

            List<CharacterController> validTargets = GetValidTargetsForCurrentWeapon(attacker, intent);

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
                    answer?.Invoke(AttackStepChoice.StopWith("the player ended the full attack"));
                    yield break;
                }

                validTargets = GetValidTargetsForCurrentWeapon(attacker, intent);
            }

            if (validTargets.Count == 0)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"No valid {modeLabel} targets for {remaining} remaining attack(s)."));
                answer?.Invoke(AttackStepChoice.StopWith("no valid target"));
                yield break;
            }

            yield return StartCoroutine(WaitForFullAttackRetargetSelection(attacker, remaining));

            if (_rangedRetargetSelectionCancelled || _selectedRangedRetarget == null)
            {
                answer?.Invoke(AttackStepChoice.StopWith("the player ended the full attack"));
                yield break;
            }

            currentTarget = _selectedRangedRetarget;
            _selectedRangedRetarget = null;
            _rangedRetargetSelectionCancelled = false;
            state.Outcome.TargetSwitches++;

            CombatUI?.ShowCombatLog(CombatLogHelper.Info("🎯", $"{attacker.Stats.CharacterName} switches to {currentTarget.Stats.CharacterName}."));
        }

        answer?.Invoke(AttackStepChoice.At(currentTarget));
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
    /// AI helper: attacks an adjacent intact Wall of Ice square with the equipped weapon (or unarmed). Runs the catalog's
    /// attack-wall entry through RunAIAction (batch 13 step 13, CORE-040): the executor pays the standard action and attacks,
    /// and continues no PC UI, so nothing but the NPC's own turn coroutine ends its turn (CORE-012). Then the NPC's 1.0 s wait.
    /// </summary>
    public IEnumerator NPCAttackWallForAI(CharacterController npc, WallOfIceAreaEffect wall, Vector2Int wallCell)
    {
        if (npc == null || wall == null || wall.IsBreached(wallCell))
            yield break;

        ActionContext ctx = ActionContext.For(ActionIds.AttackWall, npc, ActorSource.AI);
        ctx.Cell = wallCell;
        ctx.Variant = AttackWallActionExecutor.WeaponVariant;
        var outcome = new ActionOutcome();
        yield return RunAIAction(ctx, outcome);
        if (!outcome.Performed)
        {
            if (outcome.Refusal.Bar == LegalityBar.Action)
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{npc.Stats.CharacterName} has no standard action available."));
            yield break;
        }
        yield return new WaitForSeconds(1.0f);
    }

    /// <summary>
    /// AI helper: a Strength check to break an adjacent intact Wall of Ice square (DC 15 + caster level, PHB p.299). Runs
    /// the catalog's attack-wall entry (the Strength variant) through RunAIAction (batch 13 step 13, CORE-040): the executor
    /// pays the standard action, then checks, and continues no PC UI. Then the NPC's 1.0 s wait.
    /// </summary>
    public IEnumerator NPCBreakWallForAI(CharacterController npc, WallOfIceAreaEffect wall, Vector2Int wallCell)
    {
        if (npc == null || wall == null || wall.IsBreached(wallCell))
            yield break;

        ActionContext ctx = ActionContext.For(ActionIds.AttackWall, npc, ActorSource.AI);
        ctx.Cell = wallCell;
        ctx.Variant = AttackWallActionExecutor.StrengthVariant;
        var outcome = new ActionOutcome();
        yield return RunAIAction(ctx, outcome);
        if (!outcome.Performed)
        {
            if (outcome.Refusal.Bar == LegalityBar.Action)
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", $"{npc.Stats.CharacterName} has no standard action available."));
            yield break;
        }
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
            // Adjacent to the NPC's whole space (a Large creature's footprint too), the square test of both wall variants.
            if (WallCellDistance(npc, wc) > 1) continue;

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
