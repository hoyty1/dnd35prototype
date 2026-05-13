using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Custom and scenario-specific NPC creature registrations (non-Monster Manual).
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterGoblinWarchief()
    {
        Register(new NPCDefinition
        {
            Id = "goblin_warchief",
            Name = "Goblin Warchief",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            HitDice = 2,
            FortitudeSaveOverride = SaveProgression.Good,
            ReflexSaveOverride = SaveProgression.Poor,
            WillSaveOverride = SaveProgression.Poor,
            SizeCategory = SizeCategory.Small,
            STR = 14, DEX = 15, CON = 13, WIS = 10, INT = 10, CHA = 8,
            BAB = 1,
            BaseSpeed = 3,    // 15 ft (Small creature)
            BaseHitDieHP = 12,
            CreatureTags = new List<string> { "Goblinoid" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.STUDDED_LEATHER, EquipSlot.Armor),
                new EquipmentSlotPair(ItemIDs.MORNINGSTAR, EquipSlot.RightHand),
                new EquipmentSlotPair(ItemIDs.SHIELD_LIGHT_WOODEN, EquipSlot.LeftHand)
            },
            BackpackItemIds = new List<string> { ItemIDs.JAVELIN },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.6f, 0.8f, 0.3f, 1f),  // greenish
            PanelColor = new Color(0.4f, 0.1f, 0.1f, 0.85f), // dark red
            NameColor = new Color(1f, 0.4f, 0.4f),
            Description = "A cunning goblin leader who rallies lesser goblins. Fights with a morningstar and shield."
        });
    }

    /// <summary>
    /// Goblin Feint Drill (CR 1/3-ish) — stripped-down goblin target used for
    /// Feint + Sneak Attack validation scenarios.
    /// </summary>
    private static void RegisterGoblinFeintDrill()
    {
        Register(new NPCDefinition
        {
            Id = "goblin_feint_drill",
            Name = "Goblin Warrior",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            HitDice = 1,
            FortitudeSaveOverride = SaveProgression.Good,
            ReflexSaveOverride = SaveProgression.Poor,
            WillSaveOverride = SaveProgression.Poor,
            SizeCategory = SizeCategory.Small,
            STR = 11, DEX = 13, CON = 12, WIS = 9, INT = 10, CHA = 6,
            BAB = 0,
            BaseSpeed = 3,
            BaseHitDieHP = 6,
            CreatureTags = new List<string> { "Goblinoid" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.LEATHER_ARMOR, EquipSlot.Armor),
                new EquipmentSlotPair(ItemIDs.MORNINGSTAR, EquipSlot.RightHand),
                new EquipmentSlotPair(ItemIDs.SHIELD_LIGHT_WOODEN, EquipSlot.LeftHand)
            },
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.58f, 0.8f, 0.3f, 1f),
            PanelColor = new Color(0.36f, 0.1f, 0.1f, 0.85f),
            NameColor = new Color(1f, 0.45f, 0.45f),
            Description = "A basic goblin melee trainee used to validate feint-driven sneak attacks."
        });
    }

    /// <summary>
    /// Mirror Image Test Goblin (CR 1/3-ish) — lightweight ranged shooter used in
    /// the dedicated Mirror Image test arena. Keeps pressure on the wizard from
    /// distance with shortbow attacks while dying quickly for rapid iteration.
    /// </summary>
    private static void RegisterMirrorImageTestGoblin()
    {
        Register(new NPCDefinition
        {
            Id = "mirror_image_test_goblin",
            Name = "Mirror Test Goblin Archer",
            ChallengeRating = "1/3",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            HitDice = 1,
            FortitudeSaveOverride = SaveProgression.Good,
            ReflexSaveOverride = SaveProgression.Poor,
            WillSaveOverride = SaveProgression.Poor,
            SizeCategory = SizeCategory.Small,
            STR = 11,
            DEX = 15,
            CON = 12,
            WIS = 9,
            INT = 10,
            CHA = 6,
            BAB = 0,
            BaseSpeed = 3,
            BaseHitDieHP = 6,
            CreatureTags = new List<string> { "Goblinoid", "MirrorImageTest" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.LEATHER_ARMOR, EquipSlot.Armor),
                new EquipmentSlotPair(ItemIDs.SHORTBOW, EquipSlot.RightHand)
            },
            BackpackItemIds = new List<string> { ItemIDs.DAGGER },
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Ranged,
            SpriteColor = new Color(0.58f, 0.8f, 0.3f, 1f),
            PanelColor = new Color(0.36f, 0.1f, 0.1f, 0.85f),
            NameColor = new Color(1f, 0.45f, 0.45f),
            Description = "Shortbow goblin used in the Mirror Image Test Arena for clone-targeting and dissipation validation."
        });
    }

    /// <summary>
    /// Skeleton Archer (CR 1) — Undead ranged attacker.
    /// An animated skeleton wielding a shortbow from distance. Keeps away from
    /// melee, preferring to pepper targets with arrows. Falls back to a short
    /// sword if cornered. Low HP but hard to reach.
    /// 
    /// D&D 3.5 Skeleton stats: immune to cold, half damage from slashing/piercing,
    /// +2 natural armor. Simplified here for the prototype.
    /// </summary>
    private static void RegisterSkeletonArcher()
    {
        Register(new NPCDefinition
        {
            Id = "skeleton_archer",
            Name = "Skeleton Archer",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Undead",
            HitDice = 1,
            SizeCategory = SizeCategory.Medium,
            NaturalArmorBonus = 2,
            // Skeleton: mindless undead with no Intelligence score in D&D 3.5e.
            STR = 13, DEX = 15, CON = 10, WIS = 10, INT = CharacterStats.NO_SCORE, CHA = 1,
            BAB = 0,
            BaseSpeed = 6,    // 30 ft (Medium undead)
            BaseHitDieHP = 8, // 1 HD undead, fragile
            CreatureTags = new List<string> { "Undead" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.LEATHER_ARMOR, EquipSlot.Armor),
                new EquipmentSlotPair(ItemIDs.SHORTBOW, EquipSlot.RightHand)
            },
            BackpackItemIds = new List<string> { ItemIDs.SHORT_SWORD },
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Bludgeoning,
            DamageImmunities = new List<DamageType> { DamageType.Cold },
            IsMindless = true,
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.UndeadMindless,
            SpriteColor = new Color(0.85f, 0.85f, 0.75f, 1f),  // bone white
            PanelColor = new Color(0.2f, 0.2f, 0.3f, 0.85f),   // dark grey-blue
            NameColor = new Color(0.7f, 0.85f, 1f),             // pale blue
            Description = "An animated skeleton with hollow eye sockets that glow faintly. Fires arrows with eerie precision."
        });
    }

    /// <summary>
    /// Skeleton Warrior (CR 2-ish) — armored undead melee template used to validate
    /// explicit BAB overrides.
    /// </summary>
    private static void RegisterSkeletonWarrior()
    {
        Register(new NPCDefinition
        {
            Id = "skeleton_warrior",
            Name = "Skeleton Warrior",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Undead",
            HitDice = 4,
            SizeCategory = SizeCategory.Medium,
            NaturalArmorBonus = 2,
            BaseAttackBonusOverride = 4,
            STR = 15, DEX = 13, CON = 10, WIS = 10, INT = CharacterStats.NO_SCORE, CHA = 1,
            BAB = 4,
            BaseSpeed = 6,
            BaseHitDieHP = 24,
            CreatureTags = new List<string> { "Undead" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.BANDED_MAIL, EquipSlot.Armor),
                new EquipmentSlotPair(ItemIDs.LONGSWORD, EquipSlot.RightHand),
                new EquipmentSlotPair(ItemIDs.SHIELD_HEAVY_STEEL, EquipSlot.LeftHand)
            },
            BackpackItemIds = new List<string>(),
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Bludgeoning,
            DamageImmunities = new List<DamageType> { DamageType.Cold },
            IsMindless = true,
            AIBehavior = NPCAIBehavior.DefensiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.UndeadMindless,
            SpriteColor = new Color(0.82f, 0.82f, 0.74f, 1f),
            PanelColor = new Color(0.22f, 0.22f, 0.28f, 0.85f),
            NameColor = new Color(0.75f, 0.85f, 1f),
            Description = "An armored skeletal champion animated with unnatural precision. Uses a manually overridden BAB profile for encounter tuning."
        });
    }

    /// <summary>
    /// Wight Dreadwalker (CR 3-ish) — stronger undead used to validate
    /// Turn Undead's "turned and fleeing" outcome when not outright destroyed.
    /// </summary>
    private static void RegisterWightDreadwalker()
    {
        Register(new NPCDefinition
        {
            Id = "wight_dreadwalker",
            Name = "Wight Dreadwalker",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Undead",
            HitDice = 4,
            SizeCategory = SizeCategory.Medium,
            NaturalArmorBonus = 4,
            STR = 12, DEX = 12, CON = 10, WIS = 13, INT = 11, CHA = 15,
            BAB = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 6,
            BaseHitDieHP = 26,
            CreatureTags = new List<string> { "Undead" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Silver,
            DamageImmunities = new List<DamageType> { DamageType.Cold },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.78f, 0.82f, 0.9f, 1f),
            PanelColor = new Color(0.19f, 0.18f, 0.3f, 0.85f),
            NameColor = new Color(0.72f, 0.9f, 1f),
            Description = "A malevolent wight animated by hunger and shadow. Tough enough to be turned but not instantly destroyed in cleric test encounters."
        });
    }

    /// <summary>
    /// Zombie Shambler (CR 1) — basic undead melee body used in template tests.
    /// </summary>
    private static void RegisterZombieShambler()
    {
        Register(new NPCDefinition
        {
            Id = "zombie_shambler",
            Name = "Zombie",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Undead",
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            NaturalArmorBonus = 3,
            STR = 17, DEX = 6, CON = 10, WIS = 10, INT = CharacterStats.NO_SCORE, CHA = 1,
            BAB = 1,
            BaseSpeed = 4,
            BaseHitDieHP = 22,
            CreatureTags = new List<string> { "Undead" },
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Slashing,
            IsMindless = true,
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.UndeadMindless,
            SpriteColor = new Color(0.62f, 0.75f, 0.62f, 1f),
            PanelColor = new Color(0.17f, 0.28f, 0.17f, 0.85f),
            NameColor = new Color(0.72f, 0.9f, 0.72f),
            Description = "A shambling corpse that batters enemies with heavy slams. Slow, resilient, and mindless."
        });
    }

    /// <summary>
    /// Orc Berserker (CR 2) — Aggressive melee brute.
    /// A ferocious orc warrior who charges headlong into battle swinging a
    /// greataxe. High STR, decent HP, but lower AC due to reckless fighting
    /// style. Represents a serious melee threat to level 3 characters.
    /// 
    /// D&D 3.5 Orc: STR +4, INT -2, WIS -2, CHA -2. Darkvision 60ft.
    /// Light sensitivity (not implemented in this prototype).
    /// </summary>
    private static void RegisterOrcBerserker()
    {
        Register(new NPCDefinition
        {
            Id = "orc_berserker",
            Name = "Orc Berserker",
            Level = 3,
            CharacterClass = "Barbarian",
            CreatureType = "Humanoid",
            HitDice = 3,
            FortitudeSaveOverride = SaveProgression.Good,
            ReflexSaveOverride = SaveProgression.Poor,
            WillSaveOverride = SaveProgression.Poor,
            SizeCategory = SizeCategory.Medium,
            // Orc: base STR 17 + racial = effective 17 (already includes orc bonus)
            STR = 17, DEX = 11, CON = 14, WIS = 8, INT = 8, CHA = 6,
            BAB = 2,
            BaseSpeed = 6,    // 30 ft base (orc speed), +10 ft barbarian fast movement
            BaseHitDieHP = 28, // 3d12 + CON, tough brute
            CreatureTags = new List<string> { "Orc" },
            Feats = new List<string> { "Power Attack", "Cleave" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.HIDE_ARMOR, EquipSlot.Armor),
                new EquipmentSlotPair(ItemIDs.GREATAXE, EquipSlot.RightHand)
            },
            BackpackItemIds = new List<string> { ItemIDs.JAVELIN, ItemIDs.JAVELIN },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Berserk,
            SpriteColor = new Color(0.5f, 0.6f, 0.4f, 1f),     // olive-green skin
            PanelColor = new Color(0.35f, 0.15f, 0.05f, 0.85f), // dark brown
            NameColor = new Color(1f, 0.6f, 0.3f),              // orange
            Description = "A hulking orc driven by bloodlust. Charges into melee with a massive greataxe, caring nothing for defense."
        });
    }

    /// <summary>
    /// Orc Grapple Drill (CR ~2) — focused test target for grapple mechanics.
    /// Medium orc with solid STR/BAB and a light weapon (dagger) for
    /// "Use Opponent's Weapon" grapple action testing.
    /// </summary>
    private static void RegisterOrcGrappleDrill()
    {
        Register(new NPCDefinition
        {
            Id = "orc_grapple_drill",
            Name = "Orc Grapple Drill",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            HitDice = 3,
            FortitudeSaveOverride = SaveProgression.Good,
            ReflexSaveOverride = SaveProgression.Poor,
            WillSaveOverride = SaveProgression.Poor,
            SizeCategory = SizeCategory.Medium,
            STR = 16, DEX = 11, CON = 14, WIS = 10, INT = 8, CHA = 8,
            BAB = 2,
            BaseSpeed = 6,
            BaseHitDieHP = 20,
            CreatureTags = new List<string> { "Orc" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.LEATHER_ARMOR, EquipSlot.Armor),
                new EquipmentSlotPair(ItemIDs.DAGGER, EquipSlot.RightHand)
            },
            BackpackItemIds = new List<string> { ItemIDs.DAGGER },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Grappler,
            SpriteColor = new Color(0.55f, 0.68f, 0.45f, 1f),
            PanelColor = new Color(0.32f, 0.14f, 0.08f, 0.85f),
            NameColor = new Color(1f, 0.72f, 0.42f),
            Description = "An orc sparring target tuned for grappling tests. Carries a dagger to validate weapon-in-grapple interactions."
        });
    }


    /// <summary>
    /// Dedicated grapple-heavy enemies for grease scenario validation.
    /// All are configured with weak Reflex progression so Grease saves are easy to observe.
    /// </summary>
    private static void RegisterGreaseTestGrapplers()
    {
        Register(new NPCDefinition
        {
            Id = "grease_test_grappler1",
            Name = "Brutus the Grappler",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            HitDice = 3,
            FortitudeSaveOverride = SaveProgression.Good,
            ReflexSaveOverride = SaveProgression.Poor,
            WillSaveOverride = SaveProgression.Poor,
            SizeCategory = SizeCategory.Medium,
            STR = 16, DEX = 8, CON = 14, WIS = 10, INT = 8, CHA = 8,
            BAB = 2,
            BaseSpeed = 6,
            BaseHitDieHP = 28,
            Feats = new List<string> { "Improved Grapple" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.STUDDED_LEATHER, EquipSlot.Armor),
                new EquipmentSlotPair(ItemIDs.MORNINGSTAR, EquipSlot.RightHand)
            },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Grappler,
            AITargetPriority = "Slippery Sam",
            SpriteColor = new Color(0.64f, 0.54f, 0.42f, 1f),
            PanelColor = new Color(0.35f, 0.14f, 0.08f, 0.85f),
            NameColor = new Color(1f, 0.72f, 0.44f),
            Description = "Brutish grappler with very low Reflex save for Grease area/object save validation."
        });

        Register(new NPCDefinition
        {
            Id = "grease_test_grappler2",
            Name = "Thug Crusher",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            HitDice = 2,
            FortitudeSaveOverride = SaveProgression.Good,
            ReflexSaveOverride = SaveProgression.Poor,
            WillSaveOverride = SaveProgression.Poor,
            SizeCategory = SizeCategory.Medium,
            STR = 15, DEX = 9, CON = 13, WIS = 10, INT = 8, CHA = 8,
            BAB = 1,
            BaseSpeed = 6,
            BaseHitDieHP = 18,
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.LEATHER_ARMOR, EquipSlot.Armor),
                new EquipmentSlotPair(ItemIDs.CLUB, EquipSlot.RightHand)
            },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Grappler,
            AITargetPriority = "Slippery Sam",
            SpriteColor = new Color(0.60f, 0.5f, 0.38f, 1f),
            PanelColor = new Color(0.33f, 0.13f, 0.08f, 0.85f),
            NameColor = new Color(0.98f, 0.68f, 0.42f),
            Description = "Low-Reflex thug wielding a club to repeatedly pressure grapple defenses."
        });

        Register(new NPCDefinition
        {
            Id = "grease_test_grappler3",
            Name = "Brawler Grog",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            HitDice = 2,
            FortitudeSaveOverride = SaveProgression.Good,
            ReflexSaveOverride = SaveProgression.Poor,
            WillSaveOverride = SaveProgression.Poor,
            SizeCategory = SizeCategory.Medium,
            STR = 14, DEX = 10, CON = 12, WIS = 10, INT = 8, CHA = 8,
            BAB = 1,
            BaseSpeed = 6,
            BaseHitDieHP = 16,
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.LEATHER_ARMOR, EquipSlot.Armor),
                new EquipmentSlotPair(ItemIDs.QUARTERSTAFF, EquipSlot.RightHand)
            },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Grappler,
            AITargetPriority = "Slippery Sam",
            SpriteColor = new Color(0.62f, 0.52f, 0.4f, 1f),
            PanelColor = new Color(0.34f, 0.13f, 0.08f, 0.85f),
            NameColor = new Color(0.99f, 0.7f, 0.43f),
            Description = "Quarterstaff brawler with baseline Reflex save for additional Grease save sampling."
        });

        Register(new NPCDefinition
        {
            Id = "grease_test_grappler4",
            Name = "Weak Grappler",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            HitDice = 1,
            FortitudeSaveOverride = SaveProgression.Good,
            ReflexSaveOverride = SaveProgression.Poor,
            WillSaveOverride = SaveProgression.Poor,
            SizeCategory = SizeCategory.Medium,
            STR = 13, DEX = 8, CON = 12, WIS = 10, INT = 8, CHA = 8,
            BAB = 1,
            BaseSpeed = 6,
            BaseHitDieHP = 12,
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.LEATHER_ARMOR, EquipSlot.Armor),
                new EquipmentSlotPair(ItemIDs.DAGGER, EquipSlot.RightHand)
            },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Grappler,
            AITargetPriority = "Slippery Sam",
            SpriteColor = new Color(0.58f, 0.48f, 0.37f, 1f),
            PanelColor = new Color(0.31f, 0.12f, 0.07f, 0.85f),
            NameColor = new Color(0.96f, 0.66f, 0.41f),
            Description = "Fragile backup grappler used to verify repeated Grease + grapple defense interactions."
        });
    }

    /// <summary>
    /// Hobgoblin Sergeant (CR 3) — Tactical armored melee fighter.
    /// A disciplined hobgoblin warrior in chainmail with longsword and heavy
    /// shield. High AC makes it a tough nut to crack. More dangerous than
    /// goblins — organized, methodical, and well-equipped.
    /// 
    /// D&D 3.5 Hobgoblin: +2 DEX, +2 CON. Darkvision 60ft.
    /// Goblinoid subtype (triggers Dwarf racial bonus).
    /// </summary>
    private static void RegisterHobgoblinSergeant()
    {
        Register(new NPCDefinition
        {
            Id = "hobgoblin_sergeant",
            Name = "Hobgoblin Sergeant",
            Level = 3,
            CharacterClass = "Fighter",
            CreatureType = "Humanoid",
            HitDice = 3,
            FortitudeSaveOverride = SaveProgression.Good,
            ReflexSaveOverride = SaveProgression.Poor,
            WillSaveOverride = SaveProgression.Poor,
            SizeCategory = SizeCategory.Medium,
            // Hobgoblin: DEX +2, CON +2 already factored in
            STR = 15, DEX = 14, CON = 14, WIS = 12, INT = 10, CHA = 10,
            BAB = 2,
            BaseSpeed = 4,    // 20 ft (heavy armor reduces from 30 ft)
            BaseHitDieHP = 25, // 3d10 + CON, solid HP
            CreatureTags = new List<string> { "Goblinoid" },
            Feats = new List<string> { "Weapon Focus", "Combat Expertise" },
            WeaponFocusChoice = "Longsword",
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.CHAINMAIL, EquipSlot.Armor),
                new EquipmentSlotPair(ItemIDs.LONGSWORD, EquipSlot.RightHand),
                new EquipmentSlotPair(ItemIDs.SHIELD_HEAVY_STEEL, EquipSlot.LeftHand)
            },
            BackpackItemIds = new List<string> { ItemIDs.JAVELIN, ItemIDs.JAVELIN, ItemIDs.POTION_HEALING },
            AIBehavior = NPCAIBehavior.DefensiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.8f, 0.5f, 0.3f, 1f),     // orange-brown
            PanelColor = new Color(0.15f, 0.15f, 0.3f, 0.85f),  // dark blue-grey
            NameColor = new Color(1f, 0.8f, 0.5f),              // golden
            Description = "A disciplined hobgoblin officer in gleaming chainmail. Commands lesser troops and fights with precision swordplay."
        });
    }

    /// <summary>
    /// Ogre Brute (CR 3) — Large giant with high STR and natural armor.
    /// Uses a massive club and has extended natural reach from size.
    /// </summary>
    private static void RegisterOgreBrute()
    {
        Register(new NPCDefinition
        {
            Id = "ogre_brute",
            Name = "Ogre Brute",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Giant",
            HitDice = 4,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 21, DEX = 8, CON = 15, WIS = 10, INT = 6, CHA = 7,
            BAB = 3,
            NaturalArmorBonus = 5,
            BaseSpeed = 8,     // 40 ft
            BaseHitDieHP = 38,
            CreatureTags = new List<string> { "Giant" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.GREATCLUB, EquipSlot.RightHand),
                new EquipmentSlotPair(ItemIDs.HIDE_ARMOR, EquipSlot.Armor)
            },
            BackpackItemIds = new List<string> { ItemIDs.JAVELIN, ItemIDs.JAVELIN },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Berserk,
            SpriteColor = new Color(0.65f, 0.55f, 0.45f, 1f),
            PanelColor = new Color(0.25f, 0.12f, 0.08f, 0.85f),
            NameColor = new Color(1f, 0.78f, 0.52f),
            Description = "A hulking ogre that smashes foes with brutal overhead swings. Its long reach threatens nearby squares."
        });
    }

    /// <summary>
    /// Dire Wolf (CR 3) — Large long quadruped with trip attack.
    /// Reach is intentionally short for size because wolves are long creatures.
    /// </summary>
    private static void RegisterDireWolf()
    {
        Register(new NPCDefinition
        {
            Id = "dire_wolf",
            Name = "Dire Wolf",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 6,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 25, DEX = 15, CON = 17, WIS = 12, INT = 2, CHA = 10,
            BAB = 4,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            }, // long Large creature => 5-ft reach
            BaseSpeed = 10,   // 50 ft
            BaseHitDieHP = 45,
            CreatureTags = new List<string> { "Animal" },
            HasScent = true,
            HasTripAttack = true,
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.62f, 0.62f, 0.62f, 1f),
            PanelColor = new Color(0.18f, 0.18f, 0.18f, 0.85f),
            NameColor = new Color(0.95f, 0.95f, 1f),
            Description = "A massive wolf with crushing jaws and pack-hunting instincts. It can drag prey down with vicious trip attacks."
        });
    }

    /// <summary>
    /// Tiger (CR 4) — 2 primary claws and a secondary bite.
    /// </summary>
    private static void RegisterTiger()
    {
        Register(new NPCDefinition
        {
            Id = "tiger",
            Name = "Tiger",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 6,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 23, DEX = 15, CON = 17, WIS = 12, INT = 2, CHA = 6,
            BAB = 4,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 8, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 10,
            BaseHitDieHP = 45,
            CreatureTags = new List<string> { "Animal" },
            HasScent = true,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Bite",
            HasPounce = true,
            HasRake = true,
            RakeAttack = new NaturalAttackDefinition { Name = "Rake", DamageDice = 8, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = true },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.88f, 0.66f, 0.32f, 1f),
            PanelColor = new Color(0.35f, 0.2f, 0.08f, 0.85f),
            NameColor = new Color(1f, 0.82f, 0.5f),
            Description = "A massive striped predator with raking claws and a crushing bite. Uses proper primary/secondary natural attack sequencing."
        });
    }

    /// <summary>
    /// Dire Tiger baseline definition.
    /// Scenario-specific allegiance/control should be applied at spawn time in GameManager,
    /// so the same NPC definition can be reused as either hostile or allied.
    /// </summary>
    private static void RegisterDireTiger()
    {
        Register(new NPCDefinition
        {
            Id = "dire_tiger",
            Name = "Dire Tiger",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 6,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 23, DEX = 15, CON = 17, WIS = 12, INT = 2, CHA = 6,
            BAB = 4,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 8, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 10,
            BaseHitDieHP = 45,
            CreatureTags = new List<string> { "Animal" },
            HasScent = true,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Bite",
            HasPounce = true,
            HasRake = true,
            RakeAttack = new NaturalAttackDefinition { Name = "Rake", DamageDice = 8, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = true },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.9f, 0.72f, 0.38f, 1f),
            PanelColor = new Color(0.2f, 0.3f, 0.14f, 0.85f),
            NameColor = new Color(0.86f, 1f, 0.78f),
            Description = "A massive prehistoric tiger with powerful claws, crushing bite, and deadly pounce/grab/rake follow-up."
        });
    }

    /// <summary>
    /// Brown Bear (CR 4) — 2 primary claws and a secondary bite.
    /// </summary>
    private static void RegisterBrownBear()
    {
        Register(new NPCDefinition
        {
            Id = "brown_bear",
            Name = "Brown Bear",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 6,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 27, DEX = 13, CON = 19, WIS = 12, INT = 2, CHA = 6,
            BAB = 4,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 8, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 8,
            BaseHitDieHP = 57,
            CreatureTags = new List<string> { "Animal" },
            HasTripAttack = false,
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.5f, 0.38f, 0.28f, 1f),
            PanelColor = new Color(0.25f, 0.17f, 0.12f, 0.85f),
            NameColor = new Color(0.95f, 0.8f, 0.62f),
            Description = "A towering bear that mauls prey with two claws before following up with a secondary bite."
        });
    }

    /// <summary>
    /// Dire Bear (CR 7) — high-HD bear used for template scaling tests.
    /// </summary>
    private static void RegisterDireBear()
    {
        Register(new NPCDefinition
        {
            Id = "dire_bear",
            Name = "Dire Bear",
            Level = 12,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 12,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 31, DEX = 13, CON = 19, WIS = 12, INT = 2, CHA = 10,
            BAB = 9,
            NaturalArmorBonus = 7,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 8, DamageCount = 2, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 8,
            BaseHitDieHP = 114,
            CreatureTags = new List<string> { "Animal" },
            HasScent = true,
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Claw",
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.44f, 0.3f, 0.22f, 1f),
            PanelColor = new Color(0.2f, 0.14f, 0.1f, 0.85f),
            NameColor = new Color(0.95f, 0.82f, 0.7f),
            Description = "A hulking dire bear that tears foes apart with heavy claws before biting. Used as a high-HD celestial template benchmark."
        });
    }

    /// <summary>
    /// Wolf (CR 1) — D&D 3.5e Monster Manual baseline wolf.
    /// Uses bite attacks and free trip attempts on hit.
    /// </summary>
    private static void RegisterWolfPackHunter()
    {
        Register(new NPCDefinition
        {
            Id = "wolf_pack_hunter",
            Name = "Wolf",
            Level = 2,          // 2 HD animal
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 13, DEX = 15, CON = 15, WIS = 12, INT = 2, CHA = 6,
            BAB = 1,
            NaturalArmorBonus = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },  // Str bonus to bite damage
            BaseSpeed = 10,   // 50 ft
            BaseHitDieHP = 13,
            CreatureTags = new List<string> { "Animal" },
            HasScent = true,
            HasTripAttack = true,
            TripAttackCheckBonus = 1,
            Feats = new List<string> { "Weapon Focus", "Track" },
            WeaponFocusChoice = "Bite",
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.72f, 0.72f, 0.72f, 1f),
            PanelColor = new Color(0.2f, 0.2f, 0.2f, 0.85f),
            NameColor = new Color(0.9f, 0.9f, 0.95f),
            Description = "A swift pack hunter from the Monster Manual baseline. It bites to pull enemies prone with free trip attempts."
        });
    }

    private static void RegisterSummonMonsterBaseCreatures()
    {
        RegisterSummonOctopus();
        RegisterSummonSmallViper();
        RegisterSummonMonstrousCentipedeMedium();
        RegisterSummonMonstrousScorpionSmall();
        RegisterSummonMonstrousSpiderSmall();
        RegisterSummonDireBat();
        RegisterSummonSmallAirElemental();
        RegisterSummonSmallFireElemental();
        RegisterSummonCrocodile();
        RegisterSummonBlackBear();
        RegisterSummonApe();
        RegisterSummonDireBadger();
        RegisterSummonLargeShark();
        RegisterSummonConstrictorSnake();

        // Summon Monster III new creatures
        RegisterSummonBison();
        RegisterSummonBoar();
        RegisterSummonDireWeasel();
        RegisterSummonWolverine();
        RegisterSummonLargeViper();
        RegisterSummonHugeMonstruousCentipede();
        RegisterSummonSmallEarthElemental();
        RegisterSummonSmallWaterElemental();
        RegisterSummonHippogriff();
        RegisterSummonHellHound();
        RegisterSummonDretch();

        // Keep compatibility aliases for commonly referenced summon list names.
        RegisterSummonCreatureAliases();
    }

    private static void RegisterSummonCreatureAliases()
    {
        RegisterSummonAlias("wolf", "wolf_pack_hunter", "Wolf");
        RegisterSummonAlias("badger", "dire_badger", "Badger");

        // These IDs are used by external validation scripts; map to closest existing summon baselines.
        RegisterSummonAlias("riding_dog", "dog", "Riding Dog");
        RegisterSummonAlias("owl", "eagle", "Owl");
        RegisterSummonAlias("raven", "eagle", "Raven");
        RegisterSummonAlias("giant_bee", "dire_bat", "Giant Bee");
    }

    private static void RegisterSummonAlias(string aliasId, string sourceId, string overrideName = null)
    {
        NPCDefinition source = Get(sourceId);
        if (source == null)
            return;

        NPCDefinition alias = source.Clone();
        alias.Id = aliasId;

        if (!string.IsNullOrWhiteSpace(overrideName))
            alias.Name = overrideName;

        if (alias.CreatureTags == null)
            alias.CreatureTags = new List<string>();
        if (!alias.CreatureTags.Contains("SummonAlias"))
            alias.CreatureTags.Add("SummonAlias");

        Register(alias);
    }

    private static void RegisterSummonOctopus()
    {
        Register(new NPCDefinition
        {
            Id = "octopus",
            Name = "Octopus",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 2,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 12, DEX = 15, CON = 11, WIS = 12, INT = 2, CHA = 3,
            NaturalArmorBonus = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Tentacles", DamageDice = 3, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 5,
            BaseHitDieHP = 11,
            CreatureTags = new List<string> { "Animal", "Aquatic", "SummonBase" },
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Tentacles",
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.62f, 0.48f, 0.68f, 1f),
            PanelColor = new Color(0.18f, 0.13f, 0.23f, 0.85f),
            NameColor = new Color(0.88f, 0.8f, 0.95f),
            Description = "Summon Monster baseline octopus with improved-grab style control attack."
        });
    }

    private static void RegisterSummonSmallViper()
    {
        Register(new NPCDefinition
        {
            Id = "small_viper",
            Name = "Small Viper",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 6, DEX = 17, CON = 11, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 3, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 6,
            BaseHitDieHP = 6,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.45f, 0.72f, 0.34f, 1f),
            PanelColor = new Color(0.15f, 0.23f, 0.12f, 0.85f),
            NameColor = new Color(0.84f, 0.94f, 0.8f),
            Description = "Summon Monster baseline Small Viper."
        });
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

    private static void RegisterSummonSmallAirElemental()
    {
        Register(new NPCDefinition
        {
            Id = "small_air_elemental",
            Name = "Small Air Elemental",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Elemental",
            HitDice = 2,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 10, DEX = 17, CON = 10, WIS = 11, INT = 4, CHA = 11,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 20, // Fly 100 ft. (perfect)
            BaseHitDieHP = 9,
            Immunities = new CreatureImmunities
            {
                immuneToPoison = true,
                immuneToCriticalHits = true,
                immuneToSneakAttack = true
            },
            CreatureTags = new List<string> { "Elemental", "Air", "Extraplanar", "SummonBase" },
            SpecialAbilities = new List<string> { "Elemental traits", "Air mastery", "Whirlwind (DC 11)", "Fly 100 ft. (perfect)", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.68f, 0.86f, 1f, 1f),
            PanelColor = new Color(0.14f, 0.19f, 0.26f, 0.85f),
            NameColor = new Color(0.85f, 0.95f, 1f),
            Description = "Small Air Elemental. Immune to poison, sleep, paralysis, stunning. Not subject to critical hits or flanking. MM 3.5e p.95."
        });
    }

    private static void RegisterSummonSmallFireElemental()
    {
        Register(new NPCDefinition
        {
            Id = "small_fire_elemental",
            Name = "Small Fire Elemental",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Elemental",
            HitDice = 2,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 10, DEX = 13, CON = 10, WIS = 11, INT = 4, CHA = 11,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                // Slam 1d4 plus 1d4 fire (fire damage represented as special ability)
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 10, // 50 ft.
            BaseHitDieHP = 9,
            Immunities = new CreatureImmunities
            {
                immuneToPoison = true,
                immuneToFire = true,
                immuneToCriticalHits = true,
                immuneToSneakAttack = true
            },
            DamageImmunities = new List<DamageType> { DamageType.Fire },
            CreatureTags = new List<string> { "Elemental", "Fire", "Extraplanar", "SummonBase" },
            SpecialAbilities = new List<string> { "Elemental traits", "Burn (DC 11, 1d4 fire)", "Immunity to fire", "Vulnerability to cold (+50%)", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(1f, 0.55f, 0.22f, 1f),
            PanelColor = new Color(0.28f, 0.1f, 0.08f, 0.85f),
            NameColor = new Color(1f, 0.86f, 0.72f),
            Description = "Small Fire Elemental. Slam deals +1d4 fire (Burn). Immune to fire, vulnerable to cold. MM 3.5e p.98."
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

    private static void RegisterSummonBlackBear()
    {
        Register(new NPCDefinition
        {
            Id = "black_bear",
            Name = "Black Bear",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 19, DEX = 13, CON = 15, WIS = 12, INT = 2, CHA = 6,
            NaturalArmorBonus = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 8, // 40 ft.
            BaseHitDieHP = 19,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Low-light vision", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.28f, 0.2f, 0.13f, 1f),
            PanelColor = new Color(0.14f, 0.09f, 0.06f, 0.85f),
            NameColor = new Color(0.9f, 0.78f, 0.62f),
            Description = "Black bear. 2 claws +6 (1d4+4), bite +1 (1d6+2). +4 racial Swim. MM 3.5e p.269."
        });
    }

    private static void RegisterSummonApe()
    {
        Register(new NPCDefinition
        {
            Id = "ape",
            Name = "Ape",
            ChallengeRating = "2",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 4,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 21, DEX = 15, CON = 14, WIS = 12, INT = 2, CHA = 7,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 6, // 30 ft., climb 30 ft.
            BaseHitDieHP = 29,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Low-light vision", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.45f, 0.32f, 0.25f, 1f),
            PanelColor = new Color(0.18f, 0.12f, 0.09f, 0.85f),
            NameColor = new Color(0.95f, 0.84f, 0.75f),
            Description = "Ape. 2 claws +7 (1d6+5), bite +2 (1d6+2). +8 racial Climb. 10 ft. reach. MM 3.5e p.268."
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

    private static void RegisterSummonLargeShark()
    {
        Register(new NPCDefinition
        {
            Id = "large_shark",
            Name = "Large Shark",
            ChallengeRating = "2",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 4,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 17, DEX = 15, CON = 13, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 4,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8,
            BaseHitDieHP = 26,
            CreatureTags = new List<string> { "Animal", "Aquatic", "SummonBase" },
            HasScent = true,
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.62f, 0.7f, 0.78f, 1f),
            PanelColor = new Color(0.14f, 0.18f, 0.22f, 0.85f),
            NameColor = new Color(0.88f, 0.94f, 0.99f),
            Description = "Summon Monster baseline large shark."
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

    // ========================================
    // SUMMON MONSTER III — New Creatures
    // ========================================

    private static void RegisterSummonBison()
    {
        Register(new NPCDefinition
        {
            Id = "bison",
            Name = "Bison",
            ChallengeRating = "2",
            Level = 5,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 5,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 22, DEX = 10, CON = 16, WIS = 11, INT = 2, CHA = 4,
            NaturalArmorBonus = 4,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Gore", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8, // 40 ft.
            BaseHitDieHP = 37,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Stampede (1d12 per 5 bison, Ref DC 18 half)", "Low-light vision", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.55f, 0.42f, 0.28f, 1f),
            PanelColor = new Color(0.22f, 0.16f, 0.1f, 0.85f),
            NameColor = new Color(0.92f, 0.82f, 0.68f),
            Description = "Bison. Gore +8 (1d8+9). Stampede ability. MM 3.5e p.270."
        });
    }

    private static void RegisterSummonBoar()
    {
        Register(new NPCDefinition
        {
            Id = "boar",
            Name = "Boar",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 15, DEX = 10, CON = 17, WIS = 13, INT = 2, CHA = 4,
            NaturalArmorBonus = 6,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Gore", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8, // 40 ft.
            BaseHitDieHP = 25,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Ferocity (fights below 0 hp until −10)", "Low-light vision", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.52f, 0.38f, 0.3f, 1f),
            PanelColor = new Color(0.2f, 0.14f, 0.1f, 0.85f),
            NameColor = new Color(0.9f, 0.8f, 0.72f),
            Description = "Boar with ferocity — continues fighting below 0 HP until −10. Gore +4 (1d8+3). MM 3.5e p.270."
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

    private static void RegisterSummonWolverine()
    {
        Register(new NPCDefinition
        {
            Id = "wolverine",
            Name = "Wolverine",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 14, DEX = 15, CON = 19, WIS = 12, INT = 2, CHA = 10,
            NaturalArmorBonus = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 6, // 30 ft., burrow 10 ft., climb 10 ft.
            BaseHitDieHP = 28,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Rage (+4 Str, +4 Con, −2 AC when damaged)", "Low-light vision", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.48f, 0.38f, 0.28f, 1f),
            PanelColor = new Color(0.2f, 0.15f, 0.1f, 0.85f),
            NameColor = new Color(0.9f, 0.82f, 0.7f),
            Description = "Wolverine with rage when wounded (+4 Str/Con, −2 AC). +8 racial Climb. MM 3.5e p.283."
        });
    }

    private static void RegisterSummonLargeViper()
    {
        Register(new NPCDefinition
        {
            Id = "large_viper",
            Name = "Large Viper",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 3,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 10, DEX = 17, CON = 11, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                // Bite +4 melee (1d4 plus poison) — uses Weapon Finesse (Dex to attack)
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true, PoisonOnHitId = "large_viper_poison" }
            },
            BaseSpeed = 4, // 20 ft., climb 20 ft., swim 20 ft.
            BaseHitDieHP = 13,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Poison (Fort DC 11, 1d6 Con/1d6 Con)", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.45f, 0.55f, 0.3f, 1f),
            PanelColor = new Color(0.18f, 0.22f, 0.1f, 0.85f),
            NameColor = new Color(0.88f, 0.94f, 0.72f),
            Description = "Large viper snake. Bite +4 (1d4 + poison Fort DC 11, 1d6 Con/1d6 Con). Weapon Finesse. MM 3.5e p.280."
        });
    }

    private static void RegisterSummonHugeMonstruousCentipede()
    {
        Register(new NPCDefinition
        {
            Id = "huge_monstrous_centipede",
            Name = "Huge Monstrous Centipede",
            ChallengeRating = "2",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Vermin",
            HitDice = 6,
            SizeCategory = SizeCategory.Huge,
            IsTallCreature = false,
            STR = 17, DEX = 15, CON = 12, WIS = 10, INT = CharacterStats.NO_SCORE, CHA = 2,
            NaturalArmorBonus = 6,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 2, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true, PoisonOnHitId = "huge_centipede_poison" }
            },
            BaseSpeed = 8, // 40 ft., climb 40 ft.
            BaseHitDieHP = 33,
            IsMindless = true,
            CreatureTags = new List<string> { "Vermin", "SummonBase" },
            Immunities = new CreatureImmunities { immuneToMindAffecting = true },
            SpecialAbilities = new List<string> { "Poison (Fort DC 14, 1d6 Dex/1d6 Dex)", "Darkvision 60 ft.", "Vermin traits (mindless)" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.45f, 0.28f, 0.22f, 1f),
            PanelColor = new Color(0.2f, 0.1f, 0.08f, 0.85f),
            NameColor = new Color(0.9f, 0.72f, 0.65f),
            Description = "Huge monstrous centipede. Bite +5 (2d6+4 + poison Fort DC 14, 1d6 Dex). 15 ft. space, 10 ft. reach. MM 3.5e p.286."
        });
    }

    private static void RegisterSummonSmallEarthElemental()
    {
        Register(new NPCDefinition
        {
            Id = "small_earth_elemental",
            Name = "Small Earth Elemental",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Elemental",
            HitDice = 2,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 17, DEX = 8, CON = 13, WIS = 11, INT = 4, CHA = 11,
            NaturalArmorBonus = 7,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 4, // 20 ft.
            BaseHitDieHP = 11,
            Immunities = new CreatureImmunities
            {
                immuneToPoison = true,
                immuneToCriticalHits = true,
                immuneToSneakAttack = true
            },
            CreatureTags = new List<string> { "Elemental", "Earth", "Extraplanar", "SummonBase" },
            SpecialAbilities = new List<string> { "Elemental traits", "Earth mastery (+1 atk/dmg when grounded)", "Push (bull rush, no AoO)", "Earth glide", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.58f, 0.48f, 0.32f, 1f),
            PanelColor = new Color(0.24f, 0.18f, 0.1f, 0.85f),
            NameColor = new Color(0.92f, 0.84f, 0.68f),
            Description = "Small Earth Elemental. Slam +5 (1d6+4). Earth mastery, push, earth glide. MM 3.5e p.97."
        });
    }

    private static void RegisterSummonSmallWaterElemental()
    {
        Register(new NPCDefinition
        {
            Id = "small_water_elemental",
            Name = "Small Water Elemental",
            ChallengeRating = "1",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Elemental",
            HitDice = 2,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 14, DEX = 10, CON = 13, WIS = 11, INT = 4, CHA = 11,
            NaturalArmorBonus = 6,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 4, // 20 ft., swim 90 ft.
            BaseHitDieHP = 11,
            Immunities = new CreatureImmunities
            {
                immuneToPoison = true,
                immuneToCriticalHits = true,
                immuneToSneakAttack = true
            },
            CreatureTags = new List<string> { "Elemental", "Water", "Extraplanar", "SummonBase" },
            SpecialAbilities = new List<string> { "Elemental traits", "Water mastery (+1 atk/dmg in water)", "Drench (extinguish fires)", "Vortex (DC 13, 1d4 dmg)", "Swim 90 ft.", "Darkvision 60 ft." },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.32f, 0.55f, 0.78f, 1f),
            PanelColor = new Color(0.1f, 0.2f, 0.32f, 0.85f),
            NameColor = new Color(0.72f, 0.88f, 1f),
            Description = "Small Water Elemental. Slam +4 (1d6+3). Water mastery, drench, vortex. MM 3.5e p.100."
        });
    }

    private static void RegisterSummonHippogriff()
    {
        Register(new NPCDefinition
        {
            Id = "hippogriff",
            Name = "Hippogriff",
            ChallengeRating = "2",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Magical Beast",
            HitDice = 3,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 18, DEX = 15, CON = 16, WIS = 13, INT = 2, CHA = 8,
            NaturalArmorBonus = 4,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 10, // 50 ft., fly 100 ft. (average)
            BaseHitDieHP = 25,
            CreatureTags = new List<string> { "Magical Beast", "SummonBase" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Darkvision 60 ft.", "Low-light vision", "Scent", "Fly 100 ft. (average)" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.72f, 0.62f, 0.48f, 1f),
            PanelColor = new Color(0.26f, 0.2f, 0.14f, 0.85f),
            NameColor = new Color(0.94f, 0.88f, 0.78f),
            Description = "Hippogriff. 2 claws +6 (1d4+4), bite +1 (1d8+2). Fly 100 ft. (average). +4 Spot in daylight. MM 3.5e p.152."
        });
    }

    private static void RegisterSummonHellHound()
    {
        Register(new NPCDefinition
        {
            Id = "hell_hound",
            Name = "Hell Hound",
            ChallengeRating = "3",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Outsider",
            HitDice = 4,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 13, DEX = 13, CON = 13, WIS = 10, INT = 6, CHA = 6,
            NaturalArmorBonus = 5,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                // Bite 1d8+1 plus 1d6 fire (fire damage is special ability)
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8, // 40 ft.
            BaseHitDieHP = 22,
            Immunities = new CreatureImmunities
            {
                immuneToFire = true
            },
            DamageImmunities = new List<DamageType> { DamageType.Fire },
            CreatureTags = new List<string> { "Outsider", "Evil", "Extraplanar", "Fire", "Lawful", "SummonBase" },
            HasScent = true,
            SpecialAbilities = new List<string> { "Breath weapon (10-ft. cone, 2d6 fire, Ref DC 13 half, 1/2d4 rds)", "Fiery bite (+1d6 fire)", "Immunity to fire", "Vulnerability to cold (+50%)", "Darkvision 60 ft.", "Scent" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.82f, 0.28f, 0.15f, 1f),
            PanelColor = new Color(0.32f, 0.08f, 0.05f, 0.85f),
            NameColor = new Color(1f, 0.65f, 0.45f),
            Description = "Hell hound. Bite +5 (1d8+1 + 1d6 fire). Breath weapon 2d6 fire cone. Immune to fire, vulnerable to cold. MM 3.5e p.151."
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
    }

    // Monster Manual registrations moved to alphabetical partial files (NPCDatabase_B/C/D/F/H/M/O/R/S/V.cs).

    private static void RegisterFiendishWolf()
    {
        NPCDefinition baseWolf = Get("wolf_pack_hunter");
        if (baseWolf == null)
            return;

        NPCDefinition fiendishWolf = baseWolf.Clone();
        fiendishWolf.Id = "fiendish_wolf";
        fiendishWolf.Name = "Fiendish Wolf";
        fiendishWolf.AppliedTemplateIds = new List<string> { "fiendish" };
        fiendishWolf.Description = "A lower-planar wolf infused with fiendish power. Benchmark for 1-3 HD fiendish template scaling.";
        fiendishWolf.SpriteColor = new Color(0.62f, 0.22f, 0.24f, 1f);
        fiendishWolf.PanelColor = new Color(0.24f, 0.08f, 0.1f, 0.9f);
        fiendishWolf.NameColor = new Color(0.98f, 0.74f, 0.74f);
        Register(fiendishWolf);
    }

    private static void RegisterFiendishDireBear()
    {
        NPCDefinition baseBear = Get("dire_bear");
        if (baseBear == null)
            return;

        NPCDefinition fiendishBear = baseBear.Clone();
        fiendishBear.Id = "fiendish_dire_bear";
        fiendishBear.Name = "Fiendish Dire Bear";
        fiendishBear.AppliedTemplateIds = new List<string> { "fiendish" };
        fiendishBear.Description = "A towering dire bear warped by infernal essence. Benchmark for 12+ HD fiendish template scaling.";
        fiendishBear.SpriteColor = new Color(0.55f, 0.2f, 0.16f, 1f);
        fiendishBear.PanelColor = new Color(0.21f, 0.07f, 0.05f, 0.9f);
        fiendishBear.NameColor = new Color(1f, 0.76f, 0.7f);
        Register(fiendishBear);
    }

    private static void RegisterHumanPaladin()
    {
        Register(new NPCDefinition
        {
            Id = "human_paladin",
            Name = "Human Paladin",
            Level = 5,
            CharacterClass = "Paladin",
            CreatureType = "Humanoid",
            HitDice = 5,
            SizeCategory = SizeCategory.Medium,
            STR = 16, DEX = 10, CON = 14, WIS = 12, INT = 10, CHA = 14,
            BAB = 5,
            BaseSpeed = 6,
            BaseHitDieHP = 42,
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.LONGSWORD, EquipSlot.RightHand),
                new EquipmentSlotPair(ItemIDs.SHIELD_HEAVY_STEEL, EquipSlot.LeftHand),
                new EquipmentSlotPair(ItemIDs.CHAINMAIL, EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            CreatureTags = new List<string> { "Humanoid", "Human", "Good" },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.84f, 0.88f, 0.98f, 1f),
            PanelColor = new Color(0.2f, 0.2f, 0.35f, 0.85f),
            NameColor = new Color(0.95f, 0.95f, 1f),
            Description = "A righteous knight in chainmail who serves as a good-aligned smite target for fiendish template verification."
        });
    }

    private static void RegisterHumanCleric()
    {
        Register(new NPCDefinition
        {
            Id = "human_cleric",
            Name = "Human Cleric",
            Level = 5,
            CharacterClass = "Cleric",
            CreatureType = "Humanoid",
            HitDice = 5,
            SizeCategory = SizeCategory.Medium,
            STR = 12, DEX = 10, CON = 14, WIS = 17, INT = 10, CHA = 12,
            BAB = 3,
            BaseSpeed = 6,
            BaseHitDieHP = 36,
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.MACE_HEAVY, EquipSlot.RightHand),
                new EquipmentSlotPair(ItemIDs.SHIELD_HEAVY_STEEL, EquipSlot.LeftHand),
                new EquipmentSlotPair(ItemIDs.CHAINMAIL, EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            CreatureTags = new List<string> { "Humanoid", "Human", "Good" },
            AIBehavior = NPCAIBehavior.DefensiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.82f, 0.86f, 0.95f, 1f),
            PanelColor = new Color(0.22f, 0.24f, 0.34f, 0.85f),
            NameColor = new Color(0.93f, 0.96f, 1f),
            Description = "A devoted battle-priest used as a good-aligned divine caster target for fiendish smite testing."
        });
    }

    private static void RegisterArcaneMissileAdept()
    {
        Register(new NPCDefinition
        {
            Id = "arcane_missile_adept",
            Name = "Arcane Missile Adept",
            Level = 5,
            CharacterClass = "Wizard",
            CreatureType = "Humanoid",
            HitDice = 5,
            SizeCategory = SizeCategory.Medium,
            STR = 8,
            DEX = 14,
            CON = 12,
            WIS = 10,
            INT = 18,
            CHA = 10,
            BAB = 2,
            BaseSpeed = 6,
            BaseHitDieHP = 30,
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.QUARTERSTAFF, EquipSlot.RightHand)
            },
            BackpackItemIds = new List<string>(),
            KnownSpellIds = new List<string> { SpellNames.MAGIC_MISSILE },
            PreparedSpellSlotIds = new List<string> { SpellNames.MAGIC_MISSILE, SpellNames.MAGIC_MISSILE, SpellNames.MAGIC_MISSILE, SpellNames.MAGIC_MISSILE, SpellNames.MAGIC_MISSILE, SpellNames.MAGIC_MISSILE, SpellNames.MAGIC_MISSILE },
            CreatureTags = new List<string> { "Humanoid", "Arcane", "AI:MagicMissileOnly" },
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Evoker,
            SpriteColor = new Color(0.62f, 0.66f, 0.92f, 1f),
            PanelColor = new Color(0.16f, 0.14f, 0.32f, 0.85f),
            NameColor = new Color(0.9f, 0.9f, 1f),
            Description = "Enemy wizard test NPC configured to only cast Magic Missile; Shield mitigation happens during resolution."
        });
    }

    private static void RegisterProtectionFromEvilTestCasters()
    {
        Register(new NPCDefinition
        {
            Id = "evil_enchanter_test",
            Name = "Vhalzor the Corrupter",
            Level = 10,
            CharacterClass = "Wizard",
            CreatureType = "Humanoid",
            HitDice = 10,
            SizeCategory = SizeCategory.Medium,
            STR = 8,
            DEX = 14,
            CON = 12,
            WIS = 12,
            INT = 30,
            CHA = 16,
            BAB = 5,
            BaseSpeed = 6,
            BaseHitDieHP = 58,
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.QUARTERSTAFF, EquipSlot.RightHand)
            },
            BackpackItemIds = new List<string>(),
            KnownSpellIds = new List<string> { SpellNames.CHARM_PERSON },
            PreparedSpellSlotIds = new List<string>
            {
                SpellNames.CHARM_PERSON, SpellNames.CHARM_PERSON, SpellNames.CHARM_PERSON, SpellNames.CHARM_PERSON, SpellNames.CHARM_PERSON, SpellNames.CHARM_PERSON
            },
            CreatureTags = new List<string> { "Humanoid", "Arcane", "Evil", "ProtectionFromEvilTest" },
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Spellcaster,
            SpriteColor = new Color(0.74f, 0.42f, 0.78f, 1f),
            PanelColor = new Color(0.22f, 0.1f, 0.26f, 0.88f),
            NameColor = new Color(0.96f, 0.82f, 1f),
            Description = "Dedicated Protection from Evil benchmark caster: repeatedly casts Charm Person with extremely high save DC pressure."
        });
    }

    private static void RegisterProtectionFromEvilTestMelee()
    {
        Register(new NPCDefinition
        {
            Id = "evil_goblin_test",
            Name = "Goblin Ravager",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            HitDice = 4,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = true,
            STR = 14,
            DEX = 14,
            CON = 12,
            WIS = 8,
            INT = 8,
            CHA = 8,
            BAB = 4,
            BaseSpeed = 6,
            BaseHitDieHP = 32,
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.MORNINGSTAR, EquipSlot.RightHand),
                new EquipmentSlotPair(ItemIDs.LEATHER_ARMOR, EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            CreatureTags = new List<string> { "Humanoid", "Goblin", "Evil", "ProtectionFromEvilTest" },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Berserk,
            SpriteColor = new Color(0.52f, 0.76f, 0.52f, 1f),
            PanelColor = new Color(0.18f, 0.28f, 0.16f, 0.88f),
            NameColor = new Color(0.9f, 1f, 0.9f),
            Description = "Melee evil humanoid used to verify Protection from Evil's +2 deflection AC against standard weapon attacks."
        });
    }

    private static void RegisterProtectionFromEvilTestControls()
    {
        Register(new NPCDefinition
        {
            Id = "neutral_bandit_test",
            Name = "Neutral Bandit",
            Level = 3,
            CharacterClass = "Rogue",
            CreatureType = "Humanoid",
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            STR = 12,
            DEX = 16,
            CON = 12,
            WIS = 11,
            INT = 10,
            CHA = 10,
            BAB = 2,
            BaseSpeed = 6,
            BaseHitDieHP = 18,
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.SHORT_SWORD, EquipSlot.RightHand),
                new EquipmentSlotPair(ItemIDs.LEATHER_ARMOR, EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            CreatureTags = new List<string> { "Humanoid", "Human", "ProtectionFromEvilTest", "ControlTest" },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.76f, 0.74f, 0.62f, 1f),
            PanelColor = new Color(0.2f, 0.2f, 0.2f, 0.88f),
            NameColor = new Color(0.96f, 0.94f, 0.84f),
            Description = "Neutral melee control for Protection from Evil AC validation (should NOT trigger +2 deflection bonus)."
        });

        Register(new NPCDefinition
        {
            Id = "neutral_mage_test",
            Name = "Neutral Mage",
            Level = 3,
            CharacterClass = "Wizard",
            CreatureType = "Humanoid",
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            STR = 10,
            DEX = 13,
            CON = 12,
            WIS = 14,
            INT = 16,
            CHA = 10,
            BAB = 1,
            BaseSpeed = 6,
            BaseHitDieHP = 15,
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            KnownSpellIds = new List<string> { SpellNames.DAZE },
            PreparedSpellSlotIds = new List<string> { SpellNames.DAZE, SpellNames.DAZE, SpellNames.DAZE, SpellNames.DAZE },
            CreatureTags = new List<string> { "Humanoid", "Arcane", "ProtectionFromEvilTest", "ControlTest" },
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Spellcaster,
            SpriteColor = new Color(0.62f, 0.72f, 0.92f, 1f),
            PanelColor = new Color(0.14f, 0.18f, 0.34f, 0.88f),
            NameColor = new Color(0.88f, 0.94f, 1f),
            Description = "Neutral caster control for Protection from Evil save validation (Daze should NOT gain +2 save bonus)."
        });

        Register(new NPCDefinition
        {
            Id = "evil_acolyte_test",
            Name = "Evil Acolyte",
            Level = 3,
            CharacterClass = "Cleric",
            CreatureType = "Humanoid",
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            STR = 10,
            DEX = 11,
            CON = 12,
            WIS = 16,
            INT = 12,
            CHA = 13,
            BAB = 2,
            BaseSpeed = 6,
            BaseHitDieHP = 20,
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.CHAIN_SHIRT, EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            KnownSpellIds = new List<string> { SpellNames.DAZE },
            PreparedSpellSlotIds = new List<string> { SpellNames.DAZE, SpellNames.DAZE, SpellNames.DAZE, SpellNames.DAZE },
            CreatureTags = new List<string> { "Humanoid", "Divine", "Evil", "ProtectionFromEvilTest", "ControlTest" },
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Spellcaster,
            SpriteColor = new Color(0.86f, 0.52f, 0.52f, 1f),
            PanelColor = new Color(0.34f, 0.14f, 0.14f, 0.88f),
            NameColor = new Color(1f, 0.9f, 0.9f),
            Description = "Evil caster control for Protection from Evil save validation (Daze should gain +2 resistance bonus)."
        });
    }

    private static void RegisterWindDispersionTestCasters()
    {
        Register(new NPCDefinition
        {
            Id = "gust_druid",
            Name = "Zephyr Windcaller",
            Level = 5,
            CharacterClass = "Wizard",
            CreatureType = "Humanoid",
            HitDice = 5,
            SizeCategory = SizeCategory.Medium,
            STR = 10,
            DEX = 12,
            CON = 14,
            WIS = 12,
            INT = 16,
            CHA = 12,
            BAB = 2,
            BaseSpeed = 6,
            BaseHitDieHP = 22,
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.QUARTERSTAFF, EquipSlot.RightHand)
            },
            BackpackItemIds = new List<string>(),
            KnownSpellIds = new List<string> { SpellNames.OBSCURING_MIST },
            PreparedSpellSlotIds = new List<string> { SpellNames.OBSCURING_MIST, SpellNames.OBSCURING_MIST, SpellNames.OBSCURING_MIST },
            CreatureTags = new List<string> { "Humanoid", "Arcane", "WindDispersionTest" },
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Evoker,
            SpriteColor = new Color(0.58f, 0.84f, 0.72f, 1f),
            PanelColor = new Color(0.14f, 0.28f, 0.22f, 0.88f),
            NameColor = new Color(0.86f, 1f, 0.92f),
            Description = "Primary caster for Obscuring Mist concealment test coverage."
        });

        Register(new NPCDefinition
        {
            Id = "mist_wizard",
            Name = "Misty Veilweaver",
            Level = 5,
            CharacterClass = "Wizard",
            CreatureType = "Humanoid",
            HitDice = 5,
            SizeCategory = SizeCategory.Medium,
            STR = 8,
            DEX = 14,
            CON = 12,
            WIS = 12,
            INT = 18,
            CHA = 10,
            BAB = 2,
            BaseSpeed = 6,
            BaseHitDieHP = 20,
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.QUARTERSTAFF, EquipSlot.RightHand)
            },
            BackpackItemIds = new List<string>(),
            KnownSpellIds = new List<string> { SpellNames.OBSCURING_MIST, SpellNames.MAGIC_MISSILE },
            PreparedSpellSlotIds = new List<string> { SpellNames.OBSCURING_MIST, SpellNames.MAGIC_MISSILE },
            CreatureTags = new List<string> { "Humanoid", "Arcane", "WindDispersionTest" },
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Evoker,
            SpriteColor = new Color(0.76f, 0.80f, 0.92f, 1f),
            PanelColor = new Color(0.18f, 0.20f, 0.30f, 0.88f),
            NameColor = new Color(0.92f, 0.94f, 1f),
            Description = "Support wizard for concealment verification and force spell control."
        });

        Register(new NPCDefinition
        {
            Id = "test_halfling_gust",
            Name = "Finn Lightfoot",
            Level = 2,
            CharacterClass = "Rogue",
            CreatureType = "Humanoid",
            HitDice = 2,
            SizeCategory = SizeCategory.Small,
            STR = 8,
            DEX = 14,
            CON = 10,
            WIS = 10,
            INT = 10,
            CHA = 12,
            BAB = 1,
            BaseSpeed = 4,
            BaseHitDieHP = 12,
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.SHORT_SWORD, EquipSlot.RightHand),
                new EquipmentSlotPair(ItemIDs.LEATHER_ARMOR, EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            CreatureTags = new List<string> { "Humanoid", "WindDispersionTest", "SmallTarget" },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.92f, 0.84f, 0.55f, 1f),
            PanelColor = new Color(0.24f, 0.20f, 0.10f, 0.88f),
            NameColor = new Color(1f, 0.96f, 0.8f),
            Description = "Small target used to validate failed-save prone + knockback from Gust of Wind."
        });

        Register(new NPCDefinition
        {
            Id = "test_fighter_gust",
            Name = "Roland Ironheart",
            Level = 2,
            CharacterClass = "Fighter",
            CreatureType = "Humanoid",
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            STR = 14,
            DEX = 12,
            CON = 14,
            WIS = 10,
            INT = 10,
            CHA = 10,
            BAB = 2,
            BaseSpeed = 4,
            BaseHitDieHP = 20,
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.LONGSWORD, EquipSlot.RightHand),
                new EquipmentSlotPair(ItemIDs.SHIELD_HEAVY_WOODEN, EquipSlot.LeftHand),
                new EquipmentSlotPair(ItemIDs.CHAINMAIL, EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            CreatureTags = new List<string> { "Humanoid", "WindDispersionTest", "MediumTarget" },
            AIBehavior = NPCAIBehavior.DefensiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.76f, 0.78f, 0.82f, 1f),
            PanelColor = new Color(0.20f, 0.22f, 0.28f, 0.88f),
            NameColor = new Color(0.94f, 0.96f, 1f),
            Description = "Medium target used to validate prone-only Gust of Wind effect on failed Fortitude save."
        });

        Register(new NPCDefinition
        {
            Id = "test_barbarian_gust",
            Name = "Throk the Mighty",
            Level = 2,
            CharacterClass = "Barbarian",
            CreatureType = "Humanoid",
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            STR = 16,
            DEX = 10,
            CON = 16,
            WIS = 10,
            INT = 8,
            CHA = 8,
            BAB = 2,
            BaseSpeed = 8,
            BaseHitDieHP = 24,
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.GREATAXE, EquipSlot.RightHand),
                new EquipmentSlotPair(ItemIDs.HIDE_ARMOR, EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            CreatureTags = new List<string> { "Humanoid", "WindDispersionTest", "MediumTarget", "HighFortitude" },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Berserk,
            SpriteColor = new Color(0.78f, 0.52f, 0.42f, 1f),
            PanelColor = new Color(0.30f, 0.14f, 0.10f, 0.88f),
            NameColor = new Color(1f, 0.88f, 0.82f),
            Description = "High-Fortitude medium target used to demonstrate successful save resistance."
        });

        Register(new NPCDefinition
        {
            Id = "test_ogre_gust",
            Name = "Gruumsh Bonecrusher",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Giant",
            HitDice = 1,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 19,
            DEX = 8,
            CON = 13,
            WIS = 10,
            INT = 6,
            CHA = 7,
            BAB = 1,
            BaseSpeed = 8,
            BaseHitDieHP = 20,
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.GREATCLUB, EquipSlot.RightHand)
            },
            BackpackItemIds = new List<string>(),
            CreatureTags = new List<string> { "Giant", "WindDispersionTest", "LargeTarget" },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Berserk,
            SpriteColor = new Color(0.65f, 0.55f, 0.45f, 1f),
            PanelColor = new Color(0.25f, 0.12f, 0.08f, 0.88f),
            NameColor = new Color(1f, 0.85f, 0.65f),
            Description = "Large target used to validate checked (cannot advance) Gust of Wind result on failed save."
        });

        Register(new NPCDefinition
        {
            Id = "test_archer_gust",
            Name = "Elara Keeneye",
            Level = 2,
            CharacterClass = "Ranger",
            CreatureType = "Humanoid",
            HitDice = 2,
            SizeCategory = SizeCategory.Medium,
            STR = 10,
            DEX = 14,
            CON = 10,
            WIS = 12,
            INT = 10,
            CHA = 10,
            BAB = 2,
            BaseSpeed = 6,
            BaseHitDieHP = 14,
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.LONGBOW, EquipSlot.RightHand),
                new EquipmentSlotPair(ItemIDs.LONGSWORD, EquipSlot.LeftHand),
                new EquipmentSlotPair(ItemIDs.LEATHER_ARMOR, EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            CreatureTags = new List<string> { "Humanoid", "WindDispersionTest", "ArcherControl" },
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.70f, 0.86f, 0.70f, 1f),
            PanelColor = new Color(0.15f, 0.24f, 0.15f, 0.88f),
            NameColor = new Color(0.88f, 1f, 0.88f),
            Description = "Ranged control target positioned off the wind line to validate concealment attacks."
        });
    }

    private static void RegisterObscuringMistRangedOnlyTestNPCs()
    {
        Register(new NPCDefinition
        {
            Id = "ranged_test_archer_north",
            Name = "Aelindra Swiftarrow",
            Level = 4,
            CharacterClass = "Ranger",
            CreatureType = "Humanoid",
            HitDice = 4,
            SizeCategory = SizeCategory.Medium,
            STR = 12,
            DEX = 18,
            CON = 12,
            WIS = 14,
            INT = 10,
            CHA = 10,
            BAB = 4,
            BaseSpeed = 6,
            BaseHitDieHP = 28,
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.LONGBOW, EquipSlot.RightHand),
                new EquipmentSlotPair(ItemIDs.LEATHER_ARMOR, EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            CreatureTags = new List<string> { "Humanoid", "ObscuringMistRangedOnly", "Archer", "North" },
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.74f, 0.92f, 0.74f, 1f),
            PanelColor = new Color(0.15f, 0.23f, 0.14f, 0.88f),
            NameColor = new Color(0.93f, 1f, 0.92f),
            Description = "Elven longbow specialist positioned north of the mist for ranged-only concealment tests."
        });

        Register(new NPCDefinition
        {
            Id = "ranged_test_archer_ne",
            Name = "Marcus Longshot",
            Level = 4,
            CharacterClass = "Ranger",
            CreatureType = "Humanoid",
            HitDice = 4,
            SizeCategory = SizeCategory.Medium,
            STR = 14,
            DEX = 16,
            CON = 14,
            WIS = 14,
            INT = 10,
            CHA = 10,
            BAB = 4,
            BaseSpeed = 6,
            BaseHitDieHP = 32,
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.LONGBOW, EquipSlot.RightHand),
                new EquipmentSlotPair(ItemIDs.LEATHER_ARMOR, EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            CreatureTags = new List<string> { "Humanoid", "ObscuringMistRangedOnly", "Archer", "Northeast" },
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.80f, 0.86f, 0.72f, 1f),
            PanelColor = new Color(0.20f, 0.24f, 0.14f, 0.88f),
            NameColor = new Color(0.98f, 0.96f, 0.84f),
            Description = "Human ranger on the northeast lane for diagonal concealment targeting checks."
        });

        Register(new NPCDefinition
        {
            Id = "ranged_test_archer_east",
            Name = "Garrick Strongbow",
            Level = 4,
            CharacterClass = "Fighter",
            CreatureType = "Humanoid",
            HitDice = 4,
            SizeCategory = SizeCategory.Medium,
            STR = 16,
            DEX = 16,
            CON = 14,
            WIS = 10,
            INT = 10,
            CHA = 10,
            BAB = 4,
            BaseSpeed = 6,
            BaseHitDieHP = 34,
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.COMPOSITE_LONGBOW, EquipSlot.RightHand),
                new EquipmentSlotPair(ItemIDs.LEATHER_ARMOR, EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            CreatureTags = new List<string> { "Humanoid", "ObscuringMistRangedOnly", "Archer", "East" },
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.84f, 0.76f, 0.66f, 1f),
            PanelColor = new Color(0.24f, 0.18f, 0.12f, 0.88f),
            NameColor = new Color(1f, 0.92f, 0.84f),
            Description = "Fighter archer using a composite longbow to validate Strength-to-damage ranged output."
        });

        Register(new NPCDefinition
        {
            Id = "ranged_test_archer_se",
            Name = "Pip Quickfingers",
            Level = 4,
            CharacterClass = "Rogue",
            CreatureType = "Humanoid",
            HitDice = 4,
            SizeCategory = SizeCategory.Small,
            STR = 10,
            DEX = 18,
            CON = 12,
            WIS = 12,
            INT = 12,
            CHA = 10,
            BAB = 3,
            BaseSpeed = 4,
            BaseHitDieHP = 22,
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.SHORTBOW, EquipSlot.RightHand),
                new EquipmentSlotPair(ItemIDs.LEATHER_ARMOR, EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            CreatureTags = new List<string> { "Humanoid", "ObscuringMistRangedOnly", "Archer", "Southeast", "SmallTarget" },
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.95f, 0.84f, 0.56f, 1f),
            PanelColor = new Color(0.28f, 0.22f, 0.10f, 0.88f),
            NameColor = new Color(1f, 0.97f, 0.86f),
            Description = "Halfling shortbow shooter for small-size ranged accuracy and concealment behavior checks."
        });

        Register(new NPCDefinition
        {
            Id = "ranged_test_archer_south",
            Name = "Borlin Ironbolt",
            Level = 4,
            CharacterClass = "Fighter",
            CreatureType = "Humanoid",
            HitDice = 4,
            SizeCategory = SizeCategory.Medium,
            STR = 14,
            DEX = 14,
            CON = 16,
            WIS = 10,
            INT = 10,
            CHA = 8,
            BAB = 4,
            BaseSpeed = 4,
            BaseHitDieHP = 38,
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.CROSSBOW_HEAVY, EquipSlot.RightHand),
                new EquipmentSlotPair(ItemIDs.CHAINMAIL, EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            CreatureTags = new List<string> { "Humanoid", "ObscuringMistRangedOnly", "Archer", "South", "Crossbow" },
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.76f, 0.78f, 0.84f, 1f),
            PanelColor = new Color(0.18f, 0.20f, 0.26f, 0.88f),
            NameColor = new Color(0.94f, 0.96f, 1f),
            Description = "Dwarven heavy crossbowman testing slower, high-impact ranged attacks into total concealment."
        });

        Register(new NPCDefinition
        {
            Id = "ranged_test_archer_west",
            Name = "Kira Windrunner",
            Level = 3,
            CharacterClass = "Ranger",
            CreatureType = "Humanoid",
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            STR = 10,
            DEX = 16,
            CON = 12,
            WIS = 14,
            INT = 10,
            CHA = 12,
            BAB = 3,
            BaseSpeed = 6,
            BaseHitDieHP = 24,
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair(ItemIDs.SHORTBOW, EquipSlot.RightHand),
                new EquipmentSlotPair(ItemIDs.LEATHER_ARMOR, EquipSlot.Armor)
            },
            BackpackItemIds = new List<string>(),
            CreatureTags = new List<string> { "Humanoid", "ObscuringMistRangedOnly", "Archer", "West", "Scout" },
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.70f, 0.90f, 0.86f, 1f),
            PanelColor = new Color(0.12f, 0.24f, 0.22f, 0.88f),
            NameColor = new Color(0.88f, 1f, 0.98f),
            Description = "Mobile western scout with shortbow used to validate ranged-only reposition/search behavior."
        });
    }

    private static void RegisterTargetDummy()
    {
        Register(new NPCDefinition
        {
            Id = "target_dummy",
            Name = "Arcane Target Dummy",
            Level = 1,
            CharacterClass = "Commoner",
            CreatureType = "Humanoid",
            HitDice = 1,
            SizeCategory = SizeCategory.Medium,
            STR = 10,
            DEX = 10,
            CON = 1,
            WIS = 1,
            INT = 1,
            CHA = 1,
            BAB = 0,
            BaseSpeed = 6,
            NaturalArmorBonus = -4,
            FortitudeSaveOverride = SaveProgression.Poor,
            ReflexSaveOverride = SaveProgression.Poor,
            WillSaveOverride = SaveProgression.Poor,
            BaseHitDieHP = 50,
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            CreatureTags = new List<string> { "Humanoid", "Training" },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.UndeadMindless,
            SpriteColor = new Color(0.62f, 0.58f, 0.46f, 1f),
            PanelColor = new Color(0.28f, 0.24f, 0.18f, 0.85f),
            NameColor = new Color(1f, 0.92f, 0.78f),
            Description = "Training target with very low defenses that only performs basic melee attacks."
        });
    }

    private static void RegisterXPPinataGoblin()
    {
        Register(new NPCDefinition
        {
            Id = "xp_pinata_goblin",
            Name = "XP Piñata Goblin",
            Description = "A magically weakened goblin used for XP/level-up validation. It dies in one hit but awards CR 15 XP.",
            ChallengeRating = "15",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            STR = 6,
            DEX = 8,
            CON = 6,
            WIS = 6,
            INT = 6,
            CHA = 6,
            BAB = 0,
            BaseSpeed = 6,
            BaseHitDieHP = 1,
            NaturalArmorBonus = -5,
            FortitudeSaveOverride = SaveProgression.Poor,
            ReflexSaveOverride = SaveProgression.Poor,
            WillSaveOverride = SaveProgression.Poor,
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            CreatureTags = new List<string> { "Humanoid", "Goblin", "Goblinoid", "Training", "Test", "XP" },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Humanoid,
            SpriteColor = new Color(0.84f, 0.96f, 0.42f, 1f),
            PanelColor = new Color(0.35f, 0.3f, 0.08f, 0.88f),
            NameColor = new Color(1f, 0.96f, 0.62f)
        });
    }
}
