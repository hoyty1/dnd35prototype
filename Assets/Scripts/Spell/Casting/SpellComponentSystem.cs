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

    // The component test itself (the pouch, the divine focus, priced foci and materials, for every caster) and the spend
    // of priced materials are SpellComponentRules (batch 11 step 17); the old per-path validators that encoded a second,
    // different rule were deleted with it. Use SpellComponentRules, not new checks here (docs/systems/PC_NPC_PARITY.md).

    /// <summary>
    /// Check if a character has a Spell Component Pouch in their pack (a test helper; the cast rule is
    /// SpellComponentRules.Refusal). The pouch is NOT consumed — it's a reusable item.
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

    /// <summary>Get the total GP cost of consumed components for a spell.</summary>
    public static int GetTotalComponentCost(string spellId)
    {
        var req = GetRequirements(spellId);
        return req?.TotalConsumedGpCost ?? 0;
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
        // Components: V, S, M/DF (arcane: a pearl worth at least 100 gp, crushed and stirred into wine with an owl feather)
        RegisterComponents("identify", new SpellComponentRequirements
        {
            MaterialComponents = new List<SpellMaterialComponent>
            {
                // The wine and the owl feather have no price: the pouch supplies them (PHB p.174), as Stoneskin's granite.
                new SpellMaterialComponent("wine and an owl feather", 0, true, "Wine and an owl feather to stir the pearl into"),
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

        // The other priced components and foci of the registered spells (PHB chapter 11; batch 11 step 17, SPL-020). A
        // consumed component with no item is paid from CharacterStats.ComponentGold (ITM-029); a priced focus is an item
        // the caster must carry (SpellComponentRules).
        RegisterCostlyMaterial(SpellNames.AUGURY, "incense", 25, "Incense worth at least 25 gp (PHB p.202)");
        GetRequirements(SpellNames.AUGURY).FocusComponents.Add(new SpellMaterialComponent("marked tokens", 25, false,
            "Marked sticks, bones or similar tokens worth at least 25 gp (PHB p.202)", inventoryItemId: ItemIDs.FOCUS_AUGURY_TOKENS));
        RegisterCostlyMaterial(SpellNames.CONSECRATE, "holy water and silver dust", 50,
            "A vial of holy water (25 gp) and 25 gp of silver dust (PHB p.212)");
        RegisterCostlyMaterial(SpellNames.DESECRATE, "unholy water and silver dust", 50,
            "A vial of unholy water (25 gp) and 25 gp of silver dust (PHB p.218)");
        RegisterCostlyMaterial(SpellNames.CIRCLE_OF_DEATH, "black pearl powder", 500, "A crushed black pearl worth at least 500 gp (PHB p.209)");
        RegisterCostlyMaterial(SpellNames.GLYPH_OF_WARDING, "powdered diamond", 200, "Incense sprinkled with powdered diamond worth at least 200 gp (PHB p.236)",
            unpricedPart: "incense");
        RegisterCostlyMaterial(SpellNames.RESTORATION, "diamond dust", 100, "Diamond dust worth 100 gp (PHB p.272)");
        RegisterCostlyMaterial(SpellNames.RESURRECTION, "diamonds", 10000, "Holy water and diamonds worth at least 10,000 gp (PHB p.272)");
        RegisterCostlyMaterial(SpellNames.TRUE_SEEING, "eye ointment", 250, "An eye ointment costing 250 gp (PHB p.296)");
        RegisterCostlyMaterial(SpellNames.PROTECTION_FROM_SPELLS, "crushed diamond", 500, "A diamond of at least 500 gp, crushed (PHB p.266)");
        GetRequirements(SpellNames.PROTECTION_FROM_SPELLS).FocusComponents.Add(new SpellMaterialComponent("diamond", 1000, false,
            "A 1,000 gp diamond for each subject to carry (PHB p.266)", inventoryItemId: ItemIDs.FOCUS_DIAMOND_1000));
        RegisterComponents(SpellNames.SHIELD_OTHER, new SpellComponentRequirements
        {
            FocusComponents = new List<SpellMaterialComponent>
            {
                new SpellMaterialComponent("platinum rings", 100, false,
                    "A pair of platinum rings worth at least 50 gp each (PHB p.278)", inventoryItemId: ItemIDs.FOCUS_PLATINUM_RINGS)
            }
        });
        RegisterComponents(SpellNames.SHIELD_OF_LAW, new SpellComponentRequirements
        {
            FocusComponents = new List<SpellMaterialComponent>
            {
                new SpellMaterialComponent("reliquary", 500, false,
                    "A tiny reliquary worth at least 500 gp (PHB p.278)", inventoryItemId: ItemIDs.FOCUS_RELIQUARY)
            }
        });
    }

    /// <summary>
    /// Registers a spell's priced material component; <paramref name="unpricedPart"/> names the part of its Components
    /// line that has no price (Glyph of Warding's incense), a 0 gp component the pouch supplies (PHB p.174), so the
    /// caster also needs the pouch (SpellComponentRequirements.HasCommonMaterialComponents).
    /// </summary>
    private static void RegisterCostlyMaterial(string spellId, string name, int gpCost, string description, string unpricedPart = null)
    {
        var materials = new List<SpellMaterialComponent>();
        if (!string.IsNullOrEmpty(unpricedPart))
            materials.Add(new SpellMaterialComponent(unpricedPart, 0, true, unpricedPart));
        materials.Add(new SpellMaterialComponent(name, gpCost, true, description));
        RegisterComponents(spellId, new SpellComponentRequirements { MaterialComponents = materials });
    }
}
