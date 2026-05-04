using System.Collections.Generic;
using System.Linq;
using DND35e.Identifiers;
using UnityEngine;

namespace Tests.Magic
{
    /// <summary>
    /// Runtime regression checks for Summon Monster class/alignment restrictions.
    /// Run manually via SummonMonsterAlignmentRulesTests.RunAll().
    /// </summary>
    public static class SummonMonsterAlignmentRulesTests
    {
        private static int _passed;
        private static int _failed;

        public static void RunAll()
        {
            _passed = 0;
            _failed = 0;

            Debug.Log("====== SUMMON MONSTER ALIGNMENT RULE TESTS ======");

            TestWizardCanAlwaysSeeAllSummonMonsterIIOptions();
            TestClericSummonMonsterIICountsByAlignment();
            TestLgWizardCanSummonDevils();
            TestCeWizardCanSummonCelestials();
            TestLgClericCannotSummonDevils();
            TestCeClericCannotSummonCelestials();
            TestSummonMonsterListLevelOptions();
            TestSummonCreatureCountInfoRanges();
            TestSummonSwarmSpellDefinition();
            TestSummonSwarmCloseRangeScaling();

            Debug.Log($"====== Summon Monster Alignment Results: {_passed} passed, {_failed} failed ======");
        }

        private static CharacterStats CreateCaster(string characterClass, Alignment alignment)
        {
            // Keep test setup minimal; these tests only depend on class + alignment filtering.
            CharacterStats caster = new CharacterStats(
                $"Test {characterClass}", // name
                1,                        // level
                characterClass,
                10, 10, 10, 10, 10, 10,  // STR, DEX, CON, WIS, INT, CHA
                0,                        // BAB
                0,                        // armor bonus
                0,                        // shield bonus
                6,                        // damage die (d6)
                1,                        // damage count
                0,                        // bonus damage
                6,                        // base speed (squares)
                1,                        // attack range
                8                         // base hit-die HP
            );

            caster.CharacterAlignment = alignment;
            return caster;
        }

        private static List<SummonMonsterOption> GetSummonMonsterIIOptions(CharacterStats caster)
        {
            return SummonMonsterLists.GetFilteredOptions(SpellNames.SUMMON_MONSTER_2, caster);
        }

        private static bool ContainsOption(List<SummonMonsterOption> options, string displayName)
        {
            return options != null && options.Any(o => o != null && o.DisplayName == displayName);
        }

        private static void AssertTrue(bool condition, string testName)
        {
            if (condition)
            {
                _passed++;
                Debug.Log($"  PASS: {testName}");
            }
            else
            {
                _failed++;
                Debug.LogError($"  FAIL: {testName}");
            }
        }

        private static void AssertEqual(int expected, int actual, string testName)
        {
            if (expected == actual)
            {
                _passed++;
                Debug.Log($"  PASS: {testName} ({actual})");
            }
            else
            {
                _failed++;
                Debug.LogError($"  FAIL: {testName} (expected {expected}, got {actual})");
            }
        }

        private static void TestWizardCanAlwaysSeeAllSummonMonsterIIOptions()
        {
            foreach (Alignment alignment in AlignmentHelper.GridOrder)
            {
                CharacterStats wizard = CreateCaster("Wizard", alignment);
                List<SummonMonsterOption> options = GetSummonMonsterIIOptions(wizard);
                AssertEqual(10, options.Count, $"Wizard {alignment} sees all Summon Monster II options");
            }
        }

        private static void TestClericSummonMonsterIICountsByAlignment()
        {
            var expectedCounts = new Dictionary<Alignment, int>
            {
                { Alignment.LawfulGood, 3 },
                { Alignment.NeutralGood, 4 },
                { Alignment.ChaoticGood, 3 },
                { Alignment.LawfulNeutral, 3 },
                { Alignment.TrueNeutral, 10 },
                { Alignment.ChaoticNeutral, 3 },
                { Alignment.LawfulEvil, 4 },
                { Alignment.NeutralEvil, 6 },
                { Alignment.ChaoticEvil, 4 }
            };

            foreach (var pair in expectedCounts)
            {
                CharacterStats cleric = CreateCaster("Cleric", pair.Key);
                List<SummonMonsterOption> options = GetSummonMonsterIIOptions(cleric);
                AssertEqual(pair.Value, options.Count, $"Cleric {pair.Key} gets filtered Summon Monster II options");
            }
        }

        private static void TestLgWizardCanSummonDevils()
        {
            CharacterStats wizard = CreateCaster("Wizard", Alignment.LawfulGood);
            List<SummonMonsterOption> options = GetSummonMonsterIIOptions(wizard);

            AssertTrue(ContainsOption(options, "Lemure"), "LG wizard can summon Lemure (LE)");
            AssertTrue(ContainsOption(options, "Wolf"), "LG wizard can summon fiendish wolf (LE)");
        }

        private static void TestCeWizardCanSummonCelestials()
        {
            CharacterStats wizard = CreateCaster("Wizard", Alignment.ChaoticEvil);
            List<SummonMonsterOption> options = GetSummonMonsterIIOptions(wizard);

            AssertTrue(ContainsOption(options, "Giant Bee"), "CE wizard can summon celestial giant bee (LG)");
            AssertTrue(ContainsOption(options, "Eagle"), "CE wizard can summon celestial eagle (CG)");
        }

        private static void TestLgClericCannotSummonDevils()
        {
            CharacterStats cleric = CreateCaster("Cleric", Alignment.LawfulGood);
            List<SummonMonsterOption> options = GetSummonMonsterIIOptions(cleric);

            AssertTrue(!ContainsOption(options, "Lemure"), "LG cleric cannot summon Lemure (LE)");
            AssertTrue(!ContainsOption(options, "Wolf"), "LG cleric cannot summon fiendish wolf (LE)");
        }

        private static void TestCeClericCannotSummonCelestials()
        {
            CharacterStats cleric = CreateCaster("Cleric", Alignment.ChaoticEvil);
            List<SummonMonsterOption> options = GetSummonMonsterIIOptions(cleric);

            AssertTrue(!ContainsOption(options, "Giant Bee"), "CE cleric cannot summon celestial giant bee (LG)");
            AssertTrue(!ContainsOption(options, "Eagle"), "CE cleric cannot summon celestial eagle (CG)");
        }

        private static void TestSummonMonsterListLevelOptions()
        {
            List<int> sm1Levels = SummonMonsterLists.GetAvailableListLevelsForSpell(SpellNames.SUMMON_MONSTER_1);
            List<int> sm2Levels = SummonMonsterLists.GetAvailableListLevelsForSpell(SpellNames.SUMMON_MONSTER_2);
            List<int> sm3Levels = SummonMonsterLists.GetAvailableListLevelsForSpell("summon_monster_3");
            List<int> sm5Levels = SummonMonsterLists.GetAvailableListLevelsForSpell("summon_monster_5");

            AssertEqual(1, sm1Levels.Count, "Summon Monster I offers one list level");
            AssertEqual(2, sm2Levels.Count, "Summon Monster II offers levels I-II");
            AssertEqual(3, sm3Levels.Count, "Summon Monster III offers levels I-III");
            AssertEqual(5, sm5Levels.Count, "Summon Monster V offers levels I-V");
        }

        private static void TestSummonCreatureCountInfoRanges()
        {
            SummonCreatureCountInfo sameLevel = SummonMonsterLists.GetCreatureCountInfo(3, 3);
            SummonCreatureCountInfo oneLower = SummonMonsterLists.GetCreatureCountInfo(3, 2);
            SummonCreatureCountInfo twoLower = SummonMonsterLists.GetCreatureCountInfo(3, 1);

            AssertTrue(sameLevel != null && sameLevel.RangeText == "1 creature", "Same-level summon count is fixed at 1 creature");
            AssertTrue(oneLower != null && oneLower.RangeText == "1d3 creatures (1-3)", "One-level-lower summon count is 1d3");
            AssertTrue(twoLower != null && twoLower.RangeText == "1d4+1 creatures (2-5)", "Two-or-more-level-lower summon count is 1d4+1");
        }

        private static void TestSummonSwarmSpellDefinition()
        {
            SpellDatabase.Init();
            SpellData spell = SpellDatabase.GetSpell(SpellNames.SUMMON_SWARM);

            bool valid = spell != null
                         && spell.SpellLevel == 2
                         && spell.DurationType == DurationType.Concentration
                         && spell.GetEffectiveRangeCategory() == SpellRangeCategory.Close
                         && spell.IsAvailableFor("Wizard", 2)
                         && spell.IsAvailableFor("Sorcerer", 2)
                         && spell.IsAvailableFor("Druid", 2)
                         && spell.IsAvailableFor("Bard", 2);

            AssertTrue(valid, "Summon Swarm definition matches level/range/class requirements");
        }

        private static void TestSummonSwarmCloseRangeScaling()
        {
            SpellDatabase.Init();
            SpellData spell = SpellDatabase.GetSpell(SpellNames.SUMMON_SWARM);
            if (spell == null)
            {
                AssertTrue(false, "Summon Swarm range scaling test has spell data");
                return;
            }

            // 25 + 5 ft per 2 levels => 5 + floor(CL/2) squares
            AssertEqual(6, spell.GetRangeSquaresForCasterLevel(3), "Summon Swarm CL3 = 30 ft");
            AssertEqual(7, spell.GetRangeSquaresForCasterLevel(5), "Summon Swarm CL5 = 35 ft");
            AssertEqual(10, spell.GetRangeSquaresForCasterLevel(10), "Summon Swarm CL10 = 50 ft");
        }
    }
}
