// ============================================================================
// MeleeReactionService.cs — Centralized registry for melee reaction effects
//
// Maintains a list of all active IMeleeReactionEffect instances and provides
// a single entry point for triggering reactions when a melee attack hits.
//
// Effects register themselves on creation and unregister on destruction.
// This replaces all hardcoded Fire Shield retribution calls and is extensible
// for future reactive effects (Blade Barrier, Thorns, damage auras, etc.).
//
// PATTERN: Identical to LineOfEffectService / ILineOfEffectBlocker.
//
// USAGE IN ATTACK RESOLUTION:
//   // Single call replaces all hardcoded Fire Shield checks:
//   if (result.Hit && !result.IsRangedAttack)
//       MeleeReactionService.TriggerReactions(attacker, defender, result);
//
// ADDING NEW EFFECTS:
//   1. Implement IMeleeReactionEffect on your effect class
//   2. Register in Start()/OnActivate: MeleeReactionService.Register(this)
//   3. Unregister in OnDestroy()/OnExpire: MeleeReactionService.Unregister(this)
//   4. Done — your effect triggers automatically on all melee hits
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Centralized service for melee attack reaction effects.
/// All <see cref="IMeleeReactionEffect"/> implementations register here
/// so that attack resolution code can trigger ALL reactions with a single call
/// instead of checking each effect type individually.
/// </summary>
public static class MeleeReactionService
{
    private static readonly List<IMeleeReactionEffect> _effects = new List<IMeleeReactionEffect>();

    // ═══════════════════════════════════════════════════
    // REGISTRATION
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Register a reaction effect. Call this when the effect becomes active
    /// (e.g., when Fire Shield is cast, when a Thorns aura activates).
    /// </summary>
    public static void Register(IMeleeReactionEffect effect)
    {
        if (effect == null) return;
        if (!_effects.Contains(effect))
        {
            _effects.Add(effect);
            Debug.Log($"[MeleeReactionService] Registered: {effect.EffectName} (total: {_effects.Count})");
        }
    }

    /// <summary>
    /// Unregister a reaction effect. Call this when the effect expires or is destroyed
    /// (e.g., Fire Shield duration ends, creature dies, spell is dispelled).
    /// </summary>
    public static void Unregister(IMeleeReactionEffect effect)
    {
        if (effect == null) return;
        if (_effects.Remove(effect))
        {
            Debug.Log($"[MeleeReactionService] Unregistered: {effect.EffectName} (total: {_effects.Count})");
        }
    }

    /// <summary>
    /// Remove all registered effects. Useful for scene cleanup / combat reset.
    /// </summary>
    public static void ClearAll()
    {
        _effects.Clear();
        Debug.Log("[MeleeReactionService] All effects cleared.");
    }

    // ═══════════════════════════════════════════════════
    // TRIGGERING
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Trigger all registered melee reaction effects for a successful melee hit.
    /// Call this ONCE per hit in attack resolution — it replaces all individual
    /// effect checks (Fire Shield, Thorns, Blade Barrier, etc.).
    ///
    /// Each registered effect decides internally whether it applies to the
    /// given defender and executes its reaction if so.
    /// </summary>
    /// <param name="attacker">The character who made the melee attack.</param>
    /// <param name="defender">The character who was hit by the melee attack.</param>
    /// <param name="attackResult">The combat result of the individual hit.</param>
    public static void TriggerReactions(CharacterController attacker, CharacterController defender, CombatResult attackResult)
    {
        if (attacker == null || defender == null) return;
        if (_effects.Count == 0) return;

        for (int i = 0; i < _effects.Count; i++)
        {
            // Defend against destroyed Unity objects still in the list
            if (_effects[i] == null || (_effects[i] is Object obj && obj == null))
            {
                _effects.RemoveAt(i);
                i--;
                continue;
            }

            // Each effect decides if it applies to this defender
            if (_effects[i].IsActiveOn(defender))
            {
                _effects[i].OnMeleeAttackHit(attacker, defender, attackResult);
            }

            // If the attacker died from a reaction (e.g., Fire Shield killed them),
            // stop processing further reactions
            if (attacker == null || attacker.Stats == null || attacker.Stats.IsDead)
                break;
        }
    }

    // ═══════════════════════════════════════════════════
    // QUERIES
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Returns true if any registered effect is active on the given character.
    /// Useful for UI indicators, AI decisions, etc.
    /// </summary>
    public static bool HasAnyReactionEffect(CharacterController character)
    {
        if (character == null) return false;
        for (int i = 0; i < _effects.Count; i++)
        {
            if (_effects[i] == null || (_effects[i] is Object obj && obj == null))
            {
                _effects.RemoveAt(i);
                i--;
                continue;
            }
            if (_effects[i].IsActiveOn(character))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns the current number of registered effects.
    /// Useful for debugging and tests.
    /// </summary>
    public static int EffectCount => _effects.Count;

    /// <summary>
    /// Returns true if there are any active effects registered.
    /// Quick early-out check for performance.
    /// </summary>
    public static bool HasEffects => _effects.Count > 0;
}
