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
    
        RegisterSummonDireBat();
        RegisterSummonDireBadger();
        RegisterSummonDireWeasel();
        RegisterSummonDretch();
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
                new NaturalAttackDefinition
                {
                    Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1,
                    BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true,
                    HasDiseaseOnHit = true, DiseaseOnHitType = DiseaseType.FilthFever
                }
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



    private static void RegisterSummonDireBat()
    {
        Register(new NPCDefinition
        {
            Id = "dire_bat",
            Name = "Dire Bat",
            ChallengeRating = "2",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 4,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 17, DEX = 22, CON = 17, WIS = 14, INT = 2, CHA = 6,
            NaturalArmorBonus = 5,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 4, // 20 ft. land, fly 40 ft. (good)
            BaseHitDieHP = 30,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            SpecialAbilities = new List<string> { "Blindsense 40 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.4f, 0.32f, 0.38f, 1f),
            PanelColor = new Color(0.15f, 0.1f, 0.16f, 0.85f),
            NameColor = new Color(0.85f, 0.79f, 0.9f),
            Description = "Dire bat with blindsense 40 ft. echolocation. Wingspan 15 ft., ~200 lbs. MM 3.5e p.62."
        });
    }

    private static void RegisterSummonDireBadger()
    {
        Register(new NPCDefinition
        {
            Id = "dire_badger",
            Name = "Dire Badger",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 14, DEX = 17, CON = 19, WIS = 12, INT = 2, CHA = 10,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 6, // 30 ft., burrow 10 ft.
            BaseHitDieHP = 28,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Rage (+4 Str, +4 Con, −2 AC when damaged)", "Low-light vision", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.5f, 0.45f, 0.43f, 1f),
            PanelColor = new Color(0.2f, 0.17f, 0.16f, 0.85f),
            NameColor = new Color(0.93f, 0.9f, 0.9f),
            Description = "Dire badger with rage when wounded (+4 Str/Con, −2 AC). MM 3.5e p.62."
        });
    }

    private static void RegisterSummonDireWeasel()
    {
        Register(new NPCDefinition
        {
            Id = "dire_weasel",
            Name = "Dire Weasel",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 14, DEX = 19, CON = 10, WIS = 12, INT = 2, CHA = 11,
            NaturalArmorBonus = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8, // 40 ft.
            BaseHitDieHP = 13,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Attach (auto bite damage while latched)", "Blood drain (1d4 Con/round)", "Low-light vision", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.62f, 0.52f, 0.42f, 1f),
            PanelColor = new Color(0.22f, 0.18f, 0.14f, 0.85f),
            NameColor = new Color(0.92f, 0.86f, 0.78f),
            Description = "Dire weasel. Bite +6 (1d6+3), attach, blood drain 1d4 Con/round. Weapon Finesse. MM 3.5e p.65."
        });
    }

    private static void RegisterSummonDretch()
    {
        Register(new NPCDefinition
        {
            Id = "dretch",
            Name = "Dretch",
            ChallengeRating = "2",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            HitDice = 2,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 12, DEX = 10, CON = 14, WIS = 11, INT = 5, CHA = 11,
            NaturalArmorBonus = 5,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 4, // 20 ft.
            BaseHitDieHP = 13,
            // DR 5/cold iron or good
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.ColdIron | DamageBypassTag.Good,
            SpellResistance = 5,
            Immunities = new CreatureImmunities
            {
                immuneToElectricity = true,
                immuneToPoison = true
            },
            DamageImmunities = new List<DamageType> { DamageType.Electricity },
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Acid, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 10 }
            },
            CreatureTags = new List<string> { "Outsider", "Chaotic", "Demon", "Extraplanar", "Evil", "SummonBase" },
            SpecialAbilities = new List<string> { "DR 5/cold iron or good", "SR 5", "Immune: electricity, poison", "Resist: acid 10, cold 10, fire 10", "Spell-like: 1/day scare (DC 12), stinking cloud (DC 13)", "Summon demon (35% chance, 1 dretch)", "Telepathy 100 ft.", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.45f, 0.35f, 0.5f, 1f),
            PanelColor = new Color(0.18f, 0.12f, 0.22f, 0.85f),
            NameColor = new Color(0.85f, 0.75f, 0.92f),
            Description = "Dretch demon. 2 claws +4 (1d6+1), bite +2 (1d4). DR 5/cold iron or good. SR 5. Immune electricity/poison. Resist acid/cold/fire 10. MM 3.5e p.42."
        });
    }

}
