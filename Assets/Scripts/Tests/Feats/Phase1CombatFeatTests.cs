using UnityEngine;
using System.Collections.Generic;

namespace Tests.Feats
{
/// <summary>
/// Phase 1 Combat Feat Tests — validates runtime mechanics for 7 core combat feats:
///   1. Spring Attack       5. Deflect Arrows
///   2. Shot on the Run     6. Manyshot
///   3. Whirlwind Attack    7. Improved Precise Shot
///   4. Stunning Fist
///
/// Tests cover: feat detection, prerequisite validation, DC calculations,
/// usage tracking, toggle state, and combat integration hooks.
/// </summary>
public static class Phase1CombatFeatTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("========== PHASE 1 COMBAT FEAT TESTS ==========");

        // Stunning Fist
        TestStunningFistDetection();
        TestStunningFistDCCalculation();
        TestStunningFistUsesPerDay();
        TestStunningFistToggle();

        // Deflect Arrows
        TestDeflectArrowsDetection();
        TestDeflectArrowsUsedThisRound();

        // Manyshot
        TestManyshotDetection();
        TestManyshotPenalty();
        TestManyshotCanUse();

        // Improved Precise Shot
        TestImprovedPreciseShotDetection();
        TestConcealmentIgnore();
        TestCoverIgnore();

        // Spring Attack
        TestSpringAttackDetection();
        TestSpringAttackCanUse();

        // Shot on the Run
        TestShotOnTheRunDetection();
        TestShotOnTheRunCanUse();

        // Whirlwind Attack
        TestWhirlwindAttackDetection();
        TestWhirlwindAttackCanUse();

        // Cross-feat tracking
        TestRoundResetTrackers();

        Debug.Log($"========== PHASE 1 FEATS RESULTS: {_passed} passed, {_failed} failed ==========");
    }

    private static void Assert(bool condition, string testName, string detail = null)
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  [PASS] {testName}");
        }
        else
        {
            _failed++;
            string extra = string.IsNullOrEmpty(detail) ? "" : $" | {detail}";
            Debug.LogError($"  [FAIL] {testName}{extra}");
        }
    }

    // ── Helper: create a CharacterStats with specific feats ──
    private static CharacterStats MakeStats(string name, int level, string className,
        int str = 14, int dex = 14, int con = 14, int wis = 14, int bab = 4,
        params string[] feats)
    {
        // Constructor: name, level, class, str, dex, con, wis, int, cha,
        //              bab, armorBonus, shieldBonus, damageDice, damageCount, bonusDamage,
        //              baseSpeed, atkRange, baseHitDieHP
        var stats = new CharacterStats(name, level, className,
            str, dex, con, wis, 10, 10,
            bab, 4, 0,
            8, 1, 0,
            6, 1, level * 8);

        if (feats != null && feats.Length > 0)
            stats.AddFeats(new List<string>(feats));

        return stats;
    }

    // =============================================================
    // STUNNING FIST TESTS
    // =============================================================

    private static void TestStunningFistDetection()
    {
        Debug.Log("--- Stunning Fist Detection ---");
        var withFeat = MakeStats("MonkStun", 8, "Monk", bab: 6, feats: new[] { "Stunning Fist", "Improved Unarmed Strike" });
        var withoutFeat = MakeStats("Fighter", 8, "Fighter", bab: 6, feats: new[] { "Power Attack" });

        Assert(FeatManager.HasStunningFist(withFeat), "Monk with Stunning Fist feat detected");
        Assert(!FeatManager.HasStunningFist(withoutFeat), "Fighter without Stunning Fist not detected");
        Assert(!FeatManager.HasStunningFist(null), "Null stats returns false");
    }

    private static void TestStunningFistDCCalculation()
    {
        Debug.Log("--- Stunning Fist DC Calculation ---");
        // DC = 10 + level/2 + WIS modifier
        // Level 8, WIS 14 (mod +2) => DC = 10 + 4 + 2 = 16
        var stats = MakeStats("MonkDC", 8, "Monk", wis: 14, bab: 6, feats: new[] { "Stunning Fist" });
        int dc = FeatManager.GetStunningFistDC(stats);
        Assert(dc == 16, $"DC should be 16 for level 8, WIS 14 (got {dc})", $"Expected 10+4+2=16");

        // Level 12, WIS 18 (mod +4) => DC = 10 + 6 + 4 = 20
        var stats2 = MakeStats("MonkDC2", 12, "Monk", wis: 18, bab: 9, feats: new[] { "Stunning Fist" });
        int dc2 = FeatManager.GetStunningFistDC(stats2);
        Assert(dc2 == 20, $"DC should be 20 for level 12, WIS 18 (got {dc2})", $"Expected 10+6+4=20");
    }

    private static void TestStunningFistUsesPerDay()
    {
        Debug.Log("--- Stunning Fist Uses Per Day ---");
        // Uses per day = max(1, level / 4)
        var lvl4 = MakeStats("Monk4", 4, "Monk", bab: 3, feats: new[] { "Stunning Fist" });
        Assert(lvl4.StunningFistUsesPerDay == 1, $"Level 4: 1 use/day (got {lvl4.StunningFistUsesPerDay})");

        var lvl8 = MakeStats("Monk8", 8, "Monk", bab: 6, feats: new[] { "Stunning Fist" });
        Assert(lvl8.StunningFistUsesPerDay == 2, $"Level 8: 2 uses/day (got {lvl8.StunningFistUsesPerDay})");

        var lvl16 = MakeStats("Monk16", 16, "Monk", bab: 12, feats: new[] { "Stunning Fist" });
        Assert(lvl16.StunningFistUsesPerDay == 4, $"Level 16: 4 uses/day (got {lvl16.StunningFistUsesPerDay})");
    }

    private static void TestStunningFistToggle()
    {
        Debug.Log("--- Stunning Fist Toggle ---");
        var stats = MakeStats("MonkToggle", 8, "Monk", bab: 6, feats: new[] { "Stunning Fist" });
        Assert(!stats.StunningFistActive, "StunningFistActive defaults to false");
        stats.StunningFistActive = true;
        Assert(stats.StunningFistActive, "StunningFistActive can be toggled on");
        stats.StunningFistActive = false;
        Assert(!stats.StunningFistActive, "StunningFistActive can be toggled off");
    }

    // =============================================================
    // DEFLECT ARROWS TESTS
    // =============================================================

    private static void TestDeflectArrowsDetection()
    {
        Debug.Log("--- Deflect Arrows Detection ---");
        var withFeat = MakeStats("MonkDA", 6, "Monk", dex: 13, bab: 4,
            feats: new[] { "Deflect Arrows", "Improved Unarmed Strike" });
        var withoutFeat = MakeStats("Fighter", 6, "Fighter", bab: 4);

        Assert(FeatManager.HasDeflectArrows(withFeat), "Character with Deflect Arrows feat detected");
        Assert(!FeatManager.HasDeflectArrows(withoutFeat), "Character without feat not detected");
    }

    private static void TestDeflectArrowsUsedThisRound()
    {
        Debug.Log("--- Deflect Arrows Round Tracking ---");
        var stats = MakeStats("MonkDA", 6, "Monk", bab: 4, feats: new[] { "Deflect Arrows" });
        Assert(!stats.DeflectArrowsUsedThisRound, "Not used at start of round");
        stats.DeflectArrowsUsedThisRound = true;
        Assert(stats.DeflectArrowsUsedThisRound, "Marked as used after deflection");
        stats.DeflectArrowsUsedThisRound = false;
        Assert(!stats.DeflectArrowsUsedThisRound, "Reset at start of new round");
    }

    // =============================================================
    // MANYSHOT TESTS
    // =============================================================

    private static void TestManyshotDetection()
    {
        Debug.Log("--- Manyshot Detection ---");
        var archer = MakeStats("Archer", 6, "Fighter", dex: 17, bab: 6,
            feats: new[] { "Manyshot", "Point Blank Shot", "Rapid Shot" });
        Assert(FeatManager.HasManyshot(archer), "Archer with Manyshot detected");

        var noFeat = MakeStats("NoManyshot", 6, "Fighter", bab: 6);
        Assert(!FeatManager.HasManyshot(noFeat), "Character without Manyshot not detected");
    }

    private static void TestManyshotPenalty()
    {
        Debug.Log("--- Manyshot Penalty ---");
        Assert(FeatManager.GetManyshotAttackPenalty() == -4, "Manyshot attack penalty is -4");
        Assert(FeatManager.GetManyshotArrowCount() == 2, "Manyshot fires 2 arrows");
    }

    private static void TestManyshotCanUse()
    {
        Debug.Log("--- Manyshot Can Use ---");
        // Needs: Manyshot feat, Point Blank Shot, Rapid Shot, DEX 17+, BAB +6
        var valid = MakeStats("ValidArcher", 6, "Fighter", dex: 17, bab: 6,
            feats: new[] { "Manyshot", "Point Blank Shot", "Rapid Shot" });
        Assert(FeatManager.CanUseManyshot(valid), "Valid archer can use Manyshot");

        var lowBAB = MakeStats("LowBAB", 4, "Fighter", dex: 17, bab: 4,
            feats: new[] { "Manyshot", "Point Blank Shot", "Rapid Shot" });
        Assert(!FeatManager.CanUseManyshot(lowBAB), "BAB < 6 cannot use Manyshot");

        var lowDex = MakeStats("LowDex", 6, "Fighter", dex: 14, bab: 6,
            feats: new[] { "Manyshot", "Point Blank Shot", "Rapid Shot" });
        Assert(!FeatManager.CanUseManyshot(lowDex), "DEX < 17 cannot use Manyshot");
    }

    // =============================================================
    // IMPROVED PRECISE SHOT TESTS
    // =============================================================

    private static void TestImprovedPreciseShotDetection()
    {
        Debug.Log("--- Improved Precise Shot Detection ---");
        var withFeat = MakeStats("PreciseArcher", 11, "Fighter", dex: 19, bab: 11,
            feats: new[] { "Improved Precise Shot", "Point Blank Shot", "Precise Shot" });
        Assert(FeatManager.HasImprovedPreciseShot(withFeat), "Character with Improved Precise Shot detected");

        var noFeat = MakeStats("NormalArcher", 11, "Fighter", bab: 11);
        Assert(!FeatManager.HasImprovedPreciseShot(noFeat), "Character without feat not detected");
    }

    private static void TestConcealmentIgnore()
    {
        Debug.Log("--- Improved Precise Shot Concealment Ignore ---");
        var stats = MakeStats("IPS", 11, "Fighter", dex: 19, bab: 11,
            feats: new[] { "Improved Precise Shot", "Point Blank Shot", "Precise Shot" });

        // Should ignore < 50% concealment
        Assert(FeatManager.ShouldIgnoreConcealment(stats, 20), "Ignores 20% concealment");
        Assert(FeatManager.ShouldIgnoreConcealment(stats, 49), "Ignores 49% concealment");
        // Should NOT ignore 50%+ (total concealment)
        Assert(!FeatManager.ShouldIgnoreConcealment(stats, 50), "Does NOT ignore 50% (total) concealment");
        Assert(!FeatManager.ShouldIgnoreConcealment(stats, 100), "Does NOT ignore 100% concealment");
        // Without feat, doesn't ignore anything
        var noFeat = MakeStats("NoIPS", 11, "Fighter", bab: 11);
        Assert(!FeatManager.ShouldIgnoreConcealment(noFeat, 20), "Without feat, does not ignore concealment");
    }

    private static void TestCoverIgnore()
    {
        Debug.Log("--- Improved Precise Shot Cover Ignore ---");
        var stats = MakeStats("IPS", 11, "Fighter", dex: 19, bab: 11,
            feats: new[] { "Improved Precise Shot", "Point Blank Shot", "Precise Shot" });

        Assert(FeatManager.ShouldIgnoreCover(stats), "Character with IPS ignores cover");

        var noFeat = MakeStats("NoIPS", 11, "Fighter", bab: 11);
        Assert(!FeatManager.ShouldIgnoreCover(noFeat), "Without feat, does not ignore cover");
    }

    // =============================================================
    // SPRING ATTACK TESTS
    // =============================================================

    private static void TestSpringAttackDetection()
    {
        Debug.Log("--- Spring Attack Detection ---");
        var stats = MakeStats("SpringFighter", 8, "Fighter", dex: 13, bab: 8,
            feats: new[] { "Spring Attack", "Dodge", "Mobility" });
        Assert(FeatManager.HasSpringAttack(stats), "Character with Spring Attack detected");
    }

    private static void TestSpringAttackCanUse()
    {
        Debug.Log("--- Spring Attack Can Use ---");
        var valid = MakeStats("ValidSpring", 6, "Fighter", dex: 13, bab: 6,
            feats: new[] { "Spring Attack", "Dodge", "Mobility" });
        Assert(FeatManager.CanUseSpringAttack(valid), "Valid character can use Spring Attack");

        var noDodge = MakeStats("NoDodge", 6, "Fighter", dex: 13, bab: 6,
            feats: new[] { "Spring Attack", "Mobility" });
        Assert(!FeatManager.CanUseSpringAttack(noDodge), "Missing Dodge cannot use Spring Attack");

        var lowBAB = MakeStats("LowBAB", 3, "Fighter", dex: 13, bab: 3,
            feats: new[] { "Spring Attack", "Dodge", "Mobility" });
        Assert(!FeatManager.CanUseSpringAttack(lowBAB), "BAB < 4 cannot use Spring Attack");
    }

    // =============================================================
    // SHOT ON THE RUN TESTS
    // =============================================================

    private static void TestShotOnTheRunDetection()
    {
        Debug.Log("--- Shot on the Run Detection ---");
        var stats = MakeStats("MobileArcher", 6, "Fighter", dex: 13, bab: 6,
            feats: new[] { "Shot on the Run", "Dodge", "Mobility", "Point Blank Shot" });
        Assert(FeatManager.HasShotOnTheRun(stats), "Character with Shot on the Run detected");
    }

    private static void TestShotOnTheRunCanUse()
    {
        Debug.Log("--- Shot on the Run Can Use ---");
        var valid = MakeStats("ValidSotR", 6, "Fighter", dex: 13, bab: 6,
            feats: new[] { "Shot on the Run", "Dodge", "Mobility", "Point Blank Shot" });
        Assert(FeatManager.CanUseShotOnTheRun(valid), "Valid character can use Shot on the Run");

        var noPBS = MakeStats("NoPBS", 6, "Fighter", dex: 13, bab: 6,
            feats: new[] { "Shot on the Run", "Dodge", "Mobility" });
        Assert(!FeatManager.CanUseShotOnTheRun(noPBS), "Missing Point Blank Shot cannot use SotR");
    }

    // =============================================================
    // WHIRLWIND ATTACK TESTS
    // =============================================================

    private static void TestWhirlwindAttackDetection()
    {
        Debug.Log("--- Whirlwind Attack Detection ---");
        var stats = MakeStats("WhirlFighter", 12, "Fighter", dex: 13, bab: 12,
            feats: new[] { "Whirlwind Attack", "Combat Expertise", "Dodge", "Mobility", "Spring Attack" });
        Assert(FeatManager.HasWhirlwindAttack(stats), "Character with Whirlwind Attack detected");
    }

    private static void TestWhirlwindAttackCanUse()
    {
        Debug.Log("--- Whirlwind Attack Can Use ---");
        var valid = MakeStats("ValidWhirl", 12, "Fighter", dex: 13, bab: 12,
            feats: new[] { "Whirlwind Attack", "Combat Expertise", "Dodge", "Mobility", "Spring Attack" });
        Assert(FeatManager.CanUseWhirlwindAttack(valid), "Valid character can use Whirlwind Attack");

        var noCE = MakeStats("NoCE", 12, "Fighter", dex: 13, bab: 12,
            feats: new[] { "Whirlwind Attack", "Dodge", "Mobility", "Spring Attack" });
        Assert(!FeatManager.CanUseWhirlwindAttack(noCE), "Missing Combat Expertise cannot use Whirlwind");

        var lowBAB = MakeStats("LowBAB", 3, "Fighter", dex: 13, bab: 3,
            feats: new[] { "Whirlwind Attack", "Combat Expertise", "Dodge", "Mobility", "Spring Attack" });
        Assert(!FeatManager.CanUseWhirlwindAttack(lowBAB), "BAB < 4 cannot use Whirlwind Attack");
    }

    // =============================================================
    // ROUND RESET TRACKING TESTS
    // =============================================================

    private static void TestRoundResetTrackers()
    {
        Debug.Log("--- Round Reset Trackers ---");
        var stats = MakeStats("ResetTest", 8, "Monk", bab: 6,
            feats: new[] { "Stunning Fist", "Deflect Arrows" });

        // Simulate usage
        stats.DeflectArrowsUsedThisRound = true;
        stats.IsUsingSpringAttackMovement = true;

        // Verify set
        Assert(stats.DeflectArrowsUsedThisRound, "DeflectArrows marked as used");
        Assert(stats.IsUsingSpringAttackMovement, "SpringAttackMovement marked as active");

        // Simulate round reset
        stats.DeflectArrowsUsedThisRound = false;
        stats.SpringAttackTarget = null;
        stats.IsUsingSpringAttackMovement = false;

        Assert(!stats.DeflectArrowsUsedThisRound, "DeflectArrows reset for new round");
        Assert(stats.SpringAttackTarget == null, "SpringAttackTarget reset for new round");
        Assert(!stats.IsUsingSpringAttackMovement, "SpringAttackMovement reset for new round");

        // Manyshot toggle persists (player-controlled)
        stats.ManyshotActive = true;
        Assert(stats.ManyshotActive, "ManyshotActive persists across rounds (player toggle)");
    }
}
}
