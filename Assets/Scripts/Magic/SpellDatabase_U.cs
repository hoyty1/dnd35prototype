// ============================================================================
// SpellDatabase_U.cs — Spells starting with U
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterSpellsU()
    {
        Register(new SpellData
                {
                    SpellId = "unseen_servant",
                    Name = "Unseen Servant",
                    Description = "Invisible, mindless force that performs simple tasks. Duration 1 hr/level. PHB p.297",
                    SpellLevel = 1, School = "Conjuration",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 5,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Servant/minion not implemented]"
                });

    }
}
