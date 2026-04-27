using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AREA EFFECT EXAMPLES:
/// ═══════════════════════════════════════════════════════════════
/// 
/// SQUARE AREAS (SizeX = SizeY):
///   • Grease: Shape.Square, SizeX=2, SizeY=2 (10-ft square)
///   • Stinking Cloud: Shape.Square, SizeX=4, SizeY=4 (20-ft square)
///   • Cloudkill: Shape.Square, SizeX=4, SizeY=4 (20-ft square)
/// 
/// RECTANGULAR AREAS (SizeX != SizeY):
///   • Wall of Fire (horizontal): Shape.Rectangle, SizeX=4, SizeY=1 (20x5 ft)
///   • Wall of Stone: Shape.Rectangle, SizeX=8, SizeY=2 (40x10 ft)
///   • Wall of Thorns: Shape.Rectangle, SizeX=10, SizeY=2 (50x10 ft)
/// 
/// CIRCULAR AREAS (Radius-based):
///   • Fireball: Shape.Circle, Radius=4 (20-ft radius)
///   • Web: Shape.Circle, Radius=4 (20-ft radius spread)
///   • Fog Cloud: Shape.Circle, Radius=4 (20-ft radius)
///   • Sleet Storm: Shape.Circle, Radius=8 (40-ft radius)
/// 
/// LINE AREAS (SizeX=length, SizeY=width):
///   • Lightning Bolt: Shape.Line, SizeX=24, SizeY=1, DirectionAngle=X
///   • Wall of Fire (line): Shape.Line, SizeX=4, SizeY=1, DirectionAngle=X
/// 
/// CONE AREAS (Radius=length, expands):
///   • Burning Hands: Shape.Cone, Radius=3, DirectionAngle=X (15-ft cone)
///   • Cone of Cold: Shape.Cone, Radius=12, DirectionAngle=X (60-ft cone)
/// 
/// CYLINDER AREAS (Radius + height):
///   • Blade Barrier: Shape.Cylinder, Radius=4, VisualHeight=4 (20-ft radius)
///   • Flame Strike: Shape.Cylinder, Radius=2, VisualHeight=8 (10-ft radius)
/// 
/// ═══════════════════════════════════════════════════════════════
/// </summary>

/// <summary>
/// Base class for persistent battlefield area effects (Grease, Wall of Fire, Entangle, etc.).
/// Provides reusable area footprint tracking, lifecycle, round ticking, and visuals.
/// </summary>
public abstract class PersistentAreaEffect : MonoBehaviour
{
    // Core metadata
    public string EffectName { get; protected set; } = "Area Effect";
    public string SpellId { get; protected set; } = string.Empty;
    public CharacterController Caster { get; set; }
    public Vector3 CenterPosition { get; set; }
    public int RoundsRemaining { get; set; }
    public int SaveDC { get; set; }
    public int CasterLevel { get; set; }

    // ═══════════════════════════════════════════════════
    // AREA SHAPE AND SIZE
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Shape of the area effect.
    /// </summary>
    public AreaShape Shape { get; set; } = AreaShape.Square;

    /// <summary>
    /// Width of area in squares (X dimension).
    /// Used by rectangles, squares, and lines.
    /// </summary>
    public int SizeX { get; set; } = 2;

    /// <summary>
    /// Length of area in squares (Y dimension).
    /// Used by rectangles, squares, and lines.
    /// </summary>
    public int SizeY { get; set; } = 2;

    /// <summary>
    /// Radius of area in squares.
    /// Used by circles, cones, and cylinders.
    /// </summary>
    public float Radius { get; set; } = 2f;

    /// <summary>
    /// [DEPRECATED] Use SizeX and SizeY instead.
    /// Kept for backwards compatibility.
    /// </summary>
    [System.Obsolete("Use SizeX and SizeY instead")]
    public int AreaSize
    {
        get => SizeX;
        set
        {
            int clamped = Mathf.Max(1, value);
            SizeX = clamped;
            SizeY = clamped;
        }
    }

    /// <summary>
    /// Direction/orientation for line and cone effects (in degrees).
    /// 0 = East, 90 = North, 180 = West, 270 = South.
    /// </summary>
    public float DirectionAngle { get; set; } = 0f;

    // Visuals
    public Color VisualColor { get; protected set; } = new Color(0.5f, 0.5f, 0.5f, 0.45f);
    public float VisualHeight { get; protected set; } = 0.02f;
    public bool ShowVisual { get; protected set; } = true;

    public HashSet<Vector2Int> AffectedCells { get; protected set; }
    public HashSet<CharacterController> CharactersInArea { get; protected set; }
    public GameObject VisualIndicator { get; protected set; }

    protected GameManager gameManager;

    protected virtual void Awake()
    {
        gameManager = GameManager.Instance != null ? GameManager.Instance : FindObjectOfType<GameManager>();
        AffectedCells = new HashSet<Vector2Int>();
        CharactersInArea = new HashSet<CharacterController>();
    }

    protected virtual void Start()
    {
        if (CenterPosition == Vector3.zero)
            CenterPosition = transform.position;

        transform.position = CenterPosition;

        CalculateAffectedCells();

        if (ShowVisual)
            CreateVisualIndicator();

        OnAreaCreated();
        ApplyInitialEffect();

        AreaEffectManager.Instance.RegisterAreaEffect(this);

        LogEffect($"{EffectName} created at {CenterPosition}");
        LogEffect($"  Duration: {RoundsRemaining} rounds");
        LogEffect($"  Shape: {Shape}");

        switch (Shape)
        {
            case AreaShape.Square:
            case AreaShape.Rectangle:
                LogEffect($"  Dimensions: {Mathf.Max(1, SizeX)}x{Mathf.Max(1, SizeY)} squares ({Mathf.Max(1, SizeX) * 5}x{Mathf.Max(1, SizeY) * 5} ft)");
                break;

            case AreaShape.Circle:
            case AreaShape.Cylinder:
                LogEffect($"  Radius: {Radius:0.##} squares ({Radius * 5f:0.##} ft)");
                break;

            case AreaShape.Line:
                LogEffect($"  Length: {Mathf.Max(1, SizeX)} squares ({Mathf.Max(1, SizeX) * 5} ft)");
                LogEffect($"  Width: {Mathf.Max(1, SizeY)} squares ({Mathf.Max(1, SizeY) * 5} ft)");
                LogEffect($"  Direction: {DirectionAngle:0.#}°");
                break;

            case AreaShape.Cone:
                LogEffect($"  Length: {Radius:0.##} squares ({Radius * 5f:0.##} ft)");
                LogEffect($"  Direction: {DirectionAngle:0.#}°");
                break;
        }

        LogEffect($"  Affected cells: {AffectedCells.Count}");
    }

    protected virtual void OnDestroy()
    {
        if (AreaEffectManager.HasInstance)
            AreaEffectManager.Instance.UnregisterAreaEffect(this);

        if (VisualIndicator != null)
            Destroy(VisualIndicator);
    }

    protected virtual void OnAreaCreated() { }
    protected abstract void OnCreatureEntersArea(CharacterController character, bool isInitial);
    protected virtual void OnCreatureExitsArea(CharacterController character) { }
    protected virtual void OnCreatureInAreaAtRoundStart(CharacterController character) { }
    protected virtual void OnAreaExpires() { }

    /// <summary>
    /// Calculate which cells are affected by this area.
    /// Supports square, rectangle, circle, line, cone, cylinder shapes.
    /// </summary>
    protected virtual void CalculateAffectedCells()
    {
        AffectedCells.Clear();

        Vector2Int anchor = SquareGridUtils.WorldToGrid(CenterPosition);
        int baseX = anchor.x;
        int baseY = anchor.y;

        switch (Shape)
        {
            case AreaShape.Square:
                CalculateSquareArea(baseX, baseY);
                break;

            case AreaShape.Rectangle:
                CalculateRectangleArea(baseX, baseY);
                break;

            case AreaShape.Circle:
                CalculateCircleArea(baseX, baseY);
                break;

            case AreaShape.Line:
                CalculateLineArea(baseX, baseY);
                break;

            case AreaShape.Cone:
                CalculateConeArea(baseX, baseY);
                break;

            case AreaShape.Cylinder:
                CalculateCircleArea(baseX, baseY);
                break;

            default:
                CalculateSquareArea(baseX, baseY);
                break;
        }

        if (gameManager != null && gameManager.Grid != null)
        {
            var filtered = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in AffectedCells)
            {
                if (gameManager.Grid.GetCell(cell) != null)
                    filtered.Add(cell);
            }

            AffectedCells = filtered;
        }

        LogEffect($"Area calculated: {AffectedCells.Count} cells affected");
    }

    /// <summary>
    /// Calculate square area (SizeX x SizeY, typically equal).
    /// </summary>
    private void CalculateSquareArea(int baseX, int baseY)
    {
        CalculateRectangleArea(baseX, baseY);
    }

    /// <summary>
    /// Calculate rectangular area (different X and Y dimensions).
    /// </summary>
    private void CalculateRectangleArea(int baseX, int baseY)
    {
        int width = Mathf.Max(1, SizeX);
        int height = Mathf.Max(1, SizeY);

        int startX = baseX - (width / 2);
        int startY = baseY - (height / 2);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                AffectedCells.Add(new Vector2Int(startX + x, startY + y));
            }
        }
    }

    /// <summary>
    /// Calculate circular area (radius-based).
    /// </summary>
    private void CalculateCircleArea(int baseX, int baseY)
    {
        float effectiveRadius = Mathf.Max(0.5f, Radius);
        int radiusInt = Mathf.CeilToInt(effectiveRadius);

        for (int x = -radiusInt; x <= radiusInt; x++)
        {
            for (int y = -radiusInt; y <= radiusInt; y++)
            {
                float distance = Mathf.Sqrt((x * x) + (y * y));
                if (distance <= effectiveRadius)
                    AffectedCells.Add(new Vector2Int(baseX + x, baseY + y));
            }
        }
    }

    /// <summary>
    /// Calculate line area (oriented by DirectionAngle).
    /// SizeX = length, SizeY = width.
    /// </summary>
    private void CalculateLineArea(int baseX, int baseY)
    {
        int length = Mathf.Max(1, SizeX);
        int width = Mathf.Max(1, SizeY);

        float radians = DirectionAngle * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);

        for (int i = 0; i < length; i++)
        {
            Vector2 point = new Vector2(baseX, baseY) + (direction * i);

            for (int w = 0; w < width; w++)
            {
                float widthOffset = w - ((width - 1) * 0.5f);
                Vector2 widePoint = point + (perpendicular * widthOffset);

                int cellX = Mathf.RoundToInt(widePoint.x);
                int cellY = Mathf.RoundToInt(widePoint.y);
                AffectedCells.Add(new Vector2Int(cellX, cellY));
            }
        }
    }

    /// <summary>
    /// Calculate cone area (expanding from center in DirectionAngle).
    /// Radius controls cone length.
    /// </summary>
    private void CalculateConeArea(int baseX, int baseY)
    {
        float effectiveRadius = Mathf.Max(1f, Radius);
        int length = Mathf.RoundToInt(effectiveRadius);

        float radians = DirectionAngle * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);

        for (int distance = 0; distance <= length; distance++)
        {
            int spread = Mathf.Max(0, distance / 2);

            for (int offset = -spread; offset <= spread; offset++)
            {
                Vector2 point = new Vector2(baseX, baseY) + (direction * distance) + (perpendicular * offset);
                int cellX = Mathf.RoundToInt(point.x);
                int cellY = Mathf.RoundToInt(point.y);
                AffectedCells.Add(new Vector2Int(cellX, cellY));
            }
        }
    }

    public bool IsPositionInArea(Vector3 position)
    {
        Vector2Int cell = SquareGridUtils.WorldToGrid(new Vector2(position.x, position.y));
        return AffectedCells.Contains(cell);
    }

    public bool IsCellInArea(Vector2Int cell)
    {
        return AffectedCells.Contains(cell);
    }

    public bool IsCharacterInArea(CharacterController character)
    {
        if (character == null)
            return false;

        return AffectedCells.Contains(character.GridPosition);
    }

    protected virtual void CreateVisualIndicator()
    {
        if (VisualIndicator != null)
            Destroy(VisualIndicator);

        if (AffectedCells == null || AffectedCells.Count == 0)
            return;

        VisualIndicator = new GameObject($"{EffectName}_AreaVisual");
        VisualIndicator.transform.position = CenterPosition;
        VisualIndicator.transform.SetParent(transform, true);

        GameObject meshObj = CreateVisualMesh();
        if (meshObj == null)
            return;

        meshObj.name = "AreaVisual";
        meshObj.transform.SetParent(VisualIndicator.transform, false);

        Collider col = meshObj.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        MeshRenderer renderer = meshObj.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            Material mat = shader != null ? new Material(shader) : new Material(Shader.Find("Standard"));
            mat.color = VisualColor;
            renderer.material = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 2;
        }
    }

    /// <summary>
    /// Create the visual mesh based on area shape and dimensions.
    /// Uses XY-plane aligned primitives for this project's 2D world layout.
    /// </summary>
    protected virtual GameObject CreateVisualMesh()
    {
        GameObject visual;
        float width = Mathf.Max(1, SizeX) * SquareGridUtils.CellSize;
        float height = Mathf.Max(1, SizeY) * SquareGridUtils.CellSize;
        float diameter = Mathf.Max(1f, Radius * 2f) * SquareGridUtils.CellSize;

        switch (Shape)
        {
            case AreaShape.Square:
            case AreaShape.Rectangle:
                visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
                visual.transform.localScale = new Vector3(width, height, 1f);
                break;

            case AreaShape.Circle:
            case AreaShape.Cylinder:
                visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
                visual.transform.localScale = new Vector3(diameter, diameter, 1f);
                break;

            case AreaShape.Line:
                visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
                visual.transform.localScale = new Vector3(width, height, 1f);
                visual.transform.rotation = Quaternion.Euler(0f, 0f, DirectionAngle);
                break;

            case AreaShape.Cone:
                visual = CreateConeMesh();
                break;

            default:
                visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
                visual.transform.localScale = new Vector3(width, height, 1f);
                break;
        }

        visual.transform.localPosition = new Vector3(0f, 0f, -VisualHeight);
        return visual;
    }

    /// <summary>
    /// Create a cone visual mesh (simple approximation for now).
    /// </summary>
    private GameObject CreateConeMesh()
    {
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
        float diameter = Mathf.Max(1f, Radius * 2f) * SquareGridUtils.CellSize;
        visual.transform.localScale = new Vector3(diameter, diameter, 1f);
        visual.transform.rotation = Quaternion.Euler(0f, 0f, DirectionAngle);
        return visual;
    }

    public virtual void OnRoundStart()
    {
        UpdateCharacterTracking();

        foreach (CharacterController character in CharactersInArea)
        {
            if (character != null && character.Stats != null && !character.Stats.IsDead)
                OnCreatureInAreaAtRoundStart(character);
        }

        RoundsRemaining--;
        if (RoundsRemaining <= 0)
            ExpireEffect();
    }

    protected void UpdateCharacterTracking()
    {
        CharacterController[] allCharacters = FindObjectsOfType<CharacterController>();
        var currentlyInArea = new HashSet<CharacterController>();

        for (int i = 0; i < allCharacters.Length; i++)
        {
            CharacterController character = allCharacters[i];
            if (character == null || character.Stats == null || character.Stats.IsDead)
                continue;

            if (!IsCharacterInArea(character))
                continue;

            currentlyInArea.Add(character);
            if (!CharactersInArea.Contains(character))
                OnCreatureEntersArea(character, isInitial: false);
        }

        foreach (CharacterController previous in CharactersInArea)
        {
            if (previous != null && !currentlyInArea.Contains(previous))
                OnCreatureExitsArea(previous);
        }

        CharactersInArea = currentlyInArea;
    }

    protected void ApplyInitialEffect()
    {
        CharacterController[] allCharacters = FindObjectsOfType<CharacterController>();

        for (int i = 0; i < allCharacters.Length; i++)
        {
            CharacterController character = allCharacters[i];
            if (character == null || character.Stats == null || character.Stats.IsDead)
                continue;

            if (!IsCharacterInArea(character))
                continue;

            CharactersInArea.Add(character);
            OnCreatureEntersArea(character, isInitial: true);
        }
    }

    protected virtual void ExpireEffect()
    {
        OnAreaExpires();

        foreach (CharacterController character in CharactersInArea)
        {
            if (character != null)
                OnCreatureExitsArea(character);
        }

        CharactersInArea.Clear();
        LogEffect("Expires.");
        Destroy(gameObject);
    }

    protected void LogEffect(string message)
    {
        if (gameManager != null && gameManager.CombatUI != null)
            gameManager.CombatUI.ShowCombatLog($"<color=#66E0FF>[{EffectName}]</color> {message}");
        else
            Debug.Log($"[{EffectName}] {message}");
    }
}

/// <summary>
/// Area shape types for persistent area effects.
/// </summary>
public enum AreaShape
{
    /// <summary>
    /// Square area (SizeX = SizeY).
    /// Examples: Grease (2x2), Stinking Cloud (4x4).
    /// </summary>
    Square,

    /// <summary>
    /// Rectangular area (SizeX != SizeY).
    /// Examples: Wall spells in horizontal orientation.
    /// </summary>
    Rectangle,

    /// <summary>
    /// Circular area (Radius-based).
    /// Examples: Fireball, Web, Fog Cloud.
    /// </summary>
    Circle,

    /// <summary>
    /// Line area (SizeX=length, SizeY=width, DirectionAngle).
    /// Examples: Lightning Bolt, Wall of Fire.
    /// </summary>
    Line,

    /// <summary>
    /// Cone area (Radius=length, expands, DirectionAngle).
    /// Examples: Burning Hands, Cone of Cold.
    /// </summary>
    Cone,

    /// <summary>
    /// Cylinder area (Radius + height).
    /// Examples: Blade Barrier, Flame Strike.
    /// </summary>
    Cylinder
}
