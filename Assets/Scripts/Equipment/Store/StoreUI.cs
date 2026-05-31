using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pre-combat merchant interface with Buy/Sell tabs.
/// </summary>
public class StoreUI : MonoBehaviour
{
    private sealed class SellItemSource
    {
        public ItemData Item;
        public bool FromStash;
        public CharacterInventory InventoryOwner;
        public string OwnerName;
    }

    private sealed class SellStack
    {
        public string StackKey;
        public string ItemName;
        public ItemData RepresentativeItem;
        public int UnitSellPrice;
        public readonly List<SellItemSource> Sources = new List<SellItemSource>();

        public int Quantity => Sources.Count;

        public void AddSource(SellItemSource source)
        {
            if (source == null || source.Item == null)
                return;

            Sources.Add(source);
        }
    }

    private GameObject _root;
    private GameObject _buyPanel;
    private GameObject _sellPanel;
    private Text _goldText;
    private Text _messageText;

    private RectTransform _buyContent;            // Left panel item list content
    private RectTransform _buyDetailContent;      // Right panel detail scroll content
    private RectTransform _sellContent;
    private RectTransform _sellCharacterFilterRoot;
    private RectTransform _sellCharacterButtonsRoot;
    private RectTransform _sellCategoryFilterRoot;
    private readonly Dictionary<string, Image> _sellCharacterButtonImages = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Image> _sellCategoryButtonImages = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
    private enum SortMode
    {
        Alphabetical,
        Price
    }

    private enum SortDirection
    {
        Ascending,
        Descending
    }

    private string _currentBuyCategory = "All";
    private string _currentBuySubFilter = "All";
    private string _currentSellCategory = "All";
    private string _currentSellCharacterKey = SellCharacterStashKey;
    private CharacterController _selectedSellCharacter;

    private SortMode _buySortMode = SortMode.Alphabetical;
    private SortDirection _buySortDirection = SortDirection.Ascending;
    private SortMode _sellSortMode = SortMode.Alphabetical;
    private SortDirection _sellSortDirection = SortDirection.Ascending;

    private RectTransform _sellSortRoot;

    // Buy-side toolbar controls
    private string _buySearchQuery = string.Empty;
    private InputField _buySearchField;
    private Dropdown _categoryDropdown;
    private Dropdown _subFilterDropdown;
    private Dropdown _sortDropdown;
    private bool _showAffordableOnly;
    private Image _affordableToggleImage;
    private Text _buyDetailTitleText;
    private Text _buyDetailBodyText;
    private StoreInventory.StoreItemEntry _selectedBuyEntry;
    private readonly Dictionary<string, Image> _buyRowImages = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<SortMode, Image> _sellSortButtonImages = new Dictionary<SortMode, Image>();
    private Text _sellSortDirectionText;

    private const string SellCharacterStashKey = "__stash__";

    private PartyStash _partyStash;
    private List<CharacterController> _partyMembers = new List<CharacterController>();
    private Action _onBackToMenu;
    private Action _onStartEncounter;
    private GameObject _sellQuantityDialog;

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

        _currentSellCharacterKey = SellCharacterStashKey;
        _selectedSellCharacter = null;
        _currentSellCategory = "All";

        _buySortMode = SortMode.Alphabetical;
        _buySortDirection = SortDirection.Ascending;
        _sellSortMode = SortMode.Alphabetical;
        _sellSortDirection = SortDirection.Ascending;
        _buySearchQuery = string.Empty;
        _currentBuyCategory = "All";
        _currentBuySubFilter = "All";
        _showAffordableOnly = false;
        _selectedBuyEntry = null;
        if (_buySearchField != null)
            _buySearchField.SetTextWithoutNotify(string.Empty);
        if (_categoryDropdown != null)
            _categoryDropdown.value = 0;
        if (_subFilterDropdown != null)
        {
            _subFilterDropdown.gameObject.SetActive(false);
        }
        if (_affordableToggleImage != null)
            _affordableToggleImage.color = new Color(0.3f, 0.3f, 0.4f, 1f);

        BuildSellCharacterOptions(_sellCharacterButtonsRoot);
        RefreshSellCharacterButtons();
        RefreshSellCategoryButtons();
        RefreshSellSortButtons();

        ShowBuyPanel();

        Debug.Log($"[Store] Store opened with {StoreInventory.Instance.GetItemsByCategory("All").Count} items");
        Debug.Log($"[Store] Party has {GameManager.Instance.PartyGold} gp");
    }

    public void Close()
    {
        UnsubscribeGoldEvents();

        if (_sellQuantityDialog != null)
        {
            Destroy(_sellQuantityDialog);
            _sellQuantityDialog = null;
        }

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

        Debug.Log("[Store] === SORT FUNCTIONALITY ADDED ===");
        Debug.Log("[Store] BUY tab sort:");
        Debug.Log("[Store]   - Name - alphabetical sorting");
        Debug.Log("[Store]   - Price - sort by item value");
        Debug.Log("[Store]   - ↑ ASC / ↓ DESC - direction toggle");
        Debug.Log("[Store] SELL tab sort:");
        Debug.Log("[Store]   - Name - alphabetical sorting");
        Debug.Log("[Store]   - Price - sort by sell price");
        Debug.Log("[Store]   - ↑ ASC / ↓ DESC - direction toggle");
        Debug.Log("[Store] Sort areas layout:");
        Debug.Log("[Store]   - Buy sort: 84-93%");
        Debug.Log("[Store]   - Sell sort: 72-80%");
        Debug.Log("[Store] Active sort highlighted in green");

        _root.SetActive(false);
    }

    private void BuildBuyPanel()
    {
        _buyPanel = CreatePanel(_root.transform, "BuyPanel",
            new Vector2(0.1f, 0.17f), new Vector2(0.9f, 0.83f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, new Color(0.06f, 0.08f, 0.12f, 0.94f));

        // --- TOOLBAR (top 12% of buy panel) ---
        BuildBuyToolbar();

        // --- SPLIT PANELS (bottom 88%) ---
        // Left panel: item list (60% width)
        GameObject leftPanel = CreatePanel(_buyPanel.transform, "BuyLeftPanel",
            new Vector2(0f, 0f), new Vector2(0.58f, 0.88f), new Vector2(0f, 0f),
            Vector2.zero, Vector2.zero, new Color(0.05f, 0.06f, 0.1f, 0.8f));

        CreateScrollList(leftPanel.transform, "BuyItemScroll",
            Vector2.zero, Vector2.one,
            new Vector2(4f, 4f), new Vector2(-4f, -4f), out _buyContent);

        // Right panel: item details (40% width)
        GameObject rightPanel = CreatePanel(_buyPanel.transform, "BuyRightPanel",
            new Vector2(0.59f, 0f), new Vector2(1f, 0.88f), new Vector2(0f, 0f),
            Vector2.zero, Vector2.zero, new Color(0.08f, 0.09f, 0.14f, 0.9f));

        // Detail scroll area
        CreateScrollList(rightPanel.transform, "BuyDetailScroll",
            Vector2.zero, Vector2.one,
            new Vector2(4f, 4f), new Vector2(-4f, -4f), out _buyDetailContent);

        // Default detail placeholder
        _buyDetailTitleText = CreateText(_buyDetailContent, "DetailTitle", "Select an Item",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 1f), Vector2.zero,
            new Vector2(0f, 30f), 20, FontStyle.Bold,
            new Color(0.9f, 0.85f, 0.5f), TextAnchor.MiddleCenter);
        LayoutElement titleLE = _buyDetailTitleText.gameObject.AddComponent<LayoutElement>();
        titleLE.preferredHeight = 36f;
        titleLE.flexibleWidth = 1f;

        _buyDetailBodyText = CreateText(_buyDetailContent, "DetailBody", "Click an item on the left to view its details here.",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 1f), Vector2.zero,
            new Vector2(0f, 200f), 15, FontStyle.Normal,
            new Color(0.78f, 0.82f, 0.92f), TextAnchor.UpperLeft);
        _buyDetailBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _buyDetailBodyText.verticalOverflow = VerticalWrapMode.Overflow;
        LayoutElement bodyLE = _buyDetailBodyText.gameObject.AddComponent<LayoutElement>();
        bodyLE.preferredHeight = 400f;
        bodyLE.flexibleWidth = 1f;
        bodyLE.flexibleHeight = 1f;

        Debug.Log("[Store] Buy panel built with split layout + toolbar");
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
        CreateSellSortArea();

        CreateScrollList(_sellPanel.transform, "SellScroll", new Vector2(0f, 0f), new Vector2(1f, 0.72f), new Vector2(16f, 16f), new Vector2(-16f, -4f), out _sellContent);

        _sellPanel.SetActive(false);
    }

    // ========== BUY TOOLBAR ==========

    /// <summary>Category options for the main dropdown (alphabetised).</summary>
    private static readonly string[] BuyCategoryOptions = new string[]
    {
        "All",
        "Ammunition",
        "Armor",
        "Gear",
        "Potions",
        "Rings",
        "Rods",
        "Scrolls",
        "Shields",
        "Wands",
        "Weapons",
        "Wondrous Items"
    };

    /// <summary>
    /// Builds the toolbar at the top of the buy panel containing:
    /// Search | Category dropdown | Sub-filter dropdown | Sort dropdown | Affordable toggle
    /// </summary>
    private void BuildBuyToolbar()
    {
        GameObject toolbar = CreatePanel(_buyPanel.transform, "BuyToolbar",
            new Vector2(0f, 0.89f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, new Color(0.12f, 0.13f, 0.18f, 0.9f));

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // --- Search field (left 22%) ---
        CreateText(toolbar.transform, "SearchLbl", "Search:",
            new Vector2(0.005f, 0f), new Vector2(0.06f, 1f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero, 11, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);

        _buySearchField = BuildToolbarInputField(toolbar.transform, "BuySearch",
            new Vector2(0.065f, 0.12f), new Vector2(0.22f, 0.88f), "Search items...", font);
        _buySearchField.onValueChanged.AddListener(OnBuySearchChanged);

        // --- Category dropdown (next 20%) ---
        CreateText(toolbar.transform, "CatLbl", "Category:",
            new Vector2(0.225f, 0f), new Vector2(0.30f, 1f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero, 11, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);

        List<Dropdown.OptionData> catOpts = new List<Dropdown.OptionData>();
        for (int i = 0; i < BuyCategoryOptions.Length; i++)
            catOpts.Add(new Dropdown.OptionData(BuyCategoryOptions[i]));
        _categoryDropdown = BuildToolbarDropdown(toolbar.transform, "CategoryDD",
            new Vector2(0.30f, 0.10f), new Vector2(0.46f, 0.90f), catOpts, 0, font);
        _categoryDropdown.onValueChanged.AddListener(OnCategoryChanged);

        // --- Sub-filter dropdown (next 17%) - hidden by default ---
        _subFilterDropdown = BuildToolbarDropdown(toolbar.transform, "SubFilterDD",
            new Vector2(0.47f, 0.10f), new Vector2(0.63f, 0.90f),
            new List<Dropdown.OptionData> { new Dropdown.OptionData("All") }, 0, font);
        _subFilterDropdown.onValueChanged.AddListener(OnSubFilterChanged);
        _subFilterDropdown.gameObject.SetActive(false);

        // --- Sort dropdown (next 14%) ---
        CreateText(toolbar.transform, "SortLbl", "Sort:",
            new Vector2(0.64f, 0f), new Vector2(0.68f, 1f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero, 11, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);

        List<Dropdown.OptionData> sortOpts = new List<Dropdown.OptionData>
        {
            new Dropdown.OptionData("Name ↑"),
            new Dropdown.OptionData("Name ↓"),
            new Dropdown.OptionData("Price ↑"),
            new Dropdown.OptionData("Price ↓")
        };
        _sortDropdown = BuildToolbarDropdown(toolbar.transform, "SortDD",
            new Vector2(0.685f, 0.10f), new Vector2(0.80f, 0.90f), sortOpts, 0, font);
        _sortDropdown.onValueChanged.AddListener(OnSortChanged);

        // --- Affordable toggle (right 19%) ---
        GameObject affObj = new GameObject("AffordableBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        affObj.transform.SetParent(toolbar.transform, false);
        RectTransform affRect = affObj.GetComponent<RectTransform>();
        affRect.anchorMin = new Vector2(0.81f, 0.12f);
        affRect.anchorMax = new Vector2(0.995f, 0.88f);
        affRect.offsetMin = Vector2.zero;
        affRect.offsetMax = Vector2.zero;
        _affordableToggleImage = affObj.GetComponent<Image>();
        _affordableToggleImage.color = new Color(0.3f, 0.3f, 0.4f, 1f);
        affObj.GetComponent<Button>().onClick.AddListener(ToggleAffordable);
        CreateText(affObj.transform, "AffLbl", "Affordable",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, 12, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
    }

    // ========== TOOLBAR WIDGET BUILDERS ==========

    private InputField BuildToolbarInputField(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, string placeholder, Font font)
    {
        GameObject fieldObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
        fieldObj.transform.SetParent(parent, false);
        RectTransform fieldRect = fieldObj.GetComponent<RectTransform>();
        fieldRect.anchorMin = anchorMin;
        fieldRect.anchorMax = anchorMax;
        fieldRect.offsetMin = Vector2.zero;
        fieldRect.offsetMax = Vector2.zero;
        fieldObj.GetComponent<Image>().color = new Color(0.88f, 0.88f, 0.92f, 1f);

        InputField inputField = fieldObj.GetComponent<InputField>();
        inputField.targetGraphic = fieldObj.GetComponent<Image>();

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObj.transform.SetParent(fieldObj.transform, false);
        Text text = textObj.GetComponent<Text>();
        text.font = font;
        text.fontSize = 13;
        text.color = new Color(0.1f, 0.1f, 0.12f, 1f);
        text.alignment = TextAnchor.MiddleLeft;
        text.supportRichText = false;
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(6f, 2f);
        textRect.offsetMax = new Vector2(-6f, -2f);

        GameObject phObj = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
        phObj.transform.SetParent(fieldObj.transform, false);
        Text phText = phObj.GetComponent<Text>();
        phText.font = font;
        phText.fontSize = 13;
        phText.fontStyle = FontStyle.Italic;
        phText.color = new Color(0.45f, 0.45f, 0.5f, 1f);
        phText.alignment = TextAnchor.MiddleLeft;
        phText.text = placeholder;
        RectTransform phRect = phText.GetComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = new Vector2(6f, 2f);
        phRect.offsetMax = new Vector2(-6f, -2f);

        inputField.textComponent = text;
        inputField.placeholder = phText;
        inputField.lineType = InputField.LineType.SingleLine;
        inputField.characterLimit = 40;
        return inputField;
    }

    private Dropdown BuildToolbarDropdown(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        List<Dropdown.OptionData> options, int defaultIndex, Font font)
    {
        GameObject ddObj = new GameObject(name);
        ddObj.transform.SetParent(parent, false);
        RectTransform ddRT = ddObj.AddComponent<RectTransform>();
        ddRT.anchorMin = anchorMin;
        ddRT.anchorMax = anchorMax;
        ddRT.offsetMin = Vector2.zero;
        ddRT.offsetMax = Vector2.zero;
        Image ddBG = ddObj.AddComponent<Image>();
        ddBG.color = new Color(0.18f, 0.18f, 0.28f, 1f);
        Dropdown dropdown = ddObj.AddComponent<Dropdown>();

        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(ddObj.transform, false);
        RectTransform labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = new Vector2(6, 2);
        labelRT.offsetMax = new Vector2(-22, -2);
        Text labelText = labelGO.AddComponent<Text>();
        labelText.font = font;
        labelText.fontSize = 13;
        labelText.color = Color.white;
        labelText.alignment = TextAnchor.MiddleLeft;

        GameObject arrowGO = new GameObject("Arrow");
        arrowGO.transform.SetParent(ddObj.transform, false);
        RectTransform arrowRT = arrowGO.AddComponent<RectTransform>();
        arrowRT.anchorMin = new Vector2(1, 0);
        arrowRT.anchorMax = new Vector2(1, 1);
        arrowRT.pivot = new Vector2(1, 0.5f);
        arrowRT.anchoredPosition = new Vector2(-3, 0);
        arrowRT.sizeDelta = new Vector2(18, 0);
        Text arrowText = arrowGO.AddComponent<Text>();
        arrowText.font = font;
        arrowText.fontSize = 12;
        arrowText.color = new Color(0.7f, 0.7f, 0.9f);
        arrowText.text = "\u25BC";
        arrowText.alignment = TextAnchor.MiddleCenter;

        // Template
        GameObject templateGO = new GameObject("Template");
        templateGO.transform.SetParent(ddObj.transform, false);
        RectTransform tempRT = templateGO.AddComponent<RectTransform>();
        tempRT.anchorMin = new Vector2(0, 0);
        tempRT.anchorMax = new Vector2(1, 0);
        tempRT.pivot = new Vector2(0.5f, 1);
        tempRT.anchoredPosition = Vector2.zero;
        tempRT.sizeDelta = new Vector2(0, 200);
        Image tempImg = templateGO.AddComponent<Image>();
        tempImg.color = new Color(0.12f, 0.12f, 0.2f, 0.98f);
        ScrollRect tempScroll = templateGO.AddComponent<ScrollRect>();

        GameObject tempVP = new GameObject("Viewport");
        tempVP.transform.SetParent(templateGO.transform, false);
        RectTransform vpRT = tempVP.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero;
        vpRT.offsetMax = Vector2.zero;
        tempVP.AddComponent<Image>().color = Color.white;
        tempVP.AddComponent<Mask>().showMaskGraphic = false;
        tempScroll.viewport = vpRT;

        GameObject tempContent = new GameObject("Content");
        tempContent.transform.SetParent(tempVP.transform, false);
        RectTransform tcRT = tempContent.AddComponent<RectTransform>();
        tcRT.anchorMin = new Vector2(0, 1);
        tcRT.anchorMax = new Vector2(1, 1);
        tcRT.pivot = new Vector2(0.5f, 1);
        tcRT.anchoredPosition = Vector2.zero;
        tcRT.sizeDelta = new Vector2(0, 28);
        tempScroll.content = tcRT;

        GameObject itemGO = new GameObject("Item");
        itemGO.transform.SetParent(tempContent.transform, false);
        RectTransform itemRT = itemGO.AddComponent<RectTransform>();
        itemRT.anchorMin = new Vector2(0, 0.5f);
        itemRT.anchorMax = new Vector2(1, 0.5f);
        itemRT.sizeDelta = new Vector2(0, 26);
        Toggle itemToggle = itemGO.AddComponent<Toggle>();

        GameObject itemBG = new GameObject("Item Background");
        itemBG.transform.SetParent(itemGO.transform, false);
        RectTransform ibRT = itemBG.AddComponent<RectTransform>();
        ibRT.anchorMin = Vector2.zero;
        ibRT.anchorMax = Vector2.one;
        ibRT.offsetMin = Vector2.zero;
        ibRT.offsetMax = Vector2.zero;
        Image ibImg = itemBG.AddComponent<Image>();
        ibImg.color = new Color(0.15f, 0.15f, 0.25f, 1f);

        GameObject itemCheck = new GameObject("Item Checkmark");
        itemCheck.transform.SetParent(itemBG.transform, false);
        RectTransform icRT = itemCheck.AddComponent<RectTransform>();
        icRT.anchorMin = new Vector2(0, 0);
        icRT.anchorMax = new Vector2(0, 1);
        icRT.pivot = new Vector2(0, 0.5f);
        icRT.anchoredPosition = new Vector2(4, 0);
        icRT.sizeDelta = new Vector2(18, 0);
        Image icImg = itemCheck.AddComponent<Image>();
        icImg.color = new Color(0.3f, 0.8f, 0.3f);

        GameObject itemLabel = new GameObject("Item Label");
        itemLabel.transform.SetParent(itemGO.transform, false);
        RectTransform ilRT = itemLabel.AddComponent<RectTransform>();
        ilRT.anchorMin = Vector2.zero;
        ilRT.anchorMax = Vector2.one;
        ilRT.offsetMin = new Vector2(24, 2);
        ilRT.offsetMax = new Vector2(-4, -2);
        Text ilText = itemLabel.AddComponent<Text>();
        ilText.font = font;
        ilText.fontSize = 13;
        ilText.color = Color.white;
        ilText.alignment = TextAnchor.MiddleLeft;

        itemToggle.targetGraphic = ibImg;
        itemToggle.graphic = icImg;
        itemToggle.isOn = false;
        templateGO.SetActive(false);

        dropdown.targetGraphic = ddBG;
        dropdown.template = tempRT;
        dropdown.captionText = labelText;
        dropdown.itemText = ilText;
        dropdown.options = options ?? new List<Dropdown.OptionData>();
        dropdown.value = Mathf.Clamp(defaultIndex, 0, Mathf.Max(0, dropdown.options.Count - 1));
        dropdown.RefreshShownValue();
        return dropdown;
    }

    // ========== TOOLBAR EVENT HANDLERS ==========

    private void OnBuySearchChanged(string value)
    {
        _buySearchQuery = value ?? string.Empty;
        RebuildBuyList();
    }

    private void OnCategoryChanged(int index)
    {
        _currentBuyCategory = index >= 0 && index < BuyCategoryOptions.Length
            ? BuyCategoryOptions[index] : "All";
        _currentBuySubFilter = "All";
        _buySearchQuery = string.Empty;
        if (_buySearchField != null)
            _buySearchField.SetTextWithoutNotify(string.Empty);
        RefreshSubFilterDropdown();
        RebuildBuyList();
        Debug.Log($"[Store] Category changed to: {_currentBuyCategory}");
    }

    private void OnSubFilterChanged(int index)
    {
        if (_subFilterDropdown != null && _subFilterDropdown.options != null
            && index >= 0 && index < _subFilterDropdown.options.Count)
        {
            _currentBuySubFilter = _subFilterDropdown.options[index].text;
        }
        else
        {
            _currentBuySubFilter = "All";
        }
        RebuildBuyList();
        Debug.Log($"[Store] Sub-filter changed to: {_currentBuySubFilter}");
    }

    private void OnSortChanged(int index)
    {
        switch (index)
        {
            case 0: _buySortMode = SortMode.Alphabetical; _buySortDirection = SortDirection.Ascending; break;
            case 1: _buySortMode = SortMode.Alphabetical; _buySortDirection = SortDirection.Descending; break;
            case 2: _buySortMode = SortMode.Price; _buySortDirection = SortDirection.Ascending; break;
            case 3: _buySortMode = SortMode.Price; _buySortDirection = SortDirection.Descending; break;
        }
        RebuildBuyList();
    }

    private void ToggleAffordable()
    {
        _showAffordableOnly = !_showAffordableOnly;
        if (_affordableToggleImage != null)
        {
            _affordableToggleImage.color = _showAffordableOnly
                ? new Color(0.25f, 0.55f, 0.3f, 1f)
                : new Color(0.3f, 0.3f, 0.4f, 1f);
        }
        RebuildBuyList();
        Debug.Log($"[Store] Affordable filter: {_showAffordableOnly}");
    }

    // ========== SUB-FILTER LOGIC ==========

    private void RefreshSubFilterDropdown()
    {
        if (_subFilterDropdown == null)
            return;

        List<string> subOptions = GetSubFilterOptions(_currentBuyCategory);
        if (subOptions == null || subOptions.Count == 0)
        {
            _subFilterDropdown.gameObject.SetActive(false);
            return;
        }

        _subFilterDropdown.gameObject.SetActive(true);
        _subFilterDropdown.ClearOptions();
        List<Dropdown.OptionData> opts = new List<Dropdown.OptionData>();
        for (int i = 0; i < subOptions.Count; i++)
            opts.Add(new Dropdown.OptionData(subOptions[i]));
        _subFilterDropdown.AddOptions(opts);
        _subFilterDropdown.value = 0;
        _subFilterDropdown.RefreshShownValue();
        _currentBuySubFilter = "All";
    }

    private static List<string> GetSubFilterOptions(string category)
    {
        switch (category)
        {
            case "Weapons":
            case "Armor":
            case "Shields":
                return new List<string>
                {
                    "All", "Mundane", "+1", "+2", "+3", "+4", "+5",
                    "Special Properties", "Specific", "Special Material"
                };
            case "Wondrous Items":
                return new List<string> { "All", "Minor", "Medium", "Major" };
            case "Scrolls":
                return new List<string>
                {
                    "All", "Level 0 (Cantrips)", "Level 1", "Level 2", "Level 3",
                    "Level 4", "Level 5", "Level 6", "Level 7", "Level 8", "Level 9"
                };
            default:
                return null;
        }
    }

    // ========== CATEGORY + SUB-FILTER MATCHING ==========

    /// <summary>
    /// Maps a top-level dropdown category to the set of store categories it encompasses,
    /// then applies the current sub-filter to narrow the results.
    /// </summary>
    private List<StoreInventory.StoreItemEntry> GetFilteredBuyItems()
    {
        List<StoreInventory.StoreItemEntry> all = StoreInventory.Instance.GetItemsByCategory("All");
        if (string.Equals(_currentBuyCategory, "All", StringComparison.OrdinalIgnoreCase))
            return all;

        List<StoreInventory.StoreItemEntry> filtered = new List<StoreInventory.StoreItemEntry>();
        for (int i = 0; i < all.Count; i++)
        {
            StoreInventory.StoreItemEntry entry = all[i];
            if (entry == null) continue;
            if (MatchesBuyCategory(entry, _currentBuyCategory))
                filtered.Add(entry);
        }

        // Apply sub-filter
        if (!string.Equals(_currentBuySubFilter, "All", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(_currentBuySubFilter))
        {
            filtered = ApplySubFilter(filtered, _currentBuyCategory, _currentBuySubFilter);
        }

        return filtered;
    }

    private static bool MatchesBuyCategory(StoreInventory.StoreItemEntry entry, string uiCategory)
    {
        string cat = entry.Category ?? string.Empty;
        switch (uiCategory)
        {
            case "Weapons":
                return cat.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0;
            case "Armor":
                return string.Equals(cat, "Armor", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(cat, "Magic Armor", StringComparison.OrdinalIgnoreCase);
            case "Shields":
                return cat.IndexOf("Shield", StringComparison.OrdinalIgnoreCase) >= 0;
            case "Scrolls":
                return cat.StartsWith("Scroll", StringComparison.OrdinalIgnoreCase);
            case "Potions":
                return cat.StartsWith("Potion", StringComparison.OrdinalIgnoreCase);
            case "Wands":
                return cat.StartsWith("Wand", StringComparison.OrdinalIgnoreCase);
            case "Rings":
                return string.Equals(cat, "Rings", StringComparison.OrdinalIgnoreCase);
            case "Rods":
                return string.Equals(cat, "Rods", StringComparison.OrdinalIgnoreCase);
            case "Wondrous Items":
                return string.Equals(cat, "Wondrous Items", StringComparison.OrdinalIgnoreCase);
            case "Ammunition":
                return string.Equals(cat, "Ammunition", StringComparison.OrdinalIgnoreCase);
            case "Gear":
                return string.Equals(cat, "Gear", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(cat, "Spell Component", StringComparison.OrdinalIgnoreCase);
            default:
                return false;
        }
    }

    private static List<StoreInventory.StoreItemEntry> ApplySubFilter(
        List<StoreInventory.StoreItemEntry> items, string category, string subFilter)
    {
        if (items == null) return new List<StoreInventory.StoreItemEntry>();

        // Weapons / Armor / Shields sub-filters
        if (category == "Weapons" || category == "Armor" || category == "Shields")
        {
            if (subFilter == "Mundane")
                return items.FindAll(e => GetEnhBonus(e) == 0 && !HasSpecialMaterial(e));
            if (subFilter.StartsWith("+"))
            {
                int bonus;
                if (int.TryParse(subFilter.Substring(1), out bonus))
                    return items.FindAll(e => GetEnhBonus(e) == bonus);
            }
            if (subFilter == "Special Properties")
                return items.FindAll(e => { ItemData t = e.GetTemplate(); return t != null && t.IsEnchanted; });
            if (subFilter == "Specific")
                return items.FindAll(e => { ItemData t = e.GetTemplate(); return t != null && t.SpecificItemType != SpecificItemType.None; });
            if (subFilter == "Special Material")
                return items.FindAll(e => HasSpecialMaterial(e));
        }

        // Wondrous Items sub-filters (by price bracket: Minor <5k, Medium 5k-25k, Major >25k)
        if (category == "Wondrous Items")
        {
            if (subFilter == "Minor")
                return items.FindAll(e => e.PriceGp < 5000);
            if (subFilter == "Medium")
                return items.FindAll(e => e.PriceGp >= 5000 && e.PriceGp <= 25000);
            if (subFilter == "Major")
                return items.FindAll(e => e.PriceGp > 25000);
        }

        // Scrolls sub-filters (by spell level)
        if (category == "Scrolls")
        {
            if (subFilter.StartsWith("Level "))
            {
                string levelStr = subFilter.Replace("Level ", "").Replace(" (Cantrips)", "").Trim();
                int level;
                if (int.TryParse(levelStr, out level))
                    return items.FindAll(e =>
                    {
                        ItemData t = e.GetTemplate();
                        return t != null && t.ScrollSpellLevel == level;
                    });
            }
        }

        return items;
    }

    private static int GetEnhBonus(StoreInventory.StoreItemEntry entry)
    {
        if (entry == null) return 0;
        ItemData t = entry.GetTemplate();
        return t != null ? t.ResolveEnhancementBonus() : 0;
    }

    private static bool HasSpecialMaterial(StoreInventory.StoreItemEntry entry)
    {
        if (entry == null) return false;
        ItemData t = entry.GetTemplate();
        return t != null && t.Material != null && t.Material.MaterialType != ItemMaterialType.Standard;
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

        _currentSellCharacterKey = SellCharacterStashKey;
        _selectedSellCharacter = null;

        RefreshSellCharacterButtons();
        Debug.Log($"[Store] Character filter created: Stash + {_partyMembers.Count} characters (no 'All' option)");
        Debug.Log("[Store] === CHARACTER FILTER UPDATE ===");
        Debug.Log("[Store] ✅ Removed 'All' characters option");
        Debug.Log("[Store] ✅ Default selection: Stash");
        Debug.Log("[Store] Available filters:");
        Debug.Log("[Store]   - Stash (party shared items)");
        Debug.Log("[Store]   - Individual characters");
        Debug.Log("[Store] Players must choose specific source");
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
        _currentSellCharacterKey = string.IsNullOrWhiteSpace(key) ? SellCharacterStashKey : key;
        _selectedSellCharacter = string.Equals(_currentSellCharacterKey, SellCharacterStashKey, StringComparison.OrdinalIgnoreCase)
            ? null
            : character;

        RefreshSellCharacterButtons();
        RebuildSellList();

        string label = _currentSellCharacterKey == SellCharacterStashKey
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

    private void CreateSellSortArea()
    {
        if (_sellPanel == null)
            return;

        GameObject sortObj = new GameObject("SellSortFilter", typeof(RectTransform), typeof(Image));
        sortObj.transform.SetParent(_sellPanel.transform, false);

        _sellSortRoot = sortObj.GetComponent<RectTransform>();
        _sellSortRoot.anchorMin = new Vector2(0f, 0.72f);
        _sellSortRoot.anchorMax = new Vector2(1f, 0.80f);
        _sellSortRoot.offsetMin = Vector2.zero;
        _sellSortRoot.offsetMax = Vector2.zero;

        Image bg = sortObj.GetComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.2f, 0.55f);

        CreateText(sortObj.transform, "Label", "SORT BY:",
            new Vector2(0.01f, 0f), new Vector2(0.13f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, 14, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);

        GameObject rowObj = new GameObject("SortButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowObj.transform.SetParent(sortObj.transform, false);

        RectTransform rowRect = rowObj.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.14f, 0.08f);
        rowRect.anchorMax = new Vector2(0.99f, 0.92f);
        rowRect.offsetMin = Vector2.zero;
        rowRect.offsetMax = Vector2.zero;

        HorizontalLayoutGroup rowLayout = rowObj.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 8f;
        rowLayout.padding = new RectOffset(0, 0, 0, 0);
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = false;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;

        _sellSortButtonImages.Clear();

        Image alphaImage = CreateSortControlButton(rowObj.transform, "SellSortName", "Name", () => SetSellSortMode(SortMode.Alphabetical));
        if (alphaImage != null)
            _sellSortButtonImages[SortMode.Alphabetical] = alphaImage;

        Image priceImage = CreateSortControlButton(rowObj.transform, "SellSortPrice", "Price", () => SetSellSortMode(SortMode.Price));
        if (priceImage != null)
            _sellSortButtonImages[SortMode.Price] = priceImage;

        _sellSortDirectionText = CreateSortDirectionButton(rowObj.transform, "SellDirection", () => ToggleSellSortDirection());

        RefreshSellSortButtons();
        Debug.Log("[Store] Sell sort area created (Name, Price, Direction toggle)");
    }

    private Image CreateSortControlButton(Transform parent, string name, string label, Action onClick)
    {
        GameObject buttonObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObj.transform.SetParent(parent, false);

        LayoutElement layout = buttonObj.GetComponent<LayoutElement>();
        layout.minWidth = 110f;
        layout.preferredWidth = 110f;
        layout.preferredHeight = 32f;
        layout.flexibleWidth = 0f;

        Image image = buttonObj.GetComponent<Image>();
        image.color = new Color(0.3f, 0.3f, 0.4f, 1f);

        Button button = buttonObj.GetComponent<Button>();
        button.onClick.AddListener(() => onClick?.Invoke());

        CreateText(buttonObj.transform, "Label", label,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            Vector2.zero, 13, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

        return image;
    }

    private Text CreateSortDirectionButton(Transform parent, string name, Action onClick)
    {
        GameObject buttonObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObj.transform.SetParent(parent, false);

        LayoutElement layout = buttonObj.GetComponent<LayoutElement>();
        layout.minWidth = 100f;
        layout.preferredWidth = 100f;
        layout.preferredHeight = 32f;
        layout.flexibleWidth = 0f;

        Image image = buttonObj.GetComponent<Image>();
        image.color = new Color(0.4f, 0.5f, 0.6f, 1f);

        Button button = buttonObj.GetComponent<Button>();
        button.onClick.AddListener(() => onClick?.Invoke());

        return CreateText(buttonObj.transform, "Label", "↑ ASC",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            Vector2.zero, 13, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
    }

    private void SetSellSortMode(SortMode mode)
    {
        _sellSortMode = mode;
        RefreshSellSortButtons();
        RebuildSellList();

        Debug.Log($"[Store] Sell sort mode: {_sellSortMode}");
    }

    private void ToggleSellSortDirection()
    {
        _sellSortDirection = _sellSortDirection == SortDirection.Ascending
            ? SortDirection.Descending
            : SortDirection.Ascending;

        RefreshSellSortButtons();
        RebuildSellList();

        Debug.Log($"[Store] Sell sort direction: {_sellSortDirection}");
    }

    private void RefreshSellSortButtons()
    {
        foreach (KeyValuePair<SortMode, Image> kvp in _sellSortButtonImages)
        {
            if (kvp.Value == null)
                continue;

            bool selected = kvp.Key == _sellSortMode;
            kvp.Value.color = selected
                ? new Color(0.4f, 0.6f, 0.4f, 1f)
                : new Color(0.3f, 0.3f, 0.4f, 1f);
        }

        if (_sellSortDirectionText != null)
            _sellSortDirectionText.text = _sellSortDirection == SortDirection.Ascending ? "↑ ASC" : "↓ DESC";
    }

    private string GetSelectedSellCharacterKey()
    {
        if (!string.IsNullOrWhiteSpace(_currentSellCharacterKey))
            return _currentSellCharacterKey;

        if (_selectedSellCharacter != null)
            return _selectedSellCharacter.GetInstanceID().ToString();

        return SellCharacterStashKey;
    }

    private void ShowBuyPanel()
    {
        Debug.Log("[Store] Showing buy panel");
        if (_buyPanel != null) _buyPanel.SetActive(true);
        if (_sellPanel != null) _sellPanel.SetActive(false);
        RebuildBuyList();
    }

    private void ShowSellPanel()
    {
        if (_buyPanel != null) _buyPanel.SetActive(false);
        if (_sellPanel != null) _sellPanel.SetActive(true);

        _currentSellCharacterKey = SellCharacterStashKey;
        _selectedSellCharacter = null;
        _currentSellCategory = "All";
        RefreshSellCharacterButtons();
        RefreshSellCategoryButtons();
        RefreshSellSortButtons();
        RebuildSellList();

        Debug.Log("[Store] Switched to SELL tab (default: Stash, All types)");
        Debug.Log("[Store] === SELL LIST IMPROVEMENTS ===");
        Debug.Log("[Store] ✅ Equipped items hidden from sell list (only general inventory + stash shown)");
        Debug.Log("[Store] ✅ Items stacked by name with quantity display");
        Debug.Log("[Store] ✅ Quantity selector prompt for stacks");
        Debug.Log("[Store] ✅ Sell 1 to max items from stack");
        Debug.Log("[Store] ✅ Source tracking (characters + stash)");
    }

    private void RebuildBuyList()
    {
        if (_buyContent == null)
            return;

        ClearChildren(_buyContent);
        _buyRowImages.Clear();

        List<StoreInventory.StoreItemEntry> filteredItems = GetFilteredBuyItems();

        // Text search
        filteredItems = ApplyBuySearchFilter(filteredItems);

        // Affordable filter
        if (_showAffordableOnly)
        {
            int gold = GameManager.Instance != null ? GameManager.Instance.PartyGold : 0;
            filteredItems = filteredItems.FindAll(e => e != null && e.PriceGp <= gold);
        }

        List<StoreInventory.StoreItemEntry> sortedItems = SortBuyItems(filteredItems);

        for (int i = 0; i < sortedItems.Count; i++)
            CreateBuyRow(_buyContent, sortedItems[i]);

        Debug.Log($"[Store] Buy list refreshed: {sortedItems.Count} items (Cat: {_currentBuyCategory}, Sub: {_currentBuySubFilter}, Search: '{_buySearchQuery}', Affordable: {_showAffordableOnly})");
    }

    private List<StoreInventory.StoreItemEntry> ApplyBuySearchFilter(List<StoreInventory.StoreItemEntry> items)
    {
        if (items == null)
            return new List<StoreInventory.StoreItemEntry>();

        string query = (_buySearchQuery ?? string.Empty).Trim();
        if (query.Length == 0)
            return items;

        return items.FindAll(entry =>
        {
            if (entry == null)
                return false;

            ItemData template = entry.GetTemplate();
            string name = template != null
                ? (template.FullNameWithEnhancement ?? template.Name ?? string.Empty)
                : string.Empty;

            return name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        });
    }

    private void RebuildSellList()
    {
        if (_sellContent == null)
            return;

        ClearChildren(_sellContent);

        if (_sellQuantityDialog != null)
        {
            Destroy(_sellQuantityDialog);
            _sellQuantityDialog = null;
        }

        List<SellStack> stacks = BuildSellStacks();
        List<SellStack> sortedStacks = SortSellStacks(stacks);
        for (int i = 0; i < sortedStacks.Count; i++)
            CreateSellRow(_sellContent, sortedStacks[i]);

        int totalCount = CountAllSellableItems();
        int stackedCount = 0;
        for (int i = 0; i < sortedStacks.Count; i++)
            stackedCount += sortedStacks[i].Quantity;

        Debug.Log($"[Store] Sell list refreshed: showing {sortedStacks.Count} stacks / {stackedCount} items from {totalCount} sellable items (Character: {GetSellCharacterLabelForLogs()}, Category: {_currentSellCategory}, Sort: {_sellSortMode} {_sellSortDirection})");
    }

    private List<StoreInventory.StoreItemEntry> SortBuyItems(List<StoreInventory.StoreItemEntry> items)
    {
        List<StoreInventory.StoreItemEntry> sorted = items != null
            ? new List<StoreInventory.StoreItemEntry>(items)
            : new List<StoreInventory.StoreItemEntry>();

        sorted.Sort((a, b) =>
        {
            string aName = a != null && a.GetTemplate() != null ? (a.GetTemplate().FullNameWithEnhancement ?? string.Empty) : string.Empty;
            string bName = b != null && b.GetTemplate() != null ? (b.GetTemplate().FullNameWithEnhancement ?? string.Empty) : string.Empty;
            int aPrice = a != null ? a.PriceGp : 0;
            int bPrice = b != null ? b.PriceGp : 0;

            if (_buySortMode == SortMode.Price)
            {
                int priceCompare = aPrice.CompareTo(bPrice);
                if (priceCompare != 0)
                    return priceCompare;

                return string.Compare(aName, bName, StringComparison.OrdinalIgnoreCase);
            }

            int nameCompare = string.Compare(aName, bName, StringComparison.OrdinalIgnoreCase);
            if (nameCompare != 0)
                return nameCompare;

            return aPrice.CompareTo(bPrice);
        });

        if (_buySortDirection == SortDirection.Descending)
            sorted.Reverse();

        return sorted;
    }

    private List<SellStack> SortSellStacks(List<SellStack> stacks)
    {
        List<SellStack> sorted = stacks != null
            ? new List<SellStack>(stacks)
            : new List<SellStack>();

        sorted.Sort((a, b) =>
        {
            string aName = a != null ? a.ItemName : string.Empty;
            string bName = b != null ? b.ItemName : string.Empty;
            int aPrice = a != null ? a.UnitSellPrice : 0;
            int bPrice = b != null ? b.UnitSellPrice : 0;

            if (_sellSortMode == SortMode.Price)
            {
                int priceCompare = aPrice.CompareTo(bPrice);
                if (priceCompare != 0)
                    return priceCompare;

                return string.Compare(aName, bName, StringComparison.OrdinalIgnoreCase);
            }

            int nameCompare = string.Compare(aName, bName, StringComparison.OrdinalIgnoreCase);
            if (nameCompare != 0)
                return nameCompare;

            return aPrice.CompareTo(bPrice);
        });

        if (_sellSortDirection == SortDirection.Descending)
            sorted.Reverse();

        return sorted;
    }

    private List<SellStack> BuildSellStacks()
    {
        Dictionary<string, SellStack> stackLookup = new Dictionary<string, SellStack>(StringComparer.OrdinalIgnoreCase);

        bool stashSelected = string.Equals(_currentSellCharacterKey, SellCharacterStashKey, StringComparison.OrdinalIgnoreCase)
            || _selectedSellCharacter == null;

        if (stashSelected)
        {
            if (_partyStash != null)
            {
                List<ItemData> stashItems = _partyStash.GetItemsSnapshot();
                for (int i = 0; i < stashItems.Count; i++)
                {
                    ItemData item = stashItems[i];
                    if (item == null || !MatchesSellCategory(item))
                        continue;

                    AddItemToSellStack(stackLookup, item, true, null, "Stash");
                }
            }
        }
        else
        {
            CharacterController character = _selectedSellCharacter;
            CharacterInventory inventory = character != null ? character.GetComponent<CharacterInventory>() : null;
            Inventory rawInventory = inventory != null ? inventory.GetInventory() : null;

            if (rawInventory != null && rawInventory.GeneralSlots != null)
            {
                string ownerName = character.Stats != null ? character.Stats.CharacterName : character.name;

                for (int slotIndex = 0; slotIndex < rawInventory.GeneralSlots.Length; slotIndex++)
                {
                    ItemData item = rawInventory.GeneralSlots[slotIndex];
                    if (item == null || !MatchesSellCategory(item))
                        continue;

                    AddItemToSellStack(stackLookup, item, false, inventory, ownerName);
                }
            }
        }

        return new List<SellStack>(stackLookup.Values);
    }

    private void AddItemToSellStack(
        Dictionary<string, SellStack> stackLookup,
        ItemData item,
        bool fromStash,
        CharacterInventory inventoryOwner,
        string ownerName)
    {
        if (item == null)
            return;

        string key = BuildSellStackKey(item);
        if (!stackLookup.TryGetValue(key, out SellStack stack))
        {
            stack = new SellStack
            {
                StackKey = key,
                ItemName = item.FullNameWithEnhancement,
                RepresentativeItem = item,
                UnitSellPrice = StoreInventory.Instance.GetSellPrice(item)
            };
            stackLookup[key] = stack;
        }

        stack.AddSource(new SellItemSource
        {
            Item = item,
            FromStash = fromStash,
            InventoryOwner = inventoryOwner,
            OwnerName = ownerName
        });
    }

    private static string BuildSellStackKey(ItemData item)
    {
        return item != null ? (item.FullNameWithEnhancement ?? string.Empty).Trim() : string.Empty;
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

            Inventory rawInventory = inventory.GetInventory();
            if (rawInventory == null || rawInventory.GeneralSlots == null)
                continue;

            for (int slotIndex = 0; slotIndex < rawInventory.GeneralSlots.Length; slotIndex++)
            {
                if (rawInventory.GeneralSlots[slotIndex] != null)
                    total++;
            }
        }

        return total;
    }

    private string GetSellCharacterLabelForLogs()
    {
        if (string.Equals(_currentSellCharacterKey, SellCharacterStashKey, StringComparison.OrdinalIgnoreCase)
            || _selectedSellCharacter == null)
            return "Stash";

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

        string displayName = template.FullNameWithEnhancement;

        GameObject row = CreatePanel(parent, $"Buy_{entry.ItemId}",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(0f, 40f), new Color(0.16f, 0.18f, 0.25f, 1f));

        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.minHeight = 40f;
        rowLayout.preferredHeight = 40f;
        rowLayout.flexibleWidth = 1f;

        // Make entire row clickable to show details
        Button rowBtn = row.AddComponent<Button>();
        Image rowImg = row.GetComponent<Image>();
        rowBtn.targetGraphic = rowImg;
        StoreInventory.StoreItemEntry capturedEntry = entry;
        rowBtn.onClick.AddListener(() => SelectBuyItem(capturedEntry));
        _buyRowImages[entry.ItemId] = rowImg;

        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 6, 3, 3);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        // Item name (flexible width)
        Text nameText = CreateText(row.transform, "Name", displayName,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            Vector2.zero, 14, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft);
        LayoutElement nameLE = nameText.gameObject.AddComponent<LayoutElement>();
        nameLE.minWidth = 120f;
        nameLE.flexibleWidth = 1f;
        nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        nameText.verticalOverflow = VerticalWrapMode.Overflow;

        // Price (fixed)
        Text priceText = CreateText(row.transform, "Price", $"{entry.PriceGp} gp",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            Vector2.zero, 13, FontStyle.Bold, new Color(1f, 0.93f, 0.24f), TextAnchor.MiddleRight);
        LayoutElement priceLE = priceText.gameObject.AddComponent<LayoutElement>();
        priceLE.minWidth = 70f;
        priceLE.preferredWidth = 70f;
        priceLE.flexibleWidth = 0f;

        // BUY button (fixed)
        CreateSmallBuyButton(row.transform, entry);
    }

    private Button CreateSmallBuyButton(Transform parent, StoreInventory.StoreItemEntry entry)
    {
        GameObject buttonObj = new GameObject("BuyBtn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObj.transform.SetParent(parent, false);
        LayoutElement le = buttonObj.GetComponent<LayoutElement>();
        le.minWidth = 50f;
        le.preferredWidth = 50f;
        le.preferredHeight = 30f;
        le.flexibleWidth = 0f;
        Image img = buttonObj.GetComponent<Image>();
        img.color = new Color(0.2f, 0.56f, 0.26f);
        Button btn = buttonObj.GetComponent<Button>();
        StoreInventory.StoreItemEntry capturedEntry = entry;
        btn.onClick.AddListener(() => BuyItem(capturedEntry));
        CreateText(buttonObj.transform, "Lbl", "BUY",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            Vector2.zero, 12, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        return btn;
    }

    private void SelectBuyItem(StoreInventory.StoreItemEntry entry)
    {
        _selectedBuyEntry = entry;

        // Highlight selected row
        foreach (var kvp in _buyRowImages)
        {
            if (kvp.Value == null) continue;
            kvp.Value.color = string.Equals(kvp.Key, entry.ItemId, StringComparison.OrdinalIgnoreCase)
                ? new Color(0.22f, 0.32f, 0.42f, 1f)
                : new Color(0.16f, 0.18f, 0.25f, 1f);
        }

        // Update detail panel
        UpdateBuyDetailPanel(entry);
    }

    private void UpdateBuyDetailPanel(StoreInventory.StoreItemEntry entry)
    {
        if (_buyDetailTitleText == null || _buyDetailBodyText == null)
            return;

        ItemData template = entry != null ? entry.GetTemplate() : null;
        if (template == null)
        {
            _buyDetailTitleText.text = "Select an Item";
            _buyDetailBodyText.text = "Click an item on the left to view its details here.";
            return;
        }

        _buyDetailTitleText.text = template.FullNameWithEnhancement;
        _buyDetailBodyText.text = BuildDetailDescription(template, entry);
    }

    /// <summary>
    /// Builds a rich text description for the detail panel, covering all relevant
    /// item stats based on item type (weapon, armor, shield, consumable, ring, etc.).
    /// </summary>
    private static string BuildDetailDescription(ItemData item, StoreInventory.StoreItemEntry entry)
    {
        if (item == null) return string.Empty;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        // Category + Price
        sb.AppendLine($"Category: {entry.Category}");
        sb.AppendLine($"Price: {entry.PriceGp} gp");

        // Stored description
        if (!string.IsNullOrWhiteSpace(item.Description))
        {
            sb.AppendLine();
            sb.AppendLine(item.Description);
        }

        sb.AppendLine();

        // Type-specific stats
        if (item.IsWeapon)
        {
            sb.AppendLine("--- Weapon Stats ---");
            if (item.DamageDice > 0)
                sb.AppendLine($"Damage: {item.DamageCount}d{item.DamageDice}");
            int enhBonus = item.ResolveEnhancementBonus();
            if (enhBonus > 0)
                sb.AppendLine($"Enhancement: +{enhBonus}");
            sb.AppendLine($"Proficiency: {item.Proficiency}");
            sb.AppendLine($"Category: {item.WeaponCat}");
            sb.AppendLine($"Size: {item.WeaponSize}");
            if (item.CritThreatMin > 0 && item.CritThreatMin < 20)
                sb.AppendLine($"Critical: {item.CritThreatMin}-20/x{item.CritMultiplier}");
            else if (item.CritMultiplier > 0)
                sb.AppendLine($"Critical: 20/x{item.CritMultiplier}");
            if (item.IsEnchanted)
            {
                sb.Append("Enchantments: ");
                for (int i = 0; i < item.Enchantment.Abilities.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(EnchantmentProperties.GetDisplayName(item.Enchantment.Abilities[i]));
                }
                sb.AppendLine();
            }
        }
        else if (item.IsArmor)
        {
            sb.AppendLine("--- Armor Stats ---");
            int totalAC = item.GetTotalArmorBonus();
            sb.AppendLine($"Armor Bonus: +{totalAC}");
            int enhBonus = item.ResolveEnhancementBonus();
            if (enhBonus > 0)
                sb.AppendLine($"Enhancement: +{enhBonus}");
            sb.AppendLine($"Max Dex Bonus: {(item.MaxDexBonus < 0 ? "—" : $"+{item.EffectiveMaxDexBonus}")}");
            sb.AppendLine($"Armor Check Penalty: -{item.EffectiveArmorCheckPenalty}");
            sb.AppendLine($"Arcane Spell Failure: {item.EffectiveArcaneSpellFailure}%");
        }
        else if (item.IsShield)
        {
            sb.AppendLine("--- Shield Stats ---");
            int totalShield = item.GetTotalShieldBonus();
            sb.AppendLine($"Shield Bonus: +{totalShield}");
            int enhBonus = item.ResolveEnhancementBonus();
            if (enhBonus > 0)
                sb.AppendLine($"Enhancement: +{enhBonus}");
            sb.AppendLine($"Armor Check Penalty: -{item.EffectiveArmorCheckPenalty}");
            sb.AppendLine($"Arcane Spell Failure: {item.EffectiveArcaneSpellFailure}%");
        }

        // Material
        if (item.Material != null && item.Material.MaterialType != ItemMaterialType.Standard)
            sb.AppendLine($"Material: {item.Material.MaterialType}");

        // Weight
        if (item.WeightLbs > 0f)
            sb.AppendLine($"Weight: {item.EffectiveWeightLbs:F1} lbs");

        // Masterwork
        if (item.IsMasterwork && item.ResolveEnhancementBonus() <= 0)
            sb.AppendLine("Masterwork: Yes");

        // Ring / Rod / Wondrous item descriptions use the stored Description
        // which was already printed above. Add caster level if available.
        if (item.IsRingItem && item.RingCasterLevel > 0)
            sb.AppendLine($"Caster Level: {item.RingCasterLevel}");
        if (item.Type == ItemType.Rod && item.RodCasterLevel > 0)
            sb.AppendLine($"Caster Level: {item.RodCasterLevel}");
        if (item.IsWondrousItem && item.WondrousCasterLevel > 0)
            sb.AppendLine($"Caster Level: {item.WondrousCasterLevel}");

        // Scroll / Potion / Wand
        if (item.Type == ItemType.Consumable)
        {
            if (item.ScrollSpellLevel > 0)
                sb.AppendLine($"Scroll Spell Level: {item.ScrollSpellLevel}");
            if (item.PotionSpellLevel > 0)
                sb.AppendLine($"Potion Spell Level: {item.PotionSpellLevel}");
        }
        if (item.WandSpellLevel > 0)
            sb.AppendLine($"Wand Spell Level: {item.WandSpellLevel}  |  Charges: {item.CurrentCharges}/{item.MaxCharges}");

        return sb.ToString();
    }

    private void CreateSellRow(Transform parent, SellStack stack)
    {
        if (stack == null || stack.RepresentativeItem == null)
            return;

        int sellPrice = stack.UnitSellPrice;

        GameObject row = CreatePanel(parent, $"Sell_{stack.ItemName}",
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

        if (stack.Quantity > 1)
        {
            GameObject quantityObj = new GameObject("Quantity", typeof(RectTransform), typeof(LayoutElement));
            quantityObj.transform.SetParent(row.transform, false);
            LayoutElement quantityLayout = quantityObj.GetComponent<LayoutElement>();
            quantityLayout.minWidth = 60f;
            quantityLayout.preferredWidth = 60f;
            quantityLayout.flexibleWidth = 0f;

            Text quantityText = CreateText(quantityObj.transform, "QuantityText", $"x{stack.Quantity}",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.zero, 22, FontStyle.Bold, new Color(1f, 0.8f, 0.4f), TextAnchor.MiddleCenter);
            quantityText.horizontalOverflow = HorizontalWrapMode.Overflow;
            quantityText.verticalOverflow = VerticalWrapMode.Overflow;
        }

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

        Text nameText = CreateText(infoObj.transform, "Name", stack.ItemName,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            Vector2.zero, 18, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        LayoutElement nameLayout = nameText.gameObject.AddComponent<LayoutElement>();
        nameLayout.preferredHeight = 24f;
        nameLayout.flexibleWidth = 1f;
        nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        nameText.verticalOverflow = VerticalWrapMode.Overflow;

        int baseValue = sellPrice * 2;
        string sourceSummary = BuildSellStackSourceSummary(stack);
        string itemDescription = GetItemDescription(stack.RepresentativeItem, string.Empty);
        string valueLine = string.IsNullOrWhiteSpace(itemDescription)
            ? $"Value {baseValue} gp -> Sell {sellPrice} gp each{sourceSummary}"
            : $"{itemDescription} | Value {baseValue} gp -> Sell {sellPrice} gp each{sourceSummary}";

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

        string priceLabel = stack.Quantity > 1 ? $"{sellPrice} gp\neach" : $"{sellPrice} gp";
        int priceFontSize = stack.Quantity > 1 ? 16 : 18;
        Text priceText = CreateText(priceObj.transform, "PriceLabel", priceLabel,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            Vector2.zero, priceFontSize, FontStyle.Bold, new Color(1f, 0.93f, 0.24f), TextAnchor.MiddleCenter);
        priceText.horizontalOverflow = HorizontalWrapMode.Overflow;
        priceText.verticalOverflow = VerticalWrapMode.Overflow;

        GameObject buttonSection = new GameObject("ButtonSection", typeof(RectTransform), typeof(LayoutElement));
        buttonSection.transform.SetParent(row.transform, false);
        LayoutElement buttonLayout = buttonSection.GetComponent<LayoutElement>();
        buttonLayout.minWidth = 70f;
        buttonLayout.preferredWidth = 70f;
        buttonLayout.flexibleWidth = 0f;

        CreateSmallActionButton(buttonSection.transform, "SellButton", "SELL", new Color(0.58f, 0.37f, 0.18f), () =>
        {
            if (stack.Quantity > 1)
                ShowSellQuantityPrompt(stack);
            else
                SellStackItems(stack, 1);
        });

        Debug.Log($"[Store] Created sell stack row for {stack.ItemName} x{stack.Quantity}");
    }

    private string BuildSellStackSourceSummary(SellStack stack)
    {
        if (stack == null || stack.Quantity <= 0)
            return string.Empty;

        bool hasStash = false;
        HashSet<string> characterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < stack.Sources.Count; i++)
        {
            SellItemSource source = stack.Sources[i];
            if (source == null)
                continue;

            if (source.FromStash)
            {
                hasStash = true;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(source.OwnerName))
                characterNames.Add(source.OwnerName);
        }

        if (characterNames.Count == 0 && !hasStash)
            return string.Empty;

        if (characterNames.Count > 0 && hasStash)
            return $" (From {characterNames.Count} character(s) + stash)";

        if (characterNames.Count > 0)
            return $" (From {characterNames.Count} character(s))";

        return " (From stash)";
    }

    private void BuyItem(StoreInventory.StoreItemEntry entry)
    {
        ItemData item = StoreInventory.Instance.CreateItemInstance(entry.ItemId);
        if (item == null)
        {
            ShowMessage("Could not create item instance.", false);
            return;
        }

        string itemDisplayName = item.FullNameWithEnhancement;
        Debug.Log($"[Store] Buying {itemDisplayName} for {entry.PriceGp} gp");

        if (!GameManager.Instance.SpendGold(entry.PriceGp))
        {
            ShowMessage($"Not enough gold for {itemDisplayName}.", false);
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
        ShowMessage($"Purchased {itemDisplayName} for {entry.PriceGp} gp.", true);
        if (_sellPanel != null && _sellPanel.activeSelf)
            RebuildSellList();
    }

    private void ShowSellQuantityPrompt(SellStack stack)
    {
        if (stack == null || stack.RepresentativeItem == null || stack.Quantity <= 1)
        {
            SellStackItems(stack, 1);
            return;
        }

        if (_sellQuantityDialog != null)
            Destroy(_sellQuantityDialog);

        _sellQuantityDialog = CreatePanel(_root.transform, "SellQuantityPrompt",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.72f));

        GameObject panel = CreatePanel(_sellQuantityDialog.transform, "Panel",
            new Vector2(0.3f, 0.35f), new Vector2(0.7f, 0.65f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, new Color(0.15f, 0.15f, 0.2f, 1f));

        VerticalLayoutGroup panelLayout = panel.AddComponent<VerticalLayoutGroup>();
        panelLayout.spacing = 15f;
        panelLayout.padding = new RectOffset(20, 20, 20, 20);
        panelLayout.childAlignment = TextAnchor.UpperCenter;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        CreateDialogText(panel.transform, "Title", "How many to sell?", 28, FontStyle.Bold, Color.white, 40f);

        int sellPrice = stack.UnitSellPrice;
        CreateDialogText(panel.transform, "Info", $"{stack.ItemName}\n{sellPrice} gp each × {stack.Quantity} available", 18, FontStyle.Normal, new Color(0.9f, 0.9f, 0.9f), 60f);

        int currentQuantity = 1;

        GameObject selectorContainer = new GameObject("QuantitySelector", typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
        selectorContainer.transform.SetParent(panel.transform, false);
        LayoutElement selectorLayout = selectorContainer.GetComponent<LayoutElement>();
        selectorLayout.preferredHeight = 60f;
        HorizontalLayoutGroup selectorHLayout = selectorContainer.GetComponent<HorizontalLayoutGroup>();
        selectorHLayout.spacing = 12f;
        selectorHLayout.childAlignment = TextAnchor.MiddleCenter;
        selectorHLayout.childControlWidth = true;
        selectorHLayout.childControlHeight = true;
        selectorHLayout.childForceExpandWidth = false;
        selectorHLayout.childForceExpandHeight = false;

        GameObject minusButton = CreateSmallActionButton(selectorContainer.transform, "Minus", "-", new Color(0.5f, 0.3f, 0.3f), null).gameObject;

        GameObject quantityDisplay = new GameObject("QuantityDisplay", typeof(RectTransform), typeof(LayoutElement), typeof(Image));
        quantityDisplay.transform.SetParent(selectorContainer.transform, false);
        LayoutElement quantityDisplayLayout = quantityDisplay.GetComponent<LayoutElement>();
        quantityDisplayLayout.minWidth = 120f;
        quantityDisplayLayout.preferredWidth = 120f;
        quantityDisplayLayout.preferredHeight = 50f;
        quantityDisplay.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.3f, 1f);

        Text quantityText = CreateText(quantityDisplay.transform, "QuantityValue", currentQuantity.ToString(),
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            Vector2.zero, 32, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

        GameObject plusButton = CreateSmallActionButton(selectorContainer.transform, "Plus", "+", new Color(0.3f, 0.5f, 0.3f), null).gameObject;

        GameObject maxButton = CreateSmallActionButton(selectorContainer.transform, "Max", "MAX", new Color(0.4f, 0.4f, 0.5f), null).gameObject;
        LayoutElement maxLayout = maxButton.GetComponent<LayoutElement>();
        if (maxLayout != null)
        {
            maxLayout.minWidth = 80f;
            maxLayout.preferredWidth = 80f;
        }

        Text totalText = CreateDialogText(panel.transform, "Total", string.Empty, 24, FontStyle.Bold, new Color(1f, 0.93f, 0.24f), 40f);

        Action updateTotal = () =>
        {
            quantityText.text = currentQuantity.ToString();
            totalText.text = $"Total: {sellPrice * currentQuantity} gp";
        };

        Button minusButtonComp = minusButton.GetComponent<Button>();
        if (minusButtonComp != null)
        {
            minusButtonComp.onClick.RemoveAllListeners();
            minusButtonComp.onClick.AddListener(() =>
            {
                if (currentQuantity <= 1)
                    return;

                currentQuantity--;
                updateTotal();
            });
        }

        Button plusButtonComp = plusButton.GetComponent<Button>();
        if (plusButtonComp != null)
        {
            plusButtonComp.onClick.RemoveAllListeners();
            plusButtonComp.onClick.AddListener(() =>
            {
                if (currentQuantity >= stack.Quantity)
                    return;

                currentQuantity++;
                updateTotal();
            });
        }

        Button maxButtonComp = maxButton.GetComponent<Button>();
        if (maxButtonComp != null)
        {
            maxButtonComp.onClick.RemoveAllListeners();
            maxButtonComp.onClick.AddListener(() =>
            {
                currentQuantity = stack.Quantity;
                updateTotal();
            });
        }

        GameObject actionContainer = new GameObject("ActionButtons", typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
        actionContainer.transform.SetParent(panel.transform, false);
        LayoutElement actionLayout = actionContainer.GetComponent<LayoutElement>();
        actionLayout.preferredHeight = 50f;
        HorizontalLayoutGroup actionHLayout = actionContainer.GetComponent<HorizontalLayoutGroup>();
        actionHLayout.spacing = 20f;
        actionHLayout.childAlignment = TextAnchor.MiddleCenter;
        actionHLayout.childControlWidth = true;
        actionHLayout.childControlHeight = true;
        actionHLayout.childForceExpandWidth = false;
        actionHLayout.childForceExpandHeight = false;

        GameObject cancelButton = CreateSmallActionButton(actionContainer.transform, "CancelButton", "Cancel", new Color(0.5f, 0.3f, 0.3f), () =>
        {
            if (_sellQuantityDialog != null)
            {
                Destroy(_sellQuantityDialog);
                _sellQuantityDialog = null;
            }
        }).gameObject;
        LayoutElement cancelLayout = cancelButton.GetComponent<LayoutElement>();
        if (cancelLayout != null)
        {
            cancelLayout.minWidth = 120f;
            cancelLayout.preferredWidth = 120f;
        }

        GameObject confirmButton = CreateSmallActionButton(actionContainer.transform, "ConfirmSellButton", "Sell", new Color(0.3f, 0.6f, 0.3f), () =>
        {
            if (_sellQuantityDialog != null)
            {
                Destroy(_sellQuantityDialog);
                _sellQuantityDialog = null;
            }

            SellStackItems(stack, currentQuantity);
        }).gameObject;
        LayoutElement confirmLayout = confirmButton.GetComponent<LayoutElement>();
        if (confirmLayout != null)
        {
            confirmLayout.minWidth = 120f;
            confirmLayout.preferredWidth = 120f;
        }

        updateTotal();

        Debug.Log($"[Store] Showing quantity prompt for {stack.ItemName} (max {stack.Quantity})");
    }

    private static Text CreateDialogText(
        Transform parent,
        string name,
        string value,
        int fontSize,
        FontStyle fontStyle,
        Color color,
        float preferredHeight)
    {
        GameObject textObj = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
        textObj.transform.SetParent(parent, false);

        LayoutElement layout = textObj.GetComponent<LayoutElement>();
        layout.preferredHeight = preferredHeight;

        return CreateText(textObj.transform, "Label", value,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            Vector2.zero, fontSize, fontStyle, color, TextAnchor.MiddleCenter);
    }

    private void SellStackItems(SellStack stack, int quantityToSell)
    {
        if (stack == null || stack.RepresentativeItem == null)
            return;

        if (quantityToSell <= 0 || quantityToSell > stack.Quantity)
        {
            Debug.LogError($"[Store] Invalid sell quantity for {stack.ItemName}: {quantityToSell}/{stack.Quantity}");
            return;
        }

        int removedCount = 0;
        int unitPrice = stack.UnitSellPrice;

        for (int i = 0; i < stack.Sources.Count && removedCount < quantityToSell; i++)
        {
            SellItemSource source = stack.Sources[i];
            if (source == null || source.Item == null)
                continue;

            bool removed;
            if (source.FromStash)
            {
                removed = _partyStash != null && _partyStash.RemoveItem(source.Item);
            }
            else
            {
                removed = source.InventoryOwner != null && source.InventoryOwner.RemoveItem(source.Item);
            }

            if (!removed)
                continue;

            removedCount++;
            string sourceLabel = source.FromStash ? "stash" : source.OwnerName;
            Debug.Log($"[Store] Sold {source.Item.Name} from {sourceLabel} for {unitPrice} gp");
        }

        if (removedCount <= 0)
        {
            ShowMessage("Unable to sell item(s).", false);
            return;
        }

        int totalGold = unitPrice * removedCount;
        GameManager.Instance.AddGold(totalGold);

        Debug.Log($"[Store] Sold {removedCount}x {stack.ItemName} for {totalGold} gp total");
        ShowMessage($"Sold {removedCount}x {stack.ItemName} for {totalGold} gp.", true);

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
