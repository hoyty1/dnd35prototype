using UnityEngine;
using DND35e.Identifiers;

// ============================================================================
// ScareEffectData.cs — Fear effect tracking for Scare spell
//
// D&D 3.5e PHB p.274:
//   Scare causes living creatures of less than 6 HD to become frightened.
//   Will save partial: failed = frightened (1 round/level), succeeded = shaken (1 round).
//
//   Key rules:
//   - Creatures with 6+ HD are completely immune
//   - Fear, Mind-Affecting descriptors
//   - Multi-target: 1 creature per 3 caster levels, no two more than 30 ft apart
//   - Frightened: -2 attacks/saves/skills/ability checks, must flee from caster
//   - Shaken: -2 attacks/saves/skills/ability checks, can act normally
//   - Fear effects stack upward (shaken+shaken=frightened, etc.)
// ============================================================================

/// <summary>
/// Represents the fear severity level applied by Scare and similar spells.
/// Fear effects can escalate: Shaken → Frightened → Panicked.
/// </summary>
public enum FearLevel
{
    None = 0,
    Shaken = 1,
    Frightened = 2,
    Panicked = 3
}

/// <summary>
/// Runtime data for an active Scare spell fear effect.
/// Tracks fear level, duration, and caster reference for flee direction.
/// </summary>
[System.Serializable]
public class ScareEffectData
{
    // ======================== CORE STATE ========================

    /// <summary>Current fear severity level.</summary>
    public FearLevel CurrentFearLevel;

    /// <summary>Remaining duration in combat rounds.</summary>
    public int DurationRemainingRounds;

    /// <summary>Whether the fear effect is currently active.</summary>
    public bool IsActive;

    // ======================== PENALTIES ========================

    /// <summary>Attack roll penalty (typically -2 for all fear levels).</summary>
    public int AttackPenalty = -2;

    /// <summary>Saving throw penalty (typically -2 for all fear levels).</summary>
    public int SavePenalty = -2;

    /// <summary>Skill check penalty (typically -2 for all fear levels).</summary>
    public int SkillPenalty = -2;

    /// <summary>Ability check penalty (typically -2 for all fear levels).</summary>
    public int AbilityCheckPenalty = -2;

    // ======================== SOURCE TRACKING ========================

    /// <summary>The spell ID that created this effect.</summary>
    public string SourceSpellId;

    /// <summary>Human-readable name of the source spell.</summary>
    public string SourceName;

    /// <summary>Name of the caster who applied this effect.</summary>
    public string CasterName;

    /// <summary>Runtime reference to the caster (for flee direction).</summary>
    [System.NonSerialized] public CharacterController Caster;

    // ======================== QUERY METHODS ========================

    /// <summary>Whether the target is currently frightened (must flee).</summary>
    public bool IsFrightened => IsActive && CurrentFearLevel == FearLevel.Frightened;

    /// <summary>Whether the target is currently shaken (penalties only).</summary>
    public bool IsShaken => IsActive && CurrentFearLevel == FearLevel.Shaken;

    /// <summary>Whether the target is currently panicked (flee mindlessly, drop items).</summary>
    public bool IsPanicked => IsActive && CurrentFearLevel == FearLevel.Panicked;

    /// <summary>Whether the target must flee from the fear source.</summary>
    public bool MustFlee => IsActive && (CurrentFearLevel == FearLevel.Frightened || CurrentFearLevel == FearLevel.Panicked);

    /// <summary>
    /// Tick one round off the duration.
    /// Returns true if the effect is still active, false if expired.
    /// </summary>
    public bool TickRound()
    {
        if (!IsActive)
            return false;

        DurationRemainingRounds--;
        if (DurationRemainingRounds <= 0)
        {
            Expire("duration expired");
            return false;
        }

        return true;
    }

    /// <summary>Expire the fear effect.</summary>
    public void Expire(string reason)
    {
        if (!IsActive)
            return;

        IsActive = false;
        CurrentFearLevel = FearLevel.None;
        DurationRemainingRounds = 0;
        Debug.Log($"[Scare] Fear effect expired: {reason}");
    }

    /// <summary>Sets the caster reference and serializable name.</summary>
    public void SetCaster(CharacterController caster)
    {
        Caster = caster;
        CasterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : string.Empty;
    }

    // ======================== STATIC HELPERS ========================

    /// <summary>
    /// Calculate the maximum number of targets for Scare: 1 per 3 caster levels (min 1).
    /// </summary>
    public static int GetMaxTargets(int casterLevel)
    {
        return Mathf.Max(1, casterLevel / 3);
    }

    /// <summary>
    /// Check if a target is immune to Scare based on HD limit (6+ HD immune).
    /// </summary>
    public static bool IsImmuneByHD(int hitDice)
    {
        return hitDice >= 6;
    }

    /// <summary>
    /// Escalate fear level when stacking fear effects.
    /// D&D 3.5e: Shaken + any fear = Frightened, Frightened + any fear = Panicked.
    /// </summary>
    public static FearLevel EscalateFear(FearLevel current, FearLevel incoming)
    {
        if (current == FearLevel.None)
            return incoming;

        // Any stacking escalates by one level
        int escalated = Mathf.Max((int)current, (int)incoming) + 1;
        return (FearLevel)Mathf.Min(escalated, (int)FearLevel.Panicked);
    }

    // ======================== FACTORY METHODS ========================

    /// <summary>
    /// Factory: Creates a Scare effect for a frightened target (failed Will save).
    /// Duration: 1 round/caster level.
    /// </summary>
    public static ScareEffectData CreateFrightened(int casterLevel, CharacterController caster)
    {
        int duration = Mathf.Max(1, casterLevel);

        var data = new ScareEffectData
        {
            CurrentFearLevel = FearLevel.Frightened,
            DurationRemainingRounds = duration,
            IsActive = true,
            AttackPenalty = -2,
            SavePenalty = -2,
            SkillPenalty = -2,
            AbilityCheckPenalty = -2,
            SourceSpellId = SpellNames.SCARE,
            SourceName = "Scare"
        };
        data.SetCaster(caster);
        return data;
    }

    /// <summary>
    /// Factory: Creates a Scare effect for a shaken target (successful Will save).
    /// Duration: 1 round.
    /// </summary>
    public static ScareEffectData CreateShaken(CharacterController caster)
    {
        var data = new ScareEffectData
        {
            CurrentFearLevel = FearLevel.Shaken,
            DurationRemainingRounds = 1,
            IsActive = true,
            AttackPenalty = -2,
            SavePenalty = -2,
            SkillPenalty = -2,
            AbilityCheckPenalty = -2,
            SourceSpellId = SpellNames.SCARE,
            SourceName = "Scare"
        };
        data.SetCaster(caster);
        return data;
    }

    /// <summary>
    /// Factory: Creates a Scare effect with a specific fear level and duration (for testing).
    /// </summary>
    public static ScareEffectData CreateWithParams(FearLevel level, int durationRounds, CharacterController caster)
    {
        var data = new ScareEffectData
        {
            CurrentFearLevel = level,
            DurationRemainingRounds = durationRounds,
            IsActive = true,
            AttackPenalty = -2,
            SavePenalty = -2,
            SkillPenalty = -2,
            AbilityCheckPenalty = -2,
            SourceSpellId = SpellNames.SCARE,
            SourceName = "Scare"
        };
        data.SetCaster(caster);
        return data;
    }
}
