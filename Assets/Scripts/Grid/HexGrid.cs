using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates and manages the 20x20 hexagonal grid.
/// </summary>
public class HexGrid : MonoBehaviour
{
    [Header("Grid Settings")]
    public int Width = 20;
    public int Height = 20;

    [Header("References")]
    public Sprite hexSprite; // Assign a white hex sprite (generated at runtime if null)

    private Dictionary<Vector2Int, HexCell> _cells = new Dictionary<Vector2Int, HexCell>();

    public Dictionary<Vector2Int, HexCell> Cells => _cells;

    public void GenerateGrid()
    {
        // Clear existing
        foreach (Transform child in transform)
            Destroy(child.gameObject);
        _cells.Clear();

        for (int r = 0; r < Height; r++)
        {
            for (int q = 0; q < Width; q++)
            {
                CreateCell(q, r);
            }
        }
    }

    private void CreateCell(int q, int r)
    {
        GameObject cellGO = new GameObject();
        cellGO.transform.SetParent(transform);

        // Add sprite renderer
        SpriteRenderer sr = cellGO.AddComponent<SpriteRenderer>();
        sr.sprite = hexSprite;
        sr.color = new Color(0.85f, 0.9f, 0.8f, 1f); // light green tint
        sr.sortingOrder = 0;

        // Add collider for click detection
        BoxCollider2D col = cellGO.AddComponent<BoxCollider2D>();
        col.size = new Vector2(HexUtils.HexWidth * 0.95f, HexUtils.HexHeight * 0.95f);

        // Add HexCell component and initialize
        HexCell cell = cellGO.AddComponent<HexCell>();
        cell.Init(q, r);

        _cells[new Vector2Int(q, r)] = cell;
    }

    public HexCell GetCell(int q, int r)
    {
        Vector2Int key = new Vector2Int(q, r);
        _cells.TryGetValue(key, out HexCell cell);
        return cell;
    }

    public HexCell GetCell(Vector2Int coords)
    {
        return GetCell(coords.x, coords.y);
    }

    /// <summary>
    /// Get all cells within a given hex distance from a center coordinate.
    /// </summary>
    public List<HexCell> GetCellsInRange(Vector2Int center, int range)
    {
        List<HexCell> result = new List<HexCell>();
        foreach (var kvp in _cells)
        {
            if (HexUtils.HexDistance(center, kvp.Key) <= range && kvp.Key != center)
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
        return HexUtils.AxialToWorld(Width / 2, Height / 2);
    }
}
