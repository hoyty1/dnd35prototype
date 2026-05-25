// ============================================================================
// D&D 3.5e Item Creation Feats - Cost Calculator
// All pricing formulas per DMG p.282-285, 238, 230, 245
// ============================================================================

using UnityEngine;

/// <summary>
/// Calculates gold, XP, and time costs for crafting magic items.
/// All formulas follow D&D 3.5e DMG rules precisely.
/// </summary>
public static class CraftingCostCalculator
{
    /// <summary>
    /// Result of a crafting cost calculation.
    /// </summary>
    public struct CraftingCost
    {
        /// <summary>Market price of the finished item in gp.</summary>
        public int MarketPriceGp;

        /// <summary>Raw material cost in gp (MarketPrice / 2).</summary>
        public int GoldCost;

        /// <summary>XP cost to the crafter (MarketPrice / 25).</summary>
        public int XPCost;

        /// <summary>Crafting time in days (1 per 1,000 gp of market price, minimum 1).</summary>
        public int CraftingDays;

        /// <summary>Human-readable summary of the cost breakdown.</summary>
        public string Summary => $"{GoldCost:N0} gp, {XPCost:N0} XP, {CraftingDays} day{(CraftingDays != 1 ? "s" : "")}";

        public override string ToString() => $"Market: {MarketPriceGp:N0} gp | Cost: {Summary}";
    }

    // ============================== GENERAL ==============================

    /// <summary>
    /// Calculate crafting costs from a known market price (for fixed-price items like
    /// wondrous items, rings, rods, staves, and pre-priced arms & armor).
    /// DMG p.282: Gold = MarketPrice/2, XP = MarketPrice/25, Days = MarketPrice/1000 (min 1).
    /// </summary>
    public static CraftingCost FromMarketPrice(int marketPriceGp)
    {
        int safe = Mathf.Max(0, marketPriceGp);
        return new CraftingCost
        {
            MarketPriceGp = safe,
            GoldCost = safe / CraftingConstants.GoldCostDivisor,
            XPCost = safe / CraftingConstants.XPCostDivisor,
            CraftingDays = Mathf.Max(1, safe / CraftingConstants.GoldPerCraftingDay)
        };
    }

    // ============================== SCROLLS ==============================
    // DMG p.238: Base price = spell level × caster level × 25 gp
    // 0th-level scrolls treat spell level as 0.5 for pricing: CL × 12.5 gp (round down)

    /// <summary>
    /// Calculate market price for a scroll.
    /// </summary>
    public static int ScrollMarketPrice(int spellLevel, int casterLevel)
    {
        int cl = Mathf.Max(1, casterLevel);
        if (spellLevel <= 0)
        {
            // Cantrip scroll: CL × 12.5 gp (round down to nearest integer)
            return Mathf.FloorToInt(cl * 12.5f);
        }
        return spellLevel * cl * CraftingConstants.ScrollPriceMultiplier;
    }

    /// <summary>
    /// Calculate full crafting costs for a scroll.
    /// </summary>
    public static CraftingCost ForScroll(int spellLevel, int casterLevel)
    {
        return FromMarketPrice(ScrollMarketPrice(spellLevel, casterLevel));
    }

    // ============================== POTIONS ==============================
    // DMG p.230: Base price = spell level × caster level × 50 gp
    // 0th-level potions treat spell level as 0.5: CL × 25 gp

    /// <summary>
    /// Calculate market price for a potion. Max spell level 3.
    /// </summary>
    public static int PotionMarketPrice(int spellLevel, int casterLevel)
    {
        int cl = Mathf.Max(1, casterLevel);
        if (spellLevel <= 0)
        {
            // Cantrip potion: CL × 25 gp
            return cl * 25;
        }
        return spellLevel * cl * CraftingConstants.PotionPriceMultiplier;
    }

    /// <summary>
    /// Calculate full crafting costs for a potion.
    /// </summary>
    public static CraftingCost ForPotion(int spellLevel, int casterLevel)
    {
        return FromMarketPrice(PotionMarketPrice(spellLevel, casterLevel));
    }

    // ============================== WANDS ==============================
    // DMG p.245: Base price = caster level × spell level × 750 gp
    // 0th-level wands treat spell level as 0.5: CL × 375 gp

    /// <summary>
    /// Calculate market price for a wand (50 charges). Max spell level 4.
    /// </summary>
    public static int WandMarketPrice(int spellLevel, int casterLevel)
    {
        int cl = Mathf.Max(1, casterLevel);
        if (spellLevel <= 0)
        {
            // Cantrip wand: CL × 375 gp
            return Mathf.FloorToInt(cl * 375f);
        }
        return cl * spellLevel * CraftingConstants.WandPriceMultiplier;
    }

    /// <summary>
    /// Calculate full crafting costs for a wand.
    /// </summary>
    public static CraftingCost ForWand(int spellLevel, int casterLevel)
    {
        return FromMarketPrice(WandMarketPrice(spellLevel, casterLevel));
    }

    // ============================== ARMS & ARMOR ==============================
    // DMG p.215-217: Enhancement pricing
    // Weapon: bonus² × 2,000 gp + flat cost specials + masterwork base (300 gp for weapon)
    // Armor: bonus² × 1,000 gp + flat cost specials + masterwork base (150 gp for armor)
    // Shield: bonus² × 1,000 gp + flat cost specials + masterwork base (150 gp for shield)

    /// <summary>
    /// Calculate enhancement cost for weapons (total effective bonus squared × 2,000 gp).
    /// Does NOT include the masterwork base item cost (which the crafter must already have).
    /// </summary>
    public static int WeaponEnhancementMarketPrice(int totalEffectiveBonus, int flatCostGp = 0)
    {
        int bonus = Mathf.Clamp(totalEffectiveBonus, 0, 10);
        return bonus * bonus * 2000 + flatCostGp;
    }

    /// <summary>
    /// Calculate enhancement cost for armor or shields (total effective bonus squared × 1,000 gp).
    /// Does NOT include the masterwork base item cost.
    /// </summary>
    public static int ArmorEnhancementMarketPrice(int totalEffectiveBonus, int flatCostGp = 0)
    {
        int bonus = Mathf.Clamp(totalEffectiveBonus, 0, 10);
        return bonus * bonus * 1000 + flatCostGp;
    }

    /// <summary>
    /// Calculate crafting costs for a weapon enhancement.
    /// </summary>
    public static CraftingCost ForWeaponEnhancement(int totalEffectiveBonus, int flatCostGp = 0)
    {
        return FromMarketPrice(WeaponEnhancementMarketPrice(totalEffectiveBonus, flatCostGp));
    }

    /// <summary>
    /// Calculate crafting costs for an armor/shield enhancement.
    /// </summary>
    public static CraftingCost ForArmorEnhancement(int totalEffectiveBonus, int flatCostGp = 0)
    {
        return FromMarketPrice(ArmorEnhancementMarketPrice(totalEffectiveBonus, flatCostGp));
    }

    // ============================== UPGRADE COSTS ==============================

    /// <summary>
    /// Calculate the incremental cost to upgrade an existing enhanced weapon.
    /// E.g., upgrading from +1 to +3 costs the difference: (3²×2000) - (1²×2000) = 16,000 gp market.
    /// </summary>
    public static CraftingCost ForWeaponUpgrade(int currentBonus, int targetBonus, int currentFlatCost = 0, int targetFlatCost = 0)
    {
        int currentMarket = WeaponEnhancementMarketPrice(currentBonus, currentFlatCost);
        int targetMarket = WeaponEnhancementMarketPrice(targetBonus, targetFlatCost);
        return FromMarketPrice(Mathf.Max(0, targetMarket - currentMarket));
    }

    /// <summary>
    /// Calculate the incremental cost to upgrade existing enhanced armor/shield.
    /// </summary>
    public static CraftingCost ForArmorUpgrade(int currentBonus, int targetBonus, int currentFlatCost = 0, int targetFlatCost = 0)
    {
        int currentMarket = ArmorEnhancementMarketPrice(currentBonus, currentFlatCost);
        int targetMarket = ArmorEnhancementMarketPrice(targetBonus, targetFlatCost);
        return FromMarketPrice(Mathf.Max(0, targetMarket - currentMarket));
    }

    // ============================== MINIMUM CASTER LEVELS ==============================

    /// <summary>
    /// Returns the minimum caster level required to cast a spell of the given level.
    /// For full casters (Wizard, Cleric, Druid, Sorcerer): CL = spellLevel × 2 - 1 (min 1).
    /// This is the default used for scroll/potion/wand pricing when no higher CL is specified.
    /// </summary>
    public static int MinimumCasterLevelForSpell(int spellLevel)
    {
        if (spellLevel <= 0) return 1;
        return Mathf.Max(1, spellLevel * 2 - 1);
    }

    /// <summary>
    /// For Ranger/Paladin half-casters: minimum CL to cast a spell of the given level.
    /// Ranger/Paladin get spells at character level = spellLevel × 2 + 2,
    /// but CL = characterLevel - 3. So min CL = (spellLevel × 2 + 2) - 3 = spellLevel × 2 - 1.
    /// This happens to equal the full caster formula, but their max spell level is 4.
    /// </summary>
    public static int MinimumHalfCasterLevelForSpell(int spellLevel)
    {
        return MinimumCasterLevelForSpell(spellLevel);
    }
}
