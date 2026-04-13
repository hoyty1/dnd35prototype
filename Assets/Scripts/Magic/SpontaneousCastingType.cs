/// <summary>
/// D&D 3.5e Spontaneous Casting type for Clerics (PHB p.32).
///
/// Good clerics (or neutral clerics who choose this) can spontaneously convert
/// any prepared spell (except domain spells) into a cure spell of the same level.
///
/// Evil clerics (or neutral clerics who choose this) can spontaneously convert
/// any prepared spell (except domain spells) into an inflict spell of the same level.
///
/// This choice is made at character creation and is permanent.
/// </summary>
public enum SpontaneousCastingType
{
    /// <summary>No spontaneous casting (non-clerics).</summary>
    None = 0,

    /// <summary>Can convert prepared spells to Cure spells (good clerics / neutral choice).</summary>
    Cure,

    /// <summary>Can convert prepared spells to Inflict spells (evil clerics / neutral choice).</summary>
    Inflict
}

/// <summary>
/// Maps spell levels to the corresponding Cure/Inflict spell IDs for spontaneous casting.
/// D&D 3.5e PHB: Cure Minor Wounds (0), Cure Light Wounds (1), Cure Moderate Wounds (2), etc.
/// </summary>
public static class SpontaneousCastingHelper
{
    /// <summary>
    /// Get the spell ID for spontaneous casting at a given spell level.
    /// Returns null if no spontaneous spell exists for that level.
    /// </summary>
    public static string GetSpontaneousSpellId(SpontaneousCastingType type, int spellLevel)
    {
        if (type == SpontaneousCastingType.None) return null;

        if (type == SpontaneousCastingType.Cure)
        {
            switch (spellLevel)
            {
                case 0: return "cure_minor_wounds";
                case 1: return "cure_light_wounds";
                case 2: return "cure_moderate_wounds";
                // Future levels:
                // case 3: return "cure_serious_wounds";
                // case 4: return "cure_critical_wounds";
                default: return null;
            }
        }
        else // Inflict
        {
            switch (spellLevel)
            {
                case 0: return "inflict_minor_wounds";
                case 1: return "inflict_light_wounds";
                case 2: return "inflict_moderate_wounds";
                // Future levels:
                // case 3: return "inflict_serious_wounds";
                // case 4: return "inflict_critical_wounds";
                default: return null;
            }
        }
    }

    /// <summary>
    /// Get the display name for the spontaneous casting type.
    /// </summary>
    public static string GetDisplayName(SpontaneousCastingType type)
    {
        switch (type)
        {
            case SpontaneousCastingType.Cure: return "Cure Spells";
            case SpontaneousCastingType.Inflict: return "Inflict Spells";
            default: return "None";
        }
    }

    /// <summary>
    /// Determine the default spontaneous casting type based on alignment.
    /// Good clerics get Cure, Evil clerics get Inflict.
    /// Neutral (on Good/Evil axis) clerics must choose — returns None to indicate choice needed.
    /// </summary>
    public static SpontaneousCastingType GetDefaultForAlignment(Alignment alignment)
    {
        if (AlignmentHelper.IsGood(alignment))
            return SpontaneousCastingType.Cure;
        if (AlignmentHelper.IsEvil(alignment))
            return SpontaneousCastingType.Inflict;
        // Neutral on Good/Evil axis — must choose
        return SpontaneousCastingType.None;
    }
}
