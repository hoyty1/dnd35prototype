using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using DND35e.Treasure;

/// <summary>
/// Post-combat modal loot collection window.
/// Shows loot in a scrollable grid with one slot per item instance and supports Loot All.
/// </summary>
public class LootCollectionUI : MonoBehaviour
{
    private static readonly Vector2 LootCellSize = new Vector2(132f, 132f);
    private static readonly Vector2 LootCellSpacing = new Vector2(14f, 14f);

    private RectOffset _lootGridPadding;

    public enum LootSourceType
    {
        Enemy,
        Ground,
        Treasure
    }

    [Serializable]
    public class LootItemInstance
    {
        public ItemData Item;
        public LootSourceType SourceType;
        public CharacterController SourceEnemy;
        public Vector2Int GroundPosition;
        public string SourceLabel;

        public bool IsValid
        {
            get
            {
                if (Item == null)
                    return false;

                if (Item.IsDestroyed)
                    return false;

                return true;
            }
        }
    }

    [Serializable]
    public class LootStackEntry
    {
        public string StackKey;
        public string SourceGroupKey;
        public string SourceLabel;
        public ItemData Prototype;
        public readonly List<LootItemInstance> RemainingInstances = new List<LootItemInstance>();
        public int LootedQuantity;

        public int RemainingQuantity => RemainingInstances.Count;
        public int TotalQuantity => RemainingQuantity + LootedQuantity;
        public bool IsLooted => RemainingQuantity <= 0;
    }

    private class ItemTileRefs
    {
        public LootStackEntry Entry;
        public LootItemInstance Instance;
        public GameObject Root;
        public Image Bg;
        public TMP_Text Icon;
        public TMP_Text Label;
    }

    private GameObject _root;
    private GameObject _dialog;
    private RectTransform _contentRoot;
    private ScrollRect _scrollRect;
    private Text _summaryText;
    private Text _goldSummaryText;
    private Text _statusText;
    private Text _tooltipText;
    private GameObject _tooltipPanel;
    private Button _lootAllButton;
    private Button _closeButton;
    private Button _exitLoopButton;

    private readonly List<LootStackEntry> _entries = new List<LootStackEntry>();
    private readonly List<ItemTileRefs> _tiles = new List<ItemTileRefs>();

    private LootItemInstance _selectedInstance;
    private Action<LootItemInstance, Action<bool>> _onLootSingle;
    private Action<int> _onClosed;
    private Action _onExitLoop;

    private bool _isOpen;
    private int _lootedCount;
    private float _savedScrollPosition = 1f;
    private int _openedFrame = -1;

    /// <summary>Stored treasure result for the unified loot window.</summary>
    private TreasureResult _treasureResult;

    /// <summary>UI element for displaying treasure summary above the loot grid.</summary>
    private Text _treasureSummaryText;

    /// <summary>Title text element for the window header.</summary>
    private Text _titleText;

    public bool IsOpen => _isOpen && _root != null && _root.activeSelf;

    private void Awake()
    {
        _lootGridPadding = new RectOffset(14, 14, 14, 14);
    }

    public void Open(
        List<LootStackEntry> entries,
        Action<LootItemInstance, Action<bool>> onLootSingle,
        Action<int> onClosed,
        Action onExitLoop = null)
    {
        Debug.Log($"[LootUI] Open requested | incomingEntries={(entries != null ? entries.Count : 0)} | hasTreasure={_treasureResult != null}");

        EnsureBuilt();
        if (_root == null)
        {
            Debug.LogError("[LootUI] Open aborted: _root was not built.");
            return;
        }

        _onLootSingle = onLootSingle;
        _onClosed = onClosed;
        _onExitLoop = onExitLoop;
        _lootedCount = 0;
        _selectedInstance = null;
        _savedScrollPosition = 1f;

        _entries.Clear();
        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                LootStackEntry entry = entries[i];
                if (entry == null || entry.Prototype == null)
                    continue;

                if (entry.RemainingQuantity <= 0)
                    continue;

                _entries.Add(entry);
            }
        }

        if (_exitLoopButton != null)
            _exitLoopButton.gameObject.SetActive(_onExitLoop != null);

        _root.transform.SetAsLastSibling();
        _root.SetActive(true);
        _isOpen = true;
        _openedFrame = Time.frameCount;

        Debug.Log($"[LootUI] Opened | filteredEntries={_entries.Count} | rootActive={_root.activeSelf} | frame={_openedFrame}");

        RebuildContent();
        UpdateFooter();

        bool hasTreasure = _treasureResult != null && !_treasureResult.IsEmpty;
        bool hasEquipment = _entries.Count > 0;
        string statusMsg = hasTreasure && hasEquipment
            ? "Treasure collected! Click equipment items to loot them to stash."
            : hasTreasure
                ? "Treasure collected! No equipment drops."
                : hasEquipment
                    ? "Click an item to loot it to stash."
                    : "No loot found.";
        ShowStatus(statusMsg, hasTreasure || hasEquipment);
    }

    /// <summary>
    /// Open the unified loot window with both generated treasure and equipment drops.
    /// Treasure is displayed as a read-only summary section above the interactive loot grid.
    /// </summary>
    public void Open(
        List<LootStackEntry> entries,
        TreasureResult treasureResult,
        Action<LootItemInstance, Action<bool>> onLootSingle,
        Action<int> onClosed,
        Action onExitLoop = null)
    {
        _treasureResult = treasureResult;
        Open(entries, onLootSingle, onClosed, onExitLoop);
    }

    public void Close(bool invokeClosedCallback = true)
    {
        bool rootWasActive = _root != null && _root.activeSelf;
        bool wasOpen = _isOpen || rootWasActive;

        Debug.Log($"[LootUI] Close requested | isOpen={_isOpen} | rootActive={rootWasActive} | lootedCount={_lootedCount} | invokeCallback={invokeClosedCallback}");

        if (_root != null)
            _root.SetActive(false);

        _isOpen = false;
        _treasureResult = null;
        HideTooltip();

        Action<int> callback = _onClosed;
        _onClosed = null;
        _onExitLoop = null;

        if (!invokeClosedCallback)
            return;

        if (!wasOpen)
        {
            Debug.Log("[LootUI] Close callback suppressed because loot window was already closed.");
            return;
        }

        callback?.Invoke(_lootedCount);
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            OnLootAllPressed();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
    }

    private void OnOverlayClicked()
    {
        if (!IsOpen)
            return;

        if (Time.frameCount <= _openedFrame + 1)
        {
            Debug.Log($"[LootUI] Ignoring overlay click on open frame | openFrame={_openedFrame} currentFrame={Time.frameCount}");
            return;
        }

        Debug.Log("[LootUI] Overlay clicked; closing loot window.");
        Close();
    }

    private void EnsureBuilt()
    {
        if (_root != null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            Debug.LogError("[LootCollectionUI] No Canvas found.");
            return;
        }

        Debug.Log($"[LootUI] Building loot UI root on canvas '{canvas.name}'");

        _root = new GameObject("LootCollectionRoot", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(Button));
        _root.transform.SetParent(canvas.transform, false);

        RectTransform rootRT = _root.GetComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        Image overlay = _root.GetComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.78f);

        CanvasGroup cg = _root.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = true;
        cg.interactable = true;

        Button overlayButton = _root.GetComponent<Button>();
        overlayButton.transition = Selectable.Transition.None;
        overlayButton.onClick.AddListener(OnOverlayClicked); // Optional QoL: click outside closes.

        _dialog = CreatePanel(
            _root.transform,
            "LootCollectionDialog",
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.08f, 0.1f, 0.15f, 0.98f));

        Debug.Log("[Loot] Panel: (0,0) to (1,1) - FULLSCREEN");

        _dialog.AddComponent<LayoutElement>();
        Image dialogBg = _dialog.GetComponent<Image>();
        if (dialogBg != null)
            dialogBg.raycastTarget = true;

        Outline dialogOutline = _dialog.AddComponent<Outline>();
        dialogOutline.effectColor = new Color(0.95f, 0.83f, 0.46f, 0.45f);
        dialogOutline.effectDistance = new Vector2(2f, -2f);

        AddEventTrigger(_dialog, EventTriggerType.PointerClick, data =>
        {
            // Swallow clicks so they don't close the overlay.
        });

        _titleText = CreateText(
            _dialog.transform,
            "Title",
            "POST-COMBAT LOOT",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -10f),
            new Vector2(0f, 46f),
            34,
            FontStyle.Bold,
            new Color(0.96f, 0.87f, 0.54f),
            TextAnchor.MiddleCenter);

        _summaryText = CreateText(
            _dialog.transform,
            "Summary",
            "",
            new Vector2(0.02f, 1f),
            new Vector2(0.98f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, -48f),
            new Vector2(0f, 24f),
            18,
            FontStyle.Normal,
            new Color(0.85f, 0.9f, 0.97f),
            TextAnchor.MiddleLeft);

        _goldSummaryText = CreateText(
            _dialog.transform,
            "GoldSummary",
            "",
            new Vector2(0.02f, 1f),
            new Vector2(0.98f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, -48f),
            new Vector2(0f, 24f),
            18,
            FontStyle.Bold,
            new Color(1f, 0.89f, 0.43f),
            TextAnchor.MiddleRight);

        // Treasure summary section — shown between header and loot grid when treasure exists.
        // Uses rich text for colored categories matching TreasureUI style.
        _treasureSummaryText = CreateText(
            _dialog.transform,
            "TreasureSummary",
            "",
            new Vector2(0.08f, 0.85f),
            new Vector2(0.92f, 0.85f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -8f),
            new Vector2(0f, 0f),
            14,
            FontStyle.Normal,
            new Color(0.9f, 0.9f, 0.9f),
            TextAnchor.UpperLeft);
        _treasureSummaryText.supportRichText = true;
        _treasureSummaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _treasureSummaryText.verticalOverflow = VerticalWrapMode.Overflow;

        // Add a ContentSizeFitter so the treasure text auto-sizes vertically
        ContentSizeFitter treasureFitter = _treasureSummaryText.gameObject.AddComponent<ContentSizeFitter>();
        treasureFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        treasureFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        _statusText = CreateText(
            _dialog.transform,
            "Status",
            "",
            new Vector2(0.02f, 0.12f),
            new Vector2(0.98f, 0.18f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            18,
            FontStyle.Normal,
            new Color(0.83f, 0.92f, 0.98f),
            TextAnchor.MiddleCenter);

        _scrollRect = CreateScrollView(
            _dialog.transform,
            "LootScroll",
            new Vector2(0.08f, 0.18f),
            new Vector2(0.92f, 0.85f),
            out _contentRoot);

        BuildTooltip();
        BuildFooter();

        _root.SetActive(false);
        _isOpen = false;

        Debug.Log("[LootUI] Build complete.");
    }

    private void BuildFooter()
    {
        GameObject footer = new GameObject("Footer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        footer.transform.SetParent(_dialog.transform, false);

        RectTransform footerRT = footer.GetComponent<RectTransform>();
        footerRT.anchorMin = new Vector2(0.1f, 0.03f);
        footerRT.anchorMax = new Vector2(0.9f, 0.12f);
        footerRT.offsetMin = Vector2.zero;
        footerRT.offsetMax = Vector2.zero;

        HorizontalLayoutGroup layout = footer.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 5f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        _lootAllButton = CreateFooterButton(footer.transform, "Loot All", new Color(0.22f, 0.52f, 0.3f, 1f), OnLootAllPressed);
        _closeButton = CreateFooterButton(footer.transform, "Close", new Color(0.4f, 0.24f, 0.24f, 1f), () => Close());
        _exitLoopButton = CreateFooterButton(footer.transform, "Exit Loop", new Color(0.5f, 0.18f, 0.18f, 1f), OnExitLoopPressed);
    }

    private void BuildTooltip()
    {
        _tooltipPanel = CreatePanel(
            _dialog.transform,
            "TooltipPanel",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 1f),
            Vector2.zero,
            new Vector2(320f, 200f),
            new Color(0.05f, 0.05f, 0.1f, 0.97f));

        Outline outline = _tooltipPanel.AddComponent<Outline>();
        outline.effectColor = new Color(0.92f, 0.82f, 0.44f, 0.55f);
        outline.effectDistance = new Vector2(1f, -1f);

        _tooltipText = CreateText(
            _tooltipPanel.transform,
            "TooltipText",
            string.Empty,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(-14f, -14f),
            12,
            FontStyle.Normal,
            new Color(0.9f, 0.93f, 0.99f),
            TextAnchor.UpperLeft);
        _tooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _tooltipText.verticalOverflow = VerticalWrapMode.Overflow;

        _tooltipPanel.SetActive(false);
    }

    private void RebuildContent()
    {
        if (_contentRoot == null)
            return;

        UpdateTreasureSection();

        if (_scrollRect != null)
            _savedScrollPosition = _scrollRect.verticalNormalizedPosition;

        for (int i = _contentRoot.childCount - 1; i >= 0; i--)
            Destroy(_contentRoot.GetChild(i).gameObject);

        _tiles.Clear();

        GridLayoutGroup grid = _contentRoot.GetComponent<GridLayoutGroup>();
        if (grid == null)
            grid = _contentRoot.gameObject.AddComponent<GridLayoutGroup>();

        grid.cellSize = LootCellSize;
        grid.spacing = LootCellSpacing;
        if (_lootGridPadding == null)
            _lootGridPadding = new RectOffset(14, 14, 14, 14);
        grid.padding = _lootGridPadding;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 7;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;

        ContentSizeFitter fitter = _contentRoot.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = _contentRoot.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        int displayedItems = 0;
        List<string> exampleNames = new List<string>();

        for (int i = 0; i < _entries.Count; i++)
        {
            LootStackEntry entry = _entries[i];
            if (entry == null || entry.Prototype == null || entry.RemainingQuantity <= 0)
                continue;

            for (int j = 0; j < entry.RemainingInstances.Count; j++)
            {
                LootItemInstance instance = entry.RemainingInstances[j];
                if (instance == null || !instance.IsValid)
                    continue;

                ItemTileRefs tile = BuildItemTile(_contentRoot, entry, instance);
                _tiles.Add(tile);
                displayedItems++;

                if (exampleNames.Count < 3)
                    exampleNames.Add(SafeItemName(instance.Item));
            }
        }

        if (_tiles.Count == 0)
            BuildNoLootMessage();

        Debug.Log($"[LootUI] Showing {displayedItems} unstacked items");
        if (exampleNames.Count > 0)
            Debug.Log($"[LootUI] Example items: {string.Join(", ", exampleNames)}");
        Debug.Log($"[LootUI] Grid cell size: {grid.cellSize}, spacing: {grid.spacing}, padding: L{grid.padding.left} R{grid.padding.right} T{grid.padding.top} B{grid.padding.bottom}");

        LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRoot);
        Canvas.ForceUpdateCanvases();

        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = Mathf.Clamp01(_savedScrollPosition);
    }

    /// <summary>
    /// Updates the treasure summary section and adjusts the loot grid position accordingly.
    /// When treasure is present, the summary is shown between the header and the loot grid.
    /// When no treasure exists, the section is hidden and the grid uses full available space.
    /// </summary>
    private void UpdateTreasureSection()
    {
        bool hasTreasure = _treasureResult != null && !_treasureResult.IsEmpty;

        // Update title to reflect content
        if (_titleText != null)
        {
            _titleText.text = hasTreasure ? "POST-COMBAT REWARDS" : "LOOT COLLECTION";
        }

        if (_treasureSummaryText == null)
            return;

        if (!hasTreasure)
        {
            _treasureSummaryText.text = "";
            _treasureSummaryText.gameObject.SetActive(false);

            // Restore the full scroll area
            if (_scrollRect != null)
            {
                RectTransform scrollRT = _scrollRect.GetComponent<RectTransform>();
                if (scrollRT != null)
                {
                    scrollRT.anchorMin = new Vector2(0.08f, 0.18f);
                    scrollRT.anchorMax = new Vector2(0.92f, 0.85f);
                }
            }
            return;
        }

        // Build the treasure summary text (rich text with colors matching TreasureUI)
        _treasureSummaryText.text = FormatTreasureSummary(_treasureResult);
        _treasureSummaryText.gameObject.SetActive(true);

        // Force layout rebuild so ContentSizeFitter calculates height
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_treasureSummaryText.rectTransform);

        // Calculate how much vertical space the treasure section needs (in normalized anchor units)
        float treasureHeight = LayoutUtility.GetPreferredHeight(_treasureSummaryText.rectTransform);
        RectTransform dialogRT = _dialog != null ? _dialog.GetComponent<RectTransform>() : null;
        float dialogHeight = dialogRT != null ? dialogRT.rect.height : Screen.height;

        // Treasure section sits at top (below header ~0.85) and pushes loot grid down.
        // Minimum space: at least 0.10 normalized units for treasure.
        float treasureNorm = Mathf.Clamp(treasureHeight / Mathf.Max(dialogHeight, 1f), 0.05f, 0.35f);
        float treasureBottom = 0.85f - treasureNorm - 0.01f;

        // Position treasure summary text
        RectTransform treasureRT = _treasureSummaryText.rectTransform;
        treasureRT.anchorMin = new Vector2(0.08f, treasureBottom);
        treasureRT.anchorMax = new Vector2(0.92f, 0.85f);
        treasureRT.offsetMin = new Vector2(8f, 4f);
        treasureRT.offsetMax = new Vector2(-8f, -8f);

        // Shrink the loot grid to below the treasure section
        if (_scrollRect != null)
        {
            RectTransform scrollRT = _scrollRect.GetComponent<RectTransform>();
            if (scrollRT != null)
            {
                scrollRT.anchorMin = new Vector2(0.08f, 0.18f);
                scrollRT.anchorMax = new Vector2(0.92f, treasureBottom - 0.01f);
            }
        }

        Debug.Log($"[LootUI] Treasure section displayed | treasureHeight={treasureHeight:F0}px | treasureNorm={treasureNorm:F3} | gridTop={treasureBottom - 0.01f:F3}");
    }

    /// <summary>
    /// Formats a TreasureResult into a compact rich-text summary for the header area.
    /// Since treasure items now appear as tiles in the loot grid, the summary only shows:
    /// - Coins collected (auto-added to party gold)
    /// - Brief breakdown of item categories found in the grid below
    /// </summary>
    private static string FormatTreasureSummary(TreasureResult result)
    {
        if (result == null || result.IsEmpty)
            return string.Empty;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<b><color=#F5DE69>══ Treasure Found — {result.TotalGPValue:N0} gp total ══</color></b>");

        // Coins (auto-collected)
        if (result.CoinsGPValue > 0)
        {
            var parts = new List<string>();
            if (result.CopperPieces > 0) parts.Add($"{result.CopperPieces:N0} cp");
            if (result.SilverPieces > 0) parts.Add($"{result.SilverPieces:N0} sp");
            if (result.GoldPieces > 0) parts.Add($"{result.GoldPieces:N0} gp");
            if (result.PlatinumPieces > 0) parts.Add($"{result.PlatinumPieces:N0} pp");
            sb.AppendLine($"<color=#FFD700>💰 Coins collected:</color> {string.Join(", ", parts)} <i>(≈{result.CoinsGPValue:N0} gp added to party gold)</i>");
        }

        // Brief summary of items in the loot grid below
        var categories = new List<string>();
        if (result.Gems.Count > 0)
            categories.Add($"<color=#80CCFF>{result.Gems.Count} gem(s)</color>");
        if (result.ArtObjects.Count > 0)
            categories.Add($"<color=#E6B3FF>{result.ArtObjects.Count} art object(s)</color>");
        if (result.MundaneItems.Count > 0)
            categories.Add($"<color=#CCCCCC>{result.MundaneItems.Count} mundane item(s)</color>");
        if (result.MagicItems.Count > 0)
            categories.Add($"<color=#4DFF4D>{result.MagicItems.Count} magic item(s)</color>");

        if (categories.Count > 0)
        {
            sb.AppendLine($"Loot items: {string.Join(", ", categories)} — <i>click tiles below to collect</i>");
        }

        // Monster gear note
        if (result.MonsterGearSubtracted > 0)
        {
            sb.AppendLine($"<color=#FF8888><i>(Monster gear subtracted: {result.MonsterGearSubtracted:N0} gp)</i></color>");
        }

        return sb.ToString().TrimEnd();
    }

    private ItemTileRefs BuildItemTile(Transform parent, LootStackEntry entry, LootItemInstance instance)
    {
        GameObject tile = new GameObject("LootTile", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(VerticalLayoutGroup));
        tile.transform.SetParent(parent, false);

        LayoutElement le = tile.GetComponent<LayoutElement>();
        le.preferredWidth = LootCellSize.x;
        le.preferredHeight = LootCellSize.y;
        le.minWidth = LootCellSize.x;
        le.minHeight = LootCellSize.y;

        Image bg = tile.GetComponent<Image>();
        Color normalColor = GetItemTypeColor(entry.Prototype);
        bg.color = normalColor;

        Outline outline = tile.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
        outline.effectDistance = new Vector2(1f, -1f);

        VerticalLayoutGroup tileLayout = tile.GetComponent<VerticalLayoutGroup>();
        tileLayout.padding = new RectOffset(8, 8, 8, 8);
        tileLayout.spacing = 6f;
        tileLayout.childAlignment = TextAnchor.UpperCenter;
        tileLayout.childControlWidth = true;
        tileLayout.childControlHeight = false;
        tileLayout.childForceExpandWidth = true;
        tileLayout.childForceExpandHeight = false;

        Button button = tile.GetComponent<Button>();
        button.targetGraphic = bg;

        ColorBlock cb = button.colors;
        cb.normalColor = normalColor;
        cb.highlightedColor = Color.Lerp(normalColor, Color.white, 0.2f);
        cb.pressedColor = Color.Lerp(normalColor, Color.black, 0.2f);
        cb.selectedColor = cb.highlightedColor;
        cb.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        button.colors = cb;

        TMP_Text iconText = CreateTMPLabel(
            tile.transform,
            "Icon",
            string.IsNullOrWhiteSpace(entry?.Prototype?.IconChar) ? "•" : entry.Prototype.IconChar,
            30,
            FontStyles.Bold,
            Color.white,
            TextAlignmentOptions.Center,
            preferredHeight: 36f,
            useEllipsis: false);

        TMP_Text titleText = CreateTMPLabel(
            tile.transform,
            "Name",
            SafeItemName(entry.Prototype),
            12,
            FontStyles.Bold,
            new Color(0.95f, 0.97f, 1f, 1f),
            TextAlignmentOptions.Center,
            preferredHeight: 34f,
            useEllipsis: true);

        ItemTileRefs refs = new ItemTileRefs
        {
            Entry = entry,
            Instance = instance,
            Root = tile,
            Bg = bg,
            Icon = iconText,
            Label = titleText
        };

        button.onClick.AddListener(() => LootSingleInstance(refs.Entry, refs.Instance));

        AddEventTrigger(tile, EventTriggerType.PointerEnter, data => ShowTooltip(entry));
        AddEventTrigger(tile, EventTriggerType.PointerExit, data => HideTooltip());

        return refs;
    }

    private void BuildNoLootMessage()
    {
        GameObject noLoot = new GameObject("NoLootMessage", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        noLoot.transform.SetParent(_contentRoot, false);

        LayoutElement le = noLoot.GetComponent<LayoutElement>();
        le.preferredWidth = 320f;
        le.preferredHeight = 60f;

        TextMeshProUGUI text = noLoot.GetComponent<TextMeshProUGUI>();
        EnsureTMPFontAsset(text);
        text.fontSize = 16;
        text.fontStyle = FontStyles.Italic;
        text.color = new Color(0.82f, 0.88f, 0.97f);
        text.alignment = TextAlignmentOptions.Center;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.text = "No loot found.";
    }

    private void LootSingleInstance(LootStackEntry entry, LootItemInstance instance)
    {
        if (entry == null || instance == null || !instance.IsValid)
            return;

        _selectedInstance = instance;

        bool transferCompleted = false;
        _onLootSingle?.Invoke(instance, success => transferCompleted = success);
        if (!transferCompleted)
        {
            ShowStatus($"Could not loot {SafeItemName(instance.Item)}.", false);
            return;
        }

        if (entry.RemainingInstances.Remove(instance))
        {
            entry.LootedQuantity++;
            _lootedCount++;
            ShowStatus($"Looted 1x {SafeItemName(instance.Item)}.", true);
        }

        RemoveEmptyEntries();
        RebuildContent();
        UpdateFooter();

        if (_entries.Count == 0)
        {
            ShowStatus("All looted!", true);
            Close();
        }
    }

    private void OnExitLoopPressed()
    {
        if (!IsOpen)
            return;

        Action onExitLoop = _onExitLoop;
        Close(invokeClosedCallback: false);
        onExitLoop?.Invoke();
    }

    private void OnLootAllPressed()
    {
        if (!IsOpen)
            return;

        int moved = 0;
        List<LootStackEntry> entrySnapshot = new List<LootStackEntry>(_entries);

        for (int e = 0; e < entrySnapshot.Count; e++)
        {
            LootStackEntry entry = entrySnapshot[e];
            if (entry == null || entry.RemainingQuantity <= 0)
                continue;

            List<LootItemInstance> instanceSnapshot = new List<LootItemInstance>(entry.RemainingInstances);
            for (int i = 0; i < instanceSnapshot.Count; i++)
            {
                LootItemInstance instance = instanceSnapshot[i];
                if (instance == null || !instance.IsValid)
                    continue;

                bool transferCompleted = false;
                _onLootSingle?.Invoke(instance, success => transferCompleted = success);
                if (!transferCompleted)
                    continue;

                if (entry.RemainingInstances.Remove(instance))
                {
                    moved++;
                    entry.LootedQuantity++;
                    _lootedCount++;
                }
            }
        }

        RemoveEmptyEntries();
        RebuildContent();
        UpdateFooter();

        if (moved > 0)
        {
            ShowStatus($"{moved} item(s) looted to stash.", true);
            Close();
            return;
        }

        ShowStatus("No items could be looted.", false);
    }

    private void RemoveEmptyEntries()
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            if (_entries[i] == null || _entries[i].RemainingQuantity <= 0)
                _entries.RemoveAt(i);
        }
    }

    private void UpdateFooter()
    {
        int uniqueTypes = 0;
        int remainingItems = 0;
        for (int i = 0; i < _entries.Count; i++)
        {
            LootStackEntry entry = _entries[i];
            if (entry == null)
                continue;

            if (entry.RemainingQuantity > 0)
                uniqueTypes++;

            remainingItems += Mathf.Max(0, entry.RemainingQuantity);
        }

        if (_summaryText != null)
            _summaryText.text = $"Visible Slots: {remainingItems}   •   Item Types: {uniqueTypes}   •   Looted: {_lootedCount}";

        if (_goldSummaryText != null)
        {
            int gold = CalculateGoldTotal();
            _goldSummaryText.text = gold > 0 ? $"Gold: {gold}" : string.Empty;
        }

        if (_lootAllButton != null)
            _lootAllButton.interactable = remainingItems > 0;

        Debug.Log($"[LootUI] Footer updated | visibleSlots={remainingItems} | itemTypes={uniqueTypes} | looted={_lootedCount}");
    }

    private int CalculateGoldTotal()
    {
        int totalGold = 0;
        for (int i = 0; i < _entries.Count; i++)
        {
            LootStackEntry entry = _entries[i];
            if (entry == null || entry.Prototype == null)
                continue;

            if (entry.Prototype.Type != ItemType.Misc)
                continue;

            string name = entry.Prototype.Name != null ? entry.Prototype.Name.ToLowerInvariant() : string.Empty;
            if (!name.Contains("gold") && !name.Contains("gp"))
                continue;

            totalGold += Mathf.Max(1, entry.RemainingQuantity);
        }

        return totalGold;
    }

    private static string SafeItemName(ItemData item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Name))
            return "Unknown Item";
        return item.Name;
    }

    private static Color GetItemTypeColor(ItemData item)
    {
        if (item == null)
            return new Color(0.22f, 0.28f, 0.38f, 0.98f);

        // Treasure items get distinctive colors to stand out in the loot grid
        if (item.IsTreasureItem)
        {
            // Gems (💎) — blue/cyan tint
            if (item.IconChar == "💎")
                return new Color(0.15f, 0.28f, 0.42f, 0.98f);
            // Art objects (🖼) — purple tint
            if (item.IconChar == "🖼")
                return new Color(0.32f, 0.18f, 0.38f, 0.98f);
            // Magic items (✨, 🪄, 🔮, 💍, 📜) — green tint
            if (item.CountsAsMagicForBypass || item.EnhancementBonus > 0)
                return new Color(0.14f, 0.32f, 0.18f, 0.98f);
        }

        switch (item.Type)
        {
            case ItemType.Weapon: return new Color(0.34f, 0.2f, 0.2f, 0.98f);
            case ItemType.Armor: return new Color(0.22f, 0.26f, 0.38f, 0.98f);
            case ItemType.Shield: return new Color(0.24f, 0.3f, 0.34f, 0.98f);
            case ItemType.Consumable: return new Color(0.2f, 0.34f, 0.24f, 0.98f);
            case ItemType.Misc:
            default:
                return new Color(0.28f, 0.24f, 0.36f, 0.98f);
        }
    }

    private void ShowTooltip(LootStackEntry entry)
    {
        if (_tooltipPanel == null || _tooltipText == null || entry == null || entry.Prototype == null)
            return;

        ItemData item = entry.Prototype;
        string stats = item.GetStatSummary();
        string desc = string.IsNullOrWhiteSpace(item.Description) ? "No description." : item.Description;

        // Add appraisal info for treasure items (gems/art)
        string appraiseInfo = "";
        if (item.IsTreasureItem && item.IsAppraised)
        {
            bool accurate = item.AppraisedValueGp == item.TrueValueGp;
            appraiseInfo = accurate
                ? $"\n<color=#4DFF4D>Appraised: {item.AppraisedValueGp:N0} gp (accurate)</color>"
                : $"\n<color=#FFD700>Appraised: {item.AppraisedValueGp:N0} gp</color>";
        }
        else if (item.IsTreasureItem && item.BasePriceGp > 0)
        {
            appraiseInfo = $"\n<color=#CCCCCC>Value: {item.BasePriceGp:N0} gp</color>";
        }

        _tooltipText.text = $"<b>{SafeItemName(item)}</b>\n{stats}{appraiseInfo}\n\n{desc}";

        RectTransform tooltipRT = _tooltipPanel.GetComponent<RectTransform>();
        RectTransform dialogRT = _dialog != null ? _dialog.GetComponent<RectTransform>() : null;
        if (tooltipRT != null && dialogRT != null)
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(dialogRT, Input.mousePosition, null, out localPoint);

            // Offset tooltip to the right and slightly below the cursor (pivot is top-left)
            float tooltipW = tooltipRT.sizeDelta.x;
            float tooltipH = tooltipRT.sizeDelta.y;
            float halfW = dialogRT.rect.width * 0.5f;
            float halfH = dialogRT.rect.height * 0.5f;

            float x = localPoint.x + 16f;
            float y = localPoint.y - 16f;

            // If tooltip would overflow the right edge, flip to left of cursor
            if (x + tooltipW > halfW)
                x = localPoint.x - tooltipW - 8f;

            // If tooltip would overflow the bottom edge, flip above cursor
            if (y - tooltipH < -halfH)
                y = localPoint.y + 8f;

            // Final clamp to keep fully inside dialog
            x = Mathf.Clamp(x, -halfW, halfW - tooltipW);
            y = Mathf.Clamp(y, -halfH + tooltipH, halfH);

            tooltipRT.anchoredPosition = new Vector2(x, y);
        }

        _tooltipPanel.SetActive(true);
    }

    private void HideTooltip()
    {
        if (_tooltipPanel != null)
            _tooltipPanel.SetActive(false);
    }

    private void ShowStatus(string msg, bool success)
    {
        if (_statusText == null)
            return;

        _statusText.text = msg;
        _statusText.color = success
            ? new Color(0.58f, 0.95f, 0.62f)
            : new Color(0.98f, 0.68f, 0.56f);
    }

    private TMP_Text CreateTMPLabel(
        Transform parent,
        string name,
        string content,
        float fontSize,
        FontStyles fontStyle,
        Color color,
        TextAlignmentOptions alignment,
        float preferredHeight,
        bool useEllipsis)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        LayoutElement le = go.GetComponent<LayoutElement>();
        le.preferredHeight = preferredHeight;

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        EnsureTMPFontAsset(text);
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.overflowMode = useEllipsis ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;

        return text;
    }

    private static TMP_FontAsset _cachedTMPFontAsset;

    private static void EnsureTMPFontAsset(TMP_Text text)
    {
        if (text == null || text.font != null)
            return;

        TMP_FontAsset fontAsset = ResolveTMPFontAsset();
        if (fontAsset != null)
            text.font = fontAsset;
    }

    private static TMP_FontAsset ResolveTMPFontAsset()
    {
        if (_cachedTMPFontAsset != null)
            return _cachedTMPFontAsset;

        _cachedTMPFontAsset = TMP_Settings.defaultFontAsset;
        if (_cachedTMPFontAsset != null)
            return _cachedTMPFontAsset;

        _cachedTMPFontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (_cachedTMPFontAsset != null)
            return _cachedTMPFontAsset;

        Font fallbackFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (fallbackFont == null)
            fallbackFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (fallbackFont == null)
            fallbackFont = Font.CreateDynamicFontFromOSFont("Arial", 16);

        if (fallbackFont != null)
            _cachedTMPFontAsset = TMP_FontAsset.CreateFontAsset(fallbackFont);

        return _cachedTMPFontAsset;
    }

    private Button CreateFooterButton(Transform parent, string label, Color color, Action onClick)
    {
        GameObject go = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        LayoutElement le = go.GetComponent<LayoutElement>();
        le.minWidth = 150f;
        le.preferredWidth = 150f;
        le.minHeight = 40f;
        le.preferredHeight = 40f;

        Image image = go.GetComponent<Image>();
        image.color = color;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;

        ColorBlock cb = button.colors;
        cb.normalColor = color;
        cb.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        cb.pressedColor = Color.Lerp(color, Color.black, 0.2f);
        cb.selectedColor = cb.highlightedColor;
        cb.disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.85f);
        button.colors = cb;

        if (onClick != null)
            button.onClick.AddListener(() => onClick());

        CreateText(
            go.transform,
            "Label",
            label,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            16,
            FontStyle.Bold,
            Color.white,
            TextAnchor.MiddleCenter);

        return button;
    }

    private ScrollRect CreateScrollView(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, out RectTransform content)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        root.transform.SetParent(parent, false);

        RectTransform rootRT = root.GetComponent<RectTransform>();
        rootRT.anchorMin = anchorMin;
        rootRT.anchorMax = anchorMax;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        Image bg = root.GetComponent<Image>();
        bg.color = new Color(0.04f, 0.06f, 0.1f, 0.95f);

        ScrollRect scroll = root.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 26f;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(root.transform, false);

        RectTransform viewportRT = viewport.GetComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = new Vector2(4f, 4f);
        viewportRT.offsetMax = new Vector2(-4f, -4f);

        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.03f);

        Mask mask = viewport.GetComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(viewport.transform, false);

        content = contentGO.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        GameObject scrollbarGO = new GameObject("VerticalScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        scrollbarGO.transform.SetParent(root.transform, false);

        RectTransform scrollbarRT = scrollbarGO.GetComponent<RectTransform>();
        scrollbarRT.anchorMin = new Vector2(1f, 0f);
        scrollbarRT.anchorMax = new Vector2(1f, 1f);
        scrollbarRT.pivot = new Vector2(1f, 1f);
        scrollbarRT.sizeDelta = new Vector2(12f, 0f);
        scrollbarRT.anchoredPosition = Vector2.zero;

        Image scrollbarBg = scrollbarGO.GetComponent<Image>();
        scrollbarBg.color = new Color(0.12f, 0.16f, 0.24f, 0.9f);

        GameObject handleGO = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGO.transform.SetParent(scrollbarGO.transform, false);
        RectTransform handleRT = handleGO.GetComponent<RectTransform>();
        handleRT.anchorMin = Vector2.zero;
        handleRT.anchorMax = Vector2.one;
        handleRT.offsetMin = new Vector2(1f, 1f);
        handleRT.offsetMax = new Vector2(-1f, -1f);

        Image handleImage = handleGO.GetComponent<Image>();
        handleImage.color = new Color(0.74f, 0.8f, 0.92f, 0.95f);

        Scrollbar scrollbar = scrollbarGO.GetComponent<Scrollbar>();
        scrollbar.targetGraphic = handleImage;
        scrollbar.handleRect = handleRT;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.size = 0.25f;

        scroll.viewport = viewportRT;
        scroll.content = content;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scroll.verticalScrollbarSpacing = 4f;

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
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);

        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;

        Image image = panel.GetComponent<Image>();
        image.color = color;

        return panel;
    }

    private Text CreateText(
        Transform parent,
        string name,
        string content,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        int fontSize,
        FontStyle fontStyle,
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
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.text = content;

        return text;
    }

    private void AddEventTrigger(GameObject target, EventTriggerType type, Action<BaseEventData> callback)
    {
        if (target == null || callback == null)
            return;

        EventTrigger trigger = target.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = target.AddComponent<EventTrigger>();

        if (trigger.triggers == null)
            trigger.triggers = new List<EventTrigger.Entry>();

        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(data => callback(data));
        trigger.triggers.Add(entry);
    }
}
