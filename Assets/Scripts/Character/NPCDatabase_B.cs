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
        RegisterGiantBee();
        RegisterGiantBombardierBeetle();
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

    private static void RegisterGiantBee()
    {
        Register(new NPCDefinition
        {
            Id = "giant_bee",
            Name = "Giant Bee",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 11, DEX = 11, CON = 14, WIS = 10, INT = 1, CHA = 9,
            BAB = 2,
            NaturalArmorBonus = 4,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Sting",
                    DamageDice = 4,
                    DamageCount = 1,
                    Count = 1,
                    BonusDamageSource = DamageBonusSource.StrengthOneAndHalf,
                    Range = 1,
                    IsPrimary = true,
                    PoisonOnHitId = "giant_bee_poison"
                }
            },
            BaseSpeed = 4,
            BaseHitDieHP = 19,
            CreatureTags = new List<string> { "Vermin", "MM35", "Fly" },
            Feats = new List<string> { "Weapon Finesse" },
            SpecialAbilities = new List<string>
            {
                "Poison (Fort DC 11; initial 1d4 Dex; secondary 1d4 Dex)",
                "Fly 80 ft (good)",
                "Darkvision 60 ft",
                "Vermin traits",
                "Mindless"
            },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.95f, 0.78f, 0.2f, 1f),
            PanelColor = new Color(0.24f, 0.18f, 0.06f, 0.85f),
            NameColor = new Color(1f, 0.92f, 0.62f),
            Description = "Monster Manual giant bee. Flying vermin with a venomous sting that inflicts Dexterity damage."
        });
    }

    private static void RegisterGiantBombardierBeetle()
    {
        Register(new NPCDefinition
        {
            Id = "giant_bombardier_beetle",
            Name = "Giant Bombardier Beetle",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 13, DEX = 10, CON = 14, WIS = 10, INT = 1, CHA = 9,
            BAB = 2,
            NaturalArmorBonus = 6,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 6,
            BaseHitDieHP = 13,
            CreatureTags = new List<string> { "Vermin", "MM35" },
            SpecialAbilities = new List<string>
            {
                "Acid Spray (10-ft cone, 6d4 acid, Reflex DC 12 half, usable once every 1d4 rounds)",
                "Darkvision 60 ft",
                "Vermin traits",
                "Mindless"
            },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.48f, 0.28f, 0.16f, 1f),
            PanelColor = new Color(0.16f, 0.1f, 0.06f, 0.85f),
            NameColor = new Color(0.92f, 0.8f, 0.62f),
            Description = "Monster Manual giant bombardier beetle. Armored vermin that sprays boiling acid in a short cone."
        });
    }

}
