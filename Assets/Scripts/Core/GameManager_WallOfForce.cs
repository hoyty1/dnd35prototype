// ============================================================================
// GameManager_WallOfForce.cs — Wall of Force casting system
//
// D&D 3.5e PHB p.298: Wall of Force creates an invisible wall of force.
//   • WALL (Line Mode): An anchored plane of force, up to one 10-ft square
//     per caster level (2 squares per CL). Close range (25 ft + 5 ft/2 levels).
//   • No damage, no saves, no SR. Simply blocks everything.
//   • Immune to all damage; only Disintegrate or Rod of Cancellation destroys it.
//   • Duration: 1 round per caster level.
//
// Wall of Force only supports Line mode (flat wall). The PHB also allows a
// sphere/hemisphere shape, but the flat wall is the standard form. This
// implementation uses Line mode with two-click targeting (start + end).
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

public partial class GameManager
{
    // ─────────────────────────────────────────────────────────────
    //  Wall of Force state
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// For Line Mode two-click targeting: stores the first click (wall start point).
    /// Null means the first point hasn't been placed yet.
    /// </summary>
    private Vector2Int? _pendingWallOfForceLineStart;

    // ─────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────

    /// <summary>Reset all Wall of Force pending state.</summary>
    private void ResetPendingWallOfForceState()
    {
        _pendingWallOfForceLineStart = null;
    }

    /// <summary>
    /// Is the current pending spell Wall of Force?
    /// </summary>
    private bool IsPendingWallOfForce()
    {
        return _pendingSpell != null
            && string.Equals(_pendingSpell.SpellId, SpellNames.WALL_OF_FORCE, StringComparison.Ordinal);
    }

    /// <summary>
    /// Is the pending spell Wall of Force waiting for the second click (end point)?
    /// </summary>
    private bool IsPendingWallOfForceLineSecondClick()
    {
        return IsPendingWallOfForce()
            && _pendingWallOfForceLineStart.HasValue;
    }

    /// <summary>
    /// Maximum wall length in squares = 2 * CL (one 10-ft square per CL = 2 sq/level).
    /// PHB p.298: "up to one 10-ft. square/level"
    /// </summary>
    private int GetWallOfForceMaxLengthSquares(CharacterController caster)
    {
        int cl = caster != null && caster.Stats != null ? caster.Stats.GetCasterLevel() : 1;
        return Mathf.Max(2, cl * 2);
    }

    /// <summary>
    /// Get the spell range in squares for Wall of Force (Close range).
    /// </summary>
    private int GetWallOfForceRangeSquares(CharacterController caster)
    {
        int cl = caster != null && caster.Stats != null ? caster.Stats.GetCasterLevel() : 1;
        return _pendingSpell != null
            ? _pendingSpell.GetRangeSquaresForCasterLevel(cl)
            : (5 + cl);
    }

    // ─────────────────────────────────────────────────────────────
    //  Compute wall cells
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Compute the wall cells for Line Mode given start and end points.
    /// </summary>
    private HashSet<Vector2Int> ComputeWallOfForceLineCells(
        Vector2Int startPoint, Vector2Int endPoint, int maxLengthSquares)
    {
        return AoESystem.GetLineCellsBetweenPoints(startPoint, endPoint, maxLengthSquares, Grid);
    }

    // ─────────────────────────────────────────────────────────────
    //  Resolution — create the Wall of Force effect
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolve Wall of Force: validate cells, create the area effect.
    /// No saves, no SR, no damage — just placement.
    /// </summary>
    private void ResolveWallOfForce(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        System.Action<string> onComplete)
    {
        int casterLevel = caster != null && caster.Stats != null ? caster.Stats.GetCasterLevel() : 1;

        // Validate proposed wall cells are not occupied
        if (aoeCells != null && aoeCells.Count > 0)
        {
            string validationError = WallOfForceAreaEffect.ValidateWallCreation(aoeCells, Grid);
            if (!string.IsNullOrEmpty(validationError))
            {
                Debug.Log($"[WallOfForce] Creation blocked: {validationError}");
                ResetPendingWallOfForceState();
                onComplete?.Invoke($"⚠ Wall of Force cannot be placed: {validationError}");
                return;
            }
        }

        // No saves for Wall of Force — proceed directly to creation
        string log = CreateWallOfForceEffect(caster, spell, targets, aoeCells);
        onComplete?.Invoke(log);
    }

    /// <summary>
    /// Creates the Wall of Force area effect.
    /// </summary>
    private string CreateWallOfForceEffect(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells)
    {
        int casterLevel = caster != null && caster.Stats != null ? caster.Stats.GetCasterLevel() : 1;

        // Duration: 1 round per caster level
        int durationRounds = Mathf.Max(1, casterLevel);

        // Compute center and direction from AoE cells
        Vector2Int centerCell = AoESystem.GetCenterOfCells(aoeCells);
        Vector2Int direction = new Vector2Int(1, 0);

        if (_pendingWallOfForceLineStart.HasValue)
        {
            Vector2Int lineStart = _pendingWallOfForceLineStart.Value;
            Vector2Int diff = centerCell - lineStart;
            if (diff == Vector2Int.zero)
                direction = new Vector2Int(1, 0);
            else
                direction = new Vector2Int(
                    diff.x != 0 ? (diff.x > 0 ? 1 : -1) : 0,
                    diff.y != 0 ? (diff.y > 0 ? 1 : -1) : 0);
        }

        Vector3 centerPosition = SquareGridUtils.GridToWorld(centerCell);
        int maxLengthSquares = Mathf.Max(2, casterLevel * 2);

        // Create the area effect
        GameObject wallObj = new GameObject("WallOfForce_Line_Area");
        wallObj.transform.position = centerPosition;

        WallOfForceAreaEffect wallEffect = wallObj.AddComponent<WallOfForceAreaEffect>();
        wallEffect.CenterPosition = centerPosition;
        wallEffect.CenterCell = centerCell;
        wallEffect.WallDirection = direction == Vector2Int.zero ? new Vector2Int(1, 0) : direction;
        wallEffect.LengthSquares = maxLengthSquares;
        wallEffect.RoundsRemaining = durationRounds;
        wallEffect.SaveDC = 0; // No save
        wallEffect.CasterLevel = casterLevel;
        wallEffect.Caster = caster;

        // Set explicit cells if we have them (from AoE targeting)
        if (aoeCells != null && aoeCells.Count > 0)
        {
            wallEffect.SetExplicitCells(aoeCells);
        }

        // Build combat log
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"🔷 {caster.Stats.CharacterName} casts Wall of Force!");
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"  Wall: {(aoeCells != null ? aoeCells.Count : 0)} sections ({(aoeCells != null ? aoeCells.Count * 5 : 0)} ft)");
        sb.AppendLine($"  Duration: {durationRounds} rounds");
        sb.AppendLine($"  • Invisible wall blocks ALL movement, spells, and line of effect");
        sb.AppendLine($"  • Immune to ALL damage — only Disintegrate destroys it");
        sb.AppendLine($"  • Cannot be dispelled");
        sb.Append("═══════════════════════════════════");

        // Clean up pending state
        ResetPendingWallOfForceState();

        return sb.ToString();
    }

    /// <summary>
    /// Check if a spell is Wall of Force.
    /// </summary>
    private static bool IsWallOfForceSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.WALL_OF_FORCE, StringComparison.Ordinal);
    }
}
