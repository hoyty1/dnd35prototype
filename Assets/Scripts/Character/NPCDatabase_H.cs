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

}
