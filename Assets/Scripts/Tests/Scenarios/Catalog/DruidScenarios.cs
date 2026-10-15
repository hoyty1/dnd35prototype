#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace Tests.Scenarios
{
    /// <summary>
    /// Druid Wild Shape on one PC/NPC rules path (CHR-020; PHB p.37, p.263, p.197): WildShapeRules takes the form for
    /// the PC Wild Shape button and its form list, the scripted step (GameManager.TryWildShapeForAI) and the AI
    /// (AIProfile.DefaultWildShapeForm); in the form the druid fights with its natural attacks on the shared PC and NPC
    /// attack paths. The druid is a Human druid 5 built from stats (STR 12, DEX 10, CON 12, WIS 16) with the class kit
    /// (hide armor, scimitar, light wooden shield): BAB +3. As a black bear (bestiary entry black_bear, MM p.269: Medium,
    /// 3 HD, Str 19, Dex 13, Con 15, natural armor +2, 2 claws 1d4 and a bite 1d6) her claws are at +7 (BAB 3, Str 4)
    /// and the secondary bite at +2. NPC data checked through the NPC database files on 2026-10-14: black_bear,
    /// target_dummy (Commoner 1, 50 base HP, AC 6).
    /// </summary>
    public static class DruidScenarios
    {
        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return S("druid-wild-shape-ui", WildShapeUi);
            yield return S("druid-wild-shape-scripted", WildShapeScripted);
            yield return S("druid-wild-shape-ai", WildShapeAi);
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("DruidScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title) => Scenario.Define(id, title).Tags("rules");

        /// <summary>A Human druid: STR 12, DEX 10, CON 12, WIS 16; BAB from the druid table.</summary>
        internal static CharacterStats Druid(string name, int level)
        {
            var s = new CharacterStats(
                name: name, level: level, characterClass: "Druid",
                str: 12, dex: 10, con: 12, wis: 16, intelligence: 10, cha: 10,
                bab: 0, armorBonus: 0, shieldBonus: 0,
                damageDice: 6, damageCount: 1, bonusDamage: 0,
                baseSpeed: 6, atkRange: 1, baseHitDieHP: 8 * level, raceName: "Human");
            s.CharacterAlignment = Alignment.TrueNeutral;
            s.EnsureMulticlassDataInitialized();
            return s;
        }

        private static void Sturdy(CharacterController c)
        {
            c.Stats.AdjustMaxHP(300);
            c.Stats.CurrentHP += 300;
        }

        private static bool InBearForm(CharacterController d)
        {
            CharacterStats s = d.Stats;
            return WildShapeRules.IsWildShaped(s) && s.ActiveWildShape.FormId == "black_bear"
                && s.STR == 19 && s.DEX == 13 && s.CON == 15 && s.NaturalArmorBonus == 2 && s.CreatureType == "Animal"
                && d.GetEquippedMainWeapon() == null && s.NaturalAttacks.Exists(a => a.Name == "Claw")
                && WildShapeRules.UsesLeft(s) == 0 && !d.Actions.HasStandardAction;
        }

        /// <summary>
        /// The first non-AoO attack by <paramref name="by"/> in <paramref name="round"/> rolls <paramref name="dice"/> at
        /// <paramref name="mod"/>. The trace's weapon field names every natural attack "Unarmed strike" (TST-034), so the
        /// natural attack is told by its damage dice.
        /// </summary>
        private static Func<TraceView, ExpectResult> FirstAttackWith(string by, int round, string dice, int mod)
        {
            return v =>
            {
                TraceEvent a = v.Attacks(by, aoo: false, round: round).FirstOrDefault();
                if (a == null)
                    return ExpectResult.Fail("no attack by " + by + " in round " + round);
                string d = a.Str("dice") ?? string.Empty;
                if (d != dice || a.Int("mod") != mod)
                    return ExpectResult.Fail("attack for " + d + " at " + a.Int("mod") + ", expected " + dice + " at " + mod, a.Seq);
                return ExpectResult.Pass(d + " at " + CharacterStats.FormatMod(mod) + " (terms " + a.Str("dmgTerms") + ")", a.Seq);
            };
        }

        /// <summary>
        /// The PC path: the Wild Shape button and the black bear's option in the form list (round 1), a claw through the
        /// natural-attack button (round 2), and the Wild Shape button again to change back (round 3).
        /// </summary>
        private static ScenarioDef WildShapeUi()
        {
            return Rules("rules/druid-wild-shape-ui", "A PC druid 5 wild shapes into a black bear, claws at +7, and changes back (PHB p.37; CHR-020)")
                .Covers("CHR-020", "PHB p.37", "PHB p.263", "PC_NPC_PARITY")
                .MaxRounds(3)
                .Pc("druid", ActorSource.Stats(() => Druid("Druid", 5)), 10, 10, Control.Ui)
                .Npc("dummy", "target_dummy", 11, 10, Control.Scripted)
                .Tweak("dummy", Sturdy)
                .Initiative("druid", "dummy")
                .Turn("druid", 1, Step.WildShape("black_bear"),
                    Step.Assert("in black bear form: Str 19, Dex 13, Con 15, natural armor +2, animal, gear melded, the use and the standard action spent",
                        ctx => InBearForm(ctx.Get("druid"))))
                .Turn("druid", 2, Step.NaturalAttack("dummy", "Claw"))
                .Turn("druid", 3, Step.RevertWildShape(),
                    Step.Assert("her own form again: Str 12, humanoid, scimitar in hand", ctx =>
                    {
                        CharacterController d = ctx.Get("druid");
                        return !WildShapeRules.IsWildShaped(d.Stats) && d.Stats.STR == 12 && d.Stats.CreatureType == "Humanoid"
                            && d.GetEquippedMainWeapon() != null && !d.Actions.HasStandardAction;
                    }))
                .Turn("dummy", 0, Step.Pass())
                .Expect("The Wild Shape step is done", Expect.StepStatus("druid", 1, "WildShape", 0, "done"))
                .Expect("Round 2: a bear claw (1d4) at +7 (BAB 3, Str 19)", FirstAttackWith("druid", 2, "1d4", 7))
                .Expect("The change back is done", Expect.StepStatus("druid", 3, "RevertWildShape", 0, "done"))
                .Expect("The form and the change back are as the rules say", Expect.AssertsPass())
                .Build();
        }

        /// <summary>
        /// The AI-path executor: the scripted step takes the form through GameManager.TryWildShapeForAI (round 1); in
        /// round 2 NPCPerformAttackForAI makes the bear's full attack: claw +7, claw +7, bite +2.
        /// </summary>
        private static ScenarioDef WildShapeScripted()
        {
            return Rules("rules/druid-wild-shape-scripted", "A scripted druid 5 wild shapes into a black bear and full attacks: +7/+7/+2 (PHB p.37; CHR-020)")
                .Covers("CHR-020", "PHB p.37", "MM p.312", "PC_NPC_PARITY")
                .MaxRounds(2)
                .Pc("druid", ActorSource.Stats(() => Druid("Druid", 5)), 10, 10, Control.Scripted)
                .Npc("dummy", "target_dummy", 11, 10, Control.Scripted)
                .Tweak("dummy", Sturdy)
                .Initiative("druid", "dummy")
                .Turn("druid", 1, Step.WildShape("black_bear"),
                    Step.Assert("in black bear form", ctx => InBearForm(ctx.Get("druid"))))
                .Turn("druid", 2, Step.Attack("dummy"))
                .Turn("dummy", 0, Step.Pass())
                .Expect("The Wild Shape step is done", Expect.StepStatus("druid", 1, "WildShape", 0, "done"))
                .Expect("Round 2: the bear's full attack, claws +7 and the secondary bite +2", Expect.ModSequence("druid", 2, 7, 7, 2))
                .Expect("In black bear form after round 1", Expect.AssertsPass())
                .Build();
        }

        /// <summary>
        /// The AI's own choice (AIProfile.DefaultWildShapeForm): a druid 5 run by the AI, with a foe next to it, takes the
        /// animal form with the best expected full attack in round 1 (it would keep casting with no foe adjacent), and in
        /// round 2 full attacks with that form's natural attacks (every form it can pick has more than one), not its
        /// scimitar (one attack at BAB +3).
        /// </summary>
        private static ScenarioDef WildShapeAi()
        {
            return Rules("rules/druid-wild-shape-ai", "The AI druid 5 next to a foe wild shapes, then fights with natural attacks (PHB p.37; CHR-020)")
                .Covers("CHR-020", "PHB p.37", "PC_NPC_PARITY")
                .MaxRounds(2)
                .Pc("druid", ActorSource.Stats(() => Druid("Druid", 5)), 10, 10, Control.Ai)
                .Npc("dummy", "target_dummy", 11, 10, Control.Scripted)
                .Tweak("dummy", Sturdy)
                .Initiative("druid", "dummy")
                .Turn("dummy", 1, Step.Assert("the druid took an animal form in round 1", ctx =>
                    {
                        CharacterStats s = ctx.Get("druid").Stats;
                        return WildShapeRules.IsWildShaped(s) && s.CreatureType == "Animal" && s.NaturalAttacks.Count > 0;
                    }), Step.Pass())
                .Turn("dummy", 2, Step.Pass())
                .Expect("The druid is in animal form after its first turn", Expect.AssertsPass())
                .Expect("Round 2: the druid full attacks with the form's natural attacks (the scimitar gives one attack at BAB +3)", v =>
                {
                    List<TraceEvent> attacks = v.Attacks("druid", aoo: false, round: 2);
                    string detail = string.Join(", ", attacks.Select(a => a.Str("dice") + " at " + a.Int("mod")));
                    return attacks.Count >= 2
                        ? ExpectResult.Pass(attacks.Count + " natural attacks: " + detail, attacks.Select(a => a.Seq).ToArray())
                        : ExpectResult.Fail(attacks.Count + " attack(s) in round 2: " + detail, attacks.Select(a => a.Seq).ToArray());
                })
                .Build();
        }
    }
}
#endif
