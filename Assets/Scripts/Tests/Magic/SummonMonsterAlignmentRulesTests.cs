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
    }
}
