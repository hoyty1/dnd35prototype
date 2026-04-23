using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// D&D 3.5e BAB progression tracks.
/// </summary>
public enum BABProgression
{
    Good,   // +1 / level
    Medium, // +3/4 / level
    Poor    // +1/2 / level
}

/// <summary>
/// D&D 3.5e save progression tracks.
/// </summary>
public enum SaveProgression
{
    Good, // +2 + level/2
    Poor  // level/3
}

/// <summary>
/// D&D 3.5e Monster Manual creature types.
/// </summary>
public enum CreatureTypeId
{
    Aberration,
    Animal,
    Construct,
    Dragon,
    Elemental,
    Fey,
    Giant,
    Humanoid,
    MagicalBeast,
    MonstrousHumanoid,
    Ooze,
    Outsider,
    Plant,
    Undead,
    Vermin
}

/// <summary>
/// Progression package for one creature type.
/// </summary>
public class CreatureTypeProgression
{
    public CreatureTypeId Type;
    public int HitDie;
    public BABProgression BAB;
    public SaveProgression Fortitude;
    public SaveProgression Reflex;
    public SaveProgression Will;

    public CreatureTypeProgression(
        CreatureTypeId type,
        int hitDie,
        BABProgression bab,
        SaveProgression fort,
        SaveProgression reflex,
        SaveProgression will)
    {
        Type = type;
        HitDie = hitDie;
        BAB = bab;
        Fortitude = fort;
        Reflex = reflex;
        Will = will;
    }
}

/// <summary>
/// Static lookup for Monster Manual Chapter 4 creature progression data.
/// </summary>
public static class CreatureTypeProgressionDatabase
{
    private static readonly Dictionary<CreatureTypeId, CreatureTypeProgression> _data = new Dictionary<CreatureTypeId, CreatureTypeProgression>
    {
        [CreatureTypeId.Aberration] = new CreatureTypeProgression(CreatureTypeId.Aberration, 8, BABProgression.Medium, SaveProgression.Poor, SaveProgression.Poor, SaveProgression.Good),
        [CreatureTypeId.Animal] = new CreatureTypeProgression(CreatureTypeId.Animal, 8, BABProgression.Medium, SaveProgression.Good, SaveProgression.Good, SaveProgression.Poor),
        [CreatureTypeId.Construct] = new CreatureTypeProgression(CreatureTypeId.Construct, 10, BABProgression.Medium, SaveProgression.Poor, SaveProgression.Poor, SaveProgression.Poor),
        [CreatureTypeId.Dragon] = new CreatureTypeProgression(CreatureTypeId.Dragon, 12, BABProgression.Good, SaveProgression.Good, SaveProgression.Good, SaveProgression.Good),
        [CreatureTypeId.Elemental] = new CreatureTypeProgression(CreatureTypeId.Elemental, 8, BABProgression.Medium, SaveProgression.Poor, SaveProgression.Poor, SaveProgression.Poor),
        [CreatureTypeId.Fey] = new CreatureTypeProgression(CreatureTypeId.Fey, 6, BABProgression.Poor, SaveProgression.Poor, SaveProgression.Good, SaveProgression.Good),
        [CreatureTypeId.Giant] = new CreatureTypeProgression(CreatureTypeId.Giant, 8, BABProgression.Medium, SaveProgression.Good, SaveProgression.Poor, SaveProgression.Poor),
        [CreatureTypeId.Humanoid] = new CreatureTypeProgression(CreatureTypeId.Humanoid, 8, BABProgression.Medium, SaveProgression.Poor, SaveProgression.Poor, SaveProgression.Poor),
        [CreatureTypeId.MagicalBeast] = new CreatureTypeProgression(CreatureTypeId.MagicalBeast, 10, BABProgression.Good, SaveProgression.Good, SaveProgression.Good, SaveProgression.Poor),
        [CreatureTypeId.MonstrousHumanoid] = new CreatureTypeProgression(CreatureTypeId.MonstrousHumanoid, 8, BABProgression.Good, SaveProgression.Poor, SaveProgression.Good, SaveProgression.Good),
        [CreatureTypeId.Ooze] = new CreatureTypeProgression(CreatureTypeId.Ooze, 10, BABProgression.Medium, SaveProgression.Poor, SaveProgression.Poor, SaveProgression.Poor),
        [CreatureTypeId.Outsider] = new CreatureTypeProgression(CreatureTypeId.Outsider, 8, BABProgression.Good, SaveProgression.Good, SaveProgression.Good, SaveProgression.Good),
        [CreatureTypeId.Plant] = new CreatureTypeProgression(CreatureTypeId.Plant, 8, BABProgression.Medium, SaveProgression.Good, SaveProgression.Poor, SaveProgression.Poor),
        [CreatureTypeId.Undead] = new CreatureTypeProgression(CreatureTypeId.Undead, 12, BABProgression.Medium, SaveProgression.Poor, SaveProgression.Poor, SaveProgression.Good),
        [CreatureTypeId.Vermin] = new CreatureTypeProgression(CreatureTypeId.Vermin, 8, BABProgression.Medium, SaveProgression.Good, SaveProgression.Poor, SaveProgression.Poor)
    };

    private static readonly Dictionary<string, CreatureTypeId> _stringToType = new Dictionary<string, CreatureTypeId>(StringComparer.OrdinalIgnoreCase)
    {
        ["aberration"] = CreatureTypeId.Aberration,
        ["animal"] = CreatureTypeId.Animal,
        ["construct"] = CreatureTypeId.Construct,
        ["dragon"] = CreatureTypeId.Dragon,
        ["elemental"] = CreatureTypeId.Elemental,
        ["fey"] = CreatureTypeId.Fey,
        ["giant"] = CreatureTypeId.Giant,
        ["humanoid"] = CreatureTypeId.Humanoid,
        ["magical beast"] = CreatureTypeId.MagicalBeast,
        ["magicalbeast"] = CreatureTypeId.MagicalBeast,
        ["monstrous humanoid"] = CreatureTypeId.MonstrousHumanoid,
        ["monstroushumanoid"] = CreatureTypeId.MonstrousHumanoid,
        ["ooze"] = CreatureTypeId.Ooze,
        ["outsider"] = CreatureTypeId.Outsider,
        ["plant"] = CreatureTypeId.Plant,
        ["undead"] = CreatureTypeId.Undead,
        ["vermin"] = CreatureTypeId.Vermin
    };

    public static CreatureTypeProgression Get(CreatureTypeId type)
    {
        if (_data.TryGetValue(type, out CreatureTypeProgression value))
            return value;

        Debug.LogWarning($"[CreatureTypeProgression] Unknown creature type id '{type}', defaulting to Humanoid.");
        return _data[CreatureTypeId.Humanoid];
    }

    public static CreatureTypeProgression GetFromString(string creatureType)
    {
        if (TryParseCreatureType(creatureType, out CreatureTypeId parsed))
            return Get(parsed);

        Debug.LogWarning($"[CreatureTypeProgression] Unknown creature type '{creatureType}', defaulting to Humanoid.");
        return _data[CreatureTypeId.Humanoid];
    }

    public static bool TryParseCreatureType(string raw, out CreatureTypeId type)
    {
        string key = string.IsNullOrWhiteSpace(raw)
            ? "humanoid"
            : raw.Trim().Replace("-", " ").Replace("_", " ");

        if (_stringToType.TryGetValue(key, out type))
            return true;

        return Enum.TryParse(raw, ignoreCase: true, out type);
    }
}

/// <summary>
/// Shared progression math for classes and creature types.
/// </summary>
public static class ProgressionCalculator
{
    public static int CalculateBAB(BABProgression progression, int level)
    {
        int safeLevel = Mathf.Max(1, level);
        switch (progression)
        {
            case BABProgression.Good: return safeLevel;
            case BABProgression.Medium: return (safeLevel * 3) / 4;
            case BABProgression.Poor: return safeLevel / 2;
            default: return safeLevel / 2;
        }
    }

    public static int CalculateSave(SaveProgression progression, int level)
    {
        int safeLevel = Mathf.Max(1, level);
        switch (progression)
        {
            case SaveProgression.Good: return 2 + (safeLevel / 2);
            case SaveProgression.Poor: return safeLevel / 3;
            default: return safeLevel / 3;
        }
    }

    public static int CalculateAverageHpFromHitDice(int hitDie, int hitDiceCount)
    {
        int safeDie = Mathf.Max(1, hitDie);
        int safeCount = Mathf.Max(1, hitDiceCount);
        int averageDieRoll = (safeDie + 1) / 2;
        return averageDieRoll * safeCount;
    }
}
