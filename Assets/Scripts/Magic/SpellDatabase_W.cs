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

    }
}
