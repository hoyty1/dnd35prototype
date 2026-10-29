#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Tests.Scenarios
{
    /// <summary>
    /// Batch 12 step 24 (docs/designs/pc_npc_parity_batch12.md, b12-factory-leftovers): the creature-factory leftovers.
    /// Armor slows every creature through the one speed rule (CharacterStats.EffectiveSpeedFeet, PHB p.123; ITM-010), and
    /// the Greater Lion's Shield's dire lion spawns through GameManager's own summon path (GameManager.SpawnItemSummon:
    /// initiative, the AI behaviour entry, the summon tracker; CRE-005). The cleric's AI conversion is
    /// rules/npc-cleric-converts-cure (CastPipelineScenarios, with its cast helpers; CHR-070).
    /// </summary>
    public static class FactoryLeftoverScenarios
    {
        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return S("armor-speed-npc", ArmorSpeedNpc);
            yield return S("lion-shield-summon", LionShieldSummon);
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("FactoryLeftoverScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title)
            => Scenario.Define(id, title).Tags("rules", "creature-factory").Covers("PC_NPC_PARITY", "batch 12 step 24");

        private const string FactPrefix = "fact:";

        private static void Fact(ScenarioContext ctx, string key, object value) => ctx.Note(FactPrefix + key + "=" + value);

        private static string FactOf(TraceView v, string key)
        {
            string prefix = FactPrefix + key + "=";
            TraceEvent note = v.Of("note").LastOrDefault(e => (e.Str("text") ?? "").StartsWith(prefix, StringComparison.Ordinal));
            return note != null ? note.Str("text").Substring(prefix.Length) : null;
        }

        private static Func<TraceView, ExpectResult> FactIs(string key, object want)
            => v =>
            {
                string got = FactOf(v, key);
                string w = Convert.ToString(want, System.Globalization.CultureInfo.InvariantCulture);
                if (got == null)
                    return ExpectResult.Inconclusive("no fact " + key);
                return got == w ? ExpectResult.Pass(key + "=" + got) : ExpectResult.Fail(key + "=" + got + ", expected " + w);
            };

        private static Func<TraceView, ExpectResult> FactsAre(params (string key, object want)[] facts)
            => Expect.All(facts.Select(f => FactIs(f.key, f.want)).ToArray());

        private static string P(Vector2Int p) => p.x + "," + p.y;

        private static void Sturdy(CharacterController c)
        {
            c.Stats.AdjustMaxHP(100);
            c.Stats.CurrentHP += 100;
        }

        // ── rules/armor-speed-npc ──────────────────────────────────────

        /// <summary>
        /// ITM-010 (PHB p.123 Table 7-6, p.14; MM p.199): every mover is slowed by medium or heavy armor through the same
        /// speed rule. Scripted movers each move 12 squares east: the human paladin in full plate (entry speed 30 ft, no
        /// longer baked to 20 ft) and the Stats-built PC fighter in the Fighter kit's scale mail move 20 ft (4 squares); the
        /// dwarf warrior in scale mail keeps its 20 ft (dwarves are not slowed, PHB p.14); the ogre in hide armor (40 ft
        /// base) moves 30 ft (6 squares, the MM's printed speed); an unarmored PC fighter moves 30 ft. Before ITM-010 armor
        /// slowed no one, and the NPC entries had the reduced speed written into their data, so an NPC paladin moved 20 ft
        /// and a PC paladin 30 ft.
        /// </summary>
        private static ScenarioDef ArmorSpeedNpc()
        {
            return Rules("rules/armor-speed-npc", "Medium and heavy armor slow PCs and NPCs alike, dwarves excepted (PHB p.123, p.14; ITM-010)")
                .Covers("ITM-010", "PHB p.123", "PHB p.14", "MM p.199")
                .MaxRounds(1)
                .Npc("paladin", "human_paladin_3", 4, 10, Control.Scripted)
                .Npc("dwarf", "dwarf_warrior", 4, 6, Control.Scripted)
                .Npc("ogre", "ogre", 4, 13, Control.Scripted)
                .Pc("fighter", ActorSource.Stats(() => RulesScenarios.Fighter("Armored Fighter", 2)), 4, 2, Control.Scripted)
                .Pc("bare", ActorSource.Stats(() => RulesScenarios.Fighter("Unarmored Fighter", 2)), 4, 17, Control.Scripted)
                .Tweak("bare", RulesScenarios.StripArmor)
                .Initiative("paladin", "dwarf", "ogre", "fighter", "bare")
                .Turn("paladin", 1, MoveEast("paladin", 4, 10))
                .Turn("dwarf", 1, MoveEast("dwarf", 4, 6))
                .Turn("ogre", 1, MoveEast("ogre", 4, 13))
                .Turn("fighter", 1, MoveEast("fighter", 4, 2))
                .Turn("bare", 1, MoveEast("bare", 4, 17))
                .Expect("The paladin's entry keeps its unarmored 30 ft; its full plate slows it to 20 ft (4 squares)",
                    FactsAre(("paladin.base", 30), ("paladin.speed", 20), ("paladin.pos", "8,10")))
                .Expect("The PC fighter in scale mail is slowed the same way: 20 ft", FactsAre(("fighter.speed", 20), ("fighter.pos", "8,2")))
                .Expect("The dwarf in scale mail keeps its 20 ft (PHB p.14)", FactsAre(("dwarf.speed", 20), ("dwarf.pos", "8,6")))
                .Expect("The ogre in hide armor moves 30 ft of its 40 ft (MM p.199)", FactsAre(("ogre.base", 40), ("ogre.speed", 30), ("ogre.pos", "10,13")))
                .Expect("The unarmored PC fighter moves 30 ft", FactsAre(("bare.speed", 30), ("bare.pos", "10,17")))
                .Expect("The checks pass", Expect.AssertsPass())
                .Build();
        }

        /// <summary>Records the mover's entry speed and speed, moves it 12 squares east and records where it stopped.</summary>
        private static Step[] MoveEast(string key, int x, int y)
        {
            return new[]
            {
                Step.Assert(key + " speed", ctx =>
                {
                    CharacterController c = ctx.Get(key);
                    Fact(ctx, key + ".base", c.Stats.BaseSpeed * 5);
                    Fact(ctx, key + ".speed", c.Stats.EffectiveSpeedFeet);
                    return MovementService.SingleMoveBudgetSquares(c) == c.Stats.EffectiveSpeedFeet / 5;
                }),
                Step.Move(x + 12, y),
                Step.Assert(key + " moved", ctx => { Fact(ctx, key + ".pos", P(ctx.Get(key).GridPosition)); return true; })
            };
        }

        // ── rules/lion-shield-summon ───────────────────────────────────

        /// <summary>
        /// CRE-005: a Scripted hero calls the Greater Lion's Shield's dire lion (LionsShieldBehavior.Activate with no target).
        /// It appears beside the hero through GameManager.SpawnItemSummon, the summon spell's spawn: on the hero's side, run
        /// by the AI (the hero is not player-controlled), with its MM feats (MM p.63: Alertness, from the definition; the old
        /// LionsShieldBehavior.BuildSummonStats gave none), an AI behaviour entry (one added to each of NPCs and the behaviour
        /// list, AI-015), a place in the initiative after the hero, so it takes its own turn, and the summon tracker's
        /// 10 rounds, one gone at the round 2 boundary. The hero has Augment Summoning, which the lion must not get (PHB p.89:
        /// creatures conjured with a summon spell only), so it keeps the MM's Str 25 and Con 17. A goblin far away (Sturdy)
        /// keeps the fight going.
        /// </summary>
        private static ScenarioDef LionShieldSummon()
        {
            return Rules("rules/lion-shield-summon", "The Greater Lion's Shield's dire lion joins the initiative and the summon tracker like a summoned creature (CRE-005)")
                .Covers("CRE-005", "AI-015", "CORE-023", "MM p.63", "PHB p.89")
                .MaxRounds(2)
                .Pc("hero", ActorSource.Stats(() => RulesScenarios.Fighter("Lion Shield Hero", 6)), 2, 10, Control.Scripted)
                .Npc("gob", "goblin", 18, 10, Control.Idle)
                .Tweak("gob", Sturdy)
                .Initiative("hero", "gob")
                .Turn("hero", 1,
                    Step.Assert("the shield calls the lion", ctx =>
                    {
                        CharacterController hero = ctx.Get("hero");
                        int npcsBefore = ctx.Gm.NPCs.Count;
                        int behaviorsBefore = NpcBehaviorCount(ctx.Gm);
                        if (!hero.Stats.HasFeat("Augment Summoning"))
                            hero.Stats.Feats.Add("Augment Summoning");
                        var shield = new LionsShieldBehavior(isGreater: true);
                        shield.Initialize(null);
                        shield.OnEquip(hero);
                        var notes = new List<string>();
                        bool activated = shield.Activate(null, notes);
                        Fact(ctx, "activated", activated);
                        CharacterController lion = ctx.Gm.NPCs.Skip(npcsBefore).FirstOrDefault(n => n != null && n.Stats != null && n.Stats.CharacterName == "Dire Lion");
                        Fact(ctx, "lion", lion != null);
                        if (lion == null)
                            return true;
                        Fact(ctx, "lion.team", lion.Team);
                        Fact(ctx, "lion.controllable", lion.IsControllable);
                        Fact(ctx, "lion.summoned", ctx.Gm.IsSummonedCreature(lion));
                        Fact(ctx, "lion.alertness", lion.Stats.HasFeat("Alertness"));
                        Fact(ctx, "lion.bab", lion.Stats.BaseAttackBonus);
                        Fact(ctx, "lion.str", lion.Stats.BaseSTR);
                        Fact(ctx, "lion.con", lion.Stats.BaseCON);
                        Fact(ctx, "lion.adjacent", SquareGridUtils.GetDistance(hero.GridPosition, lion.GridPosition) <= 2);
                        // The spawn adds one entry to each list (AI-015).
                        Fact(ctx, "npcs.added", ctx.Gm.NPCs.Count - npcsBefore);
                        Fact(ctx, "behaviors.added", NpcBehaviorCount(ctx.Gm) - behaviorsBefore);
                        ctx.Gm.TryGetSummonRemainingRounds(lion, out int remaining, out int total);
                        Fact(ctx, "r1.rounds", remaining + "/" + total);
                        Fact(ctx, "second", shield.Activate(null, new List<string>()));
                        return true;
                    }),
                    Step.Pass())
                .Turn("hero", 2,
                    Step.Assert("the tracker counts the lion's rounds", ctx =>
                    {
                        CharacterController lion = ctx.Gm.NPCs.FirstOrDefault(n => n != null && n.Stats != null && n.Stats.CharacterName == "Dire Lion");
                        int remaining = -1, total = -1;
                        if (lion != null)
                            ctx.Gm.TryGetSummonRemainingRounds(lion, out remaining, out total);
                        Fact(ctx, "r2.rounds", remaining + "/" + total);
                        return true;
                    }),
                    Step.Pass())
                .Expect("The lion appears beside the hero, on the hero's side, run by the AI", FactsAre(
                    ("activated", true), ("lion", true), ("lion.team", CharacterTeam.Player), ("lion.controllable", false), ("lion.adjacent", true)))
                .Expect("It is built by the summon spawn: its MM feats and BAB (MM p.63: Alertness, BAB +6)", FactsAre(("lion.alertness", true), ("lion.bab", 6)))
                .Expect("The wielder's Augment Summoning does not reach an item's creature (PHB p.89; MM p.63 Str 25, Con 17)", FactsAre(("lion.str", 25), ("lion.con", 17)))
                .Expect("It gets an AI behaviour entry with its NPCs entry, so the lists stay parallel (AI-015)", FactsAre(("npcs.added", 1), ("behaviors.added", 1)))
                .Expect("The summon tracker holds it for 10 rounds and counts one off at the round 2 boundary", FactsAre(
                    ("lion.summoned", true), ("r1.rounds", "10/10"), ("r2.rounds", "9/10")))
                .Expect("The shield's one summon a day is spent", FactIs("second", false))
                .Expect("The lion takes its own turn in the initiative", Expect.Count("turn_start",
                    e => (e.Str("actor") ?? "").StartsWith("summon:Dire Lion", StringComparison.Ordinal), 1))
                .Expect("The checks pass", Expect.AssertsPass())
                .Build();
        }

        /// <summary>The length of GameManager's AI behaviour list, index-parallel to NPCs from the pool's end (AI-015).</summary>
        private static int NpcBehaviorCount(GameManager gm)
        {
            var field = typeof(GameManager).GetField("_npcAIBehaviors", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var list = field != null ? field.GetValue(gm) as System.Collections.ICollection : null;
            return list != null ? list.Count : -1;
        }
    }
}
#endif
