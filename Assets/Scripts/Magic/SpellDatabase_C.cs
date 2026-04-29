// ============================================================================
// SpellDatabase_C.cs — Spells starting with C
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterSpellsC()
    {
        Register(new SpellData
                {
                    SpellId = "domain_calm_animals",
                    Name = "Calm Animals",
                    Description = "Calms 2d4+level HD of animals, rendering them docile and harmless.",
                    SpellLevel = 1,
                    School = "Enchantment",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.Area,
                    RangeSquares = 5,
                    AreaRadius = 2,
                    EffectType = SpellEffectType.Debuff,
                    BuffDurationRounds = 30,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Animal calming not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "calm_emotions",
                    Name = "Calm Emotions",
                    Description = "Calms creatures in 20-ft radius, suppressing morale bonuses and emotion effects. Will negates. Concentration + 1 round/level. PHB p.207",
                    SpellLevel = 2, School = "Enchantment",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeSquares = 22,
                    AreaRadius = 4,
                    EffectType = SpellEffectType.Debuff,
                    DurationType = DurationType.Concentration,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Emotion suppression not fully implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "cats_grace",
                    Name = "Cat's Grace",
                    Description = "Subject gains +4 enhancement bonus to DEX for 1 min/level. PHB p.208",
                    SpellLevel = 2, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
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
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "cause_fear",
                    Name = "Cause Fear",
                    Description = "Necromancy [Fear, Mind-Affecting]. One living creature of 5 HD or less becomes frightened for 1d4 rounds; a successful Will save leaves it shaken for 1 round. SR: Yes. PHB p.208",
                    SpellLevel = 1, School = "Necromancy",
                    ClassList = new[] { "Wizard", "Sorcerer" },
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
                    SpellId = "charm_person",
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
                    SpellId = "chill_touch",
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
                    SpellId = "chromatic_orb",
                    Name = "Chromatic Orb",
                    Description = "Ranged touch attack deals 1d8 damage (type varies by caster level). At CL3: fire, 1d8.",
                    SpellLevel = 1, School = "Evocation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeSquares = 5,
                    IsTouch = true,
                    IsRangedTouch = true,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 8, DamageCount = 1,
                    DamageType = "fire",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "color_spray",
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
                    SpellId = "command",
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

        Register(new SpellData
                {
                    SpellId = "command_undead",
                    Name = "Command Undead",
                    Description = "Undead creature obeys your commands. Will save for intelligent undead. 1 day/level. PHB p.211",
                    SpellLevel = 2, School = "Necromancy",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeSquares = 5,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Undead control not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "comprehend_languages",
                    Name = "Comprehend Languages",
                    Description = "You understand all spoken and written languages. Duration 10 min/level. PHB p.212",
                    SpellLevel = 1, School = "Divination",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Language mechanics not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "consecrate",
                    Name = "Consecrate",
                    Description = "Fills area with positive energy. Undead suffer penalties. 20-ft radius. 2 hr/level. PHB p.212",
                    SpellLevel = 2, School = "Evocation",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 5,
                    AreaRadius = 4,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Area consecration not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "confusion",
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
                    SpellId = "continual_flame",
                    Name = "Continual Flame",
                    Description = "Makes a permanent, heatless flame. Requires 50 gp ruby dust. PHB p.213",
                    SpellLevel = 2, School = "Evocation",
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
                    SpellId = "create_water",
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
                    SpellId = "cure_light_wounds",
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
                    SpellId = "cure_minor_wounds",
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
                    SpellId = "cure_moderate_wounds",
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

        // Aliases
        RegisterClassSpellAlias("cause_fear_clr", "cause_fear", "Cleric", 1);
        RegisterClassSpellAlias("comprehend_languages_clr", "comprehend_languages", "Cleric", 1);

    }
}
