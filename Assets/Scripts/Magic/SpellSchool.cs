using System;

/// <summary>
/// D&D 3.5e spell schools.
/// </summary>
public enum SpellSchool
{
    None,
    Abjuration,
    Conjuration,
    Divination,
    Enchantment,
    Evocation,
    Illusion,
    Necromancy,
    Transmutation,
    Universal,
}

public static class SpellSchoolUtils
{
    public static SpellSchool Parse(string school)
    {
        if (string.IsNullOrWhiteSpace(school))
            return SpellSchool.None;

        string normalized = school.Trim().ToLowerInvariant();

        if (normalized.StartsWith("abjuration")) return SpellSchool.Abjuration;
        if (normalized.StartsWith("conjuration")) return SpellSchool.Conjuration;
        if (normalized.StartsWith("divination")) return SpellSchool.Divination;
        if (normalized.StartsWith("enchantment")) return SpellSchool.Enchantment;
        if (normalized.StartsWith("evocation")) return SpellSchool.Evocation;
        if (normalized.StartsWith("illusion")) return SpellSchool.Illusion;
        if (normalized.StartsWith("necromancy")) return SpellSchool.Necromancy;
        if (normalized.StartsWith("transmutation")) return SpellSchool.Transmutation;
        if (normalized.StartsWith("universal")) return SpellSchool.Universal;

        return SpellSchool.None;
    }
}
