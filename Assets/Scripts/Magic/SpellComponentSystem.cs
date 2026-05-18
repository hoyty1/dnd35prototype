// ============================================================================
// SpellComponentSystem.cs — Reusable spell material component system
// D&D 3.5e: Spells with costly material components (>1 GP) must have those
// components tracked and consumed. This system is data-driven and extensible.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Defines a single material component requirement for a spell.
/// Components with GpCost > 0 are "costly" and must be consumed when casting.
/// Components with GpCost == 0 are assumed freely available (spell component pouch).
/// </summary>
[Serializable]
public class SpellMaterialComponent
{
    /// <summary>Human-readable name of the component (e.g., "diamond dust", "granite").</summary>
    public string Name;

    /// <summary>GP cost of this component. 0 = free (spell component pouch covers it).</summary>
    public int GpCost;

    /// <summary>If true, the component is consumed when the spell is cast. Most costly components are consumed.</summary>
    public bool IsConsumed = true;

    /// <summary>Optional: item ID for inventory integration (future use).</summary>
    public string InventoryItemId;

    /// <summary>Optional: descriptive text for tooltips.</summary>
    public string Description;

    public SpellMaterialComponent() { }

    public SpellMaterialComponent(string name, int gpCost, bool isConsumed = true, string description = null, string inventoryItemId = null)
    {
        Name = name;
        GpCost = gpCost;
        IsConsumed = isConsumed;
        Description = description;
        InventoryItemId = inventoryItemId;
    }

    /// <summary>Returns a formatted display string for this component.</summary>
    public string GetDisplayString()
    {
        if (GpCost > 0)
            return $"{Name} ({GpCost:N0} gp{(IsConsumed ? ", consumed" : "")})";
        return Name;
    }
}

/// <summary>
/// Defines the full component requirements for a spell, including
/// verbal (V), somatic (S), material (M), focus (F), divine focus (DF), and XP components.
/// </summary>
[Serializable]
public class SpellComponentRequirements
{
    /// <summary>Material components required. Empty list = no special materials needed.</summary>
    public List<SpellMaterialComponent> MaterialComponents = new List<SpellMaterialComponent>();

    /// <summary>Focus items required (not consumed). Empty list = no focus needed.</summary>
    public List<SpellMaterialComponent> FocusComponents = new List<SpellMaterialComponent>();

    /// <summary>XP cost for casting this spell (e.g., some high-level spells cost XP). 0 = no XP cost.</summary>
    public int XpCost;

    /// <summary>Total GP cost of all consumed material components.</summary>
    public int TotalConsumedGpCost
    {
        get
        {
            int total = 0;
            if (MaterialComponents != null)
            {
                for (int i = 0; i < MaterialComponents.Count; i++)
                {
                    var comp = MaterialComponents[i];
                    if (comp != null && comp.IsConsumed)
                        total += comp.GpCost;
                }
            }
            return total;
        }
    }

    /// <summary>Whether this spell has any material components requiring inventory items.</summary>
    public bool HasInventoryComponents
    {
        get
        {
            if (MaterialComponents == null) return false;
            for (int i = 0; i < MaterialComponents.Count; i++)
            {
                if (MaterialComponents[i] != null && !string.IsNullOrEmpty(MaterialComponents[i].InventoryItemId))
                    return true;
            }
            return false;
        }
    }

    /// <summary>Whether this spell has any costly (GP > 0) material components.</summary>
    public bool HasCostlyComponents
    {
        get
        {
            if (MaterialComponents == null) return false;
            for (int i = 0; i < MaterialComponents.Count; i++)
            {
                if (MaterialComponents[i] != null && MaterialComponents[i].GpCost > 0)
                    return true;
            }
            return false;
        }
    }

    /// <summary>Whether this spell has any common (GP == 0) material components that require a spell component pouch.</summary>
    public bool HasCommonMaterialComponents
    {
        get
        {
            if (MaterialComponents == null) return false;
            for (int i = 0; i < MaterialComponents.Count; i++)
            {
                if (MaterialComponents[i] != null && MaterialComponents[i].GpCost == 0
                    && string.IsNullOrEmpty(MaterialComponents[i].InventoryItemId))
                    return true;
            }
            return false;
        }
    }

    /// <summary>Get a formatted component line for spell descriptions (e.g., "V, S, M (granite and 250 gp diamond dust)").</summary>
    public string GetComponentLine(bool hasVerbal = true, bool hasSomatic = true, bool hasMaterial = false)
    {
        var parts = new List<string>();
        if (hasVerbal) parts.Add("V");
        if (hasSomatic) parts.Add("S");

        if (hasMaterial || (MaterialComponents != null && MaterialComponents.Count > 0))
        {
            var matParts = new List<string>();
            if (MaterialComponents != null)
            {
                for (int i = 0; i < MaterialComponents.Count; i++)
                {
                    if (MaterialComponents[i] != null)
                        matParts.Add(MaterialComponents[i].GetDisplayString());
                }
            }
            if (matParts.Count > 0)
                parts.Add($"M ({string.Join(" and ", matParts)})");
            else
                parts.Add("M");
        }

        if (FocusComponents != null && FocusComponents.Count > 0)
        {
            var focusParts = new List<string>();
            for (int i = 0; i < FocusComponents.Count; i++)
            {
                if (FocusComponents[i] != null)
                    focusParts.Add(FocusComponents[i].GetDisplayString());
            }
            if (focusParts.Count > 0)
                parts.Add($"F ({string.Join(", ", focusParts)})");
        }

        if (XpCost > 0)
            parts.Add($"XP ({XpCost:N0})");

        return string.Join(", ", parts);
    }
}

/// <summary>
/// Central registry for spell component requirements.
/// Maps spell IDs to their component requirements.
/// Provides validation and consumption helpers.
/// </summary>
public static class SpellComponentRegistry
{
    private static Dictionary<string, SpellComponentRequirements> _registry;
    private static bool _initialized;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;
        _registry = new Dictionary<string, SpellComponentRequirements>(StringComparer.OrdinalIgnoreCase);

        RegisterDefaultComponents();
    }

    /// <summary>
    /// Register component requirements for a spell.
    /// Called during spell database initialization.
    /// </summary>
    public static void RegisterComponents(string spellId, SpellComponentRequirements requirements)
    {
        Init();
        if (string.IsNullOrWhiteSpace(spellId) || requirements == null) return;
        _registry[spellId] = requirements;
    }

    /// <summary>Get the component requirements for a spell. Returns null if none registered.</summary>
    public static SpellComponentRequirements GetRequirements(string spellId)
    {
        Init();
        if (string.IsNullOrWhiteSpace(spellId)) return null;
        _registry.TryGetValue(spellId, out SpellComponentRequirements req);
        return req;
    }

    /// <summary>Whether a spell has costly material components that need tracking.</summary>
    public static bool HasCostlyComponents(string spellId)
    {
        var req = GetRequirements(spellId);
        return req != null && req.HasCostlyComponents;
    }

    /// <summary>
    /// Whether a spell requires a spell component pouch for common material components.
    /// A spell needs the pouch if:
    /// 1) It has SpellData.HasMaterialComponent == true AND no costly components registered, OR
    /// 2) It has registered common (GP==0, no InventoryItemId) material components.
    /// Stoneskin has both a common component (granite) AND a costly one (diamond dust) — it needs the pouch for granite.
    /// </summary>
    public static bool RequiresComponentPouch(string spellId, SpellData spell = null)
    {
        // Check registered component requirements first
        var req = GetRequirements(spellId);
        if (req != null && req.HasCommonMaterialComponents)
            return true;

        // If spell data says it has material components but no costly components registered,
        // it's a common-material-only spell — needs the pouch.
        if (spell != null && spell.HasMaterialComponent)
        {
            if (req == null || !req.HasCostlyComponents)
                return true;
            // Even if it has costly components, it may also have common ones (e.g., Stoneskin has granite)
            // We already checked HasCommonMaterialComponents above.
        }

        return false;
    }

    /// <summary>
    /// Check if a character has a Spell Component Pouch in their inventory.
    /// The pouch is NOT consumed — it's a reusable item.
    /// </summary>
    public static bool HasSpellComponentPouch(CharacterController caster)
    {
        if (caster == null) return false;
        Inventory inv = caster.GetInventoryData();
        if (inv == null) return false;

        for (int s = 0; s < inv.GeneralSlots.Length; s++)
        {
            ItemData item = inv.GeneralSlots[s];
            if (item != null && string.Equals(item.Id, ItemIDs.COMPONENT_SPELL_POUCH, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Validates that a caster has the spell component pouch if needed for a spell.
    /// Returns true if the pouch is not needed or is present. 
    /// Returns false and sets reason if the pouch is missing.
    /// </summary>
    public static bool ValidatePouchRequirement(string spellId, SpellData spell, CharacterController caster, out string failureReason)
    {
        failureReason = null;
        if (caster == null) return true;

        if (!RequiresComponentPouch(spellId, spell))
            return true;

        if (HasSpellComponentPouch(caster))
            return true;

        failureReason = "Spell Component Pouch";
        return false;
    }

    /// <summary>Get the total GP cost of consumed components for a spell.</summary>
    public static int GetTotalComponentCost(string spellId)
    {
        var req = GetRequirements(spellId);
        return req?.TotalConsumedGpCost ?? 0;
    }

    /// <summary>
    /// Check if a character can afford the material component costs for a spell.
    /// For components with InventoryItemId, checks the character's inventory.
    /// For other costly components, checks ComponentGold.
    /// </summary>
    public static bool CanAffordComponents(string spellId, CharacterStats stats)
    {
        if (stats == null) return false;
        var req = GetRequirements(spellId);
        if (req == null || !req.HasCostlyComponents) return true;

        // For inventory-based components, defer to CanAffordComponentsFromInventory
        if (req.HasInventoryComponents)
            return true; // Gold check only applies to non-inventory components

        return stats.ComponentGold >= req.TotalConsumedGpCost;
    }

    /// <summary>
    /// Check if a character has the required inventory items for a spell's material components.
    /// Returns true if all inventory-based components are present, false otherwise.
    /// Also returns the missing component name via out parameter.
    /// </summary>
    public static bool HasRequiredInventoryComponents(string spellId, CharacterController caster, out string missingComponent)
    {
        missingComponent = null;
        if (caster == null) return true;

        var req = GetRequirements(spellId);
        if (req == null || !req.HasInventoryComponents) return true;

        Inventory inv = caster.GetInventoryData();
        if (inv == null)
        {
            // No inventory — check fails for any inventory component
            for (int i = 0; i < req.MaterialComponents.Count; i++)
            {
                var comp = req.MaterialComponents[i];
                if (comp != null && !string.IsNullOrEmpty(comp.InventoryItemId) && comp.IsConsumed)
                {
                    missingComponent = comp.Name;
                    return false;
                }
            }
            return true;
        }

        for (int i = 0; i < req.MaterialComponents.Count; i++)
        {
            var comp = req.MaterialComponents[i];
            if (comp == null || string.IsNullOrEmpty(comp.InventoryItemId) || !comp.IsConsumed)
                continue;

            // Search inventory for an item with matching Id
            bool found = false;
            for (int s = 0; s < inv.GeneralSlots.Length; s++)
            {
                ItemData item = inv.GeneralSlots[s];
                if (item != null && string.Equals(item.Id, comp.InventoryItemId, System.StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                missingComponent = comp.Name;
                Debug.Log($"[SpellComponentRegistry] {caster.Stats?.CharacterName} missing inventory component '{comp.Name}' (item id: {comp.InventoryItemId}) for {spellId}.");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Consume inventory-based material components for a spell.
    /// Removes matching items from the character's inventory.
    /// Returns true if successful.
    /// </summary>
    public static bool ConsumeInventoryComponents(string spellId, CharacterController caster)
    {
        if (caster == null) return false;

        var req = GetRequirements(spellId);
        if (req == null || !req.HasInventoryComponents) return true;

        Inventory inv = caster.GetInventoryData();
        if (inv == null) return false;

        for (int i = 0; i < req.MaterialComponents.Count; i++)
        {
            var comp = req.MaterialComponents[i];
            if (comp == null || string.IsNullOrEmpty(comp.InventoryItemId) || !comp.IsConsumed)
                continue;

            // Find and remove the first matching item from inventory
            bool removed = false;
            for (int s = 0; s < inv.GeneralSlots.Length; s++)
            {
                ItemData item = inv.GeneralSlots[s];
                if (item != null && string.Equals(item.Id, comp.InventoryItemId, System.StringComparison.OrdinalIgnoreCase))
                {
                    inv.GeneralSlots[s] = null;
                    removed = true;
                    Debug.Log($"[SpellComponentRegistry] {caster.Stats?.CharacterName} consumed inventory item '{item.Name}' for {spellId}.");
                    break;
                }
            }

            if (!removed)
            {
                Debug.LogWarning($"[SpellComponentRegistry] Failed to consume inventory component '{comp.Name}' for {spellId} — item not found.");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Consume the material components for a spell (deduct gold from component budget).
    /// Only handles non-inventory components. For inventory-based components, use ConsumeInventoryComponents.
    /// Returns true if successful, false if character can't afford it.
    /// </summary>
    public static bool ConsumeComponents(string spellId, CharacterStats stats)
    {
        if (stats == null) return false;
        var req = GetRequirements(spellId);
        if (req == null || !req.HasCostlyComponents) return true;

        // Skip inventory-based components (handled by ConsumeInventoryComponents)
        if (req.HasInventoryComponents) return true;

        int cost = req.TotalConsumedGpCost;
        if (stats.ComponentGold < cost)
        {
            Debug.LogWarning($"[SpellComponentRegistry] {stats.CharacterName} cannot afford {cost} gp for {spellId} (has {stats.ComponentGold} gp).");
            return false;
        }

        stats.ComponentGold -= cost;
        Debug.Log($"[SpellComponentRegistry] {stats.CharacterName} consumed {cost} gp in components for {spellId}. Remaining component gold: {stats.ComponentGold} gp.");
        return true;
    }

    /// <summary>
    /// Get a formatted string describing what components will be consumed and their cost.
    /// Useful for confirmation dialogs.
    /// </summary>
    public static string GetConsumptionSummary(string spellId)
    {
        var req = GetRequirements(spellId);
        if (req == null || !req.HasCostlyComponents) return null;

        var consumed = new List<string>();
        if (req.MaterialComponents != null)
        {
            for (int i = 0; i < req.MaterialComponents.Count; i++)
            {
                var comp = req.MaterialComponents[i];
                if (comp != null && comp.GpCost > 0 && comp.IsConsumed)
                    consumed.Add(comp.GetDisplayString());
            }
        }

        if (consumed.Count == 0) return null;
        return $"Consumes: {string.Join(", ", consumed)} (total {req.TotalConsumedGpCost:N0} gp)";
    }

    /// <summary>Register default component requirements for spells with costly materials.</summary>
    private static void RegisterDefaultComponents()
    {
        // ── STONESKIN (PHB p.285) ──
        // Components: V, S, M (granite and 250 gp worth of diamond dust)
        // Diamond dust is an inventory-based component — must be purchased and carried.
        RegisterComponents("stoneskin", new SpellComponentRequirements
        {
            MaterialComponents = new List<SpellMaterialComponent>
            {
                new SpellMaterialComponent("granite", 0, false, "A small piece of granite"),
                new SpellMaterialComponent("diamond dust", 250, true,
                    "250 gp worth of diamond dust, sprinkled on the target's skin",
                    inventoryItemId: ItemIDs.COMPONENT_DIAMOND_DUST)
            }
        });

        // ── IDENTIFY (PHB p.243) ──
        // Components: V, S, M (a pearl worth at least 100 gp)
        RegisterComponents("identify", new SpellComponentRequirements
        {
            MaterialComponents = new List<SpellMaterialComponent>
            {
                new SpellMaterialComponent("pearl", 100, true, "A pearl worth at least 100 gp, crushed and stirred into wine")
            }
        });

        // ── CONTINUAL FLAME (PHB p.213) ──
        // Components: V, S, M (ruby dust worth 50 gp)
        RegisterComponents("continual_flame", new SpellComponentRequirements
        {
            MaterialComponents = new List<SpellMaterialComponent>
            {
                new SpellMaterialComponent("ruby dust", 50, true, "Ruby dust worth 50 gp")
            }
        });

        // ── ARCANE LOCK (PHB p.200) ──
        // Components: V, S, M (gold dust worth 25 gp)
        RegisterComponents("arcane_lock", new SpellComponentRequirements
        {
            MaterialComponents = new List<SpellMaterialComponent>
            {
                new SpellMaterialComponent("gold dust", 25, true, "Gold dust worth 25 gp")
            }
        });

        // Future spells with costly components can be added here
        // e.g., Restoration (100 gp diamond dust), Raise Dead (5000 gp diamond), etc.
    }
}
