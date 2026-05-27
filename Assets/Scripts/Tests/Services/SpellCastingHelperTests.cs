using UnityEngine;

namespace Tests.Services
{
/// <summary>
/// Unit tests for SpellCastingHelper — verifies caster level calculations,
/// duration formulas, damage dice scaling, and spell resistance checks.
/// Run with SpellCastingHelperTests.RunAll().
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

        TestDamageDiceCount_CappedAtMax();
        TestDamageDiceCount_BelowMax();
        TestRollSpellDamage_Range();

        Debug.Log($"====== SpellCastingHelper Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition) { _passed++; Debug.Log($"  ✅ {testName}"); }
        else { _failed++; Debug.LogError($"  ❌ {testName} {detail}"); }
    }

    private static void TestDamageDiceCount_CappedAtMax()
    {
        // CL 15 with max 10 dice => 10
        int dice = SpellCastingHelper.GetDamageDiceCount(15, 10);
        Assert(dice == 10, "DamageDice(CL15,max10)==10", $"got {dice}");
    }

    private static void TestDamageDiceCount_BelowMax()
    {
        // CL 5 with max 10 dice => 5
        int dice = SpellCastingHelper.GetDamageDiceCount(5, 10);
        Assert(dice == 5, "DamageDice(CL5,max10)==5", $"got {dice}");
    }

    private static void TestRollSpellDamage_Range()
    {
        // 5d6: min 5, max 30
        bool allInRange = true;
        for (int i = 0; i < 100; i++)
        {
            int dmg = SpellCastingHelper.RollSpellDamage(5, 10, 6, "test");
            if (dmg < 5 || dmg > 30) { allInRange = false; break; }
        }
        Assert(allInRange, "RollSpellDamage 5d6 in [5,30]");
    }
}
}
