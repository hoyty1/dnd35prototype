// ============================================================================
// SpellDatabase_I.cs — Spells starting with I
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

public static partial class SpellDatabase
{
    private static void RegisterSpellsI()
    {
        Register(new SpellData
                {
                    SpellId = SpellNames.IDENTIFY,
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
                    SpellId = SpellNames.INFLICT_LIGHT_WOUNDS,
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
                    SpellId = SpellNames.INFLICT_MINOR_WOUNDS,
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
                    SpellId = SpellNames.INFLICT_MODERATE_WOUNDS,
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

        // ═══════════════════════════════════════════════════════════════
        // Invisibility — PHB p.245
        // School: Illusion (Glamer)
        // Level: Bard 2, Sorcerer/Wizard 2
        // Components: V, S, M/DF (an eyelash encased in a bit of gum arabic)
        // Casting Time: 1 standard action
        // Range: Personal or Touch
        // Target: You or a creature or object weighing no more than 100 lb./level
        // Duration: 1 min./level (D)
        // Saving Throw: Will negates (harmless) or Will negates (harmless, object)
        // Spell Resistance: Yes (harmless) or Yes (harmless, object)
        //
        // The creature or object touched becomes invisible, vanishing from sight,
        // even from darkvision. If the recipient is a creature carrying gear, that
        // vanishes, too. Anything picked up after casting is visible.
        //
        // An invisible creature gains +2 on attack rolls against sighted opponents,
        // and opponents lose their Dex bonus to AC (if positive) against it.
        // Total concealment = 50% miss chance.
        // +20 on Hide checks while moving, +40 while standing still.
        //
        // The spell ends if the subject attacks any creature. For purposes of
        // this spell, an attack includes any spell targeting a foe or whose area
        // or effect includes a foe.
        //
        // See Invisibility, True Seeing, and Glitterdust can reveal invisible creatures.
        // ═══════════════════════════════════════════════════════════════
        Register(new SpellData
                {
                    SpellId = SpellNames.INVISIBILITY,
                    Name = "Invisibility",
                    Description = "Illusion (Glamer). Subject touched (or caster) becomes invisible. Grants total concealment (50% miss chance), +2 bonus on attack rolls, opponents denied Dex to AC, +20 Hide while moving / +40 while stationary. Breaks on attack or hostile spell. Components: V, S, M/DF (eyelash in gum arabic). Duration 1 min/level (D). Will negates (harmless). SR: Yes (harmless). PHB p.245",
                    SpellLevel = 2,
                    School = "Illusion",
                    ClassList = new[] { "Wizard", "Sorcerer", "Bard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffType = SpellNames.INVISIBILITY,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    IsDismissible = true,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

    }
}
