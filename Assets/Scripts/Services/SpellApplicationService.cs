using System;
using System.Collections.Generic;
using UnityEngine;
using DND35.Magic;

// ============================================================================
// D&D 3.5 Spell Application Service - Centralized spell effect management
// ============================================================================

/// <summary>
/// Centralized service for applying, tracking, and removing spell effects.
/// Handles buff/debuff application, duration tracking, and spell effect queries.
/// Extracted from GameManager following the EconomyService pattern.
///
/// PATTERN NOTES:
/// 1. MonoBehaviour attached to the GameManager GameObject.
/// 2. Initialize() called by GameManager after Awake().
/// 3. Provides utility methods for spell duration, effect queries, and application helpers.
/// 4. GameManager delegates spell effect queries here; actual spell resolution
///    remains in GameManager's PerformSpellCast/ApplySpellBuff (too tightly coupled
///    with per-spell logic to extract wholesale, but this service provides the
///    shared infrastructure they all use).
/// </summary>
public class SpellApplicationService : MonoBehaviour
{
    // ==================== STATE ====================

    // Cached references set during Initialize().
    private GameManager _gameManager;
    private Func<CombatUI> _combatUIProvider;
    private ConditionService _conditionService;

    private CombatUI CombatUI => _combatUIProvider?.Invoke();

    // ==================== LIFECYCLE ====================

    /// <summary>
    /// Called by GameManager after Awake to inject dependencies.
    /// </summary>
    public void Initialize(GameManager gameManager, Func<CombatUI> combatUIProvider, ConditionService conditionService)
    {
        _gameManager = gameManager;
        _combatUIProvider = combatUIProvider;
        _conditionService = conditionService;
        Debug.Log("[SpellApplicationService] Initialized");
    }

    // ==================== DURATION UTILITIES ====================

    /// <summary>
    /// Calculate spell duration in rounds based on spell data and caster level.
    /// Wraps ActiveSpellEffect.CalculateDurationRounds with a minimum of 1.
    /// </summary>
    /// <param name="spell">The spell being cast.</param>
    /// <param name="casterLevel">The caster's effective caster level.</param>
    /// <returns>Duration in rounds (minimum 1).</returns>
    public static int CalculateDurationRounds(SpellData spell, int casterLevel)
    {
        return Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
    }

    /// <summary>
    /// Get the effective caster level for a character.
    /// Falls back to 1 if stats are unavailable.
    /// </summary>
    /// <param name="caster">The casting character.</param>
    /// <returns>Effective caster level (minimum 1).</returns>
    public static int GetEffectiveCasterLevel(CharacterController caster)
    {
        if (caster == null || caster.Stats == null)
            return 1;
        return Mathf.Max(1, caster.Stats.GetCasterLevel());
    }

    /// <summary>
    /// Get the effective caster level for a specific spell, including domain bonuses.
    /// D&D 3.5e: Clerics with certain domains get +1 CL on matching descriptor spells.
    /// </summary>
    public static int GetEffectiveCasterLevel(CharacterController caster, SpellData spell)
    {
        if (caster == null || caster.Stats == null)
            return 1;
        return Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell));
    }

    // ==================== EFFECT APPLICATION HELPERS ====================

    /// <summary>
    /// Apply a condition to a target through the condition service (preferred)
    /// or directly to the character controller (fallback).
    /// </summary>
    /// <param name="target">Target character to receive the condition.</param>
    /// <param name="conditionType">The condition type to apply.</param>
    /// <param name="durationRounds">Duration in rounds.</param>
    /// <param name="source">The caster applying the condition.</param>
    /// <param name="spell">The spell causing the condition.</param>
    public void ApplyCondition(
        CharacterController target,
        CombatConditionType conditionType,
        int durationRounds,
        CharacterController source = null,
        SpellData spell = null)
    {
        if (target == null) return;

        string sourceNameOverride = spell?.Name;
        string sourceCategory = spell != null ? "Spell" : "Effect";
        string sourceId = spell?.SpellId;
        string fallbackSource = source?.Stats?.CharacterName ?? sourceNameOverride ?? "Unknown";

        if (_conditionService != null)
        {
            _conditionService.ApplyCondition(
                target,
                conditionType,
                durationRounds,
                source: source,
                sourceNameOverride: sourceNameOverride,
                sourceCategory: sourceCategory,
                sourceId: sourceId);
        }
        else
        {
            target.ApplyCondition(conditionType, durationRounds, fallbackSource);
        }
    }

    /// <summary>
    /// Add a spell effect to a target's StatusEffectManager.
    /// Returns the created ActiveSpellEffect, or null if it couldn't be applied.
    /// </summary>
    /// <param name="target">Target character.</param>
    /// <param name="spell">The spell to apply.</param>
    /// <param name="caster">The caster.</param>
    /// <param name="casterLevel">Effective caster level.</param>
    /// <returns>The applied ActiveSpellEffect, or null.</returns>
    public ActiveSpellEffect AddSpellEffect(
        CharacterController target,
        SpellData spell,
        CharacterController caster,
        int casterLevel)
    {
        if (target == null || spell == null)
            return null;

        StatusEffectManager statusMgr = target.GetComponent<StatusEffectManager>();
        if (statusMgr == null)
        {
            Debug.LogWarning($"[SpellApplicationService] No StatusEffectManager on {target.Stats?.CharacterName ?? target.name}");
            return null;
        }

        string sourceName = caster?.Stats?.CharacterName ?? spell.Name;
        ActiveSpellEffect effect = statusMgr.AddEffect(spell, sourceName, casterLevel);

        if (effect != null)
        {
            Debug.Log($"[SpellApplicationService] Applied {spell.Name} to {target.Stats?.CharacterName ?? target.name} | CL={casterLevel} | duration={effect.RemainingRounds} rounds");
        }

        return effect;
    }

    /// <summary>
    /// Remove a specific spell effect from a target by spell ID.
    /// </summary>
    /// <param name="target">Target character.</param>
    /// <param name="spellId">The spell ID to remove.</param>
    /// <returns>True if the effect was found and removed.</returns>
    public bool RemoveSpellEffect(CharacterController target, string spellId)
    {
        if (target == null || string.IsNullOrWhiteSpace(spellId))
            return false;

        StatusEffectManager statusMgr = target.GetComponent<StatusEffectManager>();
        if (statusMgr == null) return false;

        ActiveSpellEffect effectToRemove = null;
        foreach (var effect in statusMgr.ActiveEffects)
        {
            if (effect != null && string.Equals(effect.SpellId, spellId, StringComparison.Ordinal))
            {
                effectToRemove = effect;
                break;
            }
        }

        if (effectToRemove != null)
        {
            statusMgr.RemoveEffect(effectToRemove);
            Debug.Log($"[SpellApplicationService] Removed {spellId} from {target.Stats?.CharacterName ?? target.name}");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Remove all spell effects from a target. Used for Dispel Magic, death cleanup, etc.
    /// </summary>
    /// <param name="target">Target character.</param>
    public void RemoveAllSpellEffects(CharacterController target)
    {
        if (target == null) return;

        StatusEffectManager statusMgr = target.GetComponent<StatusEffectManager>();
        if (statusMgr == null) return;

        statusMgr.RemoveAllEffects();
        Debug.Log($"[SpellApplicationService] Cleared all spell effects from {target.Stats?.CharacterName ?? target.name}");
    }

    // ==================== EFFECT QUERIES ====================

    /// <summary>
    /// Check if a target has an active spell effect by spell ID.
    /// </summary>
    /// <param name="target">Target character.</param>
    /// <param name="spellId">Spell ID to check for.</param>
    /// <returns>True if the spell effect is active.</returns>
    public static bool HasActiveSpellEffect(CharacterController target, string spellId)
    {
        if (target == null || string.IsNullOrWhiteSpace(spellId))
            return false;

        StatusEffectManager statusMgr = target.GetComponent<StatusEffectManager>();
        if (statusMgr == null) return false;

        foreach (var effect in statusMgr.ActiveEffects)
        {
            if (effect != null && string.Equals(effect.SpellId, spellId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Get the remaining duration of a specific spell effect on a target.
    /// Returns -1 if the effect is not found.
    /// </summary>
    /// <param name="target">Target character.</param>
    /// <param name="spellId">Spell ID to look up.</param>
    /// <returns>Remaining rounds, or -1 if not found.</returns>
    public static int GetSpellEffectRemainingRounds(CharacterController target, string spellId)
    {
        if (target == null || string.IsNullOrWhiteSpace(spellId))
            return -1;

        StatusEffectManager statusMgr = target.GetComponent<StatusEffectManager>();
        if (statusMgr == null) return -1;

        foreach (var effect in statusMgr.ActiveEffects)
        {
            if (effect != null && string.Equals(effect.SpellId, spellId, StringComparison.Ordinal))
                return effect.RemainingRounds;
        }

        return -1;
    }

    /// <summary>
    /// Get a list of all active spell effect names on a target.
    /// Useful for tooltips and UI display.
    /// </summary>
    /// <param name="target">Target character.</param>
    /// <returns>List of active spell names.</returns>
    public static List<string> GetActiveSpellEffectNames(CharacterController target)
    {
        var names = new List<string>();
        if (target == null) return names;

        StatusEffectManager statusMgr = target.GetComponent<StatusEffectManager>();
        if (statusMgr == null) return names;

        foreach (var effect in statusMgr.ActiveEffects)
        {
            if (effect != null && !string.IsNullOrWhiteSpace(effect.SpellName))
                names.Add(effect.SpellName);
        }

        return names;
    }

    // ==================== DURATION TRACKING ====================

    /// <summary>
    /// Tick all spell effect durations on a specific character.
    /// Called during end-of-round processing.
    /// Returns list of expired spell IDs.
    /// </summary>
    /// <param name="character">The character to tick effects for.</param>
    /// <returns>List of expired spell IDs.</returns>
    public static List<string> TickSpellEffectDurations(CharacterController character)
    {
        var expired = new List<string>();
        if (character == null) return expired;

        StatusEffectManager statusMgr = character.GetComponent<StatusEffectManager>();
        if (statusMgr == null) return expired;

        // StatusEffectManager.TickDurations handles this internally
        // but we return the list for external tracking/logging
        foreach (var effect in statusMgr.ActiveEffects)
        {
            if (effect != null && effect.RemainingRounds <= 0)
                expired.Add(effect.SpellId);
        }

        return expired;
    }

    // ==================== TEMPORARY ITEM EFFECTS ====================

    /// <summary>
    /// Clear all temporary spell effects from an item (e.g. Magic Weapon, Keen Edge).
    /// Called between encounters or when effects expire.
    /// </summary>
    /// <param name="item">The item to clear effects from.</param>
    public static void ClearTemporaryItemSpellEffects(ItemData item)
    {
        if (item == null || item.ActiveSpellEffects == null || item.ActiveSpellEffects.Count == 0)
            return;

        item.ActiveSpellEffects.Clear();
    }
}
