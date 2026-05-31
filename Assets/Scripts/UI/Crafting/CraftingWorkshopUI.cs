using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// D&D 3.5e Item Creation Feats - Crafting Workshop UI
// Fullscreen panel matching the game's pre-combat hub UI style.
// ============================================================================

/// <summary>
/// Main Crafting Workshop UI. Accessed from the Pre-Combat Hub.
/// Allows the player to browse craftable items by feat category,
/// preview costs, and execute crafting projects.
///
/// Layout:
/// - Left panel: Feat category tabs (only shows feats the crafter has)
/// - Center panel: Scrollable item list for the selected feat
/// - Right panel: Cost preview + craft button for the selected item
/// - Bottom: Character stats bar (gold, XP, level, caster level)
/// </summary>
public class CraftingWorkshopUI : MonoBehaviour
{
    // ============================== STATE ==============================

    private GameObject _root;
    private bool _isOpen;

    // Callbacks
    private Action _onClose;

    // Current crafter
    private CharacterStats _crafterStats;
    private SpellcastingComponent _spellComp;
    private Inventory _targetInventory;

    // Party-wide spell assistance
    private List<CharacterController> _partyMembers;
    private bool _useScrollsForMissing;

    // UI elements
    private Text _titleText;
    private Text _statsBarText;
    private Text _previewTitleText;
    private Text _previewCostText;
    private Text _previewDescText;
    private Text _previewWarningText;
    private Text _previewSpellSourcesText;
    private Button _craftButton;
    private Text _craftButtonLabel;
    private Button _scrollToggleButton;
    private Text _scrollToggleLabel;
    private GameObject _itemListContent;
    private GameObject _featTabsContent;
    private Text _categoryLabel;

    // State
    private CraftingFeatType? _selectedFeat;
    private CraftableItemDefinition _selectedItem;
    private List<CraftableItemDefinition> _currentItemList = new List<CraftableItemDefinition>();
    private List<GameObject> _itemButtons = new List<GameObject>();
    private List<GameObject> _featButtons = new List<GameObject>();
    private CraftingProject _currentProject;

    // For arms & armor upgrade: selected target item
    private ItemData _upgradeTarget;

    // Confirmation dialog
    private GameObject _confirmDialog;
    private Text _confirmText;

    // Debug mode — persists across open/close during session (static)
    private static bool _debugMode;
    private Image _debugToggleImage;
    private Text _debugToggleLabel;
    private Text _debugWarningText;

    // Metamagic scroll crafting state
    private GameObject _metamagicPanel;
    private Text _metamagicSummaryText;
    private Text _metamagicDCText;
    private List<GameObject> _metamagicToggleObjects = new List<GameObject>();
    private MetamagicData _currentMetamagic = new MetamagicData();
    private int _heightenTarget = -1; // Target level for Heighten (-1 = not set)
    private Text _heightenLevelText;
    private static readonly Color MetamagicPanelColor = new Color(0.1f, 0.08f, 0.18f, 0.9f);
    private static readonly Color MetamagicOnColor = new Color(0.3f, 0.5f, 0.8f, 1f);
    private static readonly Color MetamagicOffColor = new Color(0.15f, 0.18f, 0.3f, 0.8f);

    // Colors matching the game's UI style
    private static readonly Color BgColor = new Color(0.08f, 0.09f, 0.14f, 0.97f);
    private static readonly Color PanelColor = new Color(0.06f, 0.08f, 0.14f, 0.85f);
    private static readonly Color GoldTitleColor = new Color(0.97f, 0.87f, 0.45f, 1f);
    private static readonly Color TextColor = new Color(0.87f, 0.93f, 1f, 1f);
    private static readonly Color DimTextColor = new Color(0.6f, 0.65f, 0.75f, 1f);
    private static readonly Color SuccessColor = new Color(0.3f, 0.75f, 0.3f, 1f);
    private static readonly Color WarningColor = new Color(0.95f, 0.7f, 0.2f, 1f);
    private static readonly Color ErrorColor = new Color(0.9f, 0.3f, 0.3f, 1f);
    private static readonly Color ButtonColor = new Color(0.18f, 0.25f, 0.45f, 1f);
    private static readonly Color ButtonActiveColor = new Color(0.28f, 0.35f, 0.6f, 1f);
    private static readonly Color CraftBtnColor = new Color(0.2f, 0.55f, 0.3f, 1f);
    private static readonly Color CraftBtnDisabledColor = new Color(0.25f, 0.25f, 0.3f, 0.6f);
    private static readonly Color CloseButtonColor = new Color(0.55f, 0.23f, 0.23f, 1f);
    private static readonly Color DebugOnColor = new Color(0.85f, 0.35f, 0.15f, 1f);
    private static readonly Color DebugOffColor = new Color(0.4f, 0.2f, 0.1f, 0.7f);
    private static readonly Color DebugBannerColor = new Color(0.9f, 0.25f, 0.15f, 1f);

    public bool IsOpen => _isOpen;

    // ============================== OPEN / CLOSE ==============================

    /// <summary>
    /// Open the Crafting Workshop for the specified crafter.
    /// </summary>
    /// <param name="partyMembers">All active party members — used for party-wide spell prerequisite checking.</param>
    public void Open(CharacterStats crafter, SpellcastingComponent spellComp, Inventory inventory, Action onClose,
        List<CharacterController> partyMembers = null)
    {
        _crafterStats = crafter;
        _spellComp = spellComp;
        _targetInventory = inventory;
        _onClose = onClose;
        _partyMembers = partyMembers;
        _useScrollsForMissing = false;
        _selectedFeat = null;
        _selectedItem = null;
        _currentProject = null;
        _upgradeTarget = null;

        // Sync debug mode state to validator (static persists across open/close)
        CraftingValidator.DebugMode = _debugMode;

        EnsureBuilt();
        if (_root == null) return;

        // Ensure registry is initialized
        CraftableItemRegistry.Init();

        // Refresh debug toggle visuals (state persists but UI rebuilt only once)
        if (_debugToggleImage != null)
            _debugToggleImage.color = _debugMode ? DebugOnColor : DebugOffColor;
        if (_debugToggleLabel != null)
            _debugToggleLabel.text = _debugMode ? "🔧 DEBUG: ON" : "🔧 DEBUG: OFF";
        if (_debugWarningText != null)
            _debugWarningText.gameObject.SetActive(_debugMode);

        RefreshFeatTabs();
        ClearItemList();
        ClearPreview();
        UpdateStatsBar();

        _root.transform.SetAsLastSibling();
        _root.SetActive(true);
        _isOpen = true;

        Debug.Log($"[CraftingWorkshop] Opened for {crafter?.CharacterName ?? "NULL"}, CL {CraftingValidator.GetCrafterCasterLevel(crafter)}");
    }

    private void Update()
    {
        if (_isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
    }

    public void Close()
    {
        if (_root != null)
            _root.SetActive(false);

        _isOpen = false;
        _onClose?.Invoke();

        Debug.Log("[CraftingWorkshop] Closed.");
    }

    // ============================== BUILD UI ==============================

    private void EnsureBuilt()
    {
        if (_root != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[CraftingWorkshop] No Canvas found.");
            return;
        }

        // Root: fullscreen
        _root = CreatePanel(canvas.transform, "CraftingWorkshopRoot",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, BgColor);

        // Title
        _titleText = CreateText(_root.transform, "Title", "⚒ CRAFTING WORKSHOP",
            new Vector2(0.1f, 0.93f), new Vector2(0.9f, 0.99f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, 36, FontStyle.Bold,
            GoldTitleColor, TextAnchor.MiddleCenter);

        // ---- DEBUG MODE TOGGLE (top-right, visually distinct red/orange) ----
        var debugToggleObj = new GameObject("DebugToggleBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        debugToggleObj.transform.SetParent(_root.transform, false);
        var debugToggleRect = debugToggleObj.GetComponent<RectTransform>();
        debugToggleRect.anchorMin = new Vector2(0.75f, 0.93f);
        debugToggleRect.anchorMax = new Vector2(0.95f, 0.98f);
        debugToggleRect.pivot = new Vector2(1f, 1f);
        debugToggleRect.anchoredPosition = Vector2.zero;
        debugToggleRect.sizeDelta = Vector2.zero;
        _debugToggleImage = debugToggleObj.GetComponent<Image>();
        _debugToggleImage.color = _debugMode ? DebugOnColor : DebugOffColor;
        var debugBtn = debugToggleObj.GetComponent<Button>();
        debugBtn.onClick.AddListener(OnDebugToggleClicked);
        _debugToggleLabel = CreateText(debugToggleObj.transform, "Label",
            _debugMode ? "🔧 DEBUG: ON" : "🔧 DEBUG: OFF",
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, 14, FontStyle.Bold,
            Color.white, TextAnchor.MiddleCenter);

        // ---- DEBUG WARNING BANNER (below title, only visible when debug active) ----
        _debugWarningText = CreateText(_root.transform, "DebugWarning",
            "⚠ DEBUG MODE ACTIVE — All requirements bypassed, items are free ⚠",
            new Vector2(0.05f, 0.91f), new Vector2(0.95f, 0.935f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, 15, FontStyle.Bold,
            DebugBannerColor, TextAnchor.MiddleCenter);
        _debugWarningText.gameObject.SetActive(_debugMode);

        // Stats bar (bottom)
        var statsPanel = CreatePanel(_root.transform, "StatsBar",
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 0f), new Vector2(0f, 45f), new Color(0.04f, 0.05f, 0.1f, 0.95f));
        _statsBarText = CreateText(statsPanel.transform, "StatsText", "",
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, 16, FontStyle.Normal,
            TextColor, TextAnchor.MiddleCenter);

        // ---- LEFT: Feat category tabs ----
        var leftPanel = CreatePanel(_root.transform, "FeatTabsPanel",
            new Vector2(0.01f, 0.08f), new Vector2(0.19f, 0.92f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero, PanelColor);

        CreateText(leftPanel.transform, "FeatTabsLabel", "Crafting Feats",
            new Vector2(0f, 0.95f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, Vector2.zero, 18, FontStyle.Bold,
            GoldTitleColor, TextAnchor.MiddleCenter);

        // Scroll view for feat tabs
        _featTabsContent = CreateScrollView(leftPanel.transform, "FeatTabsScroll",
            new Vector2(0f, 0f), new Vector2(1f, 0.94f));

        // ---- CENTER: Item list ----
        var centerPanel = CreatePanel(_root.transform, "ItemListPanel",
            new Vector2(0.2f, 0.08f), new Vector2(0.6f, 0.92f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero, PanelColor);

        _categoryLabel = CreateText(centerPanel.transform, "CategoryLabel", "Select a crafting feat →",
            new Vector2(0f, 0.95f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, Vector2.zero, 18, FontStyle.Bold,
            GoldTitleColor, TextAnchor.MiddleCenter);

        _itemListContent = CreateScrollView(centerPanel.transform, "ItemListScroll",
            new Vector2(0f, 0f), new Vector2(1f, 0.94f));

        // ---- RIGHT: Cost preview + craft ----
        var rightPanel = CreatePanel(_root.transform, "PreviewPanel",
            new Vector2(0.61f, 0.08f), new Vector2(0.99f, 0.92f), new Vector2(1f, 0.5f),
            Vector2.zero, Vector2.zero, PanelColor);

        CreateText(rightPanel.transform, "PreviewHeader", "Item Details",
            new Vector2(0f, 0.95f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, Vector2.zero, 18, FontStyle.Bold,
            GoldTitleColor, TextAnchor.MiddleCenter);

        _previewTitleText = CreateText(rightPanel.transform, "PreviewTitle", "",
            new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.94f), new Vector2(0.5f, 1f),
            Vector2.zero, Vector2.zero, 20, FontStyle.Bold,
            TextColor, TextAnchor.UpperLeft);

        _previewDescText = CreateText(rightPanel.transform, "PreviewDesc", "Select an item to see details.",
            new Vector2(0.05f, 0.55f), new Vector2(0.95f, 0.82f), new Vector2(0.5f, 1f),
            Vector2.zero, Vector2.zero, 14, FontStyle.Normal,
            DimTextColor, TextAnchor.UpperLeft);

        _previewCostText = CreateText(rightPanel.transform, "PreviewCost", "",
            new Vector2(0.05f, 0.48f), new Vector2(0.95f, 0.55f), new Vector2(0.5f, 1f),
            Vector2.zero, Vector2.zero, 16, FontStyle.Normal,
            TextColor, TextAnchor.UpperLeft);

        // Spell source lines — shows who provides each required spell
        _previewSpellSourcesText = CreateText(rightPanel.transform, "PreviewSpellSources", "",
            new Vector2(0.05f, 0.22f), new Vector2(0.95f, 0.48f), new Vector2(0.5f, 1f),
            Vector2.zero, Vector2.zero, 13, FontStyle.Normal,
            TextColor, TextAnchor.UpperLeft);

        // ---- METAMAGIC PANEL (overlays spell sources area for scroll crafting) ----
        _metamagicPanel = CreatePanel(rightPanel.transform, "MetamagicPanel",
            new Vector2(0.02f, 0.22f), new Vector2(0.98f, 0.55f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, MetamagicPanelColor);

        CreateText(_metamagicPanel.transform, "MetamagicHeader", "✦ Metamagic Customization",
            new Vector2(0f, 0.88f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, Vector2.zero, 14, FontStyle.Bold,
            GoldTitleColor, TextAnchor.MiddleCenter);

        // Metamagic summary line (effective level + DC)
        _metamagicSummaryText = CreateText(_metamagicPanel.transform, "MetamagicSummary", "",
            new Vector2(0.03f, 0.74f), new Vector2(0.97f, 0.88f), new Vector2(0.5f, 1f),
            Vector2.zero, Vector2.zero, 12, FontStyle.Normal,
            TextColor, TextAnchor.MiddleLeft);

        // DC display
        _metamagicDCText = CreateText(_metamagicPanel.transform, "MetamagicDC", "",
            new Vector2(0.03f, 0.62f), new Vector2(0.97f, 0.74f), new Vector2(0.5f, 1f),
            Vector2.zero, Vector2.zero, 13, FontStyle.Bold,
            new Color(0.5f, 0.8f, 1f, 1f), TextAnchor.MiddleLeft);

        _metamagicPanel.SetActive(false); // Hidden until a scroll is selected

        // Scroll substitution toggle button
        var scrollToggleObj = new GameObject("ScrollToggleBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        scrollToggleObj.transform.SetParent(rightPanel.transform, false);
        var scrollToggleRect = scrollToggleObj.GetComponent<RectTransform>();
        scrollToggleRect.anchorMin = new Vector2(0.05f, 0.16f);
        scrollToggleRect.anchorMax = new Vector2(0.95f, 0.21f);
        scrollToggleRect.pivot = new Vector2(0.5f, 0.5f);
        scrollToggleRect.anchoredPosition = Vector2.zero;
        scrollToggleRect.sizeDelta = Vector2.zero;
        scrollToggleObj.GetComponent<Image>().color = ButtonColor;
        _scrollToggleButton = scrollToggleObj.GetComponent<Button>();
        _scrollToggleButton.onClick.AddListener(OnScrollToggleClicked);
        _scrollToggleLabel = CreateText(scrollToggleObj.transform, "Label", "📜 Use Scrolls for Missing Spells: OFF",
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, 13, FontStyle.Bold,
            Color.white, TextAnchor.MiddleCenter);

        _previewWarningText = CreateText(rightPanel.transform, "PreviewWarning", "",
            new Vector2(0.05f, 0.13f), new Vector2(0.95f, 0.16f), new Vector2(0.5f, 1f),
            Vector2.zero, Vector2.zero, 14, FontStyle.Italic,
            WarningColor, TextAnchor.UpperLeft);

        // Craft button
        var craftBtnObj = new GameObject("CraftButton", typeof(RectTransform), typeof(Image), typeof(Button));
        craftBtnObj.transform.SetParent(rightPanel.transform, false);
        var craftRect = craftBtnObj.GetComponent<RectTransform>();
        craftRect.anchorMin = new Vector2(0.1f, 0.05f);
        craftRect.anchorMax = new Vector2(0.9f, 0.13f);
        craftRect.pivot = new Vector2(0.5f, 0.5f);
        craftRect.anchoredPosition = Vector2.zero;
        craftRect.sizeDelta = Vector2.zero;

        craftBtnObj.GetComponent<Image>().color = CraftBtnDisabledColor;
        _craftButton = craftBtnObj.GetComponent<Button>();
        _craftButton.onClick.AddListener(OnCraftClicked);
        _craftButton.interactable = false;

        _craftButtonLabel = CreateText(craftBtnObj.transform, "Label", "Select an item to craft",
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, 18, FontStyle.Bold,
            Color.white, TextAnchor.MiddleCenter);

        // Confirmation dialog (hidden by default)
        BuildConfirmationDialog();

        // Close buttons — created LAST so they render on top of all panels
        CreateButtonAt(_root.transform, "CloseButton", "✕ Close", CloseButtonColor,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-10f, -8f), new Vector2(120f, 40f), () => Close());
        CreateButtonAt(_root.transform, "BackButton", "← Back to Hub", CloseButtonColor,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(10f, -8f), new Vector2(200f, 40f), () => Close());

        _root.SetActive(false);
    }

    // ============================== FEAT TABS ==============================

    private void RefreshFeatTabs()
    {
        ClearFeatTabs();
        if (_crafterStats == null) return;

        var craftingFeats = CraftingValidator.GetCraftingFeats(_crafterStats);
        if (craftingFeats.Count == 0)
        {
            CreateListItem(_featTabsContent.transform, "NoFeats", "No crafting feats", DimTextColor, 14, null);
            return;
        }

        float y = 0;
        foreach (var feat in craftingFeats)
        {
            string featName = CraftingConstants.GetFeatName(feat);
            string label = GetFeatTabLabel(feat);
            var captured = feat;

            var btn = CreateListButton(_featTabsContent.transform, $"FeatTab_{feat}", label,
                _selectedFeat == feat ? ButtonActiveColor : ButtonColor,
                new Vector2(0f, -y), new Vector2(0f, 40f),
                () => OnFeatTabClicked(captured));

            _featButtons.Add(btn);
            y += 44f;
        }

        // Update content height
        var contentRect = _featTabsContent.GetComponent<RectTransform>();
        contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, y);
    }

    private void OnFeatTabClicked(CraftingFeatType feat)
    {
        _selectedFeat = feat;
        _selectedItem = null;
        _upgradeTarget = null;

        ResetMetamagicState();
        RefreshFeatTabs();
        RefreshItemList(feat);
        ClearPreview();
    }

    // ============================== ITEM LIST ==============================

    private void RefreshItemList(CraftingFeatType feat)
    {
        ClearItemList();

        string featName = CraftingConstants.GetFeatName(feat);
        _categoryLabel.text = featName;

        // Get items based on feat type (debug mode shows ALL items from database)
        bool isDebug = _debugMode;
        switch (feat)
        {
            case CraftingFeatType.ScribeScroll:
                _currentItemList = isDebug
                    ? CraftableItemRegistry.GenerateAllScrollDefinitions()
                    : CraftableItemRegistry.GenerateScrollDefinitions(_crafterStats, _spellComp);
                break;

            case CraftingFeatType.BrewPotion:
                _currentItemList = isDebug
                    ? CraftableItemRegistry.GenerateAllPotionDefinitions()
                    : CraftableItemRegistry.GeneratePotionDefinitions(_crafterStats, _spellComp);
                break;

            case CraftingFeatType.CraftWand:
                _currentItemList = isDebug
                    ? CraftableItemRegistry.GenerateAllWandDefinitions()
                    : CraftableItemRegistry.GenerateWandDefinitions(_crafterStats, _spellComp);
                break;

            default:
                _currentItemList = CraftableItemRegistry.GetItemsForFeat(feat)
                    .OrderBy(d => d.MarketPriceGp)
                    .ThenBy(d => d.DisplayName)
                    .ToList();
                break;
        }

        if (_currentItemList.Count == 0)
        {
            CreateListItem(_itemListContent.transform, "NoItems",
                feat == CraftingFeatType.ScribeScroll || feat == CraftingFeatType.BrewPotion || feat == CraftingFeatType.CraftWand
                    ? "No eligible spells known."
                    : "No craftable items found.",
                DimTextColor, 14, null);
            return;
        }

        float y = 0;
        for (int i = 0; i < _currentItemList.Count; i++)
        {
            var item = _currentItemList[i];
            var cost = item.GetCraftingCost();

            string label = $"{item.DisplayName}\n  {cost.GoldCost:N0} gp, {cost.XPCost:N0} XP";
            var captured = item;

            var btn = CreateListButton(_itemListContent.transform, $"Item_{i}", label,
                ButtonColor, new Vector2(0f, -y), new Vector2(0f, 48f),
                () => OnItemSelected(captured));

            _itemButtons.Add(btn);
            y += 52f;
        }

        var contentRect = _itemListContent.GetComponent<RectTransform>();
        contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, y);
    }

    private void OnItemSelected(CraftableItemDefinition item)
    {
        _selectedItem = item;
        _upgradeTarget = null;

        // Reset metamagic state for the new item
        ResetMetamagicState();

        // Validate with party-wide spell checking and scroll substitution option
        _currentProject = CraftingValidator.Validate(
            item, _crafterStats, _spellComp, _upgradeTarget,
            _partyMembers, _useScrollsForMissing);

        // Set base DC on project for non-metamagic scrolls
        if (_currentProject != null && item.IsDynamic
            && item.RequiredFeat == CraftingFeatType.ScribeScroll
            && _currentProject.ScrollSavedDC == 0)
        {
            var spell = SpellDatabase.GetSpell(item.DynamicSpellId);
            if (spell != null)
            {
                int abilityMod = _crafterStats != null ? _crafterStats.GetPrimaryCastingModifier() : 0;
                _currentProject.ScrollSavedDC = 10 + spell.SpellLevel + abilityMod;
            }
        }

        RefreshPreview();

        // Show metamagic panel for scroll items
        if (item.IsDynamic && item.RequiredFeat == CraftingFeatType.ScribeScroll)
        {
            ShowMetamagicPanel();
        }
        else
        {
            HideMetamagicPanel();
        }
    }

    private void OnScrollToggleClicked()
    {
        _useScrollsForMissing = !_useScrollsForMissing;

        // Update toggle button appearance
        if (_scrollToggleLabel != null)
        {
            _scrollToggleLabel.text = _useScrollsForMissing
                ? "📜 Use Scrolls for Missing Spells: ON"
                : "📜 Use Scrolls for Missing Spells: OFF";
        }
        if (_scrollToggleButton != null)
        {
            _scrollToggleButton.GetComponent<Image>().color = _useScrollsForMissing
                ? ButtonActiveColor
                : ButtonColor;
        }

        // Re-validate the current item with the new scroll setting
        if (_selectedItem != null)
        {
            _currentProject = CraftingValidator.Validate(
                _selectedItem, _crafterStats, _spellComp, _upgradeTarget,
                _partyMembers, _useScrollsForMissing);
            RefreshPreview();
        }
    }

    // ============================== DEBUG TOGGLE ==============================

    private void OnDebugToggleClicked()
    {
        _debugMode = !_debugMode;
        CraftingValidator.DebugMode = _debugMode;

        // Update toggle button appearance
        if (_debugToggleImage != null)
            _debugToggleImage.color = _debugMode ? DebugOnColor : DebugOffColor;
        if (_debugToggleLabel != null)
            _debugToggleLabel.text = _debugMode ? "🔧 DEBUG: ON" : "🔧 DEBUG: OFF";

        // Show/hide warning banner
        if (_debugWarningText != null)
            _debugWarningText.gameObject.SetActive(_debugMode);

        // Refresh everything to reflect debug state
        _selectedItem = null;
        _currentProject = null;
        RefreshFeatTabs();

        if (_selectedFeat.HasValue)
            RefreshItemList(_selectedFeat.Value);
        else
            ClearItemList();

        ClearPreview();

        Debug.Log($"[CraftingWorkshop] Debug mode toggled: {_debugMode}");
    }

    // ============================== METAMAGIC PANEL ==============================

    /// <summary>
    /// Show the metamagic selection panel for the currently selected scroll spell.
    /// Populates toggles for each applicable metamagic feat the crafter has (or all in debug mode).
    /// </summary>
    private void ShowMetamagicPanel()
    {
        if (_metamagicPanel == null || _selectedItem == null) return;
        if (_selectedItem.RequiredFeat != CraftingFeatType.ScribeScroll || !_selectedItem.IsDynamic)
        {
            HideMetamagicPanel();
            return;
        }

        var spell = SpellDatabase.GetSpell(_selectedItem.DynamicSpellId);
        if (spell == null)
        {
            HideMetamagicPanel();
            return;
        }

        // Clear old toggles
        ClearMetamagicToggles();

        // Get available metamagic feats
        List<MetamagicFeatId> availableFeats;
        if (_debugMode)
        {
            // Show ALL metamagic feats in debug mode
            availableFeats = new List<MetamagicFeatId>(MetamagicData.AllMetamagicFeats);
        }
        else if (_spellComp != null)
        {
            availableFeats = _spellComp.GetKnownMetamagicFeats();
        }
        else
        {
            availableFeats = new List<MetamagicFeatId>();
        }

        // Filter to only feats applicable to this spell
        var applicableFeats = new List<MetamagicFeatId>();
        foreach (var feat in availableFeats)
        {
            if (MetamagicData.IsApplicable(feat, spell))
                applicableFeats.Add(feat);
        }

        if (applicableFeats.Count == 0)
        {
            // No applicable metamagic — show panel with "none available" message
            _metamagicPanel.SetActive(true);
            _metamagicSummaryText.text = "No applicable metamagic feats available.";
            _metamagicDCText.text = "";
            UpdateMetamagicSummary(spell);
            return;
        }

        _metamagicPanel.SetActive(true);

        // Create toggle buttons for each applicable metamagic feat
        float toggleY = 0.58f;
        float toggleHeight = 0.09f;
        float toggleSpacing = 0.01f;

        foreach (var feat in applicableFeats)
        {
            int levelAdj = MetamagicData.GetLevelAdjustment(feat);
            string levelText = feat == MetamagicFeatId.HeightenSpell ? "+var" : $"+{levelAdj}";
            string label = $"{MetamagicData.GetDisplayName(feat)} ({levelText} lvl)";

            bool isOn = _currentMetamagic.Has(feat);
            var captured = feat;

            var toggleObj = new GameObject($"MM_{feat}", typeof(RectTransform), typeof(Image), typeof(Button));
            toggleObj.transform.SetParent(_metamagicPanel.transform, false);

            var rect = toggleObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.03f, toggleY - toggleHeight);
            rect.anchorMax = new Vector2(0.97f, toggleY);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            toggleObj.GetComponent<Image>().color = isOn ? MetamagicOnColor : MetamagicOffColor;

            var btn = toggleObj.GetComponent<Button>();
            btn.onClick.AddListener(() => OnMetamagicToggled(captured, spell));

            CreateText(toggleObj.transform, "Label", (isOn ? "☑ " : "☐ ") + label,
                new Vector2(0.02f, 0f), new Vector2(0.98f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, 11, isOn ? FontStyle.Bold : FontStyle.Normal,
                Color.white, TextAnchor.MiddleLeft);

            _metamagicToggleObjects.Add(toggleObj);
            toggleY -= toggleHeight + toggleSpacing;

            // For Heighten, add a level selector row
            if (feat == MetamagicFeatId.HeightenSpell && isOn)
            {
                var heightenRow = new GameObject("HeightenRow", typeof(RectTransform));
                heightenRow.transform.SetParent(_metamagicPanel.transform, false);
                var hRect = heightenRow.GetComponent<RectTransform>();
                hRect.anchorMin = new Vector2(0.03f, toggleY - toggleHeight);
                hRect.anchorMax = new Vector2(0.97f, toggleY);
                hRect.pivot = new Vector2(0.5f, 0.5f);
                hRect.anchoredPosition = Vector2.zero;
                hRect.sizeDelta = Vector2.zero;

                int effTarget = _heightenTarget > spell.SpellLevel ? _heightenTarget : spell.SpellLevel + 1;
                _heightenTarget = effTarget;
                _currentMetamagic.HeightenToLevel = effTarget;

                // - button
                CreateButtonAt(heightenRow.transform, "HeightenDec", "◀",
                    ButtonColor, new Vector2(0.1f, 0f), new Vector2(0.1f, 0f), new Vector2(0f, 0f),
                    Vector2.zero, new Vector2(30f, 24f),
                    () => AdjustHeightenLevel(-1, spell));

                // Level display
                _heightenLevelText = CreateText(heightenRow.transform, "HeightenLvl",
                    $"Heighten to Level: {effTarget}",
                    new Vector2(0.15f, 0f), new Vector2(0.85f, 1f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero, 12, FontStyle.Bold,
                    Color.white, TextAnchor.MiddleCenter);

                // + button
                CreateButtonAt(heightenRow.transform, "HeightenInc", "▶",
                    ButtonColor, new Vector2(0.9f, 0f), new Vector2(0.9f, 0f), new Vector2(1f, 0f),
                    Vector2.zero, new Vector2(30f, 24f),
                    () => AdjustHeightenLevel(+1, spell));

                _metamagicToggleObjects.Add(heightenRow);
                toggleY -= toggleHeight + toggleSpacing;
            }
        }

        UpdateMetamagicSummary(spell);
    }

    private void HideMetamagicPanel()
    {
        if (_metamagicPanel != null) _metamagicPanel.SetActive(false);
        ClearMetamagicToggles();
    }

    private void ClearMetamagicToggles()
    {
        foreach (var obj in _metamagicToggleObjects)
        {
            if (obj != null) Destroy(obj);
        }
        _metamagicToggleObjects.Clear();
        _heightenLevelText = null;
    }

    private void ResetMetamagicState()
    {
        _currentMetamagic = new MetamagicData();
        _heightenTarget = -1;
    }

    private void OnMetamagicToggled(MetamagicFeatId feat, SpellData spell)
    {
        _currentMetamagic.Toggle(feat);

        // If Heighten was toggled on, initialize target level
        if (feat == MetamagicFeatId.HeightenSpell)
        {
            if (_currentMetamagic.Has(feat))
            {
                _heightenTarget = spell.SpellLevel + 1;
                _currentMetamagic.HeightenToLevel = _heightenTarget;
            }
            else
            {
                _heightenTarget = -1;
                _currentMetamagic.HeightenToLevel = -1;
            }
        }

        // Rebuild toggles and revalidate
        ShowMetamagicPanel();
        RevalidateWithMetamagic();
    }

    private void AdjustHeightenLevel(int delta, SpellData spell)
    {
        int minLevel = spell.SpellLevel + 1;
        int maxLevel = 9;

        // Get caster's max castable level for upper bound
        if (_spellComp != null && !_debugMode)
            maxLevel = Mathf.Min(9, _spellComp.GetMaxCastableSpellLevel());

        _heightenTarget = Mathf.Clamp(_heightenTarget + delta, minLevel, maxLevel);
        _currentMetamagic.HeightenToLevel = _heightenTarget;

        if (_heightenLevelText != null)
            _heightenLevelText.text = $"Heighten to Level: {_heightenTarget}";

        UpdateMetamagicSummary(spell);
        RevalidateWithMetamagic();
    }

    private void UpdateMetamagicSummary(SpellData spell)
    {
        if (spell == null || _metamagicSummaryText == null || _metamagicDCText == null) return;

        int baseLevel = spell.SpellLevel;
        int effLevel = _currentMetamagic.GetEffectiveSpellLevel(baseLevel);

        if (!_currentMetamagic.HasAnyMetamagic)
        {
            _metamagicSummaryText.text = $"Base Level: {baseLevel} (no metamagic)";
        }
        else
        {
            // Build adjustment breakdown
            var parts = new List<string>();
            parts.Add($"Base: {baseLevel}");
            foreach (var mm in _currentMetamagic.AppliedMetamagic)
            {
                int adj = _currentMetamagic.GetLevelAdjustment(mm, baseLevel);
                parts.Add($"{MetamagicData.GetAdjective(mm)} (+{adj})");
            }
            _metamagicSummaryText.text = $"{string.Join(" + ", parts)} = Eff. Level: {effLevel}";
        }

        // DC calculation: 10 + (base level + heighten bonus ONLY) + caster ability modifier
        // D&D 3.5e PHB p.88: Only Heighten Spell increases save DC; other metamagic
        // raises slot level for cost/preparation but NOT for DC.
        int heightenBonus = (_currentMetamagic.Has(MetamagicFeatId.HeightenSpell)
            && _currentMetamagic.HeightenToLevel > baseLevel)
            ? _currentMetamagic.HeightenToLevel - baseLevel : 0;
        int dcLevel = baseLevel + heightenBonus;
        int abilityMod = _crafterStats != null ? _crafterStats.GetPrimaryCastingModifier() : 0;
        int dc = 10 + dcLevel + abilityMod;

        string abilityName = GetCasterAbilityName();
        string dcLabel = heightenBonus > 0
            ? $"Save DC: {dc} (10 + {dcLevel} heightened + {abilityMod} {abilityName})"
            : $"Save DC: {dc} (10 + {dcLevel} spell + {abilityMod} {abilityName})";
        _metamagicDCText.text = dcLabel;

        // Validate effective level against max castable
        int maxCastable = 9;
        if (_spellComp != null && !_debugMode)
            maxCastable = _spellComp.GetMaxCastableSpellLevel();

        int rawLevel = _currentMetamagic.GetRawEffectiveSpellLevel(baseLevel);
        if (rawLevel > maxCastable && !_debugMode)
        {
            _metamagicDCText.text += $"\n❌ Effective level {rawLevel} exceeds max castable ({maxCastable})!";
            _metamagicDCText.color = ErrorColor;
        }
        else if (rawLevel > 9 && !_debugMode)
        {
            _metamagicDCText.text += $"\n❌ Effective level {rawLevel} exceeds maximum (9)!";
            _metamagicDCText.color = ErrorColor;
        }
        else
        {
            _metamagicDCText.color = new Color(0.5f, 0.8f, 1f, 1f);
        }
    }

    private string GetCasterAbilityName()
    {
        if (_crafterStats == null) return "MOD";
        string cls = _crafterStats.CharacterClass;
        if (string.IsNullOrEmpty(cls)) return "MOD";
        switch (cls)
        {
            case "Wizard": return "INT";
            case "Sorcerer": case "Bard": return "CHA";
            case "Cleric": case "Druid": case "Ranger": case "Paladin": return "WIS";
            default: return "MOD";
        }
    }

    /// <summary>
    /// Revalidate the current project with metamagic applied.
    /// Recalculates cost and validates effective spell level.
    /// </summary>
    private void RevalidateWithMetamagic()
    {
        if (_selectedItem == null || _selectedItem.RequiredFeat != CraftingFeatType.ScribeScroll) return;

        var spell = SpellDatabase.GetSpell(_selectedItem.DynamicSpellId);
        if (spell == null) return;

        int baseLevel = spell.SpellLevel;
        int effLevel = _currentMetamagic.HasAnyMetamagic
            ? _currentMetamagic.GetEffectiveSpellLevel(baseLevel)
            : baseLevel;
        int rawLevel = _currentMetamagic.GetRawEffectiveSpellLevel(baseLevel);

        // Validate effective level
        int maxCastable = 9;
        if (_spellComp != null && !_debugMode)
            maxCastable = _spellComp.GetMaxCastableSpellLevel();

        if (!_debugMode && rawLevel > maxCastable)
        {
            // Invalid — too high
            _currentProject = new CraftingProject
            {
                Definition = _selectedItem,
                Crafter = _crafterStats,
                IsValid = false,
                ValidationError = $"Effective spell level {rawLevel} exceeds your max castable level ({maxCastable})."
            };
            RefreshPreview();
            return;
        }

        if (!_debugMode && rawLevel > 9)
        {
            _currentProject = new CraftingProject
            {
                Definition = _selectedItem,
                Crafter = _crafterStats,
                IsValid = false,
                ValidationError = $"Effective spell level {rawLevel} exceeds maximum 9th level."
            };
            RefreshPreview();
            return;
        }

        // Recalculate with the validator (handles all other checks)
        _currentProject = CraftingValidator.Validate(
            _selectedItem, _crafterStats, _spellComp, _upgradeTarget,
            _partyMembers, _useScrollsForMissing);

        // Overlay metamagic data on the project
        if (_currentMetamagic.HasAnyMetamagic)
        {
            var feats = new List<MetamagicFeatId>(_currentMetamagic.AppliedMetamagic);
            _currentProject.ScrollMetamagicFeats = feats;
            _currentProject.ScrollEffectiveSpellLevel = effLevel;

            // Store Heighten target level separately for DC reconstruction
            bool hasHeighten = _currentMetamagic.Has(MetamagicFeatId.HeightenSpell)
                && _currentMetamagic.HeightenToLevel > baseLevel;
            _currentProject.ScrollHeightenToLevel = hasHeighten ? _currentMetamagic.HeightenToLevel : -1;

            // DC = 10 + (base level + heighten bonus ONLY) + caster ability modifier
            // D&D 3.5e: Only Heighten increases DC; Empower/Maximize/etc. do NOT.
            int dcLevel = hasHeighten ? _currentMetamagic.HeightenToLevel : baseLevel;
            int abilityMod = _crafterStats != null ? _crafterStats.GetPrimaryCastingModifier() : 0;
            _currentProject.ScrollSavedDC = 10 + dcLevel + abilityMod;

            // Recalculate cost using effective spell level
            int casterLevel = _selectedItem.RequiredCasterLevel;
            int effCL = CraftingCostCalculator.MinimumCasterLevelForSpell(effLevel);
            if (effCL < casterLevel) effCL = casterLevel;
            int newMarketPrice = CraftingCostCalculator.ScrollMarketPrice(effLevel, effCL);
            var newCost = CraftingCostCalculator.FromMarketPrice(newMarketPrice);

            if (!CraftingValidator.DebugMode)
            {
                _currentProject.GoldCost = newCost.GoldCost;
                _currentProject.XPCost = newCost.XPCost;
                _currentProject.CraftingDays = newCost.CraftingDays;
            }
            _currentProject.MarketPriceGp = newMarketPrice;
            _currentProject.ItemCasterLevel = effCL;

            // Re-check gold/XP if not debug
            if (!CraftingValidator.DebugMode && _crafterStats != null)
            {
                if (_crafterStats.ComponentGold < _currentProject.GoldCost)
                {
                    _currentProject.IsValid = false;
                    _currentProject.ValidationError = $"Insufficient gold for metamagic scroll. Need {_currentProject.GoldCost:N0} gp, have {_crafterStats.ComponentGold:N0} gp.";
                }
                else if (_currentProject.XPCost > _crafterStats.MaxSpendableXP())
                {
                    _currentProject.IsValid = false;
                    _currentProject.ValidationError = $"Insufficient XP for metamagic scroll. Need {_currentProject.XPCost:N0} XP.";
                }
            }
        }
        else
        {
            // No metamagic — set base DC
            int abilityMod = _crafterStats != null ? _crafterStats.GetPrimaryCastingModifier() : 0;
            _currentProject.ScrollEffectiveSpellLevel = baseLevel;
            _currentProject.ScrollSavedDC = 10 + baseLevel + abilityMod;
        }

        RefreshPreview();
    }

    // ============================== PREVIEW PANEL ==============================

    private void RefreshPreview()
    {
        if (_selectedItem == null || _currentProject == null)
        {
            ClearPreview();
            return;
        }

        var item = _selectedItem;
        var project = _currentProject;

        _previewTitleText.text = item.DisplayName;
        _previewDescText.text = !string.IsNullOrEmpty(item.Description)
            ? item.Description
            : "No description available.";

        // Cost breakdown
        string costText = $"💰 Gold Cost: {project.GoldCost:N0} gp";
        if (project.ScrollCostGp > 0)
            costText += $"  (includes 📜 {project.ScrollCostGp:N0} gp scrolls)";
        costText += $"\n✨ XP Cost: {project.XPCost:N0}" +
            $"\n🕐 Crafting Time: {project.CraftingDays} day{(project.CraftingDays != 1 ? "s" : "")}" +
            $"\n📊 Market Value: {project.MarketPriceGp:N0} gp" +
            $"\n🎯 Required CL: {item.RequiredCasterLevel}";

        _previewCostText.text = costText;

        // Spell source lines — show who provides each required spell
        string spellSourceText = "";
        if (project.SpellSources != null && project.SpellSources.Sources.Count > 0)
        {
            spellSourceText = "Spell Prerequisites:\n";
            foreach (var source in project.SpellSources.Sources)
            {
                spellSourceText += $"  {source.GetDisplayLine()}\n";
            }

            if (project.SpellcraftDC > 0)
                spellSourceText += $"\n🎲 Spellcraft DC: {project.SpellcraftDC}";
        }
        else if (item.RequiredSpellIds != null && item.RequiredSpellIds.Count > 0)
        {
            // Fallback if SpellSources wasn't populated (shouldn't happen)
            spellSourceText = $"⚠ Missing Spells ({project.MissingSpells.Count}):";
            foreach (string spellId in project.MissingSpells)
            {
                var spell = SpellDatabase.GetSpell(spellId);
                string spellName = spell != null ? spell.Name : spellId;
                spellSourceText += $"\n  • {spellName}";
            }
            if (project.SpellcraftDC > 0)
                spellSourceText += $"\nSpellcraft DC: {project.SpellcraftDC}";
        }

        if (_previewSpellSourcesText != null)
            _previewSpellSourcesText.text = spellSourceText;

        // Show/hide scroll toggle based on whether there are spell requirements
        bool hasSpellReqs = item.RequiredSpellIds != null && item.RequiredSpellIds.Count > 0;
        if (_scrollToggleButton != null)
            _scrollToggleButton.gameObject.SetActive(hasSpellReqs);

        // Warning / error
        if (!project.IsValid)
        {
            _previewWarningText.text = $"❌ {project.ValidationError}";
            _previewWarningText.color = ErrorColor;
            _craftButton.interactable = false;
            _craftButtonLabel.text = "Cannot Craft";
            _craftButton.GetComponent<Image>().color = CraftBtnDisabledColor;
        }
        else if (project.MissingSpells.Count > 0)
        {
            _previewWarningText.text = $"⚠ Missing spell prerequisites — Spellcraft DC {project.SpellcraftDC} required.";
            _previewWarningText.color = WarningColor;
            _craftButton.interactable = true;
            _craftButtonLabel.text = $"⚒ Craft ({project.GoldCost:N0} gp, {project.XPCost:N0} XP)";
            _craftButton.GetComponent<Image>().color = CraftBtnColor;
        }
        else
        {
            _previewWarningText.text = "✅ All prerequisites met.";
            _previewWarningText.color = SuccessColor;
            _craftButton.interactable = true;
            _craftButtonLabel.text = $"⚒ Craft ({project.GoldCost:N0} gp, {project.XPCost:N0} XP)";
            _craftButton.GetComponent<Image>().color = CraftBtnColor;
        }
    }

    private void ClearPreview()
    {
        if (_previewTitleText != null) _previewTitleText.text = "";
        if (_previewDescText != null) _previewDescText.text = "Select an item to see details.";
        if (_previewCostText != null) _previewCostText.text = "";
        if (_previewSpellSourcesText != null) _previewSpellSourcesText.text = "";
        if (_previewWarningText != null) _previewWarningText.text = "";
        if (_scrollToggleButton != null) _scrollToggleButton.gameObject.SetActive(false);
        HideMetamagicPanel();
        ResetMetamagicState();
        if (_craftButton != null)
        {
            _craftButton.interactable = false;
            _craftButtonLabel.text = "Select an item to craft";
            _craftButton.GetComponent<Image>().color = CraftBtnDisabledColor;
        }
    }

    // ============================== CRAFT EXECUTION ==============================

    private void OnCraftClicked()
    {
        if (_currentProject == null || !_currentProject.IsValid) return;

        // Show confirmation dialog
        ShowConfirmation(_currentProject);
    }

    private void ExecuteCrafting()
    {
        if (_currentProject == null || !_currentProject.IsValid) return;

        var result = CraftingExecutor.Execute(_currentProject, _targetInventory);

        if (result.Success)
        {
            Debug.Log($"[CraftingWorkshop] ✅ {result.Message}");

            // Show success message in preview
            _previewWarningText.text = $"✅ {result.Message}";
            _previewWarningText.color = SuccessColor;

            // Refresh everything
            UpdateStatsBar();
            _selectedItem = null;
            _currentProject = null;

            // Refresh item list for the current feat
            if (_selectedFeat.HasValue)
                RefreshItemList(_selectedFeat.Value);

            ClearPreview();
        }
        else
        {
            Debug.LogWarning($"[CraftingWorkshop] ❌ {result.Message}");
            _previewWarningText.text = $"❌ {result.Message}";
            _previewWarningText.color = ErrorColor;
        }
    }

    // ============================== CONFIRMATION DIALOG ==============================

    private void BuildConfirmationDialog()
    {
        _confirmDialog = CreatePanel(_root.transform, "ConfirmDialog",
            new Vector2(0.25f, 0.3f), new Vector2(0.75f, 0.7f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, new Color(0.05f, 0.06f, 0.12f, 0.98f));

        CreateText(_confirmDialog.transform, "ConfirmTitle", "Confirm Crafting",
            new Vector2(0.05f, 0.85f), new Vector2(0.95f, 0.97f), new Vector2(0.5f, 1f),
            Vector2.zero, Vector2.zero, 24, FontStyle.Bold,
            GoldTitleColor, TextAnchor.MiddleCenter);

        _confirmText = CreateText(_confirmDialog.transform, "ConfirmBody", "",
            new Vector2(0.05f, 0.25f), new Vector2(0.95f, 0.83f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, 16, FontStyle.Normal,
            TextColor, TextAnchor.UpperCenter);

        // Confirm button
        CreateButtonAt(_confirmDialog.transform, "ConfirmYes", "✅ Craft Item",
            CraftBtnColor,
            new Vector2(0.3f, 0.05f), new Vector2(0.3f, 0.05f), new Vector2(0.5f, 0f),
            new Vector2(0f, 0f), new Vector2(180f, 44f),
            () =>
            {
                _confirmDialog.SetActive(false);
                ExecuteCrafting();
            });

        // Cancel button
        CreateButtonAt(_confirmDialog.transform, "ConfirmNo", "Cancel",
            CloseButtonColor,
            new Vector2(0.7f, 0.05f), new Vector2(0.7f, 0.05f), new Vector2(0.5f, 0f),
            new Vector2(0f, 0f), new Vector2(140f, 44f),
            () => _confirmDialog.SetActive(false));

        _confirmDialog.SetActive(false);
    }

    private void ShowConfirmation(CraftingProject project)
    {
        if (_confirmDialog == null || project == null) return;

        string summary = project.GetSummary();

        // Add spell source summary to confirmation
        if (project.SpellSources != null && project.SpellSources.Sources.Count > 0)
        {
            int partyProvided = 0;
            int scrollCount = 0;
            foreach (var src in project.SpellSources.Sources)
            {
                if (src.SourceType == SpellSourceType.PartyMemberKnown) partyProvided++;
                else if (src.SourceType == SpellSourceType.ScrollSubstitute) scrollCount++;
            }
            if (partyProvided > 0)
                summary += $"\n👥 {partyProvided} spell{(partyProvided != 1 ? "s" : "")} provided by party members";
            if (scrollCount > 0)
                summary += $"\n📜 {scrollCount} spell{(scrollCount != 1 ? "s" : "")} via scroll substitution ({project.ScrollCostGp:N0} gp)";
        }

        summary += $"\n\nAfter crafting:\n" +
            $"  Gold: {_crafterStats.ComponentGold:N0} → {_crafterStats.ComponentGold - project.GoldCost:N0} gp\n" +
            $"  XP: {_crafterStats.ExperiencePoints:N0} → {_crafterStats.ExperiencePoints - project.XPCost:N0}";

        _confirmText.text = summary;
        _confirmDialog.transform.SetAsLastSibling();
        _confirmDialog.SetActive(true);
    }

    // ============================== STATS BAR ==============================

    private void UpdateStatsBar()
    {
        if (_statsBarText == null || _crafterStats == null) return;

        int cl = CraftingValidator.GetCrafterCasterLevel(_crafterStats);
        int maxXP = _crafterStats.MaxSpendableXP();

        _statsBarText.text = $"Crafter: {_crafterStats.CharacterName} | " +
            $"Level {_crafterStats.Level} | CL {cl} | " +
            $"Gold: {_crafterStats.ComponentGold:N0} gp | " +
            $"XP: {_crafterStats.ExperiencePoints:N0} (max spendable: {maxXP:N0})";
    }

    // ============================== HELPERS ==============================

    private string GetFeatTabLabel(CraftingFeatType feat)
    {
        switch (feat)
        {
            case CraftingFeatType.ScribeScroll: return "📜 Scribe Scroll";
            case CraftingFeatType.BrewPotion: return "🧪 Brew Potion";
            case CraftingFeatType.CraftWondrousItem: return "✨ Wondrous Items";
            case CraftingFeatType.CraftMagicArmsAndArmor: return "⚔ Arms & Armor";
            case CraftingFeatType.CraftWand: return "🪄 Craft Wand";
            case CraftingFeatType.CraftRod: return "🔱 Craft Rod";
            case CraftingFeatType.CraftStaff: return "🏑 Craft Staff";
            case CraftingFeatType.ForgeRing: return "💍 Forge Ring";
            default: return feat.ToString();
        }
    }

    private void ClearItemList()
    {
        foreach (var btn in _itemButtons)
        {
            if (btn != null) Destroy(btn);
        }
        _itemButtons.Clear();
        _currentItemList.Clear();
    }

    private void ClearFeatTabs()
    {
        foreach (var btn in _featButtons)
        {
            if (btn != null) Destroy(btn);
        }
        _featButtons.Clear();
    }

    // ============================== UI FACTORY METHODS ==============================
    // Matches the existing UI style from PreCombatHubUI

    private static GameObject CreatePanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 size, Color color)
    {
        var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);

        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        panel.GetComponent<Image>().color = color;
        return panel;
    }

    private static Text CreateText(Transform parent, string name, string value,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 size,
        int fontSize, FontStyle fontStyle, Color color, TextAnchor alignment)
    {
        var textObj = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObj.transform.SetParent(parent, false);

        var rect = textObj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        var text = textObj.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = alignment;
        text.text = value;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        return text;
    }

    private static GameObject CreateScrollView(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        // Viewport
        var viewport = new GameObject(name + "_Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(parent, false);

        var vpRect = viewport.GetComponent<RectTransform>();
        vpRect.anchorMin = anchorMin;
        vpRect.anchorMax = anchorMax;
        vpRect.pivot = new Vector2(0f, 1f);
        vpRect.anchoredPosition = Vector2.zero;
        vpRect.sizeDelta = Vector2.zero;

        viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f); // Nearly invisible for mask
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        // Content container
        var content = new GameObject(name + "_Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);

        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        // ScrollRect
        var scrollObj = viewport.AddComponent<ScrollRect>();
        scrollObj.content = contentRect;
        scrollObj.viewport = vpRect;
        scrollObj.horizontal = false;
        scrollObj.vertical = true;
        scrollObj.scrollSensitivity = 30f;

        return content;
    }

    private static GameObject CreateListButton(Transform parent, string name, string label,
        Color bgColor, Vector2 offset, Vector2 size, Action onClick)
    {
        var btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);

        var rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;

        btnObj.GetComponent<Image>().color = bgColor;

        var btn = btnObj.GetComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());

        CreateText(btnObj.transform, "Label", label,
            new Vector2(0.03f, 0f), new Vector2(0.97f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, 13, FontStyle.Normal,
            Color.white, TextAnchor.MiddleLeft);

        return btnObj;
    }

    private static void CreateListItem(Transform parent, string name, string label,
        Color textColor, int fontSize, Action onClick)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 30f);

        CreateText(obj.transform, "Label", label,
            new Vector2(0.05f, 0f), new Vector2(0.95f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, fontSize, FontStyle.Italic,
            textColor, TextAnchor.MiddleCenter);
    }

    private static Button CreateButtonAt(Transform parent, string name, string label, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 size, Action onClick)
    {
        var btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);

        var rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        btnObj.GetComponent<Image>().color = color;

        var btn = btnObj.GetComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());

        CreateText(btnObj.transform, "Label", label,
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, 16, FontStyle.Bold,
            Color.white, TextAnchor.MiddleCenter);

        return btn;
    }
}
