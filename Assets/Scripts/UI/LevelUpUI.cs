using System;
using System.Collections.Generic;
using System.Linq;
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

    private enum LevelUpStep
    {
        Summary,
        AbilityIncrease,
        FeatSelection,
        SkillAllocation,
        SpellSelection
    }

    private LevelUpStep _currentStep;
    private Transform _contentContainer;

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

            case LevelUpStep.FeatSelection:
                if (_currentLevelUp.NeedsFeat)
                    ShowFeatSelection();
                else
                    NextStep();
                break;

            case LevelUpStep.SkillAllocation:
                if (_currentLevelUp.SkillPointsToAllocate > 0)
                    ShowSkillAllocation();
                else
                    NextStep();
                break;

            case LevelUpStep.SpellSelection:
                if (_currentLevelUp.NeedsSpellSelection)
                    ShowSpellSelection();
                else
                    NextStep();
                break;
        }
    }

    private void NextStep()
    {
        _currentStep++;

        if (_currentStep > LevelUpStep.SpellSelection)
        {
            ApplyLevelUp();
            _currentIndex++;
            ShowNextCharacter();
            return;
        }

        ShowCurrentStep();
    }

    private void ShowSummary()
    {
        Debug.Log("[LevelUpUI] Showing summary");
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
        Debug.Log("[LevelUpUI] Showing ability increase");
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
            _currentLevelUp.SelectedAbility = code;
            Debug.Log($"[LevelUpUI] Selected {abilityName} increase");
            NextStep();
        });
    }

    private static int GetModifier(int score)
    {
        return Mathf.FloorToInt((score - 10) / 2f);
    }

    private void ShowFeatSelection()
    {
        Debug.Log("[LevelUpUI] Showing feat selection");
        ClearContent();

        CreateTitle("Choose Feat");
        CreateInfoText("Select a feat from the list:");
        CreateSeparator();

        List<FeatData> availableFeats = GetAvailableFeats(_currentLevelUp.Character);
        if (availableFeats.Count == 0)
        {
            CreateInfoText("No valid feats available right now. Skipping feat selection.", true, Color.yellow);
            CreateButton("Continue", NextStep);
            return;
        }

        for (int i = 0; i < availableFeats.Count; i++)
            CreateFeatButton(availableFeats[i]);
    }

    private List<FeatData> GetAvailableFeats(CharacterController character)
    {
        CharacterStats stats = character != null ? character.Stats : null;
        if (stats == null)
            return new List<FeatData>();

        FeatDefinitions.Init();

        List<FeatDefinition> availableDefs = FeatDefinitions.GetAvailableFeats(stats, fighterBonusOnly: false);
        availableDefs.Sort((a, b) => string.Compare(a.FeatName, b.FeatName, StringComparison.Ordinal));

        List<FeatData> feats = new List<FeatData>();
        for (int i = 0; i < availableDefs.Count; i++)
        {
            FeatDefinition feat = availableDefs[i];
            feats.Add(new FeatData(
                feat.FeatName,
                feat.Description,
                feat.GetPrerequisitesString(),
                feat.Benefit != null ? feat.Benefit.Description : string.Empty));
        }

        return feats;
    }

    private void CreateFeatButton(FeatData feat)
    {
        string prereq = string.IsNullOrWhiteSpace(feat.Prerequisites) ? "None" : feat.Prerequisites;
        string benefit = string.IsNullOrWhiteSpace(feat.Benefit) ? feat.Description : feat.Benefit;
        string label = $"{feat.FeatName}\nPrereq: {prereq}\n{benefit}";

        CreateButton(label, () =>
        {
            _currentLevelUp.SelectedFeat = feat;
            Debug.Log($"[LevelUpUI] Selected feat: {feat.FeatName}");
            NextStep();
        }, 76f);
    }

    private void ShowSkillAllocation()
    {
        Debug.Log("[LevelUpUI] Showing skill allocation");
        ClearContent();

        CreateTitle("Allocate Skill Points");
        CreateInfoText($"You have {_currentLevelUp.SkillPointsToAllocate} skill points to allocate.");

        CreateSeparator();
        CreateInfoText("Auto-allocating skill points based on class skills for now.");

        AutoAllocateSkills();

        foreach (KeyValuePair<string, int> allocation in _currentLevelUp.SkillPointsAllocated)
            CreateInfoText($"• {allocation.Key}: +{allocation.Value}");

        CreateButton("Continue", NextStep);
    }

    private void AutoAllocateSkills()
    {
        _currentLevelUp.SkillPointsAllocated.Clear();

        CharacterStats stats = _currentLevelUp.Character != null ? _currentLevelUp.Character.Stats : null;
        int points = Mathf.Max(0, _currentLevelUp.SkillPointsToAllocate);

        if (stats == null || stats.Skills == null || stats.Skills.Count == 0 || points == 0)
        {
            if (points > 0)
                _currentLevelUp.SkillPointsAllocated["Auto"] = points;
            Debug.Log($"[LevelUpUI] Auto-allocated {points} skill points");
            return;
        }

        List<Skill> classSkills = stats.Skills.Values
            .Where(s => s != null && s.IsClassSkill)
            .OrderBy(s => s.Ranks)
            .ThenBy(s => s.SkillName)
            .ToList();

        if (classSkills.Count == 0)
            classSkills = stats.Skills.Values.Where(s => s != null).OrderBy(s => s.Ranks).ThenBy(s => s.SkillName).ToList();

        int idx = 0;
        while (points > 0 && classSkills.Count > 0)
        {
            Skill skill = classSkills[idx % classSkills.Count];
            if (!_currentLevelUp.SkillPointsAllocated.ContainsKey(skill.SkillName))
                _currentLevelUp.SkillPointsAllocated[skill.SkillName] = 0;

            _currentLevelUp.SkillPointsAllocated[skill.SkillName]++;
            points--;
            idx++;
        }

        Debug.Log($"[LevelUpUI] Auto-allocated {_currentLevelUp.SkillPointsToAllocate} skill points");
    }

    private void ShowSpellSelection()
    {
        Debug.Log("[LevelUpUI] Showing spell selection");
        ClearContent();

        CreateTitle("Learn New Spells");

        CharacterStats stats = _currentLevelUp.Character != null ? _currentLevelUp.Character.Stats : null;
        string className = stats != null ? stats.CharacterClass : string.Empty;

        if (className == "Wizard")
        {
            CreateInfoText("Wizards learn 2 new spells per level.");
            CreateInfoText("(Auto-selecting for now)");
            AutoSelectSpells(2);
        }
        else if (className == "Sorcerer")
        {
            CreateInfoText("Sorcerers learn 1 new spell at certain levels.");
            CreateInfoText("(Auto-selecting for now)");
            AutoSelectSpells(1);
        }
        else if (className == "Cleric" || className == "Druid")
        {
            CreateInfoText("Divine casters gain access to all spells of available levels.");
        }
        else if (className == "Bard" || className == "Paladin" || className == "Ranger")
        {
            CreateInfoText("Known spell progression applies; selection UI is placeholder for now.");
            AutoSelectSpells(1);
        }
        else
        {
            CreateInfoText("No spell updates needed.");
        }

        CreateButton("Continue", NextStep);
    }

    private static string GetSpellSummaryText(string className)
    {
        if (className == "Wizard")
            return "Learn 2 wizard spells";
        if (className == "Sorcerer")
            return "Learn sorcerer spell(s)";
        if (className == "Cleric" || className == "Druid")
            return "Access higher-level divine spells";
        return "Review spell progression";
    }

    private void AutoSelectSpells(int count)
    {
        Debug.Log($"[LevelUpUI] Auto-selecting {count} spells");
        // TODO: Connect to spell-learning system once known/learned spell tracking is exposed.
    }

    private void ApplyLevelUp()
    {
        string name = GetCharacterName(_currentLevelUp.Character);
        Debug.Log($"[LevelUpUI] Applying level-up for {name}");

        CharacterStats stats = _currentLevelUp.Character != null ? _currentLevelUp.Character.Stats : null;
        if (stats == null)
        {
            Debug.LogWarning("[LevelUpUI] Cannot apply level-up choices because CharacterStats is null.");
            return;
        }

        if (!string.IsNullOrEmpty(_currentLevelUp.SelectedAbility))
        {
            switch (_currentLevelUp.SelectedAbility)
            {
                case "STR": stats.BaseSTR++; stats.STR++; break;
                case "DEX": stats.BaseDEX++; stats.DEX++; break;
                case "CON": stats.BaseCON++; stats.CON++; break;
                case "INT": stats.BaseINT++; stats.INT++; break;
                case "WIS": stats.BaseWIS++; stats.WIS++; break;
                case "CHA": stats.BaseCHA++; stats.CHA++; break;
            }

            Debug.Log($"[LevelUpUI] Applied +1 to {_currentLevelUp.SelectedAbility}");
        }

        if (_currentLevelUp.SelectedFeat != null && !string.IsNullOrWhiteSpace(_currentLevelUp.SelectedFeat.FeatName))
        {
            stats.Feats.Add(_currentLevelUp.SelectedFeat.FeatName);
            Debug.Log($"[LevelUpUI] Applied feat: {_currentLevelUp.SelectedFeat.FeatName}");
        }

        if (_currentLevelUp.SkillPointsAllocated.Count > 0 && stats.Skills != null)
        {
            int grantedPoints = Mathf.Max(0, _currentLevelUp.SkillPointsToAllocate);
            stats.TotalSkillPoints += grantedPoints;
            stats.AvailableSkillPoints += grantedPoints;

            foreach (KeyValuePair<string, int> allocation in _currentLevelUp.SkillPointsAllocated)
            {
                for (int i = 0; i < allocation.Value; i++)
                    stats.AddSkillRank(allocation.Key);
            }
        }

        Debug.Log($"[LevelUpUI] Level-up complete for {name}");
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
