using System.Collections.Generic;

/// <summary>
/// Static definitions for skill lists and skill point calculations.
/// Now delegates class-specific data to ClassRegistry and individual class definitions.
/// Retains the master skill list and calculation methods as utility functions.
/// </summary>
public static class ClassSkillDefinitions
{
    // ========== SKILL DEFINITIONS ==========
    // All skills available in the prototype with their key abilities and trained-only status.

    /// <summary>
    /// Master list of all skills in the prototype.
    /// Each entry: (name, keyAbility, trainedOnly)
    /// </summary>
    public static readonly (string name, AbilityType ability, bool trainedOnly)[] AllSkills = new[]
    {
        ("Appraise",            AbilityType.INT, false),
        ("Balance",             AbilityType.DEX, false),
        ("Bluff",               AbilityType.CHA, false),
        ("Climb",               AbilityType.STR, false),
        ("Diplomacy",           AbilityType.CHA, false),
        ("Disable Device",      AbilityType.INT, true),
        ("Gather Information",  AbilityType.CHA, false),
        ("Hide",                AbilityType.DEX, false),
        ("Intimidate",          AbilityType.CHA, false),
        ("Jump",                AbilityType.STR, false),
        ("Listen",              AbilityType.WIS, false),
        ("Move Silently",       AbilityType.DEX, false),
        ("Open Lock",           AbilityType.DEX, true),
        ("Search",              AbilityType.INT, false),
        ("Sleight of Hand",     AbilityType.DEX, true),
        ("Spot",                AbilityType.WIS, false),
        ("Swim",                AbilityType.STR, false),
        ("Tumble",              AbilityType.DEX, true),
        ("Use Magic Device",    AbilityType.CHA, true),
    };

    // ========== SKILL POINTS PER LEVEL ==========

    /// <summary>
    /// Get the base skill points per level for a given class (before INT modifier).
    /// Delegates to ClassRegistry for class-specific values.
    /// </summary>
    public static int GetBaseSkillPointsPerLevel(string className)
    {
        ClassRegistry.Init();
        ICharacterClass classDef = ClassRegistry.GetClass(className);
        if (classDef != null)
            return classDef.SkillPointsPerLevel;
        return 2; // Default fallback
    }

    /// <summary>
    /// Calculate total skill points for a character.
    /// D&D 3.5: (base + INT modifier) per level, minimum 1 per level.
    /// At level 1, multiply by 4 (starting skill points).
    /// Example: Level 1 Rogue with INT 14 (+2) = (8 + 2) × 4 = 40 skill points
    /// </summary>
    /// <param name="className">Character class name</param>
    /// <param name="level">Character level</param>
    /// <param name="intModifier">Intelligence ability modifier</param>
    /// <returns>Total skill points available</returns>
    public static int CalculateSkillPoints(string className, int level, int intModifier)
    {
        int basePerLevel = GetBaseSkillPointsPerLevel(className);
        int perLevel = basePerLevel + intModifier;

        // Minimum 1 skill point per level
        if (perLevel < 1) perLevel = 1;

        // Level 1 gets 4× the per-level amount
        if (level == 1)
            return perLevel * 4;

        // For higher levels: level 1 (×4) + remaining levels (×1)
        int level1Points = perLevel * 4;
        int additionalPoints = perLevel * (level - 1);
        return level1Points + additionalPoints;
    }

    /// <summary>
    /// Get the class skill set for a given class name.
    /// Delegates to ClassRegistry for class-specific skill lists.
    /// </summary>
    public static HashSet<string> GetClassSkills(string className)
    {
        ClassRegistry.Init();
        ICharacterClass classDef = ClassRegistry.GetClass(className);
        if (classDef != null)
            return classDef.ClassSkills;
        return new HashSet<string>();
    }
}
