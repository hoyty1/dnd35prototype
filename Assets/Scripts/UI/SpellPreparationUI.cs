using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Spell preparation UI for D&D 3.5e Wizards.
/// Allows the wizard to assign spells from their spellbook into individual spell slots.
/// Each slot can hold one spell; the same spell can be prepared in multiple slots.
///
/// D&D 3.5e Rules:
///   - After rest, wizard can re-prepare spells by filling each slot with a spell from the spellbook.
///   - Each slot holds exactly one spell of that slot's level.
///   - The same spell can fill multiple slots (e.g., 3 slots of Magic Missile).
///   - Cantrips also use slots.
///   - Prepared spells persist between rests unless the wizard re-prepares.
///
/// Usage: Called from GameManager when wizard chooses to re-prepare spells after rest,
/// or from character creation to set initial preparation.
/// </summary>
public class SpellPreparationUI : MonoBehaviour
{
    // ========== PUBLIC ==========
    /// <summary>Callback when preparation is confirmed.</summary>
    public Action OnPreparationConfirmed;

    /// <summary>Whether the preparation UI is currently open.</summary>
    public bool IsOpen { get; private set; }

    // ========== PRIVATE ==========
    private Font _font;
    private GameObject _overlayPanel;
    private GameObject _rootPanel;
    private SpellcastingComponent _spellComp;
    private Text _titleText;
    private Text _slotSummaryText;
    private Button _confirmButton;
    private Button _autoPrepareButton;

    // Slot UI rows
    private List<SlotRowUI> _slotRows = new List<SlotRowUI>();

    // Layout
    private const float PANEL_W = 800f;
    private const float PANEL_H = 650f;

    private class SlotRowUI
    {
        public GameObject Row;
        public Text LabelText;
        public Dropdown SpellDropdown;
        public SpellSlot Slot;
        public int SlotIndex;
        public List<SpellData> AvailableSpells; // Spells for dropdown at this level
    }

    // ========== BUILD UI ==========

    public void BuildUI(Canvas canvas)
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // Dark overlay
        _overlayPanel = MakePanel(canvas.transform, "SpellPrepOverlay",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.85f));
        var overlayRT = _overlayPanel.GetComponent<RectTransform>();
        overlayRT.offsetMin = Vector2.zero;
        overlayRT.offsetMax = Vector2.zero;

        // Main panel
        _rootPanel = MakePanel(_overlayPanel.transform, "SpellPrepPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(PANEL_W, PANEL_H), new Color(0.12f, 0.12f, 0.18f, 0.98f));

        float headerTop = PANEL_H / 2f;

        // Title
        _titleText = MakeText(_rootPanel.transform, "Title",
            new Vector2(0, headerTop - 30), new Vector2(PANEL_W - 40, 40),
            "SPELL PREPARATION", 22, Color.white, TextAnchor.MiddleCenter);

        // Slot summary
        _slotSummaryText = MakeText(_rootPanel.transform, "Summary",
            new Vector2(0, headerTop - 62), new Vector2(PANEL_W - 40, 24),
            "", 14, new Color(0.7f, 0.8f, 1f), TextAnchor.MiddleCenter);

        // Scroll area for slot list
        float contentTop = headerTop - 90;
        float contentBottom = -PANEL_H / 2f + 80;
        float contentH = contentTop - contentBottom;
        float contentCenterY = (contentTop + contentBottom) / 2f;

        GameObject scrollArea = MakePanel(_rootPanel.transform, "ScrollArea",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, contentCenterY), new Vector2(PANEL_W - 40, contentH),
            new Color(0.08f, 0.08f, 0.12f, 0.9f));

        // Viewport with mask
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollArea.transform, false);
        var vpRT = viewport.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = new Vector2(4, 4);
        vpRT.offsetMax = new Vector2(-4, -4);
        viewport.AddComponent<Image>().color = Color.white;
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        var scrollContent = new GameObject("Content");
        scrollContent.transform.SetParent(viewport.transform, false);
        var scrollContentRT = scrollContent.AddComponent<RectTransform>();
        scrollContentRT.anchorMin = new Vector2(0, 1);
        scrollContentRT.anchorMax = new Vector2(1, 1);
        scrollContentRT.pivot = new Vector2(0.5f, 1);
        scrollContentRT.anchoredPosition = Vector2.zero;
        scrollContentRT.sizeDelta = new Vector2(0, 0);

        var scrollRect = scrollArea.AddComponent<ScrollRect>();
        scrollRect.content = scrollContentRT;
        scrollRect.viewport = vpRT;
        scrollRect.vertical = true;
        scrollRect.horizontal = false;
        scrollRect.scrollSensitivity = 30f;

        ScrollbarHelper.CreateVerticalScrollbar(scrollRect, scrollArea.transform);

        // Bottom buttons
        float btnY = -PANEL_H / 2f + 35;

        _autoPrepareButton = MakeButton(_rootPanel.transform, "AutoPrepBtn",
            new Vector2(-140, btnY), new Vector2(220, 42),
            "Auto-Prepare ⚡", new Color(0.3f, 0.3f, 0.5f), Color.white, 16);
        _autoPrepareButton.onClick.AddListener(OnAutoPrepare);

        _confirmButton = MakeButton(_rootPanel.transform, "ConfirmBtn",
            new Vector2(140, btnY), new Vector2(220, 42),
            "Confirm Preparation ✓", new Color(0.2f, 0.6f, 0.2f), Color.white, 16);
        _confirmButton.onClick.AddListener(OnConfirm);

        _overlayPanel.SetActive(false);
    }

    // ========== OPEN / CLOSE ==========

    /// <summary>
    /// Open the spell preparation UI for a wizard character.
    /// Shows all spell slots with dropdowns to select spells from the spellbook.
    /// </summary>
    public void Open(SpellcastingComponent spellComp)
    {
        if (spellComp == null || spellComp.Stats == null || !spellComp.Stats.IsWizard)
        {
            Debug.LogWarning("[SpellPreparationUI] Can only open for Wizard characters!");
            return;
        }

        _spellComp = spellComp;
        _titleText.text = $"SPELL PREPARATION — {spellComp.Stats.CharacterName}";

        PopulateSlotList();
        RefreshSummary();

        _overlayPanel.SetActive(true);
        IsOpen = true;
    }

    /// <summary>Close the preparation UI.</summary>
    public void Close()
    {
        if (_overlayPanel != null)
            _overlayPanel.SetActive(false);
        IsOpen = false;
    }

    // ========== POPULATE ==========

    private void PopulateSlotList()
    {
        // Clear existing rows
        foreach (var row in _slotRows)
        {
            if (row.Row != null) Destroy(row.Row);
        }
        _slotRows.Clear();

        var scrollContent = _rootPanel.transform.Find("ScrollArea/Viewport/Content");
        if (scrollContent == null) return;

        // Clear any leftover children
        foreach (Transform child in scrollContent)
        {
            Destroy(child.gameObject);
        }

        float yPos = -8f;
        float rowHeight = 40f;
        float spacing = 4f;
        int lastLevel = -1;
        int slotIndex = 0;

        foreach (var slot in _spellComp.SpellSlots)
        {
            // Level header
            if (slot.Level != lastLevel)
            {
                if (lastLevel >= 0) yPos -= 8f;
                lastLevel = slot.Level;

                string levelLabel = slot.Level == 0 ? "═══ CANTRIPS (Level 0) ═══" :
                                    $"═══ LEVEL {slot.Level} SPELLS ═══";
                int slotsAtLevel = _spellComp.GetSlotsForLevel(slot.Level).Count;
                levelLabel += $"  ({slotsAtLevel} slots)";

                var headerGO = new GameObject("Header" + slot.Level);
                headerGO.transform.SetParent(scrollContent, false);
                var hRT = headerGO.AddComponent<RectTransform>();
                hRT.anchorMin = new Vector2(0, 1);
                hRT.anchorMax = new Vector2(1, 1);
                hRT.pivot = new Vector2(0.5f, 1);
                hRT.anchoredPosition = new Vector2(0, yPos);
                hRT.sizeDelta = new Vector2(0, 28);
                var hText = headerGO.AddComponent<Text>();
                hText.text = levelLabel;
                hText.font = _font;
                hText.fontSize = 13;
                hText.color = new Color(0.7f, 0.8f, 1f);
                hText.alignment = TextAnchor.MiddleCenter;
                hText.supportRichText = true;
                hText.raycastTarget = false;
                yPos -= 30f;
            }

            // Slot row
            var rowUI = CreateSlotRow(scrollContent, slot, slotIndex, yPos);
            _slotRows.Add(rowUI);
            yPos -= (rowHeight + spacing);
            slotIndex++;
        }

        // Set content height
        var contentRT = scrollContent.GetComponent<RectTransform>();
        contentRT.sizeDelta = new Vector2(0, Mathf.Abs(yPos) + 12f);
    }

    private SlotRowUI CreateSlotRow(Transform parent, SpellSlot slot, int index, float yPos)
    {
        var row = new SlotRowUI();
        row.Slot = slot;
        row.SlotIndex = index;

        // Get available spells at this level from spellbook
        row.AvailableSpells = _spellComp.KnownSpells
            .Where(s => s.SpellLevel == slot.Level)
            .OrderBy(s => s.Name)
            .ToList();

        // Row container
        row.Row = new GameObject($"SlotRow_{index}");
        row.Row.transform.SetParent(parent, false);
        var rt = row.Row.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, yPos);
        rt.sizeDelta = new Vector2(0, 40f);

        // Background
        var bg = row.Row.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.15f, 0.6f);

        // Slot label (left side)
        int levelSlotNum = _spellComp.GetSlotsForLevel(slot.Level).IndexOf(slot) + 1;
        string slotLabel = slot.Level == 0 ? $"Cantrip Slot {levelSlotNum}:" : $"Level {slot.Level} Slot {levelSlotNum}:";

        row.LabelText = MakeText(row.Row.transform, "Label",
            new Vector2(-280, 0), new Vector2(180, 30),
            slotLabel, 13, new Color(0.8f, 0.8f, 0.7f), TextAnchor.MiddleRight);

        // Dropdown for spell selection (right side)
        GameObject dropdownObj = new GameObject("Dropdown");
        dropdownObj.transform.SetParent(row.Row.transform, false);
        var ddRT = dropdownObj.AddComponent<RectTransform>();
        ddRT.anchorMin = new Vector2(0.5f, 0.5f);
        ddRT.anchorMax = new Vector2(0.5f, 0.5f);
        ddRT.pivot = new Vector2(0.5f, 0.5f);
        ddRT.anchoredPosition = new Vector2(60, 0);
        ddRT.sizeDelta = new Vector2(350, 32);

        // Dropdown background image
        var ddBG = dropdownObj.AddComponent<Image>();
        ddBG.color = new Color(0.15f, 0.15f, 0.25f, 1f);

        // Create dropdown component
        var dropdown = dropdownObj.AddComponent<Dropdown>();

        // Label for the dropdown (current value)
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(dropdownObj.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = new Vector2(8, 2);
        labelRT.offsetMax = new Vector2(-28, -2);
        var labelText = labelGO.AddComponent<Text>();
        labelText.font = _font;
        labelText.fontSize = 13;
        labelText.color = Color.white;
        labelText.alignment = TextAnchor.MiddleLeft;

        // Arrow indicator
        var arrowGO = new GameObject("Arrow");
        arrowGO.transform.SetParent(dropdownObj.transform, false);
        var arrowRT = arrowGO.AddComponent<RectTransform>();
        arrowRT.anchorMin = new Vector2(1, 0);
        arrowRT.anchorMax = new Vector2(1, 1);
        arrowRT.pivot = new Vector2(1, 0.5f);
        arrowRT.anchoredPosition = new Vector2(-4, 0);
        arrowRT.sizeDelta = new Vector2(20, 0);
        var arrowText = arrowGO.AddComponent<Text>();
        arrowText.font = _font;
        arrowText.fontSize = 14;
        arrowText.color = new Color(0.7f, 0.7f, 0.9f);
        arrowText.text = "▼";
        arrowText.alignment = TextAnchor.MiddleCenter;

        // Create template for dropdown items
        var templateGO = new GameObject("Template");
        templateGO.transform.SetParent(dropdownObj.transform, false);
        var tempRT = templateGO.AddComponent<RectTransform>();
        tempRT.anchorMin = new Vector2(0, 0);
        tempRT.anchorMax = new Vector2(1, 0);
        tempRT.pivot = new Vector2(0.5f, 1);
        tempRT.anchoredPosition = Vector2.zero;
        tempRT.sizeDelta = new Vector2(0, 200);
        var tempImg = templateGO.AddComponent<Image>();
        tempImg.color = new Color(0.12f, 0.12f, 0.2f, 0.98f);
        var tempScroll = templateGO.AddComponent<ScrollRect>();

        // Viewport in template
        var tempVP = new GameObject("Viewport");
        tempVP.transform.SetParent(templateGO.transform, false);
        var vpRT2 = tempVP.AddComponent<RectTransform>();
        vpRT2.anchorMin = Vector2.zero;
        vpRT2.anchorMax = Vector2.one;
        vpRT2.offsetMin = Vector2.zero;
        vpRT2.offsetMax = Vector2.zero;
        tempVP.AddComponent<Image>().color = Color.white;
        tempVP.AddComponent<Mask>().showMaskGraphic = false;
        tempScroll.viewport = vpRT2;

        // Content in template viewport
        var tempContent = new GameObject("Content");
        tempContent.transform.SetParent(tempVP.transform, false);
        var tcRT = tempContent.AddComponent<RectTransform>();
        tcRT.anchorMin = new Vector2(0, 1);
        tcRT.anchorMax = new Vector2(1, 1);
        tcRT.pivot = new Vector2(0.5f, 1);
        tcRT.anchoredPosition = Vector2.zero;
        tcRT.sizeDelta = new Vector2(0, 28);
        tempScroll.content = tcRT;

        // Item template
        var itemGO = new GameObject("Item");
        itemGO.transform.SetParent(tempContent.transform, false);
        var itemRT = itemGO.AddComponent<RectTransform>();
        itemRT.anchorMin = new Vector2(0, 0.5f);
        itemRT.anchorMax = new Vector2(1, 0.5f);
        itemRT.sizeDelta = new Vector2(0, 28);
        var itemToggle = itemGO.AddComponent<Toggle>();

        var itemBG = new GameObject("Item Background");
        itemBG.transform.SetParent(itemGO.transform, false);
        var ibRT = itemBG.AddComponent<RectTransform>();
        ibRT.anchorMin = Vector2.zero;
        ibRT.anchorMax = Vector2.one;
        ibRT.offsetMin = Vector2.zero;
        ibRT.offsetMax = Vector2.zero;
        var ibImg = itemBG.AddComponent<Image>();
        ibImg.color = new Color(0.15f, 0.15f, 0.25f, 1f);

        var itemCheck = new GameObject("Item Checkmark");
        itemCheck.transform.SetParent(itemBG.transform, false);
        var icRT = itemCheck.AddComponent<RectTransform>();
        icRT.anchorMin = new Vector2(0, 0);
        icRT.anchorMax = new Vector2(0, 1);
        icRT.pivot = new Vector2(0, 0.5f);
        icRT.anchoredPosition = new Vector2(4, 0);
        icRT.sizeDelta = new Vector2(20, 0);
        var icImg = itemCheck.AddComponent<Image>();
        icImg.color = new Color(0.3f, 0.8f, 0.3f);

        var itemLabel = new GameObject("Item Label");
        itemLabel.transform.SetParent(itemGO.transform, false);
        var ilRT = itemLabel.AddComponent<RectTransform>();
        ilRT.anchorMin = Vector2.zero;
        ilRT.anchorMax = Vector2.one;
        ilRT.offsetMin = new Vector2(28, 2);
        ilRT.offsetMax = new Vector2(-4, -2);
        var ilText = itemLabel.AddComponent<Text>();
        ilText.font = _font;
        ilText.fontSize = 12;
        ilText.color = Color.white;
        ilText.alignment = TextAnchor.MiddleLeft;

        itemToggle.targetGraphic = ibImg;
        itemToggle.graphic = icImg;
        itemToggle.isOn = false;

        templateGO.SetActive(false);

        // Configure dropdown
        dropdown.targetGraphic = ddBG;
        dropdown.template = tempRT;
        dropdown.captionText = labelText;
        dropdown.itemText = ilText;

        // Populate options
        var options = new List<Dropdown.OptionData>();
        options.Add(new Dropdown.OptionData("(Empty)"));
        foreach (var spell in row.AvailableSpells)
        {
            string optLabel = spell.Name;
            if (spell.IsPlaceholder) optLabel += " [PH]";
            options.Add(new Dropdown.OptionData(optLabel));
        }
        dropdown.options = options;

        // Set current selection
        if (slot.HasSpell)
        {
            int spellIdx = row.AvailableSpells.FindIndex(s => s.SpellId == slot.PreparedSpell.SpellId);
            dropdown.value = spellIdx >= 0 ? spellIdx + 1 : 0;
        }
        else
        {
            dropdown.value = 0;
        }

        // Handle changes
        int capturedIndex = index;
        dropdown.onValueChanged.AddListener((value) => OnSlotChanged(capturedIndex, value));

        row.SpellDropdown = dropdown;
        return row;
    }

    // ========== EVENTS ==========

    private void OnSlotChanged(int slotIndex, int dropdownValue)
    {
        if (slotIndex < 0 || slotIndex >= _slotRows.Count) return;
        var row = _slotRows[slotIndex];

        if (dropdownValue == 0)
        {
            // Empty
            row.Slot.Prepare(null);
        }
        else
        {
            int spellIdx = dropdownValue - 1;
            if (spellIdx < row.AvailableSpells.Count)
            {
                row.Slot.Prepare(row.AvailableSpells[spellIdx]);
            }
        }

        _spellComp.SyncPreparedSpellsFromSlots();
        RefreshSummary();
    }

    private void OnAutoPrepare()
    {
        _spellComp.AutoPrepareWizardSlots();

        // Refresh dropdown selections
        foreach (var row in _slotRows)
        {
            if (row.SpellDropdown == null) continue;

            if (row.Slot.HasSpell)
            {
                int idx = row.AvailableSpells.FindIndex(s => s.SpellId == row.Slot.PreparedSpell.SpellId);
                row.SpellDropdown.value = idx >= 0 ? idx + 1 : 0;
            }
            else
            {
                row.SpellDropdown.value = 0;
            }
        }

        RefreshSummary();
    }

    private void OnConfirm()
    {
        // Sync everything
        _spellComp.SyncPreparedSpellsFromSlots();

        Debug.Log($"[SpellPreparationUI] {_spellComp.Stats.CharacterName} preparation confirmed: " +
                  _spellComp.GetWizardSlotDetails());

        Close();
        OnPreparationConfirmed?.Invoke();
    }

    // ========== REFRESH ==========

    private void RefreshSummary()
    {
        if (_spellComp == null || _spellComp.SlotsMax == null) return;

        var parts = new List<string>();
        for (int level = 0; level < _spellComp.SlotsMax.Length; level++)
        {
            var slotsAtLevel = _spellComp.GetSlotsForLevel(level);
            int filled = slotsAtLevel.Count(s => s.HasSpell);
            int total = slotsAtLevel.Count;
            string label = level == 0 ? "Cantrips" : $"Lv{level}";
            string color = filled >= total ? "#44FF44" : "#FFDD44";
            parts.Add($"<color={color}>{label}: {filled}/{total}</color>");
        }

        _slotSummaryText.text = string.Join("   |   ", parts);
        _slotSummaryText.supportRichText = true;
    }

    // ========== UI HELPERS ==========

    private Text MakeText(Transform parent, string name, Vector2 pos, Vector2 size,
        string text, int fontSize, Color color, TextAnchor align)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Text t = go.AddComponent<Text>();
        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = align;
        t.font = _font;
        t.supportRichText = true;
        return t;
    }

    private Button MakeButton(Transform parent, string name, Vector2 pos, Vector2 size,
        string label, Color bgColor, Color textColor, int fontSize)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = go.AddComponent<Image>();
        img.color = bgColor;

        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = bgColor;
        cb.highlightedColor = bgColor * 1.3f;
        cb.pressedColor = bgColor * 0.7f;
        cb.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        btn.colors = cb;

        var labelText = MakeText(go.transform, name + "Label", Vector2.zero, size,
            label, fontSize, textColor, TextAnchor.MiddleCenter);
        labelText.raycastTarget = false;

        return btn;
    }

    private GameObject MakePanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 pos, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        if (color.a > 0)
        {
            Image img = go.AddComponent<Image>();
            img.color = color;
        }

        return go;
    }
}
