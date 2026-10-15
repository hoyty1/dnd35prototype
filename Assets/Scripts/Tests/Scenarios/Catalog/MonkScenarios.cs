#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace Tests.Scenarios
{
    /// <summary>
    /// Monk flurry of blows through the shared attack sequence (CHR-003, CRE-042; PHB p.40-41, Table 3-10): one extra
    /// attack at the highest base attack bonus, the flurry penalty on every attack (-2 to 4th level, -1 at 5th-8th), the
    /// full Strength bonus on damage even with a two-handed quarterstaff, declared by the PC Flurry button (Ui), by a
    /// scripted step (CharacterController.TryBeginFlurryOfBlows) or by the AI before its melee sequence.
    /// NPC data checked through the NPC database files on 2026-10-14: human_monk_3 (Monk 3, STR 14, BAB +2, no weapon in
    /// hand, no natural attacks since CRE-042), human_monk_7 (Monk 7, STR 14, BAB +5, the same); target_dummy (Commoner 1,
    /// 50 base hit-die HP, no weapon). The PC monk is a Human monk 3, STR 16, with the class kit's quarterstaff.
    /// </summary>
    public static class MonkScenarios
    {
        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return S("monk-flurry-npc", FlurryNpc);
            yield return S("monk-flurry-ai", FlurryAi);
            yield return S("monk-flurry-ui", FlurryUi);
            yield return S("monk-flurry-ui-trip", FlurryUiTrip);
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("MonkScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title) => Scenario.Define(id, title).Tags("rules");

        /// <summary>A Human monk: STR 16 (+3), DEX 14, WIS 14; BAB from the monk table (+2 at 3rd).</summary>
        private static CharacterStats Monk(string name, int level)
        {
            var s = new CharacterStats(
                name: name, level: level, characterClass: "Monk",
                str: 16, dex: 14, con: 12, wis: 14, intelligence: 10, cha: 8,
                bab: 0, armorBonus: 0, shieldBonus: 0,
                damageDice: 6, damageCount: 1, bonusDamage: 0,
                baseSpeed: 6, atkRange: 1, baseHitDieHP: 8 * level, raceName: "Human");
            s.EnsureMulticlassDataInitialized();
            return s;
        }

        private static ExpectResult FlurryAttacks(TraceView v, string by, int count, int mod, string dice, string strengthTerm)
        {
            List<TraceEvent> attacks = v.Attacks(by, aoo: false, round: 1);
            if (attacks.Count != count)
                return ExpectResult.Fail(attacks.Count + " attacks in round 1, expected " + count);
            foreach (TraceEvent a in attacks)
            {
                if (a.Int("mod") != mod)
                    return ExpectResult.Fail("attack mod " + a.Int("mod") + ", expected " + mod, a.Seq);
                if (a.Str("dice") != dice)
                    return ExpectResult.Fail("damage dice " + a.Str("dice") + ", expected " + dice, a.Seq);
                if (strengthTerm != null && (a.Str("dmgTerms") ?? string.Empty).IndexOf(strengthTerm, StringComparison.Ordinal) < 0)
                    return ExpectResult.Fail("damage terms '" + a.Str("dmgTerms") + "' lack " + strengthTerm, a.Seq);
            }
            return ExpectResult.Pass(count + " attacks at " + CharacterStats.FormatMod(mod) + ", " + dice, attacks[0].Seq);
        }

        /// <summary>
        /// The NPC monk 7 (BAB +5, STR 14) flurries on a scripted step: two unarmed attacks at +5 -1 +2 = +6, 1d8, the
        /// flurry Strength term (PHB Table 3-10: +4/+4 flurry, 1d8 at 7th). Before CRE-042 the entry made two natural
        /// "Unarmed Strike" attacks at +7 with no flurry.
        /// </summary>
        private static ScenarioDef FlurryNpc()
        {
            return Rules("rules/monk-flurry-npc", "An NPC monk 7 flurries: +4/+4 BAB terms, 1d8, full Strength (PHB p.40-41; CHR-003, CRE-042)")
                .Covers("CHR-003", "CRE-042", "PHB p.40", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("dummy", ActorSource.Stats(() => RulesScenarios.Fighter("Dummy", 4)), 10, 10, Control.Scripted)
                .Npc("monk", "human_monk_7", 11, 10, Control.Scripted)
                .Tweak("dummy", c => { c.Stats.AdjustMaxHP(200); c.Stats.CurrentHP += 200; })
                .Initiative("monk", "dummy")
                .Turn("monk", 1, Step.Flurry("dummy"))
                .Turn("dummy", 1, Step.Pass())
                .Expect("The monk's flurry step is done", Expect.StepStatus("monk", 1, "Flurry", 0, "done"))
                .Expect("Two flurry attacks at +6 (BAB 5, flurry -1, STR +2), 1d8, full Strength",
                    v => FlurryAttacks(v, "monk", 2, 6, "1d8", "STR (flurry):2"))
                .Build();
        }

        /// <summary>
        /// The AI path: a scripted Attack runs NPCPerformAttackForAI, where the AI declares the flurry
        /// (AIProfile.DefaultShouldUseFlurryOfBlows) before its melee sequence. Monk 3 (BAB +2, STR 14): one iterative
        /// attack without the flurry; with it two at +2 -2 +2 = +2, 1d6.
        /// </summary>
        private static ScenarioDef FlurryAi()
        {
            return Rules("rules/monk-flurry-ai", "The AI declares a monk 3's flurry: two attacks at +2, 1d6 (PHB p.40; CHR-003)")
                .Covers("CHR-003", "CRE-042", "PHB p.40", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("dummy", ActorSource.Stats(() => RulesScenarios.Fighter("Dummy", 4)), 10, 10, Control.Scripted)
                .Npc("monk", "human_monk_3", 11, 10, Control.Scripted)
                .Tweak("dummy", c => { c.Stats.AdjustMaxHP(200); c.Stats.CurrentHP += 200; })
                .Initiative("monk", "dummy")
                .Turn("monk", 1, Step.Attack("dummy"))
                .Turn("dummy", 1, Step.Pass())
                .Expect("The AI flurries: two attacks at +2 (BAB 2, flurry -2, STR +2), 1d6",
                    v => FlurryAttacks(v, "monk", 2, 2, "1d6", "STR (flurry):2"))
                .Build();
        }

        /// <summary>
        /// The PC path: the Flurry of Blows button declares the flurry and starts the PC progressive attack sequence; the
        /// target click makes the first flurry step and Attack Again the second. A Human monk 3 (STR 16) with the kit's
        /// quarterstaff: two attacks at +2 -2 +3 = +3, 1d6, and Strength x1 (+3), not the two-handed x1-1/2 (PHB p.41).
        /// </summary>
        private static ScenarioDef FlurryUi()
        {
            return Rules("rules/monk-flurry-ui", "A PC monk 3 flurries with a quarterstaff: two attacks at +3, STR x1 (PHB p.40-41; CHR-003)")
                .Covers("CHR-003", "PHB p.40", "PHB p.41", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("monk", ActorSource.Stats(() => Monk("Monk", 3)), 10, 10, Control.Ui)
                .Npc("dummy", "target_dummy", 11, 10, Control.Scripted)
                .Initiative("monk", "dummy")
                .Turn("monk", 1, Step.Flurry("dummy"), Step.AttackAgain("dummy"))
                .Turn("dummy", 1, Step.Pass())
                .Expect("Two flurry attacks at +3 (BAB 2, flurry -2, STR +3), 1d6, Strength x1",
                    v => FlurryAttacks(v, "monk", 2, 3, "1d6", "STR (flurry):3"))
                .Build();
        }

        /// <summary>
        /// PC/NPC parity: a PC monk may replace a flurry step with a maneuver from the Special Attack menu, as the AI may
        /// (TryCommitManeuverSubstituteStep; delegated RAW reading 2026-10-14, PHB p.40, p.143). Monk 3: the first flurry
        /// attack at +3, then a trip in place of the flurry's extra attack: its touch attack at the step's BAB +2, STR +3,
        /// flurry -2 = +3 (forced d20 2, total 5, a miss). A monk 3 without the flurry has only one attack step, so the
        /// trip itself shows the flurry's extra step. The dummy is unarmed, so the trip provokes nothing.
        /// </summary>
        private static ScenarioDef FlurryUiTrip()
        {
            return Rules("rules/monk-flurry-ui-trip", "A PC monk 3 trips in place of a flurry attack: touch attack at +3 with the flurry -2 (PHB p.40, p.143; CHR-003)")
                .Covers("CHR-003", "PHB p.40", "PHB p.143", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("monk", ActorSource.Stats(() => Monk("Monk", 3)), 10, 10, Control.Ui)
                .Npc("dummy", "target_dummy", 11, 10, Control.Scripted)
                .Initiative("monk", "dummy")
                .Force(20, 2, "Trip touch attack")
                .Turn("monk", 1, Step.Flurry("dummy"), Step.Maneuver(SpecialAttackType.Trip, "dummy"))
                .Turn("dummy", 1, Step.Pass())
                .Expect("The Flurry step is done", Expect.StepStatus("monk", 1, "Flurry", 0, "done"))
                .Expect("The Maneuver step is done", Expect.StepStatus("monk", 1, "Maneuver", 0, "done"))
                .Expect("One flurry attack at +3, 1d6, Strength x1",
                    v => FlurryAttacks(v, "monk", 1, 3, "1d6", "STR (flurry):3"))
                .Expect("The trip replaces the flurry's extra attack: touch attack d20 2 + 3 = 5", v =>
                {
                    TraceEvent m = v.Maneuvers("monk", SpecialAttackType.Trip).FirstOrDefault();
                    if (m == null) return ExpectResult.Fail("no trip");
                    if (m.Int("checkRoll") != 2) return ExpectResult.Fail("touch roll " + m.Int("checkRoll") + ", expected the forced 2", m.Seq);
                    return m.Int("check") == 5
                        ? ExpectResult.Pass("touch attack 2 + 3 = 5", m.Seq)
                        : ExpectResult.Fail("touch attack total " + m.Int("check") + ", expected 5 (BAB 2, STR 3, flurry -2)", m.Seq);
                })
                .Build();
        }
    }
}
#endif
