using System.Collections.Generic;
using UnityEngine;

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
                    BonusDamageSource = DamageBonusSource.StrengthFull,
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
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
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
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
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
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
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
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
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
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
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
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
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
