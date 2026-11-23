#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Tests.AI
{
/// <summary>
/// Batch 14 step 1 (docs/designs/personalities_batch14.md "Trace and decision timing"; owner request 2026-10-30): the AI
/// decision timer, on a fake clock.
/// <list type="bullet">
/// <item>Exclusive nesting: while an inner scope is open the outer scope's clock stops, so each tick lands in one bucket.</item>
/// <item>Turns are keyed by actor: two open turns (CORE-012 re-entry) keep their own time; a scope naming an actor with
/// no open turn charges no turn.</item>
/// <item>A second BeginTurn of an open actor closes the first as unclosed; FlushOpenTurns closes the rest.</item>
/// <item>Off, the timer is a no-op: no turn, no time, an inert scope.</item>
/// <item>The timed enumerable times each step of the walk but not the consumer's work between items.</item>
/// <item>Nearest-rank percentiles and the 50 ms count.</item>
/// </list>
/// Either mode (static state only).
/// </summary>
public static class AiDecisionTimerTests
{
    private static int _passed;
    private static int _failed;
    private static long _now;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== AI DECISION TIMER TESTS (batch 14 step 1) ======");

        Func<long> clock = AiDecisionTimer.Clock;
        double ticksPerMs = AiDecisionTimer.TicksPerMs;
        try
        {
            AiDecisionTimer.Clock = () => _now;
            AiDecisionTimer.TicksPerMs = 1.0; // one fake tick is one millisecond
            AiDecisionTimer.OverrideEnabled(true);

            TestExclusiveNesting();
            TestTurnsKeyedByActor();
            TestStaleAndFlushedTurns();
            TestTimedEnumerable();
            TestDisabledNoOp();
            TestPercentiles();
        }
        finally
        {
            AiDecisionTimer.Clock = clock;
            AiDecisionTimer.TicksPerMs = ticksPerMs;
            AiDecisionTimer.OverrideEnabled(null);
            AiDecisionTimer.Reset();
        }

        Debug.Log($"====== AI Decision Timer Results: {_passed} passed, {_failed} failed ======");
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
            Debug.LogError($"  [FAIL] {testName}" + (string.IsNullOrEmpty(detail) ? "" : " (" + detail + ")"));
        }
    }

    private static bool Near(double a, double b) => Math.Abs(a - b) < 1e-9;

    private static void TestExclusiveNesting()
    {
        AiDecisionTimer.Reset();
        _now = 0;
        var actor = new object();
        AiDecisionTimer.BeginTurn(actor, "A", 1);
        using (AiDecisionTimer.Open(AiTimeBucket.Action, actor))
        {
            _now += 3;                                     // 3 in Action
            using (AiDecisionTimer.Open(AiTimeBucket.Target, actor))
            {
                _now += 5;                                 // 5 in Target
                using (AiDecisionTimer.Open(AiTimeBucket.Movement))
                    _now += 7;                             // 7 in Movement (no actor: the innermost scope's turn)
                _now += 1;                                 // 1 more in Target
            }
            _now += 2;                                     // 2 more in Action
        }
        _now += 100;                                       // outside every scope: not decision time
        AiDecisionTimer.TurnSample t = AiDecisionTimer.EndTurn(actor);

        Assert(t != null, "Exclusive: EndTurn returns the turn");
        if (t == null)
            return;
        Assert(Near(t.BucketMs(AiTimeBucket.Action), 5), "Exclusive: the outer Action scope keeps only its own 5 ms", t.BucketMs(AiTimeBucket.Action).ToString());
        Assert(Near(t.BucketMs(AiTimeBucket.Target), 6), "Exclusive: the nested Target scope keeps its 6 ms", t.BucketMs(AiTimeBucket.Target).ToString());
        Assert(Near(t.BucketMs(AiTimeBucket.Movement), 7), "Exclusive: an actor-less scope charges the innermost scope's turn", t.BucketMs(AiTimeBucket.Movement).ToString());
        Assert(Near(t.Ms, 18), "Exclusive: the turn total is the sum of the buckets, nothing counted twice and no time outside a scope", t.Ms.ToString());
        Assert(!t.Unclosed && t.Actor == "A" && t.Round == 1, "Exclusive: the turn keeps its actor and round and is closed");
        Assert(AiDecisionTimer.Completed.Count == 1 && AiDecisionTimer.OpenTurns == 0, "Exclusive: one completed turn, none open");
    }

    private static void TestTurnsKeyedByActor()
    {
        AiDecisionTimer.Reset();
        _now = 0;
        var a = new object();
        var b = new object();
        var stranger = new object();
        AiDecisionTimer.BeginTurn(a, "A", 2);
        using (AiDecisionTimer.Open(AiTimeBucket.Target, a))
            _now += 4;
        AiDecisionTimer.BeginTurn(b, "B", 2);              // re-entrant: B's turn starts before A's ends
        using (AiDecisionTimer.Open(AiTimeBucket.Spell, b))
            _now += 10;
        using (AiDecisionTimer.Open(AiTimeBucket.Target, a))
            _now += 1;
        using (AiDecisionTimer.Open(AiTimeBucket.LegalSet, stranger))
            _now += 50;                                    // an actor with no open turn: charged to no turn
        AiDecisionTimer.TurnSample tb = AiDecisionTimer.EndTurn(b);
        AiDecisionTimer.TurnSample ta = AiDecisionTimer.EndTurn(a);

        Assert(ta != null && Near(ta.Ms, 5) && Near(ta.BucketMs(AiTimeBucket.Target), 5), "Keyed: A keeps its own 5 ms across B's turn", ta != null ? ta.Ms.ToString() : "null");
        Assert(tb != null && Near(tb.Ms, 10) && Near(tb.BucketMs(AiTimeBucket.Spell), 10), "Keyed: B keeps its own 10 ms", tb != null ? tb.Ms.ToString() : "null");
        Assert(Near(AiDecisionTimer.OutsideMs(AiTimeBucket.LegalSet), 50), "Keyed: a scope for an actor with no open turn charges no turn");
        Assert(AiDecisionTimer.EndTurn(a) == null, "Keyed: a second EndTurn finds nothing");
        Assert(AiDecisionTimer.Completed.Count == 2 && AiDecisionTimer.Completed[0].Actor == "B", "Keyed: turns complete in the order they end");

        // A cancelled turn (a scenario-scripted turn makes no AI decision) is dropped, never completed.
        AiDecisionTimer.BeginTurn(a, "A", 3);
        AiDecisionTimer.CancelTurn(a);
        Assert(AiDecisionTimer.OpenTurns == 0 && AiDecisionTimer.EndTurn(a) == null && AiDecisionTimer.Completed.Count == 2,
            "Keyed: a cancelled turn is dropped, not completed");
    }

    private static void TestStaleAndFlushedTurns()
    {
        AiDecisionTimer.Reset();
        _now = 0;
        var a = new object();
        var b = new object();
        AiDecisionTimer.BeginTurn(a, "A", 1);
        using (AiDecisionTimer.Open(AiTimeBucket.Action, a))
            _now += 2;
        AiDecisionTimer.BeginTurn(a, "A", 2);              // its coroutine was stopped: the round 1 turn is closed as unclosed
        AiDecisionTimer.BeginTurn(b, "B", 2);
        using (AiDecisionTimer.Open(AiTimeBucket.Action, b))
            _now += 3;
        AiDecisionTimer.FlushOpenTurns();

        IReadOnlyList<AiDecisionTimer.TurnSample> done = AiDecisionTimer.Completed;
        Assert(done.Count == 3 && AiDecisionTimer.OpenTurns == 0, "Stale: the stale turn and both flushed turns complete", done.Count.ToString());
        Assert(done.Count == 3 && done[0].Round == 1 && done[0].Unclosed && Near(done[0].Ms, 2), "Stale: a second BeginTurn closes the first as unclosed with its time");
        Assert(done.All(t => t.Unclosed), "Stale: flushed turns are marked unclosed");

        // A scope still open across a reset is inert afterwards.
        AiDecisionTimer.BeginTurn(a, "A", 3);
        AiDecisionTimer.Scope s = AiDecisionTimer.Open(AiTimeBucket.Target, a);
        AiDecisionTimer.Reset();
        _now += 9;
        s.Dispose();
        Assert(AiDecisionTimer.Completed.Count == 0 && AiDecisionTimer.OpenTurns == 0 && Near(AiDecisionTimer.OutsideMs(AiTimeBucket.Target), 0),
            "Stale: a scope opened before a reset charges nothing when it closes");
    }

    private static void TestTimedEnumerable()
    {
        AiDecisionTimer.Reset();
        _now = 0;
        var a = new object();
        AiDecisionTimer.BeginTurn(a, "A", 1);
        var seen = new List<int>();
        foreach (int i in AiDecisionTimer.Timed(Walk(), AiTimeBucket.Spell, a))
        {
            seen.Add(i);
            _now += 100;                                   // the consumer acts (a cast): not decision time
        }
        AiDecisionTimer.TurnSample t = AiDecisionTimer.EndTurn(a);
        Assert(seen.SequenceEqual(new[] { 1, 2, 3 }), "Timed: the items and their order are unchanged");
        Assert(t != null && Near(t.BucketMs(AiTimeBucket.Spell), 4) && Near(t.Ms, 4), "Timed: only the walk's 4 ms are timed, not the consumer's 300", t != null ? t.Ms.ToString() : "null");
    }

    private static IEnumerable<int> Walk()
    {
        _now += 1;
        yield return 1;
        _now += 1;
        yield return 2;
        _now += 1;
        yield return 3;
        _now += 1;                                         // the step that ends the walk
    }

    private static void TestDisabledNoOp()
    {
        AiDecisionTimer.Reset();
        AiDecisionTimer.OverrideEnabled(false);
        try
        {
            _now = 0;
            var a = new object();
            AiDecisionTimer.BeginTurn(a, "A", 1);
            AiDecisionTimer.Scope s = AiDecisionTimer.Open(AiTimeBucket.Target, a);
            _now += 20;
            s.Dispose();
            IEnumerable<int> src = Walk();
            Assert(ReferenceEquals(AiDecisionTimer.Timed(src, AiTimeBucket.Spell, a), src), "Disabled: Timed returns the source itself");
            Assert(AiDecisionTimer.EndTurn(a) == null && AiDecisionTimer.OpenTurns == 0 && AiDecisionTimer.Completed.Count == 0,
                "Disabled: BeginTurn opens no turn and nothing completes");
            Assert(Near(AiDecisionTimer.OutsideMs(AiTimeBucket.Target), 0), "Disabled: a scope charges nothing");
            default(AiDecisionTimer.Scope).Dispose();
            Assert(true, "Disabled: the default scope disposes safely");
        }
        finally
        {
            AiDecisionTimer.OverrideEnabled(true);
        }
    }

    private static void TestPercentiles()
    {
        var ms = new List<double> { 10, 1, 9, 2, 8, 3, 7, 4, 6, 5 };
        Assert(Near(AiDecisionTimer.Percentile(ms, 50), 5), "Percentile: p50 of 1..10 is 5 (nearest rank)");
        Assert(Near(AiDecisionTimer.Percentile(ms, 95), 10), "Percentile: p95 of 1..10 is 10 (nearest rank)");
        Assert(Near(AiDecisionTimer.Percentile(ms, 0), 1) && Near(AiDecisionTimer.Percentile(ms, 100), 10), "Percentile: p0 is the minimum, p100 the maximum");
        Assert(Near(AiDecisionTimer.Percentile(new List<double>(), 95), 0), "Percentile: an empty set gives 0");
        Assert(ms[0] == 10 && ms[1] == 1, "Percentile: the input list is not reordered");

        var many = Enumerable.Range(1, 100).Select(i => (double)i).ToList();
        many.Add(51.5);
        many.Add(250);
        AiDecisionTimer.Stats st = AiDecisionTimer.Summarize(many);
        Assert(st.Turns == 102 && Near(st.Max, 250), "Summarize: the count and the maximum");
        Assert(st.Over == 52, "Summarize: turns over the 50 ms budget are counted (51..100, 51.5 and 250)", st.Over.ToString());
        Assert(Near(st.P50, 51) && Near(st.P95, 96), "Summarize: p50 and p95 by nearest rank", st.P50 + " / " + st.P95);
        AiDecisionTimer.Stats empty = AiDecisionTimer.Summarize(new List<double>());
        Assert(empty.Turns == 0 && Near(empty.Max, 0), "Summarize: an empty set");
    }
}
}
#endif
