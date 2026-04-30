using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: D
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_D()
    {
        RegisterDog();
        RegisterRidingDog();
        RegisterDireRat();
    }

    private static void RegisterDog()
    {
        Register(new NPCDefinition
        {
            Id = "dog",
            Name = "Dog",
            ChallengeRating = "1/3",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 13, DEX = 17, CON = 15, WIS = 12, INT = 2, CHA = 6,
            NaturalArmorBonus = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8,
            BaseHitDieHP = 6,
            CreatureTags = new List<string> { "Animal", "MM35" },
            Feats = new List<string> { "Alertness", "Track" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Low-light vision", "Scent", "Skills: Jump +7, Listen +5, Spot +5, Survival +1" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.84f, 0.72f, 0.56f, 1f),
            PanelColor = new Color(0.22f, 0.15f, 0.1f, 0.85f),
            NameColor = new Color(0.98f, 0.9f, 0.8f),
            Description = "Monster Manual dog. Fast Small animal with scent and tracking-focused skill spread."
        });
    }

    private static void RegisterRidingDog()
    {
        Register(new NPCDefinition
        {
            Id = "riding_dog",
            Name = "Riding Dog",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 15, DEX = 15, CON = 15, WIS = 12, INT = 2, CHA = 6,
            BAB = 1,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8,
            BaseHitDieHP = 13,
            CreatureTags = new List<string> { "Animal", "MM35" },
            Feats = new List<string> { "Alertness", "Track" },
            HasScent = true,
            SpecialAbilities = new List<string>
            {
                "Low-light vision",
                "Scent",
                "Skills: Jump +8, Listen +5, Spot +5, Survival +1"
            },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.84f, 0.68f, 0.48f, 1f),
            PanelColor = new Color(0.24f, 0.15f, 0.09f, 0.85f),
            NameColor = new Color(0.99f, 0.88f, 0.72f),
            Description = "Monster Manual riding dog. Medium war-trained canine with high speed and a powerful bite."
        });
    }

    private static void RegisterDireRat()
    {
        Register(new NPCDefinition
        {
            Id = "dire_rat",
            Name = "Dire Rat",
            ChallengeRating = "1/3",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 10, DEX = 17, CON = 12, WIS = 12, INT = 1, CHA = 4,
            NaturalArmorBonus = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8,
            BaseHitDieHP = 5,
            CreatureTags = new List<string> { "Animal", "MM35" },
            Feats = new List<string> { "Alertness", "Weapon Finesse" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Low-light vision", "Disease (Filth Fever)", "Scent", "Climb 20 ft", "Skills: Climb +11, Hide +8, Listen +4, Move Silently +4, Spot +4, Swim +11" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.55f, 0.55f, 0.57f, 1f),
            PanelColor = new Color(0.18f, 0.18f, 0.2f, 0.85f),
            NameColor = new Color(0.88f, 0.88f, 0.92f),
            Description = "Monster Manual dire rat. Fast Small disease carrier with scent and exceptional climb/swim mobility."
        });
    }

}
