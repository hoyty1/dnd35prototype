using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies standard stat arrays to NPCs based on their class (D&D 3.5e DMG p.127).
///
/// Elite Array: 15, 14, 13, 12, 10, 8 (for important NPCs, BBEG, etc.)
/// Nonelite Array: 13, 12, 11, 10, 9, 8 (for common NPCs, guards, etc.)
///
/// Stats are assigned by class priority — highest score goes to the most important ability.
/// </summary>
public enum StatArrayType
{
    /// <summary>Standard NPC array: 15, 14, 13, 12, 10, 8 (DMG p.127).</summary>
    Elite,
    /// <summary>Basic NPC array: 13, 12, 11, 10, 9, 8 (DMG p.127).</summary>
    Nonelite
}

public static class StatArrayApplier
{
    /// <summary>Elite stat array (DMG p.127): 15, 14, 13, 12, 10, 8.</summary>
    public static readonly int[] EliteArray = { 15, 14, 13, 12, 10, 8 };

    /// <summary>Nonelite stat array (DMG p.127): 13, 12, 11, 10, 9, 8.</summary>
    public static readonly int[] NoneliteArray = { 13, 12, 11, 10, 9, 8 };

    /// <summary>Get the stat array for the given type.</summary>
    public static int[] GetArray(StatArrayType type)
    {
        return type == StatArrayType.Elite
            ? (int[])EliteArray.Clone()
            : (int[])NoneliteArray.Clone();
    }

    /// <summary>
    /// Get the stat priority order for a class (highest priority first).
    /// Returns an array of 6 stat names in priority order.
    /// The highest array value is assigned to the first stat, etc.
    /// </summary>
    public static string[] GetPriorityStats(string className)
    {
        switch (className)
        {
            // Martial classes — STR first
            case "Fighter":
            case "Warrior":
                return new[] { "STR", "CON", "DEX", "WIS", "INT", "CHA" };

            case "Barbarian":
                return new[] { "STR", "CON", "DEX", "WIS", "CHA", "INT" };

            case "Paladin":
                return new[] { "STR", "CHA", "CON", "WIS", "DEX", "INT" };

            // DEX-based martial
            case "Ranger":
                return new[] { "DEX", "STR", "CON", "WIS", "INT", "CHA" };

            case "Monk":
                return new[] { "DEX", "STR", "WIS", "CON", "INT", "CHA" };

            case "Rogue":
                return new[] { "DEX", "INT", "CON", "WIS", "STR", "CHA" };

            // Arcane casters
            case "Wizard":
                return new[] { "INT", "DEX", "CON", "WIS", "CHA", "STR" };

            case "Sorcerer":
                return new[] { "CHA", "DEX", "CON", "WIS", "INT", "STR" };

            case "Bard":
                return new[] { "CHA", "DEX", "CON", "INT", "WIS", "STR" };

            // Divine casters
            case "Cleric":
                return new[] { "WIS", "CON", "STR", "CHA", "DEX", "INT" };

            case "Druid":
                return new[] { "WIS", "CON", "STR", "CHA", "DEX", "INT" };

            case "Adept":
                return new[] { "WIS", "INT", "CON", "DEX", "CHA", "STR" };

            // NPC classes
            case "Aristocrat":
                return new[] { "CHA", "INT", "WIS", "DEX", "STR", "CON" };

            case "Expert":
                return new[] { "INT", "DEX", "WIS", "CON", "STR", "CHA" };

            case "Commoner":
                return new[] { "CON", "STR", "DEX", "WIS", "INT", "CHA" };

            default:
                return new[] { "STR", "DEX", "CON", "INT", "WIS", "CHA" };
        }
    }

    /// <summary>
    /// Apply a stat array to a set of ability scores based on class priority.
    /// Returns a dictionary mapping stat name → assigned value.
    /// </summary>
    public static Dictionary<string, int> ApplyArray(StatArrayType arrayType, string className)
    {
        int[] array = GetArray(arrayType);
        string[] priority = GetPriorityStats(className);

        // Sort array descending (already is, but be safe)
        System.Array.Sort(array);
        System.Array.Reverse(array);

        var result = new Dictionary<string, int>();
        for (int i = 0; i < 6 && i < priority.Length && i < array.Length; i++)
        {
            result[priority[i]] = array[i];
        }
        return result;
    }

    /// <summary>
    /// Apply racial modifiers on top of the stat array.
    /// Does not modify the input dictionary — returns a new one.
    /// </summary>
    public static Dictionary<string, int> ApplyRacialModifiers(
        Dictionary<string, int> baseStats,
        int strMod = 0, int dexMod = 0, int conMod = 0,
        int intMod = 0, int wisMod = 0, int chaMod = 0)
    {
        var result = new Dictionary<string, int>(baseStats);
        if (result.ContainsKey("STR")) result["STR"] += strMod;
        if (result.ContainsKey("DEX")) result["DEX"] += dexMod;
        if (result.ContainsKey("CON")) result["CON"] += conMod;
        if (result.ContainsKey("INT")) result["INT"] += intMod;
        if (result.ContainsKey("WIS")) result["WIS"] += wisMod;
        if (result.ContainsKey("CHA")) result["CHA"] += chaMod;
        return result;
    }

    /// <summary>
    /// Apply ability score increases from HD progression (every 4 HD).
    /// Adds to the highest-priority stat for the class.
    /// </summary>
    public static void ApplyAbilityIncreases(Dictionary<string, int> stats, string className, int totalHD)
    {
        int increases = totalHD / 4;
        if (increases <= 0) return;

        string[] priority = GetPriorityStats(className);
        if (priority.Length == 0) return;

        // Add all increases to the primary stat
        string primaryStat = priority[0];
        if (stats.ContainsKey(primaryStat))
            stats[primaryStat] += increases;
    }
}
