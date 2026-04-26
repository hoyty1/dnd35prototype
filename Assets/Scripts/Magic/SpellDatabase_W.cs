// ============================================================================
// SpellDatabase_W.cs — Spells starting with W
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterSpellsW()
    {
        Register(new SpellData
                {
                    SpellId = "web",
                    Name = "Web",
                    Description = "Fills 20-ft-radius spread with sticky webs. Reflex save or stuck. STR/Escape check to escape. 10 min/level. PHB p.301",
                    SpellLevel = 2, School = "Conjuration",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy, // Simplified from area
                    RangeCategory = SpellRangeCategory.Medium,
                    AreaRadius = 4,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Reflex",
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "whispering_wind",
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
                    SpellId = "domain_wind_wall",
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
