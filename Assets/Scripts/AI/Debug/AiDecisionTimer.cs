using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

/// <summary>The decision kinds the AI decision timer keeps apart (batch 14 step 1).</summary>
public enum AiTimeBucket
{
    Target,
    Action,
    Maneuver,
    Spell,
    Movement,
    LegalSet,
    Personality
}

/// <summary>
/// The AI decision-time measurement (owner request 2026-10-30; batch 14 step 1, docs/designs/personalities_batch14.md
/// "Trace and decision timing"). It times only the AI's synchronous decision code: <see cref="Open"/> scopes wrap target
/// choice, action choice, the maneuver choice, spell scoring, movement scoring and the legal-set check, never an
/// Execute call, a coroutine wait or movement playback. Time is exclusive: while an inner scope is open, the outer scope's
/// clock stops, so each tick is charged to one bucket only. A turn is opened by <see cref="BeginTurn"/> and closed by
/// <see cref="EndTurn"/>, keyed by actor, so re-entrant turns (CORE-012) do not mix; a scope charges the open turn of the
/// actor it names (or, with no actor, the innermost open scope's turn, else the turn begun last).
/// <para>Like <see cref="ActionSetShadow"/>, it runs in the editor and in development builds only, reads no dice, writes
/// no log line and changes no state the game reads, so it can never change a turn or a scenario trace hash. The scenario
/// harness resets it per job and writes the turns into the job's result (summary.json "aiTiming", ai-timing.tsv).</para>
/// </summary>
public static class AiDecisionTimer
{
    /// <summary>The per-turn budget: a turn whose decisions take longer gets a harness Notes warning, never a Fail.</summary>
    public const double WarnMs = 50.0;

    /// <summary>Completed turns kept since the last <see cref="Reset"/>; later ones are counted in <see cref="Dropped"/>.</summary>
    public const int MaxSamples = 4096;

    private static readonly int BucketCount = Enum.GetValues(typeof(AiTimeBucket)).Length;

    /// <summary>One AI turn's decision time.</summary>
    public sealed class TurnSample
    {
        public string Actor;
        public int Round;
        /// <summary>Exclusive ticks per <see cref="AiTimeBucket"/> (indexed by the enum value).</summary>
        public readonly long[] BucketTicks;
        /// <summary>True when the turn was closed without its EndTurn (a new turn of the same actor, a flush or a reset).</summary>
        public bool Unclosed;

        public TurnSample(int buckets) { BucketTicks = new long[buckets]; }

        public long Ticks
        {
            get
            {
                long t = 0;
                for (int i = 0; i < BucketTicks.Length; i++)
                    t += BucketTicks[i];
                return t;
            }
        }

        public double Ms => ToMs(Ticks);
        public double BucketMs(AiTimeBucket b) => ToMs(BucketTicks[(int)b]);
    }

    private struct Frame
    {
        public TurnSample Turn;
        public int Bucket;
    }

    private static readonly Dictionary<object, TurnSample> _open = new Dictionary<object, TurnSample>();
    private static readonly List<TurnSample> _completed = new List<TurnSample>();
    private static readonly List<Frame> _stack = new List<Frame>();
    private static readonly long[] _outsideTicks = new long[BucketCount];
    private static TurnSample _lastBegun;
    private static long _mark;
    private static int _generation = 1;
    private static bool? _enabledOverride;

    /// <summary>The clock (Stopwatch ticks); a test may swap it for a fake one.</summary>
    internal static Func<long> Clock = Stopwatch.GetTimestamp;
    /// <summary>Ticks of <see cref="Clock"/> per millisecond.</summary>
    internal static double TicksPerMs = Stopwatch.Frequency / 1000.0;

    /// <summary>On in the editor and in development builds, as <see cref="ActionSetShadow.Enabled"/>; a test may override it.</summary>
    public static bool Enabled => _enabledOverride ?? (UnityEngine.Application.isEditor || UnityEngine.Debug.isDebugBuild);

    /// <summary>Test seam: force the timer on or off (null restores the build default).</summary>
    internal static void OverrideEnabled(bool? enabled) { _enabledOverride = enabled; }

    /// <summary>The turns completed since the last <see cref="Reset"/>, in the order they ended.</summary>
    public static IReadOnlyList<TurnSample> Completed => _completed;

    /// <summary>Completed turns not kept because <see cref="MaxSamples"/> was reached.</summary>
    public static int Dropped { get; private set; }

    /// <summary>Turns open now.</summary>
    public static int OpenTurns => _open.Count;

    /// <summary>The turns open now (read only; a scenario expectation counts them with the completed ones).</summary>
    public static IEnumerable<TurnSample> OpenTurnSamples => _open.Values;

    /// <summary>Decision time charged to no turn (a scope for an actor whose turn is not open), in milliseconds.</summary>
    public static double OutsideMs(AiTimeBucket b) => ToMs(_outsideTicks[(int)b]);

    public static double ToMs(long ticks) => ticks / TicksPerMs;

    /// <summary>Drops every open and completed turn and every open scope (the harness calls it when a job starts).</summary>
    public static void Reset()
    {
        _open.Clear();
        _completed.Clear();
        _stack.Clear();
        Array.Clear(_outsideTicks, 0, _outsideTicks.Length);
        _lastBegun = null;
        Dropped = 0;
        _generation++;
    }

    /// <summary>Opens the turn of <paramref name="actor"/>; an open turn of the same actor is closed first, as unclosed.</summary>
    public static void BeginTurn(object actor, string name, int round)
    {
        if (actor == null || !Enabled)
            return;
        if (_open.TryGetValue(actor, out TurnSample stale))
        {
            stale.Unclosed = true;
            Complete(actor, stale);
        }
        var t = new TurnSample(BucketCount) { Actor = name ?? "?", Round = round };
        _open[actor] = t;
        _lastBegun = t;
    }

    /// <summary>Closes the turn of <paramref name="actor"/> and returns it, or null when none was open.</summary>
    public static TurnSample EndTurn(object actor)
    {
        if (actor == null || !_open.TryGetValue(actor, out TurnSample t))
            return null;
        Complete(actor, t);
        return t;
    }

    /// <summary>
    /// Drops the open turn of <paramref name="actor"/> without completing it: a scenario-scripted turn, which makes no AI
    /// decision (the harness's script replaces the routine), so it is not an AI turn sample.
    /// </summary>
    public static void CancelTurn(object actor)
    {
        if (actor == null || !_open.TryGetValue(actor, out TurnSample t))
            return;
        _open.Remove(actor);
        if (ReferenceEquals(_lastBegun, t))
            _lastBegun = null;
    }

    /// <summary>Closes every open turn as unclosed (a coroutine stopped before its EndTurn); the harness calls it at a job's end.</summary>
    public static void FlushOpenTurns()
    {
        if (_open.Count == 0)
            return;
        var keys = new List<object>(_open.Keys);
        foreach (object k in keys)
        {
            TurnSample t = _open[k];
            t.Unclosed = true;
            Complete(k, t);
        }
    }

    private static void Complete(object actor, TurnSample t)
    {
        _open.Remove(actor);
        if (ReferenceEquals(_lastBegun, t))
            _lastBegun = null;
        if (_completed.Count < MaxSamples)
            _completed.Add(t);
        else
            Dropped++;
    }

    /// <summary>
    /// Opens a timing scope; dispose it (a <c>using</c> declaration) when the decision code returns. With
    /// <paramref name="actor"/> the time goes to that actor's open turn (or to no turn when it has none); without it, to
    /// the innermost open scope's turn, else to the turn begun last. A no-op when the timer is off.
    /// </summary>
    public static Scope Open(AiTimeBucket bucket, object actor = null)
    {
        if (!Enabled)
            return default;
        long now = Clock();
        TurnSample turn;
        if (actor != null)
            _open.TryGetValue(actor, out turn);
        else if (_stack.Count > 0)
            turn = _stack[_stack.Count - 1].Turn;
        else
            turn = _lastBegun;
        if (_stack.Count > 0)
            Charge(_stack[_stack.Count - 1], now - _mark);
        _stack.Add(new Frame { Turn = turn, Bucket = (int)bucket });
        _mark = now;
        return new Scope(_stack.Count, _generation);
    }

    private static void Close(int depth, int generation)
    {
        if (generation != _generation || depth <= 0 || _stack.Count < depth)
            return; // reset since, or already closed by an outer scope
        long now = Clock();
        // An inner scope left open (it cannot happen with using declarations, but a throw between two hand-made scopes
        // could) is closed with this one, so the stack never drifts.
        while (_stack.Count >= depth)
        {
            Charge(_stack[_stack.Count - 1], now - _mark);
            _mark = now;
            _stack.RemoveAt(_stack.Count - 1);
        }
    }

    private static void Charge(Frame f, long ticks)
    {
        if (ticks <= 0)
            return;
        if (f.Turn != null)
            f.Turn.BucketTicks[f.Bucket] += ticks;
        else
            _outsideTicks[f.Bucket] += ticks;
    }

    /// <summary>A timing scope (<see cref="Open"/>); the default value is inert.</summary>
    public readonly struct Scope : IDisposable
    {
        private readonly int _depth;
        private readonly int _generation;

        internal Scope(int depth, int generation)
        {
            _depth = depth;
            _generation = generation;
        }

        public void Dispose()
        {
            if (_depth > 0)
                Close(_depth, _generation);
        }
    }

    /// <summary>
    /// <paramref name="source"/> with each step of its enumeration timed in <paramref name="bucket"/>: for a lazy
    /// decision iterator whose consumer acts between items (the ranked spell candidates, whose consumer casts), so the
    /// consumer's work stays outside the scope. The items and their order are unchanged.
    /// </summary>
    public static IEnumerable<T> Timed<T>(IEnumerable<T> source, AiTimeBucket bucket, object actor)
    {
        if (source == null || !Enabled)
            return source;
        return new TimedEnumerable<T>(source, bucket, actor);
    }

    private sealed class TimedEnumerable<T> : IEnumerable<T>
    {
        private readonly IEnumerable<T> _source;
        private readonly AiTimeBucket _bucket;
        private readonly object _actor;

        public TimedEnumerable(IEnumerable<T> source, AiTimeBucket bucket, object actor)
        {
            _source = source;
            _bucket = bucket;
            _actor = actor;
        }

        public IEnumerator<T> GetEnumerator()
        {
            IEnumerator<T> inner;
            using (Open(_bucket, _actor))
                inner = _source.GetEnumerator();
            return new TimedEnumerator(inner, _bucket, _actor);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private sealed class TimedEnumerator : IEnumerator<T>
        {
            private readonly IEnumerator<T> _inner;
            private readonly AiTimeBucket _bucket;
            private readonly object _actor;

            public TimedEnumerator(IEnumerator<T> inner, AiTimeBucket bucket, object actor)
            {
                _inner = inner;
                _bucket = bucket;
                _actor = actor;
            }

            public T Current => _inner.Current;
            object IEnumerator.Current => _inner.Current;

            public bool MoveNext()
            {
                using (Open(_bucket, _actor))
                    return _inner.MoveNext();
            }

            public void Reset() => _inner.Reset();

            public void Dispose()
            {
                using (Open(_bucket, _actor))
                    _inner.Dispose();
            }
        }
    }

    /// <summary>Nearest-rank percentile (<paramref name="p"/> in [0, 100]) of <paramref name="values"/>; 0 when empty.</summary>
    public static double Percentile(IList<double> values, double p)
    {
        if (values == null || values.Count == 0)
            return 0.0;
        var sorted = new List<double>(values);
        sorted.Sort();
        int rank = (int)Math.Ceiling(p / 100.0 * sorted.Count);
        if (rank < 1)
            rank = 1;
        if (rank > sorted.Count)
            rank = sorted.Count;
        return sorted[rank - 1];
    }

    /// <summary>The summary of a set of turn times: count, p50, p95, max and how many are over <see cref="WarnMs"/>.</summary>
    public struct Stats
    {
        public int Turns;
        public double P50;
        public double P95;
        public double Max;
        public int Over;
    }

    public static Stats Summarize(IList<double> ms)
    {
        var s = new Stats();
        if (ms == null || ms.Count == 0)
            return s;
        s.Turns = ms.Count;
        s.P50 = Percentile(ms, 50);
        s.P95 = Percentile(ms, 95);
        double max = 0;
        int over = 0;
        for (int i = 0; i < ms.Count; i++)
        {
            if (ms[i] > max)
                max = ms[i];
            if (ms[i] > WarnMs)
                over++;
        }
        s.Max = max;
        s.Over = over;
        return s;
    }
}
