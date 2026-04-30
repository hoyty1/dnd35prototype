using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Pre-combat inventory phase screen.
/// Lets players move items between a shared party stash and character inventories before initiative starts.
/// </summary>
public class PreCombatInventoryUI : MonoBehaviour
{
    private class StashGroup
    {
        public string Key;
        public ItemData Prototype;
        public List<ItemData> Instances = new List<ItemData>();
    }

    private class CharacterPanelRefs
    {
        public CharacterController Character;
        public GameObject Root;
        public Image Background;
        public Outline Outline;
        public Text NameText;
        public Text SummaryText;
        public RectTransform Content;
        public Button SelectButton;
        public Button EquipBestButton;
    }

    private enum StashFilterMode
    {
        All,
        Weapons,
        Armor,
        Shields,
        Consumables,
        Misc
    }

    private enum StashSortMode
    {
        Name,
        Type,
        Weight
    }

    private GameObject _panel;
    private Text _stashStatusText;
    private Text _stashInfoText;
    private Text _selectedCharacterText;
    private Text _messageText;
    private RectTransform _stashContent;
    private RectTransform _characterPanelRow;

    private Button _filterButton;
    private Button _sortButton;
    private Button _clearStashButton;
    private Button _beginCombatButton;
    private Button _skipButton;
    private Button _backButton;

    private GameObject _tooltipPanel;
    private Text _tooltipText;

    private readonly List<CharacterPanelRefs> _characterPanels = new List<CharacterPanelRefs>();

    private PartyStash _stash;
    private List<CharacterController> _partyMembers = new List<CharacterController>();
    private int _selectedCharacterIndex;

    private StashFilterMode _stashFilter = StashFilterMode.All;
    private StashSortMode _stashSort = StashSortMode.Name;

    private Action _onBeginCombat;
    private Action _onSkipInventory;
    private Action _onBack;

    public bool IsOpen => _panel != null && _panel.activeSelf;

    public void Open(
        PartyStash stash,
        List<CharacterController> partyMembers,
        Action onBeginCombat,
        Action onSkipInventory,
        Action onBack)
    {
        EnsureBuilt();
        if (_panel == null)
            return;

        _stash = stash;
        _partyMembers = partyMembers != null ? new List<CharacterController>(partyMembers) : new List<CharacterController>();
        _onBeginCombat = onBeginCombat;
        _onSkipInventory = onSkipInventory;
        _onBack = onBack;

        if (_partyMembers.Count == 0)
            _selectedCharacterIndex = -1;
        else
            _selectedCharacterIndex = Mathf.Clamp(_selectedCharacterIndex, 0, _partyMembers.Count - 1);

        _panel.SetActive(true);
        HideTooltip();
        RefreshAll();
    }

    public void Close()
    {
        if (_panel != null)
            _panel.SetActive(false);

        HideTooltip();
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

        _panel = CreatePanel(
            canvas.transform,
            "PreCombatInventoryPanel",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(1120f, 700f),
            new Color(0.06f, 0.07f, 0.11f, 0.97f));

        Outline panelOutline = _panel.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0f, 0f, 0f, 0.65f);
        panelOutline.effectDistance = new Vector2(2f, -2f);

        CreateText(
            _panel.transform,
            "PreCombatTitle",
            "PRE-COMBAT PARTY STASH",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -8f),
            new Vector2(0f, 36f),
            26,
            FontStyle.Bold,
            new Color(0.95f, 0.85f, 0.48f),
            TextAnchor.MiddleCenter);

        _stashStatusText = CreateText(
            _panel.transform,
            "StashStatusText",
            "Stash: Unlocked",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -42f),
            new Vector2(0f, 24f),
            14,
            FontStyle.Bold,
            new Color(0.56f, 0.92f, 0.64f),
            TextAnchor.MiddleCenter);

        _selectedCharacterText = CreateText(
            _panel.transform,
            "SelectedCharacterText",
            "Selected Character: None",
            new Vector2(0.02f, 1f),
            new Vector2(0.98f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, -70f),
            new Vector2(0f, 22f),
            13,
            FontStyle.Normal,
            new Color(0.75f, 0.84f, 0.95f),
            TextAnchor.MiddleLeft);

        BuildStashSection();
        BuildCharacterSection();
        BuildFooter();
        BuildTooltip();

        _panel.SetActive(false);
    }

    private void BuildStashSection()
    {
        GameObject stashRoot = CreatePanel(
            _panel.transform,
            "StashSection",
            new Vector2(0.02f, 0.49f),
            new Vector2(0.98f, 0.9f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.09f, 0.11f, 0.18f, 0.95f));

        CreateText(
            stashRoot.transform,
            "StashHeader",
            "Party Stash",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(10f, -4f),
            new Vector2(-20f, 24f),
            18,
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
            new Vector2(10f, -34f),
            new Vector2(160f, 28f),
            new Color(0.24f, 0.32f, 0.56f, 1f),
            OnFilterButtonPressed);

        _sortButton = CreateButton(
            stashRoot.transform,
            "SortButton",
            "Sort: Name",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(180f, -34f),
            new Vector2(150f, 28f),
            new Color(0.18f, 0.4f, 0.58f, 1f),
            OnSortButtonPressed);

        _clearStashButton = CreateButton(
            stashRoot.transform,
            "ClearStashButton",
            "Clear Stash",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(340f, -34f),
            new Vector2(160f, 28f),
            new Color(0.58f, 0.33f, 0.18f, 1f),
            OnClearStashPressed);

        _stashInfoText = CreateText(
            stashRoot.transform,
            "StashInfoText",
            "0 items",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-12f, -34f),
            new Vector2(360f, 28f),
            12,
            FontStyle.Normal,
            new Color(0.76f, 0.84f, 0.92f),
            TextAnchor.MiddleRight);

        ScrollRect stashScroll = CreateScrollView(
            stashRoot.transform,
            "StashScroll",
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            new Vector2(8f, 8f),
            new Vector2(-8f, -68f),
            out _stashContent,
            out _);

        VerticalLayoutGroup stashLayout = _stashContent.gameObject.AddComponent<VerticalLayoutGroup>();
        stashLayout.padding = new RectOffset(4, 4, 4, 4);
        stashLayout.spacing = 4f;
        stashLayout.childAlignment = TextAnchor.UpperLeft;
        stashLayout.childControlWidth = true;
        stashLayout.childControlHeight = false;
        stashLayout.childForceExpandWidth = true;
        stashLayout.childForceExpandHeight = false;

        ContentSizeFitter stashFitter = _stashContent.gameObject.AddComponent<ContentSizeFitter>();
        stashFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (stashScroll != null)
            stashScroll.scrollSensitivity = 24f;
    }

    private void BuildCharacterSection()
    {
        GameObject charactersRoot = CreatePanel(
            _panel.transform,
            "CharactersSection",
            new Vector2(0.02f, 0.14f),
            new Vector2(0.98f, 0.47f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.08f, 0.1f, 0.16f, 0.95f));

        CreateText(
            charactersRoot.transform,
            "CharactersHeader",
            "Party Members (Click a panel to select transfer target)",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(10f, -4f),
            new Vector2(-20f, 24f),
            15,
            FontStyle.Bold,
            Color.white,
            TextAnchor.MiddleLeft);

        GameObject rowGO = new GameObject("CharacterRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGO.transform.SetParent(charactersRoot.transform, false);

        RectTransform rowRect = rowGO.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 0f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.offsetMin = new Vector2(8f, 8f);
        rowRect.offsetMax = new Vector2(-8f, -34f);

        HorizontalLayoutGroup rowLayout = rowGO.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 6f;
        rowLayout.padding = new RectOffset(0, 0, 0, 0);
        rowLayout.childAlignment = TextAnchor.UpperLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = true;

        _characterPanelRow = rowRect;
    }

    private void BuildFooter()
    {
        _messageText = CreateText(
            _panel.transform,
            "FooterMessage",
            "",
            new Vector2(0.02f, 0.08f),
            new Vector2(0.98f, 0.13f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            13,
            FontStyle.Normal,
            new Color(0.92f, 0.74f, 0.42f),
            TextAnchor.MiddleCenter);

        GameObject footer = new GameObject("FooterButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        footer.transform.SetParent(_panel.transform, false);

        RectTransform footerRect = footer.GetComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0.08f, 0.015f);
        footerRect.anchorMax = new Vector2(0.92f, 0.07f);
        footerRect.offsetMin = Vector2.zero;
        footerRect.offsetMax = Vector2.zero;

        HorizontalLayoutGroup footerLayout = footer.GetComponent<HorizontalLayoutGroup>();
        footerLayout.spacing = 12f;
        footerLayout.childAlignment = TextAnchor.MiddleCenter;
        footerLayout.childControlWidth = true;
        footerLayout.childControlHeight = true;
        footerLayout.childForceExpandWidth = true;
        footerLayout.childForceExpandHeight = true;

        _backButton = CreateFooterButton(footer.transform, "Back", new Color(0.46f, 0.22f, 0.22f), OnBackPressed);
        _skipButton = CreateFooterButton(footer.transform, "Skip Inventory", new Color(0.22f, 0.36f, 0.58f), OnSkipPressed);
        _beginCombatButton = CreateFooterButton(footer.transform, "Begin Combat", new Color(0.2f, 0.5f, 0.28f), OnBeginCombatPressed);
    }

    private void BuildTooltip()
    {
        _tooltipPanel = CreatePanel(
            _panel.transform,
            "ItemTooltip",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-14f, -90f),
            new Vector2(320f, 180f),
            new Color(0.04f, 0.04f, 0.08f, 0.96f));

        Outline tooltipOutline = _tooltipPanel.AddComponent<Outline>();
        tooltipOutline.effectColor = new Color(0.86f, 0.78f, 0.42f, 0.55f);
        tooltipOutline.effectDistance = new Vector2(1f, -1f);

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
            new Color(0.88f, 0.9f, 0.96f),
            TextAnchor.UpperLeft);
        _tooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _tooltipText.verticalOverflow = VerticalWrapMode.Overflow;

        _tooltipPanel.SetActive(false);
    }

    private void RefreshAll()
    {
        RefreshStashControlsText();
        RebuildCharacterPanels();
        RefreshStashList();
        RefreshSelectedCharacterLabel();
    }

    private void RefreshStashControlsText()
    {
        if (_filterButton != null)
            _filterButton.GetComponentInChildren<Text>().text = $"Filter: {GetFilterLabel(_stashFilter)}";

        if (_sortButton != null)
            _sortButton.GetComponentInChildren<Text>().text = $"Sort: {GetSortLabel(_stashSort)}";

        bool stashLocked = _stash != null && _stash.IsLocked;
        if (_stashStatusText != null)
        {
            _stashStatusText.text = stashLocked ? "Stash: Locked (Combat Active)" : "Stash: Unlocked";
            _stashStatusText.color = stashLocked
                ? new Color(0.96f, 0.55f, 0.44f)
                : new Color(0.56f, 0.92f, 0.64f);
        }

        if (_stashInfoText != null)
        {
            int total = _stash != null ? _stash.Count : 0;
            _stashInfoText.text = stashLocked
                ? $"{total} items • LOCKED"
                : $"{total} items • Unlimited capacity";
        }

        if (_clearStashButton != null)
            _clearStashButton.interactable = _stash != null && !_stash.IsLocked && _stash.Count > 0;
    }

    private void RefreshSelectedCharacterLabel()
    {
        if (_selectedCharacterText == null)
            return;

        CharacterController selected = GetSelectedCharacter();
        if (selected == null || selected.Stats == null)
        {
            _selectedCharacterText.text = "Selected Character: None";
            return;
        }

        Inventory inv = GetInventory(selected);
        int items = inv != null ? inv.ItemCount : 0;
        int free = inv != null ? inv.EmptySlots : 0;
        _selectedCharacterText.text = $"Selected Character: {selected.Stats.CharacterName}  •  Backpack {items}/{Inventory.GeneralSlotCount}  •  Free Slots {free}";
    }

    private void RebuildCharacterPanels()
    {
        _characterPanels.Clear();

        if (_characterPanelRow == null)
            return;

        for (int i = _characterPanelRow.childCount - 1; i >= 0; i--)
            Destroy(_characterPanelRow.GetChild(i).gameObject);

        if (_partyMembers == null)
            return;

        for (int i = 0; i < _partyMembers.Count; i++)
        {
            CharacterController character = _partyMembers[i];
            if (character == null || character.Stats == null)
                continue;

            CharacterPanelRefs refs = BuildCharacterPanel(character, i);
            _characterPanels.Add(refs);
            RefreshCharacterPanel(refs, i == _selectedCharacterIndex);
        }

        if (_characterPanels.Count == 0)
            _selectedCharacterIndex = -1;
        else
            _selectedCharacterIndex = Mathf.Clamp(_selectedCharacterIndex, 0, _characterPanels.Count - 1);
    }

    private CharacterPanelRefs BuildCharacterPanel(CharacterController character, int index)
    {
        GameObject panel = CreatePanel(
            _characterPanelRow,
            $"CharacterPanel_{index}",
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.13f, 0.16f, 0.24f, 0.95f));

        LayoutElement panelLayout = panel.AddComponent<LayoutElement>();
        panelLayout.preferredWidth = 250f;
        panelLayout.minWidth = 210f;

        Outline outline = panel.AddComponent<Outline>();
        outline.effectDistance = new Vector2(2f, -2f);
        outline.effectColor = new Color(0.38f, 0.42f, 0.62f, 0.8f);

        Button selectButton = panel.AddComponent<Button>();
        selectButton.targetGraphic = panel.GetComponent<Image>();
        selectButton.onClick.AddListener(() =>
        {
            _selectedCharacterIndex = index;
            RefreshAll();
        });

        Text nameText = CreateText(
            panel.transform,
            "Name",
            character.Stats.CharacterName,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(8f, -4f),
            new Vector2(-16f, 22f),
            14,
            FontStyle.Bold,
            Color.white,
            TextAnchor.MiddleLeft);

        Button equipBestButton = CreateButton(
            panel.transform,
            "EquipBestButton",
            "Equip Best Gear",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -28f),
            new Vector2(-16f, 22f),
            new Color(0.24f, 0.44f, 0.32f, 1f),
            () => OnEquipBestForCharacter(index));

        Text summaryText = CreateText(
            panel.transform,
            "Summary",
            "",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(8f, -54f),
            new Vector2(-16f, 30f),
            11,
            FontStyle.Normal,
            new Color(0.83f, 0.88f, 0.96f),
            TextAnchor.UpperLeft);

        ScrollRect characterScroll = CreateScrollView(
            panel.transform,
            "ItemsScroll",
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            new Vector2(6f, 6f),
            new Vector2(-6f, -88f),
            out RectTransform content,
            out _);

        VerticalLayoutGroup listLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        listLayout.padding = new RectOffset(2, 2, 2, 2);
        listLayout.spacing = 3f;
        listLayout.childAlignment = TextAnchor.UpperLeft;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = false;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;

        ContentSizeFitter listFitter = content.gameObject.AddComponent<ContentSizeFitter>();
        listFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (characterScroll != null)
            characterScroll.scrollSensitivity = 20f;

        return new CharacterPanelRefs
        {
            Character = character,
            Root = panel,
            Background = panel.GetComponent<Image>(),
            Outline = outline,
            NameText = nameText,
            SummaryText = summaryText,
            Content = content,
            SelectButton = selectButton,
            EquipBestButton = equipBestButton
        };
    }

    private void RefreshCharacterPanel(CharacterPanelRefs refs, bool isSelected)
    {
        if (refs == null || refs.Character == null || refs.Character.Stats == null)
            return;

        Inventory inv = GetInventory(refs.Character);
        refs.NameText.text = refs.Character.Stats.CharacterName;
        refs.Background.color = isSelected
            ? new Color(0.2f, 0.26f, 0.4f, 0.98f)
            : new Color(0.13f, 0.16f, 0.24f, 0.95f);
        refs.Outline.effectColor = isSelected
            ? new Color(0.94f, 0.84f, 0.42f, 0.95f)
            : new Color(0.38f, 0.42f, 0.62f, 0.8f);

        int equippedCount = 0;
        int backpackCount = inv != null ? inv.ItemCount : 0;

        if (refs.Content != null)
        {
            for (int i = refs.Content.childCount - 1; i >= 0; i--)
                Destroy(refs.Content.GetChild(i).gameObject);
        }

        if (inv == null)
        {
            refs.SummaryText.text = "No inventory component.";
            return;
        }

        CreateCharacterSectionHeader(refs.Content, "Equipped Items");
        for (int i = 0; i < Inventory.AllEquipmentSlots.Length; i++)
        {
            EquipSlot slot = Inventory.AllEquipmentSlots[i];
            ItemData equipped = inv.GetEquipped(slot);
            if (equipped == null)
                continue;

            equippedCount++;
            string label = $"[E] {equipped.IconChar} {equipped.Name} ({slot})";
            Button b = CreateListButton(refs.Content, label, new Color(0.4f, 0.26f, 0.2f, 1f), () =>
            {
                ShowMessage("Cannot transfer equipped items. Unequip first.", false);
            });
            AttachTooltip(b.gameObject, () => equipped);
        }

        if (equippedCount == 0)
            CreateListInfo(refs.Content, "(none)");

        CreateCharacterSectionHeader(refs.Content, "Backpack");

        int listedBackpackItems = 0;
        for (int slotIndex = 0; slotIndex < inv.GeneralSlots.Length; slotIndex++)
        {
            ItemData backpackItem = inv.GeneralSlots[slotIndex];
            if (backpackItem == null)
                continue;

            listedBackpackItems++;
            int capturedIndex = slotIndex;
            string label = $"{backpackItem.IconChar} {backpackItem.Name}";
            Button b = CreateListButton(refs.Content, label, new Color(0.18f, 0.34f, 0.46f, 1f), () =>
            {
                if (TryTransferCharacterItemToStash(refs.Character, capturedIndex, out string feedback))
                    ShowMessage(feedback, true);
                else
                    ShowMessage(feedback, false);

                RefreshAll();
            });
            AttachTooltip(b.gameObject, () => backpackItem);
        }

        if (listedBackpackItems == 0)
            CreateListInfo(refs.Content, "(empty)");

        refs.SummaryText.text = $"Equipped: {equippedCount}  •  Backpack: {backpackCount}/{Inventory.GeneralSlotCount}";
    }

    private void RefreshStashList()
    {
        if (_stashContent == null)
            return;

        for (int i = _stashContent.childCount - 1; i >= 0; i--)
            Destroy(_stashContent.GetChild(i).gameObject);

        RefreshStashControlsText();

        if (_stash == null)
        {
            CreateListInfo(_stashContent, "Stash unavailable.");
            return;
        }

        List<StashGroup> groups = BuildStashGroups();
        ApplySort(groups);

        bool locked = _stash.IsLocked;
        CharacterController selectedCharacter = GetSelectedCharacter();

        if (groups.Count == 0)
        {
            CreateListInfo(_stashContent, "No items matching current filter.");
            return;
        }

        for (int i = 0; i < groups.Count; i++)
        {
            StashGroup group = groups[i];
            ItemData sample = group.Prototype;
            string icon = string.IsNullOrWhiteSpace(sample.IconChar) ? "•" : sample.IconChar;
            string itemName = string.IsNullOrWhiteSpace(sample.Name) ? "Unknown Item" : sample.Name;
            string label = $"{icon} {itemName}   x{group.Instances.Count}";

            ItemData transferItem = group.Instances.Count > 0 ? group.Instances[0] : null;

            Button button = CreateListButton(_stashContent, label, new Color(0.22f, 0.32f, 0.62f, 1f), () =>
            {
                if (transferItem == null)
                {
                    ShowMessage("Item entry is invalid.", false);
                    RefreshAll();
                    return;
                }

                if (TryTransferFromStashToCharacter(transferItem, selectedCharacter, out string feedback))
                    ShowMessage(feedback, true);
                else
                    ShowMessage(feedback, false);

                RefreshAll();
            });

            button.interactable = !locked && selectedCharacter != null;
            AttachTooltip(button.gameObject, () => sample);
        }
    }

    private List<StashGroup> BuildStashGroups()
    {
        List<StashGroup> groups = new List<StashGroup>();
        if (_stash == null)
            return groups;

        IReadOnlyList<ItemData> items = _stash.GetItems();
        Dictionary<string, StashGroup> map = new Dictionary<string, StashGroup>();

        for (int i = 0; i < items.Count; i++)
        {
            ItemData item = items[i];
            if (item == null || !PassesFilter(item))
                continue;

            string key = BuildGroupKey(item);
            if (!map.TryGetValue(key, out StashGroup group))
            {
                group = new StashGroup
                {
                    Key = key,
                    Prototype = item
                };
                map[key] = group;
                groups.Add(group);
            }

            group.Instances.Add(item);
        }

        return groups;
    }

    private string BuildGroupKey(ItemData item)
    {
        string id = item != null ? item.Id : string.Empty;
        string name = item != null ? item.Name : string.Empty;
        return $"{id}|{name}";
    }

    private bool PassesFilter(ItemData item)
    {
        if (item == null)
            return false;

        switch (_stashFilter)
        {
            case StashFilterMode.Weapons:
                return item.Type == ItemType.Weapon;
            case StashFilterMode.Armor:
                return item.Type == ItemType.Armor;
            case StashFilterMode.Shields:
                return item.Type == ItemType.Shield;
            case StashFilterMode.Consumables:
                return item.Type == ItemType.Consumable;
            case StashFilterMode.Misc:
                return item.Type == ItemType.Misc;
            case StashFilterMode.All:
            default:
                return true;
        }
    }

    private void ApplySort(List<StashGroup> groups)
    {
        if (groups == null)
            return;

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
                    int typeCmp = ia.Type.CompareTo(ib.Type);
                    if (typeCmp != 0)
                        return typeCmp;
                    break;
                case StashSortMode.Weight:
                    int weightCmp = ia.WeightLbs.CompareTo(ib.WeightLbs);
                    if (weightCmp != 0)
                        return weightCmp;
                    break;
            }

            return string.Compare(ia.Name, ib.Name, StringComparison.OrdinalIgnoreCase);
        });
    }

    private void OnFilterButtonPressed()
    {
        int next = ((int)_stashFilter + 1) % Enum.GetValues(typeof(StashFilterMode)).Length;
        _stashFilter = (StashFilterMode)next;
        RefreshStashList();
    }

    private void OnSortButtonPressed()
    {
        int next = ((int)_stashSort + 1) % Enum.GetValues(typeof(StashSortMode)).Length;
        _stashSort = (StashSortMode)next;
        RefreshStashList();
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

        if (_partyMembers == null || _partyMembers.Count == 0)
        {
            ShowMessage("No party members available.", false);
            return;
        }

        List<ItemData> snapshot = _stash.GetItemsSnapshot();
        if (snapshot.Count == 0)
        {
            ShowMessage("Stash is already empty.", false);
            return;
        }

        int moved = 0;
        int probeStart = Mathf.Max(0, _selectedCharacterIndex);

        for (int i = 0; i < snapshot.Count; i++)
        {
            ItemData item = snapshot[i];
            if (item == null)
                continue;

            CharacterController receiver = FindCharacterWithFreeSpace(probeStart);
            if (receiver == null)
                break;

            if (TryTransferFromStashToCharacter(item, receiver, out _))
            {
                moved++;
                probeStart = (_partyMembers.IndexOf(receiver) + 1) % Mathf.Max(1, _partyMembers.Count);
            }
        }

        int remaining = _stash.Count;
        if (moved > 0)
            ShowMessage($"Moved {moved} item(s) from stash. Remaining: {remaining}.", true);
        else
            ShowMessage("Could not move any stash items (party inventories are full).", false);

        RefreshAll();
    }

    private void OnEquipBestForCharacter(int characterIndex)
    {
        if (characterIndex < 0 || characterIndex >= _partyMembers.Count)
        {
            ShowMessage("Invalid character selection.", false);
            return;
        }

        CharacterController character = _partyMembers[characterIndex];
        Inventory inv = GetInventory(character);
        if (inv == null)
        {
            ShowMessage("Character has no inventory.", false);
            return;
        }

        int changed = 0;
        changed += EquipBestForSlot(inv, EquipSlot.ArmorRobe, item => item != null && item.Type == ItemType.Armor, item => item.ArmorBonus);
        changed += EquipBestForSlot(inv, EquipSlot.LeftHand, item => item != null && item.Type == ItemType.Shield, item => item.ShieldBonus);
        changed += EquipBestForSlot(inv, EquipSlot.RightHand, item => item != null && item.Type == ItemType.Weapon, ScoreWeapon);

        if (changed > 0)
            ShowMessage($"{character.Stats.CharacterName}: equipped best available gear ({changed} change(s)).", true);
        else
            ShowMessage($"{character.Stats.CharacterName}: no better gear found.", false);

        RefreshAll();
    }

    private int EquipBestForSlot(Inventory inv, EquipSlot slot, Func<ItemData, bool> predicate, Func<ItemData, int> scoreFn)
    {
        if (inv == null)
            return 0;

        int bestIndex = -1;
        int bestScore = int.MinValue;

        for (int i = 0; i < inv.GeneralSlots.Length; i++)
        {
            ItemData item = inv.GeneralSlots[i];
            if (item == null)
                continue;

            if (!item.CanEquipIn(slot))
                continue;

            if (predicate != null && !predicate(item))
                continue;

            int score = scoreFn != null ? scoreFn(item) : 0;
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
            return 0;

        return inv.EquipFromInventory(bestIndex, slot) ? 1 : 0;
    }

    private int ScoreWeapon(ItemData weapon)
    {
        if (weapon == null)
            return int.MinValue;

        int diceScore = Mathf.Max(1, weapon.DamageCount) * Mathf.Max(1, weapon.DamageDice);
        int enhancement = weapon.GetEnhancementAttackBonus() + weapon.GetEnhancementDamageBonus();
        return diceScore + weapon.BonusDamage + enhancement;
    }

    private bool TryTransferFromStashToCharacter(ItemData item, CharacterController character, out string feedback)
    {
        feedback = "";

        if (_stash == null)
        {
            feedback = "Stash unavailable.";
            return false;
        }

        if (_stash.IsLocked)
        {
            feedback = "Stash is locked during combat.";
            return false;
        }

        if (item == null)
        {
            feedback = "Invalid item.";
            return false;
        }

        if (character == null || character.Stats == null)
        {
            feedback = "Select a character first.";
            return false;
        }

        Inventory inv = GetInventory(character);
        if (inv == null)
        {
            feedback = $"{character.Stats.CharacterName} has no inventory.";
            return false;
        }

        if (inv.EmptySlots <= 0)
        {
            feedback = $"{character.Stats.CharacterName}'s backpack is full.";
            return false;
        }

        if (!_stash.RemoveItem(item))
        {
            feedback = "Item no longer exists in stash.";
            return false;
        }

        if (!inv.AddItem(item))
        {
            _stash.AddItem(item);
            feedback = $"{character.Stats.CharacterName}'s backpack is full.";
            return false;
        }

        feedback = $"Transferred {item.Name} to {character.Stats.CharacterName}.";
        return true;
    }

    private bool TryTransferCharacterItemToStash(CharacterController character, int inventorySlotIndex, out string feedback)
    {
        feedback = "";

        if (_stash == null)
        {
            feedback = "Stash unavailable.";
            return false;
        }

        if (_stash.IsLocked)
        {
            feedback = "Stash is locked during combat.";
            return false;
        }

        if (character == null || character.Stats == null)
        {
            feedback = "Invalid character.";
            return false;
        }

        Inventory inv = GetInventory(character);
        if (inv == null)
        {
            feedback = $"{character.Stats.CharacterName} has no inventory.";
            return false;
        }

        if (inventorySlotIndex < 0 || inventorySlotIndex >= inv.GeneralSlots.Length)
        {
            feedback = "Invalid inventory slot.";
            return false;
        }

        ItemData item = inv.GeneralSlots[inventorySlotIndex];
        if (item == null)
        {
            feedback = "No item in that slot.";
            return false;
        }

        ItemData removed = inv.RemoveItemAt(inventorySlotIndex);
        if (removed == null)
        {
            feedback = "Failed to remove item from character inventory.";
            return false;
        }

        if (!_stash.AddItem(removed))
        {
            inv.AddItem(removed);
            feedback = "Failed to add item to stash.";
            return false;
        }

        feedback = $"Transferred {removed.Name} to stash.";
        return true;
    }

    private CharacterController FindCharacterWithFreeSpace(int startingIndex)
    {
        if (_partyMembers == null || _partyMembers.Count == 0)
            return null;

        int start = Mathf.Clamp(startingIndex, 0, _partyMembers.Count - 1);
        for (int i = 0; i < _partyMembers.Count; i++)
        {
            CharacterController candidate = _partyMembers[(start + i) % _partyMembers.Count];
            Inventory inv = GetInventory(candidate);
            if (inv != null && inv.EmptySlots > 0)
                return candidate;
        }

        return null;
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

        InventoryComponent inventoryComponent = character.GetComponent<InventoryComponent>();
        return inventoryComponent != null ? inventoryComponent.CharacterInventory : null;
    }

    private void ShowMessage(string message, bool success)
    {
        if (_messageText == null)
            return;

        _messageText.text = message;
        _messageText.color = success
            ? new Color(0.58f, 0.95f, 0.62f)
            : new Color(0.96f, 0.66f, 0.54f);
    }

    private void OnBeginCombatPressed()
    {
        if (_stash != null && _stash.IsLocked)
        {
            ShowMessage("Stash is locked. Cannot begin from this screen.", false);
            return;
        }

        Close();
        _onBeginCombat?.Invoke();
    }

    private void OnSkipPressed()
    {
        Close();
        _onSkipInventory?.Invoke();
    }

    private void OnBackPressed()
    {
        Close();
        _onBack?.Invoke();
    }

    private string GetFilterLabel(StashFilterMode mode)
    {
        switch (mode)
        {
            case StashFilterMode.Weapons: return "Weapons";
            case StashFilterMode.Armor: return "Armor";
            case StashFilterMode.Shields: return "Shields";
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
            case StashSortMode.Weight: return "Weight";
            default: return "Name";
        }
    }

    private void AttachTooltip(GameObject target, Func<ItemData> itemResolver)
    {
        if (target == null)
            return;

        EventTrigger trigger = target.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = target.AddComponent<EventTrigger>();

        if (trigger.triggers == null)
            trigger.triggers = new List<EventTrigger.Entry>();

        AddEventTrigger(trigger, EventTriggerType.PointerEnter, _ =>
        {
            ItemData item = itemResolver != null ? itemResolver() : null;
            if (item == null)
            {
                HideTooltip();
                return;
            }

            ShowTooltip(item);
        });

        AddEventTrigger(trigger, EventTriggerType.PointerExit, _ => HideTooltip());
    }

    private void AddEventTrigger(EventTrigger trigger, EventTriggerType type, Action<BaseEventData> callback)
    {
        if (trigger == null || callback == null)
            return;

        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(data => callback(data));
        trigger.triggers.Add(entry);
    }

    private void ShowTooltip(ItemData item)
    {
        if (_tooltipPanel == null || _tooltipText == null || item == null)
            return;

        string statSummary = item.GetStatSummary();
        string description = string.IsNullOrWhiteSpace(item.Description) ? "No description." : item.Description;
        _tooltipText.text = $"<b>{item.Name}</b>\n{statSummary}\n\n{description}";

        RectTransform tooltipRect = _tooltipPanel.GetComponent<RectTransform>();
        RectTransform panelRect = _panel != null ? _panel.GetComponent<RectTransform>() : null;
        if (tooltipRect != null && panelRect != null)
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(panelRect, Input.mousePosition, null, out localPoint);

            float x = Mathf.Clamp(localPoint.x + 180f, -panelRect.rect.width * 0.5f + 170f, panelRect.rect.width * 0.5f - 170f);
            float y = Mathf.Clamp(localPoint.y - 20f, -panelRect.rect.height * 0.5f + 95f, panelRect.rect.height * 0.5f - 95f);
            tooltipRect.anchoredPosition = new Vector2(x, y);
        }

        _tooltipPanel.SetActive(true);
    }

    private void HideTooltip()
    {
        if (_tooltipPanel != null)
            _tooltipPanel.SetActive(false);
    }

    private void CreateCharacterSectionHeader(Transform parent, string label)
    {
        CreateListInfo(parent, label, new Color(0.93f, 0.83f, 0.48f), FontStyle.Bold);
    }

    private void CreateListInfo(Transform parent, string label)
    {
        CreateListInfo(parent, label, new Color(0.72f, 0.79f, 0.9f), FontStyle.Italic);
    }

    private void CreateListInfo(Transform parent, string label, Color color, FontStyle style)
    {
        GameObject info = new GameObject("Info", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        info.transform.SetParent(parent, false);

        LayoutElement le = info.GetComponent<LayoutElement>();
        le.preferredHeight = 18f;

        Text t = info.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 11;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAnchor.MiddleLeft;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.text = label;
    }

    private Button CreateListButton(Transform parent, string label, Color color, Action onClick)
    {
        GameObject buttonGO = new GameObject("ListButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonGO.transform.SetParent(parent, false);

        LayoutElement le = buttonGO.GetComponent<LayoutElement>();
        le.preferredHeight = 26f;
        le.minHeight = 24f;

        Image image = buttonGO.GetComponent<Image>();
        image.color = color;

        Button button = buttonGO.GetComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.2f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.85f);
        button.colors = colors;

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
            new Vector2(-12f, 0f),
            11,
            FontStyle.Normal,
            Color.white,
            TextAnchor.MiddleLeft);

        return button;
    }

    private Button CreateFooterButton(Transform parent, string label, Color color, Action onClick)
    {
        GameObject buttonGO = new GameObject($"Footer_{label}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonGO.transform.SetParent(parent, false);

        LayoutElement le = buttonGO.GetComponent<LayoutElement>();
        le.preferredHeight = 38f;

        Image image = buttonGO.GetComponent<Image>();
        image.color = color;

        Button button = buttonGO.GetComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.16f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.22f, 0.22f, 0.22f, 0.9f);
        button.colors = colors;

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
            15,
            FontStyle.Bold,
            Color.white,
            TextAnchor.MiddleCenter);

        return button;
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

        Button button = buttonGO.GetComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.2f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.22f, 0.22f, 0.22f, 0.9f);
        button.colors = colors;

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

    private ScrollRect CreateScrollView(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 offsetMin,
        Vector2 offsetMax,
        out RectTransform contentRect,
        out RectTransform viewportRect)
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
        rootImage.color = new Color(0.04f, 0.05f, 0.09f, 0.92f);

        ScrollRect scrollRect = root.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(root.transform, false);

        viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0f, 0f);
        viewportRect.anchorMax = new Vector2(1f, 1f);
        viewportRect.offsetMin = new Vector2(4f, 4f);
        viewportRect.offsetMax = new Vector2(-4f, -4f);

        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.05f);

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

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;

        return scrollRect;
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

        return text;
    }
}
