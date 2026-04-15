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
    /// D&D 3.5 natural reach in feet for typical tall creatures.
    /// Simplified for this prototype: Small/Medium 5 ft, Large 10 ft, then +5 ft per step.
    /// </summary>
    public static int GetNaturalReachFeet(this SizeCategory size)
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
                return 10;
            case SizeCategory.Huge:
                return 15;
            case SizeCategory.Gargantuan:
                return 20;
            case SizeCategory.Colossal:
                return 30;
            default:
                return 5;
        }
    }

    /// <summary>
    /// Grid-square approximation of natural reach (5 ft per square, minimum 1).
    /// </summary>
    public static int GetNaturalReachSquares(this SizeCategory size)
    {
        return Mathf.Max(1, GetNaturalReachFeet(size) / 5);
    }

    /// <summary>
    /// Simplified combat footprint in squares (prototype currently supports 1x1 occupancy only).
    /// Values are exposed for UI/future multi-tile occupancy work.
    /// </summary>
    public static int GetSpaceSquares(this SizeCategory size)
    {
        switch (size)
        {
            case SizeCategory.Fine:
            case SizeCategory.Diminutive:
            case SizeCategory.Tiny:
            case SizeCategory.Small:
            case SizeCategory.Medium:
                return 1;
            case SizeCategory.Large:
                return 4; // 2x2
            case SizeCategory.Huge:
                return 9; // 3x3
            case SizeCategory.Gargantuan:
                return 16; // 4x4
            case SizeCategory.Colossal:
                return 25; // 5x5
            default:
                return 1;
        }
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
