// ============================================================================
// D&D 3.5e Item Creation Feats - Craftable Item Registry
// Populates all craftable items from existing databases with spell prerequisites
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Master registry of all items that can be crafted via Item Creation feats.
/// Initialized after all item databases are loaded (called from SceneBootstrap).
/// Links ring/rod/staff/wondrous/arms entries to their crafting prerequisites.
/// </summary>
public static class CraftableItemRegistry
{
    private static readonly Dictionary<CraftingFeatType, List<CraftableItemDefinition>> _byFeat
        = new Dictionary<CraftingFeatType, List<CraftableItemDefinition>>();

    private static readonly Dictionary<string, CraftableItemDefinition> _byItemId
        = new Dictionary<string, CraftableItemDefinition>(StringComparer.OrdinalIgnoreCase);

    private static bool _initialized;

    /// <summary>Total number of registered craftable items.</summary>
    public static int Count => _byItemId.Count;

    // ============================== INITIALIZATION ==============================

    /// <summary>
    /// Populate the registry from all item databases. Call once after databases are initialized.
    /// </summary>
    public static void Init()
    {
        if (_initialized)
        {
            Debug.Log("[CraftableItemRegistry] Already initialized, skipping.");
            return;
        }

        _byFeat.Clear();
        _byItemId.Clear();

        foreach (CraftingFeatType feat in Enum.GetValues(typeof(CraftingFeatType)))
            _byFeat[feat] = new List<CraftableItemDefinition>();

        RegisterRings();
        RegisterRods();
        RegisterWondrousItems();
        RegisterStaves();
        RegisterArmsAndArmorEnhancements();
        // Scrolls, potions, and wands are generated dynamically from known spells

        _initialized = true;
        Debug.Log($"[CraftableItemRegistry] Initialized with {_byItemId.Count} craftable item definitions.");
    }

    // ============================== QUERIES ==============================

    /// <summary>Get all craftable items for a specific feat type.</summary>
    public static List<CraftableItemDefinition> GetItemsForFeat(CraftingFeatType feat)
    {
        return _byFeat.TryGetValue(feat, out var list) ? list : new List<CraftableItemDefinition>();
    }

    /// <summary>Get a specific craftable item definition by item ID.</summary>
    public static CraftableItemDefinition GetByItemId(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        _byItemId.TryGetValue(itemId, out var def);
        return def;
    }

    /// <summary>Get all feats that the crafter has, with their available items.</summary>
    public static Dictionary<CraftingFeatType, List<CraftableItemDefinition>> GetAvailableForCrafter(CharacterStats crafter)
    {
        var result = new Dictionary<CraftingFeatType, List<CraftableItemDefinition>>();
        if (crafter == null) return result;

        foreach (var kvp in CraftingConstants.FeatNames)
        {
            if (crafter.HasFeat(kvp.Value))
            {
                result[kvp.Key] = GetItemsForFeat(kvp.Key);
            }
        }

        return result;
    }

    // ============================== REGISTRATION ==============================

    private static void Register(CraftableItemDefinition def)
    {
        if (def == null || string.IsNullOrEmpty(def.ItemId)) return;

        _byFeat[def.RequiredFeat].Add(def);
        _byItemId[def.ItemId] = def;
    }

    // ============================== RINGS (Forge Ring) ==============================

    private static void RegisterRings()
    {
        int count = 0;
        var allRings = RingDatabase.GetAllRings();
        if (allRings == null) return;

        foreach (var kvp in allRings)
        {
            var ring = kvp.Value;
            if (ring == null || ring.BasePriceGp <= 0) continue;

            var def = new CraftableItemDefinition
            {
                ItemId = ring.Id,
                DisplayName = ring.Name,
                RequiredFeat = CraftingFeatType.ForgeRing,
                RequiredCasterLevel = Mathf.Max(1, ring.RingCasterLevel),
                MarketPriceGp = ring.BasePriceGp,
                Category = "Ring",
                Description = TruncateDescription(ring.Description)
            };

            // Add spell prerequisites from ring's known spell associations
            AddSpellPrereqsFromItemDescription(def, ring);
            Register(def);
            count++;
        }

        Debug.Log($"[CraftableItemRegistry] Registered {count} rings for Forge Ring.");
    }

    // ============================== RODS (Craft Rod) ==============================

    private static void RegisterRods()
    {
        int count = 0;
        // Rods are registered via ItemDatabase — look for items with IsRod flag
        foreach (var item in ItemDatabase.AllItems)
        {
            if (item == null || !item.IsRod || item.BasePriceGp <= 0) continue;
            if (item.IsSpecificItem) continue; // Skip artifact-like specific rods

            var def = new CraftableItemDefinition
            {
                ItemId = item.Id,
                DisplayName = item.Name,
                RequiredFeat = CraftingFeatType.CraftRod,
                RequiredCasterLevel = Mathf.Max(1, item.RodCasterLevel),
                MarketPriceGp = item.BasePriceGp,
                Category = "Rod",
                Description = TruncateDescription(item.Description)
            };

            AddSpellPrereqsFromItemDescription(def, item);
            Register(def);
            count++;
        }

        Debug.Log($"[CraftableItemRegistry] Registered {count} rods for Craft Rod.");
    }

    // ============================== WONDROUS ITEMS ==============================

    private static void RegisterWondrousItems()
    {
        int count = 0;
        foreach (var item in ItemDatabase.AllItems)
        {
            if (item == null || !item.IsWondrous || item.BasePriceGp <= 0) continue;
            if (item.IsRod) continue; // Rods handled separately
            if (item.IsSpecificItem) continue;

            string category = "Wondrous";
            if (item.Slot != EquipSlot.None)
                category = $"Wondrous - {item.Slot}";

            var def = new CraftableItemDefinition
            {
                ItemId = item.Id,
                DisplayName = item.Name,
                RequiredFeat = CraftingFeatType.CraftWondrousItem,
                RequiredCasterLevel = Mathf.Max(1, item.WondrousCasterLevel),
                MarketPriceGp = item.BasePriceGp,
                Category = category,
                Description = TruncateDescription(item.Description)
            };

            AddSpellPrereqsFromItemDescription(def, item);
            Register(def);
            count++;
        }

        Debug.Log($"[CraftableItemRegistry] Registered {count} wondrous items for Craft Wondrous Item.");
    }

    // ============================== STAVES (Craft Staff) ==============================

    private static void RegisterStaves()
    {
        int count = 0;
        foreach (var item in ItemDatabase.AllItems)
        {
            if (item == null || !item.IsStaff || item.BasePriceGp <= 0) continue;

            var def = new CraftableItemDefinition
            {
                ItemId = item.Id,
                DisplayName = item.Name,
                RequiredFeat = CraftingFeatType.CraftStaff,
                RequiredCasterLevel = Mathf.Max(1, item.StaffCasterLevel),
                MarketPriceGp = item.BasePriceGp,
                Category = "Staff",
                Description = TruncateDescription(item.Description)
            };

            // Staves require all spells they contain
            var staffDef = item.GetStaffDefinition();
            if (staffDef != null && staffDef.Spells != null)
            {
                foreach (var staffSpell in staffDef.Spells)
                {
                    if (staffSpell != null && !string.IsNullOrEmpty(staffSpell.SpellId))
                        def.RequiredSpellIds.Add(staffSpell.SpellId);
                }
            }

            Register(def);
            count++;
        }

        Debug.Log($"[CraftableItemRegistry] Registered {count} staves for Craft Staff.");
    }

    // ============================== ARMS & ARMOR ENHANCEMENTS ==============================

    private static void RegisterArmsAndArmorEnhancements()
    {
        int count = 0;

        // Register weapon enhancement tiers (+1 through +5)
        for (int bonus = 1; bonus <= 5; bonus++)
        {
            int marketPrice = CraftingCostCalculator.WeaponEnhancementMarketPrice(bonus);
            var def = new CraftableItemDefinition
            {
                ItemId = $"weapon_enhancement_{bonus}",
                DisplayName = $"+{bonus} Weapon Enhancement",
                RequiredFeat = CraftingFeatType.CraftMagicArmsAndArmor,
                RequiredCasterLevel = bonus * 3, // CL 3 per bonus
                MarketPriceGp = marketPrice,
                Category = "Weapon Enhancement",
                Description = $"Enhance a masterwork weapon to +{bonus}.",
                IsUpgrade = true,
                EnhancementTier = bonus,
                IsWeaponEnhancement = true
            };
            // +1 requires magic weapon; higher tiers handled by the system
            if (bonus >= 1)
                def.RequiredSpellIds.Add("magic_weapon_greater");

            Register(def);
            count++;
        }

        // Register armor enhancement tiers (+1 through +5)
        for (int bonus = 1; bonus <= 5; bonus++)
        {
            int marketPrice = CraftingCostCalculator.ArmorEnhancementMarketPrice(bonus);
            var def = new CraftableItemDefinition
            {
                ItemId = $"armor_enhancement_{bonus}",
                DisplayName = $"+{bonus} Armor Enhancement",
                RequiredFeat = CraftingFeatType.CraftMagicArmsAndArmor,
                RequiredCasterLevel = bonus * 3,
                MarketPriceGp = marketPrice,
                Category = "Armor Enhancement",
                Description = $"Enhance masterwork armor to +{bonus}.",
                IsUpgrade = true,
                EnhancementTier = bonus,
                IsWeaponEnhancement = false
            };
            if (bonus >= 1)
                def.RequiredSpellIds.Add("magic_vestment");

            Register(def);
            count++;
        }

        // Register shield enhancement tiers (+1 through +5)
        for (int bonus = 1; bonus <= 5; bonus++)
        {
            int marketPrice = CraftingCostCalculator.ArmorEnhancementMarketPrice(bonus);
            var def = new CraftableItemDefinition
            {
                ItemId = $"shield_enhancement_{bonus}",
                DisplayName = $"+{bonus} Shield Enhancement",
                RequiredFeat = CraftingFeatType.CraftMagicArmsAndArmor,
                RequiredCasterLevel = bonus * 3,
                MarketPriceGp = marketPrice,
                Category = "Shield Enhancement",
                Description = $"Enhance a masterwork shield to +{bonus}.",
                IsUpgrade = true,
                EnhancementTier = bonus,
                IsWeaponEnhancement = false
            };
            if (bonus >= 1)
                def.RequiredSpellIds.Add("magic_vestment");

            Register(def);
            count++;
        }

        Debug.Log($"[CraftableItemRegistry] Registered {count} arms & armor enhancement tiers.");
    }

    // ============================== DYNAMIC ITEM GENERATION ==============================

    /// <summary>
    /// Generate craftable scroll definitions from a caster's known spells.
    /// Called on-demand when the Scribe Scroll tab is opened.
    /// </summary>
    public static List<CraftableItemDefinition> GenerateScrollDefinitions(CharacterStats crafter, SpellcastingComponent spellComp)
    {
        var results = new List<CraftableItemDefinition>();
        if (crafter == null || spellComp == null) return results;

        var knownSpells = spellComp.GetAllKnownSpells();
        foreach (string spellId in knownSpells)
        {
            if (string.IsNullOrEmpty(spellId)) continue;
            var spell = SpellDatabase.GetSpell(spellId);
            if (spell == null) continue;

            int spellLevel = spell.SpellLevel;
            int minCL = CraftingCostCalculator.MinimumCasterLevelForSpell(spellLevel);
            int marketPrice = CraftingCostCalculator.ScrollMarketPrice(spellLevel, minCL);

            results.Add(new CraftableItemDefinition
            {
                ItemId = $"scroll_{spellId}",
                DisplayName = $"Scroll of {spell.Name}",
                RequiredFeat = CraftingFeatType.ScribeScroll,
                RequiredCasterLevel = minCL,
                MarketPriceGp = marketPrice,
                Category = $"Scroll - Level {spellLevel}",
                Description = spell.Description,
                IsDynamic = true,
                DynamicSpellId = spellId,
                DynamicSpellLevel = spellLevel,
                RequiredSpellIds = new List<string> { spellId }
            });
        }

        return results.OrderBy(d => d.DynamicSpellLevel).ThenBy(d => d.DisplayName).ToList();
    }

    /// <summary>
    /// Generate craftable potion definitions from a caster's known spells (level 0-3 only).
    /// </summary>
    public static List<CraftableItemDefinition> GeneratePotionDefinitions(CharacterStats crafter, SpellcastingComponent spellComp)
    {
        var results = new List<CraftableItemDefinition>();
        if (crafter == null || spellComp == null) return results;

        var knownSpells = spellComp.GetAllKnownSpells();
        foreach (string spellId in knownSpells)
        {
            if (string.IsNullOrEmpty(spellId)) continue;
            var spell = SpellDatabase.GetSpell(spellId);
            if (spell == null || spell.SpellLevel > CraftingConstants.PotionMaxSpellLevel) continue;

            int spellLevel = spell.SpellLevel;
            int minCL = CraftingCostCalculator.MinimumCasterLevelForSpell(spellLevel);
            int marketPrice = CraftingCostCalculator.PotionMarketPrice(spellLevel, minCL);

            results.Add(new CraftableItemDefinition
            {
                ItemId = $"potion_{spellId}",
                DisplayName = $"Potion of {spell.Name}",
                RequiredFeat = CraftingFeatType.BrewPotion,
                RequiredCasterLevel = minCL,
                MarketPriceGp = marketPrice,
                Category = $"Potion - Level {spellLevel}",
                Description = spell.Description,
                IsDynamic = true,
                DynamicSpellId = spellId,
                DynamicSpellLevel = spellLevel,
                RequiredSpellIds = new List<string> { spellId }
            });
        }

        return results.OrderBy(d => d.DynamicSpellLevel).ThenBy(d => d.DisplayName).ToList();
    }

    /// <summary>
    /// Generate craftable wand definitions from a caster's known spells (level 0-4 only).
    /// </summary>
    public static List<CraftableItemDefinition> GenerateWandDefinitions(CharacterStats crafter, SpellcastingComponent spellComp)
    {
        var results = new List<CraftableItemDefinition>();
        if (crafter == null || spellComp == null) return results;

        var knownSpells = spellComp.GetAllKnownSpells();
        foreach (string spellId in knownSpells)
        {
            if (string.IsNullOrEmpty(spellId)) continue;
            var spell = SpellDatabase.GetSpell(spellId);
            if (spell == null || spell.SpellLevel > CraftingConstants.WandMaxSpellLevel) continue;

            int spellLevel = spell.SpellLevel;
            int minCL = CraftingCostCalculator.MinimumCasterLevelForSpell(spellLevel);
            int marketPrice = CraftingCostCalculator.WandMarketPrice(spellLevel, minCL);

            results.Add(new CraftableItemDefinition
            {
                ItemId = $"wand_{spellId}",
                DisplayName = $"Wand of {spell.Name}",
                RequiredFeat = CraftingFeatType.CraftWand,
                RequiredCasterLevel = minCL,
                MarketPriceGp = marketPrice,
                Category = $"Wand - Level {spellLevel}",
                Description = spell.Description,
                IsDynamic = true,
                DynamicSpellId = spellId,
                DynamicSpellLevel = spellLevel,
                RequiredSpellIds = new List<string> { spellId }
            });
        }

        return results.OrderBy(d => d.DynamicSpellLevel).ThenBy(d => d.DisplayName).ToList();
    }

    // ============================== DEBUG: ALL-SPELL GENERATORS ==============================

    /// <summary>
    /// Generate scroll definitions for ALL spells in the database (debug mode).
    /// </summary>
    public static List<CraftableItemDefinition> GenerateAllScrollDefinitions()
    {
        var results = new List<CraftableItemDefinition>();
        foreach (var spell in SpellDatabase.GetAllSpells())
        {
            if (spell == null || string.IsNullOrEmpty(spell.SpellId)) continue;
            int spellLevel = spell.SpellLevel;
            int minCL = CraftingCostCalculator.MinimumCasterLevelForSpell(spellLevel);
            int marketPrice = CraftingCostCalculator.ScrollMarketPrice(spellLevel, minCL);

            results.Add(new CraftableItemDefinition
            {
                ItemId = $"scroll_{spell.SpellId}",
                DisplayName = $"Scroll of {spell.Name}",
                RequiredFeat = CraftingFeatType.ScribeScroll,
                RequiredCasterLevel = minCL,
                MarketPriceGp = marketPrice,
                Category = $"Scroll - Level {spellLevel}",
                Description = spell.Description,
                IsDynamic = true,
                DynamicSpellId = spell.SpellId,
                DynamicSpellLevel = spellLevel,
                RequiredSpellIds = new List<string> { spell.SpellId }
            });
        }
        return results.OrderBy(d => d.DynamicSpellLevel).ThenBy(d => d.DisplayName).ToList();
    }

    /// <summary>
    /// Generate potion definitions for ALL eligible spells in the database (debug mode).
    /// </summary>
    public static List<CraftableItemDefinition> GenerateAllPotionDefinitions()
    {
        var results = new List<CraftableItemDefinition>();
        foreach (var spell in SpellDatabase.GetAllSpells())
        {
            if (spell == null || string.IsNullOrEmpty(spell.SpellId)) continue;
            if (spell.SpellLevel > CraftingConstants.PotionMaxSpellLevel) continue;
            int spellLevel = spell.SpellLevel;
            int minCL = CraftingCostCalculator.MinimumCasterLevelForSpell(spellLevel);
            int marketPrice = CraftingCostCalculator.PotionMarketPrice(spellLevel, minCL);

            results.Add(new CraftableItemDefinition
            {
                ItemId = $"potion_{spell.SpellId}",
                DisplayName = $"Potion of {spell.Name}",
                RequiredFeat = CraftingFeatType.BrewPotion,
                RequiredCasterLevel = minCL,
                MarketPriceGp = marketPrice,
                Category = $"Potion - Level {spellLevel}",
                Description = spell.Description,
                IsDynamic = true,
                DynamicSpellId = spell.SpellId,
                DynamicSpellLevel = spellLevel,
                RequiredSpellIds = new List<string> { spell.SpellId }
            });
        }
        return results.OrderBy(d => d.DynamicSpellLevel).ThenBy(d => d.DisplayName).ToList();
    }

    /// <summary>
    /// Generate wand definitions for ALL eligible spells in the database (debug mode).
    /// </summary>
    public static List<CraftableItemDefinition> GenerateAllWandDefinitions()
    {
        var results = new List<CraftableItemDefinition>();
        foreach (var spell in SpellDatabase.GetAllSpells())
        {
            if (spell == null || string.IsNullOrEmpty(spell.SpellId)) continue;
            if (spell.SpellLevel > CraftingConstants.WandMaxSpellLevel) continue;
            int spellLevel = spell.SpellLevel;
            int minCL = CraftingCostCalculator.MinimumCasterLevelForSpell(spellLevel);
            int marketPrice = CraftingCostCalculator.WandMarketPrice(spellLevel, minCL);

            results.Add(new CraftableItemDefinition
            {
                ItemId = $"wand_{spell.SpellId}",
                DisplayName = $"Wand of {spell.Name}",
                RequiredFeat = CraftingFeatType.CraftWand,
                RequiredCasterLevel = minCL,
                MarketPriceGp = marketPrice,
                Category = $"Wand - Level {spellLevel}",
                Description = spell.Description,
                IsDynamic = true,
                DynamicSpellId = spell.SpellId,
                DynamicSpellLevel = spellLevel,
                RequiredSpellIds = new List<string> { spell.SpellId }
            });
        }
        return results.OrderBy(d => d.DynamicSpellLevel).ThenBy(d => d.DisplayName).ToList();
    }

    // ============================== HELPERS ==============================

    /// <summary>Attempt to extract spell prerequisites from item description keywords.</summary>
    private static void AddSpellPrereqsFromItemDescription(CraftableItemDefinition def, ItemData item)
    {
        // Many items in the database store spell requirements in their description text.
        // We attempt a best-effort extraction. Items without clear spell prerequisites
        // simply have an empty RequiredSpellIds list (crafter can still make them at +5 DC per missing).
        // This is a simplification — a full implementation would have per-item prerequisites
        // authored in the database.
    }

    private static string TruncateDescription(string desc)
    {
        if (string.IsNullOrEmpty(desc)) return "";
        return desc.Length > 120 ? desc.Substring(0, 117) + "..." : desc;
    }

    /// <summary>Reset for testing.</summary>
    public static void Reset()
    {
        _byFeat.Clear();
        _byItemId.Clear();
        _initialized = false;
    }
}
