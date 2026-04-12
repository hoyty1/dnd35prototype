using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draws a dotted line path preview from a character's current position
/// to the hovered destination cell during the movement phase.
/// Segments that pass through threatened squares are drawn in red/orange
/// to warn the player about Attacks of Opportunity.
/// Attach to a dedicated GameObject (created at runtime by GameManager).
/// </summary>
public class PathPreview : MonoBehaviour
{
    private LineRenderer _safeLineRenderer;
    private LineRenderer _dangerLineRenderer;
    private List<Vector3> _currentPath = new List<Vector3>();
    private Vector2Int _lastHoveredCoord = new Vector2Int(-999, -999);

    // Visual settings
    private static readonly Color SafePathColor = new Color(1f, 0.9f, 0.2f, 0.85f);   // Bright yellow
    private static readonly Color DangerPathColor = new Color(1f, 0.25f, 0.15f, 0.9f); // Bright red-orange
    private const float LineWidth = 0.08f;
    private const float ZOffset = -0.5f;    // Bring line forward (closer to camera)
    private const int SortingOrder = 5;     // Above grid (0) but below characters

    void Awake()
    {
        _safeLineRenderer = CreateLineRenderer("SafePath", SafePathColor);
        _dangerLineRenderer = CreateLineRenderer("DangerPath", DangerPathColor);
    }

    /// <summary>
    /// Creates a LineRenderer child with a dotted-line material of the given color.
    /// </summary>
    private LineRenderer CreateLineRenderer(string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        var lr = go.AddComponent<LineRenderer>();

        lr.startWidth = LineWidth;
        lr.endWidth = LineWidth;
        lr.positionCount = 0;
        lr.useWorldSpace = true;
        lr.sortingOrder = SortingOrder;

        // Create dotted material
        Texture2D dottedTexture = new Texture2D(8, 2, TextureFormat.RGBA32, false);
        dottedTexture.wrapMode = TextureWrapMode.Repeat;
        dottedTexture.filterMode = FilterMode.Point;
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                Color c = x < 4 ? Color.white : Color.clear;
                dottedTexture.SetPixel(x, y, c);
            }
        }
        dottedTexture.Apply();

        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = dottedTexture;
        mat.color = color;

        lr.material = mat;
        lr.textureMode = LineTextureMode.Tile;
        lr.startColor = Color.white;
        lr.endColor = Color.white;
        lr.enabled = false;

        return lr;
    }

    /// <summary>
    /// Show a path preview from the character's position through the given path cells.
    /// Segments where the character leaves a threatened square are drawn in red.
    /// </summary>
    /// <param name="startPos">Grid position of the character (included as first point).</param>
    /// <param name="pathCells">List of grid coordinates along the path (excluding start).</param>
    /// <param name="segmentThreatened">Per-segment flag: true if that segment leaves a threatened square. Null = all safe.</param>
    public void ShowPath(Vector2Int startPos, List<Vector2Int> pathCells, List<bool> segmentThreatened = null)
    {
        if (pathCells == null || pathCells.Count == 0)
        {
            HidePath();
            return;
        }

        // Build full world-space point list
        _currentPath.Clear();
        _currentPath.Add(NormalizePoint(SquareGridUtils.GridToWorld(startPos)));
        foreach (var cell in pathCells)
        {
            _currentPath.Add(NormalizePoint(SquareGridUtils.GridToWorld(cell)));
        }

        // Split into safe vs danger segments for dual-line rendering
        var safePoints = new List<Vector3>();
        var dangerPoints = new List<Vector3>();

        bool hasDanger = false;
        if (segmentThreatened != null)
        {
            foreach (var t in segmentThreatened)
            {
                if (t) { hasDanger = true; break; }
            }
        }

        if (!hasDanger || segmentThreatened == null)
        {
            // All safe — single line, fast path
            SetLinePositions(_safeLineRenderer, _currentPath);
            ClearLine(_dangerLineRenderer);
        }
        else
        {
            // Mixed safe/danger segments — build separate point lists
            // We render two LineRenderers:
            //   - Safe line: all segments, but danger segments get zero-length (collapsed) points
            //   - Danger line: only danger segments, safe segments collapsed
            // Alternative (simpler): render the FULL path as safe, then overlay danger segments on top.

            // Full path is safe (yellow) underneath
            SetLinePositions(_safeLineRenderer, _currentPath);

            // Overlay danger segments in red
            var dangerSegments = new List<Vector3>();
            for (int i = 0; i < segmentThreatened.Count; i++)
            {
                if (segmentThreatened[i])
                {
                    // Add the segment start and end
                    if (dangerSegments.Count == 0 || dangerSegments[dangerSegments.Count - 1] != _currentPath[i])
                    {
                        dangerSegments.Add(_currentPath[i]);
                    }
                    dangerSegments.Add(_currentPath[i + 1]);
                }
                else
                {
                    // Break in danger segments — flush if we have any
                    // LineRenderer can't have gaps, so we insert a degenerate (zero-length) segment
                    if (dangerSegments.Count > 0)
                    {
                        // Add a connecting invisible segment (same point twice)
                        dangerSegments.Add(_currentPath[i]);
                        dangerSegments.Add(_currentPath[i]);
                    }
                }
            }

            if (dangerSegments.Count > 0)
            {
                SetLinePositions(_dangerLineRenderer, dangerSegments);
            }
            else
            {
                ClearLine(_dangerLineRenderer);
            }
        }
    }

    /// <summary>
    /// Assign positions to a LineRenderer and enable it.
    /// </summary>
    private void SetLinePositions(LineRenderer lr, List<Vector3> points)
    {
        lr.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
        {
            lr.SetPosition(i, points[i]);
        }

        // Scale texture tiling for consistent dot spacing
        float totalLength = 0f;
        for (int i = 1; i < points.Count; i++)
        {
            totalLength += Vector3.Distance(points[i - 1], points[i]);
        }
        lr.material.mainTextureScale = new Vector2(totalLength * 3f, 1f);
        lr.enabled = true;
    }

    /// <summary>
    /// Disable and clear a LineRenderer.
    /// </summary>
    private void ClearLine(LineRenderer lr)
    {
        lr.positionCount = 0;
        lr.enabled = false;
    }

    /// <summary>
    /// Normalize a world-space point for the line renderer.
    /// Ensures all points share a consistent Z depth (no unwanted vertical angles in 2D).
    /// </summary>
    private Vector3 NormalizePoint(Vector3 p)
    {
        return new Vector3(p.x, p.y, ZOffset);
    }

    /// <summary>
    /// Hide the path preview.
    /// </summary>
    public void HidePath()
    {
        ClearLine(_safeLineRenderer);
        ClearLine(_dangerLineRenderer);
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
    public bool IsVisible => _safeLineRenderer.enabled || _dangerLineRenderer.enabled;
}
