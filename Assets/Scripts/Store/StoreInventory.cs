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

        public ItemID ItemIDEnum => ItemId.ToItemID();

        public ItemData GetTemplate()
        {
            ItemID parsed = ItemId.ToItemID();
            return parsed == ItemID.None ? ItemDatabase.Get(ItemId) : ItemDatabase.Get(parsed);
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
        Add(ItemID.WeaponLongsword, "Weapon", 15);
        Add(ItemID.WeaponGreatsword, "Weapon", 50);
        Add(ItemID.WeaponBattleaxe, "Weapon", 10);
        Add(ItemID.WeaponGreataxe, "Weapon", 20);
        Add(ItemID.WeaponRapier, "Weapon", 20);
        Add(ItemID.WeaponShortsword, "Weapon", 10);
        Add(ItemID.WeaponDagger, "Weapon", 2);
        Add(ItemID.WeaponShortbow, "Weapon", 30);
        Add(ItemID.WeaponLongbow, "Weapon", 75);
        Add(ItemID.WeaponCrossbowHeavy, "Weapon", 50);
        Add(ItemID.WeaponCrossbowLight, "Weapon", 35);
        Add(ItemID.WeaponMaceHeavy, "Weapon", 8);
        Add(ItemID.WeaponMorningstar, "Weapon", 8);
        Add(ItemID.WeaponWarhammer, "Weapon", 12);
        Add(ItemID.WeaponSpear, "Weapon", 2);
        Add(ItemID.WeaponJavelin, "Weapon", 1);

        // Armor
        Add(ItemID.ArmorChainShirt, "Armor", 100);
        Add(ItemID.ArmorScaleMail, "Armor", 50);
        Add(ItemID.ArmorChainMail, "Armor", 150);
        Add(ItemID.ArmorBreastplate, "Armor", 200);
        Add(ItemID.ArmorSplintMail, "Armor", 200);
        Add(ItemID.ArmorBandedMail, "Armor", 250);
        Add(ItemID.ArmorHalfPlate, "Armor", 600);
        Add(ItemID.ArmorPlate, "Armor", 1500);
        Add(ItemID.ArmorLeather, "Armor", 10);
        Add(ItemID.ArmorStuddedLeather, "Armor", 25);
        Add(ItemID.ArmorHide, "Armor", 15);

        // Shields
        Add(ItemID.ShieldBuckler, "Shield", 15);
        Add(ItemID.ShieldLightWooden, "Shield", 3);
        Add(ItemID.ShieldLightSteel, "Shield", 9);
        Add(ItemID.ShieldHeavyWooden, "Shield", 7);
        Add(ItemID.ShieldHeavySteel, "Shield", 20);
        Add(ItemID.ShieldTower, "Shield", 30);

        AddEnhancedStoreVariants();

        // Consumables
        Add(ItemID.PotionCureLightWounds, "Potion", 50);
        Add(ItemID.PotionShieldOfFaith, "Potion", 50);

        // Spell Components
        Add(ItemID.ComponentSpellPouch, "Spell Component", 5);
        Add(ItemID.ComponentDiamondDust, "Spell Component", 250);

        // Adventuring gear / misc
        Add(ItemID.AmmoCrossbowBolts20, "Ammunition", 1);
        Add(ItemID.WeaponTorch, "Weapon", 1);
        Add(ItemID.GearRopeHemp, "Gear", 1);
        Add(ItemID.GearRopeSilk, "Gear", 10);

        Debug.Log($"[Store] Initialized with {_availableItems.Count} items");
    }

    private void AddEnhancedStoreVariants()
    {
        var baseEntries = new List<StoreItemEntry>(_availableItems);
        for (int i = 0; i < baseEntries.Count; i++)
        {
            StoreItemEntry entry = baseEntries[i];
            if (entry == null)
                continue;

            if (!string.Equals(entry.Category, "Weapon", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(entry.Category, "Armor", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(entry.Category, "Shield", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ItemID baseId = entry.ItemIDEnum;
            if (baseId == ItemID.None)
                continue;

            ItemData baseTemplate = entry.GetTemplate();
            if (baseTemplate == null)
                continue;

            for (int bonus = 1; bonus <= 2; bonus++)
            {
                string enhancedEnumName = $"{baseId}Plus{bonus}";
                if (!Enum.TryParse(enhancedEnumName, out ItemID enhancedId) || enhancedId == ItemID.None)
                    continue;

                if (!ItemDatabase.HasItem(enhancedId))
                    continue;

                ItemData enhancedTemplate = ItemDatabase.Get(enhancedId);
                if (enhancedTemplate == null)
                    continue;

                enhancedTemplate.BasePriceGp = Mathf.Max(0, entry.PriceGp);
                int enhancedPrice = enhancedTemplate.GetEnhancedPriceGp(entry.PriceGp);
                Add(enhancedId, entry.Category, enhancedPrice);
            }
        }
    }

    private void Add(ItemID itemId, string category, int priceGp)
    {
        Add(itemId.ToStorageString(), category, priceGp);
    }

    [Obsolete("Prefer Add(ItemID, string, int) for type-safe registrations.", false)]
    private void Add(string itemId, string category, int priceGp)
    {
        ItemData template = ItemDatabase.Get(itemId);
        if (template == null)
        {
            Debug.LogWarning($"[Store] Skipping unknown item id '{itemId}'");
            return;
        }

        int normalizedPrice = Mathf.Max(0, priceGp);

        if (template.BasePriceGp <= 0)
            template.BasePriceGp = normalizedPrice;

        _availableItems.Add(new StoreItemEntry
        {
            ItemId = itemId,
            Category = category,
            PriceGp = normalizedPrice
        });

        _priceLookup[itemId] = normalizedPrice;
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

    public ItemData CreateItemInstance(ItemID itemId)
    {
        return ItemDatabase.CloneItem(itemId);
    }

    [Obsolete("Prefer CreateItemInstance(ItemID) for compile-time type safety.", false)]
    public ItemData CreateItemInstance(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        return ItemDatabase.CloneItem(itemId);
    }

    public bool TryGetBuyPrice(ItemID itemId, out int priceGp)
    {
        string storageId = itemId.ToStorageString();
        return TryGetBuyPrice(storageId, out priceGp);
    }

    [Obsolete("Prefer TryGetBuyPrice(ItemID, out int) for compile-time type safety.", false)]
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

        if (item.BasePriceGp > 0)
            return item.GetEnhancedPriceGp(item.BasePriceGp);

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
