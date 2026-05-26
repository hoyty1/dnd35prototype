using System.Collections.Generic;
using UnityEngine;

public static partial class NPCDatabase
{
    private static void RegisterCreatures_I()
    {
        RegisterImp();
        RegisterInvisibleStalker();
    }

    private static void RegisterImp()
    {
        Register(new NPCDefinition
        {
            Id = "imp",
            Name = "Imp",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 3,
            SizeCategory = SizeCategory.Tiny,
            IsTallCreature = true,
            STR = 10, DEX = 17, CON = 10, WIS = 12, INT = 10, CHA = 14,
            NaturalArmorBonus = 5,
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Good | DamageBypassTag.Silver,
            BaseSpeed = 4, // 20 ft, fly 50 ft (perfect)
            BaseHitDieHP = 13,
            BAB = 3,
            Immunities = new CreatureImmunities { immuneToPoison = true },
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 5 }
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Sting", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.None, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Outsider", "Evil", "Lawful", "Extraplanar", "Fly50", "Darkvision60", "MM35" },
            Feats = new List<string> { "Dodge", "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Poison (Ex): sting, DC 13 Fort, 1d4 Dex/2d4 Dex", "Alternate Form (Su): boar, giant spider, rat, or raven", "Invisibility (Su): at will, self only", "Suggestion (Sp): 1/day, DC 15", "Detect Good/Magic (Sp): at will", "DR 5/good or silver", "Immune to poison", "Resist fire 5", "Fast healing 2", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.5f, 0.3f, 0.3f, 1f),
            PanelColor = new Color(0.2f, 0.08f, 0.08f, 0.85f),
            NameColor = new Color(0.78f, 0.45f, 0.45f),
            Description = "Imp (CR 2). Tiny devil familiar with poison sting and invisibility. MM 3.5e p.56."
        });
    }

    private static void RegisterInvisibleStalker()
    {
        Register(new NPCDefinition
        {
            Id = "invisible_stalker",
            Name = "Invisible Stalker",
            ChallengeRating = "7",
            Level = 8,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 8,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 18, DEX = 19, CON = 14, WIS = 15, INT = 14, CHA = 11,
            NaturalArmorBonus = 4,
            BaseSpeed = 6, // 30 ft, fly 30 ft (perfect)
            BaseHitDieHP = 52,
            BAB = 8,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 6, DamageCount = 2, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Outsider", "Air", "Extraplanar", "NaturalInvisibility", "Fly30", "Darkvision60", "MM35" },
            Feats = new List<string> { "Combat Reflexes", "Improved Initiative", "Weapon Focus (slam)" },
            SpecialAbilities = new List<string> { "Natural Invisibility (Su): constant, even when attacking", "Improved Tracking (Ex): can track creatures through air", "Fly 30 ft. (perfect)", "Darkvision 60 ft.", "Elemental traits (immune to poison, sleep, paralysis, stun, crits, flanking)" },
            Immunities = new CreatureImmunities { immuneToPoison = true, immuneToCriticalHits = true },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.6f, 0.65f, 0.7f, 0.3f),
            PanelColor = new Color(0.2f, 0.22f, 0.25f, 0.85f),
            NameColor = new Color(0.82f, 0.85f, 0.9f),
            Description = "Invisible Stalker (CR 7). Permanently invisible air elemental tracker. MM 3.5e p.160."
        });
    }
}
