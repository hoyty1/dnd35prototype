using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

public static partial class NPCDatabase
{
    private static void RegisterCreatures_P()
    {
        RegisterPhantomFungus();
        RegisterPhaseSpider();
        RegisterPhasm();
        RegisterPurpleWorm();
        RegisterHumanPaladin3();
        RegisterHumanPaladin5();
        RegisterHumanPaladin7();
    }

    private static void RegisterPhantomFungus()
    {
        Register(new NPCDefinition
        {
            Id = "phantom_fungus",
            Name = "Phantom Fungus",
            ChallengeRating = "3",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Plant",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 14, DEX = 10, CON = 16, WIS = 14, INT = 2, CHA = 9,
            NaturalArmorBonus = 4,
            BaseSpeed = 4, // 20 ft, climb 20 ft
            BaseHitDieHP = 15,
            BAB = 1,
            Immunities = new CreatureImmunities
            {
                immuneToMindAffecting = true,
                immuneToPoison = true,
                immuneToCriticalHits = true,
                immuneToSneakAttack = true
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Plant", "NaturalInvisibility", "Tremorsense30", "MM35" },
            Feats = new List<string> { "Improved Initiative" },
            SpecialAbilities = new List<string> { "Greater Invisibility (Su): naturally invisible at all times, visible only when dead", "Plant traits", "Tremorsense 30 ft.", "Climb 20 ft.", "Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.5f, 0.5f, 0.5f, 0.3f),
            PanelColor = new Color(0.18f, 0.18f, 0.2f, 0.85f),
            NameColor = new Color(0.7f, 0.7f, 0.75f),
            Description = "Phantom Fungus (CR 3). Naturally invisible hunting fungus. MM 3.5e p.207."
        });
    }

    private static void RegisterPhaseSpider()
    {
        Register(new NPCDefinition
        {
            Id = "phase_spider",
            Name = "Phase Spider",
            ChallengeRating = "5",
            Level = 5,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 5,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 17, DEX = 15, CON = 16, WIS = 13, INT = 7, CHA = 10,
            NaturalArmorBonus = 4,
            BaseSpeed = 8, // 40 ft, climb 20 ft
            BaseHitDieHP = 42,
            BAB = 5,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 2, IsPrimary = true, PoisonOnHitId = "phase_spider_poison" }
            },
            CreatureTags = new List<string> { "Magical Beast", "Darkvision60", "MM35" },
            Feats = new List<string> { "Ability Focus (poison)", "Improved Initiative" },
            SpecialAbilities = new List<string> { "Ethereal Jaunt (Su): at will, as ethereal jaunt (CL 15)", "Poison (Ex): bite, DC 17 Fort, 1d6 Con/1d6 Con", "Darkvision 60 ft., Low-light vision", "Tremorsense 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.4f, 0.35f, 0.5f, 0.8f),
            PanelColor = new Color(0.12f, 0.1f, 0.2f, 0.85f),
            NameColor = new Color(0.62f, 0.55f, 0.78f),
            Description = "Phase Spider (CR 5). Spider that shifts between Ethereal and Material planes. MM 3.5e p.207."
        });
    }

    private static void RegisterPhasm()
    {
        Register(new NPCDefinition
        {
            Id = "phasm",
            Name = "Phasm",
            ChallengeRating = "7",
            Level = 15,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.ChaoticNeutral,
            HitDice = 15,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 12, DEX = 15, CON = 12, WIS = 14, INT = 16, CHA = 14,
            NaturalArmorBonus = 5,
            BaseSpeed = 6,
            BaseHitDieHP = 82,
            BAB = 11,
            Immunities = new CreatureImmunities { immuneToPoison = true },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 3, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Aberration", "Shapechanger", "Telepathy100", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Blind-Fight", "Combat Reflexes", "Improved Initiative", "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Alternate Form (Su): at will, any Small–Large creature", "Amorphous (Ex): immune to poison, sleep, paralysis, polymorph, stunning, crits", "Resilient (Ex): +4 racial saves vs mind-affecting", "Tremorsense 60 ft.", "Scent", "Telepathy 100 ft.", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.55f, 0.55f, 0.6f, 1f),
            PanelColor = new Color(0.2f, 0.2f, 0.25f, 0.85f),
            NameColor = new Color(0.78f, 0.78f, 0.88f),
            Description = "Phasm (CR 7). Shapeshifting aberration with at-will alternate form. MM 3.5e p.207."
        });
    }

    /// <summary>
    /// Purple Worm (CR 12) — Gargantuan magical beast.
    /// MM 3.5e p.211. 16d10+112 HP (200), bite 2d8+12 + swallow whole, sting 2d6+6 + poison.
    /// Swallow Whole: bite + improved grab → grapple → on next turn, swallows (2d8+12 crushing + 1d8 acid).
    /// Poison sting: Fort DC 25 or 1d6 Str damage (primary and secondary).
    /// Tremorsense 60 ft.
    /// </summary>
    private static void RegisterPurpleWorm()
    {
        Register(new NPCDefinition
        {
            Id = "purple_worm",
            Name = "Purple Worm",
            ChallengeRating = "12",
            Level = 16,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            CharacterAlignment = Alignment.None,
            HitDice = 16,
            SizeCategory = SizeCategory.Gargantuan,
            IsTallCreature = false,
            STR = 35, DEX = 10, CON = 25, WIS = 8, INT = 1, CHA = 8,
            NaturalArmorBonus = 11,
            BaseSpeed = 4, // 20 ft.
            BaseHitDieHP = 200,
            BAB = 12,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Bite",
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Bite", DamageDice = 8, DamageCount = 2, Count = 1,
                    BonusDamageSource = DamageBonusSource.Strength, Range = 3, IsPrimary = true
                },
                new NaturalAttackDefinition
                {
                    Name = "Sting", DamageDice = 6, DamageCount = 2, Count = 1,
                    BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 3, IsPrimary = false,
                    PoisonOnHitId = "purple_worm_poison"
                }
            },
            CreatureTags = new List<string> { "MagicalBeast", "Burrowing", "MM35" },
            Feats = new List<string> { "Awesome Blow", "Cleave", "Great Cleave", "Improved Bull Rush", "Power Attack", "Weapon Focus (bite)" },
            SpecialAbilities = new List<string> { "Improved Grab (bite)", "Swallow Whole: 2d8+12 crushing + 1d8 acid, AC 17 from inside, 25 HP to cut out", "Poison (Ex): sting, Fort DC 25, 1d6 Str/1d6 Str", "Tremorsense 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.5f, 0.2f, 0.55f, 1f),
            PanelColor = new Color(0.2f, 0.05f, 0.22f, 0.85f),
            NameColor = new Color(0.8f, 0.4f, 0.85f),
            Description = "Purple Worm (CR 12). Gargantuan burrower that swallows whole and has poison sting. MM 3.5e p.211."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Leveled Human Paladin NPCs (PHB 3.5e)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Human Paladin 3 (CR 3) — Medium humanoid (human).
    /// PHB 3.5e Paladin class level 3. Full plate, longsword, heavy steel shield.
    /// Divine Grace, Lay on Hands, Smite Evil 1/day, Aura of Courage, Divine Health.
    /// </summary>
    private static void RegisterHumanPaladin3()
    {
        Register(new NPCDefinition
        {
            Id = "human_paladin_3",
            Name = "Human Paladin",
            ChallengeRating = "3",
            Level = 3,
            CharacterClass = "Paladin",
            CreatureType = "Humanoid",
            CharacterAlignment = Alignment.LawfulGood,
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            // PHB Paladin: high STR, CHA for divine abilities, decent CON
            STR = 16, DEX = 10, CON = 14, WIS = 12, INT = 8, CHA = 15,
            NaturalArmorBonus = 0,
            BaseSpeed = 4, // 20 ft in full plate
            BaseHitDieHP = 24, // 3d10+6 average
            BAB = 3, // Paladin 3: +3 (full BAB)
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Humanoid", "Human", "Paladin", "MM35" },
            Feats = new List<string> { "Weapon Focus (longsword)", "Power Attack", "Cleave" },
            SpecialAbilities = new List<string>
            {
                "Detect Evil (Sp): at will",
                "Smite Evil 1/day: +2 attack, +3 damage vs. evil",
                "Divine Grace (Su): +2 CHA bonus to all saves",
                "Lay on Hands (Su): heal 6 HP/day (Paladin level × CHA mod)",
                "Aura of Courage (Su): immune to fear, allies within 10 ft. +4 vs. fear",
                "Divine Health (Ex): immune to all diseases",
                "Aura of Good"
            },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.LONGSWORD, EquipSlot.MainHand),
                new EquipmentSlotPair(ItemIDs.SHIELD_HEAVY_STEEL, EquipSlot.OffHand),
                new EquipmentSlotPair(ItemIDs.FULL_PLATE, EquipSlot.Armor)
            },
            BackpackItemIds = new List<string> { ItemIDs.LANCE, ItemIDs.POTION_CURE_LIGHT_WOUNDS },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.85f, 0.8f, 0.5f, 1f),
            PanelColor = new Color(0.3f, 0.28f, 0.12f, 0.85f),
            NameColor = new Color(1f, 0.95f, 0.65f),
            Description = "Human Paladin 3 (CR 3). Holy warrior in full plate with Smite Evil, Divine Grace, and Aura of Courage. PHB 3.5e."
        });
    }

    /// <summary>
    /// Human Paladin 5 (CR 5) — Medium humanoid (human).
    /// PHB 3.5e Paladin class level 5. Smite Evil 2/day. 1st-level divine spells.
    /// Special mount available.
    /// </summary>
    private static void RegisterHumanPaladin5()
    {
        Register(new NPCDefinition
        {
            Id = "human_paladin_5",
            Name = "Human Paladin",
            ChallengeRating = "5",
            Level = 5,
            CharacterClass = "Paladin",
            CreatureType = "Humanoid",
            CharacterAlignment = Alignment.LawfulGood,
            HitDice = 5,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 16, DEX = 10, CON = 14, WIS = 12, INT = 8, CHA = 15,
            NaturalArmorBonus = 0,
            BaseSpeed = 4, // 20 ft in full plate
            BaseHitDieHP = 40, // 5d10+10 average
            BAB = 5, // Paladin 5: +5 (full BAB)
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Humanoid", "Human", "Paladin", "MM35" },
            Feats = new List<string> { "Weapon Focus (longsword)", "Power Attack", "Cleave", "Mounted Combat" },
            SpecialAbilities = new List<string>
            {
                "Detect Evil (Sp): at will",
                "Smite Evil 2/day: +2 attack, +5 damage vs. evil",
                "Divine Grace (Su): +2 CHA bonus to all saves",
                "Lay on Hands (Su): heal 10 HP/day",
                "Aura of Courage (Su): immune to fear, allies +4 vs. fear",
                "Divine Health (Ex): immune to all diseases",
                "Turn Undead 5/day (as 2nd-level cleric)",
                "Remove Disease 1/week",
                "Spells: 1st-level divine (bless, protection from evil)",
                "Special Mount available"
            },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.LONGSWORD, EquipSlot.MainHand),
                new EquipmentSlotPair(ItemIDs.SHIELD_HEAVY_STEEL, EquipSlot.OffHand),
                new EquipmentSlotPair(ItemIDs.FULL_PLATE, EquipSlot.Armor)
            },
            BackpackItemIds = new List<string> { ItemIDs.LANCE, ItemIDs.POTION_CURE_LIGHT_WOUNDS, ItemIDs.POTION_CURE_LIGHT_WOUNDS },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.85f, 0.8f, 0.5f, 1f),
            PanelColor = new Color(0.3f, 0.28f, 0.12f, 0.85f),
            NameColor = new Color(1f, 0.95f, 0.65f),
            Description = "Human Paladin 5 (CR 5). Holy warrior with Smite Evil 2/day, Turn Undead, and 1st-level divine spells. PHB 3.5e."
        });
    }

    /// <summary>
    /// Human Paladin 7 (CR 7) — Medium humanoid (human).
    /// PHB 3.5e Paladin class level 7. Smite Evil 2/day. 1st and 2nd-level spells.
    /// Remove Disease 2/week.
    /// </summary>
    private static void RegisterHumanPaladin7()
    {
        Register(new NPCDefinition
        {
            Id = "human_paladin_7",
            Name = "Human Paladin",
            ChallengeRating = "7",
            Level = 7,
            CharacterClass = "Paladin",
            CreatureType = "Humanoid",
            CharacterAlignment = Alignment.LawfulGood,
            HitDice = 7,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            // Level 7: +1 STR from level 4 ability increase
            STR = 17, DEX = 10, CON = 14, WIS = 12, INT = 8, CHA = 15,
            NaturalArmorBonus = 0,
            BaseSpeed = 4, // 20 ft in full plate
            BaseHitDieHP = 56, // 7d10+14 average
            BAB = 7, // Paladin 7: +7 (full BAB)
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Humanoid", "Human", "Paladin", "MM35" },
            Feats = new List<string> { "Weapon Focus (longsword)", "Power Attack", "Cleave", "Mounted Combat", "Improved Initiative" },
            SpecialAbilities = new List<string>
            {
                "Detect Evil (Sp): at will",
                "Smite Evil 2/day: +2 attack, +7 damage vs. evil",
                "Divine Grace (Su): +2 CHA bonus to all saves",
                "Lay on Hands (Su): heal 14 HP/day",
                "Aura of Courage (Su): immune to fear, allies +4 vs. fear",
                "Divine Health (Ex): immune to all diseases",
                "Turn Undead 5/day (as 4th-level cleric)",
                "Remove Disease 2/week",
                "Spells: 1st/2nd-level divine (bless, bull's strength, protection from evil)",
                "Special Mount"
            },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.LONGSWORD, EquipSlot.MainHand),
                new EquipmentSlotPair(ItemIDs.SHIELD_HEAVY_STEEL, EquipSlot.OffHand),
                new EquipmentSlotPair(ItemIDs.FULL_PLATE, EquipSlot.Armor)
            },
            BackpackItemIds = new List<string> { ItemIDs.LANCE, ItemIDs.POTION_CURE_LIGHT_WOUNDS, ItemIDs.POTION_CURE_LIGHT_WOUNDS },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.85f, 0.8f, 0.5f, 1f),
            PanelColor = new Color(0.3f, 0.28f, 0.12f, 0.85f),
            NameColor = new Color(1f, 0.95f, 0.65f),
            Description = "Human Paladin 7 (CR 7). Veteran holy warrior with Smite Evil, Turn Undead, and 2nd-level divine spells. PHB 3.5e."
        });
    }
}
