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

        // Shared with wizard list: resistance_wiz
        RegisterClassSpellAlias("resistance_clr", "resistance_wiz", "Cleric", 0);

        // Shared with wizard list: detect_magic_wiz
        RegisterClassSpellAlias("detect_magic_clr", "detect_magic_wiz", "Cleric", 0);

        // Shared with wizard list: detect_poison_wiz
        RegisterClassSpellAlias("detect_poison_clr", "detect_poison_wiz", "Cleric", 0);

        // Shared with wizard list: light
        RegisterClassSpellAlias("light_clr", "light", "Cleric", 0);

        // Shared with wizard list: mending
        RegisterClassSpellAlias("mending_clr", "mending", "Cleric", 0);

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

        // Shared with wizard list: read_magic
        RegisterClassSpellAlias("read_magic_clr", "read_magic", "Cleric", 0);
    }

    // ====================================================================
    //  CLERIC 1ST LEVEL SPELLS
    // ====================================================================
}
