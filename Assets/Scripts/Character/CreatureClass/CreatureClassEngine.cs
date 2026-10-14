using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How a creature gains class levels, which decides its ability scores and whether a Hit Die is maximized (CRE-058,
/// delegated RAW readings 2026-10-14; DMG p.78, p.110, p.172-173; MM p.290-294).
/// </summary>
public enum ClassAdvancement
{
    /// <summary>
    /// A class added to an MM monster outside the DMG NPC rows (QuickSpawnSystem, hand-built encounters): a PC class gets
    /// the elite array (MM p.294) and a maximized first Hit Die only when its level replaces a 1-HD humanoid's Hit Die
    /// (the MM samples on human bases); on a base that keeps racial Hit Dice every die is averaged (MM p.199 ogre
    /// barbarian). An NPC class gets the nonelite array and averages every die (MM p.294, DMG p.110).
    /// </summary>
    Monster,

    /// <summary>
    /// A DMG encounter table row labelled "NPC" (DMG p.78 step 4 sends it to DMG chapter 4): an elite character
    /// (DMG p.110): the elite array as the class table arranges it, and the creature's first Hit Die at the maximum, the
    /// first class die when the class replaces a 1-HD humanoid's Hit Die, else the first racial die (DMG p.172-173).
    /// </summary>
    DmgNpc,

    /// <summary>
    /// A DMG row that names an MM stat block ("ogre barbarian, 4th level", "half-dragon 4th-level fighter", "ghost,
    /// 5th-level fighter"): the printed sample. When the base already is the sample, <see cref="CreatureClassEngine.ApplyEncounterClassLevel"/>
    /// adds no levels. When it is not (no sample registered, CRE-069), the levels are added by the rule the MM samples
    /// themselves follow (MM p.294, as <see cref="Monster"/>: the MM p.199 ogre barbarian is the elite array plus the
    /// ogre's adjustments with every die averaged, 79 hp), and the base keeps its own alignment line.
    /// </summary>
    PrintedSample
}

/// <summary>
/// Engine for applying character class levels to creatures (MM p.290-291). Sets HD, hit points, the reference BAB,
/// the ability scores (elite or nonelite array, racial adjustments, increases by total Hit Dice; CRE-058) and CR on the
/// definition; BAB and base saves are computed at spawn from racial HD plus class levels (CRE-004). Feats and skill
/// points from class levels are helpers only and are not applied yet.
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
    /// Hit points that <paramref name="classLevels"/> class levels add to a creature. The first class level gets the
    /// maximum die only when it is the creature's first Hit Die and the creature gets the maximum on it
    /// (<paramref name="firstLevelIsFirstHitDie"/>: a PC class replacing a 1-HD humanoid's Hit Die, MM p.290; PHB p.22-23
    /// and DMG p.110, elite characters get the maximum first Hit Die; the DMG table "NPC" rows are elite and so are the MM
    /// samples on human bases, while NPC classes average it; delegated RAW readings 2026-10-14, CRE-058); every other die is averaged with the MM pooled
    /// average over the creature's averaged dice, rounded down once (owner interpretation 2026-10-11, CRE-041;
    /// <see cref="ClassProgression.PooledAverageHitDice"/>), which continues a pool already holding
    /// <paramref name="halfPointDiceBefore"/> half points. Each die adds the CON modifier, minimum 1 hit point per die
    /// (PHB p.9, p.59). An ogre (4d8, a pool of 18) given barbarian 1 gets 6 + CON for the d12 (6.5 rounded down with
    /// the pool), not 12 + CON.
    /// </summary>
    public static int CalculateClassHP(int hitDie, int conModifier, int classLevels, bool firstLevelIsFirstHitDie = true, int halfPointDiceBefore = 0)
    {
        if (classLevels <= 0) return 0;

        if (!firstLevelIsFirstHitDie)
            return ClassProgression.AveragedHitPoints(hitDie, classLevels, conModifier, halfPointDiceBefore);

        return ClassProgression.HitPointsForHitDie(hitDie, conModifier)
            + ClassProgression.AveragedHitPoints(hitDie, classLevels - 1, conModifier, halfPointDiceBefore);
    }

    /// <summary>
    /// The half-point dice in <paramref name="def"/>'s averaged hit point pool: every Hit Die is averaged (MM totals
    /// average every die) except a maximized first die (<see cref="NPCDefinition.FirstHitDieMaximized"/>: a PC class that
    /// replaced a 1-HD humanoid's Hit Die, or an elite DMG-table NPC's first racial die, CRE-058). Every Hit Die size is
    /// even, so each averaged die carries a half point (CRE-041).
    /// </summary>
    public static int AveragedHalfPointDice(NPCDefinition def)
    {
        if (def == null)
            return 0;
        int dice = Mathf.Max(0, def.ResolveTotalHitDice());
        return Mathf.Max(0, def.FirstHitDieMaximized ? dice - 1 : dice);
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
    /// <see cref="ApplyClassToDefinition"/> with <paramref name="advancement"/> (a row labelled "NPC" is
    /// <see cref="ClassAdvancement.DmgNpc"/>, one naming an MM sample <see cref="ClassAdvancement.PrintedSample"/>).
    /// Returns the levels added (0 when the base already has the class level).
    /// </summary>
    public static int ApplyEncounterClassLevel(NPCDefinition def, ICharacterClass classDef, int classLevel,
        ClassAdvancement advancement = ClassAdvancement.Monster)
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
        ApplyClassToDefinition(def, classDef, toAdd, advancement);
        return toAdd;
    }

    /// <summary>
    /// A human's alignment as a class NPC: its class's typical alignment from PHB p.104 Table 6-1 (Paladins LG, Rangers
    /// CG, Monks and Wizards LN, Druids N, Barbarians, Bards and Rogues CN), else the Humans entry, neutral (PHB p.13:
    /// humans tend toward no alignment). Delegated RAW reading 2026-10-14 (CRE-056); other races keep their own listed
    /// alignment line.
    /// </summary>
    public static Alignment TypicalHumanAlignmentForClass(string className)
    {
        switch (className)
        {
            case "Paladin": return Alignment.LawfulGood;
            case "Ranger": return Alignment.ChaoticGood;
            case "Monk":
            case "Wizard": return Alignment.LawfulNeutral;
            case "Barbarian":
            case "Bard":
            case "Rogue": return Alignment.ChaoticNeutral;
            default: return Alignment.TrueNeutral;
        }
    }

    /// <summary>
    /// Rebuilds <paramref name="def"/>'s ability scores for the class it is gaining (CRE-058, delegated RAW readings
    /// 2026-10-14): a PC class starts from the elite array as the DMG chapter 4 class table arranges it, an NPC class from
    /// the nonelite array (MM p.294; arranged by <see cref="StatArrayApplier.GetPriorityStats"/>); then the racial
    /// adjustments (DMG p.126-127; <see cref="StatArrayApplier.TryGetRacialAdjustments"/>) and one point at every fourth
    /// Hit Die the creature reaches with the class levels (MM p.291-292: not for Hit Dice its racial ones already
    /// reached), on the ability the class table raises. A 1-HD humanoid given an NPC class keeps its MM warrior scores,
    /// which already are the nonelite array. A nonability (score 0) stays 0. Returns false when the scores are kept.
    /// </summary>
    private static bool TryRebuildAbilityScores(NPCDefinition def, ICharacterClass classDef, int levels, bool replaces,
        bool isPcClass, int racialHitDice)
    {
        if (replaces && !isPcClass)
            return false;
        if (!StatArrayApplier.TryGetRacialAdjustments(def, replaces, out int[] adjustments))
        {
            Debug.LogWarning($"[CreatureClassEngine] {def.Name}: no racial adjustments known for this 1-HD humanoid; " +
                             $"its ability scores are kept for {classDef.ClassName} (CRE-058).");
            return false;
        }

        int[] array;
        if (!isPcClass || !StatArrayApplier.TryGetDmgNpcStartingScores(classDef.ClassName, out array))
            array = StatArrayApplier.ArrangeByPriority(isPcClass ? StatArrayType.Elite : StatArrayType.Nonelite, classDef.ClassName);

        int[] stored = { def.STR, def.DEX, def.CON, def.INT, def.WIS, def.CHA };
        var scores = new int[6];
        for (int i = 0; i < 6; i++)
            scores[i] = stored[i] <= 0 ? stored[i] : Mathf.Max(1, array[i] + adjustments[i]);

        int totalHitDice = racialHitDice + levels;
        for (int hd = 4; hd <= totalHitDice; hd += 4)
        {
            if (hd <= racialHitDice)
                continue;
            int ability = StatArrayApplier.DmgNpcIncreaseAbility(classDef.ClassName, hd / 4);
            if (ability >= 0 && scores[ability] > 0)
                scores[ability]++;
        }

        Debug.Log($"[CreatureClassEngine] {def.Name} {classDef.ClassName}: ability scores " +
                  $"{def.STR}/{def.DEX}/{def.CON}/{def.INT}/{def.WIS}/{def.CHA} -> " +
                  $"{scores[0]}/{scores[1]}/{scores[2]}/{scores[3]}/{scores[4]}/{scores[5]} " +
                  $"({(isPcClass ? "elite" : "nonelite")} array, CRE-058)");
        def.STR = scores[StatArrayApplier.Str];
        def.DEX = scores[StatArrayApplier.Dex];
        def.CON = scores[StatArrayApplier.Con];
        def.INT = scores[StatArrayApplier.Int];
        def.WIS = scores[StatArrayApplier.Wis];
        def.CHA = scores[StatArrayApplier.Cha];
        return true;
    }

    /// <summary>
    /// Apply class levels to an NPCDefinition, modifying it in place (MM p.290-291: a creature with a class follows the
    /// multiclass rules, PHB p.59-60; its Hit Dice are its racial Hit Dice plus its class levels). Sets HD, the class,
    /// hit points, the ability scores, the reference BAB and CR. BAB and base saves are computed at spawn from the racial
    /// Hit Dice (<see cref="NPCDefinition.ResolveRacialHitDice"/>) and the class (CharacterStats, CRE-004).
    /// A humanoid with 1 HD or less loses its Hit Die, hit points, BAB override and save overrides to the class
    /// (<see cref="ClassLevelsReplaceHitDice"/>). <paramref name="advancement"/> decides the ability array and the
    /// maximized Hit Die (<see cref="ClassAdvancement"/>; CRE-058, delegated RAW readings 2026-10-14). A base that already
    /// has real class levels and keeps them keeps its scores.
    /// </summary>
    public static void ApplyClassToDefinition(NPCDefinition def, ICharacterClass classDef, int levels,
        ClassAdvancement advancement = ClassAdvancement.Monster)
    {
        if (def == null || classDef == null || levels <= 0) return;

        int oldHD = def.HitDice;
        bool replaces = ClassLevelsReplaceHitDice(def);
        bool hadRealClass = !def.ResolveClassLevelsAreRacialHitDice();
        bool isPcClass = !ClassAssociationRules.IsNPCClass(classDef.ClassName);
        // A base that already has real levels in the same class adds the new ones to them, so the definition keeps one
        // class entry (QuickSpawnSystem on a classed NPC). A DMG table row that names the class level of a complete
        // class-level sample (the MM ghost and vampire are human fighter 5s) goes through ApplyEncounterClassLevel,
        // which adds nothing here.
        int priorLevelsInClass = replaces ? 0 : RealLevelsInClass(def, classDef);
        int classBAB = CalculateClassBAB(classDef.BABAtLevel3, levels);
        int racialHitDice = replaces ? 0 : def.ResolveTotalHitDice();

        // The ability modifier (rounds down), not a truncating (CON - 10) / 2; 0 without a CON score, including the
        // CON 0 that some undead data uses (ClassProgression.HitPointConstitutionModifier).
        int oldConMod = ClassProgression.HitPointConstitutionModifier(def.CON);
        bool oldHasCon = def.CON > 0;
        // A PrintedSample row whose base is not the sample is built by the MM p.294 rule the sample follows, the same
        // rebuild as Monster (CRE-058 items 5 and 7, delegated RAW reading 2026-10-14).
        bool rebuilt = (replaces || !hadRealClass)
                       && TryRebuildAbilityScores(def, classDef, levels, replaces, isPcClass, racialHitDice);
        int conMod = ClassProgression.HitPointConstitutionModifier(def.CON);

        if (replaces)
        {
            // The class level is the creature's first Hit Die. DMG p.110: elite characters get the maximum on their first
            // Hit Die. A PC class here is elite (a DMG table "NPC" row, DMG p.78 step 4 and p.110; or an MM sample on a
            // human base, MM p.146: the half-dragon fighter's 43 hp); an NPC class is an average character and averages it
            // like every other die (MM p.294; the MM 1st-level warriors average their d8). Delegated RAW readings
            // 2026-10-14 (CRE-058). A printed sample keeps the base's own rule (PC class: maximum).
            bool maximize = isPcClass;
            int classHP = CalculateClassHP(classDef.HitDie, conMod, levels, firstLevelIsFirstHitDie: maximize);
            // MM p.290: the humanoid Hit Die, its attack bonus and its saves give way to the class.
            def.HitDice = Mathf.Max(1, levels);
            def.BAB = classBAB;
            // Like an MM total, the new total counts the definition's feat hit points (the replaced humanoid keeps its MM
            // feat, CRE-058), which the spawn leaves out of MaxHP (ClassProgression.ExcludeCountedFeatHitPoints, CRE-041).
            def.BaseHitDieHP = classHP + FeatManager.HitPointBonusForFeats(def.Feats);
            def.FirstHitDieMaximized = maximize;
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

            CreatureTypeProgression typeProgression = CreatureTypeProgressionDatabase.GetFromString(def.CreatureType);
            int racialDie = typeProgression.HitDie;

            // A definition without a hit point total (BaseHitDieHP 0) gets its racial total first, so the class HP
            // below adds to it instead of replacing it (ClassProgression.CreatureMaxHitPoints treats any total as final).
            // Like an MM total it counts the definition's feat hit points (Toughness), which the spawn then leaves out
            // of MaxHP (ClassProgression.ExcludeCountedFeatHitPoints, CRE-041).
            if (def.BaseHitDieHP <= 0)
            {
                def.BaseHitDieHP = ClassProgression.CreatureMaxHitPoints(0, racialDie, def.ResolveTotalHitDice(), def.CON)
                    + FeatManager.HitPointBonusForFeats(def.Feats);
            }
            else if (rebuilt && oldHasCon && def.CON > 0 && conMod != oldConMod)
            {
                // The new Constitution modifier applies to every racial Hit Die too (PHB p.9), minimum 1 per die.
                int racialDice = Mathf.Max(1, def.ResolveTotalHitDice());
                def.BaseHitDieHP = Mathf.Max(racialDice, def.BaseHitDieHP + (conMod - oldConMod) * racialDice);
            }

            // An elite DMG-table NPC that keeps its racial Hit Dice gets the maximum on its first Hit Die, which is its first
            // racial die (DMG p.110; DMG p.172-173: "the first Hit Die is treated as maximum"); the other racial dice and
            // every class die stay in the pooled average (CRE-041). Outside the DMG NPC rows every die is averaged
            // (MM p.199 ogre barbarian). Delegated RAW reading 2026-10-14 (CRE-058).
            if (advancement == ClassAdvancement.DmgNpc && isPcClass && !hadRealClass && !def.FirstHitDieMaximized)
            {
                int racialDice = Mathf.Max(1, def.ResolveTotalHitDice());
                def.BaseHitDieHP += racialDie
                    + ClassProgression.PooledAverageHitDice(racialDie, racialDice - 1)
                    - ClassProgression.PooledAverageHitDice(racialDie, racialDice);
                def.FirstHitDieMaximized = true;
            }

            // The creature already has Hit Dice, so no class die is its first: every class die joins the averaged pool
            // of its existing dice (MM p.290-291, PHB p.59-60; owner interpretation 2026-10-11, CRE-041).
            int classHP = CalculateClassHP(classDef.HitDie, conMod, levels, firstLevelIsFirstHitDie: false,
                halfPointDiceBefore: AveragedHalfPointDice(def));

            def.HitDice = CalculateTotalHD(def.HitDice, levels);
            // Class BAB stacks with racial BAB (MM p.290). An explicit racial BAB override keeps standing for the racial
            // part, so the class adds to it; the type progressions apply to the racial HD only.
            def.BAB += classBAB;
            if (def.BaseAttackBonusOverride.HasValue)
                def.BaseAttackBonusOverride = def.BaseAttackBonusOverride.Value + classBAB;
            def.BaseHitDieHP += classHP;
        }

        // A human class NPC takes its class's typical alignment (PHB p.104 Table 6-1; delegated RAW reading 2026-10-14,
        // CRE-056); every other creature keeps its race's listed alignment, and a printed MM sample keeps the sample's
        // (DMG p.78 step 4: the MM statistics block, alignment line included).
        if (advancement != ClassAdvancement.PrintedSample
            && string.Equals(def.RaceName, "Human", System.StringComparison.OrdinalIgnoreCase) && (replaces || !hadRealClass))
            def.CharacterAlignment = TypicalHumanAlignmentForClass(classDef.ClassName);

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
                  $"HP total {def.BaseHitDieHP}, CR → {def.ChallengeRating}");
    }
}
