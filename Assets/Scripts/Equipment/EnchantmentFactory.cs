using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// D&D 3.5e Enchantment Factory - Creates enchanted item variants
// Phase 2: Factory with validation, naming, pricing
// Mirrors ItemMaterialFactory pattern: clone → validate → apply → register
// ============================================================================

/// <summary>
/// Factory for creating enchanted item variants from base items.
/// Handles validation of enchantment compatibility, pricing via D&D 3.5 formulas,
/// and registration in ItemDatabase.
///
/// Usage:
///   string result;
///   ItemData flamingSword = EnchantmentFactory.CreateEnchantedVariant(
///       "longsword_base", 1, new[] { EnchantmentType.Flaming }, out result);
/// </summary>
public static class EnchantmentFactory
{
    // ========================================================================
    // PUBLIC API - Create Enchanted Variants
    // ========================================================================

    /// <summary>
    /// Create an enchanted variant of a base item with the specified enhancement bonus
    /// and special abilities.
    /// </summary>
    /// <param name="baseItemId">ID of the base item in ItemDatabase.</param>
    /// <param name="enhancementBonus">Base enhancement bonus (+1 to +5).</param>
    /// <param name="abilities">Special abilities to apply.</param>
    /// <param name="resultMessage">Output message describing success or failure reason.</param>
    /// <param name="baneCreatureType">For Bane weapons: the target creature type.</param>
    /// <returns>The new enchanted ItemData, or null if validation fails.</returns>
    public static ItemData CreateEnchantedVariant(
        string baseItemId,
        int enhancementBonus,
        EnchantmentType[] abilities,
        out string resultMessage,
        string baneCreatureType = "")
    {
        // Get base item
        ItemData baseItem = ItemDatabase.GetItem(baseItemId);
        if (baseItem == null)
        {
            resultMessage = $"Base item '{baseItemId}' not found in ItemDatabase.";
            return null;
        }

        // Validate enhancement bonus
        if (enhancementBonus < 1 || enhancementBonus > 5)
        {
            resultMessage = $"Enhancement bonus must be 1-5, got {enhancementBonus}.";
            return null;
        }

        // Validate item type
        if (!baseItem.IsWeapon && !baseItem.IsArmor && !baseItem.IsShield)
        {
            resultMessage = $"Item '{baseItemId}' is not a weapon, armor, or shield.";
            return null;
        }

        // Validate each ability
        for (int i = 0; i < abilities.Length; i++)
        {
            string validationError = ValidateAbility(baseItem, enhancementBonus, abilities[i], abilities, baneCreatureType);
            if (!string.IsNullOrEmpty(validationError))
            {
                resultMessage = validationError;
                return null;
            }
        }

        // Check for incompatibilities between abilities
        string incompatError = CheckIncompatibilities(abilities);
        if (!string.IsNullOrEmpty(incompatError))
        {
            resultMessage = incompatError;
            return null;
        }

        // Validate total effective bonus doesn't exceed +10
        int totalBonus = enhancementBonus;
        for (int i = 0; i < abilities.Length; i++)
        {
            var stats = EnchantmentProperties.Get(abilities[i]);
            if (stats != null) totalBonus += stats.BonusEquivalent;
        }
        if (totalBonus > 10)
        {
            resultMessage = $"Total effective bonus ({totalBonus}) exceeds maximum of +10.";
            return null;
        }

        // Clone and enchant
        ItemData enchanted = ItemDatabase.CloneItem(baseItemId);
        if (enchanted == null)
        {
            resultMessage = $"Failed to clone item '{baseItemId}'.";
            return null;
        }

        // Apply enchantment
        ApplyEnchantment(enchanted, enhancementBonus, abilities, baneCreatureType);

        // Generate unique ID and register
        string newId = GenerateEnchantedId(baseItemId, enhancementBonus, abilities, baneCreatureType);
        enchanted.Id = newId;

        ItemDatabase.RegisterItem(enchanted);

        resultMessage = $"Created enchanted item: {enchanted.FullDisplayName} (ID: {newId}, Price: {enchanted.EnhancedPriceGp} gp)";
        Debug.Log($"[EnchantmentFactory] {resultMessage}");
        return enchanted;
    }

    /// <summary>
    /// Apply enchantment bonus and abilities to an existing item (e.g., during enchanting at a forge).
    /// Does NOT clone or register — modifies the item in place.
    /// </summary>
    public static void ApplyEnchantment(ItemData item, int enhancementBonus, EnchantmentType[] abilities, string baneCreatureType = "")
    {
        // Set enhancement bonus
        item.EnhancementBonus = enhancementBonus;
        item.enhancementBonus = enhancementBonus;
        item.IsMasterwork = true; // All magic items are masterwork

        // Create enchantment data
        item.Enchantment = new ItemEnchantmentData();
        item.Enchantment.BaneCreatureType = baneCreatureType;

        for (int i = 0; i < abilities.Length; i++)
        {
            item.Enchantment.AddAbility(abilities[i]);

            // Apply special ability side effects
            ApplyAbilitySideEffects(item, abilities[i]);
        }

        // Mark as magic for DR bypass
        item.CountsAsMagicForBypass = true;
    }

    // ========================================================================
    // VALIDATION
    // ========================================================================

    /// <summary>
    /// Validate a single ability against the item and current enhancement bonus.
    /// Returns error message or empty string if valid.
    /// </summary>
    public static string ValidateAbility(ItemData item, int enhancementBonus, EnchantmentType ability,
        EnchantmentType[] allAbilities, string baneCreatureType = "")
    {
        var stats = EnchantmentProperties.Get(ability);
        if (stats == null)
            return $"Unknown enchantment type: {ability}";

        // Slot validation
        bool slotValid = false;
        switch (stats.Slot)
        {
            case EnchantmentSlot.Weapon:
                slotValid = item.IsWeapon;
                break;
            case EnchantmentSlot.Armor:
                slotValid = item.IsArmor;
                break;
            case EnchantmentSlot.Shield:
                slotValid = item.IsShield;
                break;
            case EnchantmentSlot.ArmorOrShield:
                slotValid = item.IsArmor || item.IsShield;
                break;
        }
        if (!slotValid)
            return $"{stats.DisplayName} cannot be applied to {item.Type} items.";

        // Melee/Ranged restrictions
        if (stats.MeleeOnly && item.WeaponCat == WeaponCategory.Ranged)
            return $"{stats.DisplayName} can only be applied to melee weapons.";

        // For ranged-only abilities: allow if weapon is ranged, OR if it's naturally throwable, OR if it has Throwing enchantment
        if (stats.RangedOnly)
        {
            bool isRangedOrThrowable = item.WeaponCat == WeaponCategory.Ranged
                || item.IsThrown  // Naturally throwable (dagger, handaxe, javelin)
                || System.Array.IndexOf(allAbilities, EnchantmentType.Throwing) >= 0; // Has Throwing enchantment being applied
            if (!isRangedOrThrowable)
                return $"{stats.DisplayName} can only be applied to ranged or throwable weapons.";
        }

        // Slashing/Piercing restriction (for Keen, Vorpal)
        if (stats.RequiresSlashingOrPiercing && item.IsWeapon)
        {
            var dmgTypes = item.GetDamageTypes();
            bool hasSlashingOrPiercing = dmgTypes.Contains(DamageType.Slashing) || dmgTypes.Contains(DamageType.Piercing);
            if (!hasSlashingOrPiercing)
                return $"{stats.DisplayName} requires a slashing or piercing weapon.";
        }

        // Minimum enhancement bonus
        if (stats.MinimumEnhancementBonus > 0 && enhancementBonus < stats.MinimumEnhancementBonus)
            return $"{stats.DisplayName} requires minimum +{stats.MinimumEnhancementBonus} enhancement bonus.";

        // Required enchantments
        if (stats.RequiredEnchantments != null && stats.RequiredEnchantments.Count > 0)
        {
            for (int i = 0; i < stats.RequiredEnchantments.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < allAbilities.Length; j++)
                {
                    if (allAbilities[j] == stats.RequiredEnchantments[i]) { found = true; break; }
                }
                if (!found)
                {
                    string reqName = EnchantmentProperties.GetDisplayName(stats.RequiredEnchantments[i]);
                    return $"{stats.DisplayName} requires {reqName} to be present.";
                }
            }
        }

        // Bane requires creature type
        if (ability == EnchantmentType.Bane && string.IsNullOrEmpty(baneCreatureType))
            return "Bane enchantment requires a target creature type.";

        return "";
    }

    /// <summary>Check for incompatibilities between all abilities in the list.</summary>
    private static string CheckIncompatibilities(EnchantmentType[] abilities)
    {
        for (int i = 0; i < abilities.Length; i++)
        {
            var statsI = EnchantmentProperties.Get(abilities[i]);
            if (statsI == null || statsI.IncompatibleWith == null) continue;

            for (int j = 0; j < abilities.Length; j++)
            {
                if (i == j) continue;
                for (int k = 0; k < statsI.IncompatibleWith.Count; k++)
                {
                    if (abilities[j] == statsI.IncompatibleWith[k])
                    {
                        return $"{statsI.DisplayName} is incompatible with {EnchantmentProperties.GetDisplayName(abilities[j])}.";
                    }
                }
            }
        }

        // Check for duplicate abilities
        for (int i = 0; i < abilities.Length; i++)
        {
            for (int j = i + 1; j < abilities.Length; j++)
            {
                if (abilities[i] == abilities[j])
                    return $"Duplicate ability: {EnchantmentProperties.GetDisplayName(abilities[i])}.";
            }
        }

        return "";
    }

    // ========================================================================
    // SIDE EFFECTS
    // ========================================================================

    /// <summary>
    /// Apply side effects of a specific ability to the item (e.g., Throwing makes weapon throwable,
    /// Keen modifies crit range, Distance doubles range).
    /// </summary>
    private static void ApplyAbilitySideEffects(ItemData item, EnchantmentType ability)
    {
        var stats = EnchantmentProperties.Get(ability);
        if (stats == null) return;

        // Throwing: make melee weapon throwable
        if (stats.AllowsThrow && item.WeaponCat == WeaponCategory.Melee)
        {
            item.IsThrown = true;
            if (item.RangeIncrement <= 0)
                item.RangeIncrement = stats.ThrowRangeIncrement;
        }

        // Distance: double range increment
        if (stats.DoublesRange && item.RangeIncrement > 0)
        {
            item.RangeIncrement *= 2;
        }

        // Keen: double threat range (lower CritThreatMin)
        if (stats.DoublesThreadRange && item.CritThreatMin > 0)
        {
            // D&D 3.5: threat range = 21 - CritThreatMin. Doubled means: new range = old range * 2
            // e.g., 19-20 (range 2) → 17-20 (range 4). 20 only (range 1) → 19-20 (range 2).
            int currentRange = 21 - item.CritThreatMin;
            int doubledRange = currentRange * 2;
            item.CritThreatMin = Mathf.Max(2, 21 - doubledRange); // Floor at 2 (can't go below 2-20)
        }

        // Alignment bypass tags
        if (stats.AlignmentBypassTag != DamageBypassTag.None)
        {
            // This will be checked dynamically in combat, but we can store it for reference
        }
    }

    // ========================================================================
    // ID GENERATION
    // ========================================================================

    /// <summary>Generate a unique ID for an enchanted variant.</summary>
    private static string GenerateEnchantedId(string baseId, int enhBonus, EnchantmentType[] abilities, string baneCreatureType)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(baseId);
        sb.Append($"_plus{enhBonus}");

        for (int i = 0; i < abilities.Length; i++)
        {
            sb.Append($"_{abilities[i].ToString().ToLower()}");
        }

        if (!string.IsNullOrEmpty(baneCreatureType))
            sb.Append($"_{baneCreatureType.ToLower().Replace(" ", "_")}");

        return sb.ToString();
    }

    // ========================================================================
    // BATCH CREATION HELPERS
    // ========================================================================

    /// <summary>
    /// Create a simple enchanted weapon with one special ability.
    /// Convenience method for common items.
    /// </summary>
    public static ItemData CreateSimpleEnchantedWeapon(string baseItemId, int enhancementBonus,
        EnchantmentType ability, string baneCreatureType = "")
    {
        string result;
        return CreateEnchantedVariant(baseItemId, enhancementBonus,
            new[] { ability }, out result, baneCreatureType);
    }

    /// <summary>
    /// Create a plain magic weapon with no special abilities (just enhancement bonus).
    /// </summary>
    public static ItemData CreateMagicWeapon(string baseItemId, int enhancementBonus)
    {
        string result;
        return CreateEnchantedVariant(baseItemId, enhancementBonus,
            new EnchantmentType[0], out result);
    }

    /// <summary>
    /// Create a plain magic armor/shield with no special abilities.
    /// </summary>
    public static ItemData CreateMagicArmor(string baseItemId, int enhancementBonus)
    {
        return CreateMagicWeapon(baseItemId, enhancementBonus); // Same logic, different item type
    }
}
