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
            onComplete?.Invoke();
            return;
        }

        _onComplete = onComplete;

        EnsureBuilt();
        if (_root == null)
        {
            _onComplete?.Invoke();
            return;
        }

        BuildContent(xpResult);
        _root.SetActive(true);
        _root.transform.SetAsLastSibling();
        Debug.Log("[XP UI] Panel activated");
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

    private void EnsureBuilt()
    {
        if (_root != null)
            return;

        _root = new GameObject("XPAwardRoot", typeof(RectTransform), typeof(Image));
        _root.transform.SetParent(transform, false);

        RectTransform overlayRect = _root.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlay = _root.GetComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.72f);
    }

    private void BuildContent(ExperienceCalculator.CombatXPResult xpResult)
    {
        for (int i = _root.transform.childCount - 1; i >= 0; i--)
            Destroy(_root.transform.GetChild(i).gameObject);

        _panel = CreateChild("Panel", _root.transform, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform panelRect = _panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.18f, 0.1f);
        panelRect.anchorMax = new Vector2(0.82f, 0.9f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelBg = _panel.GetComponent<Image>();
        panelBg.color = new Color(0.08f, 0.11f, 0.18f, 0.97f);

        VerticalLayoutGroup layout = _panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 18, 18);
        layout.spacing = 10f;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;

        ContentSizeFitter fitter = _panel.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

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
