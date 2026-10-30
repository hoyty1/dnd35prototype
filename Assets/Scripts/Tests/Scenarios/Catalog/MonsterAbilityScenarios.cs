#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Tests.Scenarios
{
    /// <summary>
    /// Batch 13 step 18 (docs/designs/action_catalog_batch13.md; AI-040, CRE-039 narrowed to the triggers; decision D11):
    /// the monster abilities as catalog actions any controller takes.
    ///
    /// Breath (MM p.69, p.306, p.315; PHB p.141): a standard action that provokes nothing; a dragon's recharges in 1d4
    /// rounds. The AI-run dragon's breath spends its standard action (before: none), and a nauseated dragon (a move action
    /// only, PHB p.311) does not breathe. A controllable dragon breathes through its Monster Abilities button, keeps its
    /// move action, and cannot breathe again while the breath recharges.
    ///
    /// Spittle (MM p.126): a free action once per round at one opponent within 30 ft. A second spittle in the same round is
    /// refused, on the AI runner and through the button; the turn start makes it available again.
    ///
    /// Engulf (MM p.202): a standard action, so a controllable cube engulfs once and a second engulf that turn is refused.
    ///
    /// The review's cases: a nauseated mouther spits at no one (PHB p.311: unable to attack); a dragon limited to a single
    /// action keeps it for its breath; a Large cube engulfs a creature next to any square of its space; a spittle that
    /// drops the last foe ends the fight at once on the PC's turn.
    /// </summary>
    public static class MonsterAbilityScenarios
    {
        private const string NotePrefix = "b13s18 ";

        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return S("breath-costs-standard-ai", BreathCostsStandardAi);
            yield return S("breath-costs-standard-ui", BreathCostsStandardUi);
            yield return S("spittle-once-per-round-ai", SpittleOncePerRoundAi);
            yield return S("spittle-once-per-round-ui", SpittleOncePerRoundUi);
            yield return S("engulf-ui", EngulfUi);
            yield return S("spittle-nauseated-ai", SpittleNauseatedAi);
            yield return S("breath-single-action-dragon-ai", BreathSingleActionDragonAi);
            yield return S("engulf-large-adjacent", EngulfLargeAdjacent);
            yield return S("spittle-ends-combat-ui", SpittleEndsCombatUi);
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("MonsterAbilityScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title) => Scenario.Define(id, title).Tags("rules");

        private static Action<CharacterController> SturdyNamed(string name)
            => c =>
            {
                c.Stats.AdjustMaxHP(300);
                c.Stats.CurrentHP += 300;
                if (name != null)
                    c.Stats.CharacterName = name;
            };

        /// <summary>
        /// A controllable creature with a bestiary entry's monster abilities: a party-slot fighter (the harness gives Ui
        /// control to party slots only) given <paramref name="npcId"/>'s breath weapon, ranged special and engulf (each
        /// Configure* clones the definition), as a controllable summoned creature carries them (PHB p.285). The catalog
        /// lists an entry for any creature with the data (ActionCatalog.AvailableTo), so the button and the executors are
        /// the ones a summoned hell hound would get (a bombardier beetle's acid spray stays a reaction, batch 12 D1).
        /// </summary>
        private static Action<CharacterController> WithAbilitiesOf(string npcId, string name)
            => c =>
            {
                SturdyNamed(name)(c);
                NPCDefinition def = NPCDatabase.Get(npcId);
                if (def == null)
                    return;
                c.ConfigureBreathWeapon(def.BreathWeapon);
                c.ConfigureRangedSpecialAttack(def.RangedSpecialAttack);
                c.ConfigureEngulf(def.Engulf);
            };

        private static void Note(ScenarioContext ctx, string key, object value) => ctx.Note(NotePrefix + key + "=" + value);

        /// <summary>The last note of <paramref name="key"/> is <paramref name="want"/>.</summary>
        private static Func<TraceView, ExpectResult> Fact(string key, string want)
            => v =>
            {
                string prefix = NotePrefix + key + "=";
                TraceEvent n = v.Of("note").LastOrDefault(e => (e.Str("text") ?? "").StartsWith(prefix, StringComparison.Ordinal));
                if (n == null)
                    return ExpectResult.Inconclusive("no note " + key);
                string got = n.Str("text").Substring(prefix.Length);
                return got == want ? ExpectResult.Pass(key + "=" + got, n.Seq) : ExpectResult.Fail(key + "=" + got + ", expected " + want, n.Seq);
            };

        /// <summary>The <paramref name="actor"/>'s first turn end in <paramref name="round"/> spent its standard action (econ.std).</summary>
        private static Func<TraceView, ExpectResult> StandardSpent(string actor, int round, bool want)
            => v =>
            {
                TraceEvent te = v.Of("turn_end").FirstOrDefault(e => e.Str("actor") == actor && e.Round == round);
                if (te == null || !(te.Get("econ") is JsonObj econ))
                    return ExpectResult.Fail("no turn end of " + actor + " in round " + round);
                bool std = econ.Get("std") is bool su && su;
                return std == want ? ExpectResult.Pass("std " + std, te.Seq) : ExpectResult.Fail("std " + std + ", expected " + want, te.Seq);
            };

        /// <summary>The <paramref name="who"/>'s combat-log lines matching <paramref name="what"/> number <paramref name="count"/> in <paramref name="round"/>.</summary>
        private static Func<TraceView, ExpectResult> LogCount(string regex, int round, int count)
            => v =>
            {
                int n = v.Log(regex).Count(e => e.Round == round);
                return n == count ? ExpectResult.Pass(n + " line(s) in round " + round) : ExpectResult.Fail(n + " line(s) in round " + round + ", expected " + count);
            };

        private static void NoteEconomy(ScenarioContext ctx, string prefix, CharacterController c)
        {
            Note(ctx, prefix + ".std", c.Actions.HasStandardAction);
            Note(ctx, prefix + ".move", c.Actions.HasMoveAction);
        }

        // ── Breath ────────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Two AI-run young red dragons face sturdy idle fighters 3 squares away. The first breathes in round 1 (the breath
        /// is ready and a spellcasting young red casts no cantrip first, AI-037) and its turn ends with its standard action
        /// spent (MM p.69, p.306; before AI-040's fix the breath spent none). The second starts nauseated (only a move action,
        /// PHB p.311), so the breath entry's start gate (a standard action) keeps it from breathing.
        /// </summary>
        private static ScenarioDef BreathCostsStandardAi()
        {
            return Rules("rules/breath-costs-standard-ai", "An AI dragon's breath is a standard action, and a nauseated dragon does not breathe (MM p.69, p.306; D11, AI-040)")
                .Covers("AI-040", "CRE-039", "D11", "MM p.69", "MM p.306", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Npc("dragon", "dragon_red_young", 12, 10, Control.Ai)
                .Tweak("dragon", SturdyNamed("Red Dragon"))
                .Npc("sick", "dragon_red_young", 12, 16, Control.Ai)
                .Tweak("sick", SturdyNamed("Sick Dragon"))
                .StartCondition("sick", CombatConditionType.Nauseated, 3)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 5)), 9, 10, Control.Idle)
                .Tweak("hero", SturdyNamed(null))
                .Pc("hero2", ActorSource.Stats(() => RulesScenarios.Fighter("Hero2", 5)), 9, 11, Control.Idle)
                .Tweak("hero2", SturdyNamed(null))
                .Pc("hero3", ActorSource.Stats(() => RulesScenarios.Fighter("Hero3", 5)), 9, 16, Control.Idle)
                .Tweak("hero3", SturdyNamed(null))
                .Initiative("dragon", "sick", "hero", "hero2", "hero3")
                .Expect("The dragons' turns are AI turns", Expect.All(Expect.Controller("dragon", "ai"), Expect.Controller("sick", "ai")))
                .Expect("The dragon breathes in round 1", LogCount(@"Red Dragon unleashes a \d+-ft (cone|line) of", 1, 1))
                .Expect("Its breath turn ends with its standard action spent (MM p.69, p.306; AI-040)", StandardSpent("dragon", 1, true))
                .Expect("The nauseated dragon, with only a move action, does not breathe (PHB p.311)", v =>
                    v.Log("Sick Dragon unleashes").Count == 0 ? ExpectResult.Pass("no breath") : ExpectResult.Fail("it breathed"))
                .Build();
        }

        /// <summary>
        /// A controllable creature with a young red dragon's breath (<see cref="WithAbilitiesOf"/>) presses its Monster
        /// Abilities button: Breath Weapon, aimed at Dummy Alpha. The breath spends its standard action
        /// and keeps its move action (UiAfter AttackDelay returns the panel), its 1d4-round recharge starts (MM p.69), and a
        /// second press offers no breath (refused).
        /// </summary>
        private static ScenarioDef BreathCostsStandardUi()
        {
            return Rules("rules/breath-costs-standard-ui", "A controllable creature with a dragon's breath breathes through its Monster Abilities button, as a standard action (MM p.69, p.306; D11, CRE-039)")
                .Covers("AI-040", "CRE-039", "D11", "MM p.69", "MM p.306", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("dragon", ActorSource.Stats(() => RulesScenarios.Fighter("Red Dragon", 5)), 12, 10, Control.Ui)
                .Tweak("dragon", WithAbilitiesOf("dragon_red_young", null))
                .Npc("dummyA", "target_dummy", 9, 10, Control.Scripted)
                .Tweak("dummyA", SturdyNamed("Dummy Alpha"))
                .Npc("dummyB", "target_dummy", 9, 11, Control.Scripted)
                .Tweak("dummyB", SturdyNamed("Dummy Beta"))
                .Initiative("dragon", "dummyA", "dummyB")
                .Turn("dragon", 1,
                    Step.Assert("before the breath", ctx =>
                    {
                        CharacterController d = ctx.Get("dragon");
                        Note(ctx, "pre.ready", d.HasBreathWeapon && d.IsBreathWeaponReady);
                        NoteEconomy(ctx, "pre", d);
                        return true;
                    }),
                    Step.MonsterAbility(ActionIds.BreathWeapon, "dummyA"),
                    Step.Assert("after the breath", ctx =>
                    {
                        CharacterController d = ctx.Get("dragon");
                        Note(ctx, "post.ready", d.IsBreathWeaponReady);
                        Note(ctx, "post.uses", d.BreathWeaponUsesToday);
                        NoteEconomy(ctx, "post", d);
                        return true;
                    }),
                    Step.MonsterAbility(ActionIds.BreathWeapon, "dummyB"))
                .Expect("The dragon's turn is a Ui turn", Expect.Controller("dragon", "ui"))
                .Expect("Before, the breath is ready and the standard and move actions are left",
                    Expect.All(Fact("pre.ready", "True"), Fact("pre.std", "True"), Fact("pre.move", "True")))
                .Expect("The Monster Abilities breath step is done", Expect.StepStatus("dragon", 1, "MonsterAbility", 0, "done"))
                .Expect("The dragon breathes once", LogCount(@"Red Dragon unleashes a \d+-ft (cone|line) of", 1, 1))
                .Expect("The breath spent the standard action, kept the move action and started the recharge (MM p.69)",
                    Expect.All(Fact("post.std", "False"), Fact("post.move", "True"), Fact("post.ready", "False"), Fact("post.uses", "1")))
                .Expect("A second breath that turn is not offered", Expect.StepStatus("dragon", 1, "MonsterAbility", 1, "refused"))
                .Build();
        }

        // ── Spittle ───────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A Scripted gibbering mouther takes the spittle entry through the AI runner: at Hero in round 1 (done), at Hero2
        /// the same round (refused: once per round, MM p.126), and at Hero in round 2 (done: its turn start reset it). The
        /// spittle is a free action, so its standard action is left. An AI-run mouther beside it spits at most once a round.
        /// </summary>
        private static ScenarioDef SpittleOncePerRoundAi()
        {
            return Rules("rules/spittle-once-per-round-ai", "A gibbering mouther's spittle is a free action once per round, on the AI runner (MM p.126; D11, CRE-039)")
                .Covers("CRE-039", "D11", "MM p.126", "PC_NPC_PARITY")
                .MaxRounds(2)
                .Npc("mouther", "gibbering_mouther", 12, 10, Control.Scripted)
                .Tweak("mouther", SturdyNamed("Scripted Mouther"))
                .Npc("aimouther", "gibbering_mouther", 12, 17, Control.Ai)
                .Tweak("aimouther", SturdyNamed("AI Mouther"))
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 5)), 9, 10, Control.Idle)
                .Tweak("hero", SturdyNamed(null))
                .Pc("hero2", ActorSource.Stats(() => RulesScenarios.Fighter("Hero2", 5)), 9, 12, Control.Idle)
                .Tweak("hero2", SturdyNamed(null))
                .Pc("hero3", ActorSource.Stats(() => RulesScenarios.Fighter("Hero3", 5)), 9, 17, Control.Idle)
                .Tweak("hero3", SturdyNamed(null))
                .Initiative("mouther", "aimouther", "hero", "hero2", "hero3")
                .Turn("mouther", 1,
                    Step.MonsterAbility(ActionIds.Spittle, "hero"),
                    Step.MonsterAbility(ActionIds.Spittle, "hero2"),
                    Step.Assert("after the spittle", ctx =>
                    {
                        NoteEconomy(ctx, "r1", ctx.Get("mouther"));
                        return true;
                    }))
                .Turn("mouther", 2, Step.MonsterAbility(ActionIds.Spittle, "hero"))
                .Expect("Round 1: the first spittle is taken", Expect.StepStatus("mouther", 1, "MonsterAbility", 0, "done"))
                .Expect("Round 1: a second spittle is refused (once per round, MM p.126)", Expect.StepStatus("mouther", 1, "MonsterAbility", 1, "refused"))
                .Expect("Round 2: the spittle is available again", Expect.StepStatus("mouther", 2, "MonsterAbility", 0, "done"))
                .Expect("The spittle is a free action: the standard and move actions are left", Expect.All(Fact("r1.std", "True"), Fact("r1.move", "True")))
                .Expect("The Scripted mouther spits once in each round",
                    Expect.All(LogCount("Scripted Mouther uses Spittle against", 1, 1), LogCount("Scripted Mouther uses Spittle against", 2, 1)))
                .Expect("The AI-run mouther spits at most once a round, and at least once", v =>
                {
                    List<TraceEvent> lines = v.Log("AI Mouther uses Spittle against");
                    int r1 = lines.Count(e => e.Round == 1), r2 = lines.Count(e => e.Round == 2);
                    if (lines.Count == 0 && ScenarioChecks.IsDownSnapshot(v.Final("aimouther")))
                        return ExpectResult.Inconclusive("the AI mouther was dropped");
                    return r1 <= 1 && r2 <= 1 && lines.Count >= 1
                        ? ExpectResult.Pass("rounds 1/2: " + r1 + "/" + r2)
                        : ExpectResult.Fail("rounds 1/2: " + r1 + "/" + r2);
                })
                .Build();
        }

        /// <summary>
        /// A controllable creature with a gibbering mouther's spittle (<see cref="WithAbilitiesOf"/>) spits at Dummy Alpha through its Monster Abilities button (free: its standard and
        /// move actions are left); a second press in the same turn does not offer the spittle (refused).
        /// </summary>
        private static ScenarioDef SpittleOncePerRoundUi()
        {
            return Rules("rules/spittle-once-per-round-ui", "A controllable creature with spittle spits once per round through its Monster Abilities button (MM p.126; D11, CRE-039)")
                .Covers("CRE-039", "D11", "MM p.126", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("mouther", ActorSource.Stats(() => RulesScenarios.Fighter("Our Mouther", 5)), 12, 10, Control.Ui)
                .Tweak("mouther", WithAbilitiesOf("gibbering_mouther", null))
                .Npc("dummyA", "target_dummy", 9, 10, Control.Scripted)
                .Tweak("dummyA", SturdyNamed("Dummy Alpha"))
                .Npc("dummyB", "target_dummy", 9, 12, Control.Scripted)
                .Tweak("dummyB", SturdyNamed("Dummy Beta"))
                .Initiative("mouther", "dummyA", "dummyB")
                .Turn("mouther", 1,
                    Step.MonsterAbility(ActionIds.Spittle, "dummyA"),
                    Step.Assert("after the spittle", ctx =>
                    {
                        CharacterController m = ctx.Get("mouther");
                        Note(ctx, "used", m.FreeRangedSpecialUsedThisRound);
                        NoteEconomy(ctx, "post", m);
                        return true;
                    }),
                    Step.MonsterAbility(ActionIds.Spittle, "dummyB"))
                .Expect("The mouther's turn is a Ui turn", Expect.Controller("mouther", "ui"))
                .Expect("The first spittle is taken", Expect.StepStatus("mouther", 1, "MonsterAbility", 0, "done"))
                .Expect("It is a free action, and it is spent for the round",
                    Expect.All(Fact("used", "True"), Fact("post.std", "True"), Fact("post.move", "True")))
                .Expect("A second spittle that round is not offered (MM p.126)", Expect.StepStatus("mouther", 1, "MonsterAbility", 1, "refused"))
                .Expect("The mouther spits once", LogCount("Our Mouther uses Spittle against", 1, 1))
                .Build();
        }

        // ── Engulf ────────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A controllable creature with a gelatinous cube's engulf (<see cref="WithAbilitiesOf"/>) engulfs the adjacent Dummy Alpha through its Monster Abilities button: a standard
        /// action (MM p.202), so the dummy makes its Reflex save or is engulfed, the cube keeps its move action, and a second
        /// engulf that turn (at Dummy Alpha again) is refused. Dummy Beta, far away, keeps the fight going.
        /// </summary>
        private static ScenarioDef EngulfUi()
        {
            return Rules("rules/engulf-ui", "A controllable creature with engulf engulfs through its Monster Abilities button, as a standard action (MM p.202; D11, CRE-039)")
                .Covers("CRE-039", "D11", "MM p.202", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("cube", ActorSource.Stats(() => RulesScenarios.Fighter("Our Cube", 5)), 12, 10, Control.Ui)
                .Tweak("cube", WithAbilitiesOf("gelatinous_cube", null))
                .Npc("dummyA", "target_dummy", 11, 10, Control.Scripted)
                .Tweak("dummyA", SturdyNamed("Dummy Alpha"))
                .Npc("dummyB", "target_dummy", 2, 18, Control.Scripted)
                .Tweak("dummyB", SturdyNamed("Dummy Beta"))
                .Initiative("cube", "dummyA", "dummyB")
                .Turn("cube", 1,
                    Step.MonsterAbility(ActionIds.Engulf, "dummyA"),
                    Step.Assert("after the engulf", ctx =>
                    {
                        NoteEconomy(ctx, "post", ctx.Get("cube"));
                        return true;
                    }),
                    Step.MonsterAbility(ActionIds.Engulf, "dummyA"))
                .Expect("The cube's turn is a Ui turn", Expect.Controller("cube", "ui"))
                .Expect("The engulf step is done", Expect.StepStatus("cube", 1, "MonsterAbility", 0, "done"))
                .Expect("The cube attempts the engulf once", LogCount("Our Cube attempts to engulf Dummy Alpha", 1, 1))
                .Expect("Engulf spent the standard action and kept the move action (MM p.202)",
                    Expect.All(Fact("post.std", "False"), Fact("post.move", "True")))
                .Expect("A second engulf that turn is refused", Expect.StepStatus("cube", 1, "MonsterAbility", 1, "refused"))
                .Build();
        }

        // ── Review cases ──────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Two nauseated gibbering mouthers (PHB p.311: unable to attack, a single move action) beside idle fighters within
        /// 30 ft: the Scripted one's spittle entry is refused (the shared actor bar, on the AI runner), and the AI-run one
        /// spits at no one. The spittle's free cost does not lift the bar: it is a ranged touch attack (MM p.126).
        /// </summary>
        private static ScenarioDef SpittleNauseatedAi()
        {
            return Rules("rules/spittle-nauseated-ai", "A nauseated gibbering mouther makes no spittle attack, free or not (PHB p.311; MM p.126; D11)")
                .Covers("CRE-039", "D11", "MM p.126", "PHB p.311", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Npc("mouther", "gibbering_mouther", 12, 10, Control.Scripted)
                .Tweak("mouther", SturdyNamed("Sick Mouther"))
                .StartCondition("mouther", CombatConditionType.Nauseated, 3)
                .Npc("aimouther", "gibbering_mouther", 12, 17, Control.Ai)
                .Tweak("aimouther", SturdyNamed("Sick AI Mouther"))
                .StartCondition("aimouther", CombatConditionType.Nauseated, 3)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 5)), 9, 10, Control.Idle)
                .Tweak("hero", SturdyNamed(null))
                .Pc("hero2", ActorSource.Stats(() => RulesScenarios.Fighter("Hero2", 5)), 9, 17, Control.Idle)
                .Tweak("hero2", SturdyNamed(null))
                .Initiative("mouther", "aimouther", "hero", "hero2")
                .Turn("mouther", 1, Step.MonsterAbility(ActionIds.Spittle, "hero"))
                .Expect("The nauseated Scripted mouther's spittle is refused (PHB p.311)", Expect.StepStatus("mouther", 1, "MonsterAbility", 0, "refused"))
                .Expect("Neither nauseated mouther spits", v =>
                    v.Log("Sick (AI )?Mouther uses Spittle").Count == 0 ? ExpectResult.Pass("no spittle") : ExpectResult.Fail(v.Log("Sick (AI )?Mouther uses Spittle").Count + " spittle line(s)"))
                .Build();
        }

        /// <summary>
        /// An AI-run young red dragon limited to a single action (Stats.IsSingleActionsOnly, as a disabled or staggered
        /// creature: ActionEconomy.SingleActionOnly) at (14, 4), with Hero 3 squares away (in its 40-ft cone) and two
        /// fighters at (4, 12) and (5, 13), out of the cone from where it stands but in it after a move of about 6 squares,
        /// so the best breath position needs a move. Its single action goes to the breath from where it stands: the
        /// Dragon routine takes no positioning move that would leave no standard action (review), and a refused breath
        /// would go on to its melee priorities. Before, the positioning move spent the turn and the breath was refused.
        /// </summary>
        private static ScenarioDef BreathSingleActionDragonAi()
        {
            return Rules("rules/breath-single-action-dragon-ai", "An AI dragon limited to a single action spends it on its breath, not on a positioning move (MM p.69; PHB p.145; D11, AI-040)")
                .Covers("AI-040", "D11", "MM p.69", "PHB p.145")
                .MaxRounds(1)
                .Npc("dragon", "dragon_red_young", 14, 4, Control.Ai)
                .Tweak("dragon", c => { SturdyNamed("Slow Dragon")(c); c.Stats.IsSingleActionsOnly = true; })
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 5)), 11, 4, Control.Idle)
                .Tweak("hero", SturdyNamed(null))
                .Pc("hero2", ActorSource.Stats(() => RulesScenarios.Fighter("Hero2", 5)), 4, 12, Control.Idle)
                .Tweak("hero2", SturdyNamed(null))
                .Pc("hero3", ActorSource.Stats(() => RulesScenarios.Fighter("Hero3", 5)), 5, 13, Control.Idle)
                .Tweak("hero3", SturdyNamed(null))
                .Initiative("dragon", "hero", "hero2", "hero3")
                .Expect("The dragon's turn is an AI turn", Expect.Controller("dragon", "ai"))
                .Expect("The single-action dragon breathes in round 1", LogCount(@"Slow Dragon unleashes a \d+-ft (cone|line) of", 1, 1))
                .Expect("It takes no positioning move that would cost its only action", Expect.None("move", e => e.Str("by") == "dragon" || e.Str("actor") == "dragon"))
                .Expect("Its breath turn ends with its standard action spent (MM p.69; AI-040)", StandardSpent("dragon", 1, true))
                .Build();
        }

        /// <summary>
        /// A Scripted gelatinous cube (Large: the space (12..13, 10..11)) engulfs Hero at (14, 11), next to its far square
        /// and two squares from its anchor, through the AI runner (review: adjacency is measured space to space, MM p.202).
        /// </summary>
        private static ScenarioDef EngulfLargeAdjacent()
        {
            return Rules("rules/engulf-large-adjacent", "A Large gelatinous cube engulfs a creature next to any square of its space (MM p.202; D11)")
                .Covers("CRE-039", "CRE-078", "D11", "MM p.202")
                .MaxRounds(1)
                .Npc("cube", "gelatinous_cube", 12, 10, Control.Scripted)
                .Tweak("cube", SturdyNamed("Large Cube"))
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 5)), 14, 11, Control.Idle)
                .Tweak("hero", SturdyNamed(null))
                .Pc("hero2", ActorSource.Stats(() => RulesScenarios.Fighter("Hero2", 5)), 3, 18, Control.Idle)
                .Tweak("hero2", SturdyNamed(null))
                .Initiative("cube", "hero", "hero2")
                .Turn("cube", 1,
                    Step.Assert("the cube is Large and Hero touches its far square only", ctx =>
                    {
                        CharacterController cube = ctx.Get("cube"), hero = ctx.Get("hero");
                        Note(ctx, "size", cube.GetCurrentSizeCategory());
                        Note(ctx, "anchorDistance", SquareGridUtils.GetDistance(cube.GridPosition, hero.GridPosition));
                        Note(ctx, "spaceDistance", cube.GetMinimumDistanceToTarget(hero, chebyshev: true));
                        return true;
                    }),
                    Step.MonsterAbility(ActionIds.Engulf, "hero"))
                .Expect("The cube is Large, Hero is two squares from its anchor and next to its space",
                    Expect.All(Fact("size", "Large"), Fact("anchorDistance", "2"), Fact("spaceDistance", "1")))
                .Expect("The engulf step is done", Expect.StepStatus("cube", 1, "MonsterAbility", 0, "done"))
                .Expect("The cube attempts to engulf Hero once", LogCount("Large Cube attempts to engulf Hero", 1, 1))
                .Build();
        }

        /// <summary>
        /// A controllable creature with a gibbering mouther's spittle spits at the last foe, a 1-hp dummy, through its
        /// Monster Abilities button; the ranged touch attack's d20 is forced to 20 and its 1d4 to 4, so the dummy drops to
        /// -3. The shared core runs the combat-end check (review), so the fight ends inside the action, before the
        /// mouther's turn ends, and not at a later turn boundary.
        /// </summary>
        private static ScenarioDef SpittleEndsCombatUi()
        {
            return Rules("rules/spittle-ends-combat-ui", "A controllable creature's spittle that drops the last foe ends the fight at once (MM p.126; D11)")
                .Covers("CRE-039", "D11", "MM p.126", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("mouther", ActorSource.Stats(() => RulesScenarios.Fighter("Our Mouther", 5)), 12, 10, Control.Ui)
                .Tweak("mouther", WithAbilitiesOf("gibbering_mouther", null))
                .Npc("dummy", "target_dummy", 9, 10, Control.Scripted)
                .Tweak("dummy", c => c.Stats.CharacterName = "Last Dummy")
                .Hp("dummy", 1)
                .Force(20, 20, AIService.SpecialRangedTouchContext, 1)
                .Force(4, 4, "Special attack damage", 1)
                .Initiative("mouther", "dummy")
                .Turn("mouther", 1, Step.MonsterAbility(ActionIds.Spittle, "dummy"))
                .Expect("The spittle hits the last foe", LogCount("Our Mouther's Spittle hits", 1, 1))
                .Expect("The game declares the victory itself", Expect.Outcome(Outcome.Victory))
                .Expect("The fight ends in round 1, before the mouther's turn ends (no later turn-boundary check)", v =>
                {
                    TraceEvent end = v.Of("phase").FirstOrDefault(e => e.Get("to")?.ToString() == "CombatOver");
                    if (end == null)
                        return ExpectResult.Fail("no CombatOver phase");
                    var by = new List<string>();
                    if (end.Get("by") is System.Collections.IEnumerable list && !(end.Get("by") is string))
                        foreach (object o in list)
                            by.Add(o?.ToString());
                    TraceEvent turnEnd = v.Of("turn_end").FirstOrDefault(e => e.Str("actor") == "mouther");
                    bool beforeTurnEnd = turnEnd == null || end.Seq < turnEnd.Seq;
                    string text = string.Join(" < ", by);
                    return end.Round == 1 && beforeTurnEnd && !by.Contains("BeginCombatantTurn")
                        ? ExpectResult.Pass("round " + end.Round + " by " + text, end.Seq)
                        : ExpectResult.Fail("round " + end.Round + " by " + text + (beforeTurnEnd ? "" : ", after the mouther's turn end"), end.Seq);
                })
                .Build();
        }
    }
}
#endif
