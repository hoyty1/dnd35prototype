using UnityEngine;

/// <summary>
/// Runtime payload for Protection from Energy (D&D 3.5e PHB p.266).
/// Tracks energy type, absorption pool, remaining duration, and caster reference.
/// 
/// Absorption: 12 points per caster level (max 120 at CL 10+).
/// Spell ends when protection points reach 0 (discharged) or duration expires.
/// </summary>
[System.Serializable]
public class ProtectionFromEnergyEffectData
{
    /// <summary>Energy type this protection absorbs.</summary>
    public ResistEnergyType EnergyType;

    /// <summary>Maximum absorption pool (12 × CL, capped at 120).</summary>
    public int MaxAbsorptionPoints;

    /// <summary>Current remaining absorption points.</summary>
    public int RemainingAbsorptionPoints;

    /// <summary>Duration remaining in rounds. 10 min/level = 100 rounds/level.</summary>
    public int DurationRemainingRounds;

    /// <summary>Caster who applied this protection.</summary>
    public CharacterController Caster;

    /// <summary>Caster level at time of casting (for dispel checks).</summary>
    public int CasterLevel;

    /// <summary>Whether this protection has been fully discharged (pool reached 0).</summary>
    public bool IsDischarged => RemainingAbsorptionPoints <= 0;

    /// <summary>Whether this protection is still active (has points and duration remaining).</summary>
    public bool IsActive => RemainingAbsorptionPoints > 0 && DurationRemainingRounds > 0;

    /// <summary>
    /// Calculates the absorption pool for a given caster level.
    /// Formula: min(casterLevel × 12, 120)
    /// </summary>
    public static int CalculateAbsorptionPool(int casterLevel)
    {
        return Mathf.Min(Mathf.Max(1, casterLevel) * 12, 120);
    }

    /// <summary>
    /// Absorbs incoming energy damage, reducing the protection pool.
    /// Returns the amount actually absorbed. Remaining damage passes through.
    /// </summary>
    /// <param name="incomingDamage">Amount of energy damage to absorb.</param>
    /// <returns>Amount of damage absorbed by this protection.</returns>
    public int AbsorbDamage(int incomingDamage)
    {
        if (incomingDamage <= 0 || RemainingAbsorptionPoints <= 0)
            return 0;

        int absorbed = Mathf.Min(incomingDamage, RemainingAbsorptionPoints);
        RemainingAbsorptionPoints -= absorbed;
        return absorbed;
    }

    /// <summary>Converts the energy type to a DamageType for damage system integration.</summary>
    public DamageType ToDamageType()
    {
        switch (EnergyType)
        {
            case ResistEnergyType.Acid: return DamageType.Acid;
            case ResistEnergyType.Cold: return DamageType.Cold;
            case ResistEnergyType.Electricity: return DamageType.Electricity;
            case ResistEnergyType.Fire: return DamageType.Fire;
            case ResistEnergyType.Sonic: return DamageType.Sonic;
            default: return DamageType.Untyped;
        }
    }

    /// <summary>Display label for the energy type.</summary>
    public string GetDisplayLabel()
    {
        return DamageTextUtils.GetDamageTypeDisplay(ToDamageType());
    }

    /// <summary>Summary string for combat log/UI display.</summary>
    public string GetDisplayString()
    {
        string label = GetDisplayLabel();
        return $"Protection from Energy ({label}): {RemainingAbsorptionPoints}/{MaxAbsorptionPoints} pts, {DurationRemainingRounds} rounds";
    }
}
