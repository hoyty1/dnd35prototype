// ============================================================================
// D&D 3.5e Item Creation Feats - Crafting Executor
// Executes validated crafting projects: deducts costs, creates items
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Executes a validated CraftingProject: deducts gold and XP, advances time,
/// and creates/upgrades the crafted item. Atomic operation — either fully succeeds
/// or fully fails with no partial deductions.
/// </summary>
public static class CraftingExecutor
{
    /// <summary>Result of a crafting execution.</summary>
    public struct CraftingResult
    {
        public bool Success;
        public string Message;
        public ItemData CraftedItem;
        public int GoldSpent;
        public int XPSpent;
        public int DaysElapsed;
    }

    /// <summary>
    /// Execute a validated crafting project. The project must have IsValid == true.
    /// </summary>
    public static CraftingResult Execute(CraftingProject project, Inventory targetInventory)
    {
        var result = new CraftingResult();

        // ============================== PRE-FLIGHT ==============================
        if (project == null || !project.IsValid)
        {
            result.Message = project?.ValidationError ?? "Invalid crafting project.";
            return result;
        }

        if (project.Crafter == null)
        {
            result.Message = "No crafter assigned to project.";
            return result;
        }

        var crafter = project.Crafter;
        var def = project.Definition;

        // ============================== DOUBLE-CHECK RESOURCES ==============================
        // Re-verify resources haven't changed since validation
        if (crafter.ComponentGold < project.GoldCost)
        {
            result.Message = $"Insufficient gold ({crafter.ComponentGold:N0}/{project.GoldCost:N0} gp).";
            return result;
        }

        if (project.XPCost > crafter.MaxSpendableXP())
        {
            result.Message = $"Insufficient spendable XP ({crafter.MaxSpendableXP():N0}/{project.XPCost:N0}).";
            return result;
        }

        // ============================== DEDUCT COSTS ==============================
        // Deduct gold first (less impactful if we need to roll back)
        if (!crafter.SpendComponentGold(project.GoldCost))
        {
            result.Message = "Failed to deduct gold cost.";
            return result;
        }

        if (!crafter.SpendXP(project.XPCost))
        {
            // Roll back gold
            crafter.ComponentGold += project.GoldCost;
            result.Message = "Failed to deduct XP cost (would lose level). Gold refunded.";
            return result;
        }

        // ============================== CREATE/UPGRADE ITEM ==============================
        ItemData craftedItem = null;

        if (def.IsUpgrade && project.UpgradeTargetItem != null)
        {
            // Upgrade existing item's enhancement bonus
            craftedItem = ApplyEnhancementUpgrade(project.UpgradeTargetItem, def);
        }
        else if (def.IsDynamic)
        {
            // Create dynamic item (scroll, potion, wand)
            craftedItem = CreateDynamicItem(def, project.ItemCasterLevel);
        }
        else
        {
            // Clone from database
            craftedItem = ItemDatabase.CloneItem(def.ItemId);
        }

        if (craftedItem == null)
        {
            // Roll back all costs
            crafter.ComponentGold += project.GoldCost;
            crafter.ExperiencePoints += project.XPCost;
            result.Message = $"Failed to create item '{def.DisplayName}'. Costs refunded.";
            Debug.LogError($"[CraftingExecutor] Failed to create item '{def.ItemId}'");
            return result;
        }

        // ============================== ADD TO INVENTORY ==============================
        if (!def.IsUpgrade && targetInventory != null)
        {
            targetInventory.AddItem(craftedItem);
        }

        // ============================== TIME ADVANCEMENT ==============================
        AdvanceTime(project.CraftingDays);

        // ============================== SUCCESS ==============================
        result.Success = true;
        result.CraftedItem = craftedItem;
        result.GoldSpent = project.GoldCost;
        result.XPSpent = project.XPCost;
        result.DaysElapsed = project.CraftingDays;
        result.Message = $"Successfully crafted {craftedItem.Name}! ({project.GoldCost:N0} gp, {project.XPCost:N0} XP, {project.CraftingDays} day{(project.CraftingDays != 1 ? "s" : "")})";

        Debug.Log($"[CraftingExecutor] ✅ {crafter.CharacterName} crafted {craftedItem.Name}. " +
            $"Gold: -{project.GoldCost}, XP: -{project.XPCost}, Days: {project.CraftingDays}");

        return result;
    }

    // ============================== DYNAMIC ITEM CREATION ==============================

    private static ItemData CreateDynamicItem(CraftableItemDefinition def, int casterLevel)
    {
        if (string.IsNullOrEmpty(def.DynamicSpellId)) return null;

        var spell = SpellDatabase.GetSpell(def.DynamicSpellId);
        if (spell == null)
        {
            Debug.LogWarning($"[CraftingExecutor] Spell '{def.DynamicSpellId}' not found for dynamic item creation.");
            return null;
        }

        switch (def.RequiredFeat)
        {
            case CraftingFeatType.ScribeScroll:
                return CreateScroll(spell, casterLevel);

            case CraftingFeatType.BrewPotion:
                return CreatePotion(spell, casterLevel);

            case CraftingFeatType.CraftWand:
                return CreateWand(spell, casterLevel);

            default:
                Debug.LogWarning($"[CraftingExecutor] Dynamic creation not supported for feat type {def.RequiredFeat}.");
                return null;
        }
    }

    private static ItemData CreateScroll(SpellData spell, int casterLevel)
    {
        bool isArcane = IsArcaneSpell(spell);
        var scroll = new ItemData
        {
            Id = $"crafted_scroll_{spell.SpellId}_{System.Guid.NewGuid():N}",
            Name = $"Scroll of {spell.Name}",
            Description = $"A spell scroll containing {spell.Name} (CL {casterLevel}). " + spell.Description,
            Type = ItemType.Consumable,
            Slot = EquipSlot.None,
            IsScroll = true,
            ScrollType = isArcane ? "Arcane" : "Divine",
            ScrollSpellLevel = spell.SpellLevel,
            ConsumableSpellName = spell.SpellId,
            ConsumableMinimumCasterLevel = casterLevel,
            BasePriceGp = CraftingCostCalculator.ScrollMarketPrice(spell.SpellLevel, casterLevel)
        };

        Debug.Log($"[CraftingExecutor] Created scroll: {scroll.Name} (CL {casterLevel}, {scroll.ScrollType})");
        return scroll;
    }

    private static ItemData CreatePotion(SpellData spell, int casterLevel)
    {
        var potion = new ItemData
        {
            Id = $"crafted_potion_{spell.SpellId}_{System.Guid.NewGuid():N}",
            Name = $"Potion of {spell.Name}",
            Description = $"A magic potion of {spell.Name} (CL {casterLevel}). " + spell.Description,
            Type = ItemType.Consumable,
            Slot = EquipSlot.None,
            IsPotion = true,
            PotionSpellLevel = spell.SpellLevel,
            ConsumableSpellName = spell.SpellId,
            ConsumableMinimumCasterLevel = casterLevel,
            BasePriceGp = CraftingCostCalculator.PotionMarketPrice(spell.SpellLevel, casterLevel)
        };

        Debug.Log($"[CraftingExecutor] Created potion: {potion.Name} (CL {casterLevel})");
        return potion;
    }

    private static ItemData CreateWand(SpellData spell, int casterLevel)
    {
        var wand = new ItemData
        {
            Id = $"crafted_wand_{spell.SpellId}_{System.Guid.NewGuid():N}",
            Name = $"Wand of {spell.Name}",
            Description = $"A magic wand of {spell.Name} (CL {casterLevel}, 50 charges). " + spell.Description,
            Type = ItemType.Consumable,
            Slot = EquipSlot.None,
            IsWand = true,
            WandSpellId = spell.SpellId,
            WandCasterLevel = casterLevel,
            WandSpellLevel = spell.SpellLevel,
            CurrentCharges = CraftingConstants.WandMaxCharges,
            MaxCharges = CraftingConstants.WandMaxCharges,
            ConsumableSpellName = spell.SpellId,
            ConsumableMinimumCasterLevel = casterLevel,
            BasePriceGp = CraftingCostCalculator.WandMarketPrice(spell.SpellLevel, casterLevel)
        };

        Debug.Log($"[CraftingExecutor] Created wand: {wand.Name} (CL {casterLevel}, {wand.CurrentCharges} charges)");
        return wand;
    }

    // ============================== ENHANCEMENT UPGRADE ==============================

    private static ItemData ApplyEnhancementUpgrade(ItemData item, CraftableItemDefinition def)
    {
        int targetBonus = def.EnhancementTier;
        int oldBonus = item.ResolveEnhancementBonus();

        item.EnhancementBonus = targetBonus;
        item.enhancementBonus = targetBonus;
        item.CountsAsMagicForBypass = true;
        item.IsMasterwork = true; // Enhanced items are always masterwork

        // Update name to reflect new enhancement
        string baseName = item.Name;
        // Remove old enhancement prefix if present
        if (baseName.StartsWith("+"))
        {
            int spaceIdx = baseName.IndexOf(' ');
            if (spaceIdx > 0)
                baseName = baseName.Substring(spaceIdx + 1);
        }
        else if (baseName.StartsWith("Masterwork "))
        {
            baseName = baseName.Substring("Masterwork ".Length);
        }

        item.Name = $"+{targetBonus} {baseName}";

        // Recalculate price
        if (def.IsWeaponEnhancement)
            item.BasePriceGp = CraftingCostCalculator.WeaponEnhancementMarketPrice(targetBonus);
        else
            item.BasePriceGp = CraftingCostCalculator.ArmorEnhancementMarketPrice(targetBonus);

        Debug.Log($"[CraftingExecutor] Enhanced item: +{oldBonus} → +{targetBonus} {baseName}");
        return item;
    }

    // ============================== TIME ADVANCEMENT ==============================

    private static void AdvanceTime(int days)
    {
        // Time advancement is abstracted. In a full implementation this would
        // integrate with a calendar/rest system. For now, we log the elapsed time
        // and any consumers (e.g., daily-use items) can query the advancement.
        if (days > 0)
        {
            Debug.Log($"[CraftingExecutor] ⏰ {days} day{(days != 1 ? "s" : "")} of crafting time elapsed.");

            // Signal time advancement to any registered listeners
            CraftingTimeTracker.AdvanceDays(days);
        }
    }

    // ============================== HELPERS ==============================

    private static bool IsArcaneSpell(SpellData spell)
    {
        if (spell == null || spell.AvailableFor == null) return true; // Default to arcane

        string[] arcaneCasters = { "Wizard", "Sorcerer", "Bard" };
        foreach (var avail in spell.AvailableFor)
        {
            if (avail == null) continue;
            foreach (string cls in arcaneCasters)
            {
                if (avail.MatchesClass(cls))
                    return true;
            }
        }

        return false;
    }
}
