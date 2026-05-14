// ============================================================================
// SpellDatabase_K.cs — Spells starting with K
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

public static partial class SpellDatabase
{
    private static void RegisterSpellsK()
    {
        // ──────────────────────────────────────────────────────────────
        // KEEN EDGE  (PHB p.246)
        // Transmutation
        // Level: Sor/Wiz 3
        // Components: V, S
        // Casting Time: 1 standard action
        // Range: Close (25 ft. + 5 ft./2 levels)
        // Target: One weapon or fifty projectiles, all of which must be
        //         in contact with each other at the time of casting
        // Duration: 10 min./level
        // Saving Throw: Will negates (harmless, object)
        // Spell Resistance: Yes (harmless, object)
        //
        // This spell makes a weapon magically keen, improving its ability
        // to deal telling blows. This doubles the threat range of the weapon.
        // Only works on piercing or slashing weapons. Does not stack with
        // the keen weapon property or Improved Critical feat.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.KEEN_EDGE,
                    Name = "Keen Edge",
                    Description = "Transmutation. Doubles the threat range of one slashing or piercing weapon, "
                        + "or up to fifty projectiles. "
                        + "Does not stack with keen weapon property or Improved Critical feat. "
                        + "Duration 10 min/level. Components: V, S. PHB p.246",
                    SpellLevel = 3,
                    School = "Transmutation",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Sorcerer", 3),
                        new SpellAvailability("Wizard", 3)
                    },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Buff,
                    DurationType = DurationType.Minutes,
                    DurationValue = 10,
                    DurationScalesWithLevel = true,
                    BuffDurationRounds = 100,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.KNOCK,
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
