#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;

namespace Tests.Scenarios
{
    /// <summary>
    /// Harness self-tests: boot, spawn, placement, start, turn flow, end detection and reset. NPC ids checked
    /// through NPCDatabase.Get on 2026-10-07 ("goblin" is the 1-HD goblin of the test_2_goblins preset).
    /// </summary>
    public static class SmokeScenarios
    {
        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return ScenarioCatalog.Safe("SmokeScenarios smoke/boot", Boot);
            yield return ScenarioCatalog.Safe("SmokeScenarios smoke/two-goblins-ai", () => TwoGoblinsAi("smoke/two-goblins-ai", "Quick Start party (AI-run) vs 2 goblins (AI), to the end", 20));
            yield return ScenarioCatalog.Safe("SmokeScenarios smoke/soak-mini", () => TwoGoblinsAi("smoke/soak-mini", "Mini soak: the two-goblin fight, meant for a seed range (1-3)", 20, "soak"));
        }

        private static ScenarioBuilder Party(ScenarioBuilder b, Control control)
            => b.Pc("fighter", ActorSource.QuickStart("Fighter"), 6, 9, control)
                .Pc("rogue", ActorSource.QuickStart("Rogue"), 6, 11, control)
                .Pc("cleric", ActorSource.QuickStart("Cleric"), 4, 9, control)
                .Pc("wizard", ActorSource.QuickStart("Wizard"), 4, 11, control);

        private static ScenarioDef Boot()
        {
            ScenarioBuilder b = Scenario.Define("smoke/boot", "Boot, spawn, place, start, one idle round, halt and reset")
                .Tags("smoke")
                .MaxRounds(1);
            Party(b, Control.Idle)
                .Npc("goblin1", "goblin", 12, 9, Control.Idle)
                .Npc("goblin2", "goblin", 12, 11, Control.Idle)
                .Expect("The fight is halted as a stalemate when round 2 begins", Expect.Outcome(Outcome.Stalemate))
                .Expect("All six actors are traced at their squares", v =>
                {
                    var actors = v.Of("actor").ToList();
                    var expected = new Dictionary<string, (int x, int y)>
                    {
                        { "fighter", (6, 9) }, { "rogue", (6, 11) }, { "cleric", (4, 9) }, { "wizard", (4, 11) },
                        { "goblin1", (12, 9) }, { "goblin2", (12, 11) }
                    };
                    foreach (KeyValuePair<string, (int x, int y)> kv in expected)
                    {
                        TraceEvent a = actors.FirstOrDefault(e => e.Str("key") == kv.Key);
                        if (a == null)
                            return ExpectResult.Fail("no actor line for " + kv.Key);
                        if (!(a.Get("pos") is UnityEngine.Vector2Int p) || p.x != kv.Value.x || p.y != kv.Value.y)
                            return ExpectResult.Fail(kv.Key + " at " + a.Get("pos") + ", expected " + kv.Value, a.Seq);
                    }
                    return ExpectResult.Pass(actors.Count + " actors");
                })
                .Expect("Initiative lists every actor and each takes one idle turn in round 1",
                    Expect.All(
                        Expect.Count("init", e => e.Int("count") == 6, 1, 1),
                        Expect.Count("turn_start", e => e.Round == 1 && e.Str("controller") == "idle", 6, 6),
                        Expect.None("attack", null),
                        Expect.None("move", e => e.Str("type") == "move" || e.Str("type") == "path-move")));
            return b.Build();
        }

        private static ScenarioDef TwoGoblinsAi(string id, string title, int maxRounds, string extraTag = null)
        {
            ScenarioBuilder b = Scenario.Define(id, title).Tags("smoke").MaxRounds(maxRounds);
            if (extraTag != null)
                b.Tags(extraTag);
            Party(b, Control.Ai)
                .Npc("goblin1", "goblin", 12, 9)
                .Npc("goblin2", "goblin", 12, 11)
                .Expect("The game ends the fight itself when one side is out (CORE-011: AI-run killers trigger victory too)",
                    Expect.GameDetectedEnd())
                .Expect("Every turn of a party member is an AI turn (PC-slot actors run on the NPC path)",
                    v =>
                    {
                        var pcs = new HashSet<string> { "fighter", "rogue", "cleric", "wizard" };
                        var turns = v.Of("turn_start").Where(e => pcs.Contains(e.Str("actor"))).ToList();
                        if (turns.Count == 0)
                            return ExpectResult.Fail("no party turns");
                        TraceEvent bad = turns.FirstOrDefault(e => e.Str("controller") != "ai");
                        return bad == null ? ExpectResult.Pass(turns.Count + " AI turns") : ExpectResult.Fail("controller " + bad.Str("controller"), bad.Seq);
                    })
                .Expect("Somebody attacks and damage is traced", Expect.All(Expect.Count("attack", null, 1), Expect.Count("hp", null, 1)));
            return b.Build();
        }
    }
}
#endif
