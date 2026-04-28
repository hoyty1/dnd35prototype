using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modal used before combat to choose an encounter preset.
/// Now uses a categorized, scrollable, card-based layout.
/// </summary>
public class EncounterSelectionUI : MonoBehaviour
{
    private GameObject _panel;
    private Text _descriptionText;
    private Text _selectionHintText;
    private ScrollRect _scrollRect;
    private RectTransform _contentContainer;
    private Button _confirmButton;

    private Action<string> _onSelect;
    private Action _onCancel;
    private string _selectedPresetId;

    private readonly Dictionary<string, Image> _cardImages = new Dictionary<string, Image>();
    private readonly Dictionary<string, Outline> _cardOutlines = new Dictionary<string, Outline>();

    private static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.12f, 0.97f);
    private static readonly Color ScrollAreaColor = new Color(0.05f, 0.06f, 0.1f, 0.95f);
    private static readonly Color CardColor = new Color(0.17f, 0.2f, 0.3f, 0.96f);
    private static readonly Color CardSelectedColor = new Color(0.26f, 0.34f, 0.52f, 1f);
    private static readonly Color CategoryColor = new Color(0.98f, 0.83f, 0.35f, 1f);

    public bool IsOpen => _panel != null && _panel.activeSelf;

    public void Open(List<EncounterPreset> presets, Action<string> onSelect, Action onCancel = null)
    {
        EnsureBuilt();
        if (_panel == null) return;

        _onSelect = onSelect;
        _onCancel = onCancel;

        _panel.SetActive(true);
        _descriptionText.text = "Select an encounter. Scroll to view all mechanics tests and full scenarios.";

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

        CreateText(
            _panel.transform,
            "▲ Scroll Up",
            13,
            FontStyle.Bold,
            new Color(0.82f, 0.88f, 1f, 0.95f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -124f),
            new Vector2(220f, 20f),
            TextAnchor.MiddleCenter,
            out _
        );

        GameObject scrollRoot = new GameObject("ScrollRoot", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollRoot.transform.SetParent(_panel.transform, false);
        RectTransform scrollRootRect = scrollRoot.GetComponent<RectTransform>();
        scrollRootRect.anchorMin = new Vector2(0.05f, 0.18f);
        scrollRootRect.anchorMax = new Vector2(0.95f, 0.8f);
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
        le.preferredHeight = 104f;
        le.minHeight = 104f;

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
