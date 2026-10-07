#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Random = UnityEngine.Random;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Tests.Runner
{
    /// <summary>Which editor mode a suite must run in (from tools/tests/static-suites.json).</summary>
    public enum SuiteNeeds { Either, Play, Edit, Unknown }

    /// <summary>One discovered static suite: a non-nested type named *Tests with a static RunAll() or RunAllTests().</summary>
    public sealed class SuiteInfo
    {
        public Type Type;
        public string FullName;
        public string EntryName;
        public Action Run;
    }

    [Serializable]
    public class KnownFailure
    {
        /// <summary>Ordinal prefix of a normalized failure label.</summary>
        public string match = "";
        /// <summary>Issue ID that explains the failure (for example TST-027).</summary>
        public string issue = "";
    }

    [Serializable]
    public class BaselineSuite
    {
        public string type = "";
        /// <summary>play, edit, either or unknown.</summary>
        public string needs = "unknown";
        /// <summary>Non-empty: the suite is not run (unless all=true); the text says why.</summary>
        public string skip = "";
        /// <summary>Exception type name the suite is known to throw (for example TargetParameterCountException).</summary>
        public string expectThrow = "";
        public int passed;
        public int failed;
        /// <summary>Unmatched failures tolerated before the suite counts as a regression.</summary>
        public int flakyFailures;
        public List<KnownFailure> knownFailures = new List<KnownFailure>();
        public string notes = "";
    }

    [Serializable]
    public class StaticBaseline
    {
        public int version = 1;
        public int seed = 20261007;
        public List<BaselineSuite> suites = new List<BaselineSuite>();
    }

    public class StaticRunOptions
    {
        /// <summary>Comma list; a suite runs when its full name contains any entry (case-insensitive).</summary>
        public string Filter = "";
        /// <summary>Ignore the mode gate and baseline skips; suites run in the wrong mode are marked modeMismatch.</summary>
        public bool All;
        /// <summary>Seed base; null uses the baseline seed, 0 leaves the global RNG unseeded.</summary>
        public int? Seed;
        /// <summary>Forward suite logs to the Console as well as capturing them.</summary>
        public bool Echo;
        public bool CleanupLeaks = true;
        /// <summary>Stop starting new suites after this many seconds (0 = no limit); resume with After.</summary>
        public float MaxSeconds;
        /// <summary>Resume: run only suites whose full name sorts after this one, merging into the existing report.</summary>
        public string After = "";
        /// <summary>Also write Logs/TestRunner/static-baseline-proposed.json.</summary>
        public bool WriteBaseline;
        /// <summary>Run even when a script under Assets is newer than the loaded code (see the STALE_CODE refusal).</summary>
        public bool AllowStaleCode;
    }

    [Serializable]
    public class RunTotals
    {
        public int discovered;
        public int run;
        public int passedSuites;
        public int failedSuites;
        public int errorSuites;
        public int skippedSuites;
        public int assertionsPassed;
        public int assertionsFailed;
        public int regressions;
        public int stale;
    }

    [Serializable]
    public class BaselineDiff
    {
        public bool hasEntry;
        /// <summary>OK, REGRESSION, STALE or NEW (no baseline entry).</summary>
        public string verdict = "";
        public List<string> knownSeen = new List<string>();
        public List<string> unexpected = new List<string>();
        public List<string> staleKnown = new List<string>();
        public int unexplainedFailures;
        public bool unexpectedThrow;
        public bool expectThrowMissing;
        public int baselinePassed;
        public int baselineFailed;
        public int assertionDelta;
        public List<string> warnings = new List<string>();
    }

    [Serializable]
    public class SuiteResult
    {
        public string type = "";
        public string needs = "";
        /// <summary>pass, fail, error or skipped.</summary>
        public string status = "";
        public string skipReason = "";
        public bool modeMismatch;
        /// <summary>fields (_passed/_failed), summary (last "N passed, M failed" line), markers or none.</summary>
        public string countSource = "";
        public int passed;
        public int failed;
        public bool noSummary;
        public int seed;
        public long durationMs;
        public string exceptionType = "";
        public string exceptionMessage = "";
        public List<string> exceptionStack = new List<string>();
        public List<string> failures = new List<string>();
        /// <summary>Parallel to failures: how many FAIL lines carried each label (a repeated label counts each time).</summary>
        public List<int> failureCounts = new List<int>();
        public int failureLabelsDropped;
        public int errorLogCount;
        public List<string> errorLogSamples = new List<string>();
        /// <summary>First names of the leaked root objects (capped); leakedCount has the total.</summary>
        public List<string> leaked = new List<string>();
        /// <summary>Every new root object that was neither a kept singleton nor a known leak.</summary>
        public int leakedCount;
        /// <summary>Leaks the game is known to cause, aggregated, for example "PooledLogMsg x150 (UI-001)".</summary>
        public List<string> knownLeaks = new List<string>();
        /// <summary>Game-owned singletons the suite created and the runner kept, with a count when more than one.</summary>
        public List<string> singletonsCreated = new List<string>();
        public List<string> restored = new List<string>();
        /// <summary>Files the suite created in the project root, each marked "(deleted)" or "(kept)".</summary>
        public List<string> strayFiles = new List<string>();
        public BaselineDiff baseline = new BaselineDiff();
    }

    [Serializable]
    public class StaticRunReport
    {
        public string runId = "";
        public string startedUtc = "";
        public string unityMode = "";
        public bool sceneGameManager;
        public int seedBase;
        public string filter = "";
        public bool all;
        public string after = "";
        /// <summary>runId of the interrupted report whose results were merged (after=).</summary>
        public string resumedFrom = "";
        public float maxSeconds;
        /// <summary>UTC time the scripts of this domain were loaded.</summary>
        public string codeLoadedUtc = "";
        public long durationMs;
        public bool complete;
        /// <summary>OK, STALE, REGRESSION, NO_BASELINE or REFUSED.</summary>
        public string verdict = "";
        /// <summary>
        /// While the pass runs (complete=false): the last suite with a result. If maxSeconds stopped the pass
        /// it stays set after the end; resume with after=&lt;this&gt; and the same filter, seed and all.
        /// </summary>
        public string resumeAfter = "";
        /// <summary>The suite running when the JSON was last written; still set if Unity froze or crashed in it.</summary>
        public string currentSuite = "";
        public string baselinePath = "";
        public string baselineError = "";
        public RunTotals totals = new RunTotals();
        public List<string> newSuites = new List<string>();
        public List<string> missingSuites = new List<string>();
        public List<string> warnings = new List<string>();
        public List<SuiteResult> suites = new List<SuiteResult>();
        /// <summary>Which of json, txt and log this call wrote (not serialized).</summary>
        [NonSerialized] public List<string> filesWritten = new List<string>();
    }

    /// <summary>
    /// Records when scripts were last imported in edit mode. A changed script then compiles (and the domain reloads);
    /// a touched but unchanged one does not, and this time tells the runner's STALE_CODE check that it is up to date.
    /// Imports during Play mode are ignored, because Unity may not recompile them until Play mode ends.
    /// </summary>
    internal sealed class ScriptImportWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (imported.Any(p => p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
                SessionState.SetString(StaticSuiteRunner.ScriptsImportedKey, DateTime.UtcNow.ToString("o"));
        }
    }

    /// <summary>
    /// Runs the hand-rolled static suites under Assets/Scripts/Tests with exception isolation,
    /// RNG and singleton restoration, leak cleanup and a known-failures baseline
    /// (tools/tests/static-suites.json). Results go to Logs/TestRunner/static-&lt;play|edit&gt;.{json,txt,log}.
    /// Use through Tools &gt; DND Tests or RunFromCommand from a Unity MCP CommandScript (docs/TESTING.md 3).
    /// </summary>
    public static class StaticSuiteRunner
    {
        public const string BaselineRelativePath = "tools/tests/static-suites.json";
        private const int MaxFailureLabels = 60;
        private const int MaxLeakNames = 10;
        private const int MaxErrorSamples = 5;
        private const int MaxSummaryChars = 4000;

        /// <summary>
        /// Lazily created game singletons that keep a static reference to themselves. Destroying them would
        /// leave a dangling Instance, so they are reported under singletonsCreated and kept.
        /// </summary>
        private static readonly string[] KeptRootNames =
        {
            "AreaEffectManager", "WindEffectManager", "ExperienceCalculator", "GameSettings", "DebugCommands",
        };

        /// <summary>
        /// Root objects the game is known to leak, by name, with the issue that explains them. They are destroyed
        /// like other leaks but reported as one aggregated knownLeaks entry instead of filling the leaked names.
        /// PooledLogMsg: every combat log call rebuilds CombatLogPanel's pool and orphans the 50 lines prewarmed
        /// at the scene root (UI-001); ObjectPool.Get skips destroyed entries, so destroying them is safe.
        /// </summary>
        private static readonly Dictionary<string, string> KnownLeakIssues = new Dictionary<string, string>
        {
            { "PooledLogMsg", "UI-001" },
        };

        /// <summary>Project-root files a suite is known to write; only these are deleted after a suite.</summary>
        private static readonly string[] DeletableRootFiles = { "phase5_6_test_results.txt" };

        private static readonly Regex IssueId = new Regex(@"\b[A-Z]+-\d+\b", RegexOptions.Compiled);

        private static readonly Regex FailLine = new Regex(
            @"^\s*(\[[A-Za-z0-9_ ]+\]\s*)?(\[FAIL\]|FAIL:|❌|✗)", RegexOptions.Compiled);
        private static readonly Regex PassLine = new Regex(
            @"^\s*(\[[A-Za-z0-9_ ]+\]\s*)?(\[PASS\]|PASS:|✅|✓)", RegexOptions.Compiled);
        private static readonly Regex MarkerStrip = new Regex(
            @"^\s*(\[[A-Za-z0-9_ ]+\]\s*)?((\[FAIL\]|FAIL:|❌|✗)\s*)+", RegexOptions.Compiled);
        private static readonly Regex SummaryLine = new Regex(
            @"(?i)(\d+)\s+passed,\s*(\d+)\s+failed", RegexOptions.Compiled);

        /// <summary>True once a Play-mode pass ran in this domain; the scenario harness refuses to run after it.</summary>
        public static bool RanInPlayModeThisDomain { get; private set; }

        /// <summary>UTC time this script domain was loaded (after a compile, or on entering Play mode).</summary>
        public static DateTime DomainLoadedUtc { get; private set; } = DateTime.UtcNow;

        [InitializeOnLoadMethod]
        private static void RecordDomainLoad() => DomainLoadedUtc = DateTime.UtcNow;

        public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
        public static string ResultsDir => Path.Combine(ProjectRoot, "Logs", "TestRunner");
        public static string BaselinePath => Path.Combine(ProjectRoot, BaselineRelativePath);

        // =====================================================================
        //  Discovery
        // =====================================================================

        public static IReadOnlyList<SuiteInfo> Discover()
        {
            Type[] types;
            try { types = typeof(GameManager).Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }

            var list = new List<SuiteInfo>();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
            foreach (Type t in types)
            {
                if (t.IsNested || t.IsGenericTypeDefinition) continue;
                if (!t.Name.EndsWith("Tests", StringComparison.Ordinal)) continue;
                MethodInfo m = t.GetMethod("RunAll", flags, null, Type.EmptyTypes, null);
                if (m == null || m.ReturnType != typeof(void))
                    m = t.GetMethod("RunAllTests", flags, null, Type.EmptyTypes, null);
                if (m == null || m.ReturnType != typeof(void)) continue;
                list.Add(new SuiteInfo
                {
                    Type = t,
                    FullName = t.FullName,
                    EntryName = m.Name,
                    Run = (Action)Delegate.CreateDelegate(typeof(Action), m)
                });
            }
            list.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));
            return list;
        }

        // =====================================================================
        //  Baseline
        // =====================================================================

        public static StaticBaseline LoadBaseline()
        {
            string error;
            return LoadBaseline(out error);
        }

        private static StaticBaseline LoadBaseline(out string error)
        {
            error = "";
            string path = BaselinePath;
            if (!File.Exists(path)) return null;
            try
            {
                var baseline = JsonUtility.FromJson<StaticBaseline>(File.ReadAllText(path));
                if (baseline == null) { error = "baseline file is empty"; return null; }
                if (baseline.suites == null) baseline.suites = new List<BaselineSuite>();
                foreach (BaselineSuite s in baseline.suites)
                {
                    if (s.knownFailures == null) s.knownFailures = new List<KnownFailure>();
                    foreach (KnownFailure k in s.knownFailures)
                    {
                        if (string.IsNullOrWhiteSpace(k.issue) || k.issue.IndexOf("TODO", StringComparison.OrdinalIgnoreCase) >= 0)
                            error += (error.Length > 0 ? "; " : "") + $"{s.type}: known failure '{k.match}' has no issue ID";
                    }
                    if (!string.IsNullOrEmpty(s.expectThrow) && (string.IsNullOrEmpty(s.notes) || !IssueId.IsMatch(s.notes)
                            || s.notes.IndexOf("TODO", StringComparison.OrdinalIgnoreCase) >= 0))
                        error += (error.Length > 0 ? "; " : "") + $"{s.type}: expectThrow {s.expectThrow} has no issue ID in notes";
                }
                return baseline;
            }
            catch (Exception ex)
            {
                error = "malformed baseline JSON: " + ex.Message;
                return null;
            }
        }

        private static SuiteNeeds ParseNeeds(string needs)
        {
            switch ((needs ?? "").Trim().ToLowerInvariant())
            {
                case "play": return SuiteNeeds.Play;
                case "edit": return SuiteNeeds.Edit;
                case "either": return SuiteNeeds.Either;
                default: return SuiteNeeds.Unknown;
            }
        }

        // =====================================================================
        //  Command entry points
        // =====================================================================

        public static string ListSuites()
        {
            IReadOnlyList<SuiteInfo> suites = Discover();
            string error;
            StaticBaseline baseline = LoadBaseline(out error);
            var byType = baseline == null
                ? new Dictionary<string, BaselineSuite>()
                : baseline.suites.GroupBy(s => s.type).ToDictionary(g => g.Key, g => g.First());

            var sb = new StringBuilder();
            var newSuites = suites.Where(s => !byType.ContainsKey(s.FullName)).Select(s => s.FullName).ToList();
            var discovered = new HashSet<string>(suites.Select(s => s.FullName));
            var missing = byType.Keys.Where(k => !discovered.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
            sb.AppendLine($"discovered={suites.Count} baseline={(baseline == null ? "none" : byType.Count.ToString())} new={newSuites.Count} missing={missing.Count}");
            if (error.Length > 0) sb.AppendLine("baseline: " + error);
            foreach (SuiteInfo s in suites)
            {
                BaselineSuite b;
                byType.TryGetValue(s.FullName, out b);
                string needs = b == null ? "NEW" : b.needs;
                string extra = b != null && !string.IsNullOrEmpty(b.skip) ? "  skip: " + b.skip : "";
                sb.AppendLine($"{needs,-7} {s.FullName}.{s.EntryName}(){extra}");
            }
            foreach (string m in missing) sb.AppendLine("MISSING " + m);
            return sb.ToString();
        }

        /// <summary>
        /// Parses space-separated key=value options (filter, all, seed, echo, cleanupLeaks,
        /// maxSeconds, after, writeBaseline, allowStaleCode), runs, and returns a short summary.
        /// </summary>
        public static string RunFromCommand(string args)
        {
            StaticRunOptions o;
            string parseError;
            if (!TryParseOptions(args, out o, out parseError)) return "REFUSED: " + parseError;
            StaticRunReport report = Run(o);
            return Summarize(report);
        }

        private static bool TryParseOptions(string args, out StaticRunOptions o, out string error)
        {
            o = new StaticRunOptions();
            error = "";
            if (string.IsNullOrWhiteSpace(args)) return true;
            foreach (string token in args.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = token.IndexOf('=');
                string key = (eq < 0 ? token : token.Substring(0, eq)).Trim().ToLowerInvariant();
                string val = eq < 0 ? "true" : token.Substring(eq + 1).Trim();
                switch (key)
                {
                    case "filter": o.Filter = val; break;
                    case "all": o.All = ParseBool(val); break;
                    case "echo": o.Echo = ParseBool(val); break;
                    case "cleanupleaks": o.CleanupLeaks = ParseBool(val); break;
                    case "writebaseline": o.WriteBaseline = ParseBool(val); break;
                    case "allowstalecode": o.AllowStaleCode = ParseBool(val); break;
                    case "after": o.After = val; break;
                    case "seed":
                        int seed;
                        if (!int.TryParse(val, out seed)) { error = "seed must be an integer"; return false; }
                        o.Seed = seed;
                        break;
                    case "maxseconds":
                        float secs;
                        if (!float.TryParse(val, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out secs))
                        { error = "maxSeconds must be a number"; return false; }
                        o.MaxSeconds = secs;
                        break;
                    default:
                        error = "unknown option '" + key + "' (filter, all, seed, echo, cleanupLeaks, maxSeconds, after, writeBaseline, allowStaleCode)";
                        return false;
                }
            }
            return true;
        }

        private static bool ParseBool(string v)
        {
            v = (v ?? "").Trim().ToLowerInvariant();
            return v == "" || v == "true" || v == "1" || v == "yes";
        }

        // =====================================================================
        //  Run
        // =====================================================================

        public static StaticRunReport Run(StaticRunOptions o)
        {
            if (o == null) o = new StaticRunOptions();
            bool playing = EditorApplication.isPlaying;
            string mode = playing ? "play" : "edit";

            var report = new StaticRunReport
            {
                runId = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'") + "-" + mode,
                startedUtc = DateTime.UtcNow.ToString("o"),
                unityMode = mode,
                filter = o.Filter ?? "",
                all = o.All,
                after = o.After ?? "",
                maxSeconds = o.MaxSeconds,
                codeLoadedUtc = DomainLoadedUtc.ToString("o"),
                baselinePath = BaselinePath.Replace('\\', '/'),
            };

            try { Directory.CreateDirectory(ResultsDir); }
            catch (Exception ex) { report.warnings.Add("Could not create " + ResultsDir + ": " + ex.Message); }

            // Refusals. A refused run writes only the TXT, so the JSON of an interrupted pass stays resumable.
            string refusal = null;
            if (EditorApplication.isCompiling) refusal = "Unity is compiling scripts";
            else if (EditorUtility.scriptCompilationFailed) refusal = "scripts have compile errors";
            else if (playing ? !EditorApplication.isPlayingOrWillChangePlaymode : EditorApplication.isPlayingOrWillChangePlaymode)
                refusal = "Unity is entering or leaving Play mode";
            else if (!o.AllowStaleCode) refusal = StaleCodeReason();
            if (refusal != null) return Refuse(report, mode, refusal);

            // Leftover scenario-harness state would force dice or hide logs during the suites.
            string leftover = Tests.Scenarios.ScenarioSessionGuard.CleanUp("StaticSuiteRunner");
            if (leftover != null)
                report.warnings.Add("Scenario harness state was still set before the run and was cleared (" + leftover + ").");

            string baselineError;
            StaticBaseline baseline = LoadBaseline(out baselineError);
            report.baselineError = baselineError;
            if (baselineError.Length > 0) report.warnings.Add("Baseline: " + baselineError);
            var byType = baseline == null
                ? new Dictionary<string, BaselineSuite>()
                : baseline.suites.GroupBy(s => s.type).ToDictionary(g => g.Key, g => g.First());
            report.seedBase = o.Seed ?? (baseline != null ? baseline.seed : 20261007);

            IReadOnlyList<SuiteInfo> discovered = Discover();
            report.totals.discovered = discovered.Count;
            var discoveredNames = new HashSet<string>(discovered.Select(s => s.FullName));
            if (baseline != null)
            {
                report.newSuites = discovered.Where(s => !byType.ContainsKey(s.FullName)).Select(s => s.FullName).ToList();
                report.missingSuites = byType.Keys.Where(k => !discoveredNames.Contains(k))
                    .OrderBy(k => k, StringComparer.Ordinal).ToList();
            }

            // Resume: only from the interrupted report of the same mode and options; keep its suites up to 'after'.
            if (!string.IsNullOrEmpty(report.after))
            {
                StaticRunReport previous = LoadPreviousReport(mode);
                string mismatch = ResumeMismatch(previous, report);
                if (mismatch != null) return Refuse(report, mode, "cannot resume: " + mismatch);
                report.resumedFrom = previous.runId;
                report.suites.AddRange(previous.suites.Where(s => string.CompareOrdinal(s.type, report.after) <= 0));
                if (!string.IsNullOrEmpty(previous.currentSuite) && previous.currentSuite == report.after)
                    report.warnings.Add(previous.currentSuite + " was running when run " + previous.runId
                        + " stopped (froze or crashed) and has no result; add a baseline skip for it.");
            }

            if (playing)
            {
                RanInPlayModeThisDomain = true;
                report.sceneGameManager = GameManager.Instance != null;
                if (!report.sceneGameManager)
                    report.warnings.Add("Play pass without a scene GameManager: suites that rely on it take their fallback branches.");
            }

            string[] filters = (o.Filter ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim()).Where(f => f.Length > 0).ToArray();

            Scene previousActive = SceneManager.GetActiveScene();
            Scene tempScene = default(Scene);
            bool tempCreated = false;
            if (!playing)
            {
                try
                {
                    tempScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    tempCreated = true;
                    SceneManager.SetActiveScene(tempScene);
                }
                catch (Exception ex)
                {
                    if (tempCreated)
                    {
                        try
                        {
                            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
                            EditorSceneManager.CloseScene(tempScene, true);
                        }
                        catch (Exception) { /* reported below */ }
                    }
                    return Refuse(report, mode, "could not create the temporary scene (" + ex.GetType().Name + ": " + ex.Message
                        + "); save or close untitled scenes first");
                }
            }

            // Mark the run as started, so files from an earlier run are not mistaken for this one.
            string lastHandled = report.after;
            report.resumeAfter = lastHandled;
            WriteJson(report, mode);
            WriteRunningText(report, mode);
            TryWrite(Path.Combine(ResultsDir, $"static-{mode}.log"),
                $"RUNNING {report.runId} (this file is complete only when static-{mode}.txt has a verdict)\n", report, "log");

            var fullLog = new StringBuilder();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            Random.State savedRandom = Random.state;
            int ranThisCall = 0;
            bool stoppedEarly = false;

            try
            {
                foreach (SuiteInfo suite in discovered)
                {
                    if (!string.IsNullOrEmpty(report.after) && string.CompareOrdinal(suite.FullName, report.after) <= 0) continue;
                    if (filters.Length > 0 && !filters.Any(f => suite.FullName.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)) continue;

                    BaselineSuite entry;
                    byType.TryGetValue(suite.FullName, out entry);
                    SuiteNeeds needs = entry == null ? SuiteNeeds.Unknown : ParseNeeds(entry.needs);
                    bool modeOk = playing
                        ? needs == SuiteNeeds.Play || needs == SuiteNeeds.Either || needs == SuiteNeeds.Unknown
                        : needs == SuiteNeeds.Edit;

                    var result = new SuiteResult { type = suite.FullName, needs = needs.ToString().ToLowerInvariant() };

                    if (!o.All && entry != null && !string.IsNullOrEmpty(entry.skip))
                    {
                        result.status = "skipped";
                        result.skipReason = "baseline: " + entry.skip;
                        report.suites.Add(result);
                        lastHandled = suite.FullName;
                        continue;
                    }
                    if (!modeOk && !o.All)
                    {
                        result.status = "skipped";
                        result.skipReason = "needs " + result.needs;
                        report.suites.Add(result);
                        lastHandled = suite.FullName;
                        continue;
                    }

                    if (o.MaxSeconds > 0f && ranThisCall > 0 && clock.Elapsed.TotalSeconds >= o.MaxSeconds)
                    {
                        // Resume point: the last suite handled in this call, so after= continues with this one.
                        stoppedEarly = true;
                        break;
                    }

                    // Record the running suite first: if Unity freezes or crashes in it, the JSON names it.
                    report.currentSuite = suite.FullName;
                    report.resumeAfter = lastHandled;
                    report.durationMs = clock.ElapsedMilliseconds;
                    Recompute(report, baseline != null);
                    WriteJson(report, mode);

                    result.modeMismatch = !modeOk;
                    RunOneSuite(suite, entry, o, report.seedBase, playing, result, fullLog);
                    ranThisCall++;
                    lastHandled = suite.FullName;
                    report.suites.Add(result);
                    report.currentSuite = "";
                    report.resumeAfter = lastHandled;
                    report.durationMs = clock.ElapsedMilliseconds;
                    Recompute(report, baseline != null);
                    WriteJson(report, mode);
                }
            }
            finally
            {
                if (tempCreated)
                {
                    try
                    {
                        if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
                        EditorSceneManager.CloseScene(tempScene, true);
                    }
                    catch (Exception ex) { report.warnings.Add("Could not close the temporary scene: " + ex.Message); }
                }
                Random.state = savedRandom;
            }

            report.suites.Sort((a, b) => string.CompareOrdinal(a.type, b.type));
            report.durationMs = clock.ElapsedMilliseconds;
            report.currentSuite = "";
            report.resumeAfter = stoppedEarly ? lastHandled : "";
            report.complete = !stoppedEarly;
            Recompute(report, baseline != null);
            if (!playing && SceneManager.GetActiveScene().isDirty)
                report.warnings.Add("The active scene is dirty after the edit pass; do not save it.");

            WriteJson(report, mode);
            WriteText(report, mode);
            TryWrite(Path.Combine(ResultsDir, $"static-{mode}.log"), fullLog.ToString(), report, "log");
            if (o.WriteBaseline) WriteProposedBaseline(report, baseline);
            return report;
        }

        /// <summary>Marks the report REFUSED and writes only the TXT (the JSON of an interrupted pass is left for after=).</summary>
        private static StaticRunReport Refuse(StaticRunReport report, string mode, string reason)
        {
            report.verdict = "REFUSED";
            report.complete = false;
            report.warnings.Add((mode == "play" ? "Play" : "Edit") + " pass refused: " + reason + ".");
            WriteText(report, mode);
            return report;
        }

        /// <summary>Null when 'previous' is the interrupted report this resume continues; otherwise why it is not.</summary>
        private static string ResumeMismatch(StaticRunReport previous, StaticRunReport current)
        {
            if (previous == null || previous.suites == null)
                return "no readable static-" + current.unityMode + ".json from an interrupted pass";
            if (previous.verdict == "REFUSED" && previous.suites.Count == 0)
                return "the last " + current.unityMode + " report is a refusal";
            if (previous.complete)
                return "run " + previous.runId + " is complete; start a new pass without after=";
            if (current.after != previous.resumeAfter && current.after != previous.currentSuite)
                return $"after={current.after} is neither resumeAfter ({previous.resumeAfter}) nor currentSuite ({previous.currentSuite}) of run {previous.runId}";
            if ((previous.filter ?? "") != current.filter)
                return $"filter differs (previous '{previous.filter}', now '{current.filter}')";
            if (previous.all != current.all)
                return $"all differs (previous {previous.all}, now {current.all})";
            if (previous.seedBase != current.seedBase)
                return $"seed differs (previous {previous.seedBase}, now {current.seedBase})";
            return null;
        }

        /// <summary>The options that continue an interrupted pass.</summary>
        public static string ResumeOptions(StaticRunReport r, string after)
        {
            var sb = new StringBuilder("after=" + after);
            if (!string.IsNullOrEmpty(r.filter)) sb.Append(" filter=").Append(r.filter);
            sb.Append(" seed=").Append(r.seedBase);
            if (r.all) sb.Append(" all=true");
            if (r.maxSeconds > 0f) sb.Append(" maxSeconds=").Append(r.maxSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        internal const string ScriptsImportedKey = "Tests.Runner.StaticSuiteRunner.ScriptsImportedUtc";

        /// <summary>
        /// Non-null when the loaded code may not match the scripts on disk: a script under Assets is newer than both
        /// the compiled Assembly-CSharp.dll and the last edit-mode import of scripts (ScriptImportWatcher), or the dll
        /// was rebuilt after this domain loaded. Whether Unity recompiles an edit made during Play mode depends on the
        /// editor preference Script Changes While Playing, so a Play pass must not assume it. A file whose timestamp
        /// changed without a content change is not reimported by Refresh, so it keeps tripping the check:
        /// allowStaleCode=true overrides once Refresh compiled nothing.
        /// </summary>
        private static string StaleCodeReason()
        {
            try
            {
                string dll = typeof(StaticSuiteRunner).Assembly.Location;
                DateTime compiled = !string.IsNullOrEmpty(dll) && File.Exists(dll) ? File.GetLastWriteTimeUtc(dll) : DomainLoadedUtc;
                const string fix = "exit Play mode, call AssetDatabase.Refresh() in edit mode, wait for the compile and run again "
                    + "(allowStaleCode=true overrides)";
                if (compiled > DomainLoadedUtc.AddSeconds(2))
                    return $"STALE_CODE: Assembly-CSharp.dll was rebuilt at {compiled:o}, after the scripts were loaded at {DomainLoadedUtc:o}; " + fix;

                DateTime reference = compiled;
                DateTime imported;
                if (DateTime.TryParse(SessionState.GetString(ScriptsImportedKey, ""), null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out imported) && imported > reference)
                    reference = imported;

                DateTime newest = DateTime.MinValue;
                string newestPath = null;
                foreach (string f in Directory.EnumerateFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories))
                {
                    DateTime t = File.GetLastWriteTimeUtc(f);
                    if (t > newest) { newest = t; newestPath = f; }
                }
                if (newestPath == null || newest <= reference) return null;
                string rel = newestPath.Substring(ProjectRoot.Length).TrimStart('\\', '/').Replace('\\', '/');
                return $"STALE_CODE: {rel} was written at {newest:o}, after the loaded code was compiled or last checked at {reference:o}; " + fix;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // =====================================================================
        //  One suite
        // =====================================================================

        private static void RunOneSuite(SuiteInfo suite, BaselineSuite entry, StaticRunOptions o, int seedBase,
            bool playing, SuiteResult result, StringBuilder fullLog)
        {
            HashSet<GameObject> rootsBefore = SnapshotRoots(playing);
            HashSet<string> rootFilesBefore = SnapshotProjectRootFiles();
            GameManager gmBefore = GameManager.Instance;
            SquareGrid gridBefore = SquareGrid.Instance;

            if (seedBase != 0)
            {
                result.seed = (int)((uint)seedBase ^ Fnv1a32(suite.FullName));
                Random.InitState(result.seed);
            }

            var capture = new CapturingLogHandler(Debug.unityLogger.logHandler, o.Echo);
            ILogHandler originalHandler = Debug.unityLogger.logHandler;
            Debug.unityLogger.logHandler = capture;
            Application.logMessageReceived += capture.OnNativeLog;

            Exception thrown = null;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                suite.Run();
            }
            catch (Exception ex)
            {
                thrown = ex;
                while (thrown is TargetInvocationException && thrown.InnerException != null) thrown = thrown.InnerException;
            }
            finally
            {
                watch.Stop();
                Application.logMessageReceived -= capture.OnNativeLog;
                Debug.unityLogger.logHandler = originalHandler;
            }
            result.durationMs = watch.ElapsedMilliseconds;

            List<CapturedLine> lines = capture.Snapshot();

            fullLog.AppendLine("### " + suite.FullName);
            foreach (CapturedLine l in lines)
                fullLog.Append('[').Append(l.Type).Append("] ").AppendLine(l.Message);
            if (thrown != null)
                fullLog.AppendLine("[Exception] " + thrown.GetType().Name + ": " + thrown.Message + "\n" + thrown.StackTrace);
            fullLog.AppendLine();

            if (thrown != null)
            {
                result.exceptionType = thrown.GetType().Name;
                result.exceptionMessage = thrown.Message ?? "";
                result.exceptionStack = (thrown.StackTrace ?? "")
                    .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim()).Where(s => s.Length > 0).Take(3).ToList();
            }

            ReadCounts(suite, lines, thrown != null, result);
            CollectFailures(lines, result);

            // Leaks, singletons and stray files.
            CleanupAfterSuite(rootsBefore, playing, o.CleanupLeaks, result);
            if (!ReferenceEquals(GameManager.Instance, gmBefore) && SetStaticInstance(typeof(GameManager), gmBefore))
                result.restored.Add("GameManager.Instance");
            if (!ReferenceEquals(SquareGrid.Instance, gridBefore) && SetStaticInstance(typeof(SquareGrid), gridBefore))
                result.restored.Add("SquareGrid.Instance");
            ReportStrayRootFiles(rootFilesBefore, result);

            result.status = thrown != null ? "error" : result.failed > 0 ? "fail" : "pass";
            Compare(entry, result, thrown);
        }

        private static void ReadCounts(SuiteInfo suite, List<CapturedLine> lines, bool threw, SuiteResult result)
        {
            const BindingFlags f = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
            FieldInfo pf = suite.Type.GetField("_passed", f);
            FieldInfo ff = suite.Type.GetField("_failed", f);

            Match lastSummary = null;
            foreach (CapturedLine l in lines)
            {
                MatchCollection ms = SummaryLine.Matches(l.Message);
                if (ms.Count > 0) lastSummary = ms[ms.Count - 1];
            }

            if (pf != null && ff != null && pf.FieldType == typeof(int) && ff.FieldType == typeof(int))
            {
                result.countSource = "fields";
                result.passed = (int)pf.GetValue(null);
                result.failed = (int)ff.GetValue(null);
                result.noSummary = lastSummary == null && !threw;
                return;
            }
            if (lastSummary != null)
            {
                result.countSource = "summary";
                result.passed = int.Parse(lastSummary.Groups[1].Value);
                result.failed = int.Parse(lastSummary.Groups[2].Value);
                return;
            }
            int pass = 0, fail = 0;
            foreach (CapturedLine l in lines)
            {
                foreach (string line in l.Message.Split('\n'))
                {
                    if (FailLine.IsMatch(line)) fail++;
                    else if (PassLine.IsMatch(line)) pass++;
                }
            }
            result.countSource = pass + fail > 0 ? "markers" : "none";
            result.passed = pass;
            result.failed = fail;
        }

        private static void CollectFailures(List<CapturedLine> lines, SuiteResult result)
        {
            var index = new Dictionary<string, int>();
            var dropped = new HashSet<string>();
            foreach (CapturedLine l in lines)
            {
                bool anyMarker = false;
                foreach (string raw in l.Message.Split('\n'))
                {
                    if (!FailLine.IsMatch(raw)) continue;
                    anyMarker = true;
                    string label = NormalizeLabel(raw);
                    if (label.Length == 0) continue;
                    int i;
                    if (index.TryGetValue(label, out i)) { result.failureCounts[i]++; continue; }
                    if (result.failures.Count < MaxFailureLabels)
                    {
                        index[label] = result.failures.Count;
                        result.failures.Add(label);
                        result.failureCounts.Add(1);
                    }
                    else if (dropped.Add(label)) result.failureLabelsDropped++;
                }
                if (!anyMarker && (l.Type == LogType.Error || l.Type == LogType.Exception || l.Type == LogType.Assert))
                {
                    result.errorLogCount++;
                    if (result.errorLogSamples.Count < MaxErrorSamples)
                    {
                        string first = l.Message.Split('\n')[0].Trim();
                        result.errorLogSamples.Add(first.Length > 200 ? first.Substring(0, 200) : first);
                    }
                }
            }
        }

        /// <summary>Strips the optional [Tag] and the FAIL markers, then trims.</summary>
        public static string NormalizeLabel(string line)
        {
            if (line == null) return "";
            return MarkerStrip.Replace(line, "").Trim();
        }

        // =====================================================================
        //  Baseline comparison
        // =====================================================================

        private static void Compare(BaselineSuite entry, SuiteResult result, Exception thrown)
        {
            BaselineDiff d = result.baseline;
            if (entry == null)
            {
                d.hasEntry = false;
                d.verdict = "NEW";
                return;
            }
            d.hasEntry = true;
            d.baselinePassed = entry.passed;
            d.baselineFailed = entry.failed;

            List<KnownFailure> known = entry.knownFailures ?? new List<KnownFailure>();
            var matchedKnown = new HashSet<KnownFailure>();
            int matchedOccurrences = 0;
            for (int i = 0; i < result.failures.Count; i++)
            {
                string label = result.failures[i];
                int occurrences = i < result.failureCounts.Count ? result.failureCounts[i] : 1;
                KnownFailure k = known.FirstOrDefault(x => !string.IsNullOrEmpty(x.match)
                    && label.StartsWith(x.match, StringComparison.Ordinal));
                if (k != null) { matchedKnown.Add(k); matchedOccurrences += occurrences; }
                else d.unexpected.Add(label);
            }
            foreach (KnownFailure k in known)
            {
                if (matchedKnown.Contains(k)) d.knownSeen.Add(k.match + " (" + k.issue + ")");
                else d.staleKnown.Add(k.match + " (" + k.issue + ")");
            }

            // Failures counted by the suite but not explained by a known label. A known label counts every FAIL line
            // that carried it, so an assertion text used twice is covered; suites that re-print their failures at
            // the end (Crafting, Rod, Phase5) only push the matched count above the counter, which is harmless.
            // Some suites fail without a marker line; those failures stay unexplained.
            d.unexplainedFailures = Math.Max(d.unexpected.Count + result.failureLabelsDropped,
                result.failed - Math.Min(matchedOccurrences, result.failed));

            string expect = entry.expectThrow ?? "";
            if (thrown != null)
                d.unexpectedThrow = expect.Length == 0 || !string.Equals(thrown.GetType().Name, expect, StringComparison.Ordinal);
            else if (expect.Length > 0)
                d.expectThrowMissing = true;

            int total = result.passed + result.failed;
            int baseTotal = entry.passed + entry.failed;
            d.assertionDelta = baseTotal > 0 ? total - baseTotal : 0;
            if (baseTotal > 0 && total < baseTotal)
                d.warnings.Add($"assertions dropped from {baseTotal} to {total}");
            if (result.noSummary) d.warnings.Add("no summary line");

            bool regression = d.unexpectedThrow || d.unexplainedFailures > Math.Max(0, entry.flakyFailures);
            bool stale = d.staleKnown.Count > 0 || d.expectThrowMissing;
            d.verdict = regression ? "REGRESSION" : stale ? "STALE" : "OK";
        }

        private static void Recompute(StaticRunReport report, bool haveBaseline)
        {
            var t = new RunTotals { discovered = report.totals.discovered };
            foreach (SuiteResult s in report.suites)
            {
                switch (s.status)
                {
                    case "pass": t.passedSuites++; t.run++; break;
                    case "fail": t.failedSuites++; t.run++; break;
                    case "error": t.errorSuites++; t.run++; break;
                    default: t.skippedSuites++; break;
                }
                t.assertionsPassed += s.passed;
                t.assertionsFailed += s.failed;
                if (IsRegression(s)) t.regressions++;
                if (s.baseline != null && s.baseline.verdict == "STALE") t.stale++;
            }
            report.totals = t;
            if (report.verdict == "REFUSED") return;
            report.verdict = !haveBaseline ? "NO_BASELINE" : t.regressions > 0 ? "REGRESSION" : t.stale > 0 ? "STALE" : "OK";
        }

        /// <summary>A REGRESSION verdict, or a suite with no baseline entry (NEW) that failed or threw.</summary>
        private static bool IsRegression(SuiteResult s)
        {
            if (s.baseline == null) return false;
            if (s.baseline.verdict == "REGRESSION") return true;
            return s.baseline.verdict == "NEW" && (s.status == "fail" || s.status == "error");
        }

        // =====================================================================
        //  Isolation helpers
        // =====================================================================

        private static IEnumerable<Scene> LoadedScenes(bool playing)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s.isLoaded) yield return s;
            }
            if (playing)
            {
                Scene dontDestroy = DontDestroyOnLoadScene();
                if (dontDestroy.IsValid()) yield return dontDestroy;
            }
        }

        private static Scene DontDestroyOnLoadScene()
        {
            var probe = new GameObject("StaticSuiteRunnerProbe");
            Object.DontDestroyOnLoad(probe);
            Scene s = probe.scene;
            Object.DestroyImmediate(probe);
            return s;
        }

        private static HashSet<GameObject> SnapshotRoots(bool playing)
        {
            var ids = new HashSet<GameObject>();
            foreach (Scene s in LoadedScenes(playing))
                foreach (GameObject go in s.GetRootGameObjects())
                    ids.Add(go);
            return ids;
        }

        private static void CleanupAfterSuite(HashSet<GameObject> before, bool playing, bool cleanup, SuiteResult result)
        {
            var created = new List<GameObject>();
            foreach (Scene s in LoadedScenes(playing))
                foreach (GameObject go in s.GetRootGameObjects())
                    if (go != null && !before.Contains(go)) created.Add(go);

            var kept = new Dictionary<string, int>();
            var known = new Dictionary<string, int>();
            foreach (GameObject go in created)
            {
                string name = go.name;
                if (KeptRootNames.Contains(name))
                {
                    kept[name] = (kept.TryGetValue(name, out int k) ? k : 0) + 1;
                    continue;
                }
                if (KnownLeakIssues.ContainsKey(name))
                    known[name] = (known.TryGetValue(name, out int n) ? n : 0) + 1;
                else
                {
                    result.leakedCount++;
                    if (result.leaked.Count < MaxLeakNames) result.leaked.Add(name);
                }
                if (cleanup)
                {
                    try { Object.DestroyImmediate(go); }
                    catch (Exception) { /* already gone */ }
                }
            }
            foreach (KeyValuePair<string, int> e in kept)
                result.singletonsCreated.Add(e.Value > 1 ? $"{e.Key} x{e.Value}" : e.Key);
            foreach (KeyValuePair<string, int> e in known)
                result.knownLeaks.Add($"{e.Key} x{e.Value} ({KnownLeakIssues[e.Key]}, {(cleanup ? "destroyed" : "kept")})");
        }

        private static bool SetStaticInstance(Type type, object value)
        {
            try
            {
                PropertyInfo p = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo setter = p?.GetSetMethod(true);
                if (setter == null) return false;
                setter.Invoke(null, new[] { value });
                return true;
            }
            catch (Exception) { return false; }
        }

        private static HashSet<string> SnapshotProjectRootFiles()
        {
            try { return new HashSet<string>(Directory.GetFiles(ProjectRoot), StringComparer.OrdinalIgnoreCase); }
            catch (Exception) { return new HashSet<string>(StringComparer.OrdinalIgnoreCase); }
        }

        /// <summary>
        /// Reports files that appeared in the project root while the suite ran. Only names on DeletableRootFiles
        /// are deleted; anything else may belong to someone else and is kept.
        /// </summary>
        private static void ReportStrayRootFiles(HashSet<string> before, SuiteResult result)
        {
            foreach (string path in SnapshotProjectRootFiles())
            {
                if (before.Contains(path)) continue;
                string name = Path.GetFileName(path);
                bool deletable = DeletableRootFiles.Any(d => string.Equals(d, name, StringComparison.OrdinalIgnoreCase));
                string outcome = "kept";
                if (deletable)
                {
                    try { File.Delete(path); outcome = "deleted"; }
                    catch (Exception ex) { outcome = "kept: " + ex.Message; }
                }
                result.strayFiles.Add($"{name} ({outcome})");
            }
        }

        private static uint Fnv1a32(string text)
        {
            uint hash = 2166136261u;
            foreach (byte b in Encoding.UTF8.GetBytes(text ?? ""))
            {
                hash ^= b;
                hash *= 16777619u;
            }
            return hash;
        }

        // =====================================================================
        //  Output files
        // =====================================================================

        private static StaticRunReport LoadPreviousReport(string mode)
        {
            try
            {
                string path = Path.Combine(ResultsDir, $"static-{mode}.json");
                return File.Exists(path) ? JsonUtility.FromJson<StaticRunReport>(File.ReadAllText(path)) : null;
            }
            catch (Exception) { return null; }
        }

        private static void WriteJson(StaticRunReport report, string mode)
        {
            TryWrite(Path.Combine(ResultsDir, $"static-{mode}.json"), JsonUtility.ToJson(report, true), report, "json");
        }

        private static void WriteText(StaticRunReport report, string mode)
        {
            TryWrite(Path.Combine(ResultsDir, $"static-{mode}.txt"), BuildText(report), report, "txt");
        }

        /// <summary>Overwrites the TXT at the start of a run, so a frozen or crashed run does not leave the old result.</summary>
        private static void WriteRunningText(StaticRunReport report, string mode)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"RUNNING  runId={report.runId}  mode={report.unityMode}  seed={report.seedBase}  started={report.startedUtc}");
            if (!string.IsNullOrEmpty(report.filter)) sb.AppendLine("filter=" + report.filter);
            if (!string.IsNullOrEmpty(report.resumedFrom)) sb.AppendLine($"resumed from {report.resumedFrom} with after={report.after}");
            sb.AppendLine($"This pass has not finished. If Unity froze or crashed, static-{mode}.json names the suite that was running");
            sb.AppendLine("(currentSuite) and the last finished one (resumeAfter); see docs/TESTING.md 3.1.");
            TryWrite(Path.Combine(ResultsDir, $"static-{mode}.txt"), sb.ToString(), report, "txt");
        }

        private static void TryWrite(string path, string content, StaticRunReport report, string kind)
        {
            try
            {
                File.WriteAllText(path, content);
                if (!report.filesWritten.Contains(kind)) report.filesWritten.Add(kind);
            }
            catch (Exception ex)
            {
                report.warnings.Add("Could not write " + Path.GetFileName(path) + ": " + ex.Message);
            }
        }

        private static string BuildText(StaticRunReport r)
        {
            var sb = new StringBuilder();
            RunTotals t = r.totals;
            sb.AppendLine($"Static suites  runId={r.runId}  mode={r.unityMode}  seed={r.seedBase}  sceneGameManager={r.sceneGameManager}  started={r.startedUtc}");
            sb.AppendLine($"verdict={r.verdict}  complete={r.complete}  suites run={t.run} (pass {t.passedSuites}, fail {t.failedSuites}, error {t.errorSuites}), skipped={t.skippedSuites}, discovered={t.discovered}");
            sb.AppendLine($"assertions passed={t.assertionsPassed} failed={t.assertionsFailed}  regressions={t.regressions} stale={t.stale}  {r.durationMs / 1000.0:0.0}s");
            if (!string.IsNullOrEmpty(r.filter)) sb.AppendLine("filter=" + r.filter);
            if (!string.IsNullOrEmpty(r.resumedFrom)) sb.AppendLine($"resumed from {r.resumedFrom} with after={r.after}");
            if (r.verdict == "REFUSED") sb.AppendLine($"Nothing ran; static-{r.unityMode}.json and .log were left as they were.");
            else if (!string.IsNullOrEmpty(r.resumeAfter)) sb.AppendLine("INCOMPLETE: resume with " + ResumeOptions(r, r.resumeAfter));
            foreach (string w in r.warnings) sb.AppendLine("warning: " + w);
            sb.AppendLine();

            foreach (SuiteResult s in r.suites)
            {
                if (s.status == "skipped") continue;
                int known = s.baseline.knownSeen.Count;
                int fresh = s.baseline.hasEntry ? s.baseline.unexplainedFailures : s.failed;
                string tag = s.status.ToUpperInvariant();
                string extra = s.status == "error" ? "  threw " + s.exceptionType : "";
                if (s.modeMismatch) extra += "  modeMismatch";
                if (s.noSummary) extra += "  noSummary";
                if (!s.baseline.hasEntry) extra += "  NEW";
                sb.AppendLine($"{tag,-6}{s.type}  {s.passed}/{s.passed + s.failed}  known {known}  new {fresh}  {s.durationMs / 1000.0:0.0}s{extra}");
            }
            int skipped = r.suites.Count(s => s.status == "skipped");
            if (skipped > 0)
                sb.AppendLine($"({skipped} suites skipped: " + string.Join(", ", r.suites.Where(s => s.status == "skipped")
                    .GroupBy(s => s.skipReason).Select(g => $"{g.Count()} {g.Key}")) + ")");

            sb.AppendLine();
            sb.AppendLine("REGRESSIONS");
            foreach (SuiteResult s in r.suites.Where(IsRegression))
            {
                string why = !s.baseline.hasEntry ? "  (no baseline entry)"
                    : s.baseline.unexpectedThrow ? $"  threw {s.exceptionType}: {s.exceptionMessage}" : "";
                sb.AppendLine("  " + s.type + why);
                if (!s.baseline.hasEntry)
                {
                    if (s.status == "error") sb.AppendLine($"    threw {s.exceptionType}: {s.exceptionMessage}");
                    foreach (string f in s.failures) sb.AppendLine("    - " + f);
                    continue;
                }
                foreach (string f in s.baseline.unexpected) sb.AppendLine("    - " + f);
                int gap = s.baseline.unexplainedFailures - s.baseline.unexpected.Count - s.failureLabelsDropped;
                if (s.failureLabelsDropped > 0) sb.AppendLine($"    ({s.failureLabelsDropped} more labels beyond the {MaxFailureLabels}-label cap)");
                if (gap > 0)
                    sb.AppendLine($"    ({gap} failures more than the known labels explain: a failure without a FAIL line, or a new label repeated)");
            }
            sb.AppendLine("STALE");
            foreach (SuiteResult s in r.suites.Where(x => x.baseline.verdict == "STALE"))
            {
                sb.AppendLine("  " + s.type + (s.baseline.expectThrowMissing ? "  expected a throw that did not happen" : ""));
                foreach (string k in s.baseline.staleKnown) sb.AppendLine("    - not seen: " + k);
            }
            sb.AppendLine("NEW/MISSING");
            foreach (string n in r.newSuites) sb.AppendLine("  NEW " + n);
            foreach (string m in r.missingSuites) sb.AppendLine("  MISSING " + m);
            sb.AppendLine("LEAKS");
            foreach (SuiteResult s in r.suites.Where(x => x.leakedCount > 0 || x.knownLeaks.Count > 0 || x.restored.Count > 0
                         || x.strayFiles.Count > 0 || x.singletonsCreated.Count > 0))
            {
                var parts = new List<string>();
                if (s.leakedCount > 0)
                    parts.Add($"leaked {s.leakedCount}: " + string.Join(", ", s.leaked) + (s.leakedCount > s.leaked.Count ? ", ..." : ""));
                if (s.knownLeaks.Count > 0) parts.Add("known " + string.Join(", ", s.knownLeaks));
                if (s.singletonsCreated.Count > 0) parts.Add("kept singletons " + string.Join(", ", s.singletonsCreated));
                if (s.restored.Count > 0) parts.Add("restored " + string.Join(", ", s.restored));
                if (s.strayFiles.Count > 0) parts.Add("root files " + string.Join(", ", s.strayFiles));
                sb.AppendLine("  " + s.type + ": " + string.Join("; ", parts));
            }
            sb.AppendLine("WARNINGS");
            foreach (SuiteResult s in r.suites.Where(x => x.baseline.warnings.Count > 0 || x.errorLogCount > 0))
            {
                var parts = new List<string>(s.baseline.warnings);
                if (s.errorLogCount > 0) parts.Add($"{s.errorLogCount} error logs (first: {s.errorLogSamples.FirstOrDefault()})");
                sb.AppendLine("  " + s.type + ": " + string.Join("; ", parts));
            }
            return sb.ToString();
        }

        private static string Summarize(StaticRunReport r)
        {
            var sb = new StringBuilder();
            RunTotals t = r.totals;
            sb.AppendLine($"verdict={r.verdict} mode={r.unityMode} complete={r.complete} seed={r.seedBase} sceneGameManager={r.sceneGameManager}"
                + (EditorApplication.isPaused ? " paused=true" : ""));
            sb.AppendLine($"suites run={t.run} pass={t.passedSuites} fail={t.failedSuites} error={t.errorSuites} skipped={t.skippedSuites} discovered={t.discovered} new={r.newSuites.Count} missing={r.missingSuites.Count}");
            sb.AppendLine($"assertions passed={t.assertionsPassed} failed={t.assertionsFailed} regressions={t.regressions} stale={t.stale} time={r.durationMs / 1000.0:0.0}s");
            if (r.verdict != "REFUSED" && !string.IsNullOrEmpty(r.resumeAfter))
                sb.AppendLine("INCOMPLETE: resume with " + ResumeOptions(r, r.resumeAfter));
            foreach (string w in r.warnings) sb.AppendLine("warning: " + w);
            foreach (SuiteResult s in r.suites.Where(x => x.status == "fail" || x.status == "error"
                         || IsRegression(x) || x.baseline.verdict == "STALE"))
            {
                string line = $"{s.status.ToUpperInvariant(),-6}{s.type} {s.passed}/{s.passed + s.failed} [{s.baseline.verdict}]"
                    + (s.status == "error" ? $" threw {s.exceptionType}" : "")
                    + (s.baseline.unexpected.Count > 0 ? " new: " + Truncate(s.baseline.unexpected[0], 80) : "");
                if (sb.Length + line.Length > MaxSummaryChars - 300) { sb.AppendLine("... (see the TXT report)"); break; }
                sb.AppendLine(line);
            }
            foreach (string n in r.newSuites.Take(5)) sb.AppendLine("NEW " + n);
            foreach (string m in r.missingSuites.Take(5)) sb.AppendLine("MISSING " + m);
            string dir = ResultsDir.Replace('\\', '/');
            sb.AppendLine(r.filesWritten.Count == 0
                ? "files: none written by this run"
                : $"files written by this run: {dir}/static-{r.unityMode}." + string.Join(" .", r.filesWritten));
            return sb.ToString();
        }

        private static string Truncate(string s, int n) => s == null ? "" : s.Length <= n ? s : s.Substring(0, n) + "...";

        private static void WriteProposedBaseline(StaticRunReport report, StaticBaseline baseline)
        {
            var proposed = new StaticBaseline { seed = baseline != null ? baseline.seed : report.seedBase };
            var byType = baseline == null
                ? new Dictionary<string, BaselineSuite>()
                : baseline.suites.GroupBy(s => s.type).ToDictionary(g => g.Key, g => g.First());
            var results = report.suites.ToDictionary(s => s.type, s => s);
            var names = new SortedSet<string>(byType.Keys.Concat(results.Keys), StringComparer.Ordinal);
            foreach (string name in names)
            {
                BaselineSuite old;
                byType.TryGetValue(name, out old);
                SuiteResult res;
                results.TryGetValue(name, out res);
                if (res == null || res.status == "skipped")
                {
                    if (old != null) proposed.suites.Add(old);
                    continue;
                }
                var entry = new BaselineSuite
                {
                    type = name,
                    needs = old != null ? old.needs : "unknown",
                    skip = old != null ? old.skip : "",
                    expectThrow = old != null && !string.IsNullOrEmpty(old.expectThrow) ? old.expectThrow : res.exceptionType,
                    passed = res.passed,
                    failed = res.failed,
                    flakyFailures = old != null ? old.flakyFailures : 0,
                    notes = old != null ? old.notes : "",
                };
                if ((old == null || string.IsNullOrEmpty(old.expectThrow)) && !string.IsNullOrEmpty(res.exceptionType))
                    entry.notes = (entry.notes + " TODO: issue ID for " + res.exceptionType).Trim();
                if (old != null)
                    entry.knownFailures.AddRange(old.knownFailures.Where(k => res.failures.Any(f => f.StartsWith(k.match, StringComparison.Ordinal))));
                foreach (string f in res.failures)
                    if (!entry.knownFailures.Any(k => f.StartsWith(k.match, StringComparison.Ordinal)))
                        entry.knownFailures.Add(new KnownFailure { match = f, issue = "TODO" });
                if (res.modeMismatch) entry.notes = (entry.notes + " [proposed from a modeMismatch run in " + report.unityMode + " mode]").Trim();
                proposed.suites.Add(entry);
            }
            try { File.WriteAllText(Path.Combine(ResultsDir, "static-baseline-proposed.json"), JsonUtility.ToJson(proposed, true)); }
            catch (Exception ex) { report.warnings.Add("Could not write the proposed baseline: " + ex.Message); }
        }

        // =====================================================================
        //  Menu items
        // =====================================================================

        [MenuItem("Tools/DND Tests/Run Static Suites (current mode)")]
        private static void MenuRun() => Debug.Log("[StaticSuiteRunner]\n" + RunFromCommand(""));

        [MenuItem("Tools/DND Tests/List Static Suites")]
        private static void MenuList() => Debug.Log("[StaticSuiteRunner]\n" + ListSuites());

        [MenuItem("Tools/DND Tests/Write Proposed Baseline")]
        private static void MenuWriteBaseline() => Debug.Log("[StaticSuiteRunner]\n" + RunFromCommand("writeBaseline=true"));

        // =====================================================================
        //  Log capture
        // =====================================================================

        private struct CapturedLine
        {
            public LogType Type;
            public string Message;
        }

        /// <summary>Records every log line during a suite; forwards to the original handler only when echo is on.</summary>
        private sealed class CapturingLogHandler : ILogHandler
        {
            private readonly ILogHandler _inner;
            private readonly bool _echo;
            private readonly object _lock = new object();
            private readonly List<CapturedLine> _lines = new List<CapturedLine>();
            private bool _forwarding;

            public CapturingLogHandler(ILogHandler inner, bool echo)
            {
                _inner = inner;
                _echo = echo;
            }

            public void LogFormat(LogType logType, Object context, string format, params object[] args)
            {
                string message;
                try { message = args == null || args.Length == 0 ? format : string.Format(format, args); }
                catch (FormatException) { message = format; }
                Add(logType, message);
                if (_echo) Forward(() => _inner.LogFormat(logType, context, format, args));
            }

            public void LogException(Exception exception, Object context)
            {
                Add(LogType.Exception, exception == null ? "null exception" : exception.GetType().Name + ": " + exception.Message);
                if (_echo) Forward(() => _inner.LogException(exception, context));
            }

            /// <summary>Catches messages that bypass Debug.unityLogger (engine and native logs).</summary>
            public void OnNativeLog(string condition, string stackTrace, LogType type)
            {
                if (_forwarding) return;
                Add(type, condition ?? "");
            }

            private void Forward(Action a)
            {
                _forwarding = true;
                try { a(); }
                finally { _forwarding = false; }
            }

            private void Add(LogType type, string message)
            {
                lock (_lock) _lines.Add(new CapturedLine { Type = type, Message = message ?? "" });
            }

            public List<CapturedLine> Snapshot()
            {
                lock (_lock) return new List<CapturedLine>(_lines);
            }
        }
    }
}
#endif
