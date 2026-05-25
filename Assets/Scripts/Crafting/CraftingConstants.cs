// ============================================================================
// D&D 3.5e Item Creation Feats - Constants & Feat-Name Mappings
// ============================================================================

using System.Collections.Generic;

/// <summary>
/// Central constants for the D&D 3.5e crafting system.
/// Pricing formulas, feat name mappings, and CL requirements.
/// </summary>
public static class CraftingConstants
{
    // ============================== PRICING ==============================
    // DMG p.282: Gold cost = MarketPrice / 2, XP cost = MarketPrice / 25
    // Time = 1 day per 1,000 gp of market price (minimum 1 day)

    /// <summary>Divisor for raw material gold cost (half market price).</summary>
    public const int GoldCostDivisor = 2;

    /// <summary>Divisor for XP cost (1/25 of market price).</summary>
    public const int XPCostDivisor = 25;

    /// <summary>Gold amount per day of crafting time.</summary>
    public const int GoldPerCraftingDay = 1000;

    // ============================== SCROLL PRICING ==============================
    // DMG p.238: Scroll base price = spell level × caster level × 25 gp
    // 0th-level scrolls use spell level 0.5 for pricing.
    public const int ScrollPriceMultiplier = 25;

    // ============================== POTION PRICING ==============================
    // DMG p.230: Potion base price = spell level × caster level × 50 gp
    // 0th-level potions use spell level 0.5 for pricing.
    public const int PotionPriceMultiplier = 50;

    // ============================== WAND PRICING ==============================
    // DMG p.245: Wand base price = caster level × spell level × 750 gp
    // 0th-level wands use spell level 0.5 for pricing.
    public const int WandPriceMultiplier = 750;
    public const int WandMaxCharges = 50;
    public const int WandMaxSpellLevel = 4;

    // ============================== POTION LIMITS ==============================
    public const int PotionMaxSpellLevel = 3;

    // ============================== DC ADJUSTMENT ==============================
    // DMG p.282: +5 to crafting DC per missing spell prerequisite
    public const int MissingSpellDCIncrease = 5;
    public const int BaseCraftingDC = 5; // used for Spellcraft checks when substituting

    // ============================== FEAT NAME MAPPINGS ==============================

    /// <summary>
    /// Maps CraftingFeatType enum to the feat name string used in FeatDefinitions/HasFeat().
    /// </summary>
    public static readonly Dictionary<CraftingFeatType, string> FeatNames = new Dictionary<CraftingFeatType, string>
    {
        { CraftingFeatType.ScribeScroll,           "Scribe Scroll" },
        { CraftingFeatType.BrewPotion,             "Brew Potion" },
        { CraftingFeatType.CraftWondrousItem,      "Craft Wondrous Item" },
        { CraftingFeatType.CraftMagicArmsAndArmor, "Craft Magic Arms and Armor" },
        { CraftingFeatType.CraftWand,              "Craft Wand" },
        { CraftingFeatType.CraftRod,               "Craft Rod" },
        { CraftingFeatType.CraftStaff,             "Craft Staff" },
        { CraftingFeatType.ForgeRing,              "Forge Ring" },
    };

    /// <summary>
    /// Minimum caster level prerequisite for each item creation feat.
    /// </summary>
    public static readonly Dictionary<CraftingFeatType, int> FeatCasterLevelReqs = new Dictionary<CraftingFeatType, int>
    {
        { CraftingFeatType.ScribeScroll,           1 },
        { CraftingFeatType.BrewPotion,             3 },
        { CraftingFeatType.CraftWondrousItem,      3 },
        { CraftingFeatType.CraftMagicArmsAndArmor, 5 },
        { CraftingFeatType.CraftWand,              5 },
        { CraftingFeatType.CraftRod,               9 },
        { CraftingFeatType.CraftStaff,             12 },
        { CraftingFeatType.ForgeRing,              12 },
    };

    /// <summary>Returns the feat display name for the given crafting feat type.</summary>
    public static string GetFeatName(CraftingFeatType feat)
    {
        return FeatNames.TryGetValue(feat, out string name) ? name : feat.ToString();
    }
}
