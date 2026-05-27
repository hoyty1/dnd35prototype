using UnityEngine;

/// <summary>
/// Runtime metadata for an active Slow effect (PHB p.280).
/// Tracks all penalties, duration, and caster attribution.
///
/// D&D 3.5e Slow effects:
///   • -1 penalty on attack rolls
///   • -1 penalty to AC and Reflex saves
///   • Movement speed halved (round down to nearest 5 ft)
///   • Can only take a single move or standard action per turn (no full-round actions)
///   • Counters/dispels Haste
/// </summary>
[System.Serializable]
public class SlowEffectData
{
    /// <summary>Attack roll penalty (-1).</summary>
    public int AttackPenalty = -1;

    /// <summary>AC penalty (-1).</summary>
    public int ACPenalty = -1;

    /// <summary>Penalty to Reflex saves (-1).</summary>
    public int ReflexSavePenalty = -1;

    /// <summary>Movement speed multiplier (0.5 = half speed).</summary>
    public float SpeedMultiplier = 0.5f;

    /// <summary>Whether full-round actions are blocked.</summary>
    public bool BlocksFullRoundActions = true;

    /// <summary>Remaining duration in rounds.</summary>
    public int DurationRemainingRounds;

    [System.NonSerialized] public CharacterController Caster;
    public string CasterName;

    public void SetCaster(CharacterController caster)
    {
        Caster = caster;
        CasterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : string.Empty;
    }
}
