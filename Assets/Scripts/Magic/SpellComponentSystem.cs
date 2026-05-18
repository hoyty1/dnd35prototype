// ============================================================================
// SpellComponentSystem.cs — Reusable spell material component system
// D&D 3.5e: Spells with costly material components (>1 GP) must have those
// components tracked and consumed. This system is data-driven and extensible.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

    public SpellMaterialComponent(string name, int gpCost, bool isConsumed = true, string description = null)
    {
        Name = name;
        GpCost = gpCost;
        IsConsumed = isConsumed;
        Description = description;
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

    /// <summary>Get the total GP cost of consumed components for a spell.</summary>
    public static int GetTotalComponentCost(string spellId)
    {
        var req = GetRequirements(spellId);
        return req?.TotalConsumedGpCost ?? 0;
    }

    /// <summary>
    /// Check if a character can afford the material component costs for a spell.
    /// Returns true if the character has enough gold, false otherwise.
    /// </summary>
    public static bool CanAffordComponents(string spellId, CharacterStats stats)
    {
        if (stats == null) return false;
        var req = GetRequirements(spellId);
        if (req == null || !req.HasCostlyComponents) return true;
        return stats.ComponentGold >= req.TotalConsumedGpCost;
    }

    /// <summary>
    /// Consume the material components for a spell (deduct gold from component budget).
    /// Returns true if successful, false if character can't afford it.
    /// </summary>
    public static bool ConsumeComponents(string spellId, CharacterStats stats)
    {
        if (stats == null) return false;
        var req = GetRequirements(spellId);
        if (req == null || !req.HasCostlyComponents) return true;

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
        RegisterComponents("stoneskin", new SpellComponentRequirements
        {
            MaterialComponents = new List<SpellMaterialComponent>
            {
                new SpellMaterialComponent("granite", 0, false, "A small piece of granite"),
                new SpellMaterialComponent("diamond dust", 250, true, "250 gp worth of diamond dust, sprinkled on the target's skin")
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
