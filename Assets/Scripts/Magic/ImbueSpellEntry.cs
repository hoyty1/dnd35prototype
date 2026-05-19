// ============================================================================
// ImbueSpellEntry.cs — Data class for a single imbued spell transferred via
//                       Imbue with Spell Ability (PHB p.243).
//
// Tracks the spell data, caster level, save DC, and originating slot index
// so that when the target casts the imbued spell, the caster's slot can be
// unlocked.
// ============================================================================

/// <summary>
/// Represents a single spell transferred from a cleric (caster) to a
/// non-spellcaster (target) via Imbue with Spell Ability.
/// </summary>
[System.Serializable]
public class ImbueSpellEntry
{
    /// <summary>The spell that was transferred.</summary>
    public SpellData Spell;

    /// <summary>The original caster's caster level at the time of imbuing.</summary>
    public int CasterLevel;

    /// <summary>The save DC for this spell, calculated using the caster's stats.</summary>
    public int SaveDC;

    /// <summary>Index into the caster's SpellcastingComponent.SpellSlots that was locked for this spell.</summary>
    public int LockedSlotIndex;

    /// <summary>Name of the caster for display and logging.</summary>
    public string CasterName;

    public ImbueSpellEntry(SpellData spell, int casterLevel, int saveDC, int lockedSlotIndex, string casterName)
    {
        Spell = spell;
        CasterLevel = casterLevel;
        SaveDC = saveDC;
        LockedSlotIndex = lockedSlotIndex;
        CasterName = casterName;
    }

    public override string ToString()
    {
        return $"{Spell?.Name ?? "(null)"} (CL {CasterLevel}, DC {SaveDC}, slot #{LockedSlotIndex})";
    }
}
