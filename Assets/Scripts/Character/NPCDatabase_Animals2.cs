using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual animals, vermin, and swarm creatures not covered by other files.
/// Also includes celestial/fiendish template base animals.
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_Animals2()
    {
        RegisterGiantConstrictorSnake();
        RegisterGiantStagBeetle();
        RegisterGiantWorkerAnt();
        RegisterHyena();
        RegisterMonitorLizard();
        RegisterCelestialLion();
        RegisterFiendishDireRat();
        RegisterLocustSwarm();
        RegisterCentipedeSwarm();
        RegisterHellwaspSwarm();
    }

    // ════════════════════════════════════════════════════════════
    //  Giant Constrictor Snake — MM p.279
    //  Animal, Huge, CR 5
    //  11d8+11 HP (60), bite +13 melee (1d8+10)
    //  Str 25, Dex 17, Con 13, Int 1, Wis 12, Cha 2
    //  AC 15 (-2 size, +3 Dex, +4 natural)
    //  Improved Grab, Constrict 1d8+10
    // ════════════════════════════════════════════════════════════
    private static void RegisterGiantConstrictorSnake()
    {
        Register(new NPCDefinition
        {
            Id = "giant_constrictor_snake",
            Name = "Giant Constrictor Snake",
            ChallengeRating = "5",
            Level = 11,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 11,
            SizeCategory = SizeCategory.Huge,
            IsTallCreature = false,
            STR = 25, DEX = 17, CON = 13, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 4,
            BaseSpeed = 4, // 20 ft, climb 20 ft, swim 20 ft
            BaseHitDieHP = 60,
            BAB = 8,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Bite",
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 3, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Animal", "MM35" },
            Feats = new List<string> { "Alertness", "Endurance", "Skill Focus (Hide)", "Toughness" },
            SpecialAbilities = new List<string> { "Improved Grab", "Constrict 1d8+10", "Scent", "Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Grappler,
            SpriteColor = new Color(0.4f, 0.45f, 0.3f, 1f),
            PanelColor = new Color(0.12f, 0.15f, 0.06f, 0.85f),
            NameColor = new Color(0.62f, 0.7f, 0.48f),
            Description = "Giant Constrictor Snake (CR 5). Massive snake with constrict. MM 3.5e p.279."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Giant Stag Beetle — MM p.285
    //  Vermin, Large, CR 4
    //  7d8+21 HP (52), bite +8 melee (4d6+6)
    //  Str 23, Dex 10, Con 17, Int 0, Wis 10, Cha 9
    //  AC 19 (-1 size, +10 natural), Trample 2d8+3
    // ════════════════════════════════════════════════════════════
    private static void RegisterGiantStagBeetle()
    {
        Register(new NPCDefinition
        {
            Id = "giant_stag_beetle",
            Name = "Giant Stag Beetle",
            ChallengeRating = "4",
            Level = 7,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 7,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 23, DEX = 10, CON = 17, WIS = 10, INT = 0, CHA = 9,
            NaturalArmorBonus = 10,
            IsMindless = true,
            BaseSpeed = 4, // 20 ft
            BaseHitDieHP = 52,
            BAB = 5,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 4, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 2, IsPrimary = true }
            },
            Immunities = new CreatureImmunities { immuneToMindAffecting = true },
            CreatureTags = new List<string> { "Vermin", "Darkvision60", "MM35" },
            Feats = new List<string>(),
            SpecialAbilities = new List<string> { "Trample (Ex): 2d8+3, Ref DC 19 half", "Vermin traits (mindless)", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.3f, 0.28f, 0.22f, 1f),
            PanelColor = new Color(0.08f, 0.06f, 0.03f, 0.85f),
            NameColor = new Color(0.5f, 0.45f, 0.35f),
            Description = "Giant Stag Beetle (CR 4). Large beetle with crushing mandibles. MM 3.5e p.285."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Giant Worker Ant — MM p.284
    //  Vermin, Medium, CR 1
    //  2d8 HP (9), bite +1 melee (1d6+1)
    //  Str 12, Dex 10, Con 10, Int 0, Wis 11, Cha 9
    //  AC 17 (+7 natural), Improved Grab
    // ════════════════════════════════════════════════════════════
    private static void RegisterGiantWorkerAnt()
    {
        Register(new NPCDefinition
        {
            Id = "giant_worker_ant",
            Name = "Giant Worker Ant",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 12, DEX = 10, CON = 10, WIS = 11, INT = 0, CHA = 9,
            NaturalArmorBonus = 7,
            IsMindless = true,
            BaseSpeed = 10, // 50 ft, climb 20 ft
            BaseHitDieHP = 9,
            BAB = 1,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Bite",
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            Immunities = new CreatureImmunities { immuneToMindAffecting = true },
            CreatureTags = new List<string> { "Vermin", "Darkvision60", "MM35" },
            Feats = new List<string>(),
            SpecialAbilities = new List<string> { "Improved Grab", "Acid Sting: 1d4+1 acid (workers have reduced sting)", "Vermin traits (mindless)", "Scent", "Darkvision 60 ft.", "Climb 20 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.35f, 0.25f, 0.15f, 1f),
            PanelColor = new Color(0.1f, 0.06f, 0.02f, 0.85f),
            NameColor = new Color(0.58f, 0.42f, 0.25f),
            Description = "Giant Worker Ant (CR 1). Hive-dwelling vermin with improved grab. MM 3.5e p.284."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Hyena — MM p.274
    //  Animal, Medium, CR 1
    //  2d8+4 HP (13), bite +3 melee (1d6+3)
    //  Str 14, Dex 15, Con 15, Int 2, Wis 13, Cha 6
    //  AC 14 (+2 Dex, +2 natural), Trip
    // ════════════════════════════════════════════════════════════
    private static void RegisterHyena()
    {
        Register(new NPCDefinition
        {
            Id = "hyena",
            Name = "Hyena",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 14, DEX = 15, CON = 15, WIS = 13, INT = 2, CHA = 6,
            NaturalArmorBonus = 2,
            BaseSpeed = 10, // 50 ft
            BaseHitDieHP = 13,
            BAB = 1,
            HasTripAttack = true,
            TripAttackCheckBonus = 1,
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Animal", "MM35" },
            Feats = new List<string> { "Alertness" },
            SpecialAbilities = new List<string> { "Trip (Ex): free trip on bite", "Scent", "Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.6f, 0.52f, 0.35f, 1f),
            PanelColor = new Color(0.22f, 0.18f, 0.1f, 0.85f),
            NameColor = new Color(0.85f, 0.75f, 0.55f),
            Description = "Hyena (CR 1). Pack hunter with trip attack. MM 3.5e p.274."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Monitor Lizard — MM p.275
    //  Animal, Medium, CR 2
    //  3d8+9 HP (22), bite +5 melee (1d8+4)
    //  Str 17, Dex 15, Con 17, Int 1, Wis 12, Cha 2
    //  AC 15 (+2 Dex, +3 natural)
    // ════════════════════════════════════════════════════════════
    private static void RegisterMonitorLizard()
    {
        Register(new NPCDefinition
        {
            Id = "monitor_lizard",
            Name = "Monitor Lizard",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 17, DEX = 15, CON = 17, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 3,
            BaseSpeed = 6, // 30 ft, swim 30 ft
            BaseHitDieHP = 22,
            BAB = 2,
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Animal", "MM35" },
            Feats = new List<string> { "Alertness", "Great Fortitude" },
            SpecialAbilities = new List<string> { "Scent", "Low-light vision", "Swim 30 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.45f, 0.5f, 0.35f, 1f),
            PanelColor = new Color(0.15f, 0.18f, 0.08f, 0.85f),
            NameColor = new Color(0.68f, 0.75f, 0.55f),
            Description = "Monitor Lizard (CR 2). Large reptilian predator. MM 3.5e p.275."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Celestial Lion — MM p.31 (Celestial template on Lion)
    //  Magical Beast (Augmented Animal, Extraplanar), Large, CR 4
    //  5d8+10 HP (32), 2 claws +7 melee (1d4+5), bite +2 melee (1d8+2)
    //  Str 21, Dex 17, Con 15, Int 3, Wis 12, Cha 6 (Int 3 for celestial)
    //  AC 15 (-1 size, +3 Dex, +3 natural)
    //  Smite Evil 1/day (+5 damage), DR 5/magic, Resist acid/cold/elec 5
    // ════════════════════════════════════════════════════════════
    private static void RegisterCelestialLion()
    {
        Register(new NPCDefinition
        {
            Id = "celestial_lion",
            Name = "Celestial Lion",
            ChallengeRating = "4",
            Level = 5,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            CharacterAlignment = Alignment.NeutralGood,
            HitDice = 5,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 21, DEX = 17, CON = 15, WIS = 12, INT = 3, CHA = 6,
            NaturalArmorBonus = 3,
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Magic,
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Acid, Amount = 5 },
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 5 },
                new DamageResistanceEntry { Type = DamageType.Electricity, Amount = 5 }
            },
            SpellResistance = 10,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 32,
            BAB = 3,
            HasPounce = true,
            HasRake = true,
            RakeAttack = new NaturalAttackDefinition { Name = "Rake", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false },
            HasScent = true,
            GainsSmiteEvil = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 2, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Magical Beast", "Extraplanar", "Good", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Run" },
            SpecialAbilities = new List<string> { "Pounce", "Rake: 2 claws 1d4+2", "Smite Evil (Su): 1/day, +5 damage vs evil", "DR 5/magic", "SR 10", "Resist acid 5, cold 5, electricity 5", "Scent", "Darkvision 60 ft., Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.7f, 0.65f, 0.45f, 1f),
            PanelColor = new Color(0.25f, 0.22f, 0.12f, 0.85f),
            NameColor = new Color(0.92f, 0.88f, 0.65f),
            Description = "Celestial Lion (CR 4). Holy lion with smite evil and pounce. MM 3.5e p.31 + Lion."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Fiendish Dire Rat — MM p.31 (Fiendish template on Dire Rat)
    //  Magical Beast (Augmented Animal, Extraplanar), Small, CR 1/3
    //  1d8+1 HP (5), bite +4 melee (1d4)
    //  Str 10, Dex 17, Con 12, Int 3, Wis 12, Cha 4
    //  AC 15 (+1 size, +3 Dex, +1 natural)
    //  Smite Good 1/day, DR 5/magic (3+ HD), Resist cold/fire 5
    // ════════════════════════════════════════════════════════════
    private static void RegisterFiendishDireRat()
    {
        Register(new NPCDefinition
        {
            Id = "fiendish_dire_rat",
            Name = "Fiendish Dire Rat",
            ChallengeRating = "1/3",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            CharacterAlignment = Alignment.NeutralEvil,
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 10, DEX = 17, CON = 12, WIS = 12, INT = 3, CHA = 4,
            NaturalArmorBonus = 1,
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 5 },
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 5 }
            },
            BaseSpeed = 8, // 40 ft, climb 20 ft
            BaseHitDieHP = 5,
            BAB = 0,
            HasScent = true,
            GainsSmiteGood = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.None, Range = 1, IsPrimary = true, HasDiseaseOnHit = true, DiseaseOnHitType = DiseaseType.FilthFever }
            },
            CreatureTags = new List<string> { "Magical Beast", "Extraplanar", "Evil", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Smite Good (Su): 1/day, +1 damage vs good", "Disease (Ex): filth fever, DC 11 Fort", "Resist cold 5, fire 5", "Scent", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.3f, 0.25f, 0.2f, 1f),
            PanelColor = new Color(0.08f, 0.05f, 0.03f, 0.85f),
            NameColor = new Color(0.5f, 0.4f, 0.3f),
            Description = "Fiendish Dire Rat (CR 1/3). Evil dire rat with smite good and disease. MM 3.5e."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Locust Swarm — MM p.239
    //  Vermin (Swarm), Diminutive, CR 3
    //  6d8-6 HP (21), swarm (2d6)
    //  Str 1, Dex 19, Con 8, Int 0, Wis 10, Cha 2
    //  AC 18 (+4 size, +4 Dex), Distraction DC 12
    // ════════════════════════════════════════════════════════════
    private static void RegisterLocustSwarm()
    {
        Register(new NPCDefinition
        {
            Id = "locust_swarm",
            Name = "Locust Swarm",
            ChallengeRating = "3",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 6,
            SizeCategory = SizeCategory.Diminutive,
            IsTallCreature = false,
            STR = 1, DEX = 19, CON = 8, WIS = 10, INT = 0, CHA = 2,
            NaturalArmorBonus = 0,
            IsMindless = true,
            IsSwarm = true,
            BaseSpeed = 2, // 10 ft, fly 30 ft (poor)
            BaseHitDieHP = 21,
            BAB = 4,
            Immunities = new CreatureImmunities { immuneToMindAffecting = true, immuneToWeaponDamage = true },
            SwarmTraits = new SwarmTraits { SwarmDamage = 6, SwarmDamageCount = 2, SwarmDamageType = DamageType.Piercing, DistractionDC = 12 },
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Vermin", "Swarm", "Fly30", "Darkvision60", "MM35" },
            Feats = new List<string>(),
            SpecialAbilities = new List<string> { "Swarm Attack: 2d6 to creatures in space", "Distraction (Ex): DC 12 Fort or nauseated 1 round", "Immune to weapon damage", "Vermin traits (mindless)", "Fly 30 ft.", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.IndiscriminateSwarm,
            SpriteColor = new Color(0.45f, 0.5f, 0.3f, 1f),
            PanelColor = new Color(0.15f, 0.18f, 0.06f, 0.85f),
            NameColor = new Color(0.7f, 0.78f, 0.48f),
            Description = "Locust Swarm (CR 3). Diminutive vermin swarm with distraction. MM 3.5e p.239."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Centipede Swarm — MM p.238
    //  Vermin (Swarm), Diminutive, CR 4
    //  9d8-9 HP (31), swarm (2d6 + poison)
    //  Str 1, Dex 19, Con 8, Int 0, Wis 10, Cha 2
    //  AC 18 (+4 size, +4 Dex), Distraction DC 13, Poison DC 13 1d4 Dex
    // ════════════════════════════════════════════════════════════
    private static void RegisterCentipedeSwarm()
    {
        Register(new NPCDefinition
        {
            Id = "centipede_swarm",
            Name = "Centipede Swarm",
            ChallengeRating = "4",
            Level = 9,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 9,
            SizeCategory = SizeCategory.Diminutive,
            IsTallCreature = false,
            STR = 1, DEX = 19, CON = 8, WIS = 10, INT = 0, CHA = 2,
            NaturalArmorBonus = 0,
            IsMindless = true,
            IsSwarm = true,
            BaseSpeed = 4, // 20 ft, climb 20 ft
            BaseHitDieHP = 31,
            BAB = 6,
            Immunities = new CreatureImmunities { immuneToMindAffecting = true, immuneToWeaponDamage = true },
            SwarmTraits = new SwarmTraits { SwarmDamage = 6, SwarmDamageCount = 2, SwarmDamageType = DamageType.Piercing, DistractionDC = 13 },
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Vermin", "Swarm", "Darkvision60", "MM35" },
            Feats = new List<string>(),
            SpecialAbilities = new List<string> { "Swarm Attack: 2d6 + poison", "Poison (Ex): DC 13 Fort, 1d4 Dex/1d4 Dex", "Distraction (Ex): DC 13 Fort or nauseated", "Immune to weapon damage", "Tremorsense 30 ft.", "Vermin traits (mindless)", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.IndiscriminateSwarm,
            SpriteColor = new Color(0.35f, 0.3f, 0.25f, 1f),
            PanelColor = new Color(0.1f, 0.08f, 0.04f, 0.85f),
            NameColor = new Color(0.58f, 0.5f, 0.4f),
            Description = "Centipede Swarm (CR 4). Venomous vermin swarm. MM 3.5e p.238."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Hellwasp Swarm — MM p.238
    //  Magical Beast (Extraplanar, Swarm), Diminutive, CR 8
    //  12d10+27 HP (93), swarm (3d6 + poison + inhabit)
    //  Str 1, Dex 22, Con 14, Int 6, Wis 13, Cha 9
    //  AC 20 (+4 size, +6 Dex)
    //  Distraction DC 18, Poison DC 18 1d6 Dex, Inhabit (possess dead body)
    // ════════════════════════════════════════════════════════════
    private static void RegisterHellwaspSwarm()
    {
        Register(new NPCDefinition
        {
            Id = "hellwasp_swarm",
            Name = "Hellwasp Swarm",
            ChallengeRating = "8",
            Level = 12,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 12,
            SizeCategory = SizeCategory.Diminutive,
            IsTallCreature = false,
            STR = 1, DEX = 22, CON = 14, WIS = 13, INT = 6, CHA = 9,
            NaturalArmorBonus = 0,
            IsSwarm = true,
            DamageReductionAmount = 10,
            DamageReductionBypass = DamageBypassTag.Magic,
            BaseSpeed = 1, // 5 ft, fly 40 ft (good)
            BaseHitDieHP = 93,
            BAB = 12,
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 10 }
            },
            Immunities = new CreatureImmunities { immuneToWeaponDamage = true },
            SwarmTraits = new SwarmTraits { SwarmDamage = 6, SwarmDamageCount = 3, SwarmDamageType = DamageType.Piercing, DistractionDC = 18 },
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Magical Beast", "Extraplanar", "Evil", "Swarm", "Fly40", "Darkvision60", "MM35" },
            Feats = new List<string>(),
            SpecialAbilities = new List<string> { "Swarm Attack: 3d6 + poison", "Poison (Ex): DC 18 Fort, 1d6 Dex/1d6 Dex", "Distraction (Ex): DC 18 Fort or nauseated", "Inhabit (Ex): can fill and animate a dead body", "Immune to weapon damage", "DR 10/magic", "Resist fire 10", "Fly 40 ft. (good)", "Hive Mind", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.IndiscriminateSwarm,
            SpriteColor = new Color(0.5f, 0.35f, 0.2f, 1f),
            PanelColor = new Color(0.18f, 0.1f, 0.04f, 0.85f),
            NameColor = new Color(0.78f, 0.55f, 0.3f),
            Description = "Hellwasp Swarm (CR 8). Fiendish wasp swarm that can inhabit corpses. MM 3.5e p.238."
        });
    }
}
