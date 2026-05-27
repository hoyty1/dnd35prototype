using System;
using UnityEngine;

// ============================================================================
// StaffValidator.cs — D&D 3.5e staff activation validation (DMG p.243)
//
// CORE DMG 3.5e RULES ONLY:
//   - Spell trigger activation: spell must be on character's class list
//   - Use Magic Device DC 20 as fallback (flat DC, no scaling)
//   - No ability score check needed (same as wands)
//   - No caster level check needed (staff always uses its own CL)
//   - Magic Domain clerics can activate as wizard of half cleric level
//
// Mirrors WandValidator pattern for consistency.
// ============================================================================

public static class StaffValidator
{
    /// <summary>Result of staff usage validation.</summary>
    public class StaffValidationResult
    {
        public bool CanUse;
        public string FailureReason;
        public string MatchedClassName;   // Which class grants access (or "Use Magic Device")
        public bool NeedsUMDCheck;        // True if UMD is the only path
        public bool UsedUMD;              // True if UMD was used to bypass requirements
        public bool UsedMagicDomain;      // True if Magic domain power was used
        public int EffectiveWizardLevel;  // From Magic domain (0 if not used)

        /// <summary>Format a short summary for combat log.</summary>
        public string GetSummary()
        {
            if (!CanUse)
                return FailureReason ?? "Cannot use this staff.";
            if (UsedUMD)
                return "Using staff via Use Magic Device.";
            if (UsedMagicDomain)
                return $"Uses Magic domain power to activate staff as a wizard of level {EffectiveWizardLevel}.";
            return $"Staff activated as {MatchedClassName}.";
        }
    }

    // All caster classes — staves use spell trigger activation (same as wands)
    private static readonly string[] AllCasterClasses =
        { "Wizard", "Sorcerer", "Bard", "Cleric", "Druid", "Paladin", "Ranger" };

    // ── Public API ──

    /// <summary>
    /// Validate whether a character can use the given staff.
    /// Spell trigger: spell must be on the character's class list, OR UMD DC 20.
    /// This validates the STAFF itself — individual spell availability is checked separately.
    /// </summary>
    public static StaffValidationResult Validate(CharacterController character, ItemData staffItem)
    {
        var result = new StaffValidationResult { CanUse = false };

        if (character == null || character.Stats == null)
        {
            result.FailureReason = "No active character.";
            return result;
        }

        if (staffItem == null || !staffItem.IsStaff)
        {
            result.FailureReason = "This is not a staff.";
            return result;
        }

        // Check if staff is expended
        if (staffItem.StaffCharges <= 0)
        {
            result.FailureReason = $"{staffItem.Name} is expended (0 charges remaining). It is now a non-magical quarterstaff.";
            return result;
        }

        var staffDef = StaffDatabase.GetStaff(staffItem.StaffId);
        if (staffDef == null)
        {
            result.FailureReason = $"Staff definition not found for '{staffItem.StaffId}'.";
            return result;
        }

        CharacterStats stats = character.Stats;

        // ── Check AllowedClasses on the staff definition ──
        // D&D 3.5e: Staves require spell to be on character's class list.
        // We check if the character has any class that is in the staff's allowed list.
        if (staffDef.AllowedClasses != null && staffDef.AllowedClasses.Length > 0)
        {
            foreach (string allowedClass in staffDef.AllowedClasses)
            {
                int classLevel = stats.GetClassLevel(allowedClass);
                if (classLevel > 0)
                {
                    result.CanUse = true;
                    result.MatchedClassName = allowedClass;
                    return result;
                }
            }
        }
        else
        {
            // Empty AllowedClasses = any caster class can use it
            foreach (string cls in AllCasterClasses)
            {
                if (stats.GetClassLevel(cls) > 0)
                {
                    result.CanUse = true;
                    result.MatchedClassName = cls;
                    return result;
                }
            }
        }

        // ── Magic Domain check (D&D 3.5e PHB) ──
        // A cleric with the Magic domain can use spell trigger items as if they were
        // a wizard of half their cleric level.
        if (stats.HasMagicDomain)
        {
            int effectiveWizLevel = stats.MagicDomainEffectiveWizardLevel;
            if (effectiveWizLevel > 0)
            {
                // Check if Wizard is in AllowedClasses
                bool wizardAllowed = false;
                if (staffDef.AllowedClasses == null || staffDef.AllowedClasses.Length == 0)
                {
                    wizardAllowed = true; // any caster
                }
                else
                {
                    foreach (string cls in staffDef.AllowedClasses)
                    {
                        if (string.Equals(cls, "Wizard", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(cls, "Sorcerer", StringComparison.OrdinalIgnoreCase))
                        {
                            wizardAllowed = true;
                            break;
                        }
                    }
                }

                if (wizardAllowed)
                {
                    result.CanUse = true;
                    result.MatchedClassName = "Wizard (Magic Domain)";
                    result.UsedMagicDomain = true;
                    result.EffectiveWizardLevel = effectiveWizLevel;
                    return result;
                }
            }
        }

        // ── Use Magic Device fallback (DC 20) ──
        int umdBonus = stats.GetSkillBonus("Use Magic Device");
        if (umdBonus > 0)
        {
            result.CanUse = true;
            result.NeedsUMDCheck = true;
            result.MatchedClassName = "Use Magic Device";
            return result;
        }

        // No access at all
        result.FailureReason = $"{stats.CharacterName} cannot use {staffItem.Name}: " +
                               $"no matching class on the staff's allowed list. " +
                               $"(Requires {string.Join("/", staffDef.AllowedClasses ?? new[] { "any caster" })}, or Use Magic Device skill)";
        return result;
    }

    /// <summary>
    /// Perform a Use Magic Device check to activate a staff.
    /// DC = 20 (flat, same as wands — DMG p.243).
    /// </summary>
    public static bool PerformUMDCheck(CharacterController character, ItemData staffItem, out string summary)
    {
        summary = "";
        if (character == null || character.Stats == null || staffItem == null)
        {
            summary = "Invalid character or staff.";
            return false;
        }

        int umdBonus = character.Stats.GetSkillBonus("Use Magic Device");
        int dc = 20; // Flat DC 20 for staff activation via UMD
        int d20 = DiceRoller.D20();
        int total = d20 + umdBonus;

        if (total >= dc)
        {
            summary = $"Use Magic Device check passed (d20={d20}+{umdBonus}={total} vs DC {dc}).";
            return true;
        }
        else
        {
            summary = $"Use Magic Device check failed (d20={d20}+{umdBonus}={total} vs DC {dc}). " +
                       "Cannot activate this staff via UMD this round.";
            return false;
        }
    }

    /// <summary>
    /// Calculate save DC for a spell cast from a staff.
    /// D&D 3.5e: 10 + spell level + minimum ability modifier for that spell level.
    /// Same formula as wands (DMG p.243).
    /// </summary>
    public static int CalculateStaffSaveDC(int spellLevel)
    {
        // Minimum ability score to cast spell of level N = 10 + N
        // Modifier for that score = floor((10 + N - 10) / 2) = floor(N / 2)
        return 10 + spellLevel + (spellLevel / 2);
    }
}
