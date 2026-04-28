using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Common disease entries from D&D 3.5e DMG.
/// </summary>
public enum DiseaseType
{
    BlindingSickness,
    CackleFever,
    FilthFever,
    Mindfire,
    RedAche,
    Shakes,
    SlimyDoom,
    MummyRot,
    DevilChills,
    DemonFever
}

/// <summary>
/// Generic ability damage/drain payload used by disease and poison effects.
/// </summary>
[Serializable]
public class AbilityDamageEffect
{
    public AbilityType Ability;
    public string DamageAmount;
    public bool IsDrain;

    public int RollDamage()
    {
        return DiceNotation.Roll(DamageAmount);
    }
}

/// <summary>
/// Static disease definition data (DMG disease table).
/// </summary>
[Serializable]
public class DiseaseData
{
    public string Name;
    public DiseaseType Type;
    public int FortitudeDC;
    public string IncubationPeriod;
    public List<AbilityDamageEffect> DamageEffects;
    public string Description;

    public int RollIncubationDays()
    {
        int days = DiceNotation.Roll(IncubationPeriod);
        return Mathf.Max(1, days);
    }
}

/// <summary>
/// Runtime state for a disease currently afflicting a character.
/// </summary>
[Serializable]
public class ActiveDisease
{
    public DiseaseData DiseaseData;
    public int DaysUntilActive;
    public bool IsIncubating;

    // D&D 3.5e: 2 consecutive successful saves cures a disease.
    public int ConsecutiveSuccessfulSaves;

    public ActiveDisease(DiseaseData data)
    {
        DiseaseData = data;
        DaysUntilActive = data != null ? data.RollIncubationDays() : 1;
        IsIncubating = DaysUntilActive > 0;
        ConsecutiveSuccessfulSaves = 0;
    }

    public string GetStatusSummary()
    {
        if (DiseaseData == null)
            return "Unknown disease";

        if (IsIncubating)
            return $"{DiseaseData.Name} (incubating: {Mathf.Max(1, DaysUntilActive)} day{(DaysUntilActive == 1 ? string.Empty : "s")})";

        return $"{DiseaseData.Name} (active, {ConsecutiveSuccessfulSaves}/2 saves)";
    }
}

internal static class DiceNotation
{
    public static int Roll(string notation)
    {
        if (string.IsNullOrWhiteSpace(notation))
            return 0;

        string trimmed = notation.Trim().ToLowerInvariant();
        int spaceIdx = trimmed.IndexOf(' ');
        if (spaceIdx >= 0)
            trimmed = trimmed.Substring(0, spaceIdx);

        int dIdx = trimmed.IndexOf('d');
        if (dIdx <= 0)
        {
            if (int.TryParse(trimmed, out int flat))
                return flat;
            return 0;
        }

        string left = trimmed.Substring(0, dIdx);
        string right = dIdx + 1 < trimmed.Length ? trimmed.Substring(dIdx + 1) : string.Empty;

        if (!int.TryParse(left, out int diceCount) || diceCount <= 0)
            return 0;

        if (!int.TryParse(right, out int diceSize) || diceSize <= 0)
            return 0;

        int total = 0;
        for (int i = 0; i < diceCount; i++)
            total += UnityEngine.Random.Range(1, diceSize + 1);

        return total;
    }
}
