using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: R
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_R()
    {
        RegisterRatSwarm();
        RegisterRaven();
    }

    private static void RegisterRatSwarm()
    {
        Register(new NPCDefinition
        {
            Id = "rat_swarm",
            Name = "Rat Swarm",
            ChallengeRating = "2",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 4,
            BaseAttackBonusOverride = 3, // Monster Manual BAB +3
            SizeCategory = SizeCategory.Large, // Swarm occupies 2×2 space in this prototype
            IsTallCreature = false,
            STR = 2, DEX = 15, CON = 10, WIS = 12, INT = 2, CHA = 2,
            NaturalArmorBonus = 0, // AC 14 = 10 +2 size +2 Dex
            BaseSpeed = 3, // 15 ft
            BaseHitDieHP = 18,
            CreatureTags = new List<string> { "Animal", "Swarm", "MM35" },
            HasScent = true,
            IsSwarm = true,
            CanMakeAttacksOfOpportunity = false,
            Immunities = ImmunityPresets.SwarmImmunities(),
            SwarmTraits = new SwarmTraits
            {
                IsSwarm = true,
                SwarmDamage = 6,
                SwarmDamageDice = "1d6",
                SwarmDamageType = DamageType.Piercing,
                DistractionDC = 12,
                HasDisease = true,
                DiseaseType = DiseaseType.FilthFever,
                DiseaseDcModifier = 0 // Filth fever already uses Fort DC 12
            },
            SpecialAbilities = new List<string>
            {
                "Swarm attack (1d6)",
                "Distraction (Fort DC 12)",
                "Disease (filth fever, Fort DC 12)",
                "Low-light vision",
                "Scent",
                "Climb 15 ft",
                "Swarm traits",
                "Alignment: True Neutral"
            },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.42f, 0.34f, 0.28f, 1f),
            PanelColor = new Color(0.16f, 0.12f, 0.1f, 0.85f),
            NameColor = new Color(0.9f, 0.84f, 0.78f),
            Description = "Monster Manual rat swarm. Tiny animal swarm with distraction, filth fever transmission, and scent tracking."
        });
    }

    private static void RegisterRaven()
    {
        Register(new NPCDefinition
        {
            Id = "raven",
            Name = "Raven",
            ChallengeRating = "1/6",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 1,
            SizeCategory = SizeCategory.Tiny,
            IsTallCreature = false,
            STR = 1, DEX = 15, CON = 10, WIS = 14, INT = 2, CHA = 6,
            NaturalArmorBonus = 0,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claws", DamageDice = 2, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 2,
            BaseHitDieHP = 1,
            CreatureTags = new List<string> { "Animal", "MM35", "Fly" },
            Feats = new List<string> { "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Low-light vision", "Fly 40 ft (average)", "Skills: Listen +3, Spot +5" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.24f, 0.24f, 0.28f, 1f),
            PanelColor = new Color(0.1f, 0.1f, 0.14f, 0.85f),
            NameColor = new Color(0.84f, 0.84f, 0.9f),
            Description = "Monster Manual raven. Tiny aerial scavenger with agile claws and perceptive vision."
        });
    }

}
