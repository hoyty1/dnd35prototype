// ============================================================================
// SpellDatabase_P.cs — Spells starting with P
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

public static partial class SpellDatabase
{
    private static void RegisterSpellsP()
    {
        Register(new SpellData
                {
                    SpellId = SpellNames.PRESTIDIGITATION,
                    Name = "Prestidigitation",
                    Description = "Performs minor tricks: clean, soil, color, flavor, chill, warm, create small trinket. Lasts 1 hour.",
                    SpellLevel = 0, School = "Universal",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 2,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // ──────────────────────────────────────────────────────────────
        // PRODUCE FLAME  (PHB p.265)
        // Evocation [Fire]
        // Level: Druid 1, Fire 2
        // Casting Time: 1 standard action
        // Range: 0 ft.
        // Effect: Flame in your palm
        // Duration: 1 min/level (D)
        // Saving Throw: None
        // Spell Resistance: Yes
        //
        // 1d6 + min(CL,5) fire damage as melee touch or thrown (120 ft).
        // Each attack expends the flame for that round.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.DOMAIN_PRODUCE_FLAME,
                    Name = "Produce Flame",
                    Description = "Conjures flame: 1d6 + min(CL,5) fire damage as melee touch or ranged touch (120 ft). Duration 1 min/level. PHB p.265",
                    SpellLevel = 2,
                    School = "Evocation",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeSquares = 24,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 6,
                    DamageCount = 1,
                    DamageType = "fire",
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    IsDismissible = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.PROTECTION_FROM_ARROWS,
                    Name = "Protection from Arrows",
                    Description = "Touched creature gains DR 10/magic against ranged weapons and absorbs up to 10 damage per caster level (max 100). Duration 1 hour/level (dismissible) or until discharged. PHB p.266",
                    SpellLevel = 2,
                    School = "Abjuration",
                    ClassList = new[] { "Wizard", "Sorcerer", "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffType = "DR_arrows",
                    BuffDamageReductionAmount = 10,
                    BuffDamageReductionBypass = DamageBypassTag.Magic,
                    BuffDamageReductionRangedOnly = true,
                    DurationType = DurationType.Hours,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    BuffDurationRounds = -1,
                    IsDismissible = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.PROTECTION_FROM_CHAOS,
                    Name = "Protection from Chaos",
                    Description = "Wards against chaotic creatures: +2 deflection AC and +2 resistance on saves vs chaotic creatures; blocks mental control and bodily contact by summoned chaotic creatures.",
                    SpellLevel = 1,
                    School = "Abjuration",
                    ClassList = new[] { "Cleric", "Paladin", "Sorcerer", "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDeflectionBonus = 2,
                    BuffSaveBonus = 2,
                    BuffType = "protection",
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    BuffDurationRounds = 10,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.PROTECTION_FROM_EVIL,
                    Name = "Protection from Evil",
                    Description = "Wards against evil creatures: +2 deflection AC and +2 resistance on saves vs evil creatures; blocks mental control and bodily contact by summoned evil creatures. PHB p.266",
                    SpellLevel = 1,
                    School = "Abjuration",
                    ClassList = new[] { "Cleric", "Paladin", "Sorcerer", "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDeflectionBonus = 2,
                    BuffSaveBonus = 2,
                    BuffType = "protection",
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    BuffDurationRounds = 10,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.PROTECTION_FROM_GOOD,
                    Name = "Protection from Good",
                    Description = "Wards against good creatures: +2 deflection AC and +2 resistance on saves vs good creatures; blocks mental control and bodily contact by summoned good creatures.",
                    SpellLevel = 1,
                    School = "Abjuration",
                    ClassList = new[] { "Cleric", "Sorcerer", "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDeflectionBonus = 2,
                    BuffSaveBonus = 2,
                    BuffType = "protection",
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    BuffDurationRounds = 10,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.PROTECTION_FROM_LAW,
                    Name = "Protection from Law",
                    Description = "Wards against lawful creatures: +2 deflection AC and +2 resistance on saves vs lawful creatures; blocks mental control and bodily contact by summoned lawful creatures.",
                    SpellLevel = 1,
                    School = "Abjuration",
                    ClassList = new[] { "Cleric", "Sorcerer", "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDeflectionBonus = 2,
                    BuffSaveBonus = 2,
                    BuffType = "protection",
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    BuffDurationRounds = 10,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // ================================================================
        // Protection from Energy (D&D 3.5e PHB p.266)
        // Abjuration; Cleric 3, Druid 3, Ranger 2, Sorcerer/Wizard 3
        // Touch, 10 min/level or until discharged
        // Absorbs 12 pts/CL (max 120) of chosen energy type
        // ================================================================
        Register(new SpellData
                {
                    SpellId = SpellNames.PROTECTION_FROM_ENERGY,
                    Name = "Protection from Energy",
                    Description = "Grants temporary protection from one energy type (acid, cold, electricity, fire, or sonic). Absorbs 12 points of damage per caster level (max 120) before being discharged. PHB p.266",
                    SpellLevel = 3,
                    School = "Abjuration",
                    ClassList = new[] { "Cleric", "Druid", "Sorcerer", "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffType = "energy_protection",
                    DurationType = DurationType.Minutes,
                    DurationValue = 10,
                    DurationScalesWithLevel = true,
                    BuffDurationRounds = -1,
                    IsDismissible = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Fortitude",
                    SpellResistanceApplies = true
                });

        // Ranger gets it at level 2
        RegisterClassSpellAlias("protection_from_energy_rgr", SpellNames.PROTECTION_FROM_ENERGY, "Ranger", 2);

        // ── Prayer ──
        // D&D 3.5e PHB p.264: All allies gain +1 luck bonus on attack rolls,
        // weapon damage, saves, and skill checks. All enemies take –1 penalty
        // on same rolls. 40-ft-radius burst centered on caster. 1 round/level.
        // Note: Prayer is resolved via custom TryResolvePrayerSpellEffect because
        // it applies both buff (allies) and debuff (enemies) simultaneously.
        Register(new SpellData
                {
                    SpellId = SpellNames.PRAYER,
                    Name = "Prayer",
                    Description = "Allies gain +1 luck bonus on attack rolls, weapon damage rolls, saves, and skill checks. Enemies take –1 penalty on those same rolls. 40-ft-radius burst centered on caster. 1 round/level. PHB p.264",
                    SpellLevel = 3, School = "Enchantment",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Self, // Custom AoE handled in resolution
                    RangeSquares = 0, // Self-centered burst
                    AreaRadius = 8, // 40 ft = 8 squares
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 8,
                    AoERangeSquares = 0,
                    EffectType = SpellEffectType.Buff,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    AllowsSavingThrow = false,
                    SpellResistanceApplies = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.PURIFY_FOOD_DRINK,
                    Name = "Purify Food and Drink",
                    Description = "Purifies 1 cu.ft./level of food and water.",
                    SpellLevel = 0, School = "Transmutation",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 2,
                    EffectType = SpellEffectType.Buff,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.PHANTASMAL_KILLER,
                    Name = "Phantasmal Killer",
                    Description = "Illusion (Phantasm) [Fear, Mind-Affecting]. Creates a phantasmal image of the most fearsome creature imaginable to the target. Will disbelieve, then Fort or die (3d6 on successful Fort, shaken 1 round). SR: Yes. PHB p.260",
                    SpellLevel = 4, School = "Illusion",
                    ClassList = new[] { "Wizard", "Sorcerer" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Medium,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    IsMindAffecting = true,
                    DurationType = DurationType.Instantaneous,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.PYROTECHNICS,
                    Name = "Pyrotechnics",
                    Description = "Turns fire into blinding light or choking smoke. PHB p.267",
                    SpellLevel = 2, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 8,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    AreaRadius = 4,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Fire interaction not implemented]"
                });

        // ═══════════════════════════════════════════════════════════════
        // Poison — PHB p.262
        // School: Necromancy
        // Level: Cleric 4, Druid 3
        // Range: Touch
        // Target: Living creature touched
        // Duration: Instantaneous (initial + secondary in 1 min)
        // Saving Throw: Fortitude negates
        // Spell Resistance: Yes
        // ═══════════════════════════════════════════════════════════════
        Register(new SpellData
                {
                    SpellId = SpellNames.POISON,
                    Name = "Poison",
                    Description = "Touch attack poisons target. Fort DC 14 negates. Initial: 1d10 CON damage. Secondary (1 min later): 1d10 CON damage. PHB p.262",
                    SpellLevel = 4, School = "Necromancy",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    IsMeleeTouch = true,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Fortitude",
                    SpellResistanceApplies = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

                // ──────────────────────────────────────────────────────────────
        // PLANT GROWTH  (PHB p.262)
        // Transmutation
        // Level: Druid 3, Plant 3, Ranger 3
        // Casting Time: 1 standard action
        // Range: See text
        // Target/Area/Effect: See text
        // Duration: Instantaneous
        // Saving Throw: None
        // Spell Resistance: No
        //
        // Overgrowth version: all normal vegetation in 100-ft radius
        // becomes thick and overgrown. Movement quartered (×4 cost).
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.PLANT_GROWTH,
                    Name = "Plant Growth",
                    Description = "Vegetation in 100-ft radius becomes overgrown: movement quartered (×4 cost). Instantaneous. PHB p.262",
                    SpellLevel = 3,
                    School = "Transmutation",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Medium,
                    AreaRadius = 20,
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 20,
                    EffectType = SpellEffectType.Debuff,
                    DurationType = DurationType.Instantaneous,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // Backward-compat aliases for older domain IDs.
        RegisterAlias("domain_protection_from_chaos", SpellNames.PROTECTION_FROM_CHAOS);
        RegisterAlias("domain_protection_from_good", SpellNames.PROTECTION_FROM_GOOD);
        RegisterAlias("domain_protection_from_law", SpellNames.PROTECTION_FROM_LAW);

        // Legacy alias retained for existing prepared spell references.
        RegisterClassSpellAlias("protection_from_evil_clr", SpellNames.PROTECTION_FROM_EVIL, "Cleric", 1);

        // Phase 1: Paladin 1
        RegisterClassSpellAlias("protection_from_evil_pal", SpellNames.PROTECTION_FROM_EVIL, "Paladin", 1);

        // Purify Food and Drink: Druid 0
        RegisterClassSpellAlias("purify_food_drink_drd", SpellNames.PURIFY_FOOD_DRINK, "Druid", 0);

        // Prestidigitation: Bard 0
        RegisterClassSpellAlias("prestidigitation_brd", SpellNames.PRESTIDIGITATION, "Bard", 0);

        // ── Passwall (PHB p.259) ─────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.PASSWALL,
                    Name = "Passwall",
                    Description = "Transmutation. You create a passage through wooden, plaster, or stone walls, but not through "
                        + "metal or other harder materials. The passage is 5 ft wide, 8 ft tall, and 10 ft deep (plus 5 ft "
                        + "deep per 3 additional caster levels). Duration 1 hour/level (D). PHB p.259",
                    SpellLevel = 5,
                    School = "Transmutation",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Sorcerer", 5),
                        new SpellAvailability("Wizard", 5)
                    },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Utility,
                    AllowsSavingThrow = false,
                    SpellResistanceApplies = false,
                    DurationType = DurationType.Hours,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    IsDismissible = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    HasMaterialComponent = true, // M: sesame seeds
                    IsPlaceholder = false
                });

        // ── Persistent Image (PHB p.260) ─────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.PERSISTENT_IMAGE,
                    Name = "Persistent Image",
                    Description = "Illusion (Figment). As Major Image, except that the figment includes visual, auditory, olfactory, "
                        + "and thermal elements, and the spell is permanent — no concentration is required to maintain it. "
                        + "Will disbelief (if interacted with). Duration 1 min/level (D). PHB p.260",
                    SpellLevel = 5,
                    School = "Illusion",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Bard", 5),
                        new SpellAvailability("Sorcerer", 5),
                        new SpellAvailability("Wizard", 5)
                    },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Long,
                    EffectType = SpellEffectType.Illusion,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = false, // Will disbelief only if interacted with
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    IsDismissible = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    HasMaterialComponent = true, // M: a bit of fleece and jade dust worth 25 gp
                    IsPlaceholder = false
                });

        // ── PLANE SHIFT — PHB p.262 ──
        Register(new SpellData
                {
                    SpellId = SpellNames.PLANE_SHIFT,
                    Name = "Plane Shift",
                    Description = "Conjuration (Teleportation). Touch attack transports target to another plane of existence. "
                        + "Willing creatures are transported automatically. Unwilling targets get a Will save to negate. "
                        + "For combat purposes, target is removed from the battlefield. SR: Yes. PHB p.262",
                    SpellLevel = 7, School = "Conjuration",
                    ClassList = new[] { "Cleric", "Wizard", "Sorcerer" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    IsMeleeTouch = true,
                    EffectType = SpellEffectType.Control,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    DurationType = DurationType.Instantaneous,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        // ── PROTECTION FROM SPELLS — PHB p.266 ──
        Register(new SpellData
                {
                    SpellId = SpellNames.PROTECTION_FROM_SPELLS,
                    Name = "Protection from Spells",
                    Description = "Abjuration. Grants +8 resistance bonus on saving throws against spells and spell-like abilities. Affects 1 creature touched per 4 caster levels. Does not stack with other resistance bonuses. Duration: 10 min/level. Material: 500gp diamond. PHB p.266",
                    SpellLevel = 8, School = "Abjuration",
                    ClassList = new[] { "Wizard", "Sorcerer" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    IsMeleeTouch = true,
                    EffectType = SpellEffectType.Buff,
                    BuffSaveBonus = 8,
                    BuffBonusType = BonusType.Resistance,
                    BonusTypeExplicitlySet = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 10,
                    DurationScalesWithLevel = true,
                    IsDismissible = true,
                    AllowsSavingThrow = false,
                    SpellResistanceApplies = false,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    HasMaterialComponent = true, // M: diamond worth 500 gp that is consumed
                    IsPlaceholder = false
                });

    }
}
