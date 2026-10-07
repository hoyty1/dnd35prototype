#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Tests.Scenarios
{
    /// <summary>The result of one expectation: pass, fail or inconclusive, with a detail and the trace seqs it refers to.</summary>
    public struct ExpectResult
    {
        public string Status;
        public string Detail;
        public int[] Seqs;

        public bool IsPass => Status == "pass";
        public bool IsFail => Status == "fail";
        public bool IsInconclusive => Status == "inconclusive";

        public static ExpectResult Pass(string detail, params int[] seqs) => new ExpectResult { Status = "pass", Detail = detail, Seqs = seqs };
        public static ExpectResult Fail(string detail, params int[] seqs) => new ExpectResult { Status = "fail", Detail = detail, Seqs = seqs };
        public static ExpectResult Inconclusive(string reason) => new ExpectResult { Status = "inconclusive", Detail = reason, Seqs = new int[0] };
    }

    /// <summary>Read-only queries over a finished job's trace. Keys are actor keys; null matches any.</summary>
    public sealed class TraceView
    {
        private readonly IReadOnlyList<TraceEvent> _events;

        public Outcome Outcome { get; private set; }
        public string OutcomeReason { get; private set; }
        public IReadOnlyList<TraceEvent> Events => _events;

        public TraceView(IReadOnlyList<TraceEvent> events, Outcome outcome, string reason)
        {
            _events = events;
            Outcome = outcome;
            OutcomeReason = reason;
        }

        public IEnumerable<TraceEvent> Of(string ev) => _events.Where(e => e.Ev == ev);

        public List<TraceEvent> Attacks(string by = null, string target = null, bool? aoo = null, int? round = null)
            => Of("attack").Where(e => (by == null || e.Str("attacker") == by) && (target == null || e.Str("target") == target)
                && (aoo == null || e.Bool("aoo") == aoo.Value) && (round == null || e.Round == round.Value)).ToList();

        public List<TraceEvent> AoOs(string by = null, string target = null, string trigger = null)
            => Of("aoo").Where(e => (by == null || e.Str("by") == by) && (target == null || e.Str("target") == target)
                && (trigger == null || e.Str("trigger") == trigger)).ToList();

        public List<TraceEvent> Maneuvers(string by = null, SpecialAttackType? type = null)
            => Of("maneuver").Where(e => (by == null || e.Str("by") == by) && (type == null || e.Get("type") is SpecialAttackType t && t == type.Value)).ToList();

        public List<TraceEvent> Casts(string by = null)
            => Of("threatened_cast").Where(e => by == null || e.Str("by") == by).ToList();

        public List<TraceEvent> Moves(string by = null, int? round = null)
            => Of("move").Where(e => (by == null || e.Str("actor") == by) && (round == null || e.Round == round.Value)).ToList();

        public List<TraceEvent> Conds(string key = null, CombatConditionType? type = null)
            => Of("cond").Where(e => (key == null || e.Str("actor") == key) && (type == null || e.Get("type") is CombatConditionType t && t == type.Value)).ToList();

        /// <summary>'step' events of <paramref name="actor"/> (null: any) whose label starts with <paramref name="stepPrefix"/> (null: any), in <paramref name="round"/> (null: any).</summary>
        public List<TraceEvent> Steps(string actor = null, string stepPrefix = null, int? round = null)
            => Of("step").Where(e => (actor == null || e.Str("actor") == actor)
                && (stepPrefix == null || (e.Str("step") ?? "").StartsWith(stepPrefix, StringComparison.Ordinal))
                && (round == null || e.Round == round.Value)).ToList();

        /// <summary>Integer captures of <paramref name="regex"/>'s group 1 over the combat log lines, in order (round filter optional).</summary>
        public List<KeyValuePair<TraceEvent, int>> LogInts(string regex, int? round = null)
        {
            var r = new Regex(regex, RegexOptions.CultureInvariant);
            var list = new List<KeyValuePair<TraceEvent, int>>();
            foreach (TraceEvent e in Of("log"))
            {
                if (round != null && e.Round != round.Value)
                    continue;
                Match m = r.Match(e.Str("text") ?? "");
                if (m.Success && int.TryParse(m.Groups[1].Value, out int v))
                    list.Add(new KeyValuePair<TraceEvent, int>(e, v));
            }
            return list;
        }

        public List<TraceEvent> TurnsOf(string key) => Of("turn_start").Where(e => e.Str("actor") == key).ToList();

        public List<TraceEvent> Violations(bool includeWaived = false)
            => Of("violation").Where(e => includeWaived || e.Str("waivedBy") == null).ToList();

        /// <summary>The snapshot of <paramref name="key"/> at the first turn_start of <paramref name="round"/>, or null.</summary>
        public JsonObj Snapshot(string key, int round)
        {
            TraceEvent ts = Of("turn_start").FirstOrDefault(e => e.Round == round);
            return ts != null ? FindSnap(ts.Get("snap"), key) : null;
        }

        /// <summary>The snapshot of <paramref name="key"/> at the start of <paramref name="actor"/>'s turn in <paramref name="round"/>, or null.</summary>
        public JsonObj Snapshot(string key, int round, string actor)
        {
            TraceEvent ts = Of("turn_start").FirstOrDefault(e => e.Round == round && e.Str("actor") == actor);
            return ts != null ? FindSnap(ts.Get("snap"), key) : null;
        }

        /// <summary>The final snapshot of <paramref name="key"/> (from the 'end' event), or null.</summary>
        public JsonObj Final(string key)
        {
            TraceEvent end = Of("end").LastOrDefault();
            return end != null ? FindSnap(end.Get("final"), key) : null;
        }

        private static JsonObj FindSnap(object snaps, string key)
        {
            if (snaps is List<JsonObj> list)
                foreach (JsonObj o in list)
                    if (o.Get("k") as string == key)
                        return o;
            return null;
        }

        public bool Before(int seqA, int seqB) => seqA > 0 && seqB > 0 && seqA < seqB;

        public List<TraceEvent> Log(string regex)
        {
            var r = new Regex(regex, RegexOptions.CultureInvariant);
            return Of("log").Where(e => r.IsMatch(e.Str("text") ?? "")).ToList();
        }
    }

    /// <summary>Ready-made expectation checks. Put the rule cite in the expectation name.</summary>
    public static class Expect
    {
        public static Func<TraceView, ExpectResult> Outcome(params Outcome[] allowed)
            => v => allowed.Contains(v.Outcome)
                ? ExpectResult.Pass("outcome " + v.Outcome)
                : ExpectResult.Fail("outcome " + v.Outcome + " (" + v.OutcomeReason + "), expected " + string.Join("|", allowed));

        /// <summary>The game itself ended combat (Victory or Defeat, not an Undetected end the harness had to halt).</summary>
        public static Func<TraceView, ExpectResult> GameDetectedEnd()
            => Outcome(Scenarios.Outcome.Victory, Scenarios.Outcome.Defeat);

        public static Func<TraceView, ExpectResult> RoundsAtMost(int n)
            => v =>
            {
                int max = v.Of("round").Select(e => e.Int("n")).DefaultIfEmpty(0).Max();
                return max <= n ? ExpectResult.Pass(max + " rounds") : ExpectResult.Fail(max + " rounds, expected at most " + n);
            };

        /// <summary>Between <paramref name="min"/> and <paramref name="max"/> events of type <paramref name="ev"/> match <paramref name="filter"/>.</summary>
        public static Func<TraceView, ExpectResult> Count(string ev, Func<TraceEvent, bool> filter, int min, int max = int.MaxValue)
            => v =>
            {
                List<TraceEvent> hits = v.Of(ev).Where(e => filter == null || filter(e)).ToList();
                int[] seqs = hits.Select(e => e.Seq).Take(20).ToArray();
                return hits.Count >= min && hits.Count <= max
                    ? ExpectResult.Pass(hits.Count + " " + ev, seqs)
                    : ExpectResult.Fail(hits.Count + " " + ev + ", expected " + min + (max == int.MaxValue ? "+" : ".." + max), seqs);
            };

        /// <summary>Every check passes (fails on the first failure; inconclusive if any is inconclusive and none fails).</summary>
        public static Func<TraceView, ExpectResult> All(params Func<TraceView, ExpectResult>[] checks)
            => v =>
            {
                var details = new List<string>();
                ExpectResult? inconclusive = null;
                foreach (Func<TraceView, ExpectResult> c in checks)
                {
                    ExpectResult r = c(v);
                    if (r.IsFail) return r;
                    if (r.IsInconclusive) inconclusive = r;
                    details.Add(r.Detail);
                }
                return inconclusive ?? ExpectResult.Pass(string.Join("; ", details));
            };

        public static Func<TraceView, ExpectResult> Any(params Func<TraceView, ExpectResult>[] checks)
            => v =>
            {
                var details = new List<string>();
                foreach (Func<TraceView, ExpectResult> c in checks)
                {
                    ExpectResult r = c(v);
                    if (r.IsPass) return r;
                    details.Add(r.Detail);
                }
                return ExpectResult.Fail("none passed: " + string.Join("; ", details));
            };

        /// <summary>No event of type <paramref name="ev"/> matches <paramref name="filter"/>.</summary>
        public static Func<TraceView, ExpectResult> None(string ev, Func<TraceEvent, bool> filter)
            => Count(ev, filter, 0, 0);

        /// <summary>
        /// An AoO by <paramref name="by"/> on <paramref name="target"/> comes before the first event matching
        /// <paramref name="action"/> (inconclusive when the action never happens).
        /// </summary>
        public static Func<TraceView, ExpectResult> AoOBefore(string by, string target, Func<TraceEvent, bool> action)
            => v =>
            {
                TraceEvent act = v.Events.FirstOrDefault(action);
                if (act == null)
                    return ExpectResult.Inconclusive("the action never happened");
                TraceEvent aoo = v.AoOs(by, target).FirstOrDefault();
                if (aoo == null)
                    return ExpectResult.Fail("no AoO by " + by + " on " + target, act.Seq);
                return aoo.Seq < act.Seq
                    ? ExpectResult.Pass("AoO #" + aoo.Seq + " before #" + act.Seq, aoo.Seq, act.Seq)
                    : ExpectResult.Fail("AoO #" + aoo.Seq + " after #" + act.Seq, aoo.Seq, act.Seq);
            };

        /// <summary>The attack modifiers (total - die) of <paramref name="key"/>'s non-AoO attacks in <paramref name="round"/> equal <paramref name="mods"/> in order.</summary>
        public static Func<TraceView, ExpectResult> ModSequence(string key, int round, params int[] mods)
            => v =>
            {
                List<TraceEvent> atk = v.Attacks(key, null, false, round);
                int[] got = atk.Select(e => e.Int("mod")).ToArray();
                int[] seqs = atk.Select(e => e.Seq).ToArray();
                if (atk.Count == 0)
                    return ExpectResult.Inconclusive(key + " made no attack in round " + round);
                return got.SequenceEqual(mods)
                    ? ExpectResult.Pass("mods [" + string.Join(",", got) + "]", seqs)
                    : ExpectResult.Fail("mods [" + string.Join(",", got) + "], expected [" + string.Join(",", mods) + "]", seqs);
            };

        /// <summary>
        /// The <paramref name="nth"/> (0-based) step of <paramref name="actor"/> in <paramref name="round"/> whose label
        /// starts with <paramref name="stepPrefix"/> ended with one of <paramref name="statuses"/> (done, refused, skipped, dropped).
        /// </summary>
        public static Func<TraceView, ExpectResult> StepStatus(string actor, int round, string stepPrefix, int nth, params string[] statuses)
            => v =>
            {
                List<TraceEvent> steps = v.Steps(actor, stepPrefix, round);
                if (steps.Count <= nth)
                    return ExpectResult.Fail(actor + " has " + steps.Count + " '" + stepPrefix + "' steps in round " + round + ", expected at least " + (nth + 1));
                TraceEvent e = steps[nth];
                string st = e.Str("status");
                return statuses.Contains(st)
                    ? ExpectResult.Pass(e.Str("step") + " " + st, e.Seq)
                    : ExpectResult.Fail(e.Str("step") + " " + st + (e.Str("note") != null ? " (" + e.Str("note") + ")" : "") + ", expected " + string.Join("|", statuses), e.Seq);
            };

        /// <summary>Every 'assert' event passed (inconclusive when there is none).</summary>
        public static Func<TraceView, ExpectResult> AssertsPass()
            => v =>
            {
                List<TraceEvent> asserts = v.Of("assert").ToList();
                if (asserts.Count == 0)
                    return ExpectResult.Inconclusive("no assert step ran");
                TraceEvent bad = asserts.FirstOrDefault(e => !e.Bool("ok"));
                return bad == null
                    ? ExpectResult.Pass(asserts.Count + " asserts")
                    : ExpectResult.Fail("assert '" + bad.Str("name") + "' failed" + (bad.Str("error") != null ? ": " + bad.Str("error") : ""), bad.Seq);
            };

        /// <summary>Every turn of <paramref name="actor"/> ran under <paramref name="controller"/> (ui, ai, scripted, idle).</summary>
        public static Func<TraceView, ExpectResult> Controller(string actor, string controller)
            => v =>
            {
                List<TraceEvent> turns = v.TurnsOf(actor);
                if (turns.Count == 0)
                    return ExpectResult.Fail("no turn of " + actor);
                TraceEvent bad = turns.FirstOrDefault(e => e.Str("controller") != controller);
                return bad == null
                    ? ExpectResult.Pass(turns.Count + " " + controller + " turns")
                    : ExpectResult.Fail(actor + " turn under " + bad.Str("controller"), bad.Seq);
            };

        public static Func<TraceView, ExpectResult> Custom(Func<TraceView, ExpectResult> check) => check;
    }

    /// <summary>Turns a job's outcome, expectation results and violations into a verdict (docs/TESTING.md 3.4).</summary>
    public static class VerdictRules
    {
        public static bool IsErrorOutcome(Outcome o)
            => o == Outcome.SetupFailed || o == Outcome.SoftLock || o == Outcome.SkipChainRunaway || o == Outcome.Exception
            || o == Outcome.Paused || o == Outcome.Timeout || o == Outcome.Contaminated;

        /// <param name="results">(expectation, result) pairs.</param>
        /// <param name="unwaived">Violations no waiver matched.</param>
        /// <param name="waived">Violations a waiver matched.</param>
        /// <param name="hookErrors">Harness hook errors (always an Error).</param>
        public static Verdict Compute(Outcome outcome, IList<KeyValuePair<Expectation, ExpectResult>> results, int unwaived, int waived, int hookErrors)
        {
            if (IsErrorOutcome(outcome) || hookErrors > 0)
                return Verdict.Error;

            bool fail = unwaived > 0, xpass = false, xfail = false, inconclusive = false;
            foreach (KeyValuePair<Expectation, ExpectResult> kv in results)
            {
                bool isX = !string.IsNullOrEmpty(kv.Key.XFailIssue);
                if (kv.Value.IsFail)
                {
                    if (isX) xfail = true; else fail = true;
                }
                else if (kv.Value.IsPass)
                {
                    if (isX) xpass = true;
                }
                else
                {
                    inconclusive = true;
                }
            }

            if (fail) return Verdict.Fail;
            if (xpass) return Verdict.XPass;
            if (xfail) return Verdict.XFail;
            // An aborted (Halted) job never counts as a pass: the fight did not run to its end.
            if (inconclusive || outcome == Outcome.Halted) return Verdict.Inconclusive;
            if (waived > 0) return Verdict.Known;
            return Verdict.Pass;
        }

        /// <summary>
        /// Aggregates one scenario over several seeds (sweep), first match wins: Error, Fail or XPass if any seed had it;
        /// Pass if any seed passed; Known if any seed was Known (and none passed); Inconclusive if every seed was;
        /// otherwise XFail.
        /// </summary>
        public static Verdict Aggregate(IEnumerable<Verdict> verdicts)
        {
            List<Verdict> v = verdicts.ToList();
            if (v.Count == 0) return Verdict.Inconclusive;
            if (v.Contains(Verdict.Error)) return Verdict.Error;
            if (v.Contains(Verdict.Fail)) return Verdict.Fail;
            if (v.Contains(Verdict.XPass)) return Verdict.XPass;
            if (v.Contains(Verdict.Pass)) return Verdict.Pass;
            if (v.Contains(Verdict.Known)) return Verdict.Known;
            if (v.All(x => x == Verdict.Inconclusive)) return Verdict.Inconclusive;
            return Verdict.XFail;
        }
    }
}
#endif
