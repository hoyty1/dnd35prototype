using System.Collections.Generic;
using UnityEngine;

public static partial class NPCDatabase
{
    private static void RegisterCreatures_U()
    {
        RegisterUmberHulk();
    }

    private static void RegisterUmberHulk()
    {
        Register(new NPCDefinition
        {
            Id = "umber_hulk",
            Name = "Umber Hulk",
            ChallengeRating = "7",
            Level = 8,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 8,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 23, DEX = 13, CON = 19, WIS = 11, INT = 11, CHA = 13,
            NaturalArmorBonus = 8,
            BaseSpeed = 4, // 20 ft, burrow 20 ft
            BaseHitDieHP = 71,
            BAB = 6,
            AuraAbility = new AuraAbilityDefinition
            {
                Name = "Confusing Gaze",
                SaveDC = 15,
                IsWillSave = true,
                RangeFeet = 30,
                Effect = AuraEffectType.Confused,
                DurationRounds = 1
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 2, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 2, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Aberration", "Tremorsense60", "Darkvision60", "MM35" },
            Feats = new List<string> { "Great Fortitude", "Toughness" },
            SpecialAbilities = new List<string> { "Confusing Gaze (Su): 30 ft., Will DC 15 or confused 1 round", "Tremorsense 60 ft.", "Burrow 20 ft.", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.45f, 0.35f, 0.25f, 1f),
            PanelColor = new Color(0.15f, 0.1f, 0.05f, 0.85f),
            NameColor = new Color(0.7f, 0.55f, 0.38f),
            Description = "Umber Hulk (CR 7). Tunneling aberration with confusing gaze. MM 3.5e p.248."
        });
    }
}
