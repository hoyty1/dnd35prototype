using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen UI that lets the player build a custom encounter by selecting
/// creatures from the NPCDatabase with quantity counters, alphabetical sorting,
/// and CR filtering.
/// </summary>
public class CustomEncounterBuilderUI : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════════
    //  COLORS
    // ═══════════════════════════════════════════════════════════════════
    private static readonly Color PanelBg        = new Color(0.08f, 0.08f, 0.12f, 0.97f);
    private static readonly Color ScrollAreaBg   = new Color(0.05f, 0.06f, 0.1f, 0.95f);
    private static readonly Color RowNormal      = new Color(0.14f, 0.16f, 0.24f, 0.96f);
    private static readonly Color RowSelected    = new Color(0.22f, 0.30f, 0.48f, 1f);
    private static readonly Color HeaderColor    = new Color(0.98f, 0.83f, 0.35f, 1f);
    private static readonly Color BtnGreen       = new Color(0.2f, 0.50f, 0.28f, 1f);
    private static readonly Color BtnRed         = new Color(0.50f, 0.2f, 0.2f, 1f);
    private static readonly Color BtnBlue        = new Color(0.3f, 0.38f, 0.62f, 1f);
    private static readonly Color CounterBtnColor = new Color(0.28f, 0.34f, 0.52f, 1f);

    // ═══════════════════════════════════════════════════════════════════
    //  STATE
    // ═══════════════════════════════════════════════════════════════════
    private GameObject _panel;
    private RectTransform _contentContainer;
    private ScrollRect _scrollRect;
    private Text _summaryText;
    private Text _errorText;
    private Button _startButton;
    private Dropdown _crFilterDropdown;
    private InputField _crValueInput;

    private Action<List<string>> _onStartCombat;
    private Action _onBack;

    /// <summary>Creature ID → selected quantity.</summary>
    private readonly Dictionary<string, int> _selectedCounts = new Dictionary<string, int>();
    /// <summary>Cached creature rows for rebuilding.</summary>
    private readonly List<CreatureEntry> _allCreatures = new List<CreatureEntry>();
    /// <summary>Row GameObjects keyed by creature ID for quantity label updates.</summary>
    private readonly Dictionary<string, Text> _quantityLabels = new Dictionary<string, Text>();

    private int _maxTotalCreatures = 15; // Hard cap based on NPC slot availability

    public bool IsOpen => _panel != null && _panel.activeSelf;

    // ═══════════════════════════════════════════════════════════════════
    //  DATA
    // ═══════════════════════════════════════════════════════════════════

    private struct CreatureEntry
    {
        public string Id;
        public string Name;
        public string ChallengeRating;
        public float CRNumeric;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Open the custom encounter builder UI.
    /// </summary>
    /// <param name="maxSlots">Maximum NPC slots available (NPCs.Count).</param>
    /// <param name="onStartCombat">Callback with the list of NPC IDs to spawn.</param>
    /// <param name="onBack">Callback when the user presses Back.</param>
    public void Open(int maxSlots, Action<List<string>> onStartCombat, Action onBack)
    {
        _onStartCombat = onStartCombat;
        _onBack = onBack;
        _maxTotalCreatures = Mathf.Max(1, maxSlots);
        _selectedCounts.Clear();

        EnsureBuilt();
        LoadCreatures();
        RebuildCreatureList();
        UpdateSummary();

        if (_panel != null) _panel.SetActive(true);
    }

    public void Close()
    {
        if (_panel != null) _panel.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  BUILD UI
    // ═══════════════════════════════════════════════════════════════════

    private void EnsureBuilt()
    {
        if (_panel != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[CustomEncounterBuilderUI] No Canvas found.");
            return;
        }

        // ── Full-screen panel ──
        _panel = new GameObject("CustomEncounterBuilderPanel", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = (RectTransform)_panel.transform;
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        _panel.GetComponent<Image>().color = PanelBg;

        // ── Title ──
        CreateLabel(_panel.transform, "CUSTOM ENCOUNTER BUILDER", 32, FontStyle.Bold, Color.white,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -20f), new Vector2(700f, 40f), TextAnchor.MiddleCenter);

        // ── Filter bar ──
        BuildFilterBar();

        // ── Scroll area ──
        BuildScrollArea();

        // ── Summary + error ──
        CreateLabel(_panel.transform, "", 18, FontStyle.Normal, new Color(0.85f, 0.89f, 0.95f, 1f),
            new Vector2(0.08f, 0.12f), new Vector2(0.7f, 0.17f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleLeft, out _summaryText);

        CreateLabel(_panel.transform, "", 16, FontStyle.Italic, new Color(1f, 0.4f, 0.4f, 1f),
            new Vector2(0.08f, 0.07f), new Vector2(0.7f, 0.12f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleLeft, out _errorText);

        // ── Footer buttons ──
        BuildFooterButtons();

        _panel.SetActive(false);
    }

    private void BuildFilterBar()
    {
        GameObject filterBar = new GameObject("FilterBar", typeof(RectTransform));
        filterBar.transform.SetParent(_panel.transform, false);
        RectTransform filterRect = filterBar.GetComponent<RectTransform>();
        filterRect.anchorMin = new Vector2(0.08f, 0.87f);
        filterRect.anchorMax = new Vector2(0.92f, 0.93f);
        filterRect.offsetMin = Vector2.zero;
        filterRect.offsetMax = Vector2.zero;

        // ── CR Filter Dropdown ──
        string[] crOptions = { "Any CR", "CR ≤ X", "CR = X", "CR ≥ X" };
        GameObject dropdownObj = CreateDropdown(filterBar.transform, crOptions, 0, OnCRFilterChanged);
        RectTransform ddRect = dropdownObj.GetComponent<RectTransform>();
        ddRect.anchorMin = new Vector2(0f, 0f);
        ddRect.anchorMax = new Vector2(0.35f, 1f);
        ddRect.offsetMin = Vector2.zero;
        ddRect.offsetMax = Vector2.zero;
        _crFilterDropdown = dropdownObj.GetComponent<Dropdown>();

        // ── CR Value Input ──
        GameObject inputObj = CreateInputField(filterBar.transform, "CR value...");
        RectTransform inputRect = inputObj.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0.37f, 0f);
        inputRect.anchorMax = new Vector2(0.55f, 1f);
        inputRect.offsetMin = Vector2.zero;
        inputRect.offsetMax = Vector2.zero;
        _crValueInput = inputObj.GetComponent<InputField>();
        _crValueInput.onEndEdit.AddListener(_ => RebuildCreatureList());

        // ── Hint label ──
        CreateLabel(filterBar.transform, "Sorted alphabetically • Use filters to narrow list", 14,
            FontStyle.Italic, new Color(0.6f, 0.65f, 0.75f, 1f),
            new Vector2(0.57f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleLeft);
    }

    private void BuildScrollArea()
    {
        GameObject scrollRoot = new GameObject("ScrollRoot", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollRoot.transform.SetParent(_panel.transform, false);
        RectTransform scrollRootRect = scrollRoot.GetComponent<RectTransform>();
        scrollRootRect.anchorMin = new Vector2(0.08f, 0.18f);
        scrollRootRect.anchorMax = new Vector2(0.92f, 0.86f);
        scrollRootRect.offsetMin = Vector2.zero;
        scrollRootRect.offsetMax = Vector2.zero;
        scrollRoot.GetComponent<Image>().color = ScrollAreaBg;

        _scrollRect = scrollRoot.GetComponent<ScrollRect>();
        _scrollRect.horizontal = false;
        _scrollRect.vertical = true;
        _scrollRect.movementType = ScrollRect.MovementType.Clamped;
        _scrollRect.scrollSensitivity = 24f;

        // Viewport
        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollRoot.transform, false);
        RectTransform vpRect = viewport.GetComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = new Vector2(8f, 8f);
        vpRect.offsetMax = new Vector2(-24f, -8f);
        viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.1f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        // Content
        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        _contentContainer = content.GetComponent<RectTransform>();
        _contentContainer.anchorMin = new Vector2(0f, 1f);
        _contentContainer.anchorMax = new Vector2(1f, 1f);
        _contentContainer.pivot = new Vector2(0.5f, 1f);
        _contentContainer.anchoredPosition = Vector2.zero;
        _contentContainer.sizeDelta = Vector2.zero;

        VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 3f;
        vlg.padding = new RectOffset(4, 4, 4, 4);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlHeight = false;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = content.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        _scrollRect.viewport = vpRect;
        _scrollRect.content = _contentContainer;

        // Scrollbar
        BuildScrollbar(scrollRoot.transform);
    }

    private void BuildScrollbar(Transform parent)
    {
        GameObject sbObj = new GameObject("VScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        sbObj.transform.SetParent(parent, false);
        RectTransform sbRect = sbObj.GetComponent<RectTransform>();
        sbRect.anchorMin = new Vector2(1f, 0f);
        sbRect.anchorMax = new Vector2(1f, 1f);
        sbRect.pivot = new Vector2(1f, 1f);
        sbRect.offsetMin = new Vector2(-16f, 8f);
        sbRect.offsetMax = new Vector2(-4f, -8f);
        sbObj.GetComponent<Image>().color = new Color(0.15f, 0.17f, 0.24f, 0.95f);

        GameObject sliding = new GameObject("SlidingArea", typeof(RectTransform));
        sliding.transform.SetParent(sbObj.transform, false);
        RectTransform slidingRect = sliding.GetComponent<RectTransform>();
        slidingRect.anchorMin = Vector2.zero;
        slidingRect.anchorMax = Vector2.one;
        slidingRect.offsetMin = new Vector2(0f, 6f);
        slidingRect.offsetMax = new Vector2(0f, -6f);

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(sliding.transform, false);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 1f);
        handleRect.anchorMax = new Vector2(1f, 1f);
        handleRect.pivot = new Vector2(0.5f, 1f);
        handleRect.sizeDelta = new Vector2(0f, 52f);
        handle.GetComponent<Image>().color = new Color(0.54f, 0.67f, 0.95f, 0.95f);

        Scrollbar sb = sbObj.GetComponent<Scrollbar>();
        sb.direction = Scrollbar.Direction.BottomToTop;
        sb.targetGraphic = handle.GetComponent<Image>();
        sb.handleRect = handleRect;
        sb.size = 0.24f;

        _scrollRect.verticalScrollbar = sb;
        _scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
    }

    private void BuildFooterButtons()
    {
        GameObject footer = new GameObject("Footer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        footer.transform.SetParent(_panel.transform, false);
        RectTransform footerRect = footer.GetComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0.55f, 0.03f);
        footerRect.anchorMax = new Vector2(0.92f, 0.10f);
        footerRect.offsetMin = Vector2.zero;
        footerRect.offsetMax = Vector2.zero;

        HorizontalLayoutGroup hlg = footer.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        CreateButton(footer.transform, "Clear All", BtnRed, OnClearAll, out _);
        CreateButton(footer.transform, "Back", BtnBlue, OnBackPressed, out _);
        CreateButton(footer.transform, "Start Combat", BtnGreen, OnStartCombat, out _startButton);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CREATURE LOADING
    // ═══════════════════════════════════════════════════════════════════

    private void LoadCreatures()
    {
        _allCreatures.Clear();
        NPCDatabase.Init();

        foreach (NPCDefinition def in NPCDatabase.AllNPCs)
        {
            if (def == null || string.IsNullOrEmpty(def.Id)) continue;

            // Skip test-only and alias creatures
            if (IsTestCreature(def)) continue;

            _allCreatures.Add(new CreatureEntry
            {
                Id = def.Id,
                Name = def.Name ?? def.Id,
                ChallengeRating = def.ChallengeRating ?? "—",
                CRNumeric = ParseCRToFloat(def.ChallengeRating)
            });
        }

        // Sort alphabetically
        _allCreatures.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsTestCreature(NPCDefinition def)
    {
        string id = def.Id ?? "";
        // Skip obvious test/drill creatures
        if (id.Contains("_test") || id.Contains("_drill") || id.Contains("test_")
            || id.Contains("pinata") || id.Contains("target_dummy")
            || id.Contains("grease_test"))
            return true;
        return false;
    }

    /// <summary>
    /// Parse a D&D 3.5e CR string (e.g. "1/8", "1/4", "1/2", "3") to a float.
    /// </summary>
    public static float ParseCRToFloat(string cr)
    {
        if (string.IsNullOrEmpty(cr)) return 0f;
        cr = cr.Trim();
        if (cr.Contains("/"))
        {
            string[] parts = cr.Split('/');
            if (parts.Length == 2
                && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float num)
                && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float den)
                && den != 0f)
                return num / den;
            return 0f;
        }
        if (float.TryParse(cr, NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
            return val;
        return 0f;
    }

    /// <summary>
    /// Format a numeric CR back to a display string (handles fractions).
    /// </summary>
    private static string FormatCR(float cr)
    {
        if (cr <= 0.124f) return "1/8";
        if (cr <= 0.26f)  return "1/4";
        if (cr <= 0.34f)  return "1/3";
        if (cr <= 0.51f)  return "1/2";
        return cr.ToString("0.##", CultureInfo.InvariantCulture);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CREATURE LIST
    // ═══════════════════════════════════════════════════════════════════

    private void RebuildCreatureList()
    {
        if (_contentContainer == null) return;

        _quantityLabels.Clear();

        // Clear existing children
        for (int i = _contentContainer.childCount - 1; i >= 0; i--)
            Destroy(_contentContainer.GetChild(i).gameObject);

        List<CreatureEntry> filtered = ApplyCRFilter(_allCreatures);

        if (filtered.Count == 0)
        {
            CreateLabel(_contentContainer, "No creatures match the current filter.", 18,
                FontStyle.Italic, new Color(0.7f, 0.7f, 0.7f, 1f),
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(0f, 40f), TextAnchor.MiddleCenter);
            return;
        }

        // Column header
        CreateCreatureRowHeader();

        for (int i = 0; i < filtered.Count; i++)
        {
            CreateCreatureRow(filtered[i], i);
        }

        if (_scrollRect != null) _scrollRect.verticalNormalizedPosition = 1f;
    }

    private void CreateCreatureRowHeader()
    {
        GameObject row = new GameObject("RowHeader", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        row.transform.SetParent(_contentContainer, false);
        row.GetComponent<Image>().color = new Color(0.1f, 0.11f, 0.16f, 0.95f);
        row.GetComponent<LayoutElement>().preferredHeight = 30f;
        row.GetComponent<LayoutElement>().minHeight = 30f;

        // Name
        CreateLabel(row.transform, "CREATURE", 14, FontStyle.Bold, HeaderColor,
            new Vector2(0.02f, 0f), new Vector2(0.55f, 1f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleLeft);

        // CR
        CreateLabel(row.transform, "CR", 14, FontStyle.Bold, HeaderColor,
            new Vector2(0.55f, 0f), new Vector2(0.70f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);

        // Quantity
        CreateLabel(row.transform, "QTY", 14, FontStyle.Bold, HeaderColor,
            new Vector2(0.70f, 0f), new Vector2(0.98f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
    }

    private void CreateCreatureRow(CreatureEntry entry, int index)
    {
        int currentQty = 0;
        _selectedCounts.TryGetValue(entry.Id, out currentQty);

        Color bgColor = currentQty > 0 ? RowSelected : RowNormal;
        if (index % 2 == 1 && currentQty == 0)
            bgColor = new Color(RowNormal.r + 0.02f, RowNormal.g + 0.02f, RowNormal.b + 0.02f, RowNormal.a);

        GameObject row = new GameObject($"Row_{entry.Id}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        row.transform.SetParent(_contentContainer, false);
        row.GetComponent<Image>().color = bgColor;
        LayoutElement le = row.GetComponent<LayoutElement>();
        le.preferredHeight = 36f;
        le.minHeight = 36f;

        // Name
        CreateLabel(row.transform, entry.Name, 16, FontStyle.Normal, Color.white,
            new Vector2(0.02f, 0f), new Vector2(0.55f, 1f), new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleLeft);

        // CR
        CreateLabel(row.transform, $"CR {entry.ChallengeRating}", 15, FontStyle.Normal,
            new Color(0.9f, 0.85f, 0.6f, 1f),
            new Vector2(0.55f, 0f), new Vector2(0.70f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);

        // ── Quantity controls ──
        // Minus button
        string capturedId = entry.Id;
        CreateSmallButton(row.transform, "−", new Vector2(0.72f, 0.1f), new Vector2(0.78f, 0.9f),
            CounterBtnColor, () => AdjustQuantity(capturedId, -1));

        // Quantity label
        Text qtyLabel;
        CreateLabel(row.transform, currentQty.ToString(), 18, FontStyle.Bold,
            currentQty > 0 ? new Color(0.4f, 1f, 0.5f, 1f) : Color.white,
            new Vector2(0.79f, 0f), new Vector2(0.89f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, out qtyLabel);
        _quantityLabels[entry.Id] = qtyLabel;

        // Plus button
        CreateSmallButton(row.transform, "+", new Vector2(0.90f, 0.1f), new Vector2(0.96f, 0.9f),
            CounterBtnColor, () => AdjustQuantity(capturedId, +1));
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CR FILTERING
    // ═══════════════════════════════════════════════════════════════════

    private List<CreatureEntry> ApplyCRFilter(List<CreatureEntry> creatures)
    {
        if (_crFilterDropdown == null || _crFilterDropdown.value == 0)
            return new List<CreatureEntry>(creatures); // "Any CR"

        float targetCR = 0f;
        if (_crValueInput != null && !string.IsNullOrWhiteSpace(_crValueInput.text))
            targetCR = ParseCRToFloat(_crValueInput.text);

        int filterMode = _crFilterDropdown.value; // 1=≤, 2==, 3=≥
        List<CreatureEntry> result = new List<CreatureEntry>();

        for (int i = 0; i < creatures.Count; i++)
        {
            float cr = creatures[i].CRNumeric;
            bool match = false;
            switch (filterMode)
            {
                case 1: match = cr <= targetCR + 0.001f; break; // CR ≤ X
                case 2: match = Mathf.Abs(cr - targetCR) < 0.001f; break; // CR = X
                case 3: match = cr >= targetCR - 0.001f; break; // CR ≥ X
            }
            if (match) result.Add(creatures[i]);
        }
        return result;
    }

    private void OnCRFilterChanged(int newValue)
    {
        RebuildCreatureList();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  QUANTITY MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════

    private void AdjustQuantity(string creatureId, int delta)
    {
        int current = 0;
        _selectedCounts.TryGetValue(creatureId, out current);

        int totalSelected = GetTotalSelectedCount();
        int newVal = Mathf.Clamp(current + delta, 0, _maxTotalCreatures);

        // Enforce total cap
        if (delta > 0 && totalSelected >= _maxTotalCreatures)
        {
            ShowError($"Maximum {_maxTotalCreatures} creatures allowed (limited by NPC slots).");
            return;
        }

        if (newVal <= 0)
            _selectedCounts.Remove(creatureId);
        else
            _selectedCounts[creatureId] = newVal;

        ClearError();

        // Update just the quantity label and row color without rebuilding everything
        if (_quantityLabels.TryGetValue(creatureId, out Text label))
        {
            int qty = 0;
            _selectedCounts.TryGetValue(creatureId, out qty);
            label.text = qty.ToString();
            label.color = qty > 0 ? new Color(0.4f, 1f, 0.5f, 1f) : Color.white;

            // Update row background
            Image rowImage = label.transform.parent.GetComponent<Image>();
            if (rowImage != null)
                rowImage.color = qty > 0 ? RowSelected : RowNormal;
        }

        UpdateSummary();
    }

    private int GetTotalSelectedCount()
    {
        int total = 0;
        foreach (var kv in _selectedCounts)
            total += kv.Value;
        return total;
    }

    private void UpdateSummary()
    {
        if (_summaryText == null) return;

        int totalCount = GetTotalSelectedCount();
        int uniqueTypes = _selectedCounts.Count;

        if (totalCount == 0)
        {
            _summaryText.text = "No creatures selected. Use + buttons to add creatures.";
            if (_startButton != null) _startButton.interactable = false;
            return;
        }

        // Build summary string
        List<string> parts = new List<string>();
        foreach (var kv in _selectedCounts)
        {
            if (kv.Value <= 0) continue;
            NPCDefinition def = NPCDatabase.Get(kv.Key);
            string name = def != null ? def.Name : kv.Key;
            parts.Add(kv.Value > 1 ? $"{kv.Value}× {name}" : name);
        }

        _summaryText.text = $"Selected: {totalCount} creature{(totalCount != 1 ? "s" : "")} ({uniqueTypes} type{(uniqueTypes != 1 ? "s" : "")}) — {string.Join(", ", parts)}";
        if (_startButton != null) _startButton.interactable = true;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ERROR HANDLING
    // ═══════════════════════════════════════════════════════════════════

    private void ShowError(string msg)
    {
        if (_errorText != null) _errorText.text = msg;
    }

    private void ClearError()
    {
        if (_errorText != null) _errorText.text = "";
    }

    // ═══════════════════════════════════════════════════════════════════
    //  BUTTON CALLBACKS
    // ═══════════════════════════════════════════════════════════════════

    private void OnClearAll()
    {
        _selectedCounts.Clear();
        RebuildCreatureList();
        UpdateSummary();
        ClearError();
    }

    private void OnBackPressed()
    {
        Close();
        _onBack?.Invoke();
    }

    private void OnStartCombat()
    {
        int total = GetTotalSelectedCount();
        if (total == 0)
        {
            ShowError("No creatures selected! Add at least one creature.");
            return;
        }
        if (total > _maxTotalCreatures)
        {
            ShowError($"Too many creatures selected ({total}). Maximum is {_maxTotalCreatures}.");
            return;
        }

        // Build the list of NPC IDs
        List<string> enemyIds = new List<string>();
        foreach (var kv in _selectedCounts)
        {
            for (int i = 0; i < kv.Value; i++)
                enemyIds.Add(kv.Key);
        }

        Debug.Log($"[CustomEncounterBuilder] Starting combat with {enemyIds.Count} creatures: {string.Join(", ", enemyIds)}");

        Close();
        _onStartCombat?.Invoke(enemyIds);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  NON-OVERLAPPING SPAWN POSITION CALCULATOR
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Calculate non-overlapping spawn positions for the given number of creatures,
    /// ensuring minimum distance between them and valid grid positions.
    /// </summary>
    /// <param name="count">Number of positions needed.</param>
    /// <param name="pcPositions">PC positions to avoid spawning on top of.</param>
    /// <param name="gridWidth">Grid width (default 20).</param>
    /// <param name="gridHeight">Grid height (default 20).</param>
    /// <returns>Array of valid spawn positions.</returns>
    public static Vector2Int[] CalculateSpawnPositions(int count, List<Vector2Int> pcPositions,
        int gridWidth = 20, int gridHeight = 20)
    {
        Vector2Int[] positions = new Vector2Int[count];
        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();

        // Mark PC positions as occupied
        if (pcPositions != null)
        {
            foreach (var pcPos in pcPositions)
                occupied.Add(pcPos);
        }

        // Define spawn zone on the right side of the map (enemy territory)
        int spawnMinX = gridWidth / 2 + 1; // Start from the right half
        int spawnMaxX = gridWidth - 2;      // Leave 1-cell border
        int spawnMinY = 2;
        int spawnMaxY = gridHeight - 2;

        // Build list of all candidate positions in the spawn zone
        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int x = spawnMinX; x <= spawnMaxX; x++)
        {
            for (int y = spawnMinY; y <= spawnMaxY; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                if (!occupied.Contains(pos))
                    candidates.Add(pos);
            }
        }

        // Shuffle candidates for variety
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            Vector2Int temp = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = temp;
        }

        int minDistSq = 4; // Minimum 2 cells apart (squared distance)
        int placed = 0;

        for (int ci = 0; ci < candidates.Count && placed < count; ci++)
        {
            Vector2Int candidate = candidates[ci];

            // Check minimum distance from all already-placed creatures
            bool tooClose = false;
            for (int pi = 0; pi < placed; pi++)
            {
                int dx = candidate.x - positions[pi].x;
                int dy = candidate.y - positions[pi].y;
                if (dx * dx + dy * dy < minDistSq)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                positions[placed] = candidate;
                occupied.Add(candidate);
                placed++;
            }
        }

        // Fallback: if we couldn't place all with minimum distance, relax constraint
        if (placed < count)
        {
            Debug.LogWarning($"[CustomEncounterBuilder] Could only place {placed}/{count} with min distance. Relaxing constraint for remaining.");
            for (int ci = 0; ci < candidates.Count && placed < count; ci++)
            {
                Vector2Int candidate = candidates[ci];
                if (!occupied.Contains(candidate))
                {
                    positions[placed] = candidate;
                    occupied.Add(candidate);
                    placed++;
                }
            }
        }

        // Final fallback: fill any remaining with sequential positions
        if (placed < count)
        {
            Debug.LogWarning($"[CustomEncounterBuilder] Still short {count - placed} positions. Using fallback sequential positions.");
            for (int x = spawnMinX; x <= spawnMaxX && placed < count; x++)
            {
                for (int y = spawnMinY; y <= spawnMaxY && placed < count; y++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    if (!occupied.Contains(pos))
                    {
                        positions[placed] = pos;
                        occupied.Add(pos);
                        placed++;
                    }
                }
            }
        }

        return positions;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  UI HELPERS
    // ═══════════════════════════════════════════════════════════════════

    private void CreateLabel(Transform parent, string text, int fontSize, FontStyle style, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta, TextAnchor alignment, out Text textComponent)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        // If using stretch anchors, clear offsets
        if (anchorMin != anchorMax)
        {
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        textComponent = go.GetComponent<Text>();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.fontStyle = style;
        textComponent.color = color;
        textComponent.alignment = alignment;
        textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (textComponent.font == null) textComponent.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
        textComponent.horizontalOverflow = HorizontalWrapMode.Overflow;
        textComponent.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private void CreateLabel(Transform parent, string text, int fontSize, FontStyle style, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta, TextAnchor alignment)
    {
        CreateLabel(parent, text, fontSize, style, color, anchorMin, anchorMax, pivot, anchoredPos, sizeDelta, alignment, out _);
    }

    private void CreateButton(Transform parent, string label, Color color, Action onClick, out Button button)
    {
        GameObject obj = new GameObject($"Btn_{label}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);

        LayoutElement le = obj.GetComponent<LayoutElement>();
        le.minWidth = 120f;
        le.preferredWidth = 140f;
        le.preferredHeight = 38f;
        le.minHeight = 38f;

        Image img = obj.GetComponent<Image>();
        img.color = color;

        button = obj.GetComponent<Button>();
        button.targetGraphic = img;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = button.colors;
        cb.normalColor = color;
        cb.highlightedColor = Color.Lerp(color, Color.white, 0.2f);
        cb.pressedColor = Color.Lerp(color, Color.black, 0.2f);
        cb.selectedColor = cb.highlightedColor;
        cb.disabledColor = new Color(0.22f, 0.22f, 0.22f, 0.9f);
        cb.fadeDuration = 0.08f;
        button.colors = cb;
        button.onClick.AddListener(() => onClick?.Invoke());

        CreateLabel(obj.transform, label, 16, FontStyle.Bold, Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(130f, 30f), TextAnchor.MiddleCenter);
    }

    private void CreateSmallButton(Transform parent, string label,
        Vector2 anchorMin, Vector2 anchorMax, Color color, Action onClick)
    {
        GameObject obj = new GameObject($"SmBtn_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = obj.GetComponent<Image>();
        img.color = color;

        Button btn = obj.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = btn.colors;
        cb.normalColor = color;
        cb.highlightedColor = Color.Lerp(color, Color.white, 0.3f);
        cb.pressedColor = Color.Lerp(color, Color.black, 0.2f);
        cb.selectedColor = cb.highlightedColor;
        cb.fadeDuration = 0.05f;
        btn.colors = cb;
        btn.onClick.AddListener(() => onClick?.Invoke());

        CreateLabel(obj.transform, label, 20, FontStyle.Bold, Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(30f, 30f), TextAnchor.MiddleCenter);
    }

    private GameObject CreateDropdown(Transform parent, string[] options, int defaultIndex, Action<int> onChange)
    {
        GameObject obj = new GameObject("Dropdown", typeof(RectTransform), typeof(Image), typeof(Dropdown));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<Image>().color = new Color(0.2f, 0.22f, 0.32f, 1f);

        Dropdown dd = obj.GetComponent<Dropdown>();
        dd.ClearOptions();
        List<Dropdown.OptionData> opts = new List<Dropdown.OptionData>();
        foreach (string o in options)
            opts.Add(new Dropdown.OptionData(o));
        dd.AddOptions(opts);
        dd.value = defaultIndex;

        // Create the label for the dropdown
        GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObj.transform.SetParent(obj.transform, false);
        RectTransform labelRt = labelObj.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0.05f, 0f);
        labelRt.anchorMax = new Vector2(0.95f, 1f);
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
        Text labelText = labelObj.GetComponent<Text>();
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (labelText.font == null) labelText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        labelText.fontSize = 14;
        labelText.color = Color.white;
        labelText.alignment = TextAnchor.MiddleLeft;
        dd.captionText = labelText;

        // Create dropdown template
        GameObject template = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        template.transform.SetParent(obj.transform, false);
        RectTransform templateRt = template.GetComponent<RectTransform>();
        templateRt.anchorMin = new Vector2(0f, 0f);
        templateRt.anchorMax = new Vector2(1f, 0f);
        templateRt.pivot = new Vector2(0.5f, 1f);
        templateRt.sizeDelta = new Vector2(0f, 150f);
        template.GetComponent<Image>().color = new Color(0.15f, 0.17f, 0.25f, 1f);

        // Viewport in template
        GameObject templateViewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        templateViewport.transform.SetParent(template.transform, false);
        RectTransform tvpRt = templateViewport.GetComponent<RectTransform>();
        tvpRt.anchorMin = Vector2.zero;
        tvpRt.anchorMax = Vector2.one;
        tvpRt.offsetMin = Vector2.zero;
        tvpRt.offsetMax = Vector2.zero;
        templateViewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.003f);
        templateViewport.GetComponent<Mask>().showMaskGraphic = false;

        // Content in viewport
        GameObject templateContent = new GameObject("Content", typeof(RectTransform));
        templateContent.transform.SetParent(templateViewport.transform, false);
        RectTransform tcRt = templateContent.GetComponent<RectTransform>();
        tcRt.anchorMin = new Vector2(0f, 1f);
        tcRt.anchorMax = new Vector2(1f, 1f);
        tcRt.pivot = new Vector2(0.5f, 1f);
        tcRt.sizeDelta = new Vector2(0f, 28f);

        // Item template
        GameObject item = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
        item.transform.SetParent(templateContent.transform, false);
        RectTransform itemRt = item.GetComponent<RectTransform>();
        itemRt.anchorMin = new Vector2(0f, 0.5f);
        itemRt.anchorMax = new Vector2(1f, 0.5f);
        itemRt.sizeDelta = new Vector2(0f, 28f);

        // Item background
        GameObject itemBg = new GameObject("Item Background", typeof(RectTransform), typeof(Image));
        itemBg.transform.SetParent(item.transform, false);
        RectTransform ibRt = itemBg.GetComponent<RectTransform>();
        ibRt.anchorMin = Vector2.zero;
        ibRt.anchorMax = Vector2.one;
        ibRt.offsetMin = Vector2.zero;
        ibRt.offsetMax = Vector2.zero;
        itemBg.GetComponent<Image>().color = new Color(0.25f, 0.28f, 0.4f, 1f);

        // Item checkmark (hidden)
        GameObject checkmark = new GameObject("Item Checkmark", typeof(RectTransform), typeof(Image));
        checkmark.transform.SetParent(itemBg.transform, false);
        RectTransform cmRt = checkmark.GetComponent<RectTransform>();
        cmRt.anchorMin = new Vector2(0f, 0.5f);
        cmRt.anchorMax = new Vector2(0f, 0.5f);
        cmRt.sizeDelta = new Vector2(16f, 16f);
        cmRt.anchoredPosition = new Vector2(10f, 0f);

        // Item label
        GameObject itemLabel = new GameObject("Item Label", typeof(RectTransform), typeof(Text));
        itemLabel.transform.SetParent(item.transform, false);
        RectTransform ilRt = itemLabel.GetComponent<RectTransform>();
        ilRt.anchorMin = new Vector2(0.05f, 0f);
        ilRt.anchorMax = new Vector2(0.95f, 1f);
        ilRt.offsetMin = Vector2.zero;
        ilRt.offsetMax = Vector2.zero;
        Text itemLabelText = itemLabel.GetComponent<Text>();
        itemLabelText.font = labelText.font;
        itemLabelText.fontSize = 14;
        itemLabelText.color = Color.white;
        itemLabelText.alignment = TextAnchor.MiddleLeft;

        Toggle toggle = item.GetComponent<Toggle>();
        toggle.targetGraphic = itemBg.GetComponent<Image>();
        toggle.graphic = checkmark.GetComponent<Image>();
        toggle.isOn = false;

        dd.itemText = itemLabelText;
        dd.template = templateRt;

        ScrollRect templateScroll = template.GetComponent<ScrollRect>();
        templateScroll.content = tcRt;
        templateScroll.viewport = tvpRt;

        template.SetActive(false);

        dd.onValueChanged.AddListener((val) => onChange?.Invoke(val));

        return obj;
    }

    private GameObject CreateInputField(Transform parent, string placeholder)
    {
        GameObject obj = new GameObject("InputField", typeof(RectTransform), typeof(Image), typeof(InputField));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<Image>().color = new Color(0.18f, 0.2f, 0.3f, 1f);

        // Text child for displaying input
        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObj.transform.SetParent(obj.transform, false);
        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0.05f, 0f);
        textRt.anchorMax = new Vector2(0.95f, 1f);
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        Text inputText = textObj.GetComponent<Text>();
        inputText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (inputText.font == null) inputText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        inputText.fontSize = 14;
        inputText.color = Color.white;
        inputText.alignment = TextAnchor.MiddleLeft;
        inputText.supportRichText = false;

        // Placeholder
        GameObject phObj = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
        phObj.transform.SetParent(obj.transform, false);
        RectTransform phRt = phObj.GetComponent<RectTransform>();
        phRt.anchorMin = new Vector2(0.05f, 0f);
        phRt.anchorMax = new Vector2(0.95f, 1f);
        phRt.offsetMin = Vector2.zero;
        phRt.offsetMax = Vector2.zero;
        Text phText = phObj.GetComponent<Text>();
        phText.font = inputText.font;
        phText.fontSize = 14;
        phText.color = new Color(0.5f, 0.55f, 0.65f, 1f);
        phText.alignment = TextAnchor.MiddleLeft;
        phText.fontStyle = FontStyle.Italic;
        phText.text = placeholder;

        InputField input = obj.GetComponent<InputField>();
        input.textComponent = inputText;
        input.placeholder = phText;
        input.contentType = InputField.ContentType.Standard;

        return obj;
    }
}
