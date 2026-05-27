using UnityEngine;

namespace Tests.Services
{
/// <summary>
/// Unit tests for CombatCalculationService — verifies hit determination,
/// spell save DC, AC modifiers, STR damage scaling, critical threat math,
/// concealment miss chance, and opposed checks.
/// Run with CombatCalculationServiceTests.RunAll().
///
/// PHB 3.5e References:
///   - Attack roll: nat 1 always miss, nat 20 always hit, else total >= AC (p.139)
///   - Spell Save DC: 10 + spell level + casting ability mod (p.171)
///   - Prone: +4 AC vs ranged, -4 AC vs melee (p.151)
///   - STR to damage: 1× (one-hand), 1.5× (two-hand), 0.5× (off-hand) (p.134)
///   - Minimum weapon damage: 1 (nonlethal can be 0) (p.146)
///   - Critical threat: natural roll >= threat minimum (p.140)
///   - Improved Critical / Keen: double the threat range (p.140)
///   - Critical multiplier: ×2 = 1 extra set of dice, ×3 = 2 extra, ×4 = 3 extra (p.140)
///   - Concealment: percentile roll ≤ miss chance = miss (p.152)
///   - Opposed checks: attacker wins ties (p.65)
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

        // Hit determination
        TestIsHit_NaturalOne();
        TestIsHit_NaturalTwenty();
        TestIsHit_Normal_MeetsBeats();
        TestIsHit_Normal_Miss();
        TestIsHit_HighTotal_Nat1StillMisses();
        TestIsHit_LowTotal_Nat20StillHits();

        // Spell Save DC
        TestSpellSaveDC_Basic();
        TestSpellSaveDC_Cantrip();
        TestSpellSaveDC_HighLevel();

        // Prone AC
        TestProneACModifier_Ranged();
        TestProneACModifier_Melee();

        // STR damage scaling
        TestTwoHandedStrDamage();
        TestTwoHandedStrDamage_NegativeStr();
        TestOffHandStrDamage();
        TestOffHandStrDamage_NegativeStr();
        TestApplyStrDamageMultiplier_Normal();
        TestApplyStrDamageMultiplier_NegativePassthrough();

        // Minimum damage clamping
        TestClampMinimumDamage_Lethal();
        TestClampMinimumDamage_Nonlethal();

        // Critical threats
        TestIsCriticalThreat_Nat20();
        TestIsCriticalThreat_19_20Range();
        TestIsCriticalThreat_18_20Range();
        TestIsCriticalThreat_NotInRange();
        TestDoubledThreatMin_19_20();
        TestDoubledThreatMin_20();
        TestDoubledThreatMin_18_20();
        TestCritBonusDice_x2();
        TestCritBonusDice_x3();
        TestCritBonusDice_x4();

        // Concealment
        TestConcealmentMiss_Miss();
        TestConcealmentMiss_NoMiss();
        TestConcealmentMiss_Boundary();

        // Opposed checks
        TestOpposedCheck_AttackerWins();
        TestOpposedCheck_TieGoesToAttacker();
        TestOpposedCheck_DefenderWins();

        Debug.Log($"====== CombatCalculationService Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition) { _passed++; Debug.Log($"  ✅ {testName}"); }
        else { _failed++; Debug.LogError($"  ❌ {testName} {detail}"); }
    }

    // ──────────────────────────────────────────────
    //  IsHit — nat1 miss, nat20 hit, else total>=AC
    // ──────────────────────────────────────────────

    private static void TestIsHit_NaturalOne()
    {
        // Natural 1 always misses, even with huge total
        bool hit = CombatCalculationService.IsHit(1, 50, 10);
        Assert(!hit, "Nat1 always misses (total 50 vs AC 10)");
    }

    private static void TestIsHit_NaturalTwenty()
    {
        // Natural 20 always hits, even against impossible AC
        bool hit = CombatCalculationService.IsHit(20, 5, 30);
        Assert(hit, "Nat20 always hits (total 5 vs AC 30)");
    }

    private static void TestIsHit_Normal_MeetsBeats()
    {
        // Total 15 vs AC 15 => hit ("meets it, beats it")
        bool hit = CombatCalculationService.IsHit(10, 15, 15);
        Assert(hit, "Total==AC hits (15 vs 15)");

        // Total 16 vs AC 15 => hit
        bool hit2 = CombatCalculationService.IsHit(10, 16, 15);
        Assert(hit2, "Total>AC hits (16 vs 15)");
    }

    private static void TestIsHit_Normal_Miss()
    {
        // Total 14 vs AC 15 => miss
        bool hit = CombatCalculationService.IsHit(10, 14, 15);
        Assert(!hit, "Total<AC misses (14 vs 15)");
    }

    private static void TestIsHit_HighTotal_Nat1StillMisses()
    {
        // Natural 1 with total 100 vs AC 5 => still misses
        bool hit = CombatCalculationService.IsHit(1, 100, 5);
        Assert(!hit, "Nat1 with total 100 still misses");
    }

    private static void TestIsHit_LowTotal_Nat20StillHits()
    {
        // Natural 20 with total 0 vs AC 50 => still hits
        bool hit = CombatCalculationService.IsHit(20, 0, 50);
        Assert(hit, "Nat20 with total 0 still hits AC 50");
    }

    // ──────────────────────────────────────────────
    //  SpellSaveDC — 10 + spellLevel + castingAbilityMod
    // ──────────────────────────────────────────────

    private static void TestSpellSaveDC_Basic()
    {
        int dc = CombatCalculationService.SpellSaveDC(3, 4);
        Assert(dc == 17, "SpellSaveDC(3,4)==17", $"got {dc}");
    }

    private static void TestSpellSaveDC_Cantrip()
    {
        // Cantrip (level 0) with +2 mod
        int dc = CombatCalculationService.SpellSaveDC(0, 2);
        Assert(dc == 12, "SpellSaveDC(0,2)==12", $"got {dc}");
    }

    private static void TestSpellSaveDC_HighLevel()
    {
        // 9th-level spell, +8 mod
        int dc = CombatCalculationService.SpellSaveDC(9, 8);
        Assert(dc == 27, "SpellSaveDC(9,8)==27", $"got {dc}");
    }

    // ──────────────────────────────────────────────
    //  ProneACModifier — +4 ranged, -4 melee
    // ──────────────────────────────────────────────

    private static void TestProneACModifier_Ranged()
    {
        int mod = CombatCalculationService.ProneACModifier(true);
        Assert(mod == 4, "Prone vs ranged => +4", $"got {mod}");
    }

    private static void TestProneACModifier_Melee()
    {
        int mod = CombatCalculationService.ProneACModifier(false);
        Assert(mod == -4, "Prone vs melee => -4", $"got {mod}");
    }

    // ──────────────────────────────────────────────
    //  STR damage scaling
    // ──────────────────────────────────────────────

    private static void TestTwoHandedStrDamage()
    {
        // STR mod 4 × 1.5 = 6
        int dmg = CombatCalculationService.TwoHandedStrDamage(4);
        Assert(dmg == 6, "TwoHanded STR 4 => 6", $"got {dmg}");
    }

    private static void TestTwoHandedStrDamage_NegativeStr()
    {
        // Negative STR is always 1× per PHB (no beneficial multiplier)
        int dmg = CombatCalculationService.TwoHandedStrDamage(-2);
        Assert(dmg == -2, "TwoHanded STR -2 => -2 (negative always 1x)", $"got {dmg}");
    }

    private static void TestOffHandStrDamage()
    {
        // STR mod 4 × 0.5 = 2
        int dmg = CombatCalculationService.OffHandStrDamage(4);
        Assert(dmg == 2, "OffHand STR 4 => 2", $"got {dmg}");
    }

    private static void TestOffHandStrDamage_NegativeStr()
    {
        // Negative STR always 1×
        int dmg = CombatCalculationService.OffHandStrDamage(-3);
        Assert(dmg == -3, "OffHand STR -3 => -3 (negative always 1x)", $"got {dmg}");
    }

    private static void TestApplyStrDamageMultiplier_Normal()
    {
        // STR 6 × 1.5 = 9
        int dmg = CombatCalculationService.ApplyStrDamageMultiplier(6, 1.5f);
        Assert(dmg == 9, "StrDmg(6,1.5)==9", $"got {dmg}");

        // STR 3 × 0.5 = 1 (floor)
        int dmg2 = CombatCalculationService.ApplyStrDamageMultiplier(3, 0.5f);
        Assert(dmg2 == 1, "StrDmg(3,0.5)==1", $"got {dmg2}");
    }

    private static void TestApplyStrDamageMultiplier_NegativePassthrough()
    {
        // Negative STR always 1× regardless of multiplier
        int dmg = CombatCalculationService.ApplyStrDamageMultiplier(-4, 1.5f);
        Assert(dmg == -4, "StrDmg(-4,1.5)==-4 (negative passthrough)", $"got {dmg}");
    }

    // ──────────────────────────────────────────────
    //  ClampMinimumDamage — lethal ≥ 1, nonlethal ≥ 0
    // ──────────────────────────────────────────────

    private static void TestClampMinimumDamage_Lethal()
    {
        Assert(CombatCalculationService.ClampMinimumDamage(-3) == 1,
            "ClampMin(-3)==1 (lethal)");
        Assert(CombatCalculationService.ClampMinimumDamage(0) == 1,
            "ClampMin(0)==1 (lethal)");
        Assert(CombatCalculationService.ClampMinimumDamage(5) == 5,
            "ClampMin(5)==5 (no change)");
    }

    private static void TestClampMinimumDamage_Nonlethal()
    {
        Assert(CombatCalculationService.ClampMinimumDamage(-3, true) == 0,
            "ClampMin(-3,nonlethal)==0");
        Assert(CombatCalculationService.ClampMinimumDamage(0, true) == 0,
            "ClampMin(0,nonlethal)==0");
        Assert(CombatCalculationService.ClampMinimumDamage(5, true) == 5,
            "ClampMin(5,nonlethal)==5");
    }

    // ──────────────────────────────────────────────
    //  Critical threat — naturalRoll >= critThreatMin
    // ──────────────────────────────────────────────

    private static void TestIsCriticalThreat_Nat20()
    {
        Assert(CombatCalculationService.IsCriticalThreat(20, 20), "Nat20 crit threat (20/×2)");
    }

    private static void TestIsCriticalThreat_19_20Range()
    {
        Assert(CombatCalculationService.IsCriticalThreat(19, 19), "19 with 19-20 range");
        Assert(CombatCalculationService.IsCriticalThreat(20, 19), "20 with 19-20 range");
        Assert(!CombatCalculationService.IsCriticalThreat(18, 19), "18 NOT crit with 19-20");
    }

    private static void TestIsCriticalThreat_18_20Range()
    {
        // Keen/Improved Critical scimitar: 18-20
        Assert(CombatCalculationService.IsCriticalThreat(18, 18), "18 with 18-20 range");
        Assert(!CombatCalculationService.IsCriticalThreat(17, 18), "17 NOT crit with 18-20");
    }

    private static void TestIsCriticalThreat_NotInRange()
    {
        Assert(!CombatCalculationService.IsCriticalThreat(15, 20), "15 not crit with 20 range");
    }

    // ──────────────────────────────────────────────
    //  Doubled threat range (Improved Critical / Keen)
    //  Formula: 21 - 2 × (21 - baseThreatMin)
    // ──────────────────────────────────────────────

    private static void TestDoubledThreatMin_19_20()
    {
        // 19-20 doubled => 17-20 (range goes from 2 to 4)
        int result = CombatCalculationService.DoubledThreatMin(19);
        Assert(result == 17, "Doubled 19-20 => 17-20", $"got {result}");
    }

    private static void TestDoubledThreatMin_20()
    {
        // 20 doubled => 19-20 (range goes from 1 to 2)
        int result = CombatCalculationService.DoubledThreatMin(20);
        Assert(result == 19, "Doubled 20 => 19-20", $"got {result}");
    }

    private static void TestDoubledThreatMin_18_20()
    {
        // 18-20 doubled => 15-20 (range goes from 3 to 6)
        int result = CombatCalculationService.DoubledThreatMin(18);
        Assert(result == 15, "Doubled 18-20 => 15-20", $"got {result}");
    }

    // ──────────────────────────────────────────────
    //  CritBonusDice — baseDice × (multiplier - 1)
    // ──────────────────────────────────────────────

    private static void TestCritBonusDice_x2()
    {
        // 1d8 with ×2 => 1 bonus die
        int bonus = CombatCalculationService.CritBonusDice(1, 2);
        Assert(bonus == 1, "CritBonus 1d8 x2 => 1", $"got {bonus}");
    }

    private static void TestCritBonusDice_x3()
    {
        // 2d6 with ×3 => 4 bonus dice (2 × 2)
        int bonus = CombatCalculationService.CritBonusDice(2, 3);
        Assert(bonus == 4, "CritBonus 2d6 x3 => 4", $"got {bonus}");
    }

    private static void TestCritBonusDice_x4()
    {
        // 1d12 with ×4 => 3 bonus dice (1 × 3)
        int bonus = CombatCalculationService.CritBonusDice(1, 4);
        Assert(bonus == 3, "CritBonus 1d12 x4 => 3", $"got {bonus}");
    }

    // ──────────────────────────────────────────────
    //  Concealment — percentileRoll <= missChance => miss
    // ──────────────────────────────────────────────

    private static void TestConcealmentMiss_Miss()
    {
        Assert(CombatCalculationService.ConcealmentMiss(5, 20), "Roll5 vs 20% => miss");
        Assert(CombatCalculationService.ConcealmentMiss(1, 50), "Roll1 vs 50% => miss");
    }

    private static void TestConcealmentMiss_NoMiss()
    {
        Assert(!CombatCalculationService.ConcealmentMiss(25, 20), "Roll25 vs 20% => no miss");
        Assert(!CombatCalculationService.ConcealmentMiss(100, 50), "Roll100 vs 50% => no miss");
    }

    private static void TestConcealmentMiss_Boundary()
    {
        // Exactly at boundary: roll == missChance => miss
        Assert(CombatCalculationService.ConcealmentMiss(20, 20), "Roll20 vs 20% => miss (boundary)");
        // One above boundary: roll == missChance+1 => no miss
        Assert(!CombatCalculationService.ConcealmentMiss(21, 20), "Roll21 vs 20% => no miss");
    }

    // ──────────────────────────────────────────────
    //  Opposed checks — attacker wins ties
    // ──────────────────────────────────────────────

    private static void TestOpposedCheck_AttackerWins()
    {
        Assert(CombatCalculationService.OpposedCheckWins(15, 14), "15 beats 14");
        Assert(CombatCalculationService.OpposedCheckWins(30, 10), "30 beats 10");
    }

    private static void TestOpposedCheck_TieGoesToAttacker()
    {
        Assert(CombatCalculationService.OpposedCheckWins(15, 15), "Tie (15v15) => attacker wins");
        Assert(CombatCalculationService.OpposedCheckWins(0, 0), "Tie (0v0) => attacker wins");
    }

    private static void TestOpposedCheck_DefenderWins()
    {
        Assert(!CombatCalculationService.OpposedCheckWins(14, 15), "14 loses to 15");
        Assert(!CombatCalculationService.OpposedCheckWins(0, 1), "0 loses to 1");
    }
}
}
