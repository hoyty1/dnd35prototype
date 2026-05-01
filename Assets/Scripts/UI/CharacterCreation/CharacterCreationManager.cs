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
        if (_levelUpData == null || !_levelUpData.NeedsFeat)
        {
            ShowSkillSelection();
            return;
        }

        FeatSelectionUI featUI = FindOrCreateFeatSelectionUI();
        if (featUI == null)
        {
            Debug.LogWarning("[CharacterCreationManager] FeatSelectionUI unavailable. Skipping feat step.");
            ShowSkillSelection();
            return;
        }

        featUI.ShowForLevelUp(_levelingCharacter, 1, selectedFeats =>
        {
            CharacterStats stats = _levelingCharacter != null ? _levelingCharacter.Stats : null;
            if (stats != null && selectedFeats != null)
            {
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

            ShowSkillSelection();
        });
    }

    private void ShowSkillSelection()
    {
        int points = _levelUpData != null ? Mathf.Max(0, _levelUpData.SkillPointsToAllocate) : 0;
        if (points <= 0)
        {
            ShowSpellSelection();
            return;
        }

        SkillsUIPanel skillsUI = FindOrCreateSkillSelectionUI();
        if (skillsUI == null)
        {
            Debug.LogWarning("[CharacterCreationManager] SkillsUIPanel unavailable. Skipping skill step.");
            ShowSpellSelection();
            return;
        }

        skillsUI.ShowForLevelUp(_levelingCharacter, points, ShowSpellSelection);
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

            SpellcastingComponent spellcasting = _levelingCharacter.GetComponent<SpellcastingComponent>();
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

        if (!IsSpellcaster(stats.CharacterClass))
        {
            Debug.Log($"[CharacterCreation] {stats.CharacterClass} is not a spellcaster");
            CompleteLevelUp();
            return;
        }

        if (string.Equals(stats.CharacterClass, "Cleric", StringComparison.OrdinalIgnoreCase)
            && (stats.ChosenDomains == null || stats.ChosenDomains.Count == 0)
            && !_domainSelectionAttemptedThisFlow)
        {
            _domainSelectionAttemptedThisFlow = true;
            ShowDomainSelection();
            return;
        }

        Debug.Log($"[CharacterCreation] {stats.CharacterClass} can learn new spells");

        SpellSelectionUI spellUI = FindOrCreateSpellSelectionUI();
        if (spellUI == null)
        {
            Debug.LogWarning("[CharacterCreationManager] SpellSelectionUI unavailable. Skipping spell step.");
            CompleteLevelUp();
            return;
        }

        spellUI.ShowForLevelUp(_levelingCharacter, selectedSpellIds =>
        {
            ApplyLevelUpSpellSelection(selectedSpellIds);
            Debug.Log("[CharacterCreation] Spells selected, level-up complete");
            CompleteLevelUp();
        });
    }

    private void ApplyLevelUpSpellSelection(List<string> selectedSpellIds)
    {
        if (_levelingCharacter == null || selectedSpellIds == null || selectedSpellIds.Count == 0)
            return;

        SpellcastingComponent spellcasting = _levelingCharacter.GetComponent<SpellcastingComponent>();
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

            bool alreadyKnown = spellcasting.KnownSpells.Exists(s => s != null && s.SpellId == spellId);
            spellcasting.LearnSpell(spellId);
            if (!alreadyKnown)
                learnedCount++;
        }

        spellcasting.SyncPreparedSpellsFromSlots();

        Debug.Log($"[CharacterCreationManager] Applied {learnedCount} level-up spell selection(s) for {_levelingCharacter.Stats.CharacterName}.");
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
            SpellcastingComponent spellcasting = _levelingCharacter.GetComponent<SpellcastingComponent>();
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
}
