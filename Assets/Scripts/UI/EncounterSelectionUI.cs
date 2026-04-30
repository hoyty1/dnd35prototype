using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modal used before combat to choose a preset encounter or generate a random DMG-style encounter.
/// </summary>
public class EncounterSelectionUI : MonoBehaviour
{
    private GameObject _panel;
    private Text _descriptionText;
    private Text _selectionHintText;
    private ScrollRect _scrollRect;
    private RectTransform _contentContainer;
    private Button _confirmButton;

    // Random encounter controls
    private Text _aplText;
    private Text _randomPreviewText;
    private InputField _environmentInput;
    private InputField _creatureTypeInput;
    private InputField _minCreaturesInput;
    private InputField _maxCreaturesInput;
    private Button _difficultyButton;
    private Text _difficultyButtonText;
    private Button _generateRandomButton;
    private Button _generateAgainButton;
    private Button _startRandomButton;

    private Action<string> _onSelect;
    private Action<List<string>, GeneratedRandomEncounter> _onStartRandomEncounter;
    private Action _onCancel;
    private string _selectedPresetId;

    private readonly Dictionary<string, Image> _cardImages = new Dictionary<string, Image>();
    private readonly Dictionary<string, Outline> _cardOutlines = new Dictionary<string, Outline>();

    private static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.12f, 0.97f);
    private static readonly Color ScrollAreaColor = new Color(0.05f, 0.06f, 0.1f, 0.95f);
    private static readonly Color CardColor = new Color(0.17f, 0.2f, 0.3f, 0.96f);
    private static readonly Color CardSelectedColor = new Color(0.26f, 0.34f, 0.52f, 1f);
    private static readonly Color CategoryColor = new Color(0.98f, 0.83f, 0.35f, 1f);

    private static readonly RandomEncounterDifficulty[] DifficultyCycle =
    {
        RandomEncounterDifficulty.Easy,
        RandomEncounterDifficulty.Average,
        RandomEncounterDifficulty.Challenging,
        RandomEncounterDifficulty.Hard,
        RandomEncounterDifficulty.Epic
    };

    public bool IsOpen => _panel != null && _panel.activeSelf;

    private int _partyAverageLevel = 3;
    private List<int> _partyLevels = new List<int>();
    private int _partySize = 4;
    private int _difficultyIndex = 2; // Challenging default
    private GeneratedRandomEncounter _lastGeneratedEncounter;

    public void Open(
        List<EncounterPreset> presets,
        Action<string> onSelect,
        Action<List<string>, GeneratedRandomEncounter> onStartRandomEncounter,
        Action onCancel = null,
        int partyAverageLevel = 3,
        List<int> partyLevels = null,
        int partySize = 4)
    {
        EnsureBuilt();
        if (_panel == null) return;

        _onSelect = onSelect;
        _onStartRandomEncounter = onStartRandomEncounter;
        _onCancel = onCancel;
        _partyAverageLevel = Mathf.Max(1, partyAverageLevel);
        _partyLevels = partyLevels != null ? new List<int>(partyLevels) : new List<int> { _partyAverageLevel, _partyAverageLevel, _partyAverageLevel, _partyAverageLevel };
        _partySize = Mathf.Max(1, partySize);
        _partyAverageLevel = ChallengeRatingUtils.CalculateAPL(_partyLevels, _partySize);
        _lastGeneratedEncounter = null;

        _panel.SetActive(true);
        _descriptionText.text = "Pick a preset or generate a balanced random encounter using DMG Chapter 3 rules.";

        RefreshAplDisplay();
        RefreshDifficultyLabel();
        SetRandomPreview("No random encounter generated yet.");
        UpdateRandomButtonsState();

        BuildEncounterCards(presets);

        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 1f;
    }

    public void Close()
    {
        if (_panel != null)
            _panel.SetActive(false);
    }

    private void EnsureBuilt()
    {
        if (_panel != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[EncounterSelectionUI] No Canvas found.");
            return;
        }

        _panel = new GameObject("EncounterSelectionPanel", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = (RectTransform)_panel.transform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(760f, 640f);

        Image panelImage = _panel.GetComponent<Image>();
        panelImage.color = PanelColor;

        Outline panelOutline = _panel.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0f, 0f, 0f, 0.55f);
        panelOutline.effectDistance = new Vector2(2f, -2f);

        CreateText(
            _panel.transform,
            "SELECT ENCOUNTER",
            30,
            FontStyle.Bold,
            Color.white,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -30f),
            new Vector2(680f, 42f),
            TextAnchor.MiddleCenter,
            out _
        );

        CreateText(
            _panel.transform,
            "",
            16,
            FontStyle.Normal,
            new Color(0.85f, 0.89f, 0.95f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -68f),
            new Vector2(690f, 30f),
            TextAnchor.MiddleCenter,
            out _descriptionText
        );

        CreateText(
            _panel.transform,
            "",
            14,
            FontStyle.Italic,
            new Color(0.75f, 0.82f, 0.95f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -96f),
            new Vector2(690f, 26f),
            TextAnchor.MiddleCenter,
            out _selectionHintText
        );

        BuildRandomEncounterSection();

        CreateText(
            _panel.transform,
            "▲ Scroll Up",
            13,
            FontStyle.Bold,
            new Color(0.82f, 0.88f, 1f, 0.95f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -300f),
            new Vector2(220f, 20f),
            TextAnchor.MiddleCenter,
            out _
        );

        GameObject scrollRoot = new GameObject("ScrollRoot", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollRoot.transform.SetParent(_panel.transform, false);
        RectTransform scrollRootRect = scrollRoot.GetComponent<RectTransform>();
        scrollRootRect.anchorMin = new Vector2(0.05f, 0.18f);
        scrollRootRect.anchorMax = new Vector2(0.95f, 0.54f);
        scrollRootRect.offsetMin = Vector2.zero;
        scrollRootRect.offsetMax = Vector2.zero;

        Image scrollRootImage = scrollRoot.GetComponent<Image>();
        scrollRootImage.color = ScrollAreaColor;

        _scrollRect = scrollRoot.GetComponent<ScrollRect>();
        _scrollRect.horizontal = false;
        _scrollRect.vertical = true;
        _scrollRect.movementType = ScrollRect.MovementType.Clamped;
        _scrollRect.scrollSensitivity = 24f;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollRoot.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0f, 0f);
        viewportRect.anchorMax = new Vector2(1f, 1f);
        viewportRect.offsetMin = new Vector2(8f, 8f);
        viewportRect.offsetMax = new Vector2(-24f, -8f);

        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.1f);
        Mask mask = viewport.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        _contentContainer = content.GetComponent<RectTransform>();
        _contentContainer.anchorMin = new Vector2(0f, 1f);
        _contentContainer.anchorMax = new Vector2(1f, 1f);
        _contentContainer.pivot = new Vector2(0.5f, 1f);
        _contentContainer.anchoredPosition = Vector2.zero;
        _contentContainer.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layoutGroup = content.GetComponent<VerticalLayoutGroup>();
        layoutGroup.spacing = 8f;
        layoutGroup.padding = new RectOffset(8, 8, 8, 8);
        layoutGroup.childAlignment = TextAnchor.UpperCenter;
        layoutGroup.childControlHeight = false;
        layoutGroup.childControlWidth = true;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;

        ContentSizeFitter sizeFitter = content.GetComponent<ContentSizeFitter>();
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        _scrollRect.viewport = viewportRect;
        _scrollRect.content = _contentContainer;

        BuildVerticalScrollbar(scrollRoot.transform, out Scrollbar scrollbar);
        _scrollRect.verticalScrollbar = scrollbar;
        _scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        CreateText(
            _panel.transform,
            "▼ Scroll Down",
            13,
            FontStyle.Bold,
            new Color(0.82f, 0.88f, 1f, 0.95f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 122f),
            new Vector2(220f, 20f),
            TextAnchor.MiddleCenter,
            out _
        );

        BuildFooterButtons();

        _panel.SetActive(false);
    }

    private void BuildRandomEncounterSection()
    {
        GameObject randomPanel = new GameObject("RandomEncounterPanel", typeof(RectTransform), typeof(Image));
        randomPanel.transform.SetParent(_panel.transform, false);

        RectTransform panelRect = randomPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.05f, 0.56f);
        panelRect.anchorMax = new Vector2(0.95f, 0.79f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelImage = randomPanel.GetComponent<Image>();
        panelImage.color = new Color(0.1f, 0.14f, 0.22f, 0.97f);

        Outline panelOutline = randomPanel.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.97f, 0.86f, 0.53f, 0.7f);
        panelOutline.effectDistance = new Vector2(1.2f, -1.2f);

        CreateText(
            randomPanel.transform,
            "🎲 RANDOM ENCOUNTER (DMG CH.3)",
            15,
            FontStyle.Bold,
            new Color(0.98f, 0.9f, 0.56f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(10f, -8f),
            new Vector2(500f, 20f),
            TextAnchor.UpperLeft,
            out _
        );

        CreateText(
            randomPanel.transform,
            "",
            13,
            FontStyle.Bold,
            new Color(0.85f, 0.92f, 1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(10f, -30f),
            new Vector2(280f, 20f),
            TextAnchor.MiddleLeft,
            out _aplText
        );

        CreateButton(randomPanel.transform, "Difficulty", new Color(0.19f, 0.34f, 0.56f, 1f), OnDifficultyCyclePressed,
            new Vector2(0.45f, 1f), new Vector2(0.73f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -28f), new Vector2(-8f, 24f), out _difficultyButton, out _difficultyButtonText);

        _difficultyButtonText.fontSize = 12;

        CreateButton(randomPanel.transform, "Random Encounter", new Color(0.21f, 0.45f, 0.28f, 1f), OnGenerateRandomPressed,
            new Vector2(0.74f, 1f), new Vector2(0.99f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -28f), new Vector2(-8f, 24f), out _generateRandomButton, out _);

        CreateInputField(randomPanel.transform, "Environment (optional)",
            new Vector2(0f, 1f), new Vector2(0.33f, 1f), new Vector2(0f, 1f),
            new Vector2(10f, -58f), new Vector2(-10f, 24f), out _environmentInput);

        CreateInputField(randomPanel.transform, "Type (optional)",
            new Vector2(0.34f, 1f), new Vector2(0.67f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -58f), new Vector2(-10f, 24f), out _creatureTypeInput);

        CreateInputField(randomPanel.transform, "Min #",
            new Vector2(0.68f, 1f), new Vector2(0.83f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -58f), new Vector2(-10f, 24f), out _minCreaturesInput);

        CreateInputField(randomPanel.transform, "Max #",
            new Vector2(0.84f, 1f), new Vector2(0.99f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -58f), new Vector2(-8f, 24f), out _maxCreaturesInput);

        CreateText(
            randomPanel.transform,
            "",
            12,
            FontStyle.Normal,
            new Color(0.93f, 0.95f, 0.99f, 1f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(10f, 44f),
            new Vector2(620f, 52f),
            TextAnchor.UpperLeft,
            out _randomPreviewText
        );

        CreateButton(randomPanel.transform, "Generate Again", new Color(0.24f, 0.38f, 0.62f, 1f), OnGenerateAgainPressed,
            new Vector2(0f, 0f), new Vector2(0.32f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 8f), new Vector2(-8f, 30f), out _generateAgainButton, out _);

        CreateButton(randomPanel.transform, "Start Encounter", new Color(0.2f, 0.45f, 0.28f, 1f), OnStartRandomEncounterPressed,
            new Vector2(0.33f, 0f), new Vector2(0.66f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 8f), new Vector2(-8f, 30f), out _startRandomButton, out _);
    }

    private void RefreshAplDisplay()
    {
        int apl = ChallengeRatingUtils.CalculateAPL(_partyLevels, _partySize);
        if (_aplText != null)
            _aplText.text = $"APL: {apl} (Party size: {_partySize})";
    }

    private void RefreshDifficultyLabel()
    {
        if (_difficultyButtonText != null)
            _difficultyButtonText.text = $"Difficulty: {DifficultyCycle[_difficultyIndex]}";
    }

    private void SetRandomPreview(string value)
    {
        if (_randomPreviewText != null)
            _randomPreviewText.text = value;
    }

    private void UpdateRandomButtonsState()
    {
        bool hasEncounter = _lastGeneratedEncounter != null && _lastGeneratedEncounter.NpcIds != null && _lastGeneratedEncounter.NpcIds.Count > 0;

        if (_generateAgainButton != null)
            _generateAgainButton.interactable = hasEncounter;

        if (_startRandomButton != null)
            _startRandomButton.interactable = hasEncounter;
    }

    private void OnDifficultyCyclePressed()
    {
        _difficultyIndex = (_difficultyIndex + 1) % DifficultyCycle.Length;
        RefreshDifficultyLabel();
    }

    private void OnGenerateRandomPressed()
    {
        RandomEncounterRequest request = BuildRandomRequestFromUI();
        RandomEncounterSystem generator = new RandomEncounterSystem();
        _lastGeneratedEncounter = generator.Generate(request);

        if (_lastGeneratedEncounter == null)
        {
            SetRandomPreview("No valid random encounter found for current filters. Try relaxing filters or creature count limits.");
            UpdateRandomButtonsState();
            return;
        }

        SetRandomPreview(_lastGeneratedEncounter.BuildPreviewText());
        UpdateRandomButtonsState();
    }

    private void OnGenerateAgainPressed()
    {
        OnGenerateRandomPressed();
    }

    private void OnStartRandomEncounterPressed()
    {
        if (_lastGeneratedEncounter == null || _lastGeneratedEncounter.NpcIds == null || _lastGeneratedEncounter.NpcIds.Count == 0)
            return;

        List<string> encounterNpcIds = new List<string>(_lastGeneratedEncounter.NpcIds);
        Close();
        _onStartRandomEncounter?.Invoke(encounterNpcIds, _lastGeneratedEncounter);
    }

    private RandomEncounterRequest BuildRandomRequestFromUI()
    {
        RandomEncounterRequest request = new RandomEncounterRequest
        {
            PartyLevels = new List<int>(_partyLevels),
            PartySize = _partySize,
            Difficulty = DifficultyCycle[_difficultyIndex],
            EnvironmentFilter = _environmentInput != null ? _environmentInput.text : string.Empty,
            CreatureTypeFilter = _creatureTypeInput != null ? _creatureTypeInput.text : string.Empty,
            MinCreatures = ParseOptionalPositiveInt(_minCreaturesInput != null ? _minCreaturesInput.text : null),
            MaxCreatures = ParseOptionalPositiveInt(_maxCreaturesInput != null ? _maxCreaturesInput.text : null)
        };

        if (request.MinCreatures.HasValue && request.MaxCreatures.HasValue && request.MinCreatures.Value > request.MaxCreatures.Value)
        {
            int temp = request.MinCreatures.Value;
            request.MinCreatures = request.MaxCreatures.Value;
            request.MaxCreatures = temp;
        }

        return request;
    }

    private int? ParseOptionalPositiveInt(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (!int.TryParse(raw.Trim(), out int parsed))
            return null;

        return Mathf.Max(1, parsed);
    }

    private void CreateInputField(
        Transform parent,
        string placeholder,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPos,
        Vector2 size,
        out InputField inputField)
    {
        GameObject root = new GameObject($"Input_{placeholder}", typeof(RectTransform), typeof(Image), typeof(InputField));
        root.transform.SetParent(parent, false);

        RectTransform rt = root.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image bg = root.GetComponent<Image>();
        bg.color = new Color(0.06f, 0.09f, 0.16f, 0.95f);

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(root.transform, false);
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(8f, 2f);
        textRt.offsetMax = new Vector2(-8f, -2f);

        Text text = textGo.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 11;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = Color.white;

        GameObject placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
        placeholderGo.transform.SetParent(root.transform, false);
        RectTransform placeholderRt = placeholderGo.GetComponent<RectTransform>();
        placeholderRt.anchorMin = Vector2.zero;
        placeholderRt.anchorMax = Vector2.one;
        placeholderRt.offsetMin = new Vector2(8f, 2f);
        placeholderRt.offsetMax = new Vector2(-8f, -2f);

        Text placeholderText = placeholderGo.GetComponent<Text>();
        placeholderText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        placeholderText.fontSize = 10;
        placeholderText.alignment = TextAnchor.MiddleLeft;
        placeholderText.color = new Color(0.65f, 0.72f, 0.86f, 0.9f);
        placeholderText.text = placeholder;

        inputField = root.GetComponent<InputField>();
        inputField.textComponent = text;
        inputField.placeholder = placeholderText;
        inputField.lineType = InputField.LineType.SingleLine;
    }

    private void CreateButton(
        Transform parent,
        string label,
        Color color,
        Action onClick,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPos,
        Vector2 size,
        out Button button,
        out Text buttonText)
    {
        GameObject buttonObj = new GameObject($"Button_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObj.transform.SetParent(parent, false);

        RectTransform rt = buttonObj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image image = buttonObj.GetComponent<Image>();
        image.color = color;

        button = buttonObj.GetComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.2f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.25f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        button.colors = colors;

        button.onClick.AddListener(() => onClick?.Invoke());

        CreateText(
            buttonObj.transform,
            label,
            13,
            FontStyle.Bold,
            Color.white,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(0f, 0f),
            TextAnchor.MiddleCenter,
            out buttonText
        );
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
        scrollbarBg.color = new Color(0.15f, 0.17f, 0.24f, 0.95f);

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
        handleRect.sizeDelta = new Vector2(0f, 52f);

        Image handleImage = handleObj.GetComponent<Image>();
        handleImage.color = new Color(0.54f, 0.67f, 0.95f, 0.95f);

        scrollbar = scrollbarObj.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = handleImage;
        scrollbar.handleRect = handleRect;
        scrollbar.size = 0.24f;
    }

    private void BuildFooterButtons()
    {
        GameObject footer = new GameObject("Footer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        footer.transform.SetParent(_panel.transform, false);
        RectTransform footerRect = footer.GetComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0.08f, 0.04f);
        footerRect.anchorMax = new Vector2(0.92f, 0.13f);
        footerRect.offsetMin = Vector2.zero;
        footerRect.offsetMax = Vector2.zero;

        HorizontalLayoutGroup footerLayout = footer.GetComponent<HorizontalLayoutGroup>();
        footerLayout.spacing = 14f;
        footerLayout.childAlignment = TextAnchor.MiddleCenter;
        footerLayout.childControlWidth = true;
        footerLayout.childControlHeight = true;
        footerLayout.childForceExpandWidth = true;
        footerLayout.childForceExpandHeight = true;

        CreateFooterButton(footer.transform, "Cancel", new Color(0.45f, 0.2f, 0.2f, 1f), OnCancelPressed, out _);
        CreateFooterButton(footer.transform, "Select", new Color(0.2f, 0.45f, 0.28f, 1f), OnConfirmPressed, out _confirmButton);
        _confirmButton.interactable = false;
    }

    private void CreateFooterButton(Transform parent, string label, Color color, Action onClick, out Button button)
    {
        GameObject buttonObj = new GameObject($"Footer_{label}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObj.transform.SetParent(parent, false);

        LayoutElement le = buttonObj.GetComponent<LayoutElement>();
        le.preferredWidth = 180f;
        le.preferredHeight = 44f;
        le.minHeight = 44f;

        Image image = buttonObj.GetComponent<Image>();
        image.color = color;

        button = buttonObj.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.2f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.22f, 0.22f, 0.22f, 0.9f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        button.onClick.AddListener(() => onClick?.Invoke());

        CreateText(
            buttonObj.transform,
            label,
            18,
            FontStyle.Bold,
            Color.white,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(140f, 30f),
            TextAnchor.MiddleCenter,
            out _
        );
    }

    private void BuildEncounterCards(List<EncounterPreset> presets)
    {
        if (_contentContainer == null)
            return;

        _cardImages.Clear();
        _cardOutlines.Clear();

        for (int i = _contentContainer.childCount - 1; i >= 0; i--)
            Destroy(_contentContainer.GetChild(i).gameObject);

        List<EncounterPreset> validPresets = new List<EncounterPreset>();
        if (presets != null)
        {
            for (int i = 0; i < presets.Count; i++)
            {
                if (presets[i] != null)
                    validPresets.Add(presets[i]);
            }
        }

        List<EncounterPreset> mechanicsTests = new List<EncounterPreset>();
        List<EncounterPreset> fullScenarios = new List<EncounterPreset>();

        for (int i = 0; i < validPresets.Count; i++)
        {
            EncounterPreset preset = validPresets[i];
            if (IsMechanicsPreset(preset))
                mechanicsTests.Add(preset);
            else
                fullScenarios.Add(preset);
        }

        if (mechanicsTests.Count > 0)
        {
            CreateCategoryHeader("MECHANICS TESTS");
            for (int i = 0; i < mechanicsTests.Count; i++)
                CreatePresetCard(mechanicsTests[i]);
        }

        if (fullScenarios.Count > 0)
        {
            CreateCategoryHeader("FULL SCENARIOS");
            for (int i = 0; i < fullScenarios.Count; i++)
                CreatePresetCard(fullScenarios[i]);
        }

        if (validPresets.Count == 0)
        {
            CreateCategoryHeader("NO ENCOUNTERS AVAILABLE");
            _selectionHintText.text = "No encounter presets were provided.";
            _selectedPresetId = null;
            if (_confirmButton != null)
                _confirmButton.interactable = false;
            return;
        }

        bool selectedStillExists = false;
        for (int i = 0; i < validPresets.Count; i++)
        {
            if (validPresets[i].Id == _selectedPresetId)
            {
                selectedStillExists = true;
                break;
            }
        }

        if (!selectedStillExists)
            _selectedPresetId = validPresets[0].Id;

        RefreshSelectionVisuals();
    }

    private bool IsMechanicsPreset(EncounterPreset preset)
    {
        if (preset == null) return false;

        string id = (preset.Id ?? string.Empty).ToLowerInvariant();
        string display = (preset.DisplayName ?? string.Empty).ToLowerInvariant();
        string desc = (preset.Description ?? string.Empty).ToLowerInvariant();

        return id.Contains("test") || display.Contains("test") || desc.Contains("validate") || desc.Contains("mechanic");
    }

    private void CreateCategoryHeader(string title)
    {
        GameObject header = new GameObject($"Category_{title}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        header.transform.SetParent(_contentContainer, false);

        Image headerImage = header.GetComponent<Image>();
        headerImage.color = new Color(0.12f, 0.13f, 0.17f, 0.92f);

        LayoutElement le = header.GetComponent<LayoutElement>();
        le.preferredHeight = 34f;
        le.minHeight = 34f;

        CreateText(
            header.transform,
            title,
            18,
            FontStyle.Bold,
            CategoryColor,
            new Vector2(0f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            TextAnchor.MiddleLeft,
            out Text categoryText
        );

        RectTransform textRect = categoryText.rectTransform;
        textRect.offsetMin = new Vector2(12f, -14f);
        textRect.offsetMax = new Vector2(-12f, 14f);
    }

    private void CreatePresetCard(EncounterPreset preset)
    {
        GameObject card = new GameObject($"Preset_{preset.Id}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(Outline));
        card.transform.SetParent(_contentContainer, false);

        Image cardImage = card.GetComponent<Image>();
        cardImage.color = CardColor;

        LayoutElement le = card.GetComponent<LayoutElement>();
        le.preferredHeight = 128f;
        le.minHeight = 128f;

        Outline outline = card.GetComponent<Outline>();
        outline.effectColor = new Color(0.95f, 0.87f, 0.45f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.enabled = false;

        Button button = card.GetComponent<Button>();
        button.targetGraphic = cardImage;
        button.transition = Selectable.Transition.ColorTint;

        ColorBlock colors = button.colors;
        colors.normalColor = CardColor;
        colors.highlightedColor = new Color(0.24f, 0.29f, 0.42f, 0.98f);
        colors.pressedColor = new Color(0.14f, 0.17f, 0.26f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.85f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        button.onClick.AddListener(() =>
        {
            _selectedPresetId = preset.Id;
            RefreshSelectionVisuals();
        });

        GameObject contentRoot = new GameObject("CardContent", typeof(RectTransform), typeof(VerticalLayoutGroup));
        contentRoot.transform.SetParent(card.transform, false);
        RectTransform contentRect = contentRoot.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = new Vector2(12f, 8f);
        contentRect.offsetMax = new Vector2(-12f, -8f);

        VerticalLayoutGroup contentLayout = contentRoot.GetComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 6f;
        contentLayout.padding = new RectOffset(0, 0, 0, 0);
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        CreateText(
            contentRoot.transform,
            preset.DisplayName,
            17,
            FontStyle.Bold,
            Color.white,
            new Vector2(0f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(0f, 30f),
            TextAnchor.MiddleLeft,
            out Text titleText
        );
        LayoutElement titleLayout = titleText.gameObject.AddComponent<LayoutElement>();
        titleLayout.preferredHeight = 30f;
        titleLayout.minHeight = 30f;
        titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        titleText.verticalOverflow = VerticalWrapMode.Truncate;

        CreateText(
            contentRoot.transform,
            preset.Description,
            13,
            FontStyle.Normal,
            new Color(0.85f, 0.88f, 0.94f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(0f, 52f),
            TextAnchor.UpperLeft,
            out Text descText
        );
        LayoutElement descLayout = descText.gameObject.AddComponent<LayoutElement>();
        descLayout.preferredHeight = 52f;
        descLayout.minHeight = 52f;
        descText.horizontalOverflow = HorizontalWrapMode.Wrap;
        descText.verticalOverflow = VerticalWrapMode.Truncate;

        EncounterDifficultySummary summary = ChallengeRatingUtils.CalculateEncounterDifficulty(preset, _partyAverageLevel);
        string summaryText = summary.CreatureCount > 0
            ? summary.BuildDisplayLine()
            : "CR/XP unavailable for one or more creatures in this preset";

        CreateText(
            contentRoot.transform,
            summaryText,
            12,
            FontStyle.Bold,
            new Color(0.97f, 0.86f, 0.53f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(0f, 22f),
            TextAnchor.MiddleLeft,
            out Text summaryLabel
        );
        LayoutElement summaryLayout = summaryLabel.gameObject.AddComponent<LayoutElement>();
        summaryLayout.preferredHeight = 22f;
        summaryLayout.minHeight = 22f;
        summaryLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        summaryLabel.verticalOverflow = VerticalWrapMode.Truncate;

        _cardImages[preset.Id] = cardImage;
        _cardOutlines[preset.Id] = outline;
    }

    private void RefreshSelectionVisuals()
    {
        foreach (KeyValuePair<string, Image> kv in _cardImages)
        {
            bool selected = kv.Key == _selectedPresetId;
            kv.Value.color = selected ? CardSelectedColor : CardColor;

            if (_cardOutlines.TryGetValue(kv.Key, out Outline outline))
                outline.enabled = selected;
        }

        bool hasSelection = !string.IsNullOrEmpty(_selectedPresetId);
        if (_confirmButton != null)
            _confirmButton.interactable = hasSelection;

        if (_selectionHintText != null)
            _selectionHintText.text = hasSelection ? $"Selected: {_selectedPresetId}" : "Select an encounter card to continue.";
    }

    private void OnConfirmPressed()
    {
        if (string.IsNullOrEmpty(_selectedPresetId))
            return;

        string selection = _selectedPresetId;
        Close();
        _onSelect?.Invoke(selection);
    }

    private void OnCancelPressed()
    {
        Close();
        _onCancel?.Invoke();
    }

    private void CreateText(
        Transform parent,
        string value,
        int fontSize,
        FontStyle style,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPos,
        Vector2 size,
        TextAnchor alignment,
        out Text text)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        text = go.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
    }
}
