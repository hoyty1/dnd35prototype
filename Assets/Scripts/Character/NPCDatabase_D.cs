using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: D
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_D()
    {
        RegisterDog();
        RegisterRidingDog();
        RegisterDireRat();
    
        RegisterSummonDireBat();
        RegisterSummonDireBadger();
        RegisterSummonDireWeasel();
        RegisterSummonDretch();
        RegisterDarkNaga();
        RegisterDerro();
        RegisterDestrachan();
        RegisterDigester();
        RegisterDisplacerBeast();
        RegisterDjinni();
        RegisterDoppelganger();
        RegisterDretch();
        RegisterDrider();
        RegisterDrowElf();
        RegisterDuergar();
        RegisterDwarfWarrior();
        RegisterDireBoar();
        RegisterDireWolf();
        RegisterDarkmantle();
        RegisterDireApe();
        RegisterDireWolverine();
        RegisterDeinonychus();

    }

    private static void RegisterDog()
    {
        Register(new NPCDefinition
        {
            Id = "dog",
            Name = "Dog",
            ChallengeRating = "1/3",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 13, DEX = 17, CON = 15, WIS = 12, INT = 2, CHA = 6,
            NaturalArmorBonus = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8,
            BaseHitDieHP = 6,
            CreatureTags = new List<string> { "Animal", "MM35" },
            Feats = new List<string> { "Alertness", "Track" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Low-light vision", "Scent", "Skills: Jump +7, Listen +5, Spot +5, Survival +1" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.84f, 0.72f, 0.56f, 1f),
            PanelColor = new Color(0.22f, 0.15f, 0.1f, 0.85f),
            NameColor = new Color(0.98f, 0.9f, 0.8f),
            Description = "Monster Manual dog. Fast Small animal with scent and tracking-focused skill spread."
        });
    }

    private static void RegisterRidingDog()
    {
        Register(new NPCDefinition
        {
            Id = "riding_dog",
            Name = "Riding Dog",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 15, DEX = 15, CON = 15, WIS = 12, INT = 2, CHA = 6,
            BAB = 1,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8,
            BaseHitDieHP = 13,
            CreatureTags = new List<string> { "Animal", "MM35" },
            Feats = new List<string> { "Alertness", "Track" },
            HasScent = true,
            SpecialAbilities = new List<string>
            {
                "Low-light vision",
                "Scent",
                "Skills: Jump +8, Listen +5, Spot +5, Survival +1"
            },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.84f, 0.68f, 0.48f, 1f),
            PanelColor = new Color(0.24f, 0.15f, 0.09f, 0.85f),
            NameColor = new Color(0.99f, 0.88f, 0.72f),
            Description = "Monster Manual riding dog. Medium war-trained canine with high speed and a powerful bite."
        });
    }

    private static void RegisterDireRat()
    {
        Register(new NPCDefinition
        {
            Id = "dire_rat",
            Name = "Dire Rat",
            ChallengeRating = "1/3",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 10, DEX = 17, CON = 12, WIS = 12, INT = 1, CHA = 4,
            NaturalArmorBonus = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1,
                    BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true,
                    HasDiseaseOnHit = true, DiseaseOnHitType = DiseaseType.FilthFever
                }
            },
            BaseSpeed = 8,
            BaseHitDieHP = 5,
            CreatureTags = new List<string> { "Animal", "MM35" },
            Feats = new List<string> { "Alertness", "Weapon Finesse" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Low-light vision", "Disease (Filth Fever)", "Scent", "Climb 20 ft", "Skills: Climb +11, Hide +8, Listen +4, Move Silently +4, Spot +4, Swim +11" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.55f, 0.55f, 0.57f, 1f),
            PanelColor = new Color(0.18f, 0.18f, 0.2f, 0.85f),
            NameColor = new Color(0.88f, 0.88f, 0.92f),
            Description = "Monster Manual dire rat. Fast Small disease carrier with scent and exceptional climb/swim mobility."
        });
    }



    private static void RegisterSummonDireBat()
    {
        Register(new NPCDefinition
        {
            Id = "dire_bat",
            Name = "Dire Bat",
            ChallengeRating = "2",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 4,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 17, DEX = 22, CON = 17, WIS = 14, INT = 2, CHA = 6,
            NaturalArmorBonus = 5,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 4, // 20 ft. land, fly 40 ft. (good)
            BaseHitDieHP = 30,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            SpecialAbilities = new List<string> { "Blindsense 40 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.4f, 0.32f, 0.38f, 1f),
            PanelColor = new Color(0.15f, 0.1f, 0.16f, 0.85f),
            NameColor = new Color(0.85f, 0.79f, 0.9f),
            Description = "Dire bat with blindsense 40 ft. echolocation. Wingspan 15 ft., ~200 lbs. MM 3.5e p.62."
        });
    }

    private static void RegisterSummonDireBadger()
    {
        Register(new NPCDefinition
        {
            Id = "dire_badger",
            Name = "Dire Badger",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 14, DEX = 17, CON = 19, WIS = 12, INT = 2, CHA = 10,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 6, // 30 ft., burrow 10 ft.
            BaseHitDieHP = 28,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Rage (+4 Str, +4 Con, −2 AC when damaged)", "Low-light vision", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.5f, 0.45f, 0.43f, 1f),
            PanelColor = new Color(0.2f, 0.17f, 0.16f, 0.85f),
            NameColor = new Color(0.93f, 0.9f, 0.9f),
            Description = "Dire badger with rage when wounded (+4 Str/Con, −2 AC). MM 3.5e p.62."
        });
    }

    private static void RegisterSummonDireWeasel()
    {
        Register(new NPCDefinition
        {
            Id = "dire_weasel",
            Name = "Dire Weasel",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 14, DEX = 19, CON = 10, WIS = 12, INT = 2, CHA = 11,
            NaturalArmorBonus = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8, // 40 ft.
            BaseHitDieHP = 13,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Attach (auto bite damage while latched)", "Blood drain (1d4 Con/round)", "Low-light vision", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.62f, 0.52f, 0.42f, 1f),
            PanelColor = new Color(0.22f, 0.18f, 0.14f, 0.85f),
            NameColor = new Color(0.92f, 0.86f, 0.78f),
            Description = "Dire weasel. Bite +6 (1d6+3), attach, blood drain 1d4 Con/round. Weapon Finesse. MM 3.5e p.65."
        });
    }

    private static void RegisterSummonDretch()
    {
        Register(new NPCDefinition
        {
            Id = "dretch",
            Name = "Dretch",
            ChallengeRating = "2",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            HitDice = 2,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 12, DEX = 10, CON = 14, WIS = 11, INT = 5, CHA = 11,
            NaturalArmorBonus = 5,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 4, // 20 ft.
            BaseHitDieHP = 13,
            // DR 5/cold iron or good
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.ColdIron | DamageBypassTag.Good,
            SpellResistance = 5,
            Immunities = new CreatureImmunities
            {
                immuneToElectricity = true,
                immuneToPoison = true
            },
            DamageImmunities = new List<DamageType> { DamageType.Electricity },
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Acid, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 10 }
            },
            CreatureTags = new List<string> { "Outsider", "Chaotic", "Demon", "Extraplanar", "Evil", "SummonBase" },
            SpecialAbilities = new List<string> { "DR 5/cold iron or good", "SR 5", "Immune: electricity, poison", "Resist: acid 10, cold 10, fire 10", "Spell-like: 1/day scare (DC 12), stinking cloud (DC 13)", "Summon demon (35% chance, 1 dretch)", "Telepathy 100 ft.", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.45f, 0.35f, 0.5f, 1f),
            PanelColor = new Color(0.18f, 0.12f, 0.22f, 0.85f),
            NameColor = new Color(0.85f, 0.75f, 0.92f),
            Description = "Dretch demon. 2 claws +4 (1d6+1), bite +2 (1d4). DR 5/cold iron or good. SR 5. Immune electricity/poison. Resist acid/cold/fire 10. MM 3.5e p.42."
        });
    
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

    private static void RegisterDerro()
    {
        Register(new NPCDefinition
        {
            Id = "derro",
            Name = "Derro",
            ChallengeRating = "3",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Monstrous Humanoid",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 3,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = true,
            STR = 13, DEX = 16, CON = 13, WIS = 5, INT = 10, CHA = 16,
            NaturalArmorBonus = 3,
            SpellResistance = 15,
            BaseSpeed = 4,
            BaseHitDieHP = 16,
            BAB = 3,
            Immunities = new CreatureImmunities { immuneToPoison = true },
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Monstrous Humanoid", "MM35" },
            Feats = new List<string> { "Blind-Fight", "Exotic Weapon Proficiency (repeating crossbow)" },
            SpecialAbilities = new List<string> { "Madness: immune to confusion/insanity, uses CHA for Will saves", "SR 15", "Vulnerability to sunlight (dazzled)", "Darkness (Sp) at will", "Ghost Sound (Sp) at will", "Daze (Sp) 1/day, DC 13", "Sound Burst (Sp) 1/day, DC 15", "Poison Use" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("short_sword", EquipSlot.MainHand),
                new EquipmentSlotPair("studded_leather", EquipSlot.Armor)
            },
            BackpackItemIds = new List<string> { "repeating_light_crossbow" },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.5f, 0.48f, 0.55f, 1f),
            PanelColor = new Color(0.18f, 0.16f, 0.22f, 0.85f),
            NameColor = new Color(0.75f, 0.7f, 0.85f),
            Description = "Derro (CR 3). Insane small humanoid with spell resistance 15. MM 3.5e p.49."
        });
    }

    private static void RegisterDestrachan()
    {
        Register(new NPCDefinition
        {
            Id = "destrachan",
            Name = "Destrachan",
            ChallengeRating = "8",
            Level = 8,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            CharacterAlignment = Alignment.NeutralEvil,
            HitDice = 8,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 18, DEX = 14, CON = 16, WIS = 18, INT = 12, CHA = 12,
            NaturalArmorBonus = 7,
            BaseSpeed = 6,
            BaseHitDieHP = 60,
            BAB = 6,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true }
            },
            Immunities = new CreatureImmunities { immuneToSonic = true },
            CreatureTags = new List<string> { "Aberration", "Blindsight100", "MM35" },
            Feats = new List<string> { "Dodge", "Improved Initiative", "Lightning Reflexes" },
            SpecialAbilities = new List<string> { "Destructive Harmonics (Su): 80 ft. cone, choose: 4d6 sonic (Ref DC 17), shatter objects, or lethal pitch", "Blindsight 100 ft. (deaf = blinded)", "Immune to sonic/gaze attacks", "Protection from sonics grants immunity to harmonics" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.5f, 0.42f, 0.5f, 1f),
            PanelColor = new Color(0.18f, 0.14f, 0.2f, 0.85f),
            NameColor = new Color(0.75f, 0.65f, 0.8f),
            Description = "Destrachan (CR 8). Blind aberration with devastating sonic harmonics. MM 3.5e p.49."
        });
    }

    private static void RegisterDigester()
    {
        Register(new NPCDefinition
        {
            Id = "digester",
            Name = "Digester",
            ChallengeRating = "6",
            Level = 8,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 8,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 17, DEX = 15, CON = 17, WIS = 12, INT = 2, CHA = 10,
            NaturalArmorBonus = 5,
            BaseSpeed = 12, // 60 ft
            BaseHitDieHP = 68,
            BAB = 8,
            HasScent = true,
            BreathWeapon = new BreathWeaponDefinition
            {
                Shape = BreathWeaponShape.Cone,
                RangeFeet = 20,
                DamageDice = 8,
                DamageCount = 4,
                DamageType = DamageType.Acid,
                SaveDC = 17,
                IsReflexSave = true,
                RechargeRounds = 2
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Magical Beast", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Improved Initiative", "Track" },
            SpecialAbilities = new List<string> { "Acid Spray (Ex): 20 ft. cone, 4d8 acid, Ref DC 17 half", "Scent", "Darkvision 60 ft., Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.55f, 0.5f, 0.35f, 1f),
            PanelColor = new Color(0.2f, 0.18f, 0.1f, 0.85f),
            NameColor = new Color(0.8f, 0.75f, 0.55f),
            Description = "Digester (CR 6). Acid-spraying predator that dissolves prey. MM 3.5e p.59."
        });
    }

    private static void RegisterDisplacerBeast()
    {
        Register(new NPCDefinition
        {
            Id = "displacer_beast",
            Name = "Displacer Beast",
            ChallengeRating = "4",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 6,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 18, DEX = 12, CON = 16, WIS = 12, INT = 5, CHA = 8,
            NaturalArmorBonus = 6,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 51,
            BAB = 6,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Tentacle", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 2, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Magical Beast", "Displacement", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Dodge", "Stealthy" },
            SpecialAbilities = new List<string> { "Displacement (Su): as displacement spell, 50% miss chance", "Resist Ranged: +2 saves vs. ranged magic attacks", "Darkvision 60 ft.", "Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.25f, 0.3f, 0.45f, 1f),
            PanelColor = new Color(0.06f, 0.1f, 0.18f, 0.85f),
            NameColor = new Color(0.4f, 0.5f, 0.72f),
            Description = "Displacer Beast (CR 4). Six-legged panther-like beast with displacement and tentacles. MM 3.5e p.66."
        });
    }

    private static void RegisterDjinni()
    {
        Register(new NPCDefinition
        {
            Id = "djinni",
            Name = "Djinni",
            ChallengeRating = "5",
            Level = 7,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.ChaoticGood,
            HitDice = 7,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 18, DEX = 17, CON = 14, WIS = 15, INT = 14, CHA = 15,
            NaturalArmorBonus = 4,
            BaseSpeed = 4, // 20 ft, fly 60 ft (perfect)
            BaseHitDieHP = 45,
            BAB = 7,
            Immunities = new CreatureImmunities { immuneToAcid = true },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 8, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Outsider", "Air", "Extraplanar", "Fly60", "Telepathy100", "Darkvision60", "MM35" },
            Feats = new List<string> { "Combat Casting", "Combat Reflexes", "Dodge", "Improved Initiative" },
            SpecialAbilities = new List<string> { "Air Mastery (Ex): +1 attack/damage vs airborne, -4 vs grounded", "Whirlwind (Su): 10-70 ft., 2d6+4, Ref DC 18", "Invisibility (Sp): at will, self", "Create Food/Water (Sp): 1/day", "Major Creation (Sp): 1/day", "Persistent Image (Sp): 1/day, DC 17", "Wind Walk (Sp): 1/day", "Gaseous Form (Sp): 1/day", "Plane Shift (Sp): at will", "Immune to acid", "Telepathy 100 ft.", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.55f, 0.7f, 0.85f, 1f),
            PanelColor = new Color(0.15f, 0.22f, 0.32f, 0.85f),
            NameColor = new Color(0.75f, 0.88f, 0.98f),
            Description = "Djinni (CR 5). Air genie with whirlwind and spell-like abilities. MM 3.5e p.114."
        });
    }

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

    private static void RegisterDretch()
    {
        Register(new NPCDefinition
        {
            Id = "dretch",
            Name = "Dretch",
            ChallengeRating = "2",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 2,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = true,
            STR = 14, DEX = 10, CON = 14, WIS = 11, INT = 5, CHA = 11,
            NaturalArmorBonus = 3,
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.ColdIron | DamageBypassTag.Good,
            BaseSpeed = 4, // 20 ft
            BaseHitDieHP = 13,
            BAB = 2,
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
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Outsider", "Chaotic", "Evil", "Extraplanar", "Tanarri", "Telepathy100", "Darkvision60", "MM35" },
            Feats = new List<string> { "Multiattack" },
            SpecialAbilities = new List<string> { "Stinking Cloud (Sp): 1/day, DC 13 Fort", "Summon Demon: 35% chance to summon 1 dretch", "DR 5/cold iron or good", "Immune to electricity/poison", "Resist acid 10, cold 10, fire 10", "Telepathy 100 ft.", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.5f, 0.35f, 0.3f, 1f),
            PanelColor = new Color(0.2f, 0.1f, 0.08f, 0.85f),
            NameColor = new Color(0.78f, 0.55f, 0.48f),
            Description = "Dretch (CR 2). Lowly demon with stinking cloud. MM 3.5e p.42."
        });
    }

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

    private static void RegisterDrowElf()
    {
        Register(new NPCDefinition
        {
            Id = "drow",
            Name = "Drow",
            ChallengeRating = "1",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            CharacterAlignment = Alignment.NeutralEvil,
            HitDice = 1,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 13, DEX = 15, CON = 10, WIS = 9, INT = 14, CHA = 12,
            NaturalArmorBonus = 0,
            SpellResistance = 12,
            BaseSpeed = 6,
            BaseHitDieHP = 4,
            BAB = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Humanoid", "Elf", "Drow", "MM35" },
            Feats = new List<string> { "Weapon Focus (rapier)" },
            SpecialAbilities = new List<string> { "Darkvision 120 ft.", "SR 12", "Light blindness", "Immune to sleep", "+2 saves vs. enchantment", "Dancing Lights (Sp) 1/day", "Darkness (Sp) 1/day", "Faerie Fire (Sp) 1/day", "Poison Use (no risk of self-poisoning)" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("rapier", EquipSlot.MainHand),
                new EquipmentSlotPair("hand_crossbow", EquipSlot.Ranged),
                new EquipmentSlotPair("chain_shirt", EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.3f, 0.25f, 0.4f, 1f),
            PanelColor = new Color(0.12f, 0.08f, 0.2f, 0.85f),
            NameColor = new Color(0.6f, 0.5f, 0.8f),
            Description = "Drow (CR 1). Dark elf with spell resistance, spell-like abilities, and poison use. MM 3.5e p.103."
        });
    }

    private static void RegisterDuergar()
    {
        Register(new NPCDefinition
        {
            Id = "duergar",
            Name = "Duergar",
            ChallengeRating = "1",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 1,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 13, DEX = 11, CON = 14, WIS = 9, INT = 10, CHA = 4,
            NaturalArmorBonus = 0,
            BaseSpeed = 4,
            BaseHitDieHP = 6,
            BAB = 1,
            Immunities = new CreatureImmunities { immuneToPoison = true },
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Humanoid", "Dwarf", "Duergar", "MM35" },
            Feats = new List<string> { "Toughness" },
            SpecialAbilities = new List<string> { "Darkvision 120 ft.", "Immune to paralysis/phantasms/poison", "Light sensitivity", "+2 saves vs. spells", "Stability", "Enlarge Person (Sp) 1/day (self)", "Invisibility (Sp) 1/day (self)", "+1 attack vs. orcs/goblinoids", "+4 dodge AC vs. giants" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("warhammer", EquipSlot.MainHand),
                new EquipmentSlotPair("chain_shirt", EquipSlot.Armor)
            },
            BackpackItemIds = new List<string> { "light_crossbow" },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.45f, 0.42f, 0.45f, 1f),
            PanelColor = new Color(0.18f, 0.16f, 0.2f, 0.85f),
            NameColor = new Color(0.7f, 0.65f, 0.75f),
            Description = "Duergar (CR 1). Gray dwarf with enlarge person and invisibility. MM 3.5e p.93."
        });
    }

    private static void RegisterDwarfWarrior()
    {
        Register(new NPCDefinition
        {
            Id = "dwarf_warrior",
            Name = "Dwarf Warrior",
            ChallengeRating = "1/2",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            CharacterAlignment = Alignment.LawfulGood,
            HitDice = 1,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 13, DEX = 11, CON = 14, WIS = 9, INT = 10, CHA = 6,
            NaturalArmorBonus = 0,
            BaseSpeed = 4, // 20 ft (dwarves)
            BaseHitDieHP = 6,
            BAB = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Humanoid", "Dwarf", "MM35" },
            Feats = new List<string> { "Weapon Focus (dwarven waraxe)" },
            SpecialAbilities = new List<string> { "Darkvision 60 ft.", "Stonecunning", "+2 saves vs. poison", "+2 saves vs. spells", "+1 attack vs. orcs/goblinoids", "+4 dodge AC vs. giants", "Stability (+4 vs. bull rush/trip)" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("dwarven_waraxe", EquipSlot.MainHand),
                new EquipmentSlotPair("heavy_steel_shield", EquipSlot.OffHand),
                new EquipmentSlotPair("scale_mail", EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.65f, 0.55f, 0.4f, 1f),
            PanelColor = new Color(0.25f, 0.2f, 0.12f, 0.85f),
            NameColor = new Color(0.9f, 0.8f, 0.6f),
            Description = "Dwarf Warrior (CR 1/2). Stout humanoid fighter with dwarven waraxe. MM 3.5e p.91."
        });
    }

    /// <summary>
    /// Dire Boar (CR 4) — Large animal.
    /// MM 3.5e p.63. Ferocity: fights on below 0 HP without penalty.
    /// 7d8+21 HP (52), gore 1d8+12.
    /// </summary>
    private static void RegisterDireBoar()
    {
        Register(new NPCDefinition
        {
            Id = "dire_boar",
            Name = "Dire Boar",
            ChallengeRating = "4",
            Level = 7,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 7,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 27, DEX = 10, CON = 17, WIS = 13, INT = 2, CHA = 8,
            NaturalArmorBonus = 6,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 52,
            BAB = 5,
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Gore", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Animal", "MM35" },
            Feats = new List<string> { "Alertness", "Endurance", "Iron Will" },
            SpecialAbilities = new List<string> { "Ferocity (Ex): continues fighting below 0 HP", "Scent", "Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.55f, 0.4f, 0.3f, 1f),
            PanelColor = new Color(0.2f, 0.14f, 0.1f, 0.85f),
            NameColor = new Color(0.82f, 0.65f, 0.5f),
            Description = "Dire Boar (CR 4). Massive boar with ferocity—fights past 0 HP. MM 3.5e p.63."
        });
    }

    /// <summary>
    /// Dire Wolf (CR 3) — Large animal.
    /// MM 3.5e p.65. Trip attack on bite.
    /// 6d8+18 HP (45), bite 1d8+10.
    /// </summary>
    private static void RegisterDireWolf()
    {
        Register(new NPCDefinition
        {
            Id = "dire_wolf",
            Name = "Dire Wolf",
            ChallengeRating = "3",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 6,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 25, DEX = 15, CON = 17, WIS = 12, INT = 2, CHA = 10,
            NaturalArmorBonus = 3,
            BaseSpeed = 10, // 50 ft
            BaseHitDieHP = 45,
            BAB = 4,
            HasTripAttack = true,
            TripAttackCheckBonus = 11,
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Animal", "MM35" },
            Feats = new List<string> { "Alertness", "Run", "Weapon Focus (bite)" },
            SpecialAbilities = new List<string> { "Trip (Ex): free trip on bite (+11 check)", "Scent", "Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.5f, 0.48f, 0.42f, 1f),
            PanelColor = new Color(0.18f, 0.16f, 0.14f, 0.85f),
            NameColor = new Color(0.78f, 0.75f, 0.68f),
            Description = "Dire Wolf (CR 3). Pack predator with devastating trip attack. MM 3.5e p.65."
        });
    }

    /// <summary>
    /// Darkmantle (CR 1) — Small magical beast.
    /// MM 3.5e p.38. Stalactite-mimic that drops onto prey. Darkness aura, improved grab, constrict.
    /// 1d10+1 HP (6), slam 1d4+4.
    /// </summary>
    private static void RegisterDarkmantle()
    {
        Register(new NPCDefinition
        {
            Id = "darkmantle",
            Name = "Darkmantle",
            ChallengeRating = "1",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 16, DEX = 12, CON = 13, WIS = 10, INT = 2, CHA = 10,
            NaturalArmorBonus = 4,
            BaseSpeed = 4, // 20 ft, fly 30 ft
            BaseHitDieHP = 6,
            BAB = 1,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Slam",
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Magical Beast", "Fly", "MM35" },
            Feats = new List<string> { "Improved Initiative" },
            SpecialAbilities = new List<string> { "Darkness (Su): 60 ft radius, 10 min/day", "Improved Grab", "Constrict 1d4+4", "Blindsight 90 ft", "Fly 30 ft (poor)" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Grappler,
            SpriteColor = new Color(0.25f, 0.22f, 0.3f, 1f),
            PanelColor = new Color(0.1f, 0.08f, 0.15f, 0.85f),
            NameColor = new Color(0.55f, 0.5f, 0.65f),
            Description = "Darkmantle (CR 1). Stalactite mimic with darkness aura and constrict. MM 3.5e p.38."
        });
    }

    /// <summary>
    /// Dire Ape (CR 3) — Large animal. SNA IV.
    /// MM 3.5e p.62. Rend 2d6+9 if both claws hit.
    /// 5d8+13 HP (35), 2 claws +8 (1d6+6), bite +3 (1d8+3).
    /// </summary>
    private static void RegisterDireApe()
    {
        Register(new NPCDefinition
        {
            Id = "dire_ape",
            Name = "Dire Ape",
            ChallengeRating = "3",
            Level = 5,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 5,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 22, DEX = 15, CON = 14, WIS = 12, INT = 2, CHA = 7,
            NaturalArmorBonus = 4,
            BaseSpeed = 6, // 30 ft., climb 15 ft.
            BaseHitDieHP = 35,
            BAB = 3,
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 2, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Animal", "SummonBase", "MM35" },
            Feats = new List<string> { "Alertness", "Toughness" },
            SpecialAbilities = new List<string> { "Rend (Ex): 2d6+9 if both claws hit", "Scent", "Low-light vision", "Climb 15 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.35f, 0.25f, 0.2f, 1f),
            PanelColor = new Color(0.14f, 0.1f, 0.08f, 0.85f),
            NameColor = new Color(0.88f, 0.75f, 0.65f),
            Description = "Dire Ape (CR 3). Massive ape with rend—2d6+9 if both claws hit. 10 ft. reach. MM 3.5e p.62."
        });
    }

    /// <summary>
    /// Dire Wolverine (CR 4) — Large animal. SNA IV.
    /// MM 3.5e p.66. Rage when below 50% HP (+4 STR, +4 CON, -2 AC).
    /// 5d8+23 HP (45), 2 claws +8 (1d6+6), bite +3 (1d8+3).
    /// </summary>
    private static void RegisterDireWolverine()
    {
        Register(new NPCDefinition
        {
            Id = "dire_wolverine",
            Name = "Dire Wolverine",
            ChallengeRating = "4",
            Level = 5,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 5,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 22, DEX = 17, CON = 19, WIS = 12, INT = 2, CHA = 10,
            NaturalArmorBonus = 4,
            BaseSpeed = 6, // 30 ft., climb 10 ft.
            BaseHitDieHP = 45,
            BAB = 3,
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Animal", "SummonBase", "MM35" },
            Feats = new List<string> { "Alertness", "Toughness", "Track" },
            SpecialAbilities = new List<string> { "Rage (Ex): +4 STR, +4 CON, -2 AC when below 50% HP", "Scent", "Low-light vision", "Climb 10 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.4f, 0.32f, 0.25f, 1f),
            PanelColor = new Color(0.16f, 0.12f, 0.09f, 0.85f),
            NameColor = new Color(0.85f, 0.75f, 0.62f),
            Description = "Dire Wolverine (CR 4). Ferocious beast that rages when wounded (+4 STR/CON, -2 AC). MM 3.5e p.66."
        });
    }

    /// <summary>
    /// Deinonychus (CR 3) — Medium animal (dinosaur). SNA IV.
    /// MM 3.5e p.60. Pounce (full attack on charge). Very fast (60 ft).
    /// 4d8+16 HP (34), talons +6 (1d8+4), 2 foreclaws +1 (1d3+2), bite +1 (2d4+2).
    /// </summary>
    private static void RegisterDeinonychus()
    {
        Register(new NPCDefinition
        {
            Id = "deinonychus",
            Name = "Deinonychus",
            ChallengeRating = "3",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 4,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 19, DEX = 15, CON = 19, WIS = 12, INT = 2, CHA = 10,
            NaturalArmorBonus = 4,
            BaseSpeed = 12, // 60 ft. — very fast dinosaur
            BaseHitDieHP = 34,
            BAB = 3,
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Talons", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Foreclaw", DamageDice = 3, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Animal", "SummonBase", "MM35" },
            Feats = new List<string> { "Run" },
            SpecialAbilities = new List<string> { "Pounce (Ex): full attack on charge", "Scent", "Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.5f, 0.55f, 0.35f, 1f),
            PanelColor = new Color(0.18f, 0.2f, 0.12f, 0.85f),
            NameColor = new Color(0.88f, 0.92f, 0.72f),
            Description = "Deinonychus (CR 3). Swift predatory dinosaur with pounce—full attack on charge. 60 ft. speed. MM 3.5e p.60."
        });
    }
}

}
