using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Comprehensive character sheet UI with character selection sidebar, tabbed stats panel,
/// and integrated inventory panel. Toggle with "C" key during gameplay.
///
/// Layout (inspired by classic CRPGs like Baldur's Gate / Pathfinder):
///   [Left Sidebar]  Character portraits (clickable to switch)
///   [Middle Panel]   Tabbed content: Stats | Skills | Feats | Spells
///   [Right Panel]    Integrated Inventory UI (equipment + general items)
/// </summary>
public class CharacterSheetUI : MonoBehaviour
{
    // ===== Root panel =====
    private GameObject _panelRoot;

    // ===== Sub-panels =====
    private GameObject _characterListPanel;
    private GameObject _statsPanel;
    private GameObject _inventoryContainer; // Container for the embedded InventoryUI

    // ===== Inventory integration =====
    private InventoryUI _inventoryUI; // Reference to the existing InventoryUI

    // ===== Tab system =====
    private Dictionary<string, GameObject> _tabContents = new Dictionary<string, GameObject>();
    private string _currentTab = "Stats";

    // ===== Character selection =====
    private List<CharacterController> _partyCharacters = new List<CharacterController>();
    private int _selectedCharIndex = 0;

    // ===== References =====
    private Canvas _parentCanvas;
    private Font _uiFont;

    // ===== Colors =====
    private static readonly Color PanelBg = new Color(0.08f, 0.08f, 0.12f, 0.97f);
    private static readonly Color SectionBg = new Color(0.12f, 0.10f, 0.15f, 1f);
    private static readonly Color TitleBarBg = new Color(0.18f, 0.14f, 0.22f, 1f);
    private static readonly Color TabActive = new Color(0.35f, 0.28f, 0.20f, 1f);
    private static readonly Color TabInactive = new Color(0.20f, 0.16f, 0.12f, 1f);
    private static readonly Color GoldText = new Color(0.92f, 0.82f, 0.55f);
    private static readonly Color LightText = new Color(0.85f, 0.78f, 0.65f);
    private static readonly Color DimText = new Color(0.55f, 0.50f, 0.42f);
    private static readonly Color SeparatorColor = new Color(0.5f, 0.4f, 0.25f, 0.4f);
    private static readonly Color PortraitBorderActive = new Color(0.85f, 0.65f, 0.20f, 1f);
    private static readonly Color PortraitBorderInactive = new Color(0.3f, 0.25f, 0.2f, 0.6f);
    private static readonly Color HealthRed = new Color(0.9f, 0.25f, 0.2f);
    private static readonly Color HealthGreen = new Color(0.3f, 0.85f, 0.3f);

    // ===== Layout constants =====
    private const float PanelWidth = 1060f;
    private const float PanelHeight = 700f;
    private const float SidebarWidth = 80f;
    private const float MiddleWidth = 420f;
    private const float RightWidth = 500f;
    private const float TitleBarHeight = 38f;
    private const float TabBarHeight = 28f;
    private const float Padding = 10f;

    public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

    // ===================================================================
    //  BUILD UI
    // ===================================================================

    public void BuildUI(Canvas canvas)
    {
        _parentCanvas = canvas;
        _uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_uiFont == null) _uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (_uiFont == null) _uiFont = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // Main overlay panel
        _panelRoot = MakePanel(canvas.transform, "CharacterSheetPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(PanelWidth, PanelHeight), PanelBg);

        BuildTitleBar();
        BuildCharacterSidebar();
        BuildStatsPanel();
        BuildInventoryContainer();

        _panelRoot.SetActive(false);
    }

    // ===================================================================
    //  TITLE BAR
    // ===================================================================

    private void BuildTitleBar()
    {
        var bar = MakePanel(_panelRoot.transform, "TitleBar",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            Vector2.zero, new Vector2(0, TitleBarHeight), TitleBarBg);

        MakeText(bar.transform, "Title",
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero,
            "Character Sheet", 20, GoldText, TextAnchor.MiddleCenter, FontStyle.Bold);

        // Close hint
        MakeText(bar.transform, "CloseHint",
            new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f),
            new Vector2(-10, 0), new Vector2(100, 0),
            "[C] Close", 12, DimText, TextAnchor.MiddleRight);

        // Info icon / XP bar placeholder
        MakeText(bar.transform, "InfoHint",
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f),
            new Vector2(10, 0), new Vector2(60, 0),
            "\u2694 \u00d7", 14, DimText, TextAnchor.MiddleLeft);
    }

    // ===================================================================
    //  LEFT SIDEBAR — CHARACTER PORTRAITS
    // ===================================================================

    private void BuildCharacterSidebar()
    {
        _characterListPanel = MakePanel(_panelRoot.transform, "CharacterList",
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(Padding, -TitleBarHeight - Padding), new Vector2(SidebarWidth, -(TitleBarHeight + Padding * 2)),
            SectionBg);

        // Vertical layout
        var vlg = _characterListPanel.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 6;
        vlg.padding = new RectOffset(6, 6, 8, 8);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
    }

    // ===================================================================
    //  MIDDLE — STATS PANEL WITH TABS
    // ===================================================================

    private void BuildStatsPanel()
    {
        float leftOffset = Padding + SidebarWidth + Padding;

        _statsPanel = MakePanel(_panelRoot.transform, "StatsPanel",
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(leftOffset, -TitleBarHeight - Padding),
            new Vector2(MiddleWidth, -(TitleBarHeight + Padding * 2)),
            SectionBg);

        // Tab bar
        BuildTabBar(_statsPanel.transform);

        // Tab content areas
        BuildStatsTabContent(_statsPanel.transform);
        BuildSkillsTabContent(_statsPanel.transform);
        BuildFeatsTabContent(_statsPanel.transform);
        BuildSpellsTabContent(_statsPanel.transform);

        ShowTab("Stats");
    }

    private void BuildTabBar(Transform parent)
    {
        var tabBar = MakePanel(parent, "TabBar",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            Vector2.zero, new Vector2(0, TabBarHeight), new Color(0, 0, 0, 0));

        var hlg = tabBar.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 2;
        hlg.padding = new RectOffset(2, 2, 0, 0);
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        string[] tabs = { "Stats", "Skills", "Feats", "Spells" };
        foreach (var t in tabs)
        {
            var btn = MakePanel(tabBar.transform, $"Tab_{t}",
                Vector2.zero, Vector2.zero, Vector2.zero,
                Vector2.zero, Vector2.zero, TabInactive);

            var button = btn.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1, 1, 1, 0.9f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            button.colors = colors;
            button.targetGraphic = btn.GetComponent<Image>();

            string tabName = t;
            button.onClick.AddListener(() => ShowTab(tabName));

            MakeText(btn.transform, "Label",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                t, 13, GoldText, TextAnchor.MiddleCenter, FontStyle.Bold);
        }
    }

    private void ShowTab(string tabName)
    {
        _currentTab = tabName;
        foreach (var kvp in _tabContents)
            kvp.Value.SetActive(kvp.Key == tabName);

        // Update tab button colors
        var tabBar = _statsPanel.transform.Find("TabBar");
        if (tabBar != null)
        {
            for (int i = 0; i < tabBar.childCount; i++)
            {
                var tab = tabBar.GetChild(i);
                var img = tab.GetComponent<Image>();
                if (img != null)
                    img.color = (tab.name == $"Tab_{tabName}") ? TabActive : TabInactive;
            }
        }

        RefreshCurrentTab();
    }

    // ----- Stats Tab -----

    private void BuildStatsTabContent(Transform parent)
    {
        var container = MakeTabContainer(parent, "StatsContent");
        _tabContents["Stats"] = container;
    }

    // ----- Skills Tab -----

    private void BuildSkillsTabContent(Transform parent)
    {
        var container = MakeTabContainer(parent, "SkillsContent");
        _tabContents["Skills"] = container;
    }

    // ----- Feats Tab -----

    private void BuildFeatsTabContent(Transform parent)
    {
        var container = MakeTabContainer(parent, "FeatsContent");
        _tabContents["Feats"] = container;
    }

    // ----- Spells Tab -----

    private void BuildSpellsTabContent(Transform parent)
    {
        var container = MakeTabContainer(parent, "SpellsContent");
        _tabContents["Spells"] = container;
    }

    /// <summary>Creates a scrollable container for a tab.</summary>
    private GameObject MakeTabContainer(Transform parent, string name)
    {
        // Outer container
        var outer = MakePanel(parent, name,
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f),
            new Vector2(0, -(TabBarHeight / 2)), new Vector2(0, -TabBarHeight),
            new Color(0, 0, 0, 0));

        // ScrollRect
        var scrollView = new GameObject("ScrollView");
        scrollView.transform.SetParent(outer.transform, false);
        var scrollRT = scrollView.AddComponent<RectTransform>();
        scrollRT.anchorMin = Vector2.zero;
        scrollRT.anchorMax = Vector2.one;
        scrollRT.sizeDelta = Vector2.zero;
        scrollRT.anchoredPosition = Vector2.zero;

        var scrollRect = scrollView.AddComponent<ScrollRect>();
        scrollRect.vertical = true;
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 20f;

        // Viewport
        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollView.transform, false);
        var viewportRT = viewport.AddComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.sizeDelta = Vector2.zero;
        viewportRT.anchoredPosition = Vector2.zero;
        var mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        viewport.AddComponent<Image>().color = Color.white;

        scrollRect.viewport = viewportRT;

        // Content
        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var contentRT = content.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = new Vector2(0, 0); // Will be grown by ContentSizeFitter

        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 1;
        vlg.padding = new RectOffset(8, 8, 4, 4);
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRT;

        return outer;
    }

    // ===================================================================
    //  RIGHT — INVENTORY PANEL (Embedded InventoryUI)
    // ===================================================================

    private void BuildInventoryContainer()
    {
        float leftOffset = Padding + SidebarWidth + Padding + MiddleWidth + Padding;

        // Container panel that will hold the embedded InventoryUI
        _inventoryContainer = MakePanel(_panelRoot.transform, "InventoryContainer",
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(leftOffset, -TitleBarHeight - Padding),
            new Vector2(RightWidth, -(TitleBarHeight + Padding * 2)),
            SectionBg);
    }

    /// <summary>
    /// Embeds the existing InventoryUI into the character sheet's right panel.
    /// Called after both CharacterSheetUI and InventoryUI have been built.
    /// </summary>
    public void IntegrateInventoryUI(InventoryUI inventoryUI)
    {
        if (inventoryUI == null || _inventoryContainer == null) return;

        _inventoryUI = inventoryUI;

        // Reparent InventoryUI's panel root into our container
        if (_inventoryUI.PanelRoot != null)
        {
            _inventoryUI.PanelRoot.transform.SetParent(_inventoryContainer.transform, false);

            // Stretch to fill the container
            var invRT = _inventoryUI.PanelRoot.GetComponent<RectTransform>();
            invRT.anchorMin = Vector2.zero;
            invRT.anchorMax = Vector2.one;
            invRT.pivot = new Vector2(0.5f, 0.5f);
            invRT.anchoredPosition = Vector2.zero;
            invRT.sizeDelta = Vector2.zero;

            // Mark InventoryUI as embedded so it knows not to behave as standalone
            _inventoryUI.IsEmbedded = true;

            // Hide standalone-only elements (title bar, close hint) since
            // the CharacterSheetUI already provides its own chrome
            _inventoryUI.HideStandaloneElements();
        }
    }

    // ===================================================================
    //  PUBLIC API
    // ===================================================================

    public void Open(CharacterController character)
    {
        if (_panelRoot == null) return;

        // Build party list from GameManager
        if (GameManager.Instance != null)
            _partyCharacters = new List<CharacterController>(GameManager.Instance.PCs);

        // Find the index of the requested character
        _selectedCharIndex = 0;
        if (character != null)
        {
            for (int i = 0; i < _partyCharacters.Count; i++)
            {
                if (_partyCharacters[i] == character)
                {
                    _selectedCharIndex = i;
                    break;
                }
            }
        }

        RebuildSidebarPortraits();
        RefreshAll();
        _panelRoot.SetActive(true);

        // Show embedded inventory for the selected character
        if (_inventoryUI != null && _inventoryUI.PanelRoot != null)
        {
            var selectedPC = SelectedPC;
            if (selectedPC != null)
            {
                _inventoryUI.CurrentCharacter = selectedPC;
                _inventoryUI.RefreshUI();
            }
            _inventoryUI.PanelRoot.SetActive(true);
        }
    }

    public void Close()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);

        // Hide embedded inventory (but don't null out CurrentCharacter since it's embedded)
        if (_inventoryUI != null && _inventoryUI.IsEmbedded && _inventoryUI.PanelRoot != null)
            _inventoryUI.PanelRoot.SetActive(false);
    }

    public void Toggle(CharacterController character)
    {
        if (IsOpen)
            Close();
        else
            Open(character);
    }

    // ===================================================================
    //  SIDEBAR REBUILD
    // ===================================================================

    private void RebuildSidebarPortraits()
    {
        // Clear existing children
        foreach (Transform child in _characterListPanel.transform)
            Destroy(child.gameObject);

        for (int i = 0; i < _partyCharacters.Count; i++)
        {
            var pc = _partyCharacters[i];
            if (pc == null || pc.Stats == null) continue;

            CreatePortraitButton(i, pc);
        }
    }

    private void CreatePortraitButton(int index, CharacterController pc)
    {
        var stats = pc.Stats;

        // Container
        var container = new GameObject($"Portrait_{index}");
        container.transform.SetParent(_characterListPanel.transform, false);
        var le = container.AddComponent<LayoutElement>();
        le.preferredHeight = 78;
        le.minHeight = 78;

        var rt = container.AddComponent<RectTransform>();

        // Border / background
        var bg = container.AddComponent<Image>();
        bg.color = (index == _selectedCharIndex) ? PortraitBorderActive : PortraitBorderInactive;

        // Portrait image
        var portraitGO = new GameObject("Icon");
        portraitGO.transform.SetParent(container.transform, false);
        var prt = portraitGO.AddComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = new Vector2(0, 4);
        prt.sizeDelta = new Vector2(56, 56);

        var portraitImg = portraitGO.AddComponent<Image>();
        Sprite portrait = IconLoader.GetPortrait(stats.CharacterClass);
        if (portrait != null)
        {
            portraitImg.sprite = portrait;
            portraitImg.preserveAspect = true;
        }
        else
        {
            portraitImg.color = GetClassColor(stats.CharacterClass);
        }

        // Name label below portrait
        var nameLabel = MakeText(container.transform, "Name",
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
            new Vector2(0, 2), new Vector2(0, 14),
            stats.CharacterName, 9, LightText, TextAnchor.MiddleCenter);

        // HP indicator (small bar)
        var hpBarBg = MakePanel(container.transform, "HPBarBg",
            new Vector2(0.1f, 0), new Vector2(0.9f, 0), new Vector2(0.5f, 0),
            new Vector2(0, 0), new Vector2(0, 3),
            new Color(0.2f, 0.05f, 0.05f));

        float hpPct = stats.TotalMaxHP > 0 ? Mathf.Clamp01((float)stats.CurrentHP / stats.TotalMaxHP) : 0f;
        var hpBarFill = MakePanel(hpBarBg.transform, "HPFill",
            new Vector2(0, 0), new Vector2(hpPct, 1), new Vector2(0, 0.5f),
            Vector2.zero, Vector2.zero,
            hpPct > 0.5f ? HealthGreen : HealthRed);

        // Click handler
        var button = container.AddComponent<Button>();
        var btnColors = button.colors;
        btnColors.normalColor = Color.white;
        btnColors.highlightedColor = new Color(1, 1, 1, 0.85f);
        btnColors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        button.colors = btnColors;
        button.targetGraphic = bg;

        int capturedIndex = index;
        button.onClick.AddListener(() => SelectCharacter(capturedIndex));
    }

    private void SelectCharacter(int index)
    {
        if (index < 0 || index >= _partyCharacters.Count) return;
        _selectedCharIndex = index;

        // Update borders
        for (int i = 0; i < _characterListPanel.transform.childCount; i++)
        {
            var child = _characterListPanel.transform.GetChild(i);
            var bg = child.GetComponent<Image>();
            if (bg != null)
                bg.color = (i == index) ? PortraitBorderActive : PortraitBorderInactive;
        }

        RefreshAll();

        // Update embedded inventory to show selected character's inventory
        if (_inventoryUI != null)
        {
            var selectedPC = SelectedPC;
            if (selectedPC != null)
            {
                _inventoryUI.CurrentCharacter = selectedPC;
                _inventoryUI.RefreshUI();
            }
        }
    }

    // ===================================================================
    //  REFRESH
    // ===================================================================

    private CharacterStats SelectedStats
    {
        get
        {
            if (_selectedCharIndex < 0 || _selectedCharIndex >= _partyCharacters.Count) return null;
            return _partyCharacters[_selectedCharIndex]?.Stats;
        }
    }

    private CharacterController SelectedPC
    {
        get
        {
            if (_selectedCharIndex < 0 || _selectedCharIndex >= _partyCharacters.Count) return null;
            return _partyCharacters[_selectedCharIndex];
        }
    }

    private void RefreshAll()
    {
        RefreshCurrentTab();

        // Refresh embedded inventory if present
        if (_inventoryUI != null && _inventoryUI.CurrentCharacter != null)
            _inventoryUI.RefreshUI();
    }

    private void RefreshCurrentTab()
    {
        switch (_currentTab)
        {
            case "Stats":  RefreshStatsTab(); break;
            case "Skills": RefreshSkillsTab(); break;
            case "Feats":  RefreshFeatsTab(); break;
            case "Spells": RefreshSpellsTab(); break;
        }
    }

    // ----- Stats Tab -----

    private void RefreshStatsTab()
    {
        var content = GetTabContent("Stats");
        if (content == null) return;
        ClearChildren(content);

        var stats = SelectedStats;
        if (stats == null) { AddLine(content, "No character selected.", 14, DimText); return; }

        // === Character Header ===
        AddLine(content, stats.CharacterName, 16, GoldText, FontStyle.Bold, 18);
        string alignDisplay = stats.CharacterAlignment != Alignment.None
            ? $"  {stats.AlignmentName}" : "";
        AddLine(content, $"Level {stats.Level}  {stats.RaceName}  {stats.CharacterClass}{alignDisplay}", 11, LightText, FontStyle.Normal, 14);

        // Deity display
        if (!string.IsNullOrEmpty(stats.DeityId))
        {
            AddLine(content, $"\u2726 Deity: {stats.DeityName}", 11, new Color(0.9f, 0.85f, 0.5f), FontStyle.Normal, 14);
        }
        // Domain display (Clerics)
        if (stats.ChosenDomains != null && stats.ChosenDomains.Count > 0)
        {
            AddLine(content, $"\u2726 Domains: {stats.DomainsDisplay}", 11, new Color(0.7f, 0.85f, 1f), FontStyle.Normal, 14);
        }

        AddSeparator(content);

        // === HP / Movement / Initiative ===
        CharacterController selectedPC = SelectedPC;
        HPState hpState = selectedPC != null ? selectedPC.CurrentHPState : HPState.Healthy;

        Color hpColor = hpState == HPState.Healthy
            ? (stats.CurrentHP > stats.TotalMaxHP / 2 ? HealthGreen : HealthRed)
            : new Color(1f, 0.7f, 0.35f);
        AddColoredLine(content, "❤ ", hpColor, $"HP: {stats.CurrentHP}/{stats.TotalMaxHP} [{hpState}]", LightText, 13, 16);
        AddLine(content, $"  Nonlethal: {stats.NonlethalDamage}", 11, new Color(0.93f, 0.76f, 0.52f), FontStyle.Normal, 14);
        if (stats.TempHP > 0)
            AddLine(content, $"  Temp HP: {stats.TempHP}", 11, new Color(0.4f, 0.7f, 1f), FontStyle.Normal, 14);

        AddLine(content, $"\u2694 Initiative: {FormatMod(stats.InitiativeModifier)}     \u27a1 Speed: {stats.SpeedInFeet} ft ({stats.MoveRange} sq)", 11, LightText, FontStyle.Normal, 14);
        AddLine(content, $"⚖ Load: {stats.EncumbranceSummary}", 10, DimText, FontStyle.Normal, 13);
        AddLine(content, $"📏 Size: {stats.SizeCategoryName}    Reach: {stats.NaturalReachFeet} ft ({stats.NaturalReachSquares} sq)    Space: {stats.SpaceSquares} sq", 10, DimText, FontStyle.Normal, 13);
        AddLine(content, $"🧬 Creature Type: {stats.CreatureType}    Natural Armor: {stats.NaturalArmorBonus}", 10, DimText, FontStyle.Normal, 13);

        var activeConditions = new List<string>();
        if (stats.IsProne)
            activeConditions.Add("Prone (-4 melee attacks, +4 AC vs ranged, -4 AC vs melee)");
        if (stats.IsGrappled)
            activeConditions.Add("Grappled (-4 attack; no movement; no threatened squares/AoOs; only light weapons or unarmed; lose DEX bonus to AC only vs non-grapple opponents)");
        if (stats.IsDisarmed)
            activeConditions.Add("Disarmed (-4 attack while unarmed)");
        if (stats.IsFlanked)
            activeConditions.Add("Flanked (-2 AC)");

        if (hpState == HPState.Disabled)
            activeConditions.Add("Disabled (0 HP: can take one move OR one standard action)");
        else if (hpState == HPState.Staggered)
            activeConditions.Add("Staggered (nonlethal damage equals current HP: one move OR one standard action)");
        else if (hpState == HPState.Unconscious)
            activeConditions.Add("Unconscious (nonlethal damage exceeds current HP)");
        else if (hpState == HPState.Dying)
            activeConditions.Add("Dying (-1 to -9 HP: unconscious, loses 1 HP each turn until stable)");
        else if (hpState == HPState.Stable)
            activeConditions.Add("Stable (-1 to -9 HP: unconscious, no longer losing HP)");
        else if (hpState == HPState.Dead)
            activeConditions.Add("Dead (-10 HP or lower)");

        if (activeConditions.Count > 0)
        {
            AddLine(content, "CONDITIONS", 12, GoldText, FontStyle.Bold, 16);
            foreach (string cond in activeConditions)
                AddLine(content, $"  • {cond}", 10, new Color(1f, 0.78f, 0.52f), FontStyle.Normal, 13);

            AddSeparator(content);
        }
        AddSeparator(content);

        // === Ability Scores ===
        AddLine(content, "ABILITY SCORES", 12, GoldText, FontStyle.Bold, 16);
        AddAbilityLine(content, "STR", stats.STR, stats.STRMod);
        AddAbilityLine(content, "DEX", stats.DEX, stats.DEXMod);
        AddAbilityLine(content, "CON", stats.CON, stats.CONMod);
        AddAbilityLine(content, "INT", stats.INT, stats.INTMod);
        AddAbilityLine(content, "WIS", stats.WIS, stats.WISMod);
        AddAbilityLine(content, "CHA", stats.CHA, stats.CHAMod);

        AddSeparator(content);

        // === Armor Class ===
        AddLine(content, "DEFENSE", 12, GoldText, FontStyle.Bold, 16);
        AddLine(content, $"\u26e1 AC: {stats.ArmorClass}", 13, LightText, FontStyle.Bold, 16);

        int effectiveDex = stats.DEXMod;
        if (stats.MaxDexBonus >= 0 && effectiveDex > stats.MaxDexBonus)
            effectiveDex = stats.MaxDexBonus;

        string acBreakdown = $"  10 + Armor {stats.ArmorBonus} + Shield {stats.ShieldBonus} + DEX {FormatMod(effectiveDex)}";
        if (stats.SpellACBonus > 0) acBreakdown += $" + Spell {stats.SpellACBonus}";
        if (stats.DeflectionBonus > 0) acBreakdown += $" + Deflect {stats.DeflectionBonus}";
        if (stats.SizeModifier != 0) acBreakdown += $" + Size {FormatMod(stats.SizeModifier)}";
        AddLine(content, acBreakdown, 9, DimText, FontStyle.Normal, 13);

        string maxDexDisplay = stats.MaxDexBonus < 0 ? "No limit" : $"+{stats.MaxDexBonus}";
        string armorCapDisplay = stats.EquipmentMaxDexBonus < 0 ? "No limit" : $"+{stats.EquipmentMaxDexBonus}";
        string loadCapDisplay = stats.EncumbranceMaxDexBonus < 0 ? "No limit" : $"+{stats.EncumbranceMaxDexBonus}";
        string acpDisplay = stats.ArmorCheckPenalty > 0 ? $"-{stats.ArmorCheckPenalty}" : "0";
        AddLine(content, $"  Max Dex: {maxDexDisplay} (Armor {armorCapDisplay}, Load {loadCapDisplay})", 10, DimText, FontStyle.Normal, 13);
        AddLine(content, $"  ACP: {acpDisplay} (Armor {stats.EquipmentArmorCheckPenalty}, Load {stats.EncumbranceCheckPenalty})", 10, DimText, FontStyle.Normal, 13);

        if (stats.IsAffectedByArcaneSpellFailure)
        {
            int asfChance = Mathf.Clamp(stats.ArcaneSpellFailure, 0, 100);
            Color asfColor = asfChance > 0 ? new Color(1f, 0.7f, 0.4f) : DimText;
            AddLine(content, $"  Arcane Spell Failure: {asfChance}%", 10, asfColor, FontStyle.Bold, 13);
        }
        else if (stats.IsSpellcaster)
        {
            AddLine(content, $"  Arcane Spell Failure: ignored ({stats.SpellcastingKind} caster)", 10, DimText, FontStyle.Normal, 13);
        }

        if (stats.IsSpellcaster)
        {
            AddSeparator(content);
            AddLine(content, "SPELLCASTING CONCENTRATION", 12, GoldText, FontStyle.Bold, 16);

            int concentrationBonus = stats.GetSpellcastingConcentrationBonus(includeCombatCasting: true);
            AddLine(content, $"  Concentration Bonus: {FormatMod(concentrationBonus)}", 11, LightText, FontStyle.Bold, 14);
            AddLine(content, $"  Breakdown: {stats.GetSpellcastingConcentrationBreakdown(includeCombatCasting: true)}", 10, DimText, FontStyle.Normal, 13);

            bool hasCombatCasting = stats.HasFeat("Combat Casting");
            AddLine(content,
                hasCombatCasting
                    ? "  Combat Casting: active (+4 on concentration checks while casting in melee)"
                    : "  Combat Casting: not known",
                10,
                hasCombatCasting ? new Color(0.55f, 0.86f, 1f) : DimText,
                FontStyle.Normal,
                13);
        }
        AddSeparator(content);

        // === Saving Throws ===
        AddLine(content, "SAVING THROWS", 12, GoldText, FontStyle.Bold, 16);
        AddLine(content, $"  Fortitude: {FormatMod(stats.FortitudeSave)}     Reflex: {FormatMod(stats.ReflexSave)}     Will: {FormatMod(stats.WillSave)}", 11, LightText, FontStyle.Normal, 14);

        AddSeparator(content);

        // === Attack ===
        AddLine(content, "ATTACK", 12, GoldText, FontStyle.Bold, 16);
        AddLine(content, $"  Base Attack Bonus: {FormatMod(stats.BaseAttackBonus)}", 11, LightText, FontStyle.Normal, 14);
        AddLine(content, $"  Melee Attack: {FormatMod(stats.AttackBonus)}", 11, LightText, FontStyle.Normal, 14);
        AddLine(content, $"  Ranged Attack: {FormatMod(stats.BaseAttackBonus + stats.DEXMod + stats.SizeModifier)}", 11, LightText, FontStyle.Normal, 14);
        AddLine(content, $"  Damage: {stats.BaseDamageCount}d{stats.BaseDamageDice}{(stats.BonusDamage != 0 ? FormatMod(stats.BonusDamage) : "")}", 11, LightText, FontStyle.Normal, 14);

        // === Active Buffs ===
        // Use StatusEffectManager (present on ALL characters) for buff display,
        // not SpellcastingComponent.ActiveBuffs (only on spellcasters).
        // This ensures non-spellcasters (Fighter, Rogue, etc.) also show active buffs
        // like Bless, Shield of Faith, etc. that were applied to them by party spellcasters.
        var statusMgr = SelectedPC?.GetComponent<StatusEffectManager>();
        if (statusMgr != null && statusMgr.ActiveEffectCount > 0)
        {
            AddSeparator(content);
            AddLine(content, "ACTIVE BUFFS", 12, new Color(0.4f, 0.8f, 1f), FontStyle.Bold, 16);
            foreach (var effect in statusMgr.ActiveEffects)
            {
                string displayName = effect.Spell != null ? effect.Spell.Name : "Unknown";
                string duration = effect.RemainingRounds < 0 ? "permanent" : $"{effect.RemainingRounds} rounds";
                AddLine(content, $"  \u2728 {displayName} ({duration})", 10, new Color(0.6f, 0.85f, 1f), FontStyle.Normal, 13);
            }
        }
    }

    private void AddAbilityLine(Transform content, string name, int score, int mod)
    {
        string modStr = FormatMod(mod);
        Color modColor = mod >= 0 ? new Color(0.5f, 0.9f, 0.5f) : new Color(0.9f, 0.4f, 0.4f);

        var entryGO = new GameObject($"Ability_{name}");
        entryGO.transform.SetParent(content, false);
        var le = entryGO.AddComponent<LayoutElement>();
        le.preferredHeight = 15;
        le.minHeight = 15;
        entryGO.AddComponent<RectTransform>();

        // Name label
        var nameText = MakeText(entryGO.transform, "Name",
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f),
            new Vector2(10, 0), new Vector2(40, 0),
            $"\u2726 {name}", 11, DimText, TextAnchor.MiddleLeft, FontStyle.Bold);

        // Score
        MakeText(entryGO.transform, "Score",
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f),
            new Vector2(60, 0), new Vector2(30, 0),
            score.ToString(), 12, LightText, TextAnchor.MiddleRight, FontStyle.Bold);

        // Modifier
        MakeText(entryGO.transform, "Mod",
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f),
            new Vector2(100, 0), new Vector2(40, 0),
            modStr, 12, modColor, TextAnchor.MiddleLeft);
    }

    // ----- Skills Tab -----

    private void RefreshSkillsTab()
    {
        var content = GetTabContent("Skills");
        if (content == null) return;
        ClearChildren(content);

        var stats = SelectedStats;
        if (stats == null) { AddLine(content, "No character selected.", 14, DimText); return; }

        AddLine(content, $"SKILLS — {stats.CharacterName}", 12, GoldText, FontStyle.Bold, 16);

        // Header row
        AddSkillHeader(content);
        AddSeparator(content);

        // Sort skills: class skills first, then alphabetically
        var sortedSkills = stats.Skills.Values
            .OrderByDescending(s => s.IsClassSkill)
            .ThenBy(s => s.SkillName)
            .ToList();

        foreach (var skill in sortedSkills)
        {
            int totalBonus = stats.GetSkillBonus(skill.SkillName);
            int abilityMod = stats.GetAbilityModForSkill(skill);
            int featBonus = FeatManager.GetSkillFeatBonus(stats, skill.SkillName);
            int classBonus = (skill.IsClassSkill && skill.Ranks > 0) ? 3 : 0;

            // Only show skills with ranks or class skills
            if (skill.Ranks > 0 || skill.IsClassSkill)
            {
                AddSkillRow(content, skill, totalBonus, abilityMod, featBonus, classBonus);
            }
        }

        // Show remaining (non-class, non-ranked) skills in collapsed form
        AddSeparator(content);
        AddLine(content, "Other Skills (untrained):", 10, DimText, FontStyle.Italic, 13);

        foreach (var skill in sortedSkills)
        {
            if (skill.Ranks == 0 && !skill.IsClassSkill && !skill.TrainedOnly)
            {
                int totalBonus = stats.GetSkillBonus(skill.SkillName);
                AddLine(content, $"  {skill.SkillName}: {FormatMod(totalBonus)}", 9, DimText, FontStyle.Normal, 12);
            }
        }
    }

    private void AddSkillHeader(Transform content)
    {
        var headerGO = new GameObject("SkillHeader");
        headerGO.transform.SetParent(content, false);
        var le = headerGO.AddComponent<LayoutElement>();
        le.preferredHeight = 14;
        le.minHeight = 14;
        headerGO.AddComponent<RectTransform>();

        MakeText(headerGO.transform, "H1", V2(0, 0), V2(0, 1), V2(0, 0.5f),
            V2(10, 0), V2(150, 0), "Skill", 10, DimText, TextAnchor.MiddleLeft, FontStyle.Bold);
        MakeText(headerGO.transform, "H2", V2(0, 0), V2(0, 1), V2(0, 0.5f),
            V2(170, 0), V2(40, 0), "Total", 10, DimText, TextAnchor.MiddleCenter, FontStyle.Bold);
        MakeText(headerGO.transform, "H3", V2(0, 0), V2(0, 1), V2(0, 0.5f),
            V2(215, 0), V2(40, 0), "Ranks", 10, DimText, TextAnchor.MiddleCenter, FontStyle.Bold);
        MakeText(headerGO.transform, "H4", V2(0, 0), V2(0, 1), V2(0, 0.5f),
            V2(260, 0), V2(40, 0), "Abil", 10, DimText, TextAnchor.MiddleCenter, FontStyle.Bold);
    }

    private void AddSkillRow(Transform content, Skill skill, int totalBonus, int abilityMod, int featBonus, int classBonus)
    {
        var rowGO = new GameObject($"Skill_{skill.SkillName}");
        rowGO.transform.SetParent(content, false);
        var le = rowGO.AddComponent<LayoutElement>();
        le.preferredHeight = 14;
        le.minHeight = 14;
        rowGO.AddComponent<RectTransform>();

        Color nameColor = skill.IsClassSkill ? LightText : DimText;
        string classMarker = skill.IsClassSkill ? "\u2605 " : "  ";

        MakeText(rowGO.transform, "Name", V2(0, 0), V2(0, 1), V2(0, 0.5f),
            V2(10, 0), V2(150, 0), $"{classMarker}{skill.SkillName}", 11, nameColor, TextAnchor.MiddleLeft);

        Color bonusColor = totalBonus > 0 ? new Color(0.5f, 0.9f, 0.5f) : (totalBonus < 0 ? HealthRed : LightText);
        MakeText(rowGO.transform, "Total", V2(0, 0), V2(0, 1), V2(0, 0.5f),
            V2(170, 0), V2(40, 0), FormatMod(totalBonus), 12, bonusColor, TextAnchor.MiddleCenter, FontStyle.Bold);

        MakeText(rowGO.transform, "Ranks", V2(0, 0), V2(0, 1), V2(0, 0.5f),
            V2(215, 0), V2(40, 0), skill.Ranks.ToString(), 11, LightText, TextAnchor.MiddleCenter);

        MakeText(rowGO.transform, "Abil", V2(0, 0), V2(0, 1), V2(0, 0.5f),
            V2(260, 0), V2(40, 0), FormatMod(abilityMod), 11, DimText, TextAnchor.MiddleCenter);
    }

    // ----- Feats Tab -----

    private void RefreshFeatsTab()
    {
        var content = GetTabContent("Feats");
        if (content == null) return;
        ClearChildren(content);

        var stats = SelectedStats;
        if (stats == null) { AddLine(content, "No character selected.", 14, DimText); return; }

        AddLine(content, $"FEATS — {stats.CharacterName} ({stats.Feats.Count})", 12, GoldText, FontStyle.Bold, 16);
        AddSeparator(content);

        if (stats.IsSpellcaster)
        {
            bool hasCombatCastingFeat = stats.HasFeat("Combat Casting");
            int concentrationBonus = stats.GetSpellcastingConcentrationBonus(includeCombatCasting: true);
            string ccLine = hasCombatCastingFeat
                ? $"Combat Casting: YES (+4) | Total concentration bonus while casting in melee: {FormatMod(concentrationBonus)}"
                : $"Combat Casting: NO | Total concentration bonus while casting in melee: {FormatMod(concentrationBonus)}";
            AddLine(content, ccLine, 10, hasCombatCastingFeat ? new Color(0.55f, 0.86f, 1f) : DimText, FontStyle.Normal, 13);
            AddSeparator(content);
        }

        if (stats.Feats.Count == 0)
        {
            AddLine(content, "No feats.", 12, DimText);
            return;
        }

        // Group feats by type using FeatDefinitions
        var sortedFeats = stats.Feats.OrderBy(f => f).ToList();
        foreach (var featName in sortedFeats)
        {
            FeatDefinition def = null;
            if (FeatDefinitions.AllFeats != null && FeatDefinitions.AllFeats.ContainsKey(featName))
                def = FeatDefinitions.AllFeats[featName];

            string typeTag = "";
            Color typeColor = LightText;

            if (def != null)
            {
                switch (def.Type)
                {
                    case FeatType.General:    typeTag = "[General]"; typeColor = LightText; break;
                    case FeatType.Combat:     typeTag = "[Combat]"; typeColor = new Color(0.9f, 0.5f, 0.4f); break;
                    case FeatType.Metamagic:  typeTag = "[Metamagic]"; typeColor = new Color(0.5f, 0.6f, 1f); break;
                    case FeatType.ItemCreation: typeTag = "[Item Creation]"; typeColor = new Color(0.6f, 0.9f, 0.6f); break;
                }
            }

            // Feat name with type badge
            var featGO = new GameObject($"Feat_{featName}");
            featGO.transform.SetParent(content, false);
            var le = featGO.AddComponent<LayoutElement>();
            le.preferredHeight = 15;
            le.minHeight = 15;
            featGO.AddComponent<RectTransform>();

            MakeText(featGO.transform, "Name", V2(0, 0), V2(0, 1), V2(0, 0.5f),
                V2(10, 0), V2(200, 0), $"\u2726 {featName}", 12, LightText, TextAnchor.MiddleLeft);

            MakeText(featGO.transform, "Type", V2(0, 0), V2(0, 1), V2(0, 0.5f),
                V2(220, 0), V2(120, 0), typeTag, 10, typeColor, TextAnchor.MiddleLeft);

            // Description on next line if available
            if (def != null && !string.IsNullOrEmpty(def.Description))
            {
                AddLine(content, $"    {def.Description}", 9, DimText, FontStyle.Italic, 12);
            }
        }
    }

    // ----- Spells Tab -----

    private void RefreshSpellsTab()
    {
        var content = GetTabContent("Spells");
        if (content == null) return;
        ClearChildren(content);

        var stats = SelectedStats;
        if (stats == null) { AddLine(content, "No character selected.", 14, DimText); return; }

        var spellComp = SelectedPC?.GetComponent<SpellcastingComponent>();
        if (spellComp == null)
        {
            AddLine(content, $"{stats.CharacterName} is not a spellcaster.", 13, DimText);
            return;
        }

        AddLine(content, $"SPELLS \u2014 {stats.CharacterName} ({stats.CharacterClass})", 12, GoldText, FontStyle.Bold, 16);

        // Spell slot summary (now includes ∞ for cantrips)
        if (spellComp.SlotsMax != null)
        {
            string slotSummary = spellComp.GetSlotSummary();
            AddLine(content, slotSummary, 10, new Color(0.6f, 0.8f, 1f), FontStyle.Normal, 14);
        }

        AddSeparator(content);

        bool usesSlotSystem = (stats.IsWizard || stats.IsCleric) && spellComp.SpellSlots.Count > 0;

        if (usesSlotSystem)
        {
            // ======== WIZARD/CLERIC: Show slot-based prepared spells ========
            RefreshSlotSpellsTab(content, spellComp);
        }
        else
        {
            // ======== OTHER CASTERS: Show standard spell list ========
            RefreshStandardSpellsTab(content, spellComp);
        }
    }

    /// <summary>
    /// Refresh the spells tab for slot-based casters (Wizards and Clerics).
    /// Shows: Prepared Spells (with slot counts and ∞ for cantrips), then spell list.
    /// </summary>
    private void RefreshSlotSpellsTab(Transform content, SpellcastingComponent spellComp)
    {
        // --- PREPARED SPELLS SECTION ---
        AddLine(content, "PREPARED SPELLS", 11, new Color(0.9f, 0.8f, 0.4f), FontStyle.Bold, 15);

        int maxLevel = spellComp.GetHighestSlotLevel();
        for (int lvl = 0; lvl <= maxLevel; lvl++)
        {
            var slotsAtLevel = spellComp.GetSlotsForLevel(lvl);
            if (slotsAtLevel.Count == 0) continue;

            string levelLabel;
            string slotCountStr;

            if (lvl == 0)
            {
                // Cantrips are unlimited
                string cantripName = spellComp.Stats.IsCleric ? "Orisons" : "Cantrips";
                levelLabel = $"{cantripName} (Level 0)";
                slotCountStr = $"  (\u221e unlimited)";
            }
            else
            {
                var availableSlots = spellComp.GetAvailableSlotsForLevel(lvl);
                levelLabel = $"Level {lvl} Spells";
                slotCountStr = $"  ({availableSlots.Count}/{slotsAtLevel.Count} available)";
            }

            AddLine(content, levelLabel + slotCountStr, 11, GoldText, FontStyle.Bold, 16);

            // Show each individual slot
            for (int i = 0; i < slotsAtLevel.Count; i++)
            {
                var slot = slotsAtLevel[i];
                string status;
                Color slotColor;
                string spellName;

                if (slot.IsEmpty)
                {
                    status = "\u25cb"; // ○
                    spellName = "(empty)";
                    slotColor = DimText;
                }
                else if (lvl == 0)
                {
                    // Cantrips are unlimited — always show as available
                    status = "\u221e"; // ∞
                    spellName = slot.PreparedSpell.Name;
                    slotColor = new Color(0.5f, 0.9f, 0.5f); // green
                }
                else if (slot.IsUsed)
                {
                    status = "\u2717"; // ✗
                    spellName = slot.PreparedSpell.Name;
                    slotColor = new Color(0.5f, 0.3f, 0.3f); // dim red
                }
                else
                {
                    status = "\u2713"; // ✓
                    spellName = slot.PreparedSpell.Name;
                    slotColor = new Color(0.5f, 0.9f, 0.5f); // green
                }

                var slotGO = new GameObject($"Slot_{lvl}_{i}");
                slotGO.transform.SetParent(content, false);
                var sle = slotGO.AddComponent<LayoutElement>();
                sle.preferredHeight = 15;
                sle.minHeight = 15;
                slotGO.AddComponent<RectTransform>();

                string slotText = $"  {status} Slot {i + 1}: {spellName}";
                if (lvl > 0 && slot.IsUsed) slotText += " [USED]";

                MakeText(slotGO.transform, "SlotInfo", V2(0, 0), V2(0, 1), V2(0, 0.5f),
                    V2(10, 0), V2(300, 0), slotText, 11, slotColor, TextAnchor.MiddleLeft);

                if (slot.HasSpell && !slot.IsEmpty)
                {
                    MakeText(slotGO.transform, "School", V2(0, 0), V2(0, 1), V2(0, 0.5f),
                        V2(280, 0), V2(100, 0), slot.PreparedSpell.School ?? "", 10, DimText, TextAnchor.MiddleLeft);
                }
            }
        }

        AddSeparator(content);

        // --- SPELL LIST SECTION ---
        string listHeader = spellComp.Stats.IsWizard ? "SPELLBOOK" :
                            spellComp.Stats.IsCleric ? "KNOWN SPELLS (Full Cleric List)" : "KNOWN SPELLS";
        AddLine(content, listHeader, 11, new Color(0.6f, 0.7f, 0.9f), FontStyle.Bold, 15);

        var allSpells = spellComp.KnownSpells;
        if (allSpells == null || allSpells.Count == 0)
        {
            AddLine(content, "No spells known.", 12, DimText);
            return;
        }

        int maxKnownLevel = allSpells.Max(s => s.SpellLevel);
        for (int lvl = 0; lvl <= maxKnownLevel; lvl++)
        {
            var spellsAtLevel = allSpells.Where(s => s.SpellLevel == lvl).OrderBy(s => s.Name).ToList();
            if (spellsAtLevel.Count == 0) continue;

            string levelLabel = lvl == 0 ? (spellComp.Stats.IsCleric ? "Orisons" : "Cantrips") : $"Level {lvl}";
            AddLine(content, levelLabel, 11, new Color(0.6f, 0.7f, 0.9f), FontStyle.Normal, 14);

            foreach (var spell in spellsAtLevel)
            {
                int prepCount = spellComp.CountTotalPreparedSpell(spell);
                string prepStr = prepCount > 0 ? $" (\u00d7{prepCount} prepared)" : "";
                Color spellColor = prepCount > 0 ? LightText : DimText;

                var spellGO = new GameObject($"Book_{spell.SpellId}");
                spellGO.transform.SetParent(content, false);
                var sle = spellGO.AddComponent<LayoutElement>();
                sle.preferredHeight = 15;
                sle.minHeight = 15;
                spellGO.AddComponent<RectTransform>();

                MakeText(spellGO.transform, "Name", V2(0, 0), V2(0, 1), V2(0, 0.5f),
                    V2(10, 0), V2(200, 0), $"  \u2022 {spell.Name}{prepStr}", 11, spellColor, TextAnchor.MiddleLeft);

                MakeText(spellGO.transform, "School", V2(0, 0), V2(0, 1), V2(0, 0.5f),
                    V2(215, 0), V2(100, 0), spell.School ?? "", 10, DimText, TextAnchor.MiddleLeft);

                if (spell.IsPlaceholder)
                {
                    MakeText(spellGO.transform, "PH", V2(0, 0), V2(0, 1), V2(0, 0.5f),
                        V2(320, 0), V2(60, 0), "[PH]", 9, new Color(0.9f, 0.7f, 0.2f), TextAnchor.MiddleLeft);
                }
            }
        }
    }

    /// <summary>Backward compat alias.</summary>
    private void RefreshWizardSpellsTab(Transform content, SpellcastingComponent spellComp)
    {
        RefreshSlotSpellsTab(content, spellComp);
    }

    /// <summary>
    /// Refresh spells tab for non-wizard casters (Cleric, etc.) using standard spell list.
    /// </summary>
    private void RefreshStandardSpellsTab(Transform content, SpellcastingComponent spellComp)
    {
        var allSpells = spellComp.KnownSpells;
        if (allSpells == null || allSpells.Count == 0)
        {
            AddLine(content, "No spells known.", 12, DimText);
            return;
        }

        int maxLevel = allSpells.Max(s => s.SpellLevel);
        for (int lvl = 0; lvl <= maxLevel; lvl++)
        {
            var spellsAtLevel = allSpells.Where(s => s.SpellLevel == lvl).OrderBy(s => s.Name).ToList();
            if (spellsAtLevel.Count == 0) continue;

            string levelLabel = lvl == 0 ? "Cantrips (Level 0)" : $"Level {lvl} Spells";
            AddLine(content, levelLabel, 11, GoldText, FontStyle.Bold, 16);

            foreach (var spell in spellsAtLevel)
            {
                bool isPrepared = spellComp.PreparedSpells != null && spellComp.PreparedSpells.Contains(spell);
                bool canCast = spellComp.CanCastSpell(spell);

                string prepMark = isPrepared ? "\u2713 " : "\u25cb ";
                Color spellColor = canCast ? new Color(0.5f, 0.9f, 0.5f) :
                                   isPrepared ? LightText : DimText;

                var spellGO = new GameObject($"Spell_{spell.SpellId}");
                spellGO.transform.SetParent(content, false);
                var sle = spellGO.AddComponent<LayoutElement>();
                sle.preferredHeight = 15;
                sle.minHeight = 15;
                spellGO.AddComponent<RectTransform>();

                MakeText(spellGO.transform, "Name", V2(0, 0), V2(0, 1), V2(0, 0.5f),
                    V2(10, 0), V2(200, 0), $"{prepMark}{spell.Name}", 11, spellColor, TextAnchor.MiddleLeft);

                MakeText(spellGO.transform, "School", V2(0, 0), V2(0, 1), V2(0, 0.5f),
                    V2(215, 0), V2(100, 0), spell.School ?? "", 10, DimText, TextAnchor.MiddleLeft);

                if (spell.IsPlaceholder)
                {
                    MakeText(spellGO.transform, "PH", V2(0, 0), V2(0, 1), V2(0, 0.5f),
                        V2(320, 0), V2(60, 0), "[PH]", 9, new Color(0.9f, 0.7f, 0.2f), TextAnchor.MiddleLeft);
                }
            }
        }
    }


    // (Equipment panel removed — replaced by embedded InventoryUI)
    // ===================================================================
    //  HELPERS
    // ===================================================================

    private Transform GetTabContent(string tabName)
    {
        if (!_tabContents.ContainsKey(tabName)) return null;
        var outer = _tabContents[tabName];
        var sv = outer.transform.Find("ScrollView");
        if (sv == null) return null;
        var vp = sv.Find("Viewport");
        if (vp == null) return null;
        return vp.Find("Content");
    }

    private void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    private void AddLine(Transform parent, string text, int fontSize, Color color,
                         FontStyle style = FontStyle.Normal, float height = 18f)
    {
        var go = new GameObject("Line");
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;

        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.font = _uiFont;
        txt.fontSize = fontSize;
        txt.color = color;
        txt.fontStyle = style;
        txt.alignment = TextAnchor.MiddleLeft;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Truncate;
    }

    private void AddColoredLine(Transform parent, string prefix, Color prefixColor,
                                string text, Color textColor, int fontSize, float height)
    {
        // Unity UI Text doesn't support multiple colors easily, so use rich text
        var go = new GameObject("ColorLine");
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;

        var txt = go.AddComponent<Text>();
        string pHex = ColorUtility.ToHtmlStringRGB(prefixColor);
        string tHex = ColorUtility.ToHtmlStringRGB(textColor);
        txt.text = $"<color=#{pHex}>{prefix}</color><color=#{tHex}>{text}</color>";
        txt.font = _uiFont;
        txt.fontSize = fontSize;
        txt.color = Color.white;
        txt.supportRichText = true;
        txt.alignment = TextAnchor.MiddleLeft;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Truncate;
    }

    private void AddSeparator(Transform parent)
    {
        var go = new GameObject("Separator");
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 3;
        le.minHeight = 3;

        var img = go.AddComponent<Image>();
        img.color = SeparatorColor;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 1);
    }

    private string FormatMod(int mod) => mod >= 0 ? $"+{mod}" : mod.ToString();

    private Color GetClassColor(string className)
    {
        switch (className)
        {
            case "Fighter":   return new Color(0.6f, 0.35f, 0.2f);
            case "Rogue":     return new Color(0.3f, 0.5f, 0.3f);
            case "Cleric":    return new Color(0.7f, 0.65f, 0.3f);
            case "Wizard":    return new Color(0.3f, 0.3f, 0.65f);
            case "Monk":      return new Color(0.5f, 0.4f, 0.3f);
            case "Barbarian": return new Color(0.55f, 0.25f, 0.25f);
            default:          return new Color(0.35f, 0.35f, 0.4f);
        }
    }

    // ===================================================================
    //  UI FACTORY HELPERS
    // ===================================================================

    private GameObject MakePanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta, Color bgColor)
    {
        var go = new GameObject(name);
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
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta,
        string text, int fontSize, Color color, TextAnchor alignment,
        FontStyle style = FontStyle.Normal)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        var t = go.AddComponent<Text>();
        t.text = text;
        t.font = _uiFont;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = alignment;
        t.fontStyle = style;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    // Short-hand for Vector2 in expressions
    private static Vector2 V2(float x, float y) => new Vector2(x, y);
}