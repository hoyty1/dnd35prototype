// ============================================================================
// SpellDatabase_I.cs — Spells starting with I
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterSpellsI()
    {
        Register(new SpellData
                {
                    SpellId = "identify",
                    Name = "Identify",
                    Description = "Determines all magic properties of a single magic item. Requires 100gp pearl. PHB p.243",
                    SpellLevel = 1, School = "Divination",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Item identification not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "inflict_light_wounds",
                    Name = "Inflict Light Wounds",
                    Description = "Touch attack deals 1d8 + CL (max +5) negative energy damage. Will save half. PHB p.244",
                    SpellLevel = 1, School = "Necromancy",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    IsMeleeTouch = true,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 8, DamageCount = 1, BonusDamage = 3, // +CL
                    DamageType = "negative",
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SaveHalves = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "inflict_minor_wounds",
                    Name = "Inflict Minor Wounds",
                    Description = "Touch attack deals 1 point of negative energy damage. Will save halves.",
                    SpellLevel = 0, School = "Necromancy",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 0, DamageCount = 0, BonusDamage = 1,
                    DamageType = "negative",
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SaveHalves = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "inflict_moderate_wounds",
                    Name = "Inflict Moderate Wounds",
                    Description = "Touch attack deals 2d8 + CL (max +10) negative energy damage. Will half. PHB p.244",
                    SpellLevel = 2, School = "Necromancy",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    IsMeleeTouch = true,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 8, DamageCount = 2, BonusDamage = 3,
                    DamageType = "negative",
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SaveHalves = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = "invisibility",
                    Name = "Invisibility",
                    Description = "Illusion (Glamer). Subject touched (or caster) becomes invisible. Grants total concealment (50% miss chance), +20 Hide while moving / +40 while stationary. Breaks on attack or hostile spell. Duration 1 min/level, dismissible. Components: V, S, M/DF. PHB p.245",
                    SpellLevel = 2,
                    School = "Illusion",
                    ClassList = new[] { "Wizard", "Sorcerer", "Bard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffType = "invisibility",
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    IsDismissible = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

    }
}
