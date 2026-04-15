using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data-driven enemy type definitions for the D&D 3.5 hex RPG prototype.
/// Each EnemyDefinition describes a complete enemy template including stats,
/// equipment, creature tags, AI behavior preference, and visual appearance.
/// 
/// Enemy types are balanced for encounters against level 3 PCs.
/// </summary>
public static class EnemyDatabase
{
    private static Dictionary<string, EnemyDefinition> _enemies = new Dictionary<string, EnemyDefinition>();
    private static bool _initialized = false;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;
        _enemies.Clear();

        RegisterGoblinWarchief();
        RegisterSkeletonArcher();
        RegisterOrcBerserker();
        RegisterHobgoblinSergeant();
        RegisterOgreBrute();
        RegisterDireWolf();
        RegisterWolfPackHunter();

        Debug.Log($"[EnemyDatabase] Initialized with {_enemies.Count} enemy types.");
    }

    /// <summary>Retrieve an enemy definition by ID.</summary>
    public static EnemyDefinition Get(string id)
    {
        Init();
        if (_enemies.TryGetValue(id, out var def)) return def;
        Debug.LogWarning($"[EnemyDatabase] Unknown enemy ID: '{id}'");
        return null;
    }

    /// <summary>Get all registered enemy definitions.</summary>
    public static IEnumerable<EnemyDefinition> AllEnemies => _enemies.Values;

    /// <summary>
    /// Prebuilt encounter presets selectable before combat starts.
    /// </summary>
    public static List<EncounterPreset> ListEncounterPresets()
    {
        return new List<EncounterPreset>
        {
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

    private static void Register(EnemyDefinition def)
    {
        _enemies[def.Id] = def;
    }

    // ================================================================
    //  ENEMY TYPE DEFINITIONS
    // ================================================================

    /// <summary>
    /// Goblin Warchief (CR 1) — The original enemy type.
    /// A cunning goblin leader wielding a morningstar and shield.
    /// Melee fighter with decent AC from armor + shield + DEX.
    /// Goblinoid tag triggers Dwarf racial attack bonus.
    /// </summary>
    private static void RegisterGoblinWarchief()
    {
        Register(new EnemyDefinition
        {
            Id = "goblin_warchief",
            Name = "Goblin Warchief",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Humanoid",
            SizeCategory = SizeCategory.Small,
            STR = 14, DEX = 15, CON = 13, WIS = 10, INT = 10, CHA = 8,
            BAB = 2,
            ArmorBonus = 3,   // studded leather
            ShieldBonus = 1,  // light wooden shield
            DamageDice = 8,   // morningstar 1d8
            DamageCount = 1,
            BonusDamage = 0,
            BaseSpeed = 3,    // 15 ft (Small creature)
            AttackRange = 1,
            BaseHitDieHP = 12,
            CreatureTags = new List<string> { "Goblinoid" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("studded_leather", EquipSlot.Armor),
                new EquipmentSlotPair("morningstar", EquipSlot.RightHand),
                new EquipmentSlotPair("shield_light_wooden", EquipSlot.LeftHand)
            },
            BackpackItemIds = new List<string> { "javelin" },
            AIBehavior = EnemyAIBehavior.AggressiveMelee,
            SpriteColor = new Color(0.6f, 0.8f, 0.3f, 1f),  // greenish
            PanelColor = new Color(0.4f, 0.1f, 0.1f, 0.85f), // dark red
            NameColor = new Color(1f, 0.4f, 0.4f),
            Description = "A cunning goblin leader who rallies lesser goblins. Fights with a morningstar and shield."
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
        Register(new EnemyDefinition
        {
            Id = "skeleton_archer",
            Name = "Skeleton Archer",
            Level = 1,
            CharacterClass = "Warrior",
            CreatureType = "Undead",
            SizeCategory = SizeCategory.Medium,
            NaturalArmorBonus = 2,
            // Skeleton: STR 13 (was a human), DEX 15 (undead agility), CON 10 (undead placeholder)
            // WIS 10, INT 6 (mindless but can aim), CHA 1 (undead husk)
            STR = 13, DEX = 15, CON = 10, WIS = 10, INT = 6, CHA = 1,
            BAB = 1,
            ArmorBonus = 2,   // scraps of leather + natural armor
            ShieldBonus = 0,  // no shield (needs both hands for bow)
            DamageDice = 6,   // shortbow 1d6
            DamageCount = 1,
            BonusDamage = 0,
            BaseSpeed = 6,    // 30 ft (Medium undead)
            AttackRange = 1,  // melee fallback; ranged handled by weapon
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
            AIBehavior = EnemyAIBehavior.RangedKiter,
            SpriteColor = new Color(0.85f, 0.85f, 0.75f, 1f),  // bone white
            PanelColor = new Color(0.2f, 0.2f, 0.3f, 0.85f),   // dark grey-blue
            NameColor = new Color(0.7f, 0.85f, 1f),             // pale blue
            Description = "An animated skeleton with hollow eye sockets that glow faintly. Fires arrows with eerie precision."
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
        Register(new EnemyDefinition
        {
            Id = "orc_berserker",
            Name = "Orc Berserker",
            Level = 3,
            CharacterClass = "Barbarian",
            CreatureType = "Humanoid",
            SizeCategory = SizeCategory.Medium,
            // Orc: base STR 17 + racial = effective 17 (already includes orc bonus)
            STR = 17, DEX = 11, CON = 14, WIS = 8, INT = 8, CHA = 6,
            BAB = 3,
            ArmorBonus = 3,   // hide armor
            ShieldBonus = 0,  // two-handed weapon
            DamageDice = 12,  // greataxe 1d12
            DamageCount = 1,
            BonusDamage = 0,
            BaseSpeed = 6,    // 30 ft base (orc speed), +10 ft barbarian fast movement
            AttackRange = 1,
            BaseHitDieHP = 28, // 3d12 + CON, tough brute
            CreatureTags = new List<string> { "Orc" },
            Feats = new List<string> { "Power Attack", "Cleave" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("hide_armor", EquipSlot.Armor),
                new EquipmentSlotPair("greataxe", EquipSlot.RightHand)
            },
            BackpackItemIds = new List<string> { "javelin", "javelin" },
            AIBehavior = EnemyAIBehavior.AggressiveMelee,
            SpriteColor = new Color(0.5f, 0.6f, 0.4f, 1f),     // olive-green skin
            PanelColor = new Color(0.35f, 0.15f, 0.05f, 0.85f), // dark brown
            NameColor = new Color(1f, 0.6f, 0.3f),              // orange
            Description = "A hulking orc driven by bloodlust. Charges into melee with a massive greataxe, caring nothing for defense."
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
        Register(new EnemyDefinition
        {
            Id = "hobgoblin_sergeant",
            Name = "Hobgoblin Sergeant",
            Level = 3,
            CharacterClass = "Fighter",
            CreatureType = "Humanoid",
            SizeCategory = SizeCategory.Medium,
            // Hobgoblin: DEX +2, CON +2 already factored in
            STR = 15, DEX = 14, CON = 14, WIS = 12, INT = 10, CHA = 10,
            BAB = 3,
            ArmorBonus = 5,   // chainmail
            ShieldBonus = 2,  // heavy steel shield
            DamageDice = 8,   // longsword 1d8
            DamageCount = 1,
            BonusDamage = 0,
            BaseSpeed = 4,    // 20 ft (heavy armor reduces from 30 ft)
            AttackRange = 1,
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
            AIBehavior = EnemyAIBehavior.DefensiveMelee,
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
        Register(new EnemyDefinition
        {
            Id = "ogre_brute",
            Name = "Ogre Brute",
            Level = 4,
            CharacterClass = "Warrior",
            CreatureType = "Giant",
            SizeCategory = SizeCategory.Large,
            IsTallCreature = true,
            STR = 21, DEX = 8, CON = 15, WIS = 10, INT = 6, CHA = 7,
            BAB = 4,
            ArmorBonus = 2,   // hide scraps
            NaturalArmorBonus = 5,
            ShieldBonus = 0,
            DamageDice = 10,   // greatclub 1d10 in this prototype
            DamageCount = 1,
            BonusDamage = 0,
            BaseSpeed = 8,     // 40 ft
            AttackRange = 2,
            BaseHitDieHP = 38,
            CreatureTags = new List<string> { "Giant" },
            EquipmentIds = new List<EquipmentSlotPair>
            {
                new EquipmentSlotPair("greatclub", EquipSlot.RightHand),
                new EquipmentSlotPair("hide_armor", EquipSlot.Armor)
            },
            BackpackItemIds = new List<string> { "javelin", "javelin" },
            AIBehavior = EnemyAIBehavior.AggressiveMelee,
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
        Register(new EnemyDefinition
        {
            Id = "dire_wolf",
            Name = "Dire Wolf",
            Level = 6,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            SizeCategory = SizeCategory.Large,
            IsTallCreature = false,
            STR = 25, DEX = 15, CON = 17, WIS = 12, INT = 2, CHA = 10,
            BAB = 4,
            ArmorBonus = 0,
            NaturalArmorBonus = 5,
            ShieldBonus = 0,
            DamageDice = 8,   // bite 1d8
            DamageCount = 1,
            BonusDamage = 0,
            BaseSpeed = 10,   // 50 ft
            AttackRange = 1,  // long Large creature => 5-ft reach
            BaseHitDieHP = 45,
            CreatureTags = new List<string> { "Animal" },
            HasTripAttack = true,
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = EnemyAIBehavior.AggressiveMelee,
            SpriteColor = new Color(0.62f, 0.62f, 0.62f, 1f),
            PanelColor = new Color(0.18f, 0.18f, 0.18f, 0.85f),
            NameColor = new Color(0.95f, 0.95f, 1f),
            Description = "A massive wolf with crushing jaws and pack-hunting instincts. It can drag prey down with vicious trip attacks."
        });
    }

    /// <summary>
    /// Wolf Pack Hunter (CR 1) — Fast quadruped striker with trip tendency.
    /// </summary>
    private static void RegisterWolfPackHunter()
    {
        Register(new EnemyDefinition
        {
            Id = "wolf_pack_hunter",
            Name = "Wolf Pack Hunter",
            Level = 2,
            CharacterClass = "Warrior",
            CreatureType = "Animal",
            SizeCategory = SizeCategory.Medium,
            IsTallCreature = false,
            STR = 13, DEX = 15, CON = 15, WIS = 12, INT = 2, CHA = 6,
            BAB = 2,
            ArmorBonus = 0,
            NaturalArmorBonus = 2,
            ShieldBonus = 0,
            DamageDice = 6,   // bite 1d6
            DamageCount = 1,
            BonusDamage = 0,
            BaseSpeed = 10,   // 50 ft
            AttackRange = 1,
            BaseHitDieHP = 18,
            CreatureTags = new List<string> { "Animal" },
            HasTripAttack = true,
            EquipmentIds = new List<EquipmentSlotPair>(),
            BackpackItemIds = new List<string>(),
            AIBehavior = EnemyAIBehavior.AggressiveMelee,
            SpriteColor = new Color(0.72f, 0.72f, 0.72f, 1f),
            PanelColor = new Color(0.2f, 0.2f, 0.2f, 0.85f),
            NameColor = new Color(0.9f, 0.9f, 0.95f),
            Description = "A lean predator that circles for openings and drags enemies to the ground with vicious bites."
        });
    }
}

/// <summary>
/// Complete definition of an enemy type — everything needed to instantiate
/// a fully configured CharacterStats, equip items, and choose AI behavior.
/// </summary>
[System.Serializable]
public class EnemyDefinition
{
    public string Id;
    public string Name;
    public string Description;

    // Core D&D 3.5 stats
    public int Level;
    public string CharacterClass;
    public string CreatureType = "Humanoid";
    public SizeCategory SizeCategory = SizeCategory.Medium;
    public bool IsTallCreature = true;
    public int NaturalArmorBonus;
    public bool HasTripAttack;
    public int STR, DEX, CON, WIS, INT, CHA;
    public int BAB;
    public int ArmorBonus;
    public int ShieldBonus;
    public int DamageDice;
    public int DamageCount;
    public int BonusDamage;
    public int BaseSpeed;

    // Built-in mitigation profile
    public int DamageReductionAmount;
    public DamageBypassTag DamageReductionBypass = DamageBypassTag.None;
    public bool DamageReductionRangedOnly;
    public List<DamageResistanceEntry> DamageResistances = new List<DamageResistanceEntry>();
    public List<DamageType> DamageImmunities = new List<DamageType>();
    public int AttackRange;
    public int BaseHitDieHP;

    // Tags and feats
    public List<string> CreatureTags = new List<string>();
    public List<string> Feats = new List<string>();
    public string WeaponFocusChoice;

    // Equipment
    public List<EquipmentSlotPair> EquipmentIds = new List<EquipmentSlotPair>();
    public List<string> BackpackItemIds = new List<string>();

    // AI
    public EnemyAIBehavior AIBehavior = EnemyAIBehavior.AggressiveMelee;

    // Visuals
    public Color SpriteColor = Color.white;
    public Color PanelColor = new Color(0.4f, 0.1f, 0.1f, 0.85f);
    public Color NameColor = new Color(1f, 0.4f, 0.4f);
}

[System.Serializable]
public class EncounterPreset
{
    public string Id;
    public string DisplayName;
    public string Description;
    public List<string> EnemyIds = new List<string>();

    public EncounterPreset(string id, string displayName, string description, List<string> enemyIds)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        EnemyIds = enemyIds ?? new List<string>();
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
/// AI behavior archetypes that influence how an enemy acts during its turn.
/// The GameManager NPC AI coroutine uses this to decide movement and attack patterns.
/// </summary>
public enum EnemyAIBehavior
{
    /// <summary>Move directly toward closest PC and attack in melee.</summary>
    AggressiveMelee,

    /// <summary>Stay at range, shoot with ranged weapon. Retreat if enemies get close.</summary>
    RangedKiter,

    /// <summary>Advance cautiously, use Combat Expertise for extra AC, hold position.</summary>
    DefensiveMelee
}