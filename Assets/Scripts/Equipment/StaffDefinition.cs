using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// StaffDefinition.cs — Data classes for D&D 3.5e magic staves (DMG p.243)
//
// Staves are spell-trigger items that store multiple spells with variable
// charge costs. Unlike wands (single spell, 50 charges, non-rechargeable),
// staves hold 2-16 spells, cost 1-5 charges each, and are rechargeable
// (1/day by sacrificing a spell slot).
//
// Architecture mirrors the WandFactory/WandValidator pattern but adds:
//   - Multiple spells per item with variable charge costs
//   - Spell selection UI (player picks which spell to cast)
//   - Recharging mechanics
//   - Passive bonuses (Staff of Power: +2 AC/saves)
//   - Retributive Strike (Staff of Power/Magi: break staff for burst)
// ============================================================================

/// <summary>
/// Implementation readiness of a staff.
/// Full = all spells work; Partial = some stubbed; Stub = catalog-only.
/// </summary>
public enum StaffImplementationStatus
{
    /// <summary>All spells fully implemented — staff is fully usable.</summary>
    Full,
    /// <summary>Some spells work, others are stubbed or missing — partially usable.</summary>
    Partial,
    /// <summary>Registered in catalog but not usable — all/most spells missing.</summary>
    Stub
}

/// <summary>
/// Definition of a single magic staff from the DMG.
/// Immutable once registered in StaffDatabase.
/// </summary>
public class StaffDefinition
{
    // ── Identity ──
    public string StaffId;             // e.g., "staff_of_fire"
    public string Name;                // e.g., "Staff of Fire"
    public string Description;         // Flavor + rules text
    public string AuraSchool;          // e.g., "Evocation"
    public string AuraStrength = "Strong"; // Faint/Moderate/Strong/Overwhelming

    // ── Economics ──
    public int CasterLevel;            // Staff's CL (used for spell effects)
    public int MarketPrice;            // gp value
    public float WeightLbs = 5f;       // All staves weigh 5 lbs (DMG)

    // ── Charges ──
    public int MaxCharges = 50;        // Standard D&D 3.5e (DMG p.243)

    // ── Spells ──
    public List<StaffSpellEntry> SpellEntries = new List<StaffSpellEntry>();

    // ── Passive bonuses (Staff of Power, etc.) ──
    /// <summary>Luck bonus to AC while held (Staff of Power: +2).</summary>
    public int PassiveACBonus;
    /// <summary>Luck bonus to all saves while held (Staff of Power: +2).</summary>
    public int PassiveSaveBonus;

    // ── Retributive Strike (Staff of Power, Staff of the Magi) ──
    /// <summary>Can be broken as standard action for massive burst damage.</summary>
    public bool HasRetributiveStrike;
    /// <summary>Damage multiplier: total = this × remaining charges.</summary>
    public int RetributiveStrikeDamageFactor = 8;

    // ── Activation requirements ──
    /// <summary>
    /// Classes that can activate this staff (spell must be on their list).
    /// Empty = any caster class. UMD DC 20 always available as fallback.
    /// </summary>
    public string[] RequiredClasses;

    // ── Implementation tracking ──
    public StaffImplementationStatus Status = StaffImplementationStatus.Stub;
    public string ImplementationNotes;

    // ── Convenience ──

    /// <summary>Number of spells in this staff that are fully implemented.</summary>
    public int ImplementedSpellCount
    {
        get
        {
            int count = 0;
            foreach (var entry in SpellEntries)
            {
                if (entry.IsImplemented) count++;
            }
            return count;
        }
    }

    /// <summary>Total number of spells in this staff.</summary>
    public int TotalSpellCount => SpellEntries?.Count ?? 0;

    /// <summary>Percentage of spells implemented (0-100).</summary>
    public int ImplementationPercent
    {
        get
        {
            if (TotalSpellCount == 0) return 0;
            return Mathf.RoundToInt((float)ImplementedSpellCount / TotalSpellCount * 100f);
        }
    }

    /// <summary>The highest charge cost among all spell entries.</summary>
    public int MaxChargeCost
    {
        get
        {
            int max = 0;
            foreach (var entry in SpellEntries)
            {
                if (entry.ChargeCost > max) max = entry.ChargeCost;
            }
            return max;
        }
    }

    public override string ToString()
    {
        return $"{Name} (CL {CasterLevel}, {TotalSpellCount} spells, {ImplementedSpellCount} implemented, {Status})";
    }
}

/// <summary>
/// One spell available from a staff, with its charge cost.
/// Maps to a spell in SpellDatabase.
/// </summary>
public class StaffSpellEntry
{
    /// <summary>Spell ID key into SpellDatabase (e.g., SpellNames.FIREBALL).</summary>
    public string SpellId;

    /// <summary>Display name for the spell (e.g., "Fireball").</summary>
    public string SpellName;

    /// <summary>Spell level (used for recharging: charges gained = spell level sacrificed).</summary>
    public int SpellLevel;

    /// <summary>Number of charges consumed when this spell is cast from the staff.</summary>
    public int ChargeCost;

    /// <summary>
    /// Auto-resolved: true if SpellDatabase has this spell AND it's not a placeholder.
    /// Checked at runtime so it automatically updates as spells are implemented.
    /// </summary>
    public bool IsImplemented
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SpellId)) return false;
            SpellDatabase.Init();
            var spell = SpellDatabase.GetSpell(SpellId);
            return spell != null && !spell.IsPlaceholder;
        }
    }

    /// <summary>
    /// Can this spell be cast right now? Requires implementation AND sufficient charges
    /// on the staff (checked externally — this only checks implementation).
    /// </summary>
    public bool IsAvailable => IsImplemented;

    public override string ToString()
    {
        string status = IsImplemented ? "✓" : "✗";
        return $"[{status}] {SpellName} (L{SpellLevel}, {ChargeCost} charge{(ChargeCost != 1 ? "s" : "")})";
    }
}
