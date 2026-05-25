using System.Collections.Generic;

/// <summary>
/// Determines whether a character class is "associated" with a creature type
/// for CR calculation purposes (D&D 3.5e DMG p.296).
///
/// Associated classes: +1 CR per class level added.
/// Nonassociated classes: +1 CR per 2 class levels (until levels exceed racial HD).
/// </summary>
public static class ClassAssociationRules
{
    // Class groupings for association checks
    private static readonly HashSet<string> MartialClasses = new HashSet<string>
        { "Fighter", "Barbarian", "Warrior", "Ranger", "Paladin" };

    private static readonly HashSet<string> ArcaneCasterClasses = new HashSet<string>
        { "Wizard", "Sorcerer", "Bard" };

    private static readonly HashSet<string> DivineCasterClasses = new HashSet<string>
        { "Cleric", "Druid", "Adept" };

    private static readonly HashSet<string> SkillClasses = new HashSet<string>
        { "Rogue", "Expert", "Bard" };

    /// <summary>
    /// Determines if a class is associated with a creature type (DMG p.296).
    /// Associated classes match the creature's natural role/strengths.
    /// </summary>
    public static bool IsAssociatedClass(string creatureType, string className)
    {
        if (string.IsNullOrEmpty(creatureType) || string.IsNullOrEmpty(className))
            return false;

        string type = creatureType.ToLower();
        string cls = className;

        switch (type)
        {
            case "humanoid":
                // Humanoids: ALL classes are associated (DMG p.296)
                return true;

            case "monstrous humanoid":
                // Monstrous humanoids: martial and skill classes
                return MartialClasses.Contains(cls) || SkillClasses.Contains(cls) ||
                       cls == "Monk" || cls == "Commoner";

            case "giant":
                // Giants: martial classes associated
                return MartialClasses.Contains(cls) || cls == "Commoner";

            case "dragon":
                // Dragons: arcane caster classes associated
                return ArcaneCasterClasses.Contains(cls);

            case "undead":
                // Undead: divine casters and arcane casters associated
                return DivineCasterClasses.Contains(cls) || ArcaneCasterClasses.Contains(cls);

            case "outsider":
                // Outsiders: varies widely; martial and casters both apply
                return MartialClasses.Contains(cls) || ArcaneCasterClasses.Contains(cls) ||
                       DivineCasterClasses.Contains(cls);

            case "aberration":
                // Aberrations: arcane casters mostly
                return ArcaneCasterClasses.Contains(cls);

            case "magical beast":
                // Magical beasts: martial classes
                return MartialClasses.Contains(cls);

            case "fey":
                // Fey: skill and arcane classes
                return SkillClasses.Contains(cls) || ArcaneCasterClasses.Contains(cls) ||
                       cls == "Druid";

            case "construct":
                // Constructs: generally don't gain class levels, but martial if forced
                return MartialClasses.Contains(cls);

            case "ooze":
                // Oozes: no associated classes (mindless)
                return false;

            case "plant":
                // Plants: divine casters (druids)
                return cls == "Druid" || cls == "Adept";

            case "elemental":
                // Elementals: martial or related caster classes
                return MartialClasses.Contains(cls) || cls == "Druid" || cls == "Monk";

            case "vermin":
                // Vermin: no associated classes (mindless)
                return false;

            case "animal":
                // Animals can't normally gain class levels
                return false;

            default:
                // Default: martial classes are generally associated
                return MartialClasses.Contains(cls);
        }
    }

    /// <summary>
    /// Check if a class is a martial class.
    /// </summary>
    public static bool IsMartialClass(string className) => MartialClasses.Contains(className);

    /// <summary>
    /// Check if a class is an arcane caster class.
    /// </summary>
    public static bool IsArcaneCasterClass(string className) => ArcaneCasterClasses.Contains(className);

    /// <summary>
    /// Check if a class is a divine caster class.
    /// </summary>
    public static bool IsDivineCasterClass(string className) => DivineCasterClasses.Contains(className);

    /// <summary>
    /// Check if a class is a skill-focused class.
    /// </summary>
    public static bool IsSkillClass(string className) => SkillClasses.Contains(className);

    /// <summary>
    /// Check if a class is an NPC class (not a PC/PHB class).
    /// </summary>
    public static bool IsNPCClass(string className)
    {
        return className == "Adept" || className == "Aristocrat" ||
               className == "Commoner" || className == "Expert" ||
               className == "Warrior";
    }
}
