using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI Panel for displaying character skills and allocating skill points.
/// Supports two modes:
///   1. Allocation Mode: During character creation, allows spending skill points.
///   2. Display Mode: During gameplay, shows skills and allows rolling skill checks.
///
/// Layout: Scrollable list of all skills with ranks, bonuses, and +/- buttons.
/// Uses CanvasGroup to properly control raycast blocking when hidden.
/// </summary>
public class SkillsUIPanel : MonoBehaviour
{
    // ========== STATE ==========
    private CharacterStats _stats;
    private bool _allocationMode; // true = spending points, false = display only
    private Font _font;

    // Callback when allocation is confirmed
    public System.Action OnAllocationConfirmed;

    // ========== UI REFERENCES ==========
    private GameObject _rootPanel;
    private GameObject _overlay;
    private CanvasGroup _canvasGroup; // Controls raycast blocking
    private Text _titleText;
    private Text _skillPointsText;
    private GameObject _scrollContent;
    private Button _confirmButton;
    private Button _closeButton;
    private Text _logText;

    // Per-skill UI rows
    private List<SkillRowUI> _skillRows = new List<SkillRowUI>();

    // Panel dimensions
    private const float PANEL_W = 820f;
    private const float PANEL_H = 680f;
    private const float ROW_HEIGHT = 30f;
    private const float SCROLL_AREA_HEIGHT = 460f;

    /// <summary>Whether this panel is currently open/visible.</summary>
    /// <remarks>Uses activeInHierarchy to correctly detect when parent overlay is disabled.</remarks>
    public bool IsOpen => _overlay != null && _overlay.activeInHierarchy;

    // ========== BUILD UI ==========

    /// <summary>
    /// Build the skills panel UI under the given canvas.
    /// Must be called once before Open().
    /// </summary>
    public void BuildUI(Canvas canvas)
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // Overlay
        _overlay = CreatePanel(canvas.transform, "SkillsOverlay",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.75f));
        RectTransform overlayRT = _overlay.GetComponent<RectTransform>();
        overlayRT.offsetMin = Vector2.zero;
        overlayRT.offsetMax = Vector2.zero;

        // Add CanvasGroup so we can control raycast blocking when hidden
        _canvasGroup = _overlay.AddComponent<CanvasGroup>();

        // Main panel
        _rootPanel = CreatePanel(_overlay.transform, "SkillsPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(PANEL_W, PANEL_H), new Color(0.1f, 0.1f, 0.16f, 0.98f));

        // Title
        _titleText = MakeText(_rootPanel.transform, "SkillsTitle",
            new Vector2(0, PANEL_H / 2 - 25), new Vector2(PANEL_W - 40, 35),
            "SKILLS", 24, Color.white, TextAnchor.MiddleCenter);

        // Skill points display
        _skillPointsText = MakeText(_rootPanel.transform, "SkillPoints",
            new Vector2(0, PANEL_H / 2 - 55), new Vector2(PANEL_W - 40, 25),
            "Skill Points: 0 / 0", 16, new Color(0.9f, 0.85f, 0.4f), TextAnchor.MiddleCenter);

        // Legend for class/cross-class
        float legendY = PANEL_H / 2 - 80;
        MakeText(_rootPanel.transform, "SkillLegend",
            new Vector2(0, legendY), new Vector2(PANEL_W - 40, 18),
            "<color=#E6D966>★ Class Skill (Cost: 1 pt/rank, Max: Lv+3)</color>  |  <color=#AAAAAA>○ Cross-Class (Cost: 2 pts/rank, Max: (Lv+3)/2)</color>",
            11, new Color(0.7f, 0.7f, 0.7f), TextAnchor.MiddleCenter);

        // Column headers
        float headerY = PANEL_H / 2 - 100;
        MakeText(_rootPanel.transform, "HeaderName",
            new Vector2(-260, headerY), new Vector2(220, 20),
            "Skill (Ability)", 13, new Color(0.7f, 0.7f, 0.9f), TextAnchor.MiddleLeft);
        MakeText(_rootPanel.transform, "HeaderCost",
            new Vector2(-100, headerY), new Vector2(50, 20),
            "Cost", 13, new Color(0.7f, 0.7f, 0.9f), TextAnchor.MiddleCenter);
        MakeText(_rootPanel.transform, "HeaderRanks",
            new Vector2(-30, headerY), new Vector2(60, 20),
            "Ranks", 13, new Color(0.7f, 0.7f, 0.9f), TextAnchor.MiddleCenter);
        MakeText(_rootPanel.transform, "HeaderBonus",
            new Vector2(40, headerY), new Vector2(60, 20),
            "Total", 13, new Color(0.7f, 0.7f, 0.9f), TextAnchor.MiddleCenter);
        MakeText(_rootPanel.transform, "HeaderBreakdown",
            new Vector2(210, headerY), new Vector2(240, 20),
            "Breakdown", 13, new Color(0.7f, 0.7f, 0.9f), TextAnchor.MiddleCenter);

        // Scroll area container (clip mask)
        GameObject scrollArea = CreatePanel(_rootPanel.transform, "SkillsScrollArea",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, -55), new Vector2(PANEL_W - 30, SCROLL_AREA_HEIGHT),
            new Color(0.08f, 0.08f, 0.12f, 0.9f));

        // Add mask for scrolling
        Mask mask = scrollArea.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        // Viewport for scrolling (child of scroll area, with mask for clipping)
        GameObject scrollView = new GameObject("Viewport");
        scrollView.transform.SetParent(scrollArea.transform, false);
        RectTransform svRT = scrollView.AddComponent<RectTransform>();
        svRT.anchorMin = Vector2.zero;
        svRT.anchorMax = Vector2.one;
        svRT.offsetMin = Vector2.zero;
        svRT.offsetMax = Vector2.zero;
        scrollView.AddComponent<Image>().color = Color.white;
        scrollView.AddComponent<Mask>().showMaskGraphic = false;

        // Scroll content
        _scrollContent = new GameObject("ScrollContent");
        _scrollContent.transform.SetParent(scrollView.transform, false);
        RectTransform contentRT = _scrollContent.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.anchoredPosition = Vector2.zero;
        // Height will be set when skills are populated

        // ScrollRect component
        ScrollRect scrollRect = scrollArea.AddComponent<ScrollRect>();
        scrollRect.content = contentRT;
        scrollRect.viewport = svRT;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        // Vertical scrollbar for the skills list
        ScrollbarHelper.CreateVerticalScrollbar(scrollRect, scrollArea.transform);

        // Log text (for skill check results)
        _logText = MakeText(_rootPanel.transform, "SkillLog",
            new Vector2(0, -PANEL_H / 2 + 75), new Vector2(PANEL_W - 60, 40),
            "", 13, new Color(0.6f, 0.9f, 0.6f), TextAnchor.MiddleCenter);

        // Close button
        _closeButton = MakeButton(_rootPanel.transform, "CloseBtn",
            new Vector2(PANEL_W / 2 - 70, -PANEL_H / 2 + 30), new Vector2(100, 36),
            "Close", new Color(0.5f, 0.2f, 0.2f), Color.white, 16);
        _closeButton.onClick.AddListener(Close);

        // Confirm button (only shown in allocation mode)
        _confirmButton = MakeButton(_rootPanel.transform, "ConfirmBtn",
            new Vector2(-PANEL_W / 2 + 120, -PANEL_H / 2 + 30), new Vector2(180, 36),
            "Confirm Skills ✓", new Color(0.2f, 0.6f, 0.2f), Color.white, 16);
        _confirmButton.onClick.AddListener(OnConfirmClicked);

        // Start hidden — ensure both GameObject and CanvasGroup are properly disabled
        SetPanelVisible(false);
    }

    // ========== OPEN / CLOSE ==========

    /// <summary>
    /// Open the skills panel in allocation mode (for character creation).
    /// </summary>
    public void OpenForAllocation(CharacterStats stats)
    {
        _stats = stats;
        _allocationMode = true;
        _titleText.text = $"SKILL ALLOCATION - {stats.CharacterName}";
        _confirmButton.gameObject.SetActive(true);
        _closeButton.gameObject.SetActive(false); // Can't close during allocation
        _logText.text = "★ = Class skill (1 pt/rank)  |  ○ = Cross-class (2 pts/rank)";
        PopulateSkillRows();
        RefreshAllRows();
        SetPanelVisible(true);
        Debug.Log("[UI] Skills panel opened (allocation mode) - blocking raycasts");
    }

    /// <summary>
    /// Open the skills panel in display mode (during gameplay).
    /// </summary>
    public void OpenForDisplay(CharacterStats stats)
    {
        _stats = stats;
        _allocationMode = false;
        _titleText.text = $"SKILLS - {stats.CharacterName}";
        _confirmButton.gameObject.SetActive(false);
        _closeButton.gameObject.SetActive(true);
        _logText.text = "Click a skill name to roll a skill check.";
        PopulateSkillRows();
        RefreshAllRows();
        SetPanelVisible(true);
        Debug.Log("[UI] Skills panel opened (display mode) - blocking raycasts");
    }

    /// <summary>Close the skills panel.</summary>
    public void Close()
    {
        SetPanelVisible(false);
        Debug.Log("[UI] Skills panel closed - allowing raycasts");
    }

    /// <summary>
    /// Show or hide the panel, ensuring CanvasGroup properly blocks/allows raycasts.
    /// </summary>
    private void SetPanelVisible(bool visible)
    {
        if (_overlay != null)
            _overlay.SetActive(visible);

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }
    }

    // ========== POPULATE SKILL ROWS ==========

    private void PopulateSkillRows()
    {
        // Clear existing rows
        foreach (var row in _skillRows)
        {
            if (row.RowObject != null)
                Destroy(row.RowObject);
        }
        _skillRows.Clear();

        if (_stats == null || _stats.Skills.Count == 0) return;

        // Sort: class skills first, then alphabetical
        var sortedSkills = new List<Skill>();
        foreach (var kvp in _stats.Skills)
            sortedSkills.Add(kvp.Value);
        sortedSkills.Sort((a, b) =>
        {
            if (a.IsClassSkill != b.IsClassSkill)
                return a.IsClassSkill ? -1 : 1;
            return a.SkillName.CompareTo(b.SkillName);
        });

        float totalHeight = sortedSkills.Count * ROW_HEIGHT + 10;
        RectTransform contentRT = _scrollContent.GetComponent<RectTransform>();
        contentRT.sizeDelta = new Vector2(0, totalHeight);

        for (int i = 0; i < sortedSkills.Count; i++)
        {
            CreateSkillRow(sortedSkills[i], i);
        }
    }

    private void CreateSkillRow(Skill skill, int index)
    {
        float y = -(index * ROW_HEIGHT + 5);
        bool isEven = index % 2 == 0;

        // Row background — class skills get a subtle gold tint, cross-class get default
        Color rowBg;
        if (skill.IsClassSkill)
            rowBg = isEven ? new Color(0.16f, 0.15f, 0.10f, 0.6f) : new Color(0.18f, 0.17f, 0.12f, 0.6f);
        else
            rowBg = isEven ? new Color(0.12f, 0.12f, 0.18f, 0.6f) : new Color(0.15f, 0.15f, 0.22f, 0.6f);

        GameObject rowGO = CreatePanel(_scrollContent.transform, $"SkillRow_{skill.SkillName}",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, y), new Vector2(0, ROW_HEIGHT),
            rowBg);
        RectTransform rowRT = rowGO.GetComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0, 1);
        rowRT.anchorMax = new Vector2(1, 1);
        rowRT.offsetMin = new Vector2(5, 0);
        rowRT.offsetMax = new Vector2(-5, 0);
        rowRT.anchoredPosition = new Vector2(0, y);
        rowRT.sizeDelta = new Vector2(-10, ROW_HEIGHT);

        SkillRowUI row = new SkillRowUI();
        row.RowObject = rowGO;
        row.Skill = skill;

        // Skill name with class/cross-class icon indicator
        string classIcon = skill.IsClassSkill ? "★" : "○";
        string trainedMark = skill.TrainedOnly ? "†" : "";
        Color nameColor = skill.IsClassSkill ? new Color(0.9f, 0.85f, 0.4f) : new Color(0.75f, 0.75f, 0.75f);

        Button nameBtn = MakeButton(rowGO.transform, "Name",
            new Vector2(-150, 0), new Vector2(240, ROW_HEIGHT - 2),
            $"{classIcon} {skill.SkillName}{trainedMark} ({skill.KeyAbility})",
            new Color(0, 0, 0, 0), nameColor, 12);
        // Left-align the name text
        Text nameText = nameBtn.GetComponentInChildren<Text>();
        if (nameText != null) nameText.alignment = TextAnchor.MiddleLeft;
        row.NameButton = nameBtn;

        nameBtn.onClick.AddListener(() => OnSkillNameClicked(skill));

        // Cost display — shows how many skill points per rank
        Color costColor = skill.IsClassSkill ? new Color(0.5f, 0.9f, 0.5f) : new Color(0.9f, 0.6f, 0.3f);
        row.CostText = MakeText(rowGO.transform, "Cost",
            new Vector2(-100, 0), new Vector2(50, ROW_HEIGHT),
            skill.SkillPointCost.ToString(), 13, costColor, TextAnchor.MiddleCenter);

        // Ranks display
        row.RanksText = MakeText(rowGO.transform, "Ranks",
            new Vector2(-30, 0), new Vector2(60, ROW_HEIGHT),
            "0", 14, Color.white, TextAnchor.MiddleCenter);

        // Total bonus display
        row.BonusText = MakeText(rowGO.transform, "Bonus",
            new Vector2(40, 0), new Vector2(50, ROW_HEIGHT),
            "+0", 14, new Color(0.5f, 1f, 0.5f), TextAnchor.MiddleCenter);

        // Breakdown text
        row.BreakdownText = MakeText(rowGO.transform, "Breakdown",
            new Vector2(210, 0), new Vector2(240, ROW_HEIGHT),
            "", 11, new Color(0.6f, 0.6f, 0.6f), TextAnchor.MiddleLeft);

        // +/- buttons (only in allocation mode)
        if (_allocationMode)
        {
            row.AddButton = MakeButton(rowGO.transform, "AddRank",
                new Vector2(90, 0), new Vector2(26, 24),
                "+", new Color(0.2f, 0.5f, 0.2f), Color.white, 14);
            row.AddButton.onClick.AddListener(() => OnAddRankClicked(skill));

            row.RemoveButton = MakeButton(rowGO.transform, "RemoveRank",
                new Vector2(65, 0), new Vector2(26, 24),
                "-", new Color(0.5f, 0.2f, 0.2f), Color.white, 14);
            row.RemoveButton.onClick.AddListener(() => OnRemoveRankClicked(skill));
        }

        _skillRows.Add(row);
    }

    // ========== REFRESH DISPLAY ==========

    private void RefreshAllRows()
    {
        if (_stats == null) return;

        // Update skill points display
        int spent = _stats.TotalSkillPoints - _stats.AvailableSkillPoints;
        _skillPointsText.text = $"Skill Points: {_stats.AvailableSkillPoints} remaining  ({spent} / {_stats.TotalSkillPoints} spent)";

        if (_stats.AvailableSkillPoints > 0)
            _skillPointsText.color = new Color(0.9f, 0.85f, 0.4f);
        else
            _skillPointsText.color = new Color(0.5f, 0.9f, 0.5f);

        foreach (var row in _skillRows)
        {
            RefreshRow(row);
        }

        // Update confirm button interactability (allow confirming even with unspent points)
        if (_confirmButton != null && _allocationMode)
        {
            _confirmButton.interactable = true;
        }
    }

    private void RefreshRow(SkillRowUI row)
    {
        if (row.Skill == null || _stats == null) return;

        Skill skill = row.Skill;
        int abilityMod = _stats.GetAbilityModForSkill(skill);
        int totalBonus = skill.GetTotalBonus(abilityMod);
        int maxRanks = skill.GetMaxRanks(_stats.Level);
        int cost = skill.SkillPointCost;

        // Ranks — show current/max
        row.RanksText.text = $"{skill.Ranks}/{maxRanks}";
        row.RanksText.color = skill.Ranks > 0 ? Color.white : new Color(0.4f, 0.4f, 0.4f);
        if (skill.Ranks >= maxRanks)
            row.RanksText.color = new Color(1f, 0.6f, 0.3f); // Orange when maxed

        // Total bonus
        string bonusStr = totalBonus >= 0 ? $"+{totalBonus}" : $"{totalBonus}";
        row.BonusText.text = bonusStr;

        if (skill.TrainedOnly && skill.Ranks == 0)
            row.BonusText.color = new Color(0.5f, 0.3f, 0.3f);
        else if (totalBonus > 0)
            row.BonusText.color = new Color(0.5f, 1f, 0.5f);
        else
            row.BonusText.color = new Color(0.8f, 0.8f, 0.8f);

        // Breakdown
        string breakdown = $"{skill.Ranks}r";
        string modStr = abilityMod >= 0 ? $"+{abilityMod}" : $"{abilityMod}";
        breakdown += $" {modStr}{skill.KeyAbility}";
        if (skill.ClassSkillBonus > 0)
            breakdown += $" +3cls";
        if (skill.TrainedOnly && skill.Ranks == 0)
            breakdown += " [need training]";
        if (!skill.IsClassSkill)
            breakdown += " (×2 cost)";

        row.BreakdownText.text = breakdown;

        // +/- button states — check cost against available points
        if (row.AddButton != null)
        {
            bool canAdd = _stats.AvailableSkillPoints >= cost && skill.Ranks < maxRanks;
            row.AddButton.interactable = canAdd;
        }
        if (row.RemoveButton != null)
        {
            row.RemoveButton.interactable = skill.Ranks > 0;
        }
    }

    // ========== BUTTON HANDLERS ==========

    private void OnAddRankClicked(Skill skill)
    {
        if (_stats == null) return;
        if (_stats.AddSkillRank(skill.SkillName))
        {
            RefreshAllRows();
        }
    }

    private void OnRemoveRankClicked(Skill skill)
    {
        if (_stats == null) return;
        if (_stats.RemoveSkillRank(skill.SkillName))
        {
            RefreshAllRows();
        }
    }

    private void OnSkillNameClicked(Skill skill)
    {
        if (_allocationMode || _stats == null) return;

        // Roll a skill check
        int result = _stats.RollSkillCheck(skill.SkillName);
        if (result < 0)
        {
            _logText.text = $"<color=#FF6666>Cannot use {skill.SkillName} — requires training!</color>";
        }
        else
        {
            int abilityMod = _stats.GetAbilityModForSkill(skill);
            string classStr = skill.ClassSkillBonus > 0 ? $" + {skill.ClassSkillBonus}(class)" : "";
            _logText.text = $"<color=#88FF88>{skill.SkillName} check = {result}</color>  " +
                           $"(ranks:{skill.Ranks} + {skill.KeyAbility}:{abilityMod}{classStr})";
        }
    }

    private void OnConfirmClicked()
    {
        if (_stats != null && _stats.AvailableSkillPoints > 0)
        {
            Debug.Log($"[Skills] {_stats.CharacterName} confirmed skills with {_stats.AvailableSkillPoints} unspent points.");
        }
        else
        {
            Debug.Log($"[Skills] {_stats.CharacterName} confirmed skill allocation (all points spent).");
        }

        // Log final skill summary
        foreach (var kvp in _stats.Skills)
        {
            Skill s = kvp.Value;
            if (s.Ranks > 0)
            {
                int mod = _stats.GetAbilityModForSkill(s);
                Debug.Log($"[Skills]   {s.SkillName}: {s.Ranks} ranks, total bonus {CharacterStats.FormatMod(s.GetTotalBonus(mod))}");
            }
        }

        Close();
        OnAllocationConfirmed?.Invoke();
    }

    // ========== UI HELPER METHODS ==========
    // (Same pattern as CharacterCreationUI)

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

        MakeText(go.transform, name + "Label", Vector2.zero, size,
            label, fontSize, textColor, TextAnchor.MiddleCenter);

        return btn;
    }

    private GameObject CreatePanel(Transform parent, string name,
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

    // ========== SKILL ROW DATA ==========

    private class SkillRowUI
    {
        public GameObject RowObject;
        public Skill Skill;
        public Button NameButton;
        public Text CostText;
        public Text RanksText;
        public Text BonusText;
        public Text BreakdownText;
        public Button AddButton;
        public Button RemoveButton;
    }
}
