// ============================================================================
// SpellDatabase_F.cs — Spells starting with F
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

public static partial class SpellDatabase
{
    private static void RegisterSpellsF()
    {
        Register(new SpellData
                {
                    SpellId = SpellNames.FALSE_LIFE,
                    Name = "False Life",
                    Description = "Gain 1d10 + caster level (max +10) temporary hit points. Temp HP are lost before regular HP and cannot be healed. Duration 1 hour/level or until discharged. PHB p.229",
                    SpellLevel = 2, School = "Necromancy",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffTempHP = 0, // Calculated at cast time: 1d10 + min(CL, 10)
                    DurationType = DurationType.Hours,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    BuffType = "temp_hp",
                    SpellResistanceApplies = false,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.FEATHER_FALL,
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
                    SpellId = SpellNames.FIND_TRAPS,
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

        // ──────────────────────────────────────────────────────────────
        // FIREBALL  (PHB p.231)
        // Evocation [Fire]
        // Level: Sor/Wiz 3
        // Components: V, S, M (a tiny ball of bat guano and sulfur)
        // Casting Time: 1 standard action
        // Range: Long (400 ft. + 40 ft./level)
        // Area: 20-ft.-radius spread
        // Duration: Instantaneous
        // Saving Throw: Reflex half
        // Spell Resistance: Yes
        //
        // A fireball spell is an explosion of flame that detonates
        // with a low roar and deals 1d6 points of fire damage per
        // caster level (maximum 10d6) to every creature within the area.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.FIREBALL,
                    Name = "Fireball",
                    Description = "Evocation [Fire]. An explosion of flame deals 1d6 fire damage per caster level (max 10d6) "
                        + "in a 20-ft.-radius spread. Reflex half. SR: Yes. PHB p.231",
                    SpellLevel = 3,
                    School = "Evocation [Fire]",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Sorcerer", 3),
                        new SpellAvailability("Wizard", 3)
                    },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Long,
                    AreaRadius = 4,
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 4, // 20-ft radius = 4 squares
                    AoERangeSquares = 0, // use Long range profile
                    AoEFilter = AoETargetFilter.All,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 6,
                    DamageCount = 1, // Placeholder; actual dice = min(CL, 10) resolved at cast time
                    DamageType = "fire",
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

        Register(new SpellData
                {
                    SpellId = SpellNames.TEST_CONE_30,
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
                    SpellId = SpellNames.FLAME_STRIKE,
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
                    SpellId = SpellNames.FLESH_TO_STONE,
                    Name = "Flesh to Stone",
                    Description = "Turns a creature to stone. Fortitude negates. PHB p.232",
                    SpellLevel = 6, School = "Transmutation",
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
                    SpellId = SpellNames.FLAMING_SPHERE,
                    Name = "Flaming Sphere",
                    Description = "Creates a rolling sphere of fire. Sphere deals 2d6 fire damage to a creature whose space it enters (Reflex negates). Lasts 1 round/level and can be directed up to 30 ft each round as a move action. PHB p.232",
                    SpellLevel = 2, School = "Evocation",
                    ClassList = new[] { "Wizard", "Sorcerer", "Druid" },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Medium,
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 0, // Placement on a single grid cell
                    AoERangeSquares = 0, // use Medium range progression
                    AoEFilter = AoETargetFilter.All,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 6, DamageCount = 2, // 2d6
                    DamageType = "fire",
                    AllowsSavingThrow = true,
                    SavingThrowType = "Reflex",
                    SaveHalves = false, // Reflex negates
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    BuffDurationRounds = 1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.FLARE,
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
                    SpellId = SpellNames.FLOATING_DISK,
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
                    SpellId = SpellNames.FOG_CLOUD,
                    Name = "Fog Cloud",
                    Description = "Conjuration (Creation). Fog obscures all sight, including darkvision, beyond 5 feet in a 20-ft radius spread. Creatures inside gain concealment (20% miss chance), with total concealment beyond 5 feet. Duration 10 min/level. PHB p.232",
                    SpellLevel = 2, School = "Conjuration",
                    ClassList = new[] { "Wizard", "Sorcerer", "Druid" },
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

        // ──────────────────────────────────────────────────────────────
        // FLAME ARROW  (PHB p.231)
        // Transmutation [Fire]
        // Level: Sor/Wiz 3, Druid 2 (prototype: Wizard/Sorcerer 3)
        // Components: V, S, M (a drop of oil and a small piece of flint)
        // Casting Time: 1 standard action
        // Range: Close (25 ft. + 5 ft./2 levels)
        // Target: Fifty projectiles, all of which must be in contact with each other at the time of casting
        // Duration: 10 min./level
        // Saving Throw: None
        // Spell Resistance: No
        //
        // You turn ammunition (such as arrows, bolts, shuriken, and stones)
        // into fiery projectiles. Each piece of ammunition deals an extra
        // 1d6 points of fire damage to any target it hits. A flaming
        // projectile can easily ignite a flammable object or structure,
        // but it won't ignite a creature it strikes.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.FLAME_ARROW,
                    Name = "Flame Arrow",
                    Description = "Transmutation [Fire]. Turns up to 50 projectiles into fiery projectiles. "
                        + "Each deals an extra 1d6 fire damage on hit. Duration 10 min/level or until discharged. "
                        + "Components: V, S, M (a drop of oil and a small piece of flint). PHB p.231",
                    SpellLevel = 3,
                    School = "Transmutation [Fire]",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Sorcerer", 3),
                        new SpellAvailability("Wizard", 3)
                    },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Buff,
                    DurationType = DurationType.Minutes,
                    DurationValue = 10,
                    DurationScalesWithLevel = true,
                    BuffDurationRounds = 100,
                    SpellResistanceApplies = false,
                    AllowsSavingThrow = false,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        // ──────────────────────────────────────────────────────────────
        // FEAR  (PHB p.229)
        // Necromancy [Fear, Mind-Affecting]
        // Level: Brd 3, Sor/Wiz 4
        // Components: V, S, M (either the heart of a hen or a white feather)
        // Casting Time: 1 standard action
        // Range: 30 ft.
        // Area: Cone-shaped burst
        // Duration: 1 round/level or 1 round; see text
        // Saving Throw: Will partial
        // Spell Resistance: Yes
        //
        // An invisible cone of terror causes each living creature in
        // the area to become panicked unless it succeeds on a Will save.
        // If cornered, a panicked creature begins cowering.
        // If the Will save succeeds, the creature is shaken for 1 round.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.FEAR,
                    Name = "Fear",
                    Description = "Necromancy [Fear, Mind-Affecting]. An invisible cone of terror causes each living creature in a 30-ft cone to become panicked (Will partial). Failed save: panicked for 1 round/level (flee, drop items, -2 penalties). Successful save: shaken for 1 round (-2 penalties). Does not affect undead, constructs, or other non-living creatures. Components: V, S, M. PHB p.229",
                    SpellLevel = 4,
                    School = "Necromancy",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Bard", 3),
                        new SpellAvailability("Sorcerer", 4),
                        new SpellAvailability("Wizard", 4)
                    },
                    TargetType = SpellTargetType.Area,
                    RangeSquares = 6, // 30 ft = 6 squares
                    AoEShapeType = AoEShape.Cone,
                    AoESizeSquares = 6, // 30-ft cone = 6 squares length
                    AoERangeSquares = 0, // Cone originates from caster
                    AoEFilter = AoETargetFilter.All,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    IsMindAffecting = true,
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    BuffDurationRounds = 4, // Fallback; actual is caster level rounds
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.FOXS_CUNNING,
                    Name = "Fox's Cunning",
                    Description = "Subject gains +4 enhancement bonus to INT for 1 min/level. Affects Int-based skills and Wizard spell DCs. Does NOT grant bonus skill points or spells. PHB p.233",
                    SpellLevel = 2, School = "Transmutation",
                    ClassList = new[] { "Wizard", "Sorcerer", "Bard" },
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
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

    }
}
