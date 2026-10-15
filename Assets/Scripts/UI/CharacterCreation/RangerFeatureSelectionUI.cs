using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The PC ranger's class feature choices (PHB p.47-48; CHR-020): the combat style at 2nd level and each favored enemy
/// selection due by ranger level (Table 3-14), with the +2 increase that comes with every selection after the first.
/// Used by character creation (CharacterCreationUI, on the creation record) and by the level-up flow
/// (CharacterCreationManager, on the character). The choices are validated and applied by <see cref="RangerRules"/>,
/// the same rules that give NPC rangers their choices. One screen per pending favored enemy selection.
/// </summary>
public class RangerFeatureSelectionUI : MonoBehaviour
{
    private static readonly Color ButtonIdle = new Color(0.22f, 0.30f, 0.20f);
    private static readonly Color ButtonSelected = new Color(0.25f, 0.62f, 0.30f);
    private static readonly Color ButtonTaken = new Color(0.16f, 0.16f, 0.18f);

    private Font _font;
    private GameObject _overlayPanel;
    private GameObject _rootPanel;
    private Text _titleText;
    private Text _styleHeader;
    private Text _favoredHeader;
    private Text _increaseHeader;
    private Text _summaryText;
    private Button _confirmButton;
    private Button _archeryButton;
    private Button _twoWeaponButton;

    private readonly Dictionary<string, Button> _typeButtons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
    private readonly List<Button> _increaseButtons = new List<Button>();
    private readonly List<string> _increaseButtonTypes = new List<string>();

    private string _characterName;
    private int _rangerLevel;
    private FavoredEnemyData _favored;
    private CombatStyleData _style;
    private Action _onDone;

    private bool _needsStyle;
    private bool _needsFavored;
    private bool _needsIncrease;
    private RangerCombatStyle _styleChoice = RangerCombatStyle.None;
    private string _newType;
    private string _increaseType;

    public bool IsOpen { get; private set; }

    public void BuildUI(Canvas canvas)
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        _overlayPanel = CreatePanel(canvas.transform, "RangerFeatureOverlay", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.86f));
        _rootPanel = CreatePanel(_overlayPanel.transform, "RangerFeaturePanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(840, 700), new Color(0.10f, 0.14f, 0.11f, 0.98f));

        _titleText = CreateText(_rootPanel.transform, "Title", new Vector2(0, 318), new Vector2(780, 40), "RANGER CLASS FEATURES", 22, Color.white, TextAnchor.MiddleCenter);

        _styleHeader = CreateText(_rootPanel.transform, "StyleHeader", new Vector2(0, 280), new Vector2(780, 24), "Combat style (light or no armor only)", 14, new Color(0.8f, 1f, 0.8f), TextAnchor.MiddleCenter);
        _archeryButton = CreateButton(_rootPanel.transform, "Style_Archery", new Vector2(-170, 246), new Vector2(320, 36), "Archery\n(Rapid Shot; Manyshot at 6th)", ButtonIdle);
        _archeryButton.onClick.AddListener(() => { _styleChoice = RangerCombatStyle.Archery; Refresh(); });
        _twoWeaponButton = CreateButton(_rootPanel.transform, "Style_TwoWeapon", new Vector2(170, 246), new Vector2(320, 36), "Two-Weapon Combat\n(TWF; Improved TWF at 6th)", ButtonIdle);
        _twoWeaponButton.onClick.AddListener(() => { _styleChoice = RangerCombatStyle.TwoWeaponFighting; Refresh(); });

        _favoredHeader = CreateText(_rootPanel.transform, "FavoredHeader", new Vector2(0, 206), new Vector2(780, 24), "New favored enemy (PHB Table 3-14)", 14, new Color(0.8f, 1f, 0.8f), TextAnchor.MiddleCenter);
        string[] types = FavoredEnemyTypes.AllTypes;
        for (int i = 0; i < types.Length; i++)
        {
            int row = i / 4;
            int col = i % 4;
            float x = -300 + col * 200;
            float y = 176 - row * 31;
            string type = types[i];
            Button btn = CreateButton(_rootPanel.transform, "Favored_" + type, new Vector2(x, y), new Vector2(192, 27), type, ButtonIdle);
            btn.onClick.AddListener(() => { _newType = type; Refresh(); });
            _typeButtons[type] = btn;
        }

        _increaseHeader = CreateText(_rootPanel.transform, "IncreaseHeader", new Vector2(0, -88), new Vector2(780, 24), "+2 to one favored enemy (the new one included)", 14, new Color(0.8f, 1f, 0.8f), TextAnchor.MiddleCenter);
        for (int i = 0; i < 6; i++)
        {
            int index = i;
            float x = -325 + i * 130;
            Button btn = CreateButton(_rootPanel.transform, "Increase_" + i, new Vector2(x, -124), new Vector2(124, 40), string.Empty, ButtonIdle);
            btn.onClick.AddListener(() =>
            {
                if (index < _increaseButtonTypes.Count)
                    _increaseType = _increaseButtonTypes[index];
                Refresh();
            });
            _increaseButtons.Add(btn);
            _increaseButtonTypes.Add(null);
        }

        _summaryText = CreateText(_rootPanel.transform, "Summary", new Vector2(0, -200), new Vector2(780, 70), string.Empty, 14, new Color(1f, 0.9f, 0.55f), TextAnchor.MiddleCenter);
        _confirmButton = CreateButton(_rootPanel.transform, "Confirm", new Vector2(0, -290), new Vector2(300, 46), "Confirm ✓", new Color(0.2f, 0.52f, 0.22f));
        _confirmButton.onClick.AddListener(Confirm);

        _overlayPanel.SetActive(false);
    }

    /// <summary>
    /// Opens the choices still due for a ranger of <paramref name="rangerLevel"/> on <paramref name="favored"/> and
    /// <paramref name="style"/> (changed in place), then calls <paramref name="onDone"/>. With nothing due it calls
    /// <paramref name="onDone"/> at once.
    /// </summary>
    public void Show(string characterName, int rangerLevel, FavoredEnemyData favored, CombatStyleData style, Action onDone)
    {
        _characterName = characterName;
        _rangerLevel = rangerLevel;
        _favored = favored ?? new FavoredEnemyData();
        _style = style ?? new CombatStyleData();
        _onDone = onDone;

        if (!HasPending())
        {
            Finish();
            return;
        }

        EnsureBuilt();
        if (_overlayPanel == null)
        {
            Debug.LogWarning("[RangerFeatureSelectionUI] No canvas: the ranger choices stay pending.");
            Finish();
            return;
        }

        StartScreen();
        _overlayPanel.SetActive(true);
        IsOpen = true;
    }

    public void Close()
    {
        if (_overlayPanel != null)
            _overlayPanel.SetActive(false);
        IsOpen = false;
    }

    private bool HasPending()
        => RangerRules.NeedsCombatStyleChoice(_style, _rangerLevel)
            || RangerRules.PendingFavoredEnemySelections(_favored, _rangerLevel) > 0;

    private void StartScreen()
    {
        _needsStyle = RangerRules.NeedsCombatStyleChoice(_style, _rangerLevel);
        _needsFavored = RangerRules.PendingFavoredEnemySelections(_favored, _rangerLevel) > 0;
        _needsIncrease = _needsFavored && RangerRules.NextSelectionGrantsIncrease(_favored);
        _styleChoice = RangerCombatStyle.None;
        _newType = null;
        _increaseType = null;
        Refresh();
    }

    private void Refresh()
    {
        string who = string.IsNullOrWhiteSpace(_characterName) ? "Ranger" : _characterName;
        _titleText.text = $"{who.ToUpperInvariant()}: RANGER {_rangerLevel} CLASS FEATURES";

        SetActive(_styleHeader, _needsStyle);
        _archeryButton.gameObject.SetActive(_needsStyle);
        _twoWeaponButton.gameObject.SetActive(_needsStyle);
        SetButtonColor(_archeryButton, _styleChoice == RangerCombatStyle.Archery ? ButtonSelected : ButtonIdle);
        SetButtonColor(_twoWeaponButton, _styleChoice == RangerCombatStyle.TwoWeaponFighting ? ButtonSelected : ButtonIdle);

        SetActive(_favoredHeader, _needsFavored);
        if (_needsFavored)
        {
            int pending = RangerRules.PendingFavoredEnemySelections(_favored, _rangerLevel);
            _favoredHeader.text = pending > 1
                ? $"New favored enemy (PHB Table 3-14) - {pending} selections due"
                : "New favored enemy (PHB Table 3-14)";
        }
        foreach (var kvp in _typeButtons)
        {
            kvp.Value.gameObject.SetActive(_needsFavored);
            bool taken = _favored.Has(kvp.Key);
            kvp.Value.interactable = !taken;
            bool selected = string.Equals(kvp.Key, _newType, StringComparison.OrdinalIgnoreCase);
            SetButtonColor(kvp.Value, taken ? ButtonTaken : (selected ? ButtonSelected : ButtonIdle));
        }

        // The increase options: every existing favored enemy and the new one.
        var options = new List<string>();
        if (_needsIncrease)
        {
            options.AddRange(_favored.GetFavoredEnemyTypes());
            if (!string.IsNullOrEmpty(_newType))
                options.Add(_newType);
        }
        SetActive(_increaseHeader, _needsIncrease);
        for (int i = 0; i < _increaseButtons.Count; i++)
        {
            bool show = _needsIncrease && i < options.Count;
            _increaseButtons[i].gameObject.SetActive(show);
            _increaseButtonTypes[i] = show ? options[i] : null;
            if (!show)
                continue;
            FavoredEnemyEntry entry = _favored.Find(options[i]);
            int current = entry != null ? entry.Bonus : 0;
            int after = (current == 0 ? 2 : current) + 2;
            SetLabel(_increaseButtons[i], $"{options[i]}\n(+{after})");
            SetButtonColor(_increaseButtons[i], string.Equals(options[i], _increaseType, StringComparison.OrdinalIgnoreCase) ? ButtonSelected : ButtonIdle);
        }
        if (_increaseType != null && !options.Contains(_increaseType))
            _increaseType = null;

        var lines = new List<string>();
        if (_favored.HasFavoredEnemies)
            lines.Add("Favored enemies: " + _favored.GetSummary());
        lines.Add("+bonus on weapon damage and on Bluff, Listen, Sense Motive, Spot and Survival checks against them.");
        if (_needsStyle)
            lines.Add(_styleChoice == RangerCombatStyle.None ? "Choose a combat style." : "Combat style: " + CombatStyleData.Create(_styleChoice).GetStyleName());
        _summaryText.text = string.Join("\n", lines);

        bool complete = (!_needsStyle || _styleChoice != RangerCombatStyle.None)
            && (!_needsFavored || !string.IsNullOrEmpty(_newType))
            && (!_needsIncrease || !string.IsNullOrEmpty(_increaseType));
        _confirmButton.interactable = complete;
    }

    private void Confirm()
    {
        if (_needsStyle && !RangerRules.TryChooseCombatStyle(_style, _rangerLevel, _styleChoice, out string styleReason))
        {
            _summaryText.text = styleReason;
            return;
        }
        if (_needsFavored && !RangerRules.TryApplyFavoredEnemySelection(_favored, _rangerLevel, _newType, _increaseType, out string reason))
        {
            _summaryText.text = reason;
            return;
        }
        Debug.Log($"[Ranger] {_characterName}: combat style {_style.GetStyleName()}, favored enemies {_favored.GetSummary()} (CHR-020).");

        if (HasPending())
        {
            StartScreen();
            return;
        }
        Close();
        Finish();
    }

    private void Finish()
    {
        Action done = _onDone;
        _onDone = null;
        done?.Invoke();
    }

    private void EnsureBuilt()
    {
        if (_overlayPanel != null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
            return;

        BuildUI(canvas);
    }

    private static void SetActive(Text text, bool active)
    {
        if (text != null)
            text.gameObject.SetActive(active);
    }

    private static void SetLabel(Button button, string label)
    {
        Text text = button != null ? button.GetComponentInChildren<Text>() : null;
        if (text != null)
            text.text = label;
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

        CreateText(go.transform, "Label", Vector2.zero, size, label, 12, Color.white, TextAnchor.MiddleCenter);
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
