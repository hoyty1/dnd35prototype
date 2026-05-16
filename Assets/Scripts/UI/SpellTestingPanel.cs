using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

/// <summary>
/// DEV/DEBUG Spell Testing Panel for rapid spell playtesting.
/// Toggle with F12 key. Provides spell selection by level, caster configuration,
/// enemy spawning, quick actions, and filtered combat log.
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
    private int _casterLevel = 5;
    private int _intScore = 18;
    private int _chaScore = 18;
    private bool _infiniteSlots = true;
    private bool _autoConcentration = true;
    private string _searchFilter = "";
    private int _selectedSpellLevel = -1; // -1 = All
    private string _selectedCasterType = "Player PC";
    private string _combatLogFilter = "All";

    // Enemy spawner state
    private int _customHP = 30;
    private int _customAC = 15;
    private int _customFort = 3;
    private int _customRef = 3;
    private int _customWill = 3;
    private int _customSR = 0;
    private string _spawnDistance = "Near";

    // Stats tracking
    private int _totalDamageDealt = 0;
    private int _totalTargetsHit = 0;
    private int _totalSavesMade = 0;
    private int _totalSavesFailed = 0;
    private int _totalSRPassed = 0;
    private int _totalSRFailed = 0;

    // ========== UI REFERENCES ==========
    private ScrollRect _spellListScroll;
    private Transform _spellListContent;
    private InputField _searchInput;
    private Text _casterInfoText;
    private Text _statsText;
    private ScrollRect _logScroll;
    private Transform _logContent;
    private Text _casterLevelText;
    private Text _intText;
    private Text _chaText;
    private List<GameObject> _spellEntries = new List<GameObject>();
    private List<Text> _logEntries = new List<Text>();
    private List<string> _allLogMessages = new List<string>();

    // Level filter buttons
    private List<Button> _levelFilterButtons = new List<Button>();

    // ========== COLORS ==========
    private static readonly Color PanelBg = new Color(0.08f, 0.09f, 0.14f, 0.96f);
    private static readonly Color SectionBg = new Color(0.12f, 0.13f, 0.2f, 0.95f);
    private static readonly Color HeaderColor = new Color(1f, 0.85f, 0.2f, 1f);
    private static readonly Color SubHeaderColor = new Color(0.6f, 0.8f, 1f, 1f);
    private static readonly Color SpellEntryBg = new Color(0.15f, 0.16f, 0.24f, 0.9f);
    private static readonly Color SpellEntryHover = new Color(0.2f, 0.22f, 0.35f, 0.95f);
    private static readonly Color CastBtnColor = new Color(0.2f, 0.6f, 0.3f, 1f);
    private static readonly Color SpawnBtnColor = new Color(0.3f, 0.5f, 0.8f, 1f);
    private static readonly Color DangerBtnColor = new Color(0.8f, 0.25f, 0.2f, 1f);
    private static readonly Color ActionBtnColor = new Color(0.5f, 0.4f, 0.7f, 1f);
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
                RefreshCasterInfo();
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
        _panelRect.anchorMax = new Vector2(0.95f, 0.95f);
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

        // ===== MAIN CONTENT (horizontal split) =====
        GameObject contentRow = new GameObject("ContentRow");
        contentRow.transform.SetParent(_panelRoot.transform, false);
        RectTransform contentRT = contentRow.AddComponent<RectTransform>();
        contentRT.sizeDelta = new Vector2(0, 800);
        LayoutElement contentLE = contentRow.AddComponent<LayoutElement>();
        contentLE.preferredHeight = 800;
        contentLE.flexibleHeight = 1;  // Allow it to grow with available space
        HorizontalLayoutGroup contentHlg = contentRow.AddComponent<HorizontalLayoutGroup>();
        contentHlg.spacing = 6;
        contentHlg.childControlWidth = true;
        contentHlg.childControlHeight = true;
        contentHlg.childForceExpandWidth = false;
        contentHlg.childForceExpandHeight = true;

        // Left column - Spell Selection (50%)
        GameObject leftCol = CreateSection(contentRow.transform, "SpellSelection", 0.50f);
        BuildSpellSelectionSection(leftCol.transform, font);

        // Middle column - Config & Spawner (25%)
        GameObject midCol = CreateSection(contentRow.transform, "ConfigSpawner", 0.25f);
        BuildConfigAndSpawnerSection(midCol.transform, font);

        // Right column - Actions & Log (25%)
        GameObject rightCol = CreateSection(contentRow.transform, "ActionsLog", 0.25f);
        BuildActionsAndLogSection(rightCol.transform, font);

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

        // --- FIX: Ensure the scroll panel RectTransform stretches to fill available space ---
        RectTransform spellScrollRT = _spellListScroll.GetComponent<RectTransform>();
        spellScrollRT.anchorMin = new Vector2(0f, 0f);
        spellScrollRT.anchorMax = new Vector2(1f, 1f);
        spellScrollRT.sizeDelta = new Vector2(0, 800);  // Large explicit height so it's definitely visible
        spellScrollRT.offsetMin = Vector2.zero;
        spellScrollRT.offsetMax = Vector2.zero;

        LayoutElement scrollLE = _spellListScroll.gameObject.AddComponent<LayoutElement>();
        scrollLE.flexibleHeight = 1;
        scrollLE.preferredHeight = 800;
        scrollLE.minHeight = 400;  // Ensure minimum visible height

        // --- FIX: Ensure the viewport RectTransform fully covers the scroll panel ---
        RectTransform viewportRT = _spellListScroll.viewport;
        if (viewportRT != null)
        {
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.offsetMin = Vector2.zero;
            viewportRT.offsetMax = Vector2.zero;
            viewportRT.sizeDelta = Vector2.zero;
        }

        _spellListContent = _spellListScroll.content;

        // --- FIX: Properly anchor content to top-stretch so it grows downward ---
        RectTransform contentRT = _spellListContent as RectTransform;
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.sizeDelta = new Vector2(0f, 0f);  // ContentSizeFitter will expand this

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

    // ========== CASTER CONFIG & ENEMY SPAWNER ==========

    private void BuildConfigAndSpawnerSection(Transform parent, Font font)
    {
        // Use a scroll view so everything fits
        ScrollRect configScroll = UIFactory.CreateScrollPanel(parent, "ConfigScroll");
        RectTransform configScrollRT = configScroll.GetComponent<RectTransform>();
        configScrollRT.sizeDelta = new Vector2(0, 500);
        LayoutElement scrollLE = configScroll.gameObject.AddComponent<LayoutElement>();
        scrollLE.flexibleHeight = 1;
        scrollLE.preferredHeight = 500;
        Transform content = configScroll.content;
        VerticalLayoutGroup vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 3;
        vlg.padding = new RectOffset(4, 4, 4, 4);
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        ContentSizeFitter csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ---- CASTER CONFIG ----
        Text casterHeader = UIFactory.CreateLabel(content, "🧙 CASTER CONFIG", 13,
            TextAnchor.MiddleCenter, HeaderColor, "CasterHeader", font);
        casterHeader.fontStyle = FontStyle.Bold;
        AddLayoutHeight(casterHeader.gameObject, 20);

        // Caster type buttons
        string[] casterTypes = { "Player PC", "Test Wizard", "Test Sorcerer" };
        foreach (string ct in casterTypes)
        {
            string type = ct;
            Button btn = UIFactory.CreateButton(content, type,
                () => { _selectedCasterType = type; RefreshCasterInfo(); },
                new Vector2(0, 24), type == _selectedCasterType ? FilterActiveColor : FilterInactiveColor,
                $"Caster_{type}", font, 11);
            AddLayoutHeight(btn.gameObject, 24);
        }

        // Caster Level
        _casterLevelText = CreateSliderRow(content, "Caster Level", 1, 20, _casterLevel, font,
            (val) => { _casterLevel = (int)val; RefreshCasterInfo(); });

        // Int Score
        _intText = CreateSliderRow(content, "INT", 3, 30, _intScore, font,
            (val) => { _intScore = (int)val; RefreshCasterInfo(); });

        // Cha Score
        _chaText = CreateSliderRow(content, "CHA", 3, 30, _chaScore, font,
            (val) => { _chaScore = (int)val; RefreshCasterInfo(); });

        // Checkboxes
        CreateToggleRow(content, "♾ Infinite Spell Slots", _infiniteSlots, font,
            (val) => _infiniteSlots = val);
        CreateToggleRow(content, "🎯 Auto Concentration", _autoConcentration, font,
            (val) => _autoConcentration = val);

        // Caster info display
        _casterInfoText = UIFactory.CreateLabel(content, "Caster: Player PC\nCL: 5 | DC: 14",
            11, TextAnchor.MiddleLeft, new Color(0.7f, 0.9f, 0.7f), "CasterInfo", font);
        AddLayoutHeight(_casterInfoText.gameObject, 40);

        // ---- Separator ----
        CreateSeparator(content);

        // ---- ENEMY SPAWNER ----
        Text spawnHeader = UIFactory.CreateLabel(content, "👹 ENEMY SPAWNER", 13,
            TextAnchor.MiddleCenter, HeaderColor, "SpawnHeader", font);
        spawnHeader.fontStyle = FontStyle.Bold;
        AddLayoutHeight(spawnHeader.gameObject, 20);

        // Preset buttons
        CreateSpawnPresetButton(content, "Weak Enemy", "Low HP/saves, no SR", font,
            10, 12, 0, 0, 0, 0);
        CreateSpawnPresetButton(content, "Tough Enemy", "High HP/saves, SR 15", font,
            60, 18, 6, 6, 6, 15);
        CreateSpawnPresetButton(content, "Boss Enemy", "Very high HP/saves, SR 25", font,
            120, 22, 10, 10, 10, 25);
        CreateSpawnPresetButton(content, "Swarm (4)", "4 weak enemies in formation", font,
            10, 12, 0, 0, 0, 0, true);

        // Spawn distance
        CreateSeparator(content);
        Text distLabel = UIFactory.CreateLabel(content, "Spawn Distance:", 11,
            TextAnchor.MiddleLeft, SubHeaderColor, "DistLabel", font);
        AddLayoutHeight(distLabel.gameObject, 18);

        string[] distances = { "Near", "Medium", "Long" };
        GameObject distRow = new GameObject("DistRow");
        distRow.transform.SetParent(content, false);
        AddLayoutHeight(distRow, 24);
        HorizontalLayoutGroup distHlg = distRow.AddComponent<HorizontalLayoutGroup>();
        distHlg.spacing = 3;
        distHlg.childControlWidth = true;
        distHlg.childControlHeight = true;
        distHlg.childForceExpandWidth = true;
        distHlg.childForceExpandHeight = true;

        foreach (string d in distances)
        {
            string dist = d;
            UIFactory.CreateButton(distRow.transform, dist,
                () => _spawnDistance = dist,
                new Vector2(50, 22), SpawnBtnColor, $"Dist_{dist}", font, 10);
        }

        // Custom enemy config
        CreateSeparator(content);
        Text customLabel = UIFactory.CreateLabel(content, "Custom Enemy:", 11,
            TextAnchor.MiddleLeft, SubHeaderColor, "CustomLabel", font);
        AddLayoutHeight(customLabel.gameObject, 18);

        CreateSliderRow(content, "HP", 1, 200, _customHP, font, (v) => _customHP = (int)v);
        CreateSliderRow(content, "AC", 5, 30, _customAC, font, (v) => _customAC = (int)v);
        CreateSliderRow(content, "Fort", -5, 15, _customFort, font, (v) => _customFort = (int)v);
        CreateSliderRow(content, "Ref", -5, 15, _customRef, font, (v) => _customRef = (int)v);
        CreateSliderRow(content, "Will", -5, 15, _customWill, font, (v) => _customWill = (int)v);
        CreateSliderRow(content, "SR", 0, 30, _customSR, font, (v) => _customSR = (int)v);

        Button spawnCustomBtn = UIFactory.CreateButton(content, "⚔ Spawn Custom Enemy",
            () => SpawnEnemy(_customHP, _customAC, _customFort, _customRef, _customWill, _customSR, false),
            new Vector2(0, 28), SpawnBtnColor, "SpawnCustom", font, 12);
        AddLayoutHeight(spawnCustomBtn.gameObject, 28);

        Button clearEnemiesBtn = UIFactory.CreateButton(content, "🗑 Clear All Enemies",
            () => ClearAllEnemies(),
            new Vector2(0, 26), DangerBtnColor, "ClearEnemies", font, 11);
        AddLayoutHeight(clearEnemiesBtn.gameObject, 26);
    }

    // ========== QUICK ACTIONS & COMBAT LOG ==========

    private void BuildActionsAndLogSection(Transform parent, Font font)
    {
        // Use scroll for this column too
        ScrollRect actionsScroll = UIFactory.CreateScrollPanel(parent, "ActionsScroll");
        RectTransform actionsScrollRT = actionsScroll.GetComponent<RectTransform>();
        actionsScrollRT.sizeDelta = new Vector2(0, 500);
        LayoutElement scrollLE = actionsScroll.gameObject.AddComponent<LayoutElement>();
        scrollLE.flexibleHeight = 1;
        scrollLE.preferredHeight = 500;
        Transform content = actionsScroll.content;
        VerticalLayoutGroup vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 3;
        vlg.padding = new RectOffset(4, 4, 4, 4);
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        ContentSizeFitter csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ---- QUICK ACTIONS ----
        Text actionsHeader = UIFactory.CreateLabel(content, "⚡ QUICK ACTIONS", 13,
            TextAnchor.MiddleCenter, HeaderColor, "ActionsHeader", font);
        actionsHeader.fontStyle = FontStyle.Bold;
        AddLayoutHeight(actionsHeader.gameObject, 20);

        CreateActionButton(content, "🔄 Reset Combat", "Clear effects, restore HP", font,
            ActionBtnColor, () => ResetCombat());
        CreateActionButton(content, "✨ Refresh Spell Slots", "Restore all slots", font,
            ActionBtnColor, () => RefreshSpellSlots());
        CreateActionButton(content, "💀 Kill All Enemies", "Instant victory", font,
            DangerBtnColor, () => KillAllEnemies());
        CreateActionButton(content, "💚 Heal All", "Full HP for everyone", font,
            CastBtnColor, () => HealAll());
        CreateActionButton(content, "🧹 Clear Area Effects", "Remove persistent effects", font,
            ActionBtnColor, () => ClearAreaEffects());
        CreateActionButton(content, "📊 Reset Stats", "Clear tracking stats", font,
            FilterInactiveColor, () => ResetStats());

        // ---- Separator ----
        CreateSeparator(content);

        // ---- COMBAT LOG FILTER ----
        Text logHeader = UIFactory.CreateLabel(content, "📋 COMBAT LOG", 13,
            TextAnchor.MiddleCenter, HeaderColor, "LogHeader", font);
        logHeader.fontStyle = FontStyle.Bold;
        AddLayoutHeight(logHeader.gameObject, 20);

        // Filter buttons
        string[] filters = { "All", "Spells", "Damage", "Saves", "Area" };
        GameObject filterRow = new GameObject("LogFilterRow");
        filterRow.transform.SetParent(content, false);
        AddLayoutHeight(filterRow, 22);
        HorizontalLayoutGroup filterHlg = filterRow.AddComponent<HorizontalLayoutGroup>();
        filterHlg.spacing = 2;
        filterHlg.childControlWidth = true;
        filterHlg.childControlHeight = true;
        filterHlg.childForceExpandWidth = true;
        filterHlg.childForceExpandHeight = true;

        foreach (string f in filters)
        {
            string filter = f;
            UIFactory.CreateButton(filterRow.transform, filter,
                () => { _combatLogFilter = filter; RefreshLog(); },
                new Vector2(40, 20), FilterInactiveColor, $"LogFilter_{filter}", font, 9);
        }

        // Log display (inline, since we're already in a scrollable area)
        for (int i = 0; i < 30; i++)
        {
            Text logLine = UIFactory.CreateLabel(content, "",
                10, TextAnchor.MiddleLeft, Color.white, $"LogLine_{i}", font);
            AddLayoutHeight(logLine.gameObject, 14);
            logLine.gameObject.SetActive(false);
            _logEntries.Add(logLine);
        }

        Button clearLogBtn = UIFactory.CreateButton(content, "Clear Log",
            () => ClearLog(),
            new Vector2(0, 22), FilterInactiveColor, "ClearLog", font, 10);
        AddLayoutHeight(clearLogBtn.gameObject, 22);
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

    private Text CreateSliderRow(Transform parent, string label, float min, float max,
        float initial, Font font, UnityEngine.Events.UnityAction<float> onChanged)
    {
        GameObject row = new GameObject($"Slider_{label}");
        row.transform.SetParent(parent, false);
        AddLayoutHeight(row, 20);
        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 4;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        Text lbl = UIFactory.CreateLabel(row.transform, label, 10,
            TextAnchor.MiddleLeft, Color.white, "Label", font);
        LayoutElement lblLE = lbl.gameObject.AddComponent<LayoutElement>();
        lblLE.preferredWidth = 40;

        // Decrease button
        Text valText = null;
        Button decBtn = UIFactory.CreateButton(row.transform, "-",
            null, new Vector2(20, 18), FilterInactiveColor, "Dec", font, 12);
        LayoutElement decLE = decBtn.gameObject.AddComponent<LayoutElement>();
        decLE.preferredWidth = 20;

        // Value text
        valText = UIFactory.CreateLabel(row.transform, ((int)initial).ToString(), 10,
            TextAnchor.MiddleCenter, Color.white, "Value", font);
        LayoutElement valLE = valText.gameObject.AddComponent<LayoutElement>();
        valLE.preferredWidth = 30;
        valLE.flexibleWidth = 0;

        // Increase button
        Button incBtn = UIFactory.CreateButton(row.transform, "+",
            null, new Vector2(20, 18), FilterInactiveColor, "Inc", font, 12);
        LayoutElement incLE = incBtn.gameObject.AddComponent<LayoutElement>();
        incLE.preferredWidth = 20;

        // Wire up buttons
        float currentVal = initial;
        Text capturedValText = valText;
        decBtn.onClick.AddListener(() =>
        {
            currentVal = Mathf.Max(min, currentVal - 1);
            capturedValText.text = ((int)currentVal).ToString();
            onChanged?.Invoke(currentVal);
        });
        incBtn.onClick.AddListener(() =>
        {
            currentVal = Mathf.Min(max, currentVal + 1);
            capturedValText.text = ((int)currentVal).ToString();
            onChanged?.Invoke(currentVal);
        });

        return valText;
    }

    private void CreateToggleRow(Transform parent, string label, bool initial, Font font,
        System.Action<bool> onChanged)
    {
        GameObject row = new GameObject($"Toggle_{label}");
        row.transform.SetParent(parent, false);
        AddLayoutHeight(row, 22);

        bool state = initial;
        Button btn = UIFactory.CreateButton(row.transform, (state ? "☑ " : "☐ ") + label,
            null, new Vector2(0, 22), state ? CastBtnColor : FilterInactiveColor,
            "ToggleBtn", font, 10);

        RectTransform btnRT = btn.GetComponent<RectTransform>();
        btnRT.anchorMin = Vector2.zero;
        btnRT.anchorMax = Vector2.one;
        btnRT.offsetMin = Vector2.zero;
        btnRT.offsetMax = Vector2.zero;

        Text btnText = btn.GetComponentInChildren<Text>();
        btn.onClick.AddListener(() =>
        {
            state = !state;
            btnText.text = (state ? "☑ " : "☐ ") + label;
            Image img = btn.GetComponent<Image>();
            if (img != null) img.color = state ? CastBtnColor : FilterInactiveColor;
            onChanged?.Invoke(state);
        });
    }

    private void CreateSpawnPresetButton(Transform parent, string label, string desc, Font font,
        int hp, int ac, int fort, int refSave, int will, int sr, bool isSwarm = false)
    {
        Button btn = UIFactory.CreateButton(parent, $"{label}", () =>
        {
            if (isSwarm)
            {
                for (int i = 0; i < 4; i++)
                    SpawnEnemy(hp, ac, fort, refSave, will, sr, false);
            }
            else
            {
                SpawnEnemy(hp, ac, fort, refSave, will, sr, false);
            }
        }, new Vector2(0, 26), SpawnBtnColor, $"Spawn_{label}", font, 11);
        AddLayoutHeight(btn.gameObject, 26);
    }

    private void CreateActionButton(Transform parent, string label, string tooltip, Font font,
        Color color, UnityEngine.Events.UnityAction onClick)
    {
        Button btn = UIFactory.CreateButton(parent, label, onClick,
            new Vector2(0, 26), color, $"Action_{label}", font, 11);
        AddLayoutHeight(btn.gameObject, 26);
    }

    private void CreateSeparator(Transform parent)
    {
        GameObject sep = new GameObject("Separator");
        sep.transform.SetParent(parent, false);
        RectTransform sepRT = sep.AddComponent<RectTransform>();
        sepRT.sizeDelta = new Vector2(0, 1);
        Image sepImg = sep.AddComponent<Image>();
        sepImg.color = new Color(0.4f, 0.4f, 0.5f, 0.5f);
        AddLayoutHeight(sep, 2);
    }

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
        Debug.Log($"[SpellTestingPanel] SpellDatabase.GetAllSpells() returned {allSpells?.Count ?? 0} spells.");

        if (allSpells == null || allSpells.Count == 0)
        {
            Debug.LogWarning("[SpellTestingPanel] No spells found in database! SpellDatabase.Count = " + SpellDatabase.Count);
            return;
        }

        // Filter
        if (_selectedSpellLevel >= 0)
        {
            allSpells = allSpells.Where(s => s.SpellLevel == _selectedSpellLevel).ToList();
        }

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
        Debug.Log($"[SpellTestingPanel] After filtering: {allSpells.Count} spells to display (level filter={_selectedSpellLevel}, search='{_searchFilter}').");

        Font font = UIFactory.GetDefaultFont();
        int currentLevel = -999;
        int entriesCreated = 0;

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
                headerRT.sizeDelta = new Vector2(0, 20);
                AddLayoutHeight(headerObj, 20);

                Image headerBg = headerObj.AddComponent<Image>();
                headerBg.color = new Color(0.18f, 0.2f, 0.32f, 0.95f);

                Text headerText = UIFactory.CreateLabel(headerObj.transform, $"── {levelName} ──", 12,
                    TextAnchor.MiddleCenter, SubHeaderColor, "HeaderText", font);
                headerText.fontStyle = FontStyle.Bold;
                RectTransform htRT = headerText.GetComponent<RectTransform>();
                htRT.anchorMin = Vector2.zero;
                htRT.anchorMax = Vector2.one;
                htRT.offsetMin = Vector2.zero;
                htRT.offsetMax = Vector2.zero;

                _spellEntries.Add(headerObj);
            }

            // Spell entry
            GameObject entry = CreateSpellEntry(spell, font);
            _spellEntries.Add(entry);
            entriesCreated++;
        }

        Debug.Log($"[SpellTestingPanel] Created {entriesCreated} spell entries in UI.");

        // Force layout rebuild - rebuild content first, then scroll panel, then parent
        if (_spellListContent != null)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_spellListContent as RectTransform);

            // Also rebuild the scroll rect itself and its viewport
            if (_spellListScroll != null)
            {
                RectTransform scrollRT = _spellListScroll.GetComponent<RectTransform>();
                if (scrollRT != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRT);

                // Reset scroll position to top so entries are visible
                _spellListScroll.verticalNormalizedPosition = 1f;
            }

            // Rebuild parent hierarchy to propagate sizes
            RectTransform parentRT = _spellListContent.parent?.parent?.GetComponent<RectTransform>();
            if (parentRT != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRT);

            Canvas.ForceUpdateCanvases();
        }
    }

    private GameObject CreateSpellEntry(SpellData spell, Font font)
    {
        GameObject entry = new GameObject($"Spell_{spell.SpellId}");
        entry.transform.SetParent(_spellListContent, false);
        RectTransform entryRT = entry.AddComponent<RectTransform>();
        entryRT.sizeDelta = new Vector2(0, 28);
        AddLayoutHeight(entry, 28);

        Image entryBg = entry.AddComponent<Image>();
        bool isPlaceholder = spell.IsPlaceholder;
        entryBg.color = isPlaceholder ?
            new Color(0.12f, 0.12f, 0.18f, 0.7f) : SpellEntryBg;

        HorizontalLayoutGroup hlg = entry.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 4;
        hlg.padding = new RectOffset(6, 4, 2, 2);
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        // School color indicator
        Color schoolColor = Color.gray;
        if (!string.IsNullOrEmpty(spell.School) && SchoolColors.ContainsKey(spell.School))
            schoolColor = SchoolColors[spell.School];

        Text schoolDot = UIFactory.CreateLabel(entry.transform, "●", 10,
            TextAnchor.MiddleCenter, schoolColor, "SchoolDot", font);
        LayoutElement dotLE = schoolDot.gameObject.AddComponent<LayoutElement>();
        dotLE.preferredWidth = 14;

        // Spell name
        Color nameColor = isPlaceholder ? new Color(0.5f, 0.5f, 0.5f) : Color.white;
        Text nameText = UIFactory.CreateLabel(entry.transform, spell.Name, 11,
            TextAnchor.MiddleLeft, nameColor, "SpellName", font);
        LayoutElement nameLE = nameText.gameObject.AddComponent<LayoutElement>();
        nameLE.flexibleWidth = 1;

        // School abbreviation
        string schoolAbbr = !string.IsNullOrEmpty(spell.School) ?
            spell.School.Substring(0, System.Math.Min(4, spell.School.Length)) : "???";
        Text schoolText = UIFactory.CreateLabel(entry.transform, schoolAbbr, 9,
            TextAnchor.MiddleCenter, new Color(0.6f, 0.6f, 0.7f), "School", font);
        LayoutElement schoolLE = schoolText.gameObject.AddComponent<LayoutElement>();
        schoolLE.preferredWidth = 35;

        // Range info
        string rangeInfo = GetRangeAbbrev(spell);
        Text rangeText = UIFactory.CreateLabel(entry.transform, rangeInfo, 9,
            TextAnchor.MiddleCenter, new Color(0.6f, 0.7f, 0.6f), "Range", font);
        LayoutElement rangeLE = rangeText.gameObject.AddComponent<LayoutElement>();
        rangeLE.preferredWidth = 35;

        // Cast button
        if (!isPlaceholder)
        {
            SpellData capturedSpell = spell;
            Button castBtn = UIFactory.CreateButton(entry.transform, "Cast",
                () => CastSpell(capturedSpell),
                new Vector2(45, 22), CastBtnColor, "CastBtn", font, 10);
            LayoutElement castLE = castBtn.gameObject.AddComponent<LayoutElement>();
            castLE.preferredWidth = 45;
        }
        else
        {
            Text placeholder = UIFactory.CreateLabel(entry.transform, "(N/A)", 9,
                TextAnchor.MiddleCenter, new Color(0.4f, 0.4f, 0.4f), "Placeholder", font);
            LayoutElement phLE = placeholder.gameObject.AddComponent<LayoutElement>();
            phLE.preferredWidth = 45;
        }

        return entry;
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

    private void CastSpell(SpellData spell)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            AddLog("❌ GameManager not found!", Color.red);
            return;
        }

        CharacterController caster = GetOrCreateTestCaster();
        if (caster == null)
        {
            AddLog("❌ No valid caster available!", Color.red);
            return;
        }

        // Configure caster stats for test
        ConfigureTestCaster(caster);

        // If infinite slots, ensure spell is available
        if (_infiniteSlots)
        {
            EnsureSpellAvailable(caster, spell);
        }

        AddLog($"🔮 Casting: {spell.Name} (CL {_casterLevel})", new Color(0.5f, 0.8f, 1f));

        // Trigger spell casting through GameManager
        // We use reflection-free approach: call OnSpellSelectedWithMetamagic via the public interface
        try
        {
            // Create a no-metamagic data object
            var metamagic = new MetamagicData();

            // Use the existing spell casting flow
            // GameManager stores _pendingSpell and routes to targeting
            var method = typeof(GameManager).GetMethod("OnSpellSelectedWithMetamagic",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (method != null)
            {
                method.Invoke(gm, new object[] { spell, metamagic });
                AddLog($"✅ Spell targeting initiated for {spell.Name}", new Color(0.5f, 1f, 0.5f));
            }
            else
            {
                AddLog("⚠ Could not find spell casting method, trying direct cast...", Color.yellow);
                // Fallback: Try to directly trigger
                TryDirectCast(gm, caster, spell);
            }
        }
        catch (System.Exception ex)
        {
            AddLog($"❌ Cast error: {ex.Message}", Color.red);
            Debug.LogError($"[SpellTestingPanel] Cast error: {ex}");
        }
    }

    private void TryDirectCast(GameManager gm, CharacterController caster, SpellData spell)
    {
        // Fallback approach - log the spell info for manual testing
        AddLog($"📋 Spell: {spell.Name} | Level: {spell.SpellLevel} | School: {spell.School}", Color.white);
        AddLog($"   Range: {spell.RangeCategory} | Target: {spell.TargetType}", Color.white);
        if (spell.DamageDice > 0)
            AddLog($"   Damage: {spell.DamageCount}d{spell.DamageDice} {spell.DamageType}", Color.white);
        AddLog("   ⚠ Use normal spell UI to complete cast.", Color.yellow);
    }

    private CharacterController GetOrCreateTestCaster()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return null;

        if (_selectedCasterType == "Player PC")
        {
            // Use first living PC
            if (gm.PCs != null)
            {
                foreach (var pc in gm.PCs)
                {
                    if (pc != null && pc.Stats != null && pc.Stats.CurrentHP > 0)
                        return pc;
                }
            }
            // Fallback to any PC
            if (gm.PCs != null && gm.PCs.Count > 0)
                return gm.PCs[0];
        }

        // For Test Wizard/Sorcerer, use first PC but configure differently
        if (gm.PCs != null && gm.PCs.Count > 0)
            return gm.PCs[0];

        return null;
    }

    private void ConfigureTestCaster(CharacterController caster)
    {
        if (caster == null || caster.Stats == null) return;

        // If using test caster types, override stats
        if (_selectedCasterType == "Test Wizard" || _selectedCasterType == "Test Sorcerer")
        {
            caster.Stats.INT = _intScore;
            caster.Stats.CHA = _chaScore;
        }

        // Note: Caster level is typically tied to character level
        // The spell DC uses these stats through normal calculation
    }

    private void EnsureSpellAvailable(CharacterController caster, SpellData spell)
    {
        if (caster == null) return;

        // Get or add SpellcastingComponent
        SpellcastingComponent spellComp = caster.GetComponent<SpellcastingComponent>();
        if (spellComp == null) return;

        // If infinite slots, restore all slots
        if (_infiniteSlots && spellComp.SlotsRemaining != null)
        {
            // Ensure the spell is in known/prepared spells
            bool alreadyKnown = spellComp.KnownSpells.Any(s => s.SpellId == spell.SpellId);
            if (!alreadyKnown)
            {
                spellComp.KnownSpells.Add(spell);
            }

            // Refresh slots
            for (int i = 0; i < spellComp.SlotsRemaining.Length; i++)
            {
                spellComp.SlotsRemaining[i] = 99;
            }

            // Ensure spell is in prepared spells
            bool alreadyPrepared = spellComp.PreparedSpells.Any(s => s.SpellId == spell.SpellId);
            if (!alreadyPrepared)
            {
                spellComp.PreparedSpells.Add(spell);
            }

            // Ensure there's a spell slot for this spell
            bool hasSlot = spellComp.SpellSlots.Any(s =>
                s.PreparedSpell != null && s.PreparedSpell.SpellId == spell.SpellId && !s.IsUsed);
            if (!hasSlot)
            {
                spellComp.SpellSlots.Add(new SpellSlot(spell.SpellLevel, spell));
            }
        }
    }

    // ========== ENEMY SPAWNING ==========

    private void SpawnEnemy(int hp, int ac, int fort, int refSave, int will, int sr, bool dummy)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            AddLog("❌ GameManager not found!", Color.red);
            return;
        }

        // Find an inactive NPC slot
        CharacterController npcSlot = null;
        if (gm.NPCs != null)
        {
            foreach (var npc in gm.NPCs)
            {
                if (npc != null && (!npc.gameObject.activeSelf ||
                    (npc.Stats != null && npc.Stats.CurrentHP <= 0)))
                {
                    npcSlot = npc;
                    break;
                }
            }
        }

        if (npcSlot == null)
        {
            AddLog("⚠ No available NPC slots for spawning!", Color.yellow);
            return;
        }

        // Calculate spawn position
        Vector2Int spawnPos = GetSpawnPosition();

        // Configure the NPC with a fresh CharacterStats
        npcSlot.gameObject.SetActive(true);

        int level = Mathf.Max(1, hp / 10);
        // Create stats via proper constructor
        // Use ability scores that produce approximately the desired saves
        // Fort = CON mod + class save; Ref = DEX mod + class save; Will = WIS mod + class save
        CharacterStats stats = new CharacterStats(
            name: $"Test Enemy ({hp}hp)",
            level: level,
            characterClass: "Fighter",
            str: 14, dex: 12, con: 14,
            wis: 10, intelligence: 10, cha: 8,
            bab: level,
            armorBonus: Mathf.Max(0, ac - 11), // AC = 10 + dex(1) + armor
            shieldBonus: 0,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 2,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: hp
        );

        // Adjust saves to match desired values using morale bonus
        // Fort = CONMod(2) + ClassFortSave + morale; desired = fort
        stats.MoraleSaveBonus = 0; // Reset first
        int currentFort = stats.FortitudeSave;
        int currentRef = stats.ReflexSave;
        int currentWill = stats.WillSave;
        // Use morale bonus to bring saves closer to desired (affects all saves equally)
        int avgDesired = (fort + refSave + will) / 3;
        int avgCurrent = (currentFort + currentRef + currentWill) / 3;
        stats.MoraleSaveBonus = avgDesired - avgCurrent;

        stats.SpellResistance = sr;
        stats.NaturalArmorBonus = 0;

        npcSlot.Stats = stats;

        npcSlot.ConfigureTeamControl(CharacterTeam.Enemy, controllable: false);

        // Position on grid
        if (gm.Grid != null)
        {
            SquareCell cell = gm.Grid.GetCell(spawnPos.x, spawnPos.y);
            if (cell != null)
            {
                npcSlot.transform.position = cell.transform.position;
                npcSlot.GridPosition = spawnPos;
            }
        }

        string srText = sr > 0 ? $" SR:{sr}" : "";
        AddLog($"👹 Spawned: {stats.CharacterName} | AC:{ac} Fort:{fort} Ref:{refSave} Will:{will}{srText} at ({spawnPos.x},{spawnPos.y})",
            new Color(0.9f, 0.6f, 0.3f));
    }

    private Vector2Int GetSpawnPosition()
    {
        GameManager gm = GameManager.Instance;
        Vector2Int basePos = new Vector2Int(10, 10);

        // Try to find player position
        if (gm?.PCs != null)
        {
            foreach (var pc in gm.PCs)
            {
                if (pc != null && pc.Stats != null && pc.Stats.CurrentHP > 0)
                {
                    basePos = pc.GridPosition;
                    break;
                }
            }
        }

        int offset;
        switch (_spawnDistance)
        {
            case "Near": offset = 2; break;
            case "Medium": offset = 6; break;
            case "Long": offset = 12; break;
            default: offset = 2; break;
        }

        // Spawn to the right of the player, with some randomness
        int randX = Random.Range(-1, 2);
        int randY = Random.Range(-offset / 2, offset / 2 + 1);
        return new Vector2Int(basePos.x + offset + randX, basePos.y + randY);
    }

    // ========== QUICK ACTIONS ==========

    private void ResetCombat()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        // Heal all PCs and clear conditions
        if (gm.PCs != null)
        {
            foreach (var pc in gm.PCs)
            {
                if (pc?.Stats != null)
                {
                    pc.Stats.CurrentHP = pc.Stats.TotalMaxHP;
                    pc.ClearAllConditions();
                }
            }
        }

        // Heal all NPCs and clear conditions
        if (gm.NPCs != null)
        {
            foreach (var npc in gm.NPCs)
            {
                if (npc?.Stats != null && npc.gameObject.activeSelf)
                {
                    npc.Stats.CurrentHP = npc.Stats.TotalMaxHP;
                    npc.ClearAllConditions();
                }
            }
        }

        RefreshSpellSlots();
        ResetStats();
        AddLog("🔄 Combat reset! All effects cleared, HP restored.", new Color(0.5f, 1f, 0.5f));
    }

    private void RefreshSpellSlots()
    {
        GameManager gm = GameManager.Instance;
        if (gm?.PCs == null) return;

        foreach (var pc in gm.PCs)
        {
            if (pc == null) continue;
            SpellcastingComponent spellComp = pc.GetComponent<SpellcastingComponent>();
            if (spellComp?.SlotsRemaining != null)
            {
                for (int i = 0; i < spellComp.SlotsRemaining.Length; i++)
                {
                    spellComp.SlotsRemaining[i] = 99;
                }

                // Also reset used spell slots
                foreach (var slot in spellComp.SpellSlots)
                {
                    slot.IsUsed = false;
                }
            }
        }
        AddLog("✨ All spell slots refreshed!", new Color(0.8f, 0.8f, 1f));
    }

    private void KillAllEnemies()
    {
        GameManager gm = GameManager.Instance;
        if (gm?.NPCs == null) return;

        int killed = 0;
        foreach (var npc in gm.NPCs)
        {
            if (npc?.Stats != null && npc.gameObject.activeSelf && npc.Stats.CurrentHP > 0)
            {
                npc.Stats.CurrentHP = 0;
                killed++;
            }
        }
        AddLog($"💀 Killed {killed} enemies!", DangerBtnColor);
    }

    private void HealAll()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        int healed = 0;
        if (gm.PCs != null)
        {
            foreach (var pc in gm.PCs)
            {
                if (pc?.Stats != null)
                {
                    pc.Stats.CurrentHP = pc.Stats.TotalMaxHP;
                    healed++;
                }
            }
        }
        if (gm.NPCs != null)
        {
            foreach (var npc in gm.NPCs)
            {
                if (npc?.Stats != null && npc.gameObject.activeSelf)
                {
                    npc.Stats.CurrentHP = npc.Stats.TotalMaxHP;
                    healed++;
                }
            }
        }
        AddLog($"💚 Healed {healed} characters to full HP!", new Color(0.3f, 1f, 0.3f));
    }

    private void ClearAllEnemies()
    {
        GameManager gm = GameManager.Instance;
        if (gm?.NPCs == null) return;

        int cleared = 0;
        foreach (var npc in gm.NPCs)
        {
            if (npc != null && npc.gameObject.activeSelf)
            {
                npc.gameObject.SetActive(false);
                cleared++;
            }
        }
        AddLog($"🗑 Cleared {cleared} enemies from battlefield!", Color.yellow);
    }

    private void ClearAreaEffects()
    {
        // Clear all persistent area effects if the system exists
        try
        {
            var areaEffects = FindObjectsOfType<MonoBehaviour>()
                .Where(mb => mb.GetType().Name.Contains("AreaEffect"))
                .ToArray();

            int cleared = 0;
            foreach (var ae in areaEffects)
            {
                if (ae != null && ae.gameObject != null)
                {
                    Destroy(ae.gameObject);
                    cleared++;
                }
            }
            AddLog($"🧹 Cleared {cleared} area effects!", new Color(0.8f, 0.8f, 1f));
        }
        catch (System.Exception ex)
        {
            AddLog($"⚠ Error clearing area effects: {ex.Message}", Color.yellow);
        }
    }

    // ========== COMBAT LOG ==========

    private void AddLog(string message, Color color)
    {
        _allLogMessages.Add($"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{message}</color>");

        // Also forward to main combat log
        GameManager gm = GameManager.Instance;
        if (gm?.CombatUI != null)
        {
            gm.CombatUI.ShowCombatLog($"[TEST] {message}");
        }

        RefreshLog();
    }

    private void RefreshLog()
    {
        var filtered = FilterLogMessages();
        int startIdx = Mathf.Max(0, filtered.Count - _logEntries.Count);

        for (int i = 0; i < _logEntries.Count; i++)
        {
            int msgIdx = startIdx + i;
            if (msgIdx < filtered.Count)
            {
                _logEntries[i].text = filtered[msgIdx];
                _logEntries[i].supportRichText = true;
                _logEntries[i].gameObject.SetActive(true);
            }
            else
            {
                _logEntries[i].gameObject.SetActive(false);
            }
        }
    }

    private List<string> FilterLogMessages()
    {
        if (_combatLogFilter == "All") return _allLogMessages;

        return _allLogMessages.Where(msg =>
        {
            string lower = msg.ToLower();
            switch (_combatLogFilter)
            {
                case "Spells": return lower.Contains("cast") || lower.Contains("spell") || lower.Contains("🔮");
                case "Damage": return lower.Contains("damage") || lower.Contains("dmg") || lower.Contains("hit");
                case "Saves": return lower.Contains("save") || lower.Contains("sr") || lower.Contains("fort") || lower.Contains("ref") || lower.Contains("will");
                case "Area": return lower.Contains("area") || lower.Contains("aoe") || lower.Contains("wall") || lower.Contains("sphere");
                default: return true;
            }
        }).ToList();
    }

    private void ClearLog()
    {
        _allLogMessages.Clear();
        RefreshLog();
        AddLog("📋 Log cleared.", Color.gray);
    }

    // ========== STATS ==========

    private void RefreshCasterInfo()
    {
        if (_casterInfoText == null) return;

        int intMod = (_intScore - 10) / 2;
        int chaMod = (_chaScore - 10) / 2;
        int spellDC = 10 + intMod; // Base DC for wizard

        if (_selectedCasterType == "Test Sorcerer")
        {
            spellDC = 10 + chaMod;
        }

        _casterInfoText.text = $"Caster: {_selectedCasterType}\n" +
            $"CL: {_casterLevel} | DC: {spellDC}+SL\n" +
            $"INT: {_intScore} (+{intMod}) | CHA: {_chaScore} (+{chaMod})";
    }

    private void RefreshStats()
    {
        if (_statsText == null) return;

        _statsText.text = $"📊 Dmg: {_totalDamageDealt} | Hits: {_totalTargetsHit} | " +
            $"Saves: {_totalSavesMade}✓/{_totalSavesFailed}✗ | " +
            $"SR: {_totalSRPassed}✓/{_totalSRFailed}✗";
    }

    private void ResetStats()
    {
        _totalDamageDealt = 0;
        _totalTargetsHit = 0;
        _totalSavesMade = 0;
        _totalSavesFailed = 0;
        _totalSRPassed = 0;
        _totalSRFailed = 0;
        RefreshStats();
        AddLog("📊 Stats reset!", Color.gray);
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
        AddLog(message, color ?? Color.white);
    }
}
