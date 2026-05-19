// ============================================================================
// SpellDatabase_G.cs — Spells starting with G
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

public static partial class SpellDatabase
{
    private static void RegisterSpellsG()
    {
        Register(new SpellData
                {
                    SpellId = SpellNames.GENTLE_REPOSE,
                    Name = "Gentle Repose",
                    Description = "Preserves a corpse. Duration 1 day/level. PHB p.235",
                    SpellLevel = 2, School = "Necromancy",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Corpse preservation not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.GHOST_SOUND,
                    Name = "Ghost Sound",
                    Description = "Figment sounds. Will disbelief (if interacted with).",
                    SpellLevel = 0, School = "Illusion",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 5,
                    EffectType = SpellEffectType.Buff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    BuffDurationRounds = 3,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Illusion mechanics not implemented]"
                });

        // ──────────────────────────────────────────────────────────────
        // GHOUL TOUCH  (PHB p.235)
        // Necromancy
        // Level: Sor/Wiz 2
        // Components: V, S, M (cloth from a ghoul or earth from a ghoul's lair)
        // Casting Time: 1 standard action
        // Range: Touch
        // Target: Living humanoid touched
        // Duration: 1d6+2 rounds
        // Saving Throw: Fortitude negates
        // Spell Resistance: Yes
        //
        // Imbues the caster's hand with negative energy. On a successful
        // melee touch attack against a living humanoid, the target must
        // make a Fort save or become paralyzed for 1d6+2 rounds.
        // A paralyzed target exudes a carrion stench in a 10-ft radius
        // that sickens nearby living creatures (Fort negates, poison effect).
        // No recurring saves (unlike Hold Person).
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.GHOUL_TOUCH,
                    Name = "Ghoul Touch",
                    Description = "Necromancy. Imbues your hand with negative energy. Melee touch attack paralyzes one living humanoid for 1d6+2 rounds (Fort negates). Paralyzed target exudes carrion stench in 10-ft radius that sickens living creatures (Fort negates, poison effect). No recurring saves. Components: V, S, M (cloth from a ghoul or earth from a ghoul's lair). PHB p.235",
                    SpellLevel = 2, School = "Necromancy",
                    ClassList = new[] { "Wizard", "Sorcerer" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    IsMeleeTouch = true,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Fortitude",
                    SpellResistanceApplies = true,
                    BuffDurationRounds = 5, // Placeholder — actual duration is 1d6+2 rolled at cast time
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.TEST_CONE_60,
                    Name = "Glacial Blast (60-ft Cone)",
                    Description = "TEST SPELL: 10d6 cold damage in a 60-ft cone. Reflex half. For testing 60-ft cone AoE pattern.",
                    SpellLevel = 2, School = "Evocation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Area,
                    RangeSquares = 12,
                    AreaRadius = 12,
                    AoEShapeType = AoEShape.Cone,
                    AoESizeSquares = 12, // 60 ft = 12 squares length
                    AoERangeSquares = 0, // Cone originates from caster
                    AoEFilter = AoETargetFilter.All,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 6, DamageCount = 10, // 10d6 cold
                    DamageType = "cold",
                    AllowsSavingThrow = true,
                    SavingThrowType = "Reflex",
                    SaveHalves = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.GLITTERDUST,
                    Name = "Glitterdust",
                    Description = "Conjuration (Creation). Golden particles outline creatures and objects in a 10-ft radius spread. Will negates blindness only. Outlined targets lose invisibility concealment and take -40 Hide. Duration 1 round/level. Components: V, S, M (ground mica). PHB p.236",
                    SpellLevel = 2,
                    School = "Conjuration",
                    ClassList = new[] { "Wizard", "Sorcerer", "Bard" },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Medium,
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 2,
                    AoERangeSquares = 0,
                    AreaRadius = 2,
                    AoEFilter = AoETargetFilter.All,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = false,
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.GREASE,
                    Name = "Grease",
                    Description = "Object or 10-ft-square area becomes slippery. Reflex save to avoid falling when first affected. Balance checks while traversing. Duration 1 round/level. Components: V, S, M (butter/pork rind). PHB p.237",
                    HasMaterialComponent = true, // M: butter or pork rind (common — covered by spell component pouch)
                    SpellLevel = 1, School = "Conjuration",
                    ClassList = new[] { "Wizard", "Sorcerer", "Bard" },
                    TargetType = SpellTargetType.SingleEnemy, // Runtime prompt supports object mode (single target) or area mode
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Reflex",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // ──────────────────────────────────────────────────────────────
        // GREATER MAGIC WEAPON  (PHB p.251)
        // Transmutation
        // Level: Clr 4, Pal 3, Sor/Wiz 3
        // Components: V, S, M/DF (powdered lime and carbon)
        // Casting Time: 1 standard action
        // Range: Close (25 ft. + 5 ft./2 levels)
        // Target: One weapon or fifty projectiles (all in contact)
        // Duration: 1 hour/level
        // Saving Throw: Will negates (harmless, object)
        // Spell Resistance: Yes (harmless, object)
        //
        // Gives a weapon an enhancement bonus of +1 per four caster
        // levels (maximum +5). Cannot create a weapon with an effective
        // bonus higher than +5 from enhancement alone.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.GREATER_MAGIC_WEAPON,
                    Name = "Greater Magic Weapon",
                    Description = "Transmutation. Gives weapon +1 enhancement bonus per 4 caster levels (max +5 at CL 20). "
                        + "Duration 1 hour/level. Components: V, S, M/DF (powdered lime and carbon). PHB p.251",
                    SpellLevel = 3,
                    School = "Transmutation",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Cleric", 4),
                        new SpellAvailability("Paladin", 3),
                        new SpellAvailability("Sorcerer", 3),
                        new SpellAvailability("Wizard", 3)
                    },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Buff,
                    DurationType = DurationType.Hours,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    BuffDurationRounds = 600,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.GUIDANCE,
                    Name = "Guidance",
                    Description = "+1 on one attack roll, saving throw, or skill check. Duration 1 minute or until discharged.",
                    SpellLevel = 0, School = "Divination",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffAttackBonus = 1,
                    BuffSaveBonus = 1,
                    BuffDurationRounds = 10,
                    BuffType = "competence",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.GUST_OF_WIND,
                    Name = "Gust of Wind",
                    Description = "Evocation [Air]. Line-shaped blast of severe wind in a 60-ft line. Fortitude negates size-based effects; also disperses fog, mist, and similar vapors in its path. PHB p.238",
                    SpellLevel = 2, School = "Evocation",
                    ClassList = new[] { "Wizard", "Sorcerer", "Druid" },
                    TargetType = SpellTargetType.Area,
                    RangeSquares = 12,
                    AreaRadius = 12,
                    AoEShapeType = AoEShape.Line,
                    AoESizeSquares = 12,
                    AoERangeSquares = 0,
                    AoEFilter = AoETargetFilter.All,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Fortitude",
                    SpellResistanceApplies = true,
                    BuffDurationRounds = 1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.GREATER_INVISIBILITY,
                    Name = "Greater Invisibility",
                    Description = "Illusion (Glamer). Subject is invisible and can attack without breaking invisibility. +2 attack, enemies denied Dex to AC, 50% miss chance. 1 round/level (D). PHB p.245",
                    SpellLevel = 4, School = "Illusion",
                    ClassList = new[] { "Wizard", "Sorcerer" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    EffectType = SpellEffectType.Buff,
                    AllowsSavingThrow = false,
                    SpellResistanceApplies = false,
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    IsDismissible = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true
                });

        RegisterClassSpellAlias("greater_invisibility_brd", SpellNames.GREATER_INVISIBILITY, "Bard", 4);

        // ═══════════════════════════════════════════════════════════════
        // Glyph of Warding — PHB p.236
        // School: Abjuration
        // Level: Cleric 3
        // Components: V, S, M (200 gp powdered diamond)
        // Casting Time: 10 minutes
        // Range: Touch
        // Target/Area: Object touched or up to 5 sq. ft./level
        // Duration: Permanent until discharged (D)
        // Saving Throw: See text
        // Spell Resistance: No (object); Yes (see text)
        //
        // An inscribed glyph harms those who enter, pass, or open the
        // warded area or object. Blast glyph deals 1d8/2 caster levels
        // (max 5d8) acid/cold/fire/electricity/sonic damage. Reflex half.
        // ═══════════════════════════════════════════════════════════════
        Register(new SpellData
                {
                    SpellId = SpellNames.GLYPH_OF_WARDING,
                    Name = "Glyph of Warding",
                    Description = "Inscribes a glyph that deals 1d8/2 CL (max 5d8) energy damage when triggered. Reflex half. 200 gp diamond dust. PHB p.236",
                    SpellLevel = 3, School = "Abjuration",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 8, DamageCount = 2, BonusDamage = 0, // 1d8/2 CL (2d8 at CL4-5)
                    DamageType = "fire",
                    AllowsSavingThrow = true,
                    SavingThrowType = "Reflex",
                    SaveHalves = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Trap/glyph placement system not implemented; registered for spell preparation]"
                });

        // ═══════════════════════════════════════════════════════════════
        // Giant Vermin — PHB p.235
        // School: Transmutation
        // Level: Cleric 4, Druid 4
        // Range: Close
        // Target: 1-3 vermin (no bigger than Medium)
        // Duration: 1 min/level
        // Saving Throw: None
        // Spell Resistance: Yes
        // ═══════════════════════════════════════════════════════════════
        Register(new SpellData
                {
                    SpellId = SpellNames.GIANT_VERMIN,
                    Name = "Giant Vermin",
                    Description = "Turns 1-3 centipedes, scorpions, or spiders into their giant counterparts. 1 min/level. PHB p.235",
                    SpellLevel = 4, School = "Transmutation",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Buff,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1, // 1 min/level
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Vermin size increase / creature transformation system not implemented]"
                });

    }
}
