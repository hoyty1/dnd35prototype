// ============================================================================
// D&D 3.5e Item Creation Feats - Crafting Validator
// Full prerequisite validation pipeline per DMG p.282-285
// Supports party-wide spell checking and scroll substitution.
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Validates whether a character can craft a specific item, checking:
/// 1. Required feat ownership
/// 2. Caster level requirement
/// 3. Spell prerequisites — checks entire party, with scroll substitution option
/// 4. Gold and XP affordability (including scroll costs if enabled)
/// 5. XP level-loss prevention
/// </summary>
public static class CraftingValidator
{
    // ============================== DEBUG MODE ==============================

    /// <summary>
    /// When true, bypasses feat, caster level, spell, gold, and XP requirements.
    /// Persists for the session (static field). Toggled from CraftingWorkshopUI.
    /// </summary>
    public static bool DebugMode { get; set; }

    // ============================== PARTY-WIDE SPELL CHECKING ==============================

    /// <summary>
    /// Analyze spell availability for a crafting project across the entire party.
    /// Checks crafter first, then each party member, and calculates scroll costs for missing spells.
    /// </summary>
    /// <param name="requiredSpellIds">Spell IDs required by the item.</param>
    /// <param name="crafter">The character performing the crafting.</param>
    /// <param name="crafterSpellComp">The crafter's spellcasting component (may be null).</param>
    /// <param name="partyMembers">All party member CharacterControllers (may be null or empty).</param>
    /// <param name="useScrolls">If true, missing spells are set to ScrollSubstitute instead of Missing.</param>
    /// <returns>SpellAvailabilityInfo with per-spell source details.</returns>
    public static SpellAvailabilityInfo CheckSpellSources(
        List<string> requiredSpellIds,
        CharacterStats crafter,
        SpellcastingComponent crafterSpellComp,
        List<CharacterController> partyMembers,
        bool useScrolls = false)
    {
        var info = new SpellAvailabilityInfo();
        if (requiredSpellIds == null || requiredSpellIds.Count == 0)
            return info;

        // Build crafter's known spell set
        var crafterSpells = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        if (crafterSpellComp != null)
        {
            foreach (string s in crafterSpellComp.GetAllKnownSpells())
            {
                if (!string.IsNullOrEmpty(s))
                    crafterSpells.Add(s);
            }
        }

        // Build per-party-member spell sets (excluding crafter to avoid double-counting)
        var partySpellSets = new List<(string Name, HashSet<string> Spells)>();
        if (partyMembers != null)
        {
            foreach (var member in partyMembers)
            {
                if (member == null || member.Stats == null) continue;
                // Skip the crafter themselves
                if (crafter != null && member.Stats == crafter) continue;

                var sc = member.Spellcasting;
                if (sc == null) continue;

                var spells = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                foreach (string s in sc.GetAllKnownSpells())
                {
                    if (!string.IsNullOrEmpty(s))
                        spells.Add(s);
                }

                if (spells.Count > 0)
                    partySpellSets.Add((member.Stats.CharacterName, spells));
            }
        }

        // Analyze each required spell
        foreach (string spellId in requiredSpellIds)
        {
            if (string.IsNullOrEmpty(spellId)) continue;

            var spell = SpellDatabase.GetSpell(spellId);
            string spellName = spell != null ? spell.Name : spellId;
            int spellLevel = spell != null ? spell.SpellLevel : 1;
            int minCL = CraftingCostCalculator.MinimumCasterLevelForSpell(spellLevel);
            int scrollCost = CraftingCostCalculator.ScrollMarketPrice(spellLevel, minCL);

            var source = new SpellSource
            {
                SpellId = spellId,
                SpellName = spellName,
                SpellLevel = spellLevel,
                ScrollCostGp = scrollCost
            };

            // 1. Check crafter first
            if (crafterSpells.Contains(spellId))
            {
                source.SourceType = SpellSourceType.CrafterKnown;
                source.ProviderName = crafter?.CharacterName ?? "You";
            }
            // 2. Check party members
            else
            {
                string provider = null;
                foreach (var (name, spells) in partySpellSets)
                {
                    if (spells.Contains(spellId))
                    {
                        provider = name;
                        break;
                    }
                }

                if (provider != null)
                {
                    source.SourceType = SpellSourceType.PartyMemberKnown;
                    source.ProviderName = provider;
                }
                // 3. Mark as scroll substitute or missing
                else if (useScrolls)
                {
                    source.SourceType = SpellSourceType.ScrollSubstitute;
                }
                else
                {
                    source.SourceType = SpellSourceType.Missing;
                }
            }

            info.Sources.Add(source);
        }

        return info;
    }

    // ============================== MAIN VALIDATION ==============================

    /// <summary>
    /// Validate a complete crafting project with party-wide spell checking.
    /// This is the primary validation entry point used by the UI.
    /// </summary>
    public static CraftingProject Validate(
        CraftableItemDefinition definition,
        CharacterStats crafter,
        SpellcastingComponent spellComp,
        ItemData upgradeTarget = null,
        List<CharacterController> partyMembers = null,
        bool useScrollsForMissing = false)
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

        // ============================== DEBUG MODE — BYPASS ALL CHECKS ==============================
        if (DebugMode)
        {
            // Calculate costs for display, but mark everything valid with zero costs
            CraftingCostCalculator.CraftingCost debugCost;
            if (definition.IsUpgrade && upgradeTarget != null)
            {
                int curBonus = upgradeTarget.ResolveEnhancementBonus();
                int tgtBonus = definition.EnhancementTier;
                debugCost = definition.IsWeaponEnhancement
                    ? CraftingCostCalculator.ForWeaponUpgrade(curBonus, tgtBonus)
                    : CraftingCostCalculator.ForArmorUpgrade(curBonus, tgtBonus);
            }
            else
            {
                debugCost = CraftingCostCalculator.FromMarketPrice(definition.MarketPriceGp);
            }

            project.GoldCost = 0;
            project.XPCost = 0;
            project.CraftingDays = 0;
            project.MarketPriceGp = debugCost.MarketPriceGp;
            project.ItemCasterLevel = definition.RequiredCasterLevel;
            project.IsValid = true;
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

        // ============================== 3. CALCULATE BASE COSTS ==============================
        CraftingCostCalculator.CraftingCost cost;

        if (definition.IsUpgrade && upgradeTarget != null)
        {
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

        // ============================== 4. SPELL PREREQUISITES (PARTY-WIDE) ==============================
        if (definition.RequiredSpellIds != null && definition.RequiredSpellIds.Count > 0)
        {
            project.SpellSources = CheckSpellSources(
                definition.RequiredSpellIds, crafter, spellComp,
                partyMembers, useScrollsForMissing);

            // Add scroll costs to gold cost
            project.ScrollCostGp = project.SpellSources.TotalScrollCostGp;
            project.GoldCost += project.ScrollCostGp;

            // Track truly missing spells (not covered by anyone or scroll)
            project.MissingSpells = project.SpellSources.TrulyMissingSpells
                .Select(s => s.SpellId).ToList();

            // Spellcraft DC from truly missing spells only
            project.SpellcraftDC = project.SpellSources.SpellcraftDC;
        }

        // ============================== 5. GOLD CHECK (includes scroll costs) ==============================
        if (crafter.ComponentGold < project.GoldCost)
        {
            project.IsValid = false;
            project.ValidationError = $"Insufficient gold. Need {project.GoldCost:N0} gp" +
                (project.ScrollCostGp > 0 ? $" (includes {project.ScrollCostGp:N0} gp scrolls)" : "") +
                $", have {crafter.ComponentGold:N0} gp.";
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
    /// In debug mode, returns ALL crafting feat types regardless of character feats.
    /// </summary>
    public static List<CraftingFeatType> GetCraftingFeats(CharacterStats crafter)
    {
        var result = new List<CraftingFeatType>();

        if (DebugMode)
        {
            // Return every crafting feat type so the UI shows all categories
            foreach (CraftingFeatType feat in System.Enum.GetValues(typeof(CraftingFeatType)))
                result.Add(feat);
            return result;
        }

        if (crafter == null) return result;

        foreach (var kvp in CraftingConstants.FeatNames)
        {
            if (crafter.HasFeat(kvp.Value))
                result.Add(kvp.Key);
        }

        return result;
    }
}
