using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: V
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_V()
    {
        RegisterViperTiny();
        RegisterViperSmall();
        RegisterViperMedium();
        RegisterViperLarge();
        RegisterViperHuge();
    }

    private static void RegisterViperTiny()
    {
        Register(new NPCDefinition
        {
            Id = "viper_tiny",
            Name = "Viper (Tiny)",
            ChallengeRating = "1/3",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 1,
            SizeCategory = SizeCategory.Tiny,
            IsTallCreature = false,
            STR = 4, DEX = 17, CON = 11, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 2, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.None, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 3,
            BaseHitDieHP = 1,
            CreatureTags = new List<string> { "Animal", "MM35", "Snake" },
            Feats = new List<string> { "Improved Initiative", "Weapon Finesse" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Poison (Fort DC 10; initial 1d6 Con; secondary 1d6 Con)", "Climb 15 ft", "Swim 15 ft", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.42f, 0.72f, 0.34f, 1f),
            PanelColor = new Color(0.14f, 0.24f, 0.11f, 0.85f),
            NameColor = new Color(0.84f, 0.94f, 0.8f),
            Description = "Monster Manual tiny viper. Venomous familiar-scale snake with scent and mixed movement modes."
        });
    }

    private static void RegisterViperSmall()
    {
        Register(new NPCDefinition
        {
            Id = "viper_small",
            Name = "Viper (Small)",
            ChallengeRating = "1/2",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 6, DEX = 17, CON = 11, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 2, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 4,
            BaseHitDieHP = 4,
            CreatureTags = new List<string> { "Animal", "MM35", "Snake" },
            Feats = new List<string> { "Improved Initiative", "Weapon Finesse" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Poison (Fort DC 10; initial 1d6 Con; secondary 1d6 Con)", "Climb 20 ft", "Swim 20 ft", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.38f, 0.68f, 0.3f, 1f),
            PanelColor = new Color(0.12f, 0.21f, 0.1f, 0.85f),
            NameColor = new Color(0.82f, 0.92f, 0.78f),
            Description = "Monster Manual small viper. Agile poisonous snake with scent and climbing/swimming mobility."
        });
    }

    private static void RegisterViperMedium()
    {
        Register(new NPCDefinition
        {
            Id = "viper_medium",
            Name = "Viper (Medium)",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 8, DEX = 17, CON = 11, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 4,
            BaseHitDieHP = 9,
            CreatureTags = new List<string> { "Animal", "MM35", "Snake" },
            Feats = new List<string> { "Weapon Finesse" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Poison (Fort DC 11; initial 1d6 Con; secondary 1d6 Con)", "Climb 20 ft", "Swim 20 ft", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.36f, 0.64f, 0.27f, 1f),
            PanelColor = new Color(0.11f, 0.19f, 0.09f, 0.85f),
            NameColor = new Color(0.8f, 0.9f, 0.76f),
            Description = "Monster Manual medium viper. Core serpent baseline for poison-focused animal encounters."
        });
    }

    private static void RegisterViperLarge()
    {
        Register(new NPCDefinition
        {
            Id = "viper_large",
            Name = "Viper (Large)",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 3,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 10, DEX = 17, CON = 11, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 4,
            BaseHitDieHP = 13,
            CreatureTags = new List<string> { "Animal", "MM35", "Snake" },
            Feats = new List<string> { "Improved Initiative", "Weapon Finesse" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Poison (Fort DC 11; initial 1d6 Con; secondary 1d6 Con)", "Climb 20 ft", "Swim 20 ft", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.34f, 0.6f, 0.25f, 1f),
            PanelColor = new Color(0.1f, 0.18f, 0.08f, 0.85f),
            NameColor = new Color(0.78f, 0.88f, 0.72f),
            Description = "Monster Manual large viper. Broad-bodied venomous snake suited for mid-tier wilderness fights."
        });
    }

    private static void RegisterViperHuge()
    {
        Register(new NPCDefinition
        {
            Id = "viper_huge",
            Name = "Viper (Huge)",
            ChallengeRating = "4",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 6,
            SizeCategory = SizeCategory.Huge,
            IsTallCreature = false,
            STR = 16, DEX = 15, CON = 13, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 5,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 4,
            BaseHitDieHP = 33,
            CreatureTags = new List<string> { "Animal", "MM35", "Snake" },
            Feats = new List<string> { "Improved Initiative", "Run", "Weapon Focus" },
            WeaponFocusChoice = "Bite",
            HasScent = true,
            SpecialAbilities = new List<string> { "Poison (Fort DC 14; initial 1d6 Con; secondary 1d6 Con)", "Climb 20 ft", "Swim 20 ft", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.3f, 0.54f, 0.22f, 1f),
            PanelColor = new Color(0.09f, 0.16f, 0.07f, 0.85f),
            NameColor = new Color(0.76f, 0.86f, 0.7f),
            Description = "Monster Manual huge viper. High-HD giant serpent with potent Constitution poison and long reach."
        });
    }

}
