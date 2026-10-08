#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Tests.Scenarios
{
    /// <summary>
    /// Who decides an actor's turns in a scenario.
    /// Ai: the game AI (AIService.ExecuteNPCTurn); a PC-slot actor is made non-controllable and gets a class AI profile.
    /// Scripted: the AI turn path up to the compulsion gates, then the def's typed steps for that actor and round
    /// (ScenarioDef.TurnScripts), else its coroutine script (ScenarioDef.Scripts), else an empty turn (or the AI, for
    /// an actor listed in ScenarioDef.AiWhenUnscripted).
    /// Ui: a controllable PC driven through the PC menu callbacks by its typed steps for the round; End Turn is pressed
    /// when the steps run out (at once when there are none).
    /// Idle: the AI turn path with an empty turn (the actor takes no actions).
    /// </summary>
    public enum Control { Ai, Scripted, Ui, Idle }

    /// <summary>How a scenario job ended. None while it runs.</summary>
    public enum Outcome
    {
        None,
        Victory,
        Defeat,
        VictoryUndetected,
        DefeatUndetected,
        Stalemate,
        SoftLock,
        SkipChainRunaway,
        Exception,
        Paused,
        Timeout,
        SetupFailed,
        Contaminated,
        Halted
    }

    /// <summary>The judgement on one job (see docs/TESTING.md 3.4).</summary>
    public enum Verdict { Pass, Fail, Known, XFail, XPass, Inconclusive, Error }

    public enum ActorSourceKind { QuickStart, Stats, Npc }

    /// <summary>Where an actor comes from: a class Quick Start character, an exactly built CharacterStats, or an NPC database id.</summary>
    public sealed class ActorSource
    {
        public ActorSourceKind Kind { get; private set; }
        public string ClassName { get; private set; }
        public Func<CharacterStats> Build { get; private set; }
        public string NpcId { get; private set; }
        /// <summary>Optional stable name for the trace (an Npc source whose id is generated per job, e.g. a DMG spawn id).</summary>
        public string Label { get; private set; }

        /// <summary>True for QuickStart and Stats actors, which take a party (PC) slot.</summary>
        public bool IsPcSlot => Kind != ActorSourceKind.Npc;

        public static ActorSource QuickStart(string className)
            => new ActorSource { Kind = ActorSourceKind.QuickStart, ClassName = className };

        /// <summary>
        /// An exactly built character for a PC slot. Set BAB through BaseAttackBonusOverride; writes to
        /// BaseAttackBonus are ignored for classed characters (CHR-068). The function runs once per job.
        /// </summary>
        public static ActorSource Stats(Func<CharacterStats> build)
            => new ActorSource { Kind = ActorSourceKind.Stats, Build = build };

        public static ActorSource Npc(string npcId)
            => new ActorSource { Kind = ActorSourceKind.Npc, NpcId = npcId };

        /// <summary>
        /// An NPC id registered for this job only (for example a DungeonEncounterSpawner "spawn_..." id, whose counter
        /// differs between sessions). The trace shows <paramref name="label"/> instead of the id, so hashes stay comparable.
        /// </summary>
        public static ActorSource Npc(string npcId, string label)
            => new ActorSource { Kind = ActorSourceKind.Npc, NpcId = npcId, Label = label };

        public override string ToString()
        {
            switch (Kind)
            {
                case ActorSourceKind.QuickStart: return "quickstart:" + ClassName;
                case ActorSourceKind.Stats: return "stats";
                default: return "npc:" + (Label ?? NpcId);
            }
        }

        /// <summary>The classes that have a Quick Start character (the class files' GetQuickStartCharacter).</summary>
        public static readonly string[] QuickStartClasses =
            { "Fighter", "Rogue", "Cleric", "Wizard", "Monk", "Barbarian", "Sorcerer", "Ranger", "Paladin", "Bard", "Druid" };

        /// <summary>A new Quick Start creation record for <paramref name="className"/>, or null for an unknown class.</summary>
        public static CharacterCreationData CreateQuickStart(string className)
        {
            switch (className)
            {
                case "Fighter": return FighterClass.GetQuickStartCharacter();
                case "Rogue": return RogueClass.GetQuickStartCharacter();
                case "Cleric": return ClericClass.GetQuickStartCharacter();
                case "Wizard": return WizardClass.GetQuickStartCharacter();
                case "Monk": return MonkClass.GetQuickStartCharacter();
                case "Barbarian": return BarbarianClass.GetQuickStartCharacter();
                case "Sorcerer": return SorcererClass.GetQuickStartCharacter();
                case "Ranger": return RangerClass.GetQuickStartCharacter();
                case "Paladin": return PaladinClass.GetQuickStartCharacter();
                case "Bard": return BardClass.GetQuickStartCharacter();
                case "Druid": return DruidClass.GetQuickStartCharacter();
                default: return null;
            }
        }
    }

    /// <summary>One combatant of a scenario.</summary>
    public sealed class ActorSpec
    {
        /// <summary>Stable key used in the trace and in expectations (unique per scenario).</summary>
        public string Key;
        public CharacterTeam Team = CharacterTeam.Player;
        public ActorSource Source;
        public Vector2Int Pos;
        public Control Control = Control.Ai;
        /// <summary>
        /// Optional AI profile override for an AI-run PC-slot actor (default: AiProfileForClass). Return a fresh
        /// ScriptableObject.CreateInstance: the harness destroys it after the job. An asset, or an instance another
        /// controller already uses, is assigned but never destroyed.
        /// </summary>
        public Func<DND35.AI.AIProfile> Profile;
        /// <summary>Optional key of the enemy this actor's AI should prefer (CharacterController.PriorityTargetName).</summary>
        public string PriorityTarget;
        public List<KeyValuePair<CombatConditionType, int>> StartConditions = new List<KeyValuePair<CombatConditionType, int>>();
        public int? Hp;
        public Action<CharacterController> Tweak;
    }

    /// <summary>
    /// Forces dice through ScenarioHooks.RollFilter: the first force whose Sides match and whose CtxContains
    /// (ordinal substring of the DiceService context; null matches any roll, including context-free DiceRoller dice)
    /// matches replaces the natural roll with Value. Remaining counts down per use; -1 never runs out.
    /// </summary>
    public sealed class DiceForce
    {
        public int Sides;
        public int Value;
        public string CtxContains;
        public int Remaining = 1;

        public DiceForce Clone() => new DiceForce { Sides = Sides, Value = Value, CtxContains = CtxContains, Remaining = Remaining };

        public bool Matches(int sides, string ctx)
        {
            if (Remaining == 0 || sides != Sides)
                return false;
            if (string.IsNullOrEmpty(CtxContains))
                return true;
            return ctx != null && ctx.IndexOf(CtxContains, StringComparison.Ordinal) >= 0;
        }
    }

    /// <summary>
    /// Accepts a known violation: an invariant id (or "*") and an optional regex on its detail, tied to the
    /// issue that explains it. Waived violations make a job Known instead of Fail; unused waivers are reported.
    /// </summary>
    public sealed class Waiver
    {
        public string IssueId;
        public string Invariant;
        public string DetailRegex;

        private Regex _regex;

        public bool Matches(string invariant, string detail)
        {
            if (Invariant != "*" && !string.Equals(Invariant, invariant, StringComparison.Ordinal))
                return false;
            if (string.IsNullOrEmpty(DetailRegex))
                return true;
            if (_regex == null)
                _regex = new Regex(DetailRegex, RegexOptions.CultureInvariant);
            return _regex.IsMatch(detail ?? "");
        }

        public override string ToString() => IssueId + ":" + Invariant + (string.IsNullOrEmpty(DetailRegex) ? "" : "/" + DetailRegex);
    }

    /// <summary>A named check over the finished trace. A non-null XFailIssue marks it as expected to fail until that issue is fixed.</summary>
    public sealed class Expectation
    {
        public string Name;
        public Func<TraceView, ExpectResult> Check;
        public string XFailIssue;
    }

    /// <summary>
    /// Actors a scenario generates per job (<see cref="ScenarioDef.GenerateActors"/>), for example a DMG random
    /// encounter: the actors, an optional cleanup run after the job's reset, and a description for the trace
    /// ("encounter" event) and the job result.
    /// </summary>
    public sealed class GeneratedActors
    {
        public List<ActorSpec> Actors = new List<ActorSpec>();
        /// <summary>Runs in the job's cleanup, after the world reset (for example to unregister per-job NPC ids).</summary>
        public Action Cleanup;
        /// <summary>What was generated (name, level, creature list); written to the trace and to the job result.</summary>
        public JsonObj Info = new JsonObj();
    }

    /// <summary>A scenario: actors on exact squares, who controls them, forced dice, expectations and waivers.</summary>
    public sealed class ScenarioDef
    {
        public string Id;
        public string Title;
        public List<string> Tags = new List<string>();
        /// <summary>Issue IDs and rule cites the scenario covers.</summary>
        public List<string> Covers = new List<string>();
        public int MaxRounds = 12;
        public List<ActorSpec> Actors = new List<ActorSpec>();
        /// <summary>Actor keys forced to the head of initiative in this order (null: rolled). List every actor for an exact order.</summary>
        public List<string> InitiativeOrder;
        /// <summary>Turn scripts for Scripted actors, by key: (context, actor) returns the turn coroutine. None means an empty turn.</summary>
        public Dictionary<string, Func<ScenarioContext, CharacterController, IEnumerator>> Scripts =
            new Dictionary<string, Func<ScenarioContext, CharacterController, IEnumerator>>();
        /// <summary>Typed steps per (actor, round) for Scripted and Ui actors (ScenarioSteps.cs). Round 0 means every round without its own script.</summary>
        public List<TurnScript> TurnScripts = new List<TurnScript>();
        /// <summary>Scripted actors whose rounds without a typed script or coroutine are run by the AI instead of being empty.</summary>
        public HashSet<string> AiWhenUnscripted = new HashSet<string>();
        public List<DiceForce> DiceForces = new List<DiceForce>();
        public List<Expectation> Expectations = new List<Expectation>();
        public List<Waiver> Waivers = new List<Waiver>();
        public bool RecordDice;
        public Action<ScenarioContext> OnSetup;
        /// <summary>
        /// Optional per-job actors, generated at setup right after the RNG is seeded with the job seed (so the same
        /// seed gives the same actors) and before anything spawns. They join <see cref="Actors"/> in a per-job copy of
        /// this definition, which is validated again (keys, squares, slot limits). Their keys must not be named by
        /// static parts of the definition (scripts, initiative), which are validated before generation.
        /// </summary>
        public Func<ScenarioContext, GeneratedActors> GenerateActors;
        /// <summary>Wall-clock cap per job in seconds when the run options do not set wallCap (null: the 120 s default).</summary>
        public float? WallCapSeconds;
        /// <summary>
        /// How the runner answers a controllable actor's counter-trip prompt (PHB p.158, CMB-079), by actor key:
        /// true trips back, false declines. An actor not listed trips back.
        /// </summary>
        public Dictionary<string, bool> CounterTripAnswers = new Dictionary<string, bool>();

        /// <summary>A copy for one job with <paramref name="extra"/> actors appended (the lists are new, the rest is shared).</summary>
        internal ScenarioDef WithExtraActors(IEnumerable<ActorSpec> extra)
        {
            var d = (ScenarioDef)MemberwiseClone();
            d.Actors = new List<ActorSpec>(Actors);
            if (extra != null)
                d.Actors.AddRange(extra);
            d.GenerateActors = null;
            return d;
        }

        /// <summary>The typed script for <paramref name="key"/> in <paramref name="round"/> (a round-0 script applies to every round without its own), or null.</summary>
        public TurnScript FindTurn(string key, int round)
        {
            TurnScript any = null;
            foreach (TurnScript t in TurnScripts)
            {
                if (t.Actor != key)
                    continue;
                if (t.Round == round)
                    return t;
                if (t.Round == 0 && any == null)
                    any = t;
            }
            return any;
        }

        public ActorSpec Find(string key)
        {
            for (int i = 0; i < Actors.Count; i++)
                if (Actors[i].Key == key)
                    return Actors[i];
            return null;
        }

        /// <summary>Checks the definition; returns the problems (empty when valid).</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            if (string.IsNullOrWhiteSpace(Id))
                problems.Add("missing id");
            if (MaxRounds < 1)
                problems.Add("MaxRounds must be at least 1");
            if (WallCapSeconds.HasValue && WallCapSeconds.Value < 5f)
                problems.Add("WallCapSeconds must be at least 5");

            var keys = new HashSet<string>();
            var squares = new Dictionary<Vector2Int, string>();
            int pcSlots = 0, pool = 0;
            foreach (ActorSpec a in Actors)
            {
                if (a == null || string.IsNullOrWhiteSpace(a.Key))
                {
                    problems.Add("actor without a key");
                    continue;
                }
                if (!keys.Add(a.Key))
                    problems.Add("duplicate key " + a.Key);
                if (a.Source == null)
                {
                    problems.Add(a.Key + ": no source");
                    continue;
                }

                if (a.Source.IsPcSlot)
                {
                    pcSlots++;
                    if (a.Source.Kind == ActorSourceKind.QuickStart && Array.IndexOf(ActorSource.QuickStartClasses, a.Source.ClassName) < 0)
                        problems.Add(a.Key + ": no Quick Start character for class '" + a.Source.ClassName + "'");
                    if (a.Source.Kind == ActorSourceKind.Stats && a.Source.Build == null)
                        problems.Add(a.Key + ": Stats source without a build function");
                }
                else
                {
                    pool++;
                    if (string.IsNullOrWhiteSpace(a.Source.NpcId))
                        problems.Add(a.Key + ": Npc source without an id");
                    if (a.Control == Control.Ui)
                        problems.Add(a.Key + ": Ui control is only for PC-slot (QuickStart or Stats) actors");
                }

                if (a.Source.IsPcSlot && a.Team != CharacterTeam.Player)
                    problems.Add(a.Key + ": PC-slot actors are on the Player team");
                if (a.Pos.x < 0 || a.Pos.y < 0 || a.Pos.x >= ScenarioLimits.GridWidth || a.Pos.y >= ScenarioLimits.GridHeight)
                    problems.Add(a.Key + ": position " + a.Pos + " is off the " + ScenarioLimits.GridWidth + "x" + ScenarioLimits.GridHeight + " grid");
                if (squares.TryGetValue(a.Pos, out string other))
                    problems.Add(a.Key + ": position " + a.Pos + " is taken by " + other);
                else
                    squares[a.Pos] = a.Key;
                if (!string.IsNullOrEmpty(a.PriorityTarget) && Find(a.PriorityTarget) == null)
                    problems.Add(a.Key + ": priority target '" + a.PriorityTarget + "' is not an actor");
            }

            if (pcSlots > ScenarioLimits.PcSlots)
                problems.Add("at most " + ScenarioLimits.PcSlots + " QuickStart or Stats actors (PC slots), got " + pcSlots);
            if (pool > ScenarioLimits.PoolSlots)
                problems.Add("at most " + ScenarioLimits.PoolSlots + " Npc actors (pool slots), got " + pool);

            if (InitiativeOrder != null)
            {
                foreach (string k in InitiativeOrder)
                    if (Find(k) == null)
                        problems.Add("initiative key '" + k + "' is not an actor");
            }

            foreach (string k in CounterTripAnswers.Keys)
                if (Find(k) == null)
                    problems.Add("counter-trip answer for unknown actor '" + k + "'");

            foreach (string k in Scripts.Keys)
            {
                ActorSpec a = Find(k);
                if (a == null)
                    problems.Add("script for unknown actor '" + k + "'");
                else if (a.Control != Control.Scripted)
                    problems.Add("script for '" + k + "', whose control is " + a.Control);
            }

            var turnKeys = new HashSet<string>();
            foreach (TurnScript t in TurnScripts)
            {
                ActorSpec a = t != null ? Find(t.Actor) : null;
                if (a == null)
                {
                    problems.Add("turn script for unknown actor '" + (t != null ? t.Actor : "null") + "'");
                    continue;
                }
                if (a.Control != Control.Scripted && a.Control != Control.Ui)
                    problems.Add("turn script for '" + t.Actor + "', whose control is " + a.Control + " (Scripted or Ui only)");
                if (t.Round < 0)
                    problems.Add("turn script for '" + t.Actor + "' has round " + t.Round);
                if (!turnKeys.Add(t.Actor + "|" + t.Round))
                    problems.Add("two turn scripts for '" + t.Actor + "' in round " + t.Round);
                foreach (Step step in t.Steps)
                {
                    string problem = step == null ? "null step" : step.Problem(a.Control, this);
                    if (problem != null)
                        problems.Add(t.Actor + " r" + t.Round + ": " + problem);
                }
            }
            foreach (string k in AiWhenUnscripted)
            {
                ActorSpec a = Find(k);
                if (a == null || a.Control != Control.Scripted)
                    problems.Add("AiWhenUnscripted '" + k + "' is not a Scripted actor");
                else if (TurnScripts.Exists(t => t != null && t.Actor == k && t.Round == 0) || Scripts.ContainsKey(k))
                    problems.Add("AiWhenUnscripted '" + k + "' has a round-0 turn script or a coroutine script, so the AI never runs");
            }

            foreach (DiceForce f in DiceForces)
                if (f.Sides < 2 || f.Value < 1 || f.Value > f.Sides)
                    problems.Add("dice force d" + f.Sides + "=" + f.Value + " is out of range");

            return problems;
        }
    }

    /// <summary>Fixed sizes the scenario model validates against (the MainScene grid and party/pool sizes).</summary>
    public static class ScenarioLimits
    {
        public const int GridWidth = 20;
        public const int GridHeight = 20;
        public const int PcSlots = 4;
        public const int PoolSlots = 15;
    }

    /// <summary>The live side of a job, passed to scripts and OnSetup.</summary>
    public sealed class ScenarioContext
    {
        public ScenarioDef Def { get; internal set; }
        public int Seed { get; internal set; }
        public int Rep { get; internal set; }
        public GameManager Gm { get; internal set; }
        internal ScenarioTrace Trace;
        internal readonly Dictionary<string, CharacterController> Actors = new Dictionary<string, CharacterController>();

        /// <summary>The controller for <paramref name="key"/>, or null.</summary>
        public CharacterController Get(string key)
            => key != null && Actors.TryGetValue(key, out CharacterController c) ? c : null;

        /// <summary>The trace key of <paramref name="c"/> (actor key, or a summon key).</summary>
        public string KeyOf(CharacterController c) => Trace != null ? Trace.KeyOf(c) : null;

        /// <summary>Adds a 'note' event to the trace (scripts use it to mark steps).</summary>
        public void Note(string text) => Trace?.Emit("note").Set("text", text);
    }

    /// <summary>Fluent builder: <c>Scenario.Define(id, title).Pc(...).Npc(...).Expect(...).Build()</c>.</summary>
    public sealed class ScenarioBuilder
    {
        private readonly ScenarioDef _def;

        internal ScenarioBuilder(string id, string title)
        {
            _def = new ScenarioDef { Id = id, Title = title ?? id };
        }

        public ScenarioBuilder Covers(params string[] ids) { _def.Covers.AddRange(ids); return this; }
        public ScenarioBuilder Tags(params string[] tags) { _def.Tags.AddRange(tags); return this; }
        public ScenarioBuilder MaxRounds(int rounds) { _def.MaxRounds = rounds; return this; }
        public ScenarioBuilder RecordDice(bool on = true) { _def.RecordDice = on; return this; }
        public ScenarioBuilder OnSetup(Action<ScenarioContext> setup) { _def.OnSetup = setup; return this; }
        /// <summary>Per-job actors generated after seeding (see <see cref="ScenarioDef.GenerateActors"/>).</summary>
        public ScenarioBuilder GenerateActors(Func<ScenarioContext, GeneratedActors> generate) { _def.GenerateActors = generate; return this; }
        /// <summary>The wall-clock cap per job when the run options do not set wallCap.</summary>
        public ScenarioBuilder WallCap(float seconds) { _def.WallCapSeconds = seconds; return this; }

        /// <summary>A party (PC-slot) actor from a QuickStart or Stats source.</summary>
        public ScenarioBuilder Pc(string key, ActorSource source, int x, int y, Control control = Control.Ai)
        {
            _def.Actors.Add(new ActorSpec { Key = key, Team = CharacterTeam.Player, Source = source, Pos = new Vector2Int(x, y), Control = control });
            return this;
        }

        /// <summary>A pool actor from the NPC database (Enemy team unless <paramref name="team"/> says otherwise).</summary>
        public ScenarioBuilder Npc(string key, string npcId, int x, int y, Control control = Control.Ai, CharacterTeam team = CharacterTeam.Enemy)
        {
            _def.Actors.Add(new ActorSpec { Key = key, Team = team, Source = ActorSource.Npc(npcId), Pos = new Vector2Int(x, y), Control = control });
            return this;
        }

        public ScenarioBuilder Actor(ActorSpec spec) { _def.Actors.Add(spec); return this; }

        public ScenarioBuilder StartCondition(string key, CombatConditionType type, int rounds)
            => With(key, a => a.StartConditions.Add(new KeyValuePair<CombatConditionType, int>(type, rounds)));

        public ScenarioBuilder Hp(string key, int hp) => With(key, a => a.Hp = hp);
        public ScenarioBuilder Tweak(string key, Action<CharacterController> tweak) => With(key, a => a.Tweak = tweak);
        public ScenarioBuilder Profile(string key, Func<DND35.AI.AIProfile> profile) => With(key, a => a.Profile = profile);
        public ScenarioBuilder PriorityTarget(string key, string targetKey) => With(key, a => a.PriorityTarget = targetKey);

        public ScenarioBuilder Script(string key, Func<ScenarioContext, CharacterController, IEnumerator> script)
        {
            _def.Scripts[key] = script;
            return this;
        }

        /// <summary>The typed steps <paramref name="actor"/> takes in <paramref name="round"/> (0: every round without its own script). The actor must be Scripted or Ui.</summary>
        public ScenarioBuilder Turn(string actor, int round, params Step[] steps)
        {
            _def.TurnScripts.Add(new TurnScript { Actor = actor, Round = round, Steps = new List<Step>(steps ?? new Step[0]) });
            return this;
        }

        /// <summary>Makes these actors Idle (the AI turn path with an empty turn).</summary>
        public ScenarioBuilder Idle(params string[] keys)
        {
            foreach (string k in keys)
                With(k, a => a.Control = Control.Idle);
            return this;
        }

        /// <summary>Rounds without a typed script or coroutine are run by the AI for these Scripted actors (instead of an empty turn).</summary>
        public ScenarioBuilder AiWhenUnscripted(params string[] keys)
        {
            foreach (string k in keys)
                _def.AiWhenUnscripted.Add(k);
            return this;
        }

        public ScenarioBuilder Initiative(params string[] keys) { _def.InitiativeOrder = new List<string>(keys); return this; }

        /// <summary>
        /// How the runner answers <paramref name="key"/>'s counter-trip prompt (a controllable defender after a failed
        /// trip, PHB p.158): true trips back (the default for every actor), false declines.
        /// </summary>
        public ScenarioBuilder CounterTripAnswer(string key, bool tripBack) { _def.CounterTripAnswers[key] = tripBack; return this; }

        /// <summary>Forces <paramref name="count"/> d<paramref name="sides"/> rolls (whose context contains <paramref name="ctx"/>, null for any) to <paramref name="value"/>; count -1 forces every one.</summary>
        public ScenarioBuilder Force(int sides, int value, string ctx = null, int count = 1)
        {
            _def.DiceForces.Add(new DiceForce { Sides = sides, Value = value, CtxContains = ctx, Remaining = count });
            return this;
        }

        public ScenarioBuilder Expect(string name, Func<TraceView, ExpectResult> check)
        {
            _def.Expectations.Add(new Expectation { Name = name, Check = check });
            return this;
        }

        /// <summary>An expectation that should fail until <paramref name="issue"/> is fixed; a pass is reported as XPASS.</summary>
        public ScenarioBuilder ExpectXFail(string issue, string name, Func<TraceView, ExpectResult> check)
        {
            _def.Expectations.Add(new Expectation { Name = name, Check = check, XFailIssue = issue });
            return this;
        }

        public ScenarioBuilder Waive(string issue, string invariant, string detailRegex = null)
        {
            _def.Waivers.Add(new Waiver { IssueId = issue, Invariant = invariant, DetailRegex = detailRegex });
            return this;
        }

        /// <summary>Validates and returns the definition; throws ArgumentException listing every problem.</summary>
        public ScenarioDef Build()
        {
            List<string> problems = _def.Validate();
            if (problems.Count > 0)
                throw new ArgumentException("Scenario '" + _def.Id + "' is invalid: " + string.Join("; ", problems));
            return _def;
        }

        private ScenarioBuilder With(string key, Action<ActorSpec> change)
        {
            ActorSpec a = _def.Find(key);
            if (a == null)
                throw new ArgumentException("Scenario '" + _def.Id + "': no actor '" + key + "' (declare it before configuring it)");
            change(a);
            return this;
        }
    }

    public static class Scenario
    {
        public static ScenarioBuilder Define(string id, string title) => new ScenarioBuilder(id, title);
    }

    /// <summary>
    /// Marks a <c>public static IEnumerable&lt;ScenarioDef&gt; Name()</c> method as a catalog source
    /// (files under Tests/Scenarios/Catalog). <see cref="ScenarioCatalog"/> finds them by reflection.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ScenarioSourceAttribute : Attribute { }

    /// <summary>Every catalog scenario, by id.</summary>
    public static class ScenarioCatalog
    {
        private static List<ScenarioDef> _all;
        private static readonly List<string> _loadErrors = new List<string>();

        /// <summary>Problems met while loading the catalog (a source that threw, duplicate ids).</summary>
        public static IReadOnlyList<string> LoadErrors { get { Load(); return _loadErrors; } }

        public static IReadOnlyList<ScenarioDef> All { get { Load(); return _all; } }

        /// <summary>Rebuilds the catalog on next use (definitions are built fresh).</summary>
        public static void Reload() { _all = null; }

        /// <summary>
        /// Builds one definition for a catalog source, recording a builder that throws (an invalid definition) as a
        /// load error and returning null (skipped), so one bad definition does not drop the rest of the source.
        /// Wrap every <c>yield return</c> of a source in it.
        /// </summary>
        public static ScenarioDef Safe(string source, Func<ScenarioDef> build)
        {
            try
            {
                return build();
            }
            catch (Exception ex)
            {
                _loadErrors.Add(source + ": " + ex.GetType().Name + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>Scenarios whose id matches any of the comma-separated globs (* and ?), in catalog order.</summary>
        public static List<ScenarioDef> Find(string glob)
        {
            var result = new List<ScenarioDef>();
            string[] parts = (string.IsNullOrWhiteSpace(glob) ? "*" : glob).Split(',');
            var regexes = new List<Regex>();
            foreach (string p in parts)
            {
                string t = p.Trim();
                if (t.Length == 0)
                    continue;
                regexes.Add(new Regex("^" + Regex.Escape(t).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.CultureInvariant));
            }

            foreach (ScenarioDef d in All)
            {
                foreach (Regex r in regexes)
                {
                    if (r.IsMatch(d.Id))
                    {
                        result.Add(d);
                        break;
                    }
                }
            }
            return result;
        }

        private static void Load()
        {
            if (_all != null)
                return;

            _all = new List<ScenarioDef>();
            _loadErrors.Clear();
            var ids = new HashSet<string>();
            var methods = new List<MethodInfo>();
            foreach (Type t in typeof(ScenarioCatalog).Assembly.GetTypes())
            {
                if (t.Namespace == null || !t.Namespace.StartsWith("Tests.Scenarios", StringComparison.Ordinal))
                    continue;
                foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    if (m.GetCustomAttribute<ScenarioSourceAttribute>() != null && m.GetParameters().Length == 0
                        && typeof(IEnumerable<ScenarioDef>).IsAssignableFrom(m.ReturnType))
                        methods.Add(m);
            }
            methods.Sort((a, b) => string.CompareOrdinal(a.DeclaringType.FullName + "." + a.Name, b.DeclaringType.FullName + "." + b.Name));

            foreach (MethodInfo m in methods)
            {
                try
                {
                    foreach (ScenarioDef d in (IEnumerable<ScenarioDef>)m.Invoke(null, null))
                    {
                        if (d == null)
                            continue;
                        if (!ids.Add(d.Id))
                        {
                            _loadErrors.Add("duplicate scenario id " + d.Id + " in " + m.DeclaringType.Name + "." + m.Name);
                            continue;
                        }
                        _all.Add(d);
                    }
                }
                catch (Exception ex)
                {
                    Exception inner = ex is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;
                    _loadErrors.Add(m.DeclaringType.Name + "." + m.Name + ": " + inner.GetType().Name + ": " + inner.Message);
                }
            }
        }

        /// <summary>One line per scenario: id, title, tags and covers.</summary>
        public static string Describe(IEnumerable<ScenarioDef> defs)
        {
            var sb = new StringBuilder();
            foreach (ScenarioDef d in defs)
                sb.Append(d.Id).Append(" | ").Append(d.Title)
                  .Append(" | tags=").Append(string.Join(",", d.Tags))
                  .Append(" | covers=").Append(string.Join(",", d.Covers)).Append('\n');
            return sb.ToString();
        }
    }
}
#endif
