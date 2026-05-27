using System.Collections.Generic;
using UnityEngine;

// ════════════════════════════════════════════════════════════════════════
//  D&D 3.5e Rods — Core Data Structures (DMG pp. 224–228)
//
//  Rods are scepter-like devices with unique powers. Most are use-activated
//  or command-word activated. Metamagic rods allow applying metamagic feats
//  to spells WITHOUT increasing the spell slot level. All rods weigh 5 lbs.
//
//  Rod Categories:
//  - Metamagic (21 rods = 7 types × 3 power levels)
//  - Combat (7 rods)
//  - Utility (5 rods)
//  - Legendary (3 rods — also in Combat/Utility, tagged legendary)
//
//  Total: 36 distinct rod entries (33 unique + 3 legendary overlaps)
// ════════════════════════════════════════════════════════════════════════

/// <summary>
/// Rod category classification per DMG pp. 224–228.
/// </summary>
public enum RodCategory
{
    Metamagic,   // Applies metamagic without slot increase
    Combat,      // Weapon-like or combat-oriented abilities
    Utility,     // Detection, protection, special abilities
    Legendary    // Iconic powerful rods (Alertness, Lordly Might, Security)
}

/// <summary>
/// Power level for metamagic rods. Determines maximum spell level affected.
/// </summary>
public enum RodPowerLevel
{
    None,       // Non-metamagic rods
    Lesser,     // Affects spells up to 3rd level
    Normal,     // Affects spells up to 6th level
    Greater     // Affects spells up to 9th level
}

/// <summary>
/// Weapon modes for Rod of Lordly Might (DMG p.226).
/// Button-activated transformation between 6 weapon forms.
/// </summary>
public enum LordlyMightWeaponMode
{
    HeavyMace,      // Default: +3 heavy mace (1d8+3)
    FlamingSword,    // Button 1: +1 flaming longsword (1d8+1 + 1d6 fire)
    Battleaxe,       // Button 2: +4 battleaxe (1d8+4)
    Shortspear,      // Button 3: +3 shortspear (1d6+3, 20 ft range)
    Longsword,       // Button 4: +2 longsword (1d8+2)
    ClimbingPole     // Button 5: 50-ft climbing pole (not a weapon)
}

/// <summary>
/// Core rod definition containing all mechanical data for a single rod.
/// Registered in RodDatabase and used to populate ItemData fields.
/// Immutable after creation — runtime state tracked on ItemData instance.
/// </summary>
public class RodDefinition
{
    // ── Identity ──────────────────────────────────────────────
    public string RodId;                 // Unique key (e.g., "rod_empower_lesser")
    public string DisplayName;           // Human-readable name
    public string Description;           // Full DMG description
    public RodCategory Category;         // Metamagic/Combat/Utility/Legendary
    public bool IsLegendary;             // True for Alertness, Lordly Might, Security

    // ── Economics ─────────────────────────────────────────────
    public int MarketPrice;              // GP value per DMG
    public int CasterLevel = 17;         // Most rods are CL 17th
    public float WeightLbs = 5f;         // Standard rod weight

    // ── Metamagic Rod Fields ──────────────────────────────────
    public bool IsMetamagicRod;          // True for all 21 metamagic rods
    public MetamagicFeatId MetamagicType = MetamagicFeatId.None; // Which metamagic this rod applies
    public RodPowerLevel PowerLevel = RodPowerLevel.None;
    public int MaxSpellLevel;            // 3 (Lesser), 6 (Normal), 9 (Greater)
    public int SlotLevelIncrease;        // Normal slot increase of the metamagic (for display)
    public int UsesPerDay = 3;           // Standard: 3/day for metamagic rods

    // ── Combat Rod Fields ─────────────────────────────────────
    public bool IsCombatRod;

    // Rod of Absorption
    public bool CanAbsorbSpells;         // Rod of Absorption
    public int MaxAbsorbedLevels = 50;   // 50-level capacity

    // Rod of Cancellation
    public bool CanCancelMagic;          // Single-use rod

    // Rod of Flailing
    public bool IsFlail;                 // +3 heavy flail / dire flail mode
    public int FlailEnhancement = 3;
    public string FlailDamageDice = "1d10";

    // Immovable Rod
    public bool IsImmovable;
    public int HoldWeightLbs = 8000;     // Supports 8,000 lbs
    public int MoveDC = 30;              // DC 30 Strength check to move

    // Rod of Lordly Might
    public bool IsLordlyMight;
    public int FearConeDC = 16;          // Fear cone DC 16 Will
    public int FearConeRange = 30;       // 30 ft cone
    public int FearUsesPerDay = 2;       // 2/day

    // Rod of Metal and Mineral Detection
    public bool CanDetectMetals;
    public float DetectionRadiusFt = 30f;
    public float PenetratesStoneFt = 10f;

    // Rod of Splendor
    public bool IsSplendor;
    public int TentCapacity = 100;       // Pavilion: 100 people
    public int FeastCapacity = 12;       // Feast: 12 people
    public int ClothesPerWeek = 7;       // Fine clothes: 7/week

    // ── Utility Rod Fields ────────────────────────────────────
    public bool IsUtilityRod;

    // Rod of Alertness
    public bool IsAlertness;
    public int AlertnessInsightBonus = 1; // +1 insight to Init and Listen
    public bool GrantsSeeInvisible;
    public bool GrantsDetectEvil;
    public bool GrantsDetectMagic;
    public bool GrantsLight;

    // Rod of Enemy Detection
    public bool CanDetectEnemies;
    public float EnemyDetectionRadiusFt = 60f;
    public float EnemyPenetratesStoneFt = 20f;
    public int EnemyDetectionUsesPerDay = 3;

    // Rod of Negation
    public bool IsNegation;
    public int DispelAtWillCL = 15;      // CL 15 for dispel checks
    public int GreaterDispelUsesPerDay = 2;

    // Rod of Python
    public bool CanTransformToSnake;
    public int SnakeHP = 60;             // 11d8+22
    public int SnakeAC = 15;
    public int SnakeAttackBonus = 13;
    public string SnakeDamage = "1d3+10";
    public bool SnakeHasConstrict;
    public string SnakeConstrictDamage = "1d3+10";

    // Rod of Security
    public bool CanCreateDemiplane;
    public int DemiplaneCapacity = 200;  // 200 people
    public float DemiplaneDurationHours = 12f; // 12 hours inside = 1 hour outside
    public bool DemiplaneHeals;          // Complete rest and healing

    // ── Weapon Stats (for combat rods) ────────────────────────
    public bool CanBeUsedAsWeapon;       // True for Flailing, Lordly Might, Alertness
    public int DefaultEnhancementBonus;
    public string DefaultDamageDice;
    public string DefaultWeaponType;     // "Heavy Mace", "Heavy Flail", etc.

    /// <summary>
    /// Get the maximum spell level this metamagic rod can affect.
    /// Returns 0 for non-metamagic rods.
    /// </summary>
    public int GetMaxSpellLevelForPower()
    {
        switch (PowerLevel)
        {
            case RodPowerLevel.Lesser: return 3;
            case RodPowerLevel.Normal: return 6;
            case RodPowerLevel.Greater: return 9;
            default: return 0;
        }
    }

    /// <summary>
    /// Get display string for power level (e.g., "Lesser", "Normal", "Greater").
    /// </summary>
    public string GetPowerLevelDisplay()
    {
        switch (PowerLevel)
        {
            case RodPowerLevel.Lesser: return "Lesser";
            case RodPowerLevel.Normal: return "Normal";
            case RodPowerLevel.Greater: return "Greater";
            default: return "";
        }
    }

    /// <summary>
    /// Get the metamagic feat name for display (e.g., "Empower Spell").
    /// </summary>
    public string GetMetamagicDisplayName()
    {
        return MetamagicData.GetDisplayName(MetamagicType);
    }
}
