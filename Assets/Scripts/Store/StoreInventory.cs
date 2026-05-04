using System;
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

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
        Add(ItemIDs.LONGSWORD, "Weapon", 15);
        Add(ItemIDs.GREATSWORD, "Weapon", 50);
        Add(ItemIDs.BATTLEAXE, "Weapon", 10);
        Add(ItemIDs.GREATAXE, "Weapon", 20);
        Add(ItemIDs.RAPIER, "Weapon", 20);
        Add(ItemIDs.SHORT_SWORD, "Weapon", 10);
        Add(ItemIDs.DAGGER, "Weapon", 2);
        Add(ItemIDs.SHORTBOW, "Weapon", 30);
        Add(ItemIDs.LONGBOW, "Weapon", 75);
        Add(ItemIDs.CROSSBOW_HEAVY, "Weapon", 50);
        Add(ItemIDs.CROSSBOW_LIGHT, "Weapon", 35);
        Add(ItemIDs.MACE_HEAVY, "Weapon", 8);
        Add(ItemIDs.MORNINGSTAR, "Weapon", 8);
        Add(ItemIDs.WARHAMMER, "Weapon", 12);
        Add(ItemIDs.SPEAR, "Weapon", 2);
        Add(ItemIDs.JAVELIN, "Weapon", 1);

        // Armor
        Add(ItemIDs.CHAIN_SHIRT, "Armor", 100);
        Add(ItemIDs.SCALE_MAIL, "Armor", 50);
        Add(ItemIDs.CHAINMAIL, "Armor", 150);
        Add(ItemIDs.BREASTPLATE, "Armor", 200);
        Add(ItemIDs.SPLINT_MAIL, "Armor", 200);
        Add(ItemIDs.BANDED_MAIL, "Armor", 250);
        Add(ItemIDs.HALF_PLATE, "Armor", 600);
        Add(ItemIDs.FULL_PLATE, "Armor", 1500);
        Add(ItemIDs.LEATHER_ARMOR, "Armor", 10);
        Add(ItemIDs.STUDDED_LEATHER, "Armor", 25);
        Add(ItemIDs.HIDE_ARMOR, "Armor", 15);

        // Shields
        Add(ItemIDs.BUCKLER, "Shield", 15);
        Add(ItemIDs.SHIELD_LIGHT_WOODEN, "Shield", 3);
        Add(ItemIDs.SHIELD_LIGHT_STEEL, "Shield", 9);
        Add(ItemIDs.SHIELD_HEAVY_WOODEN, "Shield", 7);
        Add(ItemIDs.SHIELD_HEAVY_STEEL, "Shield", 20);
        Add(ItemIDs.TOWER_SHIELD, "Shield", 30);

        // Consumables
        Add(ItemIDs.POTION_CURE_LIGHT_WOUNDS, "Potion", 50);
        Add(ItemIDs.POTION_SHIELD_OF_FAITH, "Potion", 50);

        // Adventuring gear / misc
        Add(ItemIDs.CROSSBOW_BOLTS_20, "Ammunition", 1);
        Add(ItemIDs.TORCH, "Gear", 1);
        Add(ItemIDs.ROPE_HEMP, "Gear", 1);
        Add(ItemIDs.ROPE_SILK, "Gear", 10);

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
