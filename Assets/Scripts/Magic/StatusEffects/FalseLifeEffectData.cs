using UnityEngine;
using DND35e.Identifiers;

// ============================================================================
// FalseLifeEffectData.cs — Temporary HP tracking for False Life spell
//
// D&D 3.5e PHB p.229:
//   False Life grants 1d10 + caster level (max +10) temporary hit points.
//   Duration: 1 hour/level or until discharged (all temp HP lost).
//   Temp HP are lost before regular HP. They cannot be healed.
//   Multiple castings of False Life do NOT stack — use the higher value.
//
// This system is designed to be general enough to support other temp HP
// sources in the future (Aid spell, Bear's Endurance, etc.).
// ============================================================================

/// <summary>
/// Runtime data for an active False Life (or other temp HP) effect on a character.
/// Tracks temp HP granted, remaining temp HP, caster level, and duration.
/// </summary>
[System.Serializable]
public class FalseLifeEffectData
{
    // ======================== CORE STATE ========================

    /// <summary>The original amount of temp HP granted when the spell was cast.</summary>
    public int GrantedTempHP;

    /// <summary>Current remaining temp HP (decreases as damage is absorbed).</summary>
    public int CurrentTempHP;

    /// <summary>Caster level at time of casting.</summary>
    public int CasterLevel;

    /// <summary>Remaining duration in combat rounds. -1 = permanent/until discharged.</summary>
    public int DurationRemainingRounds;

    /// <summary>Whether the effect is currently active.</summary>
    public bool IsActive;

    // ======================== SOURCE TRACKING ========================

    /// <summary>The spell ID that created this effect (e.g., "false_life").</summary>
    public string SourceSpellId;

    /// <summary>Human-readable name of the source spell.</summary>
    public string SourceName;

    /// <summary>Name of the caster who applied this effect.</summary>
    public string CasterName;

    /// <summary>Runtime reference to the caster (not serialized).</summary>
    [System.NonSerialized] public CharacterController Caster;

    // ======================== METHODS ========================

    /// <summary>
    /// Absorb damage from temp HP. Returns the amount of damage remaining
    /// after temp HP absorption (overflow to regular HP).
    /// If all temp HP are depleted, the effect is discharged.
    /// </summary>
    public int AbsorbDamage(int incomingDamage)
    {
        if (!IsActive || CurrentTempHP <= 0 || incomingDamage <= 0)
            return incomingDamage;

        if (incomingDamage <= CurrentTempHP)
        {
            CurrentTempHP -= incomingDamage;
            Debug.Log($"[FalseLife] Temp HP absorbed {incomingDamage} damage. Remaining: {CurrentTempHP}/{GrantedTempHP}");

            if (CurrentTempHP <= 0)
            {
                Discharge("all temp HP depleted by damage");
            }

            return 0;
        }
        else
        {
            int overflow = incomingDamage - CurrentTempHP;
            Debug.Log($"[FalseLife] Temp HP absorbed {CurrentTempHP} of {incomingDamage} damage. Overflow: {overflow}");
            CurrentTempHP = 0;
            Discharge("all temp HP depleted by damage");
            return overflow;
        }
    }

    /// <summary>
    /// Check if any temp HP remain from this effect.
    /// </summary>
    public bool HasTempHP => IsActive && CurrentTempHP > 0;

    /// <summary>
    /// Discharge the effect (temp HP fully depleted or duration expired).
    /// </summary>
    public void Discharge(string reason)
    {
        if (!IsActive) return;

        IsActive = false;
        CurrentTempHP = 0;
        Debug.Log($"[FalseLife] {SourceName} discharged: {reason}");
    }

    /// <summary>
    /// Expire the effect due to duration ending. Remaining temp HP are lost.
    /// </summary>
    public void ExpireDuration()
    {
        if (!IsActive) return;

        int remaining = CurrentTempHP;
        Discharge($"duration expired ({remaining} temp HP lost)");
    }

    /// <summary>
    /// Sets the caster reference and serializable name.
    /// </summary>
    public void SetCaster(CharacterController caster)
    {
        Caster = caster;
        CasterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : string.Empty;
    }

    // ======================== STATIC HELPERS ========================

    /// <summary>
    /// Calculate False Life temporary HP: 1d10 + min(casterLevel, 10).
    /// Maximum possible: 20 temp HP (10 on d10 + 10 CL bonus).
    /// </summary>
    public static int CalculateTempHP(int casterLevel)
    {
        int roll = DiceRoller.D10(); // 1d10
        int bonus = Mathf.Min(casterLevel, 10); // Cap at +10
        return roll + bonus;
    }

    /// <summary>
    /// Calculate False Life temp HP with a specific die roll (for testing).
    /// </summary>
    public static int CalculateTempHP(int casterLevel, int dieRoll)
    {
        int bonus = Mathf.Min(casterLevel, 10);
        return dieRoll + bonus;
    }

    // ======================== FACTORY METHODS ========================

    /// <summary>
    /// Factory: Creates a False Life spell effect with randomly rolled temp HP.
    /// PHB p.229: 1d10 + min(CL, 10) temp HP, 1 hour/level duration.
    /// </summary>
    public static FalseLifeEffectData CreateFalseLife(int casterLevel, CharacterController caster)
    {
        int tempHP = CalculateTempHP(casterLevel);
        int durationRounds = casterLevel * 600; // 1 hour/level = 600 rounds/level

        var data = new FalseLifeEffectData
        {
            GrantedTempHP = tempHP,
            CurrentTempHP = tempHP,
            CasterLevel = casterLevel,
            DurationRemainingRounds = durationRounds,
            IsActive = true,
            SourceSpellId = SpellNames.FALSE_LIFE,
            SourceName = "False Life"
        };
        data.SetCaster(caster);
        return data;
    }

    /// <summary>
    /// Factory: Creates a False Life effect with a specific temp HP amount (for testing/deterministic use).
    /// </summary>
    public static FalseLifeEffectData CreateFalseLifeWithAmount(int tempHP, int casterLevel, CharacterController caster)
    {
        int durationRounds = casterLevel * 600;

        var data = new FalseLifeEffectData
        {
            GrantedTempHP = tempHP,
            CurrentTempHP = tempHP,
            CasterLevel = casterLevel,
            DurationRemainingRounds = durationRounds,
            IsActive = true,
            SourceSpellId = SpellNames.FALSE_LIFE,
            SourceName = "False Life"
        };
        data.SetCaster(caster);
        return data;
    }

    /// <summary>
    /// Factory: Creates a generic temp HP effect from any source.
    /// Designed for future temp HP spells (Aid, Vampiric Touch, etc.).
    /// </summary>
    public static FalseLifeEffectData CreateGenericTempHP(string spellId, string sourceName, int tempHP,
        int casterLevel, int durationRounds, CharacterController caster)
    {
        var data = new FalseLifeEffectData
        {
            GrantedTempHP = tempHP,
            CurrentTempHP = tempHP,
            CasterLevel = casterLevel,
            DurationRemainingRounds = durationRounds,
            IsActive = true,
            SourceSpellId = spellId,
            SourceName = sourceName
        };
        data.SetCaster(caster);
        return data;
    }
}
