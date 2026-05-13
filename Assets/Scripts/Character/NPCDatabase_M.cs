using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: M
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_M()
    {
        RegisterMonkey();
    
        RegisterSummonMonstrousCentipedeMedium();
        RegisterSummonMonstrousScorpionSmall();
        RegisterSummonMonstrousSpiderSmall();
    }

    private static void RegisterMonkey()
    {
        Register(new NPCDefinition
        {
            Id = "monkey",
            Name = "Monkey",
            ChallengeRating = "1/6",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 1,
            SizeCategory = SizeCategory.Tiny,
            IsTallCreature = false,
            STR = 3, DEX = 15, CON = 10, WIS = 12, INT = 2, CHA = 5,
            NaturalArmorBonus = 0,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 3, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 6,
            BaseHitDieHP = 4,
            CreatureTags = new List<string> { "Animal", "MM35", "Climb" },
            Feats = new List<string> { "Agile", "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Low-light vision", "Climb 30 ft", "Skills: Balance +12, Climb +10, Escape Artist +4, Hide +10, Listen +3, Spot +3" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.72f, 0.62f, 0.48f, 1f),
            PanelColor = new Color(0.22f, 0.16f, 0.1f, 0.85f),
            NameColor = new Color(0.95f, 0.88f, 0.76f),
            Description = "Monster Manual monkey. Tiny climber with agile bite and strong movement utility."
        });
    }


    private static void RegisterSummonMonstrousCentipedeMedium()
    {
        Register(new NPCDefinition
        {
            Id = "monstrous_centipede_medium",
            Name = "Monstrous Centipede (Medium)",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = 1,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 9, DEX = 15, CON = 10, WIS = 10, INT = 1, CHA = 2,
            NaturalArmorBonus = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8,
            BaseHitDieHP = 7,
            CreatureTags = new List<string> { "Vermin", "SummonBase" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.56f, 0.44f, 0.3f, 1f),
            PanelColor = new Color(0.18f, 0.12f, 0.07f, 0.85f),
            NameColor = new Color(0.92f, 0.82f, 0.71f),
            Description = "Summon Monster baseline Monstrous Centipede (Medium)."
        });
    }

    private static void RegisterSummonMonstrousScorpionSmall()
    {
        Register(new NPCDefinition
        {
            Id = "monstrous_scorpion_small",
            Name = "Monstrous Scorpion (Small)",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 8, DEX = 13, CON = 10, WIS = 10, INT = 1, CHA = 2,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 3, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Sting", DamageDice = 3, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 8,
            BaseHitDieHP = 7,
            CreatureTags = new List<string> { "Vermin", "SummonBase" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.51f, 0.4f, 0.29f, 1f),
            PanelColor = new Color(0.17f, 0.11f, 0.07f, 0.85f),
            NameColor = new Color(0.9f, 0.8f, 0.67f),
            Description = "Summon Monster baseline Monstrous Scorpion (Small)."
        });
    }

    private static void RegisterSummonMonstrousSpiderSmall()
    {
        Register(new NPCDefinition
        {
            Id = "monstrous_spider_small",
            Name = "Monstrous Spider (Small)",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 9, DEX = 15, CON = 10, WIS = 10, INT = 1, CHA = 2,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8,
            BaseHitDieHP = 7,
            CreatureTags = new List<string> { "Vermin", "SummonBase" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.3f, 0.3f, 0.3f, 1f),
            PanelColor = new Color(0.13f, 0.13f, 0.13f, 0.85f),
            NameColor = new Color(0.82f, 0.82f, 0.82f),
            Description = "Summon Monster baseline Monstrous Spider (Small)."
        });
    }

}
