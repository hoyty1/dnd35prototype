using UnityEngine;
using System.Collections.Generic;

namespace Tests.Feats
{
/// <summary>
/// Phase 2 Specialized Tactics Feat Tests — validates runtime mechanics for 11 feats:
///   1. Far Shot              7. Improved Grapple
///   2. Rapid Reload          8. Augment Summoning
///   3. Snatch Arrows         9. Natural Spell
///   4. Improved Bull Rush   10. Extra Turning
///   5. Improved Overrun     11. Improved Turning
///   6. Improved Sunder
///
/// Tests cover: feat detection, range multipliers, summoning bonuses,
/// turning uses/level, wild shape casting, and combat maneuver flags.
/// </summary>
public static class Phase2SpecializedTacticsTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("========== PHASE 2 SPECIALIZED TACTICS FEAT TESTS ==========");

        // Far Shot
        TestFarShotDetection();
        TestFarShotRangeMultiplier();

        // Rapid Reload
        TestRapidReloadDetection();

        // Snatch Arrows
        TestSnatchArrowsDetection();
        TestSnatchArrowsPrerequisites();

        // Improved Combat Maneuvers
        TestImprovedBullRushDetection();
        TestImprovedOverrunDetection();
        TestImprovedSunderDetection();
        TestImprovedGrappleDetection();

        // Augment Summoning
        TestAugmentSummoningDetection();
        TestAugmentSummoningStatBonuses();

        // Natural Spell
        TestNaturalSpellDetection();
        TestNaturalSpellWildShapeCasting();

        // Extra Turning
        TestExtraTurningDetection();
        TestExtraTurningUsesIncrease();

        // Improved Turning
        TestImprovedTurningDetection();
        TestImprovedTurningLevelBonus();

        // GetFeatSummary integration
        TestPhase2FeatSummaryEntries();

        Debug.Log($"========== PHASE 2 FEATS RESULTS: {_passed} passed, {_failed} failed ==========");
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
        int str = 14, int dex = 14, int con = 14, int wis = 14, int cha = 10, int bab = 4,
        params string[] feats)
    {
        var stats = new CharacterStats(name, level, className,
            str, dex, con, wis, 10, cha,
            bab, 4, 0,
            8, 1, 0,
            6, 1, level * 8);

        if (feats != null && feats.Length > 0)
            stats.AddFeats(new List<string>(feats));

        return stats;
    }

    // =============================================================
    // FAR SHOT TESTS
    // =============================================================

    private static void TestFarShotDetection()
    {
        Debug.Log("--- Far Shot Detection ---");
        var withFeat = MakeStats("Archer", 6, "Fighter", feats: new[] { "Far Shot", "Point Blank Shot" });
        var withoutFeat = MakeStats("Fighter", 6, "Fighter", feats: new[] { "Power Attack" });

        Assert(FeatManager.HasFarShot(withFeat), "Character with Far Shot feat detected");
        Assert(!FeatManager.HasFarShot(withoutFeat), "Character without Far Shot not detected");
        Assert(!FeatManager.HasFarShot(null), "Null stats returns false");
    }

    private static void TestFarShotRangeMultiplier()
    {
        Debug.Log("--- Far Shot Range Multiplier ---");
        var stats = MakeStats("Archer", 6, "Fighter", feats: new[] { "Far Shot", "Point Blank Shot" });
        var noFeat = MakeStats("Fighter", 6, "Fighter");

        // Projectile weapon: ×1.5
        float projMultiplier = FeatManager.GetFarShotRangeMultiplier(stats, false);
        Assert(Mathf.Approximately(projMultiplier, 1.5f), "Far Shot projectile multiplier is 1.5",
            $"Actual: {projMultiplier}");

        // Thrown weapon: ×2.0
        float thrownMultiplier = FeatManager.GetFarShotRangeMultiplier(stats, true);
        Assert(Mathf.Approximately(thrownMultiplier, 2.0f), "Far Shot thrown multiplier is 2.0",
            $"Actual: {thrownMultiplier}");

        // Without feat: ×1.0
        float noFeatMultiplier = FeatManager.GetFarShotRangeMultiplier(noFeat, false);
        Assert(Mathf.Approximately(noFeatMultiplier, 1.0f), "No Far Shot returns 1.0 multiplier",
            $"Actual: {noFeatMultiplier}");
    }

    // =============================================================
    // RAPID RELOAD TESTS
    // =============================================================

    private static void TestRapidReloadDetection()
    {
        Debug.Log("--- Rapid Reload Detection ---");
        var withFeat = MakeStats("Crossbower", 4, "Fighter", feats: new[] { "Rapid Reload (Light Crossbow)" });
        var withHeavy = MakeStats("HeavyCross", 4, "Fighter", feats: new[] { "Rapid Reload (Heavy Crossbow)" });
        var withoutFeat = MakeStats("Fighter", 4, "Fighter");

        Assert(FeatManager.HasAnyRapidReload(withFeat), "Character with Rapid Reload (Light) detected");
        Assert(FeatManager.HasAnyRapidReload(withHeavy), "Character with Rapid Reload (Heavy) detected");
        Assert(!FeatManager.HasAnyRapidReload(withoutFeat), "Character without Rapid Reload not detected");
    }

    // =============================================================
    // SNATCH ARROWS TESTS
    // =============================================================

    private static void TestSnatchArrowsDetection()
    {
        Debug.Log("--- Snatch Arrows Detection ---");
        var withFeat = MakeStats("Monk", 8, "Monk", dex: 15,
            feats: new[] { "Snatch Arrows", "Deflect Arrows", "Improved Unarmed Strike" });
        var withoutFeat = MakeStats("Fighter", 8, "Fighter");

        Assert(FeatManager.HasSnatchArrows(withFeat), "Character with Snatch Arrows detected");
        Assert(!FeatManager.HasSnatchArrows(withoutFeat), "Character without Snatch Arrows not detected");
    }

    private static void TestSnatchArrowsPrerequisites()
    {
        Debug.Log("--- Snatch Arrows Prerequisites ---");
        // Snatch Arrows requires DEX 15, Deflect Arrows, Improved Unarmed Strike
        var fullyQualified = MakeStats("QualMonk", 8, "Monk", dex: 15,
            feats: new[] { "Snatch Arrows", "Deflect Arrows", "Improved Unarmed Strike" });
        Assert(FeatManager.CanUseSnatchArrows(fullyQualified), "Fully qualified Snatch Arrows user");

        // Missing Deflect Arrows prerequisite
        var missingDeflect = MakeStats("BadMonk", 8, "Monk", dex: 15,
            feats: new[] { "Snatch Arrows", "Improved Unarmed Strike" });
        Assert(!FeatManager.CanUseSnatchArrows(missingDeflect), "Cannot use Snatch Arrows without Deflect Arrows");

        // Low DEX
        var lowDex = MakeStats("LowDex", 8, "Monk", dex: 12,
            feats: new[] { "Snatch Arrows", "Deflect Arrows", "Improved Unarmed Strike" });
        Assert(!FeatManager.CanUseSnatchArrows(lowDex), "Cannot use Snatch Arrows with DEX < 15");
    }

    // =============================================================
    // IMPROVED COMBAT MANEUVER TESTS
    // =============================================================

    private static void TestImprovedBullRushDetection()
    {
        Debug.Log("--- Improved Bull Rush Detection ---");
        var withFeat = MakeStats("BullRusher", 6, "Fighter",
            feats: new[] { "Improved Bull Rush", "Power Attack" });
        var withoutFeat = MakeStats("Fighter", 6, "Fighter");

        Assert(FeatManager.HasImprovedBullRush(withFeat), "Character with Improved Bull Rush detected");
        Assert(!FeatManager.HasImprovedBullRush(withoutFeat), "Character without Improved Bull Rush not detected");
    }

    private static void TestImprovedOverrunDetection()
    {
        Debug.Log("--- Improved Overrun Detection ---");
        var withFeat = MakeStats("Overrunner", 6, "Fighter",
            feats: new[] { "Improved Overrun", "Power Attack" });
        var withoutFeat = MakeStats("Fighter", 6, "Fighter");

        Assert(FeatManager.HasImprovedOverrun(withFeat), "Character with Improved Overrun detected");
        Assert(!FeatManager.HasImprovedOverrun(withoutFeat), "Character without Improved Overrun not detected");
    }

    private static void TestImprovedSunderDetection()
    {
        Debug.Log("--- Improved Sunder Detection ---");
        var withFeat = MakeStats("Sunderer", 6, "Fighter",
            feats: new[] { "Improved Sunder", "Power Attack" });
        var withoutFeat = MakeStats("Fighter", 6, "Fighter");

        Assert(FeatManager.HasImprovedSunder(withFeat), "Character with Improved Sunder detected");
        Assert(!FeatManager.HasImprovedSunder(withoutFeat), "Character without Improved Sunder not detected");
    }

    private static void TestImprovedGrappleDetection()
    {
        Debug.Log("--- Improved Grapple Detection ---");
        var withFeat = MakeStats("Grappler", 6, "Fighter",
            feats: new[] { "Improved Grapple", "Improved Unarmed Strike" });
        var withoutFeat = MakeStats("Fighter", 6, "Fighter");

        Assert(FeatManager.HasImprovedGrapple(withFeat), "Character with Improved Grapple detected");
        Assert(!FeatManager.HasImprovedGrapple(withoutFeat), "Character without Improved Grapple not detected");
    }

    // =============================================================
    // AUGMENT SUMMONING TESTS
    // =============================================================

    private static void TestAugmentSummoningDetection()
    {
        Debug.Log("--- Augment Summoning Detection ---");
        var withFeat = MakeStats("Conjurer", 8, "Wizard",
            feats: new[] { "Augment Summoning", "Spell Focus" });
        var withoutFeat = MakeStats("Wizard", 8, "Wizard");

        Assert(FeatManager.HasAugmentSummoning(withFeat), "Character with Augment Summoning detected");
        Assert(!FeatManager.HasAugmentSummoning(withoutFeat), "Character without Augment Summoning not detected");
    }

    private static void TestAugmentSummoningStatBonuses()
    {
        Debug.Log("--- Augment Summoning Stat Bonuses ---");
        // Create a "summoned creature" with known base stats
        var summon = MakeStats("Celestial Eagle", 3, "Monster", str: 10, con: 12);
        summon.HitDice = 3;
        int originalSTR = summon.BaseSTR;
        int originalCON = summon.BaseCON;
        int originalMaxHP = summon.MaxHP;

        FeatManager.ApplyAugmentSummoningBonuses(summon);

        Assert(summon.BaseSTR == originalSTR + 4, "Augment Summoning adds +4 STR",
            $"Expected {originalSTR + 4}, got {summon.BaseSTR}");
        Assert(summon.BaseCON == originalCON + 4, "Augment Summoning adds +4 CON",
            $"Expected {originalCON + 4}, got {summon.BaseCON}");

        // HP bonus: +2 per hit die (from +4 CON = +2 mod increase)
        int expectedHPBonus = 2 * 3; // 2 * HitDice
        Assert(summon.MaxHP == originalMaxHP + expectedHPBonus, "Augment Summoning adds correct HP bonus",
            $"Expected {originalMaxHP + expectedHPBonus}, got {summon.MaxHP}");
    }

    // =============================================================
    // NATURAL SPELL TESTS
    // =============================================================

    private static void TestNaturalSpellDetection()
    {
        Debug.Log("--- Natural Spell Detection ---");
        var withFeat = MakeStats("Druid", 8, "Druid", wis: 16,
            feats: new[] { "Natural Spell" });
        var withoutFeat = MakeStats("Druid", 8, "Druid", wis: 16);

        Assert(FeatManager.HasNaturalSpell(withFeat), "Druid with Natural Spell feat detected");
        Assert(!FeatManager.HasNaturalSpell(withoutFeat), "Druid without Natural Spell not detected");
    }

    private static void TestNaturalSpellWildShapeCasting()
    {
        Debug.Log("--- Natural Spell Wild Shape Casting ---");
        // Test via WildShapeData directly (mirrors Phase3ClassTests)
        var ws = new WildShapeData();
        ws.Initialize(8, false);
        Assert(!ws.CanCastInWildShape, "Cannot cast without Natural Spell feat set");

        ws.SetNaturalSpellFeat(true);
        Assert(ws.CanCastInWildShape, "Can cast with Natural Spell feat set on WildShapeData");

        ws.SetNaturalSpellFeat(false);
        Assert(!ws.CanCastInWildShape, "Cannot cast after removing Natural Spell");

        // Test FeatManager.CanCastInWildShape (requires HasWildShape)
        // Since HasWildShape depends on DruidClass being L5+, test the feat query itself
        var druidWithFeat = MakeStats("WildDruid", 8, "Druid", wis: 16,
            feats: new[] { "Natural Spell" });
        Assert(FeatManager.HasNaturalSpell(druidWithFeat), "FeatManager detects Natural Spell on druid");
    }

    // =============================================================
    // EXTRA TURNING TESTS
    // =============================================================

    private static void TestExtraTurningDetection()
    {
        Debug.Log("--- Extra Turning Detection ---");
        var withFeat = MakeStats("Cleric", 8, "Cleric", cha: 14,
            feats: new[] { "Extra Turning" });
        var withoutFeat = MakeStats("Cleric", 8, "Cleric", cha: 14);

        Assert(FeatManager.HasExtraTurning(withFeat), "Cleric with Extra Turning detected");
        Assert(!FeatManager.HasExtraTurning(withoutFeat), "Cleric without Extra Turning not detected");
    }

    private static void TestExtraTurningUsesIncrease()
    {
        Debug.Log("--- Extra Turning Uses Increase ---");
        // CHA 14 = +2 modifier. Base: 3 + 2 = 5 uses/day
        var clericNoFeat = MakeStats("ClericBase", 8, "Cleric", cha: 14);
        int baseUses = clericNoFeat.MaxTurnUndeadAttemptsPerDay;
        Assert(baseUses == 5, "Base cleric CHA 14 has 5 turning uses/day",
            $"Actual: {baseUses}");

        // With Extra Turning: 5 + 4 = 9
        var clericWithFeat = MakeStats("ClericExtra", 8, "Cleric", cha: 14,
            feats: new[] { "Extra Turning" });
        int extraUses = clericWithFeat.MaxTurnUndeadAttemptsPerDay;
        Assert(extraUses == baseUses + 4, "Extra Turning adds +4 uses/day",
            $"Expected {baseUses + 4}, got {extraUses}");

        // FeatManager.GetExtraTurningUses returns 4
        Assert(FeatManager.GetExtraTurningUses(clericWithFeat) == 4,
            "GetExtraTurningUses returns 4");

        // Non-cleric should still get 0
        var fighter = MakeStats("Fighter", 8, "Fighter", feats: new[] { "Extra Turning" });
        Assert(fighter.MaxTurnUndeadAttemptsPerDay == 0,
            "Fighter with Extra Turning still has 0 uses (no base turning)");
    }

    // =============================================================
    // IMPROVED TURNING TESTS
    // =============================================================

    private static void TestImprovedTurningDetection()
    {
        Debug.Log("--- Improved Turning Detection ---");
        var withFeat = MakeStats("Cleric", 8, "Cleric", cha: 14,
            feats: new[] { "Improved Turning" });
        var withoutFeat = MakeStats("Cleric", 8, "Cleric", cha: 14);

        Assert(FeatManager.HasImprovedTurning(withFeat), "Cleric with Improved Turning detected");
        Assert(!FeatManager.HasImprovedTurning(withoutFeat), "Cleric without Improved Turning not detected");
    }

    private static void TestImprovedTurningLevelBonus()
    {
        Debug.Log("--- Improved Turning Level Bonus ---");
        var clericWithFeat = MakeStats("ClericImp", 8, "Cleric",
            feats: new[] { "Improved Turning" });
        var clericNoFeat = MakeStats("ClericBase", 8, "Cleric");

        int bonus = FeatManager.GetImprovedTurningLevelBonus(clericWithFeat);
        Assert(bonus == 1, "Improved Turning grants +1 effective level", $"Actual: {bonus}");

        int noBonus = FeatManager.GetImprovedTurningLevelBonus(clericNoFeat);
        Assert(noBonus == 0, "No Improved Turning means 0 bonus", $"Actual: {noBonus}");

        // Null safety
        Assert(FeatManager.GetImprovedTurningLevelBonus(null) == 0,
            "Null stats returns 0 bonus");
    }

    // =============================================================
    // FEAT SUMMARY TESTS
    // =============================================================

    private static void TestPhase2FeatSummaryEntries()
    {
        Debug.Log("--- Phase 2 Feat Summary Entries ---");

        // Far Shot
        var archer = MakeStats("Archer", 6, "Fighter", feats: new[] { "Far Shot" });
        string archerSummary = FeatManager.GetFeatSummary(archer);
        Assert(archerSummary.Contains("Far Shot"), "Feat summary includes Far Shot",
            $"Summary: {archerSummary}");

        // Augment Summoning
        var conjurer = MakeStats("Conjurer", 8, "Wizard", feats: new[] { "Augment Summoning" });
        string conjSummary = FeatManager.GetFeatSummary(conjurer);
        Assert(conjSummary.Contains("Augment Summoning"), "Feat summary includes Augment Summoning");

        // Extra Turning + Improved Turning
        var cleric = MakeStats("Cleric", 8, "Cleric",
            feats: new[] { "Extra Turning", "Improved Turning" });
        string clericSummary = FeatManager.GetFeatSummary(cleric);
        Assert(clericSummary.Contains("Extra Turning"), "Feat summary includes Extra Turning");
        Assert(clericSummary.Contains("Improved Turning"), "Feat summary includes Improved Turning");

        // Natural Spell
        var druid = MakeStats("Druid", 8, "Druid", feats: new[] { "Natural Spell" });
        string druidSummary = FeatManager.GetFeatSummary(druid);
        Assert(druidSummary.Contains("Natural Spell"), "Feat summary includes Natural Spell");

        // Improved Bull Rush
        var fighter = MakeStats("Fighter", 6, "Fighter",
            feats: new[] { "Improved Bull Rush", "Improved Overrun", "Improved Sunder", "Improved Grapple" });
        string fighterSummary = FeatManager.GetFeatSummary(fighter);
        Assert(fighterSummary.Contains("Improved Bull Rush"), "Feat summary includes Improved Bull Rush");
        Assert(fighterSummary.Contains("Improved Overrun"), "Feat summary includes Improved Overrun");
        Assert(fighterSummary.Contains("Improved Sunder"), "Feat summary includes Improved Sunder");
        Assert(fighterSummary.Contains("Improved Grapple"), "Feat summary includes Improved Grapple");
    }
}
}
