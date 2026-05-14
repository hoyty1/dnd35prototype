using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Wind Wall (PHB 3.5e p.302): Evocation [Air].
///
/// An invisible vertical curtain of wind 2 ft thick. Wall is up to 10 ft/level
/// long (default 2 squares per caster level) and 5 ft/level high (1 square per
/// caster level). Duration: 1 round/level.
///
/// Effects (per PHB):
///   • Tiny or smaller flying creatures cannot pass through the wall.
///   • Loose materials and cloth garments fly upward when caught in it.
///   • Arrows and bolts are deflected upward (and miss) — see
///     <see cref="BlocksRangedAttack"/>.
///   • Larger ranged weapons (such as spears, javelins) and gases (such as a
///     dragon's breath weapon or a cloudkill cloud) pass through unaffected.
///   • Tiny or smaller occupants take 3d6 nonlethal damage on entry/round.
///   • Disperses fog, smoke, vapors, and similar effects (registers a
///     <see cref="WindEffect"/> with <see cref="WindStrength.Strong"/>).
///   • No save; SR: Yes.
///
/// Implementation notes:
///   • The wall uses <see cref="AreaShape.Line"/> with <c>SizeX</c> as length
///     squares (capped at 2 × caster level) and <c>SizeY</c> = 1 square wide.
///   • <see cref="BlocksRangedAttack"/> performs a line-segment intersection
///     against affected cells. It is a best-effort hook; full integration with
///     the attack pipeline (auto-canceling arrow / bolt attacks at resolution
///     time) is left as a documented extension point — see notes below.
/// </summary>
public class WindWallAreaEffect : PersistentAreaEffect
{
    /// <summary>Anchor cell of the wall (computed from CenterPosition).</summary>
    public Vector2Int CenterCell { get; set; }

    /// <summary>Length of the wall in squares (10 ft/CL).</summary>
    public int LengthSquares { get; set; } = 10;

    /// <summary>Height of the wall in squares (5 ft/CL).</summary>
    public int HeightSquares { get; set; } = 1;

    /// <summary>Direction the wall faces (orthogonal Vector2Int from caster to wall).</summary>
    public Vector2Int WallDirection { get; set; } = new Vector2Int(1, 0);

    private const string OccupantSpellId = "wind_wall_occupant_buffet";

    private WindEffect _registeredWindEffect;
    private HashSet<Vector2Int> _pendingExplicitCells;

    protected override Color GridHighlightColor => AreaEffectColors.WindWall;
    protected override bool UseGridHighlighting => true;

    protected override void Awake()
    {
        base.Awake();

        EffectName = "Wind Wall";
        SpellId = SpellNames.WIND_WALL;
        Shape = AreaShape.Line;
        SizeY = 1; // Always 1 square wide (the wall is 2 ft thick)

        ShowVisual = false;

        // Wind Wall IS the wind — it is not dispersed by other winds.
        DispersibleByWind = false;
    }

    protected override void Start()
    {
        // Apply wall direction → DirectionAngle (degrees) before parent's Start
        // calls CalculateAffectedCells.
        WallDirection = NormalizeWallDirection(WallDirection);
        DirectionAngle = DirectionFromVector(WallDirection);

        // Configure line dimensions based on caster level.
        SizeX = Mathf.Max(2, LengthSquares);

        base.Start();

        // If targeting supplied an explicit footprint, override the auto-derived
        // cells now that base.Start has finished CalculateAffectedCells.
        if (_pendingExplicitCells != null && _pendingExplicitCells.Count > 0)
        {
            AffectedCells = new HashSet<Vector2Int>(_pendingExplicitCells);

            if (_registeredWindEffect != null)
                _registeredWindEffect.AffectedCells = new HashSet<Vector2Int>(_pendingExplicitCells);

            // Refresh grid highlight to reflect the explicit cells.
            RemoveGridHighlight();
            ApplyGridHighlight();
            _pendingExplicitCells = null;
        }
    }

    protected override void OnAreaCreated()
    {
        LogEffect($"💨 An invisible wall of roaring wind appears ({SizeX * 5} ft long, {HeightSquares * 5} ft high).");
        LogEffect("  • Arrows, bolts, and tiny flying creatures cannot cross.");
        LogEffect("  • Larger ranged weapons (spears, javelins) pass unaffected.");
        LogEffect("  • Disperses gases, fog, and smoke.");

        // Register a wind effect that disperses fog/smoke/cloud-type effects.
        // Wind Wall is described as a "roaring blast"; we model it as Strong wind
        // which is sufficient to disperse fog/cloud effects per the wind table.
        RegisterAsWindEffect();
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

        string timing = isInitial ? "is caught in" : "enters";
        LogEffect($"{character.Stats.CharacterName} {timing} the wall of wind.");

        ApplyOccupantBuffet(character);
    }

    protected override void OnCreatureInAreaAtRoundStart(CharacterController character)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        ApplyOccupantBuffet(character);
    }

    protected override void OnCreatureExitsArea(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return;

        LogEffect($"{character.Stats.CharacterName} steps out of the wind wall.");
    }

    protected override void OnAreaExpires()
    {
        UnregisterWindEffect();
        RemoveGridHighlight();
        LogEffect("The Wind Wall fades, and the wind dies down.");
    }

    // ═══════════════════════════════════════════════════════════════
    // OCCUPANT EFFECT — 3d6 nonlethal to Tiny or smaller creatures
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Per PHB: an occupant of the wall takes 3d6 nonlethal damage if Tiny or
    /// smaller. Larger creatures aren't damaged but still feel the buffet.
    /// </summary>
    private void ApplyOccupantBuffet(CharacterController character)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead)
            return;

        SizeCategory size = character.Stats.CurrentSizeCategory;
        bool isTinyOrSmaller =
            size == SizeCategory.Tiny ||
            size == SizeCategory.Diminutive ||
            size == SizeCategory.Fine;

        if (!isTinyOrSmaller)
            return;

        int dmg = Random.Range(1, 7) + Random.Range(1, 7) + Random.Range(1, 7); // 3d6
        character.Stats.ApplyNonlethalDamage(dmg);
        LogEffect($"  💨 {character.Stats.CharacterName} ({size}) is buffeted: {dmg} nonlethal damage.");
    }

    // ═══════════════════════════════════════════════════════════════
    // RANGED ATTACK DEFLECTION
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns true if a ranged attack from <paramref name="attacker"/> against
    /// <paramref name="target"/> would be intercepted by ANY active Wind Wall.
    ///
    /// The check returns true when the straight-line segment between the two
    /// creatures crosses any cell that belongs to a Wind Wall (and the weapon
    /// is a small projectile such as an arrow, bolt, sling stone, or thrown
    /// dart — anything Tiny or smaller). Larger thrown weapons (spears,
    /// javelins) are not blocked.
    ///
    /// <para>
    /// <b>Integration note:</b> For a deflection to actually negate a ranged
    /// attack at resolution time, the attack pipeline (e.g. ranged attack
    /// resolution in <c>CharacterController</c>) must call this helper and
    /// short-circuit the attack. This implementation provides the geometry
    /// hook; full pipeline integration is a future enhancement and is treated
    /// as best-effort. Simple arrow/bolt projectile spells (Magic Missile is
    /// unaffected per its spell text since it has no attack roll) will not be
    /// auto-blocked unless their resolver explicitly consults this helper.
    /// </para>
    /// </summary>
    /// <param name="attacker">Source of the ranged attack.</param>
    /// <param name="target">Target of the ranged attack.</param>
    /// <param name="weaponIsSmallProjectile">
    /// True if the weapon is a small projectile (arrow, bolt, sling stone,
    /// dart, etc.). Pass false for thrown spears/javelins — Wind Wall does
    /// not deflect those.
    /// </param>
    public static bool BlocksRangedAttack(
        CharacterController attacker,
        CharacterController target,
        bool weaponIsSmallProjectile = true)
    {
        if (!weaponIsSmallProjectile)
            return false;

        if (attacker == null || target == null)
            return false;

        if (!AreaEffectManager.HasInstance)
            return false;

        List<WindWallAreaEffect> walls = AreaEffectManager.Instance.GetEffectsOfType<WindWallAreaEffect>();
        if (walls == null || walls.Count == 0)
            return false;

        Vector2Int from = attacker.GridPosition;
        Vector2Int to = target.GridPosition;

        for (int i = 0; i < walls.Count; i++)
        {
            WindWallAreaEffect wall = walls[i];
            if (wall == null || wall.AffectedCells == null)
                continue;

            if (LineSegmentCrossesAnyCell(from, to, wall.AffectedCells))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Convenience overload defaulting to small projectile (the common case
    /// for which Wind Wall actually blocks the attack).
    /// </summary>
    public static bool BlocksRangedAttack(CharacterController attacker, CharacterController target)
    {
        return BlocksRangedAttack(attacker, target, weaponIsSmallProjectile: true);
    }

    /// <summary>
    /// Returns true if any active Wind Wall contains the supplied cell.
    /// Useful as a quick "does this square sit in a wall?" check.
    /// </summary>
    public static bool IsCellInAnyWindWall(Vector2Int cell)
    {
        if (!AreaEffectManager.HasInstance)
            return false;

        List<WindWallAreaEffect> walls = AreaEffectManager.Instance.GetEffectsOfType<WindWallAreaEffect>();
        if (walls == null)
            return false;

        for (int i = 0; i < walls.Count; i++)
        {
            WindWallAreaEffect wall = walls[i];
            if (wall != null && wall.AffectedCells != null && wall.AffectedCells.Contains(cell))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Bresenham-style line traversal between two grid cells. Returns true if
    /// any visited cell (excluding the very endpoints) is contained in
    /// <paramref name="cells"/>. Endpoints are excluded so that a creature
    /// standing on the same cell as the wall is treated as inside (not
    /// blocked) and so that the attacker's own square doesn't trigger.
    /// </summary>
    private static bool LineSegmentCrossesAnyCell(Vector2Int from, Vector2Int to, HashSet<Vector2Int> cells)
    {
        if (cells == null || cells.Count == 0)
            return false;

        int x0 = from.x, y0 = from.y;
        int x1 = to.x, y1 = to.y;

        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        int safety = dx + dy + 4; // safety bound — never iterate forever
        int steps = 0;
        int curX = x0, curY = y0;

        while (steps++ < safety)
        {
            // Skip the start cell on the first iteration; otherwise check.
            if (!(curX == x0 && curY == y0) && !(curX == x1 && curY == y1))
            {
                if (cells.Contains(new Vector2Int(curX, curY)))
                    return true;
            }

            if (curX == x1 && curY == y1)
                break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                curX += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                curY += sy;
            }
        }

        return false;
    }

    // ═══════════════════════════════════════════════════════════════
    // WIND EFFECT REGISTRATION (fog/smoke dispersion)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Registers the wall with WindEffectManager so it disperses fog/smoke/
    /// cloud-type effects over its footprint. Wind strength is set to Strong
    /// (one round to disperse fog effects).
    /// </summary>
    private void RegisterAsWindEffect()
    {
        if (!WindEffectManager.HasInstance)
            return;

        Vector3 origin = SquareGridUtils.GridToWorld(CenterCell);
        Vector3 dir = new Vector3(WallDirection.x, WallDirection.y, 0f);

        _registeredWindEffect = new WindEffect
        {
            EffectName = "Wind Wall",
            Caster = Caster,
            OriginPosition = origin,
            Direction = dir,
            Length = Mathf.Max(1, SizeX),
            AffectedRadius = 0f,
            Strength = WindStrength.Strong,
            RoundsRemaining = Mathf.Max(1, RoundsRemaining),
            SaveDC = 0,
            AffectedCells = AffectedCells != null ? new HashSet<Vector2Int>(AffectedCells) : null
        };

        WindEffectManager.Instance.RegisterWindEffect(_registeredWindEffect);
    }

    private void UnregisterWindEffect()
    {
        if (_registeredWindEffect == null || !WindEffectManager.HasInstance)
            return;

        WindEffectManager.Instance.RemoveWindEffect(_registeredWindEffect);
        _registeredWindEffect = null;
    }

    // ═══════════════════════════════════════════════════════════════
    // CELL-OVERRIDE SUPPORT (used when targeting supplies explicit AoE)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Overrides the auto-calculated AffectedCells with the provided set.
    /// Useful when the spell targeting system has already chosen the wall's
    /// footprint and we want to honor that exact set rather than re-deriving
    /// it from CenterPosition + SizeX/SizeY.
    ///
    /// Safe to call before <c>Start</c>: the override is applied after base
    /// Start finishes <c>CalculateAffectedCells</c>. Safe to call after Start:
    /// the cells are applied immediately and the wind effect (if registered)
    /// is updated.
    /// </summary>
    public void SetExplicitCells(HashSet<Vector2Int> cells)
    {
        if (cells == null || cells.Count == 0)
            return;

        // If the area has already finished its Start phase (registered with
        // AreaEffectManager), apply immediately. Otherwise queue for Start to
        // pick up after CalculateAffectedCells runs.
        bool hasFinishedStart = AffectedCells != null && AffectedCells.Count > 0
            && AreaEffectManager.HasInstance
            && AreaEffectManager.Instance.GetAllAreaEffects().Contains(this);

        if (hasFinishedStart)
        {
            AffectedCells = new HashSet<Vector2Int>(cells);

            if (_registeredWindEffect != null)
                _registeredWindEffect.AffectedCells = new HashSet<Vector2Int>(cells);

            RemoveGridHighlight();
            ApplyGridHighlight();
        }
        else
        {
            _pendingExplicitCells = new HashSet<Vector2Int>(cells);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════

    private static Vector2Int NormalizeWallDirection(Vector2Int direction)
    {
        if (direction == Vector2Int.zero)
            return new Vector2Int(1, 0);

        // Snap to one of the four cardinal directions.
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
