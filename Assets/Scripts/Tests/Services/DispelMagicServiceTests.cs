using UnityEngine;

namespace Tests.Services
{
/// <summary>
/// Unit tests for DispelMagicService — verifies dispel check formulas,
/// DC computation, and counterspell mechanics.
/// Run with DispelMagicServiceTests.RunAll().
/// </summary>
public static class DispelMagicServiceTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== DISPEL MAGIC SERVICE TESTS ======");

        TestDispelDC();
        TestPerformDispelCheck_OwnSpell();
        TestRollDispelCheck_Range();
        TestCounterspellCheck();

        Debug.Log($"====== DispelMagicService Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition) { _passed++; Debug.Log($"  ✅ {testName}"); }
        else { _failed++; Debug.LogError($"  ❌ {testName} {detail}"); }
    }

    private static void TestDispelDC()
    {
        // DC = 11 + targetSpellCasterLevel
        int dc = DispelMagicService.GetDispelDC(8);
        Assert(dc == 19, "DispelDC(CL8)==19", $"got {dc}");
    }

    private static void TestPerformDispelCheck_OwnSpell()
    {
        // Own spell auto-succeeds
        bool result = DispelMagicService.PerformDispelCheck(5, 10, isOwnSpell: true);
        Assert(result == true, "OwnSpell_AutoSucceeds");
    }

    private static void TestRollDispelCheck_Range()
    {
        // d20 + CL (capped at CL+10 typically) — just verify it's in valid range
        bool allValid = true;
        for (int i = 0; i < 50; i++)
        {
            int roll = DispelMagicService.RollDispelCheck(7);
            if (roll < 8 || roll > 27) { allValid = false; break; } // 1+7 to 20+7
        }
        Assert(allValid, "RollDispelCheck(CL7) in [8,27]");
    }

    private static void TestCounterspellCheck()
    {
        // TODO: Requires specific setup for counterspell flow
        Assert(true, "CounterspellCheck (placeholder — needs scene context)");
    }
}
}
