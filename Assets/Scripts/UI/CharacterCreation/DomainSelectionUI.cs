using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight cleric domain picker used by CharacterCreationManager.
/// Allows selecting a fixed number of domains and returns the chosen list.
/// </summary>
public class DomainSelectionUI : MonoBehaviour
{
    private Font _font;
    private GameObject _overlayPanel;
    private GameObject _rootPanel;
    private Text _titleText;
    private Text _summaryText;
    private Button _confirmButton;

    private CharacterController _character;
    private int _requiredCount;
    private Action<List<string>> _onConfirm;

    private readonly List<string> _availableDomains = new List<string>();
    private readonly List<string> _selectedDomains = new List<string>();
    private readonly List<Button> _domainButtons = new List<Button>();

    public void BuildUI(Canvas canvas)
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        _overlayPanel = CreatePanel(canvas.transform, "DomainOverlay", Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(0, 0), new Color(0, 0, 0, 0.85f));
        RectTransform overlayRT = _overlayPanel.GetComponent<RectTransform>();
        overlayRT.anchorMin = Vector2.zero;
        overlayRT.anchorMax = Vector2.one;
        overlayRT.offsetMin = Vector2.zero;
        overlayRT.offsetMax = Vector2.zero;

        _rootPanel = CreatePanel(_overlayPanel.transform, "DomainPanel", new Vector2(620, 520), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 0), new Color(0.12f, 0.12f, 0.18f, 0.98f));

        _titleText = CreateText(_rootPanel.transform, "Title", new Vector2(0, 220), new Vector2(560, 44), "SELECT DOMAINS", 22, Color.white, TextAnchor.MiddleCenter);
        _summaryText = CreateText(_rootPanel.transform, "Summary", new Vector2(0, 190), new Vector2(560, 28), "Selected: 0/2", 14, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter);

        _confirmButton = CreateButton(_rootPanel.transform, "Confirm", new Vector2(0, -220), new Vector2(260, 44), "Confirm Domains ✓", new Color(0.2f, 0.5f, 0.2f));
        _confirmButton.onClick.AddListener(OnConfirmPressed);
        _confirmButton.interactable = false;

        _overlayPanel.SetActive(false);
    }

    public void Show(CharacterController character, int requiredCount, Action<List<string>> onConfirm)
    {
        if (character == null || character.Stats == null)
        {
            onConfirm?.Invoke(new List<string>());
            return;
        }

        EnsureBuilt();
        if (_overlayPanel == null)
        {
            onConfirm?.Invoke(new List<string>());
            return;
        }

        _character = character;
        _requiredCount = Mathf.Max(1, requiredCount);
        _onConfirm = onConfirm;

        BuildDomainList();
        RebuildButtons();
        RefreshSummary();

        _overlayPanel.SetActive(true);
        Debug.Log($"[CharacterCreation] DomainSelectionUI opened for {_character.Stats.CharacterName} (need {_requiredCount})");
    }

    private void EnsureBuilt()
    {
        if (_overlayPanel != null && _rootPanel != null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
            return;

        BuildUI(canvas);
    }

    private void BuildDomainList()
    {
        _availableDomains.Clear();
        _selectedDomains.Clear();

        DomainDatabase.Init();
        DeityDatabase.Init();

        string deityId = _character.Stats.DeityId;
        DeityData deity = string.IsNullOrWhiteSpace(deityId) ? null : DeityDatabase.GetDeity(deityId);

        if (deity != null && deity.Domains != null && deity.Domains.Count > 0)
        {
            _availableDomains.AddRange(deity.Domains.Where(d => !string.IsNullOrWhiteSpace(d)).Distinct());
        }
        else
        {
            _availableDomains.AddRange(DomainDatabase.GetAllDomains()
                .Where(d => d != null && !string.IsNullOrWhiteSpace(d.Name))
                .Select(d => d.Name)
                .Distinct()
                .OrderBy(n => n));
        }

        if (_character.Stats.ChosenDomains != null)
        {
            foreach (string existing in _character.Stats.ChosenDomains)
            {
                if (_availableDomains.Contains(existing) && !_selectedDomains.Contains(existing) && _selectedDomains.Count < _requiredCount)
                    _selectedDomains.Add(existing);
            }
        }
    }

    private void RebuildButtons()
    {
        foreach (Button b in _domainButtons)
        {
            if (b != null)
                Destroy(b.gameObject);
        }
        _domainButtons.Clear();

        float startY = 140f;
        float buttonHeight = 40f;
        float spacing = 8f;

        for (int i = 0; i < _availableDomains.Count; i++)
        {
            int index = i;
            string domain = _availableDomains[i];

            Button btn = CreateButton(_rootPanel.transform,
                $"Domain_{domain}",
                new Vector2(0, startY - i * (buttonHeight + spacing)),
                new Vector2(500, buttonHeight),
                domain,
                new Color(0.2f, 0.24f, 0.36f));

            btn.onClick.AddListener(() => OnDomainPressed(index));
            _domainButtons.Add(btn);
        }

        RefreshButtons();
    }

    private void OnDomainPressed(int index)
    {
        if (index < 0 || index >= _availableDomains.Count)
            return;

        string domain = _availableDomains[index];
        if (_selectedDomains.Contains(domain))
        {
            _selectedDomains.Remove(domain);
        }
        else if (_selectedDomains.Count < _requiredCount)
        {
            _selectedDomains.Add(domain);
        }

        RefreshButtons();
        RefreshSummary();
    }

    private void RefreshButtons()
    {
        for (int i = 0; i < _domainButtons.Count; i++)
        {
            Button btn = _domainButtons[i];
            string domain = _availableDomains[i];
            bool selected = _selectedDomains.Contains(domain);

            ColorBlock cb = btn.colors;
            cb.normalColor = selected ? new Color(0.2f, 0.5f, 0.2f) : new Color(0.2f, 0.24f, 0.36f);
            cb.highlightedColor = cb.normalColor * 1.2f;
            btn.colors = cb;
        }
    }

    private void RefreshSummary()
    {
        if (_summaryText != null)
            _summaryText.text = $"Selected: {_selectedDomains.Count}/{_requiredCount}";

        if (_confirmButton != null)
            _confirmButton.interactable = _selectedDomains.Count == _requiredCount;
    }

    private void OnConfirmPressed()
    {
        if (_selectedDomains.Count != _requiredCount)
            return;

        List<string> selected = new List<string>(_selectedDomains);
        _overlayPanel.SetActive(false);

        Debug.Log($"[CharacterCreation] DomainSelectionUI confirmed: {string.Join(", ", selected)}");
        _onConfirm?.Invoke(selected);
    }

    private GameObject CreatePanel(Transform parent, string name, Vector2 size, Vector2 anchor, Vector2 pivot, Vector2 pos, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
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
        cb.highlightedColor = color * 1.2f;
        cb.pressedColor = color * 0.9f;
        btn.colors = cb;

        CreateText(go.transform, "Label", Vector2.zero, size, label, 14, Color.white, TextAnchor.MiddleCenter);
        return btn;
    }
}
