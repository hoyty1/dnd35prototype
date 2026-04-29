// ============================================================================
// SpellDatabase_A.cs — Spells starting with A
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterSpellsA()
    {
        Register(new SpellData
                {
                    SpellId = "acid_arrow",
                    Name = "Acid Arrow",
                    Description = "Ranged touch attack, 2d4 acid damage + 2d4/round for 1 round per 3 CL. PHB p.196",
                    SpellLevel = 2, School = "Conjuration",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    // Long range (400 ft + 40 ft/level)
                    RangeCategory = SpellRangeCategory.Long,
                    IsTouch = true,
                    IsRangedTouch = true,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 4, DamageCount = 2, // Initial 2d4
                    DamageType = "acid",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "acid_fog",
                    Name = "Acid Fog",
                    Description = "Acidic vapors fill a 20-ft-radius spread. Creatures inside take a -2 attack penalty from obscuring fumes and corrosive air. No save. 1 round/level (prototype simplified).",
                    SpellLevel = 2, School = "Conjuration",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Area,
                    RangeSquares = 22,
                    AreaRadius = 4,
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 4, // 20-ft radius
                    AoERangeSquares = 22,
                    AoEFilter = AoETargetFilter.EnemiesOnly,
                    EffectType = SpellEffectType.Debuff,
                    BuffAttackBonus = -2,
                    BuffDurationRounds = 3, // 1 round/level at CL3
                    BuffType = "acid_fog",
                    BuffBonusType = BonusType.Circumstance,
                    BonusTypeExplicitlySet = true,
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    AllowsSavingThrow = false,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "acid_splash",
                    Name = "Acid Splash",
                    Description = "An orb of acid deals 1d3 acid damage on a ranged touch attack. Range: Close (25 ft + 5 ft/2 levels).",
                    SpellLevel = 0, School = "Conjuration",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    // Close range (25 ft + 5 ft/2 levels)
                    RangeCategory = SpellRangeCategory.Close,
                    IsTouch = true,
                    IsRangedTouch = true,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 3, DamageCount = 1,
                    DamageType = "acid",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "aid",
                    Name = "Aid",
                    Description = "+1 morale bonus on attack and saves vs fear, plus 1d8 temporary HP. Duration 1 min/level. PHB p.196",
                    SpellLevel = 2, School = "Enchantment",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffAttackBonus = 1,
                    BuffSaveBonus = 1,
                    BuffTempHP = 5, // ~average of 1d8
                    BuffDurationRounds = 30,
                    BuffType = "morale",
                    BuffBonusType = BonusType.Morale,
                    BonusTypeExplicitlySet = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "alarm",
                    Name = "Alarm",
                    Description = "Wards an area for 2 hours/level. Mental or audible alarm when creature enters. PHB p.197",
                    SpellLevel = 1, School = "Abjuration",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 5,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Ward/alarm not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "alter_self",
                    Name = "Alter Self",
                    Description = "Assume form of a similar creature. +10 Disguise, may gain abilities. 10 min/level. PHB p.197",
                    SpellLevel = 2, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Transformation not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "animate_rope",
                    Name = "Animate Rope",
                    Description = "You hurl and animate a rope to entangle a target creature. Ranged touch attack, Reflex negates entanglement. Duration 1 round/level.",
                    SpellLevel = 1,
                    School = "Transmutation",
                    ClassList = new[] { "Wizard", "Sorcerer", "Bard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Custom,
                    RangeSquares = 10, // 50 ft max (5 increments of 10 ft)
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Reflex",
                    SpellResistanceApplies = false,
                    IsRangedTouch = true,
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "arcane_lock",
                    Name = "Arcane Lock",
                    Description = "Magically locks a portal or chest. Permanent. PHB p.200",
                    SpellLevel = 2, School = "Abjuration",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Lock mechanics not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "arcane_mark",
                    Name = "Arcane Mark",
                    Description = "Inscribes a personal rune (visible or invisible) on an object or creature.",
                    SpellLevel = 0, School = "Universal",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Marking not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "augury",
                    Name = "Augury",
                    Description = "Learns whether an action will be good, bad, mixed, or nothing. 70% + 1%/CL success. PHB p.202",
                    SpellLevel = 2, School = "Divination",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Divination/prediction not implemented]"
                });

    }
}
