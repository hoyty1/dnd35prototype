#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Tests.Scenarios
{
    /// <summary>
    /// Runs queued scenario runs, each in its own fresh Play session (docs/TESTING.md 3.5). The queue lives in
    /// <see cref="SessionState"/>, so it survives the domain reload of every Play-mode entry (Enter Play Mode Options
    /// are off). Driven by <see cref="EditorApplication.playModeStateChanged"/>:
    /// EnteredPlayMode with a queued head starts it (<see cref="ScenarioHarness.Start(string,string,string)"/>) once
    /// GameManager is ready; the run's end (<see cref="ScenarioRunner.RunEnded"/>, also after an Error outcome or an
    /// Abort) records the summary path, pops the head and exits Play mode; EnteredEditMode with a non-empty queue
    /// enters Play mode again. Leaving Play mode in the middle of a run (by hand or a crash) records the head as
    /// interrupted, drops it and pauses the batch until <see cref="Resume"/> when entries remain. A domain reload
    /// is reconciled after the fact (<see cref="Reconcile"/>): one in Play mode that killed the run (Recompile And
    /// Continue Playing) is handled like an interruption and Play mode exits; one in edit mode that dropped the pending
    /// Play-mode entry (a recompile on leaving Play mode) enters Play mode again. Use it through
    /// <see cref="ScenarioHarness.QueueFresh"/>, <see cref="ScenarioHarness.Poll"/> and <see cref="ScenarioHarness.ClearQueue"/>.
    /// Progress is also written to Logs/Scenarios/batch.json for polling without the MCP.
    /// </summary>
    [InitializeOnLoad]
    public static class ScenarioBatchDriver
    {
        public const int MaxQueue = 50;
        /// <summary>Real seconds to wait for a ready GameManager after entering Play mode.</summary>
        public const double ReadyTimeoutSeconds = 60;

        private const string QueueKey = "ScenarioBatch.Queue";
        private const string ResultsKey = "ScenarioBatch.Results";
        private const string CurrentKey = "ScenarioBatch.Current";
        private const string PausedKey = "ScenarioBatch.Paused";
        private const string BatchIdKey = "ScenarioBatch.Id";
        private const string SoakReportKey = "ScenarioBatch.SoakReport";
        private const char Sep = '|';

        private static double _waitStart;
        private static bool _waiting;

        static ScenarioBatchDriver()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            ScenarioRunner.RunEnded -= OnRunEnded;
            ScenarioRunner.RunEnded += OnRunEnded;
            // Every domain reload runs this constructor; EditorApplication.update subscriptions and the runner's
            // coroutine do not survive one, so check the batch once the editor has settled.
            EditorApplication.delayCall -= Reconcile;
            EditorApplication.delayCall += Reconcile;
        }

        /// <summary>
        /// After a domain reload: a current entry whose run is gone (the reload killed it in Play mode) is recorded as
        /// interrupted, dropped and, when entries remain, pauses the batch, and Play mode exits; in edit mode a queued,
        /// unpaused batch with no current entry enters Play mode again (the reload dropped EnterPlayModeSoon's tick).
        /// </summary>
        private static void Reconcile()
        {
            try
            {
                string current = SessionState.GetString(CurrentKey, "");
                bool runLive = ScenarioRunner.Instance != null && ScenarioRunner.Instance.State == ScenarioRunner.RunState.Running;
                if (current.Length > 0 && !runLive && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    RecordInterrupted(current, "a domain reload ended the run (edit mode)");
                }
                else if (current.Length > 0 && !runLive && Application.isPlaying)
                {
                    RecordInterrupted(current, "a domain reload ended the run during Play mode (script recompile)");
                    ExitPlayModeSoon();
                    return;
                }
                if (!EditorApplication.isPlayingOrWillChangePlaymode && Queue().Count > 0 && !IsPaused
                    && SessionState.GetString(CurrentKey, "").Length == 0)
                    EnterPlayModeSoon();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ScenarioBatch] reconcile: " + ex);
            }
        }

        /// <summary>Records the current entry as interrupted, drops it, pauses when entries remain (otherwise merges the soak report) and writes batch.json.</summary>
        private static void RecordInterrupted(string current, string reason)
        {
            string[] f = current.Split('\t');
            string entry = f.Length > 1 ? f[1] : current;
            string runId = f[0];
            AddResult(entry, runId, "INTERRUPTED", SummaryPathOf(runId), reason);
            PopHead(entry);
            SessionState.EraseString(CurrentKey);
            if (Queue().Count > 0)
                SessionState.SetString(PausedKey, "a run was interrupted (" + entry + "): " + reason + "; call ScenarioHarness.ResumeQueue() or ClearQueue()");
            else
                WriteSoakReport();
            WriteBatchFile();
        }

        // ── Public API (ScenarioHarness forwards here) ───────────────────

        /// <summary>
        /// Queues one fresh Play session per comma-separated filter and enters Play mode (edit mode only). Seeds and
        /// options apply to every entry, as for ScenarioHarness.Start. Returns JSON.
        /// </summary>
        public static string QueueFresh(string filters, string seeds, string options)
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                return Error("QueueFresh works in edit mode: exit Play mode first");
            if (EditorApplication.isCompiling)
                return Error("scripts are compiling; try again when they are done");
            // Fresh sessions depend on the domain reload at every Play-mode entry (static runner and suite state).
            if (EditorSettings.enterPlayModeOptionsEnabled
                && (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0)
                return Error("Enter Play Mode Options disable the domain reload, so the sessions would not be fresh; turn Reload Domain back on (Project Settings > Editor) or use ScenarioHarness.Start");

            if ((seeds ?? "").IndexOf(Sep) >= 0 || (options ?? "").IndexOf(Sep) >= 0)
                return Error("seeds and options must not contain '" + Sep + "'");
            // perSession=N is the driver's own option: split the seeds into sessions of N seeds.
            int perSession = 0;
            var kept = new List<string>();
            foreach (string part in (options ?? "").Split(';'))
            {
                string p = part.Trim();
                if (p.Length == 0)
                    continue;
                if (p.StartsWith("persession=", StringComparison.OrdinalIgnoreCase))
                {
                    if (!int.TryParse(p.Substring("persession=".Length), out perSession) || perSession < 1)
                        return Error("perSession must be a positive integer");
                    continue;
                }
                kept.Add(p);
            }
            options = string.Join(";", kept);
            RunOptions parsed = RunOptions.Parse(options, out string optError);
            if (parsed == null)
                return Error(optError);
            if (parsed.Keep)
                return Error("keep cannot be batched: the driver leaves Play mode as soon as the run ends (use ScenarioHarness.Start)");
            if (perSession > 0 && parsed.Sweep > 0)
                return Error("perSession and sweep cannot be combined (give the seeds instead)");
            string seedError = null;
            List<int> seedList = parsed.Sweep > 0 ? null : ScenarioHarness.ParseSeeds(seeds, out seedError);
            if (parsed.Sweep == 0 && seedList == null)
                return Error(seedError);
            var seedChunks = new List<string>();
            if (perSession > 0)
                for (int i = 0; i < seedList.Count; i += perSession)
                    seedChunks.Add(string.Join(",", seedList.Skip(i).Take(perSession)));
            else
                seedChunks.Add(string.IsNullOrWhiteSpace(seeds) ? "1" : seeds.Trim());

            ScenarioCatalog.Reload();
            var added = new List<string>();
            var problems = new List<string>();
            foreach (string part in (filters ?? "").Split(','))
            {
                string f = part.Trim();
                if (f.Length == 0)
                    continue;
                if (f.IndexOf(Sep) >= 0)
                {
                    problems.Add("filter '" + f + "' contains '" + Sep + "'");
                    continue;
                }
                List<ScenarioDef> defs = ScenarioCatalog.Find(f);
                if (defs.Count == 0)
                {
                    problems.Add("no scenario matches '" + f + "'");
                    continue;
                }
                foreach (string chunk in seedChunks)
                    added.Add(f + Sep + chunk + Sep + options.Trim());
            }
            if (problems.Count > 0)
                return Error(string.Join("; ", problems), ScenarioCatalog.LoadErrors.Count > 0 ? string.Join("; ", ScenarioCatalog.LoadErrors) : null);
            if (added.Count == 0)
                return Error("no filter given");

            List<string> queue = Queue();
            if (queue.Count + added.Count > MaxQueue)
                return Error("the queue holds at most " + MaxQueue + " entries (" + queue.Count + " queued)");
            if (queue.Count == 0)
            {
                // A new batch: earlier results are dropped.
                SessionState.SetString(BatchIdKey, DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture));
                SessionState.EraseString(ResultsKey);
                SessionState.EraseString(SoakReportKey);
            }
            queue.AddRange(added);
            SetQueue(queue);
            SessionState.EraseString(PausedKey);
            WriteBatchFile();
            EnterPlayModeSoon();

            var o = new JsonObj();
            o.Set("batchId", SessionState.GetString(BatchIdKey, "")).Set("queued", added).Set("queueLength", queue.Count)
             .Set("batchPath", BatchFilePath.Replace('\\', '/'))
             .Set("note", "Play mode is entered on the next editor tick; poll ScenarioHarness.Poll() in edit mode or watch batch.json");
            return Json.Serialize(o);
        }

        /// <summary>
        /// Empties the queue and clears a pause; a run in progress finishes and Play mode then exits (its end merges the
        /// soak report). With no run going, the batch's soak report is merged now. Results stay until the next batch.
        /// </summary>
        public static string ClearQueue()
        {
            int n = Queue().Count;
            SessionState.EraseString(QueueKey);
            SessionState.EraseString(PausedKey);
            if (!Application.isPlaying)
                SessionState.EraseString(CurrentKey); // no run can be going in edit mode
            if (SessionState.GetString(CurrentKey, "").Length == 0 && Results().Count > 0)
                WriteSoakReport();
            WriteBatchFile();
            var o = new JsonObj();
            o.Set("cleared", n);
            string soak = SessionState.GetString(SoakReportKey, "");
            if (soak.Length > 0)
                o.Set("soakReport", soak);
            return Json.Serialize(o);
        }

        /// <summary>Restarts a paused batch (after an interrupted run).</summary>
        public static string Resume()
        {
            if (Application.isPlaying)
                return Error("Resume works in edit mode");
            SessionState.EraseString(PausedKey);
            WriteBatchFile();
            if (Queue().Count > 0)
                EnterPlayModeSoon();
            return Json.Serialize(Status());
        }

        /// <summary>The batch state: queue, current entry, pause reason and finished results.</summary>
        public static JsonObj Status()
        {
            List<string> queue = Queue();
            string current = SessionState.GetString(CurrentKey, "");
            string paused = SessionState.GetString(PausedKey, "");
            var o = new JsonObj();
            o.Set("batchId", SessionState.GetString(BatchIdKey, ""))
             .Set("state", paused.Length > 0 ? "paused" : current.Length > 0 ? "running" : queue.Count > 0 ? "queued" : "idle")
             .Set("queueLength", queue.Count)
             .Set("queue", queue);
            if (current.Length > 0)
            {
                string[] f = current.Split('\t');
                string runId = f[0];
                o.Set("currentRunId", runId)
                 .Set("currentEntry", f.Length > 1 ? f[1] : null)
                 .Set("currentStatusPath", Path.Combine(ScenarioRunner.ScenariosDir, runId, "status.json").Replace('\\', '/'));
            }
            if (paused.Length > 0)
                o.Set("paused", paused);
            o.Set("results", Results().Select(ResultJson).ToList());
            string soak = SessionState.GetString(SoakReportKey, "");
            if (soak.Length > 0)
                o.Set("soakReport", soak);
            return o;
        }

        /// <summary>When the batch's runs wrote soak rows, merges them into Logs/Scenarios/soak-&lt;batchId&gt;.json.</summary>
        private static void WriteSoakReport()
        {
            try
            {
                List<string> runIds = Results().Where(f => f.Length > 1 && f[1].Length > 0).Select(f => f[1]).Distinct().ToList();
                if (!runIds.Any(id => File.Exists(Path.Combine(ScenarioRunner.ScenariosDir, id, SoakStats.RowsFile))))
                    return;
                JsonObj report = SoakStats.MergeRuns(runIds);
                string path = Path.Combine(ScenarioRunner.ScenariosDir, "soak-" + SessionState.GetString(BatchIdKey, "batch") + ".json");
                report.Set("batchId", SessionState.GetString(BatchIdKey, ""));
                Json.WriteFileAtomic(path, Json.Serialize(report));
                SessionState.SetString(SoakReportKey, path.Replace('\\', '/'));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ScenarioBatch] soak report: " + ex.Message);
            }
        }

        /// <summary>True when the batch has anything to report (a queue, a current run or results).</summary>
        public static bool HasState => IsActive || Results().Count > 0;

        /// <summary>True while entries are queued or one is running.</summary>
        public static bool IsActive => Queue().Count > 0 || SessionState.GetString(CurrentKey, "").Length > 0;

        /// <summary>The full <see cref="Status"/> while the batch is active; afterwards a compact one (id, state, result and verdict counts, paths).</summary>
        public static JsonObj PollStatus()
        {
            if (IsActive)
                return Status();
            List<string[]> results = Results();
            var counts = new JsonObj();
            foreach (var g in results.GroupBy(r => r.Length > 2 && r[2].Length > 0 ? r[2] : "?"))
                counts.Set(g.Key, g.Count());
            var o = new JsonObj();
            o.Set("batchId", SessionState.GetString(BatchIdKey, ""))
             .Set("state", IsPaused ? "paused" : "idle")
             .Set("results", results.Count)
             .Set("verdicts", counts)
             .Set("warnings", WarningTotal(results) > 0 ? (object)WarningTotal(results) : null)
             .Set("batchPath", BatchFilePath.Replace('\\', '/'));
            string soak = SessionState.GetString(SoakReportKey, "");
            if (soak.Length > 0)
                o.Set("soakReport", soak);
            return o;
        }

        public static string BatchFilePath => Path.Combine(ScenarioRunner.ScenariosDir, "batch.json");

        // ── Play-mode transitions ───────────────────────────────────────

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            try
            {
                switch (change)
                {
                    case PlayModeStateChange.EnteredPlayMode:
                        if (Queue().Count > 0 && !IsPaused && SessionState.GetString(CurrentKey, "").Length == 0)
                        {
                            _waitStart = EditorApplication.timeSinceStartup;
                            _waiting = true;
                            EditorApplication.update -= WaitAndStart;
                            EditorApplication.update += WaitAndStart;
                        }
                        break;

                    case PlayModeStateChange.ExitingPlayMode:
                        _waiting = false;
                        EditorApplication.update -= WaitAndStart;
                        string current = SessionState.GetString(CurrentKey, "");
                        if (current.Length > 0)
                        {
                            // The run did not reach RunEnded: Play mode was left by hand or by a crash. ExitingPlayMode
                            // comes before the runner's OnApplicationQuit, so have it write its summary and soak rows
                            // now, before the soak report merges them.
                            try { ScenarioRunner.Instance?.InterruptNow("Play mode ended"); }
                            catch (Exception ex) { Debug.LogWarning("[ScenarioBatch] interrupt: " + ex.Message); }
                            RecordInterrupted(current, "Play mode ended before the run finished");
                        }
                        break;

                    case PlayModeStateChange.EnteredEditMode:
                        WriteBatchFile();
                        if (Queue().Count > 0 && !IsPaused)
                            EnterPlayModeSoon();
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ScenarioBatch] " + change + ": " + ex);
            }
        }

        private static void WaitAndStart()
        {
            if (!_waiting)
            {
                EditorApplication.update -= WaitAndStart;
                return;
            }
            if (!Application.isPlaying)
                return;
            if (EditorApplication.isPaused)
                EditorApplication.isPaused = false;

            List<string> queue = Queue();
            if (queue.Count == 0 || IsPaused)
            {
                _waiting = false;
                EditorApplication.update -= WaitAndStart;
                if (queue.Count == 0)
                    ExitPlayModeSoon();
                return;
            }

            string head = queue[0];
            GameManager gm = GameManager.Instance;
            bool ready = gm != null && gm.Harness_IsReady() && Time.frameCount > 5;
            if (!ready)
            {
                if (EditorApplication.timeSinceStartup - _waitStart > ReadyTimeoutSeconds)
                {
                    _waiting = false;
                    EditorApplication.update -= WaitAndStart;
                    AddResult(head, null, "ERROR", null, "GameManager not ready " + ReadyTimeoutSeconds + " s after entering Play mode");
                    PopHead(head);
                    WriteBatchFile();
                    ExitPlayModeSoon();
                }
                else
                {
                    EditorApplication.QueuePlayerLoopUpdate();
                }
                return;
            }

            _waiting = false;
            EditorApplication.update -= WaitAndStart;

            string[] parts = head.Split(Sep);
            string filter = parts[0];
            string seeds = parts.Length > 1 ? parts[1] : "1";
            string options = parts.Length > 2 ? parts[2] : "";
            string response;
            try
            {
                response = ScenarioHarness.Start(filter, seeds, options);
            }
            catch (Exception ex)
            {
                response = null;
                AddResult(head, null, "ERROR", null, "Start threw " + ex.GetType().Name + ": " + ex.Message);
            }

            string runId = ScenarioRunner.Instance != null && ScenarioRunner.Instance.State == ScenarioRunner.RunState.Running
                ? ScenarioRunner.Instance.RunId : null;
            if (response != null && runId == null)
                AddResult(head, null, "ERROR", null, "Start refused: " + response);
            if (runId == null)
            {
                PopHead(head);
                WriteBatchFile();
                ExitPlayModeSoon();
                return;
            }
            SessionState.SetString(CurrentKey, runId + "\t" + head);
            WriteBatchFile();
        }

        private static void OnRunEnded(ScenarioRunner runner)
        {
            string current = SessionState.GetString(CurrentKey, "");
            if (current.Length == 0 || runner == null)
                return;
            string[] f = current.Split('\t');
            if (f[0] != runner.RunId)
                return;
            string entry = f.Length > 1 ? f[1] : current;
            AddResult(entry, runner.RunId, runner.RunVerdict ?? "?", SummaryPathOf(runner.RunId), runner.SummaryError, runner.RunWarnings.Count);
            PopHead(entry);
            SessionState.EraseString(CurrentKey);
            if (Queue().Count == 0)
                WriteSoakReport();
            WriteBatchFile();
            ExitPlayModeSoon();
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private static bool IsPaused => SessionState.GetString(PausedKey, "").Length > 0;

        private static void EnterPlayModeSoon()
        {
            void Enter()
            {
                EditorApplication.update -= Enter;
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    EditorApplication.update += Enter; // try again next tick
                    return;
                }
                if (!EditorApplication.isPlayingOrWillChangePlaymode && Queue().Count > 0 && !IsPaused)
                    EditorApplication.EnterPlaymode();
            }
            EditorApplication.update -= Enter;
            EditorApplication.update += Enter;
        }

        private static void ExitPlayModeSoon()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlaying)
                    EditorApplication.ExitPlaymode();
            };
        }

        private static List<string> Queue()
        {
            string raw = SessionState.GetString(QueueKey, "");
            return raw.Length == 0 ? new List<string>() : raw.Split('\n').Where(l => l.Length > 0).ToList();
        }

        private static void SetQueue(List<string> queue)
        {
            if (queue.Count == 0)
                SessionState.EraseString(QueueKey);
            else
                SessionState.SetString(QueueKey, string.Join("\n", queue));
        }

        /// <summary>Removes the head when it is <paramref name="entry"/> (a ClearQueue in between leaves nothing to pop).</summary>
        private static void PopHead(string entry)
        {
            List<string> queue = Queue();
            if (queue.Count > 0 && queue[0] == entry)
            {
                queue.RemoveAt(0);
                SetQueue(queue);
            }
        }

        private static List<string[]> Results()
        {
            string raw = SessionState.GetString(ResultsKey, "");
            return raw.Length == 0 ? new List<string[]>() : raw.Split('\n').Where(l => l.Length > 0).Select(l => l.Split('\t')).ToList();
        }

        /// <summary>Sum of the runs' summary warning counts (field 6 of a result line).</summary>
        private static int WarningTotal(List<string[]> results)
            => results.Sum(r => r.Length > 6 && int.TryParse(r[6], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int w) ? w : 0);

        /// <summary>Records a run's result; <paramref name="warnings"/> is the run's summary warning count (for example a combat-log leak), 0 when none or unknown.</summary>
        private static void AddResult(string entry, string runId, string verdict, string summaryPath, string error, int warnings = 0)
        {
            string Clean(string s) => (s ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
            string line = string.Join("\t", Clean(entry), Clean(runId), Clean(verdict), Clean(summaryPath), Clean(error),
                DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                warnings > 0 ? warnings.ToString(System.Globalization.CultureInfo.InvariantCulture) : "");
            string raw = SessionState.GetString(ResultsKey, "");
            SessionState.SetString(ResultsKey, raw.Length == 0 ? line : raw + "\n" + line);
        }

        private static JsonObj ResultJson(string[] f)
        {
            string Get(int i) => i < f.Length && f[i].Length > 0 ? f[i] : null;
            string entry = Get(0) ?? "";
            string[] parts = entry.Split(Sep);
            var o = new JsonObj();
            o.Set("filter", parts[0]).Set("seeds", parts.Length > 1 ? parts[1] : null).Set("options", parts.Length > 2 && parts[2].Length > 0 ? parts[2] : null)
             .Set("runId", Get(1)).Set("verdict", Get(2)).Set("summaryPath", Get(3)).Set("error", Get(4)).Set("utc", Get(5))
             .Set("warnings", int.TryParse(Get(6), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int warnings) && warnings > 0 ? (object)warnings : null)
             .Set("summaryExists", Get(3) != null && File.Exists(Get(3)));
            return o;
        }

        private static string SummaryPathOf(string runId)
            => string.IsNullOrEmpty(runId) ? null : Path.Combine(ScenarioRunner.ScenariosDir, runId, "summary.json").Replace('\\', '/');

        private static void WriteBatchFile()
        {
            try
            {
                Json.WriteFileAtomic(BatchFilePath, Json.Serialize(Status().Set("heartbeatUtc", DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture))));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ScenarioBatch] batch.json: " + ex.Message);
            }
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
