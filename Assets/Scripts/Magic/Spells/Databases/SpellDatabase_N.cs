// ============================================================================
// SpellDatabase_N.cs — Spells starting with N
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

public static partial class SpellDatabase
{
    private static void RegisterSpellsN()
    {
        Register(new SpellData
                {
                    SpellId = SpellNames.NYSTULS_MAGIC_AURA,
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

        // ═══════════════════════════════════════════════════════════════
        // Neutralize Poison — PHB p.257
        // School: Conjuration (Healing)
        // Level: Cleric 4, Druid 3, Paladin 4, Ranger 3, Bard 4
        // Range: Touch
        // Duration: Instantaneous (cure) + 10 min/level (immunity)
        // Saving Throw: Will negates (harmless)
        // Spell Resistance: Yes (harmless)
        // ═══════════════════════════════════════════════════════════════
        Register(new SpellData
                {
                    SpellId = SpellNames.NEUTRALIZE_POISON,
                    Name = "Neutralize Poison",
                    Description = "Detoxifies any sort of venom in the target and grants immunity to poison for 10 min/level. PHB p.257",
                    SpellLevel = 4, School = "Conjuration",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    IsMeleeTouch = true,
                    EffectType = SpellEffectType.Buff,
                    DurationType = DurationType.Minutes,
                    DurationValue = 10, // 10 min/level (immunity portion)
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

    }
}
