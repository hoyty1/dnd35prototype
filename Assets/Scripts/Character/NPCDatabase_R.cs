using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: R
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_R()
    {
        RegisterRaven();
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
