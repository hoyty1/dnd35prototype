using System;
using System.Collections.Generic;

/// <summary>
/// Non-damage immunities and explicit trait immunities for a creature.
/// This complements existing typed DamageImmunities (fire/cold/etc.) in CharacterStats.
/// D&D 3.5e references: Monster Manual glossary (mindless), PHB/SRD condition immunity patterns.
/// </summary>
[Serializable]
public sealed class CreatureImmunities
{
    public bool immuneToPoison;
    public bool immuneToFire;
    public bool immuneToCold;
    public bool immuneToElectricity;
    public bool immuneToAcid;
    public bool immuneToSonic;
    public bool immuneToForce;
    public bool immuneToPositive;
    public bool immuneToNegative;

    public bool immuneToDisease;
    public bool immuneToMindAffecting;
    public bool immuneToCriticalHits;
    public bool immuneToSneakAttack;

    public bool IsImmuneTo(DamageType damageType)
    {
        switch (damageType)
        {
            case DamageType.Fire:
                return immuneToFire;
            case DamageType.Cold:
                return immuneToCold;
            case DamageType.Electricity:
                return immuneToElectricity;
            case DamageType.Acid:
                return immuneToAcid;
            case DamageType.Sonic:
                return immuneToSonic;
            case DamageType.Force:
                return immuneToForce;
            case DamageType.Positive:
                return immuneToPositive;
            case DamageType.Negative:
                return immuneToNegative;
            default:
                return false;
        }
    }

    public bool IsImmuneToEffect(CreatureEffectImmunityType effectType)
    {
        switch (effectType)
        {
            case CreatureEffectImmunityType.Poison:
                return immuneToPoison;
            case CreatureEffectImmunityType.Disease:
                return immuneToDisease;
            case CreatureEffectImmunityType.MindAffecting:
                return immuneToMindAffecting;
            case CreatureEffectImmunityType.CriticalHits:
                return immuneToCriticalHits;
            case CreatureEffectImmunityType.SneakAttack:
                return immuneToSneakAttack;
            default:
                return false;
        }
    }

    public void MergeFrom(CreatureImmunities other)
    {
        if (other == null)
            return;

        immuneToPoison |= other.immuneToPoison;
        immuneToFire |= other.immuneToFire;
        immuneToCold |= other.immuneToCold;
        immuneToElectricity |= other.immuneToElectricity;
        immuneToAcid |= other.immuneToAcid;
        immuneToSonic |= other.immuneToSonic;
        immuneToForce |= other.immuneToForce;
        immuneToPositive |= other.immuneToPositive;
        immuneToNegative |= other.immuneToNegative;

        immuneToDisease |= other.immuneToDisease;
        immuneToMindAffecting |= other.immuneToMindAffecting;
        immuneToCriticalHits |= other.immuneToCriticalHits;
        immuneToSneakAttack |= other.immuneToSneakAttack;
    }

    public CreatureImmunities Clone()
    {
        return new CreatureImmunities
        {
            immuneToPoison = immuneToPoison,
            immuneToFire = immuneToFire,
            immuneToCold = immuneToCold,
            immuneToElectricity = immuneToElectricity,
            immuneToAcid = immuneToAcid,
            immuneToSonic = immuneToSonic,
            immuneToForce = immuneToForce,
            immuneToPositive = immuneToPositive,
            immuneToNegative = immuneToNegative,
            immuneToDisease = immuneToDisease,
            immuneToMindAffecting = immuneToMindAffecting,
            immuneToCriticalHits = immuneToCriticalHits,
            immuneToSneakAttack = immuneToSneakAttack
        };
    }

    public IEnumerable<string> GetDisplayTraits()
    {
        if (immuneToPoison) yield return "Immunity to poison";
        if (immuneToFire) yield return "Immunity to fire";
        if (immuneToCold) yield return "Immunity to cold";
        if (immuneToElectricity) yield return "Immunity to electricity";
        if (immuneToAcid) yield return "Immunity to acid";
        if (immuneToSonic) yield return "Immunity to sonic";
        if (immuneToForce) yield return "Immunity to force";
        if (immuneToPositive) yield return "Immunity to positive energy";
        if (immuneToNegative) yield return "Immunity to negative energy";

        if (immuneToDisease) yield return "Immunity to disease";
        if (immuneToMindAffecting) yield return "Immunity to mind-affecting";
        if (immuneToCriticalHits) yield return "Immunity to critical hits";
        if (immuneToSneakAttack) yield return "Immunity to sneak attack";
    }
}

public enum CreatureEffectImmunityType
{
    None = 0,
    Poison,
    Disease,
    MindAffecting,
    CriticalHits,
    SneakAttack,
}

/// <summary>
/// Shared immunity presets for reusable creature archetypes.
/// </summary>
public static class ImmunityPresets
{
    public static CreatureImmunities DevilImmunities()
    {
        return new CreatureImmunities
        {
            immuneToPoison = true,
            immuneToFire = true
        };
    }

    public static CreatureImmunities MindlessImmunities()
    {
        return new CreatureImmunities
        {
            immuneToMindAffecting = true
        };
    }

    public static CreatureImmunities UndeadImmunities()
    {
        return new CreatureImmunities
        {
            immuneToPoison = true,
            immuneToDisease = true,
            immuneToMindAffecting = true,
            immuneToCriticalHits = true,
            immuneToSneakAttack = true
        };
    }

    public static CreatureImmunities ConstructImmunities()
    {
        return new CreatureImmunities
        {
            immuneToPoison = true,
            immuneToDisease = true,
            immuneToMindAffecting = true,
            immuneToCriticalHits = true,
            immuneToSneakAttack = true
        };
    }

    public static CreatureImmunities OozeImmunities()
    {
        return new CreatureImmunities
        {
            immuneToMindAffecting = true,
            immuneToCriticalHits = true,
            immuneToSneakAttack = true
        };
    }

    public static CreatureImmunities Combine(params CreatureImmunities[] presets)
    {
        CreatureImmunities combined = new CreatureImmunities();
        if (presets == null)
            return combined;

        for (int i = 0; i < presets.Length; i++)
            combined.MergeFrom(presets[i]);

        return combined;
    }
}
