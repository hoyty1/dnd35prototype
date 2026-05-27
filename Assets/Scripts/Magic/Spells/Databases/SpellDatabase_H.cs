// ============================================================================
// SpellDatabase_H.cs — Spells starting with H
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

public static partial class SpellDatabase
{
    private static void RegisterSpellsH()
    {
        // ──────────────────────────────────────────────────────────────
        // HEAT METAL  (PHB p.236)
        // Transmutation [Fire]
        // Level: Druid 2, Sun 2
        // Casting Time: 1 standard action
        // Range: Close (25 ft. + 5 ft./2 levels)
        // Target: Metal equipment of one creature per two levels
        // Duration: 7 rounds
        // Saving Throw: Will negates (object)
        // Spell Resistance: Yes (object)
        //
        // Escalating fire damage: Rd1 no dmg, Rd2 1d4, Rd3-4 2d4,
        // Rd5 2d4, Rd6 1d4, Rd7 no dmg.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.HEAT_METAL,
                    Name = "Heat Metal",
                    Description = "Heats metal equipment: escalating fire damage over 7 rounds (0/1d4/2d4/2d4/2d4/1d4/0). Will negates (object). PHB p.236",
                    SpellLevel = 2,
                    School = "Transmutation",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Damage,
                    DamageType = "fire",
                    DurationType = DurationType.Rounds,
                    DurationValue = 7,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // ──────────────────────────────────────────────────────────────
        // HALT UNDEAD  (PHB p.239)
        // Necromancy
        // Level: Sor/Wiz 3
        // Components: V, S, M (a pinch of sulfur and powdered garlic)
        // Casting Time: 1 standard action
        // Range: Medium (100 ft. + 10 ft./level)
        // Targets: Up to three undead, no two of which can be more than
        //          30 ft. apart
        // Duration: 1 round/level
        // Saving Throw: Will negates (see text)
        // Spell Resistance: Yes
        //
        // This spell renders as many as three undead creatures immobile.
        // A nonintelligent undead creature gets no saving throw; an
        // intelligent undead creature does. If the spell is successful,
        // it renders the undead creature immobile for the duration of
        // the spell (similar to the effect of hold person on a living
        // creature). The effect is broken if the halted creatures are
        // attacked or take damage.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.HALT_UNDEAD,
                    Name = "Halt Undead",
                    Description = "Necromancy. Renders up to three undead creatures immobile (paralyzed) for 1 round/level. "
                        + "Targets cannot be more than 30 ft. apart. Nonintelligent undead get no save; "
                        + "intelligent undead get a Will save to negate. SR: Yes. PHB p.239",
                    SpellLevel = 3,
                    School = "Necromancy",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Sorcerer", 3),
                        new SpellAvailability("Wizard", 3)
                    },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Medium,
                    AoEShapeType = AoEShape.Burst,
                    // 30 ft between any two = 6 squares; using radius 3 ensures
                    // any two affected creatures fall within a 30-ft diameter span.
                    AoESizeSquares = 3,
                    AoERangeSquares = 0, // use Medium range profile
                    // Halt Undead can target any undead — but in our combat the typical caster
                    // wants to halt enemies. Using EnemiesOnly avoids incidentally paralyzing
                    // friendly summoned undead. Resolution method also filters to undead only.
                    AoEFilter = AoETargetFilter.EnemiesOnly,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    BuffDurationRounds = 1, // legacy fallback
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.HIDE_FROM_UNDEAD,
                    Name = "Hide from Undead",
                    Description = "Undead can't perceive the subject. Mindless undead are automatically affected; intelligent undead get a Will save. If the subject attacks, the spell ends for that subject. Duration 10 min/level. PHB p.241",
                    SpellLevel = 1, School = "Abjuration",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    DurationType = DurationType.Minutes,
                    DurationValue = 10,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.HIDEOUS_LAUGHTER,
                    Name = "Tasha's Hideous Laughter",
                    Description = "Enchantment (Compulsion) [Mind-Affecting]. Subject collapses into laughter, falls prone, and cannot take actions (not helpless). Int 2 or less is immune. Different creature type than caster gets +4 on save. Will negates. SR applies. Duration 1 round/level. PHB p.240",
                    SpellLevel = 2, School = "Enchantment",
                    ClassList = new[] { "Wizard", "Sorcerer", "Bard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    IsMindAffecting = true,
                    BlockedByProtectionFromAlignment = false,
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    BuffDurationRounds = 1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // ──────────────────────────────────────────────────────────────
        // HOLD ANIMAL  (PHB p.241)
        // Enchantment (Compulsion) [Mind-Affecting]
        // Level: Animal 2, Druid 2, Ranger 2
        // Casting Time: 1 standard action
        // Range: Medium (100 ft. + 10 ft./level)
        // Target: One animal
        // Duration: 1 round/level (D); see text
        // Saving Throw: Will negates; see text
        // Spell Resistance: Yes
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.HOLD_ANIMAL,
                    Name = "Hold Animal",
                    Description = "Paralyzes one animal for 1 round/level. Will negates; new save each round with cumulative +2 bonus. PHB p.241",
                    SpellLevel = 2,
                    School = "Enchantment",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Medium,
                    EffectType = SpellEffectType.Debuff,
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // ──────────────────────────────────────────────────────────────
        // HOLD PERSON  (PHB p.241)
        // Enchantment (Compulsion) [Mind-Affecting]
        // Level: Bard 2, Cleric 2, Sor/Wiz 3
        // Components: V, S, F/DF (a small, straight piece of iron)
        // Casting Time: 1 standard action
        // Range: Medium (100 ft. + 10 ft./level)
        // Target: One humanoid creature
        // Duration: 1 round/level (D); see text
        // Saving Throw: Will negates; see text
        // Spell Resistance: Yes
        //
        // The subject becomes paralyzed and freezes in place. It is
        // aware and breathes normally but cannot take any actions, even
        // speech. Each round on its turn, the subject may attempt a new
        // saving throw to end the effect (+2 on the Will save each
        // successive round). This is a full-round action that does not
        // provoke attacks of opportunity.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.HOLD_PERSON,
                    Name = "Hold Person",
                    Description = "Enchantment (Compulsion) [Mind-Affecting]. Paralyzes one humanoid for 1 round/level. "
                        + "Target is aware and breathes normally but cannot move or act. "
                        + "Each round, target gets a new Will save with cumulative +2 bonus to break free. "
                        + "Will negates. SR: Yes. Duration 1 round/level (D). PHB p.241",
                    SpellLevel = 2,
                    School = "Enchantment",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Bard", 2),
                        new SpellAvailability("Cleric", 2),
                        new SpellAvailability("Sorcerer", 3),
                        new SpellAvailability("Wizard", 3)
                    },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Medium,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    IsMindAffecting = true,
                    BlockedByProtectionFromAlignment = true,
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    IsDismissible = true,
                    BuffDurationRounds = 1, // Legacy fallback: 1 round/level
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.HOLD_PORTAL,
                    Name = "Hold Portal",
                    Description = "Holds door shut as if locked. Duration 1 min/level. PHB p.241",
                    SpellLevel = 1, School = "Abjuration",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 22,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = 30,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Door mechanics not implemented]"
                });

        // ──────────────────────────────────────────────────────────────
        // Hypnotic Pattern — PHB p.242
        // Illusion (Pattern) [Mind-Affecting]
        // Level: Bard 2, Sorcerer/Wizard 2
        // Components: V (Bard only), S, M (a glowing stick of incense
        //   or a crystal rod filled with phosphorescent material)
        // Casting Time: 1 standard action
        // Range: Medium (100 ft. + 10 ft./level)
        // Effect: Colorful lights in a 10-ft.-radius spread
        // Duration: Concentration + 2 rounds
        // Saving Throw: Will negates
        // Spell Resistance: Yes
        //
        // A twisting pattern of subtle, shifting colors weaves through
        // the air, fascinating creatures within it. Roll 2d4 and add
        // your caster level to determine the total number of Hit Dice
        // of creatures affected. Creatures with the fewest HD are
        // affected first; and, among creatures with equal HD, those
        // who are closest to the spell's point of origin are affected
        // first. HD that are not sufficient to affect a creature are
        // wasted. Affected creatures become fascinated by the pattern
        // of colors. Sightless creatures are not affected.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.HYPNOTIC_PATTERN,
                    Name = "Hypnotic Pattern",
                    Description = "Illusion (Pattern) [Mind-Affecting]. A twisting pattern of subtle, shifting colors weaves through the air, fascinating creatures within it. Roll 2d4 and add your caster level to determine the total number of Hit Dice of creatures affected. Creatures with the fewest HD are affected first. Affected creatures become fascinated. Sightless creatures are not affected. Will negates. SR: Yes. Duration: Concentration + 2 rounds. Components: V (Bard only), S, M (a glowing stick of incense or a crystal rod filled with phosphorescent material). PHB p.242",
                    SpellLevel = 2,
                    School = "Illusion",
                    ClassList = new[] { "Wizard", "Sorcerer", "Bard" },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Medium,
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 2, // 10-ft.-radius spread
                    AoERangeSquares = 0, // use Medium range profile
                    AoEFilter = AoETargetFilter.EnemiesOnly,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    IsMindAffecting = true,
                    BlockedByProtectionFromAlignment = false,
                    DurationType = DurationType.Concentration,
                    DurationValue = 2, // +2 rounds after concentration ends
                    DurationScalesWithLevel = false,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.HYPNOTISM,
                    Name = "Hypnotism",
                    Description = "Enchantment (Compulsion) [Mind-Affecting]. Fascinates creatures in a 15-ft radius burst with a 2d4 HD pool, lowest HD first. Targets must be able to see or hear you. Will negates. Duration 2d4 rounds. SR: Yes. PHB p.242",
                    SpellLevel = 1,
                    School = "Enchantment",
                    ClassList = new[] { "Wizard", "Bard" },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Close,
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 3, // 15-ft radius burst
                    AoERangeSquares = 0, // use Close range profile
                    AoEFilter = AoETargetFilter.EnemiesOnly,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    IsMindAffecting = true,
                    BlockedByProtectionFromAlignment = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    DurationType = DurationType.Rounds,
                    DurationValue = 0, // resolved at runtime (2d4 rounds)
                    DurationScalesWithLevel = false,
                    IsPlaceholder = false
                });

        // ──────────────────────────────────────────────────────────────
        // HEROISM  (PHB p.240)
        // Enchantment (Compulsion) [Mind-Affecting]
        // Level: Bard 2, Sor/Wiz 3
        // Components: V, S
        // Casting Time: 1 standard action
        // Range: Touch
        // Target: Creature touched
        // Duration: 10 min./level
        // Saving Throw: Will negates (harmless)
        // Spell Resistance: Yes (harmless)
        //
        // This spell imbues a single creature with great bravery and
        // morale in battle. The target gains a +2 morale bonus on
        // attack rolls, saves, and skill checks.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.HEROISM,
                    Name = "Heroism",
                    Description = "Enchantment (Compulsion) [Mind-Affecting]. Imbues a single creature with great bravery. "
                        + "The target gains a +2 morale bonus on attack rolls, saves, and skill checks. "
                        + "Duration 10 min/level. Will negates (harmless). SR: Yes (harmless). PHB p.240",
                    SpellLevel = 3,
                    School = "Enchantment",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Bard", 2),
                        new SpellAvailability("Sorcerer", 3),
                        new SpellAvailability("Wizard", 3)
                    },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    IsMeleeTouch = true,
                    EffectType = SpellEffectType.Buff,
                    BuffAttackBonus = 2,
                    BuffSaveBonus = 2,
                    BuffDamageBonus = 0,
                    BuffType = "morale",
                    BuffBonusType = BonusType.Morale,
                    BonusTypeExplicitlySet = true,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    IsMindAffecting = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 10,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = false
                });

        // ──────────────────────────────────────────────────────────────
        // HASTE  (PHB p.239)
        // Transmutation
        // Level: Brd 3, Sor/Wiz 3
        // Components: V, S, M (a shaving of licorice root)
        // Casting Time: 1 standard action
        // Range: Close (25 ft. + 5 ft./2 levels)
        // Targets: One creature/level, no two of which can be more than
        //          30 ft. apart
        // Duration: 1 round/level
        // Saving Throw: Fortitude negates (harmless)
        // Spell Resistance: Yes (harmless)
        //
        // The transmuted creatures move and act more quickly than normal.
        // This extra speed has several effects:
        //   • +1 bonus on attack rolls
        //   • +1 dodge bonus to AC and Reflex saves
        //   • When making a full attack action, hasted creature may make
        //     one extra attack with any weapon he is holding. The attack
        //     is made using the creature's full base attack bonus, plus
        //     any modifiers appropriate to the situation. (This effect is
        //     not cumulative with similar effects, such as that provided
        //     by a speed weapon, nor does it actually grant an extra action.)
        //   • All of the hasted creature's modes of movement (including
        //     land movement, burrow, climb, fly, and swim) increase by
        //     30 feet, to a maximum of twice the subject's normal speed.
        //   • Haste dispels and counters slow.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.HASTE,
                    Name = "Haste",
                    Description = "Transmutation. Transmuted creatures move and act more quickly. "
                        + "+1 attack rolls, +1 dodge bonus to AC and Reflex saves, "
                        + "+30 ft. movement speed, one extra attack at full BAB on full attack action. "
                        + "Haste dispels and counters Slow. "
                        + "Duration 1 round/level. Fort negates (harmless). SR: Yes (harmless). PHB p.239",
                    SpellLevel = 3,
                    School = "Transmutation",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Bard", 3),
                        new SpellAvailability("Sorcerer", 3),
                        new SpellAvailability("Wizard", 3)
                    },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Buff,
                    BuffAttackBonus = 1,
                    BuffACBonus = 1,
                    BuffSaveBonus = 1,
                    BuffSpeedBonusFeet = 30,
                    BuffType = "haste",
                    BuffBonusType = BonusType.Untyped,
                    BonusTypeExplicitlySet = true,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Fortitude",
                    SpellResistanceApplies = true,
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        // ═══════════════════════════════════════════════════════════════
        // Holy Smite — PHB p.241
        // School: Evocation [Good]
        // Level: Cleric 4 (Good domain 4)
        // Range: Medium
        // Area: 20-ft-radius burst
        // Duration: Instantaneous (1 round for blindness)
        // Saving Throw: Will partial
        // Spell Resistance: Yes
        // ═══════════════════════════════════════════════════════════════
        Register(new SpellData
                {
                    SpellId = SpellNames.HOLY_SMITE,
                    Name = "Holy Smite",
                    Description = "Burst of holy power: 1d8/2 CL (max 5d8) vs evil creatures + blinded 1 round. Will half damage and negates blind. PHB p.241",
                    SpellLevel = 4, School = "Evocation",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Medium,
                    EffectType = SpellEffectType.Damage,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SaveHalves = true,
                    SpellResistanceApplies = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // ── Hold Monster (PHB p.241) ──────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.HOLD_MONSTER,
                    Name = "Hold Monster",
                    Description = "Enchantment (Compulsion) [Mind-Affecting]. As Hold Person, except that it affects any living creature. "
                        + "The target is paralyzed and frozen in place, aware and breathing but unable to take any actions. "
                        + "Each round on its turn, the subject may attempt a new Will saving throw with a cumulative +2 bonus "
                        + "to end the effect. Will negates. SR: Yes. Duration 1 round/level (D). PHB p.241",
                    SpellLevel = 5,
                    School = "Enchantment",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Bard", 4),
                        new SpellAvailability("Sorcerer", 5),
                        new SpellAvailability("Wizard", 5)
                    },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Medium,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    IsMindAffecting = true,
                    BlockedByProtectionFromAlignment = true,
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    IsDismissible = true,
                    BuffDurationRounds = 1, // Legacy fallback: 1 round/level
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    HasMaterialComponent = true, // M: one hard metal bar or rod
                    IsPlaceholder = false
                });

        // Aliases / class-level variants
        RegisterAlias(SpellNames.HIDEOUS_LAUGHTER_LEGACY, SpellNames.HIDEOUS_LAUGHTER);
        RegisterClassSpellAlias("tashas_hideous_laughter_brd", SpellNames.HIDEOUS_LAUGHTER, "Bard", 1);
        RegisterClassSpellAlias("hideous_laughter_brd", SpellNames.HIDEOUS_LAUGHTER, "Bard", 1);

        // ── HEAL — PHB p.239 ──
        Register(new SpellData
                {
                    SpellId = SpellNames.HEAL,
                    Name = "Heal",
                    Description = "Conjuration (Healing). Heals 10 HP/CL (max 150). Cures: ability damage, blinded, confused, dazed, dazzled, deafened, diseased, exhausted, fatigued, feebleminded, insanity, nauseated, sickened, stunned, poisoned. Does NOT cure negative levels. No effect on undead (use Harm). PHB p.239",
                    SpellLevel = 6, School = "Conjuration",
                    ClassList = new[] { "Cleric", "Druid" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    IsMeleeTouch = true,
                    EffectType = SpellEffectType.Healing,
                    HealDice = 1, // Handled manually — 10 HP/level, max 150
                    HealCount = 1,
                    BonusHealing = 0,
                    DurationType = DurationType.Instantaneous,
                    AllowsSavingThrow = false,
                    SpellResistanceApplies = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        // ── Phase 1: Bard/Paladin/Ranger/Druid class assignments ──

        // Haste: Bard 3
        RegisterClassSpellAlias("haste_brd", SpellNames.HASTE, "Bard", 3);

        // Hold Person: Bard 2
        RegisterClassSpellAlias("hold_person_brd", SpellNames.HOLD_PERSON, "Bard", 2);


        // Backward-compatibility aliases for consolidated domain spell IDs
        RegisterAlias("domain_heat_metal", SpellNames.HEAT_METAL);
        RegisterAlias("domain_hold_animal", SpellNames.HOLD_ANIMAL);

    }
}
