using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Runtime-generated pre-combat inventory management UI.
/// Supports stash browsing, character selection, equipment/inventory management,
/// drag-and-drop transfers, validation feedback, and right-click quick actions.
/// </summary>
public class PreCombatInventoryUI : MonoBehaviour
{
    private const int InventoryGridColumns = 6;
    private const float SlotSize = 60f;
    private const float SlotSpacing = 8f;

    // Equipment layout mirrors InventoryUI.BuildEquipmentSlots() for visual consistency.
    private const int EquipmentGridPreferredColumns = 7;
    private const float EquipmentCellWidth = 66f;
    private const float EquipmentCellHeight = 74f;
    private const float EquipmentSpacingX = 4f;
    private const float EquipmentSpacingY = 8f;
    private static readonly Color EquipmentSlotEmptyColor = new Color(0.15f, 0.15f, 0.2f, 0.8f);
    private static readonly Color EquipmentSlotEquippedColor = new Color(0.25f, 0.25f, 0.35f, 0.9f);

    // Window layout split: 30% stash (left) / 70% character management (right).
    private const float StashWidthRatio = 0.30f;
    private const float DividerAnchorX = 0.30f;
    private const float CharacterSectionStartRatio = 0.30f;

    private const int MinStashColumns = 3;
    private const int MaxStashColumns = 8;

    private enum SlotContainerType
    {
        None,
        Stash,
        Inventory,
        InventoryStack,
        Equipment
    }

    private enum StashFilterMode
    {
        All,
        Weapons,
        Armor,
        Consumables,
        Misc
    }

    private enum StashSortMode
    {
        Name,
        Type,
        Value
    }

    private sealed class ItemStackGroup
    {
        public string Key;
        public ItemData Prototype;
        public readonly List<ItemData> Instances = new List<ItemData>();
        public int Quantity => Instances.Count;
        public ItemData FirstItem => Instances.Count > 0 ? Instances[0] : null;
    }

    private sealed class SlotRef
    {
        public SlotContainerType Container;
        public CharacterController Character;
        public int InventoryIndex = -1;
        public EquipSlot EquipSlot = EquipSlot.None;
        public ItemStackGroup ItemGroup;

        public bool IsSameLocation(SlotRef other)
        {
            if (other == null || Container != other.Container)
                return false;

            switch (Container)
            {
                case SlotContainerType.Stash:
                case SlotContainerType.InventoryStack:
                    return ItemGroup == other.ItemGroup;
                case SlotContainerType.Inventory:
                    return Character == other.Character && InventoryIndex == other.InventoryIndex;
                case SlotContainerType.Equipment:
                    return Character == other.Character && EquipSlot == other.EquipSlot;
                default:
                    return false;
            }
        }
    }

    private sealed class DropValidationResult
    {
        public bool IsValid;
        public bool IsWarning;
        public string Message;

        public static DropValidationResult Valid(string message = "")
        {
            return new DropValidationResult { IsValid = true, Message = message };
        }

        public static DropValidationResult ValidWithWarning(string warning)
        {
            return new DropValidationResult { IsValid = true, IsWarning = true, Message = warning };
        }

        public static DropValidationResult Invalid(string message)
        {
            return new DropValidationResult { IsValid = false, Message = message };
        }
    }

    /// <summary>
    /// Explicit drag manager object (requested technical structure).
    /// Holds drag state and lets slot components query drag source safely.
    /// </summary>
    private sealed class DragDropManager
    {
        private readonly PreCombatInventoryUI _owner;

        public SlotRef DragSource;
        public ItemData DragItem;
        public DraggableItem Draggable;

        public DragDropManager(PreCombatInventoryUI owner)
        {
            _owner = owner;
        }

        public void BeginDrag(DraggableItem draggable)
        {
            if (draggable == null)
                return;

            SlotRef slot = draggable.ResolveSlot();
            ItemData item = _owner.ResolveItem(slot);
            if (slot == null || item == null)
                return;

            DragSource = slot;
            DragItem = item;
            Draggable = draggable;
            _owner.OnDragStarted(this);
        }

        public void UpdateDrag(PointerEventData eventData)
        {
            _owner.OnDragUpdated(this, eventData);
        }

        public void EndDrag(PointerEventData eventData)
        {
            _owner.OnDragEnded(this, eventData);
            DragSource = null;
            DragItem = null;
            Draggable = null;
        }
    }

    /// <summary>
    /// Attached to each item slot UI for Unity drag events.
    /// </summary>
    private sealed class DraggableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private PreCombatInventoryUI _owner;
        private Func<SlotRef> _slotResolver;

        public void Init(PreCombatInventoryUI owner, Func<SlotRef> slotResolver)
        {
            _owner = owner;
            _slotResolver = slotResolver;
        }

        public SlotRef ResolveSlot()
        {
            return _slotResolver != null ? _slotResolver() : null;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            SlotRef slot = ResolveSlot();
            ItemData item = _owner != null ? _owner.ResolveItem(slot) : null;
            Debug.Log($"[PreCombatUI] Drag started | item={(item != null ? item.Name : "<none>")}");
            _owner?.TryBeginDrag(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            _owner?._dragDropManager?.UpdateDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _owner?._dragDropManager?.EndDrag(eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_owner == null || eventData == null)
                return;

            SlotRef slot = ResolveSlot();
            ItemData item = _owner.ResolveItem(slot);

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                Debug.Log($"[PreCombatUI] Item right-clicked: {(item != null ? item.Name : "<none>")}");
                _owner.ShowContextMenu(slot, eventData.position);
            }
            else if (eventData.button == PointerEventData.InputButton.Left)
            {
                Debug.Log($"[PreCombatUI] Item clicked: {(item != null ? item.Name : "<none>")}");
                _owner.HideContextMenu();
                _owner.HandleLeftClickTransfer(slot, item);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _owner?.ShowTooltipForSlot(ResolveSlot());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _owner?.HideTooltip();
        }
    }

    /// <summary>
    /// Attached to drop-capable slots for Unity drop events.
    /// </summary>
    private sealed class DropTarget : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private PreCombatInventoryUI _owner;
        private Func<SlotRef> _slotResolver;
        private Image _background;
        private Outline _outline;
        private Color _baseColor;

        public SlotRef Slot => _slotResolver != null ? _slotResolver() : null;

        public void Init(PreCombatInventoryUI owner, Func<SlotRef> slotResolver, Image background, Color baseColor)
        {
            _owner = owner;
            _slotResolver = slotResolver;
            _background = background;
            _baseColor = baseColor;

            _outline = GetComponent<Outline>();
            if (_outline == null)
                _outline = gameObject.AddComponent<Outline>();

            _outline.effectDistance = new Vector2(1f, -1f);
            _outline.effectColor = new Color(0f, 0f, 0f, 0f);
        }

        public void SetHighlightState(bool active, bool valid)
        {
            if (_background != null)
            {
                if (!active)
                    _background.color = _baseColor;
                else
                    _background.color = valid
                        ? Color.Lerp(_baseColor, new Color(0.18f, 0.74f, 0.31f, 1f), 0.42f)
                        : Color.Lerp(_baseColor, new Color(0.82f, 0.22f, 0.2f, 1f), 0.45f);
            }

            if (_outline != null)
            {
                _outline.effectColor = active
                    ? (valid ? new Color(0.2f, 1f, 0.36f, 0.95f) : new Color(1f, 0.28f, 0.22f, 0.95f))
                    : new Color(0f, 0f, 0f, 0f);
            }
        }

        public void ResetVisual()
        {
            SetHighlightState(false, false);
        }

        public void OnDrop(PointerEventData eventData)
        {
            _owner?.OnItemDroppedOnTarget(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _owner?.OnDropTargetHovered(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _owner?.OnDropTargetUnhovered(this);
        }
    }

    private GameObject _panel;
    private RectTransform _panelRect;
    private RectTransform _stashViewportRect;
    private RectTransform _equipmentViewportRect;
    private GridLayoutGroup _stashGridLayout;
    private GridLayoutGroup _equipmentGridLayout;
    private GridLayoutGroup _inventoryGridLayout;
    private ResizableWindow _resizableWindow;

    private int _lastLoggedStashColumns = -1;
    private int _lastLoggedEquipmentColumns = -1;

    private Text _stashStatusText;
    private Text _stashInfoText;
    private Text _characterHeaderText;
    private Text _messageText;

    private RectTransform _stashContent;
    private RectTransform _characterTabsContent;
    private RectTransform _equipmentContent;
    private RectTransform _inventoryContent;

    private Button _filterButton;
    private Button _sortButton;
    private Button _clearStashButton;
    private Button _beginCombatButton;
    private Button _skipButton;
    private Button _backButton;

    private GameObject _tooltipPanel;
    private Text _tooltipText;
    private bool _tooltipActive;

    private static readonly Vector2 TooltipCursorOffset = new Vector2(15f, -15f);
    private const float TooltipEdgePadding = 8f;

    private GameObject _contextMenuRoot;
    private Button _contextPrimaryButton;
    private Button _contextSecondaryButton;
    private Button _contextTertiaryButton;

    private GameObject _dragPreview;
    private Text _dragPreviewText;

    private readonly List<DropTarget> _dropTargets = new List<DropTarget>();
    private readonly List<Button> _characterTabButtons = new List<Button>();

    private readonly Dictionary<Button, CharacterController> _characterTabMap = new Dictionary<Button, CharacterController>();

    private PartyStash _stash;
    private List<CharacterController> _partyMembers = new List<CharacterController>();
    private int _selectedCharacterIndex;

    private StashFilterMode _stashFilter = StashFilterMode.All;
    private StashSortMode _stashSort = StashSortMode.Name;

    private Action _onBeginCombat;
    private Action _onSkipInventory;
    private Action _onBack;
    private Action _onClosed;
    private bool _wasOpen;
    private bool _isClosing;

    private DragDropManager _dragDropManager;

    public bool IsOpen => _panel != null && _panel.activeSelf;

    public void Open(
        PartyStash stash,
        List<CharacterController> partyMembers,
        Action onBeginCombat,
        Action onSkipInventory,
        Action onBack,
        Action onClosed = null)
    {
        EnsureBuilt();
        if (_panel == null)
            return;

        EnsureInteractionInfrastructure();

        _stash = stash;
        _partyMembers = partyMembers != null ? new List<CharacterController>(partyMembers) : new List<CharacterController>();
        _onBeginCombat = onBeginCombat;
        _onSkipInventory = onSkipInventory;
        _onBack = onBack;
        _onClosed = onClosed;
        _isClosing = false;

        if (_partyMembers.Count == 0)
            _selectedCharacterIndex = -1;
        else
            _selectedCharacterIndex = Mathf.Clamp(_selectedCharacterIndex, 0, _partyMembers.Count - 1);

        _wasOpen = true;
        _panel.SetActive(true);
        Debug.Log("[PreCombatUI] Window opened");

        if (_resizableWindow != null)
        {
            _resizableWindow.MaxSize = new Vector2(
                Mathf.Max(_resizableWindow.MinSize.x, Screen.width - 100f),
                Mathf.Max(_resizableWindow.MinSize.y, Screen.height - 100f));
        }

        HideTooltip();
        HideContextMenu();
        ShowMessage("Drag items between stash, equipment, and backpack. Right-click for quick actions.", true);
        RefreshAll();
    }

    public void Close(bool suppressCallback = false)
    {
        Debug.Log($"[PreCombatUI] Closing | wasOpen={_wasOpen} | suppress={suppressCallback} | isClosing={_isClosing}");

        if (_isClosing)
        {
            Debug.Log("[PreCombatUI] Close already in progress, ignoring re-entrant request.");
            return;
        }

        if (!_wasOpen)
        {
            Debug.Log("[PreCombatUI] Already closed, ignoring");
            return;
        }

        _isClosing = true;
        _wasOpen = false;

        HideTooltip();
        HideContextMenu();
        HideDragPreview();

        if (_panel != null)
            _panel.SetActive(false);

        Action onClosed = _onClosed;
        _onClosed = null;

        if (suppressCallback)
        {
            _onBeginCombat = null;
            _onSkipInventory = null;
            _onBack = null;
        }

        if (!suppressCallback && onClosed != null)
        {
            Debug.Log("[PreCombatUI] Triggering onClosed callback");
            onClosed.Invoke();
        }
        else
        {
            Debug.Log("[PreCombatUI] Callback suppressed or null");
        }

        _isClosing = false;
    }

    private void OnEnable()
    {
        EnsureInteractionInfrastructure();
    }

    private void Update()
    {
        if (_tooltipActive)
            UpdateTooltipPosition();
    }

    private void EnsureBuilt()
    {
        if (_panel != null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            Debug.LogError("[PreCombatInventoryUI] No Canvas found.");
            return;
        }

        EnsureInteractionInfrastructure(canvas);

        _dragDropManager = new DragDropManager(this);

        _panel = CreatePanel(
            canvas.transform,
            "PreCombatInventoryPanel",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(1240f, 760f),
            new Color(0.05f, 0.06f, 0.1f, 0.98f));

        _panelRect = _panel.GetComponent<RectTransform>();

        Outline panelOutline = _panel.AddComponent<Outline>();
        panelOutline.effectDistance = new Vector2(2f, -2f);
        panelOutline.effectColor = new Color(0f, 0f, 0f, 0.65f);

        BuildWindowChrome();

        GameObject contentRoot = new GameObject("ContentRoot", typeof(RectTransform));
        contentRoot.transform.SetParent(_panel.transform, false);
        RectTransform contentRootRt = contentRoot.GetComponent<RectTransform>();
        contentRootRt.anchorMin = new Vector2(0f, 0f);
        contentRootRt.anchorMax = new Vector2(1f, 1f);
        contentRootRt.offsetMin = new Vector2(12f, 92f);
        contentRootRt.offsetMax = new Vector2(-12f, -96f);

        BuildStashSection(contentRootRt);

        GameObject divider = CreatePanel(
            contentRootRt,
            "PanelDivider",
            new Vector2(DividerAnchorX, 0f),
            new Vector2(DividerAnchorX, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(2f, 0f),
            new Color(0.72f, 0.75f, 0.86f, 0.22f));
        divider.GetComponent<Image>().raycastTarget = false;

        GameObject characterRoot = CreatePanel(
            contentRootRt,
            "CharacterPanel",
            new Vector2(CharacterSectionStartRatio, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.08f, 0.1f, 0.16f, 0.95f));
        Image characterRootImage = characterRoot.GetComponent<Image>();
        if (characterRootImage != null)
            characterRootImage.raycastTarget = false;

        BuildCharacterSelectionSection(characterRoot.transform);
        BuildCharacterDetailSection(characterRoot.transform);
        BuildFooter();
        BuildTooltip();
        BuildContextMenu();
        BuildDragPreview();
        BuildWindowResizeBehavior();

        Debug.Log($"[PreCombatUI] Layout split: Stash {StashWidthRatio * 100f:0}% | Character {(1f - StashWidthRatio) * 100f:0}%");

        ReflowResponsiveLayout();
        _panel.SetActive(false);
    }

    private void BuildWindowChrome()
    {
        GameObject header = CreatePanel(
            _panel.transform,
            "HeaderBar",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            Vector2.zero,
            new Vector2(0f, 58f),
            new Color(0.11f, 0.14f, 0.22f, 0.98f));

        CreateText(
            header.transform,
            "Title",
            "PRE-COMBAT INVENTORY MANAGEMENT",
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -2f),
            new Vector2(-96f, -8f),
            23,
            FontStyle.Bold,
            new Color(0.95f, 0.86f, 0.5f),
            TextAnchor.MiddleCenter);

        CreateText(
            header.transform,
            "DragHint",
            "Drag header to move • Resize from right/bottom edges",
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(14f, -18f),
            new Vector2(-280f, -28f),
            10,
            FontStyle.Italic,
            new Color(0.76f, 0.82f, 0.92f),
            TextAnchor.MiddleLeft);

        Button closeButton = CreateButton(
            header.transform,
            "CloseButton",
            "✕",
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-10f, 0f),
            new Vector2(34f, 30f),
            new Color(0.52f, 0.22f, 0.22f, 1f),
            () => Close());
        Text closeText = closeButton.GetComponentInChildren<Text>();
        if (closeText != null)
            closeText.fontSize = 17;

        _stashStatusText = CreateText(
            _panel.transform,
            "StashStatusText",
            "Stash: Unlocked",
            new Vector2(0.02f, 1f),
            new Vector2(0.98f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, -62f),
            new Vector2(0f, 20f),
            13,
            FontStyle.Bold,
            new Color(0.56f, 0.92f, 0.64f),
            TextAnchor.MiddleLeft);

        DraggableWindow draggable = header.AddComponent<DraggableWindow>();
        draggable.WindowRect = _panelRect;
        draggable.ClampToCanvasBounds = true;
        draggable.PersistenceKey = "ui_window_precombat_inventory";
        draggable.SavePositionToPlayerPrefs = true;
    }

    private void BuildWindowResizeBehavior()
    {
        const float minWindowWidth = 1000f;
        const float minWindowHeight = 600f;

        _resizableWindow = _panel.AddComponent<ResizableWindow>();
        _resizableWindow.WindowRect = _panelRect;
        _resizableWindow.MinSize = new Vector2(minWindowWidth, minWindowHeight);
        _resizableWindow.MaxSize = new Vector2(
            Mathf.Max(minWindowWidth, Screen.width - 100f),
            Mathf.Max(minWindowHeight, Screen.height - 100f));
        _resizableWindow.PersistenceKey = "ui_window_precombat_inventory";
        _resizableWindow.SavePositionToPlayerPrefs = true;
        _resizableWindow.SaveSizeToPlayerPrefs = true;
        _resizableWindow.OnResized += _ => ReflowResponsiveLayout();

        SetResizeHandleActive("ResizeHandle_Left", false);
        SetResizeHandleActive("ResizeHandle_Top", false);
        SetResizeHandleActive("ResizeHandle_TopLeft", false);
        SetResizeHandleActive("ResizeHandle_TopRight", false);
        SetResizeHandleActive("ResizeHandle_BottomLeft", false);

        CreateText(
            _panel.transform,
            "CornerResizeGlyph",
            "⤡",
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-2f, 2f),
            new Vector2(20f, 20f),
            14,
            FontStyle.Bold,
            new Color(0.9f, 0.93f, 1f, 0.7f),
            TextAnchor.MiddleCenter);
    }

    private void SetResizeHandleActive(string handleName, bool active)
    {
        if (_panelRect == null)
            return;

        Transform handle = _panelRect.Find(handleName);
        if (handle != null)
            handle.gameObject.SetActive(active);
    }

    private GameObject BuildStashSection(RectTransform parent)
    {
        GameObject stashRoot = CreatePanel(
            parent,
            "StashSection",
            new Vector2(0f, 0f),
            new Vector2(StashWidthRatio, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.085f, 0.11f, 0.17f, 0.97f));

        Image stashRootImage = stashRoot.GetComponent<Image>();
        if (stashRootImage != null)
        {
            stashRootImage.raycastTarget = false;
            Debug.Log("[PreCombatUI] Stash background raycast disabled");
        }

        CreateText(
            stashRoot.transform,
            "StashHeader",
            "PARTY STASH",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(10f, -4f),
            new Vector2(-20f, 24f),
            17,
            FontStyle.Bold,
            Color.white,
            TextAnchor.MiddleLeft);

        _filterButton = CreateButton(
            stashRoot.transform,
            "FilterButton",
            "Filter: All",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(10f, -33f),
            new Vector2(150f, 28f),
            new Color(0.2f, 0.32f, 0.56f, 1f),
            OnFilterButtonPressed);
        if (_filterButton != null)
        {
            _filterButton.interactable = true;
            Image btnImage = _filterButton.GetComponent<Image>();
            if (btnImage != null)
                btnImage.raycastTarget = true;

            Debug.Log($"[PreCombatUI] Filter button created: {_filterButton.name}");
            Debug.Log($"[PreCombatUI] Button interactable: {_filterButton.interactable}");
        }

        _sortButton = CreateButton(
            stashRoot.transform,
            "SortButton",
            "Sort: Name",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(166f, -33f),
            new Vector2(140f, 28f),
            new Color(0.16f, 0.4f, 0.56f, 1f),
            OnSortButtonPressed);

        _clearStashButton = CreateButton(
            stashRoot.transform,
            "ClearStashButton",
            "Clear",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(312f, -33f),
            new Vector2(88f, 28f),
            new Color(0.56f, 0.31f, 0.16f, 1f),
            OnClearStashPressed);

        _stashInfoText = CreateText(
            stashRoot.transform,
            "StashInfo",
            "0 items",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-10f, -33f),
            new Vector2(300f, 28f),
            12,
            FontStyle.Normal,
            new Color(0.78f, 0.85f, 0.93f),
            TextAnchor.MiddleRight);
        if (_stashInfoText != null)
            _stashInfoText.raycastTarget = false;

        ScrollRect stashScroll = CreateScrollView(
            stashRoot.transform,
            "StashScroll",
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            new Vector2(8f, 8f),
            new Vector2(-8f, -66f),
            out _stashContent,
            out _stashViewportRect,
            withVerticalScrollbar: true);

        _stashGridLayout = _stashContent.gameObject.AddComponent<GridLayoutGroup>();
        _stashGridLayout.cellSize = new Vector2(SlotSize, SlotSize);
        _stashGridLayout.spacing = new Vector2(SlotSpacing, SlotSpacing);
        _stashGridLayout.padding = new RectOffset(6, 6, 6, 6);
        _stashGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _stashGridLayout.constraintCount = 5;
        _stashGridLayout.childAlignment = TextAnchor.UpperLeft;

        ContentSizeFitter fitter = _stashContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (stashScroll != null)
            stashScroll.scrollSensitivity = 24f;

        return stashRoot;
    }

    private void BuildCharacterSelectionSection(Transform parent)
    {
        GameObject section = CreatePanel(
            parent,
            "CharacterSelectionSection",
            new Vector2(0f, 0.78f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(-8f, -8f),
            new Color(0.11f, 0.14f, 0.22f, 0.95f));
        Image sectionImage = section.GetComponent<Image>();
        if (sectionImage != null)
            sectionImage.raycastTarget = false;

        CreateText(
            section.transform,
            "CharacterSelectionHeader",
            "CHARACTER MANAGEMENT",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(10f, -3f),
            new Vector2(-20f, 22f),
            14,
            FontStyle.Bold,
            Color.white,
            TextAnchor.MiddleLeft);

        GameObject row = new GameObject("CharacterTabsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(section.transform, false);

        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 0f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.offsetMin = new Vector2(8f, 6f);
        rowRect.offsetMax = new Vector2(-8f, -26f);

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(2, 2, 2, 2);
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        _characterTabsContent = rowRect;
    }

    private void BuildCharacterDetailSection(Transform parent)
    {
        GameObject detailRoot = CreatePanel(
            parent,
            "CharacterDetailSection",
            new Vector2(0f, 0f),
            new Vector2(1f, 0.76f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(-8f, 0f),
            new Color(0.08f, 0.1f, 0.15f, 0.97f));
        Image detailRootImage = detailRoot.GetComponent<Image>();
        if (detailRootImage != null)
            detailRootImage.raycastTarget = false;

        _characterHeaderText = CreateText(
            detailRoot.transform,
            "CharacterHeader",
            "No character selected",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(10f, -4f),
            new Vector2(-20f, 24f),
            14,
            FontStyle.Bold,
            new Color(0.92f, 0.93f, 0.97f),
            TextAnchor.MiddleLeft);

        GameObject columns = new GameObject("CharacterColumns", typeof(RectTransform));
        columns.transform.SetParent(detailRoot.transform, false);

        RectTransform columnsRect = columns.GetComponent<RectTransform>();
        columnsRect.anchorMin = new Vector2(0f, 0f);
        columnsRect.anchorMax = new Vector2(1f, 1f);
        columnsRect.offsetMin = new Vector2(8f, 8f);
        columnsRect.offsetMax = new Vector2(-8f, -30f);

        GameObject equipmentPanel = CreatePanel(
            columns.transform,
            "EquipmentColumn",
            new Vector2(0f, 0f),
            new Vector2(0.43f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.11f, 0.13f, 0.2f, 0.98f));
        Image equipmentPanelImage = equipmentPanel.GetComponent<Image>();
        if (equipmentPanelImage != null)
            equipmentPanelImage.raycastTarget = false;

        CreateText(
            equipmentPanel.transform,
            "EquipmentHeader",
            "EQUIPMENT",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(8f, -2f),
            new Vector2(-16f, 20f),
            12,
            FontStyle.Bold,
            new Color(0.95f, 0.85f, 0.52f),
            TextAnchor.MiddleLeft);

        ScrollRect equipmentScroll = CreateScrollView(
            equipmentPanel.transform,
            "EquipmentScroll",
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            new Vector2(6f, 6f),
            new Vector2(-6f, -24f),
            out _equipmentContent,
            out _equipmentViewportRect,
            withVerticalScrollbar: true);

        _equipmentGridLayout = _equipmentContent.gameObject.AddComponent<GridLayoutGroup>();
        _equipmentGridLayout.cellSize = new Vector2(EquipmentCellWidth, EquipmentCellHeight);
        _equipmentGridLayout.spacing = new Vector2(EquipmentSpacingX, EquipmentSpacingY);
        _equipmentGridLayout.padding = new RectOffset(0, 0, 0, 0);
        _equipmentGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _equipmentGridLayout.constraintCount = EquipmentGridPreferredColumns;
        _equipmentGridLayout.childAlignment = TextAnchor.UpperLeft;

        ContentSizeFitter equipFitter = _equipmentContent.gameObject.AddComponent<ContentSizeFitter>();
        equipFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        equipFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Debug.Log($"[PreCombatUI] Equipment grid configured to mirror normal InventoryUI: cell={EquipmentCellWidth:0}x{EquipmentCellHeight:0}, spacing={EquipmentSpacingX:0}x{EquipmentSpacingY:0}, preferredColumns={EquipmentGridPreferredColumns}");

        if (equipmentScroll != null)
            equipmentScroll.scrollSensitivity = 20f;

        GameObject inventoryPanel = CreatePanel(
            columns.transform,
            "InventoryColumn",
            new Vector2(0.44f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.1f, 0.12f, 0.18f, 0.98f));
        Image inventoryPanelImage = inventoryPanel.GetComponent<Image>();
        if (inventoryPanelImage != null)
            inventoryPanelImage.raycastTarget = false;

        CreateText(
            inventoryPanel.transform,
            "InventoryHeader",
            "BACKPACK",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(8f, -2f),
            new Vector2(-16f, 20f),
            12,
            FontStyle.Bold,
            new Color(0.95f, 0.85f, 0.52f),
            TextAnchor.MiddleLeft);

        ScrollRect inventoryScroll = CreateScrollView(
            inventoryPanel.transform,
            "InventoryScroll",
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            new Vector2(6f, 6f),
            new Vector2(-6f, -24f),
            out _inventoryContent,
            out _,
            withVerticalScrollbar: true);

        _inventoryGridLayout = _inventoryContent.gameObject.AddComponent<GridLayoutGroup>();
        _inventoryGridLayout.cellSize = new Vector2(SlotSize, SlotSize);
        _inventoryGridLayout.spacing = new Vector2(SlotSpacing, SlotSpacing);
        _inventoryGridLayout.padding = new RectOffset(6, 6, 6, 6);
        _inventoryGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _inventoryGridLayout.constraintCount = InventoryGridColumns;
        _inventoryGridLayout.childAlignment = TextAnchor.UpperLeft;

        ContentSizeFitter invFitter = _inventoryContent.gameObject.AddComponent<ContentSizeFitter>();
        invFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        invFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Debug.Log($"[Inventory] Grid configured: {_inventoryGridLayout.cellSize.x:0}x{_inventoryGridLayout.cellSize.y:0} cells, columns={InventoryGridColumns}");

        if (inventoryScroll != null)
        {
            inventoryScroll.vertical = true;
            inventoryScroll.horizontal = false;
            inventoryScroll.scrollSensitivity = 20f;
        }
    }

    private void BuildFooter()
    {
        _messageText = CreateText(
            _panel.transform,
            "MessageText",
            string.Empty,
            new Vector2(0.02f, 0.08f),
            new Vector2(0.98f, 0.12f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            12,
            FontStyle.Normal,
            new Color(0.88f, 0.77f, 0.44f),
            TextAnchor.MiddleCenter);

        GameObject footer = new GameObject("FooterButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        footer.transform.SetParent(_panel.transform, false);

        RectTransform footerRect = footer.GetComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0.06f, 0.02f);
        footerRect.anchorMax = new Vector2(0.94f, 0.07f);
        footerRect.offsetMin = Vector2.zero;
        footerRect.offsetMax = Vector2.zero;

        HorizontalLayoutGroup footerLayout = footer.GetComponent<HorizontalLayoutGroup>();
        footerLayout.spacing = 12f;
        footerLayout.childAlignment = TextAnchor.MiddleCenter;
        footerLayout.childControlWidth = true;
        footerLayout.childControlHeight = true;
        footerLayout.childForceExpandWidth = true;
        footerLayout.childForceExpandHeight = true;

        _backButton = CreateFooterButton(footer.transform, "Back", new Color(0.45f, 0.22f, 0.22f), OnBackPressed);
        _skipButton = CreateFooterButton(footer.transform, "Skip Inventory", new Color(0.22f, 0.34f, 0.58f), OnSkipPressed);
        _beginCombatButton = CreateFooterButton(footer.transform, "Begin Combat", new Color(0.2f, 0.52f, 0.29f), OnBeginCombatPressed);
    }

    private void BuildTooltip()
    {
        _tooltipPanel = CreatePanel(
            _panel.transform,
            "TooltipPanel",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 1f),
            Vector2.zero,
            new Vector2(330f, 190f),
            new Color(0.03f, 0.03f, 0.07f, 0.96f));

        Image tooltipBackground = _tooltipPanel.GetComponent<Image>();
        if (tooltipBackground != null)
            tooltipBackground.raycastTarget = false;

        Outline outline = _tooltipPanel.AddComponent<Outline>();
        outline.effectDistance = new Vector2(1f, -1f);
        outline.effectColor = new Color(0.86f, 0.78f, 0.42f, 0.56f);

        _tooltipText = CreateText(
            _tooltipPanel.transform,
            "TooltipText",
            string.Empty,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(-12f, -12f),
            11,
            FontStyle.Normal,
            new Color(0.9f, 0.92f, 0.98f),
            TextAnchor.UpperLeft);
        _tooltipText.verticalOverflow = VerticalWrapMode.Overflow;
        _tooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _tooltipText.raycastTarget = false;

        _tooltipPanel.SetActive(false);
        _tooltipActive = false;
    }

    private void BuildContextMenu()
    {
        _contextMenuRoot = CreatePanel(
            _panel.transform,
            "ContextMenu",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 1f),
            Vector2.zero,
            new Vector2(170f, 112f),
            new Color(0.07f, 0.09f, 0.14f, 0.98f));

        Outline outline = _contextMenuRoot.AddComponent<Outline>();
        outline.effectDistance = new Vector2(1f, -1f);
        outline.effectColor = new Color(0.94f, 0.84f, 0.42f, 0.45f);

        _contextPrimaryButton = CreateButton(
            _contextMenuRoot.transform,
            "ContextPrimary",
            "Action",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -6f),
            new Vector2(-10f, 28f),
            new Color(0.24f, 0.39f, 0.62f, 1f),
            null);

        _contextSecondaryButton = CreateButton(
            _contextMenuRoot.transform,
            "ContextSecondary",
            "Action",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -39f),
            new Vector2(-10f, 28f),
            new Color(0.18f, 0.36f, 0.54f, 1f),
            null);

        _contextTertiaryButton = CreateButton(
            _contextMenuRoot.transform,
            "ContextTertiary",
            "Action",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -72f),
            new Vector2(-10f, 28f),
            new Color(0.56f, 0.24f, 0.21f, 1f),
            null);

        _contextMenuRoot.SetActive(false);
    }

    private void BuildDragPreview()
    {
        _dragPreview = CreatePanel(
            _panel.transform,
            "DragPreview",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(120f, 44f),
            new Color(0.17f, 0.23f, 0.39f, 0.82f));

        CanvasGroup cg = _dragPreview.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        _dragPreviewText = CreateText(
            _dragPreview.transform,
            "DragPreviewText",
            string.Empty,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(-8f, -8f),
            11,
            FontStyle.Bold,
            Color.white,
            TextAnchor.MiddleCenter);

        _dragPreview.SetActive(false);
    }

    private void OnRectTransformDimensionsChange()
    {
        if (_panel == null)
            return;

        if (_resizableWindow != null)
        {
            _resizableWindow.MaxSize = new Vector2(
                Mathf.Max(_resizableWindow.MinSize.x, Screen.width - 100f),
                Mathf.Max(_resizableWindow.MinSize.y, Screen.height - 100f));
        }

        ReflowResponsiveLayout();
    }

    private void ReflowResponsiveLayout()
    {
        ReflowStashGridColumns();
        ReflowEquipmentGridColumns();

        if (_equipmentContent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(_equipmentContent);

        if (_inventoryContent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(_inventoryContent);

        Debug.Log("[Layout] Reflow complete for stash/equipment/inventory sections.");
    }

    private void ReflowStashGridColumns()
    {
        if (_stashGridLayout == null)
            return;

        float viewportWidth = _stashViewportRect != null && _stashViewportRect.rect.width > 0f
            ? _stashViewportRect.rect.width
            : (_panelRect != null ? _panelRect.rect.width * StashWidthRatio : 720f);

        float cellPlusSpacing = SlotSize + SlotSpacing;
        int columns = Mathf.FloorToInt((viewportWidth + SlotSpacing) / Mathf.Max(1f, cellPlusSpacing));
        columns = Mathf.Clamp(columns, MinStashColumns, MaxStashColumns);

        _stashGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _stashGridLayout.constraintCount = columns;

        if (_lastLoggedStashColumns != columns)
        {
            float panelWidth = _panelRect != null ? _panelRect.rect.width : 0f;
            float characterWidth = Mathf.Max(0f, panelWidth * (1f - CharacterSectionStartRatio));
            Debug.Log($"[PreCombatUI] Stash width: {viewportWidth:0}px | Columns: {columns}");
            Debug.Log($"[PreCombatUI] Character width: {characterWidth:0}px");
            _lastLoggedStashColumns = columns;
        }
    }

    private void ReflowEquipmentGridColumns()
    {
        if (_equipmentGridLayout == null)
            return;

        float viewportWidth = _equipmentViewportRect != null && _equipmentViewportRect.rect.width > 0f
            ? _equipmentViewportRect.rect.width
            : (_panelRect != null ? _panelRect.rect.width * 0.42f : 460f);

        float cellPlusSpacing = EquipmentCellWidth + EquipmentSpacingX;
        int responsiveColumns = Mathf.FloorToInt((viewportWidth + EquipmentSpacingX) / Mathf.Max(1f, cellPlusSpacing));
        int columns = Mathf.Clamp(responsiveColumns, 1, EquipmentGridPreferredColumns);

        _equipmentGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _equipmentGridLayout.constraintCount = columns;

        if (_lastLoggedEquipmentColumns != columns)
        {
            Debug.Log($"[PreCombatUI] Equipment layout modeled after normal inventory");
            Debug.Log($"[PreCombatUI] Cell size: {EquipmentCellWidth:0}x{EquipmentCellHeight:0} (matches InventoryUI)");
            Debug.Log($"[PreCombatUI] Spacing: {EquipmentSpacingX:0}x{EquipmentSpacingY:0} (matches InventoryUI)");
            Debug.Log($"[PreCombatUI] Columns: {columns} (responsive, max {EquipmentGridPreferredColumns} from InventoryUI)");
            Debug.Log("[PreCombatUI] Visual consistency achieved");
            _lastLoggedEquipmentColumns = columns;
        }
    }

    private void RefreshAll()
    {
        _dropTargets.Clear();
        HideContextMenu();
        ReflowResponsiveLayout();
        RefreshStatusAndHeaderText();
        RefreshCharacterTabs();
        RefreshStashGrid();
        RefreshCharacterDetail();
    }

    private void RefreshStatusAndHeaderText()
    {
        bool stashLocked = _stash != null && _stash.IsLocked;

        if (_stashStatusText != null)
        {
            _stashStatusText.text = stashLocked ? "Stash: Locked (Combat Active)" : "Stash: Unlocked";
            _stashStatusText.color = stashLocked
                ? new Color(0.96f, 0.56f, 0.44f)
                : new Color(0.56f, 0.92f, 0.64f);
        }

        if (_stashInfoText != null)
        {
            int total = _stash != null ? _stash.Count : 0;
            _stashInfoText.text = stashLocked
                ? $"{total} items • LOCKED"
                : $"{total} items • Scrollable stash";
        }

        if (_filterButton != null)
            _filterButton.GetComponentInChildren<Text>().text = $"Filter: {GetFilterLabel(_stashFilter)}";
        if (_sortButton != null)
            _sortButton.GetComponentInChildren<Text>().text = $"Sort: {GetSortLabel(_stashSort)}";

        if (_clearStashButton != null)
            _clearStashButton.interactable = _stash != null && !_stash.IsLocked && _stash.Count > 0;

        CharacterController selected = GetSelectedCharacter();
        if (_characterHeaderText != null)
        {
            if (selected == null || selected.Stats == null)
            {
                _characterHeaderText.text = "No character selected";
            }
            else
            {
                CharacterStats stats = selected.Stats;
                int hp = stats.CurrentHP;
                int maxHp = Mathf.Max(1, stats.MaxHP);
                int ac = stats.ArmorClass;
                _characterHeaderText.text =
                    $"{stats.CharacterName}  •  {stats.CharacterClass} Lv {Mathf.Max(1, stats.Level)}  •  HP {hp}/{maxHp}  •  AC {ac}";
            }
        }
    }

    private void RefreshCharacterTabs()
    {
        _characterTabButtons.Clear();
        _characterTabMap.Clear();

        if (_characterTabsContent == null)
            return;

        for (int i = _characterTabsContent.childCount - 1; i >= 0; i--)
            Destroy(_characterTabsContent.GetChild(i).gameObject);

        for (int i = 0; i < _partyMembers.Count; i++)
        {
            CharacterController ch = _partyMembers[i];
            if (ch == null || ch.Stats == null)
                continue;

            int captured = i;
            Button tab = CreateCharacterTabButton(_characterTabsContent, ch, captured);
            _characterTabButtons.Add(tab);
            _characterTabMap[tab] = ch;
        }

        if (_partyMembers.Count == 0)
            _selectedCharacterIndex = -1;
        else
            _selectedCharacterIndex = Mathf.Clamp(_selectedCharacterIndex, 0, _partyMembers.Count - 1);

        for (int i = 0; i < _characterTabButtons.Count; i++)
        {
            CharacterController character = _characterTabMap[_characterTabButtons[i]];
            bool selected = _partyMembers.IndexOf(character) == _selectedCharacterIndex;
            ApplyCharacterTabStyle(_characterTabButtons[i], selected);
        }
    }

    private Button CreateCharacterTabButton(Transform parent, CharacterController character, int index)
    {
        GameObject tab = new GameObject($"CharacterTab_{index}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        tab.transform.SetParent(parent, false);

        LayoutElement le = tab.GetComponent<LayoutElement>();
        le.preferredWidth = 142f;
        le.minWidth = 120f;

        Image image = tab.GetComponent<Image>();
        image.color = new Color(0.15f, 0.19f, 0.29f, 1f);

        Button btn = tab.GetComponent<Button>();
        btn.targetGraphic = image;
        btn.onClick.AddListener(() =>
        {
            _selectedCharacterIndex = index;
            RefreshAll();
        });

        CreateText(
            tab.transform,
            "Portrait",
            GetClassIcon(character),
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(6f, 0f),
            new Vector2(26f, 0f),
            16,
            FontStyle.Bold,
            new Color(0.95f, 0.93f, 0.72f),
            TextAnchor.MiddleCenter);

        CreateText(
            tab.transform,
            "Name",
            character.Stats.CharacterName,
            new Vector2(0f, 0.5f),
            new Vector2(1f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(34f, 0f),
            new Vector2(-38f, 0f),
            11,
            FontStyle.Bold,
            Color.white,
            TextAnchor.MiddleLeft);

        CreateText(
            tab.transform,
            "Class",
            character.Stats.CharacterClass,
            new Vector2(0f, 0f),
            new Vector2(1f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(34f, 0f),
            new Vector2(-38f, 0f),
            10,
            FontStyle.Normal,
            new Color(0.77f, 0.84f, 0.94f),
            TextAnchor.MiddleLeft);

        return btn;
    }

    private void ApplyCharacterTabStyle(Button tab, bool selected)
    {
        if (tab == null)
            return;

        Image image = tab.GetComponent<Image>();
        if (image != null)
            image.color = selected
                ? new Color(0.22f, 0.29f, 0.46f, 1f)
                : new Color(0.15f, 0.19f, 0.29f, 1f);

        Outline outline = tab.GetComponent<Outline>();
        if (outline == null)
            outline = tab.gameObject.AddComponent<Outline>();

        outline.effectDistance = new Vector2(1f, -1f);
        outline.effectColor = selected
            ? new Color(0.94f, 0.84f, 0.42f, 0.95f)
            : new Color(0.3f, 0.36f, 0.56f, 0.85f);

        StopCoroutineSafe(tab.gameObject, "TabPulse");
        if (selected)
            StartCoroutine(TabPulse(tab.transform as RectTransform));
    }

    private IEnumerator TabPulse(RectTransform rt)
    {
        if (rt == null)
            yield break;

        Vector3 start = Vector3.one;
        Vector3 peak = new Vector3(1.03f, 1.03f, 1f);

        float t = 0f;
        while (t < 0.08f)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / 0.08f);
            rt.localScale = Vector3.Lerp(start, peak, p);
            yield return null;
        }

        t = 0f;
        while (t < 0.08f)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / 0.08f);
            rt.localScale = Vector3.Lerp(peak, start, p);
            yield return null;
        }

        rt.localScale = Vector3.one;
    }

    private void StopCoroutineSafe(GameObject go, string coroutineName)
    {
        if (go == null || !isActiveAndEnabled)
            return;

        StopCoroutine(coroutineName);
    }

    private void RefreshStashGrid()
    {
        if (_stashContent == null)
            return;

        for (int i = _stashContent.childCount - 1; i >= 0; i--)
            Destroy(_stashContent.GetChild(i).gameObject);

        if (_stash == null)
            return;

        List<ItemStackGroup> groups = BuildStashGroups();
        ApplyStashSort(groups);

        if (groups.Count == 0)
        {
            CreateInfoLabel(_stashContent, "No stash items match current filter.");
            return;
        }

        foreach (ItemStackGroup group in groups)
        {
            SlotRef slot = new SlotRef
            {
                Container = SlotContainerType.Stash,
                ItemGroup = group
            };

            CreateItemSlotVisual(_stashContent, slot, group.Prototype, group.Quantity, $"x{group.Quantity}");
        }
    }

    private void RefreshCharacterDetail()
    {
        if (_equipmentContent == null || _inventoryContent == null)
            return;

        for (int i = _equipmentContent.childCount - 1; i >= 0; i--)
            Destroy(_equipmentContent.GetChild(i).gameObject);
        for (int i = _inventoryContent.childCount - 1; i >= 0; i--)
            Destroy(_inventoryContent.GetChild(i).gameObject);

        CharacterController character = GetSelectedCharacter();
        Inventory inv = GetInventory(character);

        if (character == null || inv == null)
        {
            CreateInfoLabel(_equipmentContent, "Select a character to view equipment.");
            CreateInfoLabel(_inventoryContent, "Select a character to view inventory.");
            return;
        }

        BuildEquipmentSlots(character, inv);
        BuildInventorySlots(character, inv);
    }

    private void BuildEquipmentSlots(CharacterController character, Inventory inv)
    {
        // Slot order intentionally matches InventoryUI.VisibleEquipSlots ordering.
        EquipSlot[] displayOrder =
        {
            EquipSlot.Head,
            EquipSlot.FaceEyes,
            EquipSlot.Neck,
            EquipSlot.Torso,
            EquipSlot.ArmorRobe,
            EquipSlot.Waist,
            EquipSlot.Back,
            EquipSlot.Wrists,
            EquipSlot.Hands,
            EquipSlot.LeftRing,
            EquipSlot.RightRing,
            EquipSlot.Feet,
            EquipSlot.LeftHand,
            EquipSlot.RightHand
        };

        for (int i = 0; i < displayOrder.Length; i++)
        {
            EquipSlot slot = displayOrder[i];
            ItemData item = inv.GetEquipped(slot);
            CreateEquipmentSlotVisual(character, slot, item);
        }

        Debug.Log($"[PreCombatUI] Built equipment slots using normal inventory style: count={displayOrder.Length}, cell={EquipmentCellWidth:0}x{EquipmentCellHeight:0}, spacing={EquipmentSpacingX:0}x{EquipmentSpacingY:0}");
    }

    // Equipment slot visuals intentionally mirror InventoryUI.CreateEquipSlot() styling.
    private void CreateEquipmentSlotVisual(CharacterController character, EquipSlot slot, ItemData item)
    {
        SlotRef slotRef = new SlotRef
        {
            Container = SlotContainerType.Equipment,
            Character = character,
            EquipSlot = slot
        };

        GameObject slotGO = CreatePanel(
            _equipmentContent,
            $"Equip_{slot}",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            Vector2.zero,
            new Vector2(EquipmentCellWidth, EquipmentCellHeight),
            item != null ? EquipmentSlotEquippedColor : EquipmentSlotEmptyColor);

        CreateText(
            slotGO.transform,
            "SlotLabel",
            GetNormalInventoryEquipSlotLabel(slot),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -2f),
            new Vector2(0f, 16f),
            9,
            FontStyle.Normal,
            new Color(0.7f, 0.7f, 0.5f),
            TextAnchor.MiddleCenter);

        string itemTextValue = item != null
            ? (string.IsNullOrEmpty(item.IconChar) ? item.Name : $"{item.IconChar}\n{item.Name}")
            : "Empty";

        Text itemText = CreateText(
            slotGO.transform,
            "ItemText",
            itemTextValue,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -6f),
            new Vector2(-4f, -18f),
            11,
            FontStyle.Normal,
            item != null ? item.IconColor : new Color(0.4f, 0.4f, 0.4f),
            TextAnchor.MiddleCenter);

        if (itemText != null)
        {
            itemText.horizontalOverflow = HorizontalWrapMode.Overflow;
            itemText.verticalOverflow = VerticalWrapMode.Overflow;
        }

        Image background = slotGO.GetComponent<Image>();
        if (background != null)
            background.raycastTarget = true;

        DraggableItem draggable = slotGO.AddComponent<DraggableItem>();
        draggable.Init(this, () => slotRef);

        DropTarget target = slotGO.AddComponent<DropTarget>();
        target.Init(this, () => slotRef, background, background != null ? background.color : EquipmentSlotEmptyColor);
        _dropTargets.Add(target);
    }

    private void BuildInventorySlots(CharacterController character, Inventory inv)
    {
        List<ItemStackGroup> groupedInventory = BuildInventoryGroups(inv);
        Debug.Log($"[Inventory] Building backpack grid for {character.Stats.CharacterName}: {groupedInventory.Count} stack(s), no empty placeholder slots.");

        foreach (ItemStackGroup group in groupedInventory)
        {
            SlotRef stackSlot = new SlotRef
            {
                Container = SlotContainerType.InventoryStack,
                Character = character,
                ItemGroup = group
            };

            CreateItemSlotVisual(
                _inventoryContent,
                stackSlot,
                group.Prototype,
                quantity: group.Quantity,
                quantityLabel: $"x{group.Quantity}");
        }

        if (groupedInventory.Count == 0)
            CreateInfoLabel(_inventoryContent, "Backpack is empty.");
    }

    private GameObject CreateItemSlotVisual(
        Transform parent,
        SlotRef slot,
        ItemData item,
        int quantity,
        string quantityLabel,
        Rect? anchoredOverride = null,
        string slotPlaceholder = "")
    {
        GameObject slotGO = new GameObject("ItemSlot", typeof(RectTransform), typeof(Image));
        slotGO.transform.SetParent(parent, false);

        RectTransform slotRt = slotGO.GetComponent<RectTransform>();
        if (anchoredOverride.HasValue)
        {
            Rect rect = anchoredOverride.Value;
            slotRt.anchorMin = new Vector2(0f, 0f);
            slotRt.anchorMax = new Vector2(0f, 1f);
            slotRt.pivot = new Vector2(0f, 0.5f);
            slotRt.anchoredPosition = new Vector2(rect.x, rect.y);
            slotRt.sizeDelta = new Vector2(rect.width, rect.height);
        }
        else
        {
            slotRt.sizeDelta = new Vector2(SlotSize, SlotSize);
        }

        Image background = slotGO.GetComponent<Image>();
        Color slotColor = item != null
            ? GetRarityTint(item)
            : new Color(0.16f, 0.18f, 0.26f, 0.95f);
        background.color = slotColor;
        background.raycastTarget = true;

        Outline outline = slotGO.AddComponent<Outline>();
        outline.effectDistance = new Vector2(1f, -1f);
        outline.effectColor = new Color(0f, 0f, 0f, 0.4f);

        Text iconText = CreateText(
            slotGO.transform,
            "Icon",
            item != null ? GetItemIcon(item) : slotPlaceholder,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 4f),
            new Vector2(0f, -16f),
            16,
            FontStyle.Bold,
            item != null ? Color.white : new Color(0.54f, 0.6f, 0.72f),
            TextAnchor.MiddleCenter);
        if (iconText != null)
            iconText.raycastTarget = false;

        Text nameText = CreateText(
            slotGO.transform,
            "Name",
            item != null ? BuildCompactItemName(item) : string.Empty,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 1f),
            new Vector2(-4f, 14f),
            8,
            FontStyle.Normal,
            new Color(0.92f, 0.94f, 0.98f),
            TextAnchor.LowerCenter);
        if (nameText != null)
            nameText.raycastTarget = false;

        if (item != null && quantity > 1)
        {
            GameObject badge = CreatePanel(
                slotGO.transform,
                "QtyBadge",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-3f, -3f),
                new Vector2(20f, 14f),
                new Color(0.12f, 0.14f, 0.24f, 0.92f));

            Image badgeImage = badge.GetComponent<Image>();
            if (badgeImage != null)
                badgeImage.raycastTarget = false;

            Outline badgeOutline = badge.AddComponent<Outline>();
            badgeOutline.effectDistance = new Vector2(1f, -1f);
            badgeOutline.effectColor = new Color(0f, 0f, 0f, 0.6f);

            Text qtyText = CreateText(
                badge.transform,
                "QtyText",
                string.IsNullOrWhiteSpace(quantityLabel) ? quantity.ToString() : quantityLabel,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                9,
                FontStyle.Bold,
                Color.white,
                TextAnchor.MiddleCenter);
            if (qtyText != null)
                qtyText.raycastTarget = false;
        }

        DraggableItem draggable = slotGO.AddComponent<DraggableItem>();
        draggable.Init(this, () => slot);

        DropTarget target = slotGO.AddComponent<DropTarget>();
        target.Init(this, () => slot, background, slotColor);
        _dropTargets.Add(target);

        return slotGO;
    }

    private void TryBeginDrag(DraggableItem draggable)
    {
        _dragDropManager?.BeginDrag(draggable);
    }

    private void OnDragStarted(DragDropManager manager)
    {
        if (manager == null || manager.DragItem == null)
            return;

        HideContextMenu();
        ShowDragPreview(manager.DragItem);
        UpdateDropTargetHighlights();
    }

    private void OnDragUpdated(DragDropManager manager, PointerEventData eventData)
    {
        if (manager == null || manager.DragItem == null)
            return;

        MoveDragPreview(eventData != null ? eventData.position : (Vector2)Input.mousePosition);
    }

    private void OnDragEnded(DragDropManager manager, PointerEventData eventData)
    {
        ClearDropTargetHighlights();
        HideDragPreview();
    }

    private void OnDropTargetHovered(DropTarget target)
    {
        if (_dragDropManager == null || _dragDropManager.DragItem == null || target == null)
            return;

        DropValidationResult result = ValidateDrop(_dragDropManager.DragSource, target.Slot, _dragDropManager.DragItem, showWarnings: false);
        target.SetHighlightState(true, result.IsValid);
        if (!string.IsNullOrWhiteSpace(result.Message) && !result.IsValid)
            ShowMessage(result.Message, false);
    }

    private void OnDropTargetUnhovered(DropTarget target)
    {
        if (target == null)
            return;

        UpdateDropTargetHighlights();
    }

    private void UpdateDropTargetHighlights()
    {
        if (_dragDropManager == null || _dragDropManager.DragItem == null)
        {
            ClearDropTargetHighlights();
            return;
        }

        for (int i = 0; i < _dropTargets.Count; i++)
        {
            DropTarget target = _dropTargets[i];
            if (target == null)
                continue;

            DropValidationResult result = ValidateDrop(_dragDropManager.DragSource, target.Slot, _dragDropManager.DragItem, showWarnings: false);
            target.SetHighlightState(true, result.IsValid);
        }
    }

    private void ClearDropTargetHighlights()
    {
        for (int i = 0; i < _dropTargets.Count; i++)
        {
            if (_dropTargets[i] != null)
                _dropTargets[i].ResetVisual();
        }
    }

    private void HandleLeftClickTransfer(SlotRef slot, ItemData item)
    {
        if (slot == null || item == null)
            return;

        if (slot.Container == SlotContainerType.Stash)
        {
            TransferStackFromStashToSelectedCharacter(slot.ItemGroup);
            return;
        }

        if (slot.Container == SlotContainerType.InventoryStack)
        {
            TransferStackFromInventoryToStash(slot.Character, slot.ItemGroup);
        }
    }

    private void TransferStackFromStashToSelectedCharacter(ItemStackGroup stack)
    {
        if (stack == null || stack.Quantity <= 0)
            return;

        CharacterController selected = GetSelectedCharacter();
        if (selected == null)
        {
            ShowMessage("Select a character first.", false);
            return;
        }

        if (_stash == null || _stash.IsLocked)
        {
            ShowMessage("Stash is locked.", false);
            return;
        }

        Inventory inv = GetInventory(selected);
        if (inv == null)
        {
            ShowMessage("Selected character has no inventory.", false);
            return;
        }

        int moved = 0;
        List<ItemData> toMove = new List<ItemData>(stack.Instances);
        foreach (ItemData stackItem in toMove)
        {
            if (stackItem == null)
                continue;

            if (!_stash.RemoveItem(stackItem))
                continue;

            if (!inv.AddItem(stackItem))
            {
                _stash.AddItem(stackItem);
                break;
            }

            moved++;
        }

        string stackName = stack.Prototype != null ? stack.Prototype.Name : "item";
        Debug.Log($"[Transfer] Stash -> {selected.Stats.CharacterName} | {stackName} x{moved}");
        ShowMessage(moved > 0
            ? $"Moved {moved}× {stackName} to {selected.Stats.CharacterName}."
            : "Backpack is full.",
            moved > 0);
        RefreshAll();
    }

    private void TransferStackFromInventoryToStash(CharacterController owner, ItemStackGroup stack)
    {
        if (owner == null || stack == null || stack.Quantity <= 0)
            return;

        if (_stash == null || _stash.IsLocked)
        {
            ShowMessage("Stash is locked.", false);
            return;
        }

        Inventory inv = GetInventory(owner);
        if (inv == null)
        {
            ShowMessage("Inventory unavailable.", false);
            return;
        }

        int moved = 0;
        List<ItemData> toMove = new List<ItemData>(stack.Instances);
        foreach (ItemData stackItem in toMove)
        {
            if (stackItem == null)
                continue;

            if (!inv.RemoveItem(stackItem))
                continue;

            if (!_stash.AddItem(stackItem))
            {
                inv.AddItem(stackItem);
                break;
            }

            moved++;
        }

        string stackName = stack.Prototype != null ? stack.Prototype.Name : "item";
        Debug.Log($"[Transfer] {owner.Stats.CharacterName} -> Stash | {stackName} x{moved}");
        ShowMessage(moved > 0
            ? $"Moved {moved}× {stackName} to stash."
            : "Transfer failed.",
            moved > 0);
        RefreshAll();
    }

    private void OnItemDroppedOnTarget(DropTarget target)
    {
        if (_dragDropManager == null || _dragDropManager.DragItem == null || target == null)
            return;

        Debug.Log($"[PreCombatUI] Drop attempt | item={_dragDropManager.DragItem.Name} | target={target.name}");

        SlotRef source = _dragDropManager.DragSource;
        SlotRef destination = target.Slot;
        ItemData item = _dragDropManager.DragItem;

        DropValidationResult validation = ValidateDrop(source, destination, item, showWarnings: true);
        if (!validation.IsValid)
        {
            ShowMessage(validation.Message, false);
            return;
        }

        if (TryExecuteTransfer(source, destination, item, out string feedback))
            ShowMessage(string.IsNullOrWhiteSpace(validation.Message) ? feedback : validation.Message, true);
        else
            ShowMessage(string.IsNullOrWhiteSpace(feedback) ? "Transfer failed." : feedback, false);

        RefreshAll();
    }

    private bool TryExecuteTransfer(SlotRef source, SlotRef destination, ItemData dragItem, out string feedback)
    {
        feedback = "";

        if (source == null || destination == null || dragItem == null)
        {
            feedback = "Invalid transfer state.";
            return false;
        }

        if (source.IsSameLocation(destination))
        {
            feedback = "Item is already in this slot.";
            return false;
        }

        if (destination.Container == SlotContainerType.Stash)
            return MoveItemToStash(source, dragItem, out feedback);

        if (destination.Container == SlotContainerType.Inventory || destination.Container == SlotContainerType.InventoryStack)
            return MoveItemToInventorySlot(source, destination, dragItem, out feedback);

        if (destination.Container == SlotContainerType.Equipment)
            return MoveItemToEquipmentSlot(source, destination, dragItem, out feedback);

        feedback = "Unsupported destination.";
        return false;
    }

    private bool MoveItemToStash(SlotRef source, ItemData item, out string feedback)
    {
        feedback = "";

        if (_stash == null)
        {
            feedback = "Stash unavailable.";
            return false;
        }

        if (_stash.IsLocked)
        {
            feedback = "Stash is locked.";
            return false;
        }

        switch (source.Container)
        {
            case SlotContainerType.Inventory:
            {
                Inventory inv = GetInventory(source.Character);
                if (inv == null || source.InventoryIndex < 0 || source.InventoryIndex >= inv.GeneralSlots.Length)
                {
                    feedback = "Invalid source inventory slot.";
                    return false;
                }

                ItemData removed = inv.RemoveItemAt(source.InventoryIndex);
                if (removed == null)
                {
                    feedback = "No item in source slot.";
                    return false;
                }

                if (!_stash.AddItem(removed))
                {
                    inv.GeneralSlots[source.InventoryIndex] = removed;
                    inv.RecalculateStats();
                    feedback = "Failed to move item to stash.";
                    return false;
                }

                feedback = $"Moved {removed.Name} to stash.";
                return true;
            }

            case SlotContainerType.InventoryStack:
            {
                Inventory inv = GetInventory(source.Character);
                if (inv == null || !inv.RemoveItem(item))
                {
                    feedback = "Failed to remove item from backpack stack.";
                    return false;
                }

                if (!_stash.AddItem(item))
                {
                    inv.AddItem(item);
                    feedback = "Failed to move item to stash.";
                    return false;
                }

                feedback = $"Moved {item.Name} to stash.";
                return true;
            }

            case SlotContainerType.Equipment:
            {
                Inventory inv = GetInventory(source.Character);
                if (inv == null)
                {
                    feedback = "Character inventory unavailable.";
                    return false;
                }

                if (!inv.Unequip(source.EquipSlot))
                {
                    feedback = "Cannot unequip item (backpack full).";
                    return false;
                }

                int movedIndex = FindFirstIndexOfReference(inv.GeneralSlots, item);
                if (movedIndex < 0)
                {
                    feedback = "Failed to locate unequipped item.";
                    return false;
                }

                ItemData removed = inv.RemoveItemAt(movedIndex);
                if (removed == null || !_stash.AddItem(removed))
                {
                    if (removed != null)
                        inv.AddItem(removed);
                    feedback = "Failed to move item to stash.";
                    return false;
                }

                feedback = $"Moved {item.Name} to stash.";
                return true;
            }

            default:
                feedback = "Cannot move this source to stash.";
                return false;
        }
    }

    private bool MoveItemToInventorySlot(SlotRef source, SlotRef destination, ItemData item, out string feedback)
    {
        feedback = "";

        Inventory targetInv = GetInventory(destination.Character);
        if (targetInv == null)
        {
            feedback = "Target inventory unavailable.";
            return false;
        }

        bool autoAddTarget = destination.Container == SlotContainerType.InventoryStack;
        int targetIndex = destination.InventoryIndex;
        if (!autoAddTarget && (targetIndex < 0 || targetIndex >= targetInv.GeneralSlots.Length))
        {
            feedback = "Invalid target backpack slot.";
            return false;
        }

        if (source.Container == SlotContainerType.Inventory && source.Character == destination.Character)
        {
            Inventory inv = targetInv;
            if (source.InventoryIndex < 0 || source.InventoryIndex >= inv.GeneralSlots.Length)
            {
                feedback = "Invalid source backpack slot.";
                return false;
            }

            if (autoAddTarget)
            {
                feedback = "Item is already in this backpack.";
                return false;
            }

            ItemData temp = inv.GeneralSlots[targetIndex];
            inv.GeneralSlots[targetIndex] = inv.GeneralSlots[source.InventoryIndex];
            inv.GeneralSlots[source.InventoryIndex] = temp;
            inv.RecalculateStats();
            feedback = "Reordered backpack slots.";
            return true;
        }

        if (source.Container == SlotContainerType.InventoryStack)
        {
            Inventory sourceInv = GetInventory(source.Character);
            if (sourceInv == null || !sourceInv.RemoveItem(item))
            {
                feedback = "Failed to remove item from source stack.";
                return false;
            }

            if (!autoAddTarget)
            {
                if (targetInv.GeneralSlots[targetIndex] != null)
                {
                    sourceInv.AddItem(item);
                    feedback = "Target inventory slot is occupied.";
                    return false;
                }

                targetInv.GeneralSlots[targetIndex] = item;
                targetInv.RecalculateStats();
            }
            else
            {
                if (!targetInv.AddItem(item))
                {
                    sourceInv.AddItem(item);
                    feedback = "Failed to add item to backpack.";
                    return false;
                }
            }

            feedback = $"Moved {item.Name} to backpack.";
            return true;
        }

        if (source.Container == SlotContainerType.Stash)
        {
            if (_stash == null || _stash.IsLocked)
            {
                feedback = "Stash is locked.";
                return false;
            }

            if (!_stash.RemoveItem(item))
            {
                feedback = "Item no longer in stash.";
                return false;
            }

            if (!autoAddTarget)
            {
                if (targetInv.GeneralSlots[targetIndex] != null)
                {
                    _stash.AddItem(item);
                    feedback = "Target inventory slot is occupied.";
                    return false;
                }

                targetInv.GeneralSlots[targetIndex] = item;
                targetInv.RecalculateStats();
            }
            else if (!targetInv.AddItem(item))
            {
                _stash.AddItem(item);
                feedback = "Failed to add item to backpack.";
                return false;
            }

            feedback = $"Moved {item.Name} to {destination.Character.Stats.CharacterName}'s backpack.";
            return true;
        }

        if (source.Container == SlotContainerType.Equipment)
        {
            Inventory sourceInv = GetInventory(source.Character);
            if (sourceInv == null)
            {
                feedback = "Source inventory unavailable.";
                return false;
            }

            if (!sourceInv.Unequip(source.EquipSlot))
            {
                feedback = "Cannot unequip item.";
                return false;
            }

            int sourceIndex = FindFirstIndexOfReference(sourceInv.GeneralSlots, item);
            if (sourceIndex < 0)
            {
                feedback = "Failed to locate unequipped item.";
                return false;
            }

            ItemData extracted = sourceInv.RemoveItemAt(sourceIndex);
            if (extracted == null)
            {
                feedback = "Failed to extract unequipped item.";
                return false;
            }

            if (sourceInv == targetInv && !autoAddTarget)
            {
                if (targetInv.GeneralSlots[targetIndex] != null)
                {
                    sourceInv.AddItem(extracted);
                    feedback = "Target inventory slot is occupied.";
                    return false;
                }

                targetInv.GeneralSlots[targetIndex] = extracted;
                targetInv.RecalculateStats();
                feedback = "Moved equipped item to backpack slot.";
                return true;
            }

            if (!targetInv.AddItem(extracted))
            {
                sourceInv.AddItem(extracted);
                feedback = "Failed to add item to backpack.";
                return false;
            }

            if (!autoAddTarget)
            {
                int addedIndex = FindFirstIndexOfReference(targetInv.GeneralSlots, extracted);
                if (addedIndex >= 0 && addedIndex != targetIndex && targetInv.GeneralSlots[targetIndex] == null)
                {
                    targetInv.GeneralSlots[targetIndex] = targetInv.GeneralSlots[addedIndex];
                    targetInv.GeneralSlots[addedIndex] = null;
                }

                targetInv.RecalculateStats();
            }

            feedback = $"Moved {item.Name} into backpack.";
            return true;
        }

        feedback = "Unsupported move to inventory.";
        return false;
    }

    private bool MoveItemToEquipmentSlot(SlotRef source, SlotRef destination, ItemData item, out string feedback)
    {
        feedback = "";

        Inventory inv = GetInventory(destination.Character);
        if (inv == null)
        {
            feedback = "Target inventory unavailable.";
            return false;
        }

        EquipSlot targetSlot = destination.EquipSlot;

        if (source.Container == SlotContainerType.Inventory && source.Character == destination.Character)
        {
            if (!inv.EquipFromInventory(source.InventoryIndex, targetSlot))
            {
                feedback = "Cannot equip item into that slot.";
                return false;
            }

            feedback = $"Equipped {item.Name} to {GetEquipSlotLabel(targetSlot)}.";
            return true;
        }

        if (source.Container == SlotContainerType.InventoryStack)
        {
            Inventory sourceInv = GetInventory(source.Character);
            if (sourceInv == null || !sourceInv.RemoveItem(item))
            {
                feedback = "Could not remove source item from backpack stack.";
                return false;
            }

            if (sourceInv != inv && !inv.AddItem(item))
            {
                sourceInv.AddItem(item);
                feedback = "Target backpack is full.";
                return false;
            }

            int sourceIndex = FindFirstIndexOfReference(inv.GeneralSlots, item);
            if (sourceIndex < 0 || !inv.EquipFromInventory(sourceIndex, targetSlot))
            {
                inv.RemoveItem(item);
                sourceInv.AddItem(item);
                feedback = "Cannot equip item into that slot.";
                return false;
            }

            feedback = $"Equipped {item.Name} to {GetEquipSlotLabel(targetSlot)}.";
            return true;
        }

        if (source.Container == SlotContainerType.Stash)
        {
            if (_stash == null || _stash.IsLocked)
            {
                feedback = "Stash is locked.";
                return false;
            }

            if (!_stash.RemoveItem(item))
            {
                feedback = "Item no longer in stash.";
                return false;
            }

            if (!inv.AddItem(item))
            {
                _stash.AddItem(item);
                feedback = "Backpack full.";
                return false;
            }

            int idx = FindFirstIndexOfReference(inv.GeneralSlots, item);
            if (idx < 0 || !inv.EquipFromInventory(idx, targetSlot))
            {
                inv.RemoveItem(item);
                _stash.AddItem(item);
                feedback = "Failed to equip item.";
                return false;
            }

            feedback = $"Equipped {item.Name} to {destination.Character.Stats.CharacterName}.";
            return true;
        }

        if (source.Container == SlotContainerType.Equipment && source.Character == destination.Character)
        {
            if (source.EquipSlot == destination.EquipSlot)
            {
                feedback = "Item already in that equipment slot.";
                return false;
            }

            ItemData targetItem = inv.GetEquipped(targetSlot);
            if (!inv.Unequip(source.EquipSlot))
            {
                feedback = "Could not unequip source item.";
                return false;
            }

            int srcIndex = FindFirstIndexOfReference(inv.GeneralSlots, item);
            if (srcIndex < 0)
            {
                feedback = "Failed to locate source item after unequip.";
                return false;
            }

            if (!inv.EquipFromInventory(srcIndex, targetSlot))
            {
                feedback = "Could not equip source item to target slot.";
                return false;
            }

            if (targetItem != null)
            {
                int movedTargetIndex = FindFirstIndexOfReference(inv.GeneralSlots, targetItem);
                if (movedTargetIndex >= 0 && targetItem.CanEquipIn(source.EquipSlot))
                    inv.EquipFromInventory(movedTargetIndex, source.EquipSlot);
            }

            feedback = "Swapped equipment slots.";
            return true;
        }

        feedback = "Unsupported move to equipment slot.";
        return false;
    }

    private DropValidationResult ValidateDrop(SlotRef source, SlotRef destination, ItemData item, bool showWarnings)
    {
        if (source == null || destination == null || item == null)
            return DropValidationResult.Invalid("Invalid drag operation.");

        if (source.IsSameLocation(destination))
            return DropValidationResult.Invalid("Item is already in this slot.");

        if (_stash != null && _stash.IsLocked && (source.Container == SlotContainerType.Stash || destination.Container == SlotContainerType.Stash))
            return DropValidationResult.Invalid("Stash is locked during combat.");

        if (destination.Container == SlotContainerType.Stash)
        {
            if (source.Container == SlotContainerType.Stash)
                return DropValidationResult.Invalid("Stash-to-stash move is not needed.");

            return DropValidationResult.Valid("Moved to stash.");
        }

        if (destination.Container == SlotContainerType.Inventory || destination.Container == SlotContainerType.InventoryStack)
        {
            Inventory targetInv = GetInventory(destination.Character);
            if (targetInv == null)
                return DropValidationResult.Invalid("Target inventory unavailable.");

            bool autoAddTarget = destination.Container == SlotContainerType.InventoryStack;
            if (!autoAddTarget)
            {
                if (destination.InventoryIndex < 0 || destination.InventoryIndex >= targetInv.GeneralSlots.Length)
                    return DropValidationResult.Invalid("Invalid inventory slot.");

                if (targetInv.GeneralSlots[destination.InventoryIndex] != null &&
                    (source.Container == SlotContainerType.Stash || source.Container == SlotContainerType.Equipment || source.Container == SlotContainerType.InventoryStack))
                {
                    return DropValidationResult.Invalid("Backpack slot is occupied.");
                }
            }

            if (source.Container == SlotContainerType.Inventory && source.Character == destination.Character && autoAddTarget)
                return DropValidationResult.Invalid("Item is already in this backpack.");

            return DropValidationResult.Valid(autoAddTarget ? "Added to backpack." : "Moved to backpack.");
        }

        if (destination.Container == SlotContainerType.Equipment)
        {
            Inventory targetInv = GetInventory(destination.Character);
            if (targetInv == null)
                return DropValidationResult.Invalid("Target inventory unavailable.");

            if (!item.CanEquipIn(destination.EquipSlot))
                return DropValidationResult.Invalid($"{item.Name} cannot be equipped in {GetEquipSlotLabel(destination.EquipSlot)}.");

            CharacterStats stats = destination.Character != null ? destination.Character.Stats : null;
            if (stats == null)
                return DropValidationResult.Invalid("Character stats unavailable.");

            DropValidationResult sizeResult = ValidateSizeCompatibility(item, stats);
            if (!sizeResult.IsValid)
                return sizeResult;

            DropValidationResult classResult = ValidateClassRestriction(item, stats);
            if (!classResult.IsValid)
                return classResult;

            bool proficiencyWarn = NeedsProficiencyWarning(item, stats);
            if (proficiencyWarn)
            {
                string warning = "Equipped, but character is not proficient (combat penalties apply).";
                if (showWarnings)
                    return DropValidationResult.ValidWithWarning(warning);
            }

            return DropValidationResult.Valid(proficiencyWarn
                ? "Equipped with non-proficiency warning."
                : "Equipped successfully.");
        }

        return DropValidationResult.Invalid("Unsupported drop target.");
    }

    private DropValidationResult ValidateSizeCompatibility(ItemData item, CharacterStats stats)
    {
        if (item == null || stats == null)
            return DropValidationResult.Invalid("Invalid item or character stats.");

        int itemSize = (int)item.DesignedForSize;
        int characterSize = (int)stats.CurrentSizeCategory;
        int delta = Mathf.Abs(itemSize - characterSize);

        if (item.Type == ItemType.Armor || item.Type == ItemType.Shield)
        {
            if (delta > 0)
                return DropValidationResult.Invalid("Armor/shields must match character size.");
        }
        else if (item.Type == ItemType.Weapon)
        {
            if (delta > 1)
                return DropValidationResult.Invalid("Weapon size too far from character size.");
        }

        return DropValidationResult.Valid();
    }

    private DropValidationResult ValidateClassRestriction(ItemData item, CharacterStats stats)
    {
        if (item == null || stats == null)
            return DropValidationResult.Invalid("Invalid class restriction data.");

        // Current data model does not carry explicit class lock lists.
        // Keep this hook so restrictions can be enforced in one place later.
        return DropValidationResult.Valid();
    }

    private bool NeedsProficiencyWarning(ItemData item, CharacterStats stats)
    {
        if (item == null || stats == null)
            return false;

        if (item.Type == ItemType.Weapon)
            return !stats.IsProficientWithWeapon(item);

        if (item.Type == ItemType.Armor || item.Type == ItemType.Shield)
            return !stats.IsProficientWithArmor(item);

        return false;
    }

    private void ShowContextMenu(SlotRef slot, Vector2 screenPosition)
    {
        HideTooltip();

        if (_contextMenuRoot == null || slot == null)
            return;

        ItemData item = ResolveItem(slot);
        if (item == null)
        {
            HideContextMenu();
            return;
        }

        List<(Button button, string text, Action action)> actions = BuildContextActions(slot, item);
        if (actions.Count == 0)
        {
            HideContextMenu();
            return;
        }

        Button[] buttons = { _contextPrimaryButton, _contextSecondaryButton, _contextTertiaryButton };
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null)
                continue;

            buttons[i].onClick.RemoveAllListeners();
            bool active = i < actions.Count;
            buttons[i].gameObject.SetActive(active);

            if (!active)
                continue;

            var entry = actions[i];
            Text textComp = buttons[i].GetComponentInChildren<Text>();
            if (textComp != null)
                textComp.text = entry.text;

            buttons[i].onClick.AddListener(() =>
            {
                HideContextMenu();
                entry.action?.Invoke();
            });
        }

        RectTransform rootRt = _contextMenuRoot.GetComponent<RectTransform>();
        RectTransform panelRt = _panel.GetComponent<RectTransform>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(panelRt, screenPosition, null, out Vector2 localPoint);
        rootRt.anchoredPosition = ClampContextMenuPosition(localPoint, rootRt.sizeDelta, panelRt.rect.size);

        _contextMenuRoot.SetActive(true);
    }

    private Vector2 ClampContextMenuPosition(Vector2 localPoint, Vector2 menuSize, Vector2 panelSize)
    {
        float halfW = panelSize.x * 0.5f;
        float halfH = panelSize.y * 0.5f;

        float x = Mathf.Clamp(localPoint.x + 10f, -halfW + 8f, halfW - menuSize.x - 8f);
        float y = Mathf.Clamp(localPoint.y - 10f, -halfH + menuSize.y + 8f, halfH - 8f);
        return new Vector2(x, y);
    }

    private List<(Button button, string text, Action action)> BuildContextActions(SlotRef slot, ItemData item)
    {
        List<(Button, string, Action)> actions = new List<(Button, string, Action)>();

        if (slot.Container == SlotContainerType.Stash)
        {
            CharacterController selected = GetSelectedCharacter();
            if (selected != null)
            {
                actions.Add((null, "Transfer to Backpack", () =>
                {
                    SlotRef destination = FindFirstEmptyInventorySlot(selected);
                    if (destination == null)
                    {
                        ShowMessage("No free backpack slot.", false);
                        return;
                    }

                    if (TryExecuteTransfer(slot, destination, item, out string fb))
                        ShowMessage(fb, true);
                    else
                        ShowMessage(fb, false);

                    RefreshAll();
                }));

                SlotRef bestEquipSlot = FindBestEquipmentTargetSlot(selected, item);
                if (bestEquipSlot != null)
                {
                    actions.Add((null, "Equip to Selected", () =>
                    {
                        if (TryExecuteTransfer(slot, bestEquipSlot, item, out string fb))
                            ShowMessage(fb, true);
                        else
                            ShowMessage(fb, false);

                        RefreshAll();
                    }));
                }
            }
        }
        else if (slot.Container == SlotContainerType.Inventory || slot.Container == SlotContainerType.InventoryStack)
        {
            SlotRef equipTarget = FindBestEquipmentTargetSlot(slot.Character, item);
            if (equipTarget != null)
            {
                actions.Add((null, "Equip", () =>
                {
                    if (TryExecuteTransfer(slot, equipTarget, item, out string fb))
                        ShowMessage(fb, true);
                    else
                        ShowMessage(fb, false);

                    RefreshAll();
                }));
            }

            actions.Add((null, "Transfer to Stash", () =>
            {
                if (slot.Container == SlotContainerType.InventoryStack)
                {
                    TransferStackFromInventoryToStash(slot.Character, slot.ItemGroup);
                    return;
                }

                SlotRef stashTarget = new SlotRef { Container = SlotContainerType.Stash };
                if (TryExecuteTransfer(slot, stashTarget, item, out string fb))
                    ShowMessage(fb, true);
                else
                    ShowMessage(fb, false);

                RefreshAll();
            }));

            if (slot.Container == SlotContainerType.Inventory)
            {
                actions.Add((null, "Drop Item", () =>
                {
                    Inventory inv = GetInventory(slot.Character);
                    if (inv == null)
                    {
                        ShowMessage("Inventory unavailable.", false);
                        return;
                    }

                    ItemData removed = inv.RemoveItemAt(slot.InventoryIndex);
                    ShowMessage(removed != null ? $"Dropped {removed.Name}." : "No item to drop.", removed != null);
                    RefreshAll();
                }));
            }
        }
        else if (slot.Container == SlotContainerType.Equipment)
        {
            actions.Add((null, "Unequip to Backpack", () =>
            {
                Inventory inv = GetInventory(slot.Character);
                if (inv != null && inv.Unequip(slot.EquipSlot))
                    ShowMessage($"Unequipped {item.Name}.", true);
                else
                    ShowMessage("Cannot unequip (backpack full).", false);

                RefreshAll();
            }));

            actions.Add((null, "Move to Stash", () =>
            {
                SlotRef stashTarget = new SlotRef { Container = SlotContainerType.Stash };
                if (TryExecuteTransfer(slot, stashTarget, item, out string fb))
                    ShowMessage(fb, true);
                else
                    ShowMessage(fb, false);

                RefreshAll();
            }));
        }

        if (actions.Count > 3)
            actions.RemoveRange(3, actions.Count - 3);

        return actions;
    }

    private void HideContextMenu()
    {
        if (_contextMenuRoot != null)
            _contextMenuRoot.SetActive(false);
    }

    private void ShowDragPreview(ItemData item)
    {
        if (_dragPreview == null || _dragPreviewText == null || item == null)
            return;

        _dragPreviewText.text = $"{GetItemIcon(item)} {item.Name}";
        _dragPreview.SetActive(true);
        MoveDragPreview(Input.mousePosition);
    }

    private void MoveDragPreview(Vector2 screenPosition)
    {
        if (_dragPreview == null || !_dragPreview.activeSelf || _panel == null)
            return;

        RectTransform panelRt = _panel.GetComponent<RectTransform>();
        RectTransform previewRt = _dragPreview.GetComponent<RectTransform>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(panelRt, screenPosition, null, out Vector2 localPoint);
        previewRt.anchoredPosition = localPoint + new Vector2(58f, -28f);
    }

    private void HideDragPreview()
    {
        if (_dragPreview != null)
            _dragPreview.SetActive(false);
    }

    private void ShowTooltipForSlot(SlotRef slot)
    {
        ItemData item = ResolveItem(slot);
        if (item == null)
        {
            HideTooltip();
            return;
        }

        int quantity = slot != null && slot.ItemGroup != null ? slot.ItemGroup.Quantity : 1;
        ShowTooltip(item, quantity);
    }

    private void ShowTooltip(ItemData item, int quantity)
    {
        if (_tooltipPanel == null || _tooltipText == null || _panel == null || item == null)
            return;

        string statSummary = item.GetStatSummary();
        string desc = string.IsNullOrWhiteSpace(item.Description) ? "No description." : item.Description;
        string quantityLine = quantity > 1 ? $"\nQty: {quantity}" : string.Empty;
        _tooltipText.text = $"<b>{item.Name}</b>{quantityLine}\n{statSummary}\n\n{desc}";

        _tooltipPanel.SetActive(true);
        _tooltipActive = true;
        UpdateTooltipPosition();
    }

    private void UpdateTooltipPosition()
    {
        if (!_tooltipActive || _tooltipPanel == null || !_tooltipPanel.activeSelf || _panel == null)
            return;

        RectTransform panelRt = _panel.GetComponent<RectTransform>();
        RectTransform tooltipRt = _tooltipPanel.GetComponent<RectTransform>();
        if (panelRt == null || tooltipRt == null)
            return;

        Camera eventCamera = GetCanvasEventCamera();
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(panelRt, Input.mousePosition, eventCamera, out Vector2 mouseLocalPoint))
            return;

        Vector2 tooltipSize = tooltipRt.rect.size;
        Vector2 position = GetSmartTooltipPosition(mouseLocalPoint, tooltipSize, panelRt.rect.size);
        tooltipRt.anchoredPosition = position;
    }

    private Vector2 GetSmartTooltipPosition(Vector2 mouseLocalPoint, Vector2 tooltipSize, Vector2 panelSize)
    {
        Vector2 topLeftPosition = mouseLocalPoint + TooltipCursorOffset;

        float rightBoundary = panelSize.x * 0.5f - TooltipEdgePadding;
        float bottomBoundary = -panelSize.y * 0.5f + TooltipEdgePadding;

        if (topLeftPosition.x + tooltipSize.x > rightBoundary)
            topLeftPosition.x = mouseLocalPoint.x - TooltipCursorOffset.x - tooltipSize.x;

        if (topLeftPosition.y - tooltipSize.y < bottomBoundary)
            topLeftPosition.y = mouseLocalPoint.y + Mathf.Abs(TooltipCursorOffset.y) + tooltipSize.y;

        return ClampTooltipToPanel(topLeftPosition, tooltipSize, panelSize);
    }

    private Vector2 ClampTooltipToPanel(Vector2 topLeftPosition, Vector2 tooltipSize, Vector2 panelSize)
    {
        float minX = -panelSize.x * 0.5f + TooltipEdgePadding;
        float maxX = panelSize.x * 0.5f - tooltipSize.x - TooltipEdgePadding;

        float minY = -panelSize.y * 0.5f + tooltipSize.y + TooltipEdgePadding;
        float maxY = panelSize.y * 0.5f - TooltipEdgePadding;

        topLeftPosition.x = Mathf.Clamp(topLeftPosition.x, minX, maxX);
        topLeftPosition.y = Mathf.Clamp(topLeftPosition.y, minY, maxY);
        return topLeftPosition;
    }

    private void EnsureInteractionInfrastructure(Canvas canvasOverride = null)
    {
        Canvas canvas = canvasOverride;
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();
        if (canvas == null && _panel != null)
            canvas = _panel.GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            Debug.LogError("[PreCombatUI] Interaction check failed: Canvas not found.");
            return;
        }

        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
            Debug.LogWarning("[PreCombatUI] No GraphicRaycaster on Canvas. Added one at runtime.");
        }

        if (EventSystem.current == null)
            Debug.LogError("[PreCombatUI] No EventSystem found!");
        else
            Debug.Log("[PreCombatUI] EventSystem active");
    }

    private Camera GetCanvasEventCamera()
    {
        Canvas canvas = _panel != null ? _panel.GetComponentInParent<Canvas>() : null;
        if (canvas == null)
            return null;

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        if (canvas.worldCamera != null)
            return canvas.worldCamera;

        return Camera.main;
    }

    private void HideTooltip()
    {
        _tooltipActive = false;

        if (_tooltipPanel != null)
            _tooltipPanel.SetActive(false);
    }

    private ItemData ResolveItem(SlotRef slot)
    {
        if (slot == null)
            return null;

        switch (slot.Container)
        {
            case SlotContainerType.Stash:
            case SlotContainerType.InventoryStack:
                return slot.ItemGroup != null ? slot.ItemGroup.FirstItem : null;
            case SlotContainerType.Inventory:
            {
                Inventory inv = GetInventory(slot.Character);
                if (inv == null || slot.InventoryIndex < 0 || slot.InventoryIndex >= inv.GeneralSlots.Length)
                    return null;
                return inv.GeneralSlots[slot.InventoryIndex];
            }
            case SlotContainerType.Equipment:
            {
                Inventory inv = GetInventory(slot.Character);
                return inv != null ? inv.GetEquipped(slot.EquipSlot) : null;
            }
            default:
                return null;
        }
    }

    private List<ItemStackGroup> BuildStashGroups()
    {
        if (_stash == null)
            return new List<ItemStackGroup>();

        return GroupItemsIntoStacks(_stash.GetItems(), PassesStashFilter);
    }

    private List<ItemStackGroup> BuildInventoryGroups(Inventory inv)
    {
        if (inv == null || inv.GeneralSlots == null)
            return new List<ItemStackGroup>();

        return GroupItemsIntoStacks(inv.GeneralSlots, item => item != null);
    }

    private List<ItemStackGroup> GroupItemsIntoStacks(IEnumerable<ItemData> items, Func<ItemData, bool> includePredicate)
    {
        List<ItemStackGroup> groups = new List<ItemStackGroup>();
        Dictionary<string, ItemStackGroup> map = new Dictionary<string, ItemStackGroup>();
        if (items == null)
            return groups;

        int uniqueCounter = 0;
        foreach (ItemData item in items)
        {
            if (item == null)
                continue;

            if (includePredicate != null && !includePredicate(item))
                continue;

            string key = IsStackable(item)
                ? BuildStackKey(item)
                : $"unique_{uniqueCounter++}_{BuildStackKey(item)}";

            if (!map.TryGetValue(key, out ItemStackGroup group))
            {
                group = new ItemStackGroup
                {
                    Key = key,
                    Prototype = item
                };
                map[key] = group;
                groups.Add(group);
            }

            group.Instances.Add(item);
        }

        Debug.Log($"[Stacking] Grouped items into {groups.Count} stack(s).");
        return groups;
    }

    private bool IsStackable(ItemData item)
    {
        if (item == null)
            return false;

        if (item.Type == ItemType.Consumable)
            return true;

        string name = item.Name ?? string.Empty;
        string id = item.Id ?? string.Empty;
        return name.IndexOf("arrow", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("bolt", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("bullet", StringComparison.OrdinalIgnoreCase) >= 0
            || id.IndexOf("arrow", StringComparison.OrdinalIgnoreCase) >= 0
            || id.IndexOf("bolt", StringComparison.OrdinalIgnoreCase) >= 0
            || id.IndexOf("bullet", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void ApplyStashSort(List<ItemStackGroup> groups)
    {
        groups.Sort((a, b) =>
        {
            ItemData ia = a != null ? a.Prototype : null;
            ItemData ib = b != null ? b.Prototype : null;

            if (ia == null && ib == null) return 0;
            if (ia == null) return 1;
            if (ib == null) return -1;

            switch (_stashSort)
            {
                case StashSortMode.Type:
                {
                    int typeCmp = ia.Type.CompareTo(ib.Type);
                    if (typeCmp != 0) return typeCmp;
                    break;
                }
                case StashSortMode.Value:
                {
                    int valueCmp = EstimateItemValue(ib).CompareTo(EstimateItemValue(ia));
                    if (valueCmp != 0) return valueCmp;
                    break;
                }
            }

            return string.Compare(ia.Name, ib.Name, StringComparison.OrdinalIgnoreCase);
        });
    }

    private bool PassesStashFilter(ItemData item)
    {
        switch (_stashFilter)
        {
            case StashFilterMode.Weapons:
                return item.Type == ItemType.Weapon;
            case StashFilterMode.Armor:
                return item.Type == ItemType.Armor || item.Type == ItemType.Shield;
            case StashFilterMode.Consumables:
                return item.Type == ItemType.Consumable;
            case StashFilterMode.Misc:
                return item.Type == ItemType.Misc;
            case StashFilterMode.All:
            default:
                return true;
        }
    }

    private int EstimateItemValue(ItemData item)
    {
        if (item == null)
            return 0;

        int value = 0;
        switch (item.Type)
        {
            case ItemType.Weapon:
                value += Mathf.Max(1, item.DamageCount) * Mathf.Max(1, item.DamageDice);
                value += Mathf.Max(0, item.BonusDamage);
                value += item.GetEnhancementAttackBonus() * 3;
                value += item.GetEnhancementDamageBonus() * 3;
                break;
            case ItemType.Armor:
                value += item.ArmorBonus * 5;
                value += Mathf.Max(0, item.MaxDexBonus);
                break;
            case ItemType.Shield:
                value += item.ShieldBonus * 5;
                break;
            case ItemType.Consumable:
                value += 8;
                value += Mathf.Max(0, item.HealAmount);
                break;
            case ItemType.Misc:
                value += 3;
                break;
        }

        value += Mathf.RoundToInt(Mathf.Max(0f, item.WeightLbs));
        return value;
    }

    private string BuildStackKey(ItemData item)
    {
        if (item == null)
            return "null-item";

        string id = string.IsNullOrWhiteSpace(item.Id) ? "no-id" : item.Id;
        string name = string.IsNullOrWhiteSpace(item.Name) ? "no-name" : item.Name;
        return $"{id}|{name}|{item.Type}";
    }

    private string GetFilterLabel(StashFilterMode mode)
    {
        switch (mode)
        {
            case StashFilterMode.Weapons: return "Weapons";
            case StashFilterMode.Armor: return "Armor";
            case StashFilterMode.Consumables: return "Consumables";
            case StashFilterMode.Misc: return "Misc";
            default: return "All";
        }
    }

    private string GetSortLabel(StashSortMode mode)
    {
        switch (mode)
        {
            case StashSortMode.Type: return "Type";
            case StashSortMode.Value: return "Value";
            default: return "Name";
        }
    }

    private string GetClassIcon(CharacterController ch)
    {
        if (ch == null || ch.Stats == null)
            return "👤";

        string cls = ch.Stats.CharacterClass ?? string.Empty;
        if (cls.IndexOf("wizard", StringComparison.OrdinalIgnoreCase) >= 0) return "🧙";
        if (cls.IndexOf("cleric", StringComparison.OrdinalIgnoreCase) >= 0) return "⛪";
        if (cls.IndexOf("rogue", StringComparison.OrdinalIgnoreCase) >= 0) return "🗡";
        if (cls.IndexOf("fighter", StringComparison.OrdinalIgnoreCase) >= 0) return "🛡";
        return "👤";
    }

    private string GetItemIcon(ItemData item)
    {
        if (item == null)
            return "•";

        if (!string.IsNullOrWhiteSpace(item.IconChar))
            return item.IconChar;

        switch (item.Type)
        {
            case ItemType.Weapon: return "⚔";
            case ItemType.Armor: return "🛡";
            case ItemType.Shield: return "🛡";
            case ItemType.Consumable: return "🧪";
            default: return "📦";
        }
    }

    private string BuildCompactItemName(ItemData item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Name))
            return string.Empty;

        const int max = 10;
        if (item.Name.Length <= max)
            return item.Name;

        return item.Name.Substring(0, max - 1) + "…";
    }

    private Color GetRarityTint(ItemData item)
    {
        if (item == null)
            return new Color(0.15f, 0.17f, 0.24f, 0.95f);

        int enhancement = Mathf.Max(item.GetEnhancementAttackBonus(), item.GetEnhancementDamageBonus());
        if (enhancement >= 4)
            return new Color(0.52f, 0.25f, 0.67f, 0.95f); // epic
        if (enhancement >= 2)
            return new Color(0.22f, 0.45f, 0.76f, 0.95f); // rare
        if (item.Type == ItemType.Consumable)
            return new Color(0.2f, 0.52f, 0.34f, 0.95f); // utility/green
        if (item.Type == ItemType.Weapon || item.Type == ItemType.Armor || item.Type == ItemType.Shield)
            return new Color(0.34f, 0.34f, 0.4f, 0.95f); // common equipment

        return new Color(0.24f, 0.28f, 0.36f, 0.95f);
    }

    private string GetNormalInventoryEquipSlotLabel(EquipSlot slot)
    {
        switch (slot)
        {
            case EquipSlot.Head: return "HEAD";
            case EquipSlot.FaceEyes: return "FACE/EYES";
            case EquipSlot.Neck: return "NECK";
            case EquipSlot.Torso: return "TORSO";
            case EquipSlot.Armor:
            case EquipSlot.ArmorRobe: return "ARMOR/ROBE";
            case EquipSlot.Waist: return "WAIST";
            case EquipSlot.Back: return "BACK";
            case EquipSlot.Wrists: return "WRISTS";
            case EquipSlot.Hands: return "HANDS";
            case EquipSlot.LeftRing: return "L RING";
            case EquipSlot.RightRing: return "R RING";
            case EquipSlot.Feet: return "FEET";
            case EquipSlot.LeftHand: return "L HAND";
            case EquipSlot.RightHand: return "R HAND";
            default: return slot.ToString().ToUpperInvariant();
        }
    }

    private string GetEquipSlotLabel(EquipSlot slot)
    {
        switch (slot)
        {
            case EquipSlot.Head: return "Head";
            case EquipSlot.FaceEyes: return "Face/Eyes";
            case EquipSlot.Neck: return "Neck";
            case EquipSlot.Torso: return "Torso";
            case EquipSlot.Armor:
            case EquipSlot.ArmorRobe: return "Armor/Robe";
            case EquipSlot.Waist: return "Waist";
            case EquipSlot.Back: return "Back";
            case EquipSlot.Wrists: return "Wrists";
            case EquipSlot.Hands: return "Hands";
            case EquipSlot.LeftRing: return "Ring (L)";
            case EquipSlot.RightRing: return "Ring (R)";
            case EquipSlot.Feet: return "Feet";
            case EquipSlot.RightHand: return "Main Hand";
            case EquipSlot.LeftHand: return "Off Hand";
            default: return slot.ToString();
        }
    }

    private string GetEquipSlotShortLabel(EquipSlot slot)
    {
        switch (slot)
        {
            case EquipSlot.Head: return "H";
            case EquipSlot.FaceEyes: return "FE";
            case EquipSlot.Neck: return "N";
            case EquipSlot.Torso: return "T";
            case EquipSlot.Armor:
            case EquipSlot.ArmorRobe: return "AR";
            case EquipSlot.Waist: return "W";
            case EquipSlot.Back: return "BK";
            case EquipSlot.Wrists: return "WR";
            case EquipSlot.Hands: return "HD";
            case EquipSlot.LeftRing: return "R1";
            case EquipSlot.RightRing: return "R2";
            case EquipSlot.Feet: return "F";
            case EquipSlot.RightHand: return "MH";
            case EquipSlot.LeftHand: return "OH";
            default: return "?";
        }
    }

    private SlotRef FindFirstEmptyInventorySlot(CharacterController character)
    {
        Inventory inv = GetInventory(character);
        if (inv == null)
            return null;

        for (int i = 0; i < inv.GeneralSlots.Length; i++)
        {
            if (inv.GeneralSlots[i] == null)
            {
                return new SlotRef
                {
                    Container = SlotContainerType.Inventory,
                    Character = character,
                    InventoryIndex = i
                };
            }
        }

        int previousLength = inv.GeneralSlots.Length;
        System.Array.Resize(ref inv.GeneralSlots, previousLength + 1);
        Debug.Log($"[Inventory] Expanded backpack slots in UI helper: {previousLength} -> {inv.GeneralSlots.Length}");

        return new SlotRef
        {
            Container = SlotContainerType.Inventory,
            Character = character,
            InventoryIndex = previousLength
        };
    }

    private SlotRef FindBestEquipmentTargetSlot(CharacterController character, ItemData item)
    {
        if (character == null || item == null)
            return null;

        EquipSlot[] priority =
        {
            EquipSlot.RightHand,
            EquipSlot.LeftHand,
            EquipSlot.ArmorRobe,
            EquipSlot.Head,
            EquipSlot.FaceEyes,
            EquipSlot.Neck,
            EquipSlot.Torso,
            EquipSlot.Waist,
            EquipSlot.Back,
            EquipSlot.Wrists,
            EquipSlot.Hands,
            EquipSlot.LeftRing,
            EquipSlot.RightRing,
            EquipSlot.Feet
        };

        for (int i = 0; i < priority.Length; i++)
        {
            if (!item.CanEquipIn(priority[i]))
                continue;

            return new SlotRef
            {
                Container = SlotContainerType.Equipment,
                Character = character,
                EquipSlot = priority[i]
            };
        }

        return null;
    }

    private int FindFirstIndexOfReference(ItemData[] items, ItemData target)
    {
        if (items == null || target == null)
            return -1;

        for (int i = 0; i < items.Length; i++)
        {
            if (ReferenceEquals(items[i], target))
                return i;
        }

        return -1;
    }

    private CharacterController GetSelectedCharacter()
    {
        if (_partyMembers == null || _selectedCharacterIndex < 0 || _selectedCharacterIndex >= _partyMembers.Count)
            return null;

        return _partyMembers[_selectedCharacterIndex];
    }

    private Inventory GetInventory(CharacterController character)
    {
        if (character == null)
            return null;

        InventoryComponent component = character.GetComponent<InventoryComponent>();
        return component != null ? component.CharacterInventory : null;
    }

    private void ShowMessage(string message, bool success)
    {
        if (_messageText == null)
            return;

        _messageText.text = message;
        _messageText.color = success
            ? new Color(0.58f, 0.94f, 0.62f)
            : new Color(0.96f, 0.64f, 0.52f);
    }

    private void CreateInfoLabel(Transform parent, string text)
    {
        GameObject info = new GameObject("Info", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        info.transform.SetParent(parent, false);

        LayoutElement le = info.GetComponent<LayoutElement>();
        le.preferredHeight = 20f;

        Text t = info.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 11;
        t.fontStyle = FontStyle.Italic;
        t.color = new Color(0.74f, 0.82f, 0.94f);
        t.alignment = TextAnchor.MiddleLeft;
        t.text = text;
    }

    private void OnFilterButtonPressed()
    {
        int next = ((int)_stashFilter + 1) % Enum.GetValues(typeof(StashFilterMode)).Length;
        _stashFilter = (StashFilterMode)next;
        Debug.Log($"[PreCombatUI] Filter clicked: {GetFilterLabel(_stashFilter)}");
        RefreshAll();
    }

    private void OnSortButtonPressed()
    {
        int next = ((int)_stashSort + 1) % Enum.GetValues(typeof(StashSortMode)).Length;
        _stashSort = (StashSortMode)next;
        RefreshAll();
    }

    private void OnClearStashPressed()
    {
        if (_stash == null)
        {
            ShowMessage("Stash unavailable.", false);
            return;
        }

        if (_stash.IsLocked)
        {
            ShowMessage("Stash is locked during combat.", false);
            return;
        }

        CharacterController selected = GetSelectedCharacter();
        if (selected == null)
        {
            ShowMessage("Select a character first.", false);
            return;
        }

        Inventory inv = GetInventory(selected);
        if (inv == null)
        {
            ShowMessage("Selected character has no inventory.", false);
            return;
        }

        List<ItemData> stashItems = _stash.GetItemsSnapshot();
        int moved = 0;
        for (int i = 0; i < stashItems.Count; i++)
        {
            ItemData item = stashItems[i];
            if (item == null)
                continue;

            SlotRef source = new SlotRef
            {
                Container = SlotContainerType.Stash,
                ItemGroup = new ItemStackGroup { Prototype = item }
            };
            source.ItemGroup.Instances.Add(item);

            SlotRef destination = FindFirstEmptyInventorySlot(selected);
            if (destination == null)
                break;

            if (TryExecuteTransfer(source, destination, item, out _))
                moved++;
        }

        ShowMessage(moved > 0
            ? $"Moved {moved} item(s) from stash to {selected.Stats.CharacterName}."
            : "Could not move items (backpack full).", moved > 0);

        RefreshAll();
    }

    private void OnBeginCombatPressed()
    {
        if (_stash != null && _stash.IsLocked)
        {
            ShowMessage("Stash is locked. Cannot begin from this screen.", false);
            return;
        }

        InvokeActionAfterClose(ref _onBeginCombat, "BeginCombat");
    }

    private void OnSkipPressed()
    {
        InvokeActionAfterClose(ref _onSkipInventory, "SkipInventory");
    }

    private void OnBackPressed()
    {
        InvokeActionAfterClose(ref _onBack, "Back");
    }

    private void InvokeActionAfterClose(ref Action callback, string reason)
    {
        if (_isClosing)
        {
            Debug.Log($"[PreCombatUI] Ignoring '{reason}' because close is already in progress.");
            return;
        }

        Action callbackToInvoke = callback;
        callback = null;

        // Clear the other action callbacks too so button spam cannot re-enter game flow callbacks.
        _onBeginCombat = null;
        _onSkipInventory = null;
        _onBack = null;

        Close(suppressCallback: true);

        if (callbackToInvoke != null)
        {
            Debug.Log($"[PreCombatUI] Invoking action callback for '{reason}'");
            callbackToInvoke.Invoke();
        }
        else
        {
            Debug.Log($"[PreCombatUI] No action callback bound for '{reason}'");
        }
    }

    private ScrollRect CreateScrollView(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 offsetMin,
        Vector2 offsetMax,
        out RectTransform contentRect,
        out RectTransform viewportRect,
        bool withVerticalScrollbar)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        root.transform.SetParent(parent, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = anchorMin;
        rootRect.anchorMax = anchorMax;
        rootRect.pivot = pivot;
        rootRect.offsetMin = offsetMin;
        rootRect.offsetMax = offsetMax;

        Image rootImage = root.GetComponent<Image>();
        rootImage.color = new Color(0.04f, 0.05f, 0.09f, 0.9f);

        ScrollRect scroll = root.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(root.transform, false);

        viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0f, 0f);
        viewportRect.anchorMax = new Vector2(1f, 1f);
        viewportRect.offsetMin = new Vector2(4f, 4f);
        viewportRect.offsetMax = withVerticalScrollbar ? new Vector2(-18f, -4f) : new Vector2(-4f, -4f);

        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.05f);
        viewportImage.raycastTarget = false;

        Mask mask = viewport.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);

        contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        scroll.viewport = viewportRect;
        scroll.content = contentRect;

        if (withVerticalScrollbar)
        {
            GameObject scrollbarGO = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            scrollbarGO.transform.SetParent(root.transform, false);

            RectTransform sbRect = scrollbarGO.GetComponent<RectTransform>();
            sbRect.anchorMin = new Vector2(1f, 0f);
            sbRect.anchorMax = new Vector2(1f, 1f);
            sbRect.pivot = new Vector2(1f, 1f);
            sbRect.offsetMin = new Vector2(-14f, 4f);
            sbRect.offsetMax = new Vector2(-2f, -4f);

            Image sbBg = scrollbarGO.GetComponent<Image>();
            sbBg.color = new Color(0.16f, 0.19f, 0.3f, 0.95f);

            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(scrollbarGO.transform, false);

            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0f, 0f);
            handleRect.anchorMax = new Vector2(1f, 1f);
            handleRect.offsetMin = new Vector2(1f, 1f);
            handleRect.offsetMax = new Vector2(-1f, -1f);

            Image handleImage = handle.GetComponent<Image>();
            handleImage.color = new Color(0.58f, 0.66f, 0.9f, 0.95f);

            Scrollbar scrollbar = scrollbarGO.GetComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.targetGraphic = handleImage;
            scrollbar.handleRect = handleRect;
            scrollbar.size = 0.2f;

            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        }

        return scroll;
    }

    private GameObject CreatePanel(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;

        Image image = go.GetComponent<Image>();
        image.color = color;

        return go;
    }

    private Text CreateText(
        Transform parent,
        string name,
        string value,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        int fontSize,
        FontStyle style,
        Color color,
        TextAnchor alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;

        Text text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.text = value;
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;

        return text;
    }

    private Button CreateButton(
        Transform parent,
        string name,
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Color color,
        Action onClick)
    {
        GameObject buttonGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonGO.transform.SetParent(parent, false);

        RectTransform rt = buttonGO.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;

        Image image = buttonGO.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;

        Button button = buttonGO.GetComponent<Button>();
        button.targetGraphic = image;
        button.interactable = true;

        ColorBlock cb = button.colors;
        cb.normalColor = color;
        cb.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        cb.pressedColor = Color.Lerp(color, Color.black, 0.2f);
        cb.selectedColor = cb.highlightedColor;
        cb.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        button.colors = cb;

        if (onClick != null)
            button.onClick.AddListener(() => onClick());

        CreateText(
            buttonGO.transform,
            "Label",
            label,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            12,
            FontStyle.Bold,
            Color.white,
            TextAnchor.MiddleCenter);

        return button;
    }

    private Button CreateFooterButton(Transform parent, string label, Color color, Action onClick)
    {
        GameObject buttonGO = new GameObject($"Footer_{label}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonGO.transform.SetParent(parent, false);

        LayoutElement le = buttonGO.GetComponent<LayoutElement>();
        le.preferredHeight = 36f;

        Image image = buttonGO.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;

        Button button = buttonGO.GetComponent<Button>();
        button.targetGraphic = image;
        button.interactable = true;

        ColorBlock cb = button.colors;
        cb.normalColor = color;
        cb.highlightedColor = Color.Lerp(color, Color.white, 0.16f);
        cb.pressedColor = Color.Lerp(color, Color.black, 0.2f);
        cb.selectedColor = cb.highlightedColor;
        cb.disabledColor = new Color(0.24f, 0.24f, 0.24f, 0.9f);
        button.colors = cb;

        if (onClick != null)
            button.onClick.AddListener(() => onClick());

        CreateText(
            buttonGO.transform,
            "Label",
            label,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            14,
            FontStyle.Bold,
            Color.white,
            TextAnchor.MiddleCenter);

        return button;
    }
}
