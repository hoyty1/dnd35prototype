using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents a single cell in the square grid.
/// Attach to a square tile GameObject.
/// Supports 8 neighbors: N, NE, E, SE, S, SW, W, NW.
/// </summary>
public class SquareCell : MonoBehaviour
{
    [HideInInspector] public int X; // grid x coordinate
    [HideInInspector] public int Y; // grid y coordinate

    // Characters currently occupying this square.
    // Most gameplay treats the first occupant as the "primary" occupant,
    // but we track all occupants to support shared-space states such as grapples.
    private readonly List<CharacterController> _occupants = new List<CharacterController>();

    /// <summary>
    /// Whether this cell currently has at least one occupant.
    /// </summary>
    public bool IsOccupied => _occupants.Count > 0;

    /// <summary>
    /// Primary occupant for compatibility with existing single-occupant callers.
    /// </summary>
    public CharacterController Occupant => _occupants.Count > 0 ? _occupants[0] : null;

    /// <summary>
    /// Full occupant list for multi-creature overlap scenarios.
    /// </summary>
    public IReadOnlyList<CharacterController> Occupants => _occupants;

    public bool ContainsOccupant(CharacterController occupant)
    {
        return occupant != null && _occupants.Contains(occupant);
    }

    public void AddOccupant(CharacterController occupant)
    {
        if (occupant == null || _occupants.Contains(occupant))
            return;

        _occupants.Add(occupant);
    }

    public void RemoveOccupant(CharacterController occupant)
    {
        if (occupant == null)
            return;

        _occupants.Remove(occupant);
    }

    public void ClearOccupants()
    {
        _occupants.Clear();
    }

    // Items dropped in this square (e.g., disarmed weapons).
    private readonly List<ItemData> _groundItems = new List<ItemData>();
    public IReadOnlyList<ItemData> GroundItems => _groundItems;

    private SpriteRenderer _sr;
    private Color _defaultColor;

    // Optional color overlay used by persistent area effects (mist/fog/etc.).
    private SpriteRenderer _customHighlightOverlay;
    private static Sprite _customHighlightSprite;

    // Small token rendered on top of a cell whenever it has one or more ground items.
    private SpriteRenderer _groundItemTokenRenderer;
    private static Sprite _groundItemTokenSprite;

    private const int GroundItemTokenSortingOrder = 9; // Below character tokens (10), above grid tiles (0).
    private const float GroundItemTokenScale = 0.33f;

    // Highlight colors
    private static readonly Color HighlightMove = new Color(0.3f, 0.8f, 1f, 0.5f);
    private static readonly Color HighlightFiveFootStep = new Color(0.2f, 0.65f, 1f, 0.55f);
    private static readonly Color HighlightAttack = new Color(1f, 0.3f, 0.3f, 0.5f);
    private static readonly Color HighlightAttackRange = new Color(0.95f, 0.9f, 0.25f, 0.45f); // Yellow-green: valid melee range
    private static readonly Color HighlightAttackDeadZone = new Color(0.1f, 0.1f, 0.1f, 0.45f); // Dark gray: dead zone for reach weapons
    private static readonly Color HighlightSelected = new Color(1f, 1f, 0.3f, 0.6f);
    private static readonly Color HighlightFlanking = new Color(1f, 0.6f, 0.0f, 0.6f); // Orange for flanking
    // Range increment zone colors
    private static readonly Color HighlightRangeClose = new Color(0.3f, 0.9f, 0.3f, 0.35f);   // Green: 1st increment, no penalty
    private static readonly Color HighlightRangeMedium = new Color(0.9f, 0.9f, 0.2f, 0.35f);  // Yellow: 2nd-5th increments
    private static readonly Color HighlightRangeFar = new Color(0.9f, 0.5f, 0.1f, 0.35f);     // Orange: 6th-10th increments
    // Spell range colors
    private static readonly Color HighlightSpellRange = new Color(0.4f, 0.3f, 0.9f, 0.3f);    // Purple: spell range area
    private static readonly Color HighlightSpellTarget = new Color(0.9f, 0.3f, 0.9f, 0.55f);  // Bright magenta: valid spell target
    // AoE preview colors
    private static readonly Color HighlightAoEPreview = new Color(1.0f, 0.5f, 0.0f, 0.45f);   // Orange: AoE shape preview
    private static readonly Color HighlightAoETarget = new Color(1.0f, 0.2f, 0.2f, 0.6f);     // Bright red: creature in AoE
    private static readonly Color HighlightAoEAlly = new Color(0.2f, 0.9f, 0.4f, 0.55f);      // Green: ally in AoE (Bless-type)

    public void Init(int x, int y)
    {
        X = x;
        Y = y;
        transform.position = SquareGridUtils.GridToWorld(x, y);
        gameObject.name = $"Square_{x}_{y}";

        _sr = GetComponent<SpriteRenderer>();
        if (_sr != null)
            _defaultColor = _sr.color;

        EnsureGroundItemTokenVisual();
        RefreshGroundItemTokenVisual();
    }

    public Vector2Int Coords => new Vector2Int(X, Y);

    public void SetHighlight(HighlightType type)
    {
        if (_sr == null) return;
        switch (type)
        {
            case HighlightType.None:
                _sr.color = _defaultColor;
                break;
            case HighlightType.Move:
                _sr.color = HighlightMove;
                break;
            case HighlightType.FiveFootStep:
                _sr.color = HighlightFiveFootStep;
                break;
            case HighlightType.Attack:
                _sr.color = HighlightAttack;
                break;
            case HighlightType.AttackRange:
                _sr.color = HighlightAttackRange;
                break;
            case HighlightType.AttackDeadZone:
                _sr.color = HighlightAttackDeadZone;
                break;
            case HighlightType.Selected:
                _sr.color = HighlightSelected;
                break;
            case HighlightType.Flanking:
                _sr.color = HighlightFlanking;
                break;
            case HighlightType.RangeClose:
                _sr.color = HighlightRangeClose;
                break;
            case HighlightType.RangeMedium:
                _sr.color = HighlightRangeMedium;
                break;
            case HighlightType.RangeFar:
                _sr.color = HighlightRangeFar;
                break;
            case HighlightType.SpellRange:
                _sr.color = HighlightSpellRange;
                break;
            case HighlightType.SpellTarget:
                _sr.color = HighlightSpellTarget;
                break;
            case HighlightType.AoEPreview:
                _sr.color = HighlightAoEPreview;
                break;
            case HighlightType.AoETarget:
                _sr.color = HighlightAoETarget;
                break;
            case HighlightType.AoEAlly:
                _sr.color = HighlightAoEAlly;
                break;
        }
    }

    /// <summary>
    /// Apply a custom semi-transparent overlay color without changing base grid highlight logic.
    /// </summary>
    public void SetHighlight(Color color)
    {
        EnsureCustomHighlightOverlay();
        if (_customHighlightOverlay == null)
            return;

        _customHighlightOverlay.color = color;
        _customHighlightOverlay.enabled = true;
    }

    /// <summary>
    /// Clear custom overlay highlight used by persistent area effects.
    /// </summary>
    public void ClearHighlight()
    {
        if (_customHighlightOverlay != null)
            _customHighlightOverlay.enabled = false;
    }

    public void AddGroundItem(ItemData item)
    {
        if (item == null)
            return;

        _groundItems.Add(item);
        RefreshGroundItemTokenVisual();
    }

    public bool RemoveGroundItem(ItemData item)
    {
        if (item == null)
            return false;

        bool removed = _groundItems.Remove(item);
        if (removed)
            RefreshGroundItemTokenVisual();

        return removed;
    }

    private void EnsureCustomHighlightOverlay()
    {
        if (_customHighlightOverlay != null)
            return;

        if (_sr == null)
            _sr = GetComponent<SpriteRenderer>();

        if (_sr == null)
            return;

        GameObject overlayGO = new GameObject("CustomHighlightOverlay");
        overlayGO.transform.SetParent(transform, false);
        overlayGO.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        overlayGO.transform.localScale = new Vector3(SquareGridUtils.CellSize, SquareGridUtils.CellSize, 1f);

        _customHighlightOverlay = overlayGO.AddComponent<SpriteRenderer>();
        // Always use a solid white sprite so the mist tint is visible independently of base tile artwork.
        _customHighlightOverlay.sprite = GetOrCreateCustomHighlightSprite();
        _customHighlightOverlay.sortingLayerID = _sr.sortingLayerID;
        _customHighlightOverlay.sortingOrder = _sr.sortingOrder + 2;
        _customHighlightOverlay.enabled = false;
    }

    private static Sprite GetOrCreateCustomHighlightSprite()
    {
        if (_customHighlightSprite != null)
            return _customHighlightSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        _customHighlightSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        _customHighlightSprite.name = "CellCustomHighlightSprite";
        return _customHighlightSprite;
    }

    private void EnsureGroundItemTokenVisual()
    {
        if (_groundItemTokenRenderer != null)
            return;

        GameObject tokenGO = new GameObject("GroundItemToken");
        tokenGO.transform.SetParent(transform, false);

        float halfCell = SquareGridUtils.CellSize * 0.5f;
        tokenGO.transform.localPosition = new Vector3(halfCell * 0.48f, -halfCell * 0.48f, 0f);
        tokenGO.transform.localScale = new Vector3(GroundItemTokenScale, GroundItemTokenScale, 1f);

        _groundItemTokenRenderer = tokenGO.AddComponent<SpriteRenderer>();
        _groundItemTokenRenderer.sprite = GetOrCreateGroundItemTokenSprite();
        _groundItemTokenRenderer.sortingOrder = GroundItemTokenSortingOrder;
        _groundItemTokenRenderer.enabled = false;
    }

    private void RefreshGroundItemTokenVisual()
    {
        if (_groundItemTokenRenderer == null)
            EnsureGroundItemTokenVisual();

        if (_groundItemTokenRenderer != null)
            _groundItemTokenRenderer.enabled = _groundItems.Count > 0;
    }

    private static Sprite GetOrCreateGroundItemTokenSprite()
    {
        if (_groundItemTokenSprite != null)
            return _groundItemTokenSprite;

        const int size = 20;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color bag = new Color(0.50f, 0.30f, 0.15f, 1f);
        Color bagDark = new Color(0.33f, 0.18f, 0.09f, 1f);
        Color rope = new Color(0.84f, 0.73f, 0.45f, 1f);
        Color coin = new Color(0.96f, 0.82f, 0.24f, 1f);
        Color coinDark = new Color(0.79f, 0.60f, 0.12f, 1f);

        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = clear;
        texture.SetPixels(pixels);

        DrawFilledEllipse(texture, 10, 8, 6, 5, bag);
        DrawFilledEllipse(texture, 10, 13, 3, 2, bag);
        DrawFilledRect(texture, 7, 11, 6, 2, rope);

        DrawFilledEllipse(texture, 7, 5, 2, 2, coin);
        DrawFilledEllipse(texture, 11, 4, 2, 2, coin);
        DrawFilledEllipse(texture, 14, 6, 2, 2, coin);

        DrawEllipseOutline(texture, 10, 8, 6, 5, bagDark);
        DrawEllipseOutline(texture, 10, 13, 3, 2, bagDark);
        DrawEllipseOutline(texture, 7, 5, 2, 2, coinDark);
        DrawEllipseOutline(texture, 11, 4, 2, 2, coinDark);
        DrawEllipseOutline(texture, 14, 6, 2, 2, coinDark);

        texture.Apply();

        _groundItemTokenSprite = Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            size
        );
        _groundItemTokenSprite.name = "GroundItemTokenSprite";
        return _groundItemTokenSprite;
    }

    private static void DrawFilledRect(Texture2D texture, int xMin, int yMin, int width, int height, Color color)
    {
        for (int y = yMin; y < yMin + height; y++)
        {
            for (int x = xMin; x < xMin + width; x++)
            {
                SetPixelSafe(texture, x, y, color);
            }
        }
    }

    private static void DrawFilledEllipse(Texture2D texture, int centerX, int centerY, int radiusX, int radiusY, Color color)
    {
        for (int y = centerY - radiusY; y <= centerY + radiusY; y++)
        {
            for (int x = centerX - radiusX; x <= centerX + radiusX; x++)
            {
                float nx = (x - centerX) / (float)radiusX;
                float ny = (y - centerY) / (float)radiusY;
                if (nx * nx + ny * ny <= 1f)
                    SetPixelSafe(texture, x, y, color);
            }
        }
    }

    private static void DrawEllipseOutline(Texture2D texture, int centerX, int centerY, int radiusX, int radiusY, Color color)
    {
        const float thickness = 0.26f;
        for (int y = centerY - radiusY - 1; y <= centerY + radiusY + 1; y++)
        {
            for (int x = centerX - radiusX - 1; x <= centerX + radiusX + 1; x++)
            {
                float nx = (x - centerX) / (float)radiusX;
                float ny = (y - centerY) / (float)radiusY;
                float d = nx * nx + ny * ny;
                if (Mathf.Abs(1f - d) <= thickness)
                    SetPixelSafe(texture, x, y, color);
            }
        }
    }

    private static void SetPixelSafe(Texture2D texture, int x, int y, Color color)
    {
        if (x < 0 || y < 0 || x >= texture.width || y >= texture.height)
            return;

        texture.SetPixel(x, y, color);
    }

    // OnMouseDown removed — click detection is handled via 2D raycasting
    // in GameManager.Update() for reliable behaviour across Input System modes.
}

public enum HighlightType
{
    None,
    Move,
    FiveFootStep,
    Attack,
    AttackRange,   // Generic melee attackable square preview (yellow-green)
    AttackDeadZone,// Reach dead-zone preview (dark gray)
    Selected,
    Flanking,
    RangeClose,    // 1st range increment (green) - no penalty
    RangeMedium,   // 2nd-5th range increments (yellow) - moderate penalty
    RangeFar,      // 6th-10th range increments (orange) - heavy penalty
    SpellRange,    // Spell range area (purple) - shows all hexes in spell range
    SpellTarget,   // Valid spell target (bright magenta) - clickable target within range
    AoEPreview,    // AoE shape preview (orange) - shows affected area before confirming
    AoETarget,     // Creature in AoE (bright red) - enemy that will be hit
    AoEAlly        // Ally in AoE (green) - ally that will be buffed
}
