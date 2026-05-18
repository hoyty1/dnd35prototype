using System.Collections.Generic;
using UnityEngine;

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
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
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
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
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
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
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
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
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
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.3f, 0.3f, 0.3f, 1f),
            PanelColor = new Color(0.13f, 0.13f, 0.13f, 0.85f),
            NameColor = new Color(0.82f, 0.82f, 0.82f),
            Description = "Summon Monster baseline Monstrous Spider (Small)."
        });
    }

}
