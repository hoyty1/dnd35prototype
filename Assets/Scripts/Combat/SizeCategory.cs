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
/// D&D 3.5e weapon/natural attack damage scaling by size category
/// (DMG tables for changing weapon damage by size).
/// </summary>
public static class WeaponDamageScaler
{
    private static readonly Dictionary<string, string[]> MediumReferenceProgressions = new Dictionary<string, string[]>
    {
        // Keys are the damage expression at Medium size.
        { "1",   new[] { "1",   "1",   "1",   "1",   "1",   "1d2", "1d3", "1d4", "1d6" } },
        { "1d2", new[] { "1",   "1",   "1",   "1d2", "1d3", "1d4", "1d6", "1d8", "2d6" } },
        { "1d3", new[] { "1",   "1d2", "1d2", "1d3", "1d4", "1d6", "1d8", "2d6", "3d6" } },
        { "1d4", new[] { "1",   "1d2", "1d3", "1d4", "1d6", "1d8", "2d6", "3d6", "4d6" } },
        { "1d6", new[] { "1d2", "1d3", "1d4", "1d6", "1d8", "2d6", "3d6", "4d6", "6d6" } },
        { "1d8", new[] { "1d3", "1d4", "1d6", "1d8", "2d6", "3d6", "4d6", "6d6", "8d6" } },
        { "1d10",new[] { "1d4", "1d6", "1d8", "1d10","2d8", "3d8", "4d8", "6d8", "8d8" } },
        { "1d12",new[] { "1d6", "1d8", "1d10","1d12","3d6", "4d6", "6d6", "8d6", "12d6" } },
        { "2d4", new[] { "1d3", "1d4", "1d6", "1d8", "2d4", "2d6", "3d6", "4d6", "6d6" } },
        { "2d6", new[] { "1d4", "1d6", "1d8", "1d10","2d6", "3d6", "4d6", "6d6", "8d6" } },
        { "2d8", new[] { "1d6", "1d8", "1d10","2d6", "2d8", "3d8", "4d8", "6d8", "8d8" } },
    };

    public static bool TryScaleDamageDice(int baseCount, int baseDice, SizeCategory fromSize, SizeCategory toSize, out int scaledCount, out int scaledDice)
    {
        scaledCount = Mathf.Max(1, baseCount);
        scaledDice = Mathf.Max(1, baseDice);

        if (fromSize == toSize)
            return true;

        if (!TryResolveProgression(baseCount, baseDice, fromSize, out string[] progression))
            return false;

        return TryParseDamageExpression(progression[(int)toSize], out scaledCount, out scaledDice);
    }

    public static string ScaleDamageExpression(string baseExpression, SizeCategory fromSize, SizeCategory toSize)
    {
        if (!TryParseDamageExpression(baseExpression, out int count, out int dice))
            return baseExpression;

        if (!TryScaleDamageDice(count, dice, fromSize, toSize, out int scaledCount, out int scaledDice))
            return baseExpression;

        return ToExpression(scaledCount, scaledDice);
    }

    private static bool TryResolveProgression(int damageCount, int damageDice, SizeCategory fromSize, out string[] progression)
    {
        progression = null;
        string expression = ToExpression(damageCount, damageDice);

        // Fast path when source expression is already the Medium baseline key.
        if (fromSize == SizeCategory.Medium && MediumReferenceProgressions.TryGetValue(expression, out progression))
            return true;

        string[] candidate = null;
        foreach (KeyValuePair<string, string[]> kvp in MediumReferenceProgressions)
        {
            string atSourceSize = kvp.Value[(int)fromSize];
            if (!TryParseDamageExpression(atSourceSize, out int candidateCount, out int candidateDice))
                continue;

            if (candidateCount != damageCount || candidateDice != damageDice)
                continue;

            // Prefer exact medium-key match when ambiguous.
            if (kvp.Key == expression)
            {
                progression = kvp.Value;
                return true;
            }

            if (candidate == null)
                candidate = kvp.Value;
        }

        progression = candidate;
        return progression != null;
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
