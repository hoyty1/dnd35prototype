#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DND35.AI;
using DND35.AI.Profiles;
using DND35e.Identifiers;
using Tests.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.Magic
{
/// <summary>
/// Batch 11 step 24 (docs/designs/cast_core_batch11.md, raw-area-origin-intersections; decision D5, PHB p.175-176):
/// every area's point of origin is a grid intersection, and a square is in the area when its far corner is within the
/// radius counted from the origin like movement.
/// <list type="bullet">
/// <item>Golden footprints: bursts of 4, 12, 24, 44, 96 and 172 squares at 5 to 40 ft., the 20-ft. burst's PHB p.175
/// pattern (rows of 2, 6, 6, 8, 8, 6, 6, 2), the far-edge rule; cones: a diagonal cone is a quadrant of the burst, a cone
/// along a grid line is symmetric with the same count at 15, 30 and 60 ft. (delegated RAW reading 2026-10-14); a 15-ft.
/// cone from each corner of the caster's square in each of the 8 directions (6 squares each; 20 of the 32 clear of the
/// caster); a 60-ft. line from a corner (12 squares straight, 8 diagonally).</item>
/// <item>Origins: the creature-centred origins (the four corners of a Medium creature's square, a Large creature's centre,
/// the corners of a Huge creature's central square; its own space always inside), the edge intersections a cone or line
/// starts from, the range to a point of origin, the candidate aims the AI and the PC share.</item>
/// <item>Line of effect from an intersection, through the square that faces the target; an origin on a wall's face
/// reaches only its own side; the caster needs line of effect to a burst's origin (PHB p.176), so the AI's candidates
/// and the PC's aim leave out an origin behind a wall (batch 11 step 24 review).</item>
/// <item>Play mode: the AI's area evaluation picks the corner that spares an ally (SpellcasterAIProfile.EvaluateAOECast),
/// a breath picks the start that covers foes and spares its ally (AoESystem.BestDirectedAimAt), the PC aim rule
/// (GameManager.TryBuildUiAreaAim) snaps to the nearest intersection and refuses an origin behind a wall, the cast core
/// refuses an origin out of range or out of the caster's line of effect (a Grease area's too; a Grease square or a
/// one-square effect beyond the spell's range, batch 11 step 25 review), Prayer's corner takes in
/// the most creatures and a scored corner (Invisibility Purge's) follows its score, ally sparing counts every square of a
/// Large ally, and a lasting area made by a cast covers exactly the cast's squares (SPL-029).</item>
/// </list>
/// </summary>
public static class AreaOriginTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== AREA ORIGIN TESTS (batch 11 step 24, decision D5, PHB p.175) ======");

        TestHelpers.EnsureCoreDatabasesInitialized();
        SpellDatabase.Init();

        TestBurstCounts();
        TestBurstPattern20Ft();
        TestFarEdgeRule();
        TestConeCounts();
        TestCone15FtEachCornerAndDirection();
        TestLine60Ft();
        TestCreatureCentredOrigins();
        TestEdgeIntersectionsAndRange();
        TestCandidateAims();
        TestLineOfEffectFromIntersection();
        TestLineOfEffectOnWallFace();
        TestLineOfEffectToSpace();
        TestPlayModeAims();

        Debug.Log($"====== Area Origin Results: {_passed} passed, {_failed} failed ======");
    }

    // ── Golden footprints ─────────────────────────────────────────────────────────────────────────────────

    private static void TestBurstCounts()
    {
        var origin = new GridIntersection(0, 0);
        int[] radii = { 1, 2, 3, 4, 6, 8 };
        int[] counts = { 4, 12, 24, 44, 96, 172 };
        for (int i = 0; i < radii.Length; i++)
        {
            int got = AoESystem.GetBurstCells(origin, radii[i], null).Count;
            Assert(got == counts[i], $"a {radii[i] * 5}-ft. burst covers {counts[i]} squares (PHB p.175, decision D5)", "got " + got);
        }
    }

    private static void TestBurstPattern20Ft()
    {
        // The 20-ft.-radius burst around intersection +(0,0): rows y = 3..-4 of widths 2, 6, 6, 8, 8, 6, 6, 2, each centred
        // on the grid line x = 0 (squares -w/2 .. w/2 - 1).
        HashSet<Vector2Int> burst = AoESystem.GetBurstCells(new GridIntersection(0, 0), 4, null);
        int[] widths = { 2, 6, 6, 8, 8, 6, 6, 2 };
        bool ok = burst.Count == 44;
        for (int r = 0; r < widths.Length && ok; r++)
        {
            int y = 3 - r;
            int half = widths[r] / 2;
            for (int x = -5; x <= 4; x++)
            {
                bool expected = x >= -half && x <= half - 1;
                if (burst.Contains(new Vector2Int(x, y)) != expected)
                {
                    ok = false;
                    break;
                }
            }
        }
        Assert(ok, "the 20-ft. burst is the PHB p.175 pattern: rows of 2, 6, 6, 8, 8, 6, 6, 2 squares around the intersection");

        bool symmetric = true;
        foreach (Vector2Int c in burst)
            symmetric &= burst.Contains(new Vector2Int(-1 - c.x, c.y)) && burst.Contains(new Vector2Int(c.x, -1 - c.y));
        Assert(symmetric, "a burst has four identical quadrants about its intersection");
    }

    private static void TestFarEdgeRule()
    {
        var origin = new GridIntersection(0, 0);
        Assert(AoESystem.IsSquareWithinRadius(origin, 1, new Vector2Int(0, 0)) && AoESystem.IsSquareWithinRadius(origin, 1, new Vector2Int(-1, -1)),
            "the four squares around the origin are in a 5-ft. area (their far corners are 5 ft. away)");
        Assert(!AoESystem.IsSquareWithinRadius(origin, 1, new Vector2Int(1, 0)),
            "a square the area only touches at its near edge is not in it (PHB p.175)");
        Assert(AoESystem.CountToFarCorner(origin, new Vector2Int(2, 2)) == 4 && AoESystem.CountToFarCorner(origin, new Vector2Int(-3, 0)) == 3,
            "the count to a square's far corner alternates diagonals 1 and 2 (3 diagonals = 4, PHB p.147)");
    }

    private static void TestConeCounts()
    {
        var start = new GridIntersection(0, 0);
        int[] lengths = { 2, 3, 4, 6, 8, 12 };
        int[] diagonal = { 3, 6, 11, 24, 43, 96 };
        int[] gridLine = { 4, 6, 12, 24, 44, 96 };
        for (int i = 0; i < lengths.Length; i++)
        {
            int ne = AoESystem.GetConeCells(start, 1, lengths[i], null).Count;
            int e = AoESystem.GetConeCells(start, 2, lengths[i], null).Count;
            int s = AoESystem.GetConeCells(start, 4, lengths[i], null).Count;
            Assert(ne == diagonal[i] && e == gridLine[i] && s == gridLine[i],
                $"a {lengths[i] * 5}-ft. cone: {diagonal[i]} squares diagonally, {gridLine[i]} along a grid line",
                $"NE {ne}, E {e}, S {s}");
        }

        // A diagonal cone is one quadrant of the burst.
        HashSet<Vector2Int> burst = AoESystem.GetBurstCells(start, 6, null);
        HashSet<Vector2Int> quadrant = new HashSet<Vector2Int>();
        foreach (Vector2Int c in burst)
            if (c.x >= 0 && c.y < 0)
                quadrant.Add(c);
        Assert(AoESystem.GetConeCells(start, 3, 6, null).SetEquals(quadrant), "a 30-ft. cone to the south-east is the burst's south-east quadrant");

        // A cone along a grid line is symmetric about the grid line through its start.
        HashSet<Vector2Int> north = AoESystem.GetConeCells(start, 0, 12, null);
        bool symmetric = true;
        foreach (Vector2Int c in north)
            symmetric &= north.Contains(new Vector2Int(-1 - c.x, c.y));
        Assert(symmetric, "a 60-ft. cone along a grid line is symmetric about that line (decision D5 (4))");

        HashSet<Vector2Int> east15 = AoESystem.GetConeCells(start, 2, 3, null);
        var expected = new HashSet<Vector2Int>
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0),
            new Vector2Int(0, -1), new Vector2Int(1, -1), new Vector2Int(2, -1)
        };
        Assert(east15.SetEquals(expected), "a 15-ft. cone east of +(0,0) is the two rows beside the grid line, 3 squares long (delegated reading 2026-10-14)");
    }

    private static void TestCone15FtEachCornerAndDirection()
    {
        var caster = new Vector2Int(5, 5);
        List<GridIntersection> corners = AoESystem.GetSpaceEdgeIntersections(caster, 1);
        int all6 = 0;
        int clear = 0;
        int clearDiagonal = 0;
        foreach (GridIntersection corner in corners)
        {
            for (int dir = 0; dir < 8; dir++)
            {
                HashSet<Vector2Int> cells = AoESystem.GetConeCells(corner, dir, 3, null);
                if (cells.Count == 6)
                    all6++;
                if (AoESystem.IsClearOfSpace(cells, caster, 1))
                {
                    clear++;
                    if (AoESystem.IsDiagonalDirection(dir))
                        clearDiagonal++;
                }
            }
        }
        Assert(corners.Count == 4 && all6 == 32, "a 15-ft. cone from each corner of the caster's square in each of 8 directions covers 6 squares", $"corners {corners.Count}, six {all6}");
        Assert(clear == 20 && clearDiagonal == 12,
            "of those, 20 shoot away from the caster: each diagonal from 3 corners, each grid-line direction from the 2 corners on its side",
            $"clear {clear}, diagonal {clearDiagonal}");

        HashSet<Vector2Int> ne = AoESystem.GetConeCells(new GridIntersection(6, 6), 1, 3, null);
        var expectedNe = new HashSet<Vector2Int>
        {
            new Vector2Int(6, 6), new Vector2Int(7, 6), new Vector2Int(8, 6), new Vector2Int(6, 7), new Vector2Int(7, 7), new Vector2Int(6, 8)
        };
        Assert(ne.SetEquals(expectedNe), "a 15-ft. cone north-east from the caster's north-east corner is the 1-2-3 quarter circle");

        HashSet<Vector2Int> eastFromNe = AoESystem.GetConeCells(new GridIntersection(6, 6), 2, 3, null);
        var expectedE = new HashSet<Vector2Int>
        {
            new Vector2Int(6, 6), new Vector2Int(7, 6), new Vector2Int(8, 6), new Vector2Int(6, 5), new Vector2Int(7, 5), new Vector2Int(8, 5)
        };
        Assert(eastFromNe.SetEquals(expectedE), "a 15-ft. cone east from the north-east corner covers the caster's row and the row above, 3 squares long");

        bool aimed = AoESystem.TryAimCone(caster, 1, new Vector2(9f, 5.2f), 3, null, out GridIntersection start, out int aimDir, out HashSet<Vector2Int> aimCells);
        Assert(aimed && start == new GridIntersection(6, 6) && aimDir == 2 && aimCells.SetEquals(expectedE),
            "TryAimCone starts at the corner nearest the cursor and points east", $"aimed={aimed} start={start} dir={aimDir}");
        Assert(!AoESystem.TryAimCone(caster, 1, new Vector2(5.1f, 4.8f), 3, null, out _, out _, out _),
            "no cone is aimed at a point inside the caster's square");
    }

    private static void TestLine60Ft()
    {
        var caster = new Vector2Int(5, 5);
        bool aimed = AoESystem.TryAimLine(caster, 1, new Vector2(17f, 5f), 12, null, out GridIntersection start, out HashSet<Vector2Int> cells);
        bool row5 = true;
        foreach (Vector2Int c in cells)
            row5 &= c.y == 5 && c.x >= 6 && c.x <= 17;
        Assert(aimed && start == new GridIntersection(6, 6) && cells.Count == 12 && row5,
            "a 60-ft. line east from the caster's corner covers 12 squares", $"aimed={aimed} start={start} count={cells.Count}");

        HashSet<Vector2Int> diagonal = AoESystem.GetLineCells(new GridIntersection(6, 6), new Vector2(13f, 13f), 12, null);
        bool onDiagonal = true;
        foreach (Vector2Int c in diagonal)
            onDiagonal &= c.x == c.y && c.x >= 6 && c.x <= 13;
        Assert(diagonal.Count == 8 && onDiagonal, "a 60-ft. diagonal line covers 8 squares (diagonals alternate 1 and 2)", "count " + diagonal.Count);

        HashSet<Vector2Int> far = AoESystem.GetLineCells(new GridIntersection(6, 6), new Vector2(40f, 5f), 12, null);
        Assert(far.Count == 12, "a line aimed past its length still runs its full 60 ft. (PHB p.176)", "count " + far.Count);
        Assert(!far.Contains(caster), "a line never covers the caster's square");
    }

    // ── Origins ───────────────────────────────────────────────────────────────────────────────────────────

    private static void TestCreatureCentredOrigins()
    {
        List<GridIntersection> medium = AoESystem.GetCreatureCentredOrigins(new Vector2Int(5, 5), 1);
        Assert(medium.Count == 4 && medium[0] == new GridIntersection(6, 6) && medium.Contains(new GridIntersection(5, 5))
               && medium.Contains(new GridIntersection(5, 6)) && medium.Contains(new GridIntersection(6, 5)),
            "a Small or Medium creature's area originates at a corner of its square, the north-east one by default (D5 (3))");

        List<GridIntersection> large = AoESystem.GetCreatureCentredOrigins(new Vector2Int(5, 5), 2);
        Assert(large.Count == 1 && large[0] == new GridIntersection(6, 6), "a Large creature's area originates at its centre intersection");

        List<GridIntersection> huge = AoESystem.GetCreatureCentredOrigins(new Vector2Int(5, 5), 3);
        Assert(huge.Count == 4 && huge[0] == new GridIntersection(7, 7) && huge.Contains(new GridIntersection(6, 6)),
            "a Huge creature's area originates at a corner of its central square");

        HashSet<Vector2Int> hugeBurst = AoESystem.GetCreatureCentredBurstCells(new Vector2Int(5, 5), 3, huge[0], 2, null);
        bool spaceInside = true;
        for (int x = 5; x <= 7; x++)
            for (int y = 5; y <= 7; y++)
                spaceInside &= hugeBurst.Contains(new Vector2Int(x, y));
        Assert(spaceInside && !AoESystem.GetBurstCells(huge[0], 2, null).Contains(new Vector2Int(5, 5)),
            "the creature's own space is always inside an area centred on it, even a Huge creature's far corner in a 10-ft. burst");
    }

    private static void TestEdgeIntersectionsAndRange()
    {
        Assert(AoESystem.GetSpaceEdgeIntersections(new Vector2Int(0, 0), 1).Count == 4
               && AoESystem.GetSpaceEdgeIntersections(new Vector2Int(0, 0), 2).Count == 8
               && AoESystem.GetSpaceEdgeIntersections(new Vector2Int(0, 0), 3).Count == 12,
            "a cone or line starts at a corner of a Medium creature's square, or an edge intersection of a larger space (4, 8, 12)");

        Assert(AoESystem.CountFromSpace(new Vector2Int(0, 0), 1, new GridIntersection(13, 1)) == 12
               && AoESystem.IsOriginWithinRange(new Vector2Int(0, 0), 1, new GridIntersection(13, 1), 12)
               && !AoESystem.IsOriginWithinRange(new Vector2Int(0, 0), 1, new GridIntersection(14, 1), 12),
            "a point of origin is in range when the count from the caster's space to it is at most the range");
        Assert(AoESystem.CountFromSpace(new Vector2Int(0, 0), 1, new GridIntersection(13, 13)) == 18,
            "the range count to an intersection alternates diagonals (12 diagonals = 18)");

        Assert(GridIntersection.NearestToGridPoint(new Vector2(10f, 10f)) == new GridIntersection(11, 11)
               && GridIntersection.NearestToGridPoint(new Vector2(9.7f, 10.2f)) == new GridIntersection(10, 11),
            "the cursor snaps to the nearest intersection; a square's centre gives its north-east corner");
    }

    private static void TestCandidateAims()
    {
        List<AreaAim> bursts = AoESystem.GetCandidateAimsAt(AoEShape.Burst, 4, new Vector2Int(0, 0), 1, new Vector2Int(10, 10), 1, 20, null);
        bool allHit = bursts.Count == 4;
        foreach (AreaAim a in bursts)
            allHit &= a.Cells.Count == 44 && a.Cells.Contains(new Vector2Int(10, 10));
        Assert(allHit, "the AI's burst aims at a creature are the four corners of its square, each 44 squares covering it", "count " + bursts.Count);

        List<AreaAim> inRange = AoESystem.GetCandidateAimsAt(AoEShape.Burst, 4, new Vector2Int(0, 0), 1, new Vector2Int(10, 10), 1, 13, null);
        Assert(inRange.Count == 1 && inRange[0].Origin == new GridIntersection(10, 10),
            "only the corners within the spell's range are candidates", "count " + inRange.Count);

        List<AreaAim> cones = AoESystem.GetCandidateAimsAt(AoEShape.Cone, 3, new Vector2Int(5, 5), 1, new Vector2Int(8, 5), 1, -1, null);
        bool conesOk = cones.Count == 2;
        foreach (AreaAim a in cones)
            conesOk &= a.DirIndex == 2 && a.Cells.Contains(new Vector2Int(8, 5)) && !a.Cells.Contains(new Vector2Int(5, 5));
        Assert(conesOk, "the cone aims at a creature are east from the caster's two east corners (the others would cover the caster)", "count " + cones.Count);
    }

    /// <summary>A test blocker: blocks every line that crosses between columns 10 and 11.</summary>
    private sealed class ColumnBlocker : ILineOfEffectBlocker
    {
        public bool BlocksLineOfEffect(Vector2Int from, Vector2Int to)
            => (from.x <= 10 && to.x >= 11) || (from.x >= 11 && to.x <= 10);

        public HashSet<Vector2Int> GetBlockerCells() => new HashSet<Vector2Int>();
    }

    private static void TestLineOfEffectFromIntersection()
    {
        var blocker = new ColumnBlocker();
        LineOfEffectService.Register(blocker);
        try
        {
            var origin = new GridIntersection(8, 8);
            HashSet<Vector2Int> cells = AoESystem.GetBurstCells(origin, 4, null);
            HashSet<Vector2Int> removed = AoESystem.FilterCellsByLineOfEffect(cells, origin);
            bool onlyColumn11 = removed.Count == 2;
            foreach (Vector2Int c in removed)
                onlyColumn11 &= c.x == 11;
            Assert(onlyColumn11 && cells.Count == 42,
                "line of effect is traced from the area's intersection: the squares past the wall are dropped (PHB p.176)",
                $"removed {removed.Count}, kept {cells.Count}");

            Assert(origin.SquareFacing(new Vector2Int(11, 7)) == new Vector2Int(8, 7)
                   && origin.SquareFacing(new Vector2Int(2, 9)) == new Vector2Int(7, 8),
                "the line from an intersection enters first the square of its four that faces the target");
            Assert(!AoESystem.IsLineOfEffectBlocked(new GridIntersection(11, 8), new Vector2Int(11, 8)),
                "an origin on the wall's far side reaches its own squares");
        }
        finally
        {
            LineOfEffectService.Unregister(blocker);
        }
    }

    /// <summary>A test wall: the squares of row <c>Y</c> from x = 0 to 29, blocking as Wall of Ice and Wall of Force do (the squares a line crosses, its two ends left out).</summary>
    private sealed class RowWall : ILineOfEffectBlocker
    {
        private readonly HashSet<Vector2Int> _cells = new HashSet<Vector2Int>();

        public RowWall(int y)
        {
            for (int x = 0; x < 30; x++)
                _cells.Add(new Vector2Int(x, y));
        }

        public bool BlocksLineOfEffect(Vector2Int from, Vector2Int to) => WindWallAreaEffect.LineSegmentCrossesAnyCellPublic(from, to, _cells);

        public HashSet<Vector2Int> GetBlockerCells() => new HashSet<Vector2Int>(_cells);
    }

    /// <summary>
    /// Batch 11 step 27 (PHB p.176: line of effect to any space in which an effect is created; delegated RAW reading
    /// 2026-10-14): a summoned creature's space is reached when any of its squares is (AoESystem.HasLineOfEffectToSpace),
    /// never through a wall or on a wall's own square.
    /// </summary>
    private static void TestLineOfEffectToSpace()
    {
        var wall = new RowWall(5);
        LineOfEffectService.Register(wall);
        try
        {
            var caster = new Vector2Int(5, 3);
            Assert(!AoESystem.HasLineOfEffectToSpace(caster, 1, new Vector2Int(5, 7), 1),
                "no line of effect to a square beyond a wall (a summon cannot appear there, PHB p.176)");
            Assert(AoESystem.HasLineOfEffectToSpace(caster, 1, new Vector2Int(8, 4), 1),
                "line of effect to a square on the caster's side of the wall");
            Assert(!AoESystem.HasLineOfEffectToSpace(caster, 1, new Vector2Int(5, 5), 1),
                "a wall's own square is not reached");
            Assert(AoESystem.HasLineOfEffectToSpace(caster, 1, new Vector2Int(5, 4), 2),
                "a Large space across the wall's row is reached through its near squares");
        }
        finally
        {
            LineOfEffectService.Unregister(wall);
        }
    }

    private static void TestLineOfEffectOnWallFace()
    {
        var wall = new RowWall(5);
        LineOfEffectService.Register(wall);
        try
        {
            var south = new GridIntersection(5, 5); // on the wall's south face
            var north = new GridIntersection(5, 6); // on its north face
            Assert(AoESystem.IsLineOfEffectBlocked(south, new Vector2Int(5, 7)) && !AoESystem.IsLineOfEffectBlocked(south, new Vector2Int(5, 3))
                   && !AoESystem.IsLineOfEffectBlocked(south, new Vector2Int(8, 4)),
                "an origin on a wall's south face reaches the squares south of it and none beyond it (PHB p.175: total cover from the origin)");
            Assert(!AoESystem.IsLineOfEffectBlocked(north, new Vector2Int(5, 7)) && AoESystem.IsLineOfEffectBlocked(north, new Vector2Int(5, 3)),
                "an origin on a wall's north face reaches only the north side");

            HashSet<Vector2Int> burst = AoESystem.GetBurstCells(south, 4, null);
            AoESystem.FilterCellsByLineOfEffect(burst, south);
            bool southOnly = true;
            foreach (Vector2Int c in burst)
                southOnly &= c.y <= 5;
            Assert(southOnly && burst.Contains(new Vector2Int(5, 5)) && burst.Contains(new Vector2Int(4, 4)),
                "a burst on the wall's face keeps the wall's own squares and its side, nothing beyond the wall");

            Assert(AoESystem.HasLineOfEffectToOrigin(new Vector2Int(5, 0), 1, south)
                   && !AoESystem.HasLineOfEffectToOrigin(new Vector2Int(5, 0), 1, north)
                   && AoESystem.HasLineOfEffectToOrigin(new Vector2Int(5, 8), 1, north)
                   && AoESystem.HasLineOfEffectToOrigin(new Vector2Int(5, 4), 1, south),
                "the caster needs line of effect to the point of origin: not to an intersection on the wall's far face (PHB p.176)");

            Assert(AoESystem.GetCandidateAimsAt(AoEShape.Burst, 4, new Vector2Int(5, 0), 1, new Vector2Int(5, 8), 1, 20, null).Count == 0,
                "the AI has no burst aim at a creature behind a wall (every corner of its square is out of the caster's line of effect)");
            List<AreaAim> nearAims = AoESystem.GetCandidateAimsAt(AoEShape.Burst, 4, new Vector2Int(5, 0), 1, new Vector2Int(5, 2), 1, 20, null);
            bool clipped = nearAims.Count == 4;
            foreach (AreaAim a in nearAims)
                foreach (Vector2Int c in a.Cells)
                    clipped &= c.y <= 5;
            Assert(clipped, "the AI's burst aims on its side of the wall keep only the squares their origin reaches, as the cast does", "count " + nearAims.Count);
            Assert(AoESystem.GetCandidateAimsAt(AoEShape.Cone, 3, new Vector2Int(5, 3), 1, new Vector2Int(5, 6), 1, -1, null).Count == 0,
                "no cone aim reaches a creature just beyond the wall");
        }
        finally
        {
            LineOfEffectService.Unregister(wall);
        }
    }

    // ── Play mode: the AI's and the PC's aims ─────────────────────────────────────────────────────────────

    private static void TestPlayModeAims()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("  (skipped the Play-mode aims: edit mode)");
            return;
        }

        SquareGrid previousGrid = SquareGrid.Instance;
        GameObject gridGo = null;
        GameObject gmGo = null;
        SpellcasterAIProfile profile = null;
        var made = new List<CharacterController>();
        try
        {
            gridGo = new GameObject("AreaOrigin_Grid");
            SquareGrid grid = gridGo.AddComponent<SquareGrid>();
            grid.Width = 30;
            grid.Height = 30;
            grid.GenerateGrid();
            gmGo = new GameObject("AreaOrigin_GM");
            GameManager gm = gmGo.AddComponent<GameManager>();
            gm.Grid = grid;

            CharacterController Make(string name, string cls, CharacterTeam team, int x, int y)
            {
                CharacterController c = TestHelpers.CreateCharacter(name: name, characterClass: cls, level: 5, intelligence: 18);
                c.SetTeam(team);
                TestHelpers.SetGridPosition(c, x, y);
                grid.SetCreatureOccupancy(c, c.GridPosition, 1);
                made.Add(c);
                return c;
            }

            // 1. The AI's Fireball evaluation picks the corner of the target's square that spares an ally 4 squares west.
            profile = DND35.AI.AIProfile.CreateRuntime<SpellcasterAIProfile>();
            profile.AOECasting.AvoidHittingAllies = true;
            profile.AOECasting.MaxAcceptableAllyCasualties = 0f;
            profile.AOECasting.AcceptableAllyDamagePercent = 0.05f;
            CharacterController wizard = Make("AI Wizard", "Wizard", CharacterTeam.Enemy, 0, 0);
            CharacterController hero = Make("Hero", "Fighter", CharacterTeam.Player, 10, 10);
            CharacterController ally = Make("Ally", "Fighter", CharacterTeam.Enemy, 6, 10);
            SpellData fireball = SpellDatabase.GetSpell(SpellNames.FIREBALL).Clone();
            var all = new List<CharacterController> { wizard, hero, ally };
            SpellAOEEvaluation spare = profile.EvaluateAOECast(fireball, wizard, hero, all, gm);
            Assert(spare.IsSafe && spare.EnemiesHit == 1 && spare.AlliesHit == 0,
                "the AI's Fireball takes the corner of the target's square that leaves its ally out (a square-centred burst hit it)",
                $"safe={spare.IsSafe} enemies={spare.EnemiesHit} allies={spare.AlliesHit}");

            TestHelpers.SetGridPosition(ally, 10, 11);
            SpellAOEEvaluation adjacent = profile.EvaluateAOECast(fireball, wizard, hero, all, gm);
            // Since the batch 11 step 25 review the AI searches every origin whose burst reaches the target (AreaAimSearch), so
            // an ally beside it no longer stops the Fireball: an origin on the target's far side (+(10,7): 4 to the hero's far
            // corner, 5 to the ally's, PHB p.175) takes the hero alone.
            Assert(adjacent.IsSafe && adjacent.EnemiesHit == 1 && adjacent.AlliesHit == 0 && adjacent.Placement != null
                   && !adjacent.Placement.Cells.Contains(new Vector2Int(10, 11)),
                "an ally beside the target: the AI's Fireball originates on the target's far side and spares it",
                $"safe={adjacent.IsSafe} enemies={adjacent.EnemiesHit} allies={adjacent.AlliesHit} aim={adjacent.Placement}");

            // 2. A 10-ft. breath cone starts at the corner that covers two foes and spares the breather's ally.
            CharacterController hound = Make("Hound", "Fighter", CharacterTeam.Enemy, 5, 20);
            CharacterController hero1 = Make("Hero1", "Fighter", CharacterTeam.Player, 7, 20);
            CharacterController hero2 = Make("Hero2", "Fighter", CharacterTeam.Player, 6, 19);
            CharacterController kobold = Make("Kobold", "Fighter", CharacterTeam.Enemy, 7, 21);
            AreaAim breath = AoESystem.BestDirectedAimAt(hound, hound.GridPosition, AoEShape.Cone, 2, hero1, grid);
            Assert(breath != null && breath.Origin == new GridIntersection(6, 20) && breath.Cells.Contains(hero1.GridPosition)
                   && breath.Cells.Contains(hero2.GridPosition) && !breath.Cells.Contains(kobold.GridPosition),
                "a breath starts at the corner whose cone takes both foes and not the breather's ally (MM p.69, PHB p.175)",
                breath != null ? "origin " + breath.Origin : "no aim");

            // 3. The PC aim rule: a Fireball snaps to the nearest intersection; a cone starts at the nearest corner.
            CharacterController pc = Make("PC Wizard", "Wizard", CharacterTeam.Player, 0, 25);
            bool burstOk = gm.Harness_BuildUiAreaAim(pc, fireball, new Vector2(10.3f, 24.8f), out HashSet<Vector2Int> burstCells, out GridIntersection? burstOrigin);
            Assert(burstOk && burstOrigin.HasValue && burstOrigin.Value == new GridIntersection(11, 25) && burstCells.Count == 44,
                "the PC's Fireball originates at the intersection nearest the cursor and covers 44 squares",
                $"ok={burstOk} origin={burstOrigin} count={(burstCells != null ? burstCells.Count : -1)}");
            SpellData burningHands = SpellDatabase.GetSpell(SpellNames.BURNING_HANDS).Clone();
            bool coneOk = gm.Harness_BuildUiAreaAim(pc, burningHands, new Vector2(3f, 25.2f), out HashSet<Vector2Int> coneCells, out GridIntersection? coneOrigin);
            Assert(coneOk && coneOrigin.HasValue && coneOrigin.Value == new GridIntersection(1, 26) && coneCells.Count == 6 && !coneCells.Contains(pc.GridPosition),
                "the PC's Burning Hands starts at the corner of its square nearest the cursor (6 squares, not its own)",
                $"ok={coneOk} origin={coneOrigin}");
            Assert(!gm.Harness_BuildUiAreaAim(pc, burningHands, new Vector2(0.1f, 24.9f), out _, out _),
                "the PC cannot aim a cone at its own square");
            SpellData callLightning = SpellDatabase.GetSpell(SpellNames.CALL_LIGHTNING).Clone();
            bool boltOk = gm.Harness_BuildUiAreaAim(pc, callLightning, new Vector2(5f, 25f), out HashSet<Vector2Int> boltCells, out GridIntersection? boltOrigin);
            Assert(boltOk && !boltOrigin.HasValue && boltCells.Count == 1 && boltCells.Contains(new Vector2Int(5, 25)),
                "a one-square effect (Call Lightning's bolt) takes the square under the cursor, with no intersection origin");

            // 3b. The cast core's Legality stage refuses a burst whose recorded origin is out of range, for every caster.
            //     The requests use a TestPanel source with no bypass: it skips only the caster tests (an action and a slot,
            //     CheckCastCasterLegality), which the suite's slotless TestHelpers wizard would fail first since batch 12
            //     step 9 (SPL-160); the range and line-of-effect tests still run as for any cast.
            CastSource OriginTestSource() => new CastSource { Kind = CastSourceKind.TestPanel };
            int fireballRange = CasterLevelRules.RangeSquares(pc, fireball);
            SpellCastRequest near = SpellCastRequest.ForUiArea(SpellCastRequest.ForUi(pc, fireball, null, OriginTestSource()),
                new List<CharacterController>(), burstCells, new GridIntersection(11, 25));
            SpellCastRequest far = SpellCastRequest.ForUiArea(SpellCastRequest.ForUi(pc, fireball, null, OriginTestSource()),
                new List<CharacterController>(), burstCells, new GridIntersection(fireballRange + 2, 26));
            CastOutcome nearOutcome = gm.CheckLegality(near);
            CastOutcome farOutcome = gm.CheckLegality(far);
            Assert(nearOutcome.Reason != "origin-out-of-range" && farOutcome.Kind == CastOutcomeKind.Refused && farOutcome.Reason == "origin-out-of-range",
                "the cast core refuses an area whose point of origin is beyond the spell's range of the caster's space (PHB p.175)",
                $"near {nearOutcome.Reason}, far {farOutcome.Reason}");

            // 3b'. The same for a Grease square and a one-square effect (batch 11 step 25 review): the range test lives in the
            //      cast core, not only in the PC highlight and the AI's aim search.
            SpellData greaseRanged = SpellDatabase.GetSpell(SpellNames.GREASE).Clone();
            int greaseRange = Mathf.Max(1, CasterLevelRules.RangeSquares(pc, greaseRanged));
            SpellCastRequest GreaseAt(Vector2Int anchor)
            {
                var cells = new HashSet<Vector2Int> { anchor, anchor + Vector2Int.right, anchor + Vector2Int.up, anchor + Vector2Int.one };
                SpellCastRequest r = SpellCastRequest.ForUiGrease(SpellCastRequest.ForUi(pc, greaseRanged, null, OriginTestSource()), null,
                    new List<CharacterController>(), cells, GridIntersection.NorthEastCornerOf(anchor));
                r.Choices.GreaseMode = GreaseCastMode.Area;
                return r;
            }
            CastOutcome greaseNear = gm.CheckLegality(GreaseAt(new Vector2Int(2, 25)));
            CastOutcome greaseBeyond = gm.CheckLegality(GreaseAt(new Vector2Int(greaseRange + 1, 25)));
            Assert(greaseNear.Reason != "origin-out-of-range" && greaseBeyond.Kind == CastOutcomeKind.Refused && greaseBeyond.Reason == "origin-out-of-range",
                "the cast core refuses a Grease square beyond Grease's range (PHB p.237: Close), for every caster",
                $"near {greaseNear.Reason}, beyond {greaseBeyond.Reason}");
            int boltRange = callLightning.AoERangeSquares > 0 ? callLightning.AoERangeSquares : CasterLevelRules.RangeSquares(pc, callLightning);
            SpellCastRequest boltNear = SpellCastRequest.ForUiArea(SpellCastRequest.ForUi(pc, callLightning, null, OriginTestSource()),
                new List<CharacterController>(), new HashSet<Vector2Int> { new Vector2Int(5, 25) }, null);
            SpellCastRequest boltBeyond = SpellCastRequest.ForUiArea(SpellCastRequest.ForUi(pc, callLightning, null, OriginTestSource()),
                new List<CharacterController>(), new HashSet<Vector2Int> { new Vector2Int(boltRange + 1, 25) }, null);
            CastOutcome boltNearOutcome = gm.CheckLegality(boltNear);
            CastOutcome boltBeyondOutcome = gm.CheckLegality(boltBeyond);
            Assert(boltNearOutcome.Reason != "origin-out-of-range" && boltBeyondOutcome.Kind == CastOutcomeKind.Refused && boltBeyondOutcome.Reason == "origin-out-of-range",
                "the cast core refuses a one-square effect (Call Lightning's bolt) whose square is beyond the spell's range",
                $"near {boltNearOutcome.Reason}, beyond {boltBeyondOutcome.Reason}");

            // 3c. Behind a wall: the PC's aim refuses an origin out of its line of effect, and so does the cast core (every
            //     caster), for a burst and for a Grease area (PHB p.176; SPL-006).
            var wall = new RowWall(20);
            LineOfEffectService.Register(wall);
            try
            {
                Assert(!gm.Harness_BuildUiAreaAim(pc, fireball, new Vector2(4.2f, 15.8f), out _, out _)
                       && gm.Harness_BuildUiAreaAim(pc, fireball, new Vector2(4.2f, 22.8f), out _, out _),
                    "the PC's Fireball cannot originate beyond a wall, only on the caster's side of it");
                SpellCastRequest hidden = SpellCastRequest.ForUiArea(SpellCastRequest.ForUi(pc, fireball, null, OriginTestSource()),
                    new List<CharacterController>(), AoESystem.GetBurstCells(new GridIntersection(5, 16), 4, grid), new GridIntersection(5, 16));
                CastOutcome hiddenOutcome = gm.CheckLegality(hidden);
                Assert(hiddenOutcome.Kind == CastOutcomeKind.Refused && hiddenOutcome.Reason == "origin-no-line-of-effect",
                    "the cast core refuses a burst whose point of origin the caster has no line of effect to", "reason " + hiddenOutcome.Reason);

                SpellData grease = SpellDatabase.GetSpell(SpellNames.GREASE).Clone();
                // Within Grease's range (7 squares at caster level 5) but beyond the wall, so line of effect is what refuses it.
                var greaseCells = new HashSet<Vector2Int> { new Vector2Int(1, 18), new Vector2Int(2, 18), new Vector2Int(1, 19), new Vector2Int(2, 19) };
                SpellCastRequest greaseFar = SpellCastRequest.ForUiGrease(SpellCastRequest.ForUi(pc, grease, null, OriginTestSource()), null,
                    new List<CharacterController>(), greaseCells, GridIntersection.NorthEastCornerOf(new Vector2Int(1, 18)));
                greaseFar.Choices.GreaseMode = GreaseCastMode.Area;
                CastOutcome greaseOutcome = gm.CheckLegality(greaseFar);
                Assert(greaseOutcome.Kind == CastOutcomeKind.Refused && greaseOutcome.Reason == "origin-no-line-of-effect",
                    "a Grease area beyond a wall is refused: the caster has no line of effect to its square's centre (SPL-006)", "reason " + greaseOutcome.Reason);

                // The area's own filter, from the recorded origin: a burst on the wall's south face keeps nothing north of it.
                CharacterController beyond = Make("Beyond", "Fighter", CharacterTeam.Enemy, 6, 22);
                CharacterController near2 = Make("Near", "Fighter", CharacterTeam.Enemy, 6, 18);
                var faceOrigin = new GridIntersection(6, 20);
                SpellCastRequest onFace = SpellCastRequest.ForUiArea(SpellCastRequest.ForUi(pc, fireball, null, new CastSource()),
                    new List<CharacterController> { beyond, near2 }, AoESystem.GetBurstCells(faceOrigin, 4, grid), faceOrigin);
                GameManager.Harness_FilterAreaAimByLineOfEffect(onFace);
                Assert(onFace.Aim.AreaTargets.Count == 1 && onFace.Aim.AreaTargets[0] == near2 && !onFace.Aim.Cells.Contains(new Vector2Int(6, 22)),
                    "an area cast from the face of a wall drops the creatures and squares beyond it (the line enters the wall first)",
                    "targets " + onFace.Aim.AreaTargets.Count);
            }
            finally
            {
                LineOfEffectService.Unregister(wall);
            }

            // 3d. Ally sparing counts every square of a Large ally: a corner that covers only its far squares is no safe aim.
            CharacterController target2 = Make("Hero Two", "Fighter", CharacterTeam.Player, 8, 3);
            CharacterController bigAlly = Make("Big Ally", "Fighter", CharacterTeam.Enemy, 4, 3);
            grid.ClearCreatureOccupancy(bigAlly);
            bigAlly.Stats.CurrentSizeCategory = SizeCategory.Large;
            grid.SetCreatureOccupancy(bigAlly, bigAlly.GridPosition, 2);
            Assert(AoESystem.IsCreatureInCells(new HashSet<Vector2Int> { new Vector2Int(5, 4) }, bigAlly)
                   && !AoESystem.IsCreatureInCells(new HashSet<Vector2Int> { new Vector2Int(6, 4) }, bigAlly),
                "a Large creature is in an area when any of its four squares is");
            // Since batch 11 step 25 the AI searches origins around the target's neighbours too (AreaAimSearch): every aim that
            // reaches any square of the Large ally counts it, and the best aim lies on the far side and spares it.
            var bigAll = new List<CharacterController> { wizard, target2, bigAlly };
            AreaAimSearch.ClearCache();
            bool everyAimCountsIt = true;
            int aimsReachingIt = 0;
            foreach (AreaPlacement aim in AreaAimSearch.Search(fireball, wizard, bigAll, grid))
            {
                bool reaches = AoESystem.IsCreatureInCells(aim.Cells, bigAlly);
                if (reaches) aimsReachingIt++;
                if (reaches != (aim.AlliesHit > 0)) everyAimCountsIt = false;
            }
            Assert(everyAimCountsIt && aimsReachingIt > 0,
                "every searched burst that reaches any square of the Large ally counts it as hit (its lower-left square alone was once tested)",
                "aims reaching it " + aimsReachingIt);
            SpellAOEEvaluation bigEval = profile.EvaluateAOECast(fireball, wizard, target2, bigAll, gm);
            Assert(bigEval.IsSafe && bigEval.AlliesHit == 0 && bigEval.Placement != null && !AoESystem.IsCreatureInCells(bigEval.Placement.Cells, bigAlly),
                "the AI's Fireball takes an origin that spares every square of the Large ally",
                $"safe={bigEval.IsSafe} allies={bigEval.AlliesHit}");

            // 3e. A lasting area made by a cast covers exactly the cast's squares (PersistentAreaEffect.CastFootprint, SPL-029).
            var footprint = new HashSet<Vector2Int> { new Vector2Int(2, 2), new Vector2Int(3, 2), new Vector2Int(2, 3), new Vector2Int(9, 9) };
            var before = new HashSet<PersistentAreaEffect>(Object.FindObjectsByType<PersistentAreaEffect>());
            gm.CreateWebArea(new Vector3(2f, 2f, 0f) * SquareGridUtils.CellSize, 3, 5, 15, pc, null, footprint: footprint);
            gm.CreateFogCloudArea(new Vector3(2f, 2f, 0f) * SquareGridUtils.CellSize, 3, 5, pc, footprint: footprint);
            var lasting = new List<PersistentAreaEffect>();
            foreach (PersistentAreaEffect area in Object.FindObjectsByType<PersistentAreaEffect>())
                if (!before.Contains(area) && (area is WebAreaEffect || area is FogCloudAreaEffect))
                    lasting.Add(area);
            try
            {
                var calc = typeof(PersistentAreaEffect).GetMethod("CalculateAffectedCells",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                bool exact = lasting.Count == 2 && calc != null;
                foreach (PersistentAreaEffect area in lasting)
                {
                    calc?.Invoke(area, null);
                    exact &= area.AffectedCells != null && area.AffectedCells.SetEquals(footprint);
                }
                Assert(exact, "Web and Fog Cloud cover exactly the cast's squares, not a circle around their centre (SPL-029)",
                    "areas " + lasting.Count);
            }
            finally
            {
                foreach (PersistentAreaEffect area in lasting)
                    if (area != null) Object.DestroyImmediate(area.gameObject);
            }

            // 4. Prayer's burst, centred on the caster, takes the corner that reaches a creature 2 squares west.
            CharacterController priest = Make("Priest", "Cleric", CharacterTeam.Player, 20, 5);
            Make("West", "Fighter", CharacterTeam.Enemy, 18, 5);
            GridIntersection prayerOrigin = AoESystem.BestCreatureCentredOrigin(priest, 2, grid);
            Assert(prayerOrigin == new GridIntersection(20, 6),
                "a burst centred on the caster with no aiming step takes the corner covering the most creatures (the north-west one here)",
                "origin " + prayerOrigin);

            // 5. A scored corner (Invisibility Purge's: invisible foes +1, invisible allies -1) follows its score.
            CharacterController east = Make("East", "Fighter", CharacterTeam.Enemy, 22, 5);
            GridIntersection scored = AoESystem.BestCreatureCentredOrigin(priest, 2, grid, c => c == east ? 1 : c.Stats.CharacterName == "West" ? -1 : 0);
            Assert(scored == new GridIntersection(21, 6),
                "a corner chosen by a score takes the one with the best score (the north-east one, which reaches only the eastern creature)",
                "origin " + scored);

            // 6. The cast core's summon square test (GameManager.SummonPlacementRefusal, batch 11 step 27), the one the PC
            // highlight, the AI's square and a Scripted square all meet: range, room for the creature's space, line of effect.
            CharacterController summoner = Make("Summoner", "Cleric", CharacterTeam.Player, 5, 25);
            Make("Blocker", "Fighter", CharacterTeam.Enemy, 7, 25);
            Assert(gm.SummonPlacementRefusal(summoner, new Vector2Int(6, 25), 5, 1) == null,
                "a free square within range and in sight takes the summon");
            Assert(gm.SummonPlacementRefusal(summoner, new Vector2Int(5, 29), 3, 1) == "summon-out-of-range",
                "a square beyond the spell's range is refused");
            Assert(gm.SummonPlacementRefusal(summoner, new Vector2Int(7, 25), 5, 1) == "summon-no-room",
                "an occupied square is refused (PHB p.173: not inside a creature)");
            Assert(gm.SummonPlacementRefusal(summoner, new Vector2Int(6, 25), 5, 2) == "summon-no-room",
                "a Large creature's space overlapping a creature is refused");
            var summonWall = new RowWall(27);
            LineOfEffectService.Register(summonWall);
            try
            {
                Assert(gm.SummonPlacementRefusal(summoner, new Vector2Int(5, 29), 5, 1) == "summon-no-line-of-effect",
                    "a square beyond a wall is refused (PHB p.176)");
                Assert(gm.SummonPlacementRefusal(summoner, new Vector2Int(4, 26), 5, 1) == null,
                    "a square on the caster's side of the wall is accepted");
            }
            finally
            {
                LineOfEffectService.Unregister(summonWall);
            }
        }
        finally
        {
            foreach (CharacterController c in made)
                TestHelpers.Cleanup(c != null ? c.gameObject : null);
            if (profile != null) Object.DestroyImmediate(profile);
            if (gmGo != null) Object.DestroyImmediate(gmGo);
            if (gridGo != null) Object.DestroyImmediate(gridGo);
            // SquareGrid.Awake replaced the static Instance; put the previous grid back (GRID-010).
            typeof(SquareGrid).GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.SetValue(null, previousGrid);
        }
    }

    private static void Assert(bool condition, string name, string detail = "")
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  PASS: {name}");
        }
        else
        {
            _failed++;
            Debug.LogError($"[AreaOrigin] FAIL: {name}" + (string.IsNullOrEmpty(detail) ? "" : " — " + detail));
        }
    }
}
}
#endif
