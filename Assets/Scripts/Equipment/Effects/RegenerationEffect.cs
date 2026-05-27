using UnityEngine;

// ════════════════════════════════════════════════════════════════════════════
//  Regeneration Effect — D&D 3.5e Sprint 3 Ring of Regeneration System
//
//  DMG p.232: "This white gold ring continually allows a living wearer to
//  heal 1 point of damage per level every hour until the ring is removed
//  or the wearer dies. It will even regenerate the loss of a limb or organ,
//  the restoration occurring in 1d7 days after the ring is activated."
//
//  Implemented mechanics:
//  1. Hourly healing: heal 1 HP per character level per hour
//     (in prototype: triggered per-rest since we don't track real-time hours)
//  2. Death prevention: character cannot die from HP damage alone while
//     wearing the ring — stabilizes at negative HP instead of dying at -10.
//     Exception: still dies from instant-death effects, disintegrate, etc.
//  3. Limb regeneration: flavor text only in prototype (no limb loss system)
//
//  Integration: Added as MonoBehaviour component on equip, removed on unequip.
//  Death prevention check is in CharacterStats.IsDead override logic.
//  Hourly healing is processed during rest in RingActivationManager.OnRest().
// ════════════════════════════════════════════════════════════════════════════

public class RegenerationEffect : MonoBehaviour
{
    /// <summary>Whether regeneration is currently active (ring equipped).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Reference to the character controller for healing operations.</summary>
    private CharacterController _owner;

    void Awake()
    {
        _owner = GetComponent<CharacterController>();
    }

    /// <summary>
    /// Apply hourly regeneration healing. Called during rest processing.
    /// Heals 1 HP per character level.
    /// </summary>
    public int ApplyHourlyRegeneration()
    {
        if (!IsActive) return 0;
        if (_owner == null) _owner = GetComponent<CharacterController>();
        if (_owner == null || _owner.Stats == null) return 0;
        if (_owner.Stats.IsDead) return 0;

        // Heal 1 HP per character level
        int charLevel = Mathf.Max(1, _owner.Stats.Level);
        int healAmount = charLevel;

        // Only heal if damaged
        if (_owner.Stats.CurrentHP >= _owner.Stats.TotalMaxHP) return 0;

        int hpBefore = _owner.Stats.CurrentHP;
        _owner.Stats.CurrentHP = Mathf.Min(_owner.Stats.TotalMaxHP, _owner.Stats.CurrentHP + healAmount);
        int actualHealed = _owner.Stats.CurrentHP - hpBefore;

        if (actualHealed > 0)
        {
            Debug.Log($"[Regeneration] {_owner.Stats.CharacterName} regenerates {actualHealed} HP (level {charLevel}).");
        }

        return actualHealed;
    }

    /// <summary>
    /// Check if regeneration should prevent death from HP damage.
    /// Returns true if the character should stabilize instead of dying.
    /// D&D 3.5e: Regeneration prevents death from HP damage.
    /// The character continues to take damage normally but cannot die from it.
    /// They stabilize at negative HP and continue regenerating.
    /// </summary>
    public bool ShouldPreventHPDeath()
    {
        if (!IsActive) return false;
        if (_owner == null) _owner = GetComponent<CharacterController>();
        if (_owner == null || _owner.Stats == null) return false;

        // Only prevent death from HP damage (not from death effects, disintegrate, etc.)
        // The check is: if CurrentHP would go to <= -10, prevent it
        return true;
    }

    /// <summary>
    /// Clamp HP to prevent death (called after damage is applied).
    /// Sets HP to max of -9 (just above death threshold).
    /// </summary>
    public void ClampHPAboveDeath()
    {
        if (_owner == null) _owner = GetComponent<CharacterController>();
        if (_owner == null || _owner.Stats == null) return;

        // D&D 3.5e: death at -10 HP. Regeneration keeps alive at -9 minimum.
        if (_owner.Stats.CurrentHP <= -10)
        {
            _owner.Stats.CurrentHP = -9;
            Debug.Log($"[Regeneration] {_owner.Stats.CharacterName}'s Ring of Regeneration stabilizes at {_owner.Stats.CurrentHP} HP!");

            if (GameManager.Instance != null)
                GameManager.Instance.CombatUI?.ShowCombatLog(CombatLogHelper.Info("", $"💍 {_owner.Stats.CharacterName}'s Ring of Regeneration prevents death! Stabilized at {_owner.Stats.CurrentHP} HP."));
        }
    }

    /// <summary>
    /// Static helper to check if a character has an active regeneration effect.
    /// </summary>
    public static bool HasActiveRegeneration(CharacterController character)
    {
        if (character == null) return false;
        var regen = character.GetComponent<RegenerationEffect>();
        return regen != null && regen.IsActive;
    }

    /// <summary>
    /// Static helper to apply death prevention after damage.
    /// Call this after TakeDamage() to check if regeneration saves the character.
    /// </summary>
    public static void CheckDeathPrevention(CharacterController character)
    {
        if (character == null) return;
        var regen = character.GetComponent<RegenerationEffect>();
        if (regen != null && regen.IsActive && regen.ShouldPreventHPDeath())
        {
            regen.ClampHPAboveDeath();
        }
    }
}
