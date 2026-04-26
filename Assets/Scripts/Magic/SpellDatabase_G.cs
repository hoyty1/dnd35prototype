// ============================================================================
// SpellDatabase_G.cs — Spells starting with G
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterSpellsG()
    {
        Register(new SpellData
                {
                    SpellId = "gentle_repose",
                    Name = "Gentle Repose",
                    Description = "Preserves a corpse. Duration 1 day/level. PHB p.235",
                    SpellLevel = 2, School = "Necromancy",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Corpse preservation not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "ghost_sound",
                    Name = "Ghost Sound",
                    Description = "Figment sounds. Will disbelief (if interacted with).",
                    SpellLevel = 0, School = "Illusion",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 5,
                    EffectType = SpellEffectType.Buff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    BuffDurationRounds = 3,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Illusion mechanics not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "ghoul_touch",
                    Name = "Ghoul Touch",
                    Description = "Touch attack paralyzes one living subject for 1d6+2 rounds. Fort negates. Sickens nearby. PHB p.235",
                    SpellLevel = 2, School = "Necromancy",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    IsMeleeTouch = true,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Fortitude",
                    BuffDurationRounds = 5, // 1d6+2 avg
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "test_cone_60",
                    Name = "Glacial Blast (60-ft Cone)",
                    Description = "TEST SPELL: 10d6 cold damage in a 60-ft cone. Reflex half. For testing 60-ft cone AoE pattern.",
                    SpellLevel = 2, School = "Evocation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Area,
                    RangeSquares = 12,
                    AreaRadius = 12,
                    AoEShapeType = AoEShape.Cone,
                    AoESizeSquares = 12, // 60 ft = 12 squares length
                    AoERangeSquares = 0, // Cone originates from caster
                    AoEFilter = AoETargetFilter.All,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 6, DamageCount = 10, // 10d6 cold
                    DamageType = "cold",
                    AllowsSavingThrow = true,
                    SavingThrowType = "Reflex",
                    SaveHalves = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "glitterdust",
                    Name = "Glitterdust",
                    Description = "Blinds creatures in area and outlines invisible creatures. Will save negates blindness. 1 round/level. PHB p.236",
                    SpellLevel = 2, School = "Conjuration",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy, // Simplified
                    RangeCategory = SpellRangeCategory.Medium,
                    AreaRadius = 2,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    BuffDurationRounds = 3,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "grease",
                    Name = "Grease",
                    Description = "Covers ground in grease, creatures must save or fall. Reflex save. 1 round/level. PHB p.237",
                    SpellLevel = 1, School = "Conjuration",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy, // Simplified
                    RangeCategory = SpellRangeCategory.Close,
                    AreaRadius = 2,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Reflex",
                    BuffDurationRounds = 3,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "guidance",
                    Name = "Guidance",
                    Description = "+1 on one attack roll, saving throw, or skill check. Duration 1 minute or until discharged.",
                    SpellLevel = 0, School = "Divination",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffAttackBonus = 1,
                    BuffSaveBonus = 1,
                    BuffDurationRounds = 10,
                    BuffType = "competence",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "gust_of_wind",
                    Name = "Gust of Wind",
                    Description = "Blasts of wind knock down/push creatures. Fortitude save or be blown away (size-dependent). PHB p.238",
                    SpellLevel = 2, School = "Evocation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeSquares = 12, // 60 ft
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Fortitude",
                    BuffDurationRounds = 1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

    }
}
