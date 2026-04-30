using System;
using System.Collections.Generic;
using System.Globalization;
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
        RegisterCreatures_E();
        RegisterCreatures_F();
        RegisterCreatures_G();
        RegisterCreatures_H();
        RegisterCreatures_L();
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
            new EncounterPreset("test_2_goblins", "🧪 Test: 2 Goblins", "Quick test encounter - 2 basic goblins for fast combat and loot validation.", new List<string> { "goblin", "goblin" }),
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

    /// <summary>
    /// D&D 3.5e Challenge Rating (CR). Stored as string to preserve fractions
    /// such as "1/8", "1/4", "1/3", "1/2".
    /// </summary>
    public string ChallengeRating;

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

/// <summary>
/// Difficulty labels for DMG-style random encounters.
/// </summary>
public enum RandomEncounterDifficulty
{
    Easy,
    Average,
    Challenging,
    Hard,
    Epic
}

/// <summary>
/// Utility helpers for D&D 3.5e Challenge Rating parsing/formatting and
/// encounter difficulty estimation.
/// </summary>
public static class ChallengeRatingUtils
{
    private static readonly Dictionary<string, float> FractionToValue = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
    {
        { "1/10", 0.1f },
        { "1/8", 0.125f },
        { "1/6", 1f / 6f },
        { "1/4", 0.25f },
        { "1/3", 1f / 3f },
        { "1/2", 0.5f }
    };

    private static readonly List<(float Value, string Label)> DisplayOrder = new List<(float, string)>
    {
        (0.1f, "1/10"),
        (0.125f, "1/8"),
        (1f / 6f, "1/6"),
        (0.25f, "1/4"),
        (1f / 3f, "1/3"),
        (0.5f, "1/2")
    };

    // DMG 3.5e XP table (CR/EL 1-20 + common fractions).
    private static readonly Dictionary<float, int> CrToXp = new Dictionary<float, int>
    {
        { 0.1f, 15 },
        { 0.125f, 25 },
        { 1f / 6f, 35 },
        { 0.25f, 75 },
        { 1f / 3f, 100 },
        { 0.5f, 150 },
        { 1f, 300 },
        { 2f, 600 },
        { 3f, 900 },
        { 4f, 1200 },
        { 5f, 1600 },
        { 6f, 2400 },
        { 7f, 3200 },
        { 8f, 4800 },
        { 9f, 6400 },
        { 10f, 9600 },
        { 11f, 12800 },
        { 12f, 19200 },
        { 13f, 25600 },
        { 14f, 38400 },
        { 15f, 51200 },
        { 16f, 76800 },
        { 17f, 102400 },
        { 18f, 153600 },
        { 19f, 204800 },
        { 20f, 307200 }
    };

    public static bool TryParse(string challengeRating, out float value)
    {
        value = 0f;
        if (string.IsNullOrWhiteSpace(challengeRating))
            return false;

        string normalized = challengeRating.Trim();
        if (FractionToValue.TryGetValue(normalized, out float fractionValue))
        {
            value = fractionValue;
            return true;
        }

        if (float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
        {
            value = Mathf.Max(0f, parsed);
            return true;
        }

        return false;
    }

    public static string Format(string challengeRating)
    {
        if (!TryParse(challengeRating, out float parsed))
            return "—";

        return Format(parsed);
    }

    public static string Format(float value)
    {
        if (value <= 0f)
            return "0";

        for (int i = 0; i < DisplayOrder.Count; i++)
        {
            if (Mathf.Abs(value - DisplayOrder[i].Value) < 0.0001f)
                return DisplayOrder[i].Label;
        }

        if (Mathf.Abs(value - Mathf.Round(value)) < 0.0001f)
            return Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture);

        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    public static int CalculateAPL(IList<int> partyLevels, int partySize)
    {
        if (partyLevels == null || partyLevels.Count == 0)
            return 1;

        int total = 0;
        int counted = 0;
        for (int i = 0; i < partyLevels.Count; i++)
        {
            total += Mathf.Max(1, partyLevels[i]);
            counted++;
        }

        if (counted == 0)
            return 1;

        int apl = Mathf.Max(1, Mathf.RoundToInt((float)total / counted));

        int effectivePartySize = partySize > 0 ? partySize : counted;
        if (effectivePartySize < 4)
            apl = Mathf.Max(1, apl - 1);
        else if (effectivePartySize > 6)
            apl += 1;

        return Mathf.Max(1, apl);
    }

    public static int GetTargetELForDifficulty(int apl, RandomEncounterDifficulty difficulty)
    {
        int clampedApl = Mathf.Max(1, apl);
        int offset = 0;
        switch (difficulty)
        {
            case RandomEncounterDifficulty.Easy:
                offset = -1;
                break;
            case RandomEncounterDifficulty.Average:
                offset = 0;
                break;
            case RandomEncounterDifficulty.Challenging:
                offset = 1;
                break;
            case RandomEncounterDifficulty.Hard:
                offset = 2;
                break;
            case RandomEncounterDifficulty.Epic:
                offset = 3;
                break;
            default:
                offset = 0;
                break;
        }

        return Mathf.Max(1, clampedApl + offset);
    }

    public static int GetXpForCr(float cr)
    {
        if (CrToXp.TryGetValue(cr, out int xp))
            return xp;

        // Fallback for non-table CR values: nearest known CR.
        float nearest = 1f;
        float bestDelta = float.MaxValue;
        foreach (var kv in CrToXp)
        {
            float delta = Mathf.Abs(kv.Key - cr);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                nearest = kv.Key;
            }
        }

        return CrToXp[nearest];
    }

    public static int GetXpForEL(float el)
    {
        return GetXpForCr(el);
    }

    public static float GetEquivalentCrForTotalXp(int totalXp)
    {
        if (totalXp <= 0)
            return 0f;

        float bestCr = 0.5f;
        int bestXp = int.MaxValue;
        foreach (var kv in CrToXp)
        {
            if (kv.Value >= totalXp && kv.Value < bestXp)
            {
                bestXp = kv.Value;
                bestCr = kv.Key;
            }
        }

        if (bestXp == int.MaxValue)
            return 20f;

        return bestCr;
    }

    public static float GetEquivalentELForTotalXp(int totalXp)
    {
        return GetEquivalentCrForTotalXp(totalXp);
    }

    /// <summary>
    /// DMG 3.5 same-CR group rule:
    /// 1 creature = CR
    /// 2 = CR+2, 3-4 = CR+3, 5-8 = CR+4, 9-16 = CR+5, etc.
    /// </summary>
    public static float GetELForSameCrGroup(float creatureCr, int count)
    {
        if (count <= 0)
            return 0f;

        if (count == 1)
            return creatureCr;

        // Equivalent compact form of DMG doubling table.
        int bonus = 1 + Mathf.CeilToInt(Mathf.Log(count, 2f));
        return creatureCr + bonus;
    }

    public static float CalculateEncounterEL(IList<float> creatureCrs)
    {
        if (creatureCrs == null || creatureCrs.Count == 0)
            return 0f;

        bool allSame = true;
        float first = creatureCrs[0];
        for (int i = 1; i < creatureCrs.Count; i++)
        {
            if (Mathf.Abs(creatureCrs[i] - first) > 0.0001f)
            {
                allSame = false;
                break;
            }
        }

        if (allSame)
            return GetELForSameCrGroup(first, creatureCrs.Count);

        int totalXp = 0;
        for (int i = 0; i < creatureCrs.Count; i++)
            totalXp += GetXpForCr(creatureCrs[i]);

        return GetEquivalentELForTotalXp(totalXp);
    }

    public static EncounterDifficultySummary CalculateEncounterDifficulty(EncounterPreset preset, int partyAverageLevel)
    {
        int totalXp = 0;
        int creatureCount = 0;

        if (preset?.NPCIds != null)
        {
            for (int i = 0; i < preset.NPCIds.Count; i++)
            {
                NPCDefinition def = NPCDatabase.Get(preset.NPCIds[i]);
                if (def == null || !TryParse(def.ChallengeRating, out float crValue))
                    continue;

                totalXp += GetXpForCr(crValue);
                creatureCount++;
            }
        }

        float equivalentCr = GetEquivalentCrForTotalXp(totalXp);
        int apl = Mathf.Max(1, partyAverageLevel);
        float delta = equivalentCr - apl;

        string tier;
        if (delta <= -2f) tier = "Easy";
        else if (delta <= -0.5f) tier = "Moderate";
        else if (delta <= 0.5f) tier = "Challenging";
        else if (delta <= 2f) tier = "Hard";
        else tier = "Deadly";

        return new EncounterDifficultySummary
        {
            CreatureCount = creatureCount,
            TotalXp = totalXp,
            EquivalentCR = equivalentCr,
            DifficultyTier = tier,
            PartyAverageLevel = apl
        };
    }
}

public struct EncounterDifficultySummary
{
    public int CreatureCount;
    public int TotalXp;
    public float EquivalentCR;
    public string DifficultyTier;
    public int PartyAverageLevel;

    public string BuildDisplayLine()
    {
        return $"CR {ChallengeRatingUtils.Format(EquivalentCR)} • XP {TotalXp} • {DifficultyTier} vs APL {PartyAverageLevel}";
    }
}
