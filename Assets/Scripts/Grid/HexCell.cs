using UnityEngine;

/// <summary>
/// Represents a single cell in the hex grid.
/// Attach to a hex tile GameObject.
/// </summary>
public class HexCell : MonoBehaviour
{
    [HideInInspector] public int Q; // axial q
    [HideInInspector] public int R; // axial r

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

    public void Init(int q, int r)
    {
        Q = q;
        R = r;
        transform.position = HexUtils.AxialToWorld(q, r);
        gameObject.name = $"Hex_{q}_{r}";

        _sr = GetComponent<SpriteRenderer>();
        if (_sr != null)
            _defaultColor = _sr.color;
    }

    public Vector2Int Coords => new Vector2Int(Q, R);

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
    Flanking
}
