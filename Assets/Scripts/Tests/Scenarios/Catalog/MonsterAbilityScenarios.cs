#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
    ///
    /// Batch 13 step 19 (AI-041, CRE-039, AI-053; decisions D12 and D13): frightful presence fires on every bearer's attack
    /// sequence (attacks of opportunity included), charge and breath, with the HD limit, the witness test and per-bearer
    /// immunity (MM p.69, p.309); an aura fires at the start of every bearer's acting turn, compelled turns included, and
    /// gibbering affects every creature but mouthers (MM p.126).
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
            yield return S("frightful-presence-on-aoo-ai", FrightfulPresenceOnAoOAi);
            yield return S("frightful-presence-on-charge-ai", FrightfulPresenceOnChargeAi);
            yield return S("frightful-presence-hd-limit", FrightfulPresenceHdLimit);
            yield return S("frightful-presence-immune-after-save", FrightfulPresenceImmuneAfterSave);
            yield return S("frightful-presence-controllable-bearer-ui", FrightfulPresenceControllableBearerUi);
            yield return S("frightful-presence-on-maneuver-ui", FrightfulPresenceOnManeuverUi);
            yield return S("frightful-presence-on-maneuver-ai", FrightfulPresenceOnManeuverAi);
            yield return S("gibbering-hits-allies-ai", GibberingHitsAlliesAi);
            yield return S("aura-on-compelled-turn-ai", AuraOnCompelledTurnAi);
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

        // ── Frightful presence (batch 13 step 19, D12) ──────────────────────────────────────────────────────────────

        private const string NotePrefix19 = "b13s19 ";

        /// <summary>
        /// A test bearer: the creature's HD set to <paramref name="hd"/> (0 keeps them) and a frightful presence with the
        /// glossary's defaults (MM p.309: 30 ft, 5d6 rounds), Will DC 15, panicking 4 HD or less and shaking the rest (the
        /// dragon split, MM p.69). The definition is a fresh object, cloned by ConfigureFrightfulPresence; no template is touched.
        /// </summary>
        private static Action<CharacterController> Fearsome(int hd, string name)
            => c =>
            {
                SturdyNamed(name)(c);
                if (hd > 0)
                    c.Stats.HitDice = hd;
                c.ConfigureFrightfulPresence(new FrightfulPresenceDefinition
                {
                    SaveDC = 15, RangeFeet = 30, HDThresholdForPanic = 4, DurationDice = 5, DurationDieSides = 6
                });
            };

        private static Action<CharacterController> SaveTweak(int bonus)
            => c => c.Stats.Bonuses.Set("scenario:save-tweak", BonusTarget.AllSaves, BonusType.Untyped, bonus, "scenario save tweak");

        /// <summary>The presence's announcement lines of <paramref name="bearer"/> for <paramref name="action"/> (round optional).</summary>
        private static List<TraceEvent> PresenceLines(TraceView v, string bearer, string action, int? round = null)
            => v.Log(Regex.Escape(bearer) + @"(</b>)? " + Regex.Escape(action) + ": its Frightful Presence")
                .Where(e => round == null || e.Round == round.Value).ToList();

        /// <summary><paramref name="name"/>'s own presence lines (its save: resists, or is shaken or panicked).</summary>
        private static List<TraceEvent> PresenceSaveLines(TraceView v, string name)
            => v.Log(@"(^|[^A-Za-z])" + Regex.Escape(name) + @" (resists the Frightful Presence|is (<b>)?(Shaken|Panicked)(</b>)?)").ToList();

        private static Func<TraceView, ExpectResult> CondAdded(string key, CombatConditionType type, bool want)
            => v =>
            {
                bool got = v.Conds(key, type).Any(e => e.Bool("added"));
                return got == want ? ExpectResult.Pass(key + " " + type + " " + got) : ExpectResult.Fail(key + " " + type + " " + got + ", expected " + want);
            };

        private static Func<TraceView, ExpectResult> SaveLineCount(string name, int count)
            => v =>
            {
                int n = PresenceSaveLines(v, name).Count;
                return n == count ? ExpectResult.Pass(name + ": " + n + " presence save line(s)") : ExpectResult.Fail(name + ": " + n + " presence save line(s), expected " + count);
            };

        private static Func<TraceView, ExpectResult> PresenceLineCount(string bearer, string action, int? round, int count)
            => v =>
            {
                int n = PresenceLines(v, bearer, action, round).Count;
                return n == count ? ExpectResult.Pass(n + " line(s)") : ExpectResult.Fail(n + " line(s), expected " + count);
            };

        /// <summary>The last note of <paramref name="key"/> (step 19 prefix) is <paramref name="want"/>.</summary>
        private static Func<TraceView, ExpectResult> Fact19(string key, string want)
            => v =>
            {
                string prefix = NotePrefix19 + key + "=";
                TraceEvent n = v.Of("note").LastOrDefault(e => (e.Str("text") ?? "").StartsWith(prefix, StringComparison.Ordinal));
                if (n == null)
                    return ExpectResult.Inconclusive("no note " + key);
                string got = n.Str("text").Substring(prefix.Length);
                return got == want ? ExpectResult.Pass(key + "=" + got, n.Seq) : ExpectResult.Fail(key + "=" + got + ", expected " + want, n.Seq);
            };

        /// <summary>
        /// D12 (MM p.309; an attack of opportunity is an attack, delegated RAW reading): a 6-HD orc with a frightful presence
        /// (<see cref="Fearsome"/>) passes its round-1 turn (so it is no longer flat-footed and threatens, PHB p.137), then a
        /// Scripted fighter 5 leaves its threatened square. The orc's attack of opportunity fires the presence in the hero's
        /// turn; the hero (5 HD, fewer than 6) fails its Will save (forced natural 1) and is shaken (more than 4 HD). On the
        /// orc's round-2 turn, run by the AI (its charge or attack), the hero, still shaken by it, is not asked again. Before
        /// step 19 only the AI's NPCPerformAttack and breath fired it, once per combat.
        /// </summary>
        private static ScenarioDef FrightfulPresenceOnAoOAi()
        {
            return Rules("rules/frightful-presence-on-aoo-ai", "An attack of opportunity fires the bearer's frightful presence, and a shaken witness is not asked again (MM p.309; D12, AI-041)")
                .Covers("AI-041", "CRE-039", "D12", "MM p.309", "MM p.69", "PHB p.137", "PC_NPC_PARITY")
                .MaxRounds(2)
                .Npc("orc", "orc_warrior", 10, 10, Control.Scripted)
                .Tweak("orc", Fearsome(6, "Dread Orc"))
                .AiWhenUnscripted("orc")
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 5)), 11, 10, Control.Scripted)
                .Tweak("hero", SturdyNamed(null))
                .Force(20, 1, GameManager.FrightfulPresenceSaveContext, -1)
                .Initiative("orc", "hero")
                .Turn("orc", 1, Step.EndTurn())
                .Turn("hero", 1, Step.Move(15, 10))
                .Turn("hero", 2, Step.EndTurn())
                .Expect("The orc's round-2 turn is an AI turn", v =>
                {
                    TraceEvent t = v.TurnsOf("orc").FirstOrDefault(e => e.Round == 2);
                    return t != null && t.Str("controller") == "ai" ? ExpectResult.Pass("ai", t.Seq) : ExpectResult.Fail(t == null ? "no round-2 orc turn" : "under " + t.Str("controller"));
                })
                .Expect("The hero's move provokes the orc's attack of opportunity", v =>
                    v.AoOs("orc", "hero").Count >= 1 ? ExpectResult.Pass(v.AoOs("orc", "hero").Count + " AoO(s)") : ExpectResult.Fail("no AoO by the orc"))
                .Expect("The attack of opportunity fires the presence once, in the hero's round-1 turn (D12)", v =>
                {
                    List<TraceEvent> lines = PresenceLines(v, "Dread Orc", "makes an attack of opportunity", 1);
                    TraceEvent heroTurn = v.TurnsOf("hero").FirstOrDefault(e => e.Round == 1);
                    TraceEvent orcTurn2 = v.TurnsOf("orc").FirstOrDefault(e => e.Round == 2);
                    if (lines.Count != 1)
                        return ExpectResult.Fail(lines.Count + " AoO presence line(s)");
                    bool inHeroTurn = heroTurn != null && lines[0].Seq > heroTurn.Seq && (orcTurn2 == null || lines[0].Seq < orcTurn2.Seq);
                    return inHeroTurn
                        ? ExpectResult.Pass("in the hero's turn", lines[0].Seq)
                        : ExpectResult.Fail("outside the hero's round-1 turn", lines[0].Seq);
                })
                .Expect("The hero (5 HD, fewer than 6) fails and is shaken, not panicked (more than 4 HD)",
                    Expect.All(CondAdded("hero", CombatConditionType.Shaken, true), CondAdded("hero", CombatConditionType.Panicked, false)))
                .Expect("Still shaken by the orc, the hero is not asked again on the orc's own turn (D12)", v =>
                {
                    // The no-re-roll rule is exercised only if the orc attacked or charged the hero in round 2; a turn that
                    // did neither would let the count pass without testing anything.
                    bool attacked = v.Attacks("orc", "hero", false, 2).Any() || v.Log("Dread Orc charges Hero!").Any(e => e.Round == 2);
                    if (!attacked)
                        return ExpectResult.Inconclusive("the orc neither attacked nor charged the hero in round 2");
                    return SaveLineCount("Hero", 1)(v);
                })
                .Build();
        }

        /// <summary>
        /// D12 (MM p.69, p.309: the bearer attacks or charges): an AI-run 6-HD orc with a frightful presence charges a fighter
        /// 5 three squares away (as in rules/charge-flanking-ai). The presence fires at the charge's declaration, before the
        /// charge attack, with the orc on its starting square: Far Hero, 7 squares from that square (outside 30 ft) but 5 from
        /// where the charge ends, is no witness, and the charge attack does not fire it again (once per charge).
        /// </summary>
        private static ScenarioDef FrightfulPresenceOnChargeAi()
        {
            return Rules("rules/frightful-presence-on-charge-ai", "A charge fires the bearer's frightful presence at its declaration, once (MM p.69, p.309; D12, AI-041)")
                .Covers("AI-041", "CRE-039", "D12", "MM p.309", "MM p.69", "PHB p.154", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Npc("orc", "orc_warrior", 10, 10, Control.Ai)
                .Tweak("orc", Fearsome(6, "Dread Orc"))
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 5)), 13, 10, Control.Idle)
                .Tweak("hero", c => { RulesScenarios.StripOffHand(c); SturdyNamed(null)(c); })
                .Pc("far", ActorSource.Stats(() => RulesScenarios.Fighter("Far Hero", 5)), 17, 10, Control.Idle)
                .Tweak("far", SturdyNamed(null))
                .Force(20, 1, GameManager.FrightfulPresenceSaveContext, -1)
                .Initiative("orc", "hero", "far")
                .Expect("The orc's turn is an AI turn", Expect.Controller("orc", "ai"))
                .Expect("The orc charges the hero, and the presence fires at the declaration, before the charge attack (D12)", v =>
                {
                    TraceEvent charge = v.Log("Dread Orc charges Hero!").FirstOrDefault();
                    List<TraceEvent> lines = PresenceLines(v, "Dread Orc", "charges", 1);
                    TraceEvent attack = v.Attacks("orc", "hero", false, 1).FirstOrDefault();
                    if (charge == null)
                        return ExpectResult.Inconclusive("the orc did not charge");
                    if (lines.Count != 1)
                        return ExpectResult.Fail(lines.Count + " charge presence line(s)");
                    bool ordered = charge.Seq < lines[0].Seq && (attack == null || lines[0].Seq < attack.Seq);
                    return ordered ? ExpectResult.Pass("charge < presence < attack", lines[0].Seq) : ExpectResult.Fail("out of order", lines[0].Seq);
                })
                .Expect("The charge attack does not fire it again (no 'attacks' presence line)", PresenceLineCount("Dread Orc", "attacks", null, 0))
                .Expect("The hero fails and is shaken (5 HD)", CondAdded("hero", CombatConditionType.Shaken, true))
                .Expect("Far Hero, outside the radius at the declaration, is never asked", SaveLineCount("Far Hero", 0))
                .Build();
        }

        /// <summary>
        /// D12 with the lich's own data (NPCDatabase_L: 11 HD, DC 19, 60 ft, panicked at 5 HD or less, 2d6 rounds; AI-010,
        /// its true fear aura waits for batches 16 and 18): a Scripted lich touches Novice. Every frightful-presence save is
        /// forced to a natural 1. Only opponents with fewer HD than the lich are affected (MM p.309): Novice (5 HD) is
        /// panicked, Veteran (8 HD) shaken, and Peer (11 HD) and Master (12 HD) are never asked.
        /// </summary>
        private static ScenarioDef FrightfulPresenceHdLimit()
        {
            return Rules("rules/frightful-presence-hd-limit", "Frightful presence affects only opponents with fewer HD than the bearer, panicking the weakest (MM p.309, p.69; D12, AI-041)")
                .Covers("AI-041", "AI-010", "D12", "MM p.309", "MM p.69", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Npc("lich", "lich", 10, 10, Control.Scripted)
                .Tweak("lich", SturdyNamed("Lich"))
                .Pc("novice", ActorSource.Stats(() => RulesScenarios.Fighter("Novice", 5)), 11, 10, Control.Idle)
                .Tweak("novice", SturdyNamed(null))
                .Pc("veteran", ActorSource.Stats(() => RulesScenarios.Fighter("Veteran", 8)), 10, 13, Control.Idle)
                .Tweak("veteran", SturdyNamed(null))
                .Pc("peer", ActorSource.Stats(() => RulesScenarios.Fighter("Peer", 11)), 13, 13, Control.Idle)
                .Tweak("peer", SturdyNamed(null))
                .Pc("master", ActorSource.Stats(() => RulesScenarios.Fighter("Master", 12)), 7, 13, Control.Idle)
                .Tweak("master", SturdyNamed(null))
                .Force(20, 1, GameManager.FrightfulPresenceSaveContext, -1)
                .Initiative("lich", "novice", "veteran", "peer", "master")
                .Turn("lich", 1,
                    Step.Assert("the lich's presence data and the HD", ctx =>
                    {
                        CharacterController l = ctx.Get("lich");
                        FrightfulPresenceDefinition fp = l.GetFrightfulPresenceDefinition();
                        ctx.Note(NotePrefix19 + "lich.hd=" + l.Stats.GetHitDice());
                        ctx.Note(NotePrefix19 + "lich.fp=" + (fp != null ? fp.SaveDC + "/" + fp.RangeFeet + "/" + fp.HDThresholdForPanic : "none"));
                        ctx.Note(NotePrefix19 + "hd=" + ctx.Get("novice").Stats.GetHitDice() + "/" + ctx.Get("veteran").Stats.GetHitDice()
                            + "/" + ctx.Get("peer").Stats.GetHitDice() + "/" + ctx.Get("master").Stats.GetHitDice());
                        return true;
                    }),
                    Step.Attack("novice"))
                .Expect("The lich has 11 HD and its presence is DC 19, 60 ft, panic at 5 HD or less; the heroes have 5/8/11/12 HD",
                    Expect.All(Fact19("lich.hd", "11"), Fact19("lich.fp", "19/60/5"), Fact19("hd", "5/8/11/12")))
                .Expect("The lich's attack fires its presence once", PresenceLineCount("Lich", "attacks", 1, 1))
                .Expect("Novice (5 HD) is panicked and Veteran (8 HD) shaken",
                    Expect.All(CondAdded("novice", CombatConditionType.Panicked, true), CondAdded("veteran", CombatConditionType.Shaken, true),
                        CondAdded("veteran", CombatConditionType.Panicked, false)))
                .Expect("Peer (11 HD, not fewer) and Master (12 HD) are never asked (MM p.309)",
                    Expect.All(SaveLineCount("Peer", 0), SaveLineCount("Master", 0),
                        CondAdded("peer", CombatConditionType.Shaken, false), CondAdded("master", CombatConditionType.Shaken, false)))
                .Build();
        }

        /// <summary>
        /// D12 (MM p.309: a successful save makes the creature immune to that creature's presence for 24 hours; delegated:
        /// kept for the encounter, and no re-roll while still shaken): a Scripted 6-HD orc with a frightful presence attacks
        /// Brave in rounds 1 and 2. Every presence save rolls a forced 10: Brave (+30 on saves) succeeds in round 1 and is
        /// never asked again; Timid (-30) fails in round 1, is shaken for 5d6 rounds (at least 5) and, still shaken by the
        /// orc in round 2, is not asked again. Round 2's attack has no witness left, so it announces nothing.
        /// </summary>
        private static ScenarioDef FrightfulPresenceImmuneAfterSave()
        {
            return Rules("rules/frightful-presence-immune-after-save", "A creature that saves is immune to that bearer's presence, and a shaken one is not asked again (MM p.309; D12, AI-041)")
                .Covers("AI-041", "D12", "MM p.309", "PC_NPC_PARITY")
                .MaxRounds(2)
                .Npc("orc", "orc_warrior", 10, 10, Control.Scripted)
                .Tweak("orc", Fearsome(6, "Dread Orc"))
                .Pc("brave", ActorSource.Stats(() => RulesScenarios.Fighter("Brave", 5)), 11, 10, Control.Idle)
                .Tweak("brave", c => { SturdyNamed(null)(c); SaveTweak(30)(c); })
                .Pc("timid", ActorSource.Stats(() => RulesScenarios.Fighter("Timid", 5)), 10, 13, Control.Idle)
                .Tweak("timid", c => { SturdyNamed(null)(c); SaveTweak(-30)(c); })
                .Force(20, 10, GameManager.FrightfulPresenceSaveContext, -1)
                .Initiative("orc", "brave", "timid")
                .Turn("orc", 1, Step.Attack("brave"))
                .Turn("orc", 2, Step.Attack("brave"))
                .Expect("The orc attacked in both rounds", Expect.All(
                    Expect.StepStatus("orc", 1, "Attack", 0, "done"), Expect.StepStatus("orc", 2, "Attack", 0, "done")))
                .Expect("Round 1: the orc's attack fires its presence", PresenceLineCount("Dread Orc", "attacks", 1, 1))
                .Expect("Brave resists once and is immune afterwards (24 hours, kept for the encounter)", v =>
                {
                    List<TraceEvent> lines = PresenceSaveLines(v, "Brave");
                    return lines.Count == 1 && lines[0].Round == 1 && (lines[0].Str("text") ?? "").Contains("immune to Dread Orc's for 24 hours")
                        ? ExpectResult.Pass("resisted in round 1, immune", lines[0].Seq)
                        : ExpectResult.Fail(lines.Count + " line(s)");
                })
                .Expect("Timid is shaken in round 1 and not asked again while shaken by the orc", Expect.All(
                    SaveLineCount("Timid", 1), CondAdded("timid", CombatConditionType.Shaken, true)))
                .Expect("Round 2's attack has no witness left: no announcement", PresenceLineCount("Dread Orc", "attacks", 2, 0))
                .Build();
        }

        /// <summary>
        /// D12 for every bearer (CRE-039): a controllable fighter 8 with a frightful presence (a PC-cast summon would carry
        /// one, PHB p.285) attacks Orc Alpha through the Attack button. The shared attack-sequence executor fires the presence:
        /// both orc warriors in 30 ft (1 HD each) fail (forced natural 1) and are panicked; Squire, the knight's ally, is no
        /// opponent and is never asked.
        /// </summary>
        private static ScenarioDef FrightfulPresenceControllableBearerUi()
        {
            return Rules("rules/frightful-presence-controllable-bearer-ui", "A controllable bearer's attack fires its frightful presence on its opponents only (MM p.309; D12, CRE-039)")
                .Covers("CRE-039", "AI-041", "D12", "MM p.309", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("knight", ActorSource.Stats(() => RulesScenarios.Fighter("Dread Knight", 8)), 10, 10, Control.Ui)
                .Tweak("knight", c => { Fearsome(0, null)(c); RulesScenarios.StripOffHand(c); })
                .Pc("squire", ActorSource.Stats(() => RulesScenarios.Fighter("Squire", 2)), 10, 12, Control.Idle)
                .Tweak("squire", SturdyNamed(null))
                .Npc("orcA", "orc_warrior", 11, 10, Control.Scripted)
                .Tweak("orcA", SturdyNamed("Orc Alpha"))
                .Npc("orcB", "orc_warrior", 13, 13, Control.Scripted)
                .Tweak("orcB", SturdyNamed("Orc Beta"))
                .Force(20, 1, GameManager.FrightfulPresenceSaveContext, -1)
                .Initiative("knight", "squire", "orcA", "orcB")
                .Turn("knight", 1, Step.Attack("orcA"))
                .Expect("The knight's turn is a Ui turn", Expect.Controller("knight", "ui"))
                .Expect("The Attack step is done", Expect.StepStatus("knight", 1, "Attack", 0, "done"))
                .Expect("The knight's attack fires its presence once", PresenceLineCount("Dread Knight", "attacks", 1, 1))
                .Expect("Both orcs (1 HD) are panicked", Expect.All(
                    CondAdded("orcA", CombatConditionType.Panicked, true), CondAdded("orcB", CombatConditionType.Panicked, true)))
                .Expect("The squire, an ally, is never asked", SaveLineCount("Squire", 0))
                .Build();
        }

        /// <summary>
        /// D12 for a maneuver (MM p.309: the presence fires when the bearer attacks; a trip is an attack, PHB p.158): a
        /// controllable fighter 8 with a frightful presence trips Orc Alpha from the Special Attack menu as the first attack
        /// of its turn. The shared maneuver core (ExecuteManeuver) fires the presence before the trip's touch attack (forced
        /// natural 1, so nothing follows): Orc Alpha and Orc Beta (1 HD, forced natural 1 saves) are panicked. The knight then
        /// takes a 5-foot step that brings Orc Gamma, 35 ft away at the trip, within 30 ft, and attacks Orc Alpha with the
        /// Attack button: the same attack action has made its first attack, so the presence does not fire again and Orc Gamma
        /// is never asked. Before the step 19 review only the AI's in-sequence maneuver substitute fired it.
        /// </summary>
        private static ScenarioDef FrightfulPresenceOnManeuverUi()
        {
            return Rules("rules/frightful-presence-on-maneuver-ui", "A trip from the Special Attack menu fires the bearer's frightful presence once per attack action (MM p.309; D12, PC_NPC_PARITY)")
                .Covers("AI-041", "CRE-039", "D12", "MM p.309", "PHB p.158", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("knight", ActorSource.Stats(() => RulesScenarios.Fighter("Dread Knight", 8)), 10, 10, Control.Ui)
                .Tweak("knight", c => { Fearsome(0, null)(c); RulesScenarios.StripOffHand(c); })
                .Npc("orcA", "orc_warrior", 11, 10, Control.Scripted)
                .Tweak("orcA", c => { SturdyNamed("Orc Alpha")(c); c.Stats.CanMakeAttacksOfOpportunity = false; })
                .Npc("orcB", "orc_warrior", 13, 13, Control.Scripted)
                .Tweak("orcB", c => { SturdyNamed("Orc Beta")(c); c.Stats.CanMakeAttacksOfOpportunity = false; })
                .Npc("orcC", "orc_warrior", 10, 17, Control.Scripted)
                .Tweak("orcC", c => { SturdyNamed("Orc Gamma")(c); c.Stats.CanMakeAttacksOfOpportunity = false; })
                .Force(20, 1, GameManager.FrightfulPresenceSaveContext, -1)
                .Force(20, 1, "Trip touch attack", -1)
                .Initiative("knight", "orcA", "orcB", "orcC")
                .Turn("knight", 1, Step.Maneuver(SpecialAttackType.Trip, "orcA"), Step.FiveFootStep(10, 11), Step.Attack("orcA"))
                .Expect("The knight's turn is a Ui turn", Expect.Controller("knight", "ui"))
                .Expect("The menu trip is made", Expect.StepStatus("knight", 1, "Maneuver", 0, "done"))
                .Expect("The trip fires the presence once, before its touch attack (D12)", PresenceBeforeTrip("knight", "Dread Knight"))
                .Expect("Orc Alpha and Orc Beta (1 HD) are panicked", Expect.All(
                    CondAdded("orcA", CombatConditionType.Panicked, true), CondAdded("orcB", CombatConditionType.Panicked, true)))
                .Expect("The later Attack click of the same action does not fire it again: Orc Gamma, in 30 ft after the 5-foot step, is never asked", v =>
                {
                    ExpectResult stepped = Expect.StepStatus("knight", 1, "FiveFootStep", 0, "done")(v);
                    ExpectResult attacked = Expect.StepStatus("knight", 1, "Attack", 0, "done")(v);
                    if (stepped.Status != "pass" || attacked.Status != "pass")
                        return ExpectResult.Inconclusive("5-foot step " + stepped.Status + ", attack " + attacked.Status);
                    return SaveLineCount("Orc Gamma", 0)(v);
                })
                .Build();
        }

        /// <summary>
        /// D12 for a maneuver, the AI twin of rules/frightful-presence-on-maneuver-ui: a Scripted 6-HD orc with a frightful
        /// presence trips the hero through the NPC maneuver executor (TryNPCSpecialAttackByTypeForAI, the catalog's maneuver
        /// entry, then ExecuteManeuver). The presence fires before the trip's touch attack (forced natural 1), and the hero
        /// (5 HD) fails (forced natural 1) and is shaken. Before the step 19 review this path never fired it.
        /// </summary>
        private static ScenarioDef FrightfulPresenceOnManeuverAi()
        {
            return Rules("rules/frightful-presence-on-maneuver-ai", "An NPC trip through the catalog's maneuver entry fires the bearer's frightful presence (MM p.309; D12, PC_NPC_PARITY)")
                .Covers("AI-041", "CRE-039", "D12", "MM p.309", "PHB p.158", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Npc("orc", "orc_warrior", 10, 10, Control.Scripted)
                .Tweak("orc", Fearsome(6, "Dread Orc"))
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 5)), 11, 10, Control.Idle)
                .Tweak("hero", c => { SturdyNamed(null)(c); c.Stats.CanMakeAttacksOfOpportunity = false; })
                .Force(20, 1, GameManager.FrightfulPresenceSaveContext, -1)
                .Force(20, 1, "Trip touch attack", -1)
                .Initiative("orc", "hero")
                .Turn("orc", 1, Step.Maneuver(SpecialAttackType.Trip, "hero"))
                .Expect("The trip is made", Expect.StepStatus("orc", 1, "Maneuver", 0, "done"))
                .Expect("The trip fires the presence once, before its touch attack (D12)", PresenceBeforeTrip("orc", "Dread Orc"))
                .Expect("The hero (5 HD) fails and is shaken", Expect.All(
                    CondAdded("hero", CombatConditionType.Shaken, true), CondAdded("hero", CombatConditionType.Panicked, false)))
                .Build();
        }

        /// <summary>One presence line by <paramref name="bearerName"/> in round 1, before <paramref name="key"/>'s trip and its touch attack.</summary>
        private static Func<TraceView, ExpectResult> PresenceBeforeTrip(string key, string bearerName)
            => v =>
            {
                List<TraceEvent> lines = PresenceLines(v, bearerName, "attacks", 1);
                TraceEvent trip = v.Maneuvers(key, SpecialAttackType.Trip).FirstOrDefault();
                TraceEvent touch = v.Log(@"Touch attack: d20").FirstOrDefault(e => e.Round == 1);
                if (lines.Count != 1)
                    return ExpectResult.Fail(lines.Count + " presence line(s)");
                if (trip == null)
                    return ExpectResult.Inconclusive("no trip resolved");
                bool before = lines[0].Seq < trip.Seq && (touch == null || lines[0].Seq < touch.Seq);
                return before ? ExpectResult.Pass("presence before the trip", lines[0].Seq) : ExpectResult.Fail("after the trip", lines[0].Seq);
            };

        // ── Auras (batch 13 step 19, D13) ───────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// D13 (MM p.126: all creatures other than mouthers within 60 ft): an AI-run gibbering mouther's gibbering, at the
        /// start of its acting turn, reaches its own ally (an orc warrior on its team) and the hero, both failing (every
        /// aura save forced to a natural 1) and confused; the second mouther is never asked, and its own gibbering (last in
        /// the order) spares the first. Before step 19 the gibbering spared the mouther's allies.
        /// </summary>
        private static ScenarioDef GibberingHitsAlliesAi()
        {
            return Rules("rules/gibbering-hits-allies-ai", "Gibbering affects every creature in range but mouthers, the mouther's allies included (MM p.126; D13, CRE-039)")
                .Covers("CRE-039", "D13", "MM p.126", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Npc("mouther", "gibbering_mouther", 10, 10, Control.Ai)
                .Tweak("mouther", SturdyNamed("Mouther One"))
                .Npc("orc", "orc_warrior", 12, 10, Control.Idle)
                .Tweak("orc", SturdyNamed("Orc Ally"))
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 5)), 14, 10, Control.Idle)
                .Tweak("hero", SturdyNamed(null))
                .Npc("mouther2", "gibbering_mouther", 10, 13, Control.Idle)
                .Tweak("mouther2", SturdyNamed("Mouther Two"))
                .Npc("centipede", "monstrous_centipede_medium", 8, 10, Control.Idle)
                .Tweak("centipede", SturdyNamed("Centipede Ally"))
                .Force(20, 1, GameManager.AuraSaveDiceContext, -1)
                .Initiative("mouther", "orc", "hero", "mouther2", "centipede")
                .Expect("The first mouther's turn is an AI turn", Expect.Controller("mouther", "ai"))
                .Expect("Its gibbering fires at its turn start and confuses its ally and the hero (MM p.126)", v =>
                {
                    TraceEvent echo = v.Log("Mouther One's Gibbering echoes").FirstOrDefault(e => e.Round == 1);
                    TraceEvent ally = v.Log("Orc Ally succumbs to Gibbering").FirstOrDefault();
                    TraceEvent hero = v.Log(@"(^|[^A-Za-z])Hero succumbs to Gibbering").FirstOrDefault();
                    TraceEvent orcTurn = v.TurnsOf("orc").FirstOrDefault();
                    if (echo == null || ally == null || hero == null)
                        return ExpectResult.Fail("echo " + (echo != null) + ", ally " + (ally != null) + ", hero " + (hero != null));
                    bool beforeOrc = orcTurn == null || (ally.Seq < orcTurn.Seq && hero.Seq < orcTurn.Seq);
                    return beforeOrc ? ExpectResult.Pass("both confused in the mouther's turn", ally.Seq) : ExpectResult.Fail("after the orc's turn start", ally.Seq);
                })
                .Expect("The ally and the hero are confused", Expect.All(
                    CondAdded("orc", CombatConditionType.Confused, true), CondAdded("hero", CombatConditionType.Confused, true)))
                .Expect("No mouther is ever asked (MM p.126: other than mouthers)", v =>
                {
                    int n = v.Log(@"Mouther (One|Two) (succumbs to|resists) Gibbering").Count;
                    return n == 0 ? ExpectResult.Pass("none") : ExpectResult.Fail(n + " line(s)");
                })
                .Expect("The centipede ally (vermin, mindless) is immune to the mind-affecting gibbering and never asked (PHB p.309)", v =>
                {
                    int n = v.Log(@"Centipede Ally (succumbs to|resists) Gibbering").Count;
                    bool confused = v.Conds("centipede", CombatConditionType.Confused).Any(e => e.Bool("added"));
                    return n == 0 && !confused ? ExpectResult.Pass("never asked") : ExpectResult.Fail(n + " line(s), confused " + confused);
                })
                .Build();
        }

        /// <summary>
        /// D13 (AI-053): an AI-run gibbering mouther starts its turn confused, and its d% is forced to 30 (babble, PHB p.212):
        /// a compelled turn. Its gibbering still fires at the start of that turn (before step 19 the compelled turn skipped
        /// it), the hero fails (forced natural 1) and is confused; the free spittle, a choice, is not used on the compelled
        /// turn, though the hero is 15 ft away.
        /// </summary>
        private static ScenarioDef AuraOnCompelledTurnAi()
        {
            return Rules("rules/aura-on-compelled-turn-ai", "A confused bearer's aura still fires at its turn start; its free spittle does not (D13, AI-053)")
                .Covers("AI-053", "CRE-039", "D13", "MM p.126", "PHB p.212", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Npc("mouther", "gibbering_mouther", 10, 10, Control.Ai)
                .Tweak("mouther", SturdyNamed("Confused Mouther"))
                .StartCondition("mouther", CombatConditionType.Confused, 3)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Hero", 5)), 13, 10, Control.Idle)
                .Tweak("hero", SturdyNamed(null))
                .Force(100, 30, null, 1)
                .Force(20, 1, GameManager.AuraSaveDiceContext, -1)
                .Initiative("mouther", "hero")
                .Expect("The mouther is confused and babbles (d% 30): a compelled turn", v =>
                    v.Log(@"Confused Mouther is confused \(d% 30\)").Count == 1 ? ExpectResult.Pass("babble") : ExpectResult.Fail("no d% 30 line"))
                .Expect("Its gibbering fires at the start of the compelled turn and confuses the hero (D13)", v =>
                {
                    TraceEvent echo = v.Log("Confused Mouther's Gibbering echoes").FirstOrDefault(e => e.Round == 1);
                    TraceEvent hero = v.Log(@"(^|[^A-Za-z])Hero succumbs to Gibbering").FirstOrDefault(e => e.Round == 1);
                    return echo != null && hero != null ? ExpectResult.Pass("fired", echo.Seq) : ExpectResult.Fail("echo " + (echo != null) + ", hero " + (hero != null));
                })
                .Expect("No free spittle on the compelled turn (D13)", LogCount("Confused Mouther uses Spittle against", 1, 0))
                .Build();
        }
    }
}
#endif
