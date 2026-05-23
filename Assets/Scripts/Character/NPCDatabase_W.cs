using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: W
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_W()
    {
        RegisterWraith();
        RegisterSummonWolverine();
    }

    /// <summary>
    /// Wraith (CR 5) — MM 3.5e p.258. Incorporeal undead with Con drain touch.
    /// 5d12 HP (32), incorporeal touch +5 (1d4 + 1d6 Con drain). Unnatural aura, create spawn.
    /// </summary>
    private static void RegisterWraith()
    {
        Register(new NPCDefinition
        {
            Id = "wraith",
            Name = "Wraith",
            ChallengeRating = "5",
            Level = 5,
            CharacterClass = "Warrior",
            CreatureType = "Undead",
            HitDice = 5,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 0, DEX = 16, CON = 0, WIS = 14, INT = 14, CHA = 15,
            NaturalArmorBonus = 0,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Incorporeal Touch", DamageDice = 4, DamageCount = 1, Count = 1,
                    BonusDamageSource = DamageBonusSource.None, Range = 1, IsPrimary = true,
                    AbilityDrainType = AbilityType.CON, AbilityDrainAmount = 1,
                    EnergyDrainOnHit = 1, EnergyDrainRemovalDC = 14
                }
            },
            BaseSpeed = 6, // Fly 60 ft (good)
            BaseHitDieHP = 32,
            IsIncorporeal = true,
            CreatureTags = new List<string> { "Undead", "Incorporeal", "MM35" },
            Feats = new List<string> { "Alertness", "Blind-Fight", "Combat Reflexes", "Improved Initiative", "Improved Natural Attack" },
            SpecialAbilities = new List<string> { "Incorporeal", "Con drain (1d6)", "Energy drain (1 negative level)", "Create spawn", "Unnatural aura (60 ft.)", "Darkvision 60 ft.", "Daylight powerlessness", "Fly 60 ft. (good)" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.2f, 0.2f, 0.3f, 0.5f),
            PanelColor = new Color(0.08f, 0.08f, 0.15f, 0.85f),
            NameColor = new Color(0.5f, 0.5f, 0.75f),
            Description = "Wraith (CR 5). Incorporeal undead. Touch drains 1d6 CON + 1 negative level. 50% miss chance. MM 3.5e p.258."
        });
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
