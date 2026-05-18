using UnityEngine;

/// <summary>
/// Runtime payload for Stoneskin (PHB p.285).
/// Grants DR 10/adamantine. Absorbs up to 10 points of damage per caster level
/// (maximum 150 points). Once the absorption pool is depleted, the spell is discharged.
/// </summary>
[System.Serializable]
public class StoneskinEffectData
{
    /// <summary>DR amount granted (always 10 for Stoneskin).</summary>
    public int DamageReductionAmount = 10;

    /// <summary>Bypass tag for the DR (adamantine for Stoneskin).</summary>
    public DamageBypassTag BypassTag = DamageBypassTag.Adamantine;

    /// <summary>Total absorption pool = min(150, casterLevel * 10).</summary>
    public int TotalAbsorptionPool;

    /// <summary>How much damage has been absorbed so far.</summary>
    public int CurrentAbsorbedDamage;

    /// <summary>Remaining duration in rounds (synced with ActiveSpellEffect).</summary>
    public int DurationRemainingRounds;

    /// <summary>Number of hits where DR was applied.</summary>
    public int HitsBlocked;

    /// <summary>Remaining absorption pool.</summary>
    public int RemainingAbsorptionPool => Mathf.Max(0, TotalAbsorptionPool - CurrentAbsorbedDamage);

    /// <summary>Whether the absorption pool is depleted.</summary>
    public bool IsDepleted => RemainingAbsorptionPool <= 0;
}
