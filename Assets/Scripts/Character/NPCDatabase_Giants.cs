using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual base giant creatures.
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_Giants()
    {
        RegisterOgre();
        RegisterOgreMage();
        RegisterEttin();
        RegisterHillGiant();
        RegisterStoneGiant();
    }

    // ════════════════════════════════════════════════════════════
    //  Ogre — MM p.198
    //  Giant, Large, CR 3
    //  4d8+11 HP (29), greatclub +8 melee (2d8+7) or javelin +1 ranged (1d8+5)
    //  Str 21, Dex 8, Con 15, Int 6, Wis 10, Cha 7
    //  AC 16 (-1 size, -1 Dex, +5 natural, +3 hide armor)
    // ════════════════════════════════════════════════════════════
    private static void RegisterOgre()
    {
        Register(new NPCDefinition
        {
            Id = "ogre",
            Name = "Ogre",
            ChallengeRating = "3",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Giant",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 4,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 21, DEX = 8, CON = 15, WIS = 10, INT = 6, CHA = 7,
            NaturalArmorBonus = 5,
            BaseSpeed = 6, // 30 ft (40 ft base, -10 armor)
            BaseHitDieHP = 29,
            BAB = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Giant", "Darkvision60", "MM35" },
            Feats = new List<string> { "Toughness", "Weapon Focus (greatclub)" },
            SpecialAbilities = new List<string> { "Darkvision 60 ft.", "Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("greatclub", EquipSlot.MainHand),
                new EquipmentSlotPair("hide_armor", EquipSlot.Armor)
            },
            BackpackItemIds = new List<string> { "javelin" },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.6f, 0.5f, 0.35f, 1f),
            PanelColor = new Color(0.22f, 0.18f, 0.1f, 0.85f),
            NameColor = new Color(0.85f, 0.7f, 0.5f),
            Description = "Ogre (CR 3). Large brute with greatclub. MM 3.5e p.198."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Ogre Mage — MM p.200
    //  Giant, Large, CR 8
    //  5d8+15 HP (37), greatsword +7 melee (2d8+4)
    //  Str 19, Dex 12, Con 17, Int 14, Wis 14, Cha 17
    //  AC 18 (-1 size, +1 Dex, +5 natural, +3 chain shirt)
    //  SR 19, Fly 40 ft, Regeneration 5
    //  Spell-like: darkness, invisibility (at will), charm person, cone of cold,
    //    gaseous form, polymorph, sleep (1/day each)
    // ════════════════════════════════════════════════════════════
    private static void RegisterOgreMage()
    {
        Register(new NPCDefinition
        {
            Id = "ogre_mage",
            Name = "Ogre Mage",
            ChallengeRating = "8",
            Level = 5,
            CharacterClass = "Warrior",
            CreatureType = "Giant",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 5,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 19, DEX = 12, CON = 17, WIS = 14, INT = 14, CHA = 17,
            NaturalArmorBonus = 5,
            SpellResistance = 19,
            RegenerationAmount = 5,
            BaseSpeed = 6, // 30 ft (also fly 40 ft)
            BaseHitDieHP = 37,
            BAB = 5,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Giant", "Shapechanger", "Fly40", "Darkvision90", "MM35" },
            Feats = new List<string> { "Combat Expertise", "Improved Initiative" },
            SpecialAbilities = new List<string> { "Regeneration 5 (fire/acid)", "SR 19", "Flight (Su) 40 ft. (good)", "Darkvision 90 ft.", "Low-light vision", "Darkness (Sp) at will", "Invisibility (Sp) at will", "Charm Person (Sp) 1/day DC 14", "Cone of Cold (Sp) 1/day DC 18", "Gaseous Form (Sp) 1/day", "Polymorph (Sp) 1/day", "Sleep (Sp) 1/day DC 14" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("greatsword", EquipSlot.MainHand),
                new EquipmentSlotPair("chain_shirt", EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Spellcaster,
            SpriteColor = new Color(0.35f, 0.45f, 0.6f, 1f),
            PanelColor = new Color(0.12f, 0.18f, 0.25f, 0.85f),
            NameColor = new Color(0.55f, 0.7f, 0.9f),
            Description = "Ogre Mage (CR 8). Intelligent giant with spell-like abilities, flight, and regeneration. MM 3.5e p.200."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Ettin — MM p.106
    //  Giant, Large, CR 6
    //  10d8+20 HP (65), 2 morningstars +12/+7 and +12/+7 melee (2d6+6 / 2d6+3)
    //  or 2 javelins +5 ranged (1d8+6)
    //  Str 23, Dex 8, Con 15, Int 6, Wis 10, Cha 11
    //  AC 18 (-1 size, -1 Dex, +7 natural, +3 hide armor)
    //  Two heads — superior two-weapon fighting
    // ════════════════════════════════════════════════════════════
    private static void RegisterEttin()
    {
        Register(new NPCDefinition
        {
            Id = "ettin",
            Name = "Ettin",
            ChallengeRating = "6",
            Level = 10,
            CharacterClass = "Warrior",
            CreatureType = "Giant",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 10,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 23, DEX = 8, CON = 15, WIS = 10, INT = 6, CHA = 11,
            NaturalArmorBonus = 7,
            BaseSpeed = 8, // 40 ft
            BaseHitDieHP = 65,
            BAB = 7,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Giant", "Darkvision60", "MM35" },
            Feats = new List<string> { "Alertness", "Improved Initiative", "Iron Will", "Power Attack" },
            SpecialAbilities = new List<string> { "Two heads: cannot be flanked, +2 Spot/Listen/Search", "Superior Two-Weapon Fighting: no off-hand penalty", "Darkvision 60 ft.", "Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("morningstar", EquipSlot.MainHand),
                new EquipmentSlotPair("morningstar", EquipSlot.OffHand),
                new EquipmentSlotPair("hide_armor", EquipSlot.Armor)
            },
            BackpackItemIds = new List<string> { "javelin" },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.55f, 0.48f, 0.38f, 1f),
            PanelColor = new Color(0.2f, 0.16f, 0.1f, 0.85f),
            NameColor = new Color(0.8f, 0.7f, 0.55f),
            Description = "Ettin (CR 6). Two-headed giant with superior two-weapon fighting. MM 3.5e p.106."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Hill Giant — MM p.123
    //  Giant, Large, CR 7
    //  12d8+48 HP (102), greatclub +16/+11 melee (2d8+10) or rock +8 ranged (2d6+7)
    //  Str 25, Dex 8, Con 19, Int 6, Wis 10, Cha 7
    //  AC 20 (-1 size, -1 Dex, +9 natural, +3 hide armor)
    //  Rock throwing 120 ft., rock catching
    // ════════════════════════════════════════════════════════════
    private static void RegisterHillGiant()
    {
        Register(new NPCDefinition
        {
            Id = "hill_giant",
            Name = "Hill Giant",
            ChallengeRating = "7",
            Level = 12,
            CharacterClass = "Warrior",
            CreatureType = "Giant",
            CharacterAlignment = Alignment.ChaoticEvil,
            HitDice = 12,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 25, DEX = 8, CON = 19, WIS = 10, INT = 6, CHA = 7,
            NaturalArmorBonus = 9,
            BaseSpeed = 6, // 30 ft (40 ft base -10 armor)
            BaseHitDieHP = 102,
            BAB = 9,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Giant", "RockThrowing", "Darkvision60", "MM35" },
            Feats = new List<string> { "Cleave", "Improved Bull Rush", "Power Attack", "Improved Sunder", "Weapon Focus (greatclub)" },
            SpecialAbilities = new List<string> { "Rock Throwing (Ex): 120 ft., 2d6+7", "Rock Catching (Ex): Ref DC 20", "Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("greatclub", EquipSlot.MainHand),
                new EquipmentSlotPair("hide_armor", EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.55f, 0.45f, 0.32f, 1f),
            PanelColor = new Color(0.2f, 0.15f, 0.08f, 0.85f),
            NameColor = new Color(0.8f, 0.65f, 0.45f),
            Description = "Hill Giant (CR 7). Brutish giant with rock throwing. MM 3.5e p.123."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Stone Giant — MM p.124
    //  Giant, Large, CR 8
    //  14d8+56 HP (119), greatclub +17/+12 melee (2d8+12) or rock +9 ranged (2d8+12)
    //  Str 27, Dex 15, Con 19, Int 10, Wis 12, Cha 11
    //  AC 25 (-1 size, +2 Dex, +11 natural, +3 hide armor)
    //  Rock throwing 180 ft., rock catching
    // ════════════════════════════════════════════════════════════
    private static void RegisterStoneGiant()
    {
        Register(new NPCDefinition
        {
            Id = "stone_giant",
            Name = "Stone Giant",
            ChallengeRating = "8",
            Level = 14,
            CharacterClass = "Warrior",
            CreatureType = "Giant",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 14,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 27, DEX = 15, CON = 19, WIS = 12, INT = 10, CHA = 11,
            NaturalArmorBonus = 11,
            BaseSpeed = 6, // 30 ft (40 ft -10 armor)
            BaseHitDieHP = 119,
            BAB = 10,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Giant", "RockThrowing", "Darkvision60", "MM35" },
            Feats = new List<string> { "Combat Reflexes", "Iron Will", "Point Blank Shot", "Power Attack", "Precise Shot" },
            SpecialAbilities = new List<string> { "Rock Throwing (Ex): 180 ft., 2d8+12", "Rock Catching (Ex): Ref DC 22", "Darkvision 60 ft.", "Low-light vision" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("greatclub", EquipSlot.MainHand),
                new EquipmentSlotPair("hide_armor", EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Brute,
            SpriteColor = new Color(0.6f, 0.58f, 0.55f, 1f),
            PanelColor = new Color(0.22f, 0.2f, 0.18f, 0.85f),
            NameColor = new Color(0.82f, 0.78f, 0.72f),
            Description = "Stone Giant (CR 8). Skilled rock thrower with impressive natural armor. MM 3.5e p.124."
        });
    }
}
