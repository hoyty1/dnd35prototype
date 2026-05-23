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
    }

}
