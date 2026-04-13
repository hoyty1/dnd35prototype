using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages spell concentration mechanics per D&D 3.5e PHB rules.
///
/// Core Rules:
///   - A character can only concentrate on one spell at a time.
///   - Casting a new concentration spell ends the previous one.
///   - Casting ANY spell while concentrating requires a Concentration check
///     (DC 15 + spell level of the NEW spell) or the concentration spell ends.
///   - Taking damage requires a Concentration check:
///     DC = 10 + damage dealt + spell level of concentration spell.
///   - Concentration can be voluntarily ended (free action).
///   - If the check fails or the character is killed/knocked unconscious,
///     the concentration spell ends immediately.
///
/// Concentration Check:
///   Roll = d20 + caster level + primary casting stat modifier
///   (WIS for Cleric/Druid, INT for Wizard, CHA for Sorcerer/Bard)
///
/// This component is attached to each character alongside StatusEffectManager.
/// </summary>
public class ConcentrationManager : MonoBehaviour
{
    // ========== CONCENTRATION STATE ==========

    /// <summary>The spell currently being concentrated on (null if none).</summary>
    public ActiveSpellEffect ConcentratingOn { get; private set; }

    /// <summary>The character who is the caster maintaining concentration.</summary>
    public CharacterController Caster { get; private set; }

    /// <summary>Reference to the character's stats.</summary>
    private CharacterStats _stats;

    /// <summary>Reference to the StatusEffectManager on this character.</summary>
    private StatusEffectManager _statusEffectMgr;

    /// <summary>Whether this character has the Combat Casting feat (+4 to concentration checks).</summary>
    private bool _hasCombatCasting;

    // ========== INITIALIZATION ==========

    /// <summary>Initialize with character references.</summary>
    public void Init(CharacterStats stats, CharacterController caster)
    {
        _stats = stats;
        Caster = caster;
        _statusEffectMgr = GetComponent<StatusEffectManager>();

        // Check for Combat Casting feat
        _hasCombatCasting = stats.HasFeat("Combat Casting");
    }

    /// <summary>Whether this character is currently concentrating on a spell.</summary>
    public bool IsConcentrating => ConcentratingOn != null;

    /// <summary>Get the name of the spell being concentrated on.</summary>
    public string ConcentrationSpellName => ConcentratingOn?.Spell?.Name ?? "";

    /// <summary>Get the spell level of the concentration spell.</summary>
    public int ConcentrationSpellLevel => ConcentratingOn?.Spell?.SpellLevel ?? 0;

    // ========== CONCENTRATION MANAGEMENT ==========

    /// <summary>
    /// Begin concentrating on a spell effect. If already concentrating,
    /// the previous concentration spell ends automatically.
    /// Called after a concentration spell is successfully cast.
    /// </summary>
    /// <param name="effect">The ActiveSpellEffect to concentrate on.</param>
    /// <returns>Log message describing what happened.</returns>
    public string BeginConcentration(ActiveSpellEffect effect)
    {
        if (effect == null || effect.Spell == null) return "";

        string log = "";

        // If already concentrating, end the previous spell
        if (IsConcentrating)
        {
            string prevSpell = ConcentrationSpellName;
            EndConcentration(silent: true);
            log += $"🔴 {_stats.CharacterName} stops concentrating on {prevSpell}.\n";
        }

        ConcentratingOn = effect;
        log += $"🔵 {_stats.CharacterName} concentrates on {effect.Spell.Name}.";
        Debug.Log($"[Concentration] {_stats.CharacterName} begins concentrating on {effect.Spell.Name}");

        return log;
    }

    /// <summary>
    /// End concentration voluntarily or due to failure.
    /// Removes the concentration spell effect from the character it was applied to.
    /// </summary>
    /// <param name="silent">If true, don't log (used when immediately replacing).</param>
    /// <returns>Log message describing what happened.</returns>
    public string EndConcentration(bool silent = false)
    {
        if (!IsConcentrating) return "";

        string spellName = ConcentrationSpellName;
        var effect = ConcentratingOn;
        ConcentratingOn = null;

        // Remove the effect from whoever it was applied to
        // The effect tracks its target via AffectedCharacterName
        RemoveConcentrationEffect(effect);

        string log = "";
        if (!silent)
        {
            log = $"🔴 {_stats.CharacterName} ends concentration on {spellName}.";
            Debug.Log($"[Concentration] {_stats.CharacterName} ends concentration on {spellName}");
        }

        return log;
    }

    /// <summary>
    /// Perform a concentration check when the caster takes damage.
    /// DC = 10 + damage dealt + spell level of concentration spell.
    /// </summary>
    /// <param name="damageTaken">Amount of damage dealt to the caster.</param>
    /// <returns>A ConcentrationCheckResult with the outcome and log message.</returns>
    public ConcentrationCheckResult CheckConcentrationOnDamage(int damageTaken)
    {
        if (!IsConcentrating)
            return new ConcentrationCheckResult { Success = true, LogMessage = "" };

        int spellLevel = ConcentrationSpellLevel;
        int dc = 10 + damageTaken + spellLevel;

        return PerformConcentrationCheck(dc, $"taking {damageTaken} damage");
    }

    /// <summary>
    /// Perform a concentration check when casting another spell while concentrating.
    /// DC = 15 + spell level of the NEW spell being cast.
    /// Note: In D&D 3.5e, casting a new concentration spell automatically ends
    /// the old one. This check is for casting a NON-concentration spell while
    /// maintaining an existing concentration.
    /// </summary>
    /// <param name="newSpellLevel">Spell level of the spell being cast.</param>
    /// <returns>A ConcentrationCheckResult with the outcome and log message.</returns>
    public ConcentrationCheckResult CheckConcentrationOnCasting(int newSpellLevel)
    {
        if (!IsConcentrating)
            return new ConcentrationCheckResult { Success = true, LogMessage = "" };

        int dc = 15 + newSpellLevel;

        return PerformConcentrationCheck(dc, $"casting a spell");
    }

    /// <summary>
    /// Perform a concentration check for vigorous motion (e.g., riding).
    /// DC = 10 + spell level.
    /// </summary>
    public ConcentrationCheckResult CheckConcentrationVigorousMotion()
    {
        if (!IsConcentrating)
            return new ConcentrationCheckResult { Success = true, LogMessage = "" };

        int dc = 10 + ConcentrationSpellLevel;
        return PerformConcentrationCheck(dc, "vigorous motion");
    }

    /// <summary>
    /// Perform a concentration check for violent motion (e.g., storm, galloping).
    /// DC = 15 + spell level.
    /// </summary>
    public ConcentrationCheckResult CheckConcentrationViolentMotion()
    {
        if (!IsConcentrating)
            return new ConcentrationCheckResult { Success = true, LogMessage = "" };

        int dc = 15 + ConcentrationSpellLevel;
        return PerformConcentrationCheck(dc, "violent motion");
    }

    /// <summary>
    /// Perform a concentration check for being entangled.
    /// DC = 15 + spell level.
    /// </summary>
    public ConcentrationCheckResult CheckConcentrationEntangled()
    {
        if (!IsConcentrating)
            return new ConcentrationCheckResult { Success = true, LogMessage = "" };

        int dc = 15 + ConcentrationSpellLevel;
        return PerformConcentrationCheck(dc, "being entangled");
    }

    /// <summary>
    /// Force break concentration (e.g., death, unconsciousness).
    /// No check — automatically ends.
    /// </summary>
    /// <returns>Log message.</returns>
    public string ForceBreakConcentration(string reason)
    {
        if (!IsConcentrating) return "";

        string spellName = ConcentrationSpellName;
        EndConcentration(silent: true);

        string log = $"💥 {_stats.CharacterName}'s concentration on {spellName} is broken ({reason})!";
        Debug.Log($"[Concentration] {log}");
        return log;
    }

    // ========== PRIVATE HELPERS ==========

    /// <summary>
    /// Perform the actual concentration check roll.
    /// Roll = d20 + caster level + primary casting stat modifier.
    /// Combat Casting feat adds +4.
    /// </summary>
    private ConcentrationCheckResult PerformConcentrationCheck(int dc, string reason)
    {
        string spellName = ConcentrationSpellName;
        int spellLevel = ConcentrationSpellLevel;

        // Get the Concentration skill bonus
        // In D&D 3.5e, Concentration is a skill (CON-based), not caster level + casting stat
        // But since our skill system may not have Concentration fully ranked,
        // we use: d20 + Concentration skill ranks + CON modifier
        // If no ranks, fallback to: d20 + caster level + primary casting stat mod
        int concentrationBonus = GetConcentrationBonus();
        int combatCastingBonus = _hasCombatCasting ? 4 : 0;

        int d20 = Random.Range(1, 21);
        int totalRoll = d20 + concentrationBonus + combatCastingBonus;
        bool success = totalRoll >= dc;

        // Build detailed log message
        string bonusBreakdown = GetBonusBreakdown(concentrationBonus, combatCastingBonus);
        string resultStr = success ? "<color=#88FF88>Success!</color>" : "<color=#FF4444>Failed!</color>";

        var result = new ConcentrationCheckResult();
        result.Success = success;
        result.D20Roll = d20;
        result.TotalBonus = concentrationBonus + combatCastingBonus;
        result.TotalRoll = totalRoll;
        result.DC = dc;
        result.SpellName = spellName;

        // Format: "Aldric takes 8 damage! Concentration check (Bless): DC 19"
        //         "Rolls 15 + 3 (CON) + 1 (ranks) = 19. Success! Concentration maintained."
        string logLine1 = $"⚡ Concentration check ({spellName}) — DC {dc} ({reason})";
        string logLine2 = $"  Rolls d20({d20}) {bonusBreakdown} = {totalRoll}. {resultStr}";

        if (success)
        {
            result.LogMessage = $"{logLine1}\n{logLine2}\n  ✅ Concentration maintained on {spellName}.";
            Debug.Log($"[Concentration] {_stats.CharacterName}: Check DC {dc} — rolled {totalRoll} — SUCCESS (maintaining {spellName})");
        }
        else
        {
            // Concentration fails — end the spell
            EndConcentration(silent: true);
            result.LogMessage = $"{logLine1}\n{logLine2}\n  ❌ Concentration on {spellName} is broken!";
            Debug.Log($"[Concentration] {_stats.CharacterName}: Check DC {dc} — rolled {totalRoll} — FAILED ({spellName} ends)");
        }

        return result;
    }

    /// <summary>
    /// Get the total Concentration skill bonus.
    /// D&D 3.5e: Concentration is CON-based.
    /// Uses skill ranks if available, otherwise falls back to CON modifier + caster level.
    /// </summary>
    private int GetConcentrationBonus()
    {
        if (_stats == null) return 0;

        // Try to use the Concentration skill if it has ranks
        if (_stats.Skills.ContainsKey("Concentration"))
        {
            var skill = _stats.Skills["Concentration"];
            if (skill.Ranks > 0)
            {
                // Ranks + CON modifier (+ class skill bonus if applicable)
                int conMod = CharacterStats.GetModifier(_stats.CON);
                return skill.Ranks + conMod + skill.ClassSkillBonus;
            }
        }

        // Fallback: CON modifier + half caster level (untrained skill use)
        int fallbackConMod = CharacterStats.GetModifier(_stats.CON);
        return fallbackConMod;
    }

    /// <summary>Build a human-readable breakdown of the concentration bonus.</summary>
    private string GetBonusBreakdown(int concentrationBonus, int combatCastingBonus)
    {
        var parts = new List<string>();

        if (_stats.Skills.ContainsKey("Concentration") && _stats.Skills["Concentration"].Ranks > 0)
        {
            var skill = _stats.Skills["Concentration"];
            int conMod = CharacterStats.GetModifier(_stats.CON);
            if (skill.Ranks > 0) parts.Add($"+ {skill.Ranks}(ranks)");
            if (conMod != 0) parts.Add($"+ {conMod}(CON)");
            if (skill.ClassSkillBonus > 0) parts.Add($"+ {skill.ClassSkillBonus}(class)");
        }
        else
        {
            int conMod = CharacterStats.GetModifier(_stats.CON);
            if (conMod != 0) parts.Add($"+ {conMod}(CON)");
        }

        if (combatCastingBonus > 0) parts.Add($"+ {combatCastingBonus}(Combat Casting)");

        return parts.Count > 0 ? string.Join(" ", parts) : "";
    }

    /// <summary>
    /// Remove the concentration spell effect from the target's StatusEffectManager.
    /// The effect could be on the caster themselves or on another target.
    /// </summary>
    private void RemoveConcentrationEffect(ActiveSpellEffect effect)
    {
        if (effect == null || effect.Spell == null) return;

        // First, try to remove from this character's own StatusEffectManager
        if (_statusEffectMgr != null && _statusEffectMgr.ActiveEffects.Contains(effect))
        {
            _statusEffectMgr.RemoveEffect(effect);
            return;
        }

        // If the effect is on another character, we need to find them
        // Search all characters in the scene for this effect
        var allManagers = FindObjectsOfType<StatusEffectManager>();
        foreach (var mgr in allManagers)
        {
            if (mgr.ActiveEffects.Contains(effect))
            {
                mgr.RemoveEffect(effect);
                return;
            }
        }

        Debug.LogWarning($"[Concentration] Could not find StatusEffectManager containing effect {effect.Spell.Name}");
    }

    /// <summary>
    /// Called when the character dies or becomes unconscious.
    /// Automatically breaks concentration.
    /// </summary>
    public void OnCharacterIncapacitated()
    {
        if (IsConcentrating)
        {
            ForceBreakConcentration("incapacitated");
        }
    }

    /// <summary>
    /// Get a display string for the UI showing concentration status.
    /// </summary>
    public string GetConcentrationDisplayString()
    {
        if (!IsConcentrating) return "";
        return $"<color=#44AAFF>🔵 Concentrating: {ConcentrationSpellName}</color>";
    }
}

/// <summary>
/// Result of a concentration check.
/// </summary>
public class ConcentrationCheckResult
{
    /// <summary>Whether the check succeeded.</summary>
    public bool Success;

    /// <summary>The d20 roll value.</summary>
    public int D20Roll;

    /// <summary>Total bonus added to the roll.</summary>
    public int TotalBonus;

    /// <summary>Total roll result (d20 + bonus).</summary>
    public int TotalRoll;

    /// <summary>The DC that needed to be met.</summary>
    public int DC;

    /// <summary>Name of the spell that was being concentrated on.</summary>
    public string SpellName;

    /// <summary>Formatted log message for the combat log.</summary>
    public string LogMessage;
}
