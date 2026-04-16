using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Inventory UI panel showing equipment slots and general inventory grid.
/// Toggle with "I" key during the active PC's turn.
/// Click-to-equip/unequip functionality.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    // Panel root
    public GameObject PanelRoot;

    // References
    public CharacterController CurrentCharacter;

    private readonly Dictionary<EquipSlot, Image> _equipSlotBgs = new Dictionary<EquipSlot, Image>();
    private readonly Dictionary<EquipSlot, Text> _equipSlotTexts = new Dictionary<EquipSlot, Text>();

    private struct EquipSlotDisplay
    {
        public EquipSlot Slot;
        public string Label;

        public EquipSlotDisplay(EquipSlot slot, string label)
        {
            Slot = slot;
            Label = label;
        }
    }

    // D&D 3.5e body slots + legacy combat hand slots.
    private static readonly EquipSlotDisplay[] VisibleEquipSlots =
    {
        new EquipSlotDisplay(EquipSlot.Head, "HEAD"),
        new EquipSlotDisplay(EquipSlot.FaceEyes, "FACE/EYES"),
        new EquipSlotDisplay(EquipSlot.Neck, "NECK"),
        new EquipSlotDisplay(EquipSlot.Torso, "TORSO"),
        new EquipSlotDisplay(EquipSlot.ArmorRobe, "ARMOR/ROBE"),
        new EquipSlotDisplay(EquipSlot.Waist, "WAIST"),
        new EquipSlotDisplay(EquipSlot.Back, "BACK"),

        new EquipSlotDisplay(EquipSlot.Wrists, "WRISTS"),
        new EquipSlotDisplay(EquipSlot.Hands, "HANDS"),
        new EquipSlotDisplay(EquipSlot.LeftRing, "L RING"),
        new EquipSlotDisplay(EquipSlot.RightRing, "R RING"),
        new EquipSlotDisplay(EquipSlot.Feet, "FEET"),
        new EquipSlotDisplay(EquipSlot.LeftHand, "L HAND"),
        new EquipSlotDisplay(EquipSlot.RightHand, "R HAND")
    };

    // UI Elements - General Inventory
    private Image[] _generalSlotBgs;
    private Text[] _generalSlotTexts;

    // Tooltip
    private GameObject _tooltipPanel;
    private Text _tooltipNameText;
    private Text _tooltipTypeText;
    private Text _tooltipStatsText;
    private Text _tooltipDescText;

    // Character info
    private Text _charNameText;
    private Text _charStatsText;
    private Text _instructionText;

    // Standalone-only UI elements (hidden when embedded in CharacterSheetUI)
    private GameObject _titleBar;
    private GameObject _closeHint;

    // Selection state for click-to-equip
    private int _selectedGeneralSlot = -1;
    private EquipSlot _selectedEquipSlot = EquipSlot.None;
    private bool _hasSelection = false;

    // Colors
    private static readonly Color SlotNormal = new Color(0.2f, 0.2f, 0.25f, 0.9f);
    private static readonly Color SlotSelected = new Color(0.3f, 0.5f, 0.3f, 0.95f);
    private static readonly Color SlotEquipped = new Color(0.25f, 0.25f, 0.35f, 0.9f);
    private static readonly Color SlotEmpty = new Color(0.15f, 0.15f, 0.2f, 0.8f);

    /// <summary>
    /// When true, this InventoryUI is embedded inside CharacterSheetUI's right panel.
    /// In embedded mode, the standalone 'I' key toggle is disabled, and the panel
    /// visibility is controlled by CharacterSheetUI.
    /// </summary>
    public bool IsEmbedded = false;

    public bool IsOpen => PanelRoot != null && PanelRoot.activeSelf;

    private Canvas _parentCanvas;
    private Font _uiFont;

    /// <summary>Build the entire inventory UI programmatically.</summary>
    public void BuildUI(Canvas canvas)
    {
        _parentCanvas = canvas;
        _uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_uiFont == null) _uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (_uiFont == null) _uiFont = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // Main panel - centered overlay
        PanelRoot = CreatePanel(canvas.transform, "InventoryPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(520, 580),
            new Color(0.08f, 0.08f, 0.12f, 0.95f));

        // Title bar
        _titleBar = CreatePanel(PanelRoot.transform, "TitleBar",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            Vector2.zero, new Vector2(0, 40),
            new Color(0.15f, 0.15f, 0.25f, 1f));

        _charNameText = CreateText(PanelRoot.transform, "CharName",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(15, -5), new Vector2(-30, 30),
            "Character Inventory", 20, new Color(0.9f, 0.85f, 0.6f), TextAnchor.MiddleLeft);

        // Close hint
        _closeHint = CreateText(PanelRoot.transform, "CloseHint",
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-10, -5), new Vector2(120, 30),
            "[I] Close", 14, new Color(0.6f, 0.6f, 0.6f), TextAnchor.MiddleRight).gameObject;

        // Character stats summary
        _charStatsText = CreateText(PanelRoot.transform, "CharStats",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(15, -42), new Vector2(-30, 20),
            "", 13, new Color(0.7f, 0.7f, 0.8f), TextAnchor.MiddleLeft);

        // ===== EQUIPMENT SECTION =====
        CreateText(PanelRoot.transform, "EquipLabel",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(15, -68), new Vector2(-30, 20),
            "EQUIPMENT", 15, new Color(0.8f, 0.7f, 0.4f), TextAnchor.MiddleLeft);

        CreatePanel(PanelRoot.transform, "EquipSep",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(15, -88), new Vector2(-30, 1),
            new Color(0.8f, 0.7f, 0.4f, 0.4f));

        BuildEquipmentSlots();

        // ===== INVENTORY SECTION =====
        float invLabelY = -248;
        CreateText(PanelRoot.transform, "InvLabel",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(15, invLabelY), new Vector2(-30, 20),
            "INVENTORY", 15, new Color(0.4f, 0.7f, 0.8f), TextAnchor.MiddleLeft);

        CreatePanel(PanelRoot.transform, "InvSep",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(15, invLabelY - 20), new Vector2(-30, 1),
            new Color(0.4f, 0.7f, 0.8f, 0.4f));

        // General inventory grid (5 columns x 4 rows = 20 slots)
        _generalSlotBgs = new Image[Inventory.GeneralSlotCount];
        _generalSlotTexts = new Text[Inventory.GeneralSlotCount];

        float gridStartY = invLabelY - 30;
        float cellSize = 48;
        float cellSpacing = 6;
        int cols = 5;
        float gridStartX = (520 - (cellSize * cols + cellSpacing * (cols - 1))) / 2;

        for (int i = 0; i < Inventory.GeneralSlotCount; i++)
        {
            int col = i % cols;
            int row = i / cols;
            float x = gridStartX + col * (cellSize + cellSpacing);
            float y = gridStartY - row * (cellSize + cellSpacing);

            CreateGeneralSlot(PanelRoot.transform, i, new Vector2(x, y), new Vector2(cellSize, cellSize));
        }

        // ===== INSTRUCTION TEXT =====
        _instructionText = CreateText(PanelRoot.transform, "Instructions",
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
            new Vector2(0, 28), new Vector2(-20, 32),
            "Click equipment items to select/equip.\nClick consumables to use them from inventory.",
            12, new Color(0.6f, 0.6f, 0.5f), TextAnchor.MiddleCenter);

        // ===== TOOLTIP =====
        BuildTooltip();

        PanelRoot.SetActive(false);
    }

    private void BuildEquipmentSlots()
    {
        _equipSlotBgs.Clear();
        _equipSlotTexts.Clear();

        float startY = -100f;
        float slotWidth = 66f;
        float slotHeight = 74f;
        float spacingX = 4f;
        float spacingY = 8f;
        int cols = 7;

        float gridWidth = cols * slotWidth + (cols - 1) * spacingX;
        float startX = (520f - gridWidth) / 2f;

        for (int i = 0; i < VisibleEquipSlots.Length; i++)
        {
            int row = i / cols;
            int col = i % cols;

            float x = startX + col * (slotWidth + spacingX);
            float y = startY - row * (slotHeight + spacingY);

            var def = VisibleEquipSlots[i];
            Text slotText;
            Image slotBg = CreateEquipSlot(
                PanelRoot.transform,
                $"Equip_{def.Slot}",
                def.Slot,
                new Vector2(x, y),
                new Vector2(slotWidth, slotHeight),
                def.Label,
                out slotText);

            _equipSlotBgs[def.Slot] = slotBg;
            _equipSlotTexts[def.Slot] = slotText;
        }
    }

    private void BuildTooltip()
    {
        _tooltipPanel = CreatePanel(PanelRoot.transform, "Tooltip",
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0.5f),
            new Vector2(10, 0), new Vector2(210, 140),
            new Color(0.05f, 0.05f, 0.1f, 0.95f));

        CreatePanel(_tooltipPanel.transform, "TooltipBorder",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(0, 0),
            new Color(0.5f, 0.5f, 0.3f, 0.3f));

        _tooltipNameText = CreateText(_tooltipPanel.transform, "TipName",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(8, -5), new Vector2(-16, 24),
            "", 16, new Color(1f, 0.9f, 0.6f), TextAnchor.MiddleLeft);

        _tooltipTypeText = CreateText(_tooltipPanel.transform, "TipType",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(8, -30), new Vector2(-16, 18),
            "", 12, new Color(0.6f, 0.6f, 0.8f), TextAnchor.MiddleLeft);

        _tooltipStatsText = CreateText(_tooltipPanel.transform, "TipStats",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(8, -52), new Vector2(-16, 20),
            "", 13, new Color(0.4f, 0.9f, 0.4f), TextAnchor.MiddleLeft);

        _tooltipDescText = CreateText(_tooltipPanel.transform, "TipDesc",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(8, -76), new Vector2(-16, 60),
            "", 11, new Color(0.7f, 0.7f, 0.7f), TextAnchor.UpperLeft);

        _tooltipPanel.SetActive(false);
    }

    // ===== PUBLIC API =====

    /// <summary>
    /// Hides UI elements that are only relevant when InventoryUI is standalone
    /// (title bar, close hint). Called when embedding into CharacterSheetUI.
    /// </summary>
    public void HideStandaloneElements()
    {
        if (_titleBar != null) _titleBar.SetActive(false);
        if (_closeHint != null) _closeHint.SetActive(false);
    }

    public void Open(CharacterController character)
    {
        if (character == null || character.Stats == null) return;
        CurrentCharacter = character;
        ClearSelection();
        RefreshUI();
        PanelRoot.SetActive(true);
    }

    public void Close()
    {
        if (PanelRoot != null)
            PanelRoot.SetActive(false);
        ClearSelection();
        HideTooltip();
        CurrentCharacter = null;
    }

    public void Toggle(CharacterController character)
    {
        if (IsOpen)
            Close();
        else
            Open(character);
    }

    private void Update()
    {
        if (!IsOpen || CurrentCharacter == null) return;
        HandleHover();
    }

    private void HandleHover()
    {
        Vector2 mousePos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            PanelRoot.GetComponent<RectTransform>(),
            GetMousePosition(),
            _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _parentCanvas.worldCamera,
            out mousePos);

        foreach (var kv in _equipSlotBgs)
        {
            if (IsOverRect(kv.Value, mousePos))
            {
                ShowEquipTooltip(kv.Key);
                return;
            }
        }

        for (int i = 0; i < Inventory.GeneralSlotCount; i++)
        {
            if (_generalSlotBgs[i] != null && IsOverRect(_generalSlotBgs[i], mousePos))
            {
                var inv = GetInventory();
                if (inv != null && inv.GeneralSlots[i] != null)
                    ShowTooltip(inv.GeneralSlots[i]);
                else
                    HideTooltip();
                return;
            }
        }

        HideTooltip();
    }

    private bool IsOverRect(Image img, Vector2 localMousePos)
    {
        if (img == null) return false;
        var rt = img.GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        RectTransform panelRT = PanelRoot.GetComponent<RectTransform>();
        for (int i = 0; i < 4; i++)
            corners[i] = panelRT.InverseTransformPoint(corners[i]);

        float minX = corners[0].x, maxX = corners[2].x;
        float minY = corners[0].y, maxY = corners[2].y;
        return localMousePos.x >= minX && localMousePos.x <= maxX &&
               localMousePos.y >= minY && localMousePos.y <= maxY;
    }

    private Vector3 GetMousePosition()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null) return mouse.position.ReadValue();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.mousePosition;
#endif
        return Vector3.zero;
    }

    // ===== CLICK HANDLING =====

    /// <summary>Called when an equipment slot is clicked.</summary>
    public void OnEquipSlotClicked(EquipSlot slot)
    {
        var inv = GetInventory();
        if (inv == null) return;

        if (_hasSelection && _selectedGeneralSlot >= 0)
        {
            var item = inv.GeneralSlots[_selectedGeneralSlot];
            if (item != null && item.CanEquipIn(slot))
            {
                inv.EquipFromInventory(_selectedGeneralSlot, slot);
                ClearSelection();
                RefreshUI();
                NotifyStatsChanged();
            }
            else
            {
                ClearSelection();
                RefreshUI();
            }
        }
        else
        {
            if (inv.GetEquipped(slot) != null)
            {
                inv.Unequip(slot);
                ClearSelection();
                RefreshUI();
                NotifyStatsChanged();
            }
        }
    }

    /// <summary>Called when a general inventory slot is clicked.</summary>
    public void OnGeneralSlotClicked(int index)
    {
        var inv = GetInventory();
        if (inv == null) return;

        if (_hasSelection && _selectedEquipSlot != EquipSlot.None)
        {
            ClearSelection();
            RefreshUI();
            return;
        }

        if (_hasSelection && _selectedGeneralSlot >= 0)
        {
            // Clicking the same selected consumable slot uses the item.
            if (_selectedGeneralSlot == index)
            {
                ItemData selectedItem = inv.GeneralSlots[index];
                if (selectedItem != null && selectedItem.IsConsumable)
                {
                    TryUseConsumable(index, selectedItem);
                    return;
                }

                ClearSelection();
                RefreshUI();
                return;
            }

            var temp = inv.GeneralSlots[_selectedGeneralSlot];
            inv.GeneralSlots[_selectedGeneralSlot] = inv.GeneralSlots[index];
            inv.GeneralSlots[index] = temp;
            ClearSelection();
            RefreshUI();
            return;
        }

        ItemData clickedItem = inv.GeneralSlots[index];
        if (clickedItem == null)
            return;

        // Single-click convenience: consumables can be used directly from inventory.
        if (clickedItem.IsConsumable)
        {
            TryUseConsumable(index, clickedItem);
            return;
        }

        _selectedGeneralSlot = index;
        _hasSelection = true;
        RefreshUI();
    }

    private void TryUseConsumable(int index, ItemData item)
    {
        if (CurrentCharacter == null || item == null)
            return;

        GameManager gm = GameManager.Instance;
        bool used = false;
        string feedback = string.Empty;

        if (gm != null)
        {
            used = gm.TryUseConsumableFromInventory(CurrentCharacter, index, out feedback);
            if (!used && !string.IsNullOrEmpty(feedback))
                gm.CombatUI?.ShowCombatLog($"⚠ {feedback}");
        }

        if (!used && gm == null)
        {
            feedback = "Cannot use consumables: GameManager is unavailable.";
        }

        if (used)
            NotifyStatsChanged();

        ClearSelection();
        RefreshUI();
    }
    private void ClearSelection()
    {
        _selectedGeneralSlot = -1;
        _selectedEquipSlot = EquipSlot.None;
        _hasSelection = false;
    }

    // ===== REFRESH =====

    public void RefreshUI()
    {
        if (CurrentCharacter == null) return;
        var stats = CurrentCharacter.Stats;
        var inv = GetInventory();
        if (inv == null) return;

        if (_charNameText != null)
            _charNameText.text = $"{stats.CharacterName}'s Inventory ({stats.CharacterClass} Lv {stats.Level})";

        if (_charStatsText != null)
        {
            string baseSummary = $"HP: {stats.CurrentHP}/{stats.MaxHP}  AC: {stats.ArmorClass}  Atk: {CharacterStats.FormatMod(stats.AttackBonus)}  Dmg: {stats.BaseDamageCount}d{stats.BaseDamageDice}";
            string armorSummary = stats.ArmorCheckPenalty > 0 ? $"  ACP: -{stats.ArmorCheckPenalty}" : "";

            int asfChance = (stats.IsAffectedByArcaneSpellFailure)
                ? Mathf.Clamp(stats.ArcaneSpellFailure, 0, 100)
                : 0;
            string asfSummary = asfChance > 0 ? $"  ASF: {asfChance}%" : "";

            _charStatsText.text = baseSummary + armorSummary + asfSummary;
        }

        foreach (var def in VisibleEquipSlots)
        {
            if (_equipSlotBgs.TryGetValue(def.Slot, out var bg) && _equipSlotTexts.TryGetValue(def.Slot, out var text))
            {
                RefreshEquipSlot(bg, text, inv.GetEquipped(def.Slot), "Empty");
            }
        }

        for (int i = 0; i < Inventory.GeneralSlotCount; i++)
            RefreshGeneralSlot(i, inv.GeneralSlots[i]);

        if (_instructionText != null)
        {
            if (_hasSelection && _selectedGeneralSlot >= 0)
            {
                var selItem = inv.GeneralSlots[_selectedGeneralSlot];
                string itemName = selItem != null ? selItem.Name : "item";
                bool selectedIsConsumable = selItem != null && selItem.IsConsumable;
                _instructionText.text = selectedIsConsumable
                    ? $"Selected: {itemName}\nClick again to use, or click another inventory slot to swap."
                    : $"Selected: {itemName}\nClick an equipment slot to equip, or another inventory slot to swap.";
                _instructionText.color = new Color(0.4f, 0.9f, 0.4f);
            }
            else
            {
                _instructionText.text = "Click equipment items to select/equip.\nClick consumables to use them from inventory.";
                _instructionText.color = new Color(0.6f, 0.6f, 0.5f);
            }
        }
    }

    private void RefreshEquipSlot(Image bg, Text text, ItemData item, string emptyLabel)
    {
        if (bg == null || text == null) return;
        if (item != null)
        {
            text.text = string.IsNullOrEmpty(item.IconChar) ? item.Name : $"{item.IconChar}\n{item.Name}";
            text.color = item.IconColor;
            bg.color = SlotEquipped;
        }
        else
        {
            text.text = emptyLabel;
            text.color = new Color(0.4f, 0.4f, 0.4f);
            bg.color = SlotEmpty;
        }
    }

    private void RefreshGeneralSlot(int index, ItemData item)
    {
        if (_generalSlotBgs[index] == null) return;

        bool isSelected = (_hasSelection && _selectedGeneralSlot == index);

        if (item != null)
        {
            _generalSlotTexts[index].text = item.IconChar;
            _generalSlotTexts[index].color = item.IconColor;
            _generalSlotBgs[index].color = isSelected ? SlotSelected : SlotNormal;
        }
        else
        {
            _generalSlotTexts[index].text = "";
            _generalSlotBgs[index].color = isSelected ? SlotSelected : SlotEmpty;
        }
    }

    // ===== TOOLTIP =====

    private void ShowTooltip(ItemData item)
    {
        if (item == null || _tooltipPanel == null) return;
        _tooltipPanel.SetActive(true);
        _tooltipNameText.text = item.Name;
        _tooltipTypeText.text = item.Type.ToString();
        _tooltipStatsText.text = item.GetStatSummary();
        _tooltipDescText.text = item.Description;
    }

    private void ShowEquipTooltip(EquipSlot slot)
    {
        var inv = GetInventory();
        if (inv == null) return;
        var item = inv.GetEquipped(slot);
        if (item != null)
            ShowTooltip(item);
        else
            HideTooltip();
    }

    private void HideTooltip()
    {
        if (_tooltipPanel != null)
            _tooltipPanel.SetActive(false);
    }

    // ===== HELPERS =====

    private Inventory GetInventory()
    {
        if (CurrentCharacter == null) return null;
        return CurrentCharacter.GetComponent<InventoryComponent>()?.CharacterInventory;
    }

    private void NotifyStatsChanged()
    {
        if (GameManager.Instance != null && GameManager.Instance.CombatUI != null)
        {
            GameManager.Instance.CombatUI.UpdateAllStats(
                GameManager.Instance.PC1, GameManager.Instance.PC2, GameManager.Instance.NPC);
        }
    }

    // ===== UI CREATION HELPERS =====

    private Image CreateEquipSlot(Transform parent, string name, EquipSlot slot, Vector2 pos, Vector2 size, string label, out Text itemText)
    {
        GameObject slotGO = CreatePanel(parent, name,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            pos, size,
            SlotEmpty);

        CreateText(slotGO.transform, name + "Label",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, -2), new Vector2(0, 16),
            label, 9, new Color(0.7f, 0.7f, 0.5f), TextAnchor.MiddleCenter);

        itemText = CreateText(slotGO.transform, name + "Item",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            new Vector2(0, -6), new Vector2(-4, -18),
            "Empty", 11, new Color(0.4f, 0.4f, 0.4f), TextAnchor.MiddleCenter);

        Button btn = slotGO.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = Color.white;
        colors.selectedColor = Color.white;
        btn.colors = colors;
        btn.targetGraphic = slotGO.GetComponent<Image>();

        EquipSlot capturedSlot = slot;
        btn.onClick.AddListener(() => OnEquipSlotClicked(capturedSlot));

        return slotGO.GetComponent<Image>();
    }

    private void CreateGeneralSlot(Transform parent, int index, Vector2 pos, Vector2 size)
    {
        GameObject slotGO = CreatePanel(parent, "GenSlot_" + index,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            pos, size,
            SlotEmpty);

        Text itemText = CreateText(slotGO.transform, "GenSlotText_" + index,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero,
            "", 22, Color.white, TextAnchor.MiddleCenter);

        _generalSlotBgs[index] = slotGO.GetComponent<Image>();
        _generalSlotTexts[index] = itemText;

        Button btn = slotGO.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = Color.white;
        colors.selectedColor = Color.white;
        btn.colors = colors;
        btn.targetGraphic = slotGO.GetComponent<Image>();

        int capturedIndex = index;
        btn.onClick.AddListener(() => OnGeneralSlotClicked(capturedIndex));
    }

    private GameObject CreatePanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta, Color bgColor)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        Image img = go.AddComponent<Image>();
        img.color = bgColor;
        return go;
    }

    private Text CreateText(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta,
        string text, int fontSize, Color color, TextAnchor alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        Text t = go.AddComponent<Text>();
        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = alignment;
        t.font = _uiFont;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }
}