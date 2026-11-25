#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Tests.Scenarios
{
/// <summary>
/// Batch 15 step 1 (docs/designs/npc_spellcasting_batch15.md, "The caster soak"): the soak's appended NPC columns
/// (<see cref="SoakStats.NpcColumns"/>).
/// <list type="bullet">
/// <item>A job's "npc" record counts only Enemy-team actors (and a not-yet-keyed caster with an Enemy actor's name):
/// casts, casts by level, the first cast round, the legality refusals and the casts with no effect (split at each
/// caster's begin stage, once per cast, not per buff step) of its cast stages, the Flaming Sphere lines whose mover is an
/// Enemy actor's whole name, and the live counts; the soak record's npcMaxTurns and the aggregate's dc3, byScenario and
/// mixedScenarios.</item>
/// <item>The old per-side counters and turns are the same with and without the NPC inputs.</item>
/// <item>The hash is untouched: building the soak record adds no hashed trace line and changes no trace hash, and no NPC
/// column name reaches the hashed lines.</item>
/// <item>soak-rows.tsv gains the eight columns only at its end: the first 12 columns are the old layout, an old 12-column
/// row still reads (without "npc"), a new row reads its NPC columns back, and the aggregate counts the old row apart.</item>
/// </list>
/// Either mode (static state only; a temporary directory for the rows file).
/// </summary>
public static class SoakStatsColumnsTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== SOAK STATS NPC COLUMNS TESTS (batch 15 step 1) ======");
        TestNpcRecord();
        TestOldCountersUnchanged();
        TestHashUntouched();
        TestRowsAppendOnly();
        TestByLevelText();
        Debug.Log($"====== Soak Stats Columns Results: {_passed} passed, {_failed} failed ======");
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

    /// <summary>A small fight: a party fighter p1 and two kobolds e1, e2; casts by both sides; cast stages by both.</summary>
    private static ScenarioTrace BuildTrace()
    {
        var trace = new ScenarioTrace(null, null);
        trace.Emit("actor").Set("key", "p1").Set("name", "Fighter").Set("team", CharacterTeam.Player);
        trace.Emit("actor").Set("key", "e1").Set("name", "Kobold Sorcerer 5").Set("team", CharacterTeam.Enemy);
        trace.Emit("actor").Set("key", "e2").Set("name", "Kobold").Set("team", CharacterTeam.Enemy);
        trace.Emit("round").Set("n", 1);
        trace.Emit("turn_start").Set("actor", "p1");
        trace.Emit("cast").Set("by", "p1").Set("spell", "magic_missile").Set("level", 1);
        trace.Emit("turn_start").Set("actor", "e1");
        trace.Emit("round").Set("n", 2);
        TraceEvent c1 = trace.Emit("cast").Set("by", "e1").Set("spell", "magic_missile").Set("level", 1);
        c1.Round = 2;
        trace.Emit("log").Set("text", "🔥 Kobold Sorcerer 5 rolls Flaming Sphere to (12, 9).").Round = 2;
        trace.Emit("log").Set("text", "Fighter rolls Flaming Sphere to (3, 4).").Round = 2;
        // A mover whose name only ends with an enemy's name is not that enemy.
        trace.Emit("log").Set("text", "🔥 Sir Kobold rolls Flaming Sphere to (1, 1).").Round = 2;
        TraceEvent c2 = trace.Emit("cast").Set("by", "e1").Set("spell", "flaming_sphere").Set("level", 2);
        c2.Round = 3;
        TraceEvent c3 = trace.Emit("cast").Set("by", "e2").Set("spell", "shield").Set("level", 1);
        c3.Round = 3;
        trace.Emit("end").Set("final", new List<JsonObj>
        {
            new JsonObj().Set("k", "p1").Set("hp", 10).Set("team", CharacterTeam.Player),
            new JsonObj().Set("k", "e1").Set("hp", 0).Set("st", "Dead").Set("team", CharacterTeam.Enemy),
            new JsonObj().Set("k", "e2").Set("hp", 5).Set("team", CharacterTeam.Enemy),
        });
        return trace;
    }

    /// <summary>
    /// The cast stages: e1 one refusal and one legal cast; e2 an area buff with no effect on either target (one no-effect
    /// cast, not two) and one with an effect on one of two (no count); the party's stages and an unknown unkeyed caster's
    /// (not counted); an unkeyed caster with an enemy's name (a summon before its first cast event: counted).
    /// </summary>
    private static List<ScenarioTrace.CastStageRecord> Stages() => new List<ScenarioTrace.CastStageRecord>
    {
        new ScenarioTrace.CastStageRecord("e1", "scorching_ray", CastStageTrace.Begin, "TryNPCPerformSpellCast"),
        new ScenarioTrace.CastStageRecord("e1", "scorching_ray", CastStageTrace.Legality, "out-of-range"),
        new ScenarioTrace.CastStageRecord("e1", "magic_missile", CastStageTrace.Begin, "TryNPCPerformSpellCast"),
        new ScenarioTrace.CastStageRecord("e1", "magic_missile", CastStageTrace.Legality, "ok"),
        new ScenarioTrace.CastStageRecord("e2", "shield", CastStageTrace.Begin, "TryNPCPerformAreaSpellCast"),
        new ScenarioTrace.CastStageRecord("e2", "shield", CastStageTrace.Buff, "no-effect Kobold"),
        new ScenarioTrace.CastStageRecord("e2", "shield", CastStageTrace.Buff, "no-effect Kobold Sorcerer 5"),
        new ScenarioTrace.CastStageRecord("e2", "bless", CastStageTrace.Begin, "TryNPCPerformAreaSpellCast"),
        new ScenarioTrace.CastStageRecord("e2", "bless", CastStageTrace.Buff, "no-effect Kobold"),
        new ScenarioTrace.CastStageRecord("e2", "bless", CastStageTrace.Buff, "applied Kobold Sorcerer 5"),
        new ScenarioTrace.CastStageRecord("p1", "magic_missile", CastStageTrace.Legality, "invalid-target"),
        new ScenarioTrace.CastStageRecord("name:Fighter", "shield", CastStageTrace.Buff, "no-effect Fighter"),
        new ScenarioTrace.CastStageRecord("name:Stray", "light", CastStageTrace.Legality, "invalid-target"),
        new ScenarioTrace.CastStageRecord("name:Kobold", "light", CastStageTrace.Legality, "invalid-target"),
    };

    private static int I(JsonObj o, string key) => o?.Get(key) is int i ? i : -1;

    private static void TestNpcRecord()
    {
        ScenarioTrace trace = BuildTrace();
        JsonObj soak = SoakStats.ForJob(trace.Events, Outcome.Victory, 3, Stages(), new SoakStats.NpcLive { SlotsLeftAtEnd = 4, ItemUses = 1 });
        JsonObj npc = soak.Get("npc") as JsonObj;
        Assert(npc != null, "Record: the soak record has an npc block");
        Assert(npc != null && npc.Select(kv => kv.Key).SequenceEqual(SoakStats.NpcColumns),
            "Record: the npc block holds the eight columns in order", npc != null ? string.Join(",", npc.Select(kv => kv.Key)) : "null");
        Assert(I(npc, "npcCasts") == 3, "Record: npcCasts counts the 3 enemy casts, not the party's", I(npc, "npcCasts").ToString());
        Assert(npc?.Get("npcCastsByLevel") as string == "1:2,2:1", "Record: npcCastsByLevel is 1:2,2:1", npc?.Get("npcCastsByLevel") as string);
        Assert(I(npc, "npcFirstCastRound") == 2, "Record: npcFirstCastRound is the round of the first enemy cast (2)", I(npc, "npcFirstCastRound").ToString());
        Assert(I(npc, "npcRefusedPicks") == 2, "Record: npcRefusedPicks counts the enemies' legality refusals, an unkeyed caster with an enemy's name included (not ok, not the party's or an unknown caster's)", I(npc, "npcRefusedPicks").ToString());
        Assert(I(npc, "npcNoEffectCasts") == 1, "Record: npcNoEffectCasts counts casts, not buff steps: the area buff with no effect on either target once, the one with an effect not at all", I(npc, "npcNoEffectCasts").ToString());
        Assert(I(npc, "npcDirectEffects") == 1, "Record: npcDirectEffects counts the enemy's Flaming Sphere line (icon stripped), not the party's or a name that only ends with an enemy's", I(npc, "npcDirectEffects").ToString());
        Assert(I(soak, "npcMaxTurns") == 1, "Record: npcMaxTurns (the DC3 measure) is the most turns an enemy started (1)", I(soak, "npcMaxTurns").ToString());
        Assert(I(npc, "npcSlotsLeftAtEnd") == 4 && I(npc, "npcItemUses") == 1, "Record: the live counts are copied");

        JsonObj bare = SoakStats.ForJob(trace.Events, Outcome.Victory, 3)?.Get("npc") as JsonObj;
        Assert(bare != null && I(bare, "npcCasts") == 3 && I(bare, "npcRefusedPicks") == 0 && I(bare, "npcSlotsLeftAtEnd") == 0,
            "Record: without cast stages or live counts those columns are 0");
    }

    private static void TestOldCountersUnchanged()
    {
        ScenarioTrace trace = BuildTrace();
        JsonObj with = SoakStats.ForJob(trace.Events, Outcome.Victory, 3, Stages(), new SoakStats.NpcLive { SlotsLeftAtEnd = 9, ItemUses = 2 });
        JsonObj without = SoakStats.ForJob(trace.Events, Outcome.Victory, 3);
        Assert(Json.Serialize(with.Get("sides")) == Json.Serialize(without.Get("sides")), "Counters: the per-side counters do not depend on the NPC inputs");
        Assert(I(with, "turns") == 2 && I(without, "turns") == 2, "Counters: turns unchanged (2)");
        JsonObj enemy = (with.Get("sides") as JsonObj)?.Get("Enemy") as JsonObj;
        Assert(enemy != null && I(enemy, "casts") == 3 && I(enemy, "actors") == 2, "Counters: the Enemy side still counts its 3 casts and 2 actors");
    }

    private static void TestHashUntouched()
    {
        ScenarioTrace trace = BuildTrace();
        List<string> before = trace.HashLines();
        string hashBefore = ScenarioTrace.Hash(before);
        JsonObj soak = SoakStats.ForJob(trace.Events, Outcome.Victory, 3, Stages(), new SoakStats.NpcLive { SlotsLeftAtEnd = 4, ItemUses = 1 });
        var result = new JobResult { Id = "soak/test", Seed = 1, Rep = 1, Outcome = Outcome.Victory, Verdict = Verdict.Pass, Rounds = 3, Soak = soak };
        JsonObj agg = SoakStats.Aggregate(new List<JobResult> { result });
        List<string> after = trace.HashLines();
        JsonObj dc3 = (agg.Get("npc") as JsonObj)?.Get("dc3") as JsonObj;
        Assert(dc3 != null && I(dc3, "jobsMeasured") == 1 && I(dc3, "endedBeforeNpcSecondTurn") == 1,
            "Aggregate: dc3 counts the job whose enemies started no second turn", dc3 != null ? Json.Serialize(dc3) : "null");
        Assert((agg.Get("byScenario") as JsonObj)?.Count() == 1 && agg.Get("mixedScenarios") == null,
            "Aggregate: one scenario id, not marked mixed");
        var other = new JobResult { Id = "soak/other", Seed = 1, Rep = 1, Outcome = Outcome.Defeat, Verdict = Verdict.Pass, Rounds = 2, Soak = soak };
        JsonObj mixed = SoakStats.Aggregate(new List<JobResult> { result, other });
        var perSeed = mixed.Get("perSeed") as List<JsonObj>;
        Assert(mixed.Get("mixedScenarios") is bool m && m && (mixed.Get("byScenario") as JsonObj)?.Count() == 2
               && perSeed != null && perSeed.Count == 2 && perSeed.All(r => r.Get("id") is string),
            "Aggregate: a run of two soak ids is marked mixed, with jobs per id and the id on each per-seed row");
        Assert(after.SequenceEqual(before), "Hash: building and aggregating the soak record adds no hashed trace line");
        Assert(ScenarioTrace.Hash(after) == hashBefore, "Hash: the trace hash is unchanged");
        Assert(after.All(l => SoakStats.NpcColumns.All(c => !l.Contains(c))), "Hash: no NPC column name is in the hashed lines");
    }

    private static void TestRowsAppendOnly()
    {
        ScenarioTrace trace = BuildTrace();
        JsonObj soak = SoakStats.ForJob(trace.Events, Outcome.Victory, 3, Stages(), new SoakStats.NpcLive { SlotsLeftAtEnd = 4, ItemUses = 1 });
        var generated = new JsonObj().Set("name", "kobold sorcerer").Set("dungeonLevel", 5).Set("count", 1);
        var result = new JobResult { Id = "soak/test", Seed = 7, Rep = 1, Outcome = Outcome.Victory, Verdict = Verdict.Pass, Rounds = 3, WallSec = 1.25f, Soak = soak, Generated = generated };

        string dir = Path.Combine(Path.GetTempPath(), "soakstats_columns_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            SoakStats.WriteRows(dir, new[] { result });
            string[] lines = File.ReadAllLines(Path.Combine(dir, SoakStats.RowsFile));
            string[] f = lines.Length == 1 ? lines[0].Split('\t') : new string[0];
            Assert(f.Length == SoakStats.BaseRowColumns + SoakStats.NpcColumns.Length, "Rows: one line of 12 + 8 columns", f.Length.ToString());
            Assert(f.Length >= 12 && f[0] == "soak/test" && f[1] == "7" && f[3] == "Victory" && f[5] == "3" && f[6] == "2" && f[7] == "1.25"
                   && f[8] == "kobold sorcerer" && f[9] == "5" && f[10] == "1" && f[11].StartsWith("Enemy=actors:2", StringComparison.Ordinal),
                "Rows: the first 12 columns keep the old layout", string.Join(" | ", f.Take(12)));
            Assert(f.Length == 20 && string.Join("|", f.Skip(12)) == "3|1:2,2:1|4|2|2|1|1|1",
                "Rows: the NPC columns come last, in NpcColumns order", string.Join("|", f.Skip(12)));

            MethodInfo fromRow = typeof(SoakStats).GetMethod("FromRow", BindingFlags.NonPublic | BindingFlags.Static);
            Assert(fromRow != null, "Rows: SoakStats.FromRow exists");
            if (fromRow == null)
                return;
            var back = fromRow.Invoke(null, new object[] { lines[0] }) as JobResult;
            JsonObj npc = back?.Soak?.Get("npc") as JsonObj;
            Assert(npc != null && I(npc, "npcCasts") == 3 && npc.Get("npcCastsByLevel") as string == "1:2,2:1" && I(npc, "npcSlotsLeftAtEnd") == 4
                   && I(npc, "npcFirstCastRound") == 2 && I(npc, "npcDirectEffects") == 1,
                "Rows: a new row reads its NPC columns back");
            string oldRow = string.Join("\t", f.Take(SoakStats.BaseRowColumns));
            var old = fromRow.Invoke(null, new object[] { oldRow }) as JobResult;
            Assert(old != null && old.Seed == 7 && old.Rounds == 3 && old.Soak?.Get("npc") == null
                   && Json.Serialize(old.Soak.Get("sides")) == Json.Serialize(back?.Soak?.Get("sides")),
                "Rows: an old 12-column row still reads, its sides the same, with no npc record");
            JsonObj agg = SoakStats.Aggregate(new List<JobResult> { back, old });
            JsonObj an = agg.Get("npc") as JsonObj;
            Assert(an != null && I(an, "jobs") == 1 && I(an, "jobsWithoutNpcColumns") == 1 && (an.Get("totals") as JsonObj)?.Get("npcCasts") is long casts && casts == 3,
                "Rows: the aggregate counts the old row apart and sums the new one", an != null ? Json.Serialize(an) : "null");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (Exception) { }
        }
    }

    private static void TestByLevelText()
    {
        var d = new SortedDictionary<int, int> { { 2, 1 }, { 0, 3 }, { 1, 2 } };
        string text = SoakStats.FormatByLevel(d);
        Assert(text == "0:3,1:2,2:1", "ByLevel: ascending levels", text);
        SortedDictionary<int, int> back = SoakStats.ParseByLevel(text);
        Assert(back.Count == 3 && back[0] == 3 && back[1] == 2 && back[2] == 1, "ByLevel: parses back");
        Assert(SoakStats.FormatByLevel(new SortedDictionary<int, int>()) == "" && SoakStats.ParseByLevel("").Count == 0, "ByLevel: none is empty");
    }
}
}
#endif
