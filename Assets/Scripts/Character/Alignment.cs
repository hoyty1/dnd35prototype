using System.Collections.Generic;

/// <summary>
/// D&D 3.5e Alignment system.
/// Two axes: Law/Chaos and Good/Evil, producing 9 alignments.
/// </summary>
public enum Alignment
{
    None = 0,         // Unset / not selected
    LawfulGood,       // LG
    NeutralGood,      // NG
    ChaoticGood,      // CG
    LawfulNeutral,    // LN
    TrueNeutral,      // TN / N
    ChaoticNeutral,   // CN
    LawfulEvil,       // LE
    NeutralEvil,      // NE
    ChaoticEvil       // CE
}

/// <summary>
/// Utility methods for the Alignment enum.
/// Provides display names, abbreviations, descriptions, and class restriction checks.
/// </summary>
public static class AlignmentHelper
{
    /// <summary>Grid order for 3x3 display (row-major: top-left = LG).</summary>
    public static readonly Alignment[] GridOrder = new Alignment[]
    {
        Alignment.LawfulGood,    Alignment.NeutralGood,    Alignment.ChaoticGood,
        Alignment.LawfulNeutral, Alignment.TrueNeutral,    Alignment.ChaoticNeutral,
        Alignment.LawfulEvil,    Alignment.NeutralEvil,    Alignment.ChaoticEvil
    };

    private static readonly Dictionary<Alignment, string> _fullNames = new Dictionary<Alignment, string>
    {
        { Alignment.LawfulGood,    "Lawful Good" },
        { Alignment.NeutralGood,   "Neutral Good" },
        { Alignment.ChaoticGood,   "Chaotic Good" },
        { Alignment.LawfulNeutral, "Lawful Neutral" },
        { Alignment.TrueNeutral,   "True Neutral" },
        { Alignment.ChaoticNeutral,"Chaotic Neutral" },
        { Alignment.LawfulEvil,    "Lawful Evil" },
        { Alignment.NeutralEvil,   "Neutral Evil" },
        { Alignment.ChaoticEvil,   "Chaotic Evil" },
    };

    private static readonly Dictionary<Alignment, string> _abbreviations = new Dictionary<Alignment, string>
    {
        { Alignment.LawfulGood,    "LG" },
        { Alignment.NeutralGood,   "NG" },
        { Alignment.ChaoticGood,   "CG" },
        { Alignment.LawfulNeutral, "LN" },
        { Alignment.TrueNeutral,   "TN" },
        { Alignment.ChaoticNeutral,"CN" },
        { Alignment.LawfulEvil,    "LE" },
        { Alignment.NeutralEvil,   "NE" },
        { Alignment.ChaoticEvil,   "CE" },
    };

    private static readonly Dictionary<Alignment, string> _descriptions = new Dictionary<Alignment, string>
    {
        { Alignment.LawfulGood,    "A lawful good character acts as a good person is expected to. Combining honor and compassion." },
        { Alignment.NeutralGood,   "A neutral good character does the best they can to help others according to their needs." },
        { Alignment.ChaoticGood,   "A chaotic good character acts as their conscience directs with little regard for rules." },
        { Alignment.LawfulNeutral, "A lawful neutral character acts in accordance with law, tradition, or a personal code." },
        { Alignment.TrueNeutral,   "A neutral character does what seems like a good idea, without strong moral convictions." },
        { Alignment.ChaoticNeutral,"A chaotic neutral character follows their whims. An individualist first and last." },
        { Alignment.LawfulEvil,    "A lawful evil character methodically takes what they want within the limits of a code." },
        { Alignment.NeutralEvil,   "A neutral evil character does whatever they can get away with, without compassion." },
        { Alignment.ChaoticEvil,   "A chaotic evil character acts with arbitrary violence, driven by greed and spite." },
    };

    /// <summary>Get the full display name (e.g., "Lawful Good").</summary>
    public static string GetFullName(Alignment a)
    {
        return _fullNames.TryGetValue(a, out string name) ? name : "Unknown";
    }

    /// <summary>Get the abbreviation (e.g., "LG").</summary>
    public static string GetAbbreviation(Alignment a)
    {
        return _abbreviations.TryGetValue(a, out string abbr) ? abbr : "??";
    }

    /// <summary>Get the description text for an alignment.</summary>
    public static string GetDescription(Alignment a)
    {
        return _descriptions.TryGetValue(a, out string desc) ? desc : "";
    }

    /// <summary>Whether this alignment is on the Lawful axis.</summary>
    public static bool IsLawful(Alignment a)
    {
        return a == Alignment.LawfulGood || a == Alignment.LawfulNeutral || a == Alignment.LawfulEvil;
    }

    /// <summary>Whether this alignment is on the Chaotic axis.</summary>
    public static bool IsChaotic(Alignment a)
    {
        return a == Alignment.ChaoticGood || a == Alignment.ChaoticNeutral || a == Alignment.ChaoticEvil;
    }

    /// <summary>Whether this alignment is on the Good axis.</summary>
    public static bool IsGood(Alignment a)
    {
        return a == Alignment.LawfulGood || a == Alignment.NeutralGood || a == Alignment.ChaoticGood;
    }

    /// <summary>Whether this alignment is on the Evil axis.</summary>
    public static bool IsEvil(Alignment a)
    {
        return a == Alignment.LawfulEvil || a == Alignment.NeutralEvil || a == Alignment.ChaoticEvil;
    }

    /// <summary>Whether this alignment is Neutral on the Law/Chaos axis.</summary>
    public static bool IsNeutralLC(Alignment a)
    {
        return a == Alignment.NeutralGood || a == Alignment.TrueNeutral || a == Alignment.NeutralEvil;
    }

    /// <summary>Whether this alignment is Neutral on the Good/Evil axis.</summary>
    public static bool IsNeutralGE(Alignment a)
    {
        return a == Alignment.LawfulNeutral || a == Alignment.TrueNeutral || a == Alignment.ChaoticNeutral;
    }

    /// <summary>
    /// Check if two alignments are within one step of each other.
    /// D&D 3.5e rule: a cleric's alignment must be within one step of their deity's alignment.
    /// One step means they can differ on one axis but not both.
    /// </summary>
    public static bool IsWithinOneStep(Alignment a, Alignment b)
    {
        if (a == Alignment.None || b == Alignment.None) return false;
        int axisA_LC = GetLCAxis(a); // -1=chaotic, 0=neutral, 1=lawful
        int axisA_GE = GetGEAxis(a); // -1=evil, 0=neutral, 1=good
        int axisB_LC = GetLCAxis(b);
        int axisB_GE = GetGEAxis(b);

        int diffLC = System.Math.Abs(axisA_LC - axisB_LC);
        int diffGE = System.Math.Abs(axisA_GE - axisB_GE);

        // Within one step: each axis can differ by at most 1
        return diffLC <= 1 && diffGE <= 1;
    }

    /// <summary>Get Law/Chaos axis value: 1=Lawful, 0=Neutral, -1=Chaotic.</summary>
    private static int GetLCAxis(Alignment a)
    {
        if (IsLawful(a)) return 1;
        if (IsChaotic(a)) return -1;
        return 0;
    }

    /// <summary>Get Good/Evil axis value: 1=Good, 0=Neutral, -1=Evil.</summary>
    private static int GetGEAxis(Alignment a)
    {
        if (IsGood(a)) return 1;
        if (IsEvil(a)) return -1;
        return 0;
    }

    /// <summary>
    /// Check if a given alignment is valid for a character class per D&D 3.5e rules.
    /// Returns true if the alignment is allowed, false otherwise.
    /// </summary>
    public static bool IsAlignmentValidForClass(Alignment alignment, string className)
    {
        if (string.IsNullOrEmpty(className)) return true;

        switch (className)
        {
            case "Monk":
                // Monks must be Lawful (any lawful alignment)
                return IsLawful(alignment);

            case "Barbarian":
                // Barbarians cannot be Lawful
                return !IsLawful(alignment);

            case "Paladin":
                // Paladins must be Lawful Good (not yet implemented, but for future)
                return alignment == Alignment.LawfulGood;

            case "Druid":
                // Druids must have at least one Neutral component
                return alignment == Alignment.NeutralGood || alignment == Alignment.LawfulNeutral
                    || alignment == Alignment.TrueNeutral || alignment == Alignment.ChaoticNeutral
                    || alignment == Alignment.NeutralEvil;

            default:
                // Fighter, Rogue, Cleric, Wizard, etc. — any alignment
                return true;
        }
    }

    /// <summary>
    /// Get a restriction description for a class, or empty string if no restrictions.
    /// </summary>
    public static string GetClassRestrictionText(string className)
    {
        switch (className)
        {
            case "Monk":      return "Monks must be Lawful.";
            case "Barbarian":  return "Barbarians cannot be Lawful.";
            case "Paladin":    return "Paladins must be Lawful Good.";
            case "Druid":      return "Druids must have a Neutral component.";
            default:           return "";
        }
    }
}
