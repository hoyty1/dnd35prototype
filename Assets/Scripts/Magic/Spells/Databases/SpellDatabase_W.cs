// ============================================================================
// SpellDatabase_W.cs — Spells starting with W
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

public static partial class SpellDatabase
{
    private static void RegisterSpellsW()
    {
        Register(new SpellData
                {
                    SpellId = SpellNames.WEB,
                    Name = "Web",
                    Description = "Conjuration (Creation). Webs fill a 20-ft-radius spread. Creatures in area make Reflex save or become entangled. Entangled creatures cannot move until they escape (Str or Escape Artist DC 20). Area is difficult terrain, burns if ignited, and is destroyed in 1 round by fire. Duration 10 min/level (dismissible). PHB p.301",
                    SpellLevel = 2,
                    School = "Conjuration",
                    ClassList = new[] { "Wizard", "Sorcerer" },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Medium,
                    AreaRadius = 4,
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 4,
                    AoERangeSquares = 0,
                    AoEFilter = AoETargetFilter.All,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Reflex",
                    SaveHalves = false,
                    SpellResistanceApplies = false,
                    BuffDurationRounds = 100,
                    DurationType = DurationType.Minutes,
                    DurationValue = 10,
                    DurationScalesWithLevel = true,
                    IsDismissible = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.WHISPERING_WIND,
                    Name = "Whispering Wind",
                    Description = "Sends a short message or sound to a distant location. PHB p.301",
                    SpellLevel = 2, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Long-range communication not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.DOMAIN_WIND_WALL,
                    Name = "Wind Wall",
                    Description = "Deflects arrows, smaller creatures, and gases. Creates an invisible wall of wind.",
                    SpellLevel = 2,
                    School = "Evocation",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.Area,
                    RangeSquares = 6,
                    AreaRadius = 2,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = 30,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Wind wall deflection not implemented]"
                });

        // ──────────────────────────────────────────────────────────────
        // WIND WALL  (PHB p.302)
        // Evocation [Air]
        // Level: Air 2, Cleric 3, Druid 3, Ranger 2, Sor/Wiz 3
        // Components: V, S, M/DF (a tiny fan and a feather of an exotic bird)
        // Casting Time: 1 standard action
        // Range: Medium (100 ft. + 10 ft./level)
        // Effect: Wall up to 10 ft./level long and 5 ft./level high (S)
        // Duration: 1 round/level
        // Saving Throw: None; see text
        // Spell Resistance: Yes
        //
        // An invisible vertical curtain of wind appears. It is 2 feet thick
        // and of considerable strength. It is a roaring blast sufficient to
        // blow away any bird smaller than an eagle, or tear papers and
        // similar materials from unsuspecting hands. (An occupant of the
        // wall takes 3d6 nonlethal damage if Tiny or smaller.)
        //
        // Tiny and smaller flying creatures cannot pass through the barrier.
        // Loose materials and cloth garments fly upward when caught in it.
        // Arrows and bolts are deflected upward and miss, while bigger
        // ranged weapons (such as spears) and gases (such as a dragon's
        // breath weapon or a cloudkill cloud) are unaffected.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.WIND_WALL,
                    Name = "Wind Wall",
                    Description = "Evocation [Air]. An invisible vertical wall of wind up to 10 ft/level long and 5 ft/level high. "
                        + "Deflects arrows, bolts, and tiny/smaller flying creatures (cannot pass). Disperses gases and fog. "
                        + "Larger ranged weapons (spears, javelins) pass through unaffected. Tiny or smaller occupants take 3d6 nonlethal. "
                        + "Duration 1 round/level. No save. SR: Yes. PHB p.302",
                    SpellLevel = 3,
                    School = "Evocation [Air]",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Cleric", 3),
                        new SpellAvailability("Druid", 3),
                        new SpellAvailability("Ranger", 2),
                        new SpellAvailability("Sorcerer", 3),
                        new SpellAvailability("Wizard", 3)
                    },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Medium,
                    AoEShapeType = AoEShape.Line,
                    // Length scales 2 squares per CL (10 ft per CL); height 1 square per CL
                    // Default placeholder size = 10 squares (50 ft) — runtime resolution scales by caster level
                    AoESizeSquares = 10,
                    AoERangeSquares = 0,
                    AoEFilter = AoETargetFilter.All,
                    EffectType = SpellEffectType.Buff,
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    BuffDurationRounds = 1, // legacy fallback; runtime uses scaled duration
                    IsDismissible = false,
                    AllowsSavingThrow = false,
                    SpellResistanceApplies = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

    }
}
