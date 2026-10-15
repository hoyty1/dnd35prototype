#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Tests.Scenarios
{
    /// <summary>
    /// Finds where two runs of the same scenario job part ways (batch 11, docs/designs/cast_core_batch11.md step 2;
    /// docs/TESTING.md 3.5). Both traces are read from Logs/Scenarios/&lt;runId&gt;/&lt;jobId&gt;.jsonl and compared event by event
    /// in their hashed form (ScenarioTrace.HashLines: no frame, wall-clock or host fields, no watchdog or log-leak events).
    /// The first differing event comes back with the events before it and the cast-stage lines of both runs around it
    /// (Logs/Scenarios/&lt;runId&gt;/&lt;jobId&gt;/caststages.jsonl, CastStageTrace). Editor only; works in edit and Play mode, so a
    /// Unity MCP CommandScript can call it at any time. Reads files only.
    /// </summary>
    public static class ScenarioTraceDiff
    {
        /// <summary>Trace events before the divergence returned as context.</summary>
        public const int ContextEvents = 4;

        /// <summary>Events the hash leaves out (ScenarioTrace.HashLines); never compared.</summary>
        private static readonly HashSet<string> UnhashedEvents = new HashSet<string> { "watchdog", "log-leak" };

        /// <summary>Fields that refer to other events by seq; left out when events are ignored, since the seqs then shift.</summary>
        private static readonly HashSet<string> SeqReferenceFields = new HashSet<string> { "seq", "attackSeq", "seqs" };

        /// <summary>
        /// The first trace event where <paramref name="runA"/> and <paramref name="runB"/> differ for job
        /// <paramref name="jobId"/> (the trace file name, e.g. "rules_cast-ui-fireball__s1", with or without .jsonl), as compact
        /// JSON: identical (true when no event differs), the compared event counts, and for a divergence its index, the
        /// two events (null where one trace has already ended), up to <see cref="ContextEvents"/> events before it from each
        /// run, and the cast stages of each run marked between the context's first event and the event after the divergence.
        /// Events named in <paramref name="ignoreEvents"/> (the trace's "ev" values, e.g. "cast") are dropped from both
        /// traces first; seq numbers and the fields that cite them (attackSeq, seqs) are then left out of the comparison.
        /// A run id may also be a path to a run directory. Errors come back as {"error": ...}.
        /// </summary>
        public static string FirstDivergence(string runA, string runB, string jobId, string[] ignoreEvents)
        {
            try
            {
                return Json.Serialize(Compare(runA, runB, jobId, ignoreEvents));
            }
            catch (Exception ex)
            {
                return Json.Serialize(new JsonObj().Set("error", ex.GetType().Name + ": " + ex.Message));
            }
        }

        private static JsonObj Compare(string runA, string runB, string jobId, string[] ignoreEvents)
        {
            if (string.IsNullOrWhiteSpace(jobId))
                return new JsonObj().Set("error", "jobId is empty");
            string job = jobId.EndsWith(".jsonl", StringComparison.Ordinal) ? jobId.Substring(0, jobId.Length - 6) : jobId;
            string dirA = RunDir(runA);
            string dirB = RunDir(runB);
            string traceA = Path.Combine(dirA, job + ".jsonl");
            string traceB = Path.Combine(dirB, job + ".jsonl");
            if (!File.Exists(traceA))
                return new JsonObj().Set("error", "no trace " + traceA.Replace('\\', '/'));
            if (!File.Exists(traceB))
                return new JsonObj().Set("error", "no trace " + traceB.Replace('\\', '/'));

            var ignore = new HashSet<string>(ignoreEvents ?? new string[0]);
            bool dropSeqRefs = ignore.Count > 0;
            List<Event> a = ReadTrace(traceA, ignore, dropSeqRefs);
            List<Event> b = ReadTrace(traceB, ignore, dropSeqRefs);

            var result = new JsonObj()
                .Set("runA", runA).Set("runB", runB).Set("jobId", job)
                .Set("ignoreEvents", new List<string>(ignore))
                .Set("eventsA", a.Count).Set("eventsB", b.Count);

            int n = Math.Min(a.Count, b.Count);
            int at = -1;
            for (int i = 0; i < n; i++)
            {
                if (a[i].Normalized != b[i].Normalized)
                {
                    at = i;
                    break;
                }
            }
            if (at < 0 && a.Count != b.Count)
                at = n;
            if (at < 0)
                return result.Set("identical", true);

            Event ea = at < a.Count ? a[at] : null;
            Event eb = at < b.Count ? b[at] : null;
            result.Set("identical", false)
                  .Set("index", at)
                  .Set("seqA", ea != null ? ea.Seq : -1)
                  .Set("seqB", eb != null ? eb.Seq : -1)
                  .Set("a", ea != null ? ea.Raw : null)
                  .Set("b", eb != null ? eb.Raw : null)
                  .Set("contextA", Context(a, at))
                  .Set("contextB", Context(b, at))
                  .Set("castStagesA", StagesAround(dirA, job, a, at))
                  .Set("castStagesB", StagesAround(dirB, job, b, at));
            return result;
        }

        private static string RunDir(string run)
        {
            if (string.IsNullOrWhiteSpace(run))
                throw new ArgumentException("run id is empty");
            return Directory.Exists(run) ? run : Path.Combine(ScenarioRunner.ScenariosDir, run);
        }

        private sealed class Event
        {
            public int Seq;
            public string Raw;
            public string Normalized;
        }

        private static List<Event> ReadTrace(string path, HashSet<string> ignore, bool dropSeqRefs)
        {
            var events = new List<Event>();
            foreach (string line in File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                List<KeyValuePair<string, string>> fields = ParseTopLevel(line);
                string ev = Unquote(Find(fields, "ev"));
                if (ev == null || UnhashedEvents.Contains(ev) || ignore.Contains(ev))
                    continue;
                var sb = new StringBuilder(line.Length);
                foreach (KeyValuePair<string, string> kv in fields)
                {
                    if (Json.IsHashExcluded(kv.Key) || (dropSeqRefs && SeqReferenceFields.Contains(kv.Key)))
                        continue;
                    sb.Append(kv.Key).Append('=').Append(kv.Value).Append('\u001f');
                }
                int.TryParse(Find(fields, "seq"), out int seq);
                events.Add(new Event { Seq = seq, Raw = line, Normalized = sb.ToString() });
            }
            return events;
        }

        private static List<string> Context(List<Event> events, int at)
        {
            var list = new List<string>();
            for (int i = Math.Max(0, at - ContextEvents); i < Math.Min(at, events.Count); i++)
                list.Add(events[i].Raw);
            return list;
        }

        /// <summary>
        /// The cast stages marked after the first context event and before the event that follows the divergence
        /// (each stage line's afterSeq is the seq of the last trace event before it).
        /// </summary>
        private static List<string> StagesAround(string runDir, string job, List<Event> events, int at)
        {
            var list = new List<string>();
            string path = Path.Combine(runDir, job, ScenarioRunner.CastStagesFileName);
            if (!File.Exists(path))
                return list;
            int fromIndex = Math.Max(0, at - ContextEvents);
            int lo = fromIndex < events.Count ? events[fromIndex].Seq : int.MaxValue;
            int hi = at < events.Count ? events[at].Seq : int.MaxValue;
            if (events.Count > 0 && at >= events.Count)
                lo = Math.Min(lo, events[events.Count - 1].Seq);
            foreach (string line in File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                if (!int.TryParse(Find(ParseTopLevel(line), "afterSeq"), out int afterSeq))
                    continue;
                if (afterSeq >= lo && afterSeq <= hi)
                    list.Add(line);
            }
            return list;
        }

        private static string Find(List<KeyValuePair<string, string>> fields, string key)
        {
            foreach (KeyValuePair<string, string> kv in fields)
                if (kv.Key == key)
                    return kv.Value;
            return null;
        }

        private static string Unquote(string raw)
        {
            if (raw == null || raw.Length < 2 || raw[0] != '"')
                return null;
            return raw.Substring(1, raw.Length - 2);
        }

        /// <summary>The top-level fields of one JSON object line, in order: key and raw value text (strings keep their quotes).</summary>
        private static List<KeyValuePair<string, string>> ParseTopLevel(string line)
        {
            var fields = new List<KeyValuePair<string, string>>();
            int i = line.IndexOf('{');
            if (i < 0)
                return fields;
            i++;
            while (i < line.Length)
            {
                while (i < line.Length && (line[i] == ',' || char.IsWhiteSpace(line[i])))
                    i++;
                if (i >= line.Length || line[i] == '}')
                    break;
                int keyEnd = SkipString(line, i);
                string key = line.Substring(i + 1, keyEnd - i - 2);
                i = keyEnd;
                while (i < line.Length && (line[i] == ':' || char.IsWhiteSpace(line[i])))
                    i++;
                int start = i;
                int depth = 0;
                while (i < line.Length)
                {
                    char c = line[i];
                    if (c == '"')
                    {
                        i = SkipString(line, i);
                        continue;
                    }
                    if (c == '{' || c == '[')
                        depth++;
                    else if (c == '}' || c == ']')
                    {
                        if (depth == 0)
                            break;
                        depth--;
                    }
                    else if (c == ',' && depth == 0)
                        break;
                    i++;
                }
                fields.Add(new KeyValuePair<string, string>(key, line.Substring(start, i - start)));
            }
            return fields;
        }

        /// <summary>The index just past the JSON string that opens at <paramref name="i"/>.</summary>
        private static int SkipString(string s, int i)
        {
            i++;
            while (i < s.Length)
            {
                if (s[i] == '\\')
                {
                    i += 2;
                    continue;
                }
                if (s[i] == '"')
                    return i + 1;
                i++;
            }
            return s.Length;
        }
    }
}
#endif
