// ============================================================================
// SpellDatabase_H.cs — Spells starting with H
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterSpellsH()
    {
        Register(new SpellData
                {
                    SpellId = "domain_heat_metal",
                    Name = "Heat Metal",
                    Description = "Make metal intensely hot. Creatures wearing metal armor take 1d4 to 2d4 fire damage per round.",
                    SpellLevel = 2,
                    School = "Transmutation",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeSquares = 5,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 4,
                    DamageCount = 2,
                    DamageType = "fire",
                    BuffDurationRounds = 7,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Ongoing damage over rounds not implemented]"
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
                    SpellId = "hideous_laughter",
                    Name = "Hideous Laughter",
                    Description = "Subject laughs uncontrollably, falls prone. Will save negates. 1 round/level. PHB p.240",
                    SpellLevel = 2, School = "Enchantment",
                    ClassList = new[] { "Wizard" },
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
                    SpellId = "domain_hold_animal",
                    Name = "Hold Animal",
                    Description = "Paralyzes one animal for 1 round/level.",
                    SpellLevel = 2,
                    School = "Enchantment",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeSquares = 6,
                    EffectType = SpellEffectType.Debuff,
                    BuffDurationRounds = 30,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Hold/paralyze not implemented]"
                });

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
                    SpellId = "hold_portal",
                    Name = "Hold Portal",
                    Description = "Holds door shut as if locked. Duration 1 min/level. PHB p.241",
                    SpellLevel = 1, School = "Abjuration",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 22,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = 30,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Door mechanics not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "hypnotic_pattern",
                    Name = "Hypnotic Pattern",
                    Description = "Fascinates 2d4+CL HD of creatures. Will negates. Concentration + 2 rounds. PHB p.242",
                    SpellLevel = 2, School = "Illusion",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeSquares = 22,
                    EffectType = SpellEffectType.Debuff,
                    DurationType = DurationType.Concentration,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Fascination mechanics not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "hypnotism",
                    Name = "Hypnotism",
                    Description = "Fascinates 2d4 HD of creatures. Will save negates. PHB p.242",
                    SpellLevel = 1, School = "Enchantment",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeSquares = 5,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    BuffDurationRounds = 3,
                    ActionType = SpellActionType.FullRound,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Fascination mechanics not implemented]"
                });

    }
}
