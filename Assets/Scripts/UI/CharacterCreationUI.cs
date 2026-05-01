using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Complete character creation UI for D&D 3.5.
/// 5-step flow: Roll Stats → Assign Stats → Choose Race → Choose Class → Review.
/// Creates 4 PCs before starting the game.
/// </summary>
public class CharacterCreationUI : MonoBehaviour
{
    // ========== STATE ==========
    public enum Step { RollStats, AssignStats, ChooseRace, ChooseClass, ChooseAlignment, ChooseDeity, ChooseDomains, ChooseSpontaneousCasting, AllocateSkills, SelectFeats, SelectSpells, Review }

    public Step CurrentStep = Step.RollStats;
    public int CurrentCharacterIndex = 0; // 0 = PC1, 1 = PC2, 2 = PC3, 3 = PC4
    public CharacterCreationData[] CreatedCharacters = new CharacterCreationData[4];
    public bool IsComplete { get; private set; }

    /// <summary>Total number of PCs to create.</summary>
    private const int TotalPCs = 4;

    /// <summary>Default names for each PC slot.</summary>
    private static readonly string[] DefaultNames = { "Aldric", "Lyra", "Kael", "Grunk" };

    // Legacy callback when 2 characters are created (for backward compat)
    public System.Action<CharacterCreationData, CharacterCreationData> OnCreationComplete;

    /// <summary>Callback when all 4 characters are created (array version).</summary>
    public System.Action<CharacterCreationData[]> OnCreationComplete4;

    // Reference to skills UI for skill allocation step
    public SkillsUIPanel SkillsUI;

    // Reference to feat selection UI for feat selection step
    public FeatSelectionUI FeatUI;

    // Reference to spell selection UI for spell selection step
    public SpellSelectionUI SpellUI;
    public SpellPreparationUI SpellPrepUI;

    // ========== UI REFERENCES ==========
    private GameObject _rootPanel;
    private GameObject _overlayPanel; // Full-screen overlay (parent of _rootPanel)
    private CanvasGroup _canvasGroup; // Controls raycast blocking when hidden
    private Text _titleText;
    private Text _stepText;
    private GameObject _contentArea;
    private Button _backButton;
    private Button _quickStartButton;

    // Step 1: Roll Stats
    private Text _rollDetailsText;
    private Text _rollResultsText;
    private Button _rollButton;
    private Button _rerollButton;
    private Button _acceptStatsButton;
    private Button _premadeButton; // "Choose Pre-made Character" button on roll stats screen
    private GameObject _premadeOverlayPanel; // Overlay for pre-made character selection
    private int[] _currentRolls;
    private int[][] _currentRollDetails; // 4 dice per roll

    // Step 2: Assign Stats
    private int[] _assignedValues; // index 0-5 = STR,DEX,CON,INT,WIS,CHA; value = rolled value or -1
    private int _selectedRollIndex = -1; // which rolled value is selected
    private Button[] _statSlotButtons;
    private Button[] _rollValueButtons;
    private Text _assignPreviewText;
    private Button _confirmAssignButton;
    private Text[] _statSlotTexts;
    private Text[] _rollValueTexts;
    private bool[] _rollUsed;

    // Step 3: Choose Race
    private string _selectedRace = null;
    private Button[] _raceButtons;
    private Text _raceInfoText;
    private Text _racePreviewText;
    private Button _confirmRaceButton;

    // Step 4: Choose Class
    private string _selectedClass = null;
    private Button[] _classButtons;
    private Text _classInfoText;
    private Button _confirmClassButton;

    // Step 4b: Choose Alignment
    private GameObject _stepAlignPanel;
    private Alignment _selectedAlignment = Alignment.None;
    private Button[] _alignmentButtons;
    private Text _alignInfoText;
    private Text _alignRestrictionText;
    private Button _confirmAlignButton;

    // Step 4c: Choose Deity
    private GameObject _stepDeityPanel;
    private string _selectedDeityId = "";
    private Button[] _deityButtons;
    private Text _deityInfoText;
    private Text _deityRestrictionText;
    private Button _confirmDeityButton;
    private Button _skipDeityButton;
    private List<DeityData> _currentDeityList = new List<DeityData>();

    // Step 4d: Choose Domains (Cleric only)
    private GameObject _stepDomainPanel;
    private List<string> _selectedDomains = new List<string>();
    private Button[] _domainButtons;
    private Text _domainInfoText;
    private Text _domainSummaryText;
    private Button _confirmDomainButton;
    private List<string> _availableDomainNames = new List<string>();

    // Step 4e: Choose Spontaneous Casting (Neutral clerics only)
    private GameObject _stepSpontCastPanel;
    private SpontaneousCastingType _selectedSpontCasting = SpontaneousCastingType.None;
    private Button _cureButton;
    private Button _inflictButton;
    private Text _spontCastInfoText;
    private Button _confirmSpontCastButton;

    // Step 5: Review
    private Text _reviewText;
    private RectTransform _reviewScrollContentRT; // Content RT for scroll area sizing
    private InputField _nameInput;
    private Button _createButton;

    // ========== QUICK START SELECTION STATE ==========
    private int _qsSelectedPartySize = 0;
    private string[] _qsSelectedClasses; // class name per slot (null = unselected)
    private CharacterCreationData[] _qsCharacters; // built quick start characters per slot
    private Dictionary<string, CharacterCreationData> _qsAvailableCharacters;
    private int _qsActiveSlot = -1; // which slot is being edited

    // Quick Start UI panels
    private GameObject _qsOverlayPanel;
    private GameObject _qsPartySizePanel;
    private GameObject _qsSlotSelectionPanel;
    private GameObject _qsClassDropdownPanel;
    private GameObject _qsPreviewPanel;
    private Text _qsSlotInfoText;
    private Button _qsStartGameButton;
    private Button[] _qsSlotButtons;
    private Text[] _qsSlotLabels;

    // UI Layout constants
    private Font _font;
    private const float PANEL_W = 1080f;
    private const float PANEL_H = 800f;

    // ========== RACE/CLASS DATA ==========
    private static readonly string[] RaceNames = { "Dwarf", "Elf", "Gnome", "Half-Elf", "Half-Orc", "Halfling", "Human" };
    /// <summary>Class names populated from ClassRegistry at runtime.</summary>
    private static string[] ClassNames
    {
        get
        {
            ClassRegistry.Init();
            return ClassRegistry.ClassNames;
        }
    }

    // ========== INITIALIZATION ==========

    public void BuildUI(Canvas canvas)
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 14);

        for (int i = 0; i < TotalPCs; i++)
            CreatedCharacters[i] = new CharacterCreationData();

        // Root panel - dark overlay covering entire screen
        _overlayPanel = CreatePanel(canvas.transform, "CCOverlay",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.85f));
        RectTransform overlayRT = _overlayPanel.GetComponent<RectTransform>();
        overlayRT.offsetMin = Vector2.zero;
        overlayRT.offsetMax = Vector2.zero;

        // Add CanvasGroup so we can control raycast blocking when hidden
        _canvasGroup = _overlayPanel.AddComponent<CanvasGroup>();

        // Main panel
        _rootPanel = CreatePanel(_overlayPanel.transform, "CCPanel",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, new Color(0.12f, 0.12f, 0.18f, 0.98f));

        Debug.Log("[CharacterCreation] Panel: (0,0) to (1,1) - FULLSCREEN");

        // Title
        _titleText = MakeText(_rootPanel.transform, "Title",
            new Vector2(0, PANEL_H / 2 - 30), new Vector2(PANEL_W - 40, 40),
            "CHARACTER CREATION - Hero 1", 28, Color.white, TextAnchor.MiddleCenter);

        // Step indicator
        _stepText = MakeText(_rootPanel.transform, "StepIndicator",
            new Vector2(0, PANEL_H / 2 - 60), new Vector2(PANEL_W - 40, 25),
            "Step 1 of 5: Roll Stats", 16, new Color(0.7f, 0.8f, 1f), TextAnchor.MiddleCenter);

        // Content area
        _contentArea = CreatePanel(_rootPanel.transform, "ContentArea",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, -20), new Vector2(PANEL_W - 40, PANEL_H - 130),
            new Color(0, 0, 0, 0));

        // Back button (bottom left)
        _backButton = MakeButton(_rootPanel.transform, "BackBtn",
            new Vector2(-PANEL_W / 2 + 80, -PANEL_H / 2 + 30), new Vector2(120, 36),
            "← Back", new Color(0.4f, 0.4f, 0.4f), Color.white, 16);
        _backButton.onClick.AddListener(OnBackPressed);

        // Quick Start button (bottom right)
        _quickStartButton = MakeButton(_rootPanel.transform, "QuickStartBtn",
            new Vector2(PANEL_W / 2 - 100, -PANEL_H / 2 + 30), new Vector2(160, 36),
            "⚡ Quick Start", new Color(0.5f, 0.35f, 0.1f), Color.white, 16);
        _quickStartButton.onClick.AddListener(OnQuickStart);

        // Very Quick Start button — instant 4-person party (Fighter, Rogue, Cleric, Wizard)
        Button veryQuickStartBtn = MakeButton(_rootPanel.transform, "VeryQuickStartBtn",
            new Vector2(PANEL_W / 2 - 100, -PANEL_H / 2 + 70), new Vector2(160, 36),
            "⚡⚡ Play Now!", new Color(0.1f, 0.5f, 0.2f), Color.white, 16);
        veryQuickStartBtn.onClick.AddListener(OnVeryQuickStart);

        BuildStepRollStats();
        BuildStepAssignStats();
        BuildStepChooseRace();
        BuildStepChooseClass();
        BuildStepChooseAlignment();
        BuildStepChooseDeity();
        BuildStepChooseDomains();
        BuildStepChooseSpontaneousCasting();
        BuildStepReview();

        ShowStep(Step.RollStats);
    }

    // ========== STEP 1: ROLL STATS ==========

    private GameObject _step1Panel;

    private void BuildStepRollStats()
    {
        _step1Panel = CreatePanel(_contentArea.transform, "Step1",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(PANEL_W - 60, PANEL_H - 150),
            new Color(0, 0, 0, 0));

        // Tooltip explaining the system
        MakeText(_step1Panel.transform, "RollTooltip",
            new Vector2(0, 200), new Vector2(700, 50),
            "Roll 4d6 six times, dropping the lowest die each time.\nThese 6 numbers become your ability scores to assign in the next step.",
            14, new Color(0.7f, 0.7f, 0.7f), TextAnchor.MiddleCenter);

        // Roll details (shows each die)
        _rollDetailsText = MakeText(_step1Panel.transform, "RollDetails",
            new Vector2(0, 80), new Vector2(700, 180),
            "(Click 'Roll Stats' to begin)", 16, new Color(0.9f, 0.9f, 0.7f), TextAnchor.MiddleCenter);

        // Results summary
        _rollResultsText = MakeText(_step1Panel.transform, "RollResults",
            new Vector2(0, -60), new Vector2(700, 40),
            "", 22, Color.white, TextAnchor.MiddleCenter);

        // Buttons
        _rollButton = MakeButton(_step1Panel.transform, "RollBtn",
            new Vector2(-130, -130), new Vector2(200, 45),
            "🎲 Roll Stats", new Color(0.2f, 0.5f, 0.8f), Color.white, 20);
        _rollButton.onClick.AddListener(DoRollStats);

        _rerollButton = MakeButton(_step1Panel.transform, "RerollBtn",
            new Vector2(-130, -130), new Vector2(200, 45),
            "🎲 Re-Roll", new Color(0.6f, 0.4f, 0.1f), Color.white, 20);
        _rerollButton.onClick.AddListener(DoRollStats);
        _rerollButton.gameObject.SetActive(false);

        _acceptStatsButton = MakeButton(_step1Panel.transform, "AcceptBtn",
            new Vector2(130, -130), new Vector2(200, 45),
            "Accept These Stats ✓", new Color(0.2f, 0.6f, 0.2f), Color.white, 18);
        _acceptStatsButton.onClick.AddListener(OnAcceptStats);
        _acceptStatsButton.gameObject.SetActive(false);

        // "Choose Pre-made Character" button — allows picking a quick start character for this slot
        _premadeButton = MakeButton(_step1Panel.transform, "PremadeBtn",
            new Vector2(0, -200), new Vector2(300, 42),
            "🧙 Choose Pre-made Character", new Color(0.4f, 0.25f, 0.55f), Color.white, 16);
        _premadeButton.onClick.AddListener(OnChoosePremadeCharacter);
    }

    private void DoRollStats()
    {
        _currentRolls = new int[6];
        _currentRollDetails = new int[6][];
        string details = "";

        for (int i = 0; i < 6; i++)
        {
            int[] dice = new int[4];
            for (int d = 0; d < 4; d++)
                dice[d] = UnityEngine.Random.Range(1, 7);

            // Sort descending to find lowest
            System.Array.Sort(dice);
            System.Array.Reverse(dice);

            int dropped = dice[3]; // lowest after sort
            int result = dice[0] + dice[1] + dice[2];
            _currentRolls[i] = result;
            _currentRollDetails[i] = dice;

            details += $"Roll {i + 1}: {dice[0]}, {dice[1]}, {dice[2]}, <color=#666666>{dropped}</color>  →  <color=#FFDD44><b>{result}</b></color>\n";
        }

        _rollDetailsText.text = details;

        // Sort for display
        int[] sorted = (int[])_currentRolls.Clone();
        System.Array.Sort(sorted);
        System.Array.Reverse(sorted);
        int total = 0;
        foreach (int v in sorted) total += v;
        _rollResultsText.text = $"Your stats: {string.Join(", ", sorted)}  (Total: {total})";

        _rollButton.gameObject.SetActive(false);
        _rerollButton.gameObject.SetActive(true);
        _acceptStatsButton.gameObject.SetActive(true);
    }

    private void OnAcceptStats()
    {
        if (_currentRolls == null) return;
        CreatedCharacters[CurrentCharacterIndex].RolledStats = (int[])_currentRolls.Clone();
        ShowStep(Step.AssignStats);
    }

    // ========== STEP 2: ASSIGN STATS ==========

    private GameObject _step2Panel;
    private static readonly string[] AbilityNames = { "STR", "DEX", "CON", "INT", "WIS", "CHA" };
    private static readonly string[] AbilityDescriptions = {
        "Melee attack & damage",
        "AC, ranged attack, initiative",
        "Hit points per level",
        "Skill points per level",
        "Will saves, perception",
        "Social skills, spellcasting"
    };

    private void BuildStepAssignStats()
    {
        _step2Panel = CreatePanel(_contentArea.transform, "Step2",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(PANEL_W - 60, PANEL_H - 150),
            new Color(0, 0, 0, 0));

        MakeText(_step2Panel.transform, "AssignTooltip",
            new Vector2(0, 220), new Vector2(700, 40),
            "Click a rolled value, then click an ability slot to assign it. Each value can only be used once.",
            13, new Color(0.7f, 0.7f, 0.7f), TextAnchor.MiddleCenter);

        // Rolled values row
        MakeText(_step2Panel.transform, "RolledLabel",
            new Vector2(0, 185), new Vector2(700, 25),
            "Rolled Values:", 15, new Color(0.8f, 0.8f, 0.6f), TextAnchor.MiddleCenter);

        _rollValueButtons = new Button[6];
        _rollValueTexts = new Text[6];
        _rollUsed = new bool[6];

        float startX = -250f;
        for (int i = 0; i < 6; i++)
        {
            int idx = i;
            float x = startX + i * 90;
            _rollValueButtons[i] = MakeButton(_step2Panel.transform, $"RollVal{i}",
                new Vector2(x, 150), new Vector2(78, 42),
                "?", new Color(0.3f, 0.3f, 0.5f), Color.white, 20);
            _rollValueTexts[i] = _rollValueButtons[i].GetComponentInChildren<Text>();
            _rollValueButtons[i].onClick.AddListener(() => OnRollValueClicked(idx));
        }

        // Ability slots (2 columns of 3)
        _statSlotButtons = new Button[6];
        _statSlotTexts = new Text[6];
        _assignedValues = new int[] { -1, -1, -1, -1, -1, -1 };

        for (int i = 0; i < 6; i++)
        {
            int idx = i;
            int col = i / 3;
            int row = i % 3;
            float x = -180 + col * 360;
            float y = 70 - row * 70;

            // Ability name label
            MakeText(_step2Panel.transform, $"AbilLabel{i}",
                new Vector2(x - 100, y), new Vector2(90, 30),
                AbilityNames[i], 18, new Color(0.9f, 0.8f, 0.4f), TextAnchor.MiddleRight);

            // Slot button
            _statSlotButtons[i] = MakeButton(_step2Panel.transform, $"StatSlot{i}",
                new Vector2(x, y), new Vector2(70, 42),
                "---", new Color(0.2f, 0.2f, 0.3f), Color.white, 20);
            _statSlotTexts[i] = _statSlotButtons[i].GetComponentInChildren<Text>();
            _statSlotButtons[i].onClick.AddListener(() => OnStatSlotClicked(idx));

            // Description
            MakeText(_step2Panel.transform, $"AbilDesc{i}",
                new Vector2(x + 110, y), new Vector2(150, 25),
                AbilityDescriptions[i], 11, new Color(0.6f, 0.6f, 0.6f), TextAnchor.MiddleLeft);
        }

        // Preview text
        _assignPreviewText = MakeText(_step2Panel.transform, "AssignPreview",
            new Vector2(0, -120), new Vector2(700, 40),
            "", 14, new Color(0.7f, 0.9f, 0.7f), TextAnchor.MiddleCenter);

        // Confirm button
        _confirmAssignButton = MakeButton(_step2Panel.transform, "ConfirmAssign",
            new Vector2(0, -170), new Vector2(220, 45),
            "Confirm Assignment ✓", new Color(0.2f, 0.6f, 0.2f), Color.white, 18);
        _confirmAssignButton.onClick.AddListener(OnConfirmAssignment);
    }

    private void RefreshAssignUI()
    {
        var data = CreatedCharacters[CurrentCharacterIndex];

        // Update roll value buttons
        for (int i = 0; i < 6; i++)
        {
            int val = data.RolledStats[i];
            _rollValueTexts[i].text = _rollUsed[i] ? "-" : val.ToString();

            var colors = _rollValueButtons[i].colors;
            if (_rollUsed[i])
                colors.normalColor = new Color(0.2f, 0.2f, 0.2f);
            else if (i == _selectedRollIndex)
                colors.normalColor = new Color(0.5f, 0.5f, 0.1f);
            else
                colors.normalColor = new Color(0.3f, 0.3f, 0.5f);
            colors.highlightedColor = colors.normalColor * 1.2f;
            _rollValueButtons[i].colors = colors;
        }

        // Update stat slot buttons
        for (int i = 0; i < 6; i++)
        {
            if (_assignedValues[i] >= 0)
            {
                int mod = CharacterStats.GetModifier(_assignedValues[i]);
                string modStr = mod >= 0 ? $"+{mod}" : $"{mod}";
                _statSlotTexts[i].text = $"{_assignedValues[i]}";
                var c = _statSlotButtons[i].colors;
                c.normalColor = new Color(0.15f, 0.35f, 0.15f);
                c.highlightedColor = c.normalColor * 1.2f;
                _statSlotButtons[i].colors = c;
            }
            else
            {
                _statSlotTexts[i].text = "---";
                var c = _statSlotButtons[i].colors;
                c.normalColor = new Color(0.2f, 0.2f, 0.3f);
                c.highlightedColor = c.normalColor * 1.2f;
                _statSlotButtons[i].colors = c;
            }
        }

        // Update preview
        bool allAssigned = true;
        string preview = "";
        for (int i = 0; i < 6; i++)
        {
            if (_assignedValues[i] < 0) { allAssigned = false; continue; }
            int mod = CharacterStats.GetModifier(_assignedValues[i]);
            string modStr = mod >= 0 ? $"+{mod}" : $"{mod}";
            preview += $"{AbilityNames[i]}: {_assignedValues[i]} ({modStr})  ";
        }
        _assignPreviewText.text = allAssigned ? preview : $"Assign all 6 values. {preview}";
        _confirmAssignButton.interactable = allAssigned;
    }

    private void OnRollValueClicked(int index)
    {
        if (_rollUsed[index]) return;
        _selectedRollIndex = index;
        RefreshAssignUI();
    }

    private void OnStatSlotClicked(int abilityIndex)
    {
        var data = CreatedCharacters[CurrentCharacterIndex];

        // If clicking an already-assigned slot, un-assign it
        if (_assignedValues[abilityIndex] >= 0)
        {
            // Find the roll index that was used for this slot and free it
            int val = _assignedValues[abilityIndex];
            for (int i = 0; i < 6; i++)
            {
                if (_rollUsed[i] && data.RolledStats[i] == val)
                {
                    _rollUsed[i] = false;
                    break;
                }
            }
            _assignedValues[abilityIndex] = -1;
            RefreshAssignUI();
            return;
        }

        // Assign selected roll value to this ability
        if (_selectedRollIndex < 0 || _rollUsed[_selectedRollIndex]) return;

        _assignedValues[abilityIndex] = data.RolledStats[_selectedRollIndex];
        _rollUsed[_selectedRollIndex] = true;
        _selectedRollIndex = -1;
        RefreshAssignUI();
    }

    private void OnConfirmAssignment()
    {
        var data = CreatedCharacters[CurrentCharacterIndex];
        data.STR = _assignedValues[0];
        data.DEX = _assignedValues[1];
        data.CON = _assignedValues[2];
        data.INT = _assignedValues[3];
        data.WIS = _assignedValues[4];
        data.CHA = _assignedValues[5];
        ShowStep(Step.ChooseRace);
    }

    // ========== STEP 3: CHOOSE RACE ==========

    private GameObject _step3Panel;

    private void BuildStepChooseRace()
    {
        _step3Panel = CreatePanel(_contentArea.transform, "Step3",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(PANEL_W - 60, PANEL_H - 150),
            new Color(0, 0, 0, 0));

        MakeText(_step3Panel.transform, "RaceTooltip",
            new Vector2(0, 225), new Vector2(700, 25),
            "Select a race. Racial modifiers will be applied to your assigned ability scores.",
            13, new Color(0.7f, 0.7f, 0.7f), TextAnchor.MiddleCenter);

        // Race buttons (in a row + second row)
        _raceButtons = new Button[RaceNames.Length];
        for (int i = 0; i < RaceNames.Length; i++)
        {
            int idx = i;
            int row = i / 4;
            int col = i % 4;
            float x = -250 + col * 170;
            float y = 180 - row * 50;
            _raceButtons[i] = MakeButton(_step3Panel.transform, $"Race{i}",
                new Vector2(x, y), new Vector2(155, 40),
                RaceNames[i], new Color(0.25f, 0.25f, 0.4f), Color.white, 16);
            _raceButtons[i].onClick.AddListener(() => OnRaceSelected(idx));
        }

        // Race info text (scrollable area approximated with large text)
        _raceInfoText = MakeText(_step3Panel.transform, "RaceInfo",
            new Vector2(-180, -10), new Vector2(380, 260),
            "Select a race to see details.", 13, new Color(0.85f, 0.85f, 0.8f), TextAnchor.UpperLeft);

        // Preview with stats
        _racePreviewText = MakeText(_step3Panel.transform, "RacePreview",
            new Vector2(210, -10), new Vector2(350, 260),
            "", 13, new Color(0.7f, 0.9f, 0.7f), TextAnchor.UpperLeft);

        // Confirm race button
        _confirmRaceButton = MakeButton(_step3Panel.transform, "ConfirmRace",
            new Vector2(0, -190), new Vector2(200, 42),
            "Confirm Race ✓", new Color(0.2f, 0.6f, 0.2f), Color.white, 18);
        _confirmRaceButton.onClick.AddListener(OnConfirmRace);
        _confirmRaceButton.interactable = false;
    }

    private void OnRaceSelected(int index)
    {
        _selectedRace = RaceNames[index];
        RaceDatabase.Init();
        RaceData race = RaceDatabase.GetRace(_selectedRace);

        // Highlight selected button
        for (int i = 0; i < _raceButtons.Length; i++)
        {
            var c = _raceButtons[i].colors;
            c.normalColor = (i == index) ? new Color(0.4f, 0.4f, 0.15f) : new Color(0.25f, 0.25f, 0.4f);
            c.highlightedColor = c.normalColor * 1.2f;
            _raceButtons[i].colors = c;
        }

        // Show race details
        if (race != null)
        {
            _raceInfoText.text = race.GetFeatureSummary();
            _confirmRaceButton.interactable = true;

            // Show stat preview with racial mods
            var data = CreatedCharacters[CurrentCharacterIndex];
            string preview = "--- Stats with Racial Modifiers ---\n\n";
            preview += FormatStatPreview("STR", data.STR, race.STRModifier);
            preview += FormatStatPreview("DEX", data.DEX, race.DEXModifier);
            preview += FormatStatPreview("CON", data.CON, race.CONModifier);
            preview += FormatStatPreview("INT", data.INT, race.INTModifier);
            preview += FormatStatPreview("WIS", data.WIS, race.WISModifier);
            preview += FormatStatPreview("CHA", data.CHA, race.CHAModifier);

            int finalCon = data.CON + race.CONModifier;
            int conMod = CharacterStats.GetModifier(finalCon);
            preview += $"\nSize: {race.SizeName}";
            preview += $"\nSpeed: {race.BaseSpeedFeet} ft ({race.BaseSpeedSquares} squares)";
            if (race.SizeACAndAttackModifier != 0)
                preview += $"\nSize bonus: {CharacterStats.FormatMod(race.SizeACAndAttackModifier)} AC/Attack";

            _racePreviewText.text = preview;
        }
    }

    private string FormatStatPreview(string name, int baseVal, int raceMod)
    {
        int final_ = baseVal + raceMod;
        int mod = CharacterStats.GetModifier(final_);
        string modStr = mod >= 0 ? $"+{mod}" : $"{mod}";
        string raceNote = raceMod != 0 ? $" <color={(raceMod > 0 ? "#44FF44" : "#FF4444")}>{(raceMod > 0 ? "+" : "")}{raceMod}</color>" : "";
        return $"{name}: {baseVal}{raceNote} = {final_} ({modStr})\n";
    }

    private void OnConfirmRace()
    {
        if (_selectedRace == null) return;
        var data = CreatedCharacters[CurrentCharacterIndex];
        data.RaceName = _selectedRace;
        data.Race = RaceDatabase.GetRace(_selectedRace);
        ShowStep(Step.ChooseClass);
    }

    // ========== STEP 4: CHOOSE CLASS ==========

    private GameObject _step4Panel;

    private void BuildStepChooseClass()
    {
        _step4Panel = CreatePanel(_contentArea.transform, "Step4",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(PANEL_W - 60, PANEL_H - 150),
            new Color(0, 0, 0, 0));

        MakeText(_step4Panel.transform, "ClassTooltip",
            new Vector2(0, 255), new Vector2(700, 25),
            "Select a class. Your class determines hit points, combat abilities, and starting equipment.",
            13, new Color(0.7f, 0.7f, 0.7f), TextAnchor.MiddleCenter);

        // --- Dynamically generate class panels from ClassRegistry ---
        ClassRegistry.Init();
        var allClasses = ClassRegistry.GetAllClasses();
        int classCount = allClasses.Count;

        float topLeftX = -210f;
        float topRightX = 210f;
        float row1Y = 155f;
        float row2Y = -5f;
        float row3Y = -165f;
        float panelW = 360f;
        float panelH = 140f;

        // Layout: 2 columns, rows calculated from class count
        float[] rowYValues = { row1Y, row2Y, row3Y, -325f, -485f }; // Support up to 10 classes (5 rows)

        for (int i = 0; i < classCount; i++)
        {
            ICharacterClass classDef = allClasses[i];
            int row = i / 2;
            bool isLeft = (i % 2 == 0);
            float posX = isLeft ? topLeftX : topRightX;
            float posY = row < rowYValues.Length ? rowYValues[row] : rowYValues[rowYValues.Length - 1] - (row - rowYValues.Length + 1) * 160f;

            // Background panel
            CreatePanel(_step4Panel.transform, $"{classDef.ClassName}BG",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(posX, posY), new Vector2(panelW, panelH),
                new Color(0.15f, 0.15f, 0.25f, 0.8f));

            // Title text
            MakeText(_step4Panel.transform, $"{classDef.ClassName}Title",
                new Vector2(posX, posY + panelH/2 - 12), new Vector2(340, 22),
                classDef.ClassName.ToUpper(), 17, classDef.TitleColor, TextAnchor.MiddleCenter);

            // Info text
            MakeText(_step4Panel.transform, $"{classDef.ClassName}Info",
                new Vector2(posX, posY - 15), new Vector2(330, 100),
                classDef.InfoText,
                10, new Color(0.8f, 0.8f, 0.75f), TextAnchor.UpperLeft);
        }

        // Class selection buttons - dynamically from ClassRegistry
        _classButtons = new Button[classCount];
        _classButtonDefaultColors_dynamic = new Color[classCount];
        for (int i = 0; i < classCount; i++)
        {
            ICharacterClass classDef = allClasses[i];
            int row = i / 2;
            bool isLeft = (i % 2 == 0);
            float posX = isLeft ? topLeftX : topRightX;
            float posY = row < rowYValues.Length ? rowYValues[row] : rowYValues[rowYValues.Length - 1] - (row - rowYValues.Length + 1) * 160f;

            int idx = i;
            _classButtonDefaultColors_dynamic[i] = classDef.ButtonColor;
            _classButtons[i] = MakeButton(_step4Panel.transform, $"Select{classDef.ClassName}",
                new Vector2(posX, posY - panelH/2 - 18), new Vector2(180, 30),
                $"Select {classDef.ClassName}", classDef.ButtonColor, Color.white, 14);
            _classButtons[i].onClick.AddListener(() => OnClassSelected(idx));
        }

        // Info text for selected class
        _classInfoText = MakeText(_step4Panel.transform, "ClassInfo",
            new Vector2(0, -260), new Vector2(700, 25),
            "", 14, new Color(0.9f, 0.9f, 0.5f), TextAnchor.MiddleCenter);

        // Confirm class button
        _confirmClassButton = MakeButton(_step4Panel.transform, "ConfirmClass",
            new Vector2(0, -288), new Vector2(200, 42),
            "Confirm Class ✓", new Color(0.2f, 0.6f, 0.2f), Color.white, 18);
        _confirmClassButton.onClick.AddListener(OnConfirmClass);
        _confirmClassButton.interactable = false;
    }

    // Dynamic button colors populated from ClassRegistry during BuildStepChooseClass
    private Color[] _classButtonDefaultColors_dynamic;

    private void OnClassSelected(int index)
    {
        _selectedClass = ClassNames[index];

        for (int i = 0; i < _classButtons.Length; i++)
        {
            var c = _classButtons[i].colors;
            c.normalColor = (i == index) ? new Color(0.5f, 0.5f, 0.15f) : _classButtonDefaultColors_dynamic[i];
            c.highlightedColor = c.normalColor * 1.2f;
            _classButtons[i].colors = c;
        }

        _classInfoText.text = $"Selected: {_selectedClass}";
        _confirmClassButton.interactable = true;
    }

    private void OnConfirmClass()
    {
        if (_selectedClass == null) return;
        CreatedCharacters[CurrentCharacterIndex].ClassName = _selectedClass;
        ShowStep(Step.ChooseAlignment);
    }

    // ========== STEP 4b: CHOOSE ALIGNMENT ==========

    private void BuildStepChooseAlignment()
    {
        _stepAlignPanel = CreatePanel(_contentArea.transform, "StepAlign",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(PANEL_W - 60, PANEL_H - 150),
            new Color(0, 0, 0, 0));

        MakeText(_stepAlignPanel.transform, "AlignTooltip",
            new Vector2(0, 230), new Vector2(700, 30),
            "Choose your character's alignment. This defines their moral and ethical outlook.",
            14, new Color(0.7f, 0.8f, 1f), TextAnchor.MiddleCenter);

        // Restriction text (shown when class has alignment restrictions)
        _alignRestrictionText = MakeText(_stepAlignPanel.transform, "AlignRestriction",
            new Vector2(0, 205), new Vector2(700, 24),
            "", 13, new Color(1f, 0.7f, 0.3f), TextAnchor.MiddleCenter);

        // 3x3 grid of alignment buttons
        // Column headers
        string[] colHeaders = { "Lawful", "Neutral", "Chaotic" };
        for (int c = 0; c < 3; c++)
        {
            MakeText(_stepAlignPanel.transform, $"ColHeader{c}",
                new Vector2(-180 + c * 180, 175), new Vector2(160, 22),
                colHeaders[c], 13, new Color(0.6f, 0.7f, 0.9f), TextAnchor.MiddleCenter);
        }

        // Row headers
        string[] rowHeaders = { "Good", "Neutral", "Evil" };
        for (int r = 0; r < 3; r++)
        {
            MakeText(_stepAlignPanel.transform, $"RowHeader{r}",
                new Vector2(-320, 130 - r * 80), new Vector2(80, 22),
                rowHeaders[r], 13, new Color(0.6f, 0.7f, 0.9f), TextAnchor.MiddleCenter);
        }

        _alignmentButtons = new Button[9];
        for (int i = 0; i < 9; i++)
        {
            int row = i / 3;
            int col = i % 3;
            Alignment alignment = AlignmentHelper.GridOrder[i];
            string label = AlignmentHelper.GetAbbreviation(alignment) + "\n" + AlignmentHelper.GetFullName(alignment);

            float x = -180 + col * 180;
            float y = 130 - row * 80;

            Color btnColor;
            if (AlignmentHelper.IsGood(alignment))
                btnColor = new Color(0.15f, 0.35f, 0.2f);
            else if (AlignmentHelper.IsEvil(alignment))
                btnColor = new Color(0.4f, 0.15f, 0.15f);
            else
                btnColor = new Color(0.25f, 0.25f, 0.35f);

            _alignmentButtons[i] = MakeButton(_stepAlignPanel.transform, $"Align{i}",
                new Vector2(x, y), new Vector2(160, 65),
                label, btnColor, Color.white, 13);

            int idx = i; // Capture for closure
            _alignmentButtons[i].onClick.AddListener(() => OnAlignmentSelected(idx));
        }

        // Info text for selected alignment description
        _alignInfoText = MakeText(_stepAlignPanel.transform, "AlignInfo",
            new Vector2(0, -120), new Vector2(700, 60),
            "Select an alignment to see its description.", 14, new Color(0.85f, 0.85f, 0.8f), TextAnchor.MiddleCenter);

        // Confirm button
        _confirmAlignButton = MakeButton(_stepAlignPanel.transform, "ConfirmAlign",
            new Vector2(0, -190), new Vector2(220, 45),
            "Confirm Alignment ✓", new Color(0.2f, 0.5f, 0.2f), Color.white, 18);
        _confirmAlignButton.onClick.AddListener(OnConfirmAlignment);
        _confirmAlignButton.interactable = false;
    }

    private void RefreshAlignmentButtons()
    {
        var data = CreatedCharacters[CurrentCharacterIndex];
        string className = data.ClassName;

        for (int i = 0; i < 9; i++)
        {
            Alignment alignment = AlignmentHelper.GridOrder[i];
            bool isValid = AlignmentHelper.IsAlignmentValidForClass(alignment, className);
            bool isSelected = (alignment == _selectedAlignment);

            Color btnColor;
            if (!isValid)
            {
                btnColor = new Color(0.2f, 0.2f, 0.2f, 0.5f); // Dimmed / disabled look
            }
            else if (isSelected)
            {
                btnColor = new Color(0.5f, 0.5f, 0.15f); // Gold highlight for selected
            }
            else if (AlignmentHelper.IsGood(alignment))
            {
                btnColor = new Color(0.15f, 0.35f, 0.2f);
            }
            else if (AlignmentHelper.IsEvil(alignment))
            {
                btnColor = new Color(0.4f, 0.15f, 0.15f);
            }
            else
            {
                btnColor = new Color(0.25f, 0.25f, 0.35f);
            }

            var colors = _alignmentButtons[i].colors;
            colors.normalColor = btnColor;
            colors.highlightedColor = btnColor * 1.2f;
            colors.disabledColor = btnColor;
            _alignmentButtons[i].colors = colors;
            _alignmentButtons[i].interactable = isValid;
        }
    }

    private void OnAlignmentSelected(int index)
    {
        Alignment alignment = AlignmentHelper.GridOrder[index];
        var data = CreatedCharacters[CurrentCharacterIndex];

        if (!AlignmentHelper.IsAlignmentValidForClass(alignment, data.ClassName))
            return;

        _selectedAlignment = alignment;
        _alignInfoText.text = $"<b>{AlignmentHelper.GetFullName(alignment)}</b> ({AlignmentHelper.GetAbbreviation(alignment)})\n{AlignmentHelper.GetDescription(alignment)}";
        _confirmAlignButton.interactable = true;
        RefreshAlignmentButtons();
    }

    private void OnConfirmAlignment()
    {
        if (_selectedAlignment == Alignment.None) return;
        CreatedCharacters[CurrentCharacterIndex].ChosenAlignment = _selectedAlignment;
        ShowStep(Step.ChooseDeity);
    }

    // ========== STEP 4c: CHOOSE DEITY ==========

    private void BuildStepChooseDeity()
    {
        DeityDatabase.Init();

        _stepDeityPanel = CreatePanel(_contentArea.transform, "StepDeity",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(PANEL_W - 60, PANEL_H - 150),
            new Color(0, 0, 0, 0));
        _stepDeityPanel.SetActive(false);

        MakeText(_stepDeityPanel.transform, "DeityTooltip",
            new Vector2(0, 230), new Vector2(700, 30),
            "Choose a deity for your character. Clerics must worship a deity within one step of their alignment.",
            12, new Color(0.7f, 0.7f, 0.9f), TextAnchor.MiddleCenter);

        _deityRestrictionText = MakeText(_stepDeityPanel.transform, "DeityRestriction",
            new Vector2(0, 210), new Vector2(700, 20),
            "", 11, new Color(1f, 0.7f, 0.3f), TextAnchor.MiddleCenter);

        // Scrollable deity list area
        GameObject scrollArea = CreatePanel(_stepDeityPanel.transform, "DeityScrollArea",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-140, -20), new Vector2(480, 370),
            new Color(0.08f, 0.08f, 0.14f, 0.9f));

        // Add mask + ScrollRect
        var scrollMask = scrollArea.AddComponent<Mask>();
        scrollMask.showMaskGraphic = true;
        var scrollRect = scrollArea.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;

        // Content container
        GameObject content = new GameObject("Content");
        content.transform.SetParent(scrollArea.transform, false);
        RectTransform contentRT = content.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = new Vector2(0, 800);
        scrollRect.content = contentRT;

        // Create deity buttons (will be populated dynamically)
        _deityButtons = new Button[16];
        List<DeityData> allDeities = DeityDatabase.GetAllDeities();
        float yPos = -5f;
        float btnH = 44f;
        float spacing = 4f;

        for (int i = 0; i < allDeities.Count && i < 16; i++)
        {
            int idx = i;
            DeityData deity = allDeities[i];
            _deityButtons[i] = MakeButton(content.transform, $"Deity{i}",
                new Vector2(0, yPos - btnH / 2), new Vector2(460, btnH),
                $"{deity.Name} ({deity.AlignmentAbbr})", new Color(0.2f, 0.2f, 0.35f), Color.white, 13);

            // Position using anchors
            RectTransform btnRT = _deityButtons[i].GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0, 1);
            btnRT.anchorMax = new Vector2(1, 1);
            btnRT.pivot = new Vector2(0.5f, 1);
            btnRT.anchoredPosition = new Vector2(0, yPos);
            btnRT.sizeDelta = new Vector2(-10, btnH);

            _deityButtons[i].onClick.AddListener(() => OnDeitySelected(idx));
            yPos -= (btnH + spacing);
        }
        contentRT.sizeDelta = new Vector2(0, Mathf.Abs(yPos) + 10);

        // Info panel on right side
        _deityInfoText = MakeText(_stepDeityPanel.transform, "DeityInfo",
            new Vector2(250, -20), new Vector2(260, 370),
            "Select a deity to see details.", 12, new Color(0.8f, 0.8f, 0.9f), TextAnchor.UpperLeft);

        // Confirm and Skip buttons
        _confirmDeityButton = MakeButton(_stepDeityPanel.transform, "ConfirmDeity",
            new Vector2(-80, -220), new Vector2(200, 40),
            "Confirm Deity ✓", new Color(0.15f, 0.4f, 0.15f), Color.white, 16);
        _confirmDeityButton.onClick.AddListener(OnConfirmDeity);
        _confirmDeityButton.interactable = false;

        _skipDeityButton = MakeButton(_stepDeityPanel.transform, "SkipDeity",
            new Vector2(130, -220), new Vector2(160, 40),
            "No Deity", new Color(0.4f, 0.35f, 0.15f), Color.white, 14);
        _skipDeityButton.onClick.AddListener(OnSkipDeity);
    }

    private void OnDeitySelected(int index)
    {
        List<DeityData> allDeities = DeityDatabase.GetAllDeities();
        if (index < 0 || index >= allDeities.Count) return;

        DeityData deity = allDeities[index];
        _selectedDeityId = deity.DeityId;

        // Update info display
        string info = $"<b>{deity.Name}</b>\n";
        info += $"{deity.Title}\n";
        info += $"Alignment: {AlignmentHelper.GetFullName(deity.DeityAlignment)} ({deity.AlignmentAbbr})\n\n";
        info += $"<b>Domains:</b>\n{deity.DomainsString}\n\n";
        info += $"<b>Favored Weapon:</b>\n{deity.FavoredWeapon}\n\n";
        info += $"<b>Portfolio:</b>\n{deity.Portfolio}";
        _deityInfoText.text = info;

        _confirmDeityButton.interactable = true;
        RefreshDeityButtons();
    }

    private void RefreshDeityButtons()
    {
        var data = CreatedCharacters[CurrentCharacterIndex];
        List<DeityData> allDeities = DeityDatabase.GetAllDeities();
        bool isCleric = data.ClassName == "Cleric";

        for (int i = 0; i < allDeities.Count && i < _deityButtons.Length; i++)
        {
            if (_deityButtons[i] == null) continue;
            DeityData deity = allDeities[i];

            bool compatible = deity.IsAlignmentCompatible(data.ChosenAlignment);

            var colors = _deityButtons[i].colors;
            if (deity.DeityId == _selectedDeityId)
            {
                colors.normalColor = new Color(0.6f, 0.55f, 0.1f); // Gold for selected
            }
            else if (!compatible && isCleric)
            {
                colors.normalColor = new Color(0.15f, 0.15f, 0.2f, 0.5f); // Dimmed
            }
            else
            {
                colors.normalColor = new Color(0.2f, 0.2f, 0.35f);
            }
            colors.highlightedColor = colors.normalColor * 1.3f;
            _deityButtons[i].colors = colors;

            // Disable incompatible deities for clerics
            _deityButtons[i].interactable = !isCleric || compatible;
        }

        // Clerics MUST choose a deity; hide skip for clerics
        if (_skipDeityButton != null)
            _skipDeityButton.gameObject.SetActive(!isCleric);
    }

    private void OnConfirmDeity()
    {
        if (string.IsNullOrEmpty(_selectedDeityId)) return;
        CreatedCharacters[CurrentCharacterIndex].ChosenDeityId = _selectedDeityId;

        // If cleric, go to domain selection; otherwise skip to skills
        if (CreatedCharacters[CurrentCharacterIndex].ClassName == "Cleric")
        {
            ShowStep(Step.ChooseDomains);
        }
        else
        {
            ShowStep(Step.AllocateSkills);
        }
    }

    private void OnSkipDeity()
    {
        CreatedCharacters[CurrentCharacterIndex].ChosenDeityId = "";
        ShowStep(Step.AllocateSkills);
    }

    // ========== STEP 4d: CHOOSE DOMAINS (Cleric only) ==========

    private void BuildStepChooseDomains()
    {
        DomainDatabase.Init();

        _stepDomainPanel = CreatePanel(_contentArea.transform, "StepDomain",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(PANEL_W - 60, PANEL_H - 150),
            new Color(0, 0, 0, 0));
        _stepDomainPanel.SetActive(false);

        MakeText(_stepDomainPanel.transform, "DomainTooltip",
            new Vector2(0, 230), new Vector2(700, 30),
            "Choose 2 domains from your deity's list. Each domain grants a special power and bonus spells.",
            12, new Color(0.7f, 0.7f, 0.9f), TextAnchor.MiddleCenter);

        _domainSummaryText = MakeText(_stepDomainPanel.transform, "DomainSummary",
            new Vector2(0, 205), new Vector2(700, 25),
            "Selected: 0/2", 13, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter);

        // Domain buttons area (will be dynamically populated)
        _domainButtons = new Button[8]; // max 6 domains per deity, 8 for safety

        // Info panel
        _domainInfoText = MakeText(_stepDomainPanel.transform, "DomainInfo",
            new Vector2(180, -20), new Vector2(340, 340),
            "Select a domain to see details.", 12, new Color(0.8f, 0.8f, 0.9f), TextAnchor.UpperLeft);

        // Confirm button
        _confirmDomainButton = MakeButton(_stepDomainPanel.transform, "ConfirmDomain",
            new Vector2(0, -220), new Vector2(250, 40),
            "Confirm Domains ✓", new Color(0.15f, 0.4f, 0.15f), Color.white, 16);
        _confirmDomainButton.onClick.AddListener(OnConfirmDomains);
        _confirmDomainButton.interactable = false;
    }

    private void PopulateDomainButtons()
    {
        // Clear existing buttons
        for (int i = 0; i < _domainButtons.Length; i++)
        {
            if (_domainButtons[i] != null)
            {
                Destroy(_domainButtons[i].gameObject);
                _domainButtons[i] = null;
            }
        }

        var data = CreatedCharacters[CurrentCharacterIndex];
        DeityData deity = DeityDatabase.GetDeity(data.ChosenDeityId);
        if (deity == null) return;

        _availableDomainNames = new List<string>(deity.Domains);
        _selectedDomains.Clear();

        float yStart = 170f;
        float btnH = 50f;
        float spacing = 8f;
        float xPos = -180f;

        for (int i = 0; i < _availableDomainNames.Count && i < _domainButtons.Length; i++)
        {
            int idx = i;
            string domainName = _availableDomainNames[i];
            DomainData domain = DomainDatabase.GetDomain(domainName);

            string label = domainName;
            if (domain != null)
            {
                // Show domain spell names for levels 1-2
                SpellDatabase.Init();
                string spell1Name = "";
                string spell2Name = "";
                string s1Id = domain.GetDomainSpellId(1);
                string s2Id = domain.GetDomainSpellId(2);
                if (s1Id != null) { var s = SpellDatabase.GetSpell(s1Id); spell1Name = s != null ? s.Name : s1Id; }
                if (s2Id != null) { var s = SpellDatabase.GetSpell(s2Id); spell2Name = s != null ? s.Name : s2Id; }
                label = $"{domainName}\n<size=10>1st: {spell1Name}  |  2nd: {spell2Name}</size>";
            }

            float yPos = yStart - i * (btnH + spacing);
            _domainButtons[i] = MakeButton(_stepDomainPanel.transform, $"Domain{i}",
                new Vector2(xPos, yPos), new Vector2(280, btnH),
                label, new Color(0.2f, 0.2f, 0.35f), Color.white, 13);

            _domainButtons[i].onClick.AddListener(() => OnDomainToggled(idx));
        }

        RefreshDomainButtons();
    }

    private void OnDomainToggled(int index)
    {
        if (index < 0 || index >= _availableDomainNames.Count) return;
        string domainName = _availableDomainNames[index];

        if (_selectedDomains.Contains(domainName))
        {
            _selectedDomains.Remove(domainName);
        }
        else if (_selectedDomains.Count < 2)
        {
            _selectedDomains.Add(domainName);
        }

        // Show domain info
        DomainData domain = DomainDatabase.GetDomain(domainName);
        if (domain != null)
        {
            string info = $"<b>{domain.Name} Domain</b>\n\n";
            info += $"<b>Granted Power:</b>\n{domain.GrantedPower}\n\n";
            info += $"<b>Domain Spells:</b>\n";

            SpellDatabase.Init();
            foreach (var kvp in domain.DomainSpells)
            {
                SpellData spell = SpellDatabase.GetSpell(kvp.Value);
                string spellName = spell != null ? spell.Name : kvp.Value;
                info += $"  {kvp.Key}st Level: {spellName}\n";
            }
            _domainInfoText.text = info;
        }

        RefreshDomainButtons();
    }

    private void RefreshDomainButtons()
    {
        _domainSummaryText.text = $"Selected: {_selectedDomains.Count}/2";
        _confirmDomainButton.interactable = _selectedDomains.Count == 2;

        for (int i = 0; i < _availableDomainNames.Count && i < _domainButtons.Length; i++)
        {
            if (_domainButtons[i] == null) continue;
            string domainName = _availableDomainNames[i];
            bool isSelected = _selectedDomains.Contains(domainName);

            var colors = _domainButtons[i].colors;
            if (isSelected)
            {
                colors.normalColor = new Color(0.15f, 0.5f, 0.15f); // Green for selected
            }
            else if (_selectedDomains.Count >= 2)
            {
                colors.normalColor = new Color(0.15f, 0.15f, 0.2f, 0.5f); // Dimmed when 2 selected
            }
            else
            {
                colors.normalColor = new Color(0.2f, 0.2f, 0.35f);
            }
            colors.highlightedColor = colors.normalColor * 1.3f;
            _domainButtons[i].colors = colors;
        }
    }

    private void OnConfirmDomains()
    {
        if (_selectedDomains.Count != 2) return;
        CreatedCharacters[CurrentCharacterIndex].ChosenDomains = new List<string>(_selectedDomains);

        // D&D 3.5e: Determine spontaneous casting based on alignment
        var data = CreatedCharacters[CurrentCharacterIndex];
        var defaultType = SpontaneousCastingHelper.GetDefaultForAlignment(data.ChosenAlignment);

        if (defaultType != SpontaneousCastingType.None)
        {
            // Good or Evil cleric — auto-assign and skip to skills
            data.SpontaneousCasting = defaultType;
            Debug.Log($"[CharCreation] {data.CharacterName}: Auto-assigned spontaneous casting = {defaultType} (alignment: {AlignmentHelper.GetFullName(data.ChosenAlignment)})");
            ShowStep(Step.AllocateSkills);
        }
        else
        {
            // Neutral on Good/Evil axis — must choose
            Debug.Log($"[CharCreation] {data.CharacterName}: Neutral cleric — showing spontaneous casting choice");
            ShowStep(Step.ChooseSpontaneousCasting);
        }
    }

    // ========== STEP 4e: CHOOSE SPONTANEOUS CASTING (Neutral Clerics only) ==========

    private void BuildStepChooseSpontaneousCasting()
    {
        _stepSpontCastPanel = CreatePanel(_contentArea.transform, "StepSpontCast",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(PANEL_W - 60, PANEL_H - 150), new Color(0, 0, 0, 0));
        _stepSpontCastPanel.SetActive(false);

        MakeText(_stepSpontCastPanel.transform, "SpontCastTitle",
            new Vector2(0, 170), new Vector2(500, 40),
            "SPONTANEOUS CASTING", 22, new Color(0.9f, 0.85f, 0.3f), TextAnchor.MiddleCenter);

        MakeText(_stepSpontCastPanel.transform, "SpontCastDesc",
            new Vector2(0, 130), new Vector2(500, 60),
            "As a neutral cleric, you must choose whether to channel\npositive energy (Cure spells) or negative energy (Inflict spells).\nThis choice is permanent and cannot be changed later.",
            14, new Color(0.8f, 0.8f, 0.9f), TextAnchor.MiddleCenter);

        MakeText(_stepSpontCastPanel.transform, "SpontCastRule",
            new Vector2(0, 85), new Vector2(500, 40),
            "You can spontaneously convert any prepared spell into a cure/inflict spell of the same level.",
            12, new Color(0.6f, 0.7f, 0.9f), TextAnchor.MiddleCenter);

        // Cure button
        _cureButton = MakeButton(_stepSpontCastPanel.transform, "CureBtn",
            new Vector2(-120, 20), new Vector2(200, 60),
            "☀ Cure Spells\n(Positive Energy)", new Color(0.2f, 0.5f, 0.2f), Color.white, 14);
        _cureButton.onClick.AddListener(() => OnSelectSpontaneousCasting(SpontaneousCastingType.Cure));

        // Inflict button
        _inflictButton = MakeButton(_stepSpontCastPanel.transform, "InflictBtn",
            new Vector2(120, 20), new Vector2(200, 60),
            "💀 Inflict Spells\n(Negative Energy)", new Color(0.5f, 0.2f, 0.2f), Color.white, 14);
        _inflictButton.onClick.AddListener(() => OnSelectSpontaneousCasting(SpontaneousCastingType.Inflict));

        // Info text — updated when selection changes
        _spontCastInfoText = MakeText(_stepSpontCastPanel.transform, "SpontCastInfo",
            new Vector2(0, -70), new Vector2(500, 80),
            "Select Cure or Inflict spells above.", 13, new Color(0.7f, 0.7f, 0.8f), TextAnchor.UpperCenter);

        // Confirm button
        _confirmSpontCastButton = MakeButton(_stepSpontCastPanel.transform, "ConfirmSpontCast",
            new Vector2(0, -150), new Vector2(200, 40),
            "Confirm ✓", new Color(0.2f, 0.45f, 0.25f), Color.white, 16);
        _confirmSpontCastButton.interactable = false;
        _confirmSpontCastButton.onClick.AddListener(OnConfirmSpontaneousCasting);
    }

    private void OnSelectSpontaneousCasting(SpontaneousCastingType type)
    {
        _selectedSpontCasting = type;
        _confirmSpontCastButton.interactable = true;

        // Update button colors to show selection
        var cureColors = _cureButton.colors;
        var inflictColors = _inflictButton.colors;

        if (type == SpontaneousCastingType.Cure)
        {
            cureColors.normalColor = new Color(0.15f, 0.6f, 0.15f);
            inflictColors.normalColor = new Color(0.3f, 0.2f, 0.2f);
            _spontCastInfoText.text = "☀ CURE SPELLS (Positive Energy)\n\n" +
                "You can convert any prepared spell into a Cure spell of the same level:\n" +
                "  • Level 0: Cure Minor Wounds (heal 1 HP)\n" +
                "  • Level 1: Cure Light Wounds (1d8+CL HP)\n" +
                "  • Level 2: Cure Moderate Wounds (2d8+CL HP)";
        }
        else
        {
            cureColors.normalColor = new Color(0.2f, 0.3f, 0.2f);
            inflictColors.normalColor = new Color(0.6f, 0.15f, 0.15f);
            _spontCastInfoText.text = "💀 INFLICT SPELLS (Negative Energy)\n\n" +
                "You can convert any prepared spell into an Inflict spell of the same level:\n" +
                "  • Level 0: Inflict Minor Wounds (1 negative damage)\n" +
                "  • Level 1: Inflict Light Wounds (1d8+CL negative)\n" +
                "  • Level 2: Inflict Moderate Wounds (2d8+CL negative)";
        }

        cureColors.highlightedColor = cureColors.normalColor * 1.2f;
        inflictColors.highlightedColor = inflictColors.normalColor * 1.2f;
        _cureButton.colors = cureColors;
        _inflictButton.colors = inflictColors;
    }

    private void OnConfirmSpontaneousCasting()
    {
        if (_selectedSpontCasting == SpontaneousCastingType.None) return;
        CreatedCharacters[CurrentCharacterIndex].SpontaneousCasting = _selectedSpontCasting;
        Debug.Log($"[CharCreation] Neutral cleric chose spontaneous casting: {_selectedSpontCasting}");
        ShowStep(Step.AllocateSkills);
    }

    // ========== STEP 5: ALLOCATE SKILLS ==========

    /// <summary>Temporary CharacterStats used during skill allocation in character creation.</summary>
    private CharacterStats _tempStatsForSkills;

    private void StartSkillAllocation()
    {
        var data = CreatedCharacters[CurrentCharacterIndex];
        data.ComputeFinalStats();

        // Create a temporary CharacterStats to use for skill allocation
        _tempStatsForSkills = new CharacterStats(
            name: data.CharacterName.Length > 0 ? data.CharacterName : (CurrentCharacterIndex == 0 ? "Hero 1" : "Hero 2"),
            level: 3,
            characterClass: data.ClassName,
            str: data.STR, dex: data.DEX, con: data.CON,
            wis: data.WIS, intelligence: data.INT, cha: data.CHA,
            bab: data.BAB,
            armorBonus: 0, shieldBonus: 0,
            damageDice: 8, damageCount: 1, bonusDamage: 0,
            baseSpeed: data.BaseSpeed, atkRange: 1,
            baseHitDieHP: data.HP,
            raceName: data.RaceName
        );

        _tempStatsForSkills.InitializeSkills(data.ClassName, 3);

        if (SkillsUI != null)
        {
            SkillsUI.OnAllocationConfirmed = OnSkillAllocationConfirmed;
            SkillsUI.OpenForAllocation(_tempStatsForSkills);
        }
        else
        {
            Debug.LogWarning("[CharCreation] SkillsUI not available, skipping skill allocation.");
            ShowStep(Step.Review);
        }
    }

    private void OnSkillAllocationConfirmed()
    {
        // Save the allocated skill ranks to character creation data
        var data = CreatedCharacters[CurrentCharacterIndex];
        data.SkillRanks.Clear();

        foreach (var kvp in _tempStatsForSkills.Skills)
        {
            if (kvp.Value.Ranks > 0)
            {
                data.SkillRanks[kvp.Key] = kvp.Value.Ranks;
            }
        }

        int totalSpent = _tempStatsForSkills.TotalSkillPoints - _tempStatsForSkills.AvailableSkillPoints;
        Debug.Log($"[CharCreation] Skill allocation complete: {totalSpent}/{_tempStatsForSkills.TotalSkillPoints} points spent, {data.SkillRanks.Count} skills with ranks.");

        _tempStatsForSkills = null;
        ShowStep(Step.SelectFeats);
    }

    // ========== STEP 5b: SELECT FEATS ==========

    private int _featSelectionPhase = 0; // 0 = general feat, 1 = fighter bonus feat

    private void StartFeatSelection()
    {
        var data = CreatedCharacters[CurrentCharacterIndex];
        data.ComputeFinalStats();

        FeatDefinitions.Init();

        // Create a temp stats for prerequisite checking
        var tempStats = new CharacterStats(
            name: data.CharacterName.Length > 0 ? data.CharacterName : (CurrentCharacterIndex == 0 ? "Hero 1" : "Hero 2"),
            level: 3,
            characterClass: data.ClassName,
            str: data.FinalSTR, dex: data.FinalDEX, con: data.FinalCON,
            wis: data.FinalWIS, intelligence: data.FinalINT, cha: data.FinalCHA,
            bab: data.BAB,
            armorBonus: 0, shieldBonus: 0,
            damageDice: 8, damageCount: 1, bonusDamage: 0,
            baseSpeed: data.BaseSpeed, atkRange: 1,
            baseHitDieHP: data.HP,
            raceName: data.RaceName
        );

        // Characters at level 3 get 2 general feats (level 1 + level 3)
        // Fighters also get 2 bonus feats (level 1 + level 2)
        // Humans get 1 bonus feat at level 1
        // Note: InitFeats only grants truly automatic feats (Monk: Improved Unarmed Strike only).
        // All other feats — general, bonus, racial — are selected here.

        _featSelectionPhase = 0;
        data.SelectedFeats.Clear();
        data.BonusFeats.Clear();

        if (FeatUI != null)
        {
            // General feats: 2 at level 3 (lvl 1 + lvl 3)
            int generalFeats = 2;

            // Human bonus feat adds 1 more
            bool isHuman = data.RaceName == "Human";
            if (isHuman) generalFeats++;

            // Build descriptive title and subtitle
            string featTitle;
            string featSubtitle;
            if (isHuman)
            {
                featTitle = $"Select General Feats + Human Bonus Feat ({generalFeats} total)";
                featSubtitle = "Includes 2 general feats (Lvl 1 & 3) + 1 bonus Human feat — any feat you qualify for";
            }
            else
            {
                featTitle = generalFeats == 1 ? "Select General Feat" : $"Select {generalFeats} General Feats";
                featSubtitle = "Choose from any feat you meet the prerequisites for";
            }

            Debug.Log($"[CharCreation] {data.ClassName}: selecting {generalFeats} general feats (human={isHuman})");

            FeatUI.OnFeatsConfirmed = (selectedFeats) => OnGeneralFeatsSelected(selectedFeats, tempStats);
            FeatUI.OpenForSelection(tempStats, generalFeats, false, featTitle, featSubtitle);
        }
        else
        {
            Debug.LogWarning("[CharCreation] FeatUI not available, skipping feat selection.");
            ShowStep(Step.Review);
        }
    }

    private void OnGeneralFeatsSelected(List<string> feats, CharacterStats tempStats)
    {
        var data = CreatedCharacters[CurrentCharacterIndex];
        data.SelectedFeats = new List<string>(feats);

        Debug.Log($"[CharCreation] General feats selected: {string.Join(", ", feats)}");

        // Add selected feats to temp stats for prerequisite checking in next phase
        foreach (string f in feats)
            tempStats.Feats.Add(f);

        // Class-specific bonus feats
        if (data.ClassName == "Fighter")
        {
            int bonusFeats = 2; // Level 1 + Level 2 bonus feats
            Debug.Log($"[CharCreation] Fighter: selecting {bonusFeats} bonus feats");

            string title = bonusFeats == 1
                ? "Select Fighter Bonus Feat"
                : $"Select {bonusFeats} Fighter Bonus Feats";

            FeatUI.OnFeatsConfirmed = (bonusSelected) => OnBonusFeatsSelected(bonusSelected);
            FeatUI.OpenForSelection(tempStats, bonusFeats, true,
                title, "Combat feats only — granted by Fighter class at levels 1 and 2");
        }
        else if (data.ClassName == "Monk")
        {
            // D&D 3.5e Monk bonus feats: sequential selection
            // Level 1: Improved Grapple or Stunning Fist
            // Level 2: Combat Reflexes or Deflect Arrows
            // Level 6+: also Improved Disarm or Improved Trip
            // Monks do NOT need to meet prerequisites for these bonus feats
            Debug.Log($"[CharCreation] Monk: starting sequential bonus feat selection (level 1 first)");

            data.BonusFeats.Clear();
            FeatUI.OnFeatsConfirmed = (bonusSelected) => OnMonkBonusFeatLevel1Selected(bonusSelected, tempStats);
            FeatUI.OpenForSelection(tempStats, 1, false,
                "Select Monk Bonus Feat (Level 1)",
                "Choose: Improved Grapple or Stunning Fist — prerequisites are bypassed",
                monkBonusLevel: 1);
        }
        else if (data.ClassName == "Wizard")
        {
            // ================================================================
            // D&D 3.5e Wizard Bonus Feats:
            // Wizards gain bonus feats at levels 5, 10, 15, and 20.
            // They can choose from: Metamagic feats, Item Creation feats, or Spell Mastery.
            // Unlike Monk bonus feats, Wizards MUST meet all prerequisites
            // (including caster level minimums for item creation feats).
            //
            // Note: Scribe Scroll is a free class feature at Wizard level 1,
            // not one of the bonus feats. That should be handled separately
            // during class initialization (not yet implemented).
            //
            // At level 3, Wizards don't get any bonus feats yet.
            // This system is ready for level 5+ when leveling is implemented.
            // ================================================================
            int wizLevel = 3; // current character level
            int bonusFeats = FeatDefinitions.GetWizardBonusFeatCount(wizLevel);

            if (bonusFeats > 0)
            {
                Debug.Log($"[CharCreation] Wizard at level {wizLevel}: selecting {bonusFeats} bonus feat(s)");

                FeatUI.OnFeatsConfirmed = (bonusSelected) => OnBonusFeatsSelected(bonusSelected);
                FeatUI.OpenForSelection(tempStats, bonusFeats, false,
                    $"Select Wizard Bonus Feat (Level {wizLevel})",
                    "Choose: Metamagic, Item Creation, or Spell Mastery — must meet prerequisites",
                    wizardBonus: true);
            }
            else
            {
                Debug.Log($"[CharCreation] Wizard at level {wizLevel}: no bonus feats yet (first at level 5)");
                ShowStep(Step.SelectSpells);
            }
        }
        else
        {
            ShowStep(Step.SelectSpells);
        }
    }

    private void OnBonusFeatsSelected(List<string> feats)
    {
        var data = CreatedCharacters[CurrentCharacterIndex];
        data.BonusFeats = new List<string>(feats);

        Debug.Log($"[CharCreation] Bonus feats selected: {string.Join(", ", feats)}");
        ShowStep(Step.SelectSpells);
    }

    /// <summary>Called after selecting the Monk level 1 bonus feat. Proceeds to level 2 selection.</summary>
    private void OnMonkBonusFeatLevel1Selected(List<string> feats, CharacterStats tempStats)
    {
        var data = CreatedCharacters[CurrentCharacterIndex];
        data.BonusFeats.AddRange(feats);

        // Add to temp stats so they show as already owned
        foreach (string f in feats)
            tempStats.Feats.Add(f);

        Debug.Log($"[CharCreation] Monk level 1 bonus feat selected: {string.Join(", ", feats)}");

        // Now select level 2 bonus feat (Combat Reflexes or Deflect Arrows)
        // Character level 3 means they get both level 1 and level 2 monk bonus feats
        FeatUI.OnFeatsConfirmed = (bonusSelected) => OnMonkBonusFeatLevel2Selected(bonusSelected, tempStats);
        FeatUI.OpenForSelection(tempStats, 1, false,
            "Select Monk Bonus Feat (Level 2)",
            "Choose: Combat Reflexes or Deflect Arrows — prerequisites are bypassed",
            monkBonusLevel: 2);
    }

    /// <summary>Called after selecting the Monk level 2 bonus feat. Proceeds to review (or level 6 if applicable).</summary>
    private void OnMonkBonusFeatLevel2Selected(List<string> feats, CharacterStats tempStats)
    {
        var data = CreatedCharacters[CurrentCharacterIndex];
        data.BonusFeats.AddRange(feats);

        // Add to temp stats
        foreach (string f in feats)
            tempStats.Feats.Add(f);

        Debug.Log($"[CharCreation] Monk level 2 bonus feat selected: {string.Join(", ", feats)}");
        Debug.Log($"[CharCreation] All Monk bonus feats: {string.Join(", ", data.BonusFeats)}");

        // At level 6+, there would be another selection for Improved Disarm or Improved Trip
        // For now (level 3), we're done with monk bonus feats
        ShowStep(Step.SelectSpells);
    }

    // ========== STEP 5c: SELECT SPELLS ==========

    private void StartSpellSelection()
    {
        var data = CreatedCharacters[CurrentCharacterIndex];

        // Only spellcasters need spell selection
        ClassRegistry.Init();
        ICharacterClass classDef = ClassRegistry.GetClass(data.ClassName);
        bool isSpellcaster = classDef != null && classDef.IsSpellcaster;

        if (!isSpellcaster)
        {
            Debug.Log($"[CharCreation] {data.ClassName} is not a spellcaster, skipping spell selection.");
            ShowStep(Step.Review);
            return;
        }

        if (SpellUI == null)
        {
            Debug.LogWarning("[CharCreation] SpellUI not available, skipping spell selection.");
            ShowStep(Step.Review);
            return;
        }

        SpellDatabase.Init();

        if (data.ClassName == "Wizard")
        {
            data.ComputeFinalStats();
            int intMod = CharacterStats.GetModifier(data.FinalINT);
            Debug.Log($"[CharCreation] Wizard spell selection: INT mod = {intMod}, FinalINT = {data.FinalINT}");

            // STEP 1: Build Spellbook (all cantrips auto-added, select higher-level spells)
            SpellUI.OnSpellsConfirmed = (selectedSpellIds) =>
            {
                data.SelectedSpellIds = new List<string>(selectedSpellIds);
                Debug.Log($"[CharCreation] Wizard spellbook built: {selectedSpellIds.Count} spells total");

                // STEP 2: Prepare Spells (assign spells from spellbook to slots)
                if (SpellPrepUI != null)
                {
                    SpellPrepUI.OnCreationPreparationConfirmed = (preparedSlotIds) =>
                    {
                        data.PreparedSpellSlotIds = new List<string>(preparedSlotIds);
                        Debug.Log($"[CharCreation] Wizard spell preparation complete: {preparedSlotIds.Count} slots");
                        ShowStep(Step.Review);
                    };
                    SpellPrepUI.OpenForCreation(data.SelectedSpellIds, intMod, 3, data.CharacterName);
                }
                else
                {
                    Debug.LogWarning("[CharCreation] SpellPrepUI not available, skipping preparation step.");
                    ShowStep(Step.Review);
                }
            };
            SpellUI.OpenForWizard(intMod, 3);
        }
        else if (data.ClassName == "Cleric")
        {
            data.ComputeFinalStats();
            int wisMod = CharacterStats.GetModifier(data.FinalWIS);
            Debug.Log($"[CharCreation] Cleric spell selection: WIS mod = {wisMod}, FinalWIS = {data.FinalWIS}");

            // STEP 1: Select Orisons (level 0 spells — clerics choose which ones to prepare)
            SpellUI.OnSpellsConfirmed = (selectedSpellIds) =>
            {
                data.SelectedSpellIds = new List<string>(selectedSpellIds);
                Debug.Log($"[CharCreation] Cleric orisons selected: {selectedSpellIds.Count} spells");

                // STEP 2: Prepare Spells (assign spells from full cleric list to slots)
                if (SpellPrepUI != null)
                {
                    SpellPrepUI.OnCreationPreparationConfirmed = (preparedSlotIds) =>
                    {
                        data.PreparedSpellSlotIds = new List<string>(preparedSlotIds);
                        Debug.Log($"[CharCreation] Cleric spell preparation complete: {preparedSlotIds.Count} slots");
                        ShowStep(Step.Review);
                    };
                    SpellPrepUI.OpenForClericCreation(wisMod, 3, data.CharacterName, data.ChosenDomains);
                }
                else
                {
                    Debug.LogWarning("[CharCreation] SpellPrepUI not available, skipping preparation step.");
                    ShowStep(Step.Review);
                }
            };
            SpellUI.OpenForCleric();
        }
        else
        {
            // Other spellcaster classes (future: Sorcerer, Druid, etc.)
            Debug.Log($"[CharCreation] {data.ClassName} spell selection not yet implemented.");
            ShowStep(Step.Review);
        }
    }

    // ========== STEP 6: REVIEW ==========

    private GameObject _step5Panel;

    private void BuildStepReview()
    {
        _step5Panel = CreatePanel(_contentArea.transform, "Step5",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(PANEL_W - 60, PANEL_H - 150),
            new Color(0, 0, 0, 0));

        // Name input
        MakeText(_step5Panel.transform, "NameLabel",
            new Vector2(-160, 220), new Vector2(120, 30),
            "Character Name:", 16, Color.white, TextAnchor.MiddleRight);

        GameObject inputGO = new GameObject("NameInput");
        inputGO.transform.SetParent(_step5Panel.transform, false);
        RectTransform inputRT = inputGO.AddComponent<RectTransform>();
        inputRT.anchorMin = new Vector2(0.5f, 0.5f);
        inputRT.anchorMax = new Vector2(0.5f, 0.5f);
        inputRT.pivot = new Vector2(0.5f, 0.5f);
        inputRT.anchoredPosition = new Vector2(60, 220);
        inputRT.sizeDelta = new Vector2(280, 35);

        Image inputBG = inputGO.AddComponent<Image>();
        inputBG.color = new Color(0.2f, 0.2f, 0.25f);

        _nameInput = inputGO.AddComponent<InputField>();
        _nameInput.characterLimit = 20;

        // Create child text for input
        Text inputText = MakeText(inputGO.transform, "InputText",
            Vector2.zero, new Vector2(270, 30),
            "", 18, Color.white, TextAnchor.MiddleLeft);
        RectTransform itRT = inputText.GetComponent<RectTransform>();
        itRT.anchorMin = Vector2.zero;
        itRT.anchorMax = Vector2.one;
        itRT.offsetMin = new Vector2(8, 2);
        itRT.offsetMax = new Vector2(-8, -2);
        _nameInput.textComponent = inputText;

        // Placeholder
        Text placeholder = MakeText(inputGO.transform, "Placeholder",
            Vector2.zero, new Vector2(270, 30),
            "Enter name...", 18, new Color(0.5f, 0.5f, 0.5f), TextAnchor.MiddleLeft);
        RectTransform phRT = placeholder.GetComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero;
        phRT.anchorMax = Vector2.one;
        phRT.offsetMin = new Vector2(8, 2);
        phRT.offsetMax = new Vector2(-8, -2);
        _nameInput.placeholder = placeholder;

        // Review scroll area — wraps _reviewText in a ScrollRect for long character sheets
        float reviewScrollH = 360f;
        GameObject reviewScrollArea = CreatePanel(_step5Panel.transform, "ReviewScrollArea",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, -10), new Vector2(760, reviewScrollH),
            new Color(0.08f, 0.08f, 0.12f, 0.6f));

        // Viewport with mask
        GameObject reviewViewport = new GameObject("ReviewViewport");
        reviewViewport.transform.SetParent(reviewScrollArea.transform, false);
        RectTransform rvpRT = reviewViewport.AddComponent<RectTransform>();
        rvpRT.anchorMin = Vector2.zero;
        rvpRT.anchorMax = Vector2.one;
        rvpRT.offsetMin = Vector2.zero;
        rvpRT.offsetMax = Vector2.zero;
        reviewViewport.AddComponent<Image>().color = Color.white;
        reviewViewport.AddComponent<Mask>().showMaskGraphic = false;

        // Content container that can grow beyond viewport
        GameObject reviewContent = new GameObject("ReviewContent");
        reviewContent.transform.SetParent(reviewViewport.transform, false);
        RectTransform rcRT = reviewContent.AddComponent<RectTransform>();
        rcRT.anchorMin = new Vector2(0, 1);
        rcRT.anchorMax = new Vector2(1, 1);
        rcRT.pivot = new Vector2(0.5f, 1);
        rcRT.anchoredPosition = Vector2.zero;
        rcRT.sizeDelta = new Vector2(0, 0); // Will be set dynamically

        // ScrollRect
        ScrollRect reviewScrollRect = reviewScrollArea.AddComponent<ScrollRect>();
        reviewScrollRect.content = rcRT;
        reviewScrollRect.viewport = rvpRT;
        reviewScrollRect.vertical = true;
        reviewScrollRect.horizontal = false;
        reviewScrollRect.movementType = ScrollRect.MovementType.Clamped;
        reviewScrollRect.scrollSensitivity = 30f;

        // Vertical scrollbar for the review panel
        ScrollbarHelper.CreateVerticalScrollbar(reviewScrollRect, reviewScrollArea.transform);

        // Review text inside scrollable content
        _reviewText = MakeText(reviewContent.transform, "ReviewText",
            new Vector2(0, 0), new Vector2(740, 360),
            "", 14, new Color(0.9f, 0.9f, 0.85f), TextAnchor.UpperCenter);
        // Anchor the review text to fill the content width, top-aligned
        RectTransform reviewTextRT = _reviewText.GetComponent<RectTransform>();
        reviewTextRT.anchorMin = new Vector2(0, 1);
        reviewTextRT.anchorMax = new Vector2(1, 1);
        reviewTextRT.pivot = new Vector2(0.5f, 1);
        reviewTextRT.anchoredPosition = new Vector2(0, 0);
        reviewTextRT.sizeDelta = new Vector2(-20, 0);
        _reviewText.verticalOverflow = VerticalWrapMode.Overflow;

        // Store reference to content RT so we can resize in RefreshReview
        _reviewScrollContentRT = rcRT;

        // Create Character button
        _createButton = MakeButton(_step5Panel.transform, "CreateBtn",
            new Vector2(0, -220), new Vector2(260, 50),
            "Create Character ✓", new Color(0.15f, 0.55f, 0.15f), Color.white, 22);
        _createButton.onClick.AddListener(OnCreateCharacter);
    }

    private void RefreshReview()
    {
        var data = CreatedCharacters[CurrentCharacterIndex];
        data.ComputeFinalStats();

        // Compute AC and attack based on class equipment
        int armorBonus = 0, shieldBonus = 0, weaponDie = 8;
        string weaponName = "", armorName = "";
        if (data.ClassName == "Fighter")
        {
            armorBonus = 4; shieldBonus = 2; weaponDie = 8;
            armorName = "Scale Mail"; weaponName = "Longsword (1d8)";
        }
        else
        {
            armorBonus = 2; shieldBonus = 0; weaponDie = 6;
            armorName = "Leather Armor"; weaponName = "Rapier (1d6)";
        }

        int dexMod = CharacterStats.GetModifier(data.FinalDEX);
        int strMod = CharacterStats.GetModifier(data.FinalSTR);
        int sizeMod = data.Race != null ? data.Race.SizeACAndAttackModifier : 0;

        // Max Dex for armor
        int maxDex = data.ClassName == "Fighter" ? 3 : 6; // Scale mail = 3, Leather = 6
        int effectiveDex = dexMod > maxDex ? maxDex : dexMod;

        int ac = 10 + effectiveDex + armorBonus + shieldBonus + sizeMod;
        int attackBonus = data.BAB + strMod + sizeMod;

        string sizeStr = data.Race != null && data.Race.SizeACAndAttackModifier != 0 ?
            $" ({data.Race.SizeName}: {CharacterStats.FormatMod(sizeMod)} AC/Atk)" : "";

        string alignStr = data.ChosenAlignment != Alignment.None
            ? AlignmentHelper.GetFullName(data.ChosenAlignment)
            : "None";

        // Deity and domain info
        DeityDatabase.Init();
        DomainDatabase.Init();
        DeityData reviewDeity = !string.IsNullOrEmpty(data.ChosenDeityId)
            ? DeityDatabase.GetDeity(data.ChosenDeityId) : null;
        string deityStr = reviewDeity != null ? $"{reviewDeity.Name} ({reviewDeity.Title})" : "None";
        string domainsStr = data.ChosenDomains.Count > 0 ? string.Join(", ", data.ChosenDomains) : "None";

        string review = $"══════════ CHARACTER SHEET ══════════\n\n";
        review += $"Race: {data.RaceName}   Class: {data.ClassName}   Level: 3{sizeStr}\n";
        review += $"Alignment: {alignStr}\n";
        review += $"Deity: {deityStr}\n";
        if (data.ClassName == "Cleric" && data.ChosenDomains.Count > 0)
        {
            review += $"Domains: {domainsStr}\n";
            foreach (string domName in data.ChosenDomains)
            {
                DomainData dom = DomainDatabase.GetDomain(domName);
                if (dom != null)
                    review += $"  • {domName}: {dom.GrantedPower}\n";
            }
        }
        if (data.ClassName == "Cleric" && data.SpontaneousCasting != SpontaneousCastingType.None)
        {
            string spontStr = data.SpontaneousCasting == SpontaneousCastingType.Cure
                ? "☀ Cure Spells (Positive Energy)"
                : "💀 Inflict Spells (Negative Energy)";
            review += $"Spontaneous Casting: {spontStr}\n";
        }
        review += "\n";

        review += "--- Ability Scores ---\n";
        review += data.GetFinalStatString("STR", data.STR, data.Race != null ? data.Race.STRModifier : 0) + "\n";
        review += data.GetFinalStatString("DEX", data.DEX, data.Race != null ? data.Race.DEXModifier : 0) + "\n";
        review += data.GetFinalStatString("CON", data.CON, data.Race != null ? data.Race.CONModifier : 0) + "\n";
        review += data.GetFinalStatString("INT", data.INT, data.Race != null ? data.Race.INTModifier : 0) + "\n";
        review += data.GetFinalStatString("WIS", data.WIS, data.Race != null ? data.Race.WISModifier : 0) + "\n";
        review += data.GetFinalStatString("CHA", data.CHA, data.Race != null ? data.Race.CHAModifier : 0) + "\n\n";

        review += "--- Combat Stats ---\n";
        review += $"HP: {data.HP}   (Hit Die: d{data.HitDie} + CON mod per level)\n";
        review += $"AC: {ac}   (10 + {effectiveDex} DEX + {armorBonus} armor + {shieldBonus} shield{(sizeMod != 0 ? $" + {sizeMod} size" : "")})\n";
        review += $"Attack: {CharacterStats.FormatMod(attackBonus)}   (BAB {CharacterStats.FormatMod(data.BAB)} + {CharacterStats.FormatMod(strMod)} STR{(sizeMod != 0 ? $" + {sizeMod} size" : "")})\n";
        review += $"Speed: {(data.Race != null ? data.Race.BaseSpeedFeet : 30)} ft ({data.BaseSpeed} squares)\n\n";

        review += "--- Equipment ---\n";
        review += $"Armor: {armorName}\n";
        if (data.ClassName == "Fighter") review += "Shield: Heavy Wooden Shield\n";
        review += $"Weapon: {weaponName}\n";
        review += "Shortbow + 20 arrows\n";
        if (data.ClassName == "Rogue") review += "Thieves' tools\n";
        review += "Backpack, bedroll, waterskin, rations\n";

        // Show allocated skills
        if (data.SkillRanks != null && data.SkillRanks.Count > 0)
        {
            review += "\n--- Skills ---\n";
            foreach (var kvp in data.SkillRanks)
            {
                string skillName = kvp.Key;
                int ranks = kvp.Value;
                review += $"  {skillName}: {ranks} ranks\n";
            }
        }

        // Show selected feats
        if ((data.SelectedFeats != null && data.SelectedFeats.Count > 0) ||
            (data.BonusFeats != null && data.BonusFeats.Count > 0))
        {
            review += "\n--- Feats ---\n";
            if (data.SelectedFeats != null)
            {
                foreach (string feat in data.SelectedFeats)
                    review += $"  • {feat}\n";
            }
            if (data.BonusFeats != null && data.BonusFeats.Count > 0)
            {
                string bonusLabel = data.ClassName == "Monk" ? "Monk Bonus Feats" :
                                    data.ClassName == "Fighter" ? "Fighter Bonus Feats" :
                                    data.ClassName == "Wizard" ? "Wizard Bonus Feats" :
                                    "Class Bonus Feats";
                review += $"  ({bonusLabel})\n";
                foreach (string feat in data.BonusFeats)
                    review += $"  • {feat}\n";
            }
            if (!string.IsNullOrEmpty(data.WeaponFocusChoice))
                review += $"  Weapon Focus: {data.WeaponFocusChoice}\n";
            if (!string.IsNullOrEmpty(data.SkillFocusChoice))
                review += $"  Skill Focus: {data.SkillFocusChoice}\n";
        }

        // Show spells for spellcasters
        ClassRegistry.Init();
        ICharacterClass reviewClassDef = ClassRegistry.GetClass(data.ClassName);
        if (reviewClassDef != null && reviewClassDef.IsSpellcaster)
        {
            review += "\n--- Spells ---\n";
            if (data.ClassName == "Wizard")
            {
                if (data.SelectedSpellIds != null && data.SelectedSpellIds.Count > 0)
                {
                    SpellDatabase.Init();
                    // Separate cantrips from higher-level spells for display
                    var cantrips = new List<string>();
                    var higherSpells = new List<string>();
                    foreach (string spellId in data.SelectedSpellIds)
                    {
                        SpellData spell = SpellDatabase.GetSpell(spellId);
                        if (spell != null)
                        {
                            string entry = $"  • {spell.Name} (Lvl {spell.SpellLevel}){(spell.IsPlaceholder ? " [P]" : "")}";
                            if (spell.SpellLevel == 0)
                                cantrips.Add(entry);
                            else
                                higherSpells.Add(entry);
                        }
                    }
                    if (cantrips.Count > 0)
                    {
                        review += "  Cantrips:\n";
                        foreach (string c in cantrips) review += c + "\n";
                    }
                    if (higherSpells.Count > 0)
                    {
                        review += "  Spellbook:\n";
                        foreach (string s in higherSpells) review += s + "\n";
                    }
                }
                else
                {
                    review += "  (No spells selected yet)\n";
                }
            }
            else if (data.ClassName == "Cleric")
            {
                if (data.SelectedSpellIds != null && data.SelectedSpellIds.Count > 0)
                {
                    SpellDatabase.Init();
                    review += "  Selected Orisons:\n";
                    foreach (string spellId in data.SelectedSpellIds)
                    {
                        SpellData spell = SpellDatabase.GetSpell(spellId);
                        if (spell != null && spell.SpellLevel == 0)
                            review += $"  • {spell.Name}\n";
                    }
                }
                review += "  Prepares from full 1st & 2nd level spell list each day.\n";
            }
        }

        _reviewText.text = review;

        // Resize scroll content to fit the review text
        if (_reviewScrollContentRT != null)
        {
            // Estimate height based on text: ~18px per line
            int lineCount = review.Split('\n').Length;
            float estimatedHeight = Mathf.Max(360f, lineCount * 18f + 20f);
            _reviewScrollContentRT.sizeDelta = new Vector2(0, estimatedHeight);
        }

        // Default name
        if (string.IsNullOrEmpty(_nameInput.text))
        {
            _nameInput.text = (CurrentCharacterIndex < DefaultNames.Length) ? DefaultNames[CurrentCharacterIndex] : $"Hero {CurrentCharacterIndex + 1}";
        }
    }

    private void OnCreateCharacter()
    {
        var data = CreatedCharacters[CurrentCharacterIndex];
        data.CharacterName = string.IsNullOrEmpty(_nameInput.text) ?
            $"Hero {CurrentCharacterIndex + 1}" : _nameInput.text;
        data.ComputeFinalStats();

        Debug.Log($"[CharCreation] Created {data.CharacterName}: {data.RaceName} {data.ClassName} " +
                  $"STR {data.FinalSTR} DEX {data.FinalDEX} CON {data.FinalCON} " +
                  $"INT {data.FinalINT} WIS {data.FinalWIS} CHA {data.FinalCHA} HP {data.HP}");

        if (CurrentCharacterIndex < TotalPCs - 1)
        {
            // Move to next PC creation
            CurrentCharacterIndex++;
            ResetForNewCharacter();
            ShowStep(Step.RollStats);
        }
        else
        {
            // All characters created - start game!
            IsComplete = true;
            HideCreationUI();

            NotifyCreationComplete("StandardFlow");
        }
    }

    // ========== NAVIGATION ==========

    private void ShowStep(Step step)
    {
        CurrentStep = step;

        // Hide all step panels
        _step1Panel.SetActive(false);
        _step2Panel.SetActive(false);
        _step3Panel.SetActive(false);
        _step4Panel.SetActive(false);
        _stepAlignPanel.SetActive(false);
        _stepDeityPanel.SetActive(false);
        _stepDomainPanel.SetActive(false);
        _stepSpontCastPanel.SetActive(false);
        _step5Panel.SetActive(false);

        string heroLabel = $"Hero {CurrentCharacterIndex + 1} of {TotalPCs}";
        _titleText.text = $"CHARACTER CREATION - {heroLabel}";
        _backButton.gameObject.SetActive(step != Step.RollStats);

        switch (step)
        {
            case Step.RollStats:
                _step1Panel.SetActive(true);
                _stepText.text = "Step 1 of 12: Roll Stats";
                // Reset roll UI
                if (_currentRolls == null)
                {
                    _rollButton.gameObject.SetActive(true);
                    _rerollButton.gameObject.SetActive(false);
                    _acceptStatsButton.gameObject.SetActive(false);
                    _rollDetailsText.text = "(Click 'Roll Stats' to begin)";
                    _rollResultsText.text = "";
                }
                break;

            case Step.AssignStats:
                _step2Panel.SetActive(true);
                _stepText.text = "Step 2 of 12: Assign Stats";
                // Reset assignment
                _assignedValues = new int[] { -1, -1, -1, -1, -1, -1 };
                _rollUsed = new bool[6];
                _selectedRollIndex = -1;
                RefreshAssignUI();
                break;

            case Step.ChooseRace:
                _step3Panel.SetActive(true);
                _stepText.text = "Step 3 of 12: Choose Race";
                _selectedRace = null;
                _raceInfoText.text = "Select a race to see details.";
                _racePreviewText.text = "";
                _confirmRaceButton.interactable = false;
                // Reset button colors
                for (int i = 0; i < _raceButtons.Length; i++)
                {
                    var c = _raceButtons[i].colors;
                    c.normalColor = new Color(0.25f, 0.25f, 0.4f);
                    c.highlightedColor = c.normalColor * 1.2f;
                    _raceButtons[i].colors = c;
                }
                break;

            case Step.ChooseClass:
                _step4Panel.SetActive(true);
                _stepText.text = "Step 4 of 12: Choose Class";
                _selectedClass = null;
                _classInfoText.text = "";
                _confirmClassButton.interactable = false;
                for (int i = 0; i < _classButtons.Length; i++)
                {
                    var c = _classButtons[i].colors;
                    c.normalColor = i == 0 ? new Color(0.5f, 0.3f, 0.15f) : new Color(0.2f, 0.4f, 0.2f);
                    c.highlightedColor = c.normalColor * 1.2f;
                    _classButtons[i].colors = c;
                }
                break;

            case Step.ChooseAlignment:
                _stepAlignPanel.SetActive(true);
                _stepText.text = "Step 5 of 12: Choose Alignment";
                _selectedAlignment = Alignment.None;
                _alignInfoText.text = "Select an alignment to see its description.";
                _confirmAlignButton.interactable = false;
                // Show class restriction if any
                string restriction = AlignmentHelper.GetClassRestrictionText(
                    CreatedCharacters[CurrentCharacterIndex].ClassName);
                _alignRestrictionText.text = restriction;
                RefreshAlignmentButtons();
                break;

            case Step.ChooseDeity:
                _stepDeityPanel.SetActive(true);
                _stepText.text = "Step 6 of 12: Choose Deity";
                _selectedDeityId = "";
                _deityInfoText.text = "Select a deity to see details.";
                _confirmDeityButton.interactable = false;
                string deityNote = CreatedCharacters[CurrentCharacterIndex].ClassName == "Cleric"
                    ? "Clerics must choose a deity within one step of their alignment."
                    : "Deity is optional for non-cleric characters.";
                _deityRestrictionText.text = deityNote;
                RefreshDeityButtons();
                break;

            case Step.ChooseDomains:
                _stepDomainPanel.SetActive(true);
                _stepText.text = "Step 7 of 12: Choose Domains";
                _selectedDomains.Clear();
                _domainInfoText.text = "Select a domain to see details.";
                _confirmDomainButton.interactable = false;
                PopulateDomainButtons();
                break;

            case Step.ChooseSpontaneousCasting:
                _stepSpontCastPanel.SetActive(true);
                _stepText.text = "Step 8 of 12: Spontaneous Casting";
                _selectedSpontCasting = SpontaneousCastingType.None;
                _spontCastInfoText.text = "Select Cure or Inflict spells above.";
                _confirmSpontCastButton.interactable = false;
                // Reset button colors
                var cureC = _cureButton.colors;
                cureC.normalColor = new Color(0.2f, 0.5f, 0.2f);
                cureC.highlightedColor = cureC.normalColor * 1.2f;
                _cureButton.colors = cureC;
                var inflC = _inflictButton.colors;
                inflC.normalColor = new Color(0.5f, 0.2f, 0.2f);
                inflC.highlightedColor = inflC.normalColor * 1.2f;
                _inflictButton.colors = inflC;
                break;

            case Step.AllocateSkills:
                // Skills allocation is handled by the SkillsUIPanel overlay
                _stepText.text = "Step 9 of 12: Allocate Skills";
                StartSkillAllocation();
                break;

            case Step.SelectFeats:
                _stepText.text = "Step 10 of 12: Select Feats";
                StartFeatSelection();
                break;

            case Step.SelectSpells:
                _stepText.text = "Step 11 of 12: Select Spells (incl. Cantrips)";
                StartSpellSelection();
                break;

            case Step.Review:
                _step5Panel.SetActive(true);
                _stepText.text = "Step 12 of 12: Review & Name";
                _nameInput.text = "";
                RefreshReview();
                break;
        }
    }

    private void OnBackPressed()
    {
        switch (CurrentStep)
        {
            case Step.AssignStats: ShowStep(Step.RollStats); break;
            case Step.ChooseRace: ShowStep(Step.AssignStats); break;
            case Step.ChooseClass: ShowStep(Step.ChooseRace); break;
            case Step.ChooseAlignment: ShowStep(Step.ChooseClass); break;
            case Step.ChooseDeity: ShowStep(Step.ChooseAlignment); break;
            case Step.ChooseDomains: ShowStep(Step.ChooseDeity); break;
            case Step.ChooseSpontaneousCasting: ShowStep(Step.ChooseDomains); break;
            case Step.AllocateSkills:
                // Close skills UI if open
                if (SkillsUI != null && SkillsUI.IsOpen)
                    SkillsUI.Close();
                // Go back to correct step depending on class/alignment
                var backData = CreatedCharacters[CurrentCharacterIndex];
                if (backData.ClassName == "Cleric" && !string.IsNullOrEmpty(backData.ChosenDeityId))
                {
                    // If neutral cleric, go back to spontaneous casting choice
                    if (AlignmentHelper.IsNeutralGE(backData.ChosenAlignment))
                        ShowStep(Step.ChooseSpontaneousCasting);
                    else
                        ShowStep(Step.ChooseDomains);
                }
                else
                    ShowStep(Step.ChooseDeity);
                break;
            case Step.SelectFeats:
                // Close feat UI if open
                if (FeatUI != null && FeatUI.IsOpen)
                    FeatUI.Close();
                ShowStep(Step.AllocateSkills);
                break;
            case Step.SelectSpells:
                // Close spell UI and preparation UI if open
                if (SpellUI != null && SpellUI.IsOpen)
                    SpellUI.Close();
                if (SpellPrepUI != null && SpellPrepUI.IsOpen)
                    SpellPrepUI.Close();
                ShowStep(Step.SelectFeats);
                break;
            case Step.Review: ShowStep(Step.SelectSpells); break;
        }
    }

    private void ResetForNewCharacter()
    {
        _currentRolls = null;
        _currentRollDetails = null;
        _selectedRace = null;
        _selectedClass = null;
        _selectedAlignment = Alignment.None;
        _selectedDeityId = "";
        _selectedDomains.Clear();
        _selectedSpontCasting = SpontaneousCastingType.None;
        _selectedRollIndex = -1;
        _assignedValues = new int[] { -1, -1, -1, -1, -1, -1 };
        _rollUsed = new bool[6];

        _rollButton.gameObject.SetActive(true);
        _rerollButton.gameObject.SetActive(false);
        _acceptStatsButton.gameObject.SetActive(false);
        _rollDetailsText.text = "(Click 'Roll Stats' to begin)";
        _rollResultsText.text = "";
    }

    // ========== PRE-MADE CHARACTER SELECTION (from Roll Stats screen) ==========

    /// <summary>
    /// Opens the pre-made character selection overlay from the Roll Stats step.
    /// Allows picking a quick start character for the current slot without going through
    /// all the creation steps.
    /// </summary>
    private void OnChoosePremadeCharacter()
    {
        Debug.Log($"[CharCreation] Opening pre-made character selection for slot {CurrentCharacterIndex + 1}");
        InitializeQuickStartCharacters();
        ShowPremadeSelectionPanel();
    }

    private void ShowPremadeSelectionPanel()
    {
        // Create or reuse the overlay
        if (_premadeOverlayPanel != null)
        {
            _premadeOverlayPanel.SetActive(true);
            foreach (Transform child in _premadeOverlayPanel.transform)
                GameObject.Destroy(child.gameObject);
        }
        else
        {
            Canvas canvas = _rootPanel.GetComponentInParent<Canvas>();
            _premadeOverlayPanel = CreatePanel(canvas.transform, "PremadeOverlay",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.9f));
            RectTransform oRT = _premadeOverlayPanel.GetComponent<RectTransform>();
            oRT.offsetMin = Vector2.zero;
            oRT.offsetMax = Vector2.zero;
            _premadeOverlayPanel.AddComponent<CanvasGroup>();
        }

        string[] classKeys = { "Fighter", "Rogue", "Cleric", "Wizard", "Monk", "Barbarian" };
        float panelH = 560f;
        float panelW = 520f;

        GameObject panel = CreatePanel(_premadeOverlayPanel.transform, "PremadePanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(panelW, panelH), new Color(0.12f, 0.12f, 0.18f, 0.98f));

        float topY = panelH / 2 - 30;

        MakeText(panel.transform, "PremadeTitle",
            new Vector2(0, topY), new Vector2(panelW - 40, 35),
            "CHOOSE PRE-MADE CHARACTER", 22, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);

        MakeText(panel.transform, "PremadeSubtitle",
            new Vector2(0, topY - 30), new Vector2(panelW - 40, 25),
            $"Select a character for Hero {CurrentCharacterIndex + 1}", 14, new Color(0.7f, 0.8f, 1f), TextAnchor.MiddleCenter);

        float btnStartY = topY - 75;
        float btnH = 60f;
        float btnSpacing = 6f;

        for (int i = 0; i < classKeys.Length; i++)
        {
            string className = classKeys[i];
            if (!_qsAvailableCharacters.ContainsKey(className)) continue;

            var ch = _qsAvailableCharacters[className];
            ch.ComputeFinalStats();

            float y = btnStartY - i * (btnH + btnSpacing);

            // Background button for the character
            ClassRegistry.Init();
            ICharacterClass classDef = ClassRegistry.GetClass(className);
            Color btnColor = classDef != null ? classDef.ButtonColor : new Color(0.25f, 0.25f, 0.4f);

            // Character entry: name, race, class + stat preview
            int strMod = CharacterStats.GetModifier(ch.FinalSTR);
            int dexMod = CharacterStats.GetModifier(ch.FinalDEX);
            int conMod = CharacterStats.GetModifier(ch.FinalCON);
            int intMod = CharacterStats.GetModifier(ch.FinalINT);
            int wisMod = CharacterStats.GetModifier(ch.FinalWIS);
            int chaMod = CharacterStats.GetModifier(ch.FinalCHA);

            string statLine = $"STR {ch.FinalSTR}({CharacterStats.FormatMod(strMod)})  DEX {ch.FinalDEX}({CharacterStats.FormatMod(dexMod)})  CON {ch.FinalCON}({CharacterStats.FormatMod(conMod)})  INT {ch.FinalINT}({CharacterStats.FormatMod(intMod)})  WIS {ch.FinalWIS}({CharacterStats.FormatMod(wisMod)})  CHA {ch.FinalCHA}({CharacterStats.FormatMod(chaMod)})";
            string label = $"{ch.CharacterName} — {ch.RaceName} {ch.ClassName}   HP: {ch.HP}\n<size=10>{statLine}</size>";

            Button charBtn = MakeButton(panel.transform, $"Premade_{className}",
                new Vector2(0, y), new Vector2(panelW - 50, btnH),
                "", btnColor, Color.white, 15);

            // Replace the auto-created label with a rich text one
            Text btnLabel = charBtn.GetComponentInChildren<Text>();
            if (btnLabel != null)
            {
                btnLabel.text = label;
                btnLabel.supportRichText = true;
                btnLabel.fontSize = 14;
            }

            string capturedClass = className;
            charBtn.onClick.AddListener(() => OnPremadeCharacterSelected(capturedClass));
        }

        // Cancel button
        float bottomY = -panelH / 2 + 35;
        Button cancelBtn = MakeButton(panel.transform, "PremadeCancel",
            new Vector2(0, bottomY), new Vector2(140, 40),
            "Cancel", new Color(0.5f, 0.2f, 0.2f), Color.white, 16);
        cancelBtn.onClick.AddListener(HidePremadeSelectionPanel);
    }

    private void HidePremadeSelectionPanel()
    {
        if (_premadeOverlayPanel != null)
            _premadeOverlayPanel.SetActive(false);
    }

    /// <summary>
    /// Called when a pre-made character is selected from the selection panel.
    /// Fills the current character slot and advances to the next character or finishes.
    /// </summary>
    private void OnPremadeCharacterSelected(string className)
    {
        if (!_qsAvailableCharacters.ContainsKey(className))
        {
            Debug.LogError($"[CharCreation] Pre-made character not found for class: {className}");
            return;
        }

        var premadeData = _qsAvailableCharacters[className];
        premadeData.ComputeFinalStats();
        CreatedCharacters[CurrentCharacterIndex] = premadeData;

        Debug.Log($"[CharCreation] Pre-made character selected for slot {CurrentCharacterIndex + 1}: " +
                  $"{premadeData.CharacterName} ({premadeData.RaceName} {premadeData.ClassName})");

        HidePremadeSelectionPanel();

        // Advance to next character or finish
        if (CurrentCharacterIndex < TotalPCs - 1)
        {
            CurrentCharacterIndex++;
            ResetForNewCharacter();
            ShowStep(Step.RollStats);
        }
        else
        {
            // All character slots filled — finish creation
            IsComplete = true;
            HideCreationUI();

            NotifyCreationComplete("PremadeSelection");

            Debug.Log("[CharCreation] All characters created (mix of custom and pre-made). Starting game!");
        }
    }

    // ========== QUICK START SELECTION FLOW ==========

    private void InitializeQuickStartCharacters()
    {
        FeatDefinitions.Init();
        SpellDatabase.Init();
        _qsAvailableCharacters = new Dictionary<string, CharacterCreationData>
        {
            { "Fighter", FighterClass.GetQuickStartCharacter() },
            { "Rogue", RogueClass.GetQuickStartCharacter() },
            { "Cleric", ClericClass.GetQuickStartCharacter() },
            { "Wizard", WizardClass.GetQuickStartCharacter() },
            { "Monk", MonkClass.GetQuickStartCharacter() },
            { "Barbarian", BarbarianClass.GetQuickStartCharacter() }
        };
        Debug.Log($"[QuickStart] Initialized {_qsAvailableCharacters.Count} quick start characters");
    }

    /// <summary>
    /// Very Quick Start — instantly creates a party of 4 (Fighter, Rogue, Cleric, Wizard)
    /// and starts the game with zero intermediate steps.
    /// </summary>
    private void OnVeryQuickStart()
    {
        Debug.Log("[PlayNow] Button clicked (Very Quick Start).");
        Debug.Log("[PlayNow] Creating instant party: Fighter, Rogue, Cleric, Wizard");
        InitializeQuickStartCharacters();

        string[] partyClasses = { "Fighter", "Rogue", "Cleric", "Wizard" };
        CreatedCharacters = new CharacterCreationData[partyClasses.Length];

        for (int i = 0; i < partyClasses.Length; i++)
        {
            if (_qsAvailableCharacters.ContainsKey(partyClasses[i]))
            {
                CreatedCharacters[i] = _qsAvailableCharacters[partyClasses[i]];
                Debug.Log($"[VeryQuickStart] Slot {i + 1}: {CreatedCharacters[i].CharacterName} ({CreatedCharacters[i].RaceName} {CreatedCharacters[i].ClassName})");
            }
            else
            {
                Debug.LogError($"[VeryQuickStart] Missing quick start character for class: {partyClasses[i]}");
                return;
            }
        }

        IsComplete = true;
        HideCreationUI();

        NotifyCreationComplete("VeryQuickStart");

        Debug.Log("[VeryQuickStart] Game started!");
    }

    private void OnQuickStart()
    {
        Debug.Log("[CharCreation] Quick Start selection flow opened");
        InitializeQuickStartCharacters();
        ShowQuickStartPartySize();
    }

    // --- Quick Start Overlay helper ---
    private void EnsureQSOverlay()
    {
        if (_qsOverlayPanel != null)
        {
            _qsOverlayPanel.SetActive(true);
            // Destroy children to rebuild
            foreach (Transform child in _qsOverlayPanel.transform)
                GameObject.Destroy(child.gameObject);
            return;
        }

        Canvas canvas = _rootPanel.GetComponentInParent<Canvas>();
        _qsOverlayPanel = CreatePanel(canvas.transform, "QSOverlay",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.9f));
        RectTransform oRT = _qsOverlayPanel.GetComponent<RectTransform>();
        oRT.offsetMin = Vector2.zero;
        oRT.offsetMax = Vector2.zero;
        _qsOverlayPanel.AddComponent<CanvasGroup>();
    }

    private void HideQSOverlay()
    {
        if (_qsOverlayPanel != null)
            _qsOverlayPanel.SetActive(false);
    }

    // ========== STEP 1: PARTY SIZE SELECTION ==========

    private void ShowQuickStartPartySize()
    {
        EnsureQSOverlay();

        GameObject panel = CreatePanel(_qsOverlayPanel.transform, "QSPartySizePanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(600, 360), new Color(0.12f, 0.12f, 0.18f, 0.98f));

        MakeText(panel.transform, "QSTitle",
            new Vector2(0, 130), new Vector2(540, 40),
            "QUICK START — SELECT PARTY SIZE", 24, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);

        MakeText(panel.transform, "QSSubtitle",
            new Vector2(0, 85), new Vector2(540, 30),
            "How many characters in your party?", 16, new Color(0.8f, 0.8f, 0.8f), TextAnchor.MiddleCenter);

        // Party size buttons: 1-6
        float startX = -175f;
        for (int i = 1; i <= 6; i++)
        {
            int size = i; // capture for closure
            Button btn = MakeButton(panel.transform, $"QSSize{i}",
                new Vector2(startX + (i - 1) * 70f, 20), new Vector2(56, 56),
                i.ToString(), new Color(0.2f, 0.35f, 0.55f), Color.white, 24);
            btn.onClick.AddListener(() => OnPartySizeSelected(size));
        }

        // Back button
        Button backBtn = MakeButton(panel.transform, "QSBackBtn",
            new Vector2(0, -80), new Vector2(120, 40),
            "← Back", new Color(0.4f, 0.4f, 0.4f), Color.white, 16);
        backBtn.onClick.AddListener(() =>
        {
            HideQSOverlay();
        });
    }

    // ========== STEP 2: CHARACTER SLOT SELECTION ==========

    private void OnPartySizeSelected(int size)
    {
        _qsSelectedPartySize = size;
        _qsSelectedClasses = new string[size];
        _qsCharacters = new CharacterCreationData[size];
        Debug.Log($"[QuickStart] Party size selected: {size}");
        ShowQuickStartSlotSelection();
    }

    private void ShowQuickStartSlotSelection()
    {
        EnsureQSOverlay();

        float panelHeight = 260 + _qsSelectedPartySize * 50;
        if (panelHeight < 360) panelHeight = 360;

        GameObject panel = CreatePanel(_qsOverlayPanel.transform, "QSSlotPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(700, panelHeight), new Color(0.12f, 0.12f, 0.18f, 0.98f));

        float topY = panelHeight / 2 - 30;

        MakeText(panel.transform, "QSSlotTitle",
            new Vector2(0, topY), new Vector2(640, 40),
            "QUICK START — SELECT CHARACTERS", 24, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);

        MakeText(panel.transform, "QSPartyInfo",
            new Vector2(0, topY - 35), new Vector2(640, 25),
            $"Party Size: {_qsSelectedPartySize}", 16, new Color(0.7f, 0.8f, 1f), TextAnchor.MiddleCenter);

        // Slot buttons
        _qsSlotButtons = new Button[_qsSelectedPartySize];
        _qsSlotLabels = new Text[_qsSelectedPartySize];

        float slotStartY = topY - 80;
        for (int i = 0; i < _qsSelectedPartySize; i++)
        {
            int slotIdx = i; // capture for closure
            float y = slotStartY - i * 50;

            MakeText(panel.transform, $"QSSlotLabel{i}",
                new Vector2(-230, y), new Vector2(100, 36),
                $"Slot {i + 1}:", 16, Color.white, TextAnchor.MiddleRight);

            Button slotBtn = MakeButton(panel.transform, $"QSSlotBtn{i}",
                new Vector2(30, y), new Vector2(300, 36),
                "[ Select Class ▼ ]", new Color(0.2f, 0.2f, 0.3f), new Color(0.7f, 0.7f, 0.7f), 16);
            slotBtn.onClick.AddListener(() => OnSlotClicked(slotIdx));
            _qsSlotButtons[i] = slotBtn;

            // Store the text reference so we can update label later
            _qsSlotLabels[i] = slotBtn.GetComponentInChildren<Text>();
        }

        // Bottom buttons
        float bottomY = -panelHeight / 2 + 40;

        Button backBtn = MakeButton(panel.transform, "QSSlotBack",
            new Vector2(-100, bottomY), new Vector2(120, 40),
            "← Back", new Color(0.4f, 0.4f, 0.4f), Color.white, 16);
        backBtn.onClick.AddListener(() => ShowQuickStartPartySize());

        _qsStartGameButton = MakeButton(panel.transform, "QSStartGame",
            new Vector2(100, bottomY), new Vector2(200, 44),
            "Start Game ✓", new Color(0.15f, 0.55f, 0.15f), Color.white, 20);
        _qsStartGameButton.onClick.AddListener(OnQSStartGame);
        _qsStartGameButton.interactable = false;

        // Refresh slot labels in case we already have selections
        RefreshSlotLabels();
    }

    private void RefreshSlotLabels()
    {
        if (_qsSlotLabels == null) return;
        bool allFilled = true;
        for (int i = 0; i < _qsSelectedPartySize; i++)
        {
            if (_qsSlotLabels[i] == null) continue;
            if (_qsSelectedClasses[i] != null && _qsAvailableCharacters.ContainsKey(_qsSelectedClasses[i]))
            {
                var ch = _qsAvailableCharacters[_qsSelectedClasses[i]];
                _qsSlotLabels[i].text = $"{ch.CharacterName} — {ch.RaceName} {ch.ClassName}";
                _qsSlotLabels[i].color = Color.white;

                // Recolor the button to class color
                ClassRegistry.Init();
                ICharacterClass classDef = ClassRegistry.GetClass(_qsSelectedClasses[i]);
                if (classDef != null)
                {
                    var cb = _qsSlotButtons[i].colors;
                    cb.normalColor = classDef.ButtonColor;
                    cb.highlightedColor = classDef.ButtonColor * 1.3f;
                    cb.pressedColor = classDef.ButtonColor * 0.7f;
                    _qsSlotButtons[i].colors = cb;
                }
            }
            else
            {
                _qsSlotLabels[i].text = "[ Select Class ▼ ]";
                _qsSlotLabels[i].color = new Color(0.7f, 0.7f, 0.7f);
                allFilled = false;
            }
        }
        if (_qsStartGameButton != null)
            _qsStartGameButton.interactable = allFilled;
    }

    // ========== STEP 3: CLASS SELECTION DROPDOWN ==========

    private void OnSlotClicked(int slotIndex)
    {
        _qsActiveSlot = slotIndex;
        ShowClassSelectionDropdown();
    }

    private void ShowClassSelectionDropdown()
    {
        EnsureQSOverlay();

        string[] classNames = { "Fighter", "Rogue", "Cleric", "Wizard", "Monk", "Barbarian" };
        float panelHeight = 120 + classNames.Length * 56;

        GameObject panel = CreatePanel(_qsOverlayPanel.transform, "QSClassDropdown",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(500, panelHeight), new Color(0.12f, 0.12f, 0.18f, 0.98f));

        float topY = panelHeight / 2 - 30;

        MakeText(panel.transform, "QSDropTitle",
            new Vector2(0, topY), new Vector2(460, 35),
            $"Select Class for Slot {_qsActiveSlot + 1}", 22, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);

        float btnStartY = topY - 55;
        for (int i = 0; i < classNames.Length; i++)
        {
            string className = classNames[i];
            float y = btnStartY - i * 56;

            var ch = _qsAvailableCharacters[className];
            string label = $"{ch.CharacterName} — {ch.RaceName} {className}";

            ClassRegistry.Init();
            ICharacterClass classDef = ClassRegistry.GetClass(className);
            Color btnColor = classDef != null ? classDef.ButtonColor : new Color(0.25f, 0.25f, 0.4f);

            Button classBtn = MakeButton(panel.transform, $"QSClass_{className}",
                new Vector2(0, y), new Vector2(420, 44),
                label, btnColor, Color.white, 17);
            classBtn.onClick.AddListener(() => OnClassSelectedForSlot(className));
        }

        // Cancel button
        float bottomY = -panelHeight / 2 + 30;
        Button cancelBtn = MakeButton(panel.transform, "QSDropCancel",
            new Vector2(0, bottomY), new Vector2(120, 36),
            "Cancel", new Color(0.4f, 0.4f, 0.4f), Color.white, 16);
        cancelBtn.onClick.AddListener(() => ShowQuickStartSlotSelection());
    }

    // ========== STEP 4: CHARACTER PREVIEW ==========

    private void OnClassSelectedForSlot(string className)
    {
        _qsSelectedClasses[_qsActiveSlot] = className;
        _qsCharacters[_qsActiveSlot] = _qsAvailableCharacters[className];
        ShowCharacterPreview(_qsActiveSlot);
    }

    private void ShowCharacterPreview(int slotIndex)
    {
        EnsureQSOverlay();
        var data = _qsCharacters[slotIndex];
        if (data == null) return;

        data.ComputeFinalStats();

        float previewW = 600f;
        float previewH = 600f;

        GameObject panel = CreatePanel(_qsOverlayPanel.transform, "QSPreviewPanel",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(previewW, previewH), new Color(0.12f, 0.12f, 0.18f, 0.98f));

        float topY = previewH / 2 - 30;

        MakeText(panel.transform, "QSPreviewTitle",
            new Vector2(0, topY), new Vector2(previewW - 40, 35),
            "CHARACTER PREVIEW", 22, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);

        // Build character sheet text
        string sheet = BuildQuickStartCharacterSheet(data);

        // Scroll area for the character sheet
        float scrollH = previewH - 130;
        GameObject scrollArea = CreatePanel(panel.transform, "QSPreviewScroll",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, -10), new Vector2(previewW - 40, scrollH),
            new Color(0.08f, 0.08f, 0.12f, 0.6f));

        // Viewport
        GameObject viewport = new GameObject("QSPreviewViewport");
        viewport.transform.SetParent(scrollArea.transform, false);
        RectTransform vpRT = viewport.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero;
        vpRT.offsetMax = Vector2.zero;
        viewport.AddComponent<Image>().color = Color.white;
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        // Content
        GameObject content = new GameObject("QSPreviewContent");
        content.transform.SetParent(viewport.transform, false);
        RectTransform cRT = content.AddComponent<RectTransform>();
        cRT.anchorMin = new Vector2(0, 1);
        cRT.anchorMax = new Vector2(1, 1);
        cRT.pivot = new Vector2(0.5f, 1);
        cRT.anchoredPosition = Vector2.zero;

        // ScrollRect
        ScrollRect sr = scrollArea.AddComponent<ScrollRect>();
        sr.content = cRT;
        sr.viewport = vpRT;
        sr.vertical = true;
        sr.horizontal = false;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 30f;

        ScrollbarHelper.CreateVerticalScrollbar(sr, scrollArea.transform);

        // Sheet text
        Text sheetText = MakeText(content.transform, "QSSheetText",
            Vector2.zero, new Vector2(previewW - 80, 0),
            sheet, 14, new Color(0.9f, 0.9f, 0.85f), TextAnchor.UpperCenter);
        RectTransform stRT = sheetText.GetComponent<RectTransform>();
        stRT.anchorMin = new Vector2(0, 1);
        stRT.anchorMax = new Vector2(1, 1);
        stRT.pivot = new Vector2(0.5f, 1);
        stRT.anchoredPosition = Vector2.zero;
        stRT.sizeDelta = new Vector2(-20, 0);
        sheetText.verticalOverflow = VerticalWrapMode.Overflow;

        // Resize content based on text
        int lineCount = sheet.Split('\n').Length;
        float estimatedHeight = Mathf.Max(scrollH, lineCount * 18f + 20f);
        cRT.sizeDelta = new Vector2(0, estimatedHeight);

        // Buttons
        float bottomY = -previewH / 2 + 35;

        Button confirmBtn = MakeButton(panel.transform, "QSPreviewConfirm",
            new Vector2(80, bottomY), new Vector2(160, 44),
            "Confirm ✓", new Color(0.15f, 0.55f, 0.15f), Color.white, 18);
        confirmBtn.onClick.AddListener(() =>
        {
            Debug.Log($"[QuickStart] Slot {slotIndex + 1}: Confirmed {data.CharacterName} ({data.ClassName})");
            ShowQuickStartSlotSelection();
        });

        Button cancelBtn = MakeButton(panel.transform, "QSPreviewCancel",
            new Vector2(-80, bottomY), new Vector2(140, 40),
            "Cancel", new Color(0.5f, 0.2f, 0.2f), Color.white, 16);
        cancelBtn.onClick.AddListener(() =>
        {
            _qsSelectedClasses[slotIndex] = null;
            _qsCharacters[slotIndex] = null;
            ShowQuickStartSlotSelection();
        });
    }

    private string BuildQuickStartCharacterSheet(CharacterCreationData data)
    {
        string s = "";
        s += $"══════════ CHARACTER SHEET ══════════\n\n";
        s += $"{data.CharacterName} — {data.RaceName} {data.ClassName}, Level 3\n\n";

        // Ability scores
        s += "--- Ability Scores ---\n";
        s += FormatQSStat("STR", data.STR, data.Race != null ? data.Race.STRModifier : 0, data.FinalSTR) + "\n";
        s += FormatQSStat("DEX", data.DEX, data.Race != null ? data.Race.DEXModifier : 0, data.FinalDEX) + "\n";
        s += FormatQSStat("CON", data.CON, data.Race != null ? data.Race.CONModifier : 0, data.FinalCON) + "\n";
        s += FormatQSStat("INT", data.INT, data.Race != null ? data.Race.INTModifier : 0, data.FinalINT) + "\n";
        s += FormatQSStat("WIS", data.WIS, data.Race != null ? data.Race.WISModifier : 0, data.FinalWIS) + "\n";
        s += FormatQSStat("CHA", data.CHA, data.Race != null ? data.Race.CHAModifier : 0, data.FinalCHA) + "\n\n";

        // Combat stats
        int dexMod = CharacterStats.GetModifier(data.FinalDEX);
        int strMod = CharacterStats.GetModifier(data.FinalSTR);
        int conMod = CharacterStats.GetModifier(data.FinalCON);
        int wisMod = CharacterStats.GetModifier(data.FinalWIS);
        int sizeMod = data.Race != null ? data.Race.SizeACAndAttackModifier : 0;

        // Compute AC based on class
        int armorBonus = 0, shieldBonus = 0;
        ClassRegistry.Init();
        ICharacterClass classDef = ClassRegistry.GetClass(data.ClassName);
        if (classDef != null)
        {
            armorBonus = classDef.DefaultArmorBonus;
            shieldBonus = classDef.DefaultShieldBonus;
        }

        // For Monk, add WIS to AC if positive
        int wisACBonus = 0;
        if (data.ClassName == "Monk" && wisMod > 0)
            wisACBonus = wisMod;

        int effectiveDex = dexMod;
        // Max Dex limit for heavier armor
        if (armorBonus >= 4) effectiveDex = Mathf.Min(dexMod, 3);
        else if (armorBonus >= 3) effectiveDex = Mathf.Min(dexMod, 4);

        int ac = 10 + effectiveDex + armorBonus + shieldBonus + sizeMod + wisACBonus;
        int attackBonus = data.BAB + strMod + sizeMod;

        s += "--- Combat Stats ---\n";
        s += $"HP: {data.HP}   (d{data.HitDie} + CON per level)\n";
        s += $"AC: {ac}   (10 + {effectiveDex} DEX + {armorBonus} armor";
        if (shieldBonus > 0) s += $" + {shieldBonus} shield";
        if (wisACBonus > 0) s += $" + {wisACBonus} WIS";
        if (sizeMod != 0) s += $" + {sizeMod} size";
        s += ")\n";
        s += $"Attack: {CharacterStats.FormatMod(attackBonus)}   (BAB {CharacterStats.FormatMod(data.BAB)} + {CharacterStats.FormatMod(strMod)} STR";
        if (sizeMod != 0) s += $" + {sizeMod} size";
        s += ")\n";
        s += $"Speed: {(data.Race != null ? data.Race.BaseSpeedFeet : 30)} ft ({data.BaseSpeed} squares)\n\n";

        // Saves
        int baseFort = 0, baseRef = 0, baseWill = 0;
        if (classDef != null)
        {
            // Good save = +3 at level 3, Poor save = +1 at level 3
            baseFort = classDef.GoodFortitude ? 3 : 1;
            baseRef = classDef.GoodReflex ? 3 : 1;
            baseWill = classDef.GoodWill ? 3 : 1;
        }
        s += "--- Saving Throws ---\n";
        s += $"Fortitude: {CharacterStats.FormatMod(baseFort + conMod)}   (base {baseFort} + {CharacterStats.FormatMod(conMod)} CON)\n";
        s += $"Reflex: {CharacterStats.FormatMod(baseRef + dexMod)}   (base {baseRef} + {CharacterStats.FormatMod(dexMod)} DEX)\n";
        s += $"Will: {CharacterStats.FormatMod(baseWill + wisMod)}   (base {baseWill} + {CharacterStats.FormatMod(wisMod)} WIS)\n\n";

        // Skills
        if (data.SkillRanks != null && data.SkillRanks.Count > 0)
        {
            s += "--- Skills ---\n";
            foreach (var kvp in data.SkillRanks)
                s += $"  {kvp.Key}: {kvp.Value} ranks\n";
            s += "\n";
        }

        // Feats
        if ((data.SelectedFeats != null && data.SelectedFeats.Count > 0) ||
            (data.BonusFeats != null && data.BonusFeats.Count > 0))
        {
            s += "--- Feats ---\n";
            if (data.SelectedFeats != null)
                foreach (string feat in data.SelectedFeats)
                    s += $"  • {feat}\n";
            if (data.BonusFeats != null && data.BonusFeats.Count > 0)
            {
                string bonusLabel = data.ClassName == "Monk" ? "Monk Bonus" :
                                    data.ClassName == "Fighter" ? "Fighter Bonus" :
                                    data.ClassName == "Wizard" ? "Wizard Bonus" : "Class Bonus";
                s += $"  ({bonusLabel} Feats)\n";
                foreach (string feat in data.BonusFeats)
                    s += $"  • {feat}\n";
            }
            if (!string.IsNullOrEmpty(data.WeaponFocusChoice))
                s += $"  Weapon Focus: {data.WeaponFocusChoice}\n";
            if (!string.IsNullOrEmpty(data.SkillFocusChoice))
                s += $"  Skill Focus: {data.SkillFocusChoice}\n";
            s += "\n";
        }

        // Spells (for casters)
        if (classDef != null && classDef.IsSpellcaster && data.SelectedSpellIds != null && data.SelectedSpellIds.Count > 0)
        {
            SpellDatabase.Init();
            s += "--- Spells ---\n";

            var cantrips = new List<string>();
            var higherSpells = new List<string>();
            foreach (string spellId in data.SelectedSpellIds)
            {
                SpellData spell = SpellDatabase.GetSpell(spellId);
                if (spell != null)
                {
                    string entry = $"  • {spell.Name} (Lvl {spell.SpellLevel}){(spell.IsPlaceholder ? " [P]" : "")}";
                    if (spell.SpellLevel == 0) cantrips.Add(entry);
                    else higherSpells.Add(entry);
                }
            }
            if (cantrips.Count > 0)
            {
                s += "  Cantrips:\n";
                foreach (string c in cantrips) s += c + "\n";
            }
            if (higherSpells.Count > 0)
            {
                s += "  Spellbook:\n";
                foreach (string sp in higherSpells) s += sp + "\n";
            }
            if (data.ClassName == "Cleric")
                s += "  Prepares from full 1st & 2nd level spell list each day.\n";
            s += "\n";
        }

        return s;
    }

    private string FormatQSStat(string label, int baseVal, int raceMod, int finalVal)
    {
        int mod = CharacterStats.GetModifier(finalVal);
        string modStr = mod >= 0 ? $"+{mod}" : $"{mod}";
        string raceStr = raceMod != 0 ? $" ({(raceMod > 0 ? "+" : "")}{raceMod} racial)" : "";
        return $"{label}: {finalVal} ({modStr}){raceStr}";
    }

    // ========== STEP 5: START GAME ==========

    private void OnQSStartGame()
    {
        // Validate all slots are filled
        for (int i = 0; i < _qsSelectedPartySize; i++)
        {
            if (_qsSelectedClasses[i] == null || _qsCharacters[i] == null)
            {
                Debug.LogWarning($"[QuickStart] Slot {i + 1} is empty — cannot start game");
                return;
            }
        }

        Debug.Log($"[QuickStart] Starting game with {_qsSelectedPartySize} characters:");
        CreatedCharacters = new CharacterCreationData[_qsSelectedPartySize];
        for (int i = 0; i < _qsSelectedPartySize; i++)
        {
            CreatedCharacters[i] = _qsCharacters[i];
            Debug.Log($"  Slot {i + 1}: {CreatedCharacters[i].CharacterName} ({CreatedCharacters[i].RaceName} {CreatedCharacters[i].ClassName})");
        }

        IsComplete = true;
        HideQSOverlay();
        HideCreationUI();

        NotifyCreationComplete("QuickStartPartyBuilder");
    }

    private void NotifyCreationComplete(string flowSource)
    {
        int callbackPartySize = CreatedCharacters != null ? CreatedCharacters.Length : 0;
        Debug.Log($"[PlayNow] Character creation complete callback dispatch from '{flowSource}'. partySlots={callbackPartySize}, hasOnCreationComplete4={OnCreationComplete4 != null}, hasLegacyCallback={OnCreationComplete != null}");

        if (OnCreationComplete4 != null)
        {
            OnCreationComplete4.Invoke(CreatedCharacters);
            Debug.Log("[PlayNow] Invoked OnCreationComplete4 callback.");
            return;
        }

        if (OnCreationComplete != null)
        {
            CharacterCreationData first = (CreatedCharacters != null && CreatedCharacters.Length > 0)
                ? CreatedCharacters[0]
                : null;
            CharacterCreationData second = (CreatedCharacters != null && CreatedCharacters.Length > 1)
                ? CreatedCharacters[1]
                : first;

            OnCreationComplete.Invoke(first, second);
            Debug.Log("[PlayNow] Invoked legacy OnCreationComplete callback.");
            return;
        }

        if (GameManager.Instance != null)
        {
            Debug.LogWarning("[PlayNow] No creation callbacks were assigned. Falling back to GameManager.Instance.OnCharacterCreationComplete4().");
            GameManager.Instance.OnCharacterCreationComplete4(CreatedCharacters);
            return;
        }

        Debug.LogError("[PlayNow] Unable to continue game flow: no creation callbacks and GameManager.Instance is null.");
    }

    // ========== SHOW / HIDE ==========

    /// <summary>
    /// Hide the character creation UI and disable raycast blocking.
    /// </summary>
    private void HideCreationUI()
    {
        if (_overlayPanel != null)
            _overlayPanel.SetActive(false);

        if (_premadeOverlayPanel != null)
            _premadeOverlayPanel.SetActive(false);

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
        Debug.Log("[UI] Character creation UI hidden - allowing raycasts");
    }

    // ========== UI HELPER METHODS ==========

    private Text MakeText(Transform parent, string name, Vector2 pos, Vector2 size,
        string text, int fontSize, Color color, TextAnchor align)
    {
        Text t = UIFactory.CreateLabel(parent, text, fontSize + 2, align, color, name, _font);
        RectTransform rt = t.GetComponent<RectTransform>();
        UIFactory.SetRectTransform(rt,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            pos,
            size);
        t.supportRichText = true;
        return t;
    }

    private Button MakeButton(Transform parent, string name, Vector2 pos, Vector2 size,
        string label, Color bgColor, Color textColor, int fontSize)
    {
        Vector2 adjustedSize = new Vector2(size.x * 1.08f, size.y * 1.15f);
        Button btn = UIFactory.CreateButton(parent, label, null, adjustedSize, bgColor, name, _font, fontSize + 2, textColor);

        RectTransform rt = btn.GetComponent<RectTransform>();
        UIFactory.SetRectTransform(rt,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            pos,
            adjustedSize);

        ColorBlock cb = btn.colors;
        cb.normalColor = bgColor;
        cb.highlightedColor = bgColor * 1.3f;
        cb.pressedColor = bgColor * 0.7f;
        cb.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        btn.colors = cb;

        return btn;
    }

    private GameObject CreatePanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 pos, Vector2 size, Color color)
    {
        GameObject go;
        if (color.a > 0)
        {
            go = UIFactory.CreatePanel(parent, name, color);
        }
        else
        {
            go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
        }

        RectTransform rt = go.GetComponent<RectTransform>();
        UIFactory.SetRectTransform(rt, anchorMin, anchorMax, pivot, pos, size);

        return go;
    }
}