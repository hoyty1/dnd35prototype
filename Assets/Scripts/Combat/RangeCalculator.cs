using UnityEngine;

/// <summary>
/// Utility class for D&D 3.5 range increment calculations.
/// Each ranged weapon has a range increment in feet.
/// Attacks within the first increment: no penalty.
/// Each additional increment: -2 cumulative penalty.
/// Maximum range per D&D 3.5 rules:
///   - Projectile weapons (bows, crossbows, slings): 10 × range increment
///   - Thrown weapons (daggers, javelins, handaxes, darts, shortspears, tridents): 5 × range increment
/// </summary>
public static class RangeCalculator
{
    /// <summary>Feet per hex (1 hex = 5 ft in D&D 3.5).</summary>
    public const int FeetPerHex = 5;

    /// <summary>Maximum number of range increments for projectile weapons (bows, crossbows, slings).</summary>
    public const int MaxIncrementsProjectile = 10;

    /// <summary>Maximum number of range increments for thrown weapons (D&D 3.5 rule).</summary>
    public const int MaxIncrementsThrown = 5;

    /// <summary>Penalty per range increment beyond the first.</summary>
    public const int PenaltyPerIncrement = -2;

    /// <summary>
    /// Get the maximum number of increments for a weapon based on whether it is thrown.
    /// Thrown weapons: 5, Projectile weapons: 10.
    /// </summary>
    public static int GetMaxIncrements(bool isThrownWeapon)
    {
        return isThrownWeapon ? MaxIncrementsThrown : MaxIncrementsProjectile;
    }

    /// <summary>
    /// Calculate the range increment number for a given distance.
    /// Returns 0 if out of range (beyond max increments) or weapon has no range increment.
    /// Returns 1 for first increment (no penalty), 2 for second (-2), etc.
    /// </summary>
    public static int GetRangeIncrement(int distanceInFeet, int rangeIncrementFeet, bool isThrownWeapon = false)
    {
        if (rangeIncrementFeet <= 0 || distanceInFeet <= 0) return 0;

        // Ceiling division: which increment is the target in?
        int increment = (distanceInFeet + rangeIncrementFeet - 1) / rangeIncrementFeet;

        int maxInc = GetMaxIncrements(isThrownWeapon);
        if (increment > maxInc) return 0; // Beyond max range
        return increment;
    }

    /// <summary>
    /// Calculate the attack penalty for a ranged attack at a given distance.
    /// Returns 0 for first increment, -2 for second, -4 for third, etc.
    /// Returns int.MinValue if the target is out of range.
    /// </summary>
    public static int GetRangePenalty(int distanceInFeet, int rangeIncrementFeet, bool isThrownWeapon = false)
    {
        int increment = GetRangeIncrement(distanceInFeet, rangeIncrementFeet, isThrownWeapon);
        if (increment == 0) return int.MinValue; // Out of range
        return (increment - 1) * PenaltyPerIncrement;
    }

    /// <summary>
    /// Check if a target at a given distance is within maximum range.
    /// Thrown weapons max at 5 increments, projectile weapons max at 10 increments.
    /// </summary>
    public static bool IsWithinMaxRange(int distanceInFeet, int rangeIncrementFeet, bool isThrownWeapon = false)
    {
        if (rangeIncrementFeet <= 0) return false;
        int maxInc = GetMaxIncrements(isThrownWeapon);
        return distanceInFeet <= rangeIncrementFeet * maxInc;
    }

    /// <summary>
    /// Get maximum range in feet for a weapon.
    /// Thrown weapons max at 5 increments, projectile weapons max at 10 increments.
    /// </summary>
    public static int GetMaxRangeFeet(int rangeIncrementFeet, bool isThrownWeapon = false)
    {
        return rangeIncrementFeet * GetMaxIncrements(isThrownWeapon);
    }

    /// <summary>
    /// Get maximum range in hexes for a weapon.
    /// </summary>
    public static int GetMaxRangeHexes(int rangeIncrementFeet, bool isThrownWeapon = false)
    {
        return GetMaxRangeFeet(rangeIncrementFeet, isThrownWeapon) / FeetPerHex;
    }

    /// <summary>
    /// Get range increment in hexes.
    /// </summary>
    public static int GetRangeIncrementHexes(int rangeIncrementFeet)
    {
        return rangeIncrementFeet / FeetPerHex;
    }

    /// <summary>
    /// Convert hex distance to feet.
    /// </summary>
    public static int HexesToFeet(int hexDistance)
    {
        return hexDistance * FeetPerHex;
    }

    /// <summary>
    /// Get a full range info object for a ranged attack at a given hex distance.
    /// Pass isThrownWeapon = true for thrown weapons (5 increment max) or false for projectile weapons (10 increment max).
    /// </summary>
    public static RangeInfo GetRangeInfo(int hexDistance, int rangeIncrementFeet, bool isThrownWeapon = false)
    {
        var info = new RangeInfo();
        info.HexDistance = hexDistance;
        info.DistanceFeet = HexesToFeet(hexDistance);
        info.RangeIncrementFeet = rangeIncrementFeet;
        info.IsThrownWeapon = isThrownWeapon;
        info.MaxRangeFeet = GetMaxRangeFeet(rangeIncrementFeet, isThrownWeapon);
        info.MaxRangeHexes = GetMaxRangeHexes(rangeIncrementFeet, isThrownWeapon);

        if (rangeIncrementFeet <= 0)
        {
            // Melee weapon - no range increment system
            info.IncrementNumber = 0;
            info.Penalty = 0;
            info.IsInRange = (hexDistance <= 1);
            info.IsMelee = true;
            return info;
        }

        info.IsMelee = false;
        info.IncrementNumber = GetRangeIncrement(info.DistanceFeet, rangeIncrementFeet, isThrownWeapon);
        info.IsInRange = info.IncrementNumber > 0;

        if (info.IsInRange)
            info.Penalty = (info.IncrementNumber - 1) * PenaltyPerIncrement;
        else
            info.Penalty = 0; // Can't attack - penalty doesn't matter

        return info;
    }

    /// <summary>
    /// Determine the range zone a hex falls into for visual highlighting.
    /// Zone 0: Not in range / melee weapon
    /// Zone 1: First increment (green/normal, no penalty)
    /// Zone 2: 2nd-3rd increments for thrown, 2nd-5th for projectile (yellow, moderate penalty)
    /// Zone 3: 4th-5th increments for thrown, 6th-10th for projectile (orange, heavy penalty)
    /// </summary>
    public static int GetRangeZone(int hexDistance, int rangeIncrementFeet, bool isThrownWeapon = false)
    {
        if (rangeIncrementFeet <= 0) return 0;

        int distFeet = HexesToFeet(hexDistance);
        int increment = GetRangeIncrement(distFeet, rangeIncrementFeet, isThrownWeapon);

        if (increment == 0) return 0;        // Out of range
        if (increment == 1) return 1;         // First increment - no penalty

        if (isThrownWeapon)
        {
            // Thrown weapons: 5 max increments
            if (increment <= 3) return 2;     // 2nd-3rd - moderate
            return 3;                          // 4th-5th - far
        }
        else
        {
            // Projectile weapons: 10 max increments
            if (increment <= 5) return 2;     // 2nd-5th - moderate
            return 3;                          // 6th-10th - far
        }
    }
}

/// <summary>
/// Data class holding range calculation results for a specific attack.
/// </summary>
public class RangeInfo
{
    public int HexDistance;           // Distance in hexes
    public int DistanceFeet;          // Distance in feet
    public int RangeIncrementFeet;    // Weapon's range increment
    public int MaxRangeFeet;          // Maximum range (5× increment for thrown, 10× for projectile)
    public int MaxRangeHexes;         // Maximum range in hexes
    public int IncrementNumber;       // Which increment (1-5 thrown, 1-10 projectile, 0 = out of range)
    public int Penalty;               // Attack penalty (0, -2, -4, etc.)
    public bool IsInRange;            // Whether the target can be attacked
    public bool IsMelee;              // Whether this is a melee weapon
    public bool IsThrownWeapon;       // Whether the weapon is thrown (5 increment max vs 10 for projectile)

    /// <summary>Get a formatted description for the combat log.</summary>
    public string GetDescription()
    {
        if (IsMelee) return "melee";
        if (!IsInRange) return $"out of range ({DistanceFeet} ft, max {MaxRangeFeet} ft)";
        if (Penalty == 0) return $"{DistanceFeet} ft (increment 1, no penalty)";
        return $"{DistanceFeet} ft (increment {IncrementNumber}, {Penalty} penalty)";
    }

    /// <summary>Get a short penalty string for display.</summary>
    public string GetPenaltyString()
    {
        if (IsMelee || Penalty == 0) return "";
        return $"{Penalty} range";
    }
}
