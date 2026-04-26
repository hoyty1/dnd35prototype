// ============================================================================
// SpellDatabase_V.cs — Spells starting with V
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterSpellsV()
    {
        Register(new SpellData
                {
                    SpellId = "ventriloquism",
                    Name = "Ventriloquism",
                    Description = "Throws voice for 1 min/level. Will disbelief. PHB p.298",
                    SpellLevel = 1, School = "Illusion",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 5,
                    EffectType = SpellEffectType.Buff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    BuffDurationRounds = 30,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Sound manipulation not implemented]"
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

    }
}
