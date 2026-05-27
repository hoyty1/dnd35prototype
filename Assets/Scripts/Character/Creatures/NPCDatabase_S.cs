using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: S
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_S()
    {
        RegisterShadow();
        RegisterSpiderSwarm();
        RegisterStirge();
        RegisterMonstrousScorpions();
        RegisterMonstrousSpiders();
    
        RegisterSummonSmallViper();
        RegisterShadowMastiff();
        RegisterShockerLizard();
        RegisterShrieker();
        RegisterSkum();
        RegisterSpectre();
        RegisterStoneGiant();
        RegisterSuccubus();
        RegisterSvirfneblin();
        RegisterSatyr();

    }

    /// <summary>
    /// Shadow (CR 3) — MM 3.5e p.221. Incorporeal undead, Str drain touch attack.
    /// 3d12 HP (19), incorporeal touch +3 (1d6 Str drain). Undead traits, +2 deflection AC.
    /// </summary>
    private static void RegisterShadow()
    {
        Register(new NPCDefinition
        {
            Id = "shadow",
            Name = "Shadow",
            ChallengeRating = "3",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Undead",
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 0, DEX = 14, CON = 0, WIS = 12, INT = 6, CHA = 13,
            NaturalArmorBonus = 0, // Incorporeal — uses deflection from CHA
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Incorporeal Touch", DamageDice = 6, DamageCount = 1, Count = 1,
                    BonusDamageSource = DamageBonusSource.None, Range = 1, IsPrimary = true,
                    AbilityDrainType = AbilityType.STR, AbilityDrainAmount = 1
                }
            },
            BaseSpeed = 6, // Fly 40 ft (good)
            BaseHitDieHP = 19,
            IsIncorporeal = true,
            IsMindless = false,
            DamageImmunities = new List<DamageType>(),
            CreatureTags = new List<string> { "Undead", "Incorporeal", "MM35" },
            Feats = new List<string> { "Dodge" },
            SpecialAbilities = new List<string> { "Incorporeal", "Str drain (1d6)", "Darkvision 60 ft.", "Undead traits", "+2 turn resistance", "Fly 40 ft. (good)" },
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.3f, 0.3f, 0.35f, 0.6f),
            PanelColor = new Color(0.1f, 0.1f, 0.15f, 0.85f),
            NameColor = new Color(0.6f, 0.6f, 0.75f),
            Description = "Shadow (CR 3). Incorporeal undead. Touch attack drains 1d6 STR. 50% miss chance vs corporeal attacks. MM 3.5e p.221."
        });
    }

    /// <summary>
    /// Stirge (CR 1/2) — MM 3.5e p.236. Tiny blood-draining vermin.
    /// 1d10 HP (5), touch attack +7 (attach), blood drain 1d4 Con/round.
    /// </summary>
    private static void RegisterStirge()
    {
        Register(new NPCDefinition
        {
            Id = "stirge",
            Name = "Stirge",
            ChallengeRating = "1/2",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 1,
            SizeCategory = SizeCategory.Tiny,
            IsTallCreature = false,
            STR = 3, DEX = 19, CON = 10, WIS = 12, INT = 1, CHA = 6,
            NaturalArmorBonus = 0,
            BAB = 0,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Touch", DamageDice = 0, DamageCount = 0, Count = 1,
                    BonusDamageSource = DamageBonusSource.None, Range = 1, IsPrimary = true,
                    HasBloodDrain = true, BloodDrainConDamagePerRound = 1
                }
            },
            BaseSpeed = 2, // 10 ft, fly 40 ft (average)
            BaseHitDieHP = 5,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Touch",
            CreatureTags = new List<string> { "Magical Beast", "Darkvision60", "MM35" },
            Feats = new List<string> { "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Attach (improved grab)", "Blood drain (1d4 Con/round)", "Darkvision 60 ft.", "Low-light vision", "Fly 40 ft. (average)" },
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.55f, 0.25f, 0.25f, 1f),
            PanelColor = new Color(0.22f, 0.08f, 0.08f, 0.85f),
            NameColor = new Color(0.9f, 0.5f, 0.5f),
            Description = "Stirge (CR 1/2). Tiny blood-draining flyer. Attaches and drains 1d4 CON per round. MM 3.5e p.236."
        });
    }

    private static void RegisterSpiderSwarm()
    {
        Register(new NPCDefinition
        {
            Id = "spider_swarm",
            Name = "Spider Swarm",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = 2,
            BaseAttackBonusOverride = 1, // Monster Manual BAB +1
            SizeCategory = SizeCategory.Large, // Swarm occupies 2×2 space in this prototype
            IsTallCreature = false,
            STR = 1, DEX = 17, CON = 10, WIS = 10, INT = CharacterStats.NO_SCORE, CHA = 2,
            NaturalArmorBonus = 0, // AC 17 = 10 +4 size +3 Dex
            BaseSpeed = 4, // 20 ft
            BaseHitDieHP = 9,
            CreatureTags = new List<string> { "Vermin", "Swarm", "MM35" },
            IsMindless = true,
            IsSwarm = true,
            CanMakeAttacksOfOpportunity = false,
            Immunities = ImmunityPresets.Combine(
                ImmunityPresets.SwarmImmunities(),
                ImmunityPresets.MindlessImmunities()),
            SwarmTraits = new SwarmTraits
            {
                IsSwarm = true,
                SwarmDamage = 6,
                SwarmDamageDice = "1d6",
                SwarmDamageType = DamageType.Piercing,
                DistractionDC = 11,
                HasPoison = true,
                PoisonId = "medium_spider_poison",
                PoisonDcModifier = -1 // Medium spider poison base DC 12 -> DC 11 for spider swarm
            },
            SpecialAbilities = new List<string>
            {
                "Swarm attack (1d6)",
                "Distraction (Fort DC 11)",
                "Poison (Fort DC 11; initial 1d3 Str; secondary 1d3 Str)",
                "Darkvision 60 ft",
                "Tremorsense 30 ft",
                "Climb 20 ft",
                "Vermin traits",
                "Mindless (Intelligence —)",
                "Swarm traits",
                "Alignment: True Neutral"
            },
            AIProfileArchetype = NPCAIProfileArchetype.Swarm,
            SpriteColor = new Color(0.18f, 0.18f, 0.18f, 1f),
            PanelColor = new Color(0.08f, 0.08f, 0.08f, 0.85f),
            NameColor = new Color(0.82f, 0.82f, 0.88f),
            Description = "Monster Manual spider swarm. Diminutive vermin swarm with toxic bite-cloud, tremorsense, and mindless vermin traits."
        });
    }

    private static void RegisterMonstrousScorpions()
    {
        RegisterMonstrousScorpionVariant("monstrous_scorpion_tiny", "Monstrous Scorpion (Tiny)", 1, SizeCategory.Tiny, 4, 10, 14, 3, 2, 20, 1, 2, 1, 2, 12, "1 Con", "1 Con");
        RegisterMonstrousScorpionVariant("monstrous_scorpion_small", "Monstrous Scorpion (Small)", 1, SizeCategory.Small, 6, 10, 14, 9, 3, 30, 1, 3, 1, 3, 12, "1d2 Con", "1d2 Con");
        RegisterMonstrousScorpionVariant("monstrous_scorpion_medium", "Monstrous Scorpion (Medium)", 2, SizeCategory.Medium, 13, 10, 14, 13, 4, 40, 1, 4, 1, 4, 13, "1d3 Con", "1d3 Con");
        RegisterMonstrousScorpionVariant("monstrous_scorpion_large", "Monstrous Scorpion (Large)", 5, SizeCategory.Large, 32, 10, 14, 19, 7, 50, 1, 6, 1, 6, 14, "1d4 Con", "1d4 Con");
        RegisterMonstrousScorpionVariant("monstrous_scorpion_huge", "Monstrous Scorpion (Huge)", 10, SizeCategory.Huge, 75, 10, 16, 23, 12, 50, 2, 4, 1, 8, 18, "1d6 Con", "1d6 Con");
        RegisterMonstrousScorpionVariant("monstrous_scorpion_gargantuan", "Monstrous Scorpion (Gargantuan)", 20, SizeCategory.Gargantuan, 150, 10, 16, 31, 18, 50, 2, 6, 2, 6, 23, "1d8 Con", "1d8 Con");
        RegisterMonstrousScorpionVariant("monstrous_scorpion_colossal", "Monstrous Scorpion (Colossal)", 40, SizeCategory.Colossal, 300, 8, 16, 35, 25, 50, 2, 8, 2, 8, 33, "1d10 Con", "1d10 Con");
    }

    private static void RegisterMonstrousScorpionVariant(string id, string name, int hitDice, SizeCategory size, int hp, int dex, int con, int str, int naturalArmor, int speed, int stingDamageCount, int stingDamageDice, int clawDamageCount, int clawDamageDice, int poisonDc, string poisonInitial, string poisonSecondary)
    {
        Register(new NPCDefinition
        {
            Id = id,
            Name = name,
            ChallengeRating = id switch
            {
                "monstrous_scorpion_tiny" => "1/4",
                "monstrous_scorpion_small" => "1/2",
                "monstrous_scorpion_medium" => "1",
                "monstrous_scorpion_large" => "3",
                "monstrous_scorpion_huge" => "7",
                "monstrous_scorpion_gargantuan" => "10",
                "monstrous_scorpion_colossal" => "12",
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
                new NaturalAttackDefinition { Name = "Claw", DamageDice = clawDamageDice, DamageCount = clawDamageCount, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Sting", DamageDice = stingDamageDice, DamageCount = stingDamageCount, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = speed / 5,
            BaseHitDieHP = hp,
            CreatureTags = new List<string> { "Vermin", "MM35" },
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Claw",
            SpecialAbilities = new List<string>
            {
                $"Poison (Fort DC {poisonDc}; initial {poisonInitial}; secondary {poisonSecondary})",
                "Constrict",
                "Improved Grab (claw)",
                "Darkvision 60 ft",
                "Tremorsense 60 ft",
                "Vermin traits",
                "Mindless"
            },
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.58f, 0.34f, 0.22f, 1f),
            PanelColor = new Color(0.2f, 0.12f, 0.08f, 0.85f),
            NameColor = new Color(0.94f, 0.82f, 0.72f),
            Description = $"Monster Manual {name.ToLowerInvariant()}. Armored vermin with pincer grab into venomous sting pressure."
        });
    }

    private static void RegisterMonstrousSpiders()
    {
        RegisterMonstrousSpiderVariant("monstrous_spider_tiny", "Monstrous Spider (Tiny)", 1, SizeCategory.Tiny, 2, 17, 10, 3, 0, 20, 1, 3, 10, "1d2 Str", "1d2 Str", 10);
        RegisterMonstrousSpiderVariant("monstrous_spider_small", "Monstrous Spider (Small)", 1, SizeCategory.Small, 4, 17, 10, 7, 0, 30, 1, 4, 10, "1d3 Str", "1d3 Str", 10);
        RegisterMonstrousSpiderVariant("monstrous_spider_medium", "Monstrous Spider (Medium)", 2, SizeCategory.Medium, 11, 17, 12, 11, 1, 30, 1, 6, 12, "1d4 Str", "1d4 Str", 12);
        RegisterMonstrousSpiderVariant("monstrous_spider_large", "Monstrous Spider (Large)", 4, SizeCategory.Large, 22, 17, 12, 15, 2, 30, 1, 8, 13, "1d6 Str", "1d6 Str", 13);
        RegisterMonstrousSpiderVariant("monstrous_spider_huge", "Monstrous Spider (Huge)", 8, SizeCategory.Huge, 52, 17, 14, 19, 5, 30, 2, 6, 16, "1d8 Str", "1d8 Str", 16);
        RegisterMonstrousSpiderVariant("monstrous_spider_gargantuan", "Monstrous Spider (Gargantuan)", 16, SizeCategory.Gargantuan, 104, 17, 14, 25, 10, 30, 2, 8, 20, "2d6 Str", "2d6 Str", 20);
        RegisterMonstrousSpiderVariant("monstrous_spider_colossal", "Monstrous Spider (Colossal)", 32, SizeCategory.Colossal, 208, 15, 14, 31, 18, 30, 4, 6, 28, "2d8 Str", "2d8 Str", 28);
    }

    private static void RegisterMonstrousSpiderVariant(string id, string name, int hitDice, SizeCategory size, int hp, int dex, int con, int str, int naturalArmor, int speed, int damageCount, int damageDice, int poisonDc, string poisonInitial, string poisonSecondary, int webDc)
    {
        // Map spider size to poison ID
        string poisonId = id switch
        {
            "monstrous_spider_medium" => "medium_spider_poison",
            "monstrous_spider_large" => "large_spider_poison",
            _ => null
        };

        Register(new NPCDefinition
        {
            Id = id,
            Name = name,
            ChallengeRating = id switch
            {
                "monstrous_spider_tiny" => "1/4",
                "monstrous_spider_small" => "1/2",
                "monstrous_spider_medium" => "1",
                "monstrous_spider_large" => "2",
                "monstrous_spider_huge" => "5",
                "monstrous_spider_gargantuan" => "8",
                "monstrous_spider_colossal" => "11",
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
                new NaturalAttackDefinition { Name = "Bite", DamageDice = damageDice, DamageCount = damageCount, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true, PoisonOnHitId = poisonId }
            },
            BaseSpeed = speed / 5,
            BaseHitDieHP = hp,
            CreatureTags = new List<string> { "Vermin", "MM35" },
            SpecialAbilities = new List<string>
            {
                $"Poison (Fort DC {poisonDc}; initial {poisonInitial}; secondary {poisonSecondary})",
                $"Web (Escape Artist/Strength DC {webDc})",
                "Tremorsense 60 ft",
                "Darkvision 60 ft",
                "Vermin traits",
                "Mindless"
            },
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.22f, 0.22f, 0.22f, 1f),
            PanelColor = new Color(0.1f, 0.1f, 0.1f, 0.85f),
            NameColor = new Color(0.86f, 0.86f, 0.9f),
            Description = $"Monster Manual {name.ToLowerInvariant()}. Web-spinning ambush vermin with toxic bite and tremorsense."
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
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.45f, 0.72f, 0.34f, 1f),
            PanelColor = new Color(0.15f, 0.23f, 0.12f, 0.85f),
            NameColor = new Color(0.84f, 0.94f, 0.8f),
            Description = "Summon Monster baseline Small Viper."
        });
    }
    
    private static void RegisterShadowMastiff()
    {
        Register(new NPCDefinition
        {
            Id = "shadow_mastiff",
            Name = "Shadow Mastiff",
            ChallengeRating = "5",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.NeutralEvil,
            HitDice = 4,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 17, DEX = 15, CON = 17, WIS = 12, INT = 4, CHA = 13,
            NaturalArmorBonus = 2,
            BaseSpeed = 10, // 50 ft
            BaseHitDieHP = 30,
            BAB = 4,
            HasTripAttack = true,
            TripAttackCheckBonus = 3,
            HasScent = true,
            AuraAbility = new AuraAbilityDefinition
            {
                Name = "Bay",
                SaveDC = 13,
                IsWillSave = true,
                RangeFeet = 300,
                Effect = AuraEffectType.Frightened,
                DurationRounds = 6
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Outsider", "Extraplanar", "ShadowBlend", "Darkvision60", "MM35" },
            Feats = new List<string> { "Dodge", "Improved Initiative" },
            SpecialAbilities = new List<string> { "Bay (Su): howl, 300 ft., Will DC 13 or panicked 2d4 rounds", "Trip (Ex): free trip on bite hit", "Shadow Blend (Su): total concealment in dim light except when near light", "Scent", "Darkvision 60 ft." },
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.2f, 0.2f, 0.25f, 1f),
            PanelColor = new Color(0.05f, 0.05f, 0.08f, 0.85f),
            NameColor = new Color(0.38f, 0.38f, 0.48f),
            Description = "Shadow Mastiff (CR 5). Extraplanar shadow hound with frightening bay and trip. MM 3.5e p.222."
        });
    }

    private static void RegisterShockerLizard()
    {
        Register(new NPCDefinition
        {
            Id = "shocker_lizard",
            Name = "Shocker Lizard",
            ChallengeRating = "2",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 2,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 10, DEX = 15, CON = 13, WIS = 12, INT = 2, CHA = 6,
            NaturalArmorBonus = 3,
            BaseSpeed = 8, // 40 ft, climb 20 ft, swim 20 ft
            BaseHitDieHP = 13,
            BAB = 2,
            Immunities = new CreatureImmunities { immuneToElectricity = true },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Magical Beast", "Darkvision60", "MM35" },
            Feats = new List<string> { "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Stunning Shock (Su): 5 ft., 1d8 nonlethal + Ref DC 12 or stunned 1 round", "Lethal Shock (Su): 3+ lizards within 20 ft., 2d8 elec/lizard, Ref DC 12 half", "Electricity Sense: 100 ft. range", "Immune to electricity", "Darkvision 60 ft., Low-light vision" },
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.4f, 0.55f, 0.65f, 1f),
            PanelColor = new Color(0.12f, 0.2f, 0.25f, 0.85f),
            NameColor = new Color(0.6f, 0.8f, 0.9f),
            Description = "Shocker Lizard (CR 2). Small lizard that deals electrical damage; lethal in groups. MM 3.5e p.224."
        });
    }

    private static void RegisterShrieker()
    {
        Register(new NPCDefinition
        {
            Id = "shrieker",
            Name = "Shrieker",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Plant",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 1, DEX = 1, CON = 13, WIS = 2, INT = 0, CHA = 1,
            NaturalArmorBonus = 3,
            IsMindless = true,
            BaseSpeed = 0, // Immobile
            BaseHitDieHP = 11,
            BAB = 1,
            CanMakeAttacksOfOpportunity = false,
            Immunities = new CreatureImmunities
            {
                immuneToMindAffecting = true,
                immuneToPoison = true,
                immuneToCriticalHits = true,
                immuneToSneakAttack = true
            },
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Plant", "Immobile", "MM35" },
            Feats = new List<string>(),
            SpecialAbilities = new List<string> { "Shriek (Ex): piercing sound for 1d3 rounds when movement within 10 ft., draws creatures", "Immobile: cannot move or attack", "Plant traits" },
            AIBehavior = NPCAIBehavior.DefensiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.UndeadMindless,
            SpriteColor = new Color(0.55f, 0.45f, 0.3f, 1f),
            PanelColor = new Color(0.2f, 0.15f, 0.08f, 0.85f),
            NameColor = new Color(0.8f, 0.68f, 0.48f),
            Description = "Shrieker (CR 1). Fungus that shrieks to alert nearby creatures. MM 3.5e p.112."
        });
    }

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
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.35f, 0.45f, 0.4f, 1f),
            PanelColor = new Color(0.1f, 0.16f, 0.12f, 0.85f),
            NameColor = new Color(0.55f, 0.72f, 0.65f),
            Description = "Skum (CR 2). Aquatic aberration servant of aboleths. MM 3.5e p.228."
        });
    }

    private static void RegisterSpectre()
    {
        Register(new NPCDefinition
        {
            Id = "spectre",
            Name = "Spectre",
            ChallengeRating = "7",
            Level = 7,
            CharacterClass = "Warrior",
            CreatureType = "Undead",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 7,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 0, DEX = 16, CON = 0, WIS = 14, INT = 14, CHA = 15,
            NaturalArmorBonus = 0,
            IsIncorporeal = true,
            BaseSpeed = 8, // Fly 40 ft (perfect)
            BaseHitDieHP = 45,
            BAB = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Incorporeal Touch", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.None, Range = 1, IsPrimary = true, EnergyDrainOnHit = 2, EnergyDrainRemovalDC = 15 }
            },
            CreatureTags = new List<string> { "Undead", "Incorporeal", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Blind-Fight", "Improved Initiative" },
            SpecialAbilities = new List<string> { "Energy Drain (Su): 2 negative levels per touch, DC 15 Fort", "Create Spawn: humanoids slain become spectres in 1d4 rounds", "Incorporeal", "Sunlight Powerlessness", "Unnatural Aura: 30 ft., animals panicked", "+2 turn resistance", "Darkvision 60 ft.", "Fly 40 ft. (perfect)" },
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.3f, 0.35f, 0.5f, 0.5f),
            PanelColor = new Color(0.08f, 0.1f, 0.2f, 0.85f),
            NameColor = new Color(0.5f, 0.6f, 0.85f),
            Description = "Spectre (CR 7). Powerful incorporeal undead with double energy drain. MM 3.5e p.232."
        });
    }

    private static void RegisterStoneGiant()
    {
        Register(new NPCDefinition
        {
            Id = "stone_giant",
            Name = "Stone Giant",
            ChallengeRating = "8",
            Level = 14,
            CharacterClass = "Warrior",
            CreatureType = "Giant",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 14,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 27, DEX = 15, CON = 19, WIS = 12, INT = 10, CHA = 11,
            NaturalArmorBonus = 11,
            BaseSpeed = 6, // 30 ft (40 ft -10 armor)
            BaseHitDieHP = 119,
            BAB = 10,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Giant", "RockThrowing", "Darkvision60", "MM35" },
            Feats = new List<string> { "Combat Reflexes", "Iron Will", "Point Blank Shot", "Power Attack", "Precise Shot" },
            SpecialAbilities = new List<string> { "Rock Throwing (Ex): 180 ft., 2d8+12", "Rock Catching (Ex): Ref DC 22", "Darkvision 60 ft.", "Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("greatclub", EquipSlot.MainHand),
                new EquipmentSlotPair("hide_armor", EquipSlot.Armor)
            },
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.6f, 0.58f, 0.55f, 1f),
            PanelColor = new Color(0.22f, 0.2f, 0.18f, 0.85f),
            NameColor = new Color(0.82f, 0.78f, 0.72f),
            Description = "Stone Giant (CR 8). Skilled rock thrower with impressive natural armor. MM 3.5e p.124."
        });
    }

    private static void RegisterSuccubus()
    {
        Register(new NPCDefinition
        {
            Id = "succubus",
            Name = "Succubus",
            ChallengeRating = "7",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 6,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 13, DEX = 13, CON = 13, WIS = 14, INT = 16, CHA = 26,
            NaturalArmorBonus = 9,
            DamageReductionAmount = 10,
            DamageReductionBypass = DamageBypassTag.ColdIron | DamageBypassTag.Good,
            SpellResistance = 18,
            BaseSpeed = 6, // 30 ft, fly 50 ft (average)
            BaseHitDieHP = 33,
            BAB = 6,
            Immunities = new CreatureImmunities { immuneToElectricity = true, immuneToPoison = true },
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Acid, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 10 }
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 3, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Outsider", "Chaotic", "Evil", "Extraplanar", "Tanarri", "Fly50", "Telepathy100", "Darkvision60", "MM35" },
            Feats = new List<string> { "Dodge", "Mobility", "Persuasive" },
            SpecialAbilities = new List<string> { "Energy Drain (Su): kiss, 1 negative level, Will DC 21 to resist", "Charm Monster (Sp): at will, DC 22", "Detect Good/Evil/Thoughts (Sp): at will", "Ethereal Jaunt (Sp): at will", "Suggestion (Sp): at will, DC 21", "Greater Teleport (Sp): at will, self + 50 lb.", "Polymorph (Sp): at will, humanoid form", "Tongues (Su): constant", "DR 10/cold iron or good", "SR 18", "Immune to electricity/poison", "Resist acid 10, cold 10, fire 10", "Darkvision 60 ft.", "Fly 50 ft." },
            AIProfileArchetype = NPCAIProfileArchetype.Spellcaster,
            SpriteColor = new Color(0.6f, 0.3f, 0.4f, 1f),
            PanelColor = new Color(0.25f, 0.08f, 0.12f, 0.85f),
            NameColor = new Color(0.9f, 0.48f, 0.6f),
            Description = "Succubus (CR 7). Seductive demon with energy draining kiss and charm. MM 3.5e p.47."
        });
    }

    private static void RegisterSvirfneblin()
    {
        Register(new NPCDefinition
        {
            Id = "svirfneblin",
            Name = "Svirfneblin",
            ChallengeRating = "1",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = true,
            STR = 11, DEX = 17, CON = 12, WIS = 11, INT = 10, CHA = 4,
            NaturalArmorBonus = 0,
            SpellResistance = 12,
            BaseSpeed = 4,
            BaseHitDieHP = 5,
            BAB = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Humanoid", "Gnome", "Svirfneblin", "MM35" },
            Feats = new List<string> { "Toughness" },
            SpecialAbilities = new List<string> { "Darkvision 120 ft.", "SR 12", "Nondetection (constant)", "Stonecunning", "+2 saves vs. illusions", "Blindsight 60 ft. (via exceptional hearing)", "Blur (Sp) 1/day", "Disguise Self (Sp) 1/day" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("heavy_pick", EquipSlot.MainHand),
                new EquipmentSlotPair("light_crossbow", EquipSlot.Ranged),
                new EquipmentSlotPair("chain_shirt", EquipSlot.Armor)
            },
            AIBehavior = NPCAIBehavior.DefensiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.55f, 0.52f, 0.58f, 1f),
            PanelColor = new Color(0.18f, 0.16f, 0.22f, 0.85f),
            NameColor = new Color(0.75f, 0.7f, 0.85f),
            Description = "Svirfneblin (CR 1). Deep gnome with spell resistance and nondetection. MM 3.5e p.132."
        });
    }

    /// <summary>
    /// Satyr (without pipes) (CR 2) — MM 3.5e p.219. Medium Fey, Chaotic Neutral.
    /// 5d6+5 HP (22), head butt +2 (1d6), DR 5/cold iron. 40 ft. speed.
    /// AC 15 (DEX +1, natural +4). Low-light vision. Summoned version has no pipes.
    /// </summary>
    private static void RegisterSatyr()
    {
        Register(new NPCDefinition
        {
            Id = "satyr",
            Name = "Satyr",
            ChallengeRating = "2",
            Level = 5,
            CharacterClass = "Warrior",
            CreatureType = "Fey",
            CharacterAlignment = Alignment.ChaoticNeutral,
            HitDice = 5,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 10, DEX = 13, CON = 12, WIS = 13, INT = 12, CHA = 13,
            BAB = 2,
            NaturalArmorBonus = 4,
            BaseSpeed = 8, // 40 ft.
            BaseHitDieHP = 22, // 5d6+5 = 5*3.5+5 = 22.5 → 22
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.ColdIron,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Head butt", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Fey", "MM35", "SummonBase" },
            Feats = new List<string> { "Alertness", "Dodge" },
            SpecialAbilities = new List<string>
            {
                "Damage reduction 5/cold iron",
                "Low-light vision",
                "Skills: Bluff +9, Hide +13, Knowledge (nature) +9, Listen +11, Move Silently +13, Perform +9, Spot +11",
                "No pipes (summoned version)"
            },
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.65f, 0.55f, 0.35f, 1f),
            PanelColor = new Color(0.22f, 0.18f, 0.1f, 0.85f),
            NameColor = new Color(0.85f, 0.75f, 0.5f),
            Description = "Satyr without pipes (CR 2). Goat-legged fey with DR 5/cold iron. Summoned version has no charming pipes. MM 3.5e p.219."
        });
    }

}
