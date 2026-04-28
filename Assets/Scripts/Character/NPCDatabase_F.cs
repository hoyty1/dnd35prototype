using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: F
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_F()
    {
        RegisterGiantFireBeetle();
    }

    private static void RegisterGiantFireBeetle()
    {
        Register(new NPCDefinition
        {
            Id = "giant_fire_beetle",
            Name = "Giant Fire Beetle",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 10, DEX = 11, CON = 11, WIS = 10, INT = 1, CHA = 7,
            NaturalArmorBonus = 5,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 6,
            BaseHitDieHP = 4,
            CreatureTags = new List<string> { "Vermin", "MM35" },
            SpecialAbilities = new List<string> { "Light Glands (10-ft radius red glow)", "Darkvision 60 ft", "Vermin traits", "Mindless" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.75f, 0.38f, 0.2f, 1f),
            PanelColor = new Color(0.25f, 0.1f, 0.08f, 0.85f),
            NameColor = new Color(1f, 0.78f, 0.64f),
            Description = "Monster Manual giant fire beetle. Small vermin with hard shell, strong bite, and bioluminescent glands."
        });
    }

}
