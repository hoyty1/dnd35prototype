using UnityEngine;

/// <summary>
/// Represents an active spell effect on a character with duration tracking.
/// Stores the spell data, remaining duration, source caster info, and stat modifications
/// so they can be properly reversed when the effect expires.
///
/// D&D 3.5e Duration Rules:
///   1 round = 6 seconds
///   1 minute = 10 rounds
///   1 hour = 600 rounds
/// </summary>
[System.Serializable]
public class ActiveSpellEffect
{
    /// <summary>The spell that created this effect.</summary>
    public SpellData Spell;

    /// <summary>Name of the caster who applied this effect.</summary>
    public string CasterName;

    /// <summary>Caster level at the time of casting (used for level-scaled durations).</summary>
    public int CasterLevel;

    /// <summary>Remaining duration in combat rounds. -1 = permanent/indefinite.</summary>
    public int RemainingRounds;

    /// <summary>The duration type from the spell definition.</summary>
    public DurationType DurationType;

    /// <summary>Name of the affected character (for logging).</summary>
    public string AffectedCharacterName;

    // ========== STORED STAT MODIFICATIONS ==========
    // These track exactly what was applied so we can reverse it on expiration.

    public int AppliedAttackBonus;
    public int AppliedDamageBonus;
    public int AppliedSaveBonus;
    public int AppliedACBonus;         // Spell AC bonus (e.g., Mage Armor)
    public int AppliedShieldBonus;
    public int AppliedDeflectionBonus;
    public int AppliedTempHP;
    public string AppliedStatName;     // e.g., "STR", "DEX"
    public int AppliedStatBonus;

    /// <summary>LEGACY: The bonus type string for backward compatibility.</summary>
    public string BonusTypeLegacy;

    /// <summary>D&D 3.5e bonus type enum for proper stacking rule enforcement.</summary>
    public BonusType BonusTypeEnum;

    /// <summary>Whether this effect has been fully applied to the character's stats.</summary>
    public bool IsApplied;

    public ActiveSpellEffect() { }

    public ActiveSpellEffect(SpellData spell, string casterName, int casterLevel, string affectedName)
    {
        Spell = spell;
        CasterName = casterName;
        CasterLevel = casterLevel;
        AffectedCharacterName = affectedName;
        DurationType = spell.DurationType;
        BonusTypeLegacy = spell.BuffType ?? "";
        BonusTypeEnum = spell.GetEffectiveBonusType();
        RemainingRounds = CalculateDurationRounds(spell, casterLevel);
    }

    /// <summary>
    /// Calculate the duration in rounds based on spell duration type and caster level.
    /// D&D 3.5e conversions:
    ///   Rounds: use DurationValue directly
    ///   Minutes: DurationValue × 10 rounds (× casterLevel if DurationScalesWithLevel)
    ///   Hours: DurationValue × 600 rounds (× casterLevel if DurationScalesWithLevel)
    ///   Instantaneous: 0 (no tracking needed)
    ///   Permanent: -1 (until dispelled)
    ///   Concentration: -2 (special handling)
    /// </summary>
    public static int CalculateDurationRounds(SpellData spell, int casterLevel)
    {
        if (spell == null) return 0;

        int baseValue = spell.DurationValue;
        int level = Mathf.Max(1, casterLevel);

        switch (spell.DurationType)
        {
            case DurationType.Instantaneous:
                return 0;

            case DurationType.Rounds:
                if (spell.DurationScalesWithLevel)
                    return baseValue * level;
                return baseValue;

            case DurationType.Minutes:
                // 1 minute = 10 rounds
                int minuteRounds = baseValue * 10;
                if (spell.DurationScalesWithLevel)
                    return minuteRounds * level;
                return minuteRounds;

            case DurationType.Hours:
                // 1 hour = 600 rounds
                int hourRounds = baseValue * 600;
                if (spell.DurationScalesWithLevel)
                    return hourRounds * level;
                return hourRounds;

            case DurationType.Permanent:
                return -1; // Until dispelled

            case DurationType.Concentration:
                return -2; // Special: lasts while concentrating

            default:
                // Fallback: use BuffDurationRounds if set
                return spell.BuffDurationRounds > 0 ? spell.BuffDurationRounds : 0;
        }
    }

    /// <summary>
    /// Tick this effect by 1 round. Returns true if the effect has expired.
    /// Permanent (-1) and Concentration (-2) effects are not ticked.
    /// </summary>
    public bool Tick()
    {
        if (RemainingRounds < 0) return false; // Permanent or concentration
        if (RemainingRounds <= 0) return true;  // Already expired

        RemainingRounds--;
        return RemainingRounds <= 0;
    }

    /// <summary>
    /// Get a display string showing remaining duration in human-readable format.
    /// Converts rounds back to minutes/hours for readability when appropriate.
    /// </summary>
    public string GetDurationDisplayString()
    {
        if (RemainingRounds == -1) return "Permanent";
        if (RemainingRounds == -2) return "Concentration";
        if (RemainingRounds <= 0) return "Expired";

        if (RemainingRounds >= 600)
        {
            int hours = RemainingRounds / 600;
            int remainingMinutes = (RemainingRounds % 600) / 10;
            if (remainingMinutes > 0)
                return $"{hours}h {remainingMinutes}m";
            return $"{hours}h";
        }
        else if (RemainingRounds >= 20) // 2+ minutes, show in minutes
        {
            int minutes = RemainingRounds / 10;
            int remainingRounds = RemainingRounds % 10;
            if (remainingRounds > 0)
                return $"{minutes}m {remainingRounds}rd";
            return $"{minutes}m";
        }
        else
        {
            return $"{RemainingRounds}rd";
        }
    }

    /// <summary>Get a compact display string for UI: "SpellName (duration)"</summary>
    public string GetDisplayString()
    {
        string spellName = Spell != null ? Spell.Name : "Unknown";
        return $"{spellName} ({GetDurationDisplayString()})";
    }

    /// <summary>Get a detailed log string.</summary>
    public string GetDetailedString()
    {
        string spellName = Spell != null ? Spell.Name : "Unknown";
        string mods = "";
        if (AppliedAttackBonus != 0) mods += $" Atk:{AppliedAttackBonus:+#;-#}";
        if (AppliedDamageBonus != 0) mods += $" Dmg:{AppliedDamageBonus:+#;-#}";
        if (AppliedSaveBonus != 0) mods += $" Save:{AppliedSaveBonus:+#;-#}";
        if (AppliedACBonus != 0) mods += $" AC:{AppliedACBonus:+#;-#}";
        if (AppliedShieldBonus != 0) mods += $" Shield:{AppliedShieldBonus:+#;-#}";
        if (AppliedDeflectionBonus != 0) mods += $" Defl:{AppliedDeflectionBonus:+#;-#}";
        if (AppliedTempHP != 0) mods += $" TempHP:{AppliedTempHP}";
        if (!string.IsNullOrEmpty(AppliedStatName)) mods += $" {AppliedStatName}:{AppliedStatBonus:+#;-#}";

        string typeStr = BonusTypeEnum != BonusType.Untyped ? $" ({BonusTypeHelper.GetDisplayName(BonusTypeEnum)})" : "";
        return $"{spellName}{typeStr} [{GetDurationDisplayString()}] from {CasterName}{mods}";
    }
}

/// <summary>
/// Duration type for spells following D&D 3.5e rules.
/// </summary>
public enum DurationType
{
    /// <summary>Spell effect is immediate, no duration tracking needed (e.g., Magic Missile, Cure spells).</summary>
    Instantaneous,

    /// <summary>Duration measured in combat rounds (1 round = 6 seconds).</summary>
    Rounds,

    /// <summary>Duration measured in minutes (1 minute = 10 rounds).</summary>
    Minutes,

    /// <summary>Duration measured in hours (1 hour = 600 rounds).</summary>
    Hours,

    /// <summary>Effect is permanent until dispelled.</summary>
    Permanent,

    /// <summary>Effect lasts while the caster maintains concentration.</summary>
    Concentration
}
