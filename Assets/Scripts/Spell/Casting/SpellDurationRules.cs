using UnityEngine;

/// <summary>
/// The one spell duration computation (SPL-002). Most cast paths, PC and NPC, reach it through
/// <see cref="ActiveSpellEffect.CalculateDurationRounds"/> (the tracked effect), <see cref="SpellCastingHelper.CalculateDuration(SpellData, int)"/>
/// and the handlers that set their own effect data. A few handlers still compute their own rounds (listed in SPL-038).
///
/// Rules (PHB p.176, "Duration"): a timed duration is a number of rounds, minutes, hours or days, often per caster level;
/// 1 round = 6 seconds, so 1 minute = 10 rounds, 1 hour = 600 rounds and 1 day = 14,400 rounds (PHB p.138). An
/// instantaneous spell has no duration to track; a permanent one lasts until dispelled; a concentration spell lasts
/// while the caster concentrates. The spell data says which (<see cref="SpellData.DurationType"/>, <see cref="SpellData.DurationValue"/>,
/// <see cref="SpellData.DurationScalesWithLevel"/>).
///
/// The legacy field <see cref="SpellData.BuffDurationRounds"/> no longer sets a duration: <see cref="NormalizeLegacyDuration"/>
/// runs at registration and turns a spell that set only that field into a fixed-rounds (or hours per level) duration, with a
/// warning, so no spell falls back to the Instantaneous default and gets 0 rounds.
/// </summary>
public static class SpellDurationRules
{
    public const int RoundsPerMinute = 10;
    public const int RoundsPerHour = 600;
    public const int RoundsPerDay = 14400;

    /// <summary>Sentinel: instantaneous, nothing to track.</summary>
    public const int InstantaneousRounds = 0;

    /// <summary>Sentinel: lasts until dispelled.</summary>
    public const int PermanentRounds = -1;

    /// <summary>Sentinel: lasts while the caster concentrates.</summary>
    public const int ConcentrationRounds = -2;

    /// <summary>
    /// The spell's duration in rounds at this caster level (minimum 1): the value for its unit, times the caster level when
    /// the duration is per level. Returns <see cref="InstantaneousRounds"/>, <see cref="PermanentRounds"/> or
    /// <see cref="ConcentrationRounds"/> for those durations.
    /// </summary>
    public static int Rounds(SpellData spell, int casterLevel)
    {
        if (spell == null)
            return InstantaneousRounds;

        int level = Mathf.Max(1, casterLevel);
        int value = spell.DurationValue;
        int perUnit;

        switch (spell.DurationType)
        {
            case DurationType.Instantaneous:
                return InstantaneousRounds;
            case DurationType.Permanent:
                return PermanentRounds;
            case DurationType.Concentration:
                return ConcentrationRounds;
            case DurationType.Rounds:
                perUnit = 1;
                break;
            case DurationType.Minutes:
                perUnit = RoundsPerMinute;
                break;
            case DurationType.MinutesPerLevel:
                // "1 min./level" written as its own unit: always per level, a missing value means 1.
                return Mathf.Max(1, value) * RoundsPerMinute * level;
            case DurationType.Hours:
                perUnit = RoundsPerHour;
                break;
            case DurationType.Days:
                perUnit = RoundsPerDay;
                break;
            default:
                return spell.BuffDurationRounds > 0 ? spell.BuffDurationRounds : InstantaneousRounds;
        }

        int rounds = value * perUnit;
        return spell.DurationScalesWithLevel ? rounds * level : rounds;
    }

    /// <summary>True when a timed duration (not instantaneous, permanent or concentration) is set.</summary>
    public static bool IsTimed(SpellData spell)
        => spell != null && Rounds(spell, 1) > 0;

    /// <summary>
    /// The caster level a duration uses: the cast's caster level (with the domain caster-level boosts), or the creature's
    /// level when it has no casting class (a spell-like or racial caster). Minimum 1.
    /// </summary>
    public static int CasterLevelFor(CharacterController caster, SpellData spell)
    {
        if (caster == null || caster.Stats == null)
            return 1;

        int casterLevel = caster.Stats.GetDomainBoostedCasterLevel(spell);
        if (casterLevel <= 0)
            casterLevel = caster.Stats.Level;
        return Mathf.Max(1, casterLevel);
    }

    /// <summary>A spell that set only the legacy BuffDurationRounds and left the duration fields at their defaults.</summary>
    public static bool HasLegacyOnlyDuration(SpellData spell)
        => spell != null
           && spell.DurationType == DurationType.Instantaneous
           && spell.DurationValue == 0
           && !spell.DurationScalesWithLevel
           && spell.BuffDurationRounds != 0;

    /// <summary>
    /// Turn a legacy-only duration into a real one: a positive BuffDurationRounds becomes that many rounds; a negative one,
    /// the legacy code for "hours per level", becomes 1 hour per level. Returns true when it changed the spell.
    /// </summary>
    public static bool NormalizeLegacyDuration(SpellData spell)
    {
        if (!HasLegacyOnlyDuration(spell))
            return false;

        if (spell.BuffDurationRounds > 0)
        {
            spell.DurationType = DurationType.Rounds;
            spell.DurationValue = spell.BuffDurationRounds;
            spell.DurationScalesWithLevel = false;
        }
        else
        {
            spell.DurationType = DurationType.Hours;
            spell.DurationValue = 1;
            spell.DurationScalesWithLevel = true;
        }
        return true;
    }

    /// <summary>
    /// The duration as the PHB writes it: "1 min./level", "1 round", "24 hours", "Permanent", or the spell's
    /// <see cref="SpellData.DurationText"/> for a rolled or compound duration ("1d6+2 rounds", "Concentration + 1 round/level").
    /// </summary>
    public static string Describe(SpellData spell)
    {
        if (spell == null)
            return "Instantaneous";
        if (!string.IsNullOrEmpty(spell.DurationText))
            return spell.DurationText;

        string perLevel = spell.DurationScalesWithLevel ? "/level" : "";
        int value = spell.DurationValue;
        switch (spell.DurationType)
        {
            case DurationType.Instantaneous: return "Instantaneous";
            case DurationType.Permanent: return "Permanent";
            case DurationType.Concentration: return "Concentration";
            case DurationType.Rounds: return value + (value == 1 ? " round" : " rounds") + perLevel;
            case DurationType.Minutes: return value + " min." + perLevel;
            case DurationType.MinutesPerLevel: return Mathf.Max(1, value) + " min./level";
            case DurationType.Hours: return value + (value == 1 ? " hour" : " hours") + perLevel;
            case DurationType.Days: return value + (value == 1 ? " day" : " days") + perLevel;
            default: return "Instantaneous";
        }
    }

    /// <summary>A round count as text: "3 rounds", "5 min.", "2 hours", "Permanent", "Concentration".</summary>
    public static string DescribeRounds(int rounds)
    {
        if (rounds == PermanentRounds) return "Permanent";
        if (rounds == ConcentrationRounds) return "Concentration";
        if (rounds <= 0) return "Instantaneous";
        if (rounds % RoundsPerHour == 0) return (rounds / RoundsPerHour) + (rounds == RoundsPerHour ? " hour" : " hours");
        if (rounds % RoundsPerMinute == 0 && rounds >= 2 * RoundsPerMinute) return (rounds / RoundsPerMinute) + " min.";
        return rounds + (rounds == 1 ? " round" : " rounds");
    }
}
