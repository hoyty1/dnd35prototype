using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Wall of Fire (PHB 3.5e p.298): Evocation [Fire].
///
/// Creates an immobile, blazing curtain of shimmering violet fire.
/// Supports TWO shapes per PHB:
///   1) WALL (Line Mode): An opaque sheet of flame up to 20 ft/level long.
///      The caster picks start and end points within Medium range.
///   2) RING (Ring Mode): A ring of fire with radius up to 5 ft per 2 CL.
///      The caster picks a center point and radius.
///
/// Damage:
///   • 2d4 fire damage to creatures within 10 ft on the near (hot) side
///   • 1d4 fire damage within 10 ft on the far (cool) side
///   • 2d6+CL (max +20) fire damage to creatures passing through (Reflex half)
///   • Wall is opaque: 50% concealment
///   • Duration: Concentration + 1 round/level
///
/// Simplified prototype: damages creatures standing in the wall cells
/// (pass-through damage) and logs proximity damage for awareness.
/// </summary>
public class WallOfFireAreaEffect : PersistentAreaEffect
{
    /// <summary>Anchor cell of the wall.</summary>
    public Vector2Int CenterCell { get; set; }

    /// <summary>Length of the wall in squares (Line mode only).</summary>
    public int LengthSquares { get; set; } = 8;

    /// <summary>Direction the wall faces (Line mode).</summary>
    public Vector2Int WallDirection { get; set; } = new Vector2Int(1, 0);

    /// <summary>True if this wall was created in Ring mode (circle perimeter).</summary>
    public bool IsRingMode { get; set; }

    /// <summary>Radius of the ring in squares (Ring mode only).</summary>
    public int RingRadius { get; set; } = 1;

    /// <summary>
    /// Heat wave direction for Ring mode: "Inwards" or "Outwards" (PHB p.298).
    /// Inwards = heat radiates toward center (damages creatures inside the ring).
    /// Outwards = heat radiates away from center (damages creatures outside the ring).
    /// Null = not yet selected (defaults to "Inwards" for backward compat).
    /// </summary>
    public string HeatWaveDirectionRing { get; set; }

    /// <summary>
    /// Heat wave direction for Line mode: perpendicular normal vector pointing
    /// toward the "hot" side of the wall (PHB p.298).
    /// Null = not yet selected (defaults to no heat wave direction).
    /// </summary>
    public Vector2? HeatWaveDirectionLine { get; set; }

    private HashSet<Vector2Int> _pendingExplicitCells;

    protected override Color GridHighlightColor => AreaEffectColors.WallOfFire;
    protected override bool UseGridHighlighting => true;

    protected override void Awake()
    {
        base.Awake();

        EffectName = "Wall of Fire";
        SpellId = SpellNames.WALL_OF_FIRE;
        // Shape is set in Start() based on IsRingMode
        Shape = AreaShape.Line;
        SizeY = 1; // 5 ft wide

        ShowVisual = false;
    }

    protected override void Start()
    {
        // Configure shape based on ring vs line mode
        if (IsRingMode)
        {
            Shape = AreaShape.Circle;
            EffectName = "Wall of Fire (Ring)";
            SizeX = Mathf.Max(1, RingRadius * 2);
            SizeY = SizeX;
        }
        else
        {
            Shape = AreaShape.Line;
            WallDirection = NormalizeWallDirection(WallDirection);
            DirectionAngle = DirectionFromVector(WallDirection);
            SizeX = Mathf.Max(2, LengthSquares);
        }

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
        if (IsRingMode)
        {
            string ringDir = !string.IsNullOrEmpty(HeatWaveDirectionRing) ? HeatWaveDirectionRing : "Inwards";
            LogEffect($"🔥 A blazing ring of fire appears ({RingRadius * 5}-ft radius, heat {ringDir.ToLower()})!");
        }
        else
        {
            string lineDir = HeatWaveDirectionLine.HasValue ? $", heat side selected" : "";
            LogEffect($"🔥 A blazing wall of fire appears ({SizeX * 5} ft long{lineDir})!");
        }
        LogEffect("  • 2d4 fire damage to creatures within 10 ft (near side)");
        LogEffect("  • 1d4 fire damage within 10 ft (far side)");
        LogEffect("  • 2d6+CL (max +20) fire damage to those passing through (Reflex half)");
        LogEffect("  • Wall is opaque — provides 50% concealment");
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

        string timing = isInitial ? "is caught in" : "passes through";
        DealPassThroughDamage(character, timing);
    }

    protected override void OnCreatureInAreaAtRoundStart(CharacterController character)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        DealPassThroughDamage(character, "remains in");
    }

    /// <summary>
    /// Deals 2d6 + CL (max +20) fire damage to a creature in the wall. Reflex half.
    /// </summary>
    private void DealPassThroughDamage(CharacterController character, string context)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        int clBonus = Mathf.Min(CasterLevel, 20);
        int damage = Random.Range(1, 7) + Random.Range(1, 7) + clBonus; // 2d6 + CL

        // Reflex save for half
        int saveRoll = Random.Range(1, 21);
        int saveTotal = saveRoll + character.Stats.ReflexSave;
        bool saveSuccess = saveTotal >= SaveDC;

        if (saveSuccess)
            damage = Mathf.Max(1, damage / 2);

        int finalDamage = Mathf.Max(0, damage);

        if (finalDamage > 0)
            character.Stats.TakeDamage(finalDamage);

        LogEffect($"  🔥 {character.Stats.CharacterName} {context} the Wall of Fire: "
            + $"Reflex d20({saveRoll})+{character.Stats.ReflexSave}={saveTotal} vs DC {SaveDC} "
            + $"=> {(saveSuccess ? "half" : "full")} {finalDamage} fire damage");

        if (character.Stats.IsDead)
        {
            character.OnDeath();
            LogEffect($"  💀 {character.Stats.CharacterName} is slain by the Wall of Fire!");
        }
    }

    protected override void OnCreatureExitsArea(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return;

        LogEffect($"{character.Stats.CharacterName} moves away from the Wall of Fire.");
    }

    protected override void OnAreaExpires()
    {
        RemoveGridHighlight();
        LogEffect("The Wall of Fire flickers and fades.");
    }

    /// <summary>
    /// Checks if a ranged attack line crosses any active Wall of Fire cell.
    /// </summary>
    public static bool BlocksLineOfSight(Vector2Int from, Vector2Int to)
    {
        if (!AreaEffectManager.HasInstance)
            return false;

        List<WallOfFireAreaEffect> walls = AreaEffectManager.Instance.GetEffectsOfType<WallOfFireAreaEffect>();
        if (walls == null || walls.Count == 0)
            return false;

        for (int i = 0; i < walls.Count; i++)
        {
            WallOfFireAreaEffect wall = walls[i];
            if (wall == null || wall.AffectedCells == null)
                continue;

            if (WindWallAreaEffect.LineSegmentCrossesAnyCellPublic(from, to, wall.AffectedCells))
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
