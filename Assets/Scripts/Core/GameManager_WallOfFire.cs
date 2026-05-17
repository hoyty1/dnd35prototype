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
    //  Helpers
    // ─────────────────────────────────────────────────────────────

    /// <summary>Reset all Wall of Fire pending state.</summary>
    private void ResetPendingWallOfFireMode()
    {
        _pendingWallOfFireMode = null;
        _pendingWallLineStart = null;
        _pendingWallRingRadius = null;
        _pendingWallRingCenter = null;
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

                // Compute ring cells and finalize the spell
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

                // Clear any remaining highlights
                _currentAoECells = null;
                _lastAoEHoverPos = new Vector2Int(-1, -1);
                _lastLineHoverKey = new Vector2Int(int.MinValue, int.MinValue);
                _lastConeHoverKey = new Vector2Int(int.MinValue, int.MinValue);

                // Execute the spell
                PerformAoESpellCast(caster, targets, ringCells);
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
}
