using UnityEngine;

namespace Tests.Services
{
/// <summary>
/// Unit tests for DiceService — verifies all standard dice methods
/// return values within expected ranges.
/// Run with DiceServiceTests.RunAll().
/// </summary>
public static class DiceServiceTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== DICE SERVICE TESTS ======");

        TestD20Range();
        TestD6Range();
        TestD4Range();
        TestD8Range();
        TestD10Range();
        TestD12Range();
        TestPercentileRange();
        TestRollCustomRange();
        TestRollMultiple();
        TestRollMultipleMinMax();

        Debug.Log($"====== Dice Service Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  ✅ PASS: {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  ❌ FAIL: {testName} {detail}");
        }
    }

    private static void TestD20Range()
    {
        int min = int.MaxValue, max = int.MinValue;
        for (int i = 0; i < 1000; i++)
        {
            int roll = DiceService.D20("test");
            if (roll < min) min = roll;
            if (roll > max) max = roll;
        }
        Assert(min >= 1, "D20 minimum >= 1", $"got min={min}");
        Assert(max <= 20, "D20 maximum <= 20", $"got max={max}");
        Assert(min <= 5, "D20 low end reached (statistical)", $"min={min}, expected <= 5 over 1000 rolls");
        Assert(max >= 16, "D20 high end reached (statistical)", $"max={max}, expected >= 16 over 1000 rolls");
    }

    private static void TestD6Range()
    {
        int min = int.MaxValue, max = int.MinValue;
        for (int i = 0; i < 500; i++)
        {
            int roll = DiceService.D6("test");
            if (roll < min) min = roll;
            if (roll > max) max = roll;
        }
        Assert(min >= 1, "D6 minimum >= 1", $"got min={min}");
        Assert(max <= 6, "D6 maximum <= 6", $"got max={max}");
    }

    private static void TestD4Range()
    {
        int min = int.MaxValue, max = int.MinValue;
        for (int i = 0; i < 500; i++)
        {
            int roll = DiceService.D4("test");
            if (roll < min) min = roll;
            if (roll > max) max = roll;
        }
        Assert(min >= 1, "D4 minimum >= 1", $"got min={min}");
        Assert(max <= 4, "D4 maximum <= 4", $"got max={max}");
    }

    private static void TestD8Range()
    {
        int min = int.MaxValue, max = int.MinValue;
        for (int i = 0; i < 500; i++)
        {
            int roll = DiceService.D8("test");
            if (roll < min) min = roll;
            if (roll > max) max = roll;
        }
        Assert(min >= 1, "D8 minimum >= 1", $"got min={min}");
        Assert(max <= 8, "D8 maximum <= 8", $"got max={max}");
    }

    private static void TestD10Range()
    {
        int min = int.MaxValue, max = int.MinValue;
        for (int i = 0; i < 500; i++)
        {
            int roll = DiceService.D10("test");
            if (roll < min) min = roll;
            if (roll > max) max = roll;
        }
        Assert(min >= 1, "D10 minimum >= 1", $"got min={min}");
        Assert(max <= 10, "D10 maximum <= 10", $"got max={max}");
    }

    private static void TestD12Range()
    {
        int min = int.MaxValue, max = int.MinValue;
        for (int i = 0; i < 500; i++)
        {
            int roll = DiceService.D12("test");
            if (roll < min) min = roll;
            if (roll > max) max = roll;
        }
        Assert(min >= 1, "D12 minimum >= 1", $"got min={min}");
        Assert(max <= 12, "D12 maximum <= 12", $"got max={max}");
    }

    private static void TestPercentileRange()
    {
        int min = int.MaxValue, max = int.MinValue;
        for (int i = 0; i < 1000; i++)
        {
            int roll = DiceService.Percentile("test");
            if (roll < min) min = roll;
            if (roll > max) max = roll;
        }
        Assert(min >= 1, "Percentile minimum >= 1", $"got min={min}");
        Assert(max <= 100, "Percentile maximum <= 100", $"got max={max}");
        Assert(min <= 10, "Percentile low end reached", $"min={min}");
        Assert(max >= 90, "Percentile high end reached", $"max={max}");
    }

    private static void TestRollCustomRange()
    {
        // Test Roll(5, 10)
        int min = int.MaxValue, max = int.MinValue;
        for (int i = 0; i < 500; i++)
        {
            int roll = DiceService.Roll(5, 10, "custom range");
            if (roll < min) min = roll;
            if (roll > max) max = roll;
        }
        Assert(min >= 5, "Roll(5,10) minimum >= 5", $"got min={min}");
        Assert(max <= 10, "Roll(5,10) maximum <= 10", $"got max={max}");

        // Test Roll(1, 1) - should always return 1
        int singleResult = DiceService.Roll(1, 1, "single value");
        Assert(singleResult == 1, "Roll(1,1) always returns 1", $"got {singleResult}");
    }

    private static void TestRollMultiple()
    {
        // 3d6: minimum 3, maximum 18
        int min = int.MaxValue, max = int.MinValue;
        for (int i = 0; i < 500; i++)
        {
            int roll = DiceService.RollMultiple(3, 6, "3d6 test");
            if (roll < min) min = roll;
            if (roll > max) max = roll;
        }
        Assert(min >= 3, "3d6 minimum >= 3", $"got min={min}");
        Assert(max <= 18, "3d6 maximum <= 18", $"got max={max}");
    }

    private static void TestRollMultipleMinMax()
    {
        // 1d6: should behave same as D6
        int min = int.MaxValue, max = int.MinValue;
        for (int i = 0; i < 500; i++)
        {
            int roll = DiceService.RollMultiple(1, 6, "1d6 test");
            if (roll < min) min = roll;
            if (roll > max) max = roll;
        }
        Assert(min >= 1, "1d6 via RollMultiple minimum >= 1", $"got min={min}");
        Assert(max <= 6, "1d6 via RollMultiple maximum <= 6", $"got max={max}");

        // 0 dice should return 0
        int zeroDice = DiceService.RollMultiple(0, 6, "0d6 test");
        Assert(zeroDice == 0, "0d6 returns 0", $"got {zeroDice}");
    }
}
}
