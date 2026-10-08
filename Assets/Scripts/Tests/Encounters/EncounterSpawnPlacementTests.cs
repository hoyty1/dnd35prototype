using System.Collections.Generic;
using UnityEngine;

namespace Tests.Encounters
{
/// <summary>
/// ENC-001: EncounterSpawnPlacement, the square every encounter creature spawns on. Before the fix the sixth and later
/// creature of a non-preset encounter spawned at (15 + i, 10), off the 20x20 grid, and the placement ignored creature
/// size. These tests pin that every creature of a large or mixed-size group lands with its whole footprint (PHB p.149,
/// Table 8-4) on the grid without overlapping another, that a preferred square is kept when it fits and otherwise
/// gives way to the nearest one that does, and that the first five creatures keep the old five squares. Pure (no
/// scene, no dice); runs in edit or Play mode. The spawn path itself is covered by the scenarios
/// rules/large-encounter-goblins and rules/large-encounter-sizes (docs/TESTING.md 3.4).
/// </summary>
public static class EncounterSpawnPlacementTests
{
    private static int _passed;
    private static int _failed;

    private const int W = 20;
    private const int H = 20;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("========== ENCOUNTER SPAWN PLACEMENT TESTS (ENC-001) ==========");

        TestTwentyMediumAllOnGrid();
        TestFirstFiveKeepDefaultSquares();
        TestSpreadKeepsOneSquareApart();
        TestMixedSizesFitWholeFootprints();
        TestPreferredSquareKeptWhenFree();
        TestPreferredSquareTakenGivesWayToNearest();
        TestFootprintOffTheEdgeIsMovedOnto();
        TestOffGridPreferredIsMovedOnto();
        TestPartySquaresAreAvoided();
        TestFullGridReportsNoRoom();
        TestFitsChecksBoundsAndFreeSquares();
        TestDeterministic();

        Debug.Log($"========== RESULTS: {_passed} passed, {_failed} failed ==========");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  PASS: {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  FAIL: {testName} {detail}");
        }
    }

    private static List<int> Sizes(int count, int size)
    {
        var list = new List<int>();
        for (int i = 0; i < count; i++)
            list.Add(size);
        return list;
    }

    /// <summary>Null when every placed footprint is on the grid, off the blocked squares and disjoint, else the problem.</summary>
    private static string CheckLayout(IList<int> sizes, Vector2Int?[] placed, ICollection<Vector2Int> blocked)
    {
        var taken = new HashSet<Vector2Int>(blocked ?? new List<Vector2Int>());
        for (int i = 0; i < placed.Length; i++)
        {
            if (!placed[i].HasValue)
                return "creature " + i + " was not placed";
            Vector2Int p = placed[i].Value;
            int s = Mathf.Max(1, sizes[i]);
            for (int dx = 0; dx < s; dx++)
            {
                for (int dy = 0; dy < s; dy++)
                {
                    var sq = new Vector2Int(p.x + dx, p.y + dy);
                    if (sq.x < 0 || sq.y < 0 || sq.x >= W || sq.y >= H)
                        return "creature " + i + " at " + p + " (" + s + "x" + s + ") leaves the grid at " + sq;
                    if (!taken.Add(sq))
                        return "creature " + i + " at " + p + " overlaps at " + sq;
                }
            }
        }
        return null;
    }

    private static void TestTwentyMediumAllOnGrid()
    {
        List<int> sizes = Sizes(20, 1);
        Vector2Int?[] placed = EncounterSpawnPlacement.PlaceGroup(sizes, null, W, H, null);
        string problem = CheckLayout(sizes, placed, null);
        Assert(problem == null, "Twenty Medium creatures all land on free grid squares", problem ?? "");

        bool enemySide = true;
        for (int i = 0; i < placed.Length; i++)
            if (placed[i].HasValue && placed[i].Value.x < W / 2)
                enemySide = false;
        Assert(enemySide, "Twenty Medium creatures stay on the enemy half (x >= 10)");
    }

    private static void TestFirstFiveKeepDefaultSquares()
    {
        Vector2Int?[] placed = EncounterSpawnPlacement.PlaceGroup(Sizes(7, 1), null, W, H, null);
        bool same = true;
        for (int i = 0; i < 5; i++)
            if (placed[i] != EncounterSpawnPlacement.DefaultFormation[i])
                same = false;
        Assert(same, "The first five creatures take the five default squares");
        Assert(EncounterSpawnPlacement.DefaultFormation.Length == 5
               && EncounterSpawnPlacement.DefaultFormation[0] == new Vector2Int(16, 6)
               && EncounterSpawnPlacement.DefaultFormation[4] == new Vector2Int(13, 12),
            "The default squares are the five used before ENC-001: (16,6), (14,10), (16,14), (13,8), (13,12)");
    }

    private static void TestSpreadKeepsOneSquareApart()
    {
        // Creatures past the five default squares keep a free square around them while there is room.
        Vector2Int?[] placed = EncounterSpawnPlacement.PlaceGroup(Sizes(12, 1), null, W, H, null);
        bool apart = true;
        string detail = "";
        for (int i = 5; i < placed.Length && apart; i++)
        {
            for (int j = 0; j < placed.Length; j++)
            {
                if (i == j || !placed[i].HasValue || !placed[j].HasValue)
                    continue;
                Vector2Int a = placed[i].Value, b = placed[j].Value;
                if (Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y)) <= 1)
                {
                    apart = false;
                    detail = "creature " + i + " at " + a + " touches creature " + j + " at " + b;
                    break;
                }
            }
        }
        Assert(apart, "Creatures six to twelve each keep one free square from every other", detail);
    }

    private static void TestMixedSizesFitWholeFootprints()
    {
        // Goblin, Huge snake, three Large ogres, ten goblins, a Large ogre, a Huge snake, a Gargantuan and a Colossal creature.
        var sizes = new List<int> { 1, 3, 2, 2, 2 };
        sizes.AddRange(Sizes(10, 1));
        sizes.Add(2);
        sizes.Add(3);
        sizes.Add(4);
        sizes.Add(6);
        Vector2Int?[] placed = EncounterSpawnPlacement.PlaceGroup(sizes, null, W, H, null);
        string problem = CheckLayout(sizes, placed, null);
        Assert(problem == null, "A mixed group of Medium to Colossal creatures lands with whole footprints, no overlap", problem ?? "");
        Assert(placed[1] == new Vector2Int(14, 10), "The Huge snake keeps the second default square (14,10)", "at " + placed[1]);
        Assert(placed[4].HasValue && placed[4] != new Vector2Int(13, 12),
            "The third ogre's default square (13,12) would overlap the snake, so it gives way", "at " + placed[4]);
    }

    private static void TestPreferredSquareKeptWhenFree()
    {
        bool ok = EncounterSpawnPlacement.TryFindSpawnSquare(new Vector2Int(5, 5), -1, 2, W, H, p => true, out Vector2Int sq);
        Assert(ok && sq == new Vector2Int(5, 5), "A preferred square whose footprint fits is kept", "got " + sq);
    }

    private static void TestPreferredSquareTakenGivesWayToNearest()
    {
        var taken = new HashSet<Vector2Int> { new Vector2Int(12, 5) };
        bool ok = EncounterSpawnPlacement.TryFindSpawnSquare(new Vector2Int(12, 5), -1, 1, W, H, p => !taken.Contains(p), out Vector2Int sq);
        int dist = Mathf.Max(Mathf.Abs(sq.x - 12), Mathf.Abs(sq.y - 5));
        Assert(ok && dist == 1 && sq != new Vector2Int(12, 5), "A taken preferred square gives way to an adjacent free one", "got " + sq);
        Assert(sq == new Vector2Int(13, 5), "Among equally near squares the one away from the party (larger x) wins", "got " + sq);
    }

    private static void TestFootprintOffTheEdgeIsMovedOnto()
    {
        // A Huge (3x3) creature asked for (18,18) would cover x and y 18-20: moved to (17,17), the nearest that fits.
        bool ok = EncounterSpawnPlacement.TryFindSpawnSquare(new Vector2Int(18, 18), -1, 3, W, H, p => true, out Vector2Int sq);
        Assert(ok && sq == new Vector2Int(17, 17), "A footprint that would leave the grid is moved back onto it", "got " + sq);
    }

    private static void TestOffGridPreferredIsMovedOnto()
    {
        // The old fallback square of the 8th creature, (15 + 7, 10), is off the grid.
        bool ok = EncounterSpawnPlacement.TryFindSpawnSquare(new Vector2Int(22, 10), -1, 1, W, H, p => true, out Vector2Int sq);
        Assert(ok && sq == new Vector2Int(19, 10), "An off-grid preferred square is moved to the nearest grid square", "got " + sq);
    }

    private static void TestPartySquaresAreAvoided()
    {
        // Blocked squares (the party, or creatures already on the grid) are never used.
        var blocked = new List<Vector2Int>();
        for (int x = 12; x <= 18; x++)
            for (int y = 4; y <= 16; y++)
                blocked.Add(new Vector2Int(x, y));
        List<int> sizes = Sizes(20, 1);
        Vector2Int?[] placed = EncounterSpawnPlacement.PlaceGroup(sizes, null, W, H, blocked);
        string problem = CheckLayout(sizes, placed, blocked);
        Assert(problem == null, "Twenty creatures avoid a block of 91 held squares on the enemy side", problem ?? "");
    }

    private static void TestFullGridReportsNoRoom()
    {
        bool ok = EncounterSpawnPlacement.TryFindSpawnSquare(null, 0, 1, W, H, p => false, out Vector2Int _);
        Assert(!ok, "A full grid reports no room instead of an illegal square");

        bool colossal = EncounterSpawnPlacement.TryFindSpawnSquare(null, 7, 6, 5, 5, p => true, out Vector2Int __);
        Assert(!colossal, "A 6x6 footprint on a 5x5 grid reports no room");
    }

    private static void TestFitsChecksBoundsAndFreeSquares()
    {
        Assert(EncounterSpawnPlacement.Fits(new Vector2Int(18, 18), 2, W, H, p => true), "Fits: a 2x2 at (18,18) fits a 20x20 grid");
        Assert(!EncounterSpawnPlacement.Fits(new Vector2Int(19, 18), 2, W, H, p => true), "Fits: a 2x2 at (19,18) leaves the grid");
        Assert(!EncounterSpawnPlacement.Fits(new Vector2Int(-1, 0), 1, W, H, p => true), "Fits: a negative square is off the grid");
        Assert(!EncounterSpawnPlacement.Fits(new Vector2Int(4, 4), 2, W, H, p => p != new Vector2Int(5, 5)),
            "Fits: a 2x2 whose far corner is held does not fit");
    }

    private static void TestDeterministic()
    {
        var sizes = new List<int> { 1, 2, 1, 3, 1, 1, 2, 1, 1, 1, 1, 1 };
        Vector2Int?[] a = EncounterSpawnPlacement.PlaceGroup(sizes, null, W, H, null);
        Vector2Int?[] b = EncounterSpawnPlacement.PlaceGroup(sizes, null, W, H, null);
        bool same = true;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i])
                same = false;
        Assert(same, "The same group gets the same squares every time (no dice)");
    }
}
}
