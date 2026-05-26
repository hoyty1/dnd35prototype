using System.Collections.Generic;
using UnityEngine;

public static partial class NPCDatabase
{
    private static void RegisterCreatures_P()
    {
        RegisterPhantomFungus();
        RegisterPhaseSpider();
        RegisterPhasm();
        RegisterPurpleWorm();
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
}
