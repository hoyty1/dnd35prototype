using System;
using System.Collections.Generic;

/// <summary>
/// D&D 3.5e poison delivery methods.
/// </summary>
public enum PoisonType
{
    Contact,
    Ingested,
    Inhaled,
    Injury
}

/// <summary>
/// Static poison definition data.
/// </summary>
[Serializable]
public class PoisonData
{
    public string Id;
    public string Name;
    public PoisonType Type;
    public int FortitudeDC;
    public List<AbilityDamageEffect> InitialDamage;
    public List<AbilityDamageEffect> SecondaryDamage;
    public int PriceInGold;
    public string Description;

    /// <summary>
    /// Delay to secondary effect in seconds.
    /// DMG poison defaults are commonly 1 minute (10 rounds), with some ingested poisons at 10 minutes.
    /// </summary>
    public float GetSecondaryDamageDelaySeconds()
    {
        switch (Type)
        {
            case PoisonType.Ingested:
                return 600f;
            case PoisonType.Contact:
            case PoisonType.Injury:
            case PoisonType.Inhaled:
            default:
                return 60f;
        }
    }
}

/// <summary>
/// Runtime state for poison currently affecting a character.
/// </summary>
[Serializable]
public class ActivePoison
{
    public PoisonData PoisonData;
    public float TimeUntilSecondary;
    public bool InitialSaveSucceeded;
    public bool SecondarySaveSucceeded;
    public bool SecondaryResolved;

    public ActivePoison(PoisonData data)
    {
        PoisonData = data;
        TimeUntilSecondary = data != null ? data.GetSecondaryDamageDelaySeconds() : 60f;
    }

    public string GetStatusSummary()
    {
        if (PoisonData == null)
            return "Unknown poison";

        if (!SecondaryResolved)
            return $"{PoisonData.Name} (secondary in {Math.Max(0, (int)Math.Ceiling(TimeUntilSecondary))}s)";

        return $"{PoisonData.Name} (resolved)";
    }
}
