using UnityEngine;

/// <summary>
/// Runtime metadata for an active Disguise Self effect.
/// Tracks original/displayed race identity separately from actual stats.
/// </summary>
[System.Serializable]
public class DisguiseSelfEffectData
{
    public string OriginalRace;
    public string DisguisedRace;
    public int DurationRemainingRounds;

    [System.NonSerialized] public CharacterController Caster;
    public string CasterName;

    public void SetCaster(CharacterController caster)
    {
        Caster = caster;
        CasterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : string.Empty;
    }
}
