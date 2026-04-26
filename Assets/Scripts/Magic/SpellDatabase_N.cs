// ============================================================================
// SpellDatabase_N.cs — Spells starting with N
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterSpellsN()
    {
        Register(new SpellData
                {
                    SpellId = "nystuls_magic_aura",
                    Name = "Nystul's Magic Aura",
                    Description = "Alters an object's magic aura. Duration 1 day/level. PHB p.257",
                    SpellLevel = 1, School = "Illusion",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Aura manipulation not implemented]"
                });

    }
}
