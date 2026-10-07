#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Tests.Scenarios
{
    /// <summary>An ordered list of JSON fields (keeps field order stable for hashing).</summary>
    public sealed class JsonObj : List<KeyValuePair<string, object>>
    {
        public JsonObj Set(string key, object value)
        {
            for (int i = 0; i < Count; i++)
            {
                if (this[i].Key == key)
                {
                    this[i] = new KeyValuePair<string, object>(key, value);
                    return this;
                }
            }
            Add(new KeyValuePair<string, object>(key, value));
            return this;
        }

        public object Get(string key)
        {
            for (int i = 0; i < Count; i++)
                if (this[i].Key == key)
                    return this[i].Value;
            return null;
        }
    }

    /// <summary>One trace line: sequence, frame (since start), round, turn index, event type and fields.</summary>
    public sealed class TraceEvent
    {
        public int Seq;
        public int Frame;
        public int Round;
        public int Turn;
        public string Ev;
        public readonly JsonObj Fields = new JsonObj();

        public TraceEvent Set(string key, object value) { Fields.Set(key, value); return this; }
        public object Get(string key) => Fields.Get(key);
        public string Str(string key) => Get(key) as string;

        public int Int(string key, int fallback = 0)
        {
            object v = Get(key);
            if (v is int i) return i;
            if (v is long l) return (int)l;
            if (v is bool b) return b ? 1 : 0;
            return fallback;
        }

        public bool Bool(string key) => Get(key) is bool b && b;

        /// <summary>The JSON line. With <paramref name="forHash"/> the frame, the repetition number and wall-clock or host fields are left out.</summary>
        public string ToJson(bool forHash)
        {
            var sb = new StringBuilder(128);
            sb.Append("{\"seq\":").Append(Seq);
            if (!forHash)
                sb.Append(",\"f\":").Append(Frame);
            sb.Append(",\"r\":").Append(Round).Append(",\"t\":").Append(Turn).Append(",\"ev\":");
            Json.WriteString(sb, Ev);
            foreach (KeyValuePair<string, object> kv in Fields)
            {
                if (forHash && Json.IsHashExcluded(kv.Key))
                    continue;
                sb.Append(',');
                Json.WriteString(sb, kv.Key);
                sb.Append(':');
                Json.Write(sb, kv.Value);
            }
            sb.Append('}');
            return sb.ToString();
        }

        public override string ToString() => ToJson(false);
    }

    /// <summary>Minimal JSON writer (no Newtonsoft): strings, numbers (invariant culture), bools, Vector2Int, JsonObj, lists.</summary>
    public static class Json
    {
        private static readonly HashSet<string> HashExcluded = new HashSet<string> { "f", "utc", "gitHead", "wallSec", "rep", "diag" };

        public static bool IsHashExcluded(string key) => HashExcluded.Contains(key);

        public static string Serialize(object value)
        {
            var sb = new StringBuilder();
            Write(sb, value);
            return sb.ToString();
        }

        public static void Write(StringBuilder sb, object v)
        {
            switch (v)
            {
                case null: sb.Append("null"); return;
                case string s: WriteString(sb, s); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); return;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); return;
                case float f: sb.Append(float.IsNaN(f) || float.IsInfinity(f) ? "null" : f.ToString("0.###", CultureInfo.InvariantCulture)); return;
                case double d: sb.Append(double.IsNaN(d) || double.IsInfinity(d) ? "null" : d.ToString("0.###", CultureInfo.InvariantCulture)); return;
                case Vector2Int p: sb.Append('[').Append(p.x).Append(',').Append(p.y).Append(']'); return;
                case Enum e: WriteString(sb, e.ToString()); return;
                case JsonObj o:
                    sb.Append('{');
                    for (int k = 0; k < o.Count; k++)
                    {
                        if (k > 0) sb.Append(',');
                        WriteString(sb, o[k].Key);
                        sb.Append(':');
                        Write(sb, o[k].Value);
                    }
                    sb.Append('}');
                    return;
                case IEnumerable list:
                    sb.Append('[');
                    bool first = true;
                    foreach (object item in list)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        Write(sb, item);
                    }
                    sb.Append(']');
                    return;
                default:
                    WriteString(sb, v.ToString());
                    return;
            }
        }

        public static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        /// <summary>Writes <paramref name="text"/> to a temp file and moves it into place, so a reader never sees half a file.</summary>
        public static void WriteFileAtomic(string path, string text)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text, new UTF8Encoding(false));
            // File.Replace swaps the file in one step (no moment without it); a reader holding the file open
            // (a polling grep) can make it fail with a sharing violation, so retry a few times.
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    if (File.Exists(path))
                        File.Replace(tmp, path, null);
                    else
                        File.Move(tmp, path);
                    return;
                }
                catch (IOException) when (attempt < 4)
                {
                    System.Threading.Thread.Sleep(20 * attempt);
                }
                catch (UnauthorizedAccessException) when (attempt < 4)
                {
                    System.Threading.Thread.Sleep(20 * attempt);
                }
            }
        }
    }

    /// <summary>
    /// Records one job as typed events keyed by stable actor keys: it installs the ScenarioHooks observers
    /// and GameEventSystem subscriptions, keeps the events in memory, and writes the JSONL file and its hash.
    /// Every handler goes through ScenarioHooks.Safe, so a harness bug is recorded instead of breaking the game.
    /// </summary>
    internal sealed class ScenarioTrace
    {
        public const string HarnessVersion = "1";

        private readonly ScenarioJob _job;
        private readonly GameManager _gm;
        private readonly List<TraceEvent> _events = new List<TraceEvent>();
        private readonly Dictionary<CharacterController, string> _keys = new Dictionary<CharacterController, string>();
        private readonly List<CharacterController> _order = new List<CharacterController>();
        private readonly Dictionary<CombatResult, TraceEvent> _attackByResult = new Dictionary<CombatResult, TraceEvent>();
        private readonly Dictionary<string, int> _otherCounters = new Dictionary<string, int>();
        private readonly Dictionary<CharacterController, Action<int, int>> _hpHandlers = new Dictionary<CharacterController, Action<int, int>>();
        private readonly Dictionary<CharacterController, Action<int, int>> _nlHandlers = new Dictionary<CharacterController, Action<int, int>>();
        private readonly Dictionary<CharacterController, JsonObj> _turnStartSnap = new Dictionary<CharacterController, JsonObj>();
        private static readonly Regex TagRegex = new Regex("<[^>]*>", RegexOptions.CultureInvariant);

        private Action<TurnStartedEvent> _onTurnStarted;
        private Action<TurnEndedEvent> _onTurnEnded;
        private Action<NewRoundEvent> _onNewRound;
        private int _startFrame;
        private int _turn;
        private bool _initEmitted;
        private bool _installed;
        private string _lastLog;

        public bool Closed;
        public int LastProgressFrame { get; private set; }
        public float LastProgressTime { get; private set; }
        public IReadOnlyList<TraceEvent> Events => _events;
        public int TurnIndex => _turn;
        public IEnumerable<CharacterController> KnownActors => _order;

        public ScenarioTrace(ScenarioJob job, GameManager gm)
        {
            _job = job;
            _gm = gm;
            _startFrame = Time.frameCount;
            LastProgressFrame = Time.frameCount;
            LastProgressTime = Time.time;
        }

        // ── Keys ────────────────────────────────────────────────────────

        public void RegisterActor(string key, CharacterController c)
        {
            if (c == null || _keys.ContainsKey(c))
                return;
            _keys[c] = key;
            _order.Add(c);
            SubscribeHp(c);
        }

        /// <summary>The key of <paramref name="c"/>; an unseen summon gets 'summon:&lt;name&gt;#n' and an actor line.</summary>
        public string KeyOf(CharacterController c)
        {
            if (c == null)
                return null;
            if (_keys.TryGetValue(c, out string key))
                return key;

            string name = c.Stats != null ? c.Stats.CharacterName : c.name;
            string prefix = _gm != null && _gm.IsSummonedCreature(c) ? "summon:" : "other:";
            string baseKey = prefix + name;
            _otherCounters.TryGetValue(baseKey, out int n);
            n++;
            _otherCounters[baseKey] = n;
            key = baseKey + "#" + n;
            RegisterActor(key, c);
            if (_installed)
                EmitActor(key, c, null);
            return key;
        }

        /// <summary>The key of <paramref name="c"/> if the trace already knows it, else null (never registers; for status writes, which are timed by the wall clock).</summary>
        public string TryKeyOf(CharacterController c)
            => c != null && _keys.TryGetValue(c, out string key) ? key : null;

        public CharacterController ControllerOf(string key)
        {
            foreach (KeyValuePair<CharacterController, string> kv in _keys)
                if (kv.Value == key)
                    return kv.Key;
            return null;
        }

        // ── Emission ────────────────────────────────────────────────────

        /// <summary>Appends an event (even when closed; callers check <see cref="Closed"/> for game events).</summary>
        public TraceEvent Emit(string ev)
        {
            var e = new TraceEvent
            {
                Seq = _events.Count + 1,
                Frame = Time.frameCount - _startFrame,
                Round = _gm != null ? _gm.CurrentRound : 0,
                Turn = _turn,
                Ev = ev
            };
            _events.Add(e);
            if (ev != "log" && ev != "hp" && ev != "nl" && ev != "dice")
                MarkProgress();
            return e;
        }

        private void MarkProgress()
        {
            LastProgressFrame = Time.frameCount;
            LastProgressTime = Time.time;
        }

        public void EmitMeta(string mode)
        {
            Emit("meta")
                .Set("scenario", _job.Def.Id)
                .Set("title", _job.Def.Title)
                .Set("seed", _job.Seed)
                .Set("rep", _job.Rep)
                .Set("mode", mode)
                .Set("harness", HarnessVersion)
                .Set("unity", Application.unityVersion)
                .Set("gitHead", ReadGitHead())
                .Set("utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture))
                .Set("covers", _job.Def.Covers)
                .Set("tags", _job.Def.Tags);
        }

        public void EmitActor(string key, CharacterController c, ActorSpec spec)
        {
            CharacterStats s = c != null ? c.Stats : null;
            if (s == null)
                return;
            ItemData weapon = null;
            try { weapon = c.GetEquippedMainWeapon(); } catch (Exception) { weapon = null; }
            Emit("actor")
                .Set("key", key)
                .Set("name", s.CharacterName)
                .Set("team", c.Team)
                .Set("control", spec != null ? spec.Control.ToString().ToLowerInvariant() : (c.IsControllable ? "ui" : "ai"))
                .Set("controllable", c.IsControllable)
                .Set("source", spec != null ? spec.Source.ToString() : "spawned")
                .Set("cls", s.CharacterClass)
                .Set("level", s.Level)
                .Set("hp", s.CurrentHP)
                .Set("maxHp", s.TotalMaxHP)
                .Set("ac", s.ArmorClass)
                .Set("touch", s.TouchArmorClass)
                .Set("bab", s.BaseAttackBonus)
                .Set("pos", c.GridPosition)
                .Set("size", s.CurrentSizeCategory)
                .Set("speed", s.EffectiveSpeedFeet)
                .Set("profile", c.aiProfile != null ? c.aiProfile.GetType().Name : null)
                .Set("weapon", weapon != null ? weapon.Name : null);
        }

        /// <summary>Per-actor state for snapshots: hp, nonlethal, position, sorted conditions, HP state, grappling, team.</summary>
        public JsonObj Snapshot(CharacterController c)
        {
            var o = new JsonObj();
            o.Set("k", KeyOf(c));
            if (c == null || c.Stats == null)
                return o.Set("gone", true);
            o.Set("hp", c.Stats.CurrentHP)
             .Set("nl", c.Stats.NonlethalDamage)
             .Set("pos", c.GridPosition)
             .Set("conds", ConditionNames(c))
             .Set("st", c.CurrentHPState)
             .Set("gr", SafeIsGrappling(c))
             .Set("team", c.Team);
            if (c.gameObject == null || !c.gameObject.activeInHierarchy)
                o.Set("inactive", true);
            return o;
        }

        public static List<string> ConditionNames(CharacterController c)
        {
            var names = new List<string>();
            try
            {
                List<StatusEffect> conds = c.GetActiveConditions();
                if (conds != null)
                    foreach (StatusEffect e in conds)
                        if (e != null && !names.Contains(e.Type.ToString()))
                            names.Add(e.Type.ToString());
            }
            catch (Exception)
            {
                names.Add("?");
            }
            names.Sort(StringComparer.Ordinal);
            return names;
        }

        private static bool SafeIsGrappling(CharacterController c)
        {
            try { return c.IsGrappling(); } catch (Exception) { return false; }
        }

        public List<JsonObj> SnapshotAll()
        {
            var list = new List<JsonObj>();
            foreach (CharacterController c in _order)
                list.Add(Snapshot(c));
            return list;
        }

        // ── Install / uninstall ────────────────────────────────────────

        public void Install()
        {
            ScenarioHooks.AttackResolved = ScenarioHooks.Safe<CharacterController, CombatResult>("Trace.Attack", OnAttack);
            ScenarioHooks.AoOResolved = ScenarioHooks.Safe<CharacterController, CharacterController, string, CombatResult>("Trace.AoO", OnAoO);
            ScenarioHooks.ManeuverResolved = ScenarioHooks.Safe<CharacterController, CharacterController, SpecialAttackType, SpecialAttackResult>("Trace.Maneuver", OnManeuver);
            ScenarioHooks.ThreatenedCast = ScenarioHooks.Safe<CharacterController, SpellData, bool, bool, int>("Trace.ThreatenedCast", OnThreatenedCast);
            ScenarioHooks.Moved = ScenarioHooks.Safe<CharacterController, Vector2Int, Vector2Int, string>("Trace.Moved", OnMoved);
            ScenarioHooks.ConditionChanged = ScenarioHooks.Safe<CharacterController, CombatConditionType, bool, int, string>("Trace.Condition", OnCondition);
            ScenarioHooks.CombatLog = ScenarioHooks.Safe<string>("Trace.Log", OnLog);
            ScenarioHooks.PhaseChanged = ScenarioHooks.Safe<GameManager.TurnPhase, GameManager.TurnPhase>("Trace.Phase", OnPhase);

            _onTurnStarted = e => Guard("Trace.TurnStarted", () => OnTurnStarted(e));
            _onTurnEnded = e => Guard("Trace.TurnEnded", () => OnTurnEnded(e));
            _onNewRound = e => Guard("Trace.NewRound", () => OnNewRound(e));
            GameEventSystem.Instance.Subscribe(_onTurnStarted);
            GameEventSystem.Instance.Subscribe(_onTurnEnded);
            GameEventSystem.Instance.Subscribe(_onNewRound);
            _installed = true;
        }

        public void Uninstall()
        {
            if (_onTurnStarted != null) GameEventSystem.Instance.Unsubscribe(_onTurnStarted);
            if (_onTurnEnded != null) GameEventSystem.Instance.Unsubscribe(_onTurnEnded);
            if (_onNewRound != null) GameEventSystem.Instance.Unsubscribe(_onNewRound);
            _onTurnStarted = null;
            _onTurnEnded = null;
            _onNewRound = null;

            foreach (KeyValuePair<CharacterController, Action<int, int>> kv in _hpHandlers)
                if (kv.Key != null && kv.Key.Stats != null)
                    kv.Key.Stats.CurrentHPChanged -= kv.Value;
            foreach (KeyValuePair<CharacterController, Action<int, int>> kv in _nlHandlers)
                if (kv.Key != null && kv.Key.Stats != null)
                    kv.Key.Stats.NonlethalDamageChanged -= kv.Value;
            _hpHandlers.Clear();
            _nlHandlers.Clear();
            _installed = false;
        }

        private void Guard(string name, Action body)
        {
            try
            {
                body();
            }
            catch (Exception ex)
            {
                ScenarioHooks.HookErrors.Add(name + ": " + ex.GetType().Name + ": " + ex.Message);
                Debug.LogException(ex);
            }
        }

        private void SubscribeHp(CharacterController c)
        {
            if (c == null || c.Stats == null || _hpHandlers.ContainsKey(c))
                return;
            CharacterController captured = c;
            Action<int, int> hp = (o, n) => Guard("Trace.Hp", () =>
            {
                if (!Closed)
                    Emit("hp").Set("actor", KeyOf(captured)).Set("old", o).Set("new", n);
            });
            Action<int, int> nl = (o, n) => Guard("Trace.Nl", () =>
            {
                if (!Closed)
                    Emit("nl").Set("actor", KeyOf(captured)).Set("old", o).Set("new", n);
            });
            c.Stats.CurrentHPChanged += hp;
            c.Stats.NonlethalDamageChanged += nl;
            _hpHandlers[c] = hp;
            _nlHandlers[c] = nl;
        }

        // ── Handlers ────────────────────────────────────────────────────

        private void OnNewRound(NewRoundEvent e)
        {
            if (Closed)
                return;
            if (!_initEmitted)
                EmitInit();
            Emit("round").Set("n", e.RoundNumber);
            _job.Checks.OnNewRound(e.RoundNumber);
        }

        private void EmitInit()
        {
            _initEmitted = true;
            var order = new List<JsonObj>();
            TurnService ts = _gm != null ? _gm.GetComponent<TurnService>() : null;
            if (ts != null && ts.InitiativeOrder != null)
            {
                foreach (TurnService.InitiativeEntry entry in ts.InitiativeOrder)
                {
                    var o = new JsonObj();
                    o.Set("actor", KeyOf(entry.Character)).Set("roll", entry.Roll).Set("mod", entry.Modifier).Set("total", entry.Total);
                    order.Add(o);
                }
            }
            Emit("init").Set("order", order).Set("count", order.Count);
            _job.InitiativeCount = order.Count;
        }

        private void OnTurnStarted(TurnStartedEvent e)
        {
            if (Closed || e.Character == null)
                return;
            _turn++;
            CharacterController a = e.Character;
            string controller = _job.ControllerLabel(a);
            List<JsonObj> snaps = SnapshotAll();
            Emit("turn_start").Set("actor", KeyOf(a)).Set("controller", controller).Set("snap", snaps);
            _turnStartSnap.Clear();
            foreach (CharacterController c in _order)
                _turnStartSnap[c] = Snapshot(c);
            _job.Checks.OnTurnStart(a, snaps);
            _job.OnTurnStarted(a, controller);
        }

        private void OnTurnEnded(TurnEndedEvent e)
        {
            if (Closed || e.Character == null)
                return;
            CharacterController a = e.Character;
            ActionEconomy act = a.Actions;
            var econ = new JsonObj();
            if (act != null)
            {
                econ.Set("std", act.StandardActionUsed)
                    .Set("move", act.MoveActionUsed)
                    .Set("full", act.FullRoundActionUsed)
                    .Set("conv", act.StandardConvertedToMove)
                    .Set("five", act.HasMoved5Ft)
                    .Set("single", act.SingleActionOnly)
                    .Set("swift", act.SwiftActionUsed);
            }

            var diff = new List<JsonObj>();
            foreach (CharacterController c in _order)
            {
                JsonObj now = Snapshot(c);
                if (_turnStartSnap.TryGetValue(c, out JsonObj before) && Json.Serialize(before) == Json.Serialize(now))
                    continue;
                diff.Add(now);
            }

            TraceEvent ev = Emit("turn_end").Set("actor", KeyOf(a)).Set("econ", econ).Set("diff", diff);
            _job.Checks.OnTurnEnd(a, ev);
        }

        private void OnAttack(CharacterController attacker, CombatResult r)
        {
            if (Closed || r == null)
                return;
            CharacterController target = r.Defender;
            TraceEvent ev = Emit("attack")
                .Set("attacker", KeyOf(attacker))
                .Set("target", KeyOf(target))
                .Set("die", r.DieRoll)
                .Set("total", r.TotalRoll)
                .Set("mod", r.TotalRoll - r.DieRoll)
                .Set("ac", r.TargetAC)
                .Set("hit", r.Hit)
                .Set("dmg", r.Hit ? r.TotalDamage : 0)
                .Set("nat20", r.NaturalTwenty)
                .Set("nat1", r.NaturalOne)
                .Set("threat", r.IsCritThreat)
                .Set("confirmed", r.CritConfirmed)
                .Set("aoo", r.IsAttackOfOpportunity)
                .Set("flank", r.IsFlanking)
                .Set("flankBonus", r.FlankingBonus)
                .Set("sneak", r.SneakAttackApplied)
                .Set("ranged", r.IsRangedAttack)
                .Set("weapon", r.WeaponName)
                .Set("conceal", r.MissedDueToConcealment)
                .Set("barrier", r.ProtectionSummonedBarrierBlocked)
                .Set("deflected", IsDeflected(r))
                .Set("attackerDown", ScenarioChecks.IsDown(attacker));
            _attackByResult[r] = ev;
            _job.Checks.OnAttack(attacker, ev);
        }

        /// <summary>
        /// A ranged hit negated by Deflect Arrows (PHB p.93). CharacterController sets Hit = false and only leaves a
        /// note ("... deflected ..."), with no CombatResult flag, so the note is matched.
        /// </summary>
        private static bool IsDeflected(CombatResult r)
            => r.IsRangedAttack && !r.Hit && !string.IsNullOrEmpty(r.SpecialAttackNote)
               && r.SpecialAttackNote.IndexOf("deflected", StringComparison.OrdinalIgnoreCase) >= 0;

        private void OnAoO(CharacterController threatener, CharacterController target, string trigger, CombatResult r)
        {
            if (Closed)
                return;
            int attackSeq = 0;
            if (r != null && _attackByResult.TryGetValue(r, out TraceEvent atk))
            {
                atk.Set("aoo", true);
                atk.Set("trigger", trigger);
                attackSeq = atk.Seq;
            }
            TraceEvent ev = Emit("aoo")
                .Set("by", KeyOf(threatener))
                .Set("target", KeyOf(target))
                .Set("trigger", trigger)
                .Set("hit", r != null && r.Hit)
                .Set("attackSeq", attackSeq);
            _job.Checks.OnAoO(threatener, ev);
        }

        private void OnManeuver(CharacterController attacker, CharacterController target, SpecialAttackType type, SpecialAttackResult r)
        {
            if (Closed)
                return;
            TraceEvent ev = Emit("maneuver")
                .Set("by", KeyOf(attacker))
                .Set("target", KeyOf(target))
                .Set("type", type)
                .Set("success", r != null && r.Success)
                .Set("check", r != null ? r.CheckTotal : 0)
                .Set("checkRoll", r != null ? r.CheckRoll : 0)
                .Set("opposed", r != null ? r.OpposedTotal : 0)
                .Set("opposedRoll", r != null ? r.OpposedRoll : 0)
                .Set("provoked", r != null && r.ProvokedAoO)
                .Set("consumed", r == null || r.AttackerActionConsumed)
                .Set("attackerDown", ScenarioChecks.IsDown(attacker));
            _job.Checks.OnActed(attacker, ev, "maneuver");
        }

        private void OnThreatenedCast(CharacterController caster, SpellData spell, bool defensive, bool success, int threateners)
        {
            if (Closed)
                return;
            TraceEvent ev = Emit("threatened_cast")
                .Set("by", KeyOf(caster))
                .Set("spell", spell != null ? spell.SpellId : null)
                .Set("defensive", defensive)
                .Set("success", success)
                .Set("threateners", threateners)
                // The hook fires after the AoOs the cast provoked. An AoO that drops the caster correctly loses the
                // spell (success false), so only a spell that still goes off with the caster down is a violation.
                .Set("casterDownAfter", ScenarioChecks.IsDown(caster))
                .Set("attackerDown", success && ScenarioChecks.IsDown(caster));
            _job.Checks.OnActed(caster, ev, "cast");
        }

        private void OnMoved(CharacterController mover, Vector2Int from, Vector2Int to, string type)
        {
            if (Closed)
                return;
            int squares = SquareGridUtils.GetDistance(from, to);
            TraceEvent ev = Emit("move")
                .Set("actor", KeyOf(mover))
                .Set("from", from)
                .Set("to", to)
                .Set("type", type)
                .Set("sq", squares)
                .Set("prone", mover != null && mover.HasCondition(CombatConditionType.Prone));
            _job.Checks.OnMove(mover, ev);
        }

        private void OnCondition(CharacterController target, CombatConditionType type, bool added, int rounds, string source)
        {
            if (Closed)
                return;
            Emit("cond").Set("actor", KeyOf(target)).Set("type", type).Set("added", added).Set("rounds", rounds).Set("source", source);
        }

        private void OnLog(string message)
        {
            if (Closed)
                return;
            string text = TagRegex.Replace(message ?? "", "").Trim();
            Emit("log").Set("text", text);
            if (text != _lastLog)
                MarkProgress();
            _lastLog = text;
        }

        private void OnPhase(GameManager.TurnPhase from, GameManager.TurnPhase to)
        {
            if (_job.Halting)
                return;
            List<string> by = null;
            if (to == GameManager.TurnPhase.CombatOver)
                by = CallerFrames(3);
            if (!Closed)
            {
                TraceEvent ev = Emit("phase").Set("from", from).Set("to", to);
                if (by != null)
                    ev.Set("by", by);
            }
            _job.Checks.OnPhaseChanged(from, to, by);
        }

        /// <summary>The first <paramref name="count"/> GameManager frames above the CurrentPhase setter.</summary>
        private static List<string> CallerFrames(int count)
        {
            var result = new List<string>();
            var st = new System.Diagnostics.StackTrace(1, false);
            for (int i = 0; i < st.FrameCount && result.Count < count; i++)
            {
                System.Reflection.MethodBase m = st.GetFrame(i)?.GetMethod();
                if (m == null || m.DeclaringType == null)
                    continue;
                Type t = m.DeclaringType;
                // Iterator and lambda bodies are nested in GameManager; report their owning method name.
                Type owner = t;
                while (owner.DeclaringType != null)
                    owner = owner.DeclaringType;
                if (owner != typeof(GameManager))
                    continue;
                string name = m.Name;
                if (name == "set_CurrentPhase")
                    continue;
                if (t != owner)
                {
                    // <SingleNPCTurnFromInitiative>d__12.MoveNext -> SingleNPCTurnFromInitiative
                    Match match = Regex.Match(t.Name, "<([^>]+)>");
                    name = match.Success ? match.Groups[1].Value : t.Name + "." + name;
                }
                result.Add(name);
            }
            return result;
        }

        /// <summary>Dice record (RecordDice or a forced die).</summary>
        public void EmitDice(int sides, string ctx, int natural, int value, bool forced)
        {
            if (Closed)
                return;
            Emit("dice").Set("sides", sides).Set("ctx", ctx).Set("natural", natural).Set("value", value).Set("forced", forced);
        }

        // ── Output ──────────────────────────────────────────────────────

        public List<string> HashLines()
        {
            var lines = new List<string>(_events.Count);
            foreach (TraceEvent e in _events)
                if (e.Ev != "watchdog") // editor pauses and other host events depend on timing, not on the game
                    lines.Add(e.ToJson(true));
            return lines;
        }

        public static string Hash(List<string> lines)
        {
            using (SHA1 sha = SHA1.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(string.Join("\n", lines));
                byte[] hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        public void WriteJsonl(string path)
        {
            var sb = new StringBuilder();
            foreach (TraceEvent e in _events)
                sb.Append(e.ToJson(false)).Append('\n');
            Json.WriteFileAtomic(path, sb.ToString());
        }

        public List<string> LastLines(int n)
        {
            var lines = new List<string>();
            for (int i = Math.Max(0, _events.Count - n); i < _events.Count; i++)
                lines.Add(_events[i].ToJson(false));
            return lines;
        }

        private static string ReadGitHead()
        {
            try
            {
                string root = Directory.GetParent(Application.dataPath).FullName;
                string git = Path.Combine(root, ".git");
                string head = File.ReadAllText(Path.Combine(git, "HEAD")).Trim();
                if (!head.StartsWith("ref: ", StringComparison.Ordinal))
                    return head;
                string refName = head.Substring(5).Trim();
                string refPath = Path.Combine(git, refName.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(refPath))
                    return File.ReadAllText(refPath).Trim();
                string packed = Path.Combine(git, "packed-refs");
                if (File.Exists(packed))
                    foreach (string line in File.ReadAllLines(packed))
                        if (line.EndsWith(" " + refName, StringComparison.Ordinal))
                            return line.Substring(0, line.IndexOf(' '));
                return refName;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
#endif
