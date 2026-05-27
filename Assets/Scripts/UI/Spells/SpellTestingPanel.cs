using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

/// <summary>
/// DEV/DEBUG Spell Testing Panel for rapid spell playtesting.
/// Toggle with F12 key. Shows a searchable, level-filtered spell list.
/// Casting uses the ActivePC (whose turn it currently is), temporarily
/// boosting their primary casting ability by +4 to improve DCs.
///
/// Integrates with existing SpellDatabase, GameManager, and CharacterController systems.
/// </summary>
public class SpellTestingPanel : MonoBehaviour
{
    // ========== PANEL STATE ==========
    private bool _isVisible = false;
    private GameObject _panelRoot;
    private RectTransform _panelRect;

    // ========== CONFIGURATION STATE ==========
    private string _searchFilter = "";
    private int _selectedSpellLevel = -1; // -1 = All

    // Stats tracking
    private int _totalDamageDealt = 0;
    private int _totalTargetsHit = 0;
    private int _totalSavesMade = 0;
    private int _totalSavesFailed = 0;
    private int _totalSRPassed = 0;
    private int _totalSRFailed = 0;

    // Ability boost tracking (to restore after cast)
    private CharacterController _boostedCaster;
    private string _boostedAbility; // "INT", "WIS", or "CHA"
    private int _originalAbilityScore;
    private bool _abilityBoosted = false;

    // Non-caster temporary setup tracking (to restore after cast)
    private bool _temporaryCasterSetup = false;
    private CharacterController _tempCasterCharacter;
    private SpellcastingComponent _tempSpellComp;
    private ClassLevelEntry _tempClassEntry;

    // ========== UI REFERENCES ==========
    private ScrollRect _spellListScroll;
    private Transform _spellListContent;
    private InputField _searchInput;
    private Text _statsText;
    private List<GameObject> _spellEntries = new List<GameObject>();

    // Level filter buttons
    private List<Button> _levelFilterButtons = new List<Button>();

    // ========== COLORS ==========
    private static readonly Color PanelBg = new Color(0.08f, 0.09f, 0.14f, 0.96f);
    private static readonly Color SectionBg = new Color(0.12f, 0.13f, 0.2f, 0.95f);
    private static readonly Color HeaderColor = new Color(1f, 0.85f, 0.2f, 1f);
    private static readonly Color SpellEntryBg = new Color(0.22f, 0.26f, 0.40f, 1f);
    private static readonly Color SpellEntryBgAlt = new Color(0.18f, 0.22f, 0.35f, 1f);
    private static readonly Color SpellEntryBorder = new Color(0.45f, 0.55f, 0.75f, 1f);
    private static readonly Color CastBtnColor = new Color(0.2f, 0.6f, 0.3f, 1f);
    private static readonly Color DangerBtnColor = new Color(0.8f, 0.25f, 0.2f, 1f);
    private static readonly Color FilterActiveColor = new Color(0.3f, 0.6f, 0.9f, 1f);
    private static readonly Color FilterInactiveColor = new Color(0.25f, 0.26f, 0.35f, 0.9f);

    // Spell school colors
    private static readonly Dictionary<string, Color> SchoolColors = new Dictionary<string, Color>
    {
        { "Abjuration", new Color(0.4f, 0.7f, 1f) },
        { "Conjuration", new Color(0.5f, 0.9f, 0.5f) },
        { "Divination", new Color(0.8f, 0.8f, 1f) },
        { "Enchantment", new Color(1f, 0.6f, 0.8f) },
        { "Evocation", new Color(1f, 0.5f, 0.3f) },
        { "Illusion", new Color(0.8f, 0.6f, 1f) },
        { "Necromancy", new Color(0.6f, 0.8f, 0.6f) },
        { "Transmutation", new Color(1f, 0.9f, 0.4f) },
        { "Universal", new Color(0.8f, 0.8f, 0.8f) }
    };

    // ========== LIFECYCLE ==========

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F12))
        {
            TogglePanel();
        }

        // Check if we need to restore a boosted ability and/or temporary caster setup
        if ((_abilityBoosted && _boostedCaster != null) || _temporaryCasterSetup)
        {
            // Restore after a short delay to let the spell resolve
            // We check every frame if the spell targeting is done
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.GetTestPanelCaster() == null)
            {
                RestoreAbilityBoost();
                RestoreTemporaryCasterSetup();
            }
        }
    }

    public void TogglePanel()
    {
        _isVisible = !_isVisible;
        if (_isVisible && _panelRoot == null)
        {
            BuildPanel();
        }
        if (_panelRoot != null)
        {
            _panelRoot.SetActive(_isVisible);
            if (_isVisible)
            {
                RefreshSpellList();
                RefreshStats();
            }
        }
    }

    // ========== PANEL CONSTRUCTION ==========

    private void BuildPanel()
    {
        // Find canvas
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = FindAnyObjectByType<Canvas>();
        }
        if (canvas == null) return;

        Font font = UIFactory.GetDefaultFont();

        // Root panel - large overlay
        _panelRoot = new GameObject("SpellTestingPanel");
        _panelRoot.transform.SetParent(canvas.transform, false);
        _panelRect = _panelRoot.AddComponent<RectTransform>();
        _panelRect.anchorMin = new Vector2(0.05f, 0.05f);
        _panelRect.anchorMax = new Vector2(0.50f, 0.95f);
        _panelRect.offsetMin = Vector2.zero;
        _panelRect.offsetMax = Vector2.zero;

        Image panelBg = _panelRoot.AddComponent<Image>();
        panelBg.color = PanelBg;

        // Main layout - vertical
        VerticalLayoutGroup mainVlg = _panelRoot.AddComponent<VerticalLayoutGroup>();
        mainVlg.spacing = 4;
        mainVlg.padding = new RectOffset(8, 8, 8, 8);
        mainVlg.childControlWidth = true;
        mainVlg.childControlHeight = true;
        mainVlg.childForceExpandWidth = true;
        mainVlg.childForceExpandHeight = false;

        // ===== TITLE BAR =====
        BuildTitleBar(_panelRoot.transform, font);

        // ===== MAIN CONTENT - just the spell list =====
        GameObject contentRow = new GameObject("ContentRow");
        contentRow.transform.SetParent(_panelRoot.transform, false);
        RectTransform contentRT = contentRow.AddComponent<RectTransform>();
        contentRT.sizeDelta = new Vector2(0, 800);
        LayoutElement contentLE = contentRow.AddComponent<LayoutElement>();
        contentLE.preferredHeight = 800;
        contentLE.flexibleHeight = 1;

        // Spell Selection section (full width)
        GameObject spellCol = CreateSection(contentRow.transform, "SpellSelection", 1.0f);
        BuildSpellSelectionSection(spellCol.transform, font);

        // Make contentRow use a simple layout that stretches the child
        HorizontalLayoutGroup contentHlg = contentRow.AddComponent<HorizontalLayoutGroup>();
        contentHlg.spacing = 0;
        contentHlg.childControlWidth = true;
        contentHlg.childControlHeight = true;
        contentHlg.childForceExpandWidth = true;
        contentHlg.childForceExpandHeight = true;

        // ===== BOTTOM STATS BAR =====
        BuildStatsBar(_panelRoot.transform, font);
    }

    private void BuildTitleBar(Transform parent, Font font)
    {
        GameObject titleBar = new GameObject("TitleBar");
        titleBar.transform.SetParent(parent, false);
        RectTransform titleRT = titleBar.AddComponent<RectTransform>();
        titleRT.sizeDelta = new Vector2(0, 32);
        AddLayoutHeight(titleBar, 32);
        Image titleBg = titleBar.AddComponent<Image>();
        titleBg.color = new Color(0.1f, 0.1f, 0.18f, 1f);

        HorizontalLayoutGroup titleHlg = titleBar.AddComponent<HorizontalLayoutGroup>();
        titleHlg.spacing = 10;
        titleHlg.padding = new RectOffset(10, 10, 2, 2);
        titleHlg.childControlWidth = false;
        titleHlg.childControlHeight = true;
        titleHlg.childForceExpandWidth = false;
        titleHlg.childForceExpandHeight = true;

        // Title text
        Text title = UIFactory.CreateLabel(titleBar.transform, "⚔ SPELL TESTING PANEL (F12 to toggle)", 16,
            TextAnchor.MiddleLeft, HeaderColor, "Title", font);
        title.fontStyle = FontStyle.Bold;
        LayoutElement titleLE = title.gameObject.AddComponent<LayoutElement>();
        titleLE.flexibleWidth = 1;
        titleLE.preferredHeight = 28;

        // Close button
        Button closeBtn = UIFactory.CreateButton(titleBar.transform, "✕", () => TogglePanel(),
            new Vector2(30, 26), DangerBtnColor, "CloseBtn", font, 14);
    }

    private GameObject CreateSection(Transform parent, string name, float widthRatio)
    {
        GameObject section = new GameObject(name);
        section.transform.SetParent(parent, false);
        RectTransform sRT = section.AddComponent<RectTransform>();
        Image sBg = section.AddComponent<Image>();
        sBg.color = SectionBg;

        LayoutElement le = section.AddComponent<LayoutElement>();
        le.flexibleWidth = widthRatio;

        VerticalLayoutGroup vlg = section.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 3;
        vlg.padding = new RectOffset(6, 6, 6, 6);
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        return section;
    }

    // ========== SPELL SELECTION SECTION ==========

    private void BuildSpellSelectionSection(Transform parent, Font font)
    {
        // Section header
        Text header = UIFactory.CreateLabel(parent, "📜 SPELL SELECTION", 14,
            TextAnchor.MiddleCenter, HeaderColor, "Header", font);
        header.fontStyle = FontStyle.Bold;
        LayoutElement headerLE = header.gameObject.AddComponent<LayoutElement>();
        headerLE.preferredHeight = 22;

        // Search bar
        GameObject searchRow = new GameObject("SearchRow");
        searchRow.transform.SetParent(parent, false);
        RectTransform searchRT = searchRow.AddComponent<RectTransform>();
        searchRT.sizeDelta = new Vector2(0, 28);
        AddLayoutHeight(searchRow, 28);
        HorizontalLayoutGroup searchHlg = searchRow.AddComponent<HorizontalLayoutGroup>();
        searchHlg.spacing = 4;
        searchHlg.childControlWidth = true;
        searchHlg.childControlHeight = true;
        searchHlg.childForceExpandWidth = false;
        searchHlg.childForceExpandHeight = true;

        Text searchLabel = UIFactory.CreateLabel(searchRow.transform, "🔍", 12,
            TextAnchor.MiddleCenter, Color.white, "SearchIcon", font);
        LayoutElement searchLabelLE = searchLabel.gameObject.AddComponent<LayoutElement>();
        searchLabelLE.preferredWidth = 22;

        _searchInput = UIFactory.CreateInputField(searchRow.transform, "Search spells...",
            (val) => { _searchFilter = val; RefreshSpellList(); }, font);
        LayoutElement searchLE = _searchInput.gameObject.AddComponent<LayoutElement>();
        searchLE.flexibleWidth = 1;

        // Level filter buttons
        GameObject filterRow = new GameObject("FilterRow");
        filterRow.transform.SetParent(parent, false);
        RectTransform filterRT = filterRow.AddComponent<RectTransform>();
        filterRT.sizeDelta = new Vector2(0, 24);
        AddLayoutHeight(filterRow, 24);
        HorizontalLayoutGroup filterHlg = filterRow.AddComponent<HorizontalLayoutGroup>();
        filterHlg.spacing = 2;
        filterHlg.childControlWidth = true;
        filterHlg.childControlHeight = true;
        filterHlg.childForceExpandWidth = true;
        filterHlg.childForceExpandHeight = true;

        string[] levelLabels = { "All", "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };
        int[] levelValues = { -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        for (int i = 0; i < levelLabels.Length; i++)
        {
            int lvl = levelValues[i];
            Button btn = UIFactory.CreateButton(filterRow.transform, levelLabels[i],
                () => { _selectedSpellLevel = lvl; RefreshLevelFilterButtons(); RefreshSpellList(); },
                new Vector2(30, 22), lvl == _selectedSpellLevel ? FilterActiveColor : FilterInactiveColor,
                $"LvlFilter_{levelLabels[i]}", font, 11);
            _levelFilterButtons.Add(btn);
        }

        // Scroll area for spell list
        _spellListScroll = UIFactory.CreateScrollPanel(parent, "SpellListScroll");

        // --- ScrollRect configuration ---
        _spellListScroll.vertical = true;
        _spellListScroll.horizontal = false;
        _spellListScroll.movementType = ScrollRect.MovementType.Clamped;
        _spellListScroll.scrollSensitivity = 30f;

        // --- Scroll panel RectTransform: let LayoutElement control sizing ---
        RectTransform spellScrollRT = _spellListScroll.GetComponent<RectTransform>();
        LayoutElement scrollLE = _spellListScroll.gameObject.AddComponent<LayoutElement>();
        scrollLE.flexibleHeight = 1;
        scrollLE.preferredHeight = 600;
        scrollLE.minHeight = 200;

        // --- Viewport: fix Image + Mask for proper clipping ---
        RectTransform viewportRT = _spellListScroll.viewport;
        if (viewportRT != null)
        {
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.offsetMin = Vector2.zero;
            viewportRT.offsetMax = Vector2.zero;
            viewportRT.sizeDelta = Vector2.zero;

            Image viewportImg = viewportRT.GetComponent<Image>();
            if (viewportImg == null)
                viewportImg = viewportRT.gameObject.AddComponent<Image>();
            viewportImg.color = new Color(1f, 1f, 1f, 0.004f);
            viewportImg.raycastTarget = true;

            Mask viewportMask = viewportRT.GetComponent<Mask>();
            if (viewportMask == null)
                viewportMask = viewportRT.gameObject.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;
        }

        // --- Create vertical scrollbar ---
        GameObject scrollbarGO = new GameObject("VerticalScrollbar");
        scrollbarGO.transform.SetParent(_spellListScroll.transform, false);
        RectTransform scrollbarRT = scrollbarGO.AddComponent<RectTransform>();
        scrollbarRT.anchorMin = new Vector2(1f, 0f);
        scrollbarRT.anchorMax = new Vector2(1f, 1f);
        scrollbarRT.pivot = new Vector2(1f, 0.5f);
        scrollbarRT.sizeDelta = new Vector2(10f, 0f);

        Image scrollbarBg = scrollbarGO.AddComponent<Image>();
        scrollbarBg.color = new Color(0.15f, 0.15f, 0.25f, 0.8f);

        GameObject handleArea = new GameObject("SlidingArea");
        handleArea.transform.SetParent(scrollbarGO.transform, false);
        RectTransform handleAreaRT = handleArea.AddComponent<RectTransform>();
        handleAreaRT.anchorMin = Vector2.zero;
        handleAreaRT.anchorMax = Vector2.one;
        handleAreaRT.offsetMin = Vector2.zero;
        handleAreaRT.offsetMax = Vector2.zero;

        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        RectTransform handleRT = handle.AddComponent<RectTransform>();
        handleRT.anchorMin = Vector2.zero;
        handleRT.anchorMax = Vector2.one;
        handleRT.offsetMin = Vector2.zero;
        handleRT.offsetMax = Vector2.zero;

        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = new Color(0.4f, 0.5f, 0.7f, 0.9f);

        Scrollbar scrollbar = scrollbarGO.AddComponent<Scrollbar>();
        scrollbar.handleRect = handleRT;
        scrollbar.targetGraphic = handleImg;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        _spellListScroll.verticalScrollbar = scrollbar;
        _spellListScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        _spellListScroll.verticalScrollbarSpacing = -2f;

        if (viewportRT != null)
        {
            viewportRT.offsetMax = new Vector2(-12f, 0f);
        }

        _spellListContent = _spellListScroll.content;

        RectTransform contentRT = _spellListContent as RectTransform;
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup contentVlg = _spellListContent.gameObject.AddComponent<VerticalLayoutGroup>();
        contentVlg.spacing = 2;
        contentVlg.padding = new RectOffset(2, 2, 2, 2);
        contentVlg.childControlWidth = true;
        contentVlg.childControlHeight = true;
        contentVlg.childForceExpandWidth = true;
        contentVlg.childForceExpandHeight = false;

        ContentSizeFitter csf = _spellListContent.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    // ========== STATS BAR ==========

    private void BuildStatsBar(Transform parent, Font font)
    {
        GameObject statsBar = new GameObject("StatsBar");
        statsBar.transform.SetParent(parent, false);
        RectTransform statsRT = statsBar.AddComponent<RectTransform>();
        statsRT.sizeDelta = new Vector2(0, 28);
        AddLayoutHeight(statsBar, 28);
        Image statsBg = statsBar.AddComponent<Image>();
        statsBg.color = new Color(0.1f, 0.1f, 0.18f, 1f);

        HorizontalLayoutGroup statsHlg = statsBar.AddComponent<HorizontalLayoutGroup>();
        statsHlg.spacing = 15;
        statsHlg.padding = new RectOffset(10, 10, 2, 2);
        statsHlg.childControlWidth = false;
        statsHlg.childControlHeight = true;
        statsHlg.childForceExpandWidth = false;
        statsHlg.childForceExpandHeight = true;

        _statsText = UIFactory.CreateLabel(statsBar.transform,
            "Dmg: 0 | Hits: 0 | Saves: 0/0 | SR: 0/0",
            11, TextAnchor.MiddleLeft, new Color(0.8f, 0.9f, 1f), "StatsText", font);
        LayoutElement statsLE = _statsText.gameObject.AddComponent<LayoutElement>();
        statsLE.flexibleWidth = 1;
        statsLE.preferredHeight = 24;
    }

    // ========== UI HELPERS ==========

    private void AddLayoutHeight(GameObject obj, float height)
    {
        LayoutElement le = obj.GetComponent<LayoutElement>();
        if (le == null) le = obj.AddComponent<LayoutElement>();
        le.preferredHeight = height;
    }

    // ========== SPELL LIST MANAGEMENT ==========

    private void RefreshSpellList()
    {
        // Clear existing entries
        foreach (var entry in _spellEntries)
        {
            if (entry != null) Destroy(entry);
        }
        _spellEntries.Clear();

        if (_spellListContent == null)
        {
            Debug.LogError("[SpellTestingPanel] _spellListContent is null! Cannot populate spell list.");
            return;
        }

        // Get all spells
        List<SpellData> allSpells = SpellDatabase.GetAllSpells();
        if (allSpells == null || allSpells.Count == 0)
        {
            Debug.LogWarning("[SpellTestingPanel] No spells found in database!");
            return;
        }

        // Filter by level
        if (_selectedSpellLevel >= 0)
        {
            allSpells = allSpells.Where(s => s.SpellLevel == _selectedSpellLevel).ToList();
        }

        // Filter by search text
        if (!string.IsNullOrEmpty(_searchFilter))
        {
            string filter = _searchFilter.ToLower();
            allSpells = allSpells.Where(s =>
                s.Name.ToLower().Contains(filter) ||
                (!string.IsNullOrEmpty(s.School) && s.School.ToLower().Contains(filter)) ||
                (!string.IsNullOrEmpty(s.SpellId) && s.SpellId.ToLower().Contains(filter))
            ).ToList();
        }

        // Sort by level then name
        allSpells = allSpells.OrderBy(s => s.SpellLevel).ThenBy(s => s.Name).ToList();

        Font font = UIFactory.GetDefaultFont();
        if (font == null)
        {
            font = Font.CreateDynamicFontFromOSFont("Arial", 14);
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Liberation Sans", 14);
            if (font == null)
            {
                string[] osFonts = Font.GetOSInstalledFontNames();
                if (osFonts != null && osFonts.Length > 0)
                    font = Font.CreateDynamicFontFromOSFont(osFonts[0], 14);
            }
        }

        _spellEntryIndex = 0;
        int currentLevel = -999;

        foreach (SpellData spell in allSpells)
        {
            if (spell == null) continue;

            // Level group header
            if (spell.SpellLevel != currentLevel)
            {
                currentLevel = spell.SpellLevel;
                string levelName = currentLevel == 0 ? "Level 0 (Cantrips)" : $"Level {currentLevel}";
                GameObject headerObj = new GameObject($"LevelHeader_{currentLevel}");
                headerObj.transform.SetParent(_spellListContent, false);

                RectTransform headerRT = headerObj.AddComponent<RectTransform>();
                headerRT.sizeDelta = new Vector2(0, 26);
                AddLayoutHeight(headerObj, 26);

                Image headerBg = headerObj.AddComponent<Image>();
                headerBg.color = new Color(0.15f, 0.18f, 0.30f, 1f);

                Text headerText = CreateSafeLabel(headerObj.transform, $"══ {levelName} ══", 15, HeaderColor, font, "HeaderText");
                headerText.fontStyle = FontStyle.Bold;
                headerText.alignment = TextAnchor.MiddleCenter;
                RectTransform htRT = headerText.GetComponent<RectTransform>();
                htRT.anchorMin = Vector2.zero;
                htRT.anchorMax = Vector2.one;
                htRT.offsetMin = Vector2.zero;
                htRT.offsetMax = Vector2.zero;

                _spellEntries.Add(headerObj);
                _spellEntryIndex = 0;
            }

            // Spell entry
            GameObject entry = CreateSpellEntry(spell, font);
            _spellEntries.Add(entry);
        }

        // Force layout rebuild
        if (_spellListContent != null)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_spellListContent as RectTransform);

            if (_spellListScroll != null)
            {
                RectTransform scrollRT = _spellListScroll.GetComponent<RectTransform>();
                if (scrollRT != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRT);
                _spellListScroll.verticalNormalizedPosition = 1f;
            }

            RectTransform parentRT = _spellListContent.parent?.parent?.GetComponent<RectTransform>();
            if (parentRT != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRT);

            Canvas.ForceUpdateCanvases();
        }
    }

    private int _spellEntryIndex = 0;

    private GameObject CreateSpellEntry(SpellData spell, Font font)
    {
        GameObject entry = new GameObject($"Spell_{spell.SpellId}");
        entry.transform.SetParent(_spellListContent, false);
        RectTransform entryRT = entry.AddComponent<RectTransform>();
        entryRT.sizeDelta = new Vector2(0, 36);
        AddLayoutHeight(entry, 36);

        bool isPlaceholder = spell.IsPlaceholder;
        Image entryBg = entry.AddComponent<Image>();
        if (isPlaceholder)
            entryBg.color = new Color(0.15f, 0.15f, 0.22f, 1f);
        else
            entryBg.color = (_spellEntryIndex % 2 == 0) ? SpellEntryBg : SpellEntryBgAlt;
        _spellEntryIndex++;

        Outline entryOutline = entry.AddComponent<Outline>();
        entryOutline.effectColor = SpellEntryBorder;
        entryOutline.effectDistance = new Vector2(1f, 1f);

        HorizontalLayoutGroup hlg = entry.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6;
        hlg.padding = new RectOffset(8, 6, 2, 2);
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        // School color indicator
        Color schoolColor = Color.gray;
        if (!string.IsNullOrEmpty(spell.School) && SchoolColors.ContainsKey(spell.School))
            schoolColor = SchoolColors[spell.School];

        Text schoolDot = CreateSafeLabel(entry.transform, "●", 16, schoolColor, font, "SchoolDot");
        schoolDot.fontStyle = FontStyle.Bold;
        LayoutElement dotLE = schoolDot.gameObject.GetComponent<LayoutElement>() ?? schoolDot.gameObject.AddComponent<LayoutElement>();
        dotLE.preferredWidth = 20;
        dotLE.minHeight = 30;

        // Spell name
        Color nameColor = isPlaceholder ? new Color(0.7f, 0.7f, 0.7f, 1f) : Color.white;
        Text nameText = CreateSafeLabel(entry.transform, spell.Name, 16, nameColor, font, "SpellName");
        nameText.fontStyle = FontStyle.Bold;
        nameText.alignment = TextAnchor.MiddleLeft;
        LayoutElement nameLE = nameText.gameObject.GetComponent<LayoutElement>() ?? nameText.gameObject.AddComponent<LayoutElement>();
        nameLE.flexibleWidth = 1;
        nameLE.minWidth = 80;
        nameLE.minHeight = 30;

        // School abbreviation
        string schoolAbbr = !string.IsNullOrEmpty(spell.School) ?
            spell.School.Substring(0, System.Math.Min(4, spell.School.Length)) : "???";
        Color schoolTextColor = new Color(0.6f, 0.9f, 1f, 1f);
        Text schoolText = CreateSafeLabel(entry.transform, schoolAbbr, 12, schoolTextColor, font, "School");
        schoolText.alignment = TextAnchor.MiddleCenter;
        LayoutElement schoolLE = schoolText.gameObject.GetComponent<LayoutElement>() ?? schoolText.gameObject.AddComponent<LayoutElement>();
        schoolLE.preferredWidth = 42;
        schoolLE.minHeight = 30;

        // Range info
        string rangeInfo = GetRangeAbbrev(spell);
        Color rangeColor = new Color(0.5f, 1f, 0.5f, 1f);
        Text rangeText = CreateSafeLabel(entry.transform, rangeInfo, 12, rangeColor, font, "Range");
        rangeText.alignment = TextAnchor.MiddleCenter;
        LayoutElement rangeLE = rangeText.gameObject.GetComponent<LayoutElement>() ?? rangeText.gameObject.AddComponent<LayoutElement>();
        rangeLE.preferredWidth = 42;
        rangeLE.minHeight = 30;

        // Cast button
        if (!isPlaceholder)
        {
            SpellData capturedSpell = spell;
            Button castBtn = UIFactory.CreateButton(entry.transform, "Cast",
                () => CastSpell(capturedSpell),
                new Vector2(55, 28), CastBtnColor, "CastBtn", font, 14);
            Text btnText = castBtn.GetComponentInChildren<Text>();
            if (btnText != null)
            {
                btnText.color = Color.white;
                btnText.fontStyle = FontStyle.Bold;
                if (btnText.font == null) btnText.font = font;
            }
            LayoutElement castLE = castBtn.gameObject.GetComponent<LayoutElement>() ?? castBtn.gameObject.AddComponent<LayoutElement>();
            castLE.preferredWidth = 55;
            castLE.minHeight = 28;
        }
        else
        {
            Text placeholder = CreateSafeLabel(entry.transform, "(N/A)", 12, new Color(0.6f, 0.6f, 0.6f, 1f), font, "Placeholder");
            placeholder.alignment = TextAnchor.MiddleCenter;
            LayoutElement phLE = placeholder.gameObject.GetComponent<LayoutElement>() ?? placeholder.gameObject.AddComponent<LayoutElement>();
            phLE.preferredWidth = 55;
            phLE.minHeight = 30;
        }

        return entry;
    }

    private Text CreateSafeLabel(Transform parent, string text, int fontSize, Color color, Font font, string name)
    {
        Text txt = UIFactory.CreateLabel(parent, text, fontSize, TextAnchor.MiddleLeft, color, name, font);
        if (txt != null)
        {
            txt.color = new Color(color.r, color.g, color.b, 1f);
            txt.fontSize = Mathf.Max(fontSize, 10);
            if (txt.font == null) txt.font = font;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }
        // Fallback: manually create
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        if (go.GetComponent<CanvasRenderer>() == null) go.AddComponent<CanvasRenderer>();
        txt = go.AddComponent<Text>();
        txt.text = text;
        txt.font = font ?? Font.CreateDynamicFontFromOSFont("Arial", fontSize);
        txt.fontSize = Mathf.Max(fontSize, 10);
        txt.color = new Color(color.r, color.g, color.b, 1f);
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.supportRichText = true;
        return txt;
    }

    private string GetRangeAbbrev(SpellData spell)
    {
        if (spell.RangeCategory == SpellRangeCategory.Personal) return "Self";
        if (spell.RangeCategory == SpellRangeCategory.Touch) return "Touch";
        if (spell.RangeCategory == SpellRangeCategory.Close) return "Close";
        if (spell.RangeCategory == SpellRangeCategory.Medium) return "Med";
        if (spell.RangeCategory == SpellRangeCategory.Long) return "Long";
        if (spell.RangeCategory == SpellRangeCategory.Unlimited) return "Unlim";
        if (spell.RangeSquares > 0) return $"{spell.RangeSquares}sq";
        return "?";
    }

    private void RefreshLevelFilterButtons()
    {
        int[] levelValues = { -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        for (int i = 0; i < _levelFilterButtons.Count && i < levelValues.Length; i++)
        {
            Image img = _levelFilterButtons[i].GetComponent<Image>();
            if (img != null)
            {
                img.color = levelValues[i] == _selectedSpellLevel ? FilterActiveColor : FilterInactiveColor;
            }
        }
    }

    // ========== SPELL CASTING ==========

    /// <summary>
    /// Determines the primary casting ability name for a character based on class.
    /// Returns "INT" for wizard, "CHA" for sorcerer/bard, "WIS" for cleric/druid/ranger/paladin.
    /// Falls back to the highest of INT/WIS/CHA.
    /// </summary>
    private string GetPrimaryCastingAbility(CharacterStats stats)
    {
        if (stats == null) return "INT";

        if (stats.HasClass("Wizard")) return "INT";
        if (stats.HasClass("Sorcerer") || stats.HasClass("Bard")) return "CHA";
        if (stats.HasClass("Cleric") || stats.HasClass("Druid") ||
            stats.HasClass("Ranger") || stats.HasClass("Paladin")) return "WIS";

        // Fallback: pick the highest casting stat
        int intVal = stats.INT;
        int wisVal = stats.WIS;
        int chaVal = stats.CHA;
        if (wisVal >= intVal && wisVal >= chaVal) return "WIS";
        if (chaVal >= intVal && chaVal >= wisVal) return "CHA";
        return "INT";
    }

    /// <summary>
    /// Gets the current value of a named ability score.
    /// </summary>
    private int GetAbilityScore(CharacterStats stats, string ability)
    {
        switch (ability)
        {
            case "INT": return stats.INT;
            case "WIS": return stats.WIS;
            case "CHA": return stats.CHA;
            default: return stats.INT;
        }
    }

    /// <summary>
    /// Sets a named ability score on the stats.
    /// </summary>
    private void SetAbilityScore(CharacterStats stats, string ability, int value)
    {
        switch (ability)
        {
            case "INT": stats.INT = value; break;
            case "WIS": stats.WIS = value; break;
            case "CHA": stats.CHA = value; break;
        }
    }

    /// <summary>
    /// Applies a +4 temporary boost to the caster's primary casting ability.
    /// Stores the original value for restoration after the spell resolves.
    /// </summary>
    private void ApplyAbilityBoost(CharacterController caster)
    {
        if (caster == null || caster.Stats == null) return;

        // Restore any previous boost first
        if (_abilityBoosted)
        {
            RestoreAbilityBoost();
        }

        _boostedAbility = GetPrimaryCastingAbility(caster.Stats);
        _originalAbilityScore = GetAbilityScore(caster.Stats, _boostedAbility);
        SetAbilityScore(caster.Stats, _boostedAbility, _originalAbilityScore + 4);
        _boostedCaster = caster;
        _abilityBoosted = true;

        Debug.Log($"[SpellTestPanel] Applied +4 {_boostedAbility} boost: {_originalAbilityScore} → {_originalAbilityScore + 4}");
    }

    /// <summary>
    /// Restores the caster's ability score to its original value.
    /// </summary>
    private void RestoreAbilityBoost()
    {
        if (!_abilityBoosted || _boostedCaster == null || _boostedCaster.Stats == null) return;

        SetAbilityScore(_boostedCaster.Stats, _boostedAbility, _originalAbilityScore);
        Debug.Log($"[SpellTestPanel] Restored {_boostedAbility}: {_originalAbilityScore + 4} → {_originalAbilityScore}");

        _abilityBoosted = false;
        _boostedCaster = null;
        _boostedAbility = null;
    }

    /// <summary>
    /// For non-spellcaster characters (e.g. Rogue, Fighter), temporarily adds a Wizard
    /// class level and SpellcastingComponent so the test panel can cast spells through them.
    /// This is automatically cleaned up after the spell resolves.
    /// </summary>
    private void EnsureTemporaryCasterSetup(CharacterController caster)
    {
        if (caster == null || caster.Stats == null) return;

        // If the character is already a spellcaster with a SpellcastingComponent, nothing to do
        if (caster.Stats.IsSpellcaster && caster.Spellcasting != null)
            return;

        // Restore any previous temp setup first
        if (_temporaryCasterSetup)
        {
            RestoreTemporaryCasterSetup();
        }

        Debug.Log($"[SpellTestPanel] ⚡ Setting up temporary Wizard caster for non-caster: {caster.Stats.CharacterName}");

        _tempCasterCharacter = caster;

        // ── 1. Add a temporary Wizard class level so IsSpellcaster returns true ──
        if (!caster.Stats.IsSpellcaster)
        {
            _tempClassEntry = new ClassLevelEntry("Wizard", 10);
            if (caster.Stats.ClassLevels == null)
                caster.Stats.ClassLevels = new System.Collections.Generic.List<ClassLevelEntry>();
            caster.Stats.ClassLevels.Add(_tempClassEntry);
            Debug.Log($"[SpellTestPanel]   Added temp Wizard level 10. IsSpellcaster={caster.Stats.IsSpellcaster}, CasterLevel={caster.Stats.GetCasterLevel()}");
        }

        // ── 2. Add SpellcastingComponent if missing ──
        SpellcastingComponent spellComp = caster.Spellcasting;
        if (spellComp == null)
        {
            spellComp = caster.gameObject.AddComponent<SpellcastingComponent>();
            _tempSpellComp = spellComp;
            Debug.Log($"[SpellTestPanel]   Added temporary SpellcastingComponent");
        }

        // ── 3. Initialize the component with current stats ──
        spellComp.Init(caster.Stats);

        // ── 4. Ensure generous spell slots for all levels ──
        if (spellComp.SlotsRemaining != null)
        {
            for (int i = 0; i < spellComp.SlotsRemaining.Length; i++)
                spellComp.SlotsRemaining[i] = 99;
        }
        if (spellComp.SlotsMax != null)
        {
            for (int i = 0; i < spellComp.SlotsMax.Length; i++)
                spellComp.SlotsMax[i] = Mathf.Max(spellComp.SlotsMax[i], 99);
        }

        _temporaryCasterSetup = true;
        Debug.Log($"[SpellTestPanel]   Temporary caster setup complete for {caster.Stats.CharacterName}");
    }

    /// <summary>
    /// Restores a non-caster character to its original state after a test spell resolves.
    /// Removes the temporary Wizard class level and SpellcastingComponent.
    /// </summary>
    private void RestoreTemporaryCasterSetup()
    {
        if (!_temporaryCasterSetup) return;

        Debug.Log($"[SpellTestPanel] 🧹 Restoring temporary caster setup for {_tempCasterCharacter?.Stats?.CharacterName}");

        // Remove the temporary Wizard class entry
        if (_tempClassEntry != null && _tempCasterCharacter?.Stats?.ClassLevels != null)
        {
            _tempCasterCharacter.Stats.ClassLevels.Remove(_tempClassEntry);
            Debug.Log($"[SpellTestPanel]   Removed temp Wizard class level");
        }

        // Remove the temporary SpellcastingComponent
        if (_tempSpellComp != null)
        {
            Destroy(_tempSpellComp);
            Debug.Log($"[SpellTestPanel]   Removed temp SpellcastingComponent");
        }

        _temporaryCasterSetup = false;
        _tempCasterCharacter = null;
        _tempSpellComp = null;
        _tempClassEntry = null;
    }

    private void CastSpell(SpellData spell)
    {
        Debug.Log($"[SpellTestPanel] ═══ CastSpell START  spell={spell?.Name} ═══");

        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("[SpellTestPanel] GameManager.Instance is null!");
            gm?.CombatUI?.ShowCombatLog("❌ [TEST] GameManager not found!");
            return;
        }

        // Use ActivePC (the character whose turn it currently is)
        CharacterController caster = gm.ActivePC;
        if (caster == null)
        {
            Debug.LogWarning("[SpellTestPanel] No ActivePC — must be used during a PC's turn!");
            gm.CombatUI?.ShowCombatLog("❌ [TEST] No active PC! Use this during a PC's turn.");
            return;
        }

        Debug.Log($"[SpellTestPanel] Caster: {caster.Stats?.CharacterName} (ActivePC)");

        // ── If ActivePC is a non-caster, temporarily configure them as a Wizard ──
        EnsureTemporaryCasterSetup(caster);

        // Apply +4 boost to primary casting ability
        ApplyAbilityBoost(caster);

        // Ensure the spell is available (known, prepared, has slot)
        EnsureSpellAvailable(caster, spell);

        string abilityInfo = $"{_boostedAbility} boosted +4 ({_originalAbilityScore}→{_originalAbilityScore + 4})";
        string casterNote = _temporaryCasterSetup ? " (temp Wizard CL10)" : "";
        gm.CombatUI?.ShowCombatLog($"🔮 [TEST] Casting {spell.Name} via {caster.Stats?.CharacterName}{casterNote} | {abilityInfo}");

        // Use TestCastSpellFromPanel which bypasses ActivePC/turn-phase guards
        try
        {
            var metamagic = new MetamagicData();
            gm.TestCastSpellFromPanel(caster, spell, true, metamagic);
            gm.CombatUI?.ShowCombatLog($"✅ [TEST] Spell targeting initiated for {spell.Name}");
            Debug.Log($"[SpellTestPanel] TestCastSpellFromPanel returned normally.");
        }
        catch (System.Exception ex)
        {
            gm.CombatUI?.ShowCombatLog($"❌ [TEST] Cast error: {ex.Message}");
            Debug.LogError($"[SpellTestPanel] Cast error: {ex}");
            // Restore immediately on error
            RestoreAbilityBoost();
            RestoreTemporaryCasterSetup();
        }
    }

    private void EnsureSpellAvailable(CharacterController caster, SpellData spell)
    {
        if (caster == null) return;

        SpellcastingComponent spellComp = caster.Spellcasting;
        if (spellComp == null)
        {
            Debug.LogWarning($"[SpellTestPanel] EnsureSpellAvailable: No SpellcastingComponent on {caster.Stats?.CharacterName}");
            return;
        }

        // ── Ensure known ──
        if (spellComp.KnownSpells != null)
        {
            bool alreadyKnown = spellComp.KnownSpells.Any(s => s.SpellId == spell.SpellId);
            if (!alreadyKnown)
            {
                spellComp.KnownSpells.Add(spell);
                Debug.Log($"[SpellTestPanel] Added to KnownSpells: {spell.Name}");
            }
        }

        // ── Ensure prepared ──
        if (spellComp.PreparedSpells != null)
        {
            bool alreadyPrepared = spellComp.PreparedSpells.Any(s => s.SpellId == spell.SpellId);
            if (!alreadyPrepared)
            {
                spellComp.PreparedSpells.Add(spell);
                Debug.Log($"[SpellTestPanel] Added to PreparedSpells: {spell.Name}");
            }
        }

        // ── Ensure an unused spell slot exists ──
        if (spellComp.SpellSlots != null)
        {
            bool hasSlot = spellComp.SpellSlots.Any(s =>
                s.PreparedSpell != null && s.PreparedSpell.SpellId == spell.SpellId && !s.IsUsed);
            if (!hasSlot)
            {
                spellComp.SpellSlots.Add(new SpellSlot(spell.SpellLevel, spell));
                Debug.Log($"[SpellTestPanel] Added SpellSlot for {spell.Name} at level {spell.SpellLevel}");
            }
        }

        // ── Infinite slots: max-out remaining counts ──
        if (spellComp.SlotsRemaining != null)
        {
            for (int i = 0; i < spellComp.SlotsRemaining.Length; i++)
            {
                spellComp.SlotsRemaining[i] = 99;
            }

            // Also un-use any used slots for this spell
            if (spellComp.SpellSlots != null)
            {
                foreach (var slot in spellComp.SpellSlots)
                {
                    if (slot.PreparedSpell != null && slot.PreparedSpell.SpellId == spell.SpellId && slot.IsUsed)
                    {
                        slot.IsUsed = false;
                    }
                }
            }
        }
    }

    // ========== STATS ==========

    private void RefreshStats()
    {
        if (_statsText == null) return;

        _statsText.text = $"📊 Dmg: {_totalDamageDealt} | Hits: {_totalTargetsHit} | " +
            $"Saves: {_totalSavesMade}✓/{_totalSavesFailed}✗ | " +
            $"SR: {_totalSRPassed}✓/{_totalSRFailed}✗";
    }

    // ========== PUBLIC API (for integration) ==========

    /// <summary>
    /// Call from combat system to track damage dealt during testing.
    /// </summary>
    public void TrackDamage(int amount, int targetsHit)
    {
        _totalDamageDealt += amount;
        _totalTargetsHit += targetsHit;
        RefreshStats();
    }

    /// <summary>
    /// Call from combat system to track save results during testing.
    /// </summary>
    public void TrackSave(bool passed)
    {
        if (passed) _totalSavesMade++;
        else _totalSavesFailed++;
        RefreshStats();
    }

    /// <summary>
    /// Call from combat system to track SR check results during testing.
    /// </summary>
    public void TrackSR(bool overcame)
    {
        if (overcame) _totalSRPassed++;
        else _totalSRFailed++;
        RefreshStats();
    }

    /// <summary>
    /// Add a message to the spell testing log from external code.
    /// </summary>
    public void LogMessage(string message, Color? color = null)
    {
        GameManager gm = GameManager.Instance;
        gm?.CombatUI?.ShowCombatLog($"[TEST] {message}");
    }
}
