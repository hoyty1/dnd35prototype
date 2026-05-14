using UnityEngine;

/// <summary>
/// Runtime metadata for an active Expeditious Retreat effect.
/// Tracks speed bonus, duration, and caster attribution for dismissal/expiry logs.
/// </summary>
[System.Serializable]
public class ExpeditiousRetreatEffectData
{
    public int SpeedBonusFeet;
    public int DurationRemainingRounds;

    [System.NonSerialized] public CharacterController Caster;
    public string CasterName;

    public void SetCaster(CharacterController caster)
    {
        Caster = caster;
        CasterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : string.Empty;
    }
}
