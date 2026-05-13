using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creature registrations used by Summon Monster I–III spell lists.
/// These are official D&D 3.5e Monster Manual creatures, not custom content.
/// Organized in a single partial file for easy reference alongside SummonMonsterLists.cs.
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterSummonMonsterBaseCreatures()
    {
        RegisterSummonOctopus();
        RegisterSummonSmallViper();
        RegisterSummonMonstrousCentipedeMedium();
        RegisterSummonMonstrousScorpionSmall();
        RegisterSummonMonstrousSpiderSmall();
        RegisterSummonDireBat();
        RegisterSummonSmallAirElemental();
        RegisterSummonSmallFireElemental();
        RegisterSummonCrocodile();
        RegisterSummonBlackBear();
        RegisterSummonApe();
        RegisterSummonDireBadger();
        RegisterSummonLargeShark();
        RegisterSummonConstrictorSnake();

        // Summon Monster III new creatures
        RegisterSummonBison();
        RegisterSummonBoar();
        RegisterSummonDireWeasel();
        RegisterSummonWolverine();
        RegisterSummonLargeViper();
        RegisterSummonHugeMonstruousCentipede();
        RegisterSummonSmallEarthElemental();
        RegisterSummonSmallWaterElemental();
        RegisterSummonHippogriff();
        RegisterSummonHellHound();
        RegisterSummonDretch();

        // Keep compatibility aliases for commonly referenced summon list names.
        RegisterSummonCreatureAliases();
    }

    private static void RegisterSummonCreatureAliases()
    {
        RegisterSummonAlias("wolf", "wolf_pack_hunter", "Wolf");
        RegisterSummonAlias("badger", "dire_badger", "Badger");

        // These IDs are used by external validation scripts; map to closest existing summon baselines.
        RegisterSummonAlias("riding_dog", "dog", "Riding Dog");
        RegisterSummonAlias("owl", "eagle", "Owl");
        RegisterSummonAlias("raven", "eagle", "Raven");
        RegisterSummonAlias("giant_bee", "dire_bat", "Giant Bee");
    }

    private static void RegisterSummonAlias(string aliasId, string sourceId, string overrideName = null)
    {
        NPCDefinition source = Get(sourceId);
        if (source == null)
            return;

        NPCDefinition alias = source.Clone();
        alias.Id = aliasId;

        if (!string.IsNullOrWhiteSpace(overrideName))
            alias.Name = overrideName;

        if (alias.CreatureTags == null)
            alias.CreatureTags = new List<string>();
        if (!alias.CreatureTags.Contains("SummonAlias"))
            alias.CreatureTags.Add("SummonAlias");

        Register(alias);
    }

    private static void RegisterSummonOctopus()
    {
        Register(new NPCDefinition
        {
            Id = "octopus",
            Name = "Octopus",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 2,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 12, DEX = 15, CON = 11, WIS = 12, INT = 2, CHA = 3,
            NaturalArmorBonus = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Tentacles", DamageDice = 3, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 5,
            BaseHitDieHP = 11,
            CreatureTags = new List<string> { "Animal", "Aquatic", "SummonBase" },
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Tentacles",
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.62f, 0.48f, 0.68f, 1f),
            PanelColor = new Color(0.18f, 0.13f, 0.23f, 0.85f),
            NameColor = new Color(0.88f, 0.8f, 0.95f),
            Description = "Summon Monster baseline octopus with improved-grab style control attack."
        });
    }

    private static void RegisterSummonSmallViper()
    {
        Register(new NPCDefinition
        {
            Id = "small_viper",
            Name = "Small Viper",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 6, DEX = 17, CON = 11, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 3, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 6,
            BaseHitDieHP = 6,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.45f, 0.72f, 0.34f, 1f),
            PanelColor = new Color(0.15f, 0.23f, 0.12f, 0.85f),
            NameColor = new Color(0.84f, 0.94f, 0.8f),
            Description = "Summon Monster baseline Small Viper."
        });
    }

    private static void RegisterSummonMonstrousCentipedeMedium()
    {
        Register(new NPCDefinition
        {
            Id = "monstrous_centipede_medium",
            Name = "Monstrous Centipede (Medium)",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = 1,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 9, DEX = 15, CON = 10, WIS = 10, INT = 1, CHA = 2,
            NaturalArmorBonus = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8,
            BaseHitDieHP = 7,
            CreatureTags = new List<string> { "Vermin", "SummonBase" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.56f, 0.44f, 0.3f, 1f),
            PanelColor = new Color(0.18f, 0.12f, 0.07f, 0.85f),
            NameColor = new Color(0.92f, 0.82f, 0.71f),
            Description = "Summon Monster baseline Monstrous Centipede (Medium)."
        });
    }

    private static void RegisterSummonMonstrousScorpionSmall()
    {
        Register(new NPCDefinition
        {
            Id = "monstrous_scorpion_small",
            Name = "Monstrous Scorpion (Small)",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 8, DEX = 13, CON = 10, WIS = 10, INT = 1, CHA = 2,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 3, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Sting", DamageDice = 3, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 8,
            BaseHitDieHP = 7,
            CreatureTags = new List<string> { "Vermin", "SummonBase" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.51f, 0.4f, 0.29f, 1f),
            PanelColor = new Color(0.17f, 0.11f, 0.07f, 0.85f),
            NameColor = new Color(0.9f, 0.8f, 0.67f),
            Description = "Summon Monster baseline Monstrous Scorpion (Small)."
        });
    }

    private static void RegisterSummonMonstrousSpiderSmall()
    {
        Register(new NPCDefinition
        {
            Id = "monstrous_spider_small",
            Name = "Monstrous Spider (Small)",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 9, DEX = 15, CON = 10, WIS = 10, INT = 1, CHA = 2,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8,
            BaseHitDieHP = 7,
            CreatureTags = new List<string> { "Vermin", "SummonBase" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.3f, 0.3f, 0.3f, 1f),
            PanelColor = new Color(0.13f, 0.13f, 0.13f, 0.85f),
            NameColor = new Color(0.82f, 0.82f, 0.82f),
            Description = "Summon Monster baseline Monstrous Spider (Small)."
        });
    }

    private static void RegisterSummonDireBat()
    {
        Register(new NPCDefinition
        {
            Id = "dire_bat",
            Name = "Dire Bat",
            ChallengeRating = "2",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 4,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 17, DEX = 22, CON = 17, WIS = 14, INT = 2, CHA = 6,
            NaturalArmorBonus = 5,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 4, // 20 ft. land, fly 40 ft. (good)
            BaseHitDieHP = 30,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            SpecialAbilities = new List<string> { "Blindsense 40 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.4f, 0.32f, 0.38f, 1f),
            PanelColor = new Color(0.15f, 0.1f, 0.16f, 0.85f),
            NameColor = new Color(0.85f, 0.79f, 0.9f),
            Description = "Dire bat with blindsense 40 ft. echolocation. Wingspan 15 ft., ~200 lbs. MM 3.5e p.62."
        });
    }

    private static void RegisterSummonSmallAirElemental()
    {
        Register(new NPCDefinition
        {
            Id = "small_air_elemental",
            Name = "Small Air Elemental",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Elemental",
            HitDice = 2,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 10, DEX = 17, CON = 10, WIS = 11, INT = 4, CHA = 11,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 20, // Fly 100 ft. (perfect)
            BaseHitDieHP = 9,
            Immunities = new CreatureImmunities
            {
                immuneToPoison = true,
                immuneToCriticalHits = true,
                immuneToSneakAttack = true
            },
            CreatureTags = new List<string> { "Elemental", "Air", "Extraplanar", "SummonBase" },
            SpecialAbilities = new List<string> { "Elemental traits", "Air mastery", "Whirlwind (DC 11)", "Fly 100 ft. (perfect)", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.68f, 0.86f, 1f, 1f),
            PanelColor = new Color(0.14f, 0.19f, 0.26f, 0.85f),
            NameColor = new Color(0.85f, 0.95f, 1f),
            Description = "Small Air Elemental. Immune to poison, sleep, paralysis, stunning. Not subject to critical hits or flanking. MM 3.5e p.95."
        });
    }

    private static void RegisterSummonSmallFireElemental()
    {
        Register(new NPCDefinition
        {
            Id = "small_fire_elemental",
            Name = "Small Fire Elemental",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Elemental",
            HitDice = 2,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 10, DEX = 13, CON = 10, WIS = 11, INT = 4, CHA = 11,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                // Slam 1d4 plus 1d4 fire (fire damage represented as special ability)
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 10, // 50 ft.
            BaseHitDieHP = 9,
            Immunities = new CreatureImmunities
            {
                immuneToPoison = true,
                immuneToFire = true,
                immuneToCriticalHits = true,
                immuneToSneakAttack = true
            },
            DamageImmunities = new List<DamageType> { DamageType.Fire },
            CreatureTags = new List<string> { "Elemental", "Fire", "Extraplanar", "SummonBase" },
            SpecialAbilities = new List<string> { "Elemental traits", "Burn (DC 11, 1d4 fire)", "Immunity to fire", "Vulnerability to cold (+50%)", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(1f, 0.55f, 0.22f, 1f),
            PanelColor = new Color(0.28f, 0.1f, 0.08f, 0.85f),
            NameColor = new Color(1f, 0.86f, 0.72f),
            Description = "Small Fire Elemental. Slam deals +1d4 fire (Burn). Immune to fire, vulnerable to cold. MM 3.5e p.98."
        });
    }

    private static void RegisterSummonCrocodile()
    {
        Register(new NPCDefinition
        {
            Id = "crocodile",
            Name = "Crocodile",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 19, DEX = 12, CON = 17, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 4,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 4, // 20 ft. land, swim 30 ft.
            BaseHitDieHP = 22,
            CreatureTags = new List<string> { "Animal", "Aquatic", "SummonBase" },
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Bite",
            HasScent = true,
            SpecialAbilities = new List<string> { "Improved grab", "Hold breath (68 rounds)", "Low-light vision", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.42f, 0.56f, 0.34f, 1f),
            PanelColor = new Color(0.12f, 0.2f, 0.1f, 0.85f),
            NameColor = new Color(0.84f, 0.93f, 0.78f),
            Description = "Crocodile with improved grab on bite. Hold breath 68 rounds. +8 Swim, +4 Hide in water. MM 3.5e p.271."
        });
    }

    private static void RegisterSummonBlackBear()
    {
        Register(new NPCDefinition
        {
            Id = "black_bear",
            Name = "Black Bear",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 19, DEX = 13, CON = 15, WIS = 12, INT = 2, CHA = 6,
            NaturalArmorBonus = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 8, // 40 ft.
            BaseHitDieHP = 19,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Low-light vision", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.28f, 0.2f, 0.13f, 1f),
            PanelColor = new Color(0.14f, 0.09f, 0.06f, 0.85f),
            NameColor = new Color(0.9f, 0.78f, 0.62f),
            Description = "Black bear. 2 claws +6 (1d4+4), bite +1 (1d6+2). +4 racial Swim. MM 3.5e p.269."
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

    private static void RegisterSummonDireBadger()
    {
        Register(new NPCDefinition
        {
            Id = "dire_badger",
            Name = "Dire Badger",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 14, DEX = 17, CON = 19, WIS = 12, INT = 2, CHA = 10,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 6, // 30 ft., burrow 10 ft.
            BaseHitDieHP = 28,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Rage (+4 Str, +4 Con, −2 AC when damaged)", "Low-light vision", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.5f, 0.45f, 0.43f, 1f),
            PanelColor = new Color(0.2f, 0.17f, 0.16f, 0.85f),
            NameColor = new Color(0.93f, 0.9f, 0.9f),
            Description = "Dire badger with rage when wounded (+4 Str/Con, −2 AC). MM 3.5e p.62."
        });
    }

    private static void RegisterSummonLargeShark()
    {
        Register(new NPCDefinition
        {
            Id = "large_shark",
            Name = "Large Shark",
            ChallengeRating = "2",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 4,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 17, DEX = 15, CON = 13, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 4,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8,
            BaseHitDieHP = 26,
            CreatureTags = new List<string> { "Animal", "Aquatic", "SummonBase" },
            HasScent = true,
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.62f, 0.7f, 0.78f, 1f),
            PanelColor = new Color(0.14f, 0.18f, 0.22f, 0.85f),
            NameColor = new Color(0.88f, 0.94f, 0.99f),
            Description = "Summon Monster baseline large shark."
        });
    }

    private static void RegisterSummonConstrictorSnake()
    {
        Register(new NPCDefinition
        {
            Id = "constrictor_snake",
            Name = "Constrictor Snake",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 17, DEX = 17, CON = 13, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 3, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 4, // 20 ft., climb 20 ft., swim 20 ft.
            BaseHitDieHP = 19,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Bite",
            HasScent = true,
            SpecialAbilities = new List<string> { "Constrict (1d3+4)", "Improved grab", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.56f, 0.64f, 0.28f, 1f),
            PanelColor = new Color(0.2f, 0.23f, 0.1f, 0.85f),
            NameColor = new Color(0.9f, 0.95f, 0.74f),
            Description = "Constrictor snake. Bite +5 (1d3+4), improved grab, constrict 1d3+4. MM 3.5e p.280."
        });
    }

    // ========================================
    // SUMMON MONSTER III — New Creatures
    // ========================================

    private static void RegisterSummonBison()
    {
        Register(new NPCDefinition
        {
            Id = "bison",
            Name = "Bison",
            ChallengeRating = "2",
            Level = 5,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 5,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 22, DEX = 10, CON = 16, WIS = 11, INT = 2, CHA = 4,
            NaturalArmorBonus = 4,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Gore", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8, // 40 ft.
            BaseHitDieHP = 37,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Stampede (1d12 per 5 bison, Ref DC 18 half)", "Low-light vision", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.55f, 0.42f, 0.28f, 1f),
            PanelColor = new Color(0.22f, 0.16f, 0.1f, 0.85f),
            NameColor = new Color(0.92f, 0.82f, 0.68f),
            Description = "Bison. Gore +8 (1d8+9). Stampede ability. MM 3.5e p.270."
        });
    }

    private static void RegisterSummonBoar()
    {
        Register(new NPCDefinition
        {
            Id = "boar",
            Name = "Boar",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 15, DEX = 10, CON = 17, WIS = 13, INT = 2, CHA = 4,
            NaturalArmorBonus = 6,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Gore", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8, // 40 ft.
            BaseHitDieHP = 25,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Ferocity (fights below 0 hp until −10)", "Low-light vision", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.52f, 0.38f, 0.3f, 1f),
            PanelColor = new Color(0.2f, 0.14f, 0.1f, 0.85f),
            NameColor = new Color(0.9f, 0.8f, 0.72f),
            Description = "Boar with ferocity — continues fighting below 0 HP until −10. Gore +4 (1d8+3). MM 3.5e p.270."
        });
    }

    private static void RegisterSummonDireWeasel()
    {
        Register(new NPCDefinition
        {
            Id = "dire_weasel",
            Name = "Dire Weasel",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 14, DEX = 19, CON = 10, WIS = 12, INT = 2, CHA = 11,
            NaturalArmorBonus = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8, // 40 ft.
            BaseHitDieHP = 13,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Attach (auto bite damage while latched)", "Blood drain (1d4 Con/round)", "Low-light vision", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.62f, 0.52f, 0.42f, 1f),
            PanelColor = new Color(0.22f, 0.18f, 0.14f, 0.85f),
            NameColor = new Color(0.92f, 0.86f, 0.78f),
            Description = "Dire weasel. Bite +6 (1d6+3), attach, blood drain 1d4 Con/round. Weapon Finesse. MM 3.5e p.65."
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

    private static void RegisterSummonLargeViper()
    {
        Register(new NPCDefinition
        {
            Id = "large_viper",
            Name = "Large Viper",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 3,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 10, DEX = 17, CON = 11, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                // Bite +4 melee (1d4 plus poison) — uses Weapon Finesse (Dex to attack)
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true, PoisonOnHitId = "large_viper_poison" }
            },
            BaseSpeed = 4, // 20 ft., climb 20 ft., swim 20 ft.
            BaseHitDieHP = 13,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Poison (Fort DC 11, 1d6 Con/1d6 Con)", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.45f, 0.55f, 0.3f, 1f),
            PanelColor = new Color(0.18f, 0.22f, 0.1f, 0.85f),
            NameColor = new Color(0.88f, 0.94f, 0.72f),
            Description = "Large viper snake. Bite +4 (1d4 + poison Fort DC 11, 1d6 Con/1d6 Con). Weapon Finesse. MM 3.5e p.280."
        });
    }

    private static void RegisterSummonHugeMonstruousCentipede()
    {
        Register(new NPCDefinition
        {
            Id = "huge_monstrous_centipede",
            Name = "Huge Monstrous Centipede",
            ChallengeRating = "2",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = 6,
            SizeCategory = SizeCategory.Huge,
            IsTallCreature = false,
            STR = 17, DEX = 15, CON = 12, WIS = 10, INT = CharacterStats.NO_SCORE, CHA = 2,
            NaturalArmorBonus = 6,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true, PoisonOnHitId = "huge_centipede_poison" }
            },
            BaseSpeed = 8, // 40 ft., climb 40 ft.
            BaseHitDieHP = 33,
            IsMindless = true,
            CreatureTags = new List<string> { "Vermin", "SummonBase" },
            Immunities = new CreatureImmunities { immuneToMindAffecting = true },
            SpecialAbilities = new List<string> { "Poison (Fort DC 14, 1d6 Dex/1d6 Dex)", "Darkvision 60 ft.", "Vermin traits (mindless)" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.45f, 0.28f, 0.22f, 1f),
            PanelColor = new Color(0.2f, 0.1f, 0.08f, 0.85f),
            NameColor = new Color(0.9f, 0.72f, 0.65f),
            Description = "Huge monstrous centipede. Bite +5 (2d6+4 + poison Fort DC 14, 1d6 Dex). 15 ft. space, 10 ft. reach. MM 3.5e p.286."
        });
    }

    private static void RegisterSummonSmallEarthElemental()
    {
        Register(new NPCDefinition
        {
            Id = "small_earth_elemental",
            Name = "Small Earth Elemental",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Elemental",
            HitDice = 2,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 17, DEX = 8, CON = 13, WIS = 11, INT = 4, CHA = 11,
            NaturalArmorBonus = 7,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 4, // 20 ft.
            BaseHitDieHP = 11,
            Immunities = new CreatureImmunities
            {
                immuneToPoison = true,
                immuneToCriticalHits = true,
                immuneToSneakAttack = true
            },
            CreatureTags = new List<string> { "Elemental", "Earth", "Extraplanar", "SummonBase" },
            SpecialAbilities = new List<string> { "Elemental traits", "Earth mastery (+1 atk/dmg when grounded)", "Push (bull rush, no AoO)", "Earth glide", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.58f, 0.48f, 0.32f, 1f),
            PanelColor = new Color(0.24f, 0.18f, 0.1f, 0.85f),
            NameColor = new Color(0.92f, 0.84f, 0.68f),
            Description = "Small Earth Elemental. Slam +5 (1d6+4). Earth mastery, push, earth glide. MM 3.5e p.97."
        });
    }

    private static void RegisterSummonSmallWaterElemental()
    {
        Register(new NPCDefinition
        {
            Id = "small_water_elemental",
            Name = "Small Water Elemental",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Elemental",
            HitDice = 2,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 14, DEX = 10, CON = 13, WIS = 11, INT = 4, CHA = 11,
            NaturalArmorBonus = 6,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 4, // 20 ft., swim 90 ft.
            BaseHitDieHP = 11,
            Immunities = new CreatureImmunities
            {
                immuneToPoison = true,
                immuneToCriticalHits = true,
                immuneToSneakAttack = true
            },
            CreatureTags = new List<string> { "Elemental", "Water", "Extraplanar", "SummonBase" },
            SpecialAbilities = new List<string> { "Elemental traits", "Water mastery (+1 atk/dmg in water)", "Drench (extinguish fires)", "Vortex (DC 13, 1d4 dmg)", "Swim 90 ft.", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.32f, 0.55f, 0.78f, 1f),
            PanelColor = new Color(0.1f, 0.2f, 0.32f, 0.85f),
            NameColor = new Color(0.72f, 0.88f, 1f),
            Description = "Small Water Elemental. Slam +4 (1d6+3). Water mastery, drench, vortex. MM 3.5e p.100."
        });
    }

    private static void RegisterSummonHippogriff()
    {
        Register(new NPCDefinition
        {
            Id = "hippogriff",
            Name = "Hippogriff",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            HitDice = 3,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 18, DEX = 15, CON = 16, WIS = 13, INT = 2, CHA = 8,
            NaturalArmorBonus = 4,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 10, // 50 ft., fly 100 ft. (average)
            BaseHitDieHP = 25,
            CreatureTags = new List<string> { "Magical Beast", "SummonBase" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Darkvision 60 ft.", "Low-light vision", "Scent", "Fly 100 ft. (average)" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.72f, 0.62f, 0.48f, 1f),
            PanelColor = new Color(0.26f, 0.2f, 0.14f, 0.85f),
            NameColor = new Color(0.94f, 0.88f, 0.78f),
            Description = "Hippogriff. 2 claws +6 (1d4+4), bite +1 (1d8+2). Fly 100 ft. (average). +4 Spot in daylight. MM 3.5e p.152."
        });
    }

    private static void RegisterSummonHellHound()
    {
        Register(new NPCDefinition
        {
            Id = "hell_hound",
            Name = "Hell Hound",
            ChallengeRating = "3",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            HitDice = 4,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 13, DEX = 13, CON = 13, WIS = 10, INT = 6, CHA = 6,
            NaturalArmorBonus = 5,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                // Bite 1d8+1 plus 1d6 fire (fire damage is special ability)
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8, // 40 ft.
            BaseHitDieHP = 22,
            Immunities = new CreatureImmunities
            {
                immuneToFire = true
            },
            DamageImmunities = new List<DamageType> { DamageType.Fire },
            CreatureTags = new List<string> { "Outsider", "Evil", "Extraplanar", "Fire", "Lawful", "SummonBase" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Breath weapon (10-ft. cone, 2d6 fire, Ref DC 13 half, 1/2d4 rds)", "Fiery bite (+1d6 fire)", "Immunity to fire", "Vulnerability to cold (+50%)", "Darkvision 60 ft.", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.82f, 0.28f, 0.15f, 1f),
            PanelColor = new Color(0.32f, 0.08f, 0.05f, 0.85f),
            NameColor = new Color(1f, 0.65f, 0.45f),
            Description = "Hell hound. Bite +5 (1d8+1 + 1d6 fire). Breath weapon 2d6 fire cone. Immune to fire, vulnerable to cold. MM 3.5e p.151."
        });
    }

    private static void RegisterSummonDretch()
    {
        Register(new NPCDefinition
        {
            Id = "dretch",
            Name = "Dretch",
            ChallengeRating = "2",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            HitDice = 2,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 12, DEX = 10, CON = 14, WIS = 11, INT = 5, CHA = 11,
            NaturalArmorBonus = 5,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 4, // 20 ft.
            BaseHitDieHP = 13,
            // DR 5/cold iron or good
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.ColdIron | DamageBypassTag.Good,
            SpellResistance = 5,
            Immunities = new CreatureImmunities
            {
                immuneToElectricity = true,
                immuneToPoison = true
            },
            DamageImmunities = new List<DamageType> { DamageType.Electricity },
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Acid, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 10 }
            },
            CreatureTags = new List<string> { "Outsider", "Chaotic", "Demon", "Extraplanar", "Evil", "SummonBase" },
            SpecialAbilities = new List<string> { "DR 5/cold iron or good", "SR 5", "Immune: electricity, poison", "Resist: acid 10, cold 10, fire 10", "Spell-like: 1/day scare (DC 12), stinking cloud (DC 13)", "Summon demon (35% chance, 1 dretch)", "Telepathy 100 ft.", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.45f, 0.35f, 0.5f, 1f),
            PanelColor = new Color(0.18f, 0.12f, 0.22f, 0.85f),
            NameColor = new Color(0.85f, 0.75f, 0.92f),
            Description = "Dretch demon. 2 claws +4 (1d6+1), bite +2 (1d4). DR 5/cold iron or good. SR 5. Immune electricity/poison. Resist acid/cold/fire 10. MM 3.5e p.42."
        });
    }
}
