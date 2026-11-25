using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// The sink of applied presentation events, per channel (grouped AI turns step 8, docs/designs/grouped_ai_turns.md
/// "Recording (resolve now, show later)" and "Composition with batch 14", Sidecars). It follows the ActionSetShadow
/// pattern: on in the editor and in development builds only, no dice, no rules writes, and nothing reaches the scenario
/// trace or its hash. The scenario harness records one job at a time (<see cref="Begin"/>) and writes the unhashed
/// sidecar &lt;RunDir&gt;/&lt;jobId&gt;/presentation.jsonl beside caststages.jsonl (<see cref="Write"/>), so step 9's
/// PresentationCheck can compare a run with grouping off and one with grouping on at the same boundaries.
///
/// Channels (one JSON object per line: n, the line's order; ch; optional tok or k; v):
/// <list type="bullet">
/// <item>"log" every combat log line applied (CombatUI.ShowCombatLog and the other log calls), "sep" turn separators;</item>
/// <item>"b" a rules boundary (CombatPresentation.Boundary: wait, step, control, interactive, combat-end);</item>
/// <item>"kf" a token's keyframe at a boundary, only when it differs from that token's last one (tok "Name#k");</item>
/// <item>"step" a MoveAlongPath step (tok, v "x,y&gt;x,y");</item>
/// <item>"control", "interactive", "defer", "area" the beats of those classes as they apply;</item>
/// <item>"state" the last value of a state key (k: indicator, initiative, active-pc, active-npc, buttons, panel:SLOT,
/// buff:SLOT, cell:x,y), flushed at each boundary when it changed since the last flush (cells and panels change many
/// times between two boundaries; only what a yield would show counts).</item>
/// <item>"error" the trace faulted (<see cref="Fault"/>): it stopped itself there and the rest of the job ran untraced.</item>
/// </list>
///
/// Fault isolation: the trace runs inside rules coroutines (every CombatPacing.Wait, every MoveAlongPath step, the
/// facade's applies), only while it records, so an exception of its own must never reach them: the rules outcome and the
/// scenario hash would then depend on whether the harness records. Every public entry point here, and the recording
/// bodies of CombatPresentation.Boundary and RecordStep, catch and pass the exception to <see cref="Fault"/>.
/// </summary>
public static class PresentationTrace
{
    /// <summary>The most lines one job keeps; past it the trace notes the cut and drops the rest.</summary>
    public const int MaxLines = 250000;

    private static readonly List<string> _lines = new List<string>();
    private static readonly Dictionary<TokenView, string> _tokenIds = new Dictionary<TokenView, string>();
    private static readonly Dictionary<string, int> _nameCounts = new Dictionary<string, int>(System.StringComparer.Ordinal);
    private static readonly Dictionary<TokenView, string> _lastKeyframe = new Dictionary<TokenView, string>();
    private static readonly SortedDictionary<string, string> _pendingState = new SortedDictionary<string, string>(System.StringComparer.Ordinal);
    private static readonly Dictionary<string, string> _flushedState = new Dictionary<string, string>(System.StringComparer.Ordinal);
    private static readonly Dictionary<Vector2Int, string> _pendingCells = new Dictionary<Vector2Int, string>();
    private static bool _truncated;

    /// <summary>Whether the trace can record at all: in the editor and in development builds only.</summary>
    public static bool Enabled => Application.isEditor || Debug.isDebugBuild;

    /// <summary>True while a job is recorded (between <see cref="Begin"/> and <see cref="Stop"/>).</summary>
    public static bool Recording { get; private set; }

    /// <summary>The lines recorded since <see cref="Begin"/>.</summary>
    public static IReadOnlyList<string> Lines => _lines;

    /// <summary>The fault that stopped this job's recording ("Type: message"), or null (<see cref="Fault"/>).</summary>
    public static string FaultMessage { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Recording = false;
        ClearData();
    }

    /// <summary>
    /// The trace threw (a keyframe of a half-reset creature, say): one "error" line, one warning (never an error, which
    /// would trip Error Pause and the harness's log checks), and the recording stops for the rest of the job. The rules
    /// caller goes on as if nothing were traced.
    /// </summary>
    public static void Fault(System.Exception e, string where)
    {
        if (!Recording)
            return;
        Recording = false;
        string what = (e != null ? e.GetType().Name + ": " + e.Message : "unknown") + " (" + (where ?? "?") + ")";
        FaultMessage = what;
        try
        {
            // Past the line cap too: the fault is the one line a reader must see.
            _lines.Add("{\"n\":" + _lines.Count + ",\"ch\":\"error\",\"v\":" + Quote(what) + "}");
        }
        catch (System.Exception)
        {
            // The error line is best effort; the recording is already off.
        }
        Debug.LogWarning("[Presentation] trace faulted, the rest of the job runs untraced: " + what);
    }

    /// <summary>Starts recording a new job (the old lines are dropped). Does nothing outside the editor and development builds.</summary>
    public static void Begin()
    {
        ClearData();
        Recording = Enabled;
    }

    /// <summary>Stops recording; the lines stay until the next <see cref="Begin"/>.</summary>
    public static void Stop()
    {
        if (Recording)
        {
            try { FlushState(); }
            catch (System.Exception e) { Fault(e, "stop"); }
        }
        Recording = false;
    }

    private static void ClearData()
    {
        _lines.Clear();
        _tokenIds.Clear();
        _nameCounts.Clear();
        _lastKeyframe.Clear();
        _pendingState.Clear();
        _flushedState.Clear();
        _pendingCells.Clear();
        _truncated = false;
        FaultMessage = null;
    }

    /// <summary>Writes the recorded lines to <paramref name="path"/> (its directory is created). Returns the line count.</summary>
    public static int Write(string path)
    {
        if (Recording)
        {
            try { FlushState(); }
            catch (System.Exception e) { Fault(e, "write"); }
        }
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        var sb = new StringBuilder();
        for (int i = 0; i < _lines.Count; i++)
            sb.Append(_lines[i]).Append('\n');
        File.WriteAllText(path, sb.ToString());
        return _lines.Count;
    }

    // ── Channels ────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>An applied event of <paramref name="channel"/> (log, sep, control, interactive, defer, area).</summary>
    public static void Applied(string channel, string value)
    {
        if (!Recording)
            return;
        try { Add(channel, null, null, value); }
        catch (System.Exception e) { Fault(e, "applied " + channel); }
    }

    /// <summary>The latest value of a state key; flushed at the next boundary when it changed.</summary>
    public static void State(string key, string value)
    {
        if (!Recording || key == null)
            return;
        try { _pendingState[key] = value ?? ""; }
        catch (System.Exception e) { Fault(e, "state " + key); }
    }

    /// <summary>The latest state of a cell's highlight (SquareCell's applies): "t:Type" or "o:r,g,b,a" or "o:off".</summary>
    public static void Cell(Vector2Int coords, string value)
    {
        if (!Recording)
            return;
        try { _pendingCells[coords] = value; }
        catch (System.Exception e) { Fault(e, "cell"); }
    }

    /// <summary>A rules boundary: the pending state is flushed, then the boundary line.</summary>
    public static void BoundaryReached(PresentationBoundary kind)
    {
        if (!Recording)
            return;
        try
        {
            FlushState();
            Add("b", null, null, BoundaryName(kind));
        }
        catch (System.Exception e) { Fault(e, "boundary"); }
    }

    /// <summary>A token's keyframe at the boundary just reached, kept only when it differs from the token's last one.</summary>
    public static void Keyframe(TokenView view, TokenKeyframe keyframe)
    {
        if (!Recording || view == null)
            return;
        try
        {
            string v = keyframe.Encode();
            if (_lastKeyframe.TryGetValue(view, out string last) && last == v)
                return;
            _lastKeyframe[view] = v;
            Add("kf", TokenId(view, null), null, v);
        }
        catch (System.Exception e) { Fault(e, "keyframe"); }
    }

    /// <summary>A MoveAlongPath step of <paramref name="mover"/>.</summary>
    public static void Step(TokenView view, CharacterController mover, Vector2Int from, Vector2Int to)
    {
        if (!Recording)
            return;
        try
        {
            string tok = view != null ? TokenId(view, mover) : (mover != null && mover.Stats != null ? mover.Stats.CharacterName : "?");
            Add("step", tok, null, from.x + "," + from.y + ">" + to.x + "," + to.y);
        }
        catch (System.Exception e) { Fault(e, "step"); }
    }

    private static string BoundaryName(PresentationBoundary kind)
    {
        switch (kind)
        {
            case PresentationBoundary.Wait: return "wait";
            case PresentationBoundary.Step: return "step";
            case PresentationBoundary.Control: return "control";
            case PresentationBoundary.Interactive: return "interactive";
            case PresentationBoundary.CombatEnd: return "combat-end";
            default: return kind.ToString();
        }
    }

    private static void FlushState()
    {
        if (_pendingState.Count > 0)
        {
            foreach (KeyValuePair<string, string> kv in _pendingState)
            {
                if (_flushedState.TryGetValue(kv.Key, out string last) && last == kv.Value)
                    continue;
                _flushedState[kv.Key] = kv.Value;
                Add("state", null, kv.Key, kv.Value);
            }
            _pendingState.Clear();
        }
        if (_pendingCells.Count > 0)
        {
            var cells = new List<Vector2Int>(_pendingCells.Keys);
            cells.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            foreach (Vector2Int c in cells)
            {
                string key = "cell:" + c.x + "," + c.y;
                string v = _pendingCells[c];
                if (_flushedState.TryGetValue(key, out string last) && last == v)
                    continue;
                _flushedState[key] = v;
                Add("state", null, key, v);
            }
            _pendingCells.Clear();
        }
    }

    /// <summary>
    /// A stable id for a token within one job: its creature's name (the model's name without one), with "#k" for the
    /// k-th token of that name the trace saw (k from 1), so two runs of a job name their tokens alike.
    /// </summary>
    private static string TokenId(TokenView view, CharacterController creature)
    {
        if (_tokenIds.TryGetValue(view, out string id))
            return id;
        GameObject model = view.Model;
        if (creature == null && model != null)
            creature = model.GetComponent<CharacterController>();
        string name = creature != null && creature.Stats != null && !string.IsNullOrEmpty(creature.Stats.CharacterName)
            ? creature.Stats.CharacterName
            : (model != null ? model.name : "?");
        int k = (_nameCounts.TryGetValue(name, out int n) ? n : 0) + 1;
        _nameCounts[name] = k;
        id = name + "#" + k;
        _tokenIds[view] = id;
        return id;
    }

    private static void Add(string channel, string tok, string key, string value)
    {
        if (_truncated)
            return;
        if (_lines.Count >= MaxLines)
        {
            _truncated = true;
            _lines.Add("{\"n\":" + _lines.Count + ",\"ch\":\"truncated\",\"v\":\"" + MaxLines + " lines\"}");
            return;
        }
        var sb = new StringBuilder(64);
        sb.Append("{\"n\":").Append(_lines.Count).Append(",\"ch\":\"").Append(channel).Append('"');
        if (tok != null)
            sb.Append(",\"tok\":").Append(Quote(tok));
        if (key != null)
            sb.Append(",\"k\":").Append(Quote(key));
        sb.Append(",\"v\":").Append(Quote(value ?? "")).Append('}');
        _lines.Add(sb.ToString());
    }

    /// <summary>A JSON string literal of <paramref name="s"/>.</summary>
    public static string Quote(string s)
    {
        var sb = new StringBuilder(s.Length + 2);
        sb.Append('"');
        foreach (char ch in s)
        {
            switch (ch)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (ch < 0x20)
                        sb.Append("\\u").Append(((int)ch).ToString("x4"));
                    else
                        sb.Append(ch);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }
}
