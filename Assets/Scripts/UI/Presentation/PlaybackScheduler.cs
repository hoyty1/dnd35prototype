using System;
using System.Collections.Generic;

/// <summary>
/// The overlap scheduler of grouped AI turns (step 11, gt-overlap-and-options; docs/designs/grouped_ai_turns.md
/// "Playback" R1-R8, "The group" S5, design choice C5). A pure class: no Unity call, no die, no rules state. It is fed
/// the recorded beats of a group in rules order (<see cref="Add"/>, as <see cref="Item"/>s that CombatPresentation
/// builds from its PresentationBeats) and, frame by frame (<see cref="Advance"/>), says which beats finish (to be applied,
/// in the order given) and which timed beats are in flight (a token step's tween).
///
/// Only token steps play in parallel lanes, one lane per turn (an <see cref="Item.Turn"/>); every other beat is on the
/// result track, which plays strictly in rules order:
/// - R1: a lane is admitted only once its turn is fully recorded (a beat of a later turn, a barrier, or the end of the
///   recording has arrived), so whether it holds a window stop is known before it starts.
/// - R2: the result track plays in seq order, one beat at a time; a Pace beat lasts its seconds divided by the AI speed
///   while one lane is admitted, and min(that, <see cref="StaggerFor"/>) while two or more are.
/// - R3: per token, beats play in seq order (a step starts only after the same token's previous beat has finished).
/// - R4: a step into a cell waits for every earlier beat on that cell (a fallen ally's keyframe, another token's step).
/// - R5: within a lane a run of consecutive steps is a segment; segment k+1 waits for the lane's result beats recorded
///   between segments k and k+1 (an AoO mid-move shows before the mover goes on), and a lane's result beats wait for its
///   earlier segments (an attack shows after the walk arrives).
/// - R6: a lane starts no earlier than the stagger after the previous lane of its window started.
/// - R7: a window ends at a window stop (S2: the lane holding the mark is a window of its own, started after every
///   earlier beat), at a window break (S5: the lane holding it starts a new window) and at a barrier (a control or
///   interactive beat: a window of its own; nothing after it starts before it finishes). Windows play one after another.
/// - R8: at most <see cref="MaxLanes"/> lanes play at once.
///
/// It never passes the recording head: it schedules only what was added. The S5 decision (does this turn start a second
/// turn of the same creature in the window?) is <see cref="WindowCap"/>, applied from TurnControl.AiTurnStarting.
/// </summary>
public sealed class PlaybackScheduler
{
    /// <summary>The lane stagger at 1x (R6, and R2's spacing while lanes overlap): 0.15 s, divided by the AI speed.</summary>
    public const float BaseStagger = 0.15f;

    /// <summary>The most lanes that play at once (R8).</summary>
    public const int MaxLanes = 3;

    /// <summary>The shortest a step plays (MoveAlongPath's own floor).</summary>
    public const float MinStepSeconds = 0.01f;

    private const float Epsilon = 1e-5f;
    private const int MaxEventsPerAdvance = 100000;

    /// <summary>How an item bounds the windows (R7).</summary>
    [Flags]
    public enum ItemFlags
    {
        None = 0,
        /// <summary>A control or interactive beat: a window of its own; it starts after every earlier beat, and nothing after it starts before it finishes.</summary>
        Barrier = 1,
        /// <summary>S5: the lane holding it starts a new window.</summary>
        WindowBreak = 2,
        /// <summary>S2: the lane holding it is a window of its own, started after every earlier beat.</summary>
        StopMark = 4,
    }

    /// <summary>One scheduled beat (its rules order, turn, track, token, cells and timing) and its playback state.</summary>
    public sealed class Item
    {
        /// <summary>Rules order.</summary>
        public int Seq;
        /// <summary>The turn it was recorded in (the lane key).</summary>
        public int Turn;
        /// <summary>True for a token step (or a keyframe that only settles its token at its step's end): it plays in its lane; false: the result track.</summary>
        public bool OnLane;
        /// <summary>The token it shows (compared by reference), or null.</summary>
        public object Token;
        /// <summary>The cells it touches (a keyframe's token footprint, a step's from and to footprints), or null.</summary>
        public int[] Cells;
        /// <summary>A lane item's destination cells (R4), or null.</summary>
        public int[] IntoCells;
        /// <summary>The seconds at 1x of a Pace or a Step item.</summary>
        public float Seconds;
        /// <summary>True when <see cref="Seconds"/> divides by the AI speed (recorded under the Ai pacing kind).</summary>
        public bool ScaleWithSpeed;
        /// <summary>A Pace item (R2's spacing).</summary>
        public bool IsPace;
        /// <summary>A Step item (at least <see cref="MinStepSeconds"/>).</summary>
        public bool IsStep;
        public ItemFlags Flags;
        /// <summary>The caller's beat.</summary>
        public object Payload;

        public bool Started { get; internal set; }
        public bool Finished { get; internal set; }
        /// <summary>The seconds it plays for, fixed when it starts.</summary>
        public float Duration { get; internal set; }
        public float Elapsed { get; internal set; }
        /// <summary>The scheduler time it started at.</summary>
        public float StartedAt { get; internal set; }
        /// <summary>0..1 through its duration (1 when it has none).</summary>
        public float Progress => Duration > 0f ? Math.Min(1f, Math.Max(0f, Elapsed / Duration)) : 1f;

        internal Lane Lane;
    }

    internal sealed class Lane
    {
        public int Turn;
        public readonly List<Item> Items = new List<Item>();
        public int Remaining;
        public int FirstLaneSeq = int.MaxValue;
        public bool Sealed;
        public bool Admitted;
        public bool Removed;
        /// <summary>A window of its own (a stop mark, a barrier).</summary>
        public bool Solo;
        /// <summary>Starts a new window (S5).</summary>
        public bool Break;
        public float AdmittedAt;
    }

    private readonly List<Lane> _lanes = new List<Lane>();
    private readonly List<Item> _unfinished = new List<Item>();
    private readonly List<Item> _results = new List<Item>();
    private int _resultHead;
    private Lane _tail;
    private float _lastAdmitAt = float.NegativeInfinity;
    private float _admitWakeAt = float.PositiveInfinity;

    /// <summary>The scheduler's clock (seconds of playback since the last <see cref="Reset"/>).</summary>
    public float Now { get; private set; }

    /// <summary>
    /// True while the group still records (CombatPresentation.Holding): the last lane may still grow, so it is not
    /// admitted until a later turn's beat or a barrier arrives. False: the last lane is complete (R1's "close").
    /// </summary>
    public bool RecordingOpen { get; set; }

    /// <summary>The items added and not finished.</summary>
    public int Pending => _unfinished.Count;

    /// <summary>The lanes not finished (admitted or waiting).</summary>
    public int LaneCount => _lanes.Count;

    /// <summary>The lanes admitted and not finished (they play now).</summary>
    public int AdmittedLanes
    {
        get
        {
            int n = 0;
            foreach (Lane l in _lanes)
                if (l.Admitted)
                    n++;
            return n;
        }
    }

    /// <summary>
    /// The turn of the result-track beat now playing or next to play (the banner shows its actor: the banner beat is a
    /// result beat, so it changes only when the result track reaches the next turn), or null when the track is empty.
    /// </summary>
    public int? CurrentResultTurn
    {
        get
        {
            Item head = ResultHead();
            return head != null ? head.Turn : (int?)null;
        }
    }

    /// <summary>The stagger at <paramref name="speed"/>: <see cref="BaseStagger"/> divided by the speed (1 or more).</summary>
    public static float StaggerFor(float speed) => BaseStagger / SafeSpeed(speed);

    private static float SafeSpeed(float speed) => speed > 1f && !float.IsNaN(speed) && !float.IsInfinity(speed) ? speed : 1f;

    /// <summary>An item's seconds at <paramref name="speed"/> (divided when it scales with the speed; a step at least <see cref="MinStepSeconds"/>).</summary>
    public static float BaseDuration(Item item, float speed)
    {
        if (item == null)
            return 0f;
        float s = Math.Max(0f, item.Seconds);
        if (item.ScaleWithSpeed)
            s /= SafeSpeed(speed);
        if (item.IsStep)
            return Math.Max(MinStepSeconds, s);
        return item.IsPace ? s : 0f;
    }

    /// <summary>Drops every item and lane and restarts the clock.</summary>
    public void Reset()
    {
        _lanes.Clear();
        _unfinished.Clear();
        _results.Clear();
        _resultHead = 0;
        _tail = null;
        _lastAdmitAt = float.NegativeInfinity;
        _admitWakeAt = float.PositiveInfinity;
        Now = 0f;
    }

    /// <summary>Adds the next recorded item (in rules order: its Seq is above every earlier one).</summary>
    public void Add(Item item)
    {
        if (item == null)
            return;
        item.Started = false;
        item.Finished = false;
        item.Elapsed = 0f;
        item.Duration = 0f;

        Lane lane;
        if ((item.Flags & ItemFlags.Barrier) != 0)
        {
            if (_tail != null)
                _tail.Sealed = true;
            lane = NewLane(item.Turn);
            lane.Solo = true;
            Append(lane, item);
            lane.Sealed = true;
            _tail = null; // the next item opens a new lane after the barrier
            return;
        }

        if (_tail == null || _tail.Removed || _tail.Sealed || _tail.Turn != item.Turn)
        {
            if (_tail != null)
                _tail.Sealed = true;
            _tail = NewLane(item.Turn);
        }
        lane = _tail;
        Append(lane, item);
        if ((item.Flags & ItemFlags.StopMark) != 0)
            lane.Solo = true;
        if ((item.Flags & ItemFlags.WindowBreak) != 0)
            lane.Break = true;
    }

    private Lane NewLane(int turn)
    {
        var lane = new Lane { Turn = turn };
        _lanes.Add(lane);
        return lane;
    }

    private void Append(Lane lane, Item item)
    {
        item.Lane = lane;
        lane.Items.Add(item);
        lane.Remaining++;
        if (item.OnLane && lane.FirstLaneSeq == int.MaxValue)
            lane.FirstLaneSeq = item.Seq;
        _unfinished.Add(item);
        if (!item.OnLane)
            _results.Add(item);
    }

    /// <summary>
    /// Plays <paramref name="deltaTime"/> seconds at <paramref name="speed"/>: every item that finishes is added to
    /// <paramref name="finished"/> in the order it finishes (apply them in that order; a zero-length item finishes as it
    /// starts). Items that start and end within the frame are all reported; a timed item still in flight is in
    /// <see cref="CopyInFlight"/>.
    /// </summary>
    public void Advance(float deltaTime, float speed, List<Item> finished)
    {
        float remaining = deltaTime > 0f && !float.IsNaN(deltaTime) ? deltaTime : 0f;
        for (int guard = 0; guard < MaxEventsPerAdvance; guard++)
        {
            StartReady(speed, finished);
            float next = NextEventIn();
            if (float.IsPositiveInfinity(next) || next > remaining)
            {
                Tick(remaining);
                break;
            }
            Tick(next);
            remaining -= next;
            FinishDue(finished);
        }
        Compact();
    }

    /// <summary>Copies the timed items in flight (started, not finished) into <paramref name="into"/>, in rules order.</summary>
    public void CopyInFlight(List<Item> into)
    {
        if (into == null)
            return;
        foreach (Item it in _unfinished)
            if (it.Started && !it.Finished)
                into.Add(it);
    }

    // ── Starting ────────────────────────────────────────────────────────────────────────────────────────────────────

    private void StartReady(float speed, List<Item> finished)
    {
        for (int guard = 0; guard < MaxEventsPerAdvance; guard++)
        {
            bool progress = AdmitLanes(speed);

            Item head = ResultHead();
            if (head != null && !head.Started && CanStartResult(head))
            {
                Start(head, speed, finished);
                progress = true;
            }

            for (int i = 0; i < _lanes.Count; i++)
            {
                Lane lane = _lanes[i];
                if (!lane.Admitted)
                    break; // lanes are admitted in order
                Item next = NextLaneItem(lane);
                if (next != null && !next.Started && CanStartLaneItem(next))
                {
                    Start(next, speed, finished);
                    progress = true;
                }
            }

            if (!progress)
                return;
        }
    }

    private bool IsComplete(Lane lane) => lane.Sealed || (!RecordingOpen && ReferenceEquals(lane, _tail));

    private bool AdmitLanes(float speed)
    {
        bool admitted = false;
        _admitWakeAt = float.PositiveInfinity;
        // A finished lane leaves (it frees its slot and, at a window's end, opens the next window).
        for (int i = _lanes.Count - 1; i >= 0; i--)
        {
            Lane l = _lanes[i];
            if (l.Remaining == 0 && IsComplete(l))
            {
                l.Removed = true;
                _lanes.RemoveAt(i);
            }
        }

        float stagger = StaggerFor(speed);
        for (int i = 0; i < _lanes.Count; i++)
        {
            Lane lane = _lanes[i];
            if (i > 0 && (lane.Solo || lane.Break || _lanes[i - 1].Solo))
                break; // a later window: it opens once every lane before it has finished (R7)
            if (lane.Admitted)
                continue;
            if (!IsComplete(lane))
                break; // R1: the turn is still recording
            if (i >= MaxLanes)
                break; // R8: every lane before this one plays
            if (i > 0 && Now + Epsilon < _lastAdmitAt + stagger)
            {
                _admitWakeAt = _lastAdmitAt + stagger; // R6
                break;
            }
            lane.Admitted = true;
            lane.AdmittedAt = Now;
            _lastAdmitAt = Now;
            admitted = true;
        }
        return admitted;
    }

    private Item ResultHead()
    {
        while (_resultHead < _results.Count && _results[_resultHead].Finished)
            _resultHead++;
        return _resultHead < _results.Count ? _results[_resultHead] : null;
    }

    private static Item NextLaneItem(Lane lane)
    {
        foreach (Item it in lane.Items)
            if (it.OnLane && !it.Finished)
                return it;
        return null;
    }

    private bool CanStartResult(Item item)
    {
        Lane lane = item.Lane;
        if (lane == null || !lane.Admitted)
            return false;
        foreach (Item other in _unfinished)
        {
            if (other.Seq >= item.Seq)
                break;
            if (other.Finished)
                continue; // finished earlier in this Advance (Compact has not run yet): it no longer blocks
            if (ReferenceEquals(other.Lane, lane) && other.OnLane)
                return false; // R5: a lane's result beats wait for its earlier segments
            if (item.Token != null && ReferenceEquals(other.Token, item.Token))
                return false; // R3
        }
        return true;
    }

    private bool CanStartLaneItem(Item item)
    {
        Lane lane = item.Lane;
        if (lane == null || !lane.Admitted)
            return false;
        foreach (Item other in _unfinished)
        {
            if (other.Seq >= item.Seq)
                break;
            if (other.Finished)
                continue; // finished earlier in this Advance (Compact has not run yet): it no longer blocks
            if (item.Token != null && ReferenceEquals(other.Token, item.Token))
                return false; // R3
            if (ReferenceEquals(other.Lane, lane) && !other.OnLane && other.Seq > lane.FirstLaneSeq)
                return false; // R5: the result beats between the previous segment and this one
            if (Touches(other.Cells, item.IntoCells))
                return false; // R4
        }
        return true;
    }

    private static bool Touches(int[] a, int[] b)
    {
        if (a == null || b == null)
            return false;
        for (int i = 0; i < a.Length; i++)
            for (int j = 0; j < b.Length; j++)
                if (a[i] == b[j])
                    return true;
        return false;
    }

    private void Start(Item item, float speed, List<Item> finished)
    {
        item.Started = true;
        item.StartedAt = Now;
        item.Elapsed = 0f;
        float d = BaseDuration(item, speed);
        if (item.IsPace && AdmittedLanes >= 2)
            d = Math.Min(d, StaggerFor(speed)); // R2: results are spaced by the stagger while lanes overlap
        item.Duration = d;
        if (d <= 0f)
            Finish(item, finished);
    }

    // ── Time ────────────────────────────────────────────────────────────────────────────────────────────────────────

    private float NextEventIn()
    {
        float next = float.PositiveInfinity;
        foreach (Item it in _unfinished)
            if (it.Started && !it.Finished)
                next = Math.Min(next, Math.Max(0f, it.Duration - it.Elapsed));
        if (!float.IsPositiveInfinity(_admitWakeAt))
            next = Math.Min(next, Math.Max(0f, _admitWakeAt - Now));
        return next;
    }

    private void Tick(float seconds)
    {
        if (seconds <= 0f)
            return;
        Now += seconds;
        foreach (Item it in _unfinished)
            if (it.Started && !it.Finished)
                it.Elapsed += seconds;
    }

    private void FinishDue(List<Item> finished)
    {
        // In rules order, so two items that end at the same moment apply in seq order.
        foreach (Item it in _unfinished)
            if (it.Started && !it.Finished && it.Elapsed + Epsilon >= it.Duration)
                Finish(it, finished);
    }

    private static void Finish(Item item, List<Item> finished)
    {
        item.Finished = true;
        item.Elapsed = item.Duration;
        if (item.Lane != null)
            item.Lane.Remaining--;
        finished?.Add(item);
    }

    private void Compact()
    {
        _unfinished.RemoveAll(i => i.Finished);
        if (_resultHead > 0)
        {
            _results.RemoveRange(0, _resultHead);
            _resultHead = 0;
        }
    }

    // ── S5 ──────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The window cap (S5, design choice C5): a creature's second turn inside the current overlap window ends the window,
    /// and a new one opens with that turn. CombatPresentation notes each AI-decided turn of a group here
    /// (TurnControl.AiTurnStarting) and records a window break when <see cref="Note"/> says so; a window stop (S2) and a
    /// group open start a fresh window (<see cref="Reset"/>).
    /// </summary>
    public sealed class WindowCap
    {
        private readonly List<object> _actors = new List<object>();

        /// <summary>The creatures with a turn in the current window.</summary>
        public int Count => _actors.Count;

        /// <summary>
        /// Notes <paramref name="actor"/>'s turn: true when it already has a turn in the window (the window ends here and
        /// the new one holds this turn), false otherwise. A null actor never breaks a window.
        /// </summary>
        public bool Note(object actor)
        {
            if (actor == null)
                return false;
            foreach (object a in _actors)
            {
                if (ReferenceEquals(a, actor))
                {
                    _actors.Clear();
                    _actors.Add(actor);
                    return true;
                }
            }
            _actors.Add(actor);
            return false;
        }

        /// <summary>A fresh window (a group opens, or a window stop ended the last one).</summary>
        public void Reset() => _actors.Clear();
    }
}
