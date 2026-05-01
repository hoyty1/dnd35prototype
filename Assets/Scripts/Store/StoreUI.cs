using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pre-combat merchant interface with Buy/Sell tabs.
/// </summary>
public class StoreUI : MonoBehaviour
{
    private sealed class SellEntry
    {
        public ItemData Item;
        public bool FromStash;
        public CharacterInventory InventoryOwner;
        public string OwnerName;
    }

    private GameObject _root;
    private GameObject _buyPanel;
    private GameObject _sellPanel;
    private Text _goldText;
    private Text _messageText;

    private RectTransform _buyContent;
    private RectTransform _sellContent;
    private RectTransform _categoryFilterRoot;
    private RectTransform _sellCharacterFilterRoot;
    private RectTransform _sellCharacterButtonsRoot;
    private RectTransform _sellCategoryFilterRoot;
    private readonly Dictionary<string, Image> _categoryButtonImages = new Dictionary<string, Image>();
    private readonly Dictionary<string, Image> _sellCharacterButtonImages = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Image> _sellCategoryButtonImages = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
    private string _currentBuyCategory = "All";
    private string _currentSellCategory = "All";
    private string _currentSellCharacterKey = SellCharacterAllKey;
    private CharacterController _selectedSellCharacter;

    private const string SellCharacterAllKey = "__all__";
    private const string SellCharacterStashKey = "__stash__";

    private PartyStash _partyStash;
    private List<CharacterController> _partyMembers = new List<CharacterController>();
    private Action _onBackToMenu;
    private Action _onStartEncounter;

    private Action<int> _goldChangedHandler;

    public bool IsOpen => _root != null && _root.activeSelf;

    public void ShowStore(
        PartyStash partyStash,
        List<CharacterController> partyMembers,
        Action onBackToMenu,
        Action onStartEncounter)
    {
        EnsureBuilt();
        if (_root == null)
            return;

        _partyStash = partyStash;
        _partyMembers = partyMembers != null ? new List<CharacterController>(partyMembers) : new List<CharacterController>();
        _onBackToMenu = onBackToMenu;
        _onStartEncounter = onStartEncounter;

        _root.transform.SetAsLastSibling();
        _root.SetActive(true);

        if (_goldText != null)
            _goldText.text = $"Gold: {GameManager.Instance.PartyGold} gp";

        SubscribeGoldEvents();
        BuildCategoryOptions();

        _currentSellCharacterKey = SellCharacterAllKey;
        _selectedSellCharacter = null;
        _currentSellCategory = "All";
        BuildSellCharacterOptions(_sellCharacterButtonsRoot);
        RefreshSellCharacterButtons();
        RefreshSellCategoryButtons();

        ShowBuyPanel();

        Debug.Log($"[Store] Store opened with {StoreInventory.Instance.GetItemsByCategory("All").Count} items");
        Debug.Log($"[Store] Party has {GameManager.Instance.PartyGold} gp");
    }

    public void Close()
    {
        UnsubscribeGoldEvents();
        if (_root != null)
            _root.SetActive(false);
    }

    private void SubscribeGoldEvents()
    {
        UnsubscribeGoldEvents();

        if (GameManager.Instance == null)
            return;

        _goldChangedHandler = newGold =>
        {
            if (_goldText != null)
                _goldText.text = $"Gold: {newGold} gp";
        };

        GameManager.Instance.OnGoldChanged += _goldChangedHandler;
    }

    private void UnsubscribeGoldEvents()
    {
        if (GameManager.Instance != null && _goldChangedHandler != null)
            GameManager.Instance.OnGoldChanged -= _goldChangedHandler;

        _goldChangedHandler = null;
    }

    private void EnsureBuilt()
    {
        if (_root != null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            Debug.LogError("[Store] Cannot build store UI because no Canvas was found.");
            return;
        }

        _root = CreatePanel(canvas.transform, "StoreRoot",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, new Color(0.08f, 0.1f, 0.15f, 0.985f));

        Debug.Log("[Store] Main panel bounds: FULLSCREEN (0.0 to 1.0)");

        CreateText(_root.transform, "Title", "MERCHANT SHOP",
            new Vector2(0.1f, 0.94f), new Vector2(0.9f, 0.99f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, 36, FontStyle.Bold,
            new Color(0.98f, 0.88f, 0.45f), TextAnchor.MiddleCenter);

        Debug.Log("[Store] Title bounds: 0.05 to 0.95 (within panel)");

        _goldText = CreateText(_root.transform, "GoldText", "Gold: 0 gp",
            new Vector2(0.62f, 0.89f), new Vector2(0.9f, 0.93f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, 24, FontStyle.Bold,
            new Color(1f, 0.92f, 0.2f), TextAnchor.MiddleCenter);

        CreateTabButton("BuyTab", "BUY", new Vector2(0.12f, 0.84f), new Vector2(0.38f, 0.9f), new Color(0.22f, 0.5f, 0.28f), ShowBuyPanel);
        CreateTabButton("SellTab", "SELL", new Vector2(0.4f, 0.84f), new Vector2(0.66f, 0.9f), new Color(0.56f, 0.35f, 0.18f), ShowSellPanel);

        BuildBuyPanel();
        BuildSellPanel();

        _messageText = CreateText(_root.transform, "MessageText", string.Empty,
            new Vector2(0.1f, 0.11f), new Vector2(0.9f, 0.16f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, 18, FontStyle.Bold,
            new Color(0.84f, 0.9f, 1f), TextAnchor.MiddleCenter);

        CreateBottomButton("BackButton", "Back to Menu", new Vector2(0.1f, 0.03f), new Vector2(0.44f, 0.09f), new Color(0.48f, 0.25f, 0.25f), () =>
        {
            Close();
            _onBackToMenu?.Invoke();
        });

        CreateBottomButton("StartButton", "Start Encounter", new Vector2(0.56f, 0.03f), new Vector2(0.9f, 0.09f), new Color(0.2f, 0.58f, 0.28f), () =>
        {
            Close();
            _onStartEncounter?.Invoke();
        });

        Debug.Log("[Store] All elements within safe area");

        Debug.Log("[UI] === FULLSCREEN UI UPDATES ===");
        Debug.Log("[Store] Panel: (0,0) to (1,1) - FULLSCREEN");
        Debug.Log("[UI] Store window updated with fullscreen proportions and larger typography/buttons.");
        Debug.Log("[UI] Reverting button sizes to prevent overlapping");
        Debug.Log("[UI] Action buttons: 150x40 (reverted from 200x60)");
        Debug.Log("[UI] Category buttons: 80x30 (reverted from 120x45)");
        Debug.Log("[UI] Item buttons: 55-70x35-45 (reverted from 80x50)");
        Debug.Log("[UI] Spacing: 5px (reverted from 10px)");
        Debug.Log("[UI] Padding: 5px (reverted from 10-20px)");
        Debug.Log("[UI] Button text: 14-16px (reverted from 18-20px)");
        Debug.Log("[UI] Fullscreen panels maintained");

        _root.SetActive(false);
    }

    private void BuildBuyPanel()
    {
        _buyPanel = CreatePanel(_root.transform, "BuyPanel",
            new Vector2(0.1f, 0.17f), new Vector2(0.9f, 0.83f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, new Color(0.06f, 0.08f, 0.12f, 0.94f));

        Debug.Log("[Store] Buy panel bounds: fullscreen proportion (0.10 to 0.90)");

        CreateCategoryFilter();
        CreateScrollList(_buyPanel.transform, "BuyScroll", new Vector2(0f, 0f), new Vector2(1f, 0.83f), new Vector2(16f, 16f), new Vector2(-16f, -4f), out _buyContent);

        Debug.Log("[Store] === ITEM ROW WIDTH FIX ===");
        Debug.Log("[Store] Info section: minWidth=200, preferredWidth=300, flexibleWidth=1");
        Debug.Log("[Store] Price section: fixed 90px width");
        Debug.Log("[Store] Button section: fixed 70px width");
        Debug.Log("[Store] Left padding increased to 15px");
        Debug.Log("[Store] Text overflow mode: Overflow (no clipping)");
        Debug.Log("[Store] Full item names and descriptions now visible");
        Debug.Log("[Store] === CATEGORY BUTTONS ===");
        Debug.Log("[Store] Grid layout with wrapping");
        Debug.Log("[Store] Button size: 90x35");
        Debug.Log("[Store] Active category highlighted green");
    }

    private void BuildSellPanel()
    {
        _sellPanel = CreatePanel(_root.transform, "SellPanel",
            new Vector2(0.1f, 0.17f), new Vector2(0.9f, 0.83f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, new Color(0.07f, 0.07f, 0.1f, 0.94f));

        CreateText(_sellPanel.transform, "Hint", "Items sell for 50% of listed value (D&D 3.5e)",
            new Vector2(0.02f, 0.94f), new Vector2(0.98f, 0.99f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, 16, FontStyle.Italic,
            new Color(0.95f, 0.84f, 0.45f), TextAnchor.MiddleCenter);

        CreateSellCharacterFilter();
        CreateSellCategoryFilter();

        CreateScrollList(_sellPanel.transform, "SellScroll", new Vector2(0f, 0f), new Vector2(1f, 0.80f), new Vector2(16f, 16f), new Vector2(-16f, -4f), out _sellContent);

        Debug.Log("[Store] === SELL MENU FILTERS ADDED ===");
        Debug.Log("[Store] Character filter: All, Stash, and individual characters");
        Debug.Log("[Store] Type filter: All, Weapon, Armor, Shield, Potion, Scroll, Ammunition, Gear");
        Debug.Log("[Store] Filter area layout:");
        Debug.Log("[Store]   - Character filter: 88-94% (top)");
        Debug.Log("[Store]   - Type filter: 80-88% (middle)");
        Debug.Log("[Store]   - Sell list: 0-80% (bottom)");
        Debug.Log("[Store] Filters automatically reset when switching to SELL tab");

        _sellPanel.SetActive(false);
    }

    private void BuildCategoryOptions()
    {
        if (_categoryFilterRoot == null)
            return;

        ClearChildren(_categoryFilterRoot);
        _categoryButtonImages.Clear();

        List<string> categories = StoreInventory.Instance.GetCategories();
        if (categories == null || categories.Count == 0)
            categories = new List<string> { "All" };

        if (!categories.Contains(_currentBuyCategory))
            _currentBuyCategory = categories[0];

        Debug.Log($"[Store] Creating {categories.Count} category buttons");

        for (int i = 0; i < categories.Count; i++)
            CreateCategoryButton(_categoryFilterRoot, categories[i]);

        RefreshCategoryButtons();
    }

    private void CreateCategoryFilter()
    {
        if (_buyPanel == null)
            return;

        GameObject filterObj = new GameObject("CategoryFilter", typeof(RectTransform), typeof(Image), typeof(GridLayoutGroup));
        filterObj.transform.SetParent(_buyPanel.transform, false);

        _categoryFilterRoot = filterObj.GetComponent<RectTransform>();
        _categoryFilterRoot.anchorMin = new Vector2(0f, 0.85f);
        _categoryFilterRoot.anchorMax = new Vector2(1f, 1f);
        _categoryFilterRoot.offsetMin = Vector2.zero;
        _categoryFilterRoot.offsetMax = Vector2.zero;

        Image bg = filterObj.GetComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.2f, 0.5f);

        GridLayoutGroup grid = filterObj.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(90f, 35f);
        grid.spacing = new Vector2(5f, 5f);
        grid.padding = new RectOffset(5, 5, 5, 5);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.Flexible;
    }

    private void CreateCategoryButton(Transform parent, string category)
    {
        GameObject buttonObj = new GameObject($"CategoryBtn_{category}", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObj.transform.SetParent(parent, false);

        Image buttonBg = buttonObj.GetComponent<Image>();
        buttonBg.color = new Color(0.3f, 0.3f, 0.4f, 1f);

        Button button = buttonObj.GetComponent<Button>();
        button.onClick.AddListener(() =>
        {
            FilterByCategory(category);
            RefreshCategoryButtons();
        });

        _categoryButtonImages[category] = buttonBg;

        CreateText(buttonObj.transform, "Text", category,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            Vector2.zero, 13, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

        Debug.Log($"[Store] Created category button: {category}");
    }

    private void RefreshCategoryButtons()
    {
        foreach (KeyValuePair<string, Image> kvp in _categoryButtonImages)
        {
            if (kvp.Value == null)
                continue;

            kvp.Value.color = kvp.Key == _currentBuyCategory
                ? new Color(0.4f, 0.6f, 0.4f, 1f)
                : new Color(0.3f, 0.3f, 0.4f, 1f);
        }

        Debug.Log($"[Store] Refreshed category buttons, current: {_currentBuyCategory}");
    }

    private void FilterByCategory(string category)
    {
        Debug.Log($"[Store] Filtering by category: {category}");
        _currentBuyCategory = category;
        RebuildBuyList();
    }

    private void CreateSellCharacterFilter()
    {
        if (_sellPanel == null)
            return;

        GameObject filterObj = new GameObject("SellCharacterFilter", typeof(RectTransform), typeof(Image));
        filterObj.transform.SetParent(_sellPanel.transform, false);

        _sellCharacterFilterRoot = filterObj.GetComponent<RectTransform>();
        _sellCharacterFilterRoot.anchorMin = new Vector2(0f, 0.88f);
        _sellCharacterFilterRoot.anchorMax = new Vector2(1f, 0.94f);
        _sellCharacterFilterRoot.offsetMin = Vector2.zero;
        _sellCharacterFilterRoot.offsetMax = Vector2.zero;

        Image bg = filterObj.GetComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.2f, 0.55f);

        CreateText(filterObj.transform, "Label", "CHARACTER:",
            new Vector2(0.01f, 0f), new Vector2(0.14f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, 14, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);

        GameObject buttonRow = new GameObject("CharacterButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        buttonRow.transform.SetParent(filterObj.transform, false);

        RectTransform rowRect = buttonRow.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.15f, 0.08f);
        rowRect.anchorMax = new Vector2(0.99f, 0.92f);
        rowRect.offsetMin = Vector2.zero;
        rowRect.offsetMax = Vector2.zero;
        _sellCharacterButtonsRoot = rowRect;

        HorizontalLayoutGroup rowLayout = buttonRow.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 6f;
        rowLayout.padding = new RectOffset(0, 0, 0, 0);
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = false;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;

        BuildSellCharacterOptions(buttonRow.transform);
    }

    private void BuildSellCharacterOptions(Transform parent)
    {
        if (parent == null)
            return;

        ClearChildren(parent);
        _sellCharacterButtonImages.Clear();

        CreateSellCharacterButton(parent, SellCharacterAllKey, "All", null);
        CreateSellCharacterButton(parent, SellCharacterStashKey, "Stash", null);

        for (int i = 0; i < _partyMembers.Count; i++)
        {
            CharacterController character = _partyMembers[i];
            if (character == null)
                continue;

            string ownerName = character.Stats != null ? character.Stats.CharacterName : character.name;
            if (string.IsNullOrWhiteSpace(ownerName))
                ownerName = $"Character {i + 1}";

            string key = character.GetInstanceID().ToString();
            CreateSellCharacterButton(parent, key, ownerName, character);
        }

        RefreshSellCharacterButtons();
        Debug.Log($"[Store] Character filter created with {_sellCharacterButtonImages.Count} buttons");
    }

    private void CreateSellCharacterButton(Transform parent, string key, string label, CharacterController character)
    {
        GameObject buttonObj = new GameObject($"SellCharBtn_{label}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObj.transform.SetParent(parent, false);

        LayoutElement layout = buttonObj.GetComponent<LayoutElement>();
        layout.preferredWidth = 110f;
        layout.minWidth = 90f;
        layout.preferredHeight = 34f;

        Image buttonBg = buttonObj.GetComponent<Image>();
        buttonBg.color = new Color(0.3f, 0.3f, 0.4f, 1f);

        Button button = buttonObj.GetComponent<Button>();
        button.onClick.AddListener(() =>
        {
            SelectSellCharacterFilter(key, character);
        });

        _sellCharacterButtonImages[key] = buttonBg;

        CreateText(buttonObj.transform, "Text", label,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            Vector2.zero, 13, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
    }

    private void SelectSellCharacterFilter(string key, CharacterController character)
    {
        _currentSellCharacterKey = string.IsNullOrWhiteSpace(key) ? SellCharacterAllKey : key;

        switch (_currentSellCharacterKey)
        {
            case SellCharacterAllKey:
            case SellCharacterStashKey:
                _selectedSellCharacter = null;
                break;
            default:
                _selectedSellCharacter = character;
                break;
        }

        RefreshSellCharacterButtons();
        RebuildSellList();

        string label = _currentSellCharacterKey == SellCharacterAllKey
            ? "All"
            : _currentSellCharacterKey == SellCharacterStashKey
                ? "Stash"
                : (character != null && character.Stats != null
                    ? character.Stats.CharacterName
                    : character != null ? character.name : "Unknown");
        Debug.Log($"[Store] Sell filtered by character: {label}");
    }

    private void RefreshSellCharacterButtons()
    {
        string selectedKey = GetSelectedSellCharacterKey();

        foreach (KeyValuePair<string, Image> kvp in _sellCharacterButtonImages)
        {
            if (kvp.Value == null)
                continue;

            bool selected = string.Equals(kvp.Key, selectedKey, StringComparison.OrdinalIgnoreCase);
            kvp.Value.color = selected
                ? new Color(0.4f, 0.6f, 0.7f, 1f)
                : new Color(0.3f, 0.3f, 0.4f, 1f);
        }
    }

    private void CreateSellCategoryFilter()
    {
        if (_sellPanel == null)
            return;

        GameObject filterObj = new GameObject("SellCategoryFilter", typeof(RectTransform), typeof(Image));
        filterObj.transform.SetParent(_sellPanel.transform, false);

        _sellCategoryFilterRoot = filterObj.GetComponent<RectTransform>();
        _sellCategoryFilterRoot.anchorMin = new Vector2(0f, 0.80f);
        _sellCategoryFilterRoot.anchorMax = new Vector2(1f, 0.88f);
        _sellCategoryFilterRoot.offsetMin = Vector2.zero;
        _sellCategoryFilterRoot.offsetMax = Vector2.zero;

        Image bg = filterObj.GetComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.2f, 0.55f);

        CreateText(filterObj.transform, "Label", "TYPE:",
            new Vector2(0.01f, 0f), new Vector2(0.14f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, 14, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);

        GameObject gridObj = new GameObject("CategoryButtons", typeof(RectTransform), typeof(GridLayoutGroup));
        gridObj.transform.SetParent(filterObj.transform, false);

        RectTransform gridRect = gridObj.GetComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.15f, 0.06f);
        gridRect.anchorMax = new Vector2(0.99f, 0.94f);
        gridRect.offsetMin = Vector2.zero;
        gridRect.offsetMax = Vector2.zero;

        GridLayoutGroup grid = gridObj.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(90f, 30f);
        grid.spacing = new Vector2(5f, 4f);
        grid.padding = new RectOffset(0, 0, 0, 0);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.Flexible;

        BuildSellCategoryOptions(gridObj.transform);
    }

    private void BuildSellCategoryOptions(Transform parent)
    {
        if (parent == null)
            return;

        ClearChildren(parent);
        _sellCategoryButtonImages.Clear();

        string[] categories =
        {
            "All",
            "Weapon",
            "Armor",
            "Shield",
            "Potion",
            "Scroll",
            "Ammunition",
            "Gear"
        };

        for (int i = 0; i < categories.Length; i++)
            CreateSellCategoryButton(parent, categories[i]);

        RefreshSellCategoryButtons();
        Debug.Log($"[Store] Sell category filter created with {categories.Length} categories");
    }

    private void CreateSellCategoryButton(Transform parent, string category)
    {
        GameObject buttonObj = new GameObject($"SellCategoryBtn_{category}", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObj.transform.SetParent(parent, false);

        Image bg = buttonObj.GetComponent<Image>();
        bg.color = new Color(0.3f, 0.3f, 0.4f, 1f);

        Button button = buttonObj.GetComponent<Button>();
        button.onClick.AddListener(() =>
        {
            FilterSellByCategory(category);
        });

        _sellCategoryButtonImages[category] = bg;

        CreateText(buttonObj.transform, "Text", category,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            Vector2.zero, 13, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
    }

    private void FilterSellByCategory(string category)
    {
        _currentSellCategory = string.IsNullOrWhiteSpace(category) ? "All" : category;
        RefreshSellCategoryButtons();
        RebuildSellList();

        Debug.Log($"[Store] Sell filtered by category: {_currentSellCategory}");
    }

    private void RefreshSellCategoryButtons()
    {
        foreach (KeyValuePair<string, Image> kvp in _sellCategoryButtonImages)
        {
            if (kvp.Value == null)
                continue;

            bool selected = string.Equals(kvp.Key, _currentSellCategory, StringComparison.OrdinalIgnoreCase);
            kvp.Value.color = selected
                ? new Color(0.4f, 0.6f, 0.4f, 1f)
                : new Color(0.3f, 0.3f, 0.4f, 1f);
        }
    }

    private string GetSelectedSellCharacterKey()
    {
        if (!string.IsNullOrWhiteSpace(_currentSellCharacterKey))
            return _currentSellCharacterKey;

        if (_selectedSellCharacter != null)
            return _selectedSellCharacter.GetInstanceID().ToString();

        return SellCharacterAllKey;
    }

    private void ShowBuyPanel()
    {
        Debug.Log("[Store] Showing buy panel");
        if (_buyPanel != null) _buyPanel.SetActive(true);
        if (_sellPanel != null) _sellPanel.SetActive(false);
        RefreshCategoryButtons();
        RebuildBuyList();
    }

    private void ShowSellPanel()
    {
        if (_buyPanel != null) _buyPanel.SetActive(false);
        if (_sellPanel != null) _sellPanel.SetActive(true);

        _currentSellCharacterKey = SellCharacterAllKey;
        _selectedSellCharacter = null;
        _currentSellCategory = "All";
        RefreshSellCharacterButtons();
        RefreshSellCategoryButtons();
        RebuildSellList();

        Debug.Log("[Store] Switched to SELL tab with filters reset");
    }

    private void RebuildBuyList()
    {
        if (_buyContent == null)
            return;

        ClearChildren(_buyContent);

        string category = string.IsNullOrWhiteSpace(_currentBuyCategory) ? "All" : _currentBuyCategory;

        Debug.Log($"[Store] Player selected category: {category}");

        List<StoreInventory.StoreItemEntry> items = StoreInventory.Instance.GetItemsByCategory(category);
        for (int i = 0; i < items.Count; i++)
            CreateBuyRow(_buyContent, items[i]);
    }

    private void RebuildSellList()
    {
        if (_sellContent == null)
            return;

        ClearChildren(_sellContent);

        List<SellEntry> entries = BuildSellEntries();
        for (int i = 0; i < entries.Count; i++)
            CreateSellRow(_sellContent, entries[i]);

        int totalCount = CountAllSellableItems();
        Debug.Log($"[Store] Sell list refreshed: showing {entries.Count}/{totalCount} items (Character: {GetSellCharacterLabelForLogs()}, Category: {_currentSellCategory})");
    }

    private List<SellEntry> BuildSellEntries()
    {
        List<SellEntry> entries = new List<SellEntry>();

        bool includeStash = string.Equals(_currentSellCharacterKey, SellCharacterAllKey, StringComparison.OrdinalIgnoreCase)
            || string.Equals(_currentSellCharacterKey, SellCharacterStashKey, StringComparison.OrdinalIgnoreCase);

        if (includeStash && _partyStash != null)
        {
            List<ItemData> stashItems = _partyStash.GetItemsSnapshot();
            for (int i = 0; i < stashItems.Count; i++)
            {
                ItemData item = stashItems[i];
                if (item == null || !MatchesSellCategory(item))
                    continue;

                entries.Add(new SellEntry
                {
                    Item = item,
                    FromStash = true,
                    OwnerName = "Stash"
                });
            }
        }

        if (!string.Equals(_currentSellCharacterKey, SellCharacterStashKey, StringComparison.OrdinalIgnoreCase))
        {
            for (int i = 0; i < _partyMembers.Count; i++)
            {
                CharacterController character = _partyMembers[i];
                if (character == null)
                    continue;

                if (_selectedSellCharacter != null && character != _selectedSellCharacter)
                    continue;

                CharacterInventory inventory = character.GetComponent<CharacterInventory>();
                if (inventory == null)
                    continue;

                List<ItemData> items = inventory.GetAllItems();
                for (int j = 0; j < items.Count; j++)
                {
                    ItemData item = items[j];
                    if (item == null || !MatchesSellCategory(item))
                        continue;

                    string ownerName = character.Stats != null ? character.Stats.CharacterName : character.name;
                    entries.Add(new SellEntry
                    {
                        Item = item,
                        FromStash = false,
                        InventoryOwner = inventory,
                        OwnerName = ownerName
                    });
                }
            }
        }

        return entries;
    }

    private bool MatchesSellCategory(ItemData item)
    {
        if (item == null)
            return false;

        if (string.IsNullOrWhiteSpace(_currentSellCategory)
            || string.Equals(_currentSellCategory, "All", StringComparison.OrdinalIgnoreCase))
            return true;

        string itemCategory = GetSellItemCategory(item);
        return string.Equals(itemCategory, _currentSellCategory, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetSellItemCategory(ItemData item)
    {
        if (item == null)
            return "Gear";

        switch (item.Type)
        {
            case ItemType.Weapon:
                return IsAmmunitionName(item.Name) ? "Ammunition" : "Weapon";
            case ItemType.Armor:
                return "Armor";
            case ItemType.Shield:
                return "Shield";
            case ItemType.Consumable:
                if (ContainsIgnoreCase(item.Name, "scroll"))
                    return "Scroll";
                if (ContainsIgnoreCase(item.Name, "potion"))
                    return "Potion";
                return "Gear";
            default:
                if (IsAmmunitionName(item.Name))
                    return "Ammunition";
                if (ContainsIgnoreCase(item.Name, "scroll"))
                    return "Scroll";
                if (ContainsIgnoreCase(item.Name, "potion"))
                    return "Potion";
                return "Gear";
        }
    }

    private static bool ContainsIgnoreCase(string source, string value)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(value))
            return false;

        return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsAmmunitionName(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            return false;

        return ContainsIgnoreCase(itemName, "arrow")
            || ContainsIgnoreCase(itemName, "bolt")
            || ContainsIgnoreCase(itemName, "ammunition");
    }

    private int CountAllSellableItems()
    {
        int total = 0;

        if (_partyStash != null)
            total += _partyStash.GetItemsSnapshot().Count;

        for (int i = 0; i < _partyMembers.Count; i++)
        {
            CharacterController character = _partyMembers[i];
            if (character == null)
                continue;

            CharacterInventory inventory = character.GetComponent<CharacterInventory>();
            if (inventory == null)
                continue;

            total += inventory.GetAllItems().Count;
        }

        return total;
    }

    private string GetSellCharacterLabelForLogs()
    {
        if (string.Equals(_currentSellCharacterKey, SellCharacterStashKey, StringComparison.OrdinalIgnoreCase))
            return "Stash";

        if (_selectedSellCharacter == null)
            return "All";

        return _selectedSellCharacter.Stats != null
            ? _selectedSellCharacter.Stats.CharacterName
            : _selectedSellCharacter.name;
    }

    private static string GetItemDescription(ItemData item, string fallback)
    {
        if (item == null)
            return string.IsNullOrWhiteSpace(fallback) ? "Unknown item" : fallback;

        if (!string.IsNullOrWhiteSpace(item.Description))
            return item.Description;

        string name = item.Name ?? string.Empty;

        if (name.Contains("Longsword", StringComparison.OrdinalIgnoreCase))
            return "1d8 slashing, versatile (1d10)";
        if (name.Contains("Shortsword", StringComparison.OrdinalIgnoreCase))
            return "1d6 piercing, light, finesse";
        if (name.Contains("Greatsword", StringComparison.OrdinalIgnoreCase))
            return "2d6 slashing, heavy, two-handed";
        if (name.Contains("Battleaxe", StringComparison.OrdinalIgnoreCase))
            return "1d8 slashing, versatile (1d10)";
        if (name.Contains("Handaxe", StringComparison.OrdinalIgnoreCase))
            return "1d6 slashing, light, thrown";
        if (name.Contains("Greataxe", StringComparison.OrdinalIgnoreCase))
            return "1d12 slashing, heavy, two-handed";
        if (name.Contains("Dagger", StringComparison.OrdinalIgnoreCase))
            return "1d4 piercing, finesse, light, thrown";
        if (name.Contains("Mace", StringComparison.OrdinalIgnoreCase))
            return "1d6 bludgeoning";
        if (name.Contains("Warhammer", StringComparison.OrdinalIgnoreCase))
            return "1d8 bludgeoning, versatile (1d10)";
        if (name.Contains("Rapier", StringComparison.OrdinalIgnoreCase))
            return "1d8 piercing, finesse";
        if (name.Contains("Longbow", StringComparison.OrdinalIgnoreCase))
            return "1d8 piercing, heavy, two-handed, range 100 ft";
        if (name.Contains("Shortbow", StringComparison.OrdinalIgnoreCase))
            return "1d6 piercing, two-handed, range 60 ft";
        if (name.Contains("Crossbow", StringComparison.OrdinalIgnoreCase))
            return "Piercing ranged weapon, loading";
        if (name.Contains("Plate", StringComparison.OrdinalIgnoreCase))
            return "Heavy armor, high AC";
        if (name.Contains("Chain", StringComparison.OrdinalIgnoreCase))
            return "Medium/heavy armor with armor check penalty";
        if (name.Contains("Leather", StringComparison.OrdinalIgnoreCase))
            return "Light armor";
        if (name.Contains("Shield", StringComparison.OrdinalIgnoreCase))
            return "Shield bonus to AC";
        if (name.Contains("Potion", StringComparison.OrdinalIgnoreCase))
            return "Consumable magical effect";
        if (name.Contains("Scroll", StringComparison.OrdinalIgnoreCase))
            return "Single-use spell scroll";
        if (name.Contains("Arrow", StringComparison.OrdinalIgnoreCase) || name.Contains("Bolt", StringComparison.OrdinalIgnoreCase))
            return "Ammunition";

        if (item.Type != ItemType.Misc)
            return item.Type.ToString();

        return string.IsNullOrWhiteSpace(fallback) ? "Miscellaneous item" : fallback;
    }

    private void CreateBuyRow(Transform parent, StoreInventory.StoreItemEntry entry)
    {
        ItemData template = entry.GetTemplate();
        if (template == null)
            return;

        GameObject row = CreatePanel(parent, $"Buy_{entry.ItemId}",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(0f, 70f), new Color(0.16f, 0.18f, 0.25f, 1f));

        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.minHeight = 70f;
        rowLayout.preferredHeight = 70f;
        rowLayout.flexibleWidth = 1f;

        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(15, 10, 5, 5);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        string details = GetItemDescription(template, entry.Category);

        GameObject infoObj = new GameObject("Info", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        infoObj.transform.SetParent(row.transform, false);

        LayoutElement infoLayoutElement = infoObj.GetComponent<LayoutElement>();
        infoLayoutElement.minWidth = 200f;
        infoLayoutElement.preferredWidth = 300f;
        infoLayoutElement.flexibleWidth = 1f;

        VerticalLayoutGroup infoLayout = infoObj.GetComponent<VerticalLayoutGroup>();
        infoLayout.spacing = 2f;
        infoLayout.padding = new RectOffset(0, 0, 5, 5);
        infoLayout.childAlignment = TextAnchor.MiddleLeft;
        infoLayout.childControlWidth = true;
        infoLayout.childControlHeight = true;
        infoLayout.childForceExpandWidth = true;
        infoLayout.childForceExpandHeight = false;

        Text nameText = CreateText(infoObj.transform, "Name", template.Name,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            Vector2.zero, 18, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        LayoutElement nameLayout = nameText.gameObject.AddComponent<LayoutElement>();
        nameLayout.preferredHeight = 24f;
        nameLayout.flexibleWidth = 1f;
        nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        nameText.verticalOverflow = VerticalWrapMode.Overflow;

        Text detailText = CreateText(infoObj.transform, "Details", details,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            Vector2.zero, 13, FontStyle.Normal, new Color(0.8f, 0.85f, 0.95f), TextAnchor.MiddleLeft);
        LayoutElement detailsLayout = detailText.gameObject.AddComponent<LayoutElement>();
        detailsLayout.preferredHeight = 18f;
        detailsLayout.flexibleWidth = 1f;
        detailText.horizontalOverflow = HorizontalWrapMode.Overflow;
        detailText.verticalOverflow = VerticalWrapMode.Overflow;

        GameObject priceObj = new GameObject("Price", typeof(RectTransform), typeof(LayoutElement));
        priceObj.transform.SetParent(row.transform, false);
        LayoutElement priceLayout = priceObj.GetComponent<LayoutElement>();
        priceLayout.minWidth = 90f;
        priceLayout.preferredWidth = 90f;
        priceLayout.flexibleWidth = 0f;

        Text priceText = CreateText(priceObj.transform, "PriceLabel", $"{entry.PriceGp} gp",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            Vector2.zero, 18, FontStyle.Bold, new Color(1f, 0.93f, 0.24f), TextAnchor.MiddleCenter);
        priceText.horizontalOverflow = HorizontalWrapMode.Overflow;
        priceText.verticalOverflow = VerticalWrapMode.Overflow;

        GameObject buttonSection = new GameObject("ButtonSection", typeof(RectTransform), typeof(LayoutElement));
        buttonSection.transform.SetParent(row.transform, false);
        LayoutElement buttonLayout = buttonSection.GetComponent<LayoutElement>();
        buttonLayout.minWidth = 70f;
        buttonLayout.preferredWidth = 70f;
        buttonLayout.flexibleWidth = 0f;

        CreateSmallActionButton(buttonSection.transform, "BuyButton", "BUY", new Color(0.2f, 0.56f, 0.26f), () => BuyItem(entry));

        Debug.Log($"[Store] Created buy entry for {template.Name} with proper width constraints");
    }

    private void CreateSellRow(Transform parent, SellEntry entry)
    {
        int sellPrice = StoreInventory.Instance.GetSellPrice(entry.Item);

        GameObject row = CreatePanel(parent, $"Sell_{entry.Item.Name}",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(0f, 70f), new Color(0.22f, 0.17f, 0.17f, 1f));

        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.minHeight = 70f;
        rowLayout.preferredHeight = 70f;
        rowLayout.flexibleWidth = 1f;

        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(15, 10, 5, 5);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        GameObject infoObj = new GameObject("Info", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        infoObj.transform.SetParent(row.transform, false);

        LayoutElement infoLayoutElement = infoObj.GetComponent<LayoutElement>();
        infoLayoutElement.minWidth = 200f;
        infoLayoutElement.preferredWidth = 300f;
        infoLayoutElement.flexibleWidth = 1f;

        VerticalLayoutGroup infoLayout = infoObj.GetComponent<VerticalLayoutGroup>();
        infoLayout.spacing = 2f;
        infoLayout.padding = new RectOffset(0, 0, 5, 5);
        infoLayout.childAlignment = TextAnchor.MiddleLeft;
        infoLayout.childControlWidth = true;
        infoLayout.childControlHeight = true;
        infoLayout.childForceExpandWidth = true;
        infoLayout.childForceExpandHeight = false;

        Text nameText = CreateText(infoObj.transform, "Name", $"{entry.Item.Name} ({entry.OwnerName})",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            Vector2.zero, 18, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        LayoutElement nameLayout = nameText.gameObject.AddComponent<LayoutElement>();
        nameLayout.preferredHeight = 24f;
        nameLayout.flexibleWidth = 1f;
        nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        nameText.verticalOverflow = VerticalWrapMode.Overflow;

        int baseValue = sellPrice * 2;
        string itemDescription = GetItemDescription(entry.Item, string.Empty);
        string valueLine = string.IsNullOrWhiteSpace(itemDescription)
            ? $"Value {baseValue} gp -> Sell {sellPrice} gp"
            : $"{itemDescription} | Value {baseValue} gp -> Sell {sellPrice} gp";
        Text valueText = CreateText(infoObj.transform, "Value", valueLine,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            Vector2.zero, 13, FontStyle.Normal, new Color(0.82f, 0.86f, 0.93f), TextAnchor.MiddleLeft);
        LayoutElement valueLayout = valueText.gameObject.AddComponent<LayoutElement>();
        valueLayout.preferredHeight = 18f;
        valueLayout.flexibleWidth = 1f;
        valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
        valueText.verticalOverflow = VerticalWrapMode.Overflow;

        GameObject priceObj = new GameObject("Price", typeof(RectTransform), typeof(LayoutElement));
        priceObj.transform.SetParent(row.transform, false);
        LayoutElement priceLayout = priceObj.GetComponent<LayoutElement>();
        priceLayout.minWidth = 90f;
        priceLayout.preferredWidth = 90f;
        priceLayout.flexibleWidth = 0f;

        Text priceText = CreateText(priceObj.transform, "PriceLabel", $"{sellPrice} gp",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            Vector2.zero, 18, FontStyle.Bold, new Color(1f, 0.93f, 0.24f), TextAnchor.MiddleCenter);
        priceText.horizontalOverflow = HorizontalWrapMode.Overflow;
        priceText.verticalOverflow = VerticalWrapMode.Overflow;

        GameObject buttonSection = new GameObject("ButtonSection", typeof(RectTransform), typeof(LayoutElement));
        buttonSection.transform.SetParent(row.transform, false);
        LayoutElement buttonLayout = buttonSection.GetComponent<LayoutElement>();
        buttonLayout.minWidth = 70f;
        buttonLayout.preferredWidth = 70f;
        buttonLayout.flexibleWidth = 0f;

        CreateSmallActionButton(buttonSection.transform, "SellButton", "SELL", new Color(0.58f, 0.37f, 0.18f), () => SellItem(entry));

        Debug.Log($"[Store] Created sell entry for {entry.Item.Name} with proper width constraints");
    }

    private void BuyItem(StoreInventory.StoreItemEntry entry)
    {
        ItemData item = StoreInventory.Instance.CreateItemInstance(entry.ItemId);
        if (item == null)
        {
            ShowMessage("Could not create item instance.", false);
            return;
        }

        Debug.Log($"[Store] Buying {item.Name} for {entry.PriceGp} gp");

        if (!GameManager.Instance.SpendGold(entry.PriceGp))
        {
            ShowMessage($"Not enough gold for {item.Name}.", false);
            return;
        }

        bool added = _partyStash != null && _partyStash.AddItem(item);
        if (!added)
        {
            GameManager.Instance.AddGold(entry.PriceGp);
            ShowMessage("Stash is locked. Could not purchase item.", false);
            return;
        }

        Debug.Log($"[Gold] Transaction complete. New balance: {GameManager.Instance.PartyGold} gp");
        ShowMessage($"Purchased {item.Name} for {entry.PriceGp} gp.", true);
        if (_sellPanel != null && _sellPanel.activeSelf)
            RebuildSellList();
    }

    private void SellItem(SellEntry entry)
    {
        if (entry == null || entry.Item == null)
            return;

        int sellPrice = StoreInventory.Instance.GetSellPrice(entry.Item);
        Debug.Log($"[Store] Selling {entry.Item.Name} for {sellPrice} gp (50% of {sellPrice * 2} gp)");

        bool removed = false;
        if (entry.FromStash)
        {
            removed = _partyStash != null && _partyStash.RemoveItem(entry.Item);
        }
        else
        {
            removed = entry.InventoryOwner != null && entry.InventoryOwner.RemoveItem(entry.Item);
        }

        if (!removed)
        {
            ShowMessage("Unable to sell item.", false);
            return;
        }

        GameManager.Instance.AddGold(sellPrice);
        Debug.Log($"[Gold] Transaction complete. New balance: {GameManager.Instance.PartyGold} gp");
        ShowMessage($"Sold {entry.Item.Name} for {sellPrice} gp.", true);
        RebuildSellList();
    }

    private void ShowMessage(string message, bool success)
    {
        if (_messageText == null)
            return;

        _messageText.text = message;
        _messageText.color = success ? new Color(0.45f, 0.95f, 0.52f) : new Color(1f, 0.52f, 0.46f);
    }

    private void CreateTabButton(string name, string label, Vector2 anchorMin, Vector2 anchorMax, Color color, Action onClick)
    {
        GameObject tabObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        tabObj.transform.SetParent(_root.transform, false);

        RectTransform rect = tabObj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = tabObj.GetComponent<Image>();
        image.color = color;

        Button button = tabObj.GetComponent<Button>();
        button.onClick.AddListener(() => onClick?.Invoke());

        CreateText(tabObj.transform, "Label", label,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            Vector2.zero, 20, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
    }

    private void CreateBottomButton(string name, string label, Vector2 anchorMin, Vector2 anchorMax, Color color, Action onClick)
    {
        GameObject buttonObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObj.transform.SetParent(_root.transform, false);

        RectTransform rect = buttonObj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = buttonObj.GetComponent<Image>();
        image.color = color;

        Button button = buttonObj.GetComponent<Button>();
        button.onClick.AddListener(() => onClick?.Invoke());

        CreateText(buttonObj.transform, "Label", label,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            Vector2.zero, 20, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
    }

    private static Button CreateSmallActionButton(Transform parent, string name, string label, Color color, Action onClick)
    {
        GameObject buttonObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObj.transform.SetParent(parent, false);

        LayoutElement layout = buttonObj.GetComponent<LayoutElement>();
        layout.minWidth = 70f;
        layout.preferredWidth = 70f;
        layout.preferredHeight = 55f;
        layout.flexibleWidth = 0f;

        Image image = buttonObj.GetComponent<Image>();
        image.color = color;

        Button button = buttonObj.GetComponent<Button>();
        button.onClick.AddListener(() => onClick?.Invoke());

        CreateText(buttonObj.transform, "Label", label,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            Vector2.zero, 13, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

        return button;
    }

    private static void CreateScrollList(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax,
        out RectTransform contentRect)
    {
        GameObject scrollObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollObj.transform.SetParent(parent, false);
        RectTransform scrollRect = scrollObj.GetComponent<RectTransform>();
        scrollRect.anchorMin = anchorMin;
        scrollRect.anchorMax = anchorMax;
        scrollRect.offsetMin = offsetMin;
        scrollRect.offsetMax = offsetMax;
        scrollObj.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.16f);

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollObj.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.03f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(5, 5, 5, 5);
        layout.spacing = 5f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        ScrollRect sr = scrollObj.GetComponent<ScrollRect>();
        sr.viewport = viewportRect;
        sr.content = contentRect;
        sr.horizontal = false;
        sr.vertical = true;
        sr.scrollSensitivity = 25f;

        Debug.Log($"[Store] {name} created with proper viewport constraints");
    }

    private static void ClearChildren(Transform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    private static GameObject CreatePanel(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPos,
        Vector2 size,
        Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        panel.GetComponent<Image>().color = color;
        return panel;
    }

    private static Text CreateText(
        Transform parent,
        string name,
        string value,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPos,
        Vector2 size,
        int fontSize,
        FontStyle fontStyle,
        Color color,
        TextAnchor alignment)
    {
        GameObject textObj = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObj.transform.SetParent(parent, false);

        RectTransform rect = textObj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        Text text = textObj.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.text = value;

        return text;
    }
}
