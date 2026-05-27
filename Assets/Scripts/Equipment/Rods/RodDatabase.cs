using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ════════════════════════════════════════════════════════════════════════
//  D&D 3.5e Rod Database (DMG pp. 224–228)
//
//  Static registry for all 36 DMG rods. Follows the same lazy
//  initialization pattern as WondrousItemDatabase and RingDatabase.
//
//  Lifecycle:
//  1. RodDatabase.Init() — Create all rod definitions
//  2. RodDatabase.RegisterAllInItemDatabase() — Register in main ItemDatabase
//  3. Access via GetRod(id), GetAllRods(), GetMetamagicRods(), etc.
// ════════════════════════════════════════════════════════════════════════

public static class RodDatabase
{
    private static bool _initialized = false;
    private static Dictionary<string, ItemData> _rods = new Dictionary<string, ItemData>();

    // ── Initialization ────────────────────────────────────────

    /// <summary>
    /// Initialize the rod database. Idempotent — safe to call multiple times.
    /// Must be called before ItemDatabase.Init() so rods are available for registration.
    /// </summary>
    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;
        _rods.Clear();

        var allRods = RodFactory.CreateAllRods();
        foreach (var rod in allRods)
        {
            Register(rod);
        }

        Debug.Log($"[RodDatabase] Initialized with {_rods.Count} rods " +
                  $"({allRods.Count(r => r.RodIsMetamagic)} metamagic, " +
                  $"{allRods.Count(r => !r.RodIsMetamagic)} non-metamagic)");
    }

    /// <summary>
    /// Register a single rod in the database. Duplicate IDs are rejected with a warning.
    /// </summary>
    private static void Register(ItemData rod)
    {
        if (rod == null || string.IsNullOrEmpty(rod.RodId))
        {
            Debug.LogWarning("[RodDatabase] Attempted to register null/empty rod");
            return;
        }

        if (_rods.ContainsKey(rod.RodId))
        {
            Debug.LogWarning($"[RodDatabase] Duplicate rod ID: {rod.RodId} — skipping");
            return;
        }

        _rods[rod.RodId] = rod;
    }

    /// <summary>
    /// Register all rods in the main ItemDatabase for standard inventory/shop flow.
    /// Called after ItemDatabase.Init() during SceneBootstrap.
    /// </summary>
    public static void RegisterAllInItemDatabase()
    {
        if (!_initialized) Init();

        int count = 0;
        foreach (var rod in _rods.Values)
        {
            ItemDatabase.RegisterExternal(rod);
            count++;
        }

        Debug.Log($"[RodDatabase] Registered {count} rods in ItemDatabase");
    }

    // ── Queries ───────────────────────────────────────────────

    /// <summary>Get a rod by its unique ID. Returns null if not found.</summary>
    public static ItemData GetRod(string rodId)
    {
        if (!_initialized) Init();
        return _rods.TryGetValue(rodId, out var rod) ? rod : null;
    }

    /// <summary>Get all registered rods.</summary>
    public static IEnumerable<ItemData> GetAllRods()
    {
        if (!_initialized) Init();
        return _rods.Values;
    }

    /// <summary>Get all metamagic rods (21 total).</summary>
    public static IEnumerable<ItemData> GetMetamagicRods()
    {
        if (!_initialized) Init();
        return _rods.Values.Where(r => r.RodIsMetamagic);
    }

    /// <summary>Get all non-metamagic rods (combat + utility + legendary).</summary>
    public static IEnumerable<ItemData> GetNonMetamagicRods()
    {
        if (!_initialized) Init();
        return _rods.Values.Where(r => !r.RodIsMetamagic);
    }

    /// <summary>Get rods by category.</summary>
    public static IEnumerable<ItemData> GetRodsByCategory(RodCategory category)
    {
        if (!_initialized) Init();
        return _rods.Values.Where(r => r.RodCategory == category);
    }

    /// <summary>Get metamagic rods of a specific type (all 3 power levels).</summary>
    public static IEnumerable<ItemData> GetMetamagicRodsByType(MetamagicFeatId metamagicType)
    {
        if (!_initialized) Init();
        return _rods.Values.Where(r => r.RodIsMetamagic && r.RodMetamagicType == metamagicType);
    }

    /// <summary>Get metamagic rods of a specific power level.</summary>
    public static IEnumerable<ItemData> GetMetamagicRodsByPower(RodPowerLevel power)
    {
        if (!_initialized) Init();
        return _rods.Values.Where(r => r.RodIsMetamagic && r.RodPower == power);
    }

    /// <summary>Get legendary rods.</summary>
    public static IEnumerable<ItemData> GetLegendaryRods()
    {
        if (!_initialized) Init();
        return _rods.Values.Where(r => r.IsLegendary);
    }

    /// <summary>Total number of registered rods.</summary>
    public static int Count
    {
        get
        {
            if (!_initialized) Init();
            return _rods.Count;
        }
    }

    /// <summary>Check if a rod ID is registered.</summary>
    public static bool HasRod(string rodId)
    {
        if (!_initialized) Init();
        return _rods.ContainsKey(rodId);
    }

    // ── Daily Reset ───────────────────────────────────────────

    /// <summary>
    /// Reset daily/weekly uses for all rod instances in party inventories.
    /// Called during rest handler alongside WondrousItemActivation.OnRest().
    /// </summary>
    public static void ResetDailyUses(List<ItemData> rodItems)
    {
        if (rodItems == null) return;

        foreach (var rod in rodItems)
        {
            if (rod == null || !rod.IsRod) continue;

            // Reset daily uses for metamagic rods
            rod.RodUsesToday = 0;

            // Reset daily uses for specific rods
            rod.RodFearUsesToday = 0;
            rod.RodAnimateUsesToday = 0;
            rod.RodPrayerUsesToday = 0;
            rod.RodGreaterDispelUsesToday = 0;
            rod.RodSplendorFeastUsesToday = 0;
        }
    }

    /// <summary>
    /// Reset weekly uses for rod instances (called on weekly rest cycle).
    /// </summary>
    public static void ResetWeeklyUses(List<ItemData> rodItems)
    {
        if (rodItems == null) return;

        foreach (var rod in rodItems)
        {
            if (rod == null || !rod.IsRod) continue;

            rod.RodSplendorTentUsesThisWeek = 0;
            rod.RodSplendorClothesThisWeek = 0;
            rod.RodDemiplaneUsesThisWeek = 0;
        }
    }
}
