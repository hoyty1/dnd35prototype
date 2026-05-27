using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: C
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_C()
    {
        RegisterCloaker();
        RegisterCockatrice();
        RegisterMonstrousCentipedes();
    
        RegisterSummonCrocodile();
        RegisterSummonConstrictorSnake();
        RegisterCarrionCrawler();
        RegisterCelestialLion();
        RegisterCentipedeSwarm();
        RegisterChainDevil();
        RegisterChaosBeast();
        RegisterChoker();
        RegisterChuul();
        RegisterCheetah();
        RegisterChimera();
        RegisterCouatl();
    }

    /// <summary>
    /// Cloaker (CR 5) — MM 3.5e p.36. Aberration with engulf, moan, shadow shift.
    /// 6d8+18 HP (45), tail slap 1d6+5. Engulf = grapple wrap + bite 1d4+5.
    /// </summary>
    private static void RegisterCloaker()
    {
        Register(new NPCDefinition
        {
            Id = "cloaker",
            Name = "Cloaker",
            ChallengeRating = "5",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            HitDice = 6,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 19, DEX = 16, CON = 17, WIS = 15, INT = 14, CHA = 15,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Tail Slap", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 2, // 10 ft, fly 40 ft (average)
            BaseHitDieHP = 45,
            Engulf = new EngulfDefinition
            {
                ReflexSaveDC = 17,
                DamagePerRound = 8, // 1d4+5 bite while engulfed
                DamageType = DamageType.Bludgeoning,
                EscapeDC = 17
            },
            AuraAbility = new AuraAbilityDefinition
            {
                Name = "Moan",
                SaveDC = 15,
                IsWillSave = true,
                RangeFeet = 60,
                Effect = AuraEffectType.Frightened,
                DurationRounds = 2
            },
            CreatureTags = new List<string> { "Aberration", "MM35" },
            Feats = new List<string> { "Alertness", "Combat Reflexes", "Improved Initiative" },
            SpecialAbilities = new List<string> { "Engulf (wrap + bite)", "Moan (DC 15 Will, various effects, 60 ft.)", "Shadow shift (blur, mirror image, silent image)", "Darkvision 60 ft.", "Fly 40 ft. (average)" },
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.25f, 0.22f, 0.3f, 1f),
            PanelColor = new Color(0.1f, 0.08f, 0.15f, 0.85f),
            NameColor = new Color(0.55f, 0.5f, 0.7f),
            Description = "Cloaker (CR 5). Aberration. Tail slap, engulf wrap + bite, moan aura (fear/nausea/hold). MM 3.5e p.36."
        });
    }

    /// <summary>
    /// Cockatrice (CR 3) — MM 3.5e p.37. Small magical beast with petrification bite.
    /// 5d10 HP (27), bite 1d4-2 + petrification (Fort DC 12).
    /// </summary>
    private static void RegisterCockatrice()
    {
        Register(new NPCDefinition
        {
            Id = "cockatrice",
            Name = "Cockatrice",
            ChallengeRating = "3",
            Level = 5,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 5,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 6, DEX = 17, CON = 11, WIS = 13, INT = 2, CHA = 9,
            NaturalArmorBonus = 1,
            BAB = 5,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1,
                    BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = true,
                    PetrificationOnHitDC = 12
                }
            },
            BaseSpeed = 4, // 20 ft, fly 60 ft (poor)
            BaseHitDieHP = 27,
            CreatureTags = new List<string> { "Magical Beast", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Dodge", "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Petrification (DC 12 Fort or turned to stone)", "Darkvision 60 ft.", "Low-light vision", "Fly 60 ft. (poor)" },
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.75f, 0.6f, 0.35f, 1f),
            PanelColor = new Color(0.3f, 0.22f, 0.1f, 0.85f),
            NameColor = new Color(0.95f, 0.82f, 0.5f),
            Description = "Cockatrice (CR 3). Small magical beast. Bite + petrification (DC 12 Fort). Fly 60 ft. MM 3.5e p.37."
        });
    }

    private static void RegisterMonstrousCentipedes()
    {
        RegisterMonstrousCentipedeVariant("monstrous_centipede_tiny", "Monstrous Centipede (Tiny)", 1, SizeCategory.Tiny, 1, 15, 10, 3, 1, 20, 1, 3, 10, "1 Dex", "1 Dex");
        RegisterMonstrousCentipedeVariant("monstrous_centipede_small", "Monstrous Centipede (Small)", 1, SizeCategory.Small, 2, 15, 10, 5, 1, 30, 1, 4, 10, "1d2 Dex", "1d2 Dex");
        RegisterMonstrousCentipedeVariant("monstrous_centipede_medium", "Monstrous Centipede (Medium)", 1, SizeCategory.Medium, 4, 15, 10, 9, 2, 40, 1, 6, 10, "1d3 Dex", "1d3 Dex");
        RegisterMonstrousCentipedeVariant("monstrous_centipede_large", "Monstrous Centipede (Large)", 3, SizeCategory.Large, 13, 15, 10, 13, 3, 40, 1, 8, 11, "1d4 Dex", "1d4 Dex");
        RegisterMonstrousCentipedeVariant("monstrous_centipede_huge", "Monstrous Centipede (Huge)", 6, SizeCategory.Huge, 33, 15, 12, 17, 6, 40, 2, 6, 14, "1d6 Dex", "1d6 Dex");
        RegisterMonstrousCentipedeVariant("monstrous_centipede_gargantuan", "Monstrous Centipede (Gargantuan)", 12, SizeCategory.Gargantuan, 66, 15, 12, 23, 10, 40, 2, 8, 17, "1d8 Dex", "1d8 Dex");
        RegisterMonstrousCentipedeVariant("monstrous_centipede_colossal", "Monstrous Centipede (Colossal)", 24, SizeCategory.Colossal, 132, 13, 12, 27, 16, 40, 4, 6, 23, "2d6 Dex", "2d6 Dex");
    }

    private static void RegisterMonstrousCentipedeVariant(string id, string name, int hitDice, SizeCategory size, int hp, int dex, int con, int str, int naturalArmor, int speed, int damageCount, int damageDice, int poisonDc, string poisonInitial, string poisonSecondary)
    {
        Register(new NPCDefinition
        {
            Id = id,
            Name = name,
            ChallengeRating = id switch
            {
                "monstrous_centipede_tiny" => "1/8",
                "monstrous_centipede_small" => "1/4",
                "monstrous_centipede_medium" => "1/2",
                "monstrous_centipede_large" => "1",
                "monstrous_centipede_huge" => "2",
                "monstrous_centipede_gargantuan" => "6",
                "monstrous_centipede_colossal" => "8",
                _ => null
            },
            Level = Mathf.Max(1, hitDice),
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = Mathf.Max(1, hitDice),
            SizeCategory = size,
            IsTallCreature = false,
            STR = str, DEX = dex, CON = con, WIS = 10, INT = 1, CHA = 2,
            NaturalArmorBonus = naturalArmor,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = damageDice, DamageCount = damageCount, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = speed / 5,
            BaseHitDieHP = hp,
            CreatureTags = new List<string> { "Vermin", "MM35" },
            Feats = new List<string> { "Weapon Finesse" },
            SpecialAbilities = new List<string>
            {
                $"Poison (Fort DC {poisonDc}; initial {poisonInitial}; secondary {poisonSecondary})",
                "Climb speed equals land speed",
                "Darkvision 60 ft",
                "Vermin traits",
                "Mindless"
            },
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.68f, 0.2f, 0.16f, 1f),
            PanelColor = new Color(0.22f, 0.08f, 0.07f, 0.85f),
            NameColor = new Color(0.96f, 0.78f, 0.74f),
            Description = $"Monster Manual {name.ToLowerInvariant()}. Poisonous vermin striker with climb mobility and vermin immunities."
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
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.42f, 0.56f, 0.34f, 1f),
            PanelColor = new Color(0.12f, 0.2f, 0.1f, 0.85f),
            NameColor = new Color(0.84f, 0.93f, 0.78f),
            Description = "Crocodile with improved grab on bite. Hold breath 68 rounds. +8 Swim, +4 Hide in water. MM 3.5e p.271."
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
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.56f, 0.64f, 0.28f, 1f),
            PanelColor = new Color(0.2f, 0.23f, 0.1f, 0.85f),
            NameColor = new Color(0.9f, 0.95f, 0.74f),
            Description = "Constrictor snake. Bite +5 (1d3+4), improved grab, constrict 1d3+4. MM 3.5e p.280."
        });
    }
    
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
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.5f, 0.55f, 0.4f, 1f),
            PanelColor = new Color(0.16f, 0.2f, 0.1f, 0.85f),
            NameColor = new Color(0.75f, 0.82f, 0.6f),
            Description = "Carrion Crawler (CR 4). Multi-tentacled paralyzer. MM 3.5e p.30."
        });
    }

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
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.7f, 0.65f, 0.45f, 1f),
            PanelColor = new Color(0.25f, 0.22f, 0.12f, 0.85f),
            NameColor = new Color(0.92f, 0.88f, 0.65f),
            Description = "Celestial Lion (CR 4). Holy lion with smite evil and pounce. MM 3.5e p.31 + Lion."
        });
    }

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
            AIProfileArchetype = NPCAIProfileArchetype.IndiscriminateSwarm,
            SpriteColor = new Color(0.35f, 0.3f, 0.25f, 1f),
            PanelColor = new Color(0.1f, 0.08f, 0.04f, 0.85f),
            NameColor = new Color(0.58f, 0.5f, 0.4f),
            Description = "Centipede Swarm (CR 4). Venomous vermin swarm. MM 3.5e p.238."
        });
    }

    private static void RegisterChainDevil()
    {
        Register(new NPCDefinition
        {
            Id = "chain_devil",
            Name = "Chain Devil",
            ChallengeRating = "6",
            Level = 8,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 8,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 15, DEX = 15, CON = 15, WIS = 10, INT = 6, CHA = 12,
            NaturalArmorBonus = 8,
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Silver | DamageBypassTag.Good,
            SpellResistance = 18,
            RegenerationAmount = 2,
            BaseSpeed = 6,
            BaseHitDieHP = 52,
            BAB = 8,
            Immunities = new CreatureImmunities { immuneToFire = true },
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 }
            },
            AuraAbility = new AuraAbilityDefinition
            {
                Name = "Unnerving Gaze",
                SaveDC = 15,
                IsWillSave = true,
                RangeFeet = 30,
                Effect = AuraEffectType.Sickened,
                DurationRounds = 10
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Chain", DamageDice = 4, DamageCount = 2, Count = 4, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Outsider", "Evil", "Lawful", "Extraplanar", "Baatezu", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Improved Initiative", "Iron Will" },
            SpecialAbilities = new List<string> { "Dancing Chains (Su): animate up to 4 chains within 20 ft.", "Unnerving Gaze (Su): 30 ft., Will DC 15 or sickened 1d3 rounds", "Regeneration 2 (silver or good weapons deal lethal)", "DR 5/silver or good", "SR 18", "Immune to fire", "Resist cold 10", "See in Darkness (Su)", "Darkvision 60 ft." },
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.4f, 0.35f, 0.35f, 1f),
            PanelColor = new Color(0.12f, 0.1f, 0.1f, 0.85f),
            NameColor = new Color(0.65f, 0.55f, 0.55f),
            Description = "Chain Devil (CR 6). Devil wrapped in animated chains with unnerving gaze. MM 3.5e p.53."
        });
    }

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
            AIProfileArchetype = NPCAIProfileArchetype.Berserk,
            SpriteColor = new Color(0.6f, 0.35f, 0.55f, 1f),
            PanelColor = new Color(0.22f, 0.1f, 0.2f, 0.85f),
            NameColor = new Color(0.85f, 0.5f, 0.78f),
            Description = "Chaos Beast (CR 7). Amorphous outsider that dissolves victims' forms. MM 3.5e p.33."
        });
    }

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
            AIProfileArchetype = NPCAIProfileArchetype.Grappler,
            SpriteColor = new Color(0.5f, 0.4f, 0.45f, 1f),
            PanelColor = new Color(0.18f, 0.12f, 0.15f, 0.85f),
            NameColor = new Color(0.75f, 0.6f, 0.7f),
            Description = "Choker (CR 2). Small aberration with tentacle grab and extra action per round. MM 3.5e p.35."
        });
    }

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
            AIProfileArchetype = NPCAIProfileArchetype.Grappler,
            SpriteColor = new Color(0.35f, 0.5f, 0.45f, 1f),
            PanelColor = new Color(0.1f, 0.18f, 0.15f, 0.85f),
            NameColor = new Color(0.55f, 0.78f, 0.7f),
            Description = "Chuul (CR 7). Lobster-like aberration with paralytic tentacles and constrict. MM 3.5e p.35."
        });
    }

    /// <summary>
    /// Cheetah (CR 2) — Medium animal.
    /// MM 3.5e p.271. Sprint ability, trip on bite.
    /// 3d8+6 HP (19), bite 1d6+3, 2 claws 1d2+1.
    /// </summary>
    private static void RegisterCheetah()
    {
        Register(new NPCDefinition
        {
            Id = "cheetah",
            Name = "Cheetah",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 16, DEX = 19, CON = 15, WIS = 12, INT = 2, CHA = 6,
            NaturalArmorBonus = 0,
            BaseSpeed = 10, // 50 ft
            BaseHitDieHP = 19,
            BAB = 2,
            HasTripAttack = true,
            TripAttackCheckBonus = 2,
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 2, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Animal", "MM35" },
            Feats = new List<string> { "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Sprint (Ex): 10x speed 1/hour", "Trip (Ex): free trip on bite", "Scent", "Low-light vision" },
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.85f, 0.75f, 0.45f, 1f),
            PanelColor = new Color(0.3f, 0.25f, 0.1f, 0.85f),
            NameColor = new Color(0.95f, 0.88f, 0.55f),
            Description = "Cheetah (CR 2). Fastest land animal with sprint ability and trip attack. MM 3.5e p.271."
        });
    }

    /// <summary>
    /// Chimera (CR 7) — Large magical beast.
    /// MM 3.5e p.34. Three-headed monster: lion, dragon, goat. Breath weapon + melee.
    /// </summary>
    private static void RegisterChimera()
    {
        Register(new NPCDefinition
        {
            Id = "chimera",
            Name = "Chimera",
            ChallengeRating = "7",
            Level = 9,
            CharacterClass = "Warrior",
            CreatureType = "MagicalBeast",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 9,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 19, DEX = 13, CON = 17, WIS = 13, INT = 4, CHA = 10,
            NaturalArmorBonus = 6,
            BaseSpeed = 6, // 30 ft (also fly 50 ft poor)
            BaseHitDieHP = 76,
            BAB = 9,
            BreathWeapon = new BreathWeaponDefinition
            {
                Shape = BreathWeaponShape.Cone,
                RangeFeet = 20,
                DamageDice = 8, DamageCount = 3,
                DamageType = DamageType.Fire,
                SaveDC = 17,
                IsReflexSave = true,
                RechargeRounds = 3
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Bite (lion)", DamageDice = 8, DamageCount = 2, Count = 1,
                    BonusDamageSource = DamageBonusSource.Strength,
                    Range = 1, IsPrimary = true
                },
                new NaturalAttackDefinition
                {
                    Name = "Bite (dragon)", DamageDice = 8, DamageCount = 2, Count = 1,
                    BonusDamageSource = DamageBonusSource.StrengthHalf,
                    Range = 1, IsPrimary = false
                },
                new NaturalAttackDefinition
                {
                    Name = "Gore (goat)", DamageDice = 8, DamageCount = 1, Count = 1,
                    BonusDamageSource = DamageBonusSource.StrengthHalf,
                    Range = 1, IsPrimary = false
                }
            },
            CreatureTags = new List<string> { "MagicalBeast", "Darkvision60", "LowLightVision", "HasScent", "MM35" },
            Feats = new List<string> { "Alertness", "Hover", "Iron Will", "Multiattack" },
            HasScent = true,
            SpecialAbilities = new List<string>
            {
                "Breath Weapon (Su): 20 ft. cone of fire, 3d8, Ref DC 17 half, 1d4 round recharge",
                "Three heads: bite (lion), bite (dragon), gore (goat)",
                "Fly 50 ft. (poor)",
                "Darkvision 60 ft., Low-light vision, Scent"
            },
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.65f, 0.35f, 0.2f, 1f),
            PanelColor = new Color(0.25f, 0.1f, 0.05f, 0.85f),
            NameColor = new Color(0.9f, 0.5f, 0.3f),
            Description = "Chimera (CR 7). Three-headed monster (lion/dragon/goat) with fire breath and flight. MM 3.5e p.34."
        });
    }

    /// <summary>
    /// Couatl (CR 10) — Large outsider (native).
    /// MM 3.5e p.37. Winged serpent with constriction, poison, and divine spells.
    /// </summary>
    private static void RegisterCouatl()
    {
        Register(new NPCDefinition
        {
            Id = "couatl",
            Name = "Couatl",
            ChallengeRating = "10",
            Level = 9,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.LawfulGood,
            HitDice = 9,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false, // serpentine
            STR = 16, DEX = 16, CON = 17, WIS = 17, INT = 17, CHA = 17,
            NaturalArmorBonus = 7,
            BaseSpeed = 4, // 20 ft (fly 60 ft good)
            BaseHitDieHP = 58,
            BAB = 9,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Bite", DamageDice = 3, DamageCount = 1, Count = 1,
                    BonusDamageSource = DamageBonusSource.Strength,
                    Range = 1, IsPrimary = true,
                    PoisonOnHitId = "wyvern_poison" // reuse similar venom; couatl poison is DC 16, 2d4 Str
                }
            },
            HasImprovedGrab = true,
            CreatureTags = new List<string> { "Outsider", "Native", "Darkvision60", "Constrict", "MM35" },
            Feats = new List<string> { "Dodge", "Empower Spell", "Eschew Materials", "Hover" },
            SpecialAbilities = new List<string>
            {
                "Constrict (Ex): 2d8+3 damage with successful grapple",
                "Improved Grab (Ex): bite triggers grapple, then constrict",
                "Poison (Ex): bite, Fort DC 16, 2d4 Str/4d4 Str",
                "Spells: casts as 9th-level sorcerer",
                "Spell-Like: detect chaos/evil/good/law, detect thoughts, invisibility, plane shift",
                "Telepathy 90 ft.",
                "Ethereal Jaunt (Su): at will, as spell",
                "Fly 60 ft. (good)",
                "Darkvision 60 ft."
            },
            AIProfileArchetype = NPCAIProfileArchetype.Caster,
            SpriteColor = new Color(0.3f, 0.7f, 0.9f, 1f),
            PanelColor = new Color(0.08f, 0.25f, 0.4f, 0.85f),
            NameColor = new Color(0.5f, 0.9f, 1f),
            Description = "Couatl (CR 10). Winged serpent outsider. Constricts, poisons, and casts divine spells. Telepathic. MM 3.5e p.37."
        });
    }

}
