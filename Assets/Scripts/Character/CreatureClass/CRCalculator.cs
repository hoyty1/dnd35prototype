using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Calculates Challenge Rating for creatures with class levels (D&D 3.5e DMG p.296).
///
/// Rules:
/// - Associated class: +1 CR per class level
/// - Nonassociated class: +1 CR per 2 class levels (until levels exceed racial HD)
/// - NPC class levels: +1 CR per 2 levels (effectively nonassociated for CR)
/// - Fractional CRs: 1/8, 1/6, 1/4, 1/3, 1/2 for very weak creatures
/// </summary>
public static class CRCalculator
{
    /// <summary>
    /// Fractional CR values as strings for very weak creatures.
    /// </summary>
    public static readonly string[] FractionalCRs = { "1/8", "1/6", "1/4", "1/3", "1/2" };

    /// <summary>
    /// Convert a CR string (which may be fractional like "1/2") to a float.
    /// </summary>
    public static float CRToFloat(string cr)
    {
        if (string.IsNullOrEmpty(cr)) return 0f;
        cr = cr.Trim();

        if (cr.Contains("/"))
        {
            string[] parts = cr.Split('/');
            if (parts.Length == 2 && float.TryParse(parts[0], out float num) && float.TryParse(parts[1], out float den) && den != 0)
                return num / den;
            return 0f;
        }

        if (float.TryParse(cr, out float val)) return val;
        return 0f;
    }

    /// <summary>
    /// Convert a float CR to the best string representation.
    /// </summary>
    public static string FloatToCR(float cr)
    {
        if (cr <= 0.13f) return "1/8";
        if (cr <= 0.17f) return "1/6";
        if (cr <= 0.26f) return "1/4";
        if (cr <= 0.34f) return "1/3";
        if (cr <= 0.75f) return "1/2";
        return Mathf.RoundToInt(cr).ToString();
    }

    /// <summary>
    /// Calculate the CR adjustment from adding class levels to a creature.
    ///
    /// Associated class: +1 CR per level
    /// Nonassociated class: +1 CR per 2 levels (first levels that don't exceed racial HD)
    /// NPC classes: always treated as +1 CR per 2 levels for CR purposes
    /// </summary>
    public static int CalculateCRAdjustment(string creatureType, string className, int classLevels, int racialHD)
    {
        if (classLevels <= 0) return 0;

        bool isNPCClass = ClassAssociationRules.IsNPCClass(className);
        bool isAssociated = !isNPCClass && ClassAssociationRules.IsAssociatedClass(creatureType, className);

        if (isAssociated)
        {
            // Associated: +1 CR per class level
            return classLevels;
        }
        else
        {
            // Nonassociated (or NPC class): +1 CR per 2 class levels
            // But once class levels exceed racial HD, they count as +1 each
            if (classLevels <= racialHD)
            {
                // All levels below or equal to racial HD: +1 per 2
                return classLevels / 2;
            }
            else
            {
                // Mixed: first racialHD levels at half rate, rest at full rate
                int halfRateLevels = racialHD;
                int fullRateLevels = classLevels - racialHD;
                return halfRateLevels / 2 + fullRateLevels;
            }
        }
    }

    /// <summary>
    /// Calculate total CR for a creature with base CR and added class levels.
    /// </summary>
    public static int CalculateTotalCR(string baseCR, string creatureType, string className, int classLevels, int racialHD)
    {
        float baseCRFloat = CRToFloat(baseCR);
        int baseCRInt = Mathf.Max(0, Mathf.RoundToInt(baseCRFloat));

        int adjustment = CalculateCRAdjustment(creatureType, className, classLevels, racialHD);
        return baseCRInt + adjustment;
    }

    /// <summary>
    /// Calculate CR for a PC-classed NPC (no racial HD).
    /// CR = class level - 1 (for single-classed NPCs, DMG p.127).
    /// </summary>
    public static int CalculateNPCCR(string className, int level)
    {
        if (ClassAssociationRules.IsNPCClass(className))
        {
            // NPC class: CR = level / 2 (rounded down), minimum 1/2
            return Mathf.Max(0, level / 2);
        }
        // PC class: CR = level - 1 (level 1 is CR 1 for encounters)
        return Mathf.Max(1, level - 1);
    }

    /// <summary>
    /// Get the standard CR for a class at the given level (used for templates).
    /// Matches DMG Chapter 4 conventions.
    /// </summary>
    public static int GetStandardCR(string className, int level)
    {
        // NPC classes use lower CR
        if (ClassAssociationRules.IsNPCClass(className))
        {
            if (level <= 1) return 0; // CR 1/2 or less, stored as 0
            return Mathf.Max(1, (level + 1) / 2);
        }

        // PC classes: CR ≈ level - 1, minimum 1
        return Mathf.Max(1, level - 1);
    }
}
