// ============================================================================
// SpellDatabase_P.cs — Spells starting with P
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

public static partial class SpellDatabase
{
    private static void RegisterSpellsP()
    {
        Register(new SpellData
                {
                    SpellId = SpellNames.PRESTIDIGITATION,
                    Name = "Prestidigitation",
                    Description = "Performs minor tricks: clean, soil, color, flavor, chill, warm, create small trinket. Lasts 1 hour.",
                    SpellLevel = 0, School = "Universal",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 2,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Utility effects not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.DOMAIN_PRODUCE_FLAME,
                    Name = "Produce Flame",
                    Description = "Flames appear in your hand dealing 1d6+level fire damage as touch or ranged touch.",
                    SpellLevel = 2,
                    School = "Evocation",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeSquares = 24,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 6,
                    DamageCount = 1,
                    BonusDamage = 3,
                    DamageType = "fire",
                    BuffDurationRounds = 30,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Sustained flame not fully implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.PROTECTION_FROM_ARROWS,
                    Name = "Protection from Arrows",
                    Description = "Touched creature gains DR 10/magic against ranged weapons and absorbs up to 10 damage per caster level (max 100). Duration 1 hour/level (dismissible) or until discharged. PHB p.266",
                    SpellLevel = 2,
                    School = "Abjuration",
                    ClassList = new[] { "Wizard", "Sorcerer", "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffType = "DR_arrows",
                    BuffDamageReductionAmount = 10,
                    BuffDamageReductionBypass = DamageBypassTag.Magic,
                    BuffDamageReductionRangedOnly = true,
                    DurationType = DurationType.Hours,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    BuffDurationRounds = -1,
                    IsDismissible = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.PROTECTION_FROM_CHAOS,
                    Name = "Protection from Chaos",
                    Description = "Wards against chaotic creatures: +2 deflection AC and +2 resistance on saves vs chaotic creatures; blocks mental control and bodily contact by summoned chaotic creatures.",
                    SpellLevel = 1,
                    School = "Abjuration",
                    ClassList = new[] { "Cleric", "Paladin", "Sorcerer", "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDeflectionBonus = 2,
                    BuffSaveBonus = 2,
                    BuffType = "protection",
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    BuffDurationRounds = 10,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.PROTECTION_FROM_EVIL,
                    Name = "Protection from Evil",
                    Description = "Wards against evil creatures: +2 deflection AC and +2 resistance on saves vs evil creatures; blocks mental control and bodily contact by summoned evil creatures. PHB p.266",
                    SpellLevel = 1,
                    School = "Abjuration",
                    ClassList = new[] { "Cleric", "Paladin", "Sorcerer", "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDeflectionBonus = 2,
                    BuffSaveBonus = 2,
                    BuffType = "protection",
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    BuffDurationRounds = 10,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.PROTECTION_FROM_GOOD,
                    Name = "Protection from Good",
                    Description = "Wards against good creatures: +2 deflection AC and +2 resistance on saves vs good creatures; blocks mental control and bodily contact by summoned good creatures.",
                    SpellLevel = 1,
                    School = "Abjuration",
                    ClassList = new[] { "Cleric", "Sorcerer", "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDeflectionBonus = 2,
                    BuffSaveBonus = 2,
                    BuffType = "protection",
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    BuffDurationRounds = 10,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.PROTECTION_FROM_LAW,
                    Name = "Protection from Law",
                    Description = "Wards against lawful creatures: +2 deflection AC and +2 resistance on saves vs lawful creatures; blocks mental control and bodily contact by summoned lawful creatures.",
                    SpellLevel = 1,
                    School = "Abjuration",
                    ClassList = new[] { "Cleric", "Sorcerer", "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDeflectionBonus = 2,
                    BuffSaveBonus = 2,
                    BuffType = "protection",
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    BuffDurationRounds = 10,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.PURIFY_FOOD_DRINK,
                    Name = "Purify Food and Drink",
                    Description = "Purifies 1 cu.ft./level of food and water.",
                    SpellLevel = 0, School = "Transmutation",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 2,
                    EffectType = SpellEffectType.Buff,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Food/water mechanics not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.PYROTECHNICS,
                    Name = "Pyrotechnics",
                    Description = "Turns fire into blinding light or choking smoke. PHB p.267",
                    SpellLevel = 2, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 8,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    AreaRadius = 4,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Fire interaction not implemented]"
                });

        // Backward-compat aliases for older domain IDs.
        RegisterAlias("domain_protection_from_chaos", SpellNames.PROTECTION_FROM_CHAOS);
        RegisterAlias("domain_protection_from_good", SpellNames.PROTECTION_FROM_GOOD);
        RegisterAlias("domain_protection_from_law", SpellNames.PROTECTION_FROM_LAW);

        // Legacy alias retained for existing prepared spell references.
        RegisterClassSpellAlias("protection_from_evil_clr", SpellNames.PROTECTION_FROM_EVIL, "Cleric", 1);
    }
}
