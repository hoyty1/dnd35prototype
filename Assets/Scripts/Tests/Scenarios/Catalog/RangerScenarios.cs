#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace Tests.Scenarios
{
    /// <summary>
    /// Ranger favored enemy on one PC/NPC rules path (CHR-020; PHB p.47): the bonus on weapon damage rolls against a
    /// creature of a favored type, matched by the creature's real type and subtypes (RangerRules), on the PC attack path
    /// (the Attack button and the target click) and the AI attack path (NPCPerformAttackForAI). The PC ranger is a Human
    /// ranger 3 built from stats with the class kit (longsword, studded leather) and favored enemy Humanoid (Goblinoid).
    /// The NPC ranger is test_archer_gust (Ranger 2, longbow, leather armor; no choices in its definition, so the DMG
    /// p.122 sample ranger's: favored enemy elves +2, archery). NPC data checked on 2026-10-14: hobgoblin (Humanoid,
    /// tags Humanoid, Goblinoid), target_dummy (Humanoid, tags Humanoid, Training: no subtype).
    /// </summary>
    public static class RangerScenarios
    {
        [ScenarioSource]
        public static IEnumerable<ScenarioDef> All()
        {
            yield return S("ranger-favored-enemy-ui", FavoredEnemyUi);
            yield return S("ranger-favored-enemy-other-ui", FavoredEnemyOtherUi);
            yield return S("ranger-favored-enemy-npc", FavoredEnemyNpc);
        }

        private static ScenarioDef S(string name, Func<ScenarioDef> build) => ScenarioCatalog.Safe("RangerScenarios rules/" + name, build);

        private static ScenarioBuilder Rules(string id, string title) => Scenario.Define(id, title).Tags("rules");

        /// <summary>A Human ranger: STR 14, DEX 15; favored enemy Humanoid (Goblinoid) +2.</summary>
        internal static CharacterStats Ranger(string name, int level)
        {
            var s = new CharacterStats(
                name: name, level: level, characterClass: "Ranger",
                str: 14, dex: 15, con: 12, wis: 12, intelligence: 10, cha: 10,
                bab: 0, armorBonus: 0, shieldBonus: 0,
                damageDice: 8, damageCount: 1, bonusDamage: 0,
                baseSpeed: 6, atkRange: 1, baseHitDieHP: 8 * level, raceName: "Human");
            s.EnsureMulticlassDataInitialized();
            var favored = new FavoredEnemyData();
            RangerRules.TryApplyFavoredEnemySelection(favored, level, "Humanoid (Goblinoid)", null, out _);
            RangerRules.ApplyCreationChoices(s, favored, RangerCombatStyle.Archery);
            return s;
        }

        /// <summary>An Elf fighter 4 (STR 16): a favored enemy of the DMG sample ranger.</summary>
        private static CharacterStats ElfFighter(string name)
        {
            var s = new CharacterStats(
                name: name, level: 4, characterClass: "Fighter",
                str: 16, dex: 12, con: 14, wis: 10, intelligence: 10, cha: 10,
                bab: 4, armorBonus: 0, shieldBonus: 0,
                damageDice: 8, damageCount: 1, bonusDamage: 0,
                baseSpeed: 6, atkRange: 1, baseHitDieHP: 32, raceName: "Elf");
            s.BaseAttackBonusOverride = 4;
            return s;
        }

        private static void Sturdy(CharacterController c)
        {
            c.Stats.AdjustMaxHP(200);
            c.Stats.CurrentHP += 200;
        }

        private static string Terms(TraceEvent a) => a.Str("dmgTerms") ?? string.Empty;

        /// <summary>Every non-AoO attack by <paramref name="by"/> in round 1 carries (or lacks) the favored enemy term "Favored enemy:N".</summary>
        private static Func<TraceView, ExpectResult> FavoredTerm(string by, int bonus)
        {
            return v =>
            {
                List<TraceEvent> attacks = v.Attacks(by, aoo: false, round: 1).ToList();
                if (attacks.Count == 0)
                    return ExpectResult.Fail("no attack by " + by + " in round 1");
                string want = RangerRules.FavoredEnemyDamageLabel + ":" + bonus;
                foreach (TraceEvent a in attacks)
                {
                    bool has = Terms(a).IndexOf(RangerRules.FavoredEnemyDamageLabel, StringComparison.Ordinal) >= 0;
                    bool ok = bonus > 0 ? Terms(a).IndexOf(want, StringComparison.Ordinal) >= 0 : !has;
                    if (!ok)
                        return ExpectResult.Fail("damage terms '" + Terms(a) + "' " + (bonus > 0 ? "lack " + want : "carry a favored enemy term"), a.Seq);
                }
                return ExpectResult.Pass(attacks.Count + " attack(s), terms " + Terms(attacks[0]), attacks[0].Seq);
            };
        }

        /// <summary>The PC path: a ranger 3 attacks a hobgoblin (a goblinoid) with the Attack button: +2 favored enemy damage.</summary>
        private static ScenarioDef FavoredEnemyUi()
        {
            return Rules("rules/ranger-favored-enemy-ui", "A PC ranger hits a favored goblinoid: +2 weapon damage (PHB p.47; CHR-020)")
                .Covers("CHR-020", "PHB p.47", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("ranger", ActorSource.Stats(() => Ranger("Ranger", 3)), 10, 10, Control.Ui)
                .Npc("hob", "hobgoblin", 11, 10, Control.Scripted)
                .Tweak("hob", Sturdy)
                .Initiative("ranger", "hob")
                .Turn("ranger", 1, Step.Attack("hob"))
                .Turn("hob", 1, Step.Pass())
                .Expect("The ranger's attack carries Favored enemy +2", FavoredTerm("ranger", 2))
                .Build();
        }

        /// <summary>The same ranger against a humanoid of no favored subtype: no favored enemy damage.</summary>
        private static ScenarioDef FavoredEnemyOtherUi()
        {
            return Rules("rules/ranger-favored-enemy-other-ui", "A PC ranger hits a humanoid that is not a goblinoid: no favored enemy damage (PHB p.47; CHR-020)")
                .Covers("CHR-020", "PHB p.47")
                .MaxRounds(1)
                .Pc("ranger", ActorSource.Stats(() => Ranger("Ranger", 3)), 10, 10, Control.Ui)
                .Npc("dummy", "target_dummy", 11, 10, Control.Scripted)
                .Tweak("dummy", Sturdy)
                .Initiative("ranger", "dummy")
                .Turn("ranger", 1, Step.Attack("dummy"))
                .Turn("dummy", 1, Step.Pass())
                .Expect("The ranger's attack has no favored enemy term", FavoredTerm("ranger", 0))
                .Build();
        }

        /// <summary>
        /// The AI path: an NPC ranger 2 with the DMG sample ranger's favored enemy (elves) shoots an elf PC with its
        /// longbow: the ranged weapon damage roll carries Favored enemy +2.
        /// </summary>
        private static ScenarioDef FavoredEnemyNpc()
        {
            return Rules("rules/ranger-favored-enemy-npc", "An NPC ranger shoots a favored elf: +2 weapon damage on the AI path (PHB p.47; CHR-020)")
                .Covers("CHR-020", "PHB p.47", "PC_NPC_PARITY")
                .MaxRounds(1)
                .Pc("elf", ActorSource.Stats(() => ElfFighter("Elf")), 10, 10, Control.Scripted)
                .Npc("ranger", "test_archer_gust", 15, 10, Control.Scripted)
                .Tweak("elf", Sturdy)
                .Initiative("ranger", "elf")
                .Turn("ranger", 1, Step.Attack("elf"))
                .Turn("elf", 1, Step.Pass())
                .Expect("The NPC ranger's attack carries Favored enemy +2", FavoredTerm("ranger", 2))
                .Build();
        }
    }
}
#endif
