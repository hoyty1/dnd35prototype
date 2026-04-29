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