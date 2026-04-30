using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dedicated full-screen random encounter generator UI.
/// </summary>
public class RandomEncounterGeneratorUI : MonoBehaviour
{
    private const int DefaultFontSize = 22;
    private const float LayoutSectionSpacing = 18f;
    private const float LayoutSectionPadding = 12f;
    private const float PreviewSectionMinHeight = 200f;
    private const float PreviewSectionVerticalChrome = 96f;
    private const float PreviewSectionBottomMargin = 12f;
    private const float ActionsSectionMinHeight = 122f;

    private sealed class OptionSelector
    {
        public Button Button;
        public Text ValueLabel;
        public List<string> Options = new List<string>();
        public int Index;
    }

    private GameObject _root;
    private ScrollRect _mainScrollRect;
    private RectTransform _mainViewportRect;
    private RectTransform _mainContentRect;
    private Text _partyInfoText;
    private Text _previewText;
    private VerticalLayoutGroup _contentLayoutGroup;
    private LayoutElement _previewSectionLayout;
    private LayoutElement _actionsSectionLayout;
    private RectTransform _partySectionRect;
    private RectTransform _difficultySectionRect;
    private RectTransform _filtersSectionRect;
    private RectTransform _generateSectionRect;
    private RectTransform _previewSectionRect;
    private RectTransform _actionsSectionRect;
    private Text _filtersToggleLabel;
    private GameObject _filtersContent;

    private OptionSelector _environmentSelector;
    private OptionSelector _creatureTypeSelector;
    private InputField _minCreaturesInput;
    private InputField _maxCreaturesInput;

    private Button _generateButton;
    private Button _generateAgainButton;
    private Button _startEncounterButton;

    private readonly Dictionary<RandomEncounterDifficulty, Button> _difficultyButtons = new Dictionary<RandomEncounterDifficulty, Button>();
    private readonly Dictionary<RandomEncounterDifficulty, Text> _difficultyButtonLabels = new Dictionary<RandomEncounterDifficulty, Text>();

    private List<int> _partyLevels = new List<int>();
    private int _partySize = 4;
    private int _apl = 3;
    private bool _filtersExpanded;

    private Action<List<string>, GeneratedRandomEncounter> _onStartEncounter;
    private Action _onBack;

    private GeneratedRandomEncounter _lastGeneratedEncounter;
    private RandomEncounterDifficulty _selectedDifficulty = RandomEncounterDifficulty.Challenging;

    private static readonly RandomEncounterDifficulty[] DifficultyOrder =
    {
        RandomEncounterDifficulty.Easy,
        RandomEncounterDifficulty.Average,
        RandomEncounterDifficulty.Challenging,
        RandomEncounterDifficulty.Hard,
        RandomEncounterDifficulty.Epic
    };

    private static readonly Dictionary<RandomEncounterDifficulty, Color> DifficultyColors = new Dictionary<RandomEncounterDifficulty, Color>
    {
        { RandomEncounterDifficulty.Easy, new Color(0.18f, 0.55f, 0.28f, 1f) },
        { RandomEncounterDifficulty.Average, new Color(0.2f, 0.45f, 0.7f, 1f) },
        { RandomEncounterDifficulty.Challenging, new Color(0.62f, 0.46f, 0.17f, 1f) },
        { RandomEncounterDifficulty.Hard, new Color(0.7f, 0.35f, 0.16f, 1f) },
        { RandomEncounterDifficulty.Epic, new Color(0.58f, 0.2f, 0.52f, 1f) }
    };

    public bool IsOpen => _root != null && _root.activeSelf;

    public void Open(
        List<int> partyLevels,
        int partySize,
        Action<List<string>, GeneratedRandomEncounter> onStartEncounter,
        Action onBack)
    {
        EnsureBuilt();
        if (_root == null)
            return;

        _partyLevels = (partyLevels != null && partyLevels.Count > 0)
            ? new List<int>(partyLevels)
            : new List<int> { 3, 3, 3, 3 };
        _partySize = Mathf.Max(1, partySize);
        _apl = ChallengeRatingUtils.CalculateAPL(_partyLevels, _partySize);

        _onStartEncounter = onStartEncounter;
        _onBack = onBack;
        _lastGeneratedEncounter = null;

        ResetFilterInputs();
        _root.transform.SetAsLastSibling();
        _root.SetActive(true);
        RefreshPartyInfo();
        RefreshDifficultyButtons();
        RefreshFiltersToggleLabel();
        SetPreviewPlaceholder();
        UpdateActionButtonState();
        RefreshMainLayout();

        if (_mainScrollRect != null)
            _mainScrollRect.verticalNormalizedPosition = 1f;
    }

    public void Close()
    {
        if (_root != null)
            _root.SetActive(false);
    }

    private void EnsureBuilt()
    {
        if (_root != null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[RandomEncounterGeneratorUI] No Canvas found.");
            return;
        }

        _root = new GameObject("RandomEncounterGeneratorScreen", typeof(RectTransform), typeof(Image), typeof(Canvas), typeof(GraphicRaycaster));
        _root.transform.SetParent(canvas.transform, false);

        Canvas overlayCanvas = _root.GetComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = canvas.sortingOrder + 20;

        RectTransform rootRect = _root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image rootImage = _root.GetComponent<Image>();
        rootImage.color = new Color(0.04f, 0.05f, 0.08f, 0.97f);

        GameObject scrollRoot = new GameObject("ScrollRoot", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollRoot.transform.SetParent(_root.transform, false);
        RectTransform scrollRootRect = scrollRoot.GetComponent<RectTransform>();
        scrollRootRect.anchorMin = new Vector2(0.05f, 0.05f);
        scrollRootRect.anchorMax = new Vector2(0.95f, 0.95f);
        scrollRootRect.offsetMin = Vector2.zero;
        scrollRootRect.offsetMax = Vector2.zero;

        Image scrollRootImage = scrollRoot.GetComponent<Image>();
        scrollRootImage.color = new Color(0.03f, 0.04f, 0.07f, 0.7f);

        _mainScrollRect = scrollRoot.GetComponent<ScrollRect>();
        _mainScrollRect.horizontal = false;
        _mainScrollRect.vertical = true;
        _mainScrollRect.movementType = ScrollRect.MovementType.Clamped;
        _mainScrollRect.scrollSensitivity = 36f;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollRoot.transform, false);
        _mainViewportRect = viewport.GetComponent<RectTransform>();
        _mainViewportRect.anchorMin = Vector2.zero;
        _mainViewportRect.anchorMax = Vector2.one;
        _mainViewportRect.offsetMin = new Vector2(8f, 8f);
        _mainViewportRect.offsetMax = new Vector2(-26f, -8f);

        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.06f);
        viewportImage.raycastTarget = true;

        Mask viewportMask = viewport.GetComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        _mainContentRect = content.GetComponent<RectTransform>();
        _mainContentRect.anchorMin = new Vector2(0f, 1f);
        _mainContentRect.anchorMax = new Vector2(1f, 1f);
        _mainContentRect.pivot = new Vector2(0.5f, 1f);
        _mainContentRect.anchoredPosition = Vector2.zero;
        _mainContentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup rootLayout = content.GetComponent<VerticalLayoutGroup>();
        _contentLayoutGroup = rootLayout;
        rootLayout.spacing = LayoutSectionSpacing;
        rootLayout.padding = new RectOffset(20, 20, 18, 20);
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;

        ContentSizeFitter contentFitter = content.GetComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateHeader(content.transform);
        CreatePartyInfoSection(content.transform);
        CreateDifficultySection(content.transform);
        CreateFiltersSection(content.transform);
        CreateGenerateSection(content.transform);
        CreatePreviewSection(content.transform);
        CreateActionButtonsSection(content.transform);

        BuildVerticalScrollbar(scrollRoot.transform, out Scrollbar scrollbar);
        _mainScrollRect.viewport = _mainViewportRect;
        _mainScrollRect.content = _mainContentRect;
        _mainScrollRect.verticalScrollbar = scrollbar;
        _mainScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        RefreshMainLayout();

        _root.SetActive(false);
    }

    private void BuildVerticalScrollbar(Transform parent, out Scrollbar scrollbar)
    {
        GameObject scrollbarObj = new GameObject("VerticalScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        scrollbarObj.transform.SetParent(parent, false);
        RectTransform scrollbarRect = scrollbarObj.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 1f);
        scrollbarRect.offsetMin = new Vector2(-16f, 8f);
        scrollbarRect.offsetMax = new Vector2(-4f, -8f);

        Image scrollbarBg = scrollbarObj.GetComponent<Image>();
        scrollbarBg.color = new Color(0.16f, 0.2f, 0.29f, 0.95f);

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

        Image handleImage = handleObj.GetComponent<Image>();
        handleImage.color = new Color(0.52f, 0.67f, 0.96f, 0.95f);

        scrollbar = scrollbarObj.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = handleImage;
        scrollbar.handleRect = handleRect;
    }

    private void CreateHeader(Transform parent)
    {
        GameObject header = CreateSectionPanel(parent, "Header", new Color(0.09f, 0.11f, 0.17f, 1f), 78f);
        CreateSectionTitle(header.transform, "RANDOM ENCOUNTER GENERATOR", 34, TextAnchor.MiddleCenter, Color.white, false);
    }

    private void CreatePartyInfoSection(Transform parent)
    {
        GameObject section = CreateSectionPanel(parent, "PartyInfoSection", new Color(0.1f, 0.13f, 0.2f, 0.98f), 100f);
        _partySectionRect = section.GetComponent<RectTransform>();
        CreateSectionTitle(section.transform, "1) PARTY INFO", 22, TextAnchor.UpperLeft, new Color(0.95f, 0.86f, 0.45f, 1f));

        _partyInfoText = CreateBodyText(section.transform, 20, new Color(0.9f, 0.94f, 1f, 1f));
        RectTransform textRect = _partyInfoText.rectTransform;
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(20f, 10f);
        textRect.offsetMax = new Vector2(-20f, -44f);
    }

    private void CreateDifficultySection(Transform parent)
    {
        GameObject section = CreateSectionPanel(parent, "DifficultySection", new Color(0.11f, 0.13f, 0.22f, 0.98f), 138f);
        _difficultySectionRect = section.GetComponent<RectTransform>();
        CreateSectionTitle(section.transform, "2) DIFFICULTY", 22, TextAnchor.UpperLeft, new Color(0.95f, 0.86f, 0.45f, 1f));

        GameObject row = new GameObject("DifficultyRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(section.transform, false);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 0f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.offsetMin = new Vector2(16f, 12f);
        rowRect.offsetMax = new Vector2(-16f, -48f);

        HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 8f;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = true;

        for (int i = 0; i < DifficultyOrder.Length; i++)
        {
            RandomEncounterDifficulty difficulty = DifficultyOrder[i];
            Color baseColor = DifficultyColors[difficulty];
            CreateLargeButton(
                row.transform,
                difficulty.ToString(),
                baseColor,
                () => OnDifficultySelected(difficulty),
                out Button button,
                out Text label,
                18);

            _difficultyButtons[difficulty] = button;
            _difficultyButtonLabels[difficulty] = label;
        }
    }

    private void CreateFiltersSection(Transform parent)
    {
        GameObject section = CreateSectionPanel(parent, "FiltersSection", new Color(0.11f, 0.14f, 0.24f, 0.98f), 170f);
        _filtersSectionRect = section.GetComponent<RectTransform>();
        CreateSectionTitle(section.transform, "3) FILTERS (OPTIONAL)", 22, TextAnchor.UpperLeft, new Color(0.95f, 0.86f, 0.45f, 1f));

        GameObject toggleButtonObj = new GameObject("FiltersToggle", typeof(RectTransform), typeof(Image), typeof(Button));
        toggleButtonObj.transform.SetParent(section.transform, false);
        RectTransform toggleRect = toggleButtonObj.GetComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(1f, 1f);
        toggleRect.anchorMax = new Vector2(1f, 1f);
        toggleRect.pivot = new Vector2(1f, 1f);
        toggleRect.anchoredPosition = new Vector2(-16f, -12f);
        toggleRect.sizeDelta = new Vector2(160f, 36f);

        Image toggleImage = toggleButtonObj.GetComponent<Image>();
        toggleImage.color = new Color(0.25f, 0.32f, 0.53f, 1f);

        Button toggleButton = toggleButtonObj.GetComponent<Button>();
        ConfigureButtonColors(toggleButton, toggleImage.color);
        toggleButton.onClick.AddListener(ToggleFiltersVisibility);

        _filtersToggleLabel = CreateTextElement(toggleButtonObj.transform, "SHOW FILTERS", 15, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

        _filtersContent = new GameObject("FiltersContent", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        _filtersContent.transform.SetParent(section.transform, false);
        RectTransform contentRect = _filtersContent.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 0f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.offsetMin = new Vector2(16f, 12f);
        contentRect.offsetMax = new Vector2(-16f, -54f);

        HorizontalLayoutGroup filtersLayout = _filtersContent.GetComponent<HorizontalLayoutGroup>();
        filtersLayout.spacing = 10f;
        filtersLayout.childControlWidth = true;
        filtersLayout.childControlHeight = true;
        filtersLayout.childForceExpandWidth = true;
        filtersLayout.childForceExpandHeight = true;

        _environmentSelector = CreateLabeledOptionSelector(_filtersContent.transform, "Environment", new List<string>
        {
            "Any",
            "Forest",
            "Dungeon",
            "Underground",
            "Urban",
            "Swamp",
            "Desert",
            "Mountain"
        });

        _creatureTypeSelector = CreateLabeledOptionSelector(_filtersContent.transform, "Creature Type", new List<string>
        {
            "Any",
            "Humanoid",
            "Undead",
            "Beast",
            "Monstrous Humanoid",
            "Aberration",
            "Outsider",
            "Construct"
        });

        _minCreaturesInput = CreateLabeledInput(_filtersContent.transform, "Min Creatures", "e.g. 1");
        _maxCreaturesInput = CreateLabeledInput(_filtersContent.transform, "Max Creatures", "e.g. 6");

        _filtersExpanded = false;
        _filtersContent.SetActive(_filtersExpanded);
        RefreshFiltersToggleLabel();
    }

    private void CreateGenerateSection(Transform parent)
    {
        GameObject section = CreateSectionPanel(parent, "GenerateSection", new Color(0.1f, 0.13f, 0.2f, 0.98f), 88f);
        _generateSectionRect = section.GetComponent<RectTransform>();
        CreateSectionTitle(section.transform, "4) GENERATE", 22, TextAnchor.UpperLeft, new Color(0.95f, 0.86f, 0.45f, 1f));

        CreateLargeButton(
            section.transform,
            "GENERATE ENCOUNTER",
            new Color(0.18f, 0.52f, 0.31f, 1f),
            OnGenerateEncounterPressed,
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
        GameObject section = CreateSectionPanel(parent, "PreviewSection", new Color(0.07f, 0.09f, 0.16f, 0.98f), PreviewSectionMinHeight);
        _previewSectionRect = section.GetComponent<RectTransform>();
        _previewSectionLayout = section.GetComponent<LayoutElement>();
        CreateSectionTitle(section.transform, "5) ENCOUNTER PREVIEW", 22, TextAnchor.UpperLeft, new Color(0.95f, 0.86f, 0.45f, 1f));

        GameObject previewContainer = new GameObject("PreviewContainer", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        previewContainer.transform.SetParent(section.transform, false);
        RectTransform containerRect = previewContainer.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0f, 0f);
        containerRect.anchorMax = new Vector2(1f, 1f);
        containerRect.offsetMin = new Vector2(18f, 14f + PreviewSectionBottomMargin);
        containerRect.offsetMax = new Vector2(-18f, -50f);

        previewContainer.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);

        LayoutElement previewContainerLayout = previewContainer.GetComponent<LayoutElement>();
        previewContainerLayout.minHeight = 72f;

        _previewText = CreateBodyText(previewContainer.transform, 20, new Color(0.9f, 0.95f, 1f, 1f));
        RectTransform previewRect = _previewText.rectTransform;
        previewRect.anchorMin = new Vector2(0f, 1f);
        previewRect.anchorMax = new Vector2(1f, 1f);
        previewRect.pivot = new Vector2(0.5f, 1f);
        previewRect.anchoredPosition = new Vector2(0f, -6f);
        previewRect.sizeDelta = new Vector2(-14f, 0f);

        ContentSizeFitter previewTextFitter = _previewText.gameObject.AddComponent<ContentSizeFitter>();
        previewTextFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        previewTextFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _previewText.resizeTextForBestFit = false;
        _previewText.verticalOverflow = VerticalWrapMode.Overflow;

        AdjustPreviewSectionHeight();
    }

    private void CreateActionButtonsSection(Transform parent)
    {
        GameObject section = CreateSectionPanel(parent, "ActionButtonsSection", new Color(0.1f, 0.13f, 0.2f, 0.98f), ActionsSectionMinHeight);
        _actionsSectionRect = section.GetComponent<RectTransform>();
        _actionsSectionLayout = section.GetComponent<LayoutElement>();
        CreateSectionTitle(section.transform, "6) ACTIONS", 22, TextAnchor.UpperLeft, new Color(0.95f, 0.86f, 0.45f, 1f));

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

        CreateLargeButton(row.transform, "Generate Again", new Color(0.24f, 0.4f, 0.68f, 1f), OnGenerateAgainPressed, out _generateAgainButton, out _, 18);
        CreateLargeButton(row.transform, "Start Encounter", new Color(0.2f, 0.48f, 0.27f, 1f), OnStartEncounterPressed, out _startEncounterButton, out _, 18);
        CreateLargeButton(row.transform, "Back to Encounters", new Color(0.5f, 0.22f, 0.22f, 1f), OnBackPressed, out _, out _, 18);
    }

    private void RefreshPartyInfo()
    {
        if (_partyInfoText == null)
            return;

        StringBuilder sb = new StringBuilder();
        sb.Append("Party: ");
        for (int i = 0; i < _partyLevels.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append($"L{Mathf.Max(1, _partyLevels[i])}");
        }

        sb.AppendLine();
        sb.AppendLine($"Party Size: {_partySize}");
        sb.Append($"Calculated APL: <b>{_apl}</b>");

        _partyInfoText.text = sb.ToString();
    }

    private void RefreshDifficultyButtons()
    {
        for (int i = 0; i < DifficultyOrder.Length; i++)
        {
            RandomEncounterDifficulty difficulty = DifficultyOrder[i];
            if (!_difficultyButtons.TryGetValue(difficulty, out Button button)
                || !_difficultyButtonLabels.TryGetValue(difficulty, out Text label))
                continue;

            int targetEl = ChallengeRatingUtils.GetTargetELForDifficulty(_apl, difficulty);
            label.text = $"{difficulty}\nEL {targetEl}";

            Color baseColor = DifficultyColors[difficulty];
            Image image = button.GetComponent<Image>();
            image.color = difficulty == _selectedDifficulty
                ? Color.Lerp(baseColor, Color.white, 0.2f)
                : baseColor;
            ConfigureButtonColors(button, image.color);
        }
    }

    private void OnDifficultySelected(RandomEncounterDifficulty difficulty)
    {
        _selectedDifficulty = difficulty;
        RefreshDifficultyButtons();
    }

    private void ToggleFiltersVisibility()
    {
        _filtersExpanded = !_filtersExpanded;
        if (_filtersContent != null)
            _filtersContent.SetActive(_filtersExpanded);

        RefreshFiltersToggleLabel();
        RefreshMainLayout();
    }

    private void RefreshFiltersToggleLabel()
    {
        if (_filtersToggleLabel != null)
            _filtersToggleLabel.text = _filtersExpanded ? "HIDE FILTERS" : "SHOW FILTERS";
    }

    private void ResetFilterInputs()
    {
        if (_environmentSelector != null)
        {
            _environmentSelector.Index = 0;
            UpdateOptionSelectorLabel(_environmentSelector);
        }

        if (_creatureTypeSelector != null)
        {
            _creatureTypeSelector.Index = 0;
            UpdateOptionSelectorLabel(_creatureTypeSelector);
        }

        if (_minCreaturesInput != null)
            _minCreaturesInput.text = string.Empty;
        if (_maxCreaturesInput != null)
            _maxCreaturesInput.text = string.Empty;
    }

    private void OnGenerateEncounterPressed()
    {
        GenerateEncounter();
    }

    private void OnGenerateAgainPressed()
    {
        GenerateEncounter();
    }

    private void GenerateEncounter()
    {
        RandomEncounterRequest request = BuildRequestFromUI();
        Debug.Log($"[RandomEncounterGeneratorUI] GenerateEncounter pressed | difficulty={request.Difficulty} | apl={_apl} | env='{request.EnvironmentFilter}' | type='{request.CreatureTypeFilter}' | min={request.MinCreatures?.ToString() ?? "-"} | max={request.MaxCreatures?.ToString() ?? "-"}");

        RandomEncounterSystem generator = new RandomEncounterSystem();
        _lastGeneratedEncounter = generator.Generate(request);

        if (_lastGeneratedEncounter == null)
        {
            Debug.LogWarning("[RandomEncounterGeneratorUI] GenerateEncounter returned null encounter.");
            UpdatePreviewText("No valid encounter found for current filters.\nTry loosening filters or creature count limits.");
            UpdateActionButtonState();
            return;
        }

        int npcCount = _lastGeneratedEncounter.NpcIds != null ? _lastGeneratedEncounter.NpcIds.Count : 0;
        int groupCount = _lastGeneratedEncounter.Groups != null ? _lastGeneratedEncounter.Groups.Count : 0;
        Debug.Log($"[RandomEncounterGeneratorUI] Encounter generated | groups={groupCount} | npcIds={npcCount} | actualEl={ChallengeRatingUtils.Format(_lastGeneratedEncounter.ActualEL)} | targetEl={_lastGeneratedEncounter.TargetEL} | xp={_lastGeneratedEncounter.TotalXP} | strategy={_lastGeneratedEncounter.Strategy}");

        UpdatePreviewText(BuildDetailedPreview(_lastGeneratedEncounter));
        UpdateActionButtonState();
    }

    private void OnStartEncounterPressed()
    {
        if (_lastGeneratedEncounter == null || _lastGeneratedEncounter.NpcIds == null || _lastGeneratedEncounter.NpcIds.Count == 0)
            return;

        _onStartEncounter?.Invoke(new List<string>(_lastGeneratedEncounter.NpcIds), _lastGeneratedEncounter);
    }

    private void OnBackPressed()
    {
        _onBack?.Invoke();
    }

    private RandomEncounterRequest BuildRequestFromUI()
    {
        RandomEncounterRequest request = new RandomEncounterRequest
        {
            PartyLevels = new List<int>(_partyLevels),
            PartySize = _partySize,
            Difficulty = _selectedDifficulty,
            EnvironmentFilter = ReadSelectorValue(_environmentSelector),
            CreatureTypeFilter = ReadSelectorValue(_creatureTypeSelector),
            MinCreatures = ParseOptionalPositiveInt(_minCreaturesInput != null ? _minCreaturesInput.text : string.Empty),
            MaxCreatures = ParseOptionalPositiveInt(_maxCreaturesInput != null ? _maxCreaturesInput.text : string.Empty)
        };

        if (request.MinCreatures.HasValue && request.MaxCreatures.HasValue && request.MinCreatures.Value > request.MaxCreatures.Value)
        {
            int temp = request.MinCreatures.Value;
            request.MinCreatures = request.MaxCreatures.Value;
            request.MaxCreatures = temp;
        }

        return request;
    }

    private static string ReadSelectorValue(OptionSelector selector)
    {
        if (selector == null || selector.Options == null || selector.Options.Count == 0)
            return string.Empty;

        int index = Mathf.Clamp(selector.Index, 0, selector.Options.Count - 1);
        string value = selector.Options[index];
        return string.Equals(value, "Any", StringComparison.OrdinalIgnoreCase) ? string.Empty : value;
    }

    private static int? ParseOptionalPositiveInt(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (!int.TryParse(raw.Trim(), out int parsed))
            return null;

        return Mathf.Max(1, parsed);
    }

    private void SetPreviewPlaceholder()
    {
        UpdatePreviewText("No encounter generated yet.\n\nChoose a difficulty and click GENERATE ENCOUNTER.");
    }

    private void UpdatePreviewText(string text)
    {
        if (_previewText == null)
        {
            Debug.LogError("[RandomEncounterGeneratorUI] Preview text component is missing.");
            return;
        }

        if (_previewText.font == null)
            _previewText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        _previewText.enabled = true;
        if (_previewText.gameObject != null)
            _previewText.gameObject.SetActive(true);

        Color previewColor = _previewText.color;
        if (previewColor.a < 0.9f)
            _previewText.color = new Color(previewColor.r, previewColor.g, previewColor.b, 1f);

        _previewText.text = text ?? string.Empty;
        Debug.Log($"[RandomEncounterGeneratorUI] Preview updated | chars={_previewText.text.Length} | active={_previewText.gameObject.activeInHierarchy} | enabled={_previewText.enabled}");

        AdjustPreviewSectionHeight();
    }

    private void UpdateActionButtonState()
    {
        bool hasEncounter = _lastGeneratedEncounter != null && _lastGeneratedEncounter.NpcIds != null && _lastGeneratedEncounter.NpcIds.Count > 0;

        if (_generateAgainButton != null)
            _generateAgainButton.interactable = hasEncounter;
        if (_startEncounterButton != null)
            _startEncounterButton.interactable = hasEncounter;
    }

    private void AdjustPreviewSectionHeight()
    {
        if (_previewSectionLayout == null || _previewText == null)
            return;

        Canvas.ForceUpdateCanvases();

        RectTransform previewRect = _previewText.rectTransform;
        float availableWidth = Mathf.Max(320f, previewRect.rect.width);

        float preferredTextHeight = LayoutUtility.GetPreferredHeight(previewRect);
        if (preferredTextHeight <= 0f)
        {
            TextGenerationSettings settings = _previewText.GetGenerationSettings(new Vector2(availableWidth, 0f));
            preferredTextHeight = _previewText.cachedTextGeneratorForLayout.GetPreferredHeight(_previewText.text ?? string.Empty, settings) / _previewText.pixelsPerUnit;
        }

        float sectionHeight = Mathf.Max(PreviewSectionMinHeight, preferredTextHeight + PreviewSectionVerticalChrome + PreviewSectionBottomMargin);
        _previewSectionLayout.minHeight = sectionHeight;
        _previewSectionLayout.preferredHeight = sectionHeight;
        _previewSectionLayout.flexibleHeight = 0f;

        if (_actionsSectionLayout != null)
        {
            _actionsSectionLayout.minHeight = ActionsSectionMinHeight;
            _actionsSectionLayout.preferredHeight = ActionsSectionMinHeight;
            _actionsSectionLayout.flexibleHeight = 0f;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(previewRect);
        RefreshMainLayout();

        Debug.Log($"[RandomEncounterGeneratorUI] Preview layout rebuilt | chars={(_previewText.text ?? string.Empty).Length} | preferredTextHeight={preferredTextHeight:F1} | sectionHeight={sectionHeight:F1} | textRectHeight={previewRect.rect.height:F1}");
    }

    private void RefreshMainLayout()
    {
        if (_mainContentRect == null)
            return;

        EnsureScrollRectConfiguration();

        if (_contentLayoutGroup != null && _contentLayoutGroup.spacing < 0f)
        {
            Debug.LogWarning($"[RandomEncounterGeneratorUI] Negative section spacing detected ({_contentLayoutGroup.spacing:F1}); clamping to {LayoutSectionSpacing:F1}.");
            _contentLayoutGroup.spacing = LayoutSectionSpacing;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_mainContentRect);

        if (_mainScrollRect != null)
        {
            _mainScrollRect.Rebuild(CanvasUpdate.PostLayout);
            _mainScrollRect.enabled = true;
        }

        float contentHeight = _mainContentRect.rect.height;
        float viewportHeight = _mainViewportRect != null ? _mainViewportRect.rect.height : 0f;
        bool scrollable = contentHeight > viewportHeight + 0.5f;
        LogSectionLayoutDiagnostics();

        Debug.Log($"[RandomEncounterGeneratorUI] Scroll layout refresh | contentHeight={contentHeight:F1} | viewportHeight={viewportHeight:F1} | scrollable={scrollable} | scrollEnabled={(_mainScrollRect != null && _mainScrollRect.enabled)} | hasScrollbar={(_mainScrollRect != null && _mainScrollRect.verticalScrollbar != null)}");
    }

    private void EnsureScrollRectConfiguration()
    {
        if (_mainScrollRect == null)
            return;

        if (_mainScrollRect.viewport == null && _mainViewportRect != null)
            _mainScrollRect.viewport = _mainViewportRect;

        if (_mainScrollRect.content == null && _mainContentRect != null)
            _mainScrollRect.content = _mainContentRect;

        if (_mainViewportRect != null)
        {
            Mask viewportMask = _mainViewportRect.GetComponent<Mask>();
            if (viewportMask == null)
                viewportMask = _mainViewportRect.gameObject.AddComponent<Mask>();

            viewportMask.showMaskGraphic = false;
        }

        if (_mainContentRect != null)
        {
            if (_mainViewportRect != null && _mainContentRect.parent != _mainViewportRect)
                _mainContentRect.SetParent(_mainViewportRect, false);

            _mainContentRect.anchorMin = new Vector2(0f, 1f);
            _mainContentRect.anchorMax = new Vector2(1f, 1f);
            _mainContentRect.pivot = new Vector2(0.5f, 1f);
        }

        Debug.Log($"[RandomEncounterGeneratorUI] ScrollRect verify | enabled={_mainScrollRect.enabled} | hasViewport={_mainScrollRect.viewport != null} | hasContent={_mainScrollRect.content != null} | hasScrollbar={_mainScrollRect.verticalScrollbar != null}");
    }

    private void LogSectionLayoutDiagnostics()
    {
        if (_mainContentRect == null)
            return;

        int sectionCount = _mainContentRect.childCount;
        int partyIndex = _partySectionRect != null ? _partySectionRect.GetSiblingIndex() : -1;
        int difficultyIndex = _difficultySectionRect != null ? _difficultySectionRect.GetSiblingIndex() : -1;
        int filtersIndex = _filtersSectionRect != null ? _filtersSectionRect.GetSiblingIndex() : -1;
        int generateIndex = _generateSectionRect != null ? _generateSectionRect.GetSiblingIndex() : -1;
        int previewIndex = _previewSectionRect != null ? _previewSectionRect.GetSiblingIndex() : -1;
        int actionsIndex = _actionsSectionRect != null ? _actionsSectionRect.GetSiblingIndex() : -1;

        bool expectedOrder = partyIndex >= 0
            && partyIndex < difficultyIndex
            && difficultyIndex < filtersIndex
            && filtersIndex < generateIndex
            && generateIndex < previewIndex
            && previewIndex < actionsIndex;

        float spacing = _contentLayoutGroup != null ? _contentLayoutGroup.spacing : -1f;
        bool invalidSpacing = spacing < 0f;

        string previewLayout = _previewSectionRect != null
            ? $"y={_previewSectionRect.anchoredPosition.y:F1}, h={_previewSectionRect.rect.height:F1}, min={(_previewSectionLayout != null ? _previewSectionLayout.minHeight : 0f):F1}, pref={(_previewSectionLayout != null ? _previewSectionLayout.preferredHeight : 0f):F1}"
            : "missing";
        string actionsLayout = _actionsSectionRect != null
            ? $"y={_actionsSectionRect.anchoredPosition.y:F1}, h={_actionsSectionRect.rect.height:F1}, min={(_actionsSectionLayout != null ? _actionsSectionLayout.minHeight : 0f):F1}, pref={(_actionsSectionLayout != null ? _actionsSectionLayout.preferredHeight : 0f):F1}"
            : "missing";

        Debug.Log($"[RandomEncounterGeneratorUI] Section layout diagnostics | sectionCount={sectionCount} | spacing={spacing:F1} | invalidSpacing={invalidSpacing} | order={partyIndex}>{difficultyIndex}>{filtersIndex}>{generateIndex}>{previewIndex}>{actionsIndex} | expectedOrder={expectedOrder} | preview=({previewLayout}) | actions=({actionsLayout})");
    }

    private static string BuildDetailedPreview(GeneratedRandomEncounter encounter)
    {
        if (encounter == null)
            return "No encounter generated.";

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"<b>{encounter.Difficulty} Encounter (EL {ChallengeRatingUtils.Format(encounter.ActualEL)} vs APL {encounter.APL})</b>");
        sb.AppendLine($"Difficulty: {BuildDifficultyStars(encounter.Difficulty)} {encounter.Difficulty}");
        sb.AppendLine($"Strategy: {FormatStrategy(encounter.Strategy)}");
        sb.AppendLine();
        sb.AppendLine("Enemies:");

        List<RandomEncounterCreatureGroup> groups = encounter.Groups;
        if (groups == null || groups.Count == 0)
        {
            sb.AppendLine("• (No creature groups returned)");
        }
        else
        {
            for (int i = 0; i < groups.Count; i++)
            {
                RandomEncounterCreatureGroup group = groups[i];
                if (group == null)
                    continue;

                int count = Mathf.Max(1, group.Count);
                string name = string.IsNullOrWhiteSpace(group.DisplayName) ? (group.NpcId ?? "Unknown Monster") : group.DisplayName;
                string cr = ChallengeRatingUtils.Format(group.ChallengeRatingValue);
                sb.AppendLine($"• {count} × {name} (CR {cr})");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"Total: {Mathf.Max(0, encounter.TotalCreatureCount)} creatures");
        sb.AppendLine($"EL: {ChallengeRatingUtils.Format(encounter.ActualEL)}");
        sb.AppendLine($"XP: {encounter.TotalXP:N0}");
        return sb.ToString().TrimEnd();
    }

    private static string BuildDifficultyStars(RandomEncounterDifficulty difficulty)
    {
        switch (difficulty)
        {
            case RandomEncounterDifficulty.Easy:
                return "⭐";
            case RandomEncounterDifficulty.Average:
                return "⭐⭐";
            case RandomEncounterDifficulty.Challenging:
                return "⭐⭐";
            case RandomEncounterDifficulty.Hard:
                return "⭐⭐⭐";
            case RandomEncounterDifficulty.Epic:
                return "⭐⭐⭐⭐";
            default:
                return "⭐";
        }
    }

    private static string FormatStrategy(RandomEncounterStrategy strategy)
    {
        switch (strategy)
        {
            case RandomEncounterStrategy.SingleBoss:
                return "Single Boss";
            case RandomEncounterStrategy.EliteSquad:
                return "Elite Squad";
            case RandomEncounterStrategy.Swarm:
                return "Swarm";
            case RandomEncounterStrategy.MixedGroup:
                return "Mixed Group";
            default:
                return strategy.ToString();
        }
    }

    private GameObject CreateSectionPanel(Transform parent, string name, Color color, float preferredHeight)
    {
        GameObject section = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        section.transform.SetParent(parent, false);

        Image image = section.GetComponent<Image>();
        image.color = color;

        Outline outline = section.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.4f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        LayoutElement layout = section.GetComponent<LayoutElement>();
        layout.preferredHeight = preferredHeight;
        layout.minHeight = preferredHeight;
        layout.flexibleHeight = 0f;

        return section;
    }

    private void CreateSectionTitle(Transform parent, string title, int fontSize, TextAnchor anchor, Color color, bool includeBottomDivider = true)
    {
        Text titleText = CreateTextElement(parent, title, fontSize, FontStyle.Bold, color, anchor);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -LayoutSectionPadding);
        titleRect.sizeDelta = new Vector2(-(LayoutSectionPadding * 2f), 32f);

        if (!includeBottomDivider)
            return;

        GameObject divider = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        divider.transform.SetParent(parent, false);
        RectTransform dividerRect = divider.GetComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(0f, 1f);
        dividerRect.anchorMax = new Vector2(1f, 1f);
        dividerRect.pivot = new Vector2(0.5f, 1f);
        dividerRect.anchoredPosition = new Vector2(0f, -42f);
        dividerRect.sizeDelta = new Vector2(-26f, 2f);
        divider.GetComponent<Image>().color = new Color(0.28f, 0.34f, 0.5f, 0.95f);
    }

    private Text CreateBodyText(Transform parent, int fontSize, Color color)
    {
        Text bodyText = CreateTextElement(parent, string.Empty, fontSize, FontStyle.Normal, color, TextAnchor.UpperLeft);
        bodyText.supportRichText = true;
        bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bodyText.verticalOverflow = VerticalWrapMode.Truncate;
        return bodyText;
    }

    private Text CreateTextElement(Transform parent, string value, int fontSize, FontStyle style, Color color, TextAnchor alignment)
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

    private OptionSelector CreateLabeledOptionSelector(Transform parent, string label, List<string> options)
    {
        GameObject slot = CreateFieldSlot(parent, label);

        OptionSelector selector = new OptionSelector
        {
            Options = options != null ? new List<string>(options) : new List<string> { "Any" },
            Index = 0
        };

        GameObject selectorButtonObj = new GameObject("SelectorButton", typeof(RectTransform), typeof(Image), typeof(Button));
        selectorButtonObj.transform.SetParent(slot.transform, false);
        RectTransform rt = selectorButtonObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 8f);
        rt.sizeDelta = new Vector2(-12f, 34f);

        Image image = selectorButtonObj.GetComponent<Image>();
        image.color = new Color(0.2f, 0.26f, 0.4f, 1f);

        selector.Button = selectorButtonObj.GetComponent<Button>();
        ConfigureButtonColors(selector.Button, image.color);
        selector.Button.onClick.AddListener(() =>
        {
            if (selector.Options == null || selector.Options.Count == 0)
                return;

            selector.Index = (selector.Index + 1) % selector.Options.Count;
            UpdateOptionSelectorLabel(selector);
        });

        selector.ValueLabel = CreateTextElement(selectorButtonObj.transform, string.Empty, DefaultFontSize - 6, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        RectTransform labelRect = selector.ValueLabel.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 0f);
        labelRect.offsetMax = new Vector2(-8f, 0f);

        UpdateOptionSelectorLabel(selector);
        return selector;
    }

    private InputField CreateLabeledInput(Transform parent, string label, string placeholder)
    {
        GameObject slot = CreateFieldSlot(parent, label);
        InputField input = CreateInputField(slot.transform, placeholder);
        return input;
    }

    private static void UpdateOptionSelectorLabel(OptionSelector selector)
    {
        if (selector == null || selector.ValueLabel == null)
            return;

        if (selector.Options == null || selector.Options.Count == 0)
        {
            selector.ValueLabel.text = "Any";
            return;
        }

        int index = Mathf.Clamp(selector.Index, 0, selector.Options.Count - 1);
        selector.ValueLabel.text = selector.Options[index];
    }

    private GameObject CreateFieldSlot(Transform parent, string label)
    {
        GameObject slot = new GameObject($"Slot_{label}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        slot.transform.SetParent(parent, false);

        slot.GetComponent<Image>().color = new Color(0.08f, 0.11f, 0.18f, 0.95f);
        LayoutElement layout = slot.GetComponent<LayoutElement>();
        layout.preferredWidth = 180f;

        Text labelText = CreateTextElement(slot.transform, label, 16, FontStyle.Bold, new Color(0.86f, 0.9f, 1f, 1f), TextAnchor.UpperLeft);
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = new Vector2(0f, -6f);
        labelRect.sizeDelta = new Vector2(-16f, 24f);

        return slot;
    }

    private InputField CreateInputField(Transform parent, string placeholder)
    {
        GameObject inputObj = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(InputField));
        inputObj.transform.SetParent(parent, false);
        RectTransform rt = inputObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 8f);
        rt.sizeDelta = new Vector2(-12f, 34f);

        inputObj.GetComponent<Image>().color = new Color(0.2f, 0.26f, 0.4f, 1f);

        Text text = CreateTextElement(inputObj.transform, string.Empty, 16, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 0f);
        textRect.offsetMax = new Vector2(-8f, 0f);

        Text placeholderText = CreateTextElement(inputObj.transform, placeholder, 15, FontStyle.Normal, new Color(0.75f, 0.82f, 0.92f, 0.9f), TextAnchor.MiddleLeft);
        RectTransform placeholderRect = placeholderText.rectTransform;
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(8f, 0f);
        placeholderRect.offsetMax = new Vector2(-8f, 0f);

        InputField input = inputObj.GetComponent<InputField>();
        input.textComponent = text;
        input.placeholder = placeholderText;
        input.lineType = InputField.LineType.SingleLine;
        return input;
    }

    private void CreateLargeButton(
        Transform parent,
        string label,
        Color color,
        Action onClick,
        out Button button,
        out Text buttonText,
        int fontSize,
        Vector2? sizeDelta = null,
        Vector2? anchorMin = null,
        Vector2? anchorMax = null,
        Vector2? pivot = null,
        Vector2? anchoredPosition = null)
    {
        GameObject buttonObj = new GameObject($"Button_{label}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObj.transform.SetParent(parent, false);

        RectTransform rt = buttonObj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin ?? new Vector2(0.5f, 0.5f);
        rt.anchorMax = anchorMax ?? new Vector2(0.5f, 0.5f);
        rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPosition ?? Vector2.zero;
        rt.sizeDelta = sizeDelta ?? new Vector2(0f, 0f);

        LayoutElement layout = buttonObj.GetComponent<LayoutElement>();
        float buttonHeight = Mathf.Max(42f, (sizeDelta ?? new Vector2(0f, 44f)).y);
        layout.minHeight = buttonHeight;
        layout.preferredHeight = buttonHeight;
        layout.flexibleHeight = 0f;

        Image image = buttonObj.GetComponent<Image>();
        image.color = color;

        button = buttonObj.GetComponent<Button>();
        ConfigureButtonColors(button, color);
        button.onClick.AddListener(() => onClick?.Invoke());

        buttonText = CreateTextElement(buttonObj.transform, label, fontSize, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        RectTransform textRect = buttonText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(6f, 4f);
        textRect.offsetMax = new Vector2(-6f, -4f);
    }

    private void ConfigureButtonColors(Button button, Color baseColor)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.2f);
        colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.2f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.85f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.targetGraphic = button.GetComponent<Image>();
    }
}
