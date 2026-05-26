using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual magical beast creatures.
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_MagicalBeasts()
    {
        RegisterWorg();
        RegisterOwlbear();
        RegisterDisplacerBeast();
        RegisterManticore();
        RegisterBasilisk();
        RegisterKrenshar();
        RegisterShockerLizard();
        RegisterBehir();
        RegisterGorgon();
        RegisterDigester();
    }

    // ════════════════════════════════════════════════════════════
    //  Worg — MM p.256
    //  Magical Beast, Medium, CR 2
    //  4d10+8 HP (30), bite +7 melee (1d6+4)
    //  Str 17, Dex 15, Con 15, Int 6, Wis 14, Cha 10
    //  AC 14 (+2 Dex, +2 natural), Trip
    // ════════════════════════════════════════════════════════════
    private static void RegisterWorg()
    {
        Register(new NPCDefinition
        {
            Id = "worg",
            Name = "Worg",
            ChallengeRating = "2",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            CharacterAlignment = Alignment.NeutralEvil,
            HitDice = 4,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 17, DEX = 15, CON = 15, WIS = 14, INT = 6, CHA = 10,
            NaturalArmorBonus = 2,
            BaseSpeed = 10, // 50 ft
            BaseHitDieHP = 30,
            BAB = 4,
            HasScent = true,
            HasTripAttack = true,
            TripAttackCheckBonus = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Magical Beast", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Track" },
            SpecialAbilities = new List<string> { "Trip (Ex): free trip on bite hit", "Scent", "Darkvision 60 ft.", "Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.3f, 0.3f, 0.35f, 1f),
            PanelColor = new Color(0.08f, 0.08f, 0.12f, 0.85f),
            NameColor = new Color(0.5f, 0.5f, 0.6f),
            Description = "Worg (CR 2). Evil intelligent wolf with trip attack. MM 3.5e p.256."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Owlbear — MM p.206
    //  Magical Beast, Large, CR 4
    //  5d10+25 HP (52), 2 claws +9 melee (1d6+5), bite +4 melee (1d8+2)
    //  Str 21, Dex 12, Con 21, Int 2, Wis 12, Cha 10
    //  AC 15 (-1 size, +1 Dex, +5 natural), Improved Grab
    // ════════════════════════════════════════════════════════════
    private static void RegisterOwlbear()
    {
        Register(new NPCDefinition
        {
            Id = "owlbear",
            Name = "Owlbear",
            ChallengeRating = "4",
            Level = 5,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 5,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 21, DEX = 12, CON = 21, WIS = 12, INT = 2, CHA = 10,
            NaturalArmorBonus = 5,
            BaseSpeed = 6,
            BaseHitDieHP = 52,
            BAB = 5,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Claw",
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 2, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Magical Beast", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Track" },
            SpecialAbilities = new List<string> { "Improved Grab", "Scent", "Darkvision 60 ft.", "Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Grappler,
            SpriteColor = new Color(0.55f, 0.45f, 0.3f, 1f),
            PanelColor = new Color(0.2f, 0.15f, 0.08f, 0.85f),
            NameColor = new Color(0.82f, 0.7f, 0.48f),
            Description = "Owlbear (CR 4). Ferocious hybrid beast with improved grab. MM 3.5e p.206."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Displacer Beast — MM p.66
    //  Magical Beast, Large, CR 4
    //  6d10+18 HP (51), 2 tentacles +9 melee (1d6+4), bite +4 melee (1d8+2)
    //  Str 18, Dex 12, Con 16, Int 5, Wis 12, Cha 8
    //  AC 16 (-1 size, +1 Dex, +6 natural)
    //  Displacement: 50% miss chance
    //  Resist ranged: +2 save vs ranged spells/effects
    // ════════════════════════════════════════════════════════════
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

    // ════════════════════════════════════════════════════════════
    //  Manticore — MM p.179
    //  Magical Beast, Large, CR 5
    //  6d10+24 HP (57), 2 claws +10 melee (2d4+5), bite +8 melee (1d8+2)
    //  or 6 spikes +8 ranged (1d8+2, 180 ft.)
    //  Str 20, Dex 15, Con 19, Int 7, Wis 12, Cha 9
    //  AC 17 (-1 size, +2 Dex, +6 natural)
    // ════════════════════════════════════════════════════════════
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
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Ranged,
            SpriteColor = new Color(0.55f, 0.4f, 0.3f, 1f),
            PanelColor = new Color(0.2f, 0.12f, 0.08f, 0.85f),
            NameColor = new Color(0.82f, 0.62f, 0.48f),
            Description = "Manticore (CR 5). Winged lion-beast that fires tail spikes at range. MM 3.5e p.179."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Basilisk — MM p.23
    //  Magical Beast, Medium, CR 5
    //  6d10+12 HP (45), bite +8 melee (1d8+3)
    //  Str 15, Dex 8, Con 15, Int 2, Wis 12, Cha 11
    //  AC 16 (-1 Dex, +7 natural)
    //  Petrifying Gaze: 30 ft., Fort DC 13 or permanently petrified
    // ════════════════════════════════════════════════════════════
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

    // ════════════════════════════════════════════════════════════
    //  Krenshar — MM p.163
    //  Magical Beast, Medium, CR 1
    //  2d10 HP (11), bite +2 melee (1d6+1), 2 claws +0 melee (1d4)
    //  Str 13, Dex 15, Con 11, Int 6, Wis 12, Cha 13
    //  AC 15 (+2 Dex, +3 natural)
    //  Scare: Will DC 12 or panicked 1d4 rounds
    // ════════════════════════════════════════════════════════════
    private static void RegisterKrenshar()
    {
        Register(new NPCDefinition
        {
            Id = "krenshar",
            Name = "Krenshar",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 13, DEX = 15, CON = 11, WIS = 12, INT = 6, CHA = 13,
            NaturalArmorBonus = 3,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 11,
            BAB = 2,
            HasScent = true,
            AuraAbility = new AuraAbilityDefinition
            {
                Name = "Scare",
                SaveDC = 12,
                IsWillSave = true,
                RangeFeet = 30,
                Effect = AuraEffectType.Frightened,
                DurationRounds = 4
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Magical Beast", "Darkvision60", "MM35" },
            Feats = new List<string> { "Multiattack", "Track" },
            SpecialAbilities = new List<string> { "Scare (Ex/Sp): retract face skin, Will DC 12 or panicked 1d4 rounds (1+ HD), shaken if 6+ HD", "Scent", "Darkvision 60 ft.", "Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.6f, 0.5f, 0.4f, 1f),
            PanelColor = new Color(0.22f, 0.18f, 0.12f, 0.85f),
            NameColor = new Color(0.85f, 0.75f, 0.6f),
            Description = "Krenshar (CR 1). Feline beast that retracts face skin to frighten prey. MM 3.5e p.163."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Shocker Lizard — MM p.224
    //  Magical Beast, Small, CR 2
    //  2d10+2 HP (13), bite +3 melee (1d4+1)
    //  Str 10, Dex 15, Con 13, Int 2, Wis 12, Cha 6
    //  AC 16 (+1 size, +2 Dex, +3 natural)
    //  Stunning Shock: 5 ft., Ref DC 12 or stunned 1 round (1d8 nonlethal)
    //  Lethal Shock: 3+ lizards, 2d8/lizard in 20 ft., Ref DC 12 half
    // ════════════════════════════════════════════════════════════
    private static void RegisterShockerLizard()
    {
        Register(new NPCDefinition
        {
            Id = "shocker_lizard",
            Name = "Shocker Lizard",
            ChallengeRating = "2",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 2,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 10, DEX = 15, CON = 13, WIS = 12, INT = 2, CHA = 6,
            NaturalArmorBonus = 3,
            BaseSpeed = 8, // 40 ft, climb 20 ft, swim 20 ft
            BaseHitDieHP = 13,
            BAB = 2,
            Immunities = new CreatureImmunities { immuneToElectricity = true },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Magical Beast", "Darkvision60", "MM35" },
            Feats = new List<string> { "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Stunning Shock (Su): 5 ft., 1d8 nonlethal + Ref DC 12 or stunned 1 round", "Lethal Shock (Su): 3+ lizards within 20 ft., 2d8 elec/lizard, Ref DC 12 half", "Electricity Sense: 100 ft. range", "Immune to electricity", "Darkvision 60 ft., Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.4f, 0.55f, 0.65f, 1f),
            PanelColor = new Color(0.12f, 0.2f, 0.25f, 0.85f),
            NameColor = new Color(0.6f, 0.8f, 0.9f),
            Description = "Shocker Lizard (CR 2). Small lizard that deals electrical damage; lethal in groups. MM 3.5e p.224."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Behir — MM p.25
    //  Magical Beast, Huge, CR 8
    //  9d10+45 HP (94), bite +14 melee (2d4+8)
    //  Str 26, Dex 13, Con 21, Int 7, Wis 14, Cha 12
    //  AC 17 (-2 size, +1 Dex, +8 natural)
    //  Breath Weapon: 20 ft. line, 7d6 electricity, Ref DC 19 half (1d4 round recharge)
    //  Improved Grab, Swallow Whole, Constrict 2d8+8, Rake 1d4+4
    // ════════════════════════════════════════════════════════════
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

    // ════════════════════════════════════════════════════════════
    //  Gorgon — MM p.137
    //  Magical Beast, Large, CR 8
    //  8d10+40 HP (84), gore +14 melee (1d8+7)
    //  Str 21, Dex 12, Con 21, Int 2, Wis 12, Cha 9
    //  AC 20 (-1 size, +1 Dex, +10 natural)
    //  Breath Weapon: 60 ft. cone, Fort DC 19 or petrified (permanent)
    //  Trample 1d8+7 (Ref DC 19)
    // ════════════════════════════════════════════════════════════
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
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.4f, 0.42f, 0.45f, 1f),
            PanelColor = new Color(0.12f, 0.14f, 0.16f, 0.85f),
            NameColor = new Color(0.65f, 0.68f, 0.72f),
            Description = "Gorgon (CR 8). Iron-skinned bull with petrifying breath. MM 3.5e p.137."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Digester — MM p.59
    //  Magical Beast, Medium, CR 6
    //  8d10+24 HP (68), claw +11 melee (1d8+4)
    //  Str 17, Dex 15, Con 17, Int 2, Wis 12, Cha 10
    //  AC 17 (+2 Dex, +5 natural)
    //  Acid Spray: 20 ft., 4d8 acid (Ref DC 17 half)
    // ════════════════════════════════════════════════════════════
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
}
