using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// D&D 3.5 Feat Selection UI Panel
// Used during character creation and level-up
// ============================================================================

/// <summary>
/// UI panel for selecting feats during character creation or level-up.
/// Supports general feat selection and fighter bonus feat selection.
/// Shows available feats (green), unavailable feats (grayed), prerequisites, and benefits.
/// </summary>
public class FeatSelectionUI : MonoBehaviour
{
    // ========== STATE ==========
    private CharacterStats _stats;
    private bool _isBuilt = false;
    private bool _isFighterBonus = false;
    private bool _isMonkBonus = false;
    private int _monkBonusLevel = 0; // The specific monk level for bonus feat filtering (1, 2, or 6)
    private bool _isWizardBonus = false; // Wizard bonus feat mode (metamagic + item creation + Spell Mastery)
    private int _featsToSelect = 1;
    private List<string> _selectedFeats = new List<string>();
    private List<FeatDefinition> _allFeats;
    private List<FeatDefinition> _monkBonusFeatList; // Filtered list for monk bonus feats
    private List<FeatDefinition> _wizardBonusFeatList; // Filtered list for wizard bonus feats
    private string _filterType = "All";
    private string _searchText = "";

    // ========== CALLBACKS ==========
    public Action<List<string>> OnFeatsConfirmed;

    // ========== UI REFERENCES ==========
    private GameObject _overlay;
    private CanvasGroup _canvasGroup;
    private GameObject _rootPanel;
    private Text _titleText;
    private Text _subtitleText;
    private Text _featsRemainingText;
    private Text _selectedFeatsText;
    private Text _featDetailText;
    private InputField _searchInput;
    private GameObject _scrollContent;
    private ScrollRect _scrollRect;
    private Button _confirmButton;
    private List<FeatRowUI> _featRows = new List<FeatRowUI>();
    private List<Button> _filterButtons = new List<Button>();

    // ========== FONT ==========
    private Font _font;

    // ========== COLORS ==========
    private static readonly Color COLOR_BG = new Color(0.12f, 0.12f, 0.15f, 0.95f);
    private static readonly Color COLOR_PANEL = new Color(0.18f, 0.18f, 0.22f, 1f);
    private static readonly Color COLOR_AVAILABLE = new Color(0.2f, 0.35f, 0.2f, 1f);
    private static readonly Color COLOR_SELECTED = new Color(0.2f, 0.4f, 0.6f, 1f);
    private static readonly Color COLOR_UNAVAILABLE = new Color(0.25f, 0.2f, 0.2f, 1f);
    private static readonly Color COLOR_ROW_ALT = new Color(0.16f, 0.16f, 0.2f, 1f);
    private static readonly Color COLOR_BUTTON = new Color(0.25f, 0.5f, 0.25f, 1f);
    private static readonly Color COLOR_BUTTON_DISABLED = new Color(0.3f, 0.3f, 0.3f, 1f);
    private static readonly Color COLOR_FILTER_ACTIVE = new Color(0.3f, 0.5f, 0.7f, 1f);
    private static readonly Color COLOR_FILTER_INACTIVE = new Color(0.25f, 0.25f, 0.3f, 1f);

    /// <summary>Whether the panel is currently open.</summary>
    public bool IsOpen => _overlay != null && _overlay.activeInHierarchy;

    // ========================================================================
    // BUILD UI
    // ========================================================================

    /// <summary>Build the feat selection UI. Call once during scene setup.</summary>
    public void BuildUI(Canvas canvas)
    {
        if (_isBuilt) return;

        // Load font with fallbacks (matching other UI panels)
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        if (_font == null) Debug.LogError("[FeatSelectionUI] CRITICAL: No font could be loaded! Text will be invisible.");
        else Debug.Log($"[FeatSelectionUI] Font loaded: {_font.name}");

        // Full-screen overlay
        _overlay = new GameObject("FeatSelectionOverlay");
        _overlay.transform.SetParent(canvas.transform, false);
        var overlayRT = _overlay.AddComponent<RectTransform>();
        overlayRT.anchorMin = Vector2.zero;
        overlayRT.anchorMax = Vector2.one;
        overlayRT.sizeDelta = Vector2.zero;
        var overlayImg = _overlay.AddComponent<Image>();
        overlayImg.color = new Color(0, 0, 0, 0.7f);
        _canvasGroup = _overlay.AddComponent<CanvasGroup>();

        // Root panel (centered, large)
        _rootPanel = CreatePanel(_overlay.transform, "FeatPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(900, 620), COLOR_BG);

        // Title
        _titleText = MakeText(_rootPanel.transform, "Title",
            new Vector2(0, 286), new Vector2(860, 40), "Select Feats", 24, Color.white, TextAnchor.MiddleCenter);

        // Subtitle (restriction description)
        _subtitleText = MakeText(_rootPanel.transform, "Subtitle",
            new Vector2(0, 258), new Vector2(860, 22), "", 13, new Color(0.75f, 0.85f, 1f), TextAnchor.MiddleCenter);

        // Feats remaining counter
        _featsRemainingText = MakeText(_rootPanel.transform, "FeatsRemaining",
            new Vector2(0, 238), new Vector2(860, 25), "Feats to select: 1", 14, Color.yellow, TextAnchor.MiddleCenter);

        // ===== LEFT SIDE: Feat list with filters =====
        var leftPanel = CreatePanel(_rootPanel.transform, "LeftPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-190, -10), new Vector2(500, 470), COLOR_PANEL);

        // Filter buttons row
        float filterY = 220;
        string[] filters = { "All", "Combat", "Ranged", "Defensive", "TWF", "Skill", "Metamagic", "Crafting", "General" };
        float filterStartX = -235;
        float filterWidth = 56;
        for (int i = 0; i < filters.Length; i++)
        {
            float x = filterStartX + i * (filterWidth + 4);
            var btn = MakeButton(leftPanel.transform, $"Filter_{filters[i]}",
                new Vector2(x, filterY), new Vector2(filterWidth, 22), filters[i],
                COLOR_FILTER_INACTIVE, Color.white, 10);
            string filterName = filters[i];
            btn.onClick.AddListener(() => OnFilterClicked(filterName));
            _filterButtons.Add(btn);
        }

        // Search input
        var searchGO = new GameObject("SearchInput");
        searchGO.transform.SetParent(leftPanel.transform, false);
        var searchRT = searchGO.AddComponent<RectTransform>();
        searchRT.anchorMin = searchRT.anchorMax = new Vector2(0.5f, 0.5f);
        searchRT.pivot = new Vector2(0.5f, 0.5f);
        searchRT.anchoredPosition = new Vector2(0, 195);
        searchRT.sizeDelta = new Vector2(480, 22);
        var searchBg = searchGO.AddComponent<Image>();
        searchBg.color = new Color(0.1f, 0.1f, 0.12f, 1f);
        _searchInput = searchGO.AddComponent<InputField>();

        var searchPlaceholder = MakeText(searchGO.transform, "Placeholder",
            Vector2.zero, new Vector2(470, 22), "  Search feats...", 11, new Color(0.5f, 0.5f, 0.5f), TextAnchor.MiddleLeft);
        var searchPlaceholderRT = searchPlaceholder.GetComponent<RectTransform>();
        searchPlaceholderRT.anchorMin = Vector2.zero;
        searchPlaceholderRT.anchorMax = Vector2.one;
        searchPlaceholderRT.sizeDelta = Vector2.zero;
        searchPlaceholderRT.anchoredPosition = Vector2.zero;

        var searchTextComp = MakeText(searchGO.transform, "SearchText",
            Vector2.zero, new Vector2(470, 22), "", 11, Color.white, TextAnchor.MiddleLeft);
        var searchTextRT = searchTextComp.GetComponent<RectTransform>();
        searchTextRT.anchorMin = Vector2.zero;
        searchTextRT.anchorMax = Vector2.one;
        searchTextRT.sizeDelta = new Vector2(-10, 0);
        searchTextRT.anchoredPosition = new Vector2(5, 0);

        _searchInput.textComponent = searchTextComp;
        _searchInput.placeholder = searchPlaceholder;
        _searchInput.onValueChanged.AddListener(OnSearchChanged);

        // Scroll area for feats
        var scrollGO = new GameObject("FeatScroll");
        scrollGO.transform.SetParent(leftPanel.transform, false);
        var scrollRT = scrollGO.AddComponent<RectTransform>();
        scrollRT.anchorMin = scrollRT.anchorMax = new Vector2(0.5f, 0.5f);
        scrollRT.pivot = new Vector2(0.5f, 0.5f);
        scrollRT.anchoredPosition = new Vector2(0, -20);
        scrollRT.sizeDelta = new Vector2(490, 400);
        scrollGO.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f, 1f);
        var mask = scrollGO.AddComponent<Mask>();
        mask.showMaskGraphic = true;
        _scrollRect = scrollGO.AddComponent<ScrollRect>();
        _scrollRect.horizontal = false;
        _scrollRect.vertical = true;
        _scrollRect.movementType = ScrollRect.MovementType.Clamped;
        _scrollRect.scrollSensitivity = 30f;

        _scrollContent = new GameObject("Content");
        _scrollContent.transform.SetParent(scrollGO.transform, false);
        var contentRT = _scrollContent.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = new Vector2(0, 0);
        _scrollRect.content = contentRT;

        // ===== RIGHT SIDE: Selected feats and details =====
        var rightPanel = CreatePanel(_rootPanel.transform, "RightPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(260, 60), new Vector2(360, 330), COLOR_PANEL);

        MakeText(rightPanel.transform, "SelLabel",
            new Vector2(0, 150), new Vector2(340, 25), "Selected Feats:", 14, Color.yellow, TextAnchor.MiddleLeft);

        _selectedFeatsText = MakeText(rightPanel.transform, "SelFeats",
            new Vector2(0, 100), new Vector2(340, 80), "(none)", 12, Color.white, TextAnchor.UpperLeft);
        _selectedFeatsText.verticalOverflow = VerticalWrapMode.Overflow;

        MakeText(rightPanel.transform, "DetailLabel",
            new Vector2(0, 40), new Vector2(340, 25), "Feat Details:", 14, Color.cyan, TextAnchor.MiddleLeft);

        _featDetailText = MakeText(rightPanel.transform, "DetailText",
            new Vector2(0, -60), new Vector2(340, 170), "Click a feat to see details", 11, new Color(0.8f, 0.8f, 0.8f), TextAnchor.UpperLeft);
        _featDetailText.verticalOverflow = VerticalWrapMode.Overflow;

        // Confirm button
        _confirmButton = MakeButton(_rootPanel.transform, "ConfirmBtn",
            new Vector2(310, -280), new Vector2(160, 36), "Confirm Feats",
            COLOR_BUTTON, Color.white, 14);
        _confirmButton.onClick.AddListener(OnConfirmClicked);

        // Close/Cancel button
        var cancelBtn = MakeButton(_rootPanel.transform, "CancelBtn",
            new Vector2(150, -280), new Vector2(120, 36), "Cancel",
            new Color(0.5f, 0.25f, 0.25f, 1f), Color.white, 14);
        cancelBtn.onClick.AddListener(Close);

        SetPanelVisible(false);
        _isBuilt = true;
        Debug.Log("[FeatSelectionUI] UI built successfully.");
    }

    // ========================================================================
    // OPEN / CLOSE
    // ========================================================================

    /// <summary>
    /// Open the feat selection panel for selecting feats.
    /// </summary>
    /// <param name="stats">Character stats to check prerequisites against</param>
    /// <param name="featsToSelect">Number of feats to select</param>
    /// <param name="fighterBonusOnly">If true, only show fighter bonus feats</param>
    /// <param name="title">Custom title for the selection dialog (overrides default)</param>
    /// <param name="subtitle">Optional subtitle describing restrictions or context</param>
    /// <param name="monkBonusLevel">If > 0, only show monk bonus feats for this specific monk level (1, 2, or 6). Prerequisites are bypassed.</param>
    /// <param name="wizardBonus">If true, only show Wizard bonus feats (metamagic + item creation + Spell Mastery). Prerequisites enforced.</param>
    public void OpenForSelection(CharacterStats stats, int featsToSelect, bool fighterBonusOnly = false,
        string title = null, string subtitle = null, int monkBonusLevel = 0, bool wizardBonus = false)
    {
        if (!_isBuilt)
        {
            Debug.LogError("[FeatSelectionUI] Cannot open - UI has not been built! Call BuildUI() first.");
            return;
        }

        _stats = stats;
        _featsToSelect = featsToSelect;
        _isFighterBonus = fighterBonusOnly;
        _isMonkBonus = monkBonusLevel > 0;
        _monkBonusLevel = monkBonusLevel;
        _isWizardBonus = wizardBonus;
        _monkBonusFeatList = null;
        _wizardBonusFeatList = null;
        _selectedFeats.Clear();
        _filterType = "All";
        _searchText = "";

        // Pre-fetch monk bonus feats if in monk bonus mode
        if (_isMonkBonus)
        {
            _monkBonusFeatList = FeatDefinitions.GetMonkBonusFeatsForLevel(monkBonusLevel, stats);
            Debug.Log($"[FeatSelectionUI] Monk bonus mode: level {monkBonusLevel}, " +
                      $"{_monkBonusFeatList.Count} feats available: " +
                      string.Join(", ", _monkBonusFeatList.ConvertAll(f => f.FeatName)));
        }

        // Pre-fetch Wizard bonus feats if in Wizard bonus mode
        // Shows ALL wizard bonus feats (qualified and unqualified) for display, filtering happens in CreateFeatRow
        if (_isWizardBonus)
        {
            _wizardBonusFeatList = FeatDefinitions.GetAllWizardBonusFeats();
            Debug.Log($"[FeatSelectionUI] Wizard bonus mode: {_wizardBonusFeatList.Count} total wizard bonus feats available");
        }

        // Set title - use custom title if provided, otherwise generate a default
        if (!string.IsNullOrEmpty(title))
        {
            _titleText.text = title;
        }
        else
        {
            string defaultTitle;
            if (_isMonkBonus)
                defaultTitle = $"Select Monk Bonus Feat (Level {monkBonusLevel})";
            else if (_isWizardBonus)
                defaultTitle = featsToSelect > 1 ? $"Select {featsToSelect} Wizard Bonus Feats" : "Select Wizard Bonus Feat";
            else if (fighterBonusOnly)
                defaultTitle = featsToSelect > 1 ? $"Select {featsToSelect} Fighter Bonus Feats" : "Select Fighter Bonus Feat";
            else
                defaultTitle = featsToSelect > 1 ? $"Select {featsToSelect} Feats" : "Select Feat";
            _titleText.text = defaultTitle;
        }

        // Set subtitle - restriction/context description
        if (_subtitleText != null)
        {
            _subtitleText.text = subtitle ?? "";
            _subtitleText.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));
        }

        if (_searchInput != null)
            _searchInput.text = "";

        FeatDefinitions.Init();

        string modeStr = _isMonkBonus ? $" (monk bonus level {monkBonusLevel})" :
                         _isWizardBonus ? " (wizard bonus)" :
                         fighterBonusOnly ? " (fighter bonus only)" : "";
        Debug.Log($"[FeatSelectionUI] Opening for {stats.CharacterName} ({stats.CharacterClass}): " +
                  $"{featsToSelect} feats to select{modeStr}" +
                  $", total feats in database: {FeatDefinitions.AllFeats.Count}");

        PopulateFeatList();
        UpdateSelectedDisplay();
        SetPanelVisible(true);

        // Ensure the overlay renders on top of all other UI by moving it to the
        // end of the sibling list. This is critical when re-opening for Fighter
        // bonus feat selection, as other overlays may have been created after ours.
        if (_overlay != null)
            _overlay.transform.SetAsLastSibling();

        Debug.Log($"[FeatSelectionUI] Opened successfully. Feat rows created: {_featRows.Count}, " +
                  $"overlay active: {_overlay.activeSelf}, font loaded: {(_font != null ? _font.name : "NULL")}");
    }

    /// <summary>Close the panel.</summary>
    public void Close()
    {
        SetPanelVisible(false);
        Debug.Log("[FeatSelectionUI] Closed.");
    }

    // ========================================================================
    // POPULATE FEAT LIST
    // ========================================================================

    private void PopulateFeatList()
    {
        // Clear old rows
        foreach (var row in _featRows)
        {
            if (row.RowGO != null)
                Destroy(row.RowGO);
        }
        _featRows.Clear();

        // In monk bonus mode, use the pre-filtered monk bonus feat list
        if (_isMonkBonus && _monkBonusFeatList != null)
        {
            _allFeats = _monkBonusFeatList;
            Debug.Log($"[FeatSelectionUI] PopulateFeatList (Monk bonus level {_monkBonusLevel}): " +
                      $"{_allFeats.Count} feats available");
        }
        // In wizard bonus mode, use the pre-filtered wizard bonus feat list
        else if (_isWizardBonus && _wizardBonusFeatList != null)
        {
            _allFeats = _wizardBonusFeatList;
            Debug.Log($"[FeatSelectionUI] PopulateFeatList (Wizard bonus): " +
                      $"{_allFeats.Count} wizard bonus feats available");
        }
        else
        {
            // Get all feats sorted
            _allFeats = FeatDefinitions.GetAllFeatsSorted();
        }

        Debug.Log($"[FeatSelectionUI] PopulateFeatList: total feats={_allFeats.Count}, " +
                  $"isFighterBonus={_isFighterBonus}, isMonkBonus={_isMonkBonus}, " +
                  $"filter={_filterType}, search='{_searchText}'");

        float rowHeight = 30f;
        int visibleIndex = 0;
        int filteredOutByBonus = 0;
        int filteredOutByType = 0;
        int filteredOutBySearch = 0;

        for (int i = 0; i < _allFeats.Count; i++)
        {
            var feat = _allFeats[i];

            // In monk bonus mode, skip filtering/searching (list is small and specific)
            // In wizard bonus mode, allow type filtering and search within the wizard bonus feats
            if (!_isMonkBonus)
            {
                // Apply filters
                if (!PassesFilter(feat))
                {
                    if (_isFighterBonus && !feat.IsFighterBonus)
                        filteredOutByBonus++;
                    else
                        filteredOutByType++;
                    continue;
                }

                // Apply search
                if (!string.IsNullOrEmpty(_searchText) &&
                    !feat.FeatName.ToLower().Contains(_searchText.ToLower()))
                {
                    filteredOutBySearch++;
                    continue;
                }
            }

            CreateFeatRow(feat, visibleIndex, rowHeight);
            visibleIndex++;
        }

        // Set content height
        var contentRT = _scrollContent.GetComponent<RectTransform>();
        contentRT.sizeDelta = new Vector2(0, visibleIndex * rowHeight + 10);

        Debug.Log($"[FeatSelectionUI] PopulateFeatList results: {visibleIndex} rows visible, " +
                  $"filtered out: {filteredOutByBonus} (not fighter bonus), {filteredOutByType} (type filter), " +
                  $"{filteredOutBySearch} (search). Content height: {contentRT.sizeDelta.y}");
    }

    private bool PassesFilter(FeatDefinition feat)
    {
        // Fighter bonus mode: always filter to fighter bonus feats regardless of type filter
        if (_isFighterBonus && !feat.IsFighterBonus) return false;

        // Wizard bonus mode: already pre-filtered, but respect type filter within wizard feats
        if (_isWizardBonus && !feat.IsWizardBonus) return false;

        if (_filterType == "All") return true;

        switch (_filterType)
        {
            case "Combat": return feat.Type == FeatType.Combat;
            case "Ranged": return feat.Type == FeatType.Ranged;
            case "Defensive": return feat.Type == FeatType.Defensive;
            case "TWF": return feat.Type == FeatType.TwoWeaponFighting;
            case "Skill": return feat.Type == FeatType.Skill;
            case "General": return feat.Type == FeatType.General || feat.Type == FeatType.Unarmed || feat.Type == FeatType.MountedCombat;
            case "Metamagic": return feat.Type == FeatType.Metamagic;
            case "Crafting": return feat.Type == FeatType.ItemCreation;
            default: return true;
        }
    }

    private void CreateFeatRow(FeatDefinition feat, int index, float rowHeight)
    {
        var row = new FeatRowUI();
        row.Feat = feat;

        // Monk bonus feats bypass ALL prerequisites per D&D 3.5e rules
        bool meetsPrereqs = _isMonkBonus ? true : feat.MeetsPrerequisites(_stats);
        bool alreadyHas = !feat.CanTakeMultiple && _stats.HasFeat(feat.FeatName);
        bool isSelected = _selectedFeats.Contains(feat.FeatName);
        bool canSelect = meetsPrereqs && !alreadyHas && !isSelected;

        // If fighter bonus only, also check that
        if (_isFighterBonus && !feat.IsFighterBonus)
        {
            canSelect = false;
            meetsPrereqs = false;
        }

        // Row background
        Color bgColor;
        if (isSelected) bgColor = COLOR_SELECTED;
        else if (canSelect) bgColor = (index % 2 == 0) ? COLOR_AVAILABLE : new Color(0.22f, 0.37f, 0.22f, 1f);
        else bgColor = (index % 2 == 0) ? COLOR_UNAVAILABLE : new Color(0.27f, 0.22f, 0.22f, 1f);

        row.RowGO = new GameObject($"FeatRow_{feat.FeatName}");
        row.RowGO.transform.SetParent(_scrollContent.transform, false);
        var rowRT = row.RowGO.AddComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0, 1);
        rowRT.anchorMax = new Vector2(1, 1);
        rowRT.pivot = new Vector2(0.5f, 1);
        rowRT.anchoredPosition = new Vector2(0, -index * rowHeight);
        rowRT.sizeDelta = new Vector2(0, rowHeight);

        var rowImg = row.RowGO.AddComponent<Image>();
        rowImg.color = bgColor;
        row.BgImage = rowImg;

        // Feat name button (clickable for details)
        var nameBtn = MakeButton(row.RowGO.transform, "NameBtn",
            new Vector2(-130, 0), new Vector2(200, 26),
            feat.FeatName, new Color(0, 0, 0, 0), meetsPrereqs ? Color.white : new Color(0.5f, 0.5f, 0.5f), 11);
        nameBtn.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
        string featName = feat.FeatName;
        nameBtn.onClick.AddListener(() => OnFeatNameClicked(featName));

        // Type label
        string typeStr = GetTypeShortString(feat.Type);
        MakeText(row.RowGO.transform, "Type",
            new Vector2(30, 0), new Vector2(70, 26), typeStr, 9,
            GetTypeColor(feat.Type), TextAnchor.MiddleCenter);

        // Bonus feat indicator (Fighter/Wizard)
        string bonusStr = feat.IsWizardBonus ? "W" : (feat.IsFighterBonus ? "F" : "");
        Color bonusColor = feat.IsWizardBonus ? new Color(0.4f, 0.6f, 1f) : new Color(0.8f, 0.6f, 0.2f);
        MakeText(row.RowGO.transform, "FBonus",
            new Vector2(70, 0), new Vector2(20, 26), bonusStr, 9,
            bonusColor, TextAnchor.MiddleCenter);

        // Select/deselect button
        string btnLabel;
        Color btnColor;
        if (isSelected) { btnLabel = "Remove"; btnColor = new Color(0.6f, 0.3f, 0.3f, 1f); }
        else if (canSelect) { btnLabel = "Select"; btnColor = COLOR_BUTTON; }
        else if (alreadyHas) { btnLabel = "Has"; btnColor = COLOR_BUTTON_DISABLED; }
        else { btnLabel = "Locked"; btnColor = COLOR_BUTTON_DISABLED; }

        row.SelectButton = MakeButton(row.RowGO.transform, "SelectBtn",
            new Vector2(170, 0), new Vector2(65, 24), btnLabel, btnColor, Color.white, 10);
        row.SelectButton.interactable = canSelect || isSelected;
        string fn = feat.FeatName;
        row.SelectButton.onClick.AddListener(() => OnSelectClicked(fn));

        _featRows.Add(row);
    }

    // ========================================================================
    // EVENT HANDLERS
    // ========================================================================

    private void OnFeatNameClicked(string featName)
    {
        var feat = FeatDefinitions.GetFeat(featName);
        if (feat == null) return;

        bool meetsPrereqs = _isMonkBonus ? true : feat.MeetsPrerequisites(_stats);
        var unmet = feat.GetUnmetPrerequisites(_stats);

        string detail = $"<color=#FFD700><b>{feat.FeatName}</b></color>\n";
        detail += $"<color=#88AACC>Type: {feat.Type}</color>";
        if (feat.IsFighterBonus) detail += " <color=#CC9933>[Fighter Bonus]</color>";
        if (feat.IsMonkBonus) detail += " <color=#66CCAA>[Monk Bonus]</color>";
        if (feat.IsWizardBonus) detail += " <color=#6699FF>[Wizard Bonus]</color>";
        if (feat.IsActive) detail += " <color=#CC6633>[Active]</color>";
        if (feat.IsPlaceholder) detail += "\n<color=#FF9933>[⚠ PLACEHOLDER - Mechanics not yet implemented]</color>";
        detail += "\n\n";

        detail += $"<color=#AADDAA>Benefit:</color> {feat.Benefit.Description}\n\n";

        detail += $"<color=#CCCCAA>Prerequisites:</color> {feat.GetPrerequisitesString()}\n";

        if (_isMonkBonus && unmet.Count > 0)
        {
            detail += $"\n<color=#66CCAA>Prerequisites bypassed (Monk bonus feat)</color>";
        }
        else if (!meetsPrereqs && unmet.Count > 0)
        {
            detail += $"\n<color=#FF6666>Missing: {string.Join(", ", unmet)}</color>";
        }

        detail += $"\n\n<color=#888888>{feat.Description}</color>";

        _featDetailText.text = detail;
    }

    private void OnSelectClicked(string featName)
    {
        if (_selectedFeats.Contains(featName))
        {
            _selectedFeats.Remove(featName);
            Debug.Log($"[FeatSelectionUI] Deselected: {featName}");
        }
        else
        {
            if (_selectedFeats.Count >= _featsToSelect)
            {
                Debug.Log($"[FeatSelectionUI] Already selected {_featsToSelect} feats. Remove one first.");
                return;
            }
            _selectedFeats.Add(featName);
            Debug.Log($"[FeatSelectionUI] Selected: {featName}");
        }

        UpdateSelectedDisplay();
        RefreshFeatList();
    }

    private void OnFilterClicked(string filter)
    {
        _filterType = filter;

        // Update button colors
        for (int i = 0; i < _filterButtons.Count; i++)
        {
            var btnImg = _filterButtons[i].GetComponent<Image>();
            string[] filters = { "All", "Combat", "Ranged", "Defensive", "TWF", "Skill", "Metamagic", "Crafting", "General" };
            btnImg.color = (i < filters.Length && filters[i] == filter) ? COLOR_FILTER_ACTIVE : COLOR_FILTER_INACTIVE;
        }

        PopulateFeatList();
    }

    private void OnSearchChanged(string text)
    {
        _searchText = text;
        PopulateFeatList();
    }

    private void OnConfirmClicked()
    {
        if (_selectedFeats.Count < _featsToSelect)
        {
            Debug.LogWarning($"[FeatSelectionUI] Need to select {_featsToSelect} feats, only {_selectedFeats.Count} selected.");
            return;
        }

        Debug.Log($"[FeatSelectionUI] Confirmed feats: {string.Join(", ", _selectedFeats)}");

        // IMPORTANT: Close BEFORE invoking the callback.
        // The callback (e.g. OnGeneralFeatsSelected for Fighters) may immediately
        // re-open this panel for bonus feat selection. If we Close() after the
        // callback, we'd hide the panel that was just re-opened, leaving a blank screen.
        var confirmedFeats = new List<string>(_selectedFeats);
        var callback = OnFeatsConfirmed;
        Close();
        callback?.Invoke(confirmedFeats);
    }

    // ========================================================================
    // DISPLAY UPDATES
    // ========================================================================

    private void UpdateSelectedDisplay()
    {
        int remaining = _featsToSelect - _selectedFeats.Count;
        _featsRemainingText.text = remaining > 0
            ? $"Feats to select: {remaining} of {_featsToSelect}"
            : $"All {_featsToSelect} feats selected!";
        _featsRemainingText.color = remaining > 0 ? Color.yellow : Color.green;

        if (_selectedFeats.Count == 0)
            _selectedFeatsText.text = "(none selected)";
        else
        {
            string sel = "";
            for (int i = 0; i < _selectedFeats.Count; i++)
            {
                sel += $"• {_selectedFeats[i]}\n";
            }
            _selectedFeatsText.text = sel;
        }

        _confirmButton.interactable = (_selectedFeats.Count == _featsToSelect);
        var btnImg = _confirmButton.GetComponent<Image>();
        btnImg.color = _confirmButton.interactable ? COLOR_BUTTON : COLOR_BUTTON_DISABLED;
    }

    private void RefreshFeatList()
    {
        PopulateFeatList();
    }

    // ========================================================================
    // HELPERS
    // ========================================================================

    private void SetPanelVisible(bool visible)
    {
        if (_overlay == null) return;
        _overlay.SetActive(visible);
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }
    }

    private string GetTypeShortString(FeatType type)
    {
        switch (type)
        {
            case FeatType.Combat: return "Combat";
            case FeatType.Ranged: return "Ranged";
            case FeatType.Defensive: return "Defense";
            case FeatType.TwoWeaponFighting: return "TWF";
            case FeatType.MountedCombat: return "Mount";
            case FeatType.Skill: return "Skill";
            case FeatType.Unarmed: return "Unarmed";
            case FeatType.Metamagic: return "Metamagic";
            case FeatType.ItemCreation: return "Crafting";
            case FeatType.General: return "General";
            default: return "Other";
        }
    }

    private Color GetTypeColor(FeatType type)
    {
        switch (type)
        {
            case FeatType.Combat: return new Color(1f, 0.6f, 0.4f);
            case FeatType.Ranged: return new Color(0.4f, 0.8f, 1f);
            case FeatType.Defensive: return new Color(0.6f, 0.8f, 0.4f);
            case FeatType.TwoWeaponFighting: return new Color(0.9f, 0.7f, 0.3f);
            case FeatType.MountedCombat: return new Color(0.7f, 0.5f, 0.3f);
            case FeatType.Skill: return new Color(0.7f, 0.7f, 1f);
            case FeatType.Unarmed: return new Color(0.8f, 0.5f, 0.8f);
            case FeatType.Metamagic: return new Color(0.6f, 0.4f, 1f);
            case FeatType.ItemCreation: return new Color(0.4f, 0.8f, 0.6f);
            case FeatType.General: return new Color(0.7f, 0.7f, 0.7f);
            default: return Color.gray;
        }
    }

    // ========================================================================
    // UI CREATION HELPERS (match existing project patterns)
    // ========================================================================

    private Text MakeText(Transform parent, string name, Vector2 pos, Vector2 size,
        string text, int fontSize, Color color, TextAnchor align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var t = go.AddComponent<Text>();
        t.text = text;
        t.font = _font;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = align;
        t.supportRichText = true;
        return t;
    }

    private Button MakeButton(Transform parent, string name, Vector2 pos, Vector2 size,
        string label, Color bgColor, Color textColor, int fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = bgColor;
        var btn = go.AddComponent<Button>();

        var cb = new ColorBlock();
        cb.normalColor = bgColor;
        cb.highlightedColor = bgColor * 1.2f;
        cb.pressedColor = bgColor * 0.8f;
        cb.disabledColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.1f;
        btn.colors = cb;

        var txt = MakeText(go.transform, "Label", Vector2.zero, size, label, fontSize, textColor, TextAnchor.MiddleCenter);
        var txtRT = txt.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.sizeDelta = Vector2.zero;
        txtRT.anchoredPosition = Vector2.zero;

        return btn;
    }

    private GameObject CreatePanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        if (color.a > 0)
        {
            var img = go.AddComponent<Image>();
            img.color = color;
        }
        return go;
    }

    // ========================================================================
    // NESTED DATA CLASS
    // ========================================================================

    private class FeatRowUI
    {
        public FeatDefinition Feat;
        public GameObject RowGO;
        public Image BgImage;
        public Button SelectButton;
    }
}
