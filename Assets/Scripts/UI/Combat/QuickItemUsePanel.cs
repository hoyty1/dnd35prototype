using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quick Item Use panel for combat — lets the active character use consumable items
/// (potions, scrolls, alchemical items) without opening the full inventory.
/// 
/// D&D 3.5e action economy: Using a consumable item is an item manipulation action
/// that consumes a move action (or standard action converted to move). The existing
/// TryUseConsumableFromInventory handles AoO provocation and action economy.
///
/// Features:
///   - Scrollable list of useable consumable items from character inventory
///   - Filter by category: All, Potions, Scrolls, Alchemical
///   - Real-time search by item name (case-insensitive)
///   - Sort: Alphabetical (default), by spell level for scrolls
///   - Click an item to use it (delegates to GameManager.TryUseConsumableFromInventory)
///   - Closes after use or on Cancel
/// </summary>
public class QuickItemUsePanel : MonoBehaviour
{
    // ========== ITEM CATEGORY ==========

    public enum ItemCategory
    {
        All,
        Potion,
        Scroll,
        Wand,
        Alchemical
    }

    public enum SortMode
    {
        Alphabetical,
        SpellLevelAsc,
        SpellLevelDesc
    }

    // ========== CALLBACKS ==========

    /// <summary>Called when the player selects an item. Parameter is the inventory slot index.</summary>
    public Action<int> OnItemSelected;

    /// <summary>Called when the panel is closed without selecting an item.</summary>
    public Action OnCancelled;

    // ========== STATE ==========

    public bool IsOpen { get; private set; }

    private CharacterController _character;
    private List<ConsumableEntry> _allEntries = new List<ConsumableEntry>();
    private List<ConsumableEntry> _filteredEntries = new List<ConsumableEntry>();

    private ItemCategory _currentFilter = ItemCategory.All;
    private SortMode _currentSort = SortMode.Alphabetical;
    private string _searchText = "";

    // ========== UI REFERENCES ==========

    private Font _font;
    private GameObject _overlayPanel;
    private GameObject _rootPanel;
    private Text _titleText;
    private Text _itemCountText;
    private InputField _searchField;

    // Filter buttons
    private Button _filterAllBtn;
    private Button _filterPotionBtn;
    private Button _filterScrollBtn;
    private Button _filterWandBtn;
    private Button _filterAlchemBtn;

    // Sort button
    private Button _sortBtn;
    private Text _sortBtnText;

    // Scroll area
    private GameObject _scrollContent;
    private RectTransform _scrollContentRT;

    // Close button
    private Button _closeBtn;

    // Item rows
    private List<ItemRowUI> _itemRows = new List<ItemRowUI>();

    // Layout constants
    private const float PANEL_W = 540f;
    private const float PANEL_H = 560f;
    private const float ROW_H = 44f;
    private const float ROW_SPACING = 3f;
    private const float HEADER_H = 130f;

    private class ConsumableEntry
    {
        public ItemData Item;
        public int InventoryIndex;
        public ItemCategory Category;
        public int SpellLevel; // -1 if not a scroll/spell consumable
    }

    private class ItemRowUI
    {
        public GameObject Row;
        public Button UseButton;
        public Text IconText;
        public Text NameText;
        public Text InfoText;
        public Image Background;
    }

    // ========== BUILD UI ==========

    public void BuildUI(Canvas canvas)
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // Dark overlay
        _overlayPanel = MakePanel(canvas.transform, "QuickItemOverlay",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.7f));
        var overlayRT = _overlayPanel.GetComponent<RectTransform>();
        overlayRT.offsetMin = Vector2.zero;
        overlayRT.offsetMax = Vector2.zero;

        // Main panel centered
        _rootPanel = MakePanel(_overlayPanel.transform, "QuickItemPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(PANEL_W, PANEL_H), new Color(0.12f, 0.12f, 0.18f, 0.97f));

        float halfW = PANEL_W / 2f;
        float halfH = PANEL_H / 2f;
        float y = halfH;

        // Title
        y -= 24;
        _titleText = MakeText(_rootPanel.transform, "Title",
            new Vector2(0, y), new Vector2(PANEL_W - 20, 30),
            "USE ITEM", 20, Color.white, TextAnchor.MiddleCenter);
        _titleText.fontStyle = FontStyle.Bold;

        // Item count
        y -= 22;
        _itemCountText = MakeText(_rootPanel.transform, "ItemCount",
            new Vector2(0, y), new Vector2(PANEL_W - 20, 20),
            "", 12, new Color(0.7f, 0.7f, 0.7f), TextAnchor.MiddleCenter);

        // Search field
        y -= 28;
        _searchField = MakeInputField(_rootPanel.transform, "SearchField",
            new Vector2(0, y), new Vector2(PANEL_W - 40, 26),
            "Search items...");
        _searchField.onValueChanged.AddListener(OnSearchChanged);

        // Filter buttons row
        y -= 32;
        float filterBtnW = 72f;
        float filterSpacing = 6f;
        float filterTotalW = filterBtnW * 5 + filterSpacing * 4;
        float filterStartX = -filterTotalW / 2f + filterBtnW / 2f;

        _filterAllBtn = MakeButton(_rootPanel.transform, "FilterAll",
            new Vector2(filterStartX, y), new Vector2(filterBtnW, 24),
            "All", new Color(0.35f, 0.55f, 0.35f), Color.white, 11);
        _filterAllBtn.onClick.AddListener(() => SetFilter(ItemCategory.All));

        _filterPotionBtn = MakeButton(_rootPanel.transform, "FilterPotion",
            new Vector2(filterStartX + filterBtnW + filterSpacing, y), new Vector2(filterBtnW, 24),
            "Potions", new Color(0.55f, 0.25f, 0.25f), Color.white, 11);
        _filterPotionBtn.onClick.AddListener(() => SetFilter(ItemCategory.Potion));

        _filterScrollBtn = MakeButton(_rootPanel.transform, "FilterScroll",
            new Vector2(filterStartX + (filterBtnW + filterSpacing) * 2, y), new Vector2(filterBtnW, 24),
            "Scrolls", new Color(0.3f, 0.3f, 0.55f), Color.white, 11);
        _filterScrollBtn.onClick.AddListener(() => SetFilter(ItemCategory.Scroll));

        _filterWandBtn = MakeButton(_rootPanel.transform, "FilterWand",
            new Vector2(filterStartX + (filterBtnW + filterSpacing) * 3, y), new Vector2(filterBtnW, 24),
            "Wands", new Color(0.45f, 0.3f, 0.6f), Color.white, 11);
        _filterWandBtn.onClick.AddListener(() => SetFilter(ItemCategory.Wand));

        _filterAlchemBtn = MakeButton(_rootPanel.transform, "FilterAlchem",
            new Vector2(filterStartX + (filterBtnW + filterSpacing) * 4, y), new Vector2(filterBtnW, 24),
            "Alchemical", new Color(0.5f, 0.4f, 0.2f), Color.white, 11);
        _filterAlchemBtn.onClick.AddListener(() => SetFilter(ItemCategory.Alchemical));

        // Sort button (right side)
        y -= 28;
        _sortBtn = MakeButton(_rootPanel.transform, "SortBtn",
            new Vector2(halfW - 80, y), new Vector2(130, 22),
            "Sort: A-Z", new Color(0.3f, 0.3f, 0.4f), Color.white, 11);
        _sortBtn.onClick.AddListener(CycleSortMode);
        _sortBtnText = _sortBtn.GetComponentInChildren<Text>();

        // Scroll area
        y -= 14;
        float scrollTop = y;
        float scrollBottom = -halfH + 50; // leave room for close button
        float scrollH = scrollTop - scrollBottom;

        GameObject scrollArea = MakePanel(_rootPanel.transform, "ScrollArea",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, (scrollTop + scrollBottom) / 2f), new Vector2(PANEL_W - 24, scrollH),
            new Color(0.08f, 0.08f, 0.12f, 0.9f));

        // Viewport + mask
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollArea.transform, false);
        var vpRT = viewport.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = new Vector2(4, 4);
        vpRT.offsetMax = new Vector2(-4, -4);
        viewport.AddComponent<Image>().color = Color.white;
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        _scrollContent = new GameObject("Content");
        _scrollContent.transform.SetParent(viewport.transform, false);
        _scrollContentRT = _scrollContent.AddComponent<RectTransform>();
        _scrollContentRT.anchorMin = new Vector2(0, 1);
        _scrollContentRT.anchorMax = new Vector2(1, 1);
        _scrollContentRT.pivot = new Vector2(0.5f, 1);
        _scrollContentRT.anchoredPosition = Vector2.zero;
        _scrollContentRT.sizeDelta = new Vector2(0, 0);

        var scrollRect = scrollArea.AddComponent<ScrollRect>();
        scrollRect.content = _scrollContentRT;
        scrollRect.viewport = vpRT;
        scrollRect.vertical = true;
        scrollRect.horizontal = false;
        scrollRect.scrollSensitivity = 30f;

        ScrollbarHelper.CreateVerticalScrollbar(scrollRect, scrollArea.transform);

        // Close button
        _closeBtn = MakeButton(_rootPanel.transform, "CloseBtn",
            new Vector2(0, -halfH + 22), new Vector2(120, 32),
            "Cancel", new Color(0.5f, 0.25f, 0.25f), Color.white, 14);
        _closeBtn.onClick.AddListener(Close);

        _overlayPanel.SetActive(false);
    }

    // ========== OPEN / CLOSE ==========

    /// <summary>
    /// Open the panel showing useable items for the given character.
    /// </summary>
    public void Open(CharacterController character)
    {
        if (character == null || character.Stats == null) return;
        _character = character;

        // Build item list from inventory
        BuildItemList();

        // Reset UI state
        _currentFilter = ItemCategory.All;
        _currentSort = SortMode.Alphabetical;
        _searchText = "";
        if (_searchField != null) _searchField.text = "";
        UpdateSortButtonLabel();
        UpdateFilterHighlights();

        // Apply filter and refresh display
        ApplyFilterAndSort();

        _titleText.text = $"USE ITEM — {character.Stats.CharacterName}";
        _overlayPanel.SetActive(true);
        IsOpen = true;
    }

    public void Close()
    {
        _overlayPanel.SetActive(false);
        IsOpen = false;
        _character = null;
        OnCancelled?.Invoke();
    }

    // ========== ITEM LIST BUILDING ==========

    private void BuildItemList()
    {
        _allEntries.Clear();

        if (_character == null) return;

        var invComp = _character.InventoryComp;
        var inv = invComp != null ? invComp.CharacterInventory : null;
        if (inv == null || inv.GeneralSlots == null) return;

        for (int i = 0; i < inv.GeneralSlots.Length; i++)
        {
            ItemData item = inv.GeneralSlots[i];
            if (item == null || !item.IsConsumable) continue;

            var category = ClassifyItem(item);
            int spellLevel = GetSpellLevel(item);

            _allEntries.Add(new ConsumableEntry
            {
                Item = item,
                InventoryIndex = i,
                Category = category,
                SpellLevel = spellLevel
            });
        }
    }

    /// <summary>
    /// Classify a consumable item into Potion, Scroll, or Alchemical based on its name/properties.
    /// </summary>
    private ItemCategory ClassifyItem(ItemData item)
    {
        if (item == null) return ItemCategory.Alchemical;

        // Scrolls: use the IsScroll flag (set by ScrollFactory), or fallback to name/ID patterns
        if (item.IsScroll)
            return ItemCategory.Scroll;

        // Wands: use the IsWand flag (set by WandFactory)
        if (item.IsWand)
            return ItemCategory.Wand;

        // Potions: use the IsPotion flag (set by PotionFactory), or fallback to name/ID patterns
        if (item.IsPotion)
            return ItemCategory.Potion;

        string nameLower = (item.Name ?? "").ToLowerInvariant();
        string idLower = (item.Id ?? "").ToLowerInvariant();

        // Potions: name starts with "Potion" or "Oil", or ID contains "potion_" or "oil_"
        if (nameLower.StartsWith("potion") || nameLower.StartsWith("oil")
            || idLower.Contains("potion_") || idLower.Contains("oil_"))
            return ItemCategory.Potion;

        // Scrolls: fallback name pattern
        if (nameLower.StartsWith("scroll") || idLower.Contains("scroll_"))
            return ItemCategory.Scroll;

        // Spell-effect consumables that don't match above patterns — check the ConsumableEffect
        if (item.ConsumableEffect == ConsumableEffectType.SpellEffect
            && !string.IsNullOrEmpty(item.ConsumableSpellName))
        {
            // If it has a spell effect but doesn't match potion/scroll naming, treat as alchemical
            return ItemCategory.Alchemical;
        }

        // Healing consumables without "potion" in name (edge case)
        if (item.ConsumableEffect == ConsumableEffectType.HealHP)
            return ItemCategory.Potion;

        // Everything else is alchemical (alchemist fire, tanglefoot bag, acid flask, etc.)
        return ItemCategory.Alchemical;
    }

    /// <summary>
    /// Get spell level for a consumable (scrolls/potions that emulate spells).
    /// Returns -1 if not applicable.
    /// </summary>
    private int GetSpellLevel(ItemData item)
    {
        if (item == null) return -1;

        // Scrolls store spell level directly
        if (item.IsScroll)
            return item.ScrollSpellLevel;

        // Wands store spell level directly
        if (item.IsWand)
            return item.WandSpellLevel;

        if (string.IsNullOrEmpty(item.ConsumableSpellName))
            return -1;

        SpellDatabase.Init();
        SpellData spell = SpellDatabase.GetSpell(item.ConsumableSpellName);
        if (spell == null) return -1;

        return spell.SpellLevel;
    }

    // ========== FILTER / SORT ==========

    private void SetFilter(ItemCategory category)
    {
        _currentFilter = category;
        UpdateFilterHighlights();
        ApplyFilterAndSort();
    }

    private void CycleSortMode()
    {
        switch (_currentSort)
        {
            case SortMode.Alphabetical:
                _currentSort = SortMode.SpellLevelAsc;
                break;
            case SortMode.SpellLevelAsc:
                _currentSort = SortMode.SpellLevelDesc;
                break;
            case SortMode.SpellLevelDesc:
                _currentSort = SortMode.Alphabetical;
                break;
        }
        UpdateSortButtonLabel();
        ApplyFilterAndSort();
    }

    private void OnSearchChanged(string text)
    {
        _searchText = text ?? "";
        ApplyFilterAndSort();
    }

    private void ApplyFilterAndSort()
    {
        _filteredEntries.Clear();

        foreach (var entry in _allEntries)
        {
            // Category filter
            if (_currentFilter != ItemCategory.All && entry.Category != _currentFilter)
                continue;

            // Search filter
            if (!string.IsNullOrEmpty(_searchText))
            {
                if (entry.Item.Name == null ||
                    entry.Item.Name.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
            }

            _filteredEntries.Add(entry);
        }

        // Sort
        switch (_currentSort)
        {
            case SortMode.Alphabetical:
                _filteredEntries.Sort((a, b) => string.Compare(a.Item.Name, b.Item.Name, StringComparison.OrdinalIgnoreCase));
                break;
            case SortMode.SpellLevelAsc:
                _filteredEntries.Sort((a, b) =>
                {
                    int cmp = a.SpellLevel.CompareTo(b.SpellLevel);
                    return cmp != 0 ? cmp : string.Compare(a.Item.Name, b.Item.Name, StringComparison.OrdinalIgnoreCase);
                });
                break;
            case SortMode.SpellLevelDesc:
                _filteredEntries.Sort((a, b) =>
                {
                    int cmp = b.SpellLevel.CompareTo(a.SpellLevel);
                    return cmp != 0 ? cmp : string.Compare(a.Item.Name, b.Item.Name, StringComparison.OrdinalIgnoreCase);
                });
                break;
        }

        // Update item count
        int totalUsable = _allEntries.Count;
        int showing = _filteredEntries.Count;
        _itemCountText.text = _currentFilter == ItemCategory.All && string.IsNullOrEmpty(_searchText)
            ? $"{totalUsable} useable item{(totalUsable != 1 ? "s" : "")}"
            : $"Showing {showing} of {totalUsable}";

        RefreshRows();
    }

    // ========== ROW RENDERING ==========

    private void RefreshRows()
    {
        // Clear old rows
        foreach (var row in _itemRows)
        {
            if (row.Row != null) Destroy(row.Row);
        }
        _itemRows.Clear();

        float contentW = _scrollContentRT.rect.width;
        if (contentW <= 0) contentW = PANEL_W - 40;
        float y = 0;

        for (int i = 0; i < _filteredEntries.Count; i++)
        {
            var entry = _filteredEntries[i];
            var row = CreateItemRow(entry, i, y, contentW);
            _itemRows.Add(row);
            y -= ROW_H + ROW_SPACING;
        }

        // Update scroll content size
        float totalH = _filteredEntries.Count * (ROW_H + ROW_SPACING);
        _scrollContentRT.sizeDelta = new Vector2(0, totalH);
    }

    private ItemRowUI CreateItemRow(ConsumableEntry entry, int index, float yPos, float contentW)
    {
        var rowUI = new ItemRowUI();

        // Row container
        rowUI.Row = new GameObject($"ItemRow_{index}");
        rowUI.Row.transform.SetParent(_scrollContent.transform, false);
        var rowRT = rowUI.Row.AddComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0, 1);
        rowRT.anchorMax = new Vector2(1, 1);
        rowRT.pivot = new Vector2(0.5f, 1);
        rowRT.anchoredPosition = new Vector2(0, yPos);
        rowRT.sizeDelta = new Vector2(0, ROW_H);

        // Background
        rowUI.Background = rowUI.Row.AddComponent<Image>();
        Color bgColor = index % 2 == 0
            ? new Color(0.14f, 0.14f, 0.2f, 0.8f)
            : new Color(0.16f, 0.16f, 0.22f, 0.8f);
        rowUI.Background.color = bgColor;

        // Make the entire row clickable
        rowUI.UseButton = rowUI.Row.AddComponent<Button>();
        var nav = rowUI.UseButton.navigation;
        nav.mode = Navigation.Mode.None;
        rowUI.UseButton.navigation = nav;

        int invIndex = entry.InventoryIndex;
        rowUI.UseButton.onClick.AddListener(() => OnItemClicked(invIndex));

        // Hover color
        var colors = rowUI.UseButton.colors;
        colors.highlightedColor = new Color(0.25f, 0.35f, 0.25f, 0.9f);
        colors.pressedColor = new Color(0.2f, 0.45f, 0.2f, 0.95f);
        colors.normalColor = bgColor;
        rowUI.UseButton.colors = colors;

        // Icon (left)
        string iconChar = !string.IsNullOrEmpty(entry.Item.IconChar) ? entry.Item.IconChar : GetCategoryIcon(entry.Category);
        Color iconColor = entry.Item.IconColor != default ? entry.Item.IconColor : Color.white;
        rowUI.IconText = MakeText(rowUI.Row.transform, "Icon",
            new Vector2(-contentW / 2f + 22, 0), new Vector2(32, ROW_H),
            iconChar, 18, iconColor, TextAnchor.MiddleCenter);

        // Name (with stack quantity if applicable)
        string displayName = entry.Item.Name ?? "Unknown Item";
        if (entry.Item.IsWand)
        {
            if (entry.Item.CurrentCharges <= 0)
                displayName = $"{displayName} [DEPLETED]";
            else
                displayName = $"{displayName} ({entry.Item.CurrentCharges}/{entry.Item.MaxCharges})";
        }
        else if (entry.Item.IsStackable && entry.Item.StackCount > 1)
            displayName = $"{displayName} (x{entry.Item.StackCount})";
        rowUI.NameText = MakeText(rowUI.Row.transform, "Name",
            new Vector2(-contentW / 2f + 56, 6), new Vector2(contentW - 120, 22),
            displayName, 14, Color.white, TextAnchor.MiddleLeft);
        rowUI.NameText.fontStyle = FontStyle.Bold;

        // Info line (category, spell level, description snippet)
        string infoStr = BuildInfoString(entry);
        rowUI.InfoText = MakeText(rowUI.Row.transform, "Info",
            new Vector2(-contentW / 2f + 56, -10), new Vector2(contentW - 120, 18),
            infoStr, 11, new Color(0.65f, 0.65f, 0.75f), TextAnchor.MiddleLeft);

        // Category color strip on left edge
        GameObject colorStrip = new GameObject("CategoryStrip");
        colorStrip.transform.SetParent(rowUI.Row.transform, false);
        var stripRT = colorStrip.AddComponent<RectTransform>();
        stripRT.anchorMin = new Vector2(0, 0);
        stripRT.anchorMax = new Vector2(0, 1);
        stripRT.pivot = new Vector2(0, 0.5f);
        stripRT.anchoredPosition = new Vector2(0, 0);
        stripRT.sizeDelta = new Vector2(4, 0);
        var stripImg = colorStrip.AddComponent<Image>();
        stripImg.color = GetCategoryColor(entry.Category);

        return rowUI;
    }

    private string BuildInfoString(ConsumableEntry entry)
    {
        var parts = new List<string>();

        // Category tag
        parts.Add(GetCategoryLabel(entry.Category));

        // Spell level for spell-based consumables
        if (entry.SpellLevel >= 0)
            parts.Add($"Spell Lv {entry.SpellLevel}");

        // CL for spell consumables
        if (entry.Item.ConsumableMinimumCasterLevel > 1)
            parts.Add($"CL {entry.Item.ConsumableMinimumCasterLevel}");

        // Healing info
        if (entry.Item.ConsumableEffect == ConsumableEffectType.HealHP)
        {
            if (entry.Item.HealDiceCount > 0)
                parts.Add($"{entry.Item.HealDiceCount}d{entry.Item.HealDiceSides}+{entry.Item.HealBonus} HP");
            else if (entry.Item.HealAmount > 0)
                parts.Add($"+{entry.Item.HealAmount} HP");
        }

        // Spell name for spell-effect consumables
        if (entry.Item.ConsumableEffect == ConsumableEffectType.SpellEffect
            && !string.IsNullOrEmpty(entry.Item.ConsumableSpellName))
        {
            // Show display name (resolve from database if possible)
            SpellDatabase.Init();
            SpellData infoSpell = entry.Item.Scroll?.GetSpell()
                                  ?? SpellDatabase.GetSpell(entry.Item.ConsumableSpellName);
            parts.Add(infoSpell != null ? infoSpell.Name : entry.Item.ConsumableSpellName);
        }

        // Magic domain indicator for arcane scrolls/wands usable via Magic domain
        if (_character != null && _character.Stats != null && _character.Stats.HasMagicDomain)
        {
            bool isArcaneScroll = entry.Item.IsScroll && string.Equals(entry.Item.ScrollType, "Arcane", StringComparison.OrdinalIgnoreCase);
            bool isArcaneWand = entry.Item.IsWand; // Wands don't track type — check wizard list instead
            if (isArcaneScroll || isArcaneWand)
            {
                int effWizLvl = _character.Stats.MagicDomainEffectiveWizardLevel;
                if (effWizLvl > 0)
                    parts.Add($"✦ Magic Domain (Wiz {effWizLvl})");
            }
        }

        return string.Join(" · ", parts);
    }

    private void OnItemClicked(int inventoryIndex)
    {
        if (_character == null) return;

        // Close the panel first
        _overlayPanel.SetActive(false);
        IsOpen = false;

        // Invoke the callback — GameManager handles the actual use, AoO provocation, etc.
        OnItemSelected?.Invoke(inventoryIndex);

        _character = null;
    }

    // ========== UI HELPERS ==========

    private void UpdateFilterHighlights()
    {
        SetButtonHighlight(_filterAllBtn, _currentFilter == ItemCategory.All);
        SetButtonHighlight(_filterPotionBtn, _currentFilter == ItemCategory.Potion);
        SetButtonHighlight(_filterScrollBtn, _currentFilter == ItemCategory.Scroll);
        SetButtonHighlight(_filterWandBtn, _currentFilter == ItemCategory.Wand);
        SetButtonHighlight(_filterAlchemBtn, _currentFilter == ItemCategory.Alchemical);
    }

    private void SetButtonHighlight(Button btn, bool active)
    {
        if (btn == null) return;
        var colors = btn.colors;
        colors.normalColor = active ? new Color(0.4f, 0.6f, 0.4f) : new Color(0.25f, 0.25f, 0.35f);
        btn.colors = colors;

        var img = btn.GetComponent<Image>();
        if (img != null) img.color = colors.normalColor;
    }

    private void UpdateSortButtonLabel()
    {
        if (_sortBtnText == null) return;
        switch (_currentSort)
        {
            case SortMode.Alphabetical:
                _sortBtnText.text = "Sort: A-Z";
                break;
            case SortMode.SpellLevelAsc:
                _sortBtnText.text = "Sort: Lv ↑";
                break;
            case SortMode.SpellLevelDesc:
                _sortBtnText.text = "Sort: Lv ↓";
                break;
        }
    }

    private string GetCategoryIcon(ItemCategory cat)
    {
        switch (cat)
        {
            case ItemCategory.Potion: return "🧪";
            case ItemCategory.Scroll: return "📜";
            case ItemCategory.Wand: return "🪄";
            case ItemCategory.Alchemical: return "🔥";
            default: return "•";
        }
    }

    private string GetCategoryLabel(ItemCategory cat)
    {
        switch (cat)
        {
            case ItemCategory.Potion: return "Potion";
            case ItemCategory.Scroll: return "Scroll";
            case ItemCategory.Wand: return "Wand";
            case ItemCategory.Alchemical: return "Alchemical";
            default: return "Item";
        }
    }

    private Color GetCategoryColor(ItemCategory cat)
    {
        switch (cat)
        {
            case ItemCategory.Potion: return new Color(0.8f, 0.2f, 0.2f);     // red
            case ItemCategory.Scroll: return new Color(0.3f, 0.3f, 0.85f);    // blue
            case ItemCategory.Wand: return new Color(0.6f, 0.35f, 0.8f);      // purple
            case ItemCategory.Alchemical: return new Color(0.8f, 0.6f, 0.15f); // amber
            default: return new Color(0.5f, 0.5f, 0.5f);
        }
    }

    // ========== PUBLIC QUERIES ==========

    /// <summary>
    /// Returns the count of useable consumable items in a character's inventory.
    /// Used by ActionButtonPanel to show/enable the Use Item button.
    /// </summary>
    public static int GetUseableItemCount(CharacterController character)
    {
        if (character == null) return 0;

        var invComp = character.InventoryComp;
        var inv = invComp != null ? invComp.CharacterInventory : null;
        if (inv == null || inv.GeneralSlots == null) return 0;

        int count = 0;
        for (int i = 0; i < inv.GeneralSlots.Length; i++)
        {
            ItemData item = inv.GeneralSlots[i];
            if (item != null && item.IsConsumable)
                count++;
        }
        return count;
    }

    // ========== LOW-LEVEL UI CREATION ==========

    private GameObject MakePanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta, Color bgColor)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        var img = go.AddComponent<Image>();
        img.color = bgColor;
        return go;
    }

    private Text MakeText(Transform parent, string name,
        Vector2 pos, Vector2 size, string content, int fontSize,
        Color color, TextAnchor alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.AddComponent<CanvasRenderer>();
        var txt = go.AddComponent<Text>();
        txt.font = _font;
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = alignment;
        txt.text = content;
        txt.supportRichText = true;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        return txt;
    }

    private Button MakeButton(Transform parent, string name,
        Vector2 pos, Vector2 size, string label,
        Color bgColor, Color textColor, int fontSize)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = bgColor;
        var btn = go.AddComponent<Button>();
        var nav = btn.navigation;
        nav.mode = Navigation.Mode.None;
        btn.navigation = nav;

        var colors = btn.colors;
        colors.normalColor = bgColor;
        colors.highlightedColor = bgColor * 1.2f;
        colors.pressedColor = bgColor * 0.8f;
        btn.colors = colors;

        // Label
        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
        labelGO.AddComponent<CanvasRenderer>();
        var txt = labelGO.AddComponent<Text>();
        txt.font = _font;
        txt.fontSize = fontSize;
        txt.color = textColor;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.text = label;

        return btn;
    }

    private InputField MakeInputField(Transform parent, string name,
        Vector2 pos, Vector2 size, string placeholder)
    {
        // Container
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var bgImg = go.AddComponent<Image>();
        bgImg.color = new Color(0.18f, 0.18f, 0.25f, 0.95f);

        // Text child
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(6, 2);
        textRT.offsetMax = new Vector2(-6, -2);
        textGO.AddComponent<CanvasRenderer>();
        var textComp = textGO.AddComponent<Text>();
        textComp.font = _font;
        textComp.fontSize = 13;
        textComp.color = Color.white;
        textComp.alignment = TextAnchor.MiddleLeft;
        textComp.supportRichText = false;

        // Placeholder child
        GameObject phGO = new GameObject("Placeholder");
        phGO.transform.SetParent(go.transform, false);
        var phRT = phGO.AddComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero;
        phRT.anchorMax = Vector2.one;
        phRT.offsetMin = new Vector2(6, 2);
        phRT.offsetMax = new Vector2(-6, -2);
        phGO.AddComponent<CanvasRenderer>();
        var phText = phGO.AddComponent<Text>();
        phText.font = _font;
        phText.fontSize = 13;
        phText.color = new Color(0.5f, 0.5f, 0.6f);
        phText.alignment = TextAnchor.MiddleLeft;
        phText.text = placeholder;
        phText.fontStyle = FontStyle.Italic;

        // InputField component
        var inputField = go.AddComponent<InputField>();
        inputField.textComponent = textComp;
        inputField.placeholder = phText;
        inputField.characterLimit = 50;

        return inputField;
    }
}
