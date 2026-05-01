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

        if (_root == null || _panel == null)
        {
            Debug.Log("[XP UI] Root/panel missing, calling BuildUI()");
            BuildUI();
        }
        else
        {
            Debug.Log("[XP UI] Root/panel already exists, reusing");
        }

        Debug.Log($"[XP UI] Root null after BuildUI: {_root == null}");
        Debug.Log($"[XP UI] Panel null after BuildUI: {_panel == null}");
        Debug.Log($"[XP UI] Root parent: {(_root != null ? _root.transform.parent?.name : "null")}");
        Debug.Log($"[XP UI] Panel parent: {(_panel != null ? _panel.transform.parent?.name : "null")}");
        Debug.Log($"[XP UI] Root active: {(_root != null && _root.activeSelf)}");
        Debug.Log($"[XP UI] Panel active: {(_panel != null && _panel.activeSelf)}");

        if (_root == null || _panel == null)
        {
            Debug.LogError("[XP UI] Root/panel was not built. Invoking callback to avoid flow lock.");
            _onComplete?.Invoke();
            return;
        }

        BuildContent(xpResult);

        _root.SetActive(true);
        _root.transform.SetAsLastSibling();
        _panel.SetActive(true);
        _panel.transform.SetAsLastSibling();

        RectTransform rootRect = _root.GetComponent<RectTransform>();
        RectTransform panelRect = _panel.GetComponent<RectTransform>();
        Debug.Log("[XP UI] Panel activated");
        Debug.Log($"[XP UI] Root position: {_root.transform.position}");
        Debug.Log($"[XP UI] Root rect: {(rootRect != null ? rootRect.rect.ToString() : "null")}");
        Debug.Log($"[XP UI] Panel position: {_panel.transform.position}");
        Debug.Log($"[XP UI] Panel rect: {(panelRect != null ? panelRect.rect.ToString() : "null")}");

        LogUIHierarchy();
    }

    private void OnContinueClicked()
    {
        Debug.Log("[XP UI] Continue clicked");

        if (_root != null)
            _root.SetActive(false);

        Debug.Log("[XP UI] Invoking completion callback");
        _onComplete?.Invoke();
        Debug.Log("[XP UI] Callback invoked");
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

        Debug.Log($"[XP UI] Using Canvas: {canvas.name}");
        Debug.Log($"[XP UI] Canvas activeInHierarchy: {canvas.gameObject.activeInHierarchy}");
        Debug.Log($"[XP UI] Canvas enabled: {canvas.enabled}");
        Debug.Log($"[XP UI] Canvas renderMode: {canvas.renderMode}");
        Debug.Log($"[XP UI] Canvas sortingLayerID: {canvas.sortingLayerID}, sortingOrder: {canvas.sortingOrder}");
        Debug.Log($"[XP UI] Canvas camera: {(canvas.worldCamera != null ? canvas.worldCamera.name : "null")}");

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

        _panel = CreateChild("CombatEndXPPanel", _root.transform, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));

        RectTransform panelRect = _panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.18f, 0.1f);
        panelRect.anchorMax = new Vector2(0.82f, 0.9f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelRect.localScale = Vector3.one;

        Image panelBg = _panel.GetComponent<Image>();
        panelBg.color = new Color(0.08f, 0.11f, 0.18f, 0.97f);

        VerticalLayoutGroup layout = _panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 18, 18);
        layout.spacing = 10f;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;

        ContentSizeFitter fitter = _panel.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        _root.transform.SetAsLastSibling();
        _panel.transform.SetAsLastSibling();
        _root.SetActive(false);

        Debug.Log("[XP UI] BuildUI complete");
        Debug.Log($"[XP UI] Root: {_root.name}, Parent: {_root.transform.parent?.name}");
        Debug.Log($"[XP UI] Panel: {_panel.name}, Parent: {_panel.transform.parent?.name}");
        Debug.Log($"[XP UI] Root Rect: {overlayRect.rect}");
        Debug.Log($"[XP UI] Panel Rect: {panelRect.rect}");
        Debug.Log($"[XP UI] Root Position: {overlayRect.position}");
        Debug.Log($"[XP UI] Panel Position: {panelRect.position}");
    }

    private void BuildContent(ExperienceCalculator.CombatXPResult xpResult)
    {
        if (_panel == null)
        {
            Debug.LogError("[XP UI] BuildContent called with null panel.");
            return;
        }

        for (int i = _panel.transform.childCount - 1; i >= 0; i--)
            Destroy(_panel.transform.GetChild(i).gameObject);

        CreateText(_panel.transform, "EXPERIENCE GAINED", 28, FontStyle.Bold, new Color(1f, 0.92f, 0.45f));
        CreateText(_panel.transform, $"Average Party Level: {xpResult.AveragePartyLevel:F1}", 18, FontStyle.Normal, Color.white);

        CreateText(_panel.transform, "Enemies Defeated:", 20, FontStyle.Bold, new Color(0.72f, 0.88f, 1f));
        if (xpResult.Awards.Count == 0)
        {
            CreateText(_panel.transform, "• None", 16, FontStyle.Italic, new Color(0.8f, 0.8f, 0.8f));
        }
        else
        {
            for (int i = 0; i < xpResult.Awards.Count; i++)
            {
                ExperienceCalculator.XPAward award = xpResult.Awards[i];
                string line = $"• {award.EnemyName} (CR {ChallengeRatingUtils.Format(award.ChallengeRating)}): {award.XPTotal} XP";
                CreateText(_panel.transform, line, 16, FontStyle.Normal, Color.white);
            }
        }

        CreateSeparator(_panel.transform);

        int totalEncounterXP = xpResult.Awards.Sum(a => a.XPTotal);
        int partySize = Mathf.Max(1, xpResult.CharacterXPGained.Count);
        CreateText(_panel.transform, $"Total Encounter XP: {totalEncounterXP}", 19, FontStyle.Bold, new Color(1f, 0.95f, 0.35f));
        CreateText(_panel.transform, $"Party Size: {partySize}", 17, FontStyle.Normal, Color.white);
        CreateText(_panel.transform, $"XP per Character: {totalEncounterXP} / {partySize} = {xpResult.TotalXPPerCharacter}", 21, FontStyle.Bold, Color.yellow);

        CreateText(_panel.transform, "Character XP:", 20, FontStyle.Bold, new Color(0.72f, 1f, 0.74f));
        if (xpResult.CharacterXPGained.Count == 0)
        {
            CreateText(_panel.transform, "• None", 16, FontStyle.Italic, new Color(0.8f, 0.8f, 0.8f));
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
                CreateText(_panel.transform, $"• {charName}: +{gained} XP{levelUpTag}", 16, FontStyle.Normal, textColor);
            }
        }

        GameObject buttonObj = CreateChild("ContinueButton", _panel.transform, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform btnRect = buttonObj.GetComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(0f, 48f);

        Image btnBg = buttonObj.GetComponent<Image>();
        btnBg.color = new Color(0.16f, 0.35f, 0.74f, 1f);

        Button button = buttonObj.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = button.colors;
        cb.highlightedColor = new Color(0.22f, 0.45f, 0.9f, 1f);
        cb.pressedColor = new Color(0.1f, 0.24f, 0.52f, 1f);
        button.colors = cb;

        CreateText(buttonObj.transform, "Continue", 20, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnContinueClicked);
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
        Debug.Log($"[XP UI] Root ActiveInHierarchy: {(_root != null && _root.activeInHierarchy)}");
        Debug.Log($"[XP UI] Root Parent: {(_root != null ? _root.transform.parent?.name : "null")}");
        Debug.Log($"[XP UI] Panel: {_panel.name}");
        Debug.Log($"[XP UI] Panel Active: {_panel.activeSelf}");
        Debug.Log($"[XP UI] Panel ActiveInHierarchy: {_panel.activeInHierarchy}");
        Debug.Log($"[XP UI] Panel Parent: {_panel.transform.parent?.name}");
        Debug.Log($"[XP UI] Panel Position: {_panel.transform.position}");
        Debug.Log($"[XP UI] Panel Scale: {_panel.transform.localScale}");

        RectTransform panelRect = _panel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            Debug.Log($"[XP UI] Panel Rect: {panelRect.rect}");
            Debug.Log($"[XP UI] Panel AnchoredPosition: {panelRect.anchoredPosition}");
            Debug.Log($"[XP UI] Panel SizeDelta: {panelRect.sizeDelta}");
        }

        Image image = _panel.GetComponent<Image>();
        if (image != null)
            Debug.Log($"[XP UI] Panel Background Color: {image.color}");

        Debug.Log($"[XP UI] Root child count: {(_root != null ? _root.transform.childCount : 0)}");
        Debug.Log($"[XP UI] Panel child count: {_panel.transform.childCount}");
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
        GameObject textObj = CreateChild("Text", parent, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = textObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 30f);

        TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
        text.text = message;
        text.fontSize = fontSize;
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
        GameObject sepObj = CreateChild("Separator", parent, typeof(RectTransform), typeof(Image));
        RectTransform rect = sepObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 2f);

        Image image = sepObj.GetComponent<Image>();
        image.color = new Color(0.55f, 0.62f, 0.82f, 0.95f);
    }
}
