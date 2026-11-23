using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// The AI decision trace v1 (batch 14 step 1, docs/designs/personalities_batch14.md "Trace and decision timing"). It keeps
/// one record per AI turn (round, actor, Role, traits) with the decisions the AI made in it: the site, the choice, the
/// Role's legacy choice, the personality's bonus and a reason. It keeps a ring of the last <see cref="RingSize"/> turns
/// and counts per site and verdict ("legacy" when the choice is the legacy one, else "changed").
/// <para>It follows the <see cref="ActionSetShadow"/> pattern: on in the editor and in development builds only, no dice,
/// no combat-log line and nothing the game reads, so a scenario trace hash never sees it (it is never in
/// ScenarioTrace.HashLines). With <see cref="AIService.TraceEnabled"/> on, each finished turn is also one collapsed
/// "[AI-TRACE]" Debug.Log line (the harness's fast mode drops Log-level lines, so the ring is the harness's reader).</para>
/// </summary>
public static class AiDecisionTrace
{
    /// <summary>Finished turns kept in the ring.</summary>
    public const int RingSize = 50;

    /// <summary>Decisions kept per turn; later ones are counted but not kept.</summary>
    public const int MaxDecisionsPerTurn = 64;

    public sealed class Decision
    {
        public string Site;
        public string Chosen;
        public string Legacy;
        public float Bonus;
        public string Reason;

        public string Verdict => string.Equals(Chosen, Legacy, StringComparison.Ordinal) ? "legacy" : "changed";
    }

    public sealed class TurnRecord
    {
        public int Round;
        public string Actor;
        public string Role;
        public string Traits;
        public readonly List<Decision> Decisions = new List<Decision>();
        /// <summary>Decisions past <see cref="MaxDecisionsPerTurn"/> (counted, not kept).</summary>
        public int Overflow;

        /// <summary>The collapsed one-line form ("[AI-TRACE] r2 Goblin 1 (Default AI) Target=Fighter; Action=Attack").</summary>
        public string Collapsed()
        {
            var sb = new StringBuilder();
            sb.Append("[AI-TRACE] r").Append(Round.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(Actor)
              .Append(" (").Append(Role ?? "none");
            if (!string.IsNullOrEmpty(Traits))
                sb.Append("; ").Append(Traits);
            sb.Append(')');
            for (int i = 0; i < Decisions.Count; i++)
            {
                Decision d = Decisions[i];
                sb.Append(i == 0 ? " " : "; ").Append(d.Site).Append('=').Append(d.Chosen ?? "-");
                if (d.Verdict != "legacy")
                    sb.Append(" (legacy ").Append(d.Legacy ?? "-").Append(')');
                if (d.Bonus != 0f)
                    sb.Append(' ').Append(d.Bonus.ToString("+0.##;-0.##", CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(d.Reason))
                    sb.Append(" [").Append(d.Reason).Append(']');
            }
            if (Overflow > 0)
                sb.Append("; +").Append(Overflow.ToString(CultureInfo.InvariantCulture)).Append(" more");
            return sb.ToString();
        }
    }

    private static readonly Dictionary<object, TurnRecord> _open = new Dictionary<object, TurnRecord>();
    private static readonly Queue<TurnRecord> _ring = new Queue<TurnRecord>();
    private static readonly Dictionary<string, int> _counts = new Dictionary<string, int>(StringComparer.Ordinal);
    private static bool? _enabledOverride;

    /// <summary>On in the editor and in development builds, as <see cref="ActionSetShadow.Enabled"/>; a test may override it.</summary>
    public static bool Enabled => _enabledOverride ?? (UnityEngine.Application.isEditor || UnityEngine.Debug.isDebugBuild);

    /// <summary>Test seam: force the trace on or off (null restores the build default).</summary>
    internal static void OverrideEnabled(bool? enabled) { _enabledOverride = enabled; }

    /// <summary>Turns finished since the last <see cref="Clear"/> (the ring keeps the last <see cref="RingSize"/>).</summary>
    public static int TurnsRecorded { get; private set; }

    /// <summary>Decisions per "site: verdict" since the last <see cref="Clear"/>.</summary>
    public static IReadOnlyDictionary<string, int> Counts => _counts;

    /// <summary>The finished turns in the ring, oldest first.</summary>
    public static IReadOnlyList<TurnRecord> Ring => new List<TurnRecord>(_ring);

    /// <summary>Turns open now.</summary>
    public static int OpenTurns => _open.Count;

    /// <summary>
    /// True when <paramref name="actor"/> has an open turn, so a <see cref="Record"/> would keep a row: a caller gates on
    /// it before it formats a row's strings (AiBlend, batch 14 step 3). False when the trace is off.
    /// </summary>
    public static bool HasOpenTurn(object actor) => actor != null && _open.ContainsKey(actor);

    /// <summary>The turn records open now (read only).</summary>
    public static IEnumerable<TurnRecord> OpenRecords => _open.Values;

    /// <summary>Drops the open record of <paramref name="actor"/> unfinished (a scenario-scripted turn makes no AI decision).</summary>
    public static void CancelTurn(object actor)
    {
        if (actor != null)
            _open.Remove(actor);
    }

    public static void Clear()
    {
        _open.Clear();
        _ring.Clear();
        _counts.Clear();
        TurnsRecorded = 0;
    }

    /// <summary>The ring's turns, oldest first, and an empty ring (the counts stay until <see cref="Clear"/>).</summary>
    public static List<TurnRecord> Drain()
    {
        var list = new List<TurnRecord>(_ring);
        _ring.Clear();
        return list;
    }

    /// <summary>Opens the turn record of <paramref name="actor"/>; an open record of the same actor is finished first.</summary>
    public static void BeginTurn(object actor, string name, int round, string role, string traits)
    {
        if (actor == null || !Enabled)
            return;
        if (_open.ContainsKey(actor))
            EndTurn(actor);
        _open[actor] = new TurnRecord { Round = round, Actor = name ?? "?", Role = role, Traits = traits ?? string.Empty };
    }

    /// <summary>Records one decision in the open turn of <paramref name="actor"/>; nothing when it has none.</summary>
    public static void Record(object actor, string site, string chosen, string legacy, float bonus, string reason)
    {
        if (actor == null || !_open.TryGetValue(actor, out TurnRecord t))
            return;
        var d = new Decision { Site = site ?? "?", Chosen = chosen, Legacy = legacy, Bonus = bonus, Reason = reason };
        string key = d.Site + ": " + d.Verdict;
        _counts.TryGetValue(key, out int n);
        _counts[key] = n + 1;
        if (t.Decisions.Count < MaxDecisionsPerTurn)
            t.Decisions.Add(d);
        else
            t.Overflow++;
    }

    /// <summary>Finishes the turn record of <paramref name="actor"/> into the ring and returns it, or null when none was open.</summary>
    public static TurnRecord EndTurn(object actor)
    {
        if (actor == null || !_open.TryGetValue(actor, out TurnRecord t))
            return null;
        _open.Remove(actor);
        _ring.Enqueue(t);
        while (_ring.Count > RingSize)
            _ring.Dequeue();
        TurnsRecorded++;
        if (AIService.TraceEnabled)
            UnityEngine.Debug.Log(t.Collapsed());
        return t;
    }
}
