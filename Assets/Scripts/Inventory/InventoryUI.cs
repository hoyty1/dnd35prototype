using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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

    // UI Elements - Equipment Slots
    private Image _armorSlotBg;
    private Text _armorSlotText;
    private Image _leftHandSlotBg;
    private Text _leftHandSlotText;
    private Image _rightHandSlotBg;
    private Text _rightHandSlotText;

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
    private static readonly Color SlotHover = new Color(0.3f, 0.3f, 0.4f, 0.95f);
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

        // Separator
        CreatePanel(PanelRoot.transform, "EquipSep",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(15, -88), new Vector2(-30, 1),
            new Color(0.8f, 0.7f, 0.4f, 0.4f));

        // Equipment slots layout (3 slots side by side)
        float equipY = -100;
        float slotSize = 80;
        float spacing = 20;
        float equipStartX = (520 - (slotSize * 3 + spacing * 2)) / 2;

        // Armor slot
        _armorSlotBg = CreateEquipSlot(PanelRoot.transform, "ArmorSlot",
            new Vector2(equipStartX, equipY), new Vector2(slotSize, slotSize + 20),
            "ARMOR", out _armorSlotText);

        // Left Hand slot
        _leftHandSlotBg = CreateEquipSlot(PanelRoot.transform, "LeftHandSlot",
            new Vector2(equipStartX + slotSize + spacing, equipY), new Vector2(slotSize, slotSize + 20),
            "LEFT HAND", out _leftHandSlotText);

        // Right Hand slot
        _rightHandSlotBg = CreateEquipSlot(PanelRoot.transform, "RightHandSlot",
            new Vector2(equipStartX + (slotSize + spacing) * 2, equipY), new Vector2(slotSize, slotSize + 20),
            "RIGHT HAND", out _rightHandSlotText);

        // ===== INVENTORY SECTION =====
        float invLabelY = equipY - slotSize - 35;
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
        float cellSize = 56;
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
            new Vector2(0, 50), new Vector2(-20, 40),
            "Click an item to select, then click a slot to equip/move.\nClick an equipped item to unequip it.",
            12, new Color(0.6f, 0.6f, 0.5f), TextAnchor.MiddleCenter);

        // ===== TOOLTIP =====
        BuildTooltip();

        PanelRoot.SetActive(false);
    }

    private void BuildTooltip()
    {
        _tooltipPanel = CreatePanel(PanelRoot.transform, "Tooltip",
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0.5f),
            new Vector2(10, 0), new Vector2(200, 140),
            new Color(0.05f, 0.05f, 0.1f, 0.95f));

        // Border
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

    // ===== UPDATE / INPUT =====

    private void Update()
    {
        if (!IsOpen || CurrentCharacter == null) return;

        // Handle mouse hover for tooltips
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

        // Check equipment slots
        if (IsOverRect(_armorSlotBg, mousePos))
        {
            ShowEquipTooltip(EquipSlot.Armor);
            return;
        }
        if (IsOverRect(_leftHandSlotBg, mousePos))
        {
            ShowEquipTooltip(EquipSlot.LeftHand);
            return;
        }
        if (IsOverRect(_rightHandSlotBg, mousePos))
        {
            ShowEquipTooltip(EquipSlot.RightHand);
            return;
        }

        // Check general slots
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
        // Convert to panel-local coordinates
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        RectTransform panelRT = PanelRoot.GetComponent<RectTransform>();
        for (int i = 0; i < 4; i++)
        {
            corners[i] = panelRT.InverseTransformPoint(corners[i]);
        }
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
            // Try to equip selected general item into this slot
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
            // Unequip this slot's item
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
            // Should not happen — equip slots don't get "selected" to move
            ClearSelection();
            RefreshUI();
            return;
        }

        if (_hasSelection && _selectedGeneralSlot >= 0)
        {
            // Swap items between two general slots
            if (_selectedGeneralSlot != index)
            {
                var temp = inv.GeneralSlots[_selectedGeneralSlot];
                inv.GeneralSlots[_selectedGeneralSlot] = inv.GeneralSlots[index];
                inv.GeneralSlots[index] = temp;
            }
            ClearSelection();
            RefreshUI();
        }
        else
        {
            // Select this slot
            if (inv.GeneralSlots[index] != null)
            {
                _selectedGeneralSlot = index;
                _hasSelection = true;
                RefreshUI();
            }
        }
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

        // Character info
        if (_charNameText != null)
            _charNameText.text = $"{stats.CharacterName}'s Inventory ({stats.CharacterClass} Lv {stats.Level})";

        if (_charStatsText != null)
            _charStatsText.text = $"HP: {stats.CurrentHP}/{stats.MaxHP}  AC: {stats.ArmorClass}  Atk: {CharacterStats.FormatMod(stats.AttackBonus)}  Dmg: {stats.BaseDamageCount}d{stats.BaseDamageDice}";

        // Equipment slots
        RefreshEquipSlot(_armorSlotBg, _armorSlotText, inv.ArmorSlot, "Empty");
        RefreshEquipSlot(_leftHandSlotBg, _leftHandSlotText, inv.LeftHandSlot, "Empty");
        RefreshEquipSlot(_rightHandSlotBg, _rightHandSlotText, inv.RightHandSlot, "Empty");

        // General inventory
        for (int i = 0; i < Inventory.GeneralSlotCount; i++)
        {
            RefreshGeneralSlot(i, inv.GeneralSlots[i]);
        }

        // Update instruction text based on selection
        if (_instructionText != null)
        {
            if (_hasSelection && _selectedGeneralSlot >= 0)
            {
                var selItem = inv.GeneralSlots[_selectedGeneralSlot];
                string itemName = selItem != null ? selItem.Name : "item";
                _instructionText.text = $"Selected: {itemName}\nClick an equipment slot to equip, or another inventory slot to swap.";
                _instructionText.color = new Color(0.4f, 0.9f, 0.4f);
            }
            else
            {
                _instructionText.text = "Click an item to select, then click a slot to equip/move.\nClick an equipped item to unequip it.";
                _instructionText.color = new Color(0.6f, 0.6f, 0.5f);
            }
        }
    }

    private void RefreshEquipSlot(Image bg, Text text, ItemData item, string emptyLabel)
    {
        if (bg == null || text == null) return;
        if (item != null)
        {
            text.text = $"{item.IconChar}\n{item.Name}";
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
        // Update the main combat UI
        if (GameManager.Instance != null && GameManager.Instance.CombatUI != null)
        {
            GameManager.Instance.CombatUI.UpdateAllStats(
                GameManager.Instance.PC1, GameManager.Instance.PC2, GameManager.Instance.NPC);
        }
    }

    // ===== UI CREATION HELPERS =====

    private Image CreateEquipSlot(Transform parent, string name, Vector2 pos, Vector2 size, string label, out Text itemText)
    {
        // Slot container
        GameObject slotGO = CreatePanel(parent, name,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            pos, size,
            SlotEmpty);

        // Label on top
        CreateText(slotGO.transform, name + "Label",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, -2), new Vector2(0, 16),
            label, 10, new Color(0.7f, 0.7f, 0.5f), TextAnchor.MiddleCenter);

        // Item text/icon
        itemText = CreateText(slotGO.transform, name + "Item",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            new Vector2(0, -6), new Vector2(-4, -18),
            "Empty", 13, new Color(0.4f, 0.4f, 0.4f), TextAnchor.MiddleCenter);

        // Add click handler
        Button btn = slotGO.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = Color.white;
        colors.selectedColor = Color.white;
        btn.colors = colors;
        btn.targetGraphic = slotGO.GetComponent<Image>();

        EquipSlot slot = EquipSlot.None;
        if (name.Contains("Armor")) slot = EquipSlot.Armor;
        else if (name.Contains("Left")) slot = EquipSlot.LeftHand;
        else if (name.Contains("Right")) slot = EquipSlot.RightHand;

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

        // Add click handler
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
