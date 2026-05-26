using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monster Manual base humanoid creatures (warriors, racial entries).
/// These are base creatures only — class levels/templates applied dynamically at spawn.
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_Humanoids()
    {
        RegisterDwarfWarrior();
        RegisterElfWarrior();
        RegisterHalflingWarrior();
        RegisterHobgoblin();
        RegisterDrowElf();
        RegisterDuergar();
        RegisterSvirfneblin();
        RegisterLizardfolk();
        RegisterGrimlock();
        RegisterDerro();
    }

    // ════════════════════════════════════════════════════════════
    //  Dwarf, 1st-Level Warrior — MM p.91
    //  Humanoid (Dwarf), Medium, CR 1/2
    //  1d8+2 HP (6), dwarven waraxe +1 melee (1d10+1)
    //  Str 13, Dex 11, Con 14, Int 10, Wis 9, Cha 6
    //  AC 16 (scale mail +4, heavy shield +2)
    // ════════════════════════════════════════════════════════════
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

    // ════════════════════════════════════════════════════════════
    //  Elf, 1st-Level Warrior — MM p.101
    //  Humanoid (Elf), Medium, CR 1/2
    //  1d8 HP (4), longsword +2 melee (1d8+1) or longbow +3 ranged (1d8)
    //  Str 13, Dex 15, Con 10, Int 10, Wis 9, Cha 8
    //  AC 15 (studded leather +3, light shield +1, Dex +1)
    // ════════════════════════════════════════════════════════════
    private static void RegisterElfWarrior()
    {
        Register(new NPCDefinition
        {
            Id = "elf_warrior",
            Name = "Elf Warrior",
            ChallengeRating = "1/2",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            CharacterAlignment = Alignment.ChaoticGood,
            HitDice = 1,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 13, DEX = 15, CON = 10, WIS = 9, INT = 10, CHA = 8,
            NaturalArmorBonus = 0,
            BaseSpeed = 6, // 30 ft
            BaseHitDieHP = 4,
            BAB = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Humanoid", "Elf", "MM35" },
            Feats = new List<string> { "Weapon Focus (longbow)" },
            SpecialAbilities = new List<string> { "Low-light vision", "Immune to sleep", "+2 saves vs. enchantment", "Elven weapon proficiency" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("longsword", EquipSlot.MainHand),
                new EquipmentSlotPair("longbow", EquipSlot.Ranged),
                new EquipmentSlotPair("studded_leather", EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Ranged,
            SpriteColor = new Color(0.7f, 0.85f, 0.65f, 1f),
            PanelColor = new Color(0.15f, 0.25f, 0.12f, 0.85f),
            NameColor = new Color(0.8f, 0.95f, 0.7f),
            Description = "Elf Warrior (CR 1/2). Nimble humanoid with longsword and longbow. MM 3.5e p.101."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Halfling, 1st-Level Warrior — MM p.149
    //  Humanoid (Halfling), Small, CR 1/2
    //  1d8+1 HP (5), longsword +1 melee (1d6) or light crossbow +4 ranged (1d6)
    //  Str 11, Dex 17, Con 12, Int 10, Wis 9, Cha 8
    //  AC 16 (chainmail +5, Dex +1 [max])
    // ════════════════════════════════════════════════════════════
    private static void RegisterHalflingWarrior()
    {
        Register(new NPCDefinition
        {
            Id = "halfling_warrior",
            Name = "Halfling Warrior",
            ChallengeRating = "1/2",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = true,
            STR = 11, DEX = 17, CON = 12, WIS = 9, INT = 10, CHA = 8,
            NaturalArmorBonus = 0,
            BaseSpeed = 4, // 20 ft
            BaseHitDieHP = 5,
            BAB = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Humanoid", "Halfling", "MM35" },
            Feats = new List<string> { "Weapon Focus (light crossbow)" },
            SpecialAbilities = new List<string> { "+2 morale saves vs. fear", "+1 attack with thrown/slings", "+1 size bonus to AC/attack" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("longsword", EquipSlot.MainHand),
                new EquipmentSlotPair("light_crossbow", EquipSlot.Ranged),
                new EquipmentSlotPair("chainmail", EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Ranged,
            SpriteColor = new Color(0.85f, 0.75f, 0.55f, 1f),
            PanelColor = new Color(0.3f, 0.25f, 0.15f, 0.85f),
            NameColor = new Color(0.95f, 0.85f, 0.65f),
            Description = "Halfling Warrior (CR 1/2). Small but brave humanoid fighter. MM 3.5e p.149."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Hobgoblin — MM p.153
    //  Humanoid (Goblinoid), Medium, CR 1/2
    //  1d8+2 HP (6), longsword +2 melee (1d8+1) or javelin +2 ranged (1d6+1)
    //  Str 13, Dex 13, Con 14, Int 10, Wis 9, Cha 8
    //  AC 15 (studded leather +3, light shield +1, Dex +1)
    // ════════════════════════════════════════════════════════════
    private static void RegisterHobgoblin()
    {
        Register(new NPCDefinition
        {
            Id = "hobgoblin",
            Name = "Hobgoblin",
            ChallengeRating = "1/2",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            CharacterAlignment = Alignment.LawfulEvil,
            HitDice = 1,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 13, DEX = 13, CON = 14, WIS = 9, INT = 10, CHA = 8,
            NaturalArmorBonus = 0,
            BaseSpeed = 6,
            BaseHitDieHP = 6,
            BAB = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Humanoid", "Goblinoid", "MM35" },
            Feats = new List<string> { "Alertness" },
            SpecialAbilities = new List<string> { "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("longsword", EquipSlot.MainHand),
                new EquipmentSlotPair("light_steel_shield", EquipSlot.OffHand),
                new EquipmentSlotPair("studded_leather", EquipSlot.Armor)
            },
            BackpackItemIds = new List<string> { "javelin" },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.7f, 0.45f, 0.3f, 1f),
            PanelColor = new Color(0.25f, 0.15f, 0.08f, 0.85f),
            NameColor = new Color(0.9f, 0.6f, 0.4f),
            Description = "Hobgoblin (CR 1/2). Disciplined goblinoid warrior. MM 3.5e p.153."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Drow Elf — MM p.103
    //  Humanoid (Elf), Medium, CR 1 (ECL +2)
    //  1d8 HP (4), rapier +2 melee (1d6+1) or hand crossbow +3 ranged (1d4 + poison)
    //  Str 13, Dex 15, Con 10, Int 14, Wis 9, Cha 12
    //  AC 16 (chain shirt +4, light shield +1, Dex +1)
    //  SR 12, Spell-like: dancing lights, darkness, faerie fire 1/day
    // ════════════════════════════════════════════════════════════
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

    // ════════════════════════════════════════════════════════════
    //  Duergar — MM p.93
    //  Humanoid (Dwarf), Medium, CR 1
    //  1d8+2 HP (6), warhammer +1 melee (1d8+1) or light crossbow +1 ranged (1d8)
    //  Str 13, Dex 11, Con 14, Int 10, Wis 9, Cha 4
    //  Spell-like: enlarge person, invisibility 1/day each
    // ════════════════════════════════════════════════════════════
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

    // ════════════════════════════════════════════════════════════
    //  Svirfneblin (Deep Gnome) — MM p.132
    //  Humanoid (Gnome), Small, CR 1
    //  1d8+1 HP (5), heavy pick +1 melee (1d4), light crossbow +4 ranged (1d6)
    //  Str 11, Dex 17, Con 12, Int 10, Wis 11, Cha 4
    //  SR 12, +2 dodge AC, nondetection
    // ════════════════════════════════════════════════════════════
    private static void RegisterSvirfneblin()
    {
        Register(new NPCDefinition
        {
            Id = "svirfneblin",
            Name = "Svirfneblin",
            ChallengeRating = "1",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = true,
            STR = 11, DEX = 17, CON = 12, WIS = 11, INT = 10, CHA = 4,
            NaturalArmorBonus = 0,
            SpellResistance = 12,
            BaseSpeed = 4,
            BaseHitDieHP = 5,
            BAB = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Humanoid", "Gnome", "Svirfneblin", "MM35" },
            Feats = new List<string> { "Toughness" },
            SpecialAbilities = new List<string> { "Darkvision 120 ft.", "SR 12", "Nondetection (constant)", "Stonecunning", "+2 saves vs. illusions", "Blindsight 60 ft. (via exceptional hearing)", "Blur (Sp) 1/day", "Disguise Self (Sp) 1/day" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("heavy_pick", EquipSlot.MainHand),
                new EquipmentSlotPair("light_crossbow", EquipSlot.Ranged),
                new EquipmentSlotPair("chain_shirt", EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.DefensiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.55f, 0.52f, 0.58f, 1f),
            PanelColor = new Color(0.18f, 0.16f, 0.22f, 0.85f),
            NameColor = new Color(0.75f, 0.7f, 0.85f),
            Description = "Svirfneblin (CR 1). Deep gnome with spell resistance and nondetection. MM 3.5e p.132."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Lizardfolk — MM p.169
    //  Humanoid (Reptilian), Medium, CR 1
    //  2d8+2 HP (11), 2 claws +2 melee (1d4+1), bite +0 melee (1d4)
    //  or club +2 melee (1d6+1), javelin +1 ranged (1d6+1)
    //  Str 13, Dex 10, Con 13, Int 9, Wis 10, Cha 10
    //  AC 15 (+5 natural), hold breath
    // ════════════════════════════════════════════════════════════
    private static void RegisterLizardfolk()
    {
        Register(new NPCDefinition
        {
            Id = "lizardfolk",
            Name = "Lizardfolk",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 2,
            BABOverride = BABProgression.Medium,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 13, DEX = 10, CON = 13, WIS = 10, INT = 9, CHA = 10,
            NaturalArmorBonus = 5,
            BaseSpeed = 6,
            BaseHitDieHP = 11,
            BAB = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            CreatureTags = new List<string> { "Humanoid", "Reptilian", "Aquatic", "MM35" },
            Feats = new List<string> { "Multiattack" },
            SpecialAbilities = new List<string> { "Hold Breath (4× Con rounds)", "Swim 30 ft." },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("club", EquipSlot.MainHand),
                new EquipmentSlotPair("heavy_wooden_shield", EquipSlot.OffHand)
            },
            BackpackItemIds = new List<string> { "javelin" },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.35f, 0.55f, 0.3f, 1f),
            PanelColor = new Color(0.12f, 0.2f, 0.08f, 0.85f),
            NameColor = new Color(0.6f, 0.85f, 0.5f),
            Description = "Lizardfolk (CR 1). Reptilian humanoid with claws and natural armor. MM 3.5e p.169."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Grimlock — MM p.140
    //  Monstrous Humanoid, Medium, CR 1
    //  2d8+2 HP (11), battleaxe +4 melee (1d8+4)
    //  Str 16, Dex 13, Con 13, Int 10, Wis 8, Cha 6
    //  AC 15 (+2 Dex, +3 natural), Blindsight 40 ft., immune to gaze/visual
    // ════════════════════════════════════════════════════════════
    private static void RegisterGrimlock()
    {
        Register(new NPCDefinition
        {
            Id = "grimlock",
            Name = "Grimlock",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Monstrous Humanoid",
            CharacterAlignment = Alignment.TrueNeutral,
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = true,
            STR = 16, DEX = 13, CON = 13, WIS = 8, INT = 10, CHA = 6,
            NaturalArmorBonus = 3,
            BaseSpeed = 6,
            BaseHitDieHP = 11,
            BAB = 2,
            HasScent = true,
            NaturalAttacks = new List<NaturalAttackDefinition>(),
            CreatureTags = new List<string> { "Monstrous Humanoid", "Blindsight40", "MM35" },
            Feats = new List<string> { "Alertness", "Track" },
            SpecialAbilities = new List<string> { "Blindsight 40 ft.", "Immune to gaze attacks/visual effects/illusions", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("battleaxe", EquipSlot.MainHand)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Berserk,
            SpriteColor = new Color(0.55f, 0.52f, 0.5f, 1f),
            PanelColor = new Color(0.2f, 0.18f, 0.16f, 0.85f),
            NameColor = new Color(0.8f, 0.75f, 0.7f),
            Description = "Grimlock (CR 1). Blind monstrous humanoid with blindsight 40 ft. MM 3.5e p.140."
        });
    }

    // ════════════════════════════════════════════════════════════
    //  Derro — MM p.49
    //  Monstrous Humanoid, Small, CR 3
    //  3d8+3 HP (16), short sword +5 melee (1d4+1) or repeating light crossbow +6 ranged (1d6 + poison)
    //  Str 13, Dex 16, Con 13, Int 10, Wis 5, Cha 16
    //  AC 17 (+1 size, +3 Dex, +3 natural), SR 15
    //  Spell-like: darkness, ghost sound (at will), daze, sound burst (1/day)
    // ════════════════════════════════════════════════════════════
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
}
