using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual undead creatures (beyond those already in other files).
/// Ghoul, Ghast, Vampire Spawn, Mummy, Mohrg, Spectre, Greater Shadow, Bodak.
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_Undead2()
    {
        RegisterGhoul();
        RegisterGhast();
        RegisterVampireSpawn();
        RegisterMummy();
        RegisterMohrg();
        RegisterSpectre();
        RegisterGreaterShadow();
        RegisterBodak();
    }

    // ════════════════════════════════════════════════════════════
    //  Ghoul — MM p.118
    //  Undead, Medium, CR 1
    //  2d12 HP (13), bite +2 melee (1d6+1 + paralysis), 2 claws +0 melee (1d3 + paralysis)
    //  Str 13, Dex 15, Con 0, Int 13, Wis 14, Cha 12
    //  AC 14 (+2 Dex, +2 natural), Fort +0, Ref +2, Will +5
    //  Paralysis: DC 14 Fort or paralyzed 1d4+1 rounds (elves immune)
    // ════════════════════════════════════════════════════════════
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
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true, ParalysisOnHitDC = 14, ParalysisOnHitDurationRounds = 4 },
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 3, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false, ParalysisOnHitDC = 14, ParalysisOnHitDurationRounds = 4 }
            },
            CreatureTags = new List<string> { "Undead", "Darkvision60", "MM35" },
            Feats = new List<string> { "Multiattack" },
            SpecialAbilities = new List<string> { "Ghoul Fever (disease)", "Paralysis (Su): DC 14 Fort, 1d4+1 rounds, elves immune", "Darkvision 60 ft.", "Undead traits", "+2 turn resistance" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.45f, 0.5f, 0.42f, 1f),
            PanelColor = new Color(0.15f, 0.18f, 0.12f, 0.85f),
            NameColor = new Color(0.7f, 0.8f, 0.65f),
            Description = "Ghoul (CR 1). Undead with paralysing bite and claws. Ghoul fever. MM 3.5e p.118."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Ghast — MM p.119
    //  Undead, Medium, CR 3
    //  4d12+3 HP (29), bite +4 melee (1d8+3 + paralysis), 2 claws +1 melee (1d4+1 + paralysis)
    //  Str 17, Dex 15, Con 0, Int 13, Wis 14, Cha 16
    //  AC 17 (+2 Dex, +5 natural), stench 10 ft DC 15
    //  Paralysis: DC 15 Fort or paralyzed 1d4+1 rounds (even elves)
    // ════════════════════════════════════════════════════════════
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
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true, ParalysisOnHitDC = 15, ParalysisOnHitDurationRounds = 4 },
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false, ParalysisOnHitDC = 15, ParalysisOnHitDurationRounds = 4 }
            },
            CreatureTags = new List<string> { "Undead", "Darkvision60", "MM35" },
            Feats = new List<string> { "Multiattack", "Toughness" },
            SpecialAbilities = new List<string> { "Ghoul Fever", "Paralysis (Su): DC 15 Fort, 1d4+1 rounds (elves NOT immune)", "Stench (Ex): 10 ft., DC 15 Fort or sickened 1d6+4 min", "Darkvision 60 ft.", "Undead traits", "+2 turn resistance" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.4f, 0.48f, 0.38f, 1f),
            PanelColor = new Color(0.12f, 0.16f, 0.1f, 0.85f),
            NameColor = new Color(0.65f, 0.78f, 0.6f),
            Description = "Ghast (CR 3). Advanced ghoul with stench aura, paralysis affects even elves. MM 3.5e p.119."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Vampire Spawn — MM p.253
    //  Undead, Medium, CR 4
    //  4d12+3 HP (29), slam +5 melee (1d6+4 + energy drain)
    //  Str 16, Dex 14, Con 0, Int 13, Wis 13, Cha 14
    //  AC 15 (+2 Dex, +3 natural), DR 5/silver
    //  Energy Drain: 1 negative level, DC 14 Fort to remove
    //  Fast healing 2, gaseous form, spider climb
    // ════════════════════════════════════════════════════════════
    private static void RegisterVampireSpawn()
    {
        Register(new NPCDefinition
        {
            Id = "vampire_spawn",
            Name = "Vampire Spawn",
            ChallengeRating = "4",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Undead",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 4,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 16, DEX = 14, CON = 0, WIS = 13, INT = 13, CHA = 14,
            NaturalArmorBonus = 3,
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Silver,
            BaseSpeed = 6,
            BaseHitDieHP = 29,
            BAB = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true, EnergyDrainOnHit = 1, EnergyDrainRemovalDC = 14 }
            },
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Electricity, Amount = 10 }
            },
            CreatureTags = new List<string> { "Undead", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Improved Initiative", "Lightning Reflexes", "Toughness" },
            SpecialAbilities = new List<string> { "Energy Drain (Su): 1 negative level on slam, DC 14 Fort", "Dominate (Su): DC 14 Will, as dominate person", "Blood Drain (Ex): Pin + 1d4 CON drain", "Fast Healing 2", "Gaseous Form (Su)", "Spider Climb (Ex)", "DR 5/silver", "Resist cold 10, electricity 10", "+2 turn resistance", "Darkvision 60 ft.", "Undead traits" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.35f, 0.25f, 0.3f, 1f),
            PanelColor = new Color(0.15f, 0.08f, 0.12f, 0.85f),
            NameColor = new Color(0.7f, 0.4f, 0.5f),
            Description = "Vampire Spawn (CR 4). Lesser vampire with energy drain and domination. MM 3.5e p.253."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Mummy — MM p.190
    //  Undead, Medium, CR 5
    //  8d12+3 HP (55), slam +11 melee (1d6+10 + mummy rot)
    //  Str 24, Dex 10, Con 0, Int 6, Wis 14, Cha 15
    //  AC 20 (+10 natural), DR 5/—, Vulnerable to fire
    //  Despair aura: 30 ft, Will DC 16 or paralyzed with fear 1d4 rounds
    // ════════════════════════════════════════════════════════════
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
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.6f, 0.55f, 0.4f, 1f),
            PanelColor = new Color(0.22f, 0.2f, 0.12f, 0.85f),
            NameColor = new Color(0.85f, 0.78f, 0.55f),
            Description = "Mummy (CR 5). Bandaged undead with despair aura and mummy rot. MM 3.5e p.190."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Mohrg — MM p.189
    //  Undead, Medium, CR 8
    //  14d12 HP (91), slam +12 melee (1d6+7), tongue +12 melee (paralysis)
    //  Str 21, Dex 19, Con 0, Int 11, Wis 10, Cha 10
    //  AC 23 (+4 Dex, +9 natural), paralyzing tongue
    //  Creates zombies from slain victims
    // ════════════════════════════════════════════════════════════
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
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Grappler,
            SpriteColor = new Color(0.5f, 0.35f, 0.3f, 1f),
            PanelColor = new Color(0.2f, 0.1f, 0.08f, 0.85f),
            NameColor = new Color(0.8f, 0.55f, 0.45f),
            Description = "Mohrg (CR 8). Skeletal undead with paralyzing tongue that creates zombie spawn. MM 3.5e p.189."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Spectre — MM p.232
    //  Undead (Incorporeal), Medium, CR 7
    //  7d12 HP (45), incorporeal touch +6 melee (1d8 + energy drain)
    //  Str 0, Dex 16, Con 0, Int 14, Wis 14, Cha 15
    //  AC 15 (+3 Dex, +2 deflection), incorporeal
    //  Energy drain: 2 negative levels per hit, DC 15 Fort
    //  Sunlight powerlessness, unnatural aura
    // ════════════════════════════════════════════════════════════
    private static void RegisterSpectre()
    {
        Register(new NPCDefinition
        {
            Id = "spectre",
            Name = "Spectre",
            ChallengeRating = "7",
            Level = 7,
            CharacterClass = "Warrior",
            CreatureType = "Undead",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 7,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 0, DEX = 16, CON = 0, WIS = 14, INT = 14, CHA = 15,
            NaturalArmorBonus = 0,
            IsIncorporeal = true,
            BaseSpeed = 8, // Fly 40 ft (perfect)
            BaseHitDieHP = 45,
            BAB = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Incorporeal Touch", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.None, Range = 1, IsPrimary = true, EnergyDrainOnHit = 2, EnergyDrainRemovalDC = 15 }
            },
            CreatureTags = new List<string> { "Undead", "Incorporeal", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Blind-Fight", "Improved Initiative" },
            SpecialAbilities = new List<string> { "Energy Drain (Su): 2 negative levels per touch, DC 15 Fort", "Create Spawn: humanoids slain become spectres in 1d4 rounds", "Incorporeal", "Sunlight Powerlessness", "Unnatural Aura: 30 ft., animals panicked", "+2 turn resistance", "Darkvision 60 ft.", "Fly 40 ft. (perfect)" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.3f, 0.35f, 0.5f, 0.5f),
            PanelColor = new Color(0.08f, 0.1f, 0.2f, 0.85f),
            NameColor = new Color(0.5f, 0.6f, 0.85f),
            Description = "Spectre (CR 7). Powerful incorporeal undead with double energy drain. MM 3.5e p.232."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Greater Shadow — MM p.221 (advanced Shadow)
    //  Undead (Incorporeal), Medium, CR 8
    //  9d12 HP (58), incorporeal touch +6 melee (1d8 + 1d8 STR damage)
    //  Str 0, Dex 16, Con 0, Int 6, Wis 12, Cha 14
    //  AC 14 (+3 Dex, +1 deflection), incorporeal
    //  Strength Damage: 1d8 STR per hit
    //  Create Spawn
    // ════════════════════════════════════════════════════════════
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
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.15f, 0.15f, 0.2f, 0.5f),
            PanelColor = new Color(0.05f, 0.05f, 0.1f, 0.85f),
            NameColor = new Color(0.4f, 0.4f, 0.55f),
            Description = "Greater Shadow (CR 8). Advanced incorporeal undead draining 1d8 STR per touch. MM 3.5e p.221."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Bodak — MM p.28
    //  Undead (Extraplanar), Medium, CR 8
    //  9d12 HP (58), slam +6 melee (1d8+1)
    //  Str 13, Dex 15, Con 0, Int 6, Wis 12, Cha 12
    //  AC 20 (+2 Dex, +8 natural), DR 10/cold iron
    //  Death Gaze: 30 ft., Fort DC 15 or die, 1 neg level on save
    //  Vulnerable to sunlight (1 HP/round)
    // ════════════════════════════════════════════════════════════
    private static void RegisterBodak()
    {
        Register(new NPCDefinition
        {
            Id = "bodak",
            Name = "Bodak",
            ChallengeRating = "8",
            Level = 9,
            CharacterClass = "Warrior",
            CreatureType = "Undead",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 9,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 13, DEX = 15, CON = 0, WIS = 12, INT = 6, CHA = 12,
            NaturalArmorBonus = 8,
            DamageReductionAmount = 10,
            DamageReductionBypass = DamageBypassTag.ColdIron,
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Acid, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 10 }
            },
            BaseSpeed = 4, // 20 ft
            BaseHitDieHP = 58,
            BAB = 4,
            Immunities = new CreatureImmunities { immuneToElectricity = true },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Undead", "Extraplanar", "Darkvision60", "MM35" },
            Feats = new List<string> { "Dodge", "Improved Initiative", "Toughness" },
            SpecialAbilities = new List<string> { "Death Gaze (Su): 30 ft., DC 15 Fort or die; save = 1 negative level", "DR 10/cold iron", "Resist acid 10, fire 10", "Immune to electricity", "Vulnerable to sunlight (1 HP damage/round)", "Flashback (Ex): creatures seeing bodak, DC 15 Fort or stunned 1 round", "Darkvision 60 ft.", "Undead traits" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.25f, 0.2f, 0.3f, 1f),
            PanelColor = new Color(0.08f, 0.06f, 0.12f, 0.85f),
            NameColor = new Color(0.5f, 0.4f, 0.65f),
            Description = "Bodak (CR 8). Undead with death gaze that kills on failed Fort save. MM 3.5e p.28."
        });
    }
}
