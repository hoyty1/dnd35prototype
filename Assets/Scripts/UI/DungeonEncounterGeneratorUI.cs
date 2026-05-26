using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen UI for the DMG 3.5e dungeon encounter table system (Phase 4).
///
/// Provides:
///   - Dungeon level selector (1-8) with button row
///   - "Generate Random Encounter" button that rolls d% on the selected table
///   - Encounter preview panel showing roll result, cascade info, creatures, EL
///   - "Re-roll" button to generate a different encounter on the same table
///   - "Start Combat" button that feeds the encounter into the Phase 2 spawner
///   - "Back" button to return to the caller
///
/// Integrates with:
///   - DungeonEncounterTableManager (Phase 3) for table rolling and cascade logic
///   - GameManager.StartDungeonEncounter() (Phase 2) for combat initiation
///   - EncounterDefinition / DungeonEncounterSpawner (Phase 2) for creature prep
///
/// Usage:
///   // From GameManager or EncounterSelectionUI:
///   var ui = gameObject.AddComponent&lt;DungeonEncounterGeneratorUI&gt;();
///   ui.Open(partyLevel, onStartCombat, onBack);
///
/// Unity Setup (programmatic — no manual Inspector wiring needed):
///   This UI builds itself entirely in code via EnsureBuilt(). Just attach the
///   component to any GameObject that lives under a Canvas. The UI creates its
///   own overlay Canvas for proper layering.
///
/// Phase 4: UI Integration for DMG Encounter Tables.
/// </summary>
public class DungeonEncounterGeneratorUI : MonoBehaviour
{
    // =========================================================================
    //  Constants
    // =========================================================================

    private const int DefaultFontSize = 22;
    private const float SectionSpacing = 18f;
    private const float SectionPadding = 12f;

    private static readonly Color BgColor = new Color(0.04f, 0.05f, 0.08f, 0.97f);
    private static readonly Color SectionColor = new Color(0.1f, 0.13f, 0.2f, 0.98f);
    private static readonly Color PreviewSectionColor = new Color(0.07f, 0.09f, 0.16f, 0.98f);
    private static readonly Color TitleColor = new Color(0.95f, 0.86f, 0.45f, 1f);
    private static readonly Color BodyTextColor = new Color(0.9f, 0.94f, 1f, 1f);
    private static readonly Color DividerColor = new Color(0.28f, 0.34f, 0.5f, 0.95f);

    private static readonly Color ButtonGenerate = new Color(0.18f, 0.52f, 0.31f, 1f);
    private static readonly Color ButtonReroll = new Color(0.24f, 0.4f, 0.68f, 1f);
    private static readonly Color ButtonStartCombat = new Color(0.2f, 0.48f, 0.27f, 1f);
    private static readonly Color ButtonBack = new Color(0.5f, 0.22f, 0.22f, 1f);
    private static readonly Color LevelSelected = new Color(0.3f, 0.48f, 0.72f, 1f);
    private static readonly Color LevelUnselected = new Color(0.17f, 0.22f, 0.35f, 1f);

    // =========================================================================
    //  State
    // =========================================================================

    private GameObject _root;
    private ScrollRect _mainScrollRect;
    private RectTransform _mainViewportRect;
    private RectTransform _mainContentRect;

    // Level selector
    private readonly Button[] _levelButtons = new Button[8];
    private readonly Text[] _levelLabels = new Text[8];
    private int _selectedLevel = 1;

    // Party info
    private int _partyLevel = 3;
    private Text _partyInfoText;

    // Generate section
    private Button _generateButton;

    // Preview
    private Text _previewText;
    private LayoutElement _previewSectionLayout;
    private RectTransform _previewSectionRect;

    // Actions
    private Button _rerollButton;
    private Button _startCombatButton;

    // Last generated encounter
    private EncounterDefinition _lastEncounter;
    private int _lastRollResult;
    private int _lastTableUsed;
    private int _cascadeCount;

    // Callbacks
    private Action<EncounterDefinition> _onStartCombat;
    private Action _onBack;

    /// <summary>Whether the UI is currently visible.</summary>
    public bool IsOpen => _root != null && _root.activeSelf;

    // =========================================================================
    //  Public API
    // =========================================================================

    /// <summary>
    /// Open the dungeon encounter generator UI.
    /// </summary>
    /// <param name="partyLevel">Average party level (for display and future EL adjustment).</param>
    /// <param name="onStartCombat">Called when user clicks "Start Combat" with the generated EncounterDefinition.</param>
    /// <param name="onBack">Called when user clicks "Back" to return to the previous screen.</param>
    /// <param name="defaultDungeonLevel">Initial dungeon level selection (1-8).</param>
    public void Open(
        int partyLevel,
        Action<EncounterDefinition> onStartCombat,
        Action onBack,
        int defaultDungeonLevel = 0)
    {
        EnsureBuilt();
        if (_root == null)
        {
            Debug.LogError("[DungeonEncounterGeneratorUI] Failed to build UI.");
            return;
        }

        _partyLevel = Mathf.Max(1, partyLevel);
        _onStartCombat = onStartCombat;
        _onBack = onBack;
        _lastEncounter = null;
        _lastRollResult = 0;
        _lastTableUsed = 0;
        _cascadeCount = 0;

        // Auto-select dungeon level matching party level (clamped to 1-8)
        if (defaultDungeonLevel > 0)
            _selectedLevel = Mathf.Clamp(defaultDungeonLevel, 1, 8);
        else
            _selectedLevel = Mathf.Clamp(_partyLevel, 1, 8);

        // Ensure tables are loaded
        if (!DungeonEncounterTableManager.IsLoaded)
        {
            Debug.Log("[DungeonEncounterGeneratorUI] Loading encounter tables on first use...");
            DungeonEncounterTableManager.LoadTables();
        }

        RefreshPartyInfo();
        RefreshLevelButtons();
        SetPreviewPlaceholder();
        UpdateActionButtonStates();

        _root.transform.SetAsLastSibling();
        _root.SetActive(true);

        if (_mainScrollRect != null)
            _mainScrollRect.verticalNormalizedPosition = 1f;

        Debug.Log($"[DungeonEncounterGeneratorUI] Opened | partyLevel={_partyLevel} | selectedLevel={_selectedLevel}");
    }

    /// <summary>Close the UI.</summary>
    public void Close()
    {
        if (_root != null)
            _root.SetActive(false);
    }

    // =========================================================================
    //  UI Construction
    // =========================================================================

    private void EnsureBuilt()
    {
        if (_root != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[DungeonEncounterGeneratorUI] No Canvas found in scene.");
            return;
        }

        // Root overlay
        _root = new GameObject("DungeonEncounterGeneratorScreen",
            typeof(RectTransform), typeof(Image), typeof(Canvas), typeof(GraphicRaycaster));
        _root.transform.SetParent(canvas.transform, false);

        Canvas overlayCanvas = _root.GetComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = canvas.sortingOrder + 25;

        RectTransform rootRect = _root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        _root.GetComponent<Image>().color = BgColor;

        // Scrollable body
        BuildScrollableBody();

        // Sections inside content
        CreateHeaderSection(_mainContentRect.transform);
        CreatePartyInfoSection(_mainContentRect.transform);
        CreateLevelSelectorSection(_mainContentRect.transform);
        CreateGenerateSection(_mainContentRect.transform);
        CreatePreviewSection(_mainContentRect.transform);
        CreateActionButtonsSection(_mainContentRect.transform);

        _root.SetActive(false);
    }

    private void BuildScrollableBody()
    {
        // Scroll root
        GameObject scrollRoot = new GameObject("ScrollRoot",
            typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollRoot.transform.SetParent(_root.transform, false);

        RectTransform scrollRootRect = scrollRoot.GetComponent<RectTransform>();
        scrollRootRect.anchorMin = new Vector2(0.05f, 0.05f);
        scrollRootRect.anchorMax = new Vector2(0.95f, 0.95f);
        scrollRootRect.offsetMin = Vector2.zero;
        scrollRootRect.offsetMax = Vector2.zero;
        scrollRoot.GetComponent<Image>().color = new Color(0.03f, 0.04f, 0.07f, 0.7f);

        _mainScrollRect = scrollRoot.GetComponent<ScrollRect>();
        _mainScrollRect.horizontal = false;
        _mainScrollRect.vertical = true;
        _mainScrollRect.movementType = ScrollRect.MovementType.Clamped;
        _mainScrollRect.scrollSensitivity = 36f;

        // Viewport
        GameObject viewport = new GameObject("Viewport",
            typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollRoot.transform, false);

        _mainViewportRect = viewport.GetComponent<RectTransform>();
        _mainViewportRect.anchorMin = Vector2.zero;
        _mainViewportRect.anchorMax = Vector2.one;
        _mainViewportRect.offsetMin = new Vector2(8f, 8f);
        _mainViewportRect.offsetMax = new Vector2(-26f, -8f);

        viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.06f);
        viewport.GetComponent<Image>().raycastTarget = true;
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        // Content
        GameObject content = new GameObject("Content",
            typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);

        _mainContentRect = content.GetComponent<RectTransform>();
        _mainContentRect.anchorMin = new Vector2(0f, 1f);
        _mainContentRect.anchorMax = new Vector2(1f, 1f);
        _mainContentRect.pivot = new Vector2(0.5f, 1f);
        _mainContentRect.anchoredPosition = Vector2.zero;
        _mainContentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup rootLayout = content.GetComponent<VerticalLayoutGroup>();
        rootLayout.spacing = SectionSpacing;
        rootLayout.padding = new RectOffset(20, 20, 18, 20);
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Scrollbar
        BuildVerticalScrollbar(scrollRoot.transform, out Scrollbar scrollbar);
        _mainScrollRect.viewport = _mainViewportRect;
        _mainScrollRect.content = _mainContentRect;
        _mainScrollRect.verticalScrollbar = scrollbar;
        _mainScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
    }

    private void BuildVerticalScrollbar(Transform parent, out Scrollbar scrollbar)
    {
        GameObject scrollbarObj = new GameObject("VerticalScrollbar",
            typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        scrollbarObj.transform.SetParent(parent, false);

        RectTransform scrollbarRect = scrollbarObj.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 1f);
        scrollbarRect.offsetMin = new Vector2(-16f, 8f);
        scrollbarRect.offsetMax = new Vector2(-4f, -8f);
        scrollbarObj.GetComponent<Image>().color = new Color(0.16f, 0.2f, 0.29f, 0.95f);

        GameObject slidingArea = new GameObject("SlidingArea", typeof(RectTransform));
        slidingArea.transform.SetParent(scrollbarObj.transform, false);
        RectTransform slidingRect = slidingArea.GetComponent<RectTransform>();
        slidingRect.anchorMin = Vector2.zero;
        slidingRect.anchorMax = Vector2.one;
        slidingRect.offsetMin = new Vector2(0f, 6f);
        slidingRect.offsetMax = new Vector2(0f, -6f);

        GameObject handleObj = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObj.transform.SetParent(slidingArea.transform, false);
        RectTransform handleRect = handleObj.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 1f);
        handleRect.anchorMax = new Vector2(1f, 1f);
        handleRect.pivot = new Vector2(0.5f, 1f);
        handleRect.sizeDelta = new Vector2(0f, 96f);
        handleObj.GetComponent<Image>().color = new Color(0.52f, 0.67f, 0.96f, 0.95f);

        scrollbar = scrollbarObj.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = handleObj.GetComponent<Image>();
        scrollbar.handleRect = handleRect;
    }

    // =========================================================================
    //  Section Builders
    // =========================================================================

    private void CreateHeaderSection(Transform parent)
    {
        GameObject section = CreateSectionPanel(parent, "Header", new Color(0.09f, 0.11f, 0.17f, 1f), 78f);
        CreateSectionTitle(section.transform, "DMG DUNGEON ENCOUNTER TABLES", 34, TextAnchor.MiddleCenter, Color.white, false);
    }

    private void CreatePartyInfoSection(Transform parent)
    {
        GameObject section = CreateSectionPanel(parent, "PartyInfoSection", SectionColor, 80f);
        CreateSectionTitle(section.transform, "1) PARTY INFO", 22, TextAnchor.UpperLeft, TitleColor);

        _partyInfoText = CreateBodyText(section.transform);
        RectTransform textRect = _partyInfoText.rectTransform;
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(20f, 10f);
        textRect.offsetMax = new Vector2(-20f, -44f);
    }

    private void CreateLevelSelectorSection(Transform parent)
    {
        GameObject section = CreateSectionPanel(parent, "LevelSelectorSection",
            new Color(0.11f, 0.13f, 0.22f, 0.98f), 138f);
        CreateSectionTitle(section.transform, "2) DUNGEON LEVEL (1-8)", 22, TextAnchor.UpperLeft, TitleColor);

        // Button row
        GameObject row = new GameObject("LevelRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(section.transform, false);

        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 0f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.offsetMin = new Vector2(16f, 12f);
        rowRect.offsetMax = new Vector2(-16f, -48f);

        HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 6f;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = true;

        for (int i = 0; i < 8; i++)
        {
            int level = i + 1; // Capture for closure
            CreateLevelButton(row.transform, level, out _levelButtons[i], out _levelLabels[i]);
            _levelButtons[i].onClick.AddListener(() => OnLevelSelected(level));
        }
    }

    private void CreateGenerateSection(Transform parent)
    {
        GameObject section = CreateSectionPanel(parent, "GenerateSection", SectionColor, 88f);
        CreateSectionTitle(section.transform, "3) GENERATE", 22, TextAnchor.UpperLeft, TitleColor);

        CreateLargeButton(
            section.transform,
            "🎲 ROLL ENCOUNTER (d%)",
            ButtonGenerate,
            OnGeneratePressed,
            out _generateButton,
            out _,
            24,
            new Vector2(420f, 50f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 12f));
    }

    private void CreatePreviewSection(Transform parent)
    {
        GameObject section = CreateSectionPanel(parent, "PreviewSection", PreviewSectionColor, 260f);
        _previewSectionRect = section.GetComponent<RectTransform>();
        _previewSectionLayout = section.GetComponent<LayoutElement>();
        CreateSectionTitle(section.transform, "4) ENCOUNTER PREVIEW", 22, TextAnchor.UpperLeft, TitleColor);

        GameObject previewContainer = new GameObject("PreviewContainer",
            typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        previewContainer.transform.SetParent(section.transform, false);

        RectTransform containerRect = previewContainer.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0f, 0f);
        containerRect.anchorMax = new Vector2(1f, 1f);
        containerRect.offsetMin = new Vector2(18f, 14f);
        containerRect.offsetMax = new Vector2(-18f, -50f);
        previewContainer.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);
        previewContainer.GetComponent<LayoutElement>().minHeight = 120f;

        _previewText = CreateBodyText(previewContainer.transform);
        RectTransform previewRect = _previewText.rectTransform;
        previewRect.anchorMin = new Vector2(0f, 1f);
        previewRect.anchorMax = new Vector2(1f, 1f);
        previewRect.pivot = new Vector2(0.5f, 1f);
        previewRect.anchoredPosition = new Vector2(0f, -6f);
        previewRect.sizeDelta = new Vector2(-14f, 0f);

        ContentSizeFitter previewFitter = _previewText.gameObject.AddComponent<ContentSizeFitter>();
        previewFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        previewFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _previewText.resizeTextForBestFit = false;
        _previewText.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private void CreateActionButtonsSection(Transform parent)
    {
        GameObject section = CreateSectionPanel(parent, "ActionButtonsSection", SectionColor, 122f);
        CreateSectionTitle(section.transform, "5) ACTIONS", 22, TextAnchor.UpperLeft, TitleColor);

        GameObject row = new GameObject("ActionsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(section.transform, false);

        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 0f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.offsetMin = new Vector2(16f, 12f);
        rowRect.offsetMax = new Vector2(-16f, -48f);

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        CreateLargeButton(row.transform, "🎲 Re-roll", ButtonReroll, OnRerollPressed,
            out _rerollButton, out _, 18);
        CreateLargeButton(row.transform, "⚔ Start Combat", ButtonStartCombat, OnStartCombatPressed,
            out _startCombatButton, out _, 18);
        CreateLargeButton(row.transform, "← Back", ButtonBack, OnBackPressed,
            out _, out _, 18);
    }

    // =========================================================================
    //  Event Handlers
    // =========================================================================

    private void OnLevelSelected(int level)
    {
        _selectedLevel = Mathf.Clamp(level, 1, 8);
        RefreshLevelButtons();
        Debug.Log($"[DungeonEncounterGeneratorUI] Level {_selectedLevel} selected.");
    }

    private void OnGeneratePressed()
    {
        GenerateEncounter();
    }

    private void OnRerollPressed()
    {
        GenerateEncounter();
    }

    private void OnStartCombatPressed()
    {
        if (_lastEncounter == null)
        {
            Debug.LogWarning("[DungeonEncounterGeneratorUI] No encounter to start.");
            return;
        }

        Debug.Log($"[DungeonEncounterGeneratorUI] Starting combat: {_lastEncounter.GetPreview()}");

        EncounterDefinition encounter = _lastEncounter;
        Close();
        _onStartCombat?.Invoke(encounter);
    }

    private void OnBackPressed()
    {
        Close();
        _onBack?.Invoke();
    }

    // =========================================================================
    //  Core Logic
    // =========================================================================

    private void GenerateEncounter()
    {
        if (!DungeonEncounterTableManager.IsLoaded)
        {
            Debug.Log("[DungeonEncounterGeneratorUI] Loading tables...");
            DungeonEncounterTableManager.LoadTables();
        }

        Debug.Log($"[DungeonEncounterGeneratorUI] Generating encounter | level={_selectedLevel} | partyLevel={_partyLevel}");

        // Roll on the selected table
        _lastEncounter = DungeonEncounterTableManager.GenerateRandomEncounter(_selectedLevel, _partyLevel);

        if (_lastEncounter == null)
        {
            UpdatePreviewText("❌ Failed to generate encounter.\n\nThe encounter table returned no valid result. Check Debug.Log for details.");
            UpdateActionButtonStates();
            return;
        }

        // Build the preview display
        string preview = BuildEncounterPreview(_lastEncounter);
        UpdatePreviewText(preview);
        UpdateActionButtonStates();

        Debug.Log($"[DungeonEncounterGeneratorUI] Generated: {_lastEncounter.GetPreview()}");
    }

    // =========================================================================
    //  Preview Rendering
    // =========================================================================

    private string BuildEncounterPreview(EncounterDefinition encounter)
    {
        if (encounter == null)
            return "No encounter generated.";

        StringBuilder sb = new StringBuilder();

        // Title
        sb.AppendLine($"<b>⚔ {encounter.Name ?? "Dungeon Encounter"}</b>");
        sb.AppendLine();

        // Table info
        sb.AppendLine($"<b>Dungeon Level:</b> {_selectedLevel}");
        if (encounter.TargetEL > 0)
            sb.AppendLine($"<b>Encounter Level (EL):</b> {encounter.TargetEL}");
        sb.AppendLine($"<b>Environment:</b> {encounter.Environment ?? "Underground"}");
        sb.AppendLine();

        // Creatures
        sb.AppendLine("<b>Creatures:</b>");
        if (encounter.Entries == null || encounter.Entries.Count == 0)
        {
            sb.AppendLine("  • (No creatures)");
        }
        else
        {
            int totalCount = 0;
            for (int i = 0; i < encounter.Entries.Count; i++)
            {
                EncounterCreatureEntry entry = encounter.Entries[i];
                if (entry == null) continue;

                int count = Mathf.Max(1, entry.Count);
                totalCount += count;
                string name = entry.DisplayName;
                sb.AppendLine($"  • {name}");
            }
            sb.AppendLine();
            sb.AppendLine($"<b>Total Creatures:</b> {totalCount}");
        }

        // Party comparison
        sb.AppendLine();
        sb.AppendLine($"<b>Party Level:</b> {_partyLevel}");
        if (encounter.TargetEL > 0)
        {
            int diff = encounter.TargetEL - _partyLevel;
            string difficulty;
            if (diff <= -3)
                difficulty = "💤 Trivial";
            else if (diff <= -1)
                difficulty = "🟢 Easy";
            else if (diff == 0)
                difficulty = "🟡 Average";
            else if (diff <= 2)
                difficulty = "🟠 Challenging";
            else if (diff <= 4)
                difficulty = "🔴 Hard";
            else
                difficulty = "💀 Deadly";

            sb.AppendLine($"<b>Estimated Difficulty:</b> {difficulty} (EL {encounter.TargetEL} vs Party {_partyLevel})");
        }

        return sb.ToString().TrimEnd();
    }

    // =========================================================================
    //  UI Refresh
    // =========================================================================

    private void RefreshPartyInfo()
    {
        if (_partyInfoText == null) return;
        _partyInfoText.text = $"Party Level: <b>{_partyLevel}</b>\nRecommended Dungeon Level: {Mathf.Clamp(_partyLevel, 1, 8)}";
    }

    private void RefreshLevelButtons()
    {
        for (int i = 0; i < 8; i++)
        {
            int level = i + 1;
            bool selected = level == _selectedLevel;

            if (_levelButtons[i] != null)
            {
                Image img = _levelButtons[i].GetComponent<Image>();
                if (img != null)
                {
                    img.color = selected ? LevelSelected : LevelUnselected;
                    ConfigureButtonColors(_levelButtons[i], img.color);
                }
            }

            if (_levelLabels[i] != null)
            {
                _levelLabels[i].text = $"Level {level}\nEL {level}";
                _levelLabels[i].fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
            }
        }
    }

    private void SetPreviewPlaceholder()
    {
        UpdatePreviewText(
            "No encounter generated yet.\n\n" +
            "Select a dungeon level (1-8) and click <b>ROLL ENCOUNTER</b> to generate\n" +
            "a random encounter from the DMG 3.5e encounter tables.\n\n" +
            "• Rolls 01-10 cascade to an easier table\n" +
            "• Rolls 91-100 cascade to a harder table\n" +
            "• Each table contains ~20 different encounter possibilities");
    }

    private void UpdatePreviewText(string text)
    {
        if (_previewText == null) return;

        if (_previewText.font == null)
            _previewText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        _previewText.enabled = true;
        if (_previewText.gameObject != null)
            _previewText.gameObject.SetActive(true);

        Color c = _previewText.color;
        if (c.a < 0.9f)
            _previewText.color = new Color(c.r, c.g, c.b, 1f);

        _previewText.text = text ?? string.Empty;
        AdjustPreviewSectionHeight();
    }

    private void UpdateActionButtonStates()
    {
        bool hasEncounter = _lastEncounter != null;
        if (_rerollButton != null) _rerollButton.interactable = hasEncounter;
        if (_startCombatButton != null) _startCombatButton.interactable = hasEncounter;
    }

    private void AdjustPreviewSectionHeight()
    {
        if (_previewSectionLayout == null || _previewText == null) return;

        Canvas.ForceUpdateCanvases();

        float preferredHeight = LayoutUtility.GetPreferredHeight(_previewText.rectTransform);
        if (preferredHeight <= 0f)
        {
            float availableWidth = Mathf.Max(320f, _previewText.rectTransform.rect.width);
            TextGenerationSettings settings = _previewText.GetGenerationSettings(new Vector2(availableWidth, 0f));
            preferredHeight = _previewText.cachedTextGeneratorForLayout.GetPreferredHeight(
                _previewText.text ?? string.Empty, settings) / _previewText.pixelsPerUnit;
        }

        float sectionHeight = Mathf.Max(260f, preferredHeight + 96f);
        _previewSectionLayout.minHeight = sectionHeight;
        _previewSectionLayout.preferredHeight = sectionHeight;
        _previewSectionLayout.flexibleHeight = 0f;

        LayoutRebuilder.ForceRebuildLayoutImmediate(_previewText.rectTransform);
        RefreshMainLayout();
    }

    private void RefreshMainLayout()
    {
        if (_mainContentRect == null) return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_mainContentRect);

        if (_mainScrollRect != null)
        {
            _mainScrollRect.Rebuild(CanvasUpdate.PostLayout);
            _mainScrollRect.enabled = true;
        }
    }

    // =========================================================================
    //  UI Element Builders (matching existing code style)
    // =========================================================================

    private GameObject CreateSectionPanel(Transform parent, string name, Color color, float preferredHeight)
    {
        GameObject section = new GameObject(name,
            typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        section.transform.SetParent(parent, false);

        section.GetComponent<Image>().color = color;

        Outline outline = section.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.4f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        LayoutElement layout = section.GetComponent<LayoutElement>();
        layout.preferredHeight = preferredHeight;
        layout.minHeight = preferredHeight;
        layout.flexibleHeight = 0f;

        return section;
    }

    private void CreateSectionTitle(Transform parent, string title, int fontSize,
        TextAnchor anchor, Color color, bool includeBottomDivider = true)
    {
        Text titleText = CreateTextElement(parent, title, fontSize, FontStyle.Bold, color, anchor);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -SectionPadding);
        titleRect.sizeDelta = new Vector2(-(SectionPadding * 2f), 32f);

        if (!includeBottomDivider) return;

        GameObject divider = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        divider.transform.SetParent(parent, false);
        RectTransform dividerRect = divider.GetComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(0f, 1f);
        dividerRect.anchorMax = new Vector2(1f, 1f);
        dividerRect.pivot = new Vector2(0.5f, 1f);
        dividerRect.anchoredPosition = new Vector2(0f, -42f);
        dividerRect.sizeDelta = new Vector2(-26f, 2f);
        divider.GetComponent<Image>().color = DividerColor;
    }

    private Text CreateBodyText(Transform parent)
    {
        Text body = CreateTextElement(parent, string.Empty, 20, FontStyle.Normal, BodyTextColor, TextAnchor.UpperLeft);
        body.supportRichText = true;
        body.horizontalOverflow = HorizontalWrapMode.Wrap;
        body.verticalOverflow = VerticalWrapMode.Truncate;
        return body;
    }

    private Text CreateTextElement(Transform parent, string value, int fontSize,
        FontStyle style, Color color, TextAnchor alignment)
    {
        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObj.transform.SetParent(parent, false);

        Text text = textObj.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontStyle = style;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.supportRichText = true;

        return text;
    }

    private void CreateLevelButton(Transform parent, int level, out Button button, out Text label)
    {
        GameObject buttonObj = new GameObject($"Level{level}Button",
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObj.transform.SetParent(parent, false);

        buttonObj.GetComponent<LayoutElement>().minWidth = 60f;

        Image image = buttonObj.GetComponent<Image>();
        image.color = LevelUnselected;

        button = buttonObj.GetComponent<Button>();
        ConfigureButtonColors(button, image.color);

        label = CreateTextElement(buttonObj.transform, $"Level {level}\nEL {level}",
            16, FontStyle.Normal, Color.white, TextAnchor.MiddleCenter);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(4f, 4f);
        labelRect.offsetMax = new Vector2(-4f, -4f);
    }

    private void CreateLargeButton(
        Transform parent,
        string label,
        Color baseColor,
        Action onClick,
        out Button button,
        out Text text,
        int fontSize = 18,
        Vector2? size = null,
        Vector2? anchorMin = null,
        Vector2? anchorMax = null,
        Vector2? pivot = null,
        Vector2? anchoredPos = null)
    {
        GameObject buttonObj = new GameObject($"Button_{label}",
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObj.transform.SetParent(parent, false);

        Image image = buttonObj.GetComponent<Image>();
        image.color = baseColor;

        button = buttonObj.GetComponent<Button>();
        ConfigureButtonColors(button, baseColor);
        button.onClick.AddListener(() => onClick?.Invoke());

        if (size.HasValue || anchorMin.HasValue)
        {
            RectTransform rt = buttonObj.GetComponent<RectTransform>();
            if (anchorMin.HasValue) rt.anchorMin = anchorMin.Value;
            if (anchorMax.HasValue) rt.anchorMax = anchorMax.Value;
            if (pivot.HasValue) rt.pivot = pivot.Value;
            if (anchoredPos.HasValue) rt.anchoredPosition = anchoredPos.Value;
            if (size.HasValue) rt.sizeDelta = size.Value;
        }

        LayoutElement le = buttonObj.GetComponent<LayoutElement>();
        le.minHeight = size.HasValue ? size.Value.y : 42f;
        le.preferredHeight = le.minHeight;

        text = CreateTextElement(buttonObj.transform, label, fontSize, FontStyle.Bold,
            Color.white, TextAnchor.MiddleCenter);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 4f);
        textRect.offsetMax = new Vector2(-8f, -4f);
    }

    private static void ConfigureButtonColors(Button button, Color baseColor)
    {
        if (button == null) return;

        ColorBlock colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.2f);
        colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.2f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.22f, 0.22f, 0.22f, 0.9f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }
}
