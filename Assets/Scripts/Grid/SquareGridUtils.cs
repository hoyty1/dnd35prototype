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
    /// Calculate the actual D&D 3.5e movement cost along a specific path.
    /// Counts each step's cost: orthogonal = 1, diagonal = alternating 1/2.
    /// The diagonal counter persists across the entire path.
    /// 
    /// This differs from GetDistance() which calculates the MINIMUM cost
    /// (straight-line). This method calculates the cost of the ACTUAL path taken.
    /// </summary>
    /// <param name="start">Starting position (not included in path list)</param>
    /// <param name="path">List of positions visited in order (each must be adjacent to previous)</param>
    /// <returns>Total movement cost in D&D 3.5e squares</returns>
    public static int CalculatePathCost(Vector2Int start, System.Collections.Generic.List<Vector2Int> path)
    {
        if (path == null || path.Count == 0) return 0;

        int cost = 0;
        int diagonalCount = 0;
        Vector2Int prev = start;

        for (int i = 0; i < path.Count; i++)
        {
            Vector2Int current = path[i];
            bool isDiag = IsDiagonalStep(prev, current);

            if (isDiag)
            {
                diagonalCount++;
                // Odd diagonals (1st, 3rd, 5th) cost 1; even (2nd, 4th, 6th) cost 2
                cost += (diagonalCount % 2 == 0) ? 2 : 1;
            }
            else
            {
                cost += 1;
            }

            prev = current;
        }

        return cost;
    }
}
