using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ════════════════════════════════════════════════════════════════════════
//  Rod System Tests (D&D 3.5e DMG pp. 224–228)
//
//  Comprehensive test suite for all 36 rods covering:
//  - Database registration and counts
//  - Metamagic rod spell level validation
//  - 3/day usage limits and daily resets
//  - Rod of Absorption spell level tracking
//  - Rod of Cancellation single-use
//  - Rod of Lordly Might weapon transformations
//  - Rod of Python transformation
//  - Immovable Rod toggle
//  - DMG pricing accuracy
//  - Tooltip generation
//  - CloneItem correctness
// ════════════════════════════════════════════════════════════════════════

public static class RodTests
{
    private static int _passed;
    private static int _failed;
    private static List<string> _failures = new List<string>();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        _failures.Clear();

        Debug.Log("═══════════════════════════════════════════════");
        Debug.Log("  ROD SYSTEM TESTS — D&D 3.5e DMG pp. 224–228");
        Debug.Log("═══════════════════════════════════════════════");

        // Ensure database is initialized
        RodDatabase.Init();

        // Database tests
        Test_DatabaseRegistration();
        Test_MetamagicRodCount();
        Test_NonMetamagicRodCount();
        Test_LegendaryRodCount();
        Test_CategoryCounts();

        // Pricing tests
        Test_MetamagicRodPricing();
        Test_CombatRodPricing();
        Test_UtilityRodPricing();

        // Metamagic rod validation
        Test_MetamagicRodSpellLevelValidation();
        Test_MetamagicRodDailyUseLimits();
        Test_MetamagicRodDailyReset();
        Test_MetamagicRodAppliedByRodFlag();
        Test_MetamagicRodNoSlotIncrease();
        Test_MetamagicRodDuplicatePrevention();

        // Rod of Absorption
        Test_AbsorptionSpellStorage();
        Test_AbsorptionCapacity();
        Test_AbsorptionSpendLevels();

        // Rod of Cancellation
        Test_CancellationSingleUse();
        Test_CancellationExpendedState();

        // Immovable Rod
        Test_ImmovableRodToggle();

        // Rod of Lordly Might
        Test_LordlyMightWeaponModes();
        Test_LordlyMightFearUses();

        // Rod of Python
        Test_PythonTransformation();

        // Rod of Security
        Test_SecurityDemiplaneWeeklyLimit();

        // Rod of Alertness
        Test_AlertnessAbilities();

        // Rod of Negation
        Test_NegationGreaterDispel();

        // Rod of Splendor
        Test_SplendorAbilities();

        // Rod of Flailing
        Test_FlailingStats();

        // Tooltip tests
        Test_MetamagicRodTooltip();
        Test_CombatRodTooltip();

        // Clone tests
        Test_CloneResetsDailyUses();
        Test_ClonePreservesStaticFields();

        // Summary
        Debug.Log("═══════════════════════════════════════════════");
        Debug.Log($"  ROD TESTS: {_passed} PASSED, {_failed} FAILED");
        Debug.Log("═══════════════════════════════════════════════");

        if (_failures.Count > 0)
        {
            Debug.LogWarning("FAILURES:");
            foreach (string f in _failures)
                Debug.LogWarning($"  ✗ {f}");
        }
    }

    // ── Assertion Helpers ─────────────────────────────────────

    private static void Assert(bool condition, string testName)
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  ✓ {testName}");
        }
        else
        {
            _failed++;
            _failures.Add(testName);
            Debug.LogError($"  ✗ FAIL: {testName}");
        }
    }

    private static void AssertEqual(int expected, int actual, string testName)
    {
        Assert(expected == actual, $"{testName} (expected {expected}, got {actual})");
    }

    private static void AssertEqual(float expected, float actual, string testName)
    {
        Assert(Mathf.Approximately(expected, actual), $"{testName} (expected {expected}, got {actual})");
    }

    private static void AssertEqual(string expected, string actual, string testName)
    {
        Assert(expected == actual, $"{testName} (expected '{expected}', got '{actual}')");
    }

    // ════════════════════════════════════════════════════════════
    //  DATABASE TESTS
    // ════════════════════════════════════════════════════════════

    private static void Test_DatabaseRegistration()
    {
        Debug.Log("\n── Database Registration ──");
        // 21 metamagic + 6 combat + 3 utility + 3 legendary = 33
        AssertEqual(33, RodDatabase.Count, "Total rods registered = 33");

        // Verify all rod IDs can be found
        string[] allIds = RodNames.GetAllRodIds();
        int found = 0;
        foreach (string id in allIds)
        {
            if (RodDatabase.HasRod(id)) found++;
        }
        AssertEqual(allIds.Length, found, $"All {allIds.Length} rod IDs findable in database");
    }

    private static void Test_MetamagicRodCount()
    {
        int count = RodDatabase.GetMetamagicRods().Count();
        AssertEqual(21, count, "Metamagic rods = 21 (7 types × 3 powers)");
    }

    private static void Test_NonMetamagicRodCount()
    {
        int count = RodDatabase.GetNonMetamagicRods().Count();
        // 6 combat + 3 utility + 3 legendary = 12
        AssertEqual(12, count, "Non-metamagic rods = 12 (6 combat + 3 utility + 3 legendary)");
    }

    private static void Test_LegendaryRodCount()
    {
        int count = RodDatabase.GetLegendaryRods().Count();
        AssertEqual(3, count, "Legendary rods = 3 (Alertness, Lordly Might, Security)");
    }

    private static void Test_CategoryCounts()
    {
        Debug.Log("\n── Category Counts ──");
        int metamagic = RodDatabase.GetRodsByCategory(RodCategory.Metamagic).Count();
        AssertEqual(21, metamagic, "Metamagic category = 21");

        int combat = RodDatabase.GetRodsByCategory(RodCategory.Combat).Count();
        AssertEqual(6, combat, "Combat category = 6 (Absorption, Cancellation, Flailing, Immovable, Metal Detection, Splendor)");

        int utility = RodDatabase.GetRodsByCategory(RodCategory.Utility).Count();
        AssertEqual(3, utility, "Utility category = 3 (Enemy Detection, Negation, Python)");

        int legendary = RodDatabase.GetRodsByCategory(RodCategory.Legendary).Count();
        AssertEqual(3, legendary, "Legendary category = 3 (Alertness, Lordly Might, Security)");
    }

    // ════════════════════════════════════════════════════════════
    //  PRICING TESTS (DMG accuracy)
    // ════════════════════════════════════════════════════════════

    private static void Test_MetamagicRodPricing()
    {
        Debug.Log("\n── Metamagic Rod Pricing ──");

        // +1 slot metamagics: Enlarge, Extend, Silent
        VerifyPrice(RodNames.ROD_ENLARGE_LESSER, 3000, "Enlarge Lesser");
        VerifyPrice(RodNames.ROD_ENLARGE_NORMAL, 11000, "Enlarge Normal");
        VerifyPrice(RodNames.ROD_ENLARGE_GREATER, 24500, "Enlarge Greater");

        VerifyPrice(RodNames.ROD_EXTEND_LESSER, 3000, "Extend Lesser");
        VerifyPrice(RodNames.ROD_EXTEND_NORMAL, 11000, "Extend Normal");
        VerifyPrice(RodNames.ROD_EXTEND_GREATER, 24500, "Extend Greater");

        VerifyPrice(RodNames.ROD_SILENT_LESSER, 3000, "Silent Lesser");
        VerifyPrice(RodNames.ROD_SILENT_NORMAL, 11000, "Silent Normal");
        VerifyPrice(RodNames.ROD_SILENT_GREATER, 24500, "Silent Greater");

        // +2 slot: Empower
        VerifyPrice(RodNames.ROD_EMPOWER_LESSER, 9000, "Empower Lesser");
        VerifyPrice(RodNames.ROD_EMPOWER_NORMAL, 32500, "Empower Normal");
        VerifyPrice(RodNames.ROD_EMPOWER_GREATER, 73000, "Empower Greater");

        // +3 slot: Maximize, Widen
        VerifyPrice(RodNames.ROD_MAXIMIZE_LESSER, 14000, "Maximize Lesser");
        VerifyPrice(RodNames.ROD_MAXIMIZE_NORMAL, 54000, "Maximize Normal");
        VerifyPrice(RodNames.ROD_MAXIMIZE_GREATER, 121500, "Maximize Greater");

        VerifyPrice(RodNames.ROD_WIDEN_LESSER, 14000, "Widen Lesser");
        VerifyPrice(RodNames.ROD_WIDEN_NORMAL, 54000, "Widen Normal");
        VerifyPrice(RodNames.ROD_WIDEN_GREATER, 121500, "Widen Greater");

        // +4 slot: Quicken
        VerifyPrice(RodNames.ROD_QUICKEN_LESSER, 35000, "Quicken Lesser");
        VerifyPrice(RodNames.ROD_QUICKEN_NORMAL, 75500, "Quicken Normal");
        VerifyPrice(RodNames.ROD_QUICKEN_GREATER, 170000, "Quicken Greater");
    }

    private static void Test_CombatRodPricing()
    {
        Debug.Log("\n── Combat Rod Pricing ──");
        VerifyPrice(RodNames.ROD_ABSORPTION, 50000, "Absorption");
        VerifyPrice(RodNames.ROD_CANCELLATION, 11000, "Cancellation");
        VerifyPrice(RodNames.ROD_FLAILING, 50000, "Flailing");
        VerifyPrice(RodNames.ROD_IMMOVABLE, 5000, "Immovable");
        VerifyPrice(RodNames.ROD_LORDLY_MIGHT, 70000, "Lordly Might");
        VerifyPrice(RodNames.ROD_METAL_AND_MINERAL_DETECTION, 10500, "Metal/Mineral Detection");
        VerifyPrice(RodNames.ROD_SPLENDOR, 25000, "Splendor");
    }

    private static void Test_UtilityRodPricing()
    {
        Debug.Log("\n── Utility Rod Pricing ──");
        VerifyPrice(RodNames.ROD_ALERTNESS, 85000, "Alertness");
        VerifyPrice(RodNames.ROD_ENEMY_DETECTION, 23500, "Enemy Detection");
        VerifyPrice(RodNames.ROD_NEGATION, 37000, "Negation");
        VerifyPrice(RodNames.ROD_PYTHON, 13000, "Python");
        VerifyPrice(RodNames.ROD_SECURITY, 61000, "Security");
    }

    private static void VerifyPrice(string rodId, int expectedPrice, string label)
    {
        var rod = RodDatabase.GetRod(rodId);
        Assert(rod != null, $"Rod '{label}' exists");
        if (rod != null)
            AssertEqual(expectedPrice, rod.BasePriceGp, $"Rod '{label}' price = {expectedPrice} gp");
    }

    // ════════════════════════════════════════════════════════════
    //  METAMAGIC ROD VALIDATION TESTS
    // ════════════════════════════════════════════════════════════

    private static void Test_MetamagicRodSpellLevelValidation()
    {
        Debug.Log("\n── Metamagic Rod Spell Level Validation ──");

        var lesserRod = CreateTestMetamagicRod(RodPowerLevel.Lesser, MetamagicFeatId.EmpowerSpell, 3);
        var normalRod = CreateTestMetamagicRod(RodPowerLevel.Normal, MetamagicFeatId.EmpowerSpell, 6);
        var greaterRod = CreateTestMetamagicRod(RodPowerLevel.Greater, MetamagicFeatId.EmpowerSpell, 9);

        // Lesser: max 3rd level
        var spell3 = CreateTestSpell(3); // 3rd level
        var spell4 = CreateTestSpell(4); // 4th level
        var spell9 = CreateTestSpell(9); // 9th level

        Assert(MetamagicRodActivation.ValidateRodApplication(lesserRod, spell3) == null,
            "Lesser rod accepts 3rd-level spell");
        Assert(MetamagicRodActivation.ValidateRodApplication(lesserRod, spell4) != null,
            "Lesser rod rejects 4th-level spell");

        // Normal: max 6th level
        Assert(MetamagicRodActivation.ValidateRodApplication(normalRod, spell4) == null,
            "Normal rod accepts 4th-level spell");
        var spell7 = CreateTestSpell(7);
        Assert(MetamagicRodActivation.ValidateRodApplication(normalRod, spell7) != null,
            "Normal rod rejects 7th-level spell");

        // Greater: max 9th level
        Assert(MetamagicRodActivation.ValidateRodApplication(greaterRod, spell9) == null,
            "Greater rod accepts 9th-level spell");
    }

    private static void Test_MetamagicRodDailyUseLimits()
    {
        Debug.Log("\n── Metamagic Rod Daily Use Limits ──");

        var rod = CreateTestMetamagicRod(RodPowerLevel.Greater, MetamagicFeatId.EmpowerSpell, 9);
        var spell = CreateTestSpell(3);

        // Use 3 times
        for (int i = 0; i < 3; i++)
        {
            var mod = MetamagicRodActivation.ApplyRodToSpell(rod, spell);
            Assert(mod != null, $"Use {i + 1}/3 succeeds");
        }

        // 4th use should fail
        var failMod = MetamagicRodActivation.ApplyRodToSpell(rod, spell);
        Assert(failMod == null, "4th use fails (3/day limit)");
        AssertEqual(3, rod.RodUsesToday, "Rod shows 3/3 uses");
    }

    private static void Test_MetamagicRodDailyReset()
    {
        Debug.Log("\n── Metamagic Rod Daily Reset ──");

        var rod = CreateTestMetamagicRod(RodPowerLevel.Greater, MetamagicFeatId.EmpowerSpell, 9);
        rod.RodUsesToday = 3; // Fully used

        RodDatabase.ResetDailyUses(new List<ItemData> { rod });
        AssertEqual(0, rod.RodUsesToday, "Daily reset clears uses to 0");
    }

    private static void Test_MetamagicRodAppliedByRodFlag()
    {
        Debug.Log("\n── Metamagic Rod AppliedByRod Flag ──");

        var rod = CreateTestMetamagicRod(RodPowerLevel.Greater, MetamagicFeatId.MaximizeSpell, 9);
        var spell = CreateTestSpell(3);
        var mod = MetamagicRodActivation.ApplyRodToSpell(rod, spell);

        Assert(mod != null, "Modifier created");
        if (mod != null)
        {
            Assert(mod.AppliedByRod, "Modifier has AppliedByRod = true");
            Assert(mod.Type == MetamagicFeatId.MaximizeSpell, "Modifier type = MaximizeSpell");
        }
    }

    private static void Test_MetamagicRodNoSlotIncrease()
    {
        Debug.Log("\n── Metamagic Rod No Slot Increase ──");

        var rod = CreateTestMetamagicRod(RodPowerLevel.Greater, MetamagicFeatId.QuickenSpell, 9);
        var spell = CreateTestSpell(5);
        var mod = MetamagicRodActivation.ApplyRodToSpell(rod, spell);

        Assert(mod != null, "Quicken rod modifier created");
        if (mod != null)
        {
            AssertEqual(0, mod.SlotIncrease, "Rod modifier SlotIncrease = 0 (FREE)");
        }
    }

    private static void Test_MetamagicRodDuplicatePrevention()
    {
        Debug.Log("\n── Metamagic Rod Duplicate Prevention ──");

        var rod1 = CreateTestMetamagicRod(RodPowerLevel.Greater, MetamagicFeatId.EmpowerSpell, 9);
        var rod2 = CreateTestMetamagicRod(RodPowerLevel.Greater, MetamagicFeatId.EmpowerSpell, 9);
        var spell = CreateTestSpell(3);

        var mods = MetamagicRodActivation.ApplyMultipleRods(new List<ItemData> { rod1, rod2 }, spell);
        AssertEqual(1, mods.Count, "Duplicate Empower rods: only 1 modifier applied");
    }

    // ════════════════════════════════════════════════════════════
    //  ROD OF ABSORPTION TESTS
    // ════════════════════════════════════════════════════════════

    private static void Test_AbsorptionSpellStorage()
    {
        Debug.Log("\n── Rod of Absorption: Spell Storage ──");

        var rod = ItemDatabase.CloneItem(RodNames.ROD_ABSORPTION);
        Assert(rod != null, "Rod of Absorption cloned");
        if (rod == null) return;

        var spell = CreateTestSpell(5); // 5th level
        bool absorbed = MetamagicRodActivation.TryAbsorbSpell(rod, spell);
        Assert(absorbed, "5th-level spell absorbed");
        AssertEqual(5, rod.RodAbsorbedLevels, "Absorbed levels = 5");
    }

    private static void Test_AbsorptionCapacity()
    {
        Debug.Log("\n── Rod of Absorption: 50-Level Capacity ──");

        var rod = ItemDatabase.CloneItem(RodNames.ROD_ABSORPTION);
        if (rod == null) return;

        rod.RodAbsorbedLevels = 48;
        var spell = CreateTestSpell(5); // Would push to 53
        MetamagicRodActivation.TryAbsorbSpell(rod, spell);
        AssertEqual(50, rod.RodAbsorbedLevels, "Capped at 50 levels");

        // Try to absorb when full
        bool result = MetamagicRodActivation.TryAbsorbSpell(rod, spell);
        Assert(!result, "Cannot absorb when at capacity");
    }

    private static void Test_AbsorptionSpendLevels()
    {
        Debug.Log("\n── Rod of Absorption: Spend Levels ──");

        var rod = ItemDatabase.CloneItem(RodNames.ROD_ABSORPTION);
        if (rod == null) return;

        rod.RodAbsorbedLevels = 20;

        bool spent = MetamagicRodActivation.SpendAbsorbedLevels(rod, 7);
        Assert(spent, "Spent 7 levels");
        AssertEqual(13, rod.RodAbsorbedLevels, "Remaining = 13");

        bool fail = MetamagicRodActivation.SpendAbsorbedLevels(rod, 20);
        Assert(!fail, "Cannot spend 20 (only 13 remaining)");
    }

    // ════════════════════════════════════════════════════════════
    //  ROD OF CANCELLATION TESTS
    // ════════════════════════════════════════════════════════════

    private static void Test_CancellationSingleUse()
    {
        Debug.Log("\n── Rod of Cancellation: Single Use ──");

        var rod = ItemDatabase.CloneItem(RodNames.ROD_CANCELLATION);
        if (rod == null) return;

        Assert(!rod.RodIsExpended, "Rod starts not expended");
        Assert(rod.RodCanCancelMagic, "Rod can cancel magic");

        var target = new ItemData { Name = "Magic Sword", CountsAsMagicForBypass = true, EnhancementBonus = 3 };
        bool result = MetamagicRodActivation.UseRodOfCancellation(rod, target);
        Assert(result, "Cancellation succeeded");
        Assert(rod.RodIsExpended, "Rod is now expended");
        AssertEqual(0, target.EnhancementBonus, "Target lost enhancement");
    }

    private static void Test_CancellationExpendedState()
    {
        Debug.Log("\n── Rod of Cancellation: Expended State ──");

        var rod = ItemDatabase.CloneItem(RodNames.ROD_CANCELLATION);
        if (rod == null) return;

        // Expend the rod
        var target1 = new ItemData { Name = "Item1", CountsAsMagicForBypass = true };
        MetamagicRodActivation.UseRodOfCancellation(rod, target1);

        // Try to use again
        var target2 = new ItemData { Name = "Item2", CountsAsMagicForBypass = true };
        bool result = MetamagicRodActivation.UseRodOfCancellation(rod, target2);
        Assert(!result, "Expended rod cannot be used again");
    }

    // ════════════════════════════════════════════════════════════
    //  IMMOVABLE ROD TESTS
    // ════════════════════════════════════════════════════════════

    private static void Test_ImmovableRodToggle()
    {
        Debug.Log("\n── Immovable Rod: Toggle ──");

        var rod = ItemDatabase.CloneItem(RodNames.ROD_IMMOVABLE);
        if (rod == null) return;

        Assert(!rod.RodIsActivated, "Rod starts deactivated");

        MetamagicRodActivation.ToggleImmovableRod(rod);
        Assert(rod.RodIsActivated, "Rod activated after toggle");
        AssertEqual(8000, rod.RodHoldWeightLbs, "Holds 8,000 lbs");
        AssertEqual(30, rod.RodMoveDC, "DC 30 to move");

        MetamagicRodActivation.ToggleImmovableRod(rod);
        Assert(!rod.RodIsActivated, "Rod deactivated after second toggle");
    }

    // ════════════════════════════════════════════════════════════
    //  ROD OF LORDLY MIGHT TESTS
    // ════════════════════════════════════════════════════════════

    private static void Test_LordlyMightWeaponModes()
    {
        Debug.Log("\n── Rod of Lordly Might: Weapon Modes ──");

        var rod = ItemDatabase.CloneItem(RodNames.ROD_LORDLY_MIGHT);
        if (rod == null) return;

        // Default: +3 heavy mace
        AssertEqual(3, rod.RodWeaponEnhancement, "Default: +3 heavy mace");
        AssertEqual("1d8", rod.RodWeaponDamageDice, "Default: 1d8 damage");

        // Switch to flaming longsword
        MetamagicRodActivation.SwitchLordlyMightMode(rod, LordlyMightWeaponMode.FlamingSword);
        AssertEqual(1, rod.RodWeaponEnhancement, "Flaming: +1");
        AssertEqual("Flaming Longsword", rod.RodWeaponMode, "Mode: Flaming Longsword");

        // Switch to battleaxe
        MetamagicRodActivation.SwitchLordlyMightMode(rod, LordlyMightWeaponMode.Battleaxe);
        AssertEqual(4, rod.RodWeaponEnhancement, "Battleaxe: +4");

        // Switch to shortspear
        MetamagicRodActivation.SwitchLordlyMightMode(rod, LordlyMightWeaponMode.Shortspear);
        AssertEqual(3, rod.RodWeaponEnhancement, "Shortspear: +3");
        AssertEqual("1d6", rod.RodWeaponDamageDice, "Shortspear: 1d6");

        // Switch to climbing pole
        MetamagicRodActivation.SwitchLordlyMightMode(rod, LordlyMightWeaponMode.ClimbingPole);
        AssertEqual(0, rod.RodWeaponEnhancement, "Climbing: no enhancement");
    }

    private static void Test_LordlyMightFearUses()
    {
        Debug.Log("\n── Rod of Lordly Might: Fear Cone ──");

        var rod = ItemDatabase.CloneItem(RodNames.ROD_LORDLY_MIGHT);
        if (rod == null) return;

        Assert(MetamagicRodActivation.UseLordlyMightFear(rod), "Fear use 1/2");
        Assert(MetamagicRodActivation.UseLordlyMightFear(rod), "Fear use 2/2");
        Assert(!MetamagicRodActivation.UseLordlyMightFear(rod), "Fear use 3 fails (2/day limit)");
    }

    // ════════════════════════════════════════════════════════════
    //  ROD OF PYTHON TESTS
    // ════════════════════════════════════════════════════════════

    private static void Test_PythonTransformation()
    {
        Debug.Log("\n── Rod of Python: Transformation ──");

        var rod = ItemDatabase.CloneItem(RodNames.ROD_PYTHON);
        if (rod == null) return;

        Assert(!rod.RodIsInSnakeForm, "Starts in rod form");

        MetamagicRodActivation.ToggleRodOfPython(rod);
        Assert(rod.RodIsInSnakeForm, "Now in snake form");
        AssertEqual(60, rod.RodSnakeHP, "Snake HP = 60");
        AssertEqual(15, rod.RodSnakeAC, "Snake AC = 15");
        AssertEqual(13, rod.RodSnakeAttackBonus, "Snake attack = +13");
        Assert(rod.RodSnakeHasConstrict, "Snake has constrict");

        MetamagicRodActivation.ToggleRodOfPython(rod);
        Assert(!rod.RodIsInSnakeForm, "Back to rod form");
    }

    // ════════════════════════════════════════════════════════════
    //  ROD OF SECURITY TESTS
    // ════════════════════════════════════════════════════════════

    private static void Test_SecurityDemiplaneWeeklyLimit()
    {
        Debug.Log("\n── Rod of Security: Weekly Limit ──");

        var rod = ItemDatabase.CloneItem(RodNames.ROD_SECURITY);
        if (rod == null) return;

        Assert(MetamagicRodActivation.ActivateRodOfSecurity(rod), "Demiplane created (1/week)");
        Assert(!MetamagicRodActivation.ActivateRodOfSecurity(rod), "Second use fails (1/week limit)");

        // Weekly reset
        RodDatabase.ResetWeeklyUses(new List<ItemData> { rod });
        Assert(MetamagicRodActivation.ActivateRodOfSecurity(rod), "After weekly reset: works again");
    }

    // ════════════════════════════════════════════════════════════
    //  ROD OF ALERTNESS TESTS
    // ════════════════════════════════════════════════════════════

    private static void Test_AlertnessAbilities()
    {
        Debug.Log("\n── Rod of Alertness: Abilities ──");

        var rod = ItemDatabase.CloneItem(RodNames.ROD_ALERTNESS);
        if (rod == null) return;

        Assert(rod.RodIsAlertness, "Is alertness rod");
        AssertEqual(1, rod.RodInsightBonusInit, "+1 insight to Init");
        AssertEqual(1, rod.RodInsightBonusListen, "+1 insight to Listen");
        Assert(rod.RodGrantsSeeInvisible, "Grants See Invisible");
        Assert(rod.RodGrantsDetectEvil, "Grants Detect Evil");
        Assert(rod.RodGrantsDetectMagic, "Grants Detect Magic");
        Assert(rod.RodGrantsLight, "Grants Light");

        Assert(MetamagicRodActivation.UseAlertnessAnimate(rod), "Animate 1/1");
        Assert(!MetamagicRodActivation.UseAlertnessAnimate(rod), "Animate 2 fails");

        Assert(MetamagicRodActivation.UseAlertnessPrayer(rod), "Prayer 1/1");
        Assert(!MetamagicRodActivation.UseAlertnessPrayer(rod), "Prayer 2 fails");
    }

    // ════════════════════════════════════════════════════════════
    //  ROD OF NEGATION TESTS
    // ════════════════════════════════════════════════════════════

    private static void Test_NegationGreaterDispel()
    {
        Debug.Log("\n── Rod of Negation: Greater Dispel ──");

        var rod = ItemDatabase.CloneItem(RodNames.ROD_NEGATION);
        if (rod == null) return;

        AssertEqual(15, rod.RodDispelCL, "CL 15 for dispel");
        Assert(MetamagicRodActivation.UseNegationGreaterDispel(rod), "Greater Dispel 1/2");
        Assert(MetamagicRodActivation.UseNegationGreaterDispel(rod), "Greater Dispel 2/2");
        Assert(!MetamagicRodActivation.UseNegationGreaterDispel(rod), "Greater Dispel 3 fails");
    }

    // ════════════════════════════════════════════════════════════
    //  ROD OF SPLENDOR TESTS
    // ════════════════════════════════════════════════════════════

    private static void Test_SplendorAbilities()
    {
        Debug.Log("\n── Rod of Splendor: Abilities ──");

        var rod = ItemDatabase.CloneItem(RodNames.ROD_SPLENDOR);
        if (rod == null) return;

        AssertEqual(4, rod.RodSplendorCharismaBonus, "+4 Cha enhancement");

        Assert(MetamagicRodActivation.UseSplendorFeast(rod), "Feast 1/1/day");
        Assert(!MetamagicRodActivation.UseSplendorFeast(rod), "Feast 2 fails");

        // Clothes: 7/week
        for (int i = 0; i < 7; i++)
            Assert(MetamagicRodActivation.UseSplendorClothes(rod), $"Clothes {i + 1}/7");
        Assert(!MetamagicRodActivation.UseSplendorClothes(rod), "Clothes 8 fails (7/week)");

        // Tent: 1/week
        Assert(MetamagicRodActivation.UseSplendorTent(rod), "Tent 1/1/week");
        Assert(!MetamagicRodActivation.UseSplendorTent(rod), "Tent 2 fails");
    }

    // ════════════════════════════════════════════════════════════
    //  ROD OF FLAILING TESTS
    // ════════════════════════════════════════════════════════════

    private static void Test_FlailingStats()
    {
        Debug.Log("\n── Rod of Flailing: Stats ──");

        var rod = ItemDatabase.CloneItem(RodNames.ROD_FLAILING);
        if (rod == null) return;

        Assert(rod.RodIsFlail, "Is flail rod");
        AssertEqual(3, rod.RodWeaponEnhancement, "+3 enhancement");
        AssertEqual("1d8", rod.RodWeaponDamageDice, "1d8 damage");
        AssertEqual(4, rod.RodFlailDeflectionBonus, "+4 deflection in dire flail mode");
    }

    // ════════════════════════════════════════════════════════════
    //  TOOLTIP TESTS
    // ════════════════════════════════════════════════════════════

    private static void Test_MetamagicRodTooltip()
    {
        Debug.Log("\n── Metamagic Rod Tooltip ──");

        var rod = ItemDatabase.CloneItem(RodNames.ROD_EMPOWER_LESSER);
        if (rod == null) return;

        string tooltip = rod.GetStatSummary();
        Assert(tooltip.Contains("Rod"), "Tooltip contains 'Rod'");
        Assert(tooltip.Contains("Metamagic"), "Tooltip contains 'Metamagic'");
        Assert(tooltip.Contains("Empower"), "Tooltip contains 'Empower'");
        Assert(tooltip.Contains("3"), "Tooltip shows max spell level");
        Assert(tooltip.Contains("0/3"), "Tooltip shows uses");
    }

    private static void Test_CombatRodTooltip()
    {
        Debug.Log("\n── Combat Rod Tooltip ──");

        var rod = ItemDatabase.CloneItem(RodNames.ROD_LORDLY_MIGHT);
        if (rod == null) return;

        string tooltip = rod.GetStatSummary();
        Assert(tooltip.Contains("Legendary"), "Tooltip contains 'Legendary'");
        Assert(tooltip.Contains("Heavy Mace"), "Tooltip shows weapon mode");
        Assert(tooltip.Contains("Fear"), "Tooltip shows fear ability");
    }

    // ════════════════════════════════════════════════════════════
    //  CLONE TESTS
    // ════════════════════════════════════════════════════════════

    private static void Test_CloneResetsDailyUses()
    {
        Debug.Log("\n── Clone: Resets Daily Uses ──");

        var rod = ItemDatabase.CloneItem(RodNames.ROD_EMPOWER_GREATER);
        if (rod == null) return;

        AssertEqual(0, rod.RodUsesToday, "Cloned rod starts with 0 uses");
        Assert(!rod.RodIsExpended, "Cloned rod is not expended");
        Assert(!rod.RodIsActivated, "Cloned Immovable rod starts deactivated");
    }

    private static void Test_ClonePreservesStaticFields()
    {
        Debug.Log("\n── Clone: Preserves Static Fields ──");

        var rod = ItemDatabase.CloneItem(RodNames.ROD_QUICKEN_GREATER);
        if (rod == null) return;

        Assert(rod.IsRod, "Cloned rod IsRod = true");
        Assert(rod.RodIsMetamagic, "Cloned rod IsMetamagic = true");
        Assert(rod.RodMetamagicType == MetamagicFeatId.QuickenSpell, "Metamagic type preserved");
        Assert(rod.RodPower == RodPowerLevel.Greater, "Power level preserved");
        AssertEqual(9, rod.RodMaxSpellLevel, "Max spell level preserved");
        AssertEqual(4, rod.RodSlotLevelIncrease, "Slot increase preserved");
        AssertEqual(3, rod.RodUsesPerDay, "Uses per day preserved");
        AssertEqual(170000, rod.BasePriceGp, "Price preserved");
    }

    // ════════════════════════════════════════════════════════════
    //  TEST HELPERS
    // ════════════════════════════════════════════════════════════

    private static ItemData CreateTestMetamagicRod(RodPowerLevel power, MetamagicFeatId type, int maxSpellLevel)
    {
        return new ItemData
        {
            IsRod = true,
            RodIsMetamagic = true,
            RodMetamagicType = type,
            RodPower = power,
            RodMaxSpellLevel = maxSpellLevel,
            RodUsesPerDay = 3,
            RodUsesToday = 0,
            Name = $"Test Rod of {MetamagicData.GetDisplayName(type)}, {power}"
        };
    }

    private static SpellData CreateTestSpell(int level)
    {
        return new SpellData
        {
            SpellLevel = level,
            Name = $"Test Spell (Level {level})",
            SpellId = $"test_spell_{level}",
            EffectType = SpellEffectType.Damage, // Allows Empower/Maximize
            DamageDice = 6,
            DamageCount = level,
            HasVerbalComponent = true,
            HasSomaticComponent = true
        };
    }
}
