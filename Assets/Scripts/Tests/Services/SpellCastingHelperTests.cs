using UnityEngine;

namespace Tests.Services
{
/// <summary>
/// Unit tests for SpellCastingHelper — verifies damage dice scaling,
/// spell damage rolling, and caster level clamping.
/// Run with SpellCastingHelperTests.RunAll().
///
/// PHB 3.5e References:
///   - Damage dice per caster level, capped at spell maximum (varies by spell)
///   - Fireball: 1d6/CL, max 10d6 (PHB p.231)
///   - Scorching Ray: 4d6 per ray (PHB p.274)
///   - Caster level minimum is 1 (PHB p.171)
/// </summary>
public static class SpellCastingHelperTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== SPELL CASTING HELPER TESTS ======");

        // Pure-function tests
        TestDamageDiceCount_CappedAtMax();
        TestDamageDiceCount_BelowMax();
        TestDamageDiceCount_AtExactMax();
        TestDamageDiceCount_CL1();
        TestDamageDiceCount_ClampedToMinimum1();
        TestRollSpellDamage_Range();
        TestRollSpellDamage_SingleDie();
        TestRollSpellDamage_MaxDice();

        Debug.Log($"====== SpellCastingHelper Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition) { _passed++; Debug.Log($"  ✅ {testName}"); }
        else { _failed++; Debug.LogError($"  ❌ {testName} {detail}"); }
    }

    // ──────────────────────────────────────────────
    //  GetDamageDiceCount — Mathf.Clamp(CL, 1, max)
    // ──────────────────────────────────────────────

    private static void TestDamageDiceCount_CappedAtMax()
    {
        // CL 15 with max 10 dice => 10  (Fireball at CL 15)
        int dice = SpellCastingHelper.GetDamageDiceCount(15, 10);
        Assert(dice == 10, "DamageDice(CL15,max10)==10", $"got {dice}");
    }

    private static void TestDamageDiceCount_BelowMax()
    {
        // CL 5 with max 10 dice => 5  (Fireball at CL 5)
        int dice = SpellCastingHelper.GetDamageDiceCount(5, 10);
        Assert(dice == 5, "DamageDice(CL5,max10)==5", $"got {dice}");
    }

    private static void TestDamageDiceCount_AtExactMax()
    {
        // CL 10 with max 10 dice => 10  (exactly at cap)
        int dice = SpellCastingHelper.GetDamageDiceCount(10, 10);
        Assert(dice == 10, "DamageDice(CL10,max10)==10", $"got {dice}");
    }

    private static void TestDamageDiceCount_CL1()
    {
        // CL 1 with max 5 dice => 1  (minimum caster level)
        int dice = SpellCastingHelper.GetDamageDiceCount(1, 5);
        Assert(dice == 1, "DamageDice(CL1,max5)==1", $"got {dice}");
    }

    private static void TestDamageDiceCount_ClampedToMinimum1()
    {
        // Edge case: CL 0 should clamp to at least 1 die
        int dice = SpellCastingHelper.GetDamageDiceCount(0, 5);
        Assert(dice == 1, "DamageDice(CL0,max5)==1 (clamped)", $"got {dice}");
    }

    // ──────────────────────────────────────────────
    //  RollSpellDamage — range checks  (random)
    // ──────────────────────────────────────────────

    private static void TestRollSpellDamage_Range()
    {
        // 5d6: min 5, max 30 — verify 100 rolls stay in range
        bool allInRange = true;
        for (int i = 0; i < 100; i++)
        {
            int dmg = SpellCastingHelper.RollSpellDamage(5, 10, 6, "test_5d6");
            if (dmg < 5 || dmg > 30) { allInRange = false; break; }
        }
        Assert(allInRange, "RollSpellDamage 5d6 in [5,30]");
    }

    private static void TestRollSpellDamage_SingleDie()
    {
        // CL 1, max 10, d8: should produce 1d8 => [1,8]
        bool allInRange = true;
        for (int i = 0; i < 50; i++)
        {
            int dmg = SpellCastingHelper.RollSpellDamage(1, 10, 8, "test_1d8");
            if (dmg < 1 || dmg > 8) { allInRange = false; break; }
        }
        Assert(allInRange, "RollSpellDamage 1d8 in [1,8]");
    }

    private static void TestRollSpellDamage_MaxDice()
    {
        // CL 20, max 10, d6: capped to 10d6 => [10,60]
        bool allInRange = true;
        for (int i = 0; i < 100; i++)
        {
            int dmg = SpellCastingHelper.RollSpellDamage(20, 10, 6, "test_10d6");
            if (dmg < 10 || dmg > 60) { allInRange = false; break; }
        }
        Assert(allInRange, "RollSpellDamage 10d6 (CL20 capped) in [10,60]");
    }
}
}
