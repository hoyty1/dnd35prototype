using UnityEngine;
using DND35e.Identifiers;

// ============================================================================
// CommandUndeadEffectData.cs — Reusable Command Undead effect system
//
// D&D 3.5e PHB p.211 — Command Undead
//
// Grants the caster control over a single undead creature.
// Mechanics differ based on Intelligence:
//
// Nonintelligent Undead (Int — or 0):
//   • No saving throw; control is automatic (SR still applies)
//   • Only basic commands: come, go, fight, stand still
//   • Obeys all orders including suicidal/harmful
//
// Intelligent Undead (Int ≥ 1):
//   • Will save negates the spell entirely
//   • Perceives caster's words/actions most favorably (Friendly attitude)
//   • Will not attack caster while spell lasts
//   • Unusual orders require opposed Charisma check (no retries)
//   • Never obeys suicidal or obviously harmful orders
//
// Universal rules:
//   • Commands are verbal, not telepathic — undead must hear caster
//   • Threatening acts by caster or allies break the spell immediately
//   • No HD limit on target
//   • Multiple castings can control multiple undead simultaneously
//   • Duration: 1 day/level
// ============================================================================

/// <summary>
/// Runtime metadata for an active Command Undead spell effect on a single undead creature.
/// Tracks control state, intelligence classification, duration, and caster reference.
///
/// D&D 3.5e PHB Reference: p.211
/// </summary>
[System.Serializable]
public class CommandUndeadEffectData
{
    // ======================== CORE STATE ========================

    /// <summary>Whether the command control is currently active.</summary>
    public bool IsActive = true;

    /// <summary>
    /// Whether the target undead is intelligent (Int ≥ 1).
    /// Determines save eligibility, order complexity, and suicidal order refusal.
    /// </summary>
    public bool IsIntelligent;

    /// <summary>
    /// Remaining duration in combat rounds.
    /// 1 day = 14400 rounds (1 day = 24h × 60min × 10 rounds/min).
    /// Duration = casterLevel × 14400 rounds.
    /// </summary>
    public int DurationRemainingRounds;

    /// <summary>The caster level used when casting the spell.</summary>
    public int CasterLevel;

    // ======================== SOURCE TRACKING ========================

    /// <summary>The spell ID that created this effect.</summary>
    public string SourceSpellId = SpellNames.COMMAND_UNDEAD;

    /// <summary>Human-readable source name for combat log messages.</summary>
    public string SourceName = "Command Undead";

    // ======================== CASTER TRACKING ========================

    /// <summary>Runtime reference to the caster (not serialized).</summary>
    [System.NonSerialized] public CharacterController Caster;

    /// <summary>Serializable caster name for persistence and logging.</summary>
    public string CasterName;

    // ======================== TARGET TRACKING ========================

    /// <summary>Runtime reference to the controlled undead (not serialized).</summary>
    [System.NonSerialized] public CharacterController ControlledUndead;

    /// <summary>Serializable target name for persistence and logging.</summary>
    public string ControlledUndeadName;

    // ======================== CONSTANTS ========================

    /// <summary>Number of combat rounds in one day (24h × 60min × 10 rounds/min).</summary>
    public const int ROUNDS_PER_DAY = 14400;

    // ======================== QUERY METHODS ========================

    /// <summary>
    /// Returns true if the controlled undead can receive commands.
    /// Commands require: active control + undead can hear caster (verbal, not telepathic).
    /// </summary>
    public bool CanReceiveCommands()
    {
        return IsActive && ControlledUndead != null && Caster != null;
    }

    /// <summary>
    /// Returns true if the undead would obey a suicidal or obviously harmful order.
    /// PHB p.211: Nonintelligent undead obey unconditionally; intelligent undead never obey.
    /// </summary>
    public bool WouldObeySuicidalOrder()
    {
        if (!IsActive) return false;
        return !IsIntelligent; // Nonintelligent: obeys; Intelligent: refuses
    }

    /// <summary>
    /// Returns true if an unusual order requires an opposed Charisma check.
    /// PHB p.211: Only intelligent undead require CHA checks for unusual orders.
    /// </summary>
    public bool RequiresCharismaCheckForOrder()
    {
        return IsActive && IsIntelligent;
    }

    /// <summary>
    /// Returns the number of remaining days of control (approximate).
    /// </summary>
    public float GetRemainingDays()
    {
        if (DurationRemainingRounds <= 0) return 0f;
        return (float)DurationRemainingRounds / ROUNDS_PER_DAY;
    }

    /// <summary>
    /// Ticks the duration by one round. Returns true if the effect is still active.
    /// </summary>
    public bool TickRound()
    {
        if (!IsActive) return false;

        if (DurationRemainingRounds > 0)
        {
            DurationRemainingRounds--;
            if (DurationRemainingRounds <= 0)
            {
                BreakControl("Duration expired");
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Breaks the command control immediately. Called when:
    /// - Caster or allies threaten the commanded undead
    /// - Duration expires
    /// - Spell is dispelled
    /// - Caster dies
    /// </summary>
    public void BreakControl(string reason = null)
    {
        if (!IsActive) return;

        IsActive = false;
        string undeadName = ControlledUndeadName ?? "Unknown";
        string casterName = CasterName ?? "Unknown";
        string reasonStr = string.IsNullOrEmpty(reason) ? "unknown reason" : reason;
        Debug.Log($"[CommandUndead] Control over {undeadName} by {casterName} broken: {reasonStr}");
    }

    // ======================== CASTER/TARGET SETUP ========================

    /// <summary>Sets the caster reference and serializable name.</summary>
    public void SetCaster(CharacterController caster)
    {
        Caster = caster;
        CasterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : string.Empty;
    }

    /// <summary>Sets the controlled undead reference and serializable name.</summary>
    public void SetControlledUndead(CharacterController undead)
    {
        ControlledUndead = undead;
        ControlledUndeadName = undead != null && undead.Stats != null ? undead.Stats.CharacterName : string.Empty;
    }

    // ======================== FACTORY METHODS ========================

    /// <summary>
    /// Factory: Creates a Command Undead effect for a nonintelligent undead.
    /// No saving throw required. Duration = casterLevel days.
    /// </summary>
    public static CommandUndeadEffectData CreateForNonintelligent(
        CharacterController caster,
        CharacterController target,
        int casterLevel)
    {
        var data = new CommandUndeadEffectData
        {
            IsActive = true,
            IsIntelligent = false,
            DurationRemainingRounds = casterLevel * ROUNDS_PER_DAY,
            CasterLevel = casterLevel,
            SourceSpellId = SpellNames.COMMAND_UNDEAD,
            SourceName = "Command Undead"
        };
        data.SetCaster(caster);
        data.SetControlledUndead(target);
        return data;
    }

    /// <summary>
    /// Factory: Creates a Command Undead effect for an intelligent undead.
    /// Will save was already failed at this point. Duration = casterLevel days.
    /// Intelligent undead treat caster as Friendly but refuse suicidal orders.
    /// </summary>
    public static CommandUndeadEffectData CreateForIntelligent(
        CharacterController caster,
        CharacterController target,
        int casterLevel)
    {
        var data = new CommandUndeadEffectData
        {
            IsActive = true,
            IsIntelligent = true,
            DurationRemainingRounds = casterLevel * ROUNDS_PER_DAY,
            CasterLevel = casterLevel,
            SourceSpellId = SpellNames.COMMAND_UNDEAD,
            SourceName = "Command Undead"
        };
        data.SetCaster(caster);
        data.SetControlledUndead(target);
        return data;
    }
}
