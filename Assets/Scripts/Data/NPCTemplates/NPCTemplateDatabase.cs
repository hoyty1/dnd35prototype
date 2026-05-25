using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Database manager for NPC stat block templates (D&D 3.5e DMG Chapter 4).
/// Provides fast lookup by class/level, CR, or class name.
/// Contains 70 templates: 55 PHB class + 15 NPC class.
/// </summary>
public static class NPCTemplateDatabase
{
    private static Dictionary<string, NPCTemplate> _templatesByKey = new Dictionary<string, NPCTemplate>();
    private static List<NPCTemplate> _allTemplates = new List<NPCTemplate>();
    private static Dictionary<string, List<NPCTemplate>> _templatesByClass = new Dictionary<string, List<NPCTemplate>>();
    private static Dictionary<int, List<NPCTemplate>> _templatesByCR = new Dictionary<int, List<NPCTemplate>>();
    private static bool _initialized;

    /// <summary>Initialize the template database. Safe to call multiple times.</summary>
    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        _allTemplates = TemplateData.GetAllTemplates();
        _templatesByKey.Clear();
        _templatesByClass.Clear();
        _templatesByCR.Clear();

        foreach (var tmpl in _allTemplates)
        {
            // Key lookup: "Fighter_10"
            string key = tmpl.Key;
            if (!_templatesByKey.ContainsKey(key))
                _templatesByKey[key] = tmpl;

            // Class lookup
            if (!_templatesByClass.ContainsKey(tmpl.ClassName))
                _templatesByClass[tmpl.ClassName] = new List<NPCTemplate>();
            _templatesByClass[tmpl.ClassName].Add(tmpl);

            // CR lookup
            if (!_templatesByCR.ContainsKey(tmpl.ChallengeRating))
                _templatesByCR[tmpl.ChallengeRating] = new List<NPCTemplate>();
            _templatesByCR[tmpl.ChallengeRating].Add(tmpl);
        }

        Debug.Log($"[NPCTemplateDatabase] Initialized with {_allTemplates.Count} templates across {_templatesByClass.Count} classes");
    }

    /// <summary>Get all templates.</summary>
    public static List<NPCTemplate> GetAllTemplates()
    {
        Init();
        return new List<NPCTemplate>(_allTemplates);
    }

    /// <summary>Total template count.</summary>
    public static int Count
    {
        get { Init(); return _allTemplates.Count; }
    }

    /// <summary>
    /// Get a specific template by class name and level.
    /// Returns null if no template exists for that combination.
    /// </summary>
    public static NPCTemplate GetTemplate(string className, int level)
    {
        Init();
        string key = $"{className}_{level}";
        if (_templatesByKey.TryGetValue(key, out var tmpl))
            return tmpl;
        return null;
    }

    /// <summary>
    /// Get all templates for a given class name.
    /// Returns empty list if class not found.
    /// </summary>
    public static List<NPCTemplate> GetAllTemplatesForClass(string className)
    {
        Init();
        if (_templatesByClass.TryGetValue(className, out var list))
            return new List<NPCTemplate>(list);
        return new List<NPCTemplate>();
    }

    /// <summary>
    /// Get all templates matching a specific CR.
    /// Returns empty list if no templates match.
    /// </summary>
    public static List<NPCTemplate> GetTemplatesForCR(int cr)
    {
        Init();
        if (_templatesByCR.TryGetValue(cr, out var list))
            return new List<NPCTemplate>(list);
        return new List<NPCTemplate>();
    }

    /// <summary>
    /// Get a random template matching a specific CR.
    /// Returns null if no templates match.
    /// </summary>
    public static NPCTemplate GetRandomTemplateForCR(int cr)
    {
        var candidates = GetTemplatesForCR(cr);
        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }

    /// <summary>
    /// Get the nearest template for a class at the given level.
    /// If exact match doesn't exist, returns the closest lower-level template.
    /// Returns null if no templates for this class.
    /// </summary>
    public static NPCTemplate GetNearestTemplate(string className, int level)
    {
        Init();
        // Try exact match first
        var exact = GetTemplate(className, level);
        if (exact != null) return exact;

        // Find closest lower level
        if (!_templatesByClass.TryGetValue(className, out var classTmpls)) return null;

        NPCTemplate best = null;
        int bestDist = int.MaxValue;
        foreach (var t in classTmpls)
        {
            int dist = Mathf.Abs(t.Level - level);
            if (dist < bestDist || (dist == bestDist && t.Level < level))
            {
                best = t;
                bestDist = dist;
            }
        }
        return best;
    }

    /// <summary>Get all distinct class names that have templates.</summary>
    public static List<string> GetAllClassNames()
    {
        Init();
        return new List<string>(_templatesByClass.Keys);
    }

    /// <summary>Get all distinct CRs that have templates.</summary>
    public static List<int> GetAllCRs()
    {
        Init();
        var crs = new List<int>(_templatesByCR.Keys);
        crs.Sort();
        return crs;
    }
}
