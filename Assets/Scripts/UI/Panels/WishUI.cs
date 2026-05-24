// ============================================================================
// WishUI.cs — Wish spell option selection panel.
//
// Programmatic Unity UI panel allowing the player to choose one of the 10
// standard Wish options (PHB p.302). Sub-panels handle target / ability /
// affliction / spell selection depending on the chosen option.
//
// Follows the same build pattern as QuickItemUsePanel: all UI elements are
// created via code (no prefabs).
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using DND35e.Identifiers;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Wish option selection UI. Opened by GameManager when a player casts Wish.
/// After the player picks an option (and any sub-selections), the panel invokes
/// <see cref="OnWishConfirmed"/> with all parameters needed by WishExecutor.
/// </summary>
public class WishUI : MonoBehaviour
{
    // ========== CALLBACKS ==========

    /// <summary>
    /// Fired when the player confirms a Wish option.
    /// Parameters: (WishOption option, CharacterController target, AbilityType ability,
    ///              WishAfflictionType affliction, string duplicateSpellId)
    /// </summary>
    public Action<WishOption, CharacterController, AbilityType, WishAfflictionType, string> OnWishConfirmed;

    /// <summary>Fired when the player cancels the Wish panel.</summary>
    public Action OnCancelled;

    // ========== STATE ==========

    public bool IsOpen { get; private set; }

    private CharacterController _caster;
    private bool _isItemWish;

    // Current selection state
    private WishOption? _selectedOption;
    private CharacterController _selectedTarget;
    private AbilityType _selectedAbility;
    private WishAfflictionType _selectedAffliction;
    private string _selectedSpellId;

    // ========== UI REFERENCES ==========

    private Font _font;
    private GameObject _overlayPanel;
    private GameObject _rootPanel;
    private Text _titleText;
    private Text _xpCostText;

    // Main option list (scroll area)
    private GameObject _scrollContent;
    private RectTransform _scrollContentRT;

    // Sub-panel for secondary selection
    private GameObject _subPanel;
    private GameObject _subScrollContent;
    private RectTransform _subScrollContentRT;
    private Text _subPanelTitle;

    // Buttons
    private Button _cancelBtn;

    // Generated option rows
    private List<GameObject> _optionRows = new List<GameObject>();
    private List<GameObject> _subRows = new List<GameObject>();

    // Layout constants
    private const float PANEL_W = 620f;
    private const float PANEL_H = 600f;
    private const float ROW_H = 60f;
    private const float ROW_SPACING = 4f;
    private const float SUB_ROW_H = 36f;
    private const float SUB_ROW_SPACING = 3f;

    // ========== BUILD UI ==========

    public void BuildUI(Canvas canvas)
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // Dark overlay
        _overlayPanel = MakePanel(canvas.transform, "WishOverlay",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.75f));
        var overlayRT = _overlayPanel.GetComponent<RectTransform>();
        overlayRT.offsetMin = Vector2.zero;
        overlayRT.offsetMax = Vector2.zero;

        // Main panel centered
        _rootPanel = MakePanel(_overlayPanel.transform, "WishPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(PANEL_W, PANEL_H), new Color(0.10f, 0.08f, 0.18f, 0.97f));

        float halfW = PANEL_W / 2f;
        float halfH = PANEL_H / 2f;
        float y = halfH;

        // Title
        y -= 28;
        _titleText = MakeText(_rootPanel.transform, "Title",
            new Vector2(0, y), new Vector2(PANEL_W - 20, 34),
            "✨ WISH ✨", 22, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);
        _titleText.fontStyle = FontStyle.Bold;

        // XP cost label
        y -= 22;
        _xpCostText = MakeText(_rootPanel.transform, "XPCost",
            new Vector2(0, y), new Vector2(PANEL_W - 20, 20),
            "", 13, new Color(0.8f, 0.6f, 0.6f), TextAnchor.MiddleCenter);

        // Main scroll area for options
        y -= 14;
        float scrollTop = y;
        float scrollBottom = -halfH + 50; // room for cancel button
        float scrollH = scrollTop - scrollBottom;

        GameObject scrollArea = MakePanel(_rootPanel.transform, "OptionScrollArea",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, (scrollTop + scrollBottom) / 2f), new Vector2(PANEL_W - 24, scrollH),
            new Color(0.06f, 0.06f, 0.10f, 0.9f));

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

        // Sub-panel (hidden by default, overlays the main scroll)
        _subPanel = MakePanel(_rootPanel.transform, "SubPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 0), new Vector2(PANEL_W - 40, PANEL_H - 100),
            new Color(0.12f, 0.10f, 0.20f, 0.98f));

        _subPanelTitle = MakeText(_subPanel.transform, "SubTitle",
            new Vector2(0, (PANEL_H - 100) / 2f - 20), new Vector2(PANEL_W - 80, 28),
            "", 16, new Color(1f, 0.9f, 0.6f), TextAnchor.MiddleCenter);
        _subPanelTitle.fontStyle = FontStyle.Bold;

        // Sub scroll area
        float subScrollH = PANEL_H - 100 - 90; // room for title + back button
        GameObject subScrollArea = MakePanel(_subPanel.transform, "SubScrollArea",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, -10), new Vector2(PANEL_W - 60, subScrollH),
            new Color(0.06f, 0.06f, 0.10f, 0.9f));

        GameObject subViewport = new GameObject("SubViewport");
        subViewport.transform.SetParent(subScrollArea.transform, false);
        var subVpRT = subViewport.AddComponent<RectTransform>();
        subVpRT.anchorMin = Vector2.zero;
        subVpRT.anchorMax = Vector2.one;
        subVpRT.offsetMin = new Vector2(4, 4);
        subVpRT.offsetMax = new Vector2(-4, -4);
        subViewport.AddComponent<Image>().color = Color.white;
        subViewport.AddComponent<Mask>().showMaskGraphic = false;

        _subScrollContent = new GameObject("SubContent");
        _subScrollContent.transform.SetParent(subViewport.transform, false);
        _subScrollContentRT = _subScrollContent.AddComponent<RectTransform>();
        _subScrollContentRT.anchorMin = new Vector2(0, 1);
        _subScrollContentRT.anchorMax = new Vector2(1, 1);
        _subScrollContentRT.pivot = new Vector2(0.5f, 1);
        _subScrollContentRT.anchoredPosition = Vector2.zero;
        _subScrollContentRT.sizeDelta = new Vector2(0, 0);

        var subScrollRect = subScrollArea.AddComponent<ScrollRect>();
        subScrollRect.content = _subScrollContentRT;
        subScrollRect.viewport = subVpRT;
        subScrollRect.vertical = true;
        subScrollRect.horizontal = false;
        subScrollRect.scrollSensitivity = 30f;

        ScrollbarHelper.CreateVerticalScrollbar(subScrollRect, subScrollArea.transform);

        // Back button in sub-panel
        Button backBtn = MakeButton(_subPanel.transform, "BackBtn",
            new Vector2(0, -(PANEL_H - 100) / 2f + 22), new Vector2(120, 30),
            "◀ Back", new Color(0.35f, 0.35f, 0.45f), Color.white, 13);
        backBtn.onClick.AddListener(HideSubPanel);

        _subPanel.SetActive(false);

        // Cancel button
        _cancelBtn = MakeButton(_rootPanel.transform, "CancelBtn",
            new Vector2(0, -halfH + 22), new Vector2(140, 34),
            "Cancel Wish", new Color(0.55f, 0.2f, 0.2f), Color.white, 14);
        _cancelBtn.onClick.AddListener(Close);

        _overlayPanel.SetActive(false);
    }

    // ========== OPEN / CLOSE ==========

    /// <summary>
    /// Open the Wish selection panel for a given caster.
    /// </summary>
    /// <param name="caster">The character casting Wish.</param>
    /// <param name="isItemWish">True if the Wish comes from a magic item (no XP cost).</param>
    public void Open(CharacterController caster, bool isItemWish = false)
    {
        if (caster == null || caster.Stats == null) return;

        _caster = caster;
        _isItemWish = isItemWish;
        _selectedOption = null;
        _selectedTarget = null;
        _selectedAbility = AbilityType.STR;
        _selectedAffliction = WishAfflictionType.Damage;
        _selectedSpellId = null;

        _titleText.text = $"✨ WISH — {caster.Stats.CharacterName} ✨";
        _xpCostText.text = isItemWish
            ? "<color=#66FF66>No XP cost (cast from magic item)</color>"
            : $"<color=#FF8888>Most options cost 5,000 XP (you have {caster.Stats.ExperiencePoints:N0} XP)</color>";

        BuildOptionList();
        _subPanel.SetActive(false);
        _overlayPanel.SetActive(true);
        IsOpen = true;
    }

    public void Close()
    {
        _overlayPanel.SetActive(false);
        IsOpen = false;
        _caster = null;
        OnCancelled?.Invoke();
    }

    // ========== BUILD OPTION LIST ==========

    private void BuildOptionList()
    {
        // Clear old rows
        foreach (var row in _optionRows)
        {
            if (row != null) Destroy(row);
        }
        _optionRows.Clear();

        float y = 0;
        var options = (WishOption[])Enum.GetValues(typeof(WishOption));

        foreach (var option in options)
        {
            var info = WishExecutor.GetOptionInfo(option);
            bool costsXP = info.costsXP && !_isItemWish;
            bool canAfford = !costsXP || _caster.Stats.ExperiencePoints >= 5000;

            GameObject row = MakePanel(_scrollContent.transform, $"Option_{option}",
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, y), new Vector2(0, ROW_H),
                canAfford ? new Color(0.14f, 0.14f, 0.22f) : new Color(0.20f, 0.12f, 0.12f));
            var rowRT = row.GetComponent<RectTransform>();
            rowRT.anchorMin = new Vector2(0.02f, 1);
            rowRT.anchorMax = new Vector2(0.98f, 1);
            rowRT.anchoredPosition = new Vector2(0, y - ROW_H / 2f);
            rowRT.sizeDelta = new Vector2(0, ROW_H);

            // Title text
            MakeText(row.transform, "Title",
                new Vector2(10, 10), new Vector2(500, 22),
                info.title + (costsXP ? "  <color=#FF6666>(−5,000 XP)</color>" : "  <color=#66FF66>(free)</color>"),
                14, new Color(1f, 0.9f, 0.6f), TextAnchor.MiddleLeft);

            // Description
            MakeText(row.transform, "Desc",
                new Vector2(10, -10), new Vector2(500, 20),
                info.description, 11, new Color(0.7f, 0.7f, 0.8f), TextAnchor.MiddleLeft);

            if (canAfford)
            {
                // Make entire row clickable
                var btn = row.AddComponent<Button>();
                var nav = btn.navigation;
                nav.mode = Navigation.Mode.None;
                btn.navigation = nav;

                WishOption capturedOption = option;
                btn.onClick.AddListener(() => OnOptionClicked(capturedOption));
            }

            _optionRows.Add(row);
            y -= (ROW_H + ROW_SPACING);
        }

        _scrollContentRT.sizeDelta = new Vector2(0, Mathf.Abs(y));
    }

    // ========== OPTION CLICK ==========

    private void OnOptionClicked(WishOption option)
    {
        _selectedOption = option;

        switch (option)
        {
            case WishOption.DuplicateSpell:
                ShowSpellSelectionSubPanel();
                break;

            case WishOption.UndoHarmfulEffects:
                ShowAllySelectionSubPanel("Select ally to remove harmful effects from:");
                break;

            case WishOption.GrantInherentBonus:
                ShowAbilitySelectionSubPanel();
                break;

            case WishOption.RemoveAfflictions:
                ShowAfflictionSelectionSubPanel();
                break;

            case WishOption.ReviveDead:
                ShowDeadAllySelectionSubPanel();
                break;

            case WishOption.UndoRecentEvent:
                // Immediate — narrative power, no sub-selection needed
                ConfirmWish(option, null, AbilityType.STR, WishAfflictionType.Damage, null);
                break;

            case WishOption.CreateNonmagicalItem:
            case WishOption.CreateMagicItem:
                // Placeholder — confirm immediately with a log
                ConfirmWish(option, null, AbilityType.STR, WishAfflictionType.Damage, null);
                break;

            case WishOption.TransportAllies:
            case WishOption.OtherEffect:
                // DM-arbitrated effects — confirm with log
                ConfirmWish(option, null, AbilityType.STR, WishAfflictionType.Damage, null);
                break;
        }
    }

    // ========== SUB-PANELS ==========

    private void ShowSubPanel(string title)
    {
        ClearSubRows();
        _subPanelTitle.text = title;
        _subPanel.SetActive(true);
    }

    private void HideSubPanel()
    {
        _subPanel.SetActive(false);
        _selectedOption = null;
    }

    private void ClearSubRows()
    {
        foreach (var row in _subRows)
        {
            if (row != null) Destroy(row);
        }
        _subRows.Clear();
    }

    // --- Ally selection ---
    private void ShowAllySelectionSubPanel(string title)
    {
        ShowSubPanel(title);
        float y = 0;

        var allies = GetLivingAllies();
        foreach (var ally in allies)
        {
            AddSubButton(ally.Stats.CharacterName,
                $"HP: {ally.Stats.CurrentHP}/{ally.Stats.TotalMaxHP}",
                ref y, () =>
                {
                    _selectedTarget = ally;
                    ConfirmWish(_selectedOption.Value, ally, AbilityType.STR, WishAfflictionType.Damage, null);
                });
        }

        _subScrollContentRT.sizeDelta = new Vector2(0, Mathf.Abs(y));
    }

    // --- Dead ally selection ---
    private void ShowDeadAllySelectionSubPanel()
    {
        ShowSubPanel("Select fallen ally to revive:");
        float y = 0;

        var allChars = GameManager.Instance?.GetAllCharactersForAI();
        if (allChars != null)
        {
            foreach (var ch in allChars)
            {
                if (ch == null || ch.Stats == null) continue;
                if (!ch.Stats.IsPlayerControlled) continue;
                if (!ch.HasCondition(CombatConditionType.Dead)) continue;

                AddSubButton(ch.Stats.CharacterName,
                    "DEAD — click to revive",
                    ref y, () =>
                    {
                        _selectedTarget = ch;
                        ConfirmWish(WishOption.ReviveDead, ch, AbilityType.STR, WishAfflictionType.Damage, null);
                    });
            }
        }

        if (Mathf.Approximately(y, 0))
        {
            AddSubLabel("No dead allies to revive.", ref y);
        }

        _subScrollContentRT.sizeDelta = new Vector2(0, Mathf.Abs(y));
    }

    // --- Ability selection (for inherent bonus) ---
    private void ShowAbilitySelectionSubPanel()
    {
        // First select target ally, then ability
        ShowSubPanel("Select ally for inherent ability bonus:");
        float y = 0;

        var allies = GetLivingAllies();
        foreach (var ally in allies)
        {
            var captured = ally;
            string bonusInfo = GetInherentBonusInfo(ally);
            AddSubButton(ally.Stats.CharacterName, bonusInfo, ref y, () =>
            {
                _selectedTarget = captured;
                ShowAbilityPickerForTarget(captured);
            });
        }

        _subScrollContentRT.sizeDelta = new Vector2(0, Mathf.Abs(y));
    }

    private void ShowAbilityPickerForTarget(CharacterController target)
    {
        ShowSubPanel($"Select ability to boost for {target.Stats.CharacterName}:");
        float y = 0;

        var abilities = new[] { AbilityType.STR, AbilityType.DEX, AbilityType.CON,
                                AbilityType.WIS, AbilityType.INT, AbilityType.CHA };

        foreach (var ability in abilities)
        {
            int current = target.Stats.GetInherentBonus(ability);
            bool capped = current >= 5;
            string label = $"{ability} (current inherent: +{current}{(capped ? " MAX" : "")})";

            if (!capped)
            {
                AbilityType capturedAbility = ability;
                AddSubButton(label, $"Grant +1 inherent bonus (→ +{current + 1})", ref y, () =>
                {
                    ConfirmWish(WishOption.GrantInherentBonus, target, capturedAbility, WishAfflictionType.Damage, null);
                });
            }
            else
            {
                AddSubLabel(label + " — already at maximum", ref y);
            }
        }

        _subScrollContentRT.sizeDelta = new Vector2(0, Mathf.Abs(y));
    }

    // --- Affliction selection ---
    private void ShowAfflictionSelectionSubPanel()
    {
        // First select target, then affliction
        ShowSubPanel("Select ally to remove afflictions from:");
        float y = 0;

        var allies = GetLivingAllies();
        foreach (var ally in allies)
        {
            var captured = ally;
            AddSubButton(ally.Stats.CharacterName,
                $"HP: {ally.Stats.CurrentHP}/{ally.Stats.TotalMaxHP}",
                ref y, () =>
                {
                    _selectedTarget = captured;
                    ShowAfflictionPickerForTarget(captured);
                });
        }

        _subScrollContentRT.sizeDelta = new Vector2(0, Mathf.Abs(y));
    }

    private void ShowAfflictionPickerForTarget(CharacterController target)
    {
        ShowSubPanel($"Select affliction to remove from {target.Stats.CharacterName}:");
        float y = 0;

        var afflictions = (WishAfflictionType[])Enum.GetValues(typeof(WishAfflictionType));
        foreach (var affliction in afflictions)
        {
            string desc;
            switch (affliction)
            {
                case WishAfflictionType.Damage: desc = "Restore all HP to maximum"; break;
                case WishAfflictionType.AbilityDamage: desc = "Heal all ability score damage"; break;
                case WishAfflictionType.Poison: desc = "Cure poison"; break;
                case WishAfflictionType.Disease: desc = "Cure disease"; break;
                case WishAfflictionType.Blindness: desc = "Remove blindness"; break;
                case WishAfflictionType.Deafness: desc = "Remove deafness"; break;
                case WishAfflictionType.NegativeLevels: desc = "Remove negative levels / energy drain"; break;
                default: desc = affliction.ToString(); break;
            }

            WishAfflictionType capturedAffliction = affliction;
            AddSubButton(affliction.ToString(), desc, ref y, () =>
            {
                ConfirmWish(WishOption.RemoveAfflictions, target, AbilityType.STR, capturedAffliction, null);
            });
        }

        _subScrollContentRT.sizeDelta = new Vector2(0, Mathf.Abs(y));
    }

    // --- Spell duplication selection ---
    private void ShowSpellSelectionSubPanel()
    {
        ShowSubPanel("Select a spell to duplicate (≤ 8th level Wiz/Sor, ≤ 7th other):");
        float y = 0;

        // Gather eligible spells from the database
        var allSpells = SpellDatabase.GetAllSpells();
        if (allSpells != null)
        {
            var eligible = allSpells
                .Where(s => s != null && !string.IsNullOrEmpty(s.SpellId))
                .Where(s => s.SpellLevel <= 8) // rough filter; WishExecutor validates further
                .OrderBy(s => s.SpellLevel)
                .ThenBy(s => s.Name)
                .ToList();

            foreach (var spell in eligible)
            {
                string spellId = spell.SpellId;
                string classes = spell.ClassList != null ? string.Join(", ", spell.ClassList) : "?";
                AddSubButton($"[Lv{spell.SpellLevel}] {spell.Name}",
                    $"Classes: {classes}", ref y, () =>
                    {
                        ConfirmWish(WishOption.DuplicateSpell, _caster, AbilityType.STR,
                            WishAfflictionType.Damage, spellId);
                    });
            }
        }

        if (Mathf.Approximately(y, 0))
        {
            AddSubLabel("No eligible spells found.", ref y);
        }

        _subScrollContentRT.sizeDelta = new Vector2(0, Mathf.Abs(y));
    }

    // ========== CONFIRM ==========

    private void ConfirmWish(WishOption option, CharacterController target,
        AbilityType ability, WishAfflictionType affliction, string spellId)
    {
        _overlayPanel.SetActive(false);
        IsOpen = false;
        OnWishConfirmed?.Invoke(option, target, ability, affliction, spellId);
    }

    // ========== HELPERS ==========

    private List<CharacterController> GetLivingAllies()
    {
        var result = new List<CharacterController>();
        var allChars = GameManager.Instance?.GetAllCharactersForAI();
        if (allChars == null) return result;

        foreach (var ch in allChars)
        {
            if (ch == null || ch.Stats == null) continue;
            if (!ch.Stats.IsPlayerControlled) continue;
            if (ch.HasCondition(CombatConditionType.Dead)) continue;
            result.Add(ch);
        }
        return result;
    }

    private string GetInherentBonusInfo(CharacterController ch)
    {
        var abilities = new[] { AbilityType.STR, AbilityType.DEX, AbilityType.CON,
                                AbilityType.WIS, AbilityType.INT, AbilityType.CHA };
        var parts = new List<string>();
        foreach (var a in abilities)
        {
            int bonus = ch.Stats.GetInherentBonus(a);
            if (bonus > 0) parts.Add($"{a}+{bonus}");
        }
        return parts.Count > 0
            ? $"Inherent: {string.Join(", ", parts)}"
            : "No inherent bonuses yet";
    }

    private void AddSubButton(string title, string desc, ref float y, Action onClick)
    {
        GameObject row = MakePanel(_subScrollContent.transform, "SubRow",
            new Vector2(0.02f, 1), new Vector2(0.98f, 1), new Vector2(0.5f, 1),
            new Vector2(0, y - SUB_ROW_H / 2f), new Vector2(0, SUB_ROW_H),
            new Color(0.16f, 0.16f, 0.24f));

        MakeText(row.transform, "Title",
            new Vector2(8, 4), new Vector2(460, 18),
            title, 13, Color.white, TextAnchor.MiddleLeft);

        MakeText(row.transform, "Desc",
            new Vector2(8, -8), new Vector2(460, 16),
            desc, 10, new Color(0.6f, 0.6f, 0.7f), TextAnchor.MiddleLeft);

        var btn = row.AddComponent<Button>();
        var nav = btn.navigation;
        nav.mode = Navigation.Mode.None;
        btn.navigation = nav;
        btn.onClick.AddListener(() => onClick?.Invoke());

        _subRows.Add(row);
        y -= (SUB_ROW_H + SUB_ROW_SPACING);
    }

    private void AddSubLabel(string text, ref float y)
    {
        GameObject row = MakePanel(_subScrollContent.transform, "SubLabel",
            new Vector2(0.02f, 1), new Vector2(0.98f, 1), new Vector2(0.5f, 1),
            new Vector2(0, y - SUB_ROW_H / 2f), new Vector2(0, SUB_ROW_H),
            new Color(0.12f, 0.12f, 0.16f));

        MakeText(row.transform, "Text",
            new Vector2(8, 0), new Vector2(460, SUB_ROW_H),
            text, 12, new Color(0.5f, 0.5f, 0.5f), TextAnchor.MiddleLeft);

        _subRows.Add(row);
        y -= (SUB_ROW_H + SUB_ROW_SPACING);
    }

    // ========== UI FACTORY METHODS ==========

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

        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
        labelGO.AddComponent<CanvasRenderer>();
        var txt = labelGO.AddComponent<Text>();
        txt.font = _font;
        txt.fontSize = fontSize;
        txt.color = textColor;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.text = label;

        return btn;
    }
}
