using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: M
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_M()
    {
        RegisterMonkey();
    }

    private static void RegisterMonkey()
    {
        Register(new NPCDefinition
        {
            Id = "monkey",
            Name = "Monkey",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 1,
            SizeCategory = SizeCategory.Tiny,
            IsTallCreature = false,
            STR = 3, DEX = 15, CON = 10, WIS = 12, INT = 2, CHA = 5,
            NaturalArmorBonus = 0,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 3, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 6,
            BaseHitDieHP = 4,
            CreatureTags = new List<string> { "Animal", "MM35", "Climb" },
            Feats = new List<string> { "Agile", "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Low-light vision", "Climb 30 ft", "Skills: Balance +12, Climb +10, Escape Artist +4, Hide +10, Listen +3, Spot +3" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.72f, 0.62f, 0.48f, 1f),
            PanelColor = new Color(0.22f, 0.16f, 0.1f, 0.85f),
            NameColor = new Color(0.95f, 0.88f, 0.76f),
            Description = "Monster Manual monkey. Tiny climber with agile bite and strong movement utility."
        });
    }
}
