using UnityEngine;

/// <summary>
/// Runtime metadata for an active Invisibility effect.
/// Tracks duration, movement-state Hide bonus mode, and caster attribution.
/// </summary>
[System.Serializable]
public class InvisibilityEffectData
{
    public bool IsInvisible;
    public int DurationRemainingRounds;
    public bool IsMoving;

    [System.NonSerialized] public CharacterController Caster;
    public string CasterName;

    public void SetCaster(CharacterController caster)
    {
        Caster = caster;
        CasterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : string.Empty;
    }
}
