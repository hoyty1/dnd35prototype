// ============================================================================
// SpellDatabase_T.cs — Spells starting with T
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterSpellsT()
    {
        Register(new SpellData
                {
                    SpellId = "touch_of_fatigue",
                    Name = "Touch of Fatigue",
                    Description = "Necromancy cantrip. Melee touch attack; target becomes fatigued for 1 round/level. Fortitude negates. A fatigued target becomes exhausted; exhausted targets are unaffected. SR applies.",
                    SpellLevel = 0, School = "Necromancy",
                    ClassList = new[] { "Wizard", "Sorcerer" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    IsMeleeTouch = true,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Fortitude",
                    SpellResistanceApplies = true,
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    BuffDurationRounds = 1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "touch_of_idiocy",
                    Name = "Touch of Idiocy",
                    Description = "Touch attack reduces target's INT, WIS, and CHA by 1d6 each. No save. 10 min/level. PHB p.294",
                    SpellLevel = 2, School = "Enchantment",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    IsMeleeTouch = true,
                    EffectType = SpellEffectType.Debuff,
                    DamageDice = 6, DamageCount = 1, // 1d6 to each mental stat
                    DamageType = "mental_drain",
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "true_strike",
                    Name = "True Strike",
                    Description = "You gain +20 insight on your next single attack roll before end of your next turn, and that attack ignores concealment miss chance. PHB p.296",
                    SpellLevel = 1, School = "Divination",
                    ClassList = new[] { "Wizard", "Sorcerer" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    // Runtime behavior is implemented via TrueStrikeEffect (consumed on next attack).
                    BuffType = "insight",
                    BuffBonusType = BonusType.Insight,
                    BonusTypeExplicitlySet = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

    }
}
