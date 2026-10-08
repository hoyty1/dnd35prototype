using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unified D&D 3.5 size categories used by combat and spell systems.
/// </summary>
public enum SizeCategory
{
    Fine,
    Diminutive,
    Tiny,
    Small,
    Medium,
    Large,
    Huge,
    Gargantuan,
    Colossal
}

/// <summary>
/// Extension helpers for size-based combat math (AC/attack, grapple, space, reach).
/// </summary>
public static class SizeCategoryExtensions
{
    public static int GetAttackAndAcModifier(this SizeCategory size)
    {
        switch (size)
        {
            case SizeCategory.Fine: return +8;
            case SizeCategory.Diminutive: return +4;
            case SizeCategory.Tiny: return +2;
            case SizeCategory.Small: return +1;
            case SizeCategory.Medium: return 0;
            case SizeCategory.Large: return -1;
            case SizeCategory.Huge: return -2;
            case SizeCategory.Gargantuan: return -4;
            case SizeCategory.Colossal: return -8;
            default: return 0;
        }
    }

    public static int GetGrappleModifier(this SizeCategory size)
    {
        switch (size)
        {
            case SizeCategory.Fine: return -16;
            case SizeCategory.Diminutive: return -12;
            case SizeCategory.Tiny: return -8;
            case SizeCategory.Small: return -4;
            case SizeCategory.Medium: return 0;
            case SizeCategory.Large: return +4;
            case SizeCategory.Huge: return +8;
            case SizeCategory.Gargantuan: return +12;
            case SizeCategory.Colossal: return +16;
            default: return 0;
        }
    }

    public static int GetHideModifier(this SizeCategory size)
    {
        switch (size)
        {
            case SizeCategory.Fine: return +16;
            case SizeCategory.Diminutive: return +12;
            case SizeCategory.Tiny: return +8;
            case SizeCategory.Small: return +4;
            case SizeCategory.Medium: return 0;
            case SizeCategory.Large: return -4;
            case SizeCategory.Huge: return -8;
            case SizeCategory.Gargantuan: return -12;
            case SizeCategory.Colossal: return -16;
            default: return 0;
        }
    }

    /// <summary>
    /// D&D 3.5 natural reach in feet.
    /// Tall creatures (humanoids/giants) have longer natural reach at Large+.
    /// Long creatures (quadrupeds/serpentine) have shorter reach at the same size.
    /// </summary>
    public static int GetNaturalReachFeet(this SizeCategory size, bool isTallCreature = true)
    {
        switch (size)
        {
            case SizeCategory.Fine:
            case SizeCategory.Diminutive:
            case SizeCategory.Tiny:
            case SizeCategory.Small:
            case SizeCategory.Medium:
                return 5;

            case SizeCategory.Large:
                return isTallCreature ? 10 : 5;

            case SizeCategory.Huge:
                return isTallCreature ? 15 : 10;

            case SizeCategory.Gargantuan:
                return isTallCreature ? 20 : 15;

            case SizeCategory.Colossal:
                return isTallCreature ? 30 : 20;

            default:
                return 5;
        }
    }

    /// <summary>
    /// Grid-square approximation of natural reach (5 ft per square, minimum 1).
    /// </summary>
    public static int GetNaturalReachSquares(this SizeCategory size, bool isTallCreature = true)
    {
        return Mathf.Max(1, GetNaturalReachFeet(size, isTallCreature) / 5);
    }

    /// <summary>
    /// Number of grid squares along one edge of the creature's occupied footprint.
    /// </summary>
    public static int GetSpaceWidthSquares(this SizeCategory size)
    {
        switch (size)
        {
            case SizeCategory.Fine:
            case SizeCategory.Diminutive:
            case SizeCategory.Tiny:
            case SizeCategory.Small:
            case SizeCategory.Medium:
                return 1; // 1x1
            case SizeCategory.Large:
                return 2; // 2x2
            case SizeCategory.Huge:
                return 3; // 3x3
            case SizeCategory.Gargantuan:
                return 4; // 4x4
            case SizeCategory.Colossal:
                return 6; // 6x6
            default:
                return 1;
        }
    }

    /// <summary>
    /// Visual token scale multiplier relative to a Medium token.
    /// Keeps Small creatures visibly smaller while preserving 1x1 footprint occupancy.
    /// </summary>
    public static float GetVisualTokenScale(this SizeCategory size)
    {
        switch (size)
        {
            case SizeCategory.Fine: return 0.25f;
            case SizeCategory.Diminutive: return 0.4f;
            case SizeCategory.Tiny: return 0.6f;
            case SizeCategory.Small: return 0.75f;
            case SizeCategory.Medium: return 1f;
            case SizeCategory.Large: return 2f;
            case SizeCategory.Huge: return 3f;
            case SizeCategory.Gargantuan: return 4f;
            case SizeCategory.Colossal: return 6f;
            default: return 1f;
        }
    }

    /// <summary>
    /// Total occupied squares in the creature footprint.
    /// </summary>
    public static int GetSpaceSquares(this SizeCategory size)
    {
        int width = GetSpaceWidthSquares(size);
        return width * width;
    }

    public static bool TryIncrease(this SizeCategory size, out SizeCategory increased)
    {
        if (size >= SizeCategory.Colossal)
        {
            increased = SizeCategory.Colossal;
            return false;
        }

        increased = size + 1;
        return true;
    }

    public static bool TryDecrease(this SizeCategory size, out SizeCategory decreased)
    {
        if (size <= SizeCategory.Fine)
        {
            decreased = SizeCategory.Fine;
            return false;
        }

        decreased = size - 1;
        return true;
    }
}

/// <summary>
/// D&D 3.5e weapon and natural attack damage scaling by size category.
///
/// Source: DMG Tables 2-2 (increasing) and 2-3 (decreasing weapon damage by size), p.28. Both tables list a
/// weapon's Medium damage and its damage one to four size categories larger or smaller, so a Medium weapon covers
/// Fine (four smaller) to Colossal (four larger). The rows below are those tables, one per Medium damage value,
/// indexed by <see cref="SizeCategory"/> (Fine 0 ... Medium 4 ... Colossal 8), so index 4 is always the key.
/// "-" marks a step the DMG leaves blank: the weapon would deal less than 1 point, which the DMG says has no
/// effect (it is no longer a weapon). The game has no zero-damage weapon state, so such a step keeps 1 point
/// (CMB-133).
///
/// A source that is not Medium (a natural attack of a Large creature, for example) has no DMG row of its own.
/// It is scaled one size category at a time: each step reads the "One" column of the row whose Medium damage
/// equals the current damage, the way MM Table 4-3 (p.291) repeats its one-category increase for each category.
/// Applied from Medium, these single steps give exactly the multi-step columns of both DMG tables. Values the
/// tables reach but do not list as a row (3d6, 4d6, 6d6, 8d6, 3d8, 4d8, 6d8, 8d8) step along the ladders the tables
/// themselves use (3d6, 4d6, 6d6, 8d6, 12d6 and 3d8, 4d8, 6d8, 8d8, 12d8); those extension steps are not printed
/// in the DMG. A value outside both (1d20, 3d4, ...) is not scaled.
/// </summary>
public static class WeaponDamageScaler
{
    /// <summary>A step the DMG leaves blank: less than 1 point, no longer a weapon (DMG p.28).</summary>
    public const string NoDamage = "-";

    // Keys are the damage at Medium; values are indexed by SizeCategory, Fine (0) to Colossal (8).
    // Fine..Small come from DMG Table 2-3 (four to one categories smaller), Large..Colossal from Table 2-2.
    private static readonly Dictionary<string, string[]> MediumReferenceProgressions = new Dictionary<string, string[]>
    {
        //                Fine      Dimin.    Tiny      Small     Medium  Large   Huge    Garg.   Colossal
        { "1d2",  new[] { NoDamage, NoDamage, NoDamage, "1",      "1d2",  "1d3",  "1d4",  "1d6",  "1d8"  } },
        { "1d3",  new[] { NoDamage, NoDamage, "1",      "1d2",    "1d3",  "1d4",  "1d6",  "1d8",  "2d6"  } },
        { "1d4",  new[] { NoDamage, "1",      "1d2",    "1d3",    "1d4",  "1d6",  "1d8",  "2d6",  "3d6"  } },
        { "1d6",  new[] { "1",      "1d2",    "1d3",    "1d4",    "1d6",  "1d8",  "2d6",  "3d6",  "4d6"  } },
        { "1d8",  new[] { "1d2",    "1d3",    "1d4",    "1d6",    "1d8",  "2d6",  "3d6",  "4d6",  "6d6"  } },
        { "1d10", new[] { "1d3",    "1d4",    "1d6",    "1d8",    "1d10", "2d8",  "3d8",  "4d8",  "6d8"  } },
        { "1d12", new[] { "1d4",    "1d6",    "1d8",    "1d10",   "1d12", "3d6",  "4d6",  "6d6",  "8d6"  } },
        { "2d4",  new[] { "1d2",    "1d3",    "1d4",    "1d6",    "2d4",  "2d6",  "3d6",  "4d6",  "6d6"  } },
        { "2d6",  new[] { "1d4",    "1d6",    "1d8",    "1d10",   "2d6",  "3d6",  "4d6",  "6d6",  "8d6"  } },
        { "2d8",  new[] { "1d6",    "1d8",    "1d10",   "2d6",    "2d8",  "3d8",  "4d8",  "6d8",  "8d8"  } },
        { "2d10", new[] { "1d8",    "1d10",   "2d6",    "2d8",    "2d10", "4d8",  "6d8",  "8d8",  "12d8" } },
    };

    // One size category up or down for values that are not a DMG row (the tables' own ladders, extended).
    // "1" (1 point) has no DMG row either: up it follows the 1d2 row's ladder, down it is gone (DMG p.28).
    private static readonly Dictionary<string, string> ExtensionStepUp = new Dictionary<string, string>
    {
        { "1", "1d2" },
        { "3d6", "4d6" }, { "4d6", "6d6" }, { "6d6", "8d6" }, { "8d6", "12d6" },
        { "3d8", "4d8" }, { "4d8", "6d8" }, { "6d8", "8d8" }, { "8d8", "12d8" },
    };

    private static readonly Dictionary<string, string> ExtensionStepDown = new Dictionary<string, string>
    {
        { "1", NoDamage },
        { "3d6", "2d6" }, { "4d6", "3d6" }, { "6d6", "4d6" }, { "8d6", "6d6" }, { "12d6", "8d6" },
        { "3d8", "2d8" }, { "4d8", "3d8" }, { "6d8", "4d8" }, { "8d8", "6d8" }, { "12d8", "8d8" },
    };

    /// <summary>
    /// The DMG Table 2-2/2-3 entry for a weapon whose Medium damage is <paramref name="mediumExpression"/>, at
    /// <paramref name="size"/>: "-" (<see cref="NoDamage"/>) where the table leaves the step blank, null when the
    /// value is not a table row.
    /// </summary>
    public static string GetMediumTableEntry(string mediumExpression, SizeCategory size)
    {
        if (mediumExpression == null || !MediumReferenceProgressions.TryGetValue(mediumExpression, out string[] row))
            return null;
        return row[(int)size];
    }

    /// <summary>The Medium damage values that DMG Tables 2-2 and 2-3 list as rows.</summary>
    public static IEnumerable<string> MediumTableKeys => MediumReferenceProgressions.Keys;

    /// <summary>
    /// Scales <paramref name="baseCount"/>d<paramref name="baseDice"/> from <paramref name="fromSize"/> to
    /// <paramref name="toSize"/>. Returns false, with the outputs left at the base dice, when the value is outside
    /// the tables. A step the DMG leaves blank (less than 1 point) gives 1 point (CMB-133).
    /// </summary>
    public static bool TryScaleDamageDice(int baseCount, int baseDice, SizeCategory fromSize, SizeCategory toSize, out int scaledCount, out int scaledDice)
    {
        scaledCount = Mathf.Max(1, baseCount);
        scaledDice = Mathf.Max(1, baseDice);

        if (fromSize == toSize)
            return true;

        string expression = ToExpression(baseCount, baseDice);
        string scaled;
        if (fromSize == SizeCategory.Medium && MediumReferenceProgressions.TryGetValue(expression, out string[] row))
            scaled = row[(int)toSize];
        else if (!TryStep(expression, (int)toSize - (int)fromSize, out scaled))
            return false;

        if (scaled == NoDamage)
            scaled = "1";

        return TryParseDamageExpression(scaled, out scaledCount, out scaledDice);
    }

    public static string ScaleDamageExpression(string baseExpression, SizeCategory fromSize, SizeCategory toSize)
    {
        if (!TryParseDamageExpression(baseExpression, out int count, out int dice))
            return baseExpression;

        if (!TryScaleDamageDice(count, dice, fromSize, toSize, out int scaledCount, out int scaledDice))
            return baseExpression;

        return ToExpression(scaledCount, scaledDice);
    }

    /// <summary>
    /// Applies <paramref name="steps"/> single size-category changes (positive = larger), each read from the row
    /// keyed by the current value (the DMG p.28 "One" columns) or from the extension ladders. Once the damage drops
    /// below 1 point it stays gone (<see cref="NoDamage"/>).
    /// </summary>
    private static bool TryStep(string expression, int steps, out string result)
    {
        result = expression;
        int direction = steps > 0 ? 1 : -1;
        for (int i = 0; i != steps; i += direction)
        {
            if (result == NoDamage)
                return true;

            if (MediumReferenceProgressions.TryGetValue(result, out string[] row))
            {
                result = row[(int)SizeCategory.Medium + direction];
                continue;
            }

            Dictionary<string, string> ladder = direction > 0 ? ExtensionStepUp : ExtensionStepDown;
            if (!ladder.TryGetValue(result, out string next))
            {
                result = expression;
                return false;
            }
            result = next;
        }
        return true;
    }

    private static bool TryParseDamageExpression(string expression, out int count, out int dice)
    {
        count = 1;
        dice = 1;

        if (string.IsNullOrWhiteSpace(expression))
            return false;

        string normalized = expression.Trim().ToLowerInvariant();
        if (normalized == "1")
        {
            count = 1;
            dice = 1;
            return true;
        }

        string[] parts = normalized.Split('d');
        if (parts.Length != 2)
            return false;

        if (!int.TryParse(parts[0], out count) || !int.TryParse(parts[1], out dice))
            return false;

        count = Mathf.Max(1, count);
        dice = Mathf.Max(1, dice);
        return true;
    }

    public static string ToExpression(int damageCount, int damageDice)
    {
        int clampedCount = Mathf.Max(1, damageCount);
        int clampedDice = Mathf.Max(1, damageDice);
        return clampedCount == 1 && clampedDice == 1 ? "1" : $"{clampedCount}d{clampedDice}";
    }
}
