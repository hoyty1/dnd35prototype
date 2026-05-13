using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: H
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_H()
    {
        RegisterHawk();
    
        RegisterSummonHellHound();
        RegisterSummonHippogriff();
        RegisterSummonHugeMonstruousCentipede();
    }

    private static void RegisterHawk()
    {
        Register(new NPCDefinition
        {
            Id = "hawk",
            Name = "Hawk",
            ChallengeRating = "1/3",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 1,
            SizeCategory = SizeCategory.Tiny,
            IsTallCreature = false,
            STR = 4, DEX = 17, CON = 10, WIS = 14, INT = 2, CHA = 6,
            NaturalArmorBonus = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Talons", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 2,
            BaseHitDieHP = 4,
            CreatureTags = new List<string> { "Animal", "MM35", "Fly" },
            Feats = new List<string> { "Alertness", "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Low-light vision", "Fly 60 ft (average)", "Skills: Listen +4, Spot +16" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.62f, 0.52f, 0.38f, 1f),
            PanelColor = new Color(0.2f, 0.14f, 0.1f, 0.85f),
            NameColor = new Color(0.95f, 0.86f, 0.72f),
            Description = "Monster Manual hawk. Tiny raptor with high-accuracy talons and exceptional spotting capability."
        });
    }



    private static void RegisterSummonHellHound()
    {
        Register(new NPCDefinition
        {
            Id = "hell_hound",
            Name = "Hell Hound",
            ChallengeRating = "3",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            HitDice = 4,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 13, DEX = 13, CON = 13, WIS = 10, INT = 6, CHA = 6,
            NaturalArmorBonus = 5,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                // Bite 1d8+1 plus 1d6 fire (fire damage is special ability)
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8, // 40 ft.
            BaseHitDieHP = 22,
            Immunities = new CreatureImmunities
            {
                immuneToFire = true
            },
            DamageImmunities = new List<DamageType> { DamageType.Fire },
            CreatureTags = new List<string> { "Outsider", "Evil", "Extraplanar", "Fire", "Lawful", "SummonBase" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Breath weapon (10-ft. cone, 2d6 fire, Ref DC 13 half, 1/2d4 rds)", "Fiery bite (+1d6 fire)", "Immunity to fire", "Vulnerability to cold (+50%)", "Darkvision 60 ft.", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.82f, 0.28f, 0.15f, 1f),
            PanelColor = new Color(0.32f, 0.08f, 0.05f, 0.85f),
            NameColor = new Color(1f, 0.65f, 0.45f),
            Description = "Hell hound. Bite +5 (1d8+1 + 1d6 fire). Breath weapon 2d6 fire cone. Immune to fire, vulnerable to cold. MM 3.5e p.151."
        });
    }

    private static void RegisterSummonHippogriff()
    {
        Register(new NPCDefinition
        {
            Id = "hippogriff",
            Name = "Hippogriff",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            HitDice = 3,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 18, DEX = 15, CON = 16, WIS = 13, INT = 2, CHA = 8,
            NaturalArmorBonus = 4,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 10, // 50 ft., fly 100 ft. (average)
            BaseHitDieHP = 25,
            CreatureTags = new List<string> { "Magical Beast", "SummonBase" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Darkvision 60 ft.", "Low-light vision", "Scent", "Fly 100 ft. (average)" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.72f, 0.62f, 0.48f, 1f),
            PanelColor = new Color(0.26f, 0.2f, 0.14f, 0.85f),
            NameColor = new Color(0.94f, 0.88f, 0.78f),
            Description = "Hippogriff. 2 claws +6 (1d4+4), bite +1 (1d8+2). Fly 100 ft. (average). +4 Spot in daylight. MM 3.5e p.152."
        });
    }

    private static void RegisterSummonHugeMonstruousCentipede()
    {
        Register(new NPCDefinition
        {
            Id = "huge_monstrous_centipede",
            Name = "Huge Monstrous Centipede",
            ChallengeRating = "2",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = 6,
            SizeCategory = SizeCategory.Huge,
            IsTallCreature = false,
            STR = 17, DEX = 15, CON = 12, WIS = 10, INT = CharacterStats.NO_SCORE, CHA = 2,
            NaturalArmorBonus = 6,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true, PoisonOnHitId = "huge_centipede_poison" }
            },
            BaseSpeed = 8, // 40 ft., climb 40 ft.
            BaseHitDieHP = 33,
            IsMindless = true,
            CreatureTags = new List<string> { "Vermin", "SummonBase" },
            Immunities = new CreatureImmunities { immuneToMindAffecting = true },
            SpecialAbilities = new List<string> { "Poison (Fort DC 14, 1d6 Dex/1d6 Dex)", "Darkvision 60 ft.", "Vermin traits (mindless)" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.45f, 0.28f, 0.22f, 1f),
            PanelColor = new Color(0.2f, 0.1f, 0.08f, 0.85f),
            NameColor = new Color(0.9f, 0.72f, 0.65f),
            Description = "Huge monstrous centipede. Bite +5 (2d6+4 + poison Fort DC 14, 1d6 Dex). 15 ft. space, 10 ft. reach. MM 3.5e p.286."
        });
    }

}
