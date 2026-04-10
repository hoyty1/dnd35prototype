using System.Collections.Generic;

/// <summary>
/// Static definitions for class skill lists and skill point calculations per class.
/// D&D 3.5 rules for Fighter and Rogue classes.
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

    // ========== CLASS SKILL LISTS ==========

    /// <summary>Fighter class skills (D&D 3.5 PHB).</summary>
    public static readonly HashSet<string> FighterClassSkills = new HashSet<string>
    {
        "Climb",
        "Intimidate",
        "Jump",
        "Swim"
    };

    /// <summary>Rogue class skills (D&D 3.5 PHB).</summary>
    public static readonly HashSet<string> RogueClassSkills = new HashSet<string>
    {
        "Appraise",
        "Balance",
        "Bluff",
        "Climb",
        "Diplomacy",
        "Disable Device",
        "Gather Information",
        "Hide",
        "Intimidate",
        "Jump",
        "Listen",
        "Move Silently",
        "Open Lock",
        "Search",
        "Sleight of Hand",
        "Spot",
        "Tumble",
        "Use Magic Device"
    };

    /// <summary>Monk class skills (D&D 3.5 PHB).</summary>
    public static readonly HashSet<string> MonkClassSkills = new HashSet<string>
    {
        "Balance",
        "Climb",
        "Diplomacy",
        "Hide",
        "Jump",
        "Listen",
        "Move Silently",
        "Spot",
        "Swim",
        "Tumble"
    };

    /// <summary>Barbarian class skills (D&D 3.5 PHB).</summary>
    public static readonly HashSet<string> BarbarianClassSkills = new HashSet<string>
    {
        "Climb",
        "Intimidate",
        "Jump",
        "Listen",
        "Swim"
    };

    /// <summary>Wizard class skills (D&D 3.5 PHB). Includes Concentration and Spellcraft (simplified).</summary>
    public static readonly HashSet<string> WizardClassSkills = new HashSet<string>
    {
        "Appraise",
        "Diplomacy",
        "Search"
        // Note: Knowledge, Concentration, Spellcraft not in prototype skill list
    };

    /// <summary>Cleric class skills (D&D 3.5 PHB). Includes Concentration and knowledge skills (simplified).</summary>
    public static readonly HashSet<string> ClericClassSkills = new HashSet<string>
    {
        "Diplomacy",
        "Intimidate"
        // Note: Concentration, Heal, Knowledge (Religion) not in prototype skill list
    };

    // ========== SKILL POINTS PER LEVEL ==========

    /// <summary>
    /// Get the base skill points per level for a given class (before INT modifier).
    /// Fighter: 2, Rogue: 8, Monk: 4, Barbarian: 4
    /// </summary>
    public static int GetBaseSkillPointsPerLevel(string className)
    {
        switch (className)
        {
            case "Fighter":   return 2;
            case "Rogue":     return 8;
            case "Monk":      return 4;
            case "Barbarian": return 4;
            case "Wizard":    return 2;
            case "Cleric":    return 2;
            default:          return 2; // Default to fighter-like
        }
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
    /// </summary>
    public static HashSet<string> GetClassSkills(string className)
    {
        switch (className)
        {
            case "Fighter":   return FighterClassSkills;
            case "Rogue":     return RogueClassSkills;
            case "Monk":      return MonkClassSkills;
            case "Barbarian": return BarbarianClassSkills;
            case "Wizard":    return WizardClassSkills;
            case "Cleric":    return ClericClassSkills;
            default:          return new HashSet<string>();
        }
    }
}
