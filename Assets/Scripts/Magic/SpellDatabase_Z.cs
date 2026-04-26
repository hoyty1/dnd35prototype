// ============================================================================
// SpellDatabase_Z.cs — Spells starting with Z
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterSpellsZ()
    {
        Register(new SpellData
                {
                    SpellId = "zone_of_truth",
                    Name = "Zone of Truth",
                    Description = "Subjects in area can't lie. Will negates. 20-ft radius. 1 min/level. PHB p.303",
                    SpellLevel = 2, School = "Enchantment",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 5,
                    AreaRadius = 4,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    BuffDurationRounds = 30,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Truth compulsion not implemented]"
                });

    }
}
