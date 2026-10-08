#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Tests.Scenarios
{
    /// <summary>
    /// Per-job and per-run statistics for AI-vs-AI soak scenarios (tag "soak"): outcome, rounds, turns and, per team,
    /// how often each kind of action was taken. summary.json carries the per-run aggregate as "soak", the AI-depth
    /// baseline of docs/TESTING.md 3.5. Counts come from the trace: "attack" (every weapon attack roll, AoOs included),
    /// "aoo", "maneuver", "cast" (NPC-path casts begun; ScenarioHooks.SpellCast) and own "move"/"path-move" events.
    /// Each run with soak jobs also writes Logs/Scenarios/&lt;runId&gt;/soak-rows.tsv (one line per job), which
    /// <see cref="MergeRuns"/> reads back to aggregate a soak split over several fresh Play sessions.
    /// </summary>
    public static class SoakStats
    {
        public const string Tag = "soak";

        private static readonly string[] Counters =
            { "turns", "attacks", "hits", "aoos", "maneuvers", "maneuverHits", "casts", "moves", "squares" };

        /// <summary>The soak record of one job from its trace.</summary>
        public static JsonObj ForJob(IReadOnlyList<TraceEvent> events, Outcome outcome, int rounds)
        {
            var team = new Dictionary<string, string>();
            foreach (TraceEvent e in events)
                if (e.Ev == "actor" && e.Str("key") != null)
                    team[e.Str("key")] = Convert.ToString(e.Get("team"));

            var sides = new SortedDictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
            Dictionary<string, int> Side(string key)
            {
                string t = key != null && team.TryGetValue(key, out string v) ? v : "Unknown";
                if (!sides.TryGetValue(t, out Dictionary<string, int> d))
                {
                    d = Counters.ToDictionary(c => c, c => 0);
                    d["actors"] = 0;
                    d["downAtEnd"] = 0;
                    sides[t] = d;
                }
                return d;
            }

            int turns = 0;
            foreach (TraceEvent e in events)
            {
                switch (e.Ev)
                {
                    case "turn_start":
                        turns++;
                        Side(e.Str("actor"))["turns"]++;
                        break;
                    case "attack":
                    {
                        Dictionary<string, int> d = Side(e.Str("attacker"));
                        d["attacks"]++;
                        if (e.Bool("hit"))
                            d["hits"]++;
                        break;
                    }
                    case "aoo":
                        Side(e.Str("by"))["aoos"]++;
                        break;
                    case "maneuver":
                    {
                        Dictionary<string, int> d = Side(e.Str("by"));
                        d["maneuvers"]++;
                        if (e.Bool("success"))
                            d["maneuverHits"]++;
                        break;
                    }
                    case "cast":
                        Side(e.Str("by"))["casts"]++;
                        break;
                    case "move":
                    {
                        string type = e.Str("type");
                        if (type == "move" || type == "path-move")
                        {
                            Dictionary<string, int> d = Side(e.Str("actor"));
                            d["moves"]++;
                            d["squares"] += e.Int("sq");
                        }
                        break;
                    }
                }
            }

            // Who was in the fight and who was down at the end (the "end" event's final snapshots, summons included).
            TraceEvent end = events.LastOrDefault(e => e.Ev == "end");
            if (end != null && end.Get("final") is IEnumerable<JsonObj> finals)
            {
                foreach (JsonObj snap in finals)
                {
                    string key = snap.Get("k") as string;
                    if (key != null && !team.ContainsKey(key) && snap.Get("team") != null)
                        team[key] = Convert.ToString(snap.Get("team"));
                    Dictionary<string, int> d = Side(key);
                    d["actors"]++;
                    if (ScenarioChecks.IsDownSnapshot(snap))
                        d["downAtEnd"]++;
                }
            }

            var o = new JsonObj();
            o.Set("outcome", outcome).Set("rounds", rounds).Set("turns", turns);
            var bySide = new JsonObj();
            foreach (KeyValuePair<string, Dictionary<string, int>> kv in sides)
            {
                var so = new JsonObj();
                foreach (string c in new[] { "actors", "downAtEnd" }.Concat(Counters))
                    so.Set(c, kv.Value[c]);
                bySide.Set(kv.Key, so);
            }
            o.Set("sides", bySide);
            return o;
        }

        /// <summary>The run-level aggregate of the jobs that have a soak record.</summary>
        public static JsonObj Aggregate(IList<JobResult> results)
        {
            var o = new JsonObj();
            int n = results.Count;
            o.Set("jobs", n);

            var outcomes = new JsonObj();
            foreach (IGrouping<Outcome, JobResult> g in results.GroupBy(r => r.Outcome).OrderBy(g => g.Key.ToString(), StringComparer.Ordinal))
                outcomes.Set(g.Key.ToString(), g.Count());
            o.Set("outcomes", outcomes);
            var verdicts = new JsonObj();
            foreach (IGrouping<Verdict, JobResult> g in results.GroupBy(r => r.Verdict).OrderBy(g => g.Key.ToString(), StringComparer.Ordinal))
                verdicts.Set(g.Key.ToString(), g.Count());
            o.Set("verdicts", verdicts);

            if (n > 0)
            {
                o.Set("meanRounds", results.Average(r => (double)r.Rounds))
                 .Set("meanTurns", results.Average(r => (double)Int(r.Soak, "turns")))
                 .Set("meanWallSec", results.Average(r => (double)r.WallSec))
                 .Set("maxWallSec", results.Max(r => r.WallSec));
            }

            // Per team: totals, per-job means and per-turn rates.
            var teams = new SortedSet<string>(StringComparer.Ordinal);
            foreach (JobResult r in results)
                if (r.Soak.Get("sides") is JsonObj sides)
                    foreach (KeyValuePair<string, object> kv in sides)
                        teams.Add(kv.Key);
            var bySide = new JsonObj();
            foreach (string t in teams)
            {
                var totals = new Dictionary<string, long>();
                foreach (string c in new[] { "actors", "downAtEnd" }.Concat(Counters))
                    totals[c] = 0;
                foreach (JobResult r in results)
                {
                    if (!(r.Soak.Get("sides") is JsonObj sides) || !(sides.Get(t) is JsonObj so))
                        continue;
                    foreach (string c in totals.Keys.ToList())
                        totals[c] += Int(so, c);
                }
                var side = new JsonObj();
                var tot = new JsonObj();
                foreach (KeyValuePair<string, long> kv in totals)
                    tot.Set(kv.Key, kv.Value);
                side.Set("totals", tot);
                if (n > 0)
                    side.Set("perJob", Rates(totals, n));
                if (totals["turns"] > 0)
                    side.Set("perTurn", Rates(totals.Where(kv => kv.Key != "turns" && kv.Key != "actors" && kv.Key != "downAtEnd")
                                                    .ToDictionary(kv => kv.Key, kv => kv.Value), totals["turns"]));
                bySide.Set(t, side);
            }
            o.Set("sides", bySide);

            // One compact row per job (seed order).
            var rows = new List<JsonObj>();
            foreach (JobResult r in results.OrderBy(r => r.Seed).ThenBy(r => r.Rep))
            {
                var row = new JsonObj();
                row.Set("seed", r.Seed).Set("rep", r.Rep).Set("outcome", r.Outcome).Set("verdict", r.Verdict)
                   .Set("rounds", r.Rounds).Set("turns", Int(r.Soak, "turns")).Set("wallSec", r.WallSec);
                if (r.Generated != null)
                    row.Set("encounter", r.Generated.Get("name")).Set("dungeonLevel", r.Generated.Get("dungeonLevel"))
                       .Set("creatures", r.Generated.Get("count"));
                if (r.Soak.Get("sides") is JsonObj sides)
                {
                    var compact = new JsonObj();
                    foreach (KeyValuePair<string, object> kv in sides)
                    {
                        if (!(kv.Value is JsonObj so))
                            continue;
                        compact.Set(kv.Key, Int(so, "attacks") + "a/" + Int(so, "aoos") + "o/" + Int(so, "maneuvers") + "m/"
                                            + Int(so, "casts") + "c/" + Int(so, "moves") + "mv, down " + Int(so, "downAtEnd") + "/" + Int(so, "actors"));
                    }
                    row.Set("sides", compact);
                }
                rows.Add(row);
            }
            o.Set("legend", "sides: attacks a / AoOs o / maneuvers m / casts c / moves mv, down at end / actors");
            o.Set("perSeed", rows);
            return o;
        }

        public const string RowsFile = "soak-rows.tsv";

        /// <summary>Writes one tab-separated line per soak job to &lt;runDir&gt;/soak-rows.tsv.</summary>
        public static void WriteRows(string runDir, IEnumerable<JobResult> results)
        {
            var sb = new StringBuilder();
            foreach (JobResult r in results)
            {
                if (r.Soak == null)
                    continue;
                var sides = new List<string>();
                if (r.Soak.Get("sides") is JsonObj so)
                    foreach (KeyValuePair<string, object> kv in so)
                        if (kv.Value is JsonObj c)
                            sides.Add(kv.Key + "=" + string.Join(",", c.Select(x => x.Key + ":" + Int(c, x.Key))));
                string Clean(object o) => Convert.ToString(o, CultureInfo.InvariantCulture)?.Replace('\t', ' ').Replace('\n', ' ') ?? "";
                sb.Append(string.Join("\t", new[]
                {
                    Clean(r.Id), Clean(r.Seed), Clean(r.Rep), Clean(r.Outcome), Clean(r.Verdict), Clean(r.Rounds),
                    Clean(Int(r.Soak, "turns")), r.WallSec.ToString("0.###", CultureInfo.InvariantCulture),
                    Clean(r.Generated?.Get("name")), Clean(r.Generated?.Get("dungeonLevel")), Clean(r.Generated?.Get("count")),
                    string.Join(";", sides)
                })).Append('\n');
            }
            if (sb.Length > 0)
                Json.WriteFileAtomic(Path.Combine(runDir, RowsFile), sb.ToString());
        }

        /// <summary>
        /// Aggregates the soak rows of several runs (run ids under Logs/Scenarios), as one soak split over fresh Play
        /// sessions. A seed that appears in more than one run counts once per run (repetitions are kept apart by rep).
        /// </summary>
        public static JsonObj MergeRuns(IEnumerable<string> runIds)
        {
            var results = new List<JobResult>();
            var runs = new List<string>();
            var missing = new List<string>();
            foreach (string runId in runIds)
            {
                string path = Path.Combine(ScenarioRunner.ScenariosDir, runId, RowsFile);
                if (!File.Exists(path))
                {
                    missing.Add(runId);
                    continue;
                }
                runs.Add(runId);
                foreach (string line in File.ReadAllLines(path))
                {
                    JobResult r = FromRow(line);
                    if (r != null)
                        results.Add(r);
                }
            }
            JsonObj o = Aggregate(results);
            o.Set("runs", runs);
            if (missing.Count > 0)
                o.Set("runsWithoutSoakRows", missing);
            return o;
        }

        private static JobResult FromRow(string line)
        {
            string[] f = line.Split('\t');
            if (f.Length < 12)
                return null;
            int I(string v) => int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) ? x : 0;
            var r = new JobResult
            {
                Id = f[0],
                Seed = I(f[1]),
                Rep = I(f[2]),
                Outcome = Enum.TryParse(f[3], out Outcome oc) ? oc : Outcome.None,
                Verdict = Enum.TryParse(f[4], out Verdict vd) ? vd : Verdict.Error,
                Rounds = I(f[5]),
                WallSec = float.TryParse(f[7], NumberStyles.Float, CultureInfo.InvariantCulture, out float w) ? w : 0f
            };
            if (f[8].Length > 0 || f[9].Length > 0)
            {
                r.Generated = new JsonObj();
                r.Generated.Set("name", f[8]).Set("dungeonLevel", I(f[9])).Set("count", I(f[10]));
            }
            var sides = new JsonObj();
            foreach (string side in f[11].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = side.IndexOf('=');
                if (eq <= 0)
                    continue;
                var c = new JsonObj();
                foreach (string kv in side.Substring(eq + 1).Split(','))
                {
                    int colon = kv.IndexOf(':');
                    if (colon > 0)
                        c.Set(kv.Substring(0, colon), I(kv.Substring(colon + 1)));
                }
                sides.Set(side.Substring(0, eq), c);
            }
            r.Soak = new JsonObj();
            r.Soak.Set("outcome", r.Outcome).Set("rounds", r.Rounds).Set("turns", I(f[6])).Set("sides", sides);
            return r;
        }

        private static JsonObj Rates(Dictionary<string, long> totals, long divisor)
        {
            var r = new JsonObj();
            foreach (KeyValuePair<string, long> kv in totals)
                r.Set(kv.Key, Math.Round((double)kv.Value / divisor, 3));
            return r;
        }

        private static int Int(JsonObj o, string key)
        {
            object v = o != null ? o.Get(key) : null;
            if (v is int i) return i;
            if (v is long l) return (int)l;
            return 0;
        }
    }
}
#endif
