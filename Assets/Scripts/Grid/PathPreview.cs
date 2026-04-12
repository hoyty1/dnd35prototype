using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draws a dotted line path preview from a character's current position
/// to the hovered destination cell during the movement phase.
/// Attach to a dedicated GameObject (created at runtime by GameManager).
/// </summary>
public class PathPreview : MonoBehaviour
{
    private LineRenderer _lineRenderer;
    private List<Vector3> _currentPath = new List<Vector3>();
    private Vector2Int _lastHoveredCoord = new Vector2Int(-999, -999);

    // Visual settings
    private static readonly Color PathColor = new Color(1f, 0.9f, 0.2f, 0.85f); // Bright yellow
    private const float LineWidth = 0.08f;
    private const float YOffset = 0f; // 2D game, no Y offset needed
    private const int SortingOrder = 5; // Above grid (0) but below characters

    void Awake()
    {
        _lineRenderer = gameObject.AddComponent<LineRenderer>();

        // Basic line setup
        _lineRenderer.startWidth = LineWidth;
        _lineRenderer.endWidth = LineWidth;
        _lineRenderer.positionCount = 0;
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.sortingOrder = SortingOrder;

        // Create a dotted material
        CreateDottedMaterial();

        _lineRenderer.enabled = false;
    }

    /// <summary>
    /// Creates a simple dotted-line material using a tiled texture.
    /// </summary>
    private void CreateDottedMaterial()
    {
        // Create a small texture with dot + gap pattern
        Texture2D dottedTexture = new Texture2D(8, 2, TextureFormat.RGBA32, false);
        dottedTexture.wrapMode = TextureWrapMode.Repeat;
        dottedTexture.filterMode = FilterMode.Point;

        // Pattern: 4 white pixels, 4 transparent pixels
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                Color c = x < 4 ? Color.white : Color.clear;
                dottedTexture.SetPixel(x, y, c);
            }
        }
        dottedTexture.Apply();

        // Use Sprites/Default shader for 2D rendering
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = dottedTexture;
        mat.color = PathColor;

        _lineRenderer.material = mat;
        _lineRenderer.textureMode = LineTextureMode.Tile;

        // Set vertex colors to white so material color shows through
        _lineRenderer.startColor = Color.white;
        _lineRenderer.endColor = Color.white;
    }

    /// <summary>
    /// Show a path preview from the character's position through the given path cells.
    /// </summary>
    /// <param name="startPos">Grid position of the character (included as first point).</param>
    /// <param name="pathCells">List of grid coordinates along the path (excluding start).</param>
    public void ShowPath(Vector2Int startPos, List<Vector2Int> pathCells)
    {
        if (pathCells == null || pathCells.Count == 0)
        {
            HidePath();
            return;
        }

        _currentPath.Clear();

        // Start from character position
        _currentPath.Add(SquareGridUtils.GridToWorld(startPos));

        // Add each cell in the path
        foreach (var cell in pathCells)
        {
            _currentPath.Add(SquareGridUtils.GridToWorld(cell));
        }

        // Set line renderer positions
        _lineRenderer.positionCount = _currentPath.Count;
        for (int i = 0; i < _currentPath.Count; i++)
        {
            _lineRenderer.SetPosition(i, _currentPath[i]);
        }

        // Scale texture tiling based on total path length for consistent dot spacing
        float totalLength = 0f;
        for (int i = 1; i < _currentPath.Count; i++)
        {
            totalLength += Vector3.Distance(_currentPath[i - 1], _currentPath[i]);
        }
        // Each tile unit = 1 dot+gap. We want roughly 3 dots per grid cell (CellSize=1)
        _lineRenderer.material.mainTextureScale = new Vector2(totalLength * 3f, 1f);

        _lineRenderer.enabled = true;
    }

    /// <summary>
    /// Hide the path preview.
    /// </summary>
    public void HidePath()
    {
        _lineRenderer.enabled = false;
        _lineRenderer.positionCount = 0;
        _currentPath.Clear();
        _lastHoveredCoord = new Vector2Int(-999, -999);
    }

    /// <summary>
    /// Check if the hovered coordinate has changed (avoids redundant path recalculations).
    /// </summary>
    public bool HasCoordChanged(Vector2Int newCoord)
    {
        if (newCoord == _lastHoveredCoord) return false;
        _lastHoveredCoord = newCoord;
        return true;
    }

    /// <summary>
    /// Whether the preview is currently visible.
    /// </summary>
    public bool IsVisible => _lineRenderer.enabled;
}
