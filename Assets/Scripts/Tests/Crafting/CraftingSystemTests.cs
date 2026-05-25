// ============================================================================
// D&D 3.5e Item Creation Feats - Comprehensive Test Suite
// Tests cost calculations, validation, and execution
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Comprehensive test suite for the D&D 3.5e Item Creation Feats system.
/// Run via console command or SceneBootstrap test hook.
/// </summary>
public static class CraftingSystemTests
{
    private static int _passed;
    private static int _failed;
    private static List<string> _failures = new List<string>();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        _failures.Clear();

        Debug.Log("═══════════════════════════════════════════════════════════════");
        Debug.Log("  D&D 3.5e ITEM CREATION FEATS — TEST SUITE");
        Debug.Log("═══════════════════════════════════════════════════════════════");

        TestScrollPricing();
        TestPotionPricing();
        TestWandPricing();
        TestArmsAndArmorPricing();
        TestCraftingCostGeneral();
        TestMinimumCasterLevels();
        TestXPSpending();
        TestCraftingConstants();

        Debug.Log("═══════════════════════════════════════════════════════════════");
        Debug.Log($"  RESULTS: {_passed} passed, {_failed} failed ({_passed + _failed} total)");
        if (_failures.Count > 0)
        {
            Debug.LogError("  FAILURES:");
            foreach (string f in _failures)
                Debug.LogError($"    ❌ {f}");
        }
        else
        {
            Debug.Log("  ✅ ALL TESTS PASSED");
        }
        Debug.Log("═══════════════════════════════════════════════════════════════");
    }

    // ============================== SCROLL PRICING ==============================

    private static void TestScrollPricing()
    {
        Debug.Log("\n--- Scroll Pricing Tests ---");

        // DMG p.238: Scroll of Magic Missile (1st level, CL 1) = 1 × 1 × 25 = 25 gp
        AssertEqual("Scroll: Magic Missile (L1 CL1)", 25, CraftingCostCalculator.ScrollMarketPrice(1, 1));

        // Scroll of Fireball (3rd level, CL 5) = 3 × 5 × 25 = 375 gp
        AssertEqual("Scroll: Fireball (L3 CL5)", 375, CraftingCostCalculator.ScrollMarketPrice(3, 5));

        // Scroll of Wish (9th level, CL 17) = 9 × 17 × 25 = 3,825 gp
        AssertEqual("Scroll: Wish (L9 CL17)", 3825, CraftingCostCalculator.ScrollMarketPrice(9, 17));

        // Cantrip scroll (L0, CL 1) = 1 × 12.5 = 12 gp (floor)
        AssertEqual("Scroll: Cantrip (L0 CL1)", 12, CraftingCostCalculator.ScrollMarketPrice(0, 1));

        // Cost: Scroll of Fireball
        var cost = CraftingCostCalculator.ForScroll(3, 5);
        AssertEqual("Scroll Fireball Gold", 187, cost.GoldCost); // 375/2 = 187
        AssertEqual("Scroll Fireball XP", 15, cost.XPCost); // 375/25 = 15
        AssertEqual("Scroll Fireball Days", 1, cost.CraftingDays); // 375/1000 = 0, min 1
    }

    // ============================== POTION PRICING ==============================

    private static void TestPotionPricing()
    {
        Debug.Log("\n--- Potion Pricing Tests ---");

        // DMG p.230: Potion of Cure Light Wounds (1st level, CL 1) = 1 × 1 × 50 = 50 gp
        AssertEqual("Potion: CLW (L1 CL1)", 50, CraftingCostCalculator.PotionMarketPrice(1, 1));

        // Potion of Bull's Strength (2nd level, CL 3) = 2 × 3 × 50 = 300 gp
        AssertEqual("Potion: Bull's Str (L2 CL3)", 300, CraftingCostCalculator.PotionMarketPrice(2, 3));

        // Potion of Fly (3rd level, CL 5) = 3 × 5 × 50 = 750 gp
        AssertEqual("Potion: Fly (L3 CL5)", 750, CraftingCostCalculator.PotionMarketPrice(3, 5));

        // Cantrip potion (L0, CL 1) = 1 × 25 = 25 gp
        AssertEqual("Potion: Cantrip (L0 CL1)", 25, CraftingCostCalculator.PotionMarketPrice(0, 1));

        // Cost: Potion of CLW
        var cost = CraftingCostCalculator.ForPotion(1, 1);
        AssertEqual("Potion CLW Gold", 25, cost.GoldCost);
        AssertEqual("Potion CLW XP", 2, cost.XPCost); // 50/25 = 2
        AssertEqual("Potion CLW Days", 1, cost.CraftingDays);
    }

    // ============================== WAND PRICING ==============================

    private static void TestWandPricing()
    {
        Debug.Log("\n--- Wand Pricing Tests ---");

        // DMG p.245: Wand of Magic Missile (1st level, CL 1) = 1 × 1 × 750 = 750 gp
        AssertEqual("Wand: Magic Missile (L1 CL1)", 750, CraftingCostCalculator.WandMarketPrice(1, 1));

        // Wand of Fireball (3rd level, CL 5) = 5 × 3 × 750 = 11,250 gp
        AssertEqual("Wand: Fireball (L3 CL5)", 11250, CraftingCostCalculator.WandMarketPrice(3, 5));

        // Wand of Stoneskin (4th level, CL 7) = 7 × 4 × 750 = 21,000 gp
        AssertEqual("Wand: Stoneskin (L4 CL7)", 21000, CraftingCostCalculator.WandMarketPrice(4, 7));

        // Cantrip wand (L0, CL 1) = 1 × 375 = 375 gp
        AssertEqual("Wand: Cantrip (L0 CL1)", 375, CraftingCostCalculator.WandMarketPrice(0, 1));

        // Cost: Wand of Magic Missile
        var cost = CraftingCostCalculator.ForWand(1, 1);
        AssertEqual("Wand MM Gold", 375, cost.GoldCost);
        AssertEqual("Wand MM XP", 30, cost.XPCost); // 750/25 = 30
        AssertEqual("Wand MM Days", 1, cost.CraftingDays);
    }

    // ============================== ARMS & ARMOR PRICING ==============================

    private static void TestArmsAndArmorPricing()
    {
        Debug.Log("\n--- Arms & Armor Pricing Tests ---");

        // DMG p.215: +1 weapon = 1² × 2000 = 2,000 gp
        AssertEqual("Weapon +1 market", 2000, CraftingCostCalculator.WeaponEnhancementMarketPrice(1));

        // +2 weapon = 2² × 2000 = 8,000 gp
        AssertEqual("Weapon +2 market", 8000, CraftingCostCalculator.WeaponEnhancementMarketPrice(2));

        // +5 weapon = 5² × 2000 = 50,000 gp
        AssertEqual("Weapon +5 market", 50000, CraftingCostCalculator.WeaponEnhancementMarketPrice(5));

        // +1 armor = 1² × 1000 = 1,000 gp
        AssertEqual("Armor +1 market", 1000, CraftingCostCalculator.ArmorEnhancementMarketPrice(1));

        // +3 armor = 3² × 1000 = 9,000 gp
        AssertEqual("Armor +3 market", 9000, CraftingCostCalculator.ArmorEnhancementMarketPrice(3));

        // Weapon upgrade +1 to +3: (9×2000) - (1×2000) = 18000 - 2000 = 16000
        var upgrade = CraftingCostCalculator.ForWeaponUpgrade(1, 3);
        AssertEqual("Weapon +1→+3 market", 16000, upgrade.MarketPriceGp);
        AssertEqual("Weapon +1→+3 gold", 8000, upgrade.GoldCost);
        AssertEqual("Weapon +1→+3 xp", 640, upgrade.XPCost); // 16000/25 = 640

        // Armor upgrade +2 to +4: (16×1000) - (4×1000) = 16000 - 4000 = 12000
        var armorUpgrade = CraftingCostCalculator.ForArmorUpgrade(2, 4);
        AssertEqual("Armor +2→+4 market", 12000, armorUpgrade.MarketPriceGp);

        // With flat cost
        AssertEqual("Weapon +1 + 8000 flat", 10000,
            CraftingCostCalculator.WeaponEnhancementMarketPrice(1, 8000));
    }

    // ============================== GENERAL COST FORMULA ==============================

    private static void TestCraftingCostGeneral()
    {
        Debug.Log("\n--- General Cost Formula Tests ---");

        // Standard item: 10,000 gp market price
        var cost = CraftingCostCalculator.FromMarketPrice(10000);
        AssertEqual("10k market gold", 5000, cost.GoldCost);
        AssertEqual("10k market xp", 400, cost.XPCost);
        AssertEqual("10k market days", 10, cost.CraftingDays);

        // Minimum 1 day for cheap items
        var cheapCost = CraftingCostCalculator.FromMarketPrice(500);
        AssertEqual("500gp market days", 1, cheapCost.CraftingDays);
        AssertEqual("500gp market gold", 250, cheapCost.GoldCost);
        AssertEqual("500gp market xp", 20, cheapCost.XPCost);

        // Zero cost item
        var zeroCost = CraftingCostCalculator.FromMarketPrice(0);
        AssertEqual("0gp market gold", 0, zeroCost.GoldCost);
        AssertEqual("0gp market xp", 0, zeroCost.XPCost);

        // Large item: 200,000 gp
        var largeCost = CraftingCostCalculator.FromMarketPrice(200000);
        AssertEqual("200k market gold", 100000, largeCost.GoldCost);
        AssertEqual("200k market xp", 8000, largeCost.XPCost);
        AssertEqual("200k market days", 200, largeCost.CraftingDays);
    }

    // ============================== MINIMUM CASTER LEVELS ==============================

    private static void TestMinimumCasterLevels()
    {
        Debug.Log("\n--- Minimum Caster Level Tests ---");

        // L0 → CL 1
        AssertEqual("Min CL for L0", 1, CraftingCostCalculator.MinimumCasterLevelForSpell(0));
        // L1 → CL 1 (1*2-1=1)
        AssertEqual("Min CL for L1", 1, CraftingCostCalculator.MinimumCasterLevelForSpell(1));
        // L2 → CL 3
        AssertEqual("Min CL for L2", 3, CraftingCostCalculator.MinimumCasterLevelForSpell(2));
        // L3 → CL 5
        AssertEqual("Min CL for L3", 5, CraftingCostCalculator.MinimumCasterLevelForSpell(3));
        // L5 → CL 9
        AssertEqual("Min CL for L5", 9, CraftingCostCalculator.MinimumCasterLevelForSpell(5));
        // L9 → CL 17
        AssertEqual("Min CL for L9", 17, CraftingCostCalculator.MinimumCasterLevelForSpell(9));
    }

    // ============================== XP SPENDING ==============================

    private static void TestXPSpending()
    {
        Debug.Log("\n--- XP Spending Tests ---");

        // Create test character: Level 5, 10,000 XP
        // Level 5 floor = ((5-1)*5/2)*1000 = 10,000
        // So max spendable XP = 10000 - 10000 = 0 at exact threshold

        // Level 5 with 12,000 XP → max spendable = 12000 - 10000 = 2000
        var stats = CreateTestStats("TestCrafter", 5, 12000, 5000);
        AssertEqual("MaxSpendableXP L5@12000", 2000, stats.MaxSpendableXP());

        // Spend 1000 XP → success
        bool result = stats.SpendXP(1000);
        AssertTrue("SpendXP 1000 success", result);
        AssertEqual("XP after spend 1000", 11000, stats.ExperiencePoints);

        // Try to spend 2000 XP → should fail (would drop to 9000, below 10000 floor)
        bool result2 = stats.SpendXP(2000);
        AssertTrue("SpendXP 2000 fail", !result2);
        AssertEqual("XP unchanged after failed spend", 11000, stats.ExperiencePoints);

        // Spend exactly remaining
        bool result3 = stats.SpendXP(1000);
        AssertTrue("SpendXP exact remaining", result3);
        AssertEqual("XP at floor after exact spend", 10000, stats.ExperiencePoints);

        // Test gold spending
        var goldStats = CreateTestStats("GoldTest", 5, 15000, 3000);
        AssertTrue("SpendGold 2000 success", goldStats.SpendComponentGold(2000));
        AssertEqual("Gold after spend", 1000, goldStats.ComponentGold);
        AssertTrue("SpendGold 2000 fail", !goldStats.SpendComponentGold(2000));
        AssertEqual("Gold unchanged", 1000, goldStats.ComponentGold);

        // Level 1 character at 0 XP
        var l1Stats = CreateTestStats("L1", 1, 0, 100);
        AssertEqual("MaxSpendableXP L1@0", 0, l1Stats.MaxSpendableXP());
        AssertTrue("SpendXP 0 at level 1", !l1Stats.SpendXP(1));
    }

    // ============================== CONSTANTS ==============================

    private static void TestCraftingConstants()
    {
        Debug.Log("\n--- Crafting Constants Tests ---");

        AssertEqual("Feat count", 8, CraftingConstants.FeatNames.Count);
        AssertEqual("CL req count", 8, CraftingConstants.FeatCasterLevelReqs.Count);

        AssertEqual("ScribeScroll name", "Scribe Scroll", CraftingConstants.GetFeatName(CraftingFeatType.ScribeScroll));
        AssertEqual("ForgeRing name", "Forge Ring", CraftingConstants.GetFeatName(CraftingFeatType.ForgeRing));

        AssertEqual("ScribeScroll CL", 1, CraftingConstants.FeatCasterLevelReqs[CraftingFeatType.ScribeScroll]);
        AssertEqual("BrewPotion CL", 3, CraftingConstants.FeatCasterLevelReqs[CraftingFeatType.BrewPotion]);
        AssertEqual("CraftWondrous CL", 3, CraftingConstants.FeatCasterLevelReqs[CraftingFeatType.CraftWondrousItem]);
        AssertEqual("CraftArms CL", 5, CraftingConstants.FeatCasterLevelReqs[CraftingFeatType.CraftMagicArmsAndArmor]);
        AssertEqual("CraftWand CL", 5, CraftingConstants.FeatCasterLevelReqs[CraftingFeatType.CraftWand]);
        AssertEqual("CraftRod CL", 9, CraftingConstants.FeatCasterLevelReqs[CraftingFeatType.CraftRod]);
        AssertEqual("CraftStaff CL", 12, CraftingConstants.FeatCasterLevelReqs[CraftingFeatType.CraftStaff]);
        AssertEqual("ForgeRing CL", 12, CraftingConstants.FeatCasterLevelReqs[CraftingFeatType.ForgeRing]);
    }

    // ============================== HELPERS ==============================

    private static CharacterStats CreateTestStats(string name, int level, int xp, int gold)
    {
        var stats = new CharacterStats
        {
            CharacterName = name,
            Level = level,
            ExperiencePoints = xp,
            ComponentGold = gold
        };
        return stats;
    }

    private static void AssertEqual(string testName, int expected, int actual)
    {
        if (expected == actual)
        {
            _passed++;
            Debug.Log($"  ✅ {testName}: {actual}");
        }
        else
        {
            _failed++;
            string msg = $"{testName}: expected {expected}, got {actual}";
            _failures.Add(msg);
            Debug.LogError($"  ❌ {msg}");
        }
    }

    private static void AssertEqual(string testName, string expected, string actual)
    {
        if (expected == actual)
        {
            _passed++;
            Debug.Log($"  ✅ {testName}: \"{actual}\"");
        }
        else
        {
            _failed++;
            string msg = $"{testName}: expected \"{expected}\", got \"{actual}\"";
            _failures.Add(msg);
            Debug.LogError($"  ❌ {msg}");
        }
    }

    private static void AssertTrue(string testName, bool condition)
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  ✅ {testName}: true");
        }
        else
        {
            _failed++;
            string msg = $"{testName}: expected true, got false";
            _failures.Add(msg);
            Debug.LogError($"  ❌ {msg}");
        }
    }
}
