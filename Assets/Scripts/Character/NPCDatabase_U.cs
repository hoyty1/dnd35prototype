using System.Collections.Generic;
using UnityEngine;

public static partial class NPCDatabase
{
    private static void RegisterCreatures_U()
    {
        RegisterUmberHulk();
        RegisterUnicorn();
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
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.45f, 0.35f, 0.25f, 1f),
            PanelColor = new Color(0.15f, 0.1f, 0.05f, 0.85f),
            NameColor = new Color(0.7f, 0.55f, 0.38f),
            Description = "Umber Hulk (CR 7). Tunneling aberration with confusing gaze. MM 3.5e p.248."
        });
    }

    /// <summary>
    /// Unicorn (CR 3) — MM 3.5e p.249. Large Magical Beast, Chaotic Good.
    /// 4d10+20 HP (42), horn +11 (1d8+8), 2 hooves +3 (1d4+2). AC 18 (DEX +3, natural +6, size -1).
    /// 60 ft. speed. Immune to poison, charm, compulsion. Magic circle against evil (always active).
    /// Wild empathy, spell-like abilities simplified for summoned version.
    /// CG alignment restriction for summoning.
    /// </summary>
    private static void RegisterUnicorn()
    {
        Register(new NPCDefinition
        {
            Id = "unicorn",
            Name = "Unicorn",
            ChallengeRating = "3",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            CharacterAlignment = Alignment.ChaoticGood,
            HitDice = 4,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 20, DEX = 17, CON = 21, WIS = 21, INT = 10, CHA = 24,
            BAB = 4, // 4 HD magical beast; MM horn +11 includes +3 racial bonus (system yields +8 horn, +3 hooves — hooves match MM)
            NaturalArmorBonus = 6,
            BaseSpeed = 12, // 60 ft.
            BaseHitDieHP = 42, // 4d10+20 = 4*5.5+20 = 42
            Immunities = new CreatureImmunities
            {
                immuneToPoison = true,
                immuneToMindAffecting = true // covers charm and compulsion
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Horn", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 2, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Hoof", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 2, IsPrimary = false }
            },
            HasScent = true,
            CreatureTags = new List<string> { "Magical Beast", "MM35", "SummonBase" },
            Feats = new List<string> { "Alertness", "Run" },
            SpecialAbilities = new List<string>
            {
                "Magic circle against evil (always active, 10 ft. radius)",
                "Immunity to poison, charm, compulsion",
                "Wild empathy +13",
                "Spell-like abilities: detect evil at will, cure light wounds 3/day, cure moderate wounds 1/day, greater teleport (self + rider only, 1/day)",
                "Darkvision 60 ft., low-light vision, scent"
            },
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(1f, 1f, 1f, 1f),
            PanelColor = new Color(0.3f, 0.3f, 0.35f, 0.85f),
            NameColor = new Color(1f, 0.95f, 0.85f),
            Description = "Unicorn (CR 3). Noble magical beast with healing and protective aura. CG alignment restriction for summoning. MM 3.5e p.249."
        });
    }
}
