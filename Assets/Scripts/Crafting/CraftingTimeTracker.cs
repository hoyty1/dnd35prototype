// ============================================================================
// D&D 3.5e Item Creation Feats - Time Tracker
// Tracks crafting time advancement for daily/weekly/monthly resets
// ============================================================================

using System;
using UnityEngine;

/// <summary>
/// Tracks total crafting time elapsed for integration with rest/daily-use systems.
/// Simple static tracker — can be extended for calendar integration later.
/// </summary>
public static class CraftingTimeTracker
{
    /// <summary>Total crafting days elapsed since game start.</summary>
    public static int TotalDaysElapsed { get; private set; }

    /// <summary>Days elapsed since last long rest (reset when rest occurs).</summary>
    public static int DaysSinceLastRest { get; private set; }

    /// <summary>Event fired when crafting days elapse (for UI/system hooks).</summary>
    public static event Action<int> OnDaysAdvanced;

    /// <summary>Advance time by the specified number of days.</summary>
    public static void AdvanceDays(int days)
    {
        if (days <= 0) return;

        TotalDaysElapsed += days;
        DaysSinceLastRest += days;

        Debug.Log($"[CraftingTime] Advanced {days} day(s). Total: {TotalDaysElapsed}, Since rest: {DaysSinceLastRest}");

        OnDaysAdvanced?.Invoke(days);
    }

    /// <summary>Reset the days-since-rest counter (called on long rest).</summary>
    public static void OnLongRest()
    {
        DaysSinceLastRest = 0;
        Debug.Log("[CraftingTime] Rest counter reset.");
    }

    /// <summary>Full reset (new game / testing).</summary>
    public static void Reset()
    {
        TotalDaysElapsed = 0;
        DaysSinceLastRest = 0;
        OnDaysAdvanced = null;
    }
}
