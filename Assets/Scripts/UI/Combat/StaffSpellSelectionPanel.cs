using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// StaffSpellSelectionPanel.cs — UI for selecting which spell to cast from a staff
//
// D&D 3.5e DMG p.243: Staves contain multiple spells. The wielder chooses which
// spell to activate each time (spell trigger activation, standard action).
//
// Layout mirrors QuickItemUsePanel — programmatic UI (no prefabs):
//   - Header: staff name + charges remaining
//   - Spell list with charge costs, grayed out if insufficient charges or stub
//   - Cancel button
//   - EXPENDED state when charges == 0
//
// Core DMG 3.5e only: no recharging, no supplements, no house rules.
// ============================================================================

public class StaffSpellSelectionPanel : MonoBehaviour
{
    // ========== CALLBACKS ==========

    /// <summary>Called when the player selects a spell. Parameters: (spellEntry, staffItem)</summary>
    public Action<StaffSpellEntry, ItemData> OnSpellSelected;

    /// <summary>Called when the panel is closed without selecting a spell.</summary>
    public Action OnCancelled;

    // ========== STATE ==========

    public bool IsOpen { get; private set; }

    private ItemData _currentStaff;
    private StaffDefinition _staffDef;
    private CharacterController _wielder;

    // ========== UI REFERENCES ==========

    private Font _font;
    private GameObject _overlayPanel;
    private GameObject _rootPanel;
    private Text _titleText;
    private Text _chargesText;
    private Text _infoText;

    // Scroll area
    private GameObject _scrollContent;
    private RectTransform _scrollContentRT;

    // Buttons
    private Button _closeBtn;
    private List<SpellRowUI> _spellRows = new List<SpellRowUI>();

    // Layout constants
    private const float PANEL_W = 480f;
    private const float PANEL_H = 440f;
    private const float ROW_H = 48f;
    private const float ROW_SPACING = 3f;

    private class SpellRowUI
    {
        public GameObject Row;
        public Button CastButton;
        public Text NameText;
        public Text CostText;
        public Text StatusText;
        public Image Background;
    }

    // ========== BUILD UI ==========

    public void BuildUI(Canvas canvas)
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // Dark overlay
        _overlayPanel = MakePanel(canvas.transform, "StaffSpellOverlay",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.7f));
        var overlayRT = _overlayPanel.GetComponent<RectTransform>();
        overlayRT.offsetMin = Vector2.zero;
        overlayRT.offsetMax = Vector2.zero;

        // Main panel centered
        _rootPanel = MakePanel(_overlayPanel.transform, "StaffSpellPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(PANEL_W, PANEL_H), new Color(0.12f, 0.12f, 0.18f, 0.97f));

        float halfW = PANEL_W / 2f;
        float halfH = PANEL_H / 2f;
        float y = halfH;

        // Title (staff name)
        y -= 24;
        _titleText = MakeText(_rootPanel.transform, "Title",
            new Vector2(0, y), new Vector2(PANEL_W - 20, 30),
            "STAFF", 18, Color.white, TextAnchor.MiddleCenter);
        _titleText.fontStyle = FontStyle.Bold;

        // Charges line
        y -= 22;
        _chargesText = MakeText(_rootPanel.transform, "Charges",
            new Vector2(0, y), new Vector2(PANEL_W - 20, 20),
            "", 14, new Color(0.85f, 0.75f, 0.4f), TextAnchor.MiddleCenter);

        // Info line (e.g., "Select a spell to cast" or "EXPENDED")
        y -= 20;
        _infoText = MakeText(_rootPanel.transform, "Info",
            new Vector2(0, y), new Vector2(PANEL_W - 20, 18),
            "", 11, new Color(0.6f, 0.6f, 0.6f), TextAnchor.MiddleCenter);

        // Scroll area for spell list
        y -= 10;
        float scrollTop = y;
        float scrollBottom = -halfH + 50; // room for close button
        float scrollH = scrollTop - scrollBottom;

        GameObject scrollArea = MakePanel(_rootPanel.transform, "ScrollArea",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, (scrollTop + scrollBottom) / 2f), new Vector2(PANEL_W - 24, scrollH),
            new Color(0.08f, 0.08f, 0.12f, 0.9f));

        // Viewport + mask
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollArea.transform, false);
        var vpRT = viewport.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = new Vector2(4, 4);
        vpRT.offsetMax = new Vector2(-4, -4);
        viewport.AddComponent<Image>().color = Color.white;
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        _scrollContent = new GameObject("Content");
        _scrollContent.transform.SetParent(viewport.transform, false);
        _scrollContentRT = _scrollContent.AddComponent<RectTransform>();
        _scrollContentRT.anchorMin = new Vector2(0, 1);
        _scrollContentRT.anchorMax = new Vector2(1, 1);
        _scrollContentRT.pivot = new Vector2(0.5f, 1);
        _scrollContentRT.anchoredPosition = Vector2.zero;
        _scrollContentRT.sizeDelta = new Vector2(0, 0);

        var scrollRect = scrollArea.AddComponent<ScrollRect>();
        scrollRect.content = _scrollContentRT;
        scrollRect.viewport = vpRT;
        scrollRect.vertical = true;
        scrollRect.horizontal = false;
        scrollRect.scrollSensitivity = 30f;

        ScrollbarHelper.CreateVerticalScrollbar(scrollRect, scrollArea.transform);

        // Close button
        _closeBtn = MakeButton(_rootPanel.transform, "CloseBtn",
            new Vector2(0, -halfH + 22), new Vector2(120, 32),
            "Cancel", new Color(0.5f, 0.25f, 0.25f), Color.white, 14);
        _closeBtn.onClick.AddListener(Close);

        _overlayPanel.SetActive(false);
    }

    // ========== OPEN / CLOSE ==========

    /// <summary>
    /// Open the spell selection panel for a specific staff.
    /// </summary>
    public void Open(CharacterController wielder, ItemData staffItem)
    {
        if (wielder == null || staffItem == null || !staffItem.IsStaff) return;

        _wielder = wielder;
        _currentStaff = staffItem;
        _staffDef = StaffDatabase.GetStaff(staffItem.StaffId);

        if (_staffDef == null)
        {
            Debug.LogError($"[StaffSpellSelectionPanel] Staff definition not found: {staffItem.StaffId}");
            return;
        }

        // Update header
        _titleText.text = $"⚡ {_staffDef.Name}";

        if (staffItem.StaffCharges <= 0)
        {
            _chargesText.text = "EXPENDED — non-magical";
            _chargesText.color = new Color(0.8f, 0.3f, 0.3f);
            _infoText.text = "(No spells available — staff is worthless)";
        }
        else
        {
            _chargesText.text = $"Charges: {staffItem.StaffCharges}/{_staffDef.MaxCharges} remaining";
            _chargesText.color = staffItem.StaffCharges <= 5
                ? new Color(1f, 0.6f, 0.2f) // orange warning
                : new Color(0.85f, 0.75f, 0.4f); // gold
            _infoText.text = "Select a spell to cast:";
        }

        // Build spell list
        BuildSpellList();

        _overlayPanel.SetActive(true);
        IsOpen = true;
    }

    public void Close()
    {
        _overlayPanel.SetActive(false);
        IsOpen = false;
        _wielder = null;
        _currentStaff = null;
        _staffDef = null;
        OnCancelled?.Invoke();
    }

    // ========== SPELL LIST ==========

    private void BuildSpellList()
    {
        // Clear existing rows
        foreach (var row in _spellRows)
        {
            if (row.Row != null) Destroy(row.Row);
        }
        _spellRows.Clear();

        if (_staffDef == null || _staffDef.Spells == null) return;

        float contentHeight = _staffDef.Spells.Count * (ROW_H + ROW_SPACING);
        _scrollContentRT.sizeDelta = new Vector2(0, contentHeight);

        float contentW = _scrollContentRT.rect.width > 0 ? _scrollContentRT.rect.width : PANEL_W - 40;

        for (int i = 0; i < _staffDef.Spells.Count; i++)
        {
            var entry = _staffDef.Spells[i];
            CreateSpellRow(entry, i, contentW);
        }
    }

    private void CreateSpellRow(StaffSpellEntry entry, int index, float contentW)
    {
        var row = new SpellRowUI();

        // Row background
        float yPos = -(index * (ROW_H + ROW_SPACING) + ROW_H / 2f);
        row.Row = new GameObject($"SpellRow_{index}");
        row.Row.transform.SetParent(_scrollContent.transform, false);
        var rowRT = row.Row.AddComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0, 1);
        rowRT.anchorMax = new Vector2(1, 1);
        rowRT.pivot = new Vector2(0.5f, 1);
        rowRT.anchoredPosition = new Vector2(0, -(index * (ROW_H + ROW_SPACING)));
        rowRT.sizeDelta = new Vector2(-8, ROW_H);

        // Determine availability
        bool hasCharges = _currentStaff != null && _currentStaff.StaffCharges >= entry.ChargeCost;
        bool isStub = entry.IsStub;
        bool canCast = hasCharges && !isStub;

        // Background color
        Color bgColor;
        if (!hasCharges)
            bgColor = new Color(0.15f, 0.12f, 0.12f, 0.9f); // dark red tint
        else if (isStub)
            bgColor = new Color(0.14f, 0.14f, 0.14f, 0.9f); // dark gray
        else
            bgColor = new Color(0.15f, 0.18f, 0.25f, 0.9f); // dark blue

        row.Background = row.Row.AddComponent<Image>();
        row.Background.color = bgColor;

        // Make it a button
        row.CastButton = row.Row.AddComponent<Button>();
        var nav = row.CastButton.navigation;
        nav.mode = Navigation.Mode.None;
        row.CastButton.navigation = nav;
        row.CastButton.interactable = canCast;

        if (canCast)
        {
            var colors = row.CastButton.colors;
            colors.normalColor = bgColor;
            colors.highlightedColor = new Color(0.2f, 0.25f, 0.4f, 0.95f);
            colors.pressedColor = new Color(0.25f, 0.35f, 0.5f, 0.95f);
            row.CastButton.colors = colors;
        }

        // Spell name (left side)
        Color nameColor = canCast ? Color.white : new Color(0.5f, 0.5f, 0.5f);
        row.NameText = MakeText(row.Row.transform, "Name",
            new Vector2(-50, 6), new Vector2(260, 22),
            entry.SpellName, 14, nameColor, TextAnchor.MiddleLeft);
        var nameRT = row.NameText.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0, 0.5f);
        nameRT.anchorMax = new Vector2(0, 0.5f);
        nameRT.pivot = new Vector2(0, 0.5f);
        nameRT.anchoredPosition = new Vector2(12, 6);

        // Charge cost (right side)
        string costStr = $"{entry.ChargeCost} charge{(entry.ChargeCost > 1 ? "s" : "")}";
        Color costColor = hasCharges ? new Color(0.85f, 0.75f, 0.4f) : new Color(0.7f, 0.3f, 0.3f);
        row.CostText = MakeText(row.Row.transform, "Cost",
            new Vector2(-80, 6), new Vector2(100, 20),
            costStr, 12, costColor, TextAnchor.MiddleRight);
        var costRT = row.CostText.GetComponent<RectTransform>();
        costRT.anchorMin = new Vector2(1, 0.5f);
        costRT.anchorMax = new Vector2(1, 0.5f);
        costRT.pivot = new Vector2(1, 0.5f);
        costRT.anchoredPosition = new Vector2(-12, 6);

        // Status line (below spell name)
        string statusStr;
        Color statusColor;
        if (isStub)
        {
            statusStr = $"(Not implemented) {entry.StubDescription ?? ""}";
            statusColor = new Color(0.55f, 0.45f, 0.35f);
        }
        else if (!hasCharges)
        {
            statusStr = "Insufficient charges";
            statusColor = new Color(0.7f, 0.3f, 0.3f);
        }
        else
        {
            statusStr = $"Level {entry.SpellLevel} | CL {(_staffDef != null ? _staffDef.CasterLevel : 0)} | DC {StaffValidator.CalculateStaffSaveDC(entry.SpellLevel)}";
            statusColor = new Color(0.55f, 0.55f, 0.65f);
        }

        row.StatusText = MakeText(row.Row.transform, "Status",
            new Vector2(12, -8), new Vector2(350, 16),
            statusStr, 10, statusColor, TextAnchor.MiddleLeft);
        var statusRT = row.StatusText.GetComponent<RectTransform>();
        statusRT.anchorMin = new Vector2(0, 0.5f);
        statusRT.anchorMax = new Vector2(0, 0.5f);
        statusRT.pivot = new Vector2(0, 0.5f);
        statusRT.anchoredPosition = new Vector2(12, -10);

        // Click handler
        if (canCast)
        {
            StaffSpellEntry capturedEntry = entry;
            row.CastButton.onClick.AddListener(() => OnSpellClicked(capturedEntry));
        }

        _spellRows.Add(row);
    }

    private void OnSpellClicked(StaffSpellEntry entry)
    {
        _overlayPanel.SetActive(false);
        IsOpen = false;

        // Invoke callback — GameManager handles the actual casting
        OnSpellSelected?.Invoke(entry, _currentStaff);
    }

    // ========== UI HELPERS (mirrors QuickItemUsePanel) ==========

    private GameObject MakePanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta, Color bgColor)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        var img = go.AddComponent<Image>();
        img.color = bgColor;
        return go;
    }

    private Text MakeText(Transform parent, string name,
        Vector2 pos, Vector2 size, string content, int fontSize,
        Color color, TextAnchor alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.AddComponent<CanvasRenderer>();
        var txt = go.AddComponent<Text>();
        txt.font = _font;
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = alignment;
        txt.text = content;
        txt.supportRichText = true;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        return txt;
    }

    private Button MakeButton(Transform parent, string name,
        Vector2 pos, Vector2 size, string label,
        Color bgColor, Color textColor, int fontSize)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = bgColor;
        var btn = go.AddComponent<Button>();
        var nav = btn.navigation;
        nav.mode = Navigation.Mode.None;
        btn.navigation = nav;

        var colors = btn.colors;
        colors.normalColor = bgColor;
        colors.highlightedColor = bgColor * 1.2f;
        colors.pressedColor = bgColor * 0.8f;
        btn.colors = colors;

        // Label
        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
        labelGO.AddComponent<CanvasRenderer>();
        var labelText = labelGO.AddComponent<Text>();
        labelText.font = _font;
        labelText.fontSize = fontSize;
        labelText.color = textColor;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.text = label;

        return btn;
    }
}
