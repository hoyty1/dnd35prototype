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
    /// D&D 3.5e cone: starts at caster's square, extends outward.
    /// The cone's width at any distance equals the distance from the origin.
    /// We use 8 primary directions (N, NE, E, SE, S, SW, W, NW) snapped from mouse direction.
    /// 
    /// For a 15-ft (3 square) cone:
    /// - Row 1 (adjacent): 1 cell wide
    /// - Row 2: 3 cells wide
    /// - Row 3: 5 cells wide (but constrained by cone angle)
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

        // Get the two perpendicular spread directions for the cone
        // For cardinal directions, spread is the two adjacent diagonals
        // For diagonal directions, spread is the two adjacent cardinals
        Vector2Int spreadLeft, spreadRight;
        GetConeSpreadDirections(dirIndex, out spreadLeft, out spreadRight);

        // Build cone row by row from the caster
        for (int dist = 1; dist <= lengthSquares; dist++)
        {
            // Center of this row
            Vector2Int rowCenter = origin + primaryDir * dist;

            // Add center cell
            if (grid.GetCell(rowCenter) != null)
                cells.Add(rowCenter);

            // Spread width: at distance d, the cone is (2*d - 1) cells wide
            // which means (d-1) cells to each side of center
            int spreadAmount = dist - 1;

            // Add cells to the left and right of center
            for (int s = 1; s <= spreadAmount; s++)
            {
                Vector2Int leftCell = rowCenter + spreadLeft * s;
                Vector2Int rightCell = rowCenter + spreadRight * s;

                if (grid.GetCell(leftCell) != null)
                    cells.Add(leftCell);
                if (grid.GetCell(rightCell) != null)
                    cells.Add(rightCell);
            }

            // For diagonal primary directions, we also need to fill in
            // intermediate cells between rows to avoid gaps
            if (IsDiagonalDirection(dirIndex) && dist > 1)
            {
                // Fill cells along the cardinal components
                Vector2Int cardA = new Vector2Int(primaryDir.x, 0);
                Vector2Int cardB = new Vector2Int(0, primaryDir.y);

                Vector2Int fillA = origin + cardA * dist + cardB * (dist - 1);
                Vector2Int fillB = origin + cardA * (dist - 1) + cardB * dist;

                if (grid.GetCell(fillA) != null)
                    cells.Add(fillA);
                if (grid.GetCell(fillB) != null)
                    cells.Add(fillB);
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
    /// Get the spread directions perpendicular to the cone's primary direction.
    /// These determine how the cone widens as distance increases.
    /// </summary>
    private static void GetConeSpreadDirections(int dirIndex, out Vector2Int spreadLeft, out Vector2Int spreadRight)
    {
        // Directions: 0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW
        // For each primary direction, the perpendicular spread directions:
        switch (dirIndex)
        {
            case 0: // N (0,+1): spread is W(-1,0) and E(+1,0)
                spreadLeft = new Vector2Int(-1, 0);
                spreadRight = new Vector2Int(1, 0);
                break;
            case 1: // NE (+1,+1): spread is NW(-1,+1) and SE(+1,-1) ... actually perpendicular
                spreadLeft = new Vector2Int(-1, 0);  // W component
                spreadRight = new Vector2Int(0, -1);  // S component
                break;
            case 2: // E (+1,0): spread is N(0,+1) and S(0,-1)
                spreadLeft = new Vector2Int(0, 1);
                spreadRight = new Vector2Int(0, -1);
                break;
            case 3: // SE (+1,-1): spread
                spreadLeft = new Vector2Int(0, 1);   // N component
                spreadRight = new Vector2Int(-1, 0);  // W component
                break;
            case 4: // S (0,-1): spread is E(+1,0) and W(-1,0)
                spreadLeft = new Vector2Int(1, 0);
                spreadRight = new Vector2Int(-1, 0);
                break;
            case 5: // SW (-1,-1): spread
                spreadLeft = new Vector2Int(1, 0);   // E component
                spreadRight = new Vector2Int(0, 1);   // N component
                break;
            case 6: // W (-1,0): spread is S(0,-1) and N(0,+1)
                spreadLeft = new Vector2Int(0, -1);
                spreadRight = new Vector2Int(0, 1);
                break;
            case 7: // NW (-1,+1): spread
                spreadLeft = new Vector2Int(0, -1);  // S component
                spreadRight = new Vector2Int(1, 0);   // E component
                break;
            default:
                spreadLeft = new Vector2Int(-1, 0);
                spreadRight = new Vector2Int(1, 0);
                break;
        }
    }

    /// <summary>
    /// Check if a direction index represents a diagonal direction.
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
