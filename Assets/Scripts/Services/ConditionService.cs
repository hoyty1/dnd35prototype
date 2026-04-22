using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralized runtime service for combat conditions and status effects.
///
/// Responsibilities:
/// - Track active combat conditions per character
/// - Apply/remove/query condition state
/// - Run turn/round-based expiration processing
/// - Coordinate tactical condition refreshes (for example Flanked)
/// - Cleanup condition state on death/combat end
///
/// This service intentionally interoperates with CharacterController/ConditionManager so existing
/// condition consumers keep working while GameManager delegates orchestration concerns.
/// </summary>
public class ConditionService : MonoBehaviour
{
    [Serializable]
    public class ActiveCondition
    {
        public CombatConditionType Type;
        public int RemainingRounds;
        public CharacterController Source;
        public object Data;
        public bool ExpiresAtEndOfTurn;
        public bool ExpiresAtStartOfTurn;
    }

    private readonly Dictionary<CharacterController, List<ActiveCondition>> _activeConditionsByCharacter =
        new Dictionary<CharacterController, List<ActiveCondition>>();

    private TurnService _turnService;
    private Func<List<CharacterController>> _allCharactersProvider;
    private int _lastProcessedRound = -1;

    public event Action<CharacterController, ActiveCondition> OnConditionExpired;

    public void Initialize(Func<List<CharacterController>> allCharactersProvider)
    {
        _allCharactersProvider = allCharactersProvider;
        SyncAllTrackedCharacters();
    }

    public void BindTurnService(TurnService turnService)
    {
        if (_turnService == turnService)
            return;

        UnbindTurnService();
        _turnService = turnService;

        if (_turnService != null)
        {
            _turnService.OnTurnStarted += OnTurnStartedFromTurnService;
            _turnService.OnNewRound += OnNewRoundFromTurnService;
        }
    }

    public void UnbindTurnService()
    {
        if (_turnService == null)
            return;

        _turnService.OnTurnStarted -= OnTurnStartedFromTurnService;
        _turnService.OnNewRound -= OnNewRoundFromTurnService;
        _turnService = null;
    }

    public void ApplyCondition(
        CharacterController target,
        CombatConditionType type,
        int rounds,
        CharacterController source = null,
        object data = null,
        bool expiresAtEndOfTurn = false,
        bool expiresAtStartOfTurn = false,
        string sourceNameOverride = null)
    {
        if (!IsValidCharacter(target))
            return;

        string sourceName = !string.IsNullOrWhiteSpace(sourceNameOverride)
            ? sourceNameOverride
            : (source != null && source.Stats != null
                ? source.Stats.CharacterName
                : (target.Stats != null ? target.Stats.CharacterName : "Unknown"));

        target.ApplyConditionDirect(type, rounds, sourceName);
        SyncCharacter(target);

        ActiveCondition tracked = FindActiveCondition(target, type);
        if (tracked == null)
            return;

        tracked.Source = source;
        tracked.Data = data;
        tracked.ExpiresAtEndOfTurn = expiresAtEndOfTurn;
        tracked.ExpiresAtStartOfTurn = expiresAtStartOfTurn;
    }

    public bool RemoveCondition(CharacterController target, CombatConditionType type)
    {
        if (!IsValidCharacter(target))
            return false;

        bool removed = target.RemoveConditionDirect(type);
        SyncCharacter(target);
        return removed;
    }

    public int RemoveAllConditions(CharacterController target)
    {
        if (!IsValidCharacter(target))
            return 0;

        SyncCharacter(target);
        if (!_activeConditionsByCharacter.TryGetValue(target, out List<ActiveCondition> activeList) || activeList == null || activeList.Count == 0)
            return 0;

        int removed = 0;
        // Copy list so removal does not mutate while iterating.
        List<ActiveCondition> snapshot = new List<ActiveCondition>(activeList);
        for (int i = 0; i < snapshot.Count; i++)
        {
            if (target.RemoveConditionDirect(snapshot[i].Type))
                removed++;
        }

        SyncCharacter(target);
        return removed;
    }

    public List<ActiveCondition> UpdateConditionTimers(CharacterController target)
    {
        var expired = new List<ActiveCondition>();
        if (!IsValidCharacter(target))
            return expired;

        List<StatusEffect> expiredEffects = target.TickConditionsDirect();
        if (expiredEffects == null || expiredEffects.Count == 0)
        {
            SyncCharacter(target);
            return expired;
        }

        for (int i = 0; i < expiredEffects.Count; i++)
        {
            StatusEffect effect = expiredEffects[i];
            ActiveCondition condition = ConvertToActiveCondition(effect, ResolveSourceCharacter(effect));
            expired.Add(condition);
            OnConditionExpired?.Invoke(target, condition);
        }

        SyncCharacter(target);
        return expired;
    }

    public void OnTurnStart(CharacterController actor)
    {
        if (!IsValidCharacter(actor))
            return;

        SyncCharacter(actor);
        ExpireTurnBoundaryConditions(actor, expireAtStart: true);
    }

    public void OnTurnEnd(CharacterController actor)
    {
        if (!IsValidCharacter(actor))
            return;

        SyncCharacter(actor);
        ExpireTurnBoundaryConditions(actor, expireAtStart: false);
    }

    public void OnRoundEnd()
    {
        int roundKey = _turnService != null ? _turnService.CurrentRound : Time.frameCount;
        ProcessRoundEndInternal(roundKey);
    }

    public bool HasCondition(CharacterController target, CombatConditionType type)
    {
        if (!IsValidCharacter(target))
            return false;

        SyncCharacter(target);
        return target.HasConditionDirect(type);
    }

    public int GetConditionDuration(CharacterController target, CombatConditionType type)
    {
        if (!IsValidCharacter(target))
            return 0;

        SyncCharacter(target);

        CombatConditionType normalized = ConditionRules.Normalize(type);
        if (!_activeConditionsByCharacter.TryGetValue(target, out List<ActiveCondition> activeList) || activeList == null)
            return 0;

        for (int i = 0; i < activeList.Count; i++)
        {
            ActiveCondition condition = activeList[i];
            if (ConditionRules.Normalize(condition.Type) == normalized)
                return condition.RemainingRounds;
        }

        return 0;
    }

    public List<ActiveCondition> GetActiveConditions(CharacterController target)
    {
        if (!IsValidCharacter(target))
            return new List<ActiveCondition>();

        SyncCharacter(target);

        if (_activeConditionsByCharacter.TryGetValue(target, out List<ActiveCondition> activeList) && activeList != null)
            return new List<ActiveCondition>(activeList);

        return new List<ActiveCondition>();
    }

    public void CleanupOnDeath(CharacterController target)
    {
        if (target == null)
            return;

        RemoveAllConditions(target);
        _activeConditionsByCharacter.Remove(target);
    }

    public void CleanupOnCombatEnd(List<CharacterController> allCharacters)
    {
        if (allCharacters == null)
            return;

        for (int i = 0; i < allCharacters.Count; i++)
        {
            CharacterController c = allCharacters[i];
            if (c == null)
                continue;

            RemoveAllConditions(c);
        }

        _activeConditionsByCharacter.Clear();
    }

    public void RefreshFlankedConditions(List<CharacterController> allCharacters)
    {
        if (allCharacters == null)
            return;

        for (int i = 0; i < allCharacters.Count; i++)
        {
            CharacterController character = allCharacters[i];
            if (!IsValidCharacter(character))
                continue;

            bool hasFlanked = character.HasCondition(CombatConditionType.Flanked);

            if (character.Stats.IsDead)
            {
                if (hasFlanked)
                    RemoveCondition(character, CombatConditionType.Flanked);
                continue;
            }

            bool shouldBeFlanked = false;
            for (int j = 0; j < allCharacters.Count; j++)
            {
                CharacterController enemy = allCharacters[j];
                if (!IsValidCharacter(enemy) || enemy == character)
                    continue;
                if (enemy.Stats.IsDead)
                    continue;
                if (enemy.IsPlayerControlled == character.IsPlayerControlled)
                    continue;

                if (CombatUtils.IsAttackerFlanking(enemy, character, allCharacters, out CharacterController _))
                {
                    shouldBeFlanked = true;
                    break;
                }
            }

            if (shouldBeFlanked && !hasFlanked)
                ApplyCondition(character, CombatConditionType.Flanked, -1, source: null, sourceNameOverride: "Flanking");
            else if (!shouldBeFlanked && hasFlanked)
                RemoveCondition(character, CombatConditionType.Flanked);
        }
    }

    public CharacterController GetConditionSource(CharacterController target, CombatConditionType type)
    {
        if (!IsValidCharacter(target))
            return null;

        SyncCharacter(target);
        ActiveCondition condition = FindActiveCondition(target, type);
        return condition != null ? condition.Source : null;
    }

    private void OnTurnStartedFromTurnService(CharacterController actor)
    {
        OnTurnStart(actor);
    }

    private void OnNewRoundFromTurnService(int round)
    {
        ProcessRoundEndInternal(round);
    }

    private void ProcessRoundEndInternal(int roundKey)
    {
        if (roundKey == _lastProcessedRound)
            return;

        _lastProcessedRound = roundKey;
        SyncAllTrackedCharacters();

        List<CharacterController> characters = GetAllCharactersInternal();
        for (int i = 0; i < characters.Count; i++)
        {
            CharacterController character = characters[i];
            if (!IsValidCharacter(character) || character.Stats.IsDead)
                continue;

            UpdateConditionTimers(character);
        }
    }

    private void ExpireTurnBoundaryConditions(CharacterController actor, bool expireAtStart)
    {
        if (!_activeConditionsByCharacter.TryGetValue(actor, out List<ActiveCondition> conditions) || conditions == null || conditions.Count == 0)
            return;

        List<CombatConditionType> toRemove = new List<CombatConditionType>();
        for (int i = 0; i < conditions.Count; i++)
        {
            ActiveCondition condition = conditions[i];
            if (condition == null)
                continue;

            bool shouldExpire = expireAtStart
                ? condition.ExpiresAtStartOfTurn
                : condition.ExpiresAtEndOfTurn;

            if (shouldExpire)
                toRemove.Add(condition.Type);
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            CombatConditionType type = toRemove[i];
            ActiveCondition existing = FindActiveCondition(actor, type);
            if (!RemoveCondition(actor, type))
                continue;

            if (existing != null)
                OnConditionExpired?.Invoke(actor, existing);
        }
    }

    private ActiveCondition FindActiveCondition(CharacterController target, CombatConditionType type)
    {
        if (!_activeConditionsByCharacter.TryGetValue(target, out List<ActiveCondition> activeList) || activeList == null)
            return null;

        CombatConditionType normalized = ConditionRules.Normalize(type);
        for (int i = 0; i < activeList.Count; i++)
        {
            ActiveCondition candidate = activeList[i];
            if (candidate == null)
                continue;
            if (ConditionRules.Normalize(candidate.Type) == normalized)
                return candidate;
        }

        return null;
    }

    private void SyncAllTrackedCharacters()
    {
        List<CharacterController> characters = GetAllCharactersInternal();
        for (int i = 0; i < characters.Count; i++)
            SyncCharacter(characters[i]);
    }

    private void SyncCharacter(CharacterController character)
    {
        if (character == null)
            return;

        List<StatusEffect> effects = character.GetActiveConditions();
        if (effects == null)
            effects = new List<StatusEffect>();

        List<ActiveCondition> mapped = new List<ActiveCondition>(effects.Count);
        for (int i = 0; i < effects.Count; i++)
        {
            StatusEffect effect = effects[i];
            if (effect == null)
                continue;

            CharacterController source = ResolveSourceCharacter(effect);
            ActiveCondition activeCondition = ConvertToActiveCondition(effect, source);

            // Preserve turn-boundary metadata when re-syncing the same condition.
            ActiveCondition previous = FindSyncedCondition(_activeConditionsByCharacter, character, activeCondition.Type);
            if (previous != null)
            {
                activeCondition.Data = previous.Data;
                activeCondition.ExpiresAtEndOfTurn = previous.ExpiresAtEndOfTurn;
                activeCondition.ExpiresAtStartOfTurn = previous.ExpiresAtStartOfTurn;
                if (activeCondition.Source == null)
                    activeCondition.Source = previous.Source;
            }

            mapped.Add(activeCondition);
        }

        _activeConditionsByCharacter[character] = mapped;
    }

    private static ActiveCondition FindSyncedCondition(
        Dictionary<CharacterController, List<ActiveCondition>> store,
        CharacterController character,
        CombatConditionType type)
    {
        if (store == null || character == null)
            return null;

        if (!store.TryGetValue(character, out List<ActiveCondition> existing) || existing == null)
            return null;

        CombatConditionType normalized = ConditionRules.Normalize(type);
        for (int i = 0; i < existing.Count; i++)
        {
            ActiveCondition current = existing[i];
            if (current == null)
                continue;
            if (ConditionRules.Normalize(current.Type) == normalized)
                return current;
        }

        return null;
    }

    private ActiveCondition ConvertToActiveCondition(StatusEffect effect, CharacterController source)
    {
        return new ActiveCondition
        {
            Type = effect != null ? ConditionRules.Normalize(effect.Type) : CombatConditionType.None,
            RemainingRounds = effect != null ? effect.RemainingRounds : 0,
            Source = source,
            Data = null,
            ExpiresAtEndOfTurn = false,
            ExpiresAtStartOfTurn = false
        };
    }

    private CharacterController ResolveSourceCharacter(StatusEffect effect)
    {
        if (effect == null || string.IsNullOrWhiteSpace(effect.SourceName))
            return null;

        List<CharacterController> allCharacters = GetAllCharactersInternal();
        for (int i = 0; i < allCharacters.Count; i++)
        {
            CharacterController candidate = allCharacters[i];
            if (!IsValidCharacter(candidate))
                continue;

            if (candidate.Stats != null && string.Equals(candidate.Stats.CharacterName, effect.SourceName, StringComparison.Ordinal))
                return candidate;
        }

        return null;
    }

    private List<CharacterController> GetAllCharactersInternal()
    {
        if (_allCharactersProvider == null)
            return new List<CharacterController>();

        List<CharacterController> all = _allCharactersProvider.Invoke();
        return all ?? new List<CharacterController>();
    }

    private static bool IsValidCharacter(CharacterController character)
    {
        return character != null && character.Stats != null;
    }
}
