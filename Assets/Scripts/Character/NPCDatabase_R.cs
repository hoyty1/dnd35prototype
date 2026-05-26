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
        RegisterRedSlaad();
        RegisterRustMonster();

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
            AIProfileArchetype = NPCAIProfileArchetype.Swarm,
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
    
    private static void RegisterRedSlaad()
    {
        Register(new NPCDefinition
        {
            Id = "red_slaad",
            Name = "Red Slaad",
            ChallengeRating = "7",
            Level = 7,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.ChaoticNeutral,
            HitDice = 7,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 17, DEX = 14, CON = 17, WIS = 6, INT = 6, CHA = 9,
            NaturalArmorBonus = 5,
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Acid, Amount = 5 },
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 5 },
                new DamageResistanceEntry { Type = DamageType.Electricity, Amount = 5 },
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 5 }
            },
            BaseSpeed = 6,
            BaseHitDieHP = 52,
            BAB = 7,
            HasPounce = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 2, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Outsider", "Chaotic", "Extraplanar", "Darkvision60", "MM35" },
            Feats = new List<string> { "Multiattack", "Power Attack" },
            SpecialAbilities = new List<string> { "Pounce (Ex): full attack on charge", "Stunning Croak (Su): 20 ft., Fort DC 16 or stunned 1 round, 1/hour", "Implant (Ex): claw hit may implant egg (no save)", "Summon Slaad: 40% chance 1 red slaad", "Fast Healing 5", "Resist acid 5, cold 5, electricity 5, fire 5", "Darkvision 60 ft.", "Telepathy 100 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Berserk,
            SpriteColor = new Color(0.7f, 0.25f, 0.2f, 1f),
            PanelColor = new Color(0.28f, 0.06f, 0.04f, 0.85f),
            NameColor = new Color(0.95f, 0.4f, 0.3f),
            Description = "Red Slaad (CR 7). Chaotic toad-like outsider with pounce and egg implantation. MM 3.5e p.228."
        });
    }

    private static void RegisterRustMonster()
    {
        Register(new NPCDefinition
        {
            Id = "rust_monster",
            Name = "Rust Monster",
            ChallengeRating = "3",
            Level = 5,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 5,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 10, DEX = 17, CON = 13, WIS = 13, INT = 2, CHA = 8,
            NaturalArmorBonus = 5,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 27,
            BAB = 3,
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Antennae", DamageDice = 0, DamageCount = 0, Count = 1, BonusDamageSource = DamageBonusSource.None, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 3, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.None, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Aberration", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Track" },
            SpecialAbilities = new List<string> { "Rust (Ex): metal touched by antennae corrodes/destroyed", "Scent: detects metal within 90 ft.", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.7f, 0.45f, 0.25f, 1f),
            PanelColor = new Color(0.25f, 0.15f, 0.06f, 0.85f),
            NameColor = new Color(0.9f, 0.6f, 0.35f),
            Description = "Rust Monster (CR 3). Corrodes metal on contact. MM 3.5e p.216."
        });
    }
}

}
