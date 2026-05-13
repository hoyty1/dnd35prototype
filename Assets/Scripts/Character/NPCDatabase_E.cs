using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: E
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_E()
    {
        RegisterEagle();
    
        RegisterSummonSmallAirElemental();
        RegisterSummonSmallFireElemental();
        RegisterSummonSmallEarthElemental();
        RegisterSummonSmallWaterElemental();
    }

    private static void RegisterEagle()
    {
        Register(new NPCDefinition
        {
            Id = "eagle",
            Name = "Eagle",
            ChallengeRating = "1/2",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 10, DEX = 15, CON = 12, WIS = 14, INT = 2, CHA = 6,
            BAB = 2,
            NaturalArmorBonus = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Talons", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 2,
            BaseHitDieHP = 5,
            CreatureTags = new List<string> { "Animal", "MM35", "Fly", "SummonBase" },
            Feats = new List<string> { "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Low-light vision", "Fly 80 ft (average)", "Size bonus +1 AC/+1 attack" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.78f, 0.73f, 0.64f, 1f),
            PanelColor = new Color(0.2f, 0.17f, 0.1f, 0.85f),
            NameColor = new Color(0.97f, 0.91f, 0.77f),
            Description = "Monster Manual eagle. Small raptor with swift flight and a sharp talon strike."
        });
    }


    private static void RegisterSummonSmallAirElemental()
    {
        Register(new NPCDefinition
        {
            Id = "small_air_elemental",
            Name = "Small Air Elemental",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Elemental",
            HitDice = 2,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 10, DEX = 17, CON = 10, WIS = 11, INT = 4, CHA = 11,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 20, // Fly 100 ft. (perfect)
            BaseHitDieHP = 9,
            Immunities = new CreatureImmunities
            {
                immuneToPoison = true,
                immuneToCriticalHits = true,
                immuneToSneakAttack = true
            },
            CreatureTags = new List<string> { "Elemental", "Air", "Extraplanar", "SummonBase" },
            SpecialAbilities = new List<string> { "Elemental traits", "Air mastery", "Whirlwind (DC 11)", "Fly 100 ft. (perfect)", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.68f, 0.86f, 1f, 1f),
            PanelColor = new Color(0.14f, 0.19f, 0.26f, 0.85f),
            NameColor = new Color(0.85f, 0.95f, 1f),
            Description = "Small Air Elemental. Immune to poison, sleep, paralysis, stunning. Not subject to critical hits or flanking. MM 3.5e p.95."
        });
    }

    private static void RegisterSummonSmallFireElemental()
    {
        Register(new NPCDefinition
        {
            Id = "small_fire_elemental",
            Name = "Small Fire Elemental",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Elemental",
            HitDice = 2,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 10, DEX = 13, CON = 10, WIS = 11, INT = 4, CHA = 11,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                // Slam 1d4 plus 1d4 fire (fire damage represented as special ability)
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 10, // 50 ft.
            BaseHitDieHP = 9,
            Immunities = new CreatureImmunities
            {
                immuneToPoison = true,
                immuneToFire = true,
                immuneToCriticalHits = true,
                immuneToSneakAttack = true
            },
            DamageImmunities = new List<DamageType> { DamageType.Fire },
            CreatureTags = new List<string> { "Elemental", "Fire", "Extraplanar", "SummonBase" },
            SpecialAbilities = new List<string> { "Elemental traits", "Burn (DC 11, 1d4 fire)", "Immunity to fire", "Vulnerability to cold (+50%)", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(1f, 0.55f, 0.22f, 1f),
            PanelColor = new Color(0.28f, 0.1f, 0.08f, 0.85f),
            NameColor = new Color(1f, 0.86f, 0.72f),
            Description = "Small Fire Elemental. Slam deals +1d4 fire (Burn). Immune to fire, vulnerable to cold. MM 3.5e p.98."
        });
    }

    private static void RegisterSummonSmallEarthElemental()
    {
        Register(new NPCDefinition
        {
            Id = "small_earth_elemental",
            Name = "Small Earth Elemental",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Elemental",
            HitDice = 2,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 17, DEX = 8, CON = 13, WIS = 11, INT = 4, CHA = 11,
            NaturalArmorBonus = 7,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 4, // 20 ft.
            BaseHitDieHP = 11,
            Immunities = new CreatureImmunities
            {
                immuneToPoison = true,
                immuneToCriticalHits = true,
                immuneToSneakAttack = true
            },
            CreatureTags = new List<string> { "Elemental", "Earth", "Extraplanar", "SummonBase" },
            SpecialAbilities = new List<string> { "Elemental traits", "Earth mastery (+1 atk/dmg when grounded)", "Push (bull rush, no AoO)", "Earth glide", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.58f, 0.48f, 0.32f, 1f),
            PanelColor = new Color(0.24f, 0.18f, 0.1f, 0.85f),
            NameColor = new Color(0.92f, 0.84f, 0.68f),
            Description = "Small Earth Elemental. Slam +5 (1d6+4). Earth mastery, push, earth glide. MM 3.5e p.97."
        });
    }

    private static void RegisterSummonSmallWaterElemental()
    {
        Register(new NPCDefinition
        {
            Id = "small_water_elemental",
            Name = "Small Water Elemental",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Elemental",
            HitDice = 2,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 14, DEX = 10, CON = 13, WIS = 11, INT = 4, CHA = 11,
            NaturalArmorBonus = 6,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 4, // 20 ft., swim 90 ft.
            BaseHitDieHP = 11,
            Immunities = new CreatureImmunities
            {
                immuneToPoison = true,
                immuneToCriticalHits = true,
                immuneToSneakAttack = true
            },
            CreatureTags = new List<string> { "Elemental", "Water", "Extraplanar", "SummonBase" },
            SpecialAbilities = new List<string> { "Elemental traits", "Water mastery (+1 atk/dmg in water)", "Drench (extinguish fires)", "Vortex (DC 13, 1d4 dmg)", "Swim 90 ft.", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.32f, 0.55f, 0.78f, 1f),
            PanelColor = new Color(0.1f, 0.2f, 0.32f, 0.85f),
            NameColor = new Color(0.72f, 0.88f, 1f),
            Description = "Small Water Elemental. Slam +4 (1d6+3). Water mastery, drench, vortex. MM 3.5e p.100."
        });
    }

}
