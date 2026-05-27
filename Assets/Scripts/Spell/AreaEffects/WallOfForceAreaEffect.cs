using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Wall of Force (PHB 3.5e p.298): Evocation [Force].
///
/// Creates an invisible wall of force.
/// Supports TWO shapes per PHB:
///   1) WALL (Line Mode): An anchored plane of force, up to one 10-ft square
///      per caster level (2 squares per CL).
///   2) SPHERE/HEMISPHERE: Not implemented — PHB allows a sphere or hemisphere
///      but the flat wall is the most common usage.
///
/// Key Properties (PHB p.298):
///   • Invisible, but detectable by detect magic or similar
///   • Immune to ALL damage — cannot be damaged or destroyed by any means
///   • Cannot be dispelled (immune to dispel magic)
///   • Blocks ALL physical passage, line of effect, AND spells
///   • Can ONLY be destroyed by: disintegrate spell, rod of cancellation,
///     or a sphere of annihilation
///   • No saving throw, no spell resistance
///   • Breathable — does not block air
///
/// Duration: 1 round per caster level (not dismissible)
/// Range: Close (25 ft + 5 ft/2 levels)
/// Components: V, S, M (powdered quartz)
/// </summary>
public class WallOfForceAreaEffect : PersistentAreaEffect, ILineOfEffectBlocker
{
    /// <summary>Anchor cell of the wall.</summary>
    public Vector2Int CenterCell { get; set; }

    /// <summary>Length of the wall in squares (Line mode only).</summary>
    public int LengthSquares { get; set; } = 8;

    /// <summary>Direction the wall faces (Line mode).</summary>
    public Vector2Int WallDirection { get; set; } = new Vector2Int(1, 0);

    private HashSet<Vector2Int> _pendingExplicitCells;

    protected override Color GridHighlightColor => AreaEffectColors.WallOfForce;
    protected override bool UseGridHighlighting => true;

    protected override void Awake()
    {
        base.Awake();

        EffectName = "Wall of Force";
        SpellId = SpellNames.WALL_OF_FORCE;
        Shape = AreaShape.Line;
        SizeY = 1;

        // Wall of Force is invisible — don't show mesh visual, only grid highlight
        ShowVisual = false;
    }

    protected override void Start()
    {
        Shape = AreaShape.Line;
        WallDirection = NormalizeWallDirection(WallDirection);
        DirectionAngle = DirectionFromVector(WallDirection);
        SizeX = Mathf.Max(2, LengthSquares);

        // IMPORTANT: base.Start() calls CalculateAffectedCells() then ApplyInitialEffect().
        base.Start();
    }

    /// <summary>
    /// Override CalculateAffectedCells to use explicit cells when available.
    /// </summary>
    protected override void CalculateAffectedCells()
    {
        if (_pendingExplicitCells != null && _pendingExplicitCells.Count > 0)
        {
            AffectedCells = new HashSet<Vector2Int>(_pendingExplicitCells);
            _pendingExplicitCells = null;
            Debug.Log($"[WallOfForce] CalculateAffectedCells: Used {AffectedCells.Count} explicit cells");
            return;
        }

        base.CalculateAffectedCells();
        Debug.Log($"[WallOfForce] CalculateAffectedCells: Calculated {AffectedCells.Count} cells via base");
    }

    protected override void OnAreaCreated()
    {
        // Register with the centralized LoE blocking service
        LineOfEffectService.Register(this);

        LogEffect($"🔷 An invisible Wall of Force forms ({SizeX * 5} ft long)!");
        LogEffect($"  • Blocks ALL movement, spells, and line of effect");
        LogEffect($"  • IMMUNE to all damage — cannot be damaged or dispelled");
        LogEffect($"  • Can only be destroyed by Disintegrate or Rod of Cancellation");
        LogEffect($"  • Duration: {RoundsRemaining} rounds");
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
        // Wall of Force is placed — creatures cannot enter occupied cells.
        // If a creature is somehow in the wall area (shouldn't happen), log it.
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        if (isInitial)
        {
            // PHB p.298: "You can form the wall... so that it is not flush with the surface
            // and leave small holes or gaps." — We allow the caster to place it, but
            // creatures in the path at creation time are pushed to adjacent cells.
            LogEffect($"  ⚠ {character.Stats.CharacterName} is adjacent to the forming Wall of Force.");
        }
    }

    protected override void OnCreatureExitsArea(CharacterController character)
    {
        // No special effect on exit
    }

    protected override void OnAreaExpires()
    {
        // Unregister from centralized LoE blocking service
        LineOfEffectService.Unregister(this);

        RemoveGridHighlight();
        LogEffect("The Wall of Force winks out of existence.");
    }

    // ═══════════════════════════════════════════════════
    // DAMAGE IMMUNITY
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Wall of Force is immune to all damage. Always returns false (never destroyed by damage).
    /// </summary>
    public bool DealDamageToWall(int damage, bool isForce = false)
    {
        LogEffect($"  🛡 Wall of Force is immune to damage! ({damage} damage absorbed)");
        return false;
    }

    /// <summary>
    /// Wall of Force is immune to AoE damage. Always returns false.
    /// </summary>
    public bool DealDamageToOverlappingCells(int damage, HashSet<Vector2Int> aoeCells, bool isForce = false)
    {
        LogEffect($"  🛡 Wall of Force is immune to damage!");
        return false;
    }

    /// <summary>
    /// Disintegrate can destroy Wall of Force (PHB p.222).
    /// Call this method when Disintegrate targets a Wall of Force cell.
    /// Returns true if the wall is destroyed.
    /// </summary>
    public bool OnDisintegrate()
    {
        LogEffect("  💥 Wall of Force is destroyed by Disintegrate!");
        ExpireEffect();
        return true;
    }

    // ═══════════════════════════════════════════════════
    // MOVEMENT BLOCKING
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Check if a cell is part of this wall (all cells block — no breach mechanic).
    /// </summary>
    public bool IsCellIntact(Vector2Int cell)
    {
        return AffectedCells != null && AffectedCells.Contains(cell);
    }

    /// <summary>
    /// Check if movement to a cell is blocked by any active Wall of Force.
    /// Returns true if the cell is blocked.
    /// </summary>
    public static bool DoesCellBlockMovement(Vector2Int cell)
    {
        if (!AreaEffectManager.HasInstance)
            return false;

        List<WallOfForceAreaEffect> walls = AreaEffectManager.Instance.GetEffectsOfType<WallOfForceAreaEffect>();
        if (walls == null)
            return false;

        for (int i = 0; i < walls.Count; i++)
        {
            WallOfForceAreaEffect wall = walls[i];
            if (wall != null && wall.IsCellIntact(cell))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Check if a diagonal move from 'from' to 'to' is blocked by adjacent wall cells.
    /// Same logic as Wall of Ice: diagonal blocked if either corner cell is a wall cell.
    /// </summary>
    public static bool DoesDiagonalMoveCrossWall(Vector2Int from, Vector2Int to)
    {
        int dx = to.x - from.x;
        int dy = to.y - from.y;

        // Only check diagonal moves
        if (Mathf.Abs(dx) != 1 || Mathf.Abs(dy) != 1)
            return false;

        Vector2Int corner1 = new Vector2Int(from.x + dx, from.y);
        Vector2Int corner2 = new Vector2Int(from.x, from.y + dy);

        return DoesCellBlockMovement(corner1) || DoesCellBlockMovement(corner2);
    }

    // ═══════════════════════════════════════════════════
    // LINE OF EFFECT / SIGHT BLOCKING
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Check if a Wall of Force cell blocks line of sight/effect.
    /// All cells block (no breach possible).
    /// </summary>
    public static bool DoesCellBlockLineOfSight(Vector2Int cell)
    {
        return DoesCellBlockMovement(cell);
    }

    /// <summary>
    /// Static check: does any active Wall of Force block LoE between two cells?
    /// Uses Bresenham line traversal.
    /// </summary>
    public static bool BlocksLineOfEffectStatic(Vector2Int from, Vector2Int to)
    {
        if (!AreaEffectManager.HasInstance)
            return false;

        List<WallOfForceAreaEffect> walls = AreaEffectManager.Instance.GetEffectsOfType<WallOfForceAreaEffect>();
        if (walls == null || walls.Count == 0)
            return false;

        HashSet<Vector2Int> allCells = GetAllWallOfForceCells();
        if (allCells.Count == 0)
            return false;

        return WindWallAreaEffect.LineSegmentCrossesAnyCellPublic(from, to, allCells);
    }

    // ═══════════════════════════════════════════════════
    // ILineOfEffectBlocker IMPLEMENTATION
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Instance-level LoE check for THIS wall only.
    /// </summary>
    bool ILineOfEffectBlocker.BlocksLineOfEffect(Vector2Int from, Vector2Int to)
    {
        if (AffectedCells == null || AffectedCells.Count == 0)
            return false;

        return WindWallAreaEffect.LineSegmentCrossesAnyCellPublic(from, to, AffectedCells);
    }

    /// <summary>
    /// Returns all cells of this wall. Wall of Force cells ARE the blocker cells
    /// (spells cannot pass through, but you can still target the wall itself with Disintegrate).
    /// </summary>
    HashSet<Vector2Int> ILineOfEffectBlocker.GetBlockerCells()
    {
        return AffectedCells != null ? new HashSet<Vector2Int>(AffectedCells) : new HashSet<Vector2Int>();
    }

    // ═══════════════════════════════════════════════════
    // CREATION VALIDATION
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Validate that proposed wall cells are not occupied by creatures.
    /// Wall of Force must be anchored to a surface and not pass through creatures.
    /// </summary>
    public static string ValidateWallCreation(HashSet<Vector2Int> proposedCells, SquareGrid grid)
    {
        if (proposedCells == null || proposedCells.Count == 0)
            return "No cells specified for wall creation.";

        if (grid == null)
            return "Grid not available.";

        foreach (Vector2Int cell in proposedCells)
        {
            SquareCell gridCell = grid.GetCell(cell);
            if (gridCell == null)
                continue;

            if (gridCell.IsOccupied)
            {
                IReadOnlyList<CharacterController> occupants = gridCell.Occupants;
                for (int i = 0; i < occupants.Count; i++)
                {
                    CharacterController occupant = occupants[i];
                    if (occupant != null && !occupant.Stats.IsDead)
                    {
                        string name = occupant.Stats.CharacterName;
                        Debug.Log($"[WallOfForce] Creation blocked: cell ({cell.x},{cell.y}) occupied by {name}");
                        return $"⚠ Wall of Force cannot be created — path blocked by {name} at ({cell.x},{cell.y})";
                    }
                }
            }
        }

        return null; // Valid
    }

    // ═══════════════════════════════════════════════════
    // STATIC UTILITY METHODS
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Checks if any active Wall of Force contains the given cell.
    /// </summary>
    public static bool IsCellInAnyWallOfForce(Vector2Int cell)
    {
        if (!AreaEffectManager.HasInstance)
            return false;

        List<WallOfForceAreaEffect> walls = AreaEffectManager.Instance.GetEffectsOfType<WallOfForceAreaEffect>();
        if (walls == null)
            return false;

        for (int i = 0; i < walls.Count; i++)
        {
            WallOfForceAreaEffect wall = walls[i];
            if (wall != null && wall.AffectedCells != null && wall.AffectedCells.Contains(cell))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns the WallOfForceAreaEffect occupying the given cell, or null.
    /// </summary>
    public static WallOfForceAreaEffect GetWallAtCell(Vector2Int cell)
    {
        if (!AreaEffectManager.HasInstance)
            return null;

        List<WallOfForceAreaEffect> walls = AreaEffectManager.Instance.GetEffectsOfType<WallOfForceAreaEffect>();
        if (walls == null)
            return null;

        for (int i = 0; i < walls.Count; i++)
        {
            WallOfForceAreaEffect wall = walls[i];
            if (wall != null && wall.AffectedCells != null && wall.AffectedCells.Contains(cell))
                return wall;
        }
        return null;
    }

    /// <summary>
    /// Returns all cells occupied by any active Wall of Force.
    /// </summary>
    public static HashSet<Vector2Int> GetAllWallOfForceCells()
    {
        var result = new HashSet<Vector2Int>();
        if (!AreaEffectManager.HasInstance)
            return result;

        List<WallOfForceAreaEffect> walls = AreaEffectManager.Instance.GetEffectsOfType<WallOfForceAreaEffect>();
        if (walls == null)
            return result;

        for (int i = 0; i < walls.Count; i++)
        {
            WallOfForceAreaEffect wall = walls[i];
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
        return $"Wall of Force — {(AffectedCells != null ? AffectedCells.Count : 0)} sections (immune to damage, {RoundsRemaining} rounds remaining)";
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
