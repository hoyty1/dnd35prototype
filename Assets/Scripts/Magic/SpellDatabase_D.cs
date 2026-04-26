// ============================================================================
// SpellDatabase_D.cs — Spells starting with D
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterSpellsD()
    {
        Register(new SpellData
                {
                    SpellId = "dancing_lights",
                    Name = "Dancing Lights",
                    Description = "Creates up to four lights that move as you direct. Lasts 1 minute.",
                    SpellLevel = 0, School = "Evocation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 20,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = 10,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Light/illumination not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "darkvision",
                    Name = "Darkvision",
                    Description = "See 60 ft in total darkness. Duration 1 hr/level. PHB p.216",
                    SpellLevel = 2, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Vision/darkness not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "daze",
                    Name = "Daze",
                    Description = "Enchantment (Compulsion) [Mind-Affecting]. One humanoid creature of 4 HD or less is dazed for 1 round. Will negates. SR applies.",
                    SpellLevel = 0, School = "Enchantment",
                    ClassList = new[] { "Wizard", "Sorcerer", "Bard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    // Close range (25 ft + 5 ft/2 levels)
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    IsMindAffecting = true,
                    BuffDurationRounds = 1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "daze_monster",
                    Name = "Daze Monster",
                    Description = "Living creature of 6 HD or less loses next action. Will save negates. PHB p.217",
                    SpellLevel = 2, School = "Enchantment",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Medium,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    BuffDurationRounds = 1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "death_knell",
                    Name = "Death Knell",
                    Description = "Kills dying creature, caster gains 1d8 temp HP, +2 STR, +1 CL. Touch range. Will negates. PHB p.217",
                    SpellLevel = 2, School = "Necromancy",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 0, DamageCount = 0, BonusDamage = 10, // kills dying creature
                    DamageType = "negative",
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SaveHalves = false,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "deathwatch",
                    Name = "Deathwatch",
                    Description = "Reveals how near death subjects within 30 ft are. Duration 10 min/level. PHB p.217",
                    SpellLevel = 1, School = "Necromancy",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - HP reveal not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "delay_poison",
                    Name = "Delay Poison",
                    Description = "Stops poison from harming subject for 1 hr/level. PHB p.217",
                    SpellLevel = 2, School = "Conjuration",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Poison mechanics not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "domain_desecrate",
                    Name = "Desecrate",
                    Description = "Fills area with negative energy, making undead stronger. Undead in the area gain +1 profane bonus on attack rolls, damage rolls, and saving throws.",
                    SpellLevel = 2,
                    School = "Evocation",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.Area,
                    RangeSquares = 4,
                    AreaRadius = 4,
                    EffectType = SpellEffectType.Debuff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Area desecration not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "detect_evil",
                    Name = "Detect Evil",
                    Description = "Reveals evil creatures, spells, or objects. Concentration, up to 10 min/level. PHB p.218",
                    SpellLevel = 1, School = "Divination",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    DurationType = DurationType.Concentration,
                    DurationValue = 10,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Alignment detection not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "detect_magic_wiz",
                    Name = "Detect Magic",
                    Description = "Detects spells and magic items within 60 ft cone. Concentration, up to 1 min/level.",
                    SpellLevel = 0, School = "Divination",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    DurationType = DurationType.Concentration,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Detection mechanics not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "detect_poison_wiz",
                    Name = "Detect Poison",
                    Description = "Detects poison in one creature or small object.",
                    SpellLevel = 0, School = "Divination",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeSquares = 5,
                    EffectType = SpellEffectType.Buff, // detection
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Poison detection not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "domain_detect_secret_doors",
                    Name = "Detect Secret Doors",
                    Description = "Reveals secret doors within 60 ft cone.",
                    SpellLevel = 1,
                    School = "Divination",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = 30,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Secret door detection not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "detect_thoughts",
                    Name = "Detect Thoughts",
                    Description = "Allows listening to surface thoughts. Concentration, up to 1 min/level. Will negates. PHB p.220",
                    SpellLevel = 2, School = "Divination",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 12,
                    EffectType = SpellEffectType.Buff,
                    DurationType = DurationType.Concentration,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Mind reading not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "detect_undead",
                    Name = "Detect Undead",
                    Description = "Reveals undead within 60 ft. Concentration, up to 1 min/level. PHB p.220",
                    SpellLevel = 1, School = "Divination",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    DurationType = DurationType.Concentration,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Undead detection not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "disguise_self",
                    Name = "Disguise Self",
                    Description = "Changes your appearance. Duration 10 min/level. Will disbelief (if interacted). PHB p.222",
                    SpellLevel = 1, School = "Illusion",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Disguise not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "disrupt_undead",
                    Name = "Disrupt Undead",
                    Description = "You fire a ray of positive energy at one undead creature. Make a ranged touch attack; on a hit it deals 1d6 positive damage. This spell has no effect on living creatures.",
                    SpellLevel = 0, School = "Necromancy",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Close,
                    IsTouch = true,
                    IsRangedTouch = true,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 6, DamageCount = 1,
                    DamageType = "positive",
                    SpellResistanceApplies = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "divine_favor",
                    Name = "Divine Favor",
                    Description = "+1 luck bonus on attack and damage rolls (per 3 CL, max +3). Duration 1 minute. PHB p.224",
                    SpellLevel = 1, School = "Evocation",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffAttackBonus = 1,
                    BuffDamageBonus = 1,
                    BuffDurationRounds = 10,
                    BuffType = "luck",
                    BuffBonusType = BonusType.Luck,
                    BonusTypeExplicitlySet = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = false, // Fixed 1 minute
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "doom",
                    Name = "Doom",
                    Description = "One subject is shaken (–2 on attack, saves, skills, ability checks). Will save negates. 1 min/level. PHB p.225",
                    SpellLevel = 1, School = "Necromancy",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Medium,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    BuffAttackBonus = -2,
                    BuffSaveBonus = -2,
                    BuffDurationRounds = 30,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // Aliases
        RegisterClassSpellAlias("detect_magic_clr", "detect_magic_wiz", "Cleric", 0);
        RegisterClassSpellAlias("detect_poison_clr", "detect_poison_wiz", "Cleric", 0);

    }
}
