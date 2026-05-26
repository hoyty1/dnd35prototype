using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual ooze, plant, and construct creatures.
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_OozesPlants()
    {
        RegisterGrayOoze();
        RegisterBlackPudding();
        RegisterVioletFungus();
        RegisterShrieker();
        RegisterPhantomFungus();
        RegisterFleshGolem();
    }

    // ════════════════════════════════════════════════════════════
    //  Gray Ooze — MM p.202
    //  Ooze, Medium, CR 4
    //  3d10+15 HP (31), slam +3 melee (1d6+1 + 1d6 acid)
    //  Str 12, Dex 1, Con 21, Int 0, Wis 1, Cha 1
    //  AC 5 (-5 Dex), immune to fire/cold
    //  Acid: dissolves metal and organic, not stone
    //  Transparent: DC 15 Spot or surprise
    // ════════════════════════════════════════════════════════════
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
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.UndeadMindless,
            SpriteColor = new Color(0.5f, 0.5f, 0.48f, 0.7f),
            PanelColor = new Color(0.18f, 0.18f, 0.16f, 0.85f),
            NameColor = new Color(0.75f, 0.75f, 0.7f),
            Description = "Gray Ooze (CR 4). Transparent acidic ooze that dissolves metal. MM 3.5e p.202."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Black Pudding — MM p.201
    //  Ooze, Huge, CR 7
    //  10d10+60 HP (115), slam +7 melee (2d6+4 + 2d6 acid)
    //  Str 17, Dex 1, Con 22, Int 0, Wis 1, Cha 1
    //  AC 3 (-2 size, -5 Dex), Constrict, Improved Grab
    //  Acid dissolves everything. Split by slashing/piercing.
    // ════════════════════════════════════════════════════════════
    private static void RegisterBlackPudding()
    {
        Register(new NPCDefinition
        {
            Id = "black_pudding",
            Name = "Black Pudding",
            ChallengeRating = "7",
            Level = 10,
            CharacterClass = "Warrior",
            CreatureType = "Ooze",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 10,
            SizeCategory = SizeCategory.Huge,
            IsTallCreature = false,
            STR = 17, DEX = 1, CON = 22, WIS = 1, INT = 0, CHA = 1,
            NaturalArmorBonus = 0,
            IsMindless = true,
            BaseSpeed = 4, // 20 ft, climb 20 ft
            BaseHitDieHP = 115,
            BAB = 7,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Slam",
            Immunities = new CreatureImmunities
            {
                immuneToMindAffecting = true,
                immuneToCriticalHits = true,
                immuneToSneakAttack = true
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 6, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 3, IsPrimary = true, BonusElementalDamageDice = 6, BonusElementalDamageCount = 2, BonusElementalDamageType = DamageType.Acid }
            },
            CreatureTags = new List<string> { "Ooze", "Blindsight60", "MM35" },
            Feats = new List<string>(),
            SpecialAbilities = new List<string> { "Acid (Ex): dissolves organic and metal, 21 dmg to wood/metal per round of contact", "Improved Grab", "Constrict 2d6+4 + 2d6 acid", "Split (Ex): slashing/piercing splits into two smaller puddings", "Blindsight 60 ft.", "Ooze traits (mindless, immune to crits/sneak/mind-affecting)", "Climb 20 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.UndeadMindless,
            SpriteColor = new Color(0.1f, 0.1f, 0.12f, 0.9f),
            PanelColor = new Color(0.03f, 0.03f, 0.05f, 0.85f),
            NameColor = new Color(0.25f, 0.25f, 0.32f),
            Description = "Black Pudding (CR 7). Huge acidic ooze that splits when cut. MM 3.5e p.201."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Violet Fungus — MM p.112
    //  Plant, Medium, CR 3
    //  2d8+6 HP (15), 4 tentacles +3 melee (1d6+2 + rot)
    //  Str 14, Dex 8, Con 16, Int 0, Wis 11, Cha 9
    //  AC 13 (-1 Dex, +4 natural)
    //  Poison: DC 14 Fort, 1d4 Str/1d4 Con
    // ════════════════════════════════════════════════════════════
    private static void RegisterVioletFungus()
    {
        Register(new NPCDefinition
        {
            Id = "violet_fungus",
            Name = "Violet Fungus",
            ChallengeRating = "3",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Plant",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 14, DEX = 8, CON = 16, WIS = 11, INT = 0, CHA = 9,
            NaturalArmorBonus = 4,
            IsMindless = true,
            BaseSpeed = 2, // 10 ft
            BaseHitDieHP = 15,
            BAB = 1,
            Immunities = new CreatureImmunities
            {
                immuneToMindAffecting = true,
                immuneToPoison = true,
                immuneToCriticalHits = true,
                immuneToSneakAttack = true
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Tentacle", DamageDice = 6, DamageCount = 1, Count = 4, BonusDamageSource = DamageBonusSource.Strength, Range = 2, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Plant", "MM35" },
            Feats = new List<string>(),
            SpecialAbilities = new List<string> { "Poison (Ex): tentacle, DC 14 Fort, 1d4 Str + 1d4 Con / 1d4 Str + 1d4 Con", "Plant traits (immune to mind-affecting, poison, sleep, paralysis, polymorph, stun, crits)", "Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.UndeadMindless,
            SpriteColor = new Color(0.5f, 0.25f, 0.55f, 1f),
            PanelColor = new Color(0.2f, 0.08f, 0.22f, 0.85f),
            NameColor = new Color(0.75f, 0.4f, 0.82f),
            Description = "Violet Fungus (CR 3). Poisonous plant with long tentacles. MM 3.5e p.112."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Shrieker — MM p.112
    //  Plant, Medium, CR 1
    //  2d8+2 HP (11), no attacks
    //  Str 1, Dex 1, Con 13, Int 0, Wis 2, Cha 1
    //  AC 8 (-5 Dex, +3 natural)
    //  Shriek: 1d3 rounds when movement/light within 10 ft., alerts nearby
    // ════════════════════════════════════════════════════════════
    private static void RegisterShrieker()
    {
        Register(new NPCDefinition
        {
            Id = "shrieker",
            Name = "Shrieker",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Plant",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 1, DEX = 1, CON = 13, WIS = 2, INT = 0, CHA = 1,
            NaturalArmorBonus = 3,
            IsMindless = true,
            BaseSpeed = 0, // Immobile
            BaseHitDieHP = 11,
            BAB = 1,
            CanMakeAttacksOfOpportunity = false,
            Immunities = new CreatureImmunities
            {
                immuneToMindAffecting = true,
                immuneToPoison = true,
                immuneToCriticalHits = true,
                immuneToSneakAttack = true
            },
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Plant", "Immobile", "MM35" },
            Feats = new List<string>(),
            SpecialAbilities = new List<string> { "Shriek (Ex): piercing sound for 1d3 rounds when movement within 10 ft., draws creatures", "Immobile: cannot move or attack", "Plant traits" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.DefensiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.UndeadMindless,
            SpriteColor = new Color(0.55f, 0.45f, 0.3f, 1f),
            PanelColor = new Color(0.2f, 0.15f, 0.08f, 0.85f),
            NameColor = new Color(0.8f, 0.68f, 0.48f),
            Description = "Shrieker (CR 1). Fungus that shrieks to alert nearby creatures. MM 3.5e p.112."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Phantom Fungus — MM p.207
    //  Plant, Medium, CR 3
    //  2d8+6 HP (15), bite +3 melee (1d6+3)
    //  Str 14, Dex 10, Con 16, Int 2, Wis 14, Cha 9
    //  AC 14 (+4 natural), Naturally Invisible
    // ════════════════════════════════════════════════════════════
    private static void RegisterPhantomFungus()
    {
        Register(new NPCDefinition
        {
            Id = "phantom_fungus",
            Name = "Phantom Fungus",
            ChallengeRating = "3",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Plant",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 14, DEX = 10, CON = 16, WIS = 14, INT = 2, CHA = 9,
            NaturalArmorBonus = 4,
            BaseSpeed = 4, // 20 ft, climb 20 ft
            BaseHitDieHP = 15,
            BAB = 1,
            Immunities = new CreatureImmunities
            {
                immuneToMindAffecting = true,
                immuneToPoison = true,
                immuneToCriticalHits = true,
                immuneToSneakAttack = true
            },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            CreatureTags = new List<string> { "Plant", "NaturalInvisibility", "Tremorsense30", "MM35" },
            Feats = new List<string> { "Improved Initiative" },
            SpecialAbilities = new List<string> { "Greater Invisibility (Su): naturally invisible at all times, visible only when dead", "Plant traits", "Tremorsense 30 ft.", "Climb 20 ft.", "Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.5f, 0.5f, 0.5f, 0.3f),
            PanelColor = new Color(0.18f, 0.18f, 0.2f, 0.85f),
            NameColor = new Color(0.7f, 0.7f, 0.75f),
            Description = "Phantom Fungus (CR 3). Naturally invisible hunting fungus. MM 3.5e p.207."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Flesh Golem — MM p.135
    //  Construct, Large, CR 7
    //  9d10+30 HP (79), 2 slams +13 melee (2d8+5)
    //  Str 21, Dex 9, Con 0, Int 0, Wis 11, Cha 1
    //  AC 18 (-1 size, -1 Dex, +10 natural), DR 5/adamantine
    //  Berserk, Magic Immunity, Immunity to magic (healed by fire, slowed by cold/electricity)
    // ════════════════════════════════════════════════════════════
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
}
