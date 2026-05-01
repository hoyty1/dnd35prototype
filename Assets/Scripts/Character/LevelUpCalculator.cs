using System.Collections.Generic;
using UnityEngine;

public static class LevelUpCalculator
{
    // Skill points per level by class
    private static readonly Dictionary<string, int> SkillPointsPerLevel = new Dictionary<string, int>
    {
        { "Barbarian", 4 },
        { "Bard", 6 },
        { "Cleric", 2 },
        { "Druid", 4 },
        { "Fighter", 2 },
        { "Monk", 4 },
        { "Paladin", 2 },
        { "Ranger", 6 },
        { "Rogue", 8 },
        { "Sorcerer", 2 },
        { "Wizard", 2 }
    };

    public static LevelUpData CalculateLevelUp(CharacterController character, int oldLevel, int newLevel)
    {
        CharacterStats stats = character != null ? character.Stats : null;
        string characterName = stats != null && !string.IsNullOrWhiteSpace(stats.CharacterName) ? stats.CharacterName : "Unknown";
        string className = stats != null ? stats.CharacterClass : string.Empty;

        Debug.Log($"[LevelUp] Calculating level-up for {characterName}: {oldLevel} → {newLevel}");

        LevelUpData data = new LevelUpData
        {
            Character = character,
            OldLevel = oldLevel,
            NewLevel = newLevel
        };

        if (stats == null)
        {
            Debug.LogWarning("[LevelUp] Character has no CharacterStats component; returning minimal level-up data.");
            return data;
        }

        // HP increase (already rolled in CharacterStats.OnLevelUp). Exact per-level gain history isn't tracked,
        // so estimate from expected progression and clamp to at least 1 for a level-up summary line.
        int expectedOldHp = GetExpectedHPForLevel(className, oldLevel, stats.CONMod);
        data.HPGained = Mathf.Max(1, stats.MaxHP - expectedOldHp);

        // BAB and saves
        data.OldBAB = CalculateBAB(className, oldLevel);
        data.NewBAB = CalculateBAB(className, newLevel);

        data.OldFortSave = CalculateFortSave(className, oldLevel, stats.CONMod);
        data.NewFortSave = CalculateFortSave(className, newLevel, stats.CONMod);

        data.OldRefSave = CalculateRefSave(className, oldLevel, stats.DEXMod);
        data.NewRefSave = CalculateRefSave(className, newLevel, stats.DEXMod);

        data.OldWillSave = CalculateWillSave(className, oldLevel, stats.WISMod);
        data.NewWillSave = CalculateWillSave(className, newLevel, stats.WISMod);

        // Ability score increase (every 4 levels)
        data.NeedsAbilityIncrease = (newLevel % 4 == 0);

        // Feat selection (every 3 levels, plus fighter bonus)
        data.NeedsFeat = NeedsFeatAtLevel(className, newLevel);

        // Skill points (per level, min 1)
        data.SkillPointsToAllocate = GetSkillPoints(className, stats.INTMod);

        // Spell selection for spellcasters
        data.NeedsSpellSelection = IsSpellcaster(stats);

        Debug.Log($"[LevelUp] Needs: Ability={data.NeedsAbilityIncrease}, Feat={data.NeedsFeat}, Skills={data.SkillPointsToAllocate}, Spells={data.NeedsSpellSelection}");

        return data;
    }

    private static int GetExpectedHPForLevel(string className, int level, int conMod)
    {
        int safeLevel = Mathf.Max(1, level);
        int hitDie = Mathf.Max(4, GetHitDieSize(className));
        int firstLevel = hitDie + conMod;
        int laterLevels = Mathf.Max(0, safeLevel - 1) * (((hitDie + 1) / 2) + conMod);
        return Mathf.Max(1, firstLevel + laterLevels);
    }

    private static int GetHitDieSize(string className)
    {
        switch (className)
        {
            case "Barbarian": return 12;
            case "Fighter":
            case "Paladin":
            case "Ranger": return 10;
            case "Bard":
            case "Cleric":
            case "Druid":
            case "Monk":
            case "Rogue": return 8;
            case "Sorcerer":
            case "Wizard": return 4;
            default: return 8;
        }
    }

    private static int CalculateBAB(string className, int level)
    {
        int safeLevel = Mathf.Max(1, level);

        // Full BAB: Fighter, Barbarian, Paladin, Ranger
        // 3/4 BAB: Cleric, Druid, Monk, Rogue
        // 1/2 BAB: Wizard, Sorcerer, Bard
        switch (className)
        {
            case "Fighter":
            case "Barbarian":
            case "Paladin":
            case "Ranger":
                return safeLevel;

            case "Cleric":
            case "Druid":
            case "Monk":
            case "Rogue":
                return (safeLevel * 3) / 4;

            case "Wizard":
            case "Sorcerer":
            case "Bard":
                return safeLevel / 2;

            default:
                return safeLevel;
        }
    }

    private static int CalculateFortSave(string className, int level, int conMod)
    {
        int safeLevel = Mathf.Max(1, level);
        int baseSave;

        // Good Fort: Barbarian, Cleric, Druid, Fighter, Monk, Paladin, Ranger
        switch (className)
        {
            case "Barbarian":
            case "Cleric":
            case "Druid":
            case "Fighter":
            case "Monk":
            case "Paladin":
            case "Ranger":
                baseSave = 2 + (safeLevel / 2);
                break;

            default:
                baseSave = safeLevel / 3;
                break;
        }

        return baseSave + conMod;
    }

    private static int CalculateRefSave(string className, int level, int dexMod)
    {
        int safeLevel = Mathf.Max(1, level);
        int baseSave;

        // Good Ref: Bard, Monk, Ranger, Rogue
        switch (className)
        {
            case "Bard":
            case "Monk":
            case "Ranger":
            case "Rogue":
                baseSave = 2 + (safeLevel / 2);
                break;

            default:
                baseSave = safeLevel / 3;
                break;
        }

        return baseSave + dexMod;
    }

    private static int CalculateWillSave(string className, int level, int wisMod)
    {
        int safeLevel = Mathf.Max(1, level);
        int baseSave;

        // Good Will: Bard, Cleric, Druid, Monk, Sorcerer, Wizard
        switch (className)
        {
            case "Bard":
            case "Cleric":
            case "Druid":
            case "Monk":
            case "Sorcerer":
            case "Wizard":
                baseSave = 2 + (safeLevel / 2);
                break;

            default:
                baseSave = safeLevel / 3;
                break;
        }

        return baseSave + wisMod;
    }

    private static bool NeedsFeatAtLevel(string className, int level)
    {
        int safeLevel = Mathf.Max(1, level);

        // All classes: 1, 3, 6, 9, 12, 15, 18
        bool normalFeat = FeatDefinitions.GetsGeneralFeatAtLevel(safeLevel);

        // Fighter bonus feats: 1, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20
        bool fighterBonus = className == "Fighter" && FeatDefinitions.GetsFighterBonusFeatAtLevel(safeLevel);

        return normalFeat || fighterBonus;
    }

    private static int GetSkillPoints(string className, int intMod)
    {
        int basePoints = SkillPointsPerLevel.ContainsKey(className)
            ? SkillPointsPerLevel[className]
            : ClassSkillDefinitions.GetBaseSkillPointsPerLevel(className);

        return Mathf.Max(1, basePoints + intMod);
    }

    private static bool IsSpellcaster(CharacterStats stats)
    {
        return stats != null && stats.IsSpellcaster;
    }
}
