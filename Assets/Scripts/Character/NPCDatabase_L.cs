using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: L
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_L()
    {
        RegisterLemure();
    }

    private static void RegisterLemure()
    {
        Register(new NPCDefinition
        {
            Id = "lemure",
            Name = "Lemure",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 11, DEX = 10, CON = 12, WIS = 11, INT = 0, CHA = 5,
            BAB = 2,
            NaturalArmorBonus = 4,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 4,
            BaseHitDieHP = 11,
            CreatureTags = new List<string> { "Outsider", "Evil", "Lawful", "MM35", "Devil" },
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Good | DamageBypassTag.Silver,
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Acid, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 }
            },
            DamageImmunities = new List<DamageType> { DamageType.Fire },
            RegenerationAmount = 2,
            RegenerationSuppressedBy = DamageBypassTag.Good | DamageBypassTag.Silver,
            SpecialAbilities = new List<string>
            {
                "DR 5/good or silver",
                "Immunity to fire",
                "Poison immunity",
                "Mind-affecting immunity",
                "Resist acid 10",
                "Resist cold 10",
                "Regeneration 2 (suppressed by good or silver)"
            },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Berserk,
            SpriteColor = new Color(0.58f, 0.24f, 0.24f, 1f),
            PanelColor = new Color(0.22f, 0.08f, 0.08f, 0.85f),
            NameColor = new Color(0.95f, 0.66f, 0.66f),
            Description = "Monster Manual lemure devil. Sluggish fiend with infernal resistances and relentless regeneration."
        });
    }
}
