using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Tests.UI
{
/// <summary>
/// Grouped AI turns step 11 (gt-overlap-and-options; docs/designs/grouped_ai_turns.md "Playback" R1-R8, "The group" S5,
/// design choice C5): the pure overlap scheduler, <see cref="PlaybackScheduler"/>, on fake beats. Checks: moves of two
/// turns overlap; the result track keeps rules order (and the banner's turn follows it); R4, a walk into a fallen ally's
/// square waits for the fall; R5, an AoO mid-move shows before the mover goes on and an attack after the walk arrives;
/// R7, a window stop (S2) makes its lane a window of its own and a barrier (an interactive or control beat) waits for
/// everything before it and holds everything after it; S5, a creature's second turn in a window (WindowCap) opens a new
/// window; R8, at most 3 lanes play at once; R2, a single lane plays each Pace beat at Pace / speed (an Ai one; a Player
/// one at its seconds) and overlapping lanes space them by the stagger; a beat chained on one that finished earlier in the
/// frame starts at once (a walk's length does not depend on the frame rate); streaming never passes the recording head (R1);
/// and a generated mix of turns keeps every rule and finishes every beat. Pure: either mode.
/// </summary>
public static class PlaybackSchedulerTests
{
    private static int _passed;
    private static int _failed;

    private const float Frame = 1f / 60f;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("========== PLAYBACK SCHEDULER TESTS (grouped AI turns step 11) ==========");

        Run(TestConstants);
        Run(TestMovesOverlap);
        Run(TestResultOrderHolds);
        Run(TestWalkIntoFallenAllySquare);
        Run(TestAoOMidMove);
        Run(TestWindowAtStopMark);
        Run(TestBarrierAtInteractive);
        Run(TestSecondTurnOpensWindow);
        Run(TestAtMostThreeLanes);
        Run(TestSingleLaneAtPaceOverSpeed);
        Run(TestChainedBeatsDoNotWaitForFrame);
        Run(TestOverlapSpacesResultsByStagger);
        Run(TestStreamingNeverPassesHead);
        Run(TestGeneratedMixKeepsEveryRule);

        Debug.Log($"========== RESULTS: {_passed} passed, {_failed} failed ==========");
    }

    // ── Fake beats ──────────────────────────────────────────────────────────────────────────────────────────────────

    private sealed class Token
    {
        public readonly string Name;
        public Token(string name) { Name = name; }
        public override string ToString() => Name;
    }

    private static int Cell(int x, int y) => x * 65536 + y;

    private static PlaybackScheduler.Item Result(int seq, int turn, object token = null, params int[] cells)
        => new PlaybackScheduler.Item { Seq = seq, Turn = turn, Token = token, Cells = cells != null && cells.Length > 0 ? cells : null, Payload = "r" + seq };

    private static PlaybackScheduler.Item Pace(int seq, int turn, float seconds, bool ai = true)
        => new PlaybackScheduler.Item { Seq = seq, Turn = turn, IsPace = true, Seconds = seconds, ScaleWithSpeed = ai, Payload = "p" + seq };

    private static PlaybackScheduler.Item Step(int seq, int turn, object token, int from, int into, float seconds = 0.2f)
        => new PlaybackScheduler.Item
        {
            Seq = seq, Turn = turn, OnLane = true, IsStep = true, Token = token, Seconds = seconds, ScaleWithSpeed = true,
            Cells = new[] { from, into }, IntoCells = new[] { into }, Payload = "s" + seq
        };

    private static PlaybackScheduler.Item Flagged(int seq, int turn, PlaybackScheduler.ItemFlags flags)
        => new PlaybackScheduler.Item { Seq = seq, Turn = turn, Flags = flags, Payload = "f" + seq };

    /// <summary>Feeds every item, closes the recording, and plays frame by frame until every item has finished.</summary>
    private static PlaybackScheduler Play(IList<PlaybackScheduler.Item> items, float speed, out List<PlaybackScheduler.Item> order,
        out int maxAdmitted, float limit = 120f, float frame = Frame)
    {
        var s = new PlaybackScheduler { RecordingOpen = true };
        foreach (var it in items)
            s.Add(it);
        s.RecordingOpen = false;
        order = new List<PlaybackScheduler.Item>();
        maxAdmitted = 0;
        for (float t = 0f; t < limit && s.Pending > 0; t += frame)
        {
            s.Advance(frame, speed, order);
            maxAdmitted = Math.Max(maxAdmitted, s.AdmittedLanes);
        }
        return s;
    }

    private static float End(PlaybackScheduler.Item i) => i.StartedAt + i.Duration;

    private static bool AllFinished(IEnumerable<PlaybackScheduler.Item> items) => items.All(i => i.Started && i.Finished);

    // ── Tests ───────────────────────────────────────────────────────────────────────────────────────────────────────

    private static void TestConstants()
    {
        Assert(PlaybackScheduler.BaseStagger == 0.15f && PlaybackScheduler.MaxLanes == 3, "The constants: a 0.15 s stagger and 3 lanes");
        Assert(Near(PlaybackScheduler.StaggerFor(2f), 0.075f) && Near(PlaybackScheduler.StaggerFor(1f), 0.15f)
               && Near(PlaybackScheduler.StaggerFor(0.5f), 0.15f), "The stagger is 0.15 s divided by the speed (never below 1x)");
    }

    private static void TestMovesOverlap()
    {
        var a = new Token("orc1");
        var b = new Token("orc2");
        var items = new List<PlaybackScheduler.Item>
        {
            Result(1, 1), Step(2, 1, a, Cell(0, 0), Cell(1, 0)), Step(3, 1, a, Cell(1, 0), Cell(2, 0)), Step(4, 1, a, Cell(2, 0), Cell(3, 0)),
            Result(5, 2), Step(6, 2, b, Cell(0, 5), Cell(1, 5)), Step(7, 2, b, Cell(1, 5), Cell(2, 5)), Step(8, 2, b, Cell(2, 5), Cell(3, 5)),
        };
        Play(items, 2f, out _, out int maxAdmitted);
        Assert(AllFinished(items), "Every beat of two walking turns plays");
        Assert(items[5].StartedAt < End(items[3]) - 1e-4f,
            "Moves overlap: the second orc's first step starts before the first orc's last step ends",
            items[5].StartedAt + " vs " + End(items[3]));
        Assert(Near(items[5].StartedAt - items[1].StartedAt, PlaybackScheduler.StaggerFor(2f), 0.02f),
            "R6: the second lane starts one stagger (0.15 s / speed) after the first", (items[5].StartedAt - items[1].StartedAt).ToString());
        Assert(maxAdmitted == 2, "Both lanes play at once", maxAdmitted.ToString());
    }

    private static void TestResultOrderHolds()
    {
        var a = new Token("a");
        var b = new Token("b");
        var c = new Token("c");
        var items = new List<PlaybackScheduler.Item>
        {
            Result(1, 1), Pace(2, 1, 0.5f), Step(3, 1, a, Cell(0, 0), Cell(1, 0)), Result(4, 1), Pace(5, 1, 1f),
            Result(6, 2), Step(7, 2, b, Cell(0, 3), Cell(1, 3)), Step(8, 2, b, Cell(1, 3), Cell(2, 3)), Result(9, 2), Pace(10, 2, 1f),
            Result(11, 3), Step(12, 3, c, Cell(0, 6), Cell(1, 6)), Result(13, 3), Pace(14, 3, 0.5f), Result(15, 3),
        };
        var s = new PlaybackScheduler { RecordingOpen = true };
        foreach (var it in items)
            s.Add(it);
        s.RecordingOpen = false;
        var order = new List<PlaybackScheduler.Item>();
        var bannerTurns = new List<int>();
        for (float t = 0f; t < 60f && s.Pending > 0; t += Frame)
        {
            s.Advance(Frame, 2f, order);
            int? turn = s.CurrentResultTurn;
            if (turn.HasValue && (bannerTurns.Count == 0 || bannerTurns[bannerTurns.Count - 1] != turn.Value))
                bannerTurns.Add(turn.Value);
        }
        Assert(AllFinished(items), "Every beat plays");
        int[] results = order.Where(i => !i.OnLane).Select(i => i.Seq).ToArray();
        Assert(results.SequenceEqual(results.OrderBy(x => x)), "R2: the result track finishes in rules order", string.Join(",", results));
        Assert(bannerTurns.SequenceEqual(bannerTurns.OrderBy(x => x)) && bannerTurns.Distinct().Count() == bannerTurns.Count,
            "The banner's turn (the result track's) only moves forward, one turn after another", string.Join(",", bannerTurns));
        var res = items.Where(i => !i.OnLane).ToList();
        bool serial = true;
        for (int k = 1; k < res.Count; k++)
            if (res[k].StartedAt + 1e-4f < End(res[k - 1]))
                serial = false;
        Assert(serial, "R2: a result beat starts only after the one before it has finished");
    }

    private static void TestWalkIntoFallenAllySquare()
    {
        var orc = new Token("orc");
        var goblin = new Token("goblin");
        var ally = new Token("ally");
        int fallen = Cell(5, 5);
        var items = new List<PlaybackScheduler.Item>
        {
            // Turn 1: the orc walks up and drops the goblin standing at (5,5) (its fall is a keyframe there).
            Step(1, 1, orc, Cell(0, 5), Cell(1, 5), 0.6f), Step(2, 1, orc, Cell(1, 5), Cell(2, 5), 0.6f), Step(3, 1, orc, Cell(2, 5), Cell(3, 5), 0.6f),
            Step(4, 1, orc, Cell(3, 5), Cell(4, 5), 0.6f), Result(5, 1), Pace(6, 1, 1f), Result(7, 1, goblin, fallen), Pace(8, 1, 1f),
            // Turn 2: another goblin walks into the freed square.
            Step(9, 2, ally, Cell(5, 8), Cell(5, 7)), Step(10, 2, ally, Cell(5, 7), Cell(5, 6)), Step(11, 2, ally, Cell(5, 6), fallen),
        };
        Play(items, 2f, out _, out _);
        Assert(AllFinished(items), "Every beat plays");
        Assert(items[8].StartedAt < End(items[3]), "The second walk starts while the first is still going (overlap)",
            items[8].StartedAt + " vs " + End(items[3]));
        Assert(items[10].StartedAt + 1e-4f >= End(items[6]),
            "R4: the step into the fallen goblin's square starts after its fall's keyframe has played",
            items[10].StartedAt + " vs " + End(items[6]));
    }

    private static void TestAoOMidMove()
    {
        var mover = new Token("mover");
        var fighter = new Token("fighter");
        var items = new List<PlaybackScheduler.Item>
        {
            Result(1, 1), Step(2, 1, mover, Cell(0, 0), Cell(1, 0)), Step(3, 1, mover, Cell(1, 0), Cell(2, 0)),
            // The AoO mid-move: its log line, the fighter's swing keyframe and the dwell.
            Result(4, 1), Result(5, 1, fighter, Cell(3, 1)), Pace(6, 1, 0.5f),
            Step(7, 1, mover, Cell(2, 0), Cell(3, 0)), Step(8, 1, mover, Cell(3, 0), Cell(4, 0)),
            // The attack after the walk.
            Result(9, 1), Pace(10, 1, 1f),
        };
        Play(items, 2f, out _, out _);
        Assert(AllFinished(items), "Every beat plays");
        Assert(items[6].StartedAt + 1e-4f >= End(items[5]) && items[6].StartedAt + 1e-4f >= End(items[3]),
            "R5: after an AoO mid-move the mover goes on only once the AoO's results have played",
            items[6].StartedAt + " vs " + End(items[5]));
        Assert(items[3].StartedAt + 1e-4f >= End(items[2]), "R5: the AoO's log line waits for the steps before it (the lane's earlier segment)");
        Assert(items[8].StartedAt + 1e-4f >= End(items[7]), "R5: the attack's log line shows after the walk arrives");
        Assert(items[1].StartedAt < End(items[0]) + 1e-4f, "The first segment does not wait for its own turn's opening result beats");
    }

    private static void TestWindowAtStopMark()
    {
        var a = new Token("a");
        var b = new Token("b");
        var c = new Token("c");
        var items = new List<PlaybackScheduler.Item>
        {
            Step(1, 1, a, Cell(0, 0), Cell(1, 0), 0.6f), Step(2, 1, a, Cell(1, 0), Cell(2, 0), 0.6f), Result(3, 1), Pace(4, 1, 0.6f),
            // Turn 2 provokes a player-run fighter's AoO: the window stop (S2).
            Step(5, 2, b, Cell(0, 4), Cell(1, 4), 0.6f), Flagged(6, 2, PlaybackScheduler.ItemFlags.StopMark), Result(7, 2), Pace(8, 2, 0.6f),
            Step(9, 2, b, Cell(1, 4), Cell(2, 4), 0.6f),
            Step(10, 3, c, Cell(0, 8), Cell(1, 8), 0.6f), Step(11, 3, c, Cell(1, 8), Cell(2, 8), 0.6f), Result(12, 3),
        };
        Play(items, 2f, out _, out _);
        Assert(AllFinished(items), "Every beat plays");
        float turn1End = items.Take(4).Max(End);
        float turn2End = items.Skip(4).Take(5).Max(End);
        Assert(items[4].StartedAt + 1e-4f >= turn1End, "R7: the lane holding the S2 mark starts after every earlier beat has played",
            items[4].StartedAt + " vs " + turn1End);
        Assert(items[9].StartedAt + 1e-4f >= turn2End, "R7: the next lane opens a new window after the marked lane",
            items[9].StartedAt + " vs " + turn2End);
        Assert(Near(items[7].Duration, 0.3f), "R7: the marked lane plays its Pace beats at Pace / speed (no stagger)", items[7].Duration.ToString());
    }

    private static void TestBarrierAtInteractive()
    {
        var a = new Token("a");
        var b = new Token("b");
        var items = new List<PlaybackScheduler.Item>
        {
            Step(1, 1, a, Cell(0, 0), Cell(1, 0), 0.6f), Step(2, 1, a, Cell(1, 0), Cell(2, 0), 0.6f), Result(3, 1),
            Step(4, 2, b, Cell(0, 4), Cell(1, 4), 0.6f), Result(5, 2),
            Flagged(6, 2, PlaybackScheduler.ItemFlags.Barrier), // the defender prompt (S3) or the control beat (S1)
            Result(7, 2), Step(8, 3, a, Cell(2, 0), Cell(3, 0), 0.6f),
        };
        Play(items, 2f, out _, out _);
        Assert(AllFinished(items), "Every beat plays");
        float before = items.Take(5).Max(End);
        Assert(items[5].StartedAt + 1e-4f >= before, "R7: a barrier starts after every earlier beat has played (the prompt opens after the playback reaches it)",
            items[5].StartedAt + " vs " + before);
        Assert(items[6].StartedAt + 1e-4f >= End(items[5]) && items[7].StartedAt + 1e-4f >= End(items[5]),
            "R7: nothing after a barrier starts before it has played");
        Assert(items[3].StartedAt < End(items[1]), "Before the barrier the two lanes still overlap");
    }

    private static void TestSecondTurnOpensWindow()
    {
        var cap = new PlaybackScheduler.WindowCap();
        var orc = new Token("orc");
        var wolf = new Token("wolf");
        Assert(!cap.Note(orc) && !cap.Note(wolf), "S5: first turns of two creatures share a window");
        Assert(cap.Note(orc) && cap.Count == 1, "S5: the orc's second turn ends the window and opens a new one with it");
        Assert(!cap.Note(wolf) && cap.Note(wolf), "S5: in the new window the wolf's first turn joins it and its second ends it");
        cap.Reset();
        Assert(!cap.Note(wolf) && !cap.Note(null) && !cap.Note(null), "A reset (a group open, a window stop) starts a fresh window; a null actor never breaks one");

        var items = new List<PlaybackScheduler.Item>
        {
            Step(1, 1, orc, Cell(0, 0), Cell(1, 0), 0.6f), Step(2, 1, orc, Cell(1, 0), Cell(2, 0), 0.6f),
            Step(3, 2, wolf, Cell(0, 4), Cell(1, 4), 0.6f), Step(4, 2, wolf, Cell(1, 4), Cell(2, 4), 0.6f),
            // The orc's next turn (a round later): TurnControl.AiTurnStarting records the window break in its lane.
            Result(5, 3), Flagged(6, 3, PlaybackScheduler.ItemFlags.WindowBreak), Step(7, 3, orc, Cell(2, 0), Cell(3, 0), 0.6f),
        };
        Play(items, 2f, out _, out _);
        Assert(AllFinished(items), "Every beat plays");
        float earlier = items.Take(4).Max(End);
        Assert(items[4].StartedAt + 1e-4f >= earlier && items[6].StartedAt + 1e-4f >= earlier,
            "S5: the creature's second turn starts a new window, after the first window has played", items[6].StartedAt + " vs " + earlier);
        Assert(items[2].StartedAt < End(items[1]), "Inside the first window the two turns overlap");
    }

    private static void TestAtMostThreeLanes()
    {
        var items = new List<PlaybackScheduler.Item>();
        int seq = 0;
        for (int turn = 1; turn <= 5; turn++)
        {
            var tok = new Token("t" + turn);
            for (int k = 0; k < 4; k++)
                items.Add(Step(++seq, turn, tok, Cell(k, turn * 3), Cell(k + 1, turn * 3), 0.8f));
        }
        Play(items, 2f, out _, out int maxAdmitted);
        Assert(AllFinished(items), "Every beat of five walking turns plays");
        Assert(maxAdmitted == PlaybackScheduler.MaxLanes, "R8: at most 3 lanes play at once (and 3 do)", maxAdmitted.ToString());
        int overlapping = MaxConcurrentTurns(items);
        Assert(overlapping <= 3, "R8: no moment shows beats of more than 3 turns in flight", overlapping.ToString());
        float lane1End = items.Where(i => i.Turn == 1).Max(End);
        Assert(items.First(i => i.Turn == 4).StartedAt + 1e-4f >= lane1End, "R8: the fourth lane starts only once a lane has finished");
    }

    private static void TestSingleLaneAtPaceOverSpeed()
    {
        var items = new List<PlaybackScheduler.Item> { Result(1, 1), Pace(2, 1, 1f), Result(3, 1), Pace(4, 1, 0.6f, ai: false), Result(5, 1) };
        Play(items, 2f, out _, out _);
        Assert(AllFinished(items), "Every beat plays");
        Assert(Near(items[1].Duration, 0.5f) && Near(items[2].StartedAt, 0.5f, 0.001f),
            "A single lane plays an Ai Pace beat at Pace / speed (1 s at 2x is 0.5 s)", items[1].Duration + " " + items[2].StartedAt);
        Assert(Near(items[3].Duration, 0.6f), "A Player-kind Pace beat keeps its seconds", items[3].Duration.ToString());
        var steps = new List<PlaybackScheduler.Item> { Step(1, 1, new Token("x"), Cell(0, 0), Cell(1, 0), 0.4f), Step(2, 1, new Token("y"), Cell(5, 5), Cell(6, 5), 0f) };
        Play(steps, 4f, out _, out _);
        Assert(Near(steps[0].Duration, 0.1f) && Near(steps[1].Duration, PlaybackScheduler.MinStepSeconds),
            "A step plays its seconds / speed, never under the 0.01 s floor", steps[0].Duration + " " + steps[1].Duration);
    }

    /// <summary>
    /// A beat that waits on one that finished earlier in the same Advance starts at that moment, not on the next frame:
    /// a walk's steps are contiguous and its length does not depend on the frame rate, and back-to-back zero-length
    /// beats of one token all play in one frame.
    /// </summary>
    private static void TestChainedBeatsDoNotWaitForFrame()
    {
        List<PlaybackScheduler.Item> Walk(object tok) => new List<PlaybackScheduler.Item>
        {
            Step(1, 1, tok, Cell(0, 0), Cell(1, 0), 0.15f), Step(2, 1, tok, Cell(1, 0), Cell(2, 0), 0.15f),
            Step(3, 1, tok, Cell(2, 0), Cell(3, 0), 0.15f),
        };

        var walk = Walk(new Token("w"));
        Play(walk, 2f, out _, out _);
        Assert(AllFinished(walk) && Near(walk[1].StartedAt, End(walk[0])) && Near(walk[2].StartedAt, End(walk[1]))
               && Near(End(walk[2]), 0.225f),
            "R3: each step of a walk starts when the previous one ends (0.075 s steps at 2x, 1/60 s frames)",
            walk[1].StartedAt + " " + End(walk[0]) + " " + End(walk[2]));

        var slow = Walk(new Token("w7"));
        Play(slow, 2f, out _, out _, frame: 1f / 7f);
        Assert(AllFinished(slow) && Near(End(slow[2]) - slow[0].StartedAt, 0.225f),
            "A walk's length does not depend on the frame rate (1/7 s frames)", (End(slow[2]) - slow[0].StartedAt).ToString());

        var tok = new Token("one");
        var one = Walk(tok);
        one.Add(Result(4, 1, tok));
        var s = new PlaybackScheduler();
        foreach (var it in one)
            s.Add(it);
        var order = new List<PlaybackScheduler.Item>();
        s.Advance(10f, 2f, order);
        Assert(AllFinished(one) && s.Pending == 0 && Near(one[3].StartedAt, End(one[2])),
            "One long Advance plays a whole walk and the result after it", one[3].StartedAt + " " + End(one[2]));

        var z = new Token("z");
        var zeros = new List<PlaybackScheduler.Item> { Result(1, 1, z), Result(2, 1, z), Result(3, 1, z), Result(4, 1, z), Result(5, 1, z), Pace(6, 1, 1f) };
        var sz = new PlaybackScheduler();
        foreach (var it in zeros)
            sz.Add(it);
        var zOrder = new List<PlaybackScheduler.Item>();
        sz.Advance(Frame, 2f, zOrder);
        Assert(zeros.Take(5).All(i => i.Finished) && zeros[5].Started,
            "R3: five zero-length beats of one token all play in the first frame, and the Pace after them starts",
            string.Join(",", zOrder.Select(i => i.Seq)));
    }

    private static void TestOverlapSpacesResultsByStagger()
    {
        var a = new Token("a");
        var b = new Token("b");
        var items = new List<PlaybackScheduler.Item>
        {
            // Lane 1 walks for 0.6 s; lane 2 (admitted at 0.15 s) steps, then logs and paces while lane 1 still walks.
            Step(1, 1, a, Cell(0, 0), Cell(1, 0), 0.3f), Step(2, 1, a, Cell(1, 0), Cell(2, 0), 0.3f),
            Step(3, 2, b, Cell(0, 4), Cell(1, 4), 0.2f), Result(4, 2), Pace(5, 2, 1f), Result(6, 2), Pace(7, 2, 1f),
            Result(8, 2), Pace(9, 2, 1f),
        };
        Play(items, 1f, out _, out _);
        Assert(AllFinished(items), "Every beat plays");
        Assert(Near(items[4].Duration, PlaybackScheduler.StaggerFor(1f)) && items[4].StartedAt < End(items[1]),
            "R2: while two lanes play, a Pace beat is spaced by min(Pace / speed, stagger)", items[4].Duration + " at " + items[4].StartedAt);
        Assert(items[8].StartedAt + 1e-4f >= End(items[1]) && Near(items[8].Duration, 1f),
            "R2: once one lane is left, a Pace beat plays at Pace / speed again", items[8].Duration + " at " + items[8].StartedAt);
    }

    private static void TestStreamingNeverPassesHead()
    {
        var a = new Token("a");
        var s = new PlaybackScheduler { RecordingOpen = true };
        var order = new List<PlaybackScheduler.Item>();
        var t1 = new List<PlaybackScheduler.Item> { Result(1, 1), Step(2, 1, a, Cell(0, 0), Cell(1, 0)), Result(3, 1) };
        foreach (var it in t1)
            s.Add(it);
        for (int f = 0; f < 120; f++)
            s.Advance(Frame, 2f, order);
        Assert(order.Count == 0 && !t1.Any(i => i.Started), "R1: a turn still recording does not start (the playback waits at the head)");
        var t2 = new List<PlaybackScheduler.Item> { Result(4, 2), Step(5, 2, a, Cell(1, 0), Cell(2, 0)) };
        foreach (var it in t2)
            s.Add(it);
        for (int f = 0; f < 120; f++)
            s.Advance(Frame, 2f, order);
        Assert(AllFinished(t1), "R1: once a later turn arrives, the finished turn plays");
        Assert(!t2.Any(i => i.Started) && order.All(i => i.Seq <= 3), "The turn being recorded never plays, and nothing past the recorded head is shown");
        s.RecordingOpen = false;
        for (int f = 0; f < 120 && s.Pending > 0; f++)
            s.Advance(Frame, 2f, order);
        Assert(AllFinished(t2) && s.Pending == 0, "The close (recording ended) lets the last turn play");
        var late = Result(6, 2);
        s.Add(late);
        s.Advance(0f, 2f, order);
        Assert(late.Finished, "A beat queued after the close (queue while behind) plays in order");
    }

    /// <summary>A generated mix of turns, steps, results, paces, stops, breaks and barriers: every rule holds and every beat plays.</summary>
    private static void TestGeneratedMixKeepsEveryRule()
    {
        uint state = 12345u;
        int Next(int n)
        {
            state = state * 1664525u + 1013904223u;
            return (int)((state >> 8) % (uint)n);
        }

        var tokens = Enumerable.Range(0, 6).Select(i => new Token("t" + i)).ToArray();
        var pos = new int[tokens.Length];
        for (int i = 0; i < pos.Length; i++)
            pos[i] = Cell(Next(6), Next(6));
        var items = new List<PlaybackScheduler.Item>();
        int seq = 0;
        for (int turn = 1; turn <= 30; turn++)
        {
            int actor = Next(tokens.Length);
            int beats = 3 + Next(8);
            for (int k = 0; k < beats; k++)
            {
                int roll = Next(20);
                if (roll < 7)
                {
                    int to = Cell(Next(6), Next(6));
                    items.Add(Step(++seq, turn, tokens[actor], pos[actor], to, 0.1f + Next(4) * 0.1f));
                    pos[actor] = to;
                }
                else if (roll < 11)
                {
                    items.Add(Pace(++seq, turn, 0.2f + Next(5) * 0.2f, Next(4) != 0));
                }
                else if (roll < 15)
                {
                    int victim = Next(tokens.Length);
                    items.Add(Result(++seq, turn, tokens[victim], pos[victim]));
                }
                else if (roll < 18)
                {
                    items.Add(Result(++seq, turn));
                }
                else if (roll == 18)
                {
                    items.Add(Flagged(++seq, turn, Next(2) == 0 ? PlaybackScheduler.ItemFlags.StopMark : PlaybackScheduler.ItemFlags.WindowBreak));
                }
                else
                {
                    items.Add(Flagged(++seq, turn, PlaybackScheduler.ItemFlags.Barrier));
                }
            }
        }
        Play(items, 2f, out List<PlaybackScheduler.Item> order, out int maxAdmitted, 600f);
        Assert(AllFinished(items) && order.Count == items.Count, "Every generated beat plays exactly once (no deadlock)",
            order.Count + " of " + items.Count);

        int[] results = order.Where(i => !i.OnLane).Select(i => i.Seq).ToArray();
        Assert(results.SequenceEqual(results.OrderBy(x => x)), "R2: the result track finishes in rules order");

        bool tokenOrder = true, cellOrder = true, barrierOrder = true;
        foreach (var later in items)
        {
            foreach (var earlier in items)
            {
                if (earlier.Seq >= later.Seq)
                    break;
                if (later.Token != null && ReferenceEquals(earlier.Token, later.Token) && later.StartedAt + 1e-4f < End(earlier))
                    tokenOrder = false;
                if (later.OnLane && later.IntoCells != null && earlier.Cells != null && earlier.Cells.Intersect(later.IntoCells).Any()
                    && later.StartedAt + 1e-4f < End(earlier))
                    cellOrder = false;
                bool barrier = (later.Flags & PlaybackScheduler.ItemFlags.Barrier) != 0 || (earlier.Flags & PlaybackScheduler.ItemFlags.Barrier) != 0;
                if (barrier && later.StartedAt + 1e-4f < End(earlier))
                    barrierOrder = false;
            }
        }
        Assert(tokenOrder, "R3: per token, every beat starts after the token's earlier beats have finished");
        Assert(cellOrder, "R4: every step starts after the earlier beats on the cells it enters");
        Assert(barrierOrder, "R7: a barrier plays after every earlier beat, and nothing after it starts before it ends");
        Assert(maxAdmitted <= PlaybackScheduler.MaxLanes && MaxConcurrentTurns(items) <= PlaybackScheduler.MaxLanes,
            "R8: never more than 3 lanes at once", maxAdmitted + " / " + MaxConcurrentTurns(items));
        Assert(items.Where(i => i.OnLane).Any(l => items.Any(o => o.Turn != l.Turn && o.OnLane && o.StartedAt < End(l) - 1e-4f && l.StartedAt < End(o) - 1e-4f)),
            "Steps of different turns overlap somewhere in the mix");
    }

    private static int MaxConcurrentTurns(List<PlaybackScheduler.Item> items)
    {
        int max = 0;
        foreach (var probe in items)
        {
            float t = probe.StartedAt + 1e-4f;
            int n = items.Where(i => i.Duration > 0f && i.StartedAt <= t && End(i) > t).Select(i => i.Turn).Distinct().Count();
            max = Math.Max(max, n);
        }
        return max;
    }

    // ── Plumbing ────────────────────────────────────────────────────────────────────────────────────────────────────

    private static bool Near(float a, float b, float tolerance = 1e-4f) => Math.Abs(a - b) <= tolerance;

    private static void Run(Action test)
    {
        try
        {
            test();
        }
        catch (Exception ex)
        {
            Assert(false, $"{test.Method.Name} threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void Assert(bool condition, string testName, string detail = null)
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  [PASS] {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  [FAIL] {testName}" + (string.IsNullOrEmpty(detail) ? "" : " :: " + detail));
        }
    }
}
}
