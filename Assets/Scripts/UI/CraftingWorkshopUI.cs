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

        EnsureBuilt();
        if (_root == null) return;

        // Ensure registry is initialized
        CraftableItemRegistry.Init();

        RefreshFeatTabs();
        ClearItemList();
        ClearPreview();
        UpdateStatsBar();

        _root.transform.SetAsLastSibling();
        _root.SetActive(true);
        _isOpen = true;

        Debug.Log($"[CraftingWorkshop] Opened for {crafter?.CharacterName ?? "NULL"}, CL {CraftingValidator.GetCrafterCasterLevel(crafter)}");
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

        // Stats bar (bottom)
        var statsPanel = CreatePanel(_root.transform, "StatsBar",
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 0f), new Vector2(0f, 45f), new Color(0.04f, 0.05f, 0.1f, 0.95f));
        _statsBarText = CreateText(statsPanel.transform, "StatsText", "",
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, 16, FontStyle.Normal,
            TextColor, TextAnchor.MiddleCenter);

        // Close button
        CreateButtonAt(_root.transform, "CloseButton", "← Back to Hub", CloseButtonColor,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -310f), new Vector2(260f, 44f), () => Close());

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

        // Get items based on feat type
        switch (feat)
        {
            case CraftingFeatType.ScribeScroll:
                _currentItemList = CraftableItemRegistry.GenerateScrollDefinitions(_crafterStats, _spellComp);
                break;

            case CraftingFeatType.BrewPotion:
                _currentItemList = CraftableItemRegistry.GeneratePotionDefinitions(_crafterStats, _spellComp);
                break;

            case CraftingFeatType.CraftWand:
                _currentItemList = CraftableItemRegistry.GenerateWandDefinitions(_crafterStats, _spellComp);
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

        // Validate with party-wide spell checking and scroll substitution option
        _currentProject = CraftingValidator.Validate(
            item, _crafterStats, _spellComp, _upgradeTarget,
            _partyMembers, _useScrollsForMissing);

        RefreshPreview();
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
        else if (item.RequiredSpells != null && item.RequiredSpells.Count > 0)
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
        bool hasSpellReqs = item.RequiredSpells != null && item.RequiredSpells.Count > 0;
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
