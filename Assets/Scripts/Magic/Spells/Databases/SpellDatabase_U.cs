// ============================================================================
// SpellDatabase_U.cs — Spells starting with U
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

public static partial class SpellDatabase
{
    private static void RegisterSpellsU()
    {
        Register(new SpellData
                {
                    SpellId = SpellNames.UNSEEN_SERVANT,
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

        // ═══════════════════════════════════════════════════════════════
        // Unholy Blight — PHB p.297
        // School: Evocation [Evil]
        // Level: Cleric 4 (Evil domain 4)
        // Range: Medium
        // Area: 20-ft-radius burst
        // Duration: Instantaneous (1d4 rounds for sicken)
        // Saving Throw: Will partial
        // Spell Resistance: Yes
        // ═══════════════════════════════════════════════════════════════
        Register(new SpellData
                {
                    SpellId = SpellNames.UNHOLY_BLIGHT,
                    Name = "Unholy Blight",
                    Description = "Burst of unholy power: 1d8/2 CL (max 5d8) vs good creatures + sickened 1d4 rounds. Will half damage and negates sicken. PHB p.297",
                    SpellLevel = 4, School = "Evocation",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Medium,
                    EffectType = SpellEffectType.Damage,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SaveHalves = true,
                    SpellResistanceApplies = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

    }
}
