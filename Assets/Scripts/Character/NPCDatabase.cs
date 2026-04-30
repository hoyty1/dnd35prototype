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
public static partial class NPCDatabase
{
    private static Dictionary<string, NPCDefinition> _npcs = new Dictionary<string, NPCDefinition>();
    private static bool _initialized = false;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;
        _npcs.Clear();

        // Register all non-Monster Manual creatures first.
        RegisterCustomCreatures();

        // Register Monster Manual creatures from alphabetical files.
        // These intentionally override summon alias IDs such as dog/owl/raven.
        RegisterCreatures_B();
        RegisterCreatures_C();
        RegisterCreatures_D();
        RegisterCreatures_F();
        RegisterCreatures_H();
        RegisterCreatures_M();
        RegisterCreatures_O();
        RegisterCreatures_R();
        RegisterCreatures_S();
        RegisterCreatures_V();

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
            new EncounterPreset("grease_test", "🧪 Grease Mechanics Test", "Wizard + fighter versus clustered low-Reflex grapplers for Grease area/object/armor mode and grapple-defense validation.", new List<string> { "grease_test_grappler1", "grease_test_grappler2", "grease_test_grappler3", "grease_test_grappler4" }),
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
            new EncounterPreset("npc_magic_missile_test", "🧪 NPC Magic Missile Test", "Enemy evoker only casts Magic Missile; Shield should block damage during resolution (not targeting).", new List<string> { "arcane_missile_adept" }),
            new EncounterPreset("protection_from_evil_test", "🛡️ Protection from Evil Test", "Single protected wizard versus evil and non-evil threats to validate mental-control block, summoned contact barrier, +2 AC/save bonuses vs Evil, and no bonus vs non-Evil controls.", new List<string> { "evil_enchanter_test", "fiendish_wolf", "evil_goblin_test", "neutral_bandit_test", "neutral_mage_test", "evil_acolyte_test" }),
            new EncounterPreset("wind_dispersion_test", "🌫️ Obscuring Mist Test", "Focused concealment test for Obscuring Mist (20% at 5 ft, 50% beyond 5 ft) with mixed target sizes and ranged pressure.", new List<string> { "test_halfling_gust", "test_fighter_gust", "test_barbarian_gust", "test_ogre_gust", "test_archer_gust" }),
            new EncounterPreset("obscuring_mist_ranged_only", "🏹 Obscuring Mist - Ranged Combat Only", "Dedicated ranged concealment scenario: six ranged attackers with mixed weapon types surround a party in Obscuring Mist to validate total concealment targeting, last-known-position behavior, and visible-target prioritization.", new List<string> { "ranged_test_archer_north", "ranged_test_archer_ne", "ranged_test_archer_east", "ranged_test_archer_se", "ranged_test_archer_south", "ranged_test_archer_west" }),
            new EncounterPreset("disrupt_undead_test", "☀️ Disrupt Undead Test", "Wizard cantrip drill versus mixed targets: skeletons + zombie + one living orc to confirm undead-only damage.", new List<string> { "skeleton_warrior", "skeleton_warrior", "zombie_shambler", "orc_berserker" }),
            new EncounterPreset("true_strike_test", "🎯 True Strike Test", "Focused True Strike validation (next attack +20 insight, concealment bypass, expiration timing) using the wizard spell test setup.", new List<string> { "target_dummy" }),
            new EncounterPreset("wizard_spell_test", "📘 Wizard Spell Test", "Single wizard scenario with every implemented wizard spell auto-populated into prepared slots versus a low-defense target dummy.", new List<string> { "target_dummy" }),
            new EncounterPreset("cleric_spell_test", "📖 Cleric Spell Test", "Single cleric scenario with every implemented cleric spell auto-populated into prepared slots versus a low-defense target dummy.", new List<string> { "target_dummy" }),
            new EncounterPreset("charm_person_test", "💞 Charm Person Test", "Wizard enchanter scenario: humanoid target (4 HD) with nearby hostiles to validate humanoid-only targeting, threatened +5 Will save bonus, charm AI non-hostility, and healing support behavior.", new List<string> { "human_cleric", "orc_berserker" }),
            new EncounterPreset("sleep_spell_test", "💤 Sleep Spell Test", "Wizard Sleep validation: mixed-HD enemies to test 4d4 pool, lowest-HD-first targeting, 4 HD cap immunity, wake-on-damage, and Aid Another wake ally flow.", new List<string> { "skeleton_archer", "goblin_warchief", "orc_berserker", "human_cleric" }),
            new EncounterPreset("creature_showcase", "🕷️ Creature Showcase - Animals & Vermin", "Showcase encounter covering newly implemented Monster Manual animals, vermin, and viper variants.", new List<string> { "dog", "owl", "badger", "monkey", "dire_rat", "raven", "hawk", "giant_fire_beetle", "monstrous_centipede_medium", "monstrous_scorpion_medium", "monstrous_spider_medium", "viper_medium" }),
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

    private static void RegisterCustomCreatures()
    {
        RegisterGoblinWarchief();
        RegisterGoblinFeintDrill();
        RegisterSkeletonArcher();
        RegisterSkeletonWarrior();
        RegisterWightDreadwalker();
        RegisterOrcBerserker();
        RegisterOrcGrappleDrill();
        RegisterGreaseTestGrapplers();
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
        RegisterProtectionFromEvilTestCasters();
        RegisterProtectionFromEvilTestMelee();
        RegisterProtectionFromEvilTestControls();
        RegisterWindDispersionTestCasters();
        RegisterObscuringMistRangedOnlyTestNPCs();
        RegisterZombieShambler();
        RegisterTargetDummy();
    }

    private static void Register(NPCDefinition def)
    {
        _npcs[def.Id] = def;
    }

    // Custom creature registrations are split into NPCDatabaseCustom.cs

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
    public MaterialComposition MaterialComposition = MaterialComposition.Organic;
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
    public int RegenerationAmount;
    public DamageBypassTag RegenerationSuppressedBy = DamageBypassTag.None;
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
    /// <summary>Optional explicit enemy character name this NPC should prioritize.</summary>
    public string AITargetPriority;
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
                    IsPrimary = attack.IsPrimary,
                    PoisonOnHitId = attack.PoisonOnHitId
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
                IsPrimary = RakeAttack.IsPrimary,
                PoisonOnHitId = RakeAttack.PoisonOnHitId
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
        clone.RegenerationAmount = RegenerationAmount;
        clone.RegenerationSuppressedBy = RegenerationSuppressedBy;
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