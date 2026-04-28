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
    private SpriteRenderer _spriteRenderer;
    private Color _baseColor = Color.white;
    private bool _capturedBaseColor;

    public void Init(CharacterStats stats)
    {
        _stats = stats;
        if (_stats != null && _stats.ActiveConditions == null)
            _stats.ActiveConditions = new List<StatusEffect>();

        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null)
        {
            _baseColor = _spriteRenderer.color;
            _capturedBaseColor = true;
        }
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
            if (normalized == CombatConditionType.EnergyDrained)
            {
                var created = new StatusEffect(normalized, sourceName, rounds);
                _stats.ActiveConditions.Add(created);
                OnConditionApplied(created);
            }
            else
            {
                var existingSameSource = _stats.ActiveConditions.FirstOrDefault(c =>
                    ConditionRules.Normalize(c.Type) == normalized && c.SourceName == sourceName);

                if (existingSameSource == null)
                {
                    var created = new StatusEffect(normalized, sourceName, rounds);
                    _stats.ActiveConditions.Add(created);
                    OnConditionApplied(created);
                }
                else
                {
                    RefreshDuration(existingSameSource, rounds);
                }
            }

            EnsureLinkedParalyzedHelpless(rounds, sourceName, normalized);
            _stats?.RefreshNegativeLevelState();
            return;
        }

        // Refresh-style default: one active instance per normalized condition type.
        var existing = _stats.ActiveConditions.FirstOrDefault(c => ConditionRules.Normalize(c.Type) == normalized);
        if (existing == null)
        {
            var created = new StatusEffect(normalized, sourceName, rounds);
            _stats.ActiveConditions.Add(created);
            OnConditionApplied(created);
        }
        else
        {
            existing.SourceName = sourceName;
            RefreshDuration(existing, rounds);
        }

        EnsureLinkedParalyzedHelpless(rounds, sourceName, normalized);
        _stats?.RefreshNegativeLevelState();
    }

    private void EnsureLinkedParalyzedHelpless(int rounds, string sourceName, CombatConditionType normalized)
    {
        if (normalized != CombatConditionType.Paralyzed)
            return;

        // Paralyzed creatures are helpless by rule. Ensure helpless is present and at least as long.
        var helpless = _stats.ActiveConditions.FirstOrDefault(c => ConditionRules.Normalize(c.Type) == CombatConditionType.Helpless);
        if (helpless == null)
        {
            var linkedHelpless = new StatusEffect(CombatConditionType.Helpless, sourceName, rounds);
            _stats.ActiveConditions.Add(linkedHelpless);
            OnConditionApplied(linkedHelpless);
            return;
        }

        helpless.SourceName = sourceName;
        RefreshDuration(helpless, rounds);
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

        if (normalized == CombatConditionType.Paralyzed)
            RemoveLinkedHelplessIfNoOtherDriver(removed != null ? removed.SourceName : null);

        _stats?.RefreshNegativeLevelState();
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

            CombatConditionType normalized = ConditionRules.Normalize(cond.Type);
            expired.Add(cond);
            _stats.ActiveConditions.RemoveAt(i);
            OnConditionRemoved(cond);

            if (normalized == CombatConditionType.Paralyzed)
                RemoveLinkedHelplessIfNoOtherDriver(cond != null ? cond.SourceName : null);
        }

        _stats?.RefreshNegativeLevelState();
        return expired;
    }

    private void RemoveLinkedHelplessIfNoOtherDriver(string paralyzedSourceName)
    {
        if (_stats == null || _stats.ActiveConditions == null)
            return;

        bool hasOtherHelplessDriver = _stats.ActiveConditions.Any(c =>
        {
            if (c == null)
                return false;

            CombatConditionType normalized = ConditionRules.Normalize(c.Type);
            return normalized != CombatConditionType.Helpless
                && normalized != CombatConditionType.Paralyzed
                && ConditionRules.IsHelplessLike(normalized);
        });

        if (hasOtherHelplessDriver)
            return;

        int helplessIndex = _stats.ActiveConditions.FindIndex(c => ConditionRules.Normalize(c.Type) == CombatConditionType.Helpless);
        if (helplessIndex < 0)
            return;

        StatusEffect helpless = _stats.ActiveConditions[helplessIndex];
        bool linkedSource = string.Equals(helpless != null ? helpless.SourceName : null, paralyzedSourceName, System.StringComparison.Ordinal);
        if (!linkedSource && !string.IsNullOrEmpty(paralyzedSourceName))
            return;

        _stats.ActiveConditions.RemoveAt(helplessIndex);
        OnConditionRemoved(helpless);
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
            {
                CharacterController owner = GetComponent<CharacterController>();
                if (owner != null)
                {
                    int dropped = owner.DropHeldItemsDueToCondition("stunned");
                    if (dropped > 0)
                        Debug.Log($"[ConditionManager] {_stats.CharacterName}: dropped {dropped} held item(s) from Stunned.");
                }

                Debug.Log($"[ConditionManager] {_stats.CharacterName}: {condition.Type} applied (action-limiting stub hook)");
                break;
            }
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
            case CombatConditionType.Petrified:
                ApplyPetrifiedVisual();
                Debug.Log($"[ConditionManager] {_stats.CharacterName}: Petrified applied (stone visual + hardness).");
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
            case CombatConditionType.Petrified:
                RemovePetrifiedVisual();
                Debug.Log($"[ConditionManager] {_stats.CharacterName}: Petrified removed (stone visual reset).");
                break;
        }
    }

    private void ApplyPetrifiedVisual()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_spriteRenderer == null)
            return;

        if (!_capturedBaseColor)
        {
            _baseColor = _spriteRenderer.color;
            _capturedBaseColor = true;
        }

        _spriteRenderer.color = new Color(0.62f, 0.62f, 0.62f, _baseColor.a);
    }

    private void RemovePetrifiedVisual()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_spriteRenderer == null || !_capturedBaseColor)
            return;

        _spriteRenderer.color = _baseColor;
    }
}
