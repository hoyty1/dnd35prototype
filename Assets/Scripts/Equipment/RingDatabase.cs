using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Static registry for all D&D 3.5e magic rings (DMG pp. 229–233).
/// Follows the same lazy initialization pattern as StaffDatabase.
/// 
/// Sprint 1 registers all Tier 1 passive rings (15 types, 36 variants).
/// Future sprints will add Tier 2–4 active/complex rings.
/// 
/// Ring items are stored here AND registered in ItemDatabase for
/// standard inventory/equipment flow.
/// </summary>
public static class RingDatabase
{
    private static bool _initialized = false;
    private static Dictionary<string, ItemData> _rings = new Dictionary<string, ItemData>();

    /// <summary>
    /// Initialize the ring database. Idempotent — safe to call multiple times.
    /// Must be called before ItemDatabase.Init() so rings are available for registration.
    /// </summary>
    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;
        _rings.Clear();

        // ════════════════════════════════════════════════════════════
        //  TIER 1: Passive Stat Rings (Sprint 1)
        // ════════════════════════════════════════════════════════════

        // --- Protection Rings (+1 to +5 deflection bonus to AC) ---
        for (int bonus = 1; bonus <= 5; bonus++)
            Register(RingFactory.CreateProtectionRing(bonus));

        // --- Resistance Rings (+1 to +5 resistance bonus to all saves) ---
        for (int bonus = 1; bonus <= 5; bonus++)
            Register(RingFactory.CreateResistanceRing(bonus));

        // --- Energy Resistance Rings (5 types × 3 tiers = 15 variants) ---
        string[] energyTypes = { "Acid", "Cold", "Electricity", "Fire", "Sonic" };
        int[] resistAmounts = { 10, 20, 30 }; // Minor, Major, Greater
        foreach (string energyType in energyTypes)
        {
            foreach (int amount in resistAmounts)
                Register(RingFactory.CreateEnergyResistanceRing(energyType, amount));
        }

        // --- Special Ability Rings ---
        Register(RingFactory.CreateForceShieldRing());
        Register(RingFactory.CreateEvasionRing());
        Register(RingFactory.CreateFreedomOfMovementRing());
        Register(RingFactory.CreateFeatherFallingRing());

        // --- Skill Bonus Rings ---
        Register(RingFactory.CreateSwimmingRing());
        Register(RingFactory.CreateClimbingRing());
        Register(RingFactory.CreateJumpingRing());

        // --- Utility Rings ---
        Register(RingFactory.CreateWaterWalkingRing());
        Register(RingFactory.CreateSustenanceRing());
        Register(RingFactory.CreateMindShieldingRing());
        Register(RingFactory.CreateWarmthRing());
        Register(RingFactory.CreateChameleonPowerRing());

        Debug.Log($"[RingDatabase] Initialized: {_rings.Count} rings registered (Sprint 1 — Tier 1 passive rings).");
    }

    /// <summary>Register a ring in the database. Validates for duplicates.</summary>
    private static void Register(ItemData ring)
    {
        if (ring == null)
        {
            Debug.LogWarning("[RingDatabase] Attempted to register null ring.");
            return;
        }

        string key = ring.RingId;
        if (string.IsNullOrEmpty(key))
            key = ring.Id;

        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning($"[RingDatabase] Ring '{ring.Name}' has no RingId or Id — skipping.");
            return;
        }

        if (_rings.ContainsKey(key))
        {
            Debug.LogWarning($"[RingDatabase] Duplicate ring ID: '{key}' — skipping duplicate '{ring.Name}'.");
            return;
        }

        _rings[key] = ring;
    }

    /// <summary>
    /// Retrieve a ring definition by its ID.
    /// Returns null if not found.
    /// </summary>
    public static ItemData GetRing(string ringId)
    {
        if (!_initialized) Init();
        if (string.IsNullOrEmpty(ringId)) return null;
        return _rings.ContainsKey(ringId) ? _rings[ringId] : null;
    }

    /// <summary>Get all registered ring definitions (read-only snapshot).</summary>
    public static IReadOnlyDictionary<string, ItemData> GetAllRings()
    {
        if (!_initialized) Init();
        return _rings;
    }

    /// <summary>Get the count of registered rings.</summary>
    public static int Count
    {
        get
        {
            if (!_initialized) Init();
            return _rings.Count;
        }
    }

    /// <summary>
    /// Register all rings into the main ItemDatabase so they appear in
    /// standard inventory/loot/shop systems. Call after ItemDatabase.Init().
    /// </summary>
    public static void RegisterAllRingsInItemDatabase()
    {
        if (!_initialized) Init();

        int registered = 0;
        foreach (var kvp in _rings)
        {
            ItemData existing = ItemDatabase.Get(kvp.Key);
            if (existing == null)
            {
                ItemDatabase.RegisterExternal(kvp.Value);
                registered++;
            }
        }

        Debug.Log($"[RingDatabase] Registered {registered} rings in ItemDatabase.");
    }
}
