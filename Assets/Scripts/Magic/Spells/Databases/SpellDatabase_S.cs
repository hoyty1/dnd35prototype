// ============================================================================
// SpellDatabase_S.cs — Spells starting with S
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

public static partial class SpellDatabase
{
    private static void RegisterSpellsS()
    {
        Register(new SpellData
                {
                    SpellId = SpellNames.SANCTUARY,
                    Name = "Sanctuary",
                    Description = "Opponents can't attack you unless they make a Will save. 1 round/level. PHB p.274",
                    SpellLevel = 1, School = "Abjuration",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    BuffDurationRounds = 3,
                    BuffType = SpellNames.SANCTUARY,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Attack prevention not fully implemented]"
                });

        // ──────────────────────────────────────────────────────────────
        // SCARE  (PHB p.274)
        // Necromancy [Fear, Mind-Affecting]
        // Level: Brd 2, Sor/Wiz 2
        // Components: V, S, M (a bit of bone from an undead skeleton, zombie,
        //   ghoul, ghast, or mummy)
        // Casting Time: 1 standard action
        // Range: Medium (100 ft. + 10 ft./level)
        // Targets: One living creature per three levels, no two of which can
        //   be more than 30 ft. apart
        // Duration: 1 round/level or 1 round; see text
        // Saving Throw: Will partial
        // Spell Resistance: Yes
        //
        // Functions like Cause Fear except it can target multiple creatures.
        // Creatures with 6+ HD are completely immune.
        // Failed save: Frightened for 1 round/level.
        // Successful save: Shaken for 1 round.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.SCARE,
                    Name = "Scare",
                    Description = "Necromancy [Fear, Mind-Affecting]. Causes living creatures of less than 6 HD to become frightened (Will partial). Failed save: frightened for 1 round/level (must flee, -2 penalties). Successful save: shaken for 1 round (-2 penalties). Targets 1 creature per 3 caster levels, no two more than 30 ft apart. Creatures with 6+ HD are immune. Components: V, S, M (bone from undead). PHB p.274",
                    SpellLevel = 2, School = "Necromancy",
                    ClassList = new[] { "Wizard", "Sorcerer", "Bard" },
                    TargetType = SpellTargetType.SingleEnemy, // Runtime handles multi-target
                    RangeCategory = SpellRangeCategory.Medium,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    BuffDurationRounds = 3, // Fallback; actual is caster level rounds
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.SCORCHING_RAY,
                    Name = "Scorching Ray",
                    Description = "Ranged touch attack, 4d6 fire damage per ray. 1 ray at CL3 (2 at CL7, 3 at CL11). PHB p.274",
                    SpellLevel = 2, School = "Evocation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    // Close range (25 ft + 5 ft/2 levels)
                    RangeCategory = SpellRangeCategory.Close,
                    IsTouch = true,
                    IsRangedTouch = true,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 6, DamageCount = 4, // 4d6 per ray
                    DamageType = "fire",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.SEE_INVISIBLE,
                    Name = "See Invisible",
                    Description = "Divination. Personal. You can see invisible creatures and objects normally. Negates invisibility miss chance and invisibility AC bonus/Hide bonus against you (but not mundane hiding). Duration 10 min/level, dismissible. Components: V, S, M (talc and powdered silver). PHB p.275",
                    SpellLevel = 2, School = "Divination",
                    ClassList = new[] { "Wizard", "Sorcerer" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffType = SpellNames.SEE_INVISIBLE,
                    DurationType = DurationType.Minutes,
                    DurationValue = 10,
                    DurationScalesWithLevel = true,
                    IsDismissible = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true
                });

        // ──────────────────────────────────────────────────────────────
        // SHATTER  (PHB p.278)
        // Evocation [Sonic]
        // Level: Brd 2, Clr 2, Chaos 2, Destruction 2, Sor/Wiz 2
        // Components: V, S, M/DF (a chip of mica)
        // Casting Time: 1 standard action
        // Range: Close (25 ft. + 5 ft./2 levels)
        // Target or Area: 5-ft.-radius spread; or one solid object or
        //   one crystalline creature
        // Duration: Instantaneous
        // Saving Throw: Will negates (object); Will negates (object) or
        //   Fortitude half; see text
        // Spell Resistance: Yes
        //
        // Area mode shatters nonmagical objects of brittle material in a
        // 5-ft. spread (Will negates per object, weight limit 1 lb/level).
        // Single-target mode: against a crystalline creature deals
        // 1d6 sonic damage per caster level (max 10d6), Fortitude half.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.SHATTER,
                    Name = "Shatter",
                    Description = "Sonic vibration damages objects or crystalline creatures. "
                        + "Area: shatters nonmagical objects of brittle material in a 5-ft. spread (Will negates per object, weight ≤1 lb/level). "
                        + "Single target: 1d6 sonic damage per caster level (max 10d6) to a crystalline creature (Fortitude half). "
                        + "Components: V, S, M/DF (a chip of mica). PHB p.278",
                    SpellLevel = 2, School = "Evocation [Sonic]",
                    ClassList = new[] { "Wizard", "Sorcerer", "Bard", "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Close,
                    AreaRadius = 1, // 5-ft.-radius spread (area mode)
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 6, DamageCount = 1, // 1d6 per caster level (scaled at cast time, max 10d6)
                    DamageType = "sonic",
                    AllowsSavingThrow = true,
                    SavingThrowType = "Fortitude", // Fortitude half vs crystalline creatures
                    SaveHalves = true,
                    SpellResistanceApplies = true,
                    DurationType = DurationType.Instantaneous,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.SHIELD,
                    Name = "Shield",
                    Description = "+4 shield bonus to AC, blocks Magic Missile. Duration 1 min/level. PHB p.278",
                    SpellLevel = 1, School = "Abjuration",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffShieldBonus = 4,
                    BuffDurationRounds = 30,
                    BuffType = SpellNames.SHIELD,
                    BuffBonusType = BonusType.Shield,
                    BonusTypeExplicitlySet = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.SHIELD_OF_FAITH,
                    Name = "Shield of Faith",
                    Description = "+2 deflection bonus to AC. Duration 1 min/level. PHB p.278",
                    SpellLevel = 1, School = "Abjuration",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDeflectionBonus = 2,
                    BuffDurationRounds = 30,
                    BuffType = "deflection",
                    BuffBonusType = BonusType.Deflection,
                    BonusTypeExplicitlySet = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.SHIELD_OTHER,
                    Name = "Shield Other",
                    Description = "+1 deflection AC and +1 resistance on saves. Caster takes half of subject's damage. 1 hr/level. PHB p.278",
                    SpellLevel = 2, School = "Abjuration",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Buff,
                    BuffDeflectionBonus = 1,
                    BuffSaveBonus = 1,
                    BuffDurationRounds = -1,
                    BuffType = SpellNames.SHIELD_OTHER,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.SHOCKING_GRASP,
                    Name = "Shocking Grasp",
                    Description = "Touch delivers 1d6/level electricity damage (max 5d6). +3 attack vs metal armor. PHB p.279",
                    SpellLevel = 1, School = "Evocation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    IsMeleeTouch = true,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 6, DamageCount = 3, // 3d6 at CL3
                    DamageType = "electricity",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.SILENCE,
                    Name = "Silence",
                    Description = "Negates sound in 20-ft radius. Prevents spellcasting with verbal components. 1 round/level. PHB p.279",
                    SpellLevel = 2, School = "Illusion",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy, // Can target creature or area
                    RangeCategory = SpellRangeCategory.Long,
                    AreaRadius = 4,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will", // If targeted on a creature
                    BuffDurationRounds = 3,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.SILENT_IMAGE,
                    Name = "Silent Image",
                    Description = "Creates minor illusion of your design. Concentration + 2 rounds. Will disbelief. PHB p.279",
                    SpellLevel = 1, School = "Illusion",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 8,
                    EffectType = SpellEffectType.Buff,
                    DurationType = DurationType.Concentration,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Illusion mechanics not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.SLEEP,
                    Name = "Sleep",
                    Description = "Enchantment (Compulsion) [Mind-Affecting]. A 10-ft radius burst affects creatures with lowest HD first from a 4d4 HD pool. Only creatures with 4 HD or less are affected. Will negates. Duration 1 min/level. SR: Yes. PHB p.280",
                    SpellLevel = 1,
                    School = "Enchantment",
                    ClassList = new[] { "Wizard", "Sorcerer" },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Medium,
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 2, // 10-ft radius
                    AoERangeSquares = 0, // use Medium range profile
                    AoEFilter = AoETargetFilter.All,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    IsMindAffecting = true,
                    BlockedByProtectionFromAlignment = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    BuffDurationRounds = 10, // legacy fallback; runtime uses duration system
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = false
                });

        // ──────────────────────────────────────────────────────────────
        // SLEET STORM  (PHB p.281)
        // Conjuration (Creation) [Cold]
        // Level: Drd 3, Sor/Wiz 3
        // Components: V, S, M/DF (a pinch of dust and a few drops of water)
        // Casting Time: 1 standard action
        // Range: Long (400 ft. + 40 ft./level)
        // Area: Cylinder (40-ft. radius, 20 ft. high)
        // Duration: 1 round/level
        // Saving Throw: None
        // Spell Resistance: No
        //
        // Driving sleet blocks all sight (including darkvision) within the area.
        // Icy ground requires DC 10 Balance check to move (fail by 5+ = fall prone).
        // Movement at half speed. Concentration DC 5 + spell level to cast.
        // Extinguishes torches and small fires.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.SLEET_STORM,
                    Name = "Sleet Storm",
                    Description = "Conjuration (Creation) [Cold]. Driving sleet blocks all sight (including darkvision) within a 40-ft radius cylinder. "
                        + "Icy ground: DC 10 Balance check to move at half speed; fail by 5+ = fall prone. "
                        + "Concentration DC 5 + spell level to cast inside. Extinguishes torches and small fires. "
                        + "No save. No SR. Components: V, S, M/DF. PHB p.281",
                    SpellLevel = 3, School = "Conjuration (Creation) [Cold]",
                    ClassList = new[] { "Wizard", "Sorcerer", "Druid" },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Long,
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 8, // 40-ft radius = 8 squares
                    AoERangeSquares = 0, // use Long range profile
                    AoEFilter = AoETargetFilter.All,
                    AreaRadius = 8, // 40-ft radius
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = false,
                    SavingThrowType = "None",
                    SpellResistanceApplies = false,
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        // ──────────────────────────────────────────────────────────────
        // SLOW  (PHB p.280)
        // Transmutation
        // Level: Brd 3, Sor/Wiz 3
        // Components: V, S, M (a drop of molasses)
        // Casting Time: 1 standard action
        // Range: Close (25 ft. + 5 ft./2 levels)
        // Targets: One creature/level, no two of which can be more than
        //          30 ft. apart
        // Duration: 1 round/level
        // Saving Throw: Will negates
        // Spell Resistance: Yes
        //
        // An affected creature moves and attacks at a drastically
        // slowed rate. A slowed creature can take only a single move
        // action or standard action each turn, but not both (nor may
        // it take full-round actions). Additionally, it takes a -1
        // penalty on attack rolls, AC, and Reflex saves. A slowed
        // creature moves at half its normal speed (round down to the
        // next 5-foot increment), which affects the creature's jumping
        // distance as normal for decreased speed.
        // Multiple slow effects don't stack. Slow counters and dispels
        // haste.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.SLOW,
                    Name = "Slow",
                    Description = "Transmutation. Affected creature moves and attacks at a drastically slowed rate. "
                        + "Can only take a single move action or standard action each turn (no full-round actions). "
                        + "-1 penalty on attack rolls, AC, and Reflex saves. "
                        + "Movement speed halved (round down to nearest 5 ft). "
                        + "Slow counters and dispels Haste. "
                        + "Duration 1 round/level. Will negates. SR: Yes. PHB p.280",
                    SpellLevel = 3,
                    School = "Transmutation",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Bard", 3),
                        new SpellAvailability("Sorcerer", 3),
                        new SpellAvailability("Wizard", 3)
                    },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        // ──────────────────────────────────────────────────────────────
        // STINKING CLOUD  (PHB p.284)
        // Conjuration (Creation)
        // Level: Sor/Wiz 3
        // Components: V, S, M (a rotten egg or several skunk cabbage leaves)
        // Casting Time: 1 standard action
        // Range: Medium (100 ft. + 10 ft./level)
        // Effect: Cloud spreads in 20-ft. radius, 20 ft. high
        // Duration: 1 round/level
        // Saving Throw: Fortitude negates; see text
        // Spell Resistance: No
        //
        // Creates bank of fog. Living creatures in cloud must Fort save each round
        // or become nauseated (can only take move action). Nausea persists 1d4+1
        // rounds after leaving cloud. Immune: undead, constructs, no breathe, poison immune.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.STINKING_CLOUD,
                    Name = "Stinking Cloud",
                    Description = "Conjuration (Creation). Creates a 20-ft radius bank of nauseating fog. "
                        + "Living creatures must Fort save each round or become nauseated (can only take move action). "
                        + "Nausea persists 1d4+1 rounds after leaving. Immune: undead, constructs, non-breathers, poison immune. "
                        + "Vision blocked like Fog Cloud. Components: V, S, M (rotten egg or skunk cabbage). PHB p.284",
                    SpellLevel = 3, School = "Conjuration (Creation)",
                    ClassList = new[] { "Wizard", "Sorcerer" },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Medium,
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 4, // 20-ft radius = 4 squares
                    AoERangeSquares = 0, // use Medium range profile
                    AoEFilter = AoETargetFilter.All,
                    AreaRadius = 4, // 20-ft radius
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Fortitude",
                    SpellResistanceApplies = false,
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.DOMAIN_SOFTEN_EARTH,
                    Name = "Soften Earth and Stone",
                    Description = "Turns stone to clay or dirt to sand/mud.",
                    SpellLevel = 2,
                    School = "Transmutation",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.Area,
                    RangeSquares = 5,
                    AreaRadius = 3,
                    EffectType = SpellEffectType.Debuff,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Terrain modification not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.SOUND_BURST,
                    Name = "Sound Burst",
                    Description = "Deals 1d8 sonic damage in 10-ft radius. Fortitude save or stunned for 1 round. PHB p.281",
                    SpellLevel = 2, School = "Evocation",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy, // Simplified from area
                    RangeCategory = SpellRangeCategory.Close,
                    AreaRadius = 2,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 8, DamageCount = 1,
                    DamageType = "sonic",
                    AllowsSavingThrow = true,
                    SavingThrowType = "Fortitude",
                    SaveHalves = false, // Stunned if failed, not half damage
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // ──────────────────────────────────────────────────────────────
        // SPECTRAL HAND  (PHB p.282)
        // Necromancy
        // Level: Sor/Wiz 2
        // Components: V, S
        // Casting Time: 1 standard action
        // Range: Medium (100 ft. + 10 ft./level)
        // Effect: One spectral hand
        // Duration: 1 min./level (D)
        // Saving Throw: None
        // Spell Resistance: No
        //
        // Creates ghostly hand from caster's life force. Caster loses 1d4 HP
        // (returned when spell ends, NOT if hand is destroyed). Hand delivers
        // touch spells of 4th level or lower at range with +2 melee touch attack.
        // Hand AC: 22 + Int modifier. Incorporeal. Improved Evasion. Cannot flank.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.SPECTRAL_HAND,
                    Name = "Spectral Hand",
                    Description = "Necromancy. Creates a ghostly glowing hand from your life force. "
                        + "You lose 1d4 HP (regained when spell ends, but NOT if hand is destroyed). "
                        + "Hand delivers touch spells of 4th level or lower at medium range. "
                        + "+2 bonus on melee touch attack rolls via hand. "
                        + "Hand HP: 1-4 (equal to HP lost). Hand AC: 22 + Int mod. "
                        + "Incorporeal, improved evasion, uses caster's saves. Cannot flank. "
                        + "Components: V, S. PHB p.282",
                    SpellLevel = 2, School = "Necromancy",
                    ClassList = new[] { "Wizard", "Sorcerer" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Medium,
                    EffectType = SpellEffectType.Buff,
                    BuffType = SpellNames.SPECTRAL_HAND,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    IsDismissible = true,
                    AllowsSavingThrow = false,
                    SpellResistanceApplies = false,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.SPIDER_CLIMB,
                    Name = "Spider Climb",
                    Description = "Grants ability to walk on walls and ceilings. Speed 20 ft. Duration 10 min/level. PHB p.283",
                    SpellLevel = 2, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    BuffType = SpellNames.SPIDER_CLIMB,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Wall climbing not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.SPIRITUAL_WEAPON,
                    Name = "Spiritual Weapon",
                    Description = "Magic weapon attacks on its own. 1d8 + 1/3CL force damage. Lasts 1 round/level. No AoO. PHB p.283",
                    SpellLevel = 2, School = "Evocation",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Medium,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 8, DamageCount = 1, BonusDamage = 1,
                    DamageType = "force",
                    AutoHit = false, // Uses caster's BAB + WIS mod for attack
                    BuffDurationRounds = 3,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = false // Does not provoke
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.STONE_TO_FLESH,
                    Name = "Stone to Flesh",
                    Description = "Restores petrified creature to normal flesh. PHB p.284",
                    SpellLevel = 6, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Medium,
                    EffectType = SpellEffectType.Healing,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.STATUS,
                    Name = "Status",
                    Description = "Monitors condition and position of allies. Duration 1 hr/level. PHB p.284",
                    SpellLevel = 2, School = "Divination",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Ally monitoring not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.SUMMON_MONSTER_1,
                    Name = "Summon Monster I",
                    Description = "Calls a creature to fight for you. Duration 1 round/level. PHB p.285",
                    SpellLevel = 1, School = "Conjuration",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 5,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = 3,
                    ActionType = SpellActionType.FullRound,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Summoning not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.SUMMON_MONSTER_2,
                    Name = "Summon Monster II",
                    Description = "Calls creature to fight for you. Duration 1 round/level. PHB p.286",
                    SpellLevel = 2, School = "Conjuration",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 5,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = 3,
                    ActionType = SpellActionType.FullRound,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Summoning not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.SUMMON_SWARM,
                    Name = "Summon Swarm",
                    Description = "You summon a swarm of bats, rats, or spiders (your choice). The swarm is uncontrolled and attacks the nearest living creature, friend or foe. Duration: concentration + 2 rounds.",
                    SpellLevel = 2,
                    School = "Conjuration (Summoning)",
                    ClassList = new[] { "Wizard", "Sorcerer", "Druid", "Bard" },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Buff,
                    DurationType = DurationType.Concentration,
                    DurationValue = 1,
                    DurationScalesWithLevel = false,
                    ActionType = SpellActionType.FullRound,
                    ProvokesAoO = true,
                    AllowsSavingThrow = false,
                    SavingThrowType = "None",
                    SpellResistanceApplies = false,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        // ──────────────────────────────────────────────────────────────
        // SHOUT  (PHB p.275)
        // Evocation [Sonic]
        // Level: Brd 4, Sor/Wiz 4
        // Components: V
        // Casting Time: 1 standard action
        // Range: 30 ft.
        // Area: Cone-shaped burst
        // Duration: Instantaneous
        // Saving Throw: Fortitude partial (see text)
        // Spell Resistance: Yes
        //
        // You emit an ear-splitting yell that deafens and damages creatures
        // in its path. Any creature within the area is deafened for 2d6
        // rounds and takes 5d6 points of sonic damage. A successful save
        // negates the deafness and reduces the damage by half.
        // Any exposed brittle or crystalline object or crystalline creature
        // takes 1d6 points of sonic damage per caster level (max 15d6).
        // An affected creature is allowed a Fortitude save to reduce damage
        // by half. Creatures holding such objects can negate damage by
        // making a Reflex save.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.SHOUT,
                    Name = "Shout",
                    Description = "Evocation [Sonic]. You emit an ear-splitting yell in a 30-ft cone. 5d6 sonic damage (Fortitude half). Failed save: deafened for 2d6 rounds. SR: Yes. PHB p.275",
                    SpellLevel = 4,
                    School = "Evocation [Sonic]",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Bard", 4),
                        new SpellAvailability("Sorcerer", 4),
                        new SpellAvailability("Wizard", 4)
                    },
                    TargetType = SpellTargetType.Area,
                    RangeSquares = 6, // 30 ft = 6 squares
                    AoEShapeType = AoEShape.Cone,
                    AoESizeSquares = 6, // 30-ft cone
                    AoERangeSquares = 0, // originates from caster
                    AoEFilter = AoETargetFilter.All,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 6,
                    DamageCount = 5, // 5d6 sonic
                    DamageType = "sonic",
                    AllowsSavingThrow = true,
                    SavingThrowType = "Fortitude",
                    SaveHalves = true,
                    SpellResistanceApplies = true,
                    DurationType = DurationType.Instantaneous,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = false,
                    IsPlaceholder = false
                });

        // Aliases
        RegisterAlias(SpellNames.SEE_INVISIBILITY_LEGACY, SpellNames.SEE_INVISIBLE);
        RegisterClassSpellAlias("see_invisible_brd", SpellNames.SEE_INVISIBLE, "Bard", 3);
        RegisterClassSpellAlias("see_invisibility_brd", SpellNames.SEE_INVISIBLE, "Bard", 3);
        RegisterClassSpellAlias("summon_monster_1_clr", SpellNames.SUMMON_MONSTER_1, "Cleric", 1);
        RegisterClassSpellAlias("summon_monster_2_clr", SpellNames.SUMMON_MONSTER_2, "Cleric", 2);

    }
}
