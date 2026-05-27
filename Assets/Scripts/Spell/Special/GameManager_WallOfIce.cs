// ============================================================================
// GameManager_WallOfIce.cs — Wall of Ice dual-mode casting system
//
// D&D 3.5e PHB p.299: Wall of Ice can be shaped as either:
//   1) WALL (Line Mode): An anchored plane of ice, up to one 10-ft square
//      per caster level (2 squares per CL). The caster chooses start AND
//      end points anywhere within Medium range (100 ft + 10 ft/level).
//   2) HEMISPHERE (Circle Mode): A hemisphere with radius up to
//      (3 + caster level) feet. Convert to squares: floor((3 + CL) / 5).
//      Example: CL 10 → (3+10)=13 ft → 13/5 = 2.6 → 2 squares radius.
//
// Implementation follows the Wall of Fire dual-mode pattern:
//   • Before AoE targeting, a mode selection prompt appears.
//   • Line Mode uses two-click targeting: first click = wall start, second = wall end.
//   • Circle Mode uses click for center, then radius prompt.
//
// Wall of Ice does NOT have a heat wave direction mechanic — simpler flow than
// Wall of Fire. After line/circle placement, the spell resolves immediately.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

public partial class GameManager
{
    // ─────────────────────────────────────────────────────────────
    //  Wall of Ice mode selection state
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Casting mode for Wall of Ice. Null means no mode chosen yet.
    /// </summary>
    private enum WallOfIceMode
    {
        Line,   // Straight wall — pick start + end points
        Circle  // Hemisphere — pick center + radius
    }

    /// <summary>Current Wall of Ice mode. Null = not yet selected.</summary>
    private WallOfIceMode? _pendingWallOfIceMode;

    /// <summary>
    /// For Line Mode two-click targeting: stores the first click (wall start point).
    /// Null means the first point hasn't been placed yet.
    /// </summary>
    private Vector2Int? _pendingWallOfIceLineStart;

    /// <summary>
    /// For Circle Mode: stores the chosen circle radius in squares.
    /// Null means radius hasn't been selected yet.
    /// </summary>
    private int? _pendingWallOfIceCircleRadius;

    /// <summary>
    /// For Circle Mode: stores the chosen center cell.
    /// Null means center hasn't been placed yet.
    /// </summary>
    private Vector2Int? _pendingWallOfIceCircleCenter;

    // ─────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────

    /// <summary>Reset all Wall of Ice pending state.</summary>
    private void ResetPendingWallOfIceMode()
    {
        _pendingWallOfIceMode = null;
        _pendingWallOfIceLineStart = null;
        _pendingWallOfIceCircleRadius = null;
        _pendingWallOfIceCircleCenter = null;
    }

    /// <summary>
    /// Is the current pending spell Wall of Ice?
    /// </summary>
    private bool IsPendingWallOfIce()
    {
        return _pendingSpell != null
            && string.Equals(_pendingSpell.SpellId, SpellNames.WALL_OF_ICE, StringComparison.Ordinal);
    }

    /// <summary>
    /// Is the pending spell Wall of Ice in Line Mode and waiting for the second click?
    /// </summary>
    private bool IsPendingWallOfIceLineSecondClick()
    {
        return IsPendingWallOfIce()
            && _pendingWallOfIceMode == WallOfIceMode.Line
            && _pendingWallOfIceLineStart.HasValue;
    }

    /// <summary>
    /// Maximum wall length in squares = 2 * CL (one 10-ft square per CL = 2 sq/level).
    /// PHB p.299: "up to one 10-ft. square/level"
    /// Example: CL 10 → 20 squares max length.
    /// </summary>
    private int GetWallOfIceMaxLengthSquares(CharacterController caster)
    {
        int cl = caster != null && caster.Stats != null ? caster.Stats.GetCasterLevel() : 1;
        return Mathf.Max(2, cl * 2);
    }

    /// <summary>
    /// Maximum circle/hemisphere radius in squares.
    /// PHB p.299: hemisphere radius = (3 + caster level) feet.
    /// Convert to squares: floor((3 + CL) / 5).
    /// Example: CL 5  → (3+5)  = 8 ft  → 8/5  = 1.6 → 1 square
    /// Example: CL 10 → (3+10) = 13 ft → 13/5 = 2.6 → 2 squares
    /// Example: CL 20 → (3+20) = 23 ft → 23/5 = 4.6 → 4 squares
    /// </summary>
    private int GetWallOfIceMaxCircleRadius(CharacterController caster)
    {
        int cl = caster != null && caster.Stats != null ? caster.Stats.GetCasterLevel() : 1;
        return Mathf.Max(1, (3 + cl) / 5);
    }

    /// <summary>
    /// Get the spell range in squares for Wall of Ice (Medium range).
    /// </summary>
    private int GetWallOfIceRangeSquares(CharacterController caster)
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
    /// Show Line vs Circle mode selection for Wall of Ice.
    /// Called from HandleSpellTargeting when the spell is Wall of Ice
    /// and no mode has been chosen yet.
    /// </summary>
    private void ShowWallOfIceModeSelection(CharacterController caster)
    {
        if (caster == null || caster.Stats == null || CombatUI == null)
        {
            ShowActionChoices();
            return;
        }

        int maxLength = GetWallOfIceMaxLengthSquares(caster);
        int maxRadius = GetWallOfIceMaxCircleRadius(caster);

        CurrentSubPhase = PlayerSubPhase.ChoosingAction;
        CombatUI.SetActionButtonsVisible(false);

        var options = new List<string>
        {
            $"Wall (Line) — up to {maxLength * 5} ft long ice wall",
            $"Hemisphere (Circle) — up to {maxRadius * 5}-ft radius"
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
                    ResetPendingWallOfIceMode();
                    CombatUI?.ShowCombatLog("⚠ Wall of Ice cancelled: no mode selected.");
                    ShowActionChoices();
                    return;
                }

                _pendingWallOfIceMode = selectedIndex == 0
                    ? WallOfIceMode.Line
                    : WallOfIceMode.Circle;

                string chosen = selectedIndex == 0 ? "Wall (Line)" : "Hemisphere (Circle)";
                CombatUI?.ShowCombatLog($"❄ Wall of Ice mode: {chosen}.");

                // Continue to normal AoE targeting
                BeginPendingSpellTargeting(caster);
            },
            onCancel: () =>
            {
                _pendingSpell = null;
                _pendingMetamagic = null;
                _pendingSpellFromHeldCharge = false;
                ResetPendingWallOfIceMode();
                CombatUI?.ShowCombatLog("↩ Wall of Ice cancelled (mode not selected).");
                ShowActionChoices();
            },
            titleOverride: "Wall of Ice — Choose Shape (PHB p.299)",
            bodyOverride: "Wall of Ice can be shaped as a straight wall or a hemisphere.\n"
                + $"• Wall: up to {maxLength * 5} ft long, choose start and end points\n"
                + $"• Hemisphere: circular, up to {maxRadius * 5}-ft radius, choose center point",
            optionButtonColorOverride: new Color(0.4f, 0.7f, 0.9f, 1f));
    }

    // ─────────────────────────────────────────────────────────────
    //  Circle Mode: radius selection prompt
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// After the player clicks a center cell for circle mode, show a radius picker.
    /// </summary>
    private void ShowWallOfIceRadiusSelection(CharacterController caster, Vector2Int centerCell)
    {
        if (caster == null || caster.Stats == null || CombatUI == null)
        {
            ShowActionChoices();
            return;
        }

        int maxRadius = GetWallOfIceMaxCircleRadius(caster);
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
                    _pendingWallOfIceCircleCenter = null;
                    _pendingWallOfIceCircleRadius = null;
                    CombatUI?.ShowCombatLog("⚠ Hemisphere radius selection cancelled. Pick a new center.");
                    EnterAoETargetingMode(caster, _pendingSpell);
                    return;
                }

                int chosenRadius = selectedIndex + 1; // 1-based
                _pendingWallOfIceCircleRadius = chosenRadius;
                _pendingWallOfIceCircleCenter = centerCell;

                CombatUI?.ShowCombatLog($"❄ Wall of Ice hemisphere: {chosenRadius * 5}-ft radius at ({centerCell.x}, {centerCell.y}).");

                // Compute circle (ring) cells
                HashSet<Vector2Int> circleCells = AoESystem.GetRingCells(centerCell, chosenRadius, Grid);

                if (circleCells == null || circleCells.Count == 0)
                {
                    CombatUI?.ShowCombatLog("⚠ No valid cells for hemisphere. Wall of Ice cancelled.");
                    ResetPendingWallOfIceMode();
                    _pendingSpell = null;
                    _pendingMetamagic = null;
                    _pendingSpellFromHeldCharge = false;
                    ShowActionChoices();
                    return;
                }

                // Get targets in circle area
                bool casterIsPC = caster.Team == CharacterTeam.Player;
                CharacterTeam enemyTeamType = caster.Team == CharacterTeam.Player ? CharacterTeam.Enemy : CharacterTeam.Player;
                List<CharacterController> allyTeam = GetTeamMembers(caster.Team);
                List<CharacterController> enemyTeam = GetTeamMembers(enemyTeamType);
                List<CharacterController> targets = AoESystem.GetTargetsInArea(
                    circleCells, caster, allyTeam, enemyTeam,
                    _pendingSpell.AoEFilter, casterIsPC, Grid);

                Debug.Log($"[WallOfIce] Circle mode: center=({centerCell.x},{centerCell.y}), radius={chosenRadius}, cells={circleCells.Count}, targets={targets.Count}");

                // No direction selection phase for Wall of Ice (unlike Wall of Fire).
                // Proceed directly to spell cast.

                // Exit AoE targeting
                _isAoETargeting = false;
                _currentAoECells = null;
                _lastAoEHoverPos = new Vector2Int(-1, -1);
                _lastLineHoverKey = new Vector2Int(int.MinValue, int.MinValue);
                _lastConeHoverKey = new Vector2Int(int.MinValue, int.MinValue);

                PerformAoESpellCast(caster, targets, circleCells);
            },
            onCancel: () =>
            {
                // Cancel radius selection — go back to center selection
                _pendingWallOfIceCircleCenter = null;
                _pendingWallOfIceCircleRadius = null;
                CombatUI?.ShowCombatLog("↩ Hemisphere radius selection cancelled. Pick a new center.");
                EnterAoETargetingMode(caster, _pendingSpell);
            },
            titleOverride: "Wall of Ice Hemisphere — Choose Radius",
            bodyOverride: $"Select the radius for the hemisphere of ice.\nCenter: ({centerCell.x}, {centerCell.y})",
            optionButtonColorOverride: new Color(0.4f, 0.7f, 0.9f, 1f));
    }

    // ─────────────────────────────────────────────────────────────
    //  Line Mode: compute cells between two arbitrary points
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Compute the wall cells for Line Mode given start and end points.
    /// Uses the grid line-tracing algorithm to find all cells along the line,
    /// limited by the wall's maximum length.
    /// </summary>
    private HashSet<Vector2Int> ComputeWallOfIceLineCells(
        Vector2Int startPoint, Vector2Int endPoint, int maxLengthSquares)
    {
        return AoESystem.GetLineCellsBetweenPoints(startPoint, endPoint, maxLengthSquares, Grid);
    }
}
