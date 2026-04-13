using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Spell selection UI for character creation (Step 1: Build Spellbook).
/// Wizards: All cantrips auto-added to spellbook, then select higher-level spells.
/// Clerics: Select orisons (cantrips), higher-level spells are all available.
///
/// D&D 3.5e Spell Rules (PHB p.57):
///   - Wizards: All cantrips automatic, 3 + INT mod 1st-level, 2 2nd-level spells
///   - Clerics: Select 4 orisons, have access to all 1st &amp; 2nd level spells
///   - After spellbook is built, Step 2 (SpellPreparationUI) lets wizard prepare spells into slots.
///
/// Usage: Called from CharacterCreationUI after feat selection for spellcasters.
/// </summary>
public class SpellSelectionUI : MonoBehaviour
{
    // ========== PUBLIC ==========
    /// <summary>Callback when spell selection is confirmed. Returns list of selected SpellIds.</summary>
    public Action<List<string>> OnSpellsConfirmed;

    /// <summary>Whether the spell selection panel is currently open.</summary>
    public bool IsOpen { get; private set; }

    // ========== PRIVATE UI ==========
    private Font _font;
    private GameObject _rootPanel;
    private GameObject _overlayPanel;
    private CanvasGroup _canvasGroup;
    private Text _titleText;
    private Text _subtitleText;
    private Text _selectionCountText;
    private Text _spellDetailText;
    private Button _confirmButton;

    // Filter buttons
    private Button _filterAllButton;
    private Button _filter0Button;
    private Button _filter1Button;
    private Button _filter2Button;

    // Scroll area for spell list
    private GameObject _scrollContent;
    private RectTransform _scrollContentRT;

    // State
    private string _className;
    private int _intMod;
    private int _maxCantrips;    // Max cantrips/orisons to select (4 at level 3)
    private int _maxSpells1st;
    private int _maxSpells2nd;
    private int _currentFilterLevel = -1; // -1 = all, 0/1/2 = specific level
    private bool _cantripSelectionRequired; // true if player must select cantrips
    private int _autoAddedCantripCount;    // number of cantrips auto-added (wizard mode)

    private List<SpellData> _availableSpells = new List<SpellData>();
    private HashSet<string> _selectedSpellIds = new HashSet<string>();
    private Dictionary<string, SpellRowUI> _spellRows = new Dictionary<string, SpellRowUI>();

    // Layout constants
    private const float PANEL_W = 1020f;
    private const float PANEL_H = 720f;
    private const float ROW_H = 56f;
    private const float ROW_SPACING = 4f;
    private const float LIST_W = 460f;
    private const float DETAIL_W = 460f;
    private const float GAP = 20f; // gap between list and detail panel

    private class SpellRowUI
    {
        public GameObject Row;
        public Button SelectButton;
        public Text ButtonText;
        public Text NameText;
        public Text InfoText;
        public Image Background;
        public SpellData Spell;
        public bool IsSelected;
    }

    // ========== BUILD UI ==========

    public void BuildUI(Canvas canvas)
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // Dark overlay — fills entire screen
        _overlayPanel = MakePanel(canvas.transform, "SpellSelOverlay",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.85f));
        var overlayRT = _overlayPanel.GetComponent<RectTransform>();
        overlayRT.offsetMin = Vector2.zero;
        overlayRT.offsetMax = Vector2.zero;
        _canvasGroup = _overlayPanel.AddComponent<CanvasGroup>();

        // Main panel — centered, generous sizing
        _rootPanel = MakePanel(_overlayPanel.transform, "SpellSelPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(PANEL_W, PANEL_H), new Color(0.12f, 0.12f, 0.18f, 0.98f));

        // ── Header area (top 100px) ──
        float headerTop = PANEL_H / 2f;

        // Title
        _titleText = MakeText(_rootPanel.transform, "Title",
            new Vector2(0, headerTop - 30), new Vector2(PANEL_W - 40, 40),
            "SPELL SELECTION", 24, Color.white, TextAnchor.MiddleCenter);

        // Subtitle
        _subtitleText = MakeText(_rootPanel.transform, "Subtitle",
            new Vector2(0, headerTop - 62), new Vector2(PANEL_W - 40, 24),
            "", 14, new Color(0.7f, 0.8f, 1f), TextAnchor.MiddleCenter);

        // Selection counts (left-aligned under subtitle)
        _selectionCountText = MakeText(_rootPanel.transform, "Count",
            new Vector2(-PANEL_W / 6, headerTop - 90), new Vector2(PANEL_W * 0.55f, 24),
            "", 14, new Color(0.9f, 0.9f, 0.5f), TextAnchor.MiddleLeft);
        _selectionCountText.supportRichText = true;

        // Filter buttons (right side of header row, well-spaced, taller)
        float filterY = headerTop - 90;
        float filterBtnH = 30f;
        float filterBtnSpacing = 8f;
        float filterStartX = PANEL_W / 2 - 330; // right-aligned area

        _filterAllButton = MakeButton(_rootPanel.transform, "FilterAll",
            new Vector2(filterStartX, filterY), new Vector2(60, filterBtnH),
            "All", new Color(0.3f, 0.3f, 0.5f), Color.white, 13);
        _filterAllButton.onClick.AddListener(() => SetFilter(-1));

        _filter0Button = MakeButton(_rootPanel.transform, "Filter0",
            new Vector2(filterStartX + 60 + filterBtnSpacing, filterY), new Vector2(80, filterBtnH),
            "Cantrips", new Color(0.3f, 0.3f, 0.5f), Color.white, 12);
        _filter0Button.onClick.AddListener(() => SetFilter(0));

        _filter1Button = MakeButton(_rootPanel.transform, "Filter1",
            new Vector2(filterStartX + 60 + 80 + filterBtnSpacing * 2, filterY), new Vector2(60, filterBtnH),
            "1st", new Color(0.3f, 0.3f, 0.5f), Color.white, 13);
        _filter1Button.onClick.AddListener(() => SetFilter(1));

        _filter2Button = MakeButton(_rootPanel.transform, "Filter2",
            new Vector2(filterStartX + 60 + 80 + 60 + filterBtnSpacing * 3, filterY), new Vector2(60, filterBtnH),
            "2nd", new Color(0.3f, 0.3f, 0.5f), Color.white, 13);
        _filter2Button.onClick.AddListener(() => SetFilter(2));

        // ── Content area (below header, above confirm button) ──
        float contentTop = headerTop - 115;             // start below header area
        float contentBottom = -PANEL_H / 2f + 65;       // leave room for confirm button
        float contentH = contentTop - contentBottom;

        // Calculate left/right panel positions to avoid overlap
        float totalContentW = LIST_W + GAP + DETAIL_W;
        float listCenterX = -totalContentW / 2f + LIST_W / 2f;
        float detailCenterX = totalContentW / 2f - DETAIL_W / 2f;
        float contentCenterY = (contentTop + contentBottom) / 2f;

        // Scroll area for spell list (left side)
        GameObject scrollArea = MakePanel(_rootPanel.transform, "ScrollArea",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(listCenterX, contentCenterY), new Vector2(LIST_W, contentH),
            new Color(0.08f, 0.08f, 0.12f, 0.9f));

        // ScrollRect with proper viewport and mask
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollArea.transform, false);
        var vpRT = viewport.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = new Vector2(4, 4);   // small inner padding
        vpRT.offsetMax = new Vector2(-4, -4);
        // Mask image needs non-zero alpha so the stencil buffer is written;
        // showMaskGraphic = false hides the image visually while keeping the mask functional.
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

        // Vertical scrollbar for the spell list
        ScrollbarHelper.CreateVerticalScrollbar(scrollRect, scrollArea.transform);

        // Spell detail panel (right side) — no overlap with list
        GameObject detailPanel = MakePanel(_rootPanel.transform, "DetailPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(detailCenterX, contentCenterY), new Vector2(DETAIL_W, contentH),
            new Color(0.1f, 0.1f, 0.15f, 0.9f));

        _spellDetailText = MakeText(detailPanel.transform, "DetailText",
            new Vector2(0, 0), new Vector2(DETAIL_W - 30, contentH - 24),
            "Click a spell to see its details.", 14, new Color(0.85f, 0.85f, 0.8f), TextAnchor.UpperLeft);

        // Confirm button — clearly at the bottom with space around it
        _confirmButton = MakeButton(_rootPanel.transform, "ConfirmBtn",
            new Vector2(0, -PANEL_H / 2 + 32), new Vector2(280, 46),
            "Confirm Spell Selection ✓", new Color(0.2f, 0.6f, 0.2f), Color.white, 18);
        _confirmButton.onClick.AddListener(OnConfirm);

        _overlayPanel.SetActive(false);
    }

    // ========== OPEN / CLOSE ==========

    /// <summary>
    /// Open spell selection for Wizard (select spells for spellbook).
    /// D&D 3.5e PHB: All cantrips are automatically added to the spellbook.
    /// At 1st level: 3 + INT modifier 1st-level spells chosen.
    /// Each level after 1st: 2 additional spells of any level the wizard can cast.
    /// At level 3: All cantrips (auto) + (3 + INT mod) 1st-level + 2 2nd-level.
    /// </summary>
    public void OpenForWizard(int intModifier, int characterLevel)
    {
        _className = "Wizard";
        _intMod = intModifier;

        // D&D 3.5e PHB p.57: All cantrips are automatically in the spellbook (no selection needed)
        // At level 1: 3 + INT mod 1st-level spells
        // Levels 2-3: +2 spells each of any castable level → at level 3: 2 extra for 2nd-level
        _maxCantrips = 0; // Cantrips are auto-added, no selection needed
        _maxSpells1st = Mathf.Max(1, 3 + intModifier);
        _maxSpells2nd = 2;
        _cantripSelectionRequired = false; // Cantrips auto-added

        _selectedSpellIds.Clear();

        SpellDatabase.Init();
        _availableSpells.Clear();

        // Auto-add ALL wizard cantrips to the spellbook
        var allCantrips = SpellDatabase.GetSpellsForClassAtLevel("Wizard", 0);
        foreach (var cantrip in allCantrips)
        {
            _selectedSpellIds.Add(cantrip.SpellId);
        }
        _autoAddedCantripCount = allCantrips.Count;

        // Only show higher-level spells for selection
        _availableSpells.AddRange(SpellDatabase.GetSpellsForClassAtLevel("Wizard", 1));
        _availableSpells.AddRange(SpellDatabase.GetSpellsForClassAtLevel("Wizard", 2));

        // Sort: by level, then by name
        _availableSpells.Sort((a, b) =>
        {
            int cmp = a.SpellLevel.CompareTo(b.SpellLevel);
            return cmp != 0 ? cmp : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        });

        _titleText.text = "WIZARD SPELLBOOK SELECTION";
        _subtitleText.text = $"All {_autoAddedCantripCount} cantrips auto-added. Select {_maxSpells1st} 1st-level and {_maxSpells2nd} 2nd-level spells.";

        _overlayPanel.SetActive(true);
        IsOpen = true;

        _currentFilterLevel = -1;

        // Hide cantrip filter button when cantrips are auto-added
        if (_filter0Button != null)
            _filter0Button.gameObject.SetActive(false);

        PopulateSpellList();
        RefreshUI();
    }

    /// <summary>
    /// Open for Cleric — select 4 orisons, higher-level spells are all available.
    /// D&D 3.5e PHB: Clerics prepare from the full list, but only know 4 orisons at level 3.
    /// </summary>
    public void OpenForCleric()
    {
        _className = "Cleric";
        _maxCantrips = 4;
        _maxSpells1st = 0; // Clerics don't select higher-level spells (they know all)
        _maxSpells2nd = 0;
        _cantripSelectionRequired = true;
        _selectedSpellIds.Clear();

        SpellDatabase.Init();
        _availableSpells.Clear();
        _availableSpells.AddRange(SpellDatabase.GetSpellsForClassAtLevel("Cleric", 0));
        _availableSpells.AddRange(SpellDatabase.GetSpellsForClassAtLevel("Cleric", 1));
        _availableSpells.AddRange(SpellDatabase.GetSpellsForClassAtLevel("Cleric", 2));
        _availableSpells.Sort((a, b) =>
        {
            int cmp = a.SpellLevel.CompareTo(b.SpellLevel);
            return cmp != 0 ? cmp : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        });

        _titleText.text = "CLERIC SPELL SELECTION";
        _subtitleText.text = $"Select {_maxCantrips} orisons (cantrips). Higher-level spells are prepared from the full list each day.";

        _overlayPanel.SetActive(true);
        IsOpen = true;

        _currentFilterLevel = -1;
        _autoAddedCantripCount = 0;

        // Show cantrip filter button for cleric
        if (_filter0Button != null)
            _filter0Button.gameObject.SetActive(true);

        PopulateSpellList();
        RefreshUI();
    }

    /// <summary>Close the spell selection UI.</summary>
    public void Close()
    {
        if (_overlayPanel != null)
            _overlayPanel.SetActive(false);
        IsOpen = false;
    }

    // ========== POPULATE LIST ==========

    private void PopulateSpellList()
    {
        // Clear existing rows
        foreach (var row in _spellRows.Values)
        {
            if (row.Row != null) Destroy(row.Row);
        }
        _spellRows.Clear();

        // Also destroy any leftover headers
        foreach (Transform child in _scrollContent.transform)
        {
            Destroy(child.gameObject);
        }

        float yPos = -ROW_SPACING; // start with a small top margin
        int lastLevel = -1;

        foreach (var spell in _availableSpells)
        {
            // Filter
            if (_currentFilterLevel >= 0 && spell.SpellLevel != _currentFilterLevel)
                continue;

            // Level header
            if (spell.SpellLevel != lastLevel)
            {
                if (lastLevel >= 0) yPos -= 6; // extra spacing between level groups
                lastLevel = spell.SpellLevel;
                string cantripLabel = _className == "Cleric" ? "ORISONS" : "CANTRIPS";
                string headerLabel;
                if (spell.SpellLevel == 0)
                {
                    int selectCount = _cantripSelectionRequired ? _maxCantrips : 0;
                    headerLabel = selectCount > 0 ? $"═══ {cantripLabel} (Level 0) — Select {selectCount} ═══" :
                                                    $"═══ {cantripLabel} (Level 0) — All auto-added ═══";
                }
                else
                {
                    int maxAtLevel = spell.SpellLevel == 1 ? _maxSpells1st : _maxSpells2nd;
                    headerLabel = $"═══ LEVEL {spell.SpellLevel} SPELLS — Select {maxAtLevel} ═══";
                }
                var headerGO = new GameObject("Header" + spell.SpellLevel);
                headerGO.transform.SetParent(_scrollContent.transform, false);
                var hRT = headerGO.AddComponent<RectTransform>();
                hRT.anchorMin = new Vector2(0, 1);
                hRT.anchorMax = new Vector2(1, 1);
                hRT.pivot = new Vector2(0.5f, 1);
                hRT.anchoredPosition = new Vector2(0, yPos);
                hRT.sizeDelta = new Vector2(0, 30);
                var hText = headerGO.AddComponent<Text>();
                hText.text = headerLabel;
                hText.font = _font;
                hText.fontSize = 13;
                hText.color = new Color(0.7f, 0.8f, 1f);
                hText.alignment = TextAnchor.MiddleCenter;
                hText.supportRichText = true;
                hText.raycastTarget = false; // don't block clicks
                yPos -= 32;
            }

            // Spell row with spacing
            var rowUI = CreateSpellRow(spell, yPos);
            _spellRows[spell.SpellId] = rowUI;
            yPos -= (ROW_H + ROW_SPACING);
        }

        _scrollContentRT.sizeDelta = new Vector2(0, Mathf.Abs(yPos) + 12);
    }

    private SpellRowUI CreateSpellRow(SpellData spell, float yPos)
    {
        var row = new SpellRowUI();
        row.Spell = spell;

        // Row container
        row.Row = new GameObject("Row_" + spell.SpellId);
        row.Row.transform.SetParent(_scrollContent.transform, false);
        var rt = row.Row.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, yPos);
        rt.sizeDelta = new Vector2(0, ROW_H);

        // Background — acts as the row-click area to show spell details
        row.Background = row.Row.AddComponent<Image>();
        bool isCantrip = spell.SpellLevel == 0;
        row.Background.color = isCantrip ? new Color(0.12f, 0.14f, 0.12f, 0.6f) : new Color(0.1f, 0.1f, 0.15f, 0.6f);

        // Row button for showing detail (on the background).
        // IMPORTANT: Add this BEFORE child buttons so children are layered on top and
        // receive clicks first in Unity's event system.
        var rowButton = row.Row.AddComponent<Button>();
        rowButton.transition = Selectable.Transition.None;
        rowButton.targetGraphic = row.Background;
        string sid = spell.SpellId;
        rowButton.onClick.AddListener(() => ShowSpellDetail(sid));

        // Select button — shown for cantrips (both classes) and higher-level spells (Wizard only)
        bool showSelect = false;
        if (spell.SpellLevel == 0 && _cantripSelectionRequired)
            showSelect = true;
        else if (_className == "Wizard" && spell.SpellLevel > 0)
            showSelect = true;

        // Layout: [SelectBtn 48px] [8px gap] [Name/Info text fills remaining width]
        float selectBtnW = 48f;
        float selectBtnH = 42f;
        float textLeftPadding = 10f;
        float textStartX;

        if (showSelect)
        {
            // Select button positioned at the left edge of the row
            float btnX = -LIST_W / 2f + selectBtnW / 2f + 8f; // 8px left margin
            row.SelectButton = MakeButton(row.Row.transform, "SelBtn",
                new Vector2(btnX, 0), new Vector2(selectBtnW, selectBtnH),
                "○", new Color(0.2f, 0.2f, 0.35f), Color.white, 20);
            string spellId = spell.SpellId;
            row.SelectButton.onClick.AddListener(() => ToggleSpell(spellId));
            row.ButtonText = row.SelectButton.GetComponentInChildren<Text>();
            textStartX = btnX + selectBtnW / 2f + textLeftPadding;
        }
        else
        {
            textStartX = -LIST_W / 2f + 14f;
        }

        // Available width for text content
        float textW = LIST_W / 2f + (LIST_W / 2f - (textStartX + LIST_W / 2f)) - 10f;
        // Simplify: text goes from textStartX to right edge with 10px right padding
        float textRightEdge = LIST_W / 2f - 10f;
        float textCenterX = (textStartX + textRightEdge) / 2f;
        textW = textRightEdge - textStartX;

        // Spell name (upper portion of row)
        string placeholderTag = spell.IsPlaceholder ? " <color=#FF8800>★</color>" : "";
        string levelTag = spell.SpellLevel == 0 ? "<color=#888888>[C]</color>" :
                          $"<color=#AABBFF>[{spell.SpellLevel}]</color>";

        row.NameText = MakeText(row.Row.transform, "Name",
            new Vector2(textCenterX, 10), new Vector2(textW, 22),
            $"{levelTag} {spell.Name}{placeholderTag}", 14,
            spell.IsPlaceholder ? new Color(0.7f, 0.7f, 0.6f) : Color.white, TextAnchor.MiddleLeft);
        row.NameText.raycastTarget = false; // don't block row/button clicks

        // Info line (lower portion of row — school, effect)
        string effectStr = GetEffectString(spell);
        row.InfoText = MakeText(row.Row.transform, "Info",
            new Vector2(textCenterX, -10), new Vector2(textW, 18),
            $"<color=#888888>{spell.School}</color> | {effectStr}", 11,
            new Color(0.65f, 0.65f, 0.6f), TextAnchor.MiddleLeft);
        row.InfoText.raycastTarget = false; // don't block row/button clicks

        return row;
    }

    private string GetEffectString(SpellData spell)
    {
        switch (spell.EffectType)
        {
            case SpellEffectType.Damage:
                if (spell.AutoHit && spell.MissileCount > 0)
                    return $"<color=#FF6644>{spell.MissileCount}×(1d{spell.DamageDice}+{spell.BonusDamage}) {spell.DamageType}</color>";
                if (spell.DamageCount > 0)
                    return $"<color=#FF6644>{spell.DamageCount}d{spell.DamageDice}{(spell.BonusDamage > 0 ? "+" + spell.BonusDamage : "")} {spell.DamageType}</color>";
                if (spell.BonusDamage > 0)
                    return $"<color=#FF6644>{spell.BonusDamage} {spell.DamageType}</color>";
                return "<color=#FF6644>Damage</color>";

            case SpellEffectType.Healing:
                if (spell.HealCount > 0)
                    return $"<color=#44FF44>Heals {spell.HealCount}d{spell.HealDice}+{spell.BonusHealing}</color>";
                if (spell.BonusHealing > 0)
                    return $"<color=#44FF44>Heals {spell.BonusHealing}</color>";
                return "<color=#44FF44>Healing</color>";

            case SpellEffectType.Buff:
                var parts = new List<string>();
                if (spell.BuffACBonus > 0) parts.Add($"+{spell.BuffACBonus} AC");
                if (spell.BuffShieldBonus > 0) parts.Add($"+{spell.BuffShieldBonus} Shield");
                if (spell.BuffDeflectionBonus > 0) parts.Add($"+{spell.BuffDeflectionBonus} Deflect");
                if (spell.BuffAttackBonus > 0) parts.Add($"+{spell.BuffAttackBonus} Atk");
                if (spell.BuffDamageBonus > 0) parts.Add($"+{spell.BuffDamageBonus} Dmg");
                if (spell.BuffSaveBonus > 0) parts.Add($"+{spell.BuffSaveBonus} Saves");
                if (spell.BuffStatBonus > 0 && !string.IsNullOrEmpty(spell.BuffStatName))
                    parts.Add($"+{spell.BuffStatBonus} {spell.BuffStatName}");
                if (spell.BuffTempHP > 0) parts.Add($"+{spell.BuffTempHP} THP");
                string buffStr = parts.Count > 0 ? string.Join(", ", parts) : "Buff";
                return $"<color=#44AAFF>{buffStr}</color>";

            case SpellEffectType.Debuff:
                return "<color=#FFAA44>Debuff</color>";

            default:
                return "";
        }
    }

    // ========== SELECTION LOGIC ==========

    private void ToggleSpell(string spellId)
    {
        SpellData spell = SpellDatabase.GetSpell(spellId);
        if (spell == null) return;

        // Clerics can only toggle cantrips/orisons (level 0), not higher-level spells
        if (_className == "Cleric" && spell.SpellLevel > 0) return;

        if (_selectedSpellIds.Contains(spellId))
        {
            _selectedSpellIds.Remove(spellId);
        }
        else
        {
            // Check if we can select more at this level
            int currentCount = CountSelectedAtLevel(spell.SpellLevel);
            int maxCount;
            if (spell.SpellLevel == 0)
                maxCount = _maxCantrips;
            else if (spell.SpellLevel == 1)
                maxCount = _maxSpells1st;
            else
                maxCount = _maxSpells2nd;

            if (currentCount >= maxCount)
            {
                Debug.Log($"[SpellSelectionUI] Cannot select more level {spell.SpellLevel} spells (max {maxCount})");
                return;
            }

            _selectedSpellIds.Add(spellId);
        }

        RefreshUI();
    }

    private int CountSelectedAtLevel(int level)
    {
        int count = 0;
        foreach (string id in _selectedSpellIds)
        {
            SpellData s = SpellDatabase.GetSpell(id);
            if (s != null && s.SpellLevel == level) count++;
        }
        return count;
    }

    // ========== REFRESH ==========

    private void RefreshUI()
    {
        int sel0 = CountSelectedAtLevel(0);
        int sel1 = CountSelectedAtLevel(1);
        int sel2 = CountSelectedAtLevel(2);

        if (_className == "Wizard")
        {
            if (_autoAddedCantripCount > 0)
            {
                // New mode: cantrips auto-added, only selecting higher-level spells
                string c1 = sel1 >= _maxSpells1st ? "#44FF44" : "#FFDD44";
                string c2 = sel2 >= _maxSpells2nd ? "#44FF44" : "#FFDD44";
                _selectionCountText.text = $"<color=#44FF44>Cantrips: {_autoAddedCantripCount} (all auto-added)</color>   |   " +
                                           $"<color={c1}>1st Level: {sel1}/{_maxSpells1st}</color>   |   " +
                                           $"<color={c2}>2nd Level: {sel2}/{_maxSpells2nd}</color>";

                bool allSelected = (sel1 >= _maxSpells1st && sel2 >= _maxSpells2nd);
                _confirmButton.interactable = allSelected;
            }
            else
            {
                // Legacy mode: selecting cantrips too
                string c0 = sel0 >= _maxCantrips ? "#44FF44" : "#FFDD44";
                string c1 = sel1 >= _maxSpells1st ? "#44FF44" : "#FFDD44";
                string c2 = sel2 >= _maxSpells2nd ? "#44FF44" : "#FFDD44";
                _selectionCountText.text = $"<color={c0}>Cantrips: {sel0}/{_maxCantrips}</color>   |   " +
                                           $"<color={c1}>1st Level: {sel1}/{_maxSpells1st}</color>   |   " +
                                           $"<color={c2}>2nd Level: {sel2}/{_maxSpells2nd}</color>";

                bool allSelected = (sel0 >= _maxCantrips && sel1 >= _maxSpells1st && sel2 >= _maxSpells2nd);
                _confirmButton.interactable = allSelected;
            }
        }
        else if (_className == "Cleric")
        {
            string c0 = sel0 >= _maxCantrips ? "#44FF44" : "#FFDD44";
            _selectionCountText.text = $"<color={c0}>Orisons: {sel0}/{_maxCantrips}</color>   |   " +
                                       "1st & 2nd level spells: All available";
            _confirmButton.interactable = (sel0 >= _maxCantrips);
        }
        else
        {
            _selectionCountText.text = "All spells available";
            _confirmButton.interactable = true;
        }

        // Update spell row visuals
        foreach (var kvp in _spellRows)
        {
            var row = kvp.Value;
            bool isSelected = _selectedSpellIds.Contains(kvp.Key);
            bool isCantrip = row.Spell.SpellLevel == 0;
            row.IsSelected = isSelected;

            if (row.SelectButton != null)
            {
                row.ButtonText.text = isSelected ? "✓" : "○";
                var colors = row.SelectButton.colors;
                colors.normalColor = isSelected ? new Color(0.15f, 0.45f, 0.15f) : new Color(0.2f, 0.2f, 0.35f);
                colors.highlightedColor = colors.normalColor * 1.2f;
                row.SelectButton.colors = colors;

                // Disable if max reached and not selected
                if (!isSelected)
                {
                    int lvl = row.Spell.SpellLevel;
                    int max;
                    if (lvl == 0) max = _maxCantrips;
                    else if (lvl == 1) max = _maxSpells1st;
                    else max = _maxSpells2nd;
                    row.SelectButton.interactable = CountSelectedAtLevel(lvl) < max;
                }
                else
                {
                    row.SelectButton.interactable = true;
                }
            }

            // Row background
            if (isSelected)
                row.Background.color = isCantrip ? new Color(0.12f, 0.22f, 0.12f, 0.8f) : new Color(0.12f, 0.22f, 0.12f, 0.8f);
            else if (isCantrip)
                row.Background.color = new Color(0.12f, 0.14f, 0.12f, 0.6f);
            else
                row.Background.color = new Color(0.1f, 0.1f, 0.15f, 0.6f);
        }

        // Update filter button colors
        UpdateFilterButtons();
    }

    private void UpdateFilterButtons()
    {
        Button[] buttons = { _filterAllButton, _filter0Button, _filter1Button, _filter2Button };
        int[] levels = { -1, 0, 1, 2 };

        for (int i = 0; i < buttons.Length; i++)
        {
            var c = buttons[i].colors;
            c.normalColor = (levels[i] == _currentFilterLevel) ?
                new Color(0.4f, 0.4f, 0.15f) : new Color(0.3f, 0.3f, 0.5f);
            c.highlightedColor = c.normalColor * 1.2f;
            buttons[i].colors = c;
        }
    }

    private void SetFilter(int level)
    {
        _currentFilterLevel = level;
        PopulateSpellList();
        RefreshUI();
    }

    // ========== DETAIL ==========

    private void ShowSpellDetail(string spellId)
    {
        SpellData spell = SpellDatabase.GetSpell(spellId);
        if (spell == null) return;

        string detail = "";
        string levelStr = spell.SpellLevel == 0 ? "Cantrip" : $"Level {spell.SpellLevel}";
        string rangeStr = spell.RangeSquares < 0 ? "Self" :
                          spell.RangeSquares == 0 ? "Touch" :
                          spell.RangeSquares == 1 ? "Touch (1 sq)" :
                          $"{spell.RangeSquares} sq ({spell.RangeSquares * 5} ft)";

        detail += $"<color=#FFDD44><b>{spell.Name}</b></color>\n";
        detail += $"<color=#AABBFF>{levelStr} | {spell.School}</color>\n\n";
        detail += $"{spell.Description}\n\n";

        // Mechanics
        detail += "<color=#CCCCCC>--- Mechanics ---</color>\n";
        detail += $"Range: {rangeStr}\n";

        if (spell.EffectType == SpellEffectType.Damage)
        {
            if (spell.AutoHit && spell.MissileCount > 0)
                detail += $"Damage: {spell.MissileCount} × (1d{spell.DamageDice}+{spell.BonusDamage}) {spell.DamageType}\n";
            else if (spell.DamageCount > 0)
                detail += $"Damage: {spell.DamageCount}d{spell.DamageDice}{(spell.BonusDamage > 0 ? "+" + spell.BonusDamage : "")} {spell.DamageType}\n";
            else if (spell.BonusDamage > 0)
                detail += $"Damage: {spell.BonusDamage} {spell.DamageType}\n";
        }

        if (spell.EffectType == SpellEffectType.Healing)
        {
            if (spell.HealCount > 0)
                detail += $"Healing: {spell.HealCount}d{spell.HealDice}+{spell.BonusHealing}\n";
            else if (spell.BonusHealing > 0)
                detail += $"Healing: {spell.BonusHealing}\n";
        }

        if (spell.AllowsSavingThrow)
            detail += $"Save: {spell.SavingThrowType}{(spell.SaveHalves ? " (half)" : " (negates)")}\n";

        if (spell.BuffDurationRounds != 0)
        {
            string durStr = spell.BuffDurationRounds < 0 ? "Hours/level" :
                            spell.BuffDurationRounds == 1 ? "1 round" :
                            $"{spell.BuffDurationRounds} rounds";
            detail += $"Duration: {durStr}\n";
        }

        if (spell.AreaRadius > 0)
            detail += $"Area: {spell.AreaRadius} sq radius\n";

        detail += $"Action: {spell.ActionType}\n";
        detail += $"Provokes AoO: {(spell.ProvokesAoO ? "Yes" : "No")}\n";

        // Buff details
        if (spell.EffectType == SpellEffectType.Buff || spell.EffectType == SpellEffectType.Debuff)
        {
            var buffParts = new List<string>();
            if (spell.BuffACBonus != 0) buffParts.Add($"AC {spell.BuffACBonus:+#;-#} ({spell.BuffType})");
            if (spell.BuffShieldBonus != 0) buffParts.Add($"Shield AC {spell.BuffShieldBonus:+#;-#}");
            if (spell.BuffDeflectionBonus != 0) buffParts.Add($"Deflection AC {spell.BuffDeflectionBonus:+#;-#}");
            if (spell.BuffAttackBonus != 0) buffParts.Add($"Attack {spell.BuffAttackBonus:+#;-#}");
            if (spell.BuffDamageBonus != 0) buffParts.Add($"Damage {spell.BuffDamageBonus:+#;-#}");
            if (spell.BuffSaveBonus != 0) buffParts.Add($"Saves {spell.BuffSaveBonus:+#;-#}");
            if (spell.BuffStatBonus != 0 && !string.IsNullOrEmpty(spell.BuffStatName))
                buffParts.Add($"{spell.BuffStatName} {spell.BuffStatBonus:+#;-#}");
            if (spell.BuffTempHP > 0) buffParts.Add($"Temp HP +{spell.BuffTempHP}");

            if (buffParts.Count > 0)
                detail += $"\nEffects: {string.Join(", ", buffParts)}\n";
        }

        // Status
        if (spell.IsPlaceholder)
        {
            detail += $"\n<color=#FF8800>⚠ {spell.PlaceholderReason}</color>\n";
        }
        else
        {
            detail += "\n<color=#44FF44>✓ FUNCTIONAL — Full mechanics implemented</color>\n";
        }

        // Target type
        detail += $"\nTarget: {spell.TargetType}";

        _spellDetailText.text = detail;
    }

    // ========== CONFIRM ==========

    private void OnConfirm()
    {
        List<string> result = new List<string>(_selectedSpellIds);

        int cantripCount = 0;
        int spell1Count = 0;
        int spell2Count = 0;
        foreach (string id in result)
        {
            SpellData s = SpellDatabase.GetSpell(id);
            if (s == null) continue;
            if (s.SpellLevel == 0) cantripCount++;
            else if (s.SpellLevel == 1) spell1Count++;
            else if (s.SpellLevel == 2) spell2Count++;
        }

        Debug.Log($"[SpellSelectionUI] Confirmed {result.Count} spells for {_className}: " +
                  $"{cantripCount} cantrips, {spell1Count} 1st-level, {spell2Count} 2nd-level");

        Close();

        if (OnSpellsConfirmed != null)
            OnSpellsConfirmed.Invoke(result);
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
        labelText.raycastTarget = false; // label should not intercept clicks from the button

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
