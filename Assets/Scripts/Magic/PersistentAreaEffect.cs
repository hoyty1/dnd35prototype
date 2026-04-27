using System.Collections.Generic;
using UnityEngine;

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

    // Shape and visuals
    public AreaShape Shape { get; protected set; } = AreaShape.Square;
    public int AreaSize { get; protected set; } = 2;
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
        LogEffect($"Created at ({CenterPosition.x:F1}, {CenterPosition.y:F1}, {CenterPosition.z:F1}) for {RoundsRemaining} rounds.");
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

    protected virtual void CalculateAffectedCells()
    {
        AffectedCells.Clear();

        Vector2Int anchor = SquareGridUtils.WorldToGrid(CenterPosition);

        switch (Shape)
        {
            case AreaShape.Square:
            {
                for (int x = 0; x < AreaSize; x++)
                {
                    for (int y = 0; y < AreaSize; y++)
                    {
                        AffectedCells.Add(new Vector2Int(anchor.x + x, anchor.y + y));
                    }
                }
                break;
            }
            case AreaShape.Circle:
            {
                int radius = Mathf.Max(1, AreaSize);
                for (int x = -radius; x <= radius; x++)
                {
                    for (int y = -radius; y <= radius; y++)
                    {
                        if ((x * x) + (y * y) <= radius * radius)
                            AffectedCells.Add(new Vector2Int(anchor.x + x, anchor.y + y));
                    }
                }
                break;
            }
            case AreaShape.Line:
            {
                for (int x = 0; x < AreaSize; x++)
                    AffectedCells.Add(new Vector2Int(anchor.x + x, anchor.y));
                break;
            }
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
        if (AffectedCells == null || AffectedCells.Count == 0)
            return;

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;

        foreach (Vector2Int cell in AffectedCells)
        {
            if (cell.x < minX) minX = cell.x;
            if (cell.y < minY) minY = cell.y;
            if (cell.x > maxX) maxX = cell.x;
            if (cell.y > maxY) maxY = cell.y;
        }

        int widthCells = Mathf.Max(1, maxX - minX + 1);
        int heightCells = Mathf.Max(1, maxY - minY + 1);

        Vector3 center = new Vector3(
            (minX + maxX) * 0.5f,
            (minY + maxY) * 0.5f,
            -VisualHeight);

        VisualIndicator = new GameObject($"{EffectName}_AreaVisual");
        VisualIndicator.transform.position = center;
        VisualIndicator.transform.SetParent(transform, true);

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "AreaVisual";
        quad.transform.SetParent(VisualIndicator.transform, false);
        quad.transform.localPosition = Vector3.zero;
        quad.transform.localScale = new Vector3(
            widthCells * SquareGridUtils.CellSize,
            heightCells * SquareGridUtils.CellSize,
            1f);

        Collider col = quad.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
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

public enum AreaShape
{
    Square,
    Circle,
    Line,
    Cone,
    Cylinder
}
