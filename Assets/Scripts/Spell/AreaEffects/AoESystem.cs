// ============================================================================
// AoESystem.cs — Area of Effect targeting and calculation system
// Handles burst, cube, cone and line AoE shapes on the square grid. Every area's point of
// origin is a grid intersection (GridIntersection; PHB p.175, batch 11 step 24, decision D5).
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shape of an Area of Effect spell.
/// </summary>
public enum AoEShape
{
    None,       // Single target (no AoE)
    Burst,      // Burst (or emanation, spread, sphere, cylinder) around a grid intersection (e.g., Bless, Fireball)
    Cone,       // Quarter circle from an intersection on the edge of the caster's space (e.g., Burning Hands, Cone of Cold)
    Line,       // Line from an intersection on the edge of the caster's space (e.g., Lightning Bolt); walls also use it
    Ring,       // Ring / circle perimeter (e.g., Wall of Fire ring mode) — choose center + radius
    Cube        // Cube placed at a point within range: AoESizeSquares is its side (Order's Wrath's 30-ft cube, PHB p.258;
                // Wall of Thorns' 10-ft cube, Persistent Image's 20-ft cube, PHB p.301, p.260; batch 11 step 23, SPL-042, SPL-046)
}

/// <summary>
/// Filter for which creatures are affected by the AoE.
/// </summary>
public enum AoETargetFilter
{
    All,            // All creatures in area (enemies and allies)
    AlliesOnly,     // Only allies of the caster
    EnemiesOnly,    // Only enemies of the caster
    Self            // Only the caster (rare)
}

/// <summary>
/// A grid intersection, a point where grid lines cross (PHB p.175: the point of origin of an area spell is always a grid
/// intersection; batch 11 step 24, decision D5). Intersection (X, Y) is the lower-left (south-west) corner of square
/// (X, Y), so square (x, y) has the corners (x, y), (x + 1, y), (x, y + 1) and (x + 1, y + 1). In grid units (a square's
/// centre is its coordinates) it lies at (X - 0.5, Y - 0.5).
/// </summary>
public readonly struct GridIntersection : System.IEquatable<GridIntersection>
{
    public readonly int X;
    public readonly int Y;

    public GridIntersection(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <summary>The point in grid units (a square's centre is its coordinates).</summary>
    public Vector2 GridPoint => new Vector2(X - 0.5f, Y - 0.5f);

    /// <summary>The point in world units.</summary>
    public Vector2 WorldPosition => GridPoint * SquareGridUtils.CellSize;

    /// <summary>The intersection nearest a point in grid units; a square's centre gives its north-east corner.</summary>
    public static GridIntersection NearestToGridPoint(Vector2 gridPoint)
        => new GridIntersection(Mathf.FloorToInt(gridPoint.x + 1f), Mathf.FloorToInt(gridPoint.y + 1f));

    /// <summary>The intersection nearest a point in world units (<see cref="NearestToGridPoint"/>).</summary>
    public static GridIntersection NearestToWorldPoint(Vector2 worldPoint)
        => NearestToGridPoint(worldPoint / SquareGridUtils.CellSize);

    /// <summary>The north-east corner of a square.</summary>
    public static GridIntersection NorthEastCornerOf(Vector2Int square) => new GridIntersection(square.x + 1, square.y + 1);

    /// <summary>The square of the four around this intersection that faces <paramref name="square"/> (the one a line from here toward it enters first).</summary>
    public Vector2Int SquareFacing(Vector2Int square)
        => new Vector2Int(square.x >= X ? X : X - 1, square.y >= Y ? Y : Y - 1);

    public bool Equals(GridIntersection other) => X == other.X && Y == other.Y;
    public override bool Equals(object obj) => obj is GridIntersection other && Equals(other);
    public override int GetHashCode() => (X * 397) ^ Y;
    public static bool operator ==(GridIntersection a, GridIntersection b) => a.Equals(b);
    public static bool operator !=(GridIntersection a, GridIntersection b) => !a.Equals(b);
    public override string ToString() => $"+({X},{Y})";
}

/// <summary>
/// One aim of an area: its point of origin or starting intersection, its direction (an index into
/// <see cref="SquareGridUtils.Directions"/> for a cone, -1 otherwise) and its squares (batch 11 step 24).
/// </summary>
public sealed class AreaAim
{
    public readonly GridIntersection Origin;
    public readonly int DirIndex;
    public readonly HashSet<Vector2Int> Cells;

    public AreaAim(GridIntersection origin, int dirIndex, HashSet<Vector2Int> cells)
    {
        Origin = origin;
        DirIndex = dirIndex;
        Cells = cells ?? new HashSet<Vector2Int>();
    }
}

/// <summary>
/// Static utility class for calculating which grid cells fall within
/// various AoE shapes on the square grid.
/// </summary>
public static class AoESystem
{
    // ========== ROUTING (batch 11 step 23) ==========

    /// <summary>
    /// True when a cast of <paramref name="spell"/> resolves over an area (the area targeting and the cast core's area
    /// Resolve stage): an Area target type or an area shape (Prayer, a Self spell with a burst). The PC targeting
    /// (GameManager.BeginPendingSpellTargeting), a scroll's or wand's targeting test and the spell routing golden suite
    /// read it (batch 11 step 23, SPL-042, SPL-046). Every spell it routes has a shape (Tests.Magic.AreaRoutingTests);
    /// Summon Swarm, an Area spell that places a swarm, is routed to its placement first.
    /// </summary>
    public static bool RoutesToArea(SpellData spell)
        => spell != null && (spell.TargetType == SpellTargetType.Area || spell.AoEShapeType != AoEShape.None);

    /// <summary>
    /// True for the shapes placed at a point the caster picks within range (a burst and a cube): their targeting shows the
    /// placement range, the cursor snaps to the nearest grid intersection, which is the area's point of origin, and their
    /// line of effect runs from it (PHB p.175-176; batch 11 step 24, decision D5).
    /// </summary>
    public static bool IsPlacedShape(AoEShape shape) => shape == AoEShape.Burst || shape == AoEShape.Cube;

    /// <summary>
    /// True for a placed shape of no size (Flaming Sphere's sphere, Call Lightning's bolt, <c>AoESizeSquares</c> 0): an
    /// effect put in one square the caster picks, not an area counted from an intersection.
    /// </summary>
    public static bool IsSingleSquarePlacement(SpellData spell)
        => spell != null && IsPlacedShape(spell.AoEShapeType) && spell.AoESizeSquares <= 0;

    /// <summary>
    /// The squares of a placed area (<see cref="IsPlacedShape"/>) whose point of origin is <paramref name="origin"/>: a
    /// burst of <c>AoESizeSquares</c> radius (<see cref="GetBurstCells"/>) or a cube of that side centred on it
    /// (<see cref="GetCubeCells"/>). Empty for any other shape.
    /// </summary>
    public static HashSet<Vector2Int> GetPlacedAreaCells(SpellData spell, GridIntersection origin, SquareGrid grid)
    {
        if (spell == null)
            return new HashSet<Vector2Int>();
        if (spell.AoEShapeType == AoEShape.Burst)
            return GetBurstCells(origin, spell.AoESizeSquares, grid);
        if (spell.AoEShapeType == AoEShape.Cube)
            return GetCubeCells(origin, spell.AoESizeSquares, grid);
        return new HashSet<Vector2Int>();
    }

    // ========== COUNTING FROM AN INTERSECTION (PHB p.175) ==========
    //
    // PHB p.175, Aiming a Spell, Area: the point of origin of a spell is always a grid intersection. Whether a creature is
    // in the area is counted from that point in squares as for movement, intersection to intersection, every second
    // diagonal counting as 2; a square is in the area when its far edge is within the area (touching only its near edge
    // does not count). So a square is in an area of radius r when the count from the origin to the square's far corner is
    // at most r. Every burst is then even-width with four identical quadrants: 4, 12, 24, 44, 96 and 172 squares at 5, 10,
    // 15, 20, 30 and 40 ft. (decision D5 of docs/designs/cast_core_batch11.md, batch 11 step 24). Where the DMG p.28-29
    // "most of the square" guideline differs on an edge square, the PHB count governs.
    // ========================================================================

    /// <summary>
    /// The grid count of a move of <paramref name="a"/> squares along one axis and <paramref name="b"/> along the other:
    /// straight steps count 1, diagonal steps alternate 1 and 2 (PHB p.147), which is max + min / 2 (rounded down).
    /// </summary>
    public static int GridCount(int a, int b)
    {
        a = Mathf.Abs(a);
        b = Mathf.Abs(b);
        return Mathf.Max(a, b) + Mathf.Min(a, b) / 2;
    }

    /// <summary>The grid count between two intersections (PHB p.175: intersection to intersection).</summary>
    public static int CountBetween(GridIntersection a, GridIntersection b) => GridCount(b.X - a.X, b.Y - a.Y);

    /// <summary>The grid count from <paramref name="origin"/> to the far corner of <paramref name="square"/>.</summary>
    public static int CountToFarCorner(GridIntersection origin, Vector2Int square)
    {
        int dx = square.x >= origin.X ? square.x + 1 - origin.X : origin.X - square.x;
        int dy = square.y >= origin.Y ? square.y + 1 - origin.Y : origin.Y - square.y;
        return GridCount(dx, dy);
    }

    /// <summary>True when <paramref name="square"/> is in an area of <paramref name="radiusSquares"/> from <paramref name="origin"/> (its far edge is within it).</summary>
    public static bool IsSquareWithinRadius(GridIntersection origin, int radiusSquares, Vector2Int square)
        => radiusSquares > 0 && CountToFarCorner(origin, square) <= radiusSquares;

    // ========== BURST, EMANATION, SPREAD, SPHERE, CYLINDER ==========

    /// <summary>
    /// The squares of a burst (and of an emanation, spread, sphere or cylinder placed as one) of
    /// <paramref name="radiusSquares"/> (e.g. 4 for 20 ft.) whose point of origin is <paramref name="origin"/>: every square
    /// whose far corner is within the radius counted from the origin (PHB p.175). Squares off the grid are left out (a
    /// null grid keeps them all, for geometry checks). A radius below 1 is taken as 1.
    /// </summary>
    public static HashSet<Vector2Int> GetBurstCells(GridIntersection origin, int radiusSquares, SquareGrid grid)
    {
        var cells = new HashSet<Vector2Int>();
        int r = Mathf.Max(1, radiusSquares);
        for (int x = origin.X - r; x <= origin.X + r - 1; x++)
        {
            for (int y = origin.Y - r; y <= origin.Y + r - 1; y++)
            {
                var pos = new Vector2Int(x, y);
                if (CountToFarCorner(origin, pos) <= r && (grid == null || grid.GetCell(pos) != null))
                    cells.Add(pos);
            }
        }
        return cells;
    }

    // ========== CUBE ==========

    /// <summary>
    /// The squares of a cube of <paramref name="sideSquares"/> squares a side centred on the intersection
    /// <paramref name="centre"/>, its point of origin, which must be in range (delegated RAW reading 2026-10-14: PHB p.175
    /// makes every point of origin an intersection and lists no cube; Order's Wrath is "a burst that fills a 30-ft. cube",
    /// PHB p.258). Order's Wrath's 30-ft. cube, Wall of Thorns' 10-ft. cube, Persistent Image's 20-ft. cube have even
    /// sides, so the cube is centred on its point of origin exactly; an odd side, which no spell has, is placed with the
    /// intersection half a square south-west of its centre. Squares off the grid are left out (a null grid keeps them all).
    /// </summary>
    public static HashSet<Vector2Int> GetCubeCells(GridIntersection centre, int sideSquares, SquareGrid grid)
    {
        var cells = new HashSet<Vector2Int>();
        int side = Mathf.Max(1, sideSquares);
        int lowX = centre.X - side / 2;
        int lowY = centre.Y - side / 2;
        for (int x = lowX; x < lowX + side; x++)
        {
            for (int y = lowY; y < lowY + side; y++)
            {
                var pos = new Vector2Int(x, y);
                if (grid == null || grid.GetCell(pos) != null)
                    cells.Add(pos);
            }
        }
        return cells;
    }

    // ========== CONE ==========
    //
    // PHB p.175: a cone shoots away from the caster in a quarter circle in the direction the caster designates; it starts
    // from any corner of the caster's square. DMG p.29: the caster declares a direction and the intersection where the
    // cone starts. Decision D5 (4) of docs/designs/cast_core_batch11.md, batch 11 step 24:
    //   - The cone is counted from its starting intersection like a burst (a square is in it when its far corner is within
    //     the length).
    //   - A diagonal cone is exactly one quadrant of the burst: 3, 6, 11, 24, 43 and 96 squares at 10, 15, 20, 30, 40 and
    //     60 ft.
    //   - A cone along a grid line is symmetric about the grid line through its starting intersection (delegated RAW
    //     reading 2026-10-14): a square is in it when its centre lies inside the quarter circle's 90-degree wedge; a square
    //     that the wedge's edge cuts exactly in half is in it at the odd diagonal steps along that edge (the 1st, 3rd,
    //     5th...), as the alternating diagonal count runs. That keeps the diagonal cone's number of squares (DMG p.29) at
    //     15, 30 and 60 ft. (6, 24, 96); at 10, 20 and 40 ft. the diagonal count is odd, which no symmetric cone can match,
    //     and the cone has one square more (4, 12, 44).
    //   - Its start is any corner of the caster's square, or for a creature bigger than one square any intersection on
    //     the edge of its space (delegated, by analogy with MM p.69 breath weapons); a cone never covers its source's own
    //     space (it shoots away from the caster).
    // ========================================================================

    /// <summary>True for a diagonal direction index (NE 1, SE 3, SW 5, NW 7 in <see cref="SquareGridUtils.Directions"/>).</summary>
    public static bool IsDiagonalDirection(int dirIndex)
    {
        return dirIndex == 1 || dirIndex == 3 || dirIndex == 5 || dirIndex == 7;
    }

    /// <summary>
    /// True when <paramref name="square"/> is in a cone of <paramref name="lengthSquares"/> starting at
    /// <paramref name="start"/> in direction <paramref name="dirIndex"/> (an index into
    /// <see cref="SquareGridUtils.Directions"/>); see the CONE notes above.
    /// </summary>
    public static bool IsSquareInCone(GridIntersection start, int dirIndex, int lengthSquares, Vector2Int square)
    {
        if (dirIndex < 0 || dirIndex >= SquareGridUtils.Directions.Length)
            return false;
        if (CountToFarCorner(start, square) > Mathf.Max(1, lengthSquares))
            return false;

        Vector2Int d = SquareGridUtils.Directions[dirIndex];
        if (d.x != 0 && d.y != 0)
        {
            // Diagonal: the quadrant of the burst on that side of the start.
            bool inX = d.x > 0 ? square.x >= start.X : square.x < start.X;
            bool inY = d.y > 0 ? square.y >= start.Y : square.y < start.Y;
            return inX && inY;
        }

        // Along a grid line: the forward index f and the lateral index l (both from 0) about the grid line through the start.
        int f, l;
        if (d.x != 0)
        {
            f = d.x > 0 ? square.x - start.X : start.X - 1 - square.x;
            l = square.y >= start.Y ? square.y - start.Y : start.Y - 1 - square.y;
        }
        else
        {
            f = d.y > 0 ? square.y - start.Y : start.Y - 1 - square.y;
            l = square.x >= start.X ? square.x - start.X : start.X - 1 - square.x;
        }
        if (f < 0)
            return false;
        if (l < f)
            return true; // the square's centre is strictly inside the wedge
        // On the wedge's edge (l == f, cut exactly in half): in at the odd diagonal steps f + 1 = 1, 3, 5... (delegated RAW reading 2026-10-14).
        return l == f && f % 2 == 0;
    }

    /// <summary>
    /// The squares of a cone of <paramref name="lengthSquares"/> (e.g. 3 for 15 ft.) starting at the intersection
    /// <paramref name="start"/> in direction <paramref name="dirIndex"/> (one of the 8 in
    /// <see cref="SquareGridUtils.Directions"/>), counted as in <see cref="IsSquareInCone"/>. Squares off the grid are left
    /// out (a null grid keeps them all). Whether the start is legal for the caster (a corner of its square, the cone clear
    /// of its space) is <see cref="IsClearOfSpace"/> and <see cref="GetSpaceEdgeIntersections"/>.
    /// </summary>
    public static HashSet<Vector2Int> GetConeCells(GridIntersection start, int dirIndex, int lengthSquares, SquareGrid grid)
    {
        var cells = new HashSet<Vector2Int>();
        int r = Mathf.Max(1, lengthSquares);
        for (int x = start.X - r; x <= start.X + r - 1; x++)
        {
            for (int y = start.Y - r; y <= start.Y + r - 1; y++)
            {
                var pos = new Vector2Int(x, y);
                if (IsSquareInCone(start, dirIndex, r, pos) && (grid == null || grid.GetCell(pos) != null))
                    cells.Add(pos);
            }
        }
        return cells;
    }

    // ---- Direction Utilities ----

    /// <summary>
    /// Snap a continuous direction vector to one of 8 grid directions.
    /// Returns the index into SquareGridUtils.Directions.
    /// </summary>
    public static int GetClosestDirectionIndex(Vector2 direction)
    {
        if (direction == Vector2.zero) return 0; // Default: North

        float bestDot = -2f;
        int bestIndex = 0;

        for (int i = 0; i < SquareGridUtils.Directions.Length; i++)
        {
            Vector2 d = new Vector2(SquareGridUtils.Directions[i].x, SquareGridUtils.Directions[i].y).normalized;
            float dot = Vector2.Dot(direction, d);
            if (dot > bestDot)
            {
                bestDot = dot;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    // ========== LINE ==========
    //
    // PHB p.175-176: a line shoots away from the caster in the direction the caster designates; it starts from any corner
    // of the caster's square and extends to the limit of its range; it affects the creatures in every square it passes
    // through. Here the line runs from its starting intersection through the point the caster aims at (the centre of the
    // square the caster picks) and keeps the squares whose interior it crosses and whose far corner is within its length,
    // counted from the start as for any area (batch 11 step 24, decision D5). A line passing exactly through an
    // intersection crosses neither square at that corner there.
    // ========================================================================

    /// <summary>
    /// The squares of a line of <paramref name="lengthSquares"/> (e.g. 12 for 60 ft.) starting at the intersection
    /// <paramref name="start"/> and aimed through the grid point <paramref name="towardGrid"/> (grid units: a square's
    /// centre is its coordinates). The point gives only the direction: the line always runs its full length, so a breath
    /// aimed at a creature 100 ft. away still covers its first 60 ft. Squares off the grid are left out (a null grid keeps
    /// them all).
    /// </summary>
    public static HashSet<Vector2Int> GetLineCells(GridIntersection start, Vector2 towardGrid, int lengthSquares, SquareGrid grid)
    {
        var result = new HashSet<Vector2Int>();
        int length = Mathf.Max(1, lengthSquares);
        Vector2 s = start.GridPoint;
        Vector2 dir = towardGrid - s;
        if (dir.sqrMagnitude < 1e-6f)
            return result;
        dir.Normalize();

        // Farther than any square the count can admit (a diagonal of length L reaches at most L squares away).
        Vector2 end = s + dir * (length + 2f);
        foreach (Vector2Int cell in TraceLineThroughGrid(s, end, new Vector2Int(int.MinValue, int.MinValue), grid))
        {
            if (CountToFarCorner(start, cell) <= length)
                result.Add(cell);
        }
        return result;
    }

    /// <summary>
    /// Trace a line segment from start to end through the grid and collect
    /// all cells the line passes through. Uses a boundary-crossing algorithm:
    /// finds all t-values where the line crosses vertical (x = n+0.5) or
    /// horizontal (y = m+0.5) grid boundaries, then samples the midpoint of
    /// each segment to determine which cell the line is in.
    /// </summary>
    /// <param name="start">Start point of the line (grid units)</param>
    /// <param name="end">End point of the line (grid units)</param>
    /// <param name="excludedCell">A cell left out of the result</param>
    /// <param name="grid">Reference to the game grid for bounds checking (null keeps every cell)</param>
    /// <returns>Set of grid cells the line passes through</returns>
    private static HashSet<Vector2Int> TraceLineThroughGrid(
        Vector2 start, Vector2 end, Vector2Int excludedCell, SquareGrid grid)
    {
        var cells = new HashSet<Vector2Int>();

        float dx = end.x - start.x;
        float dy = end.y - start.y;

        if (Mathf.Abs(dx) < 1e-6f && Mathf.Abs(dy) < 1e-6f) return cells;

        // Collect all t-values where the line crosses grid boundaries.
        // Cell (cx, cy) occupies [cx-0.5, cx+0.5] × [cy-0.5, cy+0.5],
        // so boundaries are at x = n+0.5 and y = m+0.5 for integer n, m.
        var tList = new List<float>();
        tList.Add(0f);

        // Vertical boundaries: x = n + 0.5
        if (Mathf.Abs(dx) > 1e-6f)
        {
            float xLo = Mathf.Min(start.x, end.x);
            float xHi = Mathf.Max(start.x, end.x);
            int nLo = Mathf.CeilToInt(xLo - 0.5f);
            int nHi = Mathf.FloorToInt(xHi - 0.5f);
            for (int n = nLo; n <= nHi; n++)
            {
                float t = (n + 0.5f - start.x) / dx;
                if (t > 1e-6f && t < 1f - 1e-6f)
                    tList.Add(t);
            }
        }

        // Horizontal boundaries: y = m + 0.5
        if (Mathf.Abs(dy) > 1e-6f)
        {
            float yLo = Mathf.Min(start.y, end.y);
            float yHi = Mathf.Max(start.y, end.y);
            int mLo = Mathf.CeilToInt(yLo - 0.5f);
            int mHi = Mathf.FloorToInt(yHi - 0.5f);
            for (int m = mLo; m <= mHi; m++)
            {
                float t = (m + 0.5f - start.y) / dy;
                if (t > 1e-6f && t < 1f - 1e-6f)
                    tList.Add(t);
            }
        }

        tList.Add(1f);
        tList.Sort();

        // Remove near-duplicate t-values (occurs when line passes through a
        // grid intersection, where vertical and horizontal crossings coincide)
        var segments = new List<float> { tList[0] };
        for (int i = 1; i < tList.Count; i++)
        {
            if (tList[i] - segments[segments.Count - 1] > 1e-5f)
                segments.Add(tList[i]);
        }

        // Sample the midpoint of each segment to determine which cell
        // the line is passing through during that interval
        for (int i = 0; i < segments.Count - 1; i++)
        {
            float midT = (segments[i] + segments[i + 1]) / 2f;
            float mx = start.x + midT * dx;
            float my = start.y + midT * dy;

            // Cell (cx, cy) contains point (mx, my) when
            // cx - 0.5 <= mx < cx + 0.5, so cx = Floor(mx + 0.5)
            int cx = Mathf.FloorToInt(mx + 0.5f);
            int cy = Mathf.FloorToInt(my + 0.5f);
            Vector2Int cellPos = new Vector2Int(cx, cy);

            if (cellPos != excludedCell && (grid == null || grid.GetCell(cellPos) != null))
                cells.Add(cellPos);
        }

        return cells;
    }

    // ========== ORIGINS AND AIMS (batch 11 step 24, decision D5) ==========

    /// <summary>
    /// The intersections on the edge of a space of <paramref name="sizeSquares"/> by <paramref name="sizeSquares"/>
    /// squares whose lower-left square is <paramref name="basePosition"/>: where a cone or a line starts (PHB p.175 "any
    /// corner of your square"; for a creature bigger than one square any intersection on the edge of its space, delegated
    /// RAW reading 2026-10-14 by analogy with MM p.69 breath weapons: "adjacent to the dragon" is read as touching its
    /// space, so an intersection a square away from it is not a start). The four corners come first, north-east, north-west,
    /// south-east, south-west, so a tie in <see cref="NearestEdgeIntersection"/> goes to the north-east one.
    /// </summary>
    public static List<GridIntersection> GetSpaceEdgeIntersections(Vector2Int basePosition, int sizeSquares)
    {
        int s = Mathf.Max(1, sizeSquares);
        int x0 = basePosition.x, y0 = basePosition.y, x1 = basePosition.x + s, y1 = basePosition.y + s;
        var list = new List<GridIntersection>
        {
            new GridIntersection(x1, y1),
            new GridIntersection(x0, y1),
            new GridIntersection(x1, y0),
            new GridIntersection(x0, y0)
        };
        for (int x = x0 + 1; x < x1; x++)
        {
            list.Add(new GridIntersection(x, y1));
            list.Add(new GridIntersection(x, y0));
        }
        for (int y = y0 + 1; y < y1; y++)
        {
            list.Add(new GridIntersection(x1, y));
            list.Add(new GridIntersection(x0, y));
        }
        return list;
    }

    /// <summary>
    /// The points of origin of an area centred on a creature (a burst or emanation "centered on you"): the intersection at,
    /// or nearest, the centre of its space (decision D5 (3), delegated RAW reading 2026-10-14). A space of odd width (Small,
    /// Medium, Huge) has four, the corners of its central square, which the caster chooses among; an even one (Large,
    /// Gargantuan) has one, its centre. The first is the default (the north-east corner), the one a cast with no choice uses.
    /// The creature's own space is always inside the area.
    /// </summary>
    public static List<GridIntersection> GetCreatureCentredOrigins(Vector2Int basePosition, int sizeSquares)
    {
        int s = Mathf.Max(1, sizeSquares);
        var list = new List<GridIntersection>();
        if (s % 2 == 0)
        {
            list.Add(new GridIntersection(basePosition.x + s / 2, basePosition.y + s / 2));
            return list;
        }
        int cx = basePosition.x + (s - 1) / 2;
        int cy = basePosition.y + (s - 1) / 2;
        list.Add(new GridIntersection(cx + 1, cy + 1));
        list.Add(new GridIntersection(cx, cy + 1));
        list.Add(new GridIntersection(cx + 1, cy));
        list.Add(new GridIntersection(cx, cy));
        return list;
    }

    /// <summary>
    /// The squares of a burst of <paramref name="radiusSquares"/> centred on a creature (from one of its creature-centred
    /// origins, <paramref name="origin"/>): the burst counted from the origin, plus the creature's own space, which is always
    /// inside an area centred on it (decision D5 (3)); the count alone leaves out a Huge creature's far corners in a 10-ft.
    /// burst. Squares off the grid are left out (a null grid keeps them all).
    /// </summary>
    public static HashSet<Vector2Int> GetCreatureCentredBurstCells(Vector2Int basePosition, int sizeSquares, GridIntersection origin,
        int radiusSquares, SquareGrid grid)
    {
        HashSet<Vector2Int> cells = GetBurstCells(origin, radiusSquares, grid);
        int s = Mathf.Max(1, sizeSquares);
        for (int x = basePosition.x; x < basePosition.x + s; x++)
        {
            for (int y = basePosition.y; y < basePosition.y + s; y++)
            {
                var pos = new Vector2Int(x, y);
                if (grid == null || grid.GetCell(pos) != null)
                    cells.Add(pos);
            }
        }
        return cells;
    }

    /// <summary>The creature-centred origins of <paramref name="creature"/>'s space (<see cref="GetCreatureCentredOrigins(Vector2Int,int)"/>).</summary>
    public static List<GridIntersection> GetCreatureCentredOrigins(CharacterController creature)
        => creature == null
            ? new List<GridIntersection>()
            : GetCreatureCentredOrigins(creature.GridPosition, creature.GetCurrentSpaceSquares());

    /// <summary>
    /// The creature-centred origin (<see cref="GetCreatureCentredOrigins(CharacterController)"/>) of a burst of
    /// <paramref name="radiusSquares"/> centred on <paramref name="creature"/> that takes in the most living creatures other
    /// than it (the first on a tie): the corner a handler with no aiming step picks for its caster, PC or NPC alike (Prayer's
    /// and Invisibility Purge's own resolvers; batch 11 step 24). The creature's own space is always inside.
    /// </summary>
    public static GridIntersection BestCreatureCentredOrigin(CharacterController creature, int radiusSquares, SquareGrid grid)
        => BestCreatureCentredOrigin(creature, radiusSquares, grid, null);

    /// <summary>
    /// <see cref="BestCreatureCentredOrigin(CharacterController, int, SquareGrid)"/> scored by <paramref name="score"/>: the
    /// origin whose burst gives the highest sum of <paramref name="score"/> over the living creatures in it other than the
    /// centre creature (null: 1 each), the first on a tie. Invisibility Purge scores invisible enemies +1 and invisible
    /// allies -1, the corner its caster would choose (batch 11 step 24 review; delegated RAW reading 2026-10-14).
    /// </summary>
    public static GridIntersection BestCreatureCentredOrigin(CharacterController creature, int radiusSquares, SquareGrid grid,
        System.Func<CharacterController, int> score)
    {
        List<GridIntersection> origins = GetCreatureCentredOrigins(creature);
        if (origins.Count == 0)
            return default;
        GridIntersection best = origins[0];
        int bestCount = int.MinValue;
        foreach (GridIntersection origin in origins)
        {
            int count = 0;
            HashSet<Vector2Int> cells = GetCreatureCentredBurstCells(creature.GridPosition, creature.GetCurrentSpaceSquares(), origin, radiusSquares, grid);
            foreach (CharacterController c in GetTargetsInArea(cells, creature, AoETargetFilter.All, grid))
            {
                if (c != creature)
                    count += score != null ? score(c) : 1;
            }
            if (count > bestCount)
            {
                bestCount = count;
                best = origin;
            }
        }
        return best;
    }

    /// <summary>
    /// True when any square of <paramref name="creature"/>'s space is in <paramref name="cells"/>: the measure the area's
    /// resolution uses (<see cref="GetTargetsInArea"/> counts every occupied square), which the AI's ally sparing and the
    /// breath weapons use too, so a Large creature covered only by its other squares is counted (batch 11 step 24 review).
    /// </summary>
    public static bool IsCreatureInCells(HashSet<Vector2Int> cells, CharacterController creature)
    {
        if (cells == null || creature == null)
            return false;
        if (cells.Contains(creature.GridPosition))
            return true;
        foreach (Vector2Int square in creature.GetOccupiedSquares())
        {
            if (cells.Contains(square))
                return true;
        }
        return false;
    }

    /// <summary>True when any square of <paramref name="creature"/>'s space is in a burst of <paramref name="radiusSquares"/> from <paramref name="origin"/>.</summary>
    public static bool IsCreatureInBurst(GridIntersection origin, int radiusSquares, CharacterController creature)
    {
        if (creature == null)
            return false;
        foreach (Vector2Int square in creature.GetOccupiedSquares())
        {
            if (IsSquareWithinRadius(origin, radiusSquares, square))
                return true;
        }
        return false;
    }

    /// <summary>The intersection of <paramref name="candidates"/> nearest <paramref name="gridPoint"/> (grid units); the first on a tie.</summary>
    public static GridIntersection NearestOf(List<GridIntersection> candidates, Vector2 gridPoint)
    {
        GridIntersection best = candidates[0];
        float bestD = float.MaxValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            float d = (candidates[i].GridPoint - gridPoint).sqrMagnitude;
            if (d < bestD - 1e-6f)
            {
                bestD = d;
                best = candidates[i];
            }
        }
        return best;
    }

    /// <summary>The intersection on the edge of the space nearest <paramref name="gridPoint"/> (<see cref="GetSpaceEdgeIntersections"/>).</summary>
    public static GridIntersection NearestEdgeIntersection(Vector2Int basePosition, int sizeSquares, Vector2 gridPoint)
        => NearestOf(GetSpaceEdgeIntersections(basePosition, sizeSquares), gridPoint);

    /// <summary>The centre of a space in grid units (a square's centre is its coordinates).</summary>
    public static Vector2 SpaceCentre(Vector2Int basePosition, int sizeSquares)
    {
        float half = (Mathf.Max(1, sizeSquares) - 1) * 0.5f;
        return new Vector2(basePosition.x + half, basePosition.y + half);
    }

    /// <summary>True when <paramref name="gridPoint"/> (grid units) lies inside the space.</summary>
    public static bool IsPointInSpace(Vector2Int basePosition, int sizeSquares, Vector2 gridPoint)
    {
        int s = Mathf.Max(1, sizeSquares);
        return gridPoint.x > basePosition.x - 0.5f && gridPoint.x < basePosition.x + s - 0.5f
               && gridPoint.y > basePosition.y - 0.5f && gridPoint.y < basePosition.y + s - 0.5f;
    }

    /// <summary>True when none of <paramref name="cells"/> is in the space (a cone or line never covers its source's own squares).</summary>
    public static bool IsClearOfSpace(HashSet<Vector2Int> cells, Vector2Int basePosition, int sizeSquares)
    {
        if (cells == null)
            return true;
        int s = Mathf.Max(1, sizeSquares);
        foreach (Vector2Int c in cells)
        {
            if (c.x >= basePosition.x && c.x < basePosition.x + s && c.y >= basePosition.y && c.y < basePosition.y + s)
                return false;
        }
        return true;
    }

    /// <summary>
    /// The grid count from the nearest point of a space to the intersection <paramref name="point"/>: how far a point of
    /// origin is from the caster for the spell's range (counted from the caster's space to the intersection, intersection to
    /// intersection, PHB p.175).
    /// </summary>
    public static int CountFromSpace(Vector2Int basePosition, int sizeSquares, GridIntersection point)
    {
        int s = Mathf.Max(1, sizeSquares);
        int nx = Mathf.Clamp(point.X, basePosition.x, basePosition.x + s);
        int ny = Mathf.Clamp(point.Y, basePosition.y, basePosition.y + s);
        return GridCount(point.X - nx, point.Y - ny);
    }

    /// <summary>
    /// True when the point of origin <paramref name="point"/> is within <paramref name="rangeSquares"/> of the caster's space
    /// (<see cref="CountFromSpace"/>); a negative range means no limit.
    /// </summary>
    public static bool IsOriginWithinRange(Vector2Int casterBase, int casterSize, GridIntersection point, int rangeSquares)
        => rangeSquares < 0 || CountFromSpace(casterBase, casterSize, point) <= rangeSquares;

    /// <summary>
    /// The cone the caster aims toward <paramref name="gridPoint"/> (grid units; the cursor or the target's centre): it
    /// starts at the intersection on the edge of the caster's space nearest the point, in the one of 8 directions closest to
    /// the way from that start to the point. False when the point is inside the caster's space or the cone would cover it.
    /// The PC cursor and the AI's candidate aims use it, so both make the same choices (batch 11 step 24).
    /// </summary>
    public static bool TryAimCone(Vector2Int casterBase, int casterSize, Vector2 gridPoint, int lengthSquares, SquareGrid grid,
        out GridIntersection start, out int dirIndex, out HashSet<Vector2Int> cells)
    {
        start = default;
        dirIndex = -1;
        cells = null;
        if (IsPointInSpace(casterBase, casterSize, gridPoint))
            return false;
        start = NearestEdgeIntersection(casterBase, casterSize, gridPoint);
        Vector2 v = gridPoint - start.GridPoint;
        if (v.sqrMagnitude < 1e-6f)
            return false;
        dirIndex = GetClosestDirectionIndex(v);
        cells = GetConeCells(start, dirIndex, lengthSquares, grid);
        return cells.Count > 0 && IsClearOfSpace(cells, casterBase, casterSize);
    }

    /// <summary>
    /// The line the caster aims through <paramref name="gridPoint"/> (grid units; the centre of the square picked): it
    /// starts at the intersection on the edge of the caster's space nearest the point. False when the point is inside the
    /// caster's space or the line would cross it.
    /// </summary>
    public static bool TryAimLine(Vector2Int casterBase, int casterSize, Vector2 gridPoint, int lengthSquares, SquareGrid grid,
        out GridIntersection start, out HashSet<Vector2Int> cells)
    {
        start = default;
        cells = null;
        if (IsPointInSpace(casterBase, casterSize, gridPoint))
            return false;
        start = NearestEdgeIntersection(casterBase, casterSize, gridPoint);
        cells = GetLineCells(start, gridPoint, lengthSquares, grid);
        return cells.Count > 0 && IsClearOfSpace(cells, casterBase, casterSize);
    }

    /// <summary>
    /// The aims a caster has at a creature for an area of <paramref name="shape"/> and <paramref name="sizeSquares"/>
    /// (radius, side or length), the same choices the PC cursor offers (batch 11 step 24, decision D5):
    /// a burst or cube placed at any intersection of the target's space (the four corners of a Small or Medium creature's
    /// square) within <paramref name="rangeSquares"/> of the caster (negative: no limit); a cone or line from each
    /// intersection on the edge of the caster's space, in the direction of the target's centre, kept when it covers one of
    /// the target's squares and is clear of the caster's space. A placed shape's origin must be in the caster's line of
    /// effect (<see cref="HasLineOfEffectToOrigin"/>), and every aim keeps only the squares its origin has line of effect to
    /// (<see cref="FilterCellsByLineOfEffect(HashSet{Vector2Int}, GridIntersection)"/>, the cast's filter), so the AI scores
    /// and a breath resolves what the area would reach. Order: the target's intersections row by row, or the caster's edge
    /// intersections in <see cref="GetSpaceEdgeIntersections"/> order.
    /// </summary>
    public static List<AreaAim> GetCandidateAimsAt(AoEShape shape, int sizeSquares, Vector2Int casterBase, int casterSize,
        Vector2Int targetBase, int targetSize, int rangeSquares, SquareGrid grid)
    {
        var aims = new List<AreaAim>();
        int ts = Mathf.Max(1, targetSize);
        if (IsPlacedShape(shape))
        {
            for (int y = targetBase.y; y <= targetBase.y + ts; y++)
            {
                for (int x = targetBase.x; x <= targetBase.x + ts; x++)
                {
                    var origin = new GridIntersection(x, y);
                    if (!IsOriginWithinRange(casterBase, casterSize, origin, rangeSquares)
                        || !HasLineOfEffectToOrigin(casterBase, casterSize, origin))
                        continue;
                    HashSet<Vector2Int> cells = shape == AoEShape.Cube
                        ? GetCubeCells(origin, sizeSquares, grid)
                        : GetBurstCells(origin, sizeSquares, grid);
                    RemoveCellsWithoutLineOfEffect(cells, origin);
                    if (cells.Count > 0)
                        aims.Add(new AreaAim(origin, -1, cells));
                }
            }
            return aims;
        }

        if (shape != AoEShape.Cone && shape != AoEShape.Line)
            return aims;

        Vector2 targetCentre = SpaceCentre(targetBase, ts);
        foreach (GridIntersection start in GetSpaceEdgeIntersections(casterBase, casterSize))
        {
            Vector2 v = targetCentre - start.GridPoint;
            if (v.sqrMagnitude < 1e-6f)
                continue;
            int dir = shape == AoEShape.Cone ? GetClosestDirectionIndex(v) : -1;
            HashSet<Vector2Int> cells = shape == AoEShape.Cone
                ? GetConeCells(start, dir, sizeSquares, grid)
                : GetLineCells(start, targetCentre, sizeSquares, grid);
            if (cells.Count == 0 || !IsClearOfSpace(cells, casterBase, casterSize))
                continue;
            RemoveCellsWithoutLineOfEffect(cells, start);
            if (cells.Count == 0 || IsClearOfSpace(cells, targetBase, ts))
                continue;
            aims.Add(new AreaAim(start, dir, cells));
        }
        return aims;
    }

    /// <summary>
    /// The best directed aim (a cone or a line, <see cref="GetCandidateAimsAt"/>) of <paramref name="source"/> standing at
    /// <paramref name="sourceBase"/> at <paramref name="aimTarget"/>: the one that covers the most of the source's enemies
    /// less its allies (TeamUtility, relative to the source, which is never counted), the first on a tie; null when none
    /// covers the target. Used by the breath weapon AI's simulation and by the breath and acid spray themselves, so the
    /// aim the AI scored is the aim that fires (batch 11 step 24).
    /// </summary>
    public static AreaAim BestDirectedAimAt(CharacterController source, Vector2Int sourceBase, AoEShape shape, int lengthSquares,
        CharacterController aimTarget, SquareGrid grid)
    {
        if (source == null || aimTarget == null || grid == null)
            return null;
        List<AreaAim> aims = GetCandidateAimsAt(shape, lengthSquares, sourceBase, source.GetCurrentSpaceSquares(),
            aimTarget.GridPosition, aimTarget.GetCurrentSpaceSquares(), -1, grid);
        AreaAim best = null;
        int bestScore = int.MinValue;
        foreach (AreaAim aim in aims)
        {
            int score = 0;
            foreach (CharacterController c in GetTargetsInArea(aim.Cells, source, AoETargetFilter.All, grid))
            {
                if (c == source)
                    continue;
                if (TeamUtility.IsEnemy(source, c))
                    score++;
                else if (TeamUtility.IsAlly(source, c))
                    score--;
            }
            if (score > bestScore)
            {
                bestScore = score;
                best = aim;
            }
        }
        return best;
    }

    // ========== TARGET FILTERING ==========

    /// <summary>
    /// The living creatures in <paramref name="cellPositions"/> that <paramref name="filter"/> lets the spell affect,
    /// judged relative to <paramref name="caster"/> with TeamUtility (SPL-119, batch 11 step 23): All takes every creature
    /// (the caster too); AlliesOnly the caster and its allies (TeamUtility.IsAlly; Bless, PHB p.205: "the caster and all
    /// allies"); EnemiesOnly the caster's enemies (TeamUtility.IsEnemy; Bane, PHB p.203); Self only the caster. The same
    /// for a Player-team and an Enemy-team caster. A creature that fills several squares of the area counts once.
    /// </summary>
    public static List<CharacterController> GetTargetsInArea(
        HashSet<Vector2Int> cellPositions,
        CharacterController caster,
        AoETargetFilter filter,
        SquareGrid grid)
    {
        var targets = new List<CharacterController>();
        var seenTargets = new HashSet<CharacterController>();
        if (cellPositions == null || grid == null)
            return targets;

        foreach (Vector2Int pos in cellPositions)
        {
            SquareCell cell = grid.GetCell(pos);
            if (cell == null || !cell.IsOccupied || cell.Occupant == null || cell.Occupant.Stats == null || cell.Occupant.Stats.IsDead)
                continue;

            CharacterController occupant = cell.Occupant;
            if (IncludedByFilter(caster, occupant, filter) && seenTargets.Add(occupant))
                targets.Add(occupant);
        }

        return targets;
    }

    /// <summary>Whether <paramref name="filter"/> lets an area spell of <paramref name="caster"/> affect <paramref name="creature"/> (<see cref="GetTargetsInArea"/>).</summary>
    public static bool IncludedByFilter(CharacterController caster, CharacterController creature, AoETargetFilter filter)
    {
        if (creature == null)
            return false;
        switch (filter)
        {
            case AoETargetFilter.All:
                return true;
            case AoETargetFilter.AlliesOnly:
                return creature == caster || TeamUtility.IsAlly(caster, creature);
            case AoETargetFilter.EnemiesOnly:
                return TeamUtility.IsEnemy(caster, creature);
            case AoETargetFilter.Self:
                return creature == caster;
            default:
                return false;
        }
    }

    /// <summary>
    /// Check if a target position is within the spell's placement range from the caster.
    /// For burst spells, this checks if the center point is within range.
    /// Cone spells always originate from the caster, so range is implicit.
    /// </summary>
    public static bool IsWithinCastingRange(Vector2Int casterPos, Vector2Int targetPos, int rangeSquares)
    {
        if (rangeSquares < 0) return true; // Self-range, always valid
        int dist = SquareGridUtils.GetDistance(casterPos, targetPos);
        return dist <= rangeSquares;
    }

    // ================================================================
    //  Wall of Fire — arbitrary-origin line and ring cell computation
    //  (D&D 3.5e PHB p.298)
    // ================================================================

    /// <summary>
    /// Compute grid cells along a line between two arbitrary points (not necessarily
    /// starting from the caster). Used by Wall of Fire Line Mode where the player
    /// picks both start and end points.
    /// </summary>
    /// <param name="startPos">Start of the wall (first click)</param>
    /// <param name="endPos">End of the wall (second click)</param>
    /// <param name="maxLengthSquares">Maximum number of squares the wall can span</param>
    /// <param name="grid">Reference to the game grid</param>
    /// <returns>Set of grid cells the wall passes through (includes both endpoints)</returns>
    public static HashSet<Vector2Int> GetLineCellsBetweenPoints(
        Vector2Int startPos, Vector2Int endPos, int maxLengthSquares, SquareGrid grid)
    {
        var result = new HashSet<Vector2Int>();
        if (grid == null) return result;
        if (startPos == endPos)
        {
            // Single-cell wall (degenerate)
            if (grid.GetCell(startPos) != null)
                result.Add(startPos);
            return result;
        }

        int actualDist = SquareGridUtils.GetDistance(startPos, endPos);
        if (actualDist > maxLengthSquares)
        {
            // Clamp: trace only maxLengthSquares in the chosen direction
            Vector2 dir = new Vector2(endPos.x - startPos.x, endPos.y - startPos.y);
            dir.Normalize();
            endPos = new Vector2Int(
                Mathf.RoundToInt(startPos.x + dir.x * maxLengthSquares),
                Mathf.RoundToInt(startPos.y + dir.y * maxLengthSquares));
        }

        // Trace through grid using the same boundary-crossing algorithm
        Vector2 start = new Vector2(startPos.x, startPos.y);
        Vector2 end = new Vector2(endPos.x, endPos.y);

        // We pass Vector2Int.one * -9999 as "excluded cell" so nothing is excluded
        Vector2Int noExclude = new Vector2Int(-9999, -9999);
        HashSet<Vector2Int> traced = TraceLineThroughGrid(start, end, noExclude, grid);

        foreach (Vector2Int cell in traced)
        {
            int distFromStart = SquareGridUtils.GetDistance(startPos, cell);
            if (distFromStart <= maxLengthSquares && grid.GetCell(cell) != null)
                result.Add(cell);
        }

        // Ensure both endpoints are included if valid
        if (grid.GetCell(startPos) != null)
            result.Add(startPos);
        if (grid.GetCell(endPos) != null
            && SquareGridUtils.GetDistance(startPos, endPos) <= maxLengthSquares)
            result.Add(endPos);

        return result;
    }

    /// <summary>
    /// Compute the cells forming a ring (circle perimeter) of a given radius
    /// centered on a point. Used by Wall of Fire Ring Mode.
    ///
    /// A cell is part of the ring if its D&D 3.5e distance from center == radius.
    /// This creates a 1-cell-wide ring at the specified distance.
    /// </summary>
    /// <param name="center">Center of the ring</param>
    /// <param name="radius">Radius in squares (D&D 3.5e alternating diagonal distance)</param>
    /// <param name="grid">Reference to the game grid</param>
    /// <returns>Set of grid cells forming the ring perimeter</returns>
    public static HashSet<Vector2Int> GetRingCells(Vector2Int center, int radius, SquareGrid grid)
    {
        var result = new HashSet<Vector2Int>();
        if (grid == null || radius <= 0) return result;

        // Check all cells within a bounding box; include cells whose D&D distance == radius
        // For the ring to be visible, we also include cells at distance radius-1 to radius
        // if that would make the ring too thin. But for D&D 3.5e, exactly == radius is correct.
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                Vector2Int cell = new Vector2Int(center.x + dx, center.y + dy);
                int dist = SquareGridUtils.GetDistance(center, cell);

                // Ring = cells exactly at the specified radius distance
                if (dist == radius && grid.GetCell(cell) != null)
                {
                    result.Add(cell);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Preview helper: get ring cells for hover preview during Wall of Fire Ring mode.
    /// Same as GetRingCells but doesn't require a confirmed radius — uses mouse distance.
    /// </summary>
    public static HashSet<Vector2Int> GetRingPreviewCells(
        Vector2Int center, Vector2 mouseWorldPos, int maxRadius, SquareGrid grid)
    {
        Vector2Int mouseGrid = WorldToGrid(mouseWorldPos);
        int hoverRadius = Mathf.Clamp(
            SquareGridUtils.GetDistance(center, mouseGrid), 1, maxRadius);
        return GetRingCells(center, hoverRadius, grid);
    }

    /// <summary>
    /// Convert a world position to grid coordinates.
    /// Public wrapper for use by other systems.
    /// </summary>
    public static Vector2Int WorldToGrid(Vector2 worldPos)
    {
        return SquareGridUtils.WorldToGrid(worldPos);
    }

    // ═══════════════════════════════════════════════════
    // LINE-OF-EFFECT FILTERING (generic — uses LineOfEffectService)
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Filter AoE cells by removing any cell whose line of effect from the AoE
    /// origin is blocked by any registered <see cref="ILineOfEffectBlocker"/>
    /// (Wall of Ice, Resilient Sphere, future wall spells, terrain, etc.).
    /// Cells that belong to a blocker (e.g., intact wall cells, sphere cells) are
    /// kept so the AoE can interact with/damage the blocker.
    /// </summary>
    /// <param name="aoeCells">The original set of AoE cells (modified in place).</param>
    /// <param name="origin">The AoE origin point (burst center, or caster position for cone/line).</param>
    /// <returns>The set of cells that were removed (blocked by LoE).</returns>
    public static HashSet<Vector2Int> FilterCellsByLineOfEffect(
        HashSet<Vector2Int> aoeCells, Vector2Int origin)
    {
        var blockedCells = new HashSet<Vector2Int>();

        if (aoeCells == null || aoeCells.Count == 0)
            return blockedCells;

        // Quick check: any active LoE blockers at all?
        if (!LineOfEffectService.HasBlockers)
            return blockedCells;

        // Gather all "blocker-owned" cells (wall cells, sphere cells, etc.)
        // These are exempt from LoE filtering so the AoE can still damage them.
        HashSet<Vector2Int> blockerCells = LineOfEffectService.GetAllBlockerCells();

        // Check each AoE cell for line-of-effect from origin
        var cellsToCheck = new List<Vector2Int>(aoeCells);
        foreach (Vector2Int cell in cellsToCheck)
        {
            // Don't filter the origin cell itself
            if (cell == origin)
                continue;

            // Don't filter cells that belong to a blocker — AoE should be able
            // to affect the blocker itself (e.g., damage an intact wall cell)
            if (blockerCells.Contains(cell))
                continue;

            // Check if line of effect from origin to this cell is blocked
            if (LineOfEffectService.IsBlocked(origin, cell))
            {
                blockedCells.Add(cell);
            }
        }

        // Remove blocked cells from the AoE
        if (blockedCells.Count > 0)
        {
            aoeCells.ExceptWith(blockedCells);
            Debug.Log($"[AoE/LoE] Filtered {blockedCells.Count} cells blocked by line-of-effect");
        }

        return blockedCells;
    }

    /// <summary>
    /// Filter AoE targets by removing any creature whose position's line of effect
    /// from the AoE origin is blocked by any registered <see cref="ILineOfEffectBlocker"/>.
    /// </summary>
    /// <param name="targets">The list of targets (modified in place).</param>
    /// <param name="origin">The AoE origin point.</param>
    /// <returns>Number of targets removed.</returns>
    public static int FilterTargetsByLineOfEffect(
        List<CharacterController> targets, Vector2Int origin)
    {
        if (targets == null || targets.Count == 0)
            return 0;

        // Quick check: any active LoE blockers?
        if (!LineOfEffectService.HasBlockers)
            return 0;

        // Gather blocker-owned cells (exempt from filtering)
        HashSet<Vector2Int> blockerCells = LineOfEffectService.GetAllBlockerCells();

        int removedCount = 0;
        for (int i = targets.Count - 1; i >= 0; i--)
        {
            CharacterController target = targets[i];
            if (target == null) continue;

            Vector2Int targetPos = target.GridPosition;

            // Don't filter targets standing on the origin cell
            if (targetPos == origin)
                continue;

            // Don't filter targets standing ON a blocker cell
            if (blockerCells.Contains(targetPos))
                continue;

            if (LineOfEffectService.IsBlocked(origin, targetPos))
            {
                Debug.Log($"[AoE/LoE] Target {target.Stats?.CharacterName} at ({targetPos.x},{targetPos.y}) blocked by LoE from origin ({origin.x},{origin.y})");
                targets.RemoveAt(i);
                removedCount++;
            }
        }

        if (removedCount > 0)
            Debug.Log($"[AoE/LoE] Filtered {removedCount} target(s) blocked by line of effect");

        return removedCount;
    }

    /// <summary>
    /// True when line of effect from the intersection <paramref name="origin"/> to <paramref name="square"/> is blocked by a
    /// registered <see cref="ILineOfEffectBlocker"/> (PHB p.176: an area reaches what it has line of effect to from its
    /// origin, a burst's centre point or a cone's starting point). The blockers test square to square, so the line is traced
    /// from the square of the four around the origin that faces the target (<see cref="GridIntersection.SquareFacing"/>), the
    /// first square a straight line from the origin to it enters; when that square is a blocker's own square (the origin on
    /// a wall's face, the target beyond it), the line is blocked (batch 11 step 24).
    /// </summary>
    public static bool IsLineOfEffectBlocked(GridIntersection origin, Vector2Int square)
    {
        if (!LineOfEffectService.HasBlockers)
            return false;
        return IsLineOfEffectBlocked(origin, square, LineOfEffectService.GetAllBlockerCells());
    }

    /// <summary>
    /// <see cref="IsLineOfEffectBlocked(GridIntersection, Vector2Int)"/> with the blockers' own squares,
    /// <paramref name="blockerCells"/>, already gathered. When the square facing the target is itself a blocker's square
    /// (an origin on the face of a wall, the target beyond it), the line enters the blocker first and is blocked: the
    /// blockers test square to square and skip the squares a line starts and ends in, so the facing square must be tested
    /// here (batch 11 step 24 review; PHB p.175: a burst does not affect what has total cover from its origin).
    /// </summary>
    private static bool IsLineOfEffectBlocked(GridIntersection origin, Vector2Int square, HashSet<Vector2Int> blockerCells)
    {
        Vector2Int from = origin.SquareFacing(square);
        if (from == square)
            return false;
        if (blockerCells != null && blockerCells.Contains(from))
            return true;
        return LineOfEffectService.IsBlocked(from, square);
    }

    /// <summary>
    /// True when a caster whose space is <paramref name="casterSize"/> squares a side with its lower-left square at
    /// <paramref name="casterBase"/> has line of effect to the intersection <paramref name="origin"/> (PHB p.176: "You must
    /// have a clear line of effect to the point of origin of any spell you cast, such as the center of a fireball"): some
    /// square of its space reaches the square of the four around the origin that faces it unblocked, and that facing square
    /// is not a blocker's own square (an origin on the far face of a wall). An origin on the caster's own space always
    /// passes. The PC cursor (GameManager.TryBuildUiAreaAim), the cast core's Legality stage (every caster) and the AI's
    /// candidate aims (<see cref="GetCandidateAimsAt"/>) all test it (batch 11 step 24; delegated RAW reading 2026-10-14,
    /// RULES_COVERAGE 4.1).
    /// </summary>
    public static bool HasLineOfEffectToOrigin(Vector2Int casterBase, int casterSize, GridIntersection origin)
    {
        if (!LineOfEffectService.HasBlockers)
            return true;
        HashSet<Vector2Int> blockerCells = LineOfEffectService.GetAllBlockerCells();
        int s = Mathf.Max(1, casterSize);
        for (int x = casterBase.x; x < casterBase.x + s; x++)
        {
            for (int y = casterBase.y; y < casterBase.y + s; y++)
            {
                var square = new Vector2Int(x, y);
                Vector2Int facing = origin.SquareFacing(square);
                if (facing.x >= casterBase.x && facing.x < casterBase.x + s && facing.y >= casterBase.y && facing.y < casterBase.y + s)
                    return true; // the origin is on the caster's own space
                if (blockerCells.Contains(facing))
                    continue;
                if (!LineOfEffectService.IsBlocked(square, facing))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Removes from <paramref name="aoeCells"/> (in place) every square the area's point of origin
    /// <paramref name="origin"/> has no line of effect to (<see cref="IsLineOfEffectBlocked"/>), keeping the squares that
    /// belong to a blocker so the area can affect the blocker itself. Returns the removed squares.
    /// </summary>
    public static HashSet<Vector2Int> FilterCellsByLineOfEffect(HashSet<Vector2Int> aoeCells, GridIntersection origin)
    {
        HashSet<Vector2Int> blockedCells = RemoveCellsWithoutLineOfEffect(aoeCells, origin);
        if (blockedCells.Count > 0)
            Debug.Log($"[AoE/LoE] Filtered {blockedCells.Count} cells blocked by line-of-effect from {origin}");
        return blockedCells;
    }

    /// <summary><see cref="FilterCellsByLineOfEffect(HashSet{Vector2Int}, GridIntersection)"/> without the log line (the AI's candidate aims).</summary>
    private static HashSet<Vector2Int> RemoveCellsWithoutLineOfEffect(HashSet<Vector2Int> aoeCells, GridIntersection origin)
    {
        var blockedCells = new HashSet<Vector2Int>();
        if (aoeCells == null || aoeCells.Count == 0 || !LineOfEffectService.HasBlockers)
            return blockedCells;

        HashSet<Vector2Int> blockerCells = LineOfEffectService.GetAllBlockerCells();
        foreach (Vector2Int cell in aoeCells)
        {
            if (blockerCells.Contains(cell))
                continue;
            if (IsLineOfEffectBlocked(origin, cell, blockerCells))
                blockedCells.Add(cell);
        }
        if (blockedCells.Count > 0)
            aoeCells.ExceptWith(blockedCells);
        return blockedCells;
    }

    /// <summary>
    /// Removes from <paramref name="targets"/> (in place) every creature whose square the area's point of origin
    /// <paramref name="origin"/> has no line of effect to (<see cref="IsLineOfEffectBlocked"/>); a creature standing on a
    /// blocker's square is kept. Returns the number removed.
    /// </summary>
    public static int FilterTargetsByLineOfEffect(List<CharacterController> targets, GridIntersection origin)
    {
        if (targets == null || targets.Count == 0 || !LineOfEffectService.HasBlockers)
            return 0;

        HashSet<Vector2Int> blockerCells = LineOfEffectService.GetAllBlockerCells();
        int removedCount = 0;
        for (int i = targets.Count - 1; i >= 0; i--)
        {
            CharacterController target = targets[i];
            if (target == null)
                continue;
            Vector2Int targetPos = target.GridPosition;
            if (blockerCells.Contains(targetPos))
                continue;
            if (IsLineOfEffectBlocked(origin, targetPos, blockerCells))
            {
                Debug.Log($"[AoE/LoE] Target {target.Stats?.CharacterName} at ({targetPos.x},{targetPos.y}) blocked by LoE from origin {origin}");
                targets.RemoveAt(i);
                removedCount++;
            }
        }
        return removedCount;
    }

    // ═══════════════════════════════════════════════════
    // BACKWARD COMPATIBILITY WRAPPERS (deprecated — use generic methods above)
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// [Deprecated] Use <see cref="FilterCellsByLineOfEffect"/> instead.
    /// Kept for backward compatibility — delegates to the generic method.
    /// </summary>
    public static HashSet<Vector2Int> FilterCellsByWallOfIceLoE(
        HashSet<Vector2Int> aoeCells, Vector2Int origin)
    {
        return FilterCellsByLineOfEffect(aoeCells, origin);
    }

    /// <summary>
    /// [Deprecated] Use <see cref="FilterTargetsByLineOfEffect"/> instead.
    /// Kept for backward compatibility — delegates to the generic method.
    /// </summary>
    public static int FilterTargetsByWallOfIceLoE(
        List<CharacterController> targets, Vector2Int origin)
    {
        return FilterTargetsByLineOfEffect(targets, origin);
    }
}