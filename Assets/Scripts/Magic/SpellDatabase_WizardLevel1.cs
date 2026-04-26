// ============================================================================
// SpellDatabase_WizardLevel1.cs — Wizard 1st Level spell registrations (43 spells)
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterWizard1stLevel()
    {
        // --- FUNCTIONAL: Damage Spells ---

        Register(new SpellData
        {
            SpellId = "magic_missile",
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
            SpellId = "burning_hands",
            Name = "Burning Hands",
            Description = "1d4/level fire damage (max 5d4) in 15-ft cone. Reflex half. PHB p.207",
            SpellLevel = 1, School = "Evocation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Area, // Cone AoE from caster
            RangeSquares = 3, // 15-ft cone ~3 squares
            AreaRadius = 3,
            // AoE properties
            AoEShapeType = AoEShape.Cone,
            AoESizeSquares = 3, // 15 ft = 3 squares length
            AoERangeSquares = 0, // Cone originates from caster (no placement range)
            AoEFilter = AoETargetFilter.All, // Hits all creatures in cone
            EffectType = SpellEffectType.Damage,
            DamageDice = 4, DamageCount = 3, // 3d4 at CL3
            DamageType = "fire",
            AllowsSavingThrow = true,
            SavingThrowType = "Reflex",
            SaveHalves = true,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "shocking_grasp",
            Name = "Shocking Grasp",
            Description = "Touch delivers 1d6/level electricity damage (max 5d6). +3 attack vs metal armor. PHB p.279",
            SpellLevel = 1, School = "Evocation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeCategory = SpellRangeCategory.Touch,
            IsTouch = true,
            IsMeleeTouch = true,
            EffectType = SpellEffectType.Damage,
            DamageDice = 6, DamageCount = 3, // 3d6 at CL3
            DamageType = "electricity",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "chromatic_orb",
            Name = "Chromatic Orb",
            Description = "Ranged touch attack deals 1d8 damage (type varies by caster level). At CL3: fire, 1d8.",
            SpellLevel = 1, School = "Evocation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5,
            IsTouch = true,
            IsRangedTouch = true,
            EffectType = SpellEffectType.Damage,
            DamageDice = 8, DamageCount = 1,
            DamageType = "fire",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "chill_touch",
            Name = "Chill Touch",
            Description = "1 touch/level, each dealing 1d6 negative energy damage and 1 STR damage. Fort save negates STR damage.",
            SpellLevel = 1, School = "Necromancy",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeCategory = SpellRangeCategory.Touch,
            IsTouch = true,
            IsMeleeTouch = true,
            EffectType = SpellEffectType.Damage,
            DamageDice = 6, DamageCount = 1,
            DamageType = "negative",
            AllowsSavingThrow = true,
            SavingThrowType = "Fortitude",
            SaveHalves = false, // Save negates STR damage only
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        // --- FUNCTIONAL: Buff Spells ---

        Register(new SpellData
        {
            SpellId = "mage_armor",
            Name = "Mage Armor",
            Description = "+4 armor bonus to AC for 1 hour/level. Doesn't stack with actual armor. PHB p.249",
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
            SpellId = "shield",
            Name = "Shield",
            Description = "+4 shield bonus to AC, blocks Magic Missile. Duration 1 min/level. PHB p.278",
            SpellLevel = 1, School = "Abjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeCategory = SpellRangeCategory.Personal,
            EffectType = SpellEffectType.Buff,
            BuffShieldBonus = 4,
            BuffDurationRounds = 30,
            BuffType = "shield",
            BuffBonusType = BonusType.Shield,
            BonusTypeExplicitlySet = true,
            DurationType = DurationType.Minutes,
            DurationValue = 1,
            DurationScalesWithLevel = true,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "enlarge_person",
            Name = "Enlarge Person",
            Description = "Humanoid creature doubles in size. +2 STR, -2 DEX, -1 AC/attack (size). Duration 1 min/level. PHB p.226",
            SpellLevel = 1, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeCategory = SpellRangeCategory.Close,
            EffectType = SpellEffectType.Buff,
            BuffStatName = "STR",
            BuffStatBonus = 2, // +2 size bonus to STR
            BuffDurationRounds = 10, // Legacy fallback: 1 minute
            BuffType = "enlarge",
            BuffBonusType = BonusType.Size,
            BonusTypeExplicitlySet = true,
            DurationType = DurationType.Minutes,
            DurationValue = 1,
            DurationScalesWithLevel = true,
            AllowsSavingThrow = true, // Fortitude negates (unwilling)
            SavingThrowType = "Fortitude",
            ActionType = SpellActionType.FullRound,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "protection_from_evil",
            Name = "Protection from Evil",
            Description = "+2 deflection bonus to AC and +2 resistance bonus on saves vs evil creatures. 1 min/level. PHB p.266",
            SpellLevel = 1, School = "Abjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeCategory = SpellRangeCategory.Touch,
            EffectType = SpellEffectType.Buff,
            BuffDeflectionBonus = 2,
            BuffSaveBonus = 2,
            BuffDurationRounds = 30,
            BuffType = "protection",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "true_strike",
            Name = "True Strike",
            Description = "+20 insight bonus on your next single attack roll. PHB p.296",
            SpellLevel = 1, School = "Divination",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeCategory = SpellRangeCategory.Personal,
            EffectType = SpellEffectType.Buff,
            BuffAttackBonus = 20,
            BuffDurationRounds = 1, // Until next attack or end of next round
            BuffType = "insight",
            BuffBonusType = BonusType.Insight,
            BonusTypeExplicitlySet = true,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "expeditious_retreat",
            Name = "Expeditious Retreat",
            Description = "Your base speed increases by 30 ft (+6 squares). Duration 1 min/level. PHB p.228",
            SpellLevel = 1, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeCategory = SpellRangeCategory.Personal,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 30,
            BuffType = "speed",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        // --- FUNCTIONAL: Debuff Spells ---

        Register(new SpellData
        {
            SpellId = "sleep",
            Name = "Sleep",
            Description = "Puts 4 HD of creatures into magical slumber. Will save negates. Range: Medium. PHB p.280",
            SpellLevel = 1, School = "Enchantment",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeCategory = SpellRangeCategory.Medium,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 10, // 1 min/level
            ActionType = SpellActionType.FullRound,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "charm_person",
            Name = "Charm Person",
            Description = "Makes one humanoid creature friendly to you. Will save negates. Duration 1 hr/level. PHB p.209",
            SpellLevel = 1, School = "Enchantment",
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
            PlaceholderReason = "[PLACEHOLDER - Charm/mind control not implemented in combat]"
        });

        Register(new SpellData
        {
            SpellId = "grease",
            Name = "Grease",
            Description = "Covers ground in grease, creatures must save or fall. Reflex save. 1 round/level. PHB p.237",
            SpellLevel = 1, School = "Conjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy, // Simplified
            RangeCategory = SpellRangeCategory.Close,
            AreaRadius = 2,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Reflex",
            BuffDurationRounds = 3,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "color_spray",
            Name = "Color Spray",
            Description = "Knocks unconscious, blinds, and/or stuns weak creatures in 15-ft cone. Will save negates. PHB p.210",
            SpellLevel = 1, School = "Illusion",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy, // Simplified from cone
            RangeSquares = 3,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 3, // Varies by HD
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "cause_fear",
            Name = "Cause Fear",
            Description = "One creature of 5 HD or less flees for 1d4 rounds. Will save: shaken for 1 round instead. PHB p.208",
            SpellLevel = 1, School = "Necromancy",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeCategory = SpellRangeCategory.Close,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 3, // 1d4 rounds avg
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "ray_of_enfeeblement",
            Name = "Ray of Enfeeblement",
            Description = "Ranged touch attack. Target takes 1d6+1 per 2 CL (max +5) STR penalty. No save. 1 min/level. PHB p.269",
            SpellLevel = 1, School = "Necromancy",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeCategory = SpellRangeCategory.Close,
            IsTouch = true,
            IsRangedTouch = true,
            EffectType = SpellEffectType.Debuff,
            DamageDice = 6, DamageCount = 1, BonusDamage = 1, // 1d6+1 STR penalty at CL3
            DamageType = "str_penalty",
            BuffDurationRounds = 30,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "reduce_person",
            Name = "Reduce Person",
            Description = "Humanoid creature halves in size. -2 STR, +2 DEX, +1 AC/attack (size). 1 min/level. PHB p.269",
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

        // --- PLACEHOLDER: Utility/Complex ---

        Register(new SpellData
        {
            SpellId = "identify",
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
            SpellId = "feather_fall",
            Name = "Feather Fall",
            Description = "Objects or creatures fall slowly (60 ft/round). Immediate action. PHB p.229",
            SpellLevel = 1, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 1,
            ActionType = SpellActionType.Free, // Immediate action
            ProvokesAoO = false,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Falling mechanics not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "comprehend_languages",
            Name = "Comprehend Languages",
            Description = "You understand all spoken and written languages. Duration 10 min/level. PHB p.212",
            SpellLevel = 1, School = "Divination",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeCategory = SpellRangeCategory.Personal,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Language mechanics not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "summon_monster_1",
            Name = "Summon Monster I",
            Description = "Calls a creature to fight for you. Duration 1 round/level. PHB p.285",
            SpellLevel = 1, School = "Conjuration",
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
            SpellId = "unseen_servant",
            Name = "Unseen Servant",
            Description = "Invisible, mindless force that performs simple tasks. Duration 1 hr/level. PHB p.297",
            SpellLevel = 1, School = "Conjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Servant/minion not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "silent_image",
            Name = "Silent Image",
            Description = "Creates minor illusion of your design. Concentration + 2 rounds. Will disbelief. PHB p.279",
            SpellLevel = 1, School = "Illusion",
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
            SpellId = "disguise_self",
            Name = "Disguise Self",
            Description = "Changes your appearance. Duration 10 min/level. Will disbelief (if interacted). PHB p.222",
            SpellLevel = 1, School = "Illusion",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeCategory = SpellRangeCategory.Personal,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Disguise not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "obscuring_mist",
            Name = "Obscuring Mist",
            Description = "Fog surrounds you, granting concealment (20% miss chance). 20-ft radius. 1 min/level. PHB p.258",
            SpellLevel = 1, School = "Conjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 4,
            AreaRadius = 4,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 30,
            BuffType = "concealment",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Concealment/fog not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "magic_weapon",
            Name = "Magic Weapon",
            Description = "Weapon gains +1 enhancement bonus on attack and damage. 1 min/level. PHB p.251",
            SpellLevel = 1, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeCategory = SpellRangeCategory.Touch,
            EffectType = SpellEffectType.Buff,
            BuffAttackBonus = 1,
            BuffDamageBonus = 1,
            BuffDurationRounds = 30,
            BuffType = "enhancement",
            BuffBonusType = BonusType.Enhancement,
            BonusTypeExplicitlySet = true,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        Register(new SpellData
        {
            SpellId = "mount",
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

        Register(new SpellData
        {
            SpellId = "alarm",
            Name = "Alarm",
            Description = "Wards an area for 2 hours/level. Mental or audible alarm when creature enters. PHB p.197",
            SpellLevel = 1, School = "Abjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Ward/alarm not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "endure_elements",
            Name = "Endure Elements",
            Description = "Exist comfortably in hot or cold environments. Duration 24 hours. PHB p.226",
            SpellLevel = 1, School = "Abjuration",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleAlly,
            RangeCategory = SpellRangeCategory.Touch,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Environmental protection not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "hold_portal",
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

        Register(new SpellData
        {
            SpellId = "floating_disk",
            Name = "Floating Disk",
            Description = "Creates 3-ft diameter horizontal disk that holds 100 lb/level. Follows you. 1 hr/level. PHB p.232",
            SpellLevel = 1, School = "Evocation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeCategory = SpellRangeCategory.Personal,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Carrying/utility not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "hypnotism",
            Name = "Hypnotism",
            Description = "Fascinates 2d4 HD of creatures. Will save negates. PHB p.242",
            SpellLevel = 1, School = "Enchantment",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 5,
            EffectType = SpellEffectType.Debuff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 3,
            ActionType = SpellActionType.FullRound,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Fascination mechanics not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "nystuls_magic_aura",
            Name = "Nystul's Magic Aura",
            Description = "Alters an object's magic aura. Duration 1 day/level. PHB p.257",
            SpellLevel = 1, School = "Illusion",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeCategory = SpellRangeCategory.Touch,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Aura manipulation not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "ventriloquism",
            Name = "Ventriloquism",
            Description = "Throws voice for 1 min/level. Will disbelief. PHB p.298",
            SpellLevel = 1, School = "Illusion",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            BuffDurationRounds = 30,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Sound manipulation not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "animate_rope",
            Name = "Animate Rope",
            Description = "Makes a rope move at your command. Duration 1 round/level. PHB p.199",
            SpellLevel = 1, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 22,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 3,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Rope animation not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "erase",
            Name = "Erase",
            Description = "Mundane or magical writing vanishes. PHB p.227",
            SpellLevel = 1, School = "Transmutation",
            ClassList = new[] { "Wizard" },
            TargetType = SpellTargetType.Self,
            RangeSquares = 5,
            EffectType = SpellEffectType.Buff,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Writing mechanics not implemented]"
        });
    }

    // ====================================================================
    //  WIZARD 2ND LEVEL SPELLS
    // ====================================================================
}
