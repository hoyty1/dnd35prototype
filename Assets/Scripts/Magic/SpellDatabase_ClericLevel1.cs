// ============================================================================
// SpellDatabase_ClericLevel1.cs — Cleric 1st Level spell registrations (28 spells)
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterCleric1stLevel()
    {
        // --- FUNCTIONAL: Healing ---

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

        // --- FUNCTIONAL: Damage ---

        Register(new SpellData
        {
            SpellId = "inflict_light_wounds",
            Name = "Inflict Light Wounds",
            Description = "Touch attack deals 1d8 + CL (max +5) negative energy damage. Will save half. PHB p.244",
            SpellLevel = 1, School = "Necromancy",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeCategory = SpellRangeCategory.Touch,
            IsTouch = true,
            IsMeleeTouch = true,
            EffectType = SpellEffectType.Damage,
            DamageDice = 8, DamageCount = 1, BonusDamage = 3, // +CL
            DamageType = "negative",
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            SaveHalves = true,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        // --- FUNCTIONAL: Buff Spells ---

        Register(new SpellData
        {
            SpellId = "bless",
            Name = "Bless",
            Description = "Allies in 50-ft burst gain +1 morale bonus on attack rolls and saves vs fear. 1 min/level. PHB p.205",
            SpellLevel = 1, School = "Enchantment",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Area, // 50-ft burst centered on caster
            RangeSquares = 0, // Self-centered burst
            AreaRadius = 10,
            // AoE properties
            AoEShapeType = AoEShape.Burst,
            AoESizeSquares = 10, // 50 ft radius = 10 squares
            AoERangeSquares = 0, // Self-centered burst (centered on caster)
            AoEFilter = AoETargetFilter.AlliesOnly,
            EffectType = SpellEffectType.Buff,
            BuffAttackBonus = 1,
            BuffSaveBonus = 1, // vs fear, simplified to all saves
            BuffDurationRounds = 30, // Legacy: 30 rounds at CL3
            BuffType = "morale",
            BuffBonusType = BonusType.Morale,
            BonusTypeExplicitlySet = true,
            // Duration system: 1 min/level (D&D 3.5e PHB p.205)
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
            SpellId = "protection_from_evil_clr",
            Name = "Protection from Evil",
            Description = "+2 deflection bonus to AC and +2 resistance bonus on saves vs evil. 1 min/level. PHB p.266",
            SpellLevel = 1, School = "Abjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeCategory = SpellRangeCategory.Touch,
            EffectType = SpellEffectType.Buff,
            BuffDeflectionBonus = 2,
            BuffSaveBonus = 2,
            BuffDurationRounds = 30,
            BuffType = "protection",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "magic_weapon_clr",
            Name = "Magic Weapon",
            Description = "Weapon gains +1 enhancement bonus on attack and damage. 1 min/level. PHB p.251",
            SpellLevel = 1, School = "Transmutation",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeCategory = SpellRangeCategory.Touch,
            EffectType = SpellEffectType.Buff,
            BuffAttackBonus = 1,
            BuffDamageBonus = 1,
            BuffDurationRounds = 30,
            BuffType = "enhancement",
            BuffBonusType = BonusType.Enhancement,
            BonusTypeExplicitlySet = true,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "endure_elements_clr",
            Name = "Endure Elements",
            Description = "Exist comfortably in hot or cold environments. Duration 24 hours. PHB p.226",
            SpellLevel = 1, School = "Abjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeCategory = SpellRangeCategory.Touch,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Environmental protection not implemented]"
        });

        // --- FUNCTIONAL: Debuff Spells ---

        Register(new SpellData
        {
            SpellId = "bane",
            Name = "Bane",
            Description = "Enemies take –1 on attack rolls and saves vs fear. 1 min/level. Will save negates. PHB p.203",
            SpellLevel = 1, School = "Enchantment",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleEnemy, // Simplified from area
            RangeSquares = 10,
            AreaRadius = 10,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffAttackBonus = -1,
            BuffSaveBonus = -1,
            BuffDurationRounds = 30,
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
            SpellId = "cause_fear_clr",
            Name = "Cause Fear",
            Description = "One creature of 5 HD or less flees for 1d4 rounds. Will save: shaken 1 round. PHB p.208",
            SpellLevel = 1, School = "Necromancy",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeCategory = SpellRangeCategory.Close,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 3,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "entropic_shield",
            Name = "Entropic Shield",
            Description = "Ranged attacks against you have 20% miss chance. Duration 1 min/level. PHB p.227",
            SpellLevel = 1, School = "Abjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeCategory = SpellRangeCategory.Personal,
            EffectType = SpellEffectType.Buff,
            BuffACBonus = 2, // Simplified: 20% miss ~= +2 AC vs ranged
            BuffDurationRounds = 30,
            BuffType = "entropic",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        // --- PLACEHOLDER: Utility ---

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
            SpellId = "comprehend_languages_clr",
            Name = "Comprehend Languages",
            Description = "Understand all spoken and written languages. Duration 10 min/level. PHB p.212",
            SpellLevel = 1, School = "Divination",
            ClassList = new[] { "Cleric" },
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
            SpellId = "remove_fear",
            Name = "Remove Fear",
            Description = "Suppresses fear or gives +4 morale bonus vs fear for 10 min. One ally +1 per 4 CL. PHB p.271",
            SpellLevel = 1, School = "Abjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeCategory = SpellRangeCategory.Close,
            EffectType = SpellEffectType.Buff,
            BuffSaveBonus = 4, // +4 morale vs fear, simplified
            BuffDurationRounds = -1,
            BuffType = "morale",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "hide_from_undead",
            Name = "Hide from Undead",
            Description = "Undead can't perceive one subject/level. Duration 10 min/level. Will negates (intelligent undead). PHB p.241",
            SpellLevel = 1, School = "Abjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeCategory = SpellRangeCategory.Touch,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Undead perception not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "obscuring_mist_clr",
            Name = "Obscuring Mist",
            Description = "Fog provides concealment (20% miss chance). 20-ft radius. 1 min/level. PHB p.258",
            SpellLevel = 1, School = "Conjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 4,
            AreaRadius = 4,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 30,
            BuffType = "concealment",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Concealment/fog not implemented]"
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
            SpellId = "summon_monster_1_clr",
            Name = "Summon Monster I",
            Description = "Calls a creature to fight for you. Duration 1 round/level. PHB p.285",
            SpellLevel = 1, School = "Conjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 3,
            ActionType = SpellActionType.FullRound,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Summoning not implemented]"
        });
    }

    // ====================================================================
    //  CLERIC 2ND LEVEL SPELLS
    // ====================================================================
}
