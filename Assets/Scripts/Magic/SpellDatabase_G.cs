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
                    Description = "Object or 10-ft-square area becomes slippery. Reflex save to avoid falling when first affected. Balance checks while traversing. Duration 1 round/level. PHB p.237",
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

    }
}
