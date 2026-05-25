// ============================================================================
// D&D 3.5e Bardic Music System (PHB p.28-29)
// Bard gains performance abilities unlocked by level.
// Uses per day = Bard level. Each use = 1 round of performance.
// Most abilities affect 30 ft radius; some are single-target.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The 9 Bardic Music abilities, unlocked at specific Bard levels.
/// </summary>
public enum BardicAbility
{
    None = 0,
    Countersong,       // Level 1: Counter sound-based effects
    Fascinate,         // Level 1: Enthrall targets, Will save
    InspireCourage,    // Level 1: +1 to +4 morale bonus (attack, damage, saves vs fear)
    InspireCompetence, // Level 3: +2 competence bonus to one ally's skill check
    Suggestion,        // Level 6: As spell, after Fascinate, Will save
    InspireGreatness,  // Level 9: +2 HD, temp HP, +2 attack to allies
    SongOfFreedom,     // Level 12: Break enchantments (as spell)
    InspireHeroics,    // Level 15: +4 dodge AC, +4 morale save to one ally
    MassSuggestion     // Level 18: Suggestion on all fascinated targets
}

/// <summary>
/// Static data about each Bardic Music ability.
/// </summary>
public static class BardicAbilityInfo
{
    /// <summary>Minimum Bard level required to use each ability (PHB p.28-29).</summary>
    public static int GetMinLevel(BardicAbility ability)
    {
        switch (ability)
        {
            case BardicAbility.Countersong:       return 1;
            case BardicAbility.Fascinate:         return 1;
            case BardicAbility.InspireCourage:    return 1;
            case BardicAbility.InspireCompetence: return 3;
            case BardicAbility.Suggestion:        return 6;
            case BardicAbility.InspireGreatness:  return 9;
            case BardicAbility.SongOfFreedom:     return 12;
            case BardicAbility.InspireHeroics:    return 15;
            case BardicAbility.MassSuggestion:    return 18;
            default: return 999;
        }
    }

    /// <summary>Whether the ability affects an area (30 ft) or a single target.</summary>
    public static bool IsAreaEffect(BardicAbility ability)
    {
        switch (ability)
        {
            case BardicAbility.Countersong:
            case BardicAbility.Fascinate:
            case BardicAbility.InspireCourage:
            case BardicAbility.MassSuggestion:
                return true;
            default:
                return false;
        }
    }

    /// <summary>Range in feet (30 ft for most AoE, 30 ft for single-target too).</summary>
    public static int GetRange(BardicAbility ability)
    {
        return 30; // All bardic music has 30 ft range
    }

    /// <summary>Whether this ability requires a Perform skill check.</summary>
    public static bool RequiresPerformCheck(BardicAbility ability)
    {
        switch (ability)
        {
            case BardicAbility.Countersong:
            case BardicAbility.Fascinate:
                return true;
            default:
                return false; // Others auto-succeed if Perform ranks are sufficient
        }
    }

    /// <summary>Whether this ability requires sustaining (concentration/continued performance).</summary>
    public static bool IsSustained(BardicAbility ability)
    {
        switch (ability)
        {
            case BardicAbility.Countersong:
            case BardicAbility.Fascinate:
            case BardicAbility.InspireCourage:
            case BardicAbility.InspireCompetence:
            case BardicAbility.InspireGreatness:
            case BardicAbility.InspireHeroics:
                return true; // Effect lasts while performing + 5 rounds after
            case BardicAbility.Suggestion:
            case BardicAbility.SongOfFreedom:
            case BardicAbility.MassSuggestion:
                return false; // One-time effect
            default:
                return false;
        }
    }

    /// <summary>Display name for each ability.</summary>
    public static string GetDisplayName(BardicAbility ability)
    {
        switch (ability)
        {
            case BardicAbility.Countersong:       return "Countersong";
            case BardicAbility.Fascinate:         return "Fascinate";
            case BardicAbility.InspireCourage:    return "Inspire Courage";
            case BardicAbility.InspireCompetence: return "Inspire Competence";
            case BardicAbility.Suggestion:        return "Suggestion";
            case BardicAbility.InspireGreatness:  return "Inspire Greatness";
            case BardicAbility.SongOfFreedom:     return "Song of Freedom";
            case BardicAbility.InspireHeroics:    return "Inspire Heroics";
            case BardicAbility.MassSuggestion:    return "Mass Suggestion";
            default: return "None";
        }
    }

    /// <summary>Description of the ability's effect.</summary>
    public static string GetDescription(BardicAbility ability)
    {
        switch (ability)
        {
            case BardicAbility.Countersong:
                return "Counter sound-based magical effects. Use Perform check in place of saving throw for affected allies within 30 ft.";
            case BardicAbility.Fascinate:
                return "Enthrall one or more creatures within 90 ft. Will save (DC 10 + 1/2 level + Cha) or be fascinated. Obvious threat breaks effect.";
            case BardicAbility.InspireCourage:
                return "Allies within 30 ft gain morale bonus to attack rolls, weapon damage, and saves vs charm/fear. Bonus scales with level.";
            case BardicAbility.InspireCompetence:
                return "One ally within 30 ft gains +2 competence bonus on skill checks with a specific skill for 2 minutes.";
            case BardicAbility.Suggestion:
                return "After successfully Fascinating a creature, implant a Suggestion (as the spell). Will save negates (DC 10 + 1/2 level + Cha).";
            case BardicAbility.InspireGreatness:
                return "One ally within 30 ft gains +2 bonus HD (d10s), +2 competence bonus on attack rolls, and +1 competence bonus on Fort saves.";
            case BardicAbility.SongOfFreedom:
                return "Break enchantment (as the spell) on one creature within 30 ft. Costs one Bardic Music use.";
            case BardicAbility.InspireHeroics:
                return "One ally within 30 ft gains +4 morale bonus on saving throws and +4 dodge bonus to AC.";
            case BardicAbility.MassSuggestion:
                return "As Suggestion, but affects all fascinated creatures. Will save negates (DC 10 + 1/2 level + Cha).";
            default: return "";
        }
    }
}

/// <summary>
/// Manages a Bard's Bardic Music system — uses per day, active performance,
/// unlocked abilities, and effect calculations.
/// Pure data class (no MonoBehaviour).
/// 
/// D&D 3.5e PHB p.28-29:
///   - Uses/day = Bard level
///   - Effects last while performing + 5 rounds after stopping
///   - Standard action to activate
///   - Inspire Courage scales: +1 (L1), +2 (L8), +3 (L14), +4 (L20)
/// </summary>
[Serializable]
public class BardicMusicData
{
    [SerializeField] private int _bardLevel;
    [SerializeField] private int _charismaModifier;
    [SerializeField] private int _usesExpended;
    [SerializeField] private int _performRanks;

    /// <summary>Currently active bardic music ability (None if not performing).</summary>
    public BardicAbility ActiveAbility { get; private set; } = BardicAbility.None;

    /// <summary>Whether the bard is currently performing.</summary>
    public bool IsPerforming => ActiveAbility != BardicAbility.None;

    /// <summary>Rounds remaining on sustained effect after stopping performance.</summary>
    public int LingerRoundsRemaining { get; private set; }

    /// <summary>Current bard level.</summary>
    public int BardLevel => _bardLevel;

    /// <summary>CHA modifier for DCs.</summary>
    public int CharismaModifier => _charismaModifier;

    /// <summary>Uses expended today.</summary>
    public int UsesExpended => _usesExpended;

    /// <summary>
    /// Maximum bardic music uses per day = Bard level (PHB p.28).
    /// </summary>
    public int MaxUsesPerDay => Mathf.Max(0, _bardLevel);

    /// <summary>Remaining uses today.</summary>
    public int RemainingUses => Mathf.Max(0, MaxUsesPerDay - _usesExpended);

    /// <summary>Whether the bard can start a new performance.</summary>
    public bool CanPerform => RemainingUses > 0 && !IsPerforming;

    /// <summary>Perform skill ranks (needed for some abilities).</summary>
    public int PerformRanks => _performRanks;

    /// <summary>Initialize or update when level/stats change.</summary>
    public void Initialize(int bardLevel, int charismaModifier, int performRanks = 0)
    {
        _bardLevel = bardLevel;
        _charismaModifier = charismaModifier;
        _performRanks = performRanks;
    }

    // ─────────────────────────────────────────────
    // Ability Access
    // ─────────────────────────────────────────────

    /// <summary>Whether the bard has unlocked a specific ability at current level.</summary>
    public bool HasAbility(BardicAbility ability)
    {
        return _bardLevel >= BardicAbilityInfo.GetMinLevel(ability);
    }

    /// <summary>Get all abilities unlocked at current bard level.</summary>
    public List<BardicAbility> GetUnlockedAbilities()
    {
        var abilities = new List<BardicAbility>();
        foreach (BardicAbility ability in Enum.GetValues(typeof(BardicAbility)))
        {
            if (ability == BardicAbility.None) continue;
            if (HasAbility(ability))
                abilities.Add(ability);
        }
        return abilities;
    }

    /// <summary>Get the number of unlocked abilities.</summary>
    public int UnlockedAbilityCount => GetUnlockedAbilities().Count;

    // ─────────────────────────────────────────────
    // Performance Management
    // ─────────────────────────────────────────────

    /// <summary>
    /// Start a bardic music performance. Uses 1 daily use per round.
    /// Returns true if started successfully.
    /// </summary>
    public bool StartPerformance(BardicAbility ability)
    {
        if (!CanPerform)
        {
            Debug.Log($"[BardicMusic] Cannot perform — no uses remaining ({RemainingUses}/{MaxUsesPerDay})");
            return false;
        }

        if (!HasAbility(ability))
        {
            Debug.Log($"[BardicMusic] Cannot use {BardicAbilityInfo.GetDisplayName(ability)} — requires Bard level {BardicAbilityInfo.GetMinLevel(ability)}");
            return false;
        }

        // Suggestion requires prior Fascinate
        if (ability == BardicAbility.Suggestion || ability == BardicAbility.MassSuggestion)
        {
            // In practice, the caller should have already Fascinated the target(s)
            // This is a rule reminder, not enforced here
            Debug.Log($"[BardicMusic] Note: {BardicAbilityInfo.GetDisplayName(ability)} requires targets to be Fascinated first.");
        }

        ActiveAbility = ability;
        _usesExpended++;
        LingerRoundsRemaining = 0;

        Debug.Log($"[BardicMusic] Started {BardicAbilityInfo.GetDisplayName(ability)} ({RemainingUses}/{MaxUsesPerDay} uses remaining)");
        return true;
    }

    /// <summary>
    /// Sustain the current performance for another round. Costs 1 use.
    /// Returns true if sustained, false if no uses remaining.
    /// </summary>
    public bool SustainPerformance()
    {
        if (!IsPerforming) return false;
        if (RemainingUses <= 0)
        {
            StopPerformance();
            return false;
        }

        _usesExpended++;
        Debug.Log($"[BardicMusic] Sustaining {BardicAbilityInfo.GetDisplayName(ActiveAbility)} ({RemainingUses}/{MaxUsesPerDay} remaining)");
        return true;
    }

    /// <summary>
    /// Stop the current performance. Effects linger for 5 rounds.
    /// </summary>
    public void StopPerformance()
    {
        if (!IsPerforming) return;

        if (BardicAbilityInfo.IsSustained(ActiveAbility))
        {
            LingerRoundsRemaining = 5; // PHB p.28: effects last 5 rounds after stopping
        }

        Debug.Log($"[BardicMusic] Stopped {BardicAbilityInfo.GetDisplayName(ActiveAbility)}. Linger: {LingerRoundsRemaining} rounds.");
        ActiveAbility = BardicAbility.None;
    }

    /// <summary>Tick down linger duration at end of round.</summary>
    public void TickLingerRound()
    {
        if (LingerRoundsRemaining > 0)
            LingerRoundsRemaining--;
    }

    /// <summary>Whether a sustained effect is still active (performing or lingering).</summary>
    public bool IsEffectActive => IsPerforming || LingerRoundsRemaining > 0;

    // ─────────────────────────────────────────────
    // Effect Calculations
    // ─────────────────────────────────────────────

    /// <summary>
    /// Inspire Courage morale bonus to attack, damage, and saves vs fear (PHB p.28).
    /// +1 at L1, +2 at L8, +3 at L14, +4 at L20.
    /// </summary>
    public int InspireCourageBonus
    {
        get
        {
            if (_bardLevel < 1) return 0;
            if (_bardLevel < 8) return 1;
            if (_bardLevel < 14) return 2;
            if (_bardLevel < 20) return 3;
            return 4;
        }
    }

    /// <summary>
    /// Inspire Competence bonus (+2 competence to one ally's skill check, PHB p.28).
    /// Always +2 in 3.5e.
    /// </summary>
    public int InspireCompetenceBonus => HasAbility(BardicAbility.InspireCompetence) ? 2 : 0;

    /// <summary>
    /// Number of targets for Fascinate (PHB p.28).
    /// 1 creature at L1, +1 per 3 levels after 1st.
    /// </summary>
    public int FascinateTargetCount
    {
        get
        {
            if (_bardLevel < 1) return 0;
            return 1 + (_bardLevel - 1) / 3;
        }
    }

    /// <summary>
    /// Inspire Greatness: number of allies affected.
    /// 1 at L9, +1 at L12, +1 at L15, +1 at L18 (PHB p.29).
    /// </summary>
    public int InspireGreatnessTargetCount
    {
        get
        {
            if (_bardLevel < 9) return 0;
            return 1 + (_bardLevel - 9) / 3;
        }
    }

    /// <summary>
    /// Inspire Greatness grants +2 bonus HD (d10s), which gives:
    /// +2d10 temp HP, +2 competence bonus on attacks, +1 Fort save.
    /// </summary>
    public int InspireGreatnessBonusHD => HasAbility(BardicAbility.InspireGreatness) ? 2 : 0;

    /// <summary>
    /// Inspire Heroics: number of allies (1 at L15, +1 at L18, PHB p.29).
    /// </summary>
    public int InspireHeroicsTargetCount
    {
        get
        {
            if (_bardLevel < 15) return 0;
            return 1 + (_bardLevel - 15) / 3;
        }
    }

    /// <summary>
    /// Inspire Heroics grants +4 morale on saves and +4 dodge to AC.
    /// </summary>
    public int InspireHeroicsBonus => HasAbility(BardicAbility.InspireHeroics) ? 4 : 0;

    /// <summary>
    /// Bardic Music DC for Will saves (Fascinate, Suggestion, Mass Suggestion).
    /// DC = 10 + 1/2 bard level + Cha modifier (PHB p.28).
    /// </summary>
    public int PerformDC => 10 + _bardLevel / 2 + Mathf.Max(0, _charismaModifier);

    /// <summary>Refresh all uses (called on long rest).</summary>
    public void RefreshUses()
    {
        _usesExpended = 0;
        ActiveAbility = BardicAbility.None;
        LingerRoundsRemaining = 0;
    }

    /// <summary>Debug summary.</summary>
    public string GetDebugSummary()
    {
        var unlocked = GetUnlockedAbilities();
        string abilities = unlocked.Count > 0
            ? string.Join(", ", unlocked.ConvertAll(a => BardicAbilityInfo.GetDisplayName(a)))
            : "none";
        return $"Bard L{_bardLevel}: {RemainingUses}/{MaxUsesPerDay} uses, " +
               $"IC+{InspireCourageBonus}, DC {PerformDC}, abilities: {abilities}" +
               (IsPerforming ? $" [ACTIVE: {BardicAbilityInfo.GetDisplayName(ActiveAbility)}]" : "");
    }
}
