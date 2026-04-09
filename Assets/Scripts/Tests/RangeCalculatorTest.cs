using UnityEngine;

/// <summary>
/// Test script to verify D&D 3.5 range increment calculations.
/// Attach to a GameObject or call RangeCalculatorTest.RunAllTests() from any script.
/// </summary>
public class RangeCalculatorTest : MonoBehaviour
{
    private void Start()
    {
        RunAllTests();
    }

    public static void RunAllTests()
    {
        int passed = 0;
        int failed = 0;

        // Test 1: Shortbow at 50 ft (10 hexes): No penalty
        AssertTest("Shortbow 50ft", 10, 60, 1, 0, true, ref passed, ref failed);

        // Test 2: Shortbow at 90 ft (18 hexes): -2 penalty (increment 2)
        AssertTest("Shortbow 90ft", 18, 60, 2, -2, true, ref passed, ref failed);

        // Test 3: Shortbow at 150 ft (30 hexes): -4 penalty (increment 3)
        AssertTest("Shortbow 150ft", 30, 60, 3, -4, true, ref passed, ref failed);

        // Test 4: Shortbow at 650 ft (130 hexes): Cannot attack (beyond max 600 ft)
        AssertOutOfRange("Shortbow 650ft", 130, 60, ref passed, ref failed);

        // Test 5: Longbow at 200 ft (40 hexes): -2 penalty (increment 2)
        AssertTest("Longbow 200ft", 40, 100, 2, -2, true, ref passed, ref failed);

        // Test 6: Javelin at 35 ft (7 hexes): -2 penalty (increment 2)
        AssertTest("Javelin 35ft", 7, 30, 2, -2, true, ref passed, ref failed);

        // Test 7: Dagger thrown at 25 ft (5 hexes): -4 penalty (increment 3)
        AssertTest("Dagger 25ft", 5, 10, 3, -4, true, ref passed, ref failed);

        // Test 8: Longbow at 250 ft (50 hexes): -4 penalty (increment 3)
        AssertTest("Longbow 250ft", 50, 100, 3, -4, true, ref passed, ref failed);

        // Test 9: Light Crossbow at 80 ft (16 hexes): No penalty (increment 1)
        AssertTest("LtCrossbow 80ft", 16, 80, 1, 0, true, ref passed, ref failed);

        // Test 10: Sling at 500 ft (100 hexes): max range (increment 10), -18 penalty
        AssertTest("Sling 500ft", 100, 50, 10, -18, true, ref passed, ref failed);

        // Test 11: Sling at 505 ft (101 hexes): beyond max range
        AssertOutOfRange("Sling 505ft", 101, 50, ref passed, ref failed);

        // Test 12: Melee weapon (range increment 0)
        {
            RangeInfo info = RangeCalculator.GetRangeInfo(1, 0);
            if (info.IsMelee && info.IsInRange)
            {
                passed++;
                Debug.Log("[RangeTest] PASS: Melee at 1 hex");
            }
            else
            {
                failed++;
                Debug.LogError($"[RangeTest] FAIL: Melee at 1 hex - IsMelee={info.IsMelee}, IsInRange={info.IsInRange}");
            }
        }

        // Test 13: Range zones
        {
            bool zonePass = true;
            // Shortbow (60 ft increment)
            int z1 = RangeCalculator.GetRangeZone(10, 60);  // 50 ft -> increment 1 -> zone 1
            int z2 = RangeCalculator.GetRangeZone(18, 60);  // 90 ft -> increment 2 -> zone 2
            int z3 = RangeCalculator.GetRangeZone(100, 60); // 500 ft -> increment 9 -> zone 3
            int z0 = RangeCalculator.GetRangeZone(130, 60); // 650 ft -> out of range -> zone 0

            if (z1 != 1) { zonePass = false; Debug.LogError($"[RangeTest] FAIL: Zone test z1={z1}, expected 1"); }
            if (z2 != 2) { zonePass = false; Debug.LogError($"[RangeTest] FAIL: Zone test z2={z2}, expected 2"); }
            if (z3 != 3) { zonePass = false; Debug.LogError($"[RangeTest] FAIL: Zone test z3={z3}, expected 3"); }
            if (z0 != 0) { zonePass = false; Debug.LogError($"[RangeTest] FAIL: Zone test z0={z0}, expected 0"); }

            if (zonePass) { passed++; Debug.Log("[RangeTest] PASS: Range zones"); }
            else { failed++; }
        }

        Debug.Log($"[RangeTest] === RESULTS: {passed} passed, {failed} failed ===");
    }

    private static void AssertTest(string name, int hexDist, int rangeInc, int expectedInc, int expectedPenalty, bool expectedInRange, ref int passed, ref int failed)
    {
        RangeInfo info = RangeCalculator.GetRangeInfo(hexDist, rangeInc);
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

    private static void AssertOutOfRange(string name, int hexDist, int rangeInc, ref int passed, ref int failed)
    {
        RangeInfo info = RangeCalculator.GetRangeInfo(hexDist, rangeInc);
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
