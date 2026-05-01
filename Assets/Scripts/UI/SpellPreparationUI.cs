using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Spell preparation UI for D&D 3.5e Wizards and Clerics.
/// Allows the caster to assign spells into individual spell slots.
/// Each slot can hold one spell; the same spell can be prepared in multiple slots.
///
/// D&D 3.5e Rules:
///   - After rest, casters can re-prepare spells by filling each slot with a spell.
///   - Wizards prepare from their spellbook (limited selection).
///   - Clerics prepare from the FULL list of cleric spells at each level.
///   - Each slot holds exactly one spell of that slot's level.
///   - The same spell can fill multiple slots.
///   - Cantrips use slots for preparation but are UNLIMITED use (never consumed).
///   - Prepared spells persist between rests unless re-prepared.
///
/// Usage: Called from GameManager when caster chooses to re-prepare spells after rest,
/// or from character creation to set initial preparation.
/// </summary>
public class SpellPreparationUI : MonoBehaviour
{
    // ========== PUBLIC ==========
    /// <summary>Callback when preparation is confirmed.</summary>
    public Action OnPreparationConfirmed;

    /// <summary>Callback when preparation is confirmed during character creation.
    /// Returns list of spell IDs in slot order (one per slot).</summary>
    public Action<List<string>> OnCreationPreparationConfirmed;

    /// <summary>Whether the preparation UI is currently open.</summary>
    public bool IsOpen { get; private set; }

    // ========== PRIVATE ==========
    private Font _font;
    private GameObject _overlayPanel;
    private GameObject _rootPanel;
    private SpellcastingComponent _spellComp;
    private Text _titleText;
    private Text _slotSummaryText;
    private Button _confirmButton;
    private Button _autoPrepareButton;
    private Button _skipCharacterButton;
    private Button _backToMenuButton;
    private Button _startEncounterButton;

    // Pre-combat multi-character preparation flow
    private readonly List<CharacterController> _preparingCharacters = new List<CharacterController>();
    private int _currentCharacterIndex = -1;
    private Action _preCombatCompleteCallback;
    private Action _preCombatBackCallback;
    private Action _preCombatStartEncounterCallback;
    private bool _isPreCombatFlowActive;

    // Slot UI rows
    private List<SlotRowUI> _slotRows = new List<SlotRowUI>();

    // Creation mode state
    private bool _isCreationMode;
    private bool _isClericCreationMode; // true when creating a cleric (affects labels)
    private List<SpellSlot> _creationSlots = new List<SpellSlot>();
    private List<SpellData> _creationKnownSpells = new List<SpellData>();
    private int[] _creationSlotsMax;

    // Domain slot tracking for clerics
    private List<string> _clericDomains = new List<string>(); // chosen domain names
    private List<SpellData> _domainSpellsLevel1 = new List<SpellData>();
    private List<SpellData> _domainSpellsLevel2 = new List<SpellData>();
    private HashSet<int> _domainSlotIndices = new HashSet<int>(); // which slot indices are domain slots

    // Layout
    private const float PANEL_W = 1200f;
    private const float PANEL_H = 860f;

    private class SlotRowUI
    {
        public GameObject Row;
        public Text LabelText;
        public Dropdown SpellDropdown;
        public SpellSlot Slot;
        public int SlotIndex;
        public List<SpellData> AvailableSpells; // Spells for dropdown at this level
    }

    // ========== BUILD UI ==========

    public void BuildUI(Canvas canvas)
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        // Dark overlay
        _overlayPanel = MakePanel(canvas.transform, "SpellPrepOverlay",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.85f));
        var overlayRT = _overlayPanel.GetComponent<RectTransform>();
        overlayRT.offsetMin = Vector2.zero;
        overlayRT.offsetMax = Vector2.zero;

        // Main panel
        _rootPanel = MakePanel(_overlayPanel.transform, "SpellPrepPanel",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, new Color(0.12f, 0.12f, 0.18f, 0.98f));

        float headerTop = PANEL_H / 2f;

        // Title
        _titleText = MakeText(_rootPanel.transform, "Title",
            new Vector2(0, headerTop - 30), new Vector2(PANEL_W - 40, 40),
            "SPELL PREPARATION", 30, Color.white, TextAnchor.MiddleCenter);

        // Slot summary
        _slotSummaryText = MakeText(_rootPanel.transform, "Summary",
            new Vector2(0, headerTop - 62), new Vector2(PANEL_W - 40, 24),
            "", 18, new Color(0.7f, 0.8f, 1f), TextAnchor.MiddleCenter);

        // Scroll area for slot list
        float contentTop = headerTop - 90;
        float contentBottom = -PANEL_H / 2f + 80;
        float contentH = contentTop - contentBottom;
        float contentCenterY = (contentTop + contentBottom) / 2f;

        GameObject scrollArea = MakePanel(_rootPanel.transform, "ScrollArea",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, contentCenterY), new Vector2(PANEL_W - 40, contentH),
            new Color(0.08f, 0.08f, 0.12f, 0.9f));

        // Viewport with mask
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollArea.transform, false);
        var vpRT = viewport.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = new Vector2(4, 4);
        vpRT.offsetMax = new Vector2(-4, -4);
        viewport.AddComponent<Image>().color = Color.white;
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        var scrollContent = new GameObject("Content");
        scrollContent.transform.SetParent(viewport.transform, false);
        var scrollContentRT = scrollContent.AddComponent<RectTransform>();
        scrollContentRT.anchorMin = new Vector2(0, 1);
        scrollContentRT.anchorMax = new Vector2(1, 1);
        scrollContentRT.pivot = new Vector2(0.5f, 1);
        scrollContentRT.anchoredPosition = Vector2.zero;
        scrollContentRT.sizeDelta = new Vector2(0, 0);

        var scrollRect = scrollArea.AddComponent<ScrollRect>();
        scrollRect.content = scrollContentRT;
        scrollRect.viewport = vpRT;
        scrollRect.vertical = true;
        scrollRect.horizontal = false;
        scrollRect.scrollSensitivity = 30f;

        ScrollbarHelper.CreateVerticalScrollbar(scrollRect, scrollArea.transform);

        // Bottom buttons
        float btnY = -PANEL_H / 2f + 35;

        _autoPrepareButton = MakeButton(_rootPanel.transform, "AutoPrepBtn",
            new Vector2(-165, btnY), new Vector2(150, 40),
            "Auto-Prepare ⚡", new Color(0.3f, 0.3f, 0.5f), Color.white, 14);
        _autoPrepareButton.onClick.AddListener(OnAutoPrepare);

        _confirmButton = MakeButton(_rootPanel.transform, "ConfirmBtn",
            new Vector2(0, btnY), new Vector2(150, 40),
            "Confirm Preparation ✓", new Color(0.2f, 0.6f, 0.2f), Color.white, 14);
        _confirmButton.onClick.AddListener(OnConfirm);

        _skipCharacterButton = MakeButton(_rootPanel.transform, "SkipCharacterBtn",
            new Vector2(-330, btnY), new Vector2(150, 40),
            "Skip", new Color(0.45f, 0.35f, 0.18f), Color.white, 14);
        _skipCharacterButton.onClick.AddListener(OnSkipCharacterPressed);
        _skipCharacterButton.gameObject.SetActive(false);

        _backToMenuButton = MakeButton(_rootPanel.transform, "BackToMenuBtn",
            new Vector2(165, btnY), new Vector2(150, 40),
            "Back", new Color(0.5f, 0.24f, 0.24f), Color.white, 14);
        _backToMenuButton.onClick.AddListener(OnBackToMenuPressed);
        _backToMenuButton.gameObject.SetActive(false);

        _startEncounterButton = MakeButton(_rootPanel.transform, "StartEncounterBtn",
            new Vector2(330, btnY), new Vector2(150, 40),
            "Start", new Color(0.2f, 0.56f, 0.26f), Color.white, 14);
        _startEncounterButton.onClick.AddListener(OnStartEncounterPressed);
        _startEncounterButton.gameObject.SetActive(false);

        _overlayPanel.SetActive(false);
    }

    // ========== OPEN / CLOSE ==========

    /// <summary>
    /// Open pre-combat spell preparation flow for all prepared casters in the party.
    /// Characters are shown one at a time and move forward on confirm/skip.
    /// </summary>
    public void Show(List<CharacterController> party, Action onCompleteCallback)
    {
        Show(party, onCompleteCallback, null, null);
    }

    public void Show(
        List<CharacterController> party,
        Action onCompleteCallback,
        Action onBackToMenuCallback,
        Action onStartEncounterCallback)
    {
        Debug.Log("[SpellPrep] Opening spell preparation UI (FULLSCREEN)");

        EnsureBuilt();
        if (_overlayPanel == null || _rootPanel == null)
        {
            Debug.LogWarning("[SpellPrep] UI was not built; skipping spell preparation flow.");
            onCompleteCallback?.Invoke();
            return;
        }

        _preCombatCompleteCallback = onCompleteCallback;
        _preCombatBackCallback = onBackToMenuCallback;
        _preCombatStartEncounterCallback = onStartEncounterCallback;
        _isPreCombatFlowActive = true;
        _preparingCharacters.Clear();

        if (party != null)
        {
            for (int i = 0; i < party.Count; i++)
            {
                CharacterController character = party[i];
                if (DoesPrepareSpells(character))
                    _preparingCharacters.Add(character);
            }
        }

        Debug.Log($"[SpellPrep] {_preparingCharacters.Count} characters need to prepare spells");

        if (_preparingCharacters.Count == 0)
        {
            _isPreCombatFlowActive = false;
            Action callback = _preCombatCompleteCallback;
            _preCombatCompleteCallback = null;
            _preCombatBackCallback = null;
            _preCombatStartEncounterCallback = null;
            callback?.Invoke();
            return;
        }

        _currentCharacterIndex = 0;
        ShowCurrentCharacterFromFlow();
    }

    private bool DoesPrepareSpells(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return false;

        string className = character.Stats.CharacterClass ?? string.Empty;
        if (className.Equals("Sorcerer", StringComparison.OrdinalIgnoreCase))
            return false;

        bool isPreparedCasterClass =
            className.Equals("Wizard", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("Cleric", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("Druid", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("Bard", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("Paladin", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("Ranger", StringComparison.OrdinalIgnoreCase);

        if (!isPreparedCasterClass)
            return false;

        SpellcastingComponent spellComp = character.GetComponent<SpellcastingComponent>();
        if (spellComp == null)
            return false;

        if (spellComp.SpellSlots == null || spellComp.SpellSlots.Count == 0)
        {
            Debug.Log($"[SpellPrep] {character.Stats.CharacterName} is a prepared caster but has no spell slots yet; skipping.");
            return false;
        }

        return true;
    }

    private void ShowCurrentCharacterFromFlow()
    {
        if (_currentCharacterIndex < 0 || _currentCharacterIndex >= _preparingCharacters.Count)
        {
            FinishPreparationFlow();
            return;
        }

        CharacterController character = _preparingCharacters[_currentCharacterIndex];
        SpellcastingComponent spellComp = character != null ? character.GetComponent<SpellcastingComponent>() : null;
        if (spellComp == null)
        {
            Debug.LogWarning("[SpellPrep] Character missing SpellcastingComponent during flow; advancing.");
            AdvanceToNextCharacter();
            return;
        }

        Debug.Log($"[SpellPrep] Showing preparation for {character.Stats.CharacterName}");

        UpdatePreCombatButtonState();
        Open(spellComp);

        string className = character.Stats != null ? character.Stats.CharacterClass : "Caster";
        _titleText.text = $"SPELL PREPARATION — {character.Stats.CharacterName} ({className}) [{_currentCharacterIndex + 1}/{_preparingCharacters.Count}]";
        OnPreparationConfirmed = OnCurrentCharacterDone;
    }

    private void OnCurrentCharacterDone()
    {
        if (!_isPreCombatFlowActive)
            return;

        CharacterController current = (_currentCharacterIndex >= 0 && _currentCharacterIndex < _preparingCharacters.Count)
            ? _preparingCharacters[_currentCharacterIndex]
            : null;
        Debug.Log($"[SpellPrep] Done with {(current != null && current.Stats != null ? current.Stats.CharacterName : "unknown character")}");

        AdvanceToNextCharacter();
    }

    private void OnSkipCharacterPressed()
    {
        if (!_isPreCombatFlowActive)
            return;

        CharacterController current = (_currentCharacterIndex >= 0 && _currentCharacterIndex < _preparingCharacters.Count)
            ? _preparingCharacters[_currentCharacterIndex]
            : null;
        Debug.Log($"[SpellPrep] Skipping {(current != null && current.Stats != null ? current.Stats.CharacterName : "unknown character")}");

        AdvanceToNextCharacter();
    }

    private void OnBackToMenuPressed()
    {
        if (!_isPreCombatFlowActive)
            return;

        Debug.Log("[SpellPrep] Back to pre-combat menu requested.");
        _isPreCombatFlowActive = false;
        OnPreparationConfirmed = null;
        Close();

        Action callback = _preCombatBackCallback;
        _preCombatCompleteCallback = null;
        _preCombatBackCallback = null;
        _preCombatStartEncounterCallback = null;
        callback?.Invoke();
    }

    private void OnStartEncounterPressed()
    {
        if (!_isPreCombatFlowActive)
            return;

        Debug.Log("[SpellPrep] Start encounter requested from spell preparation window.");
        _isPreCombatFlowActive = false;
        OnPreparationConfirmed = null;
        Close();

        Action callback = _preCombatStartEncounterCallback;
        _preCombatCompleteCallback = null;
        _preCombatBackCallback = null;
        _preCombatStartEncounterCallback = null;
        callback?.Invoke();
    }

    private void AdvanceToNextCharacter()
    {
        _currentCharacterIndex++;
        if (_currentCharacterIndex < _preparingCharacters.Count)
        {
            ShowCurrentCharacterFromFlow();
            return;
        }

        FinishPreparationFlow();
    }

    private void FinishPreparationFlow()
    {
        Debug.Log("[SpellPrep] All characters prepared.");

        _isPreCombatFlowActive = false;
        OnPreparationConfirmed = null;

        if (_skipCharacterButton != null)
            _skipCharacterButton.gameObject.SetActive(false);
        if (_backToMenuButton != null)
            _backToMenuButton.gameObject.SetActive(false);
        if (_startEncounterButton != null)
            _startEncounterButton.gameObject.SetActive(false);

        Close();

        Action callback = _preCombatCompleteCallback;
        _preCombatCompleteCallback = null;
        _preCombatBackCallback = null;
        _preCombatStartEncounterCallback = null;
        callback?.Invoke();
    }

    private void UpdatePreCombatButtonState()
    {
        if (_skipCharacterButton != null)
            _skipCharacterButton.gameObject.SetActive(_isPreCombatFlowActive);

        if (_backToMenuButton != null)
            _backToMenuButton.gameObject.SetActive(_isPreCombatFlowActive && _preCombatBackCallback != null);

        if (_startEncounterButton != null)
            _startEncounterButton.gameObject.SetActive(_isPreCombatFlowActive && _preCombatStartEncounterCallback != null);

        if (_confirmButton != null)
        {
            Text label = _confirmButton.GetComponentInChildren<Text>();
            if (label != null)
                label.text = _isPreCombatFlowActive ? "Done" : "Confirm Preparation ✓";
        }

        if (_autoPrepareButton != null)
            _autoPrepareButton.gameObject.SetActive(true);
    }

    private void EnsureBuilt()
    {
        if (_overlayPanel != null && _rootPanel != null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            Debug.LogError("[SpellPrep] No Canvas found. Cannot build SpellPreparationUI.");
            return;
        }

        BuildUI(canvas);
    }

    /// <summary>
    /// Open the spell preparation UI for a prepared caster character.
    /// Shows all spell slots with dropdowns to select spells.
    /// </summary>
    public void Open(SpellcastingComponent spellComp)
    {
        if (spellComp == null || spellComp.Stats == null)
        {
            Debug.LogWarning("[SpellPreparationUI] Missing spellcasting data; cannot open.");
            return;
        }

        if (spellComp.SpellSlots == null || spellComp.SpellSlots.Count == 0)
        {
            Debug.LogWarning("[SpellPreparationUI] Character has no spell slots to prepare.");
            return;
        }

        _spellComp = spellComp;
        _clericDomains = spellComp.Domains != null ? new List<string>(spellComp.Domains) : new List<string>();
        _domainSlotIndices.Clear();

        string domainSuffix = (_clericDomains.Count > 0 && spellComp.Stats.IsCleric)
            ? $" — Domains: {string.Join(", ", _clericDomains)}"
            : string.Empty;
        _titleText.text = $"SPELL PREPARATION — {spellComp.Stats.CharacterName}{domainSuffix}";

        PopulateSlotList();
        RefreshSummary();

        _overlayPanel.SetActive(true);
        IsOpen = true;
    }

    /// <summary>
    /// Open the spell preparation UI during character creation (no SpellcastingComponent yet).
    /// Creates temporary spell slots based on the character's INT modifier and spellbook.
    /// D&D 3.5e Bonus Spell Slots (PHB p.8):
    ///   Modifier >= spell level → +1 bonus slot at that level
    ///   e.g., INT 17 (+3 mod): +1 at 1st, +1 at 2nd, +1 at 3rd
    /// </summary>
    /// <param name="spellbookIds">All spell IDs in the wizard's spellbook (from Step 1)</param>
    /// <param name="intModifier">The wizard's INT modifier (for bonus slots)</param>
    /// <param name="characterLevel">The character's wizard level (for base slots)</param>
    /// <param name="characterName">Character name for display</param>
    public void OpenForCreation(List<string> spellbookIds, int intModifier, int characterLevel, string characterName)
    {
        _isCreationMode = true;
        _isClericCreationMode = false;
        _spellComp = null;

        SpellDatabase.Init();

        // Build known spells from spellbook IDs
        _creationKnownSpells.Clear();
        foreach (string id in spellbookIds)
        {
            SpellData spell = SpellDatabase.GetSpell(id);
            if (spell != null)
                _creationKnownSpells.Add(spell);
        }

        // Calculate spell slots per D&D 3.5e PHB Table 3-18 (Wizard)
        // Level 3: Base 4/2/1 + bonus from INT modifier (PHB p.8)
        int intMod = Mathf.Max(0, intModifier);
        int bonus1st = intMod >= 1 ? 1 : 0;
        int bonus2nd = intMod >= 2 ? 1 : 0;
        _creationSlotsMax = new int[] { 4, 2 + bonus1st, 1 + bonus2nd };

        // Create temporary spell slots
        _creationSlots.Clear();
        for (int spellLevel = 0; spellLevel < _creationSlotsMax.Length; spellLevel++)
        {
            for (int i = 0; i < _creationSlotsMax[spellLevel]; i++)
            {
                _creationSlots.Add(new SpellSlot(spellLevel));
            }
        }

        // Auto-prepare: distribute spells across slots
        AutoPrepareCreationSlots();

        _titleText.text = $"SPELL PREPARATION — {characterName}";

        PopulateCreationSlotList();
        RefreshCreationSummary();

        _overlayPanel.SetActive(true);
        IsOpen = true;

        Debug.Log($"[SpellPreparationUI] Creation mode: {_creationKnownSpells.Count} spells in book, " +
                  $"slots: L0={_creationSlotsMax[0]}, L1={_creationSlotsMax[1]}, L2={_creationSlotsMax[2]} " +
                  $"(INT mod {intModifier}, bonus 1st={bonus1st}, bonus 2nd={bonus2nd})");
    }

    /// <summary>
    /// Open the spell preparation UI during character creation for a Cleric.
    /// Clerics prepare from the FULL list of cleric spells at each level.
    /// D&D 3.5e Cleric Spell Slots (PHB Table 3-6):
    ///   Level 3 base: 4 orisons, 2 first-level, 1 second-level
    ///   + 1 domain slot at each level 1+ (so +1 at 1st, +1 at 2nd)
    ///   + bonus slots from WIS modifier (PHB p.8):
    ///     WIS mod >= spell level → +1 bonus slot at that level
    ///   Example: WIS 16 (+3 mod) → L0=4, L1=2+1domain+1bonus=4, L2=1+1domain+1bonus=3
    /// </summary>
    /// <param name="wisModifier">The cleric's WIS modifier (for bonus slots)</param>
    /// <param name="characterLevel">The character's cleric level (for base slots)</param>
    /// <param name="characterName">Character name for display</param>
    public void OpenForClericCreation(int wisModifier, int characterLevel, string characterName)
    {
        OpenForClericCreation(wisModifier, characterLevel, characterName, null);
    }

    /// <summary>
    /// Open cleric spell preparation with domain awareness.
    /// Domain slots are the last slot at each spell level 1+.
    /// Domain slots can only be filled with domain spells from the cleric's chosen domains.
    /// </summary>
    public void OpenForClericCreation(int wisModifier, int characterLevel, string characterName, List<string> domainNames)
    {
        _isCreationMode = true;
        _isClericCreationMode = true;
        _spellComp = null;

        SpellDatabase.Init();
        DomainDatabase.Init();

        // Store domain info
        _clericDomains = domainNames ?? new List<string>();
        _domainSpellsLevel1.Clear();
        _domainSpellsLevel2.Clear();
        _domainSlotIndices.Clear();

        // Gather domain spells
        foreach (string domName in _clericDomains)
        {
            DomainData domain = DomainDatabase.GetDomain(domName);
            if (domain == null) continue;

            string s1Id = domain.GetDomainSpellId(1);
            if (s1Id != null)
            {
                SpellData s1 = SpellDatabase.GetSpell(s1Id);
                if (s1 != null && !_domainSpellsLevel1.Contains(s1))
                    _domainSpellsLevel1.Add(s1);
            }

            string s2Id = domain.GetDomainSpellId(2);
            if (s2Id != null)
            {
                SpellData s2 = SpellDatabase.GetSpell(s2Id);
                if (s2 != null && !_domainSpellsLevel2.Contains(s2))
                    _domainSpellsLevel2.Add(s2);
            }
        }

        // Build known spells: ALL cleric spells at each level + domain spells
        _creationKnownSpells.Clear();
        _creationKnownSpells.AddRange(SpellDatabase.GetSpellsForClassAtLevel("Cleric", 0));
        _creationKnownSpells.AddRange(SpellDatabase.GetSpellsForClassAtLevel("Cleric", 1));
        _creationKnownSpells.AddRange(SpellDatabase.GetSpellsForClassAtLevel("Cleric", 2));

        // Add domain spells that aren't already in the cleric spell list
        foreach (var ds in _domainSpellsLevel1)
        {
            if (!_creationKnownSpells.Exists(s => s.SpellId == ds.SpellId))
                _creationKnownSpells.Add(ds);
        }
        foreach (var ds in _domainSpellsLevel2)
        {
            if (!_creationKnownSpells.Exists(s => s.SpellId == ds.SpellId))
                _creationKnownSpells.Add(ds);
        }

        // Calculate spell slots per D&D 3.5e PHB Table 3-6 (Cleric)
        // Level 3: Base 4/2/1 + domain 0/1/1 + bonus from WIS modifier
        int wisMod = Mathf.Max(0, wisModifier);
        int bonus1st = wisMod >= 1 ? 1 : 0;
        int bonus2nd = wisMod >= 2 ? 1 : 0;
        _creationSlotsMax = new int[] { 4, 3 + bonus1st, 2 + bonus2nd }; // base + domain + bonus

        // Create temporary spell slots
        _creationSlots.Clear();
        for (int spellLevel = 0; spellLevel < _creationSlotsMax.Length; spellLevel++)
        {
            for (int i = 0; i < _creationSlotsMax[spellLevel]; i++)
            {
                _creationSlots.Add(new SpellSlot(spellLevel));
            }
        }

        // Mark domain slot indices: LAST slot at each level 1+
        // Domain slot is placed at the bottom of each spell level section
        // so regular slots appear first, then the domain slot below them.
        // For L1: if total=4 (2 base + 1 bonus + 1 domain), domain = last slot (index 3 within level)
        // For L2: if total=3 (1 base + 1 bonus + 1 domain), domain = last slot (index 2 within level)
        int slotIdx = 0;
        for (int spellLevel = 0; spellLevel < _creationSlotsMax.Length; spellLevel++)
        {
            int count = _creationSlotsMax[spellLevel];
            if (spellLevel >= 1 && _clericDomains.Count > 0 && count > 0)
            {
                // Domain slot = last slot within this level (bottom of the list)
                _domainSlotIndices.Add(slotIdx + count - 1);
            }
            slotIdx += count;
        }

        // Auto-prepare: distribute spells across slots
        AutoPrepareCreationSlots();

        // Auto-prepare domain slots with domain spells
        if (_clericDomains.Count > 0)
        {
            foreach (int di in _domainSlotIndices)
            {
                if (di < _creationSlots.Count)
                {
                    SpellSlot domSlot = _creationSlots[di];
                    List<SpellData> domSpells = domSlot.Level == 1 ? _domainSpellsLevel1 : _domainSpellsLevel2;
                    if (domSpells.Count > 0)
                    {
                        domSlot.Prepare(domSpells[0]);
                    }
                }
            }
        }

        string domainStr = _clericDomains.Count > 0 ? $" — Domains: {string.Join(", ", _clericDomains)}" : "";
        _titleText.text = $"CLERIC SPELL PREPARATION — {characterName}{domainStr}";

        PopulateCreationSlotList();
        RefreshCreationSummary();

        _overlayPanel.SetActive(true);
        IsOpen = true;

        Debug.Log($"[SpellPreparationUI] Cleric creation mode: {_creationKnownSpells.Count} total cleric spells, " +
                  $"slots: L0={_creationSlotsMax[0]}, L1={_creationSlotsMax[1]}, L2={_creationSlotsMax[2]} " +
                  $"(WIS mod {wisModifier}, bonus 1st={bonus1st}, bonus 2nd={bonus2nd}), " +
                  $"domains: [{string.Join(", ", _clericDomains)}], domain slots: {_domainSlotIndices.Count}");
    }

    /// <summary>Close the preparation UI.</summary>
    public void Close()
    {
        if (_overlayPanel != null)
            _overlayPanel.SetActive(false);
        IsOpen = false;
        _isCreationMode = false;
        _isClericCreationMode = false;

        if (_skipCharacterButton != null)
            _skipCharacterButton.gameObject.SetActive(false);
        if (_backToMenuButton != null)
            _backToMenuButton.gameObject.SetActive(false);
        if (_startEncounterButton != null)
            _startEncounterButton.gameObject.SetActive(false);

        if (_confirmButton != null)
        {
            Text label = _confirmButton.GetComponentInChildren<Text>();
            if (label != null)
                label.text = "Confirm Preparation ✓";
        }
    }

    // ========== POPULATE ==========

    private void PopulateSlotList()
    {
        Debug.Log($"[SpellPrep] Building spell slots for {_spellComp?.Stats?.CharacterName ?? "Unknown"}");

        // Clear existing rows
        foreach (var row in _slotRows)
        {
            if (row.Row != null) Destroy(row.Row);
        }
        _slotRows.Clear();

        var scrollContent = _rootPanel.transform.Find("ScrollArea/Viewport/Content");
        if (scrollContent == null) return;

        // Clear any leftover children
        foreach (Transform child in scrollContent)
        {
            Destroy(child.gameObject);
        }

        float yPos = -8f;
        float rowHeight = 40f;
        float spacing = 4f;
        _domainSlotIndices.Clear();

        int maxLevel = _spellComp.SlotsMax != null ? _spellComp.SlotsMax.Length - 1 : 0;
        for (int spellLevel = 0; spellLevel <= maxLevel; spellLevel++)
        {
            List<SpellSlot> slotsAtLevel = _spellComp.GetSlotsForLevel(spellLevel);
            if (slotsAtLevel.Count == 0)
                continue;

            Debug.Log($"[SpellPrep] Creating section for spell level {spellLevel}");
            if (spellLevel > 0) yPos -= 8f;

            string cantripName = (_spellComp.Stats != null && _spellComp.Stats.IsCleric)
                ? "ORISONS" : "CANTRIPS";

            int regularSlots = _spellComp.GetMaxSpellSlotsAtLevel(spellLevel);
            int domainSlots = _spellComp.GetMaxDomainSlotsAtLevel(spellLevel);
            int totalSlots = slotsAtLevel.Count;
            Debug.Log($"[SpellPrep] Level {spellLevel}: {regularSlots} regular + {domainSlots} domain = {totalSlots} total");

            string levelLabel = spellLevel == 0
                ? $"═══ {cantripName} (Level 0 — Unlimited) ═══"
                : $"═══ LEVEL {spellLevel} SPELLS ═══";
            levelLabel += $"  ({totalSlots} slots)";

            var headerGO = new GameObject("Header" + spellLevel);
            headerGO.transform.SetParent(scrollContent, false);
            var hRT = headerGO.AddComponent<RectTransform>();
            hRT.anchorMin = new Vector2(0, 1);
            hRT.anchorMax = new Vector2(1, 1);
            hRT.pivot = new Vector2(0.5f, 1);
            hRT.anchoredPosition = new Vector2(0, yPos);
            hRT.sizeDelta = new Vector2(0, 28);
            var hText = headerGO.AddComponent<Text>();
            hText.text = levelLabel;
            hText.font = _font;
            hText.fontSize = 13;
            hText.color = new Color(0.7f, 0.8f, 1f);
            hText.alignment = TextAnchor.MiddleCenter;
            hText.supportRichText = true;
            hText.raycastTarget = false;
            yPos -= 30f;

            for (int levelSlotIndex = 0; levelSlotIndex < slotsAtLevel.Count; levelSlotIndex++)
            {
                SpellSlot slot = slotsAtLevel[levelSlotIndex];
                int slotIndex = _slotRows.Count;
                if (slot.IsDomainSlot)
                    _domainSlotIndices.Add(slotIndex);

                var rowUI = CreateSlotRow(scrollContent, slot, slotIndex, yPos);
                _slotRows.Add(rowUI);

                Debug.Log($"[SpellPrep] Created slot {levelSlotIndex} at level {spellLevel}, domain={slot.IsDomainSlot}");
                yPos -= (rowHeight + spacing);
            }
        }

        // Set content height
        var contentRT = scrollContent.GetComponent<RectTransform>();
        contentRT.sizeDelta = new Vector2(0, Mathf.Abs(yPos) + 12f);
    }

    private SlotRowUI CreateSlotRow(Transform parent, SpellSlot slot, int index, float yPos)
    {
        var row = new SlotRowUI();
        row.Slot = slot;
        row.SlotIndex = index;

        bool isDomainSlot = slot.IsDomainSlot;

        // Domain slots only allow domain spells from chosen domains.
        if (isDomainSlot)
        {
            row.AvailableSpells = _spellComp.GetAvailableDomainSpells(slot.Level)
                .OrderBy(s => s.Name)
                .ToList();
        }
        else
        {
            row.AvailableSpells = _spellComp.KnownSpells
                .Where(s => s != null && s.SpellLevel == slot.Level)
                .OrderBy(s => s.Name)
                .ToList();
        }

        // Row container
        row.Row = new GameObject($"SlotRow_{index}");
        row.Row.transform.SetParent(parent, false);
        var rt = row.Row.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, yPos);
        rt.sizeDelta = new Vector2(0, 40f);

        // Background
        var bg = row.Row.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.15f, 0.6f);

        // Slot label (left side)
        List<SpellSlot> levelSlots = _spellComp.GetSlotsForLevel(slot.Level);
        int levelSlotNum = levelSlots.IndexOf(slot) + 1;
        int domainSlotNum = levelSlots.Where(s => s.IsDomainSlot).ToList().IndexOf(slot) + 1;

        string cantripLabel = (_spellComp.Stats != null && _spellComp.Stats.IsCleric) ? "Orison" : "Cantrip";
        string slotLabel;
        if (slot.Level == 0)
            slotLabel = $"{cantripLabel} Slot {levelSlotNum}:";
        else if (isDomainSlot)
            slotLabel = $"🌟 Domain Slot {domainSlotNum}:";
        else
            slotLabel = $"Level {slot.Level} Slot {levelSlotNum}:";

        Color labelColor = isDomainSlot ? new Color(1f, 0.85f, 0.3f) : new Color(0.8f, 0.8f, 0.7f);
        row.LabelText = MakeText(row.Row.transform, "Label",
            new Vector2(-280, 0), new Vector2(180, 30),
            slotLabel, 13, labelColor, TextAnchor.MiddleRight);

        if (isDomainSlot)
            bg.color = new Color(0.15f, 0.12f, 0.05f, 0.75f);

        // Dropdown for spell selection (right side)
        GameObject dropdownObj = new GameObject("Dropdown");
        dropdownObj.transform.SetParent(row.Row.transform, false);
        var ddRT = dropdownObj.AddComponent<RectTransform>();
        ddRT.anchorMin = new Vector2(0.5f, 0.5f);
        ddRT.anchorMax = new Vector2(0.5f, 0.5f);
        ddRT.pivot = new Vector2(0.5f, 0.5f);
        ddRT.anchoredPosition = new Vector2(60, 0);
        ddRT.sizeDelta = new Vector2(430, 44);

        // Dropdown background image
        var ddBG = dropdownObj.AddComponent<Image>();
        ddBG.color = new Color(0.15f, 0.15f, 0.25f, 1f);

        // Create dropdown component
        var dropdown = dropdownObj.AddComponent<Dropdown>();

        // Label for the dropdown (current value)
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(dropdownObj.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = new Vector2(8, 2);
        labelRT.offsetMax = new Vector2(-28, -2);
        var labelText = labelGO.AddComponent<Text>();
        labelText.font = _font;
        labelText.fontSize = 16;
        labelText.color = Color.white;
        labelText.alignment = TextAnchor.MiddleLeft;

        // Arrow indicator
        var arrowGO = new GameObject("Arrow");
        arrowGO.transform.SetParent(dropdownObj.transform, false);
        var arrowRT = arrowGO.AddComponent<RectTransform>();
        arrowRT.anchorMin = new Vector2(1, 0);
        arrowRT.anchorMax = new Vector2(1, 1);
        arrowRT.pivot = new Vector2(1, 0.5f);
        arrowRT.anchoredPosition = new Vector2(-4, 0);
        arrowRT.sizeDelta = new Vector2(20, 0);
        var arrowText = arrowGO.AddComponent<Text>();
        arrowText.font = _font;
        arrowText.fontSize = 14;
        arrowText.color = new Color(0.7f, 0.7f, 0.9f);
        arrowText.text = "▼";
        arrowText.alignment = TextAnchor.MiddleCenter;

        // Create template for dropdown items
        var templateGO = new GameObject("Template");
        templateGO.transform.SetParent(dropdownObj.transform, false);
        var tempRT = templateGO.AddComponent<RectTransform>();
        tempRT.anchorMin = new Vector2(0, 0);
        tempRT.anchorMax = new Vector2(1, 0);
        tempRT.pivot = new Vector2(0.5f, 1);
        tempRT.anchoredPosition = Vector2.zero;
        tempRT.sizeDelta = new Vector2(0, 200);
        var tempImg = templateGO.AddComponent<Image>();
        tempImg.color = new Color(0.12f, 0.12f, 0.2f, 0.98f);
        var tempScroll = templateGO.AddComponent<ScrollRect>();

        // Viewport in template
        var tempVP = new GameObject("Viewport");
        tempVP.transform.SetParent(templateGO.transform, false);
        var vpRT2 = tempVP.AddComponent<RectTransform>();
        vpRT2.anchorMin = Vector2.zero;
        vpRT2.anchorMax = Vector2.one;
        vpRT2.offsetMin = Vector2.zero;
        vpRT2.offsetMax = Vector2.zero;
        tempVP.AddComponent<Image>().color = Color.white;
        tempVP.AddComponent<Mask>().showMaskGraphic = false;
        tempScroll.viewport = vpRT2;

        // Content in template viewport
        var tempContent = new GameObject("Content");
        tempContent.transform.SetParent(tempVP.transform, false);
        var tcRT = tempContent.AddComponent<RectTransform>();
        tcRT.anchorMin = new Vector2(0, 1);
        tcRT.anchorMax = new Vector2(1, 1);
        tcRT.pivot = new Vector2(0.5f, 1);
        tcRT.anchoredPosition = Vector2.zero;
        tcRT.sizeDelta = new Vector2(0, 28);
        tempScroll.content = tcRT;

        // Item template
        var itemGO = new GameObject("Item");
        itemGO.transform.SetParent(tempContent.transform, false);
        var itemRT = itemGO.AddComponent<RectTransform>();
        itemRT.anchorMin = new Vector2(0, 0.5f);
        itemRT.anchorMax = new Vector2(1, 0.5f);
        itemRT.sizeDelta = new Vector2(0, 28);
        var itemToggle = itemGO.AddComponent<Toggle>();

        var itemBG = new GameObject("Item Background");
        itemBG.transform.SetParent(itemGO.transform, false);
        var ibRT = itemBG.AddComponent<RectTransform>();
        ibRT.anchorMin = Vector2.zero;
        ibRT.anchorMax = Vector2.one;
        ibRT.offsetMin = Vector2.zero;
        ibRT.offsetMax = Vector2.zero;
        var ibImg = itemBG.AddComponent<Image>();
        ibImg.color = new Color(0.15f, 0.15f, 0.25f, 1f);

        var itemCheck = new GameObject("Item Checkmark");
        itemCheck.transform.SetParent(itemBG.transform, false);
        var icRT = itemCheck.AddComponent<RectTransform>();
        icRT.anchorMin = new Vector2(0, 0);
        icRT.anchorMax = new Vector2(0, 1);
        icRT.pivot = new Vector2(0, 0.5f);
        icRT.anchoredPosition = new Vector2(4, 0);
        icRT.sizeDelta = new Vector2(20, 0);
        var icImg = itemCheck.AddComponent<Image>();
        icImg.color = new Color(0.3f, 0.8f, 0.3f);

        var itemLabel = new GameObject("Item Label");
        itemLabel.transform.SetParent(itemGO.transform, false);
        var ilRT = itemLabel.AddComponent<RectTransform>();
        ilRT.anchorMin = Vector2.zero;
        ilRT.anchorMax = Vector2.one;
        ilRT.offsetMin = new Vector2(28, 2);
        ilRT.offsetMax = new Vector2(-4, -2);
        var ilText = itemLabel.AddComponent<Text>();
        ilText.font = _font;
        ilText.fontSize = 14;
        ilText.color = Color.white;
        ilText.alignment = TextAnchor.MiddleLeft;

        itemToggle.targetGraphic = ibImg;
        itemToggle.graphic = icImg;
        itemToggle.isOn = false;

        templateGO.SetActive(false);

        // Configure dropdown
        dropdown.targetGraphic = ddBG;
        dropdown.template = tempRT;
        dropdown.captionText = labelText;
        dropdown.itemText = ilText;

        // Populate options
        var options = new List<Dropdown.OptionData>();
        options.Add(new Dropdown.OptionData("(Empty)"));
        foreach (var spell in row.AvailableSpells)
        {
            string optLabel = spell.Name;
            string domainTag = GetDomainTagForSpell(spell);
            if (!string.IsNullOrEmpty(domainTag))
                optLabel += $" [{domainTag}]";
            if (spell.IsPlaceholder) optLabel += " [PH]";
            options.Add(new Dropdown.OptionData(optLabel));
        }
        dropdown.options = options;

        // Set current selection
        if (slot.HasSpell)
        {
            int spellIdx = row.AvailableSpells.FindIndex(s => s.SpellId == slot.PreparedSpell.SpellId);
            dropdown.value = spellIdx >= 0 ? spellIdx + 1 : 0;
        }
        else
        {
            dropdown.value = 0;
        }

        // Handle changes
        int capturedIndex = index;
        dropdown.onValueChanged.AddListener((value) => OnSlotChanged(capturedIndex, value));

        row.SpellDropdown = dropdown;
        return row;
    }

    // ========== CREATION MODE HELPERS ==========

    private void PopulateCreationSlotList()
    {
        // Clear existing rows
        foreach (var row in _slotRows)
        {
            if (row.Row != null) Destroy(row.Row);
        }
        _slotRows.Clear();

        var scrollContent = _rootPanel.transform.Find("ScrollArea/Viewport/Content");
        if (scrollContent == null) return;

        // Clear any leftover children
        foreach (Transform child in scrollContent)
        {
            Destroy(child.gameObject);
        }

        float yPos = -8f;
        float rowHeight = 40f;
        float spacing = 4f;
        int lastLevel = -1;
        int slotIndex = 0;

        foreach (var slot in _creationSlots)
        {
            // Level header
            if (slot.Level != lastLevel)
            {
                if (lastLevel >= 0) yPos -= 8f;
                lastLevel = slot.Level;

                int slotsAtLevel = _creationSlotsMax[slot.Level];
                string l0Label = _isClericCreationMode ? "ORISONS" : "CANTRIPS";
                string levelLabel = slot.Level == 0
                    ? $"═══ {l0Label} (Level 0 — Unlimited) ═══  ({slotsAtLevel} slots)"
                    : $"═══ LEVEL {slot.Level} SPELLS ═══  ({slotsAtLevel} slots)";

                var headerGO = new GameObject("Header" + slot.Level);
                headerGO.transform.SetParent(scrollContent, false);
                var hRT = headerGO.AddComponent<RectTransform>();
                hRT.anchorMin = new Vector2(0, 1);
                hRT.anchorMax = new Vector2(1, 1);
                hRT.pivot = new Vector2(0.5f, 1);
                hRT.anchoredPosition = new Vector2(0, yPos);
                hRT.sizeDelta = new Vector2(0, 28);
                var hText = headerGO.AddComponent<Text>();
                hText.text = levelLabel;
                hText.font = _font;
                hText.fontSize = 13;
                hText.color = new Color(0.7f, 0.8f, 1f);
                hText.alignment = TextAnchor.MiddleCenter;
                hText.supportRichText = true;
                hText.raycastTarget = false;
                yPos -= 30f;
            }

            // Slot row
            var rowUI = CreateCreationSlotRow(scrollContent, slot, slotIndex, yPos);
            _slotRows.Add(rowUI);
            yPos -= (rowHeight + spacing);
            slotIndex++;
        }

        // Set content height
        var contentRT = scrollContent.GetComponent<RectTransform>();
        contentRT.sizeDelta = new Vector2(0, Mathf.Abs(yPos) + 12f);
    }

    private SlotRowUI CreateCreationSlotRow(Transform parent, SpellSlot slot, int index, float yPos)
    {
        var row = new SlotRowUI();
        row.Slot = slot;
        row.SlotIndex = index;

        // Get available spells at this level from spellbook
        // For domain slots, restrict to domain spells only
        bool isDomainSlotForFilter = _domainSlotIndices.Contains(index);
        if (isDomainSlotForFilter && slot.Level >= 1)
        {
            List<SpellData> domSpells = slot.Level == 1 ? _domainSpellsLevel1 : _domainSpellsLevel2;
            row.AvailableSpells = domSpells.OrderBy(s => s.Name).ToList();
        }
        else
        {
            row.AvailableSpells = _creationKnownSpells
                .Where(s => s.SpellLevel == slot.Level)
                .OrderBy(s => s.Name)
                .ToList();
        }

        // Row container
        row.Row = new GameObject($"SlotRow_{index}");
        row.Row.transform.SetParent(parent, false);
        var rt = row.Row.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, yPos);
        rt.sizeDelta = new Vector2(0, 40f);

        // Background
        var bg = row.Row.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.15f, 0.6f);

        // Slot label
        int levelSlotNum = 0;
        foreach (var s in _creationSlots)
        {
            if (s.Level == slot.Level)
            {
                levelSlotNum++;
                if (s == slot) break;
            }
        }
        string l0SlotLabel = _isClericCreationMode ? "Orison" : "Cantrip";
        bool isDomainSlot = _domainSlotIndices.Contains(index);
        string slotLabel;
        if (slot.Level == 0)
            slotLabel = $"{l0SlotLabel} Slot {levelSlotNum}:";
        else if (isDomainSlot)
            slotLabel = $"Lv{slot.Level} DOMAIN Slot:";
        else
            slotLabel = $"Level {slot.Level} Slot {levelSlotNum}:";

        Color labelColor = isDomainSlot ? new Color(1f, 0.85f, 0.3f) : new Color(0.8f, 0.8f, 0.7f);
        row.LabelText = MakeText(row.Row.transform, "Label",
            new Vector2(-280, 0), new Vector2(180, 30),
            slotLabel, 13, labelColor, TextAnchor.MiddleRight);

        // Domain slot background tint
        if (isDomainSlot)
        {
            bg.color = new Color(0.15f, 0.12f, 0.05f, 0.7f);
        }

        // Dropdown for spell selection
        GameObject dropdownObj = new GameObject("Dropdown");
        dropdownObj.transform.SetParent(row.Row.transform, false);
        var ddRT = dropdownObj.AddComponent<RectTransform>();
        ddRT.anchorMin = new Vector2(0.5f, 0.5f);
        ddRT.anchorMax = new Vector2(0.5f, 0.5f);
        ddRT.pivot = new Vector2(0.5f, 0.5f);
        ddRT.anchoredPosition = new Vector2(60, 0);
        ddRT.sizeDelta = new Vector2(430, 44);

        var ddBG = dropdownObj.AddComponent<Image>();
        ddBG.color = new Color(0.15f, 0.15f, 0.25f, 1f);

        var dropdown = dropdownObj.AddComponent<Dropdown>();

        // Label for dropdown
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(dropdownObj.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = new Vector2(8, 2);
        labelRT.offsetMax = new Vector2(-28, -2);
        var labelText = labelGO.AddComponent<Text>();
        labelText.font = _font;
        labelText.fontSize = 16;
        labelText.color = Color.white;
        labelText.alignment = TextAnchor.MiddleLeft;

        // Arrow
        var arrowGO = new GameObject("Arrow");
        arrowGO.transform.SetParent(dropdownObj.transform, false);
        var arrowRT = arrowGO.AddComponent<RectTransform>();
        arrowRT.anchorMin = new Vector2(1, 0);
        arrowRT.anchorMax = new Vector2(1, 1);
        arrowRT.pivot = new Vector2(1, 0.5f);
        arrowRT.anchoredPosition = new Vector2(-4, 0);
        arrowRT.sizeDelta = new Vector2(20, 0);
        var arrowText = arrowGO.AddComponent<Text>();
        arrowText.font = _font;
        arrowText.fontSize = 14;
        arrowText.color = new Color(0.7f, 0.7f, 0.9f);
        arrowText.text = "▼";
        arrowText.alignment = TextAnchor.MiddleCenter;

        // Template for dropdown items
        var templateGO = new GameObject("Template");
        templateGO.transform.SetParent(dropdownObj.transform, false);
        var tempRT = templateGO.AddComponent<RectTransform>();
        tempRT.anchorMin = new Vector2(0, 0);
        tempRT.anchorMax = new Vector2(1, 0);
        tempRT.pivot = new Vector2(0.5f, 1);
        tempRT.anchoredPosition = Vector2.zero;
        tempRT.sizeDelta = new Vector2(0, 200);
        var tempImg = templateGO.AddComponent<Image>();
        tempImg.color = new Color(0.12f, 0.12f, 0.2f, 0.98f);
        var tempScroll = templateGO.AddComponent<ScrollRect>();

        var tempVP = new GameObject("Viewport");
        tempVP.transform.SetParent(templateGO.transform, false);
        var vpRT2 = tempVP.AddComponent<RectTransform>();
        vpRT2.anchorMin = Vector2.zero;
        vpRT2.anchorMax = Vector2.one;
        vpRT2.offsetMin = Vector2.zero;
        vpRT2.offsetMax = Vector2.zero;
        tempVP.AddComponent<Image>().color = Color.white;
        tempVP.AddComponent<Mask>().showMaskGraphic = false;
        tempScroll.viewport = vpRT2;

        var tempContent = new GameObject("Content");
        tempContent.transform.SetParent(tempVP.transform, false);
        var tcRT = tempContent.AddComponent<RectTransform>();
        tcRT.anchorMin = new Vector2(0, 1);
        tcRT.anchorMax = new Vector2(1, 1);
        tcRT.pivot = new Vector2(0.5f, 1);
        tcRT.anchoredPosition = Vector2.zero;
        tcRT.sizeDelta = new Vector2(0, 28);
        tempScroll.content = tcRT;

        var itemGO = new GameObject("Item");
        itemGO.transform.SetParent(tempContent.transform, false);
        var itemRT = itemGO.AddComponent<RectTransform>();
        itemRT.anchorMin = new Vector2(0, 0.5f);
        itemRT.anchorMax = new Vector2(1, 0.5f);
        itemRT.sizeDelta = new Vector2(0, 28);
        var itemToggle = itemGO.AddComponent<Toggle>();

        var itemBG = new GameObject("Item Background");
        itemBG.transform.SetParent(itemGO.transform, false);
        var ibRT = itemBG.AddComponent<RectTransform>();
        ibRT.anchorMin = Vector2.zero;
        ibRT.anchorMax = Vector2.one;
        ibRT.offsetMin = Vector2.zero;
        ibRT.offsetMax = Vector2.zero;
        var ibImg = itemBG.AddComponent<Image>();
        ibImg.color = new Color(0.15f, 0.15f, 0.25f, 1f);

        var itemCheck = new GameObject("Item Checkmark");
        itemCheck.transform.SetParent(itemBG.transform, false);
        var icRT = itemCheck.AddComponent<RectTransform>();
        icRT.anchorMin = new Vector2(0, 0);
        icRT.anchorMax = new Vector2(0, 1);
        icRT.pivot = new Vector2(0, 0.5f);
        icRT.anchoredPosition = new Vector2(4, 0);
        icRT.sizeDelta = new Vector2(20, 0);
        var icImg = itemCheck.AddComponent<Image>();
        icImg.color = new Color(0.3f, 0.8f, 0.3f);

        var itemLabel = new GameObject("Item Label");
        itemLabel.transform.SetParent(itemGO.transform, false);
        var ilRT = itemLabel.AddComponent<RectTransform>();
        ilRT.anchorMin = Vector2.zero;
        ilRT.anchorMax = Vector2.one;
        ilRT.offsetMin = new Vector2(28, 2);
        ilRT.offsetMax = new Vector2(-4, -2);
        var ilText = itemLabel.AddComponent<Text>();
        ilText.font = _font;
        ilText.fontSize = 14;
        ilText.color = Color.white;
        ilText.alignment = TextAnchor.MiddleLeft;

        itemToggle.targetGraphic = ibImg;
        itemToggle.graphic = icImg;
        itemToggle.isOn = false;

        templateGO.SetActive(false);

        // Configure dropdown
        dropdown.targetGraphic = ddBG;
        dropdown.template = tempRT;
        dropdown.captionText = labelText;
        dropdown.itemText = ilText;

        // Populate options
        var options = new List<Dropdown.OptionData>();
        options.Add(new Dropdown.OptionData("(Empty)"));
        foreach (var spell in row.AvailableSpells)
        {
            string optLabel = spell.Name;
            if (spell.IsPlaceholder) optLabel += " [PH]";
            options.Add(new Dropdown.OptionData(optLabel));
        }
        dropdown.options = options;

        // Set current selection
        if (slot.HasSpell)
        {
            int spellIdx = row.AvailableSpells.FindIndex(s => s.SpellId == slot.PreparedSpell.SpellId);
            dropdown.value = spellIdx >= 0 ? spellIdx + 1 : 0;
        }
        else
        {
            dropdown.value = 0;
        }

        // Handle changes
        int capturedIndex = index;
        dropdown.onValueChanged.AddListener((value) => OnCreationSlotChanged(capturedIndex, value));

        row.SpellDropdown = dropdown;
        return row;
    }

    private void OnCreationSlotChanged(int slotIndex, int dropdownValue)
    {
        if (slotIndex < 0 || slotIndex >= _slotRows.Count) return;
        var row = _slotRows[slotIndex];

        if (dropdownValue == 0)
        {
            row.Slot.Prepare(null);
        }
        else
        {
            int spellIdx = dropdownValue - 1;
            if (spellIdx < row.AvailableSpells.Count)
            {
                row.Slot.Prepare(row.AvailableSpells[spellIdx]);
            }
        }

        RefreshCreationSummary();
    }

    private void AutoPrepareCreationSlots()
    {
        for (int level = 0; level < _creationSlotsMax.Length; level++)
        {
            var slotsAtLevel = _creationSlots.Where(s => s.Level == level).ToList();
            var spellsAtLevel = _creationKnownSpells.Where(s => s.SpellLevel == level).ToList();

            if (spellsAtLevel.Count == 0) continue;

            int spellIdx = 0;
            foreach (var slot in slotsAtLevel)
            {
                slot.Prepare(spellsAtLevel[spellIdx % spellsAtLevel.Count]);
                spellIdx++;
            }
        }
    }

    private void RefreshCreationSummary()
    {
        if (_creationSlotsMax == null) return;

        var parts = new List<string>();
        for (int level = 0; level < _creationSlotsMax.Length; level++)
        {
            var slotsAtLevel = _creationSlots.Where(s => s.Level == level).ToList();
            int filled = slotsAtLevel.Count(s => s.HasSpell);
            int total = slotsAtLevel.Count;

            if (level == 0)
            {
                string l0Name = _isClericCreationMode ? "Orisons" : "Cantrips";
                string color = filled >= total ? "#44FF44" : "#FFDD44";
                parts.Add($"<color={color}>{l0Name}: {filled}/{total} (∞)</color>");
            }
            else
            {
                string color = filled >= total ? "#44FF44" : "#FFDD44";
                parts.Add($"<color={color}>Lv{level}: {filled}/{total}</color>");
            }
        }

        _slotSummaryText.text = string.Join("   |   ", parts);
        _slotSummaryText.supportRichText = true;
    }

    /// <summary>Get the prepared spell IDs from creation mode slots (in slot order).</summary>
    private List<string> GetCreationPreparedSpellIds()
    {
        var result = new List<string>();
        foreach (var slot in _creationSlots)
        {
            result.Add(slot.HasSpell ? slot.PreparedSpell.SpellId : "");
        }
        return result;
    }

    // ========== EVENTS ==========

    private string GetDomainTagForSpell(SpellData spell)
    {
        if (spell == null || _clericDomains == null || _clericDomains.Count == 0)
            return string.Empty;

        var matches = new List<string>();
        for (int i = 0; i < _clericDomains.Count; i++)
        {
            string domain = _clericDomains[i];
            if (SpellDatabase.IsSpellInDomain(spell.SpellId, domain))
                matches.Add(domain);
        }

        return matches.Count > 0 ? string.Join("/", matches) : string.Empty;
    }

    private void OnSlotChanged(int slotIndex, int dropdownValue)
    {
        if (slotIndex < 0 || slotIndex >= _slotRows.Count) return;
        var row = _slotRows[slotIndex];
        int levelSlotIndex = _spellComp.GetSlotsForLevel(row.Slot.Level).IndexOf(row.Slot);

        Debug.Log($"[SpellPrep] Change clicked for level {row.Slot.Level} slot {Mathf.Max(0, levelSlotIndex)}, domain={row.Slot.IsDomainSlot}");

        if (dropdownValue == 0)
        {
            row.Slot.Prepare(null);
            _spellComp.SyncPreparedSpellsFromSlots();
            RefreshSummary();
            return;
        }

        int spellIdx = dropdownValue - 1;
        if (spellIdx >= row.AvailableSpells.Count)
            return;

        SpellData selectedSpell = row.AvailableSpells[spellIdx];
        Debug.Log($"[SpellPrep] Spell selected: {selectedSpell.Name} for level {row.Slot.Level} slot {Mathf.Max(0, levelSlotIndex)}");

        int globalSlotIndex = _spellComp.SpellSlots.IndexOf(row.Slot);
        if (globalSlotIndex < 0)
            return;

        bool prepared = _spellComp.PrepareSpellInSlot(globalSlotIndex, selectedSpell);
        if (!prepared)
        {
            Debug.LogError($"[SpellPrep] Failed to prepare {selectedSpell.Name} in slot.");
            int currentSpellIndex = row.Slot.HasSpell
                ? row.AvailableSpells.FindIndex(s => s.SpellId == row.Slot.PreparedSpell.SpellId) + 1
                : 0;
            if (row.SpellDropdown != null)
                row.SpellDropdown.value = Mathf.Max(0, currentSpellIndex);
            return;
        }

        Debug.Log($"[SpellPrep] Prepared {selectedSpell.Name} in level {row.Slot.Level} slot {Mathf.Max(0, levelSlotIndex)}");
        _spellComp.SyncPreparedSpellsFromSlots();
        RefreshSummary();
    }

    private void OnAutoPrepare()
    {
        if (_isCreationMode)
        {
            AutoPrepareCreationSlots();
        }
        else
        {
            if (_spellComp.Stats != null && _spellComp.Stats.IsCleric)
                _spellComp.AutoPrepareClericSlots();
            else if (_spellComp.Stats != null && string.Equals(_spellComp.Stats.CharacterClass, "Druid", StringComparison.OrdinalIgnoreCase))
                _spellComp.AutoPrepareDruidSlots();
            else
                _spellComp.AutoPrepareWizardSlots();
        }

        // Refresh dropdown selections
        foreach (var row in _slotRows)
        {
            if (row.SpellDropdown == null) continue;

            if (row.Slot.HasSpell)
            {
                int idx = row.AvailableSpells.FindIndex(s => s.SpellId == row.Slot.PreparedSpell.SpellId);
                row.SpellDropdown.value = idx >= 0 ? idx + 1 : 0;
            }
            else
            {
                row.SpellDropdown.value = 0;
            }
        }

        if (_isCreationMode)
            RefreshCreationSummary();
        else
            RefreshSummary();
    }

    private void OnConfirm()
    {
        if (_isCreationMode)
        {
            var preparedIds = GetCreationPreparedSpellIds();
            Debug.Log($"[SpellPreparationUI] Creation preparation confirmed: {preparedIds.Count} slots filled");
            Close();
            OnCreationPreparationConfirmed?.Invoke(preparedIds);
        }
        else
        {
            // Sync everything
            _spellComp.SyncPreparedSpellsFromSlots();
            Debug.Log($"[SpellPreparationUI] {_spellComp.Stats.CharacterName} preparation confirmed: " +
                      _spellComp.GetSlotDetails());
            Close();
            OnPreparationConfirmed?.Invoke();
        }
    }

    // ========== REFRESH ==========

    private void RefreshSummary()
    {
        if (_spellComp == null || _spellComp.SlotsMax == null) return;

        var parts = new List<string>();
        for (int level = 0; level < _spellComp.SlotsMax.Length; level++)
        {
            var slotsAtLevel = _spellComp.GetSlotsForLevel(level);
            int filled = slotsAtLevel.Count(s => s.HasSpell);
            int total = slotsAtLevel.Count;

            if (level == 0)
            {
                string cantripLabel = (_spellComp.Stats != null && _spellComp.Stats.IsCleric) ? "Orisons" : "Cantrips";
                string color = filled >= total ? "#44FF44" : "#FFDD44";
                parts.Add($"<color={color}>{cantripLabel}: {filled}/{total} (\u221e)</color>");
            }
            else
            {
                int domainSlots = _spellComp.GetMaxDomainSlotsAtLevel(level);
                int regularSlots = _spellComp.GetMaxSpellSlotsAtLevel(level);
                string label = domainSlots > 0
                    ? $"Lv{level} ({regularSlots}+{domainSlots}D)"
                    : $"Lv{level}";
                string color = filled >= total ? "#44FF44" : "#FFDD44";
                parts.Add($"<color={color}>{label}: {filled}/{total}</color>");
            }
        }

        _slotSummaryText.text = string.Join("   |   ", parts);
        _slotSummaryText.supportRichText = true;
    }

    // ========== UI HELPERS ==========

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
        t.fontSize = fontSize + 2;
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

        var labelText = MakeText(go.transform, name + "Label", Vector2.zero, size,
            label, fontSize, textColor, TextAnchor.MiddleCenter);
        labelText.raycastTarget = false;

        return btn;
    }

    private GameObject MakePanel(Transform parent, string name,
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
}