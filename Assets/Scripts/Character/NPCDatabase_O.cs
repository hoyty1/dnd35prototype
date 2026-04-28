using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: O
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_O()
    {
        RegisterOwl();
    }

    private static void RegisterOwl()
    {
        Register(new NPCDefinition
        {
            Id = "owl",
            Name = "Owl",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 1,
            SizeCategory = SizeCategory.Tiny,
            IsTallCreature = false,
            STR = 4, DEX = 17, CON = 10, WIS = 14, INT = 2, CHA = 4,
            NaturalArmorBonus = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Talons", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 2,
            BaseHitDieHP = 4,
            CreatureTags = new List<string> { "Animal", "MM35", "Fly" },
            Feats = new List<string> { "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Low-light vision", "Fly 40 ft (average)", "Skills: Listen +14, Move Silently +17, Spot +6" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.76f, 0.7f, 0.6f, 1f),
            PanelColor = new Color(0.2f, 0.16f, 0.12f, 0.85f),
            NameColor = new Color(0.94f, 0.9f, 0.84f),
            Description = "Monster Manual owl. Tiny aerial hunter with keen senses and low-light vision."
        });
    }

}
