using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual miscellaneous creatures — monstrous humanoids, fey, vermin, and other types
/// not covered by the specialized files.
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_Misc()
    {
        // Monstrous Humanoids / Shapechanger
        RegisterDoppelganger();
        RegisterHarpy();
        RegisterMedusa();
        RegisterMinotaur();
        RegisterDrider();

        // Magical Beast (misc)
        RegisterPhaseSpider();
        RegisterWillOWisp();
        RegisterInvisibleStalker();
        RegisterEtherealFilcher();
        RegisterEtherealMarauder();
        RegisterDarkNaga();

        // Slaadi
        RegisterRedSlaad();
        RegisterBlueSlaad();
        RegisterGreenSlaad();

        // Xorn
        RegisterMinorXorn();
        RegisterAverageXorn();

        // Salamander
        RegisterFlamebrotherSalamander();
        RegisterAverageSalamander();

        // Yuan-ti
        RegisterYuantiPureblood();
        RegisterYuantiHalfblood();
        RegisterYuantiAbomination();
    }

    // ════════════════════════════════════════════════════════════
    //  Doppelganger — MM p.67
    //  Monstrous Humanoid (Shapechanger), Medium, CR 3
    //  4d8+4 HP (22), 2 slams +5 melee (1d6+1)
    //  Str 12, Dex 13, Con 12, Int 13, Wis 14, Cha 13
    //  AC 15 (+1 Dex, +4 natural), Detect Thoughts, Change Shape
    // ════════════════════════════════════════════════════════════
    private static void RegisterDoppelganger()
    {
        Register(new NPCDefinition
        {
            Id = "doppelganger",
            Name = "Doppelganger",
            ChallengeRating = "3",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Monstrous Humanoid",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 4,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 12, DEX = 13, CON = 12, WIS = 14, INT = 13, CHA = 13,
            NaturalArmorBonus = 4,
            BaseSpeed = 6,
            BaseHitDieHP = 22,
            BAB = 4,
            Immunities = new CreatureImmunities { immuneToMindAffecting = true },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Monstrous Humanoid", "Shapechanger", "Darkvision60", "MM35" },
            Feats = new List<string> { "Dodge", "Great Fortitude" },
            SpecialAbilities = new List<string> { "Change Shape (Su): any Small–Large humanoid", "Detect Thoughts (Su): constant, Will DC 13", "Immune to sleep/charm", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.55f, 0.55f, 0.55f, 1f),
            PanelColor = new Color(0.2f, 0.2f, 0.2f, 0.85f),
            NameColor = new Color(0.8f, 0.8f, 0.8f),
            Description = "Doppelganger (CR 3). Shapechanger that reads minds and mimics humanoids. MM 3.5e p.67."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Harpy — MM p.150
    //  Monstrous Humanoid, Medium, CR 4
    //  7d8 HP (31), 2 claws +7 melee (1d3), club +7 melee (1d6)
    //  Str 10, Dex 13, Con 10, Int 7, Wis 10, Cha 17
    //  AC 13 (+1 Dex, +2 natural)
    //  Captivating Song: 300 ft., Will DC 16 or captivated (approach)
    // ════════════════════════════════════════════════════════════
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

    // ════════════════════════════════════════════════════════════
    //  Medusa — MM p.180
    //  Monstrous Humanoid, Medium, CR 7
    //  6d8+6 HP (33), shortbow +8/+3 ranged (1d6), dagger +8/+3 melee (1d4), snakes +3 melee (1d4 + poison)
    //  Str 10, Dex 15, Con 12, Int 12, Wis 13, Cha 15
    //  AC 15 (+2 Dex, +3 natural)
    //  Petrifying Gaze: 30 ft., Fort DC 15 or permanently petrified
    //  Snake Poison: DC 14 Fort, 1d6 Str/2d6 Str
    // ════════════════════════════════════════════════════════════
    private static void RegisterMedusa()
    {
        Register(new NPCDefinition
        {
            Id = "medusa",
            Name = "Medusa",
            ChallengeRating = "7",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Monstrous Humanoid",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 6,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 10, DEX = 15, CON = 12, WIS = 13, INT = 12, CHA = 15,
            NaturalArmorBonus = 3,
            BaseSpeed = 6,
            BaseHitDieHP = 33,
            BAB = 6,
            AuraAbility = new AuraAbilityDefinition
            {
                Name = "Petrifying Gaze",
                SaveDC = 15,
                IsWillSave = false, // Fort save
                RangeFeet = 30,
                Effect = AuraEffectType.Frightened, // Closest to petrification
                DurationRounds = 999
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Snakes", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.None, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Monstrous Humanoid", "Darkvision60", "MM35" },
            Feats = new List<string> { "Point Blank Shot", "Precise Shot", "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Petrifying Gaze (Su): 30 ft., Fort DC 15 or permanently turned to stone", "Poison (Ex): snakes, DC 14 Fort, 1d6 Str/2d6 Str", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("dagger", EquipSlot.MainHand),
                new EquipmentSlotPair("shortbow", EquipSlot.Ranged)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Ranged,
            SpriteColor = new Color(0.4f, 0.5f, 0.35f, 1f),
            PanelColor = new Color(0.12f, 0.18f, 0.1f, 0.85f),
            NameColor = new Color(0.62f, 0.78f, 0.55f),
            Description = "Medusa (CR 7). Snake-haired woman with petrifying gaze and poison. MM 3.5e p.180."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Minotaur — MM p.188
    //  Monstrous Humanoid, Large, CR 4
    //  6d8+12 HP (39), greataxe +9/+4 melee (3d6+6) or gore +9 melee (1d8+4)
    //  Str 19, Dex 10, Con 15, Int 7, Wis 10, Cha 8
    //  AC 14 (-1 size, +5 natural)
    //  Powerful Charge: gore 4d6+6, Natural Cunning (never lost)
    // ════════════════════════════════════════════════════════════
    private static void RegisterMinotaur()
    {
        Register(new NPCDefinition
        {
            Id = "minotaur",
            Name = "Minotaur",
            ChallengeRating = "4",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Monstrous Humanoid",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 6,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 19, DEX = 10, CON = 15, WIS = 10, INT = 7, CHA = 8,
            NaturalArmorBonus = 5,
            BaseSpeed = 6,
            BaseHitDieHP = 39,
            BAB = 6,
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Gore", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Monstrous Humanoid", "Darkvision60", "MM35" },
            Feats = new List<string> { "Great Fortitude", "Power Attack", "Track" },
            SpecialAbilities = new List<string> { "Powerful Charge (Ex): gore 4d6+6 on charge", "Natural Cunning (Ex): immune to maze spells, never gets lost", "Scent", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("greataxe", EquipSlot.MainHand)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.5f, 0.38f, 0.28f, 1f),
            PanelColor = new Color(0.18f, 0.12f, 0.06f, 0.85f),
            NameColor = new Color(0.78f, 0.6f, 0.45f),
            Description = "Minotaur (CR 4). Bull-headed brute with powerful charge. MM 3.5e p.188."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Drider — MM p.89
    //  Aberration, Large, CR 7
    //  6d8+18 HP (45), dagger +6 melee (1d6+1) or shortbow +6 ranged (1d6), bite +1 melee (1d4 + poison)
    //  Str 15, Dex 15, Con 16, Int 15, Wis 16, Cha 16
    //  AC 17 (-1 size, +2 Dex, +6 natural), SR 17
    //  Spells as 6th-level cleric or wizard, poison
    // ════════════════════════════════════════════════════════════
    private static void RegisterDrider()
    {
        Register(new NPCDefinition
        {
            Id = "drider",
            Name = "Drider",
            ChallengeRating = "7",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 6,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 15, DEX = 15, CON = 16, WIS = 16, INT = 15, CHA = 16,
            NaturalArmorBonus = 6,
            SpellResistance = 17,
            BaseSpeed = 6, // 30 ft, climb 15 ft
            BaseHitDieHP = 45,
            BAB = 4,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Aberration", "Darkvision60", "MM35" },
            Feats = new List<string> { "Combat Casting", "Two-Weapon Fighting", "Weapon Focus (dagger)" },
            SpecialAbilities = new List<string> { "Spells: as 6th-level cleric or wizard (sorcerer)", "Poison (Ex): bite, DC 16 Fort, 1d6 Str/1d6 Str", "SR 17", "Spell-Like: Dancing Lights, Clairaudience/Clairvoyance, Darkness, Detect Good/Law/Magic, Dispel Magic, Faerie Fire, Levitate, Suggestion (1/day each)", "Darkvision 60 ft., Climb 15 ft." },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("dagger", EquipSlot.MainHand),
                new EquipmentSlotPair("shortbow", EquipSlot.Ranged)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Spellcaster,
            SpriteColor = new Color(0.35f, 0.28f, 0.4f, 1f),
            PanelColor = new Color(0.1f, 0.06f, 0.15f, 0.85f),
            NameColor = new Color(0.58f, 0.45f, 0.68f),
            Description = "Drider (CR 7). Drow-spider hybrid with spellcasting and poison. MM 3.5e p.89."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Phase Spider — MM p.207
    //  Magical Beast, Large, CR 5
    //  5d10+15 HP (42), bite +7 melee (1d6+4 + poison)
    //  Str 17, Dex 15, Con 16, Int 7, Wis 13, Cha 10
    //  AC 15 (-1 size, +2 Dex, +4 natural)
    //  Ethereal Jaunt at will, Poison DC 17 (1d6 Con/1d6 Con)
    // ════════════════════════════════════════════════════════════
    private static void RegisterPhaseSpider()
    {
        Register(new NPCDefinition
        {
            Id = "phase_spider",
            Name = "Phase Spider",
            ChallengeRating = "5",
            Level = 5,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 5,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 17, DEX = 15, CON = 16, WIS = 13, INT = 7, CHA = 10,
            NaturalArmorBonus = 4,
            BaseSpeed = 8, // 40 ft, climb 20 ft
            BaseHitDieHP = 42,
            BAB = 5,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 2, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Magical Beast", "Darkvision60", "MM35" },
            Feats = new List<string> { "Ability Focus (poison)", "Improved Initiative" },
            SpecialAbilities = new List<string> { "Ethereal Jaunt (Su): at will, as ethereal jaunt (CL 15)", "Poison (Ex): bite, DC 17 Fort, 1d6 Con/1d6 Con", "Darkvision 60 ft., Low-light vision", "Tremorsense 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.4f, 0.35f, 0.5f, 0.8f),
            PanelColor = new Color(0.12f, 0.1f, 0.2f, 0.85f),
            NameColor = new Color(0.62f, 0.55f, 0.78f),
            Description = "Phase Spider (CR 5). Spider that shifts between Ethereal and Material planes. MM 3.5e p.207."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Will-o'-Wisp — MM p.255
    //  Aberration (Air), Small, CR 6
    //  9d8 HP (40), shock +16 melee touch (2d8 electricity)
    //  Str 1, Dex 29, Con 10, Int 15, Wis 16, Cha 12
    //  AC 29 (+1 size, +9 Dex, +9 deflection), Natural Invisibility
    //  Immune to all spells/SLAs except magic missile, maze, protection from evil
    // ════════════════════════════════════════════════════════════
    private static void RegisterWillOWisp()
    {
        Register(new NPCDefinition
        {
            Id = "will_o_wisp",
            Name = "Will-o'-Wisp",
            ChallengeRating = "6",
            Level = 9,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 9,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 1, DEX = 29, CON = 10, WIS = 16, INT = 15, CHA = 12,
            NaturalArmorBonus = 0,
            BaseSpeed = 10, // Fly 50 ft (perfect)
            BaseHitDieHP = 40,
            BAB = 6,
            Immunities = new CreatureImmunities { immuneToElectricity = true },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Shock", DamageDice = 8, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.None, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Aberration", "Air", "NaturalInvisibility", "Fly50", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Blind-Fight", "Dodge", "Improved Initiative", "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Immunity to Magic (Ex): immune to all spells except magic missile, maze, protection from evil", "Natural Invisibility (Ex): can suppress/resume as free action", "Immune to electricity", "Fly 50 ft. (perfect)", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.7f, 0.8f, 0.5f, 0.6f),
            PanelColor = new Color(0.25f, 0.3f, 0.15f, 0.85f),
            NameColor = new Color(0.9f, 0.95f, 0.7f),
            Description = "Will-o'-Wisp (CR 6). Glowing aberration nearly immune to magic. MM 3.5e p.255."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Invisible Stalker — MM p.160
    //  Elemental (Air, Extraplanar), Large, CR 7
    //  8d8+16 HP (52), slam +10 melee (2d6+4) × 2
    //  Str 18, Dex 19, Con 14, Int 14, Wis 15, Cha 11
    //  AC 17 (-1 size, +4 Dex, +4 natural), Natural Invisibility
    //  Improved Tracking
    // ════════════════════════════════════════════════════════════
    private static void RegisterInvisibleStalker()
    {
        Register(new NPCDefinition
        {
            Id = "invisible_stalker",
            Name = "Invisible Stalker",
            ChallengeRating = "7",
            Level = 8,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 8,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 18, DEX = 19, CON = 14, WIS = 15, INT = 14, CHA = 11,
            NaturalArmorBonus = 4,
            BaseSpeed = 6, // 30 ft, fly 30 ft (perfect)
            BaseHitDieHP = 52,
            BAB = 8,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 6, DamageCount = 2, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Outsider", "Air", "Extraplanar", "NaturalInvisibility", "Fly30", "Darkvision60", "MM35" },
            Feats = new List<string> { "Combat Reflexes", "Improved Initiative", "Weapon Focus (slam)" },
            SpecialAbilities = new List<string> { "Natural Invisibility (Su): constant, even when attacking", "Improved Tracking (Ex): can track creatures through air", "Fly 30 ft. (perfect)", "Darkvision 60 ft.", "Elemental traits (immune to poison, sleep, paralysis, stun, crits, flanking)" },
            Immunities = new CreatureImmunities { immuneToPoison = true, immuneToCriticalHits = true },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.6f, 0.65f, 0.7f, 0.3f),
            PanelColor = new Color(0.2f, 0.22f, 0.25f, 0.85f),
            NameColor = new Color(0.82f, 0.85f, 0.9f),
            Description = "Invisible Stalker (CR 7). Permanently invisible air elemental tracker. MM 3.5e p.160."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Ethereal Filcher — MM p.104
    //  Aberration, Medium, CR 3
    //  5d8 HP (22), bite +3 melee (1d4)
    //  Str 10, Dex 14, Con 11, Int 7, Wis 12, Cha 10
    //  AC 15 (+2 Dex, +3 natural)
    //  Ethereal Jaunt (at will), steals items
    // ════════════════════════════════════════════════════════════
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

    // ════════════════════════════════════════════════════════════
    //  Ethereal Marauder — MM p.105
    //  Magical Beast (Extraplanar), Medium, CR 3
    //  2d10+2 HP (13), bite +4 melee (1d6+1)
    //  Str 13, Dex 15, Con 13, Int 7, Wis 12, Cha 10
    //  AC 14 (+2 Dex, +2 natural)
    //  Ethereal Jaunt at will, ambush predator
    // ════════════════════════════════════════════════════════════
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

    // ════════════════════════════════════════════════════════════
    //  Dark Naga — MM p.191
    //  Aberration, Large, CR 8
    //  9d8+18 HP (58), sting +7 melee (2d4+2 + poison), bite +2 melee (1d4+1)
    //  Str 14, Dex 15, Con 14, Int 16, Wis 15, Cha 17
    //  AC 14 (-1 size, +2 Dex, +3 natural)
    //  Poison: DC 16 Fort, sleep 2d4 min/1d4 Con
    //  Spells as 7th-level sorcerer, Detect Thoughts, Guarded Thoughts
    // ════════════════════════════════════════════════════════════
    private static void RegisterDarkNaga()
    {
        Register(new NPCDefinition
        {
            Id = "dark_naga",
            Name = "Dark Naga",
            ChallengeRating = "8",
            Level = 9,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 9,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 14, DEX = 15, CON = 14, WIS = 15, INT = 16, CHA = 17,
            NaturalArmorBonus = 3,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 58,
            BAB = 6,
            Immunities = new CreatureImmunities { immuneToPoison = true },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Sting", DamageDice = 4, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 2, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Aberration", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Combat Casting", "Dodge", "Eschew Materials", "Lightning Reflexes" },
            SpecialAbilities = new List<string> { "Poison (Ex): sting, DC 16 Fort, sleep 2d4 min / 1d4 Con", "Spells: as 7th-level sorcerer", "Detect Thoughts (Su): continuous, Will DC 15", "Guarded Thoughts (Ex): immune to any mind-reading", "Immune to poison", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Spellcaster,
            SpriteColor = new Color(0.3f, 0.25f, 0.35f, 1f),
            PanelColor = new Color(0.08f, 0.06f, 0.12f, 0.85f),
            NameColor = new Color(0.52f, 0.42f, 0.6f),
            Description = "Dark Naga (CR 8). Serpentine spellcaster with poison and sorcerer spells. MM 3.5e p.191."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Red Slaad — MM p.228
    //  Outsider (Chaotic, Extraplanar), Large, CR 7
    //  7d8+21 HP (52), bite +8 melee (2d8+3), 2 claws +6 melee (1d4+1)
    //  Str 17, Dex 14, Con 17, Int 6, Wis 6, Cha 9
    //  AC 16 (-1 size, +2 Dex, +5 natural)
    //  Pounce, Stunning Croak: 20 ft., Fort DC 16 or stunned 1 round
    //  Implant egg on claw hit
    // ════════════════════════════════════════════════════════════
    private static void RegisterRedSlaad()
    {
        Register(new NPCDefinition
        {
            Id = "red_slaad",
            Name = "Red Slaad",
            ChallengeRating = "7",
            Level = 7,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.ChaoticNeutral,
            HitDice = 7,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 17, DEX = 14, CON = 17, WIS = 6, INT = 6, CHA = 9,
            NaturalArmorBonus = 5,
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Acid, Amount = 5 },
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 5 },
                new DamageResistanceEntry { Type = DamageType.Electricity, Amount = 5 },
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 5 }
            },
            BaseSpeed = 6,
            BaseHitDieHP = 52,
            BAB = 7,
            HasPounce = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 2, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Outsider", "Chaotic", "Extraplanar", "Darkvision60", "MM35" },
            Feats = new List<string> { "Multiattack", "Power Attack" },
            SpecialAbilities = new List<string> { "Pounce (Ex): full attack on charge", "Stunning Croak (Su): 20 ft., Fort DC 16 or stunned 1 round, 1/hour", "Implant (Ex): claw hit may implant egg (no save)", "Summon Slaad: 40% chance 1 red slaad", "Fast Healing 5", "Resist acid 5, cold 5, electricity 5, fire 5", "Darkvision 60 ft.", "Telepathy 100 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Berserk,
            SpriteColor = new Color(0.7f, 0.25f, 0.2f, 1f),
            PanelColor = new Color(0.28f, 0.06f, 0.04f, 0.85f),
            NameColor = new Color(0.95f, 0.4f, 0.3f),
            Description = "Red Slaad (CR 7). Chaotic toad-like outsider with pounce and egg implantation. MM 3.5e p.228."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Blue Slaad — MM p.229
    //  Outsider (Chaotic, Extraplanar), Large, CR 8
    //  8d8+32 HP (68), 4 claws +11 melee (2d6+4), bite +9 melee (2d8+2)
    //  Str 19, Dex 15, Con 19, Int 6, Wis 6, Cha 9
    //  AC 18 (-1 size, +2 Dex, +7 natural), DR 5/lawful
    //  Claw rake infects with chaos phage (becomes red slaad)
    // ════════════════════════════════════════════════════════════
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

    // ════════════════════════════════════════════════════════════
    //  Green Slaad — MM p.230
    //  Outsider (Chaotic, Extraplanar), Large, CR 9
    //  9d8+36 HP (76), bite +12 melee (2d8+4), 2 claws +10 melee (1d6+2)
    //  Str 19, Dex 15, Con 19, Int 10, Wis 10, Cha 10
    //  AC 19 (-1 size, +2 Dex, +8 natural), DR 10/lawful, SR 22
    //  Change Shape, at-will spell-likes, SLA: chaos hammer, deeper darkness, detect magic, detect thoughts, fear, fireball, protection from law, see invisibility, shatter
    // ════════════════════════════════════════════════════════════
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
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Spellcaster,
            SpriteColor = new Color(0.2f, 0.6f, 0.25f, 1f),
            PanelColor = new Color(0.04f, 0.22f, 0.06f, 0.85f),
            NameColor = new Color(0.35f, 0.85f, 0.4f),
            Description = "Green Slaad (CR 9). Intelligent shapeshifting slaad with spell-like abilities. MM 3.5e p.230."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Minor Xorn — MM p.260
    //  Outsider (Earth, Extraplanar), Small, CR 3
    //  3d8+9 HP (22), bite +6 melee (2d8+2), 3 claws +4 melee (1d3+1)
    //  Str 15, Dex 10, Con 17, Int 10, Wis 11, Cha 10
    //  AC 23 (+1 size, +12 natural), DR 5/bludgeoning, Resist cold/fire 10
    //  All-Around Vision, Tremorsense 60 ft., Earth Glide
    // ════════════════════════════════════════════════════════════
    private static void RegisterMinorXorn()
    {
        Register(new NPCDefinition
        {
            Id = "minor_xorn",
            Name = "Minor Xorn",
            ChallengeRating = "3",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 3,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 15, DEX = 10, CON = 17, WIS = 11, INT = 10, CHA = 10,
            NaturalArmorBonus = 12,
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Bludgeoning,
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 10 }
            },
            Immunities = new CreatureImmunities { immuneToElectricity = true },
            BaseSpeed = 4, // 20 ft, burrow 20 ft
            BaseHitDieHP = 22,
            BAB = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 3, DamageCount = 1, Count = 3, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Outsider", "Earth", "Extraplanar", "Tremorsense60", "Darkvision60", "MM35" },
            Feats = new List<string> { "Improved Initiative", "Multiattack", "Toughness" },
            SpecialAbilities = new List<string> { "Earth Glide (Ex): burrow through stone, earth, metal as through water", "All-Around Vision (Ex): +4 Spot/Search, cannot be flanked", "Tremorsense 60 ft.", "DR 5/bludgeoning", "Immune to electricity", "Resist cold 10, fire 10", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.55f, 0.48f, 0.35f, 1f),
            PanelColor = new Color(0.2f, 0.16f, 0.1f, 0.85f),
            NameColor = new Color(0.8f, 0.72f, 0.55f),
            Description = "Minor Xorn (CR 3). Small earth outsider with earth glide and high natural armor. MM 3.5e p.260."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Average Xorn — MM p.260
    //  Outsider (Earth, Extraplanar), Medium, CR 6
    //  7d8+17 HP (48), bite +10 melee (4d6+3), 3 claws +8 melee (1d4+1)
    //  Str 17, Dex 10, Con 15, Int 10, Wis 11, Cha 10
    //  AC 24 (+14 natural), DR 5/bludgeoning, Resist cold/fire 10
    // ════════════════════════════════════════════════════════════
    private static void RegisterAverageXorn()
    {
        Register(new NPCDefinition
        {
            Id = "average_xorn",
            Name = "Average Xorn",
            ChallengeRating = "6",
            Level = 7,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 7,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 17, DEX = 10, CON = 15, WIS = 11, INT = 10, CHA = 10,
            NaturalArmorBonus = 14,
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Bludgeoning,
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 10 }
            },
            Immunities = new CreatureImmunities { immuneToElectricity = true },
            BaseSpeed = 4, // 20 ft, burrow 20 ft
            BaseHitDieHP = 48,
            BAB = 7,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 4, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 3, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Outsider", "Earth", "Extraplanar", "Tremorsense60", "Darkvision60", "MM35" },
            Feats = new List<string> { "Cleave", "Improved Initiative", "Multiattack", "Power Attack", "Toughness" },
            SpecialAbilities = new List<string> { "Earth Glide (Ex): as minor xorn", "All-Around Vision (Ex): cannot be flanked", "Tremorsense 60 ft.", "DR 5/bludgeoning", "Immune to electricity", "Resist cold 10, fire 10", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.5f, 0.45f, 0.32f, 1f),
            PanelColor = new Color(0.18f, 0.15f, 0.08f, 0.85f),
            NameColor = new Color(0.78f, 0.7f, 0.52f),
            Description = "Average Xorn (CR 6). Medium earth outsider with earth glide and massive natural armor. MM 3.5e p.260."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Flamebrother Salamander — MM p.218
    //  Outsider (Fire, Extraplanar), Small, CR 3
    //  3d8+3 HP (16), spear +5 melee (1d6+1 + 1d6 fire), tail slap +0 melee (1d4 + 1d6 fire)
    //  Str 12, Dex 13, Con 12, Int 10, Wis 10, Cha 10
    //  AC 19 (+1 size, +1 Dex, +7 natural)
    //  Fire subtype (immune fire, vulnerable cold), Heat, Constrict
    // ════════════════════════════════════════════════════════════
    private static void RegisterFlamebrotherSalamander()
    {
        Register(new NPCDefinition
        {
            Id = "flamebrother_salamander",
            Name = "Flamebrother Salamander",
            ChallengeRating = "3",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.NeutralEvil,
            HitDice = 3,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 12, DEX = 13, CON = 12, WIS = 10, INT = 10, CHA = 10,
            NaturalArmorBonus = 7,
            Immunities = new CreatureImmunities { immuneToFire = true },
            BaseSpeed = 4, // 20 ft
            BaseHitDieHP = 16,
            BAB = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Tail Slap", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false, BonusElementalDamageDice = 6, BonusElementalDamageCount = 1, BonusElementalDamageType = DamageType.Fire }
            },
            CreatureTags = new List<string> { "Outsider", "Fire", "Extraplanar", "Darkvision60", "MM35" },
            Feats = new List<string> { "Multiattack" },
            SpecialAbilities = new List<string> { "Heat (Ex): +1d6 fire on melee and constrict", "Constrict (Ex): 1d4 + 1d6 fire", "Immune to fire", "Vulnerable to cold (×1.5 damage)", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("spear", EquipSlot.MainHand)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.8f, 0.45f, 0.15f, 1f),
            PanelColor = new Color(0.3f, 0.15f, 0.03f, 0.85f),
            NameColor = new Color(0.95f, 0.65f, 0.25f),
            Description = "Flamebrother Salamander (CR 3). Small fire salamander with heat aura. MM 3.5e p.218."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Average Salamander — MM p.218
    //  Outsider (Fire, Extraplanar), Medium, CR 6
    //  7d8+14 HP (45), +1 longspear +11/+6 melee (1d8+4+1d6 fire), tail slap +6 melee (2d6+1+1d6 fire)
    //  Str 16, Dex 13, Con 14, Int 14, Wis 15, Cha 13
    //  AC 18 (+1 Dex, +7 natural), Constrict 2d6+1+1d6 fire
    // ════════════════════════════════════════════════════════════
    private static void RegisterAverageSalamander()
    {
        Register(new NPCDefinition
        {
            Id = "average_salamander",
            Name = "Average Salamander",
            ChallengeRating = "6",
            Level = 7,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.NeutralEvil,
            HitDice = 7,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 16, DEX = 13, CON = 14, WIS = 15, INT = 14, CHA = 13,
            NaturalArmorBonus = 7,
            Immunities = new CreatureImmunities { immuneToFire = true },
            DamageReductionAmount = 10,
            DamageReductionBypass = DamageBypassTag.Magic,
            BaseSpeed = 4, // 20 ft
            BaseHitDieHP = 45,
            BAB = 7,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Tail Slap", DamageDice = 6, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 2, IsPrimary = false, BonusElementalDamageDice = 6, BonusElementalDamageCount = 1, BonusElementalDamageType = DamageType.Fire }
            },
            CreatureTags = new List<string> { "Outsider", "Fire", "Extraplanar", "Darkvision60", "MM35" },
            Feats = new List<string> { "Cleave", "Multiattack", "Power Attack" },
            SpecialAbilities = new List<string> { "Heat (Ex): +1d6 fire on all melee attacks", "Constrict (Ex): 2d6+1 + 1d6 fire", "DR 10/magic", "Immune to fire", "Vulnerable to cold (×1.5 damage)", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("longspear", EquipSlot.MainHand)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.75f, 0.4f, 0.12f, 1f),
            PanelColor = new Color(0.28f, 0.12f, 0.02f, 0.85f),
            NameColor = new Color(0.92f, 0.6f, 0.2f),
            Description = "Average Salamander (CR 6). Fire outsider warrior with DR 10/magic and heat. MM 3.5e p.218."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Yuan-ti Pureblood — MM p.264
    //  Monstrous Humanoid (Reptilian), Medium, CR 3
    //  4d8+8 HP (26), scimitar +6 melee (1d6+1) or shortbow +6 ranged (1d6)
    //  Str 13, Dex 15, Con 14, Int 18, Wis 12, Cha 13
    //  AC 15 (+1 Dex, +4 natural), SR 14
    //  Spell-like: Detect Poison (at will), Animal Trance 1/day, Entangle 1/day, Suggestion 1/day, Cause Fear 1/day
    // ════════════════════════════════════════════════════════════
    private static void RegisterYuantiPureblood()
    {
        Register(new NPCDefinition
        {
            Id = "yuan_ti_pureblood",
            Name = "Yuan-ti Pureblood",
            ChallengeRating = "3",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Monstrous Humanoid",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 4,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 13, DEX = 15, CON = 14, WIS = 12, INT = 18, CHA = 13,
            NaturalArmorBonus = 1,
            SpellResistance = 14,
            BaseSpeed = 6,
            BaseHitDieHP = 26,
            BAB = 4,
            Immunities = new CreatureImmunities { immuneToPoison = true },
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Monstrous Humanoid", "Reptilian", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Blind-Fight" },
            SpecialAbilities = new List<string> { "SR 14", "Detect Poison (Sp): at will", "Animal Trance (Sp): 1/day, DC 13", "Entangle (Sp): 1/day, DC 13", "Suggestion (Sp): 1/day, DC 14", "Cause Fear (Sp): 1/day, DC 12", "Alternate Form: viper", "Immune to poison", "Chameleon Power: +8 Hide", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("scimitar", EquipSlot.MainHand),
                new EquipmentSlotPair("shortbow", EquipSlot.Ranged)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.4f, 0.5f, 0.3f, 1f),
            PanelColor = new Color(0.12f, 0.18f, 0.06f, 0.85f),
            NameColor = new Color(0.62f, 0.78f, 0.45f),
            Description = "Yuan-ti Pureblood (CR 3). Most human-looking yuan-ti with spell-like abilities. MM 3.5e p.264."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Yuan-ti Halfblood — MM p.264
    //  Monstrous Humanoid (Reptilian), Medium, CR 5
    //  7d8+14 HP (45), scimitar +10/+5 melee (1d6+3), bite +5 melee (1d6+1 + poison)
    //  Str 17, Dex 14, Con 14, Int 18, Wis 16, Cha 13
    //  AC 17 (+1 Dex, +6 natural), SR 16
    //  Poison: DC 15 Fort, 1d6 Con/1d6 Con
    //  More spell-likes: Darkness, Fear, Baleful Polymorph 1/day
    // ════════════════════════════════════════════════════════════
    private static void RegisterYuantiHalfblood()
    {
        Register(new NPCDefinition
        {
            Id = "yuan_ti_halfblood",
            Name = "Yuan-ti Halfblood",
            ChallengeRating = "5",
            Level = 7,
            CharacterClass = "Warrior",
            CreatureType = "Monstrous Humanoid",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 7,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 17, DEX = 14, CON = 14, WIS = 16, INT = 18, CHA = 13,
            NaturalArmorBonus = 6,
            SpellResistance = 16,
            BaseSpeed = 6,
            BaseHitDieHP = 45,
            BAB = 7,
            Immunities = new CreatureImmunities { immuneToPoison = true },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Monstrous Humanoid", "Reptilian", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Blind-Fight", "Dodge", "Expertise" },
            SpecialAbilities = new List<string> { "Poison (Ex): bite, DC 15 Fort, 1d6 Con/1d6 Con", "SR 16", "Produce Acid (Sp): at will (pureblood SLAs + more)", "Animal Trance (Sp): at will", "Entangle (Sp): at will", "Suggestion (Sp): at will, DC 14", "Darkness (Sp): 3/day", "Fear (Sp): 3/day, DC 15", "Baleful Polymorph (Sp): 1/day, DC 16", "Alternate Form: viper", "Immune to poison", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("scimitar", EquipSlot.MainHand),
                new EquipmentSlotPair("composite_longbow", EquipSlot.Ranged)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.35f, 0.48f, 0.28f, 1f),
            PanelColor = new Color(0.1f, 0.16f, 0.05f, 0.85f),
            NameColor = new Color(0.55f, 0.72f, 0.4f),
            Description = "Yuan-ti Halfblood (CR 5). Half-snake yuan-ti with poison and at-will spell-likes. MM 3.5e p.264."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Yuan-ti Abomination — MM p.265
    //  Monstrous Humanoid (Reptilian), Large, CR 7
    //  9d8+18 HP (58), scimitar +12/+7 melee (1d6+4), bite +7 melee (2d6+2 + poison)
    //  or composite longbow +9/+4 ranged (1d8+4)
    //  Str 19, Dex 14, Con 15, Int 18, Wis 18, Cha 18
    //  AC 22 (-1 size, +2 Dex, +11 natural), SR 18
    //  Poison: DC 16 Fort, 1d6 Con/1d6 Con
    //  Improved Grab, Constrict, at-will spell-likes, Aversion 1/day
    // ════════════════════════════════════════════════════════════
    private static void RegisterYuantiAbomination()
    {
        Register(new NPCDefinition
        {
            Id = "yuan_ti_abomination",
            Name = "Yuan-ti Abomination",
            ChallengeRating = "7",
            Level = 9,
            CharacterClass = "Warrior",
            CreatureType = "Monstrous Humanoid",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 9,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 19, DEX = 14, CON = 15, WIS = 18, INT = 18, CHA = 18,
            NaturalArmorBonus = 11,
            SpellResistance = 18,
            BaseSpeed = 6, // 30 ft, climb 20 ft, swim 20 ft
            BaseHitDieHP = 58,
            BAB = 9,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Bite",
            Immunities = new CreatureImmunities { immuneToPoison = true },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 2, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Monstrous Humanoid", "Reptilian", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Blind-Fight", "Dodge", "Expertise", "Improved Initiative" },
            SpecialAbilities = new List<string> { "Poison (Ex): bite, DC 16 Fort, 1d6 Con/1d6 Con", "Improved Grab → Constrict 1d6+4", "SR 18", "Produce Acid (Sp): at will", "All halfblood SLAs at will", "Aversion (Sp): 1/day, DC 18 Will or shaken", "Blasphemy (Sp): 1/day", "Alternate Form: viper/hybrid", "Immune to poison", "Chameleon Power", "Detect Poison (at will)", "Darkvision 60 ft.", "Scent" },
            HasScent = true,
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("scimitar", EquipSlot.MainHand),
                new EquipmentSlotPair("composite_longbow", EquipSlot.Ranged)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Spellcaster,
            SpriteColor = new Color(0.3f, 0.42f, 0.22f, 1f),
            PanelColor = new Color(0.06f, 0.14f, 0.03f, 0.85f),
            NameColor = new Color(0.48f, 0.65f, 0.35f),
            Description = "Yuan-ti Abomination (CR 7). Massive snake-bodied yuan-ti with powerful spells and constriction. MM 3.5e p.265."
        });
    }
}
