using UnityEngine;

namespace Tests.Services
{
/// <summary>
/// Unit tests for ConcentrationService — verifies DC formulas for defensive
/// casting, damage, grappling, entanglement, and success chance math.
/// Run with ConcentrationServiceTests.RunAll().
/// </summary>
public static class ConcentrationServiceTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== CONCENTRATION SERVICE TESTS ======");

        TestDefensiveCastingDC();
        TestDamageDC();
        TestGrappledCastingDC();
        TestEntangledCastingDC();
        TestVigorousMotionDC();
        TestViolentMotionDC();
        TestSuccessChancePercent_AutoSuccess();
        TestSuccessChancePercent_AutoFail();
        TestSuccessChancePercent_MidRange();

        Debug.Log($"====== ConcentrationService Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition) { _passed++; Debug.Log($"  ✅ {testName}"); }
        else { _failed++; Debug.LogError($"  ❌ {testName} {detail}"); }
    }

    private static void TestDefensiveCastingDC()
    {
        // DC = 15 + spellLevel
        int dc = ConcentrationService.GetDefensiveCastingDC(3);
        Assert(dc == 18, "DefensiveDC(3)==18", $"got {dc}");
    }

    private static void TestDamageDC()
    {
        // DC = 10 + damage + spellLevel
        int dc = ConcentrationService.GetDamageDC(12, 2);
        Assert(dc == 24, "DamageDC(12,2)==24", $"got {dc}");
    }

    private static void TestGrappledCastingDC()
    {
        // DC = 20 + spellLevel
        int dc = ConcentrationService.GetGrappledCastingDC(4);
        Assert(dc == 24, "GrappledDC(4)==24", $"got {dc}");
    }

    private static void TestEntangledCastingDC()
    {
        // DC = 15 + spellLevel
        int dc = ConcentrationService.GetEntangledCastingDC(2);
        Assert(dc == 17, "EntangledDC(2)==17", $"got {dc}");
    }

    private static void TestVigorousMotionDC()
    {
        // DC = 10 + spellLevel
        int dc = ConcentrationService.GetVigorousMotionDC(5);
        Assert(dc == 15, "VigorousDC(5)==15", $"got {dc}");
    }

    private static void TestViolentMotionDC()
    {
        // DC = 15 + spellLevel
        int dc = ConcentrationService.GetViolentMotionDC(3);
        Assert(dc == 18, "ViolentDC(3)==18", $"got {dc}");
    }

    private static void TestSuccessChancePercent_AutoSuccess()
    {
        // bonus +30 vs DC 10 => always succeed (nat-1 still fails in D&D but chance = 95% cap)
        float pct = ConcentrationService.CalculateSuccessChancePercent(30, 10);
        Assert(pct >= 95f, "SuccessChance_AutoSuccess>=95", $"got {pct}");
    }

    private static void TestSuccessChancePercent_AutoFail()
    {
        // bonus -5 vs DC 30 => min 5% (nat-20)
        float pct = ConcentrationService.CalculateSuccessChancePercent(-5, 30);
        Assert(pct <= 5f, "SuccessChance_AutoFail<=5", $"got {pct}");
    }

    private static void TestSuccessChancePercent_MidRange()
    {
        // bonus +10 vs DC 15 => need 5+, 80% chance
        float pct = ConcentrationService.CalculateSuccessChancePercent(10, 15);
        Assert(pct > 70f && pct < 90f, "SuccessChance_MidRange~80", $"got {pct}");
    }
}
}
