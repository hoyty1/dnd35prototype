using DND35e.Identifiers;
using System;

// ════════════════════════════════════════════════════════════════════════════
//  Stored Spell — D&D 3.5e Sprint 3 Spell Storage System
//  Represents a spell stored inside a Ring of Spell Storing (Minor/Major).
//  Preserves the original caster's CL and save DC per DMG p.232.
//
//  DMG p.232: "A spellcaster can cast any spells into the ring, so long as
//  the total spell levels do not add up to more than [3/5]. [...] The user
//  need not provide any material components or focus, or pay an XP cost to
//  cast the spell, and there is no arcane spell failure chance for wearing
//  armor (because the ring wearer need not gesture). The activation time for
//  the ring is the same as the casting time for the relevant spell."
// ════════════════════════════════════════════════════════════════════════════

[Serializable]
public class StoredSpell
{
    /// <summary>SpellId matching SpellDatabase keys (e.g., SpellNames.FIREBALL).</summary>
    public string SpellId;

    /// <summary>Display name of the stored spell.</summary>
    public string SpellName;

    /// <summary>Caster level at which the spell was stored. Used when casting from ring.</summary>
    public int CasterLevel;

    /// <summary>Save DC at which the spell was stored. Used when casting from ring.</summary>
    public int SaveDC;

    /// <summary>Spell level (1-9). Used for capacity tracking.</summary>
    public int SpellLevel;

    /// <summary>Name of the character who stored this spell.</summary>
    public string StoredBy;

    public StoredSpell() { }

    public StoredSpell(string spellId, string spellName, int casterLevel, int saveDC, int spellLevel, string storedBy = "")
    {
        SpellId = spellId;
        SpellName = spellName;
        CasterLevel = casterLevel;
        SaveDC = saveDC;
        SpellLevel = spellLevel;
        StoredBy = storedBy;
    }

    /// <summary>Deep clone for item cloning.</summary>
    public StoredSpell Clone()
    {
        return new StoredSpell(SpellId, SpellName, CasterLevel, SaveDC, SpellLevel, StoredBy);
    }

    public override string ToString()
    {
        return $"{SpellName} (Lv{SpellLevel}, CL {CasterLevel}, DC {SaveDC})";
    }
}
