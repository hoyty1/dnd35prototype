using UnityEngine;

/// <summary>
/// Represents a single spell slot in D&D 3.5e spell preparation.
/// Each slot holds a specific level and can be filled with one spell from the spellbook.
/// Wizards prepare spells by assigning them to individual slots.
/// The same spell can be prepared in multiple slots.
///
/// D&D 3.5e Rules:
/// - Wizards prepare spells from their spellbook into slots each day after rest.
/// - Each slot holds exactly one spell of that slot's level.
/// - The same spell can fill multiple slots (e.g., Magic Missile in 3 slots).
/// - Casting consumes a specific slot (marks it as used).
/// - After rest, all slots are restored (IsUsed = false) but prepared spells remain.
/// - Wizard can optionally re-prepare different spells after rest.
/// - Cantrips (level 0) use slots for preparation but are UNLIMITED use.
///   Casting a cantrip does not consume (mark as used) the slot.
///   This applies to both Wizards and Clerics.
/// </summary>
[System.Serializable]
public class SpellSlot
{
    /// <summary>Spell level this slot is for (0 = cantrip, 1 = 1st, 2 = 2nd, etc.)</summary>
    public int Level;

    /// <summary>The spell prepared in this slot. Null if the slot is empty.</summary>
    public SpellData PreparedSpell;

    /// <summary>Whether this slot has been used (spell cast) today.</summary>
    public bool IsUsed;

    /// <summary>True when this slot is temporarily disabled by negative levels.</summary>
    public bool DisabledByNegativeLevel;

    /// <summary>
    /// True when this slot is a cleric domain slot.
    /// Domain slots can only be prepared with spells from one of the cleric's chosen domains.
    /// </summary>
    public bool IsDomainSlot;

    /// <summary>
    /// Optional domain hint for UI/debugging (e.g., "Healing").
    /// Not required for validation because validation can use the full domain list.
    /// </summary>
    public string DomainHint;

    /// <summary>Create a new empty spell slot at the given level.</summary>
    public SpellSlot(int level, bool isDomainSlot = false)
    {
        Level = level;
        PreparedSpell = null;
        IsUsed = false;
        DisabledByNegativeLevel = false;
        IsDomainSlot = isDomainSlot;
        DomainHint = null;
    }

    /// <summary>Create a spell slot with a specific spell already prepared.</summary>
    public SpellSlot(int level, SpellData spell, bool isDomainSlot = false)
    {
        Level = level;
        PreparedSpell = spell;
        IsUsed = false;
        DisabledByNegativeLevel = false;
        IsDomainSlot = isDomainSlot;
        DomainHint = null;
    }

    /// <summary>Whether this slot can be cast (has a spell, isn't used, and isn't disabled).</summary>
    public bool CanCast => PreparedSpell != null && !IsUsed && !DisabledByNegativeLevel;

    /// <summary>Whether this slot has a spell prepared (regardless of used status).</summary>
    public bool HasSpell => PreparedSpell != null;

    /// <summary>Whether this slot is empty (no spell prepared).</summary>
    public bool IsEmpty => PreparedSpell == null;

    /// <summary>Mark this slot as used (spell has been cast).</summary>
    public void Cast()
    {
        IsUsed = true;
    }

    /// <summary>
    /// Restore this slot after rest. Marks as not used.
    /// The prepared spell stays the same unless the wizard re-prepares.
    /// </summary>
    public void Rest()
    {
        IsUsed = false;
    }

    /// <summary>Clear this slot (remove prepared spell and reset used status).</summary>
    public void Clear()
    {
        PreparedSpell = null;
        IsUsed = false;
    }

    /// <summary>Prepare a spell in this slot (replaces any existing spell).</summary>
    public void Prepare(SpellData spell)
    {
        PreparedSpell = spell;
        IsUsed = false;
    }

    public override string ToString()
    {
        string spellName = PreparedSpell != null ? PreparedSpell.Name : "(empty)";
        string status = DisabledByNegativeLevel ? "DISABLED" : (IsUsed ? "USED" : "ready");
        string domainTag = IsDomainSlot ? " [DOMAIN]" : string.Empty;
        return $"Lv{Level}{domainTag} [{spellName}] ({status})";
    }
}
