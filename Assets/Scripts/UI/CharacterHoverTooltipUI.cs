using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight screen-space tooltip for battlefield character hover details.
/// Displays name, class/race, current HP, and active statuses.
/// </summary>
public class CharacterHoverTooltipUI : MonoBehaviour
{
    public static CharacterHoverTooltipUI Instance { get; private set; }

    private RectTransform _panel;
    private Text _text;
    private Canvas _canvas;
    private int _lastShowFrame = -1;

    public static void EnsureInstance()
    {
        if (Instance != null)
            return;

        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("CharacterTooltipCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        GameObject go = new GameObject("CharacterHoverTooltipUI");
        go.transform.SetParent(canvas.transform, false);
        CharacterHoverTooltipUI tooltip = go.AddComponent<CharacterHoverTooltipUI>();
        tooltip.Initialize(canvas);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Initialize(Canvas canvas)
    {
        _canvas = canvas;

        GameObject panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(transform, false);
        _panel = panelGO.AddComponent<RectTransform>();
        Image panelImage = panelGO.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.86f);

        _panel.pivot = new Vector2(0f, 1f);
        _panel.anchorMin = new Vector2(0f, 1f);
        _panel.anchorMax = new Vector2(0f, 1f);
        _panel.sizeDelta = new Vector2(260f, 100f);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(panelGO.transform, false);
        RectTransform textRT = textGO.AddComponent<RectTransform>();
        _text = textGO.AddComponent<Text>();
        _text.font = LoadBuiltinTooltipFont();
        _text.fontSize = 14;
        _text.color = new Color(1f, 0.95f, 0.8f, 1f);
        _text.alignment = TextAnchor.UpperLeft;
        _text.horizontalOverflow = HorizontalWrapMode.Wrap;
        _text.verticalOverflow = VerticalWrapMode.Overflow;

        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(8f, 8f);
        textRT.offsetMax = new Vector2(-8f, -8f);

        HideTooltip();
    }

    private void LateUpdate()
    {
        if (_panel != null && _panel.gameObject.activeSelf && _lastShowFrame != Time.frameCount)
            _panel.gameObject.SetActive(false);
    }

    public void ShowTooltip(CharacterController character, Vector2 screenPosition)
    {
        if (_panel == null || _text == null || _canvas == null || character == null || character.Stats == null)
            return;

        _text.text = BuildTooltipText(character);

        float width = Mathf.Clamp(_text.preferredWidth + 20f, 180f, 380f);
        float height = Mathf.Clamp(_text.preferredHeight + 16f, 64f, 280f);
        _panel.sizeDelta = new Vector2(width, height);

        RectTransform canvasRT = _canvas.transform as RectTransform;
        Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screenPosition, uiCamera, out Vector2 localPos))
            _panel.anchoredPosition = new Vector2(localPos.x + 16f, localPos.y - 16f);

        _lastShowFrame = Time.frameCount;
        _panel.gameObject.SetActive(true);
    }

    public void HideTooltip()
    {
        if (_panel != null)
            _panel.gameObject.SetActive(false);
    }

    private static string BuildTooltipText(CharacterController character)
    {
        CharacterStats stats = character.Stats;
        StringBuilder sb = new StringBuilder();

        sb.Append(stats.CharacterName);

        string race = ExtractTagValue(character, "Race: ");
        string characterClass = ExtractTagValue(character, "Class: ");
        sb.Append("\n").Append(characterClass).Append(" • ").Append(race);

        string hpState = ExtractTagValue(character, "HP State: ");
        sb.Append("\nHP: ").Append(stats.CurrentHP).Append("/").Append(stats.TotalMaxHP);
        if (!string.IsNullOrWhiteSpace(hpState))
            sb.Append(" (").Append(hpState).Append(")");

        List<string> statuses = new List<string>();
        foreach (string tag in character.Tags.GetTagsByPrefix("Status: "))
        {
            if (tag.Length > 8)
                statuses.Add(tag.Substring(8));
        }

        sb.Append("\nStatus: ");
        sb.Append(statuses.Count > 0 ? string.Join(", ", statuses) : "None");

        return sb.ToString();
    }

    private static string ExtractTagValue(CharacterController character, string prefix)
    {
        foreach (string tag in character.Tags.GetTagsByPrefix(prefix))
        {
            if (tag.Length > prefix.Length)
                return tag.Substring(prefix.Length);
        }

        return "Unknown";
    }

    private static Font LoadBuiltinTooltipFont()
    {
        try
        {
            Font legacyRuntime = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (legacyRuntime != null)
                return legacyRuntime;
        }
        catch
        {
        }

        try
        {
            Font arial = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (arial != null)
                return arial;
        }
        catch
        {
        }

        return Font.CreateDynamicFontFromOSFont("Arial", 14);
    }
}
