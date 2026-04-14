using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small screen-space tooltip used by battlefield status indicators.
/// Created lazily so scene setup stays lightweight.
/// </summary>
public class StatusEffectTooltipUI : MonoBehaviour
{
    public static StatusEffectTooltipUI Instance { get; private set; }

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
            GameObject canvasGO = new GameObject("StatusTooltipCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        GameObject go = new GameObject("StatusEffectTooltipUI");
        go.transform.SetParent(canvas.transform, false);
        StatusEffectTooltipUI tooltip = go.AddComponent<StatusEffectTooltipUI>();
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
        panelImage.color = new Color(0f, 0f, 0f, 0.82f);

        _panel.pivot = new Vector2(0f, 1f);
        _panel.anchorMin = new Vector2(0f, 1f);
        _panel.anchorMax = new Vector2(0f, 1f);
        _panel.sizeDelta = new Vector2(220f, 80f);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(panelGO.transform, false);
        RectTransform textRT = textGO.AddComponent<RectTransform>();
        _text = textGO.AddComponent<Text>();
        _text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
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

    public void ShowTooltip(string tooltipText, Vector2 screenPosition)
    {
        if (_panel == null || _text == null || _canvas == null)
            return;

        _text.text = tooltipText;

        float width = Mathf.Clamp(_text.preferredWidth + 20f, 140f, 340f);
        float height = Mathf.Clamp(_text.preferredHeight + 16f, 48f, 260f);
        _panel.sizeDelta = new Vector2(width, height);

        RectTransform canvasRT = _canvas.transform as RectTransform;
        Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screenPosition, uiCamera, out Vector2 localPos))
        {
            _panel.anchoredPosition = new Vector2(localPos.x + 16f, localPos.y - 16f);
        }

        _lastShowFrame = Time.frameCount;
        _panel.gameObject.SetActive(true);
    }

    public void HideTooltip()
    {
        if (_panel != null)
            _panel.gameObject.SetActive(false);
    }
}