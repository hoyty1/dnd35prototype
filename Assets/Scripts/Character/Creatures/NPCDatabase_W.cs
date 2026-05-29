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
        RegisterWillOWisp();
        RegisterWorg();
        RegisterWight();
        RegisterWyvern();
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
            AIProfileArchetype = NPCAIProfileArchetype.UndeadIncorporeal,
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
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.48f, 0.38f, 0.28f, 1f),
            PanelColor = new Color(0.2f, 0.15f, 0.1f, 0.85f),
            NameColor = new Color(0.9f, 0.82f, 0.7f),
            Description = "Wolverine with rage when wounded (+4 Str/Con, −2 AC). +8 racial Climb. MM 3.5e p.283."
        });
    }
    private static void RegisterWillOWisp()
    {
        Register(new NPCDefinition
        {
            Id = "will_o_wisp",
            Name = "Will-o'-Wisp",
            ChallengeRating = "6",
            Level = 9,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 9,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 1, DEX = 29, CON = 10, WIS = 16, INT = 15, CHA = 12,
            NaturalArmorBonus = 0,
            BaseSpeed = 10, // Fly 50 ft (perfect)
            BaseHitDieHP = 40,
            BAB = 6,
            Immunities = new CreatureImmunities { immuneToElectricity = true },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Shock", DamageDice = 8, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.None, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Aberration", "Air", "NaturalInvisibility", "Fly50", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Blind-Fight", "Dodge", "Improved Initiative", "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Immunity to Magic (Ex): immune to all spells except magic missile, maze, protection from evil", "Natural Invisibility (Ex): can suppress/resume as free action", "Immune to electricity", "Fly 50 ft. (perfect)", "Darkvision 60 ft." },
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.7f, 0.8f, 0.5f, 0.6f),
            PanelColor = new Color(0.25f, 0.3f, 0.15f, 0.85f),
            NameColor = new Color(0.9f, 0.95f, 0.7f),
            Description = "Will-o'-Wisp (CR 6). Glowing aberration nearly immune to magic. MM 3.5e p.255."
        });
    }

    private static void RegisterWorg()
    {
        Register(new NPCDefinition
        {
            Id = "worg",
            Name = "Worg",
            ChallengeRating = "2",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            CharacterAlignment = Alignment.NeutralEvil,
            HitDice = 4,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 17, DEX = 15, CON = 15, WIS = 14, INT = 6, CHA = 10,
            NaturalArmorBonus = 2,
            BaseSpeed = 10, // 50 ft
            BaseHitDieHP = 30,
            BAB = 4,
            HasScent = true,
            HasTripAttack = true,
            TripAttackCheckBonus = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Magical Beast", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Track" },
            SpecialAbilities = new List<string> { "Trip (Ex): free trip on bite hit", "Scent", "Darkvision 60 ft.", "Low-light vision" },
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.3f, 0.3f, 0.35f, 1f),
            PanelColor = new Color(0.08f, 0.08f, 0.12f, 0.85f),
            NameColor = new Color(0.5f, 0.5f, 0.6f),
            Description = "Worg (CR 2). Evil intelligent wolf with trip attack. MM 3.5e p.256."
        });
    }

    /// <summary>
    /// Wight (CR 3) — Medium undead.
    /// MM 3.5e p.255. Energy drain (1 negative level) on slam.
    /// 4d12 HP (26), slam 1d4+1 + energy drain.
    /// </summary>
    private static void RegisterWight()
    {
        Register(new NPCDefinition
        {
            Id = "wight",
            Name = "Wight",
            ChallengeRating = "3",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Undead",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 4,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 12, DEX = 12, CON = 10, WIS = 13, INT = 11, CHA = 15,
            NaturalArmorBonus = 4,
            BaseSpeed = 6, // 30 ft
            BaseHitDieHP = 26,
            BAB = 2,
            IsMindless = false,
            Immunities = new CreatureImmunities { ImmuneToCriticalHits = true, ImmuneToMindAffecting = true, ImmuneToPoison = true },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true, EnergyDrainOnHit = 1, EnergyDrainRemovalDC = 14 }
            },
            CreatureTags = new List<string> { "Undead", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Blind-Fight" },
            SpecialAbilities = new List<string> { "Energy Drain (Su): slam bestows 1 negative level, DC 14 Fort to remove", "Create Spawn: humanoid slain rises as wight in 1d4 rounds", "Darkvision 60 ft.", "Undead traits" },
            AIProfileArchetype = NPCAIProfileArchetype.UndeadTactical,
            SpriteColor = new Color(0.4f, 0.45f, 0.5f, 1f),
            PanelColor = new Color(0.12f, 0.14f, 0.18f, 0.85f),
            NameColor = new Color(0.65f, 0.7f, 0.8f),
            Description = "Wight (CR 3). Intelligent undead with energy drain. Creates spawn from slain humanoids. MM 3.5e p.255."
        });
    }

    /// <summary>
    /// Wyvern (CR 6) — Large dragon.
    /// MM 3.5e p.259. Two-legged dragon with venomous tail stinger.
    /// </summary>
    private static void RegisterWyvern()
    {
        Register(new NPCDefinition
        {
            Id = "wyvern",
            Name = "Wyvern",
            ChallengeRating = "6",
            Level = 7,
            CharacterClass = "Warrior",
            CreatureType = "Dragon",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 7,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 19, DEX = 12, CON = 15, WIS = 12, INT = 6, CHA = 9,
            NaturalArmorBonus = 5,
            BaseSpeed = 4, // 20 ft (fly 60 ft poor)
            BaseHitDieHP = 59,
            BAB = 7,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Sting", DamageDice = 6, DamageCount = 1, Count = 1,
                    BonusDamageSource = DamageBonusSource.Strength,
                    Range = 1, IsPrimary = true,
                    PoisonOnHitId = "wyvern_poison"
                },
                new NaturalAttackDefinition
                {
                    Name = "Bite", DamageDice = 8, DamageCount = 2, Count = 1,
                    BonusDamageSource = DamageBonusSource.StrengthHalf,
                    Range = 1, IsPrimary = false
                },
                new NaturalAttackDefinition
                {
                    Name = "Wing", DamageDice = 4, DamageCount = 1, Count = 2,
                    BonusDamageSource = DamageBonusSource.StrengthHalf,
                    Range = 1, IsPrimary = false
                },
                new NaturalAttackDefinition
                {
                    Name = "Talon", DamageDice = 6, DamageCount = 2, Count = 2,
                    BonusDamageSource = DamageBonusSource.StrengthHalf,
                    Range = 1, IsPrimary = false
                }
            },
            HasImprovedGrab = true,
            CreatureTags = new List<string> { "Dragon", "Darkvision60", "LowLightVision", "HasScent", "MM35" },
            Feats = new List<string> { "Alertness", "Flyby Attack", "Multiattack" },
            HasScent = true,
            SpecialAbilities = new List<string>
            {
                "Poison (Ex): sting, Fort DC 17, 2d6 Con/2d6 Con",
                "Improved Grab (Ex): talon attack, can carry off Medium or smaller",
                "Fly 60 ft. (poor)",
                "Darkvision 60 ft., Low-light vision, Scent",
                "Immunity to sleep and paralysis"
            },
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.35f, 0.3f, 0.25f, 1f),
            PanelColor = new Color(0.15f, 0.12f, 0.08f, 0.85f),
            NameColor = new Color(0.6f, 0.5f, 0.4f),
            Description = "Wyvern (CR 6). Two-legged dragon with venomous tail stinger. Fort DC 17 poison deals 2d6 Con. Flies and carries off prey. MM 3.5e p.259."
        });
    }

}
