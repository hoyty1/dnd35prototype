// ============================================================================
// D&D 3.5e Bardic Music (PHB p.29-30)
// The per-bard tracking data: uses spent today, the performance under way and the creatures it affects. The rules that
// read and write it, for PCs and NPCs alike, are in BardicMusicRules (CHR-020, CHR-026).
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
    Countersong,       // Level 1, Perform 3: a Perform check result in place of saves against sonic or language-dependent magic
    Fascinate,         // Level 1, Perform 3: Will save against the Perform check result or fascinated (concentration)
    InspireCourage,    // Level 1, Perform 3: +1 morale (attack, weapon damage, saves against charm and fear), +2 at 8th
    InspireCompetence, // Level 3, Perform 6: +2 competence bonus on one ally's checks with one skill (concentration)
    Suggestion,        // Level 6, Perform 9: suggestion on a creature the bard has fascinated (no daily use)
    InspireGreatness,  // Level 9, Perform 12 (beyond the level 1-8 scope; not implemented)
    SongOfFreedom,     // Level 12, Perform 15 (not implemented)
    InspireHeroics,    // Level 15, Perform 18 (not implemented)
    MassSuggestion     // Level 18, Perform 21 (not implemented)
}

/// <summary>
/// Static data about each Bardic Music ability (PHB p.29-30).
/// </summary>
public static class BardicAbilityInfo
{
    /// <summary>Minimum Bard level required to use each ability (PHB p.29-30).</summary>
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

    /// <summary>
    /// Minimum ranks in a Perform skill (PHB p.29-30): without them the bard does not have the ability at all, whatever
    /// his level.
    /// </summary>
    public static int GetMinPerformRanks(BardicAbility ability)
    {
        switch (ability)
        {
            case BardicAbility.Countersong:
            case BardicAbility.Fascinate:
            case BardicAbility.InspireCourage:    return 3;
            case BardicAbility.InspireCompetence: return 6;
            case BardicAbility.Suggestion:        return 9;
            case BardicAbility.InspireGreatness:  return 12;
            case BardicAbility.SongOfFreedom:     return 15;
            case BardicAbility.InspireHeroics:    return 18;
            case BardicAbility.MassSuggestion:    return 21;
            default: return 999;
        }
    }

    /// <summary>The abilities this game resolves (the bard's levels 1-8; CHR-020). The others are data only.</summary>
    public static bool IsImplemented(BardicAbility ability)
    {
        switch (ability)
        {
            case BardicAbility.Countersong:
            case BardicAbility.Fascinate:
            case BardicAbility.InspireCourage:
            case BardicAbility.InspireCompetence:
            case BardicAbility.Suggestion:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// The abilities that take a standard action each round to keep up (PHB p.29: fascinate "as long as the bard
    /// continues to play and concentrate", inspire competence "as long as the bard concentrates").
    /// </summary>
    public static bool RequiresConcentration(BardicAbility ability)
        => ability == BardicAbility.Fascinate || ability == BardicAbility.InspireCompetence || ability == BardicAbility.SongOfFreedom;

    /// <summary>The abilities whose effect lasts 5 rounds after the ally stops hearing the bard (PHB p.29-30).</summary>
    public static bool LingersAfterPerformance(BardicAbility ability)
        => ability == BardicAbility.InspireCourage || ability == BardicAbility.InspireGreatness || ability == BardicAbility.InspireHeroics;

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

    /// <summary>Description of the ability's effect (PHB p.29-30).</summary>
    public static string GetDescription(BardicAbility ability)
    {
        switch (ability)
        {
            case BardicAbility.Countersong:
                return "A Perform check each round; a creature within 30 ft that hears it may use the result in place of its save against a sonic or language-dependent magical attack. Up to 10 rounds.";
            case BardicAbility.Fascinate:
                return "One creature within 90 ft (one more per three levels above 1st) that can see and hear the bard: Will save against the bard's Perform check result or fascinated while the bard concentrates (up to 1 round per level). Nearby combat prevents it; an obvious threat breaks it.";
            case BardicAbility.InspireCourage:
                return "Allies who hear the bard (the bard included) gain a morale bonus on attack and weapon damage rolls and on saves against charm and fear: +1, +2 at 8th level. Lasts while they hear the song and 5 rounds after.";
            case BardicAbility.InspireCompetence:
                return "One ally within 30 ft who sees and hears the bard gains a +2 competence bonus on checks with one skill while the bard concentrates (up to 2 minutes). Not on the bard himself.";
            case BardicAbility.Suggestion:
                return "A suggestion (as the spell) to a creature the bard has fascinated: Will DC 10 + 1/2 bard level + Cha modifier negates. Costs no daily use and keeps the fascination.";
            case BardicAbility.InspireGreatness:
                return "One ally within 30 ft gains +2 bonus HD (d10s), +2 competence bonus on attack rolls, and +1 competence bonus on Fort saves (not implemented).";
            case BardicAbility.SongOfFreedom:
                return "Break enchantment (as the spell) on one creature within 30 ft (not implemented).";
            case BardicAbility.InspireHeroics:
                return "One ally within 30 ft gains +4 morale bonus on saving throws and +4 dodge bonus to AC (not implemented).";
            case BardicAbility.MassSuggestion:
                return "As Suggestion, but on any number of fascinated creatures (not implemented).";
            default: return "";
        }
    }
}

/// <summary>
/// One bard's bardic music state: uses spent today and the performance under way (PHB p.29-30). Pure data; the rules
/// that change it are <see cref="BardicMusicRules"/>, called by the PC Bardic Music button and the AI alike.
/// Uses per day = bard level; each performance started spends one use, however long it lasts (Suggestion spends none).
/// One performance at a time: starting another ends the current one (delegated RAW reading 2026-10-14, CHR-026).
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

    /// <summary>Rounds the Inspire Courage effect lasts after the bard stopped singing (PHB p.29: 5 rounds thereafter).</summary>
    public int LingerRoundsRemaining { get; private set; }

    /// <summary>Current bard level.</summary>
    public int BardLevel => _bardLevel;

    /// <summary>CHA modifier for DCs.</summary>
    public int CharismaModifier => _charismaModifier;

    /// <summary>Uses expended today.</summary>
    public int UsesExpended => _usesExpended;

    /// <summary>Maximum bardic music uses per day = Bard level (PHB p.29).</summary>
    public int MaxUsesPerDay => Mathf.Max(0, _bardLevel);

    /// <summary>Remaining uses today.</summary>
    public int RemainingUses => Mathf.Max(0, MaxUsesPerDay - _usesExpended);

    /// <summary>Whether the bard has a use left to start a performance.</summary>
    public bool CanPerform => RemainingUses > 0;

    /// <summary>Perform skill ranks (they gate each ability, PHB p.29).</summary>
    public int PerformRanks => _performRanks;

    // ── The performance under way (the runtime links are not serialized) ──

    /// <summary>Initiative counts of the bard that passed since the performance began (the round cap, BardicMusicRules).</summary>
    [NonSerialized] public int RoundsPerformed;
    /// <summary>Whether the bard spent a standard action on the performance this turn (start, concentrate, suggestion).</summary>
    [NonSerialized] public bool ConcentratedThisTurn;
    /// <summary>The last Perform check of the performance (countersong's result, fascinate's save DC).</summary>
    [NonSerialized] public int LastPerformCheck;
    /// <summary>Creatures the bard's Inspire Courage covers (while singing and lingering).</summary>
    [NonSerialized] public List<CharacterController> InspiredAllies = new List<CharacterController>();
    /// <summary>
    /// Per inspired creature that does not hear the song now: the bard's rounds since it stopped hearing it (Inspire
    /// Courage ends for it after 5, PHB p.29; BardicMusicRules).
    /// </summary>
    [NonSerialized] public Dictionary<CharacterController, int> NotHearingRounds = new Dictionary<CharacterController, int>();
    /// <summary>The bonus the bard's Inspire Courage gives (fixed when the song begins).</summary>
    [NonSerialized] public int InspireCourageValue;
    /// <summary>Creatures fascinated by the current Fascinate performance.</summary>
    [NonSerialized] public List<CharacterController> FascinatedTargets = new List<CharacterController>();
    /// <summary>Creatures that have already received a suggestion during the current fascination.</summary>
    [NonSerialized] public List<CharacterController> SuggestedTargets = new List<CharacterController>();
    /// <summary>The ally of the current Inspire Competence and the skill it helps.</summary>
    [NonSerialized] public CharacterController CompetenceAlly;
    [NonSerialized] public string CompetenceSkill;
    /// <summary>
    /// Creatures that saved against this bard's Fascinate: he cannot attempt them again for 24 hours (PHB p.29). Cleared by
    /// the day's rest.
    /// </summary>
    [NonSerialized] public List<CharacterController> FascinateResistedToday = new List<CharacterController>();

    /// <summary>Initialize or update when level/stats change.</summary>
    public void Initialize(int bardLevel, int charismaModifier, int performRanks = 0)
    {
        _bardLevel = bardLevel;
        _charismaModifier = charismaModifier;
        _performRanks = performRanks;
        EnsureLists();
    }

    private void EnsureLists()
    {
        InspiredAllies ??= new List<CharacterController>();
        NotHearingRounds ??= new Dictionary<CharacterController, int>();
        FascinatedTargets ??= new List<CharacterController>();
        SuggestedTargets ??= new List<CharacterController>();
        FascinateResistedToday ??= new List<CharacterController>();
    }

    // ─────────────────────────────────────────────
    // Ability Access
    // ─────────────────────────────────────────────

    /// <summary>Whether the bard has the ability: its bard level and Perform ranks (PHB p.29).</summary>
    public bool HasAbility(BardicAbility ability)
    {
        return _bardLevel >= BardicAbilityInfo.GetMinLevel(ability) && _performRanks >= BardicAbilityInfo.GetMinPerformRanks(ability);
    }

    /// <summary>Whether the bard's level alone unlocks the ability (the Perform ranks are checked by <see cref="HasAbility"/>).</summary>
    public bool HasLevelFor(BardicAbility ability) => _bardLevel >= BardicAbilityInfo.GetMinLevel(ability);

    /// <summary>Get all abilities the bard has at his level and Perform ranks.</summary>
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
    // Performance Management (data only; BardicMusicRules applies the effects)
    // ─────────────────────────────────────────────

    /// <summary>Spends one daily use (a performance started, or a deaf bard's failed attempt, PHB p.29).</summary>
    public bool SpendUse()
    {
        if (RemainingUses <= 0)
            return false;
        _usesExpended++;
        return true;
    }

    /// <summary>
    /// Start a bardic music performance: one daily use for the whole performance (PHB p.29: once per day per bard level).
    /// The bard's level must allow the ability. Returns true if started.
    /// </summary>
    public bool StartPerformance(BardicAbility ability)
    {
        if (!HasLevelFor(ability) || ability == BardicAbility.Suggestion || ability == BardicAbility.None)
        {
            Debug.Log($"[BardicMusic] Cannot start {BardicAbilityInfo.GetDisplayName(ability)} (bard level {_bardLevel})");
            return false;
        }
        if (!SpendUse())
        {
            Debug.Log($"[BardicMusic] Cannot perform — no uses remaining ({RemainingUses}/{MaxUsesPerDay})");
            return false;
        }

        BeginActive(ability);
        Debug.Log($"[BardicMusic] Started {BardicAbilityInfo.GetDisplayName(ability)} ({RemainingUses}/{MaxUsesPerDay} uses remaining)");
        return true;
    }

    /// <summary>Marks <paramref name="ability"/> as the performance under way (the use was spent by the caller).</summary>
    public void BeginActive(BardicAbility ability)
    {
        EnsureLists();
        ActiveAbility = ability;
        RoundsPerformed = 0;
        ConcentratedThisTurn = true;
        LastPerformCheck = 0;
        FascinatedTargets.Clear();
        SuggestedTargets.Clear();
        CompetenceAlly = null;
        CompetenceSkill = null;
        if (ability == BardicAbility.InspireCourage)
            LingerRoundsRemaining = 0;
    }

    /// <summary>
    /// Stop the current performance. Inspire Courage lingers for 5 rounds (PHB p.29); the other abilities end at once.
    /// </summary>
    public void StopPerformance()
    {
        if (!IsPerforming) return;

        if (BardicAbilityInfo.LingersAfterPerformance(ActiveAbility))
            LingerRoundsRemaining = 5; // PHB p.29: the effect lasts 5 rounds after the ally stops hearing the song

        Debug.Log($"[BardicMusic] Stopped {BardicAbilityInfo.GetDisplayName(ActiveAbility)}. Linger: {LingerRoundsRemaining} rounds.");
        ActiveAbility = BardicAbility.None;
        RoundsPerformed = 0;
        ConcentratedThisTurn = false;
        FascinatedTargets?.Clear();
        SuggestedTargets?.Clear();
        CompetenceAlly = null;
        CompetenceSkill = null;
    }

    /// <summary>Tick down linger duration (at the bard's initiative count).</summary>
    public void TickLingerRound()
    {
        if (LingerRoundsRemaining > 0)
            LingerRoundsRemaining--;
    }

    /// <summary>Ends a lingering Inspire Courage at once (end of the fight, the rest).</summary>
    public void ClearLinger() => LingerRoundsRemaining = 0;

    /// <summary>Whether an Inspire Courage effect is still active (singing it, or lingering).</summary>
    public bool IsEffectActive => IsPerforming || LingerRoundsRemaining > 0;

    // ─────────────────────────────────────────────
    // Effect Calculations
    // ─────────────────────────────────────────────

    /// <summary>
    /// Inspire Courage morale bonus to attack, weapon damage, and saves vs charm and fear (PHB p.29).
    /// +1 at L1, +2 at L8, +3 at L14, +4 at L20.
    /// </summary>
    public int InspireCourageBonus => BardClass.InspireCourageBonus(_bardLevel);

    /// <summary>Inspire Competence bonus (+2 competence on one ally's checks with one skill, PHB p.29).</summary>
    public int InspireCompetenceBonus => HasAbility(BardicAbility.InspireCompetence) ? 2 : 0;

    /// <summary>Number of targets for Fascinate (PHB p.29): 1 creature at L1, +1 per 3 levels after 1st.</summary>
    public int FascinateTargetCount => BardClass.FascinateTargets(_bardLevel);

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

    /// <summary>Inspire Greatness grants +2 bonus HD (d10s).</summary>
    public int InspireGreatnessBonusHD => HasAbility(BardicAbility.InspireGreatness) ? 2 : 0;

    /// <summary>Inspire Heroics: number of allies (1 at L15, +1 at L18, PHB p.29-30).</summary>
    public int InspireHeroicsTargetCount
    {
        get
        {
            if (_bardLevel < 15) return 0;
            return 1 + (_bardLevel - 15) / 3;
        }
    }

    /// <summary>Inspire Heroics grants +4 morale on saves and +4 dodge to AC.</summary>
    public int InspireHeroicsBonus => HasAbility(BardicAbility.InspireHeroics) ? 4 : 0;

    /// <summary>
    /// The Will save DC of the bard's Suggestion (and Mass Suggestion): 10 + 1/2 bard level + Cha modifier (PHB p.29).
    /// Fascinate's DC is the Perform check result instead.
    /// </summary>
    public int SuggestionDC => BardClass.BardicMusicDC(_bardLevel, _charismaModifier);

    /// <summary>Refresh all uses and end every performance (the day's rest).</summary>
    public void RefreshUses()
    {
        _usesExpended = 0;
        ActiveAbility = BardicAbility.None;
        LingerRoundsRemaining = 0;
        RoundsPerformed = 0;
        ConcentratedThisTurn = false;
        EnsureLists();
        InspiredAllies.Clear();
        NotHearingRounds.Clear();
        FascinatedTargets.Clear();
        SuggestedTargets.Clear();
        FascinateResistedToday.Clear();
        CompetenceAlly = null;
        CompetenceSkill = null;
    }

    /// <summary>Debug summary.</summary>
    public string GetDebugSummary()
    {
        var unlocked = GetUnlockedAbilities();
        string abilities = unlocked.Count > 0
            ? string.Join(", ", unlocked.ConvertAll(a => BardicAbilityInfo.GetDisplayName(a)))
            : "none";
        return $"Bard L{_bardLevel} (Perform {_performRanks}): {RemainingUses}/{MaxUsesPerDay} uses, " +
               $"IC+{InspireCourageBonus}, Suggestion DC {SuggestionDC}, abilities: {abilities}" +
               (IsPerforming ? $" [ACTIVE: {BardicAbilityInfo.GetDisplayName(ActiveAbility)}]" : "");
    }
}
