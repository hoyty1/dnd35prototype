using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

public static partial class NPCDatabase
{
    private static void RegisterCreatures_K()
    {
        RegisterKrenshar();
        RegisterKoboldWarrior();
    }

    private static void RegisterKrenshar()
    {
        Register(new NPCDefinition
        {
            Id = "krenshar",
            Name = "Krenshar",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 13, DEX = 15, CON = 11, WIS = 12, INT = 6, CHA = 13,
            NaturalArmorBonus = 3,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 11,
            BAB = 2,
            HasScent = true,
            AuraAbility = new AuraAbilityDefinition
            {
                Name = "Scare",
                SaveDC = 12,
                IsWillSave = true,
                RangeFeet = 30,
                Effect = AuraEffectType.Frightened,
                DurationRounds = 4
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Magical Beast", "Darkvision60", "MM35" },
            Feats = new List<string> { "Multiattack", "Track" },
            SpecialAbilities = new List<string> { "Scare (Ex/Sp): retract face skin, Will DC 12 or panicked 1d4 rounds (1+ HD), shaken if 6+ HD", "Scent", "Darkvision 60 ft.", "Low-light vision" },
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.6f, 0.5f, 0.4f, 1f),
            PanelColor = new Color(0.22f, 0.18f, 0.12f, 0.85f),
            NameColor = new Color(0.85f, 0.75f, 0.6f),
            Description = "Krenshar (CR 1). Feline beast that retracts face skin to frighten prey. MM 3.5e p.163."
        });
    }

    /// <summary>
    /// Kobold Warrior (CR 1/4) — Small humanoid (reptilian), Warrior 1.
    /// MM 3.5e p.161. Light sensitivity, trap-affinity.
    /// </summary>
    private static void RegisterKoboldWarrior()
    {
        Register(new NPCDefinition
        {
            Id = "kobold_warrior",
            Name = "Kobold Warrior",
            ChallengeRating = "1/4",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 9, DEX = 13, CON = 10, WIS = 9, INT = 10, CHA = 8,
            NaturalArmorBonus = 1,
            BaseSpeed = 6,
            BaseHitDieHP = 4,
            BAB = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Humanoid", "Reptilian", "MM35" },
            Feats = new List<string> { "Alertness" },
            SpecialAbilities = new List<string> { "Darkvision 60 ft.", "Light Sensitivity: dazzled in bright sunlight", "Natural Armor +1" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.LEATHER_ARMOR, EquipSlot.Armor),
                new EquipmentSlotPair(ItemIDs.SPEAR, EquipSlot.RightHand)
            },
            BackpackItemIds = new List<string> { ItemIDs.SLING },
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.55f, 0.45f, 0.35f, 1f),
            PanelColor = new Color(0.2f, 0.15f, 0.1f, 0.85f),
            NameColor = new Color(0.8f, 0.68f, 0.52f),
            Description = "Kobold Warrior (CR 1/4). Small reptilian humanoid with light sensitivity. MM 3.5e p.161."
        });
    }
}
