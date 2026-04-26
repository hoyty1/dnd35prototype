// ============================================================================
// SpellDatabase_P.cs — Spells starting with P
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterSpellsP()
    {
        Register(new SpellData
                {
                    SpellId = "prestidigitation",
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
                    SpellId = "domain_produce_flame",
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
                    SpellId = "protection_from_arrows",
                    Name = "Protection from Arrows",
                    Description = "Subject gains DR 10/magic vs ranged weapons. Absorbs 10/CL damage (30 at CL3). 1 hr/level. PHB p.266",
                    SpellLevel = 2, School = "Abjuration",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    BuffType = "DR_arrows",
                    BuffDamageReductionAmount = 10,
                    BuffDamageReductionBypass = DamageBypassTag.Magic,
                    BuffDamageReductionRangedOnly = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "domain_protection_from_chaos",
                    Name = "Protection from Chaos",
                    Description = "+2 deflection AC and +2 resistance on saves against chaotic creatures.",
                    SpellLevel = 1,
                    School = "Abjuration",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffACBonus = 2,
                    BuffDurationRounds = 30, // 1 min/level
                    BuffType = "protection_alignment",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Alignment protection not fully implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "protection_from_evil",
                    Name = "Protection from Evil",
                    Description = "+2 deflection bonus to AC and +2 resistance bonus on saves vs evil creatures. 1 min/level. PHB p.266",
                    SpellLevel = 1, School = "Abjuration",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDeflectionBonus = 2,
                    BuffSaveBonus = 2,
                    BuffDurationRounds = 30,
                    BuffType = "protection",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "domain_protection_from_good",
                    Name = "Protection from Good",
                    Description = "+2 deflection AC and +2 resistance on saves against good creatures.",
                    SpellLevel = 1,
                    School = "Abjuration",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffACBonus = 2,
                    BuffDurationRounds = 30,
                    BuffType = "protection_alignment",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Alignment protection not fully implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "domain_protection_from_law",
                    Name = "Protection from Law",
                    Description = "+2 deflection AC and +2 resistance on saves against lawful creatures.",
                    SpellLevel = 1,
                    School = "Abjuration",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffACBonus = 2,
                    BuffDurationRounds = 30,
                    BuffType = "protection_alignment",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Alignment protection not fully implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "purify_food_drink",
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
                    SpellId = "pyrotechnics",
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

        // Aliases
        RegisterClassSpellAlias("protection_from_evil_clr", "protection_from_evil", "Cleric", 1);

    }
}
