using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reuses character creation selection panels during post-combat level-up.
/// This manager currently orchestrates feat, skill, and spell choices.
/// </summary>
public class CharacterCreationManager : MonoBehaviour
{
    private bool _isLevelUpMode;
    private CharacterController _levelingCharacter;
    private LevelUpData _levelUpData;
    private Action _levelUpCompleteCallback;

    private FeatSelectionUI _featSelectionUI;
    private SkillsUIPanel _skillsUI;
    private SpellSelectionUI _spellSelectionUI;
    private DomainSelectionUI _domainSelectionUI;
    private WizardSpecializationUI _wizardSpecializationUI;
    private FamiliarSelectionUI _familiarSelectionUI;
    private bool _domainSelectionAttemptedThisFlow;

    /// <summary>
    /// Existing/new-character creation entry point (placeholder for compatibility).
    /// </summary>
    public void StartCharacterCreation(Action onComplete)
    {
        _isLevelUpMode = false;
        onComplete?.Invoke();
    }

    /// <summary>
    /// Starts level-up flow using existing character creation components.
    /// </summary>
    public void StartLevelUpFlow(CharacterController character, LevelUpData levelUpData, Action onComplete)
    {
        if (character == null || character.Stats == null)
        {
            Debug.LogWarning("[CharacterCreationManager] StartLevelUpFlow called with null character/stats.");
            onComplete?.Invoke();
            return;
        }

        _isLevelUpMode = true;
        _levelingCharacter = character;
        _levelUpData = levelUpData ?? new LevelUpData { Character = character };
        _levelUpCompleteCallback = onComplete;
        _domainSelectionAttemptedThisFlow = false;

        Debug.Log($"[CharacterCreationManager] Starting level-up flow for {character.Stats.CharacterName} (level {character.Stats.Level})");

        DetermineLevelUpChoices();
    }

    private void DetermineLevelUpChoices()
    {
        if (!_isLevelUpMode || _levelingCharacter == null)
        {
            CompleteLevelUp();
            return;
        }

        StartLevelUpSequence();
    }

    private void StartLevelUpSequence()
    {
        // Class selection and ability score are currently handled outside this manager.
        // This manager focuses on reusing existing feat/skill/spell panels.
        ShowFeatSelection();
    }

    private void ShowFeatSelection()
    {
        if (_levelUpData == null || !_levelUpData.NeedsFeat || _levelUpData.TotalFeatsToSelect <= 0)
        {
            ShowSkillSelection();
            return;
        }

        CharacterStats stats = _levelingCharacter != null ? _levelingCharacter.Stats : null;
        if (stats == null)
        {
            ShowSkillSelection();
            return;
        }

        ShowGeneralFeatSelection(stats);
    }

    private void ShowGeneralFeatSelection(CharacterStats stats)
    {
        int generalFeats = _levelUpData != null ? Mathf.Max(0, _levelUpData.GeneralFeatsToSelect) : 0;
        if (generalFeats <= 0)
        {
            ShowFighterBonusFeatSelection(stats);
            return;
        }

        string title = generalFeats == 1 ? "Select Level-Up Feat" : $"Select {generalFeats} Level-Up Feats";
        OpenFeatSelection(
            stats,
            generalFeats,
            fighterBonusOnly: false,
            title: title,
            subtitle: "General feats from total character level progression.",
            onConfirmed: selectedFeats =>
            {
                ApplySelectedFeats(stats, selectedFeats);
                ShowFighterBonusFeatSelection(stats);
            });
    }

    private void ShowFighterBonusFeatSelection(CharacterStats stats)
    {
        int fighterBonusFeats = _levelUpData != null ? Mathf.Max(0, _levelUpData.FighterBonusFeatsToSelect) : 0;
        if (fighterBonusFeats <= 0)
        {
            ShowMonkBonusFeatSelection(stats);
            return;
        }

        string title = fighterBonusFeats == 1 ? "Select Fighter Bonus Feat" : $"Select {fighterBonusFeats} Fighter Bonus Feats";
        OpenFeatSelection(
            stats,
            fighterBonusFeats,
            fighterBonusOnly: true,
            title: title,
            subtitle: "Bonus feats from Fighter class level progression.",
            onConfirmed: selectedFeats =>
            {
                ApplySelectedFeats(stats, selectedFeats);
                ShowMonkBonusFeatSelection(stats);
            });
    }

    private void ShowMonkBonusFeatSelection(CharacterStats stats)
    {
        int monkBonusLevel = _levelUpData != null ? Mathf.Max(0, _levelUpData.MonkBonusFeatLevelToSelect) : 0;
        if (monkBonusLevel <= 0)
        {
            ShowWizardBonusFeatSelection(stats);
            return;
        }

        OpenFeatSelection(
            stats,
            1,
            fighterBonusOnly: false,
            title: $"Select Monk Bonus Feat (Level {monkBonusLevel})",
            subtitle: "Monk bonus feat from Monk class level progression.",
            monkBonusLevel: monkBonusLevel,
            onConfirmed: selectedFeats =>
            {
                ApplySelectedFeats(stats, selectedFeats);
                ShowWizardBonusFeatSelection(stats);
            });
    }

    private void ShowWizardBonusFeatSelection(CharacterStats stats)
    {
        int wizardBonusFeats = _levelUpData != null ? Mathf.Max(0, _levelUpData.WizardBonusFeatsToSelect) : 0;
        if (wizardBonusFeats <= 0)
        {
            ShowSkillSelection();
            return;
        }

        string title = wizardBonusFeats == 1 ? "Select Wizard Bonus Feat" : $"Select {wizardBonusFeats} Wizard Bonus Feats";
        OpenFeatSelection(
            stats,
            wizardBonusFeats,
            fighterBonusOnly: false,
            title: title,
            subtitle: "Bonus feats from Wizard class level progression (metamagic/item creation/spell mastery).",
            wizardBonus: true,
            onConfirmed: selectedFeats =>
            {
                ApplySelectedFeats(stats, selectedFeats);
                ShowSkillSelection();
            });
    }

    private void OpenFeatSelection(
        CharacterStats stats,
        int featsToSelect,
        bool fighterBonusOnly,
        string title,
        string subtitle,
        Action<List<string>> onConfirmed,
        int monkBonusLevel = 0,
        bool wizardBonus = false)
    {
        FeatSelectionUI featUI = FindOrCreateFeatSelectionUI();
        if (featUI == null)
        {
            Debug.LogWarning("[CharacterCreationManager] FeatSelectionUI unavailable. Skipping feat step.");
            onConfirmed?.Invoke(new List<string>());
            return;
        }

        int safeFeatCount = Mathf.Max(0, featsToSelect);
        if (safeFeatCount <= 0)
        {
            onConfirmed?.Invoke(new List<string>());
            return;
        }

        featUI.OnFeatsConfirmed = selected => onConfirmed?.Invoke(selected ?? new List<string>());
        featUI.OpenForSelection(
            stats,
            safeFeatCount,
            fighterBonusOnly: fighterBonusOnly,
            title: title,
            subtitle: subtitle,
            monkBonusLevel: monkBonusLevel,
            wizardBonus: wizardBonus);
    }

    private static void ApplySelectedFeats(CharacterStats stats, List<string> selectedFeats)
    {
        if (stats == null || selectedFeats == null)
            return;

        for (int i = 0; i < selectedFeats.Count; i++)
        {
            string featName = selectedFeats[i];
            if (string.IsNullOrWhiteSpace(featName))
                continue;

            if (!stats.Feats.Contains(featName))
            {
                stats.Feats.Add(featName);
                Debug.Log($"[CharacterCreationManager] Applied level-up feat: {featName}");
            }
        }
    }

    private void ShowSkillSelection()
    {
        int points = _levelUpData != null ? Mathf.Max(0, _levelUpData.SkillPointsToAllocate) : 0;
        int newSkillPoints = _levelUpData != null ? Mathf.Max(0, _levelUpData.SkillPointsNew) : 0;
        int classPoolPoints = _levelUpData != null ? Mathf.Max(0, _levelUpData.SkillPointsFromClassPool) : 0;
        if (points <= 0)
        {
            ShowWizardLevelOneChoices();
            return;
        }

        SkillsUIPanel skillsUI = FindOrCreateSkillSelectionUI();
        if (skillsUI == null)
        {
            Debug.LogWarning("[CharacterCreationManager] SkillsUIPanel unavailable. Skipping skill step.");
            ShowWizardLevelOneChoices();
            return;
        }

        string advancingClass = !string.IsNullOrWhiteSpace(_levelUpData?.SelectedClassName)
            ? _levelUpData.SelectedClassName
            : (_levelingCharacter != null && _levelingCharacter.Stats != null ? _levelingCharacter.Stats.CharacterClass : null);

        skillsUI.ShowForLevelUp(_levelingCharacter, newSkillPoints, classPoolPoints, advancingClass, ShowWizardLevelOneChoices);
    }

    private void ShowWizardLevelOneChoices()
    {
        if (_levelingCharacter == null || _levelingCharacter.Stats == null)
        {
            ShowSpellSelection();
            return;
        }

        CharacterStats stats = _levelingCharacter.Stats;
        string selectedClass = _levelUpData != null ? _levelUpData.SelectedClassName : string.Empty;
        bool isWizardProgression = string.Equals(selectedClass, "Wizard", StringComparison.OrdinalIgnoreCase);
        bool isWizardLevelOne = stats.GetClassLevel("Wizard") == 1;

        if (!isWizardProgression || !isWizardLevelOne)
        {
            ShowSpellSelection();
            return;
        }

        ShowWizardSpecializationSelection();
    }

    private void ShowWizardSpecializationSelection()
    {
        WizardSpecializationUI specializationUI = FindOrCreateWizardSpecializationUI();
        if (specializationUI == null)
        {
            Debug.LogWarning("[CharacterCreationManager] WizardSpecializationUI unavailable. Defaulting to generalist.");
            _levelingCharacter.Stats.WizardSpecialization = WizardSpecialization.CreateGeneralist();
            ShowWizardFamiliarSelection();
            return;
        }

        WizardSpecialization initial = _levelingCharacter.Stats.WizardSpecialization ?? WizardSpecialization.CreateGeneralist();
        specializationUI.Show(initial, selected =>
        {
            _levelingCharacter.Stats.WizardSpecialization = selected ?? WizardSpecialization.CreateGeneralist();
            _levelingCharacter.Stats.WizardSpecialization.Normalize();
            ShowWizardFamiliarSelection();
        });
    }

    private void ShowWizardFamiliarSelection()
    {
        FamiliarSelectionUI familiarUI = FindOrCreateFamiliarSelectionUI();
        if (familiarUI == null)
        {
            Debug.LogWarning("[CharacterCreationManager] FamiliarSelectionUI unavailable. Defaulting to no familiar.");
            _levelingCharacter.Stats.ApplyWizardFamiliar(WizardFamiliar.CreateNone());
            ShowSpellSelection();
            return;
        }

        WizardFamiliar initial = _levelingCharacter.Stats.WizardFamiliar ?? WizardFamiliar.CreateNone();
        familiarUI.Show(initial, selected =>
        {
            _levelingCharacter.Stats.ApplyWizardFamiliar(selected ?? WizardFamiliar.CreateNone());
            ShowSpellSelection();
        });
    }

    private void ShowDomainSelection()
    {
        Debug.Log("[CharacterCreation] Showing domain selection for Cleric");

        if (_levelingCharacter == null || _levelingCharacter.Stats == null)
        {
            ShowSpellSelection();
            return;
        }

        DomainSelectionUI domainUI = FindOrCreateDomainSelectionUI();
        if (domainUI == null)
        {
            Debug.LogWarning("[CharacterCreationManager] DomainSelectionUI unavailable. Continuing without changes.");
            ShowSpellSelection();
            return;
        }

        domainUI.Show(_levelingCharacter, 2, selectedDomains =>
        {
            List<string> domains = selectedDomains ?? new List<string>();
            _levelingCharacter.Stats.ChosenDomains = new List<string>(domains);
            Debug.Log($"[CharacterCreation] Domains selected: {string.Join(", ", domains)}");

            SpellcastingComponent spellcasting = _levelingCharacter.Spellcasting;
            if (spellcasting != null)
            {
                spellcasting.RefreshSpellSlots();
            }

            ShowSpellSelection();
        });
    }

    private void ShowSpellSelection()
    {
        Debug.Log("[CharacterCreation] Step 5: Spell Selection (Level-Up)");

        if (_levelUpData == null || !_levelUpData.NeedsSpellSelection)
        {
            CompleteLevelUp();
            return;
        }

        CharacterStats stats = _levelingCharacter != null ? _levelingCharacter.Stats : null;
        if (stats == null)
        {
            CompleteLevelUp();
            return;
        }

        string progressionClass = !string.IsNullOrWhiteSpace(_levelUpData?.SelectedClassName) ? _levelUpData.SelectedClassName : stats.CharacterClass;
        if (!IsSpellcaster(progressionClass))
        {
            Debug.Log($"[CharacterCreation] {progressionClass} is not a spellcaster");
            CompleteLevelUp();
            return;
        }

        SpellcastingComponent spellcasting = EnsureSpellcastingComponentForLevelUp(progressionClass);
        if (spellcasting == null)
        {
            Debug.LogWarning($"[CharacterCreationManager] Unable to initialize SpellcastingComponent for {progressionClass}. Skipping spell step.");
            CompleteLevelUp();
            return;
        }

        if (string.Equals(progressionClass, "Cleric", StringComparison.OrdinalIgnoreCase)
            && (stats.ChosenDomains == null || stats.ChosenDomains.Count == 0)
            && !_domainSelectionAttemptedThisFlow)
        {
            _domainSelectionAttemptedThisFlow = true;
            ShowDomainSelection();
            return;
        }

        Debug.Log($"[CharacterCreation] {progressionClass} can learn new spells");

        SpellSelectionUI spellUI = FindOrCreateSpellSelectionUI();
        if (spellUI == null)
        {
            Debug.LogWarning("[CharacterCreationManager] SpellSelectionUI unavailable. Skipping spell step.");
            CompleteLevelUp();
            return;
        }

        spellUI.ShowForLevelUp(_levelingCharacter, progressionClass, selectedSpellIds =>
        {
            ApplyLevelUpSpellSelection(progressionClass, selectedSpellIds);
            Debug.Log("[CharacterCreation] Spells selected, level-up complete");
            CompleteLevelUp();
        });
    }

    private void ApplyLevelUpSpellSelection(string progressionClass, List<string> selectedSpellIds)
    {
        if (_levelingCharacter == null || selectedSpellIds == null || selectedSpellIds.Count == 0)
            return;

        SpellcastingComponent spellcasting = _levelingCharacter.Spellcasting;
        if (spellcasting == null)
        {
            Debug.LogWarning("[CharacterCreationManager] No SpellcastingComponent found while applying level-up spells.");
            return;
        }

        SpellDatabase.Init();

        int learnedCount = 0;
        for (int i = 0; i < selectedSpellIds.Count; i++)
        {
            string spellId = selectedSpellIds[i];
            if (string.IsNullOrWhiteSpace(spellId))
                continue;

            bool alreadyKnown = spellcasting.GetKnownSpellsForClass(progressionClass)
                .Exists(s => s != null && s.SpellId == spellId);
            spellcasting.LearnSpellForClass(progressionClass, spellId);
            if (!alreadyKnown)
                learnedCount++;
        }

        spellcasting.SyncPreparedSpellsFromSlots();

        Debug.Log($"[CharacterCreationManager] Applied {learnedCount} level-up spell selection(s) for {_levelingCharacter.Stats.CharacterName}.");
    }

    private SpellcastingComponent EnsureSpellcastingComponentForLevelUp(string progressionClass)
    {
        if (_levelingCharacter == null || _levelingCharacter.Stats == null)
            return null;

        if (!IsSpellcaster(progressionClass))
            return _levelingCharacter.Spellcasting;

        SpellcastingComponent spellcasting = _levelingCharacter.Spellcasting;
        bool createdDuringLevelUp = false;
        int wizardKnownBeforeRefresh = 0;

        if (spellcasting != null)
            wizardKnownBeforeRefresh = spellcasting.GetKnownSpellsForClass("Wizard").Count;

        if (spellcasting == null)
        {
            spellcasting = _levelingCharacter.gameObject.AddComponent<SpellcastingComponent>();
            spellcasting.Init(_levelingCharacter.Stats);
            createdDuringLevelUp = true;

            string characterName = !string.IsNullOrWhiteSpace(_levelingCharacter.Stats.CharacterName)
                ? _levelingCharacter.Stats.CharacterName
                : _levelingCharacter.name;
            Debug.Log($"[CharacterCreationManager] Added SpellcastingComponent for {characterName} before level-up spell selection.");
        }

        spellcasting.RefreshSpellSlots();

        if (string.Equals(progressionClass, "Wizard", StringComparison.OrdinalIgnoreCase))
        {
            int wizardClassLevel = _levelingCharacter.Stats.GetClassLevel("Wizard");
            bool isInitialWizardLevel = wizardClassLevel == 1;

            if (isInitialWizardLevel || createdDuringLevelUp || wizardKnownBeforeRefresh == 0)
            {
                // New multiclass wizard entries should start from cantrips only.
                // First-level spellbook entries are chosen by the user in spell selection.
                spellcasting.ResetKnownSpellsForClass("Wizard", keepCantrips: true);
                spellcasting.ClearPreparedSpells();
            }
        }

        return spellcasting;
    }

    private bool IsSpellcaster(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
            return false;

        return string.Equals(className, "Wizard", StringComparison.OrdinalIgnoreCase)
            || string.Equals(className, "Cleric", StringComparison.OrdinalIgnoreCase)
            || string.Equals(className, "Druid", StringComparison.OrdinalIgnoreCase)
            || string.Equals(className, "Sorcerer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(className, "Bard", StringComparison.OrdinalIgnoreCase);
    }

    private void CompleteLevelUp()
    {
        string name = _levelingCharacter != null && _levelingCharacter.Stats != null
            ? _levelingCharacter.Stats.CharacterName
            : "Unknown";

        Debug.Log($"[CharacterCreationManager] Level-up flow complete for {name}");

        if (_levelingCharacter != null)
        {
            SpellcastingComponent spellcasting = _levelingCharacter.Spellcasting;
            if (spellcasting != null)
            {
                Debug.Log($"[CharacterCreationManager] Refreshing spell slots after level-up for {name}");
                spellcasting.RefreshSpellSlots();
            }
        }

        Action callback = _levelUpCompleteCallback;

        _isLevelUpMode = false;
        _levelingCharacter = null;
        _levelUpData = null;
        _levelUpCompleteCallback = null;
        _domainSelectionAttemptedThisFlow = false;

        callback?.Invoke();
    }

    private FeatSelectionUI FindOrCreateFeatSelectionUI()
    {
        if (_featSelectionUI != null)
            return _featSelectionUI;

        _featSelectionUI = FindObjectOfType<FeatSelectionUI>();
        if (_featSelectionUI != null)
            return _featSelectionUI;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
            return null;

        GameObject uiObj = new GameObject("FeatSelectionUI", typeof(RectTransform));
        uiObj.transform.SetParent(canvas.transform, false);
        _featSelectionUI = uiObj.AddComponent<FeatSelectionUI>();
        _featSelectionUI.BuildUI(canvas);
        return _featSelectionUI;
    }

    private SkillsUIPanel FindOrCreateSkillSelectionUI()
    {
        if (_skillsUI != null)
            return _skillsUI;

        _skillsUI = FindObjectOfType<SkillsUIPanel>();
        if (_skillsUI != null)
            return _skillsUI;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
            return null;

        GameObject uiObj = new GameObject("SkillsUIPanel", typeof(RectTransform));
        uiObj.transform.SetParent(canvas.transform, false);
        _skillsUI = uiObj.AddComponent<SkillsUIPanel>();
        _skillsUI.BuildUI(canvas);
        return _skillsUI;
    }

    private SpellSelectionUI FindOrCreateSpellSelectionUI()
    {
        if (_spellSelectionUI != null)
            return _spellSelectionUI;

        _spellSelectionUI = FindObjectOfType<SpellSelectionUI>();
        if (_spellSelectionUI != null)
            return _spellSelectionUI;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
            return null;

        GameObject uiObj = new GameObject("SpellSelectionUI", typeof(RectTransform));
        uiObj.transform.SetParent(canvas.transform, false);
        _spellSelectionUI = uiObj.AddComponent<SpellSelectionUI>();
        _spellSelectionUI.BuildUI(canvas);
        return _spellSelectionUI;
    }

    private DomainSelectionUI FindOrCreateDomainSelectionUI()
    {
        if (_domainSelectionUI != null)
            return _domainSelectionUI;

        _domainSelectionUI = FindObjectOfType<DomainSelectionUI>();
        if (_domainSelectionUI != null)
            return _domainSelectionUI;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
            return null;

        GameObject uiObj = new GameObject("DomainSelectionUI", typeof(RectTransform));
        uiObj.transform.SetParent(canvas.transform, false);
        _domainSelectionUI = uiObj.AddComponent<DomainSelectionUI>();
        _domainSelectionUI.BuildUI(canvas);
        return _domainSelectionUI;
    }

    private WizardSpecializationUI FindOrCreateWizardSpecializationUI()
    {
        if (_wizardSpecializationUI != null)
            return _wizardSpecializationUI;

        _wizardSpecializationUI = FindObjectOfType<WizardSpecializationUI>();
        if (_wizardSpecializationUI != null)
            return _wizardSpecializationUI;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
            return null;

        GameObject uiObj = new GameObject("WizardSpecializationUI", typeof(RectTransform));
        uiObj.transform.SetParent(canvas.transform, false);
        _wizardSpecializationUI = uiObj.AddComponent<WizardSpecializationUI>();
        _wizardSpecializationUI.BuildUI(canvas);
        return _wizardSpecializationUI;
    }

    private FamiliarSelectionUI FindOrCreateFamiliarSelectionUI()
    {
        if (_familiarSelectionUI != null)
            return _familiarSelectionUI;

        _familiarSelectionUI = FindObjectOfType<FamiliarSelectionUI>();
        if (_familiarSelectionUI != null)
            return _familiarSelectionUI;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
            return null;

        GameObject uiObj = new GameObject("FamiliarSelectionUI", typeof(RectTransform));
        uiObj.transform.SetParent(canvas.transform, false);
        _familiarSelectionUI = uiObj.AddComponent<FamiliarSelectionUI>();
        _familiarSelectionUI.BuildUI(canvas);
        return _familiarSelectionUI;
    }
}
