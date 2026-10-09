using UnityEngine;

/// <summary>
/// Class-level arithmetic shared by every character with class levels, PC or NPC (CHR-001, CHR-002):
/// base attack bonus and hit die come from the class definitions in <see cref="ClassRegistry"/>, and hit points
/// follow PHB p.9, p.23 and p.59: each Hit Die adds its roll plus the CON modifier, minimum 1 hit point per die.
/// <see cref="CharacterStats"/>, <see cref="LevelUpCalculator"/>, <see cref="CharacterCreationData"/> and
/// <see cref="CreatureClassEngine"/> all call these helpers, so there is no second class table to keep in sync.
/// </summary>
public static class ClassProgression
{
    /// <summary>Hit die used for a class name that has no registered definition (none of the PHB or DMG classes).</summary>
    public const int UnknownClassHitDie = 8;

    /// <summary>
    /// BAB encoding used for a class name that has no registered definition: full progression, as the old
    /// hard-coded tables did. Every PHB and DMG NPC class is registered, so this only covers stand-in names.
    /// </summary>
    public const int UnknownClassBabAtLevel3 = 3;

    /// <summary>The registered class definition, or null. Quiet (no warning) and case-insensitive.</summary>
    public static ICharacterClass GetDefinition(string className)
    {
        return ClassRegistry.TryGetClass(className, out ICharacterClass classDef) ? classDef : null;
    }

    /// <summary>Hit die sides of a class (PHB ch.3 class tables, DMG p.107-110 NPC classes).</summary>
    public static int GetHitDie(string className)
    {
        ICharacterClass classDef = GetDefinition(className);
        return classDef != null && classDef.HitDie > 0 ? classDef.HitDie : UnknownClassHitDie;
    }

    /// <summary>
    /// Base attack bonus from <paramref name="classLevel"/> levels of one class (PHB Table 3-1, p.22: good = level,
    /// average = 3/4 level, poor = 1/2 level, rounded down). A multiclass character adds the values of its classes
    /// (PHB p.59).
    /// </summary>
    public static int GetClassBaseAttackBonus(string className, int classLevel)
    {
        ICharacterClass classDef = GetDefinition(className);
        int babAtLevel3 = classDef != null ? classDef.BABAtLevel3 : UnknownClassBabAtLevel3;
        return BaseAttackBonusForProgression(babAtLevel3, classLevel);
    }

    /// <summary>
    /// BAB for <paramref name="classLevels"/> levels of a progression encoded as <see cref="ICharacterClass.BABAtLevel3"/>:
    /// 3 = good (1 per level), 2 = average (3/4 per level), 1 = poor (1/2 per level).
    /// </summary>
    public static int BaseAttackBonusForProgression(int babAtLevel3, int classLevels)
    {
        if (classLevels <= 0) return 0;

        switch (babAtLevel3)
        {
            case 3: return classLevels;
            case 2: return classLevels * 3 / 4;
            case 1: return classLevels / 2;
            default: return classLevels * babAtLevel3 / 3;
        }
    }

    /// <summary>
    /// Base save from <paramref name="classLevels"/> levels of one class (PHB Table 3-1, p.22): good 2 + level/2, poor
    /// level/3, 0 without levels. A multiclass character adds the values of its classes (PHB p.59), and a creature adds
    /// them to the base saves of its racial Hit Dice (MM p.290; CRE-004).
    /// </summary>
    public static int BaseSaveForProgression(bool goodSave, int classLevels)
    {
        if (classLevels <= 0) return 0;
        return goodSave ? 2 + classLevels / 2 : classLevels / 3;
    }

    /// <summary>
    /// Whether <paramref name="className"/> has a good <paramref name="save"/> (PHB ch.3 class tables, DMG p.107-110 NPC
    /// classes). False for a class name with no registered definition.
    /// </summary>
    public static bool IsGoodSave(string className, SavingThrowType save)
    {
        ICharacterClass classDef = GetDefinition(className);
        if (classDef == null) return false;
        switch (save)
        {
            case SavingThrowType.Fortitude: return classDef.GoodFortitude;
            case SavingThrowType.Reflex: return classDef.GoodReflex;
            default: return classDef.GoodWill;
        }
    }

    /// <summary>Base <paramref name="save"/> from <paramref name="classLevel"/> levels of one class (CRE-004).</summary>
    public static int GetClassBaseSave(string className, int classLevel, SavingThrowType save)
    {
        return BaseSaveForProgression(IsGoodSave(className, save), classLevel);
    }

    /// <summary>Hit points one Hit Die gives: the die result plus the CON modifier, minimum 1 (PHB p.9, p.23, p.59).</summary>
    public static int HitPointsForHitDie(int dieResult, int conModifier)
    {
        return Mathf.Max(1, dieResult + conModifier);
    }

    /// <summary>
    /// The average result of one hit die, rounded up (d4 3, d6 4, d8 5, d10 6, d12 7). Used where the game
    /// takes an average instead of a roll: Quick Start and preset characters after 1st level, the Average level-up
    /// mode and the level-up preview. PHB p.23 has players roll; this is the game's own shortcut.
    /// </summary>
    public static int AverageHitDieResult(int hitDie)
    {
        return Mathf.Max(1, hitDie / 2 + 1);
    }

    /// <summary>
    /// Hit points of a character built at <paramref name="level"/> in one class: the maximum at 1st level (PHB p.23),
    /// the average die after that, each die plus the CON modifier with a minimum of 1.
    /// </summary>
    public static int CreationHitPoints(int hitDie, int conModifier, int level)
    {
        int safeLevel = Mathf.Max(1, level);
        int hp = HitPointsForHitDie(hitDie, conModifier);
        if (safeLevel > 1)
            hp += (safeLevel - 1) * HitPointsForHitDie(AverageHitDieResult(hitDie), conModifier);
        return hp;
    }

    /// <summary>
    /// Adds the CON modifier once per Hit Die to a CON-free hit point total, with the PHB minimum of 1 hit point per
    /// die applied to the total (the dice themselves are not known here). Used by the <see cref="CharacterStats"/>
    /// constructor; exact whenever no single die falls below 1.
    /// </summary>
    public static int ApplyConstitution(int conFreeHitPoints, int hitDice, int conModifier)
    {
        int safeHitDice = Mathf.Max(1, hitDice);
        return Mathf.Max(safeHitDice, conFreeHitPoints + conModifier * safeHitDice);
    }

    /// <summary>
    /// The CON modifier that hit points use: 0 for a creature with no Constitution score. Undead and constructs have
    /// none (MM p.307, p.317); the NPC data stores that as <see cref="CharacterStats.NO_SCORE"/> or as CON 0 (15 MM
    /// entries, CRE-044), and a living creature at CON 0 is dead anyway, so a score of 0 or less adds nothing.
    /// </summary>
    public static int HitPointConstitutionModifier(int conScore)
    {
        return conScore <= 0 ? 0 : CharacterStats.GetModifier(conScore);
    }

    /// <summary>
    /// Max hit points of a creature spawned from an <see cref="NPCDefinition"/> (CHR-001, CRE-041). A definition total
    /// (<paramref name="definitionTotal"/> above 0) is final: it is the MM statistics-block total, which already counts
    /// CON per Hit Die, plus whatever templates and <see cref="CreatureClassEngine"/> class levels added with CON per die.
    /// Without one, the creature type's average die per Hit Die plus the CON modifier per Hit Die, minimum 1 per die.
    /// Feat hit points (Toughness) are added on top by <see cref="CharacterStats.TotalMaxHP"/>.
    /// </summary>
    public static int CreatureMaxHitPoints(int definitionTotal, int creatureHitDie, int hitDice, int conScore)
    {
        if (definitionTotal > 0)
            return definitionTotal;

        int safeHitDice = Mathf.Max(1, hitDice);
        int averageDice = ProgressionCalculator.CalculateAverageHpFromHitDice(creatureHitDie, safeHitDice);
        return ApplyConstitution(averageDice, safeHitDice, HitPointConstitutionModifier(conScore));
    }
}
