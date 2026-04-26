using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data-driven NPC type definitions for the D&D 3.5 hex RPG prototype.
/// Each NPCDefinition describes a complete NPC template including stats,
/// equipment, creature tags, AI behavior preference, and visual appearance.
/// 
/// Enemy types are balanced for encounters against level 3 PCs.
/// </summary>
public static class NPCDatabase
{
    private static Dictionary<string, NPCDefinition> _npcs = new Dictionary<string, NPCDefinition>();
    private static bool _initialized = false;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;
        _npcs.Clear();

        RegisterGoblinWarchief();
        RegisterGoblinFeintDrill();
        RegisterSkeletonArcher();
        RegisterSkeletonWarrior();
        RegisterWightDreadwalker();
        RegisterOrcBerserker();
        RegisterOrcGrappleDrill();
        RegisterHobgoblinSergeant();
        RegisterOgreBrute();
        RegisterDireWolf();
        RegisterTiger();
        RegisterDireTiger();
        RegisterBrownBear();
        RegisterDireBear();
        RegisterWolfPackHunter();
        RegisterSummonMonsterBaseCreatures();
        RegisterFiendishWolf();
        RegisterFiendishDireBear();
        RegisterHumanPaladin();
        RegisterHumanCleric();
        RegisterArcaneMissileAdept();
        RegisterZombieShambler();
        RegisterTargetDummy();

        Debug.Log($"[NPCDatabase] Initialized with {_npcs.Count} NPC types.");
    }

    /// <summary>Retrieve an NPC definition by ID.</summary>
    public static NPCDefinition Get(string id)
    {
        Init();
        if (_npcs.TryGetValue(id, out var def)) return def;
        Debug.LogWarning($"[NPCDatabase] Unknown NPC ID: '{id}'");
        return null;
    }

    /// <summary>Get all registered NPC definitions.</summary>
    public static IEnumerable<NPCDefinition> AllNPCs => _npcs.Values;

    /// <summary>Get the configured AI profile archetype for an NPC ID.</summary>
    public static NPCAIProfileArchetype GetAIProfileArchetype(string id)
    {
        NPCDefinition def = Get(id);
        return def != null ? def.AIProfileArchetype : NPCAIProfileArchetype.None;
    }

    /// <summary>
    /// Prebuilt encounter presets selectable before combat starts.
    /// </summary>
    public static List<EncounterPreset> ListEncounterPresets()
    {
        return new List<EncounterPreset>
        {
            new EncounterPreset("grapple_test", "🧪 Grapple Test Encounter", "Fighter vs orc in adjacent squares for dedicated grappling checks.", new List<string> { "orc_grapple_drill" }),
            new EncounterPreset("feint_sneak_test", "🗡️ Feint & Sneak Attack Test", "Level 6 rogue vs one goblin tuned for Bluff feints and sneak attack validation.", new List<string> { "goblin_feint_drill" }),
            new EncounterPreset("turn_undead_test", "✝️ Turn Undead Test", "Expanded cleric stress test with 12 skeletons and 3 wights to force HD-pool target selection.", new List<string> {
                "skeleton_archer", "skeleton_archer", "skeleton_archer", "skeleton_archer", "skeleton_archer", "skeleton_archer",
                "wight_dreadwalker", "wight_dreadwalker", "wight_dreadwalker",
                "skeleton_archer", "skeleton_archer", "skeleton_archer", "skeleton_archer", "skeleton_archer", "skeleton_archer"
            }),
            new EncounterPreset("armor_targeting_test", "🏹 Armor Priority Targeting Test", "Wizard (unarmored), rogue (light), fighter (heavy) vs 2 skeleton archers that prioritize weakest armor in range.", new List<string> { "skeleton_archer", "skeleton_archer" }),
            new EncounterPreset("tiger_hunt_test", "🐅 Tiger Hunt Test", "Three-PC behavior test for tiger pounce, improved grab, rake, scent vs invisible target, and low-HP withdraw AI.", new List<string> { "tiger" }),
            new EncounterPreset("ogre_battle_test", "🧙 Ogre Battle", "Player-controlled wizard and dire tiger ally versus two ogre brutes.", new List<string> { "dire_tiger", "ogre_brute", "ogre_brute" }),
            new EncounterPreset("shield_bash_test", "🛡️ Shield Bash Test", "Compare shield bash AC behavior: Shielder keeps shield AC with Improved Shield Bash while Basher loses shield AC until next turn.", new List<string> { "orc_berserker", "orc_berserker" }),
            new EncounterPreset("celestial_template_test", "✨ Celestial Template Test", "Good cleric with celestial wolf + celestial dire bear allies against evil undead. Templates are applied at spawn time.", new List<string> { "wolf_pack_hunter", "dire_bear", "skeleton_warrior", "skeleton_archer", "zombie_shambler" }),
            new EncounterPreset("fiendish_template_test", "🔥 Fiendish Template Test", "Evil necromancer with fiendish wolf + fiendish dire bear allies against good paladin and cleric. Templates are applied at spawn time.", new List<string> { "fiendish_wolf", "fiendish_dire_bear", "human_paladin", "human_cleric" }),
            new EncounterPreset("summon_monster_test", "🌀 Summon Monster Test", "Cleric + wizard summon drill with Summon Monster I/II prepared on both casters for selection UI, placement, and command validation.", new List<string> { "orc_berserker", "skeleton_archer", "goblin_warchief" }),
            new EncounterPreset("npc_magic_missile_test", "🧪 NPC Magic Missile Test", "Enemy evoker only casts Magic Missile and should skip targets protected by Shield.", new List<string> { "arcane_missile_adept" }),
            new EncounterPreset("wizard_spell_test", "📘 Wizard Spell Test", "Single wizard scenario with every implemented wizard spell auto-populated into prepared slots versus a low-defense target dummy.", new List<string> { "target_dummy" }),
            new EncounterPreset("cleric_spell_test", "📖 Cleric Spell Test", "Single cleric scenario with every implemented cleric spell auto-populated into prepared slots versus a low-defense target dummy.", new List<string> { "target_dummy" }),
            new EncounterPreset("goblin_raiders", "Goblin Raiders", "Balanced skirmish against goblins and an archer.", new List<string> { "goblin_warchief", "hobgoblin_sergeant", "skeleton_archer" }),
            new EncounterPreset("undead_ambush", "Undead Ambush", "Ranged pressure from skeletons with melee support.", new List<string> { "skeleton_archer", "skeleton_archer", "orc_berserker" }),
            new EncounterPreset("wolf_pack", "Wolf Pack", "Fast-moving animals that try to surround and trip.", new List<string> { "dire_wolf", "wolf_pack_hunter", "wolf_pack_hunter" }),
            new EncounterPreset("ogre_bodyguard", "Ogre Bodyguard", "A dangerous large brute protected by disciplined infantry.", new List<string> { "ogre_brute", "hobgoblin_sergeant", "goblin_warchief" }),
            new EncounterPreset("mixed_patrol", "Mixed Patrol", "Varied enemies showcasing melee, ranged, and size differences.", new List<string> { "wolf_pack_hunter", "skeleton_archer", "orc_berserker", "goblin_warchief" })
        };
    }

    public static EncounterPreset GetEncounterPreset(string presetId)
    {
        var all = ListEncounterPresets();
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].Id == presetId) return all[i];
        }
        return all.Count > 0 ? all[0] : null;
    }

    private static void Register(NPCDefinition def)
    {
        _npcs[def.Id] = def;
    }

    // ================================================================
    //  NPC TYPE DEFINITIONS
    // ================================================================

    /// <summary>
    /// Goblin Warchief (CR 1) — The original enemy type.
    /// A cunning goblin leader wielding a morningstar and shield.
    /// Melee fighter with decent AC from armor + shield + DEX.
    /// Goblinoid tag triggers Dwarf racial attack bonus.
    /// </summary>
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
                new EquipmentSlotPair("studded_leather", EquipSlot.Armor),
                new EquipmentSlotPair("morningstar", EquipSlot.RightHand),
                new EquipmentSlotPair("shield_light_wooden", EquipSlot.LeftHand)
            },
            BackpackItemIds = new List<string> { "javelin" },
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
                new EquipmentSlotPair("leather_armor", EquipSlot.Armor),
                new EquipmentSlotPair("morningstar", EquipSlot.RightHand),
                new EquipmentSlotPair("shield_light_wooden", EquipSlot.LeftHand)
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
            // Skeleton: STR 13 (was a human), DEX 15 (undead agility), CON 10 (undead placeholder)
            // WIS 10, INT 6 (mindless but can aim), CHA 1 (undead husk)
            STR = 13, DEX = 15, CON = 10, WIS = 10, INT = 6, CHA = 1,
            BAB = 0,
            BaseSpeed = 6,    // 30 ft (Medium undead)
            BaseHitDieHP = 8, // 1 HD undead, fragile
            CreatureTags = new List<string> { "Undead" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("leather_armor", EquipSlot.Armor),
                new EquipmentSlotPair("shortbow", EquipSlot.RightHand)
            },
            BackpackItemIds = new List<string> { "short_sword" },
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Bludgeoning,
            DamageImmunities = new List<DamageType> { DamageType.Cold },
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
            STR = 15, DEX = 13, CON = 10, WIS = 10, INT = 0, CHA = 1,
            BAB = 4,
            BaseSpeed = 6,
            BaseHitDieHP = 24,
            CreatureTags = new List<string> { "Undead" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("banded_mail", EquipSlot.Armor),
                new EquipmentSlotPair("longsword", EquipSlot.RightHand),
                new EquipmentSlotPair("shield_heavy_steel", EquipSlot.LeftHand)
            },
            BackpackItemIds = new List<string>(),
            DamageReductionAmount = 5,
            DamageReductionBypass = DamageBypassTag.Bludgeoning,
            DamageImmunities = new List<DamageType> { DamageType.Cold },
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
            STR = 17, DEX = 6, CON = 10, WIS = 10, INT = 0, CHA = 1,
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
                new EquipmentSlotPair("hide_armor", EquipSlot.Armor),
                new EquipmentSlotPair("greataxe", EquipSlot.RightHand)
            },
            BackpackItemIds = new List<string> { "javelin", "javelin" },
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
                new EquipmentSlotPair("leather_armor", EquipSlot.Armor),
                new EquipmentSlotPair("dagger", EquipSlot.RightHand)
            },
            BackpackItemIds = new List<string> { "dagger" },
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Grappler,
            SpriteColor = new Color(0.55f, 0.68f, 0.45f, 1f),
            PanelColor = new Color(0.32f, 0.14f, 0.08f, 0.85f),
            NameColor = new Color(1f, 0.72f, 0.42f),
            Description = "An orc sparring target tuned for grappling tests. Carries a dagger to validate weapon-in-grapple interactions."
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
                new EquipmentSlotPair("chainmail", EquipSlot.Armor),
                new EquipmentSlotPair("longsword", EquipSlot.RightHand),
                new EquipmentSlotPair("shield_heavy_steel", EquipSlot.LeftHand)
            },
            BackpackItemIds = new List<string> { "javelin", "javelin", "potion_healing" },
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
                new EquipmentSlotPair("greatclub", EquipSlot.RightHand),
                new EquipmentSlotPair("hide_armor", EquipSlot.Armor)
            },
            BackpackItemIds = new List<string> { "javelin", "javelin" },
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
        RegisterSummonDog();
        RegisterSummonDireRat();
        RegisterSummonEagle();
        RegisterSummonOctopus();
        RegisterSummonSmallViper();
        RegisterSummonDireBat();
        RegisterSummonSmallAirElemental();
        RegisterSummonSmallFireElemental();
        RegisterSummonCrocodile();
        RegisterSummonBlackBear();
        RegisterSummonApe();
        RegisterSummonDireBadger();
        RegisterSummonLargeShark();
        RegisterSummonConstrictorSnake();

        // Alias IDs for external checks/docs that refer to generic base names.
        RegisterSummonCreatureAliases();
    }

    private static void RegisterSummonCreatureAliases()
    {
        RegisterSummonAlias("dog", "summon_dog");
        RegisterSummonAlias("eagle", "summon_eagle");
        RegisterSummonAlias("dire_rat", "summon_dire_rat");
        RegisterSummonAlias("wolf", "wolf_pack_hunter", "Wolf");
        RegisterSummonAlias("badger", "summon_dire_badger", "Badger");

        // These IDs are used by external validation scripts; map to closest existing summon baselines.
        RegisterSummonAlias("riding_dog", "summon_dog", "Riding Dog");
        RegisterSummonAlias("owl", "summon_eagle", "Owl");
        RegisterSummonAlias("raven", "summon_eagle", "Raven");
        RegisterSummonAlias("giant_bee", "summon_dire_bat", "Giant Bee");
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

    private static void RegisterSummonDog()
    {
        Register(new NPCDefinition
        {
            Id = "summon_dog",
            Name = "Dog",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 13, DEX = 17, CON = 15, WIS = 12, INT = 2, CHA = 6,
            NaturalArmorBonus = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8,
            BaseHitDieHP = 8,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.83f, 0.73f, 0.58f, 1f),
            PanelColor = new Color(0.22f, 0.16f, 0.12f, 0.85f),
            NameColor = new Color(0.98f, 0.9f, 0.8f),
            Description = "Summon Monster baseline dog used for celestial variants and low-level summon validation."
        });
    }

    private static void RegisterSummonDireRat()
    {
        Register(new NPCDefinition
        {
            Id = "summon_dire_rat",
            Name = "Dire Rat",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 10, DEX = 17, CON = 12, WIS = 12, INT = 1, CHA = 4,
            NaturalArmorBonus = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 8,
            BaseHitDieHP = 6,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.55f, 0.55f, 0.55f, 1f),
            PanelColor = new Color(0.18f, 0.18f, 0.18f, 0.85f),
            NameColor = new Color(0.86f, 0.86f, 0.9f),
            Description = "Summon Monster baseline dire rat."
        });
    }

    private static void RegisterSummonEagle()
    {
        Register(new NPCDefinition
        {
            Id = "summon_eagle",
            Name = "Eagle",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 1,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 10, DEX = 15, CON = 12, WIS = 14, INT = 2, CHA = 7,
            NaturalArmorBonus = 1,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Talons", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Beak", DamageDice = 4, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 10,
            BaseHitDieHP = 7,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.78f, 0.73f, 0.64f, 1f),
            PanelColor = new Color(0.2f, 0.17f, 0.1f, 0.85f),
            NameColor = new Color(0.97f, 0.91f, 0.77f),
            Description = "Summon Monster baseline eagle used for celestial eagle variants."
        });
    }

    private static void RegisterSummonOctopus()
    {
        Register(new NPCDefinition
        {
            Id = "summon_octopus",
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
            Id = "summon_small_viper",
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

    private static void RegisterSummonDireBat()
    {
        Register(new NPCDefinition
        {
            Id = "summon_dire_bat",
            Name = "Dire Bat",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 4,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 17, DEX = 13, CON = 17, WIS = 14, INT = 2, CHA = 6,
            NaturalArmorBonus = 4,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 10,
            BaseHitDieHP = 30,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.4f, 0.32f, 0.38f, 1f),
            PanelColor = new Color(0.15f, 0.1f, 0.16f, 0.85f),
            NameColor = new Color(0.85f, 0.79f, 0.9f),
            Description = "Summon Monster baseline dire bat used for fiendish dire bat variants."
        });
    }

    private static void RegisterSummonSmallAirElemental()
    {
        Register(new NPCDefinition
        {
            Id = "summon_small_air_elemental",
            Name = "Small Air Elemental",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Elemental",
            HitDice = 2,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 10, DEX = 17, CON = 12, WIS = 11, INT = 4, CHA = 11,
            NaturalArmorBonus = 2,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 10,
            BaseHitDieHP = 14,
            CreatureTags = new List<string> { "Elemental", "Air", "SummonBase" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.68f, 0.86f, 1f, 1f),
            PanelColor = new Color(0.14f, 0.19f, 0.26f, 0.85f),
            NameColor = new Color(0.85f, 0.95f, 1f),
            Description = "Summon Monster baseline Small Air Elemental."
        });
    }

    private static void RegisterSummonSmallFireElemental()
    {
        Register(new NPCDefinition
        {
            Id = "summon_small_fire_elemental",
            Name = "Small Fire Elemental",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Elemental",
            HitDice = 2,
            SizeCategory = SizeCategory.Small,
            IsTallCreature = false,
            STR = 12, DEX = 17, CON = 12, WIS = 11, INT = 4, CHA = 11,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Slam", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 10,
            BaseHitDieHP = 15,
            CreatureTags = new List<string> { "Elemental", "Fire", "SummonBase" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(1f, 0.55f, 0.22f, 1f),
            PanelColor = new Color(0.28f, 0.1f, 0.08f, 0.85f),
            NameColor = new Color(1f, 0.86f, 0.72f),
            Description = "Summon Monster baseline Small Fire Elemental."
        });
    }

    private static void RegisterSummonCrocodile()
    {
        Register(new NPCDefinition
        {
            Id = "summon_crocodile",
            Name = "Crocodile",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 19, DEX = 12, CON = 17, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 5,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 8, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 6,
            BaseHitDieHP = 24,
            CreatureTags = new List<string> { "Animal", "Aquatic", "SummonBase" },
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Bite",
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.42f, 0.56f, 0.34f, 1f),
            PanelColor = new Color(0.12f, 0.2f, 0.1f, 0.85f),
            NameColor = new Color(0.84f, 0.93f, 0.78f),
            Description = "Summon Monster baseline crocodile with bite-into-grapple threat."
        });
    }

    private static void RegisterSummonBlackBear()
    {
        Register(new NPCDefinition
        {
            Id = "summon_black_bear",
            Name = "Black Bear",
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
            BaseSpeed = 8,
            BaseHitDieHP = 24,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.28f, 0.2f, 0.13f, 1f),
            PanelColor = new Color(0.14f, 0.09f, 0.06f, 0.85f),
            NameColor = new Color(0.9f, 0.78f, 0.62f),
            Description = "Summon Monster baseline black bear."
        });
    }

    private static void RegisterSummonApe()
    {
        Register(new NPCDefinition
        {
            Id = "summon_ape",
            Name = "Ape",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 4,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 21, DEX = 15, CON = 14, WIS = 12, INT = 2, CHA = 7,
            NaturalArmorBonus = 4,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 6, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 8,
            BaseHitDieHP = 30,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.45f, 0.32f, 0.25f, 1f),
            PanelColor = new Color(0.18f, 0.12f, 0.09f, 0.85f),
            NameColor = new Color(0.95f, 0.84f, 0.75f),
            Description = "Summon Monster baseline ape with heavy claw volume."
        });
    }

    private static void RegisterSummonDireBadger()
    {
        Register(new NPCDefinition
        {
            Id = "summon_dire_badger",
            Name = "Dire Badger",
            Level = 3,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 3,
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 14, DEX = 17, CON = 15, WIS = 12, INT = 2, CHA = 10,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Claw", DamageDice = 4, DamageCount = 1, Count = 2, BonusDamageSource = DamageBonusSource.Strength, Range = 1, IsPrimary = true },
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthHalf, Range = 1, IsPrimary = false }
            },
            BaseSpeed = 6,
            BaseHitDieHP = 22,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasScent = true,
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.5f, 0.45f, 0.43f, 1f),
            PanelColor = new Color(0.2f, 0.17f, 0.16f, 0.85f),
            NameColor = new Color(0.93f, 0.9f, 0.9f),
            Description = "Summon Monster baseline dire badger."
        });
    }

    private static void RegisterSummonLargeShark()
    {
        Register(new NPCDefinition
        {
            Id = "summon_large_shark",
            Name = "Large Shark",
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
            Id = "summon_constrictor_snake",
            Name = "Constrictor Snake",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            HitDice = 4,
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 17, DEX = 17, CON = 13, WIS = 12, INT = 1, CHA = 2,
            NaturalArmorBonus = 3,
            NaturalAttacks = new List<NaturalAttackDefinition>
            {
                new NaturalAttackDefinition { Name = "Bite", DamageDice = 6, DamageCount = 1, Count = 1, BonusDamageSource = DamageBonusSource.StrengthOneAndHalf, Range = 1, IsPrimary = true }
            },
            BaseSpeed = 6,
            BaseHitDieHP = 25,
            CreatureTags = new List<string> { "Animal", "SummonBase" },
            HasImprovedGrab = true,
            ImprovedGrabTriggerAttackName = "Bite",
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = NPCAIBehavior.AggressiveMelee,
            AIProfileArchetype = NPCAIProfileArchetype.Animal,
            SpriteColor = new Color(0.56f, 0.64f, 0.28f, 1f),
            PanelColor = new Color(0.2f, 0.23f, 0.1f, 0.85f),
            NameColor = new Color(0.9f, 0.95f, 0.74f),
            Description = "Summon Monster baseline constrictor snake."
        });
    }

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
                new EquipmentSlotPair("longsword", EquipSlot.RightHand),
                new EquipmentSlotPair("shield_heavy_steel", EquipSlot.LeftHand),
                new EquipmentSlotPair("chainmail", EquipSlot.Armor)
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
                new EquipmentSlotPair("mace_heavy", EquipSlot.RightHand),
                new EquipmentSlotPair("shield_heavy_steel", EquipSlot.LeftHand),
                new EquipmentSlotPair("chainmail", EquipSlot.Armor)
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
                new EquipmentSlotPair("quarterstaff", EquipSlot.RightHand)
            },
            BackpackItemIds = new List<string>(),
            KnownSpellIds = new List<string> { "magic_missile" },
            PreparedSpellSlotIds = new List<string> { "magic_missile", "magic_missile", "magic_missile", "magic_missile", "magic_missile", "magic_missile", "magic_missile" },
            CreatureTags = new List<string> { "Humanoid", "Arcane", "AI:MagicMissileOnly" },
            AIBehavior = NPCAIBehavior.RangedKiter,
            AIProfileArchetype = NPCAIProfileArchetype.Evoker,
            SpriteColor = new Color(0.62f, 0.66f, 0.92f, 1f),
            PanelColor = new Color(0.16f, 0.14f, 0.32f, 0.85f),
            NameColor = new Color(0.9f, 0.9f, 1f),
            Description = "Enemy wizard test NPC configured to only cast Magic Missile and avoid Shielded targets."
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
}

/// <summary>
/// Complete definition of an NPC type — everything needed to instantiate
/// a fully configured CharacterStats, equip items, and choose AI behavior.
/// </summary>
[System.Serializable]
public class NPCDefinition
{
    public string Id;
    public string Name;
    public string Description;

    // Core D&D 3.5 stats
    public int Level;
    public string CharacterClass;
    public string CreatureType = "Humanoid";
    public int HitDice = 1;
    public BABProgression? BABOverride;
    public int? BaseAttackBonusOverride;
    public SaveProgression? FortitudeSaveOverride;
    public SaveProgression? ReflexSaveOverride;
    public SaveProgression? WillSaveOverride;
    public SizeCategory SizeCategory = SizeCategory.Medium;
    public bool IsTallCreature = true;
    public int NaturalArmorBonus;
    public bool HasTripAttack;
    public int TripAttackCheckBonus;
    public bool HasImprovedGrab;
    public string ImprovedGrabTriggerAttackName;
    public bool HasPounce;
    public bool HasRake;
    public bool HasScent;
    public NaturalAttackDefinition RakeAttack;
    public int STR, DEX, CON, WIS, INT, CHA;
    public int BAB;
    public int BaseSpeed;

    // Optional natural attacks used when no manufactured weapon is equipped
    public List<NaturalAttackDefinition> NaturalAttacks = new List<NaturalAttackDefinition>();

    // Built-in mitigation profile
    public int DamageReductionAmount;
    public DamageBypassTag DamageReductionBypass = DamageBypassTag.None;
    public bool DamageReductionRangedOnly;
    public List<DamageResistanceEntry> DamageResistances = new List<DamageResistanceEntry>();
    public List<DamageType> DamageImmunities = new List<DamageType>();
    public int SpellResistance;
    public int BaseHitDieHP;

    // Template-afforded actions
    public bool GainsSmiteEvil;
    public bool GainsSmiteGood;

    // Tags and feats
    public List<string> CreatureTags = new List<string>();
    public List<string> Feats = new List<string>();
    public string WeaponFocusChoice;

    // Optional spellcasting loadout for NPC AI casters.
    public List<string> KnownSpellIds = new List<string>();
    public List<string> PreparedSpellSlotIds = new List<string>();

    // Optional descriptive traits surfaced in tooltips/UI (e.g., "Darkvision 60 ft", "Smite Evil 1/day").
    public List<string> SpecialAbilities = new List<string>();

    // Runtime template descriptors.
    public List<string> AppliedTemplateIds = new List<string>();

    // Equipment
    public List<EquipmentSlotPair> EquipmentIds = new List<EquipmentSlotPair>();
    public List<string> BackpackItemIds = new List<string>();

    // Team/control flags
    public bool IsAlly = false;
    public bool IsControllable = false;

    // AI
    public NPCAIBehavior AIBehavior = NPCAIBehavior.AggressiveMelee;
    public NPCAIProfileArchetype AIProfileArchetype = NPCAIProfileArchetype.None;
    // null = use AI profile default, true/false = force this individual NPC behavior.
    public bool? UseCoupDeGrace;

    // Visuals
    public Color SpriteColor = Color.white;
    public Color PanelColor = new Color(0.4f, 0.1f, 0.1f, 0.85f);
    public Color NameColor = new Color(1f, 0.4f, 0.4f);

    /// <summary>
    /// Deep-clone this definition so spawn-time template mutation never edits the shared database entry.
    /// </summary>
    public NPCDefinition Clone()
    {
        NPCDefinition clone = (NPCDefinition)MemberwiseClone();

        clone.NaturalAttacks = new List<NaturalAttackDefinition>();
        if (NaturalAttacks != null)
        {
            for (int i = 0; i < NaturalAttacks.Count; i++)
            {
                NaturalAttackDefinition attack = NaturalAttacks[i];
                if (attack == null) continue;
                clone.NaturalAttacks.Add(new NaturalAttackDefinition
                {
                    Name = attack.Name,
                    DamageDice = attack.DamageDice,
                    DamageCount = attack.DamageCount,
                    Count = attack.Count,
                    BonusDamageSource = attack.BonusDamageSource,
                    Range = attack.Range,
                    IsPrimary = attack.IsPrimary
                });
            }
        }

        clone.RakeAttack = RakeAttack == null
            ? null
            : new NaturalAttackDefinition
            {
                Name = RakeAttack.Name,
                DamageDice = RakeAttack.DamageDice,
                DamageCount = RakeAttack.DamageCount,
                Count = RakeAttack.Count,
                BonusDamageSource = RakeAttack.BonusDamageSource,
                Range = RakeAttack.Range,
                IsPrimary = RakeAttack.IsPrimary
            };

        clone.DamageResistances = new List<DamageResistanceEntry>();
        if (DamageResistances != null)
        {
            for (int i = 0; i < DamageResistances.Count; i++)
            {
                DamageResistanceEntry entry = DamageResistances[i];
                if (entry == null) continue;
                clone.DamageResistances.Add(new DamageResistanceEntry { Type = entry.Type, Amount = entry.Amount });
            }
        }

        clone.DamageImmunities = DamageImmunities != null
            ? new List<DamageType>(DamageImmunities)
            : new List<DamageType>();
        clone.CreatureTags = CreatureTags != null
            ? new List<string>(CreatureTags)
            : new List<string>();
        clone.Feats = Feats != null
            ? new List<string>(Feats)
            : new List<string>();
        clone.KnownSpellIds = KnownSpellIds != null
            ? new List<string>(KnownSpellIds)
            : new List<string>();
        clone.PreparedSpellSlotIds = PreparedSpellSlotIds != null
            ? new List<string>(PreparedSpellSlotIds)
            : new List<string>();
        clone.SpecialAbilities = SpecialAbilities != null
            ? new List<string>(SpecialAbilities)
            : new List<string>();
        clone.AppliedTemplateIds = AppliedTemplateIds != null
            ? new List<string>(AppliedTemplateIds)
            : new List<string>();

        clone.EquipmentIds = new List<EquipmentSlotPair>();
        if (EquipmentIds != null)
        {
            for (int i = 0; i < EquipmentIds.Count; i++)
            {
                EquipmentSlotPair eq = EquipmentIds[i];
                if (eq == null) continue;
                clone.EquipmentIds.Add(new EquipmentSlotPair(eq.ItemId, eq.Slot));
            }
        }

        clone.BackpackItemIds = BackpackItemIds != null
            ? new List<string>(BackpackItemIds)
            : new List<string>();

        return clone;
    }
}

[System.Serializable]
public class EncounterPreset
{
    public string Id;
    public string DisplayName;
    public string Description;
    public List<string> NPCIds = new List<string>();

    public EncounterPreset(string id, string displayName, string description, List<string> npcIds)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        NPCIds = npcIds ?? new List<string>();
    }
}
/// <summary>
/// Pairs an item ID with the slot it should be equipped to.
/// </summary>
[System.Serializable]
public class EquipmentSlotPair
{
    public string ItemId;
    public EquipSlot Slot;

    public EquipmentSlotPair(string itemId, EquipSlot slot)
    {
        ItemId = itemId;
        Slot = slot;
    }
}

/// <summary>
/// Runtime AI profile archetypes used to instantiate specialized AIProfile objects.
/// </summary>
public enum NPCAIProfileArchetype
{
    None,
    Animal,
    Humanoid,
    Berserk,
    Grappler,
    Ranged,
    Healer,
    Spellcaster,
    Evoker,
    Abjurer,
    Necromancer,
    UndeadMindless
}

/// <summary>
/// AI behavior archetypes that influence how an NPC acts during its turn.
/// The GameManager NPC AI coroutine uses this as fallback movement/attack behavior.
/// </summary>
public enum NPCAIBehavior
{
    /// <summary>Move directly toward closest PC and attack in melee.</summary>
    AggressiveMelee,

    /// <summary>Stay at range, shoot with ranged weapon. Retreat if enemies get close.</summary>
    RangedKiter,

    /// <summary>Advance cautiously, use Combat Expertise for extra AC, hold position.</summary>
    DefensiveMelee
}