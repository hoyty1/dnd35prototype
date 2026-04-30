using UnityEngine;

/// <summary>
/// Runtime metadata for an active See Invisibility effect.
/// Tracks duration, active state, and caster attribution (personal spell => self-cast).
/// </summary>
[System.Serializable]
public class SeeInvisibilityEffectData
{
    public bool CanSeeInvisible;
    public int DurationRemainingRounds;

    [System.NonSerialized] public CharacterController Caster;
    public string CasterName;

    public void SetCaster(CharacterController caster)
    {
        Caster = caster;
        CasterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : string.Empty;
    }
}
