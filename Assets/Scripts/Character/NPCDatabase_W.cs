using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: W
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_W()
    {
        RegisterSummonWolverine();
    }

    private static void RegisterSummonWolverine()
    {
        Register(new NPCDefinition
        {
            Id = "wolverine",
            Name = "Wolverine",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 14, DEX = 15, CON = 19, WIS = 12, INT = 2, CHA = 10,
            NaturalArmorBonus = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 6, // 30 ft., burrow 10 ft., climb 10 ft.
            BaseHitDieHP = 28,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Rage (+4 Str, +4 Con, −2 AC when damaged)", "Low-light vision", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.48f, 0.38f, 0.28f, 1f),
            PanelColor = new Color(0.2f, 0.15f, 0.1f, 0.85f),
            NameColor = new Color(0.9f, 0.82f, 0.7f),
            Description = "Wolverine with rage when wounded (+4 Str/Con, −2 AC). +8 racial Climb. MM 3.5e p.283."
        });
    }
}
