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
        // Inflict Serious Wounds — PHB p.244
        // School: Necromancy
        // Level: Cleric 3
        // Components: V, S
        // Casting Time: 1 standard action
        // Range: Touch
        // Target: Creature touched
        // Duration: Instantaneous
        // Saving Throw: Will half
        // Spell Resistance: Yes
        // ═══════════════════════════════════════════════════════════════
        Register(new SpellData
                {
                    SpellId = SpellNames.INFLICT_SERIOUS_WOUNDS,
                    Name = "Inflict Serious Wounds",
                    Description = "Touch attack deals 3d8 + CL (max +15) negative energy damage. Will half. PHB p.244",
                    SpellLevel = 3, School = "Necromancy",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    IsMeleeTouch = true,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 8, DamageCount = 3, BonusDamage = 5, // +CL (max +15)
                    DamageType = "negative",
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SaveHalves = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // ═══════════════════════════════════════════════════════════════
        // Invisibility Purge — PHB p.245
        // School: Evocation
        // Level: Cleric 3
        // Components: V, S
        // Casting Time: 1 standard action
        // Range: Personal
        // Target: You
        // Duration: 1 min./level (D)
        //
        // You surround yourself with a sphere of power with a radius of
        // 5 feet per caster level that negates all forms of invisibility.
        // ═══════════════════════════════════════════════════════════════
        Register(new SpellData
                {
                    SpellId = SpellNames.INVISIBILITY_PURGE,
                    Name = "Invisibility Purge",
                    Description = "Dispels invisibility within 5 ft/level. 1 min/level. PHB p.245",
                    SpellLevel = 3, School = "Evocation",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = 50, // 1 min/level at CL5 = 50 rounds
                    DurationType = DurationType.MinutesPerLevel,
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

        // ═══════════════════════════════════════════════════════════════
        // Invisibility Sphere — PHB p.245
        // School: Illusion (Glamer)
        // Level: Bard 3, Sorcerer/Wizard 3
        // Components: V, S, M (an eyelash encased in a bit of gum arabic)
        // Casting Time: 1 standard action
        // Range: Personal or Touch
        // Area: 10-ft.-radius emanation around the creature or object touched
        // Duration: 1 min./level (D)
        // Saving Throw: Will negates (harmless) or Will negates (harmless, object)
        // Spell Resistance: Yes (harmless) or Yes (harmless, object)
        //
        // The creature or object to which this spell is cast becomes the center
        // of a 10-ft.-radius emanation that turns all creatures within it
        // invisible. Creatures inside the area can see each other and the
        // recipient but lose invisibility (and the spell ends for them) if they
        // leave the emanation. Creatures that enter the area after the spell is
        // cast do NOT become invisible. Creatures within the area become visible
        // if they attack any creature.
        //
        // If the recipient (the center of the emanation) attacks, all creatures
        // affected by the sphere become visible at once and the spell ends.
        // ═══════════════════════════════════════════════════════════════
        Register(new SpellData
                {
                    SpellId = SpellNames.INVISIBILITY_SPHERE,
                    Name = "Invisibility Sphere",
                    Description = "Illusion (Glamer). Recipient becomes the center of a 10-ft-radius emanation that turns all creatures within it invisible. Affected creatures can see one another. A creature that leaves the emanation becomes visible (the spell ends for it). New creatures entering the area do NOT become invisible. If any affected creature other than the recipient attacks, only that creature becomes visible. If the recipient attacks, the entire spell ends and all become visible. Components: V, S, M (eyelash in gum arabic). Duration 1 min/level (D). Will negates (harmless). SR: Yes (harmless). PHB p.245",
                    SpellLevel = 3,
                    School = "Illusion",
                    ClassList = new[] { "Wizard", "Sorcerer", "Bard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffType = SpellNames.INVISIBILITY_SPHERE,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    IsDismissible = true,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // ──────────────────────────────────────────────────────────────
        // ICE STORM  (PHB p.243)
        // Evocation [Cold]
        // Level: Dru 4, Sor/Wiz 4
        // Components: V, S, M/DF (a pinch of dust and a few drops of water)
        // Casting Time: 1 standard action
        // Range: Long (400 ft. + 40 ft./level)
        // Area: Cylinder (20-ft. radius, 40 ft. high)
        // Duration: 1 full round (see text)
        // Saving Throw: None
        // Spell Resistance: Yes
        //
        // Great hailstones pound down for 1 full round, dealing 3d6 bludgeoning
        // and 2d6 cold damage. The hail does not permit a saving throw.
        // A -4 penalty on Listen checks and a -4 to ranged attacks apply
        // within the area. Movement at half speed. The ground in the area
        // is covered with ice, which lasts 1 round.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.ICE_STORM,
                    Name = "Ice Storm",
                    Description = "Evocation [Cold]. Great hailstones pound down dealing 3d6 bludgeoning + 2d6 cold damage (no save) in a 20-ft radius cylinder. Area becomes icy difficult terrain for 1 round. SR: Yes. PHB p.243",
                    SpellLevel = 4,
                    School = "Evocation [Cold]",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Druid", 4),
                        new SpellAvailability("Sorcerer", 4),
                        new SpellAvailability("Wizard", 4)
                    },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Long,
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 4, // 20-ft radius = 4 squares
                    AoERangeSquares = 0, // use Long range profile
                    AoEFilter = AoETargetFilter.All,
                    AreaRadius = 4,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 6,
                    DamageCount = 5, // 3d6 bludgeon + 2d6 cold total
                    DamageType = "bludgeoning/cold",
                    AllowsSavingThrow = false,
                    SavingThrowType = "None",
                    SpellResistanceApplies = true,
                    DurationType = DurationType.Instantaneous,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

    }
}
