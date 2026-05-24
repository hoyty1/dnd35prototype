// ============================================================================
// SpellDatabase.cs — Core infrastructure for the spell database
// Contains: Init(), GetSpell(), GetAllSpells(), Count, Register() helper
// Registration methods are in separate partial class files by starting letter.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Static database of all spells in the game.
/// D&D 3.5e PHB spell definitions for Wizard and Cleric (levels 0-2).
///
/// Spell Status Key:
///   FUNCTIONAL = Full mechanics implemented (damage, healing, buffs, saves)
///   PLACEHOLDER = Description only, mechanics not yet implemented
///
/// Total spells: ~140 (Wizard 0/1/2 + Cleric 0/1/2)
/// Functional: ~60-70% (combat-relevant spells)
/// Placeholder: ~30-40% (summoning, illusions, complex utility)
///
/// This is a partial class — registration methods are split across alphabetical files:
///   SpellDatabase_A.cs ... SpellDatabase_Z.cs (as needed)
/// </summary>
public static partial class SpellDatabase
{
    private static Dictionary<string, SpellData> _spells;
    private static Dictionary<string, string> _spellAliases;
    private static bool _initialized;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;
        _spells = new Dictionary<string, SpellData>();
        _spellAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);


        // ================================================================
        //  SPELL REGISTRATION (ALPHABETICAL FILES)
        // ================================================================
        RegisterSpellsA();
        RegisterSpellsB();
        RegisterSpellsC();
        RegisterSpellsD();
        RegisterSpellsE();
        RegisterSpellsF();
        RegisterSpellsG();
        RegisterSpellsH();
        RegisterSpellsI();
        RegisterSpellsJ();
        RegisterSpellsK();
        RegisterSpellsL();
        RegisterSpellsM();
        RegisterSpellsN();
        RegisterSpellsO();
        RegisterSpellsP();
        RegisterSpellsR();
        RegisterSpellsS();
        RegisterSpellsT();
        RegisterSpellsU();
        RegisterSpellsV();
        RegisterSpellsW();
        RegisterSpellsZ();

        AnnotateDomainAvailabilityFromDomainDatabase();
        AnnotateSpellDescriptors();

        // Initialize the spell component system (tracks costly material components)
        SpellComponentRegistry.Init();

        // Reclassify spells into expanded effect types for AI tactical awareness
        SpellCategoryClassifier.ReclassifyAll();

        int total = _spells.Count;
        int functional = 0;
        int placeholder = 0;
        foreach (var s in _spells.Values)
        {
            if (s.IsPlaceholder) placeholder++;
            else functional++;
        }
        Debug.Log($"[SpellDatabase] Initialized with {total} spells ({functional} functional, {placeholder} placeholders).");
    }

    private static void Register(SpellData spell)
    {
        if (spell == null || string.IsNullOrWhiteSpace(spell.SpellId))
        {
            Debug.LogWarning("[SpellDatabase] Attempted to register a null/invalid spell.");
            return;
        }

        spell.EnsureAvailabilityFromLegacyClassList();
        _spells[spell.SpellId] = spell;
    }

    private static void RegisterAlias(string aliasSpellId, string canonicalSpellId)
    {
        if (string.IsNullOrWhiteSpace(aliasSpellId) || string.IsNullOrWhiteSpace(canonicalSpellId))
            return;

        if (string.Equals(aliasSpellId, canonicalSpellId, StringComparison.OrdinalIgnoreCase))
            return;

        _spellAliases[aliasSpellId] = canonicalSpellId;
    }

    private static void RegisterClassSpellAlias(string aliasSpellId, string canonicalSpellId, string className, int spellLevel, string domain = null)
    {
        RegisterAlias(aliasSpellId, canonicalSpellId);

        if (_spells.TryGetValue(canonicalSpellId, out SpellData canonicalSpell))
        {
            canonicalSpell.AddAvailability(className, spellLevel, domain);
        }
        else
        {
            Debug.LogWarning($"[SpellDatabase] Could not register alias '{aliasSpellId}' -> '{canonicalSpellId}' because canonical spell is missing.");
        }
    }

    private static void AnnotateDomainAvailabilityFromDomainDatabase()
    {
        DomainDatabase.Init();
        List<DomainData> domains = DomainDatabase.GetAllDomains();
        foreach (DomainData domain in domains)
        {
            if (domain?.DomainSpells == null)
                continue;

            foreach (KeyValuePair<int, string> entry in domain.DomainSpells)
            {
                SpellData spell = GetSpell(entry.Value);
                if (spell == null)
                    continue;

                spell.AddAvailability("Cleric", entry.Key, domain.Name);
            }
        }
    }

    /// <summary>
    /// Annotate spells with D&D 3.5e descriptors used for domain caster level bonuses.
    /// Auto-detects from school/properties, then applies explicit overrides per PHB.
    /// </summary>
    private static void AnnotateSpellDescriptors()
    {
        // === Pass 1: Auto-detect descriptors from spell properties ===
        foreach (var spell in _spells.Values)
        {
            if (spell == null) continue;

            // Divination school → Divination descriptor (for Knowledge domain +1 CL)
            if (string.Equals(spell.School, "Divination", StringComparison.OrdinalIgnoreCase))
                spell.Descriptors |= SpellDescriptor.Divination;

            // Healing subschool: Conjuration(Healing) spells that heal HP
            if (spell.EffectType == SpellEffectType.Healing &&
                string.Equals(spell.School, "Conjuration", StringComparison.OrdinalIgnoreCase))
                spell.Descriptors |= SpellDescriptor.Healing;

            // Fire damage type
            if (!string.IsNullOrEmpty(spell.DamageType))
            {
                string dt = spell.DamageType.ToLowerInvariant();
                if (dt.Contains("fire")) spell.Descriptors |= SpellDescriptor.Fire;
                if (dt.Contains("cold")) spell.Descriptors |= SpellDescriptor.Cold;
                if (dt.Contains("acid")) spell.Descriptors |= SpellDescriptor.Acid;
                if (dt.Contains("electric") || dt.Contains("lightning")) spell.Descriptors |= SpellDescriptor.Electricity;
                if (dt.Contains("sonic")) spell.Descriptors |= SpellDescriptor.Sonic;
                if (dt.Contains("force")) spell.Descriptors |= SpellDescriptor.Force;
            }
        }

        // === Pass 2: Explicit [Good] descriptor spells (PHB) ===
        SetDescriptor(SpellNames.BLESS, SpellDescriptor.Good);
        SetDescriptor(SpellNames.CONSECRATE, SpellDescriptor.Good);
        SetDescriptor(SpellNames.HOLY_SMITE, SpellDescriptor.Good);
        SetDescriptor(SpellNames.PROTECTION_FROM_EVIL, SpellDescriptor.Good);
        SetDescriptor("magic_circle_vs_evil", SpellDescriptor.Good);
        SetDescriptor("dispel_evil", SpellDescriptor.Good);
        SetDescriptor("holy_aura", SpellDescriptor.Good);
        SetDescriptor("holy_word", SpellDescriptor.Good);
        SetDescriptor(SpellNames.AID, SpellDescriptor.Good);

        // === [Evil] descriptor spells ===
        SetDescriptor(SpellNames.BANE, SpellDescriptor.Evil);
        SetDescriptor(SpellNames.DOMAIN_DESECRATE, SpellDescriptor.Evil);
        SetDescriptor("desecrate", SpellDescriptor.Evil);
        SetDescriptor("unholy_blight", SpellDescriptor.Evil);
        SetDescriptor(SpellNames.DEATH_KNELL, SpellDescriptor.Evil | SpellDescriptor.Death);
        SetDescriptor(SpellNames.CONTAGION, SpellDescriptor.Evil);
        SetDescriptor("animate_dead", SpellDescriptor.Evil);
        SetDescriptor("create_undead", SpellDescriptor.Evil);
        SetDescriptor("unholy_aura", SpellDescriptor.Evil);
        SetDescriptor("blasphemy", SpellDescriptor.Evil);
        // Fix: Protection from Good has [Evil] descriptor per PHB
        SetDescriptor(SpellNames.PROTECTION_FROM_GOOD, SpellDescriptor.Evil);
        SetDescriptor("magic_circle_vs_good", SpellDescriptor.Evil);
        SetDescriptor("dispel_good", SpellDescriptor.Evil);

        // === [Lawful] descriptor spells ===
        SetDescriptor("order_wrath", SpellDescriptor.Lawful);
        SetDescriptor("orders_wrath", SpellDescriptor.Lawful);
        SetDescriptor(SpellNames.PROTECTION_FROM_CHAOS, SpellDescriptor.Lawful);
        SetDescriptor("magic_circle_vs_chaos", SpellDescriptor.Lawful);
        SetDescriptor("dispel_chaos", SpellDescriptor.Lawful);
        SetDescriptor("shield_of_law", SpellDescriptor.Lawful);
        SetDescriptor("dictum", SpellDescriptor.Lawful);

        // === [Chaotic] descriptor spells ===
        SetDescriptor(SpellNames.CHAOS_HAMMER, SpellDescriptor.Chaotic);
        SetDescriptor(SpellNames.PROTECTION_FROM_LAW, SpellDescriptor.Chaotic);
        SetDescriptor("magic_circle_vs_law", SpellDescriptor.Chaotic);
        SetDescriptor("dispel_law", SpellDescriptor.Chaotic);
        SetDescriptor("cloak_of_chaos", SpellDescriptor.Chaotic);
        SetDescriptor("word_of_chaos", SpellDescriptor.Chaotic);

        // === [Healing] descriptor — explicit cure spells ===
        SetDescriptor(SpellNames.CURE_MINOR_WOUNDS, SpellDescriptor.Healing);
        SetDescriptor(SpellNames.CURE_LIGHT_WOUNDS, SpellDescriptor.Healing);
        SetDescriptor(SpellNames.CURE_MODERATE_WOUNDS, SpellDescriptor.Healing);
        SetDescriptor(SpellNames.CURE_SERIOUS_WOUNDS, SpellDescriptor.Healing);
        SetDescriptor(SpellNames.CURE_CRITICAL_WOUNDS, SpellDescriptor.Healing);
        SetDescriptor("mass_cure_light_wounds", SpellDescriptor.Healing);
        SetDescriptor("mass_cure_moderate_wounds", SpellDescriptor.Healing);
        SetDescriptor("mass_cure_serious_wounds", SpellDescriptor.Healing);
        SetDescriptor("mass_cure_critical_wounds", SpellDescriptor.Healing);
        SetDescriptor("heal", SpellDescriptor.Healing);
        SetDescriptor("mass_heal", SpellDescriptor.Healing);
        SetDescriptor("remove_blindness_deafness", SpellDescriptor.Healing);
        SetDescriptor(SpellNames.REMOVE_DISEASE, SpellDescriptor.Healing);
        SetDescriptor("remove_paralysis", SpellDescriptor.Healing);
        SetDescriptor("lesser_restoration", SpellDescriptor.Healing);
        SetDescriptor("restoration_lesser", SpellDescriptor.Healing);
        SetDescriptor(SpellNames.NEUTRALIZE_POISON, SpellDescriptor.Healing);
        SetDescriptor("restoration", SpellDescriptor.Healing);
        SetDescriptor("regenerate", SpellDescriptor.Healing);

        // === [Death] descriptor ===
        SetDescriptor("slay_living", SpellDescriptor.Death);
        SetDescriptor("finger_of_death", SpellDescriptor.Death);
        SetDescriptor("destruction", SpellDescriptor.Death);
        SetDescriptor("wail_of_the_banshee", SpellDescriptor.Death);
        SetDescriptor(SpellNames.CAUSE_FEAR, SpellDescriptor.Fear | SpellDescriptor.MindAffecting);

        // === [Fear] / [Mind-Affecting] ===
        SetDescriptor(SpellNames.DOOM, SpellDescriptor.Fear | SpellDescriptor.MindAffecting);
        SetDescriptor(SpellNames.COMMAND, SpellDescriptor.MindAffecting);
        SetDescriptor(SpellNames.HOLD_PERSON, SpellDescriptor.MindAffecting);
        SetDescriptor(SpellNames.CHARM_PERSON, SpellDescriptor.MindAffecting);
        SetDescriptor(SpellNames.CONFUSION, SpellDescriptor.MindAffecting);
        SetDescriptor(SpellNames.SLEEP, SpellDescriptor.MindAffecting);
        SetDescriptor(SpellNames.DAZE, SpellDescriptor.MindAffecting);
        SetDescriptor(SpellNames.DAZE_MONSTER, SpellDescriptor.MindAffecting);

        // === [Light] ===
        SetDescriptor(SpellNames.CONTINUAL_FLAME, SpellDescriptor.Light);
        SetDescriptor(SpellNames.FLARE, SpellDescriptor.Light);
        SetDescriptor(SpellNames.FLAME_STRIKE, SpellDescriptor.Good); // Flame Strike is [Good] (divine fire)

        // === [Darkness] ===
        SetDescriptor(SpellNames.DARKNESS, SpellDescriptor.Darkness);

        int annotated = 0;
        foreach (var spell in _spells.Values)
        {
            if (spell != null && spell.Descriptors != SpellDescriptor.None)
                annotated++;
        }
        Debug.Log($"[SpellDatabase] Annotated {annotated} spells with descriptors.");
    }

    /// <summary>Add a descriptor to a spell by ID (safe — no-op if spell doesn't exist).</summary>
    private static void SetDescriptor(string spellId, SpellDescriptor descriptor)
    {
        if (string.IsNullOrEmpty(spellId)) return;
        if (_spells.TryGetValue(spellId, out SpellData spell))
            spell.Descriptors |= descriptor;
        // Silently skip if spell doesn't exist — some may not be implemented yet
    }

    /// <summary>Get a spell by ID. Returns null if not found.</summary>
    public static SpellData GetSpell(string spellId)
    {
        Init();

        if (string.IsNullOrWhiteSpace(spellId))
            return null;

        if (_spells.TryGetValue(spellId, out SpellData spell))
            return spell;

        if (_spellAliases.TryGetValue(spellId, out string canonicalId) &&
            _spells.TryGetValue(canonicalId, out spell))
            return spell;

        Debug.LogWarning($"[SpellDatabase] Spell not found: {spellId}");
        return null;
    }

    /// <summary>Get a spell by display name (case-insensitive). Returns null if not found.</summary>
    public static SpellData GetSpellByName(string spellName)
    {
        Init();
        if (string.IsNullOrWhiteSpace(spellName))
            return null;

        foreach (var spell in _spells.Values)
        {
            if (string.Equals(spell.Name, spellName, System.StringComparison.OrdinalIgnoreCase))
                return spell;
        }

        Debug.LogWarning($"[SpellDatabase] Spell not found by name: {spellName}");
        return null;
    }
    private static bool SpellMatchesClass(SpellData spell, string className)
    {
        if (spell == null || string.IsNullOrWhiteSpace(className))
            return false;

        if (spell.AvailableFor != null && spell.AvailableFor.Count > 0)
        {
            return spell.AvailableFor.Any(a =>
                a != null &&
                a.MatchesClass(className) &&
                string.IsNullOrWhiteSpace(a.Domain));
        }

        if (spell.ClassList == null)
            return false;

        for (int i = 0; i < spell.ClassList.Length; i++)
        {
            string cls = spell.ClassList[i];
            if (string.Equals(cls, className, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>Get all spells available to a specific class.</summary>
    public static List<SpellData> GetSpellsForClass(string className)
    {
        Init();

        var result = new List<SpellData>();
        foreach (var spell in _spells.Values)
        {
            if (SpellMatchesClass(spell, className))
                result.Add(spell);
        }

        result.Sort((a, b) =>
        {
            int aLevel = a.GetSpellLevelFor(className);
            int bLevel = b.GetSpellLevelFor(className);
            int levelCmp = aLevel.CompareTo(bLevel);
            if (levelCmp != 0) return levelCmp;
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        return result;
    }

    /// <summary>Get all non-placeholder spells available to a specific class.</summary>
    public static List<SpellData> GetImplementedSpellsForClass(string className)
    {
        var result = GetSpellsForClass(className);
        result.RemoveAll(spell => spell == null || spell.IsPlaceholder);
        return result;
    }

    /// <summary>Get all spells of a specific level for a class.</summary>
    public static List<SpellData> GetSpellsForClassAtLevel(string className, int spellLevel)
    {
        Init();
        var result = new List<SpellData>();
        foreach (var spell in _spells.Values)
        {
            if (spell != null && spell.IsAvailableFor(className, spellLevel))
                result.Add(spell);
        }

        result.Sort((a, b) => string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase));
        return result;
    }

    /// <summary>Compatibility alias used by some UIs.</summary>
    public static List<SpellData> GetSpellsByLevelAndClass(int spellLevel, string className)
    {
        return GetSpellsForClassAtLevel(className, spellLevel);
    }

    /// <summary>Get all spells for a specific domain at a spell level.</summary>
    public static List<SpellData> GetDomainSpells(string domainName, int spellLevel)
    {
        Init();

        var result = new List<SpellData>();
        foreach (SpellData spell in _spells.Values)
        {
            if (spell == null || spell.AvailableFor == null)
                continue;

            bool matches = spell.AvailableFor.Any(a =>
                a != null &&
                a.MatchesClass("Cleric") &&
                a.Level == spellLevel &&
                a.MatchesDomain(domainName));

            if (matches)
                result.Add(spell);
        }

        result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    /// <summary>Returns true if a spell belongs to a domain spell list.</summary>
    public static bool IsSpellInDomain(string spellIdOrName, string domainName)
    {
        if (string.IsNullOrWhiteSpace(spellIdOrName) || string.IsNullOrWhiteSpace(domainName))
            return false;

        SpellData spell = GetSpell(spellIdOrName) ?? GetSpellByName(spellIdOrName);
        if (spell == null || spell.AvailableFor == null)
            return false;

        return spell.AvailableFor.Any(a =>
            a != null &&
            a.MatchesClass("Cleric") &&
            a.MatchesDomain(domainName));
    }

    /// <summary>Get all non-placeholder spells of a specific level for a class.</summary>
    public static List<SpellData> GetImplementedSpellsForClassAtLevel(string className, int spellLevel)
    {
        var result = GetSpellsForClassAtLevel(className, spellLevel);
        result.RemoveAll(spell => spell == null || spell.IsPlaceholder);
        return result;
    }

    /// <summary>Get all registered spells.</summary>
    public static List<SpellData> GetAllSpells()
    {
        Init();
        return new List<SpellData>(_spells.Values);
    }

    /// <summary>Get count of all registered spells.</summary>
    public static int Count
    {
        get
        {
            Init();
            return _spells.Count;
        }
    }
}