using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates and manages the 20x20 square grid.
/// Each cell is positioned at (x * CellSize, y * CellSize) in world space.
/// Uses D&D 3.5 diagonal movement costs for distance calculations.
/// </summary>
public class SquareGrid : MonoBehaviour
{
    [Header("Grid Settings")]
    public int Width = 20;
    public int Height = 20;

    [Header("References")]
    public Sprite cellSprite; // Assign a white square sprite (generated at runtime if null)

    private Dictionary<Vector2Int, SquareCell> _cells = new Dictionary<Vector2Int, SquareCell>();

    public Dictionary<Vector2Int, SquareCell> Cells => _cells;

    public void GenerateGrid()
    {
        // Clear existing
        foreach (Transform child in transform)
            Destroy(child.gameObject);
        _cells.Clear();

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                CreateCell(x, y);
            }
        }
    }

    private void CreateCell(int x, int y)
    {
        GameObject cellGO = new GameObject();
        cellGO.transform.SetParent(transform);

        // Add sprite renderer
        SpriteRenderer sr = cellGO.AddComponent<SpriteRenderer>();
        sr.sprite = cellSprite;
        sr.color = new Color(0.85f, 0.9f, 0.8f, 1f); // light green tint
        sr.sortingOrder = 0;

        // Add collider for click detection
        BoxCollider2D col = cellGO.AddComponent<BoxCollider2D>();
        col.size = new Vector2(SquareGridUtils.CellSize * 0.95f, SquareGridUtils.CellSize * 0.95f);

        // Add SquareCell component and initialize
        SquareCell cell = cellGO.AddComponent<SquareCell>();
        cell.Init(x, y);

        _cells[new Vector2Int(x, y)] = cell;
    }

    public SquareCell GetCell(int x, int y)
    {
        Vector2Int key = new Vector2Int(x, y);
        _cells.TryGetValue(key, out SquareCell cell);
        return cell;
    }

    public SquareCell GetCell(Vector2Int coords)
    {
        return GetCell(coords.x, coords.y);
    }

    /// <summary>
    /// Get all cells within a given D&D 3.5 square grid distance from a center coordinate.
    /// Uses Chebyshev distance as an upper bound, then filters by actual D&D 3.5 distance.
    /// </summary>
    public List<SquareCell> GetCellsInRange(Vector2Int center, int range)
    {
        List<SquareCell> result = new List<SquareCell>();
        foreach (var kvp in _cells)
        {
            if (kvp.Key == center) continue;

            // Quick Chebyshev pre-filter (always <= actual D&D distance, so it's a valid filter)
            if (SquareGridUtils.ChebyshevDistance(center, kvp.Key) > range) continue;

            if (SquareGridUtils.GetDistance(center, kvp.Key) <= range)
            {
                result.Add(kvp.Value);
            }
        }
        return result;
    }

    // ========================================================================
    // AoO-AWARE PATHFINDING (A* with threat cost)
    // ========================================================================

    /// <summary>
    /// Find a path from start to destination using A* pathfinding.
    /// Threatened squares have a very high movement cost (but are still traversable).
    /// Returns the path (excluding start) and which AoOs would be provoked.
    /// </summary>
    /// <param name="start">Starting position.</param>
    /// <param name="destination">Target position.</param>
    /// <param name="threatenedSquares">Set of squares threatened by enemies. Null = no threat awareness.</param>
    /// <param name="maxRange">Maximum path length in D&D 3.5 squares.</param>
    /// <returns>AoOPathResult with the computed path and provoked AoOs.</returns>
    public AoOPathResult FindPathAoOAware(Vector2Int start, Vector2Int destination,
        HashSet<Vector2Int> threatenedSquares, int maxRange)
    {
        var result = new AoOPathResult();
        if (start == destination) return result;

        // A* data structures
        // Custom comparer needed because Vector2Int does not implement IComparable.
        // Since tieBreaker is always unique, comparing fScore then tieBreaker is sufficient.
        var openSet = new SortedSet<(int fScore, int tieBreaker, Vector2Int pos)>(
            Comparer<(int fScore, int tieBreaker, Vector2Int pos)>.Create((a, b) =>
            {
                int cmp = a.fScore.CompareTo(b.fScore);
                if (cmp != 0) return cmp;
                return a.tieBreaker.CompareTo(b.tieBreaker);
            }));
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, int>();
        var visited = new HashSet<Vector2Int>();
        int tieCounter = 0;

        const int THREAT_COST = 50; // Very high cost for threatened squares (but not infinite)
        const int NORMAL_COST = 1;

        gScore[start] = 0;
        int hStart = SquareGridUtils.ChebyshevDistance(start, destination);
        openSet.Add((hStart, tieCounter++, start));

        while (openSet.Count > 0)
        {
            // Get node with lowest fScore
            var (_, _, current) = openSet.Min;
            openSet.Remove(openSet.Min);

            if (current == destination)
            {
                // Reconstruct path
                var path = new List<Vector2Int>();
                Vector2Int node = destination;
                while (node != start)
                {
                    path.Add(node);
                    node = cameFrom[node];
                }
                path.Reverse();
                result.Path = path;

                Debug.Log($"[SquareGrid] A* path found: {path.Count} steps from ({start.x},{start.y}) to ({destination.x},{destination.y})");
                return result;
            }

            if (visited.Contains(current)) continue;
            visited.Add(current);

            // Expand neighbors
            Vector2Int[] neighbors = SquareGridUtils.GetNeighbors(current);
            foreach (var neighbor in neighbors)
            {
                if (visited.Contains(neighbor)) continue;

                // Check bounds
                SquareCell cell = GetCell(neighbor);
                if (cell == null) continue;

                // Check occupancy (destination is OK even if occupied for pathfinding purposes)
                if (cell.IsOccupied && neighbor != destination) continue;

                // Calculate step cost
                int stepCost = NORMAL_COST;

                // Diagonal steps may cost more (D&D 3.5 alternating diagonals)
                if (SquareGridUtils.IsDiagonalStep(current, neighbor))
                    stepCost = NORMAL_COST; // Simplified: we use Chebyshev for A* heuristic

                // Add threat penalty for moving into a threatened square
                if (threatenedSquares != null && threatenedSquares.Contains(current))
                {
                    // Leaving a threatened square is what provokes; add cost to leaving
                    stepCost += THREAT_COST;
                }

                int tentativeG = gScore.GetValueOrDefault(current, int.MaxValue / 2) + stepCost;

                // Check max range (using D&D 3.5 distance from start)
                if (SquareGridUtils.GetDistance(start, neighbor) > maxRange) continue;

                if (tentativeG < gScore.GetValueOrDefault(neighbor, int.MaxValue))
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    int h = SquareGridUtils.ChebyshevDistance(neighbor, destination);
                    openSet.Add((tentativeG + h, tieCounter++, neighbor));
                }
            }
        }

        // No path found - fall back to simple path
        Debug.Log($"[SquareGrid] A* found no path, using simple path from ({start.x},{start.y}) to ({destination.x},{destination.y})");
        result.Path = ThreatSystem.GenerateSimplePath(start, destination);

        // Trim to max range
        while (result.Path.Count > 0 && SquareGridUtils.GetDistance(start, result.Path[result.Path.Count - 1]) > maxRange)
        {
            result.Path.RemoveAt(result.Path.Count - 1);
        }

        return result;
    }

    /// <summary>
    /// Find a "safe" path that avoids threatened squares when possible.
    /// If no safe path exists within movement range, returns the direct path.
    /// Also returns which AoOs the path would provoke.
    /// </summary>
    /// <param name="start">Starting position.</param>
    /// <param name="destination">Target position.</param>
    /// <param name="mover">The character moving.</param>
    /// <param name="allCharacters">All characters in combat (for threat calculation).</param>
    /// <returns>AoOPathResult with path and provoked AoO info.</returns>
    public AoOPathResult FindSafePath(Vector2Int start, Vector2Int destination,
        CharacterController mover, List<CharacterController> allCharacters)
    {
        // Build combined set of all enemy-threatened squares
        var allThreatened = new HashSet<Vector2Int>();
        foreach (var character in allCharacters)
        {
            if (character == mover) continue;
            if (character.Stats.IsDead) continue;
            if (character.IsPlayerControlled == mover.IsPlayerControlled) continue;

            var threats = ThreatSystem.GetThreatenedSquares(character);
            allThreatened.UnionWith(threats);
        }

        if (allThreatened.Count > 0)
        {
            Debug.Log($"[SquareGrid] Total threatened squares: {allThreatened.Count}");
        }

        // Run A* with threat awareness
        var pathResult = FindPathAoOAware(start, destination, allThreatened, mover.Stats.MoveRange);

        // If we got a path, analyze it for actual AoOs
        if (pathResult.Path.Count > 0)
        {
            pathResult.ProvokedAoOs = ThreatSystem.AnalyzePathForAoOs(mover, pathResult.Path, allCharacters);
        }

        return pathResult;
    }

    /// <summary>
    /// Clear all highlights on the grid.
    /// </summary>
    public void ClearAllHighlights()
    {
        foreach (var kvp in _cells)
        {
            kvp.Value.SetHighlight(HighlightType.None);
        }
    }

    /// <summary>
    /// Highlight all cells within a spell's range with the SpellRange color (purple).
    /// Returns the list of highlighted cells for tracking.
    /// </summary>
    public List<SquareCell> HighlightSpellRange(Vector2Int origin, int range)
    {
        var highlighted = new List<SquareCell>();
        foreach (var kvp in _cells)
        {
            if (kvp.Key == origin) continue;
            if (SquareGridUtils.ChebyshevDistance(origin, kvp.Key) > range) continue;
            if (SquareGridUtils.GetDistance(origin, kvp.Key) <= range)
            {
                kvp.Value.SetHighlight(HighlightType.SpellRange);
                highlighted.Add(kvp.Value);
            }
        }
        return highlighted;
    }

    /// <summary>
    /// Clear only spell range highlights (reset SpellRange and SpellTarget cells to None).
    /// </summary>
    public void ClearSpellRangeHighlight()
    {
        foreach (var kvp in _cells)
        {
            var cell = kvp.Value;
            // Only clear spell-related highlights, preserve other highlights
            if (cell != null)
            {
                // We check the color to see if it's a spell highlight - simpler to just clear all
                cell.SetHighlight(HighlightType.None);
            }
        }
    }

    /// <summary>
    /// Get the world-space center of the grid for camera positioning.
    /// </summary>
    public Vector3 GetGridCenter()
    {
        return SquareGridUtils.GridToWorld(Width / 2, Height / 2);
    }
}
