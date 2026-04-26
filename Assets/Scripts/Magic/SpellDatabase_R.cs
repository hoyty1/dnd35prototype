// ============================================================================
// SpellDatabase_R.cs — Spells starting with R
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterSpellsR()
    {
        Register(new SpellData
                {
                    SpellId = "ray_of_enfeeblement",
                    Name = "Ray of Enfeeblement",
                    Description = "Ranged touch attack. Target takes 1d6+1 per 2 CL (max +5) STR penalty. No save. 1 min/level. PHB p.269",
                    SpellLevel = 1, School = "Necromancy",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Close,
                    IsTouch = true,
                    IsRangedTouch = true,
                    EffectType = SpellEffectType.Debuff,
                    DamageDice = 6, DamageCount = 1, BonusDamage = 1, // 1d6+1 STR penalty at CL3
                    DamageType = "str_penalty",
                    BuffDurationRounds = 30,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

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
                    SpellId = "reduce_person",
                    Name = "Reduce Person",
                    Description = "Humanoid creature halves in size. -2 STR, +2 DEX, +1 AC/attack (size). 1 min/level. PHB p.269",
                    SpellLevel = 1, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Buff,
                    BuffStatName = "DEX",
                    BuffStatBonus = 2,
                    BuffDurationRounds = 10, // Legacy fallback: 1 minute
                    BuffType = "reduce",
                    BuffBonusType = BonusType.Size,
                    BonusTypeExplicitlySet = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Fortitude",
                    ActionType = SpellActionType.FullRound,
                    ProvokesAoO = true
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

        Register(new SpellData
                {
                    SpellId = "resist_energy",
                    Name = "Resist Energy",
                    Description = "Grants resistance 10 to specified energy type (fire, cold, acid, electricity, sonic). 10 min/level. PHB p.272",
                    SpellLevel = 2, School = "Abjuration",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    BuffType = "energy_resistance",
                    BuffDamageResistanceAmount = 10,
                    BuffDamageResistanceType = DamageType.Fire, // TODO: replace with player-selected energy type
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

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
                    SpellId = "rope_trick",
                    Name = "Rope Trick",
                    Description = "As many as 8 creatures hide in extradimensional space. Duration 1 hr/level. PHB p.273",
                    SpellLevel = 2, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Extradimensional space not implemented]"
                });

        // Aliases
        RegisterClassSpellAlias("read_magic_clr", "read_magic", "Cleric", 0);
        RegisterClassSpellAlias("resist_energy_clr", "resist_energy", "Cleric", 2);
        RegisterClassSpellAlias("resistance_clr", "resistance_wiz", "Cleric", 0);

    }
}
