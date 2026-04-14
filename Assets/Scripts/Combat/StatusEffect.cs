using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Core non-spell combat condition effects.
/// Keeps special maneuver states (prone, grappled, disarmed) in a reusable format.
/// </summary>
[Serializable]
public class StatusEffect
{
    public CombatConditionType Type;
    public string SourceName;
    public int RemainingRounds; // -1 = indefinite

    public StatusEffect(CombatConditionType type, string sourceName, int rounds)
    {
        Type = type;
        SourceName = sourceName ?? "Unknown";
        RemainingRounds = rounds;
    }

    /// <summary>
    /// Tick one round. Returns true if expired this tick.
    /// </summary>
    public bool Tick()
    {
        if (RemainingRounds < 0) return false;
        if (RemainingRounds <= 0) return true;
        RemainingRounds--;
        return RemainingRounds <= 0;
    }

    public string GetDurationLabel()
    {
        if (RemainingRounds < 0) return "∞";
        return $"{Mathf.Max(0, RemainingRounds)}rd";
    }

    public string GetDisplayString()
    {
        return $"{Type}({GetDurationLabel()})";
    }
}

public enum CombatConditionType
{
    Prone,
    Grappled,
    Disarmed,
    Feinted
}

/// <summary>
/// Result payload for special maneuver checks.
/// </summary>
public class SpecialAttackResult
{
    public bool Success;
    public string ManeuverName;
    public string Log;
    public int CheckRoll;
    public int CheckTotal;
    public int OpposedRoll;
    public int OpposedTotal;
    public int DamageDealt;
    public bool ProvokedAoO;
    public bool TargetKilled;
}
