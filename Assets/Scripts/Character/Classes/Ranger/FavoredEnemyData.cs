// ============================================================================
// D&D 3.5e Favored Enemy (PHB p.47, Table 3-14)
// A ranger selects a favored enemy at levels 1, 5, 10, 15 and 20. Each new one gives +2; at 5th level and every five
// levels after, the bonus against one favored enemy (the new one included) also rises by 2. The bonus applies to
// Bluff, Listen, Sense Motive, Spot and Survival checks against those creatures and to weapon damage rolls against
// them. The rules are in RangerRules (CHR-020).
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The favored enemy choices of PHB Table 3-14: a type, or for humanoids and outsiders a type and one subtype.
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
/// A ranger's favored enemy choices (PHB p.47; Table 3-14) and the bonus against each. Pure data: the rules (which
/// selections are due, which choices are legal, which creatures match an entry, the bonus on damage and skills) are in
/// <see cref="RangerRules"/>, for PCs and NPCs alike (CHR-020).
/// </summary>
public class FavoredEnemyData
{
    /// <summary>The favored enemies in the order chosen, each with its current bonus.</summary>
    public List<FavoredEnemyEntry> Enemies = new List<FavoredEnemyEntry>();

    /// <summary>Number of favored enemy selections made (one entry per selection; PHB p.47).</summary>
    public int SelectionsMade => Enemies.Count;

    /// <summary>Whether any favored enemies have been selected.</summary>
    public bool HasFavoredEnemies => Enemies.Count > 0;

    /// <summary>Sum of every entry's bonus: 2 per selection plus 2 per increase (PHB p.47).</summary>
    public int TotalBonus
    {
        get
        {
            int total = 0;
            for (int i = 0; i < Enemies.Count; i++)
                total += Enemies[i] != null ? Enemies[i].Bonus : 0;
            return total;
        }
    }

    /// <summary>The entry for <paramref name="creatureType"/> (a Table 3-14 name, case-insensitive), or null.</summary>
    public FavoredEnemyEntry Find(string creatureType)
    {
        if (string.IsNullOrWhiteSpace(creatureType))
            return null;
        for (int i = 0; i < Enemies.Count; i++)
        {
            if (Enemies[i] != null && string.Equals(Enemies[i].CreatureType, creatureType.Trim(), StringComparison.OrdinalIgnoreCase))
                return Enemies[i];
        }
        return null;
    }

    /// <summary>Whether <paramref name="creatureType"/> is already a favored enemy.</summary>
    public bool Has(string creatureType) => Find(creatureType) != null;

    /// <summary>Adds a new favored enemy at +2. Validation is <see cref="RangerRules.TryApplyFavoredEnemySelection"/>'s.</summary>
    internal void AddNew(string creatureType)
    {
        Enemies.Add(new FavoredEnemyEntry(creatureType.Trim(), 2));
    }

    /// <summary>Raises an existing favored enemy's bonus by 2 (PHB p.47).</summary>
    internal void IncreaseBonus(string creatureType)
    {
        FavoredEnemyEntry entry = Find(creatureType);
        if (entry != null)
            entry.Bonus += 2;
    }

    /// <summary>A deep copy (NPC definitions and creation records hand theirs to a new creature).</summary>
    public FavoredEnemyData Clone()
    {
        var copy = new FavoredEnemyData();
        for (int i = 0; i < Enemies.Count; i++)
        {
            if (Enemies[i] != null)
                copy.Enemies.Add(new FavoredEnemyEntry(Enemies[i].CreatureType, Enemies[i].Bonus));
        }
        return copy;
    }

    /// <summary>"Humanoid (Elf) +4, Humanoid (Human) +2", or "None".</summary>
    public string GetSummary()
    {
        if (Enemies.Count == 0) return "None";
        var parts = new string[Enemies.Count];
        for (int i = 0; i < Enemies.Count; i++)
            parts[i] = $"{Enemies[i].CreatureType} +{Enemies[i].Bonus}";
        return string.Join(", ", parts);
    }

    /// <summary>The favored enemy types in the order chosen.</summary>
    public List<string> GetFavoredEnemyTypes()
    {
        var types = new List<string>();
        for (int i = 0; i < Enemies.Count; i++)
            types.Add(Enemies[i].CreatureType);
        return types;
    }
}
