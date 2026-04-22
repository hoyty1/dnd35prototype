using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Runtime manager for D&D 3.5e combat conditions (non-spell status states).
///
/// Design goals:
/// - Keep CharacterStats.ActiveConditions as the source list for backward compatibility.
/// - Enforce normalization + stacking behavior in one place.
/// - Provide extension hooks for condition-specific behavior (rules-as-written stubs).
/// </summary>
public class ConditionManager : MonoBehaviour
{
    private CharacterStats _stats;

    public void Init(CharacterStats stats)
    {
        _stats = stats;
        if (_stats != null && _stats.ActiveConditions == null)
            _stats.ActiveConditions = new List<StatusEffect>();
    }

    public IReadOnlyList<StatusEffect> ActiveConditions
        => _stats != null && _stats.ActiveConditions != null
            ? _stats.ActiveConditions
            : new List<StatusEffect>();

    public bool HasCondition(CombatConditionType type)
    {
        if (_stats == null || _stats.ActiveConditions == null) return false;
        CombatConditionType normalized = ConditionRules.Normalize(type);
        return _stats.ActiveConditions.Any(c => ConditionRules.Normalize(c.Type) == normalized);
    }

    public void ApplyCondition(CombatConditionType type, int rounds, string sourceName)
    {
        if (_stats == null) return;
        if (_stats.ActiveConditions == null)
            _stats.ActiveConditions = new List<StatusEffect>();

        CombatConditionType normalized = ConditionRules.Normalize(type);
        ConditionDefinition def = ConditionRules.GetDefinition(normalized);

        if (def.StackingRule == ConditionStackingRule.StackBySource)
        {
            var existingSameSource = _stats.ActiveConditions.FirstOrDefault(c =>
                ConditionRules.Normalize(c.Type) == normalized && c.SourceName == sourceName);

            if (existingSameSource == null)
            {
                var created = new StatusEffect(normalized, sourceName, rounds);
                _stats.ActiveConditions.Add(created);
                OnConditionApplied(created);
                return;
            }

            RefreshDuration(existingSameSource, rounds);
            return;
        }

        // Refresh-style default: one active instance per normalized condition type.
        var existing = _stats.ActiveConditions.FirstOrDefault(c => ConditionRules.Normalize(c.Type) == normalized);
        if (existing == null)
        {
            var created = new StatusEffect(normalized, sourceName, rounds);
            _stats.ActiveConditions.Add(created);
            OnConditionApplied(created);
            return;
        }

        RefreshDuration(existing, rounds);
    }

    public bool RemoveCondition(CombatConditionType type)
    {
        if (_stats == null || _stats.ActiveConditions == null) return false;

        CombatConditionType normalized = ConditionRules.Normalize(type);
        int idx = _stats.ActiveConditions.FindIndex(c => ConditionRules.Normalize(c.Type) == normalized);
        if (idx < 0) return false;

        StatusEffect removed = _stats.ActiveConditions[idx];
        _stats.ActiveConditions.RemoveAt(idx);
        OnConditionRemoved(removed);
        return true;
    }

    public List<StatusEffect> TickConditions()
    {
        var expired = new List<StatusEffect>();
        if (_stats == null || _stats.ActiveConditions == null) return expired;

        for (int i = _stats.ActiveConditions.Count - 1; i >= 0; i--)
        {
            var cond = _stats.ActiveConditions[i];
            if (!cond.Tick()) continue;

            expired.Add(cond);
            _stats.ActiveConditions.RemoveAt(i);
            OnConditionRemoved(cond);
        }

        return expired;
    }

    public string GetConditionSummary()
    {
        if (_stats == null || _stats.ActiveConditions == null || _stats.ActiveConditions.Count == 0)
            return string.Empty;

        var parts = new List<string>();
        foreach (var c in _stats.ActiveConditions)
        {
            var def = ConditionRules.GetDefinition(c.Type);
            string color = GetConditionColor(c.Type);
            parts.Add($"<color={color}>{def.DisplayName}</color>({c.GetDurationLabel()})");
        }

        return string.Join(" ", parts);
    }

    private static string GetConditionColor(CombatConditionType type)
    {
        switch (ConditionRules.Normalize(type))
        {
            case CombatConditionType.Grappled: return "#FFAA44";
            case CombatConditionType.Prone: return "#FF7777";
            case CombatConditionType.Feinted: return "#66CCFF";
            case CombatConditionType.ChargePenalty: return "#FF9966";
            case CombatConditionType.Flanked: return "#FFB347";
            case CombatConditionType.Invisible: return "#88CCFF";
            case CombatConditionType.Disabled: return "#FFD966";
            case CombatConditionType.Staggered: return "#FFCC88";
            case CombatConditionType.Dying: return "#FF6666";
            case CombatConditionType.Stable: return "#FFB366";
            case CombatConditionType.Unconscious: return "#99AACC";
            case CombatConditionType.Turned: return "#FFF2A8";
            default: return "#FFFF66";
        }
    }

    private static void RefreshDuration(StatusEffect existing, int rounds)
    {
        if (existing.RemainingRounds < 0) return;
        if (rounds < 0 || rounds > existing.RemainingRounds)
            existing.RemainingRounds = rounds;
    }

    private void OnConditionApplied(StatusEffect condition)
    {
        if (_stats == null || condition == null) return;

        // ===== RULE STUBS =====
        // These hooks are intentionally lightweight: we centralize where richer per-condition
        // behavior will be added without changing the rest of the combat pipeline.
        switch (ConditionRules.Normalize(condition.Type))
        {
            case CombatConditionType.Stunned:
            case CombatConditionType.Paralyzed:
            case CombatConditionType.Helpless:
            case CombatConditionType.Dazed:
            case CombatConditionType.Nauseated:
            case CombatConditionType.Panicked:
                Debug.Log($"[ConditionManager] {_stats.CharacterName}: {condition.Type} applied (action-limiting stub hook)");
                break;
            case CombatConditionType.Invisible:
                Debug.Log($"[ConditionManager] {_stats.CharacterName}: Invisible applied (visibility/combat advantage stub hook)");
                break;
        }
    }

    private void OnConditionRemoved(StatusEffect condition)
    {
        if (_stats == null || condition == null) return;

        switch (ConditionRules.Normalize(condition.Type))
        {
            case CombatConditionType.Stunned:
            case CombatConditionType.Paralyzed:
            case CombatConditionType.Helpless:
            case CombatConditionType.Dazed:
            case CombatConditionType.Nauseated:
            case CombatConditionType.Panicked:
            case CombatConditionType.Invisible:
                Debug.Log($"[ConditionManager] {_stats.CharacterName}: {condition.Type} removed (stub hook)");
                break;
        }
    }
}
