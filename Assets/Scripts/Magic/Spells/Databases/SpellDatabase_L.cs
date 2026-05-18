// ============================================================================
// SpellDatabase_L.cs — Spells starting with L
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

public static partial class SpellDatabase
{
    private static void RegisterSpellsL()
    {
        Register(new SpellData
                {
                    SpellId = SpellNames.LESSER_RESTORATION,
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
                    SpellId = SpellNames.LEVITATE,
                    Name = "Levitate",
                    Description = "Subject moves up or down at your direction. 1 min/level. Will negates (object). PHB p.248",
                    SpellLevel = 2, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeSquares = 5,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = 30,
                    BuffType = SpellNames.LEVITATE,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Vertical movement not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.LIGHT,
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
                    SpellId = SpellNames.TEST_LINE_60,
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
                    SpellId = SpellNames.LOCATE_OBJECT,
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
                    SpellId = SpellNames.DOMAIN_LONGSTRIDER,
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

        // ──────────────────────────────────────────────────────────────
        // LIGHTNING BOLT  (PHB p.248)
        // Evocation [Electricity]
        // Level: Sor/Wiz 3
        // Components: V, S, M (a bit of fur and an amber, crystal, or glass rod)
        // Casting Time: 1 standard action
        // Range: 120 ft.
        // Area: 120-ft. line (5 ft. wide)
        // Duration: Instantaneous
        // Saving Throw: Reflex half
        // Spell Resistance: Yes
        //
        // You release a powerful stroke of electrical energy that deals
        // 1d6 points of electricity damage per caster level (maximum 10d6)
        // to each creature within its area. The bolt begins at your
        // fingertips.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.LIGHTNING_BOLT,
                    Name = "Lightning Bolt",
                    Description = "Evocation [Electricity]. You release a powerful stroke of electrical energy in a 120-ft line. "
                        + "Deals 1d6 electricity damage per caster level (max 10d6). Reflex half. SR: Yes. Components: V, S, M (fur and amber rod). PHB p.248",
                    HasMaterialComponent = true, // M: fur and amber rod (common — covered by spell component pouch)
                    SpellLevel = 3,
                    School = "Evocation [Electricity]",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Sorcerer", 3),
                        new SpellAvailability("Wizard", 3)
                    },
                    TargetType = SpellTargetType.Area,
                    RangeSquares = 24,          // 120 ft = 24 squares
                    AreaRadius = 24,
                    AoEShapeType = AoEShape.Line,
                    AoESizeSquares = 24,        // 120 ft = 24 squares length
                    AoERangeSquares = 0,        // Line originates from caster
                    AoEFilter = AoETargetFilter.All,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 6,
                    DamageCount = 1, // Placeholder; actual dice = min(CL, 10) resolved at cast time
                    DamageType = "electricity",
                    AllowsSavingThrow = true,
                    SavingThrowType = "Reflex",
                    SaveHalves = true,
                    SpellResistanceApplies = true,
                    DurationType = DurationType.Instantaneous,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        // Aliases
        RegisterClassSpellAlias("light_clr", SpellNames.LIGHT, "Cleric", 0);

    }
}
