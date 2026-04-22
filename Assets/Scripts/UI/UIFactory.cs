using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Centralized factory for creating UI elements with consistent styling.
/// </summary>
public static class UIFactory
{
    // === CORE HELPERS ===

    public static Font GetDefaultFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", UITheme.FontSizeNormal);
        return font;
    }

    public static RectTransform SetRectTransform(
        RectTransform rt,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;
        return rt;
    }

    // === BUTTON CREATION ===

    public static Button CreateButton(
        Transform parent,
        string label,
        UnityAction onClick = null,
        Vector2? size = null,
        Color? color = null,
        string name = null,
        Font font = null,
        int? fontSize = null,
        Color? textColor = null)
    {
        GameObject buttonObj = new GameObject(string.IsNullOrEmpty(name) ? label + "_Button" : name);
        buttonObj.transform.SetParent(parent, false);

        RectTransform rectTransform = buttonObj.AddComponent<RectTransform>();
        rectTransform.sizeDelta = size ?? UITheme.ButtonSizeNormal;

        Image image = buttonObj.AddComponent<Image>();
        Color baseColor = color ?? UITheme.ButtonNormal;
        image.color = baseColor;

        Button button = buttonObj.AddComponent<Button>();

        ColorBlock colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.15f);
        colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.2f);
        colors.disabledColor = UITheme.ButtonDisabled;
        button.colors = colors;

        Text text = CreateLabel(
            buttonObj.transform,
            label,
            fontSize ?? UITheme.FontSizeNormal,
            TextAnchor.MiddleCenter,
            textColor ?? UITheme.TextNormal,
            "Text",
            font ?? GetDefaultFont());

        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }

        return button;
    }

    public static ColorBlock GetEnhancedCombatButtonColors(Color baseColor)
    {
        Color brightHover = Color.Lerp(baseColor, new Color(1f, 0.92f, 0.35f, 1f), 0.8f);

        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = baseColor;
        colors.highlightedColor = brightHover;
        colors.selectedColor = brightHover;
        colors.pressedColor = Color.Lerp(brightHover, Color.black, 0.3f);
        colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.65f);
        colors.colorMultiplier = 1.55f;
        colors.fadeDuration = 0.05f;
        return colors;
    }

    public static void ApplyEnhancedCombatButtonStyle(Button button, Color baseColor, bool addHoverScale = true)
    {
        if (button == null)
            return;

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = baseColor;
            button.targetGraphic = image;
        }

        button.colors = GetEnhancedCombatButtonColors(baseColor);

        if (addHoverScale)
            EnsureHoverScaleEventTriggers(button.gameObject);
    }

    private static void EnsureHoverScaleEventTriggers(GameObject buttonObj)
    {
        if (buttonObj == null)
            return;

        EventTrigger trigger = buttonObj.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = buttonObj.AddComponent<EventTrigger>();

        if (trigger.triggers == null)
            trigger.triggers = new System.Collections.Generic.List<EventTrigger.Entry>();

        AddHoverScaleEventTriggerEntry(trigger, EventTriggerType.PointerEnter, () => buttonObj.transform.localScale = new Vector3(1.08f, 1.08f, 1f));
        AddHoverScaleEventTriggerEntry(trigger, EventTriggerType.PointerExit, () => buttonObj.transform.localScale = Vector3.one);
        AddHoverScaleEventTriggerEntry(trigger, EventTriggerType.PointerDown, () => buttonObj.transform.localScale = new Vector3(0.97f, 0.97f, 1f));
        AddHoverScaleEventTriggerEntry(trigger, EventTriggerType.PointerUp, () => buttonObj.transform.localScale = new Vector3(1.08f, 1.08f, 1f));
    }

    private static void AddHoverScaleEventTriggerEntry(EventTrigger trigger, EventTriggerType eventType, UnityAction action)
    {
        if (trigger == null || action == null)
            return;

        if (trigger.triggers != null)
        {
            for (int i = 0; i < trigger.triggers.Count; i++)
            {
                if (trigger.triggers[i] != null && trigger.triggers[i].eventID == eventType)
                    return;
            }
        }

        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    public static Button CreateSpecialButton(
        Transform parent,
        string label,
        UnityAction onClick = null,
        Vector2? size = null,
        string name = null)
    {
        return CreateButton(parent, label, onClick, size, UITheme.ButtonSpecial, name);
    }

    public static Button CreateDangerButton(
        Transform parent,
        string label,
        UnityAction onClick = null,
        Vector2? size = null,
        string name = null)
    {
        return CreateButton(parent, label, onClick, size, UITheme.ButtonDanger, name);
    }

    public static Button CreateSuccessButton(
        Transform parent,
        string label,
        UnityAction onClick = null,
        Vector2? size = null,
        string name = null)
    {
        return CreateButton(parent, label, onClick, size, UITheme.ButtonSuccess, name);
    }

    // === TEXT CREATION ===

    public static Text CreateLabel(
        Transform parent,
        string text,
        int? fontSize = null,
        TextAnchor? alignment = null,
        Color? color = null,
        string name = "Label",
        Font font = null)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);

        Text textComponent = textObj.AddComponent<Text>();
        textComponent.text = text;
        textComponent.font = font ?? GetDefaultFont();
        textComponent.fontSize = fontSize ?? UITheme.FontSizeNormal;
        textComponent.color = color ?? UITheme.TextNormal;
        textComponent.alignment = alignment ?? TextAnchor.MiddleLeft;

        return textComponent;
    }

    public static Text CreateHeader(
        Transform parent,
        string text,
        Color? color = null,
        string name = "Header",
        Font font = null)
    {
        Text label = CreateLabel(parent, text, UITheme.FontSizeHeader, TextAnchor.MiddleCenter, color, name, font);
        label.fontStyle = FontStyle.Bold;
        return label;
    }

    public static Text CreateTitle(
        Transform parent,
        string text,
        Color? color = null,
        string name = "Title",
        Font font = null)
    {
        Text label = CreateLabel(parent, text, UITheme.FontSizeTitle, TextAnchor.MiddleCenter, color, name, font);
        label.fontStyle = FontStyle.Bold;
        return label;
    }

    // === PANEL CREATION ===

    public static GameObject CreatePanel(
        Transform parent,
        string name,
        Color? backgroundColor = null)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);

        panel.AddComponent<RectTransform>();

        Image image = panel.AddComponent<Image>();
        image.color = backgroundColor ?? UITheme.PanelBackground;

        return panel;
    }

    public static ScrollRect CreateScrollPanel(
        Transform parent,
        string name,
        bool vertical = true,
        bool horizontal = false)
    {
        GameObject scrollPanel = CreatePanel(parent, name);

        ScrollRect scrollRect = scrollPanel.AddComponent<ScrollRect>();
        scrollRect.vertical = vertical;
        scrollRect.horizontal = horizontal;

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollPanel.transform, false);

        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0f);

        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);

        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;

        return scrollRect;
    }

    // === INPUT FIELD CREATION ===

    public static InputField CreateInputField(
        Transform parent,
        string placeholder = "",
        UnityAction<string> onValueChanged = null,
        Font font = null)
    {
        Font resolvedFont = font ?? GetDefaultFont();

        GameObject inputObj = new GameObject("InputField");
        inputObj.transform.SetParent(parent, false);

        RectTransform rectTransform = inputObj.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(200, 30);

        Image image = inputObj.AddComponent<Image>();
        image.color = Color.white;

        InputField inputField = inputObj.AddComponent<InputField>();

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(inputObj.transform, false);

        Text text = textObj.AddComponent<Text>();
        text.font = resolvedFont;
        text.fontSize = UITheme.FontSizeNormal;
        text.color = Color.black;
        text.supportRichText = false;

        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(5, 2);
        textRect.offsetMax = new Vector2(-5, -2);

        GameObject placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(inputObj.transform, false);

        Text placeholderText = placeholderObj.AddComponent<Text>();
        placeholderText.text = placeholder;
        placeholderText.font = resolvedFont;
        placeholderText.fontSize = UITheme.FontSizeNormal;
        placeholderText.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        placeholderText.fontStyle = FontStyle.Italic;

        RectTransform placeholderRect = placeholderText.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(5, 2);
        placeholderRect.offsetMax = new Vector2(-5, -2);

        inputField.textComponent = text;
        inputField.placeholder = placeholderText;

        if (onValueChanged != null)
        {
            inputField.onValueChanged.AddListener(onValueChanged);
        }

        return inputField;
    }

    // === LAYOUT HELPERS ===

    public static VerticalLayoutGroup AddVerticalLayout(
        GameObject target,
        float spacing = -1,
        TextAnchor childAlignment = TextAnchor.UpperLeft,
        RectOffset padding = null)
    {
        VerticalLayoutGroup layout = target.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing >= 0 ? spacing : UITheme.SpacingNormal;
        layout.childAlignment = childAlignment;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        if (padding != null)
        {
            layout.padding = padding;
        }

        return layout;
    }

    public static HorizontalLayoutGroup AddHorizontalLayout(
        GameObject target,
        float spacing = -1,
        TextAnchor childAlignment = TextAnchor.MiddleLeft,
        RectOffset padding = null)
    {
        HorizontalLayoutGroup layout = target.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing >= 0 ? spacing : UITheme.SpacingNormal;
        layout.childAlignment = childAlignment;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        if (padding != null)
        {
            layout.padding = padding;
        }

        return layout;
    }

    public static GridLayoutGroup AddGridLayout(
        GameObject target,
        Vector2 cellSize,
        Vector2 spacing)
    {
        GridLayoutGroup layout = target.AddComponent<GridLayoutGroup>();
        layout.cellSize = cellSize;
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.UpperLeft;

        return layout;
    }
}
