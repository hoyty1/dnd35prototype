using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Domain-specific spells for D&D 3.5e cleric domains.
/// These are spells that only appear on domain spell lists and aren't part of
/// the standard Cleric or Wizard spell lists. Some are placeholder implementations.
/// </summary>
public static partial class SpellDatabase
{
    public static void RegisterDomainSpells()
    {
        // ========== LAW DOMAIN ==========
        Register(new SpellData
        {
            SpellId = "domain_protection_from_chaos",
            Name = "Protection from Chaos",
            Description = "+2 deflection AC and +2 resistance on saves against chaotic creatures.",
            SpellLevel = 1,
            School = "Abjuration",
            ClassList = new string[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeCategory = SpellRangeCategory.Touch,
            EffectType = SpellEffectType.Buff,
            BuffACBonus = 2,
            BuffDurationRounds = 30, // 1 min/level
            BuffType = "protection_alignment",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Alignment protection not fully implemented]"
        });

        // ========== EVIL DOMAIN ==========
        Register(new SpellData
        {
            SpellId = "domain_protection_from_good",
            Name = "Protection from Good",
            Description = "+2 deflection AC and +2 resistance on saves against good creatures.",
            SpellLevel = 1,
            School = "Abjuration",
            ClassList = new string[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeCategory = SpellRangeCategory.Touch,
            EffectType = SpellEffectType.Buff,
            BuffACBonus = 2,
            BuffDurationRounds = 30,
            BuffType = "protection_alignment",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Alignment protection not fully implemented]"
        });

        Register(new SpellData
        {
            SpellId = "domain_desecrate",
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

        // ========== CHAOS DOMAIN ==========
        Register(new SpellData
        {
            SpellId = "domain_protection_from_law",
            Name = "Protection from Law",
            Description = "+2 deflection AC and +2 resistance on saves against lawful creatures.",
            SpellLevel = 1,
            School = "Abjuration",
            ClassList = new string[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeCategory = SpellRangeCategory.Touch,
            EffectType = SpellEffectType.Buff,
            BuffACBonus = 2,
            BuffDurationRounds = 30,
            BuffType = "protection_alignment",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Alignment protection not fully implemented]"
        });

        // ========== KNOWLEDGE DOMAIN ==========
        Register(new SpellData
        {
            SpellId = "domain_detect_secret_doors",
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

        // ========== SUN DOMAIN ==========
        Register(new SpellData
        {
            SpellId = "domain_heat_metal",
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

        // ========== AIR DOMAIN ==========
        Register(new SpellData
        {
            SpellId = "domain_wind_wall",
            Name = "Wind Wall",
            Description = "Deflects arrows, smaller creatures, and gases. Creates an invisible wall of wind.",
            SpellLevel = 2,
            School = "Evocation",
            ClassList = new string[] { "Cleric" },
            TargetType = SpellTargetType.Area,
            RangeSquares = 6,
            AreaRadius = 2,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = 30,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Wind wall deflection not implemented]"
        });

        // ========== ANIMAL DOMAIN ==========
        Register(new SpellData
        {
            SpellId = "domain_calm_animals",
            Name = "Calm Animals",
            Description = "Calms 2d4+level HD of animals, rendering them docile and harmless.",
            SpellLevel = 1,
            School = "Enchantment",
            ClassList = new string[] { "Cleric" },
            TargetType = SpellTargetType.Area,
            RangeSquares = 5,
            AreaRadius = 2,
            EffectType = SpellEffectType.Debuff,
            BuffDurationRounds = 30,
            AllowsSavingThrow = true,
            SavingThrowType = "Will",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Animal calming not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "domain_hold_animal",
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

        // ========== EARTH DOMAIN ==========
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
            SpellId = "domain_soften_earth",
            Name = "Soften Earth and Stone",
            Description = "Turns stone to clay or dirt to sand/mud.",
            SpellLevel = 2,
            School = "Transmutation",
            ClassList = new string[] { "Cleric" },
            TargetType = SpellTargetType.Area,
            RangeSquares = 5,
            AreaRadius = 3,
            EffectType = SpellEffectType.Debuff,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Terrain modification not implemented]"
        });

        // ========== FIRE DOMAIN ==========
        Register(new SpellData
        {
            SpellId = "domain_produce_flame",
            Name = "Produce Flame",
            Description = "Flames appear in your hand dealing 1d6+level fire damage as touch or ranged touch.",
            SpellLevel = 2,
            School = "Evocation",
            ClassList = new string[] { "Cleric" },
            TargetType = SpellTargetType.SingleEnemy,
            RangeSquares = 24,
            EffectType = SpellEffectType.Damage,
            DamageDice = 6,
            DamageCount = 1,
            BonusDamage = 3,
            DamageType = "fire",
            BuffDurationRounds = 30,
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Sustained flame not fully implemented]"
        });

        // ========== PLANT DOMAIN ==========
        Register(new SpellData
        {
            SpellId = "domain_entangle",
            Name = "Entangle",
            Description = "Grasses and weeds entangle creatures in 40-ft radius spread. Entangled creatures can break free with Strength or Escape Artist check.",
            SpellLevel = 1,
            School = "Transmutation",
            ClassList = new string[] { "Cleric" },
            TargetType = SpellTargetType.Area,
            RangeSquares = 8,
            AreaRadius = 8,
            EffectType = SpellEffectType.Debuff,
            BuffDurationRounds = 30,
            AllowsSavingThrow = true,
            SavingThrowType = "Reflex",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Entangle/grapple not implemented]"
        });

        Register(new SpellData
        {
            SpellId = "domain_barkskin",
            Name = "Barkskin",
            Description = "Grants +2 enhancement bonus to natural armor (+1 for every three levels above 3rd, max +5 at 12th).",
            SpellLevel = 2,
            School = "Transmutation",
            ClassList = new string[] { "Cleric" },
            TargetType = SpellTargetType.SingleAlly,
            RangeCategory = SpellRangeCategory.Touch,
            EffectType = SpellEffectType.Buff,
            BuffACBonus = 2,
            BuffDurationRounds = 30,
            BuffType = "natural_armor",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true
        });

        // ========== TRAVEL DOMAIN ==========
        Register(new SpellData
        {
            SpellId = "domain_longstrider",
            Name = "Longstrider",
            Description = "Your speed increases by 10 feet (+2 squares movement).",
            SpellLevel = 1,
            School = "Transmutation",
            ClassList = new string[] { "Cleric" },
            TargetType = SpellTargetType.Self,
            RangeCategory = SpellRangeCategory.Personal,
            EffectType = SpellEffectType.Buff,
            BuffDurationRounds = -1, // 1 hour/level
            BuffType = "speed",
            ActionType = SpellActionType.Standard,
            ProvokesAoO = true,
            IsPlaceholder = true,
            PlaceholderReason = "[PLACEHOLDER - Speed buff not implemented]"
        });

        Debug.Log("[SpellDatabase] Registered domain-specific spells.");
    }
}
