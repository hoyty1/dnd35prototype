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
    /// "aoo", "maneuver", "cast" (casts begun; ScenarioHooks.SpellCast: every NPC-path cast, and PC-pipeline casts of Ui actors, which a soak has none of) and own "move"/"path-move" events.
    /// Each run with soak jobs also writes Logs/Scenarios/&lt;runId&gt;/soak-rows.tsv (one line per job), which
    /// <see cref="MergeRuns"/> reads back to aggregate a soak split over several fresh Play sessions.
    /// Since batch 14 step 1 it also holds the AI decision-time rows and summaries (<see cref="TimingRow"/>,
    /// <see cref="TimingSummary"/>; ai-timing.tsv, a separate file beside soak-rows.tsv), for every run, soak or not.
    /// soak-rows.tsv holds the 12 original columns plus <see cref="NpcColumns"/> (append-only since batch 15 step 1).
    /// The aggregate names the scenario ids it covers ("byScenario") and each per-seed row its id, so a run that mixes
    /// the two soaks (a "soak/*" filter) shows it; compare a baseline only with a run of the same id.
    /// </summary>
    public static class SoakStats
    {
        public const string Tag = "soak";

        private static readonly string[] Counters =
            { "turns", "attacks", "hits", "aoos", "maneuvers", "maneuverHits", "casts", "moves", "squares" };

        /// <summary>
        /// The NPC caster columns (batch 15 step 1, docs/designs/npc_spellcasting_batch15.md), in their soak-rows.tsv order
        /// after the 12 original columns (append-only: MergeRuns reads by index). "NPC" is every actor on the Enemy team,
        /// its summons included. Each job's soak record holds them as "npc"; none is a trace event, so no trace hash moves.
        /// <list type="bullet">
        /// <item>npcCasts: casts begun ("cast" events, ScenarioHooks.SpellCast; scroll and wand casts included).</item>
        /// <item>npcCastsByLevel: those casts by the cast event's level ("1:2,2:1"; SpellData.SpellLevel, not yet the
        /// casting class's level, SPL-008); empty when none.</item>
        /// <item>npcSlotsLeftAtEnd: 1st-level and higher slots still castable at the job's end: uncast prepared slots
        /// (not disabled by a negative level or locked by Imbue) plus a spontaneous caster's slots left; counted only for a
        /// creature the spawn gave spellcasting (GameManager.HasCastableSpellForPersonality), since a reused slot keeps an
        /// earlier creature's SpellcastingComponent.</item>
        /// <item>npcFirstCastRound: the round of the first NPC cast, 0 when none.</item>
        /// <item>npcRefusedPicks: casts the cast core refused at its legality stage (a "legality" cast stage whose detail is
        /// not "ok").</item>
        /// <item>npcNoEffectCasts: casts with no effect: the cast stages split into casts at each caster's "begin" stage, a
        /// cast counted once when it has a "buff" stage and none of its buff stages (one per target on an area buff) is
        /// "applied".</item>
        /// <item>npcItemUses: consumable uses: potions and scrolls (a stack counts its quantity) and wand and staff charges
        /// held at the start minus those held at the end.</item>
        /// <item>npcDirectEffects: a directed spell effect moved by an NPC (today a Flaming Sphere, by its combat log line,
        /// whose mover must be an Enemy actor's whole name that no other actor has).</item>
        /// </list>
        /// The cast-stage columns also count a caster the trace has not keyed yet ("name:&lt;name&gt;", a summon before its
        /// first cast event) when the name is an Enemy actor's. The live reads (ScenarioRunner.SoakNpcLive) never fail a job:
        /// on an exception their columns read 0 and the job gets an "npc columns" note.
        /// </summary>
        public static readonly string[] NpcColumns =
        {
            "npcCasts", "npcCastsByLevel", "npcSlotsLeftAtEnd", "npcFirstCastRound",
            "npcRefusedPicks", "npcNoEffectCasts", "npcItemUses", "npcDirectEffects"
        };

        /// <summary>The combat log text of a Flaming Sphere move (GameManager_FlamingSphere), after the mover's name.</summary>
        private const string SphereMove = " rolls Flaming Sphere to (";

        /// <summary>The team whose actors the NPC columns count (CharacterTeam.Enemy as the trace writes it).</summary>
        public const string NpcTeam = "Enemy";

        /// <summary>The number of columns soak-rows.tsv had before the NPC columns (MergeRuns reads older rows too).</summary>
        public const int BaseRowColumns = 12;

        /// <summary>What a soak job's NPC columns read from the live game at its start and end (not from the trace).</summary>
        public sealed class NpcLive
        {
            public int SlotsLeftAtEnd;
            public int ItemUses;
        }

        /// <summary>The soak record of one job from its trace.</summary>
        internal static JsonObj ForJob(IReadOnlyList<TraceEvent> events, Outcome outcome, int rounds,
            IReadOnlyList<ScenarioTrace.CastStageRecord> castStages = null, NpcLive live = null)
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
            o.Set("npc", NpcRecord(events, team, castStages, live));
            // The DC3 measure (batch 15 step 1): the most turns any Enemy actor started. Summary only: not a soak-rows.tsv
            // column, so a merged report (MergeRuns) does not carry it; each run's summary.json does.
            var npcTurns = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (TraceEvent e in events)
                if (e.Ev == "turn_start" && e.Str("actor") != null && team.TryGetValue(e.Str("actor"), out string tt) && tt == NpcTeam)
                    npcTurns[e.Str("actor")] = npcTurns.TryGetValue(e.Str("actor"), out int k) ? k + 1 : 1;
            o.Set("npcMaxTurns", npcTurns.Count > 0 ? npcTurns.Values.Max() : 0);
            return o;
        }

        /// <summary>The NPC columns of one job (<see cref="NpcColumns"/>), from its trace, its cast stages and its live counts.</summary>
        internal static JsonObj NpcRecord(IReadOnlyList<TraceEvent> events, IReadOnlyDictionary<string, string> team,
            IReadOnlyList<ScenarioTrace.CastStageRecord> castStages, NpcLive live)
        {
            bool IsNpcKey(string key) => key != null && team != null && team.TryGetValue(key, out string t) && t == NpcTeam;
            // The Enemy team's names, less any name a non-Enemy actor also has (so a party move is never counted as an NPC's).
            var names = new HashSet<string>(StringComparer.Ordinal);
            var otherNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (TraceEvent e in events)
            {
                if (e.Ev != "actor" || string.IsNullOrEmpty(e.Str("name")))
                    continue;
                if (IsNpcKey(e.Str("key")))
                    names.Add(e.Str("name"));
                else
                    otherNames.Add(e.Str("name"));
            }
            names.ExceptWith(otherNames);
            // A cast stage names a caster the trace has not keyed yet (a summon before its first cast event) "name:<name>".
            bool IsNpc(string key) => IsNpcKey(key)
                || (key != null && key.StartsWith("name:", StringComparison.Ordinal) && names.Contains(key.Substring("name:".Length)));

            int casts = 0, firstRound = 0, direct = 0;
            var byLevel = new SortedDictionary<int, int>();
            foreach (TraceEvent e in events)
            {
                if (e.Ev == "cast" && IsNpcKey(e.Str("by")))
                {
                    casts++;
                    if (firstRound == 0)
                        firstRound = e.Round;
                    int level = e.Int("level");
                    byLevel[level] = byLevel.TryGetValue(level, out int n) ? n + 1 : 1;
                }
                else if (e.Ev == "log" && names.Count > 0)
                {
                    // "<icon> <who> rolls Flaming Sphere to (x, y)." (GameManager_FlamingSphere): <who> must be an NPC's
                    // whole name, the leading icon and spaces stripped.
                    string text = e.Str("text");
                    int at = text != null ? text.IndexOf(SphereMove, StringComparison.Ordinal) : -1;
                    if (at <= 0)
                        continue;
                    int start = 0;
                    while (start < at && !char.IsLetterOrDigit(text[start]))
                        start++;
                    if (names.Contains(text.Substring(start, at - start)))
                        direct++;
                }
            }

            // The cast stages, split into casts at each caster's "begin" stage: a refusal is one legality stage that is not
            // "ok"; a cast with no effect is one whose buff stages (one per target on an area buff) all applied nothing.
            int refused = 0, noEffect = 0;
            if (castStages != null)
            {
                var open = new Dictionary<string, bool[]>(StringComparer.Ordinal); // caster -> {has a buff stage, any applied}
                void Close(string by)
                {
                    if (open.TryGetValue(by, out bool[] c) && c[0] && !c[1])
                        noEffect++;
                    open.Remove(by);
                }
                foreach (ScenarioTrace.CastStageRecord st in castStages)
                {
                    if (st.By == null || !IsNpc(st.By))
                        continue;
                    if (st.Stage == CastStageTrace.Begin)
                    {
                        Close(st.By);
                        open[st.By] = new bool[2];
                    }
                    else if (st.Stage == CastStageTrace.Legality && st.Detail != "ok")
                    {
                        refused++;
                    }
                    else if (st.Stage == CastStageTrace.Buff)
                    {
                        if (!open.TryGetValue(st.By, out bool[] c))
                            open[st.By] = c = new bool[2];
                        c[0] = true;
                        if (st.Detail != null && st.Detail.StartsWith("applied", StringComparison.Ordinal))
                            c[1] = true;
                    }
                }
                foreach (string by in open.Keys.ToList())
                    Close(by);
            }

            var o = new JsonObj();
            o.Set("npcCasts", casts)
             .Set("npcCastsByLevel", FormatByLevel(byLevel))
             .Set("npcSlotsLeftAtEnd", live != null ? live.SlotsLeftAtEnd : 0)
             .Set("npcFirstCastRound", firstRound)
             .Set("npcRefusedPicks", refused)
             .Set("npcNoEffectCasts", noEffect)
             .Set("npcItemUses", live != null ? live.ItemUses : 0)
             .Set("npcDirectEffects", direct);
            return o;
        }

        /// <summary>"1:2,2:1": casts per spell level, ascending; empty when none.</summary>
        public static string FormatByLevel(IDictionary<int, int> byLevel)
            => byLevel == null ? "" : string.Join(",", byLevel.OrderBy(kv => kv.Key)
                .Select(kv => kv.Key.ToString(CultureInfo.InvariantCulture) + ":" + kv.Value.ToString(CultureInfo.InvariantCulture)));

        /// <summary>Parses <see cref="FormatByLevel"/>'s text back (unknown parts are skipped).</summary>
        public static SortedDictionary<int, int> ParseByLevel(string text)
        {
            var d = new SortedDictionary<int, int>();
            if (string.IsNullOrEmpty(text))
                return d;
            foreach (string part in text.Split(','))
            {
                int colon = part.IndexOf(':');
                if (colon > 0
                    && int.TryParse(part.Substring(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out int level)
                    && int.TryParse(part.Substring(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
                    d[level] = (d.TryGetValue(level, out int had) ? had : 0) + n;
            }
            return d;
        }

        /// <summary>
        /// 1st-level and higher slots <paramref name="c"/> can still cast: uncast prepared slots that hold a spell (not
        /// disabled by a negative level or locked by Imbue) plus a spontaneous caster's slots left. 0 without a component.
        /// </summary>
        public static int SlotsLeft(CharacterController c)
        {
            SpellcastingComponent sc = c != null ? c.Spellcasting : null;
            if (sc == null)
                return 0;
            int n = 0;
            foreach (SpellSlot slot in sc.SpellSlots)
                if (slot != null && slot.Level >= 1 && slot.HasSpell && !slot.IsUsed && !slot.DisabledByNegativeLevel && !slot.LockedByImbue)
                    n++;
            if (sc.IsSpontaneousCaster)
                for (int level = 1; level <= 9; level++)
                    n += Math.Max(0, sc.SpontaneousData.GetSlotsRemaining(level));
            return n;
        }

        /// <summary>
        /// The consumable uses <paramref name="c"/> holds: potions and scrolls (a stack counts its quantity) and the charges
        /// of its wands and staffs, in its general slots and its hands.
        /// </summary>
        public static int ItemUsesHeld(CharacterController c)
        {
            global::Inventory inv = c != null && c.InventoryComp != null ? c.InventoryComp.CharacterInventory : null;
            if (inv == null)
                return 0;
            int n = 0;
            void Count(ItemData item)
            {
                if (item == null)
                    return;
                if (item.IsWand)
                    n += Math.Max(0, item.CurrentCharges);
                else if (item.IsStaff)
                    n += Math.Max(0, item.StaffCharges);
                else if (item.IsPotion || item.IsScroll)
                    n += Math.Max(1, item.Quantity);
            }
            if (inv.GeneralSlots != null)
                foreach (ItemData item in inv.GeneralSlots)
                    Count(item);
            Count(inv.RightHandSlot);
            if (!ReferenceEquals(inv.LeftHandSlot, inv.RightHandSlot))
                Count(inv.LeftHandSlot);
            return n;
        }

        /// <summary>The run-level aggregate of the jobs that have a soak record.</summary>
        public static JsonObj Aggregate(IList<JobResult> results)
        {
            var o = new JsonObj();
            int n = results.Count;
            o.Set("jobs", n);
            // The scenario ids the aggregate covers (jobs per id): more than one means a mixed run ("soak/*" matches both
            // soak/dmg-random-encounters and soak/dmg-casters), whose figures compare with no recorded baseline.
            var byScenario = new JsonObj();
            foreach (IGrouping<string, JobResult> g in results.GroupBy(r => r.Id ?? "").OrderBy(g => g.Key, StringComparer.Ordinal))
                byScenario.Set(g.Key, g.Count());
            o.Set("byScenario", byScenario);
            if (byScenario.Count() > 1)
                o.Set("mixedScenarios", true);

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
            o.Set("npc", NpcAggregate(results));

            // One compact row per job (seed order).
            var rows = new List<JsonObj>();
            foreach (JobResult r in results.OrderBy(r => r.Id ?? "", StringComparer.Ordinal).ThenBy(r => r.Seed).ThenBy(r => r.Rep))
            {
                var row = new JsonObj();
                row.Set("id", r.Id).Set("seed", r.Seed).Set("rep", r.Rep).Set("outcome", r.Outcome).Set("verdict", r.Verdict)
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
                if (r.Soak.Get("npc") is JsonObj npc)
                    row.Set("npc", Int(npc, "npcCasts") + "c [" + (npc.Get("npcCastsByLevel") as string ?? "") + "] / "
                                   + Int(npc, "npcSlotsLeftAtEnd") + " slots left / first r" + Int(npc, "npcFirstCastRound") + " / "
                                   + Int(npc, "npcRefusedPicks") + " refused / " + Int(npc, "npcNoEffectCasts") + " no effect / "
                                   + Int(npc, "npcItemUses") + " items / " + Int(npc, "npcDirectEffects") + " directed");
                rows.Add(row);
            }
            o.Set("legend", "sides: attacks a / AoOs o / maneuvers m / casts c / moves mv, down at end / actors; npc: casts c [by level] / slots left at the end / first cast round / refused picks / no-effect casts / item uses / directed effects");
            o.Set("perSeed", rows);
            return o;
        }

        /// <summary>
        /// The run's NPC column totals (<see cref="NpcColumns"/>) over the jobs whose record has them: the sums, the casts by
        /// level, the jobs with an NPC cast and their mean first-cast round. A merged row from before batch 15 step 1 has no
        /// NPC columns and is counted apart ("jobsWithoutNpcColumns").
        /// </summary>
        public static JsonObj NpcAggregate(IList<JobResult> results)
        {
            var o = new JsonObj();
            var sums = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (string c in NpcColumns)
                if (c != "npcCastsByLevel" && c != "npcFirstCastRound")
                    sums[c] = 0;
            var byLevel = new SortedDictionary<int, int>();
            int jobs = 0, without = 0, withCast = 0, turnsMeasured = 0, beforeSecondTurn = 0;
            long firstRounds = 0;
            foreach (JobResult r in results)
            {
                if (r.Soak?.Get("npcMaxTurns") is int maxTurns)
                {
                    turnsMeasured++;
                    if (maxTurns < 2)
                        beforeSecondTurn++;
                }
                if (!(r.Soak?.Get("npc") is JsonObj npc))
                {
                    without++;
                    continue;
                }
                jobs++;
                foreach (string c in sums.Keys.ToList())
                    sums[c] += Int(npc, c);
                foreach (KeyValuePair<int, int> kv in ParseByLevel(npc.Get("npcCastsByLevel") as string))
                    byLevel[kv.Key] = (byLevel.TryGetValue(kv.Key, out int had) ? had : 0) + kv.Value;
                int first = Int(npc, "npcFirstCastRound");
                if (first > 0)
                {
                    withCast++;
                    firstRounds += first;
                }
            }
            o.Set("jobs", jobs);
            if (without > 0)
                o.Set("jobsWithoutNpcColumns", without);
            var tot = new JsonObj();
            foreach (KeyValuePair<string, long> kv in sums)
                tot.Set(kv.Key, kv.Value);
            o.Set("totals", tot);
            o.Set("npcCastsByLevel", FormatByLevel(byLevel));
            o.Set("jobsWithNpcCast", withCast);
            o.Set("meanFirstCastRound", withCast > 0 ? Math.Round((double)firstRounds / withCast, 3) : 0.0);
            // DC3 (docs/designs/npc_spellcasting_batch15.md): jobs in which no Enemy actor started a second turn, over the
            // jobs that carry npcMaxTurns (a per-run summary only: a merged report from soak-rows.tsv has none).
            o.Set("dc3", new JsonObj().Set("jobsMeasured", turnsMeasured).Set("endedBeforeNpcSecondTurn", beforeSecondTurn));
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
                var fields = new List<string>
                {
                    Clean(r.Id), Clean(r.Seed), Clean(r.Rep), Clean(r.Outcome), Clean(r.Verdict), Clean(r.Rounds),
                    Clean(Int(r.Soak, "turns")), r.WallSec.ToString("0.###", CultureInfo.InvariantCulture),
                    Clean(r.Generated?.Get("name")), Clean(r.Generated?.Get("dungeonLevel")), Clean(r.Generated?.Get("count")),
                    string.Join(";", sides)
                };
                // Batch 15 step 1: the NPC columns, appended after the 12 original ones (MergeRuns reads by index).
                JsonObj npc = r.Soak.Get("npc") as JsonObj;
                foreach (string c in NpcColumns)
                    fields.Add(c == "npcCastsByLevel" ? Clean(npc?.Get(c) as string ?? "") : Clean(Int(npc, c)));
                sb.Append(string.Join("\t", fields)).Append('\n');
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

            // The decision-time baseline over the same soak jobs (batch 14 step 1): each run's ai-timing.tsv, only the rows
            // of the jobs in its soak rows, so a merged rules or smoke run adds no turns.
            // A run without the file (one from before step 1, or a failed write) is named: not measured is not "no AI turns".
            var timing = new List<TimingRow>();
            var noTiming = new List<string>();
            var soakIds = new HashSet<string>(results.Select(r => r.Id), StringComparer.Ordinal);
            foreach (string runId in runs)
            {
                string timingPath = Path.Combine(ScenarioRunner.ScenariosDir, runId, TimingFile);
                if (!File.Exists(timingPath))
                {
                    noTiming.Add(runId);
                    continue;
                }
                timing.AddRange(ReadTimingRows(timingPath).Where(t => soakIds.Contains(t.Id)));
            }
            JsonObj merged = TimingSummary(timing);
            if (noTiming.Count > 0)
                merged.Set("runsWithoutAiTiming", noTiming);
            o.Set("aiTiming", merged);
            return o;
        }

        // ── AI decision time (batch 14 step 1; docs/TESTING.md 3.5) ─────────────────────────────────────

        public const string TimingFile = "ai-timing.tsv";

        private static readonly string[] BucketNames = Enum.GetNames(typeof(AiTimeBucket));

        /// <summary>One AI turn's decision time, as written to ai-timing.tsv (one line per turn).</summary>
        public sealed class TimingRow
        {
            public string Id;
            public int Seed;
            public int Rep;
            /// <summary>The turn belongs to the first job of a fresh Play session (JIT and first-use costs), left out of the summary.</summary>
            public bool WarmUp;
            public int Round;
            public string Actor;
            /// <summary>The turn was closed without its EndTurn (a stopped coroutine, the job's end).</summary>
            public bool Unclosed;
            public double Ms;
            public double[] BucketMs = new double[BucketNames.Length];
        }

        /// <summary>The rows of one job from the timer's completed turns.</summary>
        public static List<TimingRow> RowsFor(string id, int seed, int rep, bool warmUp, IEnumerable<AiDecisionTimer.TurnSample> turns)
        {
            var rows = new List<TimingRow>();
            foreach (AiDecisionTimer.TurnSample t in turns)
            {
                var r = new TimingRow { Id = id, Seed = seed, Rep = rep, WarmUp = warmUp, Round = t.Round, Actor = t.Actor, Unclosed = t.Unclosed, Ms = t.Ms };
                for (int b = 0; b < BucketNames.Length && b < t.BucketTicks.Length; b++)
                    r.BucketMs[b] = AiDecisionTimer.ToMs(t.BucketTicks[b]);
                rows.Add(r);
            }
            return rows;
        }

        /// <summary>
        /// A job's "aiTiming" record: {turns, p50, p95, max, over50, warmup} in milliseconds over its complete turns, plus
        /// bucket totals; the unclosed turns (closed without their EndTurn, so holding only part of their time) apart.
        /// </summary>
        public static JsonObj JobTiming(IList<TimingRow> rows, bool warmUp)
        {
            List<TimingRow> complete = rows.Where(r => !r.Unclosed).ToList();
            AiDecisionTimer.Stats st = AiDecisionTimer.Summarize(complete.Select(r => r.Ms).ToList());
            var o = new JsonObj();
            o.Set("turns", st.Turns).Set("p50", st.P50).Set("p95", st.P95).Set("max", st.Max).Set("over50", st.Over).Set("warmup", warmUp);
            o.Set("bucketsMs", BucketTotals(complete));
            o.Set("unclosed", UnclosedSummary(rows));
            return o;
        }

        /// <summary>The unclosed turns of a set of rows: {turns, max, ms} (their partial times, out of every percentile).</summary>
        private static JsonObj UnclosedSummary(IEnumerable<TimingRow> rows)
        {
            List<double> ms = rows.Where(r => r.Unclosed).Select(r => r.Ms).ToList();
            return new JsonObj().Set("turns", ms.Count).Set("max", ms.Count > 0 ? ms.Max() : 0.0).Set("ms", ms.Sum());
        }

        /// <summary>
        /// The whole-run summary over the complete turns that are not warm-up: p50, p95 and max per turn, the turns over the
        /// 50 ms budget, the slowest turn, and the time per bucket; the complete warm-up turns and the unclosed turns of
        /// measured jobs (partial times) are summarized apart.
        /// </summary>
        public static JsonObj TimingSummary(IEnumerable<TimingRow> all)
        {
            List<TimingRow> rows = all.ToList();
            List<TimingRow> measured = rows.Where(r => !r.WarmUp && !r.Unclosed).ToList();
            List<TimingRow> warm = rows.Where(r => r.WarmUp && !r.Unclosed).ToList();
            AiDecisionTimer.Stats st = AiDecisionTimer.Summarize(measured.Select(r => r.Ms).ToList());
            var o = new JsonObj();
            o.Set("budgetMs", AiDecisionTimer.WarnMs)
             .Set("jobs", measured.Select(r => r.Id + "|" + r.Seed + "|" + r.Rep).Distinct().Count())
             .Set("turns", st.Turns).Set("p50", st.P50).Set("p95", st.P95).Set("max", st.Max).Set("over50", st.Over);
            TimingRow slowest = measured.OrderByDescending(r => r.Ms).FirstOrDefault();
            if (slowest != null)
                o.Set("maxAt", slowest.Id + " s" + slowest.Seed + (slowest.Rep > 1 ? " r" + slowest.Rep : "") + " round " + slowest.Round + " " + slowest.Actor);
            o.Set("bucketsMs", BucketTotals(measured));
            AiDecisionTimer.Stats ws = AiDecisionTimer.Summarize(warm.Select(r => r.Ms).ToList());
            o.Set("warmup", new JsonObj().Set("jobs", warm.Select(r => r.Id + "|" + r.Seed + "|" + r.Rep).Distinct().Count())
                .Set("turns", ws.Turns).Set("p50", ws.P50).Set("p95", ws.P95).Set("max", ws.Max).Set("over50", ws.Over));
            o.Set("unclosed", UnclosedSummary(rows.Where(r => !r.WarmUp)));
            return o;
        }

        private static JsonObj BucketTotals(IEnumerable<TimingRow> rows)
        {
            var totals = new double[BucketNames.Length];
            foreach (TimingRow r in rows)
                for (int b = 0; b < totals.Length; b++)
                    totals[b] += r.BucketMs[b];
            var o = new JsonObj();
            for (int b = 0; b < totals.Length; b++)
                o.Set(BucketNames[b], totals[b]);
            return o;
        }

        /// <summary>
        /// Writes &lt;runDir&gt;/ai-timing.tsv: a "#" header, then one tab-separated line per AI turn (id, seed, rep, warm-up,
        /// round, actor, unclosed, total ms, ms per bucket). A separate file: soak-rows.tsv keeps its own layout (the 12
        /// original columns plus <see cref="NpcColumns"/>; MergeRuns reads it by column index).
        /// </summary>
        public static void WriteTimingRows(string runDir, IEnumerable<TimingRow> rows)
        {
            var sb = new StringBuilder();
            sb.Append("#id\tseed\trep\twarmup\tround\tactor\tunclosed\tms\t").Append(string.Join("\t", BucketNames)).Append('\n');
            int n = 0;
            string Ms(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
            foreach (TimingRow r in rows)
            {
                n++;
                sb.Append(r.Id).Append('\t').Append(r.Seed.ToString(CultureInfo.InvariantCulture)).Append('\t')
                  .Append(r.Rep.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(r.WarmUp ? '1' : '0').Append('\t')
                  .Append(r.Round.ToString(CultureInfo.InvariantCulture)).Append('\t')
                  .Append((r.Actor ?? "?").Replace('\t', ' ').Replace('\n', ' ')).Append('\t').Append(r.Unclosed ? '1' : '0').Append('\t')
                  .Append(Ms(r.Ms));
                foreach (double b in r.BucketMs)
                    sb.Append('\t').Append(Ms(b));
                sb.Append('\n');
            }
            if (n > 0)
                Json.WriteFileAtomic(Path.Combine(runDir, TimingFile), sb.ToString());
        }

        /// <summary>Reads an ai-timing.tsv back (an empty list when the file is missing).</summary>
        public static List<TimingRow> ReadTimingRows(string path)
        {
            var rows = new List<TimingRow>();
            if (!File.Exists(path))
                return rows;
            int I(string v) => int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) ? x : 0;
            double D(string v) => double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double x) ? x : 0.0;
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.Length == 0 || line[0] == '#')
                    continue;
                string[] f = line.Split('\t');
                if (f.Length < 8)
                    continue;
                var r = new TimingRow
                {
                    Id = f[0], Seed = I(f[1]), Rep = I(f[2]), WarmUp = f[3] == "1", Round = I(f[4]), Actor = f[5],
                    Unclosed = f[6] == "1", Ms = D(f[7])
                };
                for (int b = 0; b < r.BucketMs.Length && 8 + b < f.Length; b++)
                    r.BucketMs[b] = D(f[8 + b]);
                rows.Add(r);
            }
            return rows;
        }

        private static JobResult FromRow(string line)
        {
            string[] f = line.Split('\t');
            if (f.Length < BaseRowColumns)
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
            // npcMaxTurns (the DC3 measure) is not a column: a merged row has none.
            // A row from before batch 15 step 1 has only the 12 original columns: no "npc" record.
            if (f.Length >= BaseRowColumns + NpcColumns.Length)
            {
                var npc = new JsonObj();
                for (int i = 0; i < NpcColumns.Length; i++)
                {
                    string v = f[BaseRowColumns + i];
                    if (NpcColumns[i] == "npcCastsByLevel")
                        npc.Set(NpcColumns[i], v);
                    else
                        npc.Set(NpcColumns[i], I(v));
                }
                r.Soak.Set("npc", npc);
            }
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
