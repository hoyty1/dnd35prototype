using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Post-combat modal panel showing XP awards and level-up highlights.
/// </summary>
public class CombatEndXPUI : MonoBehaviour
{
    private GameObject _root;
    private GameObject _panel;
    private RectTransform _contentParent;
    private Button _continueButton;
    private Action _onComplete;

    public void ShowXPAwards(ExperienceCalculator.CombatXPResult xpResult, Action onComplete = null)
    {
        Debug.Log("[XP UI] ShowXPAwards called");
        Debug.Log($"[XP UI] XP Result: {(xpResult != null ? xpResult.TotalXPPerCharacter : 0)} XP per character");
        Debug.Log($"[XP UI] Callback is null: {onComplete == null}");

        if (xpResult == null)
        {
            Debug.LogWarning("[XP UI] XP result is null. Invoking callback immediately.");
            onComplete?.Invoke();
            return;
        }

        _onComplete = onComplete;

        if (_root == null || _panel == null || _contentParent == null || _continueButton == null)
        {
            Debug.Log("[XP UI] UI references missing, calling BuildUI()");
            BuildUI();
        }
        else
        {
            Debug.Log("[XP UI] Reusing existing XP UI");
        }

        if (_root == null || _panel == null || _contentParent == null || _continueButton == null)
        {
            Debug.LogError("[XP UI] UI build failed. Invoking callback to avoid flow lock.");
            _onComplete?.Invoke();
            return;
        }

        BuildContent(xpResult);

        _root.SetActive(true);
        _root.transform.SetAsLastSibling();
        _panel.SetActive(true);
        _panel.transform.SetAsLastSibling();

        Debug.Log("[XP UI] Panel activated");
        LogUIHierarchy();
    }

    private void OnContinueClicked()
    {
        Debug.Log("[XP UI] Continue button clicked");

        if (_panel == null)
        {
            Debug.LogError("[XP UI] Panel is null in OnContinueClicked!");
            return;
        }

        if (_root != null)
        {
            Debug.Log("[XP UI] Deactivating root overlay");
            _root.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[XP UI] Root overlay is null in OnContinueClicked");
        }

        if (_onComplete == null)
        {
            Debug.LogWarning("[XP UI] onComplete callback is null!");
            return;
        }

        Debug.Log("[XP UI] Invoking completion callback");
        _onComplete.Invoke();
        Debug.Log("[XP UI] Callback invoked successfully");
    }

    private void BuildUI()
    {
        Debug.Log("[XP UI] BuildUI started");

        if (_root != null)
        {
            Debug.Log("[XP UI] Existing root found, destroying before rebuild");
            Destroy(_root);
            _root = null;
            _panel = null;
            _contentParent = null;
            _continueButton = null;
        }

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[XP UI] No Canvas found via FindObjectOfType. Attempting parent traversal.");
            Transform canvasTransform = transform.parent;
            while (canvasTransform != null)
            {
                Canvas parentCanvas = canvasTransform.GetComponent<Canvas>();
                if (parentCanvas != null)
                {
                    canvas = parentCanvas;
                    Debug.Log($"[XP UI] Found Canvas via parent traversal: {canvas.name}");
                    break;
                }

                canvasTransform = canvasTransform.parent;
            }
        }

        if (canvas == null)
        {
            Debug.LogError("[XP UI] FATAL: No Canvas found in scene. Cannot build XP UI.");
            return;
        }

        _root = new GameObject("CombatEndXPOverlay", typeof(RectTransform), typeof(Image));
        _root.transform.SetParent(canvas.transform, false);

        RectTransform overlayRect = _root.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayRect.localScale = Vector3.one;

        Image overlay = _root.GetComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.72f);
        overlay.raycastTarget = true;

        _panel = CreateChild("CombatEndXPPanel", _root.transform, typeof(RectTransform), typeof(Image));

        RectTransform panelRect = _panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelRect.localScale = Vector3.one;

        Debug.Log("[XP UI] Panel: (0,0) to (1,1) - FULLSCREEN");

        Image panelBg = _panel.GetComponent<Image>();
        panelBg.color = new Color(0.08f, 0.11f, 0.18f, 0.97f);

        CreateScrollableContent();
        CreateContinueButton();

        _root.transform.SetAsLastSibling();
        _panel.transform.SetAsLastSibling();
        _root.SetActive(false);

        Debug.Log("[XP UI] BuildUI complete");
        LogUIHierarchy();
    }

    private void CreateScrollableContent()
    {
        if (_panel == null)
        {
            Debug.LogError("[XP UI] Cannot create scroll content; panel is null.");
            return;
        }

        GameObject scrollArea = CreateChild("ContentScrollArea", _panel.transform,
            typeof(RectTransform), typeof(Image), typeof(ScrollRect));

        RectTransform scrollRect = scrollArea.GetComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0f, 0f);
        scrollRect.anchorMax = new Vector2(1f, 1f);
        scrollRect.offsetMin = new Vector2(36f, 126f); // leave room for footer button
        scrollRect.offsetMax = new Vector2(-36f, -30f);

        Image scrollBg = scrollArea.GetComponent<Image>();
        scrollBg.color = new Color(0f, 0f, 0f, 0.15f);
        scrollBg.raycastTarget = true;

        GameObject viewport = CreateChild("Viewport", scrollArea.transform,
            typeof(RectTransform), typeof(Image), typeof(Mask));

        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.02f);
        viewportImage.raycastTarget = true;

        Mask viewportMask = viewport.GetComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        GameObject content = CreateChild("Content", viewport.transform,
            typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));

        _contentParent = content.GetComponent<RectTransform>();
        _contentParent.anchorMin = new Vector2(0f, 1f);
        _contentParent.anchorMax = new Vector2(1f, 1f);
        _contentParent.pivot = new Vector2(0.5f, 1f);
        _contentParent.anchoredPosition = Vector2.zero;
        _contentParent.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 8f;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect sr = scrollArea.GetComponent<ScrollRect>();
        sr.viewport = viewportRect;
        sr.content = _contentParent;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 40f;

        Debug.Log("[XP UI] Scroll content adjusted to make room for button");
    }

    private void CreateContinueButton()
    {
        Debug.Log("[XP UI] === CREATING CONTINUE BUTTON ===");

        if (_panel == null)
        {
            Debug.LogError("[XP UI] Panel is null while creating Continue button.");
            return;
        }

        GameObject buttonContainer = CreateChild("ButtonContainer", _panel.transform,
            typeof(RectTransform), typeof(Image));

        RectTransform containerRect = buttonContainer.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0f, 0f);
        containerRect.anchorMax = new Vector2(1f, 0f);
        containerRect.pivot = new Vector2(0.5f, 0f);
        containerRect.anchoredPosition = new Vector2(0f, 12f);
        containerRect.sizeDelta = new Vector2(-80f, 64f);

        Image footerBg = buttonContainer.GetComponent<Image>();
        footerBg.color = new Color(0.05f, 0.07f, 0.12f, 0.9f);
        footerBg.raycastTarget = true;

        GameObject buttonObj = CreateChild("ContinueButton", buttonContainer.transform,
            typeof(RectTransform), typeof(Image), typeof(Button));

        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = Vector2.zero;
        buttonRect.sizeDelta = new Vector2(150f, 40f);

        Image buttonBg = buttonObj.GetComponent<Image>();
        buttonBg.color = new Color(0.2f, 0.5f, 0.8f, 1f);
        buttonBg.raycastTarget = true;

        _continueButton = buttonObj.GetComponent<Button>();
        _continueButton.targetGraphic = buttonBg;
        _continueButton.transition = Selectable.Transition.ColorTint;

        ColorBlock colors = _continueButton.colors;
        colors.normalColor = new Color(0.2f, 0.5f, 0.8f, 1f);
        colors.highlightedColor = new Color(0.3f, 0.6f, 0.9f, 1f);
        colors.pressedColor = new Color(0.15f, 0.4f, 0.7f, 1f);
        colors.selectedColor = new Color(0.25f, 0.55f, 0.85f, 1f);
        _continueButton.colors = colors;

        _continueButton.onClick.RemoveAllListeners();
        _continueButton.onClick.AddListener(OnContinueClicked);

        GameObject textObj = CreateChild("Text", buttonObj.transform, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI buttonText = textObj.GetComponent<TextMeshProUGUI>();
        buttonText.text = "Continue";
        buttonText.fontSize = 16;
        buttonText.fontStyle = FontStyles.Bold;
        buttonText.color = Color.white;
        buttonText.alignment = TextAlignmentOptions.Center;

        Debug.Log($"[XP UI] Button GameObject: {buttonObj.name}");
        Debug.Log($"[XP UI] Button Parent: {(buttonObj.transform.parent != null ? buttonObj.transform.parent.name : "null")}");
        Debug.Log($"[XP UI] Button Active: {buttonObj.activeSelf}");
        Debug.Log($"[XP UI] Button Rect Size: {buttonRect.rect.size}");
        Debug.Log($"[XP UI] Button Position: {buttonRect.position}");
        Debug.Log($"[XP UI] Button component: {_continueButton != null}");
        Debug.Log($"[XP UI] Button interactable: {_continueButton != null && _continueButton.interactable}");
        Debug.Log("[XP UI] === BUTTON CREATION COMPLETE ===");
    }

    private void BuildContent(ExperienceCalculator.CombatXPResult xpResult)
    {
        Debug.Log("[XP UI] BuildContent started");

        if (_contentParent == null)
        {
            Debug.LogError("[XP UI] BuildContent called with null content parent.");
            return;
        }

        for (int i = _contentParent.childCount - 1; i >= 0; i--)
            Destroy(_contentParent.GetChild(i).gameObject);

        CreateText(_contentParent, "EXPERIENCE GAINED", 28, FontStyle.Bold, new Color(1f, 0.92f, 0.45f));
        CreateText(_contentParent, $"Average Party Level: {xpResult.AveragePartyLevel:F1}", 18, FontStyle.Normal, Color.white);

        CreateText(_contentParent, "Enemies Defeated:", 20, FontStyle.Bold, new Color(0.72f, 0.88f, 1f));
        if (xpResult.Awards.Count == 0)
        {
            CreateText(_contentParent, "• None", 16, FontStyle.Italic, new Color(0.8f, 0.8f, 0.8f));
        }
        else
        {
            for (int i = 0; i < xpResult.Awards.Count; i++)
            {
                ExperienceCalculator.XPAward award = xpResult.Awards[i];
                string line = $"• {award.EnemyName} (CR {ChallengeRatingUtils.Format(award.ChallengeRating)}): {award.XPTotal} XP";
                CreateText(_contentParent, line, 16, FontStyle.Normal, Color.white);
            }
        }

        CreateSeparator(_contentParent);

        int totalEncounterXP = xpResult.Awards.Sum(a => a.XPTotal);
        int partySize = Mathf.Max(1, xpResult.CharacterXPGained.Count);
        CreateText(_contentParent, $"Total Encounter XP: {totalEncounterXP}", 19, FontStyle.Bold, new Color(1f, 0.95f, 0.35f));
        CreateText(_contentParent, $"Party Size: {partySize}", 17, FontStyle.Normal, Color.white);
        CreateText(_contentParent, $"XP per Character: {totalEncounterXP} / {partySize} = {xpResult.TotalXPPerCharacter}", 21, FontStyle.Bold, Color.yellow);

        CreateText(_contentParent, "Character XP:", 20, FontStyle.Bold, new Color(0.72f, 1f, 0.74f));
        if (xpResult.CharacterXPGained.Count == 0)
        {
            CreateText(_contentParent, "• None", 16, FontStyle.Italic, new Color(0.8f, 0.8f, 0.8f));
        }
        else
        {
            foreach (KeyValuePair<CharacterController, int> kvp in xpResult.CharacterXPGained)
            {
                CharacterController character = kvp.Key;
                int gained = kvp.Value;
                bool leveledUp = xpResult.CharacterLeveledUp.ContainsKey(character) && xpResult.CharacterLeveledUp[character];

                string charName = character != null && character.Stats != null ? character.Stats.CharacterName : "Unknown";
                string levelUpTag = leveledUp ? " ⭐ LEVEL UP! ⭐" : string.Empty;
                Color textColor = leveledUp ? new Color(0.58f, 1f, 0.58f) : Color.white;
                CreateText(_contentParent, $"• {charName}: +{gained} XP{levelUpTag}", 16, FontStyle.Normal, textColor);
            }
        }

        if (_continueButton == null)
        {
            Debug.LogWarning("[XP UI] Continue button missing during BuildContent; rebuilding button.");
            CreateContinueButton();
        }
        else
        {
            _continueButton.onClick.RemoveAllListeners();
            _continueButton.onClick.AddListener(OnContinueClicked);
            _continueButton.interactable = true;
        }

        Debug.Log("[XP UI] BuildContent complete");
    }

    private void LogUIHierarchy()
    {
        if (_panel == null)
        {
            Debug.LogError("[XP UI] Panel is null!");
            return;
        }

        Debug.Log("[XP UI] === UI HIERARCHY ===");
        Debug.Log($"[XP UI] Root: {(_root != null ? _root.name : "null")}");
        Debug.Log($"[XP UI] Root Active: {(_root != null && _root.activeSelf)}");
        Debug.Log($"[XP UI] Panel: {_panel.name}");
        Debug.Log($"[XP UI] Panel Active: {_panel.activeSelf}");
        Debug.Log($"[XP UI] Panel child count: {_panel.transform.childCount}");
        Debug.Log($"[XP UI] Content child count: {(_contentParent != null ? _contentParent.childCount : 0)}");
        Debug.Log($"[XP UI] Continue button present: {_continueButton != null}");
        Debug.Log("[XP UI] ==================");
    }

    private static GameObject CreateChild(string name, Transform parent, params Type[] components)
    {
        GameObject go = new GameObject(name, components);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject CreateText(Transform parent, string message, int fontSize, FontStyle style, Color color, TextAnchor align = TextAnchor.MiddleLeft)
    {
        GameObject textObj = CreateChild("Text", parent, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        RectTransform rect = textObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 38f);

        LayoutElement layoutElement = textObj.GetComponent<LayoutElement>();
        layoutElement.minHeight = 38f;
        layoutElement.flexibleHeight = 0f;

        TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
        text.text = message;
        text.fontSize = fontSize + 2;
        text.fontStyle = ConvertFontStyle(style);
        text.color = color;
        text.alignment = ConvertAlignment(align);
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Truncate;

        return textObj;
    }

    private static FontStyles ConvertFontStyle(FontStyle style)
    {
        switch (style)
        {
            case FontStyle.Bold:
                return FontStyles.Bold;
            case FontStyle.Italic:
                return FontStyles.Italic;
            case FontStyle.BoldAndItalic:
                return FontStyles.Bold | FontStyles.Italic;
            default:
                return FontStyles.Normal;
        }
    }

    private static TextAlignmentOptions ConvertAlignment(TextAnchor anchor)
    {
        switch (anchor)
        {
            case TextAnchor.UpperLeft:
                return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter:
                return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight:
                return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft:
                return TextAlignmentOptions.MidlineLeft;
            case TextAnchor.MiddleCenter:
                return TextAlignmentOptions.Center;
            case TextAnchor.MiddleRight:
                return TextAlignmentOptions.MidlineRight;
            case TextAnchor.LowerLeft:
                return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter:
                return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight:
                return TextAlignmentOptions.BottomRight;
            default:
                return TextAlignmentOptions.Center;
        }
    }

    private static void CreateSeparator(Transform parent)
    {
        GameObject sepObj = CreateChild("Separator", parent, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        RectTransform rect = sepObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 2f);

        LayoutElement layoutElement = sepObj.GetComponent<LayoutElement>();
        layoutElement.minHeight = 2f;
        layoutElement.preferredHeight = 2f;

        Image image = sepObj.GetComponent<Image>();
        image.color = new Color(0.55f, 0.62f, 0.82f, 0.95f);
    }
}
