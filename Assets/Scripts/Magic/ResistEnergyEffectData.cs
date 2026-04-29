using UnityEngine;

/// <summary>
/// Runtime payload for Resist Energy.
/// Stores chosen energy type, flat resistance amount, remaining duration, and caster reference.
/// </summary>
[System.Serializable]
public enum ResistEnergyType
{
    Acid,
    Cold,
    Electricity,
    Fire,
    Sonic
}

[System.Serializable]
public class ResistEnergyEffectData
{
    public ResistEnergyType EnergyType;
    public int ResistanceAmount;
    public int DurationRemainingRounds;
    public CharacterController Caster;

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

    public string GetDisplayLabel()
    {
        return DamageTextUtils.GetDamageTypeDisplay(ToDamageType());
    }
}
