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
        Register(new SpellData
                {
                    SpellId = SpellNames.DOMAIN_HEAT_METAL,
                    Name = "Heat Metal",
                    Description = "Make metal intensely hot. Creatures wearing metal armor take 1d4 to 2d4 fire damage per round.",
                    SpellLevel = 2,
                    School = "Transmutation",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeSquares = 5,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 4,
                    DamageCount = 2,
                    DamageType = "fire",
                    BuffDurationRounds = 7,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Ongoing damage over rounds not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.HIDE_FROM_UNDEAD,
                    Name = "Hide from Undead",
                    Description = "Undead can't perceive one subject/level. Duration 10 min/level. Will negates (intelligent undead). PHB p.241",
                    SpellLevel = 1, School = "Abjuration",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Undead perception not implemented]"
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

        Register(new SpellData
                {
                    SpellId = SpellNames.DOMAIN_HOLD_ANIMAL,
                    Name = "Hold Animal",
                    Description = "Paralyzes one animal for 1 round/level.",
                    SpellLevel = 2,
                    School = "Enchantment",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeSquares = 6,
                    EffectType = SpellEffectType.Debuff,
                    BuffDurationRounds = 30,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Hold/paralyze not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.HOLD_PERSON,
                    Name = "Hold Person",
                    Description = "Paralyzes one humanoid for 1 round/level. Will negates. PHB p.241",
                    SpellLevel = 2, School = "Enchantment",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Medium,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    BuffDurationRounds = 3, // 1 round/level
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
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

        // Aliases / class-level variants
        RegisterAlias(SpellNames.HIDEOUS_LAUGHTER_LEGACY, SpellNames.HIDEOUS_LAUGHTER);
        RegisterClassSpellAlias("tashas_hideous_laughter_brd", SpellNames.HIDEOUS_LAUGHTER, "Bard", 1);
        RegisterClassSpellAlias("hideous_laughter_brd", SpellNames.HIDEOUS_LAUGHTER, "Bard", 1);

    }
}
