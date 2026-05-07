using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class LevelUpCalculator
{
    public static LevelUpData CalculateLevelUp(CharacterController character, int oldLevel, int newLevel)
    {
        CharacterStats stats = character != null ? character.Stats : null;
        string characterName = stats != null && !string.IsNullOrWhiteSpace(stats.CharacterName) ? stats.CharacterName : "Unknown";

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

        stats.EnsureMulticlassDataInitialized();

        ClassRegistry.Init();
        data.AvailableClasses = ClassRegistry.ClassNames != null
            ? ClassRegistry.ClassNames.ToList()
            : new List<string>();

        if (!string.IsNullOrWhiteSpace(stats.CharacterClass) && !data.AvailableClasses.Contains(stats.CharacterClass))
            data.AvailableClasses.Add(stats.CharacterClass);

        data.SelectedClassName = !string.IsNullOrWhiteSpace(stats.CharacterClass)
            ? stats.CharacterClass
            : (stats.ClassLevels.Count > 0 ? stats.ClassLevels[0].ClassName : "Fighter");

        data.XPPenaltyActive = stats.HasXPPenalty;
        data.FavoredClass = stats.FavoredClass;

        // Baseline values before class choice is applied.
        data.OldBAB = stats.BaseAttackBonus;
        data.OldFortSave = stats.FortitudeSave;
        data.OldRefSave = stats.ReflexSave;
        data.OldWillSave = stats.WillSave;

        RecalculateForSelectedClass(data, data.SelectedClassName);

        data.NeedsAbilityIncrease = (newLevel % 4 == 0);
        data.NeedsFeat = NeedsFeatAtLevel(newLevel)
            || (string.Equals(data.SelectedClassName, "Fighter", System.StringComparison.OrdinalIgnoreCase)
                && FeatDefinitions.GetsFighterBonusFeatAtLevel(newLevel));

        Debug.Log($"[LevelUp] Needs: Ability={data.NeedsAbilityIncrease}, Feat={data.NeedsFeat}, Skills={data.SkillPointsToAllocate}, Spells={data.NeedsSpellSelection}");

        return data;
    }

    public static void RecalculateForSelectedClass(LevelUpData data, string selectedClassName)
    {
        if (data == null || data.Character == null || data.Character.Stats == null)
            return;

        CharacterStats stats = data.Character.Stats;
        string selected = string.IsNullOrWhiteSpace(selectedClassName) ? stats.CharacterClass : selectedClassName;
        data.SelectedClassName = selected;

        int projectedClassLevel = stats.GetClassLevel(selected) + 1;

        data.HPGained = EstimateHpGain(selected, stats.CONMod);
        data.NewBAB = stats.BaseAttackBonus + EstimateBabGain(selected, projectedClassLevel);
        data.NewFortSave = stats.CONMod + EstimateProjectedBestSave(stats, selected, projectedClassLevel, SaveKind.Fort)
            + stats.FeatFortitudeBonus + stats.MoraleSaveBonus + stats.ConditionFortitudeModifier;
        data.NewRefSave = stats.DEXMod + EstimateProjectedBestSave(stats, selected, projectedClassLevel, SaveKind.Ref)
            + stats.FeatReflexBonus + stats.MoraleSaveBonus + stats.ConditionReflexModifier;
        data.NewWillSave = stats.WISMod + EstimateProjectedBestSave(stats, selected, projectedClassLevel, SaveKind.Will)
            + stats.FeatWillBonus + stats.RageWillBonus + stats.MoraleSaveBonus + stats.ConditionWillModifier;

        int baseSkillPointsPerLevel = Mathf.Max(1, ClassSkillDefinitions.GetBaseSkillPointsPerLevel(selected) + stats.INTMod);
        int newSkillPoints = projectedClassLevel <= 1 ? baseSkillPointsPerLevel * 4 : baseSkillPointsPerLevel;
        int pooledSkillPoints = stats.GetClassSkillPointPool(selected);
        data.SkillPointsNew = newSkillPoints;
        data.SkillPointsFromClassPool = pooledSkillPoints;
        data.SkillPointsToAllocate = Mathf.Max(0, newSkillPoints + pooledSkillPoints);

        ClassRegistry.Init();
        ICharacterClass classDef = ClassRegistry.GetClass(selected);
        data.NeedsSpellSelection = classDef != null && classDef.IsSpellcaster;
        data.NeedsFeat = NeedsFeatAtLevel(data.NewLevel)
            || (string.Equals(selected, "Fighter", System.StringComparison.OrdinalIgnoreCase)
                && FeatDefinitions.GetsFighterBonusFeatAtLevel(data.NewLevel));
    }

    private enum SaveKind { Fort, Ref, Will }

    private static int EstimateProjectedBestSave(CharacterStats stats, string selectedClass, int selectedProjectedLevel, SaveKind kind)
    {
        ClassRegistry.Init();
        int best = 0;

        for (int i = 0; i < stats.ClassLevels.Count; i++)
        {
            ClassLevelEntry entry = stats.ClassLevels[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.ClassName))
                continue;

            int level = entry.Level;
            if (string.Equals(entry.ClassName, selectedClass, System.StringComparison.OrdinalIgnoreCase))
                level = selectedProjectedLevel;

            ICharacterClass classDef = ClassRegistry.GetClass(entry.ClassName);
            bool isGood = false;
            if (classDef != null)
            {
                isGood = kind == SaveKind.Fort ? classDef.GoodFortitude
                    : kind == SaveKind.Ref ? classDef.GoodReflex
                    : classDef.GoodWill;
            }

            int value = isGood ? (2 + Mathf.Max(1, level) / 2) : (Mathf.Max(1, level) / 3);
            if (value > best)
                best = value;
        }

        // New class not currently present.
        if (!stats.ClassLevels.Any(c => c != null && string.Equals(c.ClassName, selectedClass, System.StringComparison.OrdinalIgnoreCase)))
        {
            ICharacterClass classDef = ClassRegistry.GetClass(selectedClass);
            bool isGood = false;
            if (classDef != null)
                isGood = kind == SaveKind.Fort ? classDef.GoodFortitude : kind == SaveKind.Ref ? classDef.GoodReflex : classDef.GoodWill;

            int candidate = isGood ? 2 : 0;
            best = Mathf.Max(best, candidate);
        }

        return best;
    }

    private static int EstimateBabGain(string className, int newClassLevel)
    {
        int oldClassLevel = Mathf.Max(0, newClassLevel - 1);
        int oldBab = CalculateClassBab(className, oldClassLevel);
        int newBab = CalculateClassBab(className, newClassLevel);
        return Mathf.Max(0, newBab - oldBab);
    }

    private static int CalculateClassBab(string className, int classLevel)
    {
        int safeLevel = Mathf.Max(0, classLevel);
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

    private static int EstimateHpGain(string className, int conMod)
    {
        int hitDie = GetHitDieSize(className);
        int average = Mathf.CeilToInt(hitDie / 2f + 0.5f);
        return Mathf.Max(1, average + conMod);
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

    private static bool NeedsFeatAtLevel(int level)
    {
        int safeLevel = Mathf.Max(1, level);
        return FeatDefinitions.GetsGeneralFeatAtLevel(safeLevel);
    }
}
