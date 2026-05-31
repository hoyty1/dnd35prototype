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

        // Mundane Weapons
        Add(ItemID.WeaponLongsword, "Mundane Weapons", 15);
        Add(ItemID.WeaponGreatsword, "Mundane Weapons", 50);
        Add(ItemID.WeaponBattleaxe, "Mundane Weapons", 10);
        Add(ItemID.WeaponGreataxe, "Mundane Weapons", 20);
        Add(ItemID.WeaponRapier, "Mundane Weapons", 20);
        Add(ItemID.WeaponShortsword, "Mundane Weapons", 10);
        Add(ItemID.WeaponDagger, "Mundane Weapons", 2);
        Add(ItemID.WeaponShortbow, "Mundane Weapons", 30);
        Add(ItemID.WeaponLongbow, "Mundane Weapons", 75);
        Add(ItemID.WeaponCrossbowHeavy, "Mundane Weapons", 50);
        Add(ItemID.WeaponCrossbowLight, "Mundane Weapons", 35);
        Add(ItemID.WeaponMaceHeavy, "Mundane Weapons", 8);
        Add(ItemID.WeaponMorningstar, "Mundane Weapons", 8);
        Add(ItemID.WeaponWarhammer, "Mundane Weapons", 12);
        Add(ItemID.WeaponSpear, "Mundane Weapons", 2);
        Add(ItemID.WeaponJavelin, "Mundane Weapons", 1);

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

        // Mundane Shields
        Add(ItemID.ShieldBuckler, "Mundane Shields", 15);
        Add(ItemID.ShieldLightWooden, "Mundane Shields", 3);
        Add(ItemID.ShieldLightSteel, "Mundane Shields", 9);
        Add(ItemID.ShieldHeavyWooden, "Mundane Shields", 7);
        Add(ItemID.ShieldHeavySteel, "Mundane Shields", 20);
        Add(ItemID.ShieldTower, "Mundane Shields", 30);

        AddEnhancedStoreVariants();

        // Consumables
        Add(ItemID.PotionCureLightWounds, "Potion", 50);
        Add(ItemID.PotionShieldOfFaith, "Potion", 50);

        // Spell Components
        Add(ItemID.ComponentSpellPouch, "Spell Component", 5);
        Add(ItemID.ComponentDiamondDust, "Spell Component", 250);

        // Adventuring gear / misc
        Add(ItemID.AmmoCrossbowBolts20, "Ammunition", 1);
        Add(ItemID.WeaponTorch, "Mundane Weapons", 1);
        Add(ItemID.GearRopeHemp, "Gear", 1);
        Add(ItemID.GearRopeSilk, "Gear", 10);

        // Scrolls — generated from SpellDatabase by ScrollFactory
        ScrollFactory.AddScrollsToStore(this);

        // Potions — generated from SpellDatabase by PotionFactory
        PotionFactory.AddPotionsToStore(this);

        // Wands — generated from SpellDatabase by WandFactory
        WandFactory.AddWandsToStore(this);

        // Rings — pulled from RingDatabase
        AddRingsToStore();

        // Rods — pulled from RodDatabase
        AddRodsToStore();

        // Wondrous Items — pulled from WondrousItemDatabase
        AddWondrousItemsToStore();

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

            string magicCategory = ResolveMagicCategory(entry.Category);
            if (magicCategory == null)
                continue;

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
                Add(enhancedId, magicCategory, enhancedPrice);
            }
        }
    }

    /// <summary>
    /// Maps a mundane base category to its magic counterpart, or null when the
    /// category has no enhanced (magic) variants in the store.
    /// </summary>
    private static string ResolveMagicCategory(string baseCategory)
    {
        if (string.Equals(baseCategory, "Mundane Weapons", StringComparison.OrdinalIgnoreCase))
            return "Magic Weapons";
        if (string.Equals(baseCategory, "Mundane Shields", StringComparison.OrdinalIgnoreCase))
            return "Magic Shields";
        if (string.Equals(baseCategory, "Armor", StringComparison.OrdinalIgnoreCase))
            return "Magic Armor";
        return null;
    }

    /// <summary>
    /// Adds magic rings from RingDatabase to the store under the "Rings" category.
    /// </summary>
    private void AddRingsToStore()
    {
        RingDatabase.Init();
        RingDatabase.RegisterAllRingsInItemDatabase();

        var rings = RingDatabase.GetAllRings();
        if (rings == null)
            return;

        foreach (var kvp in rings)
        {
            ItemData ring = kvp.Value;
            if (ring == null || string.IsNullOrWhiteSpace(ring.Id))
                continue;

            int price = Mathf.Max(1, ring.BasePriceGp);
            AddExternalItem(ring.Id, "Rings", price);
        }
    }

    /// <summary>
    /// Adds magic rods from RodDatabase to the store under the "Rods" category.
    /// </summary>
    private void AddRodsToStore()
    {
        RodDatabase.Init();
        RodDatabase.RegisterAllInItemDatabase();

        var rods = RodDatabase.GetAllRods();
        if (rods == null)
            return;

        foreach (ItemData rod in rods)
        {
            if (rod == null || string.IsNullOrWhiteSpace(rod.Id))
                continue;

            int price = Mathf.Max(1, rod.BasePriceGp);
            AddExternalItem(rod.Id, "Rods", price);
        }
    }

    /// <summary>
    /// Adds wondrous items from WondrousItemDatabase to the store under the
    /// "Wondrous Items" category.
    /// </summary>
    private void AddWondrousItemsToStore()
    {
        WondrousItemDatabase.Init();
        WondrousItemDatabase.RegisterAllInItemDatabase();

        var items = WondrousItemDatabase.GetAllItems();
        if (items == null)
            return;

        foreach (var kvp in items)
        {
            ItemData item = kvp.Value;
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
                continue;

            int price = Mathf.Max(1, item.BasePriceGp);
            AddExternalItem(item.Id, "Wondrous Items", price);
        }
    }

    /// <summary>
    /// Adds an item that is keyed by a string Id (no ItemID enum entry), such as
    /// rings, rods and wondrous items registered into the ItemDatabase externally.
    /// </summary>
    private void AddExternalItem(string itemId, string category, int priceGp)
    {
        #pragma warning disable CS0618 // Intentional use of string-based Add for externally-registered IDs
        Add(itemId, category, priceGp);
        #pragma warning restore CS0618
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

    /// <summary>
    /// Public entry point for ScrollFactory to add scroll items to the store.
    /// Uses string IDs since scrolls are dynamically generated (no ItemID enum entries).
    /// </summary>
    public void AddScrollItem(string scrollItemId, string category, int priceGp)
    {
        #pragma warning disable CS0618 // Intentional use of string-based Add for dynamic scroll IDs
        Add(scrollItemId, category, priceGp);
        #pragma warning restore CS0618
    }

    /// <summary>
    /// Public entry point for PotionFactory to add potion items to the store.
    /// Uses string IDs since potions are dynamically generated (no ItemID enum entries).
    /// </summary>
    public void AddPotionItem(string potionItemId, string category, int priceGp)
    {
        #pragma warning disable CS0618 // Intentional use of string-based Add for dynamic potion IDs
        Add(potionItemId, category, priceGp);
        #pragma warning restore CS0618
    }

    /// <summary>
    /// Public entry point for WandFactory to add wand items to the store.
    /// Uses string IDs since wands are dynamically generated (no ItemID enum entries).
    /// </summary>
    public void AddWandItem(string wandItemId, string category, int priceGp)
    {
        #pragma warning disable CS0618 // Intentional use of string-based Add for dynamic wand IDs
        Add(wandItemId, category, priceGp);
        #pragma warning restore CS0618
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

        // Wand sell value is proportional to remaining charges
        // D&D 3.5e: a partially-used wand is worth less proportionally.
        // 0 charges = 0 gp (depleted wand is a useless stick).
        if (item.IsWand)
        {
            int maxCharges = item.Wand != null ? item.Wand.MaxCharges : item.MaxCharges;
            int currentCharges = item.Wand != null ? item.Wand.CurrentCharges : item.CurrentCharges;
            if (maxCharges > 0)
                sellPrice = Mathf.FloorToInt(sellPrice * ((float)currentCharges / maxCharges));
            else
                sellPrice = 0;
        }

        return Mathf.Max(0, sellPrice);
    }

    private int ResolveBaseValue(ItemData item)
    {
        if (item == null)
            return 0;

        // Treasure items (gems, art objects) use their appraised value for sell price.
        // D&D 3.5e: gems/art sell for appraised value (not half), but we apply the
        // standard 50% sell discount in GetSellPrice(). The appraised value IS the
        // effective "base price" for these items — a failed Appraise check means
        // the party misjudged the value and gets less (or occasionally more).
        if (item.IsTreasureItem && item.IsAppraised && item.AppraisedValueGp > 0)
            return item.AppraisedValueGp;

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
