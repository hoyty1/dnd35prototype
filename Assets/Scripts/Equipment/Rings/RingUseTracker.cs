using System.Collections.Generic;
using UnityEngine;

// ════════════════════════════════════════════════════════════════════
//  Ring Use Tracker — D&D 3.5e Sprint 2 Active Ring System
//  Tracks daily and weekly ability usage for all equipped rings.
//  Resets daily uses on rest, weekly uses every 7 rests.
//  DMG pp. 229–233: Ring ability frequency tracking.
// ════════════════════════════════════════════════════════════════════

/// <summary>
/// Tracks daily and weekly ability usage across all active rings.
/// Each ring instance is identified by its RingInstanceId.
/// Abilities are keyed by "ringInstanceId:abilityId".
/// </summary>
public class RingUseTracker
{
    // Key format: "ringInstanceId:abilityId" → uses consumed this period
    private Dictionary<string, int> _dailyUsesConsumed = new Dictionary<string, int>();
    private Dictionary<string, int> _weeklyUsesConsumed = new Dictionary<string, int>();
    private Dictionary<string, int> _dailyMaxUses = new Dictionary<string, int>();
    private Dictionary<string, int> _weeklyMaxUses = new Dictionary<string, int>();

    /// <summary>Number of rests since last weekly reset. Resets weekly uses at 7.</summary>
    public int RestsSinceWeeklyReset { get; private set; }

    // ── X-Ray Vision per-rest tracking ──
    private Dictionary<string, int> _xrayUsesThisRest = new Dictionary<string, int>();

    private static RingUseTracker _instance;
    /// <summary>Singleton instance.</summary>
    public static RingUseTracker Instance
    {
        get
        {
            if (_instance == null)
                _instance = new RingUseTracker();
            return _instance;
        }
    }

    private string MakeKey(string ringInstanceId, string abilityId)
    {
        return $"{ringInstanceId}:{abilityId}";
    }

    // ════════════════════════════════════════════════════
    //  Registration
    // ════════════════════════════════════════════════════

    /// <summary>Register a daily-use ability for tracking.</summary>
    public void RegisterDailyAbility(string ringInstanceId, string abilityId, int maxUses)
    {
        string key = MakeKey(ringInstanceId, abilityId);
        _dailyMaxUses[key] = maxUses;
        if (!_dailyUsesConsumed.ContainsKey(key))
            _dailyUsesConsumed[key] = 0;
    }

    /// <summary>Register a weekly-use ability for tracking.</summary>
    public void RegisterWeeklyAbility(string ringInstanceId, string abilityId, int maxUses)
    {
        string key = MakeKey(ringInstanceId, abilityId);
        _weeklyMaxUses[key] = maxUses;
        if (!_weeklyUsesConsumed.ContainsKey(key))
            _weeklyUsesConsumed[key] = 0;
    }

    // ════════════════════════════════════════════════════
    //  Query
    // ════════════════════════════════════════════════════

    /// <summary>Get remaining daily uses for an ability.</summary>
    public int GetDailyUsesRemaining(string ringInstanceId, string abilityId)
    {
        string key = MakeKey(ringInstanceId, abilityId);
        int max = _dailyMaxUses.ContainsKey(key) ? _dailyMaxUses[key] : 0;
        int used = _dailyUsesConsumed.ContainsKey(key) ? _dailyUsesConsumed[key] : 0;
        return Mathf.Max(0, max - used);
    }

    /// <summary>Get remaining weekly uses for an ability.</summary>
    public int GetWeeklyUsesRemaining(string ringInstanceId, string abilityId)
    {
        string key = MakeKey(ringInstanceId, abilityId);
        int max = _weeklyMaxUses.ContainsKey(key) ? _weeklyMaxUses[key] : 0;
        int used = _weeklyUsesConsumed.ContainsKey(key) ? _weeklyUsesConsumed[key] : 0;
        return Mathf.Max(0, max - used);
    }

    /// <summary>Check if a daily ability has uses remaining.</summary>
    public bool HasDailyUsesRemaining(string ringInstanceId, string abilityId)
    {
        return GetDailyUsesRemaining(ringInstanceId, abilityId) > 0;
    }

    /// <summary>Check if a weekly ability has uses remaining.</summary>
    public bool HasWeeklyUsesRemaining(string ringInstanceId, string abilityId)
    {
        return GetWeeklyUsesRemaining(ringInstanceId, abilityId) > 0;
    }

    // ════════════════════════════════════════════════════
    //  Consumption
    // ════════════════════════════════════════════════════

    /// <summary>Consume one daily use. Returns true if successful.</summary>
    public bool ConsumeDailyUse(string ringInstanceId, string abilityId)
    {
        if (!HasDailyUsesRemaining(ringInstanceId, abilityId))
            return false;
        string key = MakeKey(ringInstanceId, abilityId);
        if (!_dailyUsesConsumed.ContainsKey(key))
            _dailyUsesConsumed[key] = 0;
        _dailyUsesConsumed[key]++;
        return true;
    }

    /// <summary>Consume one weekly use. Returns true if successful.</summary>
    public bool ConsumeWeeklyUse(string ringInstanceId, string abilityId)
    {
        if (!HasWeeklyUsesRemaining(ringInstanceId, abilityId))
            return false;
        string key = MakeKey(ringInstanceId, abilityId);
        if (!_weeklyUsesConsumed.ContainsKey(key))
            _weeklyUsesConsumed[key] = 0;
        _weeklyUsesConsumed[key]++;
        return true;
    }

    // ════════════════════════════════════════════════════
    //  X-Ray Vision Tracking
    // ════════════════════════════════════════════════════

    /// <summary>Get X-Ray uses this rest for a ring instance.</summary>
    public int GetXRayUsesThisRest(string ringInstanceId)
    {
        return _xrayUsesThisRest.ContainsKey(ringInstanceId) ? _xrayUsesThisRest[ringInstanceId] : 0;
    }

    /// <summary>Increment X-Ray use counter. Returns the new count (2+ means Con damage applies).</summary>
    public int IncrementXRayUse(string ringInstanceId)
    {
        if (!_xrayUsesThisRest.ContainsKey(ringInstanceId))
            _xrayUsesThisRest[ringInstanceId] = 0;
        _xrayUsesThisRest[ringInstanceId]++;
        return _xrayUsesThisRest[ringInstanceId];
    }

    // ════════════════════════════════════════════════════
    //  Reset
    // ════════════════════════════════════════════════════

    /// <summary>
    /// Reset all daily uses. Called on rest.
    /// Also handles weekly reset every 7 rests.
    /// </summary>
    public void OnRest()
    {
        // Reset all daily uses
        var dailyKeys = new List<string>(_dailyUsesConsumed.Keys);
        foreach (string key in dailyKeys)
            _dailyUsesConsumed[key] = 0;

        // Reset X-Ray vision per-rest counter
        _xrayUsesThisRest.Clear();

        // Track weekly reset
        RestsSinceWeeklyReset++;
        if (RestsSinceWeeklyReset >= 7)
        {
            ResetWeekly();
            RestsSinceWeeklyReset = 0;
        }

        Debug.Log($"[RingUseTracker] Daily uses reset. Rests until weekly reset: {7 - RestsSinceWeeklyReset}");
    }

    /// <summary>Reset all weekly uses.</summary>
    private void ResetWeekly()
    {
        var weeklyKeys = new List<string>(_weeklyUsesConsumed.Keys);
        foreach (string key in weeklyKeys)
            _weeklyUsesConsumed[key] = 0;

        Debug.Log("[RingUseTracker] Weekly uses reset.");
    }

    /// <summary>Unregister all abilities for a specific ring (e.g., when unequipped).</summary>
    public void UnregisterRing(string ringInstanceId)
    {
        var keysToRemove = new List<string>();
        foreach (var key in _dailyUsesConsumed.Keys)
            if (key.StartsWith(ringInstanceId + ":"))
                keysToRemove.Add(key);
        foreach (var key in keysToRemove)
        {
            _dailyUsesConsumed.Remove(key);
            _dailyMaxUses.Remove(key);
        }

        keysToRemove.Clear();
        foreach (var key in _weeklyUsesConsumed.Keys)
            if (key.StartsWith(ringInstanceId + ":"))
                keysToRemove.Add(key);
        foreach (var key in keysToRemove)
        {
            _weeklyUsesConsumed.Remove(key);
            _weeklyMaxUses.Remove(key);
        }

        _xrayUsesThisRest.Remove(ringInstanceId);
    }

    /// <summary>Full reset — clear all tracking data.</summary>
    public void Reset()
    {
        _dailyUsesConsumed.Clear();
        _weeklyUsesConsumed.Clear();
        _dailyMaxUses.Clear();
        _weeklyMaxUses.Clear();
        _xrayUsesThisRest.Clear();
        RestsSinceWeeklyReset = 0;
    }
}
