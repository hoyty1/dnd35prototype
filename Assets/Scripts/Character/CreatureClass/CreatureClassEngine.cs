using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Engine for applying character class levels to creatures (D&D 3.5e DMG p.293-296).
/// Handles HD stacking, BAB calculation, save progression, skill points,
/// feat grants, and ability score increases.
///
/// Used when monsters/NPCs gain class levels (e.g., Ogre Barbarian 3).
/// </summary>
public static class CreatureClassEngine
{
    /// <summary>
    /// Calculate total Hit Dice for a creature with racial HD + class levels.
    /// </summary>
    public static int CalculateTotalHD(int racialHD, int classLevels)
    {
        return Mathf.Max(1, racialHD + classLevels);
    }

    /// <summary>
    /// Calculate BAB for class levels using the class's BAB progression.
    /// BABAtLevel3 encoding: 3 = full (1/level), 2 = medium (3/4), 1 = poor (1/2)
    /// </summary>
    public static int CalculateClassBAB(int babAtLevel3, int classLevels)
    {
        if (classLevels <= 0) return 0;

        switch (babAtLevel3)
        {
            case 3: // Full BAB: +1 per level
                return classLevels;
            case 2: // Medium BAB: +3/4 per level
                return classLevels * 3 / 4;
            case 1: // Poor BAB: +1/2 per level
                return classLevels / 2;
            default:
                return classLevels * babAtLevel3 / 3;
        }
    }

    /// <summary>
    /// Calculate total BAB for a creature with racial BAB + class BAB.
    /// They stack (DMG p.293).
    /// </summary>
    public static int CalculateTotalBAB(int racialBAB, int babAtLevel3, int classLevels)
    {
        return racialBAB + CalculateClassBAB(babAtLevel3, classLevels);
    }

    /// <summary>
    /// Calculate a save bonus from class levels.
    /// Good save: 2 + level/2 (at level 1: +2)
    /// Poor save: level/3 (at level 1: +0)
    /// </summary>
    public static int CalculateClassSave(bool isGoodSave, int classLevels)
    {
        if (classLevels <= 0) return 0;

        if (isGoodSave)
            return 2 + classLevels / 2;
        else
            return classLevels / 3;
    }

    /// <summary>
    /// Calculate total saves, using the best of racial or class progression.
    /// In 3.5e, racial and class saves stack, but we use the better base
    /// (racial save bonus or class save bonus) to avoid double-dipping.
    /// </summary>
    public static int CalculateTotalSave(int racialSaveBonus, bool isGoodSave, int classLevels)
    {
        int classSave = CalculateClassSave(isGoodSave, classLevels);
        // In practice, racial save and class save stack (DMG p.293)
        return racialSaveBonus + classSave;
    }

    /// <summary>
    /// Calculate total skill points gained from class levels.
    /// First level: (skillPoints + INT mod) × 4
    /// Subsequent levels: skillPoints + INT mod per level
    /// Minimum 1 skill point per level.
    /// </summary>
    public static int CalculateClassSkillPoints(int skillPointsPerLevel, int intModifier, int classLevels)
    {
        if (classLevels <= 0) return 0;

        int perLevel = Mathf.Max(1, skillPointsPerLevel + intModifier);
        // First class level gets ×4
        int total = perLevel * 4;
        // Remaining levels get normal
        if (classLevels > 1)
            total += perLevel * (classLevels - 1);
        return total;
    }

    /// <summary>
    /// Number of feats granted from total HD (DMG p.290).
    /// 1 feat at 1st HD, then +1 at 3rd, 6th, 9th, etc.
    /// </summary>
    public static int FeatsFromTotalHD(int totalHD)
    {
        if (totalHD <= 0) return 0;
        return 1 + (totalHD - 1) / 3;
    }

    /// <summary>
    /// Number of ability score increases from total HD (DMG p.290).
    /// +1 ability score every 4 HD (at 4, 8, 12, 16, 20...).
    /// </summary>
    public static int AbilityIncreasesFromTotalHD(int totalHD)
    {
        return totalHD / 4;
    }

    /// <summary>
    /// Calculate average hit points for class levels.
    /// First level: max hit die
    /// Subsequent levels: (hitDie + 1) / 2 + CON mod per level
    /// </summary>
    public static int CalculateClassHP(int hitDie, int conModifier, int classLevels)
    {
        if (classLevels <= 0) return 0;

        // First level: full hit die + CON
        int hp = hitDie + conModifier;
        // Subsequent levels: average roll + CON
        if (classLevels > 1)
        {
            int avgRoll = (hitDie + 1) / 2;
            hp += (avgRoll + conModifier) * (classLevels - 1);
        }
        return Mathf.Max(classLevels, hp); // At least 1 HP per level
    }

    /// <summary>
    /// Apply class levels to an NPCDefinition, modifying it in place.
    /// This handles HD, BAB, saves, HP, and tracks the class info.
    /// </summary>
    public static void ApplyClassToDefinition(NPCDefinition def, ICharacterClass classDef, int levels)
    {
        if (def == null || classDef == null || levels <= 0) return;

        int conMod = (def.CON - 10) / 2;

        // Increase HD
        int oldHD = def.HitDice;
        def.HitDice = CalculateTotalHD(def.HitDice, levels);

        // Stack BAB
        int classBAB = CalculateClassBAB(classDef.BABAtLevel3, levels);
        def.BAB += classBAB;

        // Add HP from class levels
        int classHP = CalculateClassHP(classDef.HitDie, conMod, levels);
        def.BaseHitDieHP += classHP;

        // Store class info
        def.CharacterClass = classDef.ClassName;
        def.Level = levels;

        // Add class name to special abilities for display
        def.SpecialAbilities.Add($"{classDef.ClassName} {levels}");

        // Recalculate CR
        int crAdj = CRCalculator.CalculateCRAdjustment(
            def.CreatureType, classDef.ClassName, levels, oldHD);
        float baseCR = CRCalculator.CRToFloat(def.ChallengeRating);
        int newCR = Mathf.Max(0, Mathf.RoundToInt(baseCR) + crAdj);
        def.ChallengeRating = newCR.ToString();

        Debug.Log($"[CreatureClassEngine] Applied {classDef.ClassName} {levels} to {def.Name}: " +
                  $"HD {oldHD} → {def.HitDice}, BAB +{classBAB} (total +{def.BAB}), " +
                  $"HP +{classHP} (total {def.BaseHitDieHP}), CR → {def.ChallengeRating}");
    }
}
