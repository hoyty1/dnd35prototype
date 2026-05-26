using System.Collections.Generic;
using UnityEngine;

public static partial class NPCDatabase
{
    private static void RegisterCreatures_K()
    {
        RegisterKrenshar();
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
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.6f, 0.5f, 0.4f, 1f),
            PanelColor = new Color(0.22f, 0.18f, 0.12f, 0.85f),
            NameColor = new Color(0.85f, 0.75f, 0.6f),
            Description = "Krenshar (CR 1). Feline beast that retracts face skin to frighten prey. MM 3.5e p.163."
        });
    }
}
