using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: A
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_A()
    {
        RegisterSummonApe();
    }

    private static void RegisterSummonApe()
    {
        Register(new NPCDefinition
        {
            Id = "ape",
            Name = "Ape",
            ChallengeRating = "2",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 4,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 21, DEX = 15, CON = 14, WIS = 12, INT = 2, CHA = 7,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 6, // 30 ft., climb 30 ft.
            BaseHitDieHP = 29,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Low-light vision", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.45f, 0.32f, 0.25f, 1f),
            PanelColor = new Color(0.18f, 0.12f, 0.09f, 0.85f),
            NameColor = new Color(0.95f, 0.84f, 0.75f),
            Description = "Ape. 2 claws +7 (1d6+5), bite +2 (1d6+2). +8 racial Climb. 10 ft. reach. MM 3.5e p.268."
        });
    }
}
