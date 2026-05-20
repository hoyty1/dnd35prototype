// ============================================================================
// SpellDatabase_M.cs — Spells starting with M
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

public static partial class SpellDatabase
{
    private static void RegisterSpellsM()
    {
        Register(new SpellData
                {
                    SpellId = SpellNames.MAGE_ARMOR,
                    Name = "Mage Armor",
                    HasMaterialComponent = true, // M: a piece of cured leather (common — covered by spell component pouch)
                    Description = "+4 armor bonus to AC for 1 hour/level. Doesn't stack with actual armor. Components: V, S, F (cured leather). PHB p.249",
                    SpellLevel = 1, School = "Conjuration",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffACBonus = 4,
                    BuffDurationRounds = -1, // Legacy
                    BuffType = "armor",
                    BuffBonusType = BonusType.Armor,
                    BonusTypeExplicitlySet = true,
                    DurationType = DurationType.Hours,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.MAGE_HAND,
                    Name = "Mage Hand",
                    Description = "5-pound telekinesis. Move one nonmagical, unattended object up to 5 lb.",
                    SpellLevel = 0, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 5,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Telekinesis not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.MAGIC_MISSILE,
                    Name = "Magic Missile",
                    Description = "1d4+1 force damage per missile, auto-hit. 2 missiles at CL3. No save, no SR.",
                    SpellLevel = 1, School = "Evocation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    // Medium range (100 ft + 10 ft/level)
                    RangeCategory = SpellRangeCategory.Medium,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 4, DamageCount = 1, BonusDamage = 1,
                    DamageType = "force",
                    AutoHit = true,
                    MissileCount = 2, // CL3
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.MELFS_ACID_ARROW,
                    Name = "Melf's Acid Arrow",
                    Description = "Ranged touch attack deals 2d4 acid immediately, then 2d4 acid each round for 1 + 1/3 caster levels (max 7 rounds total at CL 18). No save, no SR.",
                    SpellLevel = 2,
                    School = "Conjuration (Creation) [Acid]",
                    ClassList = new[] { "Wizard", "Sorcerer" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Long,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 4,
                    DamageCount = 2,
                    DamageType = "acid",
                    IsTouch = true,
                    IsRangedTouch = true,
                    AllowsSavingThrow = false,
                    SpellResistanceApplies = false,
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    DurationScalesWithLevel = false,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.DOMAIN_MAGIC_STONE,
                    Name = "Magic Stone",
                    Description = "Up to 3 pebbles gain +1 enhancement bonus to attack rolls and deal 1d6+1 damage. Used as sling ammunition. Counts as magic weapon. Duration 30 minutes or until discharged. PHB p.251",
                    SpellLevel = 1,
                    School = "Transmutation",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = 300, // Legacy fallback: 30 minutes
                    BuffType = "magic_stone",
                    DurationType = DurationType.Minutes,
                    DurationValue = 30,
                    DurationScalesWithLevel = false,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = false
                });

        // =================================================================
        // Magic Circle against Evil (PHB p.249)
        // Abjuration [Good] — Cleric 3, Paladin 3, Sorcerer/Wizard 3
        // =================================================================
        Register(new SpellData
                {
                    SpellId = SpellNames.MAGIC_CIRCLE_AGAINST_EVIL,
                    Name = "Magic Circle against Evil",
                    Description = "10-ft radius emanation from touched creature wards against evil: +2 deflection AC, +2 resistance saves vs evil creatures; blocks mental control and bodily contact by summoned evil creatures. Duration 10 min/level. PHB p.249",
                    SpellLevel = 3,
                    School = "Abjuration [Good]",
                    ClassList = new[] { "Cleric", "Paladin", "Sorcerer", "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDeflectionBonus = 2,
                    BuffSaveBonus = 2,
                    BuffType = "protection",
                    BuffBonusType = BonusType.Deflection,
                    BonusTypeExplicitlySet = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 10,
                    DurationScalesWithLevel = true,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = false,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // =================================================================
        // Magic Circle against Good (PHB p.249)
        // Abjuration [Evil] — Cleric 3, Sorcerer/Wizard 3 (NOT Paladin)
        // =================================================================
        Register(new SpellData
                {
                    SpellId = SpellNames.MAGIC_CIRCLE_AGAINST_GOOD,
                    Name = "Magic Circle against Good",
                    Description = "10-ft radius emanation from touched creature wards against good: +2 deflection AC, +2 resistance saves vs good creatures; blocks mental control and bodily contact by summoned good creatures. Duration 10 min/level. PHB p.249",
                    SpellLevel = 3,
                    School = "Abjuration [Evil]",
                    ClassList = new[] { "Cleric", "Sorcerer", "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDeflectionBonus = 2,
                    BuffSaveBonus = 2,
                    BuffType = "protection",
                    BuffBonusType = BonusType.Deflection,
                    BonusTypeExplicitlySet = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 10,
                    DurationScalesWithLevel = true,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = false,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // =================================================================
        // Magic Circle against Law (PHB p.249)
        // Abjuration [Chaotic] — Cleric 3, Sorcerer/Wizard 3 (NOT Paladin)
        // =================================================================
        Register(new SpellData
                {
                    SpellId = SpellNames.MAGIC_CIRCLE_AGAINST_LAW,
                    Name = "Magic Circle against Law",
                    Description = "10-ft radius emanation from touched creature wards against lawful: +2 deflection AC, +2 resistance saves vs lawful creatures; blocks mental control and bodily contact by summoned lawful creatures. Duration 10 min/level. PHB p.249",
                    SpellLevel = 3,
                    School = "Abjuration [Chaotic]",
                    ClassList = new[] { "Cleric", "Sorcerer", "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDeflectionBonus = 2,
                    BuffSaveBonus = 2,
                    BuffType = "protection",
                    BuffBonusType = BonusType.Deflection,
                    BonusTypeExplicitlySet = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 10,
                    DurationScalesWithLevel = true,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = false,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // =================================================================
        // Magic Circle against Chaos (PHB p.249)
        // Abjuration [Lawful] — Cleric 3, Paladin 3, Sorcerer/Wizard 3
        // =================================================================
        Register(new SpellData
                {
                    SpellId = SpellNames.MAGIC_CIRCLE_AGAINST_CHAOS,
                    Name = "Magic Circle against Chaos",
                    Description = "10-ft radius emanation from touched creature wards against chaotic: +2 deflection AC, +2 resistance saves vs chaotic creatures; blocks mental control and bodily contact by summoned chaotic creatures. Duration 10 min/level. PHB p.249",
                    SpellLevel = 3,
                    School = "Abjuration [Lawful]",
                    ClassList = new[] { "Cleric", "Paladin", "Sorcerer", "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDeflectionBonus = 2,
                    BuffSaveBonus = 2,
                    BuffType = "protection",
                    BuffBonusType = BonusType.Deflection,
                    BonusTypeExplicitlySet = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 10,
                    DurationScalesWithLevel = true,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = false,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.MAGIC_WEAPON,
                    Name = "Magic Weapon",
                    Description = "Weapon touched gains +1 enhancement bonus on attack and damage rolls and counts as magic for bypass. Duration 1 min/level. PHB p.251",
                    SpellLevel = 1, School = "Transmutation",
                    ClassList = new[] { "Wizard", "Sorcerer", "Cleric", "Paladin" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffType = "enhancement",
                    BuffBonusType = BonusType.Enhancement,
                    BonusTypeExplicitlySet = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // ──────────────────────────────────────────────────────────────
        // MAGIC VESTMENT  (PHB p.251)
        // Transmutation
        // Level: Cleric 3, Strength 3, War 3
        // Components: V, S, DF
        // Casting Time: 1 standard action
        // Range: Touch
        // Target: Armor or shield touched
        // Duration: 1 hour/level
        // Saving Throw: Will negates (harmless, object)
        // Spell Resistance: Yes (harmless, object)
        //
        // You imbue a suit of armor or a shield with an enhancement
        // bonus of +1 per four caster levels (maximum +5 at 20th level).
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.MAGIC_VESTMENT,
                    Name = "Magic Vestment",
                    Description = "Armor or shield gains +1 enhancement bonus per 4 caster levels (max +5). Duration 1 hour/level. PHB p.251",
                    SpellLevel = 3,
                    School = "Transmutation",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    EffectType = SpellEffectType.Buff,
                    BuffType = "enhancement",
                    BuffBonusType = BonusType.Enhancement,
                    BonusTypeExplicitlySet = true,
                    DurationType = DurationType.Hours,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.MAKE_WHOLE,
                    Name = "Make Whole",
                    Description = "Repairs an object of up to 10 cu.ft./level. PHB p.252",
                    SpellLevel = 2, School = "Transmutation",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 5,
                    EffectType = SpellEffectType.Healing,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Object repair not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.MENDING,
                    Name = "Mending",
                    Description = "Makes minor repairs on an object (1d4 damage repaired).",
                    SpellLevel = 0, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 2,
                    EffectType = SpellEffectType.Healing,
                    HealDice = 4, HealCount = 1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Object repair not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.MESSAGE,
                    Name = "Message",
                    Description = "Whispered conversation at distance. Range: 100 ft + 10 ft/level.",
                    SpellLevel = 0, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 22,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = 10,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Communication not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.MINOR_IMAGE,
                    Name = "Minor Image",
                    Description = "As silent image, plus some sound. Concentration + 2 rounds. Will disbelief. PHB p.254",
                    SpellLevel = 2, School = "Illusion",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 8,
                    EffectType = SpellEffectType.Buff,
                    DurationType = DurationType.Concentration,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Illusion mechanics not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.MIRROR_IMAGE,
                    Name = "Mirror Image",
                    Description = "1d4+1 illusory clones appear in adjacent cells. Clones are targetable decoys and can be swapped with at end of your turn. Duration 1 min/level.",
                    SpellLevel = 2,
                    School = "Illusion (Figment)",
                    ClassList = new[] { "Wizard", "Sorcerer", "Bard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffType = SpellNames.MIRROR_IMAGE,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.MOUNT,
                    Name = "Mount",
                    Description = "Summons a riding horse for 2 hr/level. PHB p.256",
                    SpellLevel = 1, School = "Conjuration",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 5,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.FullRound,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Mount/summoning not implemented]"
                });

        // ──────────────────────────────────────────────────────────────
        // MASS ENLARGE PERSON  (PHB p.226)
        // Transmutation
        // Level: Sor/Wiz 4
        // Components: V, S, M
        // Casting Time: 1 round
        // Range: Close (25 ft. + 5 ft./2 levels)
        // Targets: One humanoid creature/level, no two of which can be
        //          more than 30 ft. apart
        // Duration: 1 min./level (D)
        // Saving Throw: Fortitude negates
        // Spell Resistance: Yes
        //
        // This spell functions like Enlarge Person, except that it
        // affects multiple creatures.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.MASS_ENLARGE_PERSON,
                    Name = "Mass Enlarge Person",
                    Description = "As Enlarge Person, but affects one humanoid creature/level (no two more than 30 ft. apart). "
                        + "+2 STR, -2 DEX, -1 size penalty to AC/attack. "
                        + "Duration 1 min/level. Fort negates. SR: Yes. PHB p.226",
                    SpellLevel = 4,
                    School = "Transmutation",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Sorcerer", 4),
                        new SpellAvailability("Wizard", 4)
                    },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Buff,
                    BuffStatName = "STR",
                    BuffStatBonus = 2,
                    BuffDurationRounds = 10, // Legacy fallback: 1 minute
                    BuffType = "enlarge",
                    BuffBonusType = BonusType.Size,
                    BonusTypeExplicitlySet = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Fortitude",
                    SpellResistanceApplies = true,
                    ActionType = SpellActionType.FullRound,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        // ──────────────────────────────────────────────────────────────
        // MASS REDUCE PERSON  (PHB p.269)
        // Transmutation
        // Level: Sor/Wiz 4
        // Components: V, S, M
        // Casting Time: 1 round
        // Range: Close (25 ft. + 5 ft./2 levels)
        // Targets: One humanoid creature/level, no two of which can be
        //          more than 30 ft. apart
        // Duration: 1 min./level (D)
        // Saving Throw: Fortitude negates
        // Spell Resistance: Yes
        //
        // This spell functions like Reduce Person, except that it
        // affects multiple creatures.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.MASS_REDUCE_PERSON,
                    Name = "Mass Reduce Person",
                    Description = "As Reduce Person, but affects one humanoid creature/level (no two more than 30 ft. apart). "
                        + "-2 STR, +2 DEX, +1 size bonus to AC/attack. "
                        + "Duration 1 min/level. Fort negates. SR: Yes. PHB p.269",
                    SpellLevel = 4,
                    School = "Transmutation",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Sorcerer", 4),
                        new SpellAvailability("Wizard", 4)
                    },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Buff,
                    BuffStatName = "DEX",
                    BuffStatBonus = 2,
                    BuffDurationRounds = 10, // Legacy fallback: 1 minute
                    BuffType = "reduce",
                    BuffBonusType = BonusType.Size,
                    BonusTypeExplicitlySet = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Fortitude",
                    SpellResistanceApplies = true,
                    ActionType = SpellActionType.FullRound,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        // Aliases
        RegisterClassSpellAlias("magic_weapon_clr", SpellNames.MAGIC_WEAPON, "Cleric", 1);
        RegisterClassSpellAlias("mending_clr", SpellNames.MENDING, "Cleric", 0);

    }
}
