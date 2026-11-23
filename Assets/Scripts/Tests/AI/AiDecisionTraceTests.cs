#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Tests.Scenarios;
using UnityEngine;

namespace Tests.AI
{
/// <summary>
/// Batch 14 step 1 (docs/designs/personalities_batch14.md "Trace and decision timing"): the AI decision trace v1.
/// <list type="bullet">
/// <item>One record per turn, keyed by actor (re-entrant turns do not mix); a decision outside an open turn is dropped.</item>
/// <item>The ring keeps the last <see cref="AiDecisionTrace.RingSize"/> turns, oldest first; the counts per "site:
/// verdict" keep every decision ("legacy" when the choice is the legacy one, else "changed").</item>
/// <item>Drain returns the ring and empties it; the counts stay until Clear.</item>
/// <item>The collapsed line names the round, actor, Role, traits and decisions.</item>
/// <item>The hash is untouched: recording turns, with AIService.TraceEnabled on, adds nothing to a ScenarioTrace's
/// HashLines or its hash.</item>
/// <item>Off, the trace records nothing.</item>
/// </list>
/// Either mode (static state only).
/// </summary>
public static class AiDecisionTraceTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== AI DECISION TRACE TESTS (batch 14 step 1) ======");

        bool traceEnabled = AIService.TraceEnabled;
        try
        {
            AiDecisionTrace.OverrideEnabled(true);
            AIService.TraceEnabled = false;
            TestRecordsPerTurn();
            TestRing();
            TestDrain();
            TestCollapsedLine();
            TestHashUntouched();
            TestDisabled();
        }
        finally
        {
            AiDecisionTrace.OverrideEnabled(null);
            AIService.TraceEnabled = traceEnabled;
            AiDecisionTrace.Clear();
        }

        Debug.Log($"====== AI Decision Trace Results: {_passed} passed, {_failed} failed ======");
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

    private static void TestRecordsPerTurn()
    {
        AiDecisionTrace.Clear();
        var a = new object();
        var b = new object();
        AiDecisionTrace.Record(a, "Target", "Fighter", "Fighter", 0f, null); // no open turn: dropped
        AiDecisionTrace.BeginTurn(a, "Goblin 1", 2, "Default AI", "");
        AiDecisionTrace.Record(a, "Target", "Fighter", "Fighter", 0f, null);
        AiDecisionTrace.BeginTurn(b, "Orc 1", 2, "Aggressive", "Cowardly:0.6");
        AiDecisionTrace.Record(b, "Target", "Wizard", "Fighter", 12.5f, "Cowardly");
        AiDecisionTrace.Record(a, "Action", "Attack", "Attack", 0f, null);
        AiDecisionTrace.TurnRecord rb = AiDecisionTrace.EndTurn(b);
        AiDecisionTrace.TurnRecord ra = AiDecisionTrace.EndTurn(a);

        Assert(ra != null && ra.Decisions.Count == 2 && ra.Decisions[0].Site == "Target" && ra.Decisions[1].Site == "Action",
            "Per turn: A keeps its two decisions in order across B's turn");
        Assert(rb != null && rb.Decisions.Count == 1 && rb.Decisions[0].Verdict == "changed" && rb.Traits == "Cowardly:0.6",
            "Per turn: B keeps its own decision, its verdict and its traits");
        Assert(ra != null && ra.Round == 2 && ra.Actor == "Goblin 1" && ra.Role == "Default AI", "Per turn: the record keeps round, actor and Role");
        IReadOnlyDictionary<string, int> counts = AiDecisionTrace.Counts;
        Assert(counts.TryGetValue("Target: legacy", out int tl) && tl == 1 && counts.TryGetValue("Target: changed", out int tc) && tc == 1
            && counts.TryGetValue("Action: legacy", out int al) && al == 1 && counts.Count == 3,
            "Per turn: counts per site and verdict, the dropped decision not counted");
        Assert(AiDecisionTrace.TurnsRecorded == 2 && AiDecisionTrace.OpenTurns == 0 && AiDecisionTrace.EndTurn(a) == null,
            "Per turn: two finished turns, none open, a second EndTurn finds nothing");

        AiDecisionTrace.BeginTurn(a, "Goblin 1", 3, "Default AI", "");
        AiDecisionTrace.CancelTurn(a);
        Assert(AiDecisionTrace.OpenTurns == 0 && AiDecisionTrace.EndTurn(a) == null && AiDecisionTrace.TurnsRecorded == 2,
            "Per turn: a cancelled (scripted) turn is dropped unfinished");
    }

    private static void TestRing()
    {
        AiDecisionTrace.Clear();
        var a = new object();
        int total = AiDecisionTrace.RingSize + 7;
        for (int i = 1; i <= total; i++)
        {
            AiDecisionTrace.BeginTurn(a, "A", i, "Role", "");
            AiDecisionTrace.Record(a, "Action", "Attack", "Attack", 0f, null);
            AiDecisionTrace.EndTurn(a);
        }
        IReadOnlyList<AiDecisionTrace.TurnRecord> ring = AiDecisionTrace.Ring;
        Assert(ring.Count == AiDecisionTrace.RingSize, "Ring: keeps the last RingSize turns", ring.Count.ToString());
        Assert(ring.Count > 0 && ring[0].Round == 8 && ring[ring.Count - 1].Round == total, "Ring: oldest first, the first 7 dropped");
        Assert(AiDecisionTrace.TurnsRecorded == total && AiDecisionTrace.Counts["Action: legacy"] == total, "Ring: the counts keep every turn's decisions");

        // A second BeginTurn of an open actor finishes the first record into the ring.
        AiDecisionTrace.BeginTurn(a, "A", 100, "Role", "");
        AiDecisionTrace.BeginTurn(a, "A", 101, "Role", "");
        AiDecisionTrace.EndTurn(a);
        ring = AiDecisionTrace.Ring;
        Assert(ring[ring.Count - 2].Round == 100 && ring[ring.Count - 1].Round == 101, "Ring: a stale record is finished before the new turn");
    }

    private static void TestDrain()
    {
        AiDecisionTrace.Clear();
        var a = new object();
        for (int i = 1; i <= 3; i++)
        {
            AiDecisionTrace.BeginTurn(a, "A", i, "Role", "");
            AiDecisionTrace.Record(a, "Target", "X", "X", 0f, null);
            AiDecisionTrace.EndTurn(a);
        }
        List<AiDecisionTrace.TurnRecord> drained = AiDecisionTrace.Drain();
        Assert(drained.Count == 3 && drained[0].Round == 1 && drained[2].Round == 3, "Drain: returns the ring, oldest first");
        Assert(AiDecisionTrace.Ring.Count == 0 && AiDecisionTrace.Drain().Count == 0, "Drain: the ring is empty afterwards");
        Assert(AiDecisionTrace.Counts["Target: legacy"] == 3, "Drain: the counts stay until Clear");
        AiDecisionTrace.Clear();
        Assert(AiDecisionTrace.Counts.Count == 0 && AiDecisionTrace.TurnsRecorded == 0, "Drain: Clear empties the counts");
    }

    private static void TestCollapsedLine()
    {
        var t = new AiDecisionTrace.TurnRecord { Round = 3, Actor = "Orc 1", Role = "Aggressive", Traits = "Cowardly:0.6" };
        t.Decisions.Add(new AiDecisionTrace.Decision { Site = "Target", Chosen = "Wizard", Legacy = "Fighter", Bonus = 12.5f, Reason = "Cowardly" });
        t.Decisions.Add(new AiDecisionTrace.Decision { Site = "Action", Chosen = "Attack", Legacy = "Attack" });
        string line = t.Collapsed();
        Assert(line == "[AI-TRACE] r3 Orc 1 (Aggressive; Cowardly:0.6) Target=Wizard (legacy Fighter) +12.5 [Cowardly]; Action=Attack",
            "Collapsed: one line with round, actor, Role, traits and decisions", line);
    }

    private static void TestHashUntouched()
    {
        AiDecisionTrace.Clear();
        var trace = new ScenarioTrace(null, null);
        trace.Emit("round").Set("n", 1);
        trace.Emit("attack").Set("actor", "a:Goblin").Set("target", "p:Fighter").Set("hit", true);
        List<string> before = trace.HashLines();
        string hashBefore = ScenarioTrace.Hash(before);

        AIService.TraceEnabled = true; // the collapsed Debug.Log line too
        try
        {
            var a = new object();
            for (int i = 1; i <= 5; i++)
            {
                AiDecisionTrace.BeginTurn(a, "Goblin", i, "Default AI", "");
                AiDecisionTrace.Record(a, "Target", "Fighter", "Fighter", 0f, null);
                AiDecisionTrace.Record(a, "Action", "Attack", "Charge", 3f, "test");
                AiDecisionTrace.EndTurn(a);
            }
        }
        finally
        {
            AIService.TraceEnabled = false;
        }

        List<string> after = trace.HashLines();
        Assert(after.Count == before.Count && after.SequenceEqual(before), "Hash: recording AI turns adds no hashed trace line");
        Assert(ScenarioTrace.Hash(after) == hashBefore, "Hash: the trace hash is unchanged");
        Assert(after.All(l => !l.Contains("AI-TRACE")), "Hash: no [AI-TRACE] text in the hashed lines");
    }

    private static void TestDisabled()
    {
        AiDecisionTrace.Clear();
        AiDecisionTrace.OverrideEnabled(false);
        try
        {
            var a = new object();
            AiDecisionTrace.BeginTurn(a, "A", 1, "Role", "");
            AiDecisionTrace.Record(a, "Target", "X", "X", 0f, null);
            Assert(AiDecisionTrace.EndTurn(a) == null && AiDecisionTrace.TurnsRecorded == 0 && AiDecisionTrace.Counts.Count == 0,
                "Disabled: no turn opens and nothing is recorded");
        }
        finally
        {
            AiDecisionTrace.OverrideEnabled(true);
        }
    }
}
}
#endif
