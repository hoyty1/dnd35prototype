using System.Collections.Generic;
using UnityEngine;

// ════════════════════════════════════════════════════════════════════
//  Ring Ability — D&D 3.5e Sprint 2 Active Ring System
//  Defines a single activatable ability on a magic ring.
//  DMG pp. 229–233: Command word activation, use tracking.
// ════════════════════════════════════════════════════════════════════

/// <summary>
/// Frequency type for ring ability usage.
/// </summary>
public enum RingUseFrequency
{
    /// <summary>Unlimited use (Ring of Invisibility, Blinking, Telekinesis).</summary>
    AtWill,
    /// <summary>Limited uses per day, reset on rest (Ring of Animal Friendship 3/day).</summary>
    PerDay,
    /// <summary>Limited uses per week, reset every 7 rests (Ring of Djinni Calling 1/week).</summary>
    PerWeek,
    /// <summary>Consumes charges from the ring's charge pool (Ring of the Ram).</summary>
    Charged,
    /// <summary>Automatic — always active, no activation needed (Ring of Spell Turning).</summary>
    Automatic
}

/// <summary>
/// Action type required to activate the ring ability.
/// </summary>
public enum RingActionType
{
    /// <summary>Standard action (most rings).</summary>
    Standard,
    /// <summary>Full-round action (Ring of Djinni Calling).</summary>
    FullRound,
    /// <summary>No action — automatic activation (Ring of Spell Turning).</summary>
    None
}

/// <summary>
/// Defines a single activatable ability on a magic ring.
/// Each ring may have one or more abilities (Ring of Shooting Stars has 5).
/// </summary>
[System.Serializable]
public class RingAbility
{
    /// <summary>Internal key for this ability (e.g., "invisibility", "charm_animal", "ball_lightning").</summary>
    public string AbilityId;

    /// <summary>Display name shown in UI (e.g., "Become Invisible", "Charm Animal").</summary>
    public string DisplayName;

    /// <summary>Short description for tooltip.</summary>
    public string Description;

    /// <summary>How often this ability can be used.</summary>
    public RingUseFrequency Frequency;

    /// <summary>Max uses per period (for PerDay/PerWeek). 0 or -1 = unlimited.</summary>
    public int MaxUsesPerPeriod;

    /// <summary>Charge cost per use (for Charged frequency). Ring of Ram: 1–3.</summary>
    public int ChargeCost;

    /// <summary>Maximum charge cost player can choose (Ring of Ram: 3). 0 = fixed cost.</summary>
    public int MaxChargeCost;

    /// <summary>Action type required to activate.</summary>
    public RingActionType ActionType = RingActionType.Standard;

    /// <summary>Whether this ability requires a target creature.</summary>
    public bool RequiresTarget;

    /// <summary>Range in feet for targeting (0 = self only).</summary>
    public int RangeFeet;

    /// <summary>Caster level for the effect.</summary>
    public int CasterLevel;

    /// <summary>Spell DC (if applicable, e.g., Charm Animal DC 11).</summary>
    public int SaveDC;

    /// <summary>Save type for the ability (e.g., "Will", "Reflex"). Empty = no save.</summary>
    public string SaveType;

    /// <summary>Restriction note (e.g., "Outdoors at night only").</summary>
    public string Restriction;

    /// <summary>Whether this ability requires outdoors/night (Shooting Stars: Dancing Lights, Shooting Stars).</summary>
    public bool RequiresOutdoorsNight;

    /// <summary>
    /// Returns a display string for remaining uses.
    /// </summary>
    public string GetUsesDisplayString(RingUseTracker tracker, string ringInstanceId)
    {
        switch (Frequency)
        {
            case RingUseFrequency.AtWill:
                return "At will";
            case RingUseFrequency.PerDay:
                int dailyRemaining = tracker != null ? tracker.GetDailyUsesRemaining(ringInstanceId, AbilityId) : MaxUsesPerPeriod;
                return $"{dailyRemaining}/{MaxUsesPerPeriod} today";
            case RingUseFrequency.PerWeek:
                int weeklyRemaining = tracker != null ? tracker.GetWeeklyUsesRemaining(ringInstanceId, AbilityId) : MaxUsesPerPeriod;
                return $"{weeklyRemaining}/{MaxUsesPerPeriod} this week";
            case RingUseFrequency.Charged:
                return "Charged";
            case RingUseFrequency.Automatic:
                return "Automatic";
            default:
                return "";
        }
    }
}
