// ============================================================================
// SpellDatabase_M.cs — Spells starting with M
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

public static partial class SpellDatabase
{
    private static void RegisterSpellsM()
    {
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
                    SpellId = "mage_hand",
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
                    SpellId = "melfs_acid_arrow",
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
                    SpellId = "domain_magic_stone",
                    Name = "Magic Stone",
                    Description = "Three stones gain +1 on attack and deal 1d6+1 damage.",
                    SpellLevel = 1,
                    School = "Transmutation",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Magic stone projectiles not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = "magic_weapon",
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

        Register(new SpellData
                {
                    SpellId = "make_whole",
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
                    SpellId = "mending",
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
                    SpellId = "message",
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
                    SpellId = "mirror_image",
                    Name = "Mirror Image",
                    Description = "Creates 1d4+1 decoy duplicates of you (CL3). Attacks may hit images instead. 1 min/level. PHB p.254",
                    SpellLevel = 2, School = "Illusion",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffACBonus = 4, // Simplified: effective +4 AC from images
                    BuffDurationRounds = 30,
                    BuffType = "mirror_image",
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

        // Aliases
        RegisterClassSpellAlias("magic_weapon_clr", "magic_weapon", "Cleric", 1);
        RegisterClassSpellAlias("mending_clr", "mending", "Cleric", 0);

    }
}
