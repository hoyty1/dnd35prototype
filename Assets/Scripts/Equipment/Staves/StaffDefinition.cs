using DND35e.Identifiers;
using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// StaffDefinition.cs — Data classes for D&D 3.5e magic staves (DMG p.243)
//
// CORE DMG 3.5e RULES ONLY:
//   - Staves CANNOT be recharged. Once charges are expended, the staff
//     becomes non-magical and worthless.
//   - Standard staves have 50 charges.
//   - Each spell costs 1-5 charges to cast.
//   - Spell trigger activation (class list or UMD DC 20).
//   - All staves weigh 5 lbs and can double as quarterstaves.
//
// Architecture mirrors the WandFactory/WandValidator pattern but adds:
//   - Multiple spells per item with variable charge costs
//   - Spell selection UI (player picks which spell to cast)
//   - Passive bonuses (Staff of Power: +2 AC/saves)
//   - Retributive Strike (Staff of Power/Magi: break staff for burst)
//
// NO RECHARGING. NO UNEARTHED ARCANA. NO HOUSE RULES.
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
/// 
/// Core DMG 3.5e: Staves cannot be recharged. Once charges are expended,
/// the staff becomes non-magical. Players must manage charges as a finite resource.
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

    // ── Charges (non-rechargeable, core DMG) ──
    /// <summary>
    /// Maximum charges the staff starts with. Usually 50 (DMG p.243).
    /// Once depleted, the staff becomes non-magical and worthless.
    /// Cannot be recharged under core DMG 3.5e rules.
    /// </summary>
    public int MaxCharges = 50;

    // ── Spells ──
    public List<StaffSpellEntry> Spells = new List<StaffSpellEntry>();

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
    public string[] AllowedClasses;

    // ── Implementation tracking ──
    public StaffImplementationStatus Status = StaffImplementationStatus.Stub;
    public string ImplementationNotes;

    // ────────────────────────────────────────────
    //  Runtime state (set when staff is created/found as loot)
    // ────────────────────────────────────────────

    /// <summary>
    /// Current charges remaining. Starts at MaxCharges when created.
    /// Decreases as spells are cast. Cannot increase (no recharging in core DMG).
    /// When this reaches 0, the staff is expended and becomes non-magical.
    /// </summary>
    public int CurrentCharges;

    /// <summary>True when all charges have been used. Staff is now a mundane stick.</summary>
    public bool IsExpended => CurrentCharges <= 0;

    // ────────────────────────────────────────────
    //  Spell access
    // ────────────────────────────────────────────

    /// <summary>Find a spell entry by name.</summary>
    public StaffSpellEntry GetSpell(string spellName)
    {
        if (Spells == null) return null;
        return Spells.Find(s => s.SpellName == spellName);
    }

    /// <summary>Find a spell entry by spell ID.</summary>
    public StaffSpellEntry GetSpellById(string spellId)
    {
        if (Spells == null) return null;
        return Spells.Find(s => s.SpellId == spellId);
    }

    /// <summary>
    /// Can this spell be cast? Requires: spell exists, is not a stub,
    /// and the staff has enough charges remaining.
    /// </summary>
    public bool CanCastSpell(string spellName)
    {
        var spell = GetSpell(spellName);
        return spell != null && !spell.IsStub && CurrentCharges >= spell.ChargeCost;
    }

    /// <summary>
    /// Consume charges for a spell. Returns false if insufficient charges.
    /// Charges are NEVER restored — core DMG 3.5e rules.
    /// </summary>
    public bool ConsumeCharges(int amount)
    {
        if (CurrentCharges < amount)
            return false;

        CurrentCharges -= amount;
        return true;
    }

    // ────────────────────────────────────────────
    //  Convenience / stats
    // ────────────────────────────────────────────

    /// <summary>Number of spells in this staff that are fully implemented (not stubs).</summary>
    public int ImplementedSpellCount
    {
        get
        {
            int count = 0;
            if (Spells == null) return 0;
            foreach (var entry in Spells)
            {
                if (!entry.IsStub) count++;
            }
            return count;
        }
    }

    /// <summary>Total number of spells in this staff.</summary>
    public int TotalSpellCount => Spells?.Count ?? 0;

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
            if (Spells == null) return 0;
            foreach (var entry in Spells)
            {
                if (entry.ChargeCost > max) max = entry.ChargeCost;
            }
            return max;
        }
    }

    public override string ToString()
    {
        string chargeStr = IsExpended ? "EXPENDED" : $"{CurrentCharges}/{MaxCharges} charges";
        return $"{Name} (CL {CasterLevel}, {TotalSpellCount} spells, {chargeStr}, {Status})";
    }
}

/// <summary>
/// One spell available from a staff, with its charge cost.
/// Maps to a spell in SpellDatabase. IsStub marks spells not yet implemented.
/// </summary>
public class StaffSpellEntry
{
    /// <summary>Spell ID key into SpellDatabase (e.g., SpellNames.FIREBALL).</summary>
    public string SpellId;

    /// <summary>Display name for the spell (e.g., "Fireball").</summary>
    public string SpellName;

    /// <summary>Spell level (determines charge cost tier, also used for save DCs).</summary>
    public int SpellLevel;

    /// <summary>Number of charges consumed when this spell is cast from the staff.</summary>
    public int ChargeCost;

    /// <summary>True if the spell is not yet implemented in SpellDatabase. Cannot be cast.</summary>
    public bool IsStub;

    /// <summary>Description of what this spell should do (shown for stubs in UI).</summary>
    public string StubDescription;

    /// <summary>
    /// Runtime check: true if SpellDatabase has this spell AND it's not a placeholder.
    /// For convenience — IsStub is the authoritative flag set at registration time.
    /// </summary>
    public bool IsImplementedInSpellDB
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SpellId)) return false;
            SpellDatabase.Init();
            var spell = SpellDatabase.GetSpell(SpellId);
            return spell != null && !spell.IsPlaceholder;
        }
    }

    public override string ToString()
    {
        string status = IsStub ? "✗ STUB" : "✓";
        return $"[{status}] {SpellName} (L{SpellLevel}, {ChargeCost} charge{(ChargeCost != 1 ? "s" : "")})";
    }
}
