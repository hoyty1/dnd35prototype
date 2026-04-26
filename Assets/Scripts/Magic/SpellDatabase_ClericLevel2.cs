// ============================================================================
// SpellDatabase_ClericLevel2.cs — Cleric 2nd Level spell registrations (25 spells)
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterCleric2ndLevel()
    {
        // --- FUNCTIONAL: Healing ---

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

        // --- FUNCTIONAL: Damage ---

        Register(new SpellData
        {
            SpellId = "inflict_moderate_wounds",
            Name = "Inflict Moderate Wounds",
            Description = "Touch attack deals 2d8 + CL (max +10) negative energy damage. Will half. PHB p.244",
            SpellLevel = 2, School = "Necromancy",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeCategory = SpellRangeCategory.Touch,
            IsTouch = true,
            IsMeleeTouch = true,
            EffectType = SpellEffectType.Damage,
            DamageDice = 8, DamageCount = 2, BonusDamage = 3,
            DamageType = "negative",
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            SaveHalves = true,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
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
            SpellId = "flame_strike",
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
        // --- FUNCTIONAL: Buff Spells ---

        Register(new SpellData
        {
            SpellId = "aid",
            Name = "Aid",
            Description = "+1 morale bonus on attack and saves vs fear, plus 1d8 temporary HP. Duration 1 min/level. PHB p.196",
            SpellLevel = 2, School = "Enchantment",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeCategory = SpellRangeCategory.Touch,
            EffectType = SpellEffectType.Buff,
            BuffAttackBonus = 1,
            BuffSaveBonus = 1,
            BuffTempHP = 5, // ~average of 1d8
            BuffDurationRounds = 30,
            BuffType = "morale",
            BuffBonusType = BonusType.Morale,
            BonusTypeExplicitlySet = true,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        // Shared with wizard list: bulls_strength
        RegisterClassSpellAlias("bulls_strength_clr", "bulls_strength", "Cleric", 2);

        // Shared with wizard list: bears_endurance
        RegisterClassSpellAlias("bears_endurance_clr", "bears_endurance", "Cleric", 2);

        // Shared with wizard list: owls_wisdom
        RegisterClassSpellAlias("owls_wisdom_clr", "owls_wisdom", "Cleric", 2);

        // Shared with wizard list: eagles_splendor
        RegisterClassSpellAlias("eagles_splendor_clr", "eagles_splendor", "Cleric", 2);

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

        // Shared with wizard list: resist_energy
        RegisterClassSpellAlias("resist_energy_clr", "resist_energy", "Cleric", 2);

        // --- FUNCTIONAL: Debuff Spells ---

        Register(new SpellData
        {
            SpellId = "hold_person",
            Name = "Hold Person",
            Description = "Paralyzes one humanoid for 1 round/level. Will negates. PHB p.241",
            SpellLevel = 2, School = "Enchantment",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeCategory = SpellRangeCategory.Medium,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 3, // 1 round/level
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

        // --- PLACEHOLDER: Utility ---

        Register(new SpellData
        {
            SpellId = "augury",
            Name = "Augury",
            Description = "Learns whether an action will be good, bad, mixed, or nothing. 70% + 1%/CL success. PHB p.202",
            SpellLevel = 2, School = "Divination",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeCategory = SpellRangeCategory.Personal,
            EffectType = SpellEffectType.Buff,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Divination/prediction not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "zone_of_truth",
            Name = "Zone of Truth",
            Description = "Subjects in area can't lie. Will negates. 20-ft radius. 1 min/level. PHB p.303",
            SpellLevel = 2, School = "Enchantment",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            AreaRadius = 4,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 30,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Truth compulsion not implemented]"
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
            SpellId = "remove_paralysis",
            Name = "Remove Paralysis",
            Description = "Frees 1-4 creatures from paralysis or slow effect. PHB p.271",
            SpellLevel = 2, School = "Conjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 5,
            EffectType = SpellEffectType.Healing,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Status effect removal not implemented]"
        });

        // Shared with wizard list: summon_monster_2
        RegisterClassSpellAlias("summon_monster_2_clr", "summon_monster_2", "Cleric", 2);

        Register(new SpellData
        {
            SpellId = "find_traps",
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
            SpellId = "gentle_repose",
            Name = "Gentle Repose",
            Description = "Preserves a corpse. Duration 1 day/level. PHB p.235",
            SpellLevel = 2, School = "Necromancy",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeCategory = SpellRangeCategory.Touch,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Corpse preservation not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "make_whole",
            Name = "Make Whole",
            Description = "Repairs an object of up to 10 cu.ft./level. PHB p.252",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            EffectType = SpellEffectType.Healing,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Object repair not implemented]"
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
            SpellId = "enthrall",
            Name = "Enthrall",
            Description = "Captivates all within 100 ft + 10 ft/level. Will negates. Duration 1 hour or until distracted. PHB p.227",
            SpellLevel = 2, School = "Enchantment",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 22,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = -1,
            ActionType = SpellActionType.FullRound,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Captivation not implemented]"
        });
    }

    // ====================================================================
    //  REGISTRATION & LOOKUP
    // ====================================================================

}