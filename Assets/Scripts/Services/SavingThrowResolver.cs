using UnityEngine;

// ============================================================================
// D&D 3.5 Saving Throw Resolver - Centralized saving throw calculations
// ============================================================================

/// <summary>
/// Centralized saving throw resolution service. Extracts common saving throw
/// logic from CharacterController (disease, poison, coup de grace, etc.)
/// into a single authoritative source with consistent logging.
/// </summary>
public static class SavingThrowResolver
{
    // ========================================================================
    // ENUMS
    // ========================================================================

    /// <summary>
    /// The three D&amp;D 3.5e saving throw types.
    /// </summary>
    public enum SaveType
    {
        /// <summary>Fortitude save (CON-based). Resists poison, disease, death effects.</summary>
        Fortitude,
        /// <summary>Reflex save (DEX-based). Resists area effects, traps.</summary>
        Reflex,
        /// <summary>Will save (WIS-based). Resists mind-affecting, charms, compulsions.</summary>
        Will
    }

    // ========================================================================
    // RESULT STRUCTURES
    // ========================================================================

    /// <summary>
    /// Complete result of a saving throw resolution.
    /// </summary>
    public struct SaveResult
    {
        /// <summary>Type of save made.</summary>
        public SaveType Type;
        /// <summary>The raw d20 roll.</summary>
        public int Roll;
        /// <summary>The total save modifier (base + ability + feats + conditions).</summary>
        public int Modifier;
        /// <summary>Total result (Roll + Modifier).</summary>
        public int Total;
        /// <summary>The DC the save was made against.</summary>
        public int DC;
        /// <summary>Whether the save succeeded (Total >= DC).</summary>
        public bool Succeeded;
        /// <summary>Name of the effect being saved against.</summary>
        public string EffectName;
        /// <summary>Formatted log message for CombatUI display.</summary>
        public string LogMessage;
    }

    // ========================================================================
    // SAVE MODIFIER CALCULATION
    // ========================================================================

    /// <summary>
    /// Get the total saving throw modifier for a specific save type.
    /// Combines base save, ability modifier, feat bonuses, and condition modifiers.
    /// </summary>
    /// <param name="stats">Character's stats.</param>
    /// <param name="saveType">The type of saving throw.</param>
    /// <returns>Total save modifier.</returns>
    public static int GetSaveModifier(CharacterStats stats, SaveType saveType)
    {
        if (stats == null) return 0;

        switch (saveType)
        {
            case SaveType.Fortitude:
                return stats.FortitudeSave;
            case SaveType.Reflex:
                return stats.ReflexSave;
            case SaveType.Will:
                return stats.WillSave;
            default:
                return 0;
        }
    }

    /// <summary>
    /// Get the save type name as a display string.
    /// </summary>
    /// <param name="saveType">The save type.</param>
    /// <returns>Display name (e.g. "Fortitude", "Reflex", "Will").</returns>
    public static string GetSaveTypeName(SaveType saveType)
    {
        switch (saveType)
        {
            case SaveType.Fortitude: return "Fortitude";
            case SaveType.Reflex: return "Reflex";
            case SaveType.Will: return "Will";
            default: return "Unknown";
        }
    }

    // ========================================================================
    // SAVING THROW RESOLUTION
    // ========================================================================

    /// <summary>
    /// Resolve a saving throw: roll d20, add modifier, compare to DC.
    /// This is the primary method for all saving throw checks.
    /// </summary>
    /// <param name="stats">Character's stats.</param>
    /// <param name="saveType">The type of saving throw.</param>
    /// <param name="dc">The difficulty class to beat.</param>
    /// <param name="effectName">Name of the effect being saved against (for logging).</param>
    /// <returns>Complete save result with roll, modifier, total, and success.</returns>
    public static SaveResult ResolveSave(CharacterStats stats, SaveType saveType, int dc, string effectName = null)
    {
        string characterName = stats?.CharacterName ?? "Unknown";
        string saveTypeName = GetSaveTypeName(saveType);
        string context = $"{characterName} {saveTypeName} save vs {effectName ?? "effect"}";

        int roll = DiceService.D20(context);
        int modifier = GetSaveModifier(stats, saveType);
        int total = roll + modifier;
        bool succeeded = total >= dc;

        var result = new SaveResult
        {
            Type = saveType,
            Roll = roll,
            Modifier = modifier,
            Total = total,
            DC = dc,
            Succeeded = succeeded,
            EffectName = effectName,
            LogMessage = CombatLogger.FormatSavingThrow(
                characterName, saveTypeName, roll, modifier, total, dc, succeeded, effectName)
        };

        return result;
    }

    /// <summary>
    /// Resolve a Fortitude save. Convenience wrapper for ResolveSave.
    /// </summary>
    /// <param name="stats">Character's stats.</param>
    /// <param name="dc">The difficulty class to beat.</param>
    /// <param name="effectName">Name of the effect (for logging).</param>
    /// <returns>Complete save result.</returns>
    public static SaveResult ResolveFortitudeSave(CharacterStats stats, int dc, string effectName = null)
    {
        return ResolveSave(stats, SaveType.Fortitude, dc, effectName);
    }

    /// <summary>
    /// Resolve a Reflex save. Convenience wrapper for ResolveSave.
    /// </summary>
    /// <param name="stats">Character's stats.</param>
    /// <param name="dc">The difficulty class to beat.</param>
    /// <param name="effectName">Name of the effect (for logging).</param>
    /// <returns>Complete save result.</returns>
    public static SaveResult ResolveReflexSave(CharacterStats stats, int dc, string effectName = null)
    {
        return ResolveSave(stats, SaveType.Reflex, dc, effectName);
    }

    /// <summary>
    /// Resolve a Will save. Convenience wrapper for ResolveSave.
    /// </summary>
    /// <param name="stats">Character's stats.</param>
    /// <param name="dc">The difficulty class to beat.</param>
    /// <param name="effectName">Name of the effect (for logging).</param>
    /// <returns>Complete save result.</returns>
    public static SaveResult ResolveWillSave(CharacterStats stats, int dc, string effectName = null)
    {
        return ResolveSave(stats, SaveType.Will, dc, effectName);
    }

    // ========================================================================
    // SPECIALIZED SAVES
    // ========================================================================

    /// <summary>
    /// Resolve a Fortitude save against poison.
    /// D&amp;D 3.5e DMG p.296: Poison requires a Fortitude save against the poison's DC.
    /// </summary>
    /// <param name="stats">Character's stats.</param>
    /// <param name="dc">The poison's save DC.</param>
    /// <param name="poisonName">Name of the poison.</param>
    /// <param name="isSecondary">Whether this is the secondary save (occurs 1 minute later).</param>
    /// <returns>Complete save result.</returns>
    public static SaveResult ResolvePoisonSave(CharacterStats stats, int dc, string poisonName, bool isSecondary = false)
    {
        string effectLabel = isSecondary
            ? $"{poisonName} (secondary)"
            : $"{poisonName} (initial)";
        return ResolveFortitudeSave(stats, dc, effectLabel);
    }

    /// <summary>
    /// Resolve a Fortitude save against disease.
    /// D&amp;D 3.5e DMG p.292: Disease requires Fortitude saves.
    /// </summary>
    /// <param name="stats">Character's stats.</param>
    /// <param name="dc">The disease's save DC.</param>
    /// <param name="diseaseName">Name of the disease.</param>
    /// <param name="isDaily">Whether this is a daily save to track recovery.</param>
    /// <returns>Complete save result.</returns>
    public static SaveResult ResolveDiseaseSave(CharacterStats stats, int dc, string diseaseName, bool isDaily = false)
    {
        string effectLabel = isDaily
            ? $"{diseaseName} (daily)"
            : $"{diseaseName} (exposure)";
        return ResolveFortitudeSave(stats, dc, effectLabel);
    }

    /// <summary>
    /// Resolve a Fortitude save against a coup de grace.
    /// D&amp;D 3.5e PHB p.153: DC = 10 + damage dealt. On failure, the target dies.
    /// </summary>
    /// <param name="stats">Character's stats (the target).</param>
    /// <param name="damageDealt">Total damage dealt by the coup de grace.</param>
    /// <returns>Complete save result.</returns>
    public static SaveResult ResolveCoupDeGraceSave(CharacterStats stats, int damageDealt)
    {
        int dc = 10 + damageDealt;
        return ResolveFortitudeSave(stats, dc, "Coup de Grace");
    }

    /// <summary>
    /// Resolve a Will save against a disturbance (e.g. nearby combat, loud noise).
    /// </summary>
    /// <param name="stats">Character's stats.</param>
    /// <param name="dc">The disturbance DC.</param>
    /// <param name="source">Description of the disturbance source.</param>
    /// <returns>Complete save result.</returns>
    public static SaveResult ResolveDisturbanceSave(CharacterStats stats, int dc, string source = null)
    {
        return ResolveWillSave(stats, dc, source ?? "disturbance");
    }

    // ========================================================================
    // UTILITY
    // ========================================================================

    /// <summary>
    /// Quick check: roll a save and return just success/failure.
    /// Useful when the caller doesn't need the full result details.
    /// </summary>
    /// <param name="stats">Character's stats.</param>
    /// <param name="saveType">The type of saving throw.</param>
    /// <param name="dc">The difficulty class to beat.</param>
    /// <returns>True if the save succeeded.</returns>
    public static bool QuickSave(CharacterStats stats, SaveType saveType, int dc)
    {
        int roll = DiceService.D20();
        int modifier = GetSaveModifier(stats, saveType);
        return (roll + modifier) >= dc;
    }

    /// <summary>
    /// Convert a SavingThrowType string (from SpellData) to a SaveType enum.
    /// </summary>
    /// <param name="savingThrowType">String save type from spell data (e.g. "Fortitude", "Reflex", "Will").</param>
    /// <returns>Corresponding SaveType enum value.</returns>
    public static SaveType ParseSaveType(string savingThrowType)
    {
        if (string.IsNullOrEmpty(savingThrowType))
            return SaveType.Will; // Default

        string lower = savingThrowType.ToLower().Trim();
        if (lower.StartsWith("fort"))
            return SaveType.Fortitude;
        if (lower.StartsWith("ref"))
            return SaveType.Reflex;
        return SaveType.Will;
    }
}
