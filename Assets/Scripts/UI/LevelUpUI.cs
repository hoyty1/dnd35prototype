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
    private string _selectedClassForLevelUp;

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

        int pending = Mathf.Max(0, character.Stats.PendingLevelUps);
        int appliedLevel = 0;
        if (character.Stats.ClassLevels != null)
        {
            for (int i = 0; i < character.Stats.ClassLevels.Count; i++)
            {
                ClassLevelEntry entry = character.Stats.ClassLevels[i];
                if (entry != null)
                    appliedLevel += Mathf.Max(0, entry.Level);
            }
        }

        if (appliedLevel <= 0)
            appliedLevel = Mathf.Max(1, character.Stats.Level - pending);

        int oldLevel = Mathf.Max(1, appliedLevel);
        int newLevel = pending > 0 ? oldLevel + 1 : Mathf.Max(2, character.Stats.Level);
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
        _selectedClassForLevelUp = _currentLevelUp != null ? _currentLevelUp.SelectedClassName : null;
        EnsureAvailableClassesForCurrentLevelUp();
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
        CharacterStats stats = _currentLevelUp != null && _currentLevelUp.Character != null ? _currentLevelUp.Character.Stats : null;

        if (stats != null && stats.PendingLevelUps > 0)
        {
            int appliedLevel = 0;
            if (stats.ClassLevels != null)
            {
                for (int i = 0; i < stats.ClassLevels.Count; i++)
                {
                    ClassLevelEntry entry = stats.ClassLevels[i];
                    if (entry != null)
                        appliedLevel += Mathf.Max(0, entry.Level);
                }
            }

            if (appliedLevel <= 0)
                appliedLevel = Mathf.Max(1, stats.Level - stats.PendingLevelUps);

            int oldLevel = Mathf.Max(1, appliedLevel);
            int newLevel = oldLevel + 1;
            _currentLevelUp = LevelUpCalculator.CalculateLevelUp(_currentLevelUp.Character, oldLevel, newLevel);
            _selectedClassForLevelUp = _currentLevelUp.SelectedClassName;
            EnsureAvailableClassesForCurrentLevelUp();
            Debug.Log($"[LevelUpUI] {name} still has pending level-ups ({stats.PendingLevelUps}). Continuing level-up flow.");
            _currentStep = LevelUpStep.Summary;
            ShowCurrentStep();
            return;
        }

        Debug.Log($"[LevelUpUI] Level-up complete for {name}");
        _currentIndex++;
        ShowNextCharacter();
    }

    private void ShowSummary()
    {
        ClearContent();

        CharacterStats stats = _currentLevelUp.Character != null ? _currentLevelUp.Character.Stats : null;
        string characterName = GetCharacterName(_currentLevelUp.Character);

        EnsureAvailableClassesForCurrentLevelUp();

        if (string.IsNullOrWhiteSpace(_selectedClassForLevelUp))
            _selectedClassForLevelUp = _currentLevelUp.SelectedClassName;

        if (string.IsNullOrWhiteSpace(_selectedClassForLevelUp)
            && _currentLevelUp.AvailableClasses != null
            && _currentLevelUp.AvailableClasses.Count > 0)
        {
            _selectedClassForLevelUp = _currentLevelUp.AvailableClasses[0];
        }

        if (!string.IsNullOrWhiteSpace(_selectedClassForLevelUp))
            LevelUpCalculator.RecalculateForSelectedClass(_currentLevelUp, _selectedClassForLevelUp);

        CreateTitle($"{characterName} - LEVEL {_currentLevelUp.NewLevel}!");
        CreateInfoText("You gained a level! Choose which class to advance.", true, new Color(0.85f, 0.95f, 1f));

        CreateInfoText($"Previous Level: {_currentLevelUp.OldLevel}");
        CreateInfoText($"New Level: {_currentLevelUp.NewLevel}", true, Color.yellow);
        if (stats != null)
            CreateInfoText($"Classes: {stats.ClassSummary}", true, new Color(0.75f, 0.9f, 1f));

        if (stats != null)
        {
            string favored = string.IsNullOrWhiteSpace(stats.FavoredClass) ? "None" : stats.FavoredClass;
            string penalty = stats.HasXPPenalty ? "-20% XP penalty ACTIVE" : "No XP penalty";
            Color penaltyColor = stats.HasXPPenalty ? new Color(1f, 0.6f, 0.35f) : new Color(0.6f, 1f, 0.6f);
            CreateInfoText($"Favored Class: {favored} | {penalty}", true, penaltyColor);
        }

        CreateSeparator();
        CreateInfoText("Choose class to advance:", true, Color.cyan);
        CreateClassSelectionList(_currentLevelUp.AvailableClasses, _selectedClassForLevelUp);
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
        {
            if (_currentLevelUp.SkillPointsFromClassPool > 0)
            {
                CreateInfoText(
                    $"• Skill Points: {_currentLevelUp.SkillPointsNew} (new) + {_currentLevelUp.SkillPointsFromClassPool} ({_selectedClassForLevelUp} pool) = {_currentLevelUp.SkillPointsToAllocate} available",
                    true,
                    Color.green);
            }
            else
            {
                CreateInfoText($"• Skill Points: {_currentLevelUp.SkillPointsNew} available", true, Color.green);
            }
        }

        if (_currentLevelUp.NeedsSpellSelection)
            CreateInfoText($"• Spells: {GetSpellSummaryText(_selectedClassForLevelUp)}", true, Color.green);

        CreateButton("Continue", () =>
        {
            ApplySelectedClassLevelUp();
            NextStep();
        });
    }


    private void ApplySelectedClassLevelUp()
    {
        CharacterStats stats = _currentLevelUp != null && _currentLevelUp.Character != null
            ? _currentLevelUp.Character.Stats
            : null;

        if (stats == null)
            return;

        if (stats.PendingLevelUps > 0)
        {
            string chosenClass = string.IsNullOrWhiteSpace(_selectedClassForLevelUp) ? stats.CharacterClass : _selectedClassForLevelUp;
            stats.ApplyPendingLevelUp(chosenClass);
            _currentLevelUp.SelectedClassName = chosenClass;
            LevelUpCalculator.RecalculateForSelectedClass(_currentLevelUp, chosenClass);
        }
    }

    private void CreateClassSelectionList(List<string> availableClasses, string selectedClassName)
    {
        if (_contentContainer == null)
            return;

        GameObject listRoot = new GameObject("ClassSelectionList", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        listRoot.transform.SetParent(_contentContainer, false);

        RectTransform listRootRect = listRoot.GetComponent<RectTransform>();
        listRootRect.anchorMin = new Vector2(0f, 0.5f);
        listRootRect.anchorMax = new Vector2(1f, 0.5f);
        listRootRect.pivot = new Vector2(0.5f, 0.5f);

        LayoutElement listLayout = listRoot.GetComponent<LayoutElement>();
        listLayout.preferredHeight = 300f;
        listLayout.minHeight = 300f;
        listLayout.flexibleWidth = 1f;

        Image listBg = listRoot.GetComponent<Image>();
        listBg.color = new Color(0.05f, 0.08f, 0.14f, 0.92f);

        GameObject viewportObj = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportObj.transform.SetParent(listRoot.transform, false);

        RectTransform viewportRect = viewportObj.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0f, 0f);
        viewportRect.anchorMax = new Vector2(1f, 1f);
        viewportRect.offsetMin = new Vector2(12f, 10f);
        viewportRect.offsetMax = new Vector2(-34f, -10f);

        Image viewportImage = viewportObj.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.16f);

        Mask viewportMask = viewportObj.GetComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        GameObject scrollbarObj = CreateScrollbar(listRoot.transform);
        Scrollbar scrollbar = scrollbarObj.GetComponent<Scrollbar>();

        ScrollRect scrollRect = listRoot.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.viewport = viewportRect;
        scrollRect.scrollSensitivity = 40f;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        GameObject contentObj = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObj.transform.SetParent(viewportObj.transform, false);

        RectTransform contentRect = contentObj.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup contentLayout = contentObj.GetComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(6, 6, 6, 6);
        contentLayout.spacing = 8f;
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObj.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRect;

        if (availableClasses == null)
            return;

        for (int i = 0; i < availableClasses.Count; i++)
        {
            string className = availableClasses[i];
            string capturedClass = className;
            string label = capturedClass == selectedClassName ? $"★ {capturedClass}" : capturedClass;
            CreateButton(label, () =>
            {
                _selectedClassForLevelUp = capturedClass;
                ShowSummary();
            }, 92f, contentObj.transform);
        }

        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 1f;
    }

    private static GameObject CreateScrollbar(Transform parent)
    {
        GameObject scrollbarObj = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        scrollbarObj.transform.SetParent(parent, false);

        RectTransform scrollbarRect = scrollbarObj.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 1f);
        scrollbarRect.offsetMin = new Vector2(-20f, 10f);
        scrollbarRect.offsetMax = new Vector2(-8f, -10f);

        Image scrollbarTrack = scrollbarObj.GetComponent<Image>();
        scrollbarTrack.color = new Color(0.15f, 0.2f, 0.3f, 0.95f);

        GameObject slidingArea = new GameObject("Sliding Area", typeof(RectTransform));
        slidingArea.transform.SetParent(scrollbarObj.transform, false);

        RectTransform slidingAreaRect = slidingArea.GetComponent<RectTransform>();
        slidingAreaRect.anchorMin = Vector2.zero;
        slidingAreaRect.anchorMax = Vector2.one;
        slidingAreaRect.offsetMin = new Vector2(2f, 2f);
        slidingAreaRect.offsetMax = new Vector2(-2f, -2f);

        GameObject handleObj = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObj.transform.SetParent(slidingArea.transform, false);

        RectTransform handleRect = handleObj.GetComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = Vector2.zero;
        handleRect.offsetMax = Vector2.zero;

        Image handleImage = handleObj.GetComponent<Image>();
        handleImage.color = new Color(0.36f, 0.56f, 0.92f, 1f);

        Scrollbar scrollbar = scrollbarObj.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = handleImage;
        scrollbar.handleRect = handleRect;
        scrollbar.size = 0.2f;

        return scrollbarObj;
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

    private void EnsureAvailableClassesForCurrentLevelUp()
    {
        if (_currentLevelUp == null || _currentLevelUp.Character == null || _currentLevelUp.Character.Stats == null)
            return;

        CharacterStats stats = _currentLevelUp.Character.Stats;
        ClassRegistry.Init();

        if (_currentLevelUp.AvailableClasses == null)
            _currentLevelUp.AvailableClasses = new List<string>();

        HashSet<string> existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _currentLevelUp.AvailableClasses.Count; i++)
        {
            string className = _currentLevelUp.AvailableClasses[i];
            if (!string.IsNullOrWhiteSpace(className))
                existing.Add(className);
        }

        string[] classNames = ClassRegistry.ClassNames;
        if (classNames != null)
        {
            for (int i = 0; i < classNames.Length; i++)
            {
                string className = classNames[i];
                if (string.IsNullOrWhiteSpace(className) || existing.Contains(className))
                    continue;

                _currentLevelUp.AvailableClasses.Add(className);
                existing.Add(className);
            }
        }

        string fallbackClass = !string.IsNullOrWhiteSpace(stats.CharacterClass)
            ? stats.CharacterClass
            : (stats.ClassLevels != null && stats.ClassLevels.Count > 0 ? stats.ClassLevels[0].ClassName : null);

        if (!string.IsNullOrWhiteSpace(fallbackClass) && !existing.Contains(fallbackClass))
            _currentLevelUp.AvailableClasses.Add(fallbackClass);

        if (string.IsNullOrWhiteSpace(_currentLevelUp.SelectedClassName))
            _currentLevelUp.SelectedClassName = fallbackClass;
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
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelBg = _panel.AddComponent<Image>();
        panelBg.color = new Color(0.12f, 0.09f, 0.18f, 0.95f);

        Debug.Log("[LevelUp] Panel: (0,0) to (1,1) - FULLSCREEN");

        GameObject viewportObj = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportObj.transform.SetParent(_panel.transform, false);

        RectTransform viewportRect = viewportObj.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.06f, 0.06f);
        viewportRect.anchorMax = new Vector2(0.94f, 0.94f);
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
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 14f;
        contentLayout.padding = new RectOffset(34, 34, 28, 28);
        contentLayout.childAlignment = TextAnchor.UpperCenter;
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
        CreateText(_contentContainer, "Title", text, 28, FontStyles.Bold, new Color(0.9f, 0.8f, 0.5f), TextAlignmentOptions.Center, 54f);
    }

    private void CreateInfoText(string text, bool bold = false, Color? color = null)
    {
        FontStyles style = bold ? FontStyles.Bold : FontStyles.Normal;
        CreateText(_contentContainer, "Info", text, 16, style, color ?? Color.white, TextAlignmentOptions.Center, 36f);
    }

    private void CreateText(
        Transform parent,
        string objectName,
        string text,
        int fontSize,
        FontStyles style,
        Color color,
        TextAlignmentOptions alignment,
        float preferredHeight)
    {
        GameObject textObj = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        textObj.transform.SetParent(parent, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0.5f);
        textRect.anchorMax = new Vector2(1f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(0f, preferredHeight);

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        EnsureTMPFontAsset(tmp);
        tmp.text = text;
        tmp.fontSize = fontSize + 2;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.margin = new Vector4(12f, 4f, 12f, 4f);

        LayoutElement layout = textObj.GetComponent<LayoutElement>();
        layout.preferredHeight = preferredHeight;
        layout.minHeight = preferredHeight;
        layout.flexibleWidth = 1f;

        Debug.Log($"[LevelUpUI] Created text: '{text}' with alignment {tmp.alignment}");
    }

    private void CreateSeparator()
    {
        GameObject sepObj = new GameObject("Separator", typeof(RectTransform));
        sepObj.transform.SetParent(_contentContainer, false);

        RectTransform sepRect = sepObj.GetComponent<RectTransform>();
        sepRect.sizeDelta = new Vector2(0f, 14f);
    }

    private void CreateButton(string label, Action onClick, float height = 40f, Transform parentOverride = null)
    {
        Transform parent = parentOverride != null ? parentOverride : _contentContainer;
        if (parent == null)
            return;

        GameObject btnObj = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        btnObj.transform.SetParent(parent, false);

        LayoutElement layout = btnObj.GetComponent<LayoutElement>();
        layout.preferredHeight = height;
        layout.minHeight = height;
        layout.flexibleHeight = 0f;

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
        EnsureTMPFontAsset(text);
        text.text = label;
        text.fontSize = 16;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.enableWordWrapping = true;
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
}
