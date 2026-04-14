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
    // 
    // D&D 3.5e PHB p.304 / DMG p.69: "A cone-shaped spell shoots away from
    // you in a quarter-circle in the direction you designate."
    //
    // Implementation uses the official D&D 3.5e grid templates:
    //
    // CARDINAL CONES (N, E, S, W):
    //   Right-triangle staircase pattern emanating from a grid intersection.
    //   Row n (at distance n squares) contains n cells, all expanding to one
    //   side of the cone axis. Width at any distance = that distance.
    //   Example 15-ft East cone (3 squares):
    //                     (3,2)
    //            (2,1)    (3,1)
    //   (1,0)   (2,0)    (3,0)
    //   [caster]
    //   Total: 1+2+3 = 6 cells.
    //
    // DIAGONAL CONES (NE, SE, SW, NW):
    //   All cells in the diagonal quadrant whose D&D 3.5e distance
    //   (alternating diagonal cost rule) is ≤ cone length.
    //   Example 15-ft NE cone (3 squares):
    //          (1,3)
    //   (1,2)  (2,2)
    //   (1,1)  (2,1)  (3,1)
    //   [caster]
    //   Total: 6 cells.
    //
    // Both patterns produce the official PHB/DMG quarter-circle templates.
    // ========================================================================

    /// <summary>
    /// Get all grid cells within a cone AoE emanating from the caster,
    /// using the official D&D 3.5e quarter-circle grid templates.
    /// Snaps direction to one of 8 compass directions and generates the
    /// appropriate pattern (cardinal staircase or diagonal quadrant fill).
    /// </summary>
    /// <param name="origin">Caster's grid position (cone emanates from here)</param>
    /// <param name="targetPos">Target position (determines cone direction)</param>
    /// <param name="lengthSquares">Length of the cone in grid squares (e.g., 3 for 15 ft)</param>
    /// <param name="grid">Reference to the game grid</param>
    /// <returns>Set of grid coordinates within the cone (excludes origin)</returns>
    public static HashSet<Vector2Int> GetConeCells(Vector2Int origin, Vector2Int targetPos, int lengthSquares, SquareGrid grid)
    {
        var cells = new HashSet<Vector2Int>();

        if (targetPos == origin) return cells;

        // Determine the primary direction (snap to 8 directions)
        Vector2 rawDir = new Vector2(targetPos.x - origin.x, targetPos.y - origin.y).normalized;
        int dirIndex = GetClosestDirectionIndex(rawDir);

        // Generate cone offsets based on cardinal vs diagonal direction
        List<Vector2Int> offsets;
        if (IsDiagonalDirection(dirIndex))
        {
            offsets = GetDiagonalConeOffsets(dirIndex, lengthSquares);
        }
        else
        {
            offsets = GetCardinalConeOffsets(dirIndex, lengthSquares);
        }

        // Apply offsets to origin and filter by grid bounds
        foreach (var offset in offsets)
        {
            Vector2Int pos = origin + offset;
            if (grid.GetCell(pos) != null)
            {
                cells.Add(pos);
            }
        }

        return cells;
    }

    // ---- Cardinal Cone Pattern ----

    /// <summary>
    /// Generate offsets for a cardinal direction cone (N, E, S, W).
    /// 
    /// For 15-ft cones (length ≤ 3): Uses the classic 1-3-3 symmetric pattern (7 cells).
    /// 
    /// For larger cones (length > 3): Uses expansion/dissipation pattern:
    ///   - Starting width: 2 squares at distance 1
    ///   - First 2/3 (expansion): width increases by 2 each row (1 on each side)
    ///   - Last 1/3 (dissipation): width decreases by 4 each row
    ///   Example 30-ft (length=6): widths = 2, 4, 6, 8, 4, 2
    ///   Example 60-ft (length=12): widths = 2, 4, 6, 8, 10, 12, 14, 16, 12, 8, 4, 2
    /// </summary>
    private static List<Vector2Int> GetCardinalConeOffsets(int dirIndex, int length)
    {
        var offsets = new List<Vector2Int>();

        if (length <= 3)
        {
            // === 15-ft cone: existing 1-3-3 pattern ===
            int halfWidth = length / 2; // 3→1

            for (int primary = 1; primary <= length; primary++)
            {
                if (primary == 1)
                {
                    // First row: just the center cell (lateral = 0)
                    offsets.Add(RotateCardinalOffset(dirIndex, primary, 0));
                }
                else
                {
                    // Subsequent rows: symmetric spread from -halfWidth to +halfWidth
                    for (int lateral = -halfWidth; lateral <= halfWidth; lateral++)
                    {
                        offsets.Add(RotateCardinalOffset(dirIndex, primary, lateral));
                    }
                }
            }
        }
        else
        {
            // === Larger cones: expansion/dissipation pattern ===
            // First 2/3 of the range is expansion, last 1/3 is dissipation
            int expansionLength = (length * 2) / 3; // 6→4, 12→8

            for (int primary = 1; primary <= length; primary++)
            {
                int width;
                if (primary <= expansionLength)
                {
                    // Expansion phase: width = 2 * primary
                    width = 2 * primary;
                }
                else
                {
                    // Dissipation phase: decrease by 4 each row from peak
                    int peakWidth = 2 * expansionLength;
                    int rowsPastPeak = primary - expansionLength;
                    width = peakWidth - (4 * rowsPastPeak);
                    if (width < 2) width = 2; // minimum width of 2
                }

                // Width is always even; half-width for symmetric spread
                int halfW = width / 2;
                // Lateral offsets: centered, from -(halfW-1) to +(halfW-1) then shift
                // For width=2: cells at lateral -0.5 and +0.5 → use lateral 0 and -1 (offset by 1)
                // Actually, for even widths centered on the axis:
                // width 2: laterals = 0, -1  (or equivalently 0 and 1 centered)
                // We center symmetrically: for width W (even), laterals from -(halfW) to (halfW-1)
                for (int lateral = -(halfW - 1); lateral <= halfW; lateral++)
                {
                    offsets.Add(RotateCardinalOffset(dirIndex, primary, lateral));
                }
            }
        }

        return offsets;
    }

    /// <summary>
    /// Rotate a base-East cone offset (primary=along axis, lateral=perpendicular)
    /// to match the specified cardinal direction. Lateral can be negative for
    /// symmetric spread.
    ///   East  (dir 2): primary → +x, lateral → +y
    ///   North (dir 0): primary → +y, lateral → −x
    ///   West  (dir 6): primary → −x, lateral → −y
    ///   South (dir 4): primary → −y, lateral → +x
    /// </summary>
    private static Vector2Int RotateCardinalOffset(int dirIndex, int primary, int lateral)
    {
        switch (dirIndex)
        {
            case 2: // East → primary=+x, lateral=+y
                return new Vector2Int(primary, lateral);
            case 0: // North → primary=+y, lateral=−x
                return new Vector2Int(-lateral, primary);
            case 6: // West → primary=−x, lateral=−y
                return new Vector2Int(-primary, -lateral);
            case 4: // South → primary=−y, lateral=+x
                return new Vector2Int(lateral, -primary);
            default:
                return Vector2Int.zero;
        }
    }

    // ---- Diagonal Cone Pattern (Quadrant Fill) ----

    /// <summary>
    /// Generate offsets for a diagonal direction cone (NE, SE, SW, NW).
    /// Fills all cells in the diagonal quadrant whose D&D 3.5e distance
    /// (using alternating diagonal cost) is ≤ the cone length.
    /// This matches the official D&D 3.5e PHB quarter-circle template
    /// for diagonal directions.
    /// </summary>
    private static List<Vector2Int> GetDiagonalConeOffsets(int dirIndex, int length)
    {
        var offsets = new List<Vector2Int>();

        // Build the base NE pattern: all (dx, dy) where dx ≥ 1, dy ≥ 1,
        // and D&D 3.5e distance ≤ length.
        // Then mirror the offsets to match the actual diagonal direction.
        for (int dx = 1; dx <= length; dx++)
        {
            for (int dy = 1; dy <= length; dy++)
            {
                if (GetDnD35Distance(dx, dy) <= length)
                {
                    offsets.Add(MirrorDiagonalOffset(dirIndex, dx, dy));
                }
            }
        }

        return offsets;
    }

    /// <summary>
    /// Mirror a base-NE cone offset to match the specified diagonal direction.
    ///   NE (dir 1): (+dx, +dy)
    ///   SE (dir 3): (+dx, −dy)
    ///   SW (dir 5): (−dx, −dy)
    ///   NW (dir 7): (−dx, +dy)
    /// </summary>
    private static Vector2Int MirrorDiagonalOffset(int dirIndex, int dx, int dy)
    {
        switch (dirIndex)
        {
            case 1: return new Vector2Int(dx, dy);    // NE
            case 3: return new Vector2Int(dx, -dy);   // SE
            case 5: return new Vector2Int(-dx, -dy);  // SW
            case 7: return new Vector2Int(-dx, dy);   // NW
            default: return Vector2Int.zero;
        }
    }

    /// <summary>
    /// Calculate D&D 3.5e grid distance between origin and offset (dx, dy).
    /// Uses the alternating diagonal cost rule: odd diagonals cost 1, even cost 2.
    /// Formula: straight_steps + diagonal_steps + floor(diagonal_steps / 2)
    /// This is equivalent to SquareGridUtils.GetDistance but works with raw offsets.
    /// </summary>
    private static int GetDnD35Distance(int dx, int dy)
    {
        int absDx = Mathf.Abs(dx);
        int absDy = Mathf.Abs(dy);
        int diag = Mathf.Min(absDx, absDy);
        int straight = Mathf.Abs(absDx - absDy);
        return straight + diag + (diag / 2);
    }

    // ---- Direction Utilities ----

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

    // ========== LINE ==========
    //
    // D&D 3.5e PHB p.176: "A line-shaped spell shoots away from you in a line
    // in the direction you designate. It starts from any corner of your square
    // and extends to the limit of its range or until it strikes a barrier."
    //
    // Implementation supports two modes:
    //
    // CARDINAL LINES (N, E, S, W):
    //   Simple 1-cell-wide line extending straight along the axis for
    //   lengthSquares distance. Uses 8-direction snapping.
    //
    // NON-CARDINAL LINES (all other angles — full 360° targeting):
    //   Intersection-based targeting per D&D 3.5e grid rules.
    //   The player targets a grid intersection (corner where 4 squares meet).
    //   The line is drawn from the nearest outside corner of the caster's square
    //   to the target intersection. Any square the line passes through is affected.
    //   Uses a DDA-style grid traversal algorithm to find affected squares.
    // ========================================================================

    /// <summary>
    /// Get all grid cells along a line from origin in an 8-direction snapped direction.
    /// Used for cardinal (and legacy diagonal) line targeting.
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

    /// <summary>
    /// Get all grid cells along a line from origin to a target grid intersection.
    /// Uses intersection-based targeting for full 360-degree line casting.
    /// The line is drawn from the nearest corner of the caster's square to
    /// the target intersection, and all squares the line passes through are affected.
    /// </summary>
    /// <param name="origin">Caster's grid position</param>
    /// <param name="targetIntersection">Target grid intersection (half-integer coords, e.g., 5.5, 3.5)</param>
    /// <param name="lengthSquares">Maximum line length in grid squares</param>
    /// <param name="grid">Reference to the game grid</param>
    /// <returns>Set of grid coordinates the line passes through (excludes caster's cell)</returns>
    public static HashSet<Vector2Int> GetLineCellsFromIntersection(
        Vector2Int origin, Vector2 targetIntersection, int lengthSquares, SquareGrid grid)
    {
        var cells = new HashSet<Vector2Int>();

        Vector2 casterCenter = new Vector2(origin.x, origin.y);
        Vector2 toTarget = targetIntersection - casterCenter;

        if (toTarget.sqrMagnitude < 0.01f) return cells;

        // Validate range: intersection must be within reach of the line
        // Use generous range check (Euclidean distance from caster center)
        float maxReach = lengthSquares + 1.0f;
        if (toTarget.magnitude > maxReach) return cells;

        // Select the closest corner of the caster's square to the target intersection
        Vector2 startCorner = GetClosestCorner(origin, targetIntersection);

        // Trace the line from the start corner to the target intersection
        // and collect all grid cells the line passes through
        cells = TraceLineThroughGrid(startCorner, targetIntersection, origin, grid);

        // Filter cells by D&D 3.5e distance to enforce the line length limit
        var result = new HashSet<Vector2Int>();
        foreach (var cell in cells)
        {
            // Use Chebyshev distance as a generous upper bound for line length
            // (actual D&D distance is always >= Chebyshev, so this is permissive)
            int chebyDist = SquareGridUtils.ChebyshevDistance(origin, cell);
            if (chebyDist <= lengthSquares)
                result.Add(cell);
        }

        return result;
    }

    /// <summary>
    /// Check if a direction vector points along a cardinal axis (N, E, S, W).
    /// Returns true if the closest 8-direction snap would be a cardinal direction.
    /// Used to decide between cardinal line mode and intersection-based line mode.
    /// </summary>
    public static bool IsCardinalLineDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude < 1e-6f) return true; // Default to cardinal
        int dirIndex = GetClosestDirectionIndex(direction.normalized);
        return dirIndex % 2 == 0; // 0=N, 2=E, 4=S, 6=W are even indices
    }

    /// <summary>
    /// Select the corner of a grid cell that is closest to a target point.
    /// A cell at (cx, cy) has corners at (cx±0.5, cy±0.5).
    /// Used to determine the starting point of a line spell.
    /// </summary>
    private static Vector2 GetClosestCorner(Vector2Int cellPos, Vector2 target)
    {
        float bestDist = float.MaxValue;
        Vector2 bestCorner = Vector2.zero;

        for (int dx = -1; dx <= 1; dx += 2)
        {
            for (int dy = -1; dy <= 1; dy += 2)
            {
                Vector2 corner = new Vector2(cellPos.x + dx * 0.5f, cellPos.y + dy * 0.5f);
                float dist = (corner - target).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestCorner = corner;
                }
            }
        }

        return bestCorner;
    }

    /// <summary>
    /// Trace a line segment from start to end through the grid and collect
    /// all cells the line passes through. Uses a boundary-crossing algorithm:
    /// finds all t-values where the line crosses vertical (x = n+0.5) or
    /// horizontal (y = m+0.5) grid boundaries, then samples the midpoint of
    /// each segment to determine which cell the line is in.
    /// </summary>
    /// <param name="start">Start point of the line (corner of caster's square)</param>
    /// <param name="end">End point of the line (target intersection)</param>
    /// <param name="casterCell">Caster's cell position (excluded from results)</param>
    /// <param name="grid">Reference to the game grid for bounds checking</param>
    /// <returns>Set of grid cells the line passes through</returns>
    private static HashSet<Vector2Int> TraceLineThroughGrid(
        Vector2 start, Vector2 end, Vector2Int casterCell, SquareGrid grid)
    {
        var cells = new HashSet<Vector2Int>();

        float dx = end.x - start.x;
        float dy = end.y - start.y;

        if (Mathf.Abs(dx) < 1e-6f && Mathf.Abs(dy) < 1e-6f) return cells;

        // Collect all t-values where the line crosses grid boundaries.
        // Cell (cx, cy) occupies [cx-0.5, cx+0.5] × [cy-0.5, cy+0.5],
        // so boundaries are at x = n+0.5 and y = m+0.5 for integer n, m.
        var tList = new List<float>();
        tList.Add(0f);

        // Vertical boundaries: x = n + 0.5
        if (Mathf.Abs(dx) > 1e-6f)
        {
            float xLo = Mathf.Min(start.x, end.x);
            float xHi = Mathf.Max(start.x, end.x);
            int nLo = Mathf.CeilToInt(xLo - 0.5f);
            int nHi = Mathf.FloorToInt(xHi - 0.5f);
            for (int n = nLo; n <= nHi; n++)
            {
                float t = (n + 0.5f - start.x) / dx;
                if (t > 1e-6f && t < 1f - 1e-6f)
                    tList.Add(t);
            }
        }

        // Horizontal boundaries: y = m + 0.5
        if (Mathf.Abs(dy) > 1e-6f)
        {
            float yLo = Mathf.Min(start.y, end.y);
            float yHi = Mathf.Max(start.y, end.y);
            int mLo = Mathf.CeilToInt(yLo - 0.5f);
            int mHi = Mathf.FloorToInt(yHi - 0.5f);
            for (int m = mLo; m <= mHi; m++)
            {
                float t = (m + 0.5f - start.y) / dy;
                if (t > 1e-6f && t < 1f - 1e-6f)
                    tList.Add(t);
            }
        }

        tList.Add(1f);
        tList.Sort();

        // Remove near-duplicate t-values (occurs when line passes through a
        // grid intersection, where vertical and horizontal crossings coincide)
        var segments = new List<float> { tList[0] };
        for (int i = 1; i < tList.Count; i++)
        {
            if (tList[i] - segments[segments.Count - 1] > 1e-5f)
                segments.Add(tList[i]);
        }

        // Sample the midpoint of each segment to determine which cell
        // the line is passing through during that interval
        for (int i = 0; i < segments.Count - 1; i++)
        {
            float midT = (segments[i] + segments[i + 1]) / 2f;
            float mx = start.x + midT * dx;
            float my = start.y + midT * dy;

            // Cell (cx, cy) contains point (mx, my) when
            // cx - 0.5 <= mx < cx + 0.5, so cx = Floor(mx + 0.5)
            int cx = Mathf.FloorToInt(mx + 0.5f);
            int cy = Mathf.FloorToInt(my + 0.5f);
            Vector2Int cellPos = new Vector2Int(cx, cy);

            if (cellPos != casterCell && grid.GetCell(cellPos) != null)
                cells.Add(cellPos);
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
