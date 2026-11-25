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
/// a mark never pauses the rules. In the sequential playback (step 9) every beat already plays after every earlier one, so
/// a mark changes nothing on screen yet; step 11's overlap scheduler ends the overlap window at it (the lane holding the
/// mark plays after every earlier beat, and the next lane opens a new window).
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
    public float PlaybackSeconds;
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
/// or Removed beat. The playback (this MonoBehaviour's LateUpdate, single lane, in rules order) shows them: a Pace beat
/// lasts its seconds divided by the AI speed and a Step beat tweens the token's held view (<see cref="Playback"/> Timed),
/// or everything applies in the frame (Instant, the harness's fast mode). World input, the I/K/C toggles and End Turn
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
    private static int _head;
    private static readonly List<Action> _whenDrained = new List<Action>();
    private static int _seq;
    private static bool _holding;
    private static bool _applying;
    private static bool _headStarted;
    private static float _headElapsed;
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
        _whenDrained.Clear();
        _seq = 0;
        _holding = false;
        _applying = false;
        _headStarted = false;
        _headElapsed = 0f;
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
                if (_queue[i].Kind == BeatKind.Interactive && _queue[i].Show != null)
                    return true;
            return false;
        }
    }

    /// <summary>The beats waiting to be applied.</summary>
    public static int PendingBeats => _queue.Count - _head;

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
        _queue.Add(beat);
        if (_group != null && _group.IsOpen)
        {
            _group.Beats++;
            _group.PlaybackSeconds += DurationOf(beat, PlaybackMode.Timed);
        }
        if (Application.isPlaying)
            _ = Instance; // the playback loop lives on the singleton
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

    /// <summary>Notes an AI-decided turn of the open group as a lane ("round:actor"; TurnControl.AiTurnStarting).</summary>
    internal static void NoteLane(CharacterController actor, int round)
    {
        if (!_holding || _group == null)
            return;
        string name = actor != null && actor.Stats != null ? actor.Stats.CharacterName : (actor != null ? actor.name : "?");
        _group.Lanes.Add(round + ":" + name);
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
    /// the reaction resolves at once, as with grouping off. The lane holding the mark plays after every earlier beat (true
    /// of the sequential playback by construction) and the next lane opens a new window (step 11's scheduler). Nothing is
    /// traced (the presentation trace of a grouping-off run has no mark either), and outside a group this does nothing.
    /// </summary>
    public static void MarkStop(StopKind kind, CharacterController actor)
    {
        if (!_holding || _group == null)
            return;
        var beat = new PresentationBeat { Kind = BeatKind.WindowStop, Creature = actor, Index = (int)kind };
        Enqueue(beat);
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
            view.LastRecorded = pic;
            Enqueue(new PresentationBeat { Kind = BeatKind.Keyframe, View = view, Picture = pic });
        }
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
            {
                view.LastRecorded = pic;
                Enqueue(new PresentationBeat { Kind = BeatKind.Keyframe, View = view, Picture = pic });
            }
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
            Enqueue(new PresentationBeat
            {
                Kind = BeatKind.Step, View = mover.View, From = startPos, To = endPos, Seconds = secondsPerStep, Pace = CombatPacing.Kind
            });
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
        while (PendingBeats > 0)
        {
            PresentationBeat b = TakeHead();
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
            beats.Add(_queue[i]);
        _queue.Clear();
        _head = 0;
        _headStarted = false;
        _headElapsed = 0f;
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

    // ── Playback (sequential, single lane) ──────────────────────────────────────────────────────────────────────────

    private void LateUpdate()
    {
        Pump(Time.deltaTime);
    }

    /// <summary>
    /// One frame of playback: beats apply in rules order; a Pace beat holds the head for its seconds divided by the AI
    /// speed and a Step beat tweens its view (Timed), or every available beat applies now (Instant). The playback never
    /// passes the recording head: it plays what is queued and waits for more while the group records.
    /// </summary>
    internal static void Pump(float deltaTime)
    {
        if (PendingBeats == 0)
        {
            if (!_holding && (_whenDrained.Count > 0 || TokenView.MaybeAnyHeld))
                FinishDrain();
            return;
        }
        float dt = Mathf.Max(0f, deltaTime);
        bool instant = Playback == PlaybackMode.Instant;
        while (PendingBeats > 0)
        {
            PresentationBeat b = _queue[_head];
            float duration = instant ? 0f : DurationOf(b, PlaybackMode.Timed);
            if (!_headStarted)
            {
                _headStarted = true;
                _headElapsed = 0f;
            }
            if (duration > 0f)
            {
                float need = duration - _headElapsed;
                if (dt < need)
                {
                    _headElapsed += dt;
                    if (b.Kind == BeatKind.Step && b.View != null)
                    {
                        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_headElapsed / duration));
                        b.View.ShowStep(Vector3.Lerp(b.From, b.To, t));
                    }
                    break;
                }
                dt -= need;
            }
            TakeHead();
            ApplyGuarded(b);
        }
        Compact(false);
        if (PendingBeats == 0 && !_holding)
            FinishDrain();
    }

    /// <summary>The time a beat holds the playback head for (Timed): a Pace beat's seconds and a Step's step seconds, divided by the AI speed.</summary>
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

    private static PresentationBeat TakeHead()
    {
        PresentationBeat b = _queue[_head];
        _queue[_head] = null;
        _head++;
        _headStarted = false;
        _headElapsed = 0f;
        return b;
    }

    private static void Compact(bool force)
    {
        if (_head == 0)
            return;
        if (_head >= _queue.Count)
        {
            _queue.Clear();
            _head = 0;
            return;
        }
        if (force || _head > 1024)
        {
            _queue.RemoveRange(0, _head);
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
            case BeatKind.WindowStop: break; // step 11's scheduler ends the window here; the sequential playback shows nothing
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
            "ExportCombatLog", "RefreshAiSpeedButton", "ResetFloatingWindowLayout",
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
