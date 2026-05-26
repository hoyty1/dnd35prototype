using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: H
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_H()
    {
        RegisterHawk();
        RegisterHowler();
    
        RegisterSummonHellHound();
        RegisterSummonHippogriff();
        RegisterSummonHugeMonstruousCentipede();
        RegisterHalflingWarrior();
        RegisterHarpy();
        RegisterHellcat();
        RegisterHellwaspSwarm();
        RegisterHillGiant();
        RegisterHobgoblin();
        RegisterHoundArchon();
        RegisterHyena();

    }

    private static void RegisterHawk()
    {
        Register(new NPCDefinition
        {
            Id = "hawk",
            Name = "Hawk",
            ChallengeRating = "1/3",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 1,
            SizeCategory = SizeCategory.Tiny,
            IsTallCreature = false,
            STR = 4, DEX = 17, CON = 10, WIS = 14, INT = 2, CHA = 6,
            NaturalArmorBonus = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Talons", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 2,
            BaseHitDieHP = 4,
            CreatureTags = new List<string> { "Animal", "MM35", "Fly" },
            Feats = new List<string> { "Alertness", "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Low-light vision", "Fly 60 ft (average)", "Skills: Listen +4, Spot +16" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.62f, 0.52f, 0.38f, 1f),
            PanelColor = new Color(0.2f, 0.14f, 0.1f, 0.85f),
            NameColor = new Color(0.95f, 0.86f, 0.72f),
            Description = "Monster Manual hawk. Tiny raptor with high-accuracy talons and exceptional spotting capability."
        });
    }



    /// <summary>
    /// Howler (CR 3) — Large outsider (chaotic, evil, extraplanar).
    /// MM 3.5e p.154. 6 HD, quills deal damage to grapplers/attackers, howl causes fear.
    /// </summary>
    private static void RegisterHowler()
    {
        Register(new NPCDefinition
        {
            Id = "howler",
            Name = "Howler",
            ChallengeRating = "3",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            HitDice = 6,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 21, DEX = 17, CON = 15, WIS = 14, INT = 6, CHA = 8,
            BAB = 6,
            NaturalArmorBonus = 5,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Quills", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 12, // 60 ft.
            BaseHitDieHP = 39, // 6d8+12
            CreatureTags = new List<string> { "Outsider", "Chaotic", "Evil", "Extraplanar", "MM35" },
            Feats = new List<string> { "Alertness", "Improved Initiative", "Power Attack" },
            SpecialAbilities = new List<string>
            {
                "Howl (Will DC 12 or become affected with Wisdom damage; 1 round to take effect; repeated howls within 24 hrs auto-fail; each round exposed = 1 Wis damage)",
                "Quills (melee attackers take 1d6 quill damage, Ref DC 15 negates)",
                "Darkvision 60 ft.",
                "Skills: Climb +14, Hide +12, Listen +13, Move Silently +12, Search +7, Spot +13, Survival +11",
                "Alignment: Chaotic Evil"
            },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Berserk,
            SpriteColor = new Color(0.55f, 0.22f, 0.22f, 1f),
            PanelColor = new Color(0.22f, 0.08f, 0.08f, 0.85f),
            NameColor = new Color(0.95f, 0.6f, 0.6f),
            Description = "Monster Manual howler (CR 3). Bite +10 (1d8+5), quills +5 (1d6+2). Howl causes cumulative Wisdom damage. Fast outsider. MM 3.5e p.154."
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
            BreathWeapon = new BreathWeaponDefinition
            {
                Shape = BreathWeaponShape.Cone,
                RangeFeet = 10,
                DamageDice = 6,
                DamageCount = 2,
                DamageType = DamageType.Fire,
                SaveDC = 13,
                IsReflexSave = true,
                RechargeRounds = 3 // 1d4 rounds average
            },
            SpecialAbilities = new List<string> { "Breath weapon (10-ft. cone, 2d6 fire, Ref DC 13 half, 1/2d4 rds)", "Fiery bite (+1d6 fire)", "Immunity to fire", "Vulnerability to cold (+50%)", "Darkvision 60 ft.", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Dragon,
            SpriteColor = new Color(0.82f, 0.28f, 0.15f, 1f),
            PanelColor = new Color(0.32f, 0.08f, 0.05f, 0.85f),
            NameColor = new Color(1f, 0.65f, 0.45f),
            Description = "Hell hound (CR 3). Bite +5 (1d8+1 + 1d6 fire). Breath weapon 2d6 fire cone. Immune to fire, vulnerable to cold. MM 3.5e p.151."
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
    
    private static void RegisterHalflingWarrior()
    {
        Register(new NPCDefinition
        {
            Id = "halfling_warrior",
            Name = "Halfling Warrior",
            ChallengeRating = "1/2",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = true,
            STR = 11, DEX = 17, CON = 12, WIS = 9, INT = 10, CHA = 8,
            NaturalArmorBonus = 0,
            BaseSpeed = 4, // 20 ft
            BaseHitDieHP = 5,
            BAB = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Humanoid", "Halfling", "MM35" },
            Feats = new List<string> { "Weapon Focus (light crossbow)" },
            SpecialAbilities = new List<string> { "+2 morale saves vs. fear", "+1 attack with thrown/slings", "+1 size bonus to AC/attack" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("longsword", EquipSlot.MainHand),
                new EquipmentSlotPair("light_crossbow", EquipSlot.Ranged),
                new EquipmentSlotPair("chainmail", EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Ranged,
            SpriteColor = new Color(0.85f, 0.75f, 0.55f, 1f),
            PanelColor = new Color(0.3f, 0.25f, 0.15f, 0.85f),
            NameColor = new Color(0.95f, 0.85f, 0.65f),
            Description = "Halfling Warrior (CR 1/2). Small but brave humanoid fighter. MM 3.5e p.149."
        });
    }

    private static void RegisterHarpy()
    {
        Register(new NPCDefinition
        {
            Id = "harpy",
            Name = "Harpy",
            ChallengeRating = "4",
            Level = 7,
            CharacterClass = "Warrior",
            CreatureType = "Monstrous Humanoid",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 7,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 10, DEX = 13, CON = 10, WIS = 10, INT = 7, CHA = 17,
            NaturalArmorBonus = 2,
            BaseSpeed = 4, // 20 ft, fly 80 ft (average)
            BaseHitDieHP = 31,
            BAB = 7,
            AuraAbility = new AuraAbilityDefinition
            {
                Name = "Captivating Song",
                SaveDC = 16,
                IsWillSave = true,
                RangeFeet = 300,
                Effect = AuraEffectType.Fascinated,
                DurationRounds = 10
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 3, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Monstrous Humanoid", "Fly80", "Darkvision60", "MM35" },
            Feats = new List<string> { "Dodge", "Flyby Attack", "Persuasive" },
            SpecialAbilities = new List<string> { "Captivating Song (Su): 300 ft., Will DC 16 or captivated (approach, no new save while singing)", "Flight: 80 ft. (average)", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("club", EquipSlot.MainHand)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.55f, 0.45f, 0.4f, 1f),
            PanelColor = new Color(0.2f, 0.15f, 0.12f, 0.85f),
            NameColor = new Color(0.82f, 0.68f, 0.62f),
            Description = "Harpy (CR 4). Flying creature with captivating song that lures victims. MM 3.5e p.150."
        });
    }

    private static void RegisterHellcat()
    {
        Register(new NPCDefinition
        {
            Id = "hellcat",
            Name = "Hellcat",
            ChallengeRating = "7",
            Level = 10,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 10,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 21, DEX = 19, CON = 15, WIS = 14, INT = 10, CHA = 10,
            NaturalArmorBonus = 8,
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Good,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 65,
            BAB = 10,
            HasPounce = true,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Claw",
            HasRake = true,
            RakeAttack = new NaturalAttackDefinition { Name = "Rake", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false },
            HasScent = true,
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 10 }
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 2, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Outsider", "Evil", "Lawful", "Extraplanar", "Darkvision60", "MM35" },
            Feats = new List<string> { "Dodge", "Improved Initiative", "Lightning Reflexes", "Track" },
            SpecialAbilities = new List<string> { "Invisible in Light (Su): invisible in any light brighter than darkness", "Pounce", "Improved Grab", "Rake: 2 claws 1d6+2 each (grapple)", "DR 5/good", "Resist fire 10", "Scent", "See in Darkness (Su)", "Telepathy 100 ft.", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.6f, 0.4f, 0.25f, 1f),
            PanelColor = new Color(0.22f, 0.12f, 0.06f, 0.85f),
            NameColor = new Color(0.88f, 0.62f, 0.38f),
            Description = "Hellcat (CR 7). Fiendish feline invisible in light, with pounce and rake. MM 3.5e p.54."
        });
    }

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

    private static void RegisterHillGiant()
    {
        Register(new NPCDefinition
        {
            Id = "hill_giant",
            Name = "Hill Giant",
            ChallengeRating = "7",
            Level = 12,
            CharacterClass = "Warrior",
            CreatureType = "Giant",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 12,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 25, DEX = 8, CON = 19, WIS = 10, INT = 6, CHA = 7,
            NaturalArmorBonus = 9,
            BaseSpeed = 6, // 30 ft (40 ft base -10 armor)
            BaseHitDieHP = 102,
            BAB = 9,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Giant", "RockThrowing", "Darkvision60", "MM35" },
            Feats = new List<string> { "Cleave", "Improved Bull Rush", "Power Attack", "Improved Sunder", "Weapon Focus (greatclub)" },
            SpecialAbilities = new List<string> { "Rock Throwing (Ex): 120 ft., 2d6+7", "Rock Catching (Ex): Ref DC 20", "Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("greatclub", EquipSlot.MainHand),
                new EquipmentSlotPair("hide_armor", EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.55f, 0.45f, 0.32f, 1f),
            PanelColor = new Color(0.2f, 0.15f, 0.08f, 0.85f),
            NameColor = new Color(0.8f, 0.65f, 0.45f),
            Description = "Hill Giant (CR 7). Brutish giant with rock throwing. MM 3.5e p.123."
        });
    }

    private static void RegisterHobgoblin()
    {
        Register(new NPCDefinition
        {
            Id = "hobgoblin",
            Name = "Hobgoblin",
            ChallengeRating = "1/2",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 1,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 13, DEX = 13, CON = 14, WIS = 9, INT = 10, CHA = 8,
            NaturalArmorBonus = 0,
            BaseSpeed = 6,
            BaseHitDieHP = 6,
            BAB = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Humanoid", "Goblinoid", "MM35" },
            Feats = new List<string> { "Alertness" },
            SpecialAbilities = new List<string> { "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("longsword", EquipSlot.MainHand),
                new EquipmentSlotPair("light_steel_shield", EquipSlot.OffHand),
                new EquipmentSlotPair("studded_leather", EquipSlot.Armor)
            },
            BackpackItemIds = new List<string> { "javelin" },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.7f, 0.45f, 0.3f, 1f),
            PanelColor = new Color(0.25f, 0.15f, 0.08f, 0.85f),
            NameColor = new Color(0.9f, 0.6f, 0.4f),
            Description = "Hobgoblin (CR 1/2). Disciplined goblinoid warrior. MM 3.5e p.153."
        });
    }

    private static void RegisterHoundArchon()
    {
        Register(new NPCDefinition
        {
            Id = "hound_archon",
            Name = "Hound Archon",
            ChallengeRating = "4",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.LawfulGood,
            HitDice = 6,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 15, DEX = 10, CON = 13, WIS = 13, INT = 10, CHA = 12,
            NaturalArmorBonus = 9,
            DamageReductionAmount = 10,
            DamageReductionBypass = DamageBypassTag.Evil,
            SpellResistance = 16,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 33,
            BAB = 6,
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Outsider", "Archon", "Good", "Lawful", "Extraplanar", "Darkvision60", "MM35" },
            Feats = new List<string> { "Improved Initiative", "Power Attack" },
            SpecialAbilities = new List<string> { "Change Shape (Su): any canine form", "Detect Evil (Su): at will", "DR 10/evil", "SR 16", "Aura of Menace (Su): 20 ft., Will DC 16 or -2 attacks/AC/saves", "Aid (Sp): at will, Continual Flame (at will), Message (at will)", "Immune to electricity/petrification", "Magic Circle Against Evil (constant)", "+4 saves vs. poison", "Scent", "Darkvision 60 ft., Low-light vision" },
            Immunities = new CreatureImmunities { immuneToElectricity = true },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("greatsword", EquipSlot.MainHand),
                new EquipmentSlotPair("full_plate", EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.7f, 0.65f, 0.5f, 1f),
            PanelColor = new Color(0.25f, 0.22f, 0.15f, 0.85f),
            NameColor = new Color(0.92f, 0.88f, 0.7f),
            Description = "Hound Archon (CR 4). Dog-headed celestial warrior with aura of menace. MM 3.5e p.16."
        });
    }

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
}

}
