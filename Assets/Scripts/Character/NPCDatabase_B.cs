using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: B
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_B()
    {
        RegisterBadger();
    }

    private static void RegisterBadger()
    {
        Register(new NPCDefinition
        {
            Id = "badger",
            Name = "Badger",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 8, DEX = 17, CON = 15, WIS = 12, INT = 2, CHA = 6,
            NaturalArmorBonus = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 2, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 3, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 6,
            BaseHitDieHP = 6,
            CreatureTags = new List<string> { "Animal", "MM35", "Burrow" },
            Feats = new List<string> { "Agile", "Weapon Finesse", "Track" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Low-light vision", "Scent", "Rage (as barbarian)", "Burrow 10 ft", "Skills: Balance +5, Escape Artist +9, Listen +3, Spot +3" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.62f, 0.55f, 0.52f, 1f),
            PanelColor = new Color(0.22f, 0.19f, 0.18f, 0.85f),
            NameColor = new Color(0.93f, 0.9f, 0.9f),
            Description = "Monster Manual badger. Tunnel-capable skirmisher with claw/claw/bite routine and rage trait."
        });
    }

}
