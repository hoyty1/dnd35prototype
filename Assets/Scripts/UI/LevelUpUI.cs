using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelUpUI : MonoBehaviour
{
    private GameObject _panel;
    private readonly List<LevelUpData> _levelUpQueue = new List<LevelUpData>();
    private int _currentIndex;
    private LevelUpData _currentLevelUp;

    private Action _onComplete;
    private Transform _contentContainer;
    private CharacterCreationManager _characterCreationManager;

    private enum LevelUpStep
    {
        Summary,
        AbilityIncrease,
        ReuseCharacterCreationFlow
    }

    private LevelUpStep _currentStep;
    private bool _waitingForExternalFlow;

    public void ShowForCharacter(CharacterController character, Action onComplete)
    {
        string characterName = character != null && character.Stats != null && !string.IsNullOrWhiteSpace(character.Stats.CharacterName)
            ? character.Stats.CharacterName
            : "Unknown";
        Debug.Log($"[LevelUpUI] ShowForCharacter called for {characterName}");

        if (character == null || character.Stats == null)
        {
            Debug.LogWarning("[LevelUpUI] Cannot show level-up UI because character/stats is null.");
            onComplete?.Invoke();
            return;
        }

        int newLevel = Mathf.Max(1, character.Stats.Level);
        int oldLevel = Mathf.Max(1, newLevel - 1);
        LevelUpData levelUpData = LevelUpCalculator.CalculateLevelUp(character, oldLevel, newLevel);

        ShowLevelUps(new List<LevelUpData> { levelUpData }, onComplete);
    }

    public void ShowLevelUps(List<LevelUpData> levelUps, Action onCompleteCallback)
    {
        int count = levelUps != null ? levelUps.Count : 0;
        Debug.Log($"[LevelUpUI] Showing level-ups for {count} characters");

        _levelUpQueue.Clear();
        if (levelUps != null)
            _levelUpQueue.AddRange(levelUps);

        _currentIndex = 0;
        _onComplete = onCompleteCallback;

        if (_levelUpQueue.Count == 0)
        {
            Debug.Log("[LevelUpUI] No level-ups to show");
            _onComplete?.Invoke();
            return;
        }

        if (_panel == null)
            BuildUI();

        _panel.SetActive(true);
        ShowNextCharacter();
    }

    private void ShowNextCharacter()
    {
        if (_currentIndex >= _levelUpQueue.Count)
        {
            Debug.Log("[LevelUpUI] All level-ups complete");
            if (_panel != null)
                _panel.SetActive(false);
            _onComplete?.Invoke();
            return;
        }

        _currentLevelUp = _levelUpQueue[_currentIndex];
        string name = GetCharacterName(_currentLevelUp.Character);
        Debug.Log($"[LevelUpUI] Showing level-up for {name}");

        _waitingForExternalFlow = false;
        _currentStep = LevelUpStep.Summary;
        ShowCurrentStep();
    }

    private void ShowCurrentStep()
    {
        switch (_currentStep)
        {
            case LevelUpStep.Summary:
                ShowSummary();
                break;

            case LevelUpStep.AbilityIncrease:
                if (_currentLevelUp.NeedsAbilityIncrease)
                    ShowAbilityIncrease();
                else
                    NextStep();
                break;

            case LevelUpStep.ReuseCharacterCreationFlow:
                BeginReusableSelectionFlow();
                break;
        }
    }

    private void NextStep()
    {
        _currentStep++;

        if (_currentStep > LevelUpStep.ReuseCharacterCreationFlow)
        {
            CompleteCurrentCharacter();
            return;
        }

        ShowCurrentStep();
    }

    private void CompleteCurrentCharacter()
    {
        string name = GetCharacterName(_currentLevelUp.Character);
        Debug.Log($"[LevelUpUI] Level-up complete for {name}");

        _currentIndex++;
        ShowNextCharacter();
    }

    private void ShowSummary()
    {
        ClearContent();

        CharacterStats stats = _currentLevelUp.Character != null ? _currentLevelUp.Character.Stats : null;
        string characterName = GetCharacterName(_currentLevelUp.Character);

        CreateTitle($"{characterName} - LEVEL {_currentLevelUp.NewLevel}!");

        CreateInfoText($"Previous Level: {_currentLevelUp.OldLevel}");
        CreateInfoText($"New Level: {_currentLevelUp.NewLevel}", true, Color.yellow);

        CreateSeparator();

        CreateInfoText("GAINS:", true);
        CreateInfoText($"• Hit Points: +{Mathf.Max(1, _currentLevelUp.HPGained)}");

        if (_currentLevelUp.NewBAB > _currentLevelUp.OldBAB)
            CreateInfoText($"• Base Attack Bonus: {_currentLevelUp.OldBAB} → {_currentLevelUp.NewBAB}");

        if (_currentLevelUp.NewFortSave > _currentLevelUp.OldFortSave)
            CreateInfoText($"• Fortitude Save: {_currentLevelUp.OldFortSave} → {_currentLevelUp.NewFortSave}");

        if (_currentLevelUp.NewRefSave > _currentLevelUp.OldRefSave)
            CreateInfoText($"• Reflex Save: {_currentLevelUp.OldRefSave} → {_currentLevelUp.NewRefSave}");

        if (_currentLevelUp.NewWillSave > _currentLevelUp.OldWillSave)
            CreateInfoText($"• Will Save: {_currentLevelUp.OldWillSave} → {_currentLevelUp.NewWillSave}");

        if (_currentLevelUp.NeedsAbilityIncrease)
            CreateInfoText("• Ability Score: +1 to one ability", true, Color.green);

        if (_currentLevelUp.NeedsFeat)
            CreateInfoText("• Feat: Choose 1 new feat", true, Color.green);

        if (_currentLevelUp.SkillPointsToAllocate > 0)
            CreateInfoText($"• Skill Points: {_currentLevelUp.SkillPointsToAllocate} points", true, Color.green);

        if (_currentLevelUp.NeedsSpellSelection)
        {
            string className = stats != null ? stats.CharacterClass : string.Empty;
            CreateInfoText($"• Spells: {GetSpellSummaryText(className)}", true, Color.green);
        }

        CreateButton("Continue", NextStep);
    }

    private void ShowAbilityIncrease()
    {
        ClearContent();

        CreateTitle("Choose Ability Score Increase");
        CreateInfoText("Select one ability score to increase by +1:");

        CreateSeparator();

        CharacterStats stats = _currentLevelUp.Character != null ? _currentLevelUp.Character.Stats : null;
        if (stats == null)
        {
            CreateInfoText("Character stats unavailable; skipping.", true, Color.yellow);
            CreateButton("Continue", NextStep);
            return;
        }

        CreateAbilityButton("Strength", "STR", stats.STR);
        CreateAbilityButton("Dexterity", "DEX", stats.DEX);
        CreateAbilityButton("Constitution", "CON", stats.CON);
        CreateAbilityButton("Intelligence", "INT", stats.INT);
        CreateAbilityButton("Wisdom", "WIS", stats.WIS);
        CreateAbilityButton("Charisma", "CHA", stats.CHA);
    }

    private void CreateAbilityButton(string abilityName, string code, int currentValue)
    {
        int oldModifier = GetModifier(currentValue);
        int newModifier = GetModifier(currentValue + 1);
        string oldModText = oldModifier >= 0 ? $"+{oldModifier}" : oldModifier.ToString();
        string newModText = newModifier >= 0 ? $"+{newModifier}" : newModifier.ToString();

        string label = $"{abilityName}: {currentValue} ({oldModText}) → {currentValue + 1} ({newModText})";

        CreateButton(label, () =>
        {
            ApplyAbilityIncrease(code);
            NextStep();
        });
    }

    private void ApplyAbilityIncrease(string abilityCode)
    {
        CharacterStats stats = _currentLevelUp.Character != null ? _currentLevelUp.Character.Stats : null;
        if (stats == null)
            return;

        _currentLevelUp.SelectedAbility = abilityCode;

        switch (abilityCode)
        {
            case "STR": stats.BaseSTR++; stats.STR++; break;
            case "DEX": stats.BaseDEX++; stats.DEX++; break;
            case "CON": stats.BaseCON++; stats.CON++; break;
            case "INT": stats.BaseINT++; stats.INT++; break;
            case "WIS": stats.BaseWIS++; stats.WIS++; break;
            case "CHA": stats.BaseCHA++; stats.CHA++; break;
        }

        Debug.Log($"[LevelUpUI] Applied +1 to {abilityCode}");
    }

    private static int GetModifier(int score)
    {
        return Mathf.FloorToInt((score - 10) / 2f);
    }

    private void BeginReusableSelectionFlow()
    {
        if (_waitingForExternalFlow)
            return;

        _waitingForExternalFlow = true;

        ClearContent();
        CreateTitle("Level-Up Choices");
        CreateInfoText("Opening existing character creation selection panels...", true, Color.cyan);

        CharacterController character = _currentLevelUp != null ? _currentLevelUp.Character : null;
        if (character == null || character.Stats == null)
        {
            Debug.LogWarning("[LevelUpUI] Missing character data for reusable level-up flow.");
            _waitingForExternalFlow = false;
            NextStep();
            return;
        }

        CharacterCreationManager manager = GetOrCreateCharacterCreationManager();
        if (manager == null)
        {
            Debug.LogWarning("[LevelUpUI] CharacterCreationManager unavailable. Finishing level-up without reusable panels.");
            _waitingForExternalFlow = false;
            NextStep();
            return;
        }

        _panel.SetActive(false);

        manager.StartLevelUpFlow(character, _currentLevelUp, () =>
        {
            if (_panel != null)
                _panel.SetActive(true);

            _waitingForExternalFlow = false;
            NextStep();
        });
    }

    private CharacterCreationManager GetOrCreateCharacterCreationManager()
    {
        if (_characterCreationManager != null)
            return _characterCreationManager;

        _characterCreationManager = FindObjectOfType<CharacterCreationManager>();
        if (_characterCreationManager != null)
            return _characterCreationManager;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
            return null;

        GameObject managerObj = new GameObject("CharacterCreationManager", typeof(RectTransform));
        managerObj.transform.SetParent(canvas.transform, false);
        _characterCreationManager = managerObj.AddComponent<CharacterCreationManager>();
        return _characterCreationManager;
    }

    private static string GetSpellSummaryText(string className)
    {
        if (className == "Wizard")
            return "Learn wizard spell(s)";
        if (className == "Sorcerer")
            return "Learn sorcerer spell(s)";
        if (className == "Cleric" || className == "Druid")
            return "Access higher-level divine spells";
        return "Review spell progression";
    }

    private string GetCharacterName(CharacterController character)
    {
        if (character == null || character.Stats == null || string.IsNullOrWhiteSpace(character.Stats.CharacterName))
            return "Unknown";
        return character.Stats.CharacterName;
    }

    private void BuildUI()
    {
        _panel = new GameObject("LevelUpPanel");
        _panel.transform.SetParent(transform, false);

        RectTransform panelRect = _panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.12f, 0.08f);
        panelRect.anchorMax = new Vector2(0.88f, 0.92f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelBg = _panel.AddComponent<Image>();
        panelBg.color = new Color(0.07f, 0.09f, 0.15f, 0.95f);

        GameObject viewportObj = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportObj.transform.SetParent(_panel.transform, false);

        RectTransform viewportRect = viewportObj.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.04f, 0.04f);
        viewportRect.anchorMax = new Vector2(0.96f, 0.96f);
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        Image viewportImage = viewportObj.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.15f);

        Mask viewportMask = viewportObj.GetComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        GameObject scrollObj = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
        scrollObj.transform.SetParent(_panel.transform, false);

        RectTransform scrollRect = scrollObj.GetComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0.04f, 0.04f);
        scrollRect.anchorMax = new Vector2(0.96f, 0.96f);
        scrollRect.offsetMin = Vector2.zero;
        scrollRect.offsetMax = Vector2.zero;

        ScrollRect scroll = scrollObj.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.viewport = viewportRect;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewportObj.transform, false);

        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);

        VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 10f;
        contentLayout.padding = new RectOffset(20, 20, 20, 20);
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandWidth = true;

        ContentSizeFitter contentFitter = content.GetComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentRect;
        _contentContainer = content.transform;

        _panel.SetActive(false);
    }

    private void ClearContent()
    {
        if (_contentContainer == null)
            return;

        for (int i = _contentContainer.childCount - 1; i >= 0; i--)
            Destroy(_contentContainer.GetChild(i).gameObject);
    }

    private void CreateTitle(string text)
    {
        GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(_contentContainer, false);

        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(0f, 54f);

        TextMeshProUGUI titleText = titleObj.GetComponent<TextMeshProUGUI>();
        titleText.text = text;
        titleText.fontSize = 28;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(0.9f, 0.8f, 0.5f);
        titleText.enableWordWrapping = true;
    }

    private void CreateInfoText(string text, bool bold = false, Color? color = null)
    {
        GameObject textObj = new GameObject("Info", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(_contentContainer, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(0f, 32f);

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 16;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.color = color ?? Color.white;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
    }

    private void CreateSeparator()
    {
        GameObject sepObj = new GameObject("Separator", typeof(RectTransform));
        sepObj.transform.SetParent(_contentContainer, false);

        RectTransform sepRect = sepObj.GetComponent<RectTransform>();
        sepRect.sizeDelta = new Vector2(0f, 14f);
    }

    private void CreateButton(string label, Action onClick, float height = 42f)
    {
        GameObject btnObj = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        btnObj.transform.SetParent(_contentContainer, false);

        LayoutElement layout = btnObj.GetComponent<LayoutElement>();
        layout.preferredHeight = height;

        Image btnBg = btnObj.GetComponent<Image>();
        btnBg.color = new Color(0.16f, 0.37f, 0.71f, 1f);

        Button btn = btnObj.GetComponent<Button>();
        btn.onClick.AddListener(() => onClick());

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 5f);
        textRect.offsetMax = new Vector2(-10f, -5f);

        TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 15;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.enableWordWrapping = true;
    }
}
