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
    public enum Step { RollStats, AssignStats, ChooseRace, ChooseClass, AllocateSkills, SelectFeats, SelectSpells, Review }

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

    // Step 5: Review
    private Text _reviewText;
    private RectTransform _reviewScrollContentRT; // Content RT for scroll area sizing
    private InputField _nameInput;
    private Button _createButton;

    // UI Layout constants
    private Font _font;
    private const float PANEL_W = 900f;
    private const float PANEL_H = 680f;

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
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(PANEL_W, PANEL_H), new Color(0.12f, 0.12f, 0.18f, 0.98f));

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

        BuildStepRollStats();
        BuildStepAssignStats();
        BuildStepChooseRace();
        BuildStepChooseClass();
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
            str: data.STR, dex: data.DEX, con: data.CON,
            wis: data.WIS, intelligence: data.INT, cha: data.CHA,
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
            Debug.Log($"[CharCreation] Wizard spell selection: INT mod = {intMod}");

            SpellUI.OnSpellsConfirmed = (selectedSpellIds) =>
            {
                data.SelectedSpellIds = new List<string>(selectedSpellIds);
                Debug.Log($"[CharCreation] Wizard spells selected: {selectedSpellIds.Count} spells");
                ShowStep(Step.Review);
            };
            SpellUI.OpenForWizard(intMod, 3);
        }
        else if (data.ClassName == "Cleric")
        {
            // Clerics select 4 orisons; higher-level spells are all available
            SpellUI.OnSpellsConfirmed = (selectedSpellIds) =>
            {
                data.SelectedSpellIds = new List<string>(selectedSpellIds);
                Debug.Log($"[CharCreation] Cleric orisons selected: {selectedSpellIds.Count} spells");
                ShowStep(Step.Review);
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

        string review = $"══════════ CHARACTER SHEET ══════════\n\n";
        review += $"Race: {data.RaceName}   Class: {data.ClassName}   Level: 3{sizeStr}\n\n";

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

            // Fire both callbacks for compatibility
            if (OnCreationComplete4 != null)
            {
                OnCreationComplete4.Invoke(CreatedCharacters);
            }
            else if (OnCreationComplete != null)
            {
                // Legacy: only send first 2
                OnCreationComplete.Invoke(CreatedCharacters[0], CreatedCharacters[1]);
            }
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
        _step5Panel.SetActive(false);

        string heroLabel = $"Hero {CurrentCharacterIndex + 1} of {TotalPCs}";
        _titleText.text = $"CHARACTER CREATION - {heroLabel}";
        _backButton.gameObject.SetActive(step != Step.RollStats);

        switch (step)
        {
            case Step.RollStats:
                _step1Panel.SetActive(true);
                _stepText.text = "Step 1 of 8: Roll Stats";
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
                _stepText.text = "Step 2 of 8: Assign Stats";
                // Reset assignment
                _assignedValues = new int[] { -1, -1, -1, -1, -1, -1 };
                _rollUsed = new bool[6];
                _selectedRollIndex = -1;
                RefreshAssignUI();
                break;

            case Step.ChooseRace:
                _step3Panel.SetActive(true);
                _stepText.text = "Step 3 of 8: Choose Race";
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
                _stepText.text = "Step 4 of 8: Choose Class";
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

            case Step.AllocateSkills:
                // Skills allocation is handled by the SkillsUIPanel overlay
                _stepText.text = "Step 5 of 8: Allocate Skills";
                StartSkillAllocation();
                break;

            case Step.SelectFeats:
                _stepText.text = "Step 6 of 8: Select Feats";
                StartFeatSelection();
                break;

            case Step.SelectSpells:
                _stepText.text = "Step 7 of 8: Select Spells (incl. Cantrips)";
                StartSpellSelection();
                break;

            case Step.Review:
                _step5Panel.SetActive(true);
                _stepText.text = "Step 8 of 8: Review & Name";
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
            case Step.AllocateSkills:
                // Close skills UI if open
                if (SkillsUI != null && SkillsUI.IsOpen)
                    SkillsUI.Close();
                ShowStep(Step.ChooseClass);
                break;
            case Step.SelectFeats:
                // Close feat UI if open
                if (FeatUI != null && FeatUI.IsOpen)
                    FeatUI.Close();
                ShowStep(Step.AllocateSkills);
                break;
            case Step.SelectSpells:
                // Close spell UI if open
                if (SpellUI != null && SpellUI.IsOpen)
                    SpellUI.Close();
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
        _selectedRollIndex = -1;
        _assignedValues = new int[] { -1, -1, -1, -1, -1, -1 };
        _rollUsed = new bool[6];

        _rollButton.gameObject.SetActive(true);
        _rerollButton.gameObject.SetActive(false);
        _acceptStatsButton.gameObject.SetActive(false);
        _rollDetailsText.text = "(Click 'Roll Stats' to begin)";
        _rollResultsText.text = "";
    }

    // ========== QUICK START ==========

    private void OnQuickStart()
    {
        Debug.Log("[CharCreation] Quick Start - creating 4 PCs: Lyra (Rogue), Aldric (Fighter), Theron (Cleric), Elara (Wizard)");

        FeatDefinitions.Init();

        // PC1: Lyra the Elf Rogue
        var pc1 = CreatedCharacters[0];
        pc1.CharacterName = "Lyra";
        pc1.RaceName = "Elf";
        pc1.Race = RaceDatabase.GetRace("Elf");
        pc1.ClassName = "Rogue";
        pc1.STR = 12; pc1.DEX = 17; pc1.CON = 12;
        pc1.INT = 14; pc1.WIS = 13; pc1.CHA = 10;
        pc1.ComputeFinalStats();
        pc1.SkillRanks["Hide"] = 6;
        pc1.SkillRanks["Move Silently"] = 6;
        pc1.SkillRanks["Spot"] = 6;
        pc1.SkillRanks["Listen"] = 6;
        pc1.SkillRanks["Disable Device"] = 5;
        pc1.SkillRanks["Open Lock"] = 5;
        pc1.SkillRanks["Search"] = 5;
        pc1.SkillRanks["Tumble"] = 4;
        pc1.SkillRanks["Bluff"] = 4;
        pc1.SkillRanks["Diplomacy"] = 4;
        pc1.SkillRanks["Climb"] = 4;
        pc1.SkillRanks["Balance"] = 3;
        pc1.SkillRanks["Sleight of Hand"] = 2;
        pc1.SelectedFeats = new System.Collections.Generic.List<string> { "Weapon Finesse", "Dodge" };

        // PC2: Aldric the Dwarf Fighter
        var pc2 = CreatedCharacters[1];
        pc2.CharacterName = "Aldric";
        pc2.RaceName = "Dwarf";
        pc2.Race = RaceDatabase.GetRace("Dwarf");
        pc2.ClassName = "Fighter";
        pc2.STR = 16; pc2.DEX = 12; pc2.CON = 14;
        pc2.INT = 10; pc2.WIS = 10; pc2.CHA = 13;
        pc2.ComputeFinalStats();
        pc2.SkillRanks["Climb"] = 4;
        pc2.SkillRanks["Intimidate"] = 4;
        pc2.SkillRanks["Jump"] = 3;
        pc2.SkillRanks["Swim"] = 3;
        pc2.SelectedFeats = new System.Collections.Generic.List<string> { "Power Attack", "Cleave" };
        pc2.BonusFeats = new System.Collections.Generic.List<string> { "Weapon Focus" };
        pc2.WeaponFocusChoice = "Longsword";

        // PC3: Theron the Human Cleric
        var pc3 = CreatedCharacters[2];
        pc3.CharacterName = "Theron";
        pc3.RaceName = "Human";
        pc3.Race = RaceDatabase.GetRace("Human");
        pc3.ClassName = "Cleric";
        pc3.STR = 14; pc3.DEX = 10; pc3.CON = 14;
        pc3.INT = 10; pc3.WIS = 16; pc3.CHA = 12;
        pc3.ComputeFinalStats();
        pc3.SkillRanks["Concentration"] = 6;
        pc3.SkillRanks["Heal"] = 6;
        pc3.SkillRanks["Diplomacy"] = 4;
        pc3.SkillRanks["Knowledge (Religion)"] = 4;
        pc3.SelectedFeats = new System.Collections.Generic.List<string> { "Combat Casting", "Weapon Focus" };
        pc3.WeaponFocusChoice = "Mace";
        // Cleric selects 4 orisons (D&D 3.5e PHB)
        pc3.SelectedSpellIds = new System.Collections.Generic.List<string>
        {
            "cure_minor_wounds", "detect_magic_clr", "guidance", "light_clr"
        };

        // PC4: Elara the Elf Wizard
        var pc4 = CreatedCharacters[3];
        pc4.CharacterName = "Elara";
        pc4.RaceName = "Elf";
        pc4.Race = RaceDatabase.GetRace("Elf");
        pc4.ClassName = "Wizard";
        pc4.STR = 8; pc4.DEX = 14; pc4.CON = 12;
        pc4.INT = 17; pc4.WIS = 13; pc4.CHA = 10;
        pc4.ComputeFinalStats();
        pc4.SkillRanks["Concentration"] = 6;
        pc4.SkillRanks["Spellcraft"] = 6;
        pc4.SkillRanks["Knowledge (Arcana)"] = 6;
        pc4.SelectedFeats = new System.Collections.Generic.List<string> { "Spell Focus", "Improved Initiative" };
        pc4.BonusFeats = new System.Collections.Generic.List<string> { "Scribe Scroll" };
        // Wizard selects 4 cantrips + higher-level spells (D&D 3.5e PHB)
        pc4.SelectedSpellIds = new System.Collections.Generic.List<string>
        {
            // 4 cantrips
            "ray_of_frost", "detect_magic_wiz", "read_magic", "prestidigitation",
            // 1st and 2nd level spells
            "magic_missile", "mage_armor", "shield",
            "burning_hands", "sleep", "charm_person",
            "scorching_ray", "bulls_strength"
        };

        IsComplete = true;
        HideCreationUI();

        if (OnCreationComplete4 != null)
        {
            OnCreationComplete4.Invoke(CreatedCharacters);
        }
        else if (OnCreationComplete != null)
        {
            OnCreationComplete.Invoke(CreatedCharacters[0], CreatedCharacters[1]);
        }
    }

    // ========== SHOW / HIDE ==========

    /// <summary>
    /// Hide the character creation UI and disable raycast blocking.
    /// </summary>
    private void HideCreationUI()
    {
        if (_overlayPanel != null)
            _overlayPanel.SetActive(false);

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

        // Label text
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
}
