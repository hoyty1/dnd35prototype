using System;
using UnityEngine;

/// <summary>
/// Special effects that poisons and diseases can apply in addition to ability damage.
/// </summary>
public enum PoisonEffectType
{
    None = 0,
    Paralysis,
    Unconsciousness,
    Nausea,
    Confusion,
    Blindness,
    Deafness,
    Exhaustion,
    Petrification,
    Death
}

/// <summary>
/// Config payload for a poison/disease special effect.
/// </summary>
[Serializable]
public class PoisonSpecialEffect
{
    public PoisonEffectType EffectType;
    public string DurationFormula; // e.g. "2d6 minutes", "1d3 hours", "10 rounds", "permanent"
    public bool AppliesToInitial;
    public bool AppliesToSecondary;
    public string Description;

    public int RollDurationInRounds()
    {
        if (string.IsNullOrWhiteSpace(DurationFormula))
            return 0;

        string normalized = DurationFormula.Trim().ToLowerInvariant();
        if (normalized == "permanent")
            return -1; // Reuse condition manager convention for indefinite duration.

        int roundsPerUnit = 1;
        string dicePart = normalized;

        if (normalized.Contains("hour"))
        {
            roundsPerUnit = 600;
            dicePart = normalized.Replace("hours", string.Empty).Replace("hour", string.Empty).Trim();
        }
        else if (normalized.Contains("minute"))
        {
            roundsPerUnit = 10;
            dicePart = normalized.Replace("minutes", string.Empty).Replace("minute", string.Empty).Trim();
        }
        else if (normalized.Contains("round"))
        {
            roundsPerUnit = 1;
            dicePart = normalized.Replace("rounds", string.Empty).Replace("round", string.Empty).Trim();
        }

        int units = RollDiceFormula(dicePart);
        return Mathf.Max(0, units * roundsPerUnit);
    }

    public CombatConditionType ToConditionType()
    {
        switch (EffectType)
        {
            case PoisonEffectType.Paralysis:
                return CombatConditionType.Paralyzed;
            case PoisonEffectType.Unconsciousness:
                return CombatConditionType.Unconscious;
            case PoisonEffectType.Nausea:
                return CombatConditionType.Nauseated;
            case PoisonEffectType.Confusion:
                return CombatConditionType.Confused;
            case PoisonEffectType.Blindness:
                return CombatConditionType.Blinded;
            case PoisonEffectType.Deafness:
                return CombatConditionType.Deafened;
            case PoisonEffectType.Exhaustion:
                return CombatConditionType.Exhausted;
            case PoisonEffectType.Petrification:
                return CombatConditionType.Petrified;
            default:
                return CombatConditionType.None;
        }
    }

    private static int RollDiceFormula(string formula)
    {
        if (string.IsNullOrWhiteSpace(formula))
            return 1;

        string trimmed = formula.Trim().ToLowerInvariant();
        if (trimmed.Contains("d"))
        {
            string[] parts = trimmed.Split('d');
            if (parts.Length == 2
                && int.TryParse(parts[0].Trim(), out int numDice)
                && int.TryParse(parts[1].Trim(), out int diceSize)
                && numDice > 0
                && diceSize > 0)
            {
                int total = 0;
                for (int i = 0; i < numDice; i++)
                    total += UnityEngine.Random.Range(1, diceSize + 1);
                return total;
            }
        }

        if (int.TryParse(trimmed, out int flat))
            return flat;

        return 1;
    }
}
