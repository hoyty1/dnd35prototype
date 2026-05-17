using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Wall of Ice (PHB 3.5e p.299): Evocation [Cold].
///
/// Creates a plane of ice or hemisphere.
/// Supports TWO shapes per PHB:
///   1) WALL (Line Mode): An anchored plane of ice, up to one 10-ft square
///      per caster level (2 squares per CL). 1 inch thick/CL, hardness 0, 3 HP/inch.
///   2) HEMISPHERE (Circle Mode): A hemisphere with radius up to (3 + CL) feet.
///      Convert to squares: floor((3 + CL) / 5).
///
///   • Trapped creatures take CL cold damage (1 HP/CL, no save)
///   • Duration: 1 min/level
///   • Wall blocks movement through its cells
///   • Wall has HP that can be tracked; fire is especially effective
/// </summary>
public class WallOfIceAreaEffect : PersistentAreaEffect
{
    /// <summary>Anchor cell of the wall.</summary>
    public Vector2Int CenterCell { get; set; }

    /// <summary>Length of the wall in squares (Line mode only).</summary>
    public int LengthSquares { get; set; } = 8;

    /// <summary>Direction the wall faces (Line mode).</summary>
    public Vector2Int WallDirection { get; set; } = new Vector2Int(1, 0);

    /// <summary>True if this wall was created in Circle mode (hemisphere).</summary>
    public bool IsCircleMode { get; set; }

    /// <summary>Radius of the circle in squares (Circle mode only).</summary>
    public int CircleRadius { get; set; } = 1;

    /// <summary>Total HP of the wall (3 HP per inch, 1 inch per CL).</summary>
    public int WallHP { get; set; }

    /// <summary>Maximum HP of the wall.</summary>
    public int WallMaxHP { get; set; }

    /// <summary>Thickness in inches (= caster level).</summary>
    public int ThicknessInches { get; set; }

    private HashSet<Vector2Int> _pendingExplicitCells;

    protected override Color GridHighlightColor => AreaEffectColors.WallOfIce;
    protected override bool UseGridHighlighting => true;

    protected override void Awake()
    {
        base.Awake();

        EffectName = "Wall of Ice";
        SpellId = SpellNames.WALL_OF_ICE;
        // Shape is set in Start() based on IsCircleMode
        Shape = AreaShape.Line;
        SizeY = 1;

        ShowVisual = false;
    }

    protected override void Start()
    {
        // Configure shape based on circle vs line mode
        if (IsCircleMode)
        {
            Shape = AreaShape.Circle;
            EffectName = "Wall of Ice (Hemisphere)";
            SizeX = Mathf.Max(1, CircleRadius * 2);
            SizeY = SizeX;
        }
        else
        {
            Shape = AreaShape.Line;
            WallDirection = NormalizeWallDirection(WallDirection);
            DirectionAngle = DirectionFromVector(WallDirection);
            SizeX = Mathf.Max(2, LengthSquares);
        }

        // Calculate wall HP: 3 HP per inch of thickness
        ThicknessInches = Mathf.Max(1, CasterLevel);
        WallMaxHP = ThicknessInches * 3;
        WallHP = WallMaxHP;

        // IMPORTANT: base.Start() calls CalculateAffectedCells() then ApplyInitialEffect().
        // Our override of CalculateAffectedCells() applies _pendingExplicitCells BEFORE
        // ApplyInitialEffect() runs.
        base.Start();
    }

    /// <summary>
    /// Override CalculateAffectedCells to use explicit cells (circle perimeter) when available.
    /// This ensures AffectedCells is correct BEFORE ApplyInitialEffect() runs in base.Start().
    /// </summary>
    protected override void CalculateAffectedCells()
    {
        if (_pendingExplicitCells != null && _pendingExplicitCells.Count > 0)
        {
            AffectedCells = new HashSet<Vector2Int>(_pendingExplicitCells);
            _pendingExplicitCells = null;
            Debug.Log($"[WallOfIce] CalculateAffectedCells: Used {AffectedCells.Count} explicit cells instead of default calculation");
            return;
        }

        base.CalculateAffectedCells();
        Debug.Log($"[WallOfIce] CalculateAffectedCells: Calculated {AffectedCells.Count} cells via base");
    }

    protected override void OnAreaCreated()
    {
        if (IsCircleMode)
        {
            LogEffect($"❄ A hemisphere of ice forms ({CircleRadius * 5}-ft radius, {ThicknessInches} inches thick)!");
        }
        else
        {
            LogEffect($"❄ A wall of ice forms ({SizeX * 5} ft long, {ThicknessInches} inches thick)!");
        }
        LogEffect($"  HP: {WallHP}/{WallMaxHP} (Hardness 0, 3 HP per inch)");
        LogEffect("  • Blocks movement through wall cells");
        LogEffect("  • Creatures caught in wall take cold damage equal to caster level");
        LogEffect("  • Fire damage is especially effective against the wall");
    }

    private void Update()
    {
        UpdateCharacterTracking();

        if (!IsGridHighlightApplied)
            ApplyGridHighlight();
    }

    public override void OnRoundStart()
    {
        base.OnRoundStart();

        if (!IsGridHighlightApplied)
            ApplyGridHighlight();
    }

    protected override void OnCreatureEntersArea(CharacterController character, bool isInitial)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        if (isInitial)
        {
            // Creatures caught when wall forms take CL cold damage (1 HP per CL)
            int coldDamage = Mathf.Max(1, CasterLevel);
            character.Stats.TakeDamage(coldDamage);

            LogEffect($"  ❄ {character.Stats.CharacterName} is caught in the forming Wall of Ice: {coldDamage} cold damage!");

            if (character.Stats.IsDead)
            {
                character.OnDeath();
                LogEffect($"  💀 {character.Stats.CharacterName} is frozen to death!");
            }
        }
        else
        {
            LogEffect($"  {character.Stats.CharacterName} is adjacent to the Wall of Ice.");
        }
    }

    protected override void OnCreatureExitsArea(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return;

        LogEffect($"{character.Stats.CharacterName} moves away from the Wall of Ice.");
    }

    protected override void OnAreaExpires()
    {
        RemoveGridHighlight();
        LogEffect("The Wall of Ice melts away.");
    }

    /// <summary>
    /// Deals damage to the wall. Fire damage is especially effective.
    /// Returns true if the wall is destroyed.
    /// </summary>
    public bool DealDamageToWall(int damage, bool isFire = false)
    {
        // Fire damage is full against ice wall (hardness 0, no reduction)
        // Other damage also full (hardness 0)
        WallHP -= Mathf.Max(0, damage);

        string dmgType = isFire ? "fire" : "";
        LogEffect($"  Wall of Ice takes {damage} {dmgType} damage! ({WallHP}/{WallMaxHP} HP remaining)");

        if (WallHP <= 0)
        {
            LogEffect("  💥 The Wall of Ice shatters!");
            ExpireEffect();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if any active Wall of Ice contains the supplied cell.
    /// </summary>
    public static bool IsCellInAnyWallOfIce(Vector2Int cell)
    {
        if (!AreaEffectManager.HasInstance)
            return false;

        List<WallOfIceAreaEffect> walls = AreaEffectManager.Instance.GetEffectsOfType<WallOfIceAreaEffect>();
        if (walls == null)
            return false;

        for (int i = 0; i < walls.Count; i++)
        {
            WallOfIceAreaEffect wall = walls[i];
            if (wall != null && wall.AffectedCells != null && wall.AffectedCells.Contains(cell))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns the WallOfIceAreaEffect that occupies the given cell, or null if none.
    /// </summary>
    public static WallOfIceAreaEffect GetWallAtCell(Vector2Int cell)
    {
        if (!AreaEffectManager.HasInstance)
            return null;

        List<WallOfIceAreaEffect> walls = AreaEffectManager.Instance.GetEffectsOfType<WallOfIceAreaEffect>();
        if (walls == null)
            return null;

        for (int i = 0; i < walls.Count; i++)
        {
            WallOfIceAreaEffect wall = walls[i];
            if (wall != null && wall.AffectedCells != null && wall.AffectedCells.Contains(cell))
                return wall;
        }
        return null;
    }

    /// <summary>
    /// Returns all cells occupied by any active Wall of Ice.
    /// </summary>
    public static HashSet<Vector2Int> GetAllWallOfIceCells()
    {
        var result = new HashSet<Vector2Int>();
        if (!AreaEffectManager.HasInstance)
            return result;

        List<WallOfIceAreaEffect> walls = AreaEffectManager.Instance.GetEffectsOfType<WallOfIceAreaEffect>();
        if (walls == null)
            return result;

        for (int i = 0; i < walls.Count; i++)
        {
            WallOfIceAreaEffect wall = walls[i];
            if (wall != null && wall.AffectedCells != null)
            {
                foreach (Vector2Int cell in wall.AffectedCells)
                    result.Add(cell);
            }
        }
        return result;
    }

    /// <summary>
    /// Returns a short description of the wall for UI display.
    /// </summary>
    public string GetWallInfoString()
    {
        string mode = IsCircleMode ? "Hemisphere" : "Wall";
        return $"Wall of Ice ({mode}) — {WallHP}/{WallMaxHP} HP (Hardness 0)";
    }

    public void SetExplicitCells(HashSet<Vector2Int> cells)
    {
        if (cells == null || cells.Count == 0)
            return;

        bool hasFinishedStart = AffectedCells != null && AffectedCells.Count > 0
            && AreaEffectManager.HasInstance
            && AreaEffectManager.Instance.GetAllAreaEffects().Contains(this);

        if (hasFinishedStart)
        {
            AffectedCells = new HashSet<Vector2Int>(cells);
            RemoveGridHighlight();
            ApplyGridHighlight();
        }
        else
        {
            _pendingExplicitCells = new HashSet<Vector2Int>(cells);
        }
    }

    private static Vector2Int NormalizeWallDirection(Vector2Int direction)
    {
        if (direction == Vector2Int.zero)
            return new Vector2Int(1, 0);

        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            return new Vector2Int(direction.x >= 0 ? 1 : -1, 0);

        return new Vector2Int(0, direction.y >= 0 ? 1 : -1);
    }

    private static float DirectionFromVector(Vector2Int direction)
    {
        if (direction == Vector2Int.zero)
            return 0f;

        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }
}
