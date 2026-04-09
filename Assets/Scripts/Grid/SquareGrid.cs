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
    /// Get the world-space center of the grid for camera positioning.
    /// </summary>
    public Vector3 GetGridCenter()
    {
        return SquareGridUtils.GridToWorld(Width / 2, Height / 2);
    }
}
