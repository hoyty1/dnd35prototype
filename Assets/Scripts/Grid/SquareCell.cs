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

    /// <summary>
    /// Whether this cell is occupied by a character.
    /// </summary>
    public bool IsOccupied { get; set; }

    /// <summary>
    /// Reference to the character occupying this cell (null if empty).
    /// </summary>
    public CharacterController Occupant { get; set; }

    private SpriteRenderer _sr;
    private Color _defaultColor;

    // Highlight colors
    private static readonly Color HighlightMove = new Color(0.3f, 0.8f, 1f, 0.5f);
    private static readonly Color HighlightAttack = new Color(1f, 0.3f, 0.3f, 0.5f);
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
            case HighlightType.Attack:
                _sr.color = HighlightAttack;
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

    // OnMouseDown removed — click detection is handled via 2D raycasting
    // in GameManager.Update() for reliable behaviour across Input System modes.
}

public enum HighlightType
{
    None,
    Move,
    Attack,
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
