using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: C
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_C()
    {
        RegisterCloaker();
        RegisterCockatrice();
        RegisterMonstrousCentipedes();
    
        RegisterSummonCrocodile();
        RegisterSummonConstrictorSnake();
    }

    /// <summary>
    /// Cloaker (CR 5) — MM 3.5e p.36. Aberration with engulf, moan, shadow shift.
    /// 6d8+18 HP (45), tail slap 1d6+5. Engulf = grapple wrap + bite 1d4+5.
    /// </summary>
    private static void RegisterCloaker()
    {
        Register(new NPCDefinition
        {
            Id = "cloaker",
            Name = "Cloaker",
            ChallengeRating = "5",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Aberration",
            HitDice = 6,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 19, DEX = 16, CON = 17, WIS = 15, INT = 14, CHA = 15,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Tail Slap", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 2, // 10 ft, fly 40 ft (average)
            BaseHitDieHP = 45,
            Engulf = new EngulfDefinition
            {
                ReflexSaveDC = 17,
                DamagePerRound = 8, // 1d4+5 bite while engulfed
                DamageType = DamageType.Bludgeoning,
                EscapeDC = 17
            },
            AuraAbility = new AuraAbilityDefinition
            {
                Name = "Moan",
                SaveDC = 15,
                IsWillSave = true,
                RangeFeet = 60,
                Effect = AuraEffectType.Frightened,
                DurationRounds = 2
            },
            CreatureTags = new List<string> { "Aberration", "MM35" },
            Feats = new List<string> { "Alertness", "Combat Reflexes", "Improved Initiative" },
            SpecialAbilities = new List<string> { "Engulf (wrap + bite)", "Moan (DC 15 Will, various effects, 60 ft.)", "Shadow shift (blur, mirror image, silent image)", "Darkvision 60 ft.", "Fly 40 ft. (average)" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.25f, 0.22f, 0.3f, 1f),
            PanelColor = new Color(0.1f, 0.08f, 0.15f, 0.85f),
            NameColor = new Color(0.55f, 0.5f, 0.7f),
            Description = "Cloaker (CR 5). Aberration. Tail slap, engulf wrap + bite, moan aura (fear/nausea/hold). MM 3.5e p.36."
        });
    }

    /// <summary>
    /// Cockatrice (CR 3) — MM 3.5e p.37. Small magical beast with petrification bite.
    /// 5d10 HP (27), bite 1d4-2 + petrification (Fort DC 12).
    /// </summary>
    private static void RegisterCockatrice()
    {
        Register(new NPCDefinition
        {
            Id = "cockatrice",
            Name = "Cockatrice",
            ChallengeRating = "3",
            Level = 5,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            HitDice = 5,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 6, DEX = 17, CON = 11, WIS = 13, INT = 2, CHA = 9,
            NaturalArmorBonus = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition
                {
                    Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1,
                    BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = true,
                    PetrificationOnHitDC = 12
                }
            },
            BaseSpeed = 4, // 20 ft, fly 60 ft (poor)
            BaseHitDieHP = 27,
            CreatureTags = new List<string> { "Magical Beast", "MM35" },
            Feats = new List<string> { "Alertness", "Dodge", "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Petrification (DC 12 Fort or turned to stone)", "Darkvision 60 ft.", "Low-light vision", "Fly 60 ft. (poor)" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.75f, 0.6f, 0.35f, 1f),
            PanelColor = new Color(0.3f, 0.22f, 0.1f, 0.85f),
            NameColor = new Color(0.95f, 0.82f, 0.5f),
            Description = "Cockatrice (CR 3). Small magical beast. Bite + petrification (DC 12 Fort). Fly 60 ft. MM 3.5e p.37."
        });
    }

    private static void RegisterMonstrousCentipedes()
    {
        RegisterMonstrousCentipedeVariant("monstrous_centipede_tiny", "Monstrous Centipede (Tiny)", 1, SizeCategory.Tiny, 1, 15, 10, 3, 1, 20, 1, 3, 10, "1 Dex", "1 Dex");
        RegisterMonstrousCentipedeVariant("monstrous_centipede_small", "Monstrous Centipede (Small)", 1, SizeCategory.Small, 2, 15, 10, 5, 1, 30, 1, 4, 10, "1d2 Dex", "1d2 Dex");
        RegisterMonstrousCentipedeVariant("monstrous_centipede_medium", "Monstrous Centipede (Medium)", 1, SizeCategory.Medium, 4, 15, 10, 9, 2, 40, 1, 6, 10, "1d3 Dex", "1d3 Dex");
        RegisterMonstrousCentipedeVariant("monstrous_centipede_large", "Monstrous Centipede (Large)", 3, SizeCategory.Large, 13, 15, 10, 13, 3, 40, 1, 8, 11, "1d4 Dex", "1d4 Dex");
        RegisterMonstrousCentipedeVariant("monstrous_centipede_huge", "Monstrous Centipede (Huge)", 6, SizeCategory.Huge, 33, 15, 12, 17, 6, 40, 2, 6, 14, "1d6 Dex", "1d6 Dex");
        RegisterMonstrousCentipedeVariant("monstrous_centipede_gargantuan", "Monstrous Centipede (Gargantuan)", 12, SizeCategory.Gargantuan, 66, 15, 12, 23, 10, 40, 2, 8, 17, "1d8 Dex", "1d8 Dex");
        RegisterMonstrousCentipedeVariant("monstrous_centipede_colossal", "Monstrous Centipede (Colossal)", 24, SizeCategory.Colossal, 132, 13, 12, 27, 16, 40, 4, 6, 23, "2d6 Dex", "2d6 Dex");
    }

    private static void RegisterMonstrousCentipedeVariant(string id, string name, int hitDice, SizeCategory size, int hp, int dex, int con, int str, int naturalArmor, int speed, int damageCount, int damageDice, int poisonDc, string poisonInitial, string poisonSecondary)
    {
        Register(new NPCDefinition
        {
            Id = id,
            Name = name,
            ChallengeRating = id switch
            {
                "monstrous_centipede_tiny" => "1/8",
                "monstrous_centipede_small" => "1/4",
                "monstrous_centipede_medium" => "1/2",
                "monstrous_centipede_large" => "1",
                "monstrous_centipede_huge" => "2",
                "monstrous_centipede_gargantuan" => "6",
                "monstrous_centipede_colossal" => "8",
                _ => null
            },
            Level = Mathf.Max(1, hitDice),
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = Mathf.Max(1, hitDice),
            SizeCategory = size,
            IsTallCreature = false,
            STR = str, DEX = dex, CON = con, WIS = 10, INT = 1, CHA = 2,
            NaturalArmorBonus = naturalArmor,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = damageDice, DamageCount = damageCount, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = speed / 5,
            BaseHitDieHP = hp,
            CreatureTags = new List<string> { "Vermin", "MM35" },
            Feats = new List<string> { "Weapon Finesse" },
            SpecialAbilities = new List<string>
            {
                $"Poison (Fort DC {poisonDc}; initial {poisonInitial}; secondary {poisonSecondary})",
                "Climb speed equals land speed",
                "Darkvision 60 ft",
                "Vermin traits",
                "Mindless"
            },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.68f, 0.2f, 0.16f, 1f),
            PanelColor = new Color(0.22f, 0.08f, 0.07f, 0.85f),
            NameColor = new Color(0.96f, 0.78f, 0.74f),
            Description = $"Monster Manual {name.ToLowerInvariant()}. Poisonous vermin striker with climb mobility and vermin immunities."
        });
    }



    private static void RegisterSummonCrocodile()
    {
        Register(new NPCDefinition
        {
            Id = "crocodile",
            Name = "Crocodile",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 19, DEX = 12, CON = 17, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 4,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 4, // 20 ft. land, swim 30 ft.
            BaseHitDieHP = 22,
            CreatureTags = new List<string> { "Animal", "Aquatic", "SummonBase" },
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Bite",
            HasScent = true,
            SpecialAbilities = new List<string> { "Improved grab", "Hold breath (68 rounds)", "Low-light vision", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.42f, 0.56f, 0.34f, 1f),
            PanelColor = new Color(0.12f, 0.2f, 0.1f, 0.85f),
            NameColor = new Color(0.84f, 0.93f, 0.78f),
            Description = "Crocodile with improved grab on bite. Hold breath 68 rounds. +8 Swim, +4 Hide in water. MM 3.5e p.271."
        });
    }

    private static void RegisterSummonConstrictorSnake()
    {
        Register(new NPCDefinition
        {
            Id = "constrictor_snake",
            Name = "Constrictor Snake",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 17, DEX = 17, CON = 13, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 3, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 4, // 20 ft., climb 20 ft., swim 20 ft.
            BaseHitDieHP = 19,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Bite",
            HasScent = true,
            SpecialAbilities = new List<string> { "Constrict (1d3+4)", "Improved grab", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.56f, 0.64f, 0.28f, 1f),
            PanelColor = new Color(0.2f, 0.23f, 0.1f, 0.85f),
            NameColor = new Color(0.9f, 0.95f, 0.74f),
            Description = "Constrictor snake. Bite +5 (1d3+4), improved grab, constrict 1d3+4. MM 3.5e p.280."
        });
    }

}
