using UnityEngine;

namespace Tests.Services
{
/// <summary>
/// Unit tests for CombatCalculationService — verifies hit determination,
/// AC modifiers, STR damage scaling, critical threat math, and miss chance.
/// Run with CombatCalculationServiceTests.RunAll().
/// </summary>
public static class CombatCalculationServiceTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== COMBAT CALCULATION SERVICE TESTS ======");

        TestIsHit_NaturalOne();
        TestIsHit_NaturalTwenty();
        TestIsHit_Normal();
        TestProneACModifier();
        TestTwoHandedStrDamage();
        TestOffHandStrDamage();
        TestClampMinimumDamage();
        TestIsCriticalThreat();
        TestDoubledThreatMin();
        TestCritBonusDice();
        TestConcealmentMiss();
        TestOpposedCheck();

        Debug.Log($"====== CombatCalculationService Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition) { _passed++; Debug.Log($"  ✅ {testName}"); }
        else { _failed++; Debug.LogError($"  ❌ {testName} {detail}"); }
    }

    private static void TestIsHit_NaturalOne()
    {
        // Natural 1 always misses
        bool hit = CombatCalculationService.IsHit(1, 50, 10);
        Assert(!hit, "Nat1 always misses");
    }

    private static void TestIsHit_NaturalTwenty()
    {
        // Natural 20 always hits
        bool hit = CombatCalculationService.IsHit(20, 5, 30);
        Assert(hit, "Nat20 always hits");
    }

    private static void TestIsHit_Normal()
    {
        // Total 15 vs AC 15 => hit (meets it beats it)
        bool hit = CombatCalculationService.IsHit(10, 15, 15);
        Assert(hit, "Total>=AC hits", $"got {hit}");
    }

    private static void TestProneACModifier()
    {
        Assert(CombatCalculationService.ProneACModifier(true) == 4, "Prone vs ranged +4");
        Assert(CombatCalculationService.ProneACModifier(false) == -4, "Prone vs melee -4");
    }

    private static void TestTwoHandedStrDamage()
    {
        // STR mod 4 × 1.5 = 6
        int dmg = CombatCalculationService.TwoHandedStrDamage(4);
        Assert(dmg == 6, "TwoHanded STR 4 => 6", $"got {dmg}");
    }

    private static void TestOffHandStrDamage()
    {
        // STR mod 4 × 0.5 = 2
        int dmg = CombatCalculationService.OffHandStrDamage(4);
        Assert(dmg == 2, "OffHand STR 4 => 2", $"got {dmg}");
    }

    private static void TestClampMinimumDamage()
    {
        int clamped = CombatCalculationService.ClampMinimumDamage(-3);
        Assert(clamped == 1, "ClampMin(-3)==1", $"got {clamped}");

        int normal = CombatCalculationService.ClampMinimumDamage(5);
        Assert(normal == 5, "ClampMin(5)==5", $"got {normal}");
    }

    private static void TestIsCriticalThreat()
    {
        Assert(CombatCalculationService.IsCriticalThreat(20, 20), "Nat20 crit threat");
        Assert(CombatCalculationService.IsCriticalThreat(19, 19), "19 with 19-20 range");
        Assert(!CombatCalculationService.IsCriticalThreat(18, 19), "18 not crit with 19-20");
    }

    private static void TestDoubledThreatMin()
    {
        // 19-20 doubled => 17-20
        int result = CombatCalculationService.DoubledThreatMin(19);
        Assert(result == 17, "Doubled 19-20 => 17-20", $"got {result}");
    }

    private static void TestCritBonusDice()
    {
        // 2d6 with x3 => 4 bonus dice (2 × (3-1))
        int bonus = CombatCalculationService.CritBonusDice(2, 3);
        Assert(bonus == 4, "CritBonus 2d6 x3 => 4", $"got {bonus}");
    }

    private static void TestConcealmentMiss()
    {
        // Roll 10 vs 20% => no miss (10 > 20 is false? depends on impl)
        // Roll 5 vs 20% => miss
        Assert(CombatCalculationService.ConcealmentMiss(5, 20), "Roll5 vs 20% => miss");
        Assert(!CombatCalculationService.ConcealmentMiss(25, 20), "Roll25 vs 20% => no miss");
    }

    private static void TestOpposedCheck()
    {
        Assert(CombatCalculationService.OpposedCheckWins(15, 14), "15 beats 14");
        Assert(CombatCalculationService.OpposedCheckWins(15, 15), "15 ties 15 => attacker wins");
        Assert(!CombatCalculationService.OpposedCheckWins(14, 15), "14 loses to 15");
    }
}
}
