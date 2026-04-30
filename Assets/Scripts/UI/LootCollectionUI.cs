using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Post-combat modal loot collection window.
/// Shows loot grouped by source, supports double-click looting and Loot All.
/// </summary>
public class LootCollectionUI : MonoBehaviour
{
    public enum LootSourceType
    {
        Enemy,
        Ground
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

                return !string.IsNullOrWhiteSpace(SourceLabel);
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
        public GameObject Root;
        public Image Bg;
        public Text Label;
        public Text Quantity;
    }

    private class SourceSectionRefs
    {
        public string SourceGroupKey;
        public RectTransform GridRoot;
        public Text HeaderText;
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

    private readonly List<LootStackEntry> _entries = new List<LootStackEntry>();
    private readonly List<ItemTileRefs> _tiles = new List<ItemTileRefs>();
    private readonly List<SourceSectionRefs> _sections = new List<SourceSectionRefs>();

    private LootStackEntry _selectedEntry;
    private Action<LootItemInstance, Action<bool>> _onLootSingle;
    private Action<int> _onClosed;

    private bool _isOpen;
    private int _lootedCount;
    private float _savedScrollPosition = 1f;
    private int _openedFrame = -1;

    public bool IsOpen => _isOpen && _root != null && _root.activeSelf;

    public void Open(
        List<LootStackEntry> entries,
        Action<LootItemInstance, Action<bool>> onLootSingle,
        Action<int> onClosed)
    {
        Debug.Log($"[LootUI] Open requested | incomingEntries={(entries != null ? entries.Count : 0)}");

        EnsureBuilt();
        if (_root == null)
        {
            Debug.LogError("[LootUI] Open aborted: _root was not built.");
            return;
        }

        _onLootSingle = onLootSingle;
        _onClosed = onClosed;
        _lootedCount = 0;
        _selectedEntry = null;
        _savedScrollPosition = 1f;

        _entries.Clear();
        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                LootStackEntry entry = entries[i];
                if (entry == null || entry.Prototype == null || string.IsNullOrWhiteSpace(entry.SourceLabel))
                    continue;

                if (entry.RemainingQuantity <= 0)
                    continue;

                _entries.Add(entry);
            }
        }

        _root.transform.SetAsLastSibling();
        _root.SetActive(true);
        _isOpen = true;
        _openedFrame = Time.frameCount;

        Debug.Log($"[LootUI] Opened | filteredEntries={_entries.Count} | rootActive={_root.activeSelf} | frame={_openedFrame}");

        RebuildContent();
        UpdateFooter();
        ShowStatus(_entries.Count == 0 ? "No loot found." : "Double-click an item to loot it to stash.", false);
    }

    public void Close()
    {
        Debug.Log($"[LootUI] Close requested | isOpen={_isOpen} | lootedCount={_lootedCount}");

        if (_root != null)
            _root.SetActive(false);

        _isOpen = false;
        HideTooltip();

        Action<int> callback = _onClosed;
        _onClosed = null;
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
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(1120f, 680f),
            new Color(0.08f, 0.1f, 0.15f, 0.98f));

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

        CreateText(
            _dialog.transform,
            "Title",
            "LOOT COLLECTION",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -10f),
            new Vector2(0f, 34f),
            24,
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
            13,
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
            13,
            FontStyle.Bold,
            new Color(1f, 0.89f, 0.43f),
            TextAnchor.MiddleRight);

        _statusText = CreateText(
            _dialog.transform,
            "Status",
            "",
            new Vector2(0.02f, 0.12f),
            new Vector2(0.98f, 0.18f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            13,
            FontStyle.Normal,
            new Color(0.83f, 0.92f, 0.98f),
            TextAnchor.MiddleCenter);

        _scrollRect = CreateScrollView(
            _dialog.transform,
            "LootScroll",
            new Vector2(0.02f, 0.2f),
            new Vector2(0.98f, 0.86f),
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
        footerRT.anchorMin = new Vector2(0.08f, 0.02f);
        footerRT.anchorMax = new Vector2(0.92f, 0.1f);
        footerRT.offsetMin = Vector2.zero;
        footerRT.offsetMax = Vector2.zero;

        HorizontalLayoutGroup layout = footer.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        _lootAllButton = CreateFooterButton(footer.transform, "Loot All", new Color(0.22f, 0.52f, 0.3f, 1f), OnLootAllPressed);
        _closeButton = CreateFooterButton(footer.transform, "Close", new Color(0.4f, 0.24f, 0.24f, 1f), Close);
    }

    private void BuildTooltip()
    {
        _tooltipPanel = CreatePanel(
            _dialog.transform,
            "TooltipPanel",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-10f, -84f),
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

        if (_scrollRect != null)
            _savedScrollPosition = _scrollRect.verticalNormalizedPosition;

        for (int i = _contentRoot.childCount - 1; i >= 0; i--)
            Destroy(_contentRoot.GetChild(i).gameObject);

        _tiles.Clear();
        _sections.Clear();

        VerticalLayoutGroup sectionsLayout = _contentRoot.GetComponent<VerticalLayoutGroup>();
        if (sectionsLayout == null)
        {
            sectionsLayout = _contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            sectionsLayout.padding = new RectOffset(4, 4, 4, 4);
            sectionsLayout.spacing = 8f;
            sectionsLayout.childAlignment = TextAnchor.UpperLeft;
            sectionsLayout.childControlWidth = true;
            sectionsLayout.childControlHeight = false;
            sectionsLayout.childForceExpandWidth = true;
            sectionsLayout.childForceExpandHeight = false;
        }

        ContentSizeFitter fitter = _contentRoot.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = _contentRoot.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        Dictionary<string, SourceSectionRefs> sectionMap = new Dictionary<string, SourceSectionRefs>();

        for (int i = 0; i < _entries.Count; i++)
        {
            LootStackEntry entry = _entries[i];
            if (entry == null || entry.Prototype == null || entry.RemainingQuantity <= 0)
                continue;

            string sourceKey = string.IsNullOrWhiteSpace(entry.SourceGroupKey) ? entry.SourceLabel : entry.SourceGroupKey;
            if (!sectionMap.TryGetValue(sourceKey, out SourceSectionRefs section))
            {
                section = BuildSourceSection(sourceKey, entry.SourceLabel);
                sectionMap[sourceKey] = section;
                _sections.Add(section);
            }

            ItemTileRefs tile = BuildItemTile(section.GridRoot, entry);
            _tiles.Add(tile);
        }

        if (_tiles.Count == 0)
            BuildNoLootMessage();

        LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRoot);
        Canvas.ForceUpdateCanvases();

        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = Mathf.Clamp01(_savedScrollPosition);
    }

    private SourceSectionRefs BuildSourceSection(string sourceKey, string label)
    {
        GameObject root = CreatePanel(
            _contentRoot,
            "SourceSection_" + sourceKey,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 1f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.09f, 0.12f, 0.19f, 0.9f));

        LayoutElement sectionLE = root.AddComponent<LayoutElement>();
        sectionLE.minHeight = 110f;

        VerticalLayoutGroup sectionLayout = root.AddComponent<VerticalLayoutGroup>();
        sectionLayout.padding = new RectOffset(8, 8, 8, 8);
        sectionLayout.spacing = 6f;
        sectionLayout.childControlWidth = true;
        sectionLayout.childControlHeight = false;
        sectionLayout.childForceExpandWidth = true;
        sectionLayout.childForceExpandHeight = false;

        Text header = CreateText(
            root.transform,
            "SectionHeader",
            string.IsNullOrWhiteSpace(label) ? "Loot" : label,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            Vector2.zero,
            new Vector2(0f, 24f),
            14,
            FontStyle.Bold,
            new Color(0.94f, 0.84f, 0.48f),
            TextAnchor.MiddleLeft);

        LayoutElement headerLE = header.gameObject.AddComponent<LayoutElement>();
        headerLE.preferredHeight = 24f;

        GameObject gridGO = new GameObject("ItemGrid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        gridGO.transform.SetParent(root.transform, false);

        RectTransform gridRT = gridGO.GetComponent<RectTransform>();
        gridRT.anchorMin = new Vector2(0f, 1f);
        gridRT.anchorMax = new Vector2(1f, 1f);
        gridRT.pivot = new Vector2(0.5f, 1f);

        GridLayoutGroup grid = gridGO.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(180f, 72f);
        grid.spacing = new Vector2(8f, 8f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        grid.childAlignment = TextAnchor.UpperLeft;

        ContentSizeFitter gridFitter = gridGO.GetComponent<ContentSizeFitter>();
        gridFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return new SourceSectionRefs
        {
            SourceGroupKey = sourceKey,
            GridRoot = gridRT,
            HeaderText = header
        };
    }

    private ItemTileRefs BuildItemTile(Transform parent, LootStackEntry entry)
    {
        GameObject tile = new GameObject("LootTile", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        tile.transform.SetParent(parent, false);

        LayoutElement le = tile.GetComponent<LayoutElement>();
        le.preferredWidth = 180f;
        le.preferredHeight = 72f;

        Image bg = tile.GetComponent<Image>();
        Color normalColor = GetItemTypeColor(entry.Prototype);
        bg.color = normalColor;

        Button button = tile.GetComponent<Button>();
        button.targetGraphic = bg;

        ColorBlock cb = button.colors;
        cb.normalColor = normalColor;
        cb.highlightedColor = Color.Lerp(normalColor, Color.white, 0.2f);
        cb.pressedColor = Color.Lerp(normalColor, Color.black, 0.2f);
        cb.selectedColor = cb.highlightedColor;
        cb.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        button.colors = cb;

        GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(Text));
        titleGO.transform.SetParent(tile.transform, false);

        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 0.35f);
        titleRT.anchorMax = new Vector2(1f, 1f);
        titleRT.offsetMin = new Vector2(8f, 0f);
        titleRT.offsetMax = new Vector2(-8f, -4f);

        Text titleText = titleGO.GetComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 12;
        titleText.color = Color.white;
        titleText.alignment = TextAnchor.UpperLeft;
        titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        titleText.verticalOverflow = VerticalWrapMode.Overflow;
        titleText.text = BuildItemLabel(entry.Prototype);

        GameObject qtyGO = new GameObject("Quantity", typeof(RectTransform), typeof(Text));
        qtyGO.transform.SetParent(tile.transform, false);

        RectTransform qtyRT = qtyGO.GetComponent<RectTransform>();
        qtyRT.anchorMin = new Vector2(0f, 0f);
        qtyRT.anchorMax = new Vector2(1f, 0.35f);
        qtyRT.offsetMin = new Vector2(8f, 2f);
        qtyRT.offsetMax = new Vector2(-8f, 0f);

        Text qtyText = qtyGO.GetComponent<Text>();
        qtyText.font = titleText.font;
        qtyText.fontSize = 12;
        qtyText.fontStyle = FontStyle.Bold;
        qtyText.color = new Color(0.98f, 0.9f, 0.58f, 1f);
        qtyText.alignment = TextAnchor.MiddleRight;
        qtyText.text = "×" + Mathf.Max(1, entry.RemainingQuantity);

        ItemTileRefs refs = new ItemTileRefs
        {
            Entry = entry,
            Root = tile,
            Bg = bg,
            Label = titleText,
            Quantity = qtyText
        };

        button.onClick.AddListener(() => SelectEntry(refs.Entry));

        AddEventTrigger(tile, EventTriggerType.PointerClick, data =>
        {
            PointerEventData pointer = data as PointerEventData;
            if (pointer != null && pointer.clickCount >= 2)
            {
                LootEntryStack(entry);
            }
            else
            {
                SelectEntry(entry);
            }
        });

        AddEventTrigger(tile, EventTriggerType.PointerEnter, data => ShowTooltip(entry));
        AddEventTrigger(tile, EventTriggerType.PointerExit, data => HideTooltip());

        return refs;
    }

    private void BuildNoLootMessage()
    {
        GameObject noLoot = new GameObject("NoLootMessage", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        noLoot.transform.SetParent(_contentRoot, false);

        LayoutElement le = noLoot.GetComponent<LayoutElement>();
        le.preferredHeight = 42f;

        Text text = noLoot.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 14;
        text.fontStyle = FontStyle.Italic;
        text.color = new Color(0.82f, 0.88f, 0.97f);
        text.alignment = TextAnchor.MiddleCenter;
        text.text = "No loot found.";
    }

    private void SelectEntry(LootStackEntry entry)
    {
        _selectedEntry = entry;

        for (int i = 0; i < _tiles.Count; i++)
        {
            ItemTileRefs tile = _tiles[i];
            if (tile == null || tile.Bg == null)
                continue;

            bool selected = tile.Entry == _selectedEntry;
            Color baseColor = GetItemTypeColor(tile.Entry != null ? tile.Entry.Prototype : null);
            tile.Bg.color = selected
                ? Color.Lerp(baseColor, new Color(1f, 0.95f, 0.62f, 1f), 0.45f)
                : baseColor;
        }
    }

    private void LootEntryStack(LootStackEntry entry)
    {
        if (entry == null || entry.RemainingQuantity <= 0)
            return;

        int moved = 0;
        List<LootItemInstance> snapshot = new List<LootItemInstance>(entry.RemainingInstances);
        for (int i = 0; i < snapshot.Count; i++)
        {
            LootItemInstance instance = snapshot[i];
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

        if (moved > 0)
        {
            ShowStatus($"Looted {moved}x {SafeItemName(entry.Prototype)}.", true);
            RemoveEmptyEntries();
            RebuildContent();
            UpdateFooter();

            if (_entries.Count == 0)
            {
                ShowStatus("All looted!", true);
                Close();
            }
        }
        else
        {
            ShowStatus($"Could not loot {SafeItemName(entry.Prototype)}.", false);
        }
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
        int totalStacks = 0;
        int remainingItems = 0;
        for (int i = 0; i < _entries.Count; i++)
        {
            LootStackEntry entry = _entries[i];
            if (entry == null)
                continue;

            if (entry.RemainingQuantity > 0)
                totalStacks++;

            remainingItems += Mathf.Max(0, entry.RemainingQuantity);
        }

        if (_summaryText != null)
            _summaryText.text = $"Stacks: {totalStacks}   •   Remaining Items: {remainingItems}   •   Looted: {_lootedCount}";

        if (_goldSummaryText != null)
        {
            int gold = CalculateGoldTotal();
            _goldSummaryText.text = gold > 0 ? $"Gold: {gold}" : string.Empty;
        }

        if (_lootAllButton != null)
            _lootAllButton.interactable = remainingItems > 0;
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

    private string BuildItemLabel(ItemData item)
    {
        if (item == null)
            return "Unknown Item";

        string icon = string.IsNullOrWhiteSpace(item.IconChar) ? "•" : item.IconChar;
        return icon + " " + SafeItemName(item);
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
        _tooltipText.text = $"<b>{SafeItemName(item)}</b>\n{stats}\n\n{desc}";

        RectTransform tooltipRT = _tooltipPanel.GetComponent<RectTransform>();
        RectTransform dialogRT = _dialog != null ? _dialog.GetComponent<RectTransform>() : null;
        if (tooltipRT != null && dialogRT != null)
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(dialogRT, Input.mousePosition, null, out localPoint);
            float x = Mathf.Clamp(localPoint.x + 180f, -dialogRT.rect.width * 0.5f + 170f, dialogRT.rect.width * 0.5f - 170f);
            float y = Mathf.Clamp(localPoint.y - 16f, -dialogRT.rect.height * 0.5f + 100f, dialogRT.rect.height * 0.5f - 100f);
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

    private Button CreateFooterButton(Transform parent, string label, Color color, Action onClick)
    {
        GameObject go = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        LayoutElement le = go.GetComponent<LayoutElement>();
        le.preferredHeight = 42f;

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

        scroll.viewport = viewportRT;
        scroll.content = content;

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
