// ============================================================================
// SpellDatabase_B.cs — Spells starting with B
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

public static partial class SpellDatabase
{
    private static void RegisterSpellsB()
    {
        Register(new SpellData
                {
                    SpellId = SpellNames.BANE,
                    Name = "Bane",
                    Description = "Enemies take –1 on attack rolls and saves vs fear. 1 min/level. Will save negates. PHB p.203",
                    SpellLevel = 1, School = "Enchantment",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy, // Simplified from area
                    RangeSquares = 10,
                    AreaRadius = 10,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    BuffAttackBonus = -1,
                    BuffSaveBonus = -1,
                    BuffDurationRounds = 30,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.DOMAIN_BARKSKIN,
                    Name = "Barkskin",
                    Description = "Grants +2 enhancement bonus to natural armor (+1 for every three levels above 3rd, max +5 at 12th).",
                    SpellLevel = 2,
                    School = "Transmutation",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffACBonus = 2,
                    BuffDurationRounds = 30,
                    BuffType = "natural_armor",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.BEARS_ENDURANCE,
                    Name = "Bear's Endurance",
                    Description = "Subject gains +4 enhancement bonus to CON for 1 min/level. PHB p.203",
                    SpellLevel = 2, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffStatName = "CON",
                    BuffStatBonus = 4,
                    BuffDurationRounds = 30,
                    BuffType = "enhancement",
                    BuffBonusType = BonusType.Enhancement,
                    BonusTypeExplicitlySet = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.BLESS,
                    Name = "Bless",
                    Description = "Allies in 50-ft burst gain +1 morale bonus on attack rolls and saves vs fear. 1 min/level. PHB p.205",
                    SpellLevel = 1, School = "Enchantment",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Area, // 50-ft burst centered on caster
                    RangeSquares = 0, // Self-centered burst
                    AreaRadius = 10,
                    // AoE properties
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 10, // 50 ft radius = 10 squares
                    AoERangeSquares = 0, // Self-centered burst (centered on caster)
                    AoEFilter = AoETargetFilter.AlliesOnly,
                    EffectType = SpellEffectType.Buff,
                    BuffAttackBonus = 1,
                    BuffSaveBonus = 1, // vs fear, simplified to all saves
                    BuffDurationRounds = 30, // Legacy: 30 rounds at CL3
                    BuffType = "morale",
                    BuffBonusType = BonusType.Morale,
                    BonusTypeExplicitlySet = true,
                    // Duration system: 1 min/level (D&D 3.5e PHB p.205)
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.BLINDNESS_DEAFNESS_WIZ,
                    Name = "Blindness/Deafness",
                    Description = "Makes subject blind or deaf. Fortitude negates. Permanent. PHB p.206",
                    SpellLevel = 2, School = "Necromancy",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Medium,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Fortitude",
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.BLUR,
                    Name = "Blur",
                    Description = "Illusion (Glamer). Subject touched appears blurred and wavering, gaining concealment (20% miss chance). See Invisible does not negate this effect; True Seeing does. Duration 1 min/level (D). Save: Will negates (harmless). SR: Yes (harmless). Components: V. PHB p.206",
                    SpellLevel = 2,
                    School = "Illusion (Glamer)",
                    ClassList = new[] { "Wizard", "Sorcerer", "Bard" },
                    TargetType = SpellTargetType.Touch,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    IsMeleeTouch = true,
                    EffectType = SpellEffectType.Buff,
                    BuffType = "concealment",
                    BuffBonusType = BonusType.Concealment,
                    BonusTypeExplicitlySet = true,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    IsDismissible = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = false,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.BULLS_STRENGTH,
                    Name = "Bull's Strength",
                    Description = "Subject gains +4 enhancement bonus to STR for 1 min/level. PHB p.207",
                    SpellLevel = 2, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffStatName = "STR",
                    BuffStatBonus = 4,
                    BuffDurationRounds = 30,
                    BuffType = "enhancement",
                    BuffBonusType = BonusType.Enhancement,
                    BonusTypeExplicitlySet = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.BURNING_HANDS,
                    Name = "Burning Hands",
                    Description = "1d4/level fire damage (max 5d4) in 15-ft cone. Reflex half. PHB p.207",
                    SpellLevel = 1, School = "Evocation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Area, // Cone AoE from caster
                    RangeSquares = 3, // 15-ft cone ~3 squares
                    AreaRadius = 3,
                    // AoE properties
                    AoEShapeType = AoEShape.Cone,
                    AoESizeSquares = 3, // 15 ft = 3 squares length
                    AoERangeSquares = 0, // Cone originates from caster (no placement range)
                    AoEFilter = AoETargetFilter.All, // Hits all creatures in cone
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 4, DamageCount = 3, // 3d4 at CL3
                    DamageType = "fire",
                    AllowsSavingThrow = true,
                    SavingThrowType = "Reflex",
                    SaveHalves = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // Aliases
        RegisterClassSpellAlias("bears_endurance_clr", SpellNames.BEARS_ENDURANCE, "Cleric", 2);
        RegisterClassSpellAlias("bulls_strength_clr", SpellNames.BULLS_STRENGTH, "Cleric", 2);

    }
}
