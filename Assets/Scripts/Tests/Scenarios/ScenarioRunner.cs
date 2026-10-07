#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Tests.Scenarios
{
    /// <summary>Options for a run, parsed from "key=value;..." (see <see cref="ScenarioHarness.Start(string,string,string)"/>).</summary>
    public sealed class RunOptions
    {
        public string Mode = "fast";
        public int? MaxRounds;
        public int Repeat = 1;
        public int Sweep;
        public bool RecordDice;
        public bool Keep;
        public float WallCapSeconds = 120f;

        public static RunOptions Parse(string text, out string error)
        {
            error = null;
            var o = new RunOptions();
            if (string.IsNullOrWhiteSpace(text))
                return o;
            foreach (string part in text.Split(';'))
            {
                string p = part.Trim();
                if (p.Length == 0)
                    continue;
                int eq = p.IndexOf('=');
                string key = (eq >= 0 ? p.Substring(0, eq) : p).Trim().ToLowerInvariant();
                string val = eq >= 0 ? p.Substring(eq + 1).Trim() : "true";
                switch (key)
                {
                    case "mode":
                        if (val != "fast" && val != "watch") { error = "mode must be fast or watch"; return null; }
                        o.Mode = val;
                        break;
                    case "maxrounds":
                        if (!int.TryParse(val, out int mr) || mr < 1) { error = "maxRounds must be a positive integer"; return null; }
                        o.MaxRounds = mr;
                        break;
                    case "repeat":
                        if (!int.TryParse(val, out int rep) || rep < 1 || rep > 10) { error = "repeat must be 1-10"; return null; }
                        o.Repeat = rep;
                        break;
                    case "sweep":
                        if (!int.TryParse(val, out int sw) || sw < 1 || sw > 200) { error = "sweep must be 1-200"; return null; }
                        o.Sweep = sw;
                        break;
                    case "record":
                        o.RecordDice = val == "dice";
                        break;
                    case "keep":
                        o.Keep = val != "false";
                        break;
                    case "wallcap":
                        if (!float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float wc) || wc < 5f) { error = "wallCap must be at least 5 seconds"; return null; }
                        o.WallCapSeconds = wc;
                        break;
                    default:
                        error = "unknown option '" + key + "'";
                        return null;
                }
            }
            return o;
        }

        public override string ToString()
            => "mode=" + Mode + (MaxRounds.HasValue ? ";maxRounds=" + MaxRounds : "") + ";repeat=" + Repeat
               + (Sweep > 0 ? ";sweep=" + Sweep : "") + (RecordDice ? ";record=dice" : "") + (Keep ? ";keep" : "");
    }

    /// <summary>The result of one job, as written to summary.json.</summary>
    public sealed class JobResult
    {
        public string Id;
        public int Seed;
        public int Rep;
        public Outcome Outcome;
        public string Reason;
        public Verdict Verdict;
        public string Hash;
        public string Path;
        public int Rounds;
        public int Events;
        public float WallSec;
        public int Frames;
        public int FocusedFrames;
        public int UnfocusedFrames;
        public int FirstDiffSeq;
        public string RepeatCheck;
        public List<string> Violations = new List<string>();
        public List<string> Expectations = new List<string>();
        public List<string> Notes = new List<string>();

        public JsonObj ToJson()
        {
            var o = new JsonObj();
            o.Set("id", Id).Set("seed", Seed).Set("rep", Rep).Set("verdict", Verdict).Set("outcome", Outcome).Set("reason", Reason)
             .Set("rounds", Rounds).Set("events", Events).Set("hash", Hash).Set("path", Path).Set("wallSec", WallSec)
             .Set("frames", Frames).Set("fps", WallSec > 0f ? Frames / WallSec : 0f)
             .Set("editorFocusedFrames", FocusedFrames).Set("editorUnfocusedFrames", UnfocusedFrames);
            if (RepeatCheck != null)
                o.Set("repeat", RepeatCheck).Set("firstDiffSeq", FirstDiffSeq);
            o.Set("expectations", Expectations).Set("violations", Violations);
            if (Notes.Count > 0)
                o.Set("notes", Notes);
            return o;
        }
    }

    /// <summary>One (scenario, seed, repetition) run and its live state.</summary>
    internal sealed class ScenarioJob
    {
        public ScenarioDef Def;
        public int Seed;
        public int Rep;
        public int Index;
        public int MaxRounds;
        public RunOptions Options;
        public ScenarioRunner Runner;
        public GameManager Gm;
        public ScenarioContext Ctx;
        public ScenarioTrace Trace;
        public ScenarioChecks Checks;
        public int InitiativeCount;
        public bool Started;
        public bool CombatOverSeen;
        public bool Halting;
        public Outcome Outcome;
        public string OutcomeReason;
        public string OutcomeDiag;
        public bool ResultRecorded;
        public bool HaltRequested;
        public int StartFrame;
        public float StartRealtime;
        public int PauseCount;
        public int ErrorPauses;
        public bool RunnerFailed;
        public int FocusedFrames;
        public int UnfocusedFrames;
        public CharacterController PendingUiEnd;
        public int PendingUiEndFrame;
        public readonly Dictionary<CharacterController, ActorSpec> SpecOf = new Dictionary<CharacterController, ActorSpec>();
        public readonly List<Waiver> Waivers = new List<Waiver>();
        public readonly List<KeyValuePair<Expectation, ExpectResult>> ExpectationResults = new List<KeyValuePair<Expectation, ExpectResult>>();
        public readonly List<string> Notes = new List<string>();
        public int Unwaived;
        public int Waived;
        public int HookErrorViolations;

        public bool Decided => Outcome != Outcome.None;

        /// <summary>Ends the job with <paramref name="outcome"/>; the trace stops recording game events. haltNow halts combat inside the current callback (safe only where TurnService returns right after, e.g. a TurnStarted handler).</summary>
        public void Decide(Outcome outcome, string reason, bool haltNow, string diag = null)
        {
            if (Decided)
                return;
            Outcome = outcome;
            OutcomeReason = reason;
            OutcomeDiag = diag;
            if (Trace != null)
            {
                TraceEvent ev = Trace.Emit("decision").Set("outcome", outcome).Set("reason", reason);
                if (diag != null)
                    ev.Set("diag", diag); // timing-dependent detail; left out of the trace hash
                Trace.Closed = true;
            }
            HaltRequested = true;
            if (haltNow && Gm != null && Started)
            {
                Halting = true;
                Gm.Harness_HaltCombat(outcome.ToString());
            }
        }

        public void AddViolation(string inv, string detail, string actorKey)
        {
            Waiver match = null;
            if (inv != ScenarioChecks.HookError)
            {
                foreach (Waiver w in Waivers)
                {
                    if (w.Matches(inv, detail))
                    {
                        match = w;
                        break;
                    }
                }
            }

            if (inv == ScenarioChecks.HookError)
                HookErrorViolations++;
            else if (match != null)
                Waived++;
            else
                Unwaived++;

            if (match != null)
                Runner?.CountWaiverHit(match, Def);

            Trace?.Emit("violation").Set("inv", inv).Set("detail", detail).Set("actor", actorKey).Set("waivedBy", match != null ? match.IssueId : null);
        }

        public bool IsFeared(CharacterController a)
            => a.HasCondition(CombatConditionType.Frightened) || a.HasCondition(CombatConditionType.Panicked) || a.HasCondition(CombatConditionType.Turned);

        /// <summary>"ui", "ai", "scripted" or "idle": who decides this turn.</summary>
        public string ControllerLabel(CharacterController a)
        {
            if (a.IsControllable)
                return "ui";
            if (!SpecOf.TryGetValue(a, out ActorSpec spec))
                return "ai";
            if ((spec.Control == Control.Scripted || spec.Control == Control.Idle) && IsFeared(a))
                return "ai";
            return spec.Control.ToString().ToLowerInvariant();
        }

        public void OnTurnStarted(CharacterController a, string controller)
        {
            if (controller == "ui")
            {
                // Ui steps come with the scripted step: with none, the turn is ended through the End Turn button path next frame.
                PendingUiEnd = a;
                PendingUiEndFrame = Time.frameCount;
            }
        }

        /// <summary>ScenarioHooks.ScriptedTurn provider: null lets the AI run the turn.</summary>
        public IEnumerator GetScriptedTurn(CharacterController actor)
        {
            if (Decided || actor == null || !SpecOf.TryGetValue(actor, out ActorSpec spec))
                return null;
            if (spec.Control != Control.Scripted && spec.Control != Control.Idle)
                return null;
            // Fear compulsions stay with the AI (the hook sits after the confused, charmed and fascinated gates only).
            if (IsFeared(actor))
                return null;
            if (spec.Control == Control.Idle)
                return EmptyTurn();
            if (Def.Scripts.TryGetValue(spec.Key, out Func<ScenarioContext, CharacterController, IEnumerator> script) && script != null)
                return script(Ctx, actor) ?? EmptyTurn();
            return EmptyTurn();
        }

        private static IEnumerator EmptyTurn()
        {
            yield break;
        }
    }

    /// <summary>
    /// Runs scenario jobs in Play mode on its own DontDestroyOnLoad object (never on GameManager, whose coroutines
    /// the game and the halt stop, CORE-013). For each job: preflight, boot (once), reset and clean check, seed,
    /// spawn, control, hooks, start, monitor, finalize, cleanup. Results go to Logs/Scenarios/&lt;runId&gt;/.
    /// Drive it through <see cref="ScenarioHarness"/>.
    /// </summary>
    public sealed class ScenarioRunner : MonoBehaviour
    {
        public enum RunState { Idle, Running, Done, Interrupted }

        internal static ScenarioRunner Instance;
        internal static bool SessionDirty;
        internal static string DirtyReason;
        private static bool _booted;
        private static Dictionary<string, int> _baselineSubs;
        private static int _baselineNpcCount = -1;

        public const int PhaseFrameTimeout = 600;
        public static readonly string[] DefaultParty = { "Fighter", "Rogue", "Cleric", "Wizard" };

        internal RunState State = RunState.Idle;
        internal string RunId;
        internal string RunDir;
        internal string Filter;
        internal string SeedsText;
        internal RunOptions Options;
        internal readonly List<ScenarioJob> Jobs = new List<ScenarioJob>();
        internal readonly List<JobResult> Results = new List<JobResult>();
        internal ScenarioJob Current;
        internal int JobIndex;
        internal bool AbortRequested;
        internal string AbortReason;
        internal string SummaryJson;
        internal string RunVerdict;
        internal string SummaryError;
        internal string InterruptReason;
        internal List<string> LoadErrors = new List<string>();
        private int _lastErrorFrame = -10;
        private readonly Dictionary<string, int> _waiverHits = new Dictionary<string, int>();
        private readonly Dictionary<string, List<string>> _firstRepLines = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, string> _firstRepHash = new Dictionary<string, string>();
        private float _lastStatusWrite = -10f;
        private float _runStartRealtime;

        // Per-job restore state
        private readonly List<KeyValuePair<CharacterController, (CharacterTeam team, bool controllable, DND35.AI.AIProfile profile)>> _savedControl =
            new List<KeyValuePair<CharacterController, (CharacterTeam, bool, DND35.AI.AIProfile)>>();
        private readonly List<Object> _createdProfiles = new List<Object>();
        private Random.State _savedRandom;
        private bool _randomSaved;
        private HPCalculationMode _savedHpMode;
        private bool _hpModeSaved;
        private Application.LogCallback _logHandler;

        /// <summary>True while a run is going (StaticSuiteRunner refuses to start then: it would clear the hooks mid-job).</summary>
        internal static bool IsRunActive => Instance != null && Instance.State == RunState.Running;

        public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
        public static string ScenariosDir => Path.Combine(ProjectRoot, "Logs", "Scenarios");

        // ── Start ───────────────────────────────────────────────────────

        internal void Begin(List<ScenarioDef> defs, List<int> seeds, RunOptions options, string filter, string seedsText, List<string> loadErrors = null)
        {
            Instance = this;
            if (loadErrors != null)
                LoadErrors.AddRange(loadErrors);
            Options = options;
            Filter = filter;
            SeedsText = seedsText;
            RunId = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            RunDir = Path.Combine(ScenariosDir, RunId);
            int suffix = 1;
            while (Directory.Exists(RunDir))
                RunDir = Path.Combine(ScenariosDir, RunId + "-" + (++suffix));
            RunId = Path.GetFileName(RunDir);
            Directory.CreateDirectory(RunDir);

            int index = 0;
            foreach (ScenarioDef def in defs)
                foreach (int seed in seeds)
                    for (int rep = 1; rep <= options.Repeat; rep++)
                        Jobs.Add(new ScenarioJob
                        {
                            Def = def,
                            Seed = seed,
                            Rep = rep,
                            Index = index++,
                            MaxRounds = options.MaxRounds ?? def.MaxRounds,
                            Options = options,
                            Runner = this
                        });

            State = RunState.Running;
            _runStartRealtime = Time.realtimeSinceStartup;
            EditorApplication.update -= EditorTick;
            EditorApplication.update += EditorTick;
            Application.logMessageReceived -= OnAnyLog;
            Application.logMessageReceived += OnAnyLog;
            WriteStatus(true);
            // RunAll yields before any game work, so the MCP call that starts the run only schedules it.
            StartCoroutine(RunAll());
        }

        /// <summary>Remembers the frame of the last error, assert or exception (a pause right after one is Error Pause).</summary>
        private void OnAnyLog(string condition, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
                _lastErrorFrame = Time.frameCount;
        }

        /// <summary>Counts frames with and without editor focus (an unfocused editor may be throttled).</summary>
        private void Update()
        {
            ScenarioJob job = Current;
            if (job == null)
                return;
            if (UnityEditorInternal.InternalEditorUtility.isApplicationActive)
                job.FocusedFrames++;
            else
                job.UnfocusedFrames++;
        }

        /// <summary>
        /// Runs when Play mode ends, when the object is destroyed, and before a domain reload (a recompile while
        /// playing). The coroutine dies with any of these, so a live run is written out as interrupted here.
        /// </summary>
        private void OnDisable()
        {
            EditorApplication.update -= EditorTick;
            Application.logMessageReceived -= OnAnyLog;
            if (State == RunState.Running)
                MarkInterrupted(Application.isPlaying ? "runner disabled (script reload or object destroyed)" : "Play mode ended");
        }

        private void OnApplicationQuit()
        {
            if (State == RunState.Running)
                MarkInterrupted("Play mode ended");
        }

        private void OnDestroy()
        {
            EditorApplication.update -= EditorTick;
            Application.logMessageReceived -= OnAnyLog;
            if (Instance == this && State == RunState.Running)
                Instance = null;
        }

        private void MarkInterrupted(string reason)
        {
            State = RunState.Interrupted;
            InterruptReason = reason;
            if (Current != null && !Current.Decided)
                Current.Notes.Add("interrupted: " + reason);
            try { ScenarioSessionGuard.CleanUp("ScenarioRunner interrupted"); } catch (Exception) { }
            try { WriteSummary(); } catch (Exception ex) { SummaryError = ex.Message; }
            WriteStatus(true);
        }

        /// <summary>Editor-side tick (runs while Play is paused): handles Error Pause and keeps frames coming while unfocused.</summary>
        private void EditorTick()
        {
            if (State != RunState.Running)
            {
                EditorApplication.update -= EditorTick;
                return;
            }

            if (EditorApplication.isPaused)
                HandlePause();
            if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive)
                EditorApplication.QueuePlayerLoopUpdate();
        }

        internal void HandlePause()
        {
            ScenarioJob job = Current;
            if (job == null)
            {
                EditorApplication.isPaused = false;
                return;
            }
            // Console Error Pause stops Play on every logged error. Those errors are judged by the no-errors invariant
            // and its waivers, so a pause right after one is undone without counting; only other pauses count.
            if (Time.frameCount - _lastErrorFrame <= 1)
            {
                job.ErrorPauses++;
                if (job.ErrorPauses == 1)
                {
                    job.Notes.Add("Error Pause stopped Play after a logged error at frame " + Time.frameCount + "; unpaused (not counted)");
                    job.Trace?.Emit("watchdog").Set("kind", "error-pause");
                }
                EditorApplication.isPaused = false;
                return;
            }
            job.PauseCount++;
            if (job.PauseCount == 1)
            {
                job.Notes.Add("editor paused with no error just before it at frame " + Time.frameCount + "; unpaused once");
                job.Trace?.Emit("watchdog").Set("kind", "paused").Set("count", 1);
            }
            else
            {
                job.Decide(Outcome.Paused, "editor paused again (a human or debugger pause)", haltNow: false);
            }
            EditorApplication.isPaused = false;
        }

        internal void CountWaiverHit(Waiver w, ScenarioDef def)
        {
            string key = WaiverKey(w, def);
            _waiverHits.TryGetValue(key, out int n);
            _waiverHits[key] = n + 1;
        }

        private static string WaiverKey(Waiver w, ScenarioDef def)
            => KnownIssueWaivers.Global.Contains(w) ? "global " + w : def.Id + " " + w;

        // ── Run loop ────────────────────────────────────────────────────

        private IEnumerator RunAll()
        {
            // Nothing runs inside the MCP call that started the run: Unity runs a coroutine up to its first yield at once.
            yield return null;
            try
            {
                for (JobIndex = 0; JobIndex < Jobs.Count; JobIndex++)
                {
                    if (AbortRequested || State != RunState.Running)
                        break;
                    ScenarioJob job = Jobs[JobIndex];
                    Current = job;

                    // Driven by hand so an exception in any phase ends this job (as Exception, with a result) instead of
                    // silently killing the coroutine and leaving the run at "running" for the rest of the Play session.
                    IEnumerator it = RunJob(job);
                    while (true)
                    {
                        object step;
                        try
                        {
                            if (!it.MoveNext())
                                break;
                            step = it.Current;
                        }
                        catch (Exception ex)
                        {
                            RecoverFromRunnerException(job, ex);
                            break;
                        }
                        yield return step;
                    }

                    Current = null;
                    WriteStatus(true);
                    if (SessionDirty && (job.Outcome == Outcome.Contaminated || job.RunnerFailed))
                        break;
                    if (job.Options.Keep)
                    {
                        if (Results.Count > 0)
                            Results[Results.Count - 1].Notes.Add("keep: the run stopped after this job and left the world as it was");
                        break;
                    }
                }

                if (!AbortRequested && Jobs.Count > 0 && !Options.Keep && State == RunState.Running)
                {
                    yield return null;
                    GameManager gm = GameManager.Instance;
                    string dirty;
                    try { dirty = gm != null ? CleanCheck(gm) : null; }
                    catch (Exception ex) { dirty = "clean check threw " + ex.GetType().Name + ": " + ex.Message; }
                    if (dirty != null)
                    {
                        SessionDirty = true;
                        DirtyReason = "after the last job: " + dirty;
                    }
                }
            }
            finally
            {
                Finish();
            }
        }

        /// <summary>Writes the summary and the final status once (not after an interruption, which wrote its own).</summary>
        private void Finish()
        {
            if (State != RunState.Running)
                return;
            Current = null;
            try { WriteSummary(); }
            catch (Exception ex) { SummaryError = ex.GetType().Name + ": " + ex.Message; }
            State = RunState.Done;
            WriteStatus(true);
            EditorApplication.update -= EditorTick;
            Application.logMessageReceived -= OnAnyLog;
        }

        /// <summary>An exception escaped a job phase: end the job as Exception with a result, clean up, and stop the run (the session is dirty).</summary>
        private void RecoverFromRunnerException(ScenarioJob job, Exception ex)
        {
            Debug.LogWarning("[ScenarioHarness] runner failed in " + job.Def.Id + " s" + job.Seed + ": " + ex);
            string reason = "runner: " + ex.GetType().Name + ": " + ex.Message;
            job.Notes.Add(reason);
            job.Decide(Outcome.Exception, reason, false);
            job.RunnerFailed = true;
            GameManager gm = job.Gm ?? GameManager.Instance;
            if (job.Started && gm != null)
            {
                job.Halting = true;
                try { gm.Harness_HaltCombat("runner-failed"); } catch (Exception hex) { job.Notes.Add("halt: " + hex.Message); }
            }
            if (!job.ResultRecorded)
                RecordResult(job, ErrorResult(job, reason));
            try { Cleanup(job, gm); } catch (Exception cex) { job.Notes.Add("cleanup: " + cex.Message); }
            SessionDirty = true;
            DirtyReason = job.Def.Id + " s" + job.Seed + ": " + reason;
        }

        /// <summary>A result for a job that has none (finalize or a runner phase threw); writes whatever trace exists.</summary>
        private JobResult ErrorResult(ScenarioJob job, string reason)
        {
            var r = new JobResult { Id = job.Def.Id, Seed = job.Seed, Rep = job.Rep, Outcome = job.Outcome, Reason = reason, Verdict = Verdict.Error };
            r.Notes.AddRange(job.Notes);
            if (job.Trace != null)
            {
                try
                {
                    string path = Path.Combine(RunDir, Slug(job.Def.Id) + "__s" + job.Seed + (job.Options.Repeat > 1 ? "__r" + job.Rep : "") + ".jsonl");
                    job.Trace.WriteJsonl(path);
                    r.Path = path.Replace('\\', '/');
                    r.Events = job.Trace.Events.Count;
                }
                catch (Exception ex)
                {
                    r.Notes.Add("trace write: " + ex.Message);
                }
            }
            return r;
        }

        private void RecordResult(ScenarioJob job, JobResult result)
        {
            result.WallSec = Time.realtimeSinceStartup - job.StartRealtime;
            result.Frames = Time.frameCount - job.StartFrame;
            result.FocusedFrames = job.FocusedFrames;
            result.UnfocusedFrames = job.UnfocusedFrames;
            if (job.OutcomeDiag != null)
                result.Notes.Insert(0, "diag: " + job.OutcomeDiag);
            Results.Add(result);
            job.ResultRecorded = true;
        }

        private IEnumerator RunJob(ScenarioJob job)
        {
            job.StartFrame = Time.frameCount;
            job.StartRealtime = Time.realtimeSinceStartup;
            GameManager gm = GameManager.Instance;
            job.Gm = gm;
            job.Ctx = new ScenarioContext { Def = job.Def, Seed = job.Seed, Rep = job.Rep, Gm = gm };
            job.Waivers.AddRange(job.Def.Waivers);
            job.Waivers.AddRange(KnownIssueWaivers.Global);

            // a. Preflight
            string pre = Preflight(gm);
            if (pre != null)
                job.Decide(pre.StartsWith("needsFresh", StringComparison.Ordinal) ? Outcome.Contaminated : Outcome.SetupFailed, "preflight: " + pre, false);

            // b. Boot, once per Play session
            if (!job.Decided && !_booted)
            {
                if (gm.WaitingForCharacterCreation)
                {
                    bool ok = TryRun(job, "boot", () =>
                    {
                        CharacterCreationData[] data = BuildCreationParty(job.Def, true);
                        if (gm.CharacterCreationUI == null || !gm.CharacterCreationUI.CompleteWithParty(data, "ScenarioHarness"))
                            throw new InvalidOperationException("CompleteWithParty refused the party");
                    });
                    int waited = 0;
                    while (ok && !gm.WaitingForEncounterSelection && waited < PhaseFrameTimeout)
                    {
                        waited++;
                        yield return null;
                    }
                    if (ok && !gm.WaitingForEncounterSelection)
                        job.Decide(Outcome.SetupFailed, "boot: encounter selection did not open within " + PhaseFrameTimeout + " frames (level-up UI?) " + gm.Harness_DumpState(), false);
                }
                if (!job.Decided)
                    _booted = true;
            }

            // c. Reset and clean check
            if (!job.Decided)
            {
                string resetErr = null;
                TryRun(job, "reset", () => resetErr = gm.Harness_ResetWorld("job " + job.Def.Id + " s" + job.Seed));
                yield return null; // summons are destroyed at the end of the frame
                if (!job.Decided)
                {
                    string dirty;
                    try { dirty = resetErr != null ? "reset failed: " + resetErr : CleanCheck(gm); }
                    catch (Exception ex) { dirty = "clean check threw " + ex.GetType().Name + ": " + ex.Message; }
                    if (dirty != null)
                    {
                        SessionDirty = true;
                        DirtyReason = dirty;
                        job.Decide(Outcome.Contaminated, dirty, false);
                    }
                }
            }

            // d-g. Seed, spawn, control, hooks
            if (!job.Decided)
                TryRun(job, "setup", () => Setup(job, gm));

            // h. Start (from this coroutine, never from an MCP call or a game callback, CORE-012)
            if (!job.Decided)
            {
                Random.InitState(unchecked(job.Seed * 7919 + 17));
                job.Started = true;
                TryRun(job, "start", gm.Harness_StartCombat, Outcome.Exception);
            }

            // i. Monitor
            while (!job.Decided)
            {
                yield return null;
                if (AbortRequested)
                {
                    job.Decide(Outcome.Halted, "aborted: " + AbortReason, false);
                    break;
                }
                // A static-suite pass in this Play session clears the hooks and replaces the singletons mid-job.
                string contamination = Tests.Runner.StaticSuiteRunner.RanInPlayModeThisDomain ? "static suites ran during the job"
                    : ScenarioHooks.ScriptedTurn == null ? "the scenario hooks were cleared during the job" : null;
                if (contamination != null)
                {
                    SessionDirty = true;
                    DirtyReason = contamination;
                    job.Decide(Outcome.Contaminated, contamination, false);
                    break;
                }
                job.Checks.Frame();
                if (!job.Decided && job.Options.WallCapSeconds > 0 && Time.realtimeSinceStartup - job.StartRealtime > job.Options.WallCapSeconds)
                    job.Decide(Outcome.Timeout, "wall-clock cap " + job.Options.WallCapSeconds + " s", false);
                if (!job.Decided)
                    TryRun(job, "ui-end", () => HandleUiTurn(job, gm), Outcome.Exception);
                WriteStatus(false);
            }

            // Halt, and halt again next frame: StartPCTurn or a started NPC coroutine can still change the phase (CORE-003).
            if (job.Started)
            {
                job.Halting = true;
                TryRun(job, "halt", () => gm.Harness_HaltCombat(job.Outcome.ToString()), Outcome.Exception);
                yield return null;
                TryRun(job, "halt-recheck", () => gm.Harness_HaltCombat(job.Outcome.ToString() + "-recheck"), Outcome.Exception);
            }

            // j. Finalize
            JobResult result = null;
            Exception finalizeError = TryRunFinal(() => result = Finalize(job, gm), job);
            if (result == null)
                result = ErrorResult(job, "finalize: " + (finalizeError != null ? finalizeError.GetType().Name + ": " + finalizeError.Message : "no result"));
            RecordResult(job, result);

            // k. Cleanup (the wall time and frames below include it)
            Cleanup(job, gm);
            result.WallSec = Time.realtimeSinceStartup - job.StartRealtime;
            result.Frames = Time.frameCount - job.StartFrame;
            result.FocusedFrames = job.FocusedFrames;
            result.UnfocusedFrames = job.UnfocusedFrames;
            if (job.Outcome == Outcome.SoftLock || job.Outcome == Outcome.SkipChainRunaway || job.Outcome == Outcome.Exception)
            {
                SessionDirty = true;
                DirtyReason = job.Def.Id + " s" + job.Seed + " ended " + job.Outcome;
            }
            yield return null;
        }

        private static bool TryRun(ScenarioJob job, string phase, Action body, Outcome onFail = Outcome.SetupFailed)
        {
            try
            {
                body();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ScenarioHarness] " + phase + " failed: " + ex);
                job.Notes.Add(phase + ": " + ex.GetType().Name + ": " + ex.Message);
                job.Decide(onFail, phase + ": " + ex.GetType().Name + ": " + ex.Message, false);
                return false;
            }
        }

        private static Exception TryRunFinal(Action body, ScenarioJob job)
        {
            try
            {
                body();
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ScenarioHarness] finalize failed: " + ex);
                job.Notes.Add("finalize: " + ex.GetType().Name + ": " + ex.Message);
                return ex;
            }
        }

        // ── Phases ──────────────────────────────────────────────────────

        internal static string Preflight(GameManager gm)
        {
            if (!Application.isPlaying)
                return "not in Play mode";
            if (gm == null)
                return "GameManager.Instance is null";
            if (!gm.Harness_IsReady())
                return "GameManager is not ready (grid, combat UI or turn service missing)";
            string dataPath = Application.dataPath.Replace('\\', '/');
            if (!dataPath.EndsWith("DNDPrototype/Assets", StringComparison.Ordinal))
                return "unexpected project " + dataPath;
            if (Tests.Runner.StaticSuiteRunner.RanInPlayModeThisDomain)
                return "needsFresh: static suites ran in this Play session";
            if (SessionDirty)
                return "needsFresh: " + DirtyReason;
            return null;
        }

        private static CharacterCreationData[] BuildCreationParty(ScenarioDef def, bool defaultWhenNone)
        {
            var classes = new List<string>();
            foreach (ActorSpec a in def.Actors)
                if (a.Source.Kind == ActorSourceKind.QuickStart)
                    classes.Add(a.Source.ClassName);
            if (classes.Count == 0 && defaultWhenNone)
                classes.AddRange(DefaultParty);
            var data = new CharacterCreationData[classes.Count];
            for (int i = 0; i < classes.Count; i++)
                data[i] = ActorSource.CreateQuickStart(classes[i]);
            return data;
        }

        /// <summary>Null when the world is clean, else the problems.</summary>
        internal static string CleanCheck(GameManager gm)
        {
            var problems = new List<string>();
            for (int i = 0; i < gm.NPCs.Count; i++)
            {
                CharacterController npc = gm.NPCs[i];
                if (npc != null && npc.gameObject != null && npc.gameObject.activeSelf)
                    problems.Add("pool slot " + i + " (" + (npc.Stats != null ? npc.Stats.CharacterName : npc.name) + ") is active");
            }
            if (_baselineNpcCount < 0)
                _baselineNpcCount = gm.NPCs.Count;
            else if (gm.NPCs.Count != _baselineNpcCount)
                problems.Add("NPC list has " + gm.NPCs.Count + " entries (baseline " + _baselineNpcCount + "): a summon was left");

            foreach (CharacterController cc in Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude))
                if (!gm.PCs.Contains(cc))
                    problems.Add("live controller " + cc.name + " outside the party");

            if (AreaEffectManager.HasInstance && AreaEffectManager.Instance.GetActiveEffectCount() > 0)
                problems.Add(AreaEffectManager.Instance.GetActiveEffectCount() + " active area effects");

            var subs = new Dictionary<string, int>
            {
                { "TurnStarted", GameEventSystem.Instance.GetSubscriberCount<TurnStartedEvent>() },
                { "TurnEnded", GameEventSystem.Instance.GetSubscriberCount<TurnEndedEvent>() },
                { "NewRound", GameEventSystem.Instance.GetSubscriberCount<NewRoundEvent>() },
                { "CombatStarted", GameEventSystem.Instance.GetSubscriberCount<CombatStartedEvent>() }
            };
            if (_baselineSubs == null)
                _baselineSubs = subs;
            else
                foreach (KeyValuePair<string, int> kv in subs)
                    if (_baselineSubs.TryGetValue(kv.Key, out int b) && b != kv.Value)
                        problems.Add(kv.Key + " has " + kv.Value + " subscribers (baseline " + b + ")");

            string hooks = ScenarioHooks.DescribeSet();
            if (hooks != null)
                problems.Add("scenario hooks still set: " + hooks);

            return problems.Count == 0 ? null : string.Join("; ", problems);
        }

        private void Setup(ScenarioJob job, GameManager gm)
        {
            ScenarioDef def = job.Def;

            // d. Seed and HP mode
            _savedRandom = Random.state;
            _randomSaved = true;
            Random.InitState(job.Seed);
            _savedHpMode = GameSettings.Instance.hpCalculationMode;
            _hpModeSaved = true;
            GameSettings.Instance.hpCalculationMode = HPCalculationMode.Average;

            CharacterController[] slots = { gm.PC1, gm.PC2, gm.PC3, gm.PC4 };
            _savedControl.Clear();
            foreach (CharacterController pc in slots)
                if (pc != null)
                    _savedControl.Add(new KeyValuePair<CharacterController, (CharacterTeam, bool, DND35.AI.AIProfile)>(pc, (pc.Team, pc.IsControllable, pc.aiProfile)));

            // e. Spawn. Party slots: QuickStart actors first (in def order), then Stats actors.
            var quick = def.Actors.Where(a => a.Source.Kind == ActorSourceKind.QuickStart).ToList();
            var built = def.Actors.Where(a => a.Source.Kind == ActorSourceKind.Stats).ToList();
            var npcs = def.Actors.Where(a => a.Source.Kind == ActorSourceKind.Npc).ToList();

            if (quick.Count > 0)
            {
                string reason = gm.Harness_SetupPartyFromCreation(BuildCreationParty(def, false));
                if (reason != null)
                    throw new InvalidOperationException("party setup: " + reason);
            }

            var placed = new List<KeyValuePair<ActorSpec, CharacterController>>();
            for (int i = 0; i < quick.Count; i++)
                placed.Add(new KeyValuePair<ActorSpec, CharacterController>(quick[i], slots[i]));
            for (int j = 0; j < built.Count; j++)
            {
                int slot = quick.Count + j;
                CharacterStats stats = built[j].Source.Build();
                CharacterController pc = gm.Harness_SetupPartySlot(slot, stats, built[j].Pos);
                if (pc == null)
                    throw new InvalidOperationException("could not set up PC slot " + slot + " for " + built[j].Key);
                placed.Add(new KeyValuePair<ActorSpec, CharacterController>(built[j], pc));
            }
            for (int slot = quick.Count + built.Count; slot < slots.Length; slot++)
                gm.Harness_DeactivatePartySlot(slot);

            gm.Harness_SpawnEnemies(npcs.Select(a => a.Source.NpcId).ToList(), npcs.Select(a => a.Pos).ToArray());
            for (int i = 0; i < npcs.Count; i++)
            {
                CharacterController npc = i < gm.NPCs.Count ? gm.NPCs[i] : null;
                if (npc == null || npc.Stats == null || !npc.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("NPC '" + npcs[i].Source.NpcId + "' for " + npcs[i].Key + " did not spawn");
                placed.Add(new KeyValuePair<ActorSpec, CharacterController>(npcs[i], npc));
            }

            // Save every actor's control (pool slots are reused by later jobs).
            foreach (KeyValuePair<ActorSpec, CharacterController> kv in placed)
                if (!_savedControl.Any(s => s.Key == kv.Value))
                    _savedControl.Add(new KeyValuePair<CharacterController, (CharacterTeam, bool, DND35.AI.AIProfile)>(kv.Value, (kv.Value.Team, kv.Value.IsControllable, kv.Value.aiProfile)));

            // Exact placement: clear everyone's occupancy, then place in def order.
            foreach (KeyValuePair<ActorSpec, CharacterController> kv in placed)
                gm.Grid.ClearCreatureOccupancy(kv.Value);
            foreach (ActorSpec spec in def.Actors)
            {
                CharacterController c = placed.First(p => p.Key == spec).Value;
                SquareCell cell = gm.Grid.GetCell(spec.Pos);
                if (cell == null)
                    throw new InvalidOperationException(spec.Key + ": no grid cell at " + spec.Pos);
                c.MoveToCell(cell, markAsMoved: false);
                if (c.GridPosition != spec.Pos)
                    throw new InvalidOperationException(spec.Key + " could not be placed at " + spec.Pos + " (it is at " + c.GridPosition + "; footprint " + c.GetVisualSquaresOccupied() + ")");
                job.Ctx.Actors[spec.Key] = c;
                job.SpecOf[c] = spec;
            }

            // f. Control
            foreach (ActorSpec spec in def.Actors)
            {
                CharacterController c = job.Ctx.Actors[spec.Key];
                if (spec.Control == Control.Ui)
                {
                    c.ConfigureTeamControl(spec.Team, true);
                }
                else
                {
                    c.ConfigureTeamControl(spec.Team, false);
                    if (spec.Source.IsPcSlot || spec.Profile != null)
                    {
                        DND35.AI.AIProfile profile;
                        bool owned;
                        if (spec.Profile != null)
                        {
                            // Destroy only an override instance the harness can own: never an asset, and never one
                            // another controller (or a saved control state) still uses.
                            profile = spec.Profile();
                            owned = profile != null && !EditorUtility.IsPersistent(profile) && !IsProfileInUse(profile);
                        }
                        else
                        {
                            profile = AiProfileForClass.Create(c);
                            owned = profile != null;
                        }
                        if (owned)
                            _createdProfiles.Add(profile);
                        c.aiProfile = profile;
                    }
                }
            }

            foreach (ActorSpec spec in def.Actors)
            {
                CharacterController c = job.Ctx.Actors[spec.Key];
                foreach (KeyValuePair<CombatConditionType, int> cond in spec.StartConditions)
                    gm.ApplyCondition(c, cond.Key, cond.Value, sourceNameOverride: "ScenarioHarness", sourceCategory: "Harness");
                if (spec.Hp.HasValue)
                {
                    c.Stats.CurrentHP = spec.Hp.Value;
                    c.SyncHPStateFromCurrentHP(emitLog: false);
                }
                if (!string.IsNullOrEmpty(spec.PriorityTarget))
                    c.PriorityTargetName = job.Ctx.Actors[spec.PriorityTarget].Stats.CharacterName;
                spec.Tweak?.Invoke(c);
            }
            def.OnSetup?.Invoke(job.Ctx);

            if (def.InitiativeOrder != null && def.InitiativeOrder.Count > 0)
                ScenarioHooks.ForcedFirstInitiative = def.InitiativeOrder.Select(k => job.Ctx.Actors[k]).ToList();
            ScenarioHooks.ScriptedTurn = ScenarioHooks.SafeTurn(job.GetScriptedTurn);

            // g. Trace, checks, dice, input, log, fast mode
            job.Trace = new ScenarioTrace(job, gm);
            job.Ctx.Trace = job.Trace;
            job.Checks = new ScenarioChecks(job, gm) { WallClockCapSeconds = job.Options.WallCapSeconds };
            foreach (ActorSpec spec in def.Actors)
            {
                CharacterController c = job.Ctx.Actors[spec.Key];
                job.Trace.RegisterActor(spec.Key, c);
                job.Checks.RegisterActor(c);
            }
            job.Trace.Install();

            bool record = def.RecordDice || job.Options.RecordDice;
            if (def.DiceForces.Count > 0 || record)
            {
                List<DiceForce> forces = def.DiceForces.Select(f => f.Clone()).ToList();
                ScenarioTrace trace = job.Trace;
                ScenarioHooks.RollFilter = (sides, ctx, natural) =>
                {
                    int value = natural;
                    bool forced = false;
                    foreach (DiceForce f in forces)
                    {
                        if (!f.Matches(sides, ctx))
                            continue;
                        value = f.Value;
                        if (f.Remaining > 0)
                            f.Remaining--;
                        forced = true;
                        break;
                    }
                    if (forced || record)
                        trace.EmitDice(sides, ctx, natural, value, forced);
                    return value;
                };
            }
            ScenarioHooks.SuppressPlayerInput = true;

            _logHandler = (condition, stack, type) =>
            {
                if (type == LogType.Log || type == LogType.Warning)
                    return;
                if (job.Checks == null || job.Halting || !job.Started)
                    return;
                job.Checks.OnLogMessage(condition, stack, type);
            };
            Application.logMessageReceived += _logHandler;

            if (!ScenarioFastMode.Apply(job.Options.Mode))
                throw new InvalidOperationException("fast mode '" + job.Options.Mode + "' refused");

            job.Trace.EmitMeta(job.Options.Mode);
            foreach (ActorSpec spec in def.Actors)
                job.Trace.EmitActor(spec.Key, job.Ctx.Actors[spec.Key], spec);
        }

        private bool IsProfileInUse(DND35.AI.AIProfile profile)
        {
            if (_createdProfiles.Contains(profile))
                return true;
            foreach (KeyValuePair<CharacterController, (CharacterTeam team, bool controllable, DND35.AI.AIProfile profile)> kv in _savedControl)
                if (kv.Value.profile == profile)
                    return true;
            foreach (CharacterController cc in Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Include))
                if (cc != null && cc.aiProfile == profile)
                    return true;
            return false;
        }

        private static void HandleUiTurn(ScenarioJob job, GameManager gm)
        {
            CharacterController a = job.PendingUiEnd;
            if (a == null || Time.frameCount <= job.PendingUiEndFrame)
                return;
            if (gm.CurrentCharacter != a)
            {
                job.PendingUiEnd = null;
                return;
            }
            if (!gm.IsPlayerTurn || gm.ActivePC != a)
                return;
            job.PendingUiEnd = null;
            job.Trace.Emit("step").Set("actor", job.Trace.KeyOf(a)).Set("step", "EndTurn").Set("note", "no Ui steps");
            gm.OnEndTurnButtonPressed();
        }

        private JobResult Finalize(ScenarioJob job, GameManager gm)
        {
            var result = new JobResult
            {
                Id = job.Def.Id,
                Seed = job.Seed,
                Rep = job.Rep,
                Outcome = job.Outcome,
                Reason = job.OutcomeReason
            };
            result.Notes.AddRange(job.Notes);

            if (job.Trace == null)
            {
                result.Verdict = VerdictRules.IsErrorOutcome(job.Outcome) ? Verdict.Error : Verdict.Inconclusive;
                return result;
            }

            job.Checks.CheckHookErrors();
            ScenarioTrace trace = job.Trace;
            trace.Emit("end").Set("outcome", job.Outcome).Set("reason", job.OutcomeReason).Set("final", trace.SnapshotAll())
                 .Set("wallSec", Time.realtimeSinceStartup - job.StartRealtime);

            var view = new TraceView(trace.Events, job.Outcome, job.OutcomeReason);
            foreach (Expectation e in job.Def.Expectations)
            {
                ExpectResult r;
                try
                {
                    r = e.Check(view);
                }
                catch (Exception ex)
                {
                    r = ExpectResult.Fail("check threw " + ex.GetType().Name + ": " + ex.Message);
                }
                if (r.Status == null)
                    r = ExpectResult.Inconclusive("check returned no status");
                job.ExpectationResults.Add(new KeyValuePair<Expectation, ExpectResult>(e, r));
                trace.Emit("expectation").Set("name", e.Name).Set("status", r.Status).Set("detail", r.Detail)
                     .Set("xfail", e.XFailIssue).Set("seqs", r.Seqs ?? new int[0]);
                result.Expectations.Add((string.IsNullOrEmpty(e.XFailIssue) ? "" : "[xfail " + e.XFailIssue + "] ") + r.Status.ToUpperInvariant() + " " + e.Name + ": " + r.Detail);
            }

            result.Verdict = VerdictRules.Compute(job.Outcome, job.ExpectationResults, job.Unwaived, job.Waived, job.HookErrorViolations);
            trace.Emit("verdict").Set("verdict", result.Verdict).Set("unwaived", job.Unwaived).Set("waived", job.Waived).Set("hookErrors", job.HookErrorViolations);

            foreach (TraceEvent v in trace.Events)
            {
                if (v.Ev != "violation" || result.Violations.Count >= 50)
                    continue;
                string by = v.Str("waivedBy");
                result.Violations.Add((by != null ? "[" + by + "] " : "") + v.Str("inv") + ": " + v.Str("detail"));
            }

            result.Rounds = trace.Events.Where(e => e.Ev == "round").Select(e => e.Int("n")).DefaultIfEmpty(0).Max();
            result.Events = trace.Events.Count;
            List<string> lines = trace.HashLines();
            result.Hash = ScenarioTrace.Hash(lines);

            string slug = Slug(job.Def.Id) + "__s" + job.Seed + (job.Options.Repeat > 1 ? "__r" + job.Rep : "");
            string path = Path.Combine(RunDir, slug + ".jsonl");
            trace.WriteJsonl(path);
            result.Path = path.Replace('\\', '/');

            string repKey = job.Def.Id + "|" + job.Seed;
            if (job.Options.Repeat > 1)
            {
                if (job.Rep == 1)
                {
                    _firstRepLines[repKey] = lines;
                    _firstRepHash[repKey] = result.Hash;
                }
                else if (_firstRepHash.TryGetValue(repKey, out string firstHash))
                {
                    if (firstHash == result.Hash)
                    {
                        result.RepeatCheck = "same";
                    }
                    else
                    {
                        result.RepeatCheck = "differs";
                        List<string> first = _firstRepLines[repKey];
                        int n = Math.Min(first.Count, lines.Count);
                        int diff = n + 1;
                        for (int i = 0; i < n; i++)
                        {
                            if (first[i] != lines[i])
                            {
                                diff = i + 1;
                                break;
                            }
                        }
                        result.FirstDiffSeq = diff;
                    }
                }
            }
            return result;
        }

        /// <summary>Every step has its own guard: one failing restore must not skip the others or end the run.</summary>
        private void Cleanup(ScenarioJob job, GameManager gm)
        {
            job.Halting = true;
            if (_logHandler != null)
            {
                Application.logMessageReceived -= _logHandler;
                _logHandler = null;
            }
            try { job.Trace?.Uninstall(); } catch (Exception ex) { job.Notes.Add("uninstall: " + ex.Message); }
            try { ScenarioHooks.ClearAll(); } catch (Exception ex) { job.Notes.Add("clear hooks: " + ex.Message); }

            if (!job.Options.Keep && gm != null && gm.Harness_IsReady() && job.Outcome != Outcome.Contaminated)
            {
                try { gm.Harness_HaltCombat("cleanup"); } catch (Exception ex) { job.Notes.Add("cleanup halt: " + ex.Message); }
                try
                {
                    string err = gm.Harness_ResetWorld("cleanup " + job.Def.Id);
                    if (err != null)
                    {
                        job.Notes.Add("cleanup reset: " + err);
                        SessionDirty = true;
                        DirtyReason = "dirty reset after " + job.Def.Id + ": " + err;
                    }
                }
                catch (Exception ex) { job.Notes.Add("cleanup reset: " + ex.Message); }
                try { gm.Harness_RestorePartyAfterCombat(); } catch (Exception ex) { job.Notes.Add("cleanup rest: " + ex.Message); }
            }

            foreach (KeyValuePair<CharacterController, (CharacterTeam team, bool controllable, DND35.AI.AIProfile profile)> kv in _savedControl)
            {
                if (kv.Key == null)
                    continue;
                try
                {
                    kv.Key.ConfigureTeamControl(kv.Value.team, kv.Value.controllable);
                    kv.Key.aiProfile = kv.Value.profile;
                    kv.Key.PriorityTargetName = null;
                }
                catch (Exception ex)
                {
                    job.Notes.Add("restore control of " + kv.Key.name + ": " + ex.Message);
                    SessionDirty = true;
                    DirtyReason = "control restore failed after " + job.Def.Id + ": " + ex.Message;
                }
            }
            _savedControl.Clear();
            foreach (Object p in _createdProfiles)
            {
                try { if (p != null) Destroy(p); }
                catch (Exception ex) { job.Notes.Add("destroy profile: " + ex.Message); }
            }
            _createdProfiles.Clear();

            if (_randomSaved)
            {
                Random.state = _savedRandom;
                _randomSaved = false;
            }
            if (_hpModeSaved)
            {
                try { GameSettings.Instance.hpCalculationMode = _savedHpMode; }
                catch (Exception ex) { job.Notes.Add("restore HP mode: " + ex.Message); }
                _hpModeSaved = false;
            }
            try { ScenarioFastMode.Restore(); }
            catch (Exception ex) { job.Notes.Add("restore fast mode: " + ex.Message); }
        }

        private static string Slug(string id)
        {
            var sb = new StringBuilder(id.Length);
            foreach (char c in id)
                sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            return sb.ToString();
        }

        // ── Status and summary ──────────────────────────────────────────

        internal JsonObj StatusObject()
        {
            var o = new JsonObj();
            o.Set("runId", RunId).Set("state", State.ToString().ToLowerInvariant())
             .Set("heartbeatUtc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture))
             .Set("job", Math.Min(JobIndex + 1, Jobs.Count)).Set("jobs", Jobs.Count)
             .Set("frame", Time.frameCount)
             .Set("wallSec", Time.realtimeSinceStartup - _runStartRealtime);
            if (InterruptReason != null)
                o.Set("interrupted", InterruptReason);
            ScenarioJob job = Current;
            GameManager gm = GameManager.Instance;
            if (job != null)
            {
                o.Set("id", job.Def.Id).Set("seed", job.Seed).Set("rep", job.Rep)
                 .Set("round", gm != null ? gm.CurrentRound : 0)
                 .Set("jobFrames", Time.frameCount - job.StartFrame)
                 .Set("outcome", job.Outcome);
                CharacterController cur = gm != null ? gm.CurrentCharacter : null;
                // TryKeyOf: a status write (timed by the wall clock) must never register an actor into the trace.
                if (cur != null && job.Trace != null)
                    o.Set("actor", job.Trace.TryKeyOf(cur) ?? cur.name).Set("controller", job.ControllerLabel(cur));
                if (job.Trace != null)
                    o.Set("events", job.Trace.Events.Count).Set("last", job.Trace.LastLines(5));
            }
            o.Set("totals", Totals());
            if (RunVerdict != null)
                o.Set("verdict", RunVerdict);
            o.Set("summaryPath", Path.Combine(RunDir ?? "", "summary.json").Replace('\\', '/'));
            if (SummaryError != null)
                o.Set("summaryError", SummaryError);
            if (SessionDirty)
                o.Set("needsFresh", true).Set("dirtyReason", DirtyReason);
            else if (Tests.Runner.StaticSuiteRunner.RanInPlayModeThisDomain)
                o.Set("needsFresh", true).Set("dirtyReason", "static suites ran in this Play session");
            return o;
        }

        private JsonObj Totals()
        {
            var t = new JsonObj();
            foreach (Verdict v in Enum.GetValues(typeof(Verdict)))
            {
                int n = Results.Count(r => r.Verdict == v);
                if (n > 0)
                    t.Set(v.ToString(), n);
            }
            return t;
        }

        internal void WriteStatus(bool force)
        {
            if (RunDir == null)
                return;
            if (!force && Time.realtimeSinceStartup - _lastStatusWrite < 1f)
                return;
            _lastStatusWrite = Time.realtimeSinceStartup;
            try
            {
                Json.WriteFileAtomic(Path.Combine(RunDir, "status.json"), Json.Serialize(StatusObject()));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ScenarioHarness] status.json: " + ex.Message);
            }
        }

        private void WriteSummary()
        {
            var byId = new JsonObj();
            foreach (IGrouping<string, JobResult> g in Results.GroupBy(r => r.Id))
            {
                Verdict agg = VerdictRules.Aggregate(g.Select(r => r.Verdict));
                var o = new JsonObj();
                o.Set("verdict", agg).Set("seeds", g.Select(r => r.Seed).Distinct().ToList())
                 .Set("verdicts", g.Select(r => r.Seed + (r.Rep > 1 ? "r" + r.Rep : "") + ":" + r.Verdict).ToList());
                byId.Set(g.Key, o);
            }

            var unused = new List<string>();
            var hits = new JsonObj();
            var usedDefs = Jobs.Select(j => j.Def).Distinct().ToList();
            foreach (Waiver w in KnownIssueWaivers.Global)
            {
                string key = "global " + w;
                _waiverHits.TryGetValue(key, out int n);
                if (n == 0) unused.Add(key); else hits.Set(key, n);
            }
            foreach (ScenarioDef d in usedDefs)
                foreach (Waiver w in d.Waivers)
                {
                    string key = d.Id + " " + w;
                    _waiverHits.TryGetValue(key, out int n);
                    if (n == 0) unused.Add(key); else hits.Set(key, n);
                }

            bool interrupted = State == RunState.Interrupted;
            bool error = Results.Any(r => r.Verdict == Verdict.Error) || (Results.Count < Jobs.Count && !AbortRequested && !Options.Keep);
            bool fail = Results.Any(r => r.Verdict == Verdict.Fail);
            bool xpass = Results.Any(r => r.Verdict == Verdict.XPass);
            bool repeatDiff = Results.Any(r => r.RepeatCheck == "differs");
            RunVerdict = interrupted ? "INTERRUPTED" : error ? "ERROR" : fail ? "FAIL" : xpass ? "XPASS" : repeatDiff ? "NONDETERMINISTIC" : AbortRequested ? "ABORTED" : "OK";

            // Jobs without a result (a dirty session, an abort or an interruption stopped the run) and how to resume them.
            List<ScenarioJob> notRun = Jobs.Where(j => !j.ResultRecorded).ToList();
            var notRunList = notRun.Select(j => j.Def.Id + " s" + j.Seed + (j.Options.Repeat > 1 ? " r" + j.Rep : "")).ToList();
            JsonObj resume = null;
            if (notRun.Count > 0)
            {
                resume = new JsonObj();
                bool adhoc = Filter != null && Filter.StartsWith("adhoc:", StringComparison.Ordinal);
                resume.Set("filter", adhoc ? null : string.Join(",", notRun.Select(j => j.Def.Id).Distinct()))
                      .Set("seeds", string.Join(",", notRun.Select(j => j.Seed).Distinct()))
                      .Set("options", Options.ToString())
                      .Set("note", adhoc ? "an inline definition: pass the same definition to Start again" : "Start(filter, seeds, options) in a fresh Play session (filter and seeds cover every not-run pair, plus any finished pair they combine with)");
            }

            var s = new JsonObj();
            s.Set("runId", RunId).Set("verdict", RunVerdict).Set("filter", Filter).Set("seeds", SeedsText).Set("options", Options.ToString())
             .Set("utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture))
             .Set("jobs", Jobs.Count).Set("completed", Results.Count)
             .Set("aborted", AbortRequested ? AbortReason ?? "yes" : null)
             .Set("interrupted", InterruptReason)
             .Set("notRun", notRunList).Set("resume", resume)
             .Set("loadErrors", LoadErrors.Count > 0 ? LoadErrors : null)
             .Set("totals", Totals()).Set("byId", byId)
             .Set("waiverHits", hits).Set("unusedWaivers", unused)
             .Set("needsFresh", SessionDirty).Set("dirtyReason", DirtyReason)
             .Set("wallSec", Time.realtimeSinceStartup - _runStartRealtime)
             .Set("results", Results.Select(r => r.ToJson()).ToList());
            SummaryJson = Json.Serialize(s);
            try
            {
                Json.WriteFileAtomic(Path.Combine(RunDir, "summary.json"), SummaryJson);
                SummaryError = null;
                var idx = new JsonObj();
                idx.Set("runId", RunId).Set("utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)).Set("verdict", RunVerdict)
                   .Set("filter", Filter).Set("seeds", SeedsText).Set("options", Options.ToString()).Set("totals", Totals());
                File.AppendAllText(Path.Combine(ScenariosDir, "index.jsonl"), Json.Serialize(idx) + "\n", new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                SummaryError = ex.GetType().Name + ": " + ex.Message;
                Debug.LogWarning("[ScenarioHarness] summary: " + ex.Message);
            }
        }
    }
}
#endif
