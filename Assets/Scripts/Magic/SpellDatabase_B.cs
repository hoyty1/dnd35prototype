// ============================================================================
// SpellDatabase_B.cs — Spells starting with B
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterSpellsB()
    {
        Register(new SpellData
                {
                    SpellId = "bane",
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
                    SpellId = "domain_barkskin",
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
                    SpellId = "bears_endurance",
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
                    SpellId = "bless",
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
                    SpellId = "blindness_deafness_wiz",
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
                    SpellId = "blur",
                    Name = "Blur",
                    Description = "Attacks against subject have 20% miss chance. Duration 1 min/level. PHB p.206",
                    SpellLevel = 2, School = "Illusion",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffACBonus = 2, // Simplified: 20% miss ~= +2 AC effective
                    BuffDurationRounds = 30,
                    BuffType = "concealment",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "bulls_strength",
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
                    SpellId = "burning_hands",
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
        RegisterClassSpellAlias("bears_endurance_clr", "bears_endurance", "Cleric", 2);
        RegisterClassSpellAlias("bulls_strength_clr", "bulls_strength", "Cleric", 2);

    }
}
