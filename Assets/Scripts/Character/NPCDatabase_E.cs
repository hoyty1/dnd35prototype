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
}
