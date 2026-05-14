// ============================================================================
// SpellDatabase_T.cs — Spells starting with T
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

public static partial class SpellDatabase
{
    private static void RegisterSpellsT()
    {
        Register(new SpellData
                {
                    SpellId = SpellNames.TOUCH_OF_FATIGUE,
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
                    SpellId = SpellNames.TOUCH_OF_IDIOCY,
                    Name = "Touch of Idiocy",
                    Description = "Enchantment (Compulsion) [Mind-Affecting]. Melee touch attack. On hit, living target takes 1d6 Intelligence damage, 1d6 Wisdom damage, and 1d6 Charisma damage. No save. Duration 10 min/level. SR applies. Does not cause unconsciousness at 0 mental scores. PHB p.294",
                    SpellLevel = 2,
                    School = "Enchantment",
                    ClassList = new[] { "Wizard", "Sorcerer" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    IsMeleeTouch = true,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = false,
                    SpellResistanceApplies = true,
                    IsMindAffecting = true,
                    BlockedByProtectionFromAlignment = false,
                    DurationType = DurationType.Minutes,
                    DurationValue = 10,
                    DurationScalesWithLevel = true,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.TRUE_STRIKE,
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
