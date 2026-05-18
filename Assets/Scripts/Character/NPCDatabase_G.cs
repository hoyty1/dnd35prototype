using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Monster Manual creatures: G
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_G()
    {
        RegisterGoblin();
        RegisterGiantOwl();
        RegisterGiantWasp();
        RegisterGiantPrayingMantis();
    }

    private static void RegisterGoblin()
    {
        Register(new NPCDefinition
        {
            Id = "goblin",
            Name = "Goblin",
            ChallengeRating = "1/3",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            HitDice = 1,
            BaseAttackBonusOverride = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 11, DEX = 13, CON = 12, WIS = 9, INT = 10, CHA = 6,
            BaseSpeed = 6, // 30 ft.
            BaseHitDieHP = 5,
            CreatureTags = new List<string> { "Goblinoid", "MM35" },
            Feats = new List<string> { "Alertness" },
            SpecialAbilities = new List<string>
            {
                "Darkvision 60 ft",
                "Usually neutral evil",
                "Skills: Hide +5, Listen +2, Move Silently +5, Ride +4, Spot +2",
                "Attack: Morningstar +2 melee (1d6) or javelin +3 ranged (1d4, 30 ft.)"
            },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.LEATHER_ARMOR, EquipSlot.Armor),
                new EquipmentSlotPair(ItemIDs.MORNINGSTAR, EquipSlot.RightHand),
                new EquipmentSlotPair(ItemIDs.SHIELD_LIGHT_WOODEN, EquipSlot.LeftHand)
            },
            BackpackItemIds = new List<string> { ItemIDs.JAVELIN },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.56f, 0.78f, 0.28f, 1f),
            PanelColor = new Color(0.33f, 0.1f, 0.1f, 0.85f),
            NameColor = new Color(0.95f, 0.45f, 0.45f),
            Description = "Monster Manual goblin. Small goblinoid skirmisher with shield, morningstar, and javelin."
        });
    }

    /// <summary>
    /// Giant Owl (CR 3) — Large magical beast, INT 10, fly 70 ft (average).
    /// MM 3.5e p.205. 4 HD, superior low-light vision.
    /// </summary>
    private static void RegisterGiantOwl()
    {
        Register(new NPCDefinition
        {
            Id = "giant_owl",
            Name = "Giant Owl",
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
            BaseSpeed = 2, // 10 ft. land, fly 70 ft. (average)
            BaseHitDieHP = 26, // 4d10+4
            CreatureTags = new List<string> { "Magical Beast", "MM35", "Fly" },
            Feats = new List<string> { "Alertness", "Wingover" },
            SpecialAbilities = new List<string>
            {
                "Superior low-light vision (4x normal)",
                "Fly 70 ft. (average)",
                "Darkvision 60 ft.",
                "Languages: Common, Sylvan (understands but cannot speak)",
                "Skills: Knowledge (nature) +2, Listen +17, Move Silently +12 (*+8 racial), Spot +10",
                "Alignment: Neutral Good"
            },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.7f, 0.65f, 0.55f, 1f),
            PanelColor = new Color(0.2f, 0.18f, 0.13f, 0.85f),
            NameColor = new Color(0.96f, 0.92f, 0.84f),
            Description = "Monster Manual giant owl (CR 3). Intelligent magical beast with exceptional hearing, silent flight, and superior low-light vision. MM 3.5e p.205."
        });
    }

    /// <summary>
    /// Giant Wasp (CR 3) — Large vermin with poison sting and flight.
    /// MM 3.5e p.285. 5 HD, fly 60 ft (good).
    /// </summary>
    private static void RegisterGiantWasp()
    {
        Register(new NPCDefinition
        {
            Id = "giant_wasp",
            Name = "Giant Wasp",
            ChallengeRating = "3",
            Level = 5,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = 5,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 18, DEX = 12, CON = 21, WIS = 13, INT = CharacterStats.NO_SCORE, CHA = 11,
            BAB = 3,
            NaturalArmorBonus = 4,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Sting", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true, PoisonOnHitId = "giant_wasp_poison" }
            },
            BaseSpeed = 4, // 20 ft., fly 60 ft. (good)
            BaseHitDieHP = 32, // 5d8+10
            IsMindless = true,
            Immunities = ImmunityPresets.MindlessImmunities(),
            CreatureTags = new List<string> { "Vermin", "MM35", "Fly" },
            SpecialAbilities = new List<string>
            {
                "Poison (Fort DC 18; initial 1d6 Dex; secondary 1d6 Dex)",
                "Fly 60 ft. (good)",
                "Darkvision 60 ft.",
                "Vermin traits (mindless)",
                "Alignment: True Neutral"
            },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.85f, 0.78f, 0.2f, 1f),
            PanelColor = new Color(0.28f, 0.24f, 0.06f, 0.85f),
            NameColor = new Color(1f, 0.95f, 0.6f),
            Description = "Monster Manual giant wasp (CR 3). Sting +6 (1d6+6 + poison Fort DC 18, 1d6 Dex). Fly 60 ft. (good). MM 3.5e p.285."
        });
    }

    /// <summary>
    /// Giant Praying Mantis (CR 3) — Large vermin with improved grab.
    /// MM 3.5e p.285. 4 HD.
    /// </summary>
    private static void RegisterGiantPrayingMantis()
    {
        Register(new NPCDefinition
        {
            Id = "giant_praying_mantis",
            Name = "Giant Praying Mantis",
            ChallengeRating = "3",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = 4,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 19, DEX = 12, CON = 17, WIS = 14, INT = CharacterStats.NO_SCORE, CHA = 11,
            BAB = 3,
            NaturalArmorBonus = 5,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claws", DamageDice = 8, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 4, // 20 ft., fly 40 ft. (poor)
            BaseHitDieHP = 30, // 4d8+12
            IsMindless = true,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Claws",
            Immunities = ImmunityPresets.MindlessImmunities(),
            CreatureTags = new List<string> { "Vermin", "MM35", "Fly" },
            SpecialAbilities = new List<string>
            {
                "Improved Grab (claw attacks)",
                "Squeeze (1d8+4 damage per round on grappled foe)",
                "Fly 40 ft. (poor)",
                "Darkvision 60 ft.",
                "Vermin traits (mindless)",
                "Skills: Hide +4, Spot +6 (+4 racial due to coloration)",
                "Alignment: True Neutral"
            },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.35f, 0.68f, 0.28f, 1f),
            PanelColor = new Color(0.12f, 0.24f, 0.09f, 0.85f),
            NameColor = new Color(0.8f, 0.95f, 0.72f),
            Description = "Monster Manual giant praying mantis (CR 3). Claws +6 (1d8+4), improved grab, squeeze. Ambush predator with camouflage. MM 3.5e p.285."
        });
    }
}
