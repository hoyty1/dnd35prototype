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
    public static SquareGrid Instance { get; private set; }

    [Header("Grid Settings")]
    public int Width = 20;
    public int Height = 20;

    [Header("References")]
    public Sprite cellSprite; // Assign a white square sprite (generated at runtime if null)

    private readonly Dictionary<Vector2Int, SquareCell> _cells = new Dictionary<Vector2Int, SquareCell>();

    public Dictionary<Vector2Int, SquareCell> Cells => _cells;

    private void Awake()
    {
        Instance = this;
    }

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

    public float GetSquareSize()
    {
        return SquareGridUtils.CellSize;
    }

    public bool IsValidPosition(Vector2Int position)
    {
        return _cells.ContainsKey(position);
    }

    public Vector3 GetWorldPosition(Vector2Int position)
    {
        return SquareGridUtils.GridToWorld(position);
    }

    public Vector3 GetCenteredWorldPosition(Vector2Int basePosition, int sizeSquares)
    {
        Vector3 baseWorld = GetWorldPosition(basePosition);
        if (sizeSquares <= 1) return baseWorld;

        float offset = (sizeSquares - 1) * GetSquareSize() * 0.5f;
        return baseWorld + new Vector3(offset, offset, 0f);
    }

    public List<Vector2Int> GetOccupiedSquares(Vector2Int basePosition, int sizeSquares)
    {
        var occupied = new List<Vector2Int>();
        int squares = Mathf.Max(1, sizeSquares);

        for (int x = 0; x < squares; x++)
        {
            for (int y = 0; y < squares; y++)
            {
                occupied.Add(new Vector2Int(basePosition.x + x, basePosition.y + y));
            }
        }

        return occupied;
    }

    public bool CanPlaceCreature(
        Vector2Int basePosition,
        int sizeSquares,
        CharacterController ignoreOccupant = null,
        bool ignoreOtherOccupants = false,
        IList<CharacterController> additionalIgnoredOccupants = null)
    {
        var occupiedSquares = GetOccupiedSquares(basePosition, sizeSquares);

        for (int i = 0; i < occupiedSquares.Count; i++)
        {
            Vector2Int pos = occupiedSquares[i];
            if (!IsValidPosition(pos))
                return false;

            SquareCell cell = GetCell(pos);
            if (cell == null)
                return false;

            if (ignoreOtherOccupants)
                continue;

            if (!cell.IsOccupied)
                continue;

            IReadOnlyList<CharacterController> occupants = cell.Occupants;
            for (int occIndex = 0; occIndex < occupants.Count; occIndex++)
            {
                CharacterController occupant = occupants[occIndex];
                if (occupant == null)
                    continue;

                if (occupant == ignoreOccupant)
                    continue;

                if (additionalIgnoredOccupants != null && additionalIgnoredOccupants.Contains(occupant))
                    continue;

                return false;
            }
        }

        return true;
    }

    public void ClearCreatureOccupancy(CharacterController character)
    {
        if (character == null) return;

        foreach (var kvp in _cells)
        {
            SquareCell cell = kvp.Value;
            if (cell == null)
                continue;

            cell.RemoveOccupant(character);
        }
    }

    public void SetCreatureOccupancy(CharacterController character, Vector2Int basePosition, int sizeSquares)
    {
        if (character == null) return;

        var occupiedSquares = GetOccupiedSquares(basePosition, sizeSquares);
        for (int i = 0; i < occupiedSquares.Count; i++)
        {
            SquareCell cell = GetCell(occupiedSquares[i]);
            if (cell == null) continue;
            cell.AddOccupant(character);
        }
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
        HashSet<Vector2Int> threatenedSquares, int maxRange, int moverSizeSquares = 1, CharacterController mover = null,
        bool allowThroughAllies = true, bool allowThroughEnemies = false)
    {
        var result = new AoOPathResult();
        if (start == destination) return result;

        moverSizeSquares = Mathf.Max(1, moverSizeSquares);
        if (!CanPlaceCreature(start, moverSizeSquares, mover))
            return result;

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

        // THREAT_COST = 200: very high penalty for leaving a threatened square.
        // With ORTH_COST=2, a 1-square detour costs ~4 extra vs 200 for going through threat.
        // This strongly incentivizes routing around enemies to avoid AoO.
        const int THREAT_COST = 200;
        // Use weighted costs so A* prefers straight-line paths over zig-zag diagonals.
        // Orthogonal = 2, Diagonal = 3 (approximates √2 ratio and breaks cost ties).
        // This only affects path SELECTION; actual D&D 3.5 movement costs are calculated separately.
        const int ORTH_COST = 2;
        const int DIAG_COST = 3;

        gScore[start] = 0;
        int hStart = AStarHeuristic(start, destination);
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

                // Validate actual D&D 3.5e path cost (the path might cost more than
                // straight-line distance if it detours around obstacles/threats).
                // Trim the path to fit within maxRange.
                int pathCost = SquareGridUtils.CalculatePathCost(start, path);
                if (pathCost > maxRange && path.Count > 0)
                {
                    Debug.Log($"[SquareGrid] A* path cost {pathCost} exceeds maxRange {maxRange}, trimming...");
                    // Trim from the end until path fits within movement budget
                    while (path.Count > 0)
                    {
                        pathCost = SquareGridUtils.CalculatePathCost(start, path);
                        if (pathCost <= maxRange) break;
                        path.RemoveAt(path.Count - 1);
                    }
                }

                result.Path = path;

                Debug.Log($"[SquareGrid] A* path found: {path.Count} steps, cost {SquareGridUtils.CalculatePathCost(start, path)} from ({start.x},{start.y}) to ({destination.x},{destination.y})");
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

                bool isDestinationNode = neighbor == destination;
                bool canTraverseHere = CanTraversePathNode(neighbor, moverSizeSquares, mover, isDestinationNode, allowThroughAllies, allowThroughEnemies);

                if (!canTraverseHere) continue;

                // Calculate step cost (weighted: orthogonal < diagonal to prefer straight paths)
                bool isDiag = SquareGridUtils.IsDiagonalStep(current, neighbor);
                int stepCost = isDiag ? DIAG_COST : ORTH_COST;

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
                    int h = AStarHeuristic(neighbor, destination);
                    openSet.Add((tentativeG + h, tieCounter++, neighbor));
                }
            }
        }

        // No path found - fall back to simple path
        Debug.Log($"[SquareGrid] A* found no path, using simple path from ({start.x},{start.y}) to ({destination.x},{destination.y})");
        result.Path = ThreatSystem.GenerateSimplePath(start, destination);


        // Remove invalid fallback steps for larger footprints / blocked tiles.
        int firstInvalidIndex = -1;
        for (int i = 0; i < result.Path.Count; i++)
        {
            Vector2Int step = result.Path[i];
            bool isDestinationStep = step == destination;
            bool canTraverse = CanTraversePathNode(step, moverSizeSquares, mover, isDestinationStep, allowThroughAllies, allowThroughEnemies);
            if (!canTraverse)
            {
                firstInvalidIndex = i;
                break;
            }
        }

        if (firstInvalidIndex >= 0)
            result.Path.RemoveRange(firstInvalidIndex, result.Path.Count - firstInvalidIndex);
        // Trim to max range
        while (result.Path.Count > 0 && SquareGridUtils.GetDistance(start, result.Path[result.Path.Count - 1]) > maxRange)
        {
            result.Path.RemoveAt(result.Path.Count - 1);
        }

        return result;
    }

    /// <summary>
    /// Movement-path occupancy rules:
    /// - Intermediate path nodes may be occupied by allies (you can move through allies).
    /// - Intermediate path nodes may optionally be occupied by enemies (for maneuvers like overrun).
    /// - Destination node must be fully unoccupied by anyone except the mover itself.
    /// </summary>
    public bool CanTraversePathNode(Vector2Int basePosition, int moverSizeSquares, CharacterController mover, bool isDestinationNode,
        bool allowThroughAllies = true, bool allowThroughEnemies = false)
    {
        // Validate footprint bounds/cell existence first while ignoring occupancy.
        if (!CanPlaceCreature(basePosition, moverSizeSquares, mover, ignoreOtherOccupants: true))
            return false;

        var occupiedSquares = GetOccupiedSquares(basePosition, moverSizeSquares);
        for (int i = 0; i < occupiedSquares.Count; i++)
        {
            SquareCell cell = GetCell(occupiedSquares[i]);
            if (cell == null)
                return false;

            if (!cell.IsOccupied)
                continue;

            IReadOnlyList<CharacterController> occupants = cell.Occupants;
            for (int occIndex = 0; occIndex < occupants.Count; occIndex++)
            {
                CharacterController occupant = occupants[occIndex];
                if (occupant == null || occupant == mover)
                    continue;

                // Final destination must be unoccupied.
                if (isDestinationNode)
                    return false;

                // During traversal, ally/enemy pass-through is controlled by the caller.
                bool isAllyOccupant = mover != null && occupant.Team == mover.Team;
                if (isAllyOccupant)
                {
                    if (!allowThroughAllies)
                        return false;
                    continue;
                }

                if (!allowThroughEnemies)
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Admissible heuristic for the weighted A* (ORTH_COST=2, DIAG_COST=3).
    /// h(n) = min(dx,dy)*3 + |dx-dy|*2, which equals the optimal weighted cost
    /// and is therefore both admissible and consistent.
    /// </summary>
    private static int AStarHeuristic(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(b.x - a.x);
        int dy = Mathf.Abs(b.y - a.y);
        int diag = Mathf.Min(dx, dy);
        int orth = Mathf.Abs(dx - dy);
        return diag * 3 + orth * 2;
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
            if (character.Team == mover.Team) continue;

            var threats = ThreatSystem.GetThreatenedSquares(character);
            allThreatened.UnionWith(threats);
        }

        if (allThreatened.Count > 0)
        {
            Debug.Log($"[SquareGrid] Total threatened squares: {allThreatened.Count}");
        }

        // Run A* with threat awareness
        int moverSizeSquares = mover != null ? mover.GetVisualSquaresOccupied() : 1;
        var pathResult = FindPathAoOAware(start, destination, allThreatened, mover.Stats.MoveRange, moverSizeSquares, mover);

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