// ============================================================================
// SpellDatabase_L.cs — Spells starting with L
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterSpellsL()
    {
        Register(new SpellData
                {
                    SpellId = "lesser_restoration",
                    Name = "Lesser Restoration",
                    Description = "Dispels magical ability penalty or repairs 1d4 ability damage. PHB p.272",
                    SpellLevel = 2, School = "Conjuration",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Healing,
                    HealDice = 4, HealCount = 1, // 1d4 ability restored
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Ability damage restoration not fully implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "levitate",
                    Name = "Levitate",
                    Description = "Subject moves up or down at your direction. 1 min/level. Will negates (object). PHB p.248",
                    SpellLevel = 2, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeSquares = 5,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = 30,
                    BuffType = "levitate",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Vertical movement not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "light",
                    Name = "Light",
                    Description = "Object shines like a torch for 10 min/level.",
                    SpellLevel = 0, School = "Evocation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Light/illumination not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "test_line_60",
                    Name = "Lightning Lance (60-ft Line)",
                    Description = "TEST SPELL: 1d6/CL electricity damage (max 10d6) in a 60-ft line. Reflex half. For testing 60-ft line AoE pattern.",
                    SpellLevel = 2, School = "Evocation [Electricity]",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Area,
                    RangeSquares = 12,          // 60 ft = 12 squares
                    AreaRadius = 12,
                    AoEShapeType = AoEShape.Line,
                    AoESizeSquares = 12,        // 60 ft = 12 squares length
                    AoERangeSquares = 0,        // Line originates from caster
                    AoEFilter = AoETargetFilter.All,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 6, DamageCount = 3, // 3d6 electricity at CL3 (scales 1d6/CL, max 10d6)
                    DamageType = "electricity",
                    AllowsSavingThrow = true,
                    SavingThrowType = "Reflex",
                    SaveHalves = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "locate_object",
                    Name = "Locate Object",
                    Description = "Senses direction toward object. Duration 1 min/level. PHB p.249",
                    SpellLevel = 2, School = "Divination",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = 30,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Object location not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "domain_longstrider",
                    Name = "Longstrider",
                    Description = "Your speed increases by 10 feet (+2 squares movement).",
                    SpellLevel = 1,
                    School = "Transmutation",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1, // 1 hour/level
                    BuffType = "speed",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Speed buff not implemented]"
                });

        // Aliases
        RegisterClassSpellAlias("light_clr", "light", "Cleric", 0);

    }
}
