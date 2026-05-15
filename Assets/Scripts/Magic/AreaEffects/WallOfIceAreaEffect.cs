using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Wall of Ice (PHB 3.5e p.299): Evocation [Cold].
///
/// Creates a plane of ice or hemisphere.
///   • Wall: 1 inch thick per caster level, hardness 0, 3 HP per inch
///   • Hemisphere traps creatures inside (Reflex negates)
///   • Trapped creatures take CL cold damage (1 HP/CL, no save)
///   • Duration: 1 min/level
///
/// Simplified prototype: Creates a wall of ice that blocks movement
/// through its cells. Creatures caught when wall is placed take
/// cold damage equal to caster level. Wall has HP that can be tracked.
/// </summary>
public class WallOfIceAreaEffect : PersistentAreaEffect
{
    /// <summary>Anchor cell of the wall.</summary>
    public Vector2Int CenterCell { get; set; }

    /// <summary>Length of the wall in squares.</summary>
    public int LengthSquares { get; set; } = 8;

    /// <summary>Direction the wall faces.</summary>
    public Vector2Int WallDirection { get; set; } = new Vector2Int(1, 0);

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
        Shape = AreaShape.Line;
        SizeY = 1;

        ShowVisual = false;
    }

    protected override void Start()
    {
        WallDirection = NormalizeWallDirection(WallDirection);
        DirectionAngle = DirectionFromVector(WallDirection);
        SizeX = Mathf.Max(2, LengthSquares);

        // Calculate wall HP: 3 HP per inch of thickness
        ThicknessInches = Mathf.Max(1, CasterLevel);
        WallMaxHP = ThicknessInches * 3;
        WallHP = WallMaxHP;

        base.Start();

        if (_pendingExplicitCells != null && _pendingExplicitCells.Count > 0)
        {
            AffectedCells = new HashSet<Vector2Int>(_pendingExplicitCells);
            RemoveGridHighlight();
            ApplyGridHighlight();
            _pendingExplicitCells = null;
        }
    }

    protected override void OnAreaCreated()
    {
        LogEffect($"❄ A wall of ice forms ({SizeX * 5} ft long, {ThicknessInches} inches thick)!");
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
