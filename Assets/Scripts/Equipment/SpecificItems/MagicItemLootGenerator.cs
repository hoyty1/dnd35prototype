using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ============================================================================
// D&D 3.5e Magic Item Loot Generator
// Phase 7: CR-based treasure generation with random enchantment application
// Reference: DMG Table 7-1 (Treasure Values per Encounter Level)
// ============================================================================

/// <summary>
/// Generates magic item loot appropriate to Challenge Rating using D&D 3.5 DMG
/// treasure tables. Uses DiceService for all randomization. All enchantment
/// data is pulled from EnchantmentProperties (data-driven, no hardcoding).
/// </summary>
public static class MagicItemLootGenerator
{
    // ========================================================================
    //  DMG Table 7-1: Average Treasure Value by CR (in gold pieces)
    //  These values represent the expected treasure per encounter.
    // ========================================================================
    private static readonly int[] TreasureValueByCR = new int[]
    {
        0,      // CR 0 (unused)
        300,    // CR 1
        600,    // CR 2
        900,    // CR 3
        1200,   // CR 4
        1600,   // CR 5
        2000,   // CR 6
        2600,   // CR 7
        3400,   // CR 8
        4500,   // CR 9
        5800,   // CR 10
        7500,   // CR 11
        9800,   // CR 12
        13000,  // CR 13
        17000,  // CR 14
        22000,  // CR 15
        28000,  // CR 16
        36000,  // CR 17
        47000,  // CR 18
        61000,  // CR 19
        80000,  // CR 20
    };

    /// <summary>
    /// The result of a loot generation roll, containing items and residual gold.
    /// </summary>
    public class LootResult
    {
        public List<ItemData> MagicItems = new List<ItemData>();
        public int GoldPieces;
        public int ChallengeRating;
        public int TotalBudget;

        public override string ToString()
        {
            string items = MagicItems.Count > 0
                ? string.Join(", ", MagicItems.Select(i => i.FullDisplayName))
                : "none";
            return $"CR {ChallengeRating} Loot (budget {TotalBudget}gp): {items} + {GoldPieces}gp coin";
        }
    }

    // ========================================================================
    //  PUBLIC API
    // ========================================================================

    /// <summary>
    /// Generate treasure appropriate for the given Challenge Rating.
    /// Items are copies (cloned from database) so they can be freely modified.
    /// </summary>
    /// <param name="cr">Challenge Rating (1-20).</param>
    /// <param name="allowMagicItems">If false, returns only gold.</param>
    /// <returns>LootResult with magic items and leftover gold.</returns>
    public static LootResult GenerateLootForCR(int cr, bool allowMagicItems = true)
    {
        int budget = GetTreasureBudget(cr);
        return GenerateTreasureByValue(budget, cr, allowMagicItems);
    }

    /// <summary>
    /// Generate treasure for a specific gold piece budget.
    /// </summary>
    /// <param name="budgetGp">Total gold piece value to spend.</param>
    /// <param name="cr">CR for logging/metadata (optional, default 0).</param>
    /// <param name="allowMagicItems">If false, returns only gold.</param>
    public static LootResult GenerateTreasureByValue(int budgetGp, int cr = 0, bool allowMagicItems = true)
    {
        var result = new LootResult
        {
            ChallengeRating = cr,
            TotalBudget = budgetGp
        };

        if (!allowMagicItems || budgetGp < 300)
        {
            result.GoldPieces = budgetGp;
            return result;
        }

        int remaining = budgetGp;

        // Determine how much of the budget goes to magic items vs. coins
        // DMG suggests roughly 50-75% in items for mid-high CR
        int magicBudget = DetermineItemBudget(remaining);
        int coinBudget = remaining - magicBudget;

        // Try to fill the magic budget with 1-3 items
        int maxItems = magicBudget >= 10000 ? 3 : (magicBudget >= 4000 ? 2 : 1);
        int itemsGenerated = 0;

        while (magicBudget >= 300 && itemsGenerated < maxItems)
        {
            int perItemBudget = magicBudget / (maxItems - itemsGenerated);
            perItemBudget = Mathf.Max(300, perItemBudget);

            var item = GenerateRandomMagicItem(perItemBudget);
            if (item != null)
            {
                result.MagicItems.Add(item);
                magicBudget -= EstimateItemValue(item);
                itemsGenerated++;
            }
            else
            {
                // Couldn't generate an item within budget, convert to gold
                break;
            }
        }

        // Remaining magic budget becomes coins
        result.GoldPieces = coinBudget + Mathf.Max(0, magicBudget);
        return result;
    }

    /// <summary>
    /// Generate a single random magic weapon within the given gold budget.
    /// </summary>
    public static ItemData GenerateRandomMagicWeapon(int budgetGp)
    {
        return GenerateEnchantedItem(ItemType.Weapon, budgetGp);
    }

    /// <summary>
    /// Generate a single random magic armor within the given gold budget.
    /// </summary>
    public static ItemData GenerateRandomMagicArmor(int budgetGp)
    {
        return GenerateEnchantedItem(ItemType.Armor, budgetGp);
    }

    /// <summary>
    /// Generate a single random magic shield within the given gold budget.
    /// </summary>
    public static ItemData GenerateRandomMagicShield(int budgetGp)
    {
        return GenerateEnchantedItem(ItemType.Shield, budgetGp);
    }

    // ========================================================================
    //  BUDGET HELPERS
    // ========================================================================

    /// <summary>Get the treasure budget for a CR (clamped 1-20).</summary>
    public static int GetTreasureBudget(int cr)
    {
        cr = Mathf.Clamp(cr, 1, 20);
        return TreasureValueByCR[cr];
    }

    /// <summary>Determine how much of the budget goes to magic items.</summary>
    private static int DetermineItemBudget(int totalBudget)
    {
        // 40-70% of budget goes to items (rolled)
        int percent = DiceService.Roll(40, 70, "Loot item budget %");
        return (totalBudget * percent) / 100;
    }

    // ========================================================================
    //  ITEM GENERATION
    // ========================================================================

    /// <summary>
    /// Generate a random magic item of any equipment type within budget.
    /// </summary>
    private static ItemData GenerateRandomMagicItem(int budgetGp)
    {
        // Roll to determine item type: 60% weapon, 25% armor, 15% shield
        int typeRoll = DiceService.Percentile("Loot item type");
        ItemType targetType;
        if (typeRoll <= 60)
            targetType = ItemType.Weapon;
        else if (typeRoll <= 85)
            targetType = ItemType.Armor;
        else
            targetType = ItemType.Shield;

        return GenerateEnchantedItem(targetType, budgetGp);
    }

    /// <summary>
    /// Create an enchanted item of the specified type within the gold budget.
    /// Picks a random base item, applies enhancement bonus, then adds random
    /// special abilities until the budget is exhausted.
    /// </summary>
    private static ItemData GenerateEnchantedItem(ItemType type, int budgetGp)
    {
        // Get candidate base items from the database
        var candidates = GetBaseItemsOfType(type);
        if (candidates.Count == 0) return null;

        // Pick a random base item
        int idx = DiceService.Roll(0, candidates.Count - 1, $"Loot base {type} selection");
        var baseItem = candidates[idx];

        // Clone so we don't modify the database original
        #pragma warning disable CS0618 // CloneItem(string) obsolete warning - we need string ID here
        var item = ItemDatabase.CloneItem(baseItem.Id);
        #pragma warning restore CS0618
        if (item == null) return null;

        // Ensure masterwork (required for enchantment)
        if (!item.IsMasterwork)
        {
            item.IsMasterwork = true;
        }

        // Determine enhancement bonus (1-5, limited by budget)
        int maxEnhancement = DetermineMaxEnhancement(budgetGp, type);
        if (maxEnhancement < 1) return null;

        int enhancement = DiceService.Roll(1, maxEnhancement, "Loot enhancement bonus");
        item.EnhancementBonus = enhancement;
        int usedBudget = CalculateEnhancementCost(enhancement, type);

        // Try to add special abilities
        int remainingBudget = budgetGp - usedBudget;
        if (remainingBudget >= 1000)
        {
            AddRandomAbilities(item, enhancement, remainingBudget, type);
        }

        // Initialize enchantment data if abilities were added
        if (item.Enchantment == null)
        {
            item.Enchantment = new ItemEnchantmentData();
        }

        // Generate a unique ID for the loot item
        string lootId = $"loot_{item.Id}_{enhancement}_{DiceService.Roll(1000, 9999, "Loot unique ID")}";
        item.Id = lootId;

        return item;
    }

    /// <summary>Get all base (non-enhanced, non-material-variant) items of a type.</summary>
    private static List<ItemData> GetBaseItemsOfType(ItemType type)
    {
        var allItems = ItemDatabase.AllItems;
        var result = new List<ItemData>();

        foreach (var item in allItems)
        {
            if (item.Type != type) continue;
            // Skip items that are already enchanted or material variants
            if (item.EnhancementBonus > 0) continue;
            if (item.Material != null && item.Material.MaterialType != ItemMaterialType.Standard) continue;
            // Skip ammunition
            if (item.Type == ItemType.Ammunition) continue;
            result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Determine the maximum enhancement bonus affordable within budget.
    /// D&D 3.5: weapon +1 = 2000gp, +2 = 8000gp, +3 = 18000gp, +4 = 32000gp, +5 = 50000gp
    /// Armor/Shield: +1 = 1000gp, +2 = 4000gp, +3 = 9000gp, +4 = 16000gp, +5 = 25000gp
    /// </summary>
    private static int DetermineMaxEnhancement(int budgetGp, ItemType type)
    {
        for (int bonus = 5; bonus >= 1; bonus--)
        {
            if (CalculateEnhancementCost(bonus, type) <= budgetGp)
                return bonus;
        }
        return 0;
    }

    /// <summary>Calculate the gold cost of an enhancement bonus.</summary>
    private static int CalculateEnhancementCost(int bonus, ItemType type)
    {
        // D&D 3.5 pricing: bonus^2 × multiplier
        // Weapons: bonus^2 × 2000gp
        // Armor/Shields: bonus^2 × 1000gp
        int multiplier = (type == ItemType.Weapon) ? 2000 : 1000;
        return bonus * bonus * multiplier;
    }

    /// <summary>
    /// Try to add random special abilities to an item within the remaining budget.
    /// Respects the D&D 3.5 rule: total effective bonus (enhancement + abilities) cannot exceed +10.
    /// </summary>
    private static void AddRandomAbilities(ItemData item, int enhancementBonus, int remainingBudgetGp, ItemType type)
    {
        // Get valid enchantments for this item type
        EnchantmentSlot slot = (type == ItemType.Weapon) ? EnchantmentSlot.Weapon
            : (type == ItemType.Armor) ? EnchantmentSlot.Armor
            : EnchantmentSlot.Shield;

        var validEnchantments = EnchantmentProperties.GetForSlot(slot);
        if (validEnchantments == null || validEnchantments.Count == 0) return;

        if (item.Enchantment == null)
            item.Enchantment = new ItemEnchantmentData();

        int currentBonusTotal = enhancementBonus;
        int maxAttempts = 5; // prevent infinite loops
        int attempts = 0;

        while (attempts < maxAttempts && remainingBudgetGp >= 1000)
        {
            attempts++;

            // Filter to affordable and valid abilities
            var affordable = new List<EnchantmentStats>();
            foreach (var ench in validEnchantments)
            {
                // Skip if already applied
                if (item.Enchantment.HasAbility(ench.Type)) continue;

                // Check bonus cap (+10 total)
                if (ench.BonusEquivalent > 0 && (currentBonusTotal + ench.BonusEquivalent) > 10) continue;

                // Check cost affordability
                int abilityCost = GetAbilityCost(ench, currentBonusTotal, type);
                if (abilityCost > remainingBudgetGp) continue;

                // Validate prerequisites (e.g., Disruption requires bludgeoning)
                string valErr = EnchantmentFactory.ValidateAbility(item, enhancementBonus, ench.Type, 
                    item.Enchantment?.Abilities?.ToArray() ?? Array.Empty<EnchantmentType>());
                if (!string.IsNullOrEmpty(valErr)) continue;

                affordable.Add(ench);
            }

            if (affordable.Count == 0) break;

            // Pick a random ability
            int pick = DiceService.Roll(0, affordable.Count - 1, "Loot ability selection");
            var chosen = affordable[pick];

            // Apply it
            item.Enchantment.AddAbility(chosen.Type);
            int cost = GetAbilityCost(chosen, currentBonusTotal, type);
            remainingBudgetGp -= cost;
            if (chosen.BonusEquivalent > 0)
                currentBonusTotal += chosen.BonusEquivalent;

            // 50% chance to stop after each ability (variety)
            if (DiceService.PercentileCheck(50, "Loot stop adding abilities", out _))
                break;
        }
    }

    /// <summary>
    /// Calculate the gold cost of adding a specific ability.
    /// Bonus-equivalent abilities use the difference in squared bonus × multiplier.
    /// Flat-cost abilities use their FlatCostGp directly.
    /// </summary>
    private static int GetAbilityCost(EnchantmentStats ench, int currentBonusTotal, ItemType type)
    {
        if (ench.FlatCostGp > 0)
            return ench.FlatCostGp;

        if (ench.BonusEquivalent <= 0) return 0;

        int multiplier = (type == ItemType.Weapon) ? 2000 : 1000;
        int newTotal = currentBonusTotal + ench.BonusEquivalent;
        return (newTotal * newTotal - currentBonusTotal * currentBonusTotal) * multiplier;
    }

    /// <summary>
    /// Estimate the total gold value of an already-enchanted item.
    /// Used internally for budget tracking.
    /// </summary>
    public static int EstimateItemValue(ItemData item)
    {
        if (item == null) return 0;

        int value = item.BasePriceGp;
        int enhancement = item.EnhancementBonus;
        if (enhancement <= 0) return value;

        int effectiveBonus = item.GetEffectiveBonusForPricing();
        int multiplier = (item.Type == ItemType.Weapon) ? 2000 : 1000;
        value += effectiveBonus * effectiveBonus * multiplier;
        value += item.GetEnchantmentFlatCostGp();

        return value;
    }
}
