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
        // Humanoid racial HD: good Reflex "usually; a humanoid's good save varies" (MM p.310); an entry with another good
        // save overrides it (gnoll and troglodyte: Fortitude). Only humanoids with more than 1 HD have racial HD at all.
        [CreatureTypeId.Humanoid] = new CreatureTypeProgression(CreatureTypeId.Humanoid, 8, BABProgression.Medium, SaveProgression.Poor, SaveProgression.Good, SaveProgression.Poor),
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

    /// <summary>
    /// Sets the racial Hit Dice and their progressions of a creature built from <paramref name="def"/>: the definition's
    /// racial HD (<see cref="NPCDefinition.ResolveRacialHitDice"/>), the creature type's BAB and save progressions or the
    /// definition's overrides of them, its fixed <see cref="NPCDefinition.BaseAttackBonusOverride"/>, and the class that
    /// only stands in for those racial HD (<see cref="CharacterStats.RacialHitDiceStandInClass"/>), so the stand-in's
    /// levels are not counted a second time as class levels. CharacterStats then adds the real class levels (CRE-004).
    /// Every path that builds a creature from a definition calls this (GameManager.InitializeNPCFromDefinition,
    /// LionsShieldBehavior.BuildSummonStats).
    /// </summary>
    public static void ApplyToStats(CharacterStats stats, NPCDefinition def)
    {
        if (stats == null || def == null)
            return;

        CreatureTypeProgression progression = GetFromString(def.CreatureType);
        stats.RacialHitDice = def.ResolveRacialHitDice();
        stats.RacialHitDiceStandInClass = def.ResolveRacialHitDiceStandInClass();
        stats.CreatureBABProgression = def.BABOverride ?? progression.BAB;
        stats.CreatureFortitudeProgression = def.FortitudeSaveOverride ?? progression.Fortitude;
        stats.CreatureReflexProgression = def.ReflexSaveOverride ?? progression.Reflex;
        stats.CreatureWillProgression = def.WillSaveOverride ?? progression.Will;
        stats.BaseAttackBonusOverride = def.BaseAttackBonusOverride;
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

    /// <summary>
    /// Base attack bonus from <paramref name="racialHitDice"/> racial Hit Dice of a creature type (MM Table 4-1, p.290):
    /// 0 without racial Hit Dice, so a creature whose Hit Dice are all class levels (a 1-HD humanoid, MM p.310, or a
    /// PC) gets nothing here. Class levels add their own BAB on top (MM p.290, PHB p.59; CRE-004).
    /// </summary>
    public static int CalculateRacialBAB(BABProgression progression, int racialHitDice)
    {
        return racialHitDice <= 0 ? 0 : CalculateBAB(progression, racialHitDice);
    }

    /// <summary>
    /// Base save from <paramref name="racialHitDice"/> racial Hit Dice of a creature type (MM Table 4-1, p.290): good
    /// 2 + HD/2, poor HD/3, and 0 without racial Hit Dice. Class levels add their own base saves on top (CRE-004).
    /// </summary>
    public static int CalculateRacialSave(SaveProgression progression, int racialHitDice)
    {
        return racialHitDice <= 0 ? 0 : CalculateSave(progression, racialHitDice);
    }

    public static int CalculateAverageHpFromHitDice(int hitDie, int hitDiceCount)
    {
        int safeDie = Mathf.Max(1, hitDie);
        int safeCount = Mathf.Max(1, hitDiceCount);
        int averageDieRoll = (safeDie + 1) / 2;
        return averageDieRoll * safeCount;
    }
}
