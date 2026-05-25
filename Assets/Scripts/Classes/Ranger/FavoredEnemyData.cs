// ============================================================================
// D&D 3.5e Favored Enemy System (PHB p.47)
// Rangers select favored enemies at levels 1, 5, 10, 15, 20.
// +2 bonus to Bluff, Listen, Sense Motive, Spot, Survival vs favored enemy.
// +2 bonus to weapon damage vs favored enemy.
// When gaining a new favored enemy, can increase existing bonus by +2 instead.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// All creature types available as favored enemies (D&D 3.5e PHB p.47).
/// </summary>
public static class FavoredEnemyTypes
{
    public static readonly string[] AllTypes = new string[]
    {
        "Aberration",
        "Animal",
        "Construct",
        "Dragon",
        "Elemental",
        "Fey",
        "Giant",
        "Humanoid (Aquatic)",
        "Humanoid (Dwarf)",
        "Humanoid (Elf)",
        "Humanoid (Gnoll)",
        "Humanoid (Gnome)",
        "Humanoid (Goblinoid)",
        "Humanoid (Halfling)",
        "Humanoid (Human)",
        "Humanoid (Orc)",
        "Humanoid (Reptilian)",
        "Magical Beast",
        "Monstrous Humanoid",
        "Ooze",
        "Outsider (Air)",
        "Outsider (Chaotic)",
        "Outsider (Earth)",
        "Outsider (Evil)",
        "Outsider (Fire)",
        "Outsider (Good)",
        "Outsider (Lawful)",
        "Outsider (Native)",
        "Outsider (Water)",
        "Plant",
        "Undead",
        "Vermin"
    };

    /// <summary>Levels at which Rangers gain a new favored enemy selection.</summary>
    public static readonly int[] FavoredEnemyLevels = { 1, 5, 10, 15, 20 };

    /// <summary>Check if a given ranger level grants a favored enemy selection.</summary>
    public static bool IsFavoredEnemyLevel(int rangerLevel)
    {
        for (int i = 0; i < FavoredEnemyLevels.Length; i++)
        {
            if (FavoredEnemyLevels[i] == rangerLevel) return true;
        }
        return false;
    }

    /// <summary>How many favored enemy selections a ranger gets at a given level.</summary>
    public static int GetTotalSelectionsAtLevel(int rangerLevel)
    {
        int count = 0;
        for (int i = 0; i < FavoredEnemyLevels.Length; i++)
        {
            if (FavoredEnemyLevels[i] <= rangerLevel) count++;
        }
        return count;
    }
}

/// <summary>
/// Tracks a single favored enemy entry with its bonus level.
/// </summary>
[Serializable]
public class FavoredEnemyEntry
{
    public string CreatureType;
    public int Bonus; // +2, +4, +6, +8, +10 (stacked)

    public FavoredEnemyEntry(string creatureType, int bonus = 2)
    {
        CreatureType = creatureType;
        Bonus = bonus;
    }
}

/// <summary>
/// Manages all favored enemy data for a Ranger (D&D 3.5e PHB p.47).
/// Pure data class — no MonoBehaviour dependency.
/// </summary>
public class FavoredEnemyData
{
    /// <summary>All active favored enemies with their current bonuses.</summary>
    public List<FavoredEnemyEntry> Enemies = new List<FavoredEnemyEntry>();

    /// <summary>Number of favored enemy selections that have been made.</summary>
    public int SelectionsMade => Enemies.Count;

    /// <summary>
    /// Add a new favored enemy or increase an existing enemy's bonus by +2.
    /// </summary>
    /// <param name="creatureType">Creature type to add/increase</param>
    /// <returns>True if successful</returns>
    public bool AddOrIncreaseFavoredEnemy(string creatureType)
    {
        if (string.IsNullOrEmpty(creatureType)) return false;

        // Check if already a favored enemy — increase bonus
        for (int i = 0; i < Enemies.Count; i++)
        {
            if (string.Equals(Enemies[i].CreatureType, creatureType, StringComparison.OrdinalIgnoreCase))
            {
                Enemies[i].Bonus += 2;
                Debug.Log($"[FavoredEnemy] Increased bonus vs {creatureType} to +{Enemies[i].Bonus}");
                return true;
            }
        }

        // Add as new favored enemy
        Enemies.Add(new FavoredEnemyEntry(creatureType, 2));
        Debug.Log($"[FavoredEnemy] Added new favored enemy: {creatureType} (+2)");
        return true;
    }

    /// <summary>
    /// Get the favored enemy bonus against a specific creature type.
    /// Returns 0 if the creature type is not a favored enemy.
    /// </summary>
    public int GetBonusVs(string creatureType)
    {
        if (string.IsNullOrEmpty(creatureType)) return 0;

        for (int i = 0; i < Enemies.Count; i++)
        {
            if (MatchesCreatureType(Enemies[i].CreatureType, creatureType))
                return Enemies[i].Bonus;
        }
        return 0;
    }

    /// <summary>
    /// Get weapon damage bonus vs a creature type (same as skill bonus).
    /// D&D 3.5e PHB p.47: "The ranger gains a +2 bonus on... weapon damage rolls."
    /// </summary>
    public int GetDamageBonusVs(string creatureType) => GetBonusVs(creatureType);

    /// <summary>
    /// Get skill bonus vs a creature type.
    /// D&D 3.5e PHB p.47: "+2 bonus on Bluff, Listen, Sense Motive, Spot, and Survival checks"
    /// </summary>
    public int GetSkillBonusVs(string creatureType) => GetBonusVs(creatureType);

    /// <summary>Check if a creature type matches a favored enemy entry.</summary>
    private bool MatchesCreatureType(string favoredType, string targetType)
    {
        if (string.IsNullOrEmpty(favoredType) || string.IsNullOrEmpty(targetType))
            return false;

        // Exact match (case-insensitive)
        if (string.Equals(favoredType, targetType, StringComparison.OrdinalIgnoreCase))
            return true;

        // "Humanoid (Human)" matches "Humanoid" tag on creature
        // "Outsider (Evil)" matches creatures tagged as both "Outsider" and "Evil"
        string favoredLower = favoredType.Trim().ToLowerInvariant();
        string targetLower = targetType.Trim().ToLowerInvariant();

        // Handle subtype matching: "humanoid (human)" matches "humanoid"
        if (favoredLower.Contains("("))
        {
            string baseType = favoredLower.Substring(0, favoredLower.IndexOf('(')).Trim();
            if (string.Equals(baseType, targetLower, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>Whether any favored enemies have been selected.</summary>
    public bool HasFavoredEnemies => Enemies.Count > 0;

    /// <summary>Get a summary string for display.</summary>
    public string GetSummary()
    {
        if (Enemies.Count == 0) return "None";
        var parts = new string[Enemies.Count];
        for (int i = 0; i < Enemies.Count; i++)
            parts[i] = $"{Enemies[i].CreatureType} (+{Enemies[i].Bonus})";
        return string.Join(", ", parts);
    }

    /// <summary>Get list of creature types that are currently favored enemies.</summary>
    public List<string> GetFavoredEnemyTypes()
    {
        var types = new List<string>();
        for (int i = 0; i < Enemies.Count; i++)
            types.Add(Enemies[i].CreatureType);
        return types;
    }
}
