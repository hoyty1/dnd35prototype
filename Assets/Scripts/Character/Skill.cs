using UnityEngine;

/// <summary>
/// Represents a single D&D 3.5 skill with ranks, key ability, and class skill status.
/// 
/// D&D 3.5 Skill Rules:
/// - Max ranks = character level + 3
/// - Class skill bonus: +3 if you have at least 1 rank AND it's a class skill
/// - Total bonus = ranks + ability modifier + class skill bonus
/// - Skill check = d20 + total bonus
/// - Trained only skills can't be used with 0 ranks
/// </summary>
[System.Serializable]
public class Skill
{
    /// <summary>Name of the skill (e.g., "Hide", "Spot", "Diplomacy").</summary>
    public string SkillName;

    /// <summary>Number of ranks (skill points) invested in this skill.</summary>
    public int Ranks;

    /// <summary>Which ability score modifies this skill.</summary>
    public AbilityType KeyAbility;

    /// <summary>Whether this is a class skill for the character's class.</summary>
    public bool IsClassSkill;

    /// <summary>Whether this skill requires training (at least 1 rank) to use.</summary>
    public bool TrainedOnly;

    /// <summary>
    /// Create a new skill instance.
    /// </summary>
    /// <param name="name">Skill name</param>
    /// <param name="keyAbility">Associated ability score</param>
    /// <param name="isClassSkill">Whether this is a class skill</param>
    /// <param name="trainedOnly">Whether training is required to use</param>
    public Skill(string name, AbilityType keyAbility, bool isClassSkill = false, bool trainedOnly = false)
    {
        SkillName = name;
        KeyAbility = keyAbility;
        IsClassSkill = isClassSkill;
        TrainedOnly = trainedOnly;
        Ranks = 0;
    }

    /// <summary>
    /// Get the class skill bonus (+3 if class skill with at least 1 rank, 0 otherwise).
    /// D&D 3.5 Pathfinder-style: +3 bonus for trained class skills.
    /// </summary>
    public int ClassSkillBonus => (IsClassSkill && Ranks > 0) ? 3 : 0;

    /// <summary>
    /// Get the total skill bonus: ranks + ability modifier + class skill bonus.
    /// </summary>
    /// <param name="abilityModifier">The ability modifier for the key ability</param>
    /// <returns>Total skill bonus</returns>
    public int GetTotalBonus(int abilityModifier)
    {
        return Ranks + abilityModifier + ClassSkillBonus;
    }

    /// <summary>
    /// Roll a skill check: d20 + total bonus.
    /// Returns -1 if the skill is trained only and character has 0 ranks.
    /// </summary>
    /// <param name="abilityModifier">The ability modifier for the key ability</param>
    /// <returns>Total skill check result, or -1 if untrained and trained only</returns>
    public int RollSkillCheck(int abilityModifier)
    {
        if (TrainedOnly && Ranks == 0)
        {
            Debug.Log($"[Skills] Cannot use {SkillName} - requires training (0 ranks)");
            return -1;
        }

        int d20 = Random.Range(1, 21);
        int total = d20 + GetTotalBonus(abilityModifier);

        string classStr = ClassSkillBonus > 0 ? $" + {ClassSkillBonus}(class)" : "";
        Debug.Log($"[Skills] {SkillName} check: d20({d20}) + {Ranks}(ranks) + {abilityModifier}({KeyAbility}){classStr} = {total}");

        return total;
    }

    /// <summary>
    /// Get the maximum ranks allowed for this skill at the given character level.
    /// D&D 3.5: max ranks = level + 3.
    /// </summary>
    /// <param name="characterLevel">Character's current level</param>
    /// <returns>Maximum number of ranks</returns>
    public int GetMaxRanks(int characterLevel)
    {
        return characterLevel + 3;
    }

    /// <summary>
    /// Whether this skill can be used (i.e., not trained-only with 0 ranks).
    /// </summary>
    public bool CanUse => !TrainedOnly || Ranks > 0;

    /// <summary>
    /// Get a display string for this skill.
    /// Example: "Hide (DEX)*: Ranks 4, Total +10 (4 ranks + 3 DEX + 3 class)"
    /// </summary>
    /// <param name="abilityModifier">The ability modifier for the key ability</param>
    public string GetDisplayString(int abilityModifier)
    {
        string classMark = IsClassSkill ? "*" : "";
        int totalBonus = GetTotalBonus(abilityModifier);
        string totalStr = totalBonus >= 0 ? $"+{totalBonus}" : $"{totalBonus}";

        string breakdown = $"{Ranks} ranks + {abilityModifier} {KeyAbility}";
        if (ClassSkillBonus > 0)
            breakdown += $" + {ClassSkillBonus} class";

        string trainedTag = (TrainedOnly && Ranks == 0) ? " [untrained]" : "";

        return $"{SkillName} ({KeyAbility}){classMark}: Ranks {Ranks}, Total {totalStr} ({breakdown}){trainedTag}";
    }
}

/// <summary>
/// Ability types used as key abilities for skills.
/// </summary>
public enum AbilityType
{
    STR,
    DEX,
    CON,
    WIS,
    INT,
    CHA
}
