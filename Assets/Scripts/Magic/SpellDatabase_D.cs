// ============================================================================
// SpellDatabase_D.cs — Spells starting with D
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

public static partial class SpellDatabase
{
    private static void RegisterSpellsD()
    {
        Register(new SpellData
                {
                    SpellId = SpellNames.DANCING_LIGHTS,
                    Name = "Dancing Lights",
                    Description = "Creates up to four lights that move as you direct. Lasts 1 minute.",
                    SpellLevel = 0, School = "Evocation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 20,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = 10,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Light/illumination not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.DARKNESS,
                    Name = "Darkness",
                    Description = "Evocation [Darkness]. Creates magical darkness in a 20-ft radius spread. Darkness does not block line of sight, but attacks involving darkness squares have concealment (20% miss chance), even against darkvision. Duration 10 min/level (D). PHB p.216",
                    SpellLevel = 2,
                    School = "Evocation [Darkness]",
                    ClassList = new[] { "Wizard", "Sorcerer", "Bard", "Cleric" },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Touch,
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 4,
                    AoERangeSquares = 1,
                    AreaRadius = 4,
                    AoEFilter = AoETargetFilter.All,
                    EffectType = SpellEffectType.Buff,
                    DurationType = DurationType.Minutes,
                    DurationValue = 10,
                    DurationScalesWithLevel = true,
                    IsDismissible = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    AllowsSavingThrow = false,
                    SpellResistanceApplies = false,
                    BuffBonusType = BonusType.Concealment,
                    BonusTypeExplicitlySet = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.DARKVISION,
                    Name = "Darkvision",
                    Description = "See 60 ft in total darkness. Duration 1 hr/level. PHB p.216",
                    SpellLevel = 2, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Vision/darkness not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.DAZE,
                    Name = "Daze",
                    Description = "Enchantment (Compulsion) [Mind-Affecting]. One humanoid creature of 4 HD or less is dazed for 1 round. Will negates. SR applies.",
                    SpellLevel = 0, School = "Enchantment",
                    ClassList = new[] { "Wizard", "Sorcerer", "Bard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    // Close range (25 ft + 5 ft/2 levels)
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    IsMindAffecting = true,
                    BlockedByProtectionFromAlignment = false,
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    DurationScalesWithLevel = false,
                    BuffDurationRounds = 1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.DAZE_MONSTER,
                    Name = "Daze Monster",
                    Description = "Enchantment (Compulsion) [Mind-Affecting]. One living creature of 6 HD or less is dazed for 1 round. Will negates. SR applies. Creatures with 7+ HD are immune. PHB p.217",
                    SpellLevel = 2, School = "Enchantment",
                    ClassList = new[] { "Wizard", "Sorcerer", "Bard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Medium,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    IsMindAffecting = true,
                    BlockedByProtectionFromAlignment = false,
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    DurationScalesWithLevel = false,
                    BuffDurationRounds = 1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.DEATH_KNELL,
                    Name = "Death Knell",
                    Description = "Kills dying creature, caster gains 1d8 temp HP, +2 STR, +1 CL. Touch range. Will negates. PHB p.217",
                    SpellLevel = 2, School = "Necromancy",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 0, DamageCount = 0, BonusDamage = 10, // kills dying creature
                    DamageType = "negative",
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SaveHalves = false,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.DEATHWATCH,
                    Name = "Deathwatch",
                    Description = "Reveals how near death subjects within 30 ft are. Duration 10 min/level. PHB p.217",
                    SpellLevel = 1, School = "Necromancy",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - HP reveal not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.DELAY_POISON,
                    Name = "Delay Poison",
                    Description = "Stops poison from harming subject for 1 hr/level. PHB p.217",
                    SpellLevel = 2, School = "Conjuration",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Poison mechanics not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.DOMAIN_DESECRATE,
                    Name = "Desecrate",
                    Description = "Fills area with negative energy, making undead stronger. Undead in the area gain +1 profane bonus on attack rolls, damage rolls, and saving throws.",
                    SpellLevel = 2,
                    School = "Evocation",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.Area,
                    RangeSquares = 4,
                    AreaRadius = 4,
                    EffectType = SpellEffectType.Debuff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Area desecration not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.DETECT_EVIL,
                    Name = "Detect Evil",
                    Description = "Reveals evil creatures, spells, or objects. Concentration, up to 10 min/level. PHB p.218",
                    SpellLevel = 1, School = "Divination",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    DurationType = DurationType.Concentration,
                    DurationValue = 10,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Alignment detection not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.DETECT_MAGIC_WIZ,
                    Name = "Detect Magic",
                    Description = "Detects spells and magic items within 60 ft cone. Concentration, up to 1 min/level.",
                    SpellLevel = 0, School = "Divination",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    DurationType = DurationType.Concentration,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Detection mechanics not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.DETECT_POISON_WIZ,
                    Name = "Detect Poison",
                    Description = "Detects poison in one creature or small object.",
                    SpellLevel = 0, School = "Divination",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeSquares = 5,
                    EffectType = SpellEffectType.Buff, // detection
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Poison detection not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.DOMAIN_DETECT_SECRET_DOORS,
                    Name = "Detect Secret Doors",
                    Description = "Reveals secret doors within 60 ft cone.",
                    SpellLevel = 1,
                    School = "Divination",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = 30,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Secret door detection not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.DETECT_THOUGHTS,
                    Name = "Detect Thoughts",
                    Description = "Allows listening to surface thoughts. Concentration, up to 1 min/level. Will negates. PHB p.220",
                    SpellLevel = 2, School = "Divination",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 12,
                    EffectType = SpellEffectType.Buff,
                    DurationType = DurationType.Concentration,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Mind reading not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.DETECT_UNDEAD,
                    Name = "Detect Undead",
                    Description = "Reveals undead within 60 ft. Concentration, up to 1 min/level. PHB p.220",
                    SpellLevel = 1, School = "Divination",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    DurationType = DurationType.Concentration,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Undead detection not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.DISGUISE_SELF,
                    Name = "Disguise Self",
                    Description = "Illusion (Glamer). Caster appears as a humanoid of the same size category. Grants +10 competence bonus on Disguise checks. Duration 10 min/level (D). PHB p.222",
                    SpellLevel = 1,
                    School = "Illusion",
                    ClassList = new[] { "Wizard", "Sorcerer", "Bard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    DurationType = DurationType.Minutes,
                    DurationValue = 10,
                    DurationScalesWithLevel = true,
                    IsDismissible = true,
                    AllowsSavingThrow = false,
                    SpellResistanceApplies = false,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.DISRUPT_UNDEAD,
                    Name = "Disrupt Undead",
                    Description = "You fire a ray of positive energy at one undead creature. Make a ranged touch attack; on a hit it deals 1d6 positive damage. This spell has no effect on living creatures.",
                    SpellLevel = 0, School = "Necromancy",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Close,
                    IsTouch = true,
                    IsRangedTouch = true,
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 6, DamageCount = 1,
                    DamageType = "positive",
                    SpellResistanceApplies = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.DIVINE_FAVOR,
                    Name = "Divine Favor",
                    Description = "+1 luck bonus on attack and damage rolls (per 3 CL, max +3). Duration 1 minute. PHB p.224",
                    SpellLevel = 1, School = "Evocation",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffAttackBonus = 1,
                    BuffDamageBonus = 1,
                    BuffDurationRounds = 10,
                    BuffType = "luck",
                    BuffBonusType = BonusType.Luck,
                    BonusTypeExplicitlySet = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = false, // Fixed 1 minute
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.DOOM,
                    Name = "Doom",
                    Description = "One subject is shaken (–2 on attack, saves, skills, ability checks). Will save negates. 1 min/level. PHB p.225",
                    SpellLevel = 1, School = "Necromancy",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Medium,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    BuffAttackBonus = -2,
                    BuffSaveBonus = -2,
                    BuffDurationRounds = 30,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // ================================================================
        //  DISPEL MAGIC — Abjuration, 3rd level (PHB p.223)
        // ================================================================
        Register(new SpellData
                {
                    SpellId = SpellNames.DISPEL_MAGIC,
                    Name = "Dispel Magic",
                    Description = "Abjuration. You can use Dispel Magic to end ongoing spells on a creature or object, " +
                        "or to suppress a magic item's properties. A dispelled spell ends as if its duration had run out. " +
                        "Targeted Dispel: one dispel check (1d20 + caster level, max +10) vs DC 11 + spell's caster level, " +
                        "removes at most one spell (checked highest CL first). " +
                        "Area Dispel: 20-ft radius burst, targeted dispel on each creature/object (magic items unaffected). " +
                        "Auto-succeeds against your own spells. PHB p.223",
                    SpellLevel = 3,
                    School = "Abjuration",
                    ClassList = new[] { "Wizard", "Sorcerer", "Bard", "Cleric", "Paladin", "Druid" },
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Bard", 3),
                        new SpellAvailability("Cleric", 3),
                        new SpellAvailability("Druid", 4),
                        new SpellAvailability("Paladin", 3),
                        new SpellAvailability("Sorcerer", 3),
                        new SpellAvailability("Wizard", 3)
                    },
                    // Targeted mode: can target any creature (enemy or ally)
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Medium,
                    EffectType = SpellEffectType.Debuff,
                    DurationType = DurationType.Instantaneous,
                    DurationValue = 0,
                    DurationScalesWithLevel = false,
                    AllowsSavingThrow = false,
                    SpellResistanceApplies = false,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // Aliases
        RegisterClassSpellAlias("detect_magic_clr", SpellNames.DETECT_MAGIC_WIZ, "Cleric", 0);
        RegisterClassSpellAlias("detect_poison_clr", SpellNames.DETECT_POISON_WIZ, "Cleric", 0);
        // Druid alias for Dispel Magic at level 4
        RegisterClassSpellAlias("dispel_magic_drd", SpellNames.DISPEL_MAGIC, "Druid", 4);

    }
}
