using UnityEngine;

/// <summary>
/// Runtime metadata for an active Haste effect (PHB p.239).
/// Tracks all bonuses, duration, and caster attribution.
///
/// D&D 3.5e Haste effects:
///   • +1 bonus on attack rolls
///   • +1 dodge bonus to AC and Reflex saves
///   • +30 ft. movement speed (all modes)
///   • One extra attack at full BAB on full attack action
///   • Counters/dispels Slow
/// </summary>
[System.Serializable]
public class HasteEffectData
{
    /// <summary>Attack roll bonus (+1).</summary>
    public int AttackBonus = 1;

    /// <summary>Dodge bonus to AC (+1).</summary>
    public int ACBonus = 1;

    /// <summary>Bonus to Reflex saves (+1).</summary>
    public int ReflexSaveBonus = 1;

    /// <summary>Speed bonus in feet (+30).</summary>
    public int SpeedBonusFeet = 30;

    /// <summary>Whether this grants an extra attack on full attack actions.</summary>
    public bool GrantsExtraAttack = true;

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
