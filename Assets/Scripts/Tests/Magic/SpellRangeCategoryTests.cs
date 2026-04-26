using UnityEngine;

namespace Tests.Magic
{
    /// <summary>
    /// Lightweight runtime checks for standardized D&D 3.5e spell range categories.
    /// Run manually with SpellRangeCategoryTests.RunAll().
    /// </summary>
    public static class SpellRangeCategoryTests
    {
        private static int _passed;
        private static int _failed;

        public static void RunAll()
        {
            _passed = 0;
            _failed = 0;

            Debug.Log("====== SPELL RANGE CATEGORY TESTS ======");

            TestCloseRangeScaling();
            TestMediumRangeScaling();
            TestLongRangeScaling();
            TestFactoryHelperConfiguresRange();

            Debug.Log($"====== Spell Range Results: {_passed} passed, {_failed} failed ======");
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

        private static void TestCloseRangeScaling()
        {
            var spell = SpellData.CreateWithRange(SpellRangeCategory.Close);
            AssertEqual(5, spell.GetRangeSquaresForCasterLevel(1), "Close CL1");
            AssertEqual(7, spell.GetRangeSquaresForCasterLevel(5), "Close CL5");
            AssertEqual(10, spell.GetRangeSquaresForCasterLevel(10), "Close CL10");
            AssertEqual(15, spell.GetRangeSquaresForCasterLevel(20), "Close CL20");
        }

        private static void TestMediumRangeScaling()
        {
            var spell = SpellData.CreateWithRange(SpellRangeCategory.Medium);
            AssertEqual(22, spell.GetRangeSquaresForCasterLevel(1), "Medium CL1");
            AssertEqual(30, spell.GetRangeSquaresForCasterLevel(5), "Medium CL5");
            AssertEqual(40, spell.GetRangeSquaresForCasterLevel(10), "Medium CL10");
            AssertEqual(60, spell.GetRangeSquaresForCasterLevel(20), "Medium CL20");
        }

        private static void TestLongRangeScaling()
        {
            var spell = SpellData.CreateWithRange(SpellRangeCategory.Long);
            AssertEqual(88, spell.GetRangeSquaresForCasterLevel(1), "Long CL1");
            AssertEqual(120, spell.GetRangeSquaresForCasterLevel(5), "Long CL5");
            AssertEqual(160, spell.GetRangeSquaresForCasterLevel(10), "Long CL10");
            AssertEqual(240, spell.GetRangeSquaresForCasterLevel(20), "Long CL20");
        }

        private static void TestFactoryHelperConfiguresRange()
        {
            var spell = SpellData.CreateWithRange(SpellRangeCategory.Close);
            AssertEqual(5, spell.RangeSquares, "Factory sets base close range");
            AssertEqual(2, spell.RangeIncreasePerLevels, "Factory sets close per-level interval");
            AssertEqual(1, spell.RangeIncreaseSquares, "Factory sets close increment");
        }
    }
}
