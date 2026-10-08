using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Picks the grid square each encounter creature spawns on (ENC-001). Every spawn path in
/// <c>GameManager.SetupEnemyEncounter</c> (test presets, custom encounters, the scenario harness and the generic
/// random/DMG path) asks this for the creature's base square, so every enemy lands on the grid with its whole
/// footprint (PHB p.149, Table 8-4: Large 2x2, Huge 3x3, Gargantuan 4x4, Colossal 6x6 squares; the base square is
/// the footprint's lowest x and y) on squares no other creature holds.
///
/// Rules: a preferred square (a preset's or a custom position) is kept when the footprint fits there; otherwise the
/// nearest square where it fits is taken. A creature without a preferred square takes the next square of
/// <see cref="DefaultFormation"/> (the five squares every encounter used before), and from the sixth on the search
/// grows from <see cref="DefaultAnchor"/> on the enemy side, keeping one free square around each creature while
/// there is room, then packing. Deterministic (no dice): nearer squares first by Chebyshev, then straight-line
/// distance; ties go to larger x (away from the party's side), then smaller y. This is a layout, not the DMG's
/// spotting (encounter) distance, which the game does not model. Pure: the caller says which squares are free, so the
/// suites test it without a scene.
/// </summary>
public static class EncounterSpawnPlacement
{
    /// <summary>The first five enemy squares (the layout every encounter used before ENC-001 was fixed).</summary>
    public static readonly Vector2Int[] DefaultFormation =
    {
        new Vector2Int(16, 6),
        new Vector2Int(14, 10),
        new Vector2Int(16, 14),
        new Vector2Int(13, 8),
        new Vector2Int(13, 12),
    };

    /// <summary>Where the search for the sixth and later creature without a preferred square starts (the enemy side's middle).</summary>
    public static readonly Vector2Int DefaultAnchor = new Vector2Int(15, 10);

    /// <summary>
    /// True when the <paramref name="sizeSquares"/> x <paramref name="sizeSquares"/> footprint based at
    /// <paramref name="basePos"/> lies on the <paramref name="width"/> x <paramref name="height"/> grid and
    /// <paramref name="isFree"/> accepts every square of it.
    /// </summary>
    public static bool Fits(Vector2Int basePos, int sizeSquares, int width, int height, Func<Vector2Int, bool> isFree)
    {
        int size = Mathf.Max(1, sizeSquares);
        if (basePos.x < 0 || basePos.y < 0 || basePos.x + size > width || basePos.y + size > height)
            return false;

        for (int dx = 0; dx < size; dx++)
            for (int dy = 0; dy < size; dy++)
                if (isFree != null && !isFree(new Vector2Int(basePos.x + dx, basePos.y + dy)))
                    return false;
        return true;
    }

    /// <summary>
    /// The base square for one creature. <paramref name="preferred"/> is the preset or custom square (null for none);
    /// <paramref name="formationIndex"/> is how many creatures without a preferred square were placed before this one
    /// (used only when <paramref name="preferred"/> is null). False only when the footprint fits nowhere on the grid.
    /// </summary>
    public static bool TryFindSpawnSquare(Vector2Int? preferred, int formationIndex, int sizeSquares, int width, int height,
        Func<Vector2Int, bool> isFree, out Vector2Int square)
    {
        int size = Mathf.Max(1, sizeSquares);

        Vector2Int? wanted = preferred;
        if (!wanted.HasValue && formationIndex >= 0 && formationIndex < DefaultFormation.Length)
            wanted = DefaultFormation[formationIndex];

        if (wanted.HasValue && Fits(wanted.Value, size, width, height, isFree))
        {
            square = wanted.Value;
            return true;
        }

        // A preferred or formation square that does not fit: the nearest square that does, packed (a preset's
        // layout stays as close as it can). No preferred square past the formation: spread out from the anchor.
        bool spread = !wanted.HasValue;
        Vector2Int anchor = wanted ?? DefaultAnchor;
        List<Vector2Int> order = CandidatesByDistance(anchor, size, width, height);

        if (spread)
        {
            foreach (Vector2Int c in order)
            {
                if (Fits(c, size, width, height, isFree) && RingFree(c, size, width, height, isFree))
                {
                    square = c;
                    return true;
                }
            }
        }

        foreach (Vector2Int c in order)
        {
            if (Fits(c, size, width, height, isFree))
            {
                square = c;
                return true;
            }
        }

        square = anchor;
        return false;
    }

    /// <summary>
    /// Places a whole group on an empty grid apart from <paramref name="blocked"/> squares, in list order, as
    /// SetupEnemyEncounter does (each placed creature blocks its footprint for the next). A null entry of
    /// <paramref name="preferred"/> (or a shorter list) means no preferred square. Unplaceable creatures get null.
    /// </summary>
    public static Vector2Int?[] PlaceGroup(IList<int> sizes, IList<Vector2Int?> preferred, int width, int height,
        ICollection<Vector2Int> blocked)
    {
        var taken = new HashSet<Vector2Int>(blocked ?? new List<Vector2Int>());
        var result = new Vector2Int?[sizes.Count];
        int formationIndex = 0;
        for (int i = 0; i < sizes.Count; i++)
        {
            Vector2Int? pref = preferred != null && i < preferred.Count ? preferred[i] : null;
            int fi = pref.HasValue ? -1 : formationIndex++;
            if (!TryFindSpawnSquare(pref, fi, sizes[i], width, height, p => !taken.Contains(p), out Vector2Int sq))
                continue;
            result[i] = sq;
            int size = Mathf.Max(1, sizes[i]);
            for (int dx = 0; dx < size; dx++)
                for (int dy = 0; dy < size; dy++)
                    taken.Add(new Vector2Int(sq.x + dx, sq.y + dy));
        }
        return result;
    }

    /// <summary>Every base square whose footprint fits the grid bounds, nearest to <paramref name="anchor"/> first.</summary>
    private static List<Vector2Int> CandidatesByDistance(Vector2Int anchor, int size, int width, int height)
    {
        var list = new List<Vector2Int>();
        for (int x = 0; x + size <= width; x++)
            for (int y = 0; y + size <= height; y++)
                list.Add(new Vector2Int(x, y));

        list.Sort((a, b) =>
        {
            int c = Distance(a, anchor).CompareTo(Distance(b, anchor));
            if (c != 0) return c;
            c = DistanceSq(a, anchor).CompareTo(DistanceSq(b, anchor));
            if (c != 0) return c;
            c = b.x.CompareTo(a.x);
            if (c != 0) return c;
            return a.y.CompareTo(b.y);
        });
        return list;
    }

    /// <summary>Chebyshev distance in squares between two base squares.</summary>
    private static int Distance(Vector2Int a, Vector2Int b) => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

    private static int DistanceSq(Vector2Int a, Vector2Int b)
    {
        int dx = a.x - b.x, dy = a.y - b.y;
        return dx * dx + dy * dy;
    }

    /// <summary>True when the one-square ring around the footprint holds no taken square (off-grid squares count as free).</summary>
    private static bool RingFree(Vector2Int basePos, int size, int width, int height, Func<Vector2Int, bool> isFree)
    {
        if (isFree == null)
            return true;
        for (int dx = -1; dx <= size; dx++)
        {
            for (int dy = -1; dy <= size; dy++)
            {
                bool inside = dx >= 0 && dx < size && dy >= 0 && dy < size;
                if (inside)
                    continue;
                var p = new Vector2Int(basePos.x + dx, basePos.y + dy);
                if (p.x < 0 || p.y < 0 || p.x >= width || p.y >= height)
                    continue;
                if (!isFree(p))
                    return false;
            }
        }
        return true;
    }
}
