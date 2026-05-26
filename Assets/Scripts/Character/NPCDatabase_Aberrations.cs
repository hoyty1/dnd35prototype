using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual aberration creatures.
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_Aberrations()
    {
        RegisterChoker();
        RegisterGrick();
        RegisterRustMonster();
        RegisterEttercap();
        RegisterOtyugh();
        RegisterCarrionCrawler();
        RegisterMimic();
        RegisterGibberingMouther();
        RegisterDestrachan();
        RegisterUmberHulk();
        RegisterAboleth();
        RegisterMindFlayer();
        RegisterChuul();
        RegisterChaosBeast();
        RegisterPhasm();
        RegisterSkum();
    }

    // ════════════════════════════════════════════════════════════
    //  Choker — MM p.35
    //  Aberration, Small, CR 2
    //  3d8+3 HP (16), 2 tentacles +6 melee (1d3+3)
    //  Str 16, Dex 14, Con 13, Int 4, Wis 13, Cha 7
    //  AC 17 (+1 size, +2 Dex, +4 natural), Improved Grab, Constrict 1d3+3
    //  Quickness (Su): extra standard action each round
    // ════════════════════════════════════════════════════════════
    private static void RegisterChoker()
    {
        Register(new NPCDefinition
        {
            Id = "choker",
            Name = "Choker",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 3,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = true,
            STR = 16, DEX = 14, CON = 13, WIS = 13, INT = 4, CHA = 7,
            NaturalArmorBonus = 4,
            BaseSpeed = 4, // 20 ft, climb 10 ft
            BaseHitDieHP = 16,
            BAB = 2,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Tentacle",
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Tentacle", DamageDice = 3, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Aberration", "Darkvision60", "MM35" },
            Feats = new List<string> { "Improved Initiative", "Lightning Reflexes" },
            SpecialAbilities = new List<string> { "Improved Grab", "Constrict 1d3+3", "Quickness (Su): extra standard action each round", "Darkvision 60 ft.", "Climb 10 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Grappler,
            SpriteColor = new Color(0.5f, 0.4f, 0.45f, 1f),
            PanelColor = new Color(0.18f, 0.12f, 0.15f, 0.85f),
            NameColor = new Color(0.75f, 0.6f, 0.7f),
            Description = "Choker (CR 2). Small aberration with tentacle grab and extra action per round. MM 3.5e p.35."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Grick — MM p.139
    //  Aberration, Medium, CR 3
    //  2d8+2 HP (11), 4 tentacles +3 melee (1d4+2), bite -2 melee (1d3+1)
    //  Str 14, Dex 14, Con 13, Int 3, Wis 14, Cha 5
    //  AC 16 (+2 Dex, +4 natural), DR 10/magic
    // ════════════════════════════════════════════════════════════
    private static void RegisterGrick()
    {
        Register(new NPCDefinition
        {
            Id = "grick",
            Name = "Grick",
            ChallengeRating = "3",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 14, DEX = 14, CON = 13, WIS = 14, INT = 3, CHA = 5,
            NaturalArmorBonus = 4,
            DamageReductionAmount = 10,
            DamageReductionBypass = DamageBypassTag.Magic,
            BaseSpeed = 6,
            BaseHitDieHP = 11,
            BAB = 1,
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Tentacle", DamageDice = 4, DamageCount = 1, Count = 4, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 3, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Aberration", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Track" },
            SpecialAbilities = new List<string> { "DR 10/magic", "Scent", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.4f, 0.42f, 0.38f, 1f),
            PanelColor = new Color(0.12f, 0.14f, 0.1f, 0.85f),
            NameColor = new Color(0.65f, 0.68f, 0.6f),
            Description = "Grick (CR 3). Worm-like aberration with DR 10/magic and four tentacles. MM 3.5e p.139."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Rust Monster — MM p.216
    //  Aberration, Medium, CR 3
    //  5d8+5 HP (27), antennae touch +3 melee (rust)
    //  Str 10, Dex 17, Con 13, Int 2, Wis 13, Cha 8
    //  AC 18 (+3 Dex, +5 natural)
    //  Rust: touch rusts metal, antennae touch destroys metal armor/weapons
    // ════════════════════════════════════════════════════════════
    private static void RegisterRustMonster()
    {
        Register(new NPCDefinition
        {
            Id = "rust_monster",
            Name = "Rust Monster",
            ChallengeRating = "3",
            Level = 5,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 5,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 10, DEX = 17, CON = 13, WIS = 13, INT = 2, CHA = 8,
            NaturalArmorBonus = 5,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 27,
            BAB = 3,
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Antennae", DamageDice = 0, DamageCount = 0, Count = 1, BonusDamageSource = DamageBonusSource.None, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 3, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.None, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Aberration", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Track" },
            SpecialAbilities = new List<string> { "Rust (Ex): metal touched by antennae corrodes/destroyed", "Scent: detects metal within 90 ft.", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.7f, 0.45f, 0.25f, 1f),
            PanelColor = new Color(0.25f, 0.15f, 0.06f, 0.85f),
            NameColor = new Color(0.9f, 0.6f, 0.35f),
            Description = "Rust Monster (CR 3). Corrodes metal on contact. MM 3.5e p.216."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Ettercap — MM p.106
    //  Aberration, Medium, CR 3
    //  5d8+5 HP (27), bite +3 melee (1d8+2 + poison), 2 claws +1 melee (1d3+1)
    //  Str 14, Dex 15, Con 13, Int 6, Wis 15, Cha 8
    //  AC 14 (+2 Dex, +2 natural)
    //  Poison: DC 15 Fort, 1d6 Dex/1d6 Dex, web
    // ════════════════════════════════════════════════════════════
    private static void RegisterEttercap()
    {
        Register(new NPCDefinition
        {
            Id = "ettercap",
            Name = "Ettercap",
            ChallengeRating = "3",
            Level = 5,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.NeutralEvil,
            HitDice = 5,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 14, DEX = 15, CON = 13, WIS = 15, INT = 6, CHA = 8,
            NaturalArmorBonus = 2,
            BaseSpeed = 6,
            BaseHitDieHP = 27,
            BAB = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 3, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Aberration", "Darkvision60", "MM35" },
            Feats = new List<string> { "Great Fortitude", "Multiattack" },
            SpecialAbilities = new List<string> { "Poison (Ex): bite, DC 15 Fort, 1d6 Dex/1d6 Dex", "Web (Ex): 8/day, entangle (DC 15 Ref to avoid, DC 17 Escape Artist or DC 21 Str to break)", "Low-light vision", "Darkvision 60 ft.", "Climb 30 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.35f, 0.4f, 0.3f, 1f),
            PanelColor = new Color(0.1f, 0.14f, 0.08f, 0.85f),
            NameColor = new Color(0.55f, 0.65f, 0.48f),
            Description = "Ettercap (CR 3). Spider-like aberration with poison bite and web. MM 3.5e p.106."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Otyugh — MM p.204
    //  Aberration, Large, CR 4
    //  6d8+9 HP (36), 2 tentacles +4 melee (1d6+2), bite +2 melee (1d4+1)
    //  Str 15, Dex 10, Con 13, Int 5, Wis 12, Cha 6
    //  AC 17 (-1 size, +8 natural), Improved Grab, Constrict, Disease
    // ════════════════════════════════════════════════════════════
    private static void RegisterOtyugh()
    {
        Register(new NPCDefinition
        {
            Id = "otyugh",
            Name = "Otyugh",
            ChallengeRating = "4",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 6,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 15, DEX = 10, CON = 13, WIS = 12, INT = 5, CHA = 6,
            NaturalArmorBonus = 8,
            BaseSpeed = 4, // 20 ft
            BaseHitDieHP = 36,
            BAB = 4,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Tentacle",
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Tentacle", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false, HasDiseaseOnHit = true, DiseaseOnHitType = DiseaseType.FilthFever }
            },
            CreatureTags = new List<string> { "Aberration", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Toughness" },
            SpecialAbilities = new List<string> { "Improved Grab", "Constrict 1d6+2", "Disease (Ex): filth fever, bite, DC 14 Fort", "Scent", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Grappler,
            SpriteColor = new Color(0.45f, 0.4f, 0.3f, 1f),
            PanelColor = new Color(0.15f, 0.12f, 0.06f, 0.85f),
            NameColor = new Color(0.7f, 0.62f, 0.45f),
            Description = "Otyugh (CR 4). Trash-dwelling aberration with grabbing tentacles and disease. MM 3.5e p.204."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Carrion Crawler — MM p.30 (3.0 → 3.5 update)
    //  Aberration, Large, CR 4
    //  3d8+6 HP (19), 8 tentacles +1 melee (paralysis), bite -3 melee (1d4+1)
    //  Str 14, Dex 13, Con 14, Int 1, Wis 15, Cha 6
    //  AC 17 (-1 size, +1 Dex, +7 natural)
    //  Paralysis: DC 13 Fort or paralyzed 2d6 rounds
    // ════════════════════════════════════════════════════════════
    private static void RegisterCarrionCrawler()
    {
        Register(new NPCDefinition
        {
            Id = "carrion_crawler",
            Name = "Carrion Crawler",
            ChallengeRating = "4",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 3,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 14, DEX = 13, CON = 14, WIS = 15, INT = 1, CHA = 6,
            NaturalArmorBonus = 7,
            BaseSpeed = 6,
            BaseHitDieHP = 19,
            BAB = 2,
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Tentacle", DamageDice = 0, DamageCount = 0, Count = 8, BonusDamageSource = DamageBonusSource.None, Range = 1, IsPrimary = true, ParalysisOnHitDC = 13, ParalysisOnHitDurationRounds = 10 },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Aberration", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Combat Reflexes" },
            SpecialAbilities = new List<string> { "Paralysis (Ex): tentacle touch, DC 13 Fort or paralyzed 2d6 rounds", "Scent", "Darkvision 60 ft.", "Climb 15 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.5f, 0.55f, 0.4f, 1f),
            PanelColor = new Color(0.16f, 0.2f, 0.1f, 0.85f),
            NameColor = new Color(0.75f, 0.82f, 0.6f),
            Description = "Carrion Crawler (CR 4). Multi-tentacled paralyzer. MM 3.5e p.30."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Mimic — MM p.186
    //  Aberration (Shapechanger), Large, CR 4
    //  7d8+21 HP (52), slam +8 melee (1d8+4)
    //  Str 19, Dex 12, Con 17, Int 10, Wis 13, Cha 10
    //  AC 15 (-1 size, +1 Dex, +5 natural), Adhesive, Crush
    // ════════════════════════════════════════════════════════════
    private static void RegisterMimic()
    {
        Register(new NPCDefinition
        {
            Id = "mimic",
            Name = "Mimic",
            ChallengeRating = "4",
            Level = 7,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 7,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 19, DEX = 12, CON = 17, WIS = 13, INT = 10, CHA = 10,
            NaturalArmorBonus = 5,
            BaseSpeed = 2, // 10 ft
            BaseHitDieHP = 52,
            BAB = 5,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Slam",
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 8, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            Immunities = new CreatureImmunities { immuneToAcid = true },
            CreatureTags = new List<string> { "Aberration", "Shapechanger", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Lightning Reflexes", "Weapon Focus (slam)" },
            SpecialAbilities = new List<string> { "Adhesive (Ex): auto-grapple on touch; DC 16 Str or dissolvent to release", "Crush (Ex): 1d8+4 per round to grappled creatures", "Mimic Shape (Ex): can assume form of any Medium–Large object", "Immune to acid", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Grappler,
            SpriteColor = new Color(0.5f, 0.45f, 0.35f, 1f),
            PanelColor = new Color(0.18f, 0.14f, 0.08f, 0.85f),
            NameColor = new Color(0.78f, 0.7f, 0.55f),
            Description = "Mimic (CR 4). Shapeshifting aberration that disguises as objects and adhesive-grabs prey. MM 3.5e p.186."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Gibbering Mouther — MM p.126
    //  Aberration, Medium, CR 5
    //  4d8+24 HP (42), 6 bites +4 melee (1), 1 spittle +4 ranged touch (1d4 acid)
    //  Str 10, Dex 10, Con 22, Int 4, Wis 13, Cha 13
    //  AC 19 (+4 natural, +5 amorphous), DR 5/bludgeoning
    //  Gibbering: 60 ft., Will DC 13 or confused
    // ════════════════════════════════════════════════════════════
    private static void RegisterGibberingMouther()
    {
        Register(new NPCDefinition
        {
            Id = "gibbering_mouther",
            Name = "Gibbering Mouther",
            ChallengeRating = "5",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 4,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 10, DEX = 10, CON = 22, WIS = 13, INT = 4, CHA = 13,
            NaturalArmorBonus = 9,
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Bludgeoning,
            BaseSpeed = 2, // 10 ft, swim 20 ft
            BaseHitDieHP = 42,
            BAB = 3,
            AuraAbility = new AuraAbilityDefinition
            {
                Name = "Gibbering",
                SaveDC = 13,
                IsWillSave = true,
                RangeFeet = 60,
                Effect = AuraEffectType.Confused,
                DurationRounds = 1
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 1, DamageCount = 1, Count = 6, BonusDamageSource = DamageBonusSource.None, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Aberration", "Amorphous", "Darkvision60", "MM35" },
            Feats = new List<string> { "Lightning Reflexes" },
            SpecialAbilities = new List<string> { "Gibbering (Su): 60 ft., Will DC 13 or confused 1 round", "Spittle (Ex): 30 ft. ranged touch, 1d4 acid, blinding 1d4 rounds on crit", "Ground Manipulation (Su): 10 ft. radius becomes bog-like", "Improved Grab", "Blood Drain: 1 CON/round", "Engulf", "DR 5/bludgeoning", "Amorphous (immune to critical hits)", "Darkvision 60 ft." },
            Immunities = new CreatureImmunities { immuneToCriticalHits = true },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.55f, 0.35f, 0.4f, 1f),
            PanelColor = new Color(0.2f, 0.1f, 0.12f, 0.85f),
            NameColor = new Color(0.82f, 0.55f, 0.6f),
            Description = "Gibbering Mouther (CR 5). Mass of eyes and mouths that confuses and engulfs prey. MM 3.5e p.126."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Destrachan — MM p.49
    //  Aberration, Large, CR 8
    //  8d8+24 HP (60), 2 claws +10 melee (1d6+4)
    //  Str 18, Dex 14, Con 16, Int 12, Wis 18, Cha 12
    //  AC 18 (-1 size, +2 Dex, +7 natural)
    //  Destructive Harmonics: 80 ft. cone, various effects
    //  Blindsight 100 ft. (deaf = blinded)
    // ════════════════════════════════════════════════════════════
    private static void RegisterDestrachan()
    {
        Register(new NPCDefinition
        {
            Id = "destrachan",
            Name = "Destrachan",
            ChallengeRating = "8",
            Level = 8,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.NeutralEvil,
            HitDice = 8,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 18, DEX = 14, CON = 16, WIS = 18, INT = 12, CHA = 12,
            NaturalArmorBonus = 7,
            BaseSpeed = 6,
            BaseHitDieHP = 60,
            BAB = 6,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true }
            },
            Immunities = new CreatureImmunities { immuneToSonic = true },
            CreatureTags = new List<string> { "Aberration", "Blindsight100", "MM35" },
            Feats = new List<string> { "Dodge", "Improved Initiative", "Lightning Reflexes" },
            SpecialAbilities = new List<string> { "Destructive Harmonics (Su): 80 ft. cone, choose: 4d6 sonic (Ref DC 17), shatter objects, or lethal pitch", "Blindsight 100 ft. (deaf = blinded)", "Immune to sonic/gaze attacks", "Protection from sonics grants immunity to harmonics" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.5f, 0.42f, 0.5f, 1f),
            PanelColor = new Color(0.18f, 0.14f, 0.2f, 0.85f),
            NameColor = new Color(0.75f, 0.65f, 0.8f),
            Description = "Destrachan (CR 8). Blind aberration with devastating sonic harmonics. MM 3.5e p.49."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Umber Hulk — MM p.248
    //  Aberration, Large, CR 7
    //  8d8+35 HP (71), 2 claws +11 melee (2d4+6), bite +9 melee (2d8+3)
    //  Str 23, Dex 13, Con 19, Int 11, Wis 11, Cha 13
    //  AC 18 (-1 size, +1 Dex, +8 natural)
    //  Confusing Gaze: 30 ft., Will DC 15 or confused
    //  Tremorsense 60 ft.
    // ════════════════════════════════════════════════════════════
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

    // ════════════════════════════════════════════════════════════
    //  Aboleth — MM p.8
    //  Aberration (Aquatic), Huge, CR 7
    //  8d8+40 HP (76), 4 tentacles +12 melee (1d6+8 + slime)
    //  Str 26, Dex 12, Con 20, Int 15, Wis 17, Cha 17
    //  AC 16 (-2 size, +1 Dex, +7 natural)
    //  Slime: DC 19 Fort or skin transforms, must keep moist
    //  Enslave 3/day: Will DC 17, as dominate person
    //  Psionics, mucus cloud
    // ════════════════════════════════════════════════════════════
    private static void RegisterAboleth()
    {
        Register(new NPCDefinition
        {
            Id = "aboleth",
            Name = "Aboleth",
            ChallengeRating = "7",
            Level = 8,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 8,
            SizeCategory = SizeCategory.Huge,
            IsTallCreature = false,
            STR = 26, DEX = 12, CON = 20, WIS = 17, INT = 15, CHA = 17,
            NaturalArmorBonus = 7,
            BaseSpeed = 2, // 10 ft, swim 60 ft
            BaseHitDieHP = 76,
            BAB = 6,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Tentacle", DamageDice = 6, DamageCount = 1, Count = 4, BonusDamageSource = DamageBonusSource.Strength, Range = 3, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Aberration", "Aquatic", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Combat Casting", "Iron Will" },
            SpecialAbilities = new List<string> { "Slime (Ex): tentacle hit, DC 19 Fort or skin transforms in 1d4+1 rounds", "Enslave (Su): 3/day, Will DC 17, as dominate person, unlimited range on same plane", "Mucus Cloud (Ex): 1 ft. cloud in water, DC 19 Fort or breathe water only for 3 hours", "Psionics: Hypnotic Pattern 3/day, Mirage Arcana 3/day, Persistent Image 3/day, Programmed Image 3/day", "Darkvision 60 ft.", "Swim 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Spellcaster,
            SpriteColor = new Color(0.3f, 0.45f, 0.5f, 1f),
            PanelColor = new Color(0.08f, 0.16f, 0.2f, 0.85f),
            NameColor = new Color(0.5f, 0.7f, 0.8f),
            Description = "Aboleth (CR 7). Ancient aquatic aberration with enslave and transformative slime. MM 3.5e p.8."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Mind Flayer (Illithid) — MM p.186
    //  Aberration, Medium, CR 8
    //  8d8+8 HP (44), 4 tentacles +8 melee (1d4+1)
    //  Str 12, Dex 14, Con 12, Int 19, Wis 17, Cha 17
    //  AC 15 (+2 Dex, +3 natural), SR 25
    //  Mind Blast (Sp): 60 ft. cone, Will DC 17 or stunned 3d4 rounds
    //  Extract Brain, Improved Grab
    // ════════════════════════════════════════════════════════════
    private static void RegisterMindFlayer()
    {
        Register(new NPCDefinition
        {
            Id = "mind_flayer",
            Name = "Mind Flayer",
            ChallengeRating = "8",
            Level = 8,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 8,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 12, DEX = 14, CON = 12, WIS = 17, INT = 19, CHA = 17,
            NaturalArmorBonus = 3,
            SpellResistance = 25,
            BaseSpeed = 6,
            BaseHitDieHP = 44,
            BAB = 6,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Tentacle",
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Tentacle", DamageDice = 4, DamageCount = 1, Count = 4, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Aberration", "Telepathy100", "Darkvision60", "MM35" },
            Feats = new List<string> { "Combat Casting", "Improved Initiative", "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Mind Blast (Sp): 60 ft. cone, Will DC 17 or stunned 3d4 rounds", "Extract Brain (Ex): coup de grace on grappled target, instant death", "Improved Grab: tentacle → grapple → extract brain", "SR 25", "Telepathy 100 ft.", "Psionics: Suggestion 3/day, Charm Monster 3/day, Detect Thoughts (at will), Levitate (at will), Plane Shift (at will)" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Spellcaster,
            SpriteColor = new Color(0.5f, 0.35f, 0.55f, 1f),
            PanelColor = new Color(0.18f, 0.1f, 0.22f, 0.85f),
            NameColor = new Color(0.75f, 0.55f, 0.85f),
            Description = "Mind Flayer (CR 8). Psionic aberration with mind blast and brain extraction. MM 3.5e p.186."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Chuul — MM p.35
    //  Aberration (Aquatic), Large, CR 7
    //  11d8+44 HP (93), 2 claws +13 melee (2d6+5)
    //  Str 20, Dex 10, Con 18, Int 10, Wis 14, Cha 5
    //  AC 22 (-1 size, +13 natural), Improved Grab, Constrict, Paralytic Tentacles
    // ════════════════════════════════════════════════════════════
    private static void RegisterChuul()
    {
        Register(new NPCDefinition
        {
            Id = "chuul",
            Name = "Chuul",
            ChallengeRating = "7",
            Level = 11,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 11,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 20, DEX = 10, CON = 18, WIS = 14, INT = 10, CHA = 5,
            NaturalArmorBonus = 13,
            BaseSpeed = 6, // 30 ft, swim 20 ft
            BaseHitDieHP = 93,
            BAB = 8,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Claw",
            Immunities = new CreatureImmunities { immuneToPoison = true },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 2, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Aberration", "Aquatic", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Blind-Fight", "Combat Reflexes", "Improved Initiative" },
            SpecialAbilities = new List<string> { "Improved Grab", "Constrict 3d6+5", "Paralytic Tentacles (Ex): grappled foe transferred to tentacles, DC 19 Fort or paralyzed 6 rounds", "Immune to poison", "Amphibious", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Grappler,
            SpriteColor = new Color(0.35f, 0.5f, 0.45f, 1f),
            PanelColor = new Color(0.1f, 0.18f, 0.15f, 0.85f),
            NameColor = new Color(0.55f, 0.78f, 0.7f),
            Description = "Chuul (CR 7). Lobster-like aberration with paralytic tentacles and constrict. MM 3.5e p.35."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Chaos Beast — MM p.33
    //  Outsider (Chaotic, Extraplanar), Medium, CR 7
    //  8d8+8 HP (44), 2 claws +10 melee (1d3+2)
    //  Str 14, Dex 13, Con 13, Int 10, Wis 10, Cha 10
    //  AC 16 (+1 Dex, +5 natural), DR 10/lawful
    //  Corporeal Instability: Fort DC 15 or body goes amorphous
    //  SR 15, immune to crits/transformation
    // ════════════════════════════════════════════════════════════
    private static void RegisterChaosBeast()
    {
        Register(new NPCDefinition
        {
            Id = "chaos_beast",
            Name = "Chaos Beast",
            ChallengeRating = "7",
            Level = 8,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.ChaoticNeutral,
            HitDice = 8,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 14, DEX = 13, CON = 13, WIS = 10, INT = 10, CHA = 10,
            NaturalArmorBonus = 5,
            DamageReductionAmount = 10,
            DamageReductionBypass = DamageBypassTag.Lawful,
            SpellResistance = 15,
            BaseSpeed = 4, // 20 ft
            BaseHitDieHP = 44,
            BAB = 8,
            Immunities = new CreatureImmunities { immuneToCriticalHits = true },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 3, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Outsider", "Chaotic", "Extraplanar", "Darkvision60", "MM35" },
            Feats = new List<string> { "Dodge", "Improved Initiative", "Mobility" },
            SpecialAbilities = new List<string> { "Corporeal Instability (Su): claw hit, DC 15 Fort or body becomes amorphous (1 WIS damage/round)", "DR 10/lawful", "SR 15", "Immune to crits and transformation", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Berserk,
            SpriteColor = new Color(0.6f, 0.35f, 0.55f, 1f),
            PanelColor = new Color(0.22f, 0.1f, 0.2f, 0.85f),
            NameColor = new Color(0.85f, 0.5f, 0.78f),
            Description = "Chaos Beast (CR 7). Amorphous outsider that dissolves victims' forms. MM 3.5e p.33."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Phasm — MM p.207
    //  Aberration (Shapechanger), Medium, CR 7
    //  15d8+15 HP (82), slam +11 melee (1d3+1)
    //  Str 12, Dex 15, Con 12, Int 16, Wis 14, Cha 14
    //  AC 17 (+2 Dex, +5 natural)
    //  Shapechange at will (any form Small–Large), Amorphous, Telepathy
    // ════════════════════════════════════════════════════════════
    private static void RegisterPhasm()
    {
        Register(new NPCDefinition
        {
            Id = "phasm",
            Name = "Phasm",
            ChallengeRating = "7",
            Level = 15,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.ChaoticNeutral,
            HitDice = 15,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 12, DEX = 15, CON = 12, WIS = 14, INT = 16, CHA = 14,
            NaturalArmorBonus = 5,
            BaseSpeed = 6,
            BaseHitDieHP = 82,
            BAB = 11,
            Immunities = new CreatureImmunities { immuneToPoison = true },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 3, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Aberration", "Shapechanger", "Telepathy100", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Blind-Fight", "Combat Reflexes", "Improved Initiative", "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Alternate Form (Su): at will, any Small–Large creature", "Amorphous (Ex): immune to poison, sleep, paralysis, polymorph, stunning, crits", "Resilient (Ex): +4 racial saves vs mind-affecting", "Tremorsense 60 ft.", "Scent", "Telepathy 100 ft.", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.55f, 0.55f, 0.6f, 1f),
            PanelColor = new Color(0.2f, 0.2f, 0.25f, 0.85f),
            NameColor = new Color(0.78f, 0.78f, 0.88f),
            Description = "Phasm (CR 7). Shapeshifting aberration with at-will alternate form. MM 3.5e p.207."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Skum — MM p.228
    //  Aberration (Aquatic), Medium, CR 2
    //  2d8+4 HP (13), bite +5 melee (2d6+4), 2 claws +0 melee (1d4+2)
    //  Str 19, Dex 13, Con 15, Int 10, Wis 10, Cha 6
    //  AC 13 (+1 Dex, +2 natural), rake 1d6+2
    // ════════════════════════════════════════════════════════════
    private static void RegisterSkum()
    {
        Register(new NPCDefinition
        {
            Id = "skum",
            Name = "Skum",
            ChallengeRating = "2",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 19, DEX = 13, CON = 15, WIS = 10, INT = 10, CHA = 6,
            NaturalArmorBonus = 2,
            BaseSpeed = 4, // 20 ft, swim 40 ft
            BaseHitDieHP = 13,
            BAB = 2,
            HasRake = true,
            RakeAttack = new NaturalAttackDefinition { Name = "Rake", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = false },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Aberration", "Aquatic", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness" },
            SpecialAbilities = new List<string> { "Rake 1d6+2 (when grappling)", "Swim 40 ft.", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.35f, 0.45f, 0.4f, 1f),
            PanelColor = new Color(0.1f, 0.16f, 0.12f, 0.85f),
            NameColor = new Color(0.55f, 0.72f, 0.65f),
            Description = "Skum (CR 2). Aquatic aberration servant of aboleths. MM 3.5e p.228."
        });
    }
}
