using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>How the presentation layer shows what the rules did (grouped AI turns steps 8 and 9).</summary>
public enum PresentationMode
{
    /// <summary>Every facade call applies at once, in the same order and the same frame as before the facade.</summary>
    Passthrough,
    /// <summary>A group of AI turns is recorded: facade calls become beats that the playback shows later (step 9).</summary>
    Holding,
}

/// <summary>How the playback consumes the queue (grouped AI turns step 9, "Playback").</summary>
public enum PlaybackMode
{
    /// <summary>Each Pace beat lasts its seconds divided by the AI speed, each Step beat its step seconds divided by it (play, watch mode).</summary>
    Timed,
    /// <summary>Every recorded beat applies in the same frame's LateUpdate (the scenario harness's fast and fine modes).</summary>
    Instant,
}

/// <summary>Why a group closed (grouped AI turns "The group" (a)-(d)).</summary>
public enum GroupStop
{
    /// <summary>(a) control returned to a player-run creature: the action buttons show (S1).</summary>
    Control,
    /// <summary>(b) a defender or reactor prompt to a player-run creature opened (S3).</summary>
    Prompt,
    /// <summary>(c) combat ended (S4).</summary>
    CombatEnd,
    /// <summary>(d) a reset or a harness halt (S6).</summary>
    Reset,
}

/// <summary>
/// What a window stop marks (grouped AI turns step 10, "Stopping points" S2 and S7; design choice C1). Presentation only:
/// a mark never pauses the rules. The overlap scheduler (step 11, PlaybackScheduler R7) ends the overlap window at it: the
/// lane holding the mark plays after every earlier beat, at its recorded pacing divided by the AI speed, and the next lane
/// opens a new window.
/// </summary>
public enum StopKind
{
    /// <summary>S2: a player-run creature makes an attack of opportunity inside a group (ThreatSystem.ExecuteAoO; the owner's ruling).</summary>
    AoO,
    /// <summary>
    /// S2: a player-run creature's attacked reaction that the AI fallback answered (a controllable giant bombardier beetle's
    /// acid spray where the resolving path cannot wait; GameManager.RunAttackedReaction).
    /// </summary>
    AutomaticReaction,
    /// <summary>
    /// S7, reserved: a player-run creature's automatic trigger of a readied action or readied counterspell (CMB-029, SPL-047;
    /// neither exists yet). No caller; a readied prompt would be an Interactive-class call instead.
    /// </summary>
    PlayerReaction,
}

/// <summary>One window stop of a group (CombatPresentation.MarkStop): its kind, the reacting creature, its beat's seq and the lane it fell in.</summary>
public sealed class StopMark
{
    public StopKind Kind;
    public string Actor;
    public int Seq;
    /// <summary>The group's lane in force when the mark was made ("round:actor"), or null before the first lane.</summary>
    public string Lane;

    public override string ToString() => Kind + ":" + Actor;
}

/// <summary>
/// One recorded group (grouped AI turns step 9): its lanes (the AI-decided turns that opened or continued it, as
/// "round:actor"), why and at which seq it closed, its beat count and its playback length at the speed in force when it
/// was recorded. Read by the scenario harness (the groups.jsonl sidecar, ExpectGroups); never a trace event.
/// </summary>
public sealed class GroupRecord
{
    public int Id;
    public int OpenSeq;
    public int CloseSeq;
    /// <summary>Null while the group is open.</summary>
    public GroupStop? Stop;
    public int Beats;
    /// <summary>
    /// The beats still to play when the group closed (step 10): above 0 for a prompt asked while the playback was behind
    /// (S3), which then opens only after them.
    /// </summary>
    public int PendingAtClose;
    /// <summary>The Timed playback length at the speed in force when it was recorded, played one beat after another (the overlap playback is shorter).</summary>
    public float PlaybackSeconds;
    /// <summary>The window breaks recorded (S5, step 11: a creature's second turn in an overlap window).</summary>
    public int WindowBreaks;
    public readonly List<string> Lanes = new List<string>();
    /// <summary>The window stops recorded while the group was open, in rules order (step 10, S2; CombatPresentation.MarkStop).</summary>
    public readonly List<StopMark> Stops = new List<StopMark>();
    public bool IsOpen => Stop == null;
}

/// <summary>
/// The class of a public <see cref="CombatUI"/> method (docs/designs/grouped_ai_turns.md "`CombatUI` method classes"):
/// how the facade treats it while a group is recorded (step 9 on). In Passthrough every class applies at once.
/// </summary>
public enum UiCallClass
{
    /// <summary>A value beat: recorded with its call-time values while queueing, applied at once otherwise.</summary>
    Queued,
    /// <summary>The control beat (the action buttons show, S1): a boundary, then the show.</summary>
    Control,
    /// <summary>An interactive barrier (a defender prompt, the defeat panel): a boundary, then the show.</summary>
    Interactive,
    /// <summary>Any other prompt, menu or picker: it catches up first if it is ever called while the presentation holds or is behind (step 10, CatchUpBeforePrompt).</summary>
    CatchUp,
    /// <summary>Queries, hides, ensures, out-of-combat panels and PC-turn controls: always at once.</summary>
    Live,
}

/// <summary>A rules boundary at which the token keyframes are taken (CombatPresentation.Boundary).</summary>
public enum PresentationBoundary
{
    /// <summary>CombatPacing.Wait.</summary>
    Wait,
    /// <summary>A CharacterController.MoveAlongPath step (CombatPresentation.RecordStep).</summary>
    Step,
    /// <summary>Before the control beat (CombatUI.ShowActionButtonsForCharacter).</summary>
    Control,
    /// <summary>Before an Interactive-class call (CombatUI.ShowConfirmationDialog, CombatUI.ShowDefeatPanel).</summary>
    Interactive,
    /// <summary>TurnControl.CombatEnded.</summary>
    CombatEnd,
}

/// <summary>
/// The presentation layer of grouped AI turns (owner request 2026-10-30; docs/designs/grouped_ai_turns.md
/// "Recording (resolve now, show later)", "Run-ahead state kept hidden", "Playback", "Drains and clears", "Input").
///
/// Step 8 (gt-presentation-gates) routed every visual an AI turn can reveal through here: the <see cref="CombatUI"/>
/// facade (its Queued, Control and Interactive classes), the SquareCell highlight and ground-item gates, the
/// PersistentAreaEffect indicator hooks and <see cref="Defer"/> for one-shot visuals. In Passthrough each call applies at
/// once, in the same order and frame as before.
///
/// Step 9 (gt-run-ahead) adds Holding. The TurnControl marks open a group at an AI-decided turn's start (when
/// GameSettings.groupAiTurns is on, read once per group) and close it at the control beat, at a defender prompt, at
/// combat end and at a reset (<see cref="OpenGroup"/>, <see cref="CloseGroup"/>). While a group is open (and afterwards,
/// while the queue is not empty: queue while behind) the rules run unchanged and at once, and every facade, cell, area
/// and token change is recorded as a <see cref="PresentationBeat"/> with its call-time values: CombatPacing.Wait records
/// a Pace beat and returns one frame (<see cref="HoldingWait"/>), each MoveAlongPath step a Step beat, each boundary a
/// Keyframe beat for every token whose picture changed (TokenPicture), a token created or destroyed meanwhile a Spawned
/// or Removed beat. The playback (this MonoBehaviour's LateUpdate) shows them: a Pace beat lasts its seconds divided by
/// the AI speed and a Step beat tweens the token's held view (<see cref="Playback"/> Timed), or everything applies in the
/// frame, in rules order (Instant, the harness's fast and fine modes). World input, the I/K/C toggles and End Turn
/// wait while <see cref="BlocksInput"/>. When the queue drains with no group open, the held views copy their models
/// again and the <see cref="WhenDrained"/> actions run (the loot window). The rules never wait on the playback, so every
/// rules outcome and trace hash is the same with grouping on or off (NEUTRAL; the harness's group=pair proves it).
///
/// Step 10 (gt-stop-points) adds the stopping points: a player-run creature's automatic reaction inside a group is a
/// window stop (<see cref="MarkStop"/>, S2; the reserved S7 kind for readied actions), a confirmation dialog asked for
/// while the presentation holds or is behind closes the group and is an interactive barrier the playback opens after every
/// earlier beat (CombatUI.ShowConfirmationDialog, S3; the acid-spray menu closes the group and waits for the drain,
/// GameManager.PromptAttackedReaction), and any other prompt shown meanwhile catches the playback up first
/// (<see cref="CatchUpBeforePrompt"/>, the fallback).
///
/// Step 11 (gt-overlap-and-options, design choice C5) overlaps the Timed playback: every beat carries its turn (the lane
/// key) and, for a token step, its cells; the pure PlaybackScheduler plays token steps in parallel lanes, one per turn
/// (at most 3, each started 0.15 s / speed after the one before), and every other beat on the result track, strictly in
/// rules order (R1-R8), so the log shows the same lines in the same order as live play and each token's keyframes play
/// in their rules order. A window ends at a window stop (S2), at a creature's second turn in the window (S5, the
/// WindowBreak beat <see cref="NoteLane"/> records) and at a barrier (the control and interactive beats). The banner (the
/// turn indicator) is a result beat, so it names the actor of the result beat playing. The camera does not move (Q2).
///
/// <see cref="Boundary"/> is called at every rules boundary a group has (each CombatPacing.Wait, each MoveAlongPath step,
/// before the control beat and every Interactive-class call, and in TurnControl.CombatEnded). It records the keyframe
/// beats while queueing and feeds <see cref="PresentationTrace"/> (editor and development builds, while the scenario
/// harness records); it never changes a returned yield.
///
/// The singleton (<see cref="Instance"/>, the playback loop) is a MonoBehaviour on its own lazily created root
/// GameObject, never on GameManager: GameManager.Awake runs before scene references are assigned (CORE-009) and
/// ResetCombatStateForNextEncounter stops GameManager's coroutines (CORE-013), so a reset never stops the playback
/// silently; <see cref="Clear"/> is explicit.
///
/// Presentation only: nothing here draws from UnityEngine.Random, writes rules state, or calls SetActive or Destroy on a
/// model (a source scan in Tests.UI.CombatPresentationTests pins it). A beat's apply that throws is caught and warned
/// (a presentation fault never reaches a rules caller).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(9000)]
public sealed class CombatPresentation : MonoBehaviour
{
    /// <summary>The name of the singleton's root GameObject.</summary>
    public const string RootName = "CombatPresentation";

    /// <summary>The most groups the in-memory group log keeps (the harness clears it per job).</summary>
    public const int MaxGroupLog = 5000;

    private static CombatPresentation _instance;
    // The queue: beats from _head on wait to be applied, in rules order (seq). The applied prefix is compacted away.
    private static readonly List<PresentationBeat> _queue = new List<PresentationBeat>();
    // Applied beats leave a null slot (the overlap playback applies out of seq order); _head is the first slot not yet
    // known to be applied, _pending the beats not applied, and _queue[i].Seq is _queueBaseSeq + i.
    private static int _head;
    private static int _pending;
    private static int _queueBaseSeq = 1;
    private static readonly List<Action> _whenDrained = new List<Action>();
    private static int _seq;
    private static bool _holding;
    private static bool _applying;
    // The overlap playback (step 11): the pure scheduler, the highest seq fed to it, and a generation that a reset bumps.
    private static readonly PlaybackScheduler _scheduler = new PlaybackScheduler();
    private static int _fedSeq;
    private static int _schedulerGeneration;
    private static readonly List<PlaybackScheduler.Item> _finishedScratch = new List<PlaybackScheduler.Item>();
    private static readonly List<PlaybackScheduler.Item> _inFlightScratch = new List<PlaybackScheduler.Item>();
    private static float _stallSeconds;
    private static int _stallFrames;

    /// <summary>
    /// The Timed playback's watchdog: with the group closed and beats queued, this many seconds of frames (and at least
    /// <see cref="StallMinFrames"/> of them) with no beat finished or in flight is a stall, and the playback catches up
    /// (<see cref="SchedulerFallback"/>). Frames with no time (a paused game) never count.
    /// </summary>
    internal const float StallSeconds = 2f;

    /// <summary>The fewest stalled frames that trip the watchdog (<see cref="StallSeconds"/>).</summary>
    internal const int StallMinFrames = 30;

    /// <summary>
    /// The fallback name a scheduler fault or stall adds to <see cref="CatchUpFallbacks"/> (the harness's
    /// presentationFallbacks).
    /// </summary>
    public const string SchedulerFallbackName = "PlaybackScheduler";

    /// <summary>
    /// Test seam (Tests.UI.CombatPresentationTests): runs at the start of each Timed frame with the scheduler, before it is
    /// fed; a probe throws here (the fault isolation) or empties the scheduler (the stall watchdog). Null outside the tests.
    /// </summary>
    internal static Action<PlaybackScheduler> TimedFrameProbeForTests;

    // The turn a beat is recorded in (the lane key): changes when TurnService's current creature or the round changes.
    private static int _turnKey;
    private static CharacterController _turnCreature;
    private static int _turnRound;
    // S5: the creatures with a turn in the current overlap window.
    private static readonly PlaybackScheduler.WindowCap _windowCap = new PlaybackScheduler.WindowCap();
    private static GroupRecord _group;
    private static int _groupIds;
    private static readonly List<GroupRecord> _groups = new List<GroupRecord>();
    private static readonly List<string> _fallbacks = new List<string>();

    /// <summary>
    /// A test seam (Tests.UI.CombatPresentationTests, the trace fault check): when set, it replaces TokenKeyframe.Capture
    /// at a boundary. Null outside that test.
    /// </summary>
    internal static Func<TokenView, TokenKeyframe> KeyframeSourceForTests;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        _queue.Clear();
        _head = 0;
        _pending = 0;
        _queueBaseSeq = 1;
        _whenDrained.Clear();
        _seq = 0;
        _holding = false;
        _applying = false;
        _scheduler.Reset();
        _fedSeq = 0;
        _schedulerGeneration = 0;
        _finishedScratch.Clear();
        _inFlightScratch.Clear();
        _stallSeconds = 0f;
        _stallFrames = 0;
        TimedFrameProbeForTests = null;
        _turnKey = 0;
        _turnCreature = null;
        _turnRound = 0;
        _windowCap.Reset();
        _group = null;
        _groupIds = 0;
        _groups.Clear();
        _fallbacks.Clear();
        Playback = PlaybackMode.Timed;
        KeyframeSourceForTests = null;
    }

    /// <summary>
    /// The singleton, created on first use on its own root GameObject (<see cref="RootName"/>); null outside Play mode.
    /// Its LateUpdate is the playback loop.
    /// </summary>
    public static CombatPresentation Instance
    {
        get
        {
            if (_instance == null && Application.isPlaying)
            {
                var go = new GameObject(RootName);
                _instance = go.AddComponent<CombatPresentation>();
            }
            return _instance;
        }
    }

    /// <summary>True when the singleton exists (no creation).</summary>
    public static bool HasInstance => _instance != null;

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    /// <summary>
    /// The playback loop must exist while the presentation blocks input or waits to drain (a destroyed root is re-created
    /// by the next read), or BlocksInput would stay true with nothing pumping the queue.
    /// </summary>
    private static void EnsurePump()
    {
        if (_instance == null && Application.isPlaying && (_holding || PendingBeats > 0 || _whenDrained.Count > 0))
            _ = Instance;
    }

    /// <summary>The mode in force: Holding while a group is recorded, Passthrough otherwise.</summary>
    public static PresentationMode Mode => _holding ? PresentationMode.Holding : PresentationMode.Passthrough;

    /// <summary>True while a group is recorded (from its first AI turn's mark to its close).</summary>
    public static bool Holding => _holding;

    /// <summary>
    /// How the playback consumes the queue: Timed in play (the default), Instant for the scenario harness's fast and fine
    /// modes (RunOptions playback=). Presentation only.
    /// </summary>
    public static PlaybackMode Playback { get; set; } = PlaybackMode.Timed;

    /// <summary>
    /// True while facade calls are recorded instead of applied: while holding, or while the queue is not empty (queue
    /// while behind keeps the order across a group close). False while a beat applies, so a facade call made by a beat's
    /// own apply runs at once.
    /// </summary>
    public static bool Queueing => (_holding || PendingBeats > 0) && !_applying;

    /// <summary>
    /// True while world input, the I/K/C toggles and End Turn must wait for the playback (GameManager.CanProcessWorldInput,
    /// InputService.HandleKeyboardInput, GameManager.OnEndTurnButtonPressed): while holding or while beats wait.
    /// </summary>
    public static bool BlocksInput
    {
        get
        {
            bool blocks = _holding || PendingBeats > 0;
            if (blocks)
                EnsurePump();
            return blocks;
        }
    }

    /// <summary>
    /// True while an interactive beat waits in the queue: a confirmation dialog (S3) or the defeat panel (S4) the playback
    /// has not reached yet (GameManager.Harness_PendingPrompt joins it, step 10, G5). A dropped one (its Show cleared by
    /// CombatUI.HideConfirmationDialog) does not count: that prompt was already closed.
    /// </summary>
    public static bool HasPendingInteractive
    {
        get
        {
            for (int i = _head; i < _queue.Count; i++)
                if (_queue[i] != null && _queue[i].Kind == BeatKind.Interactive && _queue[i].Show != null)
                    return true;
            return false;
        }
    }

    /// <summary>The beats waiting to be applied.</summary>
    public static int PendingBeats => _pending;

    /// <summary>The actions waiting for the queue to drain (<see cref="WhenDrained"/>).</summary>
    public static int PendingDrainActions => _whenDrained.Count;

    /// <summary>The group being recorded, or null.</summary>
    public static GroupRecord CurrentGroup => _holding ? _group : null;

    /// <summary>The groups recorded since the last <see cref="ResetGroupLog"/> (the scenario harness's per-job record).</summary>
    public static IReadOnlyList<GroupRecord> GroupLog => _groups;

    /// <summary>
    /// The CatchUp fallbacks taken since the last <see cref="ResetGroupLog"/> (<see cref="CatchUpBeforePrompt"/>: a
    /// CatchUp-class prompt, menu or picker shown while the presentation held or was behind), as the method names. Read by
    /// the scenario harness (a job note, never a trace event).
    /// </summary>
    public static IReadOnlyList<string> CatchUpFallbacks => _fallbacks;

    /// <summary>Clears the group log and the fallback list (the scenario harness, at each job's start). The queue and the hold are untouched.</summary>
    public static void ResetGroupLog()
    {
        _groups.Clear();
        _fallbacks.Clear();
    }

    /// <summary>
    /// Records a beat in rules order. Called by the facade while <see cref="Queueing"/> (and by the tests, which empty the
    /// queue again).
    /// </summary>
    internal static void Enqueue(PresentationBeat beat)
    {
        if (beat == null)
            return;
        beat.Seq = ++_seq;
        beat.Turn = CurrentTurnKey();
        if (_queue.Count == 0)
        {
            _head = 0;
            _queueBaseSeq = beat.Seq;
        }
        _queue.Add(beat);
        _pending++;
        if (_group != null && _group.IsOpen)
        {
            _group.Beats++;
            _group.PlaybackSeconds += DurationOf(beat, PlaybackMode.Timed);
        }
        if (Application.isPlaying)
            _ = Instance; // the playback loop lives on the singleton
    }

    /// <summary>
    /// The turn a beat is recorded in (its overlap lane, step 11; docs/designs/grouped_ai_turns.md "Recording", Tags): a
    /// new key whenever TurnService's current creature or the round changes (a round wrap's beats get a key of their own,
    /// with the previous creature and the new round). A turn whose creature left initiative mid-turn keeps its key.
    /// </summary>
    private static int CurrentTurnKey()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
            return _turnKey;
        CharacterController c = gm.CurrentCharacter;
        int round = gm.CurrentRound;
        if (c != null && (!ReferenceEquals(c, _turnCreature) || round != _turnRound))
        {
            _turnCreature = c;
            _turnRound = round;
            _turnKey++;
        }
        return _turnKey;
    }

    // ── The group ───────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens a group (TurnControl.AiTurnStarting, when grouping is on and no group is open): the presentation holds from
    /// here. Every live token view is held with a keyframe of its model now (the turn-start work before the mark ran live,
    /// so the group starts from it). Returns false when a group was already open.
    /// </summary>
    internal static bool OpenGroup()
    {
        if (_holding)
            return false;
        _holding = true;
        _group = new GroupRecord { Id = ++_groupIds, OpenSeq = _seq + 1 };
        if (_groups.Count < MaxGroupLog)
            _groups.Add(_group);
        _windowCap.Reset();
        if (Application.isPlaying)
            _ = Instance;
        try
        {
            TokenView.HoldAll();
            RecordKeyframes();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Presentation] group open: " + e.GetType().Name + ": " + e.Message);
        }
        return true;
    }

    /// <summary>
    /// Notes an AI-decided turn of the open group as a lane ("round:actor"; TurnControl.AiTurnStarting) and applies the
    /// window cap (S5, design choice C5, step 11): when <paramref name="actor"/> already has a turn in the current overlap
    /// window, a WindowBreak beat ends the window and this turn opens the next one (PlaybackScheduler.WindowCap). The rules
    /// are never paused; the break is a presentation beat only.
    /// </summary>
    internal static void NoteLane(CharacterController actor, int round)
    {
        if (!_holding || _group == null)
            return;
        string name = actor != null && actor.Stats != null ? actor.Stats.CharacterName : (actor != null ? actor.name : "?");
        _group.Lanes.Add(round + ":" + name);
        if (_windowCap.Note(actor))
        {
            Enqueue(new PresentationBeat { Kind = BeatKind.WindowBreak, Creature = actor });
            _group.WindowBreaks++;
        }
    }

    // ── Stops (step 10) ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A window stop (grouped AI turns "Stopping points"; design choice C1): a player-run creature reacts automatically
    /// inside the open group. S2's two seams call it, each gated by <c>CombatPresentation.Holding &amp;&amp;
    /// !AttackDeciders.For(creature).IsSynchronous</c> (the decider seam, no new IsControllable read; a presentation-only
    /// consumer, docs/systems/PC_NPC_PARITY.md): ThreatSystem.ExecuteAoO (<see cref="StopKind.AoO"/>, after the Spring
    /// Attack suppression and before the attack sequence) and GameManager.RunAttackedReaction
    /// (<see cref="StopKind.AutomaticReaction"/>, just before ResolveAcidSpray). <see cref="StopKind.PlayerReaction"/> is
    /// the reserved S7 API for readied actions and readied counterspells (CMB-029, SPL-047), with no caller yet.
    ///
    /// The mark is a WindowStop beat in rules order and a <see cref="StopMark"/> on the group record. The rules never wait:
    /// the reaction resolves at once, as with grouping off. The lane holding the mark plays after every earlier beat and
    /// the next lane opens a new window (the overlap scheduler, step 11, R7; the window cap restarts too). Nothing is
    /// traced (the presentation trace of a grouping-off run has no mark either), and outside a group this does nothing.
    /// </summary>
    public static void MarkStop(StopKind kind, CharacterController actor)
    {
        if (!_holding || _group == null)
            return;
        var beat = new PresentationBeat { Kind = BeatKind.WindowStop, Creature = actor, Index = (int)kind };
        Enqueue(beat);
        _windowCap.Reset(); // the marked turn is a window of its own; the next turn opens a fresh window (R7)
        string name = actor != null && actor.Stats != null ? actor.Stats.CharacterName : (actor != null ? actor.name : "?");
        _group.Stops.Add(new StopMark
        {
            Kind = kind, Actor = name, Seq = beat.Seq,
            Lane = _group.Lanes.Count > 0 ? _group.Lanes[_group.Lanes.Count - 1] : null
        });
    }

    /// <summary>
    /// The S3 fallback (grouped AI turns "Stopping points", gap G5): a CatchUp-class CombatUI prompt, menu or picker
    /// (<see cref="UiCallClass.CatchUp"/>) shown while the presentation holds or is behind. Such a call is not expected (an
    /// AI-run actor's own prompts go to the AI decider, and a defender prompt is a confirmation dialog or waits for the
    /// drain, as GameManager.PromptAttackedReaction does), so it warns (<c>[Presentation]</c>), closes an open group as a
    /// prompt (TurnControl.PromptOpened) and applies every queued beat at once (<see cref="CatchUpNow"/>) before the caller
    /// shows: a new prompt never opens before the state it refers to is on screen. Each CatchUp-class method calls it
    /// first (Tests.UI.CombatPresentationTests pins it); it does nothing in Passthrough.
    /// </summary>
    public static void CatchUpBeforePrompt(string method)
    {
        if (!Queueing)
            return;
        Debug.LogWarning("[Presentation] " + (method ?? "?") + " opened while the presentation was "
                         + (_holding ? "holding a group" : "behind (" + PendingBeats + " beats)") + "; catching up first (S3 fallback).");
        if (_fallbacks.Count < MaxGroupLog)
            _fallbacks.Add(method ?? "?");
        if (_holding)
            TurnControl.PromptOpened();
        CatchUpNow();
    }

    /// <summary>
    /// Closes the open group (the control beat, a prompt, combat end, a reset): the token pictures are recorded one last
    /// time and the hold ends. Its beats still play (queue while behind keeps later calls in order behind them); when the
    /// queue is already empty the drain runs now.
    /// </summary>
    internal static void CloseGroup(GroupStop stop)
    {
        if (!_holding)
            return;
        try { RecordKeyframes(); }
        catch (Exception e) { Debug.LogWarning("[Presentation] group close: " + e.GetType().Name + ": " + e.Message); }
        _holding = false;
        if (_group != null)
        {
            _group.Stop = stop;
            _group.CloseSeq = _seq;
            _group.PendingAtClose = PendingBeats;
        }
        _group = null;
        if (PendingBeats == 0)
            FinishDrain();
    }

    // ── Recording ───────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A pacing wait while holding (CombatPacing.Wait): a Pace beat of <paramref name="seconds"/> under
    /// <paramref name="kind"/>, which the playback plays later; the caller yields CombatPacing.HoldingFrame (one frame).
    /// </summary>
    internal static void HoldingWait(float seconds, PaceKind kind)
    {
        Enqueue(new PresentationBeat { Kind = BeatKind.Pace, Seconds = seconds, Pace = kind });
    }

    /// <summary>
    /// A rules boundary: while queueing, a Keyframe beat for every live token whose model's picture changed since its last
    /// one; while the presentation trace records, the boundary line and the trace keyframes. It never changes the
    /// caller's yield, reads the tokens only, and never throws into a rules caller.
    /// </summary>
    public static void Boundary(PresentationBoundary kind)
    {
        if (Queueing)
        {
            try { RecordKeyframes(); }
            catch (Exception e) { Debug.LogWarning("[Presentation] keyframes: " + e.GetType().Name + ": " + e.Message); }
        }
        if (!PresentationTrace.Recording)
            return;
        // Trace only, inside rules coroutines: a throw here (a keyframe of a half-reset creature, say) must never reach
        // the caller, or the rules outcome would depend on whether the harness records. The trace faults and stops.
        try
        {
            PresentationTrace.BoundaryReached(kind);
            IReadOnlyList<TokenView> views = TokenView.Live;
            for (int i = 0; i < views.Count && PresentationTrace.Recording; i++)
            {
                TokenView view = views[i];
                if (view == null || view.IsReleased || view.IsDespawning || view.Model == null)
                    continue;
                TokenKeyframe kf = KeyframeSourceForTests != null ? KeyframeSourceForTests(view) : TokenKeyframe.Capture(view);
                PresentationTrace.Keyframe(view, kf);
            }
        }
        catch (Exception e)
        {
            PresentationTrace.Fault(e, "boundary " + kind);
        }
    }

    /// <summary>The Keyframe beats of a boundary: each live view whose model's picture differs from its last one.</summary>
    private static void RecordKeyframes()
    {
        IReadOnlyList<TokenView> views = TokenView.Live;
        for (int i = 0; i < views.Count; i++)
        {
            TokenView view = views[i];
            if (view == null || view.IsReleased || view.IsDespawning || view.Model == null)
                continue;
            if (!view.IsHeld)
                view.Hold();
            if (view.IsHiddenUntilSpawn)
                view.SetHidden(true); // a renderer built since (the summon aura) stays hidden too
            TokenPicture pic = TokenPicture.Capture(view);
            if (pic == null || pic.SameAs(view.LastRecorded))
                continue;
            EnqueueKeyframe(view, pic);
        }
    }

    /// <summary>
    /// Records a token's Keyframe beat, tagged for the overlap playback (step 11): the cells its token stands on (R4),
    /// and whether it only settles the token where its last Step beat ended (a picture that differs from the last one
    /// only in position, at that step's end): such a keyframe plays in its turn's lane with the steps; every other one is
    /// a result beat.
    /// </summary>
    private static void EnqueueKeyframe(TokenView view, TokenPicture pic)
    {
        TokenPicture prev = view.LastRecorded;
        bool settle = prev != null && view.LastStepEnd.HasValue && pic.Position == view.LastStepEnd.Value && pic.DiffersOnlyInPosition(prev);
        view.LastRecorded = pic;
        int[] cells = CellsOf(view, pic);
        Enqueue(new PresentationBeat
        {
            Kind = BeatKind.Keyframe, View = view, Picture = pic, OnLane = settle, Cells = cells, IntoCells = settle ? cells : null
        });
    }

    /// <summary>A cell as one int for the scheduler.</summary>
    internal static int CellKey(int x, int y) => unchecked(x * 65536 + y);

    /// <summary>The footprint of a creature of <paramref name="size"/> squares based at <paramref name="basePos"/>, as cell keys.</summary>
    internal static int[] Footprint(Vector2Int basePos, int size)
    {
        int n = Mathf.Clamp(size, 1, 8);
        var cells = new int[n * n];
        int k = 0;
        for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
                cells[k++] = CellKey(basePos.x + x, basePos.y + y);
        return cells;
    }

    /// <summary>
    /// The cells a keyframe's token stands on: its creature's grid footprint and the cell under its drawn position (a
    /// mid-step mover is between the two); a token without a creature (a Flaming Sphere) only the latter.
    /// </summary>
    private static int[] CellsOf(TokenView view, TokenPicture pic)
    {
        Vector2Int under = SquareGridUtils.WorldToGrid(pic.Position);
        CharacterController c = view.Model != null ? view.Model.GetComponent<CharacterController>() : null;
        if (c == null)
            return new[] { CellKey(under.x, under.y) };
        int[] foot = Footprint(c.GridPosition, c.GetVisualSquaresOccupied());
        int underKey = CellKey(under.x, under.y);
        foreach (int k in foot)
            if (k == underKey)
                return foot;
        var all = new int[foot.Length + 1];
        Array.Copy(foot, all, foot.Length);
        all[foot.Length] = underKey;
        return all;
    }

    /// <summary>A view's last picture before its model goes (TokenView.PlayDespawnAndRelease while queueing).</summary>
    internal static void RecordFinalKeyframe(TokenView view)
    {
        if (view == null || view.Model == null)
            return;
        try
        {
            TokenPicture pic = TokenPicture.Capture(view);
            if (pic != null && !pic.SameAs(view.LastRecorded))
                EnqueueKeyframe(view, pic);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Presentation] final keyframe: " + e.GetType().Name + ": " + e.Message);
        }
    }

    /// <summary>A view created while queueing (TokenView.Create): it stays hidden until this beat.</summary>
    internal static void RecordSpawned(TokenView view)
    {
        if (view != null)
            Enqueue(new PresentationBeat { Kind = BeatKind.Spawned, View = view });
    }

    /// <summary>A view whose model went while queueing (TokenView.Release, PlayDespawnAndRelease): it goes at this beat.</summary>
    internal static void RecordRemoved(TokenView view, bool despawn)
    {
        if (view != null)
            Enqueue(new PresentationBeat { Kind = BeatKind.Removed, View = view, Flag = despawn });
    }

    /// <summary>
    /// A MoveAlongPath step, called right after the mover's GridPosition becomes <paramref name="to"/> (before its
    /// tween): a boundary (the keyframes show the mover where the step starts), the trace's step line, and while
    /// queueing a Step beat (the view tweens from <paramref name="startPos"/> to <paramref name="endPos"/> over
    /// <paramref name="secondsPerStep"/> divided by the AI speed). The rules half of the step is untouched.
    /// </summary>
    public static void RecordStep(CharacterController mover, Vector2Int from, Vector2Int to, Vector3 startPos, Vector3 endPos, float secondsPerStep)
    {
        Boundary(PresentationBoundary.Step);
        if (Queueing && mover != null && mover.View != null)
        {
            // The overlap playback (step 11): a step plays in its turn's lane; it waits for earlier beats on the cells it
            // enters (R4), and other steps into the cells it leaves or enters wait for it.
            int size = mover.GetVisualSquaresOccupied();
            int[] into = Footprint(to, size);
            int[] fromCells = Footprint(from, size);
            var cells = new int[fromCells.Length + into.Length];
            Array.Copy(fromCells, cells, fromCells.Length);
            Array.Copy(into, 0, cells, fromCells.Length, into.Length);
            mover.View.LastStepEnd = endPos;
            Enqueue(new PresentationBeat
            {
                Kind = BeatKind.Step, View = mover.View, From = startPos, To = endPos, Seconds = secondsPerStep, Pace = CombatPacing.Kind,
                OnLane = true, Cells = cells, IntoCells = into
            });
        }
        if (!PresentationTrace.Recording)
            return;
        try
        {
            PresentationTrace.Step(mover != null ? mover.View : null, mover, from, to);
        }
        catch (Exception e)
        {
            PresentationTrace.Fault(e, "step");
        }
    }

    // ── One-shot visuals, area indicators, drains ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Shows a one-shot visual (a closure over values taken at call time, never over live rules state): at once in
    /// Passthrough, as a Deferred beat while queueing. <paramref name="label"/> names it in the trace.
    /// </summary>
    public static void Defer(Action show, string label)
    {
        if (show == null)
            return;
        if (Queueing)
        {
            Enqueue(new PresentationBeat { Kind = BeatKind.Deferred, Show = show, Text = label });
            return;
        }
        PresentationTrace.Applied("defer", label);
        show();
    }

    /// <summary>
    /// A persistent area's indicator was created (PersistentAreaEffect.CreateVisualIndicator). In Passthrough it shows at
    /// once, as before; while queueing its renderers are not drawn (forceRenderingOff) until its AreaShown beat.
    /// </summary>
    public static void AreaShown(PersistentAreaEffect effect, GameObject indicator)
    {
        if (indicator == null)
            return;
        if (PresentationTrace.Recording)
            PresentationTrace.Applied("area", "+" + AreaLabel(effect));
        if (!Queueing)
            return;
        var hidden = new List<Renderer>();
        foreach (Renderer r in indicator.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r.forceRenderingOff)
                continue;
            r.forceRenderingOff = true;
            hidden.Add(r);
        }
        Enqueue(new PresentationBeat { Kind = BeatKind.AreaShown, Indicator = indicator, Renderers = hidden.ToArray() });
    }

    /// <summary>
    /// A persistent area's indicator goes (PersistentAreaEffect.ReleaseIndicator: its expiry, a re-creation or a direct
    /// destroy site, in rules order; OnDestroy is the fallback). Returns true when the presentation took the indicator
    /// over (while queueing: the caller detaches it and it is destroyed at its AreaHidden beat); false in Passthrough,
    /// where the caller destroys it at once as before.
    /// </summary>
    public static bool AreaHidden(PersistentAreaEffect effect, GameObject indicator)
    {
        if (indicator == null)
            return false;
        if (PresentationTrace.Recording)
            PresentationTrace.Applied("area", "-" + AreaLabel(effect));
        if (!Queueing)
            return false;
        Enqueue(new PresentationBeat { Kind = BeatKind.AreaHidden, Indicator = indicator });
        return true;
    }

    private static string AreaLabel(PersistentAreaEffect effect)
    {
        if (effect == null)
            return "?";
        Vector2Int cell = SquareGridUtils.WorldToGrid(effect.CenterPosition);
        return effect.EffectName + "@" + cell.x + "," + cell.y;
    }

    /// <summary>
    /// Runs <paramref name="action"/> at once when nothing is held or queued, else when the playback drains with no group
    /// open (S4: the loot window opens after the killing blow is shown).
    /// </summary>
    public static void WhenDrained(Action action)
    {
        if (action == null)
            return;
        if (!_holding && PendingBeats == 0)
        {
            action();
            return;
        }
        _whenDrained.Add(action);
    }

    /// <summary>
    /// Applies every queued beat at once, in rules order (views snap to their latest keyframes, log lines are appended,
    /// panels set); with no group open the held views then copy their models again and the pending
    /// <see cref="WhenDrained"/> actions run. Used before a prompt shows (the step 9 stand-in for step 10's barrier), by
    /// the scenario harness before it reads the world, and by the tests.
    /// </summary>
    public static void CatchUpNow()
    {
        // A beat's apply may make a facade call of its own; it applies at once (_applying), never behind the queue.
        ResetScheduler(); // every beat applies now, in rules order; later beats start a fresh schedule
        while (PendingBeats > 0)
        {
            PresentationBeat b = TakeNext();
            if (b == null)
                break;
            ApplyGuarded(b);
        }
        Compact(true);
        if (!_holding)
            FinishDrain();
    }

    /// <summary>
    /// The reset (ResetCombatStateForNextEncounter, Harness_HaltCombat, Harness_ResetWorld): an open group closes (Reset),
    /// the pending drain actions are dropped, the queued cells, ground items and area indicators get their latest state,
    /// a view whose model went is destroyed, every other view copies its model again, and the queue empties. Nothing is
    /// shown: the log lines and panels the queue held are dropped (the reset rebuilds the UI).
    /// </summary>
    public static void Clear()
    {
        if (_holding)
        {
            _holding = false;
            if (_group != null)
            {
                _group.Stop = GroupStop.Reset;
                _group.CloseSeq = _seq;
            }
            _group = null;
        }
        _whenDrained.Clear();
        var cells = new List<SquareCell>();
        var beats = new List<PresentationBeat>();
        for (int i = _head; i < _queue.Count; i++)
            if (_queue[i] != null)
                beats.Add(_queue[i]);
        _queue.Clear();
        _head = 0;
        _pending = 0;
        _queueBaseSeq = _seq + 1;
        ResetScheduler();
        _windowCap.Reset();
        _applying = true;
        try
        {
            foreach (PresentationBeat b in beats)
            {
                try
                {
                    switch (b.Kind)
                    {
                        case BeatKind.CellHighlight:
                        case BeatKind.CellOverlay:
                        case BeatKind.GroundItem:
                            if (b.Cell != null && !cells.Contains(b.Cell))
                                cells.Add(b.Cell);
                            break;
                        case BeatKind.AreaShown:
                        case BeatKind.AreaHidden:
                        case BeatKind.Removed:
                            ApplyBeat(b);
                            break;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Presentation] clear " + b.Kind + ": " + e.GetType().Name + ": " + e.Message);
                }
            }
            foreach (SquareCell cell in cells)
            {
                if (cell == null)
                    continue;
                try { cell.ApplyRequestedState(); }
                catch (Exception e) { Debug.LogWarning("[Presentation] clear cell: " + e.GetType().Name + ": " + e.Message); }
            }
        }
        finally
        {
            _applying = false;
        }
        try { TokenView.ReleaseAllHolds(); }
        catch (Exception e) { Debug.LogWarning("[Presentation] clear views: " + e.GetType().Name + ": " + e.Message); }
    }

    /// <summary>The queue drained with no group open: the held views copy their models again, then the drain actions run.</summary>
    private static void FinishDrain()
    {
        if (_holding || PendingBeats > 0)
            return;
        try { TokenView.ReleaseAllHolds(); }
        catch (Exception e) { Debug.LogWarning("[Presentation] drain views: " + e.GetType().Name + ": " + e.Message); }
        while (_whenDrained.Count > 0 && !_holding && PendingBeats == 0)
        {
            Action a = _whenDrained[0];
            _whenDrained.RemoveAt(0);
            try { a(); }
            catch (Exception e) { Debug.LogWarning("[Presentation] drain action: " + e.GetType().Name + ": " + e.Message); }
        }
    }

    // ── Playback (overlapped lanes, step 11) ────────────────────────────────────────────────────────────────────────

    private void LateUpdate()
    {
        Pump(Time.deltaTime);
    }

    /// <summary>
    /// One frame of playback. Timed (play, watch mode): the beats go to the overlap scheduler (PlaybackScheduler, R1-R8:
    /// token steps in parallel lanes, one per turn, at most 3, staggered; every other beat on the result track, strictly
    /// in rules order; windows end at a stop, at a creature's second turn and at a barrier); each beat applies when the
    /// scheduler finishes it, and a Step in flight tweens its held view. Instant (the harness's fast and fine modes):
    /// every available beat applies now, in rules order. The playback never passes the recording head: it plays what is
    /// queued and waits for more while the group records.
    /// </summary>
    internal static void Pump(float deltaTime)
    {
        if (PendingBeats == 0)
        {
            if (_scheduler.Pending > 0)
                ResetScheduler();
            if (!_holding && (_whenDrained.Count > 0 || TokenView.MaybeAnyHeld))
                FinishDrain();
            return;
        }
        if (Playback == PlaybackMode.Instant)
        {
            ResetScheduler();
            while (PendingBeats > 0)
            {
                PresentationBeat b = TakeNext();
                if (b == null)
                    break;
                ApplyGuarded(b);
            }
        }
        else
        {
            // Fault isolation: the overlap scheduler is presentation only, so a fault in it must never leave the queue
            // (and BlocksInput) stuck; it catches the playback up instead, as a listed fallback.
            try
            {
                PumpTimed(Mathf.Max(0f, deltaTime));
            }
            catch (Exception ex)
            {
                SchedulerFallback("threw " + ex.GetType().Name + ": " + ex.Message);
            }
        }
        Compact(false);
        if (PendingBeats == 0 && !_holding)
        {
            ResetScheduler();
            FinishDrain();
        }
    }

    private static void PumpTimed(float dt)
    {
        TimedFrameProbeForTests?.Invoke(_scheduler);
        FeedScheduler();
        _scheduler.RecordingOpen = _holding;
        _finishedScratch.Clear();
        int generation = _schedulerGeneration;
        _scheduler.Advance(dt, CombatPacing.Speed, _finishedScratch);
        foreach (PlaybackScheduler.Item item in _finishedScratch)
        {
            if (generation != _schedulerGeneration)
                break; // a beat's apply reset the presentation (a reset or a catch-up): the rest is gone or applied
            var b = item.Payload as PresentationBeat;
            if (b != null && Take(b))
                ApplyGuarded(b);
        }
        int finishedCount = _finishedScratch.Count;
        _finishedScratch.Clear();
        if (generation != _schedulerGeneration)
            return;
        _inFlightScratch.Clear();
        _scheduler.CopyInFlight(_inFlightScratch);
        foreach (PlaybackScheduler.Item item in _inFlightScratch)
        {
            var b = item.Payload as PresentationBeat;
            if (b == null || b.Kind != BeatKind.Step || b.View == null)
                continue;
            float t = Mathf.SmoothStep(0f, 1f, item.Progress);
            b.View.ShowStep(Vector3.Lerp(b.From, b.To, t));
        }
        int inFlight = _inFlightScratch.Count;
        _inFlightScratch.Clear();
        WatchForStall(dt, finishedCount > 0 || inFlight > 0);
    }

    /// <summary>
    /// The stall watchdog (<see cref="StallSeconds"/>): while a group records, the playback may wait at the recording head
    /// (R1), so only a closed group's queue counts; a frame that finished a beat or has one in flight is progress.
    /// </summary>
    private static void WatchForStall(float dt, bool progressed)
    {
        if (progressed || _holding || PendingBeats == 0)
        {
            _stallSeconds = 0f;
            _stallFrames = 0;
            return;
        }
        if (dt <= 0f)
            return;
        _stallSeconds += dt;
        _stallFrames++;
        if (_stallSeconds >= StallSeconds && _stallFrames >= StallMinFrames)
            SchedulerFallback("made no progress for " + _stallSeconds.ToString("0.0") + " s");
    }

    /// <summary>
    /// The scheduler fault or stall fallback: warns (<c>[Presentation] scheduler ...</c>), lists
    /// <see cref="SchedulerFallbackName"/> in <see cref="CatchUpFallbacks"/> and applies every queued beat at once
    /// (<see cref="CatchUpNow"/>), so the queue drains and input is never blocked for good.
    /// </summary>
    private static void SchedulerFallback(string why)
    {
        Debug.LogWarning("[Presentation] scheduler " + why + " with " + PendingBeats + " beats queued; catching up (fallback).");
        if (_fallbacks.Count < MaxGroupLog)
            _fallbacks.Add(SchedulerFallbackName);
        _stallSeconds = 0f;
        _stallFrames = 0;
        try
        {
            CatchUpNow();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Presentation] the scheduler fallback's catch-up threw " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>Feeds the scheduler every queued beat it has not seen, in rules order.</summary>
    private static void FeedScheduler()
    {
        int from = Math.Max(_head, _fedSeq - _queueBaseSeq + 1);
        for (int i = Math.Max(0, from); i < _queue.Count; i++)
        {
            PresentationBeat b = _queue[i];
            if (b == null)
                continue;
            _scheduler.Add(ToItem(b));
            _fedSeq = b.Seq;
        }
    }

    /// <summary>A beat as a scheduler item: its turn, track, token, cells, timing and window role.</summary>
    internal static PlaybackScheduler.Item ToItem(PresentationBeat b)
    {
        var it = new PlaybackScheduler.Item { Seq = b.Seq, Turn = b.Turn, Payload = b };
        switch (b.Kind)
        {
            case BeatKind.Pace:
                it.IsPace = true;
                it.Seconds = b.Seconds;
                it.ScaleWithSpeed = b.Pace == PaceKind.Ai;
                break;
            case BeatKind.Step:
                it.OnLane = true;
                it.IsStep = true;
                it.Seconds = b.Seconds;
                it.ScaleWithSpeed = b.Pace == PaceKind.Ai;
                it.Token = b.View;
                it.Cells = b.Cells;
                it.IntoCells = b.IntoCells;
                break;
            case BeatKind.Keyframe:
                it.OnLane = b.OnLane;
                it.Token = b.View;
                it.Cells = b.Cells;
                it.IntoCells = b.OnLane ? b.IntoCells : null;
                break;
            case BeatKind.Spawned:
            case BeatKind.Removed:
                it.Token = b.View;
                break;
            case BeatKind.Control:
            case BeatKind.Interactive:
                it.Flags = PlaybackScheduler.ItemFlags.Barrier;
                break;
            case BeatKind.WindowStop:
                it.Flags = PlaybackScheduler.ItemFlags.StopMark;
                break;
            case BeatKind.WindowBreak:
                it.Flags = PlaybackScheduler.ItemFlags.WindowBreak;
                break;
        }
        return it;
    }

    private static void ResetScheduler()
    {
        _scheduler.Reset();
        _stallSeconds = 0f;
        _stallFrames = 0;
        _fedSeq = 0;
        _schedulerGeneration++;
    }

    /// <summary>
    /// The beat's sequential Timed length (a Pace beat's seconds and a Step's step seconds over the AI speed), summed into
    /// GroupRecord.PlaybackSeconds; the overlap playback times beats with PlaybackScheduler.BaseDuration (and R2's
    /// stagger cap).
    /// </summary>
    internal static float DurationOf(PresentationBeat b, PlaybackMode mode)
    {
        if (b == null || mode == PlaybackMode.Instant)
            return 0f;
        switch (b.Kind)
        {
            case BeatKind.Pace: return Mathf.Max(0f, CombatPacing.Scaled(b.Seconds, b.Pace, CombatPacing.Speed));
            case BeatKind.Step: return Mathf.Max(0.01f, CombatPacing.Scaled(b.Seconds, b.Pace, CombatPacing.Speed));
            default: return 0f;
        }
    }

    /// <summary>Takes the first beat not applied yet (rules order), or null.</summary>
    private static PresentationBeat TakeNext()
    {
        SkipApplied();
        if (_head >= _queue.Count)
            return null;
        PresentationBeat b = _queue[_head];
        _queue[_head] = null;
        _head++;
        _pending--;
        return b;
    }

    /// <summary>Takes <paramref name="b"/> out of the queue wherever it is (the overlap playback); false when it is not queued.</summary>
    private static bool Take(PresentationBeat b)
    {
        int i = b.Seq - _queueBaseSeq;
        if (i < 0 || i >= _queue.Count || !ReferenceEquals(_queue[i], b))
            return false;
        _queue[i] = null;
        _pending--;
        SkipApplied();
        return true;
    }

    private static void SkipApplied()
    {
        while (_head < _queue.Count && _queue[_head] == null)
            _head++;
    }

    private static void Compact(bool force)
    {
        SkipApplied();
        if (_head == 0)
            return;
        if (_head >= _queue.Count)
        {
            _queueBaseSeq += _queue.Count;
            _queue.Clear();
            _head = 0;
            return;
        }
        if (force || _head > 1024)
        {
            _queue.RemoveRange(0, _head);
            _queueBaseSeq += _head;
            _head = 0;
        }
    }

    private static void ApplyGuarded(PresentationBeat b)
    {
        bool was = _applying;
        _applying = true;
        try
        {
            ApplyBeat(b);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Presentation] beat " + (b != null ? b.Kind.ToString() : "null") + " #" + (b != null ? b.Seq : 0) + ": "
                             + e.GetType().Name + ": " + e.Message);
        }
        finally
        {
            _applying = was;
        }
    }

    /// <summary>Applies one beat: the same apply the facade runs at once in Passthrough.</summary>
    internal static void ApplyBeat(PresentationBeat b)
    {
        if (b == null)
            return;
        switch (b.Kind)
        {
            case BeatKind.Log: if (b.Ui != null) b.Ui.ApplyCombatLog(b.Text); break;
            case BeatKind.TurnSeparator: if (b.Ui != null) b.Ui.ApplyTurnSeparator(b.Index); break;
            case BeatKind.TurnIndicator: if (b.Ui != null) b.Ui.ApplyTurnIndicator(b.Text); break;
            case BeatKind.Initiative: if (b.Ui != null) b.Ui.ApplyInitiativeDisplay(b.Text); break;
            case BeatKind.ActivePC: if (b.Ui != null) b.Ui.ApplyActivePC(b.Index, b.Creature); break;
            case BeatKind.ActiveNPC: if (b.Ui != null) b.Ui.ApplyActiveNPC(b.Index, b.Creature); break;
            case BeatKind.ActionButtonsVisible: if (b.Ui != null) b.Ui.ApplyActionButtonsVisible(b.Flag); break;
            case BeatKind.Panel: CharacterInfoPanel.Apply(b.Panel); break;
            case BeatKind.CellHighlight: if (b.Cell != null) b.Cell.ApplyHighlight(b.Highlight); break;
            case BeatKind.CellOverlay:
                if (b.Cell != null)
                {
                    if (b.Flag) b.Cell.ApplyOverlay(b.Color);
                    else b.Cell.ApplyOverlayClear();
                }
                break;
            case BeatKind.GroundItem: if (b.Cell != null) b.Cell.ApplyGroundItemVisible(b.Flag); break;
            case BeatKind.Pace: break;
            case BeatKind.WindowStop: break; // the scheduler ends the overlap window here (R7); it shows nothing
            case BeatKind.WindowBreak: break; // S5: the scheduler opens a new window with this turn; it shows nothing
            case BeatKind.Step: if (b.View != null) b.View.ShowStep(b.To); break;
            case BeatKind.Keyframe: if (b.View != null) b.View.ApplyPicture(b.Picture); break;
            case BeatKind.Spawned: if (b.View != null) b.View.ApplySpawned(); break;
            case BeatKind.Removed: if (b.View != null) b.View.ApplyRemoved(b.Flag); break;
            case BeatKind.AreaShown:
                if (b.Renderers != null)
                    foreach (Renderer r in b.Renderers)
                        if (r != null)
                            r.forceRenderingOff = false;
                break;
            case BeatKind.AreaHidden:
                if (b.Indicator != null)
                    Destroy(b.Indicator);
                break;
            case BeatKind.Control:
            case BeatKind.Interactive:
            case BeatKind.Deferred:
            case BeatKind.QueuedClosure:
                // Only a Defer beat writes the "defer" line (Defer writes it in Passthrough too); a QueuedClosure apply
                // writes its own line, so both paths trace alike.
                if (b.Kind == BeatKind.Deferred)
                    PresentationTrace.Applied("defer", b.Text);
                b.Show?.Invoke();
                break;
        }
    }

    // ── The CombatUI method classes ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The class of every public CombatUI method, by name (overloads share their class). A new public CombatUI method must
    /// be classed here: Tests.UI.CombatPresentationTests.TestCombatUiMethodsClassified reflects over CombatUI and fails
    /// on any unclassed one (docs/DEVELOPMENT_RECIPES.md, "Add a CombatUI method").
    /// </summary>
    public static readonly IReadOnlyDictionary<string, UiCallClass> CombatUiClasses = BuildCombatUiClasses();

    /// <summary>The class of <paramref name="combatUiMethod"/>, or null when it is not classed.</summary>
    public static UiCallClass? ClassOf(string combatUiMethod)
    {
        if (combatUiMethod != null && CombatUiClasses.TryGetValue(combatUiMethod, out UiCallClass c))
            return c;
        return null;
    }

    private static Dictionary<string, UiCallClass> BuildCombatUiClasses()
    {
        var d = new Dictionary<string, UiCallClass>(StringComparer.Ordinal);
        void Add(UiCallClass c, params string[] names)
        {
            foreach (string n in names)
                d[n] = c;
        }

        // Value beats (the log hook ScenarioHooks.CombatLog stays ShowCombatLog's first statement, at rules time).
        Add(UiCallClass.Queued,
            "ShowCombatLog", "AddTurnSeparator", "SetTurnIndicator", "UpdateInitiativeDisplay",
            "SetActivePC", "SetActiveNPC", "SetActionButtonsVisible",
            "UpdateAllStats4PC", "UpdateAllStats", "UpdateAllStatsMultiNPC", "UpdateStats",
            "AddCombatMessage", "LogAttack", "LogDamage");

        Add(UiCallClass.Control, "ShowActionButtonsForCharacter");

        Add(UiCallClass.Interactive, "ShowConfirmationDialog", "ShowDefeatPanel");

        // Prompts, menus and pickers a group never reaches (attacker-side prompts go to the AI decider, by construction).
        Add(UiCallClass.CatchUp,
            "ShowSpecialAttackMenu", "ShowSpecialStyleSelectionMenu", "ShowBullRushExtraPushChoice",
            "ShowAoOConfirmationPrompt", "ShowTouchSpellPrompt", "ShowSpellSelection", "ShowSummonCreatureSelection",
            "ShowSummonContextMenu", "ShowCastDefensivelyPrompt", "ShowPickUpItemSelection", "ShowCharacterSelectionUI",
            "ShowDropEquippedItemSelection", "ShowDisarmWeaponSelection", "ShowSunderItemSelection",
            "ShowDisguiseSelfRaceSelector");

        // Always at once: queries, hides, resets (whose inner Queued calls go through the facade), the PC-turn controls
        // (the action panel is hidden during AI turns and refreshed by the control beat), icons and layout.
        Add(UiCallClass.Live,
            "BuildFlankingIndicator", "SetCurrentTarget", "GetCurrentTarget", "ClearCurrentTarget",
            "ClearCombatLog", "ClearTurnOrderDisplay", "ResetAllUI", "InitializeForCombat", "ShowPositioningInstructions",
            "ExportCombatLog", "RefreshAiSpeedButton", "RefreshGroupAiTurnsButton", "ResetFloatingWindowLayout",
            "UpdateActionButtons", "RefreshActionButtons", "OnGrappleActionsClicked", "ResetDamageModeToggleVisual",
            "UpdateDamageModeToggle", "UpdateFeatControls", "UpdatePowerAttackLabel", "UpdateRapidShotLabel",
            "SetPCIcon", "SetNPCIcon", "BeginSpontaneousConversion", "ClearSpontaneousCastState",
            "IsSpecialStyleSelectionMenuOpen", "IsDisguiseSelfRaceSelectorOpen",
            "HideSpecialAttackMenu", "HideSpecialStyleSelectionMenu", "HideBullRushExtraPushChoice",
            "HideAoOConfirmationPrompt", "HideSpellSelection", "HideTouchSpellPrompt", "HideSummonContextMenu",
            "HideConfirmationDialog", "HideDefeatPanel", "HideSummonCreatureSelection", "HidePickUpItemSelection",
            "HideDropEquippedItemSelection", "HideDisarmWeaponSelection", "HideDisguiseSelfRaceSelector");
        return d;
    }
}
