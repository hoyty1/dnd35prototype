// ============================================================================
// IMeleeReactionEffect.cs — Interface for reactive effects triggered by melee hits
//
// Any effect that reacts to a melee attack hitting a character should implement
// this interface and register with MeleeReactionService.
//
// Examples: Fire Shield, Blade Barrier, Thorns, damage auras, monster "when hit"
// abilities, magic item retribution effects, etc.
//
// PATTERN: Same as ILineOfEffectBlocker / LineOfEffectService.
// To add a new reactive effect:
//   1. Create a class that implements IMeleeReactionEffect
//   2. Call MeleeReactionService.Register(this) on activation
//   3. Call MeleeReactionService.Unregister(this) on expiration/destruction
//   4. Implement OnMeleeAttackHit() with your effect's logic
//   5. That's it — the combat system will call your effect automatically
// ============================================================================

/// <summary>
/// Interface for any effect that triggers when a character is hit by a melee attack.
/// Implementations register with <see cref="MeleeReactionService"/> and are called
/// automatically by the combat system — no need to modify attack resolution code.
/// </summary>
public interface IMeleeReactionEffect
{
    /// <summary>
    /// Called when a melee attack successfully hits the defender.
    /// The implementation should check whether this effect applies to the given
    /// defender and execute the reaction (damage, counter-attack, condition, etc.).
    /// </summary>
    /// <param name="attacker">The character who made the melee attack.</param>
    /// <param name="defender">The character who was hit.</param>
    /// <param name="attackResult">The combat result of the hit (for checking crit, damage type, etc.).</param>
    void OnMeleeAttackHit(CharacterController attacker, CharacterController defender, CombatResult attackResult);

    /// <summary>
    /// Returns true if this effect is currently active and attached to the given character.
    /// Used for UI display, debugging, and pre-flight checks.
    /// </summary>
    bool IsActiveOn(CharacterController character);

    /// <summary>
    /// Display name for logging and debugging (e.g., "Fire Shield (Warm)", "Thorns Aura").
    /// </summary>
    string EffectName { get; }
}
