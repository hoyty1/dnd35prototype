using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: A
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_A()
    {
        RegisterAllip();
        RegisterSummonApe();
    }

    /// <summary>
    /// Allip (CR 3) — MM 3.5e p.10. Incorporeal undead with Wisdom drain and babble aura.
    /// 4d12 HP (26), incorporeal touch +3 (1d4 Wis drain). Babble: DC 15 Will or hypnotized (fascinated).
    /// </summary>
    private static void RegisterAllip()
    {
        Register(new NPCDefinition
        {
            Id = "allip",
            Name = "Allip",
            ChallengeRating = "3",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Undead",
            HitDice = 4,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 0, DEX = 12, CON = 0, WIS = 11, INT = 11, CHA = 18,
            NaturalArmorBonus = 0,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Incorporeal Touch", DamageDice = 4, DamageCount = 1, Count = 1,
                    BonusDamageSource = DamageBonusSource.None, Range = 1, IsPrimary = true,
                    AbilityDrainType = AbilityType.WIS, AbilityDrainAmount = 1
                }
            },
            BaseSpeed = 6, // Fly 30 ft (perfect)
            BaseHitDieHP = 26,
            IsIncorporeal = true,
            IsMindless = false,
            AuraAbility = new AuraAbilityDefinition
            {
                Name = "Babble",
                SaveDC = 16,
                IsWillSave = true,
                RangeFeet = 60,
                Effect = AuraEffectType.Fascinated,
                DurationRounds = 3
            },
            CreatureTags = new List<string> { "Undead", "Incorporeal", "MM35" },
            Feats = new List<string> { "Improved Initiative", "Lightning Reflexes" },
            SpecialAbilities = new List<string> { "Incorporeal", "Wisdom drain (1d4)", "Babble (DC 16 Will, hypnotism 2d4 rounds, 60 ft.)", "Madness (+4 CHA for save DC, -6 WIS)", "Darkvision 60 ft.", "Undead traits", "Fly 30 ft. (perfect)" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.4f, 0.35f, 0.55f, 0.5f),
            PanelColor = new Color(0.12f, 0.1f, 0.22f, 0.85f),
            NameColor = new Color(0.7f, 0.6f, 0.9f),
            Description = "Allip (CR 3). Incorporeal undead. Touch drains 1d4 WIS. Babble aura fascinates (DC 16 Will). MM 3.5e p.10."
        });
    }

    private static void RegisterSummonApe()
    {
        Register(new NPCDefinition
        {
            Id = "ape",
            Name = "Ape",
            ChallengeRating = "2",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 4,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 21, DEX = 15, CON = 14, WIS = 12, INT = 2, CHA = 7,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 6, // 30 ft., climb 30 ft.
            BaseHitDieHP = 29,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Low-light vision", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.45f, 0.32f, 0.25f, 1f),
            PanelColor = new Color(0.18f, 0.12f, 0.09f, 0.85f),
            NameColor = new Color(0.95f, 0.84f, 0.75f),
            Description = "Ape. 2 claws +7 (1d6+5), bite +2 (1d6+2). +8 racial Climb. 10 ft. reach. MM 3.5e p.268."
        });
    }
}
