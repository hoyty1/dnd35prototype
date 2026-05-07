using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WizardSpecializationUI : MonoBehaviour
{
    private Font _font;
    private GameObject _overlayPanel;
    private GameObject _rootPanel;
    private Text _summaryText;
    private Text _errorText;
    private Button _confirmButton;
    private Button _generalistButton;
    private Button _specialistButton;

    private readonly Dictionary<string, Button> _schoolButtons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Button> _prohibitedButtons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);

    private WizardSpecialization _selection = WizardSpecialization.CreateGeneralist();
    private Action<WizardSpecialization> _onConfirm;

    public bool IsOpen { get; private set; }

    public void BuildUI(Canvas canvas)
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        _overlayPanel = CreatePanel(canvas.transform, "WizardSpecOverlay", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.86f));
        _rootPanel = CreatePanel(_overlayPanel.transform, "WizardSpecPanel", new Vector2(760, 620), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.11f, 0.12f, 0.19f, 0.98f));

        CreateText(_rootPanel.transform, "Title", new Vector2(0, 278), new Vector2(700, 42), "WIZARD SCHOOL SPECIALIZATION", 22, Color.white, TextAnchor.MiddleCenter);

        _generalistButton = CreateButton(_rootPanel.transform, "Generalist", new Vector2(-170, 230), new Vector2(220, 42), "Generalist", new Color(0.24f, 0.28f, 0.40f));
        _generalistButton.onClick.AddListener(() => SetSpecialist(false));
        _specialistButton = CreateButton(_rootPanel.transform, "Specialist", new Vector2(170, 230), new Vector2(220, 42), "Specialist", new Color(0.24f, 0.28f, 0.40f));
        _specialistButton.onClick.AddListener(() => SetSpecialist(true));

        CreateText(_rootPanel.transform, "SchoolLabel", new Vector2(0, 190), new Vector2(680, 28), "Choose specialized school", 15, new Color(0.8f, 0.85f, 1f), TextAnchor.MiddleCenter);

        var schools = WizardSpecialization.SelectableSchools;
        for (int i = 0; i < schools.Count; i++)
        {
            int row = i / 4;
            int col = i % 4;
            float x = -255 + col * 170;
            float y = 145 - row * 52;
            string school = schools[i];
            Button btn = CreateButton(_rootPanel.transform, $"School_{school}", new Vector2(x, y), new Vector2(160, 40), school, new Color(0.18f, 0.24f, 0.36f));
            btn.onClick.AddListener(() => SetSpecializationSchool(school));
            _schoolButtons[school] = btn;
        }

        CreateText(_rootPanel.transform, "ProhibitedLabel", new Vector2(0, 35), new Vector2(680, 28), "Choose prohibited schools", 15, new Color(0.8f, 0.85f, 1f), TextAnchor.MiddleCenter);

        for (int i = 0; i < schools.Count; i++)
        {
            int row = i / 4;
            int col = i % 4;
            float x = -255 + col * 170;
            float y = -10 - row * 52;
            string school = schools[i];
            Button btn = CreateButton(_rootPanel.transform, $"Prohibited_{school}", new Vector2(x, y), new Vector2(160, 40), school, new Color(0.28f, 0.20f, 0.20f));
            btn.onClick.AddListener(() => ToggleProhibitedSchool(school));
            _prohibitedButtons[school] = btn;
        }

        _summaryText = CreateText(_rootPanel.transform, "Summary", new Vector2(0, -145), new Vector2(700, 56), string.Empty, 14, new Color(1f, 0.9f, 0.55f), TextAnchor.MiddleCenter);
        _summaryText.supportRichText = true;

        _errorText = CreateText(_rootPanel.transform, "Error", new Vector2(0, -188), new Vector2(700, 26), string.Empty, 13, new Color(1f, 0.55f, 0.55f), TextAnchor.MiddleCenter);

        _confirmButton = CreateButton(_rootPanel.transform, "Confirm", new Vector2(0, -252), new Vector2(300, 46), "Confirm Specialization ✓", new Color(0.2f, 0.52f, 0.22f));
        _confirmButton.onClick.AddListener(ConfirmSelection);

        _overlayPanel.SetActive(false);
    }

    public void Show(WizardSpecialization initial, Action<WizardSpecialization> onConfirm)
    {
        EnsureBuilt();
        if (_overlayPanel == null)
        {
            onConfirm?.Invoke(WizardSpecialization.CreateGeneralist());
            return;
        }

        _selection = initial != null
            ? new WizardSpecialization
            {
                isSpecialist = initial.isSpecialist,
                specializationSchool = initial.specializationSchool,
                prohibitedSchools = initial.prohibitedSchools != null ? new List<string>(initial.prohibitedSchools) : new List<string>()
            }
            : WizardSpecialization.CreateGeneralist();
        _selection.Normalize();

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

    private void SetSpecialist(bool specialist)
    {
        _selection.isSpecialist = specialist;
        if (!specialist)
        {
            _selection.specializationSchool = string.Empty;
            _selection.prohibitedSchools = new List<string>();
        }
        RefreshUI();
    }

    private void SetSpecializationSchool(string school)
    {
        if (string.IsNullOrWhiteSpace(school))
            return;

        _selection.isSpecialist = true;
        _selection.specializationSchool = school;
        _selection.prohibitedSchools.RemoveAll(s => string.Equals(s, school, StringComparison.OrdinalIgnoreCase));
        RefreshUI();
    }

    private void ToggleProhibitedSchool(string school)
    {
        if (!_selection.isSpecialist || string.IsNullOrWhiteSpace(_selection.specializationSchool))
            return;

        if (string.Equals(school, _selection.specializationSchool, StringComparison.OrdinalIgnoreCase))
            return;

        if (_selection.prohibitedSchools.Contains(school))
            _selection.prohibitedSchools.Remove(school);
        else
            _selection.prohibitedSchools.Add(school);

        // Keep only the maximum required picks.
        int required = _selection.RequiredProhibitedSchoolCount;
        if (_selection.prohibitedSchools.Count > required)
            _selection.prohibitedSchools.RemoveAt(0);

        RefreshUI();
    }

    private void RefreshUI()
    {
        _selection.Normalize();

        ColorizeModeButtons();
        ColorizeSchoolButtons();
        ColorizeProhibitedButtons();

        string summary;
        if (!_selection.isSpecialist)
        {
            summary = "Generalist selected. No prohibited schools.";
        }
        else
        {
            int required = _selection.RequiredProhibitedSchoolCount;
            string prohibited = _selection.prohibitedSchools.Count > 0 ? string.Join(", ", _selection.prohibitedSchools) : "None";
            summary = $"Specialist: <b>{_selection.specializationSchool}</b> | Prohibited ({_selection.prohibitedSchools.Count}/{required}): {prohibited}";
        }

        _summaryText.text = summary;

        bool valid = _selection.IsValid(out string error);
        _errorText.text = valid ? string.Empty : error;
        _confirmButton.interactable = valid;
    }

    private void ColorizeModeButtons()
    {
        SetButtonColor(_generalistButton, !_selection.isSpecialist ? new Color(0.2f, 0.5f, 0.2f) : new Color(0.24f, 0.28f, 0.40f));
        SetButtonColor(_specialistButton, _selection.isSpecialist ? new Color(0.2f, 0.5f, 0.2f) : new Color(0.24f, 0.28f, 0.40f));
    }

    private void ColorizeSchoolButtons()
    {
        foreach (var kvp in _schoolButtons)
        {
            bool selected = _selection.isSpecialist && string.Equals(_selection.specializationSchool, kvp.Key, StringComparison.OrdinalIgnoreCase);
            bool active = _selection.isSpecialist;
            kvp.Value.interactable = active;
            SetButtonColor(kvp.Value, selected ? new Color(0.2f, 0.6f, 0.3f) : new Color(0.18f, 0.24f, 0.36f));
        }
    }

    private void ColorizeProhibitedButtons()
    {
        bool active = _selection.isSpecialist && !string.IsNullOrWhiteSpace(_selection.specializationSchool);
        int required = _selection.RequiredProhibitedSchoolCount;

        foreach (var kvp in _prohibitedButtons)
        {
            bool isSpecialSchool = string.Equals(_selection.specializationSchool, kvp.Key, StringComparison.OrdinalIgnoreCase);
            bool selected = _selection.prohibitedSchools.Contains(kvp.Key);
            bool reachedLimit = _selection.prohibitedSchools.Count >= required;
            kvp.Value.interactable = active && !isSpecialSchool && (selected || !reachedLimit);
            SetButtonColor(kvp.Value, selected ? new Color(0.62f, 0.24f, 0.22f) : new Color(0.28f, 0.20f, 0.20f));
        }
    }

    private void ConfirmSelection()
    {
        if (!_selection.IsValid(out _))
            return;

        WizardSpecialization result = new WizardSpecialization
        {
            isSpecialist = _selection.isSpecialist,
            specializationSchool = _selection.specializationSchool,
            prohibitedSchools = _selection.prohibitedSchools != null ? new List<string>(_selection.prohibitedSchools) : new List<string>()
        };
        result.Normalize();

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

        CreateText(go.transform, "Label", Vector2.zero, size, label, 14, Color.white, TextAnchor.MiddleCenter);
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
