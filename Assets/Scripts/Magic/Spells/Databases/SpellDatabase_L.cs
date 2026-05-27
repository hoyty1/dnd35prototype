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
                    Description = "Transmutation. Levitate allows you to move yourself, another creature, or an object up and down "
                        + "as you wish. A creature must be willing to be levitated, and an object must be unattended or possessed "
                        + "by a willing creature. You can mentally direct the recipient to move up or down as much as 20 feet each "
                        + "round; doing so is a move action. Duration 1 min/level (D). Will negates (object). SR: Yes. PHB p.248",
                    SpellLevel = 2, School = "Transmutation",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Sorcerer", 2),
                        new SpellAvailability("Wizard", 2)
                    },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = 10, // Legacy: 1 min/level = 10 rounds/level
                    BuffType = SpellNames.LEVITATE,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    IsDismissible = true,
                    AllowsSavingThrow = true, // Will negates (harmless or object)
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    HasMaterialComponent = true, // M: leather loop or golden wire
                    IsPlaceholder = false
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

        // ──────────────────────────────────────────────────────────────
        // LULLABY  (PHB p.249)
        // Enchantment (Compulsion) [Mind-Affecting]
        // Level: Brd 0
        // Components: V, S
        // Casting Time: 1 standard action
        // Range: Medium (100 ft. + 10 ft./level)
        // Area: Living creatures within a 10-ft.-radius burst
        // Duration: Concentration + 1 round/level (D)
        // Saving Throw: Will negates
        // Spell Resistance: Yes
        //
        // Target takes –5 penalty on Listen checks and –2 penalty on
        // Will saves against sleep effects while the lullaby is in effect.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.LULLABY,
                    Name = "Lullaby",
                    Description = "Enchantment (Compulsion) [Mind-Affecting]. Targets take –5 on Listen checks and –2 on Will saves against sleep effects. Will negates. PHB p.249",
                    SpellLevel = 0, School = "Enchantment",
                    ClassList = new[] { "Bard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Medium,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    BuffDurationRounds = 10,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
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

        // ──────────────────────────────────────────────────────────────
        // LONGSTRIDER  (PHB p.249)
        // Transmutation
        // Level: Druid 1, Ranger 1 / Travel domain 1
        // Components: V, S, M
        // Casting Time: 1 standard action
        // Range: Personal
        // Target: You
        // Duration: 1 hour/level (D)
        //
        // Your base land speed increases by 10 feet. This is an
        // enhancement bonus.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.DOMAIN_LONGSTRIDER,
                    Name = "Longstrider",
                    Description = "Your base land speed increases by 10 feet (+2 squares movement). Enhancement bonus, 1 hour/level. PHB p.249",
                    SpellLevel = 1,
                    School = "Transmutation",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffSpeedBonusFeet = 10,
                    BuffType = "enhancement",
                    BuffBonusType = BonusType.Enhancement,
                    BonusTypeExplicitlySet = true,
                    DurationType = DurationType.Hours,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    IsDismissible = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
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

        // ──────────────────────────────────────────────────────────────
        // LESSER GLOBE OF INVULNERABILITY  (PHB p.246)
        // Abjuration
        // Level: Sor/Wiz 4
        // Components: V, S, M (a glass or crystal bead)
        // Casting Time: 1 standard action
        // Range: 10 ft.
        // Area: 10-ft.-radius spherical emanation, centered on you
        // Duration: 1 round/level (D)
        // Saving Throw: None
        // Spell Resistance: No
        //
        // An immobile, faintly shimmering magical sphere surrounds you
        // and excludes all spell effects of 3rd level or lower. The area
        // or effect of any such spells does not include the area of the
        // lesser globe of invulnerability. Such spells fail to affect any
        // target located within the globe. Spells of 4th level and higher
        // are not affected by the globe. The globe can be brought down by
        // a targeted dispel magic spell.
        //
        // IMPLEMENTATION: Self-targeted buff that creates a
        // LesserGlobeOfInvulnerabilityAreaEffect (emanation centered on
        // caster, moves with caster). Blocks spell effects ≤ 3rd level
        // against creatures inside the globe.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.LESSER_GLOBE_OF_INVULNERABILITY,
                    Name = "Lesser Globe of Invulnerability",
                    Description = "Abjuration. A 10-ft-radius emanation centered on you excludes all spell effects of 3rd level or lower. "
                        + "Such spells fail to affect any target within the globe. Spells of 4th level and higher pass through normally. "
                        + "Duration 1 round/level (D). Components: V, S, M (glass bead). PHB p.246",
                    HasMaterialComponent = true, // M: glass or crystal bead (common — covered by spell component pouch)
                    SpellLevel = 4,
                    School = "Abjuration",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Sorcerer", 4),
                        new SpellAvailability("Wizard", 4)
                    },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffType = SpellNames.LESSER_GLOBE_OF_INVULNERABILITY,
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    IsDismissible = true,
                    AllowsSavingThrow = false,
                    SpellResistanceApplies = false,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        // Aliases
        RegisterClassSpellAlias("light_clr", SpellNames.LIGHT, "Cleric", 0);

        // Locate Object: Cleric 3 (also Wizard 2, Bard 2 — already registered above)
        RegisterClassSpellAlias("locate_object_clr", SpellNames.LOCATE_OBJECT, "Cleric", 3);

        // ── Phase 1: Bard/Paladin/Ranger/Druid class assignments ──

        // Light: Bard 0, Druid 0
        RegisterClassSpellAlias("light_brd", SpellNames.LIGHT, "Bard", 0);
        RegisterClassSpellAlias("light_drd", SpellNames.LIGHT, "Druid", 0);

    }
}
