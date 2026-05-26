using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: E
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_E()
    {
        RegisterEagle();
        RegisterGiantEagle();
    
        RegisterSummonSmallAirElemental();
        RegisterSummonSmallFireElemental();
        RegisterSummonSmallEarthElemental();
        RegisterSummonSmallWaterElemental();
        RegisterEfreeti();
        RegisterElfWarrior();
        RegisterErinyes();
        RegisterEtherealFilcher();
        RegisterEtherealMarauder();
        RegisterEttercap();
        RegisterEttin();

    }

    private static void RegisterEagle()
    {
        Register(new NPCDefinition
        {
            Id = "eagle",
            Name = "Eagle",
            ChallengeRating = "1/2",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 10, DEX = 15, CON = 12, WIS = 14, INT = 2, CHA = 6,
            BAB = 2,
            NaturalArmorBonus = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Talons", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 2,
            BaseHitDieHP = 5,
            CreatureTags = new List<string> { "Animal", "MM35", "Fly", "SummonBase" },
            Feats = new List<string> { "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Low-light vision", "Fly 80 ft (average)", "Size bonus +1 AC/+1 attack" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.78f, 0.73f, 0.64f, 1f),
            PanelColor = new Color(0.2f, 0.17f, 0.1f, 0.85f),
            NameColor = new Color(0.97f, 0.91f, 0.77f),
            Description = "Monster Manual eagle. Small raptor with swift flight and a sharp talon strike."
        });
    }


    /// <summary>
    /// Giant Eagle (CR 3) — Large magical beast, INT 10, can speak Common and Auran.
    /// MM 3.5e p.93. 4 HD, fly 80 ft (average), Evasion, 10 ft. space/5 ft. reach.
    /// </summary>
    private static void RegisterGiantEagle()
    {
        Register(new NPCDefinition
        {
            Id = "giant_eagle",
            Name = "Giant Eagle",
            ChallengeRating = "3",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            HitDice = 4,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 18, DEX = 17, CON = 12, WIS = 14, INT = 10, CHA = 10,
            BAB = 4,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 2, // 10 ft. land, fly 80 ft. (average)
            BaseHitDieHP = 26, // 4d10+4
            CreatureTags = new List<string> { "Magical Beast", "MM35", "Fly" },
            Feats = new List<string> { "Alertness", "Flyby Attack" },
            SpecialAbilities = new List<string>
            {
                "Evasion",
                "Fly 80 ft. (average)",
                "Low-light vision",
                "Darkvision 60 ft.",
                "Languages: Common, Auran",
                "Skills: Knowledge (nature) +2, Listen +6, Sense Motive +4, Spot +15 (+4 racial in daylight)",
                "Alignment: Neutral Good"
            },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.82f, 0.75f, 0.58f, 1f),
            PanelColor = new Color(0.25f, 0.2f, 0.12f, 0.85f),
            NameColor = new Color(1f, 0.94f, 0.78f),
            Description = "Monster Manual giant eagle (CR 3). Intelligent magical beast with evasion, keen eyesight, and powerful talons. MM 3.5e p.93."
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
    
    private static void RegisterEfreeti()
    {
        Register(new NPCDefinition
        {
            Id = "efreeti",
            Name = "Efreeti",
            ChallengeRating = "8",
            Level = 10,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 10,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 23, DEX = 17, CON = 14, WIS = 15, INT = 12, CHA = 15,
            NaturalArmorBonus = 6,
            BaseSpeed = 4, // 20 ft, fly 40 ft (perfect)
            BaseHitDieHP = 65,
            BAB = 10,
            Immunities = new CreatureImmunities { immuneToFire = true },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 8, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true, BonusElementalDamageDice = 6, BonusElementalDamageCount = 1, BonusElementalDamageType = DamageType.Fire }
            },
            CreatureTags = new List<string> { "Outsider", "Fire", "Extraplanar", "Fly40", "Telepathy100", "Darkvision60", "MM35" },
            Feats = new List<string> { "Combat Casting", "Combat Reflexes", "Dodge", "Improved Initiative", "Quicken Spell-Like Ability" },
            SpecialAbilities = new List<string> { "Heat (Ex): +1d6 fire on melee attacks", "Change Size (Sp): 2/day, reduce/enlarge", "Gaseous Form (Sp): at will", "Invisibility (Sp): at will", "Permanent Image (Sp): at will, DC 17", "Polymorph (Sp): 3/day, self, up to 1 hr", "Scorching Ray (Sp): 3/day", "Wall of Fire (Sp): 1/day, DC 16", "Grant up to 3 Wishes (Sp): to non-genies, 1/day", "Plane Shift (Sp): at will", "Immune to fire", "Vulnerable to cold", "Telepathy 100 ft.", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Spellcaster,
            SpriteColor = new Color(0.8f, 0.45f, 0.2f, 1f),
            PanelColor = new Color(0.3f, 0.15f, 0.05f, 0.85f),
            NameColor = new Color(0.95f, 0.65f, 0.3f),
            Description = "Efreeti (CR 8). Fire genie with heat attacks and wish-granting. MM 3.5e p.115."
        });
    }

    private static void RegisterElfWarrior()
    {
        Register(new NPCDefinition
        {
            Id = "elf_warrior",
            Name = "Elf Warrior",
            ChallengeRating = "1/2",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            CharacterAlignment = Alignment.ChaoticGood,
            HitDice = 1,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 13, DEX = 15, CON = 10, WIS = 9, INT = 10, CHA = 8,
            NaturalArmorBonus = 0,
            BaseSpeed = 6, // 30 ft
            BaseHitDieHP = 4,
            BAB = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Humanoid", "Elf", "MM35" },
            Feats = new List<string> { "Weapon Focus (longbow)" },
            SpecialAbilities = new List<string> { "Low-light vision", "Immune to sleep", "+2 saves vs. enchantment", "Elven weapon proficiency" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("longsword", EquipSlot.MainHand),
                new EquipmentSlotPair("longbow", EquipSlot.Ranged),
                new EquipmentSlotPair("studded_leather", EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Ranged,
            SpriteColor = new Color(0.7f, 0.85f, 0.65f, 1f),
            PanelColor = new Color(0.15f, 0.25f, 0.12f, 0.85f),
            NameColor = new Color(0.8f, 0.95f, 0.7f),
            Description = "Elf Warrior (CR 1/2). Nimble humanoid with longsword and longbow. MM 3.5e p.101."
        });
    }

    private static void RegisterErinyes()
    {
        Register(new NPCDefinition
        {
            Id = "erinyes",
            Name = "Erinyes",
            ChallengeRating = "8",
            Level = 9,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 9,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 21, DEX = 17, CON = 21, WIS = 18, INT = 14, CHA = 20,
            NaturalArmorBonus = 10,
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Good,
            SpellResistance = 20,
            BaseSpeed = 6, // 30 ft, fly 50 ft (good)
            BaseHitDieHP = 85,
            BAB = 9,
            Immunities = new CreatureImmunities { immuneToFire = true, immuneToPoison = true },
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Acid, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 }
            },
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Outsider", "Evil", "Lawful", "Extraplanar", "Baatezu", "Fly50", "Darkvision60", "MM35" },
            Feats = new List<string> { "Dodge", "Mobility", "Point Blank Shot", "Rapid Shot", "Shot on the Run" },
            SpecialAbilities = new List<string> { "True Seeing (Su): constant", "Charm Monster (Sp): at will, DC 19", "Minor Image (Sp): at will", "Unholy Blight (Sp): at will, DC 19", "Teleport Greater (Sp): at will, self + 50 lb.", "DR 5/good", "SR 20", "Immune to fire/poison", "Resist acid 10, cold 10", "See in Darkness (Su)", "Telepathy 100 ft.", "Fly 50 ft. (good)" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("longsword", EquipSlot.MainHand),
                new EquipmentSlotPair("composite_longbow", EquipSlot.Ranged)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Ranged,
            SpriteColor = new Color(0.55f, 0.3f, 0.35f, 1f),
            PanelColor = new Color(0.22f, 0.08f, 0.1f, 0.85f),
            NameColor = new Color(0.85f, 0.48f, 0.55f),
            Description = "Erinyes (CR 8). Fallen angel devil with flaming bow and charm monster. MM 3.5e p.54."
        });
    }

    private static void RegisterEtherealFilcher()
    {
        Register(new NPCDefinition
        {
            Id = "ethereal_filcher",
            Name = "Ethereal Filcher",
            ChallengeRating = "3",
            Level = 5,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 5,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 10, DEX = 14, CON = 11, WIS = 12, INT = 7, CHA = 10,
            NaturalArmorBonus = 3,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 22,
            BAB = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.None, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Aberration", "Darkvision60", "MM35" },
            Feats = new List<string> { "Dodge", "Improved Initiative" },
            SpecialAbilities = new List<string> { "Ethereal Jaunt (Su): at will, as ethereal jaunt", "Detect Magic (Su): continuous", "Filch (Ex): can steal items from foes via Sleight of Hand", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.5f, 0.5f, 0.55f, 0.7f),
            PanelColor = new Color(0.18f, 0.18f, 0.22f, 0.85f),
            NameColor = new Color(0.75f, 0.75f, 0.82f),
            Description = "Ethereal Filcher (CR 3). Item-stealing aberration from the Ethereal Plane. MM 3.5e p.104."
        });
    }

    private static void RegisterEtherealMarauder()
    {
        Register(new NPCDefinition
        {
            Id = "ethereal_marauder",
            Name = "Ethereal Marauder",
            ChallengeRating = "3",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 13, DEX = 15, CON = 13, WIS = 12, INT = 7, CHA = 10,
            NaturalArmorBonus = 2,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 13,
            BAB = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Magical Beast", "Extraplanar", "Darkvision60", "MM35" },
            Feats = new List<string> { "Improved Initiative" },
            SpecialAbilities = new List<string> { "Ethereal Jaunt (Su): at will, shifts to/from Ethereal Plane", "Darkvision 60 ft., Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.45f, 0.5f, 0.55f, 0.7f),
            PanelColor = new Color(0.14f, 0.18f, 0.22f, 0.85f),
            NameColor = new Color(0.68f, 0.75f, 0.82f),
            Description = "Ethereal Marauder (CR 3). Ethereal ambush predator. MM 3.5e p.105."
        });
    }

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

    private static void RegisterEttin()
    {
        Register(new NPCDefinition
        {
            Id = "ettin",
            Name = "Ettin",
            ChallengeRating = "6",
            Level = 10,
            CharacterClass = "Warrior",
            CreatureType = "Giant",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 10,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 23, DEX = 8, CON = 15, WIS = 10, INT = 6, CHA = 11,
            NaturalArmorBonus = 7,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 65,
            BAB = 7,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Giant", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Improved Initiative", "Iron Will", "Power Attack" },
            SpecialAbilities = new List<string> { "Two heads: cannot be flanked, +2 Spot/Listen/Search", "Superior Two-Weapon Fighting: no off-hand penalty", "Darkvision 60 ft.", "Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("morningstar", EquipSlot.MainHand),
                new EquipmentSlotPair("morningstar", EquipSlot.OffHand),
                new EquipmentSlotPair("hide_armor", EquipSlot.Armor)
            },
            BackpackItemIds = new List<string> { "javelin" },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.55f, 0.48f, 0.38f, 1f),
            PanelColor = new Color(0.2f, 0.16f, 0.1f, 0.85f),
            NameColor = new Color(0.8f, 0.7f, 0.55f),
            Description = "Ettin (CR 6). Two-headed giant with superior two-weapon fighting. MM 3.5e p.106."
        });
    }
}

}
