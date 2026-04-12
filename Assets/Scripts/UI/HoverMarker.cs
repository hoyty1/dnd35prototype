using UnityEngine;

/// <summary>
/// Displays an "X" marker on the grid square under the mouse cursor
/// during the movement phase, providing clear visual feedback about
/// which square the player is about to click on.
/// Created at runtime by GameManager.
/// </summary>
public class HoverMarker : MonoBehaviour
{
    private GameObject _markerObject;
    private SpriteRenderer _spriteRenderer;
    private float _pulseTimer = 0f;

    // Visual settings
    private const float MarkerScale = 0.55f;
    private const float ZOffset = -0.6f;       // Slightly in front of path preview (-0.5)
    private const int SortingOrder = 8;         // Above path preview (5), below characters (10)
    private const float PulseSpeed = 3f;
    private const float PulseAmplitude = 0.05f;

    void Awake()
    {
        CreateXMarker();
        Hide();
    }

    void Update()
    {
        if (_markerObject != null && _markerObject.activeSelf)
        {
            // Gentle pulse effect for visibility
            _pulseTimer += Time.deltaTime * PulseSpeed;
            float scale = MarkerScale + Mathf.Sin(_pulseTimer) * PulseAmplitude;
            _markerObject.transform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    /// <summary>
    /// Procedurally create the "X" marker sprite.
    /// </summary>
    private void CreateXMarker()
    {
        _markerObject = new GameObject("XMarker");
        _markerObject.transform.SetParent(transform);

        _spriteRenderer = _markerObject.AddComponent<SpriteRenderer>();

        // Create X texture procedurally
        Texture2D xTexture = CreateXTexture(64, 6);
        Sprite xSprite = Sprite.Create(
            xTexture,
            new Rect(0, 0, xTexture.width, xTexture.height),
            new Vector2(0.5f, 0.5f),
            64f  // pixels per unit — 64px texture fills 1 world unit
        );

        _spriteRenderer.sprite = xSprite;
        _spriteRenderer.color = Color.white;
        _spriteRenderer.sortingOrder = SortingOrder;

        _markerObject.transform.localScale = new Vector3(MarkerScale, MarkerScale, 1f);
    }

    /// <summary>
    /// Generate a texture with an "X" shape drawn on a transparent background.
    /// Includes a dark outline around the X for readability on any background.
    /// </summary>
    private Texture2D CreateXTexture(int size, int thickness)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;

        // Margin from edges so the X doesn't touch the border
        int margin = 8;

        // Fill with transparent
        Color[] clear = new Color[size * size];
        for (int i = 0; i < clear.Length; i++)
            clear[i] = Color.clear;
        texture.SetPixels(clear);

        // Draw outline (dark, slightly thicker) then fill (white)
        DrawX(texture, size, margin, thickness + 3, new Color(0f, 0f, 0f, 0.7f));
        DrawX(texture, size, margin, thickness, Color.white);

        texture.Apply();
        return texture;
    }

    /// <summary>
    /// Draw two diagonal lines forming an X onto the texture.
    /// </summary>
    private void DrawX(Texture2D texture, int size, int margin, int thickness, Color color)
    {
        int start = margin;
        int end = size - margin;
        int length = end - start;

        for (int i = 0; i < length; i++)
        {
            int x1 = start + i;
            int y1 = start + i;
            int y2 = end - 1 - i;

            // Draw thick pixels along both diagonals
            for (int tx = -thickness / 2; tx <= thickness / 2; tx++)
            {
                for (int ty = -thickness / 2; ty <= thickness / 2; ty++)
                {
                    // Diagonal 1 (bottom-left to top-right)
                    SetPixelSafe(texture, size, x1 + tx, y1 + ty, color);
                    // Diagonal 2 (top-left to bottom-right)
                    SetPixelSafe(texture, size, x1 + tx, y2 + ty, color);
                }
            }
        }
    }

    private void SetPixelSafe(Texture2D texture, int size, int x, int y, Color color)
    {
        if (x >= 0 && x < size && y >= 0 && y < size)
        {
            // Only overwrite if current pixel is clear or we're drawing the foreground (white)
            Color existing = texture.GetPixel(x, y);
            if (existing.a < color.a || color == Color.white)
            {
                texture.SetPixel(x, y, color);
            }
        }
    }

    /// <summary>
    /// Show the X marker at the given world position with the specified color.
    /// </summary>
    public void ShowAt(Vector3 worldPosition, Color color)
    {
        _markerObject.transform.position = new Vector3(
            worldPosition.x,
            worldPosition.y,
            ZOffset
        );

        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = color;
        }

        _markerObject.SetActive(true);
    }

    /// <summary>
    /// Show the X marker at the given world position in white.
    /// </summary>
    public void ShowAt(Vector3 worldPosition)
    {
        ShowAt(worldPosition, Color.white);
    }

    /// <summary>
    /// Hide the X marker.
    /// </summary>
    public void Hide()
    {
        if (_markerObject != null)
        {
            _markerObject.SetActive(false);
        }
    }

    /// <summary>
    /// Whether the marker is currently visible.
    /// </summary>
    public bool IsVisible => _markerObject != null && _markerObject.activeSelf;
}
