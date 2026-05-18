// ============================================================================
// SpellDatabase_R.cs — Spells starting with R
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

public static partial class SpellDatabase
{
    private static void RegisterSpellsR()
    {
        Register(new SpellData
                {
                    SpellId = SpellNames.RAY_OF_ENFEEBLEMENT,
                    Name = "Ray of Enfeeblement",
                    Description = "Ranged touch attack. On hit: target takes 1d6 + (1 per 2 caster levels, max +5) Strength penalty. No save. Duration 1 min/level. SR applies. PHB p.269",
                    SpellLevel = 1,
                    School = "Necromancy",
                    ClassList = new[] { "Wizard", "Sorcerer" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Close,
                    IsTouch = true,
                    IsRangedTouch = true,
                    EffectType = SpellEffectType.Debuff,
                    DamageType = "strength_penalty",
                    SpellResistanceApplies = true,
                    AllowsSavingThrow = false,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // ──────────────────────────────────────────────────────────────
        // RAY OF EXHAUSTION  (PHB p.269)
        // Necromancy
        // Level: Sor/Wiz 3
        // Components: V, S, M (a few drops of sweat)
        // Casting Time: 1 standard action
        // Range: Close (25 ft. + 5 ft./2 levels)
        // Effect: Ray
        // Duration: 1 min./level
        // Saving Throw: Fortitude partial; see text
        // Spell Resistance: Yes
        //
        // A black ray projects from your pointing finger. You must
        // succeed on a ranged touch attack with the ray to strike a
        // target. The subject is immediately exhausted for the spell's
        // duration. A successful Fortitude save means the creature is
        // only fatigued.
        //
        // Exhausted: -6 STR/DEX, half movement speed, cannot run/charge.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.RAY_OF_EXHAUSTION,
                    Name = "Ray of Exhaustion",
                    Description = "Necromancy. Ranged touch attack. On hit, target is Exhausted (-6 STR, -6 DEX, "
                        + "half movement speed, cannot run/charge) for 1 min/level. A successful Fortitude save "
                        + "reduces the effect to Fatigued instead. SR: Yes. PHB p.269",
                    SpellLevel = 3,
                    School = "Necromancy",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Sorcerer", 3),
                        new SpellAvailability("Wizard", 3)
                    },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Close,
                    IsTouch = true,
                    IsRangedTouch = true,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,        // Fortitude reduces to fatigued
                    SavingThrowType = "Fortitude",
                    SpellResistanceApplies = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.RAY_OF_FROST,
                    Name = "Ray of Frost",
                    Description = "A ray of freezing air and ice deals 1d3 cold damage. Ranged touch attack.",
                    SpellLevel = 0, School = "Evocation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Close,
                    IsTouch = true,
                    IsRangedTouch = true,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 3, DamageCount = 1,
                    DamageType = "cold",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.READ_MAGIC,
                    Name = "Read Magic",
                    Description = "Read scrolls and spellbooks. Duration 10 min/level.",
                    SpellLevel = 0, School = "Divination",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Scroll/spellbook reading not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.REDUCE_PERSON,
                    Name = "Reduce Person",
                    HasMaterialComponent = true, // M: powdered iron (common — covered by spell component pouch)
                    Description = "Humanoid creature halves in size. -2 STR, +2 DEX, +1 AC/attack (size). 1 min/level. Components: V, S, M (powdered iron). PHB p.269",
                    SpellLevel = 1, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
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
                    ActionType = SpellActionType.FullRound,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.REMOVE_FEAR,
                    Name = "Remove Fear",
                    Description = "Suppresses fear or gives +4 morale bonus vs fear for 10 min. One ally +1 per 4 CL. PHB p.271",
                    SpellLevel = 1, School = "Abjuration",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Buff,
                    BuffSaveBonus = 4, // +4 morale vs fear, simplified
                    BuffDurationRounds = -1,
                    BuffType = "morale",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.REMOVE_PARALYSIS,
                    Name = "Remove Paralysis",
                    Description = "Frees 1-4 creatures from paralysis or slow effect. PHB p.271",
                    SpellLevel = 2, School = "Conjuration",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeSquares = 5,
                    EffectType = SpellEffectType.Healing,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Status effect removal not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.RESTORATION,
                    Name = "Restoration",
                    Description = "Dispels temporary negative levels and restores drained abilities. PHB p.272",
                    SpellLevel = 4, School = "Conjuration",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Healing,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.GREATER_RESTORATION,
                    Name = "Greater Restoration",
                    Description = "Restores all temporary negative levels and ability damage. PHB p.246",
                    SpellLevel = 7, School = "Conjuration",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Healing,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.RESIST_ENERGY,
                    Name = "Resist Energy",
                    Description = "Choose one energy type (acid, cold, electricity, fire, or sonic). Grants resistance 10/20/30 based on caster level. Duration 10 min/level. PHB p.272",
                    SpellLevel = 2, School = "Abjuration",
                    ClassList = new[] { "Wizard", "Sorcerer" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    BuffType = "energy_resistance",
                    DurationType = DurationType.Minutes,
                    DurationValue = 10,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.RESISTANCE_WIZ,
                    Name = "Resistance",
                    Description = "Subject gains +1 on saving throws for 1 minute.",
                    SpellLevel = 0, School = "Abjuration",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffSaveBonus = 1,
                    BuffDurationRounds = 10, // 1 minute
                    BuffType = "resistance",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.ROPE_TRICK,
                    Name = "Rope Trick",
                    Description = "As many as 8 creatures hide in extradimensional space. Duration 1 hr/level. PHB p.273",
                    SpellLevel = 2, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Extradimensional space not implemented]"
                });

        // ──────────────────────────────────────────────────────────────
        // RAGE  (PHB p.268)
        // Enchantment (Compulsion) [Mind-Affecting]
        // Level: Bard 2, Sor/Wiz 3
        // Components: V, S
        // Casting Time: 1 standard action
        // Range: Medium (100 ft. + 10 ft./level)
        // Targets: One willing living creature per three levels, no two
        //   of which may be more than 30 ft. apart
        // Duration: Concentration + 1 round/level (max 10 rounds)
        // Saving Throw: None
        // Spell Resistance: Yes (harmless)
        //
        // Each affected creature gains a +2 morale bonus to Strength and
        // Constitution, and a +1 morale bonus on Will saves. (In 3.5e PHB
        // the bonus is +2 morale Str/Con and +1 morale Will; some sources
        // list +4/+4/+2 for the Barbarian class feature version. We use
        // the PHB spell version here.)
        //
        // An affected creature also takes a -2 penalty to AC. The spell
        // does not stack with the barbarian's rage class feature.
        Register(new SpellData
                {
                    SpellId = SpellNames.RAINBOW_PATTERN,
                    Name = "Rainbow Pattern",
                    Description = "Illusion (Pattern) [Mind-Affecting]. A glowing rainbow fascinates creatures within 20-ft radius spread (up to 24 HD total). Fascinated creatures stand still. New Will save each round to break free. Duration: Concentration +1 round/level (D). PHB p.268",
                    SpellLevel = 4, School = "Illusion",
                    ClassList = new[] { "Wizard", "Sorcerer" },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Medium,
                    AreaRadius = 4,
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 4,
                    AoERangeSquares = 20,
                    AoEFilter = AoETargetFilter.EnemiesOnly,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    IsMindAffecting = true,
                    DurationType = DurationType.Concentration,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    HasMaterialComponent = true
                });

        RegisterClassSpellAlias("rainbow_pattern_brd", SpellNames.RAINBOW_PATTERN, "Bard", 4);

        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.RAGE,
                    Name = "Rage",
                    Description = "Enchantment (Compulsion) [Mind-Affecting]. Willing target gains +2 morale bonus to Str and Con, "
                        + "+1 morale bonus on Will saves, and takes -2 penalty to AC. "
                        + "Cannot use Concentration, Int-, Dex-, or Cha-based skills (except Balance, Escape Artist, Intimidate, Ride). "
                        + "Duration: Concentration + 1 round/level (max 10 rounds after concentration ends). "
                        + "No save (willing). SR: Yes (harmless). PHB p.268",
                    SpellLevel = 3,
                    School = "Enchantment",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Bard", 2),
                        new SpellAvailability("Sorcerer", 3),
                        new SpellAvailability("Wizard", 3)
                    },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Medium,
                    EffectType = SpellEffectType.Buff,
                    BuffStatName = "STR",
                    BuffStatBonus = 2,
                    BuffSaveBonus = 1,
                    BuffACBonus = -2,
                    BuffType = "morale",
                    BuffBonusType = BonusType.Morale,
                    BonusTypeExplicitlySet = true,
                    AllowsSavingThrow = false, // Willing target
                    SpellResistanceApplies = true,
                    IsMindAffecting = true,
                    DurationType = DurationType.Concentration,
                    DurationValue = 1, // +1 round/level after concentration
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        // ──────────────────────────────────────────────────────────────
        // REMOVE CURSE  (PHB p.270)
        // Abjuration
        // Level: Cleric 3, Paladin 3, Sor/Wiz 4
        // Components: V, S (no material component)
        // Casting Time: 1 standard action
        // Range: Touch
        // Target: Creature or object touched
        // Duration: Instantaneous
        // Saving Throw: Will negates (harmless)
        // Spell Resistance: Yes (harmless)
        //
        // Remove curse instantaneously removes all curses on an object
        // or a creature. Remove curse does not remove the curse from a
        // cursed shield, weapon, or suit of armor, although the spell
        // typically enables the creature afflicted with any such cursed
        // item to remove and get rid of it. Remove curse counters and
        // dispels bestow curse.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.REMOVE_CURSE,
                    Name = "Remove Curse",
                    Description = "Abjuration. Instantaneously removes all curses on an object or creature. "
                        + "Does not remove the curse from a cursed shield, weapon, or suit of armor, "
                        + "but enables the afflicted creature to remove and get rid of it. "
                        + "Counters and dispels Bestow Curse. "
                        + "Will negates (harmless). SR: Yes (harmless). PHB p.270",
                    SpellLevel = 3,
                    School = "Abjuration",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Cleric", 3),
                        new SpellAvailability("Paladin", 3),
                        new SpellAvailability("Sorcerer", 4),
                        new SpellAvailability("Wizard", 4)
                    },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    IsMeleeTouch = true,
                    EffectType = SpellEffectType.Healing, // Removal spell
                    DurationType = DurationType.Instantaneous,
                    DurationValue = 0,
                    DurationScalesWithLevel = false,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will", // harmless
                    SpellResistanceApplies = true, // harmless
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    HasMaterialComponent = false,
                    IsPlaceholder = false
                });

        RegisterClassSpellAlias("remove_curse_pal", SpellNames.REMOVE_CURSE, "Paladin", 3);
        RegisterClassSpellAlias("remove_curse_wiz", SpellNames.REMOVE_CURSE, "Wizard", 4);
        RegisterClassSpellAlias("remove_curse_sor", SpellNames.REMOVE_CURSE, "Sorcerer", 4);

        // Aliases
        RegisterClassSpellAlias("read_magic_clr", SpellNames.READ_MAGIC, "Cleric", 0);
        RegisterClassSpellAlias("resist_energy_clr", SpellNames.RESIST_ENERGY, "Cleric", 2);
        RegisterClassSpellAlias("resist_energy_dru", SpellNames.RESIST_ENERGY, "Druid", 2);
        RegisterClassSpellAlias("resist_energy_pal", SpellNames.RESIST_ENERGY, "Paladin", 2);
        RegisterClassSpellAlias("resist_energy_rgr", SpellNames.RESIST_ENERGY, "Ranger", 1);
        RegisterClassSpellAlias("resistance_clr", SpellNames.RESISTANCE_WIZ, "Cleric", 0);

    }
}
