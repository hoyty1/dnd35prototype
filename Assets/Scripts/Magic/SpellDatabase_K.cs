// ============================================================================
// SpellDatabase_K.cs — Spells starting with K
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterSpellsK()
    {
        Register(new SpellData
                {
                    SpellId = "knock",
                    Name = "Knock",
                    Description = "Opens locked or magically sealed door. PHB p.246",
                    SpellLevel = 2, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 22,
                    EffectType = SpellEffectType.Buff,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Lock/door mechanics not implemented]"
                });

    }
}
