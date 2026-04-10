using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// D&D 3.5 Feat System - Core Data Structures
// ============================================================================

/// <summary>
/// Categories of feats in D&D 3.5.
/// </summary>
public enum FeatType
{
    General,
    Combat,
    Skill,
    Defensive,
    Ranged,
    TwoWeaponFighting,
    MountedCombat,
    Unarmed,
    Metamagic
}

/// <summary>
/// Types of prerequisites a feat can require.
/// </summary>
public enum PrerequisiteType
{
    AbilityScore,   // e.g., STR 13+
    BAB,            // e.g., BAB +4
    Feat,           // e.g., requires Power Attack
    ClassLevel,     // e.g., Fighter 4+
    SkillRanks,     // e.g., 5 ranks in Ride
    Proficiency     // e.g., proficient with martial weapons
}

/// <summary>
/// A single prerequisite that must be met to take a feat.
/// </summary>
[System.Serializable]
public class FeatPrerequisite
{
    public PrerequisiteType Type;
    public string ParamString;  // Ability name, feat name, class name, or skill name
    public int ParamValue;      // Minimum score, BAB, level, or ranks required

    public FeatPrerequisite(PrerequisiteType type, string param, int value)
    {
        Type = type;
        ParamString = param;
        ParamValue = value;
    }

    /// <summary>
    /// Check if a character meets this prerequisite.
    /// </summary>
    public bool IsMet(CharacterStats stats)
    {
        switch (Type)
        {
            case PrerequisiteType.AbilityScore:
                return GetAbilityScore(stats, ParamString) >= ParamValue;

            case PrerequisiteType.BAB:
                return stats.BaseAttackBonus >= ParamValue;

            case PrerequisiteType.Feat:
                return stats.HasFeat(ParamString);

            case PrerequisiteType.ClassLevel:
                return stats.CharacterClass == ParamString && stats.Level >= ParamValue;

            case PrerequisiteType.SkillRanks:
                if (stats.Skills != null && stats.Skills.ContainsKey(ParamString))
                    return stats.Skills[ParamString].Ranks >= ParamValue;
                return false;

            case PrerequisiteType.Proficiency:
                // Simplified: fighters are proficient with all martial weapons
                if (ParamString == "Martial Weapons")
                    return stats.CharacterClass == "Fighter";
                return true;

            default:
                return true;
        }
    }

    /// <summary>Get the display string for this prerequisite.</summary>
    public string GetDisplayString()
    {
        switch (Type)
        {
            case PrerequisiteType.AbilityScore:
                return $"{ParamString} {ParamValue}+";
            case PrerequisiteType.BAB:
                return $"BAB +{ParamValue}";
            case PrerequisiteType.Feat:
                return ParamString;
            case PrerequisiteType.ClassLevel:
                return $"{ParamString} level {ParamValue}+";
            case PrerequisiteType.SkillRanks:
                return $"{ParamValue} ranks in {ParamString}";
            case PrerequisiteType.Proficiency:
                return $"Proficient with {ParamString}";
            default:
                return "";
        }
    }

    private static int GetAbilityScore(CharacterStats stats, string ability)
    {
        switch (ability.ToUpper())
        {
            case "STR": return stats.STR;
            case "DEX": return stats.DEX;
            case "CON": return stats.CON;
            case "WIS": return stats.WIS;
            case "INT": return stats.INT;
            case "CHA": return stats.CHA;
            default: return 0;
        }
    }
}

/// <summary>
/// Defines a feat's benefit - what it does mechanically.
/// </summary>
[System.Serializable]
public class FeatBenefit
{
    // Passive bonuses
    public int AttackBonus;         // Flat bonus to attack rolls
    public int DamageBonus;         // Flat bonus to damage
    public int ACBonus;             // Flat bonus to AC
    public int InitiativeBonus;     // Bonus to initiative
    public int FortitudeSaveBonus;  // Bonus to Fort saves
    public int ReflexSaveBonus;     // Bonus to Reflex saves
    public int WillSaveBonus;       // Bonus to Will saves
    public int HPBonus;             // Bonus to max HP (Toughness)
    public int SpeedMultiplier;     // Run speed multiplier (Run feat: 5 instead of 4)

    // Skill bonuses: skill name → bonus
    public Dictionary<string, int> SkillBonuses = new Dictionary<string, int>();

    // Special flags
    public bool GrantsExtraAoO;         // Combat Reflexes
    public bool ReducesTWFPenalty;       // Two-Weapon Fighting
    public bool GrantsExtraOffHandAttack; // Improved TWF
    public bool GrantsSecondOffHandAttack; // Greater TWF
    public bool GrantsTWFACBonus;        // Two-Weapon Defense
    public bool DoublesWeaponThreatRange; // Improved Critical
    public bool AllowsUnarmedWithoutAoO; // Improved Unarmed Strike
    public bool GrantsDeflectArrows;     // Deflect Arrows
    public bool GrantsSnatchArrows;      // Snatch Arrows
    public bool GrantsBlindFight;        // Blind-Fight reroll
    public bool GrantsTrack;             // Track feat
    public bool GrantsEndurance;         // Endurance
    public bool GrantsDiehard;           // Diehard
    public bool GrantsQuickDraw;         // Quick Draw
    public bool GrantsCleave;            // Cleave
    public bool GrantsGreatCleave;       // Great Cleave
    public bool NoPenaltyShootingIntoMelee; // Precise Shot
    public bool IgnoreCoverConcealment;  // Improved Precise Shot
    public bool IncreasesRangeIncrement; // Far Shot
    public bool AllowsShotOnTheRun;      // Shot on the Run
    public bool AllowsManyshot;          // Manyshot
    public bool AllowsSpringAttack;      // Spring Attack
    public bool AllowsWhirlwindAttack;   // Whirlwind Attack
    public bool AllowsCombatExpertise;   // Combat Expertise
    public bool AllowsImprovedFeint;     // Improved Feint
    public bool AllowsImprovedBullRush;  // Improved Bull Rush
    public bool AllowsImprovedDisarm;    // Improved Disarm
    public bool AllowsImprovedGrapple;   // Improved Grapple
    public bool AllowsImprovedOverrun;   // Improved Overrun
    public bool AllowsImprovedSunder;    // Improved Sunder
    public bool AllowsImprovedTrip;      // Improved Trip
    public bool AllowsMountedCombat;     // Mounted Combat
    public bool AllowsRideByAttack;      // Ride-By Attack
    public bool AllowsSpiritedCharge;    // Spirited Charge
    public bool AllowsTrample;           // Trample
    public bool GrantsWeaponFinesse;     // Weapon Finesse
    public bool GrantsCombatCasting;     // Combat Casting
    public bool GrantsAugmentSummoning;  // Augment Summoning

    // Metamagic feats
    public bool IsMetamagic;            // True for all metamagic feats
    public MetamagicFeatId MetamagicId; // Which metamagic feat this is

    // Parameterized feats
    public bool RequiresWeaponChoice;   // Weapon Focus, Specialization, Improved Critical
    public bool RequiresSkillChoice;    // Skill Focus

    /// <summary>Human-readable benefit description.</summary>
    public string Description = "";
}

/// <summary>
/// Complete definition of a D&D 3.5 feat.
/// </summary>
[System.Serializable]
public class FeatDefinition
{
    public string FeatName;
    public string Description;
    public FeatType Type;
    public List<FeatPrerequisite> Prerequisites = new List<FeatPrerequisite>();
    public FeatBenefit Benefit = new FeatBenefit();
    public bool IsActive;           // Requires player activation (Power Attack, Combat Expertise)
    public bool IsFighterBonus;     // Can be taken as a fighter bonus feat
    public bool CanTakeMultiple;    // Can be taken more than once (Toughness, Weapon Focus)
    public bool RequiresChoice;     // Requires choosing a weapon/skill

    public FeatDefinition(string name, string description, FeatType type)
    {
        FeatName = name;
        Description = description;
        Type = type;
    }

    /// <summary>Check if a character meets all prerequisites for this feat.</summary>
    public bool MeetsPrerequisites(CharacterStats stats)
    {
        foreach (var prereq in Prerequisites)
        {
            if (!prereq.IsMet(stats))
                return false;
        }
        return true;
    }

    /// <summary>Get a formatted prerequisites string.</summary>
    public string GetPrerequisitesString()
    {
        if (Prerequisites.Count == 0) return "None";
        var parts = new List<string>();
        foreach (var p in Prerequisites)
            parts.Add(p.GetDisplayString());
        return string.Join(", ", parts);
    }

    /// <summary>Get unmet prerequisites for display.</summary>
    public List<string> GetUnmetPrerequisites(CharacterStats stats)
    {
        var unmet = new List<string>();
        foreach (var prereq in Prerequisites)
        {
            if (!prereq.IsMet(stats))
                unmet.Add(prereq.GetDisplayString());
        }
        return unmet;
    }
}
