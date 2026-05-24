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
                    Description = "Conjuration (Creation). Webs fill a 20-ft-radius spread. Creatures in area make Reflex save or become entangled. Entangled creatures cannot move until they escape (Str or Escape Artist DC 20). Area is difficult terrain, burns if ignited, and is destroyed in 1 round by fire. Duration 10 min/level (dismissible). Components: V, S, M (spider web). PHB p.301",
                    HasMaterialComponent = true, // M: spider web (common — covered by spell component pouch)
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

        // ──────────────────────────────────────────────────────────────
        // WALL OF FIRE  (PHB p.298)
        // Evocation [Fire]
        // Level: Dru 5, Fire 4, Sor/Wiz 4
        // Components: V, S, M/DF (a small piece of phosphorus)
        // Casting Time: 1 standard action
        // Range: Medium (100 ft. + 10 ft./level)
        // Effect: Opaque sheet of flame up to 20 ft. long/level (max CL*4 sq)
        //         or a ring with up to 5-ft. radius/2 levels
        // Duration: Concentration + 1 round/level
        // Saving Throw: None (proximity), Reflex half (passing through)
        // Spell Resistance: Yes
        //
        // Creates an immobile, blazing curtain of fire.
        //  • 2d4 fire damage to creatures within 10 ft on the hot side
        //  • 1d4 fire damage within 10 ft on the cool side
        //  • 2d6+CL (max +20) fire damage to creatures passing through
        //  • Wall is opaque: blocks line of sight (50% concealment)
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.WALL_OF_FIRE,
                    Name = "Wall of Fire",
                    Description = "Evocation [Fire]. Creates a blazing curtain of flame. Deals 2d4 fire to creatures within 10 ft (near side), 1d4 fire (far side), 2d6+CL (max +20) fire to those passing through (Reflex half). Opaque (50% concealment). Duration: Concentration + 1 round/level. PHB p.298",
                    SpellLevel = 4,
                    School = "Evocation [Fire]",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Druid", 5),
                        new SpellAvailability("Sorcerer", 4),
                        new SpellAvailability("Wizard", 4)
                    },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Medium,
                    AoEShapeType = AoEShape.Line,
                    AoESizeSquares = 8, // Default; runtime scales per CL
                    AoERangeSquares = 0,
                    AoEFilter = AoETargetFilter.All,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 6,
                    DamageCount = 2, // placeholder; actual is 2d6+CL pass-through
                    DamageType = "fire",
                    AllowsSavingThrow = true,
                    SavingThrowType = "Reflex",
                    SaveHalves = true,
                    SpellResistanceApplies = true,
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    HasMaterialComponent = true, // M/DF: phosphorus (common — covered by spell component pouch)
                    IsPlaceholder = false
                });

        // ──────────────────────────────────────────────────────────────
        // WALL OF ICE  (PHB p.299)
        // Evocation [Cold]
        // Level: Sor/Wiz 4
        // Components: V, S, M (a small piece of quartz or similar rock crystal)
        // Casting Time: 1 standard action
        // Range: Medium (100 ft. + 10 ft./level)
        // Effect: Anchored plane of ice, up to one 10-ft. square/level,
        //         or hemisphere with radius up to 3 ft. + 1 ft./level
        // Duration: 1 min./level
        // Saving Throw: Reflex negates (hemisphere trap); see text
        // Spell Resistance: Yes
        //
        // Creates a plane of ice or a hemisphere.
        //  • Wall: 1 inch thick per CL, hardness 0, 3 HP per inch
        //  • Hemisphere traps creatures inside (Reflex negates)
        //  • Trapped creatures take CL cold damage (no save, 1 HP/CL)
        //  • Fire damage destroys wall segment easily (fire deals full,
        //    bypasses hardness)
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.WALL_OF_ICE,
                    Name = "Wall of Ice",
                    Description = "Evocation [Cold]. Creates a wall of ice (1 inch thick/CL, hardness 0, 3 HP/inch) or hemisphere (traps creatures, Reflex negates). Trapped creatures take 1 HP cold/CL. Duration 1 min/level. SR: Yes. PHB p.299",
                    SpellLevel = 4,
                    School = "Evocation [Cold]",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Sorcerer", 4),
                        new SpellAvailability("Wizard", 4)
                    },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Medium,
                    AoEShapeType = AoEShape.Line,
                    AoESizeSquares = 8, // Default; runtime scales per CL
                    AoERangeSquares = 0,
                    AoEFilter = AoETargetFilter.All,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = false, // Main wall no save; hemisphere Reflex at runtime
                    SpellResistanceApplies = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    HasMaterialComponent = true, // M: a small piece of quartz or rock crystal (common — covered by spell component pouch)
                    IsPlaceholder = false
                });

        // ================================================================
        //  WISH — Universal 9th-level (PHB p.302)
        // ================================================================
        // The mightiest spell a mortal can cast. Grants one of 10 standard
        // effects; resolved via WishExecutor + WishUI. XP cost 5,000 for
        // most uses (waived when cast from a magic item such as a Luck Blade).
        // TargetType = Self because the WishUI handles sub-targeting.
        Register(
            new SpellData
            {
                SpellId   = SpellNames.WISH,
                Name      = "Wish",
                SpellLevel = 9,
                School     = "Universal",
                ClassList  = new[] { "Sorcerer", "Wizard" },
                AvailableFor = new List<SpellAvailability>
                {
                    new SpellAvailability { ClassName = "Sorcerer", Level = 9 },
                    new SpellAvailability { ClassName = "Wizard",   Level = 9 }
                },
                Description = "Wish is the mightiest spell a wizard or sorcerer can cast. "
                    + "By simply speaking aloud, you can alter reality to better suit you. "
                    + "Choose one of ten standard effects (see WishUI). "
                    + "Most options cost 5,000 XP; duplicating spells of 7th level or lower is free.",
                TargetType = SpellTargetType.Self,
                EffectType = SpellEffectType.Utility,
                RangeCategory = SpellRangeCategory.Personal,
                DurationType = DurationType.Instantaneous,
                ActionType   = SpellActionType.Standard,
                ProvokesAoO  = true,
                HasVerbalComponent  = true,
                HasSomaticComponent = false,
                HasMaterialComponent = false, // XP cost handled by WishExecutor
                SpellResistanceApplies = false,
                IsPlaceholder = false
            });

    }
}
