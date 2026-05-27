using UnityEngine;
using DND35e.Identifiers;

// ============================================================================
// SpectralHandEffectData.cs — Spectral Hand spell tracking
//
// D&D 3.5e PHB p.282:
//   Necromancy. Sorcerer/Wizard 2. V, S. 1 standard action.
//   Range: Medium (100 ft. + 10 ft./level). Duration: 1 min./level (D).
//   No save, no SR.
//
//   Creates a ghostly hand from the caster's life force to deliver touch
//   spells of 4th level or lower at range.
//
//   Caster loses 1d4 HP on casting (returned when spell ends, but NOT
//   if the hand is destroyed). The hand has HP equal to what was lost.
//
//   Hand AC: 22 + caster's Int modifier (base 10 + 8 size + 4 natural).
//   +2 bonus on melee touch attack rolls when delivering spells.
//   Incorporeal, improved evasion, uses caster's saves. Cannot flank.
// ============================================================================

/// <summary>
/// Runtime data for an active Spectral Hand effect on a character.
/// Tracks the hand's HP, AC, caster HP loss, and delivery state.
/// </summary>
[System.Serializable]
public class SpectralHandEffectData
{
    // ======================== CORE STATE ========================

    /// <summary>HP the caster lost when casting (1-4). Hand HP equals this value.</summary>
    public int CasterHPLost;

    /// <summary>Current HP of the spectral hand (starts equal to CasterHPLost).</summary>
    public int CurrentHandHP;

    /// <summary>Maximum HP of the spectral hand (same as CasterHPLost).</summary>
    public int MaxHandHP;

    /// <summary>The hand's Armor Class (22 + caster Int modifier, minimum 22).</summary>
    public int HandAC;

    /// <summary>Whether the hand is currently active.</summary>
    public bool IsActive;

    /// <summary>Whether the hand has been destroyed (reduced to 0 HP).</summary>
    public bool IsDestroyed;

    /// <summary>Whether the hand is currently delivering a touch spell.</summary>
    public bool IsDeliveringSpell;

    /// <summary>Remaining duration in combat rounds. -1 = permanent.</summary>
    public int DurationRemainingRounds;

    /// <summary>Caster level at time of casting.</summary>
    public int CasterLevel;

    /// <summary>The touch attack bonus granted by the hand (+2 per PHB).</summary>
    public const int TOUCH_ATTACK_BONUS = 2;

    /// <summary>Base AC of the hand before Int modifier (10 + 8 size + 4 natural).</summary>
    public const int BASE_HAND_AC = 22;

    /// <summary>Maximum spell level that can be delivered through the hand.</summary>
    public const int MAX_DELIVERABLE_SPELL_LEVEL = 4;

    // ======================== SOURCE TRACKING ========================

    /// <summary>Runtime reference to the caster (not serialized).</summary>
    [System.NonSerialized] public CharacterController Caster;

    /// <summary>Serializable caster name for persistence.</summary>
    public string CasterName;

    // ======================== METHODS ========================

    /// <summary>
    /// Sets the caster reference and serializable name.
    /// </summary>
    public void SetCaster(CharacterController caster)
    {
        Caster = caster;
        CasterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : string.Empty;
    }

    /// <summary>
    /// Returns true if the hand is active and not destroyed.
    /// </summary>
    public bool IsHandAvailable => IsActive && !IsDestroyed && CurrentHandHP > 0;

    /// <summary>
    /// Returns the +2 touch attack bonus if hand is available, 0 otherwise.
    /// </summary>
    public int GetTouchAttackBonus()
    {
        return IsHandAvailable ? TOUCH_ATTACK_BONUS : 0;
    }

    /// <summary>
    /// Check whether a spell can be delivered through the spectral hand.
    /// Must be a touch-range spell of 4th level or lower.
    /// </summary>
    public bool CanDeliverSpell(SpellData spell)
    {
        if (!IsHandAvailable) return false;
        if (spell == null) return false;
        if (spell.SpellLevel > MAX_DELIVERABLE_SPELL_LEVEL) return false;
        // Must be a touch spell (uses IsTouchSpell() which checks IsTouch, IsMeleeTouch, range, etc.)
        if (!spell.IsTouchSpell()) return false;
        return true;
    }

    /// <summary>
    /// Apply damage to the hand. Returns true if the hand is destroyed.
    /// </summary>
    public bool TakeDamage(int damage)
    {
        if (!IsActive || IsDestroyed || damage <= 0) return false;

        CurrentHandHP = Mathf.Max(0, CurrentHandHP - damage);
        Debug.Log($"[SpectralHand] Hand takes {damage} damage. HP: {CurrentHandHP}/{MaxHandHP}");

        if (CurrentHandHP <= 0)
        {
            DestroyHand();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Destroy the hand. Caster does NOT regain the lost HP.
    /// </summary>
    public void DestroyHand()
    {
        IsDestroyed = true;
        IsActive = false;
        CurrentHandHP = 0;
        Debug.Log($"[SpectralHand] Hand destroyed! {CasterName} permanently loses {CasterHPLost} HP.");
    }

    /// <summary>
    /// End the spell normally (duration expired or dismissed).
    /// Caster regains the lost HP.
    /// Returns the amount of HP the caster should regain.
    /// </summary>
    public int EndSpell(string reason)
    {
        if (!IsActive && !IsDestroyed)
            return 0;

        int hpToRestore = 0;
        if (!IsDestroyed)
        {
            // Caster regains HP when spell ends normally
            hpToRestore = CasterHPLost;
            Debug.Log($"[SpectralHand] Spell ended ({reason}). {CasterName} regains {hpToRestore} HP.");
        }
        else
        {
            Debug.Log($"[SpectralHand] Spell ended ({reason}). Hand was destroyed — no HP restored.");
        }

        IsActive = false;
        CurrentHandHP = 0;
        return hpToRestore;
    }

    /// <summary>
    /// Mark the hand as delivering a spell (for state tracking).
    /// </summary>
    public void BeginDelivery()
    {
        IsDeliveringSpell = true;
    }

    /// <summary>
    /// Mark the hand as done delivering and returned to caster.
    /// </summary>
    public void EndDelivery()
    {
        IsDeliveringSpell = false;
    }

    // ======================== STATIC HELPERS ========================

    /// <summary>
    /// Calculate the hand's AC: 22 + caster Int modifier, minimum 22.
    /// PHB p.282: "AC of at least 22 (+8 size, +4 natural armor)
    /// and the caster's Intelligence modifier applies to the hand's AC
    /// as if it were the hand's Dexterity modifier."
    /// </summary>
    public static int CalculateHandAC(int casterIntModifier)
    {
        return Mathf.Max(BASE_HAND_AC, BASE_HAND_AC + casterIntModifier);
    }

    /// <summary>
    /// Roll 1d4 for the HP cost/hand HP. Returns a value from 1-4.
    /// </summary>
    public static int RollHandHP()
    {
        return DiceRoller.D4(); // 1d4
    }

    // ======================== FACTORY METHODS ========================

    /// <summary>
    /// Factory: Creates a Spectral Hand effect with randomly rolled HP.
    /// PHB p.282: Caster loses 1d4 HP, hand gets that many HP.
    /// Duration: 1 min/level (10 rounds/level).
    /// </summary>
    public static SpectralHandEffectData Create(int casterLevel, int casterIntModifier, CharacterController caster)
    {
        int hpRoll = RollHandHP();
        return CreateWithHP(hpRoll, casterLevel, casterIntModifier, caster);
    }

    /// <summary>
    /// Factory: Creates a Spectral Hand effect with a specific HP value (for testing).
    /// </summary>
    public static SpectralHandEffectData CreateWithHP(int handHP, int casterLevel, int casterIntModifier, CharacterController caster)
    {
        int clampedHP = Mathf.Clamp(handHP, 1, 4);
        int durationRounds = casterLevel * 10; // 1 min/level = 10 rounds/level

        var data = new SpectralHandEffectData
        {
            CasterHPLost = clampedHP,
            CurrentHandHP = clampedHP,
            MaxHandHP = clampedHP,
            HandAC = CalculateHandAC(casterIntModifier),
            IsActive = true,
            IsDestroyed = false,
            IsDeliveringSpell = false,
            DurationRemainingRounds = durationRounds,
            CasterLevel = casterLevel
        };
        data.SetCaster(caster);
        return data;
    }
}
