using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Engine for applying character class levels to creatures (MM p.290-291). Sets HD, hit points, the reference BAB
/// and CR on the definition; BAB and base saves are computed at spawn from racial HD plus class levels (CRE-004).
/// Feats, skill points and ability increases from class levels are helpers only and are not applied yet.
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
        // The one BAB formula every character with class levels uses (CHR-002).
        return ClassProgression.BaseAttackBonusForProgression(babAtLevel3, classLevels);
    }

    /// <summary>
    /// Calculate total BAB for a creature with racial BAB + class BAB.
    /// They stack (MM p.290).
    /// </summary>
    public static int CalculateTotalBAB(int racialBAB, int babAtLevel3, int classLevels)
    {
        return racialBAB + CalculateClassBAB(babAtLevel3, classLevels);
    }

    /// <summary>
    /// Base save from class levels (PHB Table 3-1): good 2 + level/2, poor level/3, 0 without levels
    /// (ClassProgression.BaseSaveForProgression, the formula CharacterStats uses).
    /// </summary>
    public static int CalculateClassSave(bool isGoodSave, int classLevels)
    {
        return ClassProgression.BaseSaveForProgression(isGoodSave, classLevels);
    }

    /// <summary>
    /// Total base save of a creature: its racial base save plus the class base save. They stack (MM p.290, PHB p.59),
    /// so a good racial save and a good class save each bring their +2.
    /// </summary>
    public static int CalculateTotalSave(int racialSaveBonus, bool isGoodSave, int classLevels)
    {
        return racialSaveBonus + CalculateClassSave(isGoodSave, classLevels);
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
    /// First level: max hit die; subsequent levels: (hitDie + 1) / 2. Each die adds the CON modifier with a minimum
    /// of 1 hit point per die (PHB p.9, p.23, p.59; ClassProgression.HitPointsForHitDie, CHR-001).
    /// </summary>
    public static int CalculateClassHP(int hitDie, int conModifier, int classLevels)
    {
        if (classLevels <= 0) return 0;

        int hp = ClassProgression.HitPointsForHitDie(hitDie, conModifier);
        if (classLevels > 1)
        {
            int avgRoll = (hitDie + 1) / 2;
            hp += ClassProgression.HitPointsForHitDie(avgRoll, conModifier) * (classLevels - 1);
        }
        return hp;
    }

    /// <summary>
    /// True when class levels replace the creature's Hit Dice instead of adding to them: a humanoid with 1 HD or less
    /// (MM p.290 "Humanoids and Class Levels", MM p.310). Its one Hit Die is a 1st-level warrior presented in the entry,
    /// so a goblin adept 1 is an adept 1 with the adept's BAB and saves, not a warrior 1 / adept 1.
    /// </summary>
    public static bool ClassLevelsReplaceHitDice(NPCDefinition def)
    {
        if (def == null) return false;
        return def.ResolveTotalHitDice() <= 1
            && CreatureTypeProgressionDatabase.TryParseCreatureType(def.CreatureType, out CreatureTypeId type)
            && type == CreatureTypeId.Humanoid;
    }

    /// <summary>
    /// Real levels <paramref name="def"/> already has in <paramref name="classDef"/>'s class: its <see cref="NPCDefinition.Level"/>
    /// when its class is that class and is not a racial-HD stand-in, else 0.
    /// </summary>
    public static int RealLevelsInClass(NPCDefinition def, ICharacterClass classDef)
    {
        if (def == null || classDef == null || def.ResolveClassLevelsAreRacialHitDice())
            return 0;
        return string.Equals(def.CharacterClass, classDef.ClassName, System.StringComparison.OrdinalIgnoreCase)
            ? Mathf.Max(0, def.Level) : 0;
    }

    /// <summary>
    /// Applies a DMG encounter table row's "<paramref name="classLevel"/>th-level <paramref name="classDef"/>" to
    /// <paramref name="def"/>: the row names the creature's level in that class, so levels the base already has in it
    /// count toward it. DMG p.97 lists "vampire, 5th-level human fighter" and "ghost, 5th-level human fighter" at CR 7,
    /// the MM samples (MM p.117, p.250: the template on a human fighter 5), and the `ghost` and `vampire` entries already
    /// are those fighter 5s, so the row adds nothing. Otherwise the missing levels are added by
    /// <see cref="ApplyClassToDefinition"/>. Returns the levels added (0 when the base already has the class level).
    /// </summary>
    public static int ApplyEncounterClassLevel(NPCDefinition def, ICharacterClass classDef, int classLevel)
    {
        if (def == null || classDef == null || classLevel <= 0)
            return 0;
        int toAdd = classLevel - RealLevelsInClass(def, classDef);
        if (toAdd <= 0)
        {
            Debug.Log($"[CreatureClassEngine] {def.Name} already is a {def.CharacterClass} {def.Level}; " +
                      $"the row's {classDef.ClassName} {classLevel} adds no levels.");
            return 0;
        }
        ApplyClassToDefinition(def, classDef, toAdd);
        return toAdd;
    }

    /// <summary>
    /// Apply class levels to an NPCDefinition, modifying it in place (MM p.290-291: a creature with a class follows the
    /// multiclass rules, PHB p.59-60; its Hit Dice are its racial Hit Dice plus its class levels). Sets HD, the class,
    /// hit points, the reference BAB and CR. BAB and base saves are computed at spawn from the racial Hit Dice
    /// (<see cref="NPCDefinition.ResolveRacialHitDice"/>) and the class (CharacterStats, CRE-004).
    /// A humanoid with 1 HD or less loses its Hit Die, hit points, BAB override and save overrides to the class
    /// (<see cref="ClassLevelsReplaceHitDice"/>).
    /// </summary>
    public static void ApplyClassToDefinition(NPCDefinition def, ICharacterClass classDef, int levels)
    {
        if (def == null || classDef == null || levels <= 0) return;

        // The ability modifier (rounds down), not a truncating (CON - 10) / 2; 0 without a CON score, including the
        // CON 0 that some undead data uses (ClassProgression.HitPointConstitutionModifier).
        int conMod = ClassProgression.HitPointConstitutionModifier(def.CON);

        int oldHD = def.HitDice;
        bool replaces = ClassLevelsReplaceHitDice(def);
        bool hadRealClass = !def.ResolveClassLevelsAreRacialHitDice();
        // A base that already has real levels in the same class adds the new ones to them, so the definition keeps one
        // class entry (QuickSpawnSystem on a classed NPC). A DMG table row that names the class level of a complete
        // class-level sample (the MM ghost and vampire are human fighter 5s) goes through ApplyEncounterClassLevel,
        // which adds nothing here.
        int priorLevelsInClass = replaces ? 0 : RealLevelsInClass(def, classDef);
        int classBAB = CalculateClassBAB(classDef.BABAtLevel3, levels);
        int classHP = CalculateClassHP(classDef.HitDie, conMod, levels);

        if (replaces)
        {
            // MM p.290: the humanoid Hit Die, its attack bonus and its saves give way to the class.
            def.HitDice = Mathf.Max(1, levels);
            def.BAB = classBAB;
            def.BaseHitDieHP = classHP;
            def.BaseAttackBonusOverride = null;
            def.BABOverride = null;
            def.FortitudeSaveOverride = null;
            def.ReflexSaveOverride = null;
            def.WillSaveOverride = null;
        }
        else
        {
            if (hadRealClass && priorLevelsInClass == 0)
                Debug.LogWarning($"[CreatureClassEngine] {def.Name} already has real {def.CharacterClass} {def.Level} levels; " +
                                 $"one NPCDefinition holds one class, so they now count as racial Hit Dice (multiclass NPC data is not modelled).");

            // A definition without a hit point total (BaseHitDieHP 0) gets its racial total first, so the class HP
            // below adds to it instead of replacing it (ClassProgression.CreatureMaxHitPoints treats any total as final).
            if (def.BaseHitDieHP <= 0)
            {
                CreatureTypeProgression typeProgression = CreatureTypeProgressionDatabase.GetFromString(def.CreatureType);
                def.BaseHitDieHP = ClassProgression.CreatureMaxHitPoints(0, typeProgression.HitDie, def.ResolveTotalHitDice(), def.CON);
            }

            def.HitDice = CalculateTotalHD(def.HitDice, levels);
            // Class BAB stacks with racial BAB (MM p.290). An explicit racial BAB override keeps standing for the racial
            // part, so the class adds to it; the type progressions apply to the racial HD only.
            def.BAB += classBAB;
            if (def.BaseAttackBonusOverride.HasValue)
                def.BaseAttackBonusOverride = def.BaseAttackBonusOverride.Value + classBAB;
            def.BaseHitDieHP += classHP;
        }

        // A humanoid with more than 1 racial HD keeps them beside the class, and with them the humanoid type's
        // proficiencies (MM p.310); record that before the class overwrites the stand-in marker (CHR-072).
        if (oldHD > 1 && def.ResolveClassLevelsAreRacialHitDice()
            && CreatureTypeProgressionDatabase.TryParseCreatureType(def.CreatureType, out CreatureTypeId baseType)
            && baseType == CreatureTypeId.Humanoid)
            def.HasRacialHumanoidHitDice = true;

        // Store class info
        def.CharacterClass = classDef.ClassName;
        def.Level = priorLevelsInClass + levels;
        // The class replaces any racial-HD stand-in class, so its levels are real and grant proficiency (CHR-072).
        def.ClassLevelsAreRacialHitDice = false;

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
