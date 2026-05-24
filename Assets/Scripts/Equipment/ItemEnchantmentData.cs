using System;
using System.Collections.Generic;

// ============================================================================
// D&D 3.5e Item Enchantment Data - Per-item enchantment instance data
// Phase 1-2: Foundation - Stored on each enchanted ItemData instance
// ============================================================================

/// <summary>
/// Per-item enchantment data holding the list of special abilities applied to an item.
/// Stored on ItemData.Enchantment. Null for mundane/plain magic items.
/// 
/// This is instance data — the authoritative stats for each ability live in
/// EnchantmentProperties (looked up by EnchantmentType at runtime).
/// </summary>
[Serializable]
public class ItemEnchantmentData
{
    /// <summary>
    /// List of special abilities on this item (e.g., Flaming, Holy, Keen).
    /// Order matters for display name generation.
    /// </summary>
    public List<EnchantmentType> Abilities = new List<EnchantmentType>();

    /// <summary>
    /// For Bane weapons: the creature type this weapon is bane against.
    /// Empty for non-Bane items.
    /// </summary>
    public string BaneCreatureType = "";

    /// <summary>
    /// For Defending weapons: how many points of enhancement bonus are
    /// currently being transferred to AC (set at runtime by the player).
    /// </summary>
    public int DefendingACTransfer;

    /// <summary>
    /// For Merciful weapons: whether the merciful effect is currently suppressed
    /// (allowing lethal damage instead of nonlethal).
    /// </summary>
    public bool MercifulSuppressed;

    /// <summary>Check if this enchantment data contains a specific ability.</summary>
    public bool HasAbility(EnchantmentType type)
    {
        for (int i = 0; i < Abilities.Count; i++)
        {
            if (Abilities[i] == type) return true;
        }
        return false;
    }

    /// <summary>Add an ability if not already present.</summary>
    public bool AddAbility(EnchantmentType type)
    {
        if (HasAbility(type)) return false;
        Abilities.Add(type);
        return true;
    }

    /// <summary>Remove an ability if present.</summary>
    public bool RemoveAbility(EnchantmentType type)
    {
        return Abilities.Remove(type);
    }

    /// <summary>Create a deep copy of this enchantment data.</summary>
    public ItemEnchantmentData Clone()
    {
        var clone = new ItemEnchantmentData
        {
            BaneCreatureType = BaneCreatureType,
            DefendingACTransfer = DefendingACTransfer,
            MercifulSuppressed = MercifulSuppressed,
        };
        clone.Abilities.AddRange(Abilities);
        return clone;
    }

    /// <summary>Get a summary string of all abilities for debug/tooltip display.</summary>
    public string GetAbilitySummary()
    {
        if (Abilities.Count == 0) return "None";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < Abilities.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            string name = EnchantmentProperties.GetDisplayName(Abilities[i]);
            if (Abilities[i] == EnchantmentType.Bane && !string.IsNullOrEmpty(BaneCreatureType))
                name = $"Bane ({BaneCreatureType})";
            sb.Append(name);
        }
        return sb.ToString();
    }
}
