using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: Y
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_Y()
    {
        RegisterYethHound();
    }

    /// <summary>
    /// Yeth Hound (CR 3) — Medium outsider (evil, extraplanar).
    /// MM 3.5e p.260. 3 HD, fly 60 ft (good), bay (fear), trip.
    /// Damage reduction 5/silver.
    /// </summary>
    private static void RegisterYethHound()
    {
        Register(new NPCDefinition
        {
            Id = "yeth_hound",
            Name = "Yeth Hound",
            ChallengeRating = "3",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 17, DEX = 15, CON = 17, WIS = 12, INT = 6, CHA = 10,
            BAB = 3,
            NaturalArmorBonus = 4,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8, // 40 ft., fly 60 ft. (good)
            BaseHitDieHP = 19, // 3d8+6
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Silver,
            HasTripAttack = true,
            TripAttackCheckBonus = 3, // Str +3 trip modifier
            HasScent = true,
            CreatureTags = new List<string> { "Outsider", "Evil", "Extraplanar", "MM35", "Fly" },
            Feats = new List<string> { "Improved Initiative", "Track" },
            SpecialAbilities = new List<string>
            {
                "Bay (Will DC 11 or panicked for 2d4 rounds; 300-ft. spread; sonic mind-affecting fear)",
                "Trip (free trip attempt on bite hit, Str +3)",
                "DR 5/silver",
                "Fly 60 ft. (good)",
                "Darkvision 60 ft.",
                "Scent",
                "Sinister Bite (only harmed by silver or magic weapons at night)",
                "Skills: Listen +7, Spot +7, Survival +7 (+11 tracking by scent)",
                "Alignment: Neutral Evil"
            },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.35f, 0.32f, 0.42f, 1f),
            PanelColor = new Color(0.12f, 0.1f, 0.18f, 0.85f),
            NameColor = new Color(0.78f, 0.75f, 0.9f),
            Description = "Monster Manual yeth hound (CR 3). Bite +6 (1d6+4), trip +3, bay (fear), fly 60 ft. (good), DR 5/silver. Outsider hunting hound. MM 3.5e p.260."
        });
    }
}
