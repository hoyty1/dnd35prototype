using UnityEngine;
using DND35e.Identifiers;

// ============================================================================
// GhoulTouchEffectData.cs — Paralysis + stench tracking for Ghoul Touch spell
//
// D&D 3.5e PHB p.235:
//   Ghoul Touch imbues the caster with negative energy. On a successful melee
//   touch attack against a living humanoid, the target must make a Fortitude
//   save or become paralyzed for 1d6+2 rounds. A paralyzed target exudes a
//   carrion stench in a 10-ft radius that sickens living creatures (Fort negates).
//
//   Key rules:
//   - Target must be a living humanoid
//   - Paralysis: Str/Dex effectively 0, helpless, cannot move/speak/act physically
//   - No recurring saves (unlike Hold Person)
//   - Stench: 10-ft radius, sickens living creatures (except caster), poison effect
//   - Sickened: -2 to attacks, weapon damage, saves, skill checks, ability checks
// ============================================================================

/// <summary>
/// Runtime data for an active Ghoul Touch paralysis + stench effect.
/// Tracks paralysis duration, stench aura, and caster reference.
/// </summary>
[System.Serializable]
public class GhoulTouchEffectData
{
    // ======================== CORE STATE ========================

    /// <summary>Total paralysis duration in combat rounds (rolled 1d6+2).</summary>
    public int ParalysisDurationRounds;

    /// <summary>Remaining paralysis duration in combat rounds.</summary>
    public int ParalysisRemainingRounds;

    /// <summary>Whether the paralysis effect is currently active.</summary>
    public bool IsParalysisActive;

    /// <summary>Whether the stench aura is currently active (only while paralyzed).</summary>
    public bool IsStenchActive;

    /// <summary>Stench aura radius in feet (10 ft per PHB).</summary>
    public int StenchRadiusFeet = 10;

    /// <summary>Stench aura radius in grid squares (2 squares = 10 ft).</summary>
    public int StenchRadiusSquares = 2;

    // ======================== SOURCE TRACKING ========================

    /// <summary>The spell ID that created this effect.</summary>
    public string SourceSpellId;

    /// <summary>Human-readable name of the source spell.</summary>
    public string SourceName;

    /// <summary>Name of the caster who applied this effect.</summary>
    public string CasterName;

    /// <summary>Runtime reference to the caster (not serialized). Caster is exempt from stench.</summary>
    [System.NonSerialized] public CharacterController Caster;

    /// <summary>Runtime reference to the paralyzed target (not serialized).</summary>
    [System.NonSerialized] public CharacterController Target;

    // ======================== PARALYSIS METHODS ========================

    /// <summary>
    /// Whether the target is currently paralyzed by this effect.
    /// Paralyzed = helpless: Str/Dex effectively 0, cannot move/speak/act.
    /// </summary>
    public bool IsParalyzed => IsParalysisActive && ParalysisRemainingRounds > 0;

    /// <summary>
    /// Tick one round off the paralysis duration.
    /// Returns true if the effect is still active, false if expired.
    /// </summary>
    public bool TickRound()
    {
        if (!IsParalysisActive)
            return false;

        ParalysisRemainingRounds--;
        if (ParalysisRemainingRounds <= 0)
        {
            Expire("duration expired");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Expire the entire effect (paralysis + stench).
    /// </summary>
    public void Expire(string reason)
    {
        if (!IsParalysisActive && !IsStenchActive)
            return;

        IsParalysisActive = false;
        IsStenchActive = false;
        ParalysisRemainingRounds = 0;
        Debug.Log($"[GhoulTouch] Effect expired on {Target?.Stats?.CharacterName ?? "unknown"}: {reason}");
    }

    /// <summary>
    /// Check if a creature should be affected by the stench aura.
    /// Returns true if the creature is a valid stench target (living, not caster, not poison-immune).
    /// </summary>
    public bool IsValidStenchTarget(CharacterController creature)
    {
        if (creature == null || creature.Stats == null)
            return false;

        // Caster is exempt from stench
        if (Caster != null && creature == Caster)
            return false;

        // Undead, constructs are not living
        string creatureType = creature.Stats.CreatureType;
        if (!string.IsNullOrWhiteSpace(creatureType))
        {
            string ct = creatureType.Trim().ToLowerInvariant();
            if (ct == "undead" || ct == "construct")
                return false;
        }

        // Dead creatures not affected
        if (creature.Stats.IsDead)
            return false;

        return true;
    }

    /// <summary>
    /// Check if a creature is immune to the stench (poison effect).
    /// Creatures immune to poison are immune to the stench.
    /// </summary>
    public bool IsCreaturePoisonImmune(CharacterController creature)
    {
        if (creature == null || creature.Stats == null)
            return false;

        // Check special abilities for poison immunity
        if (creature.Stats.SpecialAbilities != null)
        {
            for (int i = 0; i < creature.Stats.SpecialAbilities.Length; i++)
            {
                string ability = creature.Stats.SpecialAbilities[i];
                if (string.IsNullOrWhiteSpace(ability))
                    continue;

                string normalized = ability.ToLowerInvariant();
                if (normalized.Contains("poison immun") || normalized.Contains("immune to poison"))
                    return true;
            }
        }

        // Undead and constructs are immune to poison
        string creatureType = creature.Stats.CreatureType;
        if (!string.IsNullOrWhiteSpace(creatureType))
        {
            string ct = creatureType.Trim().ToLowerInvariant();
            if (ct == "undead" || ct == "construct")
                return true;
        }

        return false;
    }

    /// <summary>
    /// Sets the caster reference and serializable name.
    /// </summary>
    public void SetCaster(CharacterController caster)
    {
        Caster = caster;
        CasterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : string.Empty;
    }

    /// <summary>
    /// Sets the target reference.
    /// </summary>
    public void SetTarget(CharacterController target)
    {
        Target = target;
    }

    // ======================== STATIC HELPERS ========================

    /// <summary>
    /// Roll paralysis duration: 1d6+2 rounds.
    /// Range: 3 to 8 rounds.
    /// </summary>
    public static int RollParalysisDuration()
    {
        return Random.Range(1, 7) + 2; // 1d6+2
    }

    /// <summary>
    /// Roll paralysis duration with a specific die roll (for testing).
    /// </summary>
    public static int RollParalysisDuration(int dieRoll)
    {
        return dieRoll + 2;
    }

    // ======================== FACTORY METHODS ========================

    /// <summary>
    /// Factory: Creates a Ghoul Touch effect with randomly rolled paralysis duration.
    /// PHB p.235: 1d6+2 rounds paralysis + stench aura.
    /// </summary>
    public static GhoulTouchEffectData CreateGhoulTouch(CharacterController caster, CharacterController target)
    {
        int duration = RollParalysisDuration();

        var data = new GhoulTouchEffectData
        {
            ParalysisDurationRounds = duration,
            ParalysisRemainingRounds = duration,
            IsParalysisActive = true,
            IsStenchActive = true,
            StenchRadiusFeet = 10,
            StenchRadiusSquares = 2,
            SourceSpellId = SpellNames.GHOUL_TOUCH,
            SourceName = "Ghoul Touch"
        };
        data.SetCaster(caster);
        data.SetTarget(target);
        return data;
    }

    /// <summary>
    /// Factory: Creates a Ghoul Touch effect with a specific duration (for testing).
    /// </summary>
    public static GhoulTouchEffectData CreateGhoulTouchWithDuration(int durationRounds, CharacterController caster, CharacterController target)
    {
        var data = new GhoulTouchEffectData
        {
            ParalysisDurationRounds = durationRounds,
            ParalysisRemainingRounds = durationRounds,
            IsParalysisActive = true,
            IsStenchActive = true,
            StenchRadiusFeet = 10,
            StenchRadiusSquares = 2,
            SourceSpellId = SpellNames.GHOUL_TOUCH,
            SourceName = "Ghoul Touch"
        };
        data.SetCaster(caster);
        data.SetTarget(target);
        return data;
    }
}
