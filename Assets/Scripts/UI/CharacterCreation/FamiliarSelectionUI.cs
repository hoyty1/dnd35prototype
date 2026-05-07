using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FamiliarSelectionUI : MonoBehaviour
{
    private Font _font;
    private GameObject _overlayPanel;
    private GameObject _rootPanel;
    private Text _summaryText;
    private Button _confirmButton;

    private readonly Dictionary<string, Button> _familiarButtons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);

    private WizardFamiliar _selection = WizardFamiliar.CreateNone();
    private Action<WizardFamiliar> _onConfirm;

    public bool IsOpen { get; private set; }

    public void BuildUI(Canvas canvas)
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        _overlayPanel = CreatePanel(canvas.transform, "FamiliarOverlay", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.86f));
        // Centered fixed-size panel. Anchors must be normalized (0..1), size goes in sizeDelta.
        _rootPanel = CreatePanel(_overlayPanel.transform, "FamiliarPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 620), new Color(0.11f, 0.12f, 0.19f, 0.98f));

        CreateText(_rootPanel.transform, "Title", new Vector2(0, 278), new Vector2(700, 42), "WIZARD FAMILIAR", 22, Color.white, TextAnchor.MiddleCenter);
        CreateText(_rootPanel.transform, "Subtitle", new Vector2(0, 244), new Vector2(700, 24), "Choose a familiar (or none)", 14, new Color(0.8f, 0.85f, 1f), TextAnchor.MiddleCenter);

        Button noneButton = CreateButton(_rootPanel.transform, "Familiar_None", new Vector2(0, 204), new Vector2(350, 42), "No Familiar", new Color(0.24f, 0.28f, 0.40f));
        noneButton.onClick.AddListener(() => SelectFamiliar(string.Empty));
        _familiarButtons[string.Empty] = noneButton;

        var familiarTypes = WizardFamiliar.FamiliarTypes;
        for (int i = 0; i < familiarTypes.Count; i++)
        {
            int row = i / 2;
            int col = i % 2;
            float x = col == 0 ? -180 : 180;
            float y = 152 - row * 58;
            string type = familiarTypes[i];
            string label = BuildFamiliarLabel(type);
            Button btn = CreateButton(_rootPanel.transform, $"Familiar_{type}", new Vector2(x, y), new Vector2(320, 48), label, new Color(0.2f, 0.26f, 0.38f));
            btn.onClick.AddListener(() => SelectFamiliar(type));
            _familiarButtons[type] = btn;
        }

        _summaryText = CreateText(_rootPanel.transform, "Summary", new Vector2(0, -176), new Vector2(700, 56), string.Empty, 14, new Color(1f, 0.9f, 0.55f), TextAnchor.MiddleCenter);
        _summaryText.supportRichText = true;

        _confirmButton = CreateButton(_rootPanel.transform, "Confirm", new Vector2(0, -252), new Vector2(300, 46), "Confirm Familiar ✓", new Color(0.2f, 0.52f, 0.22f));
        _confirmButton.onClick.AddListener(ConfirmSelection);

        _overlayPanel.SetActive(false);
    }

    public void Show(WizardFamiliar initial, Action<WizardFamiliar> onConfirm)
    {
        EnsureBuilt();
        if (_overlayPanel == null)
        {
            onConfirm?.Invoke(WizardFamiliar.CreateNone());
            return;
        }

        _selection = initial != null
            ? WizardFamiliar.Create(initial.hasFamiliar ? initial.familiarType : string.Empty)
            : WizardFamiliar.CreateNone();
        _onConfirm = onConfirm;
        RefreshUI();

        _overlayPanel.SetActive(true);
        IsOpen = true;
    }

    public void Close()
    {
        if (_overlayPanel != null)
            _overlayPanel.SetActive(false);
        IsOpen = false;
    }

    private string BuildFamiliarLabel(string familiarType)
    {
        WizardFamiliar familiar = WizardFamiliar.Create(familiarType);
        familiar.EnsureBonusesInitialized();

        string bonus = familiar.serializedBonuses != null && familiar.serializedBonuses.Count > 0
            ? $"+{familiar.serializedBonuses[0].value} {familiar.serializedBonuses[0].key}"
            : "bonus";
        return $"{familiarType}\n({bonus})";
    }

    private void SelectFamiliar(string familiarType)
    {
        _selection = string.IsNullOrWhiteSpace(familiarType)
            ? WizardFamiliar.CreateNone()
            : WizardFamiliar.Create(familiarType);
        RefreshUI();
    }

    private void RefreshUI()
    {
        foreach (var kvp in _familiarButtons)
        {
            bool selected = string.Equals(kvp.Key, _selection.hasFamiliar ? _selection.familiarType : string.Empty, StringComparison.OrdinalIgnoreCase);
            SetButtonColor(kvp.Value, selected ? new Color(0.2f, 0.6f, 0.3f) : new Color(0.24f, 0.28f, 0.40f));
        }

        string summary;
        if (!_selection.hasFamiliar)
        {
            summary = "No familiar selected.";
        }
        else
        {
            _selection.EnsureBonusesInitialized();
            string bonusText = _selection.serializedBonuses != null && _selection.serializedBonuses.Count > 0
                ? string.Join(", ", _selection.serializedBonuses.ConvertAll(b => $"{(b.value >= 0 ? "+" : string.Empty)}{b.value} {b.key}"))
                : "special bonus";
            summary = $"Familiar: <b>{_selection.familiarType}</b> | Master bonus: {bonusText} | Alertness while adjacent.";
        }

        _summaryText.text = summary;
        _confirmButton.interactable = true;
    }

    private void ConfirmSelection()
    {
        WizardFamiliar result = _selection != null
            ? WizardFamiliar.Create(_selection.hasFamiliar ? _selection.familiarType : string.Empty)
            : WizardFamiliar.CreateNone();

        Close();
        _onConfirm?.Invoke(result);
    }

    private void EnsureBuilt()
    {
        if (_overlayPanel != null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
            return;

        BuildUI(canvas);
    }

    private GameObject CreatePanel(Transform parent, string name, Vector2 minAnchor, Vector2 maxAnchor, Vector2 pivot, Vector2 anchoredPos, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = minAnchor;
        rt.anchorMax = maxAnchor;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        Image image = go.AddComponent<Image>();
        image.color = color;
        return go;
    }

    private Text CreateText(Transform parent, string name, Vector2 pos, Vector2 size, string value, int fontSize, Color color, TextAnchor anchor)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Text text = go.AddComponent<Text>();
        text.font = _font;
        text.fontSize = fontSize;
        text.text = value;
        text.color = color;
        text.alignment = anchor;
        text.supportRichText = true;
        return text;
    }

    private Button CreateButton(Transform parent, string name, Vector2 pos, Vector2 size, string label, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image bg = go.AddComponent<Image>();
        bg.color = color;

        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = color;
        cb.highlightedColor = color * 1.15f;
        cb.pressedColor = color * 0.9f;
        btn.colors = cb;

        CreateText(go.transform, "Label", Vector2.zero, size, label, 13, Color.white, TextAnchor.MiddleCenter);
        return btn;
    }

    private static void SetButtonColor(Button button, Color color)
    {
        if (button == null)
            return;

        Image bg = button.GetComponent<Image>();
        if (bg != null)
            bg.color = color;

        ColorBlock cb = button.colors;
        cb.normalColor = color;
        cb.highlightedColor = color * 1.15f;
        cb.pressedColor = color * 0.9f;
        button.colors = cb;
    }
}
