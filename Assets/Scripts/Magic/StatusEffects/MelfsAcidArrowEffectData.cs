using UnityEngine;

/// <summary>
/// Runtime metadata for an active Melf's Acid Arrow effect.
/// Tracks lingering acid rounds, per-round damage profile, and source caster attribution.
/// </summary>
[System.Serializable]
public class MelfsAcidArrowEffectData
{
    public bool IsActive;
    public int RemainingDamageRounds;
    public int DamageDiceSides = 4;
    public int DamageDiceCount = 2;

    [System.NonSerialized] public CharacterController Caster;
    public string CasterName;

    public void SetCaster(CharacterController caster)
    {
        Caster = caster;
        CasterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : string.Empty;
    }
}
