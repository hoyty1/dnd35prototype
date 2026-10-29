using UnityEngine;

/// <summary>
/// Utility class for square grid math with D&D 3.5 diagonal movement rules.
/// Uses simple (x, y) coordinates. 1 square = 5 feet.
/// 
/// D&D 3.5 Diagonal Movement Cost:
/// - Orthogonal movement always costs 1 square.
/// - Diagonal movement uses alternating 1/2 cost pattern:
///   1st diagonal = 1 square, 2nd = 2 squares, 3rd = 1, 4th = 2, etc.
/// </summary>
public static class SquareGridUtils
{
    /// <summary>Size of each square cell in world units.</summary>
    public const float CellSize = 1.0f;

    /// <summary>
    /// The 8 direction offsets for square grid neighbors.
    /// Order: N, NE, E, SE, S, SW, W, NW
    /// </summary>
    public static readonly Vector2Int[] Directions = new Vector2Int[]
    {
        new Vector2Int( 0, +1), // N
        new Vector2Int(+1, +1), // NE
        new Vector2Int(+1,  0), // E
        new Vector2Int(+1, -1), // SE
        new Vector2Int( 0, -1), // S
        new Vector2Int(-1, -1), // SW
        new Vector2Int(-1,  0), // W
        new Vector2Int(-1, +1), // NW
    };

    /// <summary>
    /// Direction names corresponding to the Directions array.
    /// </summary>
    public static readonly string[] DirectionNames = new string[]
    {
        "N", "NE", "E", "SE", "S", "SW", "W", "NW"
    };

    /// <summary>
    /// Convert grid coordinates (x, y) to world position.
    /// Each cell is positioned at (x * CellSize, y * CellSize, 0) in world space.
    /// </summary>
    public static Vector3 GridToWorld(int x, int y)
    {
        return new Vector3(x * CellSize, y * CellSize, 0f);
    }

    /// <summary>
    /// Convert grid coordinates to world position.
    /// </summary>
    public static Vector3 GridToWorld(Vector2Int coords)
    {
        return GridToWorld(coords.x, coords.y);
    }

    /// <summary>
    /// Convert world position to the nearest grid coordinate.
    /// </summary>
    public static Vector2Int WorldToGrid(Vector3 worldPos)
    {
        int x = Mathf.RoundToInt(worldPos.x / CellSize);
        int y = Mathf.RoundToInt(worldPos.y / CellSize);
        return new Vector2Int(x, y);
    }

    /// <summary>
    /// Calculate the D&D 3.5 distance between two grid positions using
    /// the alternating diagonal cost rule.
    /// 
    /// Algorithm: Separate movement into orthogonal and diagonal steps.
    /// The number of diagonal steps is min(dx, dy), orthogonal is |dx - dy|.
    /// Apply alternating 1/2 cost to each diagonal step.
    /// Total cost = orthogonal + sum of diagonal costs.
    /// 
    /// Odd-numbered diagonals (1st, 3rd, 5th...) cost 1 square.
    /// Even-numbered diagonals (2nd, 4th, 6th...) cost 2 squares.
    /// </summary>
    public static int GetDistance(Vector2Int from, Vector2Int to)
    {
        int dx = Mathf.Abs(to.x - from.x);
        int dy = Mathf.Abs(to.y - from.y);

        int diagonalSteps = Mathf.Min(dx, dy);
        int orthogonalSteps = Mathf.Abs(dx - dy);

        // Calculate diagonal cost with alternating 1/2 pattern
        int diagonalCost = GetDiagonalCost(diagonalSteps);

        return orthogonalSteps + diagonalCost;
    }

    /// <summary>
    /// Calculate the total cost of a number of diagonal steps using
    /// D&D 3.5 alternating 1/2 rule.
    /// Odd diagonals cost 1, even diagonals cost 2.
    /// </summary>
    public static int GetDiagonalCost(int diagonalSteps)
    {
        return GetDiagonalCost(diagonalSteps, 0);
    }

    /// <summary>
    /// Calculate the total cost of diagonal steps, starting from a given
    /// diagonal count (for continuing a path).
    /// </summary>
    /// <param name="diagonalSteps">Number of new diagonal steps</param>
    /// <param name="previousDiagonals">Number of diagonals already taken this move</param>
    public static int GetDiagonalCost(int diagonalSteps, int previousDiagonals)
    {
        int cost = 0;
        for (int i = 0; i < diagonalSteps; i++)
        {
            int diagNumber = previousDiagonals + i + 1; // 1-based
            if (diagNumber % 2 == 0)
                cost += 2; // Even diagonals cost 2
            else
                cost += 1; // Odd diagonals cost 1
        }
        return cost;
    }

    /// <summary>
    /// Get all 8 neighbor coordinates around a given position.
    /// Does NOT check grid bounds.
    /// </summary>
    public static Vector2Int[] GetNeighbors(Vector2Int coord)
    {
        Vector2Int[] neighbors = new Vector2Int[8];
        for (int i = 0; i < 8; i++)
        {
            neighbors[i] = coord + Directions[i];
        }
        return neighbors;
    }

    /// <summary>
    /// Check if two coordinates are adjacent (including diagonals).
    /// </summary>
    public static bool IsAdjacent(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return dx <= 1 && dy <= 1 && (dx + dy > 0);
    }

    /// <summary>
    /// Check if a step from one cell to another is diagonal.
    /// </summary>
    public static bool IsDiagonalStep(Vector2Int from, Vector2Int to)
    {
        int dx = Mathf.Abs(to.x - from.x);
        int dy = Mathf.Abs(to.y - from.y);
        return dx == 1 && dy == 1;
    }

    /// <summary>
    /// Chebyshev (chessboard) distance where all adjacent squares, including diagonals,
    /// count as 1. Used for reach/threat calculations that intentionally ignore
    /// D&D 3.5 movement diagonal cost alternation.
    /// </summary>
    public static int GetChebyshevDistance(Vector2Int from, Vector2Int to)
    {
        int dx = Mathf.Abs(to.x - from.x);
        int dy = Mathf.Abs(to.y - from.y);
        return Mathf.Max(dx, dy);
    }

    /// <summary>
    /// Backward-compatible alias for GetChebyshevDistance.
    /// </summary>
    public static int ChebyshevDistance(Vector2Int a, Vector2Int b)
    {
        return GetChebyshevDistance(a, b);
    }

    /// <summary>
    /// Get the direction vector from one cell to an adjacent cell.
    /// Returns Vector2Int.zero if not adjacent.
    /// </summary>
    public static Vector2Int GetDirection(Vector2Int from, Vector2Int to)
    {
        Vector2Int diff = to - from;
        if (Mathf.Abs(diff.x) <= 1 && Mathf.Abs(diff.y) <= 1 && diff != Vector2Int.zero)
            return diff;
        return Vector2Int.zero;
    }

    /// <summary>
    /// The cost <see cref="CalculatePathCost"/> gives a path that a barrier blocks: beyond every movement budget, so a
    /// path through a wall or across a sphere's boundary never fits (batch 12 step 23, GRID-013).
    /// </summary>
    public const int BlockedPathCost = 100000;

    /// <summary>An intact Wall of Ice or Wall of Force square: no creature moves into it (PHB p.298-299).</summary>
    public static bool IsCellBlockedByWall(Vector2Int cell)
        => WallOfIceAreaEffect.DoesCellBlockMovement(cell) || WallOfForceAreaEffect.DoesCellBlockMovement(cell);

    /// <summary>
    /// The one barrier rule of a single step, shared by the A* (SquareGrid.FindPathAoOAware), the path cost
    /// (<see cref="CalculatePathCost"/>), the move highlight and the 5-foot step (MovementService) and the step executor
    /// (CharacterController.MoveAlongPath, CharacterController.FiveFootStep) (batch 12 step 23, GRID-013): a step may not
    /// cross a Resilient Sphere's boundary (PHB p.258), enter an intact Wall of Ice or Wall of Force square, or cut a
    /// diagonal past a wall square's corner (PHB p.147-148).
    /// </summary>
    public static bool IsStepBlockedByBarrier(Vector2Int from, Vector2Int to)
    {
        return ResilientSphereAreaEffect.DoesMovementCrossSphereBoundary(from, to)
            || IsCellBlockedByWall(to)
            || WallOfIceAreaEffect.DoesDiagonalMoveCrossWall(from, to)
            || WallOfForceAreaEffect.DoesDiagonalMoveCrossWall(from, to);
    }

    /// <summary>
    /// Whether <paramref name="cell"/> is difficult terrain for movement (the squares an area effect marks: Grease, Web,
    /// Entangle, Spike Stones and the others; MovementService's set, read through the GameManager). False with no
    /// GameManager (an edit-mode test).
    /// </summary>
    public static bool IsDifficultTerrainForMovement(Vector2Int cell)
    {
        GameManager gm = GameManager.Instance;
        return gm != null && gm.IsDifficultTerrain(cell);
    }

    /// <summary>
    /// The one step cost of movement (PHB p.147-148; batch 12 step 23, GRID-013): an orthogonal step costs 1 square, a
    /// diagonal 1 and 2 alternately (<paramref name="diagonalCount"/> carries the count along the move); a step into
    /// difficult terrain costs 2, a diagonal one 3 (it still counts as a diagonal for the alternation).
    /// Delegated RAW reading (2026-10-29, docs/systems/RULES_COVERAGE.md 4.1): PHB p.148 says only that a diagonal into
    /// difficult terrain counts as 3 squares; whether it also advances the 1-2-1 count is silent. It is read as advancing
    /// it, because it is still a diagonal move (PHB p.147 counts every diagonal toward the alternation).
    /// </summary>
    public static int StepCost(Vector2Int from, Vector2Int to, ref int diagonalCount, bool intoDifficultTerrain)
    {
        if (IsDiagonalStep(from, to))
        {
            diagonalCount++;
            if (intoDifficultTerrain)
                return 3;
            // Odd diagonals (1st, 3rd, 5th) cost 1; even (2nd, 4th, 6th) cost 2
            return (diagonalCount % 2 == 0) ? 2 : 1;
        }

        return intoDifficultTerrain ? 2 : 1;
    }

    /// <summary>
    /// Calculate the actual D&D 3.5e movement cost along a specific path: the sum of <see cref="StepCost"/> over its
    /// steps (orthogonal 1, diagonal alternating 1/2, difficult terrain 2 or 3; the diagonal counter persists across the
    /// entire path), or <see cref="BlockedPathCost"/> when a step is blocked by a barrier
    /// (<see cref="IsStepBlockedByBarrier"/>). The one path cost of the A* trim, the movement cost, the PC move
    /// executor, the charge and the overrun (batch 12 step 23, GRID-013).
    ///
    /// This differs from GetDistance() which calculates the MINIMUM cost
    /// (straight-line). This method calculates the cost of the ACTUAL path taken.
    /// </summary>
    /// <param name="start">Starting position (not included in path list)</param>
    /// <param name="path">List of positions visited in order (each must be adjacent to previous)</param>
    /// <returns>Total movement cost in D&D 3.5e squares</returns>
    public static int CalculatePathCost(Vector2Int start, System.Collections.Generic.List<Vector2Int> path)
        => CalculatePathCost(start, path, ignoreDifficultTerrain: false);

    /// <summary>
    /// <see cref="CalculatePathCost(Vector2Int, System.Collections.Generic.List{Vector2Int})"/>; with
    /// <paramref name="ignoreDifficultTerrain"/> the steps cost what they would on open ground (the Flaming Sphere's
    /// roll, PHB p.232), and barriers still block.
    /// </summary>
    public static int CalculatePathCost(Vector2Int start, System.Collections.Generic.List<Vector2Int> path, bool ignoreDifficultTerrain)
    {
        if (path == null || path.Count == 0) return 0;

        int cost = 0;
        int diagonalCount = 0;
        Vector2Int prev = start;

        for (int i = 0; i < path.Count; i++)
        {
            Vector2Int current = path[i];
            if (IsStepBlockedByBarrier(prev, current))
                return BlockedPathCost;

            cost += StepCost(prev, current, ref diagonalCount, !ignoreDifficultTerrain && IsDifficultTerrainForMovement(current));
            prev = current;
        }

        return cost;
    }

    /// <summary>
    /// The squares whose interior meets the convex hull of a space of <paramref name="sizeSquares"/> squares a side at
    /// <paramref name="fromAnchor"/> and the same space at <paramref name="toAnchor"/> (anchors are the footprint's lowest
    /// corner square, as CharacterController.GetOccupiedSquaresAt): the squares that some line from a point of the starting
    /// space to a point of the ending space passes through, both spaces included (the charge's clear-path test, which bars
    /// every such line, PHB p.154; CMB-132; delegated RAW reading 2026-10-29, docs/systems/RULES_COVERAGE.md 4.1).
    /// A square the hull only touches at an edge or a corner is not passed through, so a straight charge along a row does
    /// not meet the rows beside it, while a diagonal one meets the squares orthogonally beside the diagonal.
    /// </summary>
    public static System.Collections.Generic.List<Vector2Int> GetSquaresCoveredBySweptSpace(Vector2Int fromAnchor, Vector2Int toAnchor, int sizeSquares)
    {
        var squares = new System.Collections.Generic.List<Vector2Int>();
        int s = Mathf.Max(1, sizeSquares);
        const double inset = 1e-4; // a square's interior, so bare edge or corner contact does not count

        // The hull is the starting box swept along the anchor-to-anchor segment: its edges are the boxes' axis-aligned
        // ones and two parallel to the segment, so a separating-axis test on x, y and the segment's normal decides it.
        double hx0 = Mathf.Min(fromAnchor.x, toAnchor.x) - 0.5, hx1 = Mathf.Max(fromAnchor.x, toAnchor.x) + s - 0.5;
        double hy0 = Mathf.Min(fromAnchor.y, toAnchor.y) - 0.5, hy1 = Mathf.Max(fromAnchor.y, toAnchor.y) + s - 0.5;
        double nx = -(toAnchor.y - fromAnchor.y), ny = toAnchor.x - fromAnchor.x;
        bool moves = nx != 0.0 || ny != 0.0;
        ProjectBox(fromAnchor.x - 0.5, fromAnchor.y - 0.5, fromAnchor.x + s - 0.5, fromAnchor.y + s - 0.5, nx, ny, out double hn0, out double hn1);

        int minX = Mathf.Min(fromAnchor.x, toAnchor.x), maxX = Mathf.Max(fromAnchor.x, toAnchor.x) + s - 1;
        int minY = Mathf.Min(fromAnchor.y, toAnchor.y), maxY = Mathf.Max(fromAnchor.y, toAnchor.y) + s - 1;
        for (int x = minX; x <= maxX; x++)
        {
            double sx0 = x - 0.5 + inset, sx1 = x + 0.5 - inset;
            if (sx1 <= hx0 || sx0 >= hx1)
                continue;
            for (int y = minY; y <= maxY; y++)
            {
                double sy0 = y - 0.5 + inset, sy1 = y + 0.5 - inset;
                if (sy1 <= hy0 || sy0 >= hy1)
                    continue;
                if (moves)
                {
                    ProjectBox(sx0, sy0, sx1, sy1, nx, ny, out double sn0, out double sn1);
                    if (sn1 <= hn0 || sn0 >= hn1)
                        continue;
                }
                squares.Add(new Vector2Int(x, y));
            }
        }

        return squares;
    }

    /// <summary>The interval a box projects to on the axis (<paramref name="ax"/>, <paramref name="ay"/>).</summary>
    private static void ProjectBox(double x0, double y0, double x1, double y1, double ax, double ay, out double min, out double max)
    {
        double a = x0 * ax + y0 * ay, b = x1 * ax + y0 * ay, c = x0 * ax + y1 * ay, d = x1 * ax + y1 * ay;
        min = System.Math.Min(System.Math.Min(a, b), System.Math.Min(c, d));
        max = System.Math.Max(System.Math.Max(a, b), System.Math.Max(c, d));
    }

    /// <summary>
    /// The steps of a move straight from <paramref name="from"/> to <paramref name="to"/> (the start left out, the end
    /// included): one square per step along the longer axis, the other axis following the line between the centres
    /// (Bresenham). It has the fewest steps and diagonals a move between the two squares can have, so it costs
    /// <see cref="GetDistance"/> on open ground, and every footprint it enters lies in the swept space
    /// (<see cref="GetSquaresCoveredBySweptSpace"/>). The charge's path (PHB p.154: the charger moves directly toward the
    /// opponent; CMB-132).
    /// </summary>
    public static System.Collections.Generic.List<Vector2Int> GetStraightLinePath(Vector2Int from, Vector2Int to)
    {
        var path = new System.Collections.Generic.List<Vector2Int>();
        int dx = to.x - from.x, dy = to.y - from.y;
        int steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
        for (int i = 1; i <= steps; i++)
        {
            // Integer rounding of from + d * i / steps, half away from zero, on each axis.
            path.Add(new Vector2Int(from.x + RoundedFraction(dx, i, steps), from.y + RoundedFraction(dy, i, steps)));
        }

        return path;
    }

    private static int RoundedFraction(int d, int i, int steps)
    {
        int n = System.Math.Abs(d) * i * 2 + steps; // round(|d| * i / steps) = floor((2|d|i + steps) / (2 steps))
        int value = n / (steps * 2);
        return d < 0 ? -value : value;
    }
}
