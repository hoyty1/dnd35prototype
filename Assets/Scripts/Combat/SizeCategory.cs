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
