using UnityEngine;

namespace Tests.Combat
{
/// <summary>
/// Test script to verify D&D 3.5 range increment calculations.
/// Includes tests for both projectile weapons (10 increment max) and
/// thrown weapons (5 increment max) per D&D 3.5 rules.
/// Attach to a GameObject or call RangeCalculatorTests.RunAllTests() from any script.
/// </summary>
public class RangeCalculatorTests : MonoBehaviour
{
    private void Start()
    {
        RunAllTests();
    }

    public static void RunAllTests()
    {
        int passed = 0;
        int failed = 0;

        // ===== PROJECTILE WEAPON TESTS (10 increment max) =====

        // Test 1: Shortbow at 50 ft (10 squares): No penalty
        AssertTest("Shortbow 50ft", 10, 60, false, 1, 0, true, ref passed, ref failed);

        // Test 2: Shortbow at 90 ft (18 squares): -2 penalty (increment 2)
        AssertTest("Shortbow 90ft", 18, 60, false, 2, -2, true, ref passed, ref failed);

        // Test 3: Shortbow at 150 ft (30 squares): -4 penalty (increment 3)
        AssertTest("Shortbow 150ft", 30, 60, false, 3, -4, true, ref passed, ref failed);

        // Test 4: Shortbow at 650 ft (130 squares): Cannot attack (beyond max 600 ft)
        AssertOutOfRange("Shortbow 650ft", 130, 60, false, ref passed, ref failed);

        // Test 5: Longbow at 200 ft (40 squares): -2 penalty (increment 2)
        AssertTest("Longbow 200ft", 40, 100, false, 2, -2, true, ref passed, ref failed);

        // Test 6: Longbow at 250 ft (50 squares): -4 penalty (increment 3)
        AssertTest("Longbow 250ft", 50, 100, false, 3, -4, true, ref passed, ref failed);

        // Test 7: Light Crossbow at 80 ft (16 squares): No penalty (increment 1)
        AssertTest("LtCrossbow 80ft", 16, 80, false, 1, 0, true, ref passed, ref failed);

        // Test 8: Sling at 500 ft (100 squares): max range (increment 10), -18 penalty
        AssertTest("Sling 500ft", 100, 50, false, 10, -18, true, ref passed, ref failed);

        // Test 9: Sling at 505 ft (101 squares): beyond max range
        AssertOutOfRange("Sling 505ft", 101, 50, false, ref passed, ref failed);

        // Test 10: Longbow at 1000 ft (200 squares): max range (increment 10), -18 penalty - STILL VALID
        AssertTest("Longbow 1000ft (10th inc)", 200, 100, false, 10, -18, true, ref passed, ref failed);

        // Test 11: Longbow at 1005 ft (201 squares): beyond max range
        AssertOutOfRange("Longbow 1005ft", 201, 100, false, ref passed, ref failed);

        // ===== THROWN WEAPON TESTS (5 increment max per D&D 3.5) =====

        // Test 12: Javelin at 30 ft (6 squares): increment 1, no penalty (thrown)
        AssertTest("Javelin 30ft (1st inc, thrown)", 6, 30, true, 1, 0, true, ref passed, ref failed);

        // Test 13: Javelin at 35 ft (7 squares): -2 penalty (increment 2, thrown)
        AssertTest("Javelin 35ft (2nd inc, thrown)", 7, 30, true, 2, -2, true, ref passed, ref failed);

        // Test 14: Javelin at 150 ft (30 squares): max range for thrown (5th increment), -8 penalty - VALID
        AssertTest("Javelin 150ft (5th inc, thrown)", 30, 30, true, 5, -8, true, ref passed, ref failed);

        // Test 15: Javelin at 160 ft (32 squares): beyond max range for thrown (6th increment) - OUT OF RANGE
        AssertOutOfRange("Javelin 160ft (6th inc, thrown)", 32, 30, true, ref passed, ref failed);

        // Test 16: Dagger at 10 ft (2 squares): increment 1, no penalty (thrown)
        AssertTest("Dagger 10ft (1st inc, thrown)", 2, 10, true, 1, 0, true, ref passed, ref failed);

        // Test 17: Dagger at 50 ft (10 squares): max range for thrown (5th increment), -8 penalty - VALID
        AssertTest("Dagger 50ft (5th inc, thrown)", 10, 10, true, 5, -8, true, ref passed, ref failed);

        // Test 18: Dagger at 55 ft (11 squares): beyond max range for thrown (6th increment) - OUT OF RANGE
        AssertOutOfRange("Dagger 55ft (6th inc, thrown)", 11, 10, true, ref passed, ref failed);

        // Test 19: Dart at 100 ft (20 squares): max range for thrown (5th increment), -8 penalty - VALID
        AssertTest("Dart 100ft (5th inc, thrown)", 20, 20, true, 5, -8, true, ref passed, ref failed);

        // Test 20: Dart at 105 ft (21 squares): beyond max range for thrown - OUT OF RANGE
        AssertOutOfRange("Dart 105ft (6th inc, thrown)", 21, 20, true, ref passed, ref failed);

        // Test 21: Handaxe at 50 ft (10 squares): max range for thrown (5th increment), -8 penalty
        AssertTest("Handaxe 50ft (5th inc, thrown)", 10, 10, true, 5, -8, true, ref passed, ref failed);

        // Test 22: Shortspear at 100 ft (20 squares): max range for thrown (5th increment), -8 penalty
        AssertTest("Shortspear 100ft (5th inc, thrown)", 20, 20, true, 5, -8, true, ref passed, ref failed);

        // Test 23: Trident at 50 ft (10 squares): max range for thrown (5th increment), -8 penalty
        AssertTest("Trident 50ft (5th inc, thrown)", 10, 10, true, 5, -8, true, ref passed, ref failed);

        // ===== MELEE AND UTILITY TESTS =====

        // Test 24: Melee weapon (range increment 0)
        {
            RangeInfo info = RangeCalculator.GetRangeInfo(1, 0);
            if (info.IsMelee && info.IsInRange)
            {
                passed++;
                Debug.Log("[RangeTest] PASS: Melee at 1 square");
            }
            else
            {
                failed++;
                Debug.LogError($"[RangeTest] FAIL: Melee at 1 square - IsMelee={info.IsMelee}, IsInRange={info.IsInRange}");
            }
        }

        // Test 25: Range zones for projectile weapons
        {
            bool zonePass = true;
            // Shortbow (60 ft increment, projectile)
            int z1 = RangeCalculator.GetRangeZone(10, 60, false);  // 50 ft -> increment 1 -> zone 1
            int z2 = RangeCalculator.GetRangeZone(18, 60, false);  // 90 ft -> increment 2 -> zone 2
            int z3 = RangeCalculator.GetRangeZone(100, 60, false); // 500 ft -> increment 9 -> zone 3
            int z0 = RangeCalculator.GetRangeZone(130, 60, false); // 650 ft -> out of range -> zone 0

            if (z1 != 1) { zonePass = false; Debug.LogError($"[RangeTest] FAIL: Projectile zone test z1={z1}, expected 1"); }
            if (z2 != 2) { zonePass = false; Debug.LogError($"[RangeTest] FAIL: Projectile zone test z2={z2}, expected 2"); }
            if (z3 != 3) { zonePass = false; Debug.LogError($"[RangeTest] FAIL: Projectile zone test z3={z3}, expected 3"); }
            if (z0 != 0) { zonePass = false; Debug.LogError($"[RangeTest] FAIL: Projectile zone test z0={z0}, expected 0"); }

            if (zonePass) { passed++; Debug.Log("[RangeTest] PASS: Projectile range zones"); }
            else { failed++; }
        }

        // Test 26: Range zones for thrown weapons
        {
            bool zonePass = true;
            // Javelin (30 ft increment, thrown)
            int z1 = RangeCalculator.GetRangeZone(5, 30, true);   // 25 ft -> increment 1 -> zone 1
            int z2 = RangeCalculator.GetRangeZone(10, 30, true);  // 50 ft -> increment 2 -> zone 2
            int z3 = RangeCalculator.GetRangeZone(25, 30, true);  // 125 ft -> increment 5 -> zone 3 (far for thrown)
            int z0 = RangeCalculator.GetRangeZone(35, 30, true);  // 175 ft -> out of range -> zone 0

            if (z1 != 1) { zonePass = false; Debug.LogError($"[RangeTest] FAIL: Thrown zone test z1={z1}, expected 1"); }
            if (z2 != 2) { zonePass = false; Debug.LogError($"[RangeTest] FAIL: Thrown zone test z2={z2}, expected 2"); }
            if (z3 != 3) { zonePass = false; Debug.LogError($"[RangeTest] FAIL: Thrown zone test z3={z3}, expected 3"); }
            if (z0 != 0) { zonePass = false; Debug.LogError($"[RangeTest] FAIL: Thrown zone test z0={z0}, expected 0"); }

            if (zonePass) { passed++; Debug.Log("[RangeTest] PASS: Thrown range zones"); }
            else { failed++; }
        }

        // Test 27: IsWithinMaxRange for thrown vs projectile
        {
            bool rangePass = true;
            // Javelin (30 ft increment, thrown): max = 150 ft
            if (!RangeCalculator.IsWithinMaxRange(150, 30, true))
            { rangePass = false; Debug.LogError("[RangeTest] FAIL: Javelin should be in range at 150 ft"); }
            if (RangeCalculator.IsWithinMaxRange(151, 30, true))
            { rangePass = false; Debug.LogError("[RangeTest] FAIL: Javelin should be out of range at 151 ft"); }
            // Longbow (100 ft increment, projectile): max = 1000 ft
            if (!RangeCalculator.IsWithinMaxRange(1000, 100, false))
            { rangePass = false; Debug.LogError("[RangeTest] FAIL: Longbow should be in range at 1000 ft"); }
            if (RangeCalculator.IsWithinMaxRange(1001, 100, false))
            { rangePass = false; Debug.LogError("[RangeTest] FAIL: Longbow should be out of range at 1001 ft"); }

            if (rangePass) { passed++; Debug.Log("[RangeTest] PASS: IsWithinMaxRange thrown vs projectile"); }
            else { failed++; }
        }

        // Test 28: GetMaxRangeFeet for thrown vs projectile
        {
            bool maxPass = true;
            if (RangeCalculator.GetMaxRangeFeet(30, true) != 150)
            { maxPass = false; Debug.LogError("[RangeTest] FAIL: Javelin max range should be 150 ft"); }
            if (RangeCalculator.GetMaxRangeFeet(30, false) != 300)
            { maxPass = false; Debug.LogError("[RangeTest] FAIL: Projectile 30ft inc max range should be 300 ft"); }
            if (RangeCalculator.GetMaxRangeFeet(100, false) != 1000)
            { maxPass = false; Debug.LogError("[RangeTest] FAIL: Longbow max range should be 1000 ft"); }
            if (RangeCalculator.GetMaxRangeFeet(10, true) != 50)
            { maxPass = false; Debug.LogError("[RangeTest] FAIL: Dagger max range should be 50 ft"); }

            if (maxPass) { passed++; Debug.Log("[RangeTest] PASS: GetMaxRangeFeet thrown vs projectile"); }
            else { failed++; }
        }

        Debug.Log($"[RangeTest] === RESULTS: {passed} passed, {failed} failed ===");
    }

    private static void AssertTest(string name, int sqDist, int rangeInc, bool isThrown, int expectedInc, int expectedPenalty, bool expectedInRange, ref int passed, ref int failed)
    {
        RangeInfo info = RangeCalculator.GetRangeInfo(sqDist, rangeInc, isThrown);
        bool pass = info.IncrementNumber == expectedInc && info.Penalty == expectedPenalty && info.IsInRange == expectedInRange;
        if (pass)
        {
            passed++;
            Debug.Log($"[RangeTest] PASS: {name} - inc={info.IncrementNumber}, penalty={info.Penalty}");
        }
        else
        {
            failed++;
            Debug.LogError($"[RangeTest] FAIL: {name} - got inc={info.IncrementNumber} (exp {expectedInc}), penalty={info.Penalty} (exp {expectedPenalty}), inRange={info.IsInRange} (exp {expectedInRange})");
        }
    }

    private static void AssertOutOfRange(string name, int sqDist, int rangeInc, bool isThrown, ref int passed, ref int failed)
    {
        RangeInfo info = RangeCalculator.GetRangeInfo(sqDist, rangeInc, isThrown);
        if (!info.IsInRange && info.IncrementNumber == 0)
        {
            passed++;
            Debug.Log($"[RangeTest] PASS: {name} - out of range as expected");
        }
        else
        {
            failed++;
            Debug.LogError($"[RangeTest] FAIL: {name} - expected out of range but got inc={info.IncrementNumber}, inRange={info.IsInRange}");
        }
    }
}

}
