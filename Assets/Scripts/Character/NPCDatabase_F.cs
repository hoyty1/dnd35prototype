using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual creatures: F
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_F()
    {
        RegisterGiantFireBeetle();
        RegisterFiendishDireRat();
        RegisterFlamebrotherSalamander();
        RegisterFleshGolem();
        RegisterFormianTaskmaster();
        RegisterFormianWorker();
        RegisterFrostGiant();
        RegisterFireGiant();

    }

    private static void RegisterGiantFireBeetle()
    {
        Register(new NPCDefinition
        {
            Id = "giant_fire_beetle",
            Name = "Giant Fire Beetle",
            ChallengeRating = "1/3",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 10, DEX = 11, CON = 11, WIS = 10, INT = 1, CHA = 7,
            NaturalArmorBonus = 5,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 6,
            BaseHitDieHP = 4,
            CreatureTags = new List<string> { "Vermin", "MM35" },
            SpecialAbilities = new List<string> { "Light Glands (10-ft radius red glow)", "Darkvision 60 ft", "Vermin traits", "Mindless" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.75f, 0.38f, 0.2f, 1f),
            PanelColor = new Color(0.25f, 0.1f, 0.08f, 0.85f),
            NameColor = new Color(1f, 0.78f, 0.64f),
            Description = "Monster Manual giant fire beetle. Small vermin with hard shell, strong bite, and bioluminescent glands."
        });
    
    private static void RegisterFiendishDireRat()
    {
        Register(new NPCDefinition
        {
            Id = "fiendish_dire_rat",
            Name = "Fiendish Dire Rat",
            ChallengeRating = "1/3",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            CharacterAlignment = Alignment.NeutralEvil,
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 10, DEX = 17, CON = 12, WIS = 12, INT = 3, CHA = 4,
            NaturalArmorBonus = 1,
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 5 },
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 5 }
            },
            BaseSpeed = 8, // 40 ft, climb 20 ft
            BaseHitDieHP = 5,
            BAB = 0,
            HasScent = true,
            GainsSmiteGood = true,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.None, Range = 1, IsPrimary = true, HasDiseaseOnHit = true, DiseaseOnHitType = DiseaseType.FilthFever }
            },
            CreatureTags = new List<string> { "Magical Beast", "Extraplanar", "Evil", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Weapon Finesse" },
            SpecialAbilities = new List<string> { "Smite Good (Su): 1/day, +1 damage vs good", "Disease (Ex): filth fever, DC 11 Fort", "Resist cold 5, fire 5", "Scent", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.3f, 0.25f, 0.2f, 1f),
            PanelColor = new Color(0.08f, 0.05f, 0.03f, 0.85f),
            NameColor = new Color(0.5f, 0.4f, 0.3f),
            Description = "Fiendish Dire Rat (CR 1/3). Evil dire rat with smite good and disease. MM 3.5e."
        });
    }

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

    private static void RegisterFleshGolem()
    {
        Register(new NPCDefinition
        {
            Id = "flesh_golem",
            Name = "Flesh Golem",
            ChallengeRating = "7",
            Level = 9,
            CharacterClass = "Warrior",
            CreatureType = "Construct",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 9,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 21, DEX = 9, CON = 0, WIS = 11, INT = 0, CHA = 1,
            NaturalArmorBonus = 10,
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Adamantine,
            IsMindless = true,
            BaseSpeed = 6,
            BaseHitDieHP = 79,
            BAB = 6,
            Immunities = new CreatureImmunities
            {
                immuneToMindAffecting = true,
                immuneToCriticalHits = true,
                immuneToSneakAttack = true,
                immuneToPoison = true,
                immuneToDisease = true
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 8, DamageCount = 2, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Construct", "Darkvision60", "MM35" },
            Feats = new List<string>(),
            SpecialAbilities = new List<string> { "Berserk: 1% cumulative chance/round of going berserk after taking damage", "Magic Immunity (Ex): immune to any spell/SLA that allows SR, EXCEPT: fire heals 1/3 points, cold/electricity slows 2d6 rounds", "DR 5/adamantine", "Construct traits (immune to crits, sneak, mind-affecting, poison, disease, death effects, necromancy, sleep, paralysis, stun, ability damage/drain, energy drain, nonlethal)", "Darkvision 60 ft., Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.45f, 0.42f, 0.38f, 1f),
            PanelColor = new Color(0.15f, 0.14f, 0.1f, 0.85f),
            NameColor = new Color(0.7f, 0.65f, 0.58f),
            Description = "Flesh Golem (CR 7). Stitched construct immune to magic, healed by fire. MM 3.5e p.135."
        });
    }

    private static void RegisterFormianTaskmaster()
    {
        Register(new NPCDefinition
        {
            Id = "formian_taskmaster",
            Name = "Formian Taskmaster",
            ChallengeRating = "7",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.LawfulNeutral,
            HitDice = 6,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 17, DEX = 16, CON = 14, WIS = 16, INT = 16, CHA = 19,
            NaturalArmorBonus = 6,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 39,
            BAB = 6,
            Immunities = new CreatureImmunities { immuneToPoison = true },
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Sonic, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 10 }
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Sting", DamageDice = 4, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Outsider", "Lawful", "HiveMind", "Darkvision60", "MM35" },
            Feats = new List<string> { "Dodge", "Improved Initiative" },
            SpecialAbilities = new List<string> { "Dominate Monster (Su): DC 17 Will, range 30 ft., up to 6 targets", "Poison (Ex): sting, DC 15 Fort, 1d6 Str/1d6 Str", "Hive Mind (Ex): all formians within 50 mi. in contact", "Immune to poison/petrification", "Resist sonic 10, cold 10, fire 10", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Spellcaster,
            SpriteColor = new Color(0.6f, 0.5f, 0.3f, 1f),
            PanelColor = new Color(0.22f, 0.18f, 0.08f, 0.85f),
            NameColor = new Color(0.85f, 0.75f, 0.48f),
            Description = "Formian Taskmaster (CR 7). Ant-like overseer with dominate monster. MM 3.5e p.109."
        });
    }

    private static void RegisterFormianWorker()
    {
        Register(new NPCDefinition
        {
            Id = "formian_worker",
            Name = "Formian Worker",
            ChallengeRating = "1/2",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            CharacterAlignment = Alignment.LawfulNeutral,
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 13, DEX = 14, CON = 13, WIS = 10, INT = 6, CHA = 9,
            NaturalArmorBonus = 4,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 5,
            BAB = 1,
            Immunities = new CreatureImmunities { immuneToPoison = true },
            DamageResistances = new List<DamageResistanceEntry>
            {
                new DamageResistanceEntry { Type = DamageType.Sonic, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Cold, Amount = 10 },
                new DamageResistanceEntry { Type = DamageType.Fire, Amount = 10 }
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Outsider", "Lawful", "HiveMind", "Darkvision60", "MM35" },
            Feats = new List<string> { "Skill Focus (Craft)" },
            SpecialAbilities = new List<string> { "Hive Mind (Ex): all formians within 50 mi. in contact", "Cure Serious Wounds (Sp): 8/day (hive structures only)", "Make Whole (Sp): 3/day", "Immune to poison/petrification", "Resist sonic 10, cold 10, fire 10", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.65f, 0.55f, 0.35f, 1f),
            PanelColor = new Color(0.25f, 0.2f, 0.1f, 0.85f),
            NameColor = new Color(0.88f, 0.78f, 0.55f),
            Description = "Formian Worker (CR 1/2). Ant-like outsider laborer with hive mind. MM 3.5e p.108."
        });
    }

    /// <summary>
    /// Frost Giant (CR 9) — Large giant.
    /// MM 3.5e p.122. 14d8+70 HP (133), greataxe 2d8+13 or rock 2d6+9 (120 ft).
    /// Rock throwing/catching. Cold immunity, fire vulnerability.
    /// </summary>
    private static void RegisterFrostGiant()
    {
        Register(new NPCDefinition
        {
            Id = "frost_giant",
            Name = "Frost Giant",
            ChallengeRating = "9",
            Level = 14,
            CharacterClass = "Warrior",
            CreatureType = "Giant",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 14,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 29, DEX = 9, CON = 21, WIS = 14, INT = 10, CHA = 11,
            NaturalArmorBonus = 9,
            BaseSpeed = 8, // 40 ft.
            BaseHitDieHP = 133,
            BAB = 10,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            DamageImmunities = new List<DamageType> { DamageType.Cold },
            CreatureTags = new List<string> { "Giant", "Cold", "RockThrowing", "MM35" },
            Feats = new List<string> { "Cleave", "Great Cleave", "Improved Sunder", "Power Attack", "Weapon Focus (greataxe)" },
            SpecialAbilities = new List<string> { "Rock Throwing (Ex): 120 ft., 2d6+9", "Rock Catching (Ex): Ref DC 25", "Immunity to cold", "Vulnerability to fire (+50% damage)", "Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("greataxe", EquipSlot.MainHand),
                new EquipmentSlotPair("chain_shirt", EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.65f, 0.75f, 0.85f, 1f),
            PanelColor = new Color(0.15f, 0.2f, 0.3f, 0.85f),
            NameColor = new Color(0.7f, 0.85f, 0.95f),
            Description = "Frost Giant (CR 9). Cold-immune giant with greataxe and rock throwing. MM 3.5e p.122."
        });
    }

    /// <summary>
    /// Fire Giant (CR 10) — Large giant.
    /// MM 3.5e p.121. 15d8+75 HP (142), greatsword 2d8+10 or rock 2d6+7 (120 ft).
    /// Rock throwing/catching. Fire immunity, cold vulnerability.
    /// </summary>
    private static void RegisterFireGiant()
    {
        Register(new NPCDefinition
        {
            Id = "fire_giant",
            Name = "Fire Giant",
            ChallengeRating = "10",
            Level = 15,
            CharacterClass = "Warrior",
            CreatureType = "Giant",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 15,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 31, DEX = 9, CON = 21, WIS = 14, INT = 10, CHA = 11,
            NaturalArmorBonus = 8,
            BaseSpeed = 6, // 30 ft (in armor)
            BaseHitDieHP = 142,
            BAB = 11,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            DamageImmunities = new List<DamageType> { DamageType.Fire },
            CreatureTags = new List<string> { "Giant", "Fire", "RockThrowing", "MM35" },
            Feats = new List<string> { "Cleave", "Great Cleave", "Improved Sunder", "Iron Will", "Power Attack", "Weapon Focus (greatsword)" },
            SpecialAbilities = new List<string> { "Rock Throwing (Ex): 120 ft., 2d6+10", "Rock Catching (Ex): Ref DC 25", "Immunity to fire", "Vulnerability to cold (+50% damage)", "Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("greatsword", EquipSlot.MainHand),
                new EquipmentSlotPair("half_plate", EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.85f, 0.45f, 0.2f, 1f),
            PanelColor = new Color(0.35f, 0.12f, 0.05f, 0.85f),
            NameColor = new Color(1f, 0.65f, 0.3f),
            Description = "Fire Giant (CR 10). Fire-immune giant with greatsword and rock throwing. MM 3.5e p.121."
        });
    }
}

}
