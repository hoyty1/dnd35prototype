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
    private Dropdown _categoryDropdown;

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
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(980f, 660f), new Color(0.08f, 0.1f, 0.15f, 0.98f));

        CreateText(_root.transform, "Title", "MERCHANT SHOP",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -32f), new Vector2(420f, 38f), 30, FontStyle.Bold,
            new Color(0.98f, 0.88f, 0.45f), TextAnchor.MiddleCenter);

        _goldText = CreateText(_root.transform, "GoldText", "Gold: 0 gp",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-18f, -28f), new Vector2(220f, 32f), 18, FontStyle.Bold,
            new Color(1f, 0.92f, 0.2f), TextAnchor.MiddleRight);

        CreateTabButton("BuyTab", "BUY", new Vector2(120f, -80f), new Color(0.22f, 0.5f, 0.28f), ShowBuyPanel);
        CreateTabButton("SellTab", "SELL", new Vector2(260f, -80f), new Color(0.56f, 0.35f, 0.18f), ShowSellPanel);

        BuildBuyPanel();
        BuildSellPanel();

        _messageText = CreateText(_root.transform, "MessageText", string.Empty,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 86f), new Vector2(700f, 30f), 15, FontStyle.Bold,
            new Color(0.84f, 0.9f, 1f), TextAnchor.MiddleCenter);

        CreateBottomButton("BackButton", "Back to Menu", new Vector2(-170f, 34f), new Color(0.48f, 0.25f, 0.25f), () =>
        {
            Close();
            _onBackToMenu?.Invoke();
        });

        CreateBottomButton("StartButton", "Start Encounter", new Vector2(170f, 34f), new Color(0.2f, 0.58f, 0.28f), () =>
        {
            Close();
            _onStartEncounter?.Invoke();
        });

        _root.SetActive(false);
    }

    private void BuildBuyPanel()
    {
        _buyPanel = CreatePanel(_root.transform, "BuyPanel",
            new Vector2(0.03f, 0.18f), new Vector2(0.97f, 0.84f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, new Color(0.06f, 0.08f, 0.12f, 0.94f));

        GameObject filterObj = new GameObject("CategoryDropdown", typeof(RectTransform), typeof(Image), typeof(Dropdown));
        filterObj.transform.SetParent(_buyPanel.transform, false);
        RectTransform filterRect = filterObj.GetComponent<RectTransform>();
        filterRect.anchorMin = new Vector2(0f, 1f);
        filterRect.anchorMax = new Vector2(0f, 1f);
        filterRect.pivot = new Vector2(0f, 1f);
        filterRect.anchoredPosition = new Vector2(14f, -10f);
        filterRect.sizeDelta = new Vector2(230f, 34f);

        filterObj.GetComponent<Image>().color = new Color(0.2f, 0.24f, 0.34f, 1f);
        _categoryDropdown = filterObj.GetComponent<Dropdown>();

        Text ddLabel = CreateText(filterObj.transform, "Label", "All",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(-24f, 0f), 14, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        ddLabel.rectTransform.offsetMin = new Vector2(10f, 0f);
        ddLabel.rectTransform.offsetMax = new Vector2(-20f, 0f);

        GameObject arrow = new GameObject("Arrow", typeof(RectTransform), typeof(Text));
        arrow.transform.SetParent(filterObj.transform, false);
        Text arrowText = arrow.GetComponent<Text>();
        arrowText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        arrowText.text = "▼";
        arrowText.fontSize = 14;
        arrowText.color = Color.white;
        arrowText.alignment = TextAnchor.MiddleCenter;
        RectTransform arrowRect = arrow.GetComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(1f, 0f);
        arrowRect.anchorMax = new Vector2(1f, 1f);
        arrowRect.pivot = new Vector2(1f, 0.5f);
        arrowRect.sizeDelta = new Vector2(22f, 0f);
        arrowRect.anchoredPosition = new Vector2(-2f, 0f);

        GameObject template = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        template.transform.SetParent(filterObj.transform, false);
        RectTransform templateRect = template.GetComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0f, 0f);
        templateRect.anchorMax = new Vector2(1f, 0f);
        templateRect.pivot = new Vector2(0.5f, 1f);
        templateRect.anchoredPosition = new Vector2(0f, -2f);
        templateRect.sizeDelta = new Vector2(0f, 150f);
        template.GetComponent<Image>().color = new Color(0.13f, 0.16f, 0.24f, 1f);
        template.SetActive(false);

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(template.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 0f;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject item = new GameObject("Item", typeof(RectTransform), typeof(Toggle), typeof(Image));
        item.transform.SetParent(content.transform, false);
        item.GetComponent<Image>().color = new Color(0.18f, 0.22f, 0.3f, 1f);
        RectTransform itemRect = item.GetComponent<RectTransform>();
        itemRect.sizeDelta = new Vector2(0f, 28f);

        Toggle toggle = item.GetComponent<Toggle>();

        GameObject itemLabelObj = new GameObject("Item Label", typeof(RectTransform), typeof(Text));
        itemLabelObj.transform.SetParent(item.transform, false);
        Text itemLabel = itemLabelObj.GetComponent<Text>();
        itemLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        itemLabel.fontSize = 14;
        itemLabel.color = Color.white;
        itemLabel.alignment = TextAnchor.MiddleLeft;
        RectTransform itemLabelRect = itemLabelObj.GetComponent<RectTransform>();
        itemLabelRect.anchorMin = Vector2.zero;
        itemLabelRect.anchorMax = Vector2.one;
        itemLabelRect.offsetMin = new Vector2(10f, 0f);
        itemLabelRect.offsetMax = new Vector2(-10f, 0f);

        toggle.targetGraphic = item.GetComponent<Image>();
        toggle.graphic = null;

        ScrollRect templateScroll = template.GetComponent<ScrollRect>();
        templateScroll.viewport = viewportRect;
        templateScroll.content = contentRect;
        templateScroll.horizontal = false;
        templateScroll.vertical = true;

        _categoryDropdown.template = templateRect;
        _categoryDropdown.captionText = ddLabel;
        _categoryDropdown.itemText = itemLabel;
        _categoryDropdown.onValueChanged.AddListener(_ => RebuildBuyList());

        CreateScrollList(_buyPanel.transform, "BuyScroll", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 12f), new Vector2(-12f, -52f), out _buyContent);
    }

    private void BuildSellPanel()
    {
        _sellPanel = CreatePanel(_root.transform, "SellPanel",
            new Vector2(0.03f, 0.18f), new Vector2(0.97f, 0.84f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, new Color(0.07f, 0.07f, 0.1f, 0.94f));

        CreateText(_sellPanel.transform, "Hint", "Items sell for 50% of listed value (D&D 3.5e)",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -16f), new Vector2(540f, 24f), 13, FontStyle.Italic,
            new Color(0.95f, 0.84f, 0.45f), TextAnchor.MiddleCenter);

        CreateScrollList(_sellPanel.transform, "SellScroll", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 12f), new Vector2(-12f, -40f), out _sellContent);

        _sellPanel.SetActive(false);
    }

    private void BuildCategoryOptions()
    {
        if (_categoryDropdown == null)
            return;

        List<string> categories = StoreInventory.Instance.GetCategories();
        _categoryDropdown.ClearOptions();
        _categoryDropdown.AddOptions(categories);
        _categoryDropdown.value = 0;
        _categoryDropdown.RefreshShownValue();
    }

    private void ShowBuyPanel()
    {
        if (_buyPanel != null) _buyPanel.SetActive(true);
        if (_sellPanel != null) _sellPanel.SetActive(false);
        RebuildBuyList();
    }

    private void ShowSellPanel()
    {
        if (_buyPanel != null) _buyPanel.SetActive(false);
        if (_sellPanel != null) _sellPanel.SetActive(true);
        RebuildSellList();
    }

    private void RebuildBuyList()
    {
        if (_buyContent == null)
            return;

        ClearChildren(_buyContent);

        string category = "All";
        if (_categoryDropdown != null && _categoryDropdown.options != null && _categoryDropdown.options.Count > 0)
            category = _categoryDropdown.options[_categoryDropdown.value].text;

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
    }

    private List<SellEntry> BuildSellEntries()
    {
        List<SellEntry> entries = new List<SellEntry>();

        if (_partyStash != null)
        {
            List<ItemData> stashItems = _partyStash.GetItemsSnapshot();
            for (int i = 0; i < stashItems.Count; i++)
            {
                ItemData item = stashItems[i];
                if (item == null)
                    continue;

                entries.Add(new SellEntry
                {
                    Item = item,
                    FromStash = true,
                    OwnerName = "Stash"
                });
            }
        }

        for (int i = 0; i < _partyMembers.Count; i++)
        {
            CharacterController character = _partyMembers[i];
            if (character == null)
                continue;

            CharacterInventory inventory = character.GetComponent<CharacterInventory>();
            if (inventory == null)
                continue;

            List<ItemData> items = inventory.GetAllItems();
            for (int j = 0; j < items.Count; j++)
            {
                ItemData item = items[j];
                if (item == null)
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

        return entries;
    }

    private void CreateBuyRow(Transform parent, StoreInventory.StoreItemEntry entry)
    {
        ItemData template = entry.GetTemplate();
        if (template == null)
            return;

        GameObject row = CreatePanel(parent, $"Buy_{entry.ItemId}",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(0f, 68f), new Color(0.16f, 0.18f, 0.25f, 1f));

        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 6, 6);
        layout.spacing = 8f;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        Text nameText = CreateText(row.transform, "Name", template.Name,
            Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f), Vector2.zero,
            new Vector2(250f, 54f), 15, FontStyle.Bold, Color.white, TextAnchor.UpperLeft);

        string details = string.IsNullOrWhiteSpace(template.Description) ? entry.Category : template.Description;
        CreateText(row.transform, "Details", details,
            Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f), Vector2.zero,
            new Vector2(420f, 54f), 12, FontStyle.Normal, new Color(0.8f, 0.85f, 0.95f), TextAnchor.UpperLeft);

        CreateText(row.transform, "Price", $"{entry.PriceGp} gp",
            Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f), Vector2.zero,
            new Vector2(90f, 54f), 15, FontStyle.Bold, new Color(1f, 0.93f, 0.24f), TextAnchor.MiddleCenter);

        CreateSmallActionButton(row.transform, "BuyButton", "BUY", new Color(0.2f, 0.56f, 0.26f), () => BuyItem(entry));

        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 68f;
    }

    private void CreateSellRow(Transform parent, SellEntry entry)
    {
        int sellPrice = StoreInventory.Instance.GetSellPrice(entry.Item);

        GameObject row = CreatePanel(parent, $"Sell_{entry.Item.Name}",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(0f, 64f), new Color(0.22f, 0.17f, 0.17f, 1f));

        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 6, 6);
        layout.spacing = 8f;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        CreateText(row.transform, "Name", $"{entry.Item.Name} ({entry.OwnerName})",
            Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f), Vector2.zero,
            new Vector2(330f, 52f), 14, FontStyle.Bold, Color.white, TextAnchor.UpperLeft);

        int baseValue = sellPrice * 2;
        CreateText(row.transform, "Value", $"Value {baseValue} gp → Sell {sellPrice} gp",
            Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f), Vector2.zero,
            new Vector2(320f, 52f), 12, FontStyle.Normal, new Color(0.82f, 0.86f, 0.93f), TextAnchor.MiddleLeft);

        CreateSmallActionButton(row.transform, "SellButton", $"SELL\n{sellPrice} gp", new Color(0.58f, 0.37f, 0.18f), () => SellItem(entry));

        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 64f;
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

    private void CreateTabButton(string name, string label, Vector2 anchoredPos, Color color, Action onClick)
    {
        GameObject tabObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        tabObj.transform.SetParent(_root.transform, false);

        RectTransform rect = tabObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(130f, 40f);

        Image image = tabObj.GetComponent<Image>();
        image.color = color;

        Button button = tabObj.GetComponent<Button>();
        button.onClick.AddListener(() => onClick?.Invoke());

        CreateText(tabObj.transform, "Label", label,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            Vector2.zero, 16, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
    }

    private void CreateBottomButton(string name, string label, Vector2 anchoredPos, Color color, Action onClick)
    {
        GameObject buttonObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObj.transform.SetParent(_root.transform, false);

        RectTransform rect = buttonObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(280f, 42f);

        Image image = buttonObj.GetComponent<Image>();
        image.color = color;

        Button button = buttonObj.GetComponent<Button>();
        button.onClick.AddListener(() => onClick?.Invoke());

        CreateText(buttonObj.transform, "Label", label,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            Vector2.zero, 17, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
    }

    private static Button CreateSmallActionButton(Transform parent, string name, string label, Color color, Action onClick)
    {
        GameObject buttonObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObj.transform.SetParent(parent, false);

        LayoutElement layout = buttonObj.GetComponent<LayoutElement>();
        layout.preferredWidth = 90f;
        layout.preferredHeight = 50f;

        Image image = buttonObj.GetComponent<Image>();
        image.color = color;

        Button button = buttonObj.GetComponent<Button>();
        button.onClick.AddListener(() => onClick?.Invoke());

        CreateText(buttonObj.transform, "Label", label,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
            Vector2.zero, 12, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

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

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect sr = scrollObj.GetComponent<ScrollRect>();
        sr.viewport = viewportRect;
        sr.content = contentRect;
        sr.horizontal = false;
        sr.vertical = true;
        sr.scrollSensitivity = 25f;
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
