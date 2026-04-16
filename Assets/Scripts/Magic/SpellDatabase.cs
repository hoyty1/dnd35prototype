// ============================================================================
// SpellDatabase.cs — Core infrastructure for the spell database
// Contains: Init(), GetSpell(), GetAllSpells(), Count, Register() helper
// Registration methods are in separate partial class files by spell level.
// ============================================================================
using System.Collections.Generic;
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
/// This is a partial class — registration methods are split across:
///   SpellDatabase_WizardCantrips.cs
///   SpellDatabase_WizardLevel1.cs
///   SpellDatabase_WizardLevel2.cs
///   SpellDatabase_ClericCantrips.cs
///   SpellDatabase_ClericLevel1.cs
///   SpellDatabase_ClericLevel2.cs
/// </summary>
public static partial class SpellDatabase
{
    private static Dictionary<string, SpellData> _spells;
    private static bool _initialized;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;
        _spells = new Dictionary<string, SpellData>();

        // ================================================================
        //  WIZARD CANTRIPS (Level 0)  — PHB p.192-196
        // ================================================================
        RegisterWizardCantrips();

        // ================================================================
        //  WIZARD 1ST LEVEL SPELLS  — PHB p.196-230 (approx)
        // ================================================================
        RegisterWizard1stLevel();

        // ================================================================
        //  WIZARD 2ND LEVEL SPELLS  — PHB p.230-260 (approx)
        // ================================================================
        RegisterWizard2ndLevel();

        // ================================================================
        //  CLERIC CANTRIPS (Level 0 Orisons)  — PHB p.183-184
        // ================================================================
        RegisterClericCantrips();

        // ================================================================
        //  CLERIC 1ST LEVEL SPELLS  — PHB p.184-210 (approx)
        // ================================================================
        RegisterCleric1stLevel();

        // ================================================================
        //  CLERIC 2ND LEVEL SPELLS  — PHB p.210-230 (approx)
        // ================================================================
        RegisterCleric2ndLevel();

        // ================================================================
        //  DOMAIN-SPECIFIC SPELLS  — spells that only appear on domain lists
        // ================================================================
        RegisterDomainSpells();

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
        _spells[spell.SpellId] = spell;
    }

    /// <summary>Get a spell by ID. Returns null if not found.</summary>
    public static SpellData GetSpell(string spellId)
    {
        Init();
        if (_spells.TryGetValue(spellId, out SpellData spell))
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
    /// <summary>Get all spells available to a specific class.</summary>
    public static List<SpellData> GetSpellsForClass(string className)
    {
        Init();
        var result = new List<SpellData>();
        foreach (var spell in _spells.Values)
        {
            foreach (string cls in spell.ClassList)
            {
                if (cls == className)
                {
                    result.Add(spell);
                    break;
                }
            }
        }
        return result;
    }

    /// <summary>Get all spells of a specific level for a class.</summary>
    public static List<SpellData> GetSpellsForClassAtLevel(string className, int spellLevel)
    {
        Init();
        var result = new List<SpellData>();
        foreach (var spell in _spells.Values)
        {
            if (spell.SpellLevel != spellLevel) continue;
            foreach (string cls in spell.ClassList)
            {
                if (cls == className)
                {
                    result.Add(spell);
                    break;
                }
            }
        }
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