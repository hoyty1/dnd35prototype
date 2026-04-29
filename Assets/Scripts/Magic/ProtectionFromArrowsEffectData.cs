using UnityEngine;

/// <summary>
/// Runtime payload for Protection from Arrows.
/// Tracks absorption pool, attacks blocked, and remaining duration.
/// </summary>
[System.Serializable]
public class ProtectionFromArrowsEffectData
{
    public int DamageReductionAmount = 10;
    public int TotalAbsorptionPool;
    public int CurrentAbsorbedDamage;
    public int DurationRemainingRounds;
    public int AttacksBlocked;

    public int RemainingAbsorptionPool => Mathf.Max(0, TotalAbsorptionPool - CurrentAbsorbedDamage);
}
