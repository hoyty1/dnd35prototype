#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.Scenarios
{
    /// <summary>
    /// Public entry points for the scenario harness, meant to be called from a Unity MCP CommandScript in Play mode
    /// (docs/TESTING.md 3.4). Every method returns compact JSON and never waits: Start schedules the run on the
    /// ScenarioRunner object and returns; poll with <see cref="Poll"/> or watch Logs/Scenarios/&lt;runId&gt;/status.json.
    /// </summary>
    public static class ScenarioHarness
    {
        private static int _lastPollFrame = -1;

        /// <summary>The catalog scenarios whose id matches <paramref name="filter"/> (comma-separated globs).</summary>
        public static string List(string filter = "*")
        {
            List<ScenarioDef> defs = ScenarioCatalog.Find(filter);
            var list = new List<JsonObj>();
            foreach (ScenarioDef d in defs)
            {
                var o = new JsonObj();
                o.Set("id", d.Id).Set("title", d.Title).Set("tags", d.Tags).Set("covers", d.Covers).Set("maxRounds", d.MaxRounds)
                 .Set("actors", d.Actors.Select(a => a.Key + ":" + a.Source + "@" + (a.GamePlaced ? "game" : a.Pos.x + "," + a.Pos.y) + "/" + a.Control.ToString().ToLowerInvariant()).ToList())
                 .Set("expectations", d.Expectations.Count);
                list.Add(o);
            }
            var r = new JsonObj();
            r.Set("count", list.Count).Set("scenarios", list);
            if (ScenarioCatalog.LoadErrors.Count > 0)
                r.Set("loadErrors", ScenarioCatalog.LoadErrors.ToList());
            return Json.Serialize(r);
        }

        /// <summary>
        /// Starts a run of the catalog scenarios matching <paramref name="filter"/>.
        /// Seeds: "1", "1-5", "1,3,7" (mixed is fine). Options: "key=value;..." with mode=fast|watch, maxRounds=N,
        /// repeat=N (compares the trace hash of each repetition per seed), sweep=N (seeds 1..N, aggregated per id),
        /// record=dice, keep (stop after the first job and skip its cleanup so a human can look), wallCap=seconds.
        /// </summary>
        public static string Start(string filter, string seeds = "1", string options = "")
        {
            ScenarioCatalog.Reload();
            List<ScenarioDef> defs = ScenarioCatalog.Find(filter);
            if (defs.Count == 0)
                return Error("no scenario matches '" + filter + "'", ScenarioCatalog.LoadErrors.Count > 0 ? string.Join("; ", ScenarioCatalog.LoadErrors) : null);
            return StartDefs(defs, filter, seeds, options, ScenarioCatalog.LoadErrors.ToList());
        }

        /// <summary>Starts a run of an ad-hoc definition (for example one built inline in a CommandScript).</summary>
        public static string Start(ScenarioDef def, string seeds = "1", string options = "")
        {
            if (def == null)
                return Error("no definition");
            List<string> problems = def.Validate();
            if (problems.Count > 0)
                return Error("invalid scenario: " + string.Join("; ", problems));
            return StartDefs(new List<ScenarioDef> { def }, "adhoc:" + def.Id, seeds, options, null);
        }

        private static string StartDefs(List<ScenarioDef> defs, string filter, string seeds, string options, List<string> loadErrors)
        {
            if (!Application.isPlaying)
                return Error("Play mode required (EditorApplication.EnterPlaymode, then call Start in a later command)");
            if (ScenarioRunner.Instance != null && ScenarioRunner.Instance.State == ScenarioRunner.RunState.Running)
                return Error("a run is in progress: " + ScenarioRunner.Instance.RunId);

            RunOptions opts = RunOptions.Parse(options, out string optError);
            if (opts == null)
                return Error(optError);
            string seedError = null;
            List<int> seedList = opts.Sweep > 0 ? Enumerable.Range(1, opts.Sweep).ToList() : ParseSeeds(seeds, out seedError);
            if (seedList == null)
                return Error(seedError);

            string pre = ScenarioRunner.Preflight(GameManager.Instance);
            if (pre != null)
            {
                var r = new JsonObj();
                r.Set("error", pre).Set("needsFresh", pre.StartsWith("needsFresh", StringComparison.Ordinal));
                return Json.Serialize(r);
            }

            if (ScenarioRunner.Instance != null)
                Object.Destroy(ScenarioRunner.Instance.gameObject);
            var go = new GameObject("ScenarioRunner");
            Object.DontDestroyOnLoad(go);
            ScenarioRunner runner = go.AddComponent<ScenarioRunner>();
            runner.Begin(defs, seedList, opts, filter, opts.Sweep > 0 ? "1-" + opts.Sweep : seeds, loadErrors);

            var o = new JsonObj();
            o.Set("runId", runner.RunId).Set("jobs", runner.Jobs.Count).Set("scenarios", defs.Select(d => d.Id).ToList())
             .Set("seeds", seedList).Set("options", opts.ToString())
             .Set("statusPath", Path.Combine(runner.RunDir, "status.json").Replace('\\', '/'))
             .Set("summaryPath", Path.Combine(runner.RunDir, "summary.json").Replace('\\', '/'));
            // Otherwise a catalog source that threw would drop its remaining scenarios silently.
            if (loadErrors != null && loadErrors.Count > 0)
                o.Set("loadErrors", loadErrors);
            return Json.Serialize(o);
        }

        /// <summary>
        /// The current run's state (never waits). Also unpauses the editor if it is paused (a pause right after a
        /// logged error is Error Pause and is not counted; a second other pause in one job ends it as Paused) and
        /// queues a player-loop update in case the editor is unfocused.
        /// </summary>
        public static string Poll()
        {
            ScenarioRunner runner = ScenarioRunner.Instance;
            if (runner == null)
            {
                var idle = new JsonObj();
                idle.Set("state", "idle").Set("playing", Application.isPlaying).Set("needsFresh", ScenarioRunner.SessionDirty);
                if (ScenarioBatchDriver.HasState)
                    idle.Set("batch", ScenarioBatchDriver.PollStatus());
                return Json.Serialize(idle);
            }

            if (EditorApplication.isPaused && runner.State == ScenarioRunner.RunState.Running)
                runner.HandlePause();
            EditorApplication.QueuePlayerLoopUpdate();

            JsonObj o = runner.StatusObject();
            o.Set("framesSinceLastPoll", _lastPollFrame < 0 ? 0 : Time.frameCount - _lastPollFrame);
            _lastPollFrame = Time.frameCount;
            if (ScenarioBatchDriver.HasState)
                o.Set("batch", ScenarioBatchDriver.PollStatus());
            return Json.Serialize(o);
        }

        /// <summary>
        /// Edit mode only: queues one fresh Play session per comma-separated filter (seeds and options as for
        /// <see cref="Start(string,string,string)"/>, applied to each) and enters Play mode. Each session starts its
        /// run when GameManager is ready, records the summary path and exits Play mode; the next entry gets a new
        /// session. Option perSession=N (handled here, not passed to Start) also splits the seeds into sessions of
        /// N seeds each, for long soaks (fresh sessions keep state from piling up); the batch then merges the soak
        /// statistics of its runs into Logs/Scenarios/soak-&lt;batchId&gt;.json. At most 50 entries. Poll in edit mode with <see cref="Poll"/> (field "batch") or read
        /// Logs/Scenarios/batch.json. See docs/TESTING.md 3.5.
        /// </summary>
        public static string QueueFresh(string filters, string seeds = "1", string options = "")
            => ScenarioBatchDriver.QueueFresh(filters, seeds, options);

        /// <summary>
        /// Merges the soak statistics of finished runs (comma-separated run ids, each with a soak-rows.tsv), for a soak
        /// split over several fresh Play sessions; writes Logs/Scenarios/soak-report-&lt;time&gt;.json and returns it.
        /// Works in edit and Play mode.
        /// </summary>
        public static string SoakReport(string runIds)
        {
            List<string> ids = (runIds ?? "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            if (ids.Count == 0)
                return Error("no run ids");
            JsonObj report = SoakStats.MergeRuns(ids);
            string path = Path.Combine(ScenarioRunner.ScenariosDir, "soak-report-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture) + ".json");
            report.Set("path", path.Replace('\\', '/'));
            string json = Json.Serialize(report);
            Json.WriteFileAtomic(path, json);
            return json;
        }

        /// <summary>Empties the fresh-session queue (a run in progress still finishes, then Play mode exits).</summary>
        public static string ClearQueue() => ScenarioBatchDriver.ClearQueue();

        /// <summary>Restarts a batch paused by an interrupted run (edit mode).</summary>
        public static string ResumeQueue() => ScenarioBatchDriver.Resume();

        /// <summary>The summary of <paramref name="runId"/> (null: the current or last run in this Play session).</summary>
        public static string Result(string runId = null)
        {
            ScenarioRunner runner = ScenarioRunner.Instance;
            if (string.IsNullOrEmpty(runId))
            {
                if (runner == null)
                    return Error("no run in this Play session; pass a runId");
                if (runner.State == ScenarioRunner.RunState.Running)
                    return Poll();
                return runner.SummaryJson ?? Error("summary missing");
            }

            string path = Path.Combine(ScenarioRunner.ScenariosDir, runId, "summary.json");
            return File.Exists(path) ? File.ReadAllText(path) : Error("no summary at " + path.Replace('\\', '/'));
        }

        /// <summary>Stops the current run after the current job's cleanup; the job ends as Halted.</summary>
        public static string Abort(string reason = "abort")
        {
            ScenarioRunner runner = ScenarioRunner.Instance;
            if (runner == null || runner.State != ScenarioRunner.RunState.Running)
                return Error("no run in progress");
            runner.AbortRequested = true;
            runner.AbortReason = reason;
            var o = new JsonObj();
            o.Set("runId", runner.RunId).Set("aborting", true).Set("reason", reason);
            return Json.Serialize(o);
        }

        /// <summary>Parses "1", "1-5", "1,3,7" or mixes; null with an error on bad input.</summary>
        public static List<int> ParseSeeds(string text, out string error)
        {
            error = null;
            var seeds = new List<int>();
            foreach (string part in (string.IsNullOrWhiteSpace(text) ? "1" : text).Split(','))
            {
                string p = part.Trim();
                if (p.Length == 0)
                    continue;
                int dash = p.IndexOf('-', 1);
                if (dash > 0)
                {
                    if (!int.TryParse(p.Substring(0, dash), out int a) || !int.TryParse(p.Substring(dash + 1), out int b) || b < a || b - a > 500)
                    {
                        error = "bad seed range '" + p + "'";
                        return null;
                    }
                    for (int s = a; s <= b; s++)
                        seeds.Add(s);
                }
                else if (int.TryParse(p, out int single))
                {
                    seeds.Add(single);
                }
                else
                {
                    error = "bad seed '" + p + "'";
                    return null;
                }
            }
            if (seeds.Count == 0)
            {
                error = "no seeds";
                return null;
            }
            return seeds;
        }

        private static string Error(string message, string detail = null)
        {
            var o = new JsonObj();
            o.Set("error", message);
            if (detail != null)
                o.Set("detail", detail);
            return Json.Serialize(o);
        }
    }
}
#endif
