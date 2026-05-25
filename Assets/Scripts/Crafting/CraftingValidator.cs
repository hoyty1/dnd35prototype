// ============================================================================
// D&D 3.5e Item Creation Feats - Crafting Validator
// Full prerequisite validation pipeline per DMG p.282-285
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Validates whether a character can craft a specific item, checking:
/// 1. Required feat ownership
/// 2. Caster level requirement
/// 3. Spell prerequisites (with +5 DC substitution per missing spell)
/// 4. Gold and XP affordability
/// 5. XP level-loss prevention
/// </summary>
public static class CraftingValidator
{
    /// <summary>
    /// Validate a complete crafting project. Returns a CraftingProject with IsValid set
    /// and any validation errors described.
    /// </summary>
    public static CraftingProject Validate(
        CraftableItemDefinition definition,
        CharacterStats crafter,
        SpellcastingComponent spellComp,
        ItemData upgradeTarget = null)
    {
        var project = new CraftingProject
        {
            Definition = definition,
            Crafter = crafter,
            UpgradeTargetItem = upgradeTarget,
            IsValid = true
        };

        if (definition == null)
        {
            project.IsValid = false;
            project.ValidationError = "No item definition provided.";
            return project;
        }

        if (crafter == null)
        {
            project.IsValid = false;
            project.ValidationError = "No crafter character provided.";
            return project;
        }

        // ============================== 1. FEAT CHECK ==============================
        string featName = CraftingConstants.GetFeatName(definition.RequiredFeat);
        if (!crafter.HasFeat(featName))
        {
            project.IsValid = false;
            project.ValidationError = $"Missing required feat: {featName}";
            return project;
        }

        // ============================== 2. CASTER LEVEL CHECK ==============================
        int crafterCL = GetCrafterCasterLevel(crafter);
        if (crafterCL < definition.RequiredCasterLevel)
        {
            project.IsValid = false;
            project.ValidationError = $"Caster level {crafterCL} is below minimum {definition.RequiredCasterLevel} for this item.";
            return project;
        }

        // ============================== 3. CALCULATE COSTS ==============================
        CraftingCostCalculator.CraftingCost cost;

        if (definition.IsUpgrade && upgradeTarget != null)
        {
            // For upgrades, calculate incremental cost
            int currentBonus = upgradeTarget.ResolveEnhancementBonus();
            int targetBonus = definition.EnhancementTier;

            if (targetBonus <= currentBonus)
            {
                project.IsValid = false;
                project.ValidationError = $"Item already has +{currentBonus} enhancement (target is +{targetBonus}).";
                return project;
            }

            if (definition.IsWeaponEnhancement)
                cost = CraftingCostCalculator.ForWeaponUpgrade(currentBonus, targetBonus);
            else
                cost = CraftingCostCalculator.ForArmorUpgrade(currentBonus, targetBonus);
        }
        else
        {
            cost = CraftingCostCalculator.FromMarketPrice(definition.MarketPriceGp);
        }

        project.GoldCost = cost.GoldCost;
        project.XPCost = cost.XPCost;
        project.CraftingDays = cost.CraftingDays;
        project.MarketPriceGp = cost.MarketPriceGp;
        project.ItemCasterLevel = definition.RequiredCasterLevel;

        // ============================== 4. SPELL PREREQUISITES ==============================
        if (definition.RequiredSpellIds != null && definition.RequiredSpellIds.Count > 0 && spellComp != null)
        {
            var knownSpells = new HashSet<string>(
                spellComp.GetAllKnownSpells().Where(s => !string.IsNullOrEmpty(s)),
                System.StringComparer.OrdinalIgnoreCase);

            foreach (string reqSpell in definition.RequiredSpellIds)
            {
                if (!string.IsNullOrEmpty(reqSpell) && !knownSpells.Contains(reqSpell))
                {
                    project.MissingSpells.Add(reqSpell);
                }
            }

            if (project.MissingSpells.Count > 0)
            {
                // DMG p.282: +5 DC per missing spell prerequisite
                project.SpellcraftDC = CraftingConstants.BaseCraftingDC
                    + (project.MissingSpells.Count * CraftingConstants.MissingSpellDCIncrease);
            }
        }

        // ============================== 5. GOLD CHECK ==============================
        if (crafter.ComponentGold < project.GoldCost)
        {
            project.IsValid = false;
            project.ValidationError = $"Insufficient gold. Need {project.GoldCost:N0} gp, have {crafter.ComponentGold:N0} gp.";
            return project;
        }

        // ============================== 6. XP CHECK (with level-loss prevention) ==============================
        if (project.XPCost > crafter.MaxSpendableXP())
        {
            int currentLevelMinXP = ExperienceCalculator.GetXPForLevel(crafter.Level);
            project.IsValid = false;
            project.ValidationError = $"Insufficient XP. Need {project.XPCost:N0} XP, but can only spend {crafter.MaxSpendableXP():N0} XP without dropping below level {crafter.Level} (floor: {currentLevelMinXP:N0} XP).";
            return project;
        }

        // ============================== 7. UPGRADE TARGET VALIDATION ==============================
        if (definition.IsUpgrade)
        {
            if (upgradeTarget == null)
            {
                project.IsValid = false;
                project.ValidationError = "Enhancement upgrade requires selecting a target item from inventory.";
                return project;
            }

            if (!upgradeTarget.IsMasterwork && upgradeTarget.ResolveEnhancementBonus() <= 0)
            {
                project.IsValid = false;
                project.ValidationError = "Only masterwork items can be enhanced. The selected item is not masterwork.";
                return project;
            }
        }

        return project;
    }

    /// <summary>
    /// Quick check: can this character craft anything at all? (Has at least one item creation feat)
    /// </summary>
    public static bool HasAnyCraftingFeat(CharacterStats crafter)
    {
        if (crafter == null) return false;
        foreach (var kvp in CraftingConstants.FeatNames)
        {
            if (crafter.HasFeat(kvp.Value))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Get the crafter's effective caster level (highest across all casting classes).
    /// Uses the same logic as FeatPrerequisite.IsMet for CasterLevel type.
    /// </summary>
    public static int GetCrafterCasterLevel(CharacterStats stats)
    {
        if (stats == null) return 0;

        int best = 0;

        // Full casters: Wizard, Sorcerer, Cleric, Druid, Bard — CL = class level
        string[] fullCasters = { "Wizard", "Sorcerer", "Cleric", "Druid", "Bard" };
        foreach (string cls in fullCasters)
        {
            int classLevel = stats.GetClassLevel(cls);
            if (classLevel > best)
                best = classLevel;
        }

        // Half casters: Ranger, Paladin — CL = class level - 3 (minimum 1 if they have levels)
        string[] halfCasters = { "Ranger", "Paladin" };
        foreach (string cls in halfCasters)
        {
            int classLevel = stats.GetClassLevel(cls);
            if (classLevel >= 4)
            {
                int cl = classLevel - 3;
                if (cl > best)
                    best = cl;
            }
        }

        return best;
    }

    /// <summary>
    /// Get all crafting feats the character has, as CraftingFeatType values.
    /// </summary>
    public static List<CraftingFeatType> GetCraftingFeats(CharacterStats crafter)
    {
        var result = new List<CraftingFeatType>();
        if (crafter == null) return result;

        foreach (var kvp in CraftingConstants.FeatNames)
        {
            if (crafter.HasFeat(kvp.Value))
                result.Add(kvp.Key);
        }

        return result;
    }
}
