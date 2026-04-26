// ============================================================================
// SpellDatabase_E.cs — Spells starting with E
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterSpellsE()
    {
        Register(new SpellData
                {
                    SpellId = "eagles_splendor",
                    Name = "Eagle's Splendor",
                    Description = "Subject gains +4 enhancement bonus to CHA for 1 min/level. PHB p.225",
                    SpellLevel = 2, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffStatName = "CHA",
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
                    SpellId = "endure_elements",
                    Name = "Endure Elements",
                    Description = "Exist comfortably in hot or cold environments. Duration 24 hours. PHB p.226",
                    SpellLevel = 1, School = "Abjuration",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Environmental protection not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "enlarge_person",
                    Name = "Enlarge Person",
                    Description = "Humanoid creature doubles in size. +2 STR, -2 DEX, -1 AC/attack (size). Duration 1 min/level. PHB p.226",
                    SpellLevel = 1, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Buff,
                    BuffStatName = "STR",
                    BuffStatBonus = 2, // +2 size bonus to STR
                    BuffDurationRounds = 10, // Legacy fallback: 1 minute
                    BuffType = "enlarge",
                    BuffBonusType = BonusType.Size,
                    BonusTypeExplicitlySet = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    AllowsSavingThrow = true, // Fortitude negates (unwilling)
                    SavingThrowType = "Fortitude",
                    ActionType = SpellActionType.FullRound,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "domain_entangle",
                    Name = "Entangle",
                    Description = "Grasses and weeds entangle creatures in 40-ft radius spread. Entangled creatures can break free with Strength or Escape Artist check.",
                    SpellLevel = 1,
                    School = "Transmutation",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.Area,
                    RangeSquares = 8,
                    AreaRadius = 8,
                    EffectType = SpellEffectType.Debuff,
                    BuffDurationRounds = 30,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Reflex",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Entangle/grapple not implemented]"
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

        Register(new SpellData
                {
                    SpellId = "erase",
                    Name = "Erase",
                    Description = "Mundane or magical writing vanishes. PHB p.227",
                    SpellLevel = 1, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 5,
                    EffectType = SpellEffectType.Buff,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Writing mechanics not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "expeditious_retreat",
                    Name = "Expeditious Retreat",
                    Description = "Your base speed increases by 30 ft (+6 squares). Duration 1 min/level. PHB p.228",
                    SpellLevel = 1, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = 30,
                    BuffType = "speed",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // Aliases
        RegisterClassSpellAlias("eagles_splendor_clr", "eagles_splendor", "Cleric", 2);
        RegisterClassSpellAlias("endure_elements_clr", "endure_elements", "Cleric", 1);

    }
}
