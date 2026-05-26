using System.Collections.Generic;
using UnityEngine;

public static partial class NPCDatabase
{
    private static void RegisterCreatures_X()
    {
        RegisterAverageXorn();
        RegisterMinorXorn();
        RegisterXill();
    }

    private static void RegisterAverageXorn()
    {
        Register(new NPCDefinition
        {
            Id = "average_xorn",
            Name = "Average Xorn",
            ChallengeRating = "6",
            Level = 7,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 7,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 17, DEX = 10, CON = 15, WIS = 11, INT = 10, CHA = 10,
            NaturalArmorBonus = 14,
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Bludgeoning,
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 10 }
            },
            Immunities = new CreatureImmunities { immuneToElectricity = true },
            BaseSpeed = 4, // 20 ft, burrow 20 ft
            BaseHitDieHP = 48,
            BAB = 7,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 4, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 3, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Outsider", "Earth", "Extraplanar", "Tremorsense60", "Darkvision60", "MM35" },
            Feats = new List<string> { "Cleave", "Improved Initiative", "Multiattack", "Power Attack", "Toughness" },
            SpecialAbilities = new List<string> { "Earth Glide (Ex): as minor xorn", "All-Around Vision (Ex): cannot be flanked", "Tremorsense 60 ft.", "DR 5/bludgeoning", "Immune to electricity", "Resist cold 10, fire 10", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.5f, 0.45f, 0.32f, 1f),
            PanelColor = new Color(0.18f, 0.15f, 0.08f, 0.85f),
            NameColor = new Color(0.78f, 0.7f, 0.52f),
            Description = "Average Xorn (CR 6). Medium earth outsider with earth glide and massive natural armor. MM 3.5e p.260."
        });
    }

    private static void RegisterMinorXorn()
    {
        Register(new NPCDefinition
        {
            Id = "minor_xorn",
            Name = "Minor Xorn",
            ChallengeRating = "3",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 3,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 15, DEX = 10, CON = 17, WIS = 11, INT = 10, CHA = 10,
            NaturalArmorBonus = 12,
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Bludgeoning,
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 10 }
            },
            Immunities = new CreatureImmunities { immuneToElectricity = true },
            BaseSpeed = 4, // 20 ft, burrow 20 ft
            BaseHitDieHP = 22,
            BAB = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 3, DamageCount = 1, Count = 3, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Outsider", "Earth", "Extraplanar", "Tremorsense60", "Darkvision60", "MM35" },
            Feats = new List<string> { "Improved Initiative", "Multiattack", "Toughness" },
            SpecialAbilities = new List<string> { "Earth Glide (Ex): burrow through stone, earth, metal as through water", "All-Around Vision (Ex): +4 Spot/Search, cannot be flanked", "Tremorsense 60 ft.", "DR 5/bludgeoning", "Immune to electricity", "Resist cold 10, fire 10", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.55f, 0.48f, 0.35f, 1f),
            PanelColor = new Color(0.2f, 0.16f, 0.1f, 0.85f),
            NameColor = new Color(0.8f, 0.72f, 0.55f),
            Description = "Minor Xorn (CR 3). Small earth outsider with earth glide and high natural armor. MM 3.5e p.260."
        });
    }

    private static void RegisterXill()
    {
        Register(new NPCDefinition
        {
            Id = "xill",
            Name = "Xill",
            ChallengeRating = "6",
            Level = 5,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 5,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 14, DEX = 16, CON = 15, WIS = 12, INT = 12, CHA = 11,
            NaturalArmorBonus = 6,
            SpellResistance = 21,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 32,
            BAB = 5,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Claw",
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 3, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Outsider", "Extraplanar", "Darkvision60", "MM35" },
            Feats = new List<string> { "Improved Initiative", "Multiattack" },
            SpecialAbilities = new List<string> { "Improved Grab", "Implant (Ex): implant egg in paralyzed/helpless foe", "Paralytic Bite (Ex): DC 14 Fort or paralyzed 1d4 hours", "Planewalk (Su): shift Ethereal/Material as standard action", "Multiweapon Fighting: four arms", "SR 21", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("short_sword", EquipSlot.MainHand),
                new EquipmentSlotPair("short_sword", EquipSlot.OffHand)
            },
            BackpackItemIds = new List<string> { "short_sword", "short_sword" },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Grappler,
            SpriteColor = new Color(0.5f, 0.4f, 0.35f, 1f),
            PanelColor = new Color(0.18f, 0.12f, 0.1f, 0.85f),
            NameColor = new Color(0.78f, 0.62f, 0.55f),
            Description = "Xill (CR 6). Four-armed extraplanar raider that implants eggs. MM 3.5e p.259."
        });
    }
}
