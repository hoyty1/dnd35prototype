using System.Collections.Generic;
using UnityEngine;

public static partial class NPCDatabase
{
    private static void RegisterCreatures_Q()
    {
        RegisterQuasit();
    }

    private static void RegisterQuasit()
    {
        Register(new NPCDefinition
        {
            Id = "quasit",
            Name = "Quasit",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 3,
            SizeCategory = SizeCategory.Tiny,
            IsTallCreature = true,
            STR = 8, DEX = 17, CON = 10, WIS = 12, INT = 10, CHA = 10,
            NaturalArmorBonus = 3,
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.ColdIron | DamageBypassTag.Good,
            BaseSpeed = 4, // 20 ft, fly 50 ft (perfect)
            BaseHitDieHP = 13,
            BAB = 3,
            Immunities = new CreatureImmunities { immuneToPoison = true },
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 10 }
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 3, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Outsider", "Chaotic", "Evil", "Extraplanar", "Fly50", "Darkvision60", "MM35" },
            Feats = new List<string> { "Improved Initiative", "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Poison (Ex): claw, DC 13 Fort, 1d4 Dex/1d4 Dex", "Alternate Form (Su): bat, Small centipede, toad, or wolf", "Invisibility (Su): at will, self only", "Cause Fear (Sp): at will, 30 ft., DC 11 Will", "Detect Good/Magic (Sp): at will", "DR 5/cold iron or good", "Immune to poison", "Resist fire 10", "Fast healing 2", "Darkvision 60 ft." },
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.45f, 0.35f, 0.5f, 1f),
            PanelColor = new Color(0.15f, 0.1f, 0.2f, 0.85f),
            NameColor = new Color(0.7f, 0.55f, 0.78f),
            Description = "Quasit (CR 2). Tiny demon familiar with poison and invisibility. MM 3.5e p.46."
        });
    }
}
