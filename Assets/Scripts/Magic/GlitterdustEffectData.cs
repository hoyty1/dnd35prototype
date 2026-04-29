using UnityEngine;

/// <summary>
/// Runtime metadata for an active Glitterdust effect on a creature.
/// Tracks outline state, blinded-state from Glitterdust save failure, duration,
/// and caster attribution.
/// </summary>
[System.Serializable]
public class GlitterdustEffectData
{
    public bool OutlinedByDust;
    public bool IsBlinded;
    public int DurationRemainingRounds;

    [System.NonSerialized] public CharacterController Caster;
    public string CasterName;

    public void SetCaster(CharacterController caster)
    {
        Caster = caster;
        CasterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : string.Empty;
    }
}
