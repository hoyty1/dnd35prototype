using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Monster Manual creatures: M
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_M()
    {
        RegisterMonkey();
        RegisterAllMephits();
    
        RegisterSummonMonstrousCentipedeMedium();
        RegisterSummonMonstrousScorpionSmall();
        RegisterSummonMonstrousSpiderSmall();
        RegisterManticore();
        RegisterMedusa();
        RegisterMimic();
        RegisterMindFlayer();
        RegisterMinotaur();
        RegisterMohrg();
        RegisterMonitorLizard();
        RegisterMummy();
        RegisterHumanMonk3();
        RegisterHumanMonk5();
        RegisterHumanMonk7();
    }

    private static void RegisterMonkey()
    {
        Register(new NPCDefinition
        {
            Id = "monkey",
            Name = "Monkey",
            ChallengeRating = "1/6",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 1,
            SizeCategory = SizeCategory.Tiny,
            IsTallCreature = false,
            STR = 3, DEX = 15, CON = 10, WIS = 12, INT = 2, CHA = 5,
            NaturalArmorBonus = 0,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 3, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 6,
            BaseHitDieHP = 4,
            CreatureTags = new List<string> { "Animal", "MM35", "Climb" },
            Feats = new List<string> { "Agile", "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Low-light vision", "Climb 30 ft", "Skills: Balance +12, Climb +10, Escape Artist +4, Hide +10, Listen +3, Spot +3" },
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.72f, 0.62f, 0.48f, 1f),
            PanelColor = new Color(0.22f, 0.16f, 0.1f, 0.85f),
            NameColor = new Color(0.95f, 0.88f, 0.76f),
            Description = "Monster Manual monkey. Tiny climber with agile bite and strong movement utility."
        });
    }


    // ═══════════════════════════════════════════════════════════
    //  Mephits — all 10 MM 3.5e variants (CR 3, Outsider, Small)
    //  Each has: 3 HD, fly 40-50 ft (average/perfect), fast healing 2,
    //  breath weapon (1/1d4 rds), and 1/day summon mephit (25%).
    //  MM 3.5e p.181-186.
    // ═══════════════════════════════════════════════════════════
    private static void RegisterAllMephits()
    {
        RegisterAirMephit();
        RegisterDustMephit();
        RegisterEarthMephit();
        RegisterFireMephit();
        RegisterIceMephit();
        RegisterMagmaMephit();
        RegisterOozeMephit();
        RegisterSaltMephit();
        RegisterSteamMephit();
        RegisterWaterMephit();
    }

    /// <summary>
    /// Shared mephit base template. All mephits are Small outsiders, 3 HD, CR 3.
    /// </summary>
    private static NPCDefinition MephitBase(string id, string name,
        int str, int dex, int con, int wis, int intel, int cha,
        int naturalArmor, int speed,
        string breathDesc, string spellLikeDesc, string fastHealingDesc,
        List<DamageType> immunities, CreatureImmunities creatureImmunities,
        List<DamageResistanceEntry> resistances,
        List<string> subTypeTags, List<string> extraAbilities,
        Color spriteColor, Color panelColor, Color nameColor,
        string description)
    {
        var tags = new List<string> { "Outsider", "Extraplanar", "MM35", "Fly", "SummonBase" };
        tags.AddRange(subTypeTags);

        var abilities = new List<string>
        {
            breathDesc,
            spellLikeDesc,
            fastHealingDesc,
            "Summon Mephit (1/day, 25% chance, summons 1 mephit of same type)",
            "Fly (average)",
            "Darkvision 60 ft.",
            "DR 5/magic",
            "Alignment: True Neutral"
        };
        abilities.AddRange(extraAbilities);

        return new NPCDefinition
        {
            Id = id,
            Name = name,
            ChallengeRating = "3",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            HitDice = 3,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = str, DEX = dex, CON = con, WIS = wis, INT = intel, CHA = cha,
            BAB = 3,
            NaturalArmorBonus = naturalArmor,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 3, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            BaseSpeed = speed,
            BaseHitDieHP = 13, // 3d8 avg
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Magic,
            DamageImmunities = immunities ?? new List<DamageType>(),
            Immunities = creatureImmunities ?? new CreatureImmunities(),
            DamageResistances = resistances ?? new List<DamageResistanceEntry>(),
            RegenerationAmount = 2, // Fast healing 2 (not true regen but close enough)
            CreatureTags = tags,
            Feats = new List<string> { "Dodge", "Improved Initiative" },
            SpecialAbilities = abilities,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = spriteColor,
            PanelColor = panelColor,
            NameColor = nameColor,
            Description = description
        };
    }

    private static void RegisterAirMephit()
    {
        Register(MephitBase(
            "air_mephit", "Air Mephit",
            str: 10, dex: 17, con: 10, wis: 11, intel: 6, cha: 15,
            naturalArmor: 4, speed: 6, // 30 ft. land, fly 60 ft. (perfect)
            breathDesc: "Breath Weapon (15-ft. cone of grit/dust, 1d8 damage, Ref DC 12 half, usable every 1d4 rds)",
            spellLikeDesc: "Spell-like: Blur 1/hr (self), Gust of Wind 1/day (DC 14)",
            fastHealingDesc: "Fast Healing 2 (in moving air/windy conditions)",
            immunities: null,
            creatureImmunities: null,
            resistances: null,
            subTypeTags: new List<string> { "Air" },
            extraAbilities: new List<string> { "Fly 60 ft. (perfect)" },
            spriteColor: new Color(0.75f, 0.88f, 1f, 1f),
            panelColor: new Color(0.16f, 0.2f, 0.28f, 0.85f),
            nameColor: new Color(0.88f, 0.95f, 1f),
            description: "Monster Manual air mephit (CR 3). 2 claws +4 (1d3), breath weapon 1d8 cone, blur, gust of wind. Fast healing 2 in wind. MM 3.5e p.181."
        ));
    }

    private static void RegisterDustMephit()
    {
        Register(MephitBase(
            "dust_mephit", "Dust Mephit",
            str: 10, dex: 17, con: 10, wis: 11, intel: 6, cha: 15,
            naturalArmor: 4, speed: 6,
            breathDesc: "Breath Weapon (10-ft. cone of irritating particles, 1d4 damage + living targets blinded 1d4 rds, Ref DC 12 negates blindness)",
            spellLikeDesc: "Spell-like: Blur 1/hr (self), Wind Wall 1/day",
            fastHealingDesc: "Fast Healing 2 (in arid/dusty environment)",
            immunities: null,
            creatureImmunities: null,
            resistances: null,
            subTypeTags: new List<string> { "Air", "Earth" },
            extraAbilities: new List<string> { "Fly 50 ft. (perfect)" },
            spriteColor: new Color(0.72f, 0.68f, 0.58f, 1f),
            panelColor: new Color(0.22f, 0.2f, 0.16f, 0.85f),
            nameColor: new Color(0.94f, 0.9f, 0.82f),
            description: "Monster Manual dust mephit (CR 3). 2 claws +4 (1d3), breath weapon 1d4 + blinding cone, blur, wind wall. Fast healing 2 in dust. MM 3.5e p.182."
        ));
    }

    private static void RegisterEarthMephit()
    {
        Register(MephitBase(
            "earth_mephit", "Earth Mephit",
            str: 17, dex: 8, con: 13, wis: 11, intel: 6, cha: 15,
            naturalArmor: 7, speed: 6,
            breathDesc: "Breath Weapon (15-ft. cone of rock shards/gravel, 1d8 damage, Ref DC 13 half, usable every 1d4 rds)",
            spellLikeDesc: "Spell-like: Soften Earth and Stone 1/day, Enlarge Person 1/hr (on self or ally)",
            fastHealingDesc: "Fast Healing 2 (while underground or on bare earth/stone)",
            immunities: null,
            creatureImmunities: null,
            resistances: null,
            subTypeTags: new List<string> { "Earth" },
            extraAbilities: new List<string> { "Fly 40 ft. (average)", "Change Size (1/hr, as Enlarge Person on self)" },
            spriteColor: new Color(0.58f, 0.48f, 0.32f, 1f),
            panelColor: new Color(0.22f, 0.18f, 0.1f, 0.85f),
            nameColor: new Color(0.92f, 0.84f, 0.68f),
            description: "Monster Manual earth mephit (CR 3). 2 claws +5 (1d3+3), breath weapon 1d8 rock cone, soften earth. Fast healing 2 on earth. MM 3.5e p.182."
        ));
    }

    private static void RegisterFireMephit()
    {
        Register(MephitBase(
            "fire_mephit", "Fire Mephit",
            str: 10, dex: 13, con: 10, wis: 11, intel: 6, cha: 15,
            naturalArmor: 4, speed: 6,
            breathDesc: "Breath Weapon (15-ft. cone of fire, 1d8 fire damage, Ref DC 12 half, usable every 1d4 rds)",
            spellLikeDesc: "Spell-like: Scorching Ray 1/hr (ranged touch +3, 1d4 fire), Heat Metal 1/day (DC 14)",
            fastHealingDesc: "Fast Healing 2 (in contact with fire or in hot environment)",
            immunities: new List<DamageType> { DamageType.Fire },
            creatureImmunities: new CreatureImmunities { immuneToFire = true },
            resistances: null,
            subTypeTags: new List<string> { "Fire" },
            extraAbilities: new List<string> { "Fly 50 ft. (average)", "Immune to fire", "Vulnerability to cold" },
            spriteColor: new Color(1f, 0.55f, 0.2f, 1f),
            panelColor: new Color(0.3f, 0.12f, 0.06f, 0.85f),
            nameColor: new Color(1f, 0.82f, 0.6f),
            description: "Monster Manual fire mephit (CR 3). 2 claws +4 (1d3 + 1d4 fire), breath weapon 1d8 fire cone. Immune fire, vulnerable cold. MM 3.5e p.182."
        ));
    }

    private static void RegisterIceMephit()
    {
        Register(MephitBase(
            "ice_mephit", "Ice Mephit",
            str: 10, dex: 17, con: 10, wis: 11, intel: 6, cha: 15,
            naturalArmor: 4, speed: 6,
            breathDesc: "Breath Weapon (10-ft. cone of ice shards, 1d4 cold damage + targets slowed (as Slow) for 3 rds, Ref DC 12 negates slow)",
            spellLikeDesc: "Spell-like: Magic Missile 1/hr (CL 3, 2 missiles), Chill Metal 1/day (DC 14)",
            fastHealingDesc: "Fast Healing 2 (in icy or cold environment)",
            immunities: new List<DamageType> { DamageType.Cold },
            creatureImmunities: new CreatureImmunities { immuneToCold = true },
            resistances: null,
            subTypeTags: new List<string> { "Air", "Cold" },
            extraAbilities: new List<string> { "Fly 50 ft. (perfect)", "Immune to cold", "Vulnerability to fire" },
            spriteColor: new Color(0.7f, 0.88f, 0.95f, 1f),
            panelColor: new Color(0.14f, 0.2f, 0.26f, 0.85f),
            nameColor: new Color(0.88f, 0.96f, 1f),
            description: "Monster Manual ice mephit (CR 3). 2 claws +4 (1d3 + 1 cold), breath weapon 1d4 cold + slow cone, magic missile, chill metal. MM 3.5e p.183."
        ));
    }

    private static void RegisterMagmaMephit()
    {
        Register(MephitBase(
            "magma_mephit", "Magma Mephit",
            str: 17, dex: 8, con: 13, wis: 11, intel: 6, cha: 15,
            naturalArmor: 7, speed: 6,
            breathDesc: "Breath Weapon (10-ft. cone of magma, 1d4 fire damage, Ref DC 13 half; molten blob that deals 1d4 fire for 1 additional round)",
            spellLikeDesc: "Spell-like: Pyrotechnics 1/day (DC 14), Heat Metal 1/hr",
            fastHealingDesc: "Fast Healing 2 (in contact with magma/lava or fire)",
            immunities: new List<DamageType> { DamageType.Fire },
            creatureImmunities: new CreatureImmunities { immuneToFire = true },
            resistances: null,
            subTypeTags: new List<string> { "Fire", "Earth" },
            extraAbilities: new List<string> { "Fly 40 ft. (average)", "Immune to fire", "Vulnerability to cold" },
            spriteColor: new Color(0.9f, 0.4f, 0.15f, 1f),
            panelColor: new Color(0.3f, 0.1f, 0.05f, 0.85f),
            nameColor: new Color(1f, 0.7f, 0.4f),
            description: "Monster Manual magma mephit (CR 3). 2 claws +5 (1d3+3 + 1d4 fire), breath 1d4 fire + lingering blob. Immune fire. MM 3.5e p.183."
        ));
    }

    private static void RegisterOozeMephit()
    {
        Register(MephitBase(
            "ooze_mephit", "Ooze Mephit",
            str: 14, dex: 10, con: 13, wis: 11, intel: 6, cha: 15,
            naturalArmor: 5, speed: 6,
            breathDesc: "Breath Weapon (10-ft. cone of caustic liquid, 1d4 acid damage, Ref DC 13 half; affects all in cone)",
            spellLikeDesc: "Spell-like: Acid Arrow 1/day (ranged touch, 2d4 acid/round for 1 round), Stinking Cloud 1/hr (DC 15)",
            fastHealingDesc: "Fast Healing 2 (in wet/muddy/swampy environment)",
            immunities: null,
            creatureImmunities: null,
            resistances: null,
            subTypeTags: new List<string> { "Water", "Earth" },
            extraAbilities: new List<string> { "Fly 40 ft. (average)", "Swim 30 ft." },
            spriteColor: new Color(0.45f, 0.55f, 0.35f, 1f),
            panelColor: new Color(0.15f, 0.2f, 0.1f, 0.85f),
            nameColor: new Color(0.82f, 0.9f, 0.72f),
            description: "Monster Manual ooze mephit (CR 3). 2 claws +3 (1d3+2), breath 1d4 acid cone, acid arrow, stinking cloud. Fast healing 2 in wet areas. MM 3.5e p.184."
        ));
    }

    private static void RegisterSaltMephit()
    {
        Register(MephitBase(
            "salt_mephit", "Salt Mephit",
            str: 17, dex: 8, con: 13, wis: 11, intel: 6, cha: 15,
            naturalArmor: 7, speed: 6,
            breathDesc: "Breath Weapon (10-ft. cone of salt crystals, 1d4 damage, Ref DC 13 half; target also dehydrated for -1 on all checks for 1 minute)",
            spellLikeDesc: "Spell-like: Glitterdust 1/hr (DC 14), Draw Moisture 1/day (2d8 desiccation damage, Fort DC 14 half)",
            fastHealingDesc: "Fast Healing 2 (in arid/dry environment)",
            immunities: null,
            creatureImmunities: null,
            resistances: null,
            subTypeTags: new List<string> { "Earth" },
            extraAbilities: new List<string> { "Fly 40 ft. (average)" },
            spriteColor: new Color(0.88f, 0.86f, 0.82f, 1f),
            panelColor: new Color(0.26f, 0.25f, 0.23f, 0.85f),
            nameColor: new Color(0.98f, 0.97f, 0.94f),
            description: "Monster Manual salt mephit (CR 3). 2 claws +5 (1d3+3), breath 1d4 + dehydration cone, glitterdust. Fast healing 2 in arid. MM 3.5e p.185."
        ));
    }

    private static void RegisterSteamMephit()
    {
        Register(MephitBase(
            "steam_mephit", "Steam Mephit",
            str: 10, dex: 17, con: 10, wis: 11, intel: 6, cha: 15,
            naturalArmor: 4, speed: 6,
            breathDesc: "Breath Weapon (10-ft. cone of scalding steam, 1d4 fire damage, Ref DC 12 half; also -4 to AC from drenching for 3 rounds)",
            spellLikeDesc: "Spell-like: Blur 1/hr (self), Rainstorm 1/day (drench fires, create difficult terrain in 20-ft. radius)",
            fastHealingDesc: "Fast Healing 2 (in boiling water or steam)",
            immunities: new List<DamageType> { DamageType.Fire },
            creatureImmunities: new CreatureImmunities { immuneToFire = true },
            resistances: null,
            subTypeTags: new List<string> { "Fire", "Water" },
            extraAbilities: new List<string> { "Fly 50 ft. (average)", "Immune to fire" },
            spriteColor: new Color(0.82f, 0.85f, 0.88f, 1f),
            panelColor: new Color(0.22f, 0.24f, 0.26f, 0.85f),
            nameColor: new Color(0.95f, 0.96f, 0.98f),
            description: "Monster Manual steam mephit (CR 3). 2 claws +4 (1d3 + 1d4 fire), breath 1d4 fire cone + AC penalty. Immune fire. MM 3.5e p.185."
        ));
    }

    private static void RegisterWaterMephit()
    {
        Register(MephitBase(
            "water_mephit", "Water Mephit",
            str: 14, dex: 10, con: 13, wis: 11, intel: 6, cha: 15,
            naturalArmor: 5, speed: 6,
            breathDesc: "Breath Weapon (15-ft. cone of caustic liquid, 1d8 acid damage, Ref DC 13 half, usable every 1d4 rds)",
            spellLikeDesc: "Spell-like: Acid Arrow 1/hr (ranged touch, 2d4 acid), Stinking Cloud 1/day (DC 15)",
            fastHealingDesc: "Fast Healing 2 (in contact with water or in rainy conditions)",
            immunities: null,
            creatureImmunities: null,
            resistances: null,
            subTypeTags: new List<string> { "Water" },
            extraAbilities: new List<string> { "Fly 40 ft. (average)", "Swim 30 ft." },
            spriteColor: new Color(0.35f, 0.55f, 0.78f, 1f),
            panelColor: new Color(0.1f, 0.18f, 0.28f, 0.85f),
            nameColor: new Color(0.75f, 0.88f, 1f),
            description: "Monster Manual water mephit (CR 3). 2 claws +3 (1d3+2), breath 1d8 acid cone, acid arrow, stinking cloud. Fast healing 2 in water. MM 3.5e p.186."
        ));
    }

    private static void RegisterSummonMonstrousCentipedeMedium()
    {
        Register(new NPCDefinition
        {
            Id = "monstrous_centipede_medium",
            Name = "Monstrous Centipede (Medium)",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = 1,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 9, DEX = 15, CON = 10, WIS = 10, INT = 1, CHA = 2,
            NaturalArmorBonus = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8,
            BaseHitDieHP = 7,
            CreatureTags = new List<string> { "Vermin", "SummonBase" },
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.56f, 0.44f, 0.3f, 1f),
            PanelColor = new Color(0.18f, 0.12f, 0.07f, 0.85f),
            NameColor = new Color(0.92f, 0.82f, 0.71f),
            Description = "Summon Monster baseline Monstrous Centipede (Medium)."
        });
    }

    private static void RegisterSummonMonstrousScorpionSmall()
    {
        Register(new NPCDefinition
        {
            Id = "monstrous_scorpion_small",
            Name = "Monstrous Scorpion (Small)",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 8, DEX = 13, CON = 10, WIS = 10, INT = 1, CHA = 2,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 3, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Sting", DamageDice = 3, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 8,
            BaseHitDieHP = 7,
            CreatureTags = new List<string> { "Vermin", "SummonBase" },
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.51f, 0.4f, 0.29f, 1f),
            PanelColor = new Color(0.17f, 0.11f, 0.07f, 0.85f),
            NameColor = new Color(0.9f, 0.8f, 0.67f),
            Description = "Summon Monster baseline Monstrous Scorpion (Small)."
        });
    }

    private static void RegisterSummonMonstrousSpiderSmall()
    {
        Register(new NPCDefinition
        {
            Id = "monstrous_spider_small",
            Name = "Monstrous Spider (Small)",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 9, DEX = 15, CON = 10, WIS = 10, INT = 1, CHA = 2,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8,
            BaseHitDieHP = 7,
            CreatureTags = new List<string> { "Vermin", "SummonBase" },
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.3f, 0.3f, 0.3f, 1f),
            PanelColor = new Color(0.13f, 0.13f, 0.13f, 0.85f),
            NameColor = new Color(0.82f, 0.82f, 0.82f),
            Description = "Summon Monster baseline Monstrous Spider (Small)."
        });
    
    private static void RegisterManticore()
    {
        Register(new NPCDefinition
        {
            Id = "manticore",
            Name = "Manticore",
            ChallengeRating = "5",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 6,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 20, DEX = 15, CON = 19, WIS = 12, INT = 7, CHA = 9,
            NaturalArmorBonus = 6,
            BaseSpeed = 6, // 30 ft, fly 50 ft (clumsy)
            BaseHitDieHP = 57,
            BAB = 6,
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 2, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 2, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Magical Beast", "Fly50", "Darkvision60", "MM35" },
            Feats = new List<string> { "Flyby Attack", "Multiattack", "Track", "Weapon Focus (spikes)" },
            SpecialAbilities = new List<string> { "Spikes (Ex): 6 spikes, 180 ft. ranged, 1d8+2 each (24 spikes, regrow 1d4/day)", "Flight: 50 ft. (clumsy)", "Scent", "Darkvision 60 ft.", "Low-light vision" },
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Ranged,
            SpriteColor = new Color(0.55f, 0.4f, 0.3f, 1f),
            PanelColor = new Color(0.2f, 0.12f, 0.08f, 0.85f),
            NameColor = new Color(0.82f, 0.62f, 0.48f),
            Description = "Manticore (CR 5). Winged lion-beast that fires tail spikes at range. MM 3.5e p.179."
        });
    }

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
                Effect = AuraEffectType.Petrified,
                DurationRounds = 999
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Snakes", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.None, Range = 1, IsPrimary = false, PoisonOnHitId = "medusa_snake_poison" }
            },
            CreatureTags = new List<string> { "Monstrous Humanoid", "Darkvision60", "MM35" },
            Feats = new List<string> { "Point Blank Shot", "Precise Shot", "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Petrifying Gaze (Su): 30 ft., Fort DC 15 or permanently turned to stone", "Poison (Ex): snakes, DC 14 Fort, 1d6 Str/2d6 Str", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("dagger", EquipSlot.MainHand),
                new EquipmentSlotPair("shortbow", EquipSlot.Ranged)
            },
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Ranged,
            SpriteColor = new Color(0.4f, 0.5f, 0.35f, 1f),
            PanelColor = new Color(0.12f, 0.18f, 0.1f, 0.85f),
            NameColor = new Color(0.62f, 0.78f, 0.55f),
            Description = "Medusa (CR 7). Snake-haired woman with petrifying gaze and poison. MM 3.5e p.180."
        });
    }

    private static void RegisterMimic()
    {
        Register(new NPCDefinition
        {
            Id = "mimic",
            Name = "Mimic",
            ChallengeRating = "4",
            Level = 7,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 7,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 19, DEX = 12, CON = 17, WIS = 13, INT = 10, CHA = 10,
            NaturalArmorBonus = 5,
            BaseSpeed = 2, // 10 ft
            BaseHitDieHP = 52,
            BAB = 5,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Slam",
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 8, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            Immunities = new CreatureImmunities { immuneToAcid = true },
            CreatureTags = new List<string> { "Aberration", "Shapechanger", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Lightning Reflexes", "Weapon Focus (slam)" },
            SpecialAbilities = new List<string> { "Adhesive (Ex): auto-grapple on touch; DC 16 Str or dissolvent to release", "Crush (Ex): 1d8+4 per round to grappled creatures", "Mimic Shape (Ex): can assume form of any Medium–Large object", "Immune to acid", "Darkvision 60 ft." },
            AIProfileArchetype = NPCAIProfileArchetype.Grappler,
            SpriteColor = new Color(0.5f, 0.45f, 0.35f, 1f),
            PanelColor = new Color(0.18f, 0.14f, 0.08f, 0.85f),
            NameColor = new Color(0.78f, 0.7f, 0.55f),
            Description = "Mimic (CR 4). Shapeshifting aberration that disguises as objects and adhesive-grabs prey. MM 3.5e p.186."
        });
    }

    private static void RegisterMindFlayer()
    {
        Register(new NPCDefinition
        {
            Id = "mind_flayer",
            Name = "Mind Flayer",
            ChallengeRating = "8",
            Level = 8,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 8,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 12, DEX = 14, CON = 12, WIS = 17, INT = 19, CHA = 17,
            NaturalArmorBonus = 3,
            SpellResistance = 25,
            BaseSpeed = 6,
            BaseHitDieHP = 44,
            BAB = 6,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Tentacle",
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Tentacle", DamageDice = 4, DamageCount = 1, Count = 4, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Aberration", "Telepathy100", "Darkvision60", "MM35" },
            Feats = new List<string> { "Combat Casting", "Improved Initiative", "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Mind Blast (Sp): 60 ft. cone, Will DC 17 or stunned 3d4 rounds", "Extract Brain (Ex): coup de grace on grappled target, instant death", "Improved Grab: tentacle → grapple → extract brain", "SR 25", "Telepathy 100 ft.", "Psionics: Suggestion 3/day, Charm Monster 3/day, Detect Thoughts (at will), Levitate (at will), Plane Shift (at will)" },
            AIProfileArchetype = NPCAIProfileArchetype.Spellcaster,
            SpriteColor = new Color(0.5f, 0.35f, 0.55f, 1f),
            PanelColor = new Color(0.18f, 0.1f, 0.22f, 0.85f),
            NameColor = new Color(0.75f, 0.55f, 0.85f),
            Description = "Mind Flayer (CR 8). Psionic aberration with mind blast and brain extraction. MM 3.5e p.186."
        });
    }

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
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.5f, 0.38f, 0.28f, 1f),
            PanelColor = new Color(0.18f, 0.12f, 0.06f, 0.85f),
            NameColor = new Color(0.78f, 0.6f, 0.45f),
            Description = "Minotaur (CR 4). Bull-headed brute with powerful charge. MM 3.5e p.188."
        });
    }

    private static void RegisterMohrg()
    {
        Register(new NPCDefinition
        {
            Id = "mohrg",
            Name = "Mohrg",
            ChallengeRating = "8",
            Level = 14,
            CharacterClass = "Warrior",
            CreatureType = "Undead",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 14,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 21, DEX = 19, CON = 0, WIS = 10, INT = 11, CHA = 10,
            NaturalArmorBonus = 9,
            BaseSpeed = 6,
            BaseHitDieHP = 91,
            BAB = 7,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Tongue", DamageDice = 0, DamageCount = 0, Count = 1, BonusDamageSource = DamageBonusSource.None, Range = 1, IsPrimary = false, ParalysisOnHitDC = 17, ParalysisOnHitDurationRounds = 6 }
            },
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Slam",
            CreatureTags = new List<string> { "Undead", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Dodge", "Improved Initiative", "Lightning Reflexes", "Mobility" },
            SpecialAbilities = new List<string> { "Paralyzing Touch (Su): tongue attack, DC 17 Fort or paralyzed 1d4 rounds", "Create Spawn (Su): humanoids killed rise as zombies in 1d4 rounds", "Improved Grab", "Darkvision 60 ft.", "Undead traits" },
            AIProfileArchetype = NPCAIProfileArchetype.Grappler,
            SpriteColor = new Color(0.5f, 0.35f, 0.3f, 1f),
            PanelColor = new Color(0.2f, 0.1f, 0.08f, 0.85f),
            NameColor = new Color(0.8f, 0.55f, 0.45f),
            Description = "Mohrg (CR 8). Skeletal undead with paralyzing tongue that creates zombie spawn. MM 3.5e p.189."
        });
    }

    private static void RegisterMonitorLizard()
    {
        Register(new NPCDefinition
        {
            Id = "monitor_lizard",
            Name = "Monitor Lizard",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 17, DEX = 15, CON = 17, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 3,
            BaseSpeed = 6, // 30 ft, swim 30 ft
            BaseHitDieHP = 22,
            BAB = 2,
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Animal", "MM35" },
            Feats = new List<string> { "Alertness", "Great Fortitude" },
            SpecialAbilities = new List<string> { "Scent", "Low-light vision", "Swim 30 ft." },
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.45f, 0.5f, 0.35f, 1f),
            PanelColor = new Color(0.15f, 0.18f, 0.08f, 0.85f),
            NameColor = new Color(0.68f, 0.75f, 0.55f),
            Description = "Monitor Lizard (CR 2). Large reptilian predator. MM 3.5e p.275."
        });
    }

    private static void RegisterMummy()
    {
        Register(new NPCDefinition
        {
            Id = "mummy",
            Name = "Mummy",
            ChallengeRating = "5",
            Level = 8,
            CharacterClass = "Warrior",
            CreatureType = "Undead",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 8,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 24, DEX = 10, CON = 0, WIS = 14, INT = 6, CHA = 15,
            NaturalArmorBonus = 10,
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.None, // DR 5/— (overcome by nothing)
            BaseSpeed = 4, // 20 ft
            BaseHitDieHP = 55,
            BAB = 4,
            AuraAbility = new AuraAbilityDefinition
            {
                Name = "Despair",
                SaveDC = 16,
                IsWillSave = true,
                RangeFeet = 30,
                Effect = AuraEffectType.Frightened,
                DurationRounds = 4
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true, HasDiseaseOnHit = true, DiseaseOnHitType = DiseaseType.MummyRot }
            },
            CreatureTags = new List<string> { "Undead", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Great Fortitude", "Toughness" },
            SpecialAbilities = new List<string> { "Despair (Su): 30 ft., Will DC 16 or paralyzed with fear 1d4 rounds", "Mummy Rot (Su): supernatural disease, 1d6 CON", "DR 5/—", "Vulnerable to fire (×1.5 damage)", "Darkvision 60 ft.", "Undead traits" },
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.6f, 0.55f, 0.4f, 1f),
            PanelColor = new Color(0.22f, 0.2f, 0.12f, 0.85f),
            NameColor = new Color(0.85f, 0.78f, 0.55f),
            Description = "Mummy (CR 5). Bandaged undead with despair aura and mummy rot. MM 3.5e p.190."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Leveled Human Monk NPCs (PHB 3.5e)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Human Monk 3 (CR 3) — Medium humanoid (human).
    /// PHB 3.5e Monk class level 3. Unarmored combatant with Flurry of Blows and Evasion.
    /// Unarmed strike 1d6. Still Mind. AC bonus from WIS.
    /// </summary>
    private static void RegisterHumanMonk3()
    {
        Register(new NPCDefinition
        {
            Id = "human_monk_3",
            Name = "Human Monk",
            ChallengeRating = "3",
            Level = 3,
            CharacterClass = "Monk",
            CreatureType = "Humanoid",
            CharacterAlignment = Alignment.LawfulNeutral,
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            // PHB Monk: high DEX/WIS, decent STR. 25 point buy equivalent.
            STR = 14, DEX = 16, CON = 12, WIS = 16, INT = 10, CHA = 8,
            NaturalArmorBonus = 0, // Unarmored — AC from WIS + class bonus
            BaseSpeed = 8, // 40 ft (Monk speed bonus at 3rd level)
            BaseHitDieHP = 20, // 3d8+3 average
            BAB = 2, // Monk 3: +2
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Unarmed Strike", DamageDice = 6, DamageCount = 1, Count = 1,
                    BonusDamageSource = DamageBonusSource.Strength,
                    Range = 1, IsPrimary = true
                }
            },
            CreatureTags = new List<string> { "Humanoid", "Human", "Monk", "Unarmored", "MM35" },
            Feats = new List<string> { "Improved Unarmed Strike", "Stunning Fist", "Dodge", "Mobility", "Combat Reflexes", "Deflect Arrows" },
            SpecialAbilities = new List<string>
            {
                "Flurry of Blows: extra attack at -2 penalty",
                "Evasion: no damage on successful Ref save",
                "Still Mind: +2 vs. enchantment",
                "AC Bonus (Ex): +3 WIS mod to AC when unarmored",
                "Unarmed Strike 1d6",
                "Speed 40 ft."
            },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                // Monks are unarmored — only carried items
                new EquipmentSlotPair(ItemIDs.SLING, EquipSlot.Ranged)
            },
            BackpackItemIds = new List<string> { ItemIDs.POTION_CURE_LIGHT_WOUNDS, ItemIDs.AMMO_SLING_BULLET },
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.65f, 0.55f, 0.35f, 1f),
            PanelColor = new Color(0.2f, 0.15f, 0.08f, 0.85f),
            NameColor = new Color(0.9f, 0.8f, 0.5f),
            Description = "Human Monk 3 (CR 3). Disciplined martial artist with Flurry of Blows and Evasion. Unarmed 1d6. PHB 3.5e."
        });
    }

    /// <summary>
    /// Human Monk 5 (CR 5) — Medium humanoid (human).
    /// PHB 3.5e Monk class level 5. Unarmed strike 1d8. Ki Strike (magic), Purity of Body.
    /// Slow Fall 20 ft.
    /// </summary>
    private static void RegisterHumanMonk5()
    {
        Register(new NPCDefinition
        {
            Id = "human_monk_5",
            Name = "Human Monk",
            ChallengeRating = "5",
            Level = 5,
            CharacterClass = "Monk",
            CreatureType = "Humanoid",
            CharacterAlignment = Alignment.LawfulNeutral,
            HitDice = 5,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 14, DEX = 16, CON = 12, WIS = 16, INT = 10, CHA = 8,
            NaturalArmorBonus = 1, // +1 AC class bonus at 5th
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 32, // 5d8+5 average
            BAB = 3, // Monk 5: +3
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Unarmed Strike", DamageDice = 8, DamageCount = 1, Count = 1,
                    BonusDamageSource = DamageBonusSource.Strength,
                    Range = 1, IsPrimary = true
                }
            },
            CreatureTags = new List<string> { "Humanoid", "Human", "Monk", "Unarmored", "KiStrikeMagic", "MM35" },
            Feats = new List<string> { "Improved Unarmed Strike", "Stunning Fist", "Dodge", "Mobility", "Combat Reflexes", "Deflect Arrows", "Improved Trip" },
            SpecialAbilities = new List<string>
            {
                "Flurry of Blows: extra attack at -1 penalty",
                "Evasion: no damage on successful Ref save",
                "Still Mind: +2 vs. enchantment",
                "Ki Strike (magic): unarmed strikes count as magic weapons",
                "Purity of Body: immune to all diseases",
                "Slow Fall 20 ft.",
                "AC Bonus (Ex): +3 WIS mod + 1 class to AC when unarmored",
                "Unarmed Strike 1d8",
                "Speed 40 ft."
            },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.SLING, EquipSlot.Ranged)
            },
            BackpackItemIds = new List<string> { ItemIDs.POTION_CURE_LIGHT_WOUNDS, ItemIDs.AMMO_SLING_BULLET },
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.65f, 0.55f, 0.35f, 1f),
            PanelColor = new Color(0.2f, 0.15f, 0.08f, 0.85f),
            NameColor = new Color(0.9f, 0.8f, 0.5f),
            Description = "Human Monk 5 (CR 5). Ki-empowered martial artist. Unarmed strikes count as magic. 1d8 damage. PHB 3.5e."
        });
    }

    /// <summary>
    /// Human Monk 7 (CR 7) — Medium humanoid (human).
    /// PHB 3.5e Monk class level 7. Unarmed strike 1d8. Wholeness of Body, Ki Strike (magic).
    /// Improved Evasion (7th level Monk ability).
    /// </summary>
    private static void RegisterHumanMonk7()
    {
        Register(new NPCDefinition
        {
            Id = "human_monk_7",
            Name = "Human Monk",
            ChallengeRating = "7",
            Level = 7,
            CharacterClass = "Monk",
            CreatureType = "Humanoid",
            CharacterAlignment = Alignment.LawfulNeutral,
            HitDice = 7,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            // Level 7: +1 DEX from level 4 ability increase
            STR = 14, DEX = 17, CON = 12, WIS = 16, INT = 10, CHA = 8,
            NaturalArmorBonus = 1, // +1 AC class bonus
            BaseSpeed = 10, // 50 ft (Monk speed at 6th+)
            BaseHitDieHP = 44, // 7d8+7 average
            BAB = 5, // Monk 7: +5
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Unarmed Strike", DamageDice = 8, DamageCount = 1, Count = 2,
                    BonusDamageSource = DamageBonusSource.Strength,
                    Range = 1, IsPrimary = true
                }
            },
            CreatureTags = new List<string> { "Humanoid", "Human", "Monk", "Unarmored", "KiStrikeMagic", "MM35" },
            Feats = new List<string> { "Improved Unarmed Strike", "Stunning Fist", "Dodge", "Mobility", "Combat Reflexes", "Deflect Arrows", "Improved Trip", "Spring Attack" },
            SpecialAbilities = new List<string>
            {
                "Flurry of Blows: two extra attacks at full BAB -2",
                "Improved Evasion: half damage even on failed Ref save",
                "Still Mind: +2 vs. enchantment",
                "Ki Strike (magic): unarmed strikes count as magic weapons",
                "Purity of Body: immune to all diseases",
                "Wholeness of Body: heal 14 HP/day as standard action",
                "Slow Fall 30 ft.",
                "AC Bonus (Ex): +3 WIS mod + 1 class to AC when unarmored",
                "Unarmed Strike 1d8 (×2 attacks)",
                "Speed 50 ft."
            },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.SLING, EquipSlot.Ranged)
            },
            BackpackItemIds = new List<string> { ItemIDs.POTION_CURE_LIGHT_WOUNDS, ItemIDs.POTION_CURE_LIGHT_WOUNDS, ItemIDs.AMMO_SLING_BULLET },
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.65f, 0.55f, 0.35f, 1f),
            PanelColor = new Color(0.2f, 0.15f, 0.08f, 0.85f),
            NameColor = new Color(0.9f, 0.8f, 0.5f),
            Description = "Human Monk 7 (CR 7). Master martial artist with Improved Evasion and Wholeness of Body. 2× unarmed 1d8. Speed 50 ft. PHB 3.5e."
        });
    }
}

}
