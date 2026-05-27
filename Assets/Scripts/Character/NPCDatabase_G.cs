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
        RegisterGargoyle();
        RegisterGelatinousCube();
        RegisterGiantCentipede();
        RegisterGiantRat();
        RegisterGnoll();
        RegisterGoblin();
        RegisterGiantOwl();
        RegisterGiantWasp();
        RegisterGiantPrayingMantis();
        RegisterNobleDjinni();
        RegisterGhast();
        RegisterGhoul();
        RegisterGiantConstrictorSnake();
        RegisterGiantStagBeetle();
        RegisterGiantWorkerAnt();
        RegisterGibberingMouther();
        RegisterGorgon();
        RegisterGrayOoze();
        RegisterGreaterShadow();
        RegisterGreenSlaad();
        RegisterGrick();
        RegisterGrimlock();
        RegisterGreenHag();
        RegisterGauth();
        RegisterGoblinWarrior();
        RegisterGirallon();
        RegisterGhost();
        RegisterGiantCrocodile();
    }

    // ════════════════════════════════════════════════════════════
    //  Noble Djinni — MM p.114–115
    //  Outsider (Air, Extraplanar), Large, CR 5
    //  Used by Ring of Djinni Calling (DMG p.232, Sprint 2)
    //  7d8+14 HD (45 HP), AC 16 (-1 size, +3 Dex, +4 natural)
    //  2 slams +10 melee (1d8+6), Fly 60 ft (perfect)
    //  Str 18, Dex 17, Con 14, Int 14, Wis 15, Cha 15
    //  Fort +7, Ref +8, Will +7
    //  Immune: Acid. Telepathy 100 ft. Plane Shift at will.
    // ════════════════════════════════════════════════════════════
    private static void RegisterNobleDjinni()
    {
        Register(new NPCDefinition
        {
            Id = "noble_djinni",
            Name = "Noble Djinni",
            ChallengeRating = "5",
            Level = 7,
            CharacterClass = "Outsider",
            CreatureType = "Outsider",
            HitDice = 7,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 18, DEX = 17, CON = 14, WIS = 15, INT = 14, CHA = 15,
            NaturalArmorBonus = 4,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Slam",
                    DamageDice = 8,
                    DamageCount = 1,
                    Count = 2, // Two slams in full attack
                    BonusDamageSource = DamageBonusSource.Strength,
                    Range = 2, // 10 ft reach for Large
                    IsPrimary = true
                }
            },
            BaseSpeed = 4, // 20 ft (fly 60 ft tracked via tag)
            BaseHitDieHP = 45,
            BAB = 7,
            SpellResistance = 0,
            CreatureTags = new List<string> { "Outsider", "Air", "Extraplanar", "Summoned", "Fly60", "Darkvision60", "MM35" },
            Feats = new List<string> { "Combat Casting", "Combat Reflexes", "Dodge", "Improved Initiative" },
            SpecialAbilities = new List<string>
            {
                "Air Mastery: +1 attack/damage vs airborne, -4 vs grounded",
                "Whirlwind (Su): 10-70 ft high, 2d6+4 damage, Ref DC 18",
                "Telepathy 100 ft",
                "Plane Shift (Sp): At will, self + passengers",
                "Invisibility (Sp): At will, self only",
                "Create Food and Water (Sp): 1/day",
                "Major Creation (Sp): 1/day, vegetable matter permanent",
                "Persistent Image (Sp): 1/day, DC 17",
                "Wind Walk (Sp): 1/day",
                "Gaseous Form (Sp): 1/day, up to 1 hour",
                "Immunity: Acid",
                "Darkvision 60 ft",
                "Fort +7, Ref +8, Will +7"
            },
            DamageImmunities = new List<DamageType> { DamageType.Acid },
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.6f, 0.8f, 1.0f, 1f), // Airy blue
            PanelColor = new Color(0.15f, 0.2f, 0.35f, 0.85f),
            NameColor = new Color(0.8f, 0.9f, 1.0f),
            Description = "A Noble Djinni (MM p.114), an air genie summoned by the Ring of Djinni Calling. Large outsider with 7 HD, powerful melee slams, whirlwind ability, and various spell-like abilities. AC 16 (-1 size, +3 Dex, +4 natural). Fort +7, Ref +8, Will +7."
        });
    }

    /// <summary>
    /// Gargoyle (CR 4) — MM 3.5e p.113. Flying stone creature with DR 10/magic.
    /// 4d8+19 HP (37), 2 claws 1d4+2, bite 1d6+2, gore 1d6+2. Freeze ability.
    /// </summary>
    private static void RegisterGargoyle()
    {
        Register(new NPCDefinition
        {
            Id = "gargoyle",
            Name = "Gargoyle",
            ChallengeRating = "4",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Monstrous Humanoid",
            MaterialComposition = MaterialComposition.Stone,
            HitDice = 4,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 15, DEX = 14, CON = 18, WIS = 11, INT = 6, CHA = 7,
            NaturalArmorBonus = 4,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false },
                new NaturalAttackDefinition { Name = "Gore", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 8, // 40 ft, fly 60 ft (average)
            BaseHitDieHP = 37,
            DamageReductionAmount = 10,
            DamageReductionBypass = DamageBypassTag.Magic,
            CreatureTags = new List<string> { "Monstrous Humanoid", "Earth", "MM35" },
            Feats = new List<string> { "Multiattack", "Toughness" },
            SpecialAbilities = new List<string> { "DR 10/magic", "Darkvision 60 ft.", "Freeze (appear as statue)", "Fly 60 ft. (average)" },
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.55f, 0.55f, 0.58f, 1f),
            PanelColor = new Color(0.2f, 0.2f, 0.22f, 0.85f),
            NameColor = new Color(0.8f, 0.8f, 0.85f),
            Description = "Gargoyle (CR 4). Stone flyer with 4 natural attacks and DR 10/magic. Claw/claw/bite/gore. MM 3.5e p.113."
        });
    }

    /// <summary>
    /// Gelatinous Cube (CR 3) — MM 3.5e p.201. Transparent ooze with engulf, paralysis, acid.
    /// 4d10+20 HP (42), slam 1d6+1 + acid 1d6 + paralysis DC 13.
    /// </summary>
    private static void RegisterGelatinousCube()
    {
        Register(new NPCDefinition
        {
            Id = "gelatinous_cube",
            Name = "Gelatinous Cube",
            ChallengeRating = "3",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Ooze",
            HitDice = 4,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 10, DEX = 1, CON = 26, WIS = 1, INT = 0, CHA = 1,
            NaturalArmorBonus = 0,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Slam", DamageDice = 6, DamageCount = 1, Count = 1,
                    BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true,
                    ParalysisOnHitDC = 13, ParalysisOnHitDurationRounds = 4,
                    BonusElementalDamageDice = 6, BonusElementalDamageCount = 1, BonusElementalDamageType = DamageType.Acid
                }
            },
            BaseSpeed = 3, // 15 ft.
            BaseHitDieHP = 42,
            IsMindless = true,
            DamageImmunities = new List<DamageType> { DamageType.Electricity },
            Engulf = new EngulfDefinition
            {
                ReflexSaveDC = 13,
                DamagePerRound = 6, // 1d6 acid
                DamageType = DamageType.Acid,
                ParalysisDC = 13,
                ParalysisDurationRounds = 4,
                EscapeDC = 12
            },
            CreatureTags = new List<string> { "Ooze", "MM35" },
            Feats = new List<string>(),
            SpecialAbilities = new List<string> { "Blindsight 60 ft.", "Transparent", "Engulf (DC 13 Ref)", "Paralysis (DC 13 Fort, 3d6 rounds)", "Acid 1d6", "Immunity to electricity", "Mindless" },
            AIProfileArchetype = NPCAIProfileArchetype.None,
            SpriteColor = new Color(0.65f, 0.85f, 0.65f, 0.5f),
            PanelColor = new Color(0.15f, 0.3f, 0.15f, 0.85f),
            NameColor = new Color(0.7f, 1f, 0.7f),
            Description = "Gelatinous Cube (CR 3). Transparent ooze. Slam + acid + paralysis. Engulf. MM 3.5e p.201."
        });
    }

    /// <summary>
    /// Giant Centipede, Medium (CR 1/2) — MM 3.5e p.286. Vermin with poison bite.
    /// 1d8 HP (4), bite 1d6-1 + poison (Fort DC 13, 1d3 Dex).
    /// </summary>
    private static void RegisterGiantCentipede()
    {
        Register(new NPCDefinition
        {
            Id = "giant_centipede",
            Name = "Giant Centipede",
            ChallengeRating = "1/2",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = 1,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 9, DEX = 15, CON = 10, WIS = 10, INT = 0, CHA = 2,
            NaturalArmorBonus = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1,
                    BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = true,
                    PoisonOnHitId = "medium_centipede_poison"
                }
            },
            BaseSpeed = 8, // 40 ft.
            BaseHitDieHP = 4,
            IsMindless = true,
            CreatureTags = new List<string> { "Vermin", "MM35" },
            Feats = new List<string> { "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Darkvision 60 ft.", "Poison (DC 13 Fort, 1d3 Dex/1d3 Dex)", "Climb 40 ft." },
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.52f, 0.35f, 0.22f, 1f),
            PanelColor = new Color(0.2f, 0.12f, 0.06f, 0.85f),
            NameColor = new Color(0.85f, 0.65f, 0.45f),
            Description = "Giant centipede (CR 1/2). Venomous vermin with Dex-damaging poison. MM 3.5e p.286."
        });
    }

    /// <summary>
    /// Giant Rat (CR 1/3) — Custom. Essentially a re-skin of dire rat, slightly weaker.
    /// 1d8+1 HP (5), bite 1d4+1. Simpler than dire rat (no disease).
    /// </summary>
    private static void RegisterGiantRat()
    {
        Register(new NPCDefinition
        {
            Id = "giant_rat",
            Name = "Giant Rat",
            ChallengeRating = "1/3",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 10, DEX = 15, CON = 12, WIS = 12, INT = 1, CHA = 4,
            NaturalArmorBonus = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 6, // 30 ft, climb 20 ft
            BaseHitDieHP = 5,
            HasScent = true,
            CreatureTags = new List<string> { "Animal", "MM35" },
            Feats = new List<string> { "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Low-light vision", "Scent" },
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.5f, 0.45f, 0.4f, 1f),
            PanelColor = new Color(0.18f, 0.16f, 0.14f, 0.85f),
            NameColor = new Color(0.85f, 0.8f, 0.75f),
            Description = "Giant rat (CR 1/3). Small vermin-like animal. Bite +1 (1d4). Low-light vision, scent."
        });
    }

    /// <summary>
    /// Gnoll (CR 1) — MM 3.5e p.130. Hyena-headed humanoid with battleaxe.
    /// 2d8+2 HP (11), battleaxe 1d8+2 or shortbow 1d6. AC 15 (+1 Dex, +1 natural, +2 leather, +1 shield).
    /// </summary>
    private static void RegisterGnoll()
    {
        Register(new NPCDefinition
        {
            Id = "gnoll",
            Name = "Gnoll",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            HitDice = 2,
            BABOverride = BABProgression.Medium,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 15, DEX = 10, CON = 13, WIS = 11, INT = 8, CHA = 8,
            NaturalArmorBonus = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Battleaxe", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 6,
            BaseHitDieHP = 11,
            CreatureTags = new List<string> { "Humanoid", "Gnoll", "MM35" },
            Feats = new List<string> { "Power Attack" },
            SpecialAbilities = new List<string> { "Darkvision 60 ft." },
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.7f, 0.58f, 0.4f, 1f),
            PanelColor = new Color(0.28f, 0.22f, 0.13f, 0.85f),
            NameColor = new Color(0.95f, 0.82f, 0.6f),
            Description = "Gnoll (CR 1). Hyena-headed warrior. Battleaxe +3 (1d8+2). Darkvision 60 ft. MM 3.5e p.130."
        });
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
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.35f, 0.68f, 0.28f, 1f),
            PanelColor = new Color(0.12f, 0.24f, 0.09f, 0.85f),
            NameColor = new Color(0.8f, 0.95f, 0.72f),
            Description = "Monster Manual giant praying mantis (CR 3). Claws +6 (1d8+4), improved grab, squeeze. Ambush predator with camouflage. MM 3.5e p.285."
        });
    private static void RegisterGhast()
    {
        Register(new NPCDefinition
        {
            Id = "ghast",
            Name = "Ghast",
            ChallengeRating = "3",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Undead",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 4,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 17, DEX = 15, CON = 0, WIS = 14, INT = 13, CHA = 16,
            NaturalArmorBonus = 5,
            BaseSpeed = 6,
            BaseHitDieHP = 29,
            BAB = 2,
            StenchAuraDC = 15,
            StenchAuraRange = 10,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true, ParalysisOnHitDC = 15, ParalysisOnHitDurationRounds = 4, HasDiseaseOnHit = true, DiseaseOnHitType = DiseaseType.GhoulFever },
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false, ParalysisOnHitDC = 15, ParalysisOnHitDurationRounds = 4 }
            },
            CreatureTags = new List<string> { "Undead", "Darkvision60", "MM35" },
            Feats = new List<string> { "Multiattack", "Toughness" },
            SpecialAbilities = new List<string> { "Ghoul Fever (Su): bite, DC 14 Fort, 1 day incubation, 1d3 Con + 1d3 Dex", "Paralysis (Su): DC 15 Fort, 1d4+1 rounds (elves NOT immune)", "Stench (Ex): 10 ft., DC 15 Fort or sickened 1d6+4 min", "Darkvision 60 ft.", "Undead traits", "+2 turn resistance" },
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.4f, 0.48f, 0.38f, 1f),
            PanelColor = new Color(0.12f, 0.16f, 0.1f, 0.85f),
            NameColor = new Color(0.65f, 0.78f, 0.6f),
            Description = "Ghast (CR 3). Advanced ghoul with stench aura, paralysis affects even elves. MM 3.5e p.119."
        });
    }

    private static void RegisterGhoul()
    {
        Register(new NPCDefinition
        {
            Id = "ghoul",
            Name = "Ghoul",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Undead",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 13, DEX = 15, CON = 0, WIS = 14, INT = 13, CHA = 12,
            NaturalArmorBonus = 2,
            BaseSpeed = 6,
            BaseHitDieHP = 13,
            BAB = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true, ParalysisOnHitDC = 14, ParalysisOnHitDurationRounds = 4, HasDiseaseOnHit = true, DiseaseOnHitType = DiseaseType.GhoulFever },
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 3, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false, ParalysisOnHitDC = 14, ParalysisOnHitDurationRounds = 4 }
            },
            CreatureTags = new List<string> { "Undead", "Darkvision60", "MM35" },
            Feats = new List<string> { "Multiattack" },
            SpecialAbilities = new List<string> { "Ghoul Fever (Su): bite, DC 14 Fort, 1 day incubation, 1d3 Con + 1d3 Dex", "Paralysis (Su): DC 14 Fort, 1d4+1 rounds, elves immune", "Darkvision 60 ft.", "Undead traits", "+2 turn resistance" },
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.45f, 0.5f, 0.42f, 1f),
            PanelColor = new Color(0.15f, 0.18f, 0.12f, 0.85f),
            NameColor = new Color(0.7f, 0.8f, 0.65f),
            Description = "Ghoul (CR 1). Undead with paralysing bite and claws. Ghoul fever. MM 3.5e p.118."
        });
    }

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
            AIProfileArchetype = NPCAIProfileArchetype.Grappler,
            SpriteColor = new Color(0.4f, 0.45f, 0.3f, 1f),
            PanelColor = new Color(0.12f, 0.15f, 0.06f, 0.85f),
            NameColor = new Color(0.62f, 0.7f, 0.48f),
            Description = "Giant Constrictor Snake (CR 5). Massive snake with constrict. MM 3.5e p.279."
        });
    }

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
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.3f, 0.28f, 0.22f, 1f),
            PanelColor = new Color(0.08f, 0.06f, 0.03f, 0.85f),
            NameColor = new Color(0.5f, 0.45f, 0.35f),
            Description = "Giant Stag Beetle (CR 4). Large beetle with crushing mandibles. MM 3.5e p.285."
        });
    }

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
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.35f, 0.25f, 0.15f, 1f),
            PanelColor = new Color(0.1f, 0.06f, 0.02f, 0.85f),
            NameColor = new Color(0.58f, 0.42f, 0.25f),
            Description = "Giant Worker Ant (CR 1). Hive-dwelling vermin with improved grab. MM 3.5e p.284."
        });
    }

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
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.55f, 0.35f, 0.4f, 1f),
            PanelColor = new Color(0.2f, 0.1f, 0.12f, 0.85f),
            NameColor = new Color(0.82f, 0.55f, 0.6f),
            Description = "Gibbering Mouther (CR 5). Mass of eyes and mouths that confuses and engulfs prey. MM 3.5e p.126."
        });
    }

    private static void RegisterGorgon()
    {
        Register(new NPCDefinition
        {
            Id = "gorgon",
            Name = "Gorgon",
            ChallengeRating = "8",
            Level = 8,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 8,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 21, DEX = 12, CON = 21, WIS = 12, INT = 2, CHA = 9,
            NaturalArmorBonus = 10,
            BaseSpeed = 6,
            BaseHitDieHP = 84,
            BAB = 8,
            HasScent = true,
            BreathWeapon = new BreathWeaponDefinition
            {
                Shape = BreathWeaponShape.Cone,
                RangeFeet = 60,
                DamageDice = 0, // No damage — petrification
                DamageCount = 0,
                DamageType = DamageType.Force, // Closest type for petrification
                SaveDC = 19,
                IsReflexSave = false, // Fort save
                RechargeRounds = 4
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Gore", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 2, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Magical Beast", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Iron Will", "Power Attack" },
            SpecialAbilities = new List<string> { "Breath Weapon (Su): 60 ft. cone, Fort DC 19 or petrified (permanent)", "Trample (Ex): 1d8+7, Ref DC 19 to halve", "Scent", "Darkvision 60 ft., Low-light vision" },
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.4f, 0.42f, 0.45f, 1f),
            PanelColor = new Color(0.12f, 0.14f, 0.16f, 0.85f),
            NameColor = new Color(0.65f, 0.68f, 0.72f),
            Description = "Gorgon (CR 8). Iron-skinned bull with petrifying breath. MM 3.5e p.137."
        });
    }

    private static void RegisterGrayOoze()
    {
        Register(new NPCDefinition
        {
            Id = "gray_ooze",
            Name = "Gray Ooze",
            ChallengeRating = "4",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Ooze",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 12, DEX = 1, CON = 21, WIS = 1, INT = 0, CHA = 1,
            NaturalArmorBonus = 0,
            IsMindless = true,
            BaseSpeed = 2, // 10 ft
            BaseHitDieHP = 31,
            BAB = 2,
            Immunities = new CreatureImmunities
            {
                immuneToFire = true,
                immuneToCold = true,
                immuneToMindAffecting = true,
                immuneToCriticalHits = true,
                immuneToSneakAttack = true
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true, BonusElementalDamageDice = 6, BonusElementalDamageCount = 1, BonusElementalDamageType = DamageType.Acid }
            },
            CreatureTags = new List<string> { "Ooze", "Transparent", "Blindsight60", "MM35" },
            Feats = new List<string>(),
            SpecialAbilities = new List<string> { "Acid (Ex): dissolves metal and organic on contact", "Transparent (Ex): DC 15 Spot to notice, surprise on failure", "Improved Grab", "Constrict 1d6+1 + 1d6 acid", "Immune to fire/cold", "Blindsight 60 ft.", "Ooze traits (mindless, immune to crits/sneak/mind-affecting)" },
            AIProfileArchetype = NPCAIProfileArchetype.UndeadMindless,
            SpriteColor = new Color(0.5f, 0.5f, 0.48f, 0.7f),
            PanelColor = new Color(0.18f, 0.18f, 0.16f, 0.85f),
            NameColor = new Color(0.75f, 0.75f, 0.7f),
            Description = "Gray Ooze (CR 4). Transparent acidic ooze that dissolves metal. MM 3.5e p.202."
        });
    }

    private static void RegisterGreaterShadow()
    {
        Register(new NPCDefinition
        {
            Id = "greater_shadow",
            Name = "Greater Shadow",
            ChallengeRating = "8",
            Level = 9,
            CharacterClass = "Warrior",
            CreatureType = "Undead",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 9,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 0, DEX = 16, CON = 0, WIS = 12, INT = 6, CHA = 14,
            NaturalArmorBonus = 0,
            IsIncorporeal = true,
            BaseSpeed = 8, // Fly 40 ft (good)
            BaseHitDieHP = 58,
            BAB = 4,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Incorporeal Touch", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.None, Range = 1, IsPrimary = true, AbilityDrainType = AbilityType.STR, AbilityDrainAmount = 8 }
            },
            CreatureTags = new List<string> { "Undead", "Incorporeal", "Darkvision60", "MM35" },
            Feats = new List<string> { "Dodge", "Improved Initiative", "Mobility", "Spring Attack" },
            SpecialAbilities = new List<string> { "Strength Damage (Su): 1d8 STR per touch", "Create Spawn (Su): humanoids reduced to 0 STR become shadows", "Incorporeal", "+2 turn resistance", "Darkvision 60 ft.", "Fly 40 ft. (good)" },
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.15f, 0.15f, 0.2f, 0.5f),
            PanelColor = new Color(0.05f, 0.05f, 0.1f, 0.85f),
            NameColor = new Color(0.4f, 0.4f, 0.55f),
            Description = "Greater Shadow (CR 8). Advanced incorporeal undead draining 1d8 STR per touch. MM 3.5e p.221."
        });
    }

    private static void RegisterGreenSlaad()
    {
        Register(new NPCDefinition
        {
            Id = "green_slaad",
            Name = "Green Slaad",
            ChallengeRating = "9",
            Level = 9,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.ChaoticNeutral,
            HitDice = 9,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 19, DEX = 15, CON = 19, WIS = 10, INT = 10, CHA = 10,
            NaturalArmorBonus = 8,
            DamageReductionAmount = 10,
            DamageReductionBypass = DamageBypassTag.Lawful,
            SpellResistance = 22,
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Acid, Amount = 5 },
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 5 },
                new DamageResistanceEntry { Type = DamageType.Electricity, Amount = 5 },
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 5 }
            },
            BaseSpeed = 6,
            BaseHitDieHP = 76,
            BAB = 9,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 2, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Outsider", "Chaotic", "Extraplanar", "Shapechanger", "Darkvision60", "MM35" },
            Feats = new List<string> { "Combat Reflexes", "Multiattack", "Power Attack" },
            SpecialAbilities = new List<string> { "Change Shape (Su): any humanoid form Small–Large", "Spell-Like: Chaos Hammer (at will, DC 14), Detect Magic (at will), Detect Thoughts (at will, DC 12), Deeper Darkness (3/day), Fear (3/day, DC 14), Fireball (3/day, DC 13), Protection from Law (at will), See Invisibility (constant), Shatter (at will, DC 12), Dispel Law (1/day, DC 15)", "DR 10/lawful", "SR 22", "Fast Healing 5", "Resist acid 5, cold 5, electricity 5, fire 5", "Summon Slaad: 40% chance 1 green slaad", "Darkvision 60 ft.", "Telepathy 100 ft." },
            AIProfileArchetype = NPCAIProfileArchetype.Spellcaster,
            SpriteColor = new Color(0.2f, 0.6f, 0.25f, 1f),
            PanelColor = new Color(0.04f, 0.22f, 0.06f, 0.85f),
            NameColor = new Color(0.35f, 0.85f, 0.4f),
            Description = "Green Slaad (CR 9). Intelligent shapeshifting slaad with spell-like abilities. MM 3.5e p.230."
        });
    }

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
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.4f, 0.42f, 0.38f, 1f),
            PanelColor = new Color(0.12f, 0.14f, 0.1f, 0.85f),
            NameColor = new Color(0.65f, 0.68f, 0.6f),
            Description = "Grick (CR 3). Worm-like aberration with DR 10/magic and four tentacles. MM 3.5e p.139."
        });
    }

    private static void RegisterGrimlock()
    {
        Register(new NPCDefinition
        {
            Id = "grimlock",
            Name = "Grimlock",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Monstrous Humanoid",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 16, DEX = 13, CON = 13, WIS = 8, INT = 10, CHA = 6,
            NaturalArmorBonus = 3,
            BaseSpeed = 6,
            BaseHitDieHP = 11,
            BAB = 2,
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Monstrous Humanoid", "Blindsight40", "MM35" },
            Feats = new List<string> { "Alertness", "Track" },
            SpecialAbilities = new List<string> { "Blindsight 40 ft.", "Immune to gaze attacks/visual effects/illusions", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("battleaxe", EquipSlot.MainHand)
            },
            AIProfileArchetype = NPCAIProfileArchetype.Berserk,
            SpriteColor = new Color(0.55f, 0.52f, 0.5f, 1f),
            PanelColor = new Color(0.2f, 0.18f, 0.16f, 0.85f),
            NameColor = new Color(0.8f, 0.75f, 0.7f),
            Description = "Grimlock (CR 1). Blind monstrous humanoid with blindsight 40 ft. MM 3.5e p.140."
        });
    }

    /// <summary>
    /// Green Hag (CR 5) — Medium monstrous humanoid.
    /// MM 3.5e p.143. Spell-like abilities, weakness aura, mimicry.
    /// 9d8+9 HP (49), 2 claws 1d4+4.
    /// </summary>
    private static void RegisterGreenHag()
    {
        Register(new NPCDefinition
        {
            Id = "green_hag",
            Name = "Green Hag",
            ChallengeRating = "5",
            Level = 9,
            CharacterClass = "Warrior",
            CreatureType = "Monstrous Humanoid",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 9,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 19, DEX = 12, CON = 12, WIS = 13, INT = 13, CHA = 14,
            NaturalArmorBonus = 11,
            BaseSpeed = 6, // 30 ft, swim 30 ft
            BaseHitDieHP = 49,
            BAB = 9,
            SpellResistance = 18,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Monstrous Humanoid", "MM35" },
            Feats = new List<string> { "Alertness", "Blind-Fight", "Combat Casting", "Great Fortitude" },
            SpecialAbilities = new List<string> { "Spell-Like Abilities: at will—dancing lights, disguise self, ghost sound, invisibility, pass without trace, tongues, water breathing", "Weakness (Su): 30 ft ray, DC 16 Fort or 2d4 Str damage", "Mimicry (Ex): imitate animal sounds or humanoid voices", "Darkvision 90 ft.", "SR 18" },
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.35f, 0.55f, 0.3f, 1f),
            PanelColor = new Color(0.1f, 0.22f, 0.08f, 0.85f),
            NameColor = new Color(0.5f, 0.8f, 0.45f),
            Description = "Green Hag (CR 5). Cunning hag with weakness ray and spell-like abilities. MM 3.5e p.143."
        });
    }

    /// <summary>
    /// Gauth (CR 6) — Medium aberration.
    /// MM 3.5e p.26. Lesser beholder-kin with eye rays.
    /// 6d8+18 HP (45), bite 1d6.
    /// </summary>
    private static void RegisterGauth()
    {
        Register(new NPCDefinition
        {
            Id = "gauth",
            Name = "Gauth",
            ChallengeRating = "6",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 6,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 10, DEX = 14, CON = 16, WIS = 15, INT = 15, CHA = 13,
            NaturalArmorBonus = 7,
            BaseSpeed = 1, // 5 ft, fly 20 ft (good)
            BaseHitDieHP = 45,
            BAB = 4,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Aberration", "Fly", "MM35" },
            Feats = new List<string> { "Alertness", "Flyby Attack", "Iron Will" },
            SpecialAbilities = new List<string> { "Eye Rays (Su): 6 eye stalks—sleep, inflict moderate wounds, dispel magic, scorching ray, paralysis, exhaustion", "Stunning Gaze (Su): 30 ft, DC 16 Will or stunned 1 round", "All-Around Vision: +4 Spot, cannot be flanked", "Fly 20 ft (good)", "Darkvision 60 ft." },
            AIBehavior = NPCAIBehavior.Ranged,
            AIProfileArchetype = NPCAIProfileArchetype.None,
            SpriteColor = new Color(0.6f, 0.5f, 0.65f, 1f),
            PanelColor = new Color(0.2f, 0.15f, 0.25f, 0.85f),
            NameColor = new Color(0.8f, 0.7f, 0.88f),
            Description = "Gauth (CR 6). Lesser beholder-kin with six eye rays and stunning gaze. MM 3.5e p.26."
        });
    }

    /// <summary>
    /// Goblin Warrior (CR 1/3) — Small humanoid (goblinoid), Warrior 1.
    /// MM 3.5e p.133. Alias variant of goblin for encounter table compatibility.
    /// </summary>
    private static void RegisterGoblinWarrior()
    {
        Register(new NPCDefinition
        {
            Id = "goblin_warrior",
            Name = "Goblin Warrior",
            ChallengeRating = "1/3",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            CharacterAlignment = Alignment.NeutralEvil,
            HitDice = 1,
            BaseAttackBonusOverride = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 11, DEX = 13, CON = 12, WIS = 9, INT = 10, CHA = 6,
            NaturalArmorBonus = 0,
            BaseSpeed = 6, // 30 ft
            BaseHitDieHP = 5,
            CreatureTags = new List<string> { "Goblinoid", "MM35" },
            Feats = new List<string> { "Alertness" },
            SpecialAbilities = new List<string> { "Darkvision 60 ft", "Skills: Hide +5, Listen +2, Move Silently +5, Ride +4, Spot +2" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.LEATHER_ARMOR, EquipSlot.Armor),
                new EquipmentSlotPair(ItemIDs.MORNINGSTAR, EquipSlot.RightHand),
                new EquipmentSlotPair(ItemIDs.SHIELD_LIGHT_WOODEN, EquipSlot.LeftHand)
            },
            BackpackItemIds = new List<string> { ItemIDs.JAVELIN },
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.56f, 0.78f, 0.28f, 1f),
            PanelColor = new Color(0.33f, 0.1f, 0.1f, 0.85f),
            NameColor = new Color(0.95f, 0.45f, 0.45f),
            Description = "Goblin Warrior (CR 1/3). Small goblinoid skirmisher. MM 3.5e p.133."
        });
    }

    /// <summary>
    /// Girallon (CR 6) — Large magical beast. Four-armed ape with rend.
    /// MM 3.5e p.126. 7d10+20 HP (58), 4 claws 1d4+6, bite 1d8+3.
    /// Rend: If 2+ claws hit same target, deals 2d4+12 extra damage.
    /// </summary>
    private static void RegisterGirallon()
    {
        Register(new NPCDefinition
        {
            Id = "girallon",
            Name = "Girallon",
            ChallengeRating = "6",
            Level = 7,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            CharacterAlignment = Alignment.None,
            HitDice = 7,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 22, DEX = 15, CON = 14, WIS = 12, INT = 2, CHA = 7,
            NaturalArmorBonus = 4,
            BaseSpeed = 8, // 40 ft.
            BaseHitDieHP = 58,
            BAB = 7,
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 4,
                    BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true
                },
                new NaturalAttackDefinition
                {
                    Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1,
                    BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 2, IsPrimary = false
                }
            },
            CreatureTags = new List<string> { "MagicalBeast", "Darkvision60", "MM35" },
            Feats = new List<string> { "Iron Will", "Toughness" },
            SpecialAbilities = new List<string> { "Rend (Ex): if 2+ claws hit same target, auto 2d4+12 extra damage", "Darkvision 60 ft.", "Low-light vision", "Scent" },
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.45f, 0.4f, 0.35f, 1f),
            PanelColor = new Color(0.2f, 0.15f, 0.12f, 0.85f),
            NameColor = new Color(0.7f, 0.6f, 0.5f),
            Description = "Girallon (CR 6). Four-armed ape with rend attack. Darkvision, scent. MM 3.5e p.126."
        });
    }

    /// <summary>
    /// Ghost (CR 7) — Medium undead (incorporeal). Template applied to 5th-level human warrior.
    /// MM 3.5e p.117. Incorporeal touch with manifestation and frightful moan.
    /// </summary>
    private static void RegisterGhost()
    {
        Register(new NPCDefinition
        {
            Id = "ghost",
            Name = "Ghost",
            ChallengeRating = "7",
            Level = 5,
            CharacterClass = "Warrior",
            CreatureType = "Undead",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 5,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 0, DEX = 14, CON = 0, WIS = 14, INT = 12, CHA = 16,
            NaturalArmorBonus = 0,
            BaseSpeed = 6, // Fly 30 ft (perfect)
            BaseHitDieHP = 32,
            BAB = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Incorporeal Touch", DamageDice = 6, DamageCount = 1, Count = 1,
                    BonusDamageSource = DamageBonusSource.None,
                    Range = 1, IsPrimary = true,
                    AbilityDrainType = AbilityType.None,
                    EnergyDrainOnHit = 1, EnergyDrainRemovalDC = 16
                }
            },
            IsIncorporeal = true,
            CreatureTags = new List<string> { "Undead", "Incorporeal", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Dodge", "Improved Initiative" },
            SpecialAbilities = new List<string>
            {
                "Incorporeal: 50% miss chance from corporeal attacks",
                "Manifestation (Su): can appear as visible but still incorporeal",
                "Frightful Moan (Su): 30 ft radius, Will DC 16 or panicked 2d4 rounds",
                "Corrupting Touch (Su): 1d6 damage ignoring armor",
                "Draining Touch (Su): 1d4 ability damage to any score",
                "Rejuvenation (Su): reforms in 2d4 days unless quest resolved",
                "Turn Resistance +4",
                "Darkvision 60 ft., Fly 30 ft. (perfect)"
            },
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.5f, 0.5f, 0.6f, 0.4f),
            PanelColor = new Color(0.1f, 0.1f, 0.2f, 0.85f),
            NameColor = new Color(0.7f, 0.7f, 0.9f),
            Description = "Ghost (CR 7). Incorporeal undead. Touch drains energy. Frightful moan panics foes. Rejuvenates unless laid to rest. MM 3.5e p.117."
        });
    }

    /// <summary>
    /// Giant Crocodile (CR 4) — Huge animal. SNA IV.
    /// MM 3.5e p.271. Improved grab on bite. Hold breath.
    /// 7d8+28 HP (59), bite +11 (2d8+12) or tail slap +11 (1d12+12).
    /// </summary>
    private static void RegisterGiantCrocodile()
    {
        Register(new NPCDefinition
        {
            Id = "giant_crocodile",
            Name = "Giant Crocodile",
            ChallengeRating = "4",
            Level = 7,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 7,
            SizeCategory = SizeCategory.Huge,
            IsTallCreature = false,
            STR = 27, DEX = 10, CON = 19, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 8,
            BaseSpeed = 4, // 20 ft. land, swim 30 ft.
            BaseHitDieHP = 59,
            BAB = 5,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Bite",
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 2, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Animal", "Aquatic", "SummonBase", "MM35" },
            Feats = new List<string> { "Alertness", "Endurance", "Skill Focus (Hide)" },
            SpecialAbilities = new List<string> { "Improved grab (bite)", "Hold breath (114 rounds)", "Low-light vision", "Tail slap +11 (1d12+12) — alternate attack", "Swim 30 ft." },
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.35f, 0.48f, 0.28f, 1f),
            PanelColor = new Color(0.1f, 0.18f, 0.08f, 0.85f),
            NameColor = new Color(0.78f, 0.9f, 0.7f),
            Description = "Giant Crocodile (CR 4). Massive reptile with crushing bite and improved grab. Hold breath 114 rounds. MM 3.5e p.271."
        });
    }

    }
}
