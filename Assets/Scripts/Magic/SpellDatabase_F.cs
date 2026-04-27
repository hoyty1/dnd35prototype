// ============================================================================
// SpellDatabase_F.cs — Spells starting with F
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterSpellsF()
    {
        Register(new SpellData
                {
                    SpellId = "false_life",
                    Name = "False Life",
                    Description = "Gain 1d10+CL temporary hit points (max +10). Duration 1 hour/level. PHB p.229",
                    SpellLevel = 2, School = "Necromancy",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffTempHP = 8, // ~average of 1d10+3 at CL3
                    BuffDurationRounds = -1,
                    BuffType = "temp_hp",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "feather_fall",
                    Name = "Feather Fall",
                    Description = "Objects or creatures fall slowly (60 ft/round). Immediate action. PHB p.229",
                    SpellLevel = 1, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeSquares = 5,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = 1,
                    ActionType = SpellActionType.Free, // Immediate action
                    ProvokesAoO = false,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Falling mechanics not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "find_traps",
                    Name = "Find Traps",
                    Description = "+10 insight bonus on Search checks to find traps. Duration 1 min/level. PHB p.230",
                    SpellLevel = 2, School = "Divination",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = 30,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Trap detection not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "test_cone_30",
                    Name = "Flame Jet (30-ft Cone)",
                    Description = "TEST SPELL: 5d6 fire damage in a 30-ft cone. Reflex half. For testing 30-ft cone AoE pattern.",
                    SpellLevel = 2, School = "Evocation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Area,
                    RangeSquares = 6,
                    AreaRadius = 6,
                    AoEShapeType = AoEShape.Cone,
                    AoESizeSquares = 6, // 30 ft = 6 squares length
                    AoERangeSquares = 0, // Cone originates from caster
                    AoEFilter = AoETargetFilter.All,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 6, DamageCount = 5, // 5d6 fire
                    DamageType = "fire",
                    AllowsSavingThrow = true,
                    SavingThrowType = "Reflex",
                    SaveHalves = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "flame_strike",
                    Name = "Flame Strike",
                    Description = "A vertical column of divine fire deals 1d6/level damage (max 15d6). Reflex half. Damage is split between fire and divine power (prototype: fire/positive).",
                    SpellLevel = 2, School = "Evocation",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Medium,
                    AreaRadius = 2,
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 2, // 10-ft radius
                    AoERangeSquares = 22,
                    AoEFilter = AoETargetFilter.EnemiesOnly,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 6, DamageCount = 3, // 1d6/level at CL3
                    DamageType = "fire/positive",
                    AllowsSavingThrow = true,
                    SavingThrowType = "Reflex",
                    SaveHalves = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "flaming_sphere",
                    Name = "Flaming Sphere",
                    Description = "Rolling ball of fire, 2d6 fire damage. Reflex negates. Lasts 1 round/level, movable. PHB p.232",
                    SpellLevel = 2, School = "Evocation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Medium,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 6, DamageCount = 2, // 2d6
                    DamageType = "fire",
                    AllowsSavingThrow = true,
                    SavingThrowType = "Reflex",
                    SaveHalves = false, // Negates
                    BuffDurationRounds = 3,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "flare",
                    Name = "Flare",
                    Description = "Dazzles one creature for 1 minute (–1 on attack rolls and sight-based Search/Spot checks). Fortitude negates.",
                    SpellLevel = 0, School = "Evocation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Fortitude",
                    BuffDurationRounds = 10,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "floating_disk",
                    Name = "Floating Disk",
                    Description = "Creates 3-ft diameter horizontal disk that holds 100 lb/level. Follows you. 1 hr/level. PHB p.232",
                    SpellLevel = 1, School = "Evocation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Carrying/utility not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "fog_cloud",
                    Name = "Fog Cloud",
                    Description = "Fog obscures vision in a 20-ft radius spread. Creatures within gain concealment (20% miss chance). Duration 10 min/level. PHB p.232",
                    SpellLevel = 2, School = "Conjuration",
                    ClassList = new[] { "Wizard", "Sorcerer", "Druid", "Cleric" },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Medium,
                    RangeSquares = 22,
                    AreaRadius = 4,
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 4,
                    AoERangeSquares = 22,
                    AoEFilter = AoETargetFilter.All,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = 100,
                    BuffType = "concealment",
                    DurationType = DurationType.Minutes,
                    DurationValue = 10,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "foxs_cunning",
                    Name = "Fox's Cunning",
                    Description = "Subject gains +4 enhancement bonus to INT for 1 min/level. PHB p.233",
                    SpellLevel = 2, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffStatName = "INT",
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

    }
}
