using UnityEngine;

/// <summary>
/// Runtime state + visuals for a Flaming Sphere instance.
/// </summary>
public class FlamingSphereEntity : MonoBehaviour
{
    public CharacterController Caster { get; private set; }
    public SpellData SourceSpell { get; private set; }
    public int RemainingRounds { get; set; }
    public int MaxRangeSquares { get; private set; }
    public int MoveRangeSquares { get; private set; } = 6; // 30 ft
    public Vector2Int GridPosition { get; private set; }
    public bool MovedThisTurn { get; set; }
    public bool WarnedNotMovedThisTurn { get; set; }

    private GameObject _coreVisual;
    private GameObject _glowVisual;

    public void Initialize(CharacterController caster, SpellData spell, Vector2Int startCell, int remainingRounds, int maxRangeSquares)
    {
        Caster = caster;
        SourceSpell = spell;
        RemainingRounds = Mathf.Max(0, remainingRounds);
        MaxRangeSquares = Mathf.Max(0, maxRangeSquares);
        MoveRangeSquares = 6;
        MovedThisTurn = false;
        WarnedNotMovedThisTurn = false;

        EnsureVisuals();
        SetGridPosition(startCell);
    }

    public void SetGridPosition(Vector2Int gridPos)
    {
        GridPosition = gridPos;
        transform.position = SquareGridUtils.GridToWorld(gridPos) + new Vector3(0f, 0f, -0.04f);
    }

    private void EnsureVisuals()
    {
        if (_coreVisual != null)
            return;

        _coreVisual = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _coreVisual.name = "FlamingSphere_Core";
        _coreVisual.transform.SetParent(transform, false);
        _coreVisual.transform.localScale = Vector3.one * 0.72f;

        MeshRenderer coreRenderer = _coreVisual.GetComponent<MeshRenderer>();
        if (coreRenderer != null)
        {
            Material coreMat = CreateUnlitMaterial(new Color(1f, 0.42f, 0.06f, 0.95f));
            coreRenderer.material = coreMat;
            coreRenderer.sortingOrder = 8;
            coreRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            coreRenderer.receiveShadows = false;
        }

        Collider coreCollider = _coreVisual.GetComponent<Collider>();
        if (coreCollider != null)
            Destroy(coreCollider);

        _glowVisual = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _glowVisual.name = "FlamingSphere_Glow";
        _glowVisual.transform.SetParent(transform, false);
        _glowVisual.transform.localScale = Vector3.one * 1.08f;
        _glowVisual.transform.localPosition = new Vector3(0f, 0f, 0.001f);

        MeshRenderer glowRenderer = _glowVisual.GetComponent<MeshRenderer>();
        if (glowRenderer != null)
        {
            Material glowMat = CreateUnlitMaterial(new Color(1f, 0.7f, 0.2f, 0.45f));
            glowRenderer.material = glowMat;
            glowRenderer.sortingOrder = 7;
            glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glowRenderer.receiveShadows = false;
        }

        Collider glowCollider = _glowVisual.GetComponent<Collider>();
        if (glowCollider != null)
            Destroy(glowCollider);
    }

    private static Material CreateUnlitMaterial(Color color)
    {
        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material mat = shader != null ? new Material(shader) : new Material(Shader.Find("Standard"));
        mat.color = color;
        return mat;
    }
}
