using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Store catalog + buy/sell pricing. Uses existing ItemDatabase item definitions.
/// </summary>
public class StoreInventory : MonoBehaviour
{
    [Serializable]
    public class StoreItemEntry
    {
        public string ItemId;
        public string Category;
        public int PriceGp;

        public ItemData GetTemplate()
        {
            return ItemDatabase.Get(ItemId);
        }
    }

    public static StoreInventory Instance { get; private set; }

    private readonly List<StoreItemEntry> _availableItems = new List<StoreItemEntry>();
    private readonly Dictionary<string, int> _priceLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<StoreItemEntry> AllItems => _availableItems;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ItemDatabase.Init();

        if (_availableItems.Count == 0)
            InitializeStore();
    }

    private void InitializeStore()
    {
        Debug.Log("[Store] Initializing store inventory catalog from ItemDatabase");
        _availableItems.Clear();
        _priceLookup.Clear();

        // Weapons
        Add("longsword", "Weapon", 15);
        Add("greatsword", "Weapon", 50);
        Add("battleaxe", "Weapon", 10);
        Add("greataxe", "Weapon", 20);
        Add("rapier", "Weapon", 20);
        Add("short_sword", "Weapon", 10);
        Add("dagger", "Weapon", 2);
        Add("shortbow", "Weapon", 30);
        Add("longbow", "Weapon", 75);
        Add("crossbow_heavy", "Weapon", 50);
        Add("crossbow_light", "Weapon", 35);
        Add("mace_heavy", "Weapon", 8);
        Add("morningstar", "Weapon", 8);
        Add("warhammer", "Weapon", 12);
        Add("spear", "Weapon", 2);
        Add("javelin", "Weapon", 1);

        // Armor
        Add("chain_shirt", "Armor", 100);
        Add("scale_mail", "Armor", 50);
        Add("chainmail", "Armor", 150);
        Add("breastplate", "Armor", 200);
        Add("splint_mail", "Armor", 200);
        Add("banded_mail", "Armor", 250);
        Add("half_plate", "Armor", 600);
        Add("full_plate", "Armor", 1500);
        Add("leather_armor", "Armor", 10);
        Add("studded_leather", "Armor", 25);
        Add("hide_armor", "Armor", 15);

        // Shields
        Add("buckler", "Shield", 15);
        Add("shield_light_wooden", "Shield", 3);
        Add("shield_light_steel", "Shield", 9);
        Add("shield_heavy_wooden", "Shield", 7);
        Add("shield_heavy_steel", "Shield", 20);
        Add("tower_shield", "Shield", 30);

        // Consumables
        Add("potion_cure_light_wounds", "Potion", 50);
        Add("potion_healing", "Potion", 50);
        Add("potion_shield_of_faith", "Potion", 50);
        Add("potion_greater_healing", "Potion", 300);

        // Adventuring gear / misc
        Add("crossbow_bolts_20", "Ammunition", 1);
        Add("torch", "Gear", 1);
        Add("rope_hemp", "Gear", 1);
        Add("rope_silk", "Gear", 10);

        Debug.Log($"[Store] Initialized with {_availableItems.Count} items");
    }

    private void Add(string itemId, string category, int priceGp)
    {
        ItemData template = ItemDatabase.Get(itemId);
        if (template == null)
        {
            Debug.LogWarning($"[Store] Skipping unknown item id '{itemId}'");
            return;
        }

        _availableItems.Add(new StoreItemEntry
        {
            ItemId = itemId,
            Category = category,
            PriceGp = Mathf.Max(0, priceGp)
        });

        _priceLookup[itemId] = Mathf.Max(0, priceGp);
    }

    public List<StoreItemEntry> GetItemsByCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category) || string.Equals(category, "All", StringComparison.OrdinalIgnoreCase))
            return new List<StoreItemEntry>(_availableItems);

        return _availableItems.FindAll(entry => string.Equals(entry.Category, category, StringComparison.OrdinalIgnoreCase));
    }

    public List<string> GetCategories()
    {
        HashSet<string> categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "All" };
        for (int i = 0; i < _availableItems.Count; i++)
            categories.Add(_availableItems[i].Category);

        List<string> sorted = new List<string>(categories);
        sorted.Sort(StringComparer.OrdinalIgnoreCase);

        // Keep All first for convenience.
        sorted.RemoveAll(c => string.Equals(c, "All", StringComparison.OrdinalIgnoreCase));
        sorted.Insert(0, "All");
        return sorted;
    }

    public ItemData CreateItemInstance(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        return ItemDatabase.CloneItem(itemId);
    }

    public bool TryGetBuyPrice(string itemId, out int priceGp)
    {
        if (!string.IsNullOrWhiteSpace(itemId) && _priceLookup.TryGetValue(itemId, out priceGp))
            return true;

        priceGp = 0;
        return false;
    }

    public int GetSellPrice(ItemData item)
    {
        if (item == null)
            return 0;

        int baseValue = ResolveBaseValue(item);
        int sellPrice = Mathf.FloorToInt(baseValue * 0.5f);
        return Mathf.Max(0, sellPrice);
    }

    private int ResolveBaseValue(ItemData item)
    {
        if (item == null)
            return 0;

        if (!string.IsNullOrWhiteSpace(item.Id) && _priceLookup.TryGetValue(item.Id, out int listed))
            return listed;

        // Fallback estimate for items not in the store's direct catalog.
        switch (item.Type)
        {
            case ItemType.Weapon:
                return 10;
            case ItemType.Armor:
                return 25;
            case ItemType.Shield:
                return 10;
            case ItemType.Consumable:
                return 50;
            default:
                return Mathf.Max(1, Mathf.RoundToInt(item.WeightLbs));
        }
    }
}
