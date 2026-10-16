using UnityEngine;

/// <summary>
/// The one spell duration computation (SPL-002). Most cast paths, PC and NPC, reach it through
/// <see cref="ActiveSpellEffect.CalculateDurationRounds"/> (the tracked effect), <see cref="SpellCastingHelper.CalculateDuration(SpellData, int)"/>
/// and the handlers that set their own effect data from the cast's spell clone, so Extend doubles them. A duration a handler
/// computes itself (a rolled or fixed part) goes through <see cref="Extended"/> (batch 11 step 19, SPL-038).
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
    /// Whether Extend Spell changes the spell (PHB p.94: a spell with a duration of concentration, instantaneous or
    /// permanent is not affected): any other duration type, a rolled one (Hypnotism's 2d4 rounds) included, and the timed
    /// part of a compound duration line: the timed part of an "Instantaneous/..." duration
    /// (<see cref="SpellData.HasTimedAftereffect"/>, Death Knell's 10 minutes per HD) and the "+N rounds" a
    /// "Concentration + N rounds" spell keeps going after the concentration ends (<see cref="SpellData.ConcentrationTailRounds"/>:
    /// Rainbow Pattern, Hypnotic Pattern, Summon Swarm; PHB p.176 calls that tail the given length of time the spell keeps
    /// going), while the concentration part is unchanged (delegated RAW reading 2026-10-14, RULES_COVERAGE 4.1). The one
    /// test of the offer (MetamagicData.CanExtend) and the effect (SpellCaster.ApplyMetamagicToSpellData) (batch 11
    /// step 19, SPL-095).
    /// </summary>
    public static bool IsExtendable(SpellData spell)
    {
        if (spell == null)
            return false;
        if (spell.HasTimedAftereffect)
            return true;
        if (spell.DurationType == DurationType.Concentration && spell.ConcentrationTailRounds > 0)
            return true;
        return spell.DurationType != DurationType.Instantaneous
               && spell.DurationType != DurationType.Permanent
               && spell.DurationType != DurationType.Concentration;
    }

    /// <summary>True when the cast's spell clone carries Extend Spell (stamped by SpellCaster.ApplyMetamagicToSpellData) and Extend changes it.</summary>
    public static bool IsExtended(SpellData spell)
        => spell != null && spell.MetamagicDataRef != null && spell.MetamagicDataRef.Has(MetamagicFeatId.ExtendSpell) && IsExtendable(spell);

    /// <summary>
    /// A duration a handler computes itself, not from the spell's duration fields (a rolled duration such as Cause Fear's
    /// 1d4 rounds, a fixed part such as Scare's 1 round on a save, Death Knell's 10 minutes per HD): doubled when the
    /// cast is extended (PHB p.94; batch 11 step 19, SPL-038). A duration from <see cref="Rounds"/> must not pass here:
    /// ApplyMetamagicToSpellData has already doubled the clone's DurationValue.
    /// </summary>
    public static int Extended(SpellData spell, int rounds)
        => rounds > 0 && IsExtended(spell) ? rounds * 2 : rounds;

    /// <summary>
    /// The caster level a duration uses: the cast's caster level (CasterLevelRules.ForCaster: a scroll's or wand's own,
    /// else the casting class's with the domain caster-level boosts, half for a paladin or ranger, or the Hit Dice of a
    /// creature with no casting class; batch 11 step 14, SPL-017). Minimum 1.
    /// </summary>
    public static int CasterLevelFor(CharacterController caster, SpellData spell)
        => CasterLevelRules.ForCaster(caster, spell);

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
