using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: S
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_S()
    {
        RegisterSpiderSwarm();
        RegisterMonstrousScorpions();
        RegisterMonstrousSpiders();
    
        RegisterSummonSmallViper();
    }

    private static void RegisterSpiderSwarm()
    {
        Register(new NPCDefinition
        {
            Id = "spider_swarm",
            Name = "Spider Swarm",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = 2,
            BaseAttackBonusOverride = 1, // Monster Manual BAB +1
            SizeCategory = SizeCategory.Large, // Swarm occupies 2×2 space in this prototype
            IsTallCreature = false,
            STR = 1, DEX = 17, CON = 10, WIS = 10, INT = CharacterStats.NO_SCORE, CHA = 2,
            NaturalArmorBonus = 0, // AC 17 = 10 +4 size +3 Dex
            BaseSpeed = 4, // 20 ft
            BaseHitDieHP = 9,
            CreatureTags = new List<string> { "Vermin", "Swarm", "MM35" },
            IsMindless = true,
            IsSwarm = true,
            CanMakeAttacksOfOpportunity = false,
            Immunities = ImmunityPresets.Combine(
                ImmunityPresets.SwarmImmunities(),
                ImmunityPresets.MindlessImmunities()),
            SwarmTraits = new SwarmTraits
            {
                IsSwarm = true,
                SwarmDamage = 6,
                SwarmDamageDice = "1d6",
                SwarmDamageType = DamageType.Piercing,
                DistractionDC = 11,
                HasPoison = true,
                PoisonId = "medium_spider_poison",
                PoisonDcModifier = -1 // Medium spider poison base DC 12 -> DC 11 for spider swarm
            },
            SpecialAbilities = new List<string>
            {
                "Swarm attack (1d6)",
                "Distraction (Fort DC 11)",
                "Poison (Fort DC 11; initial 1d3 Str; secondary 1d3 Str)",
                "Darkvision 60 ft",
                "Tremorsense 30 ft",
                "Climb 20 ft",
                "Vermin traits",
                "Mindless (Intelligence —)",
                "Swarm traits",
                "Alignment: True Neutral"
            },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Swarm,
            SpriteColor = new Color(0.18f, 0.18f, 0.18f, 1f),
            PanelColor = new Color(0.08f, 0.08f, 0.08f, 0.85f),
            NameColor = new Color(0.82f, 0.82f, 0.88f),
            Description = "Monster Manual spider swarm. Diminutive vermin swarm with toxic bite-cloud, tremorsense, and mindless vermin traits."
        });
    }

    private static void RegisterMonstrousScorpions()
    {
        RegisterMonstrousScorpionVariant("monstrous_scorpion_tiny", "Monstrous Scorpion (Tiny)", 1, SizeCategory.Tiny, 4, 10, 14, 3, 2, 20, 1, 2, 1, 2, 12, "1 Con", "1 Con");
        RegisterMonstrousScorpionVariant("monstrous_scorpion_small", "Monstrous Scorpion (Small)", 1, SizeCategory.Small, 6, 10, 14, 9, 3, 30, 1, 3, 1, 3, 12, "1d2 Con", "1d2 Con");
        RegisterMonstrousScorpionVariant("monstrous_scorpion_medium", "Monstrous Scorpion (Medium)", 2, SizeCategory.Medium, 13, 10, 14, 13, 4, 40, 1, 4, 1, 4, 13, "1d3 Con", "1d3 Con");
        RegisterMonstrousScorpionVariant("monstrous_scorpion_large", "Monstrous Scorpion (Large)", 5, SizeCategory.Large, 32, 10, 14, 19, 7, 50, 1, 6, 1, 6, 14, "1d4 Con", "1d4 Con");
        RegisterMonstrousScorpionVariant("monstrous_scorpion_huge", "Monstrous Scorpion (Huge)", 10, SizeCategory.Huge, 75, 10, 16, 23, 12, 50, 2, 4, 1, 8, 18, "1d6 Con", "1d6 Con");
        RegisterMonstrousScorpionVariant("monstrous_scorpion_gargantuan", "Monstrous Scorpion (Gargantuan)", 20, SizeCategory.Gargantuan, 150, 10, 16, 31, 18, 50, 2, 6, 2, 6, 23, "1d8 Con", "1d8 Con");
        RegisterMonstrousScorpionVariant("monstrous_scorpion_colossal", "Monstrous Scorpion (Colossal)", 40, SizeCategory.Colossal, 300, 8, 16, 35, 25, 50, 2, 8, 2, 8, 33, "1d10 Con", "1d10 Con");
    }

    private static void RegisterMonstrousScorpionVariant(string id, string name, int hitDice, SizeCategory size, int hp, int dex, int con, int str, int naturalArmor, int speed, int stingDamageCount, int stingDamageDice, int clawDamageCount, int clawDamageDice, int poisonDc, string poisonInitial, string poisonSecondary)
    {
        Register(new NPCDefinition
        {
            Id = id,
            Name = name,
            ChallengeRating = id switch
            {
                "monstrous_scorpion_tiny" => "1/4",
                "monstrous_scorpion_small" => "1/2",
                "monstrous_scorpion_medium" => "1",
                "monstrous_scorpion_large" => "3",
                "monstrous_scorpion_huge" => "7",
                "monstrous_scorpion_gargantuan" => "10",
                "monstrous_scorpion_colossal" => "12",
                _ => null
            },
            Level = Mathf.Max(1, hitDice),
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = Mathf.Max(1, hitDice),
            SizeCategory = size,
            IsTallCreature = false,
            STR = str, DEX = dex, CON = con, WIS = 10, INT = 1, CHA = 2,
            NaturalArmorBonus = naturalArmor,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = clawDamageDice, DamageCount = clawDamageCount, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Sting", DamageDice = stingDamageDice, DamageCount = stingDamageCount, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = speed / 5,
            BaseHitDieHP = hp,
            CreatureTags = new List<string> { "Vermin", "MM35" },
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Claw",
            SpecialAbilities = new List<string>
            {
                $"Poison (Fort DC {poisonDc}; initial {poisonInitial}; secondary {poisonSecondary})",
                "Constrict",
                "Improved Grab (claw)",
                "Darkvision 60 ft",
                "Tremorsense 60 ft",
                "Vermin traits",
                "Mindless"
            },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.58f, 0.34f, 0.22f, 1f),
            PanelColor = new Color(0.2f, 0.12f, 0.08f, 0.85f),
            NameColor = new Color(0.94f, 0.82f, 0.72f),
            Description = $"Monster Manual {name.ToLowerInvariant()}. Armored vermin with pincer grab into venomous sting pressure."
        });
    }

    private static void RegisterMonstrousSpiders()
    {
        RegisterMonstrousSpiderVariant("monstrous_spider_tiny", "Monstrous Spider (Tiny)", 1, SizeCategory.Tiny, 2, 17, 10, 3, 0, 20, 1, 3, 10, "1d2 Str", "1d2 Str", 10);
        RegisterMonstrousSpiderVariant("monstrous_spider_small", "Monstrous Spider (Small)", 1, SizeCategory.Small, 4, 17, 10, 7, 0, 30, 1, 4, 10, "1d3 Str", "1d3 Str", 10);
        RegisterMonstrousSpiderVariant("monstrous_spider_medium", "Monstrous Spider (Medium)", 2, SizeCategory.Medium, 11, 17, 12, 11, 1, 30, 1, 6, 12, "1d4 Str", "1d4 Str", 12);
        RegisterMonstrousSpiderVariant("monstrous_spider_large", "Monstrous Spider (Large)", 4, SizeCategory.Large, 22, 17, 12, 15, 2, 30, 1, 8, 13, "1d6 Str", "1d6 Str", 13);
        RegisterMonstrousSpiderVariant("monstrous_spider_huge", "Monstrous Spider (Huge)", 8, SizeCategory.Huge, 52, 17, 14, 19, 5, 30, 2, 6, 16, "1d8 Str", "1d8 Str", 16);
        RegisterMonstrousSpiderVariant("monstrous_spider_gargantuan", "Monstrous Spider (Gargantuan)", 16, SizeCategory.Gargantuan, 104, 17, 14, 25, 10, 30, 2, 8, 20, "2d6 Str", "2d6 Str", 20);
        RegisterMonstrousSpiderVariant("monstrous_spider_colossal", "Monstrous Spider (Colossal)", 32, SizeCategory.Colossal, 208, 15, 14, 31, 18, 30, 4, 6, 28, "2d8 Str", "2d8 Str", 28);
    }

    private static void RegisterMonstrousSpiderVariant(string id, string name, int hitDice, SizeCategory size, int hp, int dex, int con, int str, int naturalArmor, int speed, int damageCount, int damageDice, int poisonDc, string poisonInitial, string poisonSecondary, int webDc)
    {
        // Map spider size to poison ID
        string poisonId = id switch
        {
            "monstrous_spider_medium" => "medium_spider_poison",
            "monstrous_spider_large" => "large_spider_poison",
            _ => null
        };

        Register(new NPCDefinition
        {
            Id = id,
            Name = name,
            ChallengeRating = id switch
            {
                "monstrous_spider_tiny" => "1/4",
                "monstrous_spider_small" => "1/2",
                "monstrous_spider_medium" => "1",
                "monstrous_spider_large" => "2",
                "monstrous_spider_huge" => "5",
                "monstrous_spider_gargantuan" => "8",
                "monstrous_spider_colossal" => "11",
                _ => null
            },
            Level = Mathf.Max(1, hitDice),
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = Mathf.Max(1, hitDice),
            SizeCategory = size,
            IsTallCreature = false,
            STR = str, DEX = dex, CON = con, WIS = 10, INT = 1, CHA = 2,
            NaturalArmorBonus = naturalArmor,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = damageDice, DamageCount = damageCount, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true, PoisonOnHitId = poisonId }
            },
            BaseSpeed = speed / 5,
            BaseHitDieHP = hp,
            CreatureTags = new List<string> { "Vermin", "MM35" },
            SpecialAbilities = new List<string>
            {
                $"Poison (Fort DC {poisonDc}; initial {poisonInitial}; secondary {poisonSecondary})",
                $"Web (Escape Artist/Strength DC {webDc})",
                "Tremorsense 60 ft",
                "Darkvision 60 ft",
                "Vermin traits",
                "Mindless"
            },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.22f, 0.22f, 0.22f, 1f),
            PanelColor = new Color(0.1f, 0.1f, 0.1f, 0.85f),
            NameColor = new Color(0.86f, 0.86f, 0.9f),
            Description = $"Monster Manual {name.ToLowerInvariant()}. Web-spinning ambush vermin with toxic bite and tremorsense."
        });
    }



    private static void RegisterSummonSmallViper()
    {
        Register(new NPCDefinition
        {
            Id = "small_viper",
            Name = "Small Viper",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 6, DEX = 17, CON = 11, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 3, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 6,
            BaseHitDieHP = 6,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.45f, 0.72f, 0.34f, 1f),
            PanelColor = new Color(0.15f, 0.23f, 0.12f, 0.85f),
            NameColor = new Color(0.84f, 0.94f, 0.8f),
            Description = "Summon Monster baseline Small Viper."
        });
    }

}
