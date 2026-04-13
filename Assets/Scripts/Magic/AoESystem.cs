// ============================================================================
// AoESystem.cs — Area of Effect targeting and calculation system
// Handles cone, burst, and line AoE shapes on the square grid.
// Uses D&D 3.5e rules for area calculation.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shape of an Area of Effect spell.
/// </summary>
public enum AoEShape
{
    None,       // Single target (no AoE)
    Burst,      // Circular burst centered on a point (e.g., Bless, Fireball)
    Cone,       // Cone emanating from caster (e.g., Burning Hands, Cone of Cold)
    Line        // Line from caster (e.g., Lightning Bolt) — future use
}

/// <summary>
/// Filter for which creatures are affected by the AoE.
/// </summary>
public enum AoETargetFilter
{
    All,            // All creatures in area (enemies and allies)
    AlliesOnly,     // Only allies of the caster
    EnemiesOnly,    // Only enemies of the caster
    Self            // Only the caster (rare)
}

/// <summary>
/// Static utility class for calculating which grid cells fall within
/// various AoE shapes on the square grid.
/// </summary>
public static class AoESystem
{
    // ========== BURST (RADIUS) ==========

    /// <summary>
    /// Get all grid cells within a burst/radius AoE centered on a point.
    /// Uses D&D 3.5e distance calculation (alternating diagonal costs).
    /// </summary>
    /// <param name="center">Center point of the burst</param>
    /// <param name="radiusSquares">Radius in grid squares (e.g., 10 for 50 ft)</param>
    /// <param name="grid">Reference to the game grid</param>
    /// <param name="includeCenter">Whether to include the center cell</param>
    /// <returns>Set of grid coordinates within the burst</returns>
    public static HashSet<Vector2Int> GetBurstCells(Vector2Int center, int radiusSquares, SquareGrid grid, bool includeCenter = true)
    {
        var cells = new HashSet<Vector2Int>();

        foreach (var kvp in grid.Cells)
        {
            Vector2Int pos = kvp.Key;
            if (!includeCenter && pos == center) continue;

            int dist = SquareGridUtils.GetDistance(center, pos);
            if (dist <= radiusSquares)
            {
                cells.Add(pos);
            }
        }

        if (includeCenter)
            cells.Add(center);

        return cells;
    }

    // ========== CONE ==========

    /// <summary>
    /// Get all grid cells within a cone AoE emanating from the caster.
    /// D&D 3.5e PHB: "A cone shoots away from you in a quarter-circle in the
    /// direction you designate." This means a 90° arc centered on the chosen direction.
    ///
    /// We snap the target direction to one of 8 primary directions (N, NE, E, SE, S, SW, W, NW).
    /// The cone includes all cells that:
    ///   1. Fall within 45° of the primary direction (quarter-circle / 90° arc)
    ///   2. Are within the cone's length measured in D&D 3.5e grid distance
    ///      (using the alternating diagonal cost rule)
    ///   3. Are not the origin cell
    ///
    /// This produces correct cone shapes for ALL 8 directions, including diagonals.
    /// A 15-ft cone (3 squares) aimed North yields a classic triangle, and aimed
    /// NE yields a filled quarter-arc in the NE quadrant.
    /// </summary>
    /// <param name="origin">Caster's grid position (cone emanates from here)</param>
    /// <param name="targetPos">Target position (determines cone direction)</param>
    /// <param name="lengthSquares">Length of the cone in squares</param>
    /// <param name="grid">Reference to the game grid</param>
    /// <returns>Set of grid coordinates within the cone (excludes origin)</returns>
    public static HashSet<Vector2Int> GetConeCells(Vector2Int origin, Vector2Int targetPos, int lengthSquares, SquareGrid grid)
    {
        var cells = new HashSet<Vector2Int>();

        if (targetPos == origin) return cells;

        // Determine the primary direction (snap to 8 directions)
        Vector2 rawDir = new Vector2(targetPos.x - origin.x, targetPos.y - origin.y).normalized;
        int dirIndex = GetClosestDirectionIndex(rawDir);
        Vector2Int primaryDir = SquareGridUtils.Directions[dirIndex];

        // Pre-compute the normalized primary direction for angle checks
        Vector2 primaryDirF = new Vector2(primaryDir.x, primaryDir.y).normalized;

        // cos(45°) ≈ 0.7071 — the half-angle of the 90° cone.
        // Cells whose direction from origin has a dot product ≥ this threshold
        // fall within the cone's arc. A small epsilon avoids floating-point
        // exclusion of cells exactly on the boundary.
        float cosHalfAngle = Mathf.Cos(45f * Mathf.Deg2Rad) - 0.001f;

        // Scan a bounding box large enough to contain all cells within range.
        // Chebyshev distance is always ≤ D&D 3.5 distance, so using
        // lengthSquares as the Chebyshev bound is safe (won't miss cells).
        for (int dx = -lengthSquares; dx <= lengthSquares; dx++)
        {
            for (int dy = -lengthSquares; dy <= lengthSquares; dy++)
            {
                if (dx == 0 && dy == 0) continue; // skip origin

                Vector2Int candidate = new Vector2Int(origin.x + dx, origin.y + dy);

                // Check grid bounds
                if (grid.GetCell(candidate) == null) continue;

                // Check D&D 3.5e distance (with alternating diagonal cost)
                int dist = SquareGridUtils.GetDistance(origin, candidate);
                if (dist < 1 || dist > lengthSquares) continue;

                // Check angle: is this cell within the 90° cone arc?
                Vector2 toCellDir = new Vector2(dx, dy).normalized;
                float dot = Vector2.Dot(toCellDir, primaryDirF);

                if (dot >= cosHalfAngle)
                {
                    cells.Add(candidate);
                }
            }
        }

        return cells;
    }

    /// <summary>
    /// Snap a continuous direction vector to one of 8 grid directions.
    /// Returns the index into SquareGridUtils.Directions.
    /// </summary>
    public static int GetClosestDirectionIndex(Vector2 direction)
    {
        if (direction == Vector2.zero) return 0; // Default: North

        float bestDot = -2f;
        int bestIndex = 0;

        for (int i = 0; i < SquareGridUtils.Directions.Length; i++)
        {
            Vector2 d = new Vector2(SquareGridUtils.Directions[i].x, SquareGridUtils.Directions[i].y).normalized;
            float dot = Vector2.Dot(direction, d);
            if (dot > bestDot)
            {
                bestDot = dot;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    /// <summary>
    /// Check if a direction index represents a diagonal direction.
    /// Diagonal directions are at odd indices: NE(1), SE(3), SW(5), NW(7).
    /// </summary>
    private static bool IsDiagonalDirection(int dirIndex)
    {
        return dirIndex == 1 || dirIndex == 3 || dirIndex == 5 || dirIndex == 7;
    }

    // ========== LINE (future use) ==========

    /// <summary>
    /// Get all grid cells along a line from origin in a direction.
    /// The line is 1 cell wide and extends for the given length.
    /// </summary>
    public static HashSet<Vector2Int> GetLineCells(Vector2Int origin, Vector2Int targetPos, int lengthSquares, SquareGrid grid)
    {
        var cells = new HashSet<Vector2Int>();

        if (targetPos == origin) return cells;

        Vector2 rawDir = new Vector2(targetPos.x - origin.x, targetPos.y - origin.y).normalized;
        int dirIndex = GetClosestDirectionIndex(rawDir);
        Vector2Int dir = SquareGridUtils.Directions[dirIndex];

        for (int i = 1; i <= lengthSquares; i++)
        {
            Vector2Int pos = origin + dir * i;
            if (grid.GetCell(pos) != null)
                cells.Add(pos);
        }

        return cells;
    }

    // ========== TARGET FILTERING ==========

    /// <summary>
    /// Filter characters in AoE cells based on the target filter and caster's faction.
    /// </summary>
    /// <param name="cellPositions">Set of grid positions in the AoE</param>
    /// <param name="caster">The character casting the spell</param>
    /// <param name="allPCs">All player characters</param>
    /// <param name="allNPCs">All NPC enemies</param>
    /// <param name="filter">Target filter (All, AlliesOnly, EnemiesOnly)</param>
    /// <param name="casterIsPC">Whether the caster is a PC</param>
    /// <param name="grid">Reference to the game grid</param>
    /// <returns>List of characters affected by the AoE</returns>
    public static List<CharacterController> GetTargetsInArea(
        HashSet<Vector2Int> cellPositions,
        CharacterController caster,
        List<CharacterController> allPCs,
        List<CharacterController> allNPCs,
        AoETargetFilter filter,
        bool casterIsPC,
        SquareGrid grid)
    {
        var targets = new List<CharacterController>();

        foreach (Vector2Int pos in cellPositions)
        {
            SquareCell cell = grid.GetCell(pos);
            if (cell == null || !cell.IsOccupied || cell.Occupant == null || cell.Occupant.Stats.IsDead)
                continue;

            CharacterController occupant = cell.Occupant;

            switch (filter)
            {
                case AoETargetFilter.All:
                    // All creatures in the area (can include caster)
                    targets.Add(occupant);
                    break;

                case AoETargetFilter.AlliesOnly:
                    // Only allies (same faction as caster)
                    if (casterIsPC && (allPCs.Contains(occupant)))
                        targets.Add(occupant);
                    else if (!casterIsPC && allNPCs.Contains(occupant))
                        targets.Add(occupant);
                    break;

                case AoETargetFilter.EnemiesOnly:
                    // Only enemies (opposite faction from caster)
                    if (casterIsPC && allNPCs.Contains(occupant))
                        targets.Add(occupant);
                    else if (!casterIsPC && allPCs.Contains(occupant))
                        targets.Add(occupant);
                    break;

                case AoETargetFilter.Self:
                    if (occupant == caster)
                        targets.Add(occupant);
                    break;
            }
        }

        return targets;
    }

    /// <summary>
    /// Check if a target position is within the spell's placement range from the caster.
    /// For burst spells, this checks if the center point is within range.
    /// Cone spells always originate from the caster, so range is implicit.
    /// </summary>
    public static bool IsWithinCastingRange(Vector2Int casterPos, Vector2Int targetPos, int rangeSquares)
    {
        if (rangeSquares < 0) return true; // Self-range, always valid
        int dist = SquareGridUtils.GetDistance(casterPos, targetPos);
        return dist <= rangeSquares;
    }
}
