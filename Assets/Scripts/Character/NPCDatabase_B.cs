using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: B
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_B()
    {
        RegisterBadger();
        RegisterBatSwarm();
        RegisterBugbear();
        RegisterGiantBee();
        RegisterGiantBombardierBeetle();
    
        RegisterSummonBlackBear();
        RegisterSummonBison();
        RegisterSummonBoar();
        RegisterBabau();
        RegisterBarghest();
        RegisterBasilisk();
        RegisterBeardedDevil();
        RegisterBehir();
        RegisterBlackPudding();
        RegisterBlueSlaad();
        RegisterBodak();
        RegisterBralani();
        RegisterGreaterBarghest();

    }

    private static void RegisterBadger()
    {
        Register(new NPCDefinition
        {
            Id = "badger",
            Name = "Badger",
            ChallengeRating = "1/2",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 8, DEX = 17, CON = 15, WIS = 12, INT = 2, CHA = 6,
            NaturalArmorBonus = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 2, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 3, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 6,
            BaseHitDieHP = 6,
            CreatureTags = new List<string> { "Animal", "MM35", "Burrow" },
            Feats = new List<string> { "Agile", "Weapon Finesse", "Track" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Low-light vision", "Scent", "Rage (as barbarian)", "Burrow 10 ft", "Skills: Balance +5, Escape Artist +9, Listen +3, Spot +3" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.62f, 0.55f, 0.52f, 1f),
            PanelColor = new Color(0.22f, 0.19f, 0.18f, 0.85f),
            NameColor = new Color(0.93f, 0.9f, 0.9f),
            Description = "Monster Manual badger. Tunnel-capable skirmisher with claw/claw/bite routine and rage trait."
        });
    }

    private static void RegisterBatSwarm()
    {
        Register(new NPCDefinition
        {
            Id = "bat_swarm",
            Name = "Bat Swarm",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 3,
            BaseAttackBonusOverride = 2, // Monster Manual BAB +2
            SizeCategory = SizeCategory.Large, // Swarm occupies 2×2 space in this prototype
            IsTallCreature = false,
            STR = 3, DEX = 15, CON = 10, WIS = 12, INT = 2, CHA = 4,
            NaturalArmorBonus = 0, // AC 16 = 10 +4 size +2 Dex
            BaseSpeed = 1, // 5 ft
            BaseHitDieHP = 13,
            CreatureTags = new List<string> { "Animal", "Swarm", "MM35", "Fly" },
            IsSwarm = true,
            CanMakeAttacksOfOpportunity = false,
            Immunities = ImmunityPresets.SwarmImmunities(),
            SwarmTraits = new SwarmTraits
            {
                IsSwarm = true,
                SwarmDamage = 6,
                SwarmDamageDice = "1d6",
                SwarmDamageType = DamageType.Piercing,
                DistractionDC = 11,
                HasWounding = true
            },
            SpecialAbilities = new List<string>
            {
                "Swarm attack (1d6)",
                "Distraction (Fort DC 11)",
                "Wounding (bleeding when reduced to 0 hp)",
                "Blindsense 20 ft",
                "Fly 40 ft (good)",
                "Swarm traits",
                "Alignment: True Neutral"
            },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Swarm,
            SpriteColor = new Color(0.3f, 0.3f, 0.34f, 1f),
            PanelColor = new Color(0.12f, 0.12f, 0.16f, 0.85f),
            NameColor = new Color(0.86f, 0.86f, 0.92f),
            Description = "Monster Manual bat swarm. Diminutive animal swarm with blindsense, distraction, and wounding blood-loss pressure."
        });
    }

    /// <summary>
    /// Bugbear (CR 2) — MM 3.5e p.29. Goblinoid brute with natural armor and stealth.
    /// 3d8+3 HP (16), morningstar 1d8+2 or javelin 1d6+2. AC 17 (+1 Dex, +3 natural, +2 leather, +1 shield).
    /// </summary>
    private static void RegisterBugbear()
    {
        Register(new NPCDefinition
        {
            Id = "bugbear",
            Name = "Bugbear",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            HitDice = 3,
            BABOverride = BABProgression.Medium,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 15, DEX = 12, CON = 13, WIS = 10, INT = 10, CHA = 9,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Morningstar", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 6,
            BaseHitDieHP = 16,
            HasScent = true,
            CreatureTags = new List<string> { "Humanoid", "Goblinoid", "MM35" },
            Feats = new List<string> { "Alertness", "Weapon Focus" },
            WeaponFocusChoice = "Morningstar",
            SpecialAbilities = new List<string> { "Darkvision 60 ft.", "Scent", "Move Silently +6, Hide +3" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.62f, 0.48f, 0.32f, 1f),
            PanelColor = new Color(0.25f, 0.18f, 0.1f, 0.85f),
            NameColor = new Color(0.9f, 0.75f, 0.55f),
            Description = "Bugbear (CR 2). Stealthy goblinoid brute. Morningstar +4 (1d8+2). Darkvision, scent. MM 3.5e p.29."
        });
    }

    private static void RegisterGiantBee()
    {
        Register(new NPCDefinition
        {
            Id = "giant_bee",
            Name = "Giant Bee",
            ChallengeRating = "1",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 11, DEX = 11, CON = 14, WIS = 10, INT = 1, CHA = 9,
            BAB = 2,
            NaturalArmorBonus = 4,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Sting",
                    DamageDice = 4,
                    DamageCount = 1,
                    Count = 1,
                    BonusDamageSource = DamageBonusSource.StrengthOneAndHalf,
                    Range = 1,
                    IsPrimary = true,
                    PoisonOnHitId = "giant_bee_poison"
                }
            },
            BaseSpeed = 4,
            BaseHitDieHP = 19,
            CreatureTags = new List<string> { "Vermin", "MM35", "Fly" },
            Feats = new List<string> { "Weapon Finesse" },
            SpecialAbilities = new List<string>
            {
                "Poison (Fort DC 11; initial 1d4 Dex; secondary 1d4 Dex)",
                "Fly 80 ft (good)",
                "Darkvision 60 ft",
                "Vermin traits",
                "Mindless"
            },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.95f, 0.78f, 0.2f, 1f),
            PanelColor = new Color(0.24f, 0.18f, 0.06f, 0.85f),
            NameColor = new Color(1f, 0.92f, 0.62f),
            Description = "Monster Manual giant bee. Flying vermin with a venomous sting that inflicts Dexterity damage."
        });
    }

    private static void RegisterGiantBombardierBeetle()
    {
        Register(new NPCDefinition
        {
            Id = "giant_bombardier_beetle",
            Name = "Giant Bombardier Beetle",
            ChallengeRating = "2",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 13, DEX = 10, CON = 14, WIS = 10, INT = 1, CHA = 9,
            BAB = 2,
            NaturalArmorBonus = 6,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 6,
            BaseHitDieHP = 13,
            CreatureTags = new List<string> { "Vermin", "MM35" },
            SpecialAbilities = new List<string>
            {
                "Acid Spray (10-ft cone, 6d4 acid, Reflex DC 12 half, usable once every 1d4 rounds)",
                "Darkvision 60 ft",
                "Vermin traits",
                "Mindless"
            },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.48f, 0.28f, 0.16f, 1f),
            PanelColor = new Color(0.16f, 0.1f, 0.06f, 0.85f),
            NameColor = new Color(0.92f, 0.8f, 0.62f),
            Description = "Monster Manual giant bombardier beetle. Armored vermin that sprays boiling acid in a short cone."
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
    
    private static void RegisterBabau()
    {
        Register(new NPCDefinition
        {
            Id = "babau",
            Name = "Babau",
            ChallengeRating = "6",
            Level = 7,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 7,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 21, DEX = 12, CON = 20, WIS = 13, INT = 14, CHA = 16,
            NaturalArmorBonus = 8,
            DamageReductionAmount = 10,
            DamageReductionBypass = DamageBypassTag.ColdIron | DamageBypassTag.Good,
            SpellResistance = 14,
            BaseSpeed = 6,
            BaseHitDieHP = 66,
            BAB = 7,
            Immunities = new CreatureImmunities { immuneToElectricity = true, immuneToPoison = true },
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Acid, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 10 }
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Outsider", "Chaotic", "Evil", "Extraplanar", "Tanarri", "Telepathy100", "Darkvision60", "MM35" },
            Feats = new List<string> { "Cleave", "Improved Initiative", "Multiattack" },
            SpecialAbilities = new List<string> { "Protective Slime (Su): 1d8 acid to melee attackers/weapons", "Sneak Attack +2d6", "DR 10/cold iron or good", "SR 14", "Immune to electricity/poison", "Resist acid 10, cold 10, fire 10", "Darkness (Sp): at will", "Dispel Magic (Sp): at will", "See Invisibility (Sp): at will", "Greater Teleport (Sp): at will, self + 50 lb.", "Telepathy 100 ft.", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.35f, 0.25f, 0.3f, 1f),
            PanelColor = new Color(0.12f, 0.06f, 0.08f, 0.85f),
            NameColor = new Color(0.6f, 0.4f, 0.5f),
            Description = "Babau (CR 6). Skeletal demon assassin with protective acid slime and sneak attack. MM 3.5e p.40."
        });
    }

    private static void RegisterBarghest()
    {
        Register(new NPCDefinition
        {
            Id = "barghest",
            Name = "Barghest",
            ChallengeRating = "4",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 6,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 17, DEX = 15, CON = 13, WIS = 14, INT = 14, CHA = 14,
            NaturalArmorBonus = 6,
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Magic,
            BaseSpeed = 6,
            BaseHitDieHP = 33,
            BAB = 6,
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Outsider", "Evil", "Lawful", "Extraplanar", "Shapechanger", "Darkvision60", "MM35" },
            Feats = new List<string> { "Combat Reflexes", "Improved Initiative" },
            SpecialAbilities = new List<string> { "Feed (Su): devour corpse of HD 0-3 creature, gains growth", "Change Shape (Su): goblin or wolf form", "DR 5/magic", "Blink (Sp): at will", "Levitate (Sp): at will", "Misdirection (Sp): at will", "Charm Monster (Sp): 1/day, DC 16", "Crushing Despair (Sp): 1/day, DC 16", "Dimension Door (Sp): 1/day", "Scent", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.35f, 0.3f, 0.25f, 1f),
            PanelColor = new Color(0.1f, 0.08f, 0.06f, 0.85f),
            NameColor = new Color(0.6f, 0.5f, 0.4f),
            Description = "Barghest (CR 4). Fiendish wolf-goblin shapechanger that grows by feeding. MM 3.5e p.22."
        });
    }

    private static void RegisterBasilisk()
    {
        Register(new NPCDefinition
        {
            Id = "basilisk",
            Name = "Basilisk",
            ChallengeRating = "5",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 6,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 15, DEX = 8, CON = 15, WIS = 12, INT = 2, CHA = 11,
            NaturalArmorBonus = 7,
            BaseSpeed = 4, // 20 ft
            BaseHitDieHP = 45,
            BAB = 6,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            AuraAbility = new AuraAbilityDefinition
            {
                Name = "Petrifying Gaze",
                SaveDC = 13,
                IsWillSave = false, // Fort save
                RangeFeet = 30,
                Effect = AuraEffectType.Frightened, // Closest approximation; real effect is petrification
                DurationRounds = 999 // Permanent
            },
            CreatureTags = new List<string> { "Magical Beast", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Blind-Fight", "Great Fortitude" },
            SpecialAbilities = new List<string> { "Petrifying Gaze (Su): 30 ft., Fort DC 13 or permanently turned to stone", "Darkvision 60 ft.", "Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.5f, 0.52f, 0.42f, 1f),
            PanelColor = new Color(0.18f, 0.2f, 0.12f, 0.85f),
            NameColor = new Color(0.75f, 0.78f, 0.62f),
            Description = "Basilisk (CR 5). Eight-legged reptile with petrifying gaze. MM 3.5e p.23."
        });
    }

    private static void RegisterBeardedDevil()
    {
        Register(new NPCDefinition
        {
            Id = "bearded_devil",
            Name = "Bearded Devil",
            ChallengeRating = "5",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 6,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 15, DEX = 15, CON = 17, WIS = 10, INT = 6, CHA = 10,
            NaturalArmorBonus = 7,
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Silver | DamageBypassTag.Good,
            SpellResistance = 17,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 45,
            BAB = 6,
            Immunities = new CreatureImmunities { immuneToFire = true, immuneToPoison = true },
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Acid, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 }
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Outsider", "Evil", "Lawful", "Extraplanar", "Baatezu", "Darkvision60", "MM35" },
            Feats = new List<string> { "Improved Initiative", "Power Attack" },
            SpecialAbilities = new List<string> { "Infernal Wound (Su): glaive wound bleeds 2 HP/round (DC 16 Heal or Heal spell to stop)", "Beard (Ex): touch attack, DC 16 Fort or devil chills disease", "Battle Frenzy (Ex): +4 Str for 6 rounds, 1/day", "DR 5/silver or good", "SR 17", "Immune to fire/poison", "Resist acid 10, cold 10", "See in Darkness (Su)", "Telepathy 100 ft." },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("glaive", EquipSlot.MainHand)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Berserk,
            SpriteColor = new Color(0.5f, 0.3f, 0.25f, 1f),
            PanelColor = new Color(0.2f, 0.08f, 0.05f, 0.85f),
            NameColor = new Color(0.78f, 0.45f, 0.38f),
            Description = "Bearded Devil (CR 5). Glaive-wielding devil with infernal wounds and disease. MM 3.5e p.52."
        });
    }

    private static void RegisterBehir()
    {
        Register(new NPCDefinition
        {
            Id = "behir",
            Name = "Behir",
            ChallengeRating = "8",
            Level = 9,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 9,
            SizeCategory = SizeCategory.Huge,
            IsTallCreature = false,
            STR = 26, DEX = 13, CON = 21, WIS = 14, INT = 7, CHA = 12,
            NaturalArmorBonus = 8,
            BaseSpeed = 8, // 40 ft, climb 15 ft
            BaseHitDieHP = 94,
            BAB = 9,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Bite",
            HasScent = true,
            HasRake = true,
            RakeAttack = new NaturalAttackDefinition { Name = "Rake", DamageDice = 4, DamageCount = 1, Count = 6, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = false },
            Immunities = new CreatureImmunities { immuneToElectricity = true },
            BreathWeapon = new BreathWeaponDefinition
            {
                Shape = BreathWeaponShape.Line,
                RangeFeet = 20,
                DamageDice = 6,
                DamageCount = 7,
                DamageType = DamageType.Electricity,
                SaveDC = 19,
                IsReflexSave = true,
                RechargeRounds = 3
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 3, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Magical Beast", "Darkvision60", "MM35" },
            Feats = new List<string> { "Cleave", "Power Attack", "Track" },
            SpecialAbilities = new List<string> { "Breath Weapon (Su): 20 ft. line, 7d6 electricity, Ref DC 19 half", "Improved Grab", "Constrict 2d8+8", "Rake: 6 claws 1d4+4 each (grapple)", "Swallow Whole: bite → grapple → swallow, 2d8+8 crush + 8 acid/round", "Immune to electricity", "Scent", "Can't be tripped", "Darkvision 60 ft., Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Grappler,
            SpriteColor = new Color(0.3f, 0.4f, 0.55f, 1f),
            PanelColor = new Color(0.08f, 0.14f, 0.22f, 0.85f),
            NameColor = new Color(0.5f, 0.65f, 0.82f),
            Description = "Behir (CR 8). Huge serpentine beast with lightning breath and swallow whole. MM 3.5e p.25."
        });
    }

    private static void RegisterBlackPudding()
    {
        Register(new NPCDefinition
        {
            Id = "black_pudding",
            Name = "Black Pudding",
            ChallengeRating = "7",
            Level = 10,
            CharacterClass = "Warrior",
            CreatureType = "Ooze",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 10,
            SizeCategory = SizeCategory.Huge,
            IsTallCreature = false,
            STR = 17, DEX = 1, CON = 22, WIS = 1, INT = 0, CHA = 1,
            NaturalArmorBonus = 0,
            IsMindless = true,
            BaseSpeed = 4, // 20 ft, climb 20 ft
            BaseHitDieHP = 115,
            BAB = 7,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Slam",
            Immunities = new CreatureImmunities
            {
                immuneToMindAffecting = true,
                immuneToCriticalHits = true,
                immuneToSneakAttack = true
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 6, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 3, IsPrimary = true, BonusElementalDamageDice = 6, BonusElementalDamageCount = 2, BonusElementalDamageType = DamageType.Acid }
            },
            CreatureTags = new List<string> { "Ooze", "Blindsight60", "MM35" },
            Feats = new List<string>(),
            SpecialAbilities = new List<string> { "Acid (Ex): dissolves organic and metal, 21 dmg to wood/metal per round of contact", "Improved Grab", "Constrict 2d6+4 + 2d6 acid", "Split (Ex): slashing/piercing splits into two smaller puddings", "Blindsight 60 ft.", "Ooze traits (mindless, immune to crits/sneak/mind-affecting)", "Climb 20 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.UndeadMindless,
            SpriteColor = new Color(0.1f, 0.1f, 0.12f, 0.9f),
            PanelColor = new Color(0.03f, 0.03f, 0.05f, 0.85f),
            NameColor = new Color(0.25f, 0.25f, 0.32f),
            Description = "Black Pudding (CR 7). Huge acidic ooze that splits when cut. MM 3.5e p.201."
        });
    }

    private static void RegisterBlueSlaad()
    {
        Register(new NPCDefinition
        {
            Id = "blue_slaad",
            Name = "Blue Slaad",
            ChallengeRating = "8",
            Level = 8,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.ChaoticNeutral,
            HitDice = 8,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 19, DEX = 15, CON = 19, WIS = 6, INT = 6, CHA = 9,
            NaturalArmorBonus = 7,
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Lawful,
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Acid, Amount = 5 },
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 5 },
                new DamageResistanceEntry { Type = DamageType.Electricity, Amount = 5 },
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 5 }
            },
            SpellResistance = 19,
            BaseSpeed = 6,
            BaseHitDieHP = 68,
            BAB = 8,
            HasPounce = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 2, Count = 4, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 2, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Outsider", "Chaotic", "Extraplanar", "Darkvision60", "MM35" },
            Feats = new List<string> { "Multiattack", "Power Attack", "Improved Bull Rush" },
            SpecialAbilities = new List<string> { "Pounce (Ex): full attack on charge", "Chaos Phage (Su): claw hit, DC 18 Fort or infected; transforms to red slaad in 1d4 weeks", "Summon Slaad: 40% chance 1 blue slaad", "DR 5/lawful", "SR 19", "Fast Healing 5", "Resist acid 5, cold 5, electricity 5, fire 5", "Darkvision 60 ft.", "Telepathy 100 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Berserk,
            SpriteColor = new Color(0.2f, 0.3f, 0.7f, 1f),
            PanelColor = new Color(0.04f, 0.08f, 0.28f, 0.85f),
            NameColor = new Color(0.35f, 0.48f, 0.95f),
            Description = "Blue Slaad (CR 8). Slaad that spreads chaos phage via claw rakes. MM 3.5e p.229."
        });
    }

    private static void RegisterBodak()
    {
        Register(new NPCDefinition
        {
            Id = "bodak",
            Name = "Bodak",
            ChallengeRating = "8",
            Level = 9,
            CharacterClass = "Warrior",
            CreatureType = "Undead",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 9,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 13, DEX = 15, CON = 0, WIS = 12, INT = 6, CHA = 12,
            NaturalArmorBonus = 8,
            DamageReductionAmount = 10,
            DamageReductionBypass = DamageBypassTag.ColdIron,
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Acid, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 10 }
            },
            BaseSpeed = 4, // 20 ft
            BaseHitDieHP = 58,
            BAB = 4,
            Immunities = new CreatureImmunities { immuneToElectricity = true },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Undead", "Extraplanar", "Darkvision60", "MM35" },
            Feats = new List<string> { "Dodge", "Improved Initiative", "Toughness" },
            SpecialAbilities = new List<string> { "Death Gaze (Su): 30 ft., DC 15 Fort or die; save = 1 negative level", "DR 10/cold iron", "Resist acid 10, fire 10", "Immune to electricity", "Vulnerable to sunlight (1 HP damage/round)", "Flashback (Ex): creatures seeing bodak, DC 15 Fort or stunned 1 round", "Darkvision 60 ft.", "Undead traits" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.25f, 0.2f, 0.3f, 1f),
            PanelColor = new Color(0.08f, 0.06f, 0.12f, 0.85f),
            NameColor = new Color(0.5f, 0.4f, 0.65f),
            Description = "Bodak (CR 8). Undead with death gaze that kills on failed Fort save. MM 3.5e p.28."
        });
    }

    private static void RegisterBralani()
    {
        Register(new NPCDefinition
        {
            Id = "bralani",
            Name = "Bralani",
            ChallengeRating = "6",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.ChaoticGood,
            HitDice = 6,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 17, DEX = 18, CON = 17, WIS = 14, INT = 13, CHA = 14,
            NaturalArmorBonus = 6,
            DamageReductionAmount = 10,
            DamageReductionBypass = DamageBypassTag.ColdIron | DamageBypassTag.Evil,
            SpellResistance = 17,
            BaseSpeed = 8, // 40 ft, fly 100 ft (perfect in whirlwind)
            BaseHitDieHP = 45,
            BAB = 6,
            Immunities = new CreatureImmunities { immuneToElectricity = true },
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 10 }
            },
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Outsider", "Chaotic", "Good", "Extraplanar", "Eladrin", "Fly100", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Blind-Fight", "Improved Initiative" },
            SpecialAbilities = new List<string> { "Whirlwind Form (Su): 10-ft. radius, 3d6+3 damage", "Alternate Form: whirlwind or humanoid", "Tongues (Su): constant", "DR 10/cold iron or evil", "SR 17", "Immune to electricity/petrification", "Resist cold 10, fire 10", "Blur (Sp): at will", "Charm Person (Sp): at will, DC 13", "Gust of Wind (Sp): at will, DC 14", "Mirror Image (Sp): at will", "Wind Wall (Sp): at will", "Lightning Bolt (Sp): 2/day, DC 15", "Cure Serious Wounds (Sp): 2/day", "Darkvision 60 ft., Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("scimitar", EquipSlot.MainHand),
                new EquipmentSlotPair("composite_longbow", EquipSlot.Ranged)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Ranged,
            SpriteColor = new Color(0.5f, 0.65f, 0.8f, 1f),
            PanelColor = new Color(0.15f, 0.22f, 0.3f, 0.85f),
            NameColor = new Color(0.7f, 0.85f, 0.95f),
            Description = "Bralani (CR 6). Wind eladrin that shifts between humanoid and whirlwind forms. MM 3.5e p.93."
        });
    }

    private static void RegisterGreaterBarghest()
    {
        Register(new NPCDefinition
        {
            Id = "greater_barghest",
            Name = "Greater Barghest",
            ChallengeRating = "7",
            Level = 9,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 9,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 23, DEX = 15, CON = 17, WIS = 18, INT = 18, CHA = 18,
            NaturalArmorBonus = 9,
            DamageReductionAmount = 10,
            DamageReductionBypass = DamageBypassTag.Magic,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 67,
            BAB = 9,
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 2, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Outsider", "Evil", "Lawful", "Extraplanar", "Shapechanger", "Darkvision60", "MM35" },
            Feats = new List<string> { "Combat Casting", "Combat Reflexes", "Improved Initiative" },
            SpecialAbilities = new List<string> { "Feed (Su): as barghest but HD 0-8", "Change Shape (Su): goblin or dire wolf form", "DR 10/magic", "Blink/Levitate/Misdirection (Sp): at will", "Charm Monster (Sp): at will, DC 18", "Crushing Despair (Sp): at will, DC 18", "Dimension Door (Sp): at will", "Mass Bull's Strength (Sp): 1/day", "Mass Enlarge (Sp): 1/day", "Scent", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.3f, 0.25f, 0.2f, 1f),
            PanelColor = new Color(0.08f, 0.06f, 0.04f, 0.85f),
            NameColor = new Color(0.55f, 0.45f, 0.35f),
            Description = "Greater Barghest (CR 7). Fully grown fiendish wolf with at-will charm monster. MM 3.5e p.23."
        });
    }
}

}
