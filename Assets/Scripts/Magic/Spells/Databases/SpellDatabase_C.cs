// ============================================================================
// SpellDatabase_C.cs — Spells starting with C
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

public static partial class SpellDatabase
{
    private static void RegisterSpellsC()
    {
        // ──────────────────────────────────────────────────────────────
        // CALM ANIMALS  (PHB p.207)
        // Enchantment (Compulsion) [Mind-Affecting]
        // Level: Animal 1, Druid 1, Ranger 1
        // Casting Time: 1 standard action
        // Range: Close (25 ft. + 5 ft./2 levels)
        // Targets: Animals within 30 ft. of each other
        // Duration: 1 min/level
        // Saving Throw: Will negates
        // Spell Resistance: Yes
        //
        // Calms 2d4 + caster level HD of animals, rendering them docile.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.DOMAIN_CALM_ANIMALS,
                    Name = "Calm Animals",
                    Description = "Calms 2d4+CL HD of animals, rendering them docile and harmless. Will negates. Breaks if threatened. PHB p.207",
                    SpellLevel = 1,
                    School = "Enchantment",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Close,
                    AreaRadius = 2,
                    EffectType = SpellEffectType.Debuff,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // ──────────────────────────────────────────────────────────────
        // CALM EMOTIONS  (PHB p.207)
        // Enchantment (Compulsion) [Mind-Affecting]
        // Level: Brd 2, Clr 2, Law 2
        // Casting Time: 1 standard action
        // Range: Medium (100 ft. + 10 ft./level)
        // Area: Creatures in 20-ft.-radius spread
        // Duration: Concentration, up to 1 round/level (D)
        // Saving Throw: Will negates
        // Spell Resistance: Yes
        //
        // Suppresses morale bonuses, rage, fear, and confusion effects.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.CALM_EMOTIONS,
                    Name = "Calm Emotions",
                    Description = "Suppresses morale bonuses, rage, fear, and confusion in 20-ft radius. Will negates. Concentration + 1 round/level. PHB p.207",
                    SpellLevel = 2, School = "Enchantment",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Medium,
                    AreaRadius = 4,
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 4,
                    EffectType = SpellEffectType.Debuff,
                    DurationType = DurationType.Concentration,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.CATS_GRACE,
                    Name = "Cat's Grace",
                    Description = "Subject gains +4 enhancement bonus to DEX for 1 min/level. Affects AC, Reflex saves, initiative, and Dex-based skills. PHB p.208",
                    SpellLevel = 2, School = "Transmutation",
                    ClassList = new[] { "Wizard", "Sorcerer", "Bard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffStatName = "DEX",
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

        Register(new SpellData
                {
                    SpellId = SpellNames.CAUSE_FEAR,
                    Name = "Cause Fear",
                    Description = "Necromancy [Fear, Mind-Affecting]. One living creature of 5 HD or less becomes frightened for 1d4 rounds; a successful Will save leaves it shaken for 1 round. SR: Yes. PHB p.208",
                    SpellLevel = 1, School = "Necromancy",
                    ClassList = new[] { "Wizard", "Sorcerer", "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Debuff,
                    IsMindAffecting = true,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    BuffDurationRounds = 2,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.CHARM_PERSON,
                    Name = "Charm Person",
                    Description = "Enchantment (Charm) [Mind-Affecting]. One humanoid creature of 4 HD or less regards you as a trusted ally. Will negates. Duration 1 hour/level. SR: Yes. PHB p.209",
                    SpellLevel = 1,
                    School = "Enchantment",
                    ClassList = new[] { "Wizard", "Sorcerer", "Bard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Debuff,
                    IsMindAffecting = true,
                    BlockedByProtectionFromAlignment = true,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    DurationType = DurationType.Hours,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    // Legacy fallback value for systems still reading BuffDurationRounds directly.
                    BuffDurationRounds = 600,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.CHILL_TOUCH,
                    Name = "Chill Touch",
                    Description = "1 touch/level, each dealing 1d6 negative energy damage and 1 STR damage. Fort save negates STR damage.",
                    SpellLevel = 1, School = "Necromancy",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    IsMeleeTouch = true,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 6, DamageCount = 1,
                    DamageType = "negative",
                    AllowsSavingThrow = true,
                    SavingThrowType = "Fortitude",
                    SaveHalves = false, // Save negates STR damage only
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.COLOR_SPRAY,
                    Name = "Color Spray",
                    Description = "Illusion (Pattern) [Mind-Affecting]. Creatures in a 15-ft cone are stunned, blinded, and possibly knocked unconscious based on HD. Will negates. SR: Yes. Duration special (cascading by HD). PHB p.210",
                    SpellLevel = 1,
                    School = "Illusion",
                    ClassList = new[] { "Wizard", "Sorcerer" },
                    TargetType = SpellTargetType.Area,
                    RangeSquares = 3,
                    AoEShapeType = AoEShape.Cone,
                    AoESizeSquares = 3, // 15-ft cone
                    AoERangeSquares = 0, // Cone originates from caster
                    AoEFilter = AoETargetFilter.All,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    IsMindAffecting = true,
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    BuffDurationRounds = 1, // legacy fallback; runtime uses staged duration data
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = false
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.COMMAND,
                    Name = "Command",
                    Description = "One subject obeys selected command for 1 round: approach, drop, fall, flee, halt. Will negates. PHB p.211",
                    SpellLevel = 1, School = "Enchantment",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    BuffDurationRounds = 1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // ============================================================
        // Command Undead — Necromancy, Sor/Wiz 2
        // PHB p.211. Grants control over one undead creature.
        // V, S, M (raw meat + bone splinter). Range: Close.
        // Duration: 1 day/level. SR: Yes.
        // Save: Will negates (intelligent undead only).
        // Unintelligent undead get no save and obey all commands.
        // Intelligent undead: Friendly attitude, opposed CHA for
        //   unusual orders, never obey suicidal orders.
        // Threatening acts by caster/allies break control.
        // ============================================================
        Register(new SpellData
                {
                    SpellId = SpellNames.COMMAND_UNDEAD,
                    Name = "Command Undead",
                    Description = "Necromancy. One undead creature obeys your commands. Nonintelligent undead get no save and obey all orders including suicidal ones. Intelligent undead receive a Will save; if failed, they perceive you as Friendly and won't attack, but refuse suicidal orders and require opposed CHA checks for unusual orders. Threatening acts break control. V, S, M (raw meat and bone splinter). Duration 1 day/level. SR: Yes. PHB p.211",
                    SpellLevel = 2, School = "Necromancy",
                    ClassList = new[] { "Wizard", "Sorcerer" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true, // Will negates for intelligent undead; runtime skips save for nonintelligent
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    DurationType = DurationType.Days,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    BuffDurationRounds = -1, // Special: duration handled by CommandUndeadEffectData
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = false
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.COMPREHEND_LANGUAGES,
                    Name = "Comprehend Languages",
                    Description = "You understand all spoken and written languages. Duration 10 min/level. PHB p.212",
                    SpellLevel = 1, School = "Divination",
                    ClassList = new[] { "Wizard", "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Language mechanics not implemented]"
                });

        // ===== CONSECRATE — PHB p.212 =====
        // Evocation [Good]. Cleric 2. V, S, M (vial of holy water), DF.
        // Casting Time: 1 standard action. Range: Close (25 ft + 5 ft/2 levels).
        // Area: 20-ft-radius emanation. Duration: 2 hr/level.
        // Undead in area take -1 profane penalty on attack, damage, and saves.
        // Turning check in area gets +3 sacred bonus.
        // If area contains altar/shrine of caster's deity, bonuses double.
        Register(new SpellData
                {
                    SpellId = SpellNames.CONSECRATE,
                    Name = "Consecrate",
                    Description = "Fills area with positive energy. Undead suffer -1 on attacks, damage, and saves. +3 sacred bonus to turning checks. 20-ft radius. 2 hr/level. PHB p.212",
                    SpellLevel = 2, School = "Evocation",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Close,
                    RangeSquares = 5,
                    AreaRadius = 4,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1, // 2 hr/level, effectively unlimited in combat
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.CONFUSION,
                    Name = "Confusion",
                    Description = "Targets behave unpredictably for 1 round/level. Will negates. PHB p.212",
                    SpellLevel = 4, School = "Enchantment",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Medium,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    BuffDurationRounds = 4,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.CONTINUAL_FLAME,
                    Name = "Continual Flame",
                    Description = "Evocation [Light]. A flame, equivalent in brightness to a torch, springs forth from an object. "
                        + "The effect looks like a regular flame, but creates no heat and doesn't use oxygen. "
                        + "Continual Flame can be covered and hidden but not smothered or quenched. "
                        + "Material component: ruby dust worth 50 gp. Duration: Permanent. PHB p.213",
                    SpellLevel = 2, School = "Evocation [Light]",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Cleric", 3),
                        new SpellAvailability("Sorcerer", 2),
                        new SpellAvailability("Wizard", 2)
                    },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Utility,
                    DurationType = DurationType.Permanent,
                    BuffDurationRounds = -1,
                    HasMaterialComponent = true, // 50 gp ruby dust
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = false
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.CREATE_WATER,
                    Name = "Create Water",
                    Description = "Creates 2 gallons/level of pure water.",
                    SpellLevel = 0, School = "Conjuration",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 5,
                    EffectType = SpellEffectType.Buff,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Water creation not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.CURE_LIGHT_WOUNDS,
                    Name = "Cure Light Wounds",
                    Description = "Heals 1d8 + caster level (max +5) HP. PHB p.215",
                    SpellLevel = 1, School = "Conjuration",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    IsMeleeTouch = true,
                    EffectType = SpellEffectType.Healing,
                    HealDice = 8, HealCount = 1, BonusHealing = 3, // +CL (3 at CL3, max +5)
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.CURE_MINOR_WOUNDS,
                    Name = "Cure Minor Wounds",
                    Description = "Cures 1 point of damage. Touch range.",
                    SpellLevel = 0, School = "Conjuration",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Healing,
                    HealDice = 0, HealCount = 0, BonusHealing = 1, // Fixed 1 HP
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.CURE_MODERATE_WOUNDS,
                    Name = "Cure Moderate Wounds",
                    Description = "Heals 2d8 + CL (max +10) HP. Touch range. PHB p.216",
                    SpellLevel = 2, School = "Conjuration",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    IsMeleeTouch = true,
                    EffectType = SpellEffectType.Healing,
                    HealDice = 8, HealCount = 2, BonusHealing = 3, // +CL
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.CONTAGION,
                    Name = "Contagion",
                    Description = "Melee touch attack infects the target with a chosen disease. Disease takes effect immediately (no incubation). Fortitude negates. PHB p.213",
                    SpellLevel = 3, School = "Necromancy",
                    ClassList = new[] { "Cleric", "Druid" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    IsMeleeTouch = true,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Fortitude",
                    SpellResistanceApplies = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // Aliases
        RegisterClassSpellAlias("cause_fear_clr", SpellNames.CAUSE_FEAR, "Cleric", 1);
        RegisterClassSpellAlias("comprehend_languages_clr", SpellNames.COMPREHEND_LANGUAGES, "Cleric", 1);

        // Aliases — Cat's Grace: Druid 2, Ranger 2 (NOT Cleric)
        RegisterClassSpellAlias("cats_grace_drd", SpellNames.CATS_GRACE, "Druid", 2);
        RegisterClassSpellAlias("cats_grace_rgr", SpellNames.CATS_GRACE, "Ranger", 2);

        // Aliases — Contagion: Sor/Wiz 4 (base is Clr 3/Dru 3)
        RegisterClassSpellAlias("contagion_wiz", SpellNames.CONTAGION, "Wizard", 4);
        RegisterClassSpellAlias("contagion_sor", SpellNames.CONTAGION, "Sorcerer", 4);

        // Aliases — Continual Flame: Clr 3 (base is Wiz 2)
        RegisterClassSpellAlias("continual_flame_clr", SpellNames.CONTINUAL_FLAME, "Cleric", 3);

        // ═══════════════════════════════════════════════════════════════
        // Cure Serious Wounds — PHB p.216
        // School: Conjuration (Healing)
        // Level: Cleric 3, Druid 4, Paladin 4, Ranger 4
        // Components: V, S
        // Casting Time: 1 standard action
        // Range: Touch
        // Target: Creature touched
        // Duration: Instantaneous
        // Saving Throw: Will half (harmless); see text
        // Spell Resistance: Yes (harmless); see text
        // ═══════════════════════════════════════════════════════════════
        Register(new SpellData
                {
                    SpellId = SpellNames.CURE_SERIOUS_WOUNDS,
                    Name = "Cure Serious Wounds",
                    Description = "Heals 3d8 + CL (max +15) HP. Touch range. PHB p.216",
                    SpellLevel = 3, School = "Conjuration",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    IsMeleeTouch = true,
                    EffectType = SpellEffectType.Healing,
                    HealDice = 8, HealCount = 3, BonusHealing = 5, // +CL (max +15 at CL15, using 5 for CL5)
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // ═══════════════════════════════════════════════════════════════
        // Cure Critical Wounds — PHB p.215
        // School: Conjuration (Healing)
        // Level: Cleric 4, Druid 5, Bard 4
        // Range: Touch
        // Duration: Instantaneous
        // Saving Throw: Will half (harmless)
        // Spell Resistance: Yes (harmless)
        // ═══════════════════════════════════════════════════════════════
        Register(new SpellData
                {
                    SpellId = SpellNames.CURE_CRITICAL_WOUNDS,
                    Name = "Cure Critical Wounds",
                    Description = "Heals 4d8 + CL (max +20) HP. Touch range. PHB p.215",
                    SpellLevel = 4, School = "Conjuration",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    IsMeleeTouch = true,
                    EffectType = SpellEffectType.Healing,
                    HealDice = 8, HealCount = 4, BonusHealing = 7, // +CL (max +20)
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // ═══════════════════════════════════════════════════════════════
        // Chaos Hammer — PHB p.208
        // School: Evocation [Chaotic]
        // Level: Cleric 4 (Chaos domain 4)
        // Range: Medium
        // Area: 20-ft-radius burst
        // Duration: Instantaneous (1d6 rounds for slow)
        // Saving Throw: Will partial
        // Spell Resistance: Yes
        // ═══════════════════════════════════════════════════════════════
        Register(new SpellData
                {
                    SpellId = SpellNames.CHAOS_HAMMER,
                    Name = "Chaos Hammer",
                    Description = "Burst of chaotic power: 1d8/2 CL (max 5d8) vs lawful creatures + slowed 1d6 rounds. Will half damage and negates slow. PHB p.208",
                    SpellLevel = 4, School = "Evocation",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Medium,
                    EffectType = SpellEffectType.Damage,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SaveHalves = true,
                    SpellResistanceApplies = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // ──────────────────────────────────────────────────────────────
        // COMMAND PLANTS  (PHB p.211)
        // Transmutation
        // Level: Druid 4, Plant 4, Ranger 3
        // Casting Time: 1 standard action
        // Range: Close (25 ft. + 5 ft./2 levels)
        // Targets: Up to 2 HD/level of plant creatures
        // Duration: 1 day/level
        // Saving Throw: Will negates
        // Spell Resistance: Yes
        //
        // You command plant creatures (not ordinary plants) to do your
        // bidding.  You can command 2 HD per caster level.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.COMMAND_PLANTS,
                    Name = "Command Plants",
                    Description = "Command up to 2 HD/level of plant creatures. Will negates. Duration 1 day/level. PHB p.211",
                    SpellLevel = 4,
                    School = "Transmutation",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Debuff,
                    DurationType = DurationType.Days,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // ── Cone of Cold (PHB p.212) ──────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.CONE_OF_COLD,
                    Name = "Cone of Cold",
                    Description = "Evocation [Cold]. Cone of Cold creates an area of extreme cold, originating at your hand "
                        + "and extending outward in a cone. It drains heat, dealing 1d6 points of cold damage per caster level "
                        + "(maximum 15d6). Reflex half. SR: Yes. Components: V, S, M (crystal or glass cone). PHB p.212",
                    SpellLevel = 5,
                    School = "Evocation [Cold]",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Sorcerer", 5),
                        new SpellAvailability("Wizard", 5)
                    },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Close,
                    AoEShapeType = AoEShape.Cone,
                    AoESizeSquares = 12, // 60-ft cone = 12 squares
                    AoERangeSquares = 0, // Cone originates from caster
                    AoEFilter = AoETargetFilter.All,
                    AreaRadius = 12,
                    RangeSquares = 12,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 6,
                    DamageCount = 1, // Actual: min(CL, 15) d6 resolved at cast time
                    DamageType = "cold",
                    AllowsSavingThrow = true,
                    SavingThrowType = "Reflex",
                    SaveHalves = true,
                    SpellResistanceApplies = true,
                    HasMaterialComponent = true,
                    DurationType = DurationType.Instantaneous,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        // ── Charm Monster (PHB p.209) ─────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.CHARM_MONSTER,
                    Name = "Charm Monster",
                    Description = "Enchantment (Charm) [Mind-Affecting]. As Charm Person, except that it affects any living creature. "
                        + "The target regards you as its trusted friend and ally. Will negates. SR: Yes. "
                        + "Duration 1 day/level. PHB p.209",
                    SpellLevel = 4,
                    School = "Enchantment",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Bard", 3),
                        new SpellAvailability("Sorcerer", 4),
                        new SpellAvailability("Wizard", 4)
                    },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Debuff,
                    IsMindAffecting = true,
                    BlockedByProtectionFromAlignment = true,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    DurationType = DurationType.Days,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    BuffDurationRounds = 1440, // Legacy fallback: 1 day/level in rounds
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        // ── Chain Lightning (PHB p.208) ───────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.CHAIN_LIGHTNING,
                    Name = "Chain Lightning",
                    Description = "Evocation [Electricity]. You create a bolt of lightning that arcs to secondary targets. "
                        + "Primary target takes 1d6/caster level (max 20d6) electricity damage. Secondary targets "
                        + "(one per caster level, max 20) each take half as much damage. Reflex half for both primary "
                        + "and secondary. SR: Yes. PHB p.208",
                    SpellLevel = 6,
                    School = "Evocation [Electricity]",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Sorcerer", 6),
                        new SpellAvailability("Wizard", 6)
                    },
                    TargetType = SpellTargetType.Area, // Multi-target, starts with primary
                    RangeCategory = SpellRangeCategory.Long,
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 6, // Secondary targets within 30 ft of primary
                    AoERangeSquares = 0,
                    AoEFilter = AoETargetFilter.EnemiesOnly,
                    AreaRadius = 6,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 6,
                    DamageCount = 1, // Actual: min(CL, 20) d6 for primary, half for secondary
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
                    HasMaterialComponent = true,
                    IsPlaceholder = false
                });

        // ── Crushing Despair (PHB p.215) ──────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.CRUSHING_DESPAIR,
                    Name = "Crushing Despair",
                    Description = "Enchantment (Compulsion) [Mind-Affecting]. An invisible cone of despair causes creatures "
                        + "to take a -2 penalty on attack rolls, saving throws, ability checks, skill checks, and weapon "
                        + "damage rolls. Will negates. SR: Yes. Duration 1 min/level. PHB p.215",
                    SpellLevel = 4,
                    School = "Enchantment",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Bard", 3),
                        new SpellAvailability("Sorcerer", 4),
                        new SpellAvailability("Wizard", 4)
                    },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Close,
                    AoEShapeType = AoEShape.Cone,
                    AoESizeSquares = 6, // 30-ft cone
                    AoERangeSquares = 0,
                    AoEFilter = AoETargetFilter.EnemiesOnly,
                    AreaRadius = 6,
                    RangeSquares = 6,
                    EffectType = SpellEffectType.Debuff,
                    IsMindAffecting = true,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    BuffDurationRounds = 10, // Legacy fallback: 1 min/level = 10 rounds/level
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        // ── Circle of Death (PHB p.210) ───────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.CIRCLE_OF_DEATH,
                    Name = "Circle of Death",
                    Description = "Necromancy [Death]. A circle of negative energy snuffs out the life force of living creatures, "
                        + "killing 1d4 HD worth of creatures per caster level (max 20d4) starting from lowest HD. "
                        + "40-ft-radius burst. Fort negates. SR: Yes. PHB p.210",
                    SpellLevel = 6,
                    School = "Necromancy [Death]",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Sorcerer", 6),
                        new SpellAvailability("Wizard", 6)
                    },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Medium,
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 8, // 40-ft radius = 8 squares
                    AoERangeSquares = 0,
                    AoEFilter = AoETargetFilter.EnemiesOnly,
                    AreaRadius = 8,
                    EffectType = SpellEffectType.Damage,
                    DamageType = "negative_energy",
                    AllowsSavingThrow = true,
                    SavingThrowType = "Fortitude",
                    SpellResistanceApplies = true,
                    HasMaterialComponent = true, // M: crushed black pearl worth 500 gp
                    DurationType = DurationType.Instantaneous,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        // Aliases — Continual Flame for Cleric
        RegisterClassSpellAlias("continual_flame_clr", SpellNames.CONTINUAL_FLAME, "Cleric", 3);
    }
}
