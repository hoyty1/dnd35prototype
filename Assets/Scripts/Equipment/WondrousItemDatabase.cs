using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Static registry for all D&D 3.5e wondrous items (DMG pp. 248–271).
/// Follows the same lazy initialization pattern as RingDatabase.
/// 
/// Phase 1 registers foundation items from all slot categories plus test items.
/// Future phases will add the remaining 100+ wondrous items.
/// 
/// Wondrous items are stored here AND registered in ItemDatabase for
/// standard inventory/equipment/loot/shop flow.
/// </summary>
public static class WondrousItemDatabase
{
    private static bool _initialized = false;
    private static Dictionary<string, ItemData> _items = new Dictionary<string, ItemData>();

    /// <summary>
    /// Initialize the wondrous item database. Idempotent — safe to call multiple times.
    /// Must be called before ItemDatabase.Init() so items are available for registration.
    /// </summary>
    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;
        _items.Clear();

        // ════════════════════════════════════════════════════════════
        //  HEAD SLOT — Headbands, circlets, helms, hats
        // ════════════════════════════════════════════════════════════
        for (int bonus = 2; bonus <= 6; bonus += 2)
            Register(WondrousItemFactory.CreateHeadbandOfIntellect(bonus));
        Register(WondrousItemFactory.CreateCircletOfPersuasion());
        Register(WondrousItemFactory.CreateHatOfDisguise());

        // ════════════════════════════════════════════════════════════
        //  FACE/EYES SLOT — Goggles, lenses
        // ════════════════════════════════════════════════════════════
        Register(WondrousItemFactory.CreateGogglesOfNight());
        Register(WondrousItemFactory.CreateEyesOfTheEagle());

        // ════════════════════════════════════════════════════════════
        //  NECK/THROAT SLOT — Amulets, periapts, brooches
        // ════════════════════════════════════════════════════════════
        for (int bonus = 1; bonus <= 5; bonus++)
            Register(WondrousItemFactory.CreateAmuletOfNaturalArmor(bonus));
        for (int bonus = 2; bonus <= 6; bonus += 2)
            Register(WondrousItemFactory.CreateAmuletOfHealth(bonus));
        for (int bonus = 2; bonus <= 6; bonus += 2)
            Register(WondrousItemFactory.CreatePeriaptOfWisdom(bonus));
        Register(WondrousItemFactory.CreateBroochOfShielding());
        for (int bonus = 1; bonus <= 5; bonus++)
            Register(WondrousItemFactory.CreateAmuletOfMightyFists(bonus));

        // ════════════════════════════════════════════════════════════
        //  SHOULDERS/BACK SLOT — Cloaks, capes
        // ════════════════════════════════════════════════════════════
        for (int bonus = 1; bonus <= 5; bonus++)
            Register(WondrousItemFactory.CreateCloakOfResistance(bonus));
        for (int bonus = 2; bonus <= 6; bonus += 2)
            Register(WondrousItemFactory.CreateCloakOfCharisma(bonus));
        Register(WondrousItemFactory.CreateCloakOfElvenkind());
        Register(WondrousItemFactory.CreateCloakOfDisplacement(false)); // Minor: 20% miss chance
        Register(WondrousItemFactory.CreateCloakOfDisplacement(true));  // Major: 50% miss chance
        Register(WondrousItemFactory.CreateWingsOfFlying());

        // ════════════════════════════════════════════════════════════
        //  TORSO/BODY SLOT — Vests, robes, shirts
        // ════════════════════════════════════════════════════════════
        Register(WondrousItemFactory.CreateVestOfEscape());
        Register(WondrousItemFactory.CreateMonksBelt());

        // ════════════════════════════════════════════════════════════
        //  WAIST SLOT — Belts
        // ════════════════════════════════════════════════════════════
        for (int bonus = 2; bonus <= 6; bonus += 2)
            Register(WondrousItemFactory.CreateBeltOfGiantStrength(bonus));
        for (int bonus = 2; bonus <= 6; bonus += 2)
            Register(WondrousItemFactory.CreateBeltOfDexterity(bonus));

        // ════════════════════════════════════════════════════════════
        //  WRISTS/ARMS SLOT — Bracers
        // ════════════════════════════════════════════════════════════
        for (int bonus = 1; bonus <= 8; bonus++)
            Register(WondrousItemFactory.CreateBracersOfArmor(bonus));
        Register(WondrousItemFactory.CreateBracersOfArchery(false)); // Lesser
        Register(WondrousItemFactory.CreateBracersOfArchery(true));  // Greater

        // ════════════════════════════════════════════════════════════
        //  HANDS SLOT — Gloves, gauntlets
        // ════════════════════════════════════════════════════════════
        Register(WondrousItemFactory.CreateGauntletsOfOgrePower());
        for (int bonus = 2; bonus <= 6; bonus += 2)
            Register(WondrousItemFactory.CreateGlovesOfDexterity(bonus));
        Register(WondrousItemFactory.CreateGlovesOfSwimmingAndClimbing());

        // ════════════════════════════════════════════════════════════
        //  FEET SLOT — Boots, slippers
        // ════════════════════════════════════════════════════════════
        Register(WondrousItemFactory.CreateBootsOfSpeed());
        Register(WondrousItemFactory.CreateBootsOfElvenkind());
        Register(WondrousItemFactory.CreateBootsOfStridingAndSpringing());
        Register(WondrousItemFactory.CreateSlippersOfSpiderClimbing());

        // ════════════════════════════════════════════════════════════
        //  SLOTLESS — Bags, pearls, stones
        // ════════════════════════════════════════════════════════════
        for (int type = 1; type <= 4; type++)
            Register(WondrousItemFactory.CreateBagOfHolding(type));
        Register(WondrousItemFactory.CreateHandyHaversack());
        for (int level = 1; level <= 9; level++)
            Register(WondrousItemFactory.CreatePearlOfPower(level));
        Register(WondrousItemFactory.CreateStoneOfGoodLuck());

        // ════════════════════════════════════════════════════════════
        //  TEST ITEMS — One per slot for verification
        // ════════════════════════════════════════════════════════════
        Register(WondrousItemFactory.CreateTestItem(EquipSlot.Head, "Head"));
        Register(WondrousItemFactory.CreateTestItem(EquipSlot.FaceEyes, "Face"));
        Register(WondrousItemFactory.CreateTestItem(EquipSlot.Neck, "Neck"));
        Register(WondrousItemFactory.CreateTestItem(EquipSlot.Back, "Back"));
        Register(WondrousItemFactory.CreateTestItem(EquipSlot.Torso, "Torso"));
        Register(WondrousItemFactory.CreateTestItem(EquipSlot.Waist, "Waist"));
        Register(WondrousItemFactory.CreateTestItem(EquipSlot.Wrists, "Wrists"));
        Register(WondrousItemFactory.CreateTestItem(EquipSlot.Hands, "Hands"));
        Register(WondrousItemFactory.CreateTestItem(EquipSlot.Feet, "Feet"));
        Register(WondrousItemFactory.CreateTestSlotlessItem());

        Debug.Log($"[WondrousItemDatabase] Initialized: {_items.Count} wondrous items registered (Phase 1 Foundation).");
    }

    /// <summary>Register a test slotless item (delegates to factory).</summary>
    private static void RegisterTestSlotlessItem()
    {
        Register(WondrousItemFactory.CreateTestSlotlessItem());
    }

    /// <summary>Register a wondrous item in the database. Validates for duplicates.</summary>
    private static void Register(ItemData item)
    {
        if (item == null)
        {
            Debug.LogWarning("[WondrousItemDatabase] Attempted to register null item.");
            return;
        }

        string key = item.WondrousId;
        if (string.IsNullOrEmpty(key))
            key = item.Id;

        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning($"[WondrousItemDatabase] Item '{item.Name}' has no WondrousId or Id — skipping.");
            return;
        }

        if (_items.ContainsKey(key))
        {
            Debug.LogWarning($"[WondrousItemDatabase] Duplicate wondrous item ID: '{key}' — skipping '{item.Name}'.");
            return;
        }

        _items[key] = item;
    }

    /// <summary>
    /// Retrieve a wondrous item definition by its ID.
    /// Returns null if not found.
    /// </summary>
    public static ItemData GetItem(string wondrousId)
    {
        if (!_initialized) Init();
        if (string.IsNullOrEmpty(wondrousId)) return null;
        return _items.ContainsKey(wondrousId) ? _items[wondrousId] : null;
    }

    /// <summary>Get all registered wondrous item definitions (read-only snapshot).</summary>
    public static IReadOnlyDictionary<string, ItemData> GetAllItems()
    {
        if (!_initialized) Init();
        return _items;
    }

    /// <summary>Get all items for a specific equipment slot.</summary>
    public static List<ItemData> GetItemsBySlot(EquipSlot slot)
    {
        if (!_initialized) Init();
        var result = new List<ItemData>();
        foreach (var kvp in _items)
        {
            if (kvp.Value.WondrousRequiredSlot == slot || kvp.Value.Slot == slot)
                result.Add(kvp.Value);
        }
        return result;
    }

    /// <summary>Get all slotless items.</summary>
    public static List<ItemData> GetSlotlessItems()
    {
        if (!_initialized) Init();
        var result = new List<ItemData>();
        foreach (var kvp in _items)
        {
            if (kvp.Value.IsSlotless)
                result.Add(kvp.Value);
        }
        return result;
    }

    /// <summary>Get the count of registered wondrous items.</summary>
    public static int Count
    {
        get
        {
            if (!_initialized) Init();
            return _items.Count;
        }
    }

    /// <summary>
    /// Register all wondrous items into the main ItemDatabase so they appear in
    /// standard inventory/loot/shop systems. Call after ItemDatabase.Init().
    /// </summary>
    public static void RegisterAllInItemDatabase()
    {
        if (!_initialized) Init();

        int registered = 0;
        foreach (var kvp in _items)
        {
            ItemData existing = ItemDatabase.Get(kvp.Key);
            if (existing == null)
            {
                ItemDatabase.RegisterExternal(kvp.Value);
                registered++;
            }
        }

        Debug.Log($"[WondrousItemDatabase] Registered {registered} wondrous items in ItemDatabase.");
    }
}
