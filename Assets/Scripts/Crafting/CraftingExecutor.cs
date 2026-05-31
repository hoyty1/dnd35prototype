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
    /// In debug mode, skips cost deductions and prefixes item name with "[DEBUG]".
    /// </summary>
    public static CraftingResult Execute(CraftingProject project, Inventory targetInventory)
    {
        var result = new CraftingResult();
        bool debugMode = CraftingValidator.DebugMode;

        // ============================== PRE-FLIGHT ==============================
        if (project == null || !project.IsValid)
        {
            result.Message = project?.ValidationError ?? "Invalid crafting project.";
            return result;
        }

        if (project.Crafter == null && !debugMode)
        {
            result.Message = "No crafter assigned to project.";
            return result;
        }

        var crafter = project.Crafter;
        var def = project.Definition;

        // ============================== RESOURCE CHECKS (skip in debug) ==============================
        if (!debugMode)
        {
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
        }

        // ============================== DEDUCT COSTS (skip in debug) ==============================
        if (!debugMode)
        {
            if (!crafter.SpendComponentGold(project.GoldCost))
            {
                result.Message = "Failed to deduct gold cost.";
                return result;
            }

            if (!crafter.SpendXP(project.XPCost))
            {
                crafter.ComponentGold += project.GoldCost;
                result.Message = "Failed to deduct XP cost (would lose level). Gold refunded.";
                return result;
            }
        }

        // ============================== CREATE/UPGRADE ITEM ==============================
        ItemData craftedItem = null;

        if (def.IsUpgrade && project.UpgradeTargetItem != null)
        {
            craftedItem = ApplyEnhancementUpgrade(project.UpgradeTargetItem, def);
        }
        else if (def.IsDynamic)
        {
            // Pass full project so metamagic scroll data flows through
            craftedItem = CreateDynamicItem(def, project.ItemCasterLevel, project);
        }
        else
        {
            craftedItem = ItemDatabase.CloneItem(def.ItemId);
        }

        if (craftedItem == null)
        {
            if (!debugMode)
            {
                // Roll back all costs
                crafter.ComponentGold += project.GoldCost;
                crafter.ExperiencePoints += project.XPCost;
            }
            result.Message = $"Failed to create item '{def.DisplayName}'. Costs refunded.";
            Debug.LogError($"[CraftingExecutor] Failed to create item '{def.ItemId}'");
            return result;
        }

        // ============================== DEBUG PREFIX ==============================
        if (debugMode)
        {
            craftedItem.Name = "[DEBUG] " + craftedItem.Name;
        }

        // ============================== ADD TO INVENTORY ==============================
        if (!def.IsUpgrade && targetInventory != null)
        {
            targetInventory.AddItem(craftedItem);
        }

        // ============================== TIME ADVANCEMENT (skip in debug) ==============================
        if (!debugMode)
        {
            AdvanceTime(project.CraftingDays);
        }

        // ============================== SUCCESS ==============================
        result.Success = true;
        result.CraftedItem = craftedItem;
        result.GoldSpent = debugMode ? 0 : project.GoldCost;
        result.XPSpent = debugMode ? 0 : project.XPCost;
        result.DaysElapsed = debugMode ? 0 : project.CraftingDays;

        if (debugMode)
        {
            result.Message = $"[DEBUG] Instantly created {craftedItem.Name} (no cost).";
            Debug.Log($"[CraftingExecutor] 🔧 DEBUG created {craftedItem.Name}");
        }
        else
        {
            string scrollNote = project.ScrollCostGp > 0
                ? $" (includes {project.ScrollCostGp:N0} gp for scrolls)"
                : "";
            result.Message = $"Successfully crafted {craftedItem.Name}! ({project.GoldCost:N0} gp{scrollNote}, {project.XPCost:N0} XP, {project.CraftingDays} day{(project.CraftingDays != 1 ? "s" : "")})";

            Debug.Log($"[CraftingExecutor] ✅ {crafter.CharacterName} crafted {craftedItem.Name}. " +
                $"Gold: -{project.GoldCost} (scrolls: {project.ScrollCostGp}), XP: -{project.XPCost}, Days: {project.CraftingDays}");
        }

        return result;
    }

    // ============================== DYNAMIC ITEM CREATION ==============================

    private static ItemData CreateDynamicItem(CraftableItemDefinition def, int casterLevel)
    {
        return CreateDynamicItem(def, casterLevel, null);
    }

    /// <summary>
    /// Create a dynamic item (scroll/potion/wand) with optional metamagic project data.
    /// </summary>
    private static ItemData CreateDynamicItem(CraftableItemDefinition def, int casterLevel, CraftingProject project)
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
                // Pass metamagic data from project if available
                if (project != null && project.ScrollMetamagicFeats != null && project.ScrollMetamagicFeats.Count > 0)
                    return CreateScroll(spell, casterLevel, project.ScrollMetamagicFeats,
                        project.ScrollEffectiveSpellLevel, project.ScrollSavedDC,
                        project.ScrollHeightenToLevel);
                return CreateScroll(spell, casterLevel);

            case CraftingFeatType.BrewPotion:
                return CreatePotion(spell, casterLevel);

            case CraftingFeatType.CraftWand:
                // Pass metamagic data from project if available
                if (project != null && project.WandMetamagicFeats != null && project.WandMetamagicFeats.Count > 0)
                    return CreateWand(spell, casterLevel, project.WandMetamagicFeats,
                        project.WandEffectiveSpellLevel, project.WandSavedDC,
                        project.WandHeightenToLevel);
                return CreateWand(spell, casterLevel);

            default:
                Debug.LogWarning($"[CraftingExecutor] Dynamic creation not supported for feat type {def.RequiredFeat}.");
                return null;
        }
    }

    private static ItemData CreateScroll(SpellData spell, int casterLevel)
    {
        return CreateScroll(spell, casterLevel, null, 0, 0, -1);
    }

    /// <summary>
    /// Create a scroll with optional metamagic feats, saved DC, and effective spell level.
    /// </summary>
    private static ItemData CreateScroll(SpellData spell, int casterLevel,
        List<MetamagicFeatId> metamagicFeats, int effectiveSpellLevel, int savedDC,
        int heightenToLevel)
    {
        bool isArcane = IsArcaneSpell(spell);
        bool hasMetamagic = metamagicFeats != null && metamagicFeats.Count > 0;
        int baseLevel = spell.SpellLevel;
        int effLevel = hasMetamagic ? effectiveSpellLevel : baseLevel;

        // Build metamagic name prefix (e.g., "Empowered+Maximized")
        string metamagicPrefix = "";
        if (hasMetamagic)
        {
            var adjectives = new List<string>();
            foreach (var mm in metamagicFeats)
                adjectives.Add(MetamagicData.GetAdjective(mm));
            metamagicPrefix = string.Join(" ", adjectives) + " ";
        }

        string scrollName = $"Scroll of {metamagicPrefix}{spell.Name}";
        int marketPrice = CraftingCostCalculator.ScrollMarketPrice(effLevel, casterLevel);

        // Build description with metamagic info
        string desc = $"A spell scroll containing {metamagicPrefix}{spell.Name} (CL {casterLevel}).";
        if (hasMetamagic)
        {
            desc += $"\nMetamagic: {string.Join(", ", metamagicPrefix.Trim())}";
            desc += $"\nBase Spell Level: {baseLevel} → Effective Level: {effLevel}";
        }
        if (savedDC > 0)
            desc += $"\nSave DC: {savedDC} (baked at creation)";
        desc += "\n" + spell.Description;

        // Unified scroll data — single source of truth
        ScrollData scrollData = ScrollData.Create(
            spell, casterLevel, isArcane, marketPrice,
            hasMetamagic ? metamagicFeats : null, effLevel, savedDC, heightenToLevel);

        var scroll = new ItemData
        {
            Id = $"crafted_scroll_{spell.SpellId}_{System.Guid.NewGuid():N}",
            Name = scrollName,
            Description = desc,
            Type = ItemType.Consumable,
            Slot = EquipSlot.None,
            IsScroll = true,
            ScrollType = isArcane ? "Arcane" : "Divine",
            ScrollSpellLevel = baseLevel,
            ScrollEffectiveSpellLevel = effLevel,
            ScrollSavedDC = savedDC,
            ScrollMetamagicFeats = hasMetamagic ? new List<MetamagicFeatId>(metamagicFeats) : null,
            ConsumableEffect = ConsumableEffectType.SpellEffect,
            ConsumableSpellName = spell.SpellId,
            ConsumableMinimumCasterLevel = casterLevel,
            BasePriceGp = marketPrice,
            Scroll = scrollData
        };

        Debug.Log($"[CraftingExecutor] Created scroll: {scroll.Name} (CL {casterLevel}, {scroll.ScrollType}" +
            (hasMetamagic ? $", Eff.Lv {effLevel}, DC {savedDC}" : "") + ")");
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
        return CreateWand(spell, casterLevel, null, 0, 0, -1);
    }

    /// <summary>
    /// Create a wand with optional metamagic feats, saved DC, and effective spell level.
    /// </summary>
    private static ItemData CreateWand(SpellData spell, int casterLevel,
        List<MetamagicFeatId> metamagicFeats, int effectiveSpellLevel,
        int savedDC, int heightenToLevel)
    {
        bool hasMetamagic = metamagicFeats != null && metamagicFeats.Count > 0;
        int effLevel = hasMetamagic && effectiveSpellLevel > 0 ? effectiveSpellLevel : spell.SpellLevel;
        int priceGp = CraftingCostCalculator.WandMarketPrice(effLevel, casterLevel);
        bool isArcane = IsArcaneSpell(spell);

        // Calculate DC using DMG wand formula if not provided
        int dcLevel = (heightenToLevel > spell.SpellLevel) ? heightenToLevel : spell.SpellLevel;
        int dc = savedDC > 0 ? savedDC : (10 + dcLevel + dcLevel / 2);

        // Create unified WandData
        WandData wandData = WandData.Create(
            spell, casterLevel, isArcane, priceGp,
            metamagicFeats, effLevel, dc, heightenToLevel);

        string mmLabel = hasMetamagic ? " (metamagic)" : "";
        var wand = new ItemData
        {
            Id = $"crafted_wand_{spell.SpellId}_{System.Guid.NewGuid():N}",
            Name = $"Wand of {spell.Name}{mmLabel}",
            Description = $"A magic wand of {spell.Name} (CL {casterLevel}, {CraftingConstants.WandMaxCharges} charges). " + spell.Description,
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
            ConsumableEffect = ConsumableEffectType.SpellEffect,
            BasePriceGp = priceGp,
            Wand = wandData,
            IsStackable = false,
            MaxStackSize = 1,
            StackCount = 1
        };

        Debug.Log($"[CraftingExecutor] Created wand: {wand.Name} (CL {casterLevel}, {wand.CurrentCharges} charges, DC {dc})");
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
