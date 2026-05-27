using UnityEngine;

namespace Tests.Services
{
/// <summary>
/// Unit tests for ConcentrationService — verifies DC formulas for defensive
/// casting, damage, grappling, entanglement, vigorous/violent motion, casting
/// while concentrating, and success chance math.
/// Run with ConcentrationServiceTests.RunAll().
///
/// PHB 3.5e References:
///   - Defensive Casting: DC = 15 + spell level (p.170)
///   - Damage: DC = 10 + damage dealt + spell level (p.170)
///   - Grappled/Pinned: DC = 20 + spell level (p.170)
///   - Entangled: DC = 15 + spell level (p.170)
///   - Vigorous Motion: DC = 10 + spell level (p.170)
///   - Violent Motion: DC = 15 + spell level (p.170)
///   - Casting while concentrating: DC = 15 + new spell level (p.170)
///   - Natural 1 always fails, natural 20 always succeeds on ability checks
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

        // DC formula tests
        TestDefensiveCastingDC();
        TestDefensiveCastingDC_Cantrip();
        TestDamageDC();
        TestDamageDC_MinimalDamage();
        TestGrappledCastingDC();
        TestEntangledCastingDC();
        TestVigorousMotionDC();
        TestViolentMotionDC();
        TestCastingWhileConcentratingDC();

        // Success chance tests
        TestSuccessChancePercent_AutoSuccess();
        TestSuccessChancePercent_AutoFail();
        TestSuccessChancePercent_MidRange();
        TestSuccessChancePercent_Exact50();
        TestSuccessChanceFraction_Range();

        Debug.Log($"====== ConcentrationService Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition) { _passed++; Debug.Log($"  ✅ {testName}"); }
        else { _failed++; Debug.LogError($"  ❌ {testName} {detail}"); }
    }

    // ──────────────────────────────────────────────
    //  DC formulas — pure functions
    // ──────────────────────────────────────────────

    private static void TestDefensiveCastingDC()
    {
        // DC = 15 + spellLevel; level 3 => 18
        int dc = ConcentrationService.GetDefensiveCastingDC(3);
        Assert(dc == 18, "DefensiveDC(3)==18", $"got {dc}");
    }

    private static void TestDefensiveCastingDC_Cantrip()
    {
        // DC = 15 + 0 = 15 for cantrips
        int dc = ConcentrationService.GetDefensiveCastingDC(0);
        Assert(dc == 15, "DefensiveDC(0)==15 (cantrip)", $"got {dc}");
    }

    private static void TestDamageDC()
    {
        // DC = 10 + damage + spellLevel; 12 damage, level 2 => 24
        int dc = ConcentrationService.GetDamageDC(12, 2);
        Assert(dc == 24, "DamageDC(12,2)==24", $"got {dc}");
    }

    private static void TestDamageDC_MinimalDamage()
    {
        // DC = 10 + 1 + 0 = 11 (1 damage, cantrip)
        int dc = ConcentrationService.GetDamageDC(1, 0);
        Assert(dc == 11, "DamageDC(1,0)==11 (min damage cantrip)", $"got {dc}");
    }

    private static void TestGrappledCastingDC()
    {
        // DC = 20 + spellLevel; level 4 => 24
        int dc = ConcentrationService.GetGrappledCastingDC(4);
        Assert(dc == 24, "GrappledDC(4)==24", $"got {dc}");
    }

    private static void TestEntangledCastingDC()
    {
        // DC = 15 + spellLevel; level 2 => 17
        int dc = ConcentrationService.GetEntangledCastingDC(2);
        Assert(dc == 17, "EntangledDC(2)==17", $"got {dc}");
    }

    private static void TestVigorousMotionDC()
    {
        // DC = 10 + spellLevel; level 5 => 15
        int dc = ConcentrationService.GetVigorousMotionDC(5);
        Assert(dc == 15, "VigorousDC(5)==15", $"got {dc}");
    }

    private static void TestViolentMotionDC()
    {
        // DC = 15 + spellLevel; level 3 => 18
        int dc = ConcentrationService.GetViolentMotionDC(3);
        Assert(dc == 18, "ViolentDC(3)==18", $"got {dc}");
    }

    private static void TestCastingWhileConcentratingDC()
    {
        // DC = 15 + newSpellLevel; casting a 5th-level spell while concentrating => 20
        int dc = ConcentrationService.GetCastingWhileConcentratingDC(5);
        Assert(dc == 20, "CastingWhileConcentrating(5)==20", $"got {dc}");
    }

    // ──────────────────────────────────────────────
    //  Success chance — clamped to [5%, 95%]
    //  Formula: (21 - (dc - bonus)) / 20 * 100
    // ──────────────────────────────────────────────

    private static void TestSuccessChancePercent_AutoSuccess()
    {
        // bonus +30 vs DC 10: need -20 on d20, always succeed => capped at 95%
        float pct = ConcentrationService.CalculateSuccessChancePercent(30, 10);
        Assert(pct >= 95f, "SuccessChance_AutoSuccess>=95", $"got {pct}");
    }

    private static void TestSuccessChancePercent_AutoFail()
    {
        // bonus -5 vs DC 30: need 35 on d20, impossible => capped at 5%
        float pct = ConcentrationService.CalculateSuccessChancePercent(-5, 30);
        Assert(pct <= 5f, "SuccessChance_AutoFail<=5", $"got {pct}");
    }

    private static void TestSuccessChancePercent_MidRange()
    {
        // bonus +10 vs DC 15: need 5+ on d20
        // Success = (21 - 5) / 20 * 100 = 80%
        float pct = ConcentrationService.CalculateSuccessChancePercent(10, 15);
        Assert(pct > 70f && pct < 90f, "SuccessChance_MidRange~80%", $"got {pct}");
    }

    private static void TestSuccessChancePercent_Exact50()
    {
        // bonus +0 vs DC 11: need 11+ on d20
        // Success = (21 - 11) / 20 * 100 = 50%
        float pct = ConcentrationService.CalculateSuccessChancePercent(0, 11);
        Assert(pct >= 45f && pct <= 55f, "SuccessChance_Exact50~50%", $"got {pct}");
    }

    private static void TestSuccessChanceFraction_Range()
    {
        // Fraction should be between 0.05 and 0.95
        float frac = ConcentrationService.CalculateSuccessChanceFraction(10, 15);
        Assert(frac >= 0.05f && frac <= 0.95f, "SuccessFraction in [0.05, 0.95]", $"got {frac}");

        // High bonus => should be near 0.95
        float high = ConcentrationService.CalculateSuccessChanceFraction(30, 10);
        Assert(high >= 0.90f, "SuccessFraction_High>=0.90", $"got {high}");

        // Low bonus => should be near 0.05
        float low = ConcentrationService.CalculateSuccessChanceFraction(-5, 30);
        Assert(low <= 0.10f, "SuccessFraction_Low<=0.10", $"got {low}");
    }
}
}
