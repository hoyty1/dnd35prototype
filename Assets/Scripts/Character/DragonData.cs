using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// DragonData.cs — Complete D&D 3.5e Dragon System
// 10 dragon types × 6 age categories = 60 variants
// Data-driven template approach: static tables + factory method
// =====================================================================

/// <summary>
/// Dragon type enumeration covering all chromatic and metallic dragons.
/// </summary>
public enum DragonType
{
    // Chromatic (evil)
    Red,
    Blue,
    Green,
    Black,
    White,
    // Metallic (good)
    Gold,
    Silver,
    Bronze,
    Copper,
    Brass
}

/// <summary>
/// Age categories implemented (Wyrmling through Adult).
/// Values match D&D 3.5e age category numbering.
/// </summary>
public enum DragonAgeCategory
{
    Wyrmling = 1,
    VeryYoung = 2,
    Young = 3,
    Juvenile = 4,
    YoungAdult = 5,
    Adult = 6
}

/// <summary>
/// Size class determines size progression by age.
/// Lesser: White, Black, Brass, Copper
/// Ordinary: Blue, Green, Bronze
/// Greater: Red, Gold, Silver
/// </summary>
public enum DragonSizeClass { Lesser, Ordinary, Greater }

/// <summary>
/// Type of secondary (non-damaging) breath weapon for metallic dragons.
/// </summary>
public enum SecondaryBreathType
{
    None,
    WeakeningGas,   // Gold: 1d6 Str damage, Fort negates
    ParalysisGas,   // Silver: paralyzed 1d6+3 rounds, Fort negates
    RepulsionGas,   // Bronze: knocked back, Fort negates
    SlowGas,        // Copper: slowed 1d6+3 rounds, Fort negates
    SleepGas         // Brass: sleep 1d6+3 rounds, Will negates
}

/// <summary>
/// Holds all per-age-category stats for a single dragon variant.
/// </summary>
[System.Serializable]
public class DragonAgeStats
{
    public int HitDice;
    public int BaseHP;           // Pre-rolled average HP
    public SizeCategory Size;
    public int NaturalArmor;
    public int STR, DEX, CON, INT, WIS, CHA;
    public int ChallengeRating;
    public int BAB;
    public int BaseSpeed;        // In grid squares (1 sq = 5 ft)

    // Breath weapon
    public int BreathDamageDice;   // Die size (d6, d8, d10)
    public int BreathDamageCount;  // Number of dice
    public int BreathRangeFeet;    // Range in feet
    public int BreathSaveDC;

    // Secondary breath (metallic only, 0 = not available at this age)
    public int SecondaryBreathSaveDC;
    public int SecondaryBreathUsesPerDay;

    // Damage reduction (Young Adult+)
    public int DamageReduction;
    public DamageBypassTag DRBypass;

    // Spell resistance (Young Adult+)
    public int SpellResistance;

    // Frightful presence (Young Adult+)
    public int FrightfulPresenceDC;
    public int FrightfulPresenceRangeFeet;

    // Sorcerer caster level (0 = no casting)
    public int SorcererCasterLevel;

    // Natural attack damage dice by attack type
    public int BiteDamageDice;     // e.g., 6 for 1d6, 8 for 1d8
    public int BiteDamageCount;    // Usually 1, could be 2 for 2d6
    public int ClawDamageDice;
    public int ClawDamageCount;
    public int WingDamageDice;
    public int WingDamageCount;
    public int TailDamageDice;
    public int TailDamageCount;

    // Feats
    public List<string> Feats;
}

/// <summary>
/// Complete definition of a dragon type (e.g., Red Dragon) across all age categories.
/// </summary>
[System.Serializable]
public class DragonTypeTemplate
{
    public DragonType Type;
    public string TypeName;               // "Red", "Blue", etc.
    public DragonSizeClass SizeClass;
    public bool IsMetallic;

    // Breath weapon characteristics (constant across ages)
    public BreathWeaponShape BreathShape;
    public DamageType BreathDamageType;

    // Secondary breath (metallic only)
    public SecondaryBreathType SecondaryBreath;

    // Element immunity
    public DamageType ElementImmunity;

    // Sorcerer spells known (by caster level tier)
    public List<string> SorcererSpellIds;

    // Spell-like abilities (by age)
    public List<string> SpellLikeAbilityIds;

    // Visual colors
    public Color SpriteColor;
    public Color PanelColor;
    public Color NameColor;

    // Per-age stats
    public Dictionary<DragonAgeCategory, DragonAgeStats> AgeStats;
}

/// <summary>
/// Secondary breath weapon definition for metallic dragons.
/// Stored alongside primary BreathWeaponDefinition.
/// </summary>
[System.Serializable]
public class SecondaryBreathWeaponDefinition
{
    public SecondaryBreathType EffectType;
    public BreathWeaponShape Shape;
    public int RangeFeet;
    public int SaveDC;
    public bool IsWillSave;            // true = Will save, false = Fort save
    public int UsesPerDay = 3;
    public int UsesRemaining;
    public int DurationDice = 6;       // Die size for duration (d6 for most, d4 for Gold weakening gas)
    public int DurationBonus = 3;      // Bonus added to 1d(DurationDice) roll; for most = age category (1-6)
                                       // For Gold weakening gas: repurposed as number of dice (2 for 2d4)
    public int AbilityDamageAmount = 6; // Gold weakening gas: flat STR damage (scaled by age: 1/2/3/4/6/12)

    public SecondaryBreathWeaponDefinition Clone()
    {
        return (SecondaryBreathWeaponDefinition)MemberwiseClone();
    }
}

/// <summary>
/// Frightful Presence definition for Young Adult+ dragons.
/// </summary>
[System.Serializable]
public class FrightfulPresenceDefinition
{
    public int SaveDC;                  // Will save DC
    public int RangeFeet;               // Radius in feet
    public int HDThresholdForPanic = 4; // <= this HD: Panicked; > this HD: Shaken
    public int DurationDice = 4;        // 4d6 rounds
    public int DurationDieSides = 6;

    public FrightfulPresenceDefinition Clone()
    {
        return (FrightfulPresenceDefinition)MemberwiseClone();
    }
}

/// <summary>
/// Master dragon data registry. Contains all stat tables for 10 types × 6 ages.
/// </summary>
public static class DragonData
{
    private static Dictionary<DragonType, DragonTypeTemplate> _templates;

    public static DragonTypeTemplate GetTemplate(DragonType type)
    {
        EnsureInitialized();
        return _templates.TryGetValue(type, out var t) ? t : null;
    }

    public static IEnumerable<DragonType> AllTypes()
    {
        yield return DragonType.Red;
        yield return DragonType.Blue;
        yield return DragonType.Green;
        yield return DragonType.Black;
        yield return DragonType.White;
        yield return DragonType.Gold;
        yield return DragonType.Silver;
        yield return DragonType.Bronze;
        yield return DragonType.Copper;
        yield return DragonType.Brass;
    }

    public static IEnumerable<DragonAgeCategory> AllAges()
    {
        yield return DragonAgeCategory.Wyrmling;
        yield return DragonAgeCategory.VeryYoung;
        yield return DragonAgeCategory.Young;
        yield return DragonAgeCategory.Juvenile;
        yield return DragonAgeCategory.YoungAdult;
        yield return DragonAgeCategory.Adult;
    }

    /// <summary>
    /// Get display name for an age category (e.g., "Very Young", "Young Adult").
    /// </summary>
    public static string GetAgeName(DragonAgeCategory age)
    {
        switch (age)
        {
            case DragonAgeCategory.Wyrmling: return "Wyrmling";
            case DragonAgeCategory.VeryYoung: return "Very Young";
            case DragonAgeCategory.Young: return "Young";
            case DragonAgeCategory.Juvenile: return "Juvenile";
            case DragonAgeCategory.YoungAdult: return "Young Adult";
            case DragonAgeCategory.Adult: return "Adult";
            default: return age.ToString();
        }
    }

    /// <summary>
    /// Build the NPC ID string for a dragon variant (e.g., "dragon_red_young").
    /// </summary>
    public static string GetNPCId(DragonType type, DragonAgeCategory age)
    {
        string typeName = type.ToString().ToLower();
        string ageName;
        switch (age)
        {
            case DragonAgeCategory.Wyrmling: ageName = "wyrmling"; break;
            case DragonAgeCategory.VeryYoung: ageName = "very_young"; break;
            case DragonAgeCategory.Young: ageName = "young"; break;
            case DragonAgeCategory.Juvenile: ageName = "juvenile"; break;
            case DragonAgeCategory.YoungAdult: ageName = "young_adult"; break;
            case DragonAgeCategory.Adult: ageName = "adult"; break;
            default: ageName = age.ToString().ToLower(); break;
        }
        return $"dragon_{typeName}_{ageName}";
    }

    /// <summary>
    /// Build display name for a dragon variant (e.g., "Young Red Dragon").
    /// </summary>
    public static string GetDisplayName(DragonType type, DragonAgeCategory age)
    {
        string ageName = GetAgeName(age);
        return $"{ageName} {type} Dragon";
    }

    // ================================================================
    // Size lookup by dragon size class and age
    // ================================================================
    // D&D 3.5e MM p.67: Size progression by dragon "class"
    // Lesser (White/Black/Brass/Copper): Tiny→Small→Small→Medium→Medium→Large
    // Ordinary (Blue/Green/Bronze): Small→Medium→Medium→Large→Large→Huge
    // Greater (Red/Gold/Silver): Small→Medium→Large→Large→Huge→Huge

    public static SizeCategory GetSize(DragonSizeClass sizeClass, DragonAgeCategory age)
    {
        int a = (int)age; // 1-6
        switch (sizeClass)
        {
            case DragonSizeClass.Lesser:
                if (a <= 1) return SizeCategory.Tiny;
                if (a <= 3) return SizeCategory.Small;
                if (a <= 5) return SizeCategory.Medium;
                return SizeCategory.Large;
            case DragonSizeClass.Ordinary:
                if (a <= 1) return SizeCategory.Small;
                if (a <= 3) return SizeCategory.Medium;
                if (a <= 5) return SizeCategory.Large;
                return SizeCategory.Huge;
            case DragonSizeClass.Greater:
                if (a <= 1) return SizeCategory.Small;
                if (a <= 2) return SizeCategory.Medium;
                if (a <= 4) return SizeCategory.Large;
                return SizeCategory.Huge;
            default:
                return SizeCategory.Large;
        }
    }

    // ================================================================
    // Master initialization
    // ================================================================

    private static void EnsureInitialized()
    {
        if (_templates != null) return;
        _templates = new Dictionary<DragonType, DragonTypeTemplate>();

        RegisterRedDragon();
        RegisterBlueDragon();
        RegisterGreenDragon();
        RegisterBlackDragon();
        RegisterWhiteDragon();
        RegisterGoldDragon();
        RegisterSilverDragon();
        RegisterBronzeDragon();
        RegisterCopperDragon();
        RegisterBrassDragon();
    }

    // ================================================================
    // CHROMATIC DRAGONS
    // ================================================================

    private static void RegisterRedDragon()
    {
        var t = new DragonTypeTemplate
        {
            Type = DragonType.Red,
            TypeName = "Red",
            SizeClass = DragonSizeClass.Greater,
            IsMetallic = false,
            BreathShape = BreathWeaponShape.Cone,
            BreathDamageType = DamageType.Fire,
            SecondaryBreath = SecondaryBreathType.None,
            ElementImmunity = DamageType.Fire,
            SorcererSpellIds = new List<string> { "mage_armor", "shield" },
            SpellLikeAbilityIds = new List<string>(),
            SpriteColor = new Color(0.85f, 0.15f, 0.08f, 1f),
            PanelColor = new Color(0.35f, 0.05f, 0.02f, 0.85f),
            NameColor = new Color(1f, 0.45f, 0.3f),
            AgeStats = new Dictionary<DragonAgeCategory, DragonAgeStats>
            {
                { DragonAgeCategory.Wyrmling, new DragonAgeStats {
                    HitDice = 7, BaseHP = 45, NaturalArmor = 5,
                    STR = 17, DEX = 10, CON = 15, INT = 10, WIS = 11, CHA = 10,
                    ChallengeRating = 4, BAB = 7, BaseSpeed = 8,
                    BreathDamageDice = 10, BreathDamageCount = 2, BreathRangeFeet = 20, BreathSaveDC = 15,
                    BiteDamageDice = 6, BiteDamageCount = 1, ClawDamageDice = 4, ClawDamageCount = 1,
                    WingDamageDice = 0, WingDamageCount = 0, TailDamageDice = 0, TailDamageCount = 0,
                    SorcererCasterLevel = 0,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Cleave" }
                }},
                { DragonAgeCategory.VeryYoung, new DragonAgeStats {
                    HitDice = 10, BaseHP = 75, NaturalArmor = 8,
                    STR = 21, DEX = 10, CON = 17, INT = 10, WIS = 11, CHA = 10,
                    ChallengeRating = 5, BAB = 10, BaseSpeed = 8,
                    BreathDamageDice = 10, BreathDamageCount = 4, BreathRangeFeet = 30, BreathSaveDC = 18,
                    BiteDamageDice = 8, BiteDamageCount = 1, ClawDamageDice = 6, ClawDamageCount = 1,
                    WingDamageDice = 4, WingDamageCount = 1, TailDamageDice = 6, TailDamageCount = 1,
                    SorcererCasterLevel = 0,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Cleave" }
                }},
                { DragonAgeCategory.Young, new DragonAgeStats {
                    HitDice = 13, BaseHP = 123, NaturalArmor = 11,
                    STR = 25, DEX = 10, CON = 17, INT = 12, WIS = 13, CHA = 12,
                    ChallengeRating = 7, BAB = 13, BaseSpeed = 8,
                    BreathDamageDice = 10, BreathDamageCount = 6, BreathRangeFeet = 40, BreathSaveDC = 19,
                    BiteDamageDice = 6, BiteDamageCount = 2, ClawDamageDice = 8, ClawDamageCount = 1,
                    WingDamageDice = 6, WingDamageCount = 1, TailDamageDice = 8, TailDamageCount = 1,
                    SorcererCasterLevel = 1,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Cleave", "Multiattack" }
                }},
                { DragonAgeCategory.Juvenile, new DragonAgeStats {
                    HitDice = 16, BaseHP = 152, NaturalArmor = 14,
                    STR = 27, DEX = 10, CON = 19, INT = 14, WIS = 15, CHA = 14,
                    ChallengeRating = 10, BAB = 16, BaseSpeed = 8,
                    BreathDamageDice = 10, BreathDamageCount = 8, BreathRangeFeet = 40, BreathSaveDC = 22,
                    BiteDamageDice = 6, BiteDamageCount = 2, ClawDamageDice = 8, ClawDamageCount = 1,
                    WingDamageDice = 6, WingDamageCount = 1, TailDamageDice = 8, TailDamageCount = 1,
                    SorcererCasterLevel = 3,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Cleave", "Multiattack", "Flyby Attack" }
                }},
                { DragonAgeCategory.YoungAdult, new DragonAgeStats {
                    HitDice = 19, BaseHP = 199, NaturalArmor = 17,
                    STR = 31, DEX = 10, CON = 21, INT = 14, WIS = 15, CHA = 14,
                    ChallengeRating = 13, BAB = 19, BaseSpeed = 8,
                    BreathDamageDice = 10, BreathDamageCount = 10, BreathRangeFeet = 50, BreathSaveDC = 24,
                    DamageReduction = 5, DRBypass = DamageBypassTag.Magic,
                    SpellResistance = 21,
                    FrightfulPresenceDC = 23, FrightfulPresenceRangeFeet = 150,
                    BiteDamageDice = 8, BiteDamageCount = 2, ClawDamageDice = 6, ClawDamageCount = 2,
                    WingDamageDice = 8, WingDamageCount = 1, TailDamageDice = 6, TailDamageCount = 2,
                    SorcererCasterLevel = 5,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Cleave", "Multiattack", "Flyby Attack", "Weapon Focus" }
                }},
                { DragonAgeCategory.Adult, new DragonAgeStats {
                    HitDice = 22, BaseHP = 253, NaturalArmor = 20,
                    STR = 33, DEX = 10, CON = 23, INT = 16, WIS = 17, CHA = 16,
                    ChallengeRating = 15, BAB = 22, BaseSpeed = 8,
                    BreathDamageDice = 10, BreathDamageCount = 12, BreathRangeFeet = 50, BreathSaveDC = 27,
                    DamageReduction = 5, DRBypass = DamageBypassTag.Magic,
                    SpellResistance = 23,
                    FrightfulPresenceDC = 27, FrightfulPresenceRangeFeet = 180,
                    BiteDamageDice = 8, BiteDamageCount = 2, ClawDamageDice = 6, ClawDamageCount = 2,
                    WingDamageDice = 8, WingDamageCount = 1, TailDamageDice = 6, TailDamageCount = 2,
                    SorcererCasterLevel = 7,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Cleave", "Great Cleave", "Multiattack", "Flyby Attack", "Weapon Focus" }
                }}
            }
        };
        _templates[DragonType.Red] = t;
    }

    private static void RegisterBlueDragon()
    {
        var t = new DragonTypeTemplate
        {
            Type = DragonType.Blue,
            TypeName = "Blue",
            SizeClass = DragonSizeClass.Ordinary,
            IsMetallic = false,
            BreathShape = BreathWeaponShape.Line,
            BreathDamageType = DamageType.Electricity,
            SecondaryBreath = SecondaryBreathType.None,
            ElementImmunity = DamageType.Electricity,
            SorcererSpellIds = new List<string> { "mage_armor", "shield" },
            SpellLikeAbilityIds = new List<string>(),
            SpriteColor = new Color(0.2f, 0.35f, 0.85f, 1f),
            PanelColor = new Color(0.05f, 0.1f, 0.35f, 0.85f),
            NameColor = new Color(0.5f, 0.7f, 1f),
            AgeStats = new Dictionary<DragonAgeCategory, DragonAgeStats>
            {
                { DragonAgeCategory.Wyrmling, new DragonAgeStats {
                    HitDice = 7, BaseHP = 45, NaturalArmor = 6,
                    STR = 17, DEX = 10, CON = 15, INT = 10, WIS = 11, CHA = 10,
                    ChallengeRating = 3, BAB = 7, BaseSpeed = 8,
                    BreathDamageDice = 8, BreathDamageCount = 2, BreathRangeFeet = 40, BreathSaveDC = 15,
                    BiteDamageDice = 6, BiteDamageCount = 1, ClawDamageDice = 4, ClawDamageCount = 1,
                    WingDamageDice = 0, WingDamageCount = 0, TailDamageDice = 0, TailDamageCount = 0,
                    SorcererCasterLevel = 0,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Cleave" }
                }},
                { DragonAgeCategory.VeryYoung, new DragonAgeStats {
                    HitDice = 10, BaseHP = 75, NaturalArmor = 9,
                    STR = 21, DEX = 10, CON = 15, INT = 12, WIS = 13, CHA = 12,
                    ChallengeRating = 5, BAB = 10, BaseSpeed = 8,
                    BreathDamageDice = 8, BreathDamageCount = 4, BreathRangeFeet = 60, BreathSaveDC = 17,
                    BiteDamageDice = 8, BiteDamageCount = 1, ClawDamageDice = 6, ClawDamageCount = 1,
                    WingDamageDice = 4, WingDamageCount = 1, TailDamageDice = 6, TailDamageCount = 1,
                    SorcererCasterLevel = 0,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Cleave" }
                }},
                { DragonAgeCategory.Young, new DragonAgeStats {
                    HitDice = 13, BaseHP = 123, NaturalArmor = 12,
                    STR = 25, DEX = 10, CON = 17, INT = 14, WIS = 15, CHA = 14,
                    ChallengeRating = 8, BAB = 13, BaseSpeed = 8,
                    BreathDamageDice = 8, BreathDamageCount = 6, BreathRangeFeet = 60, BreathSaveDC = 19,
                    BiteDamageDice = 8, BiteDamageCount = 1, ClawDamageDice = 6, ClawDamageCount = 1,
                    WingDamageDice = 4, WingDamageCount = 1, TailDamageDice = 6, TailDamageCount = 1,
                    SorcererCasterLevel = 1,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Cleave", "Multiattack" }
                }},
                { DragonAgeCategory.Juvenile, new DragonAgeStats {
                    HitDice = 16, BaseHP = 152, NaturalArmor = 15,
                    STR = 27, DEX = 10, CON = 19, INT = 16, WIS = 17, CHA = 16,
                    ChallengeRating = 11, BAB = 16, BaseSpeed = 8,
                    BreathDamageDice = 8, BreathDamageCount = 8, BreathRangeFeet = 80, BreathSaveDC = 22,
                    BiteDamageDice = 6, BiteDamageCount = 2, ClawDamageDice = 8, ClawDamageCount = 1,
                    WingDamageDice = 6, WingDamageCount = 1, TailDamageDice = 8, TailDamageCount = 1,
                    SorcererCasterLevel = 3,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Cleave", "Multiattack", "Flyby Attack" }
                }},
                { DragonAgeCategory.YoungAdult, new DragonAgeStats {
                    HitDice = 19, BaseHP = 199, NaturalArmor = 18,
                    STR = 29, DEX = 10, CON = 21, INT = 16, WIS = 17, CHA = 16,
                    ChallengeRating = 13, BAB = 19, BaseSpeed = 8,
                    BreathDamageDice = 8, BreathDamageCount = 10, BreathRangeFeet = 80, BreathSaveDC = 24,
                    DamageReduction = 5, DRBypass = DamageBypassTag.Magic,
                    SpellResistance = 21,
                    FrightfulPresenceDC = 23, FrightfulPresenceRangeFeet = 150,
                    BiteDamageDice = 6, BiteDamageCount = 2, ClawDamageDice = 8, ClawDamageCount = 1,
                    WingDamageDice = 6, WingDamageCount = 1, TailDamageDice = 8, TailDamageCount = 1,
                    SorcererCasterLevel = 5,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Cleave", "Multiattack", "Flyby Attack", "Weapon Focus" }
                }},
                { DragonAgeCategory.Adult, new DragonAgeStats {
                    HitDice = 22, BaseHP = 253, NaturalArmor = 21,
                    STR = 31, DEX = 10, CON = 23, INT = 18, WIS = 19, CHA = 18,
                    ChallengeRating = 16, BAB = 22, BaseSpeed = 8,
                    BreathDamageDice = 8, BreathDamageCount = 12, BreathRangeFeet = 100, BreathSaveDC = 27,
                    DamageReduction = 5, DRBypass = DamageBypassTag.Magic,
                    SpellResistance = 23,
                    FrightfulPresenceDC = 27, FrightfulPresenceRangeFeet = 180,
                    BiteDamageDice = 6, BiteDamageCount = 2, ClawDamageDice = 8, ClawDamageCount = 1,
                    WingDamageDice = 6, WingDamageCount = 1, TailDamageDice = 8, TailDamageCount = 1,
                    SorcererCasterLevel = 7,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Cleave", "Great Cleave", "Multiattack", "Flyby Attack", "Weapon Focus" }
                }}
            }
        };
        _templates[DragonType.Blue] = t;
    }

    private static void RegisterGreenDragon()
    {
        var t = new DragonTypeTemplate
        {
            Type = DragonType.Green,
            TypeName = "Green",
            SizeClass = DragonSizeClass.Ordinary,
            IsMetallic = false,
            BreathShape = BreathWeaponShape.Cone,
            BreathDamageType = DamageType.Acid,
            SecondaryBreath = SecondaryBreathType.None,
            ElementImmunity = DamageType.Acid,
            SorcererSpellIds = new List<string>(),
            SpellLikeAbilityIds = new List<string>(),
            SpriteColor = new Color(0.15f, 0.65f, 0.2f, 1f),
            PanelColor = new Color(0.04f, 0.25f, 0.06f, 0.85f),
            NameColor = new Color(0.5f, 1f, 0.55f),
            AgeStats = new Dictionary<DragonAgeCategory, DragonAgeStats>
            {
                { DragonAgeCategory.Wyrmling, new DragonAgeStats {
                    HitDice = 5, BaseHP = 32, NaturalArmor = 5,
                    STR = 13, DEX = 10, CON = 13, INT = 10, WIS = 11, CHA = 10,
                    ChallengeRating = 3, BAB = 5, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 2, BreathRangeFeet = 20, BreathSaveDC = 13,
                    BiteDamageDice = 6, BiteDamageCount = 1, ClawDamageDice = 4, ClawDamageCount = 1,
                    WingDamageDice = 0, WingDamageCount = 0, TailDamageDice = 0, TailDamageCount = 0,
                    Feats = new List<string> { "Power Attack", "Cleave" }
                }},
                { DragonAgeCategory.VeryYoung, new DragonAgeStats {
                    HitDice = 8, BaseHP = 60, NaturalArmor = 8,
                    STR = 17, DEX = 10, CON = 15, INT = 10, WIS = 11, CHA = 10,
                    ChallengeRating = 5, BAB = 8, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 4, BreathRangeFeet = 30, BreathSaveDC = 16,
                    BiteDamageDice = 8, BiteDamageCount = 1, ClawDamageDice = 6, ClawDamageCount = 1,
                    WingDamageDice = 4, WingDamageCount = 1, TailDamageDice = 6, TailDamageCount = 1,
                    Feats = new List<string> { "Power Attack", "Cleave", "Multiattack" }
                }},
                { DragonAgeCategory.Young, new DragonAgeStats {
                    HitDice = 11, BaseHP = 93, NaturalArmor = 11,
                    STR = 21, DEX = 10, CON = 17, INT = 12, WIS = 13, CHA = 12,
                    ChallengeRating = 8, BAB = 11, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 6, BreathRangeFeet = 40, BreathSaveDC = 18,
                    BiteDamageDice = 8, BiteDamageCount = 1, ClawDamageDice = 6, ClawDamageCount = 1,
                    WingDamageDice = 4, WingDamageCount = 1, TailDamageDice = 6, TailDamageCount = 1,
                    Feats = new List<string> { "Power Attack", "Cleave", "Multiattack", "Alertness" }
                }},
                { DragonAgeCategory.Juvenile, new DragonAgeStats {
                    HitDice = 14, BaseHP = 126, NaturalArmor = 14,
                    STR = 25, DEX = 10, CON = 19, INT = 14, WIS = 15, CHA = 14,
                    ChallengeRating = 10, BAB = 14, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 8, BreathRangeFeet = 40, BreathSaveDC = 21,
                    BiteDamageDice = 6, BiteDamageCount = 2, ClawDamageDice = 8, ClawDamageCount = 1,
                    WingDamageDice = 6, WingDamageCount = 1, TailDamageDice = 8, TailDamageCount = 1,
                    Feats = new List<string> { "Power Attack", "Cleave", "Multiattack", "Alertness", "Flyby Attack" }
                }},
                { DragonAgeCategory.YoungAdult, new DragonAgeStats {
                    HitDice = 17, BaseHP = 161, NaturalArmor = 17,
                    STR = 27, DEX = 10, CON = 19, INT = 16, WIS = 17, CHA = 16,
                    ChallengeRating = 12, BAB = 17, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 10, BreathRangeFeet = 40, BreathSaveDC = 22,
                    DamageReduction = 5, DRBypass = DamageBypassTag.Magic,
                    SpellResistance = 20,
                    FrightfulPresenceDC = 23, FrightfulPresenceRangeFeet = 150,
                    BiteDamageDice = 6, BiteDamageCount = 2, ClawDamageDice = 8, ClawDamageCount = 1,
                    WingDamageDice = 6, WingDamageCount = 1, TailDamageDice = 8, TailDamageCount = 1,
                    Feats = new List<string> { "Power Attack", "Cleave", "Multiattack", "Alertness", "Flyby Attack", "Weapon Focus" }
                }},
                { DragonAgeCategory.Adult, new DragonAgeStats {
                    HitDice = 20, BaseHP = 200, NaturalArmor = 20,
                    STR = 29, DEX = 10, CON = 21, INT = 18, WIS = 19, CHA = 18,
                    ChallengeRating = 15, BAB = 20, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 12, BreathRangeFeet = 50, BreathSaveDC = 25,
                    DamageReduction = 5, DRBypass = DamageBypassTag.Magic,
                    SpellResistance = 22,
                    FrightfulPresenceDC = 27, FrightfulPresenceRangeFeet = 180,
                    BiteDamageDice = 8, BiteDamageCount = 2, ClawDamageDice = 6, ClawDamageCount = 2,
                    WingDamageDice = 8, WingDamageCount = 1, TailDamageDice = 6, TailDamageCount = 2,
                    Feats = new List<string> { "Power Attack", "Cleave", "Great Cleave", "Multiattack", "Alertness", "Flyby Attack", "Weapon Focus" }
                }}
            }
        };
        _templates[DragonType.Green] = t;
    }

    private static void RegisterBlackDragon()
    {
        var t = new DragonTypeTemplate
        {
            Type = DragonType.Black,
            TypeName = "Black",
            SizeClass = DragonSizeClass.Lesser,
            IsMetallic = false,
            BreathShape = BreathWeaponShape.Line,
            BreathDamageType = DamageType.Acid,
            SecondaryBreath = SecondaryBreathType.None,
            ElementImmunity = DamageType.Acid,
            SorcererSpellIds = new List<string>(),
            SpellLikeAbilityIds = new List<string>(),
            SpriteColor = new Color(0.15f, 0.12f, 0.18f, 1f),
            PanelColor = new Color(0.08f, 0.06f, 0.1f, 0.85f),
            NameColor = new Color(0.6f, 0.55f, 0.7f),
            AgeStats = new Dictionary<DragonAgeCategory, DragonAgeStats>
            {
                { DragonAgeCategory.Wyrmling, new DragonAgeStats {
                    HitDice = 4, BaseHP = 22, NaturalArmor = 3,
                    STR = 11, DEX = 10, CON = 13, INT = 6, WIS = 11, CHA = 6,
                    ChallengeRating = 2, BAB = 4, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 2, BreathRangeFeet = 30, BreathSaveDC = 13,
                    BiteDamageDice = 4, BiteDamageCount = 1, ClawDamageDice = 3, ClawDamageCount = 1,
                    WingDamageDice = 0, WingDamageCount = 0, TailDamageDice = 0, TailDamageCount = 0,
                    Feats = new List<string> { "Improved Initiative" }
                }},
                { DragonAgeCategory.VeryYoung, new DragonAgeStats {
                    HitDice = 7, BaseHP = 45, NaturalArmor = 6,
                    STR = 15, DEX = 10, CON = 15, INT = 8, WIS = 11, CHA = 8,
                    ChallengeRating = 4, BAB = 7, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 4, BreathRangeFeet = 40, BreathSaveDC = 15,
                    BiteDamageDice = 6, BiteDamageCount = 1, ClawDamageDice = 4, ClawDamageCount = 1,
                    WingDamageDice = 3, WingDamageCount = 1, TailDamageDice = 4, TailDamageCount = 1,
                    Feats = new List<string> { "Improved Initiative", "Power Attack" }
                }},
                { DragonAgeCategory.Young, new DragonAgeStats {
                    HitDice = 10, BaseHP = 75, NaturalArmor = 9,
                    STR = 19, DEX = 10, CON = 17, INT = 10, WIS = 11, CHA = 10,
                    ChallengeRating = 6, BAB = 10, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 6, BreathRangeFeet = 40, BreathSaveDC = 18,
                    BiteDamageDice = 6, BiteDamageCount = 1, ClawDamageDice = 4, ClawDamageCount = 1,
                    WingDamageDice = 3, WingDamageCount = 1, TailDamageDice = 4, TailDamageCount = 1,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Multiattack" }
                }},
                { DragonAgeCategory.Juvenile, new DragonAgeStats {
                    HitDice = 13, BaseHP = 110, NaturalArmor = 12,
                    STR = 23, DEX = 10, CON = 17, INT = 12, WIS = 13, CHA = 12,
                    ChallengeRating = 8, BAB = 13, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 8, BreathRangeFeet = 60, BreathSaveDC = 19,
                    BiteDamageDice = 8, BiteDamageCount = 1, ClawDamageDice = 6, ClawDamageCount = 1,
                    WingDamageDice = 4, WingDamageCount = 1, TailDamageDice = 6, TailDamageCount = 1,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Multiattack", "Alertness" }
                }},
                { DragonAgeCategory.YoungAdult, new DragonAgeStats {
                    HitDice = 16, BaseHP = 144, NaturalArmor = 15,
                    STR = 25, DEX = 10, CON = 19, INT = 12, WIS = 13, CHA = 12,
                    ChallengeRating = 10, BAB = 16, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 10, BreathRangeFeet = 60, BreathSaveDC = 22,
                    DamageReduction = 5, DRBypass = DamageBypassTag.Magic,
                    SpellResistance = 19,
                    FrightfulPresenceDC = 19, FrightfulPresenceRangeFeet = 150,
                    BiteDamageDice = 8, BiteDamageCount = 1, ClawDamageDice = 6, ClawDamageCount = 1,
                    WingDamageDice = 4, WingDamageCount = 1, TailDamageDice = 6, TailDamageCount = 1,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Multiattack", "Alertness", "Flyby Attack" }
                }},
                { DragonAgeCategory.Adult, new DragonAgeStats {
                    HitDice = 19, BaseHP = 180, NaturalArmor = 18,
                    STR = 27, DEX = 10, CON = 21, INT = 14, WIS = 15, CHA = 14,
                    ChallengeRating = 12, BAB = 19, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 12, BreathRangeFeet = 80, BreathSaveDC = 24,
                    DamageReduction = 5, DRBypass = DamageBypassTag.Magic,
                    SpellResistance = 21,
                    FrightfulPresenceDC = 23, FrightfulPresenceRangeFeet = 180,
                    BiteDamageDice = 6, BiteDamageCount = 2, ClawDamageDice = 8, ClawDamageCount = 1,
                    WingDamageDice = 6, WingDamageCount = 1, TailDamageDice = 8, TailDamageCount = 1,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Multiattack", "Alertness", "Flyby Attack", "Cleave" }
                }}
            }
        };
        _templates[DragonType.Black] = t;
    }

    private static void RegisterWhiteDragon()
    {
        var t = new DragonTypeTemplate
        {
            Type = DragonType.White,
            TypeName = "White",
            SizeClass = DragonSizeClass.Lesser,
            IsMetallic = false,
            BreathShape = BreathWeaponShape.Cone,
            BreathDamageType = DamageType.Cold,
            SecondaryBreath = SecondaryBreathType.None,
            ElementImmunity = DamageType.Cold,
            SorcererSpellIds = new List<string>(),
            SpellLikeAbilityIds = new List<string>(),
            SpriteColor = new Color(0.92f, 0.95f, 0.98f, 1f),
            PanelColor = new Color(0.3f, 0.33f, 0.38f, 0.85f),
            NameColor = new Color(0.85f, 0.9f, 1f),
            AgeStats = new Dictionary<DragonAgeCategory, DragonAgeStats>
            {
                { DragonAgeCategory.Wyrmling, new DragonAgeStats {
                    HitDice = 3, BaseHP = 16, NaturalArmor = 2,
                    STR = 11, DEX = 10, CON = 13, INT = 6, WIS = 11, CHA = 6,
                    ChallengeRating = 2, BAB = 3, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 1, BreathRangeFeet = 15, BreathSaveDC = 12,
                    BiteDamageDice = 4, BiteDamageCount = 1, ClawDamageDice = 3, ClawDamageCount = 1,
                    WingDamageDice = 0, WingDamageCount = 0, TailDamageDice = 0, TailDamageCount = 0,
                    Feats = new List<string> { "Improved Initiative" }
                }},
                { DragonAgeCategory.VeryYoung, new DragonAgeStats {
                    HitDice = 6, BaseHP = 39, NaturalArmor = 5,
                    STR = 15, DEX = 10, CON = 15, INT = 6, WIS = 11, CHA = 6,
                    ChallengeRating = 3, BAB = 6, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 2, BreathRangeFeet = 20, BreathSaveDC = 15,
                    BiteDamageDice = 6, BiteDamageCount = 1, ClawDamageDice = 4, ClawDamageCount = 1,
                    WingDamageDice = 3, WingDamageCount = 1, TailDamageDice = 4, TailDamageCount = 1,
                    Feats = new List<string> { "Improved Initiative", "Power Attack" }
                }},
                { DragonAgeCategory.Young, new DragonAgeStats {
                    HitDice = 9, BaseHP = 58, NaturalArmor = 8,
                    STR = 17, DEX = 10, CON = 15, INT = 8, WIS = 11, CHA = 8,
                    ChallengeRating = 5, BAB = 9, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 3, BreathRangeFeet = 20, BreathSaveDC = 16,
                    BiteDamageDice = 6, BiteDamageCount = 1, ClawDamageDice = 4, ClawDamageCount = 1,
                    WingDamageDice = 3, WingDamageCount = 1, TailDamageDice = 4, TailDamageCount = 1,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Multiattack" }
                }},
                { DragonAgeCategory.Juvenile, new DragonAgeStats {
                    HitDice = 12, BaseHP = 84, NaturalArmor = 11,
                    STR = 21, DEX = 10, CON = 17, INT = 8, WIS = 11, CHA = 8,
                    ChallengeRating = 7, BAB = 12, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 5, BreathRangeFeet = 30, BreathSaveDC = 19,
                    BiteDamageDice = 8, BiteDamageCount = 1, ClawDamageDice = 6, ClawDamageCount = 1,
                    WingDamageDice = 4, WingDamageCount = 1, TailDamageDice = 6, TailDamageCount = 1,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Multiattack", "Alertness" }
                }},
                { DragonAgeCategory.YoungAdult, new DragonAgeStats {
                    HitDice = 15, BaseHP = 112, NaturalArmor = 14,
                    STR = 23, DEX = 10, CON = 17, INT = 10, WIS = 11, CHA = 10,
                    ChallengeRating = 9, BAB = 15, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 7, BreathRangeFeet = 30, BreathSaveDC = 20,
                    DamageReduction = 5, DRBypass = DamageBypassTag.Magic,
                    SpellResistance = 17,
                    FrightfulPresenceDC = 17, FrightfulPresenceRangeFeet = 150,
                    BiteDamageDice = 8, BiteDamageCount = 1, ClawDamageDice = 6, ClawDamageCount = 1,
                    WingDamageDice = 4, WingDamageCount = 1, TailDamageDice = 6, TailDamageCount = 1,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Multiattack", "Alertness", "Flyby Attack" }
                }},
                { DragonAgeCategory.Adult, new DragonAgeStats {
                    HitDice = 18, BaseHP = 144, NaturalArmor = 17,
                    STR = 25, DEX = 10, CON = 19, INT = 10, WIS = 11, CHA = 10,
                    ChallengeRating = 11, BAB = 18, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 9, BreathRangeFeet = 40, BreathSaveDC = 23,
                    DamageReduction = 5, DRBypass = DamageBypassTag.Magic,
                    SpellResistance = 19,
                    FrightfulPresenceDC = 19, FrightfulPresenceRangeFeet = 180,
                    BiteDamageDice = 6, BiteDamageCount = 2, ClawDamageDice = 8, ClawDamageCount = 1,
                    WingDamageDice = 6, WingDamageCount = 1, TailDamageDice = 8, TailDamageCount = 1,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Multiattack", "Alertness", "Flyby Attack", "Cleave" }
                }}
            }
        };
        _templates[DragonType.White] = t;
    }

    // ================================================================
    // METALLIC DRAGONS
    // ================================================================

    private static void RegisterGoldDragon()
    {
        var t = new DragonTypeTemplate
        {
            Type = DragonType.Gold,
            TypeName = "Gold",
            SizeClass = DragonSizeClass.Greater,
            IsMetallic = true,
            BreathShape = BreathWeaponShape.Cone,
            BreathDamageType = DamageType.Fire,
            SecondaryBreath = SecondaryBreathType.WeakeningGas,
            ElementImmunity = DamageType.Fire,
            SorcererSpellIds = new List<string> { "mage_armor", "shield" },
            SpellLikeAbilityIds = new List<string>(),
            SpriteColor = new Color(0.95f, 0.82f, 0.2f, 1f),
            PanelColor = new Color(0.4f, 0.32f, 0.05f, 0.85f),
            NameColor = new Color(1f, 0.95f, 0.5f),
            AgeStats = new Dictionary<DragonAgeCategory, DragonAgeStats>
            {
                { DragonAgeCategory.Wyrmling, new DragonAgeStats {
                    HitDice = 9, BaseHP = 58, NaturalArmor = 7,
                    STR = 21, DEX = 10, CON = 17, INT = 14, WIS = 15, CHA = 14,
                    ChallengeRating = 5, BAB = 9, BaseSpeed = 8,
                    BreathDamageDice = 10, BreathDamageCount = 2, BreathRangeFeet = 20, BreathSaveDC = 17,
                    SecondaryBreathSaveDC = 17, SecondaryBreathUsesPerDay = 1,
                    BiteDamageDice = 6, BiteDamageCount = 1, ClawDamageDice = 4, ClawDamageCount = 1,
                    WingDamageDice = 0, WingDamageCount = 0, TailDamageDice = 0, TailDamageCount = 0,
                    SorcererCasterLevel = 0,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Cleave" }
                }},
                { DragonAgeCategory.VeryYoung, new DragonAgeStats {
                    HitDice = 12, BaseHP = 93, NaturalArmor = 10,
                    STR = 25, DEX = 10, CON = 19, INT = 14, WIS = 15, CHA = 14,
                    ChallengeRating = 7, BAB = 12, BaseSpeed = 8,
                    BreathDamageDice = 10, BreathDamageCount = 4, BreathRangeFeet = 30, BreathSaveDC = 20,
                    SecondaryBreathSaveDC = 20, SecondaryBreathUsesPerDay = 1,
                    BiteDamageDice = 8, BiteDamageCount = 1, ClawDamageDice = 6, ClawDamageCount = 1,
                    WingDamageDice = 4, WingDamageCount = 1, TailDamageDice = 6, TailDamageCount = 1,
                    SorcererCasterLevel = 1,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Cleave", "Multiattack" }
                }},
                { DragonAgeCategory.Young, new DragonAgeStats {
                    HitDice = 15, BaseHP = 142, NaturalArmor = 13,
                    STR = 27, DEX = 10, CON = 19, INT = 16, WIS = 17, CHA = 16,
                    ChallengeRating = 9, BAB = 15, BaseSpeed = 8,
                    BreathDamageDice = 10, BreathDamageCount = 6, BreathRangeFeet = 40, BreathSaveDC = 21,
                    SecondaryBreathSaveDC = 21, SecondaryBreathUsesPerDay = 2,
                    BiteDamageDice = 6, BiteDamageCount = 2, ClawDamageDice = 8, ClawDamageCount = 1,
                    WingDamageDice = 6, WingDamageCount = 1, TailDamageDice = 8, TailDamageCount = 1,
                    SorcererCasterLevel = 3,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Cleave", "Multiattack", "Flyby Attack" }
                }},
                { DragonAgeCategory.Juvenile, new DragonAgeStats {
                    HitDice = 18, BaseHP = 180, NaturalArmor = 16,
                    STR = 29, DEX = 10, CON = 21, INT = 18, WIS = 19, CHA = 18,
                    ChallengeRating = 12, BAB = 18, BaseSpeed = 8,
                    BreathDamageDice = 10, BreathDamageCount = 8, BreathRangeFeet = 40, BreathSaveDC = 24,
                    SecondaryBreathSaveDC = 24, SecondaryBreathUsesPerDay = 2,
                    BiteDamageDice = 6, BiteDamageCount = 2, ClawDamageDice = 8, ClawDamageCount = 1,
                    WingDamageDice = 6, WingDamageCount = 1, TailDamageDice = 8, TailDamageCount = 1,
                    SorcererCasterLevel = 5,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Cleave", "Multiattack", "Flyby Attack", "Weapon Focus" }
                }},
                { DragonAgeCategory.YoungAdult, new DragonAgeStats {
                    HitDice = 21, BaseHP = 231, NaturalArmor = 19,
                    STR = 33, DEX = 10, CON = 23, INT = 18, WIS = 19, CHA = 18,
                    ChallengeRating = 15, BAB = 21, BaseSpeed = 8,
                    BreathDamageDice = 10, BreathDamageCount = 10, BreathRangeFeet = 50, BreathSaveDC = 26,
                    SecondaryBreathSaveDC = 26, SecondaryBreathUsesPerDay = 3,
                    DamageReduction = 5, DRBypass = DamageBypassTag.Magic,
                    SpellResistance = 23,
                    FrightfulPresenceDC = 26, FrightfulPresenceRangeFeet = 150,
                    BiteDamageDice = 8, BiteDamageCount = 2, ClawDamageDice = 6, ClawDamageCount = 2,
                    WingDamageDice = 8, WingDamageCount = 1, TailDamageDice = 6, TailDamageCount = 2,
                    SorcererCasterLevel = 7,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Cleave", "Great Cleave", "Multiattack", "Flyby Attack", "Weapon Focus" }
                }},
                { DragonAgeCategory.Adult, new DragonAgeStats {
                    HitDice = 24, BaseHP = 288, NaturalArmor = 22,
                    STR = 35, DEX = 10, CON = 25, INT = 20, WIS = 21, CHA = 20,
                    ChallengeRating = 17, BAB = 24, BaseSpeed = 8,
                    BreathDamageDice = 10, BreathDamageCount = 12, BreathRangeFeet = 50, BreathSaveDC = 29,
                    SecondaryBreathSaveDC = 29, SecondaryBreathUsesPerDay = 3,
                    DamageReduction = 5, DRBypass = DamageBypassTag.Magic,
                    SpellResistance = 25,
                    FrightfulPresenceDC = 30, FrightfulPresenceRangeFeet = 180,
                    BiteDamageDice = 8, BiteDamageCount = 2, ClawDamageDice = 6, ClawDamageCount = 2,
                    WingDamageDice = 8, WingDamageCount = 1, TailDamageDice = 6, TailDamageCount = 2,
                    SorcererCasterLevel = 9,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Cleave", "Great Cleave", "Multiattack", "Flyby Attack", "Weapon Focus", "Alertness" }
                }}
            }
        };
        _templates[DragonType.Gold] = t;
    }

    private static void RegisterSilverDragon()
    {
        var t = new DragonTypeTemplate
        {
            Type = DragonType.Silver,
            TypeName = "Silver",
            SizeClass = DragonSizeClass.Greater,
            IsMetallic = true,
            BreathShape = BreathWeaponShape.Cone,
            BreathDamageType = DamageType.Cold,
            SecondaryBreath = SecondaryBreathType.ParalysisGas,
            ElementImmunity = DamageType.Cold,
            SorcererSpellIds = new List<string> { "mage_armor", "shield" },
            SpellLikeAbilityIds = new List<string>(),
            SpriteColor = new Color(0.78f, 0.82f, 0.9f, 1f),
            PanelColor = new Color(0.25f, 0.28f, 0.35f, 0.85f),
            NameColor = new Color(0.9f, 0.92f, 1f),
            AgeStats = new Dictionary<DragonAgeCategory, DragonAgeStats>
            {
                { DragonAgeCategory.Wyrmling, new DragonAgeStats {
                    HitDice = 8, BaseHP = 52, NaturalArmor = 6,
                    STR = 19, DEX = 10, CON = 17, INT = 14, WIS = 15, CHA = 14,
                    ChallengeRating = 4, BAB = 8, BaseSpeed = 8,
                    BreathDamageDice = 8, BreathDamageCount = 2, BreathRangeFeet = 20, BreathSaveDC = 17,
                    SecondaryBreathSaveDC = 17, SecondaryBreathUsesPerDay = 1,
                    BiteDamageDice = 6, BiteDamageCount = 1, ClawDamageDice = 4, ClawDamageCount = 1,
                    WingDamageDice = 0, WingDamageCount = 0, TailDamageDice = 0, TailDamageCount = 0,
                    SorcererCasterLevel = 0,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Cleave" }
                }},
                { DragonAgeCategory.VeryYoung, new DragonAgeStats {
                    HitDice = 11, BaseHP = 82, NaturalArmor = 9,
                    STR = 23, DEX = 10, CON = 17, INT = 14, WIS = 15, CHA = 14,
                    ChallengeRating = 6, BAB = 11, BaseSpeed = 8,
                    BreathDamageDice = 8, BreathDamageCount = 4, BreathRangeFeet = 30, BreathSaveDC = 18,
                    SecondaryBreathSaveDC = 18, SecondaryBreathUsesPerDay = 1,
                    BiteDamageDice = 8, BiteDamageCount = 1, ClawDamageDice = 6, ClawDamageCount = 1,
                    WingDamageDice = 4, WingDamageCount = 1, TailDamageDice = 6, TailDamageCount = 1,
                    SorcererCasterLevel = 0,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Cleave", "Multiattack" }
                }},
                { DragonAgeCategory.Young, new DragonAgeStats {
                    HitDice = 13, BaseHP = 110, NaturalArmor = 12,
                    STR = 27, DEX = 10, CON = 17, INT = 16, WIS = 17, CHA = 16,
                    ChallengeRating = 8, BAB = 13, BaseSpeed = 8,
                    BreathDamageDice = 8, BreathDamageCount = 6, BreathRangeFeet = 40, BreathSaveDC = 19,
                    SecondaryBreathSaveDC = 19, SecondaryBreathUsesPerDay = 2,
                    BiteDamageDice = 6, BiteDamageCount = 2, ClawDamageDice = 8, ClawDamageCount = 1,
                    WingDamageDice = 6, WingDamageCount = 1, TailDamageDice = 8, TailDamageCount = 1,
                    SorcererCasterLevel = 1,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Cleave", "Multiattack" }
                }},
                { DragonAgeCategory.Juvenile, new DragonAgeStats {
                    HitDice = 16, BaseHP = 144, NaturalArmor = 15,
                    STR = 29, DEX = 10, CON = 19, INT = 16, WIS = 17, CHA = 16,
                    ChallengeRating = 11, BAB = 16, BaseSpeed = 8,
                    BreathDamageDice = 8, BreathDamageCount = 8, BreathRangeFeet = 40, BreathSaveDC = 22,
                    SecondaryBreathSaveDC = 22, SecondaryBreathUsesPerDay = 2,
                    BiteDamageDice = 6, BiteDamageCount = 2, ClawDamageDice = 8, ClawDamageCount = 1,
                    WingDamageDice = 6, WingDamageCount = 1, TailDamageDice = 8, TailDamageCount = 1,
                    SorcererCasterLevel = 3,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Cleave", "Multiattack", "Flyby Attack" }
                }},
                { DragonAgeCategory.YoungAdult, new DragonAgeStats {
                    HitDice = 19, BaseHP = 199, NaturalArmor = 18,
                    STR = 31, DEX = 10, CON = 21, INT = 18, WIS = 19, CHA = 18,
                    ChallengeRating = 13, BAB = 19, BaseSpeed = 8,
                    BreathDamageDice = 8, BreathDamageCount = 10, BreathRangeFeet = 50, BreathSaveDC = 24,
                    SecondaryBreathSaveDC = 24, SecondaryBreathUsesPerDay = 3,
                    DamageReduction = 5, DRBypass = DamageBypassTag.Magic,
                    SpellResistance = 21,
                    FrightfulPresenceDC = 26, FrightfulPresenceRangeFeet = 150,
                    BiteDamageDice = 8, BiteDamageCount = 2, ClawDamageDice = 6, ClawDamageCount = 2,
                    WingDamageDice = 8, WingDamageCount = 1, TailDamageDice = 6, TailDamageCount = 2,
                    SorcererCasterLevel = 5,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Cleave", "Multiattack", "Flyby Attack", "Weapon Focus" }
                }},
                { DragonAgeCategory.Adult, new DragonAgeStats {
                    HitDice = 22, BaseHP = 253, NaturalArmor = 21,
                    STR = 33, DEX = 10, CON = 23, INT = 18, WIS = 19, CHA = 18,
                    ChallengeRating = 15, BAB = 22, BaseSpeed = 8,
                    BreathDamageDice = 8, BreathDamageCount = 12, BreathRangeFeet = 50, BreathSaveDC = 27,
                    SecondaryBreathSaveDC = 27, SecondaryBreathUsesPerDay = 3,
                    DamageReduction = 10, DRBypass = DamageBypassTag.Magic,
                    SpellResistance = 23,
                    FrightfulPresenceDC = 29, FrightfulPresenceRangeFeet = 180,
                    BiteDamageDice = 8, BiteDamageCount = 2, ClawDamageDice = 6, ClawDamageCount = 2,
                    WingDamageDice = 8, WingDamageCount = 1, TailDamageDice = 6, TailDamageCount = 2,
                    SorcererCasterLevel = 7,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Cleave", "Great Cleave", "Multiattack", "Flyby Attack", "Weapon Focus" }
                }}
            }
        };
        _templates[DragonType.Silver] = t;
    }

    private static void RegisterBronzeDragon()
    {
        var t = new DragonTypeTemplate
        {
            Type = DragonType.Bronze,
            TypeName = "Bronze",
            SizeClass = DragonSizeClass.Ordinary,
            IsMetallic = true,
            BreathShape = BreathWeaponShape.Line,
            BreathDamageType = DamageType.Electricity,
            SecondaryBreath = SecondaryBreathType.RepulsionGas,
            ElementImmunity = DamageType.Electricity,
            SorcererSpellIds = new List<string>(),
            SpellLikeAbilityIds = new List<string>(),
            SpriteColor = new Color(0.72f, 0.55f, 0.3f, 1f),
            PanelColor = new Color(0.3f, 0.2f, 0.1f, 0.85f),
            NameColor = new Color(0.9f, 0.78f, 0.5f),
            AgeStats = new Dictionary<DragonAgeCategory, DragonAgeStats>
            {
                { DragonAgeCategory.Wyrmling, new DragonAgeStats {
                    HitDice = 6, BaseHP = 39, NaturalArmor = 5,
                    STR = 17, DEX = 10, CON = 15, INT = 14, WIS = 15, CHA = 14,
                    ChallengeRating = 3, BAB = 6, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 2, BreathRangeFeet = 40, BreathSaveDC = 15,
                    SecondaryBreathSaveDC = 15, SecondaryBreathUsesPerDay = 1,
                    BiteDamageDice = 6, BiteDamageCount = 1, ClawDamageDice = 4, ClawDamageCount = 1,
                    WingDamageDice = 0, WingDamageCount = 0, TailDamageDice = 0, TailDamageCount = 0,
                    SorcererCasterLevel = 0,
                    Feats = new List<string> { "Improved Initiative", "Power Attack" }
                }},
                { DragonAgeCategory.VeryYoung, new DragonAgeStats {
                    HitDice = 9, BaseHP = 58, NaturalArmor = 8,
                    STR = 21, DEX = 10, CON = 15, INT = 14, WIS = 15, CHA = 14,
                    ChallengeRating = 5, BAB = 9, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 4, BreathRangeFeet = 60, BreathSaveDC = 16,
                    SecondaryBreathSaveDC = 16, SecondaryBreathUsesPerDay = 1,
                    BiteDamageDice = 8, BiteDamageCount = 1, ClawDamageDice = 6, ClawDamageCount = 1,
                    WingDamageDice = 4, WingDamageCount = 1, TailDamageDice = 6, TailDamageCount = 1,
                    SorcererCasterLevel = 0,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Multiattack" }
                }},
                { DragonAgeCategory.Young, new DragonAgeStats {
                    HitDice = 11, BaseHP = 82, NaturalArmor = 11,
                    STR = 25, DEX = 10, CON = 17, INT = 14, WIS = 15, CHA = 14,
                    ChallengeRating = 7, BAB = 11, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 6, BreathRangeFeet = 60, BreathSaveDC = 18,
                    SecondaryBreathSaveDC = 18, SecondaryBreathUsesPerDay = 2,
                    BiteDamageDice = 8, BiteDamageCount = 1, ClawDamageDice = 6, ClawDamageCount = 1,
                    WingDamageDice = 4, WingDamageCount = 1, TailDamageDice = 6, TailDamageCount = 1,
                    SorcererCasterLevel = 0,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Multiattack", "Alertness" }
                }},
                { DragonAgeCategory.Juvenile, new DragonAgeStats {
                    HitDice = 14, BaseHP = 112, NaturalArmor = 14,
                    STR = 27, DEX = 10, CON = 19, INT = 16, WIS = 17, CHA = 16,
                    ChallengeRating = 10, BAB = 14, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 8, BreathRangeFeet = 80, BreathSaveDC = 21,
                    SecondaryBreathSaveDC = 21, SecondaryBreathUsesPerDay = 2,
                    BiteDamageDice = 6, BiteDamageCount = 2, ClawDamageDice = 8, ClawDamageCount = 1,
                    WingDamageDice = 6, WingDamageCount = 1, TailDamageDice = 8, TailDamageCount = 1,
                    SorcererCasterLevel = 1,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Multiattack", "Alertness", "Flyby Attack" }
                }},
                { DragonAgeCategory.YoungAdult, new DragonAgeStats {
                    HitDice = 17, BaseHP = 144, NaturalArmor = 17,
                    STR = 29, DEX = 10, CON = 19, INT = 16, WIS = 17, CHA = 16,
                    ChallengeRating = 13, BAB = 17, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 10, BreathRangeFeet = 80, BreathSaveDC = 22,
                    SecondaryBreathSaveDC = 22, SecondaryBreathUsesPerDay = 3,
                    DamageReduction = 5, DRBypass = DamageBypassTag.Magic,
                    SpellResistance = 21,
                    FrightfulPresenceDC = 23, FrightfulPresenceRangeFeet = 150,
                    BiteDamageDice = 6, BiteDamageCount = 2, ClawDamageDice = 8, ClawDamageCount = 1,
                    WingDamageDice = 6, WingDamageCount = 1, TailDamageDice = 8, TailDamageCount = 1,
                    SorcererCasterLevel = 3,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Multiattack", "Alertness", "Flyby Attack", "Weapon Focus" }
                }},
                { DragonAgeCategory.Adult, new DragonAgeStats {
                    HitDice = 20, BaseHP = 200, NaturalArmor = 20,
                    STR = 31, DEX = 10, CON = 21, INT = 18, WIS = 19, CHA = 18,
                    ChallengeRating = 15, BAB = 20, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 12, BreathRangeFeet = 100, BreathSaveDC = 25,
                    SecondaryBreathSaveDC = 25, SecondaryBreathUsesPerDay = 3,
                    DamageReduction = 5, DRBypass = DamageBypassTag.Magic,
                    SpellResistance = 23,
                    FrightfulPresenceDC = 27, FrightfulPresenceRangeFeet = 180,
                    BiteDamageDice = 8, BiteDamageCount = 2, ClawDamageDice = 6, ClawDamageCount = 2,
                    WingDamageDice = 8, WingDamageCount = 1, TailDamageDice = 6, TailDamageCount = 2,
                    SorcererCasterLevel = 5,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Multiattack", "Alertness", "Flyby Attack", "Weapon Focus", "Cleave" }
                }}
            }
        };
        _templates[DragonType.Bronze] = t;
    }

    private static void RegisterCopperDragon()
    {
        var t = new DragonTypeTemplate
        {
            Type = DragonType.Copper,
            TypeName = "Copper",
            SizeClass = DragonSizeClass.Lesser,
            IsMetallic = true,
            BreathShape = BreathWeaponShape.Line,
            BreathDamageType = DamageType.Acid,
            SecondaryBreath = SecondaryBreathType.SlowGas,
            ElementImmunity = DamageType.Acid,
            SorcererSpellIds = new List<string>(),
            SpellLikeAbilityIds = new List<string>(),
            SpriteColor = new Color(0.72f, 0.45f, 0.2f, 1f),
            PanelColor = new Color(0.3f, 0.18f, 0.08f, 0.85f),
            NameColor = new Color(0.9f, 0.7f, 0.4f),
            AgeStats = new Dictionary<DragonAgeCategory, DragonAgeStats>
            {
                { DragonAgeCategory.Wyrmling, new DragonAgeStats {
                    HitDice = 4, BaseHP = 22, NaturalArmor = 3,
                    STR = 11, DEX = 10, CON = 13, INT = 10, WIS = 11, CHA = 10,
                    ChallengeRating = 2, BAB = 4, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 2, BreathRangeFeet = 30, BreathSaveDC = 13,
                    SecondaryBreathSaveDC = 13, SecondaryBreathUsesPerDay = 1,
                    BiteDamageDice = 4, BiteDamageCount = 1, ClawDamageDice = 3, ClawDamageCount = 1,
                    WingDamageDice = 0, WingDamageCount = 0, TailDamageDice = 0, TailDamageCount = 0,
                    Feats = new List<string> { "Improved Initiative" }
                }},
                { DragonAgeCategory.VeryYoung, new DragonAgeStats {
                    HitDice = 7, BaseHP = 45, NaturalArmor = 6,
                    STR = 15, DEX = 10, CON = 15, INT = 12, WIS = 13, CHA = 12,
                    ChallengeRating = 4, BAB = 7, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 4, BreathRangeFeet = 40, BreathSaveDC = 15,
                    SecondaryBreathSaveDC = 15, SecondaryBreathUsesPerDay = 1,
                    BiteDamageDice = 6, BiteDamageCount = 1, ClawDamageDice = 4, ClawDamageCount = 1,
                    WingDamageDice = 3, WingDamageCount = 1, TailDamageDice = 4, TailDamageCount = 1,
                    Feats = new List<string> { "Improved Initiative", "Power Attack" }
                }},
                { DragonAgeCategory.Young, new DragonAgeStats {
                    HitDice = 10, BaseHP = 75, NaturalArmor = 9,
                    STR = 15, DEX = 12, CON = 15, INT = 14, WIS = 15, CHA = 14,
                    ChallengeRating = 7, BAB = 10, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 6, BreathRangeFeet = 40, BreathSaveDC = 17,
                    SecondaryBreathSaveDC = 17, SecondaryBreathUsesPerDay = 2,
                    BiteDamageDice = 6, BiteDamageCount = 1, ClawDamageDice = 4, ClawDamageCount = 1,
                    WingDamageDice = 3, WingDamageCount = 1, TailDamageDice = 4, TailDamageCount = 1,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Multiattack" }
                }},
                { DragonAgeCategory.Juvenile, new DragonAgeStats {
                    HitDice = 13, BaseHP = 97, NaturalArmor = 12,
                    STR = 19, DEX = 10, CON = 17, INT = 14, WIS = 15, CHA = 14,
                    ChallengeRating = 9, BAB = 13, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 8, BreathRangeFeet = 60, BreathSaveDC = 19,
                    SecondaryBreathSaveDC = 19, SecondaryBreathUsesPerDay = 2,
                    BiteDamageDice = 8, BiteDamageCount = 1, ClawDamageDice = 6, ClawDamageCount = 1,
                    WingDamageDice = 4, WingDamageCount = 1, TailDamageDice = 6, TailDamageCount = 1,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Multiattack", "Alertness" }
                }},
                { DragonAgeCategory.YoungAdult, new DragonAgeStats {
                    HitDice = 16, BaseHP = 128, NaturalArmor = 15,
                    STR = 21, DEX = 10, CON = 17, INT = 16, WIS = 17, CHA = 16,
                    ChallengeRating = 11, BAB = 16, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 10, BreathRangeFeet = 60, BreathSaveDC = 21,
                    SecondaryBreathSaveDC = 21, SecondaryBreathUsesPerDay = 3,
                    DamageReduction = 5, DRBypass = DamageBypassTag.Magic,
                    SpellResistance = 19,
                    FrightfulPresenceDC = 21, FrightfulPresenceRangeFeet = 150,
                    BiteDamageDice = 8, BiteDamageCount = 1, ClawDamageDice = 6, ClawDamageCount = 1,
                    WingDamageDice = 4, WingDamageCount = 1, TailDamageDice = 6, TailDamageCount = 1,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Multiattack", "Alertness", "Flyby Attack" }
                }},
                { DragonAgeCategory.Adult, new DragonAgeStats {
                    HitDice = 19, BaseHP = 161, NaturalArmor = 18,
                    STR = 23, DEX = 10, CON = 19, INT = 16, WIS = 17, CHA = 16,
                    ChallengeRating = 13, BAB = 19, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 12, BreathRangeFeet = 80, BreathSaveDC = 23,
                    SecondaryBreathSaveDC = 23, SecondaryBreathUsesPerDay = 3,
                    DamageReduction = 5, DRBypass = DamageBypassTag.Magic,
                    SpellResistance = 21,
                    FrightfulPresenceDC = 23, FrightfulPresenceRangeFeet = 180,
                    BiteDamageDice = 6, BiteDamageCount = 2, ClawDamageDice = 8, ClawDamageCount = 1,
                    WingDamageDice = 6, WingDamageCount = 1, TailDamageDice = 8, TailDamageCount = 1,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Multiattack", "Alertness", "Flyby Attack", "Weapon Focus" }
                }}
            }
        };
        _templates[DragonType.Copper] = t;
    }

    private static void RegisterBrassDragon()
    {
        var t = new DragonTypeTemplate
        {
            Type = DragonType.Brass,
            TypeName = "Brass",
            SizeClass = DragonSizeClass.Lesser,
            IsMetallic = true,
            BreathShape = BreathWeaponShape.Line,
            BreathDamageType = DamageType.Fire,
            SecondaryBreath = SecondaryBreathType.SleepGas,
            ElementImmunity = DamageType.Fire,
            SorcererSpellIds = new List<string>(),
            SpellLikeAbilityIds = new List<string>(),
            SpriteColor = new Color(0.82f, 0.72f, 0.35f, 1f),
            PanelColor = new Color(0.35f, 0.28f, 0.1f, 0.85f),
            NameColor = new Color(0.95f, 0.88f, 0.5f),
            AgeStats = new Dictionary<DragonAgeCategory, DragonAgeStats>
            {
                { DragonAgeCategory.Wyrmling, new DragonAgeStats {
                    HitDice = 4, BaseHP = 22, NaturalArmor = 3,
                    STR = 11, DEX = 10, CON = 13, INT = 10, WIS = 11, CHA = 10,
                    ChallengeRating = 2, BAB = 4, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 1, BreathRangeFeet = 30, BreathSaveDC = 13,
                    SecondaryBreathSaveDC = 13, SecondaryBreathUsesPerDay = 1,
                    BiteDamageDice = 4, BiteDamageCount = 1, ClawDamageDice = 3, ClawDamageCount = 1,
                    WingDamageDice = 0, WingDamageCount = 0, TailDamageDice = 0, TailDamageCount = 0,
                    Feats = new List<string> { "Improved Initiative" }
                }},
                { DragonAgeCategory.VeryYoung, new DragonAgeStats {
                    HitDice = 7, BaseHP = 45, NaturalArmor = 6,
                    STR = 15, DEX = 10, CON = 15, INT = 10, WIS = 11, CHA = 10,
                    ChallengeRating = 3, BAB = 7, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 2, BreathRangeFeet = 40, BreathSaveDC = 15,
                    SecondaryBreathSaveDC = 15, SecondaryBreathUsesPerDay = 1,
                    BiteDamageDice = 6, BiteDamageCount = 1, ClawDamageDice = 4, ClawDamageCount = 1,
                    WingDamageDice = 3, WingDamageCount = 1, TailDamageDice = 4, TailDamageCount = 1,
                    Feats = new List<string> { "Improved Initiative", "Power Attack" }
                }},
                { DragonAgeCategory.Young, new DragonAgeStats {
                    HitDice = 9, BaseHP = 58, NaturalArmor = 8,
                    STR = 17, DEX = 10, CON = 15, INT = 12, WIS = 13, CHA = 12,
                    ChallengeRating = 5, BAB = 9, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 4, BreathRangeFeet = 40, BreathSaveDC = 16,
                    SecondaryBreathSaveDC = 16, SecondaryBreathUsesPerDay = 2,
                    BiteDamageDice = 6, BiteDamageCount = 1, ClawDamageDice = 4, ClawDamageCount = 1,
                    WingDamageDice = 4, WingDamageCount = 1, TailDamageDice = 4, TailDamageCount = 1,
                    SorcererCasterLevel = 0,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Multiattack" }
                }},
                { DragonAgeCategory.Juvenile, new DragonAgeStats {
                    HitDice = 12, BaseHP = 84, NaturalArmor = 11,
                    STR = 21, DEX = 10, CON = 17, INT = 14, WIS = 15, CHA = 14,
                    ChallengeRating = 7, BAB = 12, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 6, BreathRangeFeet = 60, BreathSaveDC = 19,
                    SecondaryBreathSaveDC = 19, SecondaryBreathUsesPerDay = 2,
                    BiteDamageDice = 8, BiteDamageCount = 1, ClawDamageDice = 6, ClawDamageCount = 1,
                    WingDamageDice = 4, WingDamageCount = 1, TailDamageDice = 6, TailDamageCount = 1,
                    SorcererCasterLevel = 0,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Multiattack", "Alertness" }
                }},
                { DragonAgeCategory.YoungAdult, new DragonAgeStats {
                    HitDice = 15, BaseHP = 112, NaturalArmor = 14,
                    STR = 23, DEX = 10, CON = 17, INT = 14, WIS = 15, CHA = 14,
                    ChallengeRating = 9, BAB = 15, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 8, BreathRangeFeet = 60, BreathSaveDC = 20,
                    SecondaryBreathSaveDC = 20, SecondaryBreathUsesPerDay = 3,
                    DamageReduction = 5, DRBypass = DamageBypassTag.Magic,
                    SpellResistance = 17,
                    FrightfulPresenceDC = 19, FrightfulPresenceRangeFeet = 150,
                    BiteDamageDice = 8, BiteDamageCount = 1, ClawDamageDice = 6, ClawDamageCount = 1,
                    WingDamageDice = 4, WingDamageCount = 1, TailDamageDice = 6, TailDamageCount = 1,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Multiattack", "Alertness", "Flyby Attack" }
                }},
                { DragonAgeCategory.Adult, new DragonAgeStats {
                    HitDice = 18, BaseHP = 144, NaturalArmor = 17,
                    STR = 25, DEX = 10, CON = 19, INT = 14, WIS = 15, CHA = 14,
                    ChallengeRating = 11, BAB = 18, BaseSpeed = 8,
                    BreathDamageDice = 6, BreathDamageCount = 10, BreathRangeFeet = 80, BreathSaveDC = 23,
                    SecondaryBreathSaveDC = 23, SecondaryBreathUsesPerDay = 3,
                    DamageReduction = 5, DRBypass = DamageBypassTag.Magic,
                    SpellResistance = 19,
                    FrightfulPresenceDC = 21, FrightfulPresenceRangeFeet = 180,
                    BiteDamageDice = 6, BiteDamageCount = 2, ClawDamageDice = 8, ClawDamageCount = 1,
                    WingDamageDice = 6, WingDamageCount = 1, TailDamageDice = 8, TailDamageCount = 1,
                    Feats = new List<string> { "Improved Initiative", "Power Attack", "Multiattack", "Alertness", "Flyby Attack", "Cleave" }
                }}
            }
        };
        _templates[DragonType.Brass] = t;
    }
}
