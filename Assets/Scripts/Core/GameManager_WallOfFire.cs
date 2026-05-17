// ============================================================================
// GameManager_WallOfFire.cs — Wall of Fire dual-mode casting system
//
// D&D 3.5e PHB p.298: Wall of Fire can be shaped as either:
//   1) WALL (Line Mode): An opaque sheet of flame up to 20 ft/level long and
//      20 ft high. The caster chooses start AND end points anywhere within
//      Medium range (100 ft + 10 ft/level). The wall occupies the cells
//      between those two points.
//   2) RING (Ring Mode): A ring of fire with a radius up to 5 ft per 2 caster
//      levels (max 10 ft at CL 4). The caster chooses a center point within
//      Medium range, then selects the ring radius.
//
// Implementation follows the Fire Shield / Grease dual-mode pattern:
//   • Before AoE targeting, a mode selection prompt appears.
//   • Line Mode uses two-click targeting: first click = wall start, second = wall end.
//   • Ring Mode uses click for center, then radius prompt.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

public partial class GameManager
{
    // ─────────────────────────────────────────────────────────────
    //  Wall of Fire mode selection state
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Casting mode for Wall of Fire. Null means no mode chosen yet.
    /// </summary>
    private enum WallOfFireMode
    {
        Line,   // Straight wall — pick start + end points
        Ring    // Circular ring — pick center + radius
    }

    /// <summary>Current Wall of Fire mode. Null = not yet selected.</summary>
    private WallOfFireMode? _pendingWallOfFireMode;

    /// <summary>
    /// For Line Mode two-click targeting: stores the first click (wall start point).
    /// Null means the first point hasn't been placed yet.
    /// </summary>
    private Vector2Int? _pendingWallLineStart;

    /// <summary>
    /// For Ring Mode: stores the chosen ring radius in squares.
    /// Null means radius hasn't been selected yet.
    /// </summary>
    private int? _pendingWallRingRadius;

    /// <summary>
    /// For Ring Mode: stores the chosen center cell.
    /// Null means center hasn't been placed yet.
    /// </summary>
    private Vector2Int? _pendingWallRingCenter;

    // ─────────────────────────────────────────────────────────────
    //  Heat Wave Direction Selection State (PHB p.298)
    // ─────────────────────────────────────────────────────────────
    //
    // Per D&D 3.5e PHB p.298, Wall of Fire sends out waves of heat
    // that deal damage on one side. The caster chooses which side
    // is the "hot" side when casting.
    //
    // Ring mode: "Inwards" or "Outwards"
    // Line mode: perpendicular direction stored as a Vector2 normal

    /// <summary>
    /// Heat wave direction for Ring mode: "Inwards" or "Outwards".
    /// Null means direction hasn't been selected yet.
    /// </summary>
    private string _pendingWallHeatDirectionRing;

    /// <summary>
    /// Heat wave direction for Line mode: perpendicular normal indicating the hot side.
    /// Null means direction hasn't been selected yet.
    /// </summary>
    private Vector2? _pendingWallHeatDirectionLine;

    /// <summary>
    /// True when we are in the Line mode direction selection phase
    /// (wall placed, waiting for player to choose which side is the hot side).
    /// </summary>
    private bool _pendingWallLineDirectionPhase;

    /// <summary>
    /// Stored wall cells during line direction selection phase (before final cast).
    /// </summary>
    private HashSet<Vector2Int> _pendingWallLineCellsForDirection;

    /// <summary>
    /// Stored targets during line direction selection phase (before final cast).
    /// </summary>
    private List<CharacterController> _pendingWallLineTargetsForDirection;

    // ─── Ring Mode Direction Selection Phase ───
    // After ring radius is selected, the player clicks inside/outside the ring
    // to choose inwards/outwards heat direction (mirroring the line mode UX).

    /// <summary>
    /// True when we are in the Ring mode direction selection phase
    /// (ring placed, waiting for player to click inside/outside to choose heat side).
    /// </summary>
    private bool _pendingWallRingDirectionPhase;

    /// <summary>
    /// Stored ring cells during ring direction selection phase (before final cast).
    /// </summary>
    private HashSet<Vector2Int> _pendingWallRingCellsForDirection;

    /// <summary>
    /// Stored targets during ring direction selection phase (before final cast).
    /// </summary>
    private List<CharacterController> _pendingWallRingTargetsForDirection;

    /// <summary>
    /// Stored ring center during ring direction selection phase.
    /// </summary>
    private Vector2Int _pendingWallRingCenterForDirection;

    /// <summary>
    /// Stored ring radius during ring direction selection phase.
    /// </summary>
    private int _pendingWallRingRadiusForDirection;

    // ─────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────

    /// <summary>Reset all Wall of Fire pending state.</summary>
    private void ResetPendingWallOfFireMode()
    {
        _pendingWallOfFireMode = null;
        _pendingWallLineStart = null;
        _pendingWallRingRadius = null;
        _pendingWallRingCenter = null;
        _pendingWallHeatDirectionRing = null;
        _pendingWallHeatDirectionLine = null;
        _pendingWallLineDirectionPhase = false;
        _pendingWallLineCellsForDirection = null;
        _pendingWallLineTargetsForDirection = null;
        _pendingWallRingDirectionPhase = false;
        _pendingWallRingCellsForDirection = null;
        _pendingWallRingTargetsForDirection = null;
        _pendingWallRingCenterForDirection = Vector2Int.zero;
        _pendingWallRingRadiusForDirection = 0;
    }

    /// <summary>
    /// Is the current pending spell Wall of Fire?
    /// </summary>
    private bool IsPendingWallOfFire()
    {
        return _pendingSpell != null
            && string.Equals(_pendingSpell.SpellId, SpellNames.WALL_OF_FIRE, StringComparison.Ordinal);
    }

    /// <summary>
    /// Is the pending spell Wall of Fire in Line Mode and waiting for the second click?
    /// </summary>
    private bool IsPendingWallOfFireLineSecondClick()
    {
        return IsPendingWallOfFire()
            && _pendingWallOfFireMode == WallOfFireMode.Line
            && _pendingWallLineStart.HasValue;
    }

    /// <summary>
    /// Maximum wall length in squares = 4 * CL (20 ft/level => 4 sq/level).
    /// </summary>
    private int GetWallOfFireMaxLengthSquares(CharacterController caster)
    {
        int cl = caster != null && caster.Stats != null ? caster.Stats.GetCasterLevel() : 1;
        return Mathf.Max(2, cl * 4);
    }

    /// <summary>
    /// Maximum ring radius in squares = CL / 2 (5 ft per 2 CL).
    /// Per PHB: radius up to 5 ft per 2 CL. At CL 7 = 15 ft = 3 sq, CL 10 = 25 ft = 5 sq.
    /// </summary>
    private int GetWallOfFireMaxRingRadius(CharacterController caster)
    {
        int cl = caster != null && caster.Stats != null ? caster.Stats.GetCasterLevel() : 1;
        return Mathf.Max(1, cl / 2);
    }

    /// <summary>
    /// Get the spell range in squares for Wall of Fire (Medium range).
    /// </summary>
    private int GetWallOfFireRangeSquares(CharacterController caster)
    {
        int cl = caster != null && caster.Stats != null ? caster.Stats.GetCasterLevel() : 1;
        return _pendingSpell != null
            ? _pendingSpell.GetRangeSquaresForCasterLevel(cl)
            : (20 + 2 * cl);
    }

    // ─────────────────────────────────────────────────────────────
    //  Mode selection prompt (shown before AoE targeting)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Show Line vs Ring mode selection for Wall of Fire.
    /// Called from HandleSpellTargeting when the spell is Wall of Fire
    /// and no mode has been chosen yet.
    /// </summary>
    private void ShowWallOfFireModeSelection(CharacterController caster)
    {
        if (caster == null || caster.Stats == null || CombatUI == null)
        {
            ShowActionChoices();
            return;
        }

        int maxLength = GetWallOfFireMaxLengthSquares(caster);
        int maxRadius = GetWallOfFireMaxRingRadius(caster);

        CurrentSubPhase = PlayerSubPhase.ChoosingAction;
        CombatUI.SetActionButtonsVisible(false);

        var options = new List<string>
        {
            $"Wall (Line) — up to {maxLength * 5} ft long straight wall",
            $"Ring — circular ring, up to {maxRadius * 5}-ft radius"
        };

        CombatUI.ShowPickUpItemSelection(
            actorName: caster.Stats.CharacterName,
            itemOptions: options,
            onSelect: selectedIndex =>
            {
                if (selectedIndex < 0 || selectedIndex >= options.Count)
                {
                    _pendingSpell = null;
                    _pendingMetamagic = null;
                    _pendingSpellFromHeldCharge = false;
                    ResetPendingWallOfFireMode();
                    CombatUI?.ShowCombatLog("⚠ Wall of Fire cancelled: no mode selected.");
                    ShowActionChoices();
                    return;
                }

                _pendingWallOfFireMode = selectedIndex == 0
                    ? WallOfFireMode.Line
                    : WallOfFireMode.Ring;

                string chosen = selectedIndex == 0 ? "Wall (Line)" : "Ring";
                CombatUI?.ShowCombatLog($"🔥 Wall of Fire mode: {chosen}.");

                // Continue to normal AoE targeting
                BeginPendingSpellTargeting(caster);
            },
            onCancel: () =>
            {
                _pendingSpell = null;
                _pendingMetamagic = null;
                _pendingSpellFromHeldCharge = false;
                ResetPendingWallOfFireMode();
                CombatUI?.ShowCombatLog("↩ Wall of Fire cancelled (mode not selected).");
                ShowActionChoices();
            },
            titleOverride: "Wall of Fire — Choose Shape (PHB p.298)",
            bodyOverride: "Wall of Fire can be shaped as a straight wall or a ring of fire.\n"
                + $"• Wall: up to {maxLength * 5} ft long, choose start and end points\n"
                + $"• Ring: circular, up to {maxRadius * 5}-ft radius, choose center point",
            optionButtonColorOverride: new Color(0.75f, 0.25f, 0.1f, 1f));
    }

    // ─────────────────────────────────────────────────────────────
    //  Ring Mode: radius selection prompt
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// After the player clicks a center cell for ring mode, show a radius picker.
    /// </summary>
    private void ShowWallOfFireRadiusSelection(CharacterController caster, Vector2Int centerCell)
    {
        if (caster == null || caster.Stats == null || CombatUI == null)
        {
            ShowActionChoices();
            return;
        }

        int maxRadius = GetWallOfFireMaxRingRadius(caster);
        var options = new List<string>();

        // Offer radius choices from 1 to maxRadius squares
        for (int r = 1; r <= maxRadius; r++)
        {
            options.Add($"{r * 5}-ft radius ({r} square{(r > 1 ? "s" : "")})");
        }

        // Pause AoE targeting while selecting radius
        _isAoETargeting = false;

        CurrentSubPhase = PlayerSubPhase.ChoosingAction;
        CombatUI.SetActionButtonsVisible(false);

        CombatUI.ShowPickUpItemSelection(
            actorName: caster.Stats.CharacterName,
            itemOptions: options,
            onSelect: selectedIndex =>
            {
                if (selectedIndex < 0 || selectedIndex >= options.Count)
                {
                    // Cancel — go back to center selection
                    _pendingWallRingCenter = null;
                    _pendingWallRingRadius = null;
                    CombatUI?.ShowCombatLog("⚠ Ring radius selection cancelled. Pick a new center.");
                    EnterAoETargetingMode(caster, _pendingSpell);
                    return;
                }

                int chosenRadius = selectedIndex + 1; // 1-based
                _pendingWallRingRadius = chosenRadius;
                _pendingWallRingCenter = centerCell;

                CombatUI?.ShowCombatLog($"🔥 Wall of Fire ring: {chosenRadius * 5}-ft radius at ({centerCell.x}, {centerCell.y}).");

                // Compute ring cells
                HashSet<Vector2Int> ringCells = AoESystem.GetRingCells(centerCell, chosenRadius, Grid);

                if (ringCells == null || ringCells.Count == 0)
                {
                    CombatUI?.ShowCombatLog("⚠ No valid cells for ring. Wall of Fire cancelled.");
                    ResetPendingWallOfFireMode();
                    _pendingSpell = null;
                    _pendingMetamagic = null;
                    _pendingSpellFromHeldCharge = false;
                    ShowActionChoices();
                    return;
                }

                // Get targets in ring area
                bool casterIsPC = caster.Team == CharacterTeam.Player;
                CharacterTeam enemyTeamType = caster.Team == CharacterTeam.Player ? CharacterTeam.Enemy : CharacterTeam.Player;
                List<CharacterController> allyTeam = GetTeamMembers(caster.Team);
                List<CharacterController> enemyTeam = GetTeamMembers(enemyTeamType);
                List<CharacterController> targets = AoESystem.GetTargetsInArea(
                    ringCells, caster, allyTeam, enemyTeam,
                    _pendingSpell.AoEFilter, casterIsPC, Grid);

                Debug.Log($"[WallOfFire] Ring mode: center=({centerCell.x},{centerCell.y}), radius={chosenRadius}, cells={ringCells.Count}, targets={targets.Count}");

                // Chain to click-based heat wave direction selection phase (PHB p.298)
                // Player clicks inside/outside the ring to choose inwards/outwards
                EnterWallOfFireRingDirectionPhase(caster, centerCell, chosenRadius, ringCells, targets);
            },
            onCancel: () =>
            {
                // Cancel radius selection — go back to center selection
                _pendingWallRingCenter = null;
                _pendingWallRingRadius = null;
                CombatUI?.ShowCombatLog("↩ Ring radius selection cancelled. Pick a new center.");
                EnterAoETargetingMode(caster, _pendingSpell);
            },
            titleOverride: "Wall of Fire Ring — Choose Radius",
            bodyOverride: $"Select the radius for the ring of fire.\nCenter: ({centerCell.x}, {centerCell.y})",
            optionButtonColorOverride: new Color(0.75f, 0.25f, 0.1f, 1f));
    }

    // ─────────────────────────────────────────────────────────────
    //  Line Mode: compute cells between two arbitrary points
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Compute the wall cells for Line Mode given start and end points.
    /// Uses the grid line-tracing algorithm to find all cells along the line,
    /// limited by the wall's maximum length.
    /// </summary>
    private HashSet<Vector2Int> ComputeWallOfFireLineCells(
        Vector2Int startPoint, Vector2Int endPoint, int maxLengthSquares)
    {
        // Delegate to AoESystem's new method for arbitrary-origin line cells
        return AoESystem.GetLineCellsBetweenPoints(startPoint, endPoint, maxLengthSquares, Grid);
    }

    // ─────────────────────────────────────────────────────────────
    //  Heat Wave Direction Selection — Ring Mode (PHB p.298)
    //  Click-based: mouse inside ring = Inwards, outside = Outwards
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Check if we are in the ring mode direction selection phase.
    /// </summary>
    private bool IsPendingWallOfFireRingDirectionPhase()
    {
        bool result = IsPendingWallOfFire()
            && _pendingWallOfFireMode == WallOfFireMode.Ring
            && _pendingWallRingDirectionPhase
            && _pendingWallRingCellsForDirection != null;
        return result;
    }

    /// <summary>
    /// Enter the ring mode direction selection phase after the ring radius is chosen.
    /// Player moves mouse inside/outside ring to preview heat direction, clicks to confirm.
    /// </summary>
    private void EnterWallOfFireRingDirectionPhase(
        CharacterController caster, Vector2Int centerCell, int chosenRadius,
        HashSet<Vector2Int> ringCells, List<CharacterController> targets)
    {
        _pendingWallRingDirectionPhase = true;
        _pendingWallRingCellsForDirection = ringCells;
        _pendingWallRingTargetsForDirection = targets;
        _pendingWallRingCenterForDirection = centerCell;
        _pendingWallRingRadiusForDirection = chosenRadius;
        _pendingWallHeatDirectionRing = null;

        // Re-enable AoE targeting so UpdateAoEPreview runs and clicks route to HandleAoETargetClick
        _isAoETargeting = true;

        // CRITICAL: Restore SubPhase to SelectingAoETarget so the Update loop
        // calls UpdateAoEPreview() and routes clicks to HandleAoETargetClick().
        // The radius selection prompt set SubPhase to ChoosingAction; we must
        // switch back so preview + click handling work during direction selection.
        CurrentSubPhase = PlayerSubPhase.SelectingAoETarget;

        // Reset hover key so the first mouse position triggers a preview update
        _lastLineHoverKey = new Vector2Int(int.MinValue, int.MinValue);

        // Highlight the ring cells
        if (ringCells != null)
        {
            foreach (Vector2Int cell in ringCells)
            {
                SquareCell sc = Grid.GetCell(cell);
                if (sc != null) sc.SetHighlight(HighlightType.AoETarget);
            }
        }

        CombatUI?.SetTurnIndicator(
            $"✦ Wall of Fire (Ring): Click INSIDE ring for Inwards heat, OUTSIDE for Outwards | Right-click to cancel");
        CombatUI?.ShowCombatLog("🔥 Ring placed! Click inside the ring for Inwards heat, or outside for Outwards. Right-click to cancel.");

        Debug.Log($"[WallOfFire][RingDir] Entered ring direction selection phase. center=({centerCell.x},{centerCell.y}), radius={chosenRadius}, ringCells={ringCells?.Count ?? 0}, subPhase={CurrentSubPhase}");
    }

    /// <summary>
    /// Determine if a grid position is inside the ring (closer to center than the ring radius).
    /// Returns true for inside, false for outside or on the ring.
    /// </summary>
    private bool IsInsideRing(Vector2Int point, Vector2Int center, int radius)
    {
        int dist = SquareGridUtils.GetDistance(center, point);
        return dist < radius;
    }

    /// <summary>
    /// Get heat wave preview cells for the ring: cells within 2 squares of the ring
    /// on the specified side (inside or outside).
    /// </summary>
    private HashSet<Vector2Int> GetHeatWaveCellsForRingSide(
        Vector2Int center, int radius, HashSet<Vector2Int> ringCells, bool inwards, int distanceSquares = 2)
    {
        var heatCells = new HashSet<Vector2Int>();
        if (ringCells == null || ringCells.Count == 0) return heatCells;

        foreach (Vector2Int ringCell in ringCells)
        {
            for (int dx = -distanceSquares; dx <= distanceSquares; dx++)
            {
                for (int dy = -distanceSquares; dy <= distanceSquares; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    Vector2Int candidate = new Vector2Int(ringCell.x + dx, ringCell.y + dy);
                    if (ringCells.Contains(candidate)) continue; // Skip ring cells themselves

                    int distFromCenter = SquareGridUtils.GetDistance(center, candidate);
                    int distFromRing = SquareGridUtils.GetDistance(ringCell, candidate);
                    if (distFromRing > distanceSquares) continue;

                    // Inwards = closer to center than ring, Outwards = farther
                    bool candidateInside = distFromCenter < radius;
                    if (inwards && candidateInside)
                        heatCells.Add(candidate);
                    else if (!inwards && !candidateInside)
                        heatCells.Add(candidate);
                }
            }
        }
        return heatCells;
    }

    /// <summary>
    /// Confirm the ring heat wave direction based on click position and finalize the spell.
    /// </summary>
    private void ConfirmWallOfFireRingDirection(CharacterController caster, bool inwards)
    {
        if (!_pendingWallRingDirectionPhase || _pendingWallRingCellsForDirection == null)
            return;

        _pendingWallHeatDirectionRing = inwards ? "Inwards" : "Outwards";

        string chosen = _pendingWallHeatDirectionRing;
        CombatUI?.ShowCombatLog($"🔥 Wall of Fire heat waves: {chosen}.");
        Debug.Log($"[WallOfFire] Ring heat direction confirmed: {chosen}, center=({_pendingWallRingCenterForDirection.x},{_pendingWallRingCenterForDirection.y}), radius={_pendingWallRingRadiusForDirection}");

        // Exit direction selection phase
        _pendingWallRingDirectionPhase = false;

        // Exit AoE targeting
        _isAoETargeting = false;
        _currentAoECells = null;
        _lastAoEHoverPos = new Vector2Int(-1, -1);
        _lastLineHoverKey = new Vector2Int(int.MinValue, int.MinValue);
        _lastConeHoverKey = new Vector2Int(int.MinValue, int.MinValue);

        // Execute the spell with stored cells and targets
        PerformAoESpellCast(caster, _pendingWallRingTargetsForDirection, _pendingWallRingCellsForDirection);
    }

    // ─────────────────────────────────────────────────────────────
    //  Heat Wave Direction Selection — Line Mode (PHB p.298)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Check if we are in the line mode direction selection phase.
    /// </summary>
    private bool IsPendingWallOfFireLineDirectionPhase()
    {
        return IsPendingWallOfFire()
            && _pendingWallOfFireMode == WallOfFireMode.Line
            && _pendingWallLineDirectionPhase
            && _pendingWallLineCellsForDirection != null;
    }

    /// <summary>
    /// Determine which side of a line a point is on.
    /// Returns +1 for one side, -1 for the other, 0 if on the line.
    /// Uses the cross product of the line direction and the vector from line start to point.
    /// </summary>
    private static int GetSideOfLine(Vector2Int lineStart, Vector2Int lineEnd, Vector2Int point)
    {
        // Cross product: (lineEnd - lineStart) × (point - lineStart)
        float cross = (float)(lineEnd.x - lineStart.x) * (point.y - lineStart.y)
                    - (float)(lineEnd.y - lineStart.y) * (point.x - lineStart.x);
        if (cross > 0.001f) return 1;
        if (cross < -0.001f) return -1;
        return 0;
    }

    /// <summary>
    /// Get the perpendicular normal vector for a side of the line.
    /// side > 0 → left normal, side < 0 → right normal.
    /// </summary>
    private static Vector2 GetPerpendicularNormal(Vector2Int lineStart, Vector2Int lineEnd, int side)
    {
        Vector2 dir = new Vector2(lineEnd.x - lineStart.x, lineEnd.y - lineStart.y);
        if (dir.sqrMagnitude < 0.0001f) return Vector2.right;

        dir.Normalize();

        // Left perpendicular: (-dy, dx), Right perpendicular: (dy, -dx)
        if (side > 0)
            return new Vector2(-dir.y, dir.x);
        else
            return new Vector2(dir.y, -dir.x);
    }

    /// <summary>
    /// Get cells on one side of the wall line within a given distance (in squares).
    /// Used for heat wave AoE preview during direction selection.
    /// </summary>
    private HashSet<Vector2Int> GetHeatWaveCellsForLineSide(
        Vector2Int lineStart, Vector2Int lineEnd,
        HashSet<Vector2Int> wallCells, int side, int distanceSquares = 2)
    {
        var heatCells = new HashSet<Vector2Int>();
        if (wallCells == null || wallCells.Count == 0 || side == 0) return heatCells;

        // For each wall cell, check adjacent cells up to distanceSquares away
        // and include them if they are on the correct side of the line
        foreach (Vector2Int wallCell in wallCells)
        {
            for (int dx = -distanceSquares; dx <= distanceSquares; dx++)
            {
                for (int dy = -distanceSquares; dy <= distanceSquares; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    Vector2Int candidate = new Vector2Int(wallCell.x + dx, wallCell.y + dy);
                    if (wallCells.Contains(candidate)) continue; // Skip wall cells themselves

                    int candidateSide = GetSideOfLine(lineStart, lineEnd, candidate);
                    if (candidateSide == side)
                    {
                        // Check distance from nearest wall cell
                        int dist = SquareGridUtils.GetDistance(wallCell, candidate);
                        if (dist <= distanceSquares)
                        {
                            heatCells.Add(candidate);
                        }
                    }
                }
            }
        }
        return heatCells;
    }

    /// <summary>
    /// Enter the line mode direction selection phase after the wall line is placed.
    /// </summary>
    private void EnterWallOfFireLineDirectionPhase(
        CharacterController caster,
        HashSet<Vector2Int> wallCells,
        List<CharacterController> targets)
    {
        _pendingWallLineDirectionPhase = true;
        _pendingWallLineCellsForDirection = wallCells;
        _pendingWallLineTargetsForDirection = targets;
        _pendingWallHeatDirectionLine = null;

        // Keep AoE targeting active so UpdateAoEPreview can show heat wave preview
        _isAoETargeting = true;

        // Highlight the wall cells
        if (wallCells != null)
        {
            foreach (Vector2Int cell in wallCells)
            {
                SquareCell sc = Grid.GetCell(cell);
                if (sc != null) sc.SetHighlight(HighlightType.AoETarget);
            }
        }

        int maxLen = GetWallOfFireMaxLengthSquares(caster);
        CombatUI?.SetTurnIndicator(
            $"✦ Wall of Fire (Line): Move mouse to choose HEAT WAVE side — click to confirm | Right-click to cancel");
        CombatUI?.ShowCombatLog("🔥 Wall placed! Move mouse to choose which side radiates heat (2d4 fire within 10 ft). Click to confirm.");

        Debug.Log($"[WallOfFire] Entered line direction selection phase. wallCells={wallCells?.Count ?? 0}");
    }

    /// <summary>
    /// Confirm the line heat wave direction and finalize the spell.
    /// </summary>
    private void ConfirmWallOfFireLineDirection(CharacterController caster, int side)
    {
        if (!_pendingWallLineDirectionPhase || _pendingWallLineCellsForDirection == null)
            return;

        Vector2Int lineStart = _pendingWallLineStart ?? Vector2Int.zero;
        // Compute end from center of wall cells as approximation
        Vector2Int lineEnd = lineStart;
        if (_pendingWallLineCellsForDirection.Count > 0)
        {
            // Find the furthest cell from start along the wall
            int maxDist = 0;
            foreach (var cell in _pendingWallLineCellsForDirection)
            {
                int d = SquareGridUtils.GetDistance(lineStart, cell);
                if (d > maxDist)
                {
                    maxDist = d;
                    lineEnd = cell;
                }
            }
        }

        _pendingWallHeatDirectionLine = GetPerpendicularNormal(lineStart, lineEnd, side);

        string sideLabel = side > 0 ? "left" : "right";
        CombatUI?.ShowCombatLog($"🔥 Wall of Fire heat waves: {sideLabel} side (hot).");
        Debug.Log($"[WallOfFire] Line heat direction confirmed: side={side}, normal={_pendingWallHeatDirectionLine}");

        // Exit direction selection phase
        _pendingWallLineDirectionPhase = false;

        // Exit AoE targeting
        _isAoETargeting = false;
        _currentAoECells = null;
        _lastAoEHoverPos = new Vector2Int(-1, -1);
        _lastConeHoverKey = new Vector2Int(int.MinValue, int.MinValue);
        _lastLineHoverKey = new Vector2Int(int.MinValue, int.MinValue);

        // Execute the spell with stored cells and targets
        PerformAoESpellCast(caster, _pendingWallLineTargetsForDirection, _pendingWallLineCellsForDirection);
    }
}
