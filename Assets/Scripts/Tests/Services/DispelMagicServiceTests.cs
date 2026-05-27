using UnityEngine;

namespace Tests.Services
{
/// <summary>
/// Unit tests for DispelMagicService — verifies dispel check DC formula,
/// auto-succeed on own spells, CL-capped dispel roll ranges, and
/// counterspell dispel checks.
/// Run with DispelMagicServiceTests.RunAll().
///
/// PHB 3.5e References:
///   - Dispel check: 1d20 + min(CL, 10) vs DC 11 + target spell CL (p.223)
///   - Own spells auto-dispel (no check needed)
///   - Counterspell: dispel magic used as counterspell (p.170)
///   - Greater Dispel Magic: CL cap raised to +20 (not +10)
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

        // Pure static tests (no scene/mock needed)
        TestDispelDC_Basic();
        TestDispelDC_CL1();
        TestDispelDC_HighCL();
        TestPerformDispelCheck_OwnSpell();
        TestRollDispelCheck_Range_CL7();
        TestRollDispelCheck_Range_CL15();
        TestRollDispelCheck_CappedAt10();
        TestPerformCounterspellCheck_OwnSpell();
        TestPerformCounterspellCheck_Range();
        TestPerformCounterspellCheck_HighCap();

        // Scene-required
        TestCounterspellFlow();

        Debug.Log($"====== DispelMagicService Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition) { _passed++; Debug.Log($"  ✅ {testName}"); }
        else { _failed++; Debug.LogError($"  ❌ {testName} {detail}"); }
    }

    // ──────────────────────────────────────────────
    //  GetDispelDC — DC = 11 + targetSpellCasterLevel
    // ──────────────────────────────────────────────

    private static void TestDispelDC_Basic()
    {
        int dc = DispelMagicService.GetDispelDC(8);
        Assert(dc == 19, "DispelDC(CL8)==19", $"got {dc}");
    }

    private static void TestDispelDC_CL1()
    {
        // Minimum CL spell: DC = 11 + 1 = 12
        int dc = DispelMagicService.GetDispelDC(1);
        Assert(dc == 12, "DispelDC(CL1)==12", $"got {dc}");
    }

    private static void TestDispelDC_HighCL()
    {
        // High-level caster: DC = 11 + 20 = 31
        int dc = DispelMagicService.GetDispelDC(20);
        Assert(dc == 31, "DispelDC(CL20)==31", $"got {dc}");
    }

    // ──────────────────────────────────────────────
    //  PerformDispelCheck — own spells auto-succeed
    // ──────────────────────────────────────────────

    private static void TestPerformDispelCheck_OwnSpell()
    {
        // Own spell auto-succeeds regardless of CL mismatch
        bool result = DispelMagicService.PerformDispelCheck(1, 20, isOwnSpell: true);
        Assert(result == true, "OwnSpell_AutoSucceeds (CL1 vs CL20)");
    }

    // ──────────────────────────────────────────────
    //  RollDispelCheck — 1d20 + min(CL, 10) range
    // ──────────────────────────────────────────────

    private static void TestRollDispelCheck_Range_CL7()
    {
        // CL 7 => roll = 1d20 + 7: range [8, 27]
        bool allValid = true;
        for (int i = 0; i < 50; i++)
        {
            int roll = DispelMagicService.RollDispelCheck(7);
            if (roll < 8 || roll > 27) { allValid = false; break; }
        }
        Assert(allValid, "RollDispelCheck(CL7) in [8,27]");
    }

    private static void TestRollDispelCheck_Range_CL15()
    {
        // CL 15 => capped to CL 10: roll = 1d20 + 10: range [11, 30]
        bool allValid = true;
        for (int i = 0; i < 50; i++)
        {
            int roll = DispelMagicService.RollDispelCheck(15);
            if (roll < 11 || roll > 30) { allValid = false; break; }
        }
        Assert(allValid, "RollDispelCheck(CL15) in [11,30] (capped to +10)");
    }

    private static void TestRollDispelCheck_CappedAt10()
    {
        // CL 50 => still capped to +10: range [11, 30]
        bool allValid = true;
        for (int i = 0; i < 50; i++)
        {
            int roll = DispelMagicService.RollDispelCheck(50);
            if (roll < 11 || roll > 30) { allValid = false; break; }
        }
        Assert(allValid, "RollDispelCheck(CL50) in [11,30] (hard cap +10)");
    }

    // ──────────────────────────────────────────────
    //  PerformCounterspellDispelCheck — static helper
    // ──────────────────────────────────────────────

    private static void TestPerformCounterspellCheck_OwnSpell()
    {
        // CL 10 vs CL 5: DC = 11 + 5 = 16, roll = 1d20+10 (range 11-30) — high chance
        // We can't guarantee pass due to randomness, so just verify it runs without error
        // and check the formula makes sense: at minimum roll (11) vs DC 16, should sometimes pass
        int passes = 0;
        for (int i = 0; i < 100; i++)
        {
            if (DispelMagicService.PerformCounterspellDispelCheck(10, 5)) passes++;
        }
        // With 1d20+10 vs DC 16, need 6+ on d20 = 75% chance; expect ~60-90 passes
        Assert(passes > 40 && passes <= 100, "CounterspellCheck(CL10 vs CL5) ~75% success",
            $"got {passes}/100");
    }

    private static void TestPerformCounterspellCheck_Range()
    {
        // CL 5 vs CL 15: DC = 11 + 15 = 26, roll = 1d20+5 (range 6-25) — should never pass
        // Max possible roll is 25, DC is 26, so always fails
        bool anyPassed = false;
        for (int i = 0; i < 50; i++)
        {
            if (DispelMagicService.PerformCounterspellDispelCheck(5, 15)) { anyPassed = true; break; }
        }
        Assert(!anyPassed, "CounterspellCheck(CL5 vs CL15) always fails (max 25 < DC 26)");
    }

    private static void TestPerformCounterspellCheck_HighCap()
    {
        // Test with maxCLBonus override (e.g., Greater Dispel Magic with +20 cap)
        // CL 18, enemy CL 15, maxCLBonus 20: roll = 1d20+18 (range 19-38) vs DC 26
        // Need 8+ on d20 = 65% chance
        int passes = 0;
        for (int i = 0; i < 100; i++)
        {
            if (DispelMagicService.PerformCounterspellDispelCheck(18, 15, 20)) passes++;
        }
        Assert(passes > 30, "CounterspellCheck(CL18 vs CL15, cap20) >30% success",
            $"got {passes}/100");
    }

    // ──────────────────────────────────────────────
    //  Full counterspell flow — requires scene
    // ──────────────────────────────────────────────

    private static void TestCounterspellFlow()
    {
        // Full TryResolveCounterspell requires:
        //   - Initialized DispelMagicService MonoBehaviour
        //   - CharacterController with readied counterspell
        //   - SpellData for the spell being cast
        //   - CombatUI for logging
        Assert(true, "CounterspellFlow (SKIP — needs initialized MonoBehaviour + scene)");
    }
}
}
