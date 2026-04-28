// ============================================================================
// SpellDatabase_S.cs — Spells starting with S
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterSpellsS()
    {
        Register(new SpellData
                {
                    SpellId = "sanctuary",
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
                    BuffType = "sanctuary",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Attack prevention not fully implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "scare",
                    Name = "Scare",
                    Description = "Frightens creatures of less than 6 HD. Will save negates. 1 round/level. PHB p.274",
                    SpellLevel = 2, School = "Necromancy",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Medium,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    BuffDurationRounds = 3,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "scorching_ray",
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
                    SpellId = "see_invisibility",
                    Name = "See Invisibility",
                    Description = "You can see invisible creatures and objects. Duration 10 min/level. PHB p.275",
                    SpellLevel = 2, School = "Divination",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    BuffType = "see_invis",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Invisibility detection not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "shatter",
                    Name = "Shatter",
                    Description = "Sonic vibration damages objects or crystalline creatures. 1d6/level (max 10d6) sonic. PHB p.278",
                    SpellLevel = 2, School = "Evocation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 6, DamageCount = 3, // 3d6 at CL3
                    DamageType = "sonic",
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SaveHalves = false,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "shield",
                    Name = "Shield",
                    Description = "+4 shield bonus to AC, blocks Magic Missile. Duration 1 min/level. PHB p.278",
                    SpellLevel = 1, School = "Abjuration",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffShieldBonus = 4,
                    BuffDurationRounds = 30,
                    BuffType = "shield",
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
                    SpellId = "shield_of_faith",
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
                    SpellId = "shield_other",
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
                    BuffType = "shield_other",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "shocking_grasp",
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
                    SpellId = "silence",
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
                    SpellId = "silent_image",
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
                    SpellId = "sleep",
                    Name = "Sleep",
                    Description = "Puts 4 HD of creatures into magical slumber. Will save negates. Range: Medium. PHB p.280",
                    SpellLevel = 1, School = "Enchantment",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Medium,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    BuffDurationRounds = 10, // 1 min/level
                    ActionType = SpellActionType.FullRound,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "domain_soften_earth",
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
                    SpellId = "sound_burst",
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

        Register(new SpellData
                {
                    SpellId = "spectral_hand",
                    Name = "Spectral Hand",
                    Description = "Creates ghostly hand to deliver touch spells at range. +2 on touch attacks via hand. 1 min/level. PHB p.282",
                    SpellLevel = 2, School = "Necromancy",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Medium,
                    EffectType = SpellEffectType.Buff,
                    BuffAttackBonus = 2,
                    BuffDurationRounds = 30,
                    BuffType = "spectral_hand",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "spider_climb",
                    Name = "Spider Climb",
                    Description = "Grants ability to walk on walls and ceilings. Speed 20 ft. Duration 10 min/level. PHB p.283",
                    SpellLevel = 2, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    BuffType = "spider_climb",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Wall climbing not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "spiritual_weapon",
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
                    SpellId = "stone_to_flesh",
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
                    SpellId = "status",
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
                    SpellId = "summon_monster_1",
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
                    SpellId = "summon_monster_2",
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
                    SpellId = "summon_swarm",
                    Name = "Summon Swarm",
                    Description = "Summons swarm of bats, rats, or spiders. 2d6 damage/round. Concentration + 2 rounds. PHB p.289",
                    SpellLevel = 2, School = "Conjuration",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeSquares = 5,
                    EffectType = SpellEffectType.Damage,
                    DurationType = DurationType.Concentration,
                    DamageDice = 6, DamageCount = 2,
                    DamageType = "swarm",
                    ActionType = SpellActionType.FullRound,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Swarm summoning not implemented]"
                });

        // Aliases
        RegisterClassSpellAlias("summon_monster_1_clr", "summon_monster_1", "Cleric", 1);
        RegisterClassSpellAlias("summon_monster_2_clr", "summon_monster_2", "Cleric", 2);

    }
}
