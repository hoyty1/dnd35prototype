// ============================================================================
// SpellDatabase_WizardCantrips.cs — Wizard Cantrip (Level 0) spell registrations (20 spells)
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterWizardCantrips()
    {
        // --- FUNCTIONAL: Damage Cantrips ---

        Register(new SpellData
        {
            SpellId = "ray_of_frost",
            Name = "Ray of Frost",
            Description = "A ray of freezing air and ice deals 1d3 cold damage. Ranged touch attack.",
            SpellLevel = 0, School = "Evocation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeCategory = SpellRangeCategory.Close,
            IsTouch = true,
            IsRangedTouch = true,
            EffectType = SpellEffectType.Damage,
            DamageDice = 3, DamageCount = 1,
            DamageType = "cold",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "acid_splash",
            Name = "Acid Splash",
            Description = "An orb of acid deals 1d3 acid damage on a ranged touch attack. Range: Close (25 ft + 5 ft/2 levels).",
            SpellLevel = 0, School = "Conjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            // Close range (25 ft + 5 ft/2 levels)
            RangeCategory = SpellRangeCategory.Close,
            IsTouch = true,
            IsRangedTouch = true,
            EffectType = SpellEffectType.Damage,
            DamageDice = 3, DamageCount = 1,
            DamageType = "acid",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
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

        // --- PLACEHOLDER: Utility Cantrips ---

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
            SpellId = "read_magic",
            Name = "Read Magic",
            Description = "Read scrolls and spellbooks. Duration 10 min/level.",
            SpellLevel = 0, School = "Divination",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeCategory = SpellRangeCategory.Personal,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Scroll/spellbook reading not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "prestidigitation",
            Name = "Prestidigitation",
            Description = "Performs minor tricks: clean, soil, color, flavor, chill, warm, create small trinket. Lasts 1 hour.",
            SpellLevel = 0, School = "Universal",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 2,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Utility effects not implemented]"
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
            SpellId = "flare",
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
            SpellId = "ghost_sound",
            Name = "Ghost Sound",
            Description = "Figment sounds. Will disbelief (if interacted with).",
            SpellLevel = 0, School = "Illusion",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 3,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Illusion mechanics not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "light",
            Name = "Light",
            Description = "Object shines like a torch for 10 min/level.",
            SpellLevel = 0, School = "Evocation",
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
            SpellId = "mage_hand",
            Name = "Mage Hand",
            Description = "5-pound telekinesis. Move one nonmagical, unattended object up to 5 lb.",
            SpellLevel = 0, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Telekinesis not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "mending",
            Name = "Mending",
            Description = "Makes minor repairs on an object (1d4 damage repaired).",
            SpellLevel = 0, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 2,
            EffectType = SpellEffectType.Healing,
            HealDice = 4, HealCount = 1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Object repair not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "message",
            Name = "Message",
            Description = "Whispered conversation at distance. Range: 100 ft + 10 ft/level.",
            SpellLevel = 0, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 22,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 10,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Communication not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "open_close",
            Name = "Open/Close",
            Description = "Opens or closes small or light things (door, chest, bottle, etc.).",
            SpellLevel = 0, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Object interaction not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "arcane_mark",
            Name = "Arcane Mark",
            Description = "Inscribes a personal rune (visible or invisible) on an object or creature.",
            SpellLevel = 0, School = "Universal",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeCategory = SpellRangeCategory.Touch,
            EffectType = SpellEffectType.Buff,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Marking not implemented]"
        });

        // Resistance cantrip (functional - minor save buff)
        Register(new SpellData
        {
            SpellId = "resistance_wiz",
            Name = "Resistance",
            Description = "Subject gains +1 on saving throws for 1 minute.",
            SpellLevel = 0, School = "Abjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeCategory = SpellRangeCategory.Touch,
            EffectType = SpellEffectType.Buff,
            BuffSaveBonus = 1,
            BuffDurationRounds = 10, // 1 minute
            BuffType = "resistance",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
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
            SpellId = "touch_of_fatigue",
            Name = "Touch of Fatigue",
            Description = "Necromancy cantrip. Melee touch attack; target becomes fatigued for 1 round/level. Fortitude negates. A fatigued target becomes exhausted; exhausted targets are unaffected. SR applies.",
            SpellLevel = 0, School = "Necromancy",
            ClassList = new[] { "Wizard", "Sorcerer" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeCategory = SpellRangeCategory.Touch,
            IsTouch = true,
            IsMeleeTouch = true,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Fortitude",
            SpellResistanceApplies = true,
            DurationType = DurationType.Rounds,
            DurationValue = 1,
            DurationScalesWithLevel = true,
            BuffDurationRounds = 1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });
    }

    // ====================================================================
    //  WIZARD 1ST LEVEL SPELLS
    // ====================================================================
}
