// ============================================================================
// SpellDatabase_WizardLevel2.cs — Wizard 2nd Level spell registrations (38 spells)
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterWizard2ndLevel()
    {
        // --- FUNCTIONAL: Damage Spells ---

        Register(new SpellData
        {
            SpellId = "scorching_ray",
            Name = "Scorching Ray",
            Description = "Ranged touch attack, 4d6 fire damage per ray. 1 ray at CL3 (2 at CL7, 3 at CL11). PHB p.274",
            SpellLevel = 2, School = "Evocation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5,
            EffectType = SpellEffectType.Damage,
            DamageDice = 6, DamageCount = 4, // 4d6 per ray
            DamageType = "fire",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "acid_arrow",
            Name = "Acid Arrow",
            Description = "Ranged touch attack, 2d4 acid damage + 2d4/round for 1 round per 3 CL. PHB p.196",
            SpellLevel = 2, School = "Conjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 8, // Long range
            EffectType = SpellEffectType.Damage,
            DamageDice = 4, DamageCount = 2, // Initial 2d4
            DamageType = "acid",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "flaming_sphere",
            Name = "Flaming Sphere",
            Description = "Rolling ball of fire, 2d6 fire damage. Reflex negates. Lasts 1 round/level, movable. PHB p.232",
            SpellLevel = 2, School = "Evocation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 22,
            EffectType = SpellEffectType.Damage,
            DamageDice = 6, DamageCount = 2, // 2d6
            DamageType = "fire",
            AllowsSavingThrow = true,
            SavingThrowType = "Reflex",
            SaveHalves = false, // Negates
            BuffDurationRounds = 3,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "shatter",
            Name = "Shatter",
            Description = "Sonic vibration damages objects or crystalline creatures. 1d6/level (max 10d6) sonic. PHB p.278",
            SpellLevel = 2, School = "Evocation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5,
            EffectType = SpellEffectType.Damage,
            DamageDice = 6, DamageCount = 3, // 3d6 at CL3
            DamageType = "sonic",
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            SaveHalves = false,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "gust_of_wind",
            Name = "Gust of Wind",
            Description = "Blasts of wind knock down/push creatures. Fortitude save or be blown away (size-dependent). PHB p.238",
            SpellLevel = 2, School = "Evocation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 12, // 60 ft
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Fortitude",
            BuffDurationRounds = 1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        // --- FUNCTIONAL: Buff Spells ---

        Register(new SpellData
        {
            SpellId = "bulls_strength",
            Name = "Bull's Strength",
            Description = "Subject gains +4 enhancement bonus to STR for 1 min/level. PHB p.207",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1, // Touch
            EffectType = SpellEffectType.Buff,
            BuffStatName = "STR",
            BuffStatBonus = 4,
            BuffDurationRounds = 30,
            BuffType = "enhancement",
            BuffBonusType = BonusType.Enhancement,
            BonusTypeExplicitlySet = true,
            DurationType = DurationType.Minutes,
            DurationValue = 1,
            DurationScalesWithLevel = true,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "cats_grace",
            Name = "Cat's Grace",
            Description = "Subject gains +4 enhancement bonus to DEX for 1 min/level. PHB p.208",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffStatName = "DEX",
            BuffStatBonus = 4,
            BuffDurationRounds = 30,
            BuffType = "enhancement",
            BuffBonusType = BonusType.Enhancement,
            BonusTypeExplicitlySet = true,
            DurationType = DurationType.Minutes,
            DurationValue = 1,
            DurationScalesWithLevel = true,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "bears_endurance",
            Name = "Bear's Endurance",
            Description = "Subject gains +4 enhancement bonus to CON for 1 min/level. PHB p.203",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffStatName = "CON",
            BuffStatBonus = 4,
            BuffDurationRounds = 30,
            BuffType = "enhancement",
            BuffBonusType = BonusType.Enhancement,
            BonusTypeExplicitlySet = true,
            DurationType = DurationType.Minutes,
            DurationValue = 1,
            DurationScalesWithLevel = true,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "foxs_cunning",
            Name = "Fox's Cunning",
            Description = "Subject gains +4 enhancement bonus to INT for 1 min/level. PHB p.233",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffStatName = "INT",
            BuffStatBonus = 4,
            BuffDurationRounds = 30,
            BuffType = "enhancement",
            BuffBonusType = BonusType.Enhancement,
            BonusTypeExplicitlySet = true,
            DurationType = DurationType.Minutes,
            DurationValue = 1,
            DurationScalesWithLevel = true,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "owls_wisdom",
            Name = "Owl's Wisdom",
            Description = "Subject gains +4 enhancement bonus to WIS for 1 min/level. PHB p.259",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffStatName = "WIS",
            BuffStatBonus = 4,
            BuffDurationRounds = 30,
            BuffType = "enhancement",
            BuffBonusType = BonusType.Enhancement,
            BonusTypeExplicitlySet = true,
            DurationType = DurationType.Minutes,
            DurationValue = 1,
            DurationScalesWithLevel = true,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "eagles_splendor",
            Name = "Eagle's Splendor",
            Description = "Subject gains +4 enhancement bonus to CHA for 1 min/level. PHB p.225",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffStatName = "CHA",
            BuffStatBonus = 4,
            BuffDurationRounds = 30,
            BuffType = "enhancement",
            BuffBonusType = BonusType.Enhancement,
            BonusTypeExplicitlySet = true,
            DurationType = DurationType.Minutes,
            DurationValue = 1,
            DurationScalesWithLevel = true,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "invisibility",
            Name = "Invisibility",
            Description = "Subject is invisible for 1 min/level or until it attacks. +2 attack on first strike. PHB p.245",
            SpellLevel = 2, School = "Illusion",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffAttackBonus = 2, // Simplified: +2 from invisibility on first attack
            BuffDurationRounds = 30,
            BuffType = "invisibility",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "mirror_image",
            Name = "Mirror Image",
            Description = "Creates 1d4+1 decoy duplicates of you (CL3). Attacks may hit images instead. 1 min/level. PHB p.254",
            SpellLevel = 2, School = "Illusion",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffACBonus = 4, // Simplified: effective +4 AC from images
            BuffDurationRounds = 30,
            BuffType = "mirror_image",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "protection_from_arrows",
            Name = "Protection from Arrows",
            Description = "Subject gains DR 10/magic vs ranged weapons. Absorbs 10/CL damage (30 at CL3). 1 hr/level. PHB p.266",
            SpellLevel = 2, School = "Abjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            BuffType = "DR_arrows",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Damage reduction not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "resist_energy",
            Name = "Resist Energy",
            Description = "Grants resistance 10 to specified energy type (fire, cold, acid, electricity, sonic). 10 min/level. PHB p.272",
            SpellLevel = 2, School = "Abjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            BuffType = "energy_resistance",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Energy resistance not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "false_life",
            Name = "False Life",
            Description = "Gain 1d10+CL temporary hit points (max +10). Duration 1 hour/level. PHB p.229",
            SpellLevel = 2, School = "Necromancy",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffTempHP = 8, // ~average of 1d10+3 at CL3
            BuffDurationRounds = -1,
            BuffType = "temp_hp",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        // --- FUNCTIONAL: Debuff Spells ---

        Register(new SpellData
        {
            SpellId = "web",
            Name = "Web",
            Description = "Fills 20-ft-radius spread with sticky webs. Reflex save or stuck. STR/Escape check to escape. 10 min/level. PHB p.301",
            SpellLevel = 2, School = "Conjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy, // Simplified from area
            RangeSquares = 22,
            AreaRadius = 4,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Reflex",
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "glitterdust",
            Name = "Glitterdust",
            Description = "Blinds creatures in area and outlines invisible creatures. Will save negates blindness. 1 round/level. PHB p.236",
            SpellLevel = 2, School = "Conjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy, // Simplified
            RangeSquares = 22,
            AreaRadius = 2,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 3,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "touch_of_idiocy",
            Name = "Touch of Idiocy",
            Description = "Touch attack reduces target's INT, WIS, and CHA by 1d6 each. No save. 10 min/level. PHB p.294",
            SpellLevel = 2, School = "Enchantment",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 1, // Touch
            EffectType = SpellEffectType.Debuff,
            DamageDice = 6, DamageCount = 1, // 1d6 to each mental stat
            DamageType = "mental_drain",
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "blindness_deafness_wiz",
            Name = "Blindness/Deafness",
            Description = "Makes subject blind or deaf. Fortitude negates. Permanent. PHB p.206",
            SpellLevel = 2, School = "Necromancy",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 22,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Fortitude",
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "daze_monster",
            Name = "Daze Monster",
            Description = "Living creature of 6 HD or less loses next action. Will save negates. PHB p.217",
            SpellLevel = 2, School = "Enchantment",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 22,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "hideous_laughter",
            Name = "Hideous Laughter",
            Description = "Subject laughs uncontrollably, falls prone. Will save negates. 1 round/level. PHB p.240",
            SpellLevel = 2, School = "Enchantment",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 3,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "scare",
            Name = "Scare",
            Description = "Frightens creatures of less than 6 HD. Will save negates. 1 round/level. PHB p.274",
            SpellLevel = 2, School = "Necromancy",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 22,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 3,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "spectral_hand",
            Name = "Spectral Hand",
            Description = "Creates ghostly hand to deliver touch spells at range. +2 on touch attacks via hand. 1 min/level. PHB p.282",
            SpellLevel = 2, School = "Necromancy",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffAttackBonus = 2,
            BuffDurationRounds = 30,
            BuffType = "spectral_hand",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        // --- PLACEHOLDER: Utility/Complex ---

        Register(new SpellData
        {
            SpellId = "knock",
            Name = "Knock",
            Description = "Opens locked or magically sealed door. PHB p.246",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 22,
            EffectType = SpellEffectType.Buff,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Lock/door mechanics not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "levitate",
            Name = "Levitate",
            Description = "Subject moves up or down at your direction. 1 min/level. Will negates (object). PHB p.248",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 30,
            BuffType = "levitate",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Vertical movement not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "see_invisibility",
            Name = "See Invisibility",
            Description = "You can see invisible creatures and objects. Duration 10 min/level. PHB p.275",
            SpellLevel = 2, School = "Divination",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            BuffType = "see_invis",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Invisibility detection not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "detect_thoughts",
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
            SpellId = "locate_object",
            Name = "Locate Object",
            Description = "Senses direction toward object. Duration 1 min/level. PHB p.249",
            SpellLevel = 2, School = "Divination",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 30,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Object location not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "summon_monster_2",
            Name = "Summon Monster II",
            Description = "Calls creature to fight for you. Duration 1 round/level. PHB p.286",
            SpellLevel = 2, School = "Conjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 3,
            ActionType = SpellActionType.FullRound,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Summoning not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "minor_image",
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
            SpellId = "blur",
            Name = "Blur",
            Description = "Attacks against subject have 20% miss chance. Duration 1 min/level. PHB p.206",
            SpellLevel = 2, School = "Illusion",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffACBonus = 2, // Simplified: 20% miss ~= +2 AC effective
            BuffDurationRounds = 30,
            BuffType = "concealment",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "alter_self",
            Name = "Alter Self",
            Description = "Assume form of a similar creature. +10 Disguise, may gain abilities. 10 min/level. PHB p.197",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Transformation not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "darkvision",
            Name = "Darkvision",
            Description = "See 60 ft in total darkness. Duration 1 hr/level. PHB p.216",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Vision/darkness not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "rope_trick",
            Name = "Rope Trick",
            Description = "As many as 8 creatures hide in extradimensional space. Duration 1 hr/level. PHB p.273",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Extradimensional space not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "spider_climb",
            Name = "Spider Climb",
            Description = "Grants ability to walk on walls and ceilings. Speed 20 ft. Duration 10 min/level. PHB p.283",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            BuffType = "spider_climb",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Wall climbing not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "whispering_wind",
            Name = "Whispering Wind",
            Description = "Sends a short message or sound to a distant location. PHB p.301",
            SpellLevel = 2, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = -1,
            EffectType = SpellEffectType.Buff,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Long-range communication not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "arcane_lock",
            Name = "Arcane Lock",
            Description = "Magically locks a portal or chest. Permanent. PHB p.200",
            SpellLevel = 2, School = "Abjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Lock mechanics not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "obscure_object",
            Name = "Obscure Object",
            Description = "Masks object against scrying. Duration 8 hours. PHB p.258",
            SpellLevel = 2, School = "Abjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Anti-scrying not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "continual_flame",
            Name = "Continual Flame",
            Description = "Makes a permanent, heatless flame. Requires 50 gp ruby dust. PHB p.213",
            SpellLevel = 2, School = "Evocation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 1,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Light/illumination not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "command_undead",
            Name = "Command Undead",
            Description = "Undead creature obeys your commands. Will save for intelligent undead. 1 day/level. PHB p.211",
            SpellLevel = 2, School = "Necromancy",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Undead control not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "ghoul_touch",
            Name = "Ghoul Touch",
            Description = "Touch attack paralyzes one living subject for 1d6+2 rounds. Fort negates. Sickens nearby. PHB p.235",
            SpellLevel = 2, School = "Necromancy",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 1,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Fortitude",
            BuffDurationRounds = 5, // 1d6+2 avg
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "hypnotic_pattern",
            Name = "Hypnotic Pattern",
            Description = "Fascinates 2d4+CL HD of creatures. Will negates. Concentration + 2 rounds. PHB p.242",
            SpellLevel = 2, School = "Illusion",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 22,
            EffectType = SpellEffectType.Debuff,
            DurationType = DurationType.Concentration,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Fascination mechanics not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "pyrotechnics",
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

        Register(new SpellData
        {
            SpellId = "fog_cloud",
            Name = "Fog Cloud",
            Description = "Fog obscures vision, 20-ft radius. Duration 10 min/level. PHB p.232",
            SpellLevel = 2, School = "Conjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 22,
            AreaRadius = 4,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Fog/concealment area not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "summon_swarm",
            Name = "Summon Swarm",
            Description = "Summons swarm of bats, rats, or spiders. 2d6 damage/round. Concentration + 2 rounds. PHB p.289",
            SpellLevel = 2, School = "Conjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5,
            EffectType = SpellEffectType.Damage,
            DurationType = DurationType.Concentration,
            DamageDice = 6, DamageCount = 2,
            DamageType = "swarm",
            ActionType = SpellActionType.FullRound,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Swarm summoning not implemented]"
        });
    }

    // ====================================================================
    //  CLERIC CANTRIPS (Level 0 - Orisons)
    // ====================================================================
}
