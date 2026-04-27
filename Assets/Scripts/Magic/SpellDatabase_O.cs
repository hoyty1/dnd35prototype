// ============================================================================
// SpellDatabase_O.cs — Spells starting with O
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterSpellsO()
    {
        Register(new SpellData
                {
                    SpellId = "obscure_object",
                    Name = "Obscure Object",
                    Description = "Masks object against scrying. Duration 8 hours. PHB p.258",
                    SpellLevel = 2, School = "Abjuration",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Anti-scrying not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "obscuring_mist",
                    Name = "Obscuring Mist",
                    Description = "Mist spreads in a 20-ft radius and grants concealment (20% miss chance) to creatures inside. Duration 1 min/level. PHB p.258",
                    SpellLevel = 1, School = "Conjuration",
                    ClassList = new[] { "Wizard", "Sorcerer", "Druid", "Cleric" },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Close,
                    RangeSquares = 4,
                    AreaRadius = 4,
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 4,
                    AoERangeSquares = 4,
                    AoEFilter = AoETargetFilter.All,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = 10,
                    BuffType = "concealment",
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
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
                    SpellId = "owls_wisdom",
                    Name = "Owl's Wisdom",
                    Description = "Subject gains +4 enhancement bonus to WIS for 1 min/level. PHB p.259",
                    SpellLevel = 2, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffStatName = "WIS",
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

        // Aliases
        RegisterClassSpellAlias("obscuring_mist_clr", "obscuring_mist", "Cleric", 1);
        RegisterClassSpellAlias("owls_wisdom_clr", "owls_wisdom", "Cleric", 2);

    }
}
