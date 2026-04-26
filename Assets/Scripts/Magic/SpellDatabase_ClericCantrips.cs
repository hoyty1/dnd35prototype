// ============================================================================
// SpellDatabase_ClericCantrips.cs — Cleric Cantrip (Level 0 Orison) spell registrations (10 spells)
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterClericCantrips()
    {
        // --- FUNCTIONAL: Healing ---

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

        // --- FUNCTIONAL: Damage ---

        Register(new SpellData
        {
            SpellId = "inflict_minor_wounds",
            Name = "Inflict Minor Wounds",
            Description = "Touch attack deals 1 point of negative energy damage. Will save halves.",
            SpellLevel = 0, School = "Necromancy",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeCategory = SpellRangeCategory.Touch,
            EffectType = SpellEffectType.Damage,
            DamageDice = 0, DamageCount = 0, BonusDamage = 1,
            DamageType = "negative",
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            SaveHalves = true,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        // --- FUNCTIONAL: Utility/Buff Orisons ---

        Register(new SpellData
        {
            SpellId = "guidance",
            Name = "Guidance",
            Description = "+1 on one attack roll, saving throw, or skill check. Duration 1 minute or until discharged.",
            SpellLevel = 0, School = "Divination",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeCategory = SpellRangeCategory.Touch,
            EffectType = SpellEffectType.Buff,
            BuffAttackBonus = 1,
            BuffSaveBonus = 1,
            BuffDurationRounds = 10,
            BuffType = "competence",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "virtue",
            Name = "Virtue",
            Description = "Subject gains 1 temporary hit point for 1 minute.",
            SpellLevel = 0, School = "Transmutation",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeCategory = SpellRangeCategory.Touch,
            EffectType = SpellEffectType.Buff,
            BuffTempHP = 1,
            BuffDurationRounds = 10,
            BuffType = "temp_hp",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "resistance_clr",
            Name = "Resistance",
            Description = "Subject gains +1 on saving throws for 1 minute.",
            SpellLevel = 0, School = "Abjuration",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeCategory = SpellRangeCategory.Touch,
            EffectType = SpellEffectType.Buff,
            BuffSaveBonus = 1,
            BuffDurationRounds = 10,
            BuffType = "resistance",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "detect_magic_clr",
            Name = "Detect Magic",
            Description = "Detects spells and magic items within 60 ft cone. Concentration, up to 1 min/level.",
            SpellLevel = 0, School = "Divination",
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
            PlaceholderReason = "[PLACEHOLDER - Detection mechanics not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "detect_poison_clr",
            Name = "Detect Poison",
            Description = "Detects poison in one creature or small object.",
            SpellLevel = 0, School = "Divination",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Poison detection not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "light_clr",
            Name = "Light",
            Description = "Object shines like a torch for 10 min/level.",
            SpellLevel = 0, School = "Evocation",
            ClassList = new[] { "Cleric" },
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
            SpellId = "mending_clr",
            Name = "Mending",
            Description = "Makes minor repairs on an object.",
            SpellLevel = 0, School = "Transmutation",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 2,
            EffectType = SpellEffectType.Buff,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Object repair not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "purify_food_drink",
            Name = "Purify Food and Drink",
            Description = "Purifies 1 cu.ft./level of food and water.",
            SpellLevel = 0, School = "Transmutation",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 2,
            EffectType = SpellEffectType.Buff,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Food/water mechanics not implemented]"
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
            SpellId = "read_magic_clr",
            Name = "Read Magic",
            Description = "Read scrolls and spellbooks. Duration 10 min/level.",
            SpellLevel = 0, School = "Divination",
            ClassList = new[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeCategory = SpellRangeCategory.Personal,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Scroll reading not implemented]"
        });
    }

    // ====================================================================
    //  CLERIC 1ST LEVEL SPELLS
    // ====================================================================
}
